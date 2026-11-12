using Lapin.Geometry;

namespace Lapin.Architecture.Stairs;

/// <summary>A line of a stair's plan symbol in the stair's own frame: a polyline, closed or not, under the plan's cut or over it.</summary>
public sealed record StairPlanLine(Vec2[] Points, bool Closed, bool Above);

/// <summary>
/// A stair's plan symbol in its own frame (report #143): the treads, the flights' sides, the landings, the winders and a
/// spiral's pole (<see cref="Lines"/>); the walking line with its arrow and the dot at its foot (<see cref="Walk"/>); the
/// break lines where the plan's cut crosses the stair (<see cref="Breaks"/>); and the walking lines whole, as polylines
/// (<see cref="Paths"/>), for what is written beside them.
/// </summary>
public sealed record StairPlanSymbol(List<StairPlanLine> Lines, List<StairPlanLine> Walk, List<Vec2[]> Breaks, List<Vec2[]> Paths);

/// <summary>
/// How a plan draws a stair (report #143, as Revit and ArchiCAD do): cut at the plan's cut plane. What lies under the cut
/// draws as seen; a break line crosses the flight where its nosings reach the cut, slanted
/// <see cref="BreakAngle"/> off square; what lies over it draws in the overhead style (<c>dashed</c>), or not at all
/// (<c>hidden</c>); the walking line goes on to the top, dashed past the break, its arrow at the top (at the break when
/// the part over the cut is hidden). A stair the cut does not reach draws whole, as before; <c>whole</c> draws every stair so.
/// Where the cut falls in a turn of winders or on a spiral, the break goes square across the walking line there (radial on
/// a spiral). The plan's elements (<see cref="StairBuilder"/>'s pick lines) are the symbol with no cut.
/// </summary>
public static class StairPlan
{
    /// <summary>The break line's slant off square across the flight: 30°, so it runs at 60° to the flight.</summary>
    public const double BreakAngle = 30 * Math.PI / 180;

    /// <summary>How the part over the cut draws: in the overhead style, not at all, or the stair whole with no cut.</summary>
    public static readonly string[] AboveModes = ["dashed", "hidden", "whole"];

    /// <summary>The break line's symbol: one line with a zigzag in its middle, or two parallel lines.</summary>
    public static readonly string[] BreakStyles = ["zigzag", "double"];

    /// <summary>
    /// Whether the step numbers go on the left of the walking line going up: when the stair turns right, so they stand on
    /// the outer side of the turn, where the treads are wider; otherwise on the right (report #143). A break line's zigzag
    /// goes on the other side, clear of them and of the walking line.
    /// </summary>
    public static bool NumbersOnLeft(Resolved r) =>
        r.Flights.Count > 1 && r.Spec.Shape != "split" && Vec2.Dot(r.Flights[^1].Start - r.Flights[0].Start, r.Flights[0].Left) < -1e-6;

    sealed record WalkSeg(Vec2 A, Vec2 B, double ZA, double ZB, int Flight);

