using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What an element's end is attached to, read from the target's body (docs/plan-attach-to.md §3.2, stage A1): moved out of
/// the walls' top (<c>WallTops</c>) so every class with an end to attach reads its targets alike.
/// <list type="bullet">
/// <item>A target's triangles, flagged as looking down (an underside) or up (a top face), vertical ones dropped; kept per
/// entity and model revision (<see cref="RevisionCache{T}"/>), whose reads land in the caller's tracking view, so an attached
/// element is built again whenever what its target was built from changes.</item>
/// <item>Along a line (<see cref="Underside"/>, its mirror <see cref="Overside"/>): sampled every few centimetres, a jump found
/// to a millimetre by bisection and kept as a step, simplified to half a millimetre.</item>
/// <item>Over an area (<see cref="UnderAt"/>, <see cref="OverAt"/> at many plan points): a section's corners and centre.</item>
/// <item>A guard against building in a circle (§3.6): <see cref="Sample"/> keeps, per thread, the elements whose targets are
/// being read. A target already among them is skipped; an element its own targets came back to stands at its own end, never
/// recursing until the stack overflows (which would end the engine).</item>
/// </list>
/// </summary>
public static class BodySampler
{
    /// <summary>A triangle of a body with its plan box, and whether it looks down (an underside) or up (a top face).</summary>
    public sealed record Tri(Vec3 A, Vec3 B, Vec3 C, double X0, double X1, double Y0, double Y1, bool Down)
    {
        public bool Up => !Down;
    }

    /// <summary>
    /// What <see cref="Sample"/> read: the targets' triangles, and the targets left out because reading them came back round
    /// to an element being built (<see cref="Cycle"/>); <see cref="Closed"/> when the circle came back to this element itself,
    /// which then has no triangles and stands at its own end.
    /// </summary>
    public sealed record Sampled(List<Tri> Tris, IReadOnlyList<string> Cycle, bool Closed)
    {
        public bool InCycle => Closed || Cycle.Count > 0;
    }

    /// <summary>A body's triangles, kept per entity (immutable) and model revision: every element under it asks for them.</summary>
    static readonly RevisionCache<List<Tri>> Cache = new();

    [ThreadStatic] static List<string>? _building;
    [ThreadStatic] static HashSet<string>? _closed;

    /// <summary>
    /// The triangles of <paramref name="targets"/> for <paramref name="element"/>'s end, with the guard against a circle: a
    /// target whose reading is already under way on this thread (it is attached, through others, to this element) is left out,
    /// and when the circle closes on <paramref name="element"/> itself it gets no triangles at all.
    /// </summary>
    public static Sampled Sample(Entity element, IEnumerable<Entity> targets, IModelView m, IServices services)
    {
        var building = _building ??= [];
        var closed = _closed ??= [];
        var tris = new List<Tri>();
        var cycle = new List<string>();
        building.Add(element.Id);
        try
        {
            foreach (var t in targets)
            {
                if (t.Id == element.Id) continue;
                if (building.Contains(t.Id)) { closed.Add(t.Id); cycle.Add(t.Id); continue; }
                tris.AddRange(TrianglesOf(t, m, services));
            }
        }
        finally { building.RemoveAt(building.Count - 1); }
        return closed.Remove(element.Id) ? new Sampled([], cycle, true) : new Sampled(tris, cycle, false);
    }

    /// <summary>Whether this thread is reading <paramref name="id"/>'s targets now (a test's look at the guard).</summary>
    public static bool Building(string id) => _building?.Contains(id) == true;

    /// <summary>A body's triangles (cached per revision), without the guard: <see cref="Sample"/> is what an attached end reads.</summary>
    public static List<Tri> TrianglesOf(Entity t, IModelView m, IServices services) => Cache.Get(t, m, v => Triangles(t, v, services));

    static List<Tri> Triangles(Entity t, IModelView m, IServices services)
    {
        var producer = StoredBodies.Has(t) ? new GeometryProducer(StoredBodies.Geometry) : services.Get<IGeometryProducers>()?.Geometry(t.Class);
        var list = new List<Tri>();
        foreach (var part in producer?.Invoke(t, m, services)?.Parts ?? [])
        {
            var v = part.Solid.Vertices;
            foreach (var (a, b, c) in Measure.Triangles(part.Solid))
            {
                var (p, q, r) = (v[a], v[b], v[c]);
                var nz = Vec3.Cross(q - p, r - p).Z;
                if (Math.Abs(nz) < 1e-12) continue;   // vertical: never an underside nor a top
                list.Add(Of(p, q, r, nz < 0));
            }
        }
        return list;
    }

    /// <summary>A triangle with its plan box; <paramref name="down"/>: it looks down.</summary>
    public static Tri Of(Vec3 p, Vec3 q, Vec3 r, bool down) =>
        new(p, q, r, Math.Min(p.X, Math.Min(q.X, r.X)), Math.Max(p.X, Math.Max(q.X, r.X)), Math.Min(p.Y, Math.Min(q.Y, r.Y)), Math.Max(p.Y, Math.Max(q.Y, r.Y)), down);

