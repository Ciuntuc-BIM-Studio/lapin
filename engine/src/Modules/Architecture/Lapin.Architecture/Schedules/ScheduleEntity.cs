using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Schedules;

/// <summary>One table of a schedule: its title and what it lists (every condition must hold; absent ones let everything through).</summary>
public sealed record SchedulePart(string Id, string Title, string? Kind = null, IReadOnlyList<string>? Levels = null, bool? External = null,
    IReadOnlyList<string>? Types = null, IReadOnlyList<string>? Exclude = null, IReadOnlyList<string>? Include = null)
{
    static List<string>? Strings(JsonNode? n) => n is JsonArray a ? a.Select(x => (string?)x).OfType<string>().Where(s => s.Length > 0).ToList() is { Count: > 0 } l ? l : null : null;
    public static SchedulePart? From(JsonNode? n) => n is JsonObject o && (string?)o["id"] is { } id
        ? new SchedulePart(id, (string?)o["title"] ?? "", (string?)o["kind"] is "door" or "window" ? (string?)o["kind"] : null, Strings(o["levels"]),
            o["external"] is JsonValue e && e.TryGetValue<bool>(out var b) ? b : null, Strings(o["types"]), Strings(o["exclude"]), Strings(o["include"]))
        : null;
}

/// <summary>
/// A joinery or lintel schedule (the old Lapin's): a <c>LapinSchedule</c>
/// entity with its kind (door, window, all, lintel), the scale of its
/// drawings, its rows, typed cells, custom rows, card order and tables. Its
/// cards are laid out on demand from the model; a sheet places its pages.
/// </summary>
public sealed class ScheduleEntity : ISheetContent
{
    public const string Class = "LapinSchedule";
    public static readonly string[] Kinds = ["door", "window", "all", "lintel"];

    public bool Handles(Entity e) => e.Class == Class;

    public static List<SchedulePart> Parts(Entity s) => s.Attrs["parts"] is JsonArray a ? a.Select(SchedulePart.From).OfType<SchedulePart>().ToList() : [];

    /// <summary>The schedule's columns of its own (decision 251): an id and its mark.</summary>
    public static List<(string Id, string Mark)> ManualEntries(Entity s) => s.Attrs["manual"] is JsonArray a
        ? a.OfType<JsonObject>().Where(o => (string?)o["id"] != null).Select(o => ((string)o["id"]!, (string?)o["mark"] ?? "")).ToList() : [];

