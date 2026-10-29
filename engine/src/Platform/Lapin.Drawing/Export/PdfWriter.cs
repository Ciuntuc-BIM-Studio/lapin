using System.Globalization;
using System.IO.Compression;
using System.Text;
using Lapin.Geometry;
using Lapin.Drawing.Views;

namespace Lapin.Drawing.Export;

/// <summary>
/// Pages as a PDF (after BubbleBIM Pro's writer): vectors, no library.
/// Fills even-odd, lines with their colour, paper width and dashes, texts in
/// the PDF standard Helvetica with the Romanian letters added to its encoding;
/// pictures (an orthophoto) as image XObjects laid by their corners, clipped
/// to their pen's box, as transparent as they are drawn.
/// </summary>
public static class PdfWriter
{
    const double Pt = 72 / 25.4;

    /// <summary>The pages; <paramref name="image"/> gives a picture's file (PNG or JPEG) by its id, and a picture without one is left out.</summary>
    public static byte[] Write(IReadOnlyList<PaperPage> pages, Func<string, byte[]?>? image = null)
    {
        var pictures = new List<Raster>();
        var index = new Dictionary<string, int>();
        var alphas = new List<double>();
        int? Picture(string id)
        {
            if (!index.TryGetValue(id, out var k))
            {
                var r = image?.Invoke(id) is { } bytes ? Raster.Read(bytes) : null;
                k = r != null ? pictures.Count : -1;
                if (r != null) pictures.Add(r);
                index[id] = k;
            }
            return k >= 0 ? k : null;
        }
        int Alpha(double a)
        {
            var k = alphas.FindIndex(x => Math.Abs(x - a) < 1e-3);
            if (k < 0) { alphas.Add(a); k = alphas.Count - 1; }
            return k;
        }
        var streams = pages.Select(p => Deflate(Encoding.Latin1.GetBytes(Content(p, Picture, Alpha)))).ToList();
        return File(pages, streams, pictures, alphas);
    }

    static string F(double v) => double.IsFinite(v) ? v.ToString("0.###", CultureInfo.InvariantCulture) : "0";

