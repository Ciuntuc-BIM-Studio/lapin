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
    static List<(string Id, string Label)> Available(Entity s)
    {
        var t = ScheduleTable.Of(s);
        var builtIn = s.Str("kind") == "lintel" ? ScheduleLayout.LintelRows : ScheduleLayout.JoineryRows;
        return [.. builtIn.Select(id => (id, RowLabels.GetValueOrDefault(id, id))), .. t.CustomRows.Select(c => ("custom:" + c.Id, c.Label))];
    }

    /// <summary>What a type's card shows: the schedule's drawings (interior by default).</summary>
    static IReadOnlyList<string> Drawings(Entity s) => s.Attrs["drawings"] is JsonArray a ? a.Select(x => (string?)x).OfType<string>().ToList() : ["interior"];

    /// <summary>The module that fills a schedule of this kind (a planting schedule), if the kind is not one of the joinery's.</summary>
    static IScheduleSource? Source(IServices services, string? kind) => kind == null || Kinds.Contains(kind) ? null : services.All<IScheduleSource>().FirstOrDefault(x => x.Kind == kind);

    public IReadOnlyList<(double W, double H, List<Pen> Pens)> Pages(IQueryContext q, Entity e, double maxWidth, double? maxHeight)
    {
        if (Source(q.Services, e.Str("kind")) is { } source) return ScheduleLayout.Pages([(source.Label, ScheduleLayout.Table(source.Grid(q), maxWidth))], maxHeight);
        var data = Schedules.Of(q, e.Str("kind") ?? "all");
        var table = ScheduleTable.Of(e);
        var drawings = Drawings(e);
        var (convention, hardware) = Joinery.SymbolSettings(q.Model);
        var tables = Tables(e, data).Select(t => (t.Title, ScheduleLayout.Bands(t.Data, table, Scale(e), maxWidth, _ => drawings, convention, hardware))).ToList();
        return ScheduleLayout.Pages(tables, maxHeight);
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
        head.AddRange(["Total", "U", "Fire", .. table.CustomRows.Select(c => c.Label)]);
        var opening = ScheduleLayout.Rows(data, table with { Rows = ["operation"] }).FirstOrDefault();
        var lines = new List<IEnumerable<string>> { head };
        foreach (var e in entries)
        {
            var line = new List<string>
            {
                e.Mark, Typed(e, "name", e.Name), Mm(e.Width), Mm(e.Height),
                opening?.Value(e) ?? "", Typed(e, "sill", e.Kind == "window" && e.Spec != null ? Mm(e.Spec.SillHeight) : ""),
            };
            line.AddRange(data.Levels.Select(l => Typed(e, "level:" + l, e.Counts.GetValueOrDefault(l).ToString(CultureInfo.InvariantCulture))));
            line.AddRange([Typed(e, "total", e.Total.ToString(CultureInfo.InvariantCulture)), Typed(e, "uValue", e.Spec?.ThermalTransmittance?.ToString(CultureInfo.InvariantCulture) ?? ""), Typed(e, "fire", "")]);
            line.AddRange(table.CustomRows.Select(c => table.Cells.GetValueOrDefault(e.Key)?.GetValueOrDefault("custom:" + c.Id) ?? ""));
            lines.Add(line);
        }
        return lines;
    }

    // ------------------------------------------------------------ commands

    static Entity Require(IModelView m, string? id) => m.Get(id ?? "") is { Class: Class } s ? s : throw new CommandException("There is no such schedule.");

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

        r.Command("createSchedule", "New schedule", (ctx, a) =>
        {
            var kind = (string?)a["kind"] ?? "all";
            var source = Source(ctx.Services, kind);
            if (!Kinds.Contains(kind) && source == null) throw new CommandException("A schedule lists doors, windows, all of them, lintels, or a table another module offers.");
            var name = (string?)a["name"] ?? source?.Label ?? kind switch { "door" => "Door schedule", "window" => "Window schedule", "lintel" => "Lintel schedule", _ => "Joinery schedule" };
            var s = new Entity(ctx.NewId(), Class, name, new JsonObject { ["kind"] = kind, ["scale"] = 50 });
            ctx.Add(s);
            return new JsonObject { ["id"] = s.Id };
        });

        // Its rows in order (null: back to the defaults), card order, drawings, tables, reviewed elements.
        r.Command("updateSchedule", "Change schedule", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var attrs = s.CloneAttrs();
            foreach (var key in new[] { "rows", "sort", "drawings", "parts", "reviewed" })
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

        r.Query("schedule.layout", (q, a) =>
        {
            var s = Require(q.Model, (string?)a["id"]);
            if (Source(q.Services, s.Str("kind")) is { } source)
            {
                // A module's table: its pens, and the grid itself; none of the joinery's rows and cards.
                var grid = source.Grid(q);
                var (gw, gh, gpens) = Pages(q, s, Json.Number(a["maxWidth"]) ?? 380, null)[0];
                return new JsonObject
                {
                    ["w"] = gw, ["h"] = gh, ["pens"] = new JsonArray(gpens.Select(p => (JsonNode)p.ToJson()).ToArray()),
                    ["entries"] = new JsonArray(), ["rows"] = new JsonArray(), ["problems"] = new JsonObject { ["twice"] = 0, ["none"] = 0, ["marks"] = new JsonArray() },
                    ["available"] = new JsonArray(), ["order"] = new JsonArray(), ["drawings"] = new JsonArray(), ["parts"] = new JsonArray(), ["reviewed"] = new JsonArray(), ["check"] = new JsonArray(), ["overridden"] = new JsonObject(),
                    ["table"] = new JsonObject { ["columns"] = new JsonArray(grid.Columns.Select(c => (JsonNode)c).ToArray()), ["rows"] = new JsonArray(grid.Rows.Select(r => (JsonNode)new JsonArray(r.Select(c => (JsonNode)c).ToArray())).ToArray()) },
                };
            }
            var data = Schedules.Of(q, s.Str("kind") ?? "all");
            var pages = Pages(q, s, Json.Number(a["maxWidth"]) ?? 380, null);
            var (twice, none, marks) = Check(s, data);
            var (w, h, pens) = pages[0];
            return new JsonObject
            {
                ["w"] = w, ["h"] = h, ["pens"] = new JsonArray(pens.Select(p => (JsonNode)p.ToJson()).ToArray()),
                ["entries"] = new JsonArray(ScheduleLayout.Sort(data.Entries, ScheduleTable.Of(s).Sort).Select(e => (JsonNode)new JsonObject
                {
                    ["key"] = e.Key, ["mark"] = e.Mark, ["name"] = e.Name, ["total"] = e.Total,
                    ["rows"] = new JsonObject(ScheduleLayout.Rows(data, ScheduleTable.Of(s)).Select(r => KeyValuePair.Create(r.Id, (JsonNode?)r.Value(e)))),
                }).ToArray()),
                ["rows"] = new JsonArray(ScheduleLayout.Rows(data, ScheduleTable.Of(s)).Select(r => (JsonNode)new JsonObject { ["id"] = r.Id, ["label"] = r.Label, ["fromModel"] = r.FromModel }).ToArray()),
                ["problems"] = new JsonObject
                {
                    ["twice"] = twice.Count, ["none"] = none.Count,
                    ["marks"] = new JsonArray(marks.Select(m => (JsonNode)$"{m.Part}: {m.Mark} × {m.Count}").ToArray()),
                },
                // What the settings choose from and what is set: the rows (built in, then the schedule's own), their order, the card's drawings, the tables.
                ["available"] = new JsonArray(Available(s).Select(r => (JsonNode)new JsonObject { ["id"] = r.Id, ["label"] = r.Label }).ToArray()),
                ["order"] = new JsonArray((ScheduleTable.Of(s).Rows ?? Available(s).Select(r => r.Id).ToList()).Select(x => (JsonNode)x).ToArray()),
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
                var g = source.Grid(q);
                return Convert.ToBase64String(Lapin.Drawing.Export.XlsxWriter.Write([(source.Label, (IReadOnlyList<IReadOnlyList<string>>)[g.Columns, .. g.Rows])]));
            }
            return Convert.ToBase64String(ToXlsx(s, Schedules.Of(q, s.Str("kind") ?? "all")));
        });

        r.Query("schedule.csv", (q, a) =>
        {
            var s = Require(q.Model, (string?)a["id"]);
            if (Source(q.Services, s.Str("kind")) is { } source) { var g = source.Grid(q); return Csv([g.Columns, .. g.Rows]); }
            return ToCsv(s, Schedules.Of(q, s.Str("kind") ?? "all"));
        });
    }
}
