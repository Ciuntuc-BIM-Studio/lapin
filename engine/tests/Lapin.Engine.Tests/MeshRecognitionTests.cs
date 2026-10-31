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
/// Recognition from triangles (decision 207): a Fragments file holds only triangles, yet its walls (with their doors
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

    [Fact]
    public void A_Fragments_file_comes_back_as_walls_with_their_doors_and_windows_and_slabs()
    {
        var (h, wall, door, window, slab) = House();
        var (lo, hi) = h.Bounds(wall);
        var g = new Fixture(new ArchitectureModule(), new StructureModule(), new StudioModule());
        var r = FragImport.Run(g.Session, Frag(h), "house.frag", out _, new FragImportOptions(Recognize: true));
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
}
