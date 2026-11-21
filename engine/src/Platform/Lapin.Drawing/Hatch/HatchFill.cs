using Lapin.Geometry;

namespace Lapin.Drawing.Hatch;

/// <summary>One hatch stroke; a dot has A equal to B.</summary>
public readonly record struct HatchSegment(Vec2 A, Vec2 B);

/// <summary>How a pattern lies on a region: its scale (all pattern lengths), its rotation (°) about its origin, a cap on strokes.</summary>
public sealed record HatchOptions(double Scale, double AngleDeg = 0, Vec2? Origin = null, int MaxSegments = 200_000);

/// <summary>
/// Hatch strokes (the old Lapin's fill): each line family of a pattern is
/// clipped against a region, even-odd over all its loops (islands are holes,
/// islands in holes fill again), and its dashes applied.
/// <para>
/// Per family, every edge is projected once into the family's frame (h across,
/// t along). Line k sits at h = k·deltaY; an edge crosses it under the
/// half-open rule min(h) ≤ h_k &lt; max(h), so a vertex exactly on a line never
/// flips the parity. Edges are swept in h order with an active list.
/// </para>
/// </summary>
public static class HatchFill
{
    sealed record FEdge(double H0, double T0, double H1, double T1)
    {
        public double HMin => Math.Min(H0, H1);
        public double HMax => Math.Max(H0, H1);
    }

    sealed class Emitter(int max)
    {
        public readonly List<HatchSegment> Out = [];
        public readonly int Max = Math.Max(0, max);
        public bool Truncated;
        public void Emit(double lx, double ly, double dx, double dy, double t0, double t1)
        {
            if (Out.Count >= Max) { Truncated = true; return; }
            Out.Add(new HatchSegment(new Vec2(lx + t0 * dx, ly + t0 * dy), new Vec2(lx + t1 * dx, ly + t1 * dy)));
        }
    }

    /// <summary>The strokes of a pattern inside the even-odd region of the loops. A solid pattern gives none (the region is filled instead).</summary>
    public static (List<HatchSegment> Segments, bool Truncated) Segments(IReadOnlyList<IReadOnlyList<Vec2>> loops, HatchPattern pattern, HatchOptions o)
    {
        var em = new Emitter(o.MaxSegments);
        if (pattern.Solid || !(o.Scale > 0) || !double.IsFinite(o.Scale)) return (em.Out, false);
        var usable = loops.Where(l => l.Count >= 3).ToList();
        if (usable.Count == 0) return (em.Out, false);
        if (pattern.Tile is { } tile) Tiled(usable, tile, o, em);
        foreach (var f in pattern.Families)
        {
            if (em.Truncated) break;
            Family(usable, f, o, em);
        }
        return (em.Out, em.Truncated);
    }

