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
/// Rebar 2D (docs/plan-rebar.md, decision 209), with the configurator's numbers: the Eurocode bend radius, the hooks,
/// the schedule's nominal length, the distributions by step and by zones, the schedule by mark, the drawing on a view.
/// </summary>
public class RebarTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static string Section(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(8, 0) })!["id"]!;

    static RebarSpec Straight(double length, double d = 0.012, Hook? start = null, Hook? end = null) =>
        new("straight", new Dictionary<string, double> { ["length"] = length }, null, d, start, end);

    [Fact]
    public void A_bend_follows_the_Eurocode_mandrel_and_a_hook_its_practice_length()
    {
        // SR EN 1992-1-1 table 8.1N: a mandrel of 4Ø up to Ø16, 7Ø over; the radius on the axis is half of it.
        Assert.Equal(0.024, RebarShapes.BendRadius(0.012), 9);
        Assert.Equal(0.032, RebarShapes.BendRadius(0.016), 9);
        Assert.Equal(0.063, RebarShapes.BendRadius(0.018), 9);
        Assert.Equal(0.070, RebarShapes.BendRadius(0.020), 9);
        // 12Ø up to 90°, 10Ø up to 135°, 5Ø beyond, and 50 mm at least.
        Assert.Equal(0.096, RebarShapes.HookLength(0.008, 90), 9);
        Assert.Equal(0.080, RebarShapes.HookLength(0.008, 135), 9);
        Assert.Equal(0.050, RebarShapes.HookLength(0.008, 180), 9);
        Assert.Equal(0.888, RebarShapes.MassPerMetre(0.012), 3);
        Assert.Equal(0.395, RebarShapes.MassPerMetre(0.008), 3);
        Assert.Equal(6.313, RebarShapes.MassPerMetre(0.032), 3);
    }

    [Fact]
    public void The_schedule_length_is_the_nominal_one_with_the_hooks_and_a_half_turn_adds_pi_r()
    {
        Assert.Equal(2.0, RebarShapes.Length(Straight(2.0)), 9);
        Assert.Equal(2.12, RebarShapes.Length(Straight(2.0, end: new Hook(135, 0.12))), 9);
        var r = RebarShapes.BendRadius(0.012);
        Assert.Equal(2.0 + 0.1 + Math.PI * r, RebarShapes.Length(Straight(2.0, end: new Hook(180, 0.1))), 9);
        Assert.Equal(2.0 + 0.2 + 2 * Math.PI * r, RebarShapes.Length(Straight(2.0, start: new Hook(180, 0.1), end: new Hook(180, 0.1))), 9);
        // An L of 800 × 400 is 1200; a U of 600 × 500 is 1600.
        Assert.Equal(1.2, RebarShapes.Length(new RebarSpec("L", new Dictionary<string, double> { ["a"] = 0.8, ["b"] = 0.4 }, null, 0.012, null, null)), 9);
        Assert.Equal(1.6, RebarShapes.Length(new RebarSpec("U", new Dictionary<string, double> { ["a"] = 0.6, ["b"] = 0.5 }, null, 0.012, null, null)), 9);
    }

    [Fact]
    public void A_stirrup_is_born_with_two_135_hooks_inside_and_measures_the_perimeter_plus_its_hooks()
    {
        var s = new RebarSpec("stirrup", new Dictionary<string, double> { ["a"] = 0.4, ["b"] = 0.6 }, null, 0.008, null, null);
        Assert.Equal(2.16, RebarShapes.Length(s), 9);
        var segs = RebarShapes.Segments(s);
        // Three corners and, at the fourth, each end's 135° wrap round it, all at r = 16 mm; the two tails parallel at 45° into the inside, a mandrel (2r) apart.
        Assert.Equal(5, segs.Count(x => x is SegArc));
        Assert.All(segs.OfType<SegArc>(), a => Assert.Equal(0.016, a.R, 9));
        Assert.Equal(2, segs.OfType<SegArc>().Count(a => Math.Abs(Math.Abs(a.Sweep) - 3 * Math.PI / 4) < 1e-9));
        var tails = new[] { (SegLine)segs[0], (SegLine)segs[^1] };
        Assert.All(tails, t => Assert.Equal(0.08, t.Length, 9));
        Assert.All(tails, t => Assert.Equal(-Math.PI / 4, Math.Atan2(Math.Abs(t.B.Y - t.A.Y) * -1, Math.Abs(t.B.X - t.A.X)), 9));
        var n = new Vec2(1, 1) * (1 / Math.Sqrt(2));
        Assert.Equal(0.032, Math.Abs(Vec2.Dot(tails[0].A - tails[1].A, n)), 9);
        // Wrapping the corner twice makes the bar a little longer than its nominal measure.
        Assert.Equal(2.183, RebarShapes.TrueLength(s), 3);
        // Both tails run into the inside of the stirrup, towards its bottom right.
        var nominal = RebarShapes.Nominal(s);
        Assert.Equal(7, nominal.Count);
        Assert.True(nominal[0].X > 0 && nominal[0].Y < 0.6 && nominal[^1].X > 0 && nominal[^1].Y < 0.6);
        // A hook of angle 0 set on the bar takes the born one away.
        var bare = s with { Start = new Hook(0, 0), End = new Hook(0, 0) };
        Assert.Equal(2.0, RebarShapes.Length(bare), 9);
    }

    [Fact]
    public void A_corner_is_bent_at_the_tangent_distance_and_a_leg_too_short_for_it_stays_sharp()
    {
        var segs = RebarShapes.Fillet([new(0, 0), new(1, 0), new(1, 1)], 0.024);
        Assert.Equal(3, segs.Count);
        var line = Assert.IsType<SegLine>(segs[0]);
        Assert.Equal(0.976, line.B.X, 9);
        var arc = Assert.IsType<SegArc>(segs[1]);
        Assert.Equal(Math.PI / 2, arc.Sweep, 9);
        Assert.Equal(new Vec2(0.976, 0.024), arc.C);
        Assert.Equal(2 * 0.976 + Math.PI / 2 * 0.024, segs.Sum(x => x.Length), 9);
        Assert.Equal(2, RebarShapes.Fillet([new(0, 0), new(0.01, 0), new(0.01, 1)], 0.024).Count);
    }

    [Fact]
    public void A_grip_sets_its_parameter_to_its_coordinate_and_never_under_the_least()
    {
        var l = new RebarSpec("L", new Dictionary<string, double> { ["a"] = 0.8, ["b"] = 0.4 }, null, 0.012, null, null);
        Assert.Equal(1.1, RebarShapes.Apply(l, "corner", new Vec2(1.1, 0.3))["a"], 9);
        Assert.Equal(0.05, RebarShapes.Apply(l, "tip", new Vec2(0.8, -2))["b"], 9);
        // The grips stand where the shape is placed: turned 90°, the corner's x becomes y.
        var placed = RebarShapes.Place(l with { Rotation = 90 }, new Vec2(2, 3), RebarShapes.Grips(l)[0].At);
        Assert.Equal(2.0, placed.X, 9);
        Assert.Equal(3.8, placed.Y, 9);
        Assert.Equal(new Vec2(0.8, 0), RebarShapes.Local(l with { Rotation = 90 }, new Vec2(2, 3), placed) is var q ? new Vec2(Math.Round(q.X, 9), Math.Round(q.Y, 9)) : default);
    }

    [Fact]
    public void A_distribution_spreads_its_bars_so_both_ends_get_one_and_its_zones_each_take_their_step()
    {
        var one = Runs.Positions(1.0, 0.1);
        Assert.Equal(11, one.Count);
        Assert.Equal(0, one[0], 9);
        Assert.Equal(1.0, one[^1], 9);
        var two = Runs.Positions(2.0, 0.1, [new Runs.Zone(1.0, 0.1), new Runs.Zone(1.0, 0.2)]);
        Assert.Equal(16, two.Count);
        Assert.Equal(1.0, two[10], 9);
        Assert.Equal(1.2, two[11], 9);
        var three = Runs.Positions(3.0, 0.1, [new Runs.Zone(1.0, 0.1), new Runs.Zone(1.0, 0.2), new Runs.Zone(1.0, 0.1)]);
        Assert.Equal(26, three.Count);
        Assert.Contains(three, p => Math.Abs(p - 1.0) < 1e-9);
        Assert.Contains(three, p => Math.Abs(p - 2.0) < 1e-9);
        // The last zone stretches to the end; its step never over the one asked.
        var stretched = Runs.Positions(2.5, 0.1, [new Runs.Zone(1.0, 0.1), new Runs.Zone(1.0, 0.2)]);
        Assert.Equal(2.5, stretched[^1], 9);
        for (var i = 10; i + 1 < stretched.Count; i++) Assert.True(stretched[i + 1] - stretched[i] <= 0.2 + 1e-9);
        Assert.Equal(new[] { 0.0, 0.5, 1.0 }, Runs.Positions(1.0, 0.5).Select(x => Math.Round(x, 9)));
        Assert.Equal(new[] { 0.0, 0.5 }, Runs.Positions(0.5, 1.0).Select(x => Math.Round(x, 9)));
        Assert.Equal("100-200", Runs.StepText(2.0, 0.1, [new Runs.Zone(1.0, 0.1), new Runs.Zone(1.0, 0.2)]));
        Assert.Equal("150", Runs.StepText(3.0, 0.15, null));
    }

    [Fact]
    public void The_schedule_sums_a_mark_s_bars_notes_a_mark_whose_bars_differ_and_leaves_out_what_is_excluded()
    {
        var f = F();
        var view = Section(f);
        JsonObject Bar(int mark, double length, double d = 0.012, int count = 1) => new() { ["viewId"] = view, ["shape"] = "straight", ["at"] = new JsonArray(0, 0), ["params"] = new JsonObject { ["length"] = length }, ["mark"] = mark, ["count"] = count, ["diameter"] = d };
        f.Run("rebar.add", Bar(1, 1.0, count: 2));
        f.Run("rebar.add", Bar(1, 1.0, count: 3));
        f.Run("rebar.add", Bar(2, 1.0));
        f.Run("rebar.add", Bar(2, 1.5));
        var excluded = (string)f.Run("rebar.add", Bar(3, 1.0))!["id"]!;
        f.Run("rebar.update", new JsonObject { ["id"] = excluded, ["excludeSchedule"] = true });
        // Eleven stirrups Ø8 along 1 m at 100 mm, mark 5.
        f.Run("rebar.addRun", new JsonObject { ["viewId"] = view, ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(1, 0), ["step"] = 0.1, ["mark"] = 5, ["params"] = new JsonObject { ["a"] = 0.4, ["b"] = 0.6 } });
        var rows = RebarSchedule.Rows(RebarSchedule.Entries(f.Model), out var mass);
        var m1 = rows.Single(r => r[0] == "1");
        Assert.Equal("5", m1[3]);
        Assert.Equal("5.00", m1[5]);
        Assert.Equal("", m1[7]);
        Assert.Equal("Ø or length differs", rows.Single(r => r[0] == "2")[7]);
        Assert.DoesNotContain(rows, r => r[0] == "3");
        var m5 = rows.Single(r => r[0] == "5");
        Assert.Equal("11", m5[3]);
        Assert.Equal("2.16", m5[4]);
        Assert.Equal(5 * 0.888 + 2.5 * 0.888 + 11 * 2.16 * 0.395, mass, 1);
        // The schedule entity reads it through its kind.
        var schedule = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "rebar" })!["id"]!;
        var grid = f.Session.Query("schedule.layout", new JsonObject { ["id"] = schedule })?.ToJsonString() ?? "";
        Assert.Contains("Bar bending schedule", grid);
        Assert.Contains("differs", grid);
    }

    [Fact]
    public void A_bar_draws_on_its_view_with_its_mark_and_label_and_a_distribution_draws_each_position()
    {
        var f = F();
        var view = Section(f);
        var bar = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = view, ["shape"] = "L", ["at"] = new JsonArray(1, 1), ["params"] = new JsonObject { ["a"] = 2.0, ["b"] = 0.5 }, ["count"] = 4 })!["id"]!;
        var run = (string)f.Run("rebar.addRun", new JsonObject { ["viewId"] = view, ["start"] = new JsonArray(1, 0.3), ["end"] = new JsonArray(3, 0.3), ["step"] = 0.2 })!["id"]!;
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
        var pens = (JsonArray)d["pens"]!;
        var layers = pens.Select(p => (string?)p!["layer"]).ToList();
        Assert.Contains("Bare_Longitudinale", layers);
        Assert.Contains("Etrieri", layers);
        var texts = pens.SelectMany(p => (p!["texts"] as JsonArray)?.Select(t => (string?)t!["text"]) ?? []).ToList();
        Assert.Contains("4Ø12 L=2500", texts);
        Assert.Contains("11Ø8/200", texts);
        var marks = (JsonArray)d["marks"]!;
        Assert.Contains(marks, m => (string?)m!["id"] == bar);
        Assert.Contains(marks, m => (string?)m!["id"] == run);
        // The bar's grips as drawn: its corner at (3, 1), told to the interface.
        var grips = (JsonArray)marks.First(m => (string?)m!["id"] == bar)!["data"]!["grips"]!;
        Assert.Contains(grips, g => (string?)g!["key"] == "corner" && Math.Abs((double)g["at"]![0]! - 3.0) < 1e-9 && Math.Abs((double)g["at"]![1]! - 1.0) < 1e-9);
        // Dragging that grip to x = 4 makes a 3 m long leg.
        f.Run("rebar.grip", new JsonObject { ["id"] = bar, ["key"] = "corner", ["point"] = new JsonArray(4, 1.2) });
        Assert.Equal(3.0, (double)f.Model.Get(bar)!.Attrs["params"]!["a"]!, 9);
        var described = (JsonObject)f.Session.Query("rebar.describe", new JsonObject { ["id"] = bar })!;
        Assert.Equal(3.5, (double)described["length"]!, 9);
        Assert.Equal("4Ø12 L=3500", (string?)described["label"]);
        // The same bar seen in section, elsewhere on the view, stays out of the schedule.
        var dot = (string)f.Run("rebar.duplicateAs", new JsonObject { ["id"] = bar, ["view"] = "section", ["at"] = new JsonArray(6, 1) })!["id"]!;
        Assert.True(f.Model.Get(dot)!.Bool("excludeSchedule"));
        Assert.Equal(bar, f.Model.Get(dot)!.Str("original"));
        Assert.Single(RebarSchedule.Entries(f.Model), e => e.Mark == Rebars.Mark(f.Model.Get(bar)!));
    }

    [Fact]
    public void A_moved_or_mirrored_bar_keeps_its_shape_and_a_mirrored_one_flips()
    {
        var f = F();
        var view = Section(f);
        var bar = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = view, ["shape"] = "L", ["at"] = new JsonArray(1, 1), ["rotation"] = 30 })!["id"]!;
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(bar), ["op"] = "move", ["by"] = new JsonArray(2, 0.5) });
        var e = f.Model.Get(bar)!;
        Assert.Equal(new Vec2(3, 1.5), Rebars.At(e));
        Assert.Equal(30, e.Num("rotation"), 9);
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(bar), ["op"] = "mirror", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(0, 1) });
        e = f.Model.Get(bar)!;
        Assert.Equal(new Vec2(-3, 1.5), Rebars.At(e));
        Assert.True(e.Bool("mirrorY"));
        Assert.Equal(150, e.Num("rotation"), 6);
        Assert.Equal(1.2, RebarShapes.Length(Rebars.SpecOf(e)), 9);
    }
}
