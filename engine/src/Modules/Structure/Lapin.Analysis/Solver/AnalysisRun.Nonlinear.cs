using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Combination = Lapin.Analysis.Combinations.Combination;

namespace Lapin.Analysis.Solver;

/// <summary>
/// One static combination solved nonlinearly (§14.1): its factors on the cases, its load vector, its result; or one of its
/// four variants with a global sway imperfection (§14.14: <see cref="Variant"/> +X, -X, +Y, -Y, its key the combination's
/// with |variant, the combination's own in <see cref="Base"/>).
/// </summary>
public sealed record NonlinearSet(string Key, string Name, string Kind, List<(int Case, double Factor)> Terms, CaseLoads Loads, NonlinearResult Result)
{
    public bool Ok => Result.Ok;
    /// <summary>The combination's key (the set's own when it has no variants).</summary>
    public required string Base { get; init; }
    public string? Variant { get; init; }
    public ImperfectionSet? Imperfection { get; init; }
    /// <summary>EN 1993-1-1 5.3.2(4): the combination's horizontal loads along the variant's direction are at least 0.15 of its vertical ones.</summary>
    public bool Neglectable { get; init; }
}

/// <summary>
/// S5's nonlinear combinations (docs/plan-fem.md §14.1): when the project solves to second order, or has a one-way member or
/// a support that lifts, each static combination is solved on its own load vector by <see cref="Nonlinear"/>; its result is
/// kept beside the cases' (<see cref="CaseAt"/>), and the combination's factors become that result's alone
/// (<see cref="Terms"/>), so the envelopes, the diagrams, the contours, the walls' forces and the checks read it as any
/// other. The cases stay first order. A combination that is unstable or a mechanism is left out of the envelopes and checks.
/// Linear buckling (§14.3) on demand, per ultimate combination.
/// </summary>
public sealed partial class AnalysisRun
{
    /// <summary>The static combinations solved nonlinearly, when the analysis calls for it (else empty).</summary>
    public List<NonlinearSet> NonlinearSets { get; init; } = [];
    Dictionary<string, int>? _nlIndex;

    Dictionary<string, int> NonlinearIndex()
    {
        if (_nlIndex != null) return _nlIndex;
        var o = new Dictionary<string, int>();
        var at = Solution?.Cases.Count ?? 0;
        foreach (var s in NonlinearSets) if (s.Ok) o[s.Key] = at++;
        return _nlIndex = o;
    }

    /// <summary>How many results there are: the cases', then the nonlinear combinations' that stood.</summary>
    public int ResultCount => (Solution?.Cases.Count ?? 0) + NonlinearSets.Count(s => s.Ok);

    /// <summary>A result by its index: a case's (the solution's), or past them a nonlinear combination's.</summary>
    public CaseResult CaseAt(int i)
    {
        var cases = Solution!.Cases;
        if (i < cases.Count) return cases[i];
        var k = i - cases.Count;
        foreach (var s in NonlinearSets)
        {
            if (!s.Ok) continue;
            if (k-- == 0) return s.Result.Result!;
        }
        throw new ArgumentOutOfRangeException(nameof(i));
    }

    /// <summary>A result's loads: a case's, or a nonlinear combination's load vector.</summary>
    public CaseLoads LoadsOf(int i)
    {
        var cases = Solution!.Cases;
        if (i < cases.Count) return Problem.Problem.Cases.First(c => c.Id == cases[i].Id);
        var k = i - cases.Count;
        foreach (var s in NonlinearSets)
        {
            if (!s.Ok) continue;
            if (k-- == 0) return s.Loads;
        }
        throw new ArgumentOutOfRangeException(nameof(i));
    }

    /// <summary>Factors on the cases themselves: a nonlinear combination's result taken back to its combination's factors (for what depends on its cases, as timber's kmod).</summary>
    public List<(int Case, double Factor)> Underlying(List<(int Case, double Factor)> terms)
    {
        var n = Solution!.Cases.Count;
        if (terms.All(t => t.Case < n)) return terms;
        var o = new List<(int, double)>();
        foreach (var (ci, k) in terms)
        {
            if (ci < n) { o.Add((ci, k)); continue; }
            var set = NonlinearSets.Where(s => s.Ok).ElementAt(ci - n);
            o.AddRange(set.Terms.Select(t => (t.Case, t.Factor * k)));
        }
        return o;
    }

