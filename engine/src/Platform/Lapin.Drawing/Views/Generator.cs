using Lapin.Geometry;

namespace Lapin.Drawing.Views;

/// <summary>
/// A solid to draw: its element, IFC class, part key and material (an IfcMaterial id). <see cref="CutInPlan"/>: a part of a
/// class plans draw by its symbol (<see cref="Generator.SymbolicInPlan"/>) that has none (a door from an IFC file), cut as any other.
/// </summary>
public sealed record DrawPart(Solid Solid, string Element, string Class, string? Key = null, string? Material = null, bool CutInPlan = false)
{
    /// <summary>A part an elevation or a section draws by its element's silhouettes (<see cref="Silhouette"/>) instead of its edges: it still sizes the view, never drawn.</summary>
    public bool BySilhouette { get; init; }
    /// <summary>A curve made of flats (a member's fillets at LOD 400): no line between two flats that both face the eye and turn gently, as the 3D's edges (<see cref="Tessellator.CreaseDeg"/>).</summary>
    public bool Smooth { get; init; }
}

/// <summary>One stroke of a silhouette, in its own (across, up) metres from the figure's foot: a closed contour or an open line.</summary>
public sealed record SilhouetteStroke(IReadOnlyList<Vec2> Points, bool Closed = true);

/// <summary>
/// A figure as an elevation or a section draws it (decision 341): a flat outline standing at <see cref="At"/> (its foot),
/// not its body's edges. <see cref="Facing"/>: always turned to the viewer (a person, a tree: <see cref="Side"/> alone); else
/// <see cref="Side"/> (along <see cref="Axis"/>, a car's length) when the view sees it more from the side, <see cref="Front"/>
/// (across) otherwise, mirrored as it faces. <see cref="Mode"/>: the entourage mode it belongs to (outline, abstract).
/// </summary>
public sealed record Silhouette(Vec3 At, IReadOnlyList<SilhouetteStroke> Side, IReadOnlyList<SilhouetteStroke>? Front = null, Vec2? Axis = null, string? Mode = null)
{
    public bool Facing => Front == null || Axis == null;
}

/// <summary>A silhouette to draw for an element: its fill (a colour, or none), its lines' own style (a thin pen).</summary>
public sealed record DrawSilhouette(Silhouette Shape, string Element, string Class, string? Fill = null, LineStyleSpec? Own = null);

/// <summary>A line an element draws in plan by itself (a door's swing, a roof's ridge): its role (projection, overhead…), and the style its file gave it.</summary>
public sealed record DrawSymbol(IReadOnlyList<Vec2> Points, bool Closed, string Element, string Class, string Role, LineStyleSpec? Own = null)
{
    /// <summary>A line in space (a steel member's axis at LOD 100, decision 405): drawn in every kind of view, mapped by its frame, where it lies within its depth.</summary>
    public IReadOnlyList<Vec3>? Points3 { get; init; }
}

/// <summary>
/// A drawn line: its role (cut, projection, hidden, overhead, beyond), its element and class, a style a line edit gave it,
/// and the style the element's own 2D gave it (an IFC file's colour and pattern), under every other.
/// </summary>
public sealed record DLine(Vec2 A, Vec2 B, string Role, string Element, string Class, LineStyleSpec? Style = null, LineStyleSpec? Own = null);

/// <summary>An area an element's own 2D fills (an IFC annotation fill area): even-odd loops, its colour.</summary>
public sealed record DFill(IReadOnlyList<Vec2[]> Loops, string Element, string Class, string? Color);

/// <summary>A text of an element's own 2D: where (view m), its height (m on the model; null: the drawing's default), angle (°), colour.</summary>
public sealed record DText(Vec2 At, string Text, double? Height, double Angle, string Element, string Class, string? Color);

/// <summary>What a cut takes out of one part: closed loops, even-odd, with the part's material.</summary>
public sealed record DPolygon(IReadOnlyList<Vec2[]> Loops, string Element, string Class, string? Key, string? Material);

