namespace Lapin.Geometry;

/// <summary>
/// The WEIGHTED straight skeleton of a simple polygon, as the faces of a roof
/// over it: every outline edge moves inward at its own speed, and the surface
/// over a point is as high as the time its wavefront needs to get there. Each
/// edge ends up owning one planar face. With speed = 1 / tan(pitch) the time IS
/// the height, so every edge can have its own pitch; speed 0 keeps an edge in
/// place, its face the vertical gable end.
///
/// Wavefront simulation (Felkel &amp; Obdržálek): active polygons of moving
/// vertices; the next event is the earliest EDGE event (an edge shrinks to
/// nothing) or SPLIT event (a reflex vertex runs into a non-adjacent edge).
/// Outlines are small (a building), so each step looks at every candidate:
/// O(n³), no event queue to keep consistent. Port of BubbleBIM Standalone's
/// lib/roof/system/weightedSkeleton.ts (itself from ifc-lite).
/// </summary>
public static class WeightedSkeleton
{
    const double Eps = 1e-9;

    sealed record Edge(int Index, double Speed, Vec2 A, Vec2 D, Vec2 N);
    sealed record Pt(Vec2 P, double T);
    sealed class Vertex(Pt origin, Edge inEdge, Edge outEdge)
    {
        public Pt Origin { get; } = origin;
        public Edge In { get; } = inEdge;
        public Edge Out { get; } = outEdge;
        public Vec2 W { get; } = Velocity(inEdge, outEdge);
        public Vec2 At(double t) => Origin.P + W * (t - Origin.T);
        public bool Reflex => Vec2.Cross(In.D, Out.D) < -Eps;
    }

    static Vec2 Velocity(Edge e1, Edge e2)
    {
        // dot(w, n1) = s1 and dot(w, n2) = s2.
        var (n1, n2) = (e1.N, e2.N);
        var det = n1.X * n2.Y - n1.Y * n2.X;
        if (Math.Abs(det) < 1e-12) return n1 * ((e1.Speed + e2.Speed) / 2);
        return new Vec2((n2.Y * e1.Speed - n1.Y * e2.Speed) / det, (n1.X * e2.Speed - n2.X * e1.Speed) / det);
    }

    static (double T, Vec2 P)? EdgeEvent(Vertex a, Vertex b, double now)
    {
        var d = a.Out.D;
        var den = Vec2.Dot(a.W - b.W, d);
        if (den <= Eps) return null;
        var num = Vec2.Dot((b.Origin.P - b.W * b.Origin.T) - (a.Origin.P - a.W * a.Origin.T), d);
        var t = num / den;
        return t >= now - Eps ? (t, a.At(t)) : null;
    }

    static (double T, Vec2 P)? SplitEvent(Vertex v, Vertex a, Vertex b, double now)
    {
        var e = a.Out;
        var k = Vec2.Dot(v.W, e.N);
        if (e.Speed - k <= Eps) return null;
        var d0 = Vec2.Dot(v.Origin.P - e.A, e.N);
        var t = (d0 - v.Origin.T * k) / (e.Speed - k);
        if (t < now - Eps || t <= v.Origin.T - Eps) return null;
        var p = v.At(t);
        var (pa, pb) = (a.At(t), b.At(t));
        double s = Vec2.Dot(p - pa, e.D), len = Vec2.Dot(pb - pa, e.D);
        return s >= -1e-7 && s <= len + 1e-7 ? (t, p) : null;
    }

