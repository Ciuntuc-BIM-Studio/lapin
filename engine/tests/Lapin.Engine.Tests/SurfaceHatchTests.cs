using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Surface hatches in elevations and sections (decision 325): the faces a drawing sees hatched with their material's
/// elevation graphics where nothing nearer hides them; a brick facing hatched round its window and not inside it, a face
/// behind another left bare, one material going on in one plane across two walls hatched as one, the view style's switch,
/// and a section hatching what it sees beyond its cut (its cut areas hiding what lies behind them).
/// </summary>
public class SurfaceHatchTests
{
    /// <summary>A wall type with a brick facing on both sides of a concrete core; the brick hatched BRICK in elevations.</summary>
    static string BrickType(Fixture f, string? align = null)
    {
        var type = f.Type("Brick 40", ("Facing brick", 0.1, false), ("Concrete", 0.2, true), ("Facing brick", 0.1, false));
        var brick = f.Model.OfClass(IfcClass.Material).First(e => e.Name == "Facing brick").Id;
        var g = new JsonObject { ["hatch"] = "BRICK" };
        if (align != null) g["hatchAlign"] = align;
        f.Run("material.update", new JsonObject { ["id"] = brick, ["elevation"] = g });
        return type;
    }

    static string Elevation(Fixture f, string direction = "south") =>
        (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = direction })!["id"]!;

    static Views.Rendered Render(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);

    static List<(Vec2 A, Vec2 B)> Hatch(Views.Rendered r) => r.Pens.Where(p => p.Layer == SurfaceHatches.Layer).SelectMany(p => p.Segments).ToList();

    static Vec2 Mid((Vec2 A, Vec2 B) s) => (s.A + s.B) * 0.5;

    /// <summary>An element's box as the view sees it.</summary>
    static (Vec2 Min, Vec2 Max) Seen(Fixture f, string id, ViewFrame frame)
    {
        var (lo, hi) = f.Bounds(id);
        var pts = Enumerable.Range(0, 8).Select(i => frame.Map(new Vec3((i & 1) == 0 ? lo.X : hi.X, (i & 2) == 0 ? lo.Y : hi.Y, (i & 4) == 0 ? lo.Z : hi.Z))).ToList();
        return (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y)));
    }

    static bool Inside(Vec2 p, (Vec2 Min, Vec2 Max) b, double inset = 0.01) =>
        p.X > b.Min.X + inset && p.X < b.Max.X - inset && p.Y > b.Min.Y + inset && p.Y < b.Max.Y - inset;

    [Fact]
    public void A_brick_facing_is_hatched_in_elevation_round_its_window_and_not_inside_it()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 8, 0, BrickType(f));
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4 })!["id"]!;
        // The south elevation looks north and sees the wall's south face.
        var view = Elevation(f, "south");
        var r = Render(f, view);
        var hatch = Hatch(r);
        Assert.NotEmpty(hatch);
        var face = Seen(f, wall, r.Frame!);
        var opening = Seen(f, window, r.Frame!);
        // Every stroke on the wall's face, none in the window.
        Assert.All(hatch, s => Assert.True(Inside(Mid(s), face, -1e-6), $"{Mid(s)} is off the wall"));
        // (The window's box takes in its sill and lining, a few centimetres past the opening.)
        Assert.DoesNotContain(hatch, s => Inside(Mid(s), opening, 0.1));
        // The courses run the wall's length, on both sides of the window.
        Assert.Contains(hatch, s => Mid(s).X < opening.Min.X - 0.2);
        Assert.Contains(hatch, s => Mid(s).X > opening.Max.X + 0.2);
        // Under the lines: the hatch's pen before the first seen line's.
        var pens = r.Pens.Select(p => p.Layer).ToList();
        Assert.True(pens.IndexOf(SurfaceHatches.Layer) < pens.IndexOf(PenLayer.Seen));
        // The courses of BRICK at 1:100 are 0.4 m apart: horizontal strokes at multiples of 0.4 from the view's origin.
        var levels = hatch.Where(s => Math.Abs(s.A.Y - s.B.Y) < 1e-9).Select(s => Math.Round(s.A.Y, 3)).Distinct().Order().ToList();
        Assert.True(levels.Count >= 5);
        Assert.All(levels, y => Assert.Equal(0, Math.Abs(Math.IEEERemainder(y, 0.4)), 3));
    }

    [Fact]
    public void A_face_behind_a_nearer_one_is_not_hatched()
    {
        var f = new Fixture();
        f.Wall(0, 0, 10, 0, BrickType(f));
        // A concrete wall in front of it (to the south), with no elevation hatch of its own.
        var plain = f.Type("Concrete 20", ("Concrete", 0.2, true));
        var front = f.Wall(3, -2, 6, -2, plain);
        var r = Render(f, Elevation(f, "south"));
        var hatch = Hatch(r);
        Assert.NotEmpty(hatch);
        var near = Seen(f, front, r.Frame!);
        Assert.DoesNotContain(hatch, s => Inside(Mid(s), near));
        Assert.Contains(hatch, s => Mid(s).X < near.Min.X - 0.2 && Mid(s).Y > near.Min.Y + 0.2 && Mid(s).Y < near.Max.Y - 0.2);
    }

    /// <summary>The distinct x of the vertical strokes and y of the horizontal ones (mm).</summary>
    static (HashSet<double> Xs, HashSet<double> Ys) Grid(List<(Vec2 A, Vec2 B)> hatch) => (
        hatch.Where(s => Math.Abs(s.A.X - s.B.X) < 1e-9).Select(s => Math.Round(s.A.X, 3)).ToHashSet(),
        hatch.Where(s => Math.Abs(s.A.Y - s.B.Y) < 1e-9).Select(s => Math.Round(s.A.Y, 3)).ToHashSet());

    [Fact]
    public void One_material_going_on_in_one_plane_across_two_walls_is_hatched_as_one()
    {
        // Aligned to the layer, each face's hatch starts at its surface's corner: two walls in one line share theirs.
        var one = new Fixture();
        one.Wall(0, 0, 9.7, 0, BrickType(one, "layer"));
        var whole = Grid(Hatch(Render(one, Elevation(one))));
        var two = new Fixture();
        var type = BrickType(two, "layer");
        two.Wall(0, 0, 4.3, 0, type);
        two.Wall(4.3, 0, 9.7, 0, type);
        var view = Elevation(two);
        var split = Grid(Hatch(Render(two, view)));
        Assert.NotEmpty(whole.Xs);
        Assert.Equal(whole.Ys.Order(), split.Ys.Order());
        Assert.Equal(whole.Xs.Order(), split.Xs.Order());
        // Without the style's joins each wall's hatch starts on its own: the second wall's joints move.
        two.Run("view.setStyle", new JsonObject { ["id"] = view, ["joins"] = false });
        var apart = Grid(Hatch(Render(two, view)));
        Assert.NotEqual(whole.Xs.Order(), apart.Xs.Order());
    }

    [Fact]
    public void The_view_style_s_switch_turns_them_off_and_structural_views_draw_none()
    {
        var f = new Fixture();
        f.Wall(0, 0, 8, 0, BrickType(f));
        var view = Elevation(f);
        Assert.NotEmpty(Hatch(Render(f, view)));
        Assert.True(ViewStyles.Of(f.Model, Views.Find(f.Model, view)!).SurfaceHatch);
        f.Run("view.setStyle", new JsonObject { ["id"] = view, ["surfaceHatch"] = false });
        Assert.Empty(Hatch(Render(f, view)));
        f.Run("view.setStyle", new JsonObject { ["id"] = view, ["surfaceHatch"] = null });
        Assert.NotEmpty(Hatch(Render(f, view)));
        // A structural elevation: off by its style.
        var structural = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south", ["discipline"] = "Structure" })!["id"]!;
        Assert.False(ViewStyles.Of(f.Model, Views.Find(f.Model, structural)!).SurfaceHatch);
        Assert.Empty(Hatch(Render(f, structural)));
        // A material without elevation graphics hatches nothing: the setting is an opt-in.
        var g = new Fixture();
        g.Wall(0, 0, 8, 0, g.Type("Brick 30", ("Brick", 0.3, true)));
        Assert.Empty(Hatch(Render(g, Elevation(g))));
        // A plan never.
        Assert.DoesNotContain(Views.Render(f.Session.Reader, Views.Find(f.Model, "plan:" + f.Storey)!).Pens, p => p.Layer == SurfaceHatches.Layer);
    }

    [Fact]
    public void A_section_hatches_what_it_sees_beyond_its_cut_and_not_behind_its_cut_areas()
    {
        var f = new Fixture();
        var brick = BrickType(f);
        var plain = f.Type("Concrete 30", ("Concrete", 0.3, true));
        // The brick wall beyond the cut; a concrete wall the section cuts across, running back to it; a brick wall before the cut.
        f.Wall(0, 5, 10, 5, brick);
        f.Wall(5, -1, 5, 4.8, plain);
        f.Wall(20, -2, 25, -2, brick);
        // Along y = 1 from west to east, looking north (to its left).
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, 1), ["end"] = new JsonArray(30, 1) })!["id"]!;
        var r = Render(f, view);
        var hatch = Hatch(r);
        Assert.NotEmpty(hatch);
        // The wall before the cut is not seen (it is behind the viewer).
        Assert.DoesNotContain(hatch, s => Mid(s).X > 18);
        // The concrete wall's cut area hides the brick behind it.
        var cut = r.Pens.Where(p => p.Layer == PenLayer.Cut).SelectMany(p => p.Segments).SelectMany(s => new[] { s.A, s.B }).ToList();
        var x0 = cut.Min(p => p.X); var x1 = cut.Max(p => p.X);
        Assert.InRange(x1 - x0, 0.29, 0.31);
        Assert.DoesNotContain(hatch, s => Mid(s).X > x0 + 0.01 && Mid(s).X < x1 - 0.01 && Mid(s).Y > 0.05 && Mid(s).Y < 2.5);
        Assert.Contains(hatch, s => Mid(s).X < x0 - 0.2);
        Assert.Contains(hatch, s => Mid(s).X > x1 + 0.2);
    }
}
