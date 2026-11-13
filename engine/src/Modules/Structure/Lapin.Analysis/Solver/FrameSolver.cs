using System.Diagnostics;
using Lapin.Geometry;

namespace Lapin.Analysis.Solver;

/// <summary>One load case for the solver: forces on nodes (kN, global) and loads on the bars' spans.</summary>
public sealed class CaseLoads(string id)
{
    public string Id { get; } = id;
    public Dictionary<int, Vec3> Nodal { get; } = [];
    public List<(int Element, SpanLoad Load)> Span { get; } = [];

    /// <summary>Moments on nodes (kN·m, global).</summary>
    public Dictionary<int, Vec3> Moments { get; } = [];

    /// <summary>Uniform loads on shells (kN/m², a global vector: self-weight, a pressure times the normal), each over its whole element.</summary>
    public List<(int Shell, Vec3 Q)> Surface { get; } = [];

    public void AddNodal(int node, Vec3 f) => Nodal[node] = Nodal.TryGetValue(node, out var had) ? had + f : f;
    public void AddMoment(int node, Vec3 m) => Moments[node] = Moments.TryGetValue(node, out var had) ? had + m : m;

    /// <summary>The case's whole force on nodes and bars (kN, global); the shells' loads the solver adds, by their areas.</summary>
    public Vec3 Sum => Nodal.Values.Aggregate(Vec3.Zero, (a, b) => a + b) + Span.Aggregate(Vec3.Zero, (a, s) => a + s.Load.Total);
}

/// <summary>A linear static problem on bars: nodes (metres), each node's held degrees of freedom (global [ux, uy, uz, rx, ry, rz]; null: free), the bars, the cases.</summary>
public sealed class FrameProblem
{
    public List<Vec3> Nodes { get; } = [];
    public List<string> NodeIds { get; } = [];
    public List<bool[]?> Held { get; } = [];
    public List<FrameElement> Elements { get; } = [];
    /// <summary>Shell elements (§10): 3 or 4 nodes each, sharing the bars' nodes where they meet.</summary>
    public List<ShellElement> Shells { get; } = [];
    public List<CaseLoads> Cases { get; } = [];

    public int AddNode(Vec3 at, string id, bool[]? held = null)
    {
        Nodes.Add(at);
        NodeIds.Add(id);
        Held.Add(held);
        return Nodes.Count - 1;
    }
}

/// <summary>Where the solver met a mechanism: the node, its degree of freedom (global ux…rz), the bars joined to it.</summary>
public sealed record Mechanism(int Node, int Dof, IReadOnlyList<int> Elements)
{
    /// <summary>The nodes the mechanism moves most (its shape: a unit push where the pivot was lost, the model otherwise as it is), and the bars at them.</summary>
    public IReadOnlyList<int> Moving { get; init; } = [];
    public IReadOnlyList<int> MovingElements { get; init; } = [];
    /// <summary>The shells at the node, and at the nodes that move.</summary>
    public IReadOnlyList<int> Shells { get; init; } = [];
    public IReadOnlyList<int> MovingShells { get; init; } = [];
}

/// <summary>A shell's result in one case or combination: its nodes' displacements in its own axes (6 a node), its resultants at its centre and corners (<see cref="ShellForces"/>).</summary>
public sealed record ShellResult(double[] ULocal, ShellForces Centre, ShellForces[] Corners)
{
    /// <summary>Σ factor × result: a combination by superposition.</summary>
    public static ShellResult Combine(IEnumerable<(ShellResult R, double Factor)> terms)
    {
        double[]? u = null;
        var centre = default(ShellForces);
        ShellForces[]? corners = null;
        foreach (var (r, k) in terms)
        {
            u ??= new double[r.ULocal.Length];
            corners ??= new ShellForces[r.Corners.Length];
            for (var i = 0; i < u.Length; i++) u[i] += r.ULocal[i] * k;
            centre += r.Centre * k;
            for (var i = 0; i < corners.Length; i++) corners[i] += r.Corners[i] * k;
        }
        return new ShellResult(u ?? [], centre, corners ?? []);
    }
}

