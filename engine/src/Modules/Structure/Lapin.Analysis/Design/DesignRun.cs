using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Analysis.Seismic;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;

namespace Lapin.Analysis.Design;

/// <summary>A bar's checks: its piece (the analytical model's member), element, name, material and kind, its checks and what reinforcement it needs.</summary>
public sealed record MemberDesign(int Index, string Id, string SourceId, string Name, string Material, string? Grade, string Kind, string? StoreyId, List<Check> Checks, Required? Need, List<string> Notes)
{
    public Check? Governing => Checks.Where(c => c.Done).MaxBy(c => c.Utilisation) ?? Checks.MaxBy(c => c.Utilisation);
    public double Utilisation => Governing?.Utilisation ?? 0;
}

/// <summary>A concrete slab's or wall's reinforcement (m²/m at each node, four layers) and its checks.</summary>
public sealed record SurfaceDesign(string Group, string SourceId, string Name, string Kind, string Material, string? StoreyId, int[] Nodes, double[][] Layers, List<Check> Checks, List<string> Notes)
{
    public static readonly string[] LayerNames = ["asxBottom", "asyBottom", "asxTop", "asyTop"];
    public Check? Governing => Checks.Where(c => c.Done).MaxBy(c => c.Utilisation);
    public double Utilisation => Governing?.Utilisation ?? 0;
}

/// <summary>A masonry (or concrete) wall's forces at its foot, middle and head in the governing combinations, and its checks.</summary>
public sealed record WallDesign(string Group, string SourceId, string Name, string Material, string? StoreyId, string Direction, double Length, double Thickness, double Height,
    List<WallForceSet> Sets, List<Check> Checks, MasonryWall? Wall, List<string> Notes)
{
    public Check? Governing => Checks.Where(c => c.Done).MaxBy(c => c.Utilisation);
    public double Utilisation => Governing?.Utilisation ?? 0;
}

/// <summary>
/// S4 (docs/plan-fem.md §12): every bar, slab and wall of the analysis checked by its material's norm, derived from the
/// analysis' results on every ask and kept in memory for the model's revision only (never stored), as the analysis itself.
/// </summary>
public sealed class DesignRun
{
    public required AnalysisRun Run { get; init; }
    public List<MemberDesign> Members { get; } = [];
    public List<SurfaceDesign> Surfaces { get; } = [];
    public List<WallDesign> Walls { get; } = [];
    public List<string> Notes { get; } = [];
    public bool Ok { get; private set; }
    public double Ms { get; private set; }

    sealed class Hit(long revision, DesignRun run)
    {
        public long Revision { get; } = revision;
        public DesignRun Run { get; } = run;
    }
    static readonly ConditionalWeakTable<IModelView, Hit> Cache = new();

    public static DesignRun Of(IQueryContext q)
    {
        var m = q.Model;
        if (Cache.TryGetValue(m, out var hit) && hit.Revision == m.Revision) return hit.Run;
        var run = new DesignRun { Run = AnalysisRun.For(q) };
        run.Compute(q.Model);
        Cache.AddOrUpdate(m, new Hit(m.Revision, run));
        return run;
    }

    readonly Dictionary<string, (StructuralMaterial Mat, Entity Entity)?> _materials = [];
    (StructuralMaterial Mat, Entity Entity)? MaterialOf(IModelView m, string? id)
    {
        if (id == null) return null;
        if (_materials.TryGetValue(id, out var had)) return had;
        var e = (m.Get(id) is { Class: IfcClass.Material } own ? own : Materials.Find(m, id)) ?? new Entity($"lapin-material-{id}", IfcClass.Material, id, []);
        return _materials[id] = (StructuralMaterials.Of(m, e), e);
    }

    void Compute(IModelView m)
    {
        var sw = Stopwatch.StartNew();
        var run = Run;
        if (!run.Ok) { Notes.Add("The analysis did not run: analysis.run says why."); return; }
        if (!run.Combinations.Any(c => UserCombinations.Ultimate(c.Kind))) { Notes.Add("There are no ULS combinations: nothing to check."); return; }
        Ok = true;
        Notes.Add("Indicative checks (docs/plan-fem.md §12): the Eurocodes' recommended values (kept by the Romanian annexes where Lapin could read them) and CR 6-2013; the signed design remains the engineer's.");
        var project = DesignSettings.Project(m);
        Run.SnowDuration = DesignSettings.Choice(project, "snowDuration", DesignSettings.SnowDurations);
        try { Bars(m, project); }
        catch (CommandException ex) { Notes.Add(ex.Message); }
        try { Shells(m, project); }
        catch (CommandException ex) { Notes.Add(ex.Message); }
        Ms = sw.Elapsed.TotalMilliseconds;
    }

    // ------------------------------------------------------------ bars

    enum End { Free, Fixed, Pinned, Joined }

