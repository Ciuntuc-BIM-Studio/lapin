using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Mesh;
using Lapin.Mesh.Fragments;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// An import's third way (decision 210, docs/spec-ifc.md, Importul): everything editable. What recognition leaves a
/// reference becomes a Studio solid of its triangles, the same element (its id, name, class or the class it exports
/// as, its sets, its storey, its group), which takes push/pull at once; a body over the limit stays a reference and
/// the import says why. IFC, Fragments and meshes alike.
/// </summary>
public class ImportEditableTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-editable").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture Make() => new(new ArchitectureModule(), new StructureModule(), new StudioModule());

    static (Vec3 Lo, Vec3 Hi) Box(Fixture f, string id)
    {
        var v = f.Session.Geometry(id)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        return (new(v.Min(p => p.X), v.Min(p => p.Y), v.Min(p => p.Z)), new(v.Max(p => p.X), v.Max(p => p.Y), v.Max(p => p.Z)));
    }

    [Fact]
    public void An_IFC_file_comes_in_editable_its_elements_the_same_ones_made_Studio_solids()
    {
        var x = new IfcImportRulesTests.Plant();
        var plate = x.Box("plate", 0, 0, 0, 2, false, ("Pset_Plant", "Tag", "P-1"));
        var a = x.Box("bolt-a", 10, 0, 0, 0.5);
        var b = x.Box("bolt-b", 12, 0, 0, 0.5);
        var pipe = x.Element("IFCPIPESEGMENT", ".RIGIDSEGMENT.", "pipe", 20, 0, 0, 1);
        var path = x.Bytes(_dir);

        var f = Make();
        var r = IfcImport.Run(f.Session, path, new IfcImportOptions(Group: true, Editable: true), out _);
        Assert.Equal(4, r.Editable!.Made);
        Assert.Empty(r.Editable.Kept);
        Assert.Equal(4, (int)r.ToJson()["editable"]!["made"]!);
        foreach (var id in new[] { plate, a, b, pipe })
        {
            var e = f.Model.Get(id)!;
            // The same element (its GlobalId), now a Studio solid of its own body, on its storey and in the file's group.
            Assert.Equal("body", e.Str("operation"));
            Assert.False(StoredBodies.Has(e));
            Assert.NotNull(Spatial.StoreyOf(f.Model, id));
            Assert.Single(Groups.All(f.Model), g => Groups.MembersOf(f.Model, g.Id).Contains(id));
        }
        // A class Studio does not draw is kept as the one it exports as.
        var p = f.Model.Get(pipe)!;
        Assert.Equal(IfcClass.BuildingElementProxy, p.Class);
        Assert.Equal("IfcPipeSegment", p.Str("exportAs"));
        // Where they were; the shared bolts each where its own was.
        var (lo, hi) = Box(f, b);
        Assert.Equal(12, lo.X, 6); Assert.Equal(12.5, hi.X, 6); Assert.Equal(0.5, hi.Z, 6);
        // What the file said of it stays with it.
        Assert.Contains("P-1", f.Model.Get(plate)!.Attrs.ToJsonString());
        // And it is editable: its top pulled up by a metre.
        f.Run("studio.pushPull", new JsonObject { ["id"] = plate, ["point"] = new JsonArray(1, 1, 2), ["normal"] = new JsonArray(0, 0, 1), ["distance"] = 1 });
        Assert.Equal(3, Box(f, plate).Hi.Z, 6);
        // One undo takes the whole import back.
        f.Session.Undo();
        f.Session.Undo();
        Assert.Null(f.Model.Get(plate));
    }

    [Fact]
    public void A_body_over_the_limit_stays_a_reference_and_says_why()
    {
        var x = new IfcImportRulesTests.Plant();
        var plate = x.Box("plate", 0, 0, 0, 2);
        var path = x.Bytes(_dir);
        var f = Make();
        IfcImport.Run(f.Session, path, null, out _);
        Assert.True(StoredBodies.Has(f.Model.Get(plate)!));
        EditableReport report = EditableReport.None;
        f.Session.Edit("Make editable", edit => report = EditableReferences.Make(edit, f.Session.Catalog, [plate], maxTriangles: 6));
        Assert.Equal(0, report.Made);
        Assert.Equal(1, report.Kept["more than 6 triangles"]);
        Assert.True(StoredBodies.Has(f.Model.Get(plate)!));
        // Without the limit it is made a solid; recognition's way (the default) leaves it a reference.
        f.Session.Edit("Make editable", edit => report = EditableReferences.Make(edit, f.Session.Catalog, [plate]));
        Assert.Equal(1, report.Made);
        Assert.Equal("body", f.Model.Get(plate)!.Str("operation"));
    }

    [Fact]
    public void Fragments_and_meshes_come_in_editable_on_request()
    {
        // A house exported as Fragments, brought into another project: its wall and columns made solids, exported as what they were.
        var h = new Fixture(new ArchitectureModule(), new StructureModule());
        h.Wall(0, 0, 6, 0);
        var wallId = h.Model.OfClass(IfcClass.Wall).Single().Id;
        var bytes = FragWriter.Write(MeshScene.Build(h.Session, new MeshExportOptions("frag", Origin: "internal")));
        var g = Make();
        var r = FragImport.Run(g.Session, bytes, "house.frag", out _, new FragImportOptions(Editable: true));
        Assert.Equal(1, r.Editable!.Made);
        var wall = g.Model.Get(wallId)!;
        Assert.Equal("body", wall.Str("operation"));
        Assert.Equal(IfcClass.Wall, wall.Str("exportAs"));

        // An OBJ cube: a solid at once.
        var obj = new StringBuilder("o Cube\n");
        foreach (var c in new[] { (0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1) }) obj.Append($"v {c.Item1} {c.Item3} {-c.Item2}\n");
        foreach (var t in new[] { (0, 2, 1), (0, 3, 2), (4, 5, 6), (4, 6, 7), (0, 1, 5), (0, 5, 4), (1, 2, 6), (1, 6, 5), (2, 3, 7), (2, 7, 6), (3, 0, 4), (3, 4, 7) }) obj.Append($"f {t.Item1 + 1} {t.Item2 + 1} {t.Item3 + 1}\n");
        var m = MeshImport.Run(g.Session, Encoding.UTF8.GetBytes(obj.ToString()), "cube.obj", new JsonObject { ["editable"] = true }, null, out _);
        Assert.Equal(1, m.Editable!.Made);
        Assert.Contains(g.Model.Entities, e => e.Str("source") == "cube.obj" && e.Str("operation") == "body");
    }
}
