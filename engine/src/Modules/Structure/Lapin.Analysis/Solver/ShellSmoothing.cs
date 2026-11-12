using Lapin.Geometry;

namespace Lapin.Analysis.Solver;

/// <summary>
/// Shell results at the nodes (docs/plan-fem.md §10.5), per surface (<see cref="ShellElement.Group"/>): the elements of one
/// plane surface share their axes (a slab's x along global X, a wall's along its length, unless <see cref="ShellElement.Axis"/>
/// says otherwise), so their values combine as they are; where surfaces meet at an angle each keeps its own value at the
/// shared node.
/// <para>
/// <see cref="AtNodes"/> averages the elements' corner values. <see cref="Recovered"/> is Zienkiewicz and Zhu's superconvergent
/// patch recovery ("The superconvergent patch recovery and a posteriori error estimates", IJNME 33, 1992): over the elements
/// round a node, a linear field fitted by least squares to their centres' values, read at the node; a node on an edge
/// or a corner, whose own patch is too thin, takes the mean of its neighbours' patches read at it. It follows a steep gradient
/// (a clamped edge's moment) where the corner average lags by half an element.
/// </para>
/// </summary>
public static class ShellSmoothing
{
    public static Dictionary<(string Group, int Node), ShellForces> AtNodes(FrameProblem p, CaseResult r) => AtNodes(p.Shells, r.Shells);

    public static Dictionary<(string Group, int Node), ShellForces> AtNodes(IReadOnlyList<ShellElement> shells, IReadOnlyList<ShellResult> results)
    {
        var sum = new Dictionary<(string, int), (ShellForces F, int N)>();
        for (var k = 0; k < shells.Count; k++)
        {
            var e = shells[k];
            var g = e.Group ?? "";
            for (var i = 0; i < e.Count; i++)
            {
                var key = (g, e.Nodes[i]);
                sum[key] = sum.TryGetValue(key, out var had) ? (had.F + results[k].Corners[i], had.N + 1) : (results[k].Corners[i], 1);
            }
        }
        return sum.ToDictionary(kv => kv.Key, kv => kv.Value.F * (1.0 / kv.Value.N));
    }

    public static Dictionary<(string Group, int Node), ShellForces> Recovered(FrameProblem p, CaseResult r) => Recovered(p.Nodes, p.Shells, r.Shells);

