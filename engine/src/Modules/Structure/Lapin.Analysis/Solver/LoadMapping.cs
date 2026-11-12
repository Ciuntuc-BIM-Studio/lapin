using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;

namespace Lapin.Analysis.Solver;

/// <summary>A refused bar (no E, no section) and why.</summary>
public sealed record RefusedBar(AFrame Frame, string Reason);

/// <summary>What one case's loads became on the solver's model: kN mapped onto bars and nodes, kN left with nothing to bear on, and the counts said.</summary>
public sealed class MappedCase(CaseLoads loads)
{
    public CaseLoads Loads { get; } = loads;
    public double Unmapped { get; set; }
    public int UnmappedLoads { get; set; }
    public HashSet<string> UnmappedSources { get; } = [];
    public double WallsOwn { get; set; }
    /// <summary>Line loads with no bar under them, spread over their slab piece.</summary>
    public int Spread { get; set; }
    /// <summary>Point loads on a slab piece, shared by its corners.</summary>
    public int OnCorners { get; set; }
    /// <summary>Area loads on a framed roof's members by their tributary widths.</summary>
    public int OnRoofs { get; set; }
    /// <summary>A snow case: its area loads are per horizontal projection.</summary>
    public bool Snow { get; init; }
}

/// <summary>
/// The solver's model from the analytical one (docs/plan-fem.md §9.1, §9.2): its bars with their stiffness (E and ν from
/// <see cref="StructuralMaterials"/>, the section's A, I22, I33, J from <see cref="SectionProperties"/>, the local axes as
/// docs/jg-structures.md, an unsymmetric section about its principal axes, eccentric ends as rigid arms, the releases of
/// <see cref="FrameReleases"/>); the load-bearing walls as line supports (every node of theirs held in translation, free to
/// turn); the slabs carrying loads only. Each case's loads go where §9.2 says: the bars' own weight along them; a line or
/// point load on the bar under it (in plan within 0.3 m of its axis, at most 1 m under it; a point on a bar's node first),
/// else on the slab under it as its piece's equivalent area load; an area load on each slab piece under it (at most 1 m
/// below), by the 45° rule (<see cref="Tributary"/>), onto the bars along its sides or straight into the walls under them.
/// </summary>
public sealed class StructuralProblem
{
    public const double PlanReach = 0.3, DepthReach = 1.0, AboveReach = 0.3;

    public required FrameProblem Problem { get; init; }
    public required AnalyticalModel Model { get; init; }
    /// <summary>The bar of each solver element.</summary>
    public required List<AFrame> Frames { get; init; }
    public required Dictionary<ANode, int> NodeIndex { get; init; }
    public required List<RefusedBar> Refused { get; init; }
    public required List<MappedCase> Cases { get; init; }
    public required List<string> Notes { get; init; }
    public required Dictionary<AFrame, EndReleases> Releases { get; init; }

    sealed class Piece
    {
        public required AShell Shell;
        public required List<int> Ring;
        public required List<Vec2> Plan;
        /// <summary>The node at each corner of <see cref="Plan"/>.</summary>
        public required List<int> Corners;
        public Vec3 Normal, Origin;
        public double Area;
        public required List<Side> Sides;
        public double ZAt(Vec2 p) => Math.Abs(Normal.Z) < 1e-9 ? Origin.Z : Origin.Z - (Normal.X * (p.X - Origin.X) + Normal.Y * (p.Y - Origin.Y)) / Normal.Z;
    }

    sealed class Side
    {
        public Vec2 A, Dir;
        public double Length;
        /// <summary>The ring's nodes along it, with their station from A.</summary>
        public required List<(int Node, double S)> Nodes;
        /// <summary>Each stretch between two nodes: the bar along it (an element), or −1.</summary>
        public required List<int> Bars;
        public bool Supported;
    }

