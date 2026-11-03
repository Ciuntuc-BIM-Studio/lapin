using Lapin.Geometry;

namespace Lapin.Steel;

/// <summary>A cope read from a sketch: on the side of the band's <c>+Across</c> (else the other), its length back from the end along the axis, its depth across.</summary>
public sealed record SketchCope(bool Plus, double Length, double Depth);

/// <summary>
/// One end of a band: the point where its cut edge meets the midline, the cut edge's normal pointing out of the band
/// (2D, a unit vector), and its copes.
/// </summary>
public sealed record BandEnd(Vec2 At, Vec2 Normal, IReadOnlyList<SketchCope> Copes);

/// <summary>A band read from a closed outline: its midline from <see cref="Start"/> to <see cref="End"/>, its width, the unit vector <see cref="Across"/> it is measured along.</summary>
public sealed record Band(BandEnd Start, BandEnd End, double Width, Vec2 Across)
{
    public Vec2 Along => (End.At - Start.At).Normalized();
    public double Length => (End.At - Start.At).Length;
}

/// <summary>
/// A member read from a drafted outline (decision 268, Convert to member): a closed outline with two long edges parallel
/// within half a degree is a band — its midline the axis, the distance between them its width — and the edges between
/// them at each end its cut: one edge crossing the midline (its slant the cut's angle), with, before or after it, a step
/// back along the axis and in across (a cope: its length from the end, its depth from the long edge). Anything else is
/// not a band, and says why.
/// </summary>
public static class SketchBands
{
    /// <summary>How far from parallel two edges may be (degrees).</summary>
    public const double ParallelTolerance = 0.5;