    static void Family(List<IReadOnlyList<Vec2>> loops, HatchLineFamily fam, HatchOptions o, Emitter em)
    {
        var s = o.Scale;
        var spacing = fam.DeltaY * s;
        if (!(Math.Abs(spacing) > 1e-12) || !double.IsFinite(spacing)) return;
        var shift = fam.DeltaX * s;
        var dashes = fam.Dashes.Select(v => v * s).ToArray();
        var period = dashes.Sum(Math.Abs);
        var draws = dashes.Length == 0 || dashes.Any(v => v >= 0);
        if (!draws) return;
        if (dashes.Length > 0 && !(period > 1e-12)) return;

        var rot = o.AngleDeg * Math.PI / 180;
        var (cr, sr) = (Math.Cos(rot), Math.Sin(rot));
        var origin = o.Origin ?? new Vec2(0, 0);
        var (fx, fy) = (fam.Origin.X * s, fam.Origin.Y * s);
        var bx = origin.X + fx * cr - fy * sr;
        var by = origin.Y + fx * sr + fy * cr;
        var a = (fam.AngleDeg + o.AngleDeg) * Math.PI / 180;
        var (dx, dy) = (Math.Cos(a), Math.Sin(a));

        var edges = new List<FEdge>();
        foreach (var loop in loops)
            for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
            {
                var (px, py) = (loop[j].X - bx, loop[j].Y - by);
                var (qx, qy) = (loop[i].X - bx, loop[i].Y - by);
                var h0 = -px * dy + py * dx;
                var h1 = -qx * dy + qy * dx;
                if (h0 == h1) continue;
                edges.Add(new FEdge(h0, px * dx + py * dy, h1, qx * dx + qy * dy));
            }
        if (edges.Count == 0) return;
        edges.Sort((p, q) => p.HMin.CompareTo(q.HMin));
        var hLo = edges.Min(e => e.HMin);
        var hHi = edges.Max(e => e.HMax);
        var step = Math.Abs(spacing);
        var sign = spacing > 0 ? 1 : -1;
        var jFirst = (long)Math.Ceiling(hLo / step);
        var jLast = (long)Math.Floor(hHi / step);
        if (jLast < jFirst) return;
        if (jLast - jFirst > (long)em.Max * 10 + 1000) { em.Truncated = true; return; }

        var active = new List<FEdge>();
        var ts = new List<double>();
        var next = 0;
        for (var j = jFirst; j <= jLast; j++)
        {
            var h = j * step;
            var along = j * sign * shift;
            while (next < edges.Count && edges[next].HMin <= h) active.Add(edges[next++]);
            ts.Clear();
            active.RemoveAll(e => e.HMax <= h);
            foreach (var e in active)
            {
                var u = (h - e.H0) / (e.H1 - e.H0);
                ts.Add(e.T0 + u * (e.T1 - e.T0) - along);
            }
            if (ts.Count < 2) continue;
            ts.Sort();
            var lx = bx + along * dx - h * dy;
            var ly = by + along * dy + h * dx;
            for (var i = 0; i + 1 < ts.Count; i += 2)
            {
                var (t0, t1) = (ts[i], ts[i + 1]);
                if (!(t1 - t0 > 1e-12)) continue;
                if (dashes.Length == 0) em.Emit(lx, ly, dx, dy, t0, t1);
                else Dashed(em, lx, ly, dx, dy, t0, t1, dashes, period);
                if (em.Truncated) return;
            }
        }
    }