    static (double R, double G, double B) Rgb(string? css)
    {
        if (css is { Length: 7 } && css[0] == '#' && int.TryParse(css[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var n))
            return (((n >> 16) & 255) / 255.0, ((n >> 8) & 255) / 255.0, (n & 255) / 255.0);
        return (0, 0, 0);
    }

    static string Content(PaperPage page, Func<string, int?> picture, Func<double, int> alpha)
    {
        // Paper y runs down; PDF's runs up.
        string X(double mm) => F(mm * Pt);
        string Y(double mm) => F((page.H - mm) * Pt);
        // Round caps and joins, or projecting square caps with mitred joins.
        var c = new StringBuilder(page.LineCap == "square" ? "2 J 0 j\n" : "1 J 1 j\n");
        foreach (var pen in page.Pens)
        {
            if (pen.Images.Count > 0)
            {
                c.Append("q\n");
                if (pen.Clip is { } box) c.Append(X(box.X0)).Append(' ').Append(Y(box.Y1)).Append(' ').Append(F((box.X1 - box.X0) * Pt)).Append(' ').Append(F((box.Y1 - box.Y0) * Pt)).Append(" re W n\n");
                foreach (var img in pen.Images)
                {
                    if (img.Corners.Length != 4 || picture(img.ImageId) is not { } k) continue;
                    // The image's unit square: (0, 0) its bottom left, (1, 0) its bottom right, (0, 1) its top left.
                    var (tl, br, bl) = (img.Corners[0], img.Corners[2], img.Corners[3]);
                    c.Append("q ");
                    if (img.Opacity < 0.999) c.Append("/GS").Append(alpha(img.Opacity)).Append(" gs ");
                    c.Append(F((br.X - bl.X) * Pt)).Append(' ').Append(F(-(br.Y - bl.Y) * Pt)).Append(' ').Append(F((tl.X - bl.X) * Pt)).Append(' ').Append(F(-(tl.Y - bl.Y) * Pt)).Append(' ')
                     .Append(X(bl.X)).Append(' ').Append(Y(bl.Y)).Append(" cm /Im").Append(k).Append(" Do Q\n");
                }
                c.Append("Q\n");
            }
            if (pen.Fill != null && pen.Loops.Count > 0)
            {
                var (r, g, b) = Rgb(pen.Fill);
                c.Append(F(r)).Append(' ').Append(F(g)).Append(' ').Append(F(b)).Append(" rg\n");
                foreach (var loop in pen.Loops)
                {
                    if (loop.Count < 3) continue;
                    c.Append(X(loop[0].X)).Append(' ').Append(Y(loop[0].Y)).Append(" m\n");
                    foreach (var p in loop.Skip(1)) c.Append(X(p.X)).Append(' ').Append(Y(p.Y)).Append(" l\n");
                    c.Append("h\n");
                }
                c.Append("f*\n");
            }
            if (pen.Stroke != null && pen.Segments.Count > 0)
            {
                var (r, g, b) = Rgb(pen.Stroke);
                var dash = pen.Dash is { Length: > 0 } d ? string.Join(' ', d.Select(v => F(v * Pt))) : "";
                c.Append(F(Math.Max(pen.Width, 0.05) * Pt)).Append(" w ").Append(F(r)).Append(' ').Append(F(g)).Append(' ').Append(F(b)).Append(" RG [").Append(dash).Append("] 0 d\n");
                foreach (var (a, e) in pen.Segments)
                {
                    // A dot: a short stroke, its round caps make it round.
                    var q = (e - a).Length < 1e-9 ? e + new Vec2(0.01, 0) : e;
                    c.Append(X(a.X)).Append(' ').Append(Y(a.Y)).Append(" m ").Append(X(q.X)).Append(' ').Append(Y(q.Y)).Append(" l S\n");
                }
            }
            if (pen.Texts.Count > 0)
            {
                var (r, g, b) = Rgb(pen.Stroke);
                c.Append("[] 0 d ").Append(F(r)).Append(' ').Append(F(g)).Append(' ').Append(F(b)).Append(" rg\n");
                foreach (var t in pen.Texts) Text(c, t, X, Y);
            }
        }
        return c.ToString();
    }

    static void Text(StringBuilder c, PenText t, Func<double, string> X, Func<double, string> Y)
    {
        // The cap height is the text's height (paper mm): Helvetica's is 0.718 em.
        var size = t.Height / 0.718;
        var width = Advance(t.Text, t.Bold) * size;
        var rad = t.Angle * Math.PI / 180;   // counter-clockwise on paper
        double cos = Math.Cos(rad), sin = Math.Sin(rad);
        var ox = t.Anchor switch { "middle" => -width / 2, "end" => -width, _ => 0 };
        var oy = t.Baseline == "middle" ? -t.Height / 2 : 0;
        // Along the text and up from its baseline; paper y down, so "up" is −y.
        var x = t.At.X + ox * cos - oy * sin;
        var y = t.At.Y - (ox * sin + oy * cos);
        c.Append("BT /").Append(t.Bold ? "F2" : "F1").Append(' ').Append(F(size * Pt)).Append(" Tf ")
         .Append(F(cos)).Append(' ').Append(F(sin)).Append(' ').Append(F(-sin)).Append(' ').Append(F(cos)).Append(' ')
         .Append(X(x)).Append(' ').Append(Y(y)).Append(" Tm (").Append(Encode(t.Text)).Append(") Tj ET\n");
    }

    /// <summary>Helvetica's advance widths (em) for the common letters, an average for the rest.</summary>
    public static double Advance(string s, bool bold)
    {
        double w = 0;
        foreach (var ch in s)
            w += ch switch
            {
                ' ' => 0.278, '.' or ',' or ':' or ';' or '!' or 'i' or 'j' or 'l' or 'I' or '|' => 0.24, 'f' or 't' or 'r' => 0.32,
                'm' or 'M' or 'W' or 'w' => 0.85, >= '0' and <= '9' => 0.556, >= 'A' and <= 'Z' => 0.68, '-' or '−' => 0.333, _ => 0.53,
            };
        return w * (bold ? 1.06 : 1);
    }

    /// <summary>Text in WinAnsi, the Romanian letters it lacks put in its unused codes (the font's /Differences names their glyphs).</summary>
    static readonly Dictionary<char, int> Extra = new()
    {
        ['ă'] = 0x81, ['Ă'] = 0x8D, ['ș'] = 0x8F, ['ş'] = 0x8F, ['Ș'] = 0x90, ['Ş'] = 0x90, ['ț'] = 0x9D, ['ţ'] = 0x9D, ['Ț'] = 0x7F, ['Ţ'] = 0x7F,
        ['„'] = 0x84, ['”'] = 0x94, ['“'] = 0x93, ['’'] = 0x92, ['‘'] = 0x91, ['–'] = 0x96, ['—'] = 0x97, ['…'] = 0x85, ['•'] = 0x95, ['€'] = 0x80, ['−'] = '-', ['·'] = 0xB7, ['×'] = 0xD7,
    };
    const string Differences = "[127 /Tcommaaccent 129 /abreve 141 /Abreve 143 /scommaaccent 144 /Scommaaccent 157 /tcommaaccent]";

    static string Encode(string s)
    {
        var b = new StringBuilder();
        foreach (var ch in s)
        {
            var code = Extra.TryGetValue(ch, out var e) ? e : ch < 256 ? ch : '?';
            if (code is '(' or ')' or '\\') b.Append('\\').Append((char)code);
            else if (code < 32 || code > 126) b.Append('\\').Append(Convert.ToString(code, 8).PadLeft(3, '0'));
            else b.Append((char)code);
        }
        return b.ToString();
    }

    static byte[] Deflate(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal)) z.Write(data);
        return ms.ToArray();
    }

    static byte[] File(IReadOnlyList<PaperPage> pages, List<byte[]> streams, List<Raster> pictures, List<double> alphas)
    {
        // 1 catalog, 2 pages, 3 and 4 the fonts, then a page and its content for each, then the pictures (each with its alpha, if any).
        var objects = new List<byte[]>();
        void Obj(string s) => objects.Add(Encoding.ASCII.GetBytes(s));
        var encoding = $"<< /Type /Encoding /BaseEncoding /WinAnsiEncoding /Differences {Differences} >>";
        Obj("<< /Type /Catalog /Pages 2 0 R >>");
        Obj($"<< /Type /Pages /Kids [{string.Join(' ', pages.Select((_, i) => $"{5 + 2 * i} 0 R"))}] /Count {pages.Count} >>");
        Obj($"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding {encoding} >>");
        Obj($"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding {encoding} >>");
        var first = 5 + 2 * pages.Count;
        var pictureIds = new List<int>();
        for (int k = 0, next = first; k < pictures.Count; k++) { pictureIds.Add(next); next += pictures[k].Alpha != null ? 2 : 1; }
        var xobjects = pictures.Count == 0 ? "" : $" /XObject << {string.Join(' ', pictureIds.Select((id, k) => $"/Im{k} {id} 0 R"))} >>";
        var states = alphas.Count == 0 ? "" : $" /ExtGState << {string.Join(' ', alphas.Select((a, k) => $"/GS{k} << /ca {F(a)} /CA {F(a)} >>"))} >>";
        byte[] Stream(string dict, byte[] data) => [.. Encoding.ASCII.GetBytes($"<< /Length {data.Length} {dict} >>\nstream\n"), .. data, .. Encoding.ASCII.GetBytes("\nendstream")];
        for (var i = 0; i < pages.Count; i++)
        {
            Obj($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(pages[i].W * Pt)} {F(pages[i].H * Pt)}] /Contents {6 + 2 * i} 0 R /Resources << /Font << /F1 3 0 R /F2 4 0 R >>{xobjects}{states} >> >>");
            objects.Add(Stream("/Filter /FlateDecode", streams[i]));
        }
        for (var k = 0; k < pictures.Count; k++)
        {
            var r = pictures[k];
            var image = $"/Type /XObject /Subtype /Image /Width {r.W} /Height {r.H} /ColorSpace /{r.ColorSpace} /BitsPerComponent 8";
            if (r.Jpeg) objects.Add(Stream(image + " /Filter /DCTDecode", r.Data));
            else
            {
                objects.Add(Stream(image + (r.Alpha != null ? $" /SMask {pictureIds[k] + 1} 0 R" : "") + " /Filter /FlateDecode", Deflate(r.Data)));
                if (r.Alpha != null) objects.Add(Stream($"/Type /XObject /Subtype /Image /Width {r.W} /Height {r.H} /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode", Deflate(r.Alpha)));
            }
        }
        using var f = new MemoryStream();
        void W(string s) => f.Write(Encoding.ASCII.GetBytes(s));
        W("%PDF-1.4\n");
        f.Write([(byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n']);
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(f.Position);
            W($"{i + 1} 0 obj\n");
            f.Write(objects[i]);
            W("\nendobj\n");
        }
        var xref = f.Position;
        W($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) W($"{o:D10} 00000 n \n");
        W($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return f.ToArray();
    }
}
