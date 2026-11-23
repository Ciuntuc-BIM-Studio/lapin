namespace Lapin.Geometry;

/// <summary>
/// An exact body read off a polyhedron whose faces are known to lie on planes and on a few curved surfaces (docs/plan-wing-walls.md, S2):
/// the faces on each curved surface gathered into one face on it, every other face on its own plane, and the boundaries between them
/// cut into edges where three or more faces meet. An edge between two planes is a line; any other is the curve through its points
/// (<c>curveOf</c>, or a quadratic through them at their chord lengths). What it makes is checked against the polyhedron by
/// <see cref="ExactCheck.Fit"/>, as every exact body is.
/// </summary>
public static class ExactRegions
{
    /// <summary>
    /// The exact body of <paramref name="s"/>, its faces lying within <paramref name="onSurface"/> of a surface of <paramref name="curved"/>
    /// taken as that surface; null when its faces do not close into a shell.
    /// </summary>
    public static ExactBody? Of(Solid s, IReadOnlyList<ExactSurface> curved, double onSurface = 1e-6, Func<IReadOnlyList<Vec3>, ExactCurve?>? curveOf = null)
    {
        var nf = s.Faces.Length;
        // Each face's surface: the curved one all its corners and its middle lie on, or its own plane (-1).
        var surfaceOf = new int[nf];
        for (var f = 0; f < nf; f++)
        {
            surfaceOf[f] = -1;
            var face = s.Faces[f];
            var pts = face.Outer.Select(i => s.Vertices[i]).ToList();
            var mid = pts.Aggregate(Vec3.Zero, (a, p) => a + p) * (1.0 / pts.Count);
            // A facet's corners on the surface, its middle near it (within the chord rule's room: a plane's face, touching the surface
            // only along an edge, has a corner off it).
            for (var k = 0; k < curved.Count && surfaceOf[f] < 0; k++)
                if (pts.All(p => curved[k].Distance(p) <= onSurface) && curved[k].Distance(mid) <= 4 * Chords.Tolerance) surfaceOf[f] = k;
        }
        // The directed edges of every loop, and the face on each side of an edge.
        var faceOfEdge = new Dictionary<(int, int), int>();
        foreach (var (f, loop) in Loops(s))
            for (var k = 0; k < loop.Length; k++) faceOfEdge[(loop[k], loop[(k + 1) % loop.Length])] = f;
        // Regions: faces on one curved surface that touch are one region; a planar face is a region of its own.
        var region = new int[nf];
        for (var f = 0; f < nf; f++) region[f] = f;
        int Find(int x) { while (region[x] != x) x = region[x] = region[region[x]]; return x; }
        foreach (var ((a, b), f) in faceOfEdge)
            if (surfaceOf[f] >= 0 && faceOfEdge.TryGetValue((b, a), out var g) && surfaceOf[g] == surfaceOf[f]) region[Find(f)] = Find(g);
        for (var f = 0; f < nf; f++) region[f] = Find(f);
        // How many regions meet at each vertex: an edge ends where three do.
        var regionsAt = new Dictionary<int, HashSet<int>>();
        foreach (var (f, loop) in Loops(s))
            foreach (var v in loop) { if (!regionsAt.TryGetValue(v, out var set)) regionsAt[v] = set = []; set.Add(region[f]); }
        // Each region's boundary: its directed edges whose other side is another region, chained into loops.
        var boundary = new Dictionary<int, List<(int A, int B)>>();
        foreach (var ((a, b), f) in faceOfEdge)
        {
            if (!faceOfEdge.TryGetValue((b, a), out var g)) return null;
            if (region[g] == region[f]) continue;
            if (!boundary.TryGetValue(region[f], out var list)) boundary[region[f]] = list = [];
            list.Add((a, b));
        }
        var builder = new ExactBuilder();
        var edges = new Dictionary<(int, int, int), (int Edge, bool Forward)>();
        foreach (var (r, list) in boundary)
        {
            var loops = new List<List<(int Edge, bool Forward)>>();
            var areas = new List<double>();
            var next = new Dictionary<int, List<int>>();
            foreach (var (a, b) in list) { if (!next.TryGetValue(a, out var l)) next[a] = l = []; l.Add(b); }
            var used = new HashSet<(int, int)>();
            foreach (var (a0, b0) in list)
            {
                if (used.Contains((a0, b0))) continue;
                var ring = new List<int> { a0 };
                var (a, b) = (a0, b0);
                while (true)
                {
                    used.Add((a, b));
                    if (b == a0) break;
                    ring.Add(b);
                    var options = next.GetValueOrDefault(b);
                    var c = options?.FirstOrDefault(x => !used.Contains((b, x)), -1) ?? -1;
                    if (c < 0) return null;
                    (a, b) = (b, c);
                }
                var loop = new List<(int, bool)>();
                foreach (var chain in Chains(ring, v => regionsAt[v].Count >= 3, (u, w) => region[faceOfEdge[(w, u)]]))
                    loop.Add(EdgeOf(chain));
                loops.Add(loop);
                areas.Add(VecMath.NewellNormal(ring.Select(v => s.Vertices[v]).ToList()).Length);
            }
            // The outer loop first: the largest.
            var order = Enumerable.Range(0, loops.Count).OrderByDescending(k => areas[k]).ToList();
            var ordered = order.Select(k => (IReadOnlyList<(int, bool)>)loops[k]).ToList();
            var faces = Enumerable.Range(0, nf).Where(f => region[f] == r).ToList();
            var k0 = surfaceOf[faces[0]];
            if (k0 < 0) builder.PlaneFace(ordered, s.Faces[faces[0]].Normal);
            else
            {
                // A point on the surface well inside it: the middle of its largest facet, put onto the surface.
                var big = faces.MaxBy(f => Measure.FaceArea(s, f));
                var pts = s.Faces[big].Outer.Select(i => s.Vertices[i]).ToList();
                var mid = pts.Aggregate(Vec3.Zero, (a, p) => a + p) * (1.0 / pts.Count);
                var inside = curved[k0] is SplineSurface sp ? sp.Surface.At(sp.Nearest(mid).U, sp.Nearest(mid).V) : mid;
                builder.Face(curved[k0], ordered, inside);
            }
        }
        return builder.Body();

        // An edge for a chain of vertices, made once whichever side asks first (each side walks it the other way).
        (int, bool) EdgeOf(List<int> chain)
        {
            var rev = Enumerable.Reverse(chain).ToList();
            var canon = (chain[0], chain[1]).CompareTo((rev[0], rev[1])) <= 0 ? chain : rev;
            var key = (canon[0], canon[1], canon[^1]);
            if (!edges.TryGetValue(key, out var made))
            {
                var pts = canon.Select(v => s.Vertices[v]).ToList();
                made = builder.Edge(pts[0], pts[^1], CurveOf(pts));
                edges[key] = made;
            }
            return ReferenceEquals(canon, chain) ? made : (made.Edge, !made.Forward);
        }

        ExactCurve CurveOf(List<Vec3> pts)
        {
            var (a, b) = (pts[0], pts[^1]);
            var dir = (b - a).Normalized();
            var straight = pts.All(p => { var d = p - a; return (d - dir * Vec3.Dot(d, dir)).Length <= 1e-7; });
            if (straight) return new LineCurve(a, dir);
            if (curveOf?.Invoke(pts) is { } c) return c;
            return new SplineCurve(BSplineCurve.Interpolate(pts, 2));
        }
    }

