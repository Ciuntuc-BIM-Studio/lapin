using BubbleBim.Geometry;

namespace Lapin.Ifc;

/// <summary>
/// IFC4's advanced B-rep: faces on surfaces (planes, cylinders, cones,
/// spheres, NURBS) bounded by loops of edges on curves (lines, circles,
/// NURBS). An edge is sampled the same way from both faces it bounds, so the
/// faces meet and the body closes into a solid. A plane is its polygon; a
/// surface of revolution is triangulated unrolled (angle, height), so a
/// cylinder's seam is just an edge; a NURBS face on its natural boundary is a
/// grid over its domain, else its boundary triangulated.
/// </summary>
public sealed partial class IfcModel
{
    MeshBuilder AdvancedFaces(IEnumerable<StepEntity> faces)
    {
        var m = new MeshBuilder();
        foreach (var f in faces)
        {
            var bounds = Items(f[0]);
            var surface = File.Get(f[1]);
            var sameSense = (f[2] as StepEnum)?.Value != "F";
            if (surface == null) { m.Append(Faces([f])); continue; }
            var loops = bounds.Select(b =>
            {
                var pts = Loop(File.Get(b[0]));
                if ((b[1] as StepEnum)?.Value == "F") pts.Reverse();
                return pts;
            }).Where(l => l.Count >= 2).ToList();
            var outer = bounds.FindIndex(b => b.Type == "IFCFACEOUTERBOUND");
            if (outer > 0) (loops[0], loops[outer]) = (loops[outer], loops[0]);
            var face = surface.Type switch
            {
                "IFCBSPLINESURFACEWITHKNOTS" or "IFCRATIONALBSPLINESURFACEWITHKNOTS" when loops.Count <= 1 => SplineGrid(surface),
                "IFCCYLINDRICALSURFACE" or "IFCCONICALSURFACE" or "IFCSPHERICALSURFACE" or "IFCTOROIDALSURFACE" or "IFCSURFACEOFREVOLUTION" => Unrolled(loops, Axis2(surface[0])),
                _ => Planar(loops),
            };
            if (!sameSense) face.T.ReverseInPlace();
            m.Append(face);
        }
        return m;
    }

    static MeshBuilder Planar(List<List<Vec3>> loops)
    {
        var m = new MeshBuilder();
        if (loops.Count == 0 || loops[0].Count < 3) return m;
        var idx = loops.Select(l => l.Select(m.Add).ToArray()).ToList();
        m.Polygon(idx[0], [.. idx.Skip(1)]);
        return m;
    }

    /// <summary>A point of a face unrolled: (angle × radius, height), its distance from the axis, and where it is when it is one of the face's own.</summary>
    readonly record struct Flat(double U, double V, double R, Vec3? At);

