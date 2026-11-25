using System.Globalization;
using System.Text;

namespace Lapin.Platform.Cde.PdfRemarks;

/// <summary>
/// A PDF font as text extraction needs it (ISO 32000-1 §9): how a shown string splits into character codes (one byte for simple
/// fonts; the codespace of a Type0 font's CMap, two bytes for Identity-H/V), each code's text (the ToUnicode CMap, else the
/// encoding with its Differences and glyph names), and each code's width in text space (Widths, W/DW, FontMatrix for Type3,
/// else an estimate for the standard fonts). The font programs are never read.
/// </summary>
internal sealed class PdfFont
{
    private readonly Dictionary<int, string> _unicode = [];
    private readonly string?[] _simple = new string?[256];
    private readonly Dictionary<int, double> _widths = [];
    private readonly List<(int Length, uint Low, uint High)> _codespace = [];
    private readonly double _defaultWidth;
    private readonly double _scale = 0.001;
    private readonly bool _composite;
    private readonly bool _monospace;

    /// <summary>A font for strings shown with no font set (a broken file): one byte per code, Latin-1.</summary>
    public static readonly PdfFont Fallback = new();

    private PdfFont()
    {
        for (int i = 0; i < 256; i++) { _simple[i] = ((char)i).ToString(); }
        _defaultWidth = 500;
    }

    public PdfFont(PdfDocument doc, PdfDict dict)
    {
        string subtype = doc.Name(dict["Subtype"]) ?? "Type1";
        string baseFont = doc.Name(dict["BaseFont"]) ?? string.Empty;
        _composite = subtype == "Type0";
        _monospace = baseFont.Contains("Courier", StringComparison.OrdinalIgnoreCase) || baseFont.Contains("Mono", StringComparison.OrdinalIgnoreCase);
        if (doc.Resolve(dict["ToUnicode"]) is PdfStream tu && doc.Decoded(tu) is { } cmap) { ReadCMap(cmap, _unicode, null); }

        if (_composite)
        {
            object? encoding = doc.Resolve(dict["Encoding"]);
            if (encoding is PdfStream es && doc.Decoded(es) is { } ecmap) { ReadCMap(ecmap, null, _codespace); }
            if (_codespace.Count == 0) { _codespace.Add((2, 0, 0xFFFF)); }
            PdfDict? descendant = doc.Array(dict["DescendantFonts"]) is { Items.Count: > 0 } kids ? doc.Dict(kids.Items[0]) : null;
            _defaultWidth = descendant != null && doc.Number(descendant["DW"]) is { } dw ? dw : 1000;
            if (descendant != null && doc.Array(descendant["W"]) is { } w) { ReadW(doc, w); }
            return;
        }

        // A simple font: its encoding, then its Differences, then ToUnicode wins where it has an entry
        PdfObj? enc = doc.Resolve(dict["Encoding"]);
        string? baseEncoding = enc is PdfName n ? n.Value : enc is PdfDict ed ? doc.Name(ed["BaseEncoding"]) : null;
        bool symbolic = baseFont.Contains("Symbol", StringComparison.OrdinalIgnoreCase) || baseFont.Contains("Dingbat", StringComparison.OrdinalIgnoreCase);
        for (int i = 0; i < 256; i++)
        {
            _simple[i] = baseEncoding switch
            {
                "StandardEncoding" => Standard(i),
                _ => WinAnsi(i)
            };
        }
        if (symbolic && baseEncoding == null) { for (int i = 0; i < 256; i++) { _simple[i] = null; } }
        if (enc is PdfDict encDict && doc.Array(encDict["Differences"]) is { } diffs)
        {
            int code = 0;
            foreach (PdfObj item in diffs.Items)
            {
                PdfObj? o = doc.Resolve(item);
                if (o is PdfNumber num) { code = (int)num.Value; }
                else if (o is PdfName glyph)
                {
                    if (code is >= 0 and < 256) { _simple[code] = GlyphText(glyph.Value); }
                    code++;
                }
            }
        }

        if (subtype == "Type3" && doc.Numbers(dict["FontMatrix"]) is { Length: 6 } fm) { _scale = fm[0]; }
        PdfDict? descriptor = doc.Dict(dict["FontDescriptor"]);
        double missing = descriptor != null && doc.Number(descriptor["MissingWidth"]) is { } mw ? mw : 0;
        if (doc.Array(dict["Widths"]) is { } widths)
        {
            int first = (int)(doc.Number(dict["FirstChar"]) ?? 0);
            for (int i = 0; i < widths.Items.Count; i++)
            {
                if (doc.Number(widths.Items[i]) is { } wv) { _widths[first + i] = wv; }
            }
            _defaultWidth = missing > 0 ? missing : 500;
        }
        else { _defaultWidth = missing > 0 ? missing : -1; }   // -1: estimate from the character
    }

