using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using Combination = Lapin.Analysis.Combinations.Combination;

namespace Lapin.Analysis.Solver;

/// <summary>
/// One linear static analysis of the model (docs/plan-fem.md §9): the solver's model and loads (<see cref="StructuralProblem"/>),
/// solved once for every case (<see cref="FrameSolver"/>), the combinations of S0 by superposition and the envelopes of the
/// ultimate and serviceability ones. Derived, never stored: kept in memory for the model's revision only.
/// </summary>
public sealed class AnalysisRun
{
    public const double Balanced = 1e-6;

    public required LoadsReport Report { get; init; }
    public required StructuralProblem Problem { get; init; }
    public FrameSolution? Solution { get; init; }
    public required List<Combination> Combinations { get; init; }
    public required List<string> Notes { get; init; }
    public double TotalMs { get; init; }
    public bool Ok => Solution is { Ok: true };

    sealed class Hit(long revision, AnalysisRun run)
    {
        public long Revision { get; } = revision;
        public AnalysisRun Run { get; } = run;
    }
    static readonly ConditionalWeakTable<IModelView, Hit> Cache = new();

    /// <summary>The analysis at the model's revision: worked out the first time it is asked for, then kept until the model changes.</summary>
    public static AnalysisRun Of(IQueryContext q)
    {
        var m = q.Model;
        if (Cache.TryGetValue(m, out var hit) && hit.Revision == m.Revision) return hit.Run;
        var run = Compute(q);
        Cache.AddOrUpdate(m, new Hit(m.Revision, run));
        return run;
    }

    static AnalysisRun Compute(IQueryContext q)
    {
        var sw = Stopwatch.StartNew();
        var rep = LoadsReport.Compute(q);
        var (combos, comboNotes) = global::Lapin.Analysis.Combinations.Of(rep);
        var sp = StructuralProblem.Build(q.Model, rep);
        var notes = new List<string>(sp.Notes);
        FrameSolution? sol = null;
        if (sp.Refused.Count == 0)
        {
            sp.Problem.Cases.AddRange(sp.Cases.Select(c => c.Loads));
            try { sol = FrameSolver.Solve(sp.Problem); }
            catch (InvalidOperationException ex) { notes.Add(ex.Message); }
        }
        if (sp.Refused.Count == 0 && sp.Problem.Elements.Count == 0)
            notes.Add(sp.Model.Shells.Any(x => x.Source.Kind == "wall")
                ? "No bars: the slabs' loads go straight into the load-bearing walls under them, their reactions; the walls and slabs themselves are analysed from S2."
                : "No bars and no load-bearing walls: nothing carries the loads (S1 analyses bars; walls and slabs come with S2).");

        notes.Add("Stage S1: the bars carry everything; the slabs only carry loads to them (the 45° rule), the load-bearing walls are line supports (every node of theirs held in translation, free to turn). Shells are analysed from S2.");
        notes.Add("Bars as Timoshenko beams, their shear areas from the section (EN 1993-1-1 §6.2.6(3) for steel, 5/6·A for a rectangle).");
        if (sp.Model.Frames.Any(f => f.Eccentric)) notes.Add($"{sp.Model.Frames.Count(f => f.Eccentric)} bars with eccentric ends, joined to their nodes by rigid arms.");
        var pinned = sp.Releases.Values.Count(r => r.Source.StartsWith("pinned: "));
        var strong = sp.Releases.Values.Count(r => r.Source.StartsWith("pinned about"));
        if (pinned > 0) notes.Add($"{pinned} bar end piece{(pinned == 1 ? "" : "s")} pinned by default (the members a connection joins); analysis.setReleases changes them.");
        if (strong > 0) notes.Add($"{strong} timber roof member piece{(strong == 1 ? "" : "s")} pinned at their ends about their strong axis by default, held across it (S1 has no deck to hold the roof sideways); analysis.setReleases changes them.");
        if (sol is { Springs: > 0 } s) notes.Add($"{s.Springs} node{(s.Springs == 1 ? "" : "s")} free to turn without moving anything (only pinned ends meet there): held by a spring that takes no force.");
        if (sol?.Refused is { } refused) notes.Add(refused);
        foreach (var c in sp.Cases)
        {
            var code = rep.Cases.First(x => x.Id == c.Loads.Id).Code;
            if (c.Unmapped > 1e-6)
                notes.Add($"„{code}”: {R(c.Unmapped, 3)} kN of {c.UnmappedLoads} load{(c.UnmappedLoads == 1 ? "" : "s")} find nothing under them (no bar, slab or wall within reach) and are left out: {string.Join(", ", c.UnmappedSources.Take(6).Select(id => q.Model.Get(id)?.Name ?? id))}{(c.UnmappedSources.Count > 6 ? "…" : "")}.");
            if (c.WallsOwn > 1e-6) notes.Add($"„{code}”: the load-bearing walls' own weight ({R(c.WallsOwn, 2)} kN) goes straight to what holds them in S1, outside the bars' sums.");
        }
        if (sp.Cases.Sum(c => c.Spread) is var spread and > 0)
            notes.Add($"{spread} stretch{(spread == 1 ? "" : "es")} of line loads on a slab with no bar under {(spread == 1 ? "it" : "them")}: shared by the slab piece's corners as the point loads are, integrated along {(spread == 1 ? "it" : "them")}.");
        if (sp.Cases.Sum(c => c.OnCorners) is var corners and > 0)
            notes.Add($"{corners} point load{(corners == 1 ? "" : "s")} on a slab with no bar under {(corners == 1 ? "it" : "them")}: shared by the slab piece's corners (barycentric, bilinear on a quadrilateral), statically consistent.");
        if (sp.Cases.Any(c => c.OnRoofs > 0))
            notes.Add("Area loads on a framed roof: each rafter (or truss top chord; purlins where there are none) takes the load over its tributary width, half way to its neighbours (an edge one to the roof's edge); snow per horizontal projection (q·w·cos α along a sloping bar), other loads per true area.");
        if (sol is { Ok: true })
            foreach (var r in sol.Cases.Where(r => r.Balance > Balanced))
                notes.Add($"„{rep.Cases.First(x => x.Id == r.Id).Code}”: the reactions do not balance the loads (off by {r.Balance:0.###e+0} of them).");
        notes.AddRange(comboNotes);
        return new AnalysisRun { Report = rep, Problem = sp, Solution = sol, Combinations = combos, Notes = notes, TotalMs = sw.Elapsed.TotalMilliseconds };
    }