    /// <summary>A combination left out (§14.1): unstable or a mechanism under the nonlinear solve (any of its variants).</summary>
    bool Failed(string key) => NonlinearSets.Any(s => s.Base == key && !s.Ok);

    /// <summary>The nonlinear set a combination's buckling is worked out on: its own, or its first variant's (+X, §14.14).</summary>
    NonlinearSet? NonlinearFor(Combination c) => NonlinearSets.FirstOrDefault(s => s.Base == c.Key);

    /// <summary>
    /// A combination's results as sets of factors on the results: its own, or (§14.14) its four imperfection variants', which
    /// a result then envelopes, each named where it governs.
    /// </summary>
    List<(string Key, string Name, List<(int Case, double Factor)> Terms)> SetsOf(Combination c)
    {
        if (NonlinearSets.Count > 0)
        {
            var index = NonlinearIndex();
            var vs = NonlinearSets.Where(s => s.Base == c.Key && s.Variant != null && s.Ok).ToList();
            if (vs.Count > 0) return vs.Select(s => (s.Key, s.Name, new List<(int, double)> { (index[s.Key], 1.0) })).ToList();
        }
        return [(c.Key, c.Name, Terms(c))];
    }

    /// <summary>Whether the analysis solves combinations on their own: second order, imperfections, a one-way member or a support that lifts.</summary>
    public static bool CallsForNonlinear(StructuralProblem sp) => sp.Settings.SecondOrder || sp.Settings.Imperfections || Nonlinear.HasStates(sp.Problem);

    static string Signed(string variant) => variant.Replace('-', '−');

