using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Schedules;

/// <summary>A field schedule's column: a field (any token of <see cref="FieldCatalog"/>) or a formula, its heading, how its values add up, its unit and decimals.</summary>
public sealed record FieldColumn(string? Field, string? Formula, string? Heading, string? Aggregate, string? Unit, int? Decimals)
{
    public static readonly string[] Aggregates = ["sum", "avg", "min", "max", "count", "countDistinct", "first", "list"];

    /// <summary>What the column reads on an element: its field, or its formula as <c>= …</c>.</summary>
    public string Token => Formula != null ? "=" + Formula : Field ?? "";

    public JsonObject ToJson()
    {
        var o = new JsonObject();
        if (Field != null) o["field"] = Field;
        if (Formula != null) o["formula"] = Formula;
        if (Heading != null) o["heading"] = Heading;
        if (Aggregate != null) o["aggregate"] = Aggregate;
        if (Unit != null) o["unit"] = Unit;
        if (Decimals != null) o["decimals"] = Decimals;
        return o;
    }
}

/// <summary>A field schedule's options (<c>fields</c> on the schedule): its filter, the fields its rows are grouped by (none: a row per element), its columns, its totals line and count column.</summary>
public sealed record FieldScheduleOptions(ElementFilter? Filter, IReadOnlyList<string> GroupBy, IReadOnlyList<FieldColumn> Columns, bool Totals = true, bool Count = true)
{
    public const string Key = "fields";
    public const int MaxColumns = 64, MaxGroups = 8;

    /// <summary>Without columns: its kind, type and name.</summary>
    public static readonly FieldColumn[] DefaultColumns = [new("class", null, "Kind", null, null, null), new("type", null, "Type", null, null, null), new("name", null, "Name", null, null, null)];

    public static FieldScheduleOptions Of(Entity? schedule) => Read(schedule?.Attrs[Key]);

    static int? Int(JsonNode? n) => Json.Number(n) is { } v ? (int)Math.Clamp(Math.Round(v), 0, 9) : null;
    static string? Text(JsonNode? n) => ((string?)n)?.Trim() is { Length: > 0 } s ? s : null;
    static bool Flag(JsonNode? n, bool fallback) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    /// <summary>Its formula's body, without the equals sign or the braces it may be written with.</summary>
    static string? FormulaBody(string? f) => f == null ? null : ElementFields.FormulaOf(f) ?? (f.Trim() is { Length: > 0 } t ? t : null);

    static FieldColumn? Column(JsonNode? n)
    {
        if (n is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim().Length > 0)
            return ElementFields.FormulaOf(s) is { } fs ? new(null, fs, null, null, null, null) : new(s.Trim(), null, null, null, null, null);
        if (n is not JsonObject o) return null;
        var formula = FormulaBody(Text(o["formula"]));
        var field = formula == null ? Text(o["field"]) : null;
        if (field != null && ElementFields.FormulaOf(field) is { } ff) (formula, field) = (ff, null);
        if (field == null && formula == null) return null;
        var aggregate = Text(o["aggregate"]) is { } ag ? FieldColumn.Aggregates.FirstOrDefault(x => string.Equals(x, ag, StringComparison.OrdinalIgnoreCase)) : null;
        return new(field, formula, Text(o["heading"]), aggregate, Text(o["unit"]), Int(o["decimals"]));
    }

    /// <summary>The options as stored (what does not read left out).</summary>
    public static FieldScheduleOptions Read(JsonNode? n)
    {
        if (n is not JsonObject o) return new(null, [], DefaultColumns);
        var groups = (o["groupBy"] as JsonArray ?? []).Select(x => Text(x)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxGroups).ToList();
        var columns = (o["columns"] as JsonArray ?? []).Select(Column).OfType<FieldColumn>().Take(MaxColumns).ToList();
        return new(ElementFilter.Read(o["filter"]), groups, columns.Count > 0 ? columns : DefaultColumns, Flag(o["totals"], true), Flag(o["count"], true));
    }

