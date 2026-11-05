using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;

namespace Lapin.Drawing.Documents;

/// <summary>
/// The document as a PDF laid out by Lapin: words wrapped by Helvetica's widths, tables across pages, vector pictures, numbered pages.
/// Laid out twice: the first pass finds each heading's page and the page count, the second writes them into the table of
/// contents and the page fields (an entry's number has its room kept in both, so the two passes break the same way).
/// The pages are pens on paper, written by the same <see cref="PdfWriter"/> as the sheets, with the headings as bookmarks.
/// </summary>
public static class DocumentPdf
{
    public static byte[] Write(FilledDocument doc)
    {
        var first = new DocumentLayout(doc, null, null).Run();
        var second = new DocumentLayout(doc, first.Headings.Select(h => h.Page).ToList(), first.Pages.Count).Run();
        var outline = second.Headings.Where(h => h.Heading.Level <= 3)
            .Select(h => new PdfOutline(DocText.Plain(h.Heading.Runs), h.Page - 1, Math.Max(0, h.Y - 2), h.Heading.Level)).ToList();
        var info = new PdfInfo(doc.Title, doc.Meta.Author, doc.Meta.Subject, outline);
        // The style's logo is the only picture a page asks for by id (the views' pictures are pens).
        var logo = DocLook.Of(doc.Page).Logo;
        return PdfWriter.Write(second.Pages, logo == null ? null : id => id == DocumentLayout.LogoId ? logo.Data : null, info);
    }

    /// <summary>Every heading with its page (from 1), as the PDF lays them out.</summary>
    public static IReadOnlyList<(DocHeading Heading, int Page)> Headings(FilledDocument doc) =>
        [.. new DocumentLayout(doc, null, null).Run().Headings.Select(h => (h.Heading, h.Page))];

    /// <summary>The PDF's pages without writing them (the layout, for a test or a preview).</summary>
    public static IReadOnlyList<PaperPage> Pages(FilledDocument doc)
    {
        var first = new DocumentLayout(doc, null, null).Run();
        return new DocumentLayout(doc, first.Headings.Select(h => h.Page).ToList(), first.Pages.Count).Run().Pages;
    }
}

/// <summary>One pass of the layout: blocks down the page, a new page when the next does not fit.</summary>
sealed class DocumentLayout(FilledDocument doc, IReadOnlyList<int>? headingPages, int? pageCount)
{
    public sealed record Placed(DocHeading Heading, int Page, double Y);
    public sealed record Result(List<PaperPage> Pages, List<Placed> Headings);

    const double PtMm = 25.4 / 72;
    const string Ink = "#000000", Grey = "#555555", Rule = "#000000", Shade = "#E6E6E6";

    readonly DocPage P = doc.Page;
    readonly DocLook L = DocLook.Of(doc.Page);
    string HeadingInk => L.HeadingColor ?? Ink;
    string FontName => L.Times ? "times" : "sans";

    /// <summary>The id the header's logo is drawn by (the PDF writer asks for its file by it).</summary>
    public const string LogoId = "doc-logo";
    double Tw => P.TextWidth;
    double X0 => P.Left;
    double Bottom => P.H - P.Bottom;

    sealed class Sheet
    {
        public readonly List<Pen> Under = [], Over = [];
        public readonly Dictionary<string, Pen> Texts = [];
        public readonly Dictionary<(string, double), Pen> Lines = [];
    }

    readonly List<Sheet> sheets = [];
    readonly List<Placed> headings = [];
    double y;
    bool atTop;
    Sheet Cur => sheets[^1];
    int PageNo => sheets.Count;

    public Result Run()
    {
        NewPage();
        var blocks = doc.Blocks;
        for (var i = 0; i < blocks.Count; i++)
        {
            var next = i + 1 < blocks.Count ? blocks[i + 1] : null;
            switch (blocks[i])
            {
                case DocHeading h: Heading(h, next); break;
                case DocParagraph p: Paragraph(p); break;
                case DocList l: List(l); break;
                case DocTable t: Table(t); break;
                case DocImage img: Image(img); break;
                case DocToc toc: Toc(toc); break;
                case DocPageBreak: if (!atTop) NewPage(); break;
            }
        }
        var total = sheets.Count;
        var pages = new List<PaperPage>();
        for (var k = 0; k < total; k++)
        {
            Bands(sheets[k], k + 1, total);
            pages.Add(new PaperPage(P.W, P.H, [.. sheets[k].Under, .. sheets[k].Over], doc.Title));
        }
        return new Result(pages, headings);
    }

    // ---- pages and pens

