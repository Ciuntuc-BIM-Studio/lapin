using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Editing ellipses in Draft (audit §18 leftovers): Offset lays the curve at a distance (no ellipse) as a polyline within the one
/// rule's millimetre, refused inwards past its tightest curvature; lines meet an ellipse exactly (Extend to it, Trim by it); an
/// elliptical arc extends round its ellipse; a line and an ellipse are filleted by a circle touching both.
/// </summary>
public sealed class DraftEllipseEditTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static string Draw(Fixture f, string view, JsonObject shape) => (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = shape })!["ids"]![0]!;
    static Shape S(Fixture f, string id) => Drafts.ShapeOf(f.Model.Get(id)!)!;

    /// <summary>How far a point lies from the ellipse x²/a² + y²/b² = 1 (centred at 0, axes along x and y), by its nearest point (sampled finely, then refined).</summary>
    static double DistanceToEllipse(Vec2 p, double a, double b)
    {
        double D(double t) => (new Vec2(a * Math.Cos(t), b * Math.Sin(t)) - p).Length;
        var best = Enumerable.Range(0, 3600).Select(k => 2 * Math.PI * k / 3600).MinBy(D);
        double lo = best - 2 * Math.PI / 3600, hi = best + 2 * Math.PI / 3600;
        for (var i = 0; i < 100; i++) { var (m1, m2) = (lo + (hi - lo) / 3, hi - (hi - lo) / 3); if (D(m1) < D(m2)) hi = m2; else lo = m1; }
        return D((lo + hi) / 2);
    }

    [Fact]
    public void An_ellipse_offset_is_a_polyline_within_a_millimetre_of_the_curve_at_that_distance()
    {
        var e = new Ellipse(new Vec2(0, 0), new Vec2(3, 0), 0.5, 0, 0);
        var outside = Assert.IsType<Polyline>(Ops.Offset(e, 0.4, new Vec2(5, 0)));
        Assert.True(outside.Closed);
        // Its corners lie on the curve 0.4 m out; its chords' middles no more than a millimetre off it.
        for (var i = 0; i < outside.Pts.Count; i++)
        {
            var (p, q) = (outside.Pts[i], outside.Pts[(i + 1) % outside.Pts.Count]);
            Assert.Equal(0.4, DistanceToEllipse(p, 3, 1.5), 6);
            Assert.True(0.4 - DistanceToEllipse((p + q) * 0.5, 3, 1.5) <= Chords.Tolerance + 1e-9);
        }
        var inside = Assert.IsType<Polyline>(Ops.Offset(e, 0.3, new Vec2(0, 0)));
        Assert.All(inside.Pts, p => Assert.Equal(0.3, DistanceToEllipse(p, 3, 1.5), 6));
        Assert.True(Math.Abs(Polygon2.SignedArea(inside.Pts)) < Math.PI * 3 * 1.5);
        // Inwards by its tightest radius of curvature (b²/a = 0.75) or more, the curve would fold: none.
        Assert.Null(Ops.Offset(e, 0.75, new Vec2(0, 0)));
        // An elliptical arc gives an open polyline from end to end.
        var half = Assert.IsType<Polyline>(Ops.Offset(e with { Start = 0, End = Math.PI }, 0.5, new Vec2(0, 5)));
        Assert.False(half.Closed);
        Assert.True((half.Pts[0] - new Vec2(3.5, 0)).Length < 1e-9);
        Assert.True((half.Pts[^1] - new Vec2(-3.5, 0)).Length < 1e-9);
    }

    [Fact]
    public void Offset_Extend_and_Fillet_on_an_ellipse_are_commands_of_one_step_each()
    {
        var f = F();
        var plan = Plan(f);
        var ellipse = Draw(f, plan, new JsonObject { ["type"] = "ellipse", ["c"] = J(0, 0), ["m"] = J(3, 0), ["ratio"] = 0.5 });
        var off = (string)f.Run("drafting.offset", new JsonObject { ["id"] = ellipse, ["distance"] = 0.25, ["side"] = J(0, 3) })!["id"]!;
        Assert.IsType<Polyline>(S(f, off));
        f.Session.Undo();
        Assert.Null(f.Model.Get(off));
        // A line short of the ellipse carried on to it, exactly: x = 1 meets it at y = 1.5·√(1 − 1/9).
        var line = Draw(f, plan, new JsonObject { ["type"] = "line", ["a"] = J(1, -4), ["b"] = J(1, -3) });
        f.Run("drafting.extend", new JsonObject { ["id"] = line, ["at"] = J(1, -3.1) });
        var l = Assert.IsType<Line>(S(f, line));
        Assert.Equal(-1.5 * Math.Sqrt(1 - 1.0 / 9), l.B.Y, 9);
        // An elliptical arc (the upper half) carried on round its ellipse to a line across it.
        var arc = Draw(f, plan, new JsonObject { ["type"] = "ellipse", ["c"] = J(10, 0), ["m"] = J(3, 0), ["ratio"] = 0.5, ["start"] = 0, ["end"] = Math.PI });
        Draw(f, plan, new JsonObject { ["type"] = "line", ["a"] = J(10, -3), ["b"] = J(10, 0) });
        f.Run("drafting.extend", new JsonObject { ["id"] = arc, ["at"] = J(13, 0.1) });
        var ext = Assert.IsType<Ellipse>(S(f, arc));
        // Picked at its start (13, 0): carried back clockwise round the bottom to x = 10, its parameter 3π/2.
        Assert.Equal(3 * Math.PI / 2, ext.Start, 9);
        Assert.Equal(Math.PI, ext.End, 9);
    }

    [Fact]
    public void A_line_and_an_ellipse_are_filleted_by_a_circle_touching_both()
    {
        var f = F();
        var plan = Plan(f);
        // An elliptical arc (the right half, from −90° to 90°) and a horizontal line through its centre, crossing it at (3, 0).
        var arc = Draw(f, plan, new JsonObject { ["type"] = "ellipse", ["c"] = J(0, 0), ["m"] = J(3, 0), ["ratio"] = 0.5, ["start"] = -Math.PI / 2, ["end"] = Math.PI / 2 });
        var line = Draw(f, plan, new JsonObject { ["type"] = "line", ["a"] = J(0, 0), ["b"] = J(6, 0) });
        // The line's part outside (picked at x = 5) and the ellipse's upper quarter (picked near its top).
        var r = (JsonObject)f.Run("drafting.fillet", new JsonObject { ["a"] = line, ["atA"] = J(5, 0), ["b"] = arc, ["atB"] = J(1, 1.4), ["radius"] = 0.3 })!;
        var fillet = Assert.IsType<Arc>(S(f, (string)r["id"]!));
        Assert.Equal(0.3, fillet.R, 9);
        var l = Assert.IsType<Line>(S(f, line));
        var e = Assert.IsType<Ellipse>(S(f, arc));
        // The circle touches the line (centre 0.3 above it) at the line's new start, and the ellipse at the arc's new start: the piece
        // below the touching point goes, the upper one picked stays up to its end.
        Assert.Equal(0.3, fillet.C.Y, 6);
        Assert.True((l.A - new Vec2(fillet.C.X, 0)).Length < 1e-6);
        Assert.Equal(new Vec2(6, 0), l.B);
        var touch = e.At(e.Start);
        Assert.Equal(Math.PI / 2, e.End, 9);
        Assert.Equal(0.3, (touch - fillet.C).Length, 6);
        // Tangent there: the radius to it square to the ellipse's tangent.
        var tg = e.M * -Math.Sin(e.Start) + e.N * Math.Cos(e.Start);
        Assert.True(Math.Abs(Vec2.Dot(Geo.Norm(tg), Geo.Norm(touch - fillet.C))) < 1e-6);
        Assert.True(e.Start > 0 && e.Start < Math.PI / 2);
        // Both ends of the fillet are the touching points.
        Assert.Contains(new[] { fillet.At(fillet.Start), fillet.At(fillet.End) }, p => (p - touch).Length < 1e-6);
        Assert.Contains(new[] { fillet.At(fillet.Start), fillet.At(fillet.End) }, p => (p - l.A).Length < 1e-6);
        // Inside the ellipse, a circle wider than the ellipse never touches both: refused.
        var whole = Draw(f, plan, new JsonObject { ["type"] = "ellipse", ["c"] = J(20, 0), ["m"] = J(3, 0), ["ratio"] = 0.5 });
        var chord = Draw(f, plan, new JsonObject { ["type"] = "line", ["a"] = J(18, -0.2), ["b"] = J(22, -0.2) });
        Assert.Throws<CommandException>(() => f.Run("drafting.fillet", new JsonObject { ["a"] = chord, ["atA"] = J(19, -0.2), ["b"] = whole, ["atB"] = J(20, 1.5), ["radius"] = 5 }));
        // A smaller one does, the whole ellipse kept whole.
        f.Run("drafting.fillet", new JsonObject { ["a"] = chord, ["atA"] = J(19, -0.2), ["b"] = whole, ["atB"] = J(20, 1.5), ["radius"] = 0.4 });
        Assert.True(Assert.IsType<Ellipse>(S(f, whole)).Whole);
        // Chamfer still takes two lines only.
        Assert.Throws<CommandException>(() => f.Run("drafting.chamfer", new JsonObject { ["a"] = line, ["atA"] = J(5, 0), ["b"] = arc, ["atB"] = J(1, 1.4), ["d1"] = 0.3 }));
    }
}
