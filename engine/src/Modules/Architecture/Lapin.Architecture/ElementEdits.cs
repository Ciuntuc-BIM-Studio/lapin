using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A wall's part in the platform's split, trim and extend (<see cref="PathEdits"/>, audit 2026-11-15, §6): its layer ends and its
/// layer extents at an end stay with the end they belong to (a cut's new end has none); its openings go to the part they fall
/// on, placed from its start, and one across a cut, or where the wall is cut away, refuses; its own sweeps and reveals are
/// cut with it (a copy on each part they run along), its type's are given to the new part as to any wall. A wall with a profile
/// of its own along its top is refused: the profile would not follow.
/// </summary>
public sealed class WallPieces : IPathPieces
{
    public bool Handles(Entity e) => e.Class == IfcClass.Wall;

    public string? Refuse(IModelView m, Entity e) =>
        e.Attrs[WallTops.TopProfileKey] != null ? $"„{e.Name}” has a profile of its own along its top: make its top flat first." : null;

    public void Piece(IModelView m, Entity e, JsonObject attrs, double s0, double s1, bool newStart, bool newEnd)
    {
        if (!newStart && !newEnd) return;
        if (attrs[Walls.LayerEndsKey] is JsonArray ends)
        {
            var kept = new JsonArray();
            var any = false;
            foreach (var layer in ends)
            {
                if (layer is not JsonArray { Count: 4 } x) { kept.Add((JsonNode?)layer?.DeepClone()); continue; }
                var v = x.Select(n => Json.Number(n) ?? 0).ToArray();
                if (newStart) v[0] = v[1] = 0;
                if (newEnd) v[2] = v[3] = 0;
                any |= v.Any(d => Math.Abs(d) > 1e-12);
                kept.Add((JsonNode)new JsonArray(v[0], v[1], v[2], v[3]));
            }
            if (any) attrs[Walls.LayerEndsKey] = kept; else attrs.Remove(Walls.LayerEndsKey);
        }
        var extents = LayerExtents.Of(e);
        if (extents.Count > 0)
        {
            var next = extents.Where(x => !(newStart && x.Edge == LayerExtents.Start) && !(newEnd && x.Edge == LayerExtents.End)).ToList();
            if (LayerExtents.ToJson(next) is { } json) attrs[LayerExtents.Key] = json; else attrs.Remove(LayerExtents.Key);
        }
    }

