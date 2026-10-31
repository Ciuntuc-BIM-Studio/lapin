using Lapin.Geometry;
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
    /// The plane of a view a sketch can lie on (<see cref="Views.PlaneOf"/>): a plan's is its storey's level (the normal up),
    /// a section's its cut line, standing (the normal toward the viewer; a developed section's piece the sketch lies on, by
    /// <paramref name="atX"/>), an elevation's once it has one (Studio gives it one when it is first sketched on), a reference
    /// plane's. A sketch off a plan stands on the highest storey at or below the plane's origin.
    /// </summary>
    public static SketchPlane? PlaneOf(IModelView m, Entity view, IServices? services = null, double? atX = null)
    {
        if (Views.PlaneOf(m, services, view, atX) is not { } p) return null;
        var storey = view.Str("kind") is "plan" or null ? m.Get(view.Str("storeyId") ?? "")?.Id
            : (Spatial.Storeys(m).LastOrDefault(s => s.Num("elevation") <= p.Origin.Z + 1e-6) ?? Spatial.Storeys(m).FirstOrDefault())?.Id;
        return new SketchPlane(p.Origin, p.Right, p.Up, p.Normal, storey);
    }

    /// <summary>The middle of a shape's box along the view's x: which piece of a developed section it is drawn on.</summary>
    public static double? MidX(Shape? s)
    {
        if (s == null) return null;
        var (lo, hi) = Geo.Bounds(s);
        return (lo.X + hi.X) / 2;
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
