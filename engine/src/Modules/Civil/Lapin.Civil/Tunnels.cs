using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Alignments;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// The tunnel lining (docs/plan-wing-walls.md, S4): a cross-section (<see cref="TunnelSections"/>) swept along an axis between two
/// stations, kept square to the axis in plan and plumb, following the axis's curves and its longitudinal profile; cut into casting
/// blocks (or a TBM's rings), each a part with its own GlobalId and quantities; an outer shell (shotcrete) as parts of its own.
/// <para>
/// It is a corridor kind (<see cref="ICorridorKind"/>): its axis is its own, as a road's, taken between two stations of an
/// alignment, a road, a path, a wall or a drafted line (its plan geometry and its red line copied, its stations kept) or drawn; so
/// the corridor configurator's stations, red line, cross-sections at stations and long section are the lining's too, and a portal,
/// an array or a section can follow it.
/// </para>
/// <para>
/// Exact where the geometry is: along a straight with a constant grade every face is ruled (planes, cylinders, or a degree 1 × 2
/// NURBS for an arc on a graded straight); a straight with a vertical curve is a translational NURBS (its parabola exact, degree 2);
/// a circular arc on a level grade is a revolution (tori, cones, cylinders and planes as NURBS). A clothoid, or an arc on a grade,
/// is skinned through sections at the chord rule's stations (a cubic B-spline across them, within the chord tolerance). The mesh
/// the views draw is the same parametrisation cut by <see cref="Chords"/>. Superelevation is not applied: a tunnel's section stays
/// plumb; the road's crossfall inside it is its own.
/// </para>
/// </summary>
public static class Tunnels
{
    public const string Class = "LapinTunnelLining";
    public const string Key = "lining";
    public const string PreviewClass = "LapinTunnel";
    public const string Color = "#b9b5ab";
    public const string PrimaryColor = "#8f8a7e";
    public const string SetName = "Lapin_TunnelLiningQuantities";
    static readonly Vec3 Ez = new(0, 0, 1);

    public static bool Is(Entity e) => e.Class == Class;

    public static Portals.Values ValuesOf(IModelView m, Entity e) => TunnelSections.Evaluate(e.Attrs[Key] as JsonObject, m);

    public static LiningSection SectionOf(IModelView m, Portals.Values v) => TunnelSections.Build(v, TunnelSections.ProfileRings(m, (string?)v.Spec["profileId"]));

    // ------------------------------------------------------------------ the axis

    /// <summary>The red line: the elevation at a station, the stations where it changes its kind (PVIs, a vertical curve's ends), and whether it is a polynomial between them.</summary>
    public sealed record Grade(Func<double, double> Z, List<double> Breaks, bool Polynomial);

    public static Grade GradeOf(IModelView m, Entity e, Chain c)
    {
        var spec = CorridorSpec.Of(e);
        if (spec.Mode == "design" && spec.Pvis.Count >= 2 && VerticalProfile.Problem(spec.Pvis) == null)
        {
            var p = VerticalProfile.Build(spec.Pvis);
            var breaks = new List<double>();
            for (var i = 0; i < p.Pvis.Count; i++)
            {
                var pv = p.Pvis[i];
                if (i == 0 || i == p.Pvis.Count - 1 || pv.Length <= 0) breaks.Add(pv.Station);
                else { breaks.Add(pv.Station - pv.Length / 2); breaks.Add(pv.Station + pv.Length / 2); }
            }
            return new(p.ElevationAt, breaks, true);
        }
        if (spec.Mode == "design" || spec.Pvis.Count == 0)
        {
            var z = Corridors.Fallback(m, e);
            return new(_ => z, [], true);
        }
        return new(Corridors.RedLine(m, e, c), [], false);
    }

    public enum SpanKind { Ruled, Translational, Revolve, Skin }

    /// <summary>A frame of the sweep: the axis point (with its elevation) and the vector across (+ left, a metre of offset; longer at a mitre or a skewed end).</summary>
    public readonly record struct Frame(Vec3 O, Vec3 U)
    {
        public Vec3 Map(Vec2 q) => O + U * q.X + Ez * q.Y;
        public Vec3 Map(Vec3 q) => O + U * q.X + Ez * q.Y;
    }

    /// <summary>
    /// A stretch of the sweep of one kind between two stations: on one segment of the axis, between the red line's breaks. Its frames
    /// run over u ∈ [0, 1]; <see cref="Us"/> are the mesh's stations along it.
    /// </summary>
    public sealed class Span
    {
        public double A { get; init; }
        public double B { get; init; }
        public int Seg { get; init; }
        public SpanKind Kind { get; set; }
        public required Func<double, Frame> Inner { get; init; }
        public Frame? Start { get; set; }
        public Frame? End { get; set; }
        /// <summary>A revolution's centre in plan and its turn (radians, + left).</summary>
        public Vec2 Centre { get; init; }
        public double Turn { get; init; }
        public double[] Us { get; set; } = [0, 1];
        /// <summary>How far the mesh's facets stray from the sweep along it at most (the chord rule's smallest step on a long radius leaves more than its tolerance).</summary>
        public double Sag { get; init; }

        public Frame At(double u)
        {
            if (Kind == SpanKind.Ruled)
            {
                var (f0, f1) = (Start ?? Inner(0), End ?? Inner(1));
                return new(f0.O + (f1.O - f0.O) * u, f0.U + (f1.U - f0.U) * u);
            }
            if (u <= 0 && Start is { } s) return s;
            if (u >= 1 && End is { } e) return e;
            return Inner(u);
        }
    }

    /// <summary>A casting block (or a ring): its index, its stations and its spans.</summary>
    public sealed record Block(int Index, double From, double To, List<Span> Spans);

    /// <summary>The spans of the sweep between two stations: cut at the axis's vertices and the red line's breaks.</summary>
    static List<Span> Spans(Chain c, Grade g, double from, double to, double xMax)
    {
        var cuts = new List<double> { from, to };
        for (var i = 0; i < c.Path.Count; i++) cuts.Add(c.StationOf(i));
        cuts.AddRange(g.Breaks);
        var ordered = cuts.Where(s => s >= from - 1e-9 && s <= to + 1e-9).Select(s => Math.Clamp(s, from, to)).Order().ToList();
        var clean = new List<double>();
        foreach (var s in ordered) if (clean.Count == 0 || s - clean[^1] > 1e-6) clean.Add(s);
        if (clean.Count == 1) clean.Add(to);
        clean[^1] = to;
        var o = new List<Span>();
        for (var k = 0; k + 1 < clean.Count; k++) o.Add(MakeSpan(c, g, clean[k], clean[k + 1], xMax));
        return o;
    }

