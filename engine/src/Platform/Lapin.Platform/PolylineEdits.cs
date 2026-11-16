using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A stretch of a polyline element's line (<see cref="IPolylinePieces"/>): its points and each segment's rise (an arc's, left
/// positive), and for each point, which of the element's own points it is (null where it is a cut, or an end carried on);
/// <see cref="S0"/> and <see cref="S1"/>: where it began and ended along the element's line as it was.
/// </summary>
public sealed record PolySlice(List<Vec2> Points, double[] Rises, int?[] From, double S0, double S1)
{
    public bool Curved => Rises.Any(r => Math.Abs(r) > 1e-9);
}

/// <summary>
/// An element along a polyline of points and arcs (a railing, an MEP run, a road or a path) in the platform's split, trim,
/// extend and offset (<see cref="PolylineEdits"/>, audit 2026-11-15 §15): how the module reads its line, writes a stretch of
/// it back, and keeps right what hangs on it (a run's connections and branches, a corridor's stations).
/// </summary>
public interface IPolylinePieces
{
    bool Handles(Entity e);

    /// <summary>Why the element cannot be cut or carried on this way; null when it can.</summary>
    string? Refuse(IModelView m, Entity e) => null;

    /// <summary>Its line: the points, and one rise per segment (0 straight).</summary>
    (List<Vec2> Points, double[] Rises) Line(Entity e);

    /// <summary>
    /// The attributes of the stretch <paramref name="slice"/> of <paramref name="e"/>, written into <paramref name="attrs"/> (a
    /// copy of the element's own): its points and arcs, and what goes by point or by station along it.
    /// <paramref name="newStart"/>, <paramref name="newEnd"/>: that end of the stretch is a cut, not one of the element's ends.
    /// </summary>
    void Piece(IModelView m, Entity e, JsonObject attrs, PolySlice slice, bool newStart, bool newEnd);

    /// <summary>What hangs on <paramref name="e"/> (as it was), put on the pieces once they are written (each piece's stretch along the line as it was).</summary>
    void Rehost(ICommandContext ctx, Entity e, IReadOnlyList<PathPiece> pieces, JsonObject args) { }
}

/// <summary>
/// Split, Trim/Extend and Offset (<see cref="PathEdits"/>' commands) on elements along a polyline with arcs rather than one
/// axis: the line is measured along its segments, an end carried on runs on along its end segment (an arc round its circle),
/// and the offset copy is the line moved square to each segment (arcs concentric), its corners where the moved segments meet.
/// </summary>
public static class PolylineEdits
{
    public static IPolylinePieces? Hook(ICommandContext ctx, Entity e) => ctx.Services.All<IPolylinePieces>().FirstOrDefault(h => h.Handles(e));

    /// <summary>The line's segments, and where each begins along it.</summary>
    public sealed class Line
    {
        public readonly List<Vec2> Points;
        public readonly double[] Rises;
        public readonly PathCurve[] Segs;
        public readonly double[] Starts;
        public double Length => Starts[^1];

        public Line(IReadOnlyList<Vec2> pts, IReadOnlyList<double>? rises)
        {
            if (pts.Count < 2) throw new CommandException("The element has no line: two points at least.");
            Points = [.. pts];
            Rises = [.. Enumerable.Range(0, pts.Count - 1).Select(i => rises != null && i < rises.Count ? rises[i] : 0)];
            Segs = [.. Enumerable.Range(0, pts.Count - 1).Select(i => new PathCurve(pts[i], pts[i + 1], Rises[i]))];
            Starts = new double[Segs.Length + 1];
            for (var i = 0; i < Segs.Length; i++) Starts[i + 1] = Starts[i] + Segs[i].Length;
        }

        /// <summary>The segment a station lies on (below 0 the first, past the end the last).</summary>
        public int SegmentAt(double s)
        {
            for (var i = 0; i < Segs.Length; i++) if (s < Starts[i + 1]) return i;
            return Segs.Length - 1;
        }

        /// <summary>The point at a station; past an end, run on along the end segment (an arc round its circle).</summary>
        public Vec2 At(double s)
        {
            var i = SegmentAt(s);
            return PathEdits.OnAxis(Segs[i], s - Starts[i]);
        }

        /// <summary>The station of the point of the line nearest <paramref name="p"/>.</summary>
        public double Along(Vec2 p)
        {
            var best = (D: double.MaxValue, S: 0.0);
            for (var i = 0; i < Segs.Length; i++)
            {
                var c = Segs[i];
                var local = Math.Clamp(c.IsArc ? PathEdits.Along(c, p) : c.Project(p), 0, c.Length);
                var d = (c.PointAt(local) - p).Length;
                if (d < best.D - 1e-12) best = (d, Starts[i] + local);
            }
            return best.S;
        }

        /// <summary>Where a line or a circle crosses the line, as stations; the end segments run on past the ends.</summary>
        public List<double> Crossings(PathEdits.Curve b)
        {
            var o = new List<double>();
            for (var i = 0; i < Segs.Length; i++)
            {
                var c = Segs[i];
                foreach (var p in PathEdits.Cross(PathEdits.Curve.Of(c), b))
                {
                    var local = c.IsArc ? PathEdits.Along(c, p) : c.Project(p);
                    var lo = i == 0 ? double.NegativeInfinity : -1e-9;
                    var hi = i == Segs.Length - 1 ? double.PositiveInfinity : c.Length + 1e-9;
                    if (local < lo || local > hi) continue;
                    var s = Starts[i] + local;
                    if (!o.Any(x => Math.Abs(x - s) < 1e-7)) o.Add(s);
                }
            }
            o.Sort();
            return o;
        }

        /// <summary>The stretch from <paramref name="s0"/> to <paramref name="s1"/> (either may lie past an end).</summary>
        public PolySlice Slice(double s0, double s1)
        {
            const double eps = 1e-7;
            var pts = new List<Vec2>();
            var from = new List<int?>();
            int? Vertex(double s) { for (var j = 0; j < Points.Count; j++) if (Math.Abs(Starts[j] - s) < eps) return j; return null; }
            pts.Add(At(s0)); from.Add(Vertex(s0));
            for (var j = 1; j < Points.Count - 1; j++)
                if (Starts[j] > s0 + eps && Starts[j] < s1 - eps) { pts.Add(Points[j]); from.Add(j); }
            pts.Add(At(s1)); from.Add(Vertex(s1));
            // Each new segment lies on one of the line's: the one its middle is on.
            var rises = new double[pts.Count - 1];
            var stations = new List<double> { s0 };
            for (var k = 1; k < pts.Count - 1; k++) stations.Add(Starts[from[k]!.Value]);
            stations.Add(s1);
            for (var k = 0; k < rises.Length; k++)
            {
                var mid = (stations[k] + stations[k + 1]) / 2;
                var i = SegmentAt(mid);
                if (!Segs[i].IsArc) continue;
                if (stations[k + 1] - stations[k] >= 2 * Math.PI * Segs[i].Radius - 1e-3) throw new CommandException("The arc would close round on itself.");
                rises[k] = Math.Round(PathCurve.RiseThrough(pts[k], pts[k + 1], At(mid)), 9);
            }
            return new PolySlice(pts, rises, [.. from], s0, s1);
        }

        /// <summary>The line moved <paramref name="y"/> to its left: each segment moved square (an arc concentric), the corners where they meet.</summary>
        public PolySlice Offset(double y)
        {
            var n = Points.Count;
            var curves = Segs.Select(c => PathEdits.Curve.Of(c, y)).ToList();
            var pts = new List<Vec2> { Segs[0].At(0, y) };
            for (var j = 1; j < n - 1; j++)
            {
                var guess = Segs[j].At(0, y);
                var hits = PathEdits.Cross(curves[j - 1], curves[j]);
                pts.Add(hits.Count == 0 ? guess : hits.MinBy(h => (h - guess).Length));
            }
            pts.Add(Segs[^1].At(Segs[^1].Length, y));
            var rises = new double[n - 1];
            for (var i = 0; i < n - 1; i++)
            {
                var (p, q) = (pts[i], pts[i + 1]);
                if ((q - p).Length < 1e-6) throw new CommandException("The offset is too large: a segment of the copy would vanish.");
                var c = Segs[i];
                if (!c.IsArc)
                {
                    if (Vec2.Dot(q - p, c.End - c.Start) <= 0) throw new CommandException("The offset is too large: a segment of the copy would turn back.");
                    continue;
                }
                // The middle of the moved arc, between its new ends, the way the arc runs.
                var centre = c.Centre;
                var sense = c.Rise > 0 ? -1 : 1;
                var (ap, aq) = (Math.Atan2(p.Y - centre.Y, p.X - centre.X), Math.Atan2(q.Y - centre.Y, q.X - centre.X));
                var turned = sense * (aq - ap);
                turned -= 2 * Math.PI * Math.Floor(turned / (2 * Math.PI));
                var am = ap + sense * turned / 2;
                var r = (p - centre).Length;
                rises[i] = Math.Round(PathCurve.RiseThrough(p, q, centre + new Vec2(Math.Cos(am), Math.Sin(am)) * r), 9);
            }
            return new PolySlice(pts, rises, [.. Enumerable.Range(0, n).Select(i => (int?)i)], 0, Length);
        }
    }

    static Entity Check(ICommandContext ctx, Entity e, IPolylinePieces h)
    {
        if (StoredBodies.Has(e) || e.Attrs["reference"] != null) throw new CommandException($"„{e.Name}” is a reference from a file: convert it to native first.");
        Pins.Refuse([e]);
        if (h.Refuse(ctx.Model, e) is { } why) throw new CommandException(why);
        return e;
    }

    static Line LineOf(IPolylinePieces h, Entity e) { var (p, r) = h.Line(e); return new Line(p, r); }

    /// <summary>The crossing named by <c>with</c> (an element's axis), <c>line</c> or <c>arc</c>.</summary>
    static PathEdits.Curve Crossing(ICommandContext ctx, JsonObject a, Entity e, string key, Vec2 keep) => PathEdits.BoundaryOf(ctx, a, e, key, keep);

    public static JsonObject Split(ICommandContext ctx, JsonObject a, Entity e, IPolylinePieces h)
    {
        Check(ctx, e, h);
        var line = LineOf(h, e);
        var length = line.Length;
        var at = Args.TryPoint(a["at"]);
        double s;
        if (a["with"] != null || a["line"] != null || a["arc"] != null)
        {
            var hits = line.Crossings(Crossing(ctx, a, e, "with", at ?? line.At(length / 2))).Where(x => x > 1e-6 && x < length - 1e-6).ToList();
            if (hits.Count == 0) throw new CommandException($"That does not cross „{e.Name}”.");
            s = at is { } pick ? hits.MinBy(x => Math.Abs(x - line.Along(pick))) : hits.MinBy(x => Math.Abs(x - length / 2));
        }
        else s = line.Along(at ?? throw new CommandException("Where? A point on the element."));
        if (s < PathEdits.MinLength || s > length - PathEdits.MinLength)
            throw new CommandException($"Split „{e.Name}” at least {PathEdits.MinLength * 1000:0} mm from its ends: each part needs a length.");

        var first = e.CloneAttrs();
        h.Piece(ctx.Model, e, first, line.Slice(0, s), false, true);
        var secondAttrs = e.CloneAttrs();
        h.Piece(ctx.Model, e, secondAttrs, line.Slice(s, length), true, false);
        ctx.Replace(e.With(attrs: first));
        var second = PathEdits.AddLike(ctx, e, secondAttrs);
        h.Rehost(ctx, e, [new PathPiece(e.Id, 0, s), new PathPiece(second.Id, s, length)], a);
        var p = line.At(s);
        return new JsonObject { ["ids"] = new JsonArray(e.Id, second.Id), ["at"] = new JsonArray(Math.Round(p.X, 9), Math.Round(p.Y, 9)) };
    }

    public static JsonObject TrimExtend(ICommandContext ctx, JsonObject a, Entity e, IPolylinePieces h)
    {
        Check(ctx, e, h);
        var line = LineOf(h, e);
        var length = line.Length;
        var pick = Args.TryPoint(a["at"]) ?? line.At(length / 2);
        var kept = line.Along(pick);
        var hits = line.Crossings(PathEdits.BoundaryOf(ctx, a, e, "boundary", pick));
        if (hits.Count == 0) throw new CommandException($"„{e.Name}” never meets the boundary, carried on as it runs.");
        double Off(double x) => x < 0 ? -x : x > length ? x - length : 0;
        var sx = hits.OrderBy(Off).ThenBy(x => Math.Abs(x - kept)).First();
        var (s0, s1) = sx <= 1e-9 ? (sx, length)
            : sx >= length - 1e-9 ? (0, sx)
            : kept <= sx ? (0, sx) : (sx, length);
        if (Math.Abs(s0) < 1e-9 && Math.Abs(s1 - length) < 1e-9) throw new CommandException($"„{e.Name}” already ends at the boundary.");
        if (s1 - s0 < PathEdits.MinLength) throw new CommandException($"„{e.Name}” would be left shorter than {PathEdits.MinLength * 1000:0} mm: pick it on the part to keep.");
        var attrs = e.CloneAttrs();
        h.Piece(ctx.Model, e, attrs, line.Slice(s0, s1), false, false);
        ctx.Replace(e.With(attrs: attrs));
        h.Rehost(ctx, e, [new PathPiece(e.Id, s0, s1)], a);
        return new JsonObject { ["id"] = e.Id };
    }

    public static JsonObject Offset(ICommandContext ctx, JsonObject a, Entity e, IPolylinePieces h)
    {
        if (StoredBodies.Has(e) || e.Attrs["reference"] != null) throw new CommandException($"„{e.Name}” is a reference from a file: convert it to native first.");
        var line = LineOf(h, e);
        var side = Args.TryPoint(a["side"]) ?? throw new CommandException("Which side? A point on it.");
        var foot = line.Along(side);
        var k = line.SegmentAt(Math.Min(foot, line.Length - 1e-9));
        var seg = line.Segs[k];
        var local = foot - line.Starts[k];
        var across = Vec2.Cross(seg.TangentAt(local), side - seg.PointAt(local));
        if (Math.Abs(across) < 1e-9) throw new CommandException("Pick a point off the line: the side the copy goes.");
        var through = a["through"] is JsonValue tv && tv.TryGetValue<bool>(out var tb) && tb;
        double shift;
        if (through) shift = Math.Abs(across);
        else
        {
            var d = Json.Number(a["distance"]) ?? throw new CommandException("Offset by how much?");
            if (!double.IsFinite(d) || d <= 1e-9) throw new CommandException("The offset distance must be positive.");
            shift = d;
        }
        var slice = line.Offset(Math.Sign(across) * shift);
        var copyId = Placement.Copy(ctx, e, Placement2D.Move(new Vec2(0, 0)));
        var copy = ctx.Model.Get(copyId)!;
        var attrs = copy.CloneAttrs();
        h.Piece(ctx.Model, e, attrs, slice, false, false);
        ctx.Replace(copy.With(attrs: attrs));
        return new JsonObject { ["id"] = copyId };
    }
}
