using System.Globalization;
using System.Text;

namespace Lapin.Platform.Cde.PdfRemarks;

/// <summary>
/// The PDF object model as the remark reader needs it (ISO 32000-1 §7.3):
/// null, booleans, numbers, strings (bytes: decoded later by
/// <see cref="PdfText"/>), names, arrays, dictionaries, streams (their raw
/// bytes, decoded on demand by the document) and indirect references.
/// Nothing is rendered: no fonts, no content streams, no images.
/// </summary>
public abstract record PdfObj;

public sealed record PdfNull : PdfObj { public static readonly PdfNull Value = new(); }
public sealed record PdfBool(bool Value) : PdfObj;
public sealed record PdfNumber(double Value) : PdfObj;
public sealed record PdfString(byte[] Bytes) : PdfObj;
public sealed record PdfName(string Value) : PdfObj;
public sealed record PdfArray(List<PdfObj> Items) : PdfObj;
public sealed record PdfRef(int Num, int Gen) : PdfObj;

/// <summary>A keyword the lexer met where an object was expected (obj, endobj, stream, R, xref, trailer…); empty at the end of the data.</summary>
public sealed record PdfKeyword(string Value) : PdfObj;

public sealed record PdfDict(Dictionary<string, PdfObj> Map) : PdfObj
{
    public PdfObj? this[string key] => Map.GetValueOrDefault(key);
    public bool Has(string key) => Map.ContainsKey(key);
    public static PdfDict Empty() => new(new Dictionary<string, PdfObj>());
}

/// <summary>A stream: its dictionary and its raw (still encoded) bytes.</summary>
public sealed record PdfStream(PdfDict Dict, byte[] Raw) : PdfObj;

/// <summary>
/// A tokenizer and object parser over the file's bytes (§7.2): whitespace
/// and comments skipped, literal strings with their escapes and nested
/// parentheses, hex strings, names with #xx, numbers, the delimiters. It
/// parses one object at a time from a position and never throws on
/// malformed input: what cannot be read becomes null or a keyword.
/// </summary>
public sealed class PdfLexer(byte[] data, int pos = 0)
{
    readonly byte[] _d = data;
    public int Pos = pos;
    public int Length => _d.Length;

    public static bool IsWhite(byte b) => b is 0 or 9 or 10 or 12 or 13 or 32;
    public static bool IsDelim(byte b) => b is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';
    public static bool IsRegular(byte b) => !IsWhite(b) && !IsDelim(b);

    public void SkipWhitespace()
    {
        while (Pos < _d.Length)
        {
            if (IsWhite(_d[Pos])) Pos++;
            else if (_d[Pos] == '%') { while (Pos < _d.Length && _d[Pos] != '\n' && _d[Pos] != '\r') Pos++; }
            else break;
        }
    }

    /// <summary>The next run of regular characters as text, or null at a delimiter or the end.</summary>
    public string? Word()
    {
        SkipWhitespace();
        var start = Pos;
        while (Pos < _d.Length && IsRegular(_d[Pos])) Pos++;
        return Pos > start ? Encoding.Latin1.GetString(_d, start, Pos - start) : null;
    }

    /// <summary>Whether the bytes at the position spell <paramref name="s"/>.</summary>
    public bool Peek(string s)
    {
        if (Pos + s.Length > _d.Length) return false;
        for (var i = 0; i < s.Length; i++) if (_d[Pos + i] != s[i]) return false;
        return true;
    }