    static Span MakeSpan(Chain c, Grade g, double a, double b, double xMax)
    {
        var seg = c.SegmentAt((a + b) / 2);
        var straight = !c.IsCurved(seg);
        var (ua, ub) = (c.Across(a, seg), c.Across(b, seg));
        Frame Inner(double u)
        {
            var s = a + (b - a) * u;
            var p = c.PointAt(s);
            var across = straight ? ua + (ub - ua) * u : c.DirectionAt(s).Perp;
            return new(p.At(g.Z(s)), new Vec3(across.X, across.Y, 0));
        }
        var (za, zb, zm) = (g.Z(a), g.Z(b), g.Z((a + b) / 2));
        var linear = g.Polynomial && Math.Abs(zm - (za + zb) / 2) < 1e-9;
        var level = linear && Math.Abs(zb - za) < 1e-9;
        var square = (ua - ub).Length < 1e-12 && Math.Abs(ua.Length - 1) < 1e-12;
        var len = b - a;
        var horizontal = c.Alignment?.Horizontal[seg];
        if (straight && linear) return new Span { A = a, B = b, Seg = seg, Kind = SpanKind.Ruled, Inner = Inner, Us = [0, 1] };
        if (straight && g.Polynomial && square)
        {
            var z2 = Math.Abs(4 * (za + zb - 2 * zm) / (len * len));
            var n = z2 < 1e-12 ? 1 : Math.Max(1, (int)Math.Ceiling(len / Math.Sqrt(8 * Chords.Tolerance / z2)));
            return new Span { A = a, B = b, Seg = seg, Kind = SpanKind.Translational, Inner = Inner, Us = Steps(n), Sag = z2 * (len / n) * (len / n) / 8 };
        }
        if (horizontal is { Type: HorizontalType.CircularArc } h && level)
        {
            var r = h.StartRadius != 0 ? h.StartRadius : h.EndRadius;
            var pa = c.PointAt(a);
            var centre = pa + c.DirectionAt(a).Perp * r;
            var turn = len / r;
            var step = Chords.Step(Math.Abs(r) + xMax);
            var n = Math.Max(1, (int)Math.Ceiling(Math.Abs(turn) / step - 1e-9));
            return new Span { A = a, B = b, Seg = seg, Kind = SpanKind.Revolve, Inner = Inner, Centre = centre, Turn = turn, Us = Steps(n), Sag = (Math.Abs(r) + xMax) * (1 - Math.Cos(Math.Abs(turn) / n / 2)) };
        }
        // Skinned: as many sections as the tightest bend (at the lining's outside) and the vertical curve ask.
        const int probes = 32;
        double heading(double s) { var d = c.DirectionAt(s); return Math.Atan2(d.Y, d.X); }
        double total = 0, kMax = 0;
        var prev = heading(a);
        for (var k = 1; k <= probes; k++)
        {
            var hk = heading(a + len * k / probes);
            var dh = Math.Abs(Math.IEEERemainder(hk - prev, 2 * Math.PI));
            total += dh;
            kMax = Math.Max(kMax, dh / (len / probes));
            prev = hk;
        }
        var nh = total < 1e-12 ? 1 : (int)Math.Ceiling(total / Chords.Step((kMax > 1e-12 ? 1 / kMax : 1e6) + xMax));
        int nv;
        if (g.Polynomial)
        {
            var z2 = Math.Abs(4 * (za + zb - 2 * zm) / (len * len));
            nv = z2 < 1e-12 ? 1 : (int)Math.Ceiling(len / Math.Sqrt(8 * Chords.Tolerance / z2));
        }
        else nv = (int)Math.Ceiling(len / 1.0);
        var ns = Math.Max(2, Math.Max(nh, nv));
        var rOut = (kMax > 1e-12 ? 1 / kMax : 1e6) + xMax;
        return new Span { A = a, B = b, Seg = seg, Kind = SpanKind.Skin, Inner = Inner, Us = Steps(ns), Sag = Math.Min(1, rOut * (1 - Math.Cos(total / ns / 2))) + Chords.Tolerance };
    }

    static double[] Steps(int n) => [.. Enumerable.Range(0, n + 1).Select(k => (double)k / n)];

    // ------------------------------------------------------------------ the lining built

    public sealed record Part(string Key, string Name, string Layer, int Block, Solid Solid);

    public sealed class Built
    {
        public required Chain Chain { get; init; }
        public required Grade Grade { get; init; }
        public required Portals.Values Values { get; init; }
        public required LiningSection Section { get; init; }
        public List<Block> Blocks { get; } = [];
        public List<Part> Parts { get; } = [];
        public List<string> Notes { get; } = [];
        public bool Rings { get; init; }
        public Frame StartFrame { get; set; }
        public Frame EndFrame { get; set; }
        /// <summary>The block boundaries: the stations where one block ends and the next starts.</summary>
        public List<double> Joints { get; } = [];
        readonly Dictionary<string, ExactBody?> _exact = [];
        /// <summary>Why a part's exact body was not made (written as its facets then), by its key.</summary>
        public Dictionary<string, string> ExactProblems { get; } = [];

        /// <summary>A part's exact body (its block's spans, its layer's rings), checked against its facets; null when it is not that body.</summary>
        public ExactBody? ExactOf(Part p)
        {
            lock (_exact)
            {
                if (_exact.TryGetValue(p.Key, out var known)) return known;
                var spans = p.Block >= 0 ? Blocks[p.Block].Spans : [.. Blocks.SelectMany(b => b.Spans)];
                var (inner, outer) = p.Layer == "primary" ? (Section.Outer, Section.Primary!) : (Section.Inner, Section.Outer);
                ExactBody? body = null;
                string? why;
                try { (body, why) = Exact(spans, inner, outer, p.Solid); }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { why = ex.Message; }
                if (why != null) ExactProblems[p.Key] = why;
                return _exact[p.Key] = body;
            }
        }
    }

    static readonly ConditionalWeakTable<Solid, Built> Made = new();

    /// <summary>The lining a body was built as, when it is still known.</summary>
    public static Built? Of(Solid s) => Made.TryGetValue(s, out var b) ? b : null;

    public static bool AsAssembly(Portals.Values v) => v.Text("ifc", "assembly") == "assembly";

    /// <summary>The skew of an end: its plane turned about the vertical by so many degrees (+ anticlockwise) from square to the axis.</summary>
    public static double SkewOf(Entity e, bool atEnd) => Math.Clamp(e.NumOrNull(atEnd ? "skewEnd" : "skewStart") ?? 0, -60, 60);

    public static Built Build(IModelView m, Entity e, bool bodies = true)
    {
        var c = Corridors.ChainOf(e);
        var v = ValuesOf(m, e);
        var section = SectionOf(m, v);
        var g = GradeOf(m, e, c);
        var rings = v.Text("preset", "horseshoe") == "circular" && TunnelSections.ProfileRings(m, (string?)v.Spec["profileId"]) == null;
        var built = new Built { Chain = c, Grade = g, Values = v, Section = section, Rings = rings };
        built.Notes.AddRange(section.Problems);
        if (c.Problem != null) built.Notes.Add(c.Problem);
        if (c.Segments == 0 || c.Length < 0.05) { built.Notes.Add("The lining's axis has no length."); return built; }
        var box = section.Outside.Box();
        var xMax = Math.Max(Math.Abs(box.MinX), Math.Abs(box.MaxX));
        var step = Math.Max(0.3, rings ? v.Maybe("ringWidth") ?? 1.8 : v.Maybe("blockLength") ?? 12.5);
        // The blocks: one every so many metres from the start, the last what is left (a few centimetres more go to the one before).
        var cuts = new List<double> { c.Start };
        for (var k = 1; c.Start + k * step < c.End - Math.Min(0.05, step / 10); k++) cuts.Add(c.Start + k * step);
        cuts.Add(c.End);
        built.Joints.AddRange(cuts.Skip(1).SkipLast(1));
        var assembly = AsAssembly(v);
        var ranges = assembly ? cuts.Zip(cuts.Skip(1)).ToList() : [(c.Start, c.End)];
        if (ranges.Count > 2000) { built.Notes.Add("More than 2000 blocks: the lining is built whole; make its blocks longer."); ranges = [(c.Start, c.End)]; }
        for (var k = 0; k < ranges.Count; k++) built.Blocks.Add(new Block(k, ranges[k].Item1, ranges[k].Item2, Spans(c, g, ranges[k].Item1, ranges[k].Item2, xMax)));
        // The skewed ends: the first span's start and the last's end on a vertical plane turned from square (exact along a straight at a constant grade).
        var first = built.Blocks[0].Spans[0];
        var last = built.Blocks[^1].Spans[^1];
        built.StartFrame = first.At(0);
        built.EndFrame = last.At(1);
        foreach (var (span, atEnd) in new[] { (first, false), (last, true) })
        {
            var skew = SkewOf(e, atEnd);
            if (Math.Abs(skew) < 1e-9) continue;
            if (span.Kind != SpanKind.Ruled) { built.Notes.Add($"The {(atEnd ? "end" : "start")} is left square: a skewed end needs a straight axis at a constant grade in its last span."); continue; }
            var f = span.At(atEnd ? 1 : 0);
            var t = c.DirectionAt(atEnd ? span.B : span.A);
            var grade = (span.Inner(1).O.Z - span.Inner(0).O.Z) / (span.B - span.A);
            var k = Math.Tan(skew * Math.PI / 180);
            // A point x across lies on the plane where the axis is −x·tan(skew) from the end: along the axis there, its grade too.
            var skewed = new Frame(f.O, f.U - (new Vec3(t.X, t.Y, 0) + Ez * grade) * k);
            if (atEnd) { span.End = skewed; built.EndFrame = skewed; } else { span.Start = skewed; built.StartFrame = skewed; }
        }
        if (!bodies) return built;
        foreach (var block in built.Blocks)
        {
            var whole = !assembly;
            var n = block.Index + 1;
            var (key, name) = whole ? ("lining", "Tunnel lining") : rings ? ($"ring:{block.Index}", $"Ring {n}") : ($"block:{block.Index}", $"Block {n}");
            if (Body(block.Spans, section.Inner, section.Outer, "lining") is { } solid) built.Parts.Add(new Part(key, name, "lining", whole ? -1 : block.Index, solid));
            if (section.Primary is { } outside && Body(block.Spans, section.Outer, outside, "primary") is { } shell)
                built.Parts.Add(new Part(whole ? "primary" : $"primary:{block.Index}", whole ? "Outer shell" : $"Outer shell {n}", "primary", whole ? -1 : block.Index, shell));
        }
        foreach (var p in built.Parts) Made.AddOrUpdate(p.Solid, built);
        return built;
    }

