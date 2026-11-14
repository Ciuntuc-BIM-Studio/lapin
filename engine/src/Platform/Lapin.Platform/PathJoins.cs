using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>A path element as joins see it now: its axis and its body across it (null: it cannot be joined as it is).</summary>
public delegate JoinWall? PathFrame(Entity entity, IModelView model);

/// <summary>The path elements of the product (walls, strip foundations), offered by the platform as a service.</summary>
public interface IPathElements
{
    bool IsPath(string ifcClass);
    JoinWall? FrameOf(Entity entity, IModelView model);
}

/// <summary>
/// Joins between path elements: elements along an axis (<c>start</c>,
/// <c>end</c>) that a module declares (a wall, a strip foundation). A join is
/// an IfcRelConnectsPathElements: the element that runs through relates (the
/// first of an in-line join), the other is related; its attributes say how
/// they connect (ATSTART, ATEND, ATPATH), its kind and its style (butt or
/// mitre). That is all that is stored: each element's cuts are worked out from
/// it, and from both elements as they are now, whenever its geometry is built,
/// so an edit to either keeps the corner clean. Only elements of the same class
/// join.
/// </summary>
public static class Joins
{
    public const string Class = IfcClass.RelConnectsPathElements;

    public static Vec2 Start(Entity e) => Args.PointOf(e.Attrs["start"]);
    public static Vec2 End(Entity e) => Args.PointOf(e.Attrs["end"]);

    public static IEnumerable<Relation> Of(IModelView m, string id) => m.RelationsOf(id, Class);

    public static Relation? Between(IModelView m, string a, string b) => Of(m, a).FirstOrDefault(r => r.Touches(b));

    public static string Other(Relation r, string id) => r.Relating == id ? r.Related[0] : r.Relating;

    public static JoinStyle StyleOf(Relation r) => (string?)r.Attrs?["style"] == "mitre" ? JoinStyle.Mitre : JoinStyle.Butt;

    /// <summary>On a join: the related element stops in the relating one, which it never cuts or shortens (a flat's inner wall in the building's wall, docs/plan-units.md).</summary>
    public const string StopsOn = "stopsOn";

    public static bool Stops(Relation r) => r.Attrs?[StopsOn] is JsonValue v && v.TryGetValue<bool>(out var on) && on;

    /// <summary>
    /// On a path element: whether each of its ends joins automatically (<c>{start, end}</c>, the forum's topic #50, decision 546).
    /// Not stored, an end of an element drawn in Lapin joins, and one recognised from a file (its <c>imported</c> record) does
    /// not: the import reproduces the file. An end that does not join automatically keeps the joins it has, which the join
    /// tools still make and take off; <see cref="Auto"/> neither joins it nor brings another element's end to it.
    /// </summary>
    public const string AutoKey = "autoJoin";

    /// <summary>Whether the element's end (0 start, 1 end) joins automatically (<see cref="AutoKey"/>).</summary>
    public static bool AutoAt(Entity e, int end) =>
        e.Attrs[AutoKey] is JsonObject o && o[end == 0 ? "start" : "end"] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : e.Attrs["imported"] == null;

    /// <summary>Whether any end of the element joins automatically; one with neither joins nothing new (not even along its run).</summary>
    public static bool AutoAny(Entity e) => AutoAt(e, 0) || AutoAt(e, 1);

    /// <summary>The attributes with the ends' automatic joining set (null: as it is); both ends stored, so a copy keeps what was asked.</summary>
    public static void SetAuto(JsonObject attrs, Entity e, bool? start, bool? end)
    {
        attrs[AutoKey] = new JsonObject { ["start"] = start ?? AutoAt(e, 0), ["end"] = end ?? AutoAt(e, 1) };
    }

    static JoinWall Frame(IPathElements paths, IModelView m, Entity e) =>
        paths.FrameOf(e, m) ?? throw new CommandException($"„{e.Name}” cannot be joined as it is.");

