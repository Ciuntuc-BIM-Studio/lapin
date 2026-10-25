using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Something other than a view a sheet can show (a schedule): its pages laid
/// out in paper millimetres (y down from the top-left), at most as wide as asked.
/// </summary>
public interface ISheetContent
{
    bool Handles(Entity e);
    /// <summary>Its pages, each with its size and pens; <paramref name="maxWidth"/> and <paramref name="maxHeight"/> (paper mm) wrap and page it.</summary>
    IReadOnlyList<(double W, double H, List<Pen> Pens)> Pages(IQueryContext q, Entity e, double maxWidth, double? maxHeight);
}

/// <summary>A view on a sheet: where its centre sits on the paper (mm, from the top-left), its box (0 × 0: fits the drawing), the drawing point at its centre (null: the drawing's centre).</summary>
public sealed record SheetViewport(string Id, string ViewId, double X, double Y, double Width = 0, double Height = 0, Vec2? Center = null, int Page = 1)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["id"] = Id, ["viewId"] = ViewId, ["x"] = X, ["y"] = Y, ["width"] = Width, ["height"] = Height };
        if (Center is { } c) o["center"] = new JsonArray(c.X, c.Y);
        if (Page > 1) o["page"] = Page;
        return o;
    }

    public static SheetViewport? From(JsonNode? n) => n is JsonObject o && (string?)o["id"] is { } id && (string?)o["viewId"] is { } v
        ? new SheetViewport(id, v, Json.Number(o["x"]) ?? 0, Json.Number(o["y"]) ?? 0, Json.Number(o["width"]) ?? 0, Json.Number(o["height"]) ?? 0,
            o["center"] is JsonArray c && c.Count >= 2 ? new Vec2(Json.Number(c[0]) ?? 0, Json.Number(c[1]) ?? 0) : null,
            (int)(Json.Number(o["page"]) ?? 1))
        : null;
}

/// <summary>
/// Sheets (the old Lapin's, part of core): paper A0–A4 in either orientation,
/// a frame 10 mm in, a 180 × 50 mm title block, and views placed on them at
/// their own scale. A sheet is a <c>LapinSheet</c> entity (not exported to
/// IFC). Paper millimetres from the top-left corner; a viewport maps the
/// view's metres to paper by its scale (1:N: 1 m is 1000 / N mm).
/// </summary>
public static class Sheets
{
    public const string Class = "LapinSheet";
    public static readonly Dictionary<string, (double W, double H)> Papers = new()
    {
        ["A0"] = (1189, 841), ["A1"] = (841, 594), ["A2"] = (594, 420), ["A3"] = (420, 297), ["A4"] = (297, 210),
    };
    public static readonly int[] Scales = [1, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000];
    public const double Margin = 10;
    public static readonly (double W, double H) TitleBlock = (180, 50);
    /// <summary>Paper pens: the frame and title block rules (mm).</summary>
    const double FramePen = 0.5, RulePen = 0.25;

    public static (double W, double H) PaperOf(Entity sheet)
    {
        var (w, h) = Papers.GetValueOrDefault(sheet.Str("paper") ?? "A1", Papers["A1"]);
        return sheet.Str("orientation") == "portrait" ? (h, w) : (w, h);
    }

    public static List<SheetViewport> Viewports(Entity sheet) =>
        sheet.Attrs["viewports"] is JsonArray a ? a.Select(SheetViewport.From).OfType<SheetViewport>().ToList() : [];

    /// <summary>A viewport box that fits a drawing's extent at its scale, plus a 10 mm margin (paper mm).</summary>
    public static (double W, double H) Fit((Vec2 Min, Vec2 Max) bounds, double scale)
    {
        var k = 1000 / scale;
        return (Math.Max(40, (bounds.Max.X - bounds.Min.X) * k + 20), Math.Max(30, (bounds.Max.Y - bounds.Min.Y) * k + 20));
    }