    /// <summary>One object. An integer followed by an integer and R is a reference; keywords come back as <see cref="PdfKeyword"/>.</summary>
    public PdfObj Parse(int depth = 0)
    {
        SkipWhitespace();
        if (Pos >= _d.Length) return new PdfKeyword("");
        if (depth > 200) { Pos++; return PdfNull.Value; }
        var b = _d[Pos];
        switch (b)
        {
            case (byte)'/': Pos++; return new PdfName(Name());
            case (byte)'(': Pos++; return new PdfString(Literal());
            case (byte)'<':
                if (Peek("<<")) { Pos += 2; return Dict(depth); }
                Pos++; return new PdfString(Hex());
            case (byte)'[':
            {
                Pos++;
                var items = new List<PdfObj>();
                while (true)
                {
                    SkipWhitespace();
                    if (Pos >= _d.Length) break;
                    if (_d[Pos] == ']') { Pos++; break; }
                    var o = Parse(depth + 1);
                    if (o is PdfKeyword { Value: "" }) break;
                    if (o is PdfKeyword k && k.Value is "endobj" or "endstream" or "stream") { Pos -= k.Value.Length; break; }
                    if (o is PdfKeyword) continue;   // junk in an array: left out
                    items.Add(o);
                }
                return new PdfArray(items);
            }
            case (byte)']' or (byte)'>' or (byte)')' or (byte)'{' or (byte)'}':
                Pos++; return new PdfKeyword(((char)b).ToString());
        }
        var word = Word();
        if (word == null) { Pos++; return PdfNull.Value; }
        if (word == "true") return new PdfBool(true);
        if (word == "false") return new PdfBool(false);
        if (word == "null") return PdfNull.Value;
        if (Number(word) is { } n)
        {
            // n g R: a reference; the lookahead is undone when it is not one.
            if (n >= 0 && n == Math.Floor(n) && !word.Contains('.'))
            {
                var save = Pos;
                var w2 = Word();
                if (w2 != null && int.TryParse(w2, out var gen) && gen >= 0)
                {
                    var save2 = Pos;
                    var w3 = Word();
                    if (w3 == "R") return new PdfRef((int)n, gen);
                    Pos = save2;
                }
                Pos = save;
            }
            return new PdfNumber(n);
        }
        return new PdfKeyword(word);
    }

