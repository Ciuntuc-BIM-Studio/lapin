using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing.Views;
using Lapin.Furniture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum 102: a dimension drawn to a stair's points stayed where it was when the stair moved. A dimension's point now holds
/// on what a plan does not cut as it holds on a cut edge: a stair's outline (drawn by its marks), a beam seen overhead, a
/// floor's, a piece of furniture's, a railing's or a Studio solid's lines, kept in the element's frame (its placement and
/// rotation in degrees, its outline's first side, its path's first piece), so it follows a move, a turn and an edit; a
/// column's point (held on its cut) turns with it too, and a railing itself moves; ends an older Lapin kept on a turned
/// element are brought into today's frame as the project opens.
/// </summary>
public class DimensionFollowTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StructureModule(), new FurnitureModule(), new StudioModule());
    static string Plan(Fixture f) => "plan:" + f.Storey;
    static ViewDrawing Drawing(Fixture f) => Views.Generate(f.Session.Reader, Views.Find(f.Model, Plan(f))!).Drawing!;
    static void Near(Vec2 want, Vec2 got, double tol = 1e-6) => Assert.True((want - got).Length < tol, $"want {want}, got {got}");

    /// <summary>A dimension from a free point to <paramref name="p"/>; returns its id.</summary>
    static string DimTo(Fixture f, Vec2 p) =>
        (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = Plan(f), ["pts"] = new JsonArray(new JsonArray(p.X - 5, p.Y - 5), new JsonArray(p.X, p.Y)), ["at"] = new JsonArray(p.X - 6, p.Y - 6) })!["id"]!;

    static JsonObject Info(Fixture f, string dim) => (JsonObject)f.Session.Query("drafting.dimension", new JsonObject { ["id"] = dim })!;
    static Vec2 End(Fixture f, string dim) => Args.PointOf(Info(f, dim)["points"]![1]);
    static string? Holds(Fixture f, string dim) => (string?)Info(f, dim)["holds"]![1];

    static Vec2 Turned(Vec2 p, Vec2 c, double deg)
    {
        var a = deg * Math.PI / 180;
        var d = p - c;
        return c + new Vec2(d.X * Math.Cos(a) - d.Y * Math.Sin(a), d.X * Math.Sin(a) + d.Y * Math.Cos(a));
    }

    static string Stair(Fixture f) => (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0, ["width"] = 1.2 })!["id"]!;

    [Fact]
    public void A_dimension_to_a_stair_corner_follows_the_stair_moved_and_turned_and_one_undo_puts_it_back()
    {
        var f = F();
        var stair = Stair(f);
        // The stair's outline is in the plan's drawing for the dimensions (its marks draw it), not drawn twice.
        var outline = Drawing(f).Outlines!.Where(l => l.Element == stair).ToList();
        Assert.NotEmpty(outline);
        Assert.DoesNotContain(Drawing(f).Lines, l => l.Element == stair);
        // The corner at the foot, on its right: (0, -0.6).
        var corner = outline.SelectMany(l => new[] { l.A, l.B }).OrderBy(p => p.X).ThenBy(p => p.Y).First();
        Near(new Vec2(0, -0.6), corner);
        var dim = DimTo(f, corner);
        Assert.Equal("edge", Holds(f, dim));

        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(stair), ["op"] = "move", ["by"] = new JsonArray(2, 1) });
        Near(corner + new Vec2(2, 1), End(f, dim));
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(stair), ["op"] = "rotate", ["center"] = new JsonArray(2, 1), ["angle"] = 90 });
        Near(Turned(corner + new Vec2(2, 1), new Vec2(2, 1), 90), End(f, dim));
        // Turned again by its own rotation (the inspector), about its foot.
        f.Run("architecture.updateStair", new JsonObject { ["id"] = stair, ["rotation"] = 180 });
        Near(Turned(corner, default, 180) + new Vec2(2, 1), End(f, dim));
        // Its foot moved by the inspector.
        f.Run("architecture.updateStair", new JsonObject { ["id"] = stair, ["x"] = 0, ["y"] = 0 });
        Near(Turned(corner, default, 180), End(f, dim));
        // The free end never moved.
        Near(corner - new Vec2(5, 5), Args.PointOf(Info(f, dim)["points"]![0]));

        f.Session.Undo();
        f.Session.Undo();
        f.Session.Undo();
        Near(corner + new Vec2(2, 1), End(f, dim));
        f.Session.Undo();
        Near(corner, End(f, dim));
    }

    [Fact]
    public void A_dimension_to_a_stair_s_side_follows_it_made_wider()
    {
        var f = F();
        var stair = Stair(f);
        var outline = Drawing(f).Outlines!.Where(l => l.Element == stair).ToList();
        var length = outline.Max(l => Math.Max(l.A.X, l.B.X));
        // A point halfway along its right side, measured across the stair from a point beside it.
        var side = new Vec2(length / 2, -0.6);
        var dim = (string)f.Run("drafting.addDimension", new JsonObject { ["viewId"] = Plan(f), ["pts"] = new JsonArray(new JsonArray(side.X, -3), new JsonArray(side.X, side.Y)), ["at"] = new JsonArray(side.X + 1, 0), ["dir"] = new JsonArray(0, 1) })!["id"]!;
        Assert.Equal("edge", Holds(f, dim));
        Assert.Equal(2.4, Info(f, dim)["values"]![0]!.GetValue<double>(), 6);
        // Wider by 40 cm: its side goes out 20 cm (the foot stays on its centre), and the point with it.
        f.Run("architecture.updateStair", new JsonObject { ["id"] = stair, ["width"] = 1.6 });
        Near(new Vec2(side.X, -0.8), End(f, dim));
        Assert.Equal(2.2, Info(f, dim)["values"]![0]!.GetValue<double>(), 6);
    }

    [Fact]
    public void Dimensions_follow_columns_beams_floors_furniture_railings_and_studio_solids_moved_and_turned()
    {
        var f = F();
        var made = new Dictionary<string, string>
        {
            ["column"] = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 10, ["y"] = 0 })!["id"]!,
            ["beam"] = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 20, ["y1"] = 0, ["x2"] = 24, ["y2"] = 0 })!["id"]!,
            ["floor"] = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(30, 0), new JsonArray(34, 0), new JsonArray(34, 3), new JsonArray(30, 3)) })!["id"]!,
            ["furniture"] = (string)f.Run("furniture.place", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 40, ["y"] = 0, ["against"] = false })!["id"]!,
            ["railing"] = (string)f.Run("architecture.addRailing", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(50, 0), new JsonArray(54, 0), new JsonArray(54, 2)) })!["id"]!,
            ["studio"] = (string)f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = new JsonArray(60, 0), ["width"] = 2, ["length"] = 3, ["height"] = 1 })!["id"]!,
        };
        var centres = new Dictionary<string, Vec2>();
        foreach (var (name, id) in made)
        {
            var d = Drawing(f);
            var corner = d.Cuts.FirstOrDefault(c => c.Element == id) is { } cut ? cut.Loops[0][0] : d.Lines.First(l => l.Element == id).A;
            var dim = DimTo(f, corner);
            Assert.True(Holds(f, dim) == "edge", $"{name}: its point holds on it");
            f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "move", ["by"] = new JsonArray(1, 2) });
            var c = corner + new Vec2(1, 2);
            Assert.True((End(f, dim) - c).Length < 1e-6, $"{name} moved: want {c}, got {End(f, dim)}");
            f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "rotate", ["center"] = new JsonArray(c.X + 1, c.Y), ["angle"] = 90 });
            centres[name] = new Vec2(c.X + 1, c.Y);
            var want = Turned(c, centres[name], 90);
            Assert.True((End(f, dim) - want).Length < 1e-6, $"{name} turned: want {want}, got {End(f, dim)}");
            Assert.False(Info(f, dim)["orphan"]!.GetValue<bool>(), $"{name}: not lost");
        }
        // The railing itself moved and turned with its line.
        var rail = Railings_Points(f, made["railing"]);
        Near(Turned(new Vec2(51, 2), centres["railing"], 90), rail[0]);
    }

    static List<Vec2> Railings_Points(Fixture f, string id) => Lapin.Architecture.Stairs.Railings.Points(f.Model.Get(id)!);

    [Fact]
    public void An_end_an_older_Lapin_kept_on_a_turned_column_is_brought_into_today_s_frame_where_it_is()
    {
        var f = F();
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0, ["rotation"] = 30 })!["id"]!;
        var dim = DimTo(f, Drawing(f).Cuts.First(c => c.Element == column).Loops[0][0]);
        var now = End(f, dim);
        var e = f.Model.Get(dim)!;
        var r = Dimensions.Stations(e)[1].Ref!;
        Assert.Equal(DimEndRef.CurrentFrame, r.FrameRev);
        // As an older Lapin kept it: in the frame that read 30 as radians.
        var col = f.Model.Get(column)!;
        var (o, u) = Dimensions.Frame(col, 0)!.Value;
        var v = now - o;
        var old = r with { Local = new Vec2(Vec2.Dot(v, u), Vec2.Dot(v, u.Perp)), LocalDir = new Vec2(Vec2.Dot(r.Dir, u), Vec2.Dot(r.Dir, u.Perp)), FrameRev = 0 };
        Assert.False(old.ToJson().ContainsKey("frame"));
        var attrs = e.CloneAttrs();
        Dimensions.Write(attrs, [Dimensions.Stations(e)[0], (now, old)]);
        var legacy = e.With(attrs: attrs);
        var (up, _) = new DimensionFrameUpgrade().Upgrade([.. f.Model.Entities.Select(x => x.Id == dim ? legacy : x)], [.. f.Model.Relations]);
        var fixedRef = Dimensions.Stations(up.Single(x => x.Id == dim))[1].Ref!;
        Assert.Equal(DimEndRef.CurrentFrame, fixedRef.FrameRev);
        Near(r.Local!.Value, fixedRef.Local!.Value);
        Near(r.LocalDir!.Value, fixedRef.LocalDir!.Value);
        // Upgraded once: nothing more to change.
        var (again, _) = new DimensionFrameUpgrade().Upgrade(up, [.. f.Model.Relations]);
        Assert.Same(up, again);
    }
}