    public static StructuralProblem Build(IModelView m, LoadsReport rep)
    {
        var am = rep.Model;
        var notes = new List<string>();
        var p = new FrameProblem();
        var nodeIndex = new Dictionary<ANode, int>();
        var wallNodes = am.Shells.Where(s => s.Source.Kind == "wall").SelectMany(s => s.Nodes).ToHashSet();
        foreach (var n in am.Nodes)
        {
            bool[]? held = n.Restraints is { Length: 6 } r ? [.. r] : null;
            if (wallNodes.Contains(n)) { held ??= new bool[6]; held[0] = held[1] = held[2] = true; }
            nodeIndex[n] = p.AddNode(n.At, n.Id, held);
        }
        var releases = FrameReleases.Of(am, m);
        var densities = new LoadsReport.Densities(m);
        var refused = new List<RefusedBar>();
        var frames = new List<AFrame>();
        var materials = new Dictionary<string, StructuralMaterial?>();
        var assumedNu = new HashSet<string>();
        var timberG = new HashSet<string>();
        StructuralMaterial? MaterialOf(string? id)
        {
            if (id == null) return null;
            if (materials.TryGetValue(id, out var had)) return had;
            var e = (m.Get(id) is { Class: IfcClass.Material } own ? own : Materials.Find(m, id))
                ?? new Entity($"lapin-material-{id}", IfcClass.Material, id, []);
            return materials[id] = StructuralMaterials.Of(m, e);
        }
        var selfWeight = new List<(int Element, double Q)>();
        var offCentre = 0;
        var noShear = new HashSet<string>();
        foreach (var f in am.Frames)
        {
            var props = SectionProperties.Of(f.Source.Section.Shape);
            if (props is not { Area: > 0 }) { refused.Add(new(f, $"its section „{f.Source.Section.Name}” gives no properties")); continue; }
            var mat = MaterialOf(f.Source.MaterialId);
            if (mat?.E is not { } e) { refused.Add(new(f, f.Source.MaterialId == null ? "its type names no material" : $"its material „{mat?.Name ?? f.Source.MaterialId}” has no E")); continue; }
            double g;
            if (mat.Poisson is { } nu) g = e.Value / (2 * (1 + nu.Value));
            else if (mat.Kind == "timber") { g = e.Value / 16; timberG.Add(mat.Name); }
            else { g = e.Value / (2 * 1.3); assumedNu.Add(mat.Name); }
            var (pi, pj) = (f.I.At + f.OffI, f.J.At + f.OffJ);
            var u = (pj - pi).Normalized();
            var rotation = f.RotationDeg;
            if (props is { Geometric: not null } && Math.Abs(props.PrincipalDeg) > 1e-9)
                rotation = AnalyticalModel.Normalize(rotation + (f.Source.Mirrored ? props.PrincipalDeg : -props.PrincipalDeg));
            var v0 = AnalyticalModel.Reference(u);
            var th = rotation * Math.PI / 180;
            var local2 = (v0 * Math.Cos(th) + Vec3.Cross(u, v0) * Math.Sin(th)).Normalized();
            // The axis through the centroid: an unsymmetric section's (a T, a channel, an angle) is off its box centre, where the
            // analytical model lays the line; the bar keeps it by rigid arms (AnalyticalModel.CentroidOffset).
            var centroid = AnalyticalModel.CentroidOffset(f, props);
            if (centroid.Length > 1e-9) offCentre++;
            if (props.Av2 is not { } av2 || props.Av3 is not { } av3) { (av2, av3) = (0, 0); noShear.Add(f.Source.Section.Name); }
            var rel = releases.GetValueOrDefault(f);
            var el = new FrameElement
            {
                Id = f.Id, Name = f.Source.Name, SourceId = f.Source.SourceId,
                I = nodeIndex[f.I], J = nodeIndex[f.J], OffI = f.OffI + centroid, OffJ = f.OffJ + centroid, Local2 = local2,
                E = e.Value / 1000, G = g / 1000, A = props.Area, I22 = props.I22, I33 = props.I33, Jt = props.J, As2 = av2, As3 = av3,
                ReleaseI = rel?.I ?? new bool[6], ReleaseJ = rel?.J ?? new bool[6],
            };
            p.Elements.Add(el);
            frames.Add(f);
            if (densities.Of(f.Source.MaterialId) is { } rho) selfWeight.Add((p.Elements.Count - 1, rho * Units.G * props.Area / 1000));
        }
        if (refused.Count > 0)
            notes.Add($"{refused.Count} bar{(refused.Count == 1 ? " is" : "s are")} refused, the analysis does not start: " +
                string.Join("; ", refused.Take(8).Select(r => $"{r.Frame.Source.Name}: {r.Reason}")) + (refused.Count > 8 ? "…" : "") + ".");
        if (timberG.Count > 0) notes.Add($"Timber ({string.Join(", ", timberG)}): G = E0,mean / 16, as EN 338's tables give it (no Poisson ratio for an orthotropic material).");
        if (offCentre > 0) notes.Add($"{offCentre} bar{(offCentre == 1 ? "" : "s")} of an unsymmetric section (a T, a channel, an angle): {(offCentre == 1 ? "its" : "their")} axis through the centroid, joined to the box centre's nodes by rigid arms.");
        if (noShear.Count > 0) notes.Add($"No shear area for {string.Join(", ", noShear.Take(6))}{(noShear.Count > 6 ? "…" : "")}: Euler–Bernoulli there (shear deformation left out).");
        if (assumedNu.Count > 0) notes.Add($"No Poisson ratio for {string.Join(", ", assumedNu)}: ν = 0.3 assumed for G.");

        var sp = new StructuralProblem
        {
            Problem = p, Model = am, Frames = frames, NodeIndex = nodeIndex, Refused = refused, Cases = [], Notes = notes, Releases = releases,
        };
        if (refused.Count > 0) return sp;
        sp.MapLoads(m, rep, densities, selfWeight);
        return sp;
    }

    // ------------------------------------------------------------ the loads

    List<Piece> _pieces = [];
    bool[] _carrying = [];
    Dictionary<(int, int), int> _barOn = [];

    void MapLoads(IModelView m, LoadsReport rep, LoadsReport.Densities densities, List<(int Element, double Q)> selfWeight)
    {
        var p = Problem;
        _carrying = new bool[p.Nodes.Count];
        for (var k = 0; k < p.Elements.Count; k++) { _carrying[p.Elements[k].I] = true; _carrying[p.Elements[k].J] = true; }
        for (var n = 0; n < p.Nodes.Count; n++) if (p.Held[n] != null) _carrying[n] = true;
        for (var k = 0; k < p.Elements.Count; k++)
        {
            var key = Key(p.Elements[k].I, p.Elements[k].J);
            if (!_barOn.ContainsKey(key) && !Frames[k].Source.Kind.Equals("column")) _barOn[key] = k;
        }
        _pieces = Model.Shells.Where(s => s.Source.Kind == "slab").Select(MakePiece).OfType<Piece>().ToList();
        _roofs = FramedRoofs();

        var actions = AnalysisModule.Actions(m, rep);
        var selfCase = rep.SelfWeightCase;
        foreach (var c in rep.Cases)
        {
            var mc = new MappedCase(new CaseLoads(c.Id)) { Snow = c.ActionSource == "SNOW_S" || c.Category == Categories.Snow };
            Cases.Add(mc);
            if (c.Id == selfCase?.Id)
            {
                var down = new Vec3(0, 0, -1);
                foreach (var (k, q) in selfWeight)
                {
                    var e = Frames[k];
                    var length = (e.J.At + e.OffJ - (e.I.At + e.OffI)).Length;
                    mc.Loads.Span.Add((k, SpanLoad.Uniform(0, length, down * q)));
                }
                // The slabs' cores as area loads on their own pieces; the walls' own weight goes straight to what holds them.
                foreach (var piece in _pieces)
                    if (densities.Of(piece.Shell.Source.MaterialId) is { } rho)
                    {
                        var q = piece.Shell.Source.Thickness * rho * Units.G / 1000;
                        // The piece's area measured on its plane, its load per plan area.
                        var slope = Math.Abs(piece.Normal.Z) > 1e-9 ? 1 / Math.Abs(piece.Normal.Z) : 1;
                        AreaOnPiece(mc, piece, [piece.Plan], down * (q * slope), piece.Shell.Source.SourceId);
                    }
                foreach (var w in Model.Shells.Where(s => s.Source.Kind == "wall"))
                    if (densities.Of(w.Source.MaterialId) is { } rho)
                        mc.WallsOwn += VecMath.NewellNormal(w.Nodes.Select(n => n.At).ToList()).Length * w.Source.Thickness * rho * Units.G / 1000;
            }
            foreach (var a in actions.Where(a => a.CaseId == c.Id))
            {
                var source = a.Derived ? a.SourceId ?? a.Id : a.Id;
                switch (a.Kind)
                {
                    case "planar" when a.Points.Count >= 3: Planar(mc, a, source); break;
                    case "curve" when a.Points.Count >= 2: Curve(mc, a, source); break;
                    case "point": PointLoad(mc, a, source); break;
                }
            }
        }
    }

