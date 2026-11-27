using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Drafted lines made curtain walls in one go (forum topic 282, Convert to… → Curtain walls): one per straight segment and one per
/// arc, in the shape's order, of the type chosen, on the plan's storey; a segment under 100 mm left out and counted; one undo step;
/// the lines go unless kept.
/// </summary>
public sealed class CurtainWallsFromLinesTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 100 })!["id"]!;
    static List<string> Draw(Fixture f, string view, params JsonObject[] shapes) =>
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(shapes) })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
    static JsonObject Line(double x0, double y0, double x1, double y1) => new() { ["type"] = "line", ["a"] = J(x0, y0), ["b"] = J(x1, y1) };
    static JsonObject Poly(params (double X, double Y)[] pts) => new() { ["type"] = "polyline", ["pts"] = new JsonArray([.. pts.Select(p => (JsonNode)J(p.X, p.Y))]), ["closed"] = false };
    static JsonObject Arc(double r, double start, double end) => new() { ["type"] = "arc", ["c"] = J(0, 0), ["r"] = r, ["start"] = start, ["end"] = end };
    static JsonObject Convert(Fixture f, string view, IEnumerable<string> ids, JsonObject? options = null, bool keep = false) =>
        (JsonObject)f.Run("drafting.convert", new JsonObject { ["viewId"] = view, ["ids"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]), ["converter"] = "curtainWalls", ["options"] = options ?? [], ["keep"] = keep })!;
    static List<Entity> Curtains(Fixture f) => [.. f.Model.OfClass(CurtainWalls.Class)];

    [Fact]
    public void Curtain_walls_are_offered_for_a_plan()
    {
        var f = F();
        var ids = f.Session.Query("drafting.converters", new JsonObject { ["viewId"] = Plan(f) })!["converters"]!.AsArray().Select(c => (string)c!["id"]!);
        Assert.Contains("curtainWalls", ids);
    }

    [Fact]
    public void A_line_becomes_one_curtain_wall_on_the_plans_storey()
    {
        var f = F();
        var plan = Plan(f);
        var r = Convert(f, plan, Draw(f, plan, Line(0, 0, 6, 0)), new JsonObject { ["height"] = 3.2, ["baseOffset"] = 0.1 });
        var w = Assert.Single(Curtains(f));
        Assert.Equal(w.Id, (string)r["ids"]![0]!);
        Assert.Equal(6, PathCurve.Of(w).Length, 9);
        Assert.False(PathCurve.Of(w).IsArc);
        Assert.Equal(3.2, w.Num("height"), 9);
        Assert.Equal(0.1, w.Num("baseOffset"), 9);
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, w.Id)!.Id);
        Assert.Null(f.Session.Geometry(w.Id)!.Diagnostics);
    }

    [Fact]
    public void A_polyline_of_three_segments_makes_three_in_its_order_end_to_end()
    {
        var f = F();
        var plan = Plan(f);
        var r = Convert(f, plan, Draw(f, plan, Poly((0, 0), (5, 0), (5, 4), (0, 4))));
        var ids = r["ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal(3, ids.Count);
        var walls = ids.Select(id => f.Model.Get(id)!).ToList();
        Assert.Equal([5, 4, 5], walls.Select(w => Math.Round(PathCurve.Of(w).Length, 9)));
        // Each starts where the one before ends.
        for (var i = 1; i < walls.Count; i++) Assert.True((Walls.Start(walls[i]) - Walls.End(walls[i - 1])).Length < 1e-9);
        Assert.Null(r["skipped"]);
    }

    [Fact]
    public void An_arc_becomes_a_curved_curtain_wall_and_one_past_a_half_turn_two()
    {
        var f = F();
        var plan = Plan(f);
        Convert(f, plan, Draw(f, plan, Arc(5, 0, Math.PI / 2)));
        var w = Assert.Single(Curtains(f));
        var c = PathCurve.Of(w);
        Assert.True(c.IsArc);
        Assert.Equal(5 * Math.PI / 2, c.Length, 6);
        Assert.Equal(5 * (1 - Math.Cos(Math.PI / 4)), Math.Abs(w.Num(PathCurve.Key)), 9);

        // Three quarters of a turn: two curtain walls of half of it each, the whole length kept.
        var g = F();
        var plan2 = Plan(g);
        Convert(g, plan2, Draw(g, plan2, Arc(4, 0, 3 * Math.PI / 2)));
        var two = Curtains(g);
        Assert.Equal(2, two.Count);
        Assert.Equal(4 * 1.5 * Math.PI, two.Sum(x => PathCurve.Of(x).Length), 6);
        Assert.All(two, x => Assert.True(PathCurve.Of(x).IsArc));
    }

    [Fact]
    public void A_segment_under_100_mm_is_left_out_and_counted()
    {
        var f = F();
        var plan = Plan(f);
        var r = Convert(f, plan, Draw(f, plan, Poly((0, 0), (4, 0), (4, 0.05), (8, 0.05))));
        Assert.Equal(2, Curtains(f).Count);
        Assert.Equal(1, (int)r["skipped"]!);
    }

    [Fact]
    public void Undo_takes_back_the_curtain_walls_and_brings_back_the_lines()
    {
        var f = F();
        var plan = Plan(f);
        var lines = Draw(f, plan, Line(0, 0, 6, 0), Poly((0, 3), (4, 3), (4, 6)));
        Convert(f, plan, lines);
        Assert.Equal(3, Curtains(f).Count);
        Assert.All(lines, id => Assert.Null(f.Model.Get(id)));
        f.Session.Undo();
        Assert.Empty(Curtains(f));
        Assert.All(lines, id => Assert.NotNull(f.Model.Get(id)));
    }

    [Fact]
    public void The_curtain_walls_take_the_type_chosen()
    {
        var f = F();
        var plan = Plan(f);
        var type = (string)f.Run("architecture.createCurtainWallType", new JsonObject { ["name"] = "Shopfront", ["spec"] = CurtainSpec.Default.ToJson() })!["id"]!;
        Convert(f, plan, Draw(f, plan, Poly((0, 0), (3, 0), (3, 3))), new JsonObject { ["typeId"] = type });
        var walls = Curtains(f);
        Assert.Equal(2, walls.Count);
        Assert.All(walls, w => Assert.Equal(type, f.Model.RelatingOf(w.Id, IfcClass.RelDefinesByType)!.Id));
    }

    [Fact]
    public void The_lines_go_unless_kept()
    {
        var f = F();
        var plan = Plan(f);
        var gone = Draw(f, plan, Line(0, 0, 6, 0));
        Convert(f, plan, gone);
        Assert.Null(f.Model.Get(gone[0]));
        var kept = Draw(f, plan, Line(0, 5, 6, 5));
        Convert(f, plan, kept, keep: true);
        Assert.NotNull(f.Model.Get(kept[0]));
        Assert.Equal(2, Curtains(f).Count);
    }

    [Fact]
    public void A_circle_makes_four_quarter_arcs_and_the_command_alone_refuses_a_path_of_short_segments()
    {
        var f = F();
        var plan = Plan(f);
        Convert(f, plan, Draw(f, plan, new JsonObject { ["type"] = "circle", ["c"] = J(0, 0), ["r"] = 3 }));
        var walls = Curtains(f);
        Assert.Equal(4, walls.Count);
        Assert.Equal(2 * Math.PI * 3, walls.Sum(w => PathCurve.Of(w).Length), 6);

        var ex = Assert.ThrowsAny<Exception>(() => f.Run("architecture.curtainWallsAlong", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(J(0, 0), J(0.05, 0)) }));
        Assert.Contains("100 mm", ex.Message);
    }
}