/// <summary>A view's drawing in its own coordinates (metres): the cut areas and the lines; the fills and texts of elements' own 2D.</summary>
public sealed record ViewDrawing(List<DPolygon> Cuts, List<DLine> Lines, List<DFill>? Fills = null, List<DText>? Texts = null)
{
    /// <summary>The drawing moved <paramref name="dx"/> along x (a piece of a developed section laid at its station).</summary>
    public ViewDrawing Shifted(double dx)
    {
        if (Math.Abs(dx) < 1e-12) return this;
        var d = new Vec2(dx, 0);
        return new ViewDrawing(
            Cuts.Select(c => c with { Loops = c.Loops.Select(l => l.Select(p => p + d).ToArray()).ToList() }).ToList(),
            Lines.Select(l => l with { A = l.A + d, B = l.B + d }).ToList(),
            Fills?.Select(f => f with { Loops = f.Loops.Select(l => l.Select(p => p + d).ToArray()).ToList() }).ToList(),
            Texts?.Select(t => t with { At = t.At + d }).ToList());
    }

    /// <summary>Several pieces as one drawing.</summary>
    public static ViewDrawing Join(IReadOnlyList<ViewDrawing> pieces)
    {
        if (pieces.Count == 1) return pieces[0];
        var fills = pieces.Any(p => p.Fills != null) ? pieces.SelectMany(p => p.Fills ?? []).ToList() : null;
        var texts = pieces.Any(p => p.Texts != null) ? pieces.SelectMany(p => p.Texts ?? []).ToList() : null;
        return new ViewDrawing(pieces.SelectMany(p => p.Cuts).ToList(), pieces.SelectMany(p => p.Lines).ToList(), fills, texts);
    }

    public (Vec2 Min, Vec2 Max)? Bounds
    {
        get
        {
            var pts = Cuts.SelectMany(c => c.Loops.SelectMany(l => l)).Concat(Lines.SelectMany(l => new[] { l.A, l.B }))
                .Concat((Fills ?? []).SelectMany(f => f.Loops.SelectMany(l => l))).Concat((Texts ?? []).Select(t => t.At)).ToList();
            return pts.Count == 0 ? null : (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y)));
        }
    }
}

/// <summary>
/// How a view sees: its frame (plans look down from the cut), whether it cuts,
/// how deep it projects (<see cref="Below"/>), from where lines are beyond, and
/// for a plan how high overhead elements are drawn (<see cref="Above"/>).
/// </summary>
public sealed record ViewSpec(string Kind, ViewFrame Frame, bool Cut, double Below, double? BeyondFrom = null, double Above = 0, bool Hidden = true, bool Up = false)
{
    /// <summary>How far the drawing reaches sideways (along the frame's Right, from its origin); null: without end. One piece of a section developed along a polyline sees only between its ends.</summary>
    public (double Min, double Max)? Span { get; init; }
    /// <summary>How far the drawing reaches up and down (along the frame's Up, from its origin); null: without end. A section across an element sees only its window.</summary>
    public (double Min, double Max)? Rise { get; init; }

    /// <summary>
    /// A plan cut at <paramref name="cutZ"/>, looking down; or, <paramref name="up"/>, looking up
    /// (a reflected ceiling plan: what is above the cut up to the range's top, seen from below,
    /// drawn the way the plan lies so the two lay over each other).
    /// </summary>
    public static ViewSpec Plan(double cutZ, RangeBands bands, bool hidden = true, bool up = false) =>
        new("plan", new ViewFrame(new Vec3(0, 0, cutZ), new Vec3(1, 0, 0), new Vec3(0, 1, 0), -Vec3.UnitZ), true, bands.Below, bands.BeyondFrom, bands.Above, hidden, up);

    /// <summary>
    /// A section along a line in plan, looking to its left-hand side (as drawn from <paramref name="a"/> to <paramref name="b"/>), as deep as asked;
    /// <paramref name="spanned"/>: seeing only between its two ends (a piece of a developed section).
    /// </summary>
    public static ViewSpec Section(Vec2 a, Vec2 b, double depth, bool hidden = true, bool spanned = false)
    {
        var along = (b - a).Normalized();
        var look = along.Perp; // left of the line
        var frame = new ViewFrame(new Vec3(a.X, a.Y, 0), new Vec3(along.X, along.Y, 0), Vec3.UnitZ, new Vec3(look.X, look.Y, 0));
        return new ViewSpec("section", frame, true, depth, Hidden: hidden) { Span = spanned ? (0, (b - a).Length) : null };
    }

