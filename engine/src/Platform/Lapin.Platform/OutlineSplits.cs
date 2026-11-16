using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// An element with a closed outline (corners and each edge's rise, <see cref="ArcOutline"/>) in the platform's split and trim
/// by a line (<see cref="OutlineSplits"/>, audit 2026-11-15 §6 and §15): floors and ceilings, roofs, drawn rooms, platforms,
/// a terrain's parcel. Each module says where it keeps the outline and keeps right what goes by edge or by place on it.
/// </summary>
public interface IOutlinePieces
{
    bool Handles(Entity e);

    /// <summary>Why the element cannot be cut this way (<paramref name="split"/>: into two; else trimmed); null when it can.</summary>
    string? Refuse(IModelView m, Entity e, bool split) => null;

    /// <summary>The attributes holding its corners and its edges' rises.</summary>
    string Key => "contour";
    string ArcsKey => ArcOutline.Key;

    /// <summary>What it is, for a refusal (“A floor”).</summary>
    string What(Entity e) => "An outline";

    /// <summary>The elements it hosts by a loop in plan (a floor's openings): each on one side of the line, never across.</summary>
    IEnumerable<(Entity Element, IReadOnlyList<Vec2> Contour)> Hosted(IModelView m, Entity e) => [];

    /// <summary>
    /// The attributes of one part (<paramref name="attrs"/>, a copy of the element's with the part's outline written):
    /// <paramref name="first"/> the part that keeps the element's id; <paramref name="on"/> whether a plan point lies on this part's side of the line.
    /// </summary>
    void Piece(ICommandContext ctx, Entity e, JsonObject attrs, IReadOnlyList<Vec2> corners, double[]? rises, bool first, Func<Vec2, bool> on) { }

    /// <summary>What hangs on the element otherwise, once the parts are written (<paramref name="second"/>: null for a trim).</summary>
    void Rehost(ICommandContext ctx, Entity e, Entity? second, Func<Vec2, bool> onFirst) { }
}

/// <summary>
/// An outline split by a line into two elements, or trimmed by it (audit 2026-11-15, §6 and §15; <see cref="OutlineCuts"/>).
/// The element keeps its GlobalId and the part on the side picked (without a pick, the larger); the other part of a split is a
/// new element like it (its type, storey, layers, properties, groups). Each keeps its edges' arcs; its own holes and the
/// elements it hosts by a loop go to the part they lie in, and one the line crosses refuses (as one in the part a trim cuts
/// away). An outline with an elliptical edge is refused. One undo step.
/// </summary>
public static class OutlineSplits
{
    public static void Register(IModuleRegistry r)
    {
        // {id, a: [x, y], b: [x, y] (the line, run on past them), keep?: [x, y] (the side the element keeps)} → {ids: [element, new]}.
        r.Command("element.splitOutline", "Split outline", (ctx, a) => Cut(ctx, a, split: true));
        // {id, a, b, keep: [x, y] (the side kept)} → {id}.
        r.Command("element.trimOutline", "Trim outline", (ctx, a) => Cut(ctx, a, split: false));
    }

