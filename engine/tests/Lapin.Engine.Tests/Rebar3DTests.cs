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
/// Bars drawn by hand standing in space (decision 216): a bar drawn in a beam's longitudinal section belongs to the beam,
/// is laid across it by the cover, pierces the cross-section as dots and is counted whole in the schedule.
/// </summary>
public class Rebar3DTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonArray Pens(JsonObject d) => (JsonArray)d["pens"]!;
    static int Dots(JsonObject d) => Pens(d).Where(p => p!["fill"] != null && (string?)p["layer"] == "DRAFT-FILL" && (string?)p["fill"] != "#222222").Sum(p => (p!["loops"] as JsonArray)?.Count ?? 0);
    static int Lines(JsonObject d) => Pens(d).Where(p => (string?)p!["layer"] == RebarShapes.LongitudinalLayer && (double)p["width"]! > 0.2).Sum(p => ((p!["segments"] as JsonArray)?.Count ?? 0) / 4);

    [Fact]
    public void A_bar_drawn_in_a_beam_s_section_belongs_to_it_and_is_laid_across_by_the_cover()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id, ["enabled"] = true });
        var top = Beams.Top(f.Model, f.Model.Get(beam)!);
        // A section along the axis itself, looking +y: its x is the model's x, its y the level.
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(6, 0), ["scale"] = 50 })!["id"]!;
        var bar = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(1, top - 0.36), ["params"] = new JsonObject { ["length"] = 4 }, ["diameter"] = 0.016 })!["id"]!;
        var e = f.Model.Get(bar)!;
        Assert.Equal(beam, e.Str("host"));
        Assert.Equal(1, Rebar3D.Across.Of(e)!.Count);
        f.Run("rebar.update", new JsonObject { ["id"] = bar, ["across"] = new JsonObject { ["count"] = 3, ["mode"] = "cover" } });
        e = f.Model.Get(bar)!;
        Assert.Equal(3, Rebars.Pieces(e));
        // Three copies: the first and the last 25 + 8 + 8 mm in from the beam's faces (±0.125), the third in the middle; all at the bar's level.
        var bars = Rebar3D.Bars(f.Model, null, id => f.Session.Geometry(id), e, RebarSettings.Of(f.Model));
        Assert.Equal(3, bars.Count);
        Assert.Equal(new[] { -0.084, 0.0, 0.084 }, bars.Select(b => Math.Round(b[0].Y, 9)).Order());
        Assert.All(bars, b => Assert.Equal(top - 0.36, b[0].Z, 9));
        Assert.All(bars, b => { Assert.Equal(1.0, b[0].X, 9); Assert.Equal(5.0, b[^1].X, 9); });
        // The schedule counts all three, under the bar's mark.
        Assert.Contains(RebarSchedule.Entries(f.Model), x => x.Mark == Rebars.Mark(e) && x.Count == 3 && Math.Abs(x.Length - 4) < 1e-9);
    }

    [Fact]
    public void A_bar_drawn_in_a_stair_s_section_belongs_to_it_and_is_laid_between_its_flight_s_faces()
    {
        // Decision 248: a stair is a host; its railings and finish do not widen it.
        var f = F();
        var stair = (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0 })!["id"]!;
        var g = f.Session.Geometry(stair)!;
        var flight = g.Parts.First(p => p.Key == "flight:0");
        var (min, max) = Measure.Bounds(flight.Solid)!.Value;
        var all = g.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        // A section along the flight, looking across it; the bar drawn in the flight's cut, 150 mm under its box's middle (the box's middle is above the steps' nosings).
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(min.X - 1, (min.Y + max.Y) / 2), ["end"] = new JsonArray(max.X + 1, (min.Y + max.Y) / 2), ["scale"] = 50 })!["id"]!;
        var frame = Rebar3D.FrameOf(f.Model, f.Session.Catalog, f.Model.Get(along))!;
        var mid = frame.Map(new Vec3((min.X + max.X) / 2, (min.Y + max.Y) / 2, (min.Z + max.Z) / 2));
        var bar = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(mid.X, mid.Y - 0.15), ["params"] = new JsonObject { ["length"] = 0.6 }, ["diameter"] = 0.012, ["across"] = new JsonObject { ["count"] = 5, ["mode"] = "cover" } })!["id"]!;
        var e = f.Model.Get(bar)!;
        Assert.Equal(stair, e.Str("host"));
        var bars = Rebar3D.Bars(f.Model, null, id => f.Session.Geometry(id), e, RebarSettings.Of(f.Model));
        Assert.Equal(5, bars.Count);
        // Across the flight: the outer copies the cover and half a bar in from the concrete's faces, not from the railings'.
        var cover = RebarSettings.Of(f.Model).Cover + 0.006;
        var ys = bars.Select(b => b[0].Y).Order().ToList();
        Assert.Equal(min.Y + cover, ys[0], 6);
        Assert.Equal(max.Y - cover, ys[^1], 6);
        Assert.True(all.Min(v => v.Y) <= min.Y + 1e-9 && all.Max(v => v.Y) >= max.Y - 1e-9);
        Assert.Contains(RebarSchedule.Entries(f.Model), x => x.Mark == Rebars.Mark(e) && x.Count == 5);
    }

    [Fact]
    public void The_cross_section_shows_the_bar_as_dots_where_it_pierces_the_plane_and_a_structural_plan_as_lines()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var top = Beams.Top(f.Model, f.Model.Get(beam)!);
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(6, 0), ["scale"] = 50 })!["id"]!;
        // With 90° hooks at both ends: in the cross-section only the straight run pierces the plane, the hooks lie beyond its depth.
        f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(0.5, top - 0.36), ["params"] = new JsonObject { ["length"] = 5 }, ["diameter"] = 0.016,
            ["hooks"] = new JsonObject { ["start"] = new JsonObject { ["angle"] = 90, ["length"] = 0.2 }, ["end"] = new JsonObject { ["angle"] = 90, ["length"] = 0.2 } }, ["across"] = new JsonObject { ["count"] = 3, ["mode"] = "cover" } });
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 3 })!["id"]!;
        var d = Drawing(f, across);
        Assert.Equal(3, Dots(d));
        // The dots inside the beam's cut: its 0.25 m stand from x = 0.5 to 0.75 in the window, the bars 33 mm in from its faces (no stirrups on this type).
        var xs = Pens(d).Where(p => p!["fill"] != null && (string?)p["layer"] == "DRAFT-FILL").SelectMany(p => (JsonArray)p!["loops"]!)
            .Select(l => ((JsonArray)l!).Select(v => (double)v!).Where((_, i) => i % 2 == 0).Average()).Order().ToList();
        Assert.Equal(new[] { 0.533, 0.625, 0.717 }, xs.Select(x => Math.Round(x, 3)));
        Assert.Equal(0, Lines(d));
        var texts = Pens(d).SelectMany(p => (p!["texts"] as JsonArray)?.Select(t => (string?)t!["text"]) ?? []).ToList();
        Assert.Contains("3Ø16 L=5400", texts);
        // Seen from above in the structure's plan: three lines; in the architect's plan, none.
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["discipline"] = "Structure" })!["id"]!;
        Assert.True(Lines(Drawing(f, plan)) >= 3);
        Assert.Equal(0, Lines(Drawing(f, "plan:" + f.Storey)));
        // Its own view draws it itself, once, as before.
        Assert.Equal(0, Dots(Drawing(f, along)));
        // Drawn over the beam seen from the side (a section in front of it), the bar is the beam's too.
        var front = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, -2), ["end"] = new JsonArray(7, -2) })!["id"]!;
        var over = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = front, ["shape"] = "straight", ["at"] = new JsonArray(2, top - 0.2), ["params"] = new JsonObject { ["length"] = 2 } })!["id"]!;
        Assert.Equal(beam, f.Model.Get(over)!.Str("host"));
        Assert.Equal(0.0, Rebar3D.Bars(f.Model, null, id => f.Session.Geometry(id), f.Model.Get(over)!, RebarSettings.Of(f.Model))[0][0].Y, 9);
    }

    [Fact]
    public void A_type_s_stirrup_seen_across_is_bent_with_its_corners_rounded_and_its_hooks_at_the_top()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id, ["enabled"] = true });
        var across = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["across"] = beam, ["station"] = 3 })!["id"]!;
        var segs = Pens(Drawing(f, across)).Where(p => (string?)p!["layer"] == RebarShapes.StirrupLayer).Sum(p => ((p!["segments"] as JsonArray)?.Count ?? 0) / 4);
        // Far more than a rectangle's four edges: the arcs of the corners and the hooks' tails.
        Assert.True(segs > 20, $"{segs} segments");
    }
}
