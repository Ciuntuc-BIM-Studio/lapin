using BubbleBim.Geometry;
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
        var o = new List<GeometryPart>();
        foreach (var part in parts)
        {
            try { if (booleans.Difference(part.Solid, by, part.Solid.Tag) is { } cut) o.Add(part with { Solid = cut }); }
            catch (InvalidOperationException ex) { problems.Add($"{part.Name}: could not be cut ({ex.Message})."); o.Add(part); }
        }
        return o;
    }
}