    /// <summary>
    /// Each static combination's load vector, solved nonlinearly (when second order or a state calls for it), and each ultimate
    /// one's four imperfection variants (§14.14: the forces of the first-order axial forces added, ±X, ±Y); the notes that say
    /// what came of it. With imperfections alone (first order, no states) only the ultimate combinations are solved.
    /// </summary>
    static List<NonlinearSet> SolveNonlinear(StructuralProblem sp, FrameSolution sol, List<Combination> combos, List<string> notes, CancellationToken cancel = default)
    {
        var p = sp.Problem;
        var every = sp.Settings.SecondOrder || Nonlinear.HasStates(p);
        var options = new NonlinearOptions(sp.Settings.SecondOrder);
        var noColumns = false;
        // The load vectors first, in turn (each a set made once its result is known); then their solves side by side (§14.20).
        var jobs = new List<(CaseLoads Loads, Func<NonlinearResult, NonlinearSet> Make)>();
        foreach (var c in combos)
        {
            var terms = c.Factors.Select(f => (sol.Cases.FindIndex(x => x.Id == f.Case.Id), f.Value)).Where(x => x.Item1 >= 0).ToList();
            CaseLoads Loads(string id)
            {
                var loads = new CaseLoads(id);
                foreach (var (ci, k) in terms)
                {
                    var src = p.Cases[ci];
                    foreach (var (n, v) in src.Nodal) loads.AddNodal(n, v * k);
                    foreach (var (n, v) in src.Moments) loads.AddMoment(n, v * k);
                    foreach (var (e, s) in src.Span) loads.Span.Add((e, s.Scaled(k)));
                    foreach (var (s, q) in src.Surface) loads.Surface.Add((s, q * k));
                }
                return loads;
            }
            List<ImperfectionSet>? variants = null;
            if (sp.Settings.Imperfections && UserCombinations.Ultimate(c.Kind))
            {
                // The first-order axial forces of the combination (the cases' superposition): NEd of its columns.
                var axial = new double[p.Elements.Count];
                foreach (var (ci, k) in terms)
                {
                    var els = sol.Cases[ci].Elements;
                    for (var e = 0; e < axial.Length; e++) axial[e] += k * Nonlinear.AxialOf(els[e]);
                }
                variants = Imperfections.Variants.Select(v => Imperfections.Of(p, axial, v.Direction, $"imp/{c.Key}|{v.Name}")).ToList();
                if (variants[0].Force <= 0) { variants = null; noColumns = true; }
            }
            if (variants == null)
            {
                if (!every) continue;
                var loads = Loads($"nl/{c.Key}");
                jobs.Add((loads, r => new NonlinearSet(c.Key, c.Name, c.Kind, terms, loads, r) { Base = c.Key }));
                continue;
            }
            var sum = terms.Aggregate(Vec3.Zero, (a, t) => a + sol.Cases[t.Item1].SumLoads * t.Item2);
            for (var i = 0; i < variants.Count; i++)
            {
                var (vn, dir) = Imperfections.Variants[i];
                var loads = Loads($"nl/{c.Key}|{vn}");
                foreach (var (n, f) in variants[i].Loads.Nodal) loads.AddNodal(n, f);
                var h = Math.Abs(Vec3.Dot(sum, dir));
                var imperfection = variants[i];
                jobs.Add((loads, r => new NonlinearSet($"{c.Key}|{vn}", $"{c.Name} + φ({Signed(vn)})", c.Kind, terms, loads, r)
                {
                    Base = c.Key, Variant = vn, Imperfection = imperfection, Neglectable = Math.Abs(sum.Z) > 1e-9 && h >= 0.15 * Math.Abs(sum.Z),
                }));
            }
        }
        var results = FemParallel.Map(jobs, j => Nonlinear.Solve(p, j.Loads, options), FemParallel.BytesFor(sol.Profile, sol.Dofs), cancel);
        var o = jobs.Select((j, i) => j.Make(results[i])).ToList();
        var names = new Func<IEnumerable<int>, string>(ks => string.Join(", ", ks.Select(k => p.Elements[k].Name ?? p.Elements[k].Id).Distinct().Take(6)));
        if (sp.Settings.SecondOrder)
        {
            notes.Add("Second order (P-Δ): each static combination solved with the bars' geometric stiffness of their axial forces, iterated; the cases stay first order. Along a bar the moment keeps its first-order shape between its ends: its own buckling is its check's (EN 1993-1-1 6.3).");
            if (!sp.Settings.Imperfections)
                notes.Add("The global imperfections (EN 1993-1-1 5.3.2) are off: analysis.setOptions {imperfections: true} adds them to each ultimate combination as ±X and ±Y variants, or give them as loads (equivalent horizontal forces φ·NEd).");
            if (p.Shells.Count > 0) notes.Add("The shells have no geometric stiffness: second order and buckling are the bars' only.");
            var amp = o.Where(s => s.Ok).MaxBy(s => s.Result.Amplification);
            if (amp != null && amp.Result.Amplification > 1 + 1e-6) notes.Add($"Second order amplifies the sway most in „{amp.Name}”: × {amp.Result.Amplification:0.###}.");
        }
        else if (every) notes.Add("Each static combination solved on its own (one-way members, supports that lift): the cases stay linear, every member in, every support seated.");
        if (o.Where(s => s.Imperfection != null).ToList() is { Count: > 0 } imp)
        {
            var first = imp[0].Imperfection!;
            string Along(bool x)
            {
                var g = imp.Where(s => s.Variant!.EndsWith(x ? 'X' : 'Y')).Select(s => s.Imperfection!.Governing).MaxBy(v => v.Phi);
                return g.Phi > 0 ? $"φ up to 1/{1 / g.Phi:0} along {(x ? "X" : "Y")}{(g.M > 0 ? $" (m = {g.M})" : "")}" : $"no column along {(x ? "X" : "Y")}";
            }
            notes.Add($"Global sway imperfections (EN 1993-1-1 5.3.2; the same form for concrete, EN 1992-1-1 5.2; timber by EN 1995-1-1 5.4.4): each ultimate combination solved four times, ±X and ±Y, with φ·NEd at the top and −φ·NEd at the foot of each compressed column, NEd of its first-order result; h = {first.H:0.##} m, αh = {first.AlphaH:0.###}, {Along(true)}, {Along(false)}. A combination's results are the envelope of its four.");
            if (sp.Model.Shells.Any(x => x.Source.Kind == "wall") && p.Shells.Count > 0)
                notes.Add("The shell walls' vertical load is not tilted: the imperfections are the bar columns' only (give the walls' as loads where they matter).");
            var neglect = imp.Where(s => s.Neglectable).ToList();
            if (neglect.Count > 0)
                notes.Add($"EN 1993-1-1 5.3.2(4): in {neglect.Select(s => s.Base).Distinct().Count()} ultimate combination{(neglect.Select(s => s.Base).Distinct().Count() == 1 ? "" : "s")} the horizontal loads are at least 0.15 of the vertical ones along {(neglect.Any(s => s.Variant!.EndsWith('X')) && neglect.Any(s => s.Variant!.EndsWith('Y')) ? "X and Y" : neglect[0].Variant!.EndsWith('X') ? "X" : "Y")}: the sway imperfections could be left out there; they are kept, on the safe side.");
        }
        else if (sp.Settings.Imperfections && noColumns)
            notes.Add("Global imperfections on, but no column (a bar within 10° of the vertical) is in compression: none added.");
        foreach (var s in o.Where(s => !s.Ok))
            notes.Add($"„{s.Name}”: {s.Result.Message} Left out of the envelopes and the checks.");
        foreach (var s in o.Where(s => s.Ok && !s.Result.Converged))
            notes.Add($"„{s.Name}”: {s.Result.Message} Its last iteration is taken.");
        var slack = o.Where(s => s.Ok && s.Result.Slack.Count > 0).ToList();
        if (slack.Count > 0)
        {
            var all = slack.SelectMany(s => s.Result.Slack).Distinct().ToList();
            var count = slack.Select(s => s.Base).Distinct().Count();
            notes.Add($"One-way members slack in {count} combination{(count == 1 ? "" : "s")}: {names(all)}{(all.Select(k => p.Elements[k].Member ?? p.Elements[k].Id).Distinct().Count() > 6 ? "…" : "")}.");
        }
        var lifted = o.Where(s => s.Ok && s.Result.Lifted.Count > 0).ToList();
        if (lifted.Count > 0)
        {
            var count = lifted.Select(s => s.Base).Distinct().Count();
            notes.Add($"Supports lifted in {count} combination{(count == 1 ? "" : "s")}: {lifted.SelectMany(s => s.Result.Lifted).Distinct().Count()} node{(lifted.SelectMany(s => s.Result.Lifted).Distinct().Count() == 1 ? "" : "s")}.");
        }
        return o;
    }

