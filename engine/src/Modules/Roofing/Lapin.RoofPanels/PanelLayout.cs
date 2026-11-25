using Lapin.Geometry;

namespace Lapin.RoofPanels;

/// <summary>
/// The panel layout of a roof (docs/plan-roof-panels.md §2.3), the Panotaj Bilka app's (layout.py, reuse.py) rewritten:
/// each face in columns of the useful width from its eave, the column's start on the module, a phase across the face tried
/// every 15 mm (a column with a sliver of eave under 22 cm refused); each column one panel or several, lapped up the slope;
/// the whole roof in the fewest millimetres of panel, then the fewest panels. A made-to-order profile then tries fewer
/// different lengths, at most 5 % more material and no more panels. A profile whose offcuts are reused tries other phases
/// on each face for the layout that buys the least once hip offcuts fill valley pieces (a bipartite matching, the longest
/// valley pieces first).
/// </summary>
public static class PanelLayout
{
    /// <summary>
    /// A column, or a run of one cut by an opening: its left side u, its first panel's start (in modules up from the eave), its top
    /// (mm up the slope), the target in it; <see cref="Index"/>, the column it is a run of (0 the first across the face);
    /// <see cref="Region"/>, the run's part of the face over the panel's whole width (null: the face's whole target).
    /// </summary>
    public sealed record Column(double X, int Start, int Stop, List<Vec2> Footprint, int Index = 0, List<Vec2>? Region = null);

    /// <summary>A panel laid: its left side and start (u, v), its nominal length (mm), its cut outline (whole width) and what it covers (useful width).</summary>
    public sealed record Piece(double X, double Y, int Length, List<Vec2> Gross, List<Vec2> Covered);

    /// <summary>A face's layout: millimetres of panel, panels, the columns (runs) of pieces, and the column each run is of.</summary>
    public sealed record FaceLayout(long Cost, int Number, List<List<Piece>> Columns, List<int> Index);

    /// <summary>A panel of the result: its code (face-column-row), where it is, its edges' kinds.</summary>
    public sealed class Panel
    {
        public required string Id { get; init; }
        public required RoofFace Face { get; init; }
        public required double X { get; init; }
        public required double Y { get; init; }
        public required int Length { get; init; }
        public required List<Vec2> Poly { get; init; }
        public required List<Vec2> Net { get; init; }
        public required int Col { get; init; }
        public required int Row { get; init; }
        /// <summary>It starts at the eave or on a joint (Python's <c>bottom</c>): a valley piece shifted on its donor must not move it.</summary>
        public required bool Bottom { get; init; }
        /// <summary>Another panel laps over its top.</summary>
        public required bool Upper { get; init; }
        public required List<string> Tags { get; init; }
    }

    /// <summary>The layout of a roof: the stock lengths it used, each face's, the panels and the offcuts reused (recipient → donor, shift mm).</summary>
    public sealed record Result(IReadOnlyList<int> Stock, IReadOnlyList<FaceLayout> Faces, List<Panel> Panels, Dictionary<string, (string Donor, int Shift)> Reuse);

    public static Result Lay(IReadOnlyList<RoofFace> faces, IReadOnlyList<RoofEdge> edges, PanelProfile profile)
    {
        var s = new Solver(profile);
        var (stock, layouts) = s.Solve(faces);
        if (profile.ReuseOffcuts) layouts = s.Refine(faces, edges, stock, layouts);
        var panels = Collect(faces, edges, layouts, profile);
        var reuse = profile.ReuseOffcuts ? Match(panels, profile) : [];
        return new Result(stock, layouts, panels, reuse);
    }

    sealed class Solver(PanelProfile p)
    {
        readonly Dictionary<(int, int, string), int[]?> _memo = [];

