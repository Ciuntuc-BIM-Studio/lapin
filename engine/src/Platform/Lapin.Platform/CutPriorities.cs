using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Cut priorities applied to an element's parts: what cuts it (a column
/// through a wall, a slab over its head) is taken out of every part, by the
/// boolean library. Without the library the parts stay whole and say so.
/// </summary>
public static class CutPriorities
{
    public static List<GeometryPart> Apply(Entity e, IModelView m, IServices? services, List<GeometryPart> parts, List<string> problems)
    {
        if (parts.Count == 0 || services?.Get<ICutters>() is not { } cutters) return parts;
        var bounds = parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        var all = (new Vec3(bounds.Min(b => b.Min.X), bounds.Min(b => b.Min.Y), bounds.Min(b => b.Min.Z)), new Vec3(bounds.Max(b => b.Max.X), bounds.Max(b => b.Max.Y), bounds.Max(b => b.Max.Z)));
        var by = cutters.For(e, m, all);
        if (by.Count == 0) return parts;
        if (services.Get<ISolidBooleans>() is not { } booleans) { problems.Add("Cut priorities need the boolean library (manifold), missing here."); return parts; }
        // Each part by the cutters that reach it, not by all that reach the element: a roof's downpipes take its box down to the
        // ground, and a floor anywhere under it cut the covering it never meets (manifold returned it with edges shared by four faces).
        var boxes = by.Select(Measure.Bounds).ToList();
        var o = new List<GeometryPart>();
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            var mine = by.Where((_, k) => boxes[k] is { } b && Meets(b, bounds[i])).ToList();
            if (mine.Count == 0) { o.Add(part); continue; }
            // A cutter's faces lying on the part's (a column flush with a wall's face, both turned) moved out a hair, so no sliver is left (decision 519).
            try { if (booleans.Difference(part.Solid, mine.Select(t => Flush.Proud(t, part.Solid)).ToList(), part.Solid.Tag) is { } cut) o.Add(part with { Solid = cut }); }
            catch (InvalidOperationException ex) { problems.Add($"{part.Name}: could not be cut ({ex.Message})."); o.Add(part); }
        }
        return o;
    }

    /// <summary>Whether two boxes overlap by more than a touch, as <see cref="ICutters.For"/> reads it.</summary>
    static bool Meets((Vec3 Min, Vec3 Max) a, (Vec3 Min, Vec3 Max) b)
    {
        const double eps = 1e-6;
        return a.Min.X < b.Max.X - eps && a.Max.X > b.Min.X + eps && a.Min.Y < b.Max.Y - eps && a.Max.Y > b.Min.Y + eps && a.Min.Z < b.Max.Z - eps && a.Max.Z > b.Min.Z + eps;
    }
}
