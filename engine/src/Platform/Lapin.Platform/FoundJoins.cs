using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Joins between path elements read from a file (docs/spec-ifc.md, Recunoașterea, Îmbinările): where two recognised
/// elements meet, the join is the one that leaves both as the file has them. Each free end is tried against the elements
/// it may meet (nearest first), as a mitre, a butt with either through, a T or in line, and a join is made only when the
/// ends it shapes land on the file's footprints within <c>tolerance</c> (both corners of each end, and nothing more of the
/// footprint there). Where none does (a corner the file overlaps, notches, a gap, a corner the file mitres at 45° between
/// elements of unequal thickness), the ends stay free where the file's body ends, cut at the file's angle there when the
/// end is one straight line (<see cref="Joins.OwnEnd"/>), square otherwise: a join never runs an element into a neighbour
/// that stays whole, nor mitres what the file butts.
/// Crossings are not joined (the file's two bodies overlap there, and an X would take one out of the other). Elements
/// drawn in Lapin keep <see cref="Joins.Auto"/>.
/// </summary>
public static class FoundJoins
{
    /// <summary>How the joins went: made, and the free ends brought to where the file's body ends.</summary>
    public sealed record Result(int Joined, int Trimmed);

    static readonly (JoinStyle Style, char? Priority)[] Options = [(JoinStyle.Mitre, null), (JoinStyle.Butt, 'a'), (JoinStyle.Butt, 'b')];

    /// <summary>
    /// Joins the elements <paramref name="ids"/> among themselves as their footprints say (an element without one keeps
    /// its own outline as it came), then brings each end left free to the end of its footprint.
    /// </summary>
    /// <remarks>
    /// <paramref name="connections"/>: the file's own (IfcRelConnectsPathElements); an end the file connects to an element
    /// tries that element first, whatever its distance, and is still made only when the footprints say so.
    /// </remarks>
    public static Result Make(IEdit edit, IPathElements paths, IReadOnlyList<string> ids, IReadOnlyDictionary<string, IReadOnlyList<Vec2>> footprints, double tolerance = 1e-3,
        IReadOnlyList<ForeignConnection>? connections = null)
    {
        // Each end the file connects, and to what.
        var told = new HashSet<(string, Connection, string)>();
        foreach (var c in connections ?? [])
        {
            told.Add((c.Relating, c.RelatingAt, c.Related));
            told.Add((c.Related, c.RelatedAt, c.Relating));
        }
        var m = edit.Model;
        var set = ids.Where(id => m.Get(id) is { } e && paths.IsPath(e.Class) && !PathCurve.Of(e).IsArc).ToList();
        if (set.Count == 0) return new(0, 0);
        // Each element's footprint, and its storey: only elements of one storey and one class join.
        var prints = new Dictionary<string, IReadOnlyList<Vec2>>();
        var storeyOf = new Dictionary<string, string?>();
        foreach (var id in set)
        {
            var e = m.Get(id)!;
            storeyOf[id] = Spatial.StoreyOf(m, id)?.Id;
            if (footprints.TryGetValue(id, out var fp) && fp.Count >= 3) prints[id] = fp;
            else if (paths.FrameOf(e, m) is { } f) prints[id] = Corners(f, WallJoins.Outline(f));
        }
        var byStorey = set.Where(prints.ContainsKey).GroupBy(id => storeyOf[id] ?? "").ToDictionary(g => g.Key, g => g.ToList());
        var joined = 0;
        foreach (var id in set)
        {
            if (!prints.ContainsKey(id)) continue;
            foreach (var end in new[] { Connection.AtStart, Connection.AtEnd })
            {
                var e = m.Get(id)!;
                if (Taken(m, e, end)) continue;
                if (paths.FrameOf(e, m) is not { } fe) continue;
                var tip = end == Connection.AtStart ? fe.Start : fe.End;
                var others = byStorey[storeyOf[id] ?? ""]
                    .Where(o => o != id && m.Get(o) is { } x && x.Class == e.Class && Joins.Between(m, id, o) == null)
                    .Select(o => (Id: o, Frame: paths.FrameOf(m.Get(o)!, m)))
                    .Where(o => o.Frame != null && (told.Contains((id, end, o.Id)) || Distance(o.Frame!, tip) <= 2 * Math.Max(fe.Thickness, o.Frame!.Thickness) + tolerance))
                    .OrderBy(o => told.Contains((id, end, o.Id)) ? 0 : 1).ThenBy(o => Distance(o.Frame!, tip));
                foreach (var (oid, fo) in others)
                {
                    if (Try(edit, paths, e, fe, end, m.Get(oid)!, fo!, prints, tolerance)) { joined++; break; }
                }
            }
        }
        // The ends left free: where the file's body ends, cut at its angle there (square when it is).
        var trimmed = 0;
        foreach (var id in set)
        {
            if (!prints.TryGetValue(id, out var fp) || m.Get(id) is not { } e || paths.FrameOf(e, m) is not { } f) continue;
            var (start, end) = (f.Start, f.End);
            var dir = (end - start).Normalized();
            var length = (end - start).Length;
            var moved = false;
            var angles = new double?[2];
            foreach (var at in new[] { Connection.AtStart, Connection.AtEnd })
            {
                if (Taken(m, e, at) || FootprintEnd(f, at, fp, tolerance) is not var (s, angle)) continue;
                var i = at == Connection.AtStart ? 0 : 1;
                if (Math.Abs(angle - (e.NumOrNull(i == 0 ? Joins.StartAngle : Joins.EndAngle) ?? 0)) > 1e-6) { angles[i] = angle; moved = true; }
                var current = i == 0 ? 0 : length;
                if (Math.Abs(s - current) <= tolerance) continue;
                if (i == 0) start = f.Start + dir * s; else end = f.Start + dir * s;
                moved = true;
            }
            if (!moved || (end - start).Length < 1e-3 || Vec2.Dot(end - start, dir) <= 0) continue;
            var attrs = e.CloneAttrs();
            attrs["start"] = new System.Text.Json.Nodes.JsonArray(start.X, start.Y);
            attrs["end"] = new System.Text.Json.Nodes.JsonArray(end.X, end.Y);
            for (var i = 0; i < 2; i++)
                if (angles[i] is { } a)
                {
                    var key = i == 0 ? Joins.StartAngle : Joins.EndAngle;
                    if (Math.Abs(a) < 1e-6) attrs.Remove(key); else attrs[key] = a;
                }
            edit.Replace(e.With(attrs: attrs));
            trimmed++;
        }
        return new(joined, trimmed);
    }