/// <summary>A bar's result in one case or combination: its own ends' local displacements (the released ones recovered), the end forces on it (local), its span loads (local).</summary>
public sealed class ElementResult(double[] uLocal, double[] forces, List<LocalLoad> loads)
{
    public double[] ULocal { get; } = uLocal;
    public double[] Forces { get; } = forces;
    public List<LocalLoad> Loads { get; } = loads;

    /// <summary>Σ factor × result: a combination by superposition.</summary>
    public static ElementResult Combine(IEnumerable<(ElementResult R, double Factor)> terms)
    {
        var (u, f) = (new double[12], new double[12]);
        var loads = new List<LocalLoad>();
        foreach (var (r, k) in terms)
        {
            for (var i = 0; i < 12; i++) { u[i] += r.ULocal[i] * k; f[i] += r.Forces[i] * k; }
            loads.AddRange(r.Loads.Select(l => l.Scaled(k)));
        }
        return new ElementResult(u, f, loads);
    }
}

/// <summary>One case's result: the nodes' displacements (6 a node, global; metres, radians), the reactions at the held nodes (kN, kN·m), each bar's, and the sums.</summary>
public sealed class CaseResult
{
    public required string Id { get; init; }
    public required double[] U { get; init; }
    public required Dictionary<int, double[]> Reactions { get; init; }
    public required ElementResult[] Elements { get; init; }
    public ShellResult[] Shells { get; init; } = [];
    public Vec3 SumLoads { get; init; }
    public Vec3 SumReactions { get; init; }
    /// <summary>|ΣR + ΣF| / |ΣF| (absolute when the loads sum to nothing).</summary>
    public double Balance { get; init; }
}

public sealed class FrameSolution
{
    public bool Ok { get; init; }
    public Mechanism? Mechanism { get; init; }
    public int Dofs { get; init; }
    public long Profile { get; init; }
    public double SolveMs { get; init; }
    /// <summary>Node rotations no bar gives any stiffness (pinned ends meeting): held, as they carry nothing.</summary>
    public int Springs { get; init; }
    public List<CaseResult> Cases { get; init; } = [];
    public string? Refused { get; init; }
}

/// <summary>Which factorization the solver uses: Lapin's sparse Cholesky (§10.6, the default) or the profile LDLᵀ of S1 (kept to compare).</summary>
public enum FactorKind { Sparse, Skyline }

/// <summary>
/// The linear static solver (docs/plan-fem.md §9.3, §10): the bars' and the shells' stiffness assembled in global axes,
/// factored once for every case: by Lapin's sparse Cholesky (supernodal, ordered by approximate minimum degree on the
/// nodes), or by the profile L·D·Lᵀ on nodes renumbered by Reverse Cuthill–McKee. A turning of nodes that moves none of them and
/// strains no bar (only pinned ends meet there) is held by a spring, which takes nothing; any other pivot that is not positive
/// is a mechanism, said with its node and degree of freedom. Reactions are R = K·U − F at the held degrees of
/// freedom; each case's balance ΣR + ΣF is checked.
/// </summary>
public static class FrameSolver
{
    public const long MaxProfile = 400_000_000;
    static readonly string[] DofNames = ["ux", "uy", "uz", "rx", "ry", "rz"];
    public static string DofName(int d) => DofNames[d];