        /// <summary>
        /// The cheapest panels for a column from module <paramref name="start"/> to <paramref name="stopMm"/>: one when a
        /// length reaches (a made-to-order profile takes it), else a panel whose length less the lap is whole modules and the
        /// rest, never a longer one above a shorter; the least millimetres, then the fewest panels, then the longest first.
        /// </summary>
        public int[]? Best(int start, int stopMm, IReadOnlyList<int> stock)
        {
            var key = (start, stopMm, string.Join(",", stock));
            if (_memo.TryGetValue(key, out var hit)) return hit;
            var needed = stopMm - start * p.Module;
            int[]? last = null;
            foreach (var length in stock) if (length >= needed) { last = [length]; break; }
            int[]? result;
            if (last != null && p.OnOrder) result = last;
            else
            {
                var possibles = new List<int[]>();
                if (last != null) possibles.Add(last);
                foreach (var length in stock)
                {
                    var advance = length - p.SlopeLap;
                    if (length >= needed || advance <= 0 || advance % p.Module != 0) continue;
                    var remaining = Best(start + advance / p.Module, stopMm, [.. stock.Where(n => n <= length)]);
                    if (remaining != null) possibles.Add([length, .. remaining]);
                }
                result = possibles.Count == 0 ? null : possibles.OrderBy(q => q.Sum()).ThenBy(q => q.Length).ThenBy(q => q, Lexi.Descending).First();
            }
            _memo[key] = result;
            return result;
        }

        /// <summary>
        /// A staggered profile's column (a standing seam): the longest panel while the rest is longer, every other column's
        /// <see cref="PanelProfile.Stagger"/> shorter so the joints do not line up, then the shortest that reaches.
        /// </summary>
        public int[] Staggered(int start, int stop, IReadOnlyList<int> stock, int column)
        {
            var need = stop - start * p.Module;
            var lengths = new List<int>();
            var maximum = stock.Max();
            while (need > maximum - p.EndFold)
            {
                var lower = maximum - p.Stagger * (column % 2);
                lengths.Add(lower);
                need -= lower - p.EndFold - p.SlopeLap;
            }
            lengths.Add(stock.First(n => n - p.EndFold >= need));
            return [.. lengths];
        }

        /// <summary>
        /// Each phase across the face that leaves no sliver of eave: its columns. A column an opening crosses (S3) is laid in runs:
        /// below the opening, beside it (each side its own run, its panels cut round it) and above it, each run starting on the
        /// module as a column does.
        /// </summary>
        public List<List<Column>> Columns(RoofFace roof)
        {
            var width = p.Width;
            double low = Clip.MinX(roof.Target), high = Clip.MaxX(roof.Target);
            var options = new List<List<Column>>();
            // A phase the user fixed on the face (S7): that one alone, a sliver of eave warned rather than refused.
            var fixedPhase = roof.FixedPhase is { } fp ? Phase(fp, width) : (double?)null;
            for (var k = 0; fixedPhase != null ? k == 0 : k * 0.015 < width - 1e-6; k++)
            {
                var phase = fixedPhase ?? k * 0.015;
                var cols = new List<Column>();
                var left = (int)Math.Floor((low - phase + 1e-7) / width);
                var right = (int)Math.Ceiling((high - phase - 1e-7) / width);
                var ok = true;
                var index = 0;
                int Start(List<Vec2> f) => Math.Max(0, (int)Math.Floor((Clip.MinY(f) + 1e-6) / p.Pitch));
                int Stop(List<Vec2> f) => (int)Math.Ceiling((Clip.MaxY(f) - 1e-6) * 1000);
                for (var ix = left; ix < right; ix++)
                {
                    var x = phase + ix * width;
                    var footprint = Clip.Strip(roof.Target, x, x + width);
                    if (Clip.Area(footprint) < 1e-6) continue;
                    var eaveWidth = roof.EaveSpans.Sum(e => Math.Max(0, Math.Min(x + width, e.To) - Math.Max(x, e.From)));
                    if (eaveWidth > 1e-5 && eaveWidth < 0.22 && fixedPhase == null) { ok = false; break; }
                    if (roof.Holes.Count == 0) { cols.Add(new Column(x, Start(footprint), Stop(footprint), footprint, index++)); continue; }
                    var any = false;
                    foreach (var region in Runs(Clip.Strip(roof.Target, x, x + p.Gross), roof.Holes))
                    {
                        var f = Clip.Strip(region, x, x + width);
                        if (Clip.Area(f) < 1e-6) continue;
                        cols.Add(new Column(x, Start(f), Stop(f), f, index, region));
                        any = true;
                    }
                    if (any) index++;
                }
                if (ok) options.Add(cols);
            }
            if (options.Count == 0) throw new ArgumentException($"Roof plane {roof.Id} takes no layout of {p.Name}.");
            return options;
        }