    // ------------------------------------------------------------ analysis.run

    JsonObject? NonlinearJson()
    {
        if (NonlinearSets.Count == 0) return null;
        var p = Problem.Problem;
        JsonArray Slack(IEnumerable<int> ks) => new(ks.Distinct().GroupBy(k => p.Elements[k].Member ?? p.Elements[k].Id).Select(g => (JsonNode)new JsonObject
        {
            ["id"] = g.Key, ["sourceId"] = p.Elements[g.First()].SourceId, ["name"] = p.Elements[g.First()].Name,
        }).ToArray());
        JsonArray Lifted(IEnumerable<int> ns) => new(ns.Distinct().Select(n => (JsonNode)new JsonObject { ["node"] = p.NodeIds[n], ["at"] = V3(p.Nodes[n]) }).ToArray());
        // One row a combination: its own set, or its four variants (§14.14) summed up, the worst of them, each variant listed.
        var rows = new JsonArray();
        foreach (var g in NonlinearSets.GroupBy(s => s.Base))
        {
            var sets = g.ToList();
            var s0 = sets[0];
            var gov = sets.Where(s => s.Ok).MaxBy(s => s.Result.Amplification) ?? s0;
            var failed = sets.FirstOrDefault(s => !s.Ok);
            var row = new JsonObject
            {
                ["key"] = g.Key, ["name"] = s0.Variant == null ? s0.Name : s0.Name[..s0.Name.LastIndexOf(" + φ(", StringComparison.Ordinal)],
                ["ok"] = failed == null, ["iterations"] = sets.Max(s => s.Result.Iterations), ["converged"] = sets.All(s => s.Result.Converged),
                ["amplification"] = R(gov.Result.Amplification, 6), ["unstable"] = sets.Any(s => s.Result.Unstable), ["message"] = (failed ?? sets.FirstOrDefault(s => s.Result.Message != null) ?? s0).Result.Message,
                ["slack"] = Slack(sets.Where(s => s.Ok).SelectMany(s => s.Result.Slack)),
                ["lifted"] = Lifted(sets.Where(s => s.Ok).SelectMany(s => s.Result.Lifted)),
            };
            if (s0.Variant != null)
            {
                row["governing"] = gov.Variant;
                row["variants"] = new JsonArray(sets.Select(s => (JsonNode)new JsonObject
                {
                    ["variant"] = s.Variant, ["key"] = s.Key, ["name"] = s.Name, ["ok"] = s.Ok, ["iterations"] = s.Result.Iterations, ["converged"] = s.Result.Converged,
                    ["amplification"] = R(s.Result.Amplification, 6), ["phi"] = R(s.Imperfection!.Governing.Phi, 9), ["m"] = s.Imperfection.Governing.M,
                    ["force"] = R(s.Imperfection.Force, 6), ["neglectable"] = s.Neglectable, ["message"] = s.Result.Message,
                }).ToArray());
            }
            rows.Add((JsonNode)row);
        }
        var o = new JsonObject { ["secondOrder"] = Problem.Settings.SecondOrder, ["imperfections"] = ImperfectionsJson(), ["combinations"] = rows };
        return o;
    }

