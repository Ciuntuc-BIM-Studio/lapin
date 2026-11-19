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
        foreach (var l in LinesOf(m, storeyId))
        {
            var entry = new JsonObject
            {
                ["id"] = l.Id, ["name"] = l.Name, ["start"] = J(l.Start), ["end"] = J(l.End),
                ["faces"] = new JsonArray(Math.Round(l.FaceMin, 9), Math.Round(l.FaceMax, 9)),
            };
            if (l.CoreMin > l.FaceMin + 1e-6 || l.CoreMax < l.FaceMax - 1e-6) entry["core"] = new JsonArray(Math.Round(l.CoreMin, 9), Math.Round(l.CoreMax, 9));
            JsonArray Ids(IReadOnlyList<string>? ids) => new([.. (ids ?? []).Select(x => (JsonNode)x)]);
            entry["joins"] = new JsonObject { ["start"] = Ids(l.JoinsStart), ["end"] = Ids(l.JoinsEnd), ["path"] = Ids(l.JoinsPath) };
            walls.Add((JsonNode)entry);
        }
        return new JsonObject { ["storeyId"] = storeyId, ["walls"] = walls };
    }

    /// <summary>
    /// The storey's straight walls as temporary-dimension lines (decision 733, <see cref="DimLines"/>): axis, faces, core faces
    /// (the whole wall when no layer is core), the reference line it keeps, its joins. Curved walls are left out.
    /// </summary>
    public static IEnumerable<DimLine> LinesOf(IModelView m, string storeyId)
    {
        foreach (var w in m.OfClass(IfcClass.Wall))
        {
            if (Spatial.StoreyOf(m, w.Id)?.Id != storeyId) continue;
            if (PathCurve.Of(w).IsArc || Walls.Frame(w, m) is not { } g) continue;
            var bands = Walls.Section(m, w);
            var core = bands.Where(b => b.Core).ToList();
            var (c0, c1) = core.Count > 0 ? (core.Min(b => b.From), core.Max(b => b.To)) : (g.YMin, g.YMax);
            var reference = WallReference.Before(m, w);
            var (atStart, atEnd, along) = DimLines.JoinsOf(m, w.Id);
            yield return new DimLine(w.Id, w.Name, "wall", g.Start, g.End, g.YMin, g.YMax, c0, c1, reference, null, atStart, atEnd, along);
        }
    }
}

/// <summary>The walls' lines for <c>view.dimLines</c> (decision 733).</summary>
sealed class WallDimLines : IDimLineProvider
{
    public IEnumerable<DimLine> Lines(IModelView model, string storeyId) => WallLines.LinesOf(model, storeyId);
}
