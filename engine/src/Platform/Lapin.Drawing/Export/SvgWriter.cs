using System.Globalization;
using System.Text;
using Lapin.Geometry;
using Lapin.Drawing.Views;

namespace Lapin.Drawing.Export;

/// <summary>
/// What a click on a drawing takes (decision 482): an element by its GlobalId, the areas it cut and the lines it drew,
/// in paper millimetres. Never drawn; a page highlights it.
/// </summary>
public sealed record SvgPick(string Id, IReadOnlyList<IReadOnlyList<Vec2>> Loops, IReadOnlyList<(Vec2 A, Vec2 B)> Lines);

/// <summary>
/// A page as SVG 1.1 (W3C), from the pens a PDF prints (<see cref="PdfWriter"/>), so a browser shows what the sheets
/// print: the page in paper millimetres (y down, as the pens have it), each pen in order — its pictures laid by their
/// corners and clipped to its box, its fills even-odd, its lines as one path with their colour, paper width and
/// dashes, its texts in Helvetica (Times for a document's), the cap height the text's height as in the PDF. After the
/// drawing, what a click takes (<see cref="SvgPick"/>): an invisible group per element, its GlobalId in
/// <c>data-gid</c>. No script, no comment: the page inlines it as it is.
/// </summary>
public static class SvgWriter
{
    /// <summary>The page as an SVG document; <paramref name="image"/> gives a picture's file (PNG or JPEG) by its id, and a picture without one is left out.</summary>
    public static string Write(PaperPage page, IReadOnlyList<SvgPick>? picks = null, Func<string, byte[]?>? image = null)
    {
        var o = new StringBuilder();
        o.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ").Append(F(page.W)).Append(' ').Append(F(page.H))
         .Append("\" width=\"").Append(F(page.W)).Append("mm\" height=\"").Append(F(page.H)).Append("mm\">");
        if (page.Title.Length > 0) o.Append("<title>").Append(Escape(page.Title)).Append("</title>");
        var cap = page.LineCap == "square" ? "square" : "round";
        var join = page.LineCap == "square" ? "miter" : "round";
        var clips = 0;
        foreach (var pen in page.Pens)
        {
            if (pen.Images.Count > 0) Pictures(o, pen, image, ref clips);
            if (pen.Fill != null && pen.Loops.Count > 0)
            {
                var d = new StringBuilder();
                foreach (var loop in pen.Loops)
                {
                    if (loop.Count < 3) continue;
                    d.Append('M').Append(P(loop[0]));
                    foreach (var p in loop.Skip(1)) d.Append('L').Append(P(p));
                    d.Append('Z');
                }
                if (d.Length > 0) o.Append("<path fill=\"").Append(Color(HexColor.Opaque(pen.Fill))).Append('"').Append(HexColor.Alpha(pen.Fill) is var fa && fa < 0.999 ? $" fill-opacity=\"{F(fa)}\"" : "").Append(" fill-rule=\"evenodd\" d=\"").Append(d).Append("\"/>");
            }
            if (pen.Stroke != null && pen.Segments.Count > 0)
            {
                var d = new StringBuilder();
                foreach (var (a, e) in pen.Segments)
                {
                    // A dot: a short stroke, its round caps make it round (as the PDF draws it).
                    var q = (e - a).Length < 1e-9 ? e + new Vec2(0.01, 0) : e;
                    d.Append('M').Append(P(a)).Append('L').Append(P(q));
                }
                o.Append("<path fill=\"none\" stroke=\"").Append(Color(pen.Stroke)).Append("\" stroke-width=\"").Append(F(Math.Max(pen.Width, 0.05)))
                 .Append("\" stroke-linecap=\"").Append(cap).Append("\" stroke-linejoin=\"").Append(join).Append('"');
                if (pen.Dash is { Length: > 0 } dash) o.Append(" stroke-dasharray=\"").Append(string.Join(' ', dash.Select(F))).Append('"');
                o.Append(" d=\"").Append(d).Append("\"/>");
            }
            if (pen.Texts.Count > 0)
            {
                o.Append("<g fill=\"").Append(Color(pen.Stroke ?? "#000000")).Append("\">");
                foreach (var t in pen.Texts) Text(o, t);
                o.Append("</g>");
            }
        }
        if (picks is { Count: > 0 })
        {
            o.Append("<g class=\"lp-pick\" fill=\"#000\" fill-opacity=\"0\" stroke=\"#000\" stroke-opacity=\"0\" stroke-width=\"1.2\" stroke-linecap=\"round\" pointer-events=\"all\">");
            foreach (var pick in picks)
            {
                var d = new StringBuilder();
                foreach (var loop in pick.Loops)
                {
                    if (loop.Count < 3) continue;
                    d.Append('M').Append(P(loop[0]));
                    foreach (var p in loop.Skip(1)) d.Append('L').Append(P(p));
                    d.Append('Z');
                }
                foreach (var (a, b) in pick.Lines) d.Append('M').Append(P(a)).Append('L').Append(P(b));
                if (d.Length == 0) continue;
                o.Append("<path data-gid=\"").Append(Escape(pick.Id)).Append("\" d=\"").Append(d).Append("\"/>");
            }
            o.Append("</g>");
        }
        return o.Append("</svg>").ToString();
    }