    public static (Band? Band, string? Problem) Read(IReadOnlyList<Vec2> loop)
    {
        var pts = Clean(loop);
        if (pts.Count < 4) return (null, "The outline has fewer than four sides: it is not a band.");
        var n = pts.Count;
        Vec2 P(int i) => pts[((i % n) + n) % n];
        Vec2 D(int i) => (P(i + 1) - P(i)).Normalized();
        double Len(int i) => (P(i + 1) - P(i)).Length;
        var sinTol = Math.Sin(ParallelTolerance * Math.PI / 180);

        // The pair of edges running opposite ways, parallel, apart and overlapping, longest together.
        (int I, int J, double Score)? best = null;
        for (var i = 0; i < n; i++)
            for (var j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue;
                var (di, dj) = (D(i), D(j));
                if (Vec2.Dot(di, dj) > -0.9 || Math.Abs(Vec2.Cross(di, dj)) > sinTol) continue;
                var h = Math.Abs(Vec2.Cross(di, P(j) - P(i)));
                if (h < 1e-4) continue;
                // Both longer than the band is wide, and side by side.
                if (Len(i) <= h || Len(j) <= h) continue;
                double a0 = 0, a1 = Len(i), b0 = Vec2.Dot(P(j + 1) - P(i), di), b1 = Vec2.Dot(P(j) - P(i), di);
                if (Math.Min(a1, b1) - Math.Max(a0, b0) < h) continue;
                var score = Math.Min(Len(i), Len(j));
                if (best == null || score > best.Value.Score + 1e-9) best = (i, j, score);
            }
        if (best is not var (bi, bj, _)) return (null, "The outline has no two long parallel edges: it is not a band.");

        var along = D(bi);
        var across = along.Perp;
        var width = Vec2.Dot(P(bj) - P(bi), across);
        if (width < 0) { across = -across; width = -width; }
        // The midline: half the width over from the first long edge.
        var mid0 = P(bi) + across * (width / 2);
        double Off(Vec2 p) => Vec2.Dot(p - mid0, across);

        // At the far end of the first long edge (along +along): the edges from it to the second; at the near end, from the second back to it.
        var (end, endProblem) = End(Enumerable.Range(bi + 1, (bj - bi - 1 + n) % n).Select(k => k % n).ToList(), true);
        if (end == null) return (null, endProblem);
        var (start, startProblem) = End(Enumerable.Range(bj + 1, (bi - bj - 1 + n) % n).Select(k => k % n).ToList(), false);
        if (start == null) return (null, startProblem);
        if (Vec2.Dot(end.At - start.At, along) < width / 2) return (null, "The outline is wider than it is long: it is not a band.");
        return (new Band(start, end, width, across), null);

        // One end's edges, in the outline's order: the cut crossing the midline, a cope's two edges before it (from the long
        // edge on the -across side at the far end) or after it.
        (BandEnd? End, string? Problem) End(List<int> edges, bool far)
        {
            if (edges.Count == 0) return (null, "An end of the outline has no edge across it: it is not a band.");
            var crossing = edges.Where(k => Off(P(k)) * Off(P(k + 1)) <= 1e-12 && Math.Abs(Off(P(k)) - Off(P(k + 1))) > 1e-9).ToList();
            if (crossing.Count != 1) return (null, "An end of the outline is not one cut across the band.");
            var c = crossing[0];
            var (a, b) = (P(c), P(c + 1));
            var t = Off(a) / (Off(a) - Off(b));
            var at = a + (b - a) * t;
            var normal = (b - a).Normalized().Perp;
            var outward = far ? along : -along;
            if (Vec2.Dot(normal, outward) < 0) normal = -normal;
            if (Vec2.Dot(normal, outward) < Math.Sin(10 * Math.PI / 180)) return (null, "An end's cut runs nearly along the band.");
            var copes = new List<SketchCope>();
            var pos = edges.IndexOf(c);
            foreach (var side in new[] { edges.Take(pos).ToList(), edges.Skip(pos + 1).ToList() })
            {
                if (side.Count == 0) continue;
                if (side.Count != 2) return (null, "An end of the outline is neither a cut nor a cut with a cope.");
                // The edge along the band (the cope's floor), and the corner where the step turns back.
                var floor = side.FirstOrDefault(k => Math.Abs(Vec2.Cross(D(k), along)) <= sinTol);
                var step = side.FirstOrDefault(k => k != floor);
                if (side.Count(k => Math.Abs(Vec2.Cross(D(k), along)) <= sinTol) != 1 || Math.Abs(Vec2.Dot(D(step), along)) > Math.Sin(1 * Math.PI / 180))
                    return (null, "A notch at an end of the outline is not square: a cope steps in square and runs along the band.");
                var inner = side[0] == step ? P(step + 1) : P(floor + 1);
                var plus = Off(P(floor)) > 0;
                var depth = width / 2 - Math.Abs(Off(P(floor)));
                var length = Math.Abs(Vec2.Dot(inner - at, along));
                if (depth <= 1e-4 || length <= 1e-4) return (null, "A notch at an end of the outline has no size.");
                copes.Add(new SketchCope(plus, length, depth));
            }
            return (new BandEnd(at, normal, copes), null);
        }
    }

    /// <summary>The outline's corners without repeated points or points on a straight run.</summary>
    static List<Vec2> Clean(IReadOnlyList<Vec2> loop)
    {
        var pts = loop.ToList();
        if (pts.Count > 1 && (pts[0] - pts[^1]).Length < 1e-6) pts.RemoveAt(pts.Count - 1);
        for (var changed = true; changed && pts.Count >= 3;)
        {
            changed = false;
            for (var i = 0; i < pts.Count && pts.Count >= 3; i++)
            {
                var (a, b, c) = (pts[(i - 1 + pts.Count) % pts.Count], pts[i], pts[(i + 1) % pts.Count]);
                if ((b - a).Length < 1e-6 || Math.Abs(Vec2.Cross((b - a).Normalized(), (c - b).Normalized())) < 1e-7 && Vec2.Dot(b - a, c - b) > 0)
                {
                    pts.RemoveAt(i);
                    changed = true;
                    break;
                }
            }
        }
        return pts;
    }
}
