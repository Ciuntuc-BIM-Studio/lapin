using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Drawing;
using Lapin.Drawing.Export;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Title blocks of the user's own (docs/plan-title-blocks.md, decision 340): <c>LapinTitleBlock</c> entities of the project, each
/// a <see cref="TitleBlockTemplate"/>, and the built-ins (<see cref="BuiltinTitleBlocks"/>). The project names its default
/// (<c>titleBlock</c> on the IfcProject), a sheet its own (<c>titleBlockId</c>); neither: Lapin standard, drawn as before.
/// Live text: <c>{project.*}</c>, <c>{sheet.*}</c>, <c>{system.*}</c> and <c>{param.*}</c> tokens in any text, an unknown one
/// printed as <c>&lt;key&gt;</c>.
/// </summary>
public static partial class TitleBlocks
{
    public const string Class = "LapinTitleBlock";
    /// <summary>The project's default, on the IfcProject; a sheet's own, on the sheet.</summary>
    public const string DefaultKey = "titleBlock", SheetKey = "titleBlockId";
    const double FramePen = 0.5;

    [GeneratedRegex(@"\{\{?\s*([A-Za-z]\w*\.[A-Za-z0-9_.\-]+)\s*\}\}?")]
    private static partial Regex Token();

    // ------------------------------------------------------------ which template, where

    /// <summary>The id of the title block a sheet uses: its own, else the project's default, else Lapin standard; one gone falls back the same way.</summary>
    public static string AssignedId(IModelView m, Entity sheet)
    {
        foreach (var id in new[] { sheet.Str(SheetKey), Spatial.Project(m)?.Str(DefaultKey) })
            if (id is { Length: > 0 } && (BuiltinTitleBlocks.Is(id) ? BuiltinTitleBlocks.NameOf(id) != null : m.Get(id) is { Class: Class })) return id;
        return BuiltinTitleBlocks.Standard;
    }

    /// <summary>The project's default title block's id (Lapin standard when it names none).</summary>
    public static string DefaultId(IModelView m) =>
        Spatial.Project(m)?.Str(DefaultKey) is { Length: > 0 } id && (BuiltinTitleBlocks.Is(id) ? BuiltinTitleBlocks.NameOf(id) != null : m.Get(id) is { Class: Class }) ? id : BuiltinTitleBlocks.Standard;

    /// <summary>A title block's template by id (a built-in's or the project's); null when there is none.</summary>
    public static TitleBlockTemplate? TemplateById(IModelView m, string id) =>
        BuiltinTitleBlocks.Is(id) ? BuiltinTitleBlocks.Template(id) : m.Get(id) is { Class: Class } e ? TitleBlockTemplate.From(e.Attrs["template"]) : null;

    /// <summary>The template a sheet draws; null for Lapin standard, which <see cref="Sheets.Compose"/> draws with its own code.</summary>
    public static TitleBlockTemplate? TemplateOf(IModelView m, Entity sheet)
    {
        var id = AssignedId(m, sheet);
        return id == BuiltinTitleBlocks.Standard ? null : TemplateById(m, id);
    }

    /// <summary>A title block's name: a built-in's, or the entity's.</summary>
    public static string NameOf(IModelView m, string id) => BuiltinTitleBlocks.NameOf(id) ?? m.Get(id)?.Name ?? id;

    /// <summary>Where the frame and the title block sit on a sheet's paper (mm, y down from the top-left).</summary>
    public sealed record SheetLayout(double W, double H, bool ShowFrame, (double X0, double Y0, double X1, double Y1) Frame, (double X0, double Y0, double X1, double Y1) Block);

    public static SheetLayout Layout(IModelView m, Entity sheet) => Layout(Sheets.PaperOf(sheet), TemplateOf(m, sheet));

    /// <summary>The frame and block of a paper with a template (null: Lapin standard's, 10 mm in, 180 × 50 in the bottom-right corner).</summary>
    public static SheetLayout Layout((double W, double H) paper, TitleBlockTemplate? t)
    {
        var (W, H) = paper;
        if (t == null)
        {
            var (bw, bh) = Sheets.TitleBlock;
            const double M = Sheets.Margin;
            return new(W, H, true, (M, M, W - M, H - M), (W - M - bw, H - M - bh, W - M, H - M));
        }
        var f = t.Frame;
        var frame = (X0: f.Left, Y0: f.Top, X1: W - f.Right, Y1: H - f.Bottom);
        var right = t.Anchor is "bottomRight" or "topRight";
        var bottom = t.Anchor is "bottomRight" or "bottomLeft";
        var x0 = right ? frame.X1 - t.W : frame.X0;
        var y0 = bottom ? frame.Y1 - t.H : frame.Y0;
        return new(W, H, f.Show, frame, (x0, y0, x0 + t.W, y0 + t.H));
    }

