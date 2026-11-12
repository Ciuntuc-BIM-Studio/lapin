using Lapin.Geometry;

namespace Lapin.Analysis.Solver;

/// <summary>A line load along a side, from S0 to S1 (metres along the side from its start), W0 to W1 (per unit intensity: the area a metre of side takes, m²/m).</summary>
public readonly record struct Trapezoid(double S0, double S1, double W0, double W1)
{
    public double Integral => (W0 + W1) / 2 * (S1 - S0);
    /// <summary>Where its resultant acts (along the side).</summary>
    public double Centroid => W0 + W1 is var w && Math.Abs(w) > 1e-15 ? S0 + (S1 - S0) * (W0 + 2 * W1) / (3 * w) : (S0 + S1) / 2;
    public double At(double s) => S1 - S0 > 1e-15 ? W0 + (W1 - W0) * (s - S0) / (S1 - S0) : W0;
    /// <summary>The part of it between a and b.</summary>
    public Trapezoid? Clip(double a, double b)
    {
        var (lo, hi) = (Math.Max(a, S0), Math.Min(b, S1));
        return hi - lo > 1e-12 ? new Trapezoid(lo, hi, At(lo), At(hi)) : null;
    }
}

/// <summary>
/// The 45° rule (docs/plan-fem.md §9.2) on a convex piece of slab, in plan: the corners' bisectors share the piece among its
/// supported sides — each point goes to the supported side whose line is nearest, which for a convex piece is the straight
/// skeleton's face of that side (a rectangle's two trapezoids and two triangles) — so a side without support gives its share
/// to the others. A load over part of the piece gives each side what of it falls in that side's zone, as a line load along the
/// side (the zone's width square to it, piecewise linear). A piece with no supported side goes to its corners by the area
/// nearest each (their Voronoi cells).
/// </summary>
public static class Tributary
{
    /// <summary>Keeps the part of a ring where n·p ≤ c (Sutherland–Hodgman; its orientation kept).</summary>
    public static List<Vec2> Clip(IReadOnlyList<Vec2> ring, Vec2 n, double c)
    {
        var o = new List<Vec2>(ring.Count + 2);
        for (var i = 0; i < ring.Count; i++)
        {
            var (p, q) = (ring[i], ring[(i + 1) % ring.Count]);
            var (dp, dq) = (Vec2.Dot(n, p) - c, Vec2.Dot(n, q) - c);
            if (dp <= 0) o.Add(p);
            if ((dp < 0 && dq > 0) || (dp > 0 && dq < 0)) o.Add(p + (q - p) * (dp / (dp - dq)));
        }
        return o;
    }

    /// <summary>A ring cut to a convex anticlockwise polygon.</summary>
    public static List<Vec2> ClipTo(IReadOnlyList<Vec2> ring, IReadOnlyList<Vec2> convex)
    {
        var o = ring.ToList();
        for (var i = 0; i < convex.Count && o.Count >= 3; i++)
        {
            var (a, b) = (convex[i], convex[(i + 1) % convex.Count]);
            // A side shorter than a micrometre has no direction to cut by (two corners that nearly coincide).
            var d = b - a;
            var len = d.Length;
            if (len < 1e-6) continue;
            var n = new Vec2(d.Y / len, -d.X / len);
            o = Clip(o, n, Vec2.Dot(n, a));
        }
        return o.Count >= 3 ? o : [];
    }

    public static double Area(IEnumerable<IReadOnlyList<Vec2>> rings) => rings.Where(r => r.Count >= 3).Sum(r => Polygon2.SignedArea(r));

    /// <summary>The rings as the rule takes them: outsides anticlockwise, holes clockwise.</summary>
    public static List<IReadOnlyList<Vec2>> Oriented(IReadOnlyList<Vec2> outline, IEnumerable<IReadOnlyList<Vec2>> holes)
    {
        var o = new List<IReadOnlyList<Vec2>> { Polygon2.EnsureCcw(outline).ToList() };
        foreach (var h in holes.Where(h => h.Count >= 3)) o.Add(Polygon2.EnsureCcw(h).Reverse().ToList());
        return o;
    }