    static double? Number(string w)
    {
        // PDF numbers: digits with an optional sign and point; "--5" and "3." occur in the wild.
        var s = w.TrimStart('+');
        while (s.StartsWith("--")) s = s[1..];
        if (s.Length == 0) return null;
        foreach (var ch in s) if (!(char.IsAsciiDigit(ch) || ch is '.' or '-')) return null;
        return double.TryParse(s.TrimEnd('.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    string Name()
    {
        var sb = new StringBuilder();
        while (Pos < _d.Length && IsRegular(_d[Pos]))
        {
            var c = _d[Pos++];
            if (c == '#' && Pos + 1 < _d.Length && IsHex(_d[Pos]) && IsHex(_d[Pos + 1]))
            {
                sb.Append((char)((HexVal(_d[Pos]) << 4) | HexVal(_d[Pos + 1])));
                Pos += 2;
            }
            else sb.Append((char)c);
        }
        return sb.ToString();
    }

    static bool IsHex(byte b) => b is >= (byte)'0' and <= (byte)'9' or >= (byte)'a' and <= (byte)'f' or >= (byte)'A' and <= (byte)'F';
    static int HexVal(byte b) => b <= '9' ? b - '0' : b >= 'a' ? b - 'a' + 10 : b - 'A' + 10;

    /// <summary>A literal string after its opening parenthesis: balanced parentheses, escapes, line continuations; EOLs become \n.</summary>
    byte[] Literal()
    {
        var o = new List<byte>();
        var depth = 1;
        while (Pos < _d.Length)
        {
            var c = _d[Pos++];
            if (c == '\\')
            {
                if (Pos >= _d.Length) break;
                var e = _d[Pos++];
                switch (e)
                {
                    case (byte)'n': o.Add(10); break;
                    case (byte)'r': o.Add(13); break;
                    case (byte)'t': o.Add(9); break;
                    case (byte)'b': o.Add(8); break;
                    case (byte)'f': o.Add(12); break;
                    case (byte)'\r': if (Pos < _d.Length && _d[Pos] == '\n') Pos++; break;   // a continued line
                    case (byte)'\n': break;
                    default:
                        if (e is >= (byte)'0' and <= (byte)'7')
                        {
                            var v = e - '0';
                            for (var k = 0; k < 2 && Pos < _d.Length && _d[Pos] is >= (byte)'0' and <= (byte)'7'; k++) v = v * 8 + (_d[Pos++] - '0');
                            o.Add((byte)(v & 255));
                        }
                        else o.Add(e);   // \( \) \\ and any other: the character itself
                        break;
                }
            }
            else if (c == '(') { depth++; o.Add(c); }
            else if (c == ')') { if (--depth == 0) break; o.Add(c); }
            else if (c == '\r') { if (Pos < _d.Length && _d[Pos] == '\n') Pos++; o.Add(10); }
            else o.Add(c);
        }
        return [.. o];
    }

    /// <summary>A hex string after its opening angle bracket; whitespace ignored, an odd last digit read as if followed by 0.</summary>
    byte[] Hex()
    {
        var o = new List<byte>();
        var pending = -1;
        while (Pos < _d.Length)
        {
            var c = _d[Pos++];
            if (c == '>') break;
            if (!IsHex(c)) continue;
            if (pending < 0) pending = HexVal(c);
            else { o.Add((byte)((pending << 4) | HexVal(c))); pending = -1; }
        }
        if (pending >= 0) o.Add((byte)(pending << 4));
        return [.. o];
    }

    PdfDict Dict(int depth)
    {
        var map = new Dictionary<string, PdfObj>();
        while (true)
        {
            SkipWhitespace();
            if (Pos >= _d.Length) break;
            if (Peek(">>")) { Pos += 2; break; }
            var key = Parse(depth + 1);
            if (key is PdfKeyword { Value: "" }) break;
            if (key is PdfKeyword k && k.Value is "endobj" or "stream" or "endstream") { Pos -= k.Value.Length; break; }
            if (key is not PdfName name) continue;   // a value without a key: skipped
            SkipWhitespace();
            if (Peek(">>")) { map[name.Value] = PdfNull.Value; Pos += 2; break; }
            var value = Parse(depth + 1);
            if (value is PdfKeyword { Value: "" }) break;
            if (value is PdfKeyword kv) { if (kv.Value is "endobj" or "stream" or "endstream") { Pos -= kv.Value.Length; break; } continue; }
            map[name.Value] = value;
        }
        return new PdfDict(map);
    }

    /// <summary>After a dictionary: the stream keyword and its data. <paramref name="length"/> is /Length when known; the data is checked to end at endstream, else searched.</summary>
    public PdfStream? Stream(PdfDict dict, long? length)
    {
        SkipWhitespace();
        if (!Peek("stream")) return null;
        Pos += 6;
        if (Pos < _d.Length && _d[Pos] == '\r') Pos++;
        if (Pos < _d.Length && _d[Pos] == '\n') Pos++;
        var start = Pos;
        if (length is { } n && n >= 0 && start + n <= _d.Length)
        {
            var p = (int)(start + n);
            var probe = p;
            while (probe < _d.Length && IsWhite(_d[probe])) probe++;
            if (probe + 9 <= _d.Length && Encoding.Latin1.GetString(_d, probe, 9) == "endstream")
            {
                Pos = probe + 9;
                return new PdfStream(dict, _d[start..p]);
            }
        }
        // The length was wrong or missing: the data runs to the next endstream (the EOL before it left out).
        var end = IndexOf("endstream", start);
        if (end < 0) { Pos = _d.Length; return new PdfStream(dict, _d[start..]); }
        var stop = end;
        if (stop > start && _d[stop - 1] == '\n') stop--;
        if (stop > start && _d[stop - 1] == '\r') stop--;
        Pos = end + 9;
        return new PdfStream(dict, _d[start..stop]);
    }

    public int IndexOf(string s, int from)
    {
        if (from < 0 || from >= _d.Length) return -1;
        var i = _d.AsSpan(from).IndexOf(Encoding.Latin1.GetBytes(s));
        return i < 0 ? -1 : i + from;
    }

    public int LastIndexOf(string s, int before)
    {
        before = Math.Min(before, _d.Length);
        return before <= 0 ? -1 : _d.AsSpan(0, before).LastIndexOf(Encoding.Latin1.GetBytes(s));
    }
}
