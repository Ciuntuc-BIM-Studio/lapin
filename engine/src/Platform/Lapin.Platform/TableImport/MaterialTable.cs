using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing.Export;
using Lapin.Model;
using Lapin.Platform.Analysis;

namespace Lapin.Platform;

/// <summary>
/// Material data from a table (forum topic #251), a target of the table import: a row finds its material by its id, else by its
/// name (case and spaces aside), and every other column is one of its parameters, as Manage → Materials → Data holds them.
/// <c>Pset_MaterialThermal.ThermalConductivity</c> goes into that standard set (its own names only, its value in IFC's SI unit), any
/// other <c>Set.Name</c> into a set of the user's, a bare heading into the material's own parameters. The Data tab's ready-made ones
/// are found by their names and symbols (λ, ρ, μ, E, fck, fy, Reaction to fire) and go where the tab puts them; a unit in the heading
/// (<c>E (GPa)</c>) or the cell (<c>31 GPa</c>) is converted to the one the parameter is held in. A row with no such material is
/// skipped, or the material made (<c>ifMissing</c>). One <c>material.setData</c> a material; the whole table one undo step.
/// <see cref="Export"/> writes the same table, so a file exported, filled in and imported changes only what was changed.
/// </summary>
public static class MaterialTable
{
    public const string Id = "core.materials";
    public const string Skip = "Skip", Create = "Create";

    public static readonly TableTarget Target = new(Id, "Material data", "core",
    [
        new("material", "Material", "text", Aliases: ["Material name", "Name", "Nume", "Denumire", "Material", "Materialul", "Werkstoff", "Matériau", "Materiale", "Materiał", "Materiais", "Nombre"],
            Help: "The material's name, as the project has it (case and spaces aside)."),
        new("id", "Material id", "text", Aliases: ["Id", "GlobalId", "Global Id", "Guid", "IfcGUID", "Material id"],
            Help: "The material's id, as Export data… writes it: found even when its name changed (the Material column then renames it)."),
        new("description", "Description", "text", Aliases: ["Descriere", "Beschreibung", "Specification", "Specificație"]),
        new("category", "Category", "text", Aliases: ["Categorie", "Kategorie", "Catégorie", "Categoría", "Categoria"]),
        new("ifMissing", "Rows with no such material", "choice", Choices: [Skip, Create], Option: true,
            Help: "Skip: only the project's materials change. Create: a row whose material the project lacks makes it."),
    ],
    Steps, Open: true,
    Description: "Fills or changes the data of the project's materials, found by name or id; each other column is a parameter (Set.Name for a property set, a unit in brackets).",
    Example: [["EPS 100", "", "Expanded polystyrene", "Insulation"], ["Concrete C25/30", "", "", "Structure"]])
    {
        OpenColumn = (q, heading) => Column(q.Model, heading) is var c && c.Error != null ? (heading, c.Error) : (c.Describe, null),
    };

    // ------------------------------------------------------------------ columns

    /// <summary>A quantity a unit can be converted in: its name in words and each unit's factor to its base (Pa, kg/m³, W/(m·K), m).</summary>
    internal sealed record Quantity(string Name, Func<string, double?> Per);

    static string NormUnit(string u) => u.Trim().ToLowerInvariant().Replace(" ", "").Replace("²", "2").Replace("³", "3").Replace("·", "").Replace("*", "").Replace("^", "").Replace("°", "");

    static readonly Quantity Stress = new("stress (Pa, kPa, MPa, N/mm², GPa, psi, ksi)", StructuralMaterials.PascalPer);
    static readonly Quantity Density = new("density (kg/m³, g/cm³, t/m³)", u => NormUnit(u) switch
    {
        "kg/m3" => 1, "g/cm3" or "kg/dm3" or "t/m3" or "kg/l" or "g/ml" => 1000, "g/m3" => 0.001, _ => null,
    });
    static readonly Quantity Conductivity = new("thermal conductivity (W/(m·K), mW/(m·K))", u => NormUnit(u) switch
    {
        "w/(mk)" or "w/mk" or "w/m/k" or "w/(mc)" or "w/mc" => 1, "mw/(mk)" or "mw/mk" or "mw/m/k" => 0.001, _ => null,
    });
    static readonly Quantity Length = new("length (m, cm, mm)", u => TableUnits.Find(u) is { Kind: "length" } f ? f.Unit.Factor : null);

