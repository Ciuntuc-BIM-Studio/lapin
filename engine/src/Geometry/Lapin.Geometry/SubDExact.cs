namespace Lapin.Geometry;

/// <summary>
/// A closed SubD body as exactly as it can be said (docs/plan-studio-freeform.md §9, F2b): every regular face of its cage (a quad whose four
/// corners each have four quads round them, with no crease in that 4 × 4 neighbourhood) is one uniform bicubic B-spline patch of its 16
/// control points — Catmull–Clark's limit surface there, exactly (Catmull &amp; Clark 1978) — written in Bézier form; the faces at star
/// points and creases are left as the body's own facets. The cage is looked at as given and, when it has irregular faces, after two steps
/// (where all but the faces round each star point are regular), so most of the surface is exact. A facet's edge along a patch is the
/// patch's own boundary curve, cut where the facets meet it (their points are its points: limit points at dyadic parameters).
/// </summary>
public static class SubDExact
{
    /// <summary>The exact body, how many patches and facets it has, the level its patches were taken at.</summary>
    public sealed record Result(ExactBody Body, int Patches, int Facets, int PatchLevel);

    /// <summary>
    /// The exact body of a closed <paramref name="cage"/> whose body was made at <paramref name="level"/> (<see cref="SubDResult.Level"/>);
    /// null when it is open or no face of it is regular (then nothing of it is exact, and the body is its facets).
    /// </summary>
    public static Result? Of(SubDCage cage, int level)
    {
        if (!cage.IsClosed || SubD.Problem(cage) != null || level < 1) return null;
        var cages = new List<SubDCage> { cage };
        var regular0 = Regular(cage);
        var j = regular0.All(r => r) ? 0 : Math.Min(2, level);
        for (var s = 1; s <= Math.Max(j, level); s++) cages.Add(SubD.Step(cages[^1]));
        var cj = cages[j];
        var regular = j == 0 ? regular0 : Regular(cj);
        if (!regular.Any(r => r)) return null;
        var cl = cages[level];
        var (lim, _) = SubD.Limit(cl);
        // Each face of the body's level and the face of the patch level it comes from (a face's children come one after another).
        var ancestor = Enumerable.Range(0, cj.Faces.Length).ToArray();
        for (var s = j; s < level; s++)
        {
            var next = new List<int>();
            for (var f = 0; f < cages[s].Faces.Length; f++) for (var k = 0; k < cages[s].Faces[f].Length; k++) next.Add(ancestor[f]);
            ancestor = [.. next];
        }
        // The edges' middle points, level by level: the points of the body's level along an edge of the patch level.
        var edgeIndex = new List<Dictionary<long, int>>();
        for (var s = 0; s < cages.Count; s++)
        {
            var d = new Dictionary<long, int>();
            var es = cages[s].Edges();
            for (var e = 0; e < es.Count; e++) d[SubD.Key(es[e].A, es[e].B)] = e;
            edgeIndex.Add(d);
        }
        List<int> Along(int a, int b, int from, int to)
        {
            if (from == to) return [a, b];
            var mid = cages[from].Points.Length + edgeIndex[from][SubD.Key(a, b)];
            var left = Along(a, mid, from + 1, to);
            var right = Along(mid, b, from + 1, to);
            return [.. left, .. right.Skip(1)];
        }
        var faceOf = new Dictionary<(int, int), int>();
        for (var f = 0; f < cj.Faces.Length; f++)
        {
            var loop = cj.Faces[f];
            for (var k = 0; k < loop.Length; k++) faceOf[(loop[k], loop[(k + 1) % loop.Length])] = f;
        }
        var b = new ExactBuilder();
        // The patches' edges cut at the facets' points, by the body's level points they join.
        var pieces = new Dictionary<long, int>();
        var patches = 0;
        for (var f = 0; f < cj.Faces.Length; f++)
        {
            if (!regular[f]) continue;
            var net = Net(cj, f, faceOf);
            if (net == null) continue;
            var patch = BSplineSurface.UniformBicubic(net);
            var corners = cj.Faces[f];
            var loopEdges = new List<(int, bool)>();
            // Its sides, v0→v1 (v = 0), v1→v2 (u = 1), v2→v3 (v = 1, back), v3→v0 (u = 0, back).
            var sides = new (BSplineCurve Curve, int A, int B)[]
            {
                (patch.IsoV(0), corners[0], corners[1]),
                (patch.IsoU(1), corners[1], corners[2]),
                (patch.IsoV(1).Reversed(), corners[2], corners[3]),
                (patch.IsoU(0).Reversed(), corners[3], corners[0]),
            };
            foreach (var (curve, a, c) in sides)
            {
                var across = faceOf.TryGetValue((c, a), out var g) ? g : -1;
                if (across >= 0 && regular[across])
                {
                    loopEdges.Add(b.Edge(curve.At(curve.Start), curve.At(curve.End), new SplineCurve(curve)));
                    continue;
                }
                // Facets beside it: the side in as many pieces as they have there.
                var pts = Along(a, c, j, level);
                var n = pts.Count - 1;
                var rest = curve;
                for (var k = 0; k < n; k++)
                {
                    BSplineCurve piece;
                    if (k == n - 1) piece = rest;
                    else
                    {
                        var t = curve.Start + (curve.End - curve.Start) * (k + 1) / n;
                        (piece, rest) = rest.Split(t);
                    }
                    var e = b.Edge(lim[pts[k]], lim[pts[k + 1]], new SplineCurve(piece));
                    pieces[SubD.Key(pts[k], pts[k + 1])] = e.Edge;
                    loopEdges.Add(e);
                }
            }
            b.Face(new SplineSurface(patch), [loopEdges], patch.At(0.5, 0.5));
            patches++;
        }
        // The facets of the other faces, as the body has them: a quad whole where it is flat, else two triangles on its shorter diagonal.
        var facets = 0;
        (int, bool) EdgeOf(int x, int y)
        {
            // A piece of a patch's side: taken the way the facet runs.
            if (pieces.TryGetValue(SubD.Key(x, y), out var e)) return (e, b.StartOf(e) == b.Vertex(lim[x]));
            return b.Line(lim[x], lim[y]);
        }
        for (var f = 0; f < cl.Faces.Length; f++)
        {
            if (regular[ancestor[f]]) continue;
            var q = cl.Faces[f];
            if (Flat(lim[q[0]], lim[q[1]], lim[q[2]], lim[q[3]]))
            {
                b.PlaneFace([[EdgeOf(q[0], q[1]), EdgeOf(q[1], q[2]), EdgeOf(q[2], q[3]), EdgeOf(q[3], q[0])]]);
                facets++;
                continue;
            }
            var (t1, t2) = (lim[q[0]] - lim[q[2]]).Length <= (lim[q[1]] - lim[q[3]]).Length
                ? ((q[0], q[1], q[2]), (q[0], q[2], q[3]))
                : ((q[1], q[2], q[3]), (q[1], q[3], q[0]));
            foreach (var (x, y, z) in new[] { t1, t2 })
            {
                b.PlaneFace([[EdgeOf(x, y), EdgeOf(y, z), EdgeOf(z, x)]]);
                facets++;
            }
        }
        return new Result(b.Body(), patches, facets, j);
    }

