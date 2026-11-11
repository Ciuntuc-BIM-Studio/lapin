using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Mesh;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// References (a mesh's furniture, an IFC file's elements Lapin did not recognise) moved, turned, mirrored, copied, raised and
/// arrayed like any element (decision 448): the body stays the file's, only where it stands changes; copies share it.
/// </summary>
public class ReferenceMoveTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-refmove").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    /// <summary>A 1 × 2 × 0.5 m box from the origin, its faces outward, Z up.</summary>
    const string Box = "o box\nv 0 0 0\nv 1 0 0\nv 1 2 0\nv 0 2 0\nv 0 0 0.5\nv 1 0 0.5\nv 1 2 0.5\nv 0 2 0.5\n"
        + "f 1 4 3 2\nf 5 6 7 8\nf 1 2 6 5\nf 3 4 8 7\nf 1 5 8 4\nf 2 3 7 6\n";

    static (Fixture F, string Id) Imported()
    {
        var f = new Fixture(new ArchitectureModule());
        MeshImport.Run(f.Session, Encoding.UTF8.GetBytes(Box), "box.obj", new JsonObject { ["up"] = "z" }, null, out _);
        var id = f.Model.Entities.Single(e => StoredBodies.Has(e)).Id;
        return (f, id);
    }

    static JsonArray Ids(params string[] ids) => new([.. ids.Select(x => (JsonNode)x)]);

    static void Near(Vec3 expected, Vec3 actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Z, actual.Z, 6);
    }

    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.SignedVolume(p.Solid));

    [Fact]
    public void A_reference_moves_and_rises_its_body_shared_and_undone_in_one_step()
    {
        var (f, id) = Imported();
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(id), ["op"] = "move", ["by"] = new JsonArray(2, 3), ["up"] = 1 });
        var (min, max) = f.Bounds(id);
        Near(new Vec3(2, 3, 1), min);
        Near(new Vec3(3, 5, 1.5), max);
        // Its body went to a shared one, the element placed by its numbers.
        var e = f.Model.Get(id)!;
        Assert.NotNull(StoredBodies.InstanceOf(e));
        Assert.Null(e.Attrs[StoredBodies.Key]);
        Assert.Single(f.Model.OfClass(StoredBodies.SharedClass));
        f.Session.Undo();
        Near(new Vec3(0, 0, 0), f.Bounds(id).Min);
        Assert.Empty(f.Model.OfClass(StoredBodies.SharedClass));
    }

    [Fact]
    public void The_group_a_mesh_file_came_in_as_moves_and_turns_whole()
    {
        var (f, id) = Imported();
        var group = Assert.Single(Groups.All(f.Model)).Id;
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "rotate", ["center"] = new JsonArray(0, 0), ["angle"] = 90 });
        var (min, max) = f.Bounds(id);
        Near(new Vec3(-2, 0, 0), min);
        Near(new Vec3(0, 1, 0.5), max);
        Assert.Equal(1.0, Volume(f, id), 6);
    }

    [Fact]
    public void Copies_share_the_body_and_the_last_one_deleted_takes_it()
    {
        var (f, id) = Imported();
        var copy = (string)f.Run("element.transform", new JsonObject { ["ids"] = Ids(id), ["op"] = "copy", ["by"] = new JsonArray(5, 0) })!["ids"]![0]!;
        Near(new Vec3(5, 0, 0), f.Bounds(copy).Min);
        Near(new Vec3(0, 0, 0), f.Bounds(id).Min);
        var shared = Assert.Single(f.Model.OfClass(StoredBodies.SharedClass)).Id;
        Assert.Equal(shared, StoredBodies.InstanceOf(f.Model.Get(id)!)?.Body);
        Assert.Equal(shared, StoredBodies.InstanceOf(f.Model.Get(copy)!)?.Body);
        Assert.Equal(f.Model.RelatingOf(id, IfcClass.RelContainedInSpatialStructure)?.Id, f.Model.RelatingOf(copy, IfcClass.RelContainedInSpatialStructure)?.Id);
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(id) });
        Assert.Single(f.Model.OfClass(StoredBodies.SharedClass));
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(copy) });
        Assert.Empty(f.Model.OfClass(StoredBodies.SharedClass));
    }

    [Fact]
    public void A_mirrored_reference_takes_a_body_of_its_own_still_outward()
    {
        var (f, id) = Imported();
        // Shared first (a move), then mirrored in the line x = 0.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(id), ["op"] = "move", ["by"] = new JsonArray(1, 0) });
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(id), ["op"] = "mirror", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(0, 1) });
        var (min, max) = f.Bounds(id);
        Near(new Vec3(-2, 0, 0), min);
        Near(new Vec3(-1, 2, 0.5), max);
        var e = f.Model.Get(id)!;
        Assert.Null(StoredBodies.InstanceOf(e));
        Assert.NotNull(e.Attrs[StoredBodies.Key]);
        // Not inside out: its faces still face out.
        Assert.Equal(1.0, Volume(f, id), 6);
        // A mirrored copy of it too.
        var copy = (string)f.Run("element.transform", new JsonObject { ["ids"] = Ids(id), ["op"] = "mirror", ["copy"] = true, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(1, 0) })!["ids"]![0]!;
        Near(new Vec3(-2, -2, 0), f.Bounds(copy).Min);
        Assert.Equal(1.0, Volume(f, copy), 6);
    }

    [Fact]
    public void An_array_copies_in_a_row_or_round_a_centre_in_one_step()
    {
        var (f, id) = Imported();
        var row = f.Run("element.array", new JsonObject { ["ids"] = Ids(id), ["count"] = 4, ["step"] = new JsonArray(1.5, 0, 0.25) })!["ids"]!.AsArray();
        Assert.Equal(3, row.Count);
        Near(new Vec3(4.5, 0, 0.75), f.Bounds((string)row[2]!).Min);
        Assert.Single(f.Model.OfClass(StoredBodies.SharedClass));
        f.Session.Undo();
        Assert.Single(f.Model.Entities, StoredBodies.Has);

        var polar = f.Run("element.array", new JsonObject { ["ids"] = Ids(id), ["kind"] = "polar", ["count"] = 4, ["angle"] = 360, ["center"] = new JsonArray(0, 0) })!["ids"]!.AsArray();
        Assert.Equal(3, polar.Count);
        // The third copy turned 270° (a full turn shared out by four): x 0 to 2, y −1 to 0.
        var (min, max) = f.Bounds((string)polar[2]!);
        Near(new Vec3(0, -1, 0), min);
        Near(new Vec3(2, 0, 0.5), max);
        Assert.Throws<CommandException>(() => f.Run("element.array", new JsonObject { ["ids"] = Ids(id), ["count"] = 1, ["step"] = new JsonArray(1, 0, 0) }));
        Assert.Throws<CommandException>(() => f.Run("element.array", new JsonObject { ["ids"] = Ids(id), ["count"] = 3, ["step"] = new JsonArray(0, 0, 0) }));
    }

    [Fact]
    public void An_array_works_on_any_element()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 4, 0);
        var made = f.Run("element.array", new JsonObject { ["ids"] = Ids(wall), ["count"] = 3, ["step"] = new JsonArray(0, 3, 0) })!["ids"]!.AsArray();
        Assert.Equal(2, made.Count);
        Assert.Equal(6, Args.PointOf(f.Model.Get((string)made[1]!)!.Attrs["start"]).Y, 6);
    }

    [Fact]
    public void Moved_and_copied_references_are_written_to_IFC_where_they_stand_each_GlobalId_once()
    {
        var (f, id) = Imported();
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(id), ["op"] = "rotate", ["center"] = new JsonArray(0, 0), ["angle"] = 30 });
        f.Run("element.array", new JsonObject { ["ids"] = Ids(id), ["count"] = 3, ["step"] = new JsonArray(2, 0, 0) });
        var path = Path.Combine(_dir, "boxes.ifc");
        IfcExport.Write(f.Session, path);
        var text = File.ReadAllText(path);
        Assert.Equal(3, Regex.Matches(text, "IFCBUILDINGELEMENTPROXY\\(").Count);
        Assert.Contains("IFCMAPPEDITEM(", text);
        var gids = Regex.Matches(text, @"=\s*IFC[A-Z0-9]+\('([0-9A-Za-z_$]{22})'").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(gids.Count, gids.Distinct().Count());
        // Opened again, the copies stand where they were put.
        var g = new Fixture(new ArchitectureModule());
        g.Session.New(plain: true);
        IfcImport.Run(g.Session, path, null, out _);
        var xs = g.Model.Entities.Where(StoredBodies.Has).Select(e => Math.Round(g.Bounds(e.Id).Min.X, 3)).OrderBy(x => x).ToList();
        Assert.Equal(3, xs.Count);
        Assert.Equal(2, xs[1] - xs[0], 3);
        Assert.Equal(2, xs[2] - xs[1], 3);
    }
}