    /// <summary>The codes of a shown string with their text (empty when unknown), width in text space (font size 1) and whether the code is a single-byte 32 (word spacing applies).</summary>
    public IEnumerable<(string Text, double Width, bool WordSpace)> Decode(byte[] bytes)
    {
        int i = 0;
        while (i < bytes.Length)
        {
            int code, length = 1;
            if (_composite)
            {
                length = CodeLength(bytes, i);
                code = 0;
                for (int k = 0; k < length && i + k < bytes.Length; k++) { code = (code << 8) | bytes[i + k]; }
            }
            else { code = bytes[i]; }
            i += length;
            // A code without text still is a character drawn (counted for the text layer), shown as U+FFFD
            string text = _unicode.TryGetValue(code, out string? u) ? u : _composite ? "�" : _simple[code & 255] ?? "�";
            double w = _widths.TryGetValue(code, out double known) ? known : _defaultWidth >= 0 ? _defaultWidth : Estimate(text);
            yield return (text, w * _scale, !_composite && code == 32);
        }
    }

    private int CodeLength(byte[] bytes, int at)
    {
        for (int length = 1; length <= 4; length++)
        {
            if (at + length > bytes.Length) { break; }
            uint value = 0;
            for (int k = 0; k < length; k++) { value = (value << 8) | bytes[at + k]; }
            foreach ((int l, uint low, uint high) in _codespace)
            {
                if (l == length && value >= low && value <= high) { return length; }
            }
        }
        return Math.Min(_codespace.Min(c => c.Length), bytes.Length - at);
    }

    /// <summary>A glyph's width (1/1000 em) guessed from its character, for a standard font without Widths.</summary>
    private double Estimate(string text)
    {
        if (_monospace) { return 600; }
        char c = text.Length > 0 ? text[0] : 'n';
        if (c == ' ') { return 278; }
        if ("iljI.,:;'!|".Contains(c)) { return 250; }
        if ("ftr()[]-\"".Contains(c)) { return 333; }
        if ("mwMW".Contains(c)) { return c is 'W' ? 944 : 833; }
        if (char.IsUpper(c)) { return 690; }
        return 556;
    }

    private void ReadW(PdfDocument doc, PdfArray w)
    {
        List<PdfObj> items = w.Items;
        for (int i = 0; i < items.Count;)
        {
            if (doc.Number(items[i]) is not { } first) { i++; continue; }
            if (i + 1 < items.Count && doc.Resolve(items[i + 1]) is PdfArray list)
            {
                for (int k = 0; k < list.Items.Count && k < 65536; k++)
                {
                    if (doc.Number(list.Items[k]) is { } wv) { _widths[(int)first + k] = wv; }
                }
                i += 2;
            }
            else if (i + 2 < items.Count && doc.Number(items[i + 1]) is { } last && doc.Number(items[i + 2]) is { } wv)
            {
                for (int c = (int)first; c <= (int)last && c - first < 65536; c++) { _widths[c] = wv; }
                i += 3;
            }
            else { break; }
        }
    }

