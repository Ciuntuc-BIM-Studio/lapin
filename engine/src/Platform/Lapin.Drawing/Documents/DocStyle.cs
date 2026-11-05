using Lapin.Drawing.Export;

namespace Lapin.Drawing.Documents;

/// <summary>
/// A company's look for a document (docs/plan-documents.md, stage 4; <c>page.style</c>): the text's font in DOCX and ODT
/// (any family name: Arial, Calibri, Times New Roman…), the PDF's font (<c>helvetica</c> or <c>times</c>, the PDF standard
/// fonts; when not said, Times for a serif <see cref="Font"/>, else Helvetica), the body text's size in points (the other
/// sizes follow it), the headings' colour (#rrggbb) and the logo in the header. Every field null: Lapin's own look.
/// </summary>
public sealed record DocStyle(string? Font = null, double? Size = null, string? HeadingColor = null, string? PdfFont = null, DocLogo? Logo = null);

/// <summary>A logo in the header: a PNG or JPEG file, its width on paper (mm), on the left or the right of the header.</summary>
public sealed record DocLogo(byte[] Data, double W, string Position = "left")
{
    /// <summary>Its pixels' width and height (null: not a PNG or JPEG Lapin reads).</summary>
    public (int W, int H)? Pixels => Raster.Read(Data) is { W: > 0, H: > 0 } r ? (r.W, r.H) : null;

    public bool Jpeg => Data.Length > 3 && Data[0] == 0xFF && Data[1] == 0xD8;
    public string Extension => Jpeg ? "jpeg" : "png";
    public string Mime => Jpeg ? "image/jpeg" : "image/png";
    public bool Right => Position == "right";
}

/// <summary>
/// The look a writer uses, from the page's style: the sizes (points), the fonts, the headings' colour and where the logo goes.
/// With no style, exactly <see cref="DocText"/>'s defaults, so a document without one is written as before.
/// </summary>
public sealed class DocLook
{
    /// <summary>The serif families that make Times the PDF's font when the style does not say.</summary>
    static readonly string[] Serif = ["Times New Roman", "Times", "Georgia", "Liberation Serif", "Cambria", "Garamond", "Book Antiqua", "Palatino Linotype"];

    public string Font { get; }
    public bool Times { get; }
    public double Scale { get; }
    public string? HeadingColor { get; }
    public DocLogo? Logo { get; }

    public double Body => DocText.BodyPt * Scale;
    public double Caption => DocText.CaptionPt * Scale;
    public double Note => DocText.NotePt * Scale;
    public double Header => DocText.HeaderPt * Scale;
    public double Table => DocText.TablePt * Scale;
    public double Heading(int level) => DocText.HeadingPt(level) * Scale;

    DocLook(DocStyle? s)
    {
        Font = s?.Font is { Length: > 0 } f ? f : DocText.Font;
        Times = s?.PdfFont is { } pdf ? pdf == "times" : Serif.Contains(Font, StringComparer.OrdinalIgnoreCase);
        Scale = s?.Size is { } size && size > 0 ? size / DocText.BodyPt : 1;
        HeadingColor = s?.HeadingColor;
        Logo = s?.Logo is { W: > 0 } logo && logo.Pixels != null ? logo : null;
    }

    public static DocLook Of(DocPage page) => new(page.Style);

    /// <summary>A text's width in mm at a size in points, by the PDF font's widths.</summary>
    public double Mm(string s, bool bold, double pt) => Times ? TimesMetrics.Mm(s, bold, pt) : HelveticaMetrics.Mm(s, bold, pt);

    /// <summary>How far down the paper the header starts (mm): the DOCX header distance, the ODT page's top margin.</summary>
    public static double HeaderTop(DocPage p) => Math.Max(5, p.Top * 0.4);

    /// <summary>
    /// Where the logo sits (paper mm): from the header's top, at its width scaled down to fit the top margin (2 mm kept above the
    /// text), on the left or the right of the text width; null without a logo.
    /// </summary>
    public (double X, double Y, double W, double H)? LogoBox(DocPage p)
    {
        if (Logo is not { } logo || logo.Pixels is not { } px) return null;
        var top = HeaderTop(p);
        var w = Math.Min(logo.W, p.TextWidth * 0.5);
        var h = w * px.H / px.W;
        var room = Math.Max(4, p.Top - top - 2);
        if (h > room) { w *= room / h; h = room; }
        return (logo.Right ? p.Left + p.TextWidth - w : p.Left, top, w, h);
    }

    /// <summary>The gap between the logo and the header's text (mm).</summary>
    public const double LogoGap = 4;
}