    /// <summary>
    /// The join as it is now, from both elements (by their own frames, or by
    /// <paramref name="frameFor"/>: a strip footing joins as its stem does, with
    /// its own width); null when they no longer meet.
    /// </summary>
    public static WallJoin? Compute(IModelView m, Relation r, IPathElements paths, out string? problem, Func<Entity, JoinWall?>? frameFor = null)
    {
        problem = null;
        if (m.Get(r.Relating) is not { } a || r.Related.Count == 0 || m.Get(r.Related[0]) is not { } b || !paths.IsPath(a.Class) || a.Class != b.Class)
        {
            problem = "A join between elements that are not both there.";
            return null;
        }
        var fa = frameFor?.Invoke(a) ?? paths.FrameOf(a, m);
        var fb = frameFor?.Invoke(b) ?? paths.FrameOf(b, m);
        if (fa == null || fb == null) { problem = "A joined element has no body to join."; return null; }
        (fa, fb) = Tangents(a, b, r, fa, fb);
        try { return WallJoins.Compute(fa, fb, new JoinOptions(Priority: 'a', Style: StyleOf(r), Through: Stops(r) ? 'a' : null)); }
        catch (CommandException ex) { problem = ex.Message; return null; }
    }

    /// <summary>Where each end of the element meets its joins: the joint, and the cut there (none: square at its own end).</summary>
    public static (Vec2 Point, EndCut Cut)?[] EndsOf(IModelView m, Entity e, IPathElements paths, List<string> problems, Func<Entity, JoinWall?>? frameFor = null)
    {
        var ends = new (Vec2, EndCut)?[2];
        foreach (var r in Of(m, e.Id))
        {
            var join = Compute(m, r, paths, out var problem, frameFor);
            if (join == null) { problems.Add(problem!); continue; }
            var side = r.Relating == e.Id ? join.A : join.B;
            var i = side.Connection switch { Connection.AtStart => 0, Connection.AtEnd => 1, _ => -1 };
            if (i < 0 || ends[i] != null) continue;
            ends[i] = i == 0 ? (side.Wall.Start, side.Wall.StartCut ?? EndCut.Square) : (side.Wall.End, side.Wall.EndCut ?? EndCut.Square);
        }
        // An end where three or more meet (a tester's report: a V's corner with a third wall going on from it): cut as a fan.
        for (var i = 0; i < 2; i++)
            if (NodeAt(m, e, i, paths, frameFor) is { } fan) ends[i] = fan;
        return ends;
    }

    /// <summary>Whether an element's end is a node of three or more (joined there to two others or more): its layers then follow the fan cut.</summary>
    public static bool IsNode(IModelView m, Entity e, int end)
    {
        var mine = end == 0 ? "ATSTART" : "ATEND";
        return Of(m, e.Id).Count(r => (string?)r.Attrs?[r.Relating == e.Id ? "relatingConnection" : "relatedConnection"] == mine) >= 2;
    }

    /// <summary>
    /// An element's end joined at one point to two others or more, each by its own end (a node of three walls or more):
    /// all of them taken round the point by the direction each leaves it, every face of this one is cut where it meets
    /// the face of its neighbour on that side, as a mitred corner is between two; null when the end is not such a node
    /// (one join, a T on a run, an arc among them).
    /// </summary>
    static (Vec2 Point, EndCut Cut)? NodeAt(IModelView m, Entity e, int end, IPathElements paths, Func<Entity, JoinWall?>? frameFor)
    {
        var mine = end == 0 ? "ATSTART" : "ATEND";
        var partners = new List<(Entity E, int End)>();
        foreach (var r in Of(m, e.Id))
        {
            if ((string?)r.Attrs?[r.Relating == e.Id ? "relatingConnection" : "relatedConnection"] != mine) continue;
            var theirs = (string?)r.Attrs?[r.Relating == e.Id ? "relatedConnection" : "relatingConnection"];
            if (theirs is not ("ATSTART" or "ATEND") || m.Get(Other(r, e.Id)) is not { } o) return null;
            partners.Add((o, theirs == "ATSTART" ? 0 : 1));
        }
        if (partners.Count < 2 || PathCurve.Of(e).IsArc || partners.Any(x => PathCurve.Of(x.E).IsArc)) return null;
        if ((frameFor?.Invoke(e) ?? paths.FrameOf(e, m)) is not { } me) return null;
        var others = new List<(JoinWall, int)>();
        foreach (var (o, oe) in partners)
            if ((frameFor?.Invoke(o) ?? paths.FrameOf(o, m)) is { } f) others.Add((f, oe)); else return null;
        return Fan(me, end, others);
    }