    static double R(double v, int digits = 6) => Math.Round(v, digits);
    static JsonArray V3(Vec3 v, int digits = 6) => new(R(v.X, digits), R(v.Y, digits), R(v.Z, digits));
    static JsonArray Arr(IEnumerable<double> xs, int digits = 6) => new(xs.Select(x => (JsonNode)R(x, digits)).ToArray());

    // ------------------------------------------------------------ analysis.run

    public JsonObject RunJson()
    {
        var p = Problem.Problem;
        var o = new JsonObject
        {
            ["ok"] = Ok,
            ["dofs"] = Solution?.Dofs ?? 0,
            ["elements"] = p.Elements.Count,
            ["nodes"] = p.Elements.SelectMany(e => new[] { e.I, e.J }).Distinct().Count(),
            ["solveMs"] = R(Solution?.SolveMs ?? 0, 2),
            ["totalMs"] = R(TotalMs, 2),
            ["profile"] = Solution?.Profile ?? 0,
        };
        var cases = new JsonArray();
        foreach (var c in Report.Cases)
        {
            var mc = Problem.Cases.FirstOrDefault(x => x.Loads.Id == c.Id);
            var r = Solution?.Cases.FirstOrDefault(x => x.Id == c.Id);
            cases.Add((JsonNode)new JsonObject
            {
                ["id"] = c.Id, ["code"] = c.Code,
                ["sumLoads"] = V3(r?.SumLoads ?? mc?.Loads.Sum ?? default),
                ["sumReactions"] = r != null ? V3(r.SumReactions) : null,
                ["balance"] = r != null ? r.Balance : null,
                ["balanced"] = r != null ? r.Balance <= Balanced : null,
                ["unmapped"] = R(mc?.Unmapped ?? 0),
            });
        }
        o["cases"] = cases;
        o["combinations"] = new JsonArray(Combinations.Select(c => (JsonNode)new JsonObject { ["key"] = c.Key, ["kind"] = c.Kind, ["name"] = c.Name }).ToArray());
        if (Solution?.Mechanism is { } mech)
        {
            var node = p.Nodes[mech.Node];
            o["mechanism"] = new JsonObject
            {
                ["node"] = p.NodeIds[mech.Node], ["at"] = V3(node), ["dof"] = FrameSolver.DofName(mech.Dof),
                ["elements"] = new JsonArray(mech.Elements.Select(k => (JsonNode)p.Elements[k].Id).ToArray()),
                ["sources"] = new JsonArray(mech.Elements.Select(k => p.Elements[k].SourceId).Distinct().Select(x => (JsonNode)x!).ToArray()),
                ["names"] = new JsonArray(mech.Elements.Select(k => p.Elements[k].Name).Distinct().Select(x => (JsonNode)x!).ToArray()),
                ["moving"] = new JsonArray(mech.Moving.Select(n => (JsonNode)new JsonObject { ["node"] = p.NodeIds[n], ["at"] = V3(p.Nodes[n]) }).ToArray()),
                ["movingElements"] = new JsonArray(mech.MovingElements.Select(k => (JsonNode)p.Elements[k].Id).ToArray()),
                ["movingNames"] = new JsonArray(mech.MovingElements.Select(k => p.Elements[k].Name).Distinct().Select(x => (JsonNode)x!).ToArray()),
                ["message"] = $"A mechanism at node {p.NodeIds[mech.Node]} ({R(node.X, 3)}, {R(node.Y, 3)}, {R(node.Z, 3)}), {FrameSolver.DofName(mech.Dof)}: nothing holds it" +
                    (mech.Elements.Count > 0 ? $" ({string.Join(", ", mech.Elements.Select(k => p.Elements[k].Name).Distinct().Take(6))})." : " (no bar reaches it)."),
            };
        }
        if (Problem.Refused.Count > 0)
            o["refused"] = new JsonArray(Problem.Refused.Select(r => (JsonNode)new JsonObject { ["id"] = r.Frame.Id, ["sourceId"] = r.Frame.Source.SourceId, ["name"] = r.Frame.Source.Name, ["reason"] = r.Reason }).ToArray());
        o["notes"] = new JsonArray(Notes.Select(n => (JsonNode)n).ToArray());
        return o;
    }

