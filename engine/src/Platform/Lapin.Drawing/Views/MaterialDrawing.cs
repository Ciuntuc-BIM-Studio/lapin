using BubbleBim.Geometry;

namespace Lapin.Drawing.Views;

/// <summary>A layer of an element's set, in order: its material (id), name and thickness.</summary>
public sealed record LayerRef(string? Material, string Name, double Thickness);

/// <summary>A hatch to lay over a cut band: its loops, pattern, scale and pen, and the class it belongs to.</summary>
public sealed record MaterialHatch(IReadOnlyList<Vec2[]> Loops, string Pattern, double Scale, int? Pen, string Class);

/// <summary>What the materials make of a cut (indices into the drawing's polygons and lines).</summary>
public sealed class MaterialDrawingResult
{
    public List<MaterialHatch> Hatches { get; } = [];
    /// <summary>Lines left out: a boundary inside one material.</summary>
    public HashSet<int> DropLines { get; } = [];
    /// <summary>Fill per polygon (null: none).</summary>
    public Dictionary<int, string?> Fills { get; } = [];
    /// <summary>Polygons not drawn at all (a membrane's own thin band).</summary>
    public HashSet<int> HidePolygons { get; } = [];
    /// <summary>Membrane lines (heavy, dashed).</summary>
    public List<(Vec2 A, Vec2 B)> Membranes { get; } = [];
    /// <summary>Per polygon, the sides (over its loops in order) it shares with another element's band of its material.</summary>
    public Dictionary<int, HashSet<int>> Seams { get; } = [];
}

/// <summary>
/// A cut drawn by its building materials (the old Lapin's, the ArchiCAD way):
/// <list type="bullet">
/// <item>each band takes its material's hatch, fill and hatch pen;</item>
/// <item>where bands of the SAME material of two elements touch, the cut line
/// between them goes and their shared sides are marked as seams, so the
/// material reads as one; elements with no material join the same way with
/// others of their kind (walls with walls, beams with members, slabs, columns,
/// footings); a material whose joints are drawn joins nothing;</item>
/// <item>a membrane — a layer under 2 mm, or a material marked as one — is a
/// heavy dashed line: along the middle of its own thin band, or on the
/// interface between its neighbouring layers when it has no band.</item>
/// </list>
/// </summary>
public static class MaterialDrawing
{
    const double Tol = 0.002;

    public static double Len((Vec2 A, Vec2 B) s) => (s.B - s.A).Length;

    /// <summary>How long <paramref name="q"/> runs along <paramref name="p"/> when both lie on one line (within the tolerance); negative when they do not.</summary>
    static double Overlap((Vec2 A, Vec2 B) p, (Vec2 A, Vec2 B) q, double tol)
    {
        var lp = Len(p);
        if (lp < tol || Len(q) < tol) return -1;
        var u = (p.B - p.A) * (1 / lp);
        double Off(Vec2 r) => Math.Abs((r.X - p.A.X) * u.Y - (r.Y - p.A.Y) * u.X);
        if (Off(q.A) > tol || Off(q.B) > tol) return -1;
        double T(Vec2 r) => Vec2.Dot(r - p.A, u);
        var lo = Math.Max(0, Math.Min(T(q.A), T(q.B)));
        var hi = Math.Min(lp, Math.Max(T(q.A), T(q.B)));
        return hi - lo;
    }

    /// <summary>Whether two segments lie on one line and overlap over at least half of the shorter.</summary>
    public static bool Coincide((Vec2 A, Vec2 B) p, (Vec2 A, Vec2 B) q, double tol = Tol) =>
        Overlap(p, q, tol) is var o && o >= 0 && o >= 0.5 * Math.Min(Len(p), Len(q));

    /// <summary>Whether a line lies on a side over at least half of the line's own length (a long line is not dropped for a short side on it).</summary>
    static bool Covers((Vec2 A, Vec2 B) side, (Vec2 A, Vec2 B) line) =>
        Math.Max(Overlap(line, side, Tol), Overlap(side, line, Tol)) is var o && o >= 0 && o >= 0.5 * Len(line);

    static List<(Vec2 A, Vec2 B)> Edges(DPolygon p)
    {
        var o = new List<(Vec2, Vec2)>();
        foreach (var loop in p.Loops) for (var i = 0; i < loop.Length; i++) o.Add((loop[i], loop[(i + 1) % loop.Length]));
        return o;
    }

    /// <summary>
    /// A coarse hash of segments: each in every cell it runs through (sampled
    /// at half a cell), looked up through the cells of the query segment and
    /// their neighbours, so a short side finds the long side it lies on
    /// however far apart their middles are.
    /// </summary>
    sealed class SegIndex<T>(double size = 0.5)
    {
        readonly List<((Vec2 A, Vec2 B) Seg, T Item)> _items = [];
        readonly Dictionary<(long, long), List<int>> _cells = [];
        (long, long) Key(Vec2 p) => ((long)Math.Floor(p.X / size), (long)Math.Floor(p.Y / size));