    /// <summary>The zone of each supported side of an anticlockwise convex piece (empty for a side without support).</summary>
    public static List<Vec2>[] Zones(IReadOnlyList<Vec2> piece, IReadOnlyList<bool> supported)
    {
        var n = piece.Count;
        var normals = new Vec2[n];
        for (var i = 0; i < n; i++) normals[i] = (piece[(i + 1) % n] - piece[i]).Normalized().Perp;
        var zones = new List<Vec2>[n];
        for (var e = 0; e < n; e++)
        {
            if (!supported[e]) { zones[e] = []; continue; }
            var z = piece.ToList();
            for (var f = 0; f < n && z.Count >= 3; f++)
            {
                if (f == e || !supported[f]) continue;
                // dist_e(p) ≤ dist_f(p): (n_e − n_f)·p ≤ n_e·a_e − n_f·a_f.
                var nn = normals[e] - normals[f];
                if (nn.Length < 1e-12) continue;
                z = Clip(z, nn, Vec2.Dot(normals[e], piece[e]) - Vec2.Dot(normals[f], piece[f]));
            }
            zones[e] = z.Count >= 3 ? z : [];
        }
        return zones;
    }

    /// <summary>The Voronoi cells of the given corners within the piece.</summary>
    public static List<Vec2>[] Cells(IReadOnlyList<Vec2> piece, IReadOnlyList<Vec2> corners)
    {
        var cells = new List<Vec2>[corners.Count];
        for (var i = 0; i < corners.Count; i++)
        {
            var z = piece.ToList();
            for (var j = 0; j < corners.Count && z.Count >= 3; j++)
            {
                if (j == i) continue;
                // |p − ci|² ≤ |p − cj|²: 2 (cj − ci)·p ≤ |cj|² − |ci|².
                var (ci, cj) = (corners[i], corners[j]);
                var nn = (cj - ci) * 2;
                if (nn.Length < 1e-12) continue;
                z = Clip(z, nn, Vec2.Dot(cj, cj) - Vec2.Dot(ci, ci));
            }
            cells[i] = z.Count >= 3 ? z : [];
        }
        return cells;
    }

    /// <summary>
    /// The width square to a side (from <paramref name="origin"/> along <paramref name="dir"/>, a unit vector) of the region
    /// the rings bound, as trapezoids between the points where it changes slope: its integral is the region's area.
    /// </summary>
    public static List<Trapezoid> Profile(IReadOnlyList<IReadOnlyList<Vec2>> rings, Vec2 origin, Vec2 dir)
    {
        var n = dir.Perp;
        var st = rings.Where(r => r.Count >= 3).Select(r => r.Select(p => new Vec2(Vec2.Dot(p - origin, dir), Vec2.Dot(p - origin, n))).ToList()).ToList();
        var ss = st.SelectMany(r => r.Select(p => p.X)).OrderBy(x => x).ToList();
        var breaks = new List<double>();
        foreach (var s in ss) if (breaks.Count == 0 || s - breaks[^1] > 1e-9) breaks.Add(s);
        double Chord(double s0)
        {
            var c = 0.0;
            foreach (var r in st)
                for (var i = 0; i < r.Count; i++)
                {
                    var (p, q) = (r[i], r[(i + 1) % r.Count]);
                    if ((p.X - s0) * (q.X - s0) >= 0) continue;
                    var t = p.Y + (q.Y - p.Y) * (s0 - p.X) / (q.X - p.X);
                    c -= Math.Sign(q.X - p.X) * t;
                }
            return c;
        }
        var o = new List<Trapezoid>();
        for (var k = 0; k + 1 < breaks.Count; k++)
        {
            var (a, b) = (breaks[k], breaks[k + 1]);
            var h = b - a;
            var (c1, c2) = (Chord(a + h / 3), Chord(a + 2 * h / 3));
            var (w0, w1) = (2 * c1 - c2, 2 * c2 - c1);
            if (Math.Abs(w0) + Math.Abs(w1) > 1e-12) o.Add(new Trapezoid(a, b, w0, w1));
        }
        return o;
    }
}
