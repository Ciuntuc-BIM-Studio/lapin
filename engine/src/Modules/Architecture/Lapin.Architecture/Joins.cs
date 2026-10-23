using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Wall joins in the model. A join is an IfcRelConnectsPathElements: the
/// wall that runs through relates (the first wall of an in-line join), the
/// other is related; its attributes say how they connect (ATSTART, ATEND,
/// ATPATH), its kind and its style (butt or mitre). That is all that is
/// stored: each wall's cuts are worked out from it, and from both walls as
/// they are now, whenever the wall's geometry is built, so an edit to either
/// wall keeps the corner clean.
/// </summary>
public static class Joins
{
    public const string Class = IfcClass.RelConnectsPathElements;

    public static IEnumerable<Relation> Of(IModelView m, string wallId) =>
        m.RelationsOf(wallId, Class);

    public static Relation? Between(IModelView m, string a, string b) =>
        Of(m, a).FirstOrDefault(r => r.Touches(b));

    public static string Other(Relation r, string wallId) => r.Relating == wallId ? r.Related[0] : r.Relating;

    public static JoinStyle StyleOf(Relation r) => (string?)r.Attrs?["style"] == "mitre" ? JoinStyle.Mitre : JoinStyle.Butt;

    /// <summary>A wall as the join geometry sees it: its axis and its body's extent across it.</summary>
    public static JoinWall Frame(IModelView m, Entity wall)
    {
        var bands = Walls.Section(m, wall);
        return new JoinWall(Walls.Start(wall), Walls.End(wall), bands.Min(b => b.From), bands.Max(b => b.To));
    }

    /// <summary>The join as it is now, from both walls; null when they no longer meet.</summary>
    public static WallJoin? Compute(IModelView m, Relation r, out string? problem)
    {
        problem = null;
        if (m.Get(r.Relating) is not { Class: IfcClass.Wall } a || r.Related.Count == 0 || m.Get(r.Related[0]) is not { Class: IfcClass.Wall } b)
        {
            problem = "A join between walls that are not both there.";
            return null;
        }
        try { return WallJoins.Compute(Frame(m, a), Frame(m, b), new JoinOptions(Priority: 'a', Style: StyleOf(r))); }
        catch (CommandException ex) { problem = ex.Message; return null; }
    }

    /// <summary>Where each end of the wall meets its joins: the joint, and the cut there (none: square at its own end).</summary>
    public static (Vec2 Point, EndCut Cut)?[] EndsOf(IModelView m, Entity wall, List<string> problems)
    {
        var ends = new (Vec2, EndCut)?[2];
        foreach (var r in Of(m, wall.Id))
        {
            var join = Compute(m, r, out var problem);
            if (join == null) { problems.Add(problem!); continue; }
            var side = r.Relating == wall.Id ? join.A : join.B;
            var i = side.Connection switch { Connection.AtStart => 0, Connection.AtEnd => 1, _ => -1 };
            if (i < 0 || ends[i] != null) continue;
            ends[i] = i == 0 ? (side.Wall.Start, side.Wall.StartCut ?? EndCut.Square) : (side.Wall.End, side.Wall.EndCut ?? EndCut.Square);
        }
        return ends;
    }

    static string Name(Connection c) => c switch { Connection.AtStart => "ATSTART", Connection.AtEnd => "ATEND", _ => "ATPATH" };

