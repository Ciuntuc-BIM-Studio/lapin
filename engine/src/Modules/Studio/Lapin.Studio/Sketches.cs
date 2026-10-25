using BubbleBim.Geometry;
using Lapin.Drafting.Cad;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>Where a sketch lies in the model: its view's plane, from the view's coordinates to the world, and the side it faces.</summary>
public sealed record SketchPlane(Vec3 Origin, Vec3 Right, Vec3 Up, Vec3 Normal, string? StoreyId)
{
    public Vec3 World(Vec2 p) => Origin + Right * p.X + Up * p.Y;
}

/// <summary>A sketch: the outline drawn on a view, its holes, the plane they lie on.</summary>
public static class Sketches
{
    /// <summary>
    /// The plane of a view a sketch can lie on: a plan's is its storey's level
    /// (the normal up), a section's its cut line, standing (the normal toward
    /// the viewer). Elevations have no fixed plane: null.
    /// </summary>
    public static SketchPlane? PlaneOf(IModelView m, Entity view)
    {
        switch (view.Str("kind") ?? "plan")
        {
            case "plan":
            {
                var storey = m.Get(view.Str("storeyId") ?? "");
                return new SketchPlane(new Vec3(0, 0, storey?.Num("elevation") ?? 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0), Vec3.UnitZ, storey?.Id);
            }
            case "section":
            {
                if (view.Attrs["start"] is not System.Text.Json.Nodes.JsonArray s || view.Attrs["end"] is not System.Text.Json.Nodes.JsonArray e) return null;
                var (a, b) = (new Vec2(Json.Number(s[0]) ?? 0, Json.Number(s[1]) ?? 0), new Vec2(Json.Number(e[0]) ?? 0, Json.Number(e[1]) ?? 0));
                if ((b - a).Length < 1e-6) return null;
                var spec = Lapin.Drawing.Views.ViewSpec.Section(a, b, 1);
                var f = spec.Frame;
                return new SketchPlane(f.Origin, f.Right, f.Up, -f.Dir, Spatial.Storeys(m).FirstOrDefault()?.Id);
            }
        }
        return null;
    }

    /// <summary>A closed shape's points (a circle as 64 sides); null for an open one.</summary>
    public static IReadOnlyList<Vec2>? Loop(Shape? s) => s switch
    {
        Polyline { Closed: true } p when p.Pts.Count >= 3 => p.Pts,
        Circle c => Geo.Polylines(c, Math.PI / 32)[0].SkipLast(1).ToList(),
        _ => null,
    };

    /// <summary>
    /// A profile's holes: the closed shapes on its view lying inside it, the
    /// first level only (an island inside a hole is a profile of its own), not
    /// themselves the profile of a solid.
    /// </summary>
    public static List<(string Id, IReadOnlyList<Vec2> Loop)> Holes(IModelView m, Entity profile, IReadOnlyCollection<string> profiles)
    {
        var outer = Loop(Drafts.ShapeOf(profile));
        if (outer == null) return [];
        var candidates = Drafts.On(m, profile.Str("viewId") ?? "")
            .Where(e => e.Id != profile.Id && !profiles.Contains(e.Id))
            .Select(e => (e.Id, Loop: Loop(Drafts.ShapeOf(e))))
            .Where(x => x.Loop != null && x.Loop.All(p => Polygon2.Contains(outer, p)))
            .Select(x => (x.Id, Loop: x.Loop!)).ToList();
        return candidates.Where(c => !candidates.Any(o => o.Id != c.Id && Math.Abs(Polygon2.SignedArea(o.Loop)) > Math.Abs(Polygon2.SignedArea(c.Loop)) && c.Loop.All(p => Polygon2.Contains(o.Loop, p)))).ToList();
    }
}
