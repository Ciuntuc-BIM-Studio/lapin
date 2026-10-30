using System.Globalization;

namespace Lapin.Platform.Cde.PdfRemarks;

/// <summary>A page as the annotations need it: its index, its displayed box (CropBox within MediaBox, points, user space) and its rotation.</summary>
public sealed record PdfPageInfo(int Index, double X0, double Y0, double X1, double Y1, int Rotate)
{
    public double Width => X1 - X0;
    public double Height => Y1 - Y0;
}

/// <summary>
/// One markup annotation as read (§12.5.6): where it is on its page (points,
/// user space), what kind it is, its text, who made it and when, and the
/// thread it belongs to (<c>/IRT</c>). <see cref="Key"/> is the object it
/// was ("num:gen"); <see cref="Name"/> its <c>/NM</c> when the writer gave
/// one (Acrobat and Bluebeam do), which survives a full re-save.
/// </summary>
public sealed record PdfAnnotation(
    int Page, string Key, string Subtype, string? Name, double[]? Rect, double[]? QuadPoints, List<double[]>? InkList, double[]? Line, double[]? Vertices,
    string Contents, string? Author, string? Subject, string? Modified, string? Created, string? InReplyTo, string? ReplyType, string? State, string? StateModel,
    string? Intent, string? Color);

/// <summary>What a file yielded: its pages, its markup annotations in page order, the warnings met, and how many annotations were not markup (links, widgets, popups).</summary>
public sealed record PdfRead(List<PdfPageInfo> Pages, List<PdfAnnotation> Annotations, List<string> Warnings, int Skipped);

/// <summary>
/// The markup annotations of a PDF: each page's <c>/Annots</c>, the
/// dictionaries that are remarks (notes, free text, highlights and the
/// other text markups, ink, squares, circles, lines, polygons, polylines,
/// stamps, carets, redactions — what Acrobat, Bluebeam, Preview and
/// Drawboard write; Bluebeam's measurements and stamps come as the same
/// kinds), with their geometry, text, author, dates, colour and thread.
/// Popups, links, widgets and hidden annotations are skipped and counted.
/// </summary>
public static class PdfAnnotations
{
    static readonly HashSet<string> Markup =
    [
        "Text", "FreeText", "Highlight", "Underline", "Squiggly", "StrikeOut", "Ink", "Square", "Circle", "Line", "Polygon", "PolyLine", "Stamp", "Caret", "Redact", "FileAttachment",
    ];

    public static PdfRead Read(byte[] pdf)
    {
        var doc = PdfDocument.Load(pdf);
        var pages = new List<PdfPageInfo>();
        var annotations = new List<PdfAnnotation>();
        var skipped = 0;
        List<PdfDocument.Page> tree;
        try { tree = doc.Pages(); }
        catch (Exception e) { doc.Warnings.Add($"The pages could not be read: {e.Message}"); tree = []; }
        for (var pi = 0; pi < tree.Count; pi++)
        {
            var page = tree[pi];
            var box = page.MediaBox;
            if (page.CropBox is { } crop)
                box = [Math.Max(box[0], crop[0]), Math.Max(box[1], crop[1]), Math.Min(box[2], crop[2]), Math.Min(box[3], crop[3])];
            if (!(box[2] > box[0]) || !(box[3] > box[1])) box = page.MediaBox;
            var info = new PdfPageInfo(pi, box[0], box[1], box[2], box[3], page.Rotate);
            pages.Add(info);
            PdfArray? annots;
            try { annots = doc.Array(page.Dict["Annots"]); }
            catch (Exception e) { doc.Warnings.Add($"Page {pi + 1}: its annotations could not be read ({e.Message})."); continue; }
            if (annots == null) continue;
            for (var ai = 0; ai < annots.Items.Count; ai++)
            {
                try
                {
                    var item = annots.Items[ai];
                    var a = doc.Dict(item);
                    if (a == null) continue;
                    var subtype = doc.Name(a["Subtype"]) ?? "";
                    if (!Markup.Contains(subtype)) { skipped++; continue; }
                    var flags = (int)(doc.Number(a["F"]) ?? 0);
                    if ((flags & 2) != 0 || (flags & 32) != 0) { skipped++; continue; }   // hidden, or not for viewing
                    var key = item is PdfRef r ? $"{r.Num}:{r.Gen}" : $"p{pi + 1}#{ai}";
                    var irt = a["IRT"] is PdfRef ir ? $"{ir.Num}:{ir.Gen}" : null;
                    var ink = doc.Array(a["InkList"])?.Items.Select(x => doc.Numbers(x)).Where(x => x is { Length: >= 2 }).Select(x => x!).ToList();
                    annotations.Add(new PdfAnnotation(
                        pi, key, subtype, doc.Text(a["NM"]),
                        Finite(doc.Numbers(a["Rect"]), 4), Finite(doc.Numbers(a["QuadPoints"]), 8), ink is { Count: > 0 } ? ink : null,
                        Finite(doc.Numbers(a["L"]), 4), Finite(doc.Numbers(a["Vertices"]), 2),
                        doc.Text(a["Contents"]) ?? "", Blank(doc.Text(a["T"])), Blank(doc.Text(a["Subj"])),
                        PdfText.Date(doc.Text(a["M"])), PdfText.Date(doc.Text(a["CreationDate"])),
                        irt, doc.Name(a["RT"]), doc.Name(a["State"]) ?? Blank(doc.Text(a["State"])), doc.Name(a["StateModel"]) ?? Blank(doc.Text(a["StateModel"])),
                        doc.Name(a["IT"]), Colour(doc.Numbers(a["C"]))));
                }
                catch (Exception e) { doc.Warnings.Add($"Page {pi + 1}: an annotation could not be read ({e.Message})."); }
            }
        }
        if (tree.Count == 0 && doc.Warnings.Count == 0) doc.Warnings.Add("The file has no page.");
        return new PdfRead(pages, annotations, doc.Warnings.Distinct().ToList(), skipped);
    }

