using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis;

/// <summary>
/// Load cases and loads from a table (docs/plan-user-extensibility.md §7.2): a spreadsheet of another program's loads, or the
/// office's own, read through the table import. Loads: a point (x, y), a line (x1, y1 → x2, y2) or an area (its outline's points
/// "x y; x y; …"), its value in any unit of its kind (kN, daN, kgf, kip…; per metre; per square metre, kPa, psf…) given in the
/// heading or on the row, its case by code or name (a case the project lacks is made, variable unless the row says otherwise),
/// its storey by name (else the one chosen for the table), its direction and height. Cases: code or name, action, source, category.
/// </summary>
public static class AnalysisTables
{
    public const string LoadsId = "analysis.loads", CasesId = "analysis.cases";

    static readonly Dictionary<string, string[]> KindWords = new()
    {
        [UserLoads.Area] = ["area", "areal", "planar", "surface", "surfacique", "suprafata", "arie", "flache", "flachenlast", "plane", "slab", "a"],
        [UserLoads.Line] = ["line", "linear", "curve", "liniara", "linie", "linienlast", "lineique", "linea", "l"],
        [UserLoads.Point] = ["point", "punct", "punctuala", "concentrata", "punktlast", "ponctuelle", "punto", "nodal", "p", "f"],
    };

    static readonly Dictionary<string, string[]> ActionWords = new()
    {
        [LoadCases.Permanent] = ["permanent", "permanenta", "permanente", "standig", "dead", "g", "permanentg"],
        [LoadCases.Variable] = ["variable", "variabila", "veranderlich", "live", "q", "variableq", "imposed"],
        [LoadCases.Extraordinary] = ["extraordinary", "exceptionala", "accidental", "accidentala", "aussergewohnlich", "a", "seismic", "seism"],
    };

    static readonly Dictionary<string, string[]> SourceWords = new()
    {
        ["DEAD_LOAD_G"] = ["dead", "deadload", "selfweight", "greutateproprie", "eigengewicht"],
        ["COMPLETION_G1"] = ["completion", "finishes", "finisaje", "ausbau"],
        ["LIVE_LOAD_Q"] = ["live", "liveload", "imposed", "utila", "nutzlast", "exploatare"],
        ["SNOW_S"] = ["snow", "zapada", "schnee", "neige"],
        ["WIND_W"] = ["wind", "vant", "vent"],
        ["EARTHQUAKE_E"] = ["earthquake", "seismic", "seism", "erdbeben", "seisme"],
        ["USERDEFINED"] = ["user", "userdefined", "other", "altele"],
    };

    static string? Word(Dictionary<string, string[]> words, string? text)
    {
        if (text == null) return null;
        var n = TableImport.Norm(text);
        foreach (var (key, list) in words) if (TableImport.Norm(key) == n || list.Contains(n)) return key;
        return null;
    }

    public static readonly TableTarget Loads = new(LoadsId, "Structural loads", AnalysisModule.ModuleId,
    [
        new("kind", "Kind", Aliases: ["Type", "Tip", "Fel", "Typ", "Art", "Load type", "Tipul"], Help: "area, line or point (any language); none: read from the coordinates given"),
        new("case", "Load case", Aliases: ["Case", "Caz", "Ipoteza", "Ipoteză", "Lastfall", "Cas", "LC", "Load case code"]),
        new("storey", "Storey", Aliases: ["Level", "Nivel", "Etaj", "Geschoss", "Niveau", "Floor", "Storey name"]),
        new("x", "x", "length"), new("y", "y", "length"),
        new("x1", "x1", "length", Aliases: ["xa", "xstart"]), new("y1", "y1", "length", Aliases: ["ya", "ystart"]),
        new("x2", "x2", "length", Aliases: ["xb", "xend"]), new("y2", "y2", "length", Aliases: ["yb", "yend"]),
        new("points", "Outline points", "length", Aliases: ["Points", "Puncte", "Outline", "Contur", "Polygon", "Punkte", "Coordinates", "Coordonate"], Help: "x y; x y; … (an area load's outline)"),
        new("value", "Value", "load", Required: true, Aliases: ["Load", "Value", "Valoare", "Wert", "Valeur", "q", "p", "F", "Magnitude", "Intensity", "Intensitate", "Incarcare", "Încărcare"]),
        new("unit", "Unit", Aliases: ["Unit", "UM", "U.M.", "Unitate", "Einheit", "Unité", "Units"], Help: "kN, kN/m, kN/m², daN/m², kPa, psf… on the row"),
        new("direction", "Direction", Aliases: ["Dir", "Directie", "Direcție", "Richtung", "Sens"], Help: string.Join(", ", UserLoads.Directions)),
        new("z", "Height above the storey", "length", Aliases: ["z", "Height", "Inaltime", "Înălțime", "Höhe", "Hauteur", "dz"]),
        new("caseType", "Case action", Aliases: ["Action", "Actiune", "Acțiune", "Einwirkung"], Help: "permanent, variable or extraordinary, for a case the project lacks"),
    ],
    LoadSteps, Description: "Point, line and area loads by their coordinates, each in its load case and storey; a case the project lacks is made.",
    Example: [["point", "Q-A", "Ground floor", "2.5", "3", "", "", "", "", "", "12", "kN", "down", "", ""],
              ["line", "G", "Ground floor", "", "", "0", "0", "6", "0", "", "4.5", "kN/m", "down", "", ""],
              ["area", "S", "Ground floor", "", "", "", "", "", "", "0 0; 6 0; 6 4; 0 4", "1,2", "kN/m²", "down", "", "variable"]]);