    // ------------------------------------------------------------ what a result is of

    /// <summary>A case, a combination or an envelope's combinations, each as factors on the cases.</summary>
    sealed record Selection(string Kind, string Key, string Name, List<(string Key, string Name, List<(int Case, double Factor)> Terms)> Sets);

    /// <summary>What a result is asked of: a case (its id or code), a combination (its key) or an envelope (uls, sls); one bar's element or piece, or all.</summary>
    public sealed record Ask(string? Case, string? Combination, string? Envelope, string? ElementId = null);

    Selection Select(Ask ask)
    {
        var (caseArg, comboArg, envArg) = (ask.Case, ask.Combination, ask.Envelope);
        if (!Ok) throw new CommandException(Solution?.Mechanism != null ? "The analysis met a mechanism: analysis.run says where." : "The analysis did not run: analysis.run says why.");
        var cases = Solution!.Cases;
        int CaseIndex(string id) => cases.FindIndex(c => c.Id == id);
        if (caseArg is { } cid)
        {
            var c = Report.Cases.FirstOrDefault(x => x.Id == cid) ?? Report.Cases.FirstOrDefault(x => x.Code == cid) ?? throw new CommandException($"There is no load case „{cid}”.");
            return new Selection("case", c.Id, c.Code, [(c.Id, c.Code, [(CaseIndex(c.Id), 1.0)])]);
        }
        List<(int, double)> Terms(Combination c) => c.Factors.Select(f => (CaseIndex(f.Case.Id), f.Value)).Where(x => x.Item1 >= 0).ToList();
        if (comboArg is { } key)
        {
            var c = Combinations.FirstOrDefault(x => x.Key == key) ?? throw new CommandException($"There is no combination „{key}”.");
            return new Selection("combination", c.Key, c.Name, [(c.Key, c.Name, Terms(c))]);
        }
        if (envArg is { } env)
        {
            var list = env switch
            {
                "uls" => Combinations.Where(c => c.Kind == "uls").ToList(),
                "sls" => Combinations.Where(c => c.Kind.StartsWith("sls")).ToList(),
                _ => throw new CommandException("An envelope is „uls” or „sls”."),
            };
            if (list.Count == 0) throw new CommandException($"There are no {env.ToUpperInvariant()} combinations.");
            return new Selection("envelope", env, env == "uls" ? "ULS envelope" : "SLS envelope", list.Select(c => (c.Key, c.Name, Terms(c))).ToList());
        }
        throw new CommandException("Say a case, a combination or an envelope (uls, sls).");
    }