    /// <summary>The standard sets' properties a unit converts for (to IFC's SI unit).</summary>
    static readonly Dictionary<string, Quantity> StandardQuantities = new(StringComparer.Ordinal)
    {
        ["YoungModulus"] = Stress, ["ShearModulus"] = Stress, ["HardeningModule"] = Stress, ["CompressiveStrength"] = Stress,
        ["YieldStress"] = Stress, ["UltimateStress"] = Stress, ["ProportionalStress"] = Stress,
        ["MassDensity"] = Density, ["ThermalConductivity"] = Conductivity, ["MaxAggregateSize"] = Length,
    };

    /// <summary>A ready-made parameter of the Data tab (materialData.ts, PARAM_PRESETS): the names it is known by (squashed), where it goes, its unit.</summary>
    sealed record Preset(HashSet<string> Names, string? Pset, string Name, string Kind, Quantity? Quantity = null, double HeldPer = 1, string Unit = "");

    static readonly Preset[] Presets =
    [
        new(["thermalconductivity", "thermalconductivityλ", "λ", "lambda", "conductivitatetermica", "conductivitatetermică", "conductivitatetermicaλ", "conductivitatetermicăλ", "wärmeleitfähigkeit", "warmeleitfahigkeit", "conductivitethermique", "conductivitéthermique"],
            "Pset_MaterialThermal", "ThermalConductivity", "number", Conductivity, 1, "W/(m·K)"),
        new(["density", "massdensity", "ρ", "rho", "densitate", "densitatea", "densitateaaparenta", "densitateaaparentă", "masaspecifica", "masăspecifică", "dichte", "rohdichte", "massevolumique", "densidad", "densità", "densita"],
            "Pset_MaterialCommon", "MassDensity", "number", Density, 1, "kg/m³"),
        new(["μ", "µ", "mu", "vapourresistance", "vaporresistance", "vapourresistanceμ", "vaporresistanceμ", "vapourresistancefactor", "vaporresistancefactor", "watervapourresistancefactor",
             "diffusionresistancefactor", "factorderezistentaladifuziavaporilor", "factorderezistențăladifuziavaporilor", "rezistentaladifuzie", "rezistențaladifuzie", "rezistențăladifuzie"],
            "Pset_MaterialHygroscopic", "UpperVaporResistanceFactor", "number"),
        new(StructuralMaterials.ModulusNames, null, "Young's modulus", "number", Stress, 1e6, "MPa"),
        new(StructuralMaterials.Compressive.Names, null, "Compressive strength", "number", Stress, 1e6, "MPa"),
        new(StructuralMaterials.Yield.Names, null, "Yield strength", "number", Stress, 1e6, "MPa"),
        new(["reactiontofire", "reactiontofireclass", "reactialafoc", "reacțialafoc", "clasadereactielafoc", "clasadereacțielafoc", "brandverhalten"],
            null, "Reaction to fire", "text"),
    ];

    /// <summary>
    /// Where a heading's values go: the set (null: the material's own parameters) and name, the kind a standard set or a preset
    /// holds it as (null: the material's own kind, else read from the value), the quantity its unit converts in and the factor it is
    /// held in (MPa: 1e6), the heading's unit; or why it cannot be written.
    /// </summary>
    public sealed record Col(string? Pset, string Name, string? Kind, string? HeadingUnit, string Describe, string? Error = null)
    {
        internal Quantity? Quantity { get; init; }
        internal double HeldPer { get; init; } = 1;
    }

