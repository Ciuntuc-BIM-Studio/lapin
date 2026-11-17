using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A corridor (a path or a road, a retaining wall, a ditch, a module's kind) in the platform's split, trim, extend and offset
/// (<see cref="PolylineEdits"/>, audit 2026-11-15 §15). Its axis is cut along its path's points; each vertex keeps its curve (a
/// cut's new end has none). The stations go on as they were: a part's start station is where it began along the old axis, so
/// the designed profile (its PVIs, kept whole on each part), the cross-sections and joints asked at stations stay where they
/// were; those off a part are left out. A cut must fall on a straight stretch of the axis, not in a curve. An axis made of
/// IFC 4.3 segments (an imported alignment) is refused: its segments would have to be cut, which Lapin does not do yet. The
/// terrain it cut, it cuts on each part.
/// </summary>
public sealed class CorridorPieces : IPolylinePieces
{
    public bool Handles(Entity e) => Corridors.Is(e) && e.Attrs["path"] is JsonArray;

    public string? Refuse(IModelView m, Entity e) =>
        CorridorSpec.Of(e).Horizontal != null ? $"„{e.Name}” runs on an IFC 4.3 alignment of its own segments: its axis is not cut here." : null;

    public (List<Vec2> Points, double[] Rises) Line(Entity e)
    {
        var p = Terrains.Contour(e, "path").ToList();
        return (p, new double[Math.Max(0, p.Count - 1)]);
    }

    public void Piece(IModelView m, Entity e, JsonObject attrs, PolySlice slice, bool newStart, bool newEnd)
    {
        attrs["path"] = Terrains.ContourJson(slice.Points);
        var spec = CorridorSpec.Of(e);
        var chain = Corridors.ChainOf(e);
        var length = new PolylineEdits.Line(Terrains.Contour(e, "path"), null).Length;
        var curved = spec.Curves.Any(c => !c.IsCorner);
        // A station along the old axis: carried on past an end, straight on from it.
        double StationAt(Vec2 p, double s)
        {
            if (s <= 0) return chain.Start + s;
            if (s >= length) return chain.End + (s - length);
            var (st, off) = chain.Project(p);
            if (curved && Math.Abs(off) > 1e-3) throw new CommandException($"„{e.Name}” would be cut in a curve of its axis: cut it on a straight stretch.");
            return st;
        }
        var start = StationAt(slice.Points[0], slice.S0);
        var end = StationAt(slice.Points[^1], slice.S1);
        if (attrs["corridor"] is not JsonObject && Math.Abs(start - chain.Start) < 1e-9) return;
        var curves = curved ? slice.From.Select(i => i is { } k && k < spec.Curves.Count ? spec.Curves[k] : PiCurve.None).ToList() : [];
        // A curve stays at an inner vertex only.
        if (curves.Count > 0) { curves[0] = PiCurve.None; curves[^1] = PiCurve.None; }
        var kept = spec with
        {
            StationStart = Math.Round(start, 9),
            Sections = [.. spec.Sections.Where(s => s >= start - 1e-6 && s <= end + 1e-6)],
            JointsAt = [.. spec.JointsAt.Where(s => s > start + 1e-6 && s < end - 1e-6)],
            Curves = curves,
        };
        if (curves.Any(c => !c.IsCorner) && new Chain(slice.Points, curves, start).Problem is { } why)
            throw new CommandException($"„{e.Name}” cut there leaves a curve no room: {why}");
        attrs["corridor"] = kept.ToJson();
    }

    public void Rehost(ICommandContext ctx, Entity e, IReadOnlyList<PathPiece> pieces, JsonObject args)
    {
        // The terrain it cut: cut by each part.
        if (Terrains.TerrainOf(ctx.Model, e.Id) is { } terrain)
            foreach (var p in pieces.Where(p => p.Id != e.Id))
                ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, terrain.Id, [p.Id]));
        foreach (var p in pieces) CorridorKinds.Of(e)?.AfterChange(ctx, p.Id);
    }
}