    /// <summary>The triangle's height over a plan point, or null when the point is not under it.</summary>
    public static double? ZAt(Tri t, Vec2 p)
    {
        var d = t.B.Xy - t.A.Xy;
        var e = t.C.Xy - t.A.Xy;
        var den = Vec2.Cross(d, e);
        if (Math.Abs(den) < 1e-14) return null;
        var q = p - t.A.Xy;
        var l1 = Vec2.Cross(q, e) / den;
        var l2 = Vec2.Cross(d, q) / den;
        if (l1 < -1e-9 || l2 < -1e-9 || l1 + l2 > 1 + 1e-9) return null;
        return t.A.Z + l1 * (t.B.Z - t.A.Z) + l2 * (t.C.Z - t.A.Z);
    }

    /// <summary>The lowest face looking down above <paramref name="above"/> over the plan point; null when there is none.</summary>
    public static double? UnderAt(IEnumerable<Tri> tris, Vec2 p, double above)
    {
        var best = double.PositiveInfinity;
        foreach (var t in tris)
        {
            if (!t.Down || p.X < t.X0 || p.X > t.X1 || p.Y < t.Y0 || p.Y > t.Y1) continue;
            if (ZAt(t, p) is { } z && z > above + 1e-6 && z < best) best = z;
        }
        return double.IsPositiveInfinity(best) ? null : best;
    }

    /// <summary>The highest face looking up below <paramref name="below"/> under the plan point; null when there is none.</summary>
    public static double? OverAt(IEnumerable<Tri> tris, Vec2 p, double below)
    {
        var best = double.NegativeInfinity;
        foreach (var t in tris)
        {
            if (!t.Up || p.X < t.X0 || p.X > t.X1 || p.Y < t.Y0 || p.Y > t.Y1) continue;
            if (ZAt(t, p) is { } z && z < below - 1e-6 && z > best) best = z;
        }
        return double.IsNegativeInfinity(best) ? null : best;
    }

    /// <summary>
    /// Over an area (a section's corners and centre): at each plan point, the lowest underside above <paramref name="above"/>
    /// (<paramref name="top"/>), or the highest top face below <paramref name="above"/> (not <paramref name="top"/>: a bottom);
    /// null where nothing is there.
    /// </summary>
    public static double?[] Area(IReadOnlyList<Tri> tris, IReadOnlyList<Vec2> points, double above, bool top)
    {
        if (points.Count == 0) return [];
        var (x0, x1) = (points.Min(p => p.X) - 1e-6, points.Max(p => p.X) + 1e-6);
        var (y0, y1) = (points.Min(p => p.Y) - 1e-6, points.Max(p => p.Y) + 1e-6);
        var near = tris.Where(t => t.Down == top && t.X1 >= x0 && t.X0 <= x1 && t.Y1 >= y0 && t.Y0 <= y1).ToList();
        return [.. points.Select(p => top ? UnderAt(near, p, above) : OverAt(near, p, above))];
    }

    /// <summary>
    /// The bodies' underside along a line from <paramref name="a"/> along <paramref name="dir"/> for <paramref name="length"/>,
    /// as (u along it, v above <paramref name="baseZ"/>), sampled every few centimetres from the base up (<paramref name="height"/>
    /// where nothing is over it); a jump between two samples (the body's end, a vertical face) is found to a millimetre and kept
    /// as a step. Null when the top stays at <paramref name="height"/> all along.
    /// </summary>
    public static List<(double U, double V)>? Underside(List<Tri> tris, Vec2 a, Vec2 dir, double length, double baseZ, double height) =>
        Along(tris, a, dir, length, baseZ, height, top: true);

    /// <summary>
    /// The mirror of <see cref="Underside"/> for a bottom: the bodies' top faces along the line, below the element's top
    /// (<paramref name="baseZ"/> + <paramref name="height"/>), as (u, v above <paramref name="baseZ"/>); 0 where nothing is under it.
    /// Null when the bottom stays at the base all along.
    /// </summary>
    public static List<(double U, double V)>? Overside(List<Tri> tris, Vec2 a, Vec2 dir, double length, double baseZ, double height) =>
        Along(tris, a, dir, length, baseZ, height, top: false);

