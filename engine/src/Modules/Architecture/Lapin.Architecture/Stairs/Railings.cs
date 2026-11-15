using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Stairs;

/// <summary>
/// Railings: a stair's along its sides, and one of its own (testers' request, decision 238) drawn as a line in plan along a
/// balcony's edge, a terrace or a gallery: a <c>LapinRailing</c> exported as an IfcRailing (GUARDRAIL) standing on its storey (plus its base offset),
/// in the stair railings' designs: posts and a handrail ("bar"), balusters, glass panes, cables or a solid panel, with its
/// height, handrail profile and sizes and post spacing. Both are built by <see cref="Solids"/> from the line under them.
/// </summary>
public static class Railings
{
    /// <summary>Lapin's own class (Studio owns IfcRailing for its solids), exported as IfcRailing.</summary>
    public const string Class = "LapinRailing";

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;

    /// <summary>
    /// A railing's solids along a line (its foot, point by point; it may climb), in its style: the handrail (square or round)
    /// on top, then posts at their spacing (glass and cables between them), balusters at most their spacing apart, or a solid
    /// panel; glass apart, to be drawn as glass. On a stair the line runs along the nosings, over the treads: <paramref name="ground"/>
    /// gives the top of what lies under a post's footprint (its centre, its half size), so each post stands on the tread or landing
    /// under it and still reaches the handrail; without it (or where it finds nothing) a post stands on the line.
    /// </summary>
    /// <param name="pieceOf">
    /// For each straight piece of <paramref name="path"/> (from point i to i + 1), the segment of the line it was drawn as that it
    /// lies on: a railing's arc laid in chords by the one rule is one segment, its posts and balusters spaced along it whole (at
    /// most their spacing apart, as along a straight one), not chord by chord. None: each piece a segment of its own.
    /// </param>
    public static (List<Solid> Metal, List<Solid> Glass) Solids(IReadOnlyList<Vec3> path, StairRailing rail, Func<Vec3, double, double?>? ground = null, IReadOnlyList<int>? pieceOf = null)
    {
        var metal = Bars(path, rail);
        var glass = new List<Solid>();
        var height = Math.Max(0.3, rail.Height);
        Vec3 Up(Vec3 p, double up) => new(p.X, p.Y, p.Z + up);
        for (var i = 0; i + 1 < path.Count; i++)
        {
            var (a, b) = (path[i], path[i + 1]);
            var len = (b.Xy - a.Xy).Length;
            // An upright step of the handrail (where a flight's rail meets a landing's, forum topic 104): the bar only.
            if (len < 1e-6) continue;
            var across = b - a;
            var flat = new Vec3(-across.Y, across.X, 0);
            flat = flat * (1 / Math.Max(flat.Length, 1e-9));
            Solid? Pane(double from, double to, double thick, string tag)
            {
                List<Vec3> ring = [Up(a, from), Up(b, from), Up(b, to), Up(a, to)];
                var shift = flat * (-thick / 2);
                return Prism.Extrude(ring.Select(q => q + shift).ToList(), null, flat * thick, tag);
            }
            // The segment this piece lies on: its posts are placed once, at its first piece, along all of its pieces.
            var first = pieceOf == null || i == 0 || pieceOf[i] != pieceOf[i - 1];
            var upTo = i + 1;
            if (pieceOf != null) while (upTo + 1 < path.Count && pieceOf[upTo] == pieceOf[i]) upTo++;
            void Posts(double spacing, double size, string tag, bool last)
            {
                if (!first) return;
                List<Vec3> run = pieceOf == null ? [a, b] : [.. path.Skip(i).Take(upTo - i + 1)];
                var lens = run.Zip(run.Skip(1), (p, q) => (q.Xy - p.Xy).Length).ToList();
                var total = lens.Sum();
                var count = Math.Max(1, (int)Math.Ceiling(total / Math.Max(spacing, 0.05)));
                for (var j = 0; j <= count; j++)
                {
                    if (j == count && !last) continue;
                    var on = At(run, lens, total * j / count, total);
                    var top = on.Z + height;
                    var foot = Math.Min(ground?.Invoke(on, size / 2) ?? on.Z, top - 0.05);
                    if (Prism.Box(new Vec3(on.X, on.Y, (foot + top) / 2), size, size, top - foot, tag) is { } post) metal.Add(post);
                }
            }
            var lastSegment = upTo + 1 >= path.Count;
            switch (rail.Style)
            {
                case "balusters":
                    Posts(Math.Max(rail.BalusterSpacing, 0.05), Math.Max(0.012, rail.PostSize * 0.6), "baluster", lastSegment);
                    if (Prism.SweepBox(Up(a, 0.08), Up(b, 0.08), 0.03, 0.03, "rail") is { } bottomRail) metal.Add(bottomRail);
                    break;
                case "glass":
                    Posts(Math.Max(rail.PostSpacing, 0.3), rail.PostSize, "post", lastSegment);
                    if (Pane(0.08, height - 0.06, 0.012, "glass") is { } pane) glass.Add(pane);
                    break;
                case "cable":
                    Posts(Math.Max(rail.PostSpacing, 0.3), rail.PostSize, "post", lastSegment);
                    for (var c = 1; c <= 6; c++)
                        if (Prism.SweepBox(Up(a, height * c / 7.5), Up(b, height * c / 7.5), 0.006, 0.006, "cable") is { } cable) metal.Add(cable);
                    break;
                case "panel":
                    if (Pane(0, height, Math.Max(0.06, rail.PostSize * 2), "panel") is { } panel) metal.Add(panel);
                    break;
                default:
                    Posts(Math.Max(rail.PostSpacing, 0.3), rail.PostSize, "post", lastSegment);
                    break;
            }
        }
        return (metal, glass);
    }

