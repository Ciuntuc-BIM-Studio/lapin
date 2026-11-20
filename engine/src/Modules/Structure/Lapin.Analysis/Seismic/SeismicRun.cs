using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis.Seismic;

/// <summary>A storey of the seismic analysis: its level and top (m), the nodes it holds by their height, and its results.</summary>
public sealed record SeismicStorey(string Id, string Name, double Z, double Top)
{
    public double H => Top - Z;
    public double Mass { get; set; }
    public double[] Shear { get; } = new double[2];
    public double[] Force { get; } = new double[2];
    public double[] Drift { get; } = new double[2];
    public double[] DriftMax { get; } = new double[2];
    public double[] Width { get; } = new double[2];
}

/// <summary>
/// The modal and response spectrum analysis of the model (docs/plan-fem.md §11.2, §11.3), derived, kept for the model's
/// revision only: the static analysis' problem with the cracked stiffness of concrete and masonry (and a rigid diaphragm's
/// penalty where asked and no shell floor gives one), the lumped mass of the seismic combination's gravity loads, the modes
/// (<see cref="Modal"/>), each mode's response to the design spectrum along X and Y, combined by CQC or SRSS, the accidental
/// torsion as a static case per direction added in absolute value, the two directions by the 30 % rule or SRSS; the storeys'
/// masses, shears, forces and drifts with P100's (or EN's) drift checks; and the seismic combination G + ψ2 Q ± AEd for the
/// results (<see cref="AnalysisRun"/>'s envelope „seismic”).
/// </summary>
public sealed class SeismicRun
{
    public required SeismicSettings Settings { get; init; }
    public required Spectrum Spectrum { get; init; }
    public required AnalysisRun Static { get; init; }
    public FrameProblem? Problem { get; private set; }
    public FrameSystem? System { get; private set; }
    public ModalResult? Modal { get; private set; }
    public double[] NodeMass { get; private set; } = [];
    public List<string> Notes { get; } = [];
    public List<SeismicStorey> Storeys { get; } = [];
    public double[] Base { get; } = new double[2];
    public double[] Equivalent { get; } = new double[2];
    public double[] T1 { get; } = new double[2];
    public int CloseModes { get; private set; }
    public double Ms { get; private set; }
    /// <summary>Where the time went (ms), for the timing tests.</summary>
    public string Timings { get; private set; } = "";
    /// <summary>The static cases of the gravity loads of the seismic combination (G + ψ2 Q), each by its index in the static solution and its factor.</summary>
    public List<(int Case, double Factor)> Gravity { get; } = [];
    public string GravityName { get; private set; } = "";
    public bool Ok => Modal is { Modes.Count: > 0 } && Settings.Given;

    CaseResult[] _modes = [];
    readonly CaseResult?[] _torsion = new CaseResult?[2];
    double[,] _a = new double[0, 2];
    (int I, int J, double W)[] _pairs = [];

    sealed class Hit(long revision, SeismicRun run)
    {
        public long Revision { get; } = revision;
        public SeismicRun Run { get; } = run;
    }
    static readonly ConditionalWeakTable<IModelView, Hit> Cache = new();

    public static SeismicRun Of(IQueryContext q)
    {
        var m = q.Model;
        if (Cache.TryGetValue(m, out var hit) && hit.Revision == m.Revision) return hit.Run;
        var run = Compute(q);
        Cache.AddOrUpdate(m, new Hit(m.Revision, run));
        return run;
    }

    static SeismicRun Compute(IQueryContext q)
    {
        var settings = SeismicSettings.Of(q.Model);
        var run = new SeismicRun { Settings = settings, Spectrum = Spectrum.Of(settings), Static = AnalysisRun.Of(q) };
        run.Work(q.Model);
        return run;
    }

