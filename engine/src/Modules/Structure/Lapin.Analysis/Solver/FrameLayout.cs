namespace Lapin.Analysis.Solver;

/// <summary>
/// What a <see cref="FrameSystem"/> takes from its problem's shape alone (docs/plan-fem.md §14.21): which nodes the members
/// use, the held degrees of freedom, the nodes' Reverse Cuthill–McKee order, the equations' numbering and each member's
/// equations, the symbolic factorization (the ordering by minimum degree or nested dissection, the elimination tree, the
/// supernodes, the pattern) of the whole system and of its rotations alone. None of it reads a stiffness value: a member
/// taken out (zero stiffness) or an axial force changes nothing here, so the iterations of a nonlinear solve and the
/// combinations of one problem share one layout until a support lifts (its degrees of freedom become equations). Built once,
/// then only read: systems solved side by side share it; each has its own values (<see cref="ISymmetricFactor.Fresh"/>).
/// </summary>
public sealed class FrameLayout
{
    public FactorKind Kind { get; }
    public int Nodes { get; }
    /// <summary>The nodes some member uses.</summary>
    public bool[] Used { get; }
    /// <summary>The held degrees of freedom (node × 6): the problem's, and every one of a node no member uses.</summary>
    public bool[] Held { get; }
    // The shape it was made for (each bar's two nodes, each shell's and link's), to tell whether a problem has it.
    readonly int[] _bars;
    readonly int[][] _shells, _links;
    readonly int _members;

    /// <summary>The bars and the shells at each node (to say a mechanism).</summary>
    internal List<int>[] Touching { get; } = [];
    internal List<int>[] TouchingShells { get; } = [];

    internal sealed record Numbering(int[] Eq, int Count, int[] First, int[][] DofsOf, long Profile);

    /// <summary>Every free degree of freedom's equation, in RCM order.</summary>
    internal Numbering All { get; }
    /// <summary>The rotations alone (the shells' nodes left out), what finds the turnings no member holds; null when the system is refused.</summary>
    internal Numbering? Rot { get; }
    /// <summary>Each rotation equation's degree of freedom (node × 6 + dof).</summary>
    internal int[] RotDofOfEq { get; } = [];
    /// <summary>The structures, never assembled themselves: each system takes a <see cref="ISymmetricFactor.Fresh"/> copy.</summary>
    internal ISymmetricFactor Structure { get; }
    internal ISymmetricFactor? RotStructure { get; }
    /// <summary>The entries the factor holds (the profile with <see cref="FactorKind.Skyline"/>).</summary>
    public long Profile { get; }

    FrameLayout(FrameProblem p, FactorKind kind)
    {
        Kind = kind;
        var nn = Nodes = p.Nodes.Count;
        _bars = new int[2 * p.Elements.Count];
        for (var k = 0; k < p.Elements.Count; k++) (_bars[2 * k], _bars[2 * k + 1]) = (p.Elements[k].I, p.Elements[k].J);
        _shells = p.Shells.Select(s => (int[])s.Nodes.Clone()).ToArray();
        _links = p.Links.Select(l => (int[])l.Nodes.Clone()).ToArray();
        _members = p.Elements.Count + _shells.Length + _links.Length;

        var used = new bool[nn];
        var adjacent = new List<int>[nn];
        for (var i = 0; i < nn; i++) adjacent[i] = [];
        var touching = new List<int>[nn];
        for (var i = 0; i < nn; i++) touching[i] = [];
        for (var k = 0; k < p.Elements.Count; k++)
        {
            var (a, b) = (_bars[2 * k], _bars[2 * k + 1]);
            used[a] = used[b] = true;
            touching[a].Add(k);
            touching[b].Add(k);
            if (a != b) { adjacent[a].Add(b); adjacent[b].Add(a); }
        }
        var touchingShells = new List<int>[nn];
        for (var i = 0; i < nn; i++) touchingShells[i] = [];
        for (var k = 0; k < _shells.Length; k++)
        {
            var ns = _shells[k];
            foreach (var a in ns)
            {
                used[a] = true;
                touchingShells[a].Add(k);
                foreach (var b in ns) if (a != b) adjacent[a].Add(b);
            }
        }
        foreach (var l in _links)
            foreach (var a in l)
            {
                used[a] = true;
                foreach (var b in l) if (a != b) adjacent[a].Add(b);
            }
        Used = used;
        Touching = touching;
        TouchingShells = touchingShells;

        // Held degrees of freedom.
        var held = new bool[nn * 6];
        for (var n = 0; n < nn; n++)
        {
            if (!used[n]) { for (var d = 0; d < 6; d++) held[n * 6 + d] = true; continue; }
            if (p.Held[n] is { } h) for (var d = 0; d < 6; d++) held[n * 6 + d] = h[d];
        }
        Held = held;
        var order = Rcm.Order(nn, adjacent.Select(a => (IReadOnlyList<int>)a.Distinct().ToList()).ToArray());

        // The equations of a set of degrees of freedom in RCM order, and their profile.
        Numbering Number(Func<int, int, bool> takes)
        {
            var eq = new int[nn * 6];
            Array.Fill(eq, -1);
            var count = 0;
            foreach (var n in order)
                for (var d = 0; d < 6; d++)
                    if (!held[n * 6 + d] && takes(n, d)) eq[n * 6 + d] = count++;
            var first = new int[count];
            for (var j = 0; j < count; j++) first[j] = j;
            var dofsOf = new int[_members][];
            for (var k = 0; k < _members; k++)
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
            return new Numbering(eq, count, first, dofsOf, profile);
        }
        ISymmetricFactor Make(Numbering x)
        {
            if (kind == FactorKind.Skyline) return new Skyline(x.First);
            var group = new int[x.Count];
            for (var i = 0; i < x.Eq.Length; i++) if (x.Eq[i] >= 0) group[x.Eq[i]] = i / 6;
            return new SparseCholesky(x.Count, x.DofsOf, group);
        }

        All = Number((_, _) => true);
        Structure = Make(All);
        Profile = kind == FactorKind.Skyline ? All.Profile : Structure.Stored;
        if (Profile > FrameSolver.MaxProfile) return;
        // (A shell's node is left out: its plate bending and drilling hold all three turnings.)
        var rot = Number((n, d) => d >= 3 && touchingShells[n].Count == 0);
        Rot = rot;
        if (rot.Count == 0) return;
        RotDofOfEq = new int[rot.Count];
        for (var i = 0; i < nn * 6; i++) if (rot.Eq[i] >= 0) RotDofOfEq[rot.Eq[i]] = i;
        RotStructure = Make(rot);
    }

