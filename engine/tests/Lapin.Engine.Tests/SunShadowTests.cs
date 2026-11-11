using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Vector sun shadows in a drawing (docs/plan-visualisation.md): a balcony's shadow on the wall behind it under the
/// drawing convention (the sun over the left shoulder, rays along the cube's diagonal: a shadow as deep as the
/// balcony stands out, down and to the right), the part the balcony hides in the view left out, a face turned from
/// the sun in shade whole, and the cut of a section.
/// </summary>
public class SunShadowTests
{
    /// <summary>An elevation looking north from 10 m south of the origin.</summary>
    static readonly ViewFrame North = new(new Vec3(0, -10, 0), new Vec3(1, 0, 0), new Vec3(0, 0, 1), new Vec3(0, 1, 0));

    static Solid Box(double x0, double y0, double z0, double x1, double y1, double z1) =>
        Prism.Box(new Vec3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0)!;

    static double Area(IEnumerable<SunShadows.Area> areas) => areas.Sum(a =>
    {
        var s = 0.0;
        for (var i = 0; i < a.Outline.Length; i++) { var p = a.Outline[i]; var q = a.Outline[(i + 1) % a.Outline.Length]; s += p.X * q.Y - q.X * p.Y; }
        return Math.Abs(s) / 2;
    });

    static bool Covers(IEnumerable<SunShadows.Area> areas, Vec2 at) => areas.Any(a =>
    {
        var inside = false;
        for (int i = 0, j = a.Outline.Length - 1; i < a.Outline.Length; j = i++)
        {
            Vec2 p = a.Outline[i], q = a.Outline[j];
            if (p.Y > at.Y != q.Y > at.Y && at.X < (q.X - p.X) * (at.Y - p.Y) / (q.Y - p.Y) + p.X) inside = !inside;
        }
        return inside;
    });

    static bool Inside(IReadOnlyList<Vec2> loop, Vec2 at)
    {
        var inside = false;
        for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
        {
            Vec2 p = loop[i], q = loop[j];
            if (p.Y > at.Y != q.Y > at.Y && at.X < (q.X - p.X) * (at.Y - p.Y) / (q.Y - p.Y) + p.X) inside = !inside;
        }
        return inside;
    }

    /// <summary>How many of the outlines hold the point: a drawing fills them all even-odd (every renderer does), so an even count is left unpainted.</summary>
    static int Cover(IEnumerable<IReadOnlyList<Vec2>> loops, Vec2 at) => loops.Count(l => Inside(l, at));

    /// <summary>
    /// The outlines as a drawing paints them, sampled on a grid (offset so no sample lies on an edge): the cells painted
    /// (an odd count), the deepest overlap met (the most outlines over one sample) and the painted samples, for comparing two drawings.
    /// </summary>
    static (HashSet<(int, int)> Painted, int Deepest) Paint(IEnumerable<IReadOnlyList<Vec2>> outlines, double step = 0.05)
    {
        var loops = outlines.Where(l => l.Count >= 3).ToList();
        var painted = new HashSet<(int, int)>();
        var deepest = 0;
        if (loops.Count == 0) return (painted, 0);
        var boxes = loops.Select(l => (X0: l.Min(p => p.X), X1: l.Max(p => p.X), Y0: l.Min(p => p.Y), Y1: l.Max(p => p.Y))).ToList();
        for (var ix = (int)Math.Floor(boxes.Min(b => b.X0) / step); ix <= (int)Math.Ceiling(boxes.Max(b => b.X1) / step); ix++)
            for (var iy = (int)Math.Floor(boxes.Min(b => b.Y0) / step); iy <= (int)Math.Ceiling(boxes.Max(b => b.Y1) / step); iy++)
            {
                var at = new Vec2((ix + 0.5) * step + 0.0137 * step, (iy + 0.5) * step + 0.0291 * step);
                var n = 0;
                for (var k = 0; k < loops.Count; k++)
                    if (at.X > boxes[k].X0 && at.X < boxes[k].X1 && at.Y > boxes[k].Y0 && at.Y < boxes[k].Y1 && Inside(loops[k], at)) n++;
                deepest = Math.Max(deepest, n);
                if (n % 2 == 1) painted.Add((ix, iy));
            }
        return (painted, deepest);
    }

    static IEnumerable<IReadOnlyList<Vec2>> Outlines(IEnumerable<SunShadows.Area> areas) => areas.Select(a => (IReadOnlyList<Vec2>)a.Outline);

