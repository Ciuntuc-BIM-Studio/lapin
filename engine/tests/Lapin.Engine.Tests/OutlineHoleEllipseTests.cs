using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// An elliptical edge on an outline's own hole (audit §18 leftovers): a floor's hole may run along an ellipse (<c>holeEllipses</c>
/// beside <c>holes</c>), measured exactly, built in chords by the one rule, moved and mirrored with the floor, kept through Edit
/// sketch, written to IFC as an IfcEllipse and to DXF as ELLIPSE.
/// </summary>
public sealed class OutlineHoleEllipseTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-hole-ellipse").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture F() => new(new ArchitectureModule(), new CivilModule(), new Lapin.Drafting.DraftingModule(), new StudioModule());
    static JsonArray Pts(IEnumerable<Vec2> p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);
    static double Q(Fixture f, string id) => f.Session.Quantities(id).First(q => q.Name == "Qto_SlabBaseQuantities").Quantities.Single(x => x.Name == "GrossArea").Value;

    static (string Slab, double HoleArea) FloorWithEllipticalHole(Fixture f)
    {
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts([new(0, 0), new(10, 0), new(10, 8), new(0, 8)]) })!["id"]!;
        var (pts, el) = EllipseEdge.Whole(new Vec2(5, 4), new Vec2(2, 0), 0.5);
        f.Run("architecture.addOutlineHole", new JsonObject
        {
            ["id"] = slab, ["points"] = Pts(pts), [EllipseEdge.Key] = new JsonArray([.. el.Select(x => (JsonNode?)x!.ToJson())]),
        });
        return (slab, Math.PI * 2 * 1);
    }

    [Fact]
    public void A_floor_takes_an_elliptical_hole_measured_exactly_and_moved_with_it()
    {
        var f = F();
        var (slab, hole) = FloorWithEllipticalHole(f);
        var h = Assert.Single(OutlineHoles.Of(f.Model.Get(slab)!));
        Assert.NotNull(h.Ellipses);
        Assert.Equal(80 - hole, Q(f, slab), 9);
        // Built along the ellipse: its loop's corners on it.
        Assert.True(h.Points.Count >= 24);
        Assert.All(h.Points, p => Assert.True(h.Ellipses!.First(x => x != null)!.Off(p) < 1e-9));
        // A clockwise hole given, kept anticlockwise, its edges run back with it.
        f.Session.Undo();
        var (pts, el) = EllipseEdge.Whole(new Vec2(5, 4), new Vec2(2, 0), 0.5);
        var back = Enumerable.Range(0, 4).Select(j => el[((4 - 2 - j) % 4 + 4) % 4]!.Reversed).ToArray();
        pts.Reverse();
        f.Run("architecture.addOutlineHole", new JsonObject { ["id"] = slab, ["points"] = Pts(pts), [EllipseEdge.Key] = new JsonArray([.. back.Select(x => (JsonNode?)x.ToJson())]) });
        Assert.Equal(80 - hole, Q(f, slab), 9);
        // Moved and mirrored with the floor: the same area, its corners still on its edges' ellipses.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(slab), ["op"] = "move", ["by"] = new JsonArray(1, 0) });
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(slab), ["op"] = "mirror", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(0, 1) });
        var moved = Assert.Single(OutlineHoles.Of(f.Model.Get(slab)!));
        Assert.Equal(80 - hole, Q(f, slab), 9);
        Assert.Equal(-6, moved.Ellipses!.First(x => x != null)!.C.X, 9);
        for (var i = 0; i < moved.Corners.Count; i++)
            if (moved.Ellipses![i] is { } x) Assert.True(x.Off(moved.Corners[i]) < 1e-9 && x.Off(moved.Corners[(i + 1) % 4]) < 1e-9);
    }

    [Fact]
    public void Edit_sketch_keeps_an_elliptical_hole_and_takes_an_ellipse_drawn_as_one()
    {
        var f = F();
        var (slab, hole) = FloorWithEllipticalHole(f);
        var r = (JsonObject)f.Run("studio.editOutline", new JsonObject { ["id"] = slab })!;
        var view = (string)r["viewId"]!;
        Assert.Equal(4, Drafts.On(f.Model, view).Count(d => Drafts.ShapeOf(d) is Ellipse));
        // A second ellipse drawn whole inside: a second hole.
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = new JsonObject { ["type"] = "ellipse", ["c"] = new JsonArray(2, 2), ["m"] = new JsonArray(0, 1), ["ratio"] = 0.5 } });
        f.Run("studio.finishOutline", new JsonObject { ["viewId"] = view });
        Assert.Equal(2, OutlineHoles.Of(f.Model.Get(slab)!).Count);
        Assert.Equal(80 - hole - Math.PI * 0.5, Q(f, slab), 6);
    }

    [Fact]
    public void Ifc_writes_the_hole_on_an_IfcEllipse_and_DXF_as_ELLIPSE()
    {
        var f = F();
        var (slab, _) = FloorWithEllipticalHole(f);
        var path = Path.Combine(_dir, "hole.ifc");
        IfcExport.Write(f.Session, path);
        var step = StepFile.Read(path);
        Assert.NotEmpty(step.OfType("IFCELLIPSE"));
        var doc = Lapin.Drawing.Import.DxfReader.Read(Views.Dxf(f.Session.Reader, Views.Find(f.Model, "plan:" + f.Storey)!));
        Assert.Contains(doc.Entities.OfType<Lapin.Drawing.Import.DxfEllipse>(), e => (e.C - new Vec2(5000, 4000)).Length < 1e-2);
        _ = slab;
    }

    [Fact]
    public void A_grip_edit_that_sends_the_holes_without_their_ellipses_keeps_them_moved_with_the_hole()
    {
        var f = F();
        var (slab, hole) = FloorWithEllipticalHole(f);
        var h = OutlineHoles.Of(f.Model.Get(slab)!)[0];
        // The hole dragged 1 m right, as the hole's middle grip saves it: corners and rises alone.
        var moved = new JsonArray(Pts(h.Corners.Select(p => p + new Vec2(1, 0))));
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, [OutlineHoles.Key] = moved, [OutlineHoles.ArcsKey] = new JsonArray(new JsonArray(0, 0, 0, 0)) });
        var after = Assert.Single(OutlineHoles.Of(f.Model.Get(slab)!));
        Assert.Equal(6, after.Ellipses!.First(x => x != null)!.C.X, 9);
        Assert.Equal(80 - hole, Q(f, slab), 9);
    }

    [Fact]
    public void An_outline_with_an_elliptical_edge_is_offset_as_a_whole_along_the_curve_at_that_distance()
    {
        // Once refused; a whole outline's offset now lays the curve at the distance from its ellipse in chords (OutlineOffsetTests).
        var f = F();
        var (pts, el) = EllipseEdge.Whole(new Vec2(0, 0), new Vec2(2, 0), 0.5);
        var r = (JsonObject)f.Session.Query("outline.offset", new JsonObject
        {
            ["points"] = Pts(pts), [EllipseEdge.Key] = new JsonArray([.. el.Select(x => (JsonNode?)x!.ToJson())]), ["distance"] = 0.1,
        })!;
        Assert.Null(r["problem"]);
        Assert.True(((JsonArray)r["points"]!).Count > 4);
        // Inward past its tightest curvature (b²/a = 0.5 m) it would fold on itself.
        var folded = (JsonObject)f.Session.Query("outline.offset", new JsonObject
        {
            ["points"] = Pts(pts), [EllipseEdge.Key] = new JsonArray([.. el.Select(x => (JsonNode?)x!.ToJson())]), ["distance"] = -0.6,
        })!;
        Assert.NotNull(folded["problem"]);
    }
}