    int[] NodesOf(int k)
    {
        var nb = _bars.Length / 2;
        return k < nb ? [_bars[2 * k], _bars[2 * k + 1]] : k < nb + _shells.Length ? _shells[k - nb] : _links[k - nb - _shells.Length];
    }

    /// <summary>Whether a problem has this shape: the same nodes, members on the same nodes in the same order, the same held degrees of freedom.</summary>
    public bool Fits(FrameProblem p, FactorKind kind)
    {
        if (kind != Kind || p.Nodes.Count != Nodes || 2 * p.Elements.Count != _bars.Length || p.Shells.Count != _shells.Length || p.Links.Count != _links.Length) return false;
        for (var k = 0; k < p.Elements.Count; k++)
            if (p.Elements[k].I != _bars[2 * k] || p.Elements[k].J != _bars[2 * k + 1]) return false;
        static bool Same(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
        for (var k = 0; k < _shells.Length; k++) if (!Same(p.Shells[k].Nodes, _shells[k])) return false;
        for (var k = 0; k < _links.Length; k++) if (!Same(p.Links[k].Nodes, _links[k])) return false;
        for (var n = 0; n < Nodes; n++)
        {
            if (!Used[n]) continue;
            var h = p.Held[n];
            for (var d = 0; d < 6; d++) if ((h != null && h[d]) != Held[n * 6 + d]) return false;
        }
        return true;
    }

    /// <summary>A layout for a problem: the hint when the problem has its shape, else the problem's kept one (<see cref="FrameLayouts"/>), else a new one.</summary>
    public static FrameLayout For(FrameProblem p, FactorKind kind, FrameLayout? hint = null) =>
        FemReuse.Off ? Make(p, kind) : hint != null && hint.Fits(p, kind) ? hint : p.Layouts.For(p, kind);

    internal static FrameLayout Make(FrameProblem p, FactorKind kind) => new(p, kind);
}

/// <summary>
/// The layouts a problem and the problems made from it (<see cref="Nonlinear.InStates"/>: its iterations, its combinations,
/// the states buckling is worked out on) have had (§14.21): the most recent first, at most <see cref="Kept"/>. Each is looked
/// up by its shape, checked whole (<see cref="FrameLayout.Fits"/>), so a problem changed after a solve gets a new one. A
/// missing one is made under the lock, so problems on many threads asking for the same one together make it once.
/// </summary>
public sealed class FrameLayouts
{
    /// <summary>Layouts kept: the problem's own and a few with supports lifted.</summary>
    public const int Kept = 8;

    readonly object _gate = new();
    readonly List<FrameLayout> _kept = [];

    public int Count { get { lock (_gate) return _kept.Count; } }

    public FrameLayout For(FrameProblem p, FactorKind kind)
    {
        lock (_gate)
        {
            var at = _kept.FindIndex(x => x.Fits(p, kind));
            if (at == 0) return _kept[0];
            var layout = at > 0 ? _kept[at] : FrameLayout.Make(p, kind);
            if (at > 0) _kept.RemoveAt(at);
            _kept.Insert(0, layout);
            if (_kept.Count > Kept) _kept.RemoveAt(_kept.Count - 1);
            return layout;
        }
    }
}

/// <summary>
/// Solves with nothing kept from one system to the next (§14.21, §14.22), within a flow of calls (a test's): every system
/// gets a layout made for it and new arrays for its factor's values, as before the reuse. The reuse is bit for bit the same,
/// which tests compare in one process (on one machine, one JIT), the goldens' portable half (§14.23).
/// </summary>
public static class FemReuse
{
    static readonly AsyncLocal<bool> Disabled = new();

    /// <summary>Whether this flow of calls solves without reuse.</summary>
    public static bool Off => Disabled.Value;

    /// <summary>No layouts or factor values kept until disposed.</summary>
    public static IDisposable Without()
    {
        var had = Disabled.Value;
        Disabled.Value = true;
        return new Restore(had);
    }

    sealed class Restore(bool had) : IDisposable
    {
        public void Dispose() => Disabled.Value = had;
    }
}
