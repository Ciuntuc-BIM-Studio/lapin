using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// Volumetric context buildings (<see cref="SiteFeatures"/> of kind <c>building</c>):
/// extrudes 2D footprints (from OpenStreetMap Overpass or LiDAR class 6) into closed LoD1 prisms
/// grounded onto the project's terrain so they sit naturally on slopes and cast shadows in the sun study.
/// </summary>
public static class ContextBuildings
{
    /// <summary>Extrudes a CCW 2D polygon from <paramref name="baseZ"/> to <paramref name="topZ"/> into a <see cref="StoredBodies"/> JSON payload.</summary>
    public static JsonObject? Extrude(IReadOnlyList<Vec2> footprint, double baseZ, double topZ, string name)
    {
        var clean = CleanRing(footprint);
        if (clean.Count < 3 || topZ <= baseZ + 0.1) return null;
        if (Polygon2.SignedArea(clean) < 0) clean.Reverse();

        var n = clean.Count;
        var verts = new List<Vec3>(n * 2);
        for (var i = 0; i < n; i++) verts.Add(new Vec3(clean[i].X, clean[i].Y, baseZ));
        for (var i = 0; i < n; i++) verts.Add(new Vec3(clean[i].X, clean[i].Y, topZ));

        var tris = new List<(int, int, int)>(n * 4);
        // Top cap (CCW looking down from +Z).
        var topPts = verts.Skip(n).ToList();
        var topIx = Enumerable.Range(0, n).ToArray();
        foreach (var (a, b, c) in Triangulator.Face(topPts, topIx, [], new Vec3(0, 0, 1)))
            tris.Add((n + a, n + b, n + c));

        // Bottom cap (reversed winding, facing -Z).
        foreach (var (a, b, c) in Triangulator.Face(topPts, topIx, [], new Vec3(0, 0, 1)))
            tris.Add((a, c, b));

        // Vertical side walls.
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            tris.Add((i, j, n + j));
            tris.Add((i, n + j, n + i));
        }

        if (tris.Count == 0) return null;
        return StoredBodies.Write([(name, SiteFeatures.Color("building"), verts, tris)]);
    }

    /// <summary>
    /// Samples the lowest terrain elevation under <paramref name="footprint"/> (corners + centroid) in model heights;
    /// returns <paramref name="fallback"/> when no terrain covers the footprint.
    /// </summary>
    public static double GroundUnder(IReadOnlyList<Terrain> terrains, IReadOnlyList<Vec2> footprint, double fallback = 0)
    {
        if (terrains.Count == 0 || footprint.Count == 0) return fallback;
        double minZ = double.PositiveInfinity;
        double cx = 0, cy = 0;
        foreach (var p in footprint)
        {
            cx += p.X; cy += p.Y;
            foreach (var t in terrains)
                if (t.ElevationAt(p.X, p.Y) is { } z && z < minZ) minZ = z;
        }
        cx /= footprint.Count; cy /= footprint.Count;
        foreach (var t in terrains)
            if (t.ElevationAt(cx, cy) is { } z && z < minZ) minZ = z;
        return double.IsFinite(minZ) ? minZ : fallback;
    }

    /// <summary>Samples the highest terrain elevation under <paramref name="footprint"/> (for computing building top on a slope).</summary>
    public static double HighestGroundUnder(IReadOnlyList<Terrain> terrains, IReadOnlyList<Vec2> footprint, double fallback = 0)
    {
        if (terrains.Count == 0 || footprint.Count == 0) return fallback;
        double maxZ = double.NegativeInfinity;
        double cx = 0, cy = 0;
        foreach (var p in footprint)
        {
            cx += p.X; cy += p.Y;
            foreach (var t in terrains)
                if (t.ElevationAt(p.X, p.Y) is { } z && z > maxZ) maxZ = z;
        }
        cx /= footprint.Count; cy /= footprint.Count;
        foreach (var t in terrains)
            if (t.ElevationAt(cx, cy) is { } z && z > maxZ) maxZ = z;
        return double.IsFinite(maxZ) ? maxZ : fallback;
    }

    public static List<Terrain> AllTerrains(IModelView m) =>
        m.OfClass(IfcClass.GeographicElement).Where(Terrains.IsTerrain).Select(Terrains.Of).ToList();

    static List<Vec2> CleanRing(IReadOnlyList<Vec2> pts)
    {
        var clean = new List<Vec2>(pts.Count);
        foreach (var p in pts)
            if (double.IsFinite(p.X) && double.IsFinite(p.Y) && (clean.Count == 0 || (p - clean[^1]).Length > 1e-4))
                clean.Add(p);
        if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 1e-4) clean.RemoveAt(clean.Count - 1);
        return clean;
    }
}
