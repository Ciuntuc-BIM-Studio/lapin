using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Property values from a table (docs/plan-user-extensibility.md §7.2): a column finds the element — its GlobalId, else its mark,
/// else every element of that name —, and every other column is a parameter of it: <c>Pset_WallCommon.FireRating</c> goes into that
/// set (a standard set only by its own property names), <c>Set.Name</c> into a set of the user's, a plain heading into the user's
/// own parameters; a <c>Name</c> column renames. A value is held as the kind the element's parameter has, else a number when it
/// reads as one, else text. One <c>element.setParam</c> a cell; the whole table one undo step.
/// </summary>
public static class PropertyTable
{
    public const string Id = "core.properties";

    public static readonly TableTarget Target = new(Id, "Property values", "core",
    [
        new("element", "Element", "text", Required: true, Aliases: ["GlobalId", "Global Id", "IfcGUID", "Guid", "Id", "Mark", "Tag", "Marca", "Marcă", "Kennzeichen", "Repère", "Name", "Nume", "Element", "Element ID"],
            Help: "The element's GlobalId, its mark, or its name (every element of that name)."),
    ],
    Steps, Open: true,
    Description: "Fills or changes parameters on elements found by GlobalId, mark or name; each other column is a parameter (Set.Name for a property set).",
    Example: [["W-01", "REI 120", "0.25"], ["W-02", "REI 60", "0.25"]]);

    /// <summary>The elements a cell names: one by GlobalId, else those of that mark, else those of that name (spatial structure and views aside).</summary>
    public static List<Entity> Find(IModelView m, string key)
    {
        if (m.Get(key) is { } byId) return [byId];
        static bool Element(Entity e) => e.Class is not (IfcClass.Project or IfcClass.Site or IfcClass.Building or IfcClass.BuildingStorey) && e.Class != Views.Class && e.Class != Sheets.Class;
        var byMark = m.Entities.Where(e => Element(e) && string.Equals(e.Str("mark"), key, StringComparison.OrdinalIgnoreCase)).ToList();
        if (byMark.Count > 0) return byMark;
        return [.. m.Entities.Where(e => Element(e) && string.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase))];
    }

    static IReadOnlyList<TableStep> Steps(IQueryContext q, TableRow row)
    {
        var key = row.Need("element", "The element");
        var found = Find(q.Model, key);
        if (found.Count == 0) throw new CommandException($"No element has the GlobalId, mark or name „{key}”.");
        var ids = new JsonArray(found.Select(e => (JsonNode)e.Id).ToArray());
        var what = found.Count == 1 ? $"„{found[0].Name}”" : $"{found.Count} elements „{key}”";
        var steps = new List<TableStep>();
        foreach (var (heading, value) in row.OpenCells)
        {
            if (TableImport.Norm(heading) is "name" or "nume" or "bezeichnung" or "nom")
            {
                foreach (var e in found) steps.Add(new("element.rename", new JsonObject { ["id"] = e.Id, ["name"] = value }, $"{what}: name {value}"));
                continue;
            }
            // Set.Name: into that property set; the last dot parts them (a name may hold none).
            var dot = heading.LastIndexOf('.');
            var (pset, name) = dot > 0 && dot < heading.Length - 1 ? (heading[..dot].Trim(), heading[(dot + 1)..].Trim()) : ((string?)null, heading.Trim());
            if (name.Length == 0) continue;
            var (checkedSet, standardKind) = StandardPsets.Check(found[0].Class, pset, name);
            var existing = (found[0].Attrs["params"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(p => Params.Same(p, checkedSet, name));
            var kind = standardKind ?? (string?)existing?["kind"] ?? (TableUnits.Parse(value) != null ? "number" : "text");
            JsonNode? v = kind switch
            {
                "number" or "length" or "area" or "angle" => TableUnits.Parse(value) is { } n ? JsonValue.Create(n) : throw new CommandException($"{heading}: „{value}” is not a number."),
                "bool" => TableImport.Norm(value) is "yes" or "true" or "da" or "ja" or "oui" or "1" or "x" ? JsonValue.Create(true)
                        : TableImport.Norm(value) is "no" or "false" or "nu" or "nein" or "non" or "0" ? JsonValue.Create(false)
                        : throw new CommandException($"{heading}: „{value}” is not yes or no."),
                _ => JsonValue.Create(value),
            };
            var args = new JsonObject { ["ids"] = ids.DeepClone(), ["name"] = name, ["kind"] = kind, ["value"] = v };
            if (checkedSet != null) args["pset"] = checkedSet;
            steps.Add(new("element.setParam", args, $"{what}: {(checkedSet != null ? checkedSet + "." : "")}{name} = {value}"));
        }
        if (steps.Count == 0) throw new CommandException("The row has no values to set.");
        return steps;
    }
}