    static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

    Piece? MakePiece(AShell s)
    {
        var ring = s.Nodes.Select(n => NodeIndex[n]).ToList();
        var pts3 = s.Nodes.Select(n => n.At).ToList();
        var normal = VecMath.NewellNormal(pts3);
        if (normal.Length < 1e-12 || Math.Abs(normal.Normalized().Z) < 1e-6) return null;
        normal = normal.Normalized();
        var plan = pts3.Select(q => q.Xy).ToList();
        if (Polygon2.SignedArea(plan) < 0) { plan.Reverse(); ring.Reverse(); pts3.Reverse(); }
        var area = Polygon2.SignedArea(plan);
        if (area < 1e-9) return null;
        // The corners: where the ring turns.
        var n = plan.Count;
        var corners = new List<int>();
        for (var i = 0; i < n; i++)
        {
            var (a, b, c) = (plan[(i + n - 1) % n], plan[i], plan[(i + 1) % n]);
            var (d1, d2) = ((b - a).Normalized(), (c - b).Normalized());
            if (Math.Abs(Vec2.Cross(d1, d2)) > 1e-4 || Vec2.Dot(d1, d2) < 0) corners.Add(i);
        }
        if (corners.Count < 3) return null;
        var sides = new List<Side>();
        for (var k = 0; k < corners.Count; k++)
        {
            var (i0, i1) = (corners[k], corners[(k + 1) % corners.Count]);
            var (a, b) = (plan[i0], plan[i1]);
            var len = (b - a).Length;
            var dir = (b - a).Normalized();
            var nodes = new List<(int, double)>();
            for (var i = i0; ; i = (i + 1) % n)
            {
                nodes.Add((ring[i], Math.Clamp(Vec2.Dot(plan[i] - a, dir), 0, len)));
                if (i == i1) break;
            }
            var bars = new List<int>();
            var supported = false;
            for (var j = 0; j + 1 < nodes.Count; j++)
            {
                var bar = _barOn.TryGetValue(Key(nodes[j].Item1, nodes[j + 1].Item1), out var e) ? e : -1;
                bars.Add(bar);
                if (bar >= 0 || (Problem.Held[nodes[j].Item1] != null && Problem.Held[nodes[j + 1].Item1] != null)) supported = true;
            }
            sides.Add(new Side { A = a, Dir = dir, Length = len, Nodes = nodes, Bars = bars, Supported = supported });
        }
        return new Piece
        {
            Shell = s, Ring = ring, Plan = corners.Select(i => plan[i]).ToList(), Corners = corners.Select(i => ring[i]).ToList(), Normal = normal, Origin = pts3[0], Area = Polygon2.SignedArea(corners.Select(i => plan[i]).ToList()), Sides = sides,
        };
    }

    /// <summary>A force on a node: on the solver's model when a bar or a support carries the node.</summary>
    bool OnNode(MappedCase mc, int node, Vec3 f)
    {
        if (!_carrying[node]) return false;
        mc.Loads.AddNodal(node, f);
        return true;
    }

    void Unmapped(MappedCase mc, double kn, string source)
    {
        if (kn <= 1e-9) return;
        mc.Unmapped += kn;
        mc.UnmappedLoads++;
        mc.UnmappedSources.Add(source);
    }

    /// <summary>
    /// An area load over a piece (rings in plan, intensity a global force vector per plan m²): the 45° rule onto the piece's
    /// supported sides, or onto its corners when no side is supported. Returns the force it placed (kN).
    /// </summary>
    double AreaOnPiece(MappedCase mc, Piece piece, IReadOnlyList<IReadOnlyList<Vec2>> rings, Vec3 q, string source)
    {
        var placed = 0.0;
        var qn = q.Length;
        if (qn < 1e-15) return 0;
        var dir = q * (1 / qn);
        var supported = piece.Sides.Select(s => s.Supported).ToList();
        if (supported.Any(x => x))
        {
            var zones = Tributary.Zones(piece.Plan, supported);
            for (var k = 0; k < piece.Sides.Count; k++)
            {
                if (zones[k].Count < 3) continue;
                var clipped = rings.Select(r => (IReadOnlyList<Vec2>)Tributary.ClipTo(r, zones[k])).Where(r => r.Count >= 3).ToList();
                if (clipped.Count == 0) continue;
                var side = piece.Sides[k];
                var profile = Tributary.Profile(clipped, side.A, side.Dir);
                placed += PlaceOnSide(mc, side, profile, dir * qn, source);
            }
            return placed;
        }
        // No side is held: each carried corner takes the load in its cell.
        var corners = piece.Ring.Where(n => _carrying[n]).Distinct().ToList();
        if (corners.Count == 0) return 0;
        var cells = Tributary.Cells(piece.Plan, corners.Select(n => Problem.Nodes[n].Xy).ToList());
        for (var i = 0; i < corners.Count; i++)
        {
            if (cells[i].Count < 3) continue;
            var a = Tributary.Area(rings.Select(r => (IReadOnlyList<Vec2>)Tributary.ClipTo(r, cells[i])));
            if (a <= 1e-12) continue;
            OnNode(mc, corners[i], dir * (qn * a));
            placed += qn * a;
        }
        return placed;
    }