    /// <summary>A sheet's viewport as placed: its box on the paper and the transform from the view's metres.</summary>
    public sealed record Placement(SheetViewport Viewport, Entity View, Views.Rendered Drawing, double X0, double Y0, double X1, double Y1, Func<Vec2, Vec2> ToPaper, bool Content = false, int Pages = 1);

    /// <summary>The frame's inner width and height on a sheet, less the title block's height (what a schedule pages into).</summary>
    public static (double W, double H) FrameSize(Entity sheet)
    {
        var (w, h) = PaperOf(sheet);
        return (w - 2 * Margin - 4, h - 2 * Margin - TitleBlock.H - 4);
    }

    public static List<Placement> Place(IQueryContext q, Entity sheet)
    {
        var o = new List<Placement>();
        foreach (var vp in Viewports(sheet))
        {
            // A schedule (or any module's sheet content): its page, laid out in paper mm, centred on the viewport.
            if (q.Model.Get(vp.ViewId) is { } other && q.Services.All<ISheetContent>().FirstOrDefault(c => c.Handles(other)) is { } content)
            {
                var (fw, fh) = FrameSize(sheet);
                var wrap = vp.Width > 0 ? Math.Max(60, Math.Min(vp.Width, fw)) : fw;
                var pages = content.Pages(q, other, wrap, fh);
                var (pw, ph, pp) = vp.Page - 1 < pages.Count ? pages[vp.Page - 1] : (40, 10, new List<Pen>());
                var bw = Math.Max(pw, vp.Width > 0 ? vp.Width : 0);
                var (x0, y0) = (vp.X - bw / 2, vp.Y - ph / 2);
                Vec2 Shift(Vec2 p) => new(p.X + x0, p.Y + y0);
                o.Add(new Placement(vp, other, new Views.Rendered(pp, [], null, 1000, "content", null, []), x0, y0, x0 + bw, y0 + ph, Shift, true, pages.Count));
                continue;
            }
            if (Views.Find(q.Model, vp.ViewId) is not { } view) continue;
            var r = Views.Render(q, view, print: true);
            var k = 1000 / r.Scale;
            var (w, h) = vp.Width > 0 && vp.Height > 0 ? (vp.Width, vp.Height) : r.Bounds is { } b ? Fit(b, r.Scale) : (120, 80);
            var c = vp.Center ?? (r.Bounds is { } bb ? (bb.Min + bb.Max) * 0.5 : new Vec2(0, 0));
            Vec2 ToPaper(Vec2 p) => new(vp.X + (p.X - c.X) * k, vp.Y - (p.Y - c.Y) * k);
            o.Add(new Placement(vp, view, r, vp.X - w / 2, vp.Y - h / 2, vp.X + w / 2, vp.Y + h / 2, ToPaper));
        }
        return o;
    }

    // ------------------------------------------------------------ clipping to a viewport's box

    static bool ClipSegment(ref Vec2 a, ref Vec2 b, double x0, double y0, double x1, double y1)
    {
        double t0 = 0, t1 = 1, dx = b.X - a.X, dy = b.Y - a.Y;
        foreach (var (p, q) in new[] { (-dx, a.X - x0), (dx, x1 - a.X), (-dy, a.Y - y0), (dy, y1 - a.Y) })
        {
            if (Math.Abs(p) < 1e-15) { if (q < 0) return false; continue; }
            var t = q / p;
            if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
            else { if (t < t0) return false; if (t < t1) t1 = t; }
        }
        var a0 = a;
        a = new Vec2(a0.X + dx * t0, a0.Y + dy * t0);
        b = new Vec2(a0.X + dx * t1, a0.Y + dy * t1);
        return true;
    }