    /// <summary>The ring between two outlines swept along the spans, closed at both ends: the mesh the views draw.</summary>
    public static Solid? Body(IReadOnlyList<Span> spans, LiningRing? inner, LiningRing outer, string tag)
    {
        var verts = new List<Vec3>();
        var faces = new List<FaceInput>();
        var po = outer.Points();
        var pi = inner?.Points();
        int[] Ring(List<Vec2> pts, Frame f)
        {
            var o = new int[pts.Count];
            for (var i = 0; i < pts.Count; i++) { verts.Add(f.Map(pts[i])); o[i] = verts.Count - 1; }
            return o;
        }
        void Strip(int[] a, int[] b, bool inward)
        {
            var n = a.Length;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                int[] quad = inward ? [a[j], a[i], b[i], b[j]] : [a[i], a[j], b[j], b[i]];
                if (Planar(verts[quad[0]], verts[quad[1]], verts[quad[2]], verts[quad[3]])) faces.Add(new FaceInput(quad));
                else { faces.Add(new FaceInput([quad[0], quad[1], quad[2]])); faces.Add(new FaceInput([quad[0], quad[2], quad[3]])); }
            }
        }
        int[]? firstO = null, firstI = null, lastO = null, lastI = null;
        foreach (var sp in spans)
        {
            int[]? prevO = null, prevI = null;
            foreach (var u in sp.Us)
            {
                var f = sp.At(u);
                var ro = Ring(po, f);
                var ri = pi != null ? Ring(pi, f) : null;
                if (prevO != null) { Strip(prevO, ro, false); if (ri != null) Strip(prevI!, ri, true); }
                prevO = ro;
                prevI = ri;
                firstO ??= ro;
                firstI ??= ri;
            }
            lastO = prevO;
            lastI = prevI;
        }
        if (firstO == null || lastO == null) return null;
        faces.Add(new FaceInput([.. Enumerable.Reverse(firstO)], firstI != null ? [[.. firstI]] : null));
        faces.Add(new FaceInput(lastO, lastI != null ? [[.. Enumerable.Reverse(lastI)]] : null));
        var s = SolidBuilder.Make(verts, faces, tag);
        if (Measure.SignedVolume(s) < 0) s = SolidBuilder.Flip(s);
        return s;
    }

    static bool Planar(Vec3 a, Vec3 b, Vec3 c, Vec3 d)
    {
        var n = Vec3.Cross(b - a, c - a);
        var l = n.Length;
        if (l < 1e-15) return true;
        return Math.Abs(Vec3.Dot(d - a, n * (1 / l))) < 1e-9;
    }

    // ------------------------------------------------------------------ exactly

    static (int, bool) Flip((int E, bool F) e) => (e.E, !e.F);

    static (int, bool) RingEdge(ExactBuilder b, Frame f, Piece2 p)
    {
        if (!p.Arc) return b.Line(f.Map(p.A), f.Map(p.B));
        if (Math.Abs(f.U.Length - 1) < 1e-12 && Math.Abs(f.U.Z) < 1e-12)
            return b.Arc(new Frame3(f.Map(p.Centre), Vec3.Cross(f.U, Ez).Normalized(), f.U), p.Radius, f.Map(p.A), f.Map(p.B), p.Sweep);
        return b.Edge(f.Map(p.A), f.Map(p.B), new SplineCurve(p.Curve().Transformed(f.Map)));
    }

    /// <summary>
    /// The exact body of a layer between <paramref name="inner"/> and <paramref name="outer"/> along the spans: a face per piece per span
    /// (on its plane, cylinder, ruled, translational, revolved or skinned surface), the two ends planar; checked against
    /// <paramref name="reference"/> (its facets) within 2 mm, or what its facets stray from the sweep where the chord rule's smallest step leaves more.
    /// </summary>
    public static (ExactBody? Body, string? Problem) Exact(IReadOnlyList<Span> spans, LiningRing? inner, LiningRing outer, Solid reference)
    {
        var b = new ExactBuilder();
        var rings = inner != null ? new[] { outer, inner } : [outer];
        foreach (var sp in spans)
            foreach (var ring in rings)
                foreach (var p in ring.Pieces) Face(b, sp, p);
        var (f0, f1) = (spans[0].At(0), spans[^1].At(1));
        b.PlaneFace([.. rings.Select(r => (IReadOnlyList<(int, bool)>)[.. r.Pieces.Select(p => RingEdge(b, f0, p))])]);
        b.PlaneFace([.. rings.Select(r => (IReadOnlyList<(int, bool)>)[.. r.Pieces.Select(p => RingEdge(b, f1, p))])]);
        var raw = b.Body();
        return ExactCheck.Fit(raw, reference, Math.Max(0.002, 1.5 * spans.Max(s => s.Sag)));
    }

    static void Face(ExactBuilder b, Span sp, Piece2 p)
    {
        var (f0, f1) = (sp.At(0), sp.At(1));
        var e0 = RingEdge(b, f0, p);
        var e1 = RingEdge(b, f1, p);
        ExactSurface? surface = null;
        (int, bool) l0, l1;
        switch (sp.Kind)
        {
            case SpanKind.Ruled:
            {
                l0 = b.Line(f0.Map(p.A), f1.Map(p.A));
                l1 = b.Line(f0.Map(p.B), f1.Map(p.B));
                var d = f1.O - f0.O;
                if (!p.Arc)
                {
                    if (!Planar(f0.Map(p.A), f0.Map(p.B), f1.Map(p.B), f1.Map(p.A))) surface = Ruled(p, f0, f1);
                }
                else if ((f0.U - f1.U).Length < 1e-12 && Math.Abs(f0.U.Length - 1) < 1e-12 && Vec3.Cross(d.Normalized(), Vec3.Cross(f0.U, Ez)).Length < 1e-12)
                    surface = new CylinderSurface(Frame3.Of(f0.Map(p.Centre), d.Normalized(), f0.U), p.Radius);
                else surface = Ruled(p, f0, f1);
                break;
            }
            case SpanKind.Translational:
            {
                var path = PathCurve(sp, f0, f1);
                var section = p.Curve().Transformed(f0.Map);
                l0 = b.Edge(f0.Map(p.A), f1.Map(p.A), new SplineCurve(path.Transformed(q => q + (f0.Map(p.A) - f0.O))));
                l1 = b.Edge(f0.Map(p.B), f1.Map(p.B), new SplineCurve(path.Transformed(q => q + (f0.Map(p.B) - f0.O))));
                if (p.Arc || Math.Abs(p.A.X - p.B.X) > 1e-12) surface = new SplineSurface(Translational(section, path));
                break;
            }
            case SpanKind.Revolve:
            {
                (int, bool) Around(Vec2 q)
                {
                    var a = f0.Map(q);
                    var o = new Vec3(sp.Centre.X, sp.Centre.Y, a.Z);
                    var r = (a - o).Length;
                    return b.Arc(new Frame3(o, Ez, (a - o) * (1 / r)), r, a, f1.Map(q), sp.Turn);
                }
                l0 = Around(p.A);
                l1 = Around(p.B);
                if (p.Arc || Math.Abs(p.A.Y - p.B.Y) > 1e-12)
                    surface = new SplineSurface(BSplineSurface.Revolve(p.Curve().Transformed(f0.Map), new Vec3(sp.Centre.X, sp.Centre.Y, 0), Ez, sp.Turn));
                break;
            }
            default:
            {
                var sections = sp.Us.Select(u => p.Curve().Transformed(sp.At(u).Map)).ToList();
                var skin = BSplineSurface.Skin(sections, 3, sp.Us);
                var (u0, u1) = (skin.DomainU.Start, skin.DomainU.End);
                l0 = b.Edge(f0.Map(p.A), f1.Map(p.A), new SplineCurve(skin.IsoU(u0)));
                l1 = b.Edge(f0.Map(p.B), f1.Map(p.B), new SplineCurve(skin.IsoU(u1)));
                surface = new SplineSurface(skin);
                break;
            }
        }
        IReadOnlyList<(int, bool)> loop = [e0, l1, Flip(e1), Flip(l0)];
        if (surface == null) b.PlaneFace([loop]);
        else
        {
            var inside = surface is SplineSurface ss
                ? ss.Surface.At((ss.Surface.DomainU.Start + ss.Surface.DomainU.End) / 2, (ss.Surface.DomainV.Start + ss.Surface.DomainV.End) / 2)
                : sp.At(0.5).Map(p.At(0.5));
            b.Face(surface, [loop], inside);
        }
    }

    /// <summary>The surface ruled between a piece at two frames (degree 1 across them): exact for a sweep along a straight at a constant grade, its ends square, mitred or skewed.</summary>
    static SplineSurface Ruled(Piece2 p, Frame f0, Frame f1) => new(BSplineSurface.Skin([p.Curve().Transformed(f0.Map), p.Curve().Transformed(f1.Map)], 1, [0.0, 1.0]));

    /// <summary>The axis point's path along a span with a vertical curve: a quadratic Bézier (the parabola exactly), from the start's axis point.</summary>
    static BSplineCurve PathCurve(Span sp, Frame f0, Frame f1)
    {
        var fm = sp.Inner(0.5);
        var mid = (f0.O + f1.O) * 0.5;
        var zc = 2 * fm.O.Z - (f0.O.Z + f1.O.Z) / 2;
        return new BSplineCurve(2, [0, 0, 0, 1, 1, 1], [f0.O, new Vec3(mid.X, mid.Y, zc), f1.O]);
    }

    /// <summary>A section curve translated along a polynomial path (its start at the curve's frame): the net P[i, j] = S[i] + C[j] − C[0], weights the section's.</summary>
    static BSplineSurface Translational(BSplineCurve section, BSplineCurve path)
    {
        var (n, k) = (section.Points.Length, path.Points.Length);
        var pts = new Vec3[n, k];
        var w = new double[n, k];
        for (var i = 0; i < n; i++)
            for (var j = 0; j < k; j++) { pts[i, j] = section.Points[i] + (path.Points[j] - path.Points[0]); w[i, j] = section.Weight(i); }
        return new BSplineSurface(section.Degree, path.Degree, section.Knots, path.Knots, pts, w);
    }

    // ------------------------------------------------------------------ quantities

    static readonly double[] Gx = [-0.9602898564975363, -0.7966664774136267, -0.5255324099163290, -0.1834346424956498, 0.1834346424956498, 0.5255324099163290, 0.7966664774136267, 0.9602898564975363];
    static readonly double[] Gw = [0.1012285362903763, 0.2223810344533745, 0.3137066458778873, 0.3626837833783620, 0.3626837833783620, 0.3137066458778873, 0.2223810344533745, 0.1012285362903763];

    static IEnumerable<(double U, double W)> Nodes(int parts)
    {
        for (var k = 0; k < parts; k++)
            for (var g = 0; g < Gx.Length; g++) yield return ((k + (Gx[g] + 1) / 2) / parts, Gw[g] / 2 / parts);
    }

    static (Vec3 dO, Vec3 dU) Rates(Span sp, double u)
    {
        const double h = 1e-6;
        var (a, b) = (sp.At(Math.Max(0, u - h)), sp.At(Math.Min(1, u + h)));
        var du = Math.Min(1, u + h) - Math.Max(0, u - h);
        return ((b.O - a.O) * (1 / du), (b.U - a.U) * (1 / du));
    }

    static double Cross(Vec3 a, Vec3 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>
    /// The volume of a section of area <paramref name="area"/> and first moment <paramref name="qx"/> swept along the spans: ∫ (O′ × U) A + (U′ × U) Qx du
    /// (in plan; the grade shears the section and leaves its volume). Exact for the sweep: on a curve the inner side runs shorter.
    /// </summary>
    public static double Volume(IReadOnlyList<Span> spans, double area, double qx)
    {
        var v = 0.0;
        foreach (var sp in spans)
            foreach (var (u, w) in Nodes(Math.Clamp(sp.Us.Length - 1, 1, 64)))
            {
                var f = sp.At(u);
                var (dO, dU) = Rates(sp, u);
                v += w * (Cross(dO, f.U) * area + Cross(dU, f.U) * qx);
            }
        return Math.Abs(v);
    }

    /// <summary>The area of a ring's surface swept along the spans, on the true surface (Gauss in both directions).</summary>
    public static double Area(IReadOnlyList<Span> spans, LiningRing ring)
    {
        var total = 0.0;
        foreach (var sp in spans)
        {
            var along = Math.Clamp(sp.Us.Length - 1, 1, 64);
            foreach (var (u, w) in Nodes(along))
            {
                var f = sp.At(u);
                var (dO, dU) = Rates(sp, u);
                foreach (var p in ring.Pieces)
                {
                    var across = p.Arc ? Math.Max(1, (int)Math.Ceiling(Math.Abs(p.Sweep) / (Math.PI / 4))) : 1;
                    foreach (var (t, wt) in Nodes(across))
                    {
                        var q = p.At(t);
                        var dq = p.Tangent(t);
                        var su = dO + dU * q.X;
                        var st = f.U * dq.X + Ez * dq.Y;
                        total += w * wt * Vec3.Cross(su, st).Length;
                    }
                }
            }
        }
        return total;
    }

    /// <summary>An end's area: the section's, stretched across where the end is skewed (|U| in plan).</summary>
    static double EndArea(Frame f, double area) => area * Math.Sqrt(f.U.X * f.U.X + f.U.Y * f.U.Y);

    public sealed record Measures(double Length, double Volume, double Inner, double Outer, double Ends);

    public static Measures MeasureOf(Built b, Part p)
    {
        var spans = p.Block >= 0 ? b.Blocks[p.Block].Spans : [.. b.Blocks.SelectMany(x => x.Spans)];
        var (inner, outer) = p.Layer == "primary" ? (b.Section.Outer, b.Section.Primary!) : (b.Section.Inner, b.Section.Outer);
        var area = outer.Area - inner.Area;
        var qx = outer.Qx - inner.Qx;
        var length = spans.Sum(s => s.B - s.A);
        return new(length, Volume(spans, area, qx), Area(spans, inner), Area(spans, outer), EndArea(spans[0].At(0), area) + EndArea(spans[^1].At(1), area));
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        var built = Of(g.Parts[0].Solid) ?? Build(m, e);
        if (built.Parts.Count == 0) yield break;
        var outerFormwork = built.Values.Flag("outerFormwork");
        var material = e.Str("material") is { } mid && m.Get(mid) is { Class: IfcClass.Material } ? mid : null;
        var shellMaterial = e.Str("primaryMaterial") is { } pid && m.Get(pid) is { Class: IfcClass.Material } ? pid : null;
        double concrete = 0, shell = 0, inner = 0, outer = 0, ends = 0, shellArea = 0;
        var sets = new List<QuantitySet>();
        foreach (var p in built.Parts)
        {
            var q = MeasureOf(built, p);
            if (p.Layer == "primary")
            {
                shell += q.Volume;
                shellArea += q.Outer;
                if (p.Block >= 0) sets.Add(new QuantitySet(SetName, [new("Length", q.Length, "LENGTH"), new("ShellVolume", q.Volume, "VOLUME", "Outer shell"), new("ShellArea", q.Outer, "AREA", "Outer shell, its outside")], Part: p.Key, PartName: p.Name, Material: shellMaterial));
                continue;
            }
            concrete += q.Volume;
            inner += q.Inner;
            outer += q.Outer;
            ends += q.Ends;
            if (p.Block >= 0)
                sets.Add(new QuantitySet(SetName,
                [
                    new("Length", q.Length, "LENGTH"), new("ConcreteVolume", q.Volume, "VOLUME", "Concrete"), new("FormworkInner", q.Inner, "AREA", "Formwork, inner face"),
                    new("OuterArea", q.Outer, "AREA", "Outer face"), new("FormworkEnds", q.Ends, "AREA", "Formwork, ends"),
                    new("FormworkArea", q.Inner + q.Ends + (outerFormwork ? q.Outer : 0), "AREA", "Formwork"),
                ], Part: p.Key, PartName: p.Name, Material: material));
        }
        var section = built.Section;
        var main = new List<Quantity>
        {
            new("Length", built.Chain.Length, "LENGTH"),
            new("SectionArea", section.Outer.Area - section.Inner.Area, "AREA", "Lining section"),
            new("ClearArea", section.Inner.Area, "AREA", "Clear opening"),
            new("ConcreteVolume", concrete, "VOLUME", "Concrete"), new("GrossVolume", concrete + shell, "VOLUME"), new("NetVolume", concrete + shell, "VOLUME"),
            new("FormworkInner", inner, "AREA", "Formwork, inner face"), new("OuterArea", outer, "AREA", "Outer face"), new("FormworkEnds", ends, "AREA", "Formwork, ends"),
            new("FormworkArea", inner + ends + (outerFormwork ? outer : 0), "AREA", "Formwork"),
            new(built.Rings ? "Rings" : "Blocks", built.Joints.Count + 1, "COUNT", built.Rings ? "Rings" : "Blocks"),
        };
        if (section.Primary != null) { main.Add(new("ShellVolume", shell, "VOLUME", "Outer shell")); main.Add(new("ShellArea", shellArea, "AREA", "Outer shell, its outside")); }
        yield return new QuantitySet(SetName, main, Material: material);
        foreach (var s in sets) yield return s;
    }

    // ------------------------------------------------------------------ geometry, plan, section

    public static ElementGeometry Geometry(Entity e, IModelView m)
    {
        var built = Build(m, e);
        if (built.Parts.Count == 0) return new ElementGeometry([], Diagnostics: built.Notes.Count > 0 ? built.Notes : ["The tunnel lining could not be built."]);
        var material = e.Str("material");
        var shellMaterial = e.Str("primaryMaterial");
        var color = material != null ? Materials.ColorOf(m, material) : Color;
        var shellColor = shellMaterial != null ? Materials.ColorOf(m, shellMaterial) : PrimaryColor;
        var parts = built.Parts.Select(p => p.Layer == "primary" ? new GeometryPart(p.Name, shellColor, p.Solid, shellMaterial, p.Key) : new GeometryPart(p.Name, color, p.Solid, material, p.Key)).ToList();
        var c = built.Chain;
        return new ElementGeometry(parts, Plan(built), [c.PointAt(c.Start), c.PointAt(c.End)], built.Notes.Count > 0 ? built.Notes : null);
    }

    static readonly LineStyleSpec CentreLine = new(Pattern: "center");

    /// <summary>The plan: the lining's outside and its clear opening (hidden, it is under the ground), its axis, its ends and its block joints.</summary>
    static List<PlanItem> Plan(Built b)
    {
        var c = b.Chain;
        var o = b.Section.Outside.Box();
        var i = b.Section.Inner.Box();
        var plan = new List<PlanItem>();
        List<Vec2> Edge(double x)
        {
            var line = c.Offset(x);
            // The ends where they are: skewed, the line stops on their plane.
            line[0] = Flat(b.StartFrame, x);
            line[^1] = Flat(b.EndFrame, x);
            return line;
        }
        foreach (var x in new[] { o.MaxX, o.MinX, i.MaxX, i.MinX }) plan.Add(new PlanItem("dashed", false, Edge(x)));
        plan.Add(new PlanItem("symbol", false, c.Axis(), Style: CentreLine));
        plan.Add(new PlanItem("symbol", false, [Flat(b.StartFrame, o.MinX), Flat(b.StartFrame, o.MaxX)]));
        plan.Add(new PlanItem("symbol", false, [Flat(b.EndFrame, o.MinX), Flat(b.EndFrame, o.MaxX)]));
        foreach (var j in b.Joints)
        {
            var p = c.PointAt(j);
            var n = c.DirectionAt(j).Perp;
            plan.Add(new PlanItem("dashed", false, [p + n * o.MinX, p + n * o.MaxX]));
        }
        return plan;
    }

    static Vec2 Flat(Frame f, double x) => new(f.O.X + f.U.X * x, f.O.Y + f.U.Y * x);

    /// <summary>The section at a station (offset + left, elevation): each layer's ring in two halves (or one piece with its hole bridged), and how far it reaches either side.</summary>
    public static (IReadOnlyList<SectionPart> Parts, double Left, double Right) SectionAt(IModelView m, Entity e, double station)
    {
        var c = Corridors.ChainOf(e);
        var v = ValuesOf(m, e);
        var section = SectionOf(m, v);
        var z = GradeOf(m, e, c).Z(Math.Clamp(station, c.Start, c.End));
        var parts = new List<SectionPart>();
        void Layer(string key, LiningRing inner, LiningRing outer)
        {
            var po = outer.Points();
            var pi = inner.Points();
            List<Vec2> Up(IEnumerable<Vec2> pts) => [.. pts.Select(p => new Vec2(p.X, z + p.Y))];
            if (outer.Half is { } ho && inner.Half is { } hi)
            {
                var (ko, ki) = (HalfIndex(outer, ho), HalfIndex(inner, hi));
                var rightO = po.Take(ko + 1).ToList();
                var leftO = po.Skip(ko).Append(po[0]).ToList();
                var rightI = pi.Take(ki + 1).ToList();
                var leftI = pi.Skip(ki).Append(pi[0]).ToList();
                parts.Add(new SectionPart(key, Up(rightO.Concat(Enumerable.Reverse(rightI)))));
                parts.Add(new SectionPart(key, Up(leftO.Concat(Enumerable.Reverse(leftI)))));
            }
            else
            {
                // One piece: the outside, a bridge to the hole at their lowest points, the hole the other way round.
                var a = po.IndexOf(po.MinBy(p => p.Y));
                var bI = pi.IndexOf(pi.MinBy(p => p.Y));
                var ring = po.Skip(a).Concat(po.Take(a + 1)).ToList();
                var hole = pi.Skip(bI).Concat(pi.Take(bI + 1)).Reverse().ToList();
                parts.Add(new SectionPart(key, Up(ring.Concat(hole))));
            }
        }
        Layer("lining", section.Inner, section.Outer);
        if (section.Primary != null) Layer("primary", section.Outer, section.Primary);
        var box = section.Outside.Box();
        return (parts, box.MaxX, box.MinX);
    }

    /// <summary>The index in a ring's points where its left half starts (the first point of piece <paramref name="half"/>).</summary>
    static int HalfIndex(LiningRing r, int half) => r.Pieces.Take(half).Sum(p => p.Samples().Length - 1);

    // ------------------------------------------------------------------ an axis taken from another element

    /// <summary>The axis of an element a lining can be taken from: a road's, a path's, a wall's, a ditch's (their corridor's), else a drafted line's points.</summary>
    public static Chain? HostChain(Entity host)
    {
        if (Corridors.Is(host)) return Corridors.ChainOf(host);
        var pts = Terrains.Contour(host, "path") is { Count: >= 2 } pp ? pp : Args.StoredPoints(host.Attrs["points"]);
        return pts is { Count: >= 2 } ? new Chain(pts) : null;
    }

    /// <summary>
    /// The chain between two stations of another: its IFC 4.3 segments cut there (a line and an arc as they are; a clothoid by its
    /// curvature, which runs linearly along it; another transition as a clothoid between its end curvatures), or the polyline cut.
    /// </summary>
    public static (List<Vec2> Path, List<HorizontalSegment>? Segments) Clip(Chain c, double from, double to)
    {
        if (c.Alignment is { } al)
        {
            var prep = PreparedAlignment.Of(al);
            var o = new List<HorizontalSegment>();
            for (var i = 0; i < c.Segments; i++)
            {
                var (s0, s1) = (c.StationOf(i), c.StationOf(i + 1));
                var (a, b) = (Math.Max(s0, from), Math.Min(s1, to));
                if (b - a < 1e-6) continue;
                var seg = prep.Horizontal.Segments[i];
                var (la, lb) = (a - s0, b - s0);
                var pa = seg.At(la);
                var pb = seg.At(lb);
                static double R(double k) => Math.Abs(k) < 1e-15 ? 0 : 1 / k;
                o.Add(seg.Segment.Type switch
                {
                    HorizontalType.Line => new HorizontalSegment(HorizontalType.Line, pa.P, pa.Direction, 0, 0, lb - la),
                    HorizontalType.CircularArc => new HorizontalSegment(HorizontalType.CircularArc, pa.P, pa.Direction, R(pa.Curvature), R(pa.Curvature), lb - la),
                    _ => new HorizontalSegment(HorizontalType.Clothoid, pa.P, pa.Direction, R(pa.Curvature), R(pb.Curvature), lb - la),
                });
            }
            var path = o.Select(s => s.Start).Append(c.PointAt(to)).ToList();
            return (path, o);
        }
        var pts = new List<Vec2> { c.PointAt(from) };
        for (var i = 1; i + 1 < c.Path.Count; i++) if (c.StationOf(i) > from + 1e-6 && c.StationOf(i) < to - 1e-6) pts.Add(c.Path[i]);
        pts.Add(c.PointAt(to));
        return (pts, null);
    }

    /// <summary>A station on a chain: a number, or the station nearest a point [x, y].</summary>
    static double StationArg(Chain c, JsonNode? n, double fallback) =>
        n is JsonArray p && p.Count >= 2 && Json.Number(p[0]) is { } x && Json.Number(p[1]) is { } y ? c.Project(new Vec2(x, y)).Station : Json.Number(n) ?? fallback;

    /// <summary>The lining's axis and red line from the command: taken from another element between two stations, or drawn (points, curves, a level).</summary>
    static JsonObject AxisAttrs(IModelView m, JsonObject a, double fallbackZ, out JsonObject? source)
    {
        source = null;
        var corridor = CorridorSpec.Default.ToJson();
        corridor["interval"] = 20.0;
        var attrs = new JsonObject();
        if (a["axis"] is JsonObject ax)
        {
            var host = m.Get((string?)ax["id"] ?? "") ?? throw new CommandException("There is no such axis to follow.");
            var hc = HostChain(host) ?? throw new CommandException("The element picked has no axis to follow.");
            var s0 = Math.Clamp(StationArg(hc, ax["from"], hc.Start), hc.Start, hc.End);
            var s1 = Math.Clamp(StationArg(hc, ax["to"], hc.End), hc.Start, hc.End);
            if (s1 < s0) (s0, s1) = (s1, s0);
            if (s1 - s0 < 1) throw new CommandException("The lining runs one metre at least between its two stations.");
            var (path, segments) = Clip(hc, s0, s1);
            attrs["path"] = Terrains.ContourJson(path);
            corridor["stationStart"] = s0;
            if (segments != null) corridor["horizontal"] = AlignmentJson.Horizontal(segments);
            // The red line: the host's designed one as it is (its PVIs by station), else its own line under the axis fitted to PVIs.
            var hs = Corridors.Is(host) ? CorridorSpec.Of(host) : null;
            List<Pvi> pvis;
            if (hs is { Mode: "design" } && hs.Pvis.Count >= 2 && VerticalProfile.Problem(hs.Pvis) == null) pvis = [.. hs.Pvis];
            else if (hs != null)
            {
                var red = Corridors.RedLine(m, host, hc);
                pvis = VerticalProfile.FromGround(s0, s1, s => red(s), fallbackZ, 5, 0.02);
            }
            else
            {
                var z = Json.Number(a["level"]) ?? fallbackZ;
                pvis = [new Pvi(s0, z), new Pvi(s1, z)];
            }
            corridor["profile"] = new JsonObject { ["mode"] = "design", ["ground"] = "proposed", ["pvis"] = new JsonArray([.. pvis.Select(p => (JsonNode)new JsonArray(p.Station, p.Elevation, p.Length))]) };
            source = new JsonObject { ["id"] = host.Id, ["from"] = s0, ["to"] = s1 };
        }
        else
        {
            var pts = Args.StoredPoints(a["points"] as JsonArray);
            if (pts is not { Count: >= 2 }) throw new CommandException("A lining needs its axis: two points at least, or an axis picked and two stations on it.");
            var (clean, _) = Chain.Clean(pts, null);
            if (clean.Count < 2) throw new CommandException("A lining needs its axis: two different points.");
            attrs["path"] = Terrains.ContourJson(clean);
            if (a["curves"] is JsonArray cv)
            {
                var curves = CorridorSpec.ReadCurves(cv);
                var (path, kept) = Chain.Clean(clean, curves);
                if (kept.Any(k => !k.IsCorner) && PiAlignment.Problem(PiAlignment.Of(path, kept)) is { } why) throw new CommandException(why);
                corridor["curves"] = CorridorSpec.CurvesJson(curves);
            }
            var c = new Chain(clean, CorridorSpec.ReadCurves(corridor["curves"]), 0);
            var z0 = Json.Number(a["level"]) ?? fallbackZ;
            var z1 = Json.Number(a["levelEnd"]) ?? z0;
            corridor["profile"] = new JsonObject { ["mode"] = "design", ["ground"] = "proposed", ["pvis"] = new JsonArray(new JsonArray(c.Start, z0, 0), new JsonArray(c.End, z1, 0)) };
        }
        attrs["corridor"] = corridor;
        return attrs;
    }

    // ------------------------------------------------------------------ commands

    public static void Register(IModuleRegistry r)
    {
        r.Catalog("tunnel", TunnelSections.Catalog());
        // A corridor kind: the corridor configurator's stations, red line and sections are the lining's too.
        CorridorKinds.All = [.. CorridorKinds.All.Where(k => k is not TunnelKind), new TunnelKind()];

        // A lining on an axis: taken between two stations of an element picked ({axis: {id, from, to}}: stations or points), or drawn ({points, curves, level}).
        r.Command("addTunnelLining", "Tunnel lining", (ctx, a) =>
        {
            var storey = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id;
            var fallback = storey != null && ctx.Model.Get(storey) is { } st ? st.Num("elevation") : 0.0;
            var attrs = AxisAttrs(ctx.Model, a, fallback, out var source);
            if (source != null) attrs["axisSource"] = source;
            var raw = (a["spec"] as JsonObject)?.DeepClone() as JsonObject ?? [];
            Apply(ctx, attrs, raw);
            foreach (var k in new[] { "skewStart", "skewEnd" }) if (Json.Number(a[k]) is { } sk) attrs[k] = Math.Clamp(sk, -60, 60);
            var count = ctx.Model.OfClass(Class).Count() + 1;
            var e = new Entity(ctx.NewId(), Class, ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Tunnel lining {count}", attrs, "USERDEFINED");
            ctx.Add(e);
            if (storey != null) Spatial.Contain(ctx, storey, e.Id);
            return new JsonObject { ["id"] = e.Id };
        });

        // The configurator again on a lining: its section, blocks and IFC rebuilt in place (its GlobalIds kept); its ends' skew; its axis taken again.
        r.Command("updateTunnelLining", "Change tunnel lining", (ctx, a) =>
        {
            var e = Spatial.Require(ctx.Model, (string?)a["id"], Class);
            var attrs = e.CloneAttrs();
            if (a["axis"] is not JsonObject && a["points"] is JsonArray moved)
            {
                // Its points moved (grips): the path drawn anew, its stations, red line and curves kept (an axis taken from another is drawn from now on).
                var pts = Chain.Clean(Args.StoredPoints(moved), null).Path;
                if (pts.Count < 2) throw new CommandException("A lining needs its axis: two different points.");
                attrs["path"] = Terrains.ContourJson(pts);
                if (attrs["corridor"] is JsonObject cor)
                {
                    cor.Remove("horizontal");
                    if (a["curves"] is JsonArray cv) cor["curves"] = CorridorSpec.CurvesJson(CorridorSpec.ReadCurves(cv));
                    else if (CorridorSpec.ReadCurves(cor["curves"]).Count != pts.Count) cor["curves"] = new JsonArray();
                }
                attrs.Remove("axisSource");
            }
            else if (a["axis"] is JsonObject)
            {
                var fallback = Corridors.Fallback(ctx.Model, e);
                var axis = AxisAttrs(ctx.Model, a, fallback, out var source);
                var keep = CorridorSpec.Of(e).ToJson();
                foreach (var (k, val) in axis) attrs[k] = val?.DeepClone();
                // The stations ticked and the sections chosen stay.
                if (attrs["corridor"] is JsonObject cor) { cor["interval"] = keep["interval"]?.DeepClone(); cor["sections"] = keep["sections"]?.DeepClone(); }
                if (source != null) attrs["axisSource"] = source; else attrs.Remove("axisSource");
            }
            var raw = (a["spec"] as JsonObject)?.DeepClone() as JsonObject ?? (e.Attrs[Key]?.DeepClone() as JsonObject) ?? [];
            Apply(ctx, attrs, raw);
            foreach (var k in new[] { "skewStart", "skewEnd" })
                if (a.ContainsKey(k)) { if (Json.Number(a[k]) is { } sk) attrs[k] = Math.Clamp(sk, -60, 60); else attrs.Remove(k); }
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // A portal at one end of a lining (S3's configurator, aligned with the axis): its face wall across the axis there, square to it or
        // skewed with the lining's end, sized from the lining (wider than it, its crown over the lining's crown), the lining's opening cut through it.
        r.Command("tunnelPortal", "Portal at the lining's end", (ctx, a) =>
        {
            var e = Spatial.Require(ctx.Model, (string?)a["id"], Class);
            var atEnd = (string?)a["end"] != "start";
            if (Json.Number(a["skew"]) is { } skew)
            {
                var attrs = e.CloneAttrs();
                attrs[atEnd ? "skewEnd" : "skewStart"] = Math.Clamp(skew, -60, 60);
                ctx.Replace(e = e.With(attrs: attrs));
            }
            var built = Build(ctx.Model, e);
            if (built.Parts.Count == 0) throw new CommandException("The lining could not be built: it has no end to stand a portal at.");
            var c = built.Chain;
            var f = atEnd ? built.EndFrame : built.StartFrame;
            var origin = new Vec2(f.O.X, f.O.Y);
            // The wings go out of the tunnel; the face wall lies in the end's plane (square to the axis, or turned with its skew).
            var t = c.DirectionAt(atEnd ? c.End : c.Start);
            var plane = new Vec2(f.U.X, f.U.Y);
            var normal = new Vec2(plane.Y, -plane.X).Normalized();
            var direction = (Vec2.Dot(normal, t) > 0 ? normal : -normal) * (atEnd ? 1 : -1);
            var box = built.Section.Outside.Box();
            var grade = f.O.Z;
            var width = (box.MaxX - box.MinX) * Math.Sqrt(f.U.X * f.U.X + f.U.Y * f.U.Y);
            var raw = new JsonObject
            {
                ["preset"] = "flared",
                ["faceLength"] = Math.Ceiling((width + 4) * 2) / 2,
                ["crown"] = Math.Ceiling((grade + box.MaxY + 1.0) * 10) / 10,
                ["footingLevel"] = Math.Floor((grade + box.MinY - 0.3) * 10) / 10,
                ["embankment"] = Math.Round(grade, 3),
            };
            if (a["spec"] is JsonObject given) foreach (var (k, val) in given) raw[k] = val?.DeepClone();
            var storey = Spatial.StoreyOf(ctx.Model, e.Id)?.Id ?? (string?)a["storeyId"];
            var wall = Portals.Add(ctx, raw, origin, direction, (string?)a["name"] ?? $"{e.Name} {(atEnd ? "end" : "start")} portal", storey,
                new JsonObject { ["tunnelOpening"] = new JsonObject { ["id"] = e.Id, ["end"] = atEnd ? "end" : "start" } });
            return new JsonObject { ["id"] = wall.Id };
        });

        // The configurator's numbers worked out (formulas and all), what does not hold, the section's rings and their areas.
        r.Query("civil.tunnelValues", (q, a) =>
        {
            var v = TunnelSections.Evaluate(a["spec"] as JsonObject ?? [], q.Model);
            var s = SectionOf(q.Model, v);
            JsonArray Ring(LiningRing ring) => new([.. ring.Points().Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 5), Math.Round(p.Y, 5)))]);
            return new JsonObject
            {
                ["values"] = new JsonObject(v.Numbers.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)Math.Round(kv.Value, 6)))),
                ["problems"] = new JsonArray([.. s.Problems.Select(p => (JsonNode)p)]),
                ["inner"] = Ring(s.Inner), ["outer"] = Ring(s.Outer), ["primary"] = s.Primary != null ? Ring(s.Primary) : null,
                ["sectionArea"] = Math.Round(s.Outer.Area - s.Inner.Area, 6), ["clearArea"] = Math.Round(s.Inner.Area, 6),
            };
        });
        r.Service<ITypePreview>(new TunnelPreview());
        r.Service<IViewAnnotations>(new TunnelMarks());
        r.Service<IIfcExactBodies>(new TunnelExactBodies());
    }

    /// <summary>The configurator's spec on the lining (to open again on it), its materials made sure of, its IFC mapping.</summary>
    static void Apply(ICommandContext ctx, JsonObject attrs, JsonObject raw)
    {
        var v = TunnelSections.Evaluate(raw, ctx.Model);
        attrs[Key] = raw;
        attrs["material"] = Materials.Ensure(ctx, v.Text("material", "Reinforced concrete"), Color).Id;
        attrs["primaryMaterial"] = Materials.Ensure(ctx, v.Text("primaryMaterial", "Shotcrete"), PrimaryColor).Id;
        attrs["objectType"] = "Tunnel lining";
        attrs["exportAs"] = AsAssembly(v) ? "IfcElementAssembly" : IfcClass.BuildingElementProxy;
    }

    // ------------------------------------------------------------------ the portal's opening

    /// <summary>
    /// What a portal standing at a lining's end loses (its <c>tunnelOpening</c>): the lining's outside behind the end's plane, into the
    /// tunnel, and its clear opening in front of it, out of the tunnel, each a prism along the axis there. None for any other wall.
    /// </summary>
    public static List<Solid> PortalCutters(IModelView m, Entity wall)
    {
        if (wall.Attrs["tunnelOpening"] is not JsonObject o || m.Get((string?)o["id"] ?? "") is not { Class: Class } lining) return [];
        var built = Build(m, lining, bodies: false);
        if (built.Blocks.Count == 0) return [];
        var atEnd = (string?)o["end"] != "start";
        var f = atEnd ? built.EndFrame : built.StartFrame;
        var c = built.Chain;
        var station = atEnd ? c.End : c.Start;
        var t = c.DirectionAt(station);
        var grade = (built.Grade.Z(Math.Min(c.End, station + 0.5)) - built.Grade.Z(Math.Max(c.Start, station - 0.5))) / (Math.Min(c.End, station + 0.5) - Math.Max(c.Start, station - 0.5));
        var along = new Vec3(t.X, t.Y, 0) + Ez * grade;
        var outward = atEnd ? along : along * -1;
        const double reach = 12;
        var cutters = new List<Solid>();
        if (Prism(built.Section.Outside.Points(), f, outward * -reach, outward * 0.001) is { } back) cutters.Add(back);
        if (Prism(built.Section.Inner.Points(), f, outward * -0.001, outward * reach) is { } front) cutters.Add(front);
        return cutters;
    }

    /// <summary>An outline at a frame drawn out between two shifts along the axis: a closed prism.</summary>
    static Solid? Prism(List<Vec2> ring, Frame f, Vec3 from, Vec3 to)
    {
        var verts = new List<Vec3>();
        foreach (var q in ring) verts.Add(f.Map(q) + from);
        foreach (var q in ring) verts.Add(f.Map(q) + to);
        var n = ring.Count;
        var faces = new List<FaceInput> { new([.. Enumerable.Range(0, n).Reverse()]), new([.. Enumerable.Range(n, n)]) };
        for (var i = 0; i < n; i++) { var j = (i + 1) % n; faces.Add(new FaceInput([i, j, n + j, n + i])); }
        var s = SolidBuilder.Make(verts, faces, "opening");
        if (Measure.SignedVolume(s) < 0) s = SolidBuilder.Flip(s);
        return s.Faces.Length > 0 ? s : null;
    }

    /// <summary>
    /// The lining's axis as a section follows it (its long section), from its first station: its curves drawn through by chords
    /// straying 2 cm at most (a developed section cuts the model once a chord; at a long section's scale 2 cm is not seen).
    /// </summary>
    public static FollowedPath FollowedPathOf(Entity e)
    {
        var c = Corridors.ChainOf(e);
        return new FollowedPath(c.Axis(0.02), c.Start);
    }
}