    public static readonly TableTarget Cases = new(CasesId, "Load cases", AnalysisModule.ModuleId,
    [
        new("name", "Load case", Required: true, Aliases: ["Case", "Caz", "Name", "Nume", "Code", "Cod", "Lastfall", "Cas"]),
        new("title", "Description", Aliases: ["Title", "Descriere", "Beschreibung", "Description", "Denumire"]),
        new("action", "Action", Aliases: ["Type", "Tip", "Actiune", "Acțiune", "Einwirkung", "Action type"], Help: "permanent, variable or extraordinary"),
        new("source", "Source", Aliases: ["Sursa", "Sursă", "Quelle", "Origine", "Action source"], Help: "dead, completion, live, snow, wind, earthquake, user"),
        new("category", "Category of use", Aliases: ["Category", "Categorie", "Kategorie", "Catégorie"]),
    ],
    CaseSteps, Description: "Load cases made or changed by their code: action, source and category of use.",
    Example: [["G", "Permanent", "permanent", "dead", ""], ["Q-A", "Imposed, housing", "variable", "live", "A"], ["S", "Snow", "variable", "snow", ""]]);

    static LoadCase? CaseNamed(IModelView m, string name) =>
        LoadCases.All(m).FirstOrDefault(c => string.Equals(c.Code, name, StringComparison.OrdinalIgnoreCase)) ?? LoadCases.All(m).FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    static IReadOnlyList<TableStep> CaseSteps(IQueryContext q, TableRow row)
    {
        var name = row.Need("name", "The load case");
        var action = row.Text("action") is { } at ? Word(ActionWords, at) ?? throw new CommandException($"„{at}” is not permanent, variable or extraordinary.") : null;
        var source = row.Text("source") is { } st ? Word(SourceWords, st) ?? throw new CommandException($"„{st}” is not a source ({string.Join(", ", LoadCases.ActionSources)}).") : null;
        var args = new JsonObject();
        if (action != null) args["actionType"] = action;
        if (source != null) args["actionSource"] = source;
        if (row.Text("category") is { } cat) args["category"] = cat;
        if (CaseNamed(q.Model, name) is { } existing)
        {
            args["id"] = existing.Id;
            if (row.Text("title") is { } t) args["name"] = t;
            if (args.Count == 1) throw new CommandException($"„{name}” is the project's already, and the row changes nothing.");
            return [new("analysis.updateCase", args, $"Load case {existing.Code}: changed")];
        }
        args["code"] = name;
        args["name"] = row.Text("title") ?? name;
        if (!args.ContainsKey("actionType")) args["actionType"] = LoadCases.Variable;
        return [new("analysis.addCase", args, $"Load case {name}: new, {Describe((string)args["actionType"]!)}")];
    }

    static string Describe(string action) => action switch { LoadCases.Permanent => "permanent", LoadCases.Extraordinary => "extraordinary", _ => "variable" };