    /// <summary>
    /// The cut of one end of <paramref name="me"/> at a node it shares with <paramref name="others"/> (each by an end): its
    /// face on each side runs to where it meets the facing face of its neighbour round the node on that side. Faces in
    /// line (a straight angle between the two) stop square; a face that would run on past the node more than twice the
    /// wall's thickness (walls all to one side, sharp) stops there.
    /// </summary>
    public static (Vec2 Point, EndCut Cut) Fan(JoinWall me, int myEnd, IReadOnlyList<(JoinWall Wall, int End)> others)
    {
        var all = others.Prepend((me, myEnd)).Select(x =>
        {
            var along = (x.Item1.End - x.Item1.Start).Normalized();
            var into = x.Item2 == 0 ? along : -along;
            return (Wall: x.Item1, End: x.Item2, At: x.Item2 == 0 ? x.Item1.Start : x.Item1.End, Along: along, Into: into, Angle: Math.Atan2(into.Y, into.X));
        }).ToList();
        var self = all[0];
        var sorted = all.OrderBy(x => x.Angle).ToList();
        var k = sorted.IndexOf(self);
        var n = sorted.Count;
        var (ccw, cw) = (sorted[(k + 1) % n], sorted[(k - 1 + n) % n]);
        // The side of a wall facing its counter-clockwise neighbour: the left of the way it leaves the node.
        double CcwSide(JoinWall w, int e) => e == 0 ? w.YMax : w.YMin;
        double CwSide(JoinWall w, int e) => e == 0 ? w.YMin : w.YMax;
        double Reach(double y, (JoinWall Wall, int End, Vec2 At, Vec2 Along, Vec2 Into, double Angle) other, double theirY)
        {
            var gap = Math.Abs(Vec2.Cross(self.Into, other.Into));
            if (gap < 1e-4) return 0;
            var a = self.At + self.Along.Perp * y;
            var b = other.At + other.Along.Perp * theirY;
            var sin = Vec2.Cross(self.Along, other.Along);
            var hit = a + self.Along * (Vec2.Cross(b - a, other.Along) / sin);
            var reach = Vec2.Dot(hit - a, -self.Into);
            return Math.Min(reach, 2 * me.Thickness);
        }
        var toCcw = Reach(CcwSide(me, myEnd), ccw, CwSide(ccw.Wall, ccw.End));
        var toCw = Reach(CwSide(me, myEnd), cw, CcwSide(cw.Wall, cw.End));
        // EndCut: Left is the face at YMax.
        var cut = myEnd == 0 ? new EndCut(toCcw, toCw) : new EndCut(toCw, toCcw);
        return (self.At, cut);
    }

    /// <summary>The attributes an element's own end cuts are kept in: the angle of each free end from square, in degrees (docs/spec-ifc.md, Îmbinările).</summary>
    public const string StartAngle = "startAngle", EndAngle = "endAngle";

    /// <summary>The steepest an end may be cut, in degrees from square.</summary>
    public const double MaxEndAngle = 80;

    /// <summary>
    /// An element's own cut at an end no join takes: the end turned by the angle its attribute gives (degrees from square,
    /// positive taking the left face, +Y, on past the end and the right face short of it), the cut line through the end's
    /// point on the axis, so each layer takes its share of it. Null when the end is square, or the element an arc.
    /// </summary>
    public static (Vec2 Point, EndCut Cut)? OwnEnd(Entity e, JoinWall f, int end)
    {
        var deg = e.NumOrNull(end == 0 ? StartAngle : EndAngle) ?? 0;
        if (Math.Abs(deg) < 1e-6 || PathCurve.Of(e).IsArc) return null;
        var t = Math.Tan(Math.Clamp(deg, -MaxEndAngle, MaxEndAngle) * Math.PI / 180);
        return (end == 0 ? f.Start : f.End, new EndCut(f.YMax * t, f.YMin * t));
    }

