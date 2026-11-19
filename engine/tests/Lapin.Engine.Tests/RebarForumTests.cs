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
/// Forum topics 174 and 175: a plan shows its own storey's bars and Visibility / Graphics switches the reinforcement by
/// subcategory (decision 728); marks written with a prefix by shape, numbered apart, stable (decision 729); rebar sets in slabs
/// and walls with their multi-rebar tag (decision 730).
/// </summary>
public class RebarForumTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonArray Pens(JsonObject d) => (JsonArray)d["pens"]!;
    static int Lines(JsonObject d, string layer = RebarShapes.LongitudinalLayer) => Pens(d).Where(p => (string?)p!["layer"] == layer && (double)p["width"]! > 0.2).Sum(p => ((p!["segments"] as JsonArray)?.Count ?? 0) / 4);
    static int Dots(JsonObject d) => Pens(d).Where(p => p!["fill"] != null && (string?)p["layer"] == "DRAFT-FILL" && (string?)p["fill"] != "#222222").Sum(p => (p!["loops"] as JsonArray)?.Count ?? 0);
    static List<string> Texts(JsonObject d) => [.. Pens(d).SelectMany(p => (p!["texts"] as JsonArray)?.Select(t => (string?)t!["text"] ?? "") ?? [])];
    static JsonArray Rect(double x0, double y0, double x1, double y1) => new(new JsonArray(x0, y0), new JsonArray(x1, y0), new JsonArray(x1, y1), new JsonArray(x0, y1));
    static string Id(JsonNode? r) => (string)r!["id"]!;

    /// <summary>A strip footing on the ground storey with a bar drawn in it (in a section along it), and a first floor above.</summary>
    static (Fixture F, string Footing, string Bar, string First) FootingWithBar()
    {
        var f = F();
        var footing = Id(f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 }));
        var body = f.Session.Geometry(footing)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        var zMid = (body.Min(p => p.Z) + body.Max(p => p.Z)) / 2;
        var along = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(6, 0), ["scale"] = 50 }));
        var bar = Id(f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(0.5, zMid), ["params"] = new JsonObject { ["length"] = 5 } }));
        var first = Id(f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 }));
        return (f, footing, bar, first);
    }

    static string StructuralPlan(Fixture f, string storey) => Id(f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey, ["discipline"] = "Structure" }));

    // ─── 728: a plan's own storey, Visibility / Graphics, Hide in view ────

    [Fact]
    public void A_foundation_bar_shows_in_the_foundation_plan_and_not_in_the_plan_of_the_floor_above()
    {
        var (f, footing, bar, first) = FootingWithBar();
        Assert.Equal(footing, f.Model.Get(bar)!.Str("host"));
        Assert.True(Lines(Drawing(f, StructuralPlan(f, f.Storey))) >= 1);
        var upper = Drawing(f, StructuralPlan(f, first));
        Assert.Equal(0, Lines(upper));
        Assert.DoesNotContain(Texts(upper), t => t.Contains("Ø12"));
    }

    [Fact]
    public void A_bar_with_no_host_shows_in_the_plan_whose_view_range_holds_it()
    {
        var f = F();
        var first = Id(f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 }));
        var along = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(6, 0), ["scale"] = 50 }));
        // Drawn in the air at 4.2 m: nothing holds it, so it is the first floor's by its height.
        var bar = Id(f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(0.5, 4.2), ["params"] = new JsonObject { ["length"] = 3 } }));
        Assert.Null(f.Model.Get(bar)!.Str("host"));
        Assert.True(Lines(Drawing(f, StructuralPlan(f, first))) >= 1);
        Assert.Equal(0, Lines(Drawing(f, StructuralPlan(f, f.Storey))));
    }

    [Fact]
    public void Visibility_graphics_hides_the_rebar_by_subcategory_and_hide_in_view_takes_a_bar_with_its_host()
    {
        var (f, footing, bar, _) = FootingWithBar();
        var plan = StructuralPlan(f, f.Storey);
        Assert.True(Lines(Drawing(f, plan)) >= 1);
        // The subcategories are offered under Rebar.
        var info = (JsonObject)f.Session.Query("view.visibility", new JsonObject { ["id"] = plan })!;
        Assert.Equal(["longitudinal", "stirrups", "sets"], ((JsonArray)info["subcategories"]!["rebar"]!).Select(x => (string)x!));
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "rebar", ["sub"] = "longitudinal", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Equal(0, Lines(Drawing(f, plan)));
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "rebar", ["sub"] = "longitudinal", ["value"] = null });
        Assert.True(Lines(Drawing(f, plan)) >= 1);
        // Hide in view on the footing takes its bars too; on the bar alone, the bar.
        f.Run("view.hideElements", new JsonObject { ["viewId"] = plan, ["ids"] = new JsonArray(footing) });
        Assert.Equal(0, Lines(Drawing(f, plan)));
        f.Run("view.unhideElements", new JsonObject { ["viewId"] = plan, ["all"] = true });
        f.Run("view.hideElements", new JsonObject { ["viewId"] = plan, ["ids"] = new JsonArray(bar) });
        Assert.Equal(0, Lines(Drawing(f, plan)));
    }

    [Fact]
    public void The_rebar_category_hidden_takes_a_type_s_stirrups_too()
    {
        var f = F();
        var beam = Id(f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 }));
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id, ["enabled"] = true });
        var along = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, -1), ["end"] = new JsonArray(6, -1), ["scale"] = 50 }));
        Assert.True(Lines(Drawing(f, along), RebarShapes.StirrupLayer) > 0);
        f.Run("view.graphics", new JsonObject { ["id"] = along, ["category"] = "rebar", ["sub"] = "stirrups", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Equal(0, Lines(Drawing(f, along), RebarShapes.StirrupLayer));
        f.Run("view.graphics", new JsonObject { ["id"] = along, ["category"] = "rebar", ["sub"] = "stirrups", ["value"] = null });
        f.Run("view.graphics", new JsonObject { ["id"] = along, ["category"] = "rebar", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Equal(0, Lines(Drawing(f, along), RebarShapes.StirrupLayer));
    }

    // ─── 729: marks with a prefix ─────────────────────────────────────────

    static string AddBar(Fixture f, string view, string shape, double x) =>
        Id(f.Run("rebar.add", new JsonObject { ["viewId"] = view, ["shape"] = shape, ["at"] = new JsonArray(x, 0) }));

    [Fact]
    public void Without_prefixes_every_bar_shares_one_numbering_as_before()
    {
        var f = F();
        var v = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 50), ["end"] = new JsonArray(6, 50) }));
        var ids = new[] { AddBar(f, v, "straight", 0), AddBar(f, v, "U", 2), AddBar(f, v, "straight", 4) };
        Assert.Equal([1, 2, 3], ids.Select(i => Rebars.Mark(f.Model.Get(i)!)));
        var st = RebarSettings.Of(f.Model);
        Assert.Equal(["1", "2", "3"], ids.Select(i => RebarMarks.TextOf(st, f.Model.Get(i)!)));
    }

    [Fact]
    public void Prefixes_by_shape_number_each_apart_and_show_in_balloons_and_the_schedule()
    {
        var f = F();
        f.Run("rebar.setSettings", new JsonObject { ["marks"] = new JsonObject { ["prefixes"] = new JsonObject { ["straight"] = "R", ["U"] = "U", ["stirrup"] = "S" } } });
        var v = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 50), ["end"] = new JsonArray(6, 50) }));
        var r1 = AddBar(f, v, "straight", 0);
        var u1 = AddBar(f, v, "U", 1);
        var r2 = AddBar(f, v, "straight", 2);
        var s1 = AddBar(f, v, "stirrup", 3);
        var u2 = AddBar(f, v, "U", 4);
        var st = RebarSettings.Of(f.Model);
        string T(string id) => RebarMarks.TextOf(st, f.Model.Get(id)!);
        Assert.Equal(["R1", "U1", "R2", "S1", "U2"], new[] { r1, u1, r2, s1, u2 }.Select(T));
        // The balloons write them; the schedule lists them by prefix, then number.
        var texts = Texts(Drawing(f, v));
        Assert.Contains("R2", texts);
        Assert.Contains("U1", texts);
        var marks = RebarSchedule.Rows(RebarSchedule.Entries(f.Model), out _).Select(r => r[0]).Where(x => x is { Length: > 0 } && x != "Total").ToList();
        Assert.Equal(["R1", "R2", "S1", "U1", "U2"], marks);
        // A bar added later takes the next of its own prefix; none of the others moves.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(r1) });
        var r3 = AddBar(f, v, "straight", 5);
        Assert.Equal("R3", T(r3));
        Assert.Equal("R2", T(r2));
        // Its own prefix over its shape's.
        f.Run("rebar.update", new JsonObject { ["id"] = r3, ["markPrefix"] = "P" });
        Assert.Equal("P3", T(r3));
        // Renumbered on request: from 1 in each prefix.
        f.Run("rebar.renumber", new JsonObject());
        Assert.Equal("R1", T(r2));
        Assert.Equal("P1", T(r3));
        Assert.Equal("U2", T(u2));
    }

    [Fact]
    public void A_mark_s_pattern_is_live_text()
    {
        var f = F();
        f.Run("rebar.setSettings", new JsonObject { ["marks"] = new JsonObject { ["pattern"] = "{prefix}{= n | 00}", ["prefixes"] = new JsonObject { ["straight"] = "R" } } });
        var v = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 50), ["end"] = new JsonArray(6, 50) }));
        var r = AddBar(f, v, "straight", 0);
        Assert.Equal("R01", RebarMarks.TextOf(RebarSettings.Of(f.Model), f.Model.Get(r)!));
        Assert.Throws<CommandException>(() => f.Run("rebar.setSettings", new JsonObject { ["marks"] = new JsonObject { ["pattern"] = "R" } }));
        // Back to the coded default.
        f.Run("rebar.setSettings", new JsonObject { ["marks"] = new JsonObject { ["pattern"] = null } });
        Assert.Equal("R1", RebarMarks.TextOf(RebarSettings.Of(f.Model), f.Model.Get(r)!));
    }

    // ─── 730: rebar sets ──────────────────────────────────────────────────

    static (Fixture F, string Slab) Slab(double x1 = 6, double y1 = 4)
    {
        var f = F();
        var slab = Id(f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Rect(0, 0, x1, y1) }));
        return (f, slab);
    }

    [Fact]
    public void A_slab_set_lays_its_bars_at_the_spacing_within_the_cover()
    {
        var (f, slab) = Slab();
        var set = Id(f.Run("rebar.addSet", new JsonObject { ["host"] = slab, ["layer"] = "B1", ["diameter"] = 0.012, ["step"] = 0.15 }));
        var laid = RebarSets.Lay(f.Model, f.Session.Geometry, f.Model.Get(set)!, RebarSettings.Of(f.Model))!;
        // Across y: from 25 + 6 mm to 4 m less that, 3.938 m at most 150 apart → 28 bars, each 6 m less the cover at both ends.
        Assert.Equal(28, laid.Lines);
        Assert.All(laid.Pieces, p => Assert.Equal(5.95, p.Length, 9));
        Assert.Equal(0.031, laid.Pieces.Min(p => p.A.Y), 9);
        var body = f.Session.Geometry(slab)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        Assert.All(laid.Pieces, p => Assert.Equal(body.Min(v => v.Z) + 0.031, p.A.Z, 9));
        // The second layer lies on the first, along y.
        var b2 = Id(f.Run("rebar.addSet", new JsonObject { ["host"] = slab, ["layer"] = "B2", ["diameter"] = 0.01 }));
        var laid2 = RebarSets.Lay(f.Model, f.Session.Geometry, f.Model.Get(b2)!, RebarSettings.Of(f.Model))!;
        Assert.All(laid2.Pieces, p => Assert.Equal(body.Min(v => v.Z) + 0.025 + 0.012 + 0.005, p.A.Z, 9));
        Assert.All(laid2.Pieces, p => Assert.Equal(3.95, p.Length, 9));
        // The top layer from the top face down.
        var t1 = Id(f.Run("rebar.addSet", new JsonObject { ["host"] = slab, ["layer"] = "T1" }));
        var laid3 = RebarSets.Lay(f.Model, f.Session.Geometry, f.Model.Get(t1)!, RebarSettings.Of(f.Model))!;
        Assert.All(laid3.Pieces, p => Assert.Equal(body.Max(v => v.Z) - 0.031, p.A.Z, 9));
        // The schedule: one row per length under the set's mark; the mass π d²/4 · 7850 per metre.
        var rows = RebarSchedule.Entries(f.Model, f.Session.Geometry).Where(x => x.Mark == Rebars.Mark(f.Model.Get(set)!) && x.Diameter == 0.012).ToList();
        Assert.Single(rows);
        Assert.Equal(28, rows[0].Count);
        var info = (JsonObject)f.Session.Query("rebar.describeSet", new JsonObject { ["id"] = set })!;
        Assert.Equal(28, (int)info["bars"]!);
        Assert.Equal(Math.Round(28 * 5.95 * RebarShapes.MassPerMetre(0.012), 2), (double)info["mass"]!, 2);
        Assert.Equal("28Ø12/150 B1", (string)info["label"]!);
    }

    [Fact]
    public void An_opening_stops_the_bars_with_the_cover_and_a_long_bar_is_lapped_at_the_stock_length()
    {
        var (f, slab) = Slab();
        f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["points"] = Rect(2, 1.5, 3, 2.5) });
        var set = Id(f.Run("rebar.addSet", new JsonObject { ["host"] = slab, ["layer"] = "B1" }));
        var laid = RebarSets.Lay(f.Model, f.Session.Geometry, f.Model.Get(set)!, RebarSettings.Of(f.Model))!;
        Assert.Equal(28, laid.Lines);
        var byLength = laid.Pieces.GroupBy(p => Math.Round(p.Length, 3)).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(22, byLength[5.95]);
        Assert.Equal(6, byLength[1.95]);
        Assert.Equal(6, byLength[2.95]);

        var (g, long_) = Slab(15, 2);
        var lapped = Id(g.Run("rebar.addSet", new JsonObject { ["host"] = long_, ["layer"] = "B1" }));
        var laid2 = RebarSets.Lay(g.Model, g.Session.Geometry, g.Model.Get(lapped)!, RebarSettings.Of(g.Model))!;
        // 14.95 m over a 12 m stock, lapped 50Ø = 600 mm: 12 m, then 14.975 − 11.425 = 3.55 m.
        var lengths = laid2.Pieces.Where(p => p.Position == 0).Select(p => Math.Round(p.Length, 3)).ToList();
        Assert.Equal([12.0, 3.55], lengths);
        g.Run("rebar.updateSet", new JsonObject { ["id"] = lapped, ["splice"] = false });
        Assert.All(RebarSets.Lay(g.Model, g.Session.Geometry, g.Model.Get(lapped)!, RebarSettings.Of(g.Model))!.Pieces, p => Assert.Equal(14.95, p.Length, 9));
        // Legs at the ends make it a U bar; the stock length counts them.
        g.Run("rebar.updateSet", new JsonObject { ["id"] = lapped, ["legs"] = 0.2 });
        var withLegs = RebarSets.Lay(g.Model, g.Session.Geometry, g.Model.Get(lapped)!, RebarSettings.Of(g.Model))!;
        Assert.All(withLegs.Pieces, p => { Assert.Equal("U", p.Shape); Assert.Equal(15.35, p.Length, 9); Assert.True(p.LegA!.Value.Z > p.A.Z); });
    }

    [Fact]
    public void A_region_bounds_a_slab_set_and_a_count_spreads_its_bars()
    {
        var (f, slab) = Slab();
        var set = Id(f.Run("rebar.addSet", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["region"] = Rect(1, 1, 3, 2), ["layer"] = "T1", ["mode"] = "count", ["count"] = 5 }));
        Assert.Equal(slab, f.Model.Get(set)!.Str("host"));
        var laid = RebarSets.Lay(f.Model, f.Session.Geometry, f.Model.Get(set)!, RebarSettings.Of(f.Model))!;
        Assert.Equal(5, laid.Lines);
        Assert.Equal([1.0, 1.25, 1.5, 1.75, 2.0], laid.Pieces.Select(p => Math.Round(p.A.Y, 9)).Order());
        // The region's edges inside the slab cut the bars there, without a cover.
        Assert.All(laid.Pieces, p => Assert.Equal(2.0, p.Length, 9));
        Assert.Equal("250", laid.StepText);
    }

    [Fact]
    public void A_wall_set_lies_by_the_face_it_is_on_and_stops_at_an_opening()
    {
        var f = F();
        var wall = f.Wall(0, 0, 5, 0, height: 3);
        var set = Id(f.Run("rebar.addSet", new JsonObject { ["host"] = wall, ["layer"] = "E1", ["direction"] = "vertical", ["step"] = 0.2, ["diameter"] = 0.01 }));
        var laid = RebarSets.Lay(f.Model, f.Session.Geometry, f.Model.Get(set)!, RebarSettings.Of(f.Model))!;
        var body = f.Session.Geometry(wall)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        // Vertical bars the height less the cover at each end, on the exterior face (right of the axis: −y), the cover and half a bar in.
        Assert.All(laid.Pieces, p => Assert.Equal(body.Max(v => v.Z) - body.Min(v => v.Z) - 0.05, p.Length, 9));
        Assert.All(laid.Pieces, p => Assert.Equal(body.Min(v => v.Y) + 0.03, p.A.Y, 9));
        var interior = Id(f.Run("rebar.addSet", new JsonObject { ["host"] = wall, ["layer"] = "I1", ["direction"] = "horizontal" }));
        var laidI = RebarSets.Lay(f.Model, f.Session.Geometry, f.Model.Get(interior)!, RebarSettings.Of(f.Model))!;
        Assert.All(laidI.Pieces, p => Assert.Equal(body.Max(v => v.Y) - 0.031, p.A.Y, 9));
        Assert.Throws<CommandException>(() => f.Run("rebar.addSet", new JsonObject { ["host"] = wall, ["layer"] = "B1" }));
        // A window's opening stops the vertical bars through it.
        var bars = laid.Lines;
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 2.5 });
        var cut = RebarSets.Lay(f.Model, f.Session.Geometry, f.Model.Get(set)!, RebarSettings.Of(f.Model))!;
        Assert.Equal(bars, cut.Lines);
        Assert.True(cut.Pieces.Count > laid.Pieces.Count);
        // The wall deleted takes its sets.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(wall) });
        Assert.Null(f.Model.Get(set));
    }

    [Fact]
    public void A_set_is_drawn_in_its_storey_s_plan_with_one_tag_and_as_dots_across_it()
    {
        var (f, slab) = Slab();
        var set = Id(f.Run("rebar.addSet", new JsonObject { ["host"] = slab, ["layer"] = "B1" }));
        var mark = RebarMarks.TextOf(RebarSettings.Of(f.Model), f.Model.Get(set)!);
        var plan = StructuralPlan(f, f.Storey);
        var d = Drawing(f, plan);
        // One bar shown (the middle one), the label once, the balloon with the mark.
        Assert.Equal(1, Lines(d));
        Assert.Contains("28Ø12/150 B1", Texts(d));
        Assert.Contains(mark, Texts(d));
        f.Run("rebar.updateSet", new JsonObject { ["id"] = set, ["show"] = "all" });
        Assert.Equal(28, Lines(Drawing(f, plan)));
        // A section across the bars: 28 dots.
        var body = f.Session.Geometry(slab)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        var across = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(3, -1), ["end"] = new JsonArray(3, 5), ["scale"] = 50 }));
        Assert.Equal(28, Dots(Drawing(f, across)));
        // Not in the plan of another storey; hidden with its subcategory.
        var first = Id(f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 }));
        Assert.Equal(0, Lines(Drawing(f, StructuralPlan(f, first))));
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "rebar", ["sub"] = "sets", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Equal(0, Lines(Drawing(f, plan)));
        // In 3D, one tube a bar.
        var bodies = (JsonObject)f.Session.Query("rebar.bodies", new JsonObject { ["ids"] = new JsonArray(set) })!;
        Assert.True((int)bodies["triangles"]! > 0);
        Assert.Equal(28, RebarSets.Chains(f.Model, f.Session.Geometry, RebarSettings.Of(f.Model), null, null).Count());
        Assert.True(body.Count > 0);
    }

    [Fact]
    public void A_set_takes_its_prefix_and_its_own_numbering()
    {
        var (f, slab) = Slab();
        f.Run("rebar.setSettings", new JsonObject { ["marks"] = new JsonObject { ["prefixes"] = new JsonObject { ["straight"] = "R" } } });
        var a = Id(f.Run("rebar.addSet", new JsonObject { ["host"] = slab, ["layer"] = "B1" }));
        var b = Id(f.Run("rebar.addSet", new JsonObject { ["host"] = slab, ["layer"] = "B2", ["markPrefix"] = "P" }));
        var st = RebarSettings.Of(f.Model);
        Assert.Equal("R1", RebarMarks.TextOf(st, f.Model.Get(a)!));
        Assert.Equal("P1", RebarMarks.TextOf(st, f.Model.Get(b)!));
        var bending = (JsonObject)f.Session.Query("rebar.bendingSchedule", new JsonObject())!;
        Assert.Equal(["P1", "R1"], ((JsonArray)bending["rows"]!).Select(r => (string)r![0]!));
    }
}