    public void Rehost(ICommandContext ctx, Entity e, IReadOnlyList<PathPiece> pieces)
    {
        var m = ctx.Model;
        var length = PathCurve.Of(e).Length;
        // Openings: each on the part it falls on.
        var moves = new List<(Openings.Placed O, PathPiece To)>();
        foreach (var o in Openings.PlacedOn(m, e.Id))
        {
            var (x0, x1) = (o.Along - o.Width / 2, o.Along + o.Width / 2);
            var name = o.Fill?.Name ?? o.Opening.Name;
            var on = pieces.Where(p => x0 >= p.S0 - 1e-6 && x1 <= p.S1 + 1e-6).Select(p => (PathPiece?)p).FirstOrDefault();
            if (on is not { } piece)
            {
                if (pieces.Any(p => x0 < p.S1 - 1e-6 && x1 > p.S0 + 1e-6))
                    throw new CommandException(pieces.Count > 1
                        ? $"„{name}” lies across the cut in „{e.Name}”: split it elsewhere, or move the opening first."
                        : $"„{name}” in „{e.Name}” would be cut by the new end: move it first, or keep the other part.");
                throw new CommandException($"„{name}” in „{e.Name}” would fall where the wall is cut away: move it first, or keep the other part.");
            }
            moves.Add((o, piece));
        }
        foreach (var (o, to) in moves)
        {
            if (to.Id != e.Id) Rehang(ctx, e.Id, o.Opening.Id, to.Id);
            var along = o.Along - to.S0;
            var now = ctx.Model.Get(o.Opening.Id)!;
            if (Math.Abs(along - o.Along) > 1e-12)
            {
                var oa = now.CloneAttrs();
                oa["along"] = Math.Round(along, 9);
                ctx.Replace(now.With(attrs: oa));
            }
        }
        foreach (var (o, to) in moves)
            Openings.CheckFits(ctx.Model, ctx.Model.Get(to.Id)!, o.Along - to.S0, o.Sill, o.Width, o.Height, o.Opening.Id);

        // The wall's own sweeps and reveals: cut with it, a copy on each further part they run along; none left, they go.
        foreach (var s in m.RelatedOf(e.Id, IfcClass.RelVoidsElement).Where(x => x.Class == WallSweeps.Class && x.Str("typeSweep") == null).ToList())
        {
            var (from, to) = (s.NumOrNull("start"), s.NumOrNull("end"));
            var (a0, a1) = (from ?? 0, to ?? length);
            var first = true;
            foreach (var p in pieces)
            {
                var (lo, hi) = (Math.Max(a0, p.S0), Math.Min(a1, p.S1));
                // Carried on past an end, a sweep that ran to that end runs on with it.
                if (from == null && p.S0 < 0) lo = p.S0;
                if (to == null && p.S1 > length) hi = p.S1;
                if (hi - lo < 1e-3) continue;
                var attrs = s.CloneAttrs();
                if (from == null && lo <= p.S0 + 1e-9) attrs.Remove("start"); else attrs["start"] = Math.Round(lo - p.S0, 9);
                if (to == null && hi >= p.S1 - 1e-9) attrs.Remove("end"); else attrs["end"] = Math.Round(hi - p.S0, 9);
                if (first)
                {
                    ctx.Replace(s.With(attrs: attrs));
                    if (p.Id != e.Id) Rehang(ctx, e.Id, s.Id, p.Id);
                    first = false;
                    continue;
                }
                var copy = new Entity(ctx.NewId(), s.Class, s.Name, attrs, s.PredefinedType);
                ctx.Add(copy);
                ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, p.Id, [copy.Id]));
                foreach (var cr in m.RelationsOf(s.Id, IfcClass.RelContainedInSpatialStructure).Where(x => x.Related.Contains(s.Id)).ToList())
                    ctx.Relate(cr.Class, cr.Relating, copy.Id);
            }
            if (first) ctx.Remove(s.Id);
        }
        // The type's sweeps on a new part, as any wall of the type has them.
        foreach (var p in pieces.Where(p => p.Id != e.Id))
            if (ctx.Model.Get(p.Id) is { } wall) WallSweepTypes.Rebuild(ctx, wall);
    }

    /// <summary>A hosted element moved from one host to another (its IfcRelVoidsElement).</summary>
    static void Rehang(ICommandContext ctx, string from, string id, string to)
    {
        foreach (var r in ctx.Model.RelationsOf(id, IfcClass.RelVoidsElement).Where(r => r.Relating == from && r.Related.Contains(id)).ToList())
        {
            if (r.Related.Count == 1) { ctx.ReplaceRelation(r with { Relating = to }); continue; }
            ctx.ReplaceRelation(r with { Related = [.. r.Related.Where(x => x != id)] });
            ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, to, [id]));
        }
    }
}

/// <summary>
/// A floor's or a ceiling's outline split by a line into two elements, or trimmed by a line (audit 2026-11-15, §6;
/// <see cref="OutlineCuts"/>). The element keeps its GlobalId and the part on the side picked (without a pick, the larger); the
/// other part of a split is a new element like it (its type, storey, layers, properties, groups). Each keeps its edges' arcs and
/// a floor its layer extents on the edges it keeps; the floor openings go to the part they lie in, and one the line crosses
/// refuses (as one in the part a trim cuts away). One undo step.
/// </summary>
public static class OutlineEdits
{
    public static void Register(IModuleRegistry r)
    {
        // {id, a: [x, y], b: [x, y] (the line, run on past them), keep?: [x, y] (the side the element keeps)} → {ids: [element, new]}.
        r.Command("splitOutline", "Split floor", (ctx, a) => Cut(ctx, a, split: true));
        // {id, a, b, keep: [x, y] (the side kept)} → {id}.
        r.Command("trimOutline", "Trim outline", (ctx, a) => Cut(ctx, a, split: false));
    }