    /// <summary>The options as a command gives them, checked: each column a field or a formula that reads, an aggregate Lapin knows.</summary>
    public static FieldScheduleOptions Check(JsonNode? n)
    {
        if (n is null) return new(null, [], DefaultColumns);
        if (n is not JsonObject o) throw new CommandException("A field schedule's options are an object: {filter, groupBy, columns, totals}.");
        ElementFilter.Check(o["filter"]);
        if (o["groupBy"] is not null and not JsonArray) throw new CommandException("groupBy is a list of fields.");
        if ((o["groupBy"] as JsonArray)?.Count > MaxGroups) throw new CommandException($"A schedule groups by at most {MaxGroups} fields.");
        var columns = o["columns"] as JsonArray ?? [];
        if (o["columns"] is not null and not JsonArray) throw new CommandException("columns is a list: [{field | formula, heading, aggregate, unit, decimals}].");
        if (columns.Count > MaxColumns) throw new CommandException($"A schedule has at most {MaxColumns} columns.");
        foreach (var c in columns)
        {
            if (Column(c) is not { } col) throw new CommandException("A column reads a field or a formula: {field} or {formula}.");
            if (c is JsonObject co && Text(co["aggregate"]) is { } ag && !FieldColumn.Aggregates.Contains(ag, StringComparer.OrdinalIgnoreCase))
                throw new CommandException($"„{ag}”: a column adds up by {string.Join(", ", FieldColumn.Aggregates)}.");
            if (col.Formula != null && !ParamFormulas.IsTemplate(col.Formula)) ParamFormulas.Check(col.Formula);
        }
        foreach (var x in o["groupBy"] as JsonArray ?? []) if (ElementFields.FormulaOf(Text(x) ?? "") is { } gf) ParamFormulas.Check(gf);
        return Read(o);
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject();
        if (Filter is { IsEmpty: false } f) o["filter"] = f.ToJson();
        if (GroupBy.Count > 0) o["groupBy"] = new JsonArray([.. GroupBy.Select(x => (JsonNode)x)]);
        o["columns"] = new JsonArray([.. Columns.Select(c => (JsonNode)c.ToJson())]);
        if (!Totals) o["totals"] = false;
        if (!Count) o["count"] = false;
        return o;
    }
}

/// <summary>
/// A schedule of the user's own (docs/plan-user-extensibility.md, „Extrase și câmpuri configurabile”): the elements a filter
/// takes — native ones and those of IFC, Fragments and BIM Open Schema files alike —, one row each or a row per value of the
/// fields it groups by, and columns of any field (<see cref="FieldCatalog"/>: attributes, quantities, parameters, the property
/// sets a file gave, scopes, the office's aliases) or formula, each added up as it says (sum, average, min, max, count, distinct
/// count, first, the list of its values), with a totals line. A schedule like any: on sheets, CSV, XLSX, charts, columns and rows
/// of its own, cells typed over, sorted by a column. Its options are the schedule's <c>fields</c> (<see cref="FieldScheduleOptions"/>).
/// </summary>
public sealed class FieldSchedule : IScheduleSource
{
    public const string KindId = "fields";
    public string Kind => KindId;
    public const string Title = "Field schedule";
    public string Label => Title;

    public ScheduleGrid Grid(IQueryContext q) => Table(q, null);
    public ScheduleGrid Grid(IQueryContext q, Entity schedule) => Table(q, schedule);

