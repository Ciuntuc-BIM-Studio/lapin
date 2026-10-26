using Lapin.Geometry;

namespace Lapin.Drafting.Cad;

/// <summary>
/// Open pieces (lines, open polylines) welded end to end into polylines: ends
/// within a tolerance meet; a chain stops where three or more pieces meet
/// (nothing is guessed there) and closes when it comes back to its start.
/// </summary>
public static class Joins
{
    public sealed record Chain(List<Vec2> Pts, bool Closed, List<int> Members);

    public static List<Chain> Chains(IReadOnlyList<IReadOnlyList<Vec2>> pieces, double tolerance)
    {
        var tol = Math.Max(tolerance, 1e-12);
        // Endpoints clustered into nodes: a grid of the tolerance, each new end checked against the cells around it.
        var cells = new Dictionary<(long, long), List<int>>();
        var nodes = new List<Vec2>();
        int Node(Vec2 p)
        {
            var (cx, cy) = ((long)Math.Floor(p.X / tol), (long)Math.Floor(p.Y / tol));
            for (var dx = -1L; dx <= 1; dx++)
                for (var dy = -1L; dy <= 1; dy++)
                    if (cells.TryGetValue((cx + dx, cy + dy), out var list))
                        foreach (var n in list) if ((nodes[n] - p).Length <= tol) return n;
            nodes.Add(p);
            var id = nodes.Count - 1;
            if (!cells.TryGetValue((cx, cy), out var mine)) cells[(cx, cy)] = mine = [];
            mine.Add(id);
            return id;
        }
        var ends = new (int A, int B)[pieces.Count];
        var incident = new Dictionary<int, List<int>>();
        for (var i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p.Count < 2) { ends[i] = (-1, -1); continue; }
            ends[i] = (Node(p[0]), Node(p[^1]));
            foreach (var n in new[] { ends[i].A, ends[i].B }.Distinct()) (incident.TryGetValue(n, out var l) ? l : incident[n] = []).Add(i);
        }
        var used = new bool[pieces.Count];
        var chains = new List<Chain>();

        // From a node, along a piece, through every node where exactly two pieces meet.
        Chain Follow(int piece, int from)
        {
            var pts = new List<Vec2>();
            var members = new List<int>();
            var node = from;
            var cur = piece;
            while (true)
            {
                used[cur] = true;
                members.Add(cur);
                var p = pieces[cur];
                var forward = ends[cur].A == node;
                var seq = forward ? p : p.Reverse().ToList();
                foreach (var q in seq) if (pts.Count == 0 || (pts[^1] - q).Length > tol) pts.Add(q);
                node = forward ? ends[cur].B : ends[cur].A;
                if (ends[cur].A == ends[cur].B) break; // a piece back on itself
                var next = incident[node];
                if (next.Count != 2) break;
                var other = next[0] == cur ? next[1] : next[0];
                if (used[other]) break;
                cur = other;
            }
            var closed = pts.Count >= 4 && (pts[0] - pts[^1]).Length <= tol;
            if (closed) pts.RemoveAt(pts.Count - 1);
            return new Chain(pts, closed, members);
        }

        // Open chains start at ends and junctions; what is left are rings.
        foreach (var (node, list) in incident.Where(kv => kv.Value.Count != 2).ToList())
            foreach (var piece in list) if (!used[piece]) chains.Add(Follow(piece, node));
        for (var i = 0; i < pieces.Count; i++)
            if (!used[i] && ends[i].A >= 0) chains.Add(Follow(i, ends[i].A));
        return chains;
    }
}
