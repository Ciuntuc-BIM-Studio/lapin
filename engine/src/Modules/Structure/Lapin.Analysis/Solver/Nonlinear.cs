namespace Lapin.Analysis.Solver;

/// <summary>What the nonlinear solve of one load vector takes (§14.1): the geometric stiffness of the bars' axial forces (P-Δ) or not.</summary>
public sealed record NonlinearOptions(bool SecondOrder = false, int MaxIterations = 30, double Tolerance = 1e-4);

/// <summary>
/// One load vector solved nonlinearly (§14.1): its result (on the problem's own bars, shells and nodes), how many iterations,
/// whether it settled, the one-way members taken out (slack) and the supports lifted at the end, the second-order
/// amplification (the largest horizontal displacement over the first-order one), or why it could not be solved.
/// </summary>
public sealed class NonlinearResult
{
    public bool Ok { get; init; }
    public CaseResult? Result { get; init; }
    public CaseResult? FirstOrder { get; init; }
    public int Iterations { get; init; }
    public bool Converged { get; init; }
    public List<int> Slack { get; init; } = [];
    public List<int> Lifted { get; init; } = [];
    public double Amplification { get; init; } = 1;
    public string? Message { get; init; }
    public Mechanism? Mechanism { get; init; }
    /// <summary>The factor lost its pivot with the geometric stiffness only: the loads are past the critical ones (αcr &lt; 1).</summary>
    public bool Unstable { get; init; }
    /// <summary>The bars' axial forces at the end (kN, tension positive; a slack one nought).</summary>
    public double[] Axial { get; init; } = [];
    /// <summary>The problem in its last states, without the geometric stiffness: what buckling is worked out on (§14.3).</summary>
    public FrameProblem? States { get; init; }
}

/// <summary>
/// The nonlinear solve of S5 (docs/plan-fem.md §14.1): a modified Newton–Raphson on states (Bathe, "Finite Element
/// Procedures", §8.4; McGuire, Gallagher and Ziemian, "Matrix Structural Analysis", ch. 9–10). Each iteration assembles and
/// factors the problem as the last result left it — the one-way members (<see cref="FrameElement.Only"/>) in or out, the
/// supports that work in compression only (<see cref="FrameProblem.Uplift"/>) seated or lifted, and with P-Δ the bars'
/// geometric stiffness of their axial forces — and solves it again, until no state changes and the axial forces settle.
/// A factor that loses a pivot is a mechanism, or with the geometric stiffness an instability. Written for Lapin.
/// </summary>
public static class Nonlinear
{
    /// <summary>A bar's axial force from its end forces (kN, tension positive): the mean of its two ends'.</summary>
    public static double AxialOf(ElementResult r) => (r.Forces[6] - r.Forces[0]) / 2;

    /// <summary>Whether a problem has anything the nonlinear solve would change: a one-way member or a support that lifts.</summary>
    public static bool HasStates(FrameProblem p) => p.Uplift.Count > 0 || p.Elements.Any(e => e.Only != null);

    /// <summary>The problem in the given states: its lifted supports free, their springs off; its bars out or with their axial forces.</summary>
    public static FrameProblem InStates(FrameProblem p, bool[] active, IReadOnlySet<int> lifted, double[]? axial)
    {
        var q = new FrameProblem();
        for (var n = 0; n < p.Nodes.Count; n++) q.AddNode(p.Nodes[n], p.NodeIds[n], lifted.Contains(n) ? null : p.Held[n]);
        for (var k = 0; k < p.Elements.Count; k++)
        {
            var e = p.Elements[k];
            var n = active[k] && axial != null ? axial[k] : 0;
            q.Elements.Add(active[k] && n == 0 ? e : e.With(n, !active[k]));
        }
        q.Shells.AddRange(p.Shells);
        q.Links.AddRange(p.Links.Where(l => !(l.Ground && l.Nodes.Any(lifted.Contains))));
        return q;
    }

