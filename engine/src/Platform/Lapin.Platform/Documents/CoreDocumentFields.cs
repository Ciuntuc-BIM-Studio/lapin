using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform.Cde;

namespace Lapin.Platform.Documents;

/// <summary>
/// The platform's fields for documents (docs/plan-documents.md, Câmpurile): the project's information (<c>project.*</c>), its own
/// parameters (<c>param.&lt;Name&gt;</c>), the site (<c>site.*</c>), today's date, the CDE's codes and last issue (<c>cde.*</c>),
/// the number of storeys; the collections <c>storeys</c> ({{storey.*}}: id, name, elevation, height), <c>sheets</c> ({{sheet.*}}:
/// id, number, title, scale, revision, status) and <c>documents</c> ({{doc.*}}: id, name, revision, status, template).
/// </summary>
public sealed class CoreDocumentFields : IDocumentFields
{
    public IEnumerable<DocField> Fields(IQueryContext q, DocFormat f)
    {
        var m = q.Model;
        yield return new("project.name", "Project name", Sheets.ProjectName(q));
        foreach (var (key, label) in ProjectInfo.Fields) yield return new("project." + key, label, ProjectInfo.Get(m, key));
        // The user's own parameters of the project (decision 340), by their keys.
        foreach (var c in ProjectInfo.Customs(m)) yield return new("project." + c.Key, c.Label, c.Printed);
        foreach (var p in Spatial.Project(m)?.Attrs["params"] as JsonArray ?? [])
            if (p is JsonObject o && (string?)o["name"] is { Length: > 0 } name && Params.SetOf(o) == null)
                yield return new("param." + name, name, Param(o, f));
        yield return new("site.address", "Site address", ProjectInfo.Get(m, "address"));
        yield return new("site.cadastral", "Cadastral number", ProjectInfo.Get(m, "cadastral"));
        yield return new("site.landBook", "Land book", ProjectInfo.Get(m, "landBook"));
        var crs = Location.Crs(m);
        yield return new("site.crs", "Coordinate system", crs == null ? null : GeoRef.Get(crs)?.Name ?? crs);
        yield return new("date.today", "Today's date", DocNumbers.Date(DateTime.Now, f));
        var naming = CdeHooks.Naming?.Invoke(CdeNaming.Of(m)) ?? CdeNaming.Of(m);
        yield return new("cde.project", "CDE project code", naming.Project);
        yield return new("cde.originator", "CDE originator code", naming.Originator);
        var last = CdeIssues.All(m).LastOrDefault();
        yield return new("cde.lastIssue", "Last issue", last == null ? null : $"{last.Name} ({DateOf(last.At, f)})");
        yield return new("cde.revision", "Last issue's revision", last?.Revision);
        yield return new("cde.status", "Last issue's status", last?.Status);
        yield return new("counts.storeys", "Storeys", Spatial.Storeys(m).Count().ToString(CultureInfo.InvariantCulture));
    }

    static string DateOf(string at, DocFormat f) =>
        DateTime.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var d) ? DocNumbers.Date(d.ToLocalTime(), f) : at;

    /// <summary>A parameter's value as its kind writes it.</summary>
    static string? Param(JsonObject p, DocFormat f)
    {
        var v = p["value"];
        if (v is null) return null;
        var n = Json.Number(v);
        return (string?)p["kind"] switch
        {
            "number" when n is { } x => DocNumbers.Number(x, f, x == Math.Round(x) ? 0 : 2),
            "length" when n is { } x => DocNumbers.Length(x, f),
            "area" when n is { } x => DocNumbers.Area(x, f),
            "angle" when n is { } x => DocNumbers.Number(x, f, 1) + "°",
            "bool" => v is JsonValue b && b.TryGetValue<bool>(out var yes) ? DocNumbers.YesNo(yes, f) : null,
            _ => v.ToString(),
        };
    }

    public IEnumerable<DocCollection> Collections =>
    [
        new("storeys", "Storeys", "storey"),
        new("sheets", "Sheets", "sheet"),
        new("documents", "Documents", "doc"),
    ];

    public IEnumerable<IReadOnlyDictionary<string, string>> Items(IQueryContext q, string collection, DocFormat f)
    {
        var m = q.Model;
        switch (collection)
        {
            case "storeys":
                return Spatial.Storeys(m).Select(s => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
                {
                    ["id"] = s.Id, ["name"] = s.Name, ["elevation"] = DocNumbers.Level(s.Num("elevation"), f), ["height"] = DocNumbers.Length(s.Num("height", 3), f),
                }).ToList();
            case "sheets":
                return m.OfClass(Sheets.Class).Select(s =>
                {
                    var scales = Sheets.Viewports(s).Select(vp => Views.Find(m, vp.ViewId)?.NumOrNull("scale")).OfType<double>().Where(x => x > 0).Distinct().Order()
                        .Select(x => "1:" + x.ToString("0", CultureInfo.InvariantCulture)).ToList();
                    return (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
                    {
                        ["id"] = s.Id, ["number"] = s.Str("number") ?? "", ["title"] = s.Name, ["scale"] = string.Join(", ", scales),
                        ["revision"] = CdeCheckContext.Field(s, "revision") ?? "", ["status"] = CdeCheckContext.Field(s, "status") ?? "",
                        ["discipline"] = s.Str("discipline") ?? "",
                    };
                }).ToList();
            case "documents":
                return Documents.All(m).Select(d => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
                {
                    ["id"] = d.Id, ["name"] = d.Name, ["revision"] = d.Str("revision") ?? "", ["status"] = d.Str("status") ?? "", ["template"] = d.Str("templateTitle") ?? d.Str("template") ?? "",
                }).ToList();
            default:
                return [];
        }
    }
}