    /// <summary>The point <paramref name="s"/> along a line of pieces (their lengths in plan, <paramref name="total"/> in all), its height between theirs.</summary>
    static Vec3 At(IReadOnlyList<Vec3> run, IReadOnlyList<double> lens, double s, double total)
    {
        if (run.Count == 2) return run[0] + (run[1] - run[0]) * (total < 1e-12 ? 0 : s / total);
        for (var k = 0; k < lens.Count; k++)
        {
            if (s <= lens[k] + 1e-12 || k == lens.Count - 1) return run[k] + (run[k + 1] - run[k]) * (lens[k] < 1e-12 ? 0 : Math.Clamp(s / lens[k], 0, 1));
            s -= lens[k];
        }
        return run[^1];
    }

    /// <summary>
    /// The handrail along a railing's line, at its height over it: a bar (square or round) along each piece, upright ones too
    /// (a flight's rail stepping down to a landing's, forum topic 104); each runs on half its size past a joint, so the
    /// handrail turns with no gap at its kinks and corners.
    /// </summary>
    public static List<Solid> Bars(IReadOnlyList<Vec3> path, StairRailing rail)
    {
        var bars = new List<Solid>();
        var height = Math.Max(0.3, rail.Height);
        Vec3 Up(Vec3 p, double up) => new(p.X, p.Y, p.Z + up);
        for (var i = 0; i + 1 < path.Count; i++)
        {
            var (a, b) = (Up(path[i], height), Up(path[i + 1], height));
            var axis = b - a;
            if (axis.Length < 1e-6) continue;
            var dir = axis * (1 / axis.Length);
            var lap = rail.BarSize / 2;
            if (i > 0) a -= dir * lap;
            if (i + 2 < path.Count) b += dir * lap;
            var bar = rail.Profile == "round" ? StairBuilder.RoundBar(a, b, rail.BarSize, "rail") : Prism.SweepBox(a, b, rail.BarSize, rail.BarSize, "rail");
            if (bar != null) bars.Add(bar);
        }
        return bars;
    }