    /// <summary>Whether the schedule lists types nothing in the model uses (decision 251: by default it does not).</summary>
    public static bool ShowsUnused(Entity s) => s.Attrs["unused"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>
    /// What a joinery or lintel schedule lists: the model's types (those in use unless it shows the unused too), then its
    /// own columns, each a card with only what is typed in it.
    /// </summary>
    public static ScheduleData Data(IQueryContext q, Entity s)
    {
        var kind = s.Str("kind") ?? "all";
        var data = Schedules.Of(q, kind);
        var entries = kind == "lintel" || ShowsUnused(s) ? data.Entries : data.Entries.Where(e => e.Total > 0).ToList();
        var typed = ScheduleTable.Of(s).Cells;
        entries = [.. entries, .. ManualEntries(s).Select(m => new ScheduleEntry("manual:" + m.Id, ScheduleEntry.Manual, null, m.Mark,
            typed.GetValueOrDefault("manual:" + m.Id)?.GetValueOrDefault("name") ?? "", 0, 0, [], 0, []))];
        return data with { Entries = entries };
    }

    /// <summary>A module's table as the schedule shows it: its own columns and rows, typed cells, its order.</summary>
    public static EditableGrid SourceGrid(IQueryContext q, Entity s, IScheduleSource source) => ScheduleGrids.Apply(source.Grid(q, s), s);

    /// <summary>The table a schedule's charts read: a module's as shown, or the joinery's as its CSV lays it out.</summary>
    public static (IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows) ChartGrid(IQueryContext q, Entity s)
    {
        if (Source(q.Services, s.Str("kind")) is { } source) { var g = SourceGrid(q, s, source).ToGrid(); return (g.Columns, g.Rows); }
        var grid = Grid(s, Data(q, s)).Select(r => (IReadOnlyList<string>)r.ToList()).ToList();
        return (grid[0], grid.Skip(1).ToList());
    }

    /// <summary>Each chart drawn, its heading over it (a chart that reads a column the table no longer has says so).</summary>
    static List<(string Title, List<ScheduleBand> Bands)> Charts(IQueryContext q, Entity s, double maxWidth)
    {
        var charts = ScheduleChart.Of(s);
        if (charts.Count == 0) return [];
        var (columns, rows) = ChartGrid(q, s);
        return charts.Select(c =>
        {
            try { return (c.Heading, new List<ScheduleBand> { ScheduleCharts.Draw(c, ScheduleCharts.Values(c, columns, rows), maxWidth) }); }
            catch (CommandException ex) { return (c.Heading, new List<ScheduleBand> { ScheduleCharts.Message(ex.Message) }); }
        }).ToList();
    }

    /// <summary>
    /// A table's entries: whole types when only its kind or types are set (unplaced
    /// ones too), else the occurrences that pass (not excluded; included, or of its
    /// kind and types, on its levels, external or not), recounted.
    /// </summary>
    public static ScheduleData PartData(ScheduleData data, SchedulePart p)
    {
        var levels = p.Levels == null ? data.Levels : data.Levels.Where(p.Levels.Contains).ToList();
        bool TypeOk(ScheduleEntry e) => (p.Kind == null || e.Kind == p.Kind) && (p.Types == null || p.Types.Contains(e.Key));
        var whole = p.Levels == null && p.External == null && p.Exclude == null && p.Include == null;
        var entries = new List<ScheduleEntry>();
        foreach (var e in data.Entries)
        {
            if (e.Kind == ScheduleEntry.Manual) { entries.Add(e); continue; }
            if (whole) { if (TypeOk(e)) entries.Add(e); continue; }
            var kept = e.Occurrences.Where(o => p.Exclude?.Contains(o.Id) != true && (p.Include?.Contains(o.Id) == true
                || TypeOk(e) && (p.Levels == null || p.Levels.Contains(o.Level))
                   && (p.External == null || (p.External.Value ? o.External == true : o.External != true)))).ToList();
            if (kept.Count == 0) continue;
            var counts = kept.GroupBy(o => o.Level).ToDictionary(g => g.Key, g => g.Count());
            entries.Add(e with { Occurrences = kept, Counts = counts, Total = kept.Count });
        }
        return data with { Entries = entries, Levels = levels };
    }

    /// <summary>The schedule's tables: one of everything under its name, or one per part (empty ones left out).</summary>
    public static List<(string Title, ScheduleData Data)> Tables(Entity s, ScheduleData data)
    {
        var parts = Parts(s);
        return parts.Count == 0 ? [(s.Name, data)] : parts.Select(p => (p.Title, PartData(data, p))).Where(t => t.Item2.Entries.Count > 0).ToList();
    }

    /// <summary>
    /// The check: what two tables list, what none does (when it has parts), and a
    /// mark carried by more than one type in one table; elements reviewed on purpose
    /// are left out of the first two.
    /// </summary>
    public static (Dictionary<string, List<string>> Twice, List<string> None, List<(string Part, string Mark, int Count)> Marks) Check(Entity s, ScheduleData data)
    {
        var parts = Parts(s);
        var reviewed = s.Attrs["reviewed"] is JsonArray r ? r.Select(x => (string?)x).OfType<string>().ToHashSet() : [];
        var twice = new Dictionary<string, List<string>>();
        var none = new List<string>();
        if (parts.Count > 0)
        {
            var listing = new Dictionary<string, List<string>>();
            foreach (var p in parts)
                foreach (var o in PartData(data, p).Entries.SelectMany(e => e.Occurrences))
                    (listing.TryGetValue(o.Id, out var l) ? l : listing[o.Id] = []).Add(p.Id);
            foreach (var (id, ps) in listing) if (ps.Count > 1 && !reviewed.Contains(id)) twice[id] = ps;
            none = data.Entries.SelectMany(e => e.Occurrences).Select(o => o.Id).Where(id => !listing.ContainsKey(id) && !reviewed.Contains(id)).ToList();
        }
        var marks = Tables(s, data).SelectMany(t => t.Data.Entries.Where(e => e.Mark is { Length: > 0 } and not Schedules.NoLevel).GroupBy(e => e.Mark)
            .Where(g => g.Count() > 1).Select(g => (t.Title, g.Key, g.Count()))).ToList();
        return (twice, none, marks);
    }

    static double Scale(Entity s) => s.NumOrNull("scale") ?? 50;

    static readonly Dictionary<string, string> RowLabels = new()
    {
        ["name"] = "Name", ["size"] = "Size", ["operation"] = "Opening", ["sill"] = "Sill height", ["levels"] = "Count by level", ["total"] = "Total",
        ["uValue"] = "U value", ["fire"] = "Fire rating", ["lintel"] = "Lintel", ["lintelKind"] = "Kind", ["product"] = "Product", ["section"] = "Section",
        ["length"] = "Length", ["openings"] = "Openings", ["concrete"] = "Concrete", ["steel"] = "Steel",
    };

    /// <summary>Every row a schedule can show: the built-in ones of its kind, then its own.</summary>
    static List<(string Id, string Label)> Available(Entity s, ScheduleData data)
    {
        var t = ScheduleTable.Of(s);
        var lintel = s.Str("kind") == "lintel";
        var builtIn = lintel ? ScheduleLayout.LintelRows : ScheduleLayout.JoineryRows;
        var ps = lintel ? [] : ScheduleLayout.ParamRows(data);
        return [.. builtIn.Select(id => (id, RowLabels.GetValueOrDefault(id, id))), .. ps.Select(id => (id, id[ScheduleLayout.ParamPrefix.Length..])), .. t.CustomRows.Select(c => ("custom:" + c.Id, c.Label))];
    }

    /// <summary>What a type's card shows: the schedule's drawings (interior by default).</summary>
    static IReadOnlyList<string> Drawings(Entity s) => s.Attrs["drawings"] is JsonArray a ? a.Select(x => (string?)x).OfType<string>().ToList() : ["interior"];

    /// <summary>Whether a type assemblies schedule draws each type's build-up after its table (its <c>drawings</c> hold <see cref="TypeBuildUps.Drawing"/>).</summary>
    public static bool DrawsBuildUps(Entity s) => s.Attrs["drawings"] is JsonArray a && a.Any(x => (string?)x == TypeBuildUps.Drawing);

    /// <summary>The module that fills a schedule of this kind (a planting schedule), if the kind is not one of the joinery's.</summary>
    static IScheduleSource? Source(IServices services, string? kind) => kind == null || Kinds.Contains(kind) ? null : services.All<IScheduleSource>().FirstOrDefault(x => x.Kind == kind);

    public IReadOnlyList<(double W, double H, List<Pen> Pens)> Pages(IQueryContext q, Entity e, double maxWidth, double? maxHeight)
    {
        if (Source(q.Services, e.Str("kind")) is { } source)
        {
            // A type assemblies schedule's build-ups (reports #65–#67) after its table, at its scale, when it draws them.
            var buildUps = source is TypeAssemblies && DrawsBuildUps(e) ? TypeBuildUps.Bands(q.Model, Scale(e), maxWidth) : [];
            return ScheduleLayout.Pages([(source.Label, ScheduleLayout.Table(SourceGrid(q, e, source).ToGrid(), maxWidth)),
                .. buildUps.Count > 0 ? [("Build-ups", buildUps)] : new List<(string, List<ScheduleBand>)>(), .. Charts(q, e, maxWidth)], maxHeight);
        }
        var data = Data(q, e);
        var table = ScheduleTable.Of(e);
        var drawings = Drawings(e);
        var (convention, hardware) = Joinery.SymbolSettings(q.Model);
        var tables = Tables(e, data).Select(t => (t.Title, ScheduleLayout.Bands(t.Data, table, Scale(e), maxWidth, _ => drawings, convention, hardware))).ToList();
        return ScheduleLayout.Pages([.. tables, .. Charts(q, e, maxWidth)], maxHeight);
    }

    static string Csv(IEnumerable<IEnumerable<string>> lines) =>
        string.Concat(lines.Select(l => string.Join(",", l.Select(f => System.Text.RegularExpressions.Regex.IsMatch(f, "[\",\n;]") ? $"\"{f.Replace("\"", "\"\"")}\"" : f)) + "\n"));

    /// <summary>
    /// The schedule as CSV. Joinery: a fixed set of columns (mark, name, size,
    /// opening, sill, a column per level, total, U, fire, the custom rows), typed
    /// text over the model's. Lintels: the mark and each shown row.
    /// </summary>
    public static string ToCsv(Entity s, ScheduleData data) => Csv(Grid(s, data));

    /// <summary>A schedule as a workbook: one sheet a table, as the CSV lays it out.</summary>
    public static byte[] ToXlsx(Entity s, ScheduleData data) =>
        Lapin.Drawing.Export.XlsxWriter.Write(Tables(s, data).Select(t => (t.Title, (IReadOnlyList<IReadOnlyList<string>>)Grid(s, t.Data).Select(r => (IReadOnlyList<string>)r.ToList()).ToList())).ToList());

    /// <summary>The schedule as a grid: a header row, then a row an entry.</summary>
    static List<IEnumerable<string>> Grid(Entity s, ScheduleData data)
    {
        var table = ScheduleTable.Of(s);
        var entries = ScheduleLayout.Sort(data.Entries, table.Sort);
        string Typed(ScheduleEntry e, string row, string model) => table.Cells.GetValueOrDefault(e.Key)?.GetValueOrDefault(row) ?? model;
        if (data.Entries.Count > 0 && data.Entries.All(e => e.Kind == "lintel"))
        {
            var rows = ScheduleLayout.Rows(data, table);
            return [["Mark", .. rows.Select(r => r.Label)], .. entries.Select(e => (IEnumerable<string>)[e.Mark, .. rows.Select(r => r.Value(e))])];
        }
        string Mm(double m) => Math.Round(m * 1000).ToString(CultureInfo.InvariantCulture);
        var head = new List<string> { "Mark", "Name", "Width (mm)", "Height (mm)", "Opening", "Sill" };
        head.AddRange(data.Levels);
        var ps = ScheduleLayout.ParamRows(data);
        head.AddRange(["Total", "U", "Fire", .. ps.Select(p => p[ScheduleLayout.ParamPrefix.Length..]), .. table.CustomRows.Select(c => c.Label)]);
        var opening = ScheduleLayout.Rows(data, table with { Rows = ["operation"] }).FirstOrDefault();
        var lines = new List<IEnumerable<string>> { head };
        foreach (var e in entries)
        {
            var manual = e.Kind == ScheduleEntry.Manual;
            var line = new List<string>
            {
                e.Mark, Typed(e, "name", e.Name), manual ? Typed(e, "size", "") : Mm(e.Width), manual ? "" : Mm(e.Height),
                opening?.Value(e) ?? "", Typed(e, "sill", e.Kind == "window" && e.Spec != null ? Mm(e.Spec.SillHeight) : ""),
            };
            line.AddRange(data.Levels.Select(l => Typed(e, "level:" + l, manual ? "" : e.Counts.GetValueOrDefault(l).ToString(CultureInfo.InvariantCulture))));
            line.AddRange([Typed(e, "total", manual ? "" : e.Total.ToString(CultureInfo.InvariantCulture)), Typed(e, "uValue", e.Spec?.ThermalTransmittance?.ToString(CultureInfo.InvariantCulture) ?? ""), Typed(e, "fire", ScheduleLayout.FireRating(e) ?? "")]);
            line.AddRange(ps.Select(p => Typed(e, p, e.Params?.GetValueOrDefault(p[ScheduleLayout.ParamPrefix.Length..]) ?? "")));
            line.AddRange(table.CustomRows.Select(c => table.Cells.GetValueOrDefault(e.Key)?.GetValueOrDefault("custom:" + c.Id) ?? ""));
            lines.Add(line);
        }
        return lines;
    }

    // ------------------------------------------------------------ commands

    static Entity Require(IModelView m, string? id) => m.Get(id ?? "") is { Class: Class } s ? s : throw new CommandException("There is no such schedule.");

    /// <summary>A column of the schedule's own by its key: where it is kept, its name's field, its id.</summary>
    static (string Key, string Field, string Id) Column(string? column) => column switch
    {
        { } c when c.StartsWith("manual:", StringComparison.Ordinal) => ("manual", "mark", c[7..]),
        { } c when c.StartsWith("custom:", StringComparison.Ordinal) => ("customColumns", "label", c[7..]),
        _ => throw new CommandException("Which column of the schedule's own?"),
    };

    static JsonArray ChartsJson(Entity s) => new([.. ScheduleChart.Of(s).Select(c => (JsonNode)new JsonObject
    {
        ["id"] = c.Id, ["kind"] = c.Kind, ["title"] = c.Title, ["heading"] = c.Heading, ["category"] = c.Category, ["value"] = c.Value, ["aggregate"] = c.Aggregate, ["top"] = c.Top,
    })]);

    static List<string> DefaultRows(Entity s, ScheduleTable t) =>
        [.. (s.Str("kind") == "lintel" ? ScheduleLayout.LintelRows : ScheduleLayout.JoineryRows), .. t.CustomRows.Select(c => "custom:" + c.Id)];

    public void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Schedule",
        [
            new("kind", "Lists", "choice", Choices: Kinds, ReadOnly: true),
            new("scale", "Drawings 1:", "number", Min: 1, Max: 500),
            new("sort", "Order", "choice", Choices: ["mark", "name", "width", "height", "total"]),
        ], "#555555", Kind: "view"));
        r.Service<ISheetContent>(this);
        r.Service<IScheduleSource>(new QuantityTakeoff());

        r.Command("createSchedule", "New schedule", (ctx, a) =>
        {
            var kind = (string?)a["kind"] ?? "all";
            var source = Source(ctx.Services, kind);
            if (!Kinds.Contains(kind) && source == null) throw new CommandException("A schedule lists doors, windows, all of them, lintels, or a table another module offers.");
            var name = (string?)a["name"] ?? source?.Label ?? kind switch { "door" => "Door schedule", "window" => "Window schedule", "lintel" => "Lintel schedule", _ => "Joinery schedule" };
            var attrs = new JsonObject { ["kind"] = kind, ["scale"] = 50 };
            // A new type assemblies schedule draws the build-ups (reports #65–#67); one made before keeps its plain table until asked.
            if (source is TypeAssemblies) attrs["drawings"] = new JsonArray((JsonNode)TypeBuildUps.Drawing);
            var s = new Entity(ctx.NewId(), Class, name, attrs);
            ctx.Add(s);
            return new JsonObject { ["id"] = s.Id };
        });

        // Its rows in order (null: back to the defaults), card order, drawings, tables, reviewed elements.
        r.Command("updateSchedule", "Change schedule", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var attrs = s.CloneAttrs();
            foreach (var key in new[] { "rows", "sort", "drawings", "parts", "reviewed", "unused", "sortBy", "sortDesc", "groupBy", "classes" })
            {
                if (!a.ContainsKey(key)) continue;
                if (a[key] is null) attrs.Remove(key); else attrs[key] = a[key]!.DeepClone();
            }
            if (Json.Number(a["scale"]) is { } sc) attrs["scale"] = sc > 0 ? sc : throw new CommandException("A scale is positive.");
            ctx.Replace(s.With(name: ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : null, attrs: attrs));
            return null;
        });

        // A typed cell: a custom row's value, or a model value overridden on purpose (null: back to the model; "" on a model row too).
        r.Command("scheduleCell", "Schedule cell", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var (entry, row) = ((string?)a["entry"] ?? throw new CommandException("Which entry?"), (string?)a["row"] ?? throw new CommandException("Which row?"));
            // A module's table whose column is the model's: the value goes into what the row lists ("r:<key>#<n>", "c:<column>"), not onto the schedule.
            if (Source(ctx.Services, s.Str("kind")) is { } source && entry.StartsWith("r:", StringComparison.Ordinal) && entry.LastIndexOf('#') is var hash and > 2
                && row.StartsWith("c:", StringComparison.Ordinal) && source.Write(ctx, entry[2..hash], row[2..], (string?)a["text"]))
                return null;
            var attrs = s.CloneAttrs();
            var cells = attrs["cells"] as JsonObject ?? new JsonObject();
            var line = cells[entry] as JsonObject ?? new JsonObject();
            var text = (string?)a["text"];
            if (text == null || (text.Length == 0 && !row.StartsWith("custom:", StringComparison.Ordinal))) line.Remove(row); else line[row] = text;
            if (line.Count > 0) cells[entry] = line; else cells.Remove(entry);
            if (cells.Count > 0) attrs["cells"] = cells; else attrs.Remove("cells");
            ctx.Replace(s.With(attrs: attrs));
            return null;
        });

        r.Command("addScheduleRow", "Schedule row", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var label = ((string?)a["label"])?.Trim();
            if (string.IsNullOrEmpty(label)) throw new CommandException("A row needs a label.");
            var t = ScheduleTable.Of(s);
            var id = ctx.NewId()[..8];
            var attrs = s.CloneAttrs();
            var custom = attrs["customRows"] as JsonArray ?? new JsonArray();
            custom.Add((JsonNode)new JsonObject { ["id"] = id, ["label"] = label });
            attrs["customRows"] = custom;
            attrs["rows"] = new JsonArray([.. (t.Rows ?? DefaultRows(s, t)).Append("custom:" + id).Select(x => (JsonNode)x)]);
            ctx.Replace(s.With(attrs: attrs));
            return new JsonObject { ["rowId"] = "custom:" + id };
        });

        r.Command("removeScheduleRow", "Remove schedule row", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var rowId = (string?)a["rowId"] ?? "";
            var id = rowId.StartsWith("custom:", StringComparison.Ordinal) ? rowId[7..] : rowId;
            var attrs = s.CloneAttrs();
            attrs["customRows"] = new JsonArray([.. (attrs["customRows"] as JsonArray ?? []).Where(x => (string?)x?["id"] != id).Select(x => x?.DeepClone())]);
            if (attrs["rows"] is JsonArray rows) attrs["rows"] = new JsonArray([.. rows.Where(x => (string?)x != "custom:" + id).Select(x => x?.DeepClone())]);
            if (attrs["cells"] is JsonObject cells)
            {
                foreach (var (_, line) in cells.ToList()) (line as JsonObject)?.Remove("custom:" + id);
                foreach (var key in cells.Where(c => c.Value is JsonObject { Count: 0 }).Select(c => c.Key).ToList()) cells.Remove(key);
                if (cells.Count == 0) attrs.Remove("cells");
            }
            ctx.Replace(s.With(attrs: attrs));
            return null;
        });

        // A column of the schedule's own (decision 251): a card with no type in a joinery schedule, a column after the module's in another.
        r.Command("addScheduleColumn", "Schedule column", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var label = ((string?)a["label"])?.Trim();
            if (string.IsNullOrEmpty(label)) throw new CommandException("A column needs a name.");
            var id = ctx.NewId()[..8];
            var attrs = s.CloneAttrs();
            var key = Kinds.Contains(s.Str("kind") ?? "all") ? "manual" : "customColumns";
            if (key == "manual" && s.Str("kind") == "lintel") throw new CommandException("A lintel schedule lists the lintels the walls make: it takes rows of its own, not columns.");
            var list = attrs[key] as JsonArray ?? new JsonArray();
            list.Add((JsonNode)(key == "manual" ? new JsonObject { ["id"] = id, ["mark"] = label } : new JsonObject { ["id"] = id, ["label"] = label }));
            attrs[key] = list;
            ctx.Replace(s.With(attrs: attrs));
            return new JsonObject { ["column"] = (key == "manual" ? "manual:" : "custom:") + id };
        });

        r.Command("renameScheduleColumn", "Rename schedule column", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var label = ((string?)a["label"])?.Trim();
            if (string.IsNullOrEmpty(label)) throw new CommandException("A column needs a name.");
            var (key, field, id) = Column((string?)a["column"]);
            var attrs = s.CloneAttrs();
            var item = (attrs[key] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(o => (string?)o["id"] == id) ?? throw new CommandException("The schedule has no such column of its own.");
            item[field] = label;
            ctx.Replace(s.With(attrs: attrs));
            return null;
        });

        r.Command("removeScheduleColumn", "Remove schedule column", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var column = (string?)a["column"];
            var (key, _, id) = Column(column);
            var attrs = s.CloneAttrs();
            if (attrs[key] is not JsonArray list || list.OfType<JsonObject>().All(o => (string?)o["id"] != id)) throw new CommandException("The schedule has no such column of its own.");
            var kept = new JsonArray([.. list.Where(x => (string?)x?["id"] != id).Select(x => x?.DeepClone())]);
            if (kept.Count > 0) attrs[key] = kept; else attrs.Remove(key);
            if (attrs["cells"] is JsonObject cells)
            {
                // A card's typed values go with it; a column's from every row.
                if (key == "manual") cells.Remove(column!);
                else foreach (var (_, line) in cells.ToList()) (line as JsonObject)?.Remove(column!);
                foreach (var k in cells.Where(c => c.Value is JsonObject { Count: 0 }).Select(c => c.Key).ToList()) cells.Remove(k);
                if (cells.Count == 0) attrs.Remove("cells");
            }
            if (key == "customColumns" && s.Str("sortBy") == column) { attrs.Remove("sortBy"); attrs.Remove("sortDesc"); }
            ctx.Replace(s.With(attrs: attrs));
            return null;
        });

        // A row of the schedule's own in a module's table (a joinery schedule's own rows are addScheduleRow's).
        r.Command("addScheduleManualRow", "Schedule row", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            if (Kinds.Contains(s.Str("kind") ?? "all")) throw new CommandException("A joinery or lintel schedule takes rows of its own by their label (addScheduleRow).");
            var id = ctx.NewId()[..8];
            var attrs = s.CloneAttrs();
            var list = attrs["manualRows"] as JsonArray ?? new JsonArray();
            list.Add((JsonNode)id);
            attrs["manualRows"] = list;
            ctx.Replace(s.With(attrs: attrs));
            return new JsonObject { ["row"] = ScheduleGrids.ManualRow + id };
        });

        r.Command("removeScheduleManualRow", "Remove schedule row", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var row = (string?)a["row"] ?? "";
            var id = row.StartsWith(ScheduleGrids.ManualRow, StringComparison.Ordinal) ? row[ScheduleGrids.ManualRow.Length..] : row;
            var attrs = s.CloneAttrs();
            if (attrs["manualRows"] is not JsonArray list || list.All(x => (string?)x != id)) throw new CommandException("The schedule has no such row of its own.");
            var kept = new JsonArray([.. list.Where(x => (string?)x != id).Select(x => x?.DeepClone())]);
            if (kept.Count > 0) attrs["manualRows"] = kept; else attrs.Remove("manualRows");
            if (attrs["cells"] is JsonObject cells) { cells.Remove(ScheduleGrids.ManualRow + id); if (cells.Count == 0) attrs.Remove("cells"); }
            ctx.Replace(s.With(attrs: attrs));
            return null;
        });

        // A chart of the schedule, added or changed (by its id), drawn after its tables: on screen, on sheets, in the PDF.
        r.Command("scheduleChart", "Schedule chart", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var o = a["chart"] as JsonObject ?? throw new CommandException("Which chart?");
            var kind = (string?)o["kind"] ?? "column";
            if (!ScheduleChart.Kinds.Contains(kind)) throw new CommandException($"A chart is one of: {string.Join(", ", ScheduleChart.Kinds)}.");
            var aggregate = (string?)o["aggregate"] ?? "sum";
            if (!ScheduleChart.Aggregates.Contains(aggregate)) throw new CommandException($"A chart adds up by one of: {string.Join(", ", ScheduleChart.Aggregates)}.");
            var category = ((string?)o["category"])?.Trim() is { Length: > 0 } c ? c : throw new CommandException("A chart needs the column its categories come from.");
            var value = ((string?)o["value"])?.Trim() is { Length: > 0 } v ? v : null;
            if (value == null && aggregate != "count") throw new CommandException("A chart needs the column it adds up, or counts the rows.");
            if (Json.Number(o["top"]) is { } top && (top < 2 || top > 50)) throw new CommandException("A chart keeps between 2 and 50 categories.");
            var id = (string?)o["id"] is { Length: > 0 } cid ? cid : ctx.NewId()[..8];
            var chart = new JsonObject { ["id"] = id, ["kind"] = kind, ["category"] = category, ["aggregate"] = aggregate };
            if (value != null) chart["value"] = value;
            if (((string?)o["title"])?.Trim() is { Length: > 0 } title) chart["title"] = title;
            if (Json.Number(o["top"]) is { } t) chart["top"] = (int)t;
            // It reads columns the table has.
            var (columns, rows) = ChartGrid(ctx.Query, s);
            ScheduleCharts.Values(ScheduleChart.From(chart)!, columns, rows);
            var attrs = s.CloneAttrs();
            var list = (attrs["charts"] as JsonArray)?.Select(x => x?.DeepClone()).ToList() ?? [];
            var at = list.FindIndex(x => (string?)x?["id"] == id);
            if (at >= 0) list[at] = chart; else list.Add(chart);
            attrs["charts"] = new JsonArray([.. list]);
            ctx.Replace(s.With(attrs: attrs));
            return new JsonObject { ["chartId"] = id };
        });

        r.Command("removeScheduleChart", "Remove schedule chart", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var id = (string?)a["chartId"];
            var attrs = s.CloneAttrs();
            if (attrs["charts"] is not JsonArray list || list.All(x => (string?)x?["id"] != id)) throw new CommandException("The schedule has no such chart.");
            var kept = new JsonArray([.. list.Where(x => (string?)x?["id"] != id).Select(x => x?.DeepClone())]);
            if (kept.Count > 0) attrs["charts"] = kept; else attrs.Remove("charts");
            ctx.Replace(s.With(attrs: attrs));
            return null;
        });

        r.Query("schedule.layout", (q, a) =>
        {
            var s = Require(q.Model, (string?)a["id"]);
            if (Source(q.Services, s.Str("kind")) is { } source)
            {
                // A module's table: its pens, and the grid itself; none of the joinery's rows and cards.
                var grid = SourceGrid(q, s, source);
                var (gw, gh, gpens) = Pages(q, s, Json.Number(a["maxWidth"]) ?? 380, null)[0];
                var plain = grid.ToGrid();
                return new JsonObject
                {
                    ["w"] = gw, ["h"] = gh, ["pens"] = new JsonArray(gpens.Select(p => (JsonNode)p.ToJson()).ToArray()),
                    ["entries"] = new JsonArray(), ["rows"] = new JsonArray(), ["problems"] = new JsonObject { ["twice"] = 0, ["none"] = 0, ["marks"] = new JsonArray() },
                    ["available"] = new JsonArray(), ["order"] = new JsonArray(), ["drawings"] = s.Attrs["drawings"]?.DeepClone() ?? new JsonArray(), ["parts"] = new JsonArray(), ["reviewed"] = new JsonArray(), ["check"] = new JsonArray(), ["overridden"] = new JsonObject(),
                    ["table"] = new JsonObject { ["columns"] = new JsonArray(plain.Columns.Select(c => (JsonNode)c).ToArray()), ["rows"] = new JsonArray(plain.Rows.Select(r => (JsonNode)new JsonArray(r.Select(c => (JsonNode)c).ToArray())).ToArray()) },
                    ["grid"] = ScheduleGrids.ToJson(grid),
                    ["charts"] = ChartsJson(s), ["chartColumns"] = new JsonArray(plain.Columns.Select(c => (JsonNode)c).ToArray()),
                };
            }
            var data = Data(q, s);
            var pages = Pages(q, s, Json.Number(a["maxWidth"]) ?? 380, null);
            var (twice, none, marks) = Check(s, data);
            var (w, h, pens) = pages[0];
            return new JsonObject
            {
                ["w"] = w, ["h"] = h, ["pens"] = new JsonArray(pens.Select(p => (JsonNode)p.ToJson()).ToArray()),
                ["entries"] = new JsonArray(ScheduleLayout.Sort(data.Entries, ScheduleTable.Of(s).Sort).Select(e => (JsonNode)new JsonObject
                {
                    ["key"] = e.Key, ["mark"] = e.Mark, ["name"] = e.Name, ["total"] = e.Total, ["manual"] = e.Kind == ScheduleEntry.Manual,
                    ["rows"] = new JsonObject(ScheduleLayout.Rows(data, ScheduleTable.Of(s)).Select(r => KeyValuePair.Create(r.Id, (JsonNode?)r.Value(e)))),
                }).ToArray()),
                ["rows"] = new JsonArray(ScheduleLayout.Rows(data, ScheduleTable.Of(s)).Select(r => (JsonNode)new JsonObject { ["id"] = r.Id, ["label"] = r.Label, ["fromModel"] = r.FromModel, ["param"] = r.Param }).ToArray()),
                ["unused"] = ShowsUnused(s), ["unusedCount"] = s.Str("kind") == "lintel" ? 0 : Schedules.Of(q, s.Str("kind") ?? "all").Entries.Count(e => e.Total == 0),
                ["charts"] = ChartsJson(s), ["chartColumns"] = new JsonArray(ChartGrid(q, s).Columns.Select(c => (JsonNode)c).ToArray()),
                ["problems"] = new JsonObject
                {
                    ["twice"] = twice.Count, ["none"] = none.Count,
                    ["marks"] = new JsonArray(marks.Select(m => (JsonNode)$"{m.Part}: {m.Mark} × {m.Count}").ToArray()),
                },
                // What the settings choose from and what is set: the rows (built in, then the schedule's own), their order, the card's drawings, the tables.
                ["available"] = new JsonArray(Available(s, data).Select(r => (JsonNode)new JsonObject { ["id"] = r.Id, ["label"] = r.Label }).ToArray()),
                ["order"] = new JsonArray((ScheduleTable.Of(s).Rows ?? Available(s, data).Select(r => r.Id).ToList()).Select(x => (JsonNode)x).ToArray()),
                ["drawings"] = new JsonArray(Drawings(s).Select(x => (JsonNode)x).ToArray()),
                ["parts"] = s.Attrs["parts"]?.DeepClone() ?? new JsonArray(),
                ["reviewed"] = s.Attrs["reviewed"]?.DeepClone() ?? new JsonArray(),
                // The elements to check, by name: listed twice (and where), listed nowhere.
                ["check"] = new JsonArray([
                    .. twice.Select(t => (JsonNode)new JsonObject { ["id"] = t.Key, ["name"] = q.Model.Get(t.Key)?.Name, ["why"] = $"in {string.Join(" and ", t.Value.Select(pid => Parts(s).FirstOrDefault(x => x.Id == pid)?.Title ?? pid))}" }),
                    .. none.Select(id => (JsonNode)new JsonObject { ["id"] = id, ["name"] = q.Model.Get(id)?.Name, ["why"] = "in no table" }),
                ]),
                // Model values typed over on purpose: entry → rows.
                ["overridden"] = new JsonObject(ScheduleTable.Of(s).Cells.Select(c => KeyValuePair.Create(c.Key, (JsonNode?)new JsonArray(c.Value.Keys.Where(k => !k.StartsWith("custom:", StringComparison.Ordinal)).Select(k => (JsonNode)k).ToArray())))),
            };
        });

        // The workbook, as base64 (the interface saves it).
        r.Query("schedule.xlsx", (q, a) =>
        {
            var s = Require(q.Model, (string?)a["id"]);
            if (Source(q.Services, s.Str("kind")) is { } source)
            {
                var g = SourceGrid(q, s, source).ToGrid();
                return Convert.ToBase64String(Lapin.Drawing.Export.XlsxWriter.Write([(source.Label, (IReadOnlyList<IReadOnlyList<string>>)[g.Columns, .. g.Rows])]));
            }
            return Convert.ToBase64String(ToXlsx(s, Data(q, s)));
        });

        // Every kind of schedule this product makes, with its name: the joinery's, then the modules' tables (the Quantities tab, the browser's list).
        r.Query("schedule.kinds", (q, _) => new JsonArray([
            .. new[] { ("all", "Joinery schedule"), ("door", "Door schedule"), ("window", "Window schedule"), ("lintel", "Lintel schedule") }
                .Select(k => (JsonNode)new JsonObject { ["kind"] = k.Item1, ["label"] = k.Item2 }),
            .. q.Services.All<IScheduleSource>().Select(x => (JsonNode)new JsonObject { ["kind"] = x.Kind, ["label"] = x.Label })]));

        // What a quantity takeoff can list: the classes the model measures, with their names and counts.
        r.Query("schedule.takeoffClasses", (q, _) => new JsonArray([.. QuantityTakeoff.Classes(q).Select(c => (JsonNode)new JsonObject { ["class"] = c.Class, ["label"] = c.Label, ["count"] = c.Count })]));

        r.Query("schedule.csv", (q, a) =>
        {
            var s = Require(q.Model, (string?)a["id"]);
            if (Source(q.Services, s.Str("kind")) is { } source) { var g = SourceGrid(q, s, source).ToGrid(); return Csv([g.Columns, .. g.Rows]); }
            return ToCsv(s, Data(q, s));
        });
    }
}