    static readonly StringComparer Natural = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);

    /// <summary>A unit a column may show its numbers in, by its factor from SI; another unit is only written in its heading.</summary>
    static readonly Dictionary<string, double> Units = new(StringComparer.Ordinal)
    {
        ["m"] = 1, ["cm"] = 100, ["mm"] = 1000, ["km"] = 0.001, ["m²"] = 1, ["m2"] = 1, ["cm²"] = 1e4, ["mm²"] = 1e6, ["ha"] = 1e-4,
        ["m³"] = 1, ["m3"] = 1, ["dm³"] = 1000, ["l"] = 1000, ["L"] = 1000, ["cm³"] = 1e6, ["kg"] = 1, ["t"] = 0.001,
    };

    /// <summary>
    /// A field's heading: its name, capitalised, the set it is in left out (<c>Pset_Firma.CodLucrare</c>: CodLucrare); a scope's
    /// kept (<c>storey.name</c>); <c>class</c> is the Kind.
    /// </summary>
    public static string FieldHeading(string field)
    {
        var f = field.Trim();
        if (f.Equals("class", StringComparison.OrdinalIgnoreCase)) return "Kind";
        if (f.Contains('{') || f.StartsWith('=')) return f;
        var dot = f.IndexOf('.');
        if (dot > 0 && !ParamScopes.IsScope(f[..dot])) f = f[(f.LastIndexOf('.') + 1)..];
        return f.Length > 0 ? char.ToUpperInvariant(f[0]) + f[1..] : f;
    }

    static string HeadingOf(FieldColumn c)
    {
        var h = c.Heading ?? (c.Formula != null ? "= " + c.Formula : FieldHeading(c.Field!));
        return c.Unit != null && !h.Contains('(') ? $"{h} ({c.Unit})" : h;
    }

    sealed class Bucket
    {
        public readonly List<string> Ids = [];
        public string[] Cells = [];
        public List<FieldValue>[] Values = [];
    }

    static ScheduleGrid Table(IQueryContext q, Entity? schedule)
    {
        var o = FieldScheduleOptions.Of(schedule);
        var units = DraftingResources.Book(q.Model).Units;
        var elements = ElementFilter.Elements(q, o.Filter, units);
        var columns = o.Columns;
        var grouped = o.GroupBy.Count > 0;
        var buckets = new Dictionary<string, Bucket>(StringComparer.Ordinal);
        var all = Enumerable.Range(0, columns.Count).Select(_ => new List<FieldValue>()).ToArray();
        foreach (var e in elements)
        {
            var f = new ElementFields(q, e, units);
            var groupCells = o.GroupBy.Select(g => f.Value(g) is { Exists: true } v ? v.Shown : "—").ToArray();
            var key = grouped ? string.Join("\u001f", groupCells) : e.Id;
            if (!buckets.TryGetValue(key, out var b))
                buckets[key] = b = new Bucket { Cells = groupCells, Values = [.. Enumerable.Range(0, columns.Count).Select(_ => new List<FieldValue>())] };
            b.Ids.Add(e.Id);
            for (var i = 0; i < columns.Count; i++)
            {
                var v = f.Value(columns[i].Token);
                b.Values[i].Add(v);
                all[i].Add(v);
            }
        }
        // A column is of numbers when every value it has is one; its aggregate by default: numbers summed, texts listed.
        var numeric = all.Select(vs => vs.Any(v => v.Exists) && vs.Where(v => v.Exists).All(v => v.Number != null)).ToArray();
        string AggregateOf(int i) => columns[i].Aggregate ?? (numeric[i] ? "sum" : "list");

        string Number(int i, double v, bool whole = false)
        {
            var c = columns[i];
            if (!whole && c.Unit != null && Units.TryGetValue(c.Unit, out var factor)) v *= factor;
            if (whole) return Math.Round(v).ToString("0", CultureInfo.InvariantCulture);
            return c.Decimals is { } d ? v.ToString("F" + d, CultureInfo.InvariantCulture) : Expr.Format(v, null);
        }

        string Cell(int i, List<FieldValue> vs, bool total)
        {
            var present = vs.Where(v => v.Exists).ToList();
            var numbers = present.Where(v => v.Number != null).Select(v => v.Number!.Value).ToList();
            switch (AggregateOf(i))
            {
                case "count": return present.Count.ToString(CultureInfo.InvariantCulture);
                case "countDistinct": return present.Select(v => v.Shown).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString(CultureInfo.InvariantCulture);
                case "sum": return numbers.Count == 0 ? "" : Number(i, numbers.Sum());
                case "avg": return numbers.Count == 0 ? "" : Number(i, numbers.Average());
                case "min": return numbers.Count > 0 && numbers.Count == present.Count ? Number(i, numbers.Min()) : present.Select(v => v.Shown).Order(Natural).FirstOrDefault() ?? "";
                case "max": return numbers.Count > 0 && numbers.Count == present.Count ? Number(i, numbers.Max()) : present.Select(v => v.Shown).Order(Natural).LastOrDefault() ?? "";
            }
            if (total) return "";
            if (AggregateOf(i) == "first" || present.Count <= 1)
                return present.FirstOrDefault() is { Exists: true } first ? first.Number is { } n && (first.Text == null || FieldValue.StrictNumber(first.Text) != null) ? Number(i, n) : first.Shown : "";
            return string.Join(", ", present.Select(v => v.Number is { } n && (v.Text == null || FieldValue.StrictNumber(v.Text) != null) ? Number(i, n) : v.Shown).Distinct(StringComparer.OrdinalIgnoreCase).Order(Natural));
        }

        var count = grouped && o.Count;
        var rows = new List<(string Key, List<string> Cells)>();
        foreach (var (key, b) in buckets)
        {
            var cells = new List<string>(b.Cells);
            if (count) cells.Add(b.Ids.Count.ToString(CultureInfo.InvariantCulture));
            for (var i = 0; i < columns.Count; i++)
                cells.Add(grouped ? Cell(i, b.Values[i], false) : Single(i, b.Values[i][0]));
            rows.Add((grouped ? string.Join("|", b.Cells) : key, cells));
        }

        string Single(int i, FieldValue v) =>
            !v.Exists ? "" : v.Number is { } n && (v.Text == null || FieldValue.StrictNumber(v.Text) != null || columns[i].Unit != null || columns[i].Decimals != null) ? Number(i, n) : v.Shown;

        // Rows in the order of their cells, left to right (a group's "—" after the rest), as a person sorts them.
        int Compare((string Key, List<string> Cells) x, (string Key, List<string> Cells) y)
        {
            for (var i = 0; i < Math.Min(x.Cells.Count, y.Cells.Count); i++)
            {
                var (a, b) = (x.Cells[i], y.Cells[i]);
                if (i < o.GroupBy.Count && (a == "—") != (b == "—")) return a == "—" ? 1 : -1;
                var c = FieldValue.StrictNumber(a) is { } na && FieldValue.StrictNumber(b) is { } nb ? na.CompareTo(nb) : Natural.Compare(a, b);
                if (c != 0) return c;
            }
            return 0;
        }
        rows.Sort(Compare);

        var headings = new List<string>([.. o.GroupBy.Select(g => HeadingOf(new FieldColumn(g, null, null, null, null, null)))]);
        if (count) headings.Add("Count");
        headings.AddRange(columns.Select(HeadingOf));
        // Headings name the columns (typed cells, charts): one named twice is numbered.
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < headings.Count; i++)
        {
            var n = seen[headings[i]] = seen.GetValueOrDefault(headings[i]) + 1;
            if (n > 1) headings[i] = $"{headings[i]} {n}";
        }

        var grid = rows.Select(r => (IReadOnlyList<string>)r.Cells).ToList();
        var keys = rows.Select(r => (string?)r.Key).ToList();
        if (o.Totals && rows.Count > 1)
        {
            var total = new List<string>(Enumerable.Repeat("", o.GroupBy.Count));
            if (count) total.Add(elements.Count.ToString(CultureInfo.InvariantCulture));
            for (var i = 0; i < columns.Count; i++) total.Add(Cell(i, all[i], true));
            total[0] = "Total";
            grid.Add(total);
            keys.Add(null);
        }
        return new ScheduleGrid(schedule?.Name ?? Title, headings, grid, keys);
    }

    // ------------------------------------------------------------ templates (.lapinschedule)

    public const string FileFormat = "lapin-schedule";

    /// <summary>
    /// A field schedule as a template (<c>.lapinschedule</c>, the computer's library): its name, its options with the filter's
    /// types, storeys, materials, groups and phases by their names (another project finds its own by them), its columns of its
    /// own and its charts.
    /// </summary>
    public static JsonObject Template(IModelView m, Entity s)
    {
        var options = FieldScheduleOptions.Of(s);
        if (options.Filter is { } f)
        {
            string NameOf(string id) => m.Get(id) is { } x ? ReferenceTypes.Name(x) ?? x.Name : id;
            var phases = ((Spatial.Project(m)?.Attrs["phases"] as JsonObject)?["phases"] as JsonArray ?? []).OfType<JsonObject>().ToList();
            string PhaseName(string id) => phases.FirstOrDefault(p => (string?)p["id"] == id) is { } p ? (string?)p["name"] ?? id : id;
            options = options with
            {
                Filter = f with
                {
                    Types = [.. f.Types.Select(NameOf)], Storeys = [.. f.Storeys.Select(NameOf)], Materials = [.. f.Materials.Select(NameOf)],
                    Groups = [.. f.Groups.Select(NameOf)], Phases = [.. f.Phases.Select(PhaseName)],
                },
            };
        }
        var o = new JsonObject { ["name"] = s.Name, [FieldScheduleOptions.Key] = options.ToJson() };
        if (s.Attrs["customColumns"] is JsonArray cc && cc.Count > 0) o["customColumns"] = cc.DeepClone();
        if (s.Attrs["charts"] is JsonArray ch && ch.Count > 0) o["charts"] = ch.DeepClone();
        return LibraryFile.Write(Spec, o, LibraryFile.SourceOf(m));
    }

    public static LibraryFile.Spec Spec => LibraryFile.Of(FileFormat);

    /// <summary>A template's text or object read and checked; refused when it is not one.</summary>
    public static JsonObject ReadTemplate(JsonNode? given)
    {
        var n = given;
        if (given is JsonValue v && v.TryGetValue<string>(out var text))
            try { n = JsonNode.Parse(text); } catch (JsonException) { throw new CommandException("That is not a schedule file (.lapinschedule)."); }
        if (n is not JsonObject given0 || (string?)given0["format"] != FileFormat) throw new CommandException("That is not a schedule file (.lapinschedule).");
        var o = LibraryFile.Read(given0, Spec).Root;
        FieldScheduleOptions.Check(o[FieldScheduleOptions.Key]);
        return o;
    }

    public static string TemplateText(JsonObject template) => template.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
}
