using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>Moving, turning, mirroring and copying elements in plan (element.transform): joins kept, hosted openings and doors carried, one undo step.</summary>
public class PlacementTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static Vec2 P(JsonNode? n) => new(Json.Number(n![0]) ?? 0, Json.Number(n[1]) ?? 0);
    static JsonNode? T(Fixture f, JsonObject a) => f.Run("element.transform", a);
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void A_moved_wall_keeps_its_corner_its_neighbour_s_end_follows()
    {
        var f = F();
        var w1 = f.Wall(0, 0, 5, 0);
        var w2 = f.Wall(5, 0, 5, 4);
        Assert.NotEmpty(f.Model.RelationsOf(w1, IfcClass.RelConnectsPathElements));
        T(f, new JsonObject { ["ids"] = new JsonArray(w2), ["op"] = "move", ["by"] = J(1, 0) });
        Assert.Equal(new Vec2(6, 0), P(f.Model.Get(w1)!.Attrs["end"]));
        Assert.Equal(new Vec2(6, 0), P(f.Model.Get(w2)!.Attrs["start"]));
        f.Session.Undo();
        Assert.Equal(new Vec2(5, 0), P(f.Model.Get(w1)!.Attrs["end"]));
    }

    [Fact]
    public void A_column_turns_about_a_point_and_a_floor_mirrors_with_its_opening()
    {
        var f = F();
        var col = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 0 })!["id"]!;
        T(f, new JsonObject { ["ids"] = new JsonArray(col), ["op"] = "rotate", ["center"] = J(0, 0), ["angle"] = 90 });
        var c = f.Model.Get(col)!;
        Assert.Equal(0, P(c.Attrs["at"]).X, 9);
        Assert.Equal(2, P(c.Attrs["at"]).Y, 9);
        Assert.Equal(Math.PI / 2, c.Num("rotation"), 9);

        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 2 })!["id"]!;
        var hole = (string)f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["x1"] = 1, ["y1"] = 0.5, ["x2"] = 2, ["y2"] = 1.5 })!["id"]!;
        var before = Vol(f, slab);
        T(f, new JsonObject { ["ids"] = new JsonArray(slab), ["op"] = "mirror", ["a"] = J(0, 0), ["b"] = J(0, 1) });
        Assert.Equal(before, Vol(f, slab), 6);
        var contour = Slabs.Contour(f.Model.Get(slab)!);
        Assert.True(Polygon2.SignedArea(contour) > 0);
        Assert.Equal(-4, contour.Min(p => p.X), 9);
        Assert.Equal(-2, Slabs.Contour(f.Model.Get(hole)!).Min(p => p.X), 9);
    }

    [Fact]
    public void A_copied_wall_takes_its_type_its_own_layers_and_its_door()
    {
        var f = F();
        var w = f.Wall(0, 0, 5, 0);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = w, ["x"] = 2.5, ["y"] = 0 });
        var copies = T(f, new JsonObject { ["ids"] = new JsonArray(w), ["op"] = "copy", ["by"] = J(0, 3) })!["ids"]!.AsArray();
        var c = (string)copies.Single()!;
        Assert.Equal(new Vec2(0, 3), P(f.Model.Get(c)!.Attrs["start"]));
        Assert.Equal(f.Model.RelatingOf(w, IfcClass.RelDefinesByType)!.Id, f.Model.RelatingOf(c, IfcClass.RelDefinesByType)!.Id);
        Assert.NotEqual(Walls.UsageOf(f.Model, w)!.Id, Walls.UsageOf(f.Model, c)!.Id);
        Assert.Equal(2, f.Model.OfClass(IfcClass.Door).Count());
        Assert.Equal(Vol(f, w), Vol(f, c), 6);
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, c)!.Id);
        // One undo takes the whole copy back.
        f.Session.Undo();
        Assert.Single(f.Model.OfClass(IfcClass.Door));
        Assert.Null(f.Model.Get(c));
    }

    [Fact]
    public void Drafting_and_a_Studio_solid_move_with_their_sketch()
    {
        var f = F();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var rect = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(2, 0), J(2, 1), J(0, 1)), ["closed"] = true } })!["ids"]![0]!.ToString();
        var solid = (string)f.Run("studio.extrude", new JsonObject { ["profileId"] = rect, ["depth"] = 1 })!["id"]!;
        T(f, new JsonObject { ["ids"] = new JsonArray(solid), ["op"] = "move", ["by"] = J(10, 0) });
        Assert.Equal(10, f.Bounds(solid).Min.X, 9);
        var copy = (string)T(f, new JsonObject { ["ids"] = new JsonArray(solid), ["op"] = "copy", ["by"] = J(0, 5) })!["ids"]![0]!;
        Assert.Equal(5, f.Bounds(copy).Min.Y, 9);
        Assert.Equal(10, f.Bounds(solid).Min.X, 9);
        Assert.Equal(2.0, Vol(f, copy), 9);
    }
}
