using System.Collections;
using System.Collections.Concurrent;
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
/// <para>
/// The file is kept as its bytes, with an index of where each entity starts
/// and what type it is; an entity is parsed when it is asked for, and not
/// kept (docs/plan-instancing.md, "Still open"). Parsed whole, a Revit
/// export of twenty million entities (its points, loops and faces) was 7.6 GB
/// of objects; indexed, it is the file and a few hundred megabytes. The
/// index is made on every core.
/// </para>
/// </summary>
public sealed class StepFile
{
    public string Schema { get; private set; } = "";

    /// <summary>The entities by id, parsed as they are asked for (a dictionary in all but the keeping).</summary>
    public EntityTable Entities { get; }

    readonly byte[] _bytes;
    readonly Index _index;
    readonly ConcurrentDictionary<string, List<StepEntity>> _byType = [];
    List<StepEntity>? _rooted;

    StepFile(byte[] bytes, Index index)
    {
        _bytes = bytes;
        _index = index;
        Entities = new EntityTable(this);
    }

    public StepEntity? Get(object? r) => r is StepRef x ? Entities.Parse(x.Id) : null;

    /// <summary>The entities of a type, in the file's order (parsed once, kept).</summary>
    public IEnumerable<StepEntity> OfType(string type) =>
        _index.ByType.TryGetValue(type, out var ids) ? _byType.GetOrAdd(type, _ => [.. ids.Select(id => Entities.Parse(id)!)]) : [];

    /// <summary>
    /// The entities whose first attribute is a 22-character string, a GlobalId: the products,
    /// relationships, types and property sets (IfcRoot), a few in a hundred of a file's
    /// entities; the rest are geometry and values. Parsed once, kept.
    /// </summary>
    public IEnumerable<StepEntity> Rooted
    {
        get
        {
            if (_rooted != null) return _rooted;
            lock (_byType) return _rooted ??= [.. _index.RootedIds().Select(id => Entities.Parse(id)!)];
        }
    }

    /// <summary>Reads a file: ASCII as the standard says; a string in UTF-8 as some programs write it, Latin-1 else.</summary>
    public static StepFile Read(string path) => Read(File.ReadAllBytes(path));

    /// <summary>Reads a file's bytes, as they are: indexed now, each entity parsed when asked for.</summary>
    public static StepFile Read(byte[] bytes)
    {
        var schema = "";
        var at = IndexOf(bytes, "FILE_SCHEMA"u8, 0);
        if (at >= 0)
        {
            var q = Array.IndexOf(bytes, (byte)'\'', at);
            var q2 = q >= 0 ? Array.IndexOf(bytes, (byte)'\'', q + 1) : -1;
            if (q2 > q) schema = Encoding.ASCII.GetString(bytes, q + 1, q2 - q - 1).ToUpperInvariant();
        }
        var data = IndexOf(bytes, "DATA;"u8, 0);
        if (data < 0) throw new FormatException("The file has no DATA section.");
        var f = new StepFile(bytes, Index.Build(bytes, data + 5)) { Schema = schema };
        return f;
    }

    /// <summary>A text, as its UTF-8 bytes.</summary>
    public static StepFile Parse(string text) => Read(Encoding.UTF8.GetBytes(text));

    static int IndexOf(byte[] b, ReadOnlySpan<byte> what, int from)
    {
        var k = b.AsSpan(from).IndexOf(what);
        return k < 0 ? -1 : from + k;
    }

    static bool Space(byte c) => c is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
    static bool Digit(byte c) => c is >= (byte)'0' and <= (byte)'9';
    static bool Letter(byte c) => c is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z';

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

    // ─── The index ───────────────────────────────────────────────────────────

    /// <summary>
    /// Each entity's start (the offset of its '#'), its type and whether it is rooted, by id. Ids
    /// are dense in a STEP file, so arrays by id; a sparse file (ids far apart) takes a dictionary.
    /// </summary>
    sealed class Index
    {
        readonly int[]? _offsetByIdPlus1;      // 0: no entity of that id
        readonly int[]? _typeById;
        readonly bool[]? _rootedById;
        readonly Dictionary<int, (int Offset, int Type, bool Rooted)>? _sparse;
        readonly List<int>? _sparseIds;
        public readonly List<string> TypeNames = [];
        public readonly Dictionary<string, List<int>> ByType = [];
        public int Count { get; private set; }

        Index(int maxId, int count)
        {
            if ((long)maxId <= 4L * count + 1_000_000) { _offsetByIdPlus1 = new int[maxId + 1]; _typeById = new int[maxId + 1]; _rootedById = new bool[maxId + 1]; }
            else { _sparse = []; _sparseIds = []; }
        }