    /// <summary>The ends <see cref="EndsOf"/> gives, those no join takes cut as the element's own angles say (<see cref="OwnEnd"/>).</summary>
    public static (Vec2 Point, EndCut Cut)?[] WithOwnEnds((Vec2 Point, EndCut Cut)?[] ends, Entity e, JoinWall? f)
    {
        if (f == null) return ends;
        var o = ends.ToArray();
        for (var i = 0; i < 2; i++) o[i] ??= OwnEnd(e, f, i);
        return o;
    }

    /// <summary>
    /// The frame an element joins by near a point: its own; an arc's, the
    /// tangent line through the arc's point nearest there, as long as the arc
    /// and starting as far back as the point is along it (so a joint at its
    /// start or end is that end).
    /// </summary>
    public static JoinWall Tangent(JoinWall f, Entity e, Vec2 near)
    {
        var c = PathCurve.Of(e);
        if (!c.IsArc) return f;
        var length = c.Length;
        var s = Math.Clamp(c.Project(near), 0, length);
        var (p, t) = (c.PointAt(s), c.TangentAt(s));
        return f with { Start = p - t * s, End = p + t * (length - s) };
    }

    /// <summary>
    /// Two elements' frames for joining each other: straight ones as they are;
    /// an arc by its tangent where they meet (the ends the join names, else the
    /// nearest end of one to the other's path).
    /// </summary>
    static (JoinWall A, JoinWall B) Tangents(Entity a, Entity b, Relation? r, JoinWall fa, JoinWall fb)
    {
        var (ca, cb) = (PathCurve.Of(a), PathCurve.Of(b));
        if (!ca.IsArc && !cb.IsArc) return (fa, fb);
        Vec2? pa = null, pb = null;
        if (r != null && r.Touches(a.Id) && r.Touches(b.Id))
        {
            var mine = (string?)r.Attrs?[r.Relating == a.Id ? "relatingConnection" : "relatedConnection"];
            var theirs = (string?)r.Attrs?[r.Relating == a.Id ? "relatedConnection" : "relatingConnection"];
            pa = mine switch { "ATSTART" => Start(a), "ATEND" => End(a), _ => null };
            pb = theirs switch { "ATSTART" => Start(b), "ATEND" => End(b), _ => null };
        }
        if (pa == null && pb == null)
        {
            var best = double.MaxValue;
            foreach (var p in new[] { Start(a), End(a) })
                if (cb.Distance(p) is var d && d < best) { best = d; (pa, pb) = (p, p); }
            foreach (var p in new[] { Start(b), End(b) })
                if (ca.Distance(p) is var d && d < best - 1e-9) { best = d; (pa, pb) = (p, p); }
        }
        pa ??= pb;
        pb ??= pa;
        return (Tangent(fa, a, pa!.Value), Tangent(fb, b, pb!.Value));
    }

    /// <summary>Two elements' frames for joining each other (null when either cannot be joined as it is).</summary>
    static (JoinWall A, JoinWall B)? Pair(IPathElements paths, IModelView m, Entity a, Entity b)
    {
        if (paths.FrameOf(a, m) is not { } fa || paths.FrameOf(b, m) is not { } fb) return null;
        return Tangents(a, b, Between(m, a.Id, b.Id), fa, fb);
    }

    static string Name(Connection c) => c switch { Connection.AtStart => "ATSTART", Connection.AtEnd => "ATEND", _ => "ATPATH" };