    /// <summary>A side's line load (per unit intensity, trapezoids along it) times the intensity: on the bars along it, else on its carried nodes by the lever rule; what falls past its ends at them.</summary>
    double PlaceOnSide(MappedCase mc, Side side, List<Trapezoid> profile, Vec3 q, string source)
    {
        var placed = 0.0;
        foreach (var t0 in profile)
        {
            // What lies past the side's ends goes to the end.
            if (t0.Clip(double.NegativeInfinity, 0) is { } before) placed += Lump(mc, side, 0, before.Integral, q);
            if (t0.Clip(side.Length, double.PositiveInfinity) is { } after) placed += Lump(mc, side, side.Length, after.Integral, q);
            if (t0.Clip(0, side.Length) is not { } t) continue;
            for (var j = 0; j + 1 < side.Nodes.Count; j++)
            {
                var (sa, sb) = (side.Nodes[j].S, side.Nodes[j + 1].S);
                if (sb - sa < 1e-9 || t.Clip(sa, sb) is not { } part) continue;
                var bar = side.Bars[j];
                if (bar >= 0)
                {
                    var e = Problem.Elements[bar];
                    var f = Frames[bar];
                    var len = (f.J.At + f.OffJ - (f.I.At + f.OffI)).Length;
                    var fromI = e.I == side.Nodes[j].Node;
                    double X(double s) => (fromI ? (s - sa) : (sb - s)) / (sb - sa) * len;
                    var k = (sb - sa) / len;
                    var (x0, x1) = (X(part.S0), X(part.S1));
                    var (w0, w1) = (part.W0 * k, part.W1 * k);
                    if (x0 > x1) { (x0, x1) = (x1, x0); (w0, w1) = (w1, w0); }
                    mc.Loads.Span.Add((bar, new SpanLoad(Math.Max(0, x0), Math.Min(len, x1), q * w0, q * w1)));
                    placed += part.Integral * q.Length;
                }
                else placed += Lump(mc, side, part.Centroid, part.Integral, q);
            }
        }
        return placed;
    }

    /// <summary>A force (per unit intensity × q) at a station of a side, on the nearest carried nodes either side of it by the lever rule.</summary>
    double Lump(MappedCase mc, Side side, double s, double amount, Vec3 q)
    {
        if (Math.Abs(amount) < 1e-15) return 0;
        var carried = side.Nodes.Where(x => _carrying[x.Node]).ToList();
        if (carried.Count == 0) return 0;
        var left = carried.Where(x => x.S <= s + 1e-9).Select(x => ((int, double)?)x).LastOrDefault();
        var right = carried.Where(x => x.S >= s - 1e-9).Select(x => ((int, double)?)x).FirstOrDefault();
        var f = q * amount;
        if (left is { } l && right is { } r && r.Item2 - l.Item2 > 1e-9)
        {
            var t = (s - l.Item2) / (r.Item2 - l.Item2);
            OnNode(mc, l.Item1, f * (1 - t));
            OnNode(mc, r.Item1, f * t);
        }
        else OnNode(mc, (left ?? right)!.Value.Item1, f);
        return f.Length;
    }

    /// <summary>The pieces under a plan point or region at a height: within 1 m below it (0.3 m above), the highest first.</summary>
    IEnumerable<Piece> PiecesUnder(Vec2 at, double z) =>
        _pieces.Where(pc => Polygon2.Contains(pc.Plan, at) && pc.ZAt(at) is var pz && pz <= z + AboveReach && pz >= z - DepthReach).OrderByDescending(pc => pc.ZAt(at));

    void Planar(MappedCase mc, StructuralActionData a, string source)
    {
        var outline = a.Points.Select(x => x.Xy).ToList();
        var rings = Tributary.Oriented(outline, a.Holes.Select(h => (IReadOnlyList<Vec2>)h.Select(x => x.Xy).ToList()));
        var z = a.Points[0].Z;
        var q = a.Direction * a.Value;
        var total = Tributary.Area(rings) * a.Value;
        var placed = 0.0;
        var taken = new List<Piece>();
        var rest = rings.ToList();
        foreach (var (pc, clipped, at) in _pieces
            .Select(pc => (Pc: pc, Clipped: rings.Select(r => (IReadOnlyList<Vec2>)Tributary.ClipTo(r, pc.Plan)).Where(r => r.Count >= 3).ToList()))
            .Where(x => Tributary.Area(x.Clipped) > 1e-9)
            .Select(x => (x.Pc, x.Clipped, At: Polygon2.InteriorPoint(x.Clipped[0])))
            .Where(x => x.Pc.ZAt(x.At) <= z + AboveReach && x.Pc.ZAt(x.At) >= z - DepthReach)
            .OrderByDescending(x => x.Pc.ZAt(x.At)).ToList())
        {
            // A piece of another slab under one already taken here (two floors within a metre) is not loaded twice.
            if (taken.Any(t => t.Shell.Source.SourceId != pc.Shell.Source.SourceId && Tributary.Area([Tributary.ClipTo(pc.Plan, t.Plan)]) > 1e-6)) continue;
            taken.Add(pc);
            placed += AreaOnPiece(mc, pc, clipped, q, source);
            // What the piece took is no longer on the roofs: its part of the rings, turned round.
            foreach (var r in clipped) rest.Add(r.Reverse().ToList());
        }
        if (_roofs.Count > 0 && rest.Count > 0) placed += OnFramedRoofs(mc, rest, z, a.Direction, a.Value);
        Unmapped(mc, total - placed, source);
    }

