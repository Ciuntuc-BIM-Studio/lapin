using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>Sweeps along straight-sided paths with mitred corners: closed rings (perimeter pavements) and the profile placed beside its path.</summary>
public class StudioSweepTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static string Draw(Fixture f, string view, JsonObject shape) => (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = shape })!["ids"]![0]!;
    static JsonObject Poly(bool closed, params (double X, double Y)[] p) =>
        new() { ["type"] = "polyline", ["pts"] = new JsonArray(p.Select(q => (JsonNode)J(q.X, q.Y)).ToArray()), ["closed"] = closed };
    static JsonObject Rect(double x0, double y0, double x1, double y1) => Poly(true, (x0, y0), (x1, y0), (x1, y1), (x0, y1));
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    /// <summary>A 1.00 × 0.10 m pavement section along a 10 × 6 m outline drawn one way or the other.</summary>
    static (Fixture F, string Solid) Pavement(string place, bool reversed = false, double gap = 0)
    {
        var f = F();
        var plan = Plan(f);
        var profile = Draw(f, plan, Rect(30, 0, 31, 0.1));
        (double, double)[] corners = [(0, 0), (10, 0), (10, 6), (0, 6)];
        if (reversed) Array.Reverse(corners);
        var path = Draw(f, plan, Poly(true, corners));
        var id = (string)f.Run("studio.sweep", new JsonObject { ["profileId"] = profile, ["pathId"] = path, ["place"] = place, ["placeOffset"] = gap })!["id"]!;
        return (f, id);
    }

    [Fact]
    public void A_closed_outline_gives_one_mitred_ring_outside_whichever_way_it_was_drawn()
    {
        // Outside a 10 × 6 rectangle, 1 m wide: (12 · 8 − 10 · 6) · 0.1 = 3.6 m³, no end faces.
        foreach (var reversed in new[] { false, true })
        {
            var (f, id) = Pavement("outside", reversed);
            Assert.Empty(f.Session.Geometry(id)!.Diagnostics ?? []);
            Assert.Equal(3.6, Vol(f, id), 6);
            var b = f.Bounds(id);
            Assert.Equal(-1, b.Min.X, 6); Assert.Equal(11, b.Max.X, 6);
            Assert.Equal(-1, b.Min.Y, 6); Assert.Equal(7, b.Max.Y, 6);
            Assert.Equal(0, b.Min.Z, 9); Assert.Equal(0.1, b.Max.Z, 9);
        }
    }

    [Fact]
    public void Inside_centred_and_with_a_gap()
    {
        // Inside: (60 − 8 · 4) · 0.1 = 2.8; centred on the outline: (11 · 7 − 9 · 5) · 0.1 = 3.2.
        var (fi, i) = Pavement("inside");
        Assert.Equal(2.8, Vol(fi, i), 6);
        var (fc, c) = Pavement("centre");
        Assert.Equal(3.2, Vol(fc, c), 6);
        // 0.2 m off the outside face: (12.4 · 8.4 − 10.4 · 6.4) · 0.1 = 3.76.
        var (fg, g) = Pavement("outside", gap: 0.2);
        Assert.Equal(3.76, Vol(fg, g), 6);
        Assert.Equal(-1.2, fg.Bounds(g).Min.X, 6);
    }

    [Fact]
    public void An_open_path_takes_the_side_it_is_asked_for()
    {
        var f = F();
        var plan = Plan(f);
        var profile = Draw(f, plan, Rect(30, 0, 31, 0.1));
        // An L turning left: its right side is the outer one, 5 + 5 + the 1 × 1 corner = 11 m² · 0.1.
        var path = Draw(f, plan, Poly(false, (0, 0), (5, 0), (5, 5)));
        var right = (string)f.Run("studio.sweep", new JsonObject { ["profileId"] = profile, ["pathId"] = path, ["place"] = "right" })!["id"]!;
        Assert.Equal(1.1, Vol(f, right), 6);
        Assert.Equal(-1, f.Bounds(right).Min.Y, 6);
        // On the left (the inner side): 4 + 5 = 9 m² · 0.1, mitred into the corner.
        f.Run("element.update", new JsonObject { ["id"] = right, ["attrs"] = new JsonObject { ["place"] = "left" } });
        Assert.Equal(0.9, Vol(f, right), 6);
        Assert.Equal(0, f.Bounds(right).Min.Y, 6);
    }

    [Fact]
    public void A_side_too_short_for_its_mitres_says_so()
    {
        var f = F();
        var plan = Plan(f);
        var profile = Draw(f, plan, Rect(30, 0, 31, 0.1));
        // A 10 × 0.5 outline with a 1 m section inside: each short side would need 2 m.
        var path = Draw(f, plan, Rect(0, 0, 10, 0.5));
        var id = (string)f.Run("studio.sweep", new JsonObject { ["profileId"] = profile, ["pathId"] = path, ["place"] = "inside" })!["id"]!;
        var g = f.Session.Geometry(id)!;
        Assert.Empty(g.Parts);
        Assert.Contains(g.Diagnostics!, d => d.Contains("too short"));
        Assert.Throws<Lapin.Platform.CommandException>(() => f.Run("studio.sweep", new JsonObject { ["profileId"] = Draw(f, plan, Rect(40, 0, 41, 1)), ["pathId"] = path, ["place"] = "above" }));
    }
}
