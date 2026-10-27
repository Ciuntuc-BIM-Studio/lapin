using Lapin.Geometry;

namespace Lapin.Drawing.Views;

/// <summary>
/// A solid to draw: its element, IFC class, part key and material (an IfcMaterial id). <see cref="CutInPlan"/>: a part of a
/// class plans draw by its symbol (<see cref="Generator.SymbolicInPlan"/>) that has none (a door from an IFC file), cut as any other.
/// </summary>
public sealed record DrawPart(Solid Solid, string Element, string Class, string? Key = null, string? Material = null, bool CutInPlan = false);

/// <summary>A line an element draws in plan by itself (a door's swing, a roof's ridge): its role (projection, overhead…), and the style its file gave it.</summary>
public sealed record DrawSymbol(IReadOnlyList<Vec2> Points, bool Closed, string Element, string Class, string Role, LineStyleSpec? Own = null);

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
public sealed record ViewSpec(string Kind, ViewFrame Frame, bool Cut, double Below, double? BeyondFrom = null, double Above = 0, bool Hidden = true)
{
    /// <summary>A plan cut at <paramref name="cutZ"/>, looking down.</summary>
    public static ViewSpec Plan(double cutZ, RangeBands bands, bool hidden = true) =>
        new("plan", new ViewFrame(new Vec3(0, 0, cutZ), new Vec3(1, 0, 0), new Vec3(0, 1, 0), -Vec3.UnitZ), true, bands.Below, bands.BeyondFrom, bands.Above, hidden);

    /// <summary>A section along a line in plan, looking to its left-hand side (as drawn from <paramref name="a"/> to <paramref name="b"/>), as deep as asked.</summary>
    public static ViewSpec Section(Vec2 a, Vec2 b, double depth, bool hidden = true)
    {
        var along = (b - a).Normalized();
        var look = along.Perp; // left of the line
        var frame = new ViewFrame(new Vec3(a.X, a.Y, 0), new Vec3(along.X, along.Y, 0), Vec3.UnitZ, new Vec3(look.X, look.Y, 0));
        return new("section", frame, true, depth, Hidden: hidden);
    }

    /// <summary>An elevation looking along <paramref name="dir"/> (a plan direction) from outside the model: nothing cut.</summary>
    public static ViewSpec Elevation(Vec2 dir, (Vec3 Min, Vec3 Max) bounds, bool hidden = false)
    {
        var d = dir.Normalized();
        var right = new Vec2(d.Y, -d.X); // looking along d, right is d turned clockwise
        // Origin outside the bounds, against the view.
        var c = (bounds.Min + bounds.Max) * 0.5;
        var r = (bounds.Max - bounds.Min).Length;
        var o = new Vec3(c.X - d.X * r, c.Y - d.Y * r, 0);
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
    /// <summary>Classes drawn in plan by their own symbols instead of their cut.</summary>
    public static readonly HashSet<string> SymbolicInPlan = ["IfcDoor", "IfcWindow", "IfcRoof", "IfcGeographicElement", "IfcStair", "IfcPavement", "LapinPlanting"];

    /// <param name="seamKey">
    /// What a part's surface joins by (<see cref="MaterialDrawing.SeamKey"/>): where it goes on in one plane into a part of the
    /// same key, the edge between them is no line. Default: parts of one material (or, without one, of one class) join.
    /// </param>
    public static ViewDrawing Draw(IReadOnlyList<DrawPart> parts, ViewSpec v, IReadOnlyList<DrawSymbol>? symbols = null, Func<DrawPart, string?>? seamKey = null)
    {
        seamKey ??= p => p.Material ?? " class:" + p.Class;
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
        var primary = parts.Where(p => !plan || p.CutInPlan || !SymbolicInPlan.Contains(p.Class)).Where(p =>
        {
            if (!plan) return true;
            var (z0, z1) = Z(p.Solid);
            return z0 < cutZ - 1e-9 && z1 > cutZ - v.Below - 1e-9;
        }).ToList();
        var (seen, areas, behind) = HiddenLines.Draw(primary.Select((p, i) => (p.Solid, i)).ToList(), v.Frame, v.Cut, v.Below, v.Hidden, primary.Select(seamKey).ToList());
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
                var (oLines, _, _) = HiddenLines.Draw(over.Select((p, i) => (p.Solid, i)).ToList(), up, false, v.Above, keys: over.Select(seamKey).ToList());
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
        lines = DropStray(lines, cutBox, modelBox);
        if (plan && symbols != null)
            foreach (var sym in symbols)
            {
                var n = sym.Points.Count;
                for (var i = 0; i + 1 < n + (sym.Closed ? 1 : 0); i++)
                    lines.Add(new DLine(sym.Points[i], sym.Points[(i + 1) % n], sym.Role, sym.Element, sym.Class, Own: sym.Own));
            }
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
            for (var x = lo.Item1; x <= hi.Item1; x++)
                for (var y = lo.Item2; y <= hi.Item2; y++)
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