    static IReadOnlyList<TableStep> LoadSteps(IQueryContext q, TableRow row)
    {
        var m = q.Model;
        // Its kind: the row's word, else what its coordinates are.
        var kind = row.Text("kind") is { } kt ? Word(KindWords, kt) ?? throw new CommandException($"„{kt}” is not area, line or point.")
            : row.Has("points") ? UserLoads.Area : row.Has("x1") || row.Has("x2") ? UserLoads.Line : row.Has("x") ? UserLoads.Point
            : throw new CommandException("Where is the load? Give x and y, x1 y1 x2 y2, or the outline's points.");
        var family = kind switch { UserLoads.Area => "areaLoad", UserLoads.Line => "lineLoad", _ => "force" };
        // Its value in the unit the row gives, else the heading's, else the base unit of its kind.
        var raw = row.Need("value", "The value");
        // The number, and a unit written in the cell itself („4.5 kN/m”) over the row's and the heading's.
        var (number, inCell) = TableUnits.Split(raw) ?? throw new CommandException($"„{raw}” is not a number.");
        var unit = inCell ?? row.Text("unit") ?? row.Unit("value");
        var factor = 1.0;
        if (unit != null)
        {
            var found = TableUnits.Find(unit) ?? throw new CommandException($"„{unit}” is not a unit of loads.");
            if (found.Kind != family) throw new CommandException($"„{unit}” is not a unit of {(family == "force" ? "force" : family == "lineLoad" ? "load per metre" : "load per square metre")} ({kind} load).");
            factor = found.Unit.Factor;
        }
        var value = number * factor;
        // A negative value is a load the other way.
        var direction = row.Text("direction") is { } dt ? UserLoads.Directions.FirstOrDefault(d => TableImport.Norm(d) == TableImport.Norm(dt) || d == dt.Trim().ToLowerInvariant())
            ?? (TableImport.Norm(dt) is "jos" or "unten" or "bas" or "gravity" or "z" ? "down" : TableImport.Norm(dt) is "sus" or "oben" or "haut" ? "up" : throw new CommandException($"„{dt}” is not a direction ({string.Join(", ", UserLoads.Directions)}).")) : "down";
        if (value < 0)
        {
            value = -value;
            direction = direction switch { "down" => "up", "up" => "down", "+x" => "-x", "-x" => "+x", "+y" => "-y", _ => "+y" };
        }
        UserLoads.CheckValue(value);
        // Its storey: by name, else the table's own.
        var storeyName = row.Need("storey", "The storey");
        var storey = Spatial.Storeys(m).FirstOrDefault(s => s.Id == storeyName || string.Equals(s.Name, storeyName, StringComparison.OrdinalIgnoreCase))
            ?? throw new CommandException($"The project has no storey „{storeyName}”.");
        var args = new JsonObject { ["storeyId"] = storey.Id, ["value"] = Math.Round(value, 9), ["direction"] = direction };
        if (row.Num("z", "length") is { } z) args["z"] = z;
        var steps = new List<TableStep>();
        // Its case: the project's by code or name; one it lacks made once for the whole table.
        if (row.Text("case") is { } caseName)
        {
            if (CaseNamed(m, caseName) is { } c) args["caseId"] = c.Id;
            else
            {
                args["caseName"] = caseName;
                var made = row.Shared.TryGetValue("cases", out var o) ? (HashSet<string>)o : (HashSet<string>)(row.Shared["cases"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                if (made.Add(caseName))
                {
                    var action = row.Text("caseType") is { } ct ? Word(ActionWords, ct) ?? throw new CommandException($"„{ct}” is not permanent, variable or extraordinary.") : LoadCases.Variable;
                    steps.Add(new("analysis.addCase", new JsonObject { ["code"] = caseName, ["name"] = caseName, ["actionType"] = action }, $"Load case {caseName}: new, {Describe(action)}"));
                }
            }
        }
        double L(string key, string label) => row.Num(key, "length") ?? throw new CommandException($"{label} is empty.");
        var unitText = UserLoads.Unit(kind);
        var at = value.ToString("0.###", CultureInfo.InvariantCulture);
        switch (kind)
        {
            case UserLoads.Point:
                args["at"] = new JsonArray(L("x", "x"), L("y", "y"));
                steps.Add(new("analysis.addPointLoad", args, $"Point load {at} {unitText} {direction} at ({F(L("x", "x"))}, {F(L("y", "y"))}) on {storey.Name}"));
                break;
            case UserLoads.Line:
                args["a"] = new JsonArray(L("x1", "x1"), L("y1", "y1"));
                args["b"] = new JsonArray(L("x2", "x2"), L("y2", "y2"));
                steps.Add(new("analysis.addLineLoad", args, $"Line load {at} {unitText} {direction} from ({F(L("x1", "x1"))}, {F(L("y1", "y1"))}) to ({F(L("x2", "x2"))}, {F(L("y2", "y2"))}) on {storey.Name}"));
                break;
            default:
            {
                var pts = Points(row.Need("points", "The outline's points"), TableUnits.Factor("length", row.Unit("points")));
                if (pts.Count < 3) throw new CommandException("An area load's outline needs three points at least.");
                args["points"] = new JsonArray(pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray());
                steps.Add(new("analysis.addAreaLoad", args, $"Area load {at} {unitText} {direction}, {pts.Count} corners, {F(Math.Abs(Polygon2.SignedArea(pts)))} m², on {storey.Name}"));
                break;
            }
        }
        return steps;
    }

    static string F(double v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>An outline's points as written in a cell: pairs parted by ; or |, each "x y" (or "x,y" with no space), brackets left out.</summary>
    public static List<Vec2> Points(string text, double factor)
    {
        var o = new List<Vec2>();
        foreach (var raw in text.Replace("(", "").Replace(")", "").Split([';', '|'], StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = raw.Trim();
            var parts = pair.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) parts = pair.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || TableUnits.Parse(parts[0]) is not { } x || TableUnits.Parse(parts[1]) is not { } y)
                throw new CommandException($"„{pair}” is not a point (x y).");
            o.Add(new Vec2(x * factor, y * factor));
        }
        return o;
    }
}