/// <summary>The lining as a corridor kind: its cross-section at a station and its layers' materials.</summary>
sealed class TunnelKind : ICorridorKind
{
    public string Kind => "tunnelLining";
    public bool Is(Entity e) => Tunnels.Is(e);
    public (IReadOnlyList<SectionPart> Parts, double Left, double Right) Section(IModelView m, Entity e, Chain c, double station) => Tunnels.SectionAt(m, e, station);
    public string? MaterialOf(Entity e, string key) => key == "primary" ? e.Str("primaryMaterial") : e.Str("material");
}

/// <summary>The lining's exact bodies for IFC (IfcAdvancedBrep) and STEP: each block's (and the whole lining's) on its surfaces.</summary>
sealed class TunnelExactBodies : IIfcExactBodies
{
    public ExactBody? ExactOf(IModelView model, IServices services, Entity element, Solid body)
    {
        if (!Tunnels.Is(element)) return null;
        var built = Tunnels.Of(body) ?? Tunnels.Build(model, element);
        var part = built.Parts.FirstOrDefault(p => ReferenceEquals(p.Solid, body)) ?? (built.Parts.Count == 1 ? built.Parts[0] : null);
        return part == null ? null : built.ExactOf(part);
    }

    public ExactBody? ExactOfPart(IModelView model, IServices services, Entity element, string key, Solid body)
    {
        if (!Tunnels.Is(element)) return null;
        var built = Tunnels.Of(body) ?? Tunnels.Build(model, element);
        return built.Parts.FirstOrDefault(p => p.Key == key) is { } part ? built.ExactOf(part) : null;
    }
}