    /// <summary>
    /// A handrail fixed to a wall (forum topic 102: a wall handrail on the wall side instead of a balustrade): the bar along the
    /// line at its height, held off the wall by brackets at most the post spacing apart, each an arm out to the side
    /// (<paramref name="outward"/>: +1 to the left of the way the line runs, -1 to its right) and a plate on the wall.
    /// </summary>
    public static List<Solid> WallHandrail(IReadOnlyList<Vec3> path, StairRailing rail, double outward)
    {
        var metal = Bars(path, rail);
        var height = Math.Max(0.3, rail.Height);
        var reach = Math.Max(0.04, rail.Inset) + rail.BarSize / 2;
        for (var i = 0; i + 1 < path.Count; i++)
        {
            var (a, b) = (path[i], path[i + 1]);
            var len = (b.Xy - a.Xy).Length;
            if (len < 1e-6) continue;
            var side = (b.Xy - a.Xy).Normalized().Perp * outward;
            var count = Math.Max(1, (int)Math.Ceiling(len / Math.Max(rail.PostSpacing, 0.3)));
            for (var j = 0; j <= count; j++)
            {
                if (j == count && i + 2 < path.Count) continue;
                // Brackets stand a little in from the ends of each piece.
                var t = Math.Clamp((double)j / count, 0.08, 0.92);
                var on = a + (b - a) * t;
                var under = on.Z + height - rail.BarSize / 2;
                var elbow = new Vec3(on.X, on.Y, under - 0.05);
                var arm = new Vec3(side.X * reach, side.Y * reach, 0);
                if (Prism.SweepBox(new Vec3(on.X, on.Y, under), elbow, 0.012, 0.012, "bracket") is { } stem) metal.Add(stem);
                if (Prism.SweepBox(elbow, elbow + arm, 0.012, 0.012, "bracket") is { } reachOut) metal.Add(reachOut);
                var wall = elbow + arm;
                var flatX = Math.Abs(side.X) > Math.Abs(side.Y);
                if (Prism.Box(new Vec3(wall.X + side.X * 0.004, wall.Y + side.Y * 0.004, wall.Z), flatX ? 0.008 : 0.07, flatX ? 0.07 : 0.008, 0.07, "bracket") is { } plate) metal.Add(plate);
            }
        }
        return metal;
    }

    // ------------------------------------------------------------ the railing of its own

    /// <summary>A railing element's design, from its own fields (the stair railing's defaults where it has none).</summary>
    public static StairRailing Of(Entity e) => new("both",
        e.NumOrNull("height") ?? 1.0, e.NumOrNull("barSize") ?? 0.045, e.NumOrNull("postSize") ?? 0.04, e.NumOrNull("postSpacing") ?? 1.2,
        StairSpec.RailingStyles.Contains(e.Str("style") ?? "") ? e.Str("style")! : "balusters", e.Str("profile") == "square" ? "square" : "round",
        e.NumOrNull("balusterSpacing") ?? 0.11, 0, 0, e.Str("color"));


    public static List<Vec2> Points(Entity e) => (e.Attrs["points"] as JsonArray)?.Select(P).ToList() ?? [];

    /// <summary>
    /// Each segment's rise off its chord (a user's request, as a floor's edges, ArcOutline): segment i runs from point i to
    /// i + 1 and rises that many metres at its middle, to the left of the way it runs when positive (a wall's <c>arc</c>);
    /// 0 straight. Kept in <c>arcs</c> beside the points only when one is curved, so a railing drawn before reads unchanged.
    /// </summary>
    public static double[] Rises(Entity e, int count) => Pad(ArcOutline.Rises(e.Attrs[ArcOutline.Key], Math.Max(0, count - 1)), count);

    static double[] Pad(IReadOnlyList<double>? rises, int count)
    {
        var o = new double[Math.Max(0, count - 1)];
        for (var i = 0; i < o.Length && rises != null && i < rises.Count; i++) o[i] = rises[i];
        return o;
    }

    /// <summary>Segment i as an arc (or a straight line).</summary>
    public static PathCurve Segment(IReadOnlyList<Vec2> pts, IReadOnlyList<double> rises, int i) => new(pts[i], pts[i + 1], i < rises.Count ? rises[i] : 0);

