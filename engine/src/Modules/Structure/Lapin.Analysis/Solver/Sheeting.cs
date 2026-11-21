using Lapin.Geometry;

namespace Lapin.Analysis.Solver;

/// <summary>What the sheeting's hold came to (§14.15): the members it braces, the nodes held, those left as they were and why.</summary>
public sealed record SheetingSummary(int Members, int Nodes, int Spans, List<string> Skipped);

/// <summary>
/// The sheeting's lateral hold on a purlin or a wall rail (docs/plan-fem.md §14.15): the member stays straight along its local 3
/// (its section's width, in the sheeting's plane) between the nodes it rests on — its ends, a support, a member crossing it in
/// that plane (its axis within 30° of local 3: the rafter under a purlin, the column behind a rail) — relative to them, not to
/// the ground: at each node between two such, its displacement along local 3 is the chord's and its turn about local 2 the
/// chord's, as two penalty constraints (10⁴ × the member's largest EA/L, as the rigid diaphragm's, §11.2). The members are not
/// tied to one another: the sheeting is no diaphragm (no stressed skin), so the frames' own sway stays theirs.
/// </summary>
public static class Sheeting
{
    public const string LinkPrefix = "sheeting:";
    /// <summary>A member crossing within 30° of the braced one's local 3 is one it rests on.</summary>
    public static readonly double Across = Math.Cos(30 * Math.PI / 180);

    /// <summary>Holds each member (its pieces' indices) straight across; returns what it did.</summary>
    public static SheetingSummary Restrain(FrameProblem p, IReadOnlyList<(string Name, List<int> Pieces)> members)
    {
        var skipped = new List<string>();
        if (members.Count == 0) return new SheetingSummary(0, 0, 0, skipped);
        foreach (var e in p.Elements) e.Prepare(p.Nodes);
        var at = new Dictionary<int, List<int>>();
        for (var k = 0; k < p.Elements.Count; k++)
        {
            var e = p.Elements[k];
            foreach (var n in new[] { e.I, e.J })
            {
                if (!at.TryGetValue(n, out var l)) at[n] = l = [];
                l.Add(k);
            }
        }
        var (count, held, spans) = (0, 0, 0);
        foreach (var (name, pieces) in members)
        {
            if (pieces.Count == 0) continue;
            var e0 = p.Elements[pieces[0]];
            var (ex, e2, d) = (e0.Ex, e0.Ey, e0.Ez);
            if (pieces.Any(k => Math.Abs(Vec3.Dot(p.Elements[k].Ex, ex)) < 1 - 1e-6)) { skipped.Add($"{name}: not straight"); continue; }
            var own = pieces.ToHashSet();
            // The member's nodes along its axis, each with its piece's arm to the member's line.
            var nodes = new Dictionary<int, (double X, Vec3 Off)>();
            var origin = e0.Pi;
            foreach (var k in pieces)
            {
                var e = p.Elements[k];
                foreach (var (n, off) in new[] { (e.I, e.OffI), (e.J, e.OffJ) })
                    if (!nodes.ContainsKey(n)) nodes[n] = (Vec3.Dot(p.Nodes[n] + off - origin, ex), off);
            }
            var line = nodes.OrderBy(x => x.Value.X).Select(x => (Node: x.Key, x.Value.X, x.Value.Off)).ToList();
            if (line.Count < 3) continue;
            bool Rests(int n) => p.Held[n] != null
                || at[n].Any(k => !own.Contains(k) && Math.Abs(Vec3.Dot(p.Elements[k].Ex, d)) >= Across);
            var breaks = Enumerable.Range(0, line.Count).Where(i => i == 0 || i == line.Count - 1 || Rests(line[i].Node)).ToList();
            var alpha = 1e4 * pieces.Max(k => p.Elements[k].E * p.Elements[k].A / Math.Max(p.Elements[k].L, 1e-3));
            var made = 0;
            for (var b = 0; b + 1 < breaks.Count; b++)
            {
                var (ia, ib) = (breaks[b], breaks[b + 1]);
                if (ib - ia < 2) continue;
                var (a, z) = (line[ia], line[ib]);
                var l = z.X - a.X;
                if (l < 1e-6) continue;
                spans++;
                for (var i = ia + 1; i < ib; i++)
                {
                    var m = line[i];
                    var t = (m.X - a.X) / l;
                    // The point of a node's piece: u + θ × off, along d: d·u + θ·(off × d).
                    var c1 = new double[18];
                    var c2 = new double[18];
                    void Put(double[] c, int slot, Vec3 trans, Vec3 rot)
                    {
                        c[slot * 6] += trans.X; c[slot * 6 + 1] += trans.Y; c[slot * 6 + 2] += trans.Z;
                        c[slot * 6 + 3] += rot.X; c[slot * 6 + 4] += rot.Y; c[slot * 6 + 5] += rot.Z;
                    }
                    // Across: d·(u_m − (1 − t) u_a − t u_b) = 0.
                    Put(c1, 0, d * -(1 - t), Vec3.Cross(a.Off, d) * -(1 - t));
                    Put(c1, 1, d, Vec3.Cross(m.Off, d));
                    Put(c1, 2, d * -t, Vec3.Cross(z.Off, d) * -t);
                    // Its turn about local 2 the chord's: l·θ_m·e2 + d·(u_b − u_a) = 0 (w along local 3 is −θ₂·x).
                    Put(c2, 1, Vec3.Zero, e2 * l);
                    Put(c2, 2, d, Vec3.Cross(z.Off, d));
                    Put(c2, 0, -d, Vec3.Cross(a.Off, d) * -1);
                    var k = new double[18 * 18];
                    for (var r = 0; r < 18; r++)
                        for (var c = 0; c < 18; c++)
                            k[r * 18 + c] = alpha * (c1[r] * c1[c] + c2[r] * c2[c]);
                    p.Links.Add(new Link([a.Node, m.Node, z.Node], k, $"{LinkPrefix}{name}:{p.NodeIds[m.Node]}"));
                    made++;
                }
            }
            if (made > 0) { count++; held += made; }
        }
        return new SheetingSummary(count, held, spans, skipped);
    }
}