    /// <summary>Joins <paramref name="e"/>'s end to <paramref name="other"/> the first way that leaves both as their footprints say; false when none does.</summary>
    static bool Try(IEdit edit, IPathElements paths, Entity e, JoinWall fe, Connection end, Entity other, JoinWall fo, Dictionary<string, IReadOnlyList<Vec2>> prints, double tolerance)
    {
        var tried = new HashSet<JoinKind>();
        foreach (var (style, priority) in Options)
        {
            WallJoin trial;
            try { trial = WallJoins.Compute(fe, fo, new JoinOptions(Priority: priority, Style: style)); }
            catch (CommandException) { continue; }
            // A T or an in-line join is the same whatever the style: tried once.
            if (trial.Kind is JoinKind.T or JoinKind.Butt && !tried.Add(trial.Kind)) continue;
            if (trial.Kind == JoinKind.X || trial.A.Connection != end) return false;
            if (trial.B.Connection != Connection.AtPath && Taken(edit.Model, other, trial.B.Connection)) return false;
            if (!Matches(fe, trial.A.Wall, end, prints[e.Id], tolerance)) continue;
            if (trial.B.Connection != Connection.AtPath && !Matches(fo, trial.B.Wall, trial.B.Connection, prints[other.Id], tolerance)) continue;
            Joins.Join(edit, paths, e, other, trial.Kind == JoinKind.L ? trial.Style : JoinStyle.Butt, trial.Relating);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Whether the end a join leaves (<paramref name="after"/>) is the footprint's there: its two corners on footprint
    /// vertices, and the footprint's vertices on that half of the element (as it was, <paramref name="before"/>) on them.
    /// </summary>
    static bool Matches(JoinWall before, JoinWall after, Connection end, IReadOnlyList<Vec2> footprint, double tolerance)
    {
        Vec2[] mine;
        try
        {
            var corners = Corners(after, WallJoins.Outline(after));
            mine = end == Connection.AtStart ? [corners[0], corners[3]] : [corners[1], corners[2]];
        }
        catch (CommandException) { return false; }
        var theirs = Half(before, end, footprint);
        if (theirs.Count == 0) return false;
        return mine.All(p => theirs.Any(q => (p - q).Length <= tolerance)) && theirs.All(q => mine.Any(p => (p - q).Length <= tolerance));
    }

    /// <summary>The footprint's vertices on the half of the element at <paramref name="end"/>.</summary>
    static List<Vec2> Half(JoinWall w, Connection end, IReadOnlyList<Vec2> footprint)
    {
        var dir = (w.End - w.Start).Normalized();
        var half = (w.End - w.Start).Length / 2;
        return [.. footprint.Where(p => end == Connection.AtStart ? Vec2.Dot(p - w.Start, dir) < half : Vec2.Dot(p - w.Start, dir) >= half)];
    }

    /// <summary>
    /// Where along the element (from its start) its footprint ends at <paramref name="end"/>, and at what angle: the end's
    /// line between where its two faces end there, crossing the axis at <c>At</c>, turned <c>Angle</c> degrees from square
    /// (as <see cref="Joins.OwnEnd"/> reads it; 0 when square within <paramref name="tolerance"/>, or when the end is more
    /// than that line: a notch, a step, or steeper than <see cref="Joins.MaxEndAngle"/>, which stay square halfway between
    /// the faces' ends). Null when the footprint's end is not two corners on the faces.
    /// </summary>
    static (double At, double Angle)? FootprintEnd(JoinWall w, Connection end, IReadOnlyList<Vec2> footprint, double tolerance)
    {
        var dir = (w.End - w.Start).Normalized();
        var left = dir.Perp;
        var half = Half(w, end, footprint);
        double? Face(double y)
        {
            var on = half.Where(p => Math.Abs(Vec2.Dot(p - w.Start, left) - y) <= 2 * tolerance).Select(p => Vec2.Dot(p - w.Start, dir)).ToList();
            return on.Count == 0 ? null : end == Connection.AtStart ? on.Min() : on.Max();
        }
        if (Face(w.YMin) is not { } a || Face(w.YMax) is not { } b) return null;
        var t = w.YMax - w.YMin;
        var square = ((a + b) / 2, 0d);
        if (Math.Abs(b - a) <= tolerance || t < 1e-6) return square;
        // An end that is one straight line from face to face: every vertex there on a face or on that line.
        var sign = end == Connection.AtStart ? -1 : 1;
        bool Off(Vec2 p)
        {
            var (x, y) = (Vec2.Dot(p - w.Start, dir), Vec2.Dot(p - w.Start, left));
            if (Math.Abs(y - w.YMin) <= 2 * tolerance || Math.Abs(y - w.YMax) <= 2 * tolerance) return false;
            return Math.Abs(x - (a + (b - a) * (y - w.YMin) / t)) > tolerance;
        }
        var angle = Math.Atan(sign * (b - a) / t) * 180 / Math.PI;
        if (half.Any(Off) || Math.Abs(angle) > Joins.MaxEndAngle) return square;
        return (a + (b - a) * (0 - w.YMin) / t, Math.Round(angle, 6));
    }

    /// <summary>An outline's corners in the plan (right-start, right-end, left-end, left-start).</summary>
    static Vec2[] Corners(JoinWall w, WallOutline o)
    {
        var dir = (w.End - w.Start).Normalized();
        var left = dir.Perp;
        return [.. o.Corners.Select(c => w.Start + dir * c.X + left * c.Y)];
    }

    /// <summary>Whether a join already takes this end of the element.</summary>
    static bool Taken(IModelView m, Entity e, Connection end)
    {
        var name = end == Connection.AtStart ? "ATSTART" : "ATEND";
        return Joins.Of(m, e.Id).Any(r => (string?)r.Attrs?[r.Relating == e.Id ? "relatingConnection" : "relatedConnection"] == name);
    }

    static double Distance(JoinWall f, Vec2 p)
    {
        var d = f.End - f.Start;
        var t = Math.Clamp(Vec2.Dot(p - f.Start, d) / Math.Max(Vec2.Dot(d, d), 1e-12), 0, 1);
        return (f.Start + d * t - p).Length;
    }
}
