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
    /// <summary>Stiff couplings of nodes (§11.2: a rigid diaphragm's penalty), each its nodes and its global stiffness (6 a node, row-major); and springs to the ground (§14.2, <see cref="Link.Ground"/>).</summary>
    public List<Link> Links { get; } = [];
    /// <summary>The nodes whose support works in compression only (§14.4): held or on springs while it pushes, lifted (free) when it would pull.</summary>
    public HashSet<int> Uplift { get; } = [];

    FrameLayouts? _layouts;
    /// <summary>
    /// The layouts its systems have had (§14.21: numbering, ordering, symbolic factorization), shared with the problems made
    /// from it (<see cref="Nonlinear.InStates"/>); each is checked against the problem's shape before it is used.
    /// </summary>
    public FrameLayouts Layouts
    {
        get => LazyInitializer.EnsureInitialized(ref _layouts);
        init => _layouts = value;
    }

    public int AddNode(Vec3 at, string id, bool[]? held = null)
    {
        Nodes.Add(at);
        NodeIds.Add(id);
        Held.Add(held);
        return Nodes.Count - 1;
    }
}

/// <summary>A coupling of nodes by a stiffness matrix in global axes (6 degrees of freedom a node, row-major): no element results of its own.</summary>
public sealed record Link(int[] Nodes, double[] K, string Id)
{
    /// <summary>A spring from its node to the ground (§14.2): its force is a reaction.</summary>
    public bool Ground { get; init; }