    /// <summary>A heading's unit in brackets at its end (brackets inside it counted: <c>λ (W/(m·K))</c>), and the heading without it.</summary>
    public static (string Base, string? Unit) SplitHeading(string heading)
    {
        var h = heading.Trim();
        if (h.Length < 3 || h[^1] is not (')' or ']')) return (h, null);
        var depth = 0;
        for (var i = h.Length - 1; i >= 0; i--)
        {
            if (h[i] is ')' or ']') depth++;
            else if (h[i] is '(' or '[' && --depth == 0)
            {
                var b = h[..i].Trim();
                var u = h[(i + 1)..^1].Trim();
                return b.Length > 0 && u.Length > 0 ? (b, u) : (h, null);
            }
        }
        return (h, null);
    }

    /// <summary>Where the column of <paramref name="heading"/> writes (<see cref="Col"/>).</summary>
    public static Col Column(IModelView m, string heading)
    {
        var full = heading.Trim();
        // An own parameter some material already has under exactly this heading (its unit in its name) is that one.
        if (m.OfClass(IfcClass.Material).Any(x => Stored(x).Any(p => Params.Same(p, null, full))))
            return new(null, full, null, null, $"own parameter „{full}”");
        var (bare, unit) = SplitHeading(full);
        // Set.Name: the last dot parts them, unless what follows it starts with a space („Max. size” is a name).
        var dot = bare.LastIndexOf('.');
        if (dot > 0 && dot < bare.Length - 1 && !char.IsWhiteSpace(bare[dot + 1]) && !bare[..dot].Contains(' '))
        {
            var (set, name) = (bare[..dot].Trim(), bare[(dot + 1)..].Trim());
            if (set.Equals(StandardPsets.MaterialOwn, StringComparison.OrdinalIgnoreCase) || set.Equals(StandardPsets.Own, StringComparison.OrdinalIgnoreCase))
                return Own(unit != null ? $"{name} ({unit})" : name);
            if (StandardPsets.IsStandard(set))
            {
                var known = StandardPsets.All.FirstOrDefault(s => s.Name.Equals(set, StringComparison.OrdinalIgnoreCase) && StandardPsets.AppliesTo(s, IfcClass.Material));
                var prop = known?.Properties.Keys.FirstOrDefault(k => k.Equals(name, StringComparison.OrdinalIgnoreCase));
                try
                {
                    var (checkedSet, kind) = StandardPsets.Check(IfcClass.Material, known?.Name ?? set, prop ?? name);
                    var q = StandardQuantities.GetValueOrDefault(prop!);
                    if (unit != null && q != null && q.Per(unit) == null)
                        return new(checkedSet, prop!, kind, unit, full, $"„{unit}” is not a unit of {q.Name}.");
                    var shown = q == null ? (unit != null ? $" (as written: IFC holds it in its SI unit)" : "") : unit != null ? $", from {unit} to IFC's SI unit" : " (IFC's SI unit)";
                    return new(checkedSet, prop!, kind, unit, $"{checkedSet}.{prop}{shown}") { Quantity = q };
                }
                catch (CommandException ex) { return new(set, name, null, unit, full, ex.Message); }
            }
            try
            {
                var (checkedSet, _) = StandardPsets.Check(IfcClass.Material, set, name);
                var n = unit != null ? $"{name} ({unit})" : name;
                return new(checkedSet, n, null, null, $"{checkedSet}.{n}");
            }
            catch (CommandException ex) { return new(set, name, null, unit, full, ex.Message); }
        }
        // A ready-made parameter of the Data tab, by its names and symbols.
        var squashed = TagText.Squash(bare);
        var raw = bare.Trim().ToLowerInvariant();
        if (Presets.FirstOrDefault(p => p.Names.Contains(squashed) || p.Names.Contains(raw)) is { } preset)
        {
            var key = preset.Pset != null ? $"{preset.Pset}.{preset.Name}" : $"own parameter „{preset.Name}”";
            if (unit != null && (preset.Quantity == null || preset.Quantity.Per(unit) == null))
                return new(preset.Pset, preset.Name, preset.Kind, unit, key, preset.Quantity == null ? $"{key} has no unit; „{unit}” cannot be converted." : $"„{unit}” is not a unit of {preset.Quantity.Name}.");
            var into = preset.Unit.Length > 0 ? $" in {preset.Unit}" : "";
            return new(preset.Pset, preset.Name, preset.Kind, unit, unit != null ? $"{key}{into}, from {unit}" : $"{key}{into}") { Quantity = preset.Quantity, HeldPer = preset.HeldPer };
        }
        return Own(full);
    }