    /// <summary>
    /// The faces of the weighted straight skeleton of <paramref name="outline"/>:
    /// edge i (point i → i + 1) moves inward at <paramref name="speeds"/>[i] ≥ 0; a
    /// face point's Z is the time its edge's wavefront needs to reach it. Faces come
    /// back in the input edge order (an edge merged away as collinear yields an empty face).
    /// </summary>
    public static List<Vec3>[] Faces(IReadOnlyList<Vec2> outline, IReadOnlyList<double> speeds)
    {
        if (speeds.Count != outline.Count || speeds.Any(s => !(s >= 0) || !double.IsFinite(s)))
            throw new ArgumentException("Every outline edge needs a finite speed ≥ 0.");
        var (pts, sp, edgeOf) = Clean(outline, speeds);
        if (pts.Count < 3) throw new ArgumentException("The outline needs at least 3 corners.");
        int n = pts.Count;
        var edges = pts.Select((a, i) =>
        {
            var b = pts[(i + 1) % n];
            var d = (b - a).Normalized();
            return new Edge(i, sp[i], a, d, d.Perp);
        }).ToArray();
        var arcs = edges.Select(_ => new List<(Pt, Pt)>()).ToArray();
        void Trace(Vertex v, Pt to) { arcs[v.In.Index].Add((v.Origin, to)); arcs[v.Out.Index].Add((v.Origin, to)); }

        var lavs = new List<List<Vertex>> { pts.Select((p, i) => new Vertex(new Pt(p, 0), edges[(i + n - 1) % n], edges[i])).ToList() };
        double now = 0;
        for (int guard = 0; lavs.Count > 0 && guard < 10000; guard++)
        {
            // Two-vertex polygons close as a ridge; empty ones go.
            lavs = lavs.Where(lav =>
            {
                if (lav.Count >= 3) return true;
                if (lav.Count == 2)
                {
                    var (a, b) = (lav[0], lav[1]);
                    arcs[a.In.Index].Add((a.Origin, b.Origin));
                    arcs[a.Out.Index].Add((a.Origin, b.Origin));
                }
                return false;
            }).ToList();

            (bool Split, double T, int Lav, int I, int J, Vec2 P)? best = null;
            for (int li = 0; li < lavs.Count; li++)
            {
                var lav = lavs[li];
                for (int i = 0; i < lav.Count; i++)
                {
                    var (a, b) = (lav[i], lav[(i + 1) % lav.Count]);
                    if (EdgeEvent(a, b, now) is { } ev && (best == null || ev.T < best.Value.T - Eps)) best = (false, ev.T, li, i, 0, ev.P);
                    if (!a.Reflex) continue;
                    for (int j = 0; j < lav.Count; j++)
                    {
                        var (ea, eb) = (lav[j], lav[(j + 1) % lav.Count]);
                        if (ea == a || eb == a) continue;
                        if (SplitEvent(a, ea, eb, now) is { } se && (best == null || se.T < best.Value.T - Eps)) best = (true, se.T, li, i, j, se.P);
                    }
                }
            }
            if (best is not { } e) break;
            now = e.T;
            var cur = lavs[e.Lav];
            var node = new Pt(e.P, e.T);
            if (!e.Split)
            {
                var (a, b) = (cur[e.I], cur[(e.I + 1) % cur.Count]);
                Trace(a, node); Trace(b, node);
                if (cur.Count == 3)
                {
                    Trace(cur[(e.I + 2) % 3], node);
                    lavs.RemoveAt(e.Lav);
                    continue;
                }
                var merged = new Vertex(node, a.In, b.Out);
                var next = cur.Where(x => x != a && x != b).ToList();
                var k = (e.I + 1) % cur.Count == 0 ? 0 : e.I;
                next.Insert(Math.Min(k, next.Count), merged);
                lavs[e.Lav] = next;
            }
            else
            {
                var v = cur[e.I];
                var (ea, eb) = (cur[e.J], cur[(e.J + 1) % cur.Count]);
                Trace(v, node);
                int m = cur.Count;
                List<Vertex> Walk(int from, Vertex to)
                {
                    var o = new List<Vertex>();
                    for (int k = from; cur[k % m] != to; k++) o.Add(cur[k % m]);
                    o.Add(to);
                    return o;
                }
                var v1 = new Vertex(node, v.In, ea.Out);
                var v2 = new Vertex(node, ea.Out, v.Out);
                int iv = e.I, ib = cur.IndexOf(eb);
                var first = new List<Vertex> { v1 }.Concat(Walk(ib, cur[(iv + m - 1) % m])).Where(x => x != v).ToList();
                var second = new List<Vertex> { v2 }.Concat(Walk((iv + 1) % m, ea)).Where(x => x != v).ToList();
                lavs.RemoveAt(e.Lav);
                lavs.InsertRange(e.Lav, [first, second]);
            }
        }

        var result = outline.Select(_ => new List<Vec3>()).ToArray();
        for (int i = 0; i < n; i++) result[edgeOf[i]] = FaceOf(edges[i], pts[(i + 1) % n], arcs[i]);
        return result;
    }

    /// <summary>Chain a face's arcs from its edge's end back to its start.</summary>
    static List<Vec3> FaceOf(Edge e, Vec2 end, List<(Pt A, Pt B)> arcs)
    {
        static bool Same(Vec2 a, Vec2 b) => (a - b).Length < 1e-7;
        var loop = new List<Pt> { new(e.A, 0), new(end, 0) };
        var left = arcs.Where(x => !Same(x.A.P, x.B.P)).ToList();
        var cur = end;
        int limit = left.Count + 1;
        for (int guard = 0; guard < limit && !Same(cur, e.A); guard++)
        {
            var k = left.FindIndex(x => Same(x.A.P, cur) || Same(x.B.P, cur));
            if (k < 0) break;
            var (a, b) = left[k];
            left.RemoveAt(k);
            var next = Same(a.P, cur) ? b : a;
            if (!Same(next.P, e.A)) loop.Add(next);
            cur = next.P;
        }
        return loop.Select(p => p.P.At(p.T)).ToList();
    }

    /// <summary>Counter-clockwise, without repeated points or collinear ones between edges of the same speed. Speeds follow their edges.</summary>
    static (List<Vec2> Pts, List<double> Speeds, List<int> EdgeOf) Clean(IReadOnlyList<Vec2> outline, IReadOnlyList<double> speeds)
    {
        var pts = outline.ToList();
        var sp = speeds.ToList();
        var edgeOf = Enumerable.Range(0, speeds.Count).ToList();
        if (Polygon2.SignedArea(pts) < 0)
        {
            // Edge i (p_i → p_i+1) becomes the edge p_i+1 → p_i of the reversed loop.
            int n = pts.Count;
            pts.Reverse();
            edgeOf = Enumerable.Range(0, n).Select(j => (2 * n - 2 - j) % n).ToList();
            sp = edgeOf.Select(i => speeds[i]).ToList();
        }
        bool changed = true;
        while (changed && pts.Count > 3)
        {
            changed = false;
            for (int i = 0; i < pts.Count; i++)
            {
                int k = (i + pts.Count - 1) % pts.Count;
                var (prev, p, next) = (pts[k], pts[i], pts[(i + 1) % pts.Count]);
                Vec2 u = p - prev, w = next - p;
                bool repeated = u.Length < 1e-9;
                bool straight = Math.Abs(Vec2.Cross(u, w)) < 1e-9 * u.Length * w.Length && Vec2.Dot(u, w) > 0 && Math.Abs(sp[k] - sp[i]) < 1e-12;
                if (!repeated && !straight) continue;
                pts.RemoveAt(i); sp.RemoveAt(i); edgeOf.RemoveAt(i);
                changed = true;
                break;
            }
        }
        return (pts, sp, edgeOf);
    }
}
