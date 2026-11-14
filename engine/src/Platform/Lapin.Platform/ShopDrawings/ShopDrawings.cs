using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A drawing rule (docs/plan-shop-drawings.md §5, decision 581): which element views a kind of element gets on its sheet (the
/// orthographic roles, sections square to its axes at stations, an iso), its dimensions, the papers it may go on (the first that
/// fits wins, unless <see cref="Prefer"/> is <c>scale</c>: then the scale goes down the shop series first), the sheet's name.
/// The built-in rules are coded (the generic one here, the modules' through <see cref="IDrawingRules"/>); a user's own come in
/// stage S6, beside them.
/// </summary>
public sealed record DrawingRule(string Id, string Name, IReadOnlyList<string> Views, IReadOnlyList<(string Role, double? Station)> Sections,
    bool Iso = true, string Dimensions = "overall", string Prefer = "paper", IReadOnlyList<string>? Papers = null, string SheetName = "{kind} {mark}")
{
    public IReadOnlyList<string> PaperList => Papers ?? ShopDrawings.DefaultPapers;
}

/// <summary>A module's drawing rules for its kinds of element (a steel member's, a plate's): the rule for an element, null when it has none.</summary>
public interface IDrawingRules
{
    IEnumerable<DrawingRule> Rules();
    DrawingRule? RuleFor(IModelView m, Entity e);
}

/// <summary>
/// Shop drawings of a single part (docs/plan-shop-drawings.md §6, §7.1, decision 581): one command makes the sheet and the
/// element views a rule asks for, laid out in ISO E (first angle: the top view under the front, the view from the left on its
/// right) or third angle, on the first paper that fits at the scale, with the part's data over the title block. The drawing is
/// kept as a <c>LapinShopDrawing</c> (its element, rule, choices, sheet and views by role), so making it again keeps every id
/// and the user's own edits on its views, and its views are drawn by the engine like any other, always as the part is now.
/// </summary>
public static class ShopDrawings
{
    public const string Class = "LapinShopDrawing";
    public const string Discipline = "Shop drawings";
    public static readonly string[] DefaultPapers = ["A4", "A3", "A2", "A1", "A0"];
    public static readonly string[] Projections = ["iso-e", "third-angle"];
    public static readonly string[] DimensionModes = ["overall", "none"];

    /// <summary>The generic rule: front, top, the view from the right and an iso, overall dimensions (any element).</summary>
    public static readonly DrawingRule Generic = new("generic", "Generic", ["front", "top", "right"], [], true);

    /// <summary>Every rule the product has: the modules' first, the generic one last.</summary>
    public static List<DrawingRule> Rules(IServices services) => [.. services.All<IDrawingRules>().SelectMany(r => r.Rules()), Generic];

    /// <summary>The rule for an element: the one asked for by id, else its module's for its kind, else the generic one.</summary>
    public static DrawingRule RuleFor(IModelView m, IServices services, Entity e, string? id = null)
    {
        if (id is { Length: > 0 })
            return Rules(services).FirstOrDefault(r => r.Id == id) ?? throw new CommandException($"There is no drawing rule „{id}”.");
        foreach (var s in services.All<IDrawingRules>())
            if (s.RuleFor(m, e) is { } r) return r;
        return Generic;
    }

    /// <summary>The project's projection method for its drawings (Drafting standards: <c>drafting.projection</c>), ISO E by default.</summary>
    public static string ProjectionOf(IModelView m) =>
        (string?)Spatial.Project(m)?.Attrs["drafting"]?["projection"] is { } p && Projections.Contains(p) ? p : "iso-e";

    /// <summary>What a drawing is made with: the rule's choices and the user's over them.</summary>
    public sealed record Options(List<string> Views, List<(string Role, double? Station)> Sections, bool Iso, double? Scale, string? Paper,
        string Projection, string Dims, string Prefer, string Rule)
    {
        public JsonObject ToJson()
        {
            var o = new JsonObject
            {
                ["views"] = new JsonArray(Views.Select(v => (JsonNode)v).ToArray()),
                ["sections"] = new JsonArray(Sections.Select(s => (JsonNode)new JsonObject { ["role"] = s.Role, ["station"] = s.Station is { } st ? Math.Round(st, 6) : null }).ToArray()),
                ["iso"] = Iso, ["projection"] = Projection, ["dims"] = Dims, ["prefer"] = Prefer,
            };
            if (Scale is { } sc) o["scale"] = sc;
            if (Paper != null) o["paper"] = Paper;
            return o;
        }
    }

    static bool? Flag(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    /// <summary>The options from a command's arguments over those kept (<paramref name="kept"/>, a drawing made before) over the rule's.</summary>
    public static Options OptionsFrom(IModelView m, DrawingRule rule, JsonObject a, JsonObject? kept)
    {
        JsonNode? Arg(string k) => a.ContainsKey(k) ? a[k] : kept?[k];
        var views = Arg("views") is JsonArray va ? va.Select(x => (string?)x).OfType<string>().Distinct().ToList() : [.. rule.Views];
        foreach (var v in views) if (!ElementViews.Ortho.Contains(v)) throw new CommandException($"„{v}” is not a face of the part: front, back, top, bottom, left or right.");
        var sections = new List<(string, double?)>();
        if (Arg("sections") is JsonArray sa)
            foreach (var s in sa.OfType<JsonObject>())
            {
                var role = (string?)s["role"] ?? "section:x";
                if (!ElementViews.SectionRoles.Contains(role)) throw new CommandException("A section is square to the part's x, y or z: section:x, section:y or section:z.");
                sections.Add((role, Json.Number(s["station"])));
            }
        else sections.AddRange(rule.Sections);
        if (views.Count == 0 && sections.Count == 0 && Flag(Arg("iso")) != true && !(rule.Iso && Flag(Arg("iso")) == null))
            throw new CommandException("Tick a view of the part at least.");
        var scale = Json.Number(Arg("scale")) is { } sc && sc >= 1 && sc <= 1000 ? sc : (double?)null;
        var paper = (string?)Arg("paper") is { Length: > 0 } p && p != "auto" ? Sheets.Papers.ContainsKey(p) ? p : throw new CommandException($"„{p}” is not a paper.") : null;
        var projection = (string?)Arg("projection") is { } pr && Projections.Contains(pr) ? pr : ProjectionOf(m);
        var dims = (string?)Arg("dims") is { } d && DimensionModes.Contains(d) ? d : rule.Dimensions;
        var prefer = (string?)Arg("prefer") is "scale" or "paper" ? (string)Arg("prefer")! : rule.Prefer;
        return new Options(views, sections, Flag(Arg("iso")) ?? rule.Iso, scale, paper, projection, dims, prefer, rule.Id);
    }

    // ------------------------------------------------------------ the layout

    /// <summary>A view's place on the sheet: its key, role, station, scale, where its drawing's middle falls (paper mm) and its box.</summary>
    public sealed record Slot(string Key, string Role, double? Station, double Scale, double Cx, double Cy, double X0, double Y0, double X1, double Y1);

    /// <summary>The views laid out on a paper at a scale; <see cref="Fits"/> false when even the last paper and scale did not hold them.</summary>
    public sealed record Arrangement(string Paper, double Scale, List<Slot> Slots, (double X, double Y, double W) Data, bool Fits);

    /// <summary>Where each role goes round the front (column, row; the front at 1, 1), first angle or third.</summary>
    static (int Col, int Row) Cell(string role, string projection) => (role, projection == "third-angle") switch
    {
        ("front", _) => (1, 1),
        ("top", false) => (1, 2), ("bottom", false) => (1, 0), ("left", false) => (2, 1), ("right", false) => (0, 1),
        ("top", true) => (1, 0), ("bottom", true) => (1, 2), ("left", true) => (0, 1), ("right", true) => (2, 1),
        _ => (3, 1),
    };

    const double Gap = 8, Pad = 3, DimRoom = 11, TitleRoom = 8, Inset = 6, DataRow = 5, DataWidth = 100, DataLabel = 26;

    /// <summary>The scale an iso is drawn at: half the views' (the next of the shop series at twice their number).</summary>
    public static double IsoScale(double scale) => ElementViews.ShopScales.FirstOrDefault(s => s >= scale * 2 - 1e-9) is var s && s > 0 ? s : scale * 2;

    /// <summary>The rows of the part's data table (its name, kind, type, material, size, weight, quantity): how tall it stands (mm).</summary>
    public static double DataHeight(int rows) => rows * DataRow + 2;

    /// <summary>
    /// The views laid out: the first paper (in the order asked) that holds them at the scale, else down the shop series of scales
    /// (prefer <c>scale</c>: the scale first, then the next paper). The orthographic views round the front by the projection
    /// method, the sections in a row under them, the iso at the top right, the part's data over the title block.
    /// </summary>
    public static Arrangement Arrange(IModelView m, Entity sheetProto, ElementFrame f, Options o, IReadOnlyList<string> papers, int dataRows, Func<string, string>? titles = null)
    {
        var size = Math.Max(f.Length, Math.Max(f.Width, f.Height));
        var s0 = o.Scale ?? ElementViews.AutoScale(size, 110);
        var scales = new List<double> { s0 };
        scales.AddRange(ElementViews.ShopScales.Where(s => s > s0 + 1e-9).Select(s => (double)s));
        var paperList = o.Paper != null ? [o.Paper] : papers.Where(Sheets.Papers.ContainsKey).ToList();
        if (paperList.Count == 0) paperList = ["A3"];
        Arrangement? last = null;
        if (o.Prefer == "scale")
        {
            foreach (var p in paperList)
                foreach (var s in scales)
                    if (Try(m, sheetProto, f, o, p, s, dataRows, titles, out last)) return last!;
        }
        else
        {
            // A scale asked for is kept while some paper holds it; only then does it go down the series.
            foreach (var s in scales)
                foreach (var p in paperList)
                    if (Try(m, sheetProto, f, o, p, s, dataRows, titles, out last)) return last!;
        }
        return last! with { Fits = false };
    }

    sealed record Item(string Key, string Role, double? Station, bool Iso, double W, double H, double Scale, bool Dims, double TitleW = 0)
    {
        double K => 1000 / Scale;
        public double L => W * K / 2 + Pad;
        /// <summary>To the right of the middle: the drawing, its dimensions, and its title (written from the box's left) when that is wider.</summary>
        public double R => Math.Max(W * K / 2 + Pad + (Dims ? DimRoom : 0), TitleW - L);
        public double U => H * K / 2 + Pad;
        public double D => H * K / 2 + Pad + (Dims ? DimRoom : 0) + TitleRoom;
    }

    /// <summary>A view drawn smaller than this (its larger side, paper mm) at the drawing's scale gets a scale of its own (an end view of a long member).</summary>
    const double Tiny = 20;
    /// <summary>How large a section, or a view of its own scale, is drawn (its larger side, paper mm, at most).</summary>
    const double Detail = 45;

    /// <summary>
    /// The views of the options, each with its drawing's size (m) on the frame and its scale: the drawing's; a section's or a
    /// tiny view's its own (the next of the shop series that draws it about <see cref="Detail"/> mm, never smaller than the drawing's);
    /// the iso's half the drawing's.
    /// </summary>
    static List<Item> Items(ElementFrame f, Options o, double scale, Func<string, string>? titles)
    {
        var dims = o.Dims != "none";
        var items = new List<Item>();
        double TitleW(string key, double sc) => titles?.Invoke(key) is { } t
            ? PdfWriter.Advance(t, true) * 3.5 / 0.718 + 4 + PdfWriter.Advance($"1:{sc:0}", false) * 2.5 / 0.718 + 2 : 0;
        double Own(double w, double h) => Math.Min(scale, ElementViews.AutoScale(Math.Max(w, h), Detail));
        foreach (var v in o.Views)
        {
            var (w, h) = ElementViews.Size(f, v);
            var sc = Math.Max(w, h) * 1000 / scale < Tiny ? Own(w, h) : scale;
            items.Add(new Item(v, v, null, false, w, h, sc, dims, TitleW(v, sc)));
        }
        for (var i = 0; i < o.Sections.Count; i++)
        {
            var (role, station) = o.Sections[i];
            var (w, h) = ElementViews.Size(f, role);
            var key = $"{role}#{i}";
            var sc = Own(w, h);
            items.Add(new Item(key, role, station, false, w, h, sc, dims, TitleW(key, sc)));
        }
        if (o.Iso) { var (w, h) = ElementViews.IsoSize(f); items.Add(new Item(ElementViews.Iso, ElementViews.Iso, null, true, w, h, IsoScale(scale), false, TitleW(ElementViews.Iso, IsoScale(scale)))); }
        return items;
    }

    static bool Try(IModelView m, Entity sheetProto, ElementFrame f, Options o, string paper, double scale, int dataRows, Func<string, string>? titles, out Arrangement? result)
    {
        var (pw, ph) = Sheets.Papers[paper];
        var (W, H) = pw >= ph ? (pw, ph) : (ph, pw);
        var proto = sheetProto.CloneAttrs();
        proto["paper"] = paper;
        proto["orientation"] = "landscape";
        var layout = TitleBlocks.Layout(m, sheetProto.With(attrs: proto));
        var (fx0, fy0, fx1, fy1) = layout.Frame;
        var (bx0, by0, bx1, by1) = layout.Block;
        var dataH = DataHeight(dataRows);
        // The title block and the part's data over its right end: nothing goes on them.
        var dataW = Math.Min(DataWidth, bx1 - bx0);
        (double X0, double Y0, double X1, double Y1)[] reserved = [(bx0, by0, bx1, by1), (bx1 - dataW, by0 - dataH - 3, bx1, by0)];
        var items = Items(f, o, scale, titles);
        var grid = items.Where(i => !i.Iso && !i.Role.StartsWith("section:", StringComparison.Ordinal)).ToList();
        var sections = items.Where(i => i.Role.StartsWith("section:", StringComparison.Ordinal)).ToList();
        var iso = items.FirstOrDefault(i => i.Iso);
        var at = new Dictionary<string, (double Cx, double Cy)>();
        // The grid: each column as wide as its widest view each side of the middles, each row as tall.
        var cells = grid.ToDictionary(i => i.Key, i => Cell(i.Role, o.Projection));
        double x = fx0 + Inset, gridRight = x, gridBottom = fy0 + Inset;
        var colX = new double[4];
        for (var c = 0; c < 4; c++)
        {
            var col = grid.Where(i => cells[i.Key].Col == c).ToList();
            if (col.Count == 0) continue;
            colX[c] = x + col.Max(i => i.L);
            x = colX[c] + col.Max(i => i.R) + Gap;
            gridRight = x - Gap;
        }
        var y = fy0 + Inset;
        var rowY = new double[3];
        for (var r = 0; r < 3; r++)
        {
            var row = grid.Where(i => cells[i.Key].Row == r).ToList();
            if (row.Count == 0) continue;
            rowY[r] = y + row.Max(i => i.U);
            y = rowY[r] + row.Max(i => i.D) + Gap;
            gridBottom = y - Gap;
        }
        foreach (var i in grid) at[i.Key] = (colX[cells[i.Key].Col], rowY[cells[i.Key].Row]);
        // The sections in a row under the grid.
        var sx = fx0 + Inset;
        var sy = (grid.Count > 0 ? gridBottom + Gap : fy0 + Inset) + (sections.Count > 0 ? sections.Max(i => i.U) : 0);
        foreach (var s in sections) { at[s.Key] = (sx + s.L, sy); sx += s.L + s.R + Gap; }
        var sectionsBottom = sections.Count > 0 ? sy + sections.Max(i => i.D) : grid.Count > 0 ? gridBottom : fy0 + Inset;
        bool Inside((double X0, double Y0, double X1, double Y1) b) =>
            b.X0 >= fx0 + 2 && b.Y0 >= fy0 + 2 && b.X1 <= fx1 - 2 && b.Y1 <= fy1 - 2
            && reserved.All(r => !(b.X1 > r.X0 && b.X0 < r.X1 && b.Y1 > r.Y0 && b.Y0 < r.Y1));
        (double, double, double, double) Box(Item i, (double Cx, double Cy) c) => (c.Cx - i.L, c.Cy - i.U, c.Cx + i.R, c.Cy + i.D);
        static bool Overlap((double X0, double Y0, double X1, double Y1) a, (double X0, double Y0, double X1, double Y1) b) =>
            a.X1 > b.X0 && a.X0 < b.X1 && a.Y1 > b.Y0 && a.Y0 < b.Y1;
        if (iso != null)
        {
            // At the top right of the grid; else after the sections; else under everything.
            var tries = new List<(double, double)>
            {
                (Math.Max(gridRight, sections.Count > 0 ? sx - Gap : gridRight) + Gap + iso.L, fy0 + Inset + iso.U),
                (sx + iso.L, sy - (sections.Count > 0 ? sections.Max(i => i.U) : 0) + iso.U),
                (fx0 + Inset + iso.L, sectionsBottom + Gap + iso.U),
            };
            var placed = false;
            foreach (var t in tries)
            {
                var b = Box(iso, t);
                if (Inside(b) && items.Where(i => i != iso && at.ContainsKey(i.Key)).All(i => !Overlap(b, Box(i, at[i.Key])))) { at[iso.Key] = t; placed = true; break; }
            }
            if (!placed) at[iso.Key] = tries[^1];
        }
        var fits = items.All(i => Inside(Box(i, at[i.Key])));
        var slots = items.Select(i =>
        {
            var (cx, cy) = at[i.Key];
            // The viewport's box: the drawing and its dimensions; its title goes under it.
            return new Slot(i.Key, i.Role, i.Station, i.Scale, cx, cy, cx - i.L, cy - i.U, cx + i.R, cy + i.D - TitleRoom);
        }).ToList();
        result = new Arrangement(paper, scale, slots, (bx1 - dataW / 2, by0 - 2 - dataH / 2, dataW), fits);
        return fits;
    }

    // ------------------------------------------------------------ the part's data

    /// <summary>A token of a shop drawing's texts: {mark}, {name}, {kind}, {type}, {material}, {length}, {width}, {height}, {weight}, {qty}.</summary>
    public static string Token(IQueryContext q, Entity e, ElementFrame? f, string key) => key switch
    {
        "mark" => Marks.MarkOf(q, e) ?? e.Name,
        "name" => e.Name,
        "kind" => q.Describe(e.Class)?.Label ?? e.Class,
        "type" => q.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.Name ?? "",
        "material" => string.Join(", ", (q.Geometry(e.Id)?.Parts ?? []).Select(p => p.Material).OfType<string>().Distinct().Select(id => q.Model.Get(id)?.Name ?? id)),
        "length" => f is { } fl ? Mm(fl.Length) : "",
        "width" => f is { } fw ? Mm(fw.Width) : "",
        "height" => f is { } fh ? Mm(fh.Height) : "",
        "weight" => q.Quantities(e.Id).SelectMany(s => s.Quantities).FirstOrDefault(x => x.Kind == "WEIGHT") is { } w ? $"{w.Value.ToString("0.0", CultureInfo.InvariantCulture)} kg" : "",
        "qty" => Marks.CountOf(q, e).ToString(CultureInfo.InvariantCulture),
        _ => "",
    };

    static string Mm(double m) => Math.Round(m * 1000).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>A text with its {tokens} filled for the element.</summary>
    public static string Fill(IQueryContext q, Entity e, ElementFrame? f, string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\{([a-z]+)\}", mt => Token(q, e, f, mt.Groups[1].Value)).Trim();

    /// <summary>The part's data a single part's drawing shows over its title block: label and value, empty ones left out.</summary>
    public static List<(string Label, string Value)> DataRows(IQueryContext q, Entity e)
    {
        var f = ElementFrames.Of(q, e);
        var o = new List<(string, string)>
        {
            ("MARK", Token(q, e, f, "mark")), ("KIND", Token(q, e, f, "kind")), ("TYPE", Token(q, e, f, "type")), ("MATERIAL", Token(q, e, f, "material")),
            ("SIZE", f is { } ff ? $"{Mm(ff.Length)} × {Mm(ff.Width)} × {Mm(ff.Height)}" : ""), ("WEIGHT", Token(q, e, f, "weight")), ("QUANTITY", Token(q, e, f, "qty")),
        };
        return [.. o.Where(r => r.Item2.Length > 0)];
    }

    // ------------------------------------------------------------ making it

    /// <summary>What a drawing is made of: the drawing, its sheet and its views as they will be, and the views that go.</summary>
    public sealed record Made(Entity Drawing, Entity Sheet, List<Entity> Views, List<string> Gone, Arrangement Arrangement);

    /// <summary>The drawing of an element kept already (by its element and rule), if any.</summary>
    public static Entity? Existing(IModelView m, string element, string rule) =>
        m.OfClass(Class).FirstOrDefault(d => d.Str("element") == element && (d.Str("rule") ?? Generic.Id) == rule);

    /// <summary>
    /// The drawing of <paramref name="e"/> as it will be, its ids from <paramref name="newId"/> for what is new: the views the options
    /// ask for (those it had kept, with the user's own settings on them, their role's fields set again), the sheet laid out again.
    /// </summary>
    public static Made Make(IQueryContext q, Entity e, JsonObject a, Func<string> newId, Entity? existing = null)
    {
        var m = q.Model;
        var rule = RuleFor(m, q.Services, e, (string?)a["rule"] ?? existing?.Str("rule"));
        existing ??= Existing(m, e.Id, rule.Id);
        var opts = OptionsFrom(m, rule, a, existing?.Attrs["options"] as JsonObject);
        var frame = ElementFrames.Of(q, e) ?? throw new CommandException($"„{e.Name}” has nothing to draw.");
        var keptViews = existing?.Attrs["views"] as JsonObject ?? [];
        var oldSheet = m.Get(existing?.Str("sheet") ?? "") is { Class: Sheets.Class } s ? s : null;
        var sheetProto = oldSheet ?? new Entity("", Sheets.Class, "", new JsonObject { ["titleBlock"] = new JsonObject(), ["viewports"] = new JsonArray() });
        var drawingId = existing?.Id ?? newId();
        var rows = DataRows(q, e).Count;
        // Each view's name: the one it has (kept by its key: its role, a section's place in the list), else the part's and its role's.
        var names = new Dictionary<string, string>();
        foreach (var v in opts.Views) names[v] = $"{e.Name} · {ElementViews.Title(v)}";
        for (var i = 0; i < opts.Sections.Count; i++) names[$"{opts.Sections[i].Role}#{i}"] = $"{e.Name} · Section {(char)('A' + i)}–{(char)('A' + i)}";
        names[ElementViews.Iso] = $"{e.Name} · {ElementViews.Title(ElementViews.Iso)}";
        foreach (var key in names.Keys.ToList())
            if ((string?)keptViews[key] is { } kid && m.Get(kid) is { Class: Views.Class } kv) names[key] = kv.Name;
        var arr = Arrange(m, sheetProto, frame, opts, rule.PaperList, rows, key => names.GetValueOrDefault(key, key));
        var views = new List<Entity>();
        var byKey = new JsonObject();
        foreach (var slot in arr.Slots)
        {
            var args = new JsonObject { ["scale"] = slot.Scale };
            if (slot.Station is { } st) args["station"] = st;
            var fresh = ElementViews.Attrs(q, e, slot.Role, args);
            if (opts.Dims != "none" && slot.Role != ElementViews.Iso) ((JsonObject)fresh[ElementViews.Key]!)["dims"] = opts.Dims;
            fresh["discipline"] = Discipline;
            fresh["shopDrawing"] = drawingId;
            if ((string?)keptViews[slot.Key] is { } vid && m.Get(vid) is { Class: Views.Class } had)
            {
                // Kept: the user's own settings (hidden elements, graphics, crop, line edits, a name of their own) stay; the role's fields are set again.
                var attrs = had.CloneAttrs();
                foreach (var key in new[] { "kind", ElementViews.Key, "scale", "discipline", "shopDrawing" }) attrs[key] = fresh[key]?.DeepClone();
                if (fresh["hiddenLines"] != null && attrs["hiddenLines"] == null) attrs["hiddenLines"] = fresh["hiddenLines"]!.DeepClone();
                if (attrs["styleOverrides"] == null) attrs["styleOverrides"] = fresh["styleOverrides"]!.DeepClone();
                views.Add(had.With(attrs: attrs));
            }
            else views.Add(new Entity(newId(), Views.Class, names[slot.Key], fresh));
            byKey[slot.Key] = views[^1].Id;
        }
        var gone = keptViews.Where(kv => !byKey.ContainsKey(kv.Key) && (string?)kv.Value is { } id && m.Get(id) != null).Select(kv => (string)kv.Value!).ToList();
        // The sheet: its paper, landscape; the drawing's viewports where the layout puts them (kept by their view), the user's own others kept.
        var sheetId = oldSheet?.Id ?? newId();
        var sheetAttrs = oldSheet?.CloneAttrs() ?? new JsonObject
        {
            ["number"] = Sheets.NextNumber(m), ["titleBlock"] = new JsonObject(), ["viewports"] = new JsonArray(), ["discipline"] = Discipline,
        };
        sheetAttrs["paper"] = arr.Paper;
        sheetAttrs["orientation"] = "landscape";
        sheetAttrs["shopDrawing"] = drawingId;
        var oldVps = Sheets.Viewports(oldSheet ?? sheetProto);
        var ours = new HashSet<string>([.. views.Select(v => v.Id), .. gone, drawingId]);
        var vps = new List<SheetViewport>();
        for (var i = 0; i < arr.Slots.Count; i++)
        {
            var slot = arr.Slots[i];
            var v = views[i];
            var k = 1000 / slot.Scale;
            var (bx, by) = ((slot.X0 + slot.X1) / 2, (slot.Y0 + slot.Y1) / 2);
            var center = new Vec2((bx - slot.Cx) / k, -(by - slot.Cy) / k);
            var vpId = oldVps.FirstOrDefault(x => x.ViewId == v.Id)?.Id ?? newId();
            vps.Add(new SheetViewport(vpId, v.Id, Math.Round(bx, 3), Math.Round(by, 3), Math.Round(slot.X1 - slot.X0, 3), Math.Round(slot.Y1 - slot.Y0, 3), new Vec2(Math.Round(center.X, 6), Math.Round(center.Y, 6))));
        }
        // The part's data over the title block.
        var dataVp = oldVps.FirstOrDefault(x => x.ViewId == drawingId)?.Id ?? newId();
        vps.Add(new SheetViewport(dataVp, drawingId, Math.Round(arr.Data.X, 3), Math.Round(arr.Data.Y, 3), Math.Round(arr.Data.W, 3), 0));
        vps.AddRange(oldVps.Where(x => !ours.Contains(x.ViewId)));
        sheetAttrs["viewports"] = new JsonArray(vps.Select(x => (JsonNode)x.ToJson()).ToArray());
        // The sheet's name: the rule's ("{kind} {mark}") as the part is now, unless the user gave it one of their own.
        var autoName = Fill(q, e, frame, rule.SheetName);
        var renamed = oldSheet is { } os && os.Name.Length > 0 && os.Str("autoName") is { } an && an != os.Name;
        var sheetName = renamed ? oldSheet!.Name : autoName;
        sheetAttrs["autoName"] = autoName;
        var sheet = new Entity(sheetId, Sheets.Class, sheetName, sheetAttrs);
        var drawingAttrs = new JsonObject
        {
            ["element"] = e.Id, ["rule"] = rule.Id, ["options"] = opts.ToJson(), ["sheet"] = sheetId, ["views"] = byKey,
            ["paper"] = arr.Paper, ["scale"] = arr.Scale, ["fits"] = arr.Fits,
        };
        var drawing = new Entity(drawingId, Class, $"{Fill(q, e, frame, "{kind} {mark}")}", drawingAttrs);
        return new Made(drawing, sheet, views, gone, arr);
    }

    /// <summary>The drawing made in the command's step: what is new added, what was kept replaced, the views no longer asked for removed with their drafting.</summary>
    public static Made Apply(ICommandContext ctx, Entity e, JsonObject a, Entity? existing = null)
    {
        var made = Make(ctx.Query, e, a, ctx.NewId, existing);
        void Put(Entity x) { if (ctx.Model.Get(x.Id) != null) ctx.Replace(x); else ctx.Add(x); }
        foreach (var v in made.Views) Put(v);
        Put(made.Sheet);
        Put(made.Drawing);
        foreach (var id in made.Gone) RemoveView(ctx, id);
        return made;
    }

    static void RemoveView(ICommandContext ctx, string viewId)
    {
        foreach (var x in ctx.Model.Entities.Where(x => x.Str("viewId") == viewId).Select(x => x.Id).ToList()) ctx.Remove(x);
        if (ctx.Model.Get(viewId) != null) ctx.Remove(viewId);
    }

    static JsonObject Result(Made made) => new()
    {
        ["id"] = made.Drawing.Id, ["sheet"] = made.Sheet.Id, ["paper"] = made.Arrangement.Paper, ["scale"] = made.Arrangement.Scale, ["fits"] = made.Arrangement.Fits,
        ["views"] = new JsonObject(made.Arrangement.Slots.Select((s, i) => KeyValuePair.Create(s.Key, (JsonNode?)made.Views[i].Id))),
    };

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Shop drawing",
        [
            new("element", "Element", "text", ReadOnly: true),
            new("rule", "Rule", "text", ReadOnly: true),
        ], "#555555", Kind: "view"));
        r.Service<ISheetContent>(new PartData());

        // A part's drawing: {element, rule?, views?: [front, top…], sections?: [{role, station}], iso?, scale? (null: auto), paper? (null:
        // the first that fits), projection? (iso-e, third-angle), dims? (overall, none), prefer? (paper, scale)} → {id, sheet, views, paper, scale}.
        // Made again (the same element and rule): its sheet and views keep their ids, the user's edits on them stay. One step.
        r.Command("drawing.create", "Shop drawing", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["element"] ?? "") ?? throw new CommandException("Which element is the drawing of?");
            if (e.Class is Views.Class or Sheets.Class or Class) throw new CommandException("A drawing is of an element of the model.");
            return Result(Apply(ctx, e, a));
        });

        // A drawing made again with other choices: {id, …the same as drawing.create}.
        r.Command("drawing.update", "Update shop drawing", (ctx, a) =>
        {
            var d = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("There is no such drawing.");
            var e = ctx.Model.Get(d.Str("element") ?? "") ?? throw new CommandException("The drawing's element is gone.");
            return Result(Apply(ctx, e, a, d));
        });

        // A drawing removed with its sheet and its views: {id}.
        r.Command("drawing.remove", "Remove shop drawing", (ctx, a) =>
        {
            var d = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("There is no such drawing.");
            foreach (var (_, v) in d.Attrs["views"] as JsonObject ?? []) if ((string?)v is { } vid) RemoveView(ctx, vid);
            if (d.Str("sheet") is { } sid && ctx.Model.Get(sid) != null) ctx.Remove(sid);
            ctx.Remove(d.Id);
            return null;
        });

        // The sheet a drawing would make, drawn by the engine, nothing kept: {the same as drawing.create} → the page (as sheet.drawing) and its paper and scale.
        r.Query("drawing.preview", (q, a) =>
        {
            var e = q.Model.Get((string?)a["element"] ?? "") ?? throw new CommandException("Which element is the drawing of?");
            var n = 0;
            var existing = q.Model.Get((string?)a["id"] ?? "") is { Class: Class } d ? d : null;
            var made = Make(q, e, a, () => $"preview:{++n}", existing);
            var overlay = new OverlayQuery(q, new OverlayModel(q.Model, [.. made.Views, made.Sheet, made.Drawing], made.Gone));
            var placements = Sheets.Place(overlay, made.Sheet);
            var page = Sheets.PageJson(overlay.Model, Sheets.Compose(overlay, made.Sheet, placements), made.Sheet, placements);
            page["paper"] = made.Arrangement.Paper;
            page["scale"] = made.Arrangement.Scale;
            page["fits"] = made.Arrangement.Fits;
            page["rule"] = made.Drawing.Str("rule");
            page["options"] = made.Drawing.Attrs["options"]?.DeepClone();
            return page;
        });

        // The rules an element may be drawn by (its own first) and the drawing it has: {id} → {rules: [{id, name}], rule, drawing}.
        r.Query("drawing.rules", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("There is no such element.");
            var own = RuleFor(q.Model, q.Services, e);
            return new JsonObject
            {
                ["rule"] = own.Id,
                ["rules"] = new JsonArray(Rules(q.Services).Select(x => (JsonNode)new JsonObject
                {
                    ["id"] = x.Id, ["name"] = x.Name, ["views"] = new JsonArray(x.Views.Select(v => (JsonNode)v).ToArray()), ["iso"] = x.Iso, ["dims"] = x.Dimensions,
                    ["sections"] = new JsonArray(x.Sections.Select(s => (JsonNode)new JsonObject { ["role"] = s.Role, ["station"] = s.Station }).ToArray()),
                }).ToArray()),
                ["drawing"] = q.Model.OfClass(Class).FirstOrDefault(d => d.Str("element") == e.Id)?.Id,
                ["projection"] = ProjectionOf(q.Model),
                ["frame"] = ElementFrames.Of(q, e)?.ToJson(),
            };
        });
    }

    /// <summary>The part's data as a sheet shows it (a shop drawing's viewport of the drawing itself): a table of label and value.</summary>
    sealed class PartData : ISheetContent
    {
        public bool Handles(Entity e) => e.Class == Class;

        public IReadOnlyList<(double W, double H, List<Pen> Pens)> Pages(IQueryContext q, Entity e, double maxWidth, double? maxHeight)
        {
            var rows = q.Model.Get(e.Str("element") ?? "") is { } el ? DataRows(q, el) : [("ELEMENT", "—")];
            var w = Math.Min(maxWidth, DataWidth);
            var h = DataHeight(rows.Count);
            var rules = new Pen("TITLE", "#000000", 0.25, null, null);
            var text = new Pen("TEXT", "#000000", 0, null, null);
            rules.Segments.AddRange([(new(0, 0), new(w, 0)), (new(w, 0), new(w, h)), (new(w, h), new(0, h)), (new(0, h), new(0, 0)), (new(DataLabel, 0), new(DataLabel, h))]);
            for (var i = 0; i < rows.Count; i++)
            {
                var y = 1 + i * DataRow;
                if (i > 0) rules.Segments.Add((new(0, y), new(w, y)));
                text.Texts.Add(new PenText(new Vec2(2, y + 3.6), rows[i].Label, 1.8, Anchor: "start"));
                text.Texts.Add(new PenText(new Vec2(DataLabel + 2, y + 3.8), rows[i].Value, 2.8, Anchor: "start", Bold: i == 0));
            }
            return [(w, h, [rules, text])];
        }
    }
}
