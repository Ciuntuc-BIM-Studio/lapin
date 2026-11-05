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