    /// <summary>
    /// The symbol for a plan cutting at <paramref name="cut"/> over the stair's foot (null: no cut, the stair whole);
    /// <paramref name="breakSize"/> the zigzag's size (m, a few paper millimetres at the plan's scale).
    /// </summary>
    public static StairPlanSymbol Of(Resolved r, double? cut, string mode = "dashed", string breakStyle = "zigzag", double breakSize = 0.1)
    {
        // Across the stair from its right to its left going up, the zigzag on the side the numbers leave free.
        var zig = NumbersOnLeft(r) ? 0.25 : 0.75;
        var hc = mode == "whole" ? null : cut;
        var lines = new List<StairPlanLine>();
        var walk = new List<StairPlanLine>();
        var breaks = new List<Vec2[]>();
        var (h, b) = (r.RiserHeight, r.Tread);
        bool Above(double z) => hc is { } c && z > c + 1e-9;

        // The flights the cut crosses: each its break line through the point of its centre line whose nosing line is at the cut.
        var planes = new (Vec2 P, Vec2 N)?[r.Flights.Count];
        var tan = Math.Tan(BreakAngle);
        for (var i = 0; i < r.Flights.Count; i++)
        {
            var fl = r.Flights[i];
            var run = (fl.Risers - 1) * b;
            if (run < 1e-9 || hc is not { } c || !(fl.Z0 + h <= c + 1e-9 && c < fl.Z0 + fl.Risers * h - 1e-9)) continue;
            var w = r.WidthOf(fl);
            // Kept within the flight, so the slanted line meets its two sides.
            var t = Math.Min(w / 2 * tan, run / 2);
            var u = Math.Clamp((c - fl.Z0 - h) * b / h, t, run - t);
            var e = (fl.Left + fl.Dir * tan).Normalized();
            var n = e.Perp;
            if (Vec2.Dot(n, fl.Dir) < 0) n = n * -1;
            var p = fl.Start + fl.Dir * u;
            planes[i] = (p, n);
            var half = w / 2 / Vec2.Dot(e, fl.Left);
            breaks.AddRange(Break(p - e * half, p + e * half, breakStyle, Math.Min(breakSize, w / 3), zig));
        }

        // A segment of a flight the cut crosses is split by its break line; any other is under or over the cut by its height.
        IEnumerable<(Vec2 A, Vec2 B, bool Above)> Split(Vec2 a, Vec2 c2, double za, double zb, int flight)
        {
            if (flight >= 0 && planes[flight] is var (p, n))
            {
                var (sa, sb) = (Vec2.Dot(a - p, n), Vec2.Dot(c2 - p, n));
                if ((sa <= 1e-9 && sb <= 1e-9) || (sa >= -1e-9 && sb >= -1e-9)) { yield return (a, c2, sa + sb > 0); yield break; }
                var m = a + (c2 - a) * (sa / (sa - sb));
                yield return (a, m, sa > 0);
                yield return (m, c2, sb > 0);
                yield break;
            }
            if (hc is { } c && Math.Abs(zb - za) > 1e-12 && (za - c) * (zb - c) < 0)
            {
                var m = a + (c2 - a) * ((c - za) / (zb - za));
                yield return (a, m, Above(za));
                yield return (m, c2, Above(zb));
                yield break;
            }
            yield return (a, c2, Above(Math.Max(za, zb)));
        }

        for (var i = 0; i < r.Flights.Count; i++)
        {
            var fl = r.Flights[i];
            var l = fl.Left * (r.WidthOf(fl) / 2);
            var run = (fl.Risers - 1) * b;
            // Each riser's line, the front of the tread it climbs to.
            for (var k = 1; k <= fl.Risers; k++)
            {
                var u = (k - 1) * b;
                var z = fl.Z0 + k * h;
                foreach (var s in Split(fl.Start + fl.Dir * u - l, fl.Start + fl.Dir * u + l, z, z, i)) lines.Add(new([s.A, s.B], false, s.Above));
            }
            if (run < 1e-9) continue;
            foreach (var side in new[] { -l, l })
                foreach (var s in Split(fl.Start + side, fl.Start + fl.Dir * run + side, fl.Z0 + h, fl.Z0 + fl.Risers * h, i)) lines.Add(new([s.A, s.B], false, s.Above));
        }
        // Landings and winders edge by edge, each edge once: an edge two of them share (or a riser's line) is seen when either
        // is under the cut, and a dashed edge is never drawn twice out of step.
        var drawn = new Dictionary<(long, long, long, long), int>();
        (long, long, long, long) Key(Vec2 a, Vec2 c2)
        {
            static long R(double v) => (long)Math.Round(v * 1e5);
            var (k1, k2) = ((R(a.X), R(a.Y)), (R(c2.X), R(c2.Y)));
            return k1.CompareTo(k2) <= 0 ? (k1.Item1, k1.Item2, k2.Item1, k2.Item2) : (k2.Item1, k2.Item2, k1.Item1, k1.Item2);
        }
        for (var i = 0; i < lines.Count; i++) if (lines[i].Points.Length == 2) drawn.TryAdd(Key(lines[i].Points[0], lines[i].Points[1]), i);
        foreach (var (outline, z) in r.Landings.Select(x => (x.Outline, x.Z)).Concat(r.Winders.Select(x => (x.Outline, x.Z))))
            for (var i = 0; i < outline.Length; i++)
            {
                var (a, c2) = (outline[i], outline[(i + 1) % outline.Length]);
                if ((c2 - a).Length < 1e-9) continue;
                var key = Key(a, c2);
                if (drawn.TryGetValue(key, out var at)) { if (!Above(z) && lines[at].Above) lines[at] = lines[at] with { Above = false }; continue; }
                drawn[key] = lines.Count;
                lines.Add(new([a, c2], false, Above(z)));
            }
        var anyBelow = lines.Any(l => !l.Above);
        if (r.Arc is { } arc0 && r.Spec.Shape == "spiral")
            lines.Add(new(Circle(arc0.Centre, Math.Max(0.03, Math.Min(0.08, arc0.Inner * 0.8)), 16), true, false));

        // The walking lines: one up the stair, or a split stair's one up each flight.
        var paths = new List<List<WalkSeg>>();
        if (r.Spec.Shape == "split" && r.Flights.Count == 3)
        {
            for (var i = 0; i < r.Flights.Count; i++)
            {
                var fl = r.Flights[i];
                var run = (fl.Risers - 1) * b;
                if (run < 1e-9) continue;
                var u0 = Math.Min(b / 2, run / 2);
                paths.Add([new(fl.Start + fl.Dir * u0, fl.Start + fl.Dir * run, fl.Z0 + h + u0 * h / b, fl.Z0 + fl.Risers * h, i)]);
            }
        }
        else
        {
            var segs = new List<WalkSeg>();
            Vec2? last = null;
            var lastZ = 0.0;
            for (var i = 0; i < r.Flights.Count; i++)
            {
                var fl = r.Flights[i];
                var run = (fl.Risers - 1) * b;
                // A flight with no tread (a riser between two winding corners): the walking line passes over its riser.
                if (run < 1e-9)
                {
                    if (last is { } lp) { segs.Add(new(lp, fl.Start, lastZ, fl.Z0, -1)); last = fl.Start; lastZ = fl.Z0 + h; }
                    continue;
                }
                var u0 = last == null ? Math.Min(b / 2, run / 2) : 0;
                var a = fl.Start + fl.Dir * u0;
                // Through a turn, from the end of one flight to the start of the next, over the landing (level) or the winders (climbing).
                if (last is { } prev) segs.Add(new(prev, a, lastZ, fl.Z0, -1));
                var end = fl.Start + fl.Dir * run;
                segs.Add(new(a, end, fl.Z0 + h + u0 * h / b, fl.Z0 + fl.Risers * h, i));
                (last, lastZ) = (end, fl.Z0 + fl.Risers * h);
            }
            if (r.Arc is { } arc)
            {
                // Along the walking line, from half a tread in to the top, its height on the nosings' line.
                var a0 = arc.A0 + arc.Step / 2;
                var a1 = arc.Angle(arc.Steps);
                var m = Math.Max(4, (int)Math.Ceiling(Math.Abs(a1 - a0) / (Math.PI / 36)));
                double Z(double ang) => h + (ang - arc.A0) / arc.Step * h;
                for (var j = 0; j < m; j++)
                {
                    var (p, q) = (a0 + (a1 - a0) * j / m, a0 + (a1 - a0) * (j + 1) / m);
                    segs.Add(new(arc.At(p, arc.Walk), arc.At(q, arc.Walk), Z(p), Z(q), -1));
                }
            }
            if (segs.Count > 0) paths.Add(segs);
        }

        var hidden = mode == "hidden";
        var arrowSize = Math.Min(0.25, r.Width * 0.25);
        var crossed = planes.Any(x => x != null);
        foreach (var path in paths)
        {
            var pieces = path.SelectMany(s => Split(s.A, s.B, s.ZA, s.ZB, s.Flight)).Where(s => (s.B - s.A).Length > 1e-9).ToList();
            if (pieces.Count == 0) continue;
            // Joined into polylines of one kind, so a dashed line's dashes run on round its corners.
            var run = new List<Vec2> { pieces[0].A, pieces[0].B };
            var above = pieces[0].Above;
            for (var i = 1; i < pieces.Count; i++)
            {
                if (pieces[i].Above == above && (pieces[i].A - run[^1]).Length < 1e-9) { run.Add(pieces[i].B); continue; }
                walk.Add(new([.. run], false, above));
                (run, above) = ([pieces[i].A, pieces[i].B], pieces[i].Above);
            }
            walk.Add(new([.. run], false, above));
            // The arrow at the top; at the break when what is over the cut is not drawn.
            var head = hidden ? pieces.LastOrDefault(x => !x.Above) : pieces[^1];
            if ((head.B - head.A).Length > 1e-9)
            {
                var d = (head.B - head.A).Normalized();
                var tip = head.B;
                walk.Add(new([tip - d * arrowSize + d.Perp * (arrowSize * 0.5), tip, tip - d * arrowSize - d.Perp * (arrowSize * 0.5)], false, head.Above));
            }
            // A dot at the foot of the one walking line.
            if (paths.Count == 1)
            {
                var foot = pieces[0].A;
                walk.Add(new([foot + new Vec2(0.04, 0), foot + new Vec2(0, 0.04), foot - new Vec2(0.04, 0), foot - new Vec2(0, 0.04)], true, pieces[0].Above));
            }
            // No flight crossed: the cut falls in a turn of winders, on a spiral, or at a flight's first riser (a split stair's
            // return flights over its landing); the break goes square across the walking line where it passes from under the
            // cut to over it.
            if (!crossed && anyBelow)
                for (var i = 0; i < pieces.Count; i++)
                {
                    if (!pieces[i].Above || (i > 0 && pieces[i - 1].Above)) continue;
                    var p = pieces[i].A;
                    var d = (pieces[i].B - pieces[i].A).Normalized();
                    // A walking line of one flight (a split stair's) starts half a tread in: its break on the flight's first riser.
                    var own = path.Count == 1 && path[0].Flight >= 0 ? r.Flights[path[0].Flight] : null;
                    var width = own != null ? r.WidthOf(own) : r.Width;
                    if (own != null && i == 0) p = own.Start;
                    if (r.Arc is { } a && path.All(s => s.Flight < 0) && r.Flights.Count == 0)
                    {
                        var radial = (p - a.Centre).Normalized();
                        breaks.AddRange(Break(a.Centre + radial * a.Inner, a.Centre + radial * a.Outer, breakStyle, Math.Min(breakSize, r.Width / 3), 0.25));
                    }
                    else breaks.AddRange(Break(p - d.Perp * (width / 2), p + d.Perp * (width / 2), breakStyle, Math.Min(breakSize, width / 3), zig));
                    break;
                }
        }
        if (hidden)
        {
            lines.RemoveAll(x => x.Above);
            walk.RemoveAll(x => x.Above);
        }
        return new StairPlanSymbol(lines, walk, breaks, paths.Select(p => p.Count == 0 ? [] : p.Select(s => s.A).Append(p[^1].B).ToArray()).ToList());
    }