    /// <summary>
    /// Joins two walls: the join is worked out (refused with the reason when
    /// they do not meet), their joined axis ends brought to the crossing, and
    /// the relationship written, in place of any the pair already had. Without
    /// a priority, the wall that ran through before still does.
    /// </summary>
    public static WallJoin Join(IEdit edit, Entity a, Entity b, JoinStyle? style = null, char? priority = null)
    {
        if (a.Id == b.Id) throw new CommandException("A wall cannot be joined to itself.");
        var existing = Between(edit.Model, a.Id, b.Id);
        priority ??= existing == null ? null : existing.Relating == a.Id ? 'a' : 'b';
        var join = WallJoins.Compute(Frame(edit.Model, a), Frame(edit.Model, b), new JoinOptions(Priority: priority, Style: style ?? (existing != null ? StyleOf(existing) : JoinStyle.Butt)));
        MoveAxis(edit, a, join.A.Wall.Start, join.A.Wall.End);
        MoveAxis(edit, b, join.B.Wall.Start, join.B.Wall.End);
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

    static void MoveAxis(IEdit edit, Entity wall, Vec2 start, Vec2 end)
    {
        var current = edit.Model.Get(wall.Id)!;
        if ((Walls.Start(current) - start).Length < 1e-12 && (Walls.End(current) - end).Length < 1e-12) return;
        var attrs = current.CloneAttrs();
        attrs["start"] = new JsonArray(start.X, start.Y);
        attrs["end"] = new JsonArray(end.X, end.Y);
        edit.Replace(current.With(attrs: attrs));
    }

    public static void Unjoin(IEdit edit, string a, string b)
    {
        if (Between(edit.Model, a, b) is { } r) edit.RemoveRelation(r.Id);
    }

    /// <summary>The ends of the wall that a join already takes (start, end).</summary>
    static bool[] JoinedEnds(IModelView m, Entity wall)
    {
        var o = new bool[2];
        foreach (var r in Of(m, wall.Id))
        {
            var connection = (string?)r.Attrs?[r.Relating == wall.Id ? "relatingConnection" : "relatedConnection"];
            if (connection == "ATSTART") o[0] = true;
            if (connection == "ATEND") o[1] = true;
        }
        return o;
    }

    /// <summary>
    /// Joins a wall where it meets the walls of its storey, as drawing does:
    /// each free end to the nearest wall it ends on (a corner, an in-line
    /// wall, or another's path), and the free ends of other walls that end on
    /// its path. A corner already joined is left as it is.
    /// </summary>
    public static void Auto(IEdit edit, string wallId, JoinStyle style = JoinStyle.Butt)
    {
        var m = edit.Model;
        if (m.Get(wallId) is not { Class: IfcClass.Wall } wall) return;
        var storey = Spatial.StoreyOf(m, wallId);
        var others = (storey == null ? m.OfClass(IfcClass.Wall) : m.RelatedOf(storey.Id, IfcClass.RelContainedInSpatialStructure))
            .Where(o => o.Class == IfcClass.Wall && o.Id != wallId && Between(m, wallId, o.Id) == null).ToList();
        foreach (var end in new[] { 0, 1 })
        {
            wall = m.Get(wallId)!;
            if (JoinedEnds(m, wall)[end]) continue;
            var at = end == 0 ? Walls.Start(wall) : Walls.End(wall);
            foreach (var other in others.OrderBy(o => DistanceTo(o, at)))
            {
                if (Between(m, wallId, other.Id) != null) continue;
                var current = m.Get(other.Id)!;
                WallJoin trial;
                try { trial = WallJoins.Compute(Frame(m, wall), Frame(m, current), new JoinOptions(Style: style)); }
                catch (CommandException) { continue; }
                var mine = trial.A.Connection;
                if (mine != (end == 0 ? Connection.AtStart : Connection.AtEnd)) continue;
                // The other wall's end must be free too (a corner of three is not joined automatically).
                if (trial.B.Connection != Connection.AtPath && JoinedEnds(m, current)[trial.B.Connection == Connection.AtStart ? 0 : 1]) continue;
                Join(edit, wall, current, trial.Kind == JoinKind.L ? style : JoinStyle.Butt);
                break;
            }
        }
        // Other walls whose free end lies on this wall's path: a T, this wall through.
        foreach (var other in others)
        {
            if (Between(m, wallId, other.Id) != null) continue;
            var current = m.Get(other.Id)!;
            wall = m.Get(wallId)!;
            WallJoin trial;
            try { trial = WallJoins.Compute(Frame(m, wall), Frame(m, current)); }
            catch (CommandException) { continue; }
            if (trial.Kind != JoinKind.T || trial.A.Connection != Connection.AtPath) continue;
            if (JoinedEnds(m, current)[trial.B.Connection == Connection.AtStart ? 0 : 1]) continue;
            Join(edit, wall, current);
        }
    }

    static double DistanceTo(Entity wall, Vec2 p)
    {
        var a = Walls.Start(wall);
        var b = Walls.End(wall);
        var d = b - a;
        var t = Math.Clamp(Vec2.Dot(p - a, d) / Math.Max(Vec2.Dot(d, d), 1e-12), 0, 1);
        return (a + d * t - p).Length;
    }

    /// <summary>
    /// Gives a wall new axis ends. With <paramref name="moveJoinedEnds"/>, a
    /// moved end that is a corner (or an in-line joint) brings the other
    /// wall's joined end along; without, the other wall holds the corner and
    /// the moved end goes back to it. Every join of the walls that moved is
    /// worked out again: one that no longer holds goes, and the moved wall's
    /// free ends join what they now meet.
    /// </summary>
    public static void MoveEnds(IEdit edit, Entity wall, Vec2 start, Vec2 end, bool moveJoinedEnds = true)
    {
        if ((end - start).Length < 1e-3) throw new CommandException("A wall needs two different points: it would have no length.");
        var m = edit.Model;
        var oldStart = Walls.Start(wall);
        var oldEnd = Walls.End(wall);
        var moved = new HashSet<string> { wall.Id };
        foreach (var r in Of(m, wall.Id).ToList())
        {
            var mineConn = (string?)r.Attrs?[r.Relating == wall.Id ? "relatingConnection" : "relatedConnection"];
            var theirsConn = (string?)r.Attrs?[r.Relating == wall.Id ? "relatedConnection" : "relatingConnection"];
            if (theirsConn is not ("ATSTART" or "ATEND")) continue;
            var (from, to) = mineConn switch { "ATSTART" => (oldStart, start), "ATEND" => (oldEnd, end), _ => (default(Vec2?), default(Vec2)) };
            if (from == null || (to - from.Value).Length < 1e-12) continue;
            if (m.Get(Other(r, wall.Id)) is not { } other) continue;
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
        var current = m.Get(wall.Id)!;
        var next = current.CloneAttrs();
        next["start"] = new JsonArray(start.X, start.Y);
        next["end"] = new JsonArray(end.X, end.Y);
        edit.Replace(current.With(attrs: next));
        Refresh(edit, moved);
        foreach (var id in moved) Auto(edit, id);
    }

    /// <summary>Works out the joins of these walls again: still meeting, they are rewritten (axes to the crossing); not, they go.</summary>
    public static void Refresh(IEdit edit, IEnumerable<string> wallIds)
    {
        var m = edit.Model;
        foreach (var r in wallIds.SelectMany(id => Of(m, id)).DistinctBy(r => r.Id).ToList())
        {
            if (m.GetRelation(r.Id) is not { } current) continue;
            if (Compute(m, current, out _) == null) { edit.RemoveRelation(current.Id); continue; }
            Join(edit, m.Get(current.Relating)!, m.Get(current.Related[0])!, StyleOf(current), 'a');
        }
    }

    /// <summary>Restyles the corners (L) and in-line joins of the walls: mitre, butt, or the other wall through.</summary>
    public static int Restyle(IEdit edit, IEnumerable<string> wallIds, JoinStyle? style, bool swap)
    {
        var m = edit.Model;
        var count = 0;
        foreach (var r in wallIds.SelectMany(id => Of(m, id)).DistinctBy(r => r.Id).ToList())
        {
            if ((string?)r.Attrs?["kind"] == "T") continue;
            if (m.GetRelation(r.Id) is not { } current) continue;
            var a = m.Get(current.Relating)!;
            var b = m.Get(current.Related[0])!;
            Join(edit, a, b, style ?? StyleOf(current), swap ? 'b' : 'a');
            count++;
        }
        return count;
    }
}