    static Col Own(string name) => new(null, name, null, null, $"own parameter „{name}”");

    static IEnumerable<JsonObject> Stored(Entity mat) => (mat.Attrs["params"] as JsonArray)?.OfType<JsonObject>() ?? [];

    // ------------------------------------------------------------------ rows

    /// <summary>A name as rows match it: without case or any spaces.</summary>
    public static string Key(string name) => new(name.Where(c => !char.IsWhiteSpace(c)).Select(char.ToLowerInvariant).ToArray());

    /// <summary>The project's material of this name: the same name (case aside), else the only one of the same name without spaces; null when none.</summary>
    public static Entity? Find(IModelView m, string name)
    {
        if (Materials.Find(m, name.Trim()) is { } exact) return exact;
        var k = Key(name);
        var same = m.OfClass(IfcClass.Material).Where(x => Key(x.Name) == k).ToList();
        if (same.Count > 1) throw new CommandException($"„{name.Trim()}” matches {same.Count} materials: {string.Join(", ", same.Select(x => $"„{x.Name}”"))}.");
        return same.FirstOrDefault();
    }

    static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>A cell as the kind a parameter is held as: a number in its unit (the heading's or the cell's converted), yes or no, text.</summary>
    static JsonNode? Value(Col c, string kind, string text, JsonArray? options)
    {
        var at = c.Pset != null ? $"{c.Pset}.{c.Name}" : c.Name;
        switch (kind)
        {
            case "number" or "length" or "area" or "angle":
            {
                var (n, cellUnit) = TableUnits.Split(text) ?? throw new CommandException($"{at}: „{text}” is not a number.");
                var unit = cellUnit ?? c.HeadingUnit;
                if (unit != null && c.Quantity != null)
                {
                    var per = c.Quantity.Per(unit) ?? throw new CommandException($"{at}: „{unit}” is not a unit of {c.Quantity.Name}.");
                    n = n * per / c.HeldPer;
                }
                else if (unit != null && kind == "length") n *= TableUnits.Factor("length", unit);
                else if (cellUnit != null && c.Quantity == null && TableUnits.Parse(text) == null)
                    throw new CommandException($"{at}: „{text}” is not a number (its unit goes in the heading).");
                return Params.Check(kind, JsonValue.Create(n), options);
            }
            case "bool":
                return TableImport.Norm(text) is "yes" or "true" or "da" or "ja" or "oui" or "si" or "sim" or "tak" or "1" or "x" ? JsonValue.Create(true)
                     : TableImport.Norm(text) is "no" or "false" or "nu" or "nein" or "non" or "nao" or "nie" or "0" ? JsonValue.Create(false)
                     : throw new CommandException($"{at}: „{text}” is not yes or no.");
            default:
                try { return Params.Check(kind, JsonValue.Create(text), options); }
                catch (CommandException ex) { throw new CommandException($"{at}: {ex.Message}"); }
        }
    }