    /// <summary>Springs to the ground on one node's degrees of freedom (kN/m, kN·m/rad, global): its 6 × 6 stiffness, or its diagonal.</summary>
    public static Link Spring(int node, double[] k, string id)
    {
        if (k.Length == 36) return new Link([node], (double[])k.Clone(), id) { Ground = true };
        if (k.Length != 6) throw new ArgumentException("A spring is six stiffnesses or a 6 × 6 matrix.");
        var m = new double[36];
        for (var d = 0; d < 6; d++) m[d * 6 + d] = k[d];
        return new Link([node], m, id) { Ground = true };
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
/// freedom; each case's balance ΣR + ΣF is checked. The assembled, factored system (<see cref="FrameSystem"/>) is kept for
/// the modal analysis (§11.2), which solves with the same factor.
/// </summary>
public static class FrameSolver
{
    public const long MaxProfile = 400_000_000;
    /// <summary>Equations past which a model's load cases are solved side by side.</summary>
    public const int CasesApart = 2_000;
    static readonly string[] DofNames = ["ux", "uy", "uz", "rx", "ry", "rz"];
    public static string DofName(int d) => DofNames[d];

    public static FrameSolution Solve(FrameProblem p, FactorKind kind = FactorKind.Sparse)
    {
        var sw = Stopwatch.StartNew();
        var (sys, failure) = FrameSystem.Build(p, kind, sw);
        if (sys == null) return failure!;
        // The cases on the one factor, side by side when the model is large enough to be worth it (§14.20): each solve reads the factor only.
        var cases = (sys.Neq >= CasesApart ? FemParallel.Map(p.Cases, sys.SolveCase) : p.Cases.Select(sys.SolveCase).ToArray()).ToList();
        return new FrameSolution { Ok = true, Dofs = sys.Neq, Profile = sys.Profile, Springs = sys.SpringNodes, Cases = cases, SolveMs = sw.Elapsed.TotalMilliseconds };
    }
}

/// <summary>A problem's stiffness assembled and factored (§9.3, §10.7): solves a case, a load vector or a displacement's results.</summary>
public sealed class FrameSystem
{
    public FrameProblem P { get; }
    public int Neq { get; private set; }
    public long Profile { get; private set; }
    /// <summary>Each global degree of freedom's equation (node × 6 + dof), −1 when held.</summary>
    public int[] Eq { get; private set; } = [];
    public bool[] Held { get; private set; } = [];
    /// <summary>The nodes some member uses.</summary>
    public bool[] Used { get; private set; } = [];
    public int SpringNodes => _springs.Keys.Select(d => d / 6).Distinct().Count();

    readonly int _nn, _nb, _ns, _members;
    double[][] _globals = [];
    ISymmetricFactor _k = null!;
    readonly Dictionary<int, double> _springs = [];

    FrameSystem(FrameProblem p)
    {
        P = p;
        _nn = p.Nodes.Count;
        _nb = p.Elements.Count;
        _ns = p.Shells.Count;
        _members = _nb + _ns + p.Links.Count;
    }

    int[] NodesOf(int k) => k < _nb ? [P.Elements[k].I, P.Elements[k].J] : k < _nb + _ns ? P.Shells[k - _nb].Nodes : P.Links[k - _nb - _ns].Nodes;

    /// <summary>The layout it was built on (§14.21): what it shares with the systems of the same shape.</summary>
    public FrameLayout Layout { get; private set; } = null!;

    /// <summary>
    /// The system assembled and factored, or the solution that says why it could not be (too large, a mechanism). Its
    /// numbering, ordering and symbolic factorization are the layout's (<paramref name="layout"/> when the problem has its
    /// shape, else the problem's kept one, else made now: §14.21); only the values are worked out here.
    /// </summary>
    public static (FrameSystem? System, FrameSolution? Failure) Build(FrameProblem p, FactorKind kind = FactorKind.Sparse, Stopwatch? sw = null, FrameLayout? layout = null)
    {
        sw ??= Stopwatch.StartNew();
        var s = new FrameSystem(p);
        var nn = s._nn;
        foreach (var e in p.Elements) e.Prepare(p.Nodes);
        foreach (var e in p.Shells) e.Prepare(p.Nodes);
        var members = s._members;
        var lay = s.Layout = FrameLayout.For(p, kind, layout);
        var (used, held, touching, touchingShells, all) = (lay.Used, lay.Held, lay.Touching, lay.TouchingShells, lay.All);
        s.Used = used;
        // Each member's stiffness on its nodes, worked out once by the member for its places (shared, only read).
        var globals = new double[members][];
        for (var k = 0; k < s._nb; k++) globals[k] = p.Elements[k].Global();
        for (var k = 0; k < s._ns; k++) globals[s._nb + k] = p.Shells[k].Global();
        for (var k = 0; k < p.Links.Count; k++) globals[s._nb + s._ns + k] = p.Links[k].K;
        s._globals = globals;
        s.Held = held;
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

        ISymmetricFactor Assemble(ISymmetricFactor sk, int[][] dofsOf)
        {
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

        var neq = all.Count;
        s.Neq = neq;
        var profile = lay.Profile;
        s.Profile = profile;
        if (profile > FrameSolver.MaxProfile)
            return (null, new FrameSolution { Ok = false, Dofs = neq, Profile = profile, Refused = $"The model is too large for this solver: {neq} degrees of freedom, a profile of {profile:N0} entries.", SolveMs = sw.Elapsed.TotalMilliseconds });

        // First the rotations alone, the translations held: a pivot lost there is a turning that moves no node and strains no
        // bar (only pinned ends meet, a pinned member's pieces twisting together). It carries nothing; a spring holds it, and
        // takes no force (its node's moment balances), so the results are those of the model as it is.
        var rot = lay.Rot!;
        var springs = s._springs;
        if (rot.Count > 0)
        {
            var dofOfEq = lay.RotDofOfEq;
            var kr = Assemble(lay.RotStructure!.Fresh(), rot.DofsOf);
            kr.Factor(j =>
            {
                var dof = dofOfEq[j];
                var k = reference[dof / 6] > 0 ? reference[dof / 6] : 1;
                springs[dof] = k;
                return k;
            });
        }

        var eq = all.Eq;
        s.Eq = eq;
        var k0 = Assemble(lay.Structure.Fresh(), all.DofsOf);
        foreach (var (dof, k) in springs) k0.Add(eq[dof], eq[dof], k);
        var bad = k0.Factor();
        if (bad >= 0)
        {
            var at = Array.IndexOf(eq, bad);
            var node = at / 6;
            // The mechanism's shape: the model held at that pivot (and any later one), pushed there; what moves most is the mechanism.
            var k1 = Assemble(lay.Structure.Fresh(), all.DofsOf);
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
            return (null, new FrameSolution
            {
                Ok = false, Dofs = neq, Profile = profile, Springs = s.SpringNodes,
                Mechanism = new Mechanism(node, at % 6, touching[node].ToList())
                {
                    Moving = moving, MovingElements = moving.SelectMany(n => touching[n]).Distinct().ToList(),
                    Shells = touchingShells[node].ToList(), MovingShells = moving.SelectMany(n => touchingShells[n]).Distinct().ToList(),
                },
                SolveMs = sw.Elapsed.TotalMilliseconds,
            });
        }
        s._k = k0;
        return (s, null);
    }

    /// <summary>The diagonal of K by equation (the springs' included).</summary>
    public double[] Diagonal()
    {
        var d = new double[Neq];
        for (var k = 0; k < _members; k++)
        {
            var ns = NodesOf(k);
            var g = _globals[k];
            var m = 6 * ns.Length;
            for (var r = 0; r < m; r++)
                if (Eq[ns[r / 6] * 6 + r % 6] is var e and >= 0) d[e] += g[r * m + r];
        }
        foreach (var (dof, k) in _springs) d[Eq[dof]] += k;
        return d;
    }

    /// <summary>A vector by equation spread onto the nodes' degrees of freedom (node × 6; the held ones nought).</summary>
    public double[] ToGlobal(double[] byEq)
    {
        var u = new double[_nn * 6];
        for (var i = 0; i < u.Length; i++) if (Eq[i] >= 0) u[i] = byEq[Eq[i]];
        return u;
    }

    /// <summary>Solves K·x = b in place (b by equation).</summary>
    public void SolveInPlace(double[] b) => _k.Solve(b);

    /// <summary>The springs' and the members' K·u (global, node × 6), without their loads.</summary>
    public double[] Multiply(double[] u)
    {
        var o = Internal(u, null, null, null, null, null);
        foreach (var (dof, k) in _springs) o[dof] += k * u[dof];
        return o;
    }

    /// <summary>A case's loads as forces on the nodes (global, node × 6): its nodal ones, its bars' span loads by their fixed-end forces, its shells' by their consistent shares.</summary>
    public double[] LoadVector(CaseLoads c) => Loads(c).F;

    sealed record CaseVectors(double[] F, double[] Applied, List<LocalLoad>[] LocalLoads, double[][] Peq, double[][] Pc, double[]?[] Fq, Vec3 SumQ);

    CaseVectors Loads(CaseLoads c)
    {
        var p = P;
        var nn = _nn;
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
            if (localLoads[k].Count == 0) { peq[k] = Zero12; pc[k] = Zero12; continue; }
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
        return new CaseVectors(f, applied, localLoads, peq, pc, fq, sumQ);
    }

    static readonly double[] Zero12 = new double[12];

    /// <summary>What the members push back on the nodes: K·u − (their own loads), and their results.</summary>
    double[] Internal(double[] u, List<LocalLoad>[]? localLoads, double[][]? peq, double[][]? pc, double[]?[]? fq, (ElementResult[] Bars, ShellResult[] Shells)? results)
    {
        var p = P;
        var nodeForce = new double[_nn * 6];
        // Scratch for what is not kept (§14.21: the refinement's residuals make no arrays); a result's own are new.
        Span<double> un = stackalloc double[12], gn = stackalloc double[12], ulTmp = stackalloc double[12], feTmp = stackalloc double[12];
        for (var k = 0; k < p.Elements.Count; k++)
        {
            var e = p.Elements[k];
            u.AsSpan(e.I * 6, 6).CopyTo(un);
            u.AsSpan(e.J * 6, 6).CopyTo(un[6..]);
            double[]? keptU = null, keptF = null;
            if (results != null) (keptU, keptF) = (new double[12], new double[12]);
            var ul = keptU ?? ulTmp;
            var fe = keptF ?? feTmp;
            e.ToLocal(un, ul);
            var pe = peq?[k] ?? Zero12;
            e.Recover(ul, pe);
            e.EndForces(ul, pc?[k] ?? pe, fe);
            if (results is { } r) r.Bars[k] = new ElementResult(keptU!, keptF!, localLoads?[k] ?? []);
            gn.Clear();
            e.ToNodes(fe, gn);
            for (var d = 0; d < 6; d++) { nodeForce[e.I * 6 + d] += gn[d]; nodeForce[e.J * 6 + d] += gn[6 + d]; }
        }
        Span<double> ug = stackalloc double[24];
        for (var k = 0; k < p.Shells.Count; k++)
        {
            var e = p.Shells[k];
            var ns = e.Nodes;
            var m = e.Size;
            if (ug.Length < m) ug = new double[m];
            for (var x = 0; x < ns.Length; x++) u.AsSpan(ns[x] * 6, 6).CopyTo(ug[(6 * x)..]);
            var kg = _globals[_nb + k];
            for (var r = 0; r < m; r++)
            {
                var t = 0.0;
                for (var x = 0; x < m; x++) t += kg[r * m + x] * ug[x];
                if (fq?[k] is { } g) t -= g[r];
                nodeForce[ns[r / 6] * 6 + r % 6] += t;
            }
            if (results is { } rs)
            {
                var ul = e.ToLocal(ug[..m]);
                var (centre, corners) = e.Resultants(ul);
                rs.Shells[k] = new ShellResult(ul, centre, corners);
            }
        }
        for (var k = 0; k < p.Links.Count; k++)
        {
            var l = p.Links[k];
            var m = 6 * l.Nodes.Length;
            var ul = m <= 24 ? ug[..m] : new double[m];
            for (var x = 0; x < l.Nodes.Length; x++) u.AsSpan(l.Nodes[x] * 6, 6).CopyTo(ul[(6 * x)..]);
            for (var r = 0; r < m; r++)
            {
                var t = 0.0;
                for (var x = 0; x < m; x++) t += l.K[r * m + x] * ul[x];
                nodeForce[l.Nodes[r / 6] * 6 + r % 6] += t;
            }
        }
        return nodeForce;
    }

    /// <summary>One case solved: the displacements (refined), the members' results, the reactions and the balance.</summary>
    public CaseResult SolveCase(CaseLoads c)
    {
        var v = Loads(c);
        var (f, applied) = (v.F, v.Applied);
        var (nn, eq, neq) = (_nn, Eq, Neq);
        var rhs = new double[neq];
        for (var i = 0; i < nn * 6; i++) if (eq[i] >= 0) rhs[eq[i]] = f[i];
        _k.Solve(rhs);
        var u = new double[nn * 6];
        for (var i = 0; i < nn * 6; i++) if (eq[i] >= 0) u[i] = rhs[eq[i]];

        // Iterative refinement: the free equations' residual f − (K + springs)·u, solved again and added (Wilkinson;
        // Higham, "Accuracy and Stability of Numerical Algorithms", ch. 12), up to three times while it shrinks: it keeps
        // the balance whatever the elimination order on a badly conditioned model (a near-rigid bar beside flexible ones,
        // a very thin plate).
        var fNorm = Math.Sqrt(f.Sum(x => x * x));
        var last = double.MaxValue;
        for (var step = 0; step < 3; step++)
        {
            var inner = Internal(u, v.LocalLoads, v.Peq, v.Pc, v.Fq, null);
            var res = new double[neq];
            var norm = 0.0;
            for (var i = 0; i < nn * 6; i++)
                if (eq[i] >= 0)
                {
                    var r = applied[i] - inner[i] - (_springs.TryGetValue(i, out var ks) ? ks * u[i] : 0);
                    res[eq[i]] = r;
                    norm += r * r;
                }
            norm = Math.Sqrt(norm);
            if (norm <= 1e-15 * fNorm || norm >= 0.5 * last) break;
            last = norm;
            _k.Solve(res);
            for (var i = 0; i < nn * 6; i++) if (eq[i] >= 0) u[i] += res[eq[i]];
        }
        return Result(c.Id, u, v, c.Sum + v.SumQ);
    }

    /// <summary>The results of a displacement with no loads on the members (a mode's, a spectral response's): their forces, the reactions.</summary>
    public CaseResult FromDisplacements(string id, double[] u) =>
        Result(id, u, new CaseVectors(new double[_nn * 6], new double[_nn * 6], null!, null!, null!, null!, Vec3.Zero), Vec3.Zero);

    CaseResult Result(string id, double[] u, CaseVectors v, Vec3 sumF)
    {
        var p = P;
        var (nn, held, used) = (_nn, Held, Used);
        var results = new ElementResult[p.Elements.Count];
        var shellResults = new ShellResult[p.Shells.Count];
        var nodeForce = Internal(u, v.LocalLoads, v.Peq, v.Pc, v.Fq, (results, shellResults));
        // The springs to the ground (§14.2): their forces are reactions, what the members push on their nodes less the loads there.
        double[]? ground = null;
        bool[]? onSpring = null;
        foreach (var l in p.Links)
        {
            if (!l.Ground) continue;
            ground ??= new double[nn * 6];
            onSpring ??= new bool[nn * 6];
            var m = 6 * l.Nodes.Length;
            for (var r = 0; r < m; r++)
            {
                var t = 0.0;
                var any = false;
                for (var x = 0; x < m; x++) { var k = l.K[r * m + x]; if (k == 0) continue; any = true; t += k * u[l.Nodes[x / 6] * 6 + x % 6]; }
                if (!any) continue;
                ground[l.Nodes[r / 6] * 6 + r % 6] += t;
                onSpring[l.Nodes[r / 6] * 6 + r % 6] = true;
            }
        }
        var reactions = new Dictionary<int, double[]>();
        var sumR = Vec3.Zero;
        for (var n = 0; n < nn; n++)
        {
            var sprung = onSpring != null && Enumerable.Range(0, 6).Any(d => onSpring[n * 6 + d]);
            // A node no bar uses holds what is put on it (a wall's head under a slab's edge) when something holds it.
            if (!sprung && (used[n] ? !Enumerable.Range(0, 6).Any(d => held[n * 6 + d]) : p.Held[n] == null)) continue;
            var r = new double[6];
            for (var d = 0; d < 6; d++)
                if (held[n * 6 + d]) r[d] = nodeForce[n * 6 + d] - (ground != null ? ground[n * 6 + d] : 0) - v.Applied[n * 6 + d];
                else if (onSpring != null && onSpring[n * 6 + d]) r[d] = -ground![n * 6 + d];
            if (!used[n] && r.All(x => x == 0)) continue;
            reactions[n] = r;
            sumR += new Vec3(r[0], r[1], r[2]);
        }
        var scale = sumF.Length;
        var balance = (sumF + sumR).Length / (scale > 1e-9 ? scale : 1);
        return new CaseResult { Id = id, U = u, Reactions = reactions, Elements = results, Shells = shellResults, SumLoads = sumF, SumReactions = sumR, Balance = balance };
    }
}