        /// <summary>The cells a segment runs through: its points every half cell, and its ends.</summary>
        HashSet<(long, long)> Cells((Vec2 A, Vec2 B) seg)
        {
            var o = new HashSet<(long, long)>();
            var n = (int)Math.Min(1e6, Math.Ceiling(Len(seg) / (size / 2)));
            for (var i = 0; i <= n; i++) o.Add(Key(n == 0 ? seg.A : seg.A + (seg.B - seg.A) * ((double)i / n)));
            return o;
        }

        public void Add((Vec2 A, Vec2 B) seg, T item)
        {
            _items.Add((seg, item));
            foreach (var k in Cells(seg))
            {
                if (!_cells.TryGetValue(k, out var l)) _cells[k] = l = [];
                l.Add(_items.Count - 1);
            }
        }

        public IEnumerable<((Vec2 A, Vec2 B) Seg, T Item)> Near((Vec2 A, Vec2 B) seg)
        {
            var seen = new HashSet<int>();
            foreach (var (cx, cy) in Cells(seg))
                for (var dx = -1; dx <= 1; dx++)
                    for (var dy = -1; dy <= 1; dy++)
                        if (_cells.TryGetValue((cx + dx, cy + dy), out var l))
                            foreach (var i in l)
                                if (seen.Add(i)) yield return _items[i];
        }
    }

    /// <summary>The middle line of a thin band: along its longest edge, halfway across.</summary>
    static (Vec2, Vec2)? Midline(DPolygon p)
    {
        var pts = p.Loops[0];
        if (pts.Length < 3) return null;
        var best = Edges(p).MaxBy(Len);
        var l = Len(best);
        if (l < 1e-12) return null;
        var u = (best.B - best.A) * (1 / l);
        double t0 = double.PositiveInfinity, t1 = double.NegativeInfinity, n0 = double.PositiveInfinity, n1 = double.NegativeInfinity;
        foreach (var q in pts)
        {
            var t = Vec2.Dot(q - best.A, u);
            var n = -(q.X - best.A.X) * u.Y + (q.Y - best.A.Y) * u.X;
            t0 = Math.Min(t0, t); t1 = Math.Max(t1, t); n0 = Math.Min(n0, n); n1 = Math.Max(n1, n);
        }
        var nm = (n0 + n1) / 2;
        Vec2 At(double t) => new(best.A.X + u.X * t - u.Y * nm, best.A.Y + u.Y * t + u.X * nm);
        return (At(t0), At(t1));
    }

    /// <summary>The kinds whose bands join one another when they have no material.</summary>
    static string? KindOf(string cls) => cls.ToUpperInvariant() switch
    {
        "IFCWALL" or "IFCWALLSTANDARDCASE" or "IFCWALLELEMENTEDCASE" => "wall",
        "IFCBEAM" or "IFCMEMBER" or "IFCBEAMSTANDARDCASE" or "IFCMEMBERSTANDARDCASE" => "linear",
        "IFCSLAB" or "IFCSLABSTANDARDCASE" => "slab",
        "IFCCOLUMN" or "IFCCOLUMNSTANDARDCASE" => "column",
        "IFCFOOTING" => "footing",
        _ => null,
    };

    /// <summary>What a band joins by: its material's name, else its kind; null joins nothing (a membrane, a material whose joints are drawn).</summary>
    static string? JoinKey(DPolygon p, ResolvedMaterial? g)
    {
        if (g != null) return g.Membrane || g.DrawJoints ? null : MaterialGraphics.Normalise(g.Name);
        // A leading space: no trimmed material name can be the same key.
        return KindOf(p.Class) is { } k ? " kind:" + k : null;
    }