    void Bars(IModelView m, JsonObject? project)
    {
        var sp = Run.Problem;
        var p = sp.Problem;
        var members = sp.Members;
        // How many bars and shells meet at each node.
        var at = new Dictionary<int, int>();
        foreach (var e in members) { at[e.I] = at.GetValueOrDefault(e.I) + 1; at[e.J] = at.GetValueOrDefault(e.J) + 1; }
        var shellNodes = p.Shells.SelectMany(s => s.Nodes).ToHashSet();
        var slabNodes = sp.Model.Shells.Where(s => s.Source.Kind == "slab").SelectMany(s => s.Nodes).ToHashSet();
        var serviceDefault = (int)(DesignSettings.Num(project, "serviceClass") ?? 1);
        var cv = Values(project);
        var unchecked_ = new HashSet<string>();
        for (var k = 0; k < members.Count && k < sp.Frames.Count; k++)
        {
            var e = members[k];
            var f = sp.Frames[k];
            var src = f.Source;
            var own = DesignSettings.Element(m, src.SourceId);
            var mat = MaterialOf(m, src.MaterialId);
            var notes = new List<string>();
            var storey = src.StoreyId ?? Spatial.StoreyOf(m, src.SourceId)?.Id;
            if (mat is not { } mm) continue;
            var kind = mm.Mat.Kind;
            if (kind is not ("steel" or "concrete" or "timber")) { unchecked_.Add($"{src.Name} ({mm.Mat.Name})"); continue; }
            var sets = Run.MemberForces(k);
            var l = e.L;
            var vertical = Math.Abs((e.Pj - e.Pi).Normalized().Z) > Math.Sqrt(0.5);
            End EndOf(int node, bool released)
            {
                if (released) return End.Pinned;
                if (p.Held[node] is { } h) return h[3] && h[4] && h[5] ? End.Fixed : End.Pinned;
                return at.GetValueOrDefault(node) <= 1 && !shellNodes.Contains(node) ? End.Free : End.Joined;
            }
            (double K, string How) Factor(int r)
            {
                var (a, b) = (EndOf(e.I, e.ReleaseI[r]), EndOf(e.J, e.ReleaseJ[r]));
                if (a == End.Free || b == End.Free) return (2.0, "a cantilever (one end free): k = 2.0");
                if (a == End.Fixed && b == End.Fixed) return (0.7, "fixed at both ends: k = 0.7");
                if (a == End.Fixed || b == End.Fixed) return (0.85, "fixed at one end: k = 0.85");
                return (1.0, "k = 1.0 (ends joined or pinned)");
            }
            var (ky, hy) = Factor(5);
            var (kz, hz) = Factor(4);
            if (DesignSettings.Num(own, "ky") is { } oky) (ky, hy) = (oky, "k given");
            if (DesignSettings.Num(own, "kz") is { } okz) (kz, hz) = (okz, "k given");
            var lcrY = DesignSettings.Num(own, "lcrY") ?? ky * l;
            var lcrZ = DesignSettings.Num(own, "lcrZ") ?? kz * l;
            var cantilever = EndOf(e.I, false) == End.Free || EndOf(e.J, false) == End.Free;
            var restrained = !vertical && slabNodes.Contains(f.I) && slabNodes.Contains(f.J);
            var llt = DesignSettings.Num(own, "llt") ?? (restrained ? 0 : l);
            var lengths = $"Buckling lengths: strong axis {Checks.F(lcrY, "0.##")} m ({hy}), weak axis {Checks.F(lcrZ, "0.##")} m ({hz}); a braced frame assumed (a sway frame's k > 1 is the user's to give).";
            if (restrained && DesignSettings.Num(own, "llt") == null) notes.Add("Laterally restrained by the slab it carries: no lateral torsional buckling.");
            var shape = src.Section.Shape;
            List<Check> checks;
            Required? need = null;
            switch (kind)
            {
                case "steel":
                {
                    var props = SectionProperties.Of(shape);
                    if (props == null || mm.Mat.Fy is not { } fy) { notes.Add("No fy or no section properties: not checked."); checks = []; break; }
                    var member = new SteelMember(shape, props, fy.Value, mm.Mat.E?.Value ?? 210e9, l, lcrY, lcrZ, llt)
                        { LengthsSource = lengths, Method = DesignSettings.Choice(project, "steelInteraction", DesignSettings.SteelMethods) };
                    checks = SteelDesign.Check(member, sets);
                    break;
                }
                case "concrete":
                {
                    if (shape.Kind != "rectangle") { notes.Add($"A „{shape.Kind}” concrete section: only rectangles are checked."); checks = []; break; }
                    if (mm.Mat.Fc is not { } fc) { notes.Add("No fck (the material's name has no class such as C25/30): not checked."); checks = []; break; }
                    var c = cv with { Fck = fc.Value };
                    var column = src.Kind == "column" || vertical;
                    var (sys, sk) = SystemOf(own, k, e, cantilever, members);
                    var cm = new ConcreteMember(shape.Width, shape.Depth, l, column, c, lcrY, lcrZ, sk, sys) { LengthsSource = lengths };
                    var (pst, pbars) = Provided(m, src.SourceId);
                    cm = cm with { ProvidedStirrups = pst, ProvidedBars = column ? pbars : null };
                    (checks, need) = ConcreteMembers.Check(cm, sets);
                    break;
                }
                default:
                {
                    if (shape.Kind != "rectangle") { notes.Add($"A „{shape.Kind}” timber section: only rectangles are checked."); checks = []; break; }
                    var tc = TimberDesign.Find(mm.Mat.Grade);
                    if (tc == null) { notes.Add("No strength class (C24, GL24h…) in the material's name: not checked."); checks = []; break; }
                    var sc = (int)(DesignSettings.Num(own, "serviceClass") ?? serviceDefault);
                    var lef = DesignSettings.Num(own, "llt");
                    var tm = new TimberMember(shape.Width, shape.Depth, l, tc, sc, lcrY, lcrZ, restrained && lef == null ? 0 : lef) { LengthsSource = lengths, Cantilever = cantilever };
                    checks = TimberDesign.Check(tm, sets);
                    var snow = Run.SnowDuration;
                    checks = checks.Select(c => c.Note != null && c.Note.Contains("kmod") ? c with { Note = c.Note + $" Snow taken as a {snow}-term action (EN 1995-1-1 Table 2.2; the project's choice, analysis.setDesign snowDuration)." } : c).ToList();
                    if (restrained && lef == null) checks.RemoveAll(x => x.Key == "timber.ltb");
                    checks.Add(TimberDesign.Deflection(tm, TimberDeflections(k, TimberDesign.Kdef(sc))));
                    break;
                }
            }
            Members.Add(new MemberDesign(k, e.Id, src.SourceId, src.Name, kind, mm.Mat.Grade, src.Kind, storey, checks, need, notes));
        }
        Sway(sp);
        if (Members.Any(x => x.Material == "steel")) SecondOrderNote();
        if (unchecked_.Count > 0) Notes.Add($"Not checked (a material Lapin has no rules for): {string.Join(", ", unchecked_.Take(8))}{(unchecked_.Count > 8 ? "…" : "")}.");
    }

