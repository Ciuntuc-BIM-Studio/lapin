using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Furniture;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// GitHub issue #114: Make drawing of a kitchen turned in plan drew its views in no one frame (the right view leaning, the iso
/// turned against the others, the depth 875 for a 600 kitchen). A shop drawing's views are taken in the element's own axes:
/// a piece of furniture's are its placement's (x along it, its front its −y, z up); a body without axes of its own stands
/// upright when that boxes it no larger than its principal axes do. With <c>axes: ucs</c> the views follow the project's UCS.
/// </summary>
public class ShopDrawingFrameTests
{
    static Fixture F() => new(new ArchitectureModule(), new FurnitureModule(), new DraftingModule());

    static string Kitchen(Fixture f, double rotation, double length = 3.0) =>
        (string)f.Run("furniture.place", new JsonObject
        {
            ["storeyId"] = f.Storey, ["kind"] = "kitchen", ["x"] = 5, ["y"] = 3, ["rotation"] = rotation, ["against"] = false, ["length"] = length,
        })!["id"]!;

    static void Near(Vec3 expected, Vec3 actual, double tol = 1e-9) =>
        Assert.True((expected - actual).Length < tol, $"expected {expected}, got {actual}");

    /// <summary>The body's box in the piece's own axes (turned back by its rotation about its point), worked out apart from the frame.</summary>
    static (Vec3 Min, Vec3 Max) OwnBox(Fixture f, string id, double rotation)
    {
        var e = f.Model.Get(id)!;
        var at = Args.PointOf(e.Attrs["at"]);
        var r = rotation * Math.PI / 180;
        var (x, y) = (new Vec3(Math.Cos(r), Math.Sin(r), 0), new Vec3(-Math.Sin(r), Math.Cos(r), 0));
        var pts = f.Session.Geometry(id)!.Parts.SelectMany(p => p.Solid.Vertices)
            .Select(p => { var d = p - new Vec3(at.X, at.Y, 0); return new Vec3(Vec3.Dot(d, x), Vec3.Dot(d, y), p.Z); }).ToList();
        return (new Vec3(pts.Min(p => p.X), pts.Min(p => p.Y), pts.Min(p => p.Z)), new Vec3(pts.Max(p => p.X), pts.Max(p => p.Y), pts.Max(p => p.Z)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(-123.5)]
    public void A_kitchens_views_are_taken_in_its_own_axes_whichever_way_it_is_turned(double rotation)
    {
        var f = F();
        var k = Kitchen(f, rotation);
        var e = f.Model.Get(k)!;
        var frame = ElementFrames.Of(f.Session.Reader, e)!;
        // Its frame is its placement's: x along the run, z up, its front (local −y) what the front view sees.
        var r = rotation * Math.PI / 180;
        Near(new Vec3(Math.Cos(r), Math.Sin(r), 0), frame.X);
        Near(Vec3.UnitZ, frame.Z);
        Near(new Vec3(-Math.Sin(r), Math.Cos(r), 0), frame.Y);
        // Its size is its own box's, not the world's nor a leaning one's.
        var (min, max) = OwnBox(f, k, rotation);
        Assert.Equal(max.X - min.X, frame.Length, 6);
        Assert.Equal(max.Y - min.Y, frame.Width, 6);
        Assert.Equal(max.Z - min.Z, frame.Height, 6);
        Assert.Equal(3.0, frame.Length, 3);
        var rows = ShopDrawings.DataRows(f.Session.Reader, e);
        Assert.Contains(rows, x => x.Label == "SIZE" && x.Value == $"{Numbers.Mm(frame.Length)} × {Numbers.Mm(frame.Width)} × {Numbers.Mm(frame.Height)}");

        var made = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = k })!;
        var views = (JsonObject)made["views"]!;
        foreach (var role in new[] { "front", "top", "right" })
        {
            var view = f.Model.Get((string)views[role]!)!;
            var parts = f.Session.Geometry(k)!.Parts.Select(p => new DrawPart(p.Solid, k, "IfcFurniture")).ToList();
            var (spec, problem) = ElementViews.Spec(f.Model, view, parts, f.Session.Reader.Services, false);
            Assert.Null(problem);
            var vf = spec!.Frame;
            // One orthonormal frame, the element's: every view's axes are the frame's axes.
            Assert.Equal(0, Vec3.Dot(vf.Right, vf.Up), 9);
            Assert.Equal(0, Vec3.Dot(vf.Right, vf.Dir), 9);
            Assert.Equal(0, Vec3.Dot(vf.Up, vf.Dir), 9);
            var (ar, au, ad) = ElementViews.Axes(role);
            Near(frame.Dir(ar), vf.Right);
            Near(frame.Dir(au), vf.Up);
            Near(frame.Dir(ad), vf.Dir);
            // Front and right stand upright: their up is the world's, so a box draws as a rectangle, not leaning.
            if (role != "top") Near(Vec3.UnitZ, vf.Up);
            else Near(-Vec3.UnitZ, vf.Dir);
            // The window is the box's outline in that view, plus the margin.
            var (w, h) = ElementViews.Size(frame, role);
            Assert.Equal(w + 2 * ElementViews.DefaultMargin, spec.Span!.Value.Item2 - spec.Span.Value.Item1, 6);
            Assert.Equal(h + 2 * ElementViews.DefaultMargin, spec.Rise!.Value.Item2 - spec.Rise.Value.Item1, 6);
        }
        // The iso in the same frame: from its south-west, its right level.
        var iso = f.Model.Get((string)views["iso"]!)!;
        var isoParts = f.Session.Geometry(k)!.Parts.Select(p => new DrawPart(p.Solid, k, "IfcFurniture")).ToList();
        var isf = ElementViews.IsoFrame(f.Model, f.Session.Reader.Services, iso, isoParts)!;
        Near(frame.Dir(new Vec3(1, -1, 0)).Normalized(), isf.Right);
        Assert.Equal(0, isf.Right.Z, 9);
        Near(frame.Dir(new Vec3(1, 1, -1)).Normalized(), isf.Dir);
        // The front's dimensions are the run's length and height.
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = (string)views["front"]! })!;
        var texts = ((JsonArray)d["pens"]!).OfType<JsonObject>().Where(p => p["texts"] is JsonArray).SelectMany(p => ((JsonArray)p["texts"]!).OfType<JsonObject>()).Select(t => (string)t["text"]!).ToList();
        Assert.Contains(Numbers.Mm(frame.Length), texts);
        Assert.Contains(Numbers.Mm(frame.Height), texts);
    }

    [Fact]
    public void Made_again_after_the_fix_the_drawing_keeps_its_ids_and_follows_a_turn()
    {
        var f = F();
        var k = Kitchen(f, 17);
        var first = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = k })!;
        f.Run("element.update", new JsonObject { ["id"] = k, ["attrs"] = new JsonObject { ["rotation"] = 40 } });
        var again = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = k })!;
        Assert.Equal((string)first["id"]!, (string)again["id"]!);
        foreach (var (key, id) in (JsonObject)first["views"]!) Assert.Equal((string)id!, (string)again["views"]![key]!);
        var frame = ElementFrames.Of(f.Session.Reader, f.Model.Get(k)!)!;
        Near(new Vec3(Math.Cos(40 * Math.PI / 180), Math.Sin(40 * Math.PI / 180), 0), frame.X);
    }

    /// <summary>A kitchen-like body with no axes of its own: base units at the front, wall units higher and further back, turned in plan.</summary>
    static List<Vec3> Stepped(double turnDegrees)
    {
        var r = turnDegrees * Math.PI / 180;
        var pts = new List<Vec3>();
        void Box(double x0, double x1, double y0, double y1, double z0, double z1)
        {
            foreach (var x in new[] { x0, x1 }) foreach (var y in new[] { y0, y1 }) foreach (var z in new[] { z0, z1 })
                pts.Add(new Vec3(2 + x * Math.Cos(r) - y * Math.Sin(r), 1 + x * Math.Sin(r) + y * Math.Cos(r), z));
        }
        Box(0, 3, 0, 0.6, 0, 0.9);
        Box(0, 3, 0.25, 0.6, 1.45, 2.17);
        Box(2.4, 3, 0, 0.6, 0, 2.17);
        return pts;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void A_body_of_no_axes_of_its_own_stands_upright_when_that_boxes_it_no_larger(double turn)
    {
        var (x, y, z) = ElementFrames.FromBody(Stepped(turn));
        Near(Vec3.UnitZ, z);
        var r = turn * Math.PI / 180;
        Assert.Equal(1, Math.Abs(Vec3.Dot(x, new Vec3(Math.Cos(r), Math.Sin(r), 0))), 9);
        Assert.Equal(0, y.Z, 9);
    }

    [Fact]
    public void A_body_that_truly_leans_keeps_its_principal_axes()
    {
        // A rafter 4 m long rising at 30°: its own axes lean with it, as before.
        var a = 30 * Math.PI / 180;
        var along = new Vec3(Math.Cos(a), 0, Math.Sin(a));
        var up = new Vec3(-Math.Sin(a), 0, Math.Cos(a));
        var pts = new List<Vec3>();
        foreach (var s in new[] { 0.0, 4.0 }) foreach (var w in new[] { 0.0, 0.08 }) foreach (var h in new[] { 0.0, 0.2 })
            pts.Add(along * s + new Vec3(0, w, 0) + up * h);
        var (x, _, z) = ElementFrames.FromBody(pts);
        Assert.Equal(1, Math.Abs(Vec3.Dot(x, along)), 6);
        Assert.Equal(1, Math.Abs(Vec3.Dot(z, up)), 6);
    }

    [Fact]
    public void With_axes_ucs_the_views_follow_the_projects_ucs()
    {
        var f = F();
        var k = Kitchen(f, 17);
        f.Run("project.location", new JsonObject { ["ucs"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["angle"] = 30 } });
        var made = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = k, ["axes"] = "ucs" })!;
        var view = f.Model.Get((string)made["views"]!["front"]!)!;
        var parts = f.Session.Geometry(k)!.Parts.Select(p => new DrawPart(p.Solid, k, "IfcFurniture")).ToList();
        var frame = ElementViews.FrameOf(f.Model, f.Session.Reader.Services, view, parts)!;
        Near(new Vec3(Math.Cos(30 * Math.PI / 180), Math.Sin(30 * Math.PI / 180), 0), frame.X);
        Near(Vec3.UnitZ, frame.Z);
        // Kept with the drawing: made again without saying, it stays in the UCS; element axes again when asked.
        var again = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = k })!;
        Assert.Equal((string)made["views"]!["front"]!, (string)again["views"]!["front"]!);
        frame = ElementViews.FrameOf(f.Model, f.Session.Reader.Services, f.Model.Get((string)again["views"]!["front"]!)!, parts)!;
        Near(new Vec3(Math.Cos(30 * Math.PI / 180), Math.Sin(30 * Math.PI / 180), 0), frame.X);
        f.Run("drawing.create", new JsonObject { ["element"] = k, ["axes"] = "element" });
        frame = ElementViews.FrameOf(f.Model, f.Session.Reader.Services, f.Model.Get((string)again["views"]!["front"]!)!, parts)!;
        Near(new Vec3(Math.Cos(17 * Math.PI / 180), Math.Sin(17 * Math.PI / 180), 0), frame.X);
    }
}