    void NewPage() { sheets.Add(new Sheet()); y = P.Top; atTop = true; }

    /// <summary>Room for <paramref name="h"/> mm, else a new page (unless this one is still empty: what does not fit an empty page runs over).</summary>
    void Ensure(double h) { if (!atTop && y + h > Bottom + 1e-6) NewPage(); }

    void Space(double mm) { if (!atTop) y += mm; }

    Pen TextPen(Sheet s, string color)
    {
        if (!s.Texts.TryGetValue(color, out var pen)) { pen = new Pen("TEXT", color, 0.1, null, null); s.Texts[color] = pen; s.Over.Add(pen); }
        return pen;
    }

    Pen LinePen(Sheet s, string color, double width)
    {
        if (!s.Lines.TryGetValue((color, width), out var pen)) { pen = new Pen("LINE", color, width, null, null); s.Lines[(color, width)] = pen; s.Over.Add(pen); }
        return pen;
    }

    void Rect(double x, double top, double w, double h, string fill)
    {
        var pen = new Pen("FILL", null, 0, fill, null);
        pen.Loops.Add([new Vec2(x, top), new Vec2(x + w, top), new Vec2(x + w, top + h), new Vec2(x, top + h)]);
        Cur.Under.Add(pen);
    }

    // ---- text

    sealed record Seg(string Text, bool Bold, bool Italic, double X, double W);
    sealed record Line(List<Seg> Segs, double W);

    static double LineH(double pt) => pt * 1.3 * PtMm;
    static double Cap(double pt) => pt * PtMm * 0.718;

    /// <summary>Runs wrapped on words into lines no wider than <paramref name="maxW"/> mm; a word wider than a line is broken by letters.</summary>
    List<Line> Wrap(IEnumerable<DocRun> runs, double pt, double maxW, bool bold = false, bool italic = false)
    {
        // Words (each a list of styled parts), spaces and breaks.
        var tokens = new List<(char Kind, List<(string T, bool B, bool I)> Parts)>();
        List<(string T, bool B, bool I)>? word = null;
        foreach (var r in runs)
        {
            var text = DocText.Of(r, PageNo, pageCount);
            bool b = bold || r.Bold, it = italic || r.Italic;
            foreach (var ch in text)
            {
                if (ch == '\n') { word = null; tokens.Add(('n', [])); }
                else if (ch is ' ' or '\t')
                {
                    word = null;
                    if (tokens.Count > 0 && tokens[^1].Kind == ' ') continue;
                    tokens.Add((' ', [(" ", b, it)]));
                }
                else if (ch == '\r') { }
                else
                {
                    if (word == null) { word = []; tokens.Add(('w', word)); }
                    if (word.Count > 0 && word[^1].B == b && word[^1].I == it) word[^1] = (word[^1].T + ch, b, it);
                    else word.Add((ch.ToString(), b, it));
                }
            }
        }
        var lines = new List<Line>();
        var cur = new List<Seg>();
        double x = 0;
        (string T, bool B, bool I)? space = null;
        void Flush() { lines.Add(new Line(cur, x)); cur = []; x = 0; space = null; }
        void Add(string t, bool b, bool it)
        {
            var w = L.Mm(t, b, pt);
            if (cur.Count > 0 && cur[^1].Bold == b && cur[^1].Italic == it)
            {
                var last = cur[^1];
                cur[^1] = last with { Text = last.Text + t, W = last.W + w };
            }
            else cur.Add(new Seg(t, b, it, x, w));
            x += w;
        }
        foreach (var (kind, parts) in tokens)
        {
            if (kind == 'n') { Flush(); continue; }
            if (kind == ' ') { if (cur.Count > 0) space = parts[0]; continue; }
            var w = parts.Sum(p => L.Mm(p.T, p.B, pt));
            var sw = space is { } s ? L.Mm(" ", s.B, pt) : 0;
            if (cur.Count > 0 && x + sw + w > maxW + 1e-6) Flush();
            if (cur.Count == 0 && w > maxW)
            {
                // Letter by letter, a line at a time.
                foreach (var (t, b, it) in parts)
                    foreach (var ch in t)
                    {
                        var cw = L.Mm(ch.ToString(), b, pt);
                        if (cur.Count > 0 && x + cw > maxW) Flush();
                        Add(ch.ToString(), b, it);
                    }
                space = null;
                continue;
            }
            if (space is { } sp && cur.Count > 0) Add(" ", sp.B, sp.I);
            space = null;
            foreach (var (t, b, it) in parts) Add(t, b, it);
        }
        if (cur.Count > 0 || lines.Count == 0) Flush();
        return lines;
    }

