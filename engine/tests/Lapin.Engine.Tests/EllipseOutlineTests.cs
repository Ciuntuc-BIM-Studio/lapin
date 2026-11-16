using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Elliptical edges on outlines (audit S4): an edge may run along an ellipse (<c>ellipses</c> beside the corners: centre, major
/// semi-axis, ratio, way round), laid in chords by the one rule (<see cref="Chords.EllipseArc"/>), its area exact (½∮x dy − y dx
/// along the ellipse), its length by quadrature; floors, floor openings, ceilings and drawn rooms; carried by transforms; IFC writes
/// the true curve (IfcTrimmedCurve on IfcEllipse) and reads it back.
/// </summary>
public sealed class EllipseOutlineTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ellipse-outlines").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static double Q(Fixture f, string id, string set, string name) => f.Session.Quantities(id).First(q => q.Name == set).Quantities.Single(x => x.Name == name).Value;
    const double A = 3, B = 2;

    static JsonObject Whole(Vec2 c, Vec2 major, double ratio, JsonObject a)
    {
        var (pts, el) = EllipseEdge.Whole(c, major, ratio);
        a["points"] = new JsonArray([.. pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]);
        a[EllipseEdge.Key] = new JsonArray([.. el.Select(x => (JsonNode?)x!.ToJson())]);
        return a;
    }

    static double Ramanujan(double a, double b) { var h = (a - b) * (a - b) / ((a + b) * (a + b)); return Math.PI * (a + b) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h))); }

    [Fact]
    public void An_ellipse_measured_exactly_and_laid_within_a_millimetre()
    {
        var (pts, el) = EllipseEdge.Whole(new Vec2(1, 1), new Vec2(A, 0), B / A);
        Assert.Equal(Math.PI * A * B, ArcOutline.SignedArea(pts, null, el), 9);
        Assert.Equal(Ramanujan(A, B), ArcOutline.Perimeter(pts, null, el), 6);
        var laid = ArcOutline.Points(pts, null, null, el);
        Assert.True(laid.Count >= 24);
        // Every chord's middle within the tolerance of the ellipse ((x/a)² + (y/b)² = 1 there, scaled back to metres).
        for (var i = 0; i < laid.Count; i++)
        {
            var m = (laid[i] + laid[(i + 1) % laid.Count]) * 0.5 - new Vec2(1, 1);
            var r = Math.Sqrt(m.X * m.X / (A * A) + m.Y * m.Y / (B * B));
            Assert.True((1 - r) * B <= Chords.Tolerance + 1e-9, $"{(1 - r) * B}");
        }
        // Turned: the same.
        var (tp, te) = EllipseEdge.Whole(new Vec2(0, 0), new Vec2(Math.Cos(0.5), Math.Sin(0.5)) * A, B / A);
        Assert.Equal(Math.PI * A * B, ArcOutline.SignedArea(tp, null, te), 9);
    }

    [Fact]
    public void An_elliptical_floor_ceiling_opening_and_room()
    {
        var f = new Fixture();
        var slab = (string)f.Run("architecture.addSlab", Whole(new Vec2(0, 0), new Vec2(A, 0), B / A, new JsonObject { ["storeyId"] = f.Storey }))!["id"]!;
        Assert.Equal(Math.PI * A * B, Q(f, slab, "Qto_SlabBaseQuantities", "GrossArea"), 9);
        Assert.Equal(Ramanujan(A, B), Q(f, slab, "Qto_SlabBaseQuantities", "Perimeter"), 6);
        var vol = f.Session.Geometry(slab)!.Parts.Sum(p => Measure.Volume(p.Solid));
        Assert.True(Math.Abs(vol - Math.PI * A * B * 0.22) / (Math.PI * A * B * 0.22) < 0.002, $"{vol}");
        var opening = (string)f.Run("architecture.addSlabOpening", Whole(new Vec2(0, 0), new Vec2(0, 1), 0.5, new JsonObject { ["storeyId"] = f.Storey }))!["id"]!;
        Assert.NotNull(f.Model.Get(opening)!.Attrs[EllipseEdge.Key]);
        var ceiling = (string)f.Run("architecture.addCeiling", Whole(new Vec2(10, 0), new Vec2(A, 0), B / A, new JsonObject { ["storeyId"] = f.Storey }))!["id"]!;
        Assert.Equal(Math.PI * A * B, Q(f, ceiling, "Qto_CoveringBaseQuantities", "GrossArea"), 9);
        var room = (string)f.Run("architecture.addRoom", Whole(new Vec2(20, 0), new Vec2(A, 0), B / A, new JsonObject { ["storeyId"] = f.Storey }))!["id"]!;
        Assert.Equal(Math.PI * A * B, Q(f, room, "Qto_SpaceBaseQuantities", "GrossFloorArea"), 9);
        // A corner beside an elliptical edge is not filleted.
        Assert.Throws<CommandException>(() => f.Run("architecture.roundCorner", new JsonObject { ["id"] = slab, ["index"] = 0, ["radius"] = 0.2 }));
        // Edges whose corners left their ellipse go straight; the others stay.
        var corners = ArcOutline.CornersOf(f.Model.Get(slab)!);
        corners[0] = corners[0] + new Vec2(1, 0);
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = new JsonArray([.. corners.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]) });
        var kept = EllipseEdge.Of(f.Model.Get(slab)!)!;
        Assert.Equal(2, kept.Count(x => x != null));
        Assert.Null(kept[0]);
        Assert.Null(kept[3]);
        // A corner given off its ellipse is refused.
        var bad = Whole(new Vec2(0, 0), new Vec2(A, 0), B / A, new JsonObject { ["storeyId"] = f.Storey });
        ((JsonArray)bad["points"]!)[0] = new JsonArray(A + 0.5, 0);
        Assert.Throws<CommandException>(() => f.Run("architecture.addSlab", bad));
    }

    [Fact]
    public void Mirrored_and_moved_an_elliptical_floor_keeps_its_area()
    {
        var f = new Fixture();
        var slab = (string)f.Run("architecture.addSlab", Whole(new Vec2(0, 0), new Vec2(Math.Cos(0.3), Math.Sin(0.3)) * A, B / A, new JsonObject { ["storeyId"] = f.Storey }))!["id"]!;
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(slab), ["op"] = "mirror", ["a"] = new JsonArray(5, 0), ["b"] = new JsonArray(5, 1) });
        Assert.Equal(Math.PI * A * B, Q(f, slab, "Qto_SlabBaseQuantities", "GrossArea"), 6);
        var c = Polygon2.Centroid(Slabs.Contour(f.Model.Get(slab)!));
        Assert.Equal(10, c.X, 3);
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(slab), ["op"] = "rotate", ["center"] = new JsonArray(0, 0), ["angle"] = 40 });
        Assert.Equal(Math.PI * A * B, Q(f, slab, "Qto_SlabBaseQuantities", "GrossArea"), 6);
    }

    [Fact]
    public void Ifc_carries_the_ellipse_and_reads_it_back()
    {
        var f = new Fixture();
        var slab = (string)f.Run("architecture.addSlab", Whole(new Vec2(0, 0), new Vec2(A, 0), B / A, new JsonObject { ["storeyId"] = f.Storey }))!["id"]!;
        var path = Path.Combine(_dir, "ellipse.ifc");
        IfcExport.Write(f.Session, path);
        var text = File.ReadAllText(path);
        Assert.Equal(4, StepFile.Read(path).OfType("IFCTRIMMEDCURVE").Count());
        Assert.Contains("IFCELLIPSE(", text);
        var g = new Fixture();
        IfcImport.Run(g.Session, path, null, out _);
        Assert.Equal(Math.PI * A * B, Q(g, slab, "Qto_SlabBaseQuantities", "GrossArea"), 6);
    }
}