    sealed class State
    {
        public required double[] U;
        public required Dictionary<int, double[]> Reactions;
        public required ElementResult[] Elements;
    }

    State Combine(List<(int Case, double Factor)> terms)
    {
        var cases = Solution!.Cases;
        var p = Problem.Problem;
        var u = new double[p.Nodes.Count * 6];
        var reactions = new Dictionary<int, double[]>();
        foreach (var (ci, k) in terms)
        {
            var c = cases[ci];
            for (var i = 0; i < u.Length; i++) u[i] += c.U[i] * k;
            foreach (var (n, r) in c.Reactions)
            {
                var acc = reactions.TryGetValue(n, out var had) ? had : reactions[n] = new double[6];
                for (var d = 0; d < 6; d++) acc[d] += r[d] * k;
            }
        }
        var els = new ElementResult[p.Elements.Count];
        for (var e = 0; e < els.Length; e++) els[e] = ElementResult.Combine(terms.Select(t => (cases[t.Case].Elements[e], t.Factor)));
        return new State { U = u, Reactions = reactions, Elements = els };
    }

    List<int> ElementsFor(string? id)
    {
        var p = Problem.Problem;
        if (id == null) return Enumerable.Range(0, p.Elements.Count).ToList();
        var list = Enumerable.Range(0, p.Elements.Count).Where(k => p.Elements[k].Id == id || p.Elements[k].SourceId == id).ToList();
        return list.Count > 0 ? list : throw new CommandException("That element is not a bar of the analysis.");
    }

    // ------------------------------------------------------------ analysis.results

    /// <summary>{case | combination | envelope, elementId?}: the analysis at the model's revision, its results.</summary>
    public static JsonObject Results(IQueryContext q, JsonObject a) =>
        Of(q).Results(new Ask((string?)a["case"], (string?)a["combination"], (string?)a["envelope"], (string?)a["elementId"]));