    static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Numbers that are all finite and come in whole groups of <paramref name="group"/>; else null.</summary>
    static double[]? Finite(double[]? a, int group) =>
        a is { Length: > 0 } && a.Length % group == 0 && a.All(double.IsFinite) ? a : null;

    /// <summary>/C as a CSS colour: gray, RGB or CMYK; null for none (transparent).</summary>
    public static string? Colour(double[]? c)
    {
        if (c == null) return null;
        double r, g, b;
        switch (c.Length)
        {
            case 1: r = g = b = c[0]; break;
            case 3: (r, g, b) = (c[0], c[1], c[2]); break;
            case 4: r = (1 - c[0]) * (1 - c[3]); g = (1 - c[1]) * (1 - c[3]); b = (1 - c[2]) * (1 - c[3]); break;
            default: return null;
        }
        static int B(double v) => (int)Math.Round(Math.Clamp(v, 0, 1) * 255);
        return $"#{B(r):x2}{B(g):x2}{B(b):x2}";
    }
}

/// <summary>
/// The page's user space onto the sheet's paper (millimetres, y down from
/// the top-left corner, as <c>Sheets</c> composes it and <c>PdfWriter</c>
/// prints it): the page as displayed — its box turned by <c>/Rotate</c>
/// (clockwise, §7.7.3.3) — scaled to the sheet's paper when the page was
/// printed to another size. Lapin's own PDF of the sheet is the paper at
/// 1:1 with no rotation, so there the map is the writer's transform undone.
/// </summary>
public sealed record PdfPageMap(PdfPageInfo Page, double PaperW, double PaperH)
{
    public const double Pt = 72 / 25.4;

    /// <summary>The page as shown (after the rotation), in points.</summary>
    public (double W, double H) Displayed => Page.Rotate is 90 or 270 ? (Page.Height, Page.Width) : (Page.Width, Page.Height);

    /// <summary>The scale from the displayed page to the paper, per axis (1 when the page is the paper).</summary>
    public (double X, double Y) Scale
    {
        get
        {
            var (w, h) = Displayed;
            return (w > 0 ? PaperW / (w / Pt) : 1, h > 0 ? PaperH / (h / Pt) : 1);
        }
    }

    /// <summary>Whether the page is not the paper's size (within half a millimetre), so the remarks were scaled onto it.</summary>
    public bool Scaled { get { var (w, h) = Displayed; return Math.Abs(w / Pt - PaperW) > 0.5 || Math.Abs(h / Pt - PaperH) > 0.5; } }

    /// <summary>A point of the page's user space (points) on the sheet (mm, y down).</summary>
    public (double X, double Y) ToSheet(double ux, double uy)
    {
        // The displayed point, top-left origin, y down, in points.
        var (dx, dy) = Page.Rotate switch
        {
            90 => (uy - Page.Y0, ux - Page.X0),
            180 => (Page.X1 - ux, uy - Page.Y0),
            270 => (Page.Y1 - uy, Page.X1 - ux),
            _ => (ux - Page.X0, Page.Y1 - uy),
        };
        var (sx, sy) = Scale;
        return (Math.Round(dx / Pt * sx, 3), Math.Round(dy / Pt * sy, 3));
    }
}
