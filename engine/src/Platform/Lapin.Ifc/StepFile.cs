using System.Globalization;
using System.Text;

namespace Lapin.Ifc;

/// <summary>An entity reference in a STEP file (#12).</summary>
public readonly record struct StepRef(int Id);

/// <summary>An enumeration value (.T., .ELEMENT.).</summary>
public sealed record StepEnum(string Value);

/// <summary>A typed value: IFCLABEL('x'), IFCLENGTHMEASURE(0.3).</summary>
public sealed record StepTyped(string Type, object? Value);

/// <summary>One entity: its type (upper case) and its attributes — null ($ or *), string, double, long, StepRef, StepEnum, StepTyped or List&lt;object?&gt;.</summary>
public sealed record StepEntity(int Id, string Type, List<object?> Args)
{
    public object? this[int i] => i < Args.Count ? Args[i] : null;
}

/// <summary>
/// An ISO 10303-21 file read: its schema and its entities by id. Strings are
/// decoded (\X2\…\X0\, \X\hh, \S\ and doubled quotes); numbers are doubles
/// (integers longs).
/// </summary>
public sealed class StepFile
{
    public string Schema { get; private set; } = "";
    public Dictionary<int, StepEntity> Entities { get; } = [];

    public StepEntity? Get(object? r) => r is StepRef x && Entities.TryGetValue(x.Id, out var e) ? e : null;
    public IEnumerable<StepEntity> OfType(string type) => Entities.Values.Where(e => e.Type == type);

    /// <summary>Reads a file: ASCII as the standard says, UTF-8 as some programs write it, Latin-1 else.</summary>
    public static StepFile Read(string path) => Read(File.ReadAllBytes(path));

    /// <summary>Reads a file's bytes: ASCII as the standard says, UTF-8 as some programs write it, Latin-1 else.</summary>
    public static StepFile Read(byte[] bytes)
    {
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { text = Encoding.Latin1.GetString(bytes); }
        return Parse(text);
    }

    public static StepFile Parse(string text)
    {
        var f = new StepFile();
        var schema = text.IndexOf("FILE_SCHEMA", StringComparison.Ordinal);
        if (schema >= 0)
        {
            var q = text.IndexOf('\'', schema);
            var q2 = q >= 0 ? text.IndexOf('\'', q + 1) : -1;
            if (q2 > q) f.Schema = text[(q + 1)..q2].ToUpperInvariant();
        }
        var data = text.IndexOf("DATA;", StringComparison.Ordinal);
        if (data < 0) throw new FormatException("The file has no DATA section.");
        var p = new Parser(text, data + 5);
        while (p.NextEntity() is { } e) f.Entities[e.Id] = e;
        return f;
    }

    sealed class Parser(string s, int i)
    {
        int _i = i;

        void Skip()
        {
            while (_i < s.Length)
            {
                if (char.IsWhiteSpace(s[_i])) _i++;
                else if (s[_i] == '/' && _i + 1 < s.Length && s[_i + 1] == '*') { var end = s.IndexOf("*/", _i + 2, StringComparison.Ordinal); _i = end < 0 ? s.Length : end + 2; }
                else break;
            }
        }

        public StepEntity? NextEntity()
        {
            while (true)
            {
                Skip();
                if (_i >= s.Length || s.AsSpan(_i).StartsWith("ENDSEC")) return null;
                if (s[_i] != '#') { _i = s.IndexOf(';', _i) is var k && k < 0 ? s.Length : k + 1; continue; }
                _i++;
                var id = (int)Integer();
                Skip(); Expect('='); Skip();
                var type = Keyword();
                Skip();
                var args = (List<object?>)Value()!;
                Skip(); Expect(';');
                return new StepEntity(id, type, args);
            }
        }

        void Expect(char c)
        {
            if (_i >= s.Length || s[_i] != c) throw new FormatException($"STEP: aștept „{c}” la poziția {_i}.");
            _i++;
        }

        long Integer()
        {
            var st = _i;
            while (_i < s.Length && (char.IsDigit(s[_i]) || s[_i] == '-')) _i++;
            return long.Parse(s.AsSpan(st, _i - st), CultureInfo.InvariantCulture);
        }

        string Keyword()
        {
            var st = _i;
            while (_i < s.Length && (char.IsLetterOrDigit(s[_i]) || s[_i] == '_')) _i++;
            return s[st.._i].ToUpperInvariant();
        }

        object? Value()
        {
            Skip();
            var c = s[_i];
            switch (c)
            {
                case '(':
                {
                    _i++;
                    var list = new List<object?>();
                    Skip();
                    if (s[_i] == ')') { _i++; return list; }
                    while (true)
                    {
                        list.Add(Value());
                        Skip();
                        if (s[_i] == ',') { _i++; continue; }
                        Expect(')');
                        return list;
                    }
                }
                case '$': case '*': _i++; return null;
                case '#': _i++; return new StepRef((int)Integer());
                case '\'': return Str();
                case '.':
                {
                    var end = s.IndexOf('.', _i + 1);
                    var v = s[(_i + 1)..end];
                    _i = end + 1;
                    return new StepEnum(v.ToUpperInvariant());
                }
                case '"': { var end = s.IndexOf('"', _i + 1); var v = s[(_i + 1)..end]; _i = end + 1; return v; } // binary: kept as text
                default:
                    if (char.IsLetter(c))
                    {
                        var type = Keyword();
                        Skip();
                        Expect('(');
                        var v = Value();
                        Skip();
                        Expect(')');
                        return new StepTyped(type, v);
                    }
                    return Number();
            }
        }

        object Number()
        {
            var st = _i;
            while (_i < s.Length && (char.IsDigit(s[_i]) || s[_i] is '-' or '+' or '.' or 'E' or 'e')) _i++;
            var t = s.AsSpan(st, _i - st);
            if (t.IndexOfAny(".Ee") < 0 && long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
            return double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        string Str()
        {
            _i++;
            var raw = new StringBuilder();
            while (true)
            {
                var c = s[_i++];
                if (c == '\'') { if (_i < s.Length && s[_i] == '\'') { raw.Append('\''); _i++; continue; } break; }
                raw.Append(c);
            }
            return Decode(raw.ToString());
        }
    }

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
                var width = s[i + 2] == '2' ? 4 : 8;
                var end = s.IndexOf("\\X0\\", i + 4, StringComparison.Ordinal);
                if (end < 0) { o.Append(s[i]); continue; }
                var hex = s[(i + 4)..end];
                for (var k = 0; k + width <= hex.Length; k += width)
                    o.Append(char.ConvertFromUtf32(int.Parse(hex.AsSpan(k, width), NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
                i = end + 3;
            }
            else if (s.AsSpan(i).StartsWith("\\X\\") && i + 4 < s.Length + 1)
            {
                o.Append((char)int.Parse(s.AsSpan(i + 3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
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
