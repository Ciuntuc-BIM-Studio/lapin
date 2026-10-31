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

    public static Vec2 Start(Entity e) => Point(e.Attrs["start"]);
    public static Vec2 End(Entity e) => Point(e.Attrs["end"]);

    static Vec2 Point(JsonNode? n) =>
        n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : default;

    public static IEnumerable<Relation> Of(IModelView m, string id) => m.RelationsOf(id, Class);

    public static Relation? Between(IModelView m, string a, string b) => Of(m, a).FirstOrDefault(r => r.Touches(b));

    public static string Other(Relation r, string id) => r.Relating == id ? r.Related[0] : r.Relating;

    public static JoinStyle StyleOf(Relation r) => (string?)r.Attrs?["style"] == "mitre" ? JoinStyle.Mitre : JoinStyle.Butt;

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
        try { return WallJoins.Compute(fa, fb, new JoinOptions(Priority: 'a', Style: StyleOf(r))); }
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
        return ends;
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
    public static WallJoin Join(IEdit edit, IPathElements paths, Entity a, Entity b, JoinStyle? style = null, char? priority = null)
    {
        if (a.Id == b.Id) throw new CommandException("An element cannot be joined to itself.");
        if (a.Class != b.Class) throw new CommandException($"A {a.Class} joins only a {a.Class}.");
        var existing = Between(edit.Model, a.Id, b.Id);
        priority ??= existing == null ? null : existing.Relating == a.Id ? 'a' : 'b';
        // A curved element and a straight one: the arc runs through (on along its tangent), the straight one stops at its face.
        if (priority == null && PathCurve.Of(a).IsArc != PathCurve.Of(b).IsArc) priority = PathCurve.Of(a).IsArc ? 'a' : 'b';
        var (fa, fb) = Tangents(a, b, existing, Frame(paths, edit.Model, a), Frame(paths, edit.Model, b));
        var join = WallJoins.Compute(fa, fb, new JoinOptions(Priority: priority, Style: style ?? (existing != null ? StyleOf(existing) : JoinStyle.Mitre)));
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

    public static void Unjoin(IEdit edit, string a, string b)
    {
        if (Between(edit.Model, a, b) is { } r) edit.RemoveRelation(r.Id);
    }

    /// <summary>The ends of the element that a join already takes (start, end).</summary>
    static bool[] JoinedEnds(IModelView m, Entity e)
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
    public static void Auto(IEdit edit, IPathElements paths, string id, JoinStyle style = JoinStyle.Mitre)
    {
        var m = edit.Model;
        if (m.Get(id) is not { } e || !paths.IsPath(e.Class)) return;
        var storey = Spatial.StoreyOf(m, id);
        var others = (storey == null ? m.OfClass(e.Class) : m.RelatedOf(storey.Id, IfcClass.RelContainedInSpatialStructure))
            .Where(o => o.Class == e.Class && o.Id != id && Between(m, id, o.Id) == null).ToList();
        foreach (var end in new[] { 0, 1 })
        {
            e = m.Get(id)!;
            if (JoinedEnds(m, e)[end]) continue;
            var at = end == 0 ? Start(e) : End(e);
            foreach (var other in others.OrderBy(o => DistanceTo(o, at)))
            {
                if (Between(m, id, other.Id) != null) continue;
                var current = m.Get(other.Id)!;
                if (Pair(paths, m, e, current) is not (var fe, var fo)) continue;
                WallJoin trial;
                try { trial = WallJoins.Compute(fe, fo, new JoinOptions(Style: style)); }
                catch (CommandException) { continue; }
                if (trial.A.Connection != (end == 0 ? Connection.AtStart : Connection.AtEnd)) continue;
                // The other's end must be free too (a corner of three is not joined automatically).
                if (trial.B.Connection != Connection.AtPath && JoinedEnds(m, current)[trial.B.Connection == Connection.AtStart ? 0 : 1]) continue;
                Join(edit, paths, e, current, trial.Kind == JoinKind.L ? style : JoinStyle.Butt);
                break;
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
            if (JoinedEnds(m, current)[trial.B.Connection == Connection.AtStart ? 0 : 1]) continue;
            Join(edit, paths, e, current);
        }
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
    public static void Refresh(IEdit edit, IPathElements paths, IEnumerable<string> ids)
    {
        var m = edit.Model;
        foreach (var r in ids.SelectMany(id => Of(m, id)).DistinctBy(r => r.Id).ToList())
        {
            if (m.GetRelation(r.Id) is not { } current) continue;
            if (Compute(m, current, paths, out _) == null) { edit.RemoveRelation(current.Id); continue; }
            Join(edit, paths, m.Get(current.Relating)!, m.Get(current.Related[0])!, StyleOf(current), 'a');
        }
    }

    /// <summary>Restyles the corners (L) and in-line joins of the elements: mitre, butt, or the other through.</summary>
    public static int Restyle(IEdit edit, IPathElements paths, IEnumerable<string> ids, JoinStyle? style, bool swap)
    {
        var m = edit.Model;
        var count = 0;
        foreach (var r in ids.SelectMany(id => Of(m, id)).DistinctBy(r => r.Id).ToList())
        {
            if ((string?)r.Attrs?["kind"] == "T") continue;
            if (m.GetRelation(r.Id) is not { } current) continue;
            Join(edit, paths, m.Get(current.Relating)!, m.Get(current.Related[0])!, style ?? StyleOf(current), swap ? 'b' : 'a');
            count++;
        }
        return count;
    }
}
