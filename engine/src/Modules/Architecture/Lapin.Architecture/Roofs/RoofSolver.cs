// Ported from BubbleBIM Pro (the user's own engine, engine/src/Modules/Building/BubbleBim.Building/Roofs), adapted to Lapin's model.
using Lapin.Geometry;

namespace Lapin.Architecture.Roofs;

/// <summary>How an outline edge of a roof ends: an eave (a sloped plane rises from it) or a gable (a vertical end).</summary>
public enum EdgeKind { Eave, Gable }

/// <summary>The rule of one outline edge: its kind, its pitch (eaves, degrees) and its overhang past the wall plate (a gable's is the rake), metres.</summary>
public sealed record EdgeRule(EdgeKind Kind, double Pitch, double Overhang);

/// <summary>A roof plane: rises from outline edge <see cref="Edge"/>; its surface counter-clockwise seen from above, z from the wall plate.</summary>
public sealed record RoofPlane(int Edge, IReadOnlyList<Vec3> Points, double Pitch);

/// <summary>Break: where a mansard's steep lower slope meets its upper one.</summary>
public enum LineKind { Ridge, Hip, Valley, Eave, Verge, Break }

/// <summary>A mansard's upper tier: its pitch, starting at the break height above the wall plate.</summary>
public sealed record Mansard(double UpperPitch, double BreakHeight);

public sealed record RoofLine(LineKind Kind, Vec3 A, Vec3 B)
{
    public double Length => (B - A).Length;
}

/// <summary>
/// The solved roof, in the outline's frame (z = 0 is the wall plate): planes,
/// gable ends, the classified lines, and the outline and rules (counter-clockwise).
/// </summary>
public sealed record RoofShape(
    IReadOnlyList<RoofPlane> Planes,
    IReadOnlyList<(int Edge, Vec3[] Points)> Gables,
    IReadOnlyList<RoofLine> Lines,
    IReadOnlyList<Vec2> Outline,
    IReadOnlyList<EdgeRule> Rules)
{
    /// <summary>The highest point (the ridge) above the wall plate.</summary>
    public double Height => Planes.Count == 0 ? 0 : Planes.Max(p => p.Points.Max(q => q.Z));

    /// <summary>Height of the roof surface over a plan point (the lowest plane covering it), or null outside the roof.</summary>
    public double? HeightAt(Vec2 p)
    {
        double? best = null;
        foreach (var pl in Planes)
        {
            var plan = pl.Points.Select(q => q.Xy).ToList();
            if (!Polygon2.Contains(plan, p) && !OnBoundary(plan, p)) continue;
            var z = RoofSolver.PlaneZ(pl.Points)(p);
            if (best == null || z < best) best = z;
        }
        return best;
    }

    static bool OnBoundary(IReadOnlyList<Vec2> poly, Vec2 p)
    {
        for (int i = 0; i < poly.Count; i++)
        {
            var (a, b) = (poly[i], poly[(i + 1) % poly.Count]);
            var d = b - a; var l = d.Length;
            if (l < 1e-12) continue;
            var t = Vec2.Dot(p - a, d) / (l * l);
            if (t >= -1e-9 && t <= 1 + 1e-9 && Math.Abs(Vec2.Cross(d, p - a)) / l < 1e-6) return true;
        }
        return false;
    }
}

/// <summary>
/// A roof from one outline (the wall-plate line, z = 0) and a rule per edge.
/// The planes follow as a weighted straight skeleton: an eave edge moves in at
/// 1 / tan(pitch), a gable stays put, so hips, valleys and ridges land where
/// planes of different pitches really meet. Overhangs: the skeleton runs on the
/// outline pushed out until every eave sits at the same (deepest) drop, then
/// each plane is cut back to its own eave line — different overhangs and
/// pitches still meet on true plane intersections. Port of Standalone's
/// lib/roof/system/geometry.ts; hip, gable and shed roofs are only presets of rules.
/// </summary>
public static class RoofSolver
{
    const double Eps = 1e-9;