    /// <summary>
    /// Joins two elements: the join is worked out (refused with the reason when
    /// they do not meet), their joined axis ends brought to the crossing, and
    /// the relationship written, in place of any the pair already had. Without
    /// a priority, the element that ran through before still does; without a style, the pair keeps
    /// its own, and a new corner is mitred (decision 175; a T is butt whatever is asked).
    /// </summary>
    /// <param name="stop"><paramref name="b"/> stops in <paramref name="a"/>, which is left as it is (<see cref="StopsOn"/>); a join made so stays so.</param>
    public static WallJoin Join(IEdit edit, IPathElements paths, Entity a, Entity b, JoinStyle? style = null, char? priority = null, bool stop = false)
    {
        if (a.Id == b.Id) throw new CommandException("An element cannot be joined to itself.");
        if (a.Class != b.Class) throw new CommandException($"A {a.Class} joins only a {a.Class}.");
        var existing = Between(edit.Model, a.Id, b.Id);
        char? through = stop ? 'a' : existing != null && Stops(existing) ? existing.Relating == a.Id ? 'a' : 'b' : null;
        priority ??= existing == null ? null : existing.Relating == a.Id ? 'a' : 'b';
        // A curved element and a straight one: the arc runs through (on along its tangent), the straight one stops at its face.
        if (priority == null && PathCurve.Of(a).IsArc != PathCurve.Of(b).IsArc) priority = PathCurve.Of(a).IsArc ? 'a' : 'b';
        var (fa, fb) = Tangents(a, b, existing, Frame(paths, edit.Model, a), Frame(paths, edit.Model, b));
        var join = WallJoins.Compute(fa, fb, new JoinOptions(Priority: priority, Style: style ?? (existing != null ? StyleOf(existing) : JoinStyle.Mitre), Through: through));
        // A crossing is worked out on straight axes: an arc's frame is only a tangent, which says nothing of where it crosses.
        if (join.Kind == JoinKind.X && (PathCurve.Of(a).IsArc || PathCurve.Of(b).IsArc))
            throw new CommandException("A curved element is not joined where it crosses another: split it there.");
        MoveAxis(edit, a, join.A);
        MoveAxis(edit, b, join.B);
        var (relating, related, relatingSide, relatedSide) = join.Relating == 'a' ? (a, b, join.A, join.B) : (b, a, join.B, join.A);
        var attrs = new JsonObject
        {
            ["relatingConnection"] = Name(relatingSide.Connection),
            ["relatedConnection"] = Name(relatedSide.Connection),
            ["kind"] = join.Kind.ToString(),
            ["style"] = join.Style == JoinStyle.Mitre ? "mitre" : "butt",
        };
        if (through != null) attrs[StopsOn] = true;
        if (existing != null) edit.ReplaceRelation(existing with { Relating = relating.Id, Related = [related.Id], Attrs = attrs });
        else edit.AddRelation(new Relation(edit.NewId(), Class, relating.Id, [related.Id], attrs));
        return join;
    }

    /// <summary>The element's axis brought to the join: a straight one both ends as the join has them; an arc only its joined end (its frame is a tangent).</summary>
    static void MoveAxis(IEdit edit, Entity e, JoinSide side)
    {
        var current = edit.Model.Get(e.Id)!;
        var (start, end) = (side.Wall.Start, side.Wall.End);
        if (PathCurve.Of(current).IsArc)
            (start, end) = side.Connection switch
            {
                Connection.AtStart => (start, End(current)),
                Connection.AtEnd => (Start(current), end),
                _ => (Start(current), End(current)),
            };
        if ((Start(current) - start).Length < 1e-12 && (End(current) - end).Length < 1e-12) return;
        var attrs = current.CloneAttrs();
        attrs["start"] = new JsonArray(start.X, start.Y);
        attrs["end"] = new JsonArray(end.X, end.Y);
        edit.Replace(current.With(attrs: attrs));
    }

    /// <summary>
    /// An end of a straight element, free, stopped in the nearest of <paramref name="walls"/> it ends on: a T that leaves that
    /// wall as it is, the end brought to its axis and cut at its face. The wall it stopped in, or null when none is near.
    /// </summary>
    public static Entity? Stop(IEdit edit, IPathElements paths, Entity e, int end, IEnumerable<Entity> walls)
    {
        var m = edit.Model;
        if (PathCurve.Of(e).IsArc || paths.FrameOf(e, m) is not { } fe) return null;
        var at = end == 0 ? Start(e) : End(e);
        foreach (var wall in walls.Where(w => w.Id != e.Id && w.Class == e.Class && !PathCurve.Of(w).IsArc && Between(m, w.Id, e.Id) == null).OrderBy(w => DistanceTo(w, at)))
        {
            if (paths.FrameOf(wall, m) is not { } fw) continue;
            WallJoin trial;
            try { trial = WallJoins.Compute(fw, fe, new JoinOptions(Through: 'a')); }
            catch (CommandException) { continue; }
            if (trial.B.Connection != (end == 0 ? Connection.AtStart : Connection.AtEnd)) continue;
            Join(edit, paths, wall, e, JoinStyle.Butt, stop: true);
            return wall;
        }
        return null;
    }