    static bool Flat(Vec3 a, Vec3 b, Vec3 c, Vec3 d)
    {
        var n = VecMath.NewellNormal([a, b, c, d]);
        if (n.Length < 1e-18) return false;
        n = n.Normalized();
        var m = (a + b + c + d) * 0.25;
        const double tol = Tolerance.Planar * 0.1;
        return Math.Abs(Vec3.Dot(a - m, n)) <= tol && Math.Abs(Vec3.Dot(b - m, n)) <= tol && Math.Abs(Vec3.Dot(c - m, n)) <= tol && Math.Abs(Vec3.Dot(d - m, n)) <= tol;
    }

    /// <summary>
    /// Which faces of a closed cage are regular: quads whose four corners each have four quads round them, no edge of those nine faces
    /// creased. Such a face's limit surface is one uniform bicubic patch.
    /// </summary>
    public static bool[] Regular(SubDCage c)
    {
        var around = new List<int>[c.Points.Length];
        for (var i = 0; i < around.Length; i++) around[i] = [];
        for (var f = 0; f < c.Faces.Length; f++) foreach (var k in c.Faces[f]) around[k].Add(f);
        var creased = c.Creases.Where(x => x.Sharpness > 0).Select(x => SubD.Key(x.A, x.B)).ToHashSet();
        var o = new bool[c.Faces.Length];
        for (var f = 0; f < c.Faces.Length; f++)
        {
            var loop = c.Faces[f];
            if (loop.Length != 4) continue;
            var ok = true;
            var ring = new HashSet<int>();
            foreach (var k in loop)
            {
                if (around[k].Count != 4) { ok = false; break; }
                foreach (var g in around[k]) { if (c.Faces[g].Length != 4) ok = false; ring.Add(g); }
            }
            if (ok)
                foreach (var g in ring)
                {
                    var l = c.Faces[g];
                    for (var i = 0; i < 4 && ok; i++) if (creased.Contains(SubD.Key(l[i], l[(i + 1) % 4]))) ok = false;
                }
            o[f] = ok;
        }
        return o;
    }