        /// <summary>One entity's start and type: the first argument of a rooted one is a 22-character string.</summary>
        readonly record struct Entry(int Id, int Offset, int Type, bool Rooted);

        public static Index Build(byte[] b, int from)
        {
            var cuts = Cuts(b, from, b.Length < 8 << 20 ? 1 : Environment.ProcessorCount * 4);
            var parts = new (List<Entry> Entries, List<string> Types)[cuts.Count - 1];
            if (parts.Length == 1) parts[0] = Scan(b, cuts[0], cuts[1]);
            else Parallel.For(0, parts.Length, k => parts[k] = Scan(b, cuts[k], cuts[k + 1]));
            var count = 0; var maxId = 0;
            foreach (var (entries, _) in parts) { count += entries.Count; foreach (var e in entries) maxId = Math.Max(maxId, e.Id); }
            var index = new Index(maxId, count);
            var typeIndex = new Dictionary<string, int>();
            foreach (var (entries, types) in parts)
            {
                // The part's type names mapped to the file's.
                var map = new int[types.Count];
                for (var t = 0; t < types.Count; t++)
                {
                    if (!typeIndex.TryGetValue(types[t], out var g)) { g = index.TypeNames.Count; typeIndex[types[t]] = g; index.TypeNames.Add(types[t]); index.ByType[types[t]] = []; }
                    map[t] = g;
                }
                foreach (var e in entries) index.Put(e.Id, e.Offset, map[e.Type], e.Rooted);
            }
            return index;
        }

        void Put(int id, int offset, int type, bool rooted)
        {
            if (_offsetByIdPlus1 != null)
            {
                if (_offsetByIdPlus1[id] == 0) Count++;
                _offsetByIdPlus1[id] = offset + 1; _typeById![id] = type; _rootedById![id] = rooted;
            }
            else
            {
                if (!_sparse!.ContainsKey(id)) { Count++; _sparseIds!.Add(id); }
                _sparse[id] = (offset, type, rooted);
            }
            ByType[TypeNames[type]].Add(id);
        }

        public bool TryGet(int id, out int offset, out string type)
        {
            if (_offsetByIdPlus1 != null)
            {
                if (id >= 0 && id < _offsetByIdPlus1.Length && _offsetByIdPlus1[id] != 0) { offset = _offsetByIdPlus1[id] - 1; type = TypeNames[_typeById![id]]; return true; }
            }
            else if (_sparse!.TryGetValue(id, out var e)) { offset = e.Offset; type = TypeNames[e.Type]; return true; }
            offset = -1; type = ""; return false;
        }

        public IEnumerable<int> Ids()
        {
            if (_offsetByIdPlus1 != null) { for (var id = 0; id < _offsetByIdPlus1.Length; id++) if (_offsetByIdPlus1[id] != 0) yield return id; }
            else foreach (var id in _sparseIds!.Order()) yield return id;
        }

        public IEnumerable<int> RootedIds()
        {
            if (_rootedById != null) { for (var id = 0; id < _rootedById.Length; id++) if (_rootedById[id]) yield return id; }
            else foreach (var id in _sparseIds!.Order()) if (_sparse![id].Rooted) yield return id;
        }

        /// <summary>The entities between <paramref name="from"/> and <paramref name="end"/>: each '#id=TYPE(' found, its body skipped (strings honoured) to its ';'.</summary>
        static (List<Entry>, List<string>) Scan(byte[] b, int from, int end)
        {
            var entries = new List<Entry>();
            var types = new List<string>();
            var typeIndex = new Dictionary<string, int>();
            var i = from;
            while (i < end)
            {
                // Between entities: blanks and comments; ENDSEC ends the section.
                if (Space(b[i])) { i++; continue; }
                if (b[i] == '/' && i + 1 < b.Length && b[i + 1] == '*') { var e = IndexOf(b, "*/"u8, i + 2); i = e < 0 ? b.Length : e + 2; continue; }
                if (b.AsSpan(i).StartsWith("ENDSEC"u8)) break;
                if (b[i] != '#') { var k = Array.IndexOf(b, (byte)';', i); i = k < 0 ? b.Length : k + 1; continue; }
                var start = i;
                i++;
                var id = 0;
                while (i < b.Length && Digit(b[i])) id = id * 10 + (b[i++] - '0');
                while (i < b.Length && Space(b[i])) i++;
                if (i < b.Length && b[i] == '=') i++;
                while (i < b.Length && Space(b[i])) i++;
                var t0 = i;
                while (i < b.Length && (Letter(b[i]) || Digit(b[i]) || b[i] == '_')) i++;
                var type = Name(b.AsSpan(t0, i - t0));
                if (!typeIndex.TryGetValue(type, out var ti)) { ti = types.Count; typeIndex[type] = ti; types.Add(type); }
                while (i < b.Length && Space(b[i])) i++;
                // Rooted: '(' then a 22-character string with no quote inside.
                var rooted = false;
                if (i < b.Length && b[i] == '(')
                {
                    var p = i + 1;
                    while (p < b.Length && Space(b[p])) p++;
                    if (p + 23 < b.Length && b[p] == '\'' && b[p + 23] == '\'' && b.AsSpan(p + 1, 22).IndexOf((byte)'\'') < 0) rooted = true;
                }
                // To the ';' that ends the entity, past any string.
                var inString = false;
                while (i < b.Length)
                {
                    var c = b[i++];
                    if (c == '\'') inString = !inString;
                    else if (c == ';' && !inString) break;
                }
                entries.Add(new Entry(id, start, ti, rooted));
            }
            return (entries, types);
        }