    /// <summary>The global imperfections taken (§14.14): h, αh, the largest φ along X and Y with its m, where 5.3.2(4) would let them go.</summary>
    JsonObject? ImperfectionsJson()
    {
        var imp = NonlinearSets.Where(s => s.Imperfection != null).ToList();
        if (imp.Count == 0) return null;
        JsonObject Along(char axis)
        {
            var g = imp.Where(s => s.Variant!.EndsWith(axis)).Select(s => s.Imperfection!.Governing).MaxBy(v => v.Phi);
            return new JsonObject { ["phi"] = R(g.Phi, 9), ["m"] = g.M };
        }
        var first = imp[0].Imperfection!;
        return new JsonObject
        {
            ["phi0"] = Imperfections.Phi0, ["h"] = R(first.H, 6), ["alphaH"] = R(first.AlphaH, 6), ["x"] = Along('X'), ["y"] = Along('Y'),
            ["timber"] = imp.Any(s => s.Imperfection!.Timber),
            ["neglectable"] = new JsonArray(imp.Where(s => s.Neglectable).GroupBy(s => (s.Base, Axis: s.Variant![1])).Select(x => (JsonNode)new JsonObject
            {
                ["key"] = x.Key.Base, ["name"] = x.First().Name[..x.First().Name.LastIndexOf(" + φ(", StringComparison.Ordinal)], ["direction"] = x.Key.Axis.ToString(),
            }).ToArray()),
        };
    }

    JsonObject? SheetingJson() => Problem.SheetingHold is { } s
        ? new JsonObject { ["members"] = s.Members, ["nodes"] = s.Nodes, ["spans"] = s.Spans, ["skipped"] = new JsonArray(s.Skipped.Select(x => (JsonNode)x).ToArray()) }
        : null;

    JsonObject? SupportsJson()
    {
        if (Problem.Springs is not { } s) return null;
        return new JsonObject
        {
            ["supports"] = s.Supports, ["nodes"] = s.Nodes, ["winkler"] = s.Winkler, ["slabs"] = s.Slabs, ["slabNodes"] = s.SlabNodes, ["compressionOnly"] = s.CompressionOnly,
        };
    }

    // ------------------------------------------------------------ buckling (§14.3)

    readonly Dictionary<string, BucklingResult> _buckling = [];
    FrameSystem? _linear;

    /// <summary>The critical load factors of an ultimate static combination: on the nonlinear states when the analysis solves them so, else first order.</summary>
    public BucklingResult BucklingOf(Combination c, int modes = 3)
    {
        lock (_gate)
        {
            var key = $"{c.Key}#{modes}";
            if (_buckling.TryGetValue(key, out var had)) return had;
            return _buckling[key] = BucklingJob(c, modes)();
        }
    }

    /// <summary>
    /// The buckling of several combinations worked out side by side (§14.20) and kept, those not had yet: each its own
    /// system (its nonlinear states') or the first-order one shared, which its solves only read.
    /// </summary>
    void BucklingOfAll(IEnumerable<Combination> list, int modes, CancellationToken cancel)
    {
        lock (_gate)
        {
            var missing = list.Select(c => (c, Key: $"{c.Key}#{modes}")).Where(x => !_buckling.ContainsKey(x.Key)).DistinctBy(x => x.Key).ToList();
            if (missing.Count < 2) return;
            var jobs = missing.Select(x => BucklingJob(x.c, modes)).ToList();
            var results = FemParallel.Map(jobs, j => j(), FemParallel.BytesFor(Solution?.Profile ?? 0, Solution?.Dofs ?? 0), cancel);
            for (var i = 0; i < missing.Count; i++) _buckling[missing[i].Key] = results[i];
        }
    }