/// <summary>In plans, a lining's stations along its axis: a tick across it at every interval and at its ends, with the station's label.</summary>
sealed class TunnelMarks : IViewAnnotations
{
    public IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.Kind != "plan") yield break;
        var storey = c.View.Str("storeyId");
        var text = Standards.ResolveText(c.Book, new JsonObject());
        foreach (var e in c.Model.OfClass(Tunnels.Class))
        {
            if (SitePlans.Of(c.View) == null && Spatial.StoreyOf(c.Model, e.Id)?.Id != storey) continue;
            var ch = Corridors.ChainOf(e);
            if (ch.Segments == 0) continue;
            var spec = CorridorSpec.Of(e);
            var v = Tunnels.ValuesOf(c.Model, e);
            var box = Tunnels.SectionOf(c.Model, v).Outside.Box();
            var mk = new ViewMark(e.Id);
            var stations = Corridors.Stations(ch, spec);
            foreach (var s in stations)
            {
                var p = ch.PointAt(s);
                var n = ch.DirectionAt(s).Perp;
                var (a, b) = (p + n * (box.MaxX + 0.5), p + n * (box.MaxX + 2.0));
                mk.Line(c.Map(a), c.Map(b), "STATIONS", Pens.Default.Seen);
                var angle = -Standards.ReadableDeg(-Math.Atan2(n.Y, n.X) * 180 / Math.PI);
                mk.Text(new PenText(c.Map(b + n * 0.3), Chain.Label(s), text.Height * 0.8, angle, "start", "middle", false, text.Font));
            }
            yield return mk;
        }
    }
}

