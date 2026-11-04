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

/// <summary>
/// A view on a sheet: where its centre sits on the paper (mm, from the top-left), its box (0 × 0: fits the drawing), the drawing
/// point at its centre (null: the drawing's centre); or bound to a named <see cref="Boundary"/> of its view (decision 236), which
/// gives the box and the middle. A <see cref="Pinned"/> one stays where it is: not moved, nudged or cropped on the sheet.
/// </summary>
public sealed record SheetViewport(string Id, string ViewId, double X, double Y, double Width = 0, double Height = 0, Vec2? Center = null, int Page = 1, string? Boundary = null, bool Pinned = false)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["id"] = Id, ["viewId"] = ViewId, ["x"] = X, ["y"] = Y, ["width"] = Width, ["height"] = Height };
        if (Center is { } c) o["center"] = new JsonArray(c.X, c.Y);
        if (Page > 1) o["page"] = Page;
        if (Boundary != null) o["boundary"] = Boundary;
        if (Pinned) o["pinned"] = true;
        return o;
    }

    public static SheetViewport? From(JsonNode? n) => n is JsonObject o && (string?)o["id"] is { } id && (string?)o["viewId"] is { } v
        ? new SheetViewport(id, v, Json.Number(o["x"]) ?? 0, Json.Number(o["y"]) ?? 0, Json.Number(o["width"]) ?? 0, Json.Number(o["height"]) ?? 0,
            Args.TryPoint(o["center"]),
            (int)(Json.Number(o["page"]) ?? 1), (string?)o["boundary"], o["pinned"] is JsonValue pv && pv.TryGetValue<bool>(out var pinned) && pinned)
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
    public sealed record Placement(SheetViewport Viewport, Entity View, Views.Rendered Drawing, double X0, double Y0, double X1, double Y1, Func<Vec2, Vec2> ToPaper, bool Content = false, int Pages = 1)
    {
        /// <summary>The drawing point at the box's middle, and the named boundary it shows (decision 236), if any.</summary>
        public Vec2 Middle { get; init; }
        public Entity? Boundary { get; init; }
        /// <summary>Paper mm per drawing metre.</summary>
        public double K => 1000 / Drawing.Scale;
    }

    /// <summary>A viewport's boundary when it names one of its view's.</summary>
    static Entity? BoundaryOf(IModelView m, SheetViewport vp) =>
        vp.Boundary is { } bid && m.Get(bid) is { Class: ViewBoundaries.Class } b && b.Str("viewId") == vp.ViewId ? b : null;

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
            // Bound to a named boundary of its view: its box and middle are the boundary's.
            var boundary = BoundaryOf(q.Model, vp);
            if (boundary != null)
            {
                var (bmin, bmax) = ViewBoundaries.Box(boundary);
                (w, h, c) = ((bmax.X - bmin.X) * k, (bmax.Y - bmin.Y) * k, (bmin + bmax) * 0.5);
            }
            Vec2 ToPaper(Vec2 p) => new(vp.X + (p.X - c.X) * k, vp.Y - (p.Y - c.Y) * k);
            o.Add(new Placement(vp, view, r, vp.X - w / 2, vp.Y - h / 2, vp.X + w / 2, vp.Y + h / 2, ToPaper) { Middle = c, Boundary = boundary });
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

    /// <summary>An issue being made (docs/plan-cde.md): its revision, status, name and date, printed on the sheet's title block and revision table as issued.</summary>
    public sealed record Issuing(string Revision, string Status, string Name, string At);

    /// <summary>
    /// A sheet as paper pens: the views clipped to their boxes with their titles, the frame and the title block (its views placed here unless
    /// given); with <paramref name="issuing"/>, as that issue prints it (its revision and status in the title block, its row in the table).
    /// </summary>
    public static PaperPage Compose(IQueryContext q, Entity sheet, List<Placement>? placed = null, Issuing? issuing = null)
    {
        if (issuing != null)
        {
            var sa = sheet.CloneAttrs();
            var tbIssued = (sa["titleBlock"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
            tbIssued["revision"] = issuing.Revision;
            tbIssued["status"] = issuing.Status;
            sa["titleBlock"] = tbIssued;
            sheet = sheet.With(attrs: sa);
        }
        var (W, H) = PaperOf(sheet);
        var pens = new List<Pen>();
        var placements = placed ?? Place(q, sheet);
        foreach (var pl in placements)
            foreach (var pen in pl.Drawing.Pens)
            {
                var o = new Pen(pen.Layer, pen.Stroke, pen.Width, pen.Fill, pen.Dash) { Viewport = pl.Viewport.Id };
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
                // A picture keeps its corners and is clipped to the box where it is printed.
                foreach (var img in pen.Images) o.Images.Add(img with { Corners = [.. img.Corners.Select(pl.ToPaper)] });
                if (o.Images.Count > 0) o.Clip = (pl.X0, pl.Y0, pl.X1, pl.Y1);
                if (o.Segments.Count + o.Loops.Count + o.Texts.Count + o.Images.Count > 0) pens.Add(o);
            }

        var frame = new Pen("FRAME", "#000000", FramePen, null, null);
        var rules = new Pen("TITLE", "#000000", RulePen, null, null);
        var text = new Pen("TEXT", "#000000", 0, null, null);
        void Rect(Pen p, double x0, double y0, double x1, double y1) =>
            p.Segments.AddRange([(new(x0, y0), new(x1, y0)), (new(x1, y0), new(x1, y1)), (new(x1, y1), new(x0, y1)), (new(x0, y1), new(x0, y0))]);
        Rect(frame, Margin, Margin, W - Margin, H - Margin);

        // View titles: the view's name over a rule, its scale beside, under the box's lower-left corner; the view's own pens, so they move with it.
        foreach (var pl in placements.Where(p => !p.Content))
        {
            var y = pl.Y1 + 7;
            var title = new Pen("TEXT", "#000000", 0, null, null) { Viewport = pl.Viewport.Id };
            var rule = new Pen("TITLE", "#000000", RulePen, null, null) { Viewport = pl.Viewport.Id };
            var name = pl.Boundary != null ? $"{pl.View.Name} · {pl.Boundary.Name}" : pl.View.Name;
            title.Texts.Add(new PenText(new Vec2(pl.X0, y), name, 3.5, Anchor: "start", Bold: true));
            var nameWidth = PdfWriter.Advance(name, true) * 3.5 / 0.718;
            rule.Segments.Add((new(pl.X0, y + 1.5), new(pl.X0 + Math.Max(nameWidth, 30) + 4, y + 1.5)));
            title.Texts.Add(new PenText(new Vec2(pl.X0 + nameWidth + 4, y), $"1:{pl.Drawing.Scale:0}", 2.5, Anchor: "start"));
            pens.AddRange([rule, title]);
        }

        // The title block: project; sheet with its revision and status; then number, scale, date, drawn by and checked by.
        var (bw, bh) = TitleBlock;
        double bx0 = W - Margin - bw, by0 = H - Margin - bh, bx1 = W - Margin, by1 = H - Margin;
        Rect(frame, bx0, by0, bx1, by1);
        rules.Segments.Add((new(bx0, by0 + 20), new(bx1, by0 + 20)));
        rules.Segments.Add((new(bx0, by0 + 35), new(bx1, by0 + 35)));
        const int cellCount = 5;
        for (var i = 1; i < cellCount; i++) rules.Segments.Add((new(bx0 + i * bw / cellCount, by0 + 35), new(bx0 + i * bw / cellCount, by1)));
        var tb = sheet.Attrs["titleBlock"] as JsonObject;
        string Field(string k, string fallback = "") => (string?)tb?[k] is { Length: > 0 } v ? v : fallback;
        var projectName = Field("project", ProjectName(q));
        text.Texts.Add(new PenText(new Vec2(bx0 + 3, by0 + 4.5), "PROJECT", 1.8, Anchor: "start"));
        text.Texts.Add(new PenText(new Vec2(bx0 + 3, by0 + 15), projectName, 5, Anchor: "start", Bold: true));
        text.Texts.Add(new PenText(new Vec2(bx0 + 3, by0 + 24.5), "SHEET", 1.8, Anchor: "start"));
        text.Texts.Add(new PenText(new Vec2(bx0 + 3, by0 + 32), sheet.Name, 4, Anchor: "start", Bold: true));
        // The revision and the status (ISO 19650: P01…, S2…) at the right of the sheet's row, as the last issue set them.
        double rx = bx0 + bw - 54, sx = bx0 + bw - 27;
        rules.Segments.Add((new(rx, by0 + 20), new(rx, by0 + 35)));
        rules.Segments.Add((new(sx, by0 + 20), new(sx, by0 + 35)));
        text.Texts.Add(new PenText(new Vec2(rx + 3, by0 + 24.5), "REV", 1.8, Anchor: "start"));
        text.Texts.Add(new PenText(new Vec2(rx + 3, by0 + 32), Field("revision", "—"), 4, Anchor: "start", Bold: true));
        text.Texts.Add(new PenText(new Vec2(sx + 3, by0 + 24.5), "STATUS", 1.8, Anchor: "start"));
        text.Texts.Add(new PenText(new Vec2(sx + 3, by0 + 32), Field("status", "—"), 4, Anchor: "start", Bold: true));
        var scales = placements.Where(p => !p.Content).Select(p => p.Drawing.Scale).Distinct().OrderBy(x => x).Select(s => $"1:{s:0}").ToList();
        var cells = new[]
        {
            ("NUMBER", sheet.Str("number") ?? ""),
            ("SCALE", scales.Count == 0 ? "—" : string.Join(", ", scales)),
            ("DATE", Field("date", DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture))),
            ("DRAWN BY", Field("drawnBy")),
            ("CHECKED BY", Field("checkedBy")),
        };
        for (var i = 0; i < cellCount; i++)
        {
            var x = bx0 + i * bw / cellCount + 3;
            text.Texts.Add(new PenText(new Vec2(x, by0 + 39.5), cells[i].Item1, 1.8, Anchor: "start"));
            text.Texts.Add(new PenText(new Vec2(x, by0 + 46.5), cells[i].Item2, i == 0 ? 4 : 3, Anchor: "start", Bold: i == 0));
        }
        RevisionTable(q, sheet, frame, rules, text, bx0, by0, bw, issuing);
        pens.AddRange([frame, rules, text]);
        return new PaperPage(W, H, pens, sheet.Name, DraftingResources.Book(q.Model).LineCap);
    }

    /// <summary>
    /// The sheet's revision table (ISO 19650: the issues the sheet went out
    /// in), over the title block, newest last: revision, date, status, the
    /// issue's name. Nothing when the sheet was never issued.
    /// </summary>
    static void RevisionTable(IQueryContext q, Entity sheet, Pen frame, Pen rules, Pen text, double bx0, double by0, double bw, Issuing? issuing)
    {
        var issues = Cde.CdeIssues.OfSheet(q.Model, sheet.Id).Select(i => (i.Revision, i.At, i.Status, i.Name)).ToList();
        if (issuing != null) issues.Add((issuing.Revision, issuing.At, issuing.Status, issuing.Name));
        if (issues.Count == 0) return;
        const double row = 5;
        var top = by0 - (issues.Count + 1) * row;
        frame.Segments.AddRange([(new(bx0, top), new(bx0 + bw, top)), (new(bx0, top), new(bx0, by0)), (new(bx0 + bw, top), new(bx0 + bw, by0))]);
        double[] cols = [bx0 + 3, bx0 + 18, bx0 + 42, bx0 + 60];
        void Row(double y, string rev, string date, string status, string name, bool head)
        {
            text.Texts.Add(new PenText(new Vec2(cols[0], y), rev, head ? 1.8 : 2.5, Anchor: "start", Bold: !head));
            text.Texts.Add(new PenText(new Vec2(cols[1], y), date, head ? 1.8 : 2.5, Anchor: "start"));
            text.Texts.Add(new PenText(new Vec2(cols[2], y), status, head ? 1.8 : 2.5, Anchor: "start"));
            text.Texts.Add(new PenText(new Vec2(cols[3], y), name, head ? 1.8 : 2.5, Anchor: "start"));
        }
        Row(top + row - 1.3, "REV", "DATE", "STATUS", "DESCRIPTION", true);
        rules.Segments.Add((new(bx0, top + row), new(bx0 + bw, top + row)));
        for (var i = 0; i < issues.Count; i++)
        {
            var it = issues[i];
            var date = DateTime.TryParse(it.At, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var d) ? d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : it.At;
            Row(top + (i + 2) * row - 1.3, it.Revision, date, it.Status, it.Name, false);
        }
    }

    /// <summary>What a placed view breaks on the paper: "outside" (past the frame), "titleBlock" (over it); empty when it sits well (a little slack).</summary>
    public static List<string> Problems(Entity sheet, Placement p)
    {
        var (W, H) = PaperOf(sheet);
        var (bw, bh) = TitleBlock;
        double bx0 = W - Margin - bw, by0 = H - Margin - bh, bx1 = W - Margin, by1 = H - Margin;
        var o = new List<string>();
        if (p.X0 < Margin - 0.5 || p.Y0 < Margin - 0.5 || p.X1 > W - Margin + 0.5 || p.Y1 > H - Margin + 0.5) o.Add("outside");
        if (p.X1 > bx0 + 0.5 && p.X0 < bx1 - 0.5 && p.Y1 > by0 + 0.5 && p.Y0 < by1 - 0.5) o.Add("titleBlock");
        return o;
    }

    /// <summary>
    /// The project's name as a title block shows it when its project field is left empty: the IfcProject's name when the user gave one,
    /// else the project file's name ("House" for House.lapin), what the interface calls the project; "Project" before either exists.
    /// </summary>
    public static string ProjectName(IQueryContext q)
    {
        var own = Spatial.Project(q.Model)?.Name;
        if (own is { Length: > 0 } && own.Trim() != "Project") return own.Trim();
        return q.Title is { Length: > 0 } title ? title : own is { Length: > 0 } ? own : "Project";
    }

    /// <summary>
    /// A hash of what the sheet's views draw on the paper (their pens as
    /// placed, with their titles): the same while the drawing is the same,
    /// whatever the date or the title block say. An issue keeps it, so a later
    /// check tells a sheet changed since it went out (docs/plan-cde.md).
    /// </summary>
    public static string DrawingHash(IQueryContext q, Entity sheet)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var pen in Compose(q, sheet).Pens.Where(p => p.Viewport != null)) sb.Append(pen.ToJson().ToJsonString()).Append('\n');
        return Cde.CdeTransmittal.Sha256(sb.ToString());
    }

    static JsonObject PageJson(PaperPage page, Entity sheet, List<Placement> placements)
    {
        var (W, H) = (page.W, page.H);
        var (bw, bh) = TitleBlock;
        double bx0 = W - Margin - bw, by0 = H - Margin - bh, bx1 = W - Margin, by1 = H - Margin;
        return new JsonObject
        {
            ["w"] = W, ["h"] = H,
            ["frame"] = new JsonArray(Margin, Margin, W - Margin, H - Margin),
            ["titleBlock"] = new JsonArray(bx0, by0, bx1, by1),
            ["pens"] = new JsonArray(page.Pens.Select(p => (JsonNode)p.ToJson()).ToArray()),
            ["viewports"] = new JsonArray(placements.Select(p => (JsonNode)new JsonObject
            {
                ["id"] = p.Viewport.Id, ["viewId"] = p.Viewport.ViewId, ["x0"] = p.X0, ["y0"] = p.Y0, ["x1"] = p.X1, ["y1"] = p.Y1, ["scale"] = p.Drawing.Scale,
                ["content"] = p.Content, ["page"] = p.Viewport.Page, ["pages"] = p.Pages, ["width"] = p.Viewport.Width,
                ["pinned"] = p.Viewport.Pinned, ["boundary"] = p.Boundary?.Id, ["boundaryName"] = p.Boundary?.Name, ["name"] = p.View.Name,
                ["cx"] = p.Middle.X, ["cy"] = p.Middle.Y,
                ["warnings"] = new JsonArray(Problems(sheet, p).Select(w => (JsonNode)w).ToArray()),
            }).ToArray()),
        };
    }

    public static byte[] Pdf(IQueryContext q, IEnumerable<Entity> sheets, Issuing? issuing = null) =>
        PdfWriter.Write(sheets.Select(s => Compose(q, s, null, issuing)).ToList(), id => q.Model.Get(id) is { Class: Images.Class } image ? Images.Bytes(image) : null);

    public static byte[] Dxf(IQueryContext q, Entity sheet, Issuing? issuing = null) => DxfWriter.Page(Compose(q, sheet, null, issuing), $"Lapin · {sheet.Str("number")} {sheet.Name}");

    // ------------------------------------------------------------ commands

    static Entity Require(IModelView m, string? id) => m.Get(id ?? "") is { Class: Class } s ? s : throw new CommandException("There is no such sheet.");

    /// <summary>A sheet's viewport as placed now (refused when pinned or a schedule).</summary>
    static (List<SheetViewport> Vps, int I, Placement Pl) Placed(ICommandContext ctx, Entity sheet, string? viewportId)
    {
        var vps = Viewports(sheet);
        var i = vps.FindIndex(v => v.Id == viewportId);
        if (i < 0) throw new CommandException("The sheet has no such view.");
        if (vps[i].Pinned) throw new CommandException("The view is pinned to the sheet: unpin it first.");
        var pl = Place(ctx.Query, sheet).FirstOrDefault(p => p.Viewport.Id == viewportId) ?? throw new CommandException("The view does not draw.");
        if (pl.Content) throw new CommandException("A schedule is set by its width.");
        return (vps, i, pl);
    }

    static void SetBoundary(ICommandContext ctx, Entity b, Vec2 min, Vec2 max)
    {
        var attrs = b.CloneAttrs();
        attrs["min"] = new JsonArray(Math.Round(min.X, 6), Math.Round(min.Y, 6));
        attrs["max"] = new JsonArray(Math.Round(max.X, 6), Math.Round(max.Y, 6));
        ctx.Replace(b.With(attrs: attrs));
    }

    static void Write(ICommandContext ctx, Entity sheet, List<SheetViewport> vps)
    {
        var attrs = sheet.CloneAttrs();
        attrs["viewports"] = new JsonArray(vps.Select(v => (JsonNode)v.ToJson()).ToArray());
        ctx.Replace(sheet.With(attrs: attrs));
    }

    /// <summary>
    /// A new sheet (the next number) with the views or contents given, each by its id, centre (paper mm) and page: what a
    /// module's command lays out in one step (the Gantt on a sheet, a stages sheet).
    /// </summary>
    public static Entity AddSheet(ICommandContext ctx, string name, string paper, bool portrait, IEnumerable<(string ViewId, double X, double Y, int Page)> views, string? discipline = null)
    {
        if (!Papers.ContainsKey(paper)) throw new CommandException("A sheet is A0, A1, A2, A3 or A4.");
        var id = ctx.NewId();
        var vps = views.Select(v => (JsonNode)new SheetViewport(ctx.NewId(), v.ViewId, v.X, v.Y, Page: v.Page).ToJson()).ToArray();
        var sheet = new Entity(id, Class, name, new JsonObject
        {
            ["number"] = NextNumber(ctx.Model), ["paper"] = paper, ["orientation"] = portrait ? "portrait" : "landscape",
            ["titleBlock"] = new JsonObject(), ["viewports"] = new JsonArray(vps),
        });
        if (discipline != null) sheet.Attrs["discipline"] = discipline;
        ctx.Add(sheet);
        return sheet;
    }

    /// <summary>The next sheet number after the last one: A-101 → A-102.</summary>
    public static string NextNumber(IModelView m)
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
            // A named boundary of the view (decision 236): the view may go on as many sheets as it has boundaries, once each.
            var boundary = (string?)a["boundaryId"] is { Length: > 0 } bid
                ? ctx.Model.Get(bid) is { Class: ViewBoundaries.Class } be && be.Str("viewId") == viewId ? bid : throw new CommandException("The view has no such boundary.")
                : null;
            if (ctx.Model.OfClass(Class).FirstOrDefault(s => Viewports(s).Any(v => v.ViewId == viewId && v.Page == page && v.Boundary == boundary)) is { } on)
                throw new CommandException(boundary != null ? $"That boundary of the view is on sheet {on.Str("number")} already." : $"The view is on sheet {on.Str("number")} already.");
            var (W, H) = PaperOf(sheet);
            var vps = Viewports(sheet);
            var (x, y) = (Json.Number(a["x"]) ?? (W - TitleBlock.W) / 2, Json.Number(a["y"]) ?? H / 2);
            // A schedule placed with no point (decision 287): its top-left corner in the frame's, where a table is read from, not
            // its middle in the paper's (a wide table ran past the frame on the left).
            if (isContent && a["x"] is null && a["y"] is null && ctx.Services.All<ISheetContent>().First(c => c.Handles(ctx.Model.Get(viewId)!)) is var content
                && content.Pages(ctx.Query, ctx.Model.Get(viewId)!, FrameSize(sheet).W, FrameSize(sheet).H) is { } pages && page - 1 < pages.Count)
                (x, y) = (Margin + 4 + pages[page - 1].W / 2, Margin + 4 + pages[page - 1].H / 2);
            var vp = new SheetViewport(ctx.NewId(), viewId, x, y, Page: page, Boundary: boundary);
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
            var moves = a.ContainsKey("x") || a.ContainsKey("y") || a.ContainsKey("width") || a.ContainsKey("height") || a.ContainsKey("center");
            if (v.Pinned && moves && !(a["pinned"] is JsonValue up && up.TryGetValue<bool>(out var unpin) && !unpin)) throw new CommandException("The view is pinned to the sheet: unpin it to move it.");
            var next = v with
            {
                X = Json.Number(a["x"]) ?? v.X, Y = Json.Number(a["y"]) ?? v.Y,
                Width = Json.Number(a["width"]) ?? v.Width, Height = Json.Number(a["height"]) ?? v.Height,
                Center = a.ContainsKey("center") ? Args.TryPoint(a["center"]) : v.Center,
                Pinned = a["pinned"] is JsonValue pv && pv.TryGetValue<bool>(out var pin) ? pin : v.Pinned,
            };
            // Bound to a boundary, or set free of it (keeping the box it had, so nothing moves on the paper).
            if (a.ContainsKey("boundary"))
            {
                if ((string?)a["boundary"] is { Length: > 0 } bid)
                {
                    if (ctx.Model.Get(bid) is not { Class: ViewBoundaries.Class } be || be.Str("viewId") != v.ViewId) throw new CommandException("The view has no such boundary.");
                    if (ctx.Model.OfClass(Class).Any(s => Viewports(s).Any(o => o.Id != v.Id && o.ViewId == v.ViewId && o.Page == v.Page && o.Boundary == bid)))
                        throw new CommandException("That boundary of the view is on a sheet already.");
                    next = next with { Boundary = bid };
                }
                else if (Place(ctx.Query, sheet).FirstOrDefault(p => p.Viewport.Id == v.Id) is { } pl)
                    next = next with { Boundary = null, Width = pl.X1 - pl.X0, Height = pl.Y1 - pl.Y0, Center = pl.Middle };
            }
            vps[i] = next;
            Write(ctx, sheet, vps);
            return null;
        });

        // A view's box set on the sheet (paper mm, x0 y0 x1 y1): what it shows stays where it is on the paper, the box takes more or less
        // of it; a view bound to a boundary resizes the boundary in its view (decision 236).
        r.Command("sheet.cropViewport", "Crop view", (ctx, a) =>
        {
            var sheet = Require(ctx.Model, (string?)a["id"]);
            var (vps, i, pl) = Placed(ctx, sheet, (string?)a["viewportId"]);
            var (x0, y0, x1, y1) = (Json.Number(a["x0"]) ?? pl.X0, Json.Number(a["y0"]) ?? pl.Y0, Json.Number(a["x1"]) ?? pl.X1, Json.Number(a["y1"]) ?? pl.Y1);
            if (x1 < x0) (x0, x1) = (x1, x0);
            if (y1 < y0) (y0, y1) = (y1, y0);
            if (x1 - x0 < 10 || y1 - y0 < 10) throw new CommandException("A view on a sheet is 10 mm across at least.");
            var (cx, cy) = ((x0 + x1) / 2, (y0 + y1) / 2);
            var k = pl.K;
            var middle = pl.Middle + new Vec2((cx - pl.Viewport.X) / k, -(cy - pl.Viewport.Y) / k);
            if (pl.Boundary is { } b)
            {
                var half = new Vec2((x1 - x0) / 2 / k, (y1 - y0) / 2 / k);
                SetBoundary(ctx, b, middle - half, middle + half);
                vps[i] = vps[i] with { X = cx, Y = cy };
            }
            else vps[i] = vps[i] with { X = cx, Y = cy, Width = x1 - x0, Height = y1 - y0, Center = middle };
            Write(ctx, sheet, vps);
            return null;
        });

        // What a view shows slid inside its box by dx, dy paper mm (the box stays); a bound one moves its boundary in its view.
        r.Command("sheet.panViewport", "Pan view", (ctx, a) =>
        {
            var sheet = Require(ctx.Model, (string?)a["id"]);
            var (vps, i, pl) = Placed(ctx, sheet, (string?)a["viewportId"]);
            var shift = new Vec2(-(Json.Number(a["dx"]) ?? 0) / pl.K, (Json.Number(a["dy"]) ?? 0) / pl.K);
            if (pl.Boundary is { } b)
            {
                var (min, max) = ViewBoundaries.Box(b);
                SetBoundary(ctx, b, min + shift, max + shift);
            }
            else
            {
                vps[i] = vps[i] with { Width = pl.X1 - pl.X0, Height = pl.Y1 - pl.Y0, Center = pl.Middle + shift };
                Write(ctx, sheet, vps);
            }
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
            return PageJson(Compose(q, sheet, placements), sheet, placements);
        });
    }
}
