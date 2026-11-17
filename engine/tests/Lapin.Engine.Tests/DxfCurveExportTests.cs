using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Drawing.Export;
using Lapin.Drawing.Import;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// DXF writes real curves (audit §18 leftover): drafted circles as CIRCLE, arcs as ARC, ellipses and elliptical arcs as
/// ELLIPSE, from the exact curves the pens carry beside their chords; the chords a viewport cut keeps their piece as lines;
/// a DXF written then read back keeps them.
/// </summary>
public sealed class DxfCurveExportTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;

    static void DrawCurves(Fixture f, string view) => f.Run("drafting.draw", new JsonObject
    {
        ["viewId"] = view,
        ["shapes"] = new JsonArray(
            new JsonObject { ["type"] = "line", ["a"] = J(0, 0), ["b"] = J(4, 0) },
            new JsonObject { ["type"] = "circle", ["c"] = J(2, 2), ["r"] = 1 },
            new JsonObject { ["type"] = "arc", ["c"] = J(6, 2), ["r"] = 1.5, ["start"] = 0, ["end"] = Math.PI / 2 },
            new JsonObject { ["type"] = "ellipse", ["c"] = J(10, 2), ["m"] = J(2, 0), ["ratio"] = 0.5 },
            new JsonObject { ["type"] = "ellipse", ["c"] = J(15, 2), ["m"] = J(0, 2), ["ratio"] = 0.25, ["start"] = 0, ["end"] = Math.PI }),
    });

    static DxfDocument Read(byte[] bytes) => DxfReader.Read(bytes);

    [Fact]
    public void A_view_writes_its_drafted_circles_arcs_and_ellipses_as_curves_and_only_its_lines_as_lines()
    {
        var f = F();
        var plan = Plan(f);
        DrawCurves(f, plan);
        var doc = Read(Views.Dxf(f.Session.Reader, Views.Find(f.Model, plan)!));
        var circle = Assert.Single(doc.Entities.OfType<DxfCircle>());
        Assert.Equal(1000, circle.R, 3);
        Assert.Equal(new Vec2(2000, 2000), circle.C);
        var arc = Assert.Single(doc.Entities.OfType<DxfArc>());
        Assert.Equal(1500, arc.R, 3);
        Assert.Equal(0, arc.StartDeg, 3);
        Assert.Equal(90, arc.EndDeg, 3);
        var ellipses = doc.Entities.OfType<DxfEllipse>().OrderBy(e => e.C.X).ToList();
        Assert.Equal(2, ellipses.Count);
        Assert.Equal(new Vec2(10000, 2000), ellipses[0].C);
        Assert.Equal(0.5, ellipses[0].Ratio, 6);
        Assert.Equal(2000, ellipses[0].Major.Length, 3);
        Assert.Equal(2 * Math.PI, ellipses[0].End - ellipses[0].Start, 6);
        // The half ellipse: the same points, by its principal axes (its major one along y).
        var half = ellipses[1];
        Assert.Equal(0.25, half.Ratio, 6);
        Assert.Equal(Math.PI, half.End - half.Start, 6);
        Vec2 At(DxfEllipse e, double t) => e.C + e.Major * Math.Cos(t) + new Vec2(-e.Major.Y, e.Major.X) * (e.Ratio * Math.Sin(t));
        Assert.True((At(half, half.Start) - new Vec2(15000, 4000)).Length < 1e-3);
        Assert.True((At(half, half.End) - new Vec2(15000, 0)).Length < 1e-3);
        Assert.True((At(half, (half.Start + half.End) / 2) - new Vec2(14500, 2000)).Length < 1e-3);
        // The line alone stays a line: no chord of the curves is left behind.
        Assert.Single(doc.Entities.OfType<DxfLine>());
    }

    [Fact]
    public void A_sheet_writes_them_too_flipped_and_scaled_and_a_viewport_cut_keeps_the_rest_as_lines()
    {
        var f = F();
        var plan = Plan(f);
        DrawCurves(f, plan);
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "D", ["paper"] = "A1" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = plan });
        var doc = Read(Sheets.Dxf(f.Session.Reader, f.Model.Get(s)!));
        // At 1:100 a 1 m circle is 10 mm on the paper.
        var circle = Assert.Single(doc.Entities.OfType<DxfCircle>(), c => Math.Abs(c.R - 10) < 1e-3);
        Assert.Contains(doc.Entities.OfType<DxfArc>(), a => Math.Abs(a.R - 15) < 1e-3 && Math.Abs(Geo.Sweep(a.StartDeg * Math.PI / 180, a.EndDeg * Math.PI / 180) - Math.PI / 2) < 1e-6);
        Assert.Equal(2, doc.Entities.OfType<DxfEllipse>().Count());
        _ = circle;
    }

    [Fact]
    public void Chords_cut_by_a_box_give_the_piece_left_as_an_arc_and_the_cut_chord_as_a_line()
    {
        var pen = new Pen("A", "#000000", 0.25, null, null);
        var ring = Chords.Ring(new Vec2(0, 0), 1);
        for (var i = 0; i < ring.Count; i++) pen.Segments.Add((ring[i], ring[(i + 1) % ring.Count]));
        pen.Curves.Add(PenCurve.Circle(new Vec2(0, 0), 1));
        var clipped = Sheets.ClipToBox(pen, p => p, -2, -2, 2, 0.5)!;
        var (runs, lines) = DxfWriter.Split(clipped.Segments, clipped.Curves);
        var run = Assert.Single(runs);
        Assert.True(run.To - run.From < 2 * Math.PI - 0.5);
        Assert.Equal(2, lines.Count);
        // Mirrored (the paper's flip), the circle still reads whole.
        var flipped = PaperViews.Map(pen, p => new Vec2(p.X, -p.Y));
        var (whole, rest) = DxfWriter.Split(flipped.Segments, flipped.Curves);
        Assert.Empty(rest);
        Assert.True(Assert.Single(whole).To - whole[0].From >= 2 * Math.PI - 1e-9);
    }

    [Fact]
    public void A_view_written_to_DXF_and_imported_keeps_its_circles_arcs_and_ellipses()
    {
        var f = F();
        var plan = Plan(f);
        DrawCurves(f, plan);
        var bytes = Views.Dxf(f.Session.Reader, Views.Find(f.Model, plan)!);
        var other = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Imported" })!["id"]!;
        f.Run("drafting.importDxf", new JsonObject { ["viewId"] = other, ["name"] = "back.dxf", ["data"] = Convert.ToBase64String(bytes) });
        var shapes = Drafts.On(f.Model, other).Select(Drafts.ShapeOf).OfType<Shape>().ToList();
        var c = Assert.Single(shapes.OfType<Circle>());
        Assert.Equal(1, c.R, 6);
        var a = Assert.Single(shapes.OfType<Arc>());
        Assert.Equal(1.5, a.R, 6);
        Assert.Equal(Math.PI / 2, a.Sweep, 6);
        var es = shapes.OfType<Ellipse>().ToList();
        Assert.Equal(2, es.Count);
        Assert.Contains(es, e => e.Whole && Math.Abs(Geo.Area(e)!.Value - Math.PI * 2 * 1) < 1e-6);
        Assert.Contains(es, e => !e.Whole && Math.Abs(e.Sweep - Math.PI) < 1e-6 && Math.Abs(e.A - 2) < 1e-6);
    }

    [Fact]
    public void A_plan_writes_a_floors_arc_edge_as_an_ARC_and_an_elliptical_floor_as_ELLIPSE_pieces()
    {
        var f = F();
        // A 6 × 4 floor whose bottom edge bulges 1 m outwards (its rise to the right of the way it runs).
        f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray(J(0, 0), J(6, 0), J(6, 4), J(0, 4)), ["arcs"] = new JsonArray(-1, 0, 0, 0),
        });
        var (pts, el) = EllipseEdge.Whole(new Vec2(20, 0), new Vec2(3, 0), 0.5);
        f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray([.. pts.Select(p => (JsonNode)J(p.X, p.Y))]),
            [EllipseEdge.Key] = new JsonArray([.. el.Select(x => (JsonNode?)x!.ToJson())]),
        });
        var doc = Read(Views.Dxf(f.Session.Reader, Views.Find(f.Model, "plan:" + f.Storey)!));
        // The floor's arc: through (0, 0) and (6, 0), 1 m below its chord at its middle: r = (9 + 1) / 2 = 5, centre (3, 4).
        Assert.Contains(doc.Entities.OfType<DxfArc>(), a => Math.Abs(a.R - 5000) < 1e-2 && (a.C - new Vec2(3000, 4000)).Length < 1e-2);
        // The elliptical floor's outline (each quarter an elliptical edge), wherever the plan draws it, as ELLIPSE, never as its chords.
        var ellipses = doc.Entities.OfType<DxfEllipse>().Where(e => (e.C - new Vec2(20000, 0)).Length < 1e-2).ToList();
        Assert.NotEmpty(ellipses);
        Assert.All(ellipses, e => Assert.Equal(0.5, e.Ratio, 6));
    }
}