    /// <summary>
    /// The 4 × 4 control net of a regular face (corners v0 v1 v2 v3 in its order): net[i, j], i along v0→v1, j along v0→v3, the face's own
    /// corners at [1..2, 1..2]; its neighbours across each side and at each corner round it.
    /// </summary>
    static Vec3[,]? Net(SubDCage c, int f, Dictionary<(int, int), int> faceOf)
    {
        var v = c.Faces[f];
        var idx = new int[4, 4];
        for (var a = 0; a < 4; a++) for (var b = 0; b < 4; b++) idx[a, b] = -1;
        (idx[1, 1], idx[2, 1], idx[2, 2], idx[1, 2]) = (v[0], v[1], v[2], v[3]);
        // Across a side a→b of the face: the face running b→a, and its two other corners, beyond a then beyond b.
        (int A, int B)? Beyond(int a, int b)
        {
            if (!faceOf.TryGetValue((b, a), out var g)) return null;
            var l = c.Faces[g];
            var i = Array.IndexOf(l, a);
            return (l[(i + 1) % 4], l[(i + 2) % 4]);
        }
        if (Beyond(v[0], v[1]) is not { } s0 || Beyond(v[1], v[2]) is not { } s1 || Beyond(v[2], v[3]) is not { } s2 || Beyond(v[3], v[0]) is not { } s3) return null;
        (idx[1, 0], idx[2, 0]) = (s0.A, s0.B);
        (idx[3, 1], idx[3, 2]) = (s1.A, s1.B);
        (idx[2, 3], idx[1, 3]) = (s2.A, s2.B);
        (idx[0, 2], idx[0, 1]) = (s3.A, s3.B);
        // A corner: the fourth point of the quad round the face's corner holding the two side points beside it.
        int Corner(int at, int p, int q)
        {
            // The quad running p→at→q→corner (or q→at→p, by which corner of the face it is).
            foreach (var (x, y) in new[] { (p, q), (q, p) })
            {
                if (!faceOf.TryGetValue((x, at), out var d)) continue;
                var l = c.Faces[d];
                var i = Array.IndexOf(l, at);
                if (l[(i + 1) % 4] == y) return l[(i + 2) % 4];
            }
            return -1;
        }
        idx[0, 0] = Corner(v[0], idx[1, 0], idx[0, 1]);
        idx[3, 0] = Corner(v[1], idx[2, 0], idx[3, 1]);
        idx[3, 3] = Corner(v[2], idx[3, 2], idx[2, 3]);
        idx[0, 3] = Corner(v[3], idx[1, 3], idx[0, 2]);
        var net = new Vec3[4, 4];
        for (var a = 0; a < 4; a++)
            for (var b = 0; b < 4; b++)
            {
                if (idx[a, b] < 0) return null;
                net[a, b] = c.Points[idx[a, b]];
            }
        return net;
    }
}