    /// <summary>
    /// A combination's buckling as a job (under <see cref="_gate"/>: what it reads of the run is taken now, the first-order
    /// system built once): the job then builds and solves on its own.
    /// </summary>
    Func<BucklingResult> BucklingJob(Combination c, int modes)
    {
        var nl = Failed(c.Key) ? NonlinearSets.First(s => s.Base == c.Key && !s.Ok) : NonlinearFor(c);
        if (nl is { Ok: true, Result.States: { } states })
        {
            var axial = nl.Result.Axial;
            return () =>
            {
                var (sys, fail) = FrameSystem.Build(states);
                return sys == null ? new BucklingResult(false, [], [], 0, false, false, fail?.Refused ?? "A mechanism in its last states.")
                    : Buckling.Solve(sys, axial, modes, tolerance: 1e-6);
            };
        }
        if (nl is { Ok: false })
        {
            var message = nl.Result.Message;
            return () => new BucklingResult(false, [], [], 0, false, true, message);
        }
        if (_linear == null)
        {
            var (sys, fail) = FrameSystem.Build(Problem.Problem);
            _linear = sys ?? throw new CommandException(fail?.Refused ?? "The analysis met a mechanism: analysis.run says where.");
        }
        var linear = _linear;
        var n = Combine(Terms(c)).Elements.Select(Nonlinear.AxialOf).ToArray();
        return () => Buckling.Solve(linear, n, modes, tolerance: 1e-6);
    }

    /// <summary>A mode one member (its element) holds more than half of: that member's own buckling.</summary>
    (string? SourceId, string? Name, double Share)? LocalOf(BucklingResult b, int mode)
    {
        if (mode >= b.Shares.Length) return null;
        var p = Problem.Problem;
        var top = b.Shares[mode].Select((v, k) => (v, k)).GroupBy(x => p.Elements[x.k].SourceId ?? p.Elements[x.k].Id).Select(g => (g.Key, Share: g.Sum(x => x.v), First: g.First().k)).MaxBy(x => x.Share);
        return top.Share > 0.5 ? (top.Key, p.Elements[top.First].Name, top.Share) : null;
    }

    HashSet<string>? _purlins;

    /// <summary>The members the framed roofs name as their cladding's carriers (a steel hall's purlins).</summary>
    HashSet<string> Purlins() => _purlins ??= Problem.Model.Roofs.SelectMany(r => r.Members?.Where(kv => kv.Value == "purlin").Select(kv => kv.Key) ?? []).ToHashSet();

    /// <summary>The frame's own buckling: the first mode no one member holds.</summary>
    (double? Alpha, int Mode) GlobalOf(BucklingResult b)
    {
        for (var i = 0; i < b.Alpha.Length; i++) if (LocalOf(b, i) == null) return (b.Alpha[i], i + 1);
        return (null, 0);
    }

    /// <summary>The lowest global αcr of the ultimate static combinations (or its lower bound), with its combination: what the steel checks say (§14.3).</summary>
    public (Combination Combination, double Alpha, bool Bound)? LowestGlobal(int modes = 6, CancellationToken cancel = default)
    {
        (Combination, double, bool)? best = null;
        BucklingOfAll(UltimateStatic(), modes, cancel);
        foreach (var c in UltimateStatic())
        {
            var b = BucklingOf(c, modes);
            if (!b.Ok || b.Alpha.Length == 0) continue;
            var g = GlobalOf(b);
            var (a, bound) = g.Alpha is { } x ? (x, false) : (b.Alpha[^1], true);
            if (best == null || a < best.Value.Item2) best = (c, a, bound);
        }
        return best;
    }

    /// <summary>
    /// The global αcr of the heaviest ultimate static combination (the most vertical load: the lowest αcr as a rule), or its
    /// lower bound when every mode found is one member's: what the steel checks say of EN 1993-1-1 5.2.1(3) at the cost of one
    /// buckling solve (§14.3; analysis.buckling gives every combination's).
    /// </summary>
    public (Combination Combination, double Alpha, bool Bound)? HeaviestGlobal()
    {
        var cases = Solution!.Cases;
        var c = UltimateStatic().Where(x => !Failed(x.Key))
            .MaxBy(x => x.Factors.Sum(f => cases.FirstOrDefault(r => r.Id == f.Case.Id) is { } r ? -f.Value * r.SumLoads.Z : 0));
        if (c == null) return null;
        var b = BucklingOf(c, 6);
        if (!b.Ok || b.Alpha.Length == 0) return null;
        var g = GlobalOf(b);
        return g.Alpha is { } a ? (c, a, false) : (c, b.Alpha[^1], true);
    }

