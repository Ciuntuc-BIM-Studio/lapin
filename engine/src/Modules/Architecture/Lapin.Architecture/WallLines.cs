using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// What a wall's temporary dimensions measure to (decision 693): the storey's straight walls as lines, each with its axis
/// (start, end), how far its faces and its core's faces lie left of the axis (the core's only when it is not the whole wall),
/// and the walls joined at its start, at its end and along it. The interface finds from these the parallel walls either side
/// of a wall selected or being drawn, and the walls each of its ends runs toward. Curved walls are left out. Asked for a storey
/// (<c>storeyId</c>) or a wall's (<c>id</c>). Nothing changes in the model.
/// </summary>
public static class WallLines
{
    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));

    public static JsonObject Of(IQueryContext q, JsonObject a)
    {
        var m = q.Model;
        string storeyId;
        if ((string?)a["id"] is { } id)
        {
            var wall = Spatial.Require(m, id, IfcClass.Wall);
            storeyId = Spatial.StoreyOf(m, wall.Id)?.Id ?? throw new CommandException("The wall is on no storey.");
        }
        else storeyId = (string?)a["storeyId"] is { Length: > 0 } s ? s : throw new CommandException("Which storey (storeyId) or wall (id)?");

        var walls = new JsonArray();
        foreach (var w in m.OfClass(IfcClass.Wall))
        {
            if (Spatial.StoreyOf(m, w.Id)?.Id != storeyId) continue;
            if (PathCurve.Of(w).IsArc || Walls.Frame(w, m) is not { } g) continue;
            var core = Walls.Section(m, w).Where(b => b.Core).ToList();
            var entry = new JsonObject
            {
                ["id"] = w.Id, ["name"] = w.Name, ["start"] = J(g.Start), ["end"] = J(g.End),
                ["faces"] = new JsonArray(Math.Round(g.YMin, 9), Math.Round(g.YMax, 9)),
            };
            if (core.Count > 0)
            {
                var (c0, c1) = (core.Min(b => b.From), core.Max(b => b.To));
                if (c0 > g.YMin + 1e-6 || c1 < g.YMax - 1e-6) entry["core"] = new JsonArray(Math.Round(c0, 9), Math.Round(c1, 9));
            }
            var (atStart, atEnd, along) = (new JsonArray(), new JsonArray(), new JsonArray());
            foreach (var r in Joins.Of(m, w.Id))
            {
                var mine = (string?)r.Attrs?[r.Relating == w.Id ? "relatingConnection" : "relatedConnection"];
                var other = Joins.Other(r, w.Id);
                (mine == "ATSTART" ? atStart : mine == "ATEND" ? atEnd : along).Add((JsonNode)other);
            }
            entry["joins"] = new JsonObject { ["start"] = atStart, ["end"] = atEnd, ["path"] = along };
            walls.Add((JsonNode)entry);
        }
        return new JsonObject { ["storeyId"] = storeyId, ["walls"] = walls };
    }
}