    /// <param name="graphics">A polygon's material graphics, or null when it has none.</param>
    /// <param name="layers">An element's layers in order, when it has a layer set.</param>
    /// <param name="membrane">Whether a layer draws as a membrane.</param>
    public static MaterialDrawingResult Apply(ViewDrawing d, Func<DPolygon, ResolvedMaterial?> graphics, Func<string, IReadOnlyList<LayerRef>?>? layers = null, Func<LayerRef, bool>? membrane = null)
    {
        var o = new MaterialDrawingResult();
        var named = d.Cuts.Select((p, i) => (P: p, I: i, G: graphics(p))).ToList();
        // A band's layer in its element's set: by its part's key (layer:<index>), else the first layer of its material.
        int LayerIndex(DPolygon p, IReadOnlyList<LayerRef> ls)
        {
            if (p.Key is { } k && k.StartsWith("layer:", StringComparison.Ordinal) && int.TryParse(k[6..], System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 0 && n < ls.Count && ls[n].Material == p.Material) return n;
            for (var j = 0; j < ls.Count; j++) if (p.Material != null && ls[j].Material == p.Material) return j;
            return -1;
        }
        // The bands of layers that draw as membranes (a material marked as one, or a layer thinner than a membrane).
        var membraneBands = new HashSet<int>();
        foreach (var (p, i, g) in named)
            if (g?.Membrane == true || layers != null && membrane != null && layers(p.Element) is { } ls && LayerIndex(p, ls) is var k && k >= 0 && membrane(ls[k]))
                membraneBands.Add(i);

        // Bands: hatch and fill, or (a membrane's own band) a line along its middle.
        foreach (var (p, i, g) in named)
        {
            if (membraneBands.Contains(i))
            {
                o.HidePolygons.Add(i);
                if (Midline(p) is { } m) o.Membranes.Add(m);
                continue;
            }
            if (g == null) continue;
            o.Fills[i] = g.Fill;
            if (g.Hatch != null) o.Hatches.Add(new MaterialHatch(p.Loops, g.Hatch, g.HatchScale, g.HatchPen, p.Class));
        }

        // Edges of every band, by join key.
        var index = new SegIndex<(int Poly, string Key, int Side)>();
        foreach (var (p, i, g) in named)
            if (!membraneBands.Contains(i) && JoinKey(p, g) is { } key)
            {
                var es = Edges(p);
                for (var s = 0; s < es.Count; s++) index.Add(es[s], (i, key, s));
            }

        // Sides two elements' bands of one material share: their outlines leave them out.
        var seamSegs = new SegIndex<bool>();
        foreach (var (p, i, g) in named)
        {
            if (membraneBands.Contains(i) || JoinKey(p, g) is not { } key) continue;
            var es = Edges(p);
            for (var s = 0; s < es.Count; s++)
                foreach (var c in index.Near(es[s]))
                {
                    if (c.Item.Key != key || d.Cuts[c.Item.Poly].Element == p.Element) continue;
                    if (Coincide(es[s], c.Seg) || Coincide(c.Seg, es[s]))
                    {
                        if (!o.Seams.TryGetValue(i, out var set)) o.Seams[i] = set = [];
                        set.Add(s);
                        // The seam is where the two meet: the shorter of the two sides.
                        seamSegs.Add(Len(es[s]) <= Len(c.Seg) ? es[s] : c.Seg, true);
                    }
                }
        }
        // Any other line on a seam goes too: the edge between the two solids seen from above.
        for (var li = 0; li < d.Lines.Count; li++)
        {
            var seg = (d.Lines[li].A, d.Lines[li].B);
            if (seamSegs.Near(seg).Any(c => Covers(c.Seg, seg))) o.DropLines.Add(li);
        }
        var cutLines = Enumerable.Range(0, d.Lines.Count).Where(li => d.Lines[li].Role == LineRole.Cut).ToList();
        foreach (var li in cutLines)
        {
            var seg = (d.Lines[li].A, d.Lines[li].B);
            // Two elements' bands of one material meet along this line: it is inside the material.
            var byKey = index.Near(seg).Where(c => Covers(c.Seg, seg))
                .GroupBy(c => c.Item.Key).Select(g => g.Select(c => d.Cuts[c.Item.Poly].Element).Distinct().Count());
            if (byKey.Any(n => n > 1)) o.DropLines.Add(li);
        }

        // Thin layers without a band: on the interface between the layers either side of them.
        if (layers != null && membrane != null)
            foreach (var group in named.GroupBy(n => n.P.Element))
            {
                if (layers(group.Key) is not { } ls) continue;
                for (var k = 0; k < ls.Count; k++)
                {
                    var layer = ls[k];
                    // A membrane with a band of its own is drawn along its middle already.
                    if (!membrane(layer) || group.Any(x => membraneBands.Contains(x.I) && LayerIndex(x.P, ls) == k)) continue;
                    var before = ls.Take(k).Reverse().FirstOrDefault(l => !membrane(l));
                    var after = ls.Skip(k + 1).FirstOrDefault(l => !membrane(l));
                    if (before == null || after == null) continue;
                    List<(Vec2 A, Vec2 B)> Side(LayerRef l) => group.Where(x => x.P.Material == l.Material).SelectMany(x => Edges(x.P)).ToList();
                    var (a, b) = (Side(before), Side(after));
                    foreach (var e in a)
                    {
                        if (!b.Any(f => Coincide(e, f) || Coincide(f, e))) continue;
                        o.Membranes.Add(e);
                        foreach (var li in cutLines)
                            if (d.Lines[li].Element == group.Key && (Coincide(e, (d.Lines[li].A, d.Lines[li].B)) || Coincide((d.Lines[li].A, d.Lines[li].B), e))) o.DropLines.Add(li);
                    }
                }
            }
        return o;
    }
}