    /// <summary>Why the rules cannot be built (shown to the user), or null.</summary>
    public static string? Problem(IReadOnlyList<Vec2> outline, IReadOnlyList<EdgeRule> rules)
    {
        if (outline.Count < 3) return "A roof's outline needs at least 3 corners.";
        if (!rules.Any(r => r.Kind == EdgeKind.Eave)) return "At least one edge must be an eave.";
        foreach (var r in rules)
        {
            if (r.Kind == EdgeKind.Eave && !(r.Pitch >= 1 && r.Pitch <= 85)) return "An eave's pitch is between 1° and 85°.";
            if (!(r.Overhang >= 0) || r.Overhang > 5) return "An overhang is between 0 and 5 m.";
        }
        return null;
    }

    /// <summary>The outline counter-clockwise with its rules following their edges; missing rules repeat the last.</summary>
    public static (List<Vec2> Outline, List<EdgeRule> Rules) Orient(IReadOnlyList<Vec2> outline, IReadOnlyList<EdgeRule> rules)
    {
        var pts = outline.ToList();
        if (pts.Count > 1 && (pts[0] - pts[^1]).Length < Eps) pts.RemoveAt(pts.Count - 1);
        var rs = rules.ToList();
        while (rs.Count < pts.Count) rs.Add(rs.Count > 0 ? rs[^1] : new EdgeRule(EdgeKind.Eave, 30, 0.5));
        rs = rs.Take(pts.Count).ToList();
        if (pts.Count >= 3 && Polygon2.SignedArea(pts) < 0)
        {
            int n = pts.Count;
            pts.Reverse();
            rs = Enumerable.Range(0, n).Select(j => rs[(2 * n - 2 - j) % n]).ToList();
        }
        return (pts, rs);
    }

    /// <summary>The outline with edge i pushed out (right of the counter-clockwise edge) by d[i], corners on the offset lines' intersections.</summary>
    public static Vec2[] OffsetEdges(IReadOnlyList<Vec2> pts, IReadOnlyList<double> d)
    {
        int n = pts.Count;
        var lines = pts.Select((a, i) =>
        {
            var dir = (pts[(i + 1) % n] - a).Normalized();
            return (P: a + new Vec2(dir.Y, -dir.X) * d[i], Dir: dir);
        }).ToArray();
        return pts.Select((_, i) =>
        {
            var (l1, l2) = (lines[(i + n - 1) % n], lines[i]);
            var den = Vec2.Cross(l1.Dir, l2.Dir);
            if (Math.Abs(den) < 1e-12) return l2.P;
            var t = Vec2.Cross(l2.P - l1.P, l2.Dir) / den;
            return l1.P + l1.Dir * t;
        }).ToArray();
    }