    static void Pictures(StringBuilder o, Pen pen, Func<string, byte[]?>? image, ref int clips)
    {
        string? clip = null;
        if (pen.Clip is { } box)
        {
            clip = "lpc" + clips++;
            o.Append("<clipPath id=\"").Append(clip).Append("\"><rect x=\"").Append(F(box.X0)).Append("\" y=\"").Append(F(box.Y0))
             .Append("\" width=\"").Append(F(box.X1 - box.X0)).Append("\" height=\"").Append(F(box.Y1 - box.Y0)).Append("\"/></clipPath>");
        }
        o.Append(clip != null ? $"<g clip-path=\"url(#{clip})\">" : "<g>");
        foreach (var img in pen.Images)
        {
            var bytes = img.Data ?? image?.Invoke(img.ImageId);
            if (img.Corners.Length != 4 || bytes == null) continue;
            var mime = img.Mime ?? (bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8 ? "image/jpeg" : "image/png");
            // The image's unit square: (0, 0) its top left, (1, 0) its top right, (0, 1) its bottom left.
            var (tl, tr, bl) = (img.Corners[0], img.Corners[1], img.Corners[3]);
            o.Append("<image width=\"1\" height=\"1\" preserveAspectRatio=\"none\" transform=\"matrix(")
             .Append(F(tr.X - tl.X)).Append(' ').Append(F(tr.Y - tl.Y)).Append(' ').Append(F(bl.X - tl.X)).Append(' ').Append(F(bl.Y - tl.Y)).Append(' ')
             .Append(F(tl.X)).Append(' ').Append(F(tl.Y)).Append(")\"");
            if (img.Opacity < 0.999) o.Append(" opacity=\"").Append(F(img.Opacity)).Append('"');
            o.Append(" href=\"data:").Append(mime).Append(";base64,").Append(Convert.ToBase64String(bytes)).Append("\"/>");
        }
        o.Append("</g>");
    }

    static void Text(StringBuilder o, PenText t)
    {
        // The cap height is the text's height (paper mm): Helvetica's is 0.718 em, as the PDF sets it.
        var size = t.Height / 0.718;
        var anchor = t.Anchor switch { "middle" => "middle", "end" => "end", _ => "start" };
        // A text centred on its point: its baseline half its height below, along the text's own up.
        var at = t.At;
        if (t.Baseline == "middle")
        {
            var rad = t.Angle * Math.PI / 180;
            at = new Vec2(at.X + Math.Sin(rad) * t.Height / 2, at.Y + Math.Cos(rad) * t.Height / 2);
        }
        o.Append("<text x=\"").Append(F(at.X)).Append("\" y=\"").Append(F(at.Y)).Append("\" font-size=\"").Append(F(size))
         .Append("\" font-family=\"").Append(t.Font == "times" ? "Times New Roman,Times,serif" : "Helvetica,Arial,sans-serif").Append('"');
        if (anchor != "start") o.Append(" text-anchor=\"").Append(anchor).Append('"');
        if (t.Bold) o.Append(" font-weight=\"bold\"");
        // Italic: the same lean as the PDF's (12°).
        if (t.Italic) o.Append(" font-style=\"italic\"");
        // Counter-clockwise on paper, whose y runs down: SVG turns the other way.
        if (Math.Abs(t.Angle) > 1e-9) o.Append(" transform=\"rotate(").Append(F(-t.Angle)).Append(' ').Append(F(at.X)).Append(' ').Append(F(at.Y)).Append(")\"");
        o.Append('>').Append(Escape(t.Text)).Append("</text>");
    }

    static string P(Vec2 p) => F(p.X) + " " + F(p.Y);

    /// <summary>A length on paper: a hundredth of a millimetre is finer than any printer.</summary>
    static string F(double v) => double.IsFinite(v) ? Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture) : "0";

    static string Color(string css) => Escape(css);

    /// <summary>Text and attribute values as XML takes them.</summary>
    public static string Escape(string s)
    {
        var b = new StringBuilder(s.Length);
        foreach (var ch in s)
            switch (ch)
            {
                case '<': b.Append("&lt;"); break;
                case '>': b.Append("&gt;"); break;
                case '&': b.Append("&amp;"); break;
                case '"': b.Append("&quot;"); break;
                // XML 1.0 has no other control characters.
                case < ' ' when ch is not ('\t' or '\n' or '\r'): break;
                default: b.Append(ch); break;
            }
        return b.ToString();
    }
}