    // ------------------------------------------------------------ live text

    /// <summary>The tokens a text holds, in order (<c>project.name</c>).</summary>
    public static List<string> Tokens(string text) => [.. Token().Matches(text).Select(m => m.Groups[1].Value)];

    /// <summary>A text with its tokens replaced by their values (one with none: empty); an unknown token as <c>&lt;key&gt;</c>, listed in <paramref name="unknown"/>.</summary>
    public static string Fill(string text, Func<string, (bool Known, string? Value)> resolve, List<string>? unknown = null) =>
        Token().Replace(text, m =>
        {
            var key = m.Groups[1].Value;
            var (known, value) = resolve(key);
            if (known) return value ?? "";
            unknown?.Add(key);
            return $"<{key}>";
        });

    /// <summary>
    /// What a sheet's tokens say: the project's (its name, information and own parameters), the sheet's (number, name, scales of
    /// its views, paper, title block fields with the project's information behind them, x of y), the system's and the
    /// project's parameters (decision 119). <paramref name="sheet"/> null: a sample sheet's (the designer with no sheet).
    /// </summary>
    public static Func<string, (bool Known, string? Value)> Resolver(IQueryContext q, Entity? sheet, IReadOnlyList<Sheets.Placement>? placements = null)
    {
        var m = q.Model;
        List<Entity>? order = null;
        string? scales = null;
        return key =>
        {
            var dot = key.IndexOf('.');
            var (source, k) = (key[..dot], key[(dot + 1)..]);
            switch (source)
            {
                case "project":
                    if (k == "name") return (true, Sheets.ProjectName(q));
                    if (Documents.ProjectInfo.Fields.Any(f => f.Key == k)) return (true, Documents.ProjectInfo.Get(m, k));
                    return Documents.ProjectInfo.Customs(m).FirstOrDefault(c => c.Key.Equals(k, StringComparison.OrdinalIgnoreCase)) is { } c ? (true, c.Printed) : (false, null);
                case "param":
                    return Spatial.Project(m)?.Attrs["params"] is JsonArray ps && ps.OfType<JsonObject>().FirstOrDefault(p => string.Equals((string?)p["name"], k, StringComparison.OrdinalIgnoreCase) && Params.SetOf(p) == null) is { } param
                        ? (true, ParamText(param)) : (false, null);
                case "system":
                    return k switch
                    {
                        "printDate" => (true, DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)),
                        "printTime" => (true, DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture)),
                        "file" => (true, q.Title is { Length: > 0 } t ? t + ".lapin" : ""),
                        "app" => (true, "Lapin"),
                        _ => (false, null),
                    };
                case "sheet":
                    return SheetValue(k);
                default: return (false, null);
            }
        };

        (bool, string?) SheetValue(string k)
        {
            var tb = sheet?.Attrs["titleBlock"] as JsonObject;
            string? Own(string f) => (string?)tb?[f] is { Length: > 0 } v ? v : null;
            switch (k)
            {
                case "number": return (true, sheet?.Str("number") ?? "A-101");
                case "name": case "title": return (true, sheet?.Name ?? "Sheet");
                case "project": return (true, Own("project") ?? Sheets.ProjectName(q));
                case "client": return (true, Own("client") ?? Documents.ProjectInfo.Get(m, "client"));
                case "scale":
                    if (sheet == null) return (true, "1:100");
                    scales ??= string.Join(", ", (placements ?? Sheets.Place(q, sheet)).Where(p => !p.Content).Select(p => p.Drawing.Scale).Distinct().OrderBy(x => x).Select(s => $"1:{s:0}"));
                    return (true, scales);
                case "paper": return (true, sheet?.Str("paper") ?? "A1");
                case "size": { var (w, h) = sheet != null ? Sheets.PaperOf(sheet) : (841, 594); return (true, $"{w:0} × {h:0} mm"); }
                case "orientation": return (true, sheet?.Str("orientation") ?? "landscape");
                case "date": return (true, Own("date") ?? DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
                case "drawnBy": return (true, Own("drawnBy") ?? Documents.ProjectInfo.Get(m, "author"));
                case "checkedBy": return (true, Own("checkedBy") ?? Documents.ProjectInfo.Get(m, "checkedBy"));
                case "approvedBy": return (true, Own("approvedBy") ?? Documents.ProjectInfo.Get(m, "approvedBy"));
                case "phase": return (true, Own("phase") ?? Documents.ProjectInfo.Get(m, "phase"));
                case "revision": case "status": return (true, Own(k));
                case "discipline": return (true, sheet?.Str("discipline") ?? Views.Disciplines[0]);
                case "index": case "count":
                {
                    order ??= [.. m.OfClass(Sheets.Class).OrderBy(s => s.Str("number") ?? "", NaturalOrder.Instance)];
                    if (k == "count") return (true, Math.Max(order.Count, 1).ToString(CultureInfo.InvariantCulture));
                    var i = sheet == null ? 0 : order.FindIndex(s => s.Id == sheet.Id);
                    return (true, (Math.Max(i, 0) + 1).ToString(CultureInfo.InvariantCulture));
                }
                default:
                    // Any other field the sheet's title block sets (typed in its properties or by a command).
                    return tb?[k] is JsonValue ? (true, Own(k)) : (false, null);
            }
        }
    }

    static string? ParamText(JsonObject p) => p["value"] is not { } v ? null
        : Json.Number(v) is { } n && (string?)p["kind"] is "number" or "length" or "area" or "angle" ? n.ToString("0.###", CultureInfo.InvariantCulture)
        : v is JsonValue b && b.TryGetValue<bool>(out var yes) ? (yes ? "yes" : "no")
        : v.ToString();

    /// <summary>The tokens offered to insert, by source, with their values on <paramref name="sheet"/> (or a sample sheet).</summary>
    public static List<(string Key, string Label, string Group, string? Value)> FieldList(IQueryContext q, Entity? sheet)
    {
        var resolve = Resolver(q, sheet);
        var o = new List<(string Key, string Label, string Group, string? Value)>();
        void Add(string key, string label, string group) => o.Add((key, label, group, resolve(key).Value));
        Add("project.name", "Project name", "project");
        foreach (var (key, label, _) in Documents.ProjectInfo.Grouped) Add("project." + key, label, "project");
        foreach (var c in Documents.ProjectInfo.Customs(q.Model)) Add("project." + c.Key, c.Label, "project");
        foreach (var p in Spatial.Project(q.Model)?.Attrs["params"] as JsonArray ?? [])
            if (p is JsonObject po && (string?)po["name"] is { Length: > 0 } name && Params.SetOf(po) == null) Add("param." + name, name, "project");
        (string Key, string Label)[] sheetFields =
        [
            ("number", "Sheet number"), ("name", "Sheet name"), ("scale", "Scale"), ("paper", "Paper"), ("size", "Paper size"), ("orientation", "Orientation"),
            ("revision", "Revision"), ("status", "Status"), ("date", "Date"), ("drawnBy", "Drawn by"), ("checkedBy", "Checked by"), ("approvedBy", "Approved by"),
            ("phase", "Phase"), ("client", "Client"), ("project", "Project (the sheet's, else the project's name)"), ("discipline", "Discipline"),
            ("index", "Sheet x (of y)"), ("count", "Sheets in all (y)"),
        ];
        foreach (var (key, label) in sheetFields) Add("sheet." + key, label, "sheet");
        foreach (var (key, label) in new[] { ("printDate", "Date of print"), ("printTime", "Time of print"), ("file", "File name"), ("app", "Program") }) Add("system." + key, label, "system");
        return o;
    }

    // ------------------------------------------------------------ drawing

    /// <summary>
    /// A template drawn on a sheet's paper into <paramref name="pens"/>: the frame (when shown), then the block's items in its box
    /// with their live text filled, each pen marked as the block's (DXF writes it as a block with attributes); the revision
    /// table where an item puts it, else above the block (as Lapin standard has it) unless the template says none.
    /// </summary>
    public static void Draw(IQueryContext q, Entity sheet, TitleBlockTemplate t, SheetLayout l, IReadOnlyList<Sheets.Placement> placements, Sheets.Issuing? issuing, List<Pen> pens, List<string>? unknown = null)
    {
        if (l.ShowFrame)
        {
            var frame = new Pen("FRAME", "#000000", FramePen, null, null);
            var (fx0, fy0, fx1, fy1) = l.Frame;
            frame.Segments.AddRange([(new(fx0, fy0), new(fx1, fy0)), (new(fx1, fy0), new(fx1, fy1)), (new(fx1, fy1), new(fx0, fy1)), (new(fx0, fy1), new(fx0, fy0))]);
            pens.Add(frame);
        }
        var block = "TB_" + Regex.Replace(TitleBlockName(q.Model, sheet), @"[^\p{L}\p{N}_\-]+", "_");
        var resolve = Resolver(q, sheet, placements);
        var revisions = Sheets.RevisionRows(q.Model, sheet, issuing);
        var own = DrawItems(q.Model, t, l.Block.X0, l.Block.Y1, resolve, revisions, unknown);
        foreach (var p in own) p.Block = block;
        pens.AddRange(own);
        if (t.Revisions == "above" && !t.Items.OfType<JsonObject>().Any(i => (string?)i["type"] == "revisions"))
        {
            var frame = new Pen("FRAME", "#000000", FramePen, null, null);
            var rules = new Pen("TITLE", "#000000", 0.25, null, null);
            var text = new Pen("TEXT", "#000000", 0, null, null);
            Sheets.RevisionTable(revisions, frame, rules, text, l.Block.X0, l.Block.Y0, l.Block.X1 - l.Block.X0);
            pens.AddRange([frame, rules, text]);
        }
    }

    static string TitleBlockName(IModelView m, Entity sheet) => NameOf(m, AssignedId(m, sheet));

    /// <summary>
    /// A template's items as pens on paper (mm, y down): the block's bottom-left corner at (<paramref name="x0"/>, <paramref name="y1"/>),
    /// its items' y up from there; texts filled by <paramref name="resolve"/>, a live one tagged by its first token.
    /// </summary>
    public static List<Pen> DrawItems(IModelView m, TitleBlockTemplate t, double x0, double y1, Func<string, (bool Known, string? Value)> resolve,
        IReadOnlyList<(string Revision, string Date, string Status, string Name)> revisions, List<string>? unknown = null)
    {
        Vec2 P(double x, double y) => new(x0 + x, y1 - y);
        Vec2 Pt(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? P(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : P(0, 0);
        var book = DraftingResources.Book(m);
        var patterns = DraftingResources.Patterns(m);
        var fills = new List<Pen>();
        var lines = new Dictionary<string, Pen>();
        var texts = new Dictionary<string, Pen>();
        var images = new Pen("IMAGE", null, 0, null, null);
        Pen LinePen(double w, string? color, double[]? dash)
        {
            var c = color ?? "#000000";
            var key = $"{w}:{c}:{(dash == null ? "" : string.Join(" ", dash))}";
            if (!lines.TryGetValue(key, out var pen)) lines[key] = pen = new Pen(w >= FramePen ? "FRAME" : "TITLE", c, w, null, dash);
            return pen;
        }
        Pen TextPen(string? color)
        {
            var c = color ?? "#000000";
            if (!texts.TryGetValue(c, out var pen)) texts[c] = pen = new Pen("TEXT", c, 0, null, null);
            return pen;
        }
        static double[]? Dash(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? [.. a.Select(x => Math.Max(0.05, Json.Number(x) ?? 1))] : null;
        void Outline(JsonObject item, List<Vec2> loop, bool closed)
        {
            // The fill or the hatch under the outline.
            if (closed && (string?)item["fill"] is { Length: > 0 } fill)
            {
                var pen = new Pen("TITLE-FILL", null, 0, fill, null);
                pen.Loops.Add(loop);
                fills.Add(pen);
            }
            if (closed && item["hatch"] is JsonObject h && HatchLibrary.Find((string?)h["pattern"] ?? "LINES45", patterns) is { } pattern)
            {
                var color = (string?)h["color"] ?? "#000000";
                if (pattern.Solid) { var pen = new Pen("TITLE-FILL", null, 0, color, null); pen.Loops.Add(loop); fills.Add(pen); }
                else
                {
                    var (segs, _) = HatchFill.Segments([loop], pattern, new HatchOptions(Json.Number(h["scale"]) ?? 1, Json.Number(h["angle"]) ?? 0, null, 20_000));
                    var pen = LinePen(Json.Number(h["w"]) ?? 0.13, color, null);
                    foreach (var s in segs) pen.Segments.Add((s.A, s.B));
                }
            }
            var w = Json.Number(item["stroke"]) ?? Json.Number(item["w"]) ?? 0.25;
            if (w <= 0) return;
            var lp = LinePen(w, (string?)item["color"], Dash(item["dash"]));
            for (var i = 0; i + 1 < loop.Count; i++) lp.Segments.Add((loop[i], loop[i + 1]));
            if (closed && loop.Count > 2) lp.Segments.Add((loop[^1], loop[0]));
        }
        foreach (var item in t.Items.OfType<JsonObject>())
        {
            switch ((string?)item["type"])
            {
                case "line":
                {
                    var w = Json.Number(item["w"]) ?? 0.25;
                    if (w > 0) LinePen(w, (string?)item["color"], Dash(item["dash"])).Segments.Add((Pt(item["a"]), Pt(item["b"])));
                    break;
                }
                case "rect":
                {
                    double x = Json.Number(item["x"]) ?? 0, y = Json.Number(item["y"]) ?? 0, w = Json.Number(item["w"]) ?? 0, h = Json.Number(item["h"]) ?? 0;
                    Outline(item, [P(x, y), P(x + w, y), P(x + w, y + h), P(x, y + h)], true);
                    break;
                }
                case "polyline":
                {
                    var pts = (item["pts"] as JsonArray ?? []).Select(Pt).ToList();
                    if (pts.Count >= 2) Outline(item, pts, item["closed"] is JsonValue c && c.TryGetValue<bool>(out var closed) && closed);
                    break;
                }
                case "text":
                {
                    var raw = item["text"]?.ToString() ?? "";
                    var tokens = Tokens(raw);
                    var value = Fill(raw, resolve, unknown);
                    var style = (string?)item["style"] is { Length: > 0 } sid ? Standards.TextOf(book, sid) : null;
                    var height = Json.Number(item["h"]) ?? style?.Height ?? 2.5;
                    var bold = item["bold"] is JsonValue b ? b.TryGetValue<bool>(out var bb) && bb : style?.Bold ?? false;
                    var italic = item["italic"] is JsonValue it ? it.TryGetValue<bool>(out var ii) && ii : style?.Italic ?? false;
                    var font = (string?)item["font"] ?? style?.Font ?? "sans";
                    // Shrunk to its width when it would run over it (a long project name in its cell).
                    if (Json.Number(item["fit"]) is > 0 and var fit && value.Length > 0)
                    {
                        var width = PdfWriter.Advance(value, bold) * height / 0.718;
                        if (width > fit) height *= fit / width;
                    }
                    var anchor = (string?)item["align"] is "middle" or "end" ? (string)item["align"]! : "start";
                    var baseline = (string?)item["valign"] == "middle" ? "middle" : "bottom";
                    var pen = TextPen((string?)item["color"] ?? style?.Color);
                    // Each line of a text one under the other.
                    var rows = value.Split('\n');
                    for (var r = 0; r < rows.Length; r++)
                    {
                        var at = Pt(item["at"]) + new Vec2(0, r * height * 1.4);
                        pen.Texts.Add(new PenText(at, rows[r], height, Json.Number(item["angle"]) ?? 0, anchor, baseline, bold, font, italic)
                        {
                            Tag = tokens.Count > 0 ? ((string?)item["tag"] is { Length: > 0 } tag ? tag : tokens[0].Replace('.', '_')) + (rows.Length > 1 ? $"_{r + 1}" : "") : null,
                        });
                    }
                    break;
                }
                case "image":
                {
                    if ((string?)item["imageId"] is not { Length: > 0 } id || m.Get(id) is not { Class: Images.Class }) break;
                    double x = Json.Number(item["x"]) ?? 0, y = Json.Number(item["y"]) ?? 0, w = Json.Number(item["w"]) ?? 0, h = Json.Number(item["h"]) ?? 0;
                    images.Images.Add(new PenImage(id, [P(x, y + h), P(x + w, y + h), P(x + w, y), P(x, y)]));
                    break;
                }
                case "revisions":
                {
                    double x = Json.Number(item["x"]) ?? 0, y = Json.Number(item["y"]) ?? 0, w = Json.Number(item["w"]) ?? 0, h = Json.Number(item["h"]) ?? 0;
                    var rowH = Math.Max(2.5, Json.Number(item["rowH"]) ?? 5);
                    var size = Math.Min(2.5, rowH * 0.5);
                    var rules = LinePen(Json.Number(item["stroke"]) ?? 0.25, (string?)item["color"], null);
                    var text = TextPen((string?)item["color"]);
                    var labels = item["labels"] as JsonObject;
                    string Label(string k, string d) => (string?)labels?[k] is { } s ? s : d;
                    double[] cols = [0, w * 0.12, w * 0.32, w * 0.47];
                    var count = Math.Max(0, (int)Math.Floor(h / rowH) - 1);
                    var shown = revisions.TakeLast(count).ToList();
                    // The header on top, the issues under it oldest first, the last ones that fit.
                    var top = y + h;
                    rules.Segments.AddRange([(P(x, top), P(x + w, top)), (P(x, y), P(x + w, y)), (P(x, y), P(x, top)), (P(x + w, y), P(x + w, top))]);
                    for (var i = 1; i <= count; i++) rules.Segments.Add((P(x, top - i * rowH), P(x + w, top - i * rowH)));
                    foreach (var c in cols.Skip(1)) rules.Segments.Add((P(x + c, y), P(x + c, top)));
                    void Row(double baseY, string[] cells, bool head)
                    {
                        for (var c = 0; c < cells.Length; c++) text.Texts.Add(new PenText(P(x + cols[c] + 1.2, baseY), cells[c], head ? Math.Min(1.8, size) : size, Anchor: "start", Bold: !head && c == 0));
                    }
                    Row(top - rowH + rowH * 0.3, [Label("revision", "REV"), Label("date", "DATE"), Label("status", "STATUS"), Label("description", "DESCRIPTION")], true);
                    for (var i = 0; i < shown.Count; i++)
                        Row(top - (i + 2) * rowH + rowH * 0.3, [shown[i].Revision, shown[i].Date, shown[i].Status, shown[i].Name], false);
                    break;
                }
            }
        }
        var o = new List<Pen>();
        if (images.Images.Count > 0) o.Add(images);
        o.AddRange(fills);
        o.AddRange(lines.Values);
        o.AddRange(texts.Values);
        return o;
    }

    /// <summary>The tokens a sheet's title block cannot fill (a key no source has), each once.</summary>
    public static List<string> Unknown(IQueryContext q, Entity sheet)
    {
        if (TemplateOf(q.Model, sheet) is not { } t) return [];
        var resolve = Resolver(q, sheet, []);
        var unknown = new List<string>();
        foreach (var item in t.Items.OfType<JsonObject>().Where(i => (string?)i["type"] == "text"))
            Fill(item["text"]?.ToString() ?? "", key => key == "sheet.scale" ? (true, "") : resolve(key), unknown);
        return [.. unknown.Distinct()];
    }

    /// <summary>A block alone as the designer and the list show it: its box (mm) and its pens, y down from its top-left.</summary>
    public static (double W, double H, List<Pen> Pens) Preview(IQueryContext q, TitleBlockTemplate t, Entity? sheet)
    {
        var resolve = Resolver(q, sheet);
        var revisions = sheet != null ? Sheets.RevisionRows(q.Model, sheet, null) : [("P01", DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), "S2", "For information")];
        return (t.W, t.H, DrawItems(q.Model, t, 0, t.H, resolve, revisions));
    }
}

/// <summary>Text compared with its numbers as numbers (A-9 before A-10).</summary>
sealed class NaturalOrder : IComparer<string>
{
    public static readonly NaturalOrder Instance = new();

    public int Compare(string? a, string? b)
    {
        a ??= ""; b ??= "";
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                var si = i; while (i < a.Length && char.IsDigit(a[i])) i++;
                var sj = j; while (j < b.Length && char.IsDigit(b[j])) j++;
                var (na, nb) = (a[si..i].TrimStart('0'), b[sj..j].TrimStart('0'));
                var c = na.Length != nb.Length ? na.Length.CompareTo(nb.Length) : string.CompareOrdinal(na, nb);
                if (c != 0) return c;
                continue;
            }
            var d = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
            if (d != 0) return d;
            i++; j++;
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}