    /// <summary>
    /// An elevation looking along <paramref name="dir"/> (a plan direction) from outside the model: nothing cut.
    /// <paramref name="originX"/>: where its x is measured from (along its Right, from the model's origin), so that what is
    /// drawn on it stays put when the model grows; without, from the model's middle as it is now.
    /// </summary>
    public static ViewSpec Elevation(Vec2 dir, (Vec3 Min, Vec3 Max) bounds, bool hidden = false, double? originX = null)
    {
        var d = dir.Normalized();
        var right = new Vec2(d.Y, -d.X); // looking along d, right is d turned clockwise
        // Origin outside the bounds, against the view, on the line through the world's origin along the view: the
        // drawing's x is the model's own (along right), whatever the model's extent, so what is drafted on an elevation
        // stays where it was drawn when the model grows (Studio's sketches on it among them).
        var c = (bounds.Min + bounds.Max) * 0.5;
        var r = (bounds.Max - bounds.Min).Length;
        var o = new Vec3(c.X - d.X * r, c.Y - d.Y * r, 0);
        if (originX is { } ox) { var rv = new Vec3(right.X, right.Y, 0); o += rv * (ox - Vec3.Dot(o, rv)); }
        return new("elevation", new ViewFrame(o, new Vec3(right.X, right.Y, 0), Vec3.UnitZ, new Vec3(d.X, d.Y, 0)), false, double.PositiveInfinity, Hidden: hidden);
    }
}

/// <summary>
/// The 2D drawing of a view (the old Lapin's drawing generator, on BubbleBIM
/// Pro's exact hidden-line removal): the cut areas of every part, their
/// outlines as cut lines, what is seen beyond the cut, the hidden lines, the
/// overhead band of a plan and what lies beyond its range's bottom. In a plan
/// doors and windows draw their symbols, and roofs their outline overhead.
/// </summary>
public static class Generator
{
    /// <summary>A line in space whose role the drawing works out (a steel member's axis at LOD 100, decision 405).</summary>
    public const string StickRole = "stick";

    /// <summary>Classes drawn in plan by their own symbols instead of their cut.</summary>
    public static readonly HashSet<string> SymbolicInPlan = ["IfcDoor", "IfcWindow", "IfcRoof", "IfcGeographicElement", "IfcStair", "IfcRamp", "IfcPavement", "LapinPlanting", "LapinEntourage", "IfcFurniture", "IfcShadingDevice", "IfcTransportElement", "LapinTrack", "LapinPlatform", "LapinTurnout", "LapinCrossing", "LapinLevelCrossing"];

    /// <summary>
    /// Classes a plan draws dashed when they lie wholly under its range (decision 435, a tester's report #115): foundations
    /// under the floor are drawn in the plan of their storey as hidden outlines, as a plan of a building shows what is under it.
    /// A foundation plan, whose range reaches them, cuts or sees them as any element.
    /// </summary>
    public static readonly HashSet<string> HiddenBelowInPlan = ["IfcFooting", "IfcPile"];