    static List<Vec2> ClipLoop(IReadOnlyList<Vec2> loop, double x0, double y0, double x1, double y1)
    {
        var pts = loop.ToList();
        foreach (var (inside, cut) in new (Func<Vec2, bool>, Func<Vec2, Vec2, Vec2>)[]
        {
            (p => p.X >= x0, (a, b) => a + (b - a) * ((x0 - a.X) / (b.X - a.X))),
            (p => p.X <= x1, (a, b) => a + (b - a) * ((x1 - a.X) / (b.X - a.X))),
            (p => p.Y >= y0, (a, b) => a + (b - a) * ((y0 - a.Y) / (b.Y - a.Y))),
            (p => p.Y <= y1, (a, b) => a + (b - a) * ((y1 - a.Y) / (b.Y - a.Y))),
        })
        {
            if (pts.Count == 0) break;
            var o = new List<Vec2>();
            for (var i = 0; i < pts.Count; i++)
            {
                var (cur, prev) = (pts[i], pts[(i + pts.Count - 1) % pts.Count]);
                if (inside(cur)) { if (!inside(prev)) o.Add(cut(prev, cur)); o.Add(cur); }
                else if (inside(prev)) o.Add(cut(prev, cur));
            }
            pts = o;
        }
        return pts;
    }

    // ------------------------------------------------------------ the sheet on paper

