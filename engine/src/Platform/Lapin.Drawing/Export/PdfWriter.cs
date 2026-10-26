using System.Globalization;
using System.IO.Compression;
using System.Text;
using Lapin.Geometry;
using Lapin.Drawing.Views;

namespace Lapin.Drawing.Export;

/// <summary>
/// Pages as a PDF (after BubbleBIM Pro's writer): vector only, no library.
/// Fills even-odd, lines with their colour, paper width and dashes, texts in
/// the PDF standard Helvetica with the Romanian letters added to its encoding.
/// </summary>
public static class PdfWriter
{
    const double Pt = 72 / 25.4;

    public static byte[] Write(IReadOnlyList<PaperPage> pages)
    {
        var streams = pages.Select(p => Deflate(Encoding.Latin1.GetBytes(Content(p)))).ToList();
        return File(pages, streams);
    }

    static string F(double v) => double.IsFinite(v) ? v.ToString("0.###", CultureInfo.InvariantCulture) : "0";

    static (double R, double G, double B) Rgb(string? css)
    {
        if (css is { Length: 7 } && css[0] == '#' && int.TryParse(css[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var n))
            return (((n >> 16) & 255) / 255.0, ((n >> 8) & 255) / 255.0, (n & 255) / 255.0);
        return (0, 0, 0);
    }

    static string Content(PaperPage page)
    {
        // Paper y runs down; PDF's runs up.
        string X(double mm) => F(mm * Pt);
        string Y(double mm) => F((page.H - mm) * Pt);
        // Round caps and joins, or projecting square caps with mitred joins.
        var c = new StringBuilder(page.LineCap == "square" ? "2 J 0 j\n" : "1 J 1 j\n");
        foreach (var pen in page.Pens)
        {
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

    static byte[] File(IReadOnlyList<PaperPage> pages, List<byte[]> streams)
    {
        // 1 catalog, 2 pages, 3 and 4 the fonts, then a page and its content for each.
        var objects = new List<byte[]>();
        void Obj(string s) => objects.Add(Encoding.ASCII.GetBytes(s));
        var encoding = $"<< /Type /Encoding /BaseEncoding /WinAnsiEncoding /Differences {Differences} >>";
        Obj("<< /Type /Catalog /Pages 2 0 R >>");
        Obj($"<< /Type /Pages /Kids [{string.Join(' ', pages.Select((_, i) => $"{5 + 2 * i} 0 R"))}] /Count {pages.Count} >>");
        Obj($"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding {encoding} >>");
        Obj($"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding {encoding} >>");
        for (var i = 0; i < pages.Count; i++)
        {
            Obj($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(pages[i].W * Pt)} {F(pages[i].H * Pt)}] /Contents {6 + 2 * i} 0 R /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> >>");
            objects.Add([.. Encoding.ASCII.GetBytes($"<< /Length {streams[i].Length} /Filter /FlateDecode >>\nstream\n"), .. streams[i], .. Encoding.ASCII.GetBytes("\nendstream")]);
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