    /// <param name="seamKey">
    /// What a part's surface joins by (<see cref="MaterialDrawing.SeamKey"/>): where it goes on in one plane into a part of the
    /// same key, the edge between them is no line. Default: parts of one material (or, without one, of one class) join.
    /// </param>
    public static ViewDrawing Draw(IReadOnlyList<DrawPart> parts, ViewSpec v, IReadOnlyList<DrawSymbol>? symbols = null, Func<DrawPart, string?>? seamKey = null, IReadOnlyList<DrawSilhouette>? silhouettes = null)
    {
        seamKey ??= p => p.Material ?? " class:" + p.Class;
        if (v.Kind == "plan" && v.Up) return DrawUp(parts, v, seamKey);
        var cuts = new List<DPolygon>();
        var lines = new List<DLine>();
        var plan = v.Kind == "plan";
        var cutZ = v.Frame.Origin.Z;
        // Each part's height range, once.
        var zs = new Dictionary<Solid, (double Min, double Max)>(ReferenceEqualityComparer.Instance);
        (double Min, double Max) Z(Solid s)
        {
            if (zs.TryGetValue(s, out var z)) return z;
            if (s.Vertices.Length == 0) return zs[s] = (0, 0);
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            foreach (var p in s.Vertices) { lo = Math.Min(lo, p.Z); hi = Math.Max(hi, p.Z); }
            return zs[s] = (lo, hi);
        }

        // The primary set: what the cut and the projection below it see.
        var primary = parts.Where(p => plan ? p.CutInPlan || !SymbolicInPlan.Contains(p.Class) : !p.BySilhouette).Where(p =>
        {
            if (!plan) return true;
            var (z0, z1) = Z(p.Solid);
            return z0 < cutZ - 1e-9 && z1 > cutZ - v.Below - 1e-9;
        }).ToList();
        var (seen, areas, behind) = HiddenLines.Draw(primary.Select((p, i) => (p.Solid, i)).ToList(), v.Frame, v.Cut, v.Below, v.Hidden, primary.Select(seamKey).ToList(), v.Span, v.Rise, primary.Select(p => p.Smooth).ToList());
        foreach (var a in areas)
        {
            var p = primary[a.Owner];
            cuts.Add(new DPolygon(a.Loops, p.Element, p.Class, p.Key, p.Material));
            foreach (var loop in a.Loops)
                for (var i = 0; i < loop.Length; i++) lines.Add(new DLine(loop[i], loop[(i + 1) % loop.Length], LineRole.Cut, p.Element, p.Class));
        }
        // A cut line another part's corner stands on is split there: where a core running through meets the end of one
        // stopping at it, only the shared stretch is inside the material, and only that stretch may go (MaterialDrawing).
        lines = SplitAtCorners(lines, cuts);
        foreach (var s in seen)
        {
            var p = primary[s.Owner];
            var beyond = plan && v.BeyondFrom is { } bf && Z(p.Solid).Max < cutZ - bf - 1e-4;
            lines.Add(new DLine(s.A, s.B, beyond ? LineRole.Beyond : LineRole.Projection, p.Element, p.Class));
        }
        foreach (var s in behind) { var p = primary[s.Owner]; lines.Add(new DLine(s.A, s.B, LineRole.Hidden, p.Element, p.Class)); }

        if (plan && v.Above > 0)
        {
            // Overhead: what lies wholly above the cut, below Top (beams, a slab's edge over a void), seen from below as dashed lines.
            var over = parts.Where(p => p.CutInPlan || !SymbolicInPlan.Contains(p.Class)).Where(p => { var (z0, _) = Z(p.Solid); return z0 > cutZ + 1e-6 && z0 < cutZ + v.Above - 1e-9; }).ToList();
            if (over.Count > 0)
            {
                var up = new ViewFrame(new Vec3(0, 0, cutZ), new Vec3(1, 0, 0), new Vec3(0, -1, 0), Vec3.UnitZ);
                var (oLines, _, _) = HiddenLines.Draw(over.Select((p, i) => (p.Solid, i)).ToList(), up, false, v.Above, keys: over.Select(seamKey).ToList(), smooth: over.Select(p => p.Smooth).ToList());
                foreach (var s in oLines)
                {
                    var p = over[s.Owner];
                    lines.Add(new DLine(new Vec2(s.A.X, -s.A.Y), new Vec2(s.B.X, -s.B.Y), LineRole.Overhead, p.Element, p.Class));
                }
            }
        }
        // Stray lines: projection longer than the drawing could hold (docs/transfer.md). Symbols are drawn, never stray.
        (Vec2, Vec2)? Box(IEnumerable<Vec2> ps) { var l = ps.ToList(); return l.Count == 0 ? null : (new Vec2(l.Min(p => p.X), l.Min(p => p.Y)), new Vec2(l.Max(p => p.X), l.Max(p => p.Y))); }
        var cutBox = Box(cuts.SelectMany(c => c.Loops).SelectMany(l => l));
        var modelBox = Box(primary.SelectMany(p => p.Solid.Vertices).Select(v.Frame.Map));
        if (modelBox is { } mb && v.Span is { } span) modelBox = (new Vec2(Math.Max(mb.Item1.X, span.Min), mb.Item1.Y), new Vec2(Math.Min(mb.Item2.X, span.Max), mb.Item2.Y));
        lines = DropStray(lines, cutBox, modelBox);
        if (plan && v.Hidden)
        {
            var low = parts.Where(p => HiddenBelowInPlan.Contains(p.Class) && Z(p.Solid).Max <= cutZ - v.Below + 1e-9).ToList();
            if (low.Count > 0)
            {
                var (edges, _, _) = HiddenLines.Draw(low.Select((p, i) => (p.Solid, i)).ToList(), v.Frame, false, ViewRanges.Unlimited, keys: low.Select(seamKey).ToList(), smooth: low.Select(p => p.Smooth).ToList());
                foreach (var e in edges) { var p = low[e.Owner]; lines.Add(new DLine(e.A, e.B, LineRole.Hidden, p.Element, p.Class)); }
            }
        }
        if (symbols != null)
            foreach (var sym in symbols.Where(s => s.Points3 != null))
            {
                // In front of the view and within its depth (a plan's: under its cut, above its range's bottom).
                var pts = sym.Points3!;
                if (plan ? pts.All(q => q.Z > cutZ + v.Above + 1e-9) || pts.All(q => q.Z < cutZ - v.Below - 1e-9)
                    : pts.All(q => Vec3.Dot(q - v.Frame.Origin, v.Frame.Dir) < -1e-6) || pts.All(q => Vec3.Dot(q - v.Frame.Origin, v.Frame.Dir) > v.Below + 1e-6)) continue;
                var mapped = pts.Select(v.Frame.Map).ToList();
                var role = sym.Role;
                if (role == StickRole)
                {
                    // Cut where the plane runs through it, overhead above a plan's cut, seen otherwise.
                    var d = pts.Select(q => Vec3.Dot(q - v.Frame.Origin, v.Frame.Dir)).ToList();
                    role = v.Cut && d.Min() <= 1e-9 && d.Max() >= -1e-9 ? LineRole.Cut : plan && pts.Min(q => q.Z) > cutZ ? LineRole.Overhead : LineRole.Projection;
                }
                for (var i = 0; i + 1 < mapped.Count; i++)
                    lines.Add(new DLine(mapped[i], mapped[i + 1], role, sym.Element, sym.Class, Own: sym.Own));
            }
        if (plan && symbols != null)
            foreach (var sym in symbols.Where(s => s.Points3 == null))
            {
                var n = sym.Points.Count;
                for (var i = 0; i + 1 < n + (sym.Closed ? 1 : 0); i++)
                    lines.Add(new DLine(sym.Points[i], sym.Points[(i + 1) % n], sym.Role, sym.Element, sym.Class, Own: sym.Own));
            }
        List<DFill>? fills = null;
        if (!plan && silhouettes is { Count: > 0 }) DrawSilhouettes(silhouettes, v, lines, ref fills);
        return new ViewDrawing(cuts, lines, fills);
    }