    // The convention's sun and a balcony slab standing 1.2 m out of a wall's face, 3 to 8 m along it. Its shadow on the wall
    // lies between x 3 and 9.2, down to z 0.8, less what the slab hides in the view.
    static readonly Vec3 Sun = new Vec3(-1, -1, 1).Normalized();
    static Solid Balcony() => Box(3, -1.2, 2.0, 8, 0, 2.2);

    [Fact]
    public void Two_flush_walls_that_overlap_paint_the_shadow_where_they_cross_as_one_wall_would()
    {
        // Issue #14: two coplanar faces that overlap (4 to 6 m) each receive the same shadow there, and the drawing fills the
        // shadow areas even-odd, so the overlap cancelled out and stayed unpainted.
        var one = SunShadows.Draw([(Box(0, 0, 0, 10, 0.3, 3), 0), (Balcony(), 2)], North, Sun);
        var lapped = SunShadows.Draw([(Box(0, 0, 0, 6, 0.3, 3), 0), (Box(4, 0, 0, 10, 0.3, 3), 1), (Balcony(), 2)], North, Sun);
        Assert.True(Cover(Outlines(lapped), new Vec2(5, 1.5)) % 2 == 1, "the shadow where the two walls overlap is painted");
        Assert.True(Cover(Outlines(lapped), new Vec2(3.9, 1.9)) % 2 == 1);
        Assert.True(Cover(Outlines(lapped), new Vec2(8, 1.5)) % 2 == 1);
        // Each area stays with an element whose face it lies on: the overlap with the first wall, the rest with the wall that has it alone.
        Assert.All(lapped.Where(a => Cover([a.Outline], new Vec2(5, 1.5)) == 1), a => Assert.Equal(0, a.Owner));
        Assert.All(lapped.Where(a => Cover([a.Outline], new Vec2(8, 1.5)) == 1), a => Assert.Equal(1, a.Owner));
        Assert.All(lapped.Where(a => Cover([a.Outline], new Vec2(3.9, 1.9)) == 1), a => Assert.Equal(0, a.Owner));
        var (paintOne, _) = Paint(Outlines(one));
        var (paintLapped, deepest) = Paint(Outlines(lapped));
        Assert.Equal(1, deepest);
        Assert.Equal(paintOne, paintLapped);
    }

    [Fact]
    public void Two_walls_with_a_gap_between_them_leave_the_gap_unpainted()
    {
        var sep = SunShadows.Draw([(Box(0, 0, 0, 5, 0.3, 3), 0), (Box(6, 0, 0, 10, 0.3, 3), 1), (Balcony(), 2)], North, Sun);
        var o = Outlines(sep).ToList();
        Assert.True(Cover(o, new Vec2(4.5, 1.5)) % 2 == 1);
        Assert.True(Cover(o, new Vec2(6.5, 1.5)) % 2 == 1);
        Assert.Equal(0, Cover(o, new Vec2(5.5, 1.5)));
        Assert.Equal(1, Paint(o).Deepest);
    }

    [Fact]
    public void A_shadow_nested_in_another_on_the_same_wall_adds_nothing_and_cancels_nothing()
    {
        var wall = Box(0, 0, 0, 10, 0.3, 3);
        // A small block in front of the wall whose shadow falls inside the balcony's: 5 to 5.5 m along, 1.6 to 1.9 m up, 0.2 to 0.5 m out.
        var block = Box(5, -0.5, 1.6, 5.5, -0.2, 1.9);
        var withBlock = SunShadows.Draw([(wall, 0), (Balcony(), 2), (block, 3)], North, Sun);
        var without = SunShadows.Draw([(wall, 0), (Balcony(), 2)], North, Sun);
        Assert.Equal(1, Paint(Outlines(withBlock.Where(a => a.Owner == 0))).Deepest);
        // The wall's shadow is the balcony's alone, less the block's own footprint in the view (it hides the wall there).
        var before = Paint(Outlines(without.Where(a => a.Owner == 0))).Painted;
        var after = Paint(Outlines(withBlock.Where(a => a.Owner == 0))).Painted;
        before.ExceptWith(before.Where(c => c.Item1 * 0.05 is >= 4.95 and <= 5.55 && c.Item2 * 0.05 is >= 1.55 and <= 1.95).ToList());
        after.ExceptWith(after.Where(c => c.Item1 * 0.05 is >= 4.95 and <= 5.55 && c.Item2 * 0.05 is >= 1.55 and <= 1.95).ToList());
        Assert.Equal(before, after);
    }