    /// <summary>The ends of the element that a join already takes (start, end).</summary>
    public static bool[] JoinedEnds(IModelView m, Entity e)
    {
        var o = new bool[2];
        foreach (var r in Of(m, e.Id))
        {
            var connection = (string?)r.Attrs?[r.Relating == e.Id ? "relatingConnection" : "relatedConnection"];
            if (connection == "ATSTART") o[0] = true;
            if (connection == "ATEND") o[1] = true;
        }
        return o;
    }

    /// <summary>
    /// Joins an element where it meets the elements of its class on its
    /// storey, as drawing does: each free end to the nearest it ends on (a
    /// corner, an in-line element, or another's path), and the free ends of
    /// others that end on its path. A corner already joined is left as it is.
    /// </summary>
    /// <summary>How near ends must be to share a node of three or more.</summary>
    public const double NodeTolerance = 0.01;

    /// <param name="skip">Elements left out (what moved or was copied with it keeps its joins among them as they were, decision 531).</param>
    /// <param name="thickness">Each element's thickness as joins see it, kept across the calls of one command (null: worked out each call).</param>
    /// <remarks>
    /// An element whose main material stays out of joins (<see cref="Materials.Joins"/>, forum topic 59) neither joins nor is joined.
    /// Each end's <see cref="AutoKey"/> is kept (decision 546): an end that does not join automatically is not joined, nor is
    /// another's end brought to it; an element neither of whose ends does joins nothing, and nothing joins it along its run but
    /// an end stopping on it (it is left whole, as a T's through element is).
    /// </remarks>
    public static void Auto(IEdit edit, IPathElements paths, string id, JoinStyle style = JoinStyle.Mitre, IReadOnlySet<string>? skip = null,
        Dictionary<string, double>? thickness = null)
    {
        var m = edit.Model;
        // An element of a material that stays out of joins (forum topic 59) joins nothing automatically, nor is joined.
        if (m.Get(id) is not { } e || !paths.IsPath(e.Class) || !AutoAny(e) || !Materials.Joins(m, e)) return;
        var storey = Spatial.StoreyOf(m, id);
        // Only those near enough to meet are tried (a move of hundreds of walls tried each against the whole storey, decision 531):
        // two meet only within twice the thicker's thickness of each other's ends (WallJoins.Compute); the margin covers an end a join moves first.
        thickness ??= [];
        double Thick(Entity x) => thickness.TryGetValue(x.Id, out var t) ? t : thickness[x.Id] = paths.FrameOf(x, m)?.Thickness ?? double.PositiveInfinity;
        var straight = !PathCurve.Of(e).IsArc;
        var (s0, s1) = (Start(e), End(e));
        bool Near(Entity o) => !straight || PathCurve.Of(o).IsArc || SegmentsDistance(s0, s1, Start(o), End(o)) <= 6 * Math.Max(Thick(e), Thick(o)) + 0.05;
        var others = (storey == null ? m.OfClass(e.Class) : m.RelatedOf(storey.Id, IfcClass.RelContainedInSpatialStructure))
            .Where(o => o.Class == e.Class && o.Id != id && skip?.Contains(o.Id) != true && o.Attrs["start"] is JsonArray && o.Attrs["end"] is JsonArray
                && Near(o) && Between(m, id, o.Id) == null && Materials.Joins(m, o)).ToList();
        foreach (var end in new[] { 0, 1 })
        {
            e = m.Get(id)!;
            if (JoinedEnds(m, e)[end] || !AutoAt(e, end)) continue;
            var at = end == 0 ? Start(e) : End(e);
            var joinedHere = false;
            foreach (var other in others.OrderBy(o => DistanceTo(o, at)))
            {
                if (Between(m, id, other.Id) != null) continue;
                var current = m.Get(other.Id)!;
                if (Pair(paths, m, e, current) is not (var fe, var fo)) continue;
                WallJoin trial;
                try { trial = WallJoins.Compute(fe, fo, new JoinOptions(Style: style)); }
                catch (CommandException) { continue; }
                if (trial.A.Connection != (end == 0 ? Connection.AtStart : Connection.AtEnd)) continue;
                // An end already joined there: a node of three or more when the two ends are at the same point (each cut as a
                // fan, NodeAt), else left alone.
                var theirs = trial.B.Connection == Connection.AtStart ? 0 : 1;
                var node = trial.B.Connection != Connection.AtPath && ((theirs == 0 ? Start(current) : End(current)) - at).Length <= NodeTolerance;
                // Their end there does not join automatically (decision 546): it stays where it is.
                if (trial.B.Connection != Connection.AtPath && !AutoAt(current, theirs)) continue;
                if (trial.B.Connection != Connection.AtPath && JoinedEnds(m, current)[theirs] && !node) continue;
                if (joinedHere && !node) continue;
                Join(edit, paths, e, current, trial.Kind == JoinKind.L ? style : JoinStyle.Butt);
                joinedHere = true;
                // Only more ends at this very point join it after the first.
                if (!node) break;
            }
        }
        // Others whose free end lies on this element's path: a T, this one through.
        foreach (var other in others)
        {
            if (Between(m, id, other.Id) != null) continue;
            var current = m.Get(other.Id)!;
            e = m.Get(id)!;
            if (Pair(paths, m, e, current) is not (var fe, var fo)) continue;
            WallJoin trial;
            try { trial = WallJoins.Compute(fe, fo); }
            catch (CommandException) { continue; }
            if (trial.Kind != JoinKind.T || trial.A.Connection != Connection.AtPath) continue;
            var theirEnd = trial.B.Connection == Connection.AtStart ? 0 : 1;
            if (JoinedEnds(m, current)[theirEnd] || !AutoAt(current, theirEnd)) continue;
            Join(edit, paths, e, current);
        }
        // Others whose run this element's run crosses: an X, the one already there through (the thicker, when they differ).
        foreach (var other in others)
        {
            if (Between(m, id, other.Id) != null) continue;
            var current = m.Get(other.Id)!;
            e = m.Get(id)!;
            if (PathCurve.Of(e).IsArc || PathCurve.Of(current).IsArc || !AutoAny(current)) continue;
            if (Pair(paths, m, current, e) is not (var fo, var fe)) continue;
            WallJoin trial;
            try { trial = WallJoins.Compute(fo, fe); }
            catch (CommandException) { continue; }
            if (trial.Kind != JoinKind.X) continue;
            Join(edit, paths, current, e);
        }
    }

