using Lapin.Geometry;

namespace Lapin.Platform;

/// <summary>
/// A closed outline with arcs (<see cref="ArcOutline"/>: its corners, each edge's rise) cut by a line in two (audit 2026-11-15, §6:
/// a floor split, a ceiling trimmed by a line). The line runs on past the two points that give it; it must cross the outline
/// twice, in and out, so each side is one outline. An arc it crosses is cut into two arcs of the same circle; the cut's edge is
/// straight. Each piece keeps the outline's way round.
/// </summary>
public static class OutlineCuts
{
    const double Eps = 1e-7;

    public sealed record Piece(List<Vec2> Corners, double[]? Rises);

    /// <summary>The piece left of the line from <paramref name="a"/> to <paramref name="b"/>, and the piece right of it.</summary>
    public static (Piece Left, Piece Right) Split(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, Vec2 a, Vec2 b)
    {
        var n = corners.Count;
        if (n < 3) throw new CommandException("The element has no outline.");
        if ((b - a).Length < 1e-9) throw new CommandException("The line has no length: give it two points apart.");
        var dir = (b - a).Normalized();
        double Side(Vec2 p) => Vec2.Cross(dir, p - a);
        PathCurve Edge(int i) => new(corners[i], corners[(i + 1) % n], rises == null ? 0 : rises[i]);
        int Sign(double d) => d > Eps ? 1 : d < -Eps ? -1 : 0;
        var edgeSide = Enumerable.Range(0, n).Select(i => { var e = Edge(i); return Sign(Side(e.PointAt(e.Length / 2))); }).ToArray();

        // The nodes round the outline, the crossings among them: each node's edge runs to the next.
        var nodes = new List<(Vec2 P, double Rise, bool Cut)>();
        var line = PathEdits.Curve.Line(a, b);
        for (var i = 0; i < n; i++)
        {
            var e = Edge(i);
            var d = Sign(Side(corners[i]));
            var (before, after) = (edgeSide[(i + n - 1) % n], edgeSide[i]);
            if (after == 0 && !e.IsArc && Sign(Side(e.End)) == 0) throw new CommandException("The line runs along an edge of the outline: move it off the edge.");
            // A corner on the line is a crossing where the outline goes from one side to the other there.
            var cutHere = d == 0 && before != 0 && after != 0 && before != after;
            var length = e.Length;
            var inner = PathEdits.Cross(e.IsArc ? PathEdits.Curve.Of(e) : PathEdits.Curve.Line(e.Start, e.End), line)
                .Select(p => e.IsArc ? PathEdits.Along(e, p) : Vec2.Dot(p - e.Start, (e.End - e.Start).Normalized()))
                .Where(s => s > 1e-6 && s < length - 1e-6)
                // A crossing, not a touch: the edge goes from one side to the other there.
                .Where(s => Sign(Side(e.PointAt(Math.Max(0, s - 1e-4)))) * Sign(Side(e.PointAt(Math.Min(length, s + 1e-4)))) < 0)
                .OrderBy(s => s).ToList();
            var stations = new List<double> { 0 };
            stations.AddRange(inner);
            stations.Add(length);
            for (var k = 0; k < stations.Count - 1; k++)
            {
                var (s0, s1) = (stations[k], stations[k + 1]);
                var (p, q) = (e.PointAt(s0), e.PointAt(s1));
                var rise = e.IsArc ? PathCurve.RiseThrough(p, q, e.PointAt((s0 + s1) / 2)) : 0;
                nodes.Add((p, rise, k == 0 ? cutHere : true));
            }
        }
        var cuts = Enumerable.Range(0, nodes.Count).Where(i => nodes[i].Cut).ToList();
        if (cuts.Count == 0) throw new CommandException("The line does not cross the outline.");
        if (cuts.Count != 2) throw new CommandException($"The line crosses the outline {cuts.Count} times: it must go in once and come out once. Split it where it crosses twice.");

        Piece Walk(int from, int to)
        {
            var c = new List<Vec2>();
            var r = new List<double>();
            for (var i = from; ; i = (i + 1) % nodes.Count)
            {
                c.Add(nodes[i].P);
                // The cut's own edge, back to where the piece began, is straight.
                r.Add(i == to ? 0 : nodes[i].Rise);
                if (i == to) break;
            }
            return new Piece(c, r.Any(x => Math.Abs(x) > 1e-9) ? [.. r] : null);
        }
        var (one, two) = (Walk(cuts[0], cuts[1]), Walk(cuts[1], cuts[0]));
        foreach (var p in new[] { one, two })
            if (p.Corners.Count < 3 && (p.Rises == null || p.Corners.Count < 2) || Math.Abs(ArcOutline.SignedArea(p.Corners, p.Rises)) < 1e-6)
                throw new CommandException("The line only touches the outline: a part would have no area.");
        // Which side each lies on: its first edge's middle.
        var firstEdge = new PathCurve(one.Corners[0], one.Corners[1 % one.Corners.Count], one.Rises?[0] ?? 0);
        return Side(firstEdge.PointAt(firstEdge.Length / 2)) > 0 ? (one, two) : (two, one);
    }
}