    [Fact]
    public void A_hole_in_a_shadow_stays_a_hole_when_the_walls_that_receive_it_overlap()
    {
        // A frame standing 1.0 to 1.2 m out with a window in it: its shadow on the wall has a hole where the sun comes through.
        Solid[] frame = [Box(4, -1.2, 1.4, 8, -1.0, 1.6), Box(4, -1.2, 2.0, 8, -1.0, 2.2), Box(4, -1.2, 1.6, 5, -1.0, 2.0), Box(7, -1.2, 1.6, 8, -1.0, 2.0)];
        var lapped = SunShadows.Draw([(Box(0, 0, 0, 7, 0.3, 3), 0), (Box(5, 0, 0, 10, 0.3, 3), 1), .. frame.Select((s, i) => (s, 2 + i))], North, Sun);
        var one = SunShadows.Draw([(Box(0, 0, 0, 10, 0.3, 3), 0), .. frame.Select((s, i) => (s, 2 + i))], North, Sun);
        var o = Outlines(lapped).ToList();
        // The light through the window: the window (5 to 7 m, 1.6 to 2.0 m) carried 1.0 to 1.2 m right and down lies between x 6.2 and 8.0, z 0.6 and 0.8.
        Assert.Equal(0, Cover(o, new Vec2(7.1, 0.7)));
        // The frame's shadow around it, in the walls' overlap (5 to 7 m) and outside it.
        Assert.True(Cover(o, new Vec2(5.8, 0.7)) % 2 == 1);
        Assert.True(Cover(o, new Vec2(6.0, 1.0)) % 2 == 1);
        var (paintOne, _) = Paint(Outlines(one.Where(a => a.Owner <= 1)));
        var (paintLapped, deepest) = Paint(Outlines(lapped.Where(a => a.Owner <= 1)));
        Assert.Equal(1, deepest);
        Assert.Equal(paintOne, paintLapped);
    }

    [Fact]
    public void A_face_in_shade_whole_is_painted_once_where_flush_faces_overlap()
    {
        // The sun behind the walls: both faces are in shade whole, and their overlap was painted twice, so not at all.
        var behind = new Vec3(0, 1, 1).Normalized();
        var lapped = SunShadows.Draw([(Box(0, 0, 0, 6, 0.3, 3), 0), (Box(4, 0, 0, 10, 0.3, 3), 1)], North, behind);
        Assert.Equal(1, Cover(Outlines(lapped), new Vec2(5, 1.5)));
        var (painted, deepest) = Paint(Outlines(lapped));
        Assert.Equal(1, deepest);
        Assert.Equal(Paint(Outlines(SunShadows.Draw([(Box(0, 0, 0, 10, 0.3, 3), 0)], North, behind))).Painted, painted);
    }

