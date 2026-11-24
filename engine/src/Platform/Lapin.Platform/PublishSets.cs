using System.IO.Compression;
using System.Text.Json.Nodes;
using Lapin.Drawing.Export;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Publish sets (forum topic 225, as Revit's print and export sets): a named choice of sheets and views kept on the
/// project (<c>publishSets</c>), with the files it makes, so the same drawings for a phase (feasibility, building permit,
/// erection plans) are exported again in one step. A set holds the sheets and views in the order chosen and the formats:
/// each one as its own PDF, all of them in one PDF, each one as a DXF. A sheet or view deleted since drops out of the
/// set as it is read (it is counted as missing, never an error). Exporting makes no issue and records nothing: an issue
/// (Manage → Issue…) is the CDE's, with its revision, checks and transmittal, and can start from a set's sheets.
/// </summary>
public static class PublishSets
{
    public const string Key = "publishSets";

    public sealed record Formats(bool Pdf, bool Combined, bool Dxf)
    {
        public static readonly Formats Default = new(true, false, false);
        public bool Any => Pdf || Combined || Dxf;

        public static Formats Of(JsonNode? n, Formats? fallback = null)
        {
            var d = fallback ?? Default;
            if (n is not JsonObject o) return d;
            return new(Flag(o, "pdf") ?? d.Pdf, Flag(o, "combined") ?? d.Combined, Flag(o, "dxf") ?? d.Dxf);
        }

        public JsonObject ToJson() => new() { ["pdf"] = Pdf, ["combined"] = Combined, ["dxf"] = Dxf };
    }

    public sealed record Set(string Id, string Name, List<string> Sheets, List<string> Views, Formats Formats);

    /// <summary>The sets as kept, in order, each with every id it names (those gone since included).</summary>
    public static List<Set> All(IModelView m) =>
        (Spatial.Project(m)?.Attrs[Key] as JsonArray ?? []).OfType<JsonObject>().Select(Read).OfType<Set>().ToList();

    static Set? Read(JsonObject o) =>
        (string?)o["id"] is { Length: > 0 } id && (string?)o["name"] is { } name
            ? new Set(id, name, Ids(o["sheets"]), Ids(o["views"]), Formats.Of(o["formats"]))
            : null;

    static List<string> Ids(JsonNode? n) => n is JsonArray a ? a.Select(x => (string?)x).OfType<string>().Where(x => x.Length > 0).Distinct().ToList() : [];

    static bool? Flag(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    static JsonObject Write(Set s) => new()
    {
        ["id"] = s.Id, ["name"] = s.Name,
        ["sheets"] = new JsonArray([.. s.Sheets.Select(x => (JsonNode)x)]),
        ["views"] = new JsonArray([.. s.Views.Select(x => (JsonNode)x)]),
        ["formats"] = s.Formats.ToJson(),
    };

    static void Save(ICommandContext ctx, IEnumerable<Set> sets)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        attrs[Key] = new JsonArray([.. sets.Select(s => (JsonNode)Write(s))]);
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>The sheets of these ids that are in the project, in the order given.</summary>
    public static List<Entity> SheetsOf(IModelView m, IEnumerable<string> ids) =>
        ids.Select(m.Get).OfType<Entity>().Where(e => e.Class == Sheets.Class).ToList();

    /// <summary>The views of these ids that are in the project (a level's own plan, <c>plan:&lt;storey&gt;</c>, and the 3D view, <c>3d</c>, too), in the order given.</summary>
    public static List<Entity> ViewsOf(IModelView m, IEnumerable<string> ids) =>
        ids.Select(id => Views.Find(m, id)).OfType<Entity>().ToList();

    static void CheckIds(IModelView m, List<string> sheets, List<string> views)
    {
        if (sheets.FirstOrDefault(id => m.Get(id) is not { Class: Sheets.Class }) is { } s) throw new CommandException($"There is no sheet {s}.");
        if (views.FirstOrDefault(id => Views.Find(m, id) == null) is { } v) throw new CommandException($"There is no view {v}.");
    }

    // ------------------------------------------------------------ the files

    public sealed record Output(string Name, byte[] Bytes);

    /// <summary>A name a file can carry on every system: no path or reserved characters, no trailing dots or spaces, never empty.</summary>
    public static string FileStem(string name)
    {
        var bad = "<>:\"/\\|?*".ToHashSet();
        var s = new string(name.Select(c => c < 32 || bad.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.', ' ');
        return s.Length == 0 ? "Untitled" : s;
    }

    /// <summary>A sheet's file stem: its number and name ("A-101 Ground floor"), its name alone when it has no number.</summary>
    public static string StemOf(Entity sheet) =>
        FileStem(sheet.Str("number") is { Length: > 0 } n ? $"{n} {sheet.Name}" : sheet.Name);

    /// <summary>
    /// The files the sheets and views make in these formats: each one's PDF and DXF (named after it, numbered when two
    /// share a name) and, with <c>Combined</c>, one PDF with every sheet then every view, named <paramref name="combinedName"/>.
    /// </summary>
    public static List<Output> Files(IQueryContext q, IReadOnlyList<Entity> sheets, IReadOnlyList<Entity> views, Formats f, string combinedName)
    {
        if (sheets.Count + views.Count == 0) throw new CommandException("Choose at least one sheet or view.");
        if (!f.Any) throw new CommandException("Choose at least one format: PDF, one PDF for all, or DXF.");
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string Unique(string stem, string ext)
        {
            var name = $"{stem}.{ext}";
            for (var i = 2; !taken.Add(name); i++) name = $"{stem} ({i}).{ext}";
            return name;
        }
        var outs = new List<Output>();
        var images = Sheets.ImageOf(q);
        // Each drawing's page made once: its own PDF and the combined one share it.
        var pages = new List<PaperPage>();
        foreach (var sheet in sheets)
        {
            var stem = StemOf(sheet);
            var page = Sheets.Compose(q, sheet);
            pages.Add(page);
            if (f.Pdf) outs.Add(new(Unique(stem, "pdf"), PdfWriter.Write([page], images)));
            if (f.Dxf) outs.Add(new(Unique(stem, "dxf"), Sheets.Dxf(q, sheet)));
        }
        foreach (var view in views)
        {
            var stem = FileStem(view.Name);
            var page = ViewPages.Page(q, view);
            pages.Add(page);
            if (f.Pdf) outs.Add(new(Unique(stem, "pdf"), PdfWriter.Write([page], images)));
            if (f.Dxf) outs.Add(new(Unique(stem, "dxf"), Views.Dxf(q, view)));
        }
        if (f.Combined) outs.Add(new(Unique(FileStem(combinedName), "pdf"), PdfWriter.Write(pages, images)));
        return outs;
    }

    /// <summary>The files as one zip, in the order made.</summary>
    public static byte[] Zip(IEnumerable<Output> files)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var o in files)
            {
                using var s = zip.CreateEntry(o.Name, CompressionLevel.Optimal).Open();
                s.Write(o.Bytes);
            }
        return ms.ToArray();
    }

    /// <summary>
    /// What an export asks for: a saved set (<c>set</c>: its sheets, views and formats, the formats given overriding its own)
    /// or the sheets and views given (<c>sheets</c>, <c>views</c>, <c>formats</c>). The name is the set's, or <c>name</c>.
    /// </summary>
    public static (List<Entity> Sheets, List<Entity> Views, Formats Formats, string Name, int Missing) Request(IModelView m, JsonObject p)
    {
        if ((string?)p["set"] is { Length: > 0 } setId)
        {
            var set = All(m).FirstOrDefault(s => s.Id == setId) ?? throw new CommandException("There is no such publish set.");
            var sheets = SheetsOf(m, set.Sheets);
            var views = ViewsOf(m, set.Views);
            return (sheets, views, Formats.Of(p["formats"], set.Formats), set.Name, set.Sheets.Count + set.Views.Count - sheets.Count - views.Count);
        }
        var sheetIds = Ids(p["sheets"]);
        var viewIds = Ids(p["views"]);
        CheckIds(m, sheetIds, viewIds);
        return (SheetsOf(m, sheetIds), ViewsOf(m, viewIds), Formats.Of(p["formats"]), (string?)p["name"] is { Length: > 0 } n ? n.Trim() : "Sheets and views", 0);
    }

    static JsonObject ToJson(IModelView m, Set s)
    {
        var sheets = SheetsOf(m, s.Sheets).Select(e => e.Id).ToList();
        var views = ViewsOf(m, s.Views).Select(e => e.Id).ToList();
        // A level's own plan is found by its storey: kept by the id it was saved with.
        var viewIds = s.Views.Where(id => Views.Find(m, id) != null).ToList();
        return new JsonObject
        {
            ["id"] = s.Id, ["name"] = s.Name,
            ["sheets"] = new JsonArray([.. sheets.Select(x => (JsonNode)x)]),
            ["views"] = new JsonArray([.. viewIds.Select(x => (JsonNode)x)]),
            ["missing"] = s.Sheets.Count - sheets.Count + s.Views.Count - views.Count,
            ["formats"] = s.Formats.ToJson(),
        };
    }

    public static void Register(IModuleRegistry r)
    {
        // A set added (no id) or changed: its name, its sheets and views (in order), its formats; what is not given stays.
        r.Command("publish.setSet", "Publish set", (ctx, a) =>
        {
            var sets = All(ctx.Model);
            var id = (string?)a["id"];
            var at = id == null ? -1 : sets.FindIndex(s => s.Id == id);
            if (id != null && at < 0) throw new CommandException("There is no such publish set.");
            var name = ((string?)a["name"])?.Trim();
            if (at < 0 && string.IsNullOrEmpty(name)) throw new CommandException("A publish set needs a name.");
            var old = at >= 0 ? sets[at] : new Set(ctx.NewId(), name!, [], [], Formats.Default);
            var sheets = a.ContainsKey("sheets") ? Ids(a["sheets"]) : old.Sheets;
            var views = a.ContainsKey("views") ? Ids(a["views"]) : old.Views;
            if (a.ContainsKey("sheets") || a.ContainsKey("views")) CheckIds(ctx.Model, a.ContainsKey("sheets") ? sheets : [], a.ContainsKey("views") ? views : []);
            var next = old with { Name = string.IsNullOrEmpty(name) ? old.Name : name, Sheets = sheets, Views = views, Formats = Formats.Of(a["formats"], old.Formats) };
            if (sets.Any(s => s.Id != next.Id && string.Equals(s.Name, next.Name, StringComparison.OrdinalIgnoreCase)))
                throw new CommandException($"There is a publish set named “{next.Name}” already.");
            if (at >= 0) sets[at] = next; else sets.Add(next);
            Save(ctx, sets);
            return new JsonObject { ["id"] = next.Id };
        });

        r.Command("publish.removeSet", "Remove publish set", (ctx, a) =>
        {
            var sets = All(ctx.Model);
            if (sets.RemoveAll(s => s.Id == (string?)a["id"]) == 0) throw new CommandException("There is no such publish set.");
            Save(ctx, sets);
            return null;
        });

        // The sets with the sheets and views still in the project, and how many they named that are gone.
        r.Query("publish.sets", (q, _) => new JsonArray([.. All(q.Model).Select(s => (JsonNode)ToJson(q.Model, s))]));
    }
}