    /// <summary>The railing's line as points, each arc laid in chords by the one rule (<see cref="Chords"/>); for each chord, the segment it lies on.</summary>
    public static (List<Vec2> Points, List<int> PieceOf) Chorded(IReadOnlyList<Vec2> pts, IReadOnlyList<double> rises)
    {
        var o = new List<Vec2>();
        var of = new List<int>();
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            var c = Segment(pts, rises, i);
            if (!c.IsArc) { o.Add(pts[i]); of.Add(i); continue; }
            var st = c.Stations(0, c.Length);
            for (var k = 0; k + 1 < st.Count; k++) { o.Add(k == 0 ? pts[i] : c.PointAt(st[k])); of.Add(i); }
        }
        if (pts.Count > 0) o.Add(pts[^1]);
        return (o, of);
    }

    /// <summary>Its length along its line, the arcs at their own length.</summary>
    public static double Length(IReadOnlyList<Vec2> pts, IReadOnlyList<double> rises)
    {
        double l = 0;
        for (var i = 0; i + 1 < pts.Count; i++) l += Segment(pts, rises, i).Length;
        return l;
    }

    /// <summary>A line as a command gives it: points that coincide made one (the segment leaving keeps its arc), two at least, each rise checked.</summary>
    static (List<Vec2> Points, double[] Rises) CheckPath(JsonNode? n, JsonNode? arcs)
    {
        var pts = (n as JsonArray)?.Select(P).ToList() ?? throw new CommandException("A railing runs along two points at least.");
        var given = arcs is JsonArray arr ? arr.Select(x => Json.Number(x) ?? 0).ToList() : [];
        var clean = new List<Vec2>();
        var leaving = new List<double>();
        for (var i = 0; i < pts.Count; i++)
        {
            var rise = i < given.Count ? given[i] : 0;
            if (!double.IsFinite(rise)) throw new CommandException("A segment's arc must be a number.");
            // A point on the one before: the segment leaving it is the later one's.
            if (clean.Count > 0 && (pts[i] - clean[^1]).Length <= 1e-3) { leaving[^1] = rise; continue; }
            clean.Add(pts[i]);
            leaving.Add(rise);
        }
        var r = leaving.Take(Math.Max(0, clean.Count - 1)).ToList();
        if (clean.Count < 2) throw new CommandException("A railing runs along two points at least.");
        for (var i = 0; i < r.Count; i++) r[i] = PathCurve.CheckRise(r[i], (clean[i + 1] - clean[i]).Length);
        return (clean, [.. r]);
    }

    /// <summary>A line run the other way: its points reversed, each arc the same one, so rising to the other side of its new way.</summary>
    static (List<Vec2>, double[]) Reversed(IReadOnlyList<Vec2> pts, IReadOnlyList<double> rises)
    {
        var p = pts.Reverse().ToList();
        var n = rises.Count;
        return (p, [.. Enumerable.Range(0, n).Select(j => -rises[n - 1 - j])]);
    }

    /// <summary>Two lines that share an end (within a millimetre) as one, the first one's way kept; null when no end is shared.</summary>
    static (List<Vec2>, double[])? Joined(IReadOnlyList<Vec2> p1, IReadOnlyList<double> r1, IReadOnlyList<Vec2> p2, IReadOnlyList<double> r2)
    {
        static bool Same(Vec2 a, Vec2 b) => (a - b).Length <= 1e-3;
        (List<Vec2>, double[]) Cat(IReadOnlyList<Vec2> a, IReadOnlyList<double> ra, IReadOnlyList<Vec2> b, IReadOnlyList<double> rb) =>
            ([.. a, .. b.Skip(1)], [.. ra, .. rb]);
        if (Same(p1[^1], p2[0])) return Cat(p1, r1, p2, r2);
        if (Same(p1[^1], p2[^1])) { var (q, rq) = Reversed(p2, r2); return Cat(p1, r1, q, rq); }
        if (Same(p1[0], p2[^1])) return Cat(p2, r2, p1, r1);
        if (Same(p1[0], p2[0])) { var (q, rq) = Reversed(p2, r2); return Cat(q, rq, p1, r1); }
        return null;
    }

    /// <summary>The railing's point <paramref name="i"/> (between two straight segments) rounded by an arc of <paramref name="radius"/> tangent to both, or cut straight across <paramref name="distance"/> back along each.</summary>
    static (List<Vec2>, double[]) Corner(IReadOnlyList<Vec2> pts, IReadOnlyList<double> rises, int i, double size, bool chamfer)
    {
        if (i <= 0 || i >= pts.Count - 1) throw new CommandException($"Which corner? The railing's corners are its points between two segments, 1 to {pts.Count - 2}.");
        // As a closed outline whose last edge (back to the first point) is straight: the corner is between two of its edges.
        var closed = rises.Append(0).ToList();
        var (c, r) = chamfer ? ArcOutline.Chamfer(pts, closed, i, size) : ArcOutline.Fillet(pts, closed, i, size);
        return (c, r[..^1]);
    }

    static bool SameDesign(Entity a, Entity b) =>
        a.Str("style") == b.Str("style") && Math.Abs(a.Num("height", 1) - b.Num("height", 1)) < 1e-6 && Math.Abs(a.Num("baseOffset") - b.Num("baseOffset")) < 1e-6;

    static Entity Require(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: Class } x && x.Attrs["points"] != null ? x : throw new CommandException("Not a railing drawn in Lapin.");

    static void Write(ICommandContext ctx, Entity e, IReadOnlyList<Vec2> pts, IReadOnlyList<double> rises)
    {
        var attrs = ctx.Model.Get(e.Id)!.CloneAttrs();
        attrs["points"] = SectionPaths.PointsJson(pts);
        ArcOutline.Write(attrs, rises);
        ctx.Replace(e.With(attrs: attrs));
    }

    /// <summary>The segment of the railing's line nearest a point.</summary>
    static int SegmentNear(IReadOnlyList<Vec2> pts, IReadOnlyList<double> rises, Vec2 p) =>
        Enumerable.Range(0, pts.Count - 1).MinBy(i => Segment(pts, rises, i).Distance(p));

    /// <summary>
    /// A railing's corner rounded or cut (FILLET, CHAMFER): the corner between the two segments of one railing picked
    /// (<c>atA</c>, <c>atB</c>), or between two railings, whose ends meet or would meet carried on along their end segments:
    /// one railing then, the first one (its id and design), the second taken into it. No radius: a sharp corner.
    /// </summary>
    static JsonObject CornerOf(ICommandContext ctx, JsonObject a, bool chamfer)
    {
        var m = ctx.Model;
        var ea = Require(m, (string?)a["a"]);
        var eb = (string?)a["b"] is { } bid && bid != ea.Id ? Require(m, bid) : null;
        var size = chamfer ? Json.Number(a["distance"]) ?? Json.Number(a["d1"]) ?? 0 : Json.Number(a["radius"]) ?? 0;
        if (!double.IsFinite(size) || size < 0) throw new CommandException(chamfer ? "A chamfer's distance is a length, 0 or more." : "A fillet's radius is a length, 0 or more.");
        Pins.Refuse(eb == null ? [ea] : [ea, eb]);
        var pts = Points(ea);
        var rises = Rises(ea, pts.Count);
        int corner;
        if (eb == null)
        {
            if (Args.TryPoint(a["atA"]) is not { } pa || Args.TryPoint(a["atB"]) is not { } pb) throw new CommandException("Pick the railing on the two segments that meet at the corner.");
            var (i, j) = (SegmentNear(pts, rises, pa), SegmentNear(pts, rises, pb));
            if (Math.Abs(i - j) != 1) throw new CommandException("Pick two segments of the railing that meet at a corner.");
            corner = Math.Max(i, j);
            if (size <= 1e-9) return new JsonObject { ["id"] = ea.Id };
        }
        else
        {
            if (Spatial.StoreyOf(m, ea.Id)?.Id != Spatial.StoreyOf(m, eb.Id)?.Id) throw new CommandException("The railings stand on different storeys.");
            var matchFirst = a["matchFirst"] is JsonValue mv && mv.TryGetValue<bool>(out var mf) && mf;
            if (!matchFirst && !SameDesign(ea, eb)) throw new CommandException("The railings differ (design, height or base): join them with the first railing's, or make them alike first.");
            var q = Points(eb);
            var rq = Rises(eb, q.Count);
            // The ends nearest each other: the first railing's to its end, the second's to its start.
            var (bestA, bestB, best) = (1, 0, double.MaxValue);
            foreach (var ia in new[] { 0, 1 })
                foreach (var ib in new[] { 0, 1 })
                    if (((ia == 0 ? pts[0] : pts[^1]) - (ib == 0 ? q[0] : q[^1])).Length is var d && d < best - 1e-9) (bestA, bestB, best) = (ia, ib, d);
            if (bestA == 0) (pts, rises) = Reversed(pts, rises);
            if (bestB == 1) (q, rq) = Reversed(q, rq);
            if (Math.Abs(rises[^1]) > 1e-6 || Math.Abs(rq[0]) > 1e-6) throw new CommandException("The railings end in arcs: a corner is between two straight ends.");
            var (ua, ub) = ((pts[^1] - pts[^2]).Normalized(), (q[1] - q[0]).Normalized());
            Vec2 x;
            if (best <= 1e-3) x = pts[^1];
            else
            {
                var den = Vec2.Cross(ua, ub);
                if (Math.Abs(den) < 1e-6) throw new CommandException("The railings run parallel: their ends never meet.");
                x = pts[^1] + ua * (Vec2.Cross(q[0] - pts[^1], ub) / den);
                if (Vec2.Dot(x - pts[^2], ua) < 1e-3 || Vec2.Dot(q[1] - x, ub) < 1e-3) throw new CommandException("The railings' ends do not meet carried on: pick two railings that end towards each other.");
            }
            List<Vec2> joined = [.. pts.Take(pts.Count - 1), x, .. q.Skip(1)];
            pts = joined;
            rises = [.. rises, .. rq];
            corner = joined.Count - q.Count;
            ctx.Remove(eb.Id);
            // In line, there is no corner to round: one railing straight on.
            if (Math.Abs(Vec2.Cross(ua, ub)) < 1e-6 || size <= 1e-9)
            {
                Write(ctx, ea, pts, rises);
                return new JsonObject { ["id"] = ea.Id };
            }
        }
        var (c, r) = Corner(pts, rises, corner, size, chamfer);
        Write(ctx, ea, c, r);
        return new JsonObject { ["id"] = ea.Id };
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Railing",
        [
            new("style", "Design", "choice", Choices: StairSpec.RailingStyles),
            new("height", "Height", "length", Min: 0.3, Max: 2.5),
            new("baseOffset", "Base offset", "length", Min: -5, Max: 50),
            new("profile", "Handrail", "choice", Choices: ["round", "square"]),
            new("barSize", "Handrail size", "length", Min: 0.02, Max: 0.15),
            new("postSize", "Post size", "length", Min: 0.01, Max: 0.15),
            new("postSpacing", "Post spacing", "length", Min: 0.3, Max: 3),
            new("balusterSpacing", "Baluster spacing", "length", Min: 0.05, Max: 0.3),
        ], "#4a4a4a"));
        r.Geometry(Class, (e, m, _) => Geometry(e, m));
        r.Quantities(Class, (e, _, m) => Quantities(e, m));

        // A railing along a line drawn in plan: {storeyId, points: [[x, y]…], arcs? (each segment's rise, left positive), style?,
        // height?, baseOffset?, join? (taken into a railing of the same design whose end it meets, as drafting converted is)}.
        r.Command("addRailing", "Railing", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var (pts, rises) = CheckPath(a["points"], a[ArcOutline.Key]);
            var attrs = new JsonObject { ["exportAs"] = "IfcRailing", ["points"] = SectionPaths.PointsJson(pts), ["style"] = (string?)a["style"] is { } st && StairSpec.RailingStyles.Contains(st) ? st : "balusters", ["height"] = Json.Number(a["height"]) ?? 1.0 };
            if (Json.Number(a["baseOffset"]) is { } bo) attrs["baseOffset"] = bo;
            ArcOutline.Write(attrs, rises);
            var n = Numbering.Next(ctx.Model.OfClass(Class));
            var e = new Entity(ctx.NewId(), Class, $"Railing {n}", attrs, "GUARDRAIL");
            if (a["join"] is JsonValue jv && jv.TryGetValue<bool>(out var join) && join && JoinOn(ctx, storeyId, e, pts, rises) is { } into) return new JsonObject { ["id"] = into };
            ctx.Add(e);
            Spatial.Contain(ctx, storeyId, e.Id);
            return new JsonObject { ["id"] = e.Id };
        });

        // Its line: {id, points, arcs?}; without arcs, the ones it has while its points are as many.
        r.Command("updateRailing", "Change railing", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var keep = Points(e).Count is var count && a[ArcOutline.Key] == null && a["points"] is JsonArray given && given.Count == count;
            var (pts, rises) = CheckPath(a["points"], keep ? ArcOutline.ToJson(Rises(e, count)) : a[ArcOutline.Key]);
            Write(ctx, e, pts, rises);
            return null;
        });

        // A segment bent into an arc or made straight: {id, index (the segment, from point index), rise (m off its chord, left positive; 0 straight)}.
        r.Command("setRailingArc", "Railing arc", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var pts = Points(e);
            var rises = Rises(e, pts.Count);
            var i = Json.Number(a["index"]) is { } k && k >= 0 && k < rises.Length ? (int)k : throw new CommandException($"Which segment? The railing has {rises.Length}, from 0.");
            rises[i] = PathCurve.CheckRise(Json.Number(a["rise"]) ?? 0, (pts[i + 1] - pts[i]).Length);
            Write(ctx, e, pts, rises);
            return null;
        });

        // A corner of a railing rounded or cut: {id, index (the point between the two segments), radius | distance}.
        r.Command("roundRailingCorner", "Fillet railing corner", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var pts = Points(e);
            var (c, rr) = Corner(pts, Rises(e, pts.Count), (int)(Json.Number(a["index"]) ?? -1), Json.Number(a["radius"]) ?? throw new CommandException("What radius? A length in metres."), false);
            Write(ctx, e, c, rr);
            return null;
        });
        r.Command("chamferRailingCorner", "Chamfer railing corner", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var pts = Points(e);
            var (c, rr) = Corner(pts, Rises(e, pts.Count), (int)(Json.Number(a["index"]) ?? -1), Json.Number(a["distance"]) ?? throw new CommandException("What distance? A length in metres."), true);
            Write(ctx, e, c, rr);
            return null;
        });

        // FILLET and CHAMFER on railings: {a, b? (another railing: the two made one), atA?, atB? (where each was picked), radius | distance, matchFirst?}.
        r.Command("filletRailings", "Fillet railings", (ctx, a) => CornerOf(ctx, a, false));
        r.Command("chamferRailings", "Chamfer railings", (ctx, a) => CornerOf(ctx, a, true));
    }

    /// <summary>
    /// A new railing's line taken into the railings of the same design on its storey whose ends it meets (drafting converted
    /// piece by piece: a line, its fillet's arc, the next line become one railing). The first one met keeps its id; null when it meets none.
    /// </summary>
    static string? JoinOn(ICommandContext ctx, string storeyId, Entity made, List<Vec2> pts, double[] rises)
    {
        var m = ctx.Model;
        Entity? into = null;
        var candidates = m.RelatedOf(storeyId, IfcClass.RelContainedInSpatialStructure).Where(x => x.Class == Class && x.Attrs["points"] != null && SameDesign(x, made)).ToList();
        for (var found = true; found;)
        {
            found = false;
            foreach (var c in candidates.Where(c => c.Id != into?.Id && m.Get(c.Id) != null).ToList())
            {
                var q = Points(c);
                if (q.Count < 2 || Joined(pts, rises, q, Rises(c, q.Count)) is not var (jp, jr)) continue;
                // Not round into itself: a loop stays open where it closes.
                if ((jp[0] - jp[^1]).Length <= 1e-3 && into != null) continue;
                (pts, rises) = (jp, jr);
                if (into == null) into = c; else ctx.Remove(c.Id);
                found = true;
                break;
            }
        }
        if (into == null) return null;
        Write(ctx, into, pts, rises);
        return into.Id;
    }

    static double BaseZ(Entity e, IModelView m) => (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("baseOffset");

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        var pts = Points(e);
        if (pts.Count < 2) return null;
        var rises = Rises(e, pts.Count);
        var rail = Of(e);
        var z = BaseZ(e, m);
        var curved = rises.Any(x => Math.Abs(x) > 1e-6);
        var (path, pieceOf) = curved ? Chorded(pts, rises) : (pts, null);
        var (metal, glass) = Solids(path.Select(p => p.At(z)).ToList(), rail, null, pieceOf);
        var parts = new List<GeometryPart>();
        if (metal.Count > 0) parts.Add(new GeometryPart("Railing", rail.Color ?? "#4a4a4a", Openings.Combine(metal, "railing"), null, "railing"));
        if (glass.Count > 0) parts.Add(new GeometryPart("Railing glass", "#9cc8e8", Openings.Combine(glass, "glass"), null, "railing-glass"));
        if (parts.Count == 0) return null;
        // In plan: the handrail as a band along each segment (round an arc, in its chords), and the posts.
        var plan = new List<PlanItem>();
        var half = Math.Max(rail.BarSize, rail.Style == "panel" ? Math.Max(0.06, rail.PostSize * 2) : rail.BarSize) / 2;
        var snaps = new List<Vec2>(pts);
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            var c = Segment(pts, rises, i);
            if (c.IsArc)
            {
                plan.Add(new PlanItem("cut", true, c.Band(_ => 0, _ => c.Length, -half, half)));
                snaps.Add(c.PointAt(c.Length / 2));
                continue;
            }
            var (a, b) = (pts[i], pts[i + 1]);
            var n = (b - a).Normalized().Perp * half;
            plan.Add(new PlanItem("cut", true, [a + n, b + n, b - n, a - n]));
        }
        return new ElementGeometry(parts, plan, snaps);
    }

    static IEnumerable<QuantitySet> Quantities(Entity e, IModelView m)
    {
        var pts = Points(e);
        if (pts.Count < 2) yield break;
        yield return new QuantitySet("Qto_RailingBaseQuantities", [new("Length", Length(pts, Rises(e, pts.Count)), "LENGTH", $"Railing, {Of(e).Style}")]);
    }
}
