using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Convert drafting to… (docs/plan-user-extensibility.md §6.1, §7.4–7.5): drafted shapes made walls, floors, columns and beams on a
/// plan's storey, by the shapes chosen or by their layers (kept then, as a DXF underlay), what cannot be converted left with why;
/// lines and polylines made bars, a straight, L, U or stirrup recognised; one undo step.
/// </summary>
public sealed class LineworkConvertTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 100 })!["id"]!;
    static List<string> Draw(Fixture f, string view, string layer, params JsonObject[] shapes) =>
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["layer"] = layer, ["shapes"] = new JsonArray(shapes) })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
    static JsonObject Rect(double x0, double y0, double x1, double y1) => new() { ["type"] = "polyline", ["pts"] = new JsonArray(J(x0, y0), J(x1, y0), J(x1, y1), J(x0, y1)), ["closed"] = true };
    static JsonObject Line(double x0, double y0, double x1, double y1) => new() { ["type"] = "line", ["a"] = J(x0, y0), ["b"] = J(x1, y1) };
    static JsonObject Convert(string view, IEnumerable<string> ids, string converter, JsonObject? options = null) =>
        new() { ["viewId"] = view, ["ids"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]), ["converter"] = converter, ["options"] = options ?? [] };

    [Fact]
    public void A_plan_s_converters_are_listed_with_their_options_and_its_layers_with_their_shapes()
    {
        var f = F();
        var plan = Plan(f);
        Draw(f, plan, "0", Line(0, 0, 1, 0), Line(0, 1, 1, 1));
        var r = f.Session.Query("drafting.converters", new JsonObject { ["viewId"] = plan })!;
        var ids = r["converters"]!.AsArray().Select(c => (string)c!["id"]!).ToList();
        Assert.Subset(ids.ToHashSet(), new HashSet<string> { "walls", "floor", "room", "railing", "beams", "columns", "stripFootings", "padFootings", "rebar" });
        Assert.Equal(2, (int)r["layers"]![0]!["shapes"]!);
    }

    [Fact]
    public void Lines_and_a_closed_outline_become_walls_joined_on_the_plan_s_storey_in_one_undo_step()
    {
        var f = F();
        var plan = Plan(f);
        var ids = Draw(f, plan, "0", Rect(0, 0, 6, 4), Line(10, 0, 14, 0));
        var walls = f.Model.OfClass(IfcClass.Wall).Count();
        var r = f.Run("drafting.convert", Convert(plan, ids, "walls"))!;
        Assert.Equal(2, (int)r["converted"]!);
        Assert.Equal(walls + 5, f.Model.OfClass(IfcClass.Wall).Count());
        // The shapes converted are gone; undone, the walls go and the drafting is back.
        Assert.All(ids, id => Assert.Null(f.Model.Get(id)));
        f.Session.Undo();
        Assert.Equal(walls, f.Model.OfClass(IfcClass.Wall).Count());
        Assert.All(ids, id => Assert.NotNull(f.Model.Get(id)));
    }

    [Fact]
    public void A_floor_needs_a_closed_outline_and_says_so_of_a_line()
    {
        var f = F();
        var plan = Plan(f);
        var ids = Draw(f, plan, "0", Rect(0, 0, 5, 5), Line(0, 0, 3, 0));
        var preview = f.Session.Query("drafting.convertPreview", Convert(plan, ids, "floor"))!;
        Assert.Equal(1, (int)preview["ok"]!);
        Assert.Contains("closed outline", (string)preview["shapes"]!.AsArray().Single(s => s!["error"] != null)!["error"]!);
        var slabs = f.Model.OfClass(IfcClass.Slab).Count();
        var r = f.Run("drafting.convert", Convert(plan, ids, "floor"))!;
        Assert.Equal(slabs + 1, f.Model.OfClass(IfcClass.Slab).Count());
        Assert.Equal(1, (int)r["left"]!);
        // What could not be converted stays.
        Assert.NotNull(f.Model.Get(ids[1]));
    }

    [Fact]
    public void Columns_stand_where_circles_and_outlines_are_turned_with_their_longest_side()
    {
        var f = F();
        var plan = Plan(f);
        // A 0.3 × 0.6 outline turned 30°, and a circle.
        var (c, s) = (Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6));
        Vec2 R(double x, double y) => new(5 + x * c - y * s, 5 + x * s + y * c);
        var turned = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(new[] { R(-0.3, -0.15), R(0.3, -0.15), R(0.3, 0.15), R(-0.3, 0.15) }.Select(p => (JsonNode)J(p.X, p.Y)).ToArray()), ["closed"] = true };
        var ids = Draw(f, plan, "0", turned, new JsonObject { ["type"] = "circle", ["c"] = J(1, 2), ["r"] = 0.2 });
        f.Run("drafting.convert", Convert(plan, ids, "columns"));
        var cols = f.Model.OfClass(IfcClass.Column).ToList();
        Assert.Equal(2, cols.Count);
        var a = cols.Single(x => Math.Abs(Args.PointOf(x.Attrs["at"]).X - 5) < 1e-6);
        Assert.Equal(5, Args.PointOf(a.Attrs["at"]).Y, 6);
        Assert.Equal(30, a.Num("rotation"), 6);
        var b = cols.Single(x => Math.Abs(Args.PointOf(x.Attrs["at"]).X - 1) < 1e-6);
        Assert.Equal(2, Args.PointOf(b.Attrs["at"]).Y, 6);
    }

    [Fact]
    public void Each_straight_piece_of_a_polyline_becomes_a_beam()
    {
        var f = F();
        var plan = Plan(f);
        var ids = Draw(f, plan, "0", new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(4, 0), J(4, 3), J(8, 3)), ["closed"] = false });
        f.Run("drafting.convert", Convert(plan, ids, "beams"));
        Assert.Equal(3, f.Model.OfClass(IfcClass.Beam).Count());
    }

    [Fact]
    public void A_dxf_s_layers_map_to_what_they_become_and_the_drafting_stays_as_underlay()
    {
        var f = F();
        var plan = Plan(f);
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "A-WALL" });
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "S-COLS" });
        var walls = Draw(f, plan, "A-WALL", Line(0, 0, 6, 0), Line(6, 0, 6, 4));
        var cols = Draw(f, plan, "S-COLS", new JsonObject { ["type"] = "circle", ["c"] = J(0, 0), ["r"] = 0.15 }, new JsonObject { ["type"] = "circle", ["c"] = J(6, 0), ["r"] = 0.15 });
        Draw(f, plan, "0", Line(0, -2, 6, -2));
        var before = (f.Model.OfClass(IfcClass.Wall).Count(), f.Model.OfClass(IfcClass.Column).Count());
        f.Run("drafting.convert", new JsonObject
        {
            ["viewId"] = plan,
            ["map"] = new JsonArray(
                new JsonObject { ["layer"] = "A-WALL", ["converter"] = "walls", ["options"] = new JsonObject { ["height"] = 2.7 } },
                new JsonObject { ["layer"] = "s-cols", ["converter"] = "columns" },
                new JsonObject { ["layer"] = "0", ["converter"] = null }),
        });
        Assert.Equal(before.Item1 + 2, f.Model.OfClass(IfcClass.Wall).Count());
        Assert.Equal(before.Item2 + 2, f.Model.OfClass(IfcClass.Column).Count());
        Assert.All(walls.Concat(cols), id => Assert.NotNull(f.Model.Get(id)));
    }

    // ------------------------------------------------------------ bars

    static JsonObject Rec(params (double X, double Y)[] p) => RebarLinework.Recognise([.. p.Select(q => new Vec2(q.X, q.Y))], false, true);

    [Fact]
    public void Two_three_and_four_points_are_a_straight_an_L_and_a_U_bar_and_a_rectangle_a_stirrup()
    {
        var s = Rec((1, 1), (1, 4));
        Assert.Equal("straight", (string)s["shape"]!);
        Assert.Equal(3, (double)s["params"]!["length"]!, 9);
        Assert.Equal(90, (double)s["rotation"]!, 9);

        var l = Rec((0, 0), (2, 0), (2, 0.5));
        Assert.Equal("L", (string)l["shape"]!);
        Assert.Equal(2, (double)l["params"]!["a"]!, 9);
        Assert.Null(l["mirrorY"]);
        Assert.True((bool)Rec((0, 0), (2, 0), (2, -0.5))["mirrorY"]!);

        var u = Rec((0, 0.5), (0, 0), (3, 0), (3, 0.5));
        Assert.Equal("U", (string)u["shape"]!);
        Assert.Equal(3, (double)u["params"]!["a"]!, 9);
        Assert.Equal(0.5, (double)u["params"]!["b"]!, 9);

        var st = RebarLinework.Recognise([new(0, 0), new(0, 0.6), new(0.3, 0.6), new(0.3, 0)], true, true);
        Assert.Equal("stirrup", (string)st["shape"]!);
        Assert.Equal(0.3, (double)st["params"]!["a"]!, 9);
        Assert.Equal(0.6, (double)st["params"]!["b"]!, 9);

        // Unequal legs, or recognition off: a polyline bar through the same points.
        Assert.Equal("polyline", (string)Rec((0, 0.5), (0, 0), (3, 0), (3, 0.7))["shape"]!);
        Assert.Equal("polyline", (string)RebarLinework.Recognise([new(0, 0), new(1, 0)], false, false)["shape"]!);
    }

    [Fact]
    public void Drafted_lines_on_a_section_become_bars_drawn_where_the_lines_were()
    {
        var f = F();
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(0, 0), ["end"] = J(5, 0), ["scale"] = 20 })!["id"]!;
        var ids = Draw(f, section, "0", Line(0.05, 0.05, 2.95, 0.05),
            new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0.05, 0.5), J(0.05, 0.05), J(0.45, 0.05)), ["closed"] = false },
            Rect(1, 0, 1.25, 0.4));
        f.Run("drafting.convert", Convert(section, ids, "rebar", new JsonObject { ["diameter"] = 0.016, ["hooks"] = "135" }));
        var bars = f.Model.OfClass(Rebars.Class).ToList();
        Assert.Equal(["L", "stirrup", "straight"], bars.Select(b => b.Str("shape")!).Order().ToArray());
        Assert.All(bars, b => Assert.Equal(0.016, b.Num("diameter"), 9));
        Assert.All(bars, b => Assert.Equal(section, b.Str("viewId")));
        Assert.All(bars, b => Assert.Equal(135, (double)b.Attrs["hooks"]!["start"]!["angle"]!, 9));
    }
}
