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
        Circle c => Geo.Polylines(c)[0].SkipLast(1).ToList(),
        _ => null,
    };

    /// <summary>A closed outline on a view: the drafted shapes it is made of (one closed shape, or lines, arcs and polylines end to end), its points.</summary>
    public sealed record Outline(IReadOnlyList<string> Ids, IReadOnlyList<Vec2> Loop);

    /// <summary>
    /// Every closed outline on a view: each closed polyline and circle, and each
    /// loop of open shapes (lines, arcs, open polylines) meeting end to end, an
    /// arc in steps of at most 5.6° (as a circle is); a loop stops where three
    /// shapes meet. Construction lines take no part.
    /// </summary>
    public static List<Outline> Closed(IModelView m, string viewId)
    {
        var o = new List<Outline>();
        var open = new List<(string Id, List<Vec2> Pts)>();
        foreach (var e in Drafts.On(m, viewId))
        {
            if (e.Str("role") == "draft" || e.Attrs["hatch"] != null) continue;
            var shape = Drafts.ShapeOf(e);
            if (Loop(shape) is { } loop) { o.Add(new([e.Id], loop)); continue; }
            if (shape is Line or Arc or Polyline { Closed: false }) open.Add((e.Id, Geo.Polylines(shape)[0]));
        }
        if (open.Count < 2) return o;
        foreach (var chain in Lapin.Drafting.Cad.Joins.Chains(open.Select(x => (IReadOnlyList<Vec2>)x.Pts).ToList(), 1e-6))
        {
            if (!chain.Closed) continue;
            var pts = chain.Pts.ToList();
            if (pts.Count > 1 && (pts[^1] - pts[0]).Length < 1e-6) pts.RemoveAt(pts.Count - 1);
            if (pts.Count >= 3) o.Add(new(chain.Members.Select(i => open[i].Id).ToList(), pts));
        }
        return o;
    }

    /// <summary>The closed outline a drafted shape is (or is a piece of); null when it closes nothing.</summary>
    public static Outline? OutlineOf(IModelView m, Entity draft)
    {
        if (Loop(Drafts.ShapeOf(draft)) is { } loop) return new([draft.Id], loop);
        return Closed(m, draft.Str("viewId") ?? "").FirstOrDefault(x => x.Ids.Contains(draft.Id));
    }

    /// <summary>
    /// A profile's holes: the closed outlines on its view lying inside it, the
    /// first level only (an island inside a hole is a profile of its own), not
    /// themselves the profile of a solid.
    /// </summary>
    public static List<(string Id, IReadOnlyList<Vec2> Loop)> Holes(IModelView m, Entity profile, IReadOnlyCollection<string> profiles) =>
        HoleOutlines(m, profile, profiles).Select(h => (h.Ids[0], h.Loop)).ToList();

    public static List<Outline> HoleOutlines(IModelView m, Entity profile, IReadOnlyCollection<string> profiles)
    {
        var all = Closed(m, profile.Str("viewId") ?? "");
        var outer = all.FirstOrDefault(x => x.Ids.Contains(profile.Id));
        if (outer == null) return [];
        var candidates = all.Where(x => x != outer && !x.Ids.Any(profiles.Contains) && x.Loop.All(p => Polygon2.Contains(outer.Loop, p))).ToList();
        return candidates.Where(c => !candidates.Any(o => o != c && Math.Abs(Polygon2.SignedArea(o.Loop)) > Math.Abs(Polygon2.SignedArea(c.Loop)) && c.Loop.All(p => Polygon2.Contains(o.Loop, p)))).ToList();
    }
}