    /// <summary>A line load's stretches over slab pieces: on each piece's corners by the point rule, integrated along the stretch.</summary>
    double OnSlab(MappedCase mc, Vec2 a, Vec2 b, double z, Vec3 wPerLength, string source)
    {
        var placed = 0.0;
        var spans = new List<(double, double)>();
        foreach (var pc in _pieces)
        {
            spans.Clear();
            Polygon2.InsideIntervals([pc.Plan.ToArray()], a, b, 0, 1, spans);
            foreach (var (t0, t1) in spans)
            {
                var mid = a + (b - a) * ((t0 + t1) / 2);
                var pz = pc.ZAt(mid);
                if (pz > z + AboveReach || pz < z - DepthReach) continue;
                // The point rule integrated along the stretch: each Gauss point's share of its force on the piece's corners
                // (exact for a triangle's and a quadrilateral's weights, quadratic at most along a line).
                var (pa, pb) = (a + (b - a) * t0, a + (b - a) * t1);
                var force = wPerLength * (pb - pa).Length;
                foreach (var (g, w) in FrameElement.Gauss3)
                    placed += PointOnPiece(mc, pc, pa + (pb - pa) * ((1 + g) / 2), force * (w / 2), source);
                mc.Spread++;
            }
        }
        return placed;
    }

    void Curve(MappedCase mc, StructuralActionData act, string source)
    {
        var (a3, b3) = (act.Points[0], act.Points[1]);
        var (a, b) = (a3.Xy, b3.Xy);
        var z = (a3.Z + b3.Z) / 2;
        var len = (b - a).Length;
        if (len < 1e-9) return;
        var w = act.Direction * act.Value;
        var dir = (b - a) * (1 / len);
        // The bars along it: parallel within 10° in plan, their axis within 0.3 m, at most 1 m under it; each over the stretch it covers.
        var cands = new List<(int Bar, double U0, double U1, double Z, double Off)>();
        for (var k = 0; k < Problem.Elements.Count; k++)
        {
            var f = Frames[k];
            if (f.Source.Kind == "column") continue;
            var (pi, pj) = (f.I.At + f.OffI, f.J.At + f.OffJ);
            var fd = pj.Xy - pi.Xy;
            var fl = fd.Length;
            if (fl < 1e-6 || Math.Abs(Vec2.Cross(fd * (1 / fl), dir)) > Math.Sin(10 * Math.PI / 180)) continue;
            var fu = fd * (1 / fl);
            // The load's stretch the bar covers: its ends projected on the load's line.
            var (s0, s1) = (Vec2.Dot(pi.Xy - a, dir) / len, Vec2.Dot(pj.Xy - a, dir) / len);
            var (u0, u1) = (Math.Max(0, Math.Min(s0, s1)), Math.Min(1, Math.Max(s0, s1)));
            if (u1 - u0 < 1e-9) continue;
            var mid = a + (b - a) * ((u0 + u1) / 2);
            var off = Math.Abs(Vec2.Cross(fu, mid - pi.Xy));
            if (off > PlanReach) continue;
            var t = Vec2.Dot(mid - pi.Xy, fu) / fl;
            var fz = pi.Z + (pj.Z - pi.Z) * Math.Clamp(t, 0, 1);
            if (fz > z + AboveReach || fz < z - DepthReach) continue;
            cands.Add((k, u0, u1, fz, off));
        }
        var cuts = new List<double> { 0, 1 };
        foreach (var c in cands) { cuts.Add(c.U0); cuts.Add(c.U1); }
        cuts = cuts.Distinct().OrderBy(x => x).ToList();
        for (var i = 0; i + 1 < cuts.Count; i++)
        {
            var (u0, u1) = (cuts[i], cuts[i + 1]);
            if (u1 - u0 < 1e-9) continue;
            var um = (u0 + u1) / 2;
            var best = cands.Where(c => c.U0 <= um && c.U1 >= um).OrderByDescending(c => c.Z).ThenBy(c => c.Off).Select(c => (int?)c.Bar).FirstOrDefault();
            var (pa, pb) = (a + (b - a) * u0, a + (b - a) * u1);
            var force = (pb - pa).Length * act.Value;
            if (best is { } k)
            {
                var f = Frames[k];
                var (pi, pj) = (f.I.At + f.OffI, f.J.At + f.OffJ);
                var el = (pj - pi).Length;
                var fp = pj.Xy - pi.Xy;
                double X(Vec2 q) => Math.Clamp(Vec2.Dot(q - pi.Xy, fp) / Vec2.Dot(fp, fp), 0, 1) * el;
                var (x0, x1) = (X(pa), X(pb));
                if (x0 > x1) (x0, x1) = (x1, x0);
                if (x1 - x0 < 1e-9) mc.Loads.Span.Add((k, SpanLoad.Force(x0, act.Direction * force)));
                else mc.Loads.Span.Add((k, SpanLoad.Uniform(x0, x1, act.Direction * (force / (x1 - x0)))));
                continue;
            }
            var placed = OnSlab(mc, pa, pb, z, w, source);
            Unmapped(mc, force - placed, source);
        }
    }

