using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// A building's exterior dimensions made at once (forum topic 78): for each facade, strings outside it in the order the
/// user's rules give (details, openings' centres, grid axes, rooms behind, the whole facade), held on what they measure.
/// </summary>
public class ExteriorDimensionTests
{
    static Fixture New() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());

    /// <summary>
    /// A 10 × 8 m building of 30 cm walls on their axes (its faces 10.30 × 8.30), two windows in the south wall at 2.5 and
    /// 7.5 m, a 15 cm wall inside at x = 4 meeting it, grid axes A, B, C at x = 0, 4 and 10.
    /// </summary>
    static (Fixture F, string[] Walls, string[] Windows, string Inner) Building()
    {
        var f = New();
        var type = f.Type("Brick 30", ("Brick", 0.3, true));
        var thin = f.Type("Brick 15", ("Brick", 0.15, true));
        string[] walls = [f.Wall(0, 0, 10, 0, type), f.Wall(10, 0, 10, 8, type), f.Wall(10, 8, 0, 8, type), f.Wall(0, 8, 0, 0, type)];
        var inner = f.Wall(4, 0, 4, 8, thin);
        string[] windows =
        [
            (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = walls[0], ["along"] = 2.5 })!["id"]!,
            (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = walls[0], ["along"] = 7.5 })!["id"]!,
        ];
        foreach (var (tag, x) in new[] { ("A", 0.0), ("B", 4.0), ("C", 10.0) })
            f.Run("drafting.addAxis", new JsonObject { ["x1"] = x, ["y1"] = -3, ["x2"] = x, ["y2"] = 11, ["tag"] = tag });
        return (f, walls, windows, inner);
    }

    static string Plan(Fixture f) => "plan:" + f.Storey;

    static JsonObject Run(Fixture f, JsonObject? settings = null, JsonArray? walls = null)
    {
        var a = new JsonObject { ["viewId"] = Plan(f) };
        if (settings != null) a["settings"] = settings;
        if (walls != null) a["wallIds"] = walls;
        return f.Run("drafting.exteriorDimensions", a)!.AsObject();
    }

    static JsonArray Order(params string[] on) => new(DimensionRules.ExteriorKinds.OrderBy(k => Array.IndexOf(on, k) is var i && i < 0 ? 99 : i)
        .Select(k => (JsonNode)new JsonObject { ["kind"] = k, ["on"] = on.Contains(k) }).ToArray());

    /// <summary>The south facade's strings (their points below the building, their line further out), nearest the wall first.</summary>
    static List<(string Kind, List<double> Values, List<Vec2> Pts, double Out)> South(Fixture f)
    {
        var drawing = Views.Generate(f.Session.Reader, Views.Find(f.Model, Plan(f))!).Drawing!;
        return f.Model.OfClass(IfcClass.Annotation).Where(d => d.Str(ExteriorDimensions.Key) != null).Select(d =>
        {
            var pts = Dimensions.Points(f.Model, "plan", d, drawing).Pts;
            return (Kind: d.Str(ExteriorDimensions.Key)!, Values: Dimensions.Values(d, pts), Pts: pts, Out: -Args.PointOf(d.Attrs["at"]).Y);
        }).Where(s => s.Pts.All(p => Math.Abs(p.Y + 0.15) < 1e-6) && s.Out > 0.15).OrderBy(s => s.Out).ToList();
    }

    static void Values(IEnumerable<double> want, IEnumerable<double> got) =>
        Assert.Equal(want.Select(v => Math.Round(v, 4)), got.Select(v => Math.Round(v, 4)));

    [Fact]
    public void The_south_facade_has_its_strings_in_order_with_the_right_values()
    {
        var (f, _, _, _) = Building();
        var r = Run(f);
        Assert.Equal(4, (int)r["facades"]!);
        var south = South(f);
        Assert.Equal(["details", "openings", "axes", "rooms", "overall"], south.Select(s => s.Kind));
        // Each further out than the one before, 10 mm then 7 mm apart on the paper (1:100).
        Assert.Equal(0.15 + 1.0, south[0].Out, 6);
        Assert.Equal(0.7, south[1].Out - south[0].Out, 6);
        Values([2.65, 5, 2.65], south.Single(s => s.Kind == "openings").Values);
        Values([0.15, 4, 6, 0.15], south.Single(s => s.Kind == "axes").Values);
        Values([0.3, 3.775, 0.15, 5.775, 0.3], south.Single(s => s.Kind == "rooms").Values);
        Values([10.3], south.Single(s => s.Kind == "overall").Values);
        // The details: the corners and both windows' jambs, the windows' widths over their openings.
        var details = south.Single(s => s.Kind == "details");
        Assert.Equal(6, details.Pts.Count);
        Assert.Equal(10.3, details.Values.Sum(), 6);
        Assert.Equal(details.Values[1], details.Values[3], 6);
        Assert.Equal(2.5, (details.Pts[1].X + details.Pts[2].X) / 2, 6);
        // Every point holds on something.
        Assert.All(f.Model.OfClass(IfcClass.Annotation).Where(d => d.Str(ExteriorDimensions.Key) != null), d => Assert.All(d.Attrs["refs"]!.AsArray(), x => Assert.NotNull(x)));
    }

    [Fact]
    public void Measured_to_the_axes_the_facade_is_ten_metres()
    {
        var (f, _, _, _) = Building();
        Run(f, new JsonObject { ["measureTo"] = "axes" });
        var south = South(f);
        Values([10], south.Single(s => s.Kind == "overall").Values);
        Values([4, 6], south.Single(s => s.Kind == "rooms").Values);
        Values([2.5, 5, 2.5], south.Single(s => s.Kind == "openings").Values);
    }

    [Fact]
    public void The_order_and_the_strings_turned_off_are_the_rules()
    {
        var (f, _, _, _) = Building();
        f.Run("drafting.setDimensionRules", new JsonObject { ["exterior"] = new JsonObject { ["strings"] = Order("overall", "axes", "details"), ["offset"] = 12, ["spacing"] = 8 } });
        Run(f);
        var south = South(f);
        Assert.Equal(["overall", "axes", "details"], south.Select(s => s.Kind));
        Assert.Equal(0.15 + 1.2, south[0].Out, 6);
        Assert.Equal(0.8, south[2].Out - south[1].Out, 6);
        // The project keeps the rules; one run's settings go over them without being kept.
        var rules = f.Session.Query("drafting.dimensionRules", new JsonObject())!;
        Assert.Equal(12, (double)rules["exterior"]!["offset"]!);
        Run(f, new JsonObject { ["strings"] = Order("rooms") });
        Assert.Equal(["rooms"], South(f).Select(s => s.Kind));
        Assert.Equal(12, (double)f.Session.Query("drafting.dimensionRules", new JsonObject())!["exterior"]!["offset"]!);
        // Rules that cannot be are refused; reset, the rules are the built-in ones again.
        Assert.Throws<CommandException>(() => f.Run("drafting.setDimensionRules", new JsonObject { ["exterior"] = new JsonObject { ["strings"] = new JsonArray("nothing") } }));
        Assert.Throws<CommandException>(() => f.Run("drafting.setDimensionRules", new JsonObject { ["exterior"] = new JsonObject { ["measureTo"] = "paint" } }));
        f.Run("drafting.setDimensionRules", new JsonObject { ["exterior"] = null });
        Assert.Equal(10, (double)f.Session.Query("drafting.dimensionRules", new JsonObject())!["exterior"]!["offset"]!);
    }

    [Fact]
    public void Run_again_it_makes_its_strings_anew_and_keeps_the_users_own()
    {
        var (f, walls, _, _) = Building();
        var mine = (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = Plan(f), ["a"] = new JsonArray(-0.15, -0.15), ["b"] = new JsonArray(10.15, -0.15), ["at"] = new JsonArray(5, -5) })!["id"]!;
        var first = (int)Run(f)["strings"]!;
        var second = (int)Run(f)["strings"]!;
        Assert.Equal(first, second);
        Assert.Equal(first, f.Model.OfClass(IfcClass.Annotation).Count(d => d.Str(ExteriorDimensions.Key) != null));
        Assert.NotNull(f.Model.Get(mine));
        // One wall's facade alone: the others' strings stay.
        Run(f, walls: new JsonArray(walls[0]));
        Assert.Equal(first, f.Model.OfClass(IfcClass.Annotation).Count(d => d.Str(ExteriorDimensions.Key) != null));
        Assert.Throws<CommandException>(() => Run(f, walls: new JsonArray(f.Storey)));
    }

    [Fact]
    public void A_window_moved_moves_its_points()
    {
        var (f, _, windows, inner) = Building();
        Run(f);
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = windows[0], ["along"] = 1.5 });
        var south = South(f);
        Values([1.65, 6, 2.65], south.Single(s => s.Kind == "openings").Values);
        var details = south.Single(s => s.Kind == "details");
        Assert.Equal(1.5, (details.Pts[1].X + details.Pts[2].X) / 2, 6);
        Assert.Equal(10.3, details.Values.Sum(), 6);
        // The inside wall moved: the rooms' string follows it.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = inner, ["x1"] = 5, ["y1"] = 0, ["x2"] = 5, ["y2"] = 8 });
        Values([0.3, 4.775, 0.15, 4.775, 0.3], South(f).Single(s => s.Kind == "rooms").Values);
    }

    [Fact]
    public void An_L_shaped_building_is_dimensioned_facade_by_facade()
    {
        var f = New();
        var type = f.Type("Brick 30", ("Brick", 0.3, true));
        // 10 × 8 less its north-east 4 × 4 quarter: six facades (the notch's two 4.00 between faces, the east 4.30).
        f.Wall(0, 0, 10, 0, type); f.Wall(10, 0, 10, 4, type); f.Wall(10, 4, 6, 4, type);
        f.Wall(6, 4, 6, 8, type); f.Wall(6, 8, 0, 8, type); f.Wall(0, 8, 0, 0, type);
        var r = f.Run("drafting.exteriorDimensions", new JsonObject { ["viewId"] = Plan(f), ["settings"] = new JsonObject { ["strings"] = Order("overall") } })!;
        Assert.Equal(6, (int)r["facades"]!);
        var drawing = Views.Generate(f.Session.Reader, Views.Find(f.Model, Plan(f))!).Drawing!;
        var lengths = f.Model.OfClass(IfcClass.Annotation).Where(d => d.Str(ExteriorDimensions.Key) == "overall")
            .Select(d => Math.Round(Dimensions.Values(d, Dimensions.Points(f.Model, "plan", d, drawing).Pts).Sum(), 4)).OrderBy(v => v).ToList();
        Assert.Equal([4.0, 4.0, 4.3, 6.3, 8.3, 10.3], lengths);
    }

    [Fact]
    public void Room_rules_add_the_whole_side_and_leave_narrow_openings_out()
    {
        var f = New();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        string[] walls = [f.Wall(0, 0, 5, 0, type), f.Wall(5, 0, 5, 4, type), f.Wall(5, 4, 0, 4, type), f.Wall(0, 4, 0, 0, type)];
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["along"] = 2 });
        f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 });
        f.Run("drafting.setDimensionRules", new JsonObject { ["rooms"] = new JsonObject { ["strings"] = new JsonArray(new JsonObject { ["kind"] = "details", ["on"] = true }, new JsonObject { ["kind"] = "overall", ["on"] = true }) } });
        var r = f.Run("drafting.dimensionRooms", new JsonObject { ["viewId"] = Plan(f) })!;
        // Four sides' details and the south side's whole length (the others say it once).
        Assert.Equal(5, (int)r["strings"]!);
        var overall = f.Model.OfClass(IfcClass.Annotation).Single(d => d.Str(RoomDimensions.StringKey) == "overall");
        Assert.Equal(2, overall.Attrs["pts"]!.AsArray().Count);
        // Openings under 2 m left out: the south side corner to corner, once.
        f.Run("drafting.dimensionRooms", new JsonObject { ["viewId"] = Plan(f), ["settings"] = new JsonObject { ["minOpening"] = 2 } });
        Assert.Equal(4, f.Model.OfClass(IfcClass.Annotation).Count(d => d.Str(RoomDimensions.RoomKey) != null));
        Assert.All(f.Model.OfClass(IfcClass.Annotation).Where(d => d.Str(RoomDimensions.RoomKey) != null), d => Assert.Equal(2, d.Attrs["pts"]!.AsArray().Count));
    }

    [Fact]
    public void Measured_to_the_core_the_finishes_are_left_out()
    {
        var f = New();
        f.EveryLayerInPlans();
        // 2 cm of plaster each side of a 26 cm core.
        var type = f.Type("Plastered 30", ("Plaster", 0.02, false), ("Brick", 0.26, true), ("Plaster", 0.02, false));
        f.Wall(0, 0, 10, 0, type); f.Wall(10, 0, 10, 8, type); f.Wall(10, 8, 0, 8, type); f.Wall(0, 8, 0, 0, type);
        double Overall(string to)
        {
            f.Run("drafting.exteriorDimensions", new JsonObject { ["viewId"] = Plan(f), ["settings"] = new JsonObject { ["strings"] = Order("overall"), ["measureTo"] = to } });
            var drawing = Views.Generate(f.Session.Reader, Views.Find(f.Model, Plan(f))!).Drawing!;
            return f.Model.OfClass(IfcClass.Annotation).Where(d => d.Str(ExteriorDimensions.Key) == "overall")
                .Select(d => Dimensions.Values(d, Dimensions.Points(f.Model, "plan", d, drawing).Pts).Sum()).Max();
        }
        Assert.Equal(10.3, Overall("faces"), 6);
        Assert.Equal(10.26, Overall("core"), 6);
        Assert.Equal(10, Overall("axes"), 6);
    }

    [Fact]
    public void The_rules_travel_in_a_standards_file_with_its_options()
    {
        var (f, _, _, _) = Building();
        f.Run("drafting.setDimensionRules", new JsonObject { ["exterior"] = new JsonObject { ["spacing"] = 9 }, ["rooms"] = new JsonObject { ["offset"] = 6 } });
        var text = (string)f.Session.Query("standards.export", new JsonObject())!["text"]!;
        var b = New();
        Assert.True((bool)b.Session.Query("standards.readPack", new JsonObject { ["text"] = text })!["options"]!);
        b.Run("standards.importFrom", new JsonObject { ["text"] = text, ["options"] = true });
        var rules = b.Session.Query("drafting.dimensionRules", new JsonObject())!;
        Assert.Equal(9, (double)rules["exterior"]!["spacing"]!);
        Assert.Equal(6, (double)rules["rooms"]!["offset"]!);
    }
}