/// <summary>The configurator's preview: a lining of its section on a straight axis 30 m long in a scratch project, cut across its middle.</summary>
sealed class TunnelPreview : ITypePreview
{
    public bool Handles(string typeClass) => typeClass == Tunnels.PreviewClass;

    public TypePreviewSample Build(TypePreviewScratch s, JsonObject type, JsonObject sample)
    {
        var spec = (JsonObject)type.DeepClone();
        var length = Math.Clamp(Json.Number(sample["length"]) ?? 30, 5, 200);
        var r = s.Run("civil.addTunnelLining", new JsonObject
        {
            ["spec"] = spec, ["storeyId"] = s.Storey,
            ["points"] = new JsonArray(new JsonArray(0, -length / 2), new JsonArray(0, length / 2)),
        });
        var id = (string?)r?["id"] ?? throw new CommandException("The lining could not be built.");
        var box = Tunnels.SectionOf(s.Model, Tunnels.ValuesOf(s.Model, s.Model.Get(id)!)).Outside.Box();
        var reach = Math.Max(Math.Abs(box.MinX), Math.Abs(box.MaxX)) + 1.5;
        // Across the axis (+y) at its middle, looking along it.
        return new TypePreviewSample([id], new Vec2(reach, 0), new Vec2(-reach, 0), 1.0);
    }
}