    /// <summary>
    /// A break line from <paramref name="a"/> to <paramref name="b"/>: one line with a zigzag of <paramref name="size"/>
    /// <paramref name="at"/> that fraction of the way along it (clear of the walking line in the middle), or two parallel lines.
    /// </summary>
    public static IEnumerable<Vec2[]> Break(Vec2 a, Vec2 b, string style, double size, double at = 0.5)
    {
        var length = (b - a).Length;
        if (length < 1e-9) yield break;
        var d = (b - a) * (1 / length);
        var n = d.Perp;
        if (style == "double")
        {
            var g = Math.Min(size / 4, length / 8);
            yield return [a - n * g, b - n * g];
            yield return [a + n * g, b + n * g];
            yield break;
        }
        var s = Math.Min(size, length / 3);
        var m = a + (b - a) * Math.Clamp(at, s / 2 / length + 1e-6, 1 - s / 2 / length - 1e-6);
        yield return [a, m - d * (s / 2), m - d * (s / 6) + n * (s / 3), m + d * (s / 6) - n * (s / 3), m + d * (s / 2), b];
    }

    static Vec2[] Circle(Vec2 c, double r, int n) => Enumerable.Range(0, n).Select(k => c + new Vec2(Math.Cos(2 * Math.PI * k / n), Math.Sin(2 * Math.PI * k / n)) * r).ToArray();
}
