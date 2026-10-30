using System.Globalization;
using System.Text;

namespace Lapin.Platform.Cde.PdfRemarks;

/// <summary>
/// PDF text strings (§7.9.2) as .NET strings: UTF-16BE with its byte order
/// mark (what Acrobat and Bluebeam write for anything beyond ASCII), UTF-8
/// with a mark (PDF 2.0), else PDFDocEncoding (Latin-1 with its own
/// characters in 0x18–0x1F and 0x80–0xAD); and PDF dates
/// (<c>D:YYYYMMDDHHmmSSOHH'mm'</c>, §7.9.4) as ISO 8601.
/// </summary>
public static class PdfText
{
    public static string Decode(byte[] b)
    {
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Clean(Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2));
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Clean(Encoding.Unicode.GetString(b, 2, b.Length - 2));
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Clean(Encoding.UTF8.GetString(b, 3, b.Length - 3));
        var sb = new StringBuilder(b.Length);
        foreach (var x in b) sb.Append(PdfDoc(x));
        return Clean(sb.ToString());
    }

    /// <summary>Line ends as \n, the null characters some writers pad with gone.</summary>
    static string Clean(string s) => s.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\0", "");

    /// <summary>PDFDocEncoding (Annex D): where it differs from Latin-1.</summary>
    static char PdfDoc(byte x) => x switch
    {
        0x18 => '˘', 0x19 => 'ˇ', 0x1A => 'ˆ', 0x1B => '˙', 0x1C => '˝', 0x1D => '˛', 0x1E => '˚', 0x1F => '˜',
        0x80 => '•', 0x81 => '†', 0x82 => '‡', 0x83 => '…', 0x84 => '—', 0x85 => '–', 0x86 => 'ƒ', 0x87 => '⁄',
        0x88 => '‹', 0x89 => '›', 0x8A => '−', 0x8B => '‰', 0x8C => '„', 0x8D => '“', 0x8E => '”', 0x8F => '‘',
        0x90 => '’', 0x91 => '‚', 0x92 => '™', 0x93 => 'ﬁ', 0x94 => 'ﬂ', 0x95 => 'Ł', 0x96 => 'Œ', 0x97 => 'Š',
        0x98 => 'Ÿ', 0x99 => 'Ž', 0x9A => 'ı', 0x9B => 'ł', 0x9C => 'œ', 0x9D => 'š', 0x9E => 'ž', 0xA0 => '€',
        0x9F or 0xAD => '�',
        _ => (char)x,
    };

    /// <summary>A PDF date as ISO 8601 (the offset kept when given, else none); null when it is not one. "D:" optional; a partial date fills with the first month, day, hour….</summary>
    public static string? Date(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        if (t.StartsWith("D:", StringComparison.Ordinal)) t = t[2..];
        var digits = new StringBuilder();
        var i = 0;
        while (i < t.Length && char.IsAsciiDigit(t[i]) && digits.Length < 14) digits.Append(t[i++]);
        if (digits.Length < 4) return null;
        var d = digits.ToString().PadRight(14, '0');
        int Part(int at, int len, int min, int max) { var v = int.Parse(d.Substring(at, len), CultureInfo.InvariantCulture); return Math.Clamp(v, min, max); }
        int year = Part(0, 4, 1, 9999), month = Part(4, 2, 1, 12), day = Part(6, 2, 1, DateTime.DaysInMonth(year, Part(4, 2, 1, 12)));
        int hour = Part(8, 2, 0, 23), minute = Part(10, 2, 0, 59), second = Part(12, 2, 0, 59);
        var iso = $"{year:D4}-{month:D2}-{day:D2}T{hour:D2}:{minute:D2}:{second:D2}";
        var rest = t[i..];
        if (rest.Length > 0 && rest[0] is '+' or '-' or 'Z')
        {
            if (rest[0] == 'Z') return iso + "Z";
            var sign = rest[0];
            var oh = rest.Length >= 3 && char.IsAsciiDigit(rest[1]) && char.IsAsciiDigit(rest[2]) ? rest.Substring(1, 2) : "00";
            var om = rest.Length >= 6 && char.IsAsciiDigit(rest[4]) && char.IsAsciiDigit(rest[5]) ? rest.Substring(4, 2) : "00";
            return $"{iso}{sign}{oh}:{om}";
        }
        return iso;
    }
}