    /// <summary>
    /// A note when the model looks unbraced along X or Y (§12.10): columns, but no wall and no diagonal bar working along that
    /// direction. The buckling lengths assume a braced frame; a sway frame's (k > 1) are the user's to give.
    /// </summary>
    void Sway(StructuralProblem sp)
    {
        var bars = sp.Members.Select(e => (e.Pj - e.Pi).Normalized()).ToList();
        if (!bars.Any(d => Math.Abs(d.Z) > 0.97)) return;
        var unbraced = new List<string>();
        foreach (var (name, ax) in new[] { ("X", new Vec3(1, 0, 0)), ("Y", new Vec3(0, 1, 0)) })
        {
            var walls = sp.Model.Shells.Any(s => s.Source.Kind == "wall" && s.Source.Line is { Count: >= 2 } line
                && Math.Abs(Vec2.Dot((line[^1] - line[0]).Normalized(), new Vec2(ax.X, ax.Y))) > Math.Cos(Math.PI / 4));
            var braces = bars.Any(d => Math.Abs(d.Z) is > 0.2 and < 0.97 && Math.Abs(Vec3.Dot(new Vec3(d.X, d.Y, 0), ax)) > 0.5);
            if (!walls && !braces) unbraced.Add(name);
        }
        if (unbraced.Count > 0)
            Notes.Add($"No wall or bracing along {string.Join(" and ", unbraced)}: the frame looks unbraced (sway) that way. The buckling lengths assume a braced frame; give the columns' own (k > 1, analysis.setDesign ky / kz or lcrY / lcrZ).");
    }

    /// <summary>
    /// EN 1993-1-1 5.2.1(3) for the steel checks (§14.3): with second order off, αcr of the heaviest ultimate combination when
    /// it is under 10 (second-order effects must be taken: P-Δ); with it on, that the forces are second order and the members'
    /// own buckling stays their check's.
    /// </summary>
    void SecondOrderNote()
    {
        if (Run.Problem.Settings.SecondOrder)
        {
            Notes.Add("Second order (P-Δ) on: the steel members' forces are second order; their own buckling (EN 1993-1-1 6.3.1) stays with their buckling lengths, the global imperfections (5.3.2) are not added yet.");
            return;
        }
        (Combinations.Combination Combination, double Alpha, bool Bound)? g;
        try { g = Run.HeaviestGlobal(); }
        catch (CommandException) { return; }
        if (g is not { } x || x.Alpha >= 10) return;
        Notes.Add($"EN 1993-1-1 5.2.1(3): αcr {(x.Bound ? "≥ " : "= ")}{x.Alpha:0.##} under „{x.Combination.Name}”, below 10: second-order effects must be taken (analysis.setOptions secondOrder, or the sway effects × 1/(1 − 1/αcr) when αcr ≥ 3); analysis.buckling gives every combination's.");
    }

    static ConcreteValues Values(JsonObject? project) => new(25e6,
        DesignSettings.Num(project, "fyk") ?? 500e6, DesignSettings.Num(project, "cover") ?? 0.030, DesignSettings.Num(project, "bar") ?? 0.016, DesignSettings.Num(project, "stirrup") ?? 0.008);

    /// <summary>The span/depth system (Table 7.4N): given, or from its ends (a free end a cantilever; an end continuing into another beam in line).</summary>
    static (string Name, double K) SystemOf(JsonObject? own, int k, FrameElement e, bool cantilever, List<FrameElement> members)
    {
        var given = (string?)own?["system"];
        if (given != null) return Sys(given);
        if (cantilever) return Sys("cantilever");
        var dir = (e.Pj - e.Pi).Normalized();
        bool Continues(int node, bool released) => !released && members.Where((x, i) => i != k && (x.I == node || x.J == node))
            .Any(x => Math.Abs(Vec3.Dot((x.Pj - x.Pi).Normalized(), dir)) > 0.9);
        var n = (Continues(e.I, e.ReleaseI[5]) ? 1 : 0) + (Continues(e.J, e.ReleaseJ[5]) ? 1 : 0);
        return Sys(n == 2 ? "interior" : n == 1 ? "end" : "simple");
        static (string, double) Sys(string s) => s switch
        {
            "cantilever" => ("cantilever", 0.4), "end" => ("end span of a continuous beam", 1.3), "interior" => ("interior span", 1.5), "flat" => ("flat slab", 1.2),
            _ => ("simply supported", 1.0),
        };
    }

    /// <summary>What the element's type provides (the rebar module's settings on it): the stirrups' Asw/s and a column's bars.</summary>
    static (double? Stirrups, double? Bars) Provided(IModelView m, string sourceId)
    {
        if (m.RelatingOf(sourceId, IfcClass.RelDefinesByType) is not { } type) return (null, null);
        double? st = null, bars = null;
        if (type.Attrs["stirrups"] is JsonObject s && s["enabled"] is JsonValue ev && ev.TryGetValue<bool>(out var on) && on
            && Json.Number(s["diameter"]) is { } d && Json.Number(s["step"]) is { } step && step > 0)
        {
            var loops = (string?)s["pattern"] switch { "diamond" => 2, "cross" => 3, _ => 1 };
            st = 2 * loops * Math.PI * d * d / 4 / step;
        }
        if (type.Attrs["bars"] is JsonObject b && b["enabled"] is JsonValue bv && bv.TryGetValue<bool>(out var bon) && bon
            && Json.Number(b["diameter"]) is { } bd && Json.Number(b["perFace"]) is { } pf)
            bars = 4 * (Math.Max(2, pf) - 1) * Math.PI * bd * bd / 4;
        return (st, bars);
    }

