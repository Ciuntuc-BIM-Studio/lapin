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
    Dictionary<string, List<StepEntity>>? _byType;

    public StepEntity? Get(object? r) => r is StepRef x && Entities.TryGetValue(x.Id, out var e) ? e : null;

    /// <summary>The entities of a type, in the file's order (indexed once: a large file has tens of millions).</summary>
    public IEnumerable<StepEntity> OfType(string type)
    {
        var byType = _byType;
        if (byType == null)
            lock (Entities)
            {
                if (_byType == null)
                {
                    var index = new Dictionary<string, List<StepEntity>>();
                    foreach (var e in Entities.Values) (index.TryGetValue(e.Type, out var l) ? l : index[e.Type] = []).Add(e);
                    _byType = index;
                }
                byType = _byType;
            }
        return byType.TryGetValue(type, out var list) ? list : [];
    }

    /// <summary>Reads a file: ASCII as the standard says; a string in UTF-8 as some programs write it, Latin-1 else.</summary>
    public static StepFile Read(string path) => Read(File.ReadAllBytes(path));

    /// <summary>
    /// Reads a file's bytes, as they are: no text of the whole file is made (a string holds at
    /// most a gigabyte, and would double the memory); only strings are decoded. A large DATA
    /// section is parsed on every core, cut where an entity starts on a new line.
    /// </summary>
    public static StepFile Read(byte[] bytes)
    {
        var f = new StepFile();
        var schema = IndexOf(bytes, "FILE_SCHEMA"u8, 0);
        if (schema >= 0)
        {
            var q = Array.IndexOf(bytes, (byte)'\'', schema);
            var q2 = q >= 0 ? Array.IndexOf(bytes, (byte)'\'', q + 1) : -1;
            if (q2 > q) f.Schema = Encoding.ASCII.GetString(bytes, q + 1, q2 - q - 1).ToUpperInvariant();
        }
        var data = IndexOf(bytes, "DATA;"u8, 0);
        if (data < 0) throw new FormatException("The file has no DATA section.");
        var from = data + 5;
        var cuts = Cuts(bytes, from, bytes.Length < 8 << 20 ? 1 : Environment.ProcessorCount * 4);
        var parts = new List<StepEntity>[cuts.Count - 1];
        if (parts.Length == 1) parts[0] = new Parser(bytes, cuts[0], cuts[1]).All();
        else Parallel.For(0, parts.Length, k => parts[k] = new Parser(bytes, cuts[k], cuts[k + 1]).All());
        f.Entities.EnsureCapacity(parts.Sum(x => x.Count));
        foreach (var part in parts) foreach (var e in part) f.Entities[e.Id] = e;
        return f;
    }

    /// <summary>A text, as its UTF-8 bytes.</summary>
    public static StepFile Parse(string text) => Read(Encoding.UTF8.GetBytes(text));

    static int IndexOf(byte[] b, ReadOnlySpan<byte> what, int from)
    {
        var k = b.AsSpan(from).IndexOf(what);
        return k < 0 ? -1 : from + k;
    }

    /// <summary>Where the DATA section is cut into <paramref name="n"/> parts: each at a line that starts an entity (#…) after one that ended (…;).</summary>
    static List<int> Cuts(byte[] b, int from, int n)
    {
        var cuts = new List<int> { from };
        var size = (b.Length - from) / Math.Max(n, 1);
        for (var k = 1; k < n; k++)
        {
            var at = Math.Max(from + k * size, cuts[^1] + 1);
            while (at < b.Length)
            {
                var nl = Array.IndexOf(b, (byte)'\n', at);
                if (nl < 0) { at = b.Length; break; }
                var next = nl + 1;
                while (next < b.Length && b[next] is (byte)' ' or (byte)'\t' or (byte)'\r') next++;
                var before = nl - 1;
                while (before > from && b[before] is (byte)' ' or (byte)'\t' or (byte)'\r') before--;
                if (next < b.Length && b[next] == '#' && b[before] == ';') { at = next; break; }
                at = nl + 1;
            }
            if (at >= b.Length) break;
            cuts.Add(at);
        }
        cuts.Add(b.Length);
        return cuts;
    }

    sealed class Parser(byte[] s, int i, int end)
    {
        int _i = i;
        // Each type's name and each enumeration made once: a large file repeats a few dozen of them millions of times.
        readonly Dictionary<string, string> _names = [];
        readonly Dictionary<string, StepEnum> _enums = [];

        public List<StepEntity> All()
        {
            var o = new List<StepEntity>();
            while (NextEntity() is { } e) o.Add(e);
            return o;
        }

        static bool Space(byte c) => c is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
        static bool Digit(byte c) => c is >= (byte)'0' and <= (byte)'9';
        static bool Letter(byte c) => c is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z';

        void Skip()
        {
            while (_i < s.Length)
            {
                if (Space(s[_i])) _i++;
                else if (s[_i] == '/' && _i + 1 < s.Length && s[_i + 1] == '*') { var e = IndexOf(s, "*/"u8, _i + 2); _i = e < 0 ? s.Length : e + 2; }
                else break;
            }
        }

        public StepEntity? NextEntity()
        {
            while (true)
            {
                Skip();
                if (_i >= end || s.AsSpan(_i).StartsWith("ENDSEC"u8)) return null;
                if (s[_i] != '#') { var k = Array.IndexOf(s, (byte)';', _i); _i = k < 0 ? s.Length : k + 1; continue; }
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
            while (_i < s.Length && (Digit(s[_i]) || s[_i] == '-')) _i++;
            return long.Parse(s.AsSpan(st, _i - st), CultureInfo.InvariantCulture);
        }

        string Keyword()
        {
            var st = _i;
            while (_i < s.Length && (Letter(s[_i]) || Digit(s[_i]) || s[_i] == '_')) _i++;
            return Name(s.AsSpan(st, _i - st), _names);
        }

        static string Name(ReadOnlySpan<byte> b, Dictionary<string, string> names)
        {
            Span<char> c = stackalloc char[Math.Min(b.Length, 256)];
            if (b.Length > c.Length) return Encoding.ASCII.GetString(b).ToUpperInvariant();
            for (var k = 0; k < b.Length; k++) c[k] = char.ToUpperInvariant((char)b[k]);
            var lookup = names.GetAlternateLookup<ReadOnlySpan<char>>();
            if (lookup.TryGetValue(c, out var known)) return known;
            var made = new string(c);
            names[made] = made;
            return made;
        }

        object? Value()
        {
            Skip();
            var c = s[_i];
            switch (c)
            {
                case (byte)'(':
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
                case (byte)'$': case (byte)'*': _i++; return null;
                case (byte)'#': _i++; return new StepRef((int)Integer());
                case (byte)'\'': return Str();
                case (byte)'.':
                {
                    var e = Array.IndexOf(s, (byte)'.', _i + 1);
                    var name = Name(s.AsSpan(_i + 1, e - _i - 1), _names);
                    _i = e + 1;
                    if (!_enums.TryGetValue(name, out var en)) _enums[name] = en = new StepEnum(name);
                    return en;
                }
                case (byte)'"': { var e = Array.IndexOf(s, (byte)'"', _i + 1); var v = Encoding.ASCII.GetString(s, _i + 1, e - _i - 1); _i = e + 1; return v; } // binary: kept as text
                default:
                    if (Letter(c))
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
            while (_i < s.Length && (Digit(s[_i]) || s[_i] is (byte)'-' or (byte)'+' or (byte)'.' or (byte)'E' or (byte)'e')) _i++;
            var t = s.AsSpan(st, _i - st);
            if (t.IndexOfAny(".Ee"u8) < 0 && long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
            return double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        static readonly UTF8Encoding Strict = new(false, true);

        /// <summary>A quoted string: its bytes (a doubled quote one quote) as UTF-8, or Latin-1 when they are not; then its escapes.</summary>
        string Str()
        {
            _i++;
            var st = _i;
            var doubled = false;
            while (true)
            {
                if (_i >= s.Length) throw new FormatException("STEP: a string runs to the end of the file.");
                if (s[_i] == '\'') { if (_i + 1 < s.Length && s[_i + 1] == '\'') { doubled = true; _i += 2; continue; } break; }
                _i++;
            }
            var raw = s.AsSpan(st, _i - st);
            _i++;
            string text;
            try { text = Strict.GetString(raw); }
            catch (DecoderFallbackException) { text = Encoding.Latin1.GetString(raw); }
            if (doubled) text = text.Replace("''", "'");
            return Decode(text);
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