    static List<(double U, double V)>? Along(List<Tri> tris, Vec2 a, Vec2 dir, double L, double baseZ, double H, bool top)
    {
        if (L < 1e-6) return null;
        var b = a + dir * L;
        var (x0, x1) = (Math.Min(a.X, b.X) - 1e-6, Math.Max(a.X, b.X) + 1e-6);
        var (y0, y1) = (Math.Min(a.Y, b.Y) - 1e-6, Math.Max(a.Y, b.Y) + 1e-6);
        var near = tris.Where(t => t.Down == top && t.X1 >= x0 && t.X0 <= x1 && t.Y1 >= y0 && t.Y0 <= y1).ToList();
        if (near.Count == 0) return null;
        var own = top ? H : 0;
        double? Hit(double u)
        {
            var p = a + dir * u;
            return top ? UnderAt(near, p, baseZ) - baseZ : OverAt(near, p, baseZ + H) - baseZ;
        }
        var step = Math.Clamp(L / 200, 0.02, 0.1);
        var n = Math.Max(1, (int)Math.Ceiling(L / step - 1e-9));
        var samples = new List<(double U, double V)>();
        for (var i = 0; i <= n; i++) { var u = Math.Min(L, i * step); samples.Add((u, Hit(u) ?? own)); }
        var profile = new List<(double U, double V)> { samples[0] };
        for (var i = 1; i < samples.Count; i++)
        {
            var (p, q) = (samples[i - 1], samples[i]);
            if (Math.Abs(q.V - p.V) > Math.Max(0.1, 2.5 * step))
            {
                var (lo, hi, vLo, vHi) = (p.U, q.U, p.V, q.V);
                for (var k = 0; k < 12 && hi - lo > 1e-3; k++)
                {
                    var mid = (lo + hi) / 2;
                    var vm = Hit(mid) ?? own;
                    if (Math.Abs(vm - vLo) <= Math.Abs(vm - vHi)) (lo, vLo) = (mid, vm); else (hi, vHi) = (mid, vm);
                }
                var at = (lo + hi) / 2;
                profile.Add((at, vLo));
                profile.Add((at, vHi));
            }
            profile.Add(q);
        }
        if (profile.All(p => Math.Abs(p.V - own) < 1e-6)) return null;
        return Simplify(profile);
    }

    /// <summary>The value of a profile at u: linear between its points, flat past its ends (a step is read a hair to either side).</summary>
    public static double Eval(List<(double U, double V)> p, double u)
    {
        if (u <= p[0].U) return p[0].V;
        if (u >= p[^1].U) return p[^1].V;
        for (var i = 0; i + 1 < p.Count; i++)
        {
            var (a, b) = (p[i], p[i + 1]);
            if (b.U - a.U < 1e-12 || u < a.U || u > b.U) continue;
            return a.V + (b.V - a.V) * (u - a.U) / (b.U - a.U);
        }
        return p[^1].V;
    }

    /// <summary>The lower of two profiles everywhere: at both their points, and where they cross.</summary>
    public static List<(double U, double V)> Merge(List<(double U, double V)> a, List<(double U, double V)> b)
    {
        var us = new SortedSet<double>(a.Select(p => p.U).Concat(b.Select(p => p.U)));
        for (var i = 0; i + 1 < a.Count; i++)
            for (var j = 0; j + 1 < b.Count; j++)
            {
                var (a0, a1, b0, b1) = (a[i], a[i + 1], b[j], b[j + 1]);
                if (a1.U - a0.U < 1e-9 || b1.U - b0.U < 1e-9) continue;
                var (lo, hi) = (Math.Max(a0.U, b0.U), Math.Min(a1.U, b1.U));
                if (hi - lo < 1e-9) continue;
                var (ka, kb) = ((a1.V - a0.V) / (a1.U - a0.U), (b1.V - b0.V) / (b1.U - b0.U));
                if (Math.Abs(ka - kb) < 1e-12) continue;
                var u = ((b0.V - kb * b0.U) - (a0.V - ka * a0.U)) / (ka - kb);
                if (u > lo + 1e-9 && u < hi - 1e-9) us.Add(u);
            }
        const double hair = 1e-7;
        var o = new List<(double U, double V)>();
        foreach (var u in us)
        {
            var l = Math.Min(Eval(a, u - hair), Eval(b, u - hair));
            var r = Math.Min(Eval(a, u + hair), Eval(b, u + hair));
            if (Math.Abs(l - r) > 1e-3) { o.Add((u, l)); o.Add((u, r)); }
            else o.Add((u, Math.Min(Eval(a, u), Eval(b, u))));
        }
        return Simplify(o);
    }

    /// <summary>Drop the points within half a millimetre of the line between their neighbours.</summary>
    public static List<(double U, double V)> Simplify(List<(double U, double V)> p)
    {
        var o = new List<(double U, double V)>();
        foreach (var q in p)
        {
            if (o.Count > 0 && Math.Abs(o[^1].U - q.U) < 1e-9 && Math.Abs(o[^1].V - q.V) < 1e-9) continue;
            while (o.Count >= 2 && Deviation(o[^2], o[^1], q) < 5e-4) o.RemoveAt(o.Count - 1);
            o.Add(q);
        }
        return o;
    }

    /// <summary>How far b stands off the segment a–c.</summary>
    static double Deviation((double U, double V) a, (double U, double V) b, (double U, double V) c)
    {
        var (dx, dy) = (c.U - a.U, c.V - a.V);
        var len2 = dx * dx + dy * dy;
        if (len2 < 1e-18) return Math.Sqrt((b.U - a.U) * (b.U - a.U) + (b.V - a.V) * (b.V - a.V));
        var t = Math.Clamp(((b.U - a.U) * dx + (b.V - a.V) * dy) / len2, 0, 1);
        var (px, py) = (a.U + dx * t - b.U, a.V + dy * t - b.V);
        return Math.Sqrt(px * px + py * py);
    }
}