    public static NonlinearResult Solve(FrameProblem p, CaseLoads loads, NonlinearOptions o)
    {
        var ne = p.Elements.Count;
        var active = Enumerable.Repeat(true, ne).ToArray();
        var lifted = new HashSet<int>();
        double[]? axial = null;
        CaseResult? first = null, last = null;
        var solvedActive = (bool[])active.Clone();
        var solvedLifted = new HashSet<int>();
        var seen = new HashSet<string>();
        var groups = Enumerable.Range(0, ne).Where(k => p.Elements[k].Only != null).GroupBy(k => p.Elements[k].Member ?? p.Elements[k].Id).Select(g => g.ToList()).ToList();
        string Key() => string.Concat(active.Select(a => a ? '1' : '0')) + "|" + string.Join(",", lifted.Order());
        seen.Add(Key());
        var loadScale = Math.Max(1, loads.Sum.Length + loads.Surface.Sum(s => s.Q.Length));
        for (var it = 1; it <= o.MaxIterations; it++)
        {
            var withKg = o.SecondOrder && axial != null;
            var q = InStates(p, active, lifted, withKg ? axial : null);
            var (sys, fail) = FrameSystem.Build(q);
            if (sys == null)
            {
                if (fail?.Mechanism == null) return new NonlinearResult { Ok = false, Iterations = it, Message = fail?.Refused ?? "The problem could not be assembled." };
                // Unstable when the same states without the axial forces stand.
                var unstable = withKg && FrameSystem.Build(InStates(p, active, lifted, null)).System != null;
                var slackNow = Enumerable.Range(0, ne).Where(k => !active[k]).ToList();
                return new NonlinearResult
                {
                    Ok = false, Iterations = it, Mechanism = fail.Mechanism, Unstable = unstable, Slack = slackNow, Lifted = [.. lifted.Order()],
                    Message = unstable
                        ? "Second order: unstable, the loads are past the critical ones (αcr < 1)."
                        : slackNow.Count > 0 || lifted.Count > 0
                            ? $"A mechanism once {Said(slackNow.Count, "member")} went slack{(lifted.Count > 0 ? $" and {Said(lifted.Count, "support")} lifted" : "")}: nothing holds node {q.NodeIds[fail.Mechanism.Node]} ({FrameSolver.DofName(fail.Mechanism.Dof)})."
                            : $"A mechanism at node {q.NodeIds[fail.Mechanism.Node]} ({FrameSolver.DofName(fail.Mechanism.Dof)}).",
                };
            }
            var r = sys.SolveCase(loads);
            first ??= r;
            var n = new double[ne];
            for (var k = 0; k < ne; k++) n[k] = active[k] ? AxialOf(r.Elements[k]) : 0;
            var maxN = n.Length == 0 ? 0 : n.Max(Math.Abs);
            var tolN = 1e-6 * maxN + 1e-9;
            var changed = false;
            solvedActive = (bool[])active.Clone();
            solvedLifted = [.. lifted];
            // A one-way member goes in or out whole (all its pieces): by its pieces' mean axial force, their whole stretch.
            foreach (var g in groups)
            {
                var e0 = q.Elements[g[0]];
                var tension = e0.Only == "tension";
                if (active[g[0]])
                {
                    var mean = g.Average(k => n[k]);
                    if (tension ? mean < -tolN : mean > tolN) { foreach (var k in g) active[k] = false; changed = true; }
                    continue;
                }
                var stretch = 0.0;
                foreach (var k in g)
                {
                    var e = q.Elements[k];
                    var un = new double[12];
                    Array.Copy(r.U, e.I * 6, un, 0, 6);
                    Array.Copy(r.U, e.J * 6, un, 6, 6);
                    var ul = e.ToLocal(un);
                    stretch += ul[6] - ul[0];
                }
                if (tension ? stretch > 1e-9 : stretch < -1e-9) { foreach (var k in g) active[k] = true; changed = true; }
            }
            foreach (var node in p.Uplift)
            {
                if (!lifted.Contains(node))
                {
                    var rz = r.Reactions.TryGetValue(node, out var rr) ? rr[2] : 0;
                    if (rz < -1e-9 * loadScale) { lifted.Add(node); changed = true; }
                }
                else if (r.U[node * 6 + 2] < -1e-9) { lifted.Remove(node); changed = true; }
            }
            var settled = !o.SecondOrder || (withKg && axial!.Zip(n).All(x => Math.Abs(x.First - x.Second) <= o.Tolerance * maxN + 1e-6));
            axial = n;
            last = r;
            if (!changed && settled)
                return Done(it, true, null);
            if (changed && !seen.Add(Key()))
                return Done(it, false, "The one-way members and supports did not settle: they go back to states already tried.");
        }
        return Done(o.MaxIterations, false, $"Not settled in {o.MaxIterations} iterations.");

        NonlinearResult Done(int it, bool converged, string? message)
        {
            // The last result is in the states it was solved in: say those (a change found after it is not taken).
            var slack = Enumerable.Range(0, ne).Where(k => !solvedActive[k]).ToList();
            return new NonlinearResult
            {
                Ok = true, Result = last, FirstOrder = first, Iterations = it, Converged = converged, Message = message,
                Slack = slack, Lifted = [.. solvedLifted.Order()], Amplification = Amplification(first!, last!), Axial = axial ?? new double[ne],
                States = InStates(p, solvedActive, solvedLifted, null),
            };
        }
    }

    static string Said(int n, string what) => $"{n} {what}{(n == 1 ? "" : "s")}";

    /// <summary>The largest horizontal displacement of a result over the first-order one's (1 when nothing moves sideways).</summary>
    static double Amplification(CaseResult first, CaseResult last)
    {
        double Max(double[] u)
        {
            var m = 0.0;
            for (var i = 0; i + 1 < u.Length; i += 6) m = Math.Max(m, Math.Sqrt(u[i] * u[i] + u[i + 1] * u[i + 1]));
            return m;
        }
        // Nothing to speak of moves sideways (under 0.1 mm): no sway to amplify.
        var a = Max(first.U);
        return a > 1e-4 ? Max(last.U) / a : 1;
    }
}