    /// <summary>One line drawn with its top at <paramref name="top"/>, from <paramref name="x"/> in a box <paramref name="w"/> wide.</summary>
    void Draw(Sheet s, Line line, double x, double w, double top, double pt, string color, char align = 'l')
    {
        var off = align switch { 'r' => w - line.W, 'c' => (w - line.W) / 2, _ => 0 };
        var baseline = top + pt * PtMm * 0.95;
        var pen = TextPen(s, color);
        foreach (var seg in line.Segs)
        {
            if (seg.Text.Trim().Length == 0) continue;
            pen.Texts.Add(new PenText(new Vec2(x + off + seg.X, baseline), seg.Text, Cap(pt), 0, "start", "bottom", seg.Bold, FontName, seg.Italic));
        }
    }

    /// <summary>Lines down the page, each moved to the next page when it does not fit.</summary>
    void Lines(List<Line> lines, double x, double w, double pt, string color, char align = 'l')
    {
        var lh = LineH(pt);
        foreach (var line in lines)
        {
            Ensure(lh);
            Draw(Cur, line, x, w, y, pt, color, align);
            y += lh;
            atTop = false;
        }
    }

    // ---- blocks

    void Heading(DocHeading h, DocBlock? next)
    {
        var pt = L.Heading(h.Level);
        var before = h.Level <= 1 ? 7.0 : h.Level == 2 ? 5.0 : 4.0;
        const double after = 2.2;
        var lines = Wrap(h.Runs, pt, Tw, bold: true);
        var need = before + lines.Count * LineH(pt) + after + Least(next);
        if (!atTop && y + need > Bottom) NewPage();
        else Space(before);
        headings.Add(new Placed(h, PageNo, y));
        Lines(lines, X0, Tw, pt, HeadingInk);
        y += after;
    }

    /// <summary>The least of a block kept on the page with the heading before it.</summary>
    double Least(DocBlock? b) => b switch
    {
        DocParagraph p => LineH(Style(p.Style).Pt) * 2,
        DocList => LineH(L.Body) * 2,
        DocTable t => (t.Title != null ? LineH(L.Body) + 1.5 : 0) + (t.Plain ? 1 : 2) * (LineH(L.Table) + 2.4),
        DocImage img => Math.Min(DocText.Fit(img, Tw).H, (Bottom - P.Top) * 0.6) + (img.Caption != null ? LineH(L.Caption) + 2 : 0),
        _ => 0,
    };

    (double Pt, bool Italic, string Color, double After) Style(string style) => style switch
    {
        "caption" => (L.Caption, true, Ink, 3.5),
        "note" => (L.Note, false, Grey, 2.5),
        _ => (L.Body, false, Ink, 2.5),
    };

    void Paragraph(DocParagraph p)
    {
        var (pt, italic, color, after) = Style(p.Style);
        Lines(Wrap(p.Runs, pt, Tw, italic: italic), X0, Tw, pt, color);
        y += after;
    }

    void List(DocList l)
    {
        var pt = L.Body;
        var lh = LineH(pt);
        var indent = l.Ordered ? Math.Max(6.5, L.Mm($"{l.Items.Count}.", false, pt) + 3) : 6;
        for (var i = 0; i < l.Items.Count; i++)
        {
            var lines = Wrap(l.Items[i], pt, Tw - indent);
            for (var k = 0; k < lines.Count; k++)
            {
                Ensure(lh);
                if (k == 0)
                {
                    var marker = l.Ordered ? $"{i + 1}." : "•";
                    var mw = L.Mm(marker, false, pt);
                    Draw(Cur, new Line([new Seg(marker, false, false, 0, mw)], mw), X0, indent - 2, y, pt, Ink, l.Ordered ? 'r' : 'l');
                }
                Draw(Cur, lines[k], X0 + indent, Tw - indent, y, pt, Ink);
                y += lh;
                atTop = false;
            }
            y += 1;
        }
        y += 1.5;
    }