        /// <summary>
        /// A column's strip cut into runs by the holes it crosses, bottom to top: for each hole the part below it, the parts beside
        /// it (the band of its height less the hole: one a side, or one when the column reaches past one side only) and above it.
        /// </summary>
        static List<List<Vec2>> Runs(List<Vec2> strip, IReadOnlyList<FaceHole> holes)
        {
            var runs = new List<List<Vec2>> { strip };
            foreach (var h in holes.OrderBy(h => h.MinV))
            {
                var next = new List<List<Vec2>>();
                foreach (var s in runs)
                {
                    if (s.Count < 3 || Clip.MaxX(s) <= Clip.MinX(h.Ring) + 1e-9 || Clip.MinX(s) >= Clip.MaxX(h.Ring) - 1e-9
                        || Clip.MaxY(s) <= h.MinV + 1e-9 || Clip.MinY(s) >= h.MaxV - 1e-9) { next.Add(s); continue; }
                    var below = Clip.Trim(s, new Vec2(0, -1), -h.MinV);
                    var above = Clip.Trim(s, new Vec2(0, 1), h.MaxV);
                    var band = Clip.Trim(Clip.Trim(s, new Vec2(0, 1), h.MinV), new Vec2(0, -1), -h.MaxV);
                    if (Clip.Area(below) > 1e-9) next.Add(below);
                    if (band.Count >= 3 && Clip.Area(band) > 1e-9)
                        next.AddRange(PlanTrim.Difference(band, [.. h.Regions]).Rings.Where(r => Clip.Area(r) > 1e-9));
                    if (Clip.Area(above) > 1e-9) next.Add(above);
                }
                runs = next;
            }
            return [.. runs.OrderBy(r => Math.Round(Clip.MinY(r), 6)).ThenBy(r => Clip.MinX(r))];
        }

        /// <summary>The face's layouts that cover it, cheapest first, while within <paramref name="allowance"/> mm of the first.</summary>
        public IEnumerable<FaceLayout> Candidates(RoofFace roof, List<List<Column>> options, IReadOnlyList<int> stock, long allowance = 0)
        {
            var layouts = new List<(long Cost, int Number, List<Column> Cols, List<int[]> Seqs)>();
            foreach (var cols in options)
            {
                var seqs = new List<int[]>();
                var all = true;
                for (var i = 0; i < cols.Count; i++)
                {
                    var c = cols[i];
                    var seq = p.Staggered ? Staggered(c.Start, c.Stop, stock, c.Index) : Best(c.Start, c.Stop, stock);
                    if (seq == null) { all = false; break; }
                    seqs.Add(seq);
                }
                if (all) layouts.Add((seqs.Sum(q => (long)q.Sum()), seqs.Sum(q => q.Length), cols, seqs));
            }
            long? first = null;
            var target = roof.TargetArea;
            foreach (var (cost, number, cols, seqs) in layouts.OrderBy(l => l.Cost).ThenBy(l => l.Number))
            {
                if (first != null && cost > first + allowance) yield break;
                var chosen = new List<List<Piece>>();
                var failed = false;
                for (var ci = 0; ci < cols.Count && !failed; ci++)
                {
                    var (x, start, _, footprint, _, region) = cols[ci];
                    var sequence = seqs[ci];
                    var y = start * p.Pitch;
                    var pieces = new List<Piece>();
                    for (var index = 0; index < sequence.Length; index++)
                    {
                        var length = sequence[index];
                        // The side lap stays outside the useful width.
                        var end = y + p.EffectiveLength(length);
                        var covered = Clip.Strip(footprint, x, x + p.Width, y, index == sequence.Length - 1 ? end : end - p.Lap);
                        var gross = Clip.Strip(region ?? roof.Target, x, x + p.Gross, y, end);
                        if (Clip.Area(covered) < 1e-7) { failed = true; break; }
                        pieces.Add(new Piece(x, y, length, gross, covered));
                        y = end - p.Lap;
                    }
                    if (!failed) chosen.Add(pieces);
                }
                if (failed) continue;
                if (Math.Abs(chosen.Sum(c => c.Sum(q => Clip.Area(q.Covered))) - target) < 2e-5)
                {
                    first ??= cost;
                    yield return new FaceLayout(cost, number, chosen, [.. cols.Select(c => c.Index)]);
                }
            }
        }