    [Fact]
    public void An_elevation_of_two_overlapping_walls_paints_the_balcony_s_shadow_once_where_they_cross()
    {
        double Run(Action<Fixture> walls, out int deepest)
        {
            var f = new Fixture();
            walls(f);
            var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
            f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = upper, ["points"] = new JsonArray(new JsonArray(3, -1.2), new JsonArray(8, -1.2), new JsonArray(8, 0), new JsonArray(3, 0)) });
            var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
            f.Run("view.setShadows", new JsonObject { ["id"] = south, ["mode"] = "view" });
            var pen = Views.Render(f.Session.Reader, Views.Find(f.Model, south)!).Pens.First(p => p.Layer == ViewShadows.Layer);
            var (painted, d) = Paint(pen.Loops);
            deepest = d;
            return painted.Count * 0.05 * 0.05;
        }
        var one = Run(f => f.Wall(-2, 0, 10, 0), out _);
        var lapped = Run(f => { f.Wall(-2, 0, 6, 0); f.Wall(4, 0, 10, 0); }, out var deepest);
        Assert.True(one > 1);
        // Painted as much as one continuous wall would be, and no outline over another.
        Assert.Equal(1, deepest);
        Assert.Equal(one, lapped, 1);
    }

    [Fact]
    public void A_balcony_casts_on_its_wall_as_deep_as_it_stands_out_less_what_it_hides()
    {
        var wall = Box(0, 0, 0, 10, 0.3, 3);
        var balcony = Box(3, -1.2, 2.0, 6, 0, 2.2);
        // The convention: towards the sun is left, towards the viewer and up, along the cube's diagonal.
        var sun = new Vec3(-1, -1, 1).Normalized();
        var areas = SunShadows.Draw([(wall, 0), (balcony, 1)], North, sun);
        Assert.All(areas, a => Assert.Equal(0, a.Owner));
        // The slab's outline swept 1.2 m right and 1.2 m down: 0.6 + 1.2 × 0.2 + 1.2 × 3 = 4.44 m², less the 0.6 m² the balcony's front hides.
        Assert.Equal(3.84, Area(areas), 3);
        Assert.Equal(0.8, areas.SelectMany(a => a.Outline).Min(p => p.Y), 6);
        Assert.Equal(7.2, areas.SelectMany(a => a.Outline).Max(p => p.X), 6);
        Assert.True(Covers(areas, new Vec2(6.5, 1.5)));
        Assert.False(Covers(areas, new Vec2(4.5, 2.1)));
        Assert.False(Covers(areas, new Vec2(1, 1)));
    }

    [Fact]
    public void A_face_turned_from_the_sun_is_in_shade_whole()
    {
        var wall = Box(0, 0, 0, 10, 0.3, 3);
        var balcony = Box(3, -1.2, 2.0, 6, 0, 2.2);
        // The sun behind the wall: everything the elevation sees is in shade.
        var areas = SunShadows.Draw([(wall, 0), (balcony, 1)], North, new Vec3(0, 1, 1).Normalized());
        Assert.Equal(30, Area(areas), 3);
        Assert.Equal(0.6, Area(areas.Where(a => a.Owner == 1)), 3);
    }

    [Fact]
    public void A_section_draws_and_casts_only_what_lies_beyond_its_cut()
    {
        var wall = Box(0, 0, 0, 10, 0.3, 3);
        var balcony = Box(3, -1.2, 2.0, 6, 0, 2.2);
        var sun = new Vec3(-1, -1, 1).Normalized();
        // Cut at y = −0.5 looking north: the balcony's front 0.7 m is taken away, so its shadow is 0.5 m deep and its cut hides the wall behind.
        var cut = North with { Origin = new Vec3(0, -0.5, 0) };
        var areas = SunShadows.Draw([(wall, 0), (balcony, 1)], cut, sun, cut: true);
        // 0.6 + 0.5 × 0.2 + 0.5 × 3 = 2.2, less the 0.6 behind the balcony's cut face.
        Assert.Equal(1.6, Area(areas), 3);
        Assert.Equal(1.5, areas.SelectMany(a => a.Outline).Min(p => p.Y), 6);
    }

    [Fact]
    public void A_south_elevation_draws_the_shadow_of_a_balcony_by_the_view_s_sun_or_the_project_s()
    {
        var f = new Fixture();
        // A wall along y = 0, and on a storey at 3 m a slab standing 1.2 m out to the south: a balcony.
        f.Wall(-2, 0, 10, 0);
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = upper, ["points"] = new JsonArray(new JsonArray(3, -1.2), new JsonArray(6, -1.2), new JsonArray(6, 0), new JsonArray(3, 0)) });
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        Pen? Shadows() => Views.Render(f.Session.Reader, Views.Find(f.Model, south)!).Pens.FirstOrDefault(p => p.Layer == ViewShadows.Layer);
        Assert.Null(Shadows());
        // The view's own sun, the convention: the slab's shadow on the wall, down and to the right, under everything else.
        f.Run("view.setShadows", new JsonObject { ["id"] = south, ["mode"] = "view" });
        var pen = Shadows();
        Assert.NotNull(pen);
        Assert.Equal(ViewShadows.DefaultColor, pen!.Fill);
        Assert.Equal(ViewShadows.Layer, Views.Render(f.Session.Reader, Views.Find(f.Model, south)!).Pens[0].Layer);
        var pts = pen.Loops.SelectMany(l => l).ToList();
        // About as deep as the balcony stands out (1.2 m), with the slab's own thickness.
        Assert.InRange(pts.Max(p => p.Y) - pts.Min(p => p.Y), 1.2, 1.6);
        // At night by the project's sun: none.
        f.Run("view.setShadows", new JsonObject { ["id"] = south, ["mode"] = "project", ["date"] = "2026-06-21", ["hour"] = 2 });
        Assert.Null(Shadows());
        // At noon the sun is high in the south: a shadow again.
        f.Run("view.setShadows", new JsonObject { ["id"] = south, ["hour"] = 13 });
        Assert.NotNull(Shadows());
        // A storey plan draws none.
        Assert.Throws<CommandException>(() => f.Run("view.setShadows", new JsonObject { ["id"] = "plan:" + f.Storey, ["mode"] = "view" }));
    }
}