    void Table(DocTable t)
    {
        var widths = DocText.ColumnWidths(t, Tw, measure: L.Mm, pt: L.Table);
        var n = widths.Length;
        if (n == 0) return;
        var pt = L.Table;
        var lh = LineH(pt);
        const double padX = 1.5, padY = 1.2;
        var edges = new double[n + 1];
        edges[0] = X0;
        for (var c = 0; c < n; c++) edges[c + 1] = edges[c] + widths[c];

        List<Line>[] Cells(IReadOnlyList<IReadOnlyList<DocRun>> cells, bool bold) =>
            [.. Enumerable.Range(0, n).Select(c => Wrap(c < cells.Count ? cells[c] : [], pt, widths[c] - 2 * padX, bold: bold))];
        double Height(List<Line>[] cells) => cells.Max(c => c.Count) * lh + 2 * padY;

        var header = t.Plain || t.Header.Count == 0 ? null : Cells([.. t.Header.Select(h => (IReadOnlyList<DocRun>)[new DocRun(h)])], true);
        var headerH = header != null ? Height(header) : 0;
        var rows = t.Rows.Select((r, i) => Cells(r, t.TotalRow && i == t.Rows.Count - 1)).ToList();

        if (t.Title is { } title)
        {
            var tl = Wrap([new DocRun(title, Bold: true)], L.Body, Tw);
            Ensure(tl.Count * LineH(L.Body) + 1.5 + headerH + (rows.Count > 0 ? Height(rows[0]) : 0));
            Lines(tl, X0, Tw, L.Body, Ink);
            y += 1.5;
        }
        else Ensure(headerH + (rows.Count > 0 ? Height(rows[0]) : 0));

        // The grid of the part of the table on this page: its top, and each row's bottom.
        double top = y;
        var bottoms = new List<(double Y, bool Heavy)>();
        void Close()
        {
            if (t.Plain || bottoms.Count == 0) return;
            var thin = LinePen(Cur, Rule, 0.18);
            var heavy = LinePen(Cur, Rule, 0.35);
            thin.Segments.Add((new Vec2(edges[0], top), new Vec2(edges[n], top)));
            foreach (var (by, h) in bottoms) (h ? heavy : thin).Segments.Add((new Vec2(edges[0], by), new Vec2(edges[n], by)));
            var last = bottoms[^1].Y;
            foreach (var ex in edges) thin.Segments.Add((new Vec2(ex, top), new Vec2(ex, last)));
        }
        void Row(List<Line>[] cells, double h, bool isHeader, bool bold)
        {
            if (isHeader) Rect(edges[0], y, edges[n] - edges[0], h, Shade);
            for (var c = 0; c < n; c++)
            {
                var align = t.Numeric.Contains(c) ? 'r' : 'l';
                var ly = y + padY;
                foreach (var line in cells[c]) { Draw(Cur, line, edges[c] + padX, widths[c] - 2 * padX, ly, pt, Ink, align); ly += lh; }
            }
            y += h;
            atTop = false;
            bottoms.Add((y, isHeader));
        }
        void Head() { top = y; bottoms.Clear(); if (header != null) Row(header, headerH, true, true); }

        Head();
        for (var i = 0; i < rows.Count; i++)
        {
            var h = Height(rows[i]);
            if (y + h > Bottom + 1e-6 && bottoms.Count > (header != null ? 1 : 0))
            {
                Close();
                NewPage();
                Head();
            }
            var total = t.TotalRow && i == rows.Count - 1;
            if (total && bottoms.Count > 0) bottoms[^1] = (bottoms[^1].Y, true);
            Row(rows[i], h, false, total);
        }
        Close();
        y += 3.5;
    }

    void Image(DocImage img)
    {
        var (w, h) = DocText.Fit(img, Tw);
        var cap = img.Caption is { Length: > 0 } c ? Wrap([new DocRun(c, Italic: true)], L.Caption, Tw) : null;
        var capH = cap != null ? cap.Count * LineH(L.Caption) + 1.5 : 0;
        if (w > 0)
        {
            if (y + h + capH > Bottom && !atTop) NewPage();
            var room = Bottom - y - capH;
            if (h > room && room > 0) { var k = room / h; (w, h) = (w * k, h * k); }
            var s = w / img.W;
            var x = X0 + (Tw - w) / 2;
            foreach (var pen in img.Pens) Cur.Over.Add(Moved(pen, x, y, s));
            y += h;
            atTop = false;
        }
        if (cap != null) { y += 1.5; Lines(cap, X0, Tw, L.Caption, Ink, 'c'); }
        y += 3.5;
    }

    /// <summary>A picture's pen on the page: scaled by <paramref name="s"/> from its corner put at (x, y), its widths and texts with it.</summary>
    static Pen Moved(Pen pen, double x, double y, double s)
    {
        Vec2 M(Vec2 p) => new(x + p.X * s, y + p.Y * s);
        var m = new Pen(pen.Layer, pen.Stroke, pen.Width * s, pen.Fill, pen.Dash?.Select(d => d * s).ToArray());
        foreach (var (a, b) in pen.Segments) m.Segments.Add((M(a), M(b)));
        foreach (var loop in pen.Loops) m.Loops.Add([.. loop.Select(M)]);
        foreach (var t in pen.Texts) m.Texts.Add(t with { At = M(t.At), Height = t.Height * s });
        return m;
    }