    /// <summary>How near two straight axes come (0 when they cross).</summary>
    static double SegmentsDistance(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        double Side(Vec2 p, Vec2 q, Vec2 r) => Vec2.Cross(q - p, r - p);
        var (d1, d2, d3, d4) = (Side(c, d, a), Side(c, d, b), Side(a, b, c), Side(a, b, d));
        if ((d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0) && d1 != 0 && d2 != 0 && d3 != 0 && d4 != 0) return 0;
        return Math.Min(Math.Min(Polyline2.SegmentDistance(a, c, d), Polyline2.SegmentDistance(b, c, d)),
            Math.Min(Polyline2.SegmentDistance(c, a, b), Polyline2.SegmentDistance(d, a, b)));
    }

    static double DistanceTo(Entity e, Vec2 p)
    {
        if (PathCurve.Of(e) is { IsArc: true } c) return c.Distance(p);
        var a = Start(e);
        var d = End(e) - a;
        var t = Math.Clamp(Vec2.Dot(p - a, d) / Math.Max(Vec2.Dot(d, d), 1e-12), 0, 1);
        return (a + d * t - p).Length;
    }

    /// <summary>
    /// Gives an element new axis ends. With <paramref name="moveJoinedEnds"/>,
    /// a moved end that is a corner (or an in-line joint) brings the other
    /// element's joined end along; without, the other holds the corner and the
    /// moved end goes back to it; detached, the moved end goes alone and the
    /// join stays only while the two still meet. Every join of what moved is worked out again:
    /// one that no longer holds goes, and the moved free ends join what they
    /// now meet.
    /// </summary>
    public static void MoveEnds(IEdit edit, IPathElements paths, Entity e, Vec2 start, Vec2 end, bool moveJoinedEnds = true, bool detach = false)
    {
        if ((end - start).Length < 1e-3) throw new CommandException("An element needs two different points: it would have no length.");
        var m = edit.Model;
        var oldStart = Start(e);
        var oldEnd = End(e);
        var moved = new HashSet<string> { e.Id };
        foreach (var r in Of(m, e.Id).ToList())
        {
            var mineConn = (string?)r.Attrs?[r.Relating == e.Id ? "relatingConnection" : "relatedConnection"];
            var theirsConn = (string?)r.Attrs?[r.Relating == e.Id ? "relatedConnection" : "relatingConnection"];
            if (theirsConn is not ("ATSTART" or "ATEND")) continue;
            var (from, to) = mineConn switch { "ATSTART" => (oldStart, start), "ATEND" => (oldEnd, end), _ => (default(Vec2?), default(Vec2)) };
            if (from == null || (to - from.Value).Length < 1e-12) continue;
            if (m.Get(Other(r, e.Id)) is not { } other) continue;
            if (detach) continue;
            if (moveJoinedEnds)
            {
                // The other's far end, where its joined end would go: it would have no length.
                var far = theirsConn == "ATSTART" ? End(other) : Start(other);
                if ((to - far).Length < 1e-3) throw new CommandException("The joined element would have no length: its end would land on its other end.");
                var attrs = other.CloneAttrs();
                attrs[theirsConn == "ATSTART" ? "start" : "end"] = new JsonArray(to.X, to.Y);
                edit.Replace(other.With(attrs: attrs));
                moved.Add(other.Id);
            }
            else if (mineConn == "ATSTART") start = from.Value;
            else end = from.Value;
        }
        var current = m.Get(e.Id)!;
        var next = current.CloneAttrs();
        next["start"] = new JsonArray(start.X, start.Y);
        next["end"] = new JsonArray(end.X, end.Y);
        edit.Replace(current.With(attrs: next));
        Refresh(edit, paths, moved);
        foreach (var id in moved) Auto(edit, paths, id);
    }