    void PointLoad(MappedCase mc, StructuralActionData act, string source)
    {
        var at = act.Points[0];
        var f = act.Direction * act.Value;
        // A bar's node under it first (a column's head), then the bar under it, then the slab.
        var node = Enumerable.Range(0, Problem.Nodes.Count).Where(n => _carrying[n])
            .Select(n => (N: n, P: Problem.Nodes[n])).Where(x => (x.P.Xy - at.Xy).Length <= PlanReach && x.P.Z <= at.Z + AboveReach && x.P.Z >= at.Z - DepthReach)
            .OrderBy(x => (x.P.Xy - at.Xy).Length).ThenByDescending(x => x.P.Z).Select(x => (int?)x.N).FirstOrDefault();
        var bar = -1;
        var bx = 0.0;
        var bestD = double.PositiveInfinity;
        var bestZ = double.NegativeInfinity;
        for (var k = 0; k < Problem.Elements.Count; k++)
        {
            var fr = Frames[k];
            if (fr.Source.Kind == "column") continue;
            var (pi, pj) = (fr.I.At + fr.OffI, fr.J.At + fr.OffJ);
            var fp = pj.Xy - pi.Xy;
            var l2 = Vec2.Dot(fp, fp);
            if (l2 < 1e-12) continue;
            var t = Vec2.Dot(at.Xy - pi.Xy, fp) / l2;
            if (t < -1e-9 || t > 1 + 1e-9) continue;
            t = Math.Clamp(t, 0, 1);
            var d = (pi.Xy + fp * t - at.Xy).Length;
            var fz = pi.Z + (pj.Z - pi.Z) * t;
            if (d > PlanReach || fz > at.Z + AboveReach || fz < at.Z - DepthReach) continue;
            if (fz > bestZ + 1e-6 || (Math.Abs(fz - bestZ) <= 1e-6 && d < bestD)) { (bar, bx, bestD, bestZ) = (k, t * (pj - pi).Length, d, fz); }
        }
        if (node is { } n)
        {
            OnNode(mc, n, f);
            return;
        }
        if (bar >= 0) { mc.Loads.Span.Add((bar, SpanLoad.Force(bx, f))); return; }
        if (PiecesUnder(at.Xy, at.Z).FirstOrDefault() is { } pc)
        {
            Unmapped(mc, act.Value - PointOnPiece(mc, pc, at.Xy, f, source), source);
            mc.OnCorners++;
            return;
        }
        Unmapped(mc, act.Value, source);
    }

    // ------------------------------------------------------------ a point load on a slab piece

    /// <summary>
    /// The weights a point gives a convex piece's corners, statically consistent (they sum to one and their centroid is the
    /// point, so the corners' forces have the force's resultant and moment): barycentric on a triangle, the bilinear shape
    /// functions on a quadrilateral (the point's (ξ, η) by Newton), Wachspress's coordinates on a polygon of more sides.
    /// </summary>
    public static double[] CornerWeights(IReadOnlyList<Vec2> c, Vec2 p)
    {
        var n = c.Count;
        if (n == 3)
        {
            var area = Vec2.Cross(c[1] - c[0], c[2] - c[0]);
            return [Vec2.Cross(c[1] - p, c[2] - p) / area, Vec2.Cross(c[2] - p, c[0] - p) / area, Vec2.Cross(c[0] - p, c[1] - p) / area];
        }
        if (n == 4)
        {
            double xi = 0.5, eta = 0.5;
            for (var it = 0; it < 50; it++)
            {
                var m = c[0] * ((1 - xi) * (1 - eta)) + c[1] * (xi * (1 - eta)) + c[2] * (xi * eta) + c[3] * ((1 - xi) * eta);
                var r = m - p;
                if (r.Length < 1e-13) break;
                var dxi = (c[1] - c[0]) * (1 - eta) + (c[2] - c[3]) * eta;
                var deta = (c[3] - c[0]) * (1 - xi) + (c[2] - c[1]) * xi;
                var det = Vec2.Cross(dxi, deta);
                if (Math.Abs(det) < 1e-18) break;
                xi -= Vec2.Cross(r, deta) / det;
                eta -= Vec2.Cross(dxi, r) / det;
            }
            return [(1 - xi) * (1 - eta), xi * (1 - eta), xi * eta, (1 - xi) * eta];
        }
        // Wachspress: w_i = A(c_{i−1}, c_i, c_{i+1}) / (A(p, c_{i−1}, c_i) · A(p, c_i, c_{i+1})), normalized.
        double Tri(Vec2 a, Vec2 b, Vec2 d) => Vec2.Cross(b - a, d - a) / 2;
        var w = new double[n];
        for (var i = 0; i < n; i++)
        {
            var (prev, cur, next) = (c[(i + n - 1) % n], c[i], c[(i + 1) % n]);
            var (a1, a2) = (Tri(p, prev, cur), Tri(p, cur, next));
            if (Math.Abs(a1) < 1e-14 || Math.Abs(a2) < 1e-14)
            {
                // On a side: linear between its two ends.
                var (u, v) = Math.Abs(a1) < 1e-14 ? (prev, cur) : (cur, next);
                var t = Math.Clamp(Vec2.Dot(p - u, v - u) / Vec2.Dot(v - u, v - u), 0, 1);
                var o = new double[n];
                o[Array.IndexOf(c.ToArray(), u)] += 1 - t;
                o[Array.IndexOf(c.ToArray(), v)] += t;
                return o;
            }
            w[i] = Tri(prev, cur, next) / (a1 * a2);
        }
        var sum = w.Sum();
        return w.Select(x => x / sum).ToArray();
    }

    /// <summary>A force at a plan point of a piece: on its corners by <see cref="CornerWeights"/>; a corner nothing carries passes its share along a supported side to the nodes there, else over the piece by the 45° rule.</summary>
    double PointOnPiece(MappedCase mc, Piece pc, Vec2 at, Vec3 f, string source)
    {
        var w = CornerWeights(pc.Plan, at);
        var placed = 0.0;
        var n = pc.Corners.Count;
        for (var k = 0; k < n; k++)
        {
            if (Math.Abs(w[k]) < 1e-15) continue;
            var fk = f * w[k];
            if (OnNode(mc, pc.Corners[k], fk)) { placed += fk.Length * Math.Sign(w[k]); continue; }
            var (after, before) = (pc.Sides[k], pc.Sides[(k + n - 1) % n]);
            var side = after.Supported ? after : before.Supported ? before : null;
            var amount = f.Length > 0 ? w[k] : 0;
            var unit = f.Length > 0 ? f * (1 / f.Length) : f;
            if (side != null) { placed += Math.Sign(w[k]) * Lump(mc, side, side == after ? 0 : side.Length, amount * f.Length, unit); continue; }
            placed += Math.Sign(w[k]) * AreaOnPiece(mc, pc, [pc.Plan], fk * (1 / pc.Area), source);
        }
        return placed;
    }