    /// <summary>
    /// A mansard: the steep roof up to the break height, and over the break
    /// outline (each eave moved in to where its steep plane reaches that height)
    /// a roof at the upper pitch — two solves of the same rules, meeting exactly
    /// on the break lines. A gable edge stays a gable in both tiers (a gambrel).
    /// </summary>
    public static RoofShape Solve(IReadOnlyList<Vec2> outline, IReadOnlyList<EdgeRule> rules, Mansard? mansard)
    {
        if (mansard == null) return Solve(outline, rules);
        var lower = Solve(outline, rules);
        var (pts, rs) = Orient(outline, rules);
        var hb = mansard.BreakHeight;
        var inset = rs.Select(r => r.Kind == EdgeKind.Eave ? -hb / Math.Tan(r.Pitch * Math.PI / 180) : 0).ToArray();
        var brk = OffsetEdges(pts, inset);
        // The break outline must still be the same polygon, smaller: no edge turned round, no corner lost.
        var fits = Polygon2.SignedArea(brk) > 1 && Enumerable.Range(0, pts.Count).All(i =>
            Vec2.Dot(brk[(i + 1) % pts.Count] - brk[i], pts[(i + 1) % pts.Count] - pts[i]) > 0.01);
        if (!fits || hb <= 0 || lower.Height <= hb + 1e-6) throw new ArgumentException("The mansard does not fit: its break is too high for this outline.");
        var upper = Solve(brk, rs.Select(r => r with { Pitch = r.Kind == EdgeKind.Eave ? mansard.UpperPitch : r.Pitch, Overhang = 0 }).ToList());
        Vec3 Up(Vec3 p) => p with { Z = p.Z + hb };
        var planes = lower.Planes
            .Select(p => p with { Points = ClipHalf(p.Points, q => hb - q.Z) })
            .Where(p => p.Points.Count >= 3)
            .Concat(upper.Planes.Select(p => p with { Points = p.Points.Select(Up).ToArray() }))
            .ToList();
        var gables = lower.Gables.Select(g => (g.Edge, ClipHalf(g.Points, q => hb - q.Z).ToArray()))
            .Concat(upper.Gables.Select(g => (g.Edge, g.Points.Select(Up).ToArray()))).ToList();
        return new RoofShape(planes, gables, Lines(planes), pts, rs);
    }

    /// <summary>The roof over the outline with its per-edge rules. Throws <see cref="ArgumentException"/> with <see cref="Problem"/>'s message.</summary>
    public static RoofShape Solve(IReadOnlyList<Vec2> outline, IReadOnlyList<EdgeRule> rules)
    {
        var (pts, rs) = Orient(outline, rules);
        if (Problem(pts, rs) is { } problem) throw new ArgumentException(problem);
        int n = pts.Count;
        var tan = rs.Select(r => r.Kind == EdgeKind.Eave ? Math.Tan(r.Pitch * Math.PI / 180) : double.PositiveInfinity).ToArray();
        // The deepest eave: every eave edge is pushed out to the line where its plane is that low.
        var drop = Math.Max(0, rs.Select((r, i) => r.Kind == EdgeKind.Eave ? r.Overhang * tan[i] : 0).Max());
        var push = rs.Select((r, i) => r.Kind == EdgeKind.Eave ? drop / tan[i] : r.Overhang).ToArray();
        var work = OffsetEdges(pts, push);
        var faces = WeightedSkeleton.Faces(work, rs.Select((r, i) => r.Kind == EdgeKind.Eave ? 1 / tan[i] : 0).ToArray());

        var planes = new List<RoofPlane>();
        var gables = new List<(int, Vec3[])>();
        for (int i = 0; i < n; i++)
        {
            var face = faces[i];
            if (face.Count < 3) continue;
            var lifted = face.Select(p => p with { Z = p.Z - drop }).ToList();
            var a = pts[i]; var b = pts[(i + 1) % n];
            var d = (b - a).Normalized(); var nIn = d.Perp;
            if (rs[i].Kind == EdgeKind.Gable)
            {
                // The vertical end over the wall line, up to the face's apex.
                var top = lifted.Max(p => p.Z);
                var apex = lifted.First(p => Math.Abs(p.Z - top) < 1e-9);
                var along = Vec2.Dot(apex.Xy - a, d);
                gables.Add((i, [a.At(0), b.At(0), (a + d * along).At(top)]));
                continue;
            }
            // Cut back to this edge's own eave line.
            var clipped = ClipHalf(lifted, p => Vec2.Dot(p.Xy - a, nIn) + rs[i].Overhang);
            if (clipped.Count >= 3) planes.Add(new RoofPlane(i, clipped, rs[i].Pitch));
        }
        return new RoofShape(planes, gables, Lines(planes), pts, rs);
    }