        static string Name(ReadOnlySpan<byte> b)
        {
            Span<char> c = stackalloc char[Math.Min(b.Length, 256)];
            if (b.Length > c.Length) return Encoding.ASCII.GetString(b).ToUpperInvariant();
            for (var k = 0; k < b.Length; k++) c[k] = char.ToUpperInvariant((char)b[k]);
            return new string(c);
        }
    }

    // ─── The entities, parsed on demand ──────────────────────────────────────

    /// <summary>The file's entities by id, each parsed from the bytes when asked for; <see cref="Values"/> parses them all, in id order.</summary>
    public sealed class EntityTable(StepFile file) : IReadOnlyDictionary<int, StepEntity>
    {
        /// <summary>The entity of an id, or null.</summary>
        public StepEntity? Parse(int id)
        {
            if (!file._index.TryGet(id, out var offset, out var type)) return null;
            var scratch = _scratch ??= new Scratch();
            return new Parser(file._bytes, offset, file._bytes.Length, scratch).NextEntity(type);
        }

        [ThreadStatic] static Scratch? _scratch;

        public StepEntity this[int id] => Parse(id) ?? throw new KeyNotFoundException($"No entity #{id}.");
        public bool ContainsKey(int id) => file._index.TryGet(id, out _, out _);
        public bool TryGetValue(int id, out StepEntity value) { value = Parse(id)!; return value != null; }
        public int Count => file._index.Count;
        public IEnumerable<int> Keys => file._index.Ids();
        public IEnumerable<StepEntity> Values => file._index.Ids().Select(id => Parse(id)!);
        public IEnumerator<KeyValuePair<int, StepEntity>> GetEnumerator() => file._index.Ids().Select(id => new KeyValuePair<int, StepEntity>(id, Parse(id)!)).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>A thread's interned enumerations and keywords: a file repeats a few dozen of them millions of times.</summary>
    sealed class Scratch
    {
        public readonly Dictionary<string, string> Names = [];
        public readonly Dictionary<string, StepEnum> Enums = [];
    }

    sealed class Parser(byte[] s, int i, int end, Scratch scratch)
    {
        int _i = i;

        void Skip()
        {
            while (_i < s.Length)
            {
                if (Space(s[_i])) _i++;
                else if (s[_i] == '/' && _i + 1 < s.Length && s[_i + 1] == '*') { var e = IndexOf(s, "*/"u8, _i + 2); _i = e < 0 ? s.Length : e + 2; }
                else break;
            }
        }

        /// <summary>The entity at the parser's start; <paramref name="type"/>, the file's interned name of its type, when known.</summary>
        public StepEntity? NextEntity(string? type = null)
        {
            while (true)
            {
                Skip();
                if (_i >= end || s.AsSpan(_i).StartsWith("ENDSEC"u8)) return null;
                if (s[_i] != '#') { var k = Array.IndexOf(s, (byte)';', _i); _i = k < 0 ? s.Length : k + 1; continue; }
                _i++;
                var id = (int)Integer();
                Skip(); Expect('='); Skip();
                var parsedType = Keyword();
                Skip();
                var args = (List<object?>)Value()!;
                Skip(); Expect(';');
                return new StepEntity(id, type ?? parsedType, args);
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
            return Name(s.AsSpan(st, _i - st), scratch.Names);
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
                    var name = Name(s.AsSpan(_i + 1, e - _i - 1), scratch.Names);
                    _i = e + 1;
                    if (!scratch.Enums.TryGetValue(name, out var en)) scratch.Enums[name] = en = new StepEnum(name);
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
