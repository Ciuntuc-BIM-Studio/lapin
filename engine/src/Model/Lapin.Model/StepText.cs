using System.Globalization;
using System.Text;

namespace Lapin.Model;

/// <summary>
/// STEP's string escapes (ISO 10303-21), the one decoder the IFC reader and the reference layers both use.
/// </summary>
public static class StepText
{
    /// <summary>A STEP string's escapes: \X2\…\X0\ (UTF-16), \X4\…\X0\, \X\hh (Latin-1), \S\c (high half), \\ (a backslash).</summary>
    public static string Decode(string s)
    {
        if (!s.Contains('\\')) return s;
        var o = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\') { o.Append(s[i]); continue; }
            if (s.AsSpan(i).StartsWith("\\X2\\") || s.AsSpan(i).StartsWith("\\X4\\"))
            {
                // \X2\ holds UTF-16 units (a character beyond the BMP as its two surrogates), \X4\ code points of 8 digits.
                var width = s[i + 2] == '2' ? 4 : 8;
                var end = s.IndexOf("\\X0\\", i + 4, StringComparison.Ordinal);
                if (end < 0 || (end - i - 4) % width != 0) { o.Append(s[i]); continue; }
                var run = new StringBuilder();
                var ok = true;
                for (var k = i + 4; k < end; k += width)
                {
                    if (!int.TryParse(s.AsSpan(k, width), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code)) { ok = false; break; }
                    if (width == 4) run.Append((char)code);
                    else if (Rune.IsValid(code)) run.Append(char.ConvertFromUtf32(code));
                    else run.Append('�');
                }
                if (!ok) { o.Append(s[i]); continue; } // malformed: kept as written
                o.Append(run);
                i = end + 3;
            }
            else if (s.AsSpan(i).StartsWith("\\X\\") && i + 5 <= s.Length
                && int.TryParse(s.AsSpan(i + 3, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var latin))
            {
                o.Append((char)latin);
                i += 4;
            }
            else if (s.AsSpan(i).StartsWith("\\S\\") && i + 3 < s.Length) { o.Append((char)(s[i + 3] + 128)); i += 3; }
            else if (i + 1 < s.Length && s[i + 1] == '\\') { o.Append('\\'); i++; }
            else if (s.AsSpan(i).StartsWith("\\P") && i + 3 < s.Length && s[i + 3] == '\\') i += 3; // code page switch
            else o.Append('\\');
        }
        return o.ToString();
    }
}