    /// <summary>
    /// The silhouettes an elevation or a section sees (decision 341): each figure in front of the view's depth (a section's
    /// beyond its cut), within its span, laid flat at its foot's place as seen lines, with its fill under them.
    /// </summary>
    static void DrawSilhouettes(IReadOnlyList<DrawSilhouette> silhouettes, ViewSpec v, List<DLine> lines, ref List<DFill>? fills)
    {
        var f = v.Frame;
        foreach (var s in silhouettes)
        {
            var depth = Vec3.Dot(s.Shape.At - f.Origin, f.Dir);
            if (v.Cut ? depth < 0 || depth > v.Below : depth > v.Below) continue;
            var c = f.Map(s.Shape.At);
            if (v.Span is { } span && (c.X < span.Min || c.X > span.Max)) continue;
            if (v.Rise is { } rise && (c.Y > rise.Max)) continue;
            var strokes = s.Shape.Side;
            var sign = 1.0;
            if (!s.Shape.Facing)
            {
                var a = s.Shape.Axis!.Value;
                var along = a.X * f.Right.X + a.Y * f.Right.Y;
                var across = -a.Y * f.Right.X + a.X * f.Right.Y;
                if (Math.Abs(along) >= Math.Abs(across)) sign = along < 0 ? -1 : 1;
                else { strokes = s.Shape.Front!; sign = across < 0 ? -1 : 1; }
            }
            foreach (var st in strokes)
            {
                var n = st.Points.Count;
                if (n < 2) continue;
                var pts = new Vec2[n];
                for (var i = 0; i < n; i++) pts[i] = new Vec2(c.X + sign * st.Points[i].X, c.Y + st.Points[i].Y);
                for (var i = 0; i + 1 < n + (st.Closed ? 1 : 0); i++) lines.Add(new DLine(pts[i], pts[(i + 1) % n], LineRole.Projection, s.Element, s.Class, Own: s.Own));
                if (st.Closed && n >= 3 && s.Fill != null) (fills ??= []).Add(new DFill([pts], s.Element, s.Class, s.Fill));
            }
        }
    }

