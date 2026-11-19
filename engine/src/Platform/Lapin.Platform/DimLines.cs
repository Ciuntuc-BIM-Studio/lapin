using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// One straight line an element offers temporary dimensions in plan (decision 733): its axis from <see cref="Start"/> to
/// <see cref="End"/>, and across it, as metres left of the axis, its faces (finish included), its core's faces (the faces
/// without finish layers: a wall's core layers, a column's section) and, for a wall that keeps one, its reference line. A
/// wall or a beam is one line; a column two, along its own x and y (<see cref="Part"/>), through its insertion point. What
/// joins it at its ends and along it (path elements only) lets a free end measure to the element it runs toward.
/// </summary>
public sealed record DimLine(
    string Id, string Name, string Kind, Vec2 Start, Vec2 End, double FaceMin, double FaceMax, double CoreMin, double CoreMax,
    double? Reference = null, string? Part = null,
    IReadOnlyList<string>? JoinsStart = null, IReadOnlyList<string>? JoinsEnd = null, IReadOnlyList<string>? JoinsPath = null)
{
    /// <summary>
    /// The middle of its core (decision 738, Revit's "Center of core"), metres left of its axis: off the axis when the core is not
    /// centred on it (a wall with more than one core layer, an imported wall's layers offset, a column's section off its point).
    /// </summary>
    public double CoreCentre => (CoreMin + CoreMax) / 2;
}

/// <summary>
/// A module's elements as temporary-dimension lines (decision 733): walls (architecture), columns and beams (structure);
/// slabs' edges, stair sides or footings may plug in the same way. Only the storey's elements, those a plan of it draws.
/// </summary>
public interface IDimLineProvider
{
    IEnumerable<DimLine> Lines(IModelView model, string storeyId);
}

/// <summary>
/// <c>view.dimLines</c> (decision 733): every provider's lines for a storey (<c>storeyId</c>, or an element's: <c>id</c>),
/// optionally only some kinds (<c>kinds</c>: ["wall", "column", "beam"]). One query for every temporary dimension in plan,
/// the interface measuring from them by the user's dimension reference (core faces, centre of core: <c>coreCentre</c>, decision 738; faces, axis, reference line). Read-only.
/// </summary>
public static class DimLines
{
    public static void Register(IModuleRegistry r) => r.Query("view.dimLines", Of);

    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));
    static JsonArray Pair(double a, double b) => new(Math.Round(a, 9), Math.Round(b, 9));
    static JsonArray Ids(IReadOnlyList<string>? ids) => new([.. (ids ?? []).Select(x => (JsonNode)x)]);

    public static JsonObject Of(IQueryContext q, JsonObject a)
    {
        var m = q.Model;
        string storeyId;
        if ((string?)a["id"] is { Length: > 0 } id)
        {
            if (m.Get(id) is null) throw new CommandException("There is no such element.");
            storeyId = Spatial.StoreyOf(m, id)?.Id ?? throw new CommandException("The element is on no storey.");
        }
        else storeyId = (string?)a["storeyId"] is { Length: > 0 } s ? s : throw new CommandException("Which storey (storeyId) or element (id)?");
        var kinds = (a["kinds"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet();

        var lines = new JsonArray();
        foreach (var p in q.Services.All<IDimLineProvider>())
            foreach (var l in p.Lines(m, storeyId))
            {
                if (kinds != null && !kinds.Contains(l.Kind)) continue;
                var o = new JsonObject
                {
                    ["id"] = l.Id, ["name"] = l.Name, ["kind"] = l.Kind, ["start"] = J(l.Start), ["end"] = J(l.End),
                    ["faces"] = Pair(l.FaceMin, l.FaceMax), ["core"] = Pair(l.CoreMin, l.CoreMax), ["coreCentre"] = Math.Round(l.CoreCentre, 9),
                };
                if (l.Part != null) o["part"] = l.Part;
                if (l.Reference is { } rf) o["reference"] = Math.Round(rf, 9);
                if (l.JoinsStart != null || l.JoinsEnd != null || l.JoinsPath != null)
                    o["joins"] = new JsonObject { ["start"] = Ids(l.JoinsStart), ["end"] = Ids(l.JoinsEnd), ["path"] = Ids(l.JoinsPath) };
                lines.Add((JsonNode)o);
            }
        return new JsonObject { ["storeyId"] = storeyId, ["lines"] = lines };
    }

    /// <summary>
    /// A path element's joins, split by where they meet it (<see cref="Joins.Of"/>): at its start, at its end, along it.
    /// </summary>
    public static (List<string> Start, List<string> End, List<string> Path) JoinsOf(IModelView m, string id)
    {
        var (atStart, atEnd, along) = (new List<string>(), new List<string>(), new List<string>());
        foreach (var r in Joins.Of(m, id))
        {
            var mine = (string?)r.Attrs?[r.Relating == id ? "relatingConnection" : "relatedConnection"];
            var other = Joins.Other(r, id);
            (mine == "ATSTART" ? atStart : mine == "ATEND" ? atEnd : along).Add(other);
        }
        return (atStart, atEnd, along);
    }
}