    void Toc(DocToc toc)
    {
        Space(4);
        var pt = L.Heading(1);
        Lines(Wrap([new DocRun(toc.Title, Bold: true)], pt, Tw), X0, Tw, pt, Ink);
        y += 3;
        var all = doc.Blocks.OfType<DocHeading>().ToList();
        var body = L.Body;
        var lh = LineH(body);
        var numberW = L.Mm("0000", false, body);
        var dot = L.Mm(" .", false, body);
        for (var i = 0; i < all.Count; i++)
        {
            var h = all[i];
            if (h.Level > 2) continue;
            var indent = h.Level == 1 ? 0 : 6;
            var bold = h.Level == 1;
            var lines = Wrap(h.Runs, body, Tw - indent - numberW - 4, bold: bold);
            var page = headingPages != null && i < headingPages.Count ? headingPages[i].ToString() : "";
            for (var k = 0; k < lines.Count; k++)
            {
                Ensure(lh);
                Draw(Cur, lines[k], X0 + indent, Tw - indent, y, body, Ink);
                if (k == lines.Count - 1 && page.Length > 0)
                {
                    var pw = L.Mm(page, bold, body);
                    Draw(Cur, new Line([new Seg(page, bold, false, 0, pw)], pw), X0, Tw, y, body, Ink, 'r');
                    var from = X0 + indent + lines[k].W + 1.5;
                    var to = X0 + Tw - pw - 1.5;
                    var count = (int)Math.Floor((to - from) / dot);
                    if (count > 0)
                    {
                        var dots = string.Concat(Enumerable.Repeat(" .", count));
                        var dw = L.Mm(dots, false, body);
                        Draw(Cur, new Line([new Seg(dots, false, false, 0, dw)], dw), to - dw, dw, y, body, Grey);
                    }
                }
                y += lh;
                atTop = false;
            }
            y += h.Level == 1 ? 1 : 0.4;
        }
        y += 4;
    }

    /// <summary>The header and the footer of a page: the header left at the top margin's middle, the footer centred in the bottom one.</summary>
    void Bands(Sheet s, int page, int total)
    {
        var pt = L.Header;
        List<Line> Band(IReadOnlyList<DocRun> runs)
        {
            return WrapAt(runs, page, total);
        }
        var logo = L.LogoBox(P);
        if (logo is { } box)
        {
            // The logo at the header's top, by its corners (top left, top right, bottom right, bottom left).
            var pen = new Pen("IMAGE", null, 0, null, null);
            pen.Images.Add(new PenImage(LogoId, [new Vec2(box.X, box.Y), new Vec2(box.X + box.W, box.Y), new Vec2(box.X + box.W, box.Y + box.H), new Vec2(box.X, box.Y + box.H)]));
            s.Under.Add(pen);
        }
        if (P.Header.Count > 0)
        {
            // Beside a logo, the text takes the rest of the width, its last line on the logo's bottom (as Word and LibreOffice set it).
            var (tx, tw) = logo is { } b ? (L.Logo!.Right ? X0 : X0 + b.W + DocLook.LogoGap, Tw - b.W - DocLook.LogoGap) : (X0, Tw);
            var lines = WrapAt(P.Header, page, total, tw);
            var top = logo is { } lb ? lb.Y + lb.H - LineH(pt) * lines.Count + pt * PtMm * 0.25 : Math.Max(3, P.Top * 0.5 - LineH(pt) * lines.Count / 2);
            foreach (var line in lines) { Draw(s, line, tx, tw, top, pt, Grey); top += LineH(pt); }
        }
        if (P.Footer.Count > 0)
        {
            var lines = Band(P.Footer);
            var top = P.H - P.Bottom * 0.5 - LineH(pt) * lines.Count / 2;
            foreach (var line in lines) { Draw(s, line, X0, Tw, top, pt, Grey, 'c'); top += LineH(pt); }
        }
    }

    /// <summary>Runs wrapped with the page fields of a given page.</summary>
    List<Line> WrapAt(IReadOnlyList<DocRun> runs, int page, int total, double? width = null) =>
        Wrap(runs.Select(r => r.Field is "page" or "pages" && !r.Missing ? r with { Text = r.Field == "page" ? page.ToString() : total.ToString(), Field = null } : r), L.Header, width ?? Tw);
}