    public JsonObject Results(Ask ask)
    {
        var sel = Select(ask);
        var p = Problem.Problem;
        var els = ElementsFor(ask.ElementId);
        var nodes = (ask.ElementId != null ? els.SelectMany(k => new[] { p.Elements[k].I, p.Elements[k].J }) : p.Elements.SelectMany(e => new[] { e.I, e.J })).Distinct().OrderBy(n => n).ToList();
        var states = sel.Sets.Select(s => Combine(s.Terms)).ToList();
        var envelope = sel.Kind == "envelope";
        var o = new JsonObject
        {
            ["kind"] = sel.Kind, ["key"] = sel.Key, ["name"] = sel.Name,
            ["units"] = new JsonObject { ["length"] = "m", ["rotation"] = "rad", ["force"] = "kN", ["moment"] = "kN·m" },
        };
        if (envelope) o["combinations"] = new JsonArray(sel.Sets.Select(s => (JsonNode)s.Key).ToArray());

        // The nodes' displacements.
        var nodeArr = new JsonArray();
        (string Node, double Value)? maxDisp = null;
        foreach (var n in nodes)
        {
            var ds = states.Select(s => Enumerable.Range(0, 6).Select(d => s.U[n * 6 + d]).ToArray()).ToList();
            foreach (var d in ds)
            {
                var v = Math.Sqrt(d[0] * d[0] + d[1] * d[1] + d[2] * d[2]);
                if (maxDisp == null || v > maxDisp.Value.Value) maxDisp = (p.NodeIds[n], v);
            }
            var no = new JsonObject { ["id"] = p.NodeIds[n], ["at"] = V3(p.Nodes[n]) };
            if (envelope) { no["min"] = Arr(Enumerable.Range(0, 6).Select(d => ds.Min(x => x[d])), 9); no["max"] = Arr(Enumerable.Range(0, 6).Select(d => ds.Max(x => x[d])), 9); }
            else no["d"] = Arr(ds[0], 9);
            nodeArr.Add((JsonNode)no);
        }
        o["nodes"] = nodeArr;

        // The reactions.
        var nodeSet = nodes.ToHashSet();
        var reactArr = new JsonArray();
        foreach (var n in states.SelectMany(s => s.Reactions.Keys).Distinct().OrderBy(n => n))
        {
            if (ask.ElementId != null && !nodeSet.Contains(n)) continue;
            var rs = states.Select(s => s.Reactions.TryGetValue(n, out var r) ? r : new double[6]).ToList();
            var ro = new JsonObject { ["node"] = p.NodeIds[n], ["at"] = V3(p.Nodes[n]) };
            if (envelope) { ro["min"] = Arr(Enumerable.Range(0, 6).Select(d => rs.Min(x => x[d]))); ro["max"] = Arr(Enumerable.Range(0, 6).Select(d => rs.Max(x => x[d]))); }
            else ro["f"] = Arr(rs[0]);
            reactArr.Add((JsonNode)ro);
        }
        o["reactions"] = reactArr;

        // The bars.
        var frames = new JsonArray();
        var maxima = new (string Frame, double X, double Value)?[6];
        var minima = new (string Frame, double X, double Value)?[6];
        (string Frame, double X, double Value)? maxDefl = null;
        foreach (var k in els)
        {
            var e = p.Elements[k];
            var fo = new JsonObject { ["id"] = e.Id, ["sourceId"] = e.SourceId, ["name"] = e.Name, ["length"] = R(e.L) };
            var stations = states.Select(s => Stations.Along(e, s.Elements[k])).ToList();
            var arr = new JsonArray();
            for (var i = 0; i < Stations.Count; i++)
            {
                var so = new JsonObject { ["x"] = R(stations[0][i].X) };
                for (var q = 0; q < 6; q++)
                    so[Section.Names[q]] = envelope ? new JsonArray(R(stations.Min(st => st[i][q])), R(stations.Max(st => st[i][q]))) : R(stations[0][i][q]);
                arr.Add((JsonNode)so);
            }
            fo["stations"] = arr;
            var ext = new JsonObject();
            var extremes = states.Select(s => Stations.Extremes(e, s.Elements[k])).ToList();
            for (var q = 0; q < 6; q++)
            {
                var hi = extremes.MaxBy(x => x[q].Max)![q];
                var lo = extremes.MinBy(x => x[q].Min)![q];
                ext[Section.Names[q]] = new JsonObject { ["max"] = R(hi.Max), ["xMax"] = R(hi.XMax), ["min"] = R(lo.Min), ["xMin"] = R(lo.XMin) };
                if (maxima[q] == null || hi.Max > maxima[q]!.Value.Value) maxima[q] = (e.Id, hi.XMax, hi.Max);
                if (minima[q] == null || lo.Min < minima[q]!.Value.Value) minima[q] = (e.Id, lo.XMin, lo.Min);
            }
            fo["extremes"] = ext;
            // The largest deflection (of 21 points along it): the axis' displacement square to it, from the chord between its displaced ends.
            var (dx, dv) = (0.0, 0.0);
            foreach (var s in states)
                for (var i = 0; i <= 20; i++)
                {
                    var x = e.L * i / 20;
                    var (_, v, w) = Stations.Deflection(e, s.Elements[k], x);
                    var ul = s.Elements[k].ULocal;
                    var (v0, w0, v1, w1) = (ul[1], ul[2], ul[7], ul[8]);
                    var t = x / e.L;
                    var (rv, rw) = (v - (v0 + (v1 - v0) * t), w - (w0 + (w1 - w0) * t));
                    var d = Math.Sqrt(rv * rv + rw * rw);
                    if (d > dv) (dx, dv) = (x, d);
                }
            fo["deflection"] = new JsonObject { ["max"] = R(dv, 9), ["x"] = R(dx) };
            if (maxDefl == null || dv > maxDefl.Value.Value) maxDefl = (e.Id, dx, dv);
            frames.Add((JsonNode)fo);
        }
        o["frames"] = frames;
        var mx = new JsonObject();
        if (maxDisp is { } md) mx["displacement"] = new JsonObject { ["node"] = md.Node, ["value"] = R(md.Value, 9) };
        if (maxDefl is { } mdf) mx["deflection"] = new JsonObject { ["frame"] = mdf.Frame, ["x"] = R(mdf.X), ["value"] = R(mdf.Value, 9) };
        for (var q = 0; q < 6; q++)
            if (maxima[q] is { } h && minima[q] is { } l)
                mx[Section.Names[q]] = new JsonObject
                {
                    ["max"] = new JsonObject { ["frame"] = h.Frame, ["x"] = R(h.X), ["value"] = R(h.Value) },
                    ["min"] = new JsonObject { ["frame"] = l.Frame, ["x"] = R(l.X), ["value"] = R(l.Value) },
                };
        o["maxima"] = mx;
        return o;
    }