    // ------------------------------------------------------------ framed roofs

    sealed record Carrier(int Element, Vec2 A, Vec2 Dir, double PlanLength, double Length);

    /// <summary>A carrier in a plane's frame: its stretch along it, its place across as t(s) = TA + K·(s − SA).</summary>
    sealed record Placed(Carrier C, double S0, double S1, double SA, double TA, double K)
    {
        public double T(double s) => TA + K * (s - SA);
    }

    sealed class RoofPlane
    {
        public required List<Vec2> Plan;
        /// <summary>The plane's plan in convex pieces (a plane may be concave: an L-shaped roof's).</summary>
        public required List<List<Vec2>> Convex;
        /// <summary>The plane's unit normal, upward: its Z is the pitch's cosine.</summary>
        public Vec3 Normal;
        public required List<Carrier> Carriers;
    }

    sealed class Roof
    {
        public required string Id;
        public required List<RoofPlane> Planes;
        public double ZMin, ZMax;
    }

    List<Roof> _roofs = [];

    static readonly string[] Sloping = ["rafter", "trimmer", "hip", "valley", "topChord"];
    static readonly string[] Longitudinal = ["purlin", "ridge"];

    static string RoleOf(AnalyticalFrame f) => f.Role is { } r && r.StartsWith("truss:") ? r[(r.LastIndexOf(':') + 1)..] : f.Role ?? "";

    /// <summary>
    /// The roofs framed in bars, with their planes as the roof module solves them (<see cref="AnalyticalRoof"/>), and each
    /// plane's carriers: the rafters, hip and valley rafters and trusses' top chords lying in it (in plan within it, at most a
    /// metre off it in height; a hip or valley on the line between two planes is in both), else its purlins and ridge beam.
    /// </summary>
    List<Roof> FramedRoofs()
    {
        var o = new List<Roof>();
        foreach (var ar in Model.Roofs)
        {
            var members = Enumerable.Range(0, Frames.Count).Where(k => Frames[k].Source.SourceId == ar.SourceId && Frames[k].Source.Category == AnalyticalKinds.Roofs).ToList();
            if (members.Count == 0) continue;
            List<(int K, Vec3 Pi, Vec3 Pj)> Of(string[] roles) => members.Where(k => roles.Contains(RoleOf(Frames[k].Source)))
                .Select(k => (k, Frames[k].I.At + Frames[k].OffI, Frames[k].J.At + Frames[k].OffJ)).Where(x => (x.Item3.Xy - x.Item2.Xy).Length > 1e-6).ToList();
            var carriers = Of(Sloping);
            if (carriers.Count == 0) carriers = Of(Longitudinal);
            var planes = new List<RoofPlane>();
            foreach (var pts in ar.Planes.Where(x => x.Count >= 3))
            {
                var normal = VecMath.NewellNormal(pts.ToList()).Normalized();
                if (normal.Z < 0) normal = -normal;
                if (normal.Z < 1e-6) continue;
                var plan = Polygon2.EnsureCcw(pts.Select(x => x.Xy).ToList()).ToList();
                if (Polygon2.SignedArea(plan) < 1e-9) continue;
                double ZAt(Vec2 q) => pts[0].Z - (normal.X * (q.X - pts[0].X) + normal.Y * (q.Y - pts[0].Y)) / normal.Z;
                bool Near(Vec2 q) => Polygon2.Contains(plan, q) || Enumerable.Range(0, plan.Count).Any(i => ShellPieces.DistToSegment(q, plan[i], plan[(i + 1) % plan.Count]) <= 0.02);
                var mine = carriers.Where(c => (c.Pi + c.Pj) * 0.5 is var mid && Near(mid.Xy) && Math.Abs(mid.Z - ZAt(mid.Xy)) <= DepthReach)
                    .Select(c =>
                    {
                        var plen = (c.Pj.Xy - c.Pi.Xy).Length;
                        return new Carrier(c.K, c.Pi.Xy, (c.Pj.Xy - c.Pi.Xy) * (1 / plen), plen, (c.Pj - c.Pi).Length);
                    }).ToList();
                var convex = ShellPieces.ConvexPieces(plan, []).Select(x => Polygon2.EnsureCcw(x).ToList()).Where(x => x.Count >= 3).ToList();
                planes.Add(new RoofPlane { Plan = plan, Convex = convex, Normal = normal, Carriers = mine });
            }
            if (planes.Count == 0) continue;
            var zs = members.SelectMany(k => new[] { Frames[k].I.At.Z, Frames[k].J.At.Z }).Concat(ar.Planes.SelectMany(x => x.Select(q => q.Z))).ToList();
            o.Add(new Roof { Id = ar.SourceId, Planes = planes, ZMin = zs.Min(), ZMax = zs.Max() });
        }
        return o;
    }