    static IReadOnlyList<TableStep> Steps(IQueryContext q, TableRow row)
    {
        var m = q.Model;
        var name = row.Text("material");
        var id = row.Text("id");
        if (name == null && id == null) throw new CommandException("The row names no material (a Material or an Id column).");
        Entity? mat = null;
        if (id != null)
        {
            mat = m.Get(id) is { Class: IfcClass.Material } byId ? byId : null;
            if (mat == null && name == null) throw new CommandException($"No material has the id „{id}”.");
        }
        mat ??= Find(m, name!);

        // The same material twice in one table: the later row is refused.
        var seen = row.Shared.TryGetValue("seen", out var s) ? (Dictionary<string, int>)s : (Dictionary<string, int>)(row.Shared["seen"] = new Dictionary<string, int>());
        var who = mat?.Id ?? "new:" + Key(name!);
        if (seen.TryGetValue(who, out var first)) throw new CommandException($"„{mat?.Name ?? name}” is already in row {first}.");
        seen[who] = row.Number;

        if (mat == null)
        {
            var choice = row.Text("ifMissing") is { } im ? TableImport.Choice(Target.Columns.First(c => c.Key == "ifMissing"), im) : null;
            if (choice != Create) return [TableStep.Note($"„{name}”: no such material in the project, skipped", "skipped")];
        }

        // Its parameters, each cell written over the one of the same set and name (the others kept).
        var list = mat != null ? new JsonArray(Stored(mat).Select(p => (JsonNode)p.DeepClone()).ToArray()) : [];
        var said = new List<string>();
        var cols = row.Shared.TryGetValue("columns", out var cc) ? (Dictionary<string, Col>)cc : (Dictionary<string, Col>)(row.Shared["columns"] = new Dictionary<string, Col>());
        foreach (var (heading, text) in row.OpenCells)
        {
            if (!cols.TryGetValue(heading, out var col)) cols[heading] = col = Column(m, heading);
            if (col.Error != null) throw new CommandException($"{heading}: {col.Error}");
            var existing = list.OfType<JsonObject>().FirstOrDefault(p => Params.Same(p, col.Pset, col.Name));
            var kind = col.Kind ?? (string?)existing?["kind"] ?? (TableUnits.Parse(text) != null ? "number" : "text");
            var value = Value(col, kind, text, existing?["options"] as JsonArray);
            var entry = existing ?? new JsonObject { ["name"] = col.Name };
            entry["kind"] = kind;
            entry["value"] = value;
            entry.Remove(ParamFormulas.Key);
            if (col.Pset != null) entry["pset"] = col.Pset;
            if (existing == null) list.Add((JsonNode)entry);
            said.Add($"{(col.Pset != null ? col.Pset + "." : "")}{col.Name} = {(value is JsonValue jv && jv.TryGetValue<double>(out var d) ? Num(d) : value?.ToString())}");
        }
        var description = row.Text("description");
        var category = row.Text("category");
        if (description != null) said.Add($"description „{(description.Length > 40 ? description[..40] + "…" : description)}”");
        if (category != null) said.Add($"category {category}");

        if (mat == null)
        {
            var args = new JsonObject { ["name"] = name!.Trim() };
            if (list.Count > 0) args["params"] = list;
            if (description != null) args["description"] = description;
            if (category != null) args["category"] = category;
            return [new("material.create", args, $"New material „{name!.Trim()}”{(said.Count > 0 ? ": " + string.Join(", ", said) : "")}", "new")];
        }

        var steps = new List<TableStep>();
        var label = $"„{mat.Name}”";
        if (id != null && name != null && name.Trim() != mat.Name)
        {
            if (Materials.Find(m, name.Trim()) is { } other && other.Id != mat.Id) throw new CommandException($"There is already a material „{name.Trim()}”.");
            steps.Add(new("material.update", new JsonObject { ["id"] = mat.Id, ["name"] = name.Trim() }, $"{label}: renamed „{name.Trim()}”", "matched"));
        }
        var data = new JsonObject { ["id"] = mat.Id };
        var stored = mat.Attrs["params"] as JsonArray ?? [];
        if (!JsonNode.DeepEquals(Params.ParseList(list, IfcClass.Material), Params.ParseList(stored, IfcClass.Material))) data["params"] = list;
        if (description != null && description.Replace("\r\n", "\n").TrimEnd() != mat.Str("description")) data["description"] = description;
        if (data.Count > 1) steps.Add(new("material.setData", data, $"{label}: {string.Join(", ", said)}", "matched"));
        if (category != null && category != mat.Str("category"))
            steps.Add(new("material.update", new JsonObject { ["id"] = mat.Id, ["category"] = category }, $"{label}: category {category}", "matched"));
        if (steps.Count == 0) steps.Add(TableStep.Note($"{label}: {(said.Count > 0 ? "unchanged" : "nothing to change")}", "matched"));
        return steps;
    }