    static JsonObject Cut(ICommandContext ctx, JsonObject a, bool split)
    {
        var m = ctx.Model;
        var e = m.Get((string?)a["id"] ?? "") ?? throw new CommandException("The element is not in the project.");
        if (e.Class is not (IfcClass.Slab or Ceilings.Class)) throw new CommandException($"„{e.Name}” is not split by a line here: floors and ceilings are.");
        if (StoredBodies.Has(e) || e.Attrs["reference"] != null) throw new CommandException($"„{e.Name}” is a reference from a file: convert it to native first.");
        Pins.Refuse([e]);
        var corners = Slabs.Corners(e);
        if (corners.Count < 3) throw new CommandException($"„{e.Name}” has no outline.");
        var (p, q) = (Args.TryPoint(a["a"]) ?? throw new CommandException("The line's first point?"), Args.TryPoint(a["b"]) ?? throw new CommandException("The line's second point?"));
        var keep = Args.TryPoint(a["keep"]);
        if (!split && keep == null) throw new CommandException("Which side to keep? A point on it.");
        var (left, right) = OutlineCuts.Split(corners, Slabs.Rises(e), p, q);
        var dir = (q - p).Normalized();
        bool LeftOf(Vec2 x) => Vec2.Cross(dir, x - p) > 0;
        var keepLeft = keep is { } k ? LeftOf(k) : Math.Abs(ArcOutline.SignedArea(left.Corners, left.Rises)) >= Math.Abs(ArcOutline.SignedArea(right.Corners, right.Rises));
        var (mine, other) = keepLeft ? (left, right) : (right, left);
        var what = e.Class == IfcClass.Slab ? "A floor" : "A ceiling";
        var (mc, mr) = ArcOutline.Check(mine.Corners, mine.Rises, what);
        var (oc, otherRises) = ArcOutline.Check(other.Corners, other.Rises, what);

        // The openings: on one side or the other of the line, never across.
        var holes = Slabs.Holes(m, e.Id).ToList();
        var toOther = new List<Entity>();
        foreach (var (o, contour) in holes)
        {
            var sides = contour.Select(x => Vec2.Cross(dir, x - p)).ToList();
            if (sides.Any(s => s > 1e-6) && sides.Any(s => s < -1e-6))
                throw new CommandException($"The line crosses the opening „{o.Name}”: draw it beside the opening.");
            var onLeft = sides.Sum() > 0;
            if (onLeft == keepLeft) continue;
            if (!split) throw new CommandException($"The opening „{o.Name}” lies in the part cut away: delete it first, or keep that side.");
            toOther.Add(o);
        }

        var attrs = e.CloneAttrs();
        Slabs.SetOutline(attrs, mc, mr);
        Remap(e, attrs, mc);
        if (!split)
        {
            ctx.Replace(e.With(attrs: attrs));
            return new JsonObject { ["id"] = e.Id };
        }
        var otherAttrs = e.CloneAttrs();
        Slabs.SetOutline(otherAttrs, oc, otherRises);
        Remap(e, otherAttrs, oc);
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
        return new JsonObject { ["ids"] = new JsonArray(e.Id, second.Id) };
    }

    /// <summary>A floor's layer extents on the edges it keeps (along the old ones); a ceiling has none.</summary>
    static void Remap(Entity e, JsonObject attrs, IReadOnlyList<Vec2> corners)
    {
        if (e.Class != IfcClass.Slab || LayerExtents.Of(e) is not { Count: > 0 } extents) return;
        if (LayerExtents.ToJson(LayerExtents.RemapEdges(extents, Slabs.Corners(e), corners)) is { } kept) attrs[LayerExtents.Key] = kept;
        else attrs.Remove(LayerExtents.Key);
    }
}