public sealed partial class StructuralProblem
{
    /// <summary>What the sheeting's hold came to (§14.15), when some bar says its sheeting braces it.</summary>
    public SheetingSummary? SheetingHold { get; private set; }

    /// <summary>The bars whose sheeting braces them (<see cref="SheetingSettings"/>), each its pieces, held straight across (§14.15).</summary>
    void Sheet(Lapin.Model.IModelView m)
    {
        var members = Problem.Elements.Select((e, k) => (e, k)).Where(x => x.e.SourceId != null).GroupBy(x => x.e.SourceId!)
            .Where(g => SheetingSettings.Of(m.Get(g.Key))).Select(g => (g.First().e.Name ?? g.Key, g.Select(x => x.k).ToList())).ToList();
        if (members.Count == 0) return;
        var s = SheetingHold = Sheeting.Restrain(Problem, members);
        Notes.Add($"{s.Members} member{(s.Members == 1 ? "" : "s")} braced by {(s.Members == 1 ? "its" : "their")} sheeting: held straight across in the sheeting's plane (along {(s.Members == 1 ? "its" : "their")} section's width) between the members {(s.Members == 1 ? "it rests" : "they rest")} on, at {s.Nodes} node{(s.Nodes == 1 ? "" : "s")}; the sheeting ties no member to another (no stressed skin). The members' own buckling checks keep their lengths.");
        if (s.Skipped.Count > 0) Notes.Add($"Sheeting left without effect on {string.Join(", ", s.Skipped.Take(6))}{(s.Skipped.Count > 6 ? "…" : "")}.");
    }
}