    /// <summary>EN 1993-1-1 5.2.1(3) and 5.2.2(5B) for an elastic analysis: first order will do, the sway effects amplified, second order needed, unstable.</summary>
    public static string ClassOf(double? alpha) => alpha switch
    {
        null => "none",
        >= 10 => "first-order",
        >= 3 => "amplify",
        >= 1 => "second-order",
        _ => "unstable",
    };

    /// <summary>The ultimate static combinations buckling is worked out for (those left out of the envelopes too, said).</summary>
    List<Combination> UltimateStatic() => Combinations.Where(c => UserCombinations.Ultimate(c.Kind)).ToList();

    /// <summary>{combination?, modes?}: each ultimate combination's αcr (§14.3), its class and amplifier.</summary>
    public JsonObject BucklingJson(string? combination, int modes, CancellationToken cancel = default)
    {
        if (!Ok) throw new CommandException(Solution?.Mechanism != null ? "The analysis met a mechanism: analysis.run says where." : "The analysis did not run: analysis.run says why.");
        modes = Math.Clamp(modes, 1, 12);
        var list = combination == null ? UltimateStatic()
            : [Combinations.FirstOrDefault(c => c.Key == combination) ?? throw new CommandException($"There is no static combination „{combination}”.")];
        var arr = new JsonArray();
        (string Key, double Alpha)? lowest = null;
        BucklingOfAll(list, modes, cancel);
        foreach (var c in list)
        {
            var b = BucklingOf(c, modes);
            var g = GlobalOf(b);
            double? a1 = b.Alpha.Length > 0 ? b.Alpha[0] : null;
            // The class by the frame's own (global) buckling; when every mode found is one member's, the last of them bounds it from below.
            var judged = g.Alpha ?? (b.Alpha.Length > 0 ? b.Alpha[^1] : null);
            if (judged is { } v && (lowest == null || v < lowest.Value.Alpha)) lowest = (c.Key, v);
            arr.Add((JsonNode)new JsonObject
            {
                ["key"] = c.Key, ["name"] = c.Name, ["ok"] = b.Ok, ["alpha"] = Arr(b.Alpha, 6),
                ["modes"] = new JsonArray(b.Alpha.Select((a, i) => (JsonNode)new JsonObject
                {
                    ["alpha"] = R(a, 6), ["local"] = LocalOf(b, i) is { } lo ? new JsonObject
                    {
                        ["sourceId"] = lo.SourceId, ["name"] = lo.Name, ["share"] = R(lo.Share, 4),
                        // A roof's purlin (§14.15): its sheeting may brace it (analysis.setSheeting {purlins: true}).
                        ["purlin"] = lo.SourceId != null && Purlins().Contains(lo.SourceId),
                    } : null,
                }).ToArray()),
                ["global"] = g.Alpha is { } ga ? R(ga, 6) : null, ["globalAbove"] = g.Alpha == null && b.Alpha.Length > 0 ? R(b.Alpha[^1], 6) : null,
                ["class"] = b.Ok ? ClassOf(judged) : "unstable",
                ["amplifier"] = judged is { } x && x > 1 ? R(1 / (1 - 1 / x), 6) : null, ["compressed"] = b.Compressed, ["converged"] = b.Converged, ["message"] = b.Message,
            });
        }
        var notes = new JsonArray(
            "Linear buckling of the bars (the shells have no geometric stiffness): (K + αcr·Kg(NEd)) φ = 0, NEd of each ultimate combination; αcr ≥ 10: first order will do, 3 to 10: second order or the sway effects × 1/(1 − 1/αcr), below 3: second order (EN 1993-1-1 5.2.1(3), 5.2.2(5B)).",
            "Only the compressed bars' geometric stiffness is taken (the tension's stiffening left out, on the safe side). A mode one member holds more than half of is that member's own buckling (its check's, EN 1993-1-1 6.3): the class is the frame's first other mode's.");
        if (NonlinearSets.Any(s => s.Variant != null))
            notes.Add((JsonNode)"With the global imperfections on, a combination's buckling is worked out on its +X variant (its states and axial forces): the imperfections' forces are small and horizontal.");
        if (Problem.Problem.Elements.Any(e => e.Material == "concrete"))
            notes.Add((JsonNode)"Concrete bars at their uncracked stiffness: EN 1992-1-1 5.8.2(6) asks for second-order effects over 10 %, judged with the cracked stiffness.");
        return new JsonObject
        {
            ["ok"] = true, ["combinations"] = arr,
            ["lowest"] = lowest is { } lo ? new JsonObject { ["key"] = lo.Key, ["alpha"] = R(lo.Alpha, 6), ["class"] = ClassOf(lo.Alpha) } : null,
            ["notes"] = notes,
        };
    }

