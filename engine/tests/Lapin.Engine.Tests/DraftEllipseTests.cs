using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// A true ellipse in Draft (audit S4): stored as an ellipse (centre, major semi-axis, ratio, optional start and end parameters),
/// laid by the one rule for curves, snapped at its centre and its axes' ends, trimmed by its own parameter (offset as a polyline),
/// kept as an ellipse from a DXF ELLIPSE, made into an elliptical floor by Convert to… and by Edit sketch.
/// </summary>
public sealed class DraftEllipseTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static JsonObject E(double cx, double cy, double mx, double my, double ratio, double? start = null, double? end = null)
    {
        var o = new JsonObject { ["type"] = "ellipse", ["c"] = J(cx, cy), ["m"] = J(mx, my), ["ratio"] = ratio };
        if (start != null) { o["start"] = start; o["end"] = end; }
        return o;
    }
    static string Draw(Fixture f, string view, JsonObject shape) => (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = shape })!["ids"]![0]!;

    [Fact]
    public void An_ellipse_is_kept_laid_within_a_millimetre_and_snapped_at_its_axes()
    {
        var f = F();
        var plan = Plan(f);
        var id = Draw(f, plan, E(1, 2, 3, 0, 0.5));
        var s = Assert.IsType<Ellipse>(Drafts.ShapeOf(f.Model.Get(id)!));
        Assert.True(s.Whole);
        Assert.Equal(Math.PI * 3 * 1.5, Geo.Area(s)!.Value, 9);
        var k = Geo.Keys(s);
        Assert.Equal(new Vec2(1, 2), k.Centers.Single());
        Assert.Equal(4, k.Quadrants.Count);
        Assert.Contains(k.Quadrants, q => (q - new Vec2(4, 2)).Length < 1e-9);
        Assert.Contains(k.Quadrants, q => (q - new Vec2(1, 3.5)).Length < 1e-9);
        var run = Drafts.Flatten(s)[0];
        for (var i = 0; i + 1 < run.Count; i++)
        {
            var m = (run[i] + run[i + 1]) * 0.5 - new Vec2(1, 2);
            Assert.True((1 - Math.Sqrt(m.X * m.X / 9 + m.Y * m.Y / 2.25)) * 1.5 <= Chords.Tolerance + 1e-9);
        }
        // Turned and mirrored, still the ellipse.
        var turned = (Ellipse)Geo.Rotate(s, new Vec2(0, 0), Math.PI / 2);
        Assert.True((turned.M - new Vec2(0, 3)).Length < 1e-9);
        var mirrored = (Ellipse)Geo.Mirror(new Ellipse(new Vec2(0, 0), new Vec2(2, 0), 0.5, 0, Math.PI / 2), new Vec2(0, 0), new Vec2(0, 1));
        // The quarter from +x to +y mirrored about x = 0 runs from +y to −x.
        Assert.True((mirrored.At(mirrored.Start) - new Vec2(0, 1)).Length < 1e-9);
        Assert.True((mirrored.At(mirrored.End) - new Vec2(-2, 0)).Length < 1e-9);
    }

    [Fact]
    public void An_ellipse_is_trimmed_into_elliptical_arcs_and_offset_as_a_polyline()
    {
        var e = new Ellipse(new Vec2(0, 0), new Vec2(3, 0), 0.5, 0, 0);
        var cutter = new Line(new Vec2(0, -5), new Vec2(0, 5));
        var pieces = Ops.Trim(e, [cutter], new Vec2(3, 0))!;
        var arc = Assert.IsType<Ellipse>(Assert.Single(pieces));
        // Trim takes away the piece picked: the left half stays.
        Assert.False(arc.Whole);
        Assert.True(arc.At(arc.Start + arc.Sweep / 2).X < 0);
        // Offset: no ellipse, a polyline (audit §18 leftovers; DraftEllipseEditTests).
        Assert.IsType<Polyline>(Ops.Offset(e, 0.2, new Vec2(5, 0)));
    }

    [Fact]
    public void A_dxf_ellipse_comes_in_as_an_ellipse()
    {
        var f = F();
        var plan = Plan(f);
        var dxf = string.Join("\n", "0", "SECTION", "2", "HEADER", "9", "$INSUNITS", "70", "6", "0", "ENDSEC", "0", "SECTION", "2", "ENTITIES",
            "0", "ELLIPSE", "8", "0", "10", "1", "20", "1", "30", "0", "11", "2", "21", "0", "31", "0", "40", "0.5", "41", "0", "42", "6.283185307179586",
            "0", "ENDSEC", "0", "EOF");
        f.Run("drafting.importDxf", new JsonObject { ["viewId"] = plan, ["name"] = "e.dxf", ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(dxf)) });
        var e = Assert.IsType<Ellipse>(Drafts.On(f.Model, plan).Select(Drafts.ShapeOf).Single(s => s != null));
        Assert.True(e.Whole);
        Assert.Equal(0.5, e.Ratio, 9);
        // Its semi-axes 2 and 1 (in metres: the file is in metres).
        Assert.Equal(Math.PI * 2 * 1, Geo.Area(e)!.Value, 6);
    }

    [Fact]
    public void A_drafted_ellipse_becomes_an_elliptical_floor_and_its_sketch_keeps_it()
    {
        var f = F();
        var plan = Plan(f);
        var id = Draw(f, plan, E(0, 0, 3, 0, 2.0 / 3));
        f.Run("drafting.convert", new JsonObject { ["viewId"] = plan, ["ids"] = new JsonArray(id), ["converter"] = "floor", ["options"] = new JsonObject() });
        var slab = f.Model.OfClass(IfcClass.Slab).Single();
        Assert.Equal(Math.PI * 3 * 2, Slabs.Area(slab), 9);
        // Edit sketch: one elliptical arc per quarter; finished, the same ellipse.
        var r = (JsonObject)f.Run("studio.editOutline", new JsonObject { ["id"] = slab.Id })!;
        var view = (string)r["viewId"]!;
        Assert.All(Drafts.On(f.Model, view), d => Assert.IsType<Ellipse>(Drafts.ShapeOf(d)));
        f.Run("studio.finishOutline", new JsonObject { ["viewId"] = view });
        Assert.Equal(Math.PI * 3 * 2, Slabs.Area(f.Model.Get(slab.Id)!), 6);
        // A whole ellipse drawn alone in a floor's sketch replaces its outline.
        var floor = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(J(10, 0), J(14, 0), J(14, 3), J(10, 3)) })!["id"]!;
        var v2 = (string)((JsonObject)f.Run("studio.editOutline", new JsonObject { ["id"] = floor })!)["viewId"]!;
        foreach (var d in Drafts.On(f.Model, v2).ToList()) f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(d.Id) });
        Draw(f, v2, E(12, 1.5, 2, 0, 0.75));
        f.Run("studio.finishOutline", new JsonObject { ["viewId"] = v2 });
        Assert.Equal(Math.PI * 2 * 1.5, Slabs.Area(f.Model.Get(floor)!), 6);
    }
}