    /// <summary>Timber's deflections in each characteristic combination: winst, and wfin with kdef on the quasi-permanent part (2.3.2.2).</summary>
    List<(string Key, string Name, double[] Inst, double[] Fin)> TimberDeflections(int member, double kdef)
    {
        var w = Run.MemberCaseDeflections(member);
        var o = new List<(string, string, double[], double[])>();
        var cases = Run.Solution!.Cases;
        foreach (var (key, name, terms) in Run.CharacteristicTerms())
        {
            var inst = new double[21];
            var fin = new double[21];
            foreach (var (ci, k) in terms)
            {
                var id = cases[ci].Id;
                var lc = Run.Report.Cases.First(c => c.Id == id);
                var phi = lc.Permanent ? 1 : lc.Psi.Psi2;
                var wc = w[id];
                for (var i = 0; i < 21; i++) { inst[i] += k * wc[i]; fin[i] += k * wc[i] + kdef * phi * wc[i]; }
            }
            o.Add((key, name, inst, fin));
        }
        return o;
    }

    // ------------------------------------------------------------ shells

    void Shells(IModelView m, JsonObject? project)
    {
        var sp = Run.Problem;
        var p = sp.Problem;
        if (p.Shells.Count == 0) return;
        var byGroup = p.Shells.Select((s, i) => (s, i)).GroupBy(x => x.s.Group ?? "").ToDictionary(g => g.Key, g => g.Select(x => x.i).ToList());
        var cv = Values(project);
        var (sets, durations, seismic, srun) = Run.UlsTerms();
        var keys = Run.ShellKeys();
        List<ShellForces[]>? nodal = null;
        var slabTop = sp.Surfaces.Where(s => s.Value.Kind == "slab").SelectMany(s => byGroup.GetValueOrDefault(s.Key) ?? []).SelectMany(i => p.Shells[i].Nodes).ToHashSet();
        var settings = SeismicSettings.Of(m);
        foreach (var (g, info) in sp.Surfaces.OrderBy(x => x.Value.Name, StringComparer.Ordinal))
        {
            if (!byGroup.TryGetValue(g, out var shells)) continue;
            var srcShell = sp.Model.Shells.FirstOrDefault(s => s.Source.SourceId == info.SourceId);
            var mat = MaterialOf(m, srcShell?.Source.MaterialId);
            if (mat is not { } mm) continue;
            var storey = srcShell?.Source.StoreyId ?? Spatial.StoreyOf(m, info.SourceId)?.Id;
            if (info.Kind == "wall")
                Walls.Add(Wall(m, project, g, info.SourceId, info.Name, mm, storey, shells, sets, seismic, srun, slabTop, settings));
            if (mm.Mat.Kind == "concrete" && mm.Mat.Fc is { } fc)
            {
                nodal ??= Run.UlsShellNodal();
                Surfaces.Add(Reinforcement(g, info, cv with { Fck = fc.Value }, storey, shells, keys, nodal, sets));
            }
        }
    }

    readonly Dictionary<(FrameProblem, int), double[]> _k = new(ReferenceEqualityComparerTuple.Instance);
    double[] Stiffness(FrameProblem p, int s)
    {
        if (_k.TryGetValue((p, s), out var k)) return k;
        return _k[(p, s)] = p.Shells[s].GlobalStiffness();
    }

    sealed class ReferenceEqualityComparerTuple : IEqualityComparer<(FrameProblem, int)>
    {
        public static readonly ReferenceEqualityComparerTuple Instance = new();
        public bool Equals((FrameProblem, int) a, (FrameProblem, int) b) => ReferenceEquals(a.Item1, b.Item1) && a.Item2 == b.Item2;
        public int GetHashCode((FrameProblem, int) x) => HashCode.Combine(RuntimeHelpers.GetHashCode(x.Item1), x.Item2);
    }