        (long Cost, int Number, IReadOnlyList<int> Stock, List<FaceLayout> Layouts)? Evaluate(IReadOnlyList<RoofFace> roofs, List<List<List<Column>>> options, IReadOnlyList<int> stock)
        {
            var layouts = new List<FaceLayout>();
            for (var i = 0; i < roofs.Count; i++)
            {
                var l = Candidates(roofs[i], options[i], stock).FirstOrDefault();
                if (l == null) return null;
                layouts.Add(l);
            }
            return (layouts.Sum(l => l.Cost), layouts.Sum(l => l.Number), stock, layouts);
        }

        static HashSet<int> Used(List<FaceLayout> v) => [.. v.SelectMany(f => f.Columns).SelectMany(c => c).Select(q => q.Length)];

        public (IReadOnlyList<int> Stock, List<FaceLayout> Layouts) Solve(IReadOnlyList<RoofFace> roofs)
        {
            var options = roofs.Select(Columns).ToList();
            var baseline = Evaluate(roofs, options, p.Lengths) ?? throw new ArgumentException($"No layout of {p.Name} covers the roof with its lengths.");
            if (!p.OnOrder) return (baseline.Stock, baseline.Layouts);
            // Fewer lengths to order, with no more joints and little more material.
            IReadOnlyList<int> regular = p.Staggered ? [.. p.Lengths.Take(p.Lengths.Count - 1)] : [.. p.Lengths.Where(n => (n - p.SlopeLap) % p.Module == 0)];
            var anchor = Used(baseline.Layouts).Max();
            var anchors = p.Staggered ? new HashSet<int> { anchor, p.Lengths[^1] } : [anchor];
            var stocks = new List<int[]>();
            foreach (var step in new[] { 2, 3, 4 })
                for (var offset = 0; offset < step; offset++)
                {
                    var set = new SortedSet<int>(anchors);
                    for (var i = offset; i < regular.Count; i += step) set.Add(regular[i]);
                    int[] st = [.. set];
                    if (!stocks.Any(x => x.SequenceEqual(st))) stocks.Add(st);
                }
            stocks.Sort(Lexi.Ascending);
            var viable = new List<(long Cost, int Number, IReadOnlyList<int> Stock, List<FaceLayout> Layouts)> { baseline };
            foreach (var st in stocks)
                if (Evaluate(roofs, options, st) is { } c && c.Cost <= baseline.Cost * 1.05 && c.Number <= baseline.Number) viable.Add(c);
            var best = viable.OrderBy(v => Used(v.Layouts).Count).ThenBy(v => v.Cost).ThenBy(v => v.Number).First();
            return (best.Stock, best.Layouts);
        }

        /// <summary>Each face in turn: of its layouts within 2.35 m of panel of its cheapest, about 14 across, the one that buys least once offcuts are reused.</summary>
        public List<FaceLayout> Refine(IReadOnlyList<RoofFace> roofs, IReadOnlyList<RoofEdge> edges, IReadOnlyList<int> stock, List<FaceLayout> layouts)
        {
            var current = new List<FaceLayout>(layouts);
            (long, int, int) Score()
            {
                var rows = Collect(roofs, edges, current, p);
                var reuse = Match(rows, p);
                return (rows.Where(r => !reuse.ContainsKey(r.Id)).Sum(r => (long)r.Length), rows.Count, -reuse.Count);
            }
            for (var i = 0; i < roofs.Count; i++)
            {
                var options = Candidates(roofs[i], Columns(roofs[i]), stock, 2350).ToList();
                var every = Math.Max(1, options.Count / 14);
                var best = current[i];
                var bestScore = Score();
                for (var k = 0; k < options.Count; k += every)
                {
                    current[i] = options[k];
                    var value = Score();
                    if (value.CompareTo(bestScore) < 0) (bestScore, best) = (value, options[k]);
                }
                current[i] = best;
            }
            return current;
        }
    }

