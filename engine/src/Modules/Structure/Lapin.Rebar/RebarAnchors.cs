using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// The anchor grips of a bar (docs/plan-rebar.md, decision 226), after the configurator: a selected bar seen from the
/// front shows, beside its shape's grips, three anchors standing <em>outside</em> it by its cover — at its origin and at
/// its two ends. They stand for the formwork's face: an anchor dragged onto the element's cut (its corner, its face)
/// puts the bar the cover inside. In the bar's own frame the origin anchor is at (−cover, −cover); an end anchor is the
/// end's vertex pushed on along its segment by the cover and off it by the cover, to the right of the bar walked from
/// its start (below a straight bar: the configurator's (−c, −c) and (L + c, −c)). An end with a hook has no anchor, a
/// closed shape (a stirrup) none at its ends, a polyline or a dot none at all. The cover is the bar's own, else its
/// host type's stirrup cover plus the stirrup's diameter when the type has stirrups, else the project's.
/// </summary>
public static class RebarAnchors
{
    public const string Origin = "anchor:origin";
    public const string Start = "anchor:start";
    public const string End = "anchor:end";

    /// <summary>An anchor: its key, where it stands in the view, and where it stands in the bar's own frame.</summary>
    public sealed record Anchor(string Key, Vec2 At, Vec2 Local);

    public static bool IsAnchor(string? key) => key != null && key.StartsWith("anchor:", StringComparison.Ordinal);

    /// <summary>The cover a bar's anchors stand off by (m), and where it comes from: <c>own</c>, <c>host</c> or <c>settings</c>.</summary>
    public static (double Cover, string Source) CoverOf(IModelView m, Entity e, RebarSettings st)
    {
        if (e.NumOrNull("cover") is { } own) return (own, "own");
        if (e.Str("host") is { } hostId && m.Get(hostId) is { } host && TypeStirrups.SpecOf(m, host) is { } sp)
            return (sp.Cover + (sp.Enabled ? sp.Diameter : 0), "host");
        return (st.Cover, "settings");
    }

    /// <summary>The right-hand normal of a direction: off the bar, walked from its start, for a shape that turns left (an L, a U, a stirrup).</summary>
    static Vec2 Right(Vec2 u) => new(u.Y, -u.X);

    /// <summary>The shape's corners alone (no hooks): what the anchors stand off.</summary>
    static IReadOnlyList<Vec2> Corners(RebarSpec s) => RebarShapes.Def(s.Shape).Vertices(s.Params);

    /// <summary>The anchors in the bar's own frame.</summary>
    public static List<(string Key, Vec2 Local)> LocalAnchors(RebarSpec s, double cover)
    {
        var def = RebarShapes.Def(s.Shape);
        if (def.FreeVertices || s.Shape == "dot") return [];
        var o = new List<(string, Vec2)> { (Origin, new Vec2(-cover, -cover)) };
        var pts = Corners(s);
        if (pts.Count < 2 || (pts[0] - pts[^1]).Length < 1e-9) return o;
        var (start, end) = RebarShapes.HooksOf(s);
        // The start's anchor only where the start is not the origin (a U's first leg): the origin's anchor stands there otherwise.
        if (start == null && pts[0].Length > 1e-6)
        {
            var u = (pts[1] - pts[0]).Normalized();
            o.Add((Start, pts[0] - u * cover + Right(u) * cover));
        }
        if (end == null && pts[^1].Length > 1e-6)
        {
            var u = (pts[^1] - pts[^2]).Normalized();
            o.Add((End, pts[^1] + u * cover + Right(u) * cover));
        }
        return o;
    }

    /// <summary>The anchors where they stand in the view (through the bar's rotation and mirrors).</summary>
    public static List<Anchor> Anchors(RebarSpec s, Vec2 at, double cover) =>
        LocalAnchors(s, cover).Select(a => new Anchor(a.Key, RebarShapes.Place(s, at, a.Local), a.Local)).ToList();

    /// <summary>
    /// An anchor dragged to <paramref name="point"/> (the view's coordinates): the origin's moves the bar so that the anchor lands
    /// there; an end's sets the parameter that end runs on so that the end lands the cover inside the point (never under the
    /// parameter's least); the start's of a shape starting at its origin moves the bar along its first segment and takes the
    /// same off that segment's length, so the far corner stays.
    /// </summary>
    public static (Vec2 At, RebarSpec Spec) Drag(RebarSpec s, Vec2 at, double cover, string key, Vec2 point)
    {
        var anchors = LocalAnchors(s, cover);
        var pts = Corners(s);
        if (key == Origin && anchors.Count > 0)
            return (point - (RebarShapes.Place(s, at, anchors[0].Local) - at), s);
        if (key == End && anchors.Any(a => a.Key == End))
        {
            var u = (pts[^1] - pts[^2]).Normalized();
            var corrected = RebarShapes.Local(s, at, point) - (u * cover + Right(u) * cover);
            return (at, RebarShapes.Apply(s, GripFor(s, pts[^1], u), corrected));
        }
        if (key == Start)
        {
            var u = (pts[1] - pts[0]).Normalized();
            var corrected = RebarShapes.Local(s, at, point) - (-u * cover + Right(u) * cover);
            if (anchors.Any(a => a.Key == Start)) return (at, RebarShapes.Apply(s, GripFor(s, pts[0], u), corrected));
            if (pts.Count >= 2 && RebarShapes.HooksOf(s).Start == null && (pts[0] - pts[^1]).Length > 1e-9)
            {
                // The start is the origin: the bar moves along its first segment by what the start moves, its first corner staying.
                var shift = u * Vec2.Dot(corrected - pts[0], u);
                var next = RebarShapes.Apply(s, GripFor(s, pts[1], u), pts[1] - shift);
                var kept = RebarShapes.Def(s.Shape).Vertices(next.Params)[1];
                // Clamped at the parameter's least, the far corner would move: the bar goes only as far as keeps it.
                var moved = u * Vec2.Dot(pts[1] - kept, u);
                return (RebarShapes.Place(s, at, moved), next);
            }
        }
        throw new CommandException($"The bar has no anchor „{key}”.");
    }

    /// <summary>The shape's grip that drives the coordinate of <paramref name="vertex"/> along <paramref name="u"/>'s axis.</summary>
    static string GripFor(RebarSpec s, Vec2 vertex, Vec2 u)
    {
        var axis = Math.Abs(u.X) >= Math.Abs(u.Y) ? 'x' : 'y';
        double Along(Vec2 p) => axis == 'x' ? p.X : p.Y;
        var grip = RebarShapes.Grips(s).FirstOrDefault(g => g.Axis == axis && Math.Abs(Along(g.At) - Along(vertex)) < 1e-9)
            ?? throw new CommandException("That end of the bar has no parameter to set.");
        return grip.Key;
    }
}