    /// <summary>{combination, mode?, scale?}: a buckling mode's shape for the 3D view, as a natural mode's (bars' polylines, shells' nodes displaced).</summary>
    public JsonObject BucklingModeJson(string combination, int mode, double? scaleArg)
    {
        if (!Ok) throw new CommandException("The analysis did not run: analysis.run says why.");
        var c = Combinations.FirstOrDefault(x => x.Key == combination) ?? throw new CommandException($"There is no static combination „{combination}”.");
        if (mode < 1 || mode > 12) throw new CommandException("A buckling mode is 1 to 12.");
        var b = BucklingOf(c, Math.Max(6, mode));
        if (!b.Ok) throw new CommandException(b.Message ?? "No buckling for that combination.");
        if (mode < 1 || mode > b.Alpha.Length) throw new CommandException(b.Alpha.Length == 0 ? "No buckling under that combination: no bar is in compression." : $"There is no mode {mode}: 1 to {b.Alpha.Length}.");
        var u = b.Modes[mode - 1];
        var p = Problem.Problem;
        var r = _linear is { } sys && NonlinearFor(c) is not { Ok: true } ? sys.FromDisplacements("buckling", u)
            : FrameSystem.Build(NonlinearFor(c)!.Result.States!).System!.FromDisplacements("buckling", u);
        var pts = Problem.Members.SelectMany(e => new[] { e.Pi, e.Pj }).Concat(p.Shells.SelectMany(s => s.Nodes.Select(x => p.Nodes[x]))).ToList();
        var extent = pts.Count == 0 ? 1 : Math.Max((new Vec3(pts.Max(x => x.X), pts.Max(x => x.Y), pts.Max(x => x.Z)) - new Vec3(pts.Min(x => x.X), pts.Min(x => x.Y), pts.Min(x => x.Z))).Length, 1);
        var scale = scaleArg ?? 0.15 * extent;
        var lines = new JsonArray();
        for (var k = 0; k < Problem.Members.Count; k++)
        {
            var e = Problem.Members[k];
            var list = new JsonArray();
            for (var i = 0; i <= 10; i++)
            {
                var x = e.L * i / 10;
                var (du, dv, dw) = MemberDeflection(k, r.Elements, x);
                list.Add((JsonNode)V3(e.Pi + e.Ex * x + (e.Ex * du + e.Ey * dv + e.Ez * dw) * scale, 4));
            }
            lines.Add((JsonNode)new JsonObject { ["frame"] = e.Id, ["sourceId"] = e.SourceId, ["base"] = new JsonArray(V3(e.Pi, 4), V3(e.Pj, 4)), ["points"] = list });
        }
        var shells = new JsonArray();
        foreach (var g in p.Shells.GroupBy(s => s.Group ?? ""))
        {
            var nodes = g.SelectMany(s => s.Nodes).Distinct().ToList();
            var local = nodes.Select((x, i) => (x, i)).ToDictionary(t => t.x, t => t.i);
            shells.Add((JsonNode)new JsonObject
            {
                ["id"] = g.Key, ["sourceId"] = g.First().SourceId,
                ["positions"] = Arr(nodes.SelectMany(x => new[] { p.Nodes[x].X, p.Nodes[x].Y, p.Nodes[x].Z }), 4),
                ["displaced"] = Arr(nodes.SelectMany(x => { var v = p.Nodes[x] + new Vec3(u[x * 6], u[x * 6 + 1], u[x * 6 + 2]) * scale; return new[] { v.X, v.Y, v.Z }; }), 4),
                ["faces"] = new JsonArray(g.SelectMany(s => s.Count == 4 ? s.Nodes.Select(x => local[x]) : s.Nodes.Select(x => local[x]).Append(local[s.Nodes[2]])).Select(x => (JsonNode)x).ToArray()),
            });
        }
        return new JsonObject
        {
            ["combination"] = c.Key, ["name"] = c.Name, ["mode"] = mode, ["alpha"] = R(b.Alpha[mode - 1], 6), ["class"] = ClassOf(b.Alpha[0]),
            ["scale"] = scale, ["max"] = 1, ["polylines"] = lines, ["shells"] = shells,
        };
    }
}