    // ------------------------------------------------------------------ export

    /// <summary>
    /// The project's materials (or those of <paramref name="ids"/>) as the table the import reads: Material, Category, Description,
    /// a column a parameter (Set.Name, or its own name; a formula's left empty), Id last. Numbers with a point, as stored.
    /// </summary>
    public static List<List<string>> Export(IModelView m, IReadOnlyCollection<string>? ids = null)
    {
        var mats = m.OfClass(IfcClass.Material).Where(x => ids == null || ids.Contains(x.Id)).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var keys = new List<(string? Pset, string Name)>();
        foreach (var p in mats.SelectMany(Stored))
        {
            var (set, n) = (Params.SetOf(p), ((string?)p["name"])?.Trim() ?? "");
            if (n.Length > 0 && !keys.Any(k => string.Equals(k.Pset, set, StringComparison.OrdinalIgnoreCase) && string.Equals(k.Name, n, StringComparison.OrdinalIgnoreCase))) keys.Add((set, n));
        }
        // The standard sets first, in IFC's order; then the user's sets; the own parameters last.
        int Rank((string? Pset, string Name) k) => k.Pset == null ? 2 : StandardPsets.IsStandard(k.Pset) ? 0 : 1;
        keys = [.. keys.Select((k, i) => (k, i)).OrderBy(x => Rank(x.k)).ThenBy(x => x.i).Select(x => x.k)];
        var header = new List<string> { "Material", "Category", "Description" };
        header.AddRange(keys.Select(k => k.Pset != null ? $"{k.Pset}.{k.Name}" : k.Name));
        header.Add("Id");
        var rows = new List<List<string>> { header };
        foreach (var mat in mats)
        {
            var row = new List<string> { mat.Name, mat.Str("category") ?? "", mat.Str("description") ?? "" };
            var stored = Stored(mat).ToList();
            foreach (var (set, n) in keys)
            {
                var p = stored.FirstOrDefault(x => Params.Same(x, set, n));
                row.Add(p == null || p[ParamFormulas.Key] != null ? "" : p["value"] switch
                {
                    null => "",
                    JsonValue v when v.TryGetValue<bool>(out var b) => b ? "yes" : "no",
                    JsonValue v when v.TryGetValue<double>(out var d) => Num(d),
                    var v => v.ToString(),
                });
            }
            row.Add(mat.Id);
            rows.Add(row);
        }
        return rows;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Service<TableTarget>(Target);

        // The materials' data as a table to fill in and import back (Manage → Materials → Export data…): {format?: csv | xlsx, ids?}
        // → {text, file} (CSV, semicolons) or {data (base64), file} (a workbook).
        r.Query("material.exportData", (q, a) =>
        {
            var ids = a["ids"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToHashSet() : null;
            var rows = Export(q.Model, ids);
            if ((string?)a["format"] == "xlsx")
                return new JsonObject
                {
                    ["data"] = Convert.ToBase64String(XlsxWriter.Write([("Materials", rows.Select(x => (IReadOnlyList<string>)x).ToList())])),
                    ["file"] = "materials-data.xlsx",
                };
            var sb = new StringBuilder();
            foreach (var row in rows) sb.Append(CsvText.Line(row)).Append("\r\n");
            return new JsonObject { ["text"] = sb.ToString(), ["file"] = "materials-data.csv" };
        });
    }
}
