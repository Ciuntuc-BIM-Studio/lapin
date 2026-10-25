using BubbleBim.Geometry;

namespace Lapin.Drawing.Hatch;

/// <summary>
/// "Pick a point inside" (the old Lapin's): the smallest closed loop holding the
/// point is the outline, and every loop nested in it its islands, first-level
/// ones first. Filled even-odd, that is the "normal" island style. An outline
/// given twice (a closed polyline and the area its own segments enclose) would
/// cancel itself under even-odd, so repeats are dropped first.
/// </summary>
public static class HatchRegion
{
    public static double LoopArea(IReadOnlyList<Vec2> pts) => Math.Abs(Polygon2.SignedArea(pts));

    sealed record Loop(IReadOnlyList<Vec2> Pts, double Area, double MinX, double MinY, double MaxX, double MaxY);

    static Loop Info(IReadOnlyList<Vec2> pts) =>
        new(pts, LoopArea(pts), pts.Min(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.X), pts.Max(p => p.Y));

    static double DistSqToSeg(Vec2 p, Vec2 a, Vec2 b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var len2 = dx * dx + dy * dy;
        var t = len2 > 0 ? Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1) : 0;
        var (ex, ey) = (a.X + t * dx - p.X, a.Y + t * dy - p.Y);
        return ex * ex + ey * ey;
    }

    static bool OnBoundary(Vec2 p, Loop l, double tol)
    {
        for (int i = 0, j = l.Pts.Count - 1; i < l.Pts.Count; j = i++)
            if (DistSqToSeg(p, l.Pts[j], l.Pts[i]) <= tol * tol) return true;
        return false;
    }

    /// <summary>Even-odd point in polygon (ray casting).</summary>
    public static bool Contains(IReadOnlyList<Vec2> poly, Vec2 p)
    {
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var (a, b) = (poly[i], poly[j]);
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    /// <summary>Whether inner lies in outer: no vertex strictly outside, one strictly inside (touching ones aside; all touching: the centroid decides).</summary>
    static bool Inside(Loop inner, Loop outer)
    {
        if (inner.Area >= outer.Area) return false;
        if (inner.MinX < outer.MinX - 1e-9 || inner.MaxX > outer.MaxX + 1e-9 || inner.MinY < outer.MinY - 1e-9 || inner.MaxY > outer.MaxY + 1e-9) return false;
        var tol = Math.Max(Math.Max(outer.MaxX - outer.MinX, outer.MaxY - outer.MinY) * 1e-9, 1e-12);
        var any = false;
        foreach (var v in inner.Pts)
        {
            if (OnBoundary(v, outer, tol)) continue;
            if (!Contains(outer.Pts, v)) return false;
            any = true;
        }
        if (any) return true;
        var c = new Vec2(inner.Pts.Average(p => p.X), inner.Pts.Average(p => p.Y));
        return Contains(outer.Pts, c);
    }

    static bool Same(Loop a, Loop b)
    {
        var span = Math.Max(Math.Max(a.MaxX - a.MinX, a.MaxY - a.MinY), 1e-9);
        var tol = span * 1e-6;
        if (Math.Abs(a.Area - b.Area) > span * span * 1e-6) return false;
        if (Math.Abs(a.MinX - b.MinX) > tol || Math.Abs(a.MinY - b.MinY) > tol || Math.Abs(a.MaxX - b.MaxX) > tol || Math.Abs(a.MaxY - b.MaxY) > tol) return false;
        return a.Pts.All(v => OnBoundary(v, b, tol)) && b.Pts.All(v => OnBoundary(v, a, tol));
    }

    /// <summary>The region around p: [outline, …islands] (first-level islands first, then those nested in them), or null when no loop holds p.</summary>
    public static List<IReadOnlyList<Vec2>>? At(Vec2 p, IEnumerable<IReadOnlyList<Vec2>> loops)
    {
        var valid = new List<Loop>();
        foreach (var l in loops)
        {
            if (l.Count < 3) continue;
            var li = Info(l);
            var span = Math.Max(li.MaxX - li.MinX, li.MaxY - li.MinY);
            if (!(li.Area > span * span * 1e-12)) continue;
            if (valid.Any(v => Same(v, li))) continue;
            valid.Add(li);
        }
        Loop? outer = null;
        foreach (var l in valid)
        {
            if (p.X < l.MinX || p.X > l.MaxX || p.Y < l.MinY || p.Y > l.MaxY || !Contains(l.Pts, p)) continue;
            if (outer == null || l.Area < outer.Area) outer = l;
        }
        if (outer == null) return null;
        var candidates = valid.Where(l => l != outer && !Contains(l.Pts, p) && Inside(l, outer)).ToList();
        var holes = candidates.Where(l => !candidates.Any(o => o != l && Inside(l, o))).ToList();
        var nested = candidates.Where(l => !holes.Contains(l));
        return [outer.Pts, .. holes.Select(h => h.Pts), .. nested.Select(h => h.Pts)];
    }
}