    // ---------------------------------------------------------------- CMaps

    /// <summary>A CMap's bfchar and bfrange (into <paramref name="unicode"/>) and its codespace ranges (into <paramref name="codespace"/>).</summary>
    internal static void ReadCMap(byte[] data, Dictionary<int, string>? unicode, List<(int Length, uint Low, uint High)>? codespace)
    {
        var lexer = new PdfLexer(data);
        var operands = new List<PdfObj>();
        string mode = string.Empty;
        int guard = 0;
        while (lexer.Pos < lexer.Length && guard++ < 2_000_000)
        {
            PdfObj o = lexer.Parse();
            if (o is PdfKeyword { Value: "" }) { break; }
            if (o is PdfKeyword k)
            {
                switch (k.Value)
                {
                    case "begincodespacerange": case "beginbfchar": case "beginbfrange": case "begincidrange": mode = k.Value; operands.Clear(); break;
                    case "endcodespacerange":
                        for (int i = 0; i + 1 < operands.Count; i += 2)
                        {
                            if (operands[i] is PdfString lo && operands[i + 1] is PdfString hi && lo.Bytes.Length is >= 1 and <= 4)
                            {
                                codespace?.Add((lo.Bytes.Length, Value(lo.Bytes), Value(hi.Bytes)));
                            }
                        }
                        mode = string.Empty; operands.Clear(); break;
                    case "endbfchar":
                        for (int i = 0; i + 1 < operands.Count; i += 2)
                        {
                            if (operands[i] is PdfString src && operands[i + 1] is PdfString dst && unicode != null) { unicode[(int)Value(src.Bytes)] = Utf16(dst.Bytes); }
                        }
                        mode = string.Empty; operands.Clear(); break;
                    case "endbfrange":
                        for (int i = 0; i + 2 < operands.Count; i += 3)
                        {
                            if (operands[i] is not PdfString lo || operands[i + 1] is not PdfString hi || unicode == null) { continue; }
                            uint low = Value(lo.Bytes), high = Value(hi.Bytes);
                            if (high < low || high - low > 65535) { continue; }
                            if (operands[i + 2] is PdfString dst)
                            {
                                byte[] b = (byte[])dst.Bytes.Clone();
                                for (uint c = low; c <= high; c++)
                                {
                                    unicode[(int)c] = Utf16(b);
                                    Increment(b);
                                }
                            }
                            else if (operands[i + 2] is PdfArray list)
                            {
                                for (int k2 = 0; k2 < list.Items.Count && low + k2 <= high; k2++)
                                {
                                    if (list.Items[k2] is PdfString s) { unicode[(int)(low + k2)] = Utf16(s.Bytes); }
                                }
                            }
                        }
                        mode = string.Empty; operands.Clear(); break;
                    default: if (mode.Length == 0) { operands.Clear(); } break;
                }
                continue;
            }
            if (mode.Length > 0) { operands.Add(o); }
        }
    }

    private static uint Value(byte[] b)
    {
        uint v = 0;
        foreach (byte x in b.Take(4)) { v = (v << 8) | x; }
        return v;
    }

    private static void Increment(byte[] b)
    {
        for (int i = b.Length - 1; i >= 0; i--)
        {
            if (++b[i] != 0) { return; }
        }
    }

    private static string Utf16(byte[] b)
    {
        if (b.Length == 1) { return ((char)b[0]).ToString(); }
        string s = Encoding.BigEndianUnicode.GetString(b, 0, b.Length - b.Length % 2);
        return s.Replace("\0", string.Empty);
    }

    // ---------------------------------------------------------------- Encodings and glyph names

    private static readonly char[] WIN_ANSI_HIGH =
    [
        '€', '\0', '‚', 'ƒ', '„', '…', '†', '‡', 'ˆ', '‰', 'Š', '‹', 'Œ', '\0', 'Ž', '\0',
        '\0', '‘', '’', '“', '”', '•', '–', '—', '˜', '™', 'š', '›', 'œ', '\0', 'ž', 'Ÿ'
    ];