    /// <summary>
    /// An area load (its rings in plan, what slabs did not take) on the framed roofs it lies over (its height at most 1 m below
    /// the roof's lowest member and 1 m above its highest), plane by plane: within a plane each carrier takes the load over its
    /// tributary strip — across it, half way to the next carrier either side (an edge one to the plane's edge; a hip or valley
    /// rafter is the jack rafters' neighbour, so it takes what lies between their strips and its line), along it the stretch it
    /// covers — as a line load. Snow is per horizontal projection (q·w per metre of plan along the bar), any other load per true
    /// area (over the plane's cosine). What lies outside every plane is left. Returns the load placed, as q × its plan area.
    /// </summary>
    double OnFramedRoofs(MappedCase mc, List<IReadOnlyList<Vec2>> rings, double z, Vec3 dir, double q)
    {
        var placed = 0.0;
        foreach (var roof in _roofs.Where(r => z >= r.ZMin - DepthReach && z <= r.ZMax + DepthReach))
            foreach (var plane in roof.Planes.Where(x => x.Carriers.Count > 0))
            {
                // The plane's frame: along its most numerous carriers' direction in plan.
                var groups = new List<List<Carrier>>();
                foreach (var c in plane.Carriers)
                {
                    var g = groups.FirstOrDefault(x => Math.Abs(Vec2.Cross(x[0].Dir, c.Dir)) < Math.Sin(2 * Math.PI / 180));
                    if (g == null) groups.Add([c]); else g.Add(c);
                }
                var d = groups.MaxBy(x => x.Count)![0].Dir;
                var nrm = d.Perp;
                // Each carrier over its stretch along the frame, its place across as a line: t(s) = t0 + k·(s − s0).
                var items = plane.Carriers.Select(c =>
                {
                    var along = Vec2.Dot(c.Dir, d);
                    // A member nearly square to the frame (a neighbouring plane's rafter along its edge) is not this plane's carrier.
                    if (Math.Abs(along) < Math.Cos(80 * Math.PI / 180)) return (Placed?)null;
                    var (s0, t0) = (Vec2.Dot(c.A, d), Vec2.Dot(c.A, nrm));
                    var s1 = s0 + along * c.PlanLength;
                    return new Placed(c, Math.Min(s0, s1), Math.Max(s0, s1), s0, t0, Vec2.Dot(c.Dir, nrm) / along);
                }).OfType<Placed>().ToList();
                static double T(Placed x, double sv) => x.T(sv);
                var cuts = items.SelectMany(x => new[] { x.S0, x.S1 }).ToList();
                // Where two carriers cross along the frame, their order across changes: a cut there too.
                for (var i = 0; i < items.Count; i++)
                    for (var j = i + 1; j < items.Count; j++)
                    {
                        var (x, y) = (items[i], items[j]);
                        if (Math.Abs(x.K - y.K) < 1e-12) continue;
                        var sc = (y.TA - y.K * y.SA - (x.TA - x.K * x.SA)) / (x.K - y.K);
                        if (sc > Math.Max(x.S0, y.S0) + 1e-9 && sc < Math.Min(x.S1, y.S1) - 1e-9) cuts.Add(sc);
                    }
                cuts = cuts.Distinct().OrderBy(x => x).ToList();
                const double Far = 1e3;
                // The stretches between the cuts, each with the carriers over it; one over none (past the rafters' tails, by
                // the ridge) takes its nearest neighbour's, and the outermost reach the plane's edge.
                var spans = new List<(double A, double B, List<Placed> Active)>();
                for (var i = 0; i + 1 < cuts.Count; i++)
                {
                    var (ca, cb) = (cuts[i], cuts[i + 1]);
                    if (cb - ca < 1e-9) continue;
                    var mid = (ca + cb) / 2;
                    spans.Add((ca, cb, items.Where(x => x.S0 <= mid + 1e-12 && x.S1 >= mid - 1e-12).ToList()));
                }
                if (spans.Count == 0 || spans.All(x => x.Active.Count == 0)) continue;
                for (var i = 0; i < spans.Count; i++)
                    if (spans[i].Active.Count == 0)
                    {
                        var near = Enumerable.Range(0, spans.Count).Where(k => spans[k].Active.Count > 0).MinBy(k => Math.Abs(k - i));
                        spans[i] = (spans[i].A, spans[i].B, spans[near].Active);
                    }
                spans[0] = (-Far, spans[0].B, spans[0].Active);
                spans[^1] = (spans[^1].A, Far, spans[^1].Active);
                foreach (var (ca, cb, set) in spans)
                {
                    var mid = Math.Clamp((ca + cb) / 2, cuts[0], cuts[^1]);
                    var active = set.OrderBy(x => T(x, mid)).ToList();
                    for (var j = 0; j < active.Count; j++)
                    {
                        var x = active[j];
                        double Lo(double sv) => j == 0 ? T(x, sv) - Far : (T(active[j - 1], sv) + T(x, sv)) / 2;
                        double Hi(double sv) => j == active.Count - 1 ? T(x, sv) + Far : (T(x, sv) + T(active[j + 1], sv)) / 2;
                        Vec2 P(double sv, double tv) => d * sv + nrm * tv;
                        var strip = new List<Vec2> { P(ca, Lo(ca)), P(cb, Lo(cb)), P(cb, Hi(cb)), P(ca, Hi(ca)) };
                        var part = plane.Convex.SelectMany(piece => Tributary.ClipTo(strip, piece) is { Count: >= 3 } region
                            ? rings.Select(r => (IReadOnlyList<Vec2>)Tributary.ClipTo(r, region)).Where(r => r.Count >= 3) : []).ToList();
                        var area = Tributary.Area(part);
                        if (Math.Abs(area) < 1e-12) continue;
                        var c = x.C;
                        // Per metre of the bar: the plan's load over the bar's length, over the plane's cosine for a true-area load.
                        var k = c.PlanLength / c.Length * (mc.Snow ? 1 : 1 / plane.Normal.Z);
                        foreach (var t in Tributary.Profile(part, c.A, c.Dir))
                        {
                            // A strip of a carrier oblique to the frame (a hip) may reach past its ends in its own axis: at its ends.
                            if (t.Clip(double.NegativeInfinity, 0) is { } before) mc.Loads.Span.Add((c.Element, SpanLoad.Force(0, dir * (q * before.Integral * k * c.Length / c.PlanLength))));
                            if (t.Clip(c.PlanLength, double.PositiveInfinity) is { } after) mc.Loads.Span.Add((c.Element, SpanLoad.Force(c.Length, dir * (q * after.Integral * k * c.Length / c.PlanLength))));
                            if (t.Clip(0, c.PlanLength) is not { } tc) continue;
                            var (x0, x1) = (tc.S0 / c.PlanLength * c.Length, tc.S1 / c.PlanLength * c.Length);
                            mc.Loads.Span.Add((c.Element, new SpanLoad(x0, x1, dir * (q * tc.W0 * k), dir * (q * tc.W1 * k))));
                        }
                        placed += q * area;
                    }
                }
                mc.OnRoofs++;
            }
        return placed;
    }
}
