using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;

namespace Lapin.Analysis;

/// <summary>One plane of a roof: its outline in 3D, its upward unit normal, its plan (anticlockwise) and its pitch.</summary>
public sealed class RoofPlane
{
    public required IReadOnlyList<Vec3> Outline { get; init; }
    public required Vec3 Normal { get; init; }
    public required List<Vec2> Plan { get; init; }
    /// <summary>The pitch in degrees (0: level).</summary>
    public double Slope => Math.Acos(Math.Clamp(Normal.Z, -1, 1)) * 180 / Math.PI;
    /// <summary>Down the slope in plan (unit), zero for a level plane.</summary>
    public Vec2 Downslope => new Vec2(Normal.X, Normal.Y) is var h && h.Length > 1e-9 ? h * (1 / h.Length) : default;
    public double PlanArea => Math.Abs(Polygon2.SignedArea(Plan));
    public double ZAt(Vec2 p) => Outline[0].Z - (Normal.X * (p.X - Outline[0].X) + Normal.Y * (p.Y - Outline[0].Y)) / Normal.Z;
    public Vec3 Lift(Vec2 p) => p.At(ZAt(p));

    public static RoofPlane? Of(IReadOnlyList<Vec3> pts)
    {
        if (pts.Count < 3) return null;
        var n = VecMath.NewellNormal(pts.ToList());
        if (n.Length < 1e-12) return null;
        n = n.Normalized();
        if (n.Z < 0) n = -n;
        if (n.Z < 1e-6) return null;
        var plan = Polygon2.EnsureCcw(pts.Select(p => p.Xy).ToList()).ToList();
        return Math.Abs(Polygon2.SignedArea(plan)) < 1e-6 ? null : new RoofPlane { Outline = pts, Normal = n, Plan = plan };
    }
}

/// <summary>
/// A surface the snow lies on and the wind blows over: a roof's planes (a framed roof's as its roof solves them, a steel
/// hall's over its rafters, a roof deck's), or the pieces of a floor nothing covers (a terrace, a flat roof slab); its shape
/// by its planes — flat (every plane under 5°), mono-pitch (one plane, or parallel ones), duo-pitch (two planes falling
/// opposite ways) or another (a hipped roof, a roof of more planes).
/// </summary>
public sealed class RoofSurface
{
    public required string SourceId { get; init; }
    public required string Name { get; init; }
    public string? StoreyId { get; init; }
    public required List<RoofPlane> Planes { get; init; }
    /// <summary>A floor's pieces nothing covers (not a roof).</summary>
    public bool Floor { get; init; }

    public const double FlatBelow = 5;

    public string Shape
    {
        get
        {
            var sloped = Planes.Where(p => p.Slope >= FlatBelow).ToList();
            if (sloped.Count == 0) return "flat";
            if (sloped.All(p => Vec2.Dot(p.Downslope, sloped[0].Downslope) > 0.99)) return "mono";
            if (sloped.Count == 2 && Vec2.Dot(sloped[0].Downslope, sloped[1].Downslope) < -0.9) return "duo";
            return "other";
        }
    }
}

/// <summary>The surfaces the climate's loads lie on, from the analytical model (docs/plan-fem.md §9.9).</summary>
public static class RoofSurfaces
{
    public static List<RoofSurface> Of(AnalyticalModel model, IModelView m)
    {
        string NameOf(string id, string? fallback) => m.Get(id)?.Name is { Length: > 0 } n ? n : fallback ?? id;
        var o = new List<RoofSurface>();
        // Framed roofs (timber, a steel hall's): their planes.
        foreach (var r in model.Roofs)
        {
            var planes = r.Planes.Select(RoofPlane.Of).OfType<RoofPlane>().ToList();
            if (planes.Count > 0) o.Add(new RoofSurface { SourceId = r.SourceId, Name = r.Name ?? NameOf(r.SourceId, null), StoreyId = r.StoreyId ?? Spatial.StoreyOf(m, r.SourceId)?.Id, Planes = planes });
        }
        // Roof decks: each plane its deck's mid-plane over the plane's outline.
        foreach (var g in model.Shells.Select(s => s.Source).Where(s => s.IfcClass == "IfcRoof" && s.Kind == "slab" && s.Outline is { Count: >= 3 }).Distinct().GroupBy(s => s.SourceId))
        {
            var planes = g.Select(s => RoofPlane.Of(s.Outline!.Select(p => p.At(s.Z + Vec2.Dot(s.Gradient, p))).ToList())).OfType<RoofPlane>().ToList();
            if (planes.Count > 0) o.Add(new RoofSurface { SourceId = g.Key, Name = NameOf(g.Key, g.First().Name), StoreyId = g.First().StoreyId, Planes = planes });
        }
        // The pieces of floors nothing covers (a terrace, a flat roof of a slab), but those on the ground.
        var covers = o.SelectMany(s => s.Planes).ToList();
        var slabs = model.Shells.Select(s => s.Source).Where(s => s.Kind == "slab" && s.Outline is { Count: >= 3 }).Distinct().ToList();
        var ground = model.Storeys.Count > 0 ? model.Storeys.Min(s => s.Num("elevation")) : double.NegativeInfinity;
        if (model.Nodes.Count > 0) ground = Math.Max(ground, model.Nodes.Min(n => n.At.Z));
        foreach (var g in model.Shells.Where(s => s.Source.Kind == "slab" && s.Source.IfcClass != "IfcRoof" && s.Source.Gradient.Length < 1e-9).GroupBy(s => s.Source.SourceId))
        {
            var planes = new List<RoofPlane>();
            foreach (var piece in g)
            {
                if (RoofPlane.Of(piece.Nodes.Select(n => n.At).ToList()) is not { } plane) continue;
                var z = plane.Outline[0].Z;
                if (z <= ground + 0.3) continue;
                var at = Polygon2.InteriorPoint(plane.Plan);
                var covered = covers.Any(c => Polygon2.Contains(c.Plan, at) && c.ZAt(at) > z + 0.05)
                    || slabs.Any(s => s.SourceId != g.Key && Polygon2.Contains(s.Outline!, at) && s.Z + Vec2.Dot(s.Gradient, at) > z + 0.05);
                if (!covered) planes.Add(plane);
            }
            if (planes.Count > 0) o.Add(new RoofSurface { SourceId = g.Key, Name = NameOf(g.Key, g.First().Source.Name), StoreyId = g.First().Source.StoreyId, Planes = planes, Floor = true });
        }
        return o;
    }
}