    /// <summary>A phase taken into [0, <paramref name="width"/>): the same columns.</summary>
    public static double Phase(double phase, double width) => width > 0 ? ((phase % width) + width) % width : 0;

    /// <summary>The phase of a face's layout (metres, in [0, useful width)): its first column's left side, modulo the width; 0 for none.</summary>
    public static double PhaseOf(FaceLayout layout, PanelProfile p) =>
        layout.Columns.FirstOrDefault(c => c.Count > 0) is { } c ? Phase(c[0].X, p.Width) : 0;

    /// <summary>Whether the columns of <paramref name="roof"/> at <paramref name="phase"/> leave a sliver of eave under 22 cm (a phase the layout itself refuses).</summary>
    public static bool LeavesSliver(RoofFace roof, PanelProfile p, double phase)
    {
        var width = p.Width;
        phase = Phase(phase, width);
        double low = Clip.MinX(roof.Target), high = Clip.MaxX(roof.Target);
        var left = (int)Math.Floor((low - phase + 1e-7) / width);
        var right = (int)Math.Ceiling((high - phase - 1e-7) / width);
        for (var ix = left; ix < right; ix++)
        {
            var x = phase + ix * width;
            if (Clip.Area(Clip.Strip(roof.Target, x, x + width)) < 1e-6) continue;
            var eaveWidth = roof.EaveSpans.Sum(e => Math.Max(0, Math.Min(x + width, e.To) - Math.Max(x, e.From)));
            if (eaveWidth > 1e-5 && eaveWidth < 0.22) return true;
        }
        return false;
    }

    /// <summary>Whether one of the polygon's edges lies along the line a–b moved <paramref name="offset"/> to its left, over more than 5 mm of it.</summary>
    static bool SegmentHits(IReadOnlyList<Vec2> poly, Vec2 a, Vec2 b, double offset)
    {
        var d = b - a;
        var len = d.Length;
        var axis = d * (1 / len);
        var normal = new Vec2(-axis.Y, axis.X);
        for (var i = 0; i < poly.Count; i++)
        {
            var j = (i + 1) % poly.Count;
            double pi = Vec2.Dot(poly[i] - a, axis), pj = Vec2.Dot(poly[j] - a, axis);
            double hi = Vec2.Dot(poly[i] - a, normal), hj = Vec2.Dot(poly[j] - a, normal);
            if (Math.Max(Math.Abs(hi - offset), Math.Abs(hj - offset)) < 1e-5 && Math.Min(Math.Max(pi, pj), len) - Math.Max(Math.Min(pi, pj), 0) > 0.005) return true;
        }
        return false;
    }

    /// <summary>The panels of every face, each with the kinds of the edges its cut outline runs along (a valley's at the gap from it).</summary>
    public static List<Panel> Collect(IReadOnlyList<RoofFace> roofs, IReadOnlyList<RoofEdge> edges, IReadOnlyList<FaceLayout> layouts, PanelProfile profile)
    {
        var o = new List<Panel>();
        var gap = profile.ValleyGap / 1000.0;
        for (var fi = 0; fi < roofs.Count; fi++)
        {
            var roof = roofs[fi];
            var mean = new Vec2(roof.Poly.Average(v => v.X), roof.Poly.Average(v => v.Y));
            var relevant = edges.Where(e => e.Faces.Contains(roof.Id)).Select(e => (e.Kind, A: roof.Uv(e.A), B: roof.Uv(e.B))).ToList();
            var cols = layouts[fi].Columns;
            // A column's runs (an opening crosses it) number their panels on from the run under them.
            var rows = new Dictionary<int, int>();
            for (var c = 0; c < cols.Count; c++)
                for (var r = 0; r < cols[c].Count; r++)
                {
                    var piece = cols[c][r];
                    var col = layouts[fi].Index[c];
                    var row = rows[col] = rows.GetValueOrDefault(col) + 1;
                    var tags = new List<string>();
                    foreach (var (kind, a, b) in relevant)
                    {
                        var offset = kind == EdgeKinds.Valley ? gap * Math.Sign(Vec2.Cross(b - a, mean - a)) : 0;
                        if (SegmentHits(piece.Gross, a, b, offset)) tags.Add(kind);
                    }
                    o.Add(new Panel
                    {
                        Id = $"{roof.Id}-{col + 1:00}-{row:00}", Face = roof, X = piece.X, Y = piece.Y, Length = piece.Length,
                        Poly = piece.Gross, Net = piece.Covered, Col = col + 1, Row = row, Bottom = r > 0 || piece.Y < 1e-6,
                        Upper = r < cols[c].Count - 1, Tags = tags,
                    });
                }
        }
        return o;
    }