    /// <summary>
    /// A face on a surface turning about an axis: its loops unrolled to
    /// (angle × radius, height) — the angle carried on along each loop, so a
    /// seam is just an edge — then cut into strips 15° wide and each strip
    /// triangulated there: no triangle spans more of the turn than a strip, so
    /// none cuts through the body. A point a strip adds is put back on the
    /// surface at its angle, its distance from the axis taken along its edge.
    /// </summary>
    static MeshBuilder Unrolled(List<List<Vec3>> loops, Xf frame)
    {
        var m = new MeshBuilder();
        if (loops.Count == 0) return m;
        var radius = loops.SelectMany(l => l).Select(p => Radial(p, frame).Length).DefaultIfEmpty(1).Max();
        if (radius < 1e-9) return Planar(loops);
        var flat = loops.Select(l =>
        {
            double? last = null;
            return l.Select(p =>
            {
                var r = Radial(p, frame);
                var a = Math.Atan2(Vec3.Dot(r, frame.Y), Vec3.Dot(r, frame.X));
                if (r.Length < 1e-9 && last is { } keep) a = keep; // on the axis: no angle of its own
                if (last is { } prev) { while (a - prev > Math.PI) a -= 2 * Math.PI; while (a - prev < -Math.PI) a += 2 * Math.PI; }
                last = a;
                return new Flat(a * radius, Vec3.Dot(p - frame.O, frame.Z), r.Length, p);
            }).ToList();
        }).ToList();
        Vec3 Back(Flat f)
        {
            if (f.At is { } at) return at;
            var a = f.U / radius;
            return frame.O + frame.Z * f.V + (frame.X * Math.Cos(a) + frame.Y * Math.Sin(a)) * f.R;
        }
        double umin = flat.SelectMany(l => l).Min(f => f.U), umax = flat.SelectMany(l => l).Max(f => f.U);
        var step = radius * Math.PI / 12;
        for (var u0 = umin; u0 < umax - 1e-9; u0 += step)
        {
            var pieces = flat.Select(l => Clip(Clip(l, u0, true), Math.Min(u0 + step, umax), false)).ToList();
            if (pieces[0].Count < 3) continue;
            var plane = new List<Vec3>();
            var idx = pieces.Where(l => l.Count >= 3).Select(l => l.Select(f => { plane.Add(new Vec3(f.U, f.V, 0)); return m.Add(Back(f)); }).ToArray()).ToList();
            var at = m.V.Count - plane.Count; // this strip's points in the mesh
            foreach (var (a, b, c) in Triangulator.Face(plane, [.. idx[0].Select(i => i - at)], [.. idx.Skip(1).Select(h => h.Select(i => i - at).ToArray())], new Vec3(0, 0, 1)))
                m.T.Add((a + at, b + at, c + at));
        }
        return m;
    }

    /// <summary>A loop clipped to one side of the line U = <paramref name="u"/> (Sutherland–Hodgman): the side above it, or below.</summary>
    static List<Flat> Clip(List<Flat> loop, double u, bool above)
    {
        var o = new List<Flat>();
        bool In(Flat f) => above ? f.U >= u : f.U <= u;
        for (var i = 0; i < loop.Count; i++)
        {
            Flat a = loop[i], b = loop[(i + 1) % loop.Count];
            if (In(a)) o.Add(a);
            if (In(a) != In(b))
            {
                var t = (u - a.U) / (b.U - a.U);
                o.Add(new Flat(u, a.V + (b.V - a.V) * t, a.R + (b.R - a.R) * t, null));
            }
        }
        // A vertex on the line comes twice (kept, and met by its edge): once is enough.
        static bool Same(Flat x, Flat y) => Math.Abs(x.U - y.U) < 1e-9 && Math.Abs(x.V - y.V) < 1e-9;
        var clean = new List<Flat>();
        foreach (var f in o) if (clean.Count == 0 || !Same(clean[^1], f)) clean.Add(f);
        while (clean.Count > 1 && Same(clean[0], clean[^1])) clean.RemoveAt(clean.Count - 1);
        return clean;
    }

    static Vec3 Radial(Vec3 p, Xf frame) { var d = p - frame.O; return d - frame.Z * Vec3.Dot(d, frame.Z); }

    /// <summary>A loop as points: a polygon, or edges on curves joined end to start.</summary>
    List<Vec3> Loop(StepEntity? loop)
    {
        var o = new List<Vec3>();
        if (loop == null) return o;
        if (loop.Type == "IFCPOLYLOOP") { o.AddRange(Refs(loop[0]).Select(r => Point(new StepRef(r)))); return o; }
        if (loop.Type != "IFCEDGELOOP") return o;
        foreach (var oe in Items(loop[0]))
        {
            var edge = oe.Type == "IFCORIENTEDEDGE" ? File.Get(oe[2]) : oe;
            if (edge == null) continue;
            var pts = EdgePoints(edge);
            if (oe.Type == "IFCORIENTEDEDGE" && (oe[3] as StepEnum)?.Value == "F") pts.Reverse();
            o.AddRange(pts.Take(pts.Count - 1));
        }
        return o;
    }