    static IEnumerable<(int Face, int[] Loop)> Loops(Solid s)
    {
        for (var f = 0; f < s.Faces.Length; f++)
        {
            yield return (f, s.Faces[f].Outer);
            foreach (var h in s.Faces[f].Holes) yield return (f, h);
        }
    }

    /// <summary>
    /// A closed ring of vertices cut where <paramref name="corner"/> holds or the region across changes (<paramref name="across"/> of an
    /// edge's two ends); a ring with no such vertex is cut in two halves.
    /// </summary>
    static List<List<int>> Chains(List<int> ring, Func<int, bool> corner, Func<int, int, int> across)
    {
        var n = ring.Count;
        var cuts = new List<int>();
        for (var k = 0; k < n; k++)
        {
            var (prev, at, nxt) = (ring[(k - 1 + n) % n], ring[k], ring[(k + 1) % n]);
            if (corner(at) || across(prev, at) != across(at, nxt)) cuts.Add(k);
        }
        if (cuts.Count == 0) cuts = [0, n / 2];
        if (cuts.Count == 1) cuts.Add((cuts[0] + n / 2) % n);
        cuts.Sort();
        var o = new List<List<int>>();
        for (var c = 0; c < cuts.Count; c++)
        {
            var (from, to) = (cuts[c], cuts[(c + 1) % cuts.Count]);
            if (to <= from) to += n;
            var chain = new List<int>();
            for (var k = from; k <= to; k++) chain.Add(ring[k % n]);
            o.Add(chain);
        }
        return o;
    }
}
