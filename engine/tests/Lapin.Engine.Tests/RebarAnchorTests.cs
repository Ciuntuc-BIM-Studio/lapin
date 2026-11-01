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
/// The anchor grips with cover (decision 226), after the configurator: a bar's anchors stand the cover outside its origin and
/// its ends; dragged onto the formwork's face, the bar lands the cover inside; the cover is the bar's own, the host type's
/// (stirrup cover plus stirrup) or the settings'.
/// </summary>
public class RebarAnchorTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static RebarSpec Straight(double length, double rotation = 0, Hook? end = null) =>
        new("straight", new Dictionary<string, double> { ["length"] = length }, null, 0.012, null, end, rotation);

    static Vec2 At(IReadOnlyList<RebarAnchors.Anchor> anchors, string key) => anchors.Single(a => a.Key == key).At;

    static string Section(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(8, 0) })!["id"]!;

    static string Bar(Fixture f, string view, Vec2 at, double length, JsonObject? more = null)
    {
        var a = new JsonObject { ["viewId"] = view, ["shape"] = "straight", ["at"] = new JsonArray(at.X, at.Y), ["params"] = new JsonObject { ["length"] = length } };
        if (more != null) foreach (var (k, v) in more) a[k] = v?.DeepClone();
        return (string)f.Run("rebar.add", a)!["id"]!;
    }

    [Fact]
    public void A_straight_bar_s_anchors_stand_the_cover_outside_its_origin_and_its_end()
    {
        // At (1, 1), 2 m long, a cover of 30 mm: the configurator's (−c, −c) and (L + c, −c).
        var anchors = RebarAnchors.Anchors(Straight(2), new Vec2(1, 1), 0.03);
        Assert.Equal([RebarAnchors.Origin, RebarAnchors.End], anchors.Select(a => a.Key));
        Assert.Equal(0.97, At(anchors, RebarAnchors.Origin).X, 9);
        Assert.Equal(0.97, At(anchors, RebarAnchors.Origin).Y, 9);
        Assert.Equal(3.03, At(anchors, RebarAnchors.End).X, 9);
        Assert.Equal(0.97, At(anchors, RebarAnchors.End).Y, 9);
    }

    [Fact]
    public void A_bar_turned_90_degrees_places_its_anchors_in_its_own_frame()
    {
        var anchors = RebarAnchors.Anchors(Straight(2, rotation: 90), new Vec2(1, 1), 0.03);
        // Local (−c, −c) turns to (c, −c); the end's (L + c, −c) to (c, L + c).
        Assert.Equal(1.03, At(anchors, RebarAnchors.Origin).X, 9);
        Assert.Equal(0.97, At(anchors, RebarAnchors.Origin).Y, 9);
        Assert.Equal(1.03, At(anchors, RebarAnchors.End).X, 9);
        Assert.Equal(3.03, At(anchors, RebarAnchors.End).Y, 9);
    }

    [Fact]
    public void A_hook_at_the_end_takes_the_end_s_anchor_away_and_a_polyline_or_a_dot_has_none()
    {
        Assert.Equal([RebarAnchors.Origin], RebarAnchors.Anchors(Straight(2, end: new Hook(90, 0.15)), new Vec2(1, 1), 0.03).Select(a => a.Key));
        Assert.Empty(RebarAnchors.Anchors(new RebarSpec("polyline", new Dictionary<string, double>(), [new(0, 0), new(1, 0)], 0.012, null, null), new Vec2(0, 0), 0.03));
        Assert.Empty(RebarAnchors.Anchors(new RebarSpec("dot", new Dictionary<string, double>(), null, 0.012, null, null), new Vec2(0, 0), 0.03));
        // A stirrup is closed and born with hooks: the origin's alone, at the formwork's corner.
        Assert.Equal([RebarAnchors.Origin], RebarAnchors.Anchors(new RebarSpec("stirrup", new Dictionary<string, double> { ["a"] = 0.4, ["b"] = 0.6 }, null, 0.008, null, null), new Vec2(0, 0), 0.03).Select(a => a.Key));
    }

    [Fact]
    public void A_U_bar_s_start_anchor_stands_off_its_first_leg_to_the_right_of_the_bar()
    {
        // The U walks down its left leg from (0, b): its start anchor is (−c, b + c), its end anchor (a + c, b + c).
        var anchors = RebarAnchors.Anchors(new RebarSpec("U", new Dictionary<string, double> { ["a"] = 0.6, ["b"] = 0.5 }, null, 0.012, null, null), new Vec2(0, 0), 0.03);
        Assert.Equal([RebarAnchors.Origin, RebarAnchors.Start, RebarAnchors.End], anchors.Select(a => a.Key));
        Assert.Equal(-0.03, At(anchors, RebarAnchors.Start).X, 9);
        Assert.Equal(0.53, At(anchors, RebarAnchors.Start).Y, 9);
        Assert.Equal(0.63, At(anchors, RebarAnchors.End).X, 9);
        Assert.Equal(0.53, At(anchors, RebarAnchors.End).Y, 9);
    }

    [Fact]
    public void The_end_anchor_dragged_onto_the_formwork_sets_the_length_so_the_bar_stops_the_cover_inside()
    {
        var f = F();
        var view = Section(f);
        var bar = Bar(f, view, new Vec2(1, 1), 2, new JsonObject { ["cover"] = 0.03 });
        f.Run("rebar.grip", new JsonObject { ["id"] = bar, ["key"] = RebarAnchors.End, ["point"] = new JsonArray(4.03, 0.97) });
        Assert.Equal(3.0, Rebars.SpecOf(f.Model.Get(bar)!)["length"], 6);
        // Never under the parameter's least: dragged back past the origin, the bar keeps 50 mm.
        f.Run("rebar.grip", new JsonObject { ["id"] = bar, ["key"] = RebarAnchors.End, ["point"] = new JsonArray(0.5, 0.97) });
        Assert.Equal(0.05, Rebars.SpecOf(f.Model.Get(bar)!)["length"], 6);
    }

    [Fact]
    public void The_origin_anchor_dragged_onto_the_formwork_s_corner_moves_the_bar_the_cover_inside()
    {
        var f = F();
        var view = Section(f);
        var bar = Bar(f, view, new Vec2(1, 1), 2, new JsonObject { ["cover"] = 0.03 });
        f.Run("rebar.grip", new JsonObject { ["id"] = bar, ["key"] = RebarAnchors.Origin, ["point"] = new JsonArray(0, 0) });
        var at = Rebars.At(f.Model.Get(bar)!);
        Assert.Equal(0.03, at.X, 9);
        Assert.Equal(0.03, at.Y, 9);
        Assert.Equal(2.0, Rebars.SpecOf(f.Model.Get(bar)!)["length"], 9);
    }

    [Fact]
    public void The_start_anchor_of_a_straight_bar_moves_its_start_and_keeps_its_far_end()
    {
        var f = F();
        var view = Section(f);
        var bar = Bar(f, view, new Vec2(1, 1), 2, new JsonObject { ["cover"] = 0.03 });
        // The start anchor (not shown: the origin's stands there) sent to x = 0.03: the bar starts at 0.06 and still ends at 3.
        f.Run("rebar.grip", new JsonObject { ["id"] = bar, ["key"] = RebarAnchors.Start, ["point"] = new JsonArray(0.03, 0.97) });
        var e = f.Model.Get(bar)!;
        Assert.Equal(0.06, Rebars.At(e).X, 9);
        Assert.Equal(1.0, Rebars.At(e).Y, 9);
        Assert.Equal(2.94, Rebars.SpecOf(e)["length"], 6);
    }

    [Fact]
    public void The_cover_comes_from_the_bar_else_the_host_type_s_stirrups_else_the_settings()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var typeId = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id;
        var top = Beams.Top(f.Model, f.Model.Get(beam)!);
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(6, 0), ["scale"] = 50 })!["id"]!;
        // Drawn in the beam's cut: its host is the beam; the type has no stirrups yet, so the cover is the settings' 25 mm.
        var bar = Bar(f, along, new Vec2(1, top - 0.36), 4);
        Assert.Equal(beam, f.Model.Get(bar)!.Str("host"));
        JsonObject Describe() => (JsonObject)f.Session.Query("rebar.describe", new JsonObject { ["id"] = bar })!;
        Assert.Equal(0.025, (double)Describe()["cover"]!, 9);
        Assert.Equal("settings", (string?)Describe()["coverSource"]);
        f.Run("rebar.setSettings", new JsonObject { ["cover"] = 0.04 });
        Assert.Equal(0.04, (double)Describe()["cover"]!, 9);
        // Stirrups of 25 mm cover and Ø8 on the type: 33 mm.
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = typeId, ["enabled"] = true, ["cover"] = 0.025, ["diameter"] = 0.008 });
        Assert.Equal(0.033, (double)Describe()["cover"]!, 9);
        Assert.Equal("host", (string?)Describe()["coverSource"]);
        // The bar's own wins; null gives it back.
        f.Run("rebar.update", new JsonObject { ["id"] = bar, ["cover"] = 0.05 });
        Assert.Equal(0.05, (double)Describe()["cover"]!, 9);
        Assert.Equal("own", (string?)Describe()["coverSource"]);
        var anchors = (JsonArray)Describe()["anchors"]!;
        Assert.Equal(0.95, (double)anchors[0]!["at"]![0]!, 9);
        f.Run("rebar.update", new JsonObject { ["id"] = bar, ["cover"] = null });
        Assert.Equal("host", (string?)Describe()["coverSource"]);
        Assert.Throws<CommandException>(() => f.Run("rebar.update", new JsonObject { ["id"] = bar, ["cover"] = 0.5 }));
    }

    [Fact]
    public void The_drawn_mark_carries_the_anchors_for_the_interface()
    {
        var f = F();
        var view = Section(f);
        var bar = Bar(f, view, new Vec2(1, 1), 2, new JsonObject { ["cover"] = 0.03 });
        var drawing = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
        var mark = ((JsonArray)drawing["marks"]!).Select(m => (JsonObject)m!).Single(m => (string?)m["id"] == bar);
        var anchors = (JsonArray)mark["data"]!["anchors"]!;
        Assert.Equal(2, anchors.Count);
        Assert.Equal(RebarAnchors.End, (string?)anchors[1]!["key"]);
        Assert.Equal(3.03, (double)anchors[1]!["at"]![0]!, 9);
    }
}