    void Work(IModelView m)
    {
        var sw = Stopwatch.StartNew();
        if (!Settings.Given) { Notes.Add("No seismic parameters: give the site's design ground acceleration ag (analysis.setSeismic), as the code's map gives it."); return; }
        if (!Static.Ok) { Notes.Add("The static analysis did not run: analysis.run says why."); return; }
        var sp = Static.Problem;
        var src = sp.Problem;

        // The gravity loads of the seismic combination: G + ψ2,i Q (P100-1/2013 §3.3 / CR 0-2012; EN 1998-1 3.2.4 with φ = 1).
        var terms = new List<string>();
        var rep = Static.Report;
        for (var ci = 0; ci < Static.Solution!.Cases.Count; ci++)
        {
            var c = rep.Cases.FirstOrDefault(x => x.Id == Static.Solution.Cases[ci].Id);
            if (c == null || rep.Empty(c)) continue;
            double f;
            if (c.Permanent) f = 1;
            else if (!c.Variable || c.Wind) f = 0;
            else if (c.Exclusive == LoadCases.Snow || c.Category == Categories.Snow)
                f = c.WindDirection != null ? 0 : c.OwnPsi?.Psi2 ?? Settings.SnowPsi2Used;
            else f = c.Psi.Psi2;
            if (f <= 0) continue;
            Gravity.Add((ci, f));
            terms.Add(Math.Abs(f - 1) < 1e-9 ? c.Code : $"{f:0.##} {c.Code}");
        }
        GravityName = string.Join(" + ", terms);

        // The modal problem: the static one, concrete and masonry cracked.
        var p = new FrameProblem();
        for (var n = 0; n < src.Nodes.Count; n++) p.AddNode(src.Nodes[n], src.NodeIds[n], src.Held[n] is { } h ? (bool[])h.Clone() : null);
        double K(string? kind) => kind == "concrete" ? Settings.CrackedConcrete : kind == "masonry" ? Settings.CrackedMasonry : 1;
        p.Elements.AddRange(src.Elements.Select(e => e.Scaled(K(e.Material))));
        p.Shells.AddRange(src.Shells.Select(e => e.Scaled(K(e.Material))));
        if (Settings.CrackedConcrete < 1 || Settings.CrackedMasonry < 1)
            Notes.Add($"Cracked stiffness: concrete × {Settings.CrackedConcrete:0.##}, masonry × {Settings.CrackedMasonry:0.##} (EN 1998-1 4.3.1(7), P100-1/2013 ch. 8: half the uncracked stiffness unless worked out); steel and timber as they are.");

        // The masses, from the gravity loads as the static solver puts them on the nodes.
        var (sys0, fail0) = FrameSystem.Build(p);
        if (sys0 == null) { Notes.Add(fail0?.Mechanism != null ? "The modal model meets a mechanism: analysis.run says where." : fail0?.Refused ?? "The modal model could not be assembled."); return; }
        var fz = new double[p.Nodes.Count];
        foreach (var (ci, f) in Gravity)
        {
            var load = sp.Cases.First(c => c.Loads.Id == Static.Solution.Cases[ci].Id).Loads;
            var v = sys0.LoadVector(load);
            for (var n = 0; n < fz.Length; n++) fz[n] += f * v[n * 6 + 2];
        }
        NodeMass = fz.Select(x => Math.Max(0, -x) / Spectrum.G).ToArray();
        var upward = fz.Count(x => x > 1e-9);
        if (upward > 0) Notes.Add($"{upward} node{(upward == 1 ? "" : "s")} with an upward net gravity load: no mass there.");

        // The storeys (§11.3): each from its level to the next (or its own height).
        var storeys = sp.Model.Storeys.OrderBy(s => s.Num("elevation")).ToList();
        var zTop = p.Nodes.Count > 0 ? p.Nodes.Max(x => x.Z) : 0;
        for (var i = 0; i < storeys.Count; i++)
        {
            var z = storeys[i].Num("elevation");
            if (z > zTop - 0.05) break;
            // The storey's height is level to level (the last one's own height, else up to the highest node).
            var top = i + 1 < storeys.Count ? storeys[i + 1].Num("elevation") : storeys[i].Num("height") is var hh and > 0 ? Math.Max(z + hh, zTop) : zTop;
            top = Math.Max(top, z + 0.1);
            Storeys.Add(new SeismicStorey(storeys[i].Id, storeys[i].Name, z, top));
        }

        // A rigid diaphragm where asked, on the levels with no shell floor (§11.2; never where a slab is a shell).
        if (Settings.Diaphragm == "rigid") Diaphragms(p, sp);

        var (sys, fail) = p.Links.Count > 0 ? FrameSystem.Build(p) : (sys0, null);
        if (sys == null) { Notes.Add(fail?.Mechanism != null ? "The modal model meets a mechanism: analysis.run says where." : fail?.Refused ?? "The modal model could not be assembled."); return; }
        Problem = p;
        System = sys;
        if (NodeMass.Length < p.Nodes.Count) NodeMass = [.. NodeMass, .. new double[p.Nodes.Count - NodeMass.Length]];
        if (NodeMass.Sum() <= 0) { Notes.Add("No mass: the seismic combination's gravity loads are nothing."); return; }

        var t0 = sw.Elapsed.TotalMilliseconds;
        Modal = Seismic.Modal.Solve(sys, NodeMass, Settings.Modes);
        var t1 = sw.Elapsed.TotalMilliseconds;
        var md = Modal;
        if (md.Modes.Count == 0) { Notes.Add("No mode found."); return; }
        if (!md.Converged) Notes.Add($"The subspace iteration did not converge in {md.Iterations} steps: the highest modes are approximate.");
        Notes.Add($"{md.Modes.Count} mode{(md.Modes.Count == 1 ? "" : "s")} by subspace iteration on the sparse Cholesky ({md.Iterations} steps); effective masses {md.Cumulative(0):P1} along X, {md.Cumulative(1):P1} along Y of the mass on the free nodes ({md.TotalMass[0]:0.##} t).");
        if (!md.Reached) Notes.Add($"The modes' limit ({Settings.Modes}) stops before 90 % of the mass along both directions (P100-1/2013 §4.5.3.3.1, EN 1998-1 §4.3.3.3.1): raise it, or check with √n modes as the codes allow.");
        if (md.SupportMass > 0) Notes.Add($"{md.SupportMass:0.##} t of the mass is on the supports (it does not move).");

        // Each mode's response to a unit shape, and its coefficient per direction: a = Γ Sd(T) / ω².
        _modes = md.Modes.Select(x => sys.FromDisplacements($"mode{x.N}", x.Phi)).ToArray();
        var t2 = sw.Elapsed.TotalMilliseconds;
        _a = new double[md.Modes.Count, 2];
        for (var k = 0; k < md.Modes.Count; k++)
            for (var d = 0; d < 2; d++)
                _a[k, d] = md.Modes[k].Gamma[d] * Spectrum.Design(md.Modes[k].T) / (md.Modes[k].Omega * md.Modes[k].Omega);
        var pairs = new List<(int, int, double)>();
        for (var i = 0; i < md.Modes.Count; i++)
        {
            pairs.Add((i, i, 1));
            if (Settings.ModalRule != "cqc") continue;
            for (var j = i + 1; j < md.Modes.Count; j++)
            {
                var rho = Spectrum.Rho(md.Modes[i].Omega, md.Modes[j].Omega, Settings.Damping);
                if (rho > 1e-6) pairs.Add((i, j, 2 * rho));
            }
        }
        _pairs = [.. pairs];
        for (var i = 0; i + 1 < md.Modes.Count; i++) if (md.Modes[i + 1].T > 0.9 * md.Modes[i].T) CloseModes++;
        Notes.Add(Settings.ModalRule == "cqc"
            ? $"Modes combined by CQC (Der Kiureghian 1981, ξ = {Settings.Damping:0.###}); {CloseModes} pair{(CloseModes == 1 ? "" : "s")} of neighbouring modes within 10 % of each other's period."
            : $"Modes combined by SRSS; {CloseModes} pair{(CloseModes == 1 ? "" : "s")} of neighbouring modes within 10 % of each other's period{(CloseModes > 0 ? ": CQC would be right for them (EN 1998-1 4.3.3.3.2)" : "")}.");
        for (var d = 0; d < 2; d++) T1[d] = md.Modes.MaxBy(x => x.Mass[d])!.T;

        StoreyResults(p);
        var t3 = sw.Elapsed.TotalMilliseconds;
        Torsion(p, sys);
        Timings = $"prepare {t0:0}, modes {t1 - t0:0}, mode results {t2 - t1:0}, storeys {t3 - t2:0}, torsion {sw.Elapsed.TotalMilliseconds - t3:0}";
        Notes.Add(Settings.DirectionRule == "30%" ? "The two horizontal directions by the 30 % rule: E = max(Ex + 0.3 Ey, 0.3 Ex + Ey) (P100-1/2013 §4.5.3.6, EN 1998-1 4.3.3.5.1); no vertical component." : "The two horizontal directions by SRSS: E = √(Ex² + Ey²); no vertical component.");
        Notes.Add($"Seismic combination: {GravityName} ± AEd (CR 0-2012, EN 1990 6.4.3.4); the static results of the gravity loads with the uncracked stiffness, the seismic action with the cracked.");
        Notes.Add("Masonry is linear elastic in S3: no tension cut-off, no cracking; the forces are those of the design spectrum (q).");
        Notes.Add($"Indicative: {(Settings.IsP100 ? "P100-1/2013" : "EN 1998-1, the recommended values")}; ag = {Settings.Ag:0.###} g, the site's values yours.");
        Ms = sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>A rigid diaphragm (§11.2) on each level without a shell floor: the level's free nodes tied to a master node at their centre by penalty (in-plane translations and the turn about Z).</summary>
    void Diaphragms(FrameProblem p, StructuralProblem sp)
    {
        var slabNodes = p.Shells.Where(s => sp.Surfaces.TryGetValue(s.Group ?? "", out var info) && info.Kind == "slab").SelectMany(s => s.Nodes).ToHashSet();
        var alpha = 0.0;
        foreach (var e in p.Elements) { e.Prepare(p.Nodes); alpha = Math.Max(alpha, e.E * e.A / Math.Max(e.L, 1e-3)); }
        foreach (var s in p.Shells) alpha = Math.Max(alpha, s.E * s.Thickness);
        alpha *= 1e4;
        var (made, skipped) = (0, 0);
        foreach (var st in Storeys)
        {
            var level = st.Top;
            var band = Enumerable.Range(0, p.Nodes.Count).Where(n => Math.Abs(p.Nodes[n].Z - level) <= 0.5 && !(p.Held[n] is { } h && h[0] && h[1])).ToList();
            if (band.Count < 2) continue;
            if (band.Any(slabNodes.Contains)) { skipped++; continue; }
            var mass = band.Sum(n => NodeMass[n]);
            var c = mass > 0 ? band.Aggregate(Vec3.Zero, (acc, n) => acc + p.Nodes[n] * NodeMass[n]) * (1 / mass) : band.Aggregate(Vec3.Zero, (acc, n) => acc + p.Nodes[n]) * (1.0 / band.Count);
            var master = p.AddNode(c, $"diaphragm:{st.Id}", [false, false, true, true, true, false]);
            foreach (var s in band)
            {
                var (dx, dy) = (p.Nodes[s].X - c.X, p.Nodes[s].Y - c.Y);
                // u_s,x − u_m,x + θ_m (y_s − y_m) = 0; u_s,y − u_m,y − θ_m (x_s − x_m) = 0.
                var k = new double[144];
                foreach (var a in new[] { new (int, double)[] { (0, 1), (6, -1), (11, dy) }, [(1, 1), (7, -1), (11, -dx)] })
                    foreach (var (i, vi) in a)
                        foreach (var (j, vj) in a)
                            k[i * 12 + j] += alpha * vi * vj;
                p.Links.Add(new Link([s, master], k, $"diaphragm:{st.Id}:{s}"));
            }
            made++;
        }
        if (made > 0) Notes.Add($"Rigid diaphragm on {made} level{(made == 1 ? "" : "s")} without a shell floor (a penalty tying each level's nodes in its plane).");
        if (skipped > 0) Notes.Add($"{skipped} level{(skipped == 1 ? " has a shell floor: no diaphragm imposed there" : "s have a shell floor: no diaphragm imposed there")} (the slab gives it).");
    }

    // ------------------------------------------------------------ combining

    /// <summary>The modal values of one direction combined (CQC or SRSS), each mode's value its unit result times its coefficient.</summary>
    double Modal1(double[] v, int d)
    {
        var s = 0.0;
        foreach (var (i, j, w) in _pairs) s += w * v[i] * _a[i, d] * v[j] * _a[j, d];
        return Math.Sqrt(Math.Max(s, 0));
    }

    double Directions(double ex, double ey) => Settings.DirectionRule == "srss" ? Math.Sqrt(ex * ex + ey * ey) : Math.Max(ex + 0.3 * ey, 0.3 * ex + ey);

    /// <summary>AEd for a vector of quantities a result gives: each component's modal response along X and Y, the torsion's added, the directions combined.</summary>
    public double[] Combine(Func<CaseResult, double[]> pick)
    {
        var per = _modes.Select(pick).ToArray();
        var len = per.Length > 0 ? per[0].Length : 0;
        var tors = _torsion.Select(t => t != null ? pick(t) : null).ToArray();
        var o = new double[len];
        var v = new double[per.Length];
        for (var c = 0; c < len; c++)
        {
            for (var k = 0; k < per.Length; k++) v[k] = per[k][c];
            var e = new double[2];
            for (var d = 0; d < 2; d++) e[d] = Modal1(v, d) + (tors[d] is { } t ? Math.Abs(t[c]) : 0);
            o[c] = Directions(e[0], e[1]);
        }
        return o;
    }

    /// <summary>One direction's response of a quantity, the modes combined (no torsion, no other direction).</summary>
    double Along(Func<int, double> modeValue, int d)
    {
        var v = new double[_modes.Length];
        for (var k = 0; k < v.Length; k++) v[k] = modeValue(k);
        return Modal1(v, d);
    }

    // ------------------------------------------------------------ storeys

    void StoreyResults(FrameProblem p)
    {
        var md = Modal!;
        var free = Enumerable.Range(0, p.Nodes.Count).Where(n => NodeMass[n] > 0 && System!.Eq[n * 6] >= 0).ToList();
        // The inertia force of a node in mode k along d per unit coefficient: ω² m φ.
        double F(int k, int n, int d) => md.Modes[k].Omega * md.Modes[k].Omega * NodeMass[n] * md.Modes[k].Phi[n * 6 + d];
        for (var d = 0; d < 2; d++) Base[d] = Along(k => free.Sum(n => F(k, n, d)), d);
        var total = md.TotalMass;
        for (var d = 0; d < 2; d++)
        {
            var lambda = T1[d] <= 2 * Spectrum.TC && Storeys.Count > 2 ? 0.85 : 1;
            Equivalent[d] = Spectrum.Design(T1[d]) * total[d] * lambda;
        }
        foreach (var s in Storeys)
        {
            var above = free.Where(n => p.Nodes[n].Z > s.Z + 0.01).ToList();
            var own = free.Where(n => p.Nodes[n].Z > s.Z + 0.01 && p.Nodes[n].Z <= s.Top + 0.01).ToList();
            s.Mass = own.Sum(n => NodeMass[n]);
            if (own.Count > 0)
            {
                s.Width[0] = own.Max(n => p.Nodes[n].Y) - own.Min(n => p.Nodes[n].Y);
                s.Width[1] = own.Max(n => p.Nodes[n].X) - own.Min(n => p.Nodes[n].X);
            }
            var bottom = Level(p, s.Z);
            var top = Level(p, s.Top);
            for (var d = 0; d < 2; d++)
            {
                s.Shear[d] = Along(k => above.Sum(n => F(k, n, d)), d);
                s.Force[d] = Along(k => own.Sum(n => F(k, n, d)), d);
                double Mean(List<int> ns, int k) => ns.Count == 0 ? 0 : ns.Average(n => md.Modes[k].Phi[n * 6 + d]);
                s.Drift[d] = Along(k => Mean(top, k) - Mean(bottom, k), d);
                s.DriftMax[d] = top.Count == 0 ? 0 : top.Max(n => Along(k => md.Modes[k].Phi[n * 6 + d] - Mean(bottom, k), d));
            }
        }
    }

    /// <summary>The nodes at a level: those nearest it within 0.5 m (a floor's mid-plane under its level), and their like.</summary>
    static List<int> Level(FrameProblem p, double z)
    {
        var near = Enumerable.Range(0, p.Nodes.Count).Where(n => Math.Abs(p.Nodes[n].Z - z) <= 0.5).ToList();
        if (near.Count == 0) return near;
        var d0 = near.Min(n => Math.Abs(p.Nodes[n].Z - z));
        return near.Where(n => Math.Abs(p.Nodes[n].Z - z) <= d0 + 0.05).ToList();
    }

    /// <summary>The accidental torsion (§11.3): each storey's force times e = ±0.05 L as a torque on its nodes, as a rigid turn's inertia would share it; a static case per direction.</summary>
    void Torsion(FrameProblem p, FrameSystem sys)
    {
        if (Settings.Torsion <= 0 || Storeys.Count == 0) return;
        for (var d = 0; d < 2; d++)
        {
            var c = new CaseLoads($"torsion-{(d == 0 ? "x" : "y")}");
            foreach (var s in Storeys)
            {
                var own = Enumerable.Range(0, p.Nodes.Count).Where(n => NodeMass[n] > 0 && sys.Eq[n * 6] >= 0 && p.Nodes[n].Z > s.Z + 0.01 && p.Nodes[n].Z <= s.Top + 0.01).ToList();
                var m = own.Sum(n => NodeMass[n]);
                if (m <= 0) continue;
                var cx = own.Sum(n => NodeMass[n] * p.Nodes[n].X) / m;
                var cy = own.Sum(n => NodeMass[n] * p.Nodes[n].Y) / m;
                var j = own.Sum(n => NodeMass[n] * ((p.Nodes[n].X - cx) * (p.Nodes[n].X - cx) + (p.Nodes[n].Y - cy) * (p.Nodes[n].Y - cy)));
                if (j <= 1e-12) continue;
                var mt = s.Force[d] * Settings.Torsion * s.Width[d];
                foreach (var n in own)
                    c.AddNodal(n, new Vec3(-(p.Nodes[n].Y - cy), p.Nodes[n].X - cx, 0) * (mt * NodeMass[n] / j));
            }
            if (c.Nodal.Count > 0) _torsion[d] = sys.SolveCase(c);
        }
        if (_torsion.Any(t => t != null))
            Notes.Add($"Accidental torsion: each storey's seismic force times ±{Settings.Torsion:0.###} of its width across the direction (P100-1/2013 §4.5.2.2, EN 1998-1 §4.3.2), as a static torque on its masses; its effects added in absolute value.");
    }

    // ------------------------------------------------------------ members through their pieces (the modal problem's elements)

    public Section MemberAt(int member, CaseResult r, double x)
    {
        var ps = Static.Problem.PieceMap[member];
        var pc = ps.Count == 1 ? ps[0] : ps.FirstOrDefault(q => x <= q.X1 + 1e-12) ?? ps[^1];
        return Stations.At(Problem!.Elements[pc.Element], r.Elements[pc.Element], x - pc.X0) with { X = x };
    }

    public (double U, double V, double W) MemberDeflection(int member, CaseResult r, double x)
    {
        var ps = Static.Problem.PieceMap[member];
        var pc = ps.Count == 1 ? ps[0] : ps.FirstOrDefault(q => x <= q.X1 + 1e-12) ?? ps[^1];
        return Stations.Deflection(Problem!.Elements[pc.Element], r.Elements[pc.Element], x - pc.X0);
    }

    /// <summary>AEd of a member's forces at x (each of N, Vy, Vz, T, My, Mz).</summary>
    public double[] SectionAt(int member, double x) => Combine(r => { var s = MemberAt(member, r, x); return [s.N, s.Vy, s.Vz, s.T, s.My, s.Mz]; });

    /// <summary>AEd of a node's displacements (global ux … rz).</summary>
    public double[] NodeAt(int n) => Combine(r => r.U.AsSpan(n * 6, 6).ToArray());

    /// <summary>AEd of a node's reactions.</summary>
    public double[] ReactionAt(int n) => Combine(r => r.Reactions.TryGetValue(n, out var x) ? x : new double[6]);

    /// <summary>AEd of a member's displacement at x (local u, v, w).</summary>
    public double[] DeflectionAt(int member, double x) => Combine(r => { var (u, v, w) = MemberDeflection(member, r, x); return [u, v, w]; });

    /// <summary>
    /// AEd of the shells' values at their nodes (keys: surface and node; per key the eight resultants and w along the surface's
    /// normal), from each mode's results recovered as the static ones are.
    /// </summary>
    public (ShellForces[] F, double[] W) ShellNodal((string Group, int Node)[] keys, Func<string, Vec3> normal)
    {
        var p = Problem!;
        double[] Pick(CaseResult r)
        {
            var rec = ShellSmoothing.Recovered(p, r);
            var o = new double[keys.Length * 9];
            for (var i = 0; i < keys.Length; i++)
            {
                var f = rec.TryGetValue(keys[i], out var x) ? x : default;
                for (var c = 0; c < 8; c++) o[i * 9 + c] = f[c];
                var nv = normal(keys[i].Group);
                var b = keys[i].Node * 6;
                o[i * 9 + 8] = r.U[b] * nv.X + r.U[b + 1] * nv.Y + r.U[b + 2] * nv.Z;
            }
            return o;
        }
        var e = Combine(Pick);
        var fs = new ShellForces[keys.Length];
        var w = new double[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            fs[i] = new ShellForces(e[i * 9], e[i * 9 + 1], e[i * 9 + 2], e[i * 9 + 3], e[i * 9 + 4], e[i * 9 + 5], e[i * 9 + 6], e[i * 9 + 7]);
            w[i] = e[i * 9 + 8];
        }
        return (fs, w);
    }

    /// <summary>The modes' results, for the shells' recovered values.</summary>
    public IReadOnlyList<CaseResult> ModeResults => _modes;
    public IReadOnlyList<CaseResult?> TorsionResults => _torsion;

    // ------------------------------------------------------------ the queries

    static double R(double v, int digits = 6) => Math.Round(v, digits);
    static JsonArray A(IEnumerable<double> xs, int digits = 6) => new(xs.Select(x => (JsonNode)R(x, digits)).ToArray());

    public JsonObject ModalJson()
    {
        var o = new JsonObject { ["ok"] = Ok, ["settings"] = Settings.ToJson(), ["ms"] = R(Ms, 1) };
        if (Modal is { } md)
        {
            var cum = new double[3];
            o["modes"] = new JsonArray(md.Modes.Select(x =>
            {
                for (var d = 0; d < 3; d++) cum[d] += x.Mass[d];
                return (JsonNode)new JsonObject
                {
                    ["n"] = x.N, ["omega"] = R(x.Omega), ["f"] = R(x.F), ["T"] = R(x.T), ["gamma"] = A(x.Gamma), ["mass"] = A(x.Mass, 4),
                    ["ratio"] = A(Enumerable.Range(0, 3).Select(d => md.TotalMass[d] > 0 ? x.Mass[d] / md.TotalMass[d] : 0), 5),
                    ["cumulative"] = A(Enumerable.Range(0, 3).Select(d => md.TotalMass[d] > 0 ? cum[d] / md.TotalMass[d] : 0), 5),
                    ["rayleigh"] = R(x.Rayleigh / (x.Omega * x.Omega), 9), ["residual"] = x.Residual,
                };
            }).ToArray());
            o["totalMass"] = A(md.TotalMass, 4);
            o["supportMass"] = R(md.SupportMass, 4);
            o["reached"] = md.Reached;
            o["dofs"] = System?.Neq ?? 0;
        }
        o["notes"] = new JsonArray(Notes.Select(n => (JsonNode)n).ToArray());
        return o;
    }

    public JsonObject SpectrumJson()
    {
        var sp = Spectrum;
        var tmax = Math.Max(4, Math.Max(sp.TD + 1, (Modal?.Modes.FirstOrDefault()?.T ?? 0) * 1.2));
        var pts = new JsonArray();
        var ts = Enumerable.Range(0, 161).Select(i => tmax * i / 160.0).Concat([sp.TB, sp.TC, sp.TD]).Distinct().Order();
        foreach (var t in ts) pts.Add((JsonNode)new JsonObject { ["T"] = R(t, 4), ["Sd"] = R(sp.Design(t), 5), ["Se"] = R(sp.Factor * sp.Se(t), 5) });
        return new JsonObject
        {
            ["code"] = sp.Code, ["ag"] = R(sp.Ag, 5), ["S"] = sp.S, ["TB"] = sp.TB, ["TC"] = sp.TC, ["TD"] = sp.TD, ["q"] = sp.Q, ["gamma"] = Settings.Gamma, ["given"] = Settings.Given,
            ["unit"] = "m/s²", ["points"] = pts,
            ["modes"] = new JsonArray((Modal?.Modes ?? []).Select(x => (JsonNode)new JsonObject { ["n"] = x.N, ["T"] = R(x.T, 5), ["Sd"] = R(sp.Design(x.T), 5) }).ToArray()),
        };
    }

    public JsonObject SeismicJson()
    {
        var o = new JsonObject { ["ok"] = Ok, ["settings"] = Settings.ToJson(), ["combination"] = Ok ? $"{GravityName} ± AEd" : null };
        if (Ok)
        {
            var q = Settings.Q;
            o["base"] = A(Base, 3);
            o["equivalent"] = A(Equivalent, 3);
            o["T1"] = A(T1, 5);
            var arr = new JsonArray();
            foreach (var s in Storeys)
            {
                var sls = new double[2];
                var uls = new double[2];
                for (var d = 0; d < 2; d++)
                {
                    sls[d] = Settings.DriftNu * q * s.Drift[d];
                    var c = Math.Clamp(3 - 2.3 * T1[d] / Spectrum.TC, 1, 2);
                    uls[d] = (Settings.IsP100 ? c : 1) * q * s.Drift[d];
                }
                arr.Add((JsonNode)new JsonObject
                {
                    ["id"] = s.Id, ["name"] = s.Name, ["z"] = R(s.Z, 3), ["h"] = R(s.H, 3), ["mass"] = R(s.Mass, 3),
                    ["shear"] = A(s.Shear, 3), ["force"] = A(s.Force, 3), ["drift"] = A(s.Drift, 7), ["driftMax"] = A(s.DriftMax, 7),
                    ["sls"] = A(sls, 7), ["uls"] = A(uls, 7),
                    ["slsLimit"] = R(Settings.DriftSls * s.H, 6), ["ulsLimit"] = Settings.IsP100 ? R(Settings.DriftUls * s.H, 6) : null,
                    ["slsOk"] = sls.All(x => x <= Settings.DriftSls * s.H + 1e-12),
                    ["ulsOk"] = Settings.IsP100 ? uls.All(x => x <= Settings.DriftUls * s.H + 1e-12) : null,
                });
            }
            o["storeys"] = arr;
            o["closeModes"] = CloseModes;
        }
        o["notes"] = new JsonArray(Notes.Select(n => (JsonNode)n).ToArray());
        return o;
    }

    /// <summary>{mode, scale?}: a mode's shape on the bars (polylines) and the shells (their nodes displaced), world coordinates.</summary>
    public JsonObject ModeShape(int n, double? scaleArg)
    {
        if (!Ok) throw new CommandException(Settings.Given ? "The modal analysis did not run: analysis.modal says why." : "Give the seismic parameters first (analysis.setSeismic).");
        if (n < 1 || n > Modal!.Modes.Count) throw new CommandException($"There is no mode {n}: 1 to {Modal!.Modes.Count}.");
        var mode = Modal.Modes[n - 1];
        var r = _modes[n - 1];
        var sp = Static.Problem;
        var p = Problem!;
        var pts = sp.Members.SelectMany(e => new[] { e.Pi, e.Pj }).Concat(p.Shells.SelectMany(s => s.Nodes.Select(x => p.Nodes[x]))).ToList();
        var extent = pts.Count == 0 ? 1 : Math.Max((new Vec3(pts.Max(x => x.X), pts.Max(x => x.Y), pts.Max(x => x.Z)) - new Vec3(pts.Min(x => x.X), pts.Min(x => x.Y), pts.Min(x => x.Z))).Length, 1);
        var max = 0.0;
        for (var i = 0; i < p.Nodes.Count; i++) max = Math.Max(max, Math.Sqrt(r.U[i * 6] * r.U[i * 6] + r.U[i * 6 + 1] * r.U[i * 6 + 1] + r.U[i * 6 + 2] * r.U[i * 6 + 2]));
        var scale = scaleArg ?? (max > 1e-15 ? 0.15 * extent / max : 1);
        var lines = new JsonArray();
        for (var k = 0; k < sp.Members.Count; k++)
        {
            var e = sp.Members[k];
            var list = new JsonArray();
            for (var i = 0; i <= 10; i++)
            {
                var x = e.L * i / 10;
                var (u, v, w) = MemberDeflection(k, r, x);
                list.Add((JsonNode)A([.. Pt(e.Pi + e.Ex * x + (e.Ex * u + e.Ey * v + e.Ez * w) * scale)], 4));
            }
            lines.Add((JsonNode)new JsonObject { ["frame"] = e.Id, ["sourceId"] = e.SourceId, ["base"] = new JsonArray(A(Pt(e.Pi), 4), A(Pt(e.Pj), 4)), ["points"] = list });
        }
        var shells = new JsonArray();
        foreach (var g in p.Shells.GroupBy(s => s.Group ?? ""))
        {
            var nodes = g.SelectMany(s => s.Nodes).Distinct().ToList();
            var local = nodes.Select((x, i) => (x, i)).ToDictionary(t => t.x, t => t.i);
            shells.Add((JsonNode)new JsonObject
            {
                ["id"] = g.Key, ["sourceId"] = g.First().SourceId,
                ["positions"] = A(nodes.SelectMany(x => Pt(p.Nodes[x])), 4),
                ["displaced"] = A(nodes.SelectMany(x => Pt(p.Nodes[x] + new Vec3(r.U[x * 6], r.U[x * 6 + 1], r.U[x * 6 + 2]) * scale)), 4),
                ["faces"] = new JsonArray(g.SelectMany(s => s.Count == 4 ? s.Nodes.Select(x => local[x]) : s.Nodes.Select(x => local[x]).Append(local[s.Nodes[2]])).Select(x => (JsonNode)x).ToArray()),
            });
        }
        return new JsonObject
        {
            ["mode"] = n, ["T"] = R(mode.T, 5), ["f"] = R(mode.F, 5), ["ratio"] = A(Enumerable.Range(0, 3).Select(d => Modal.TotalMass[d] > 0 ? mode.Mass[d] / Modal.TotalMass[d] : 0), 5),
            ["scale"] = scale, ["max"] = max, ["polylines"] = lines, ["shells"] = shells,
        };
        static double[] Pt(Vec3 v) => [v.X, v.Y, v.Z];
    }
}