    /// <summary>
    /// A tile pattern's strokes (stage 7): its cell's pieces laid on every cell of its grid that reaches the region, each clipped to
    /// the region even-odd (a dot kept when it is inside). Arcs are made straight by the one rule for curves
    /// (<see cref="Chords"/>) at their size in the drawing.
    /// </summary>
    static void Tiled(List<IReadOnlyList<Vec2>> loops, HatchTile tile, HatchOptions o, Emitter em)
    {
        var s = o.Scale;
        var (w, h) = (tile.Width * s, tile.Height * s);
        if (!(w > 1e-12 && h > 1e-12) || !double.IsFinite(w) || !double.IsFinite(h)) return;
        var rot = o.AngleDeg * Math.PI / 180;
        var (cr, sr) = (Math.Cos(rot), Math.Sin(rot));
        var origin = o.Origin ?? new Vec2(0, 0);
        // The region in the grid's frame (its origin, its axes turned with the pattern), where cells are boxes.
        Vec2 ToGrid(Vec2 p) { var d = p - origin; return new Vec2(d.X * cr + d.Y * sr, -d.X * sr + d.Y * cr); }
        Vec2 FromGrid(Vec2 q) => new(origin.X + q.X * cr - q.Y * sr, origin.Y + q.X * sr + q.Y * cr);
        var region = loops.Select(l => l.Select(ToGrid).ToArray()).ToList();
        var (minX, minY, maxX, maxY) = (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        foreach (var l in region) foreach (var p in l) { minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); }
        // The cell's pieces at the drawing's size: straight pieces and dots, arcs as their chords.
        var pieces = new List<(Vec2 A, Vec2 B)>();
        foreach (var l in tile.Lines) pieces.Add((l.A * s, l.B * s));
        foreach (var a in tile.Arcs)
        {
            var pts = Chords.Arc(a.C * s, a.R * s, a.Start * Math.PI / 180, (a.Whole ? 360 : a.Sweep) * Math.PI / 180, a.Whole ? 3 : 1);
            for (var k = 0; k + 1 < pts.Count; k++) pieces.Add((pts[k], pts[k + 1]));
        }
        if (pieces.Count == 0) return;
        var (pMinX, pMinY, pMaxX, pMaxY) = (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        foreach (var (a, b) in pieces) { pMinX = Math.Min(pMinX, Math.Min(a.X, b.X)); pMinY = Math.Min(pMinY, Math.Min(a.Y, b.Y)); pMaxX = Math.Max(pMaxX, Math.Max(a.X, b.X)); pMaxY = Math.Max(pMaxY, Math.Max(a.Y, b.Y)); }
        var (i0, i1) = ((long)Math.Floor((minX - pMaxX) / w), (long)Math.Ceiling((maxX - pMinX) / w));
        var (j0, j1) = ((long)Math.Floor((minY - pMaxY) / h), (long)Math.Ceiling((maxY - pMinY) / h));
        if ((double)(i1 - i0 + 1) * (j1 - j0 + 1) * pieces.Count > (double)em.Max * 20 + 10000) { em.Truncated = true; return; }
        var edges = new List<(Vec2 A, Vec2 B)>();
        foreach (var l in region) for (int k = 0, m = l.Length - 1; k < l.Length; m = k++) if (l[m] != l[k]) edges.Add((l[m], l[k]));
        // A tile whose closed motifs are drawn whole (HatchTile.Whole): those motifs' pieces, kept or left out together.
        var motifs = tile.Whole ? Motifs(pieces, w, h) : [];
        var inMotif = new HashSet<int>(motifs.SelectMany(x => x));
        var ts = new List<double>();
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
            {
                var shift = new Vec2(i * w, j * h);
                // A cell whose pieces' box misses the region's: nothing.
                if (pMaxX + shift.X < minX || pMinX + shift.X > maxX || pMaxY + shift.Y < minY || pMinY + shift.Y > maxY) continue;
                foreach (var motif in motifs)
                {
                    // Whole: every piece inside, none crossing the outline.
                    if (!motif.All(pi => { var (a, b) = (pieces[pi].A + shift, pieces[pi].B + shift); return Inside(region, a) && Inside(region, b) && !edges.Any(e => Cross(a, b - a, e.A, e.B) != null); })) continue;
                    foreach (var pi in motif) { Emit(pieces[pi].A + shift, pieces[pi].B + shift); if (em.Truncated) return; }
                }
                for (var pi = 0; pi < pieces.Count; pi++)
                {
                    if (inMotif.Contains(pi)) continue;
                    var (pa, pb) = pieces[pi];
                    var (a, b) = (pa + shift, pb + shift);
                    if (Math.Max(a.X, b.X) < minX || Math.Min(a.X, b.X) > maxX || Math.Max(a.Y, b.Y) < minY || Math.Min(a.Y, b.Y) > maxY) continue;
                    var d = b - a;
                    if (d.Length < 1e-12)
                    {
                        if (Inside(region, a)) Emit(a, a);
                        if (em.Truncated) return;
                        continue;
                    }
                    ts.Clear();
                    ts.Add(0);
                    ts.Add(1);
                    foreach (var (ea, eb) in edges)
                        if (Cross(a, d, ea, eb) is { } t) ts.Add(t);
                    ts.Sort();
                    for (var k = 0; k + 1 < ts.Count; k++)
                    {
                        var (t0, t1) = (ts[k], ts[k + 1]);
                        if (t1 - t0 < 1e-9) continue;
                        if (!Inside(region, a + d * ((t0 + t1) / 2))) continue;
                        // Runs that go on through a crossing that changes nothing (a vertex touched) stay one stroke.
                        var end = t1;
                        while (k + 2 < ts.Count && Inside(region, a + d * ((ts[k + 1] + ts[k + 2]) / 2))) { k++; end = ts[k + 1]; }
                        Emit(a + d * t0, a + d * end);
                        if (em.Truncated) return;
                    }
                }
            }

        void Emit(Vec2 a, Vec2 b)
        {
            if (em.Out.Count >= em.Max) { em.Truncated = true; return; }
            em.Out.Add(new HatchSegment(FromGrid(a), FromGrid(b)));
        }
    }

    /// <summary>
    /// A tile's closed motifs: the pieces of one cell that join end to end into closed outlines (every end met by another
    /// piece's), each clear of its copies in the cells around (its box and theirs apart), by piece index.
    /// </summary>
    static List<List<int>> Motifs(List<(Vec2 A, Vec2 B)> pieces, double w, double h)
    {
        var eps = 1e-9 * Math.Max(w, h);
        var parent = Enumerable.Range(0, pieces.Count).ToArray();
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
        bool Near(Vec2 p, Vec2 q) => (p - q).Length <= eps;
        for (var i = 0; i < pieces.Count; i++)
            for (var j = i + 1; j < pieces.Count; j++)
                if (Near(pieces[i].A, pieces[j].A) || Near(pieces[i].A, pieces[j].B) || Near(pieces[i].B, pieces[j].A) || Near(pieces[i].B, pieces[j].B))
                    parent[Find(i)] = Find(j);
        var o = new List<List<int>>();
        foreach (var g in Enumerable.Range(0, pieces.Count).GroupBy(Find))
        {
            var ids = g.ToList();
            // Closed: each end of each piece met by an end of another (a lone dot or a loose stroke is no motif).
            var ends = ids.SelectMany(i => new[] { pieces[i].A, pieces[i].B }).ToList();
            if (ids.Any(i => (pieces[i].A - pieces[i].B).Length <= eps) || ends.Any(p => ends.Count(q => Near(p, q)) < 2)) continue;
            double x0 = ends.Min(p => p.X), x1 = ends.Max(p => p.X), y0 = ends.Min(p => p.Y), y1 = ends.Max(p => p.Y);
            // Apart from its copies one cell over (a lattice's motifs touch theirs: those stay clipped).
            if (x1 - x0 >= w - eps || y1 - y0 >= h - eps) continue;
            o.Add(ids);
        }
        return o;
    }

    /// <summary>Where the segment a + t·d (0 &lt; t &lt; 1) crosses the edge e0–e1; null when it does not, or runs along it.</summary>
    static double? Cross(Vec2 a, Vec2 d, Vec2 e0, Vec2 e1)
    {
        var e = e1 - e0;
        var den = d.X * e.Y - d.Y * e.X;
        if (Math.Abs(den) < 1e-18) return null;
        var w = e0 - a;
        var t = (w.X * e.Y - w.Y * e.X) / den;
        var u = (w.X * d.Y - w.Y * d.X) / den;
        return t > 0 && t < 1 && u >= 0 && u <= 1 ? t : null;
    }

    /// <summary>Whether a point is inside loops, even-odd.</summary>
    static bool Inside(List<Vec2[]> loops, Vec2 p)
    {
        var inside = false;
        foreach (var l in loops)
            for (int i = 0, j = l.Length - 1; i < l.Length; j = i++)
                if ((l[i].Y > p.Y) != (l[j].Y > p.Y) && p.X < (l[j].X - l[i].X) * (p.Y - l[i].Y) / (l[j].Y - l[i].Y) + l[i].X) inside = !inside;
        return inside;
    }

    static void Dashed(Emitter em, double lx, double ly, double dx, double dy, double t0, double t1, double[] dashes, double period)
    {
        var pos = Math.Floor(t0 / period) * period;
        while (pos <= t1)
            foreach (var v in dashes)
            {
                var len = Math.Abs(v);
                if (v > 0)
                {
                    var (a, b) = (Math.Max(pos, t0), Math.Min(pos + len, t1));
                    if (b > a) em.Emit(lx, ly, dx, dy, a, b);
                }
                else if (v == 0 && pos >= t0 && pos <= t1) em.Emit(lx, ly, dx, dy, pos, pos);
                if (em.Truncated) return;
                pos += len;
                if (pos > t1) return;
            }
    }
}

/// <summary>
/// How big a pattern lies in a drawing (decision 679): a paper pattern's millimetres are paper millimetres, so at 1:scale
/// a millimetre is scale/1000 m of the drawing; a model pattern's are the object's, a millimetre 1/1000 m at any scale.
/// A model pattern so dense on paper that its lines would run together (its closest family under
/// <see cref="DefaultMinSpacing"/> mm apart, a view style's <c>minHatchSpacing</c>) is not drawn.
/// </summary>
public static class HatchScale
{
    /// <summary>The closest a real-size hatch's lines may lie on paper (mm) and still be drawn (decision 679, Î4).</summary>
    public const double DefaultMinSpacing = 0.7;