    // ------------------------------------------------------------ analysis.diagram

    public static readonly string[] Quantities = [.. Section.Names, "deformed"];

    /// <summary>{case | combination | envelope, quantity, scale?, elementId?}: the analysis at the model's revision, a diagram of it.</summary>
    public static JsonObject Diagram(IQueryContext q, JsonObject a) =>
        Of(q).Diagram(new Ask((string?)a["case"], (string?)a["combination"], (string?)a["envelope"], (string?)a["elementId"]), (string?)a["quantity"],
            a["scale"] is JsonNode sn && Json.Number(sn) is { } sv && double.IsFinite(sv) ? sv : null);

    public JsonObject Diagram(Ask ask, string? quantityArg, double? scaleArg)
    {
        var sel = Select(ask);
        var quantity = quantityArg ?? throw new CommandException($"Say the quantity: {string.Join(", ", Quantities)}.");
        if (!Quantities.Contains(quantity)) throw new CommandException($"„{quantity}” is not a quantity: one of {string.Join(", ", Quantities)}.");
        var p = Problem.Problem;
        var els = ElementsFor(ask.ElementId);
        var states = sel.Sets.Select(s => Combine(s.Terms)).ToList();
        var envelope = sel.Kind == "envelope";
        var pts = p.Elements.SelectMany(e => new[] { e.Pi, e.Pj }).ToList();
        var extent = pts.Count == 0 ? 1 : Math.Max((new Vec3(pts.Max(x => x.X), pts.Max(x => x.Y), pts.Max(x => x.Z)) - new Vec3(pts.Min(x => x.X), pts.Min(x => x.Y), pts.Min(x => x.Z))).Length, 1);
        var polylines = new JsonArray();
        var scale = scaleArg;
        string unit;
        if (quantity == "deformed")
        {
            unit = "m";
            var maxD = 0.0;
            var samples = new Dictionary<(int, int), List<(double X, double U, double V, double W)>>();
            for (var si = 0; si < states.Count; si++)
                foreach (var k in els)
                {
                    var e = p.Elements[k];
                    var list = new List<(double, double, double, double)>();
                    for (var i = 0; i <= 20; i++)
                    {
                        var x = e.L * i / 20;
                        var (u, v, w) = Stations.Deflection(e, states[si].Elements[k], x);
                        list.Add((x, u, v, w));
                        maxD = Math.Max(maxD, Math.Sqrt(u * u + v * v + w * w));
                    }
                    samples[(si, k)] = list;
                }
            var s = scale ?? (maxD > 1e-15 ? 0.05 * extent / maxD : 1);
            foreach (var ((si, k), list) in samples)
            {
                var e = p.Elements[k];
                var line = new JsonObject
                {
                    ["frame"] = e.Id, ["sourceId"] = e.SourceId, ["base"] = new JsonArray(V3(e.Pi), V3(e.Pj)),
                    ["points"] = new JsonArray(list.Select(x => (JsonNode)V3(e.Pi + e.Ex * x.X + (e.Ex * x.U + e.Ey * x.V + e.Ez * x.W) * s)).ToArray()),
                    ["values"] = Arr(list.Select(x => Math.Sqrt(x.U * x.U + x.V * x.V + x.W * x.W)), 9),
                };
                if (envelope) line["combination"] = sel.Sets[si].Key;
                polylines.Add((JsonNode)line);
            }
            return new JsonObject { ["kind"] = sel.Kind, ["key"] = sel.Key, ["quantity"] = quantity, ["unit"] = unit, ["scale"] = s, ["max"] = R(maxD, 9), ["polylines"] = polylines };
        }

        var q = Section.IndexOf(quantity);
        unit = q is 0 or 1 or 2 ? "kN" : "kN·m";
        var values = new Dictionary<int, (List<double> X, List<double> Lo, List<double> Hi)>();
        var max = 0.0;
        foreach (var k in els)
        {
            var e = p.Elements[k];
            var xs = new List<double>();
            for (var i = 0; i <= 20; i++) xs.Add(e.L * i / 20);
            foreach (var s in states)
            {
                foreach (var b in Stations.Breaks(e, s.Elements[k])) { xs.Add(Math.Max(0, b - 1e-6)); xs.Add(Math.Min(e.L, b + 1e-6)); }
                foreach (var ex in Stations.Extremes(e, s.Elements[k])) { xs.Add(ex.XMax); xs.Add(ex.XMin); }
            }
            xs = xs.Distinct().OrderBy(x => x).ToList();
            var (lo, hi) = (new List<double>(), new List<double>());
            foreach (var x in xs)
            {
                var vs = states.Select(s => Stations.At(e, s.Elements[k], x)[q]).ToList();
                lo.Add(vs.Min());
                hi.Add(vs.Max());
                max = Math.Max(max, Math.Max(Math.Abs(vs.Min()), Math.Abs(vs.Max())));
            }
            values[k] = (xs, lo, hi);
        }
        var sc = scale ?? (max > 1e-12 ? 0.1 * extent / max : 1);
        foreach (var k in els)
        {
            var e = p.Elements[k];
            // Moments on the tension side: Mz (σ = −Mz·y/I33) along −local 2, My (σ = My·z/I22) along local 3; forces along local 2 (Vz along local 3).
            var dir = q switch { 5 => -e.Ey, 4 or 2 => e.Ez, _ => e.Ey };
            var (xs, lo, hi) = values[k];
            void Line(List<double> vs, string? bound)
            {
                var line = new JsonObject
                {
                    ["frame"] = e.Id, ["sourceId"] = e.SourceId, ["base"] = new JsonArray(V3(e.Pi), V3(e.Pj)),
                    ["points"] = new JsonArray(xs.Select((x, i) => (JsonNode)V3(e.Pi + e.Ex * x + dir * (vs[i] * sc))).ToArray()),
                    ["values"] = Arr(vs),
                };
                if (bound != null) line["bound"] = bound;
                polylines.Add((JsonNode)line);
            }
            if (envelope) { Line(hi, "max"); Line(lo, "min"); }
            else Line(hi, null);
        }
        return new JsonObject { ["kind"] = sel.Kind, ["key"] = sel.Key, ["quantity"] = quantity, ["unit"] = unit, ["scale"] = sc, ["max"] = R(max), ["polylines"] = polylines };
    }
}