    /// <summary>Keep the part of a planar loop where inside(p) ≥ 0 (Sutherland–Hodgman, one half-plane).</summary>
    static List<Vec3> ClipHalf(IReadOnlyList<Vec3> loop, Func<Vec3, double> inside)
    {
        var o = new List<Vec3>();
        for (int i = 0; i < loop.Count; i++)
        {
            var (p, q) = (loop[i], loop[(i + 1) % loop.Count]);
            double dp = inside(p), dq = inside(q);
            if (dp >= -Eps) o.Add(p);
            if ((dp >= -Eps) != (dq >= -Eps)) o.Add(p + (q - p) * (dp / (dp - dq)));
        }
        return o.Where((p, i) => (p - o[(i + o.Count - 1) % o.Count]).Length > 1e-9).ToList();
    }

    /// <summary>Ridges, hips, valleys, eaves and verges: the planes' edges, classified.</summary>
    static List<RoofLine> Lines(IReadOnlyList<RoofPlane> planes)
    {
        static string Key(Vec3 p) => $"{p.X:F6},{p.Y:F6},{p.Z:F6}";
        var seen = new Dictionary<string, (Vec3 A, Vec3 B, List<int> Owners)>();
        var order = new List<string>();
        for (int k = 0; k < planes.Count; k++)
        {
            var ps = planes[k].Points;
            for (int i = 0; i < ps.Count; i++)
            {
                var (a, b) = (ps[i], ps[(i + 1) % ps.Count]);
                var (ka, kb) = (Key(a), Key(b));
                var id = string.CompareOrdinal(ka, kb) < 0 ? ka + "|" + kb : kb + "|" + ka;
                if (!seen.TryGetValue(id, out var entry)) { entry = (a, b, []); seen[id] = entry; order.Add(id); }
                entry.Owners.Add(k);
            }
        }
        var lines = new List<RoofLine>();
        foreach (var id in order)
        {
            var (a, b, owners) = seen[id];
            bool horizontal = Math.Abs(a.Z - b.Z) < 1e-6;
            if (owners.Count == 1) { lines.Add(new(horizontal ? LineKind.Eave : LineKind.Verge, a, b)); continue; }
            if (horizontal)
            {
                // A ridge falls away on both sides; a mansard's break has its upper slope rising from it.
                var midH = ((a + b) * 0.5).Xy;
                var across = (b - a).Xy.Normalized().Perp;
                bool Falls(RoofPlane p)
                {
                    var c = new Vec2(p.Points.Average(q => q.X), p.Points.Average(q => q.Y));
                    var side = Vec2.Dot(c - midH, across) >= 0 ? across : -across;
                    return PlaneZ(p.Points)(midH + side * 0.01) < a.Z - 1e-9;
                }
                lines.Add(new(owners.All(k => Falls(planes[k])) ? LineKind.Ridge : LineKind.Break, a, b));
                continue;
            }
            // Hip or valley: just inside one plane, a hip (convex) has that plane below the other's extension.
            var (p1, p2) = (planes[owners[0]], planes[owners[1]]);
            var (lo, hi) = a.Z < b.Z ? (a, b) : (b, a);
            var c1 = new Vec2(p1.Points.Average(p => p.X), p1.Points.Average(p => p.Y));
            var mid = ((a + b) * 0.5).Xy;
            var nrm = (b - a).Xy.Normalized().Perp;
            if (Vec2.Dot(nrm, c1 - mid) < 0) nrm = -nrm;
            var q = mid + nrm * 0.01;
            lines.Add(new(PlaneZ(p1.Points)(q) < PlaneZ(p2.Points)(q) - 1e-9 ? LineKind.Hip : LineKind.Valley, lo, hi));
        }
        return lines;
    }

    /// <summary>z over a plan point on the plane through a planar loop.</summary>
    public static Func<Vec2, double> PlaneZ(IReadOnlyList<Vec3> pts)
    {
        var nrm = VecMath.NewellNormal(pts);
        var o = pts[0];
        return p => Math.Abs(nrm.Z) < 1e-12 ? o.Z : o.Z - (nrm.X * (p.X - o.X) + nrm.Y * (p.Y - o.Y)) / nrm.Z;
    }
}