    private static string? WinAnsi(int code)
    {
        if (code is >= 0x80 and <= 0x9F) { return WIN_ANSI_HIGH[code - 0x80] is '\0' ? null : WIN_ANSI_HIGH[code - 0x80].ToString(); }
        if (code < 32) { return null; }
        return code == 0xA0 ? " " : ((char)code).ToString();
    }

    private static string? Standard(int code) => code switch
    {
        0x27 => "’", 0x60 => "‘", < 32 => null, < 0x80 => ((char)code).ToString(), _ => WinAnsi(code)
    };

    private static readonly Dictionary<string, string> NAMES = new(StringComparer.Ordinal)
    {
        ["space"] = " ", ["exclam"] = "!", ["quotedbl"] = "\"", ["numbersign"] = "#", ["dollar"] = "$", ["percent"] = "%", ["ampersand"] = "&",
        ["quotesingle"] = "'", ["parenleft"] = "(", ["parenright"] = ")", ["asterisk"] = "*", ["plus"] = "+", ["comma"] = ",", ["hyphen"] = "-",
        ["period"] = ".", ["slash"] = "/", ["zero"] = "0", ["one"] = "1", ["two"] = "2", ["three"] = "3", ["four"] = "4", ["five"] = "5", ["six"] = "6",
        ["seven"] = "7", ["eight"] = "8", ["nine"] = "9", ["colon"] = ":", ["semicolon"] = ";", ["less"] = "<", ["equal"] = "=", ["greater"] = ">",
        ["question"] = "?", ["at"] = "@", ["bracketleft"] = "[", ["backslash"] = "\\", ["bracketright"] = "]", ["asciicircum"] = "^", ["underscore"] = "_",
        ["grave"] = "`", ["braceleft"] = "{", ["bar"] = "|", ["braceright"] = "}", ["asciitilde"] = "~", ["quoteright"] = "’", ["quoteleft"] = "‘",
        ["endash"] = "–", ["emdash"] = "—", ["bullet"] = "•", ["quotedblleft"] = "“", ["quotedblright"] = "”", ["quotedblbase"] = "„",
        ["quotesinglbase"] = "‚", ["ellipsis"] = "…", ["degree"] = "°", ["plusminus"] = "±", ["multiply"] = "×", ["divide"] = "÷", ["Euro"] = "€",
        ["copyright"] = "©", ["registered"] = "®", ["trademark"] = "™", ["section"] = "§", ["paragraph"] = "¶", ["periodcentered"] = "·", ["minus"] = "−",
        ["fi"] = "fi", ["fl"] = "fl", ["ff"] = "ff", ["ffi"] = "ffi", ["ffl"] = "ffl", ["nbspace"] = " ", ["nonbreakingspace"] = " ", ["guillemotleft"] = "«",
        ["guillemotright"] = "»", ["guilsinglleft"] = "‹", ["guilsinglright"] = "›", ["dotlessi"] = "ı", ["Lslash"] = "Ł", ["lslash"] = "ł", ["Oslash"] = "Ø",
        ["oslash"] = "ø", ["AE"] = "Æ", ["ae"] = "æ", ["OE"] = "Œ", ["oe"] = "œ", ["germandbls"] = "ß", ["Eth"] = "Ð", ["eth"] = "ð", ["Thorn"] = "Þ",
        ["thorn"] = "þ", ["Dcroat"] = "Đ", ["dcroat"] = "đ", ["mu"] = "µ", ["onehalf"] = "½", ["onequarter"] = "¼", ["threequarters"] = "¾",
        ["twosuperior"] = "²", ["threesuperior"] = "³", ["onesuperior"] = "¹", ["exclamdown"] = "¡", ["questiondown"] = "¿", ["cent"] = "¢",
        ["sterling"] = "£", ["yen"] = "¥", ["florin"] = "ƒ", ["dagger"] = "†", ["daggerdbl"] = "‡", ["perthousand"] = "‰", ["circumflex"] = "ˆ",
        ["tilde"] = "˜", ["macron"] = "¯", ["acute"] = "´", ["cedilla"] = "¸", ["dieresis"] = "¨", ["ordfeminine"] = "ª", ["ordmasculine"] = "º",
        ["logicalnot"] = "¬", ["brokenbar"] = "¦", ["currency"] = "¤", ["diameter"] = "Ø", ["uni00A0"] = " "
    };