    /// <summary>The units a pattern lies in: <paramref name="over"/> (a material's <c>hatchUnits</c>), else the pattern's own.</summary>
    public static HatchUnits UnitsOf(HatchPattern p, HatchUnits? over = null) => over ?? p.Units;

    /// <summary>"paper" or "model", as the protocol writes them.</summary>
    public static string Name(HatchUnits u) => u == HatchUnits.Model ? "model" : "paper";

    /// <summary>"paper" or "model" read (any case); null otherwise.</summary>
    public static HatchUnits? Parse(string? s) => s?.Trim().ToLowerInvariant() switch { "paper" => HatchUnits.Paper, "model" => HatchUnits.Model, _ => null };

    /// <summary>Drawing metres per pattern unit at 1:<paramref name="viewScale"/>, times the material's own <paramref name="userScale"/>.</summary>
    public static double Factor(HatchPattern p, double viewScale, double userScale = 1, HatchUnits? over = null) =>
        userScale * (UnitsOf(p, over) == HatchUnits.Model ? 0.001 : viewScale / 1000);

    /// <summary>How far apart (paper mm) the closest lines of a pattern lie at a <paramref name="factor"/> (drawing metres per unit) at 1:<paramref name="viewScale"/>; ∞ with no lines.</summary>
    public static double PaperSpacing(HatchPattern p, double factor, double viewScale)
    {
        var min = double.PositiveInfinity;
        foreach (var f in p.Families) if (Math.Abs(f.DeltaY) > 1e-12) min = Math.Min(min, Math.Abs(f.DeltaY));
        // A tile's cell repeats at its smaller side (stage 7).
        if (p.Tile is { } t) min = Math.Min(min, Math.Min(t.Width, t.Height));
        return min * factor * 1000 / viewScale;
    }

    /// <summary>Whether a model pattern lies too dense to draw (paper patterns are always drawn, as before).</summary>
    public static bool TooDense(HatchPattern p, double factor, double viewScale, double minSpacing, HatchUnits? over = null) =>
        UnitsOf(p, over) == HatchUnits.Model && minSpacing > 0 && viewScale > 0 && PaperSpacing(p, factor, viewScale) < minSpacing - 1e-9;

    /// <summary>The options a pattern is laid with in a drawing at 1:<paramref name="viewScale"/>; null when it is too dense to draw.</summary>
    public static HatchOptions? Lay(HatchPattern p, double viewScale, double userScale, double angle, Vec2? origin, int max, HatchUnits? over = null, double minSpacing = DefaultMinSpacing)
    {
        var factor = Factor(p, viewScale, userScale, over);
        return TooDense(p, factor, viewScale, minSpacing, over) ? null : new HatchOptions(factor, angle, origin, max);
    }
}