    public static JsonObject Cut(ICommandContext ctx, JsonObject a, bool split)
    {
        var m = ctx.Model;
        var e = m.Get((string?)a["id"] ?? "") ?? throw new CommandException("The element is not in the project.");
        var h = ctx.Services.All<IOutlinePieces>().FirstOrDefault(x => x.Handles(e))
            ?? throw new CommandException($"„{e.Name}” is not cut by a line: floors, ceilings, roofs, drawn rooms, platforms and parcels are.");
        if (StoredBodies.Has(e) || e.Attrs["reference"] != null) throw new CommandException($"„{e.Name}” is a reference from a file: convert it to native first.");
        Pins.Refuse([e]);
        if (h.Refuse(m, e, split) is { } why) throw new CommandException(why);
        var corners = Args.StoredPoints(e.Attrs[h.Key]);
        if (corners.Count < 3) throw new CommandException($"„{e.Name}” has no outline.");
        if (h.Key == "contour" && EllipseEdge.Of(e) is { } el && el.Any(x => x != null)) throw new CommandException($"„{e.Name}” has an elliptical edge: it is not cut by a line yet.");
        var (p, q) = (Args.TryPoint(a["a"]) ?? throw new CommandException("The line's first point?"), Args.TryPoint(a["b"]) ?? throw new CommandException("The line's second point?"));
        var keep = Args.TryPoint(a["keep"]);
        if (!split && keep == null) throw new CommandException("Which side to keep? A point on it.");
        var (left, right) = OutlineCuts.Split(corners, ArcOutline.Rises(e.Attrs[h.ArcsKey], corners.Count), p, q);
        var dir = (q - p).Normalized();
        bool LeftOf(Vec2 x) => Vec2.Cross(dir, x - p) > 0;
        var keepLeft = keep is { } k ? LeftOf(k) : Math.Abs(ArcOutline.SignedArea(left.Corners, left.Rises)) >= Math.Abs(ArcOutline.SignedArea(right.Corners, right.Rises));
        var (mine, other) = keepLeft ? (left, right) : (right, left);
        var what = h.What(e);
        var (mc, mr) = ArcOutline.Check(mine.Corners, mine.Rises, what);
        var (oc, orr) = ArcOutline.Check(other.Corners, other.Rises, what);
        bool OnFirst(Vec2 x) => LeftOf(x) == keepLeft;
        // Which side a loop lies on, refusing one across.
        bool? Side(IReadOnlyList<Vec2> contour, string label)
        {
            var sides = contour.Select(x => Vec2.Cross(dir, x - p)).ToList();
            if (sides.Any(s => s > 1e-6) && sides.Any(s => s < -1e-6)) throw new CommandException($"The line crosses {label}: draw it beside it.");
            return sides.Sum() > 0 == keepLeft;
        }

        // The elements it hosts by a loop: on one side or the other of the line, never across.
        var toOther = new List<Entity>();
        foreach (var (o, contour) in h.Hosted(m, e).ToList())
        {
            if (Side(contour, $"the opening „{o.Name}”") == true) continue;
            if (!split) throw new CommandException($"The opening „{o.Name}” lies in the part cut away: delete it first, or keep that side.");
            toOther.Add(o);
        }
        // Its own holes.
        var holes = h.Key == "contour" ? OutlineHoles.Of(e) : [];
        var (myHoles, otherHoles) = (new List<OutlineHoles.Hole>(), new List<OutlineHoles.Hole>());
        foreach (var hole in holes)
        {
            if (Side(hole.Points, $"a hole of „{e.Name}”") == true) { myHoles.Add(hole); continue; }
            if (!split) throw new CommandException($"A hole of „{e.Name}” lies in the part cut away: take it away first, or keep that side.");
            otherHoles.Add(hole);
        }

        JsonObject Attrs(IReadOnlyList<Vec2> c, double[]? r, List<OutlineHoles.Hole> hs, bool first)
        {
            var x = e.CloneAttrs();
            x[h.Key] = new JsonArray([.. c.Select(v => (JsonNode)new JsonArray(v.X, v.Y))]);
            ArcOutline.Write(x, r, h.ArcsKey);
            if (h.Key == "contour") { OutlineHoles.Write(x, hs); EllipseEdge.Write(x, null); }
            h.Piece(ctx, e, x, c, r, first, first ? OnFirst : v => !OnFirst(v));
            return x;
        }
        var attrs = Attrs(mc, mr, myHoles, true);
        if (!split)
        {
            ctx.Replace(e.With(attrs: attrs));
            h.Rehost(ctx, e, null, OnFirst);
            PathEdits.Follow(ctx, e, [new PathPiece(e.Id, 0, 0)]);
            return new JsonObject { ["id"] = e.Id };
        }
        var otherAttrs = Attrs(oc, orr, otherHoles, false);
        ctx.Replace(e.With(attrs: attrs));
        var second = PathEdits.AddLike(ctx, e, otherAttrs);
        foreach (var o in toOther)
            foreach (var r in ctx.Model.RelationsOf(o.Id, IfcClass.RelVoidsElement).Where(r => r.Relating == e.Id).ToList())
            {
                if (r.Related.Count == 1) ctx.ReplaceRelation(r with { Relating = second.Id });
                else
                {
                    ctx.ReplaceRelation(r with { Related = [.. r.Related.Where(x => x != o.Id)] });
                    ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, second.Id, [o.Id]));
                }
            }
        h.Rehost(ctx, e, ctx.Model.Get(second.Id), OnFirst);
        PathEdits.Follow(ctx, e, [new PathPiece(e.Id, 0, 0), new PathPiece(second.Id, 0, 0)]);
        return new JsonObject { ["ids"] = new JsonArray(e.Id, second.Id) };
    }
}