    /// <summary>
    /// A plan looking up: the cut at the plan's height seen from below, and
    /// what lies above it up to the range's top (soffits, beams, formwork) as
    /// seen lines. Seen from below the drawing would come out mirrored; it is
    /// turned back, so it lies as the plan does (the reflected ceiling plan's
    /// convention). No symbols: doors and windows are a plan's.
    /// </summary>
    static ViewDrawing DrawUp(IReadOnlyList<DrawPart> parts, ViewSpec v, Func<DrawPart, string?> seamKey)
    {
        var cutZ = v.Frame.Origin.Z;
        var reach = v.Above > 0 ? v.Above : ViewRanges.Unlimited;
        var primary = parts.Where(p => p.CutInPlan || !SymbolicInPlan.Contains(p.Class)).Where(p =>
        {
            if (p.Solid.Vertices.Length == 0) return false;
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            foreach (var q in p.Solid.Vertices) { lo = Math.Min(lo, q.Z); hi = Math.Max(hi, q.Z); }
            return hi > cutZ + 1e-9 && lo < cutZ + reach - 1e-9;
        }).ToList();
        var up = new ViewFrame(new Vec3(0, 0, cutZ), new Vec3(1, 0, 0), new Vec3(0, -1, 0), Vec3.UnitZ);
        var (seen, areas, behind) = HiddenLines.Draw(primary.Select((p, i) => (p.Solid, i)).ToList(), up, true, reach, v.Hidden, primary.Select(seamKey).ToList(), smooth: primary.Select(p => p.Smooth).ToList());
        static Vec2 F(Vec2 p) => new(p.X, -p.Y);
        var cuts = new List<DPolygon>();
        var lines = new List<DLine>();
        foreach (var a in areas)
        {
            var p = primary[a.Owner];
            var loops = a.Loops.Select(l => l.Select(F).Reverse().ToArray()).ToList();
            cuts.Add(new DPolygon(loops, p.Element, p.Class, p.Key, p.Material));
            foreach (var loop in loops)
                for (var i = 0; i < loop.Length; i++) lines.Add(new DLine(loop[i], loop[(i + 1) % loop.Length], LineRole.Cut, p.Element, p.Class));
        }
        lines = SplitAtCorners(lines, cuts);
        foreach (var s in seen) { var p = primary[s.Owner]; lines.Add(new DLine(F(s.A), F(s.B), LineRole.Projection, p.Element, p.Class)); }
        foreach (var s in behind) { var p = primary[s.Owner]; lines.Add(new DLine(F(s.A), F(s.B), LineRole.Hidden, p.Element, p.Class)); }
        return new ViewDrawing(cuts, lines);
    }