    readonly Dictionary<int, List<Vec3>> _edges = [];

    /// <summary>An edge from its start vertex to its end, along its curve: the same points whichever face asks.</summary>
    List<Vec3> EdgePoints(StepEntity edge)
    {
        if (_edges.TryGetValue(edge.Id, out var known)) return [.. known];
        var a = Point(File.Get(edge[0])?[0]);
        var b = Point(File.Get(edge[1])?[0]);
        var sense = (edge[3] as StepEnum)?.Value != "F";
        var curve = File.Get(edge[2]);
        List<Vec3> pts;
        switch (curve?.Type)
        {
            case "IFCCIRCLE" or "IFCELLIPSE":
            {
                var xf = Axis2(curve[0]);
                double rx = Num(curve[1]) * _len, ry = curve.Type == "IFCELLIPSE" ? Num(curve[2]) * _len : rx;
                double At(Vec3 p) => Math.Atan2(Vec3.Dot(p - xf.O, xf.Y) / ry, Vec3.Dot(p - xf.O, xf.X) / rx);
                double t0 = At(a), t1 = At(b);
                // Along the curve (counter-clockwise about its axis) when the edge agrees with it, else against; a closed edge goes all round.
                var sweep = sense ? t1 - t0 : t0 - t1;
                while (sweep <= 1e-9) sweep += 2 * Math.PI;
                if (!sense) sweep = -sweep;
                var n = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 12)));
                pts = [.. Enumerable.Range(0, n + 1).Select(k => { var t = t0 + sweep * k / n; return xf.Apply(new Vec3(rx * Math.Cos(t), ry * Math.Sin(t), 0)); })];
                pts[0] = a; pts[^1] = b;
                break;
            }
            case "IFCBSPLINECURVEWITHKNOTS" or "IFCRATIONALBSPLINECURVEWITHKNOTS" or "IFCPOLYLINE" or "IFCINDEXEDPOLYCURVE" or "IFCCOMPOSITECURVE" or "IFCTRIMMEDCURVE":
            {
                pts = curve.Type.Contains("BSPLINE") ? SplineCurve(curve) : Curve(curve);
                if (pts.Count < 2) { pts = [a, b]; break; }
                if ((pts[0] - a).Length > (pts[^1] - a).Length) pts.Reverse();
                pts[0] = a; pts[^1] = b;
                break;
            }
            default: pts = [a, b]; break;
        }
        _edges[edge.Id] = pts;
        return [.. pts];
    }

    // ─── NURBS ───────────────────────────────────────────────────────────────

    static double[] KnotVector(object? knots, object? mults)
    {
        var k = knots is List<object?> kl ? kl.Select(Num).ToList() : [];
        var m = mults is List<object?> ml ? ml.Select(x => (int)Num(x)).ToList() : [];
        return [.. k.SelectMany((v, i) => Enumerable.Repeat(v, i < m.Count ? m[i] : 1))];
    }

    /// <summary>The non-zero basis functions at <paramref name="u"/> (de Boor), and the first index they belong to.</summary>
    static (int First, double[] N) Basis(double[] U, int p, int count, double u)
    {
        var span = Math.Clamp(Array.FindLastIndex(U, k => k <= u), p, count - 1);
        if (u >= U[count]) span = count - 1;
        var N = new double[p + 1];
        var left = new double[p + 1];
        var right = new double[p + 1];
        N[0] = 1;
        for (var j = 1; j <= p; j++)
        {
            left[j] = u - U[span + 1 - j];
            right[j] = U[span + j] - u;
            double saved = 0;
            for (var r = 0; r < j; r++)
            {
                var den = right[r + 1] + left[j - r];
                var t = den == 0 ? 0 : N[r] / den;
                N[r] = saved + right[r + 1] * t;
                saved = left[j - r] * t;
            }
            N[j] = saved;
        }
        return (span - p, N);
    }

    List<Vec3> SplineCurve(StepEntity c)
    {
        var p = (int)Num(c[0]);
        var ctrl = Refs(c[1]).Select(r => Point(new StepRef(r))).ToList();
        var U = KnotVector(c[6], c[5]);
        var w = c.Type.StartsWith("IFCRATIONAL", StringComparison.Ordinal) && c[8] is List<object?> wl ? wl.Select(Num).ToList() : null;
        if (ctrl.Count <= p || U.Length != ctrl.Count + p + 1) return ctrl;
        var steps = Math.Clamp(ctrl.Count * 4, 8, 96);
        double u0 = U[p], u1 = U[ctrl.Count];
        return [.. Enumerable.Range(0, steps + 1).Select(k =>
        {
            var (first, N) = Basis(U, p, ctrl.Count, u0 + (u1 - u0) * k / steps);
            Vec3 s = Vec3.Zero; double sw = 0;
            for (var i = 0; i <= p; i++) { var wi = w?[first + i] ?? 1; s += ctrl[first + i] * (N[i] * wi); sw += N[i] * wi; }
            return sw > 0 ? s * (1 / sw) : s;
        })];
    }

    /// <summary>A NURBS surface over its whole domain, as a grid of quads (finer with more control points).</summary>
    MeshBuilder SplineGrid(StepEntity s)
    {
        int pu = (int)Num(s[0]), pv = (int)Num(s[1]);
        var rows = s[2] is List<object?> l ? l.OfType<List<object?>>().Select(r => r.OfType<StepRef>().Select(x => Point(x)).ToList()).ToList() : [];
        var m = new MeshBuilder();
        if (rows.Count <= pu || rows[0].Count <= pv) return m;
        int nu = rows.Count, nv = rows[0].Count;
        double[] U = KnotVector(s[9], s[7]), V = KnotVector(s[10], s[8]);
        var w = s.Type.StartsWith("IFCRATIONAL", StringComparison.Ordinal) && s[12] is List<object?> wl
            ? wl.OfType<List<object?>>().Select(r => r.Select(Num).ToList()).ToList() : null;
        if (U.Length != nu + pu + 1 || V.Length != nv + pv + 1) return m;
        int su = Math.Clamp(nu * 4, 8, 48), sv = Math.Clamp(nv * 4, 8, 48);
        var grid = new int[su + 1, sv + 1];
        for (var i = 0; i <= su; i++)
        {
            var (fu, Nu) = Basis(U, pu, nu, U[pu] + (U[nu] - U[pu]) * i / su);
            for (var j = 0; j <= sv; j++)
            {
                var (fv, Nv) = Basis(V, pv, nv, V[pv] + (V[nv] - V[pv]) * j / sv);
                Vec3 p = Vec3.Zero; double sw = 0;
                for (var a = 0; a <= pu; a++)
                    for (var b = 0; b <= pv; b++)
                    {
                        var wt = Nu[a] * Nv[b] * (w?[fu + a][fv + b] ?? 1);
                        p += rows[fu + a][fv + b] * wt; sw += wt;
                    }
                grid[i, j] = m.Add(sw > 0 ? p * (1 / sw) : p);
            }
        }
        for (var i = 0; i < su; i++)
            for (var j = 0; j < sv; j++)
            {
                m.T.Add((grid[i, j], grid[i + 1, j], grid[i + 1, j + 1]));
                m.T.Add((grid[i, j], grid[i + 1, j + 1], grid[i, j + 1]));
            }
        return m;
    }
}

static class TriangleListExtensions
{
    /// <summary>Every triangle turned the other way.</summary>
    public static void ReverseInPlace(this List<(int A, int B, int C)> t) { for (var i = 0; i < t.Count; i++) t[i] = (t[i].A, t[i].C, t[i].B); }
}