    public static FrameSolution Solve(FrameProblem p, FactorKind kind = FactorKind.Sparse)
    {
        var sw = Stopwatch.StartNew();
        var nn = p.Nodes.Count;
        foreach (var e in p.Elements) e.Prepare(p.Nodes);
        foreach (var e in p.Shells) e.Prepare(p.Nodes);
        // The members: the bars, then the shells; each one's nodes.
        var nb = p.Elements.Count;
        var members = nb + p.Shells.Count;
        int[] NodesOf(int k) => k < nb ? [p.Elements[k].I, p.Elements[k].J] : p.Shells[k - nb].Nodes;
        var used = new bool[nn];
        var adjacent = new List<int>[nn];
        for (var i = 0; i < nn; i++) adjacent[i] = [];
        var touching = new List<int>[nn];
        for (var i = 0; i < nn; i++) touching[i] = [];
        for (var k = 0; k < p.Elements.Count; k++)
        {
            var e = p.Elements[k];
            used[e.I] = used[e.J] = true;
            touching[e.I].Add(k);
            touching[e.J].Add(k);
            if (e.I != e.J) { adjacent[e.I].Add(e.J); adjacent[e.J].Add(e.I); }
        }
        var touchingShells = new List<int>[nn];
        for (var i = 0; i < nn; i++) touchingShells[i] = [];
        for (var k = 0; k < p.Shells.Count; k++)
        {
            var ns = p.Shells[k].Nodes;
            foreach (var a in ns)
            {
                used[a] = true;
                touchingShells[a].Add(k);
                foreach (var b in ns) if (a != b) adjacent[a].Add(b);
            }
        }
        var globals = p.Elements.Select(e => e.GlobalStiffness()).Concat(p.Shells.Select(e => e.GlobalStiffness())).ToArray();

        // Held degrees of freedom.
        var held = new bool[nn * 6];
        for (var n = 0; n < nn; n++)
        {
            if (!used[n]) { for (var d = 0; d < 6; d++) held[n * 6 + d] = true; continue; }
            if (p.Held[n] is { } h) for (var d = 0; d < 6; d++) held[n * 6 + d] = h[d];
        }
        // A bar's own rotational stiffness (E·I/L, G·J/L) is what holding a free rotation takes at its nodes.
        var reference = new double[nn];
        for (var k = 0; k < p.Elements.Count; k++)
        {
            var e = p.Elements[k];
            var own = Math.Max(e.E * Math.Max(e.I22, e.I33), e.G * e.Jt) / e.L;
            reference[e.I] = Math.Max(reference[e.I], own);
            reference[e.J] = Math.Max(reference[e.J], own);
        }
        // A shell's: its plate stiffness D = E·t³ / 12(1 − ν²).
        foreach (var e in p.Shells)
        {
            var own = e.E * e.Thickness * e.Thickness * e.Thickness / (12 * (1 - e.Nu * e.Nu));
            foreach (var a in e.Nodes) reference[a] = Math.Max(reference[a], own);
        }
        var order = Rcm.Order(nn, adjacent.Select(a => (IReadOnlyList<int>)a.Distinct().ToList()).ToArray());

        // The equations of a set of degrees of freedom in RCM order, and their profile.
        (int[] Eq, int Count, int[] First, int[][] DofsOf, long Profile) Number(Func<int, int, bool> takes)
        {
            var eq = new int[nn * 6];
            Array.Fill(eq, -1);
            var count = 0;
            foreach (var n in order)
                for (var d = 0; d < 6; d++)
                    if (!held[n * 6 + d] && takes(n, d)) eq[n * 6 + d] = count++;
            var first = new int[count];
            for (var j = 0; j < count; j++) first[j] = j;
            var dofsOf = new int[members][];
            for (var k = 0; k < members; k++)
            {
                var ns = NodesOf(k);
                var ds = new int[6 * ns.Length];
                for (var x = 0; x < ns.Length; x++) for (var d = 0; d < 6; d++) ds[6 * x + d] = eq[ns[x] * 6 + d];
                dofsOf[k] = ds;
                var min = int.MaxValue;
                foreach (var x in ds) if (x >= 0 && x < min) min = x;
                foreach (var x in ds) if (x >= 0 && min < first[x]) first[x] = min;
            }
            long profile = 0;
            for (var j = 0; j < count; j++) profile += j - first[j] + 1;
            return (eq, count, first, dofsOf, profile);
        }
        ISymmetricFactor Structure(int count, int[] first, int[][] dofsOf, int[] eqs)
        {
            if (kind == FactorKind.Skyline) return new Skyline(first);
            var group = new int[count];
            for (var i = 0; i < eqs.Length; i++) if (eqs[i] >= 0) group[eqs[i]] = i / 6;
            return new SparseCholesky(count, dofsOf, group);
        }
        ISymmetricFactor Assemble(int count, int[] first, int[][] dofsOf, int[] eqs, ISymmetricFactor? structure = null)
        {
            var sk = structure ?? Structure(count, first, dofsOf, eqs);
            for (var k = 0; k < members; k++)
            {
                var (ds, g) = (dofsOf[k], globals[k]);
                var m = ds.Length;
                for (var r = 0; r < m; r++)
                {
                    if (ds[r] < 0) continue;
                    for (var c = 0; c < m; c++)
                        if (ds[c] >= ds[r] && g[r * m + c] != 0) sk.Add(ds[r], ds[c], g[r * m + c]);
                }
            }
            return sk;
        }

        var all = Number((_, _) => true);
        var neq = all.Count;
        var k0 = Structure(all.Count, all.First, all.DofsOf, all.Eq);
        var profile = kind == FactorKind.Skyline ? all.Profile : k0.Stored;
        if (profile > MaxProfile)
            return new FrameSolution { Ok = false, Dofs = neq, Profile = profile, Refused = $"The model is too large for this solver: {neq} degrees of freedom, a profile of {profile:N0} entries.", SolveMs = sw.Elapsed.TotalMilliseconds };

        // First the rotations alone, the translations held: a pivot lost there is a turning that moves no node and strains no
        // bar (only pinned ends meet, a pinned member's pieces twisting together). It carries nothing; a spring holds it, and
        // takes no force (its node's moment balances), so the results are those of the model as it is.
        // (A shell's node is left out: its plate bending and drilling hold all three turnings.)
        var rot = Number((n, d) => d >= 3 && touchingShells[n].Count == 0);
        var springs = new Dictionary<int, double>();
        if (rot.Count > 0)
        {
            var dofOfEq = new int[rot.Count];
            for (var i = 0; i < nn * 6; i++) if (rot.Eq[i] >= 0) dofOfEq[rot.Eq[i]] = i;
            var kr = Assemble(rot.Count, rot.First, rot.DofsOf, rot.Eq);
            kr.Factor(j =>
            {
                var dof = dofOfEq[j];
                var k = reference[dof / 6] > 0 ? reference[dof / 6] : 1;
                springs[dof] = k;
                return k;
            });
        }

        var eq = all.Eq;
        Assemble(all.Count, all.First, all.DofsOf, all.Eq, k0);
        foreach (var (dof, k) in springs) k0.Add(eq[dof], eq[dof], k);
        var bad = k0.Factor();
        if (bad >= 0)
        {
            var at = Array.IndexOf(eq, bad);
            var node = at / 6;
            // The mechanism's shape: the model held at that pivot (and any later one), pushed there; what moves most is the mechanism.
            var k1 = Assemble(all.Count, all.First, all.DofsOf, all.Eq);
            foreach (var (dof, k) in springs) k1.Add(eq[dof], eq[dof], k);
            var scale = reference[node] > 0 ? reference[node] : 1;
            k1.Factor(_ => scale);
            var x = new double[neq];
            x[bad] = scale;
            k1.Solve(x);
            var moves = new List<(int Node, double D)>();
            for (var n = 0; n < nn; n++)
            {
                if (!used[n]) continue;
                var d = 0.0;
                for (var c = 0; c < 3; c++) if (eq[n * 6 + c] >= 0) d += x[eq[n * 6 + c]] * x[eq[n * 6 + c]];
                moves.Add((n, Math.Sqrt(d)));
            }
            var top = moves.Count > 0 ? moves.Max(m => m.D) : 0;
            var moving = moves.Where(m => top > 0 && m.D >= 0.5 * top).OrderByDescending(m => m.D).Select(m => m.Node).Take(20).ToList();
            return new FrameSolution
            {
                Ok = false, Dofs = neq, Profile = profile, Springs = springs.Keys.Select(d => d / 6).Distinct().Count(),
                Mechanism = new Mechanism(node, at % 6, touching[node])
                {
                    Moving = moving, MovingElements = moving.SelectMany(n => touching[n]).Distinct().ToList(),
                    Shells = touchingShells[node], MovingShells = moving.SelectMany(n => touchingShells[n]).Distinct().ToList(),
                },
                SolveMs = sw.Elapsed.TotalMilliseconds,
            };
        }

        var cases = new List<CaseResult>();
        foreach (var c in p.Cases)
        {
            var localLoads = new List<LocalLoad>[p.Elements.Count];
            for (var k = 0; k < localLoads.Length; k++) localLoads[k] = [];
            foreach (var (k, s) in c.Span) localLoads[k].Add(p.Elements[k].ToLocal(s));
            var peq = new double[p.Elements.Count][];
            var pc = new double[p.Elements.Count][];
            var f = new double[nn * 6];
            foreach (var (n, v) in c.Nodal) { f[n * 6] += v.X; f[n * 6 + 1] += v.Y; f[n * 6 + 2] += v.Z; }
            foreach (var (n, v) in c.Moments) { f[n * 6 + 3] += v.X; f[n * 6 + 4] += v.Y; f[n * 6 + 5] += v.Z; }
            var applied = (double[])f.Clone();
            for (var k = 0; k < p.Elements.Count; k++)
            {
                var e = p.Elements[k];
                if (localLoads[k].Count == 0) { peq[k] = new double[12]; pc[k] = peq[k]; continue; }
                peq[k] = e.Equivalent(localLoads[k]);
                pc[k] = e.Condense(peq[k]);
                var g = e.ToNodes(pc[k]);
                for (var d = 0; d < 6; d++) { f[e.I * 6 + d] += g[d]; f[e.J * 6 + d] += g[6 + d]; }
            }
            // The shells' loads, by their consistent shares.
            var fq = new double[p.Shells.Count][];
            var sumQ = Vec3.Zero;
            foreach (var (k, q) in c.Surface)
            {
                var e = p.Shells[k];
                var g = e.Equivalent(q);
                if (fq[k] is { } had) for (var x = 0; x < g.Length; x++) had[x] += g[x];
                else fq[k] = g;
                sumQ += q * e.Area;
            }
            for (var k = 0; k < p.Shells.Count; k++)
            {
                if (fq[k] is not { } g) continue;
                var ns = p.Shells[k].Nodes;
                for (var x = 0; x < ns.Length; x++) for (var d = 0; d < 6; d++) f[ns[x] * 6 + d] += g[6 * x + d];
            }
            var rhs = new double[neq];
            for (var i = 0; i < nn * 6; i++) if (eq[i] >= 0) rhs[eq[i]] = f[i];
            k0.Solve(rhs);
            var u = new double[nn * 6];
            for (var i = 0; i < nn * 6; i++) if (eq[i] >= 0) u[i] = rhs[eq[i]];

            // What the elements push back on the nodes: K·u − (their own loads), and their results.
            double[] Internal(ElementResult[]? results, ShellResult[]? shellResults)
            {
                var nodeForce = new double[nn * 6];
                for (var k = 0; k < p.Elements.Count; k++)
                {
                    var e = p.Elements[k];
                    var un = new double[12];
                    Array.Copy(u, e.I * 6, un, 0, 6);
                    Array.Copy(u, e.J * 6, un, 6, 6);
                    var ul = e.ToLocal(un);
                    e.Recover(ul, peq[k]);
                    var fe = e.EndForces(ul, pc[k]);
                    if (results != null) results[k] = new ElementResult(ul, fe, localLoads[k]);
                    var g = e.ToNodes(fe);
                    for (var d = 0; d < 6; d++) { nodeForce[e.I * 6 + d] += g[d]; nodeForce[e.J * 6 + d] += g[6 + d]; }
                }
                for (var k = 0; k < p.Shells.Count; k++)
                {
                    var e = p.Shells[k];
                    var ns = e.Nodes;
                    var m = e.Size;
                    var ug = new double[m];
                    for (var x = 0; x < ns.Length; x++) Array.Copy(u, ns[x] * 6, ug, 6 * x, 6);
                    var kg = globals[nb + k];
                    for (var r = 0; r < m; r++)
                    {
                        var t = 0.0;
                        for (var x = 0; x < m; x++) t += kg[r * m + x] * ug[x];
                        if (fq[k] is { } g) t -= g[r];
                        nodeForce[ns[r / 6] * 6 + r % 6] += t;
                    }
                    if (shellResults != null)
                    {
                        var ul = e.ToLocal(ug);
                        var (centre, corners) = e.Resultants(ul);
                        shellResults[k] = new ShellResult(ul, centre, corners);
                    }
                }
                return nodeForce;
            }

            // Iterative refinement: the free equations' residual f − (K + springs)·u, solved again and added (Wilkinson;
            // Higham, "Accuracy and Stability of Numerical Algorithms", ch. 12), up to three times while it shrinks: it keeps
            // the balance whatever the elimination order on a badly conditioned model (a near-rigid bar beside flexible ones,
            // a very thin plate).
            var fNorm = Math.Sqrt(f.Sum(x => x * x));
            var last = double.MaxValue;
            for (var step = 0; step < 3; step++)
            {
                var inner = Internal(null, null);
                var res = new double[neq];
                var norm = 0.0;
                for (var i = 0; i < nn * 6; i++)
                    if (eq[i] >= 0)
                    {
                        var r = applied[i] - inner[i] - (springs.TryGetValue(i, out var ks) ? ks * u[i] : 0);
                        res[eq[i]] = r;
                        norm += r * r;
                    }
                norm = Math.Sqrt(norm);
                if (norm <= 1e-15 * fNorm || norm >= 0.5 * last) break;
                last = norm;
                k0.Solve(res);
                for (var i = 0; i < nn * 6; i++) if (eq[i] >= 0) u[i] += res[eq[i]];
            }
            var results = new ElementResult[p.Elements.Count];
            var shellResults = new ShellResult[p.Shells.Count];
            var nodeForce = Internal(results, shellResults);
            var reactions = new Dictionary<int, double[]>();
            var sumR = Vec3.Zero;
            for (var n = 0; n < nn; n++)
            {
                // A node no bar uses holds what is put on it (a wall's head under a slab's edge) when something holds it.
                if (used[n] ? !Enumerable.Range(0, 6).Any(d => held[n * 6 + d]) : p.Held[n] == null) continue;
                var r = new double[6];
                for (var d = 0; d < 6; d++) if (held[n * 6 + d]) r[d] = nodeForce[n * 6 + d] - applied[n * 6 + d];
                if (!used[n] && r.All(x => x == 0)) continue;
                reactions[n] = r;
                sumR += new Vec3(r[0], r[1], r[2]);
            }
            var sumF = c.Sum + sumQ;
            var scale = sumF.Length;
            var balance = (sumF + sumR).Length / (scale > 1e-9 ? scale : 1);
            cases.Add(new CaseResult { Id = c.Id, U = u, Reactions = reactions, Elements = results, Shells = shellResults, SumLoads = sumF, SumReactions = sumR, Balance = balance });
        }
        return new FrameSolution { Ok = true, Dofs = neq, Profile = profile, Springs = springs.Keys.Select(d => d / 6).Distinct().Count(), Cases = cases, SolveMs = sw.Elapsed.TotalMilliseconds };
    }
}