    public static Dictionary<(string Group, int Node), ShellForces> Recovered(IReadOnlyList<Vec3> nodes, IReadOnlyList<ShellElement> shells, IReadOnlyList<ShellResult> results)
    {
        // Each element's sampling point: its centre, where a bilinear element's resultants are most accurate (Barlow,
        // "Optimal stress locations in finite element models", IJNME 10, 1976); MITC4's Mx is constant across an element in x,
        // so its Gauss points would read a step.
        var samples = new (Vec3 At, ShellForces F)[shells.Count][];
        var around = new Dictionary<(string, int), List<int>>();
        for (var k = 0; k < shells.Count; k++)
        {
            var e = shells[k];
            samples[k] = [(e.Nodes.Aggregate(Vec3.Zero, (acc, i) => acc + nodes[i]) * (1.0 / e.Count), results[k].Centre)];
            foreach (var i in e.Nodes)
            {
                var key = (e.Group ?? "", i);
                if (!around.TryGetValue(key, out var l)) around[key] = l = [];
                l.Add(k);
            }
        }

        // A node's patch fitted: a + b·x + c·y in its first element's axes, from the node; null when too thin to fit.
        var fits = new Dictionary<(string, int), (Vec3 Origin, Vec3 Ex, Vec3 Ey, double[,] Coef)?>();
        (Vec3, Vec3, Vec3, double[,])? Fit((string, int) key)
        {
            if (fits.TryGetValue(key, out var had)) return had;
            var elems = around[key];
            var origin = nodes[key.Item2];
            var (ex, ey) = (shells[elems[0]].Ex, shells[elems[0]].Ey);
            var pts = elems.SelectMany(k => samples[k]).ToList();
            // A patch round an inner node has elements on every side; an edge's (two quadrilaterals) or a corner's does not.
            (Vec3, Vec3, Vec3, double[,])? result = null;
            if (pts.Count >= 3 && Spread(pts.Select(q => q.At).ToList(), origin, ex, ey))
            {
                var ata = new double[3, 3];
                var atb = new double[3, 8];
                foreach (var (at, f) in pts)
                {
                    var d = at - origin;
                    double[] row = [1, Vec3.Dot(d, ex), Vec3.Dot(d, ey)];
                    for (var i = 0; i < 3; i++)
                    {
                        for (var j = 0; j < 3; j++) ata[i, j] += row[i] * row[j];
                        for (var q = 0; q < 8; q++) atb[i, q] += row[i] * f[q];
                    }
                }
                if (Solve3(ata, atb)) result = (origin, ex, ey, atb);
            }
            fits[key] = result;
            return result;
        }

        var o = new Dictionary<(string Group, int Node), ShellForces>();
        foreach (var key in around.Keys)
        {
            if (Fit(key) is { } own) { o[key] = Eval(own, nodes[key.Item2]); continue; }
            var sum = default(ShellForces);
            var n = 0;
            foreach (var w in around[key].SelectMany(k => shells[k].Nodes).Distinct())
            {
                if (w == key.Item2 || Fit((key.Item1, w)) is not { } fit) continue;
                sum += Eval(fit, nodes[key.Item2]);
                n++;
            }
            if (n > 0) { o[key] = sum * (1.0 / n); continue; }
            // Nothing to fit (a lone element): the corner mean.
            var mean = default(ShellForces);
            var m = 0;
            foreach (var k in around[key])
            {
                var i = Array.IndexOf(shells[k].Nodes, key.Item2);
                mean += results[k].Corners[i];
                m++;
            }
            o[key] = mean * (1.0 / m);
        }
        return o;

        static ShellForces Eval((Vec3 Origin, Vec3 Ex, Vec3 Ey, double[,] C) fit, Vec3 at)
        {
            var d = at - fit.Origin;
            var (x, y) = (Vec3.Dot(d, fit.Ex), Vec3.Dot(d, fit.Ey));
            var v = new double[8];
            for (var q = 0; q < 8; q++) v[q] = fit.C[0, q] + fit.C[1, q] * x + fit.C[2, q] * y;
            return new ShellForces(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7]);
        }
    }

    /// <summary>Whether the points lie on every side of the node (each quadrant of its plane holds one), so a fitted plane reads it by interpolation.</summary>
    static bool Spread(List<Vec3> pts, Vec3 origin, Vec3 ex, Vec3 ey)
    {
        var (px, nx, py, ny) = (false, false, false, false);
        foreach (var q in pts)
        {
            var d = q - origin;
            var (x, y) = (Vec3.Dot(d, ex), Vec3.Dot(d, ey));
            px |= x > 1e-9; nx |= x < -1e-9; py |= y > 1e-9; ny |= y < -1e-9;
        }
        return px && nx && py && ny;
    }

    /// <summary>Solves the 3 × 3 normal equations for eight right-hand sides in place (Gaussian elimination, partial pivoting); false when singular.</summary>
    static bool Solve3(double[,] a, double[,] b)
    {
        var scale = Math.Abs(a[0, 0]) + Math.Abs(a[1, 1]) + Math.Abs(a[2, 2]);
        for (var c = 0; c < 3; c++)
        {
            var piv = c;
            for (var r = c + 1; r < 3; r++) if (Math.Abs(a[r, c]) > Math.Abs(a[piv, c])) piv = r;
            if (Math.Abs(a[piv, c]) < 1e-12 * scale) return false;
            if (piv != c)
            {
                for (var x = 0; x < 3; x++) (a[c, x], a[piv, x]) = (a[piv, x], a[c, x]);
                for (var x = 0; x < 8; x++) (b[c, x], b[piv, x]) = (b[piv, x], b[c, x]);
            }
            for (var r = 0; r < 3; r++)
            {
                if (r == c) continue;
                var f = a[r, c] / a[c, c];
                for (var x = 0; x < 3; x++) a[r, x] -= f * a[c, x];
                for (var x = 0; x < 8; x++) b[r, x] -= f * b[c, x];
            }
        }
        for (var r = 0; r < 3; r++) for (var x = 0; x < 8; x++) b[r, x] /= a[r, r];
        return true;
    }
}