    // Letters with an accent, by the accent's glyph-name suffix: the base letters and their accented forms, position by position
    private static readonly (string Accent, string Bases, string Accented)[] ACCENTS =
    [
        ("acute", "AEIOUYaeiouyCcNnSsZzLlRr", "ÁÉÍÓÚÝáéíóúýĆćŃńŚśŹźĹĺŔŕ"),
        ("grave", "AEIOUaeiou", "ÀÈÌÒÙàèìòù"),
        ("circumflex", "AEIOUaeiouCcGgHhJjSsWwYy", "ÂÊÎÔÛâêîôûĈĉĜĝĤĥĴĵŜŝŴŵŶŷ"),
        ("tilde", "ANOanoIiUu", "ÃÑÕãñõĨĩŨũ"),
        ("dieresis", "AEIOUaeiouyY", "ÄËÏÖÜäëïöüÿŸ"),
        ("ring", "AaUu", "ÅåŮů"),
        ("cedilla", "CcSsTtGgKkLlNnRr", "ÇçŞşŢţĢģĶķĻļŅņŖŗ"),
        ("caron", "CcDdEeNnRrSsTtZzLl", "ČčĎďĚěŇňŘřŠšŤťŽžĽľ"),
        ("breve", "AaGgUuEeIiOo", "ĂăĞğŬŭĔĕĬĭŎŏ"),
        ("macron", "AaEeIiOoUu", "ĀāĒēĪīŌōŪū"),
        ("ogonek", "AaEeIiUu", "ĄąĘęĮįŲų"),
        ("dotaccent", "CcEeGgIZz", "ĊċĖėĠġİŻż"),
        ("commaaccent", "SsTtGgKkLlNnRr", "ȘșȚțĢģĶķĻļŅņŖŗ"),
        ("hungarumlaut", "OoUu", "ŐőŰű")
    ];

    /// <summary>A glyph name's text (§9.10.2 and the Adobe Glyph List's conventions): known names, letters, accented letters, uniXXXX and uXXXX[XX].</summary>
    internal static string GlyphText(string name)
    {
        int dot = name.IndexOf('.');
        if (dot > 0) { name = name[..dot]; }
        if (name.Contains('_')) { return string.Concat(name.Split('_').Select(GlyphText)); }
        if (name.Length == 1) { return name; }
        if (NAMES.TryGetValue(name, out string? known)) { return known; }
        if (name.StartsWith("uni", StringComparison.Ordinal) && name.Length >= 7 && (name.Length - 3) % 4 == 0)
        {
            var sb = new StringBuilder();
            for (int i = 3; i + 4 <= name.Length; i += 4)
            {
                if (!int.TryParse(name.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) { return string.Empty; }
                sb.Append((char)v);
            }
            return sb.ToString();
        }
        if (name.Length is >= 5 and <= 7 && name[0] == 'u' && int.TryParse(name.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp) && cp <= 0x10FFFF)
        {
            return char.ConvertFromUtf32(cp is >= 0xD800 and <= 0xDFFF ? 0xFFFD : cp);
        }
        foreach ((string accent, string bases, string accented) in ACCENTS)
        {
            if (name.Length == accent.Length + 1 && name.EndsWith(accent, StringComparison.Ordinal) && bases.IndexOf(name[0]) is int at and >= 0) { return accented[at].ToString(); }
        }
        return string.Empty;
    }
}