    /// <summary>
    /// The cut lines split at every corner of a cut area that stands on them
    /// (a T junction): a long side shared only in part with a neighbour becomes
    /// the stretch it shares and the stretches it does not, so the shared one
    /// can be left out where the material goes on. Lines of other roles are
    /// kept as they are.
    /// </summary>
    public static List<DLine> SplitAtCorners(List<DLine> lines, IReadOnlyList<DPolygon> cuts, double tol = 1e-6)
    {
        const double Cell = 0.5;
        var grid = new Dictionary<(long, long), List<Vec2>>();
        static (long, long) Key(Vec2 p) => ((long)Math.Floor(p.X / Cell), (long)Math.Floor(p.Y / Cell));
        foreach (var c in cuts)
            foreach (var loop in c.Loops)
                foreach (var v in loop)
                {
                    var k = Key(v);
                    (grid.TryGetValue(k, out var l) ? l : grid[k] = []).Add(v);
                }
        if (grid.Count == 0) return lines;
        var result = new List<DLine>(lines.Count);
        var at = new List<double>();
        foreach (var line in lines)
        {
            var d = line.B - line.A;
            var len2 = d.X * d.X + d.Y * d.Y;
            if (line.Role != LineRole.Cut || len2 < tol * tol) { result.Add(line); continue; }
            at.Clear();
            var (lo, hi) = (Key(new Vec2(Math.Min(line.A.X, line.B.X) - tol, Math.Min(line.A.Y, line.B.Y) - tol)), Key(new Vec2(Math.Max(line.A.X, line.B.X) + tol, Math.Max(line.A.Y, line.B.Y) + tol)));
            var len = Math.Sqrt(len2);
            // Only the cells along the line (within tol of it), column by column: its whole bounding box was some
            // 4·10⁶ cells for a kilometre's diagonal (a terrain's cut line in a site section).
            for (var x = lo.Item1; x <= hi.Item1; x++)
            {
                double ya, yb;
                if (Math.Abs(d.X) < 1e-12) (ya, yb) = (Math.Min(line.A.Y, line.B.Y), Math.Max(line.A.Y, line.B.Y));
                else
                {
                    var t0 = Math.Clamp((x * Cell - tol - line.A.X) / d.X, 0, 1);
                    var t1 = Math.Clamp(((x + 1) * Cell + tol - line.A.X) / d.X, 0, 1);
                    (ya, yb) = (Math.Min(line.A.Y + d.Y * t0, line.A.Y + d.Y * t1), Math.Max(line.A.Y + d.Y * t0, line.A.Y + d.Y * t1));
                }
                var (y0, y1) = (Math.Max(lo.Item2, (long)Math.Floor((ya - tol) / Cell)), Math.Min(hi.Item2, (long)Math.Floor((yb + tol) / Cell)));
                for (var y = y0; y <= y1; y++)
                {
                    if (!grid.TryGetValue((x, y), out var pts)) continue;
                    foreach (var v in pts)
                    {
                        var w = v - line.A;
                        var t = (w.X * d.X + w.Y * d.Y) / len2;
                        if (t * len <= tol || (1 - t) * len <= tol) continue;
                        var off = Math.Abs(w.X * d.Y - w.Y * d.X) / len;
                        if (off <= tol) at.Add(t);
                    }
                }
            }
            if (at.Count == 0) { result.Add(line); continue; }
            at.Sort();
            var from = 0.0;
            foreach (var t in at)
            {
                if ((t - from) * len <= tol) continue;
                result.Add(line with { A = line.A + d * from, B = line.A + d * t });
                from = t;
            }
            result.Add(line with { A = line.A + d * from });
        }
        return result;
    }

    /// <summary>
    /// Projected lines (seen, beyond, hidden, overhead; not cuts nor symbols such
    /// as a terrain's contours), longer than 1.5 × the larger diagonal of the cut
    /// area and of the projected model, are left out: numerical strays of the
    /// projection. Without either extent, all stay.
    /// </summary>
    public static List<DLine> DropStray(List<DLine> lines, (Vec2 Min, Vec2 Max)? cut, (Vec2 Min, Vec2 Max)? model)
    {
        var diag = Math.Max(cut is { } c ? (c.Max - c.Min).Length : 0, model is { } m ? (m.Max - m.Min).Length : 0);
        if (!(diag > 1e-9)) return lines;
        var limit = 1.5 * diag;
        return lines.Where(l => l.Role is not (LineRole.Projection or LineRole.Beyond or LineRole.Hidden or LineRole.Overhead) || (l.B - l.A).Length <= limit).ToList();
    }
}