    WallDesign Wall(IModelView m, JsonObject? project, string g, string sourceId, string name, (StructuralMaterial Mat, Entity Entity) mm, string? storey, List<int> shells,
        List<(string Key, string Name, List<(int Case, double Factor)> Terms)> sets, List<bool> seismic, SeismicRun? srun, HashSet<int> slabTop, SeismicSettings settings)
    {
        var sp = Run.Problem;
        var p = sp.Problem;
        var e0 = p.Shells[shells[0]];
        var along = e0.Ex;
        var cuts = WallForces.Cuts(p, shells, along);
        var notes = new List<string>();
        var dir = Math.Abs(along.X) >= Math.Cos(Math.PI / 6) ? "X" : Math.Abs(along.Y) >= Math.Cos(Math.PI / 6) ? "Y" : "–";
        var zs = shells.SelectMany(s => p.Shells[s].Nodes).Select(n => p.Nodes[n].Z).ToList();
        var height = zs.Max() - zs.Min();
        var foot = cuts.FirstOrDefault(c => c.Where == "foot");
        var length = foot?.Length ?? 0;
        // Each case's resultants at each cut, then each combination's by its factors.
        // (S5: past the cases, the nonlinear combinations' results, each with its own loads.)
        var perCase = new WallSection[Run.ResultCount][];
        for (var ci = 0; ci < perCase.Length; ci++)
        {
            var loads = Run.LoadsOf(ci).Surface.GroupBy(x => x.Shell).ToDictionary(x => x.Key, x => x.Aggregate(Vec3.Zero, (a, b) => a + b.Q));
            var u = Run.CaseAt(ci).U;
            perCase[ci] = cuts.Select(c => WallForces.Of(p, c, u, s => loads.TryGetValue(s, out var q) ? q : default, s => Stiffness(p, s))).ToArray();
        }
        WallSection[]? aed = null;
        if (srun is { Problem: { } sp2 } && seismic.Any(x => x))
            aed = cuts.Select(c => WallSection.Of(srun.Combine(r => WallForces.Of(sp2, c, r.U, null, s => Stiffness(sp2, s)).ToArray()))).ToArray();
        var fsets = new List<WallForceSet>();
        for (var i = 0; i < sets.Count; i++)
        {
            var sums = new WallSection[cuts.Count];
            foreach (var (ci, k) in sets[i].Terms) for (var c = 0; c < cuts.Count; c++) sums[c] += perCase[ci][c] * k;
            if (seismic[i] && aed != null)
            {
                // The seismic bounds, each the unfavourable side: the axial force less (or more) by AEd, the shear and moments more.
                var sign = sets[i].Key.EndsWith('-') ? -1 : 1;
                for (var c = 0; c < cuts.Count; c++)
                {
                    var (s0, a) = (sums[c], aed[c]);
                    sums[c] = new WallSection(s0.N + sign * a.N, Math.Abs(s0.V) + a.V, Math.Abs(s0.M) + a.M, Math.Abs(s0.MOut) + a.MOut, Math.Abs(s0.VOut) + a.VOut);
                }
            }
            WallSection? At(string w) { var j = cuts.FindIndex(c => c.Where == w); return j >= 0 ? sums[j] : null; }
            if (At("foot") is { } ft) fsets.Add(new WallForceSet(sets[i].Key, sets[i].Name, seismic[i], ft, At("middle"), At("head")));
        }
        MasonryWall? wall = null;
        var checks = new List<Check>();
        if (mm.Mat.Kind == "masonry" && fsets.Count > 0 && length > 1e-6)
        {
            var lower = $"{mm.Entity.Name} {MaterialFields.Of(mm.Entity).Category}".ToLowerInvariant();
            var group = Masonry.GroupOf(lower) ?? Masonry.Find("clay1")!;
            var vals = Masonry.Of(MaterialFields.Of(mm.Entity), group, notes);
            var gr = vals.Of ?? group;
            var fm = gr.Fm;
            var fvk0 = DesignSettings.Num(project, "fvk0") is { } own ? own / 1e6 : gr.Key == "aac" ? 0.15 : fm >= 10 ? 0.30 : 0.20;
            var gm = DesignSettings.Num(project, "gammaM") ?? 2.2;
            var gms = settings.Ag is { } ag && ag <= 0.12 ? Math.Min(gm, 1.65) : gm;
            var headNodes = cuts.FirstOrDefault(c => c.Where == "head")?.Nodes ?? [];
            var rho2 = headNodes.Any(slabTop.Contains) ? 0.75 : 1.0;
            var t = e0.Thickness;
            wall = new MasonryWall(length, t, height, rho2, vals.Fk.Value / 1e6, vals.E.Value / 1e6, vals.Fb, fvk0, gm, gms);
            checks = MasonryDesign.Check(wall, fsets);
            notes.Add($"fk = {Checks.F(wall.Fk, "0.##")} MPa, E = {Checks.F(wall.E, "0")} MPa, fvk0 = {Checks.F(fvk0, "0.##")} MPa (CR 6 Table 4.5, indicative), γM = {Checks.F(gm, "0.##")}{(gms != gm ? $" ({Checks.F(gms, "0.##")} seismic, ag ≤ 0.12 g: CR 6 2.4.2.3.1(2))" : "")}.");
        }
        else if (mm.Mat.Kind == "masonry") notes.Add("No cut through the wall's mesh: not checked.");
        // Keep the governing combinations' forces only: the largest N, |V|, |M| at the foot.
        var keep = new HashSet<string>();
        if (fsets.Count > 0)
        {
            keep.Add(fsets.MaxBy(x => x.Foot.N)!.Key);
            keep.Add(fsets.MaxBy(x => Math.Abs(x.Foot.V))!.Key);
            keep.Add(fsets.MaxBy(x => Math.Abs(x.Foot.M))!.Key);
            foreach (var c in checks) if (c.Combination != null) keep.Add(c.Combination);
        }
        return new WallDesign(g, sourceId, name, mm.Mat.Kind, storey, dir, length, e0.Thickness, height, fsets.Where(x => keep.Contains(x.Key)).ToList(), checks, wall, notes) { };
    }

    SurfaceDesign Reinforcement(string g, (string SourceId, string Name, string Kind, Vec3 Normal) info, ConcreteValues c, string? storey, List<int> shells,
        (string Group, int Node)[] keys, List<ShellForces[]> nodal, List<(string Key, string Name, List<(int Case, double Factor)> Terms)> sets)
    {
        var p = Run.Problem.Problem;
        var t = p.Shells[shells[0]].Thickness;
        var idx = Enumerable.Range(0, keys.Length).Where(i => keys[i].Group == g).ToArray();
        var nodes = idx.Select(i => keys[i].Node).ToArray();
        var layers = Enumerable.Range(0, 4).Select(_ => new double[idx.Length]).ToArray();
        var (dx, dy) = (t - c.Edge, t - c.Edge - c.Bar);
        var slab = info.Kind == "slab";
        var asMin = slab ? ConcreteDesign.MinAs(1, dx, c) : 0.001 * t;
        var fyd = c.Fyd;
        var (worstV, worstVAt, worstVSet) = (0.0, -1, -1);
        var (worstC, worstCAt, worstCSet) = (0.0, -1, -1);
        // The shear is read off the supports' lines (the nodes bars or other surfaces share): there the slab hands it to them, and
        // the values at a point support are the mesh's singularity, not a design shear (punching, 6.4, is not done).
        var supported = p.Elements.SelectMany(e => new[] { e.I, e.J }).Concat(p.Shells.Where(x => x.Group != g).SelectMany(x => x.Nodes)).ToHashSet();
        for (var j = 0; j < idx.Length; j++)
        {
            for (var si = 0; si < nodal.Count; si++)
            {
                var f = nodal[si][idx[j]];
                double[] a;
                if (slab)
                {
                    // The engineer's sign: the bottom in tension positive (the slab's normal up, mechanics' positive on top).
                    var (xb, yb, xt, yt) = ConcreteDesign.WoodArmer(-f.Mx, -f.My, -f.Mxy);
                    a = [ConcreteDesign.Bending(xb, 1, dx, c.Edge, c).As, ConcreteDesign.Bending(yb, 1, dy, c.Edge, c).As, ConcreteDesign.Bending(xt, 1, dx, c.Edge, c).As, ConcreteDesign.Bending(yt, 1, dy, c.Edge, c).As];
                    var v = Math.Sqrt(f.Qx * f.Qx + f.Qy * f.Qy);
                    var (vrdc, _, _) = ConcreteDesign.VRdc(1, dx, Math.Max(asMin, Math.Max(a[0], a[2])), c);
                    if (!supported.Contains(nodes[j]) && v / vrdc > worstV) (worstV, worstVAt, worstVSet) = (v / vrdc, j, si);
                }
                else
                {
                    // A wall (simplified, §12.3): each face's bars from the membrane force and the moment, n tension positive; − the face away from the normal.
                    var z = Math.Max(0.5 * t, t - 2 * c.Edge);
                    a = [Math.Max(0, f.Nx / 2 - f.Mx / z) / fyd, Math.Max(0, f.Ny / 2 - f.My / z) / fyd, Math.Max(0, f.Nx / 2 + f.Mx / z) / fyd, Math.Max(0, f.Ny / 2 + f.My / z) / fyd];
                    var sc = Math.Max(-f.Ny / t + 6 * Math.Abs(f.My) / (t * t), -f.Nx / t + 6 * Math.Abs(f.Mx) / (t * t));
                    if (sc / c.Fcd > worstC) (worstC, worstCAt, worstCSet) = (sc / c.Fcd, j, si);
                }
                for (var l = 0; l < 4; l++) layers[l][j] = Math.Max(layers[l][j], a[l]);
            }
            for (var l = 0; l < 4; l++) layers[l][j] = Math.Max(layers[l][j], slab ? asMin : l % 2 == 1 ? asMin : Math.Max(0.25 * asMin, 0.0005 * t));
        }
        var checks = new List<Check>();
        var max = layers.Select(x => x.Length > 0 ? x.Max() : 0).ToArray();
        var asMax = 0.04 * t;
        checks.Add(new Check("concrete.shellBars", slab ? "Slab reinforcement (Wood–Armer)" : "Wall reinforcement (simplified)", slab ? $"{ConcreteDesign.Norm} 6.1, 9.3.1.1" : $"{ConcreteDesign.Norm} 9.6",
            Math.Max(max[0] + max[2], max[1] + max[3]) / asMax,
            [new("as,x bottom", max[0] * 1e4, "cm²/m"), new("as,y bottom", max[1] * 1e4, "cm²/m"), new("as,x top", max[2] * 1e4, "cm²/m"), new("as,y top", max[3] * 1e4, "cm²/m"), new("as,min", asMin * 1e4, "cm²/m")],
            slab ? "Wood–Armer's moments at each node (the patch-recovered values), the rectangular block on a metre's strip; utilisation: the bars needed over 0.04 Ac."
                 : "Each face's bars from n/2 ± m/z (n tension positive, z the bars' lever arm); bottom: the face away from the wall's normal; minimum 9.6.2 / 9.6.3."));
        if (slab && worstVAt >= 0)
            checks.Add(new Check("concrete.slabShear", "Slab shear without shear reinforcement", $"{ConcreteDesign.Norm} 6.2.2", worstV, [new("vEd/VRd,c", worstV, "")], "v = √(vx² + vy²) on a metre's strip, off the supports' lines (where the slab hands its shear to them); punching (6.4) not checked.")
            { Combination = sets[Math.Min(worstVSet, sets.Count - 1)].Key, CombinationName = sets[Math.Min(worstVSet, sets.Count - 1)].Name, X = 0 });
        if (!slab && worstCAt >= 0)
            checks.Add(new Check("concrete.wallCompression", "Wall compression", $"{ConcreteDesign.Norm} 6.1", worstC, [new("σc/fcd", worstC, "")], "Elastic stress n/t + 6m/t² against fcd; the wall's slenderness not taken.")
            { Combination = sets[Math.Min(worstCSet, sets.Count - 1)].Key, CombinationName = sets[Math.Min(worstCSet, sets.Count - 1)].Name });
        return new SurfaceDesign(g, info.SourceId, info.Name, info.Kind, "concrete", storey, nodes, layers, checks, []);
    }

    // ------------------------------------------------------------ JSON

    static JsonObject Gov(Check? c) => c == null ? new JsonObject() : new JsonObject
    {
        ["key"] = c.Key, ["name"] = c.Name, ["clause"] = c.Clause, ["combination"] = c.Combination, ["combinationName"] = c.CombinationName, ["utilisation"] = Math.Round(c.Utilisation, 4),
    };

    static JsonArray Strings(IEnumerable<string> xs) => new(xs.Select(x => (JsonNode)x).ToArray());

    public JsonObject ChecksJson(string? material, string? storeyId)
    {
        var o = new JsonObject { ["ok"] = Ok, ["indicative"] = true, ["ms"] = Math.Round(Ms, 1) };
        bool Takes(string mat, string? storey) => (material == null || material == mat) && (storeyId == null || storeyId == storey);
        o["members"] = new JsonArray(Members.Where(x => Takes(x.Material, x.StoreyId)).Select(x => (JsonNode)new JsonObject
        {
            ["id"] = x.Id, ["sourceId"] = x.SourceId, ["name"] = x.Name, ["material"] = x.Material, ["grade"] = x.Grade, ["kind"] = x.Kind, ["storeyId"] = x.StoreyId,
            ["utilisation"] = Math.Round(x.Utilisation, 4), ["ok"] = x.Utilisation <= 1 + 1e-9, ["done"] = x.Checks.Any(c => c.Done), ["governing"] = Gov(x.Governing),
        }).ToArray());
        o["surfaces"] = new JsonArray(Surfaces.Where(x => Takes(x.Material, x.StoreyId)).Select(x => (JsonNode)new JsonObject
        {
            ["id"] = x.Group, ["sourceId"] = x.SourceId, ["name"] = x.Name, ["kind"] = x.Kind, ["material"] = x.Material, ["storeyId"] = x.StoreyId,
            ["utilisation"] = Math.Round(x.Utilisation, 4), ["ok"] = x.Utilisation <= 1 + 1e-9, ["governing"] = Gov(x.Governing),
            ["reinforcement"] = new JsonObject(SurfaceDesign.LayerNames.Select((n, i) => KeyValuePair.Create(n, (JsonNode?)Math.Round((x.Layers[i].Length > 0 ? x.Layers[i].Max() : 0) * 1e4, 3)))),
        }).ToArray());
        o["walls"] = new JsonArray(Walls.Where(x => x.Material == "masonry" && Takes("masonry", x.StoreyId)).Select(x => (JsonNode)new JsonObject
        {
            ["id"] = x.Group, ["sourceId"] = x.SourceId, ["name"] = x.Name, ["material"] = x.Material, ["storeyId"] = x.StoreyId,
            ["utilisation"] = Math.Round(x.Utilisation, 4), ["ok"] = x.Utilisation <= 1 + 1e-9, ["governing"] = Gov(x.Governing),
        }).ToArray());
        o["notes"] = Strings(Notes);
        return o;
    }

    /// <summary>One element's checks in detail: each piece's (or surface's, or wall's) checks with their values.</summary>
    public JsonObject CheckJson(string id)
    {
        var ms = Members.Where(x => x.Id == id || x.SourceId == id).ToList();
        var ss = Surfaces.Where(x => x.Group == id || x.SourceId == id).ToList();
        var ws = Walls.Where(x => x.Group == id || x.SourceId == id).ToList();
        if (ms.Count == 0 && ss.Count == 0 && ws.Count == 0) throw new CommandException("That element is not checked (not a bar, slab or wall of the analysis, or of a material without rules).");
        return new JsonObject
        {
            ["id"] = id, ["indicative"] = true,
            ["members"] = new JsonArray(ms.Select(x => (JsonNode)new JsonObject
            {
                ["id"] = x.Id, ["name"] = x.Name, ["material"] = x.Material, ["grade"] = x.Grade, ["kind"] = x.Kind, ["utilisation"] = Math.Round(x.Utilisation, 4), ["governing"] = Gov(x.Governing),
                ["checks"] = new JsonArray(x.Checks.Select(c => (JsonNode)c.ToJson()).ToArray()), ["notes"] = Strings(x.Notes),
                ["reinforcement"] = x.Need is { } n ? NeedJson(n) : null,
            }).ToArray()),
            ["surfaces"] = new JsonArray(ss.Select(x => (JsonNode)new JsonObject
            {
                ["id"] = x.Group, ["name"] = x.Name, ["kind"] = x.Kind, ["utilisation"] = Math.Round(x.Utilisation, 4), ["governing"] = Gov(x.Governing),
                ["checks"] = new JsonArray(x.Checks.Select(c => (JsonNode)c.ToJson()).ToArray()),
            }).ToArray()),
            ["walls"] = new JsonArray(ws.Select(x => (JsonNode)WallJson(x, true)).ToArray()),
        };
    }

    static JsonObject NeedJson(Required n) => new()
    {
        ["bottom"] = Math.Round(n.Bottom * 1e4, 3), ["top"] = Math.Round(n.Top * 1e4, 3), ["total"] = Math.Round(n.Total * 1e4, 3), ["stirrups"] = Math.Round(n.Stirrups * 1e4, 3),
        ["side"] = Math.Round(n.Side * 1e4, 3), ["unit"] = "cm² (stirrups cm²/m)",
    };

    static JsonObject SectionJson(WallSection s) => new()
    {
        ["N"] = Math.Round(s.N, 3), ["V"] = Math.Round(s.V, 3), ["M"] = Math.Round(s.M, 3), ["mOut"] = Math.Round(s.MOut, 4), ["vOut"] = Math.Round(s.VOut, 3),
    };

    JsonObject WallJson(WallDesign x, bool detail)
    {
        var o = new JsonObject
        {
            ["id"] = x.Group, ["sourceId"] = x.SourceId, ["name"] = x.Name, ["material"] = x.Material, ["storeyId"] = x.StoreyId, ["direction"] = x.Direction,
            ["length"] = Math.Round(x.Length, 3), ["thickness"] = Math.Round(x.Thickness, 3), ["height"] = Math.Round(x.Height, 3),
            ["utilisation"] = Math.Round(x.Utilisation, 4), ["governing"] = Gov(x.Governing),
        };
        // The foot's forces in the combinations giving the largest N, |V| and |M|, and each check's resistance.
        if (x.Sets.Count > 0)
        {
            var n = x.Sets.MaxBy(s => s.Foot.N)!;
            var v = x.Sets.MaxBy(s => Math.Abs(s.Foot.V))!;
            var mm = x.Sets.MaxBy(s => Math.Abs(s.Foot.M))!;
            o["N"] = new JsonObject { ["value"] = Math.Round(n.Foot.N, 3), ["combination"] = n.Key, ["combinationName"] = n.Name };
            o["V"] = new JsonObject { ["value"] = Math.Round(Math.Abs(v.Foot.V), 3), ["combination"] = v.Key, ["combinationName"] = v.Name };
            o["M"] = new JsonObject { ["value"] = Math.Round(Math.Abs(mm.Foot.M), 3), ["combination"] = mm.Key, ["combinationName"] = mm.Name };
            o["mOut"] = Math.Round(x.Sets.Max(s => Math.Abs(s.Foot.MOut)), 4);
        }
        double? Res(string key, string value) => x.Checks.FirstOrDefault(c => c.Key == key)?.Values.FirstOrDefault(s => s.Name == value).Value;
        o["NRd"] = Res("masonry.vertical.foot", "NRd");
        o["NRdMiddle"] = Res("masonry.vertical.middle", "NRd");
        o["VRd"] = Res("masonry.shear", "VRd");
        o["MRd"] = Res("masonry.bending", "MRd");
        o["utilisations"] = new JsonObject(x.Checks.Select(c => KeyValuePair.Create(c.Key, (JsonNode?)Math.Round(c.Utilisation, 4))));
        if (detail)
        {
            o["checks"] = new JsonArray(x.Checks.Select(c => (JsonNode)c.ToJson()).ToArray());
            o["sets"] = new JsonArray(x.Sets.Select(s => (JsonNode)new JsonObject
            {
                ["key"] = s.Key, ["name"] = s.Name, ["seismic"] = s.Seismic, ["foot"] = SectionJson(s.Foot),
                ["middle"] = s.Middle is { } mi ? SectionJson(mi) : null, ["head"] = s.Head is { } h ? SectionJson(h) : null,
            }).ToArray());
            o["notes"] = Strings(x.Notes);
        }
        return o;
    }

    /// <summary>The masonry designer's table (§12.5): by storey, each wall's forces at its foot and its resistances, the shears summed by direction.</summary>
    public JsonObject WallsJson(IModelView m)
    {
        var storeys = new JsonArray();
        var order = Spatial.Storeys(m).Select(s => s.Id).ToList();
        foreach (var g in Walls.GroupBy(w => w.StoreyId ?? "").OrderBy(g => order.IndexOf(g.Key)))
        {
            var rows = g.OrderBy(w => w.Direction).ThenBy(w => w.Name, StringComparer.Ordinal).ToList();
            var sum = new JsonObject();
            foreach (var d in new[] { "X", "Y" })
            {
                var ws = rows.Where(w => w.Direction == d && w.Material == "masonry").ToList();
                sum[d] = new JsonObject
                {
                    ["walls"] = ws.Count,
                    ["V"] = Math.Round(ws.Sum(w => w.Sets.Count > 0 ? w.Sets.Max(s => Math.Abs(s.Foot.V)) : 0), 3),
                    ["VRd"] = Math.Round(ws.Sum(w => w.Checks.FirstOrDefault(c => c.Key == "masonry.shear")?.Values.FirstOrDefault(v => v.Name == "VRd").Value ?? 0), 3),
                    ["area"] = Math.Round(ws.Sum(w => w.Length * w.Thickness), 4),
                };
            }
            storeys.Add((JsonNode)new JsonObject
            {
                ["id"] = g.Key, ["name"] = m.Get(g.Key)?.Name ?? "", ["walls"] = new JsonArray(rows.Select(w => (JsonNode)WallJson(w, false)).ToArray()), ["sums"] = sum,
            });
        }
        return new JsonObject { ["ok"] = Ok, ["indicative"] = true, ["storeys"] = storeys, ["notes"] = Strings(Notes) };
    }

    /// <summary>The reinforcement the bars and surfaces need (§12.3), for the rebar module and the interface; one element's when asked.</summary>
    public JsonObject ReinforcementJson(string? id)
    {
        var p = Run.Problem.Problem;
        bool Takes(string a, string b) => id == null || id == a || id == b;
        return new JsonObject
        {
            ["indicative"] = true, ["unit"] = "cm² (bars), cm²/m (stirrups, surfaces)",
            ["members"] = new JsonArray(Members.Where(x => x.Need != null && Takes(x.Id, x.SourceId)).Select(x => (JsonNode)new JsonObject
            {
                ["id"] = x.Id, ["sourceId"] = x.SourceId, ["name"] = x.Name, ["kind"] = x.Kind, ["required"] = NeedJson(x.Need!),
                ["provided"] = x.Checks.FirstOrDefault(c => c.Key is "concrete.stirrupsProvided" or "concrete.barsProvided") is { } pc ? pc.ToJson() : null,
            }).ToArray()),
            ["surfaces"] = new JsonArray(Surfaces.Where(x => Takes(x.Group, x.SourceId)).Select(x =>
            {
                var so = new JsonObject { ["id"] = x.Group, ["sourceId"] = x.SourceId, ["name"] = x.Name, ["kind"] = x.Kind };
                for (var l = 0; l < 4; l++) so[SurfaceDesign.LayerNames[l]] = Math.Round((x.Layers[l].Length > 0 ? x.Layers[l].Max() : 0) * 1e4, 3);
                if (id != null)
                    so["nodes"] = new JsonArray(x.Nodes.Select((n, j) => (JsonNode)new JsonObject
                    {
                        ["at"] = new JsonArray(Math.Round(p.Nodes[n].X, 4), Math.Round(p.Nodes[n].Y, 4), Math.Round(p.Nodes[n].Z, 4)),
                        ["as"] = new JsonArray(Enumerable.Range(0, 4).Select(l => (JsonNode)Math.Round(x.Layers[l][j] * 1e4, 3)).ToArray()),
                    }).ToArray());
                return (JsonNode)so;
            }).ToArray()),
        };
    }

    /// <summary>A surface's required reinforcement at a node (cm²/m), for the contours; null when the surface is not designed.</summary>
    public double? LayerAt(string group, int node, string layer)
    {
        var l = Array.IndexOf(SurfaceDesign.LayerNames, layer);
        if (l < 0) return null;
        var s = Surfaces.FirstOrDefault(x => x.Group == group);
        if (s == null) return null;
        var j = Array.IndexOf(s.Nodes, node);
        return j >= 0 ? s.Layers[l][j] * 1e4 : null;
    }
}
