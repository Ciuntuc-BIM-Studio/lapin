using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Mesh;
using Lapin.Mesh.Fragments;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Recognition from triangles (decision 214): a Fragments file holds only triangles, yet its walls (with their doors
/// and windows), slabs and columns come back as Lapin elements, by the same recognisers as an IFC file's; what none
/// takes stays the reference it was, in the file's group.
/// </summary>
public class MeshRecognitionTests
{
    static (Fixture House, string Wall, string Door, string Window, string Slab) House()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var wall = f.Wall(0, 0, 8, 0);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 2 })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 5.5 })!["id"]!;
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 1, ["x2"] = 8, ["y2"] = 6 })!["id"]!;
        return (f, wall, door, window, slab);
    }

    static byte[] Frag(Fixture f) => FragWriter.Write(MeshScene.Build(f.Session, new MeshExportOptions("frag", Origin: "internal")));

    static readonly FidelityOptions Report = new(FidelityOptions.Report);

    /// <summary>
    /// What the recognisers read from a Fragments file (the gate reporting only, docs/spec-ifc.md, Poarta de fidelitate):
    /// the wall, its door and window, the slab.
    /// </summary>
    [Fact]
    public void A_Fragments_file_comes_back_as_walls_with_their_doors_and_windows_and_slabs()
    {
        var (h, wall, door, window, slab) = House();
        var (lo, hi) = h.Bounds(wall);
        var g = new Fixture(new ArchitectureModule(), new StructureModule(), new StudioModule());
        var r = FragImport.Run(g.Session, Frag(h), "house.frag", out _, new FragImportOptions(Recognize: true, Fidelity: Report));
        Assert.True(r.Recognition!.Recognized >= 2, $"recognized {r.Recognition.Recognized}: {r.Recognition.ToJson().ToJsonString()}");
        // The wall: a Lapin wall again, on its axis, as long and as high as it was, with its openings.
        var w = g.Model.Get(wall)!;
        Assert.Equal(IfcClass.Wall, w.Class);
        Assert.False(StoredBodies.Has(w));
        var (lo2, hi2) = g.Bounds(wall);
        Assert.Equal(lo.X, lo2.X, 2); Assert.Equal(hi.X, hi2.X, 2); Assert.Equal(hi.Z, hi2.Z, 2);
        Assert.Equal(2, g.Model.Relations.Count(x => x.Class == IfcClass.RelVoidsElement && x.Relating == wall));
        // Its door and window filled their openings, natively, keeping their ids.
        Assert.False(StoredBodies.Has(g.Model.Get(door)!));
        Assert.False(StoredBodies.Has(g.Model.Get(window)!));
        // The slab too.
        Assert.False(StoredBodies.Has(g.Model.Get(slab)!));
        // Everything still in the file's group, what the file said of it kept.
        var group = Assert.Single(Groups.All(g.Model));
        Assert.Contains(wall, Groups.MembersOf(g.Model, group.Id));
        Assert.NotNull(g.Model.Get(wall)!.Attrs["imported"]);
    }

    /// <summary>
    /// The same file with the gate enforcing (the default): the door's jambs are rebated in the file (the frame's 15 mm step in
    /// the masonry), the recognised opening a rectangle, so the wall is not the file's and comes back the reference it was,
    /// its door and window with it; the slab, the file's, stays native.
    /// </summary>
    [Fact]
    public void Enforced_a_wall_whose_rebated_jambs_recognition_squares_stays_the_reference_it_was()
    {
        var (h, wall, door, window, slab) = House();
        var g = new Fixture(new ArchitectureModule(), new StructureModule(), new StudioModule());
        var r = FragImport.Run(g.Session, Frag(h), "house.frag", out _, new FragImportOptions(Recognize: true));
        var gate = r.Recognition!.Fidelity!;
        Assert.Equal(FidelityOptions.Enforce, gate.Options.Mode);
        var check = gate.Checks.Single(c => c.Id == wall);
        Assert.True(check.Reverted);
        Assert.Contains("base outline", check.Reason);                    // the rebated jambs, 15–20 mm in plan
        foreach (var id in new[] { wall, door, window }) Assert.True(StoredBodies.Has(g.Model.Get(id)!), id);
        Assert.False(StoredBodies.Has(g.Model.Get(slab)!));
        Assert.True(gate.Checks.Single(c => c.Id == slab).Pass);
        // Still in the file's group, as it came.
        Assert.Contains(wall, Groups.MembersOf(g.Model, Assert.Single(Groups.All(g.Model)).Id));
    }

    [Fact]
    public void As_references_the_file_stays_triangles_and_what_no_recogniser_takes_comes_back_as_it_was()
    {
        var (h, wall, _, _, _) = House();
        var g = new Fixture(new ArchitectureModule(), new StructureModule());
        FragImport.Run(g.Session, Frag(h), "house.frag", out _);
        Assert.True(StoredBodies.Has(g.Model.Get(wall)!));
        // A body no recogniser takes (an octahedron, from a mesh file, in that file's group): offered, it comes back as it was.
        var obj = "o Gem\nv 0 0 1\nv 1 0 0\nv 0 1 0\nv -1 0 0\nv 0 -1 0\nv 0 0 -1\nf 1 2 3\nf 1 3 4\nf 1 4 5\nf 1 5 2\nf 6 3 2\nf 6 4 3\nf 6 5 4\nf 6 2 5\n";
        MeshImport.Run(g.Session, System.Text.Encoding.UTF8.GetBytes(obj), "gem.obj", new JsonObject { ["up"] = "z" }, null, out _);
        var gem = g.Model.Entities.Single(e => e.Str("source") == "gem.obj");
        var group = Groups.All(g.Model).Single(x => x.Name == "gem");
        MeshRecognitionReport report = MeshRecognitionReport.None;
        g.Session.Edit("Recognise", edit => report = MeshRecognition.Recognize(edit, g.Session.Catalog, [gem.Id]));
        Assert.Equal(0, report.Recognized);
        Assert.True(StoredBodies.Has(g.Model.Get(gem.Id)!));
        Assert.Contains(gem.Id, Groups.MembersOf(g.Model, group.Id));
        Assert.NotNull(Spatial.StoreyOf(g.Model, gem.Id));
    }

    /// <summary>A slab with sloping edges: the recogniser reads it by its top face; not the file's body, the gate keeps it a reference.</summary>
    [Fact]
    public void A_slab_with_sloping_edges_is_read_by_its_top_face()
    {
        // A slab as another program meshes it: a 6 × 4 m top at 0.2, its edges sloping in to a 5.6 × 3.6 m bottom at 0.
        var top = new[] { (0.0, 0.0), (6, 0), (6, 4), (0, 4) };
        var bottom = new[] { (0.2, 0.2), (5.8, 0.2), (5.8, 3.8), (0.2, 3.8) };
        var obj = new System.Text.StringBuilder("o Slab\n");
        foreach (var (x, y) in bottom) obj.Append($"v {x} {y} 0\n");
        foreach (var (x, y) in top) obj.Append($"v {x} {y} 0.2\n");
        foreach (var (a, b, c) in new[] { (1, 3, 2), (1, 4, 3), (5, 6, 7), (5, 7, 8), (1, 2, 6), (1, 6, 5), (2, 3, 7), (2, 7, 6), (3, 4, 8), (3, 8, 7), (4, 1, 5), (4, 5, 8) })
            obj.Append($"f {a} {b} {c}\n");
        var g = new Fixture(new ArchitectureModule(), new StructureModule());
        MeshImport.Run(g.Session, System.Text.Encoding.UTF8.GetBytes(obj.ToString()), "slab.obj", new JsonObject { ["up"] = "z" }, null, out _);
        var e = g.Model.Entities.Single(x => x.Str("source") == "slab.obj");
        // A mesh comes as a proxy: told it is a slab (as a Fragments or IFC file says), it is read as one.
        g.Session.Edit("As a slab", edit => edit.Replace(e.With(attrs: Set(e.CloneAttrs(), "exportAs", IfcClass.Slab))));
        MeshRecognitionReport report = MeshRecognitionReport.None;
        // The gate enforcing (the default): its top swept down is not the file's sloping edges, it stays the reference it was.
        g.Session.Edit("Recognise", edit => report = MeshRecognition.Recognize(edit, g.Session.Catalog, [e.Id]));
        Assert.Equal(0, report.Recognized);
        Assert.True(StoredBodies.Has(g.Model.Get(e.Id)!));
        var check = Assert.Single(report.Fidelity!.Checks);
        Assert.True(check.Reverted);
        // Its edges sloping in to the bottom, 200 mm each way: the bottom's outline 283 mm off at its corners, the volume 8.8 % more.
        Assert.Equal("volume +8.83 %; base outline 282.8 mm", check.Reason);
        // Reporting only, the recogniser's reading: the top's 6 × 4, 0.2 thick.
        g.Session.Edit("Recognise", edit => report = MeshRecognition.Recognize(edit, g.Session.Catalog, [e.Id], Report));
        Assert.Equal(1, report.Recognized);
        var slab = g.Model.Get(e.Id)!;
        Assert.Equal(IfcClass.Slab, slab.Class);
        var (lo, hi) = g.Bounds(slab.Id);
        Assert.Equal(6, hi.X - lo.X, 3); Assert.Equal(4, hi.Y - lo.Y, 3); Assert.Equal(0.2, hi.Z - lo.Z, 3);
    }

    static JsonObject Set(JsonObject o, string k, string v) { o[k] = v; return o; }

    /// <summary>A box meshed as another program does, its four top corners at their own heights, told it is a column.</summary>
    static Entity Column(Fixture g, string name, (double X, double Y)[] bottom, (double X, double Y, double Z)[] top)
    {
        var obj = new System.Text.StringBuilder($"o {name}\n");
        foreach (var (x, y) in bottom) obj.Append($"v {x} {y} 0\n");
        foreach (var (x, y, z) in top) obj.Append($"v {x} {y} {z}\n");
        foreach (var (a, b, c) in new[] { (1, 3, 2), (1, 4, 3), (5, 6, 7), (5, 7, 8), (1, 2, 6), (1, 6, 5), (2, 3, 7), (2, 7, 6), (3, 4, 8), (3, 8, 7), (4, 1, 5), (4, 5, 8) })
            obj.Append($"f {a} {b} {c}\n");
        MeshImport.Run(g.Session, System.Text.Encoding.UTF8.GetBytes(obj.ToString()), name + ".obj", new JsonObject { ["up"] = "z" }, null, out _);
        var e = g.Model.Entities.Single(x => x.Str("source") == name + ".obj");
        g.Session.Edit("As a column", edit => edit.Replace(e.With(attrs: Set(e.CloneAttrs(), "exportAs", IfcClass.Column))));
        return e;
    }

    /// <summary>A column cut at its top by a slope: the recogniser reads it by its bottom face, flat at its highest point; not the file's body, the gate keeps it a reference.</summary>
    [Fact]
    public void A_column_cut_at_its_top_by_a_slope_is_read_by_its_bottom_face()
    {
        var g = new Fixture(new ArchitectureModule(), new StructureModule());
        // 400 × 300 at (2, 1), its top falling from 3.0 to 2.8 along x (under a sloping beam): no prism standing, one lying.
        var cut = Column(g, "cut", [(2, 1), (2.4, 1), (2.4, 1.3), (2, 1.3)], [(2, 1, 3.0), (2.4, 1, 2.8), (2.4, 1.3, 2.8), (2, 1.3, 3.0)]);
        // A tapered one, 400 × 300 at the foot and 200 × 200 at the head: another shape, it stays a reference.
        var tapered = Column(g, "tapered", [(6, 1), (6.4, 1), (6.4, 1.3), (6, 1.3)], [(6.1, 1.05, 3), (6.3, 1.05, 3), (6.3, 1.25, 3), (6.1, 1.25, 3)]);
        MeshRecognitionReport report = MeshRecognitionReport.None;
        // The gate enforcing (the default): flat at 3 m where the file's top falls to 2.8, it stays the reference it was.
        g.Session.Edit("Recognise", edit => report = MeshRecognition.Recognize(edit, g.Session.Catalog, [cut.Id, tapered.Id]));
        Assert.Equal(0, report.Recognized);
        Assert.True(StoredBodies.Has(g.Model.Get(cut.Id)!));
        var check = Assert.Single(report.Fidelity!.Checks);
        Assert.True(check.Reverted);
        // As high as the file's at its highest corner, the box the same: the volume says it (a wedge 0.4 × 0.3 × 0.2 more, +3.4 %).
        Assert.Equal(0.012, check.NativeVolume!.Value - check.FileVolume!.Value, 4);
        Assert.StartsWith("volume +3.4", check.Reason);
        // Reporting only, the recogniser's reading.
        g.Session.Edit("Recognise", edit => report = MeshRecognition.Recognize(edit, g.Session.Catalog, [cut.Id, tapered.Id], Report));
        Assert.Equal(1, report.Recognized);
        var column = g.Model.Get(cut.Id)!;
        Assert.Equal(IfcClass.Column, column.Class);
        Assert.False(StoredBodies.Has(column));
        // Its section the bottom's, up to the top's highest point.
        var (lo, hi) = g.Bounds(column.Id);
        Assert.Equal(2, lo.X, 3); Assert.Equal(2.4, hi.X, 3); Assert.Equal(1, lo.Y, 3); Assert.Equal(1.3, hi.Y, 3);
        Assert.Equal(0, lo.Z, 3); Assert.Equal(3, hi.Z, 3);
        Assert.True(StoredBodies.Has(g.Model.Get(tapered.Id)!));
    }

    [Fact]
    public void A_long_footing_from_a_file_is_a_strip_foundation_along_its_middle()
    {
        var dir = Directory.CreateTempSubdirectory("lapin-strip").FullName;
        try
        {
            var x = new IfcImportRulesTests.Plant();
            var id = x.Cuboid("IFCFOOTING", ".STRIP_FOOTING.", "SF-1", 2, 1, -1, 10, 0.6, 0.5);
            var path = x.Bytes(dir);
            var g = new Fixture(new ArchitectureModule(), new StructureModule());
            var r = IfcImport.Run(g.Session, path, null, out _);
            Assert.Equal(1, r.Recognized);
            var f = g.Model.Get(id)!;
            Assert.Equal("STRIP_FOOTING", f.PredefinedType);
            var (lo, hi) = g.Bounds(id);
            Assert.Equal(2, lo.X, 3); Assert.Equal(12, hi.X, 3);
            Assert.Equal(1, lo.Y, 3); Assert.Equal(1.6, hi.Y, 3);
            Assert.Equal(-1, lo.Z, 3); Assert.Equal(-0.5, hi.Z, 3);
        }
        finally { Directory.Delete(dir, true); }
    }
}