    /// <summary>Works out these elements' joins again: still meeting, they are rewritten (axes to the crossing); not, they go.</summary>
    /// <param name="only">The joins to work out again (all of them when null).</param>
    public static void Refresh(IEdit edit, IPathElements paths, IEnumerable<string> ids, Func<Relation, bool>? only = null)
    {
        var m = edit.Model;
        foreach (var r in ids.SelectMany(id => Of(m, id)).DistinctBy(r => r.Id).Where(r => only?.Invoke(r) != false).ToList())
        {
            if (m.GetRelation(r.Id) is not { } current) continue;
            if (Compute(m, current, paths, out _) == null) { edit.RemoveRelation(current.Id); continue; }
            Join(edit, paths, m.Get(current.Relating)!, m.Get(current.Related[0])!, StyleOf(current), 'a');
        }
    }

    /// <summary>Restyles the corners (L) and in-line joins of the elements: mitre, butt, or the other through; a crossing (X) only takes the other through.</summary>
    public static int Restyle(IEdit edit, IPathElements paths, IEnumerable<string> ids, JoinStyle? style, bool swap)
    {
        var m = edit.Model;
        var count = 0;
        foreach (var r in ids.SelectMany(id => Of(m, id)).DistinctBy(r => r.Id).ToList())
        {
            var kind = (string?)r.Attrs?["kind"];
            if (kind == "T" || kind == "X" && !swap) continue;
            if (m.GetRelation(r.Id) is not { } current) continue;
            // A join that no longer meets (a type made thicker since) is left for Refresh, not restyled: Join would refuse the whole restyle.
            if (Compute(m, current, paths, out _) == null) continue;
            Join(edit, paths, m.Get(current.Relating)!, m.Get(current.Related[0])!, style ?? StyleOf(current), swap ? 'b' : 'a');
            count++;
        }
        return count;
    }
}