    /// <summary>
    /// The offcuts reused (reuse.match): a valley piece cut from what a hip piece leaves of its panel, the two outlines apart
    /// on the panel by a clearance, the valley piece shifted up the panel by whole modules (its bottom kept when it starts on
    /// the eave or a joint, its top on the panel's when another laps over it); the most valley pieces, the longest first.
    /// </summary>
    public static Dictionary<string, (string Donor, int Shift)> Match(List<Panel> records, PanelProfile profile)
    {
        if (!profile.ReuseOffcuts) return [];
        var donors = records.Where(r => r.Tags.Contains(EdgeKinds.Hip) && !r.Tags.Contains(EdgeKinds.Valley)).ToList();
        var receivers = records.Where(r => r.Tags.Contains(EdgeKinds.Valley)).ToList();
        var shapes = donors.Select(d => (D: d, P: Clip.Shift(d.Poly, -d.X, -d.Y))).ToList();
        var compatible = new Dictionary<string, List<(string Donor, int Shift)>>();
        foreach (var r in receivers)
        {
            var q = Clip.Shift(r.Poly, -r.X, -r.Y);
            double lower = Clip.MinY(q), upper = Clip.MaxY(q), qArea = Clip.Area(q);
            var choices = new List<(string, int)>();
            foreach (var (d, pp) in shapes)
            {
                var stock = d.Length / 1000.0;
                if (qArea + Clip.Area(pp) > stock * profile.Gross || upper - lower > stock + 1e-6) continue;
                var k0 = (int)Math.Ceiling((-lower - 1e-6) / profile.Pitch);
                var k1 = (int)Math.Floor((stock - upper + 1e-6) / profile.Pitch);
                for (var k = k0; k <= k1; k++)
                {
                    var shift = k * profile.Pitch;
                    if (r.Bottom && k != 0 && lower < 1e-5) continue;
                    if (r.Upper && Math.Abs(stock - upper - shift) > 1e-5) continue;
                    if (Clip.Separated(pp, Clip.Shift(q, 0, shift))) { choices.Add((d.Id, (int)Math.Round(shift * 1000))); break; }
                }
            }
            compatible[r.Id] = choices;
        }
        var chosen = new Dictionary<string, (string Receiver, int Shift)>();
        bool Augment(string receiver, HashSet<string> seen)
        {
            foreach (var (donor, shift) in compatible[receiver])
            {
                if (!seen.Add(donor)) continue;
                if (!chosen.TryGetValue(donor, out var was) || Augment(was.Receiver, seen))
                {
                    chosen[donor] = (receiver, shift);
                    return true;
                }
            }
            return false;
        }
        foreach (var r in receivers.OrderBy(r => -r.Length).ThenBy(r => compatible[r.Id].Count)) Augment(r.Id, []);
        return chosen.ToDictionary(kv => kv.Value.Receiver, kv => (kv.Key, kv.Value.Shift));
    }

    /// <summary>Integer sequences compared as Python compares tuples.</summary>
    sealed class Lexi(int sign) : IComparer<int[]>
    {
        public static readonly Lexi Ascending = new(1), Descending = new(-1);
        public int Compare(int[]? a, int[]? b)
        {
            for (var i = 0; i < Math.Min(a!.Length, b!.Length); i++) if (a[i] != b[i]) return sign * a[i].CompareTo(b[i]);
            return a.Length.CompareTo(b.Length);
        }
    }
}