    /// <summary>A sheet as paper pens: the views clipped to their boxes with their titles, the frame and the title block (its views placed here unless given).</summary>
    public static PaperPage Compose(IQueryContext q, Entity sheet, List<Placement>? placed = null)
    {
        var (W, H) = PaperOf(sheet);
        var pens = new List<Pen>();
        var placements = placed ?? Place(q, sheet);
        foreach (var pl in placements)
            foreach (var pen in pl.Drawing.Pens)
            {
                var o = new Pen(pen.Layer, pen.Stroke, pen.Width, pen.Fill, pen.Dash);
                foreach (var (a, b) in pen.Segments)
                {
                    var (pa, pb) = (pl.ToPaper(a), pl.ToPaper(b));
                    if (ClipSegment(ref pa, ref pb, pl.X0, pl.Y0, pl.X1, pl.Y1)) o.Segments.Add((pa, pb));
                }
                foreach (var loop in pen.Loops)
                    if (ClipLoop(loop.Select(pl.ToPaper).ToList(), pl.X0, pl.Y0, pl.X1, pl.Y1) is { Count: >= 3 } c) o.Loops.Add(c);
                foreach (var t in pen.Texts)
                {
                    var at = pl.ToPaper(t.At);
                    if (at.X >= pl.X0 && at.X <= pl.X1 && at.Y >= pl.Y0 && at.Y <= pl.Y1) o.Texts.Add(t with { At = at });
                }
                if (o.Segments.Count + o.Loops.Count + o.Texts.Count > 0) pens.Add(o);
            }

        var frame = new Pen("FRAME", "#000000", FramePen, null, null);
        var rules = new Pen("TITLE", "#000000", RulePen, null, null);
        var text = new Pen("TEXT", "#000000", 0, null, null);
        void Rect(Pen p, double x0, double y0, double x1, double y1) =>
            p.Segments.AddRange([(new(x0, y0), new(x1, y0)), (new(x1, y0), new(x1, y1)), (new(x1, y1), new(x0, y1)), (new(x0, y1), new(x0, y0))]);
        Rect(frame, Margin, Margin, W - Margin, H - Margin);

        // View titles: the view's name over a rule, its scale beside, under the box's lower-left corner.
        foreach (var pl in placements.Where(p => !p.Content))
        {
            var y = pl.Y1 + 7;
            text.Texts.Add(new PenText(new Vec2(pl.X0, y), pl.View.Name, 3.5, Anchor: "start", Bold: true));
            var nameWidth = PdfWriter.Advance(pl.View.Name, true) * 3.5 / 0.718;
            rules.Segments.Add((new(pl.X0, y + 1.5), new(pl.X0 + Math.Max(nameWidth, 30) + 4, y + 1.5)));
            text.Texts.Add(new PenText(new Vec2(pl.X0 + nameWidth + 4, y), $"1:{pl.Drawing.Scale:0}", 2.5, Anchor: "start"));
        }

        // The title block: project, sheet, then number, scale, date and drawn by.
        var (bw, bh) = TitleBlock;
        double bx0 = W - Margin - bw, by0 = H - Margin - bh, bx1 = W - Margin, by1 = H - Margin;
        Rect(frame, bx0, by0, bx1, by1);
        rules.Segments.Add((new(bx0, by0 + 20), new(bx1, by0 + 20)));
        rules.Segments.Add((new(bx0, by0 + 35), new(bx1, by0 + 35)));
        for (var i = 1; i < 4; i++) rules.Segments.Add((new(bx0 + i * bw / 4, by0 + 35), new(bx0 + i * bw / 4, by1)));
        var tb = sheet.Attrs["titleBlock"] as JsonObject;
        string Field(string k, string fallback = "") => (string?)tb?[k] is { Length: > 0 } v ? v : fallback;
        var projectName = Field("project", Spatial.Project(q.Model)?.Name ?? "Project");
        text.Texts.Add(new PenText(new Vec2(bx0 + 3, by0 + 4.5), "PROJECT", 1.8, Anchor: "start"));
        text.Texts.Add(new PenText(new Vec2(bx0 + 3, by0 + 15), projectName, 5, Anchor: "start", Bold: true));
        text.Texts.Add(new PenText(new Vec2(bx0 + 3, by0 + 24.5), "SHEET", 1.8, Anchor: "start"));
        text.Texts.Add(new PenText(new Vec2(bx0 + 3, by0 + 32), sheet.Name, 4, Anchor: "start", Bold: true));
        var scales = placements.Where(p => !p.Content).Select(p => p.Drawing.Scale).Distinct().OrderBy(x => x).Select(s => $"1:{s:0}").ToList();
        var cells = new[]
        {
            ("NUMBER", sheet.Str("number") ?? ""),
            ("SCALE", scales.Count == 0 ? "—" : string.Join(", ", scales)),
            ("DATE", Field("date", DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture))),
            ("DRAWN BY", Field("drawnBy")),
        };
        for (var i = 0; i < 4; i++)
        {
            var x = bx0 + i * bw / 4 + 3;
            text.Texts.Add(new PenText(new Vec2(x, by0 + 39.5), cells[i].Item1, 1.8, Anchor: "start"));
            text.Texts.Add(new PenText(new Vec2(x, by0 + 46.5), cells[i].Item2, i == 0 ? 4 : 3, Anchor: "start", Bold: i == 0));
        }
        pens.AddRange([frame, rules, text]);
        return new PaperPage(W, H, pens, sheet.Name);
    }

    static JsonObject PageJson(PaperPage page, List<Placement> placements)
    {
        var (W, H) = (page.W, page.H);
        var (bw, bh) = TitleBlock;
        double bx0 = W - Margin - bw, by0 = H - Margin - bh, bx1 = W - Margin, by1 = H - Margin;
        // What runs past the frame or over the title block (paper mm, a little slack).
        JsonArray Warnings(Placement p)
        {
            var o = new JsonArray();
            if (p.X0 < Margin - 0.5 || p.Y0 < Margin - 0.5 || p.X1 > W - Margin + 0.5 || p.Y1 > H - Margin + 0.5) o.Add((JsonNode)"outside");
            if (p.X1 > bx0 + 0.5 && p.X0 < bx1 - 0.5 && p.Y1 > by0 + 0.5 && p.Y0 < by1 - 0.5) o.Add((JsonNode)"titleBlock");
            return o;
        }
        return new JsonObject
        {
            ["w"] = W, ["h"] = H,
            ["frame"] = new JsonArray(Margin, Margin, W - Margin, H - Margin),
            ["titleBlock"] = new JsonArray(bx0, by0, bx1, by1),
            ["pens"] = new JsonArray(page.Pens.Select(p => (JsonNode)p.ToJson()).ToArray()),
            ["viewports"] = new JsonArray(placements.Select(p => (JsonNode)new JsonObject
            {
                ["id"] = p.Viewport.Id, ["viewId"] = p.Viewport.ViewId, ["x0"] = p.X0, ["y0"] = p.Y0, ["x1"] = p.X1, ["y1"] = p.Y1, ["scale"] = p.Drawing.Scale,
                ["content"] = p.Content, ["page"] = p.Viewport.Page, ["pages"] = p.Pages, ["width"] = p.Viewport.Width, ["warnings"] = Warnings(p),
            }).ToArray()),
        };
    }

    public static byte[] Pdf(IQueryContext q, IEnumerable<Entity> sheets) => PdfWriter.Write(sheets.Select(s => Compose(q, s)).ToList());

    public static byte[] Dxf(IQueryContext q, Entity sheet) => DxfWriter.Page(Compose(q, sheet), $"Lapin · {sheet.Str("number")} {sheet.Name}");

    // ------------------------------------------------------------ commands

    static Entity Require(IModelView m, string? id) => m.Get(id ?? "") is { Class: Class } s ? s : throw new CommandException("There is no such sheet.");

    static void Write(ICommandContext ctx, Entity sheet, List<SheetViewport> vps)
    {
        var attrs = sheet.CloneAttrs();
        attrs["viewports"] = new JsonArray(vps.Select(v => (JsonNode)v.ToJson()).ToArray());
        ctx.Replace(sheet.With(attrs: attrs));
    }

    /// <summary>The next sheet number after the last one: A-101 → A-102.</summary>
    static string NextNumber(IModelView m)
    {
        var last = m.OfClass(Class).Select(s => s.Str("number")).OfType<string>().LastOrDefault();
        if (last == null) return "A-101";
        var match = System.Text.RegularExpressions.Regex.Match(last, @"^(.*?)(\d+)$");
        return match.Success ? match.Groups[1].Value + (long.Parse(match.Groups[2].Value) + 1).ToString().PadLeft(match.Groups[2].Value.Length, '0') : last + "-2";
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Sheet",
        [
            new("number", "Number", "text"),
            new("paper", "Paper", "choice", Choices: [.. Papers.Keys]),
            new("orientation", "Orientation", "choice", Choices: ["landscape", "portrait"]),
            new("discipline", "Discipline", "text"),
        ], "#555555", Kind: "view"));

        r.Command("sheet.create", "New sheet", (ctx, a) =>
        {
            var paper = (string?)a["paper"] ?? "A1";
            if (!Papers.ContainsKey(paper)) throw new CommandException("A sheet is A0, A1, A2, A3 or A4.");
            var number = ((string?)a["number"])?.Trim() is { Length: > 0 } n ? n : NextNumber(ctx.Model);
            var sheet = new Entity(ctx.NewId(), Class, (string?)a["name"] ?? "Sheet", new JsonObject
            {
                ["number"] = number, ["paper"] = paper, ["orientation"] = (string?)a["orientation"] == "portrait" ? "portrait" : "landscape",
                ["titleBlock"] = new JsonObject(), ["viewports"] = new JsonArray(),
            });
            if ((string?)a["discipline"] is { Length: > 0 } discipline) sheet.Attrs["discipline"] = discipline;
            ctx.Add(sheet);
            return new JsonObject { ["id"] = sheet.Id };
        });

        // A copy of a sheet: its paper, title block and discipline, the next number, no views (a view sits on one sheet).
        r.Command("sheet.duplicate", "Duplicate sheet", (ctx, a) =>
        {
            var sheet = Require(ctx.Model, (string?)a["id"]);
            var attrs = sheet.CloneAttrs();
            attrs["number"] = NextNumber(ctx.Model);
            attrs["viewports"] = new JsonArray();
            var copy = new Entity(ctx.NewId(), Class, (string?)a["name"] is { Length: > 0 } nm ? nm : Views.CopyName(ctx.Model, sheet.Name, Class), attrs);
            ctx.Add(copy);
            return new JsonObject { ["id"] = copy.Id };
        });

        // A title block field (project, drawnBy, date, revision…); empty clears it.
        r.Command("sheet.titleBlock", "Title block", (ctx, a) =>
        {
            var sheet = Require(ctx.Model, (string?)a["id"]);
            var attrs = sheet.CloneAttrs();
            var tb = attrs["titleBlock"] as JsonObject ?? new JsonObject();
            foreach (var (k, v) in a["fields"] as JsonObject ?? [])
                if ((string?)v is { Length: > 0 } s) tb[k] = s; else tb.Remove(k);
            attrs["titleBlock"] = tb;
            ctx.Replace(sheet.With(attrs: attrs));
            return null;
        });

        // Place a view: where asked, else in the middle of the paper's free area. A view sits on one sheet, as in Revit.
        r.Command("sheet.addViewport", "Place view", (ctx, a) =>
        {
            var sheet = Require(ctx.Model, (string?)a["id"]);
            var viewId = (string?)a["viewId"] ?? throw new CommandException("Which view?");
            var page = (int)(Json.Number(a["page"]) ?? 1);
            var isContent = ctx.Model.Get(viewId) is { } ent && ctx.Services.All<ISheetContent>().Any(c => c.Handles(ent));
            if (Views.Find(ctx.Model, viewId) == null && !isContent) throw new CommandException("There is no such view.");
            if (ctx.Model.OfClass(Class).FirstOrDefault(s => Viewports(s).Any(v => v.ViewId == viewId && v.Page == page)) is { } on)
                throw new CommandException($"The view is on sheet {on.Str("number")} already.");
            var (W, H) = PaperOf(sheet);
            var vps = Viewports(sheet);
            var vp = new SheetViewport(ctx.NewId(), viewId, Json.Number(a["x"]) ?? (W - TitleBlock.W) / 2, Json.Number(a["y"]) ?? H / 2, Page: page);
            vps.Add(vp);
            Write(ctx, sheet, vps);
            return new JsonObject { ["viewportId"] = vp.Id };
        });

        r.Command("sheet.updateViewport", "Move view", (ctx, a) =>
        {
            var sheet = Require(ctx.Model, (string?)a["id"]);
            var vps = Viewports(sheet);
            var i = vps.FindIndex(v => v.Id == (string?)a["viewportId"]);
            if (i < 0) throw new CommandException("The sheet has no such view.");
            var v = vps[i];
            vps[i] = v with
            {
                X = Json.Number(a["x"]) ?? v.X, Y = Json.Number(a["y"]) ?? v.Y,
                Width = Json.Number(a["width"]) ?? v.Width, Height = Json.Number(a["height"]) ?? v.Height,
                Center = a.ContainsKey("center") ? a["center"] is JsonArray c && c.Count >= 2 ? new Vec2(Json.Number(c[0]) ?? 0, Json.Number(c[1]) ?? 0) : null : v.Center,
            };
            Write(ctx, sheet, vps);
            return null;
        });

        r.Command("sheet.removeViewport", "Remove view from sheet", (ctx, a) =>
        {
            var sheet = Require(ctx.Model, (string?)a["id"]);
            var vps = Viewports(sheet);
            if (vps.RemoveAll(v => v.Id == (string?)a["viewportId"]) == 0) throw new CommandException("The sheet has no such view.");
            Write(ctx, sheet, vps);
            return null;
        });

        r.Query("sheet.drawing", (q, a) =>
        {
            var sheet = Require(q.Model, (string?)a["id"]);
            // Every view rendered once, for the page and its viewports both.
            var placements = Place(q, sheet);
            return PageJson(Compose(q, sheet, placements), placements);
        });
    }
}
