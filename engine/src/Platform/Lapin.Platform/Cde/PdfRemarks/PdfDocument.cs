using System.IO.Compression;
using System.Text;

namespace Lapin.Platform.Cde.PdfRemarks;

/// <summary>
/// A PDF file read for its structure (ISO 32000-1 §7.5): the cross-reference
/// chain from startxref — classic tables and, from PDF 1.5, xref streams,
/// following /Prev through every incremental update (a reviewer's
/// annotations are usually one appended), /XRefStm in hybrid files — the
/// objects at their offsets or inside object streams (FlateDecode, with the
/// PNG predictors xref streams use), and the page tree with its inherited
/// boxes and rotation. Defensive throughout: a broken chain or a wrong
/// offset makes it scan the whole file for "n g obj" instead, the newest
/// definition of each object winning, and every problem met goes into
/// <see cref="Warnings"/> rather than out as an exception.
/// </summary>
public sealed class PdfDocument
{
    readonly byte[] _d;
    readonly PdfLexer _lex;
    /// <summary>Where each object is: an offset in the file, or (type 2) the object stream holding it and its index there.</summary>
    readonly Dictionary<int, long> _offsets = [];
    readonly Dictionary<int, (int Stream, int Index)> _inStreams = [];
    readonly Dictionary<int, PdfObj?> _cache = [];
    readonly Dictionary<int, Dictionary<int, PdfObj>?> _objectStreams = [];
    readonly HashSet<int> _parsing = [];
    bool _reconstructed;

    public PdfDict Trailer { get; private set; } = PdfDict.Empty();
    public List<string> Warnings { get; } = [];
    public bool Reconstructed => _reconstructed;

    PdfDocument(byte[] data) { _d = data; _lex = new PdfLexer(data); }

    public static PdfDocument Load(byte[] data)
    {
        var doc = new PdfDocument(data);
        if (data.Length < 8 || !doc._lex.Peek("%PDF")) doc.Warnings.Add("The file does not start with %PDF.");
        try { doc.ReadChain(); }
        catch (Exception e) { doc.Warnings.Add($"The cross-reference could not be read ({e.Message}); the file was scanned instead."); doc.Reconstruct(); }
        if (doc.Root() == null) { doc.Reconstruct(); }
        return doc;
    }

    // ------------------------------------------------------------ the cross-reference chain

    void ReadChain()
    {
        var at = _lex.LastIndexOf("startxref", _d.Length);
        if (at < 0) { Warnings.Add("No startxref: the file was scanned for its objects."); Reconstruct(); return; }
        var lx = new PdfLexer(_d, at + 9);
        var w = lx.Word();
        if (w == null || !long.TryParse(w, out var start) || start < 0 || start >= _d.Length) { Warnings.Add("startxref points outside the file: the file was scanned."); Reconstruct(); return; }
        var seen = new HashSet<long>();
        var queue = new Queue<long>();
        queue.Enqueue(start);
        var trailer = new Dictionary<string, PdfObj>();
        while (queue.Count > 0)
        {
            var offset = queue.Dequeue();
            if (!seen.Add(offset) || seen.Count > 64) continue;
            var section = ReadSection(offset);
            if (section == null) { Warnings.Add($"A cross-reference section at {offset} could not be read: the file was scanned."); Reconstruct(); return; }
            // The newest section's trailer keys win (it is read first).
            foreach (var (k, v) in section.Map) trailer.TryAdd(k, v);
            // A hybrid file's xref stream holds what the table does not; it comes before the previous section.
            if (section["XRefStm"] is PdfNumber xs && xs.Value >= 0 && xs.Value < _d.Length) queue.Enqueue((long)xs.Value);
            if (section["Prev"] is PdfNumber prev && prev.Value >= 0 && prev.Value < _d.Length) queue.Enqueue((long)prev.Value);
        }
        Trailer = new PdfDict(trailer);
    }

    /// <summary>One section at an offset: a table ("xref") or a stream ("n g obj"); its trailer dictionary, or null when neither is there.</summary>
    PdfDict? ReadSection(long offset)
    {
        var lx = new PdfLexer(_d, (int)offset);
        lx.SkipWhitespace();
        if (lx.Peek("xref")) { lx.Pos += 4; return ReadTable(lx); }
        // An xref stream: "n g obj << ... >> stream".
        var obj = ReadIndirectAt((int)offset, out _);
        if (obj is PdfStream s && s.Dict["Type"] is PdfName { Value: "XRef" }) { ReadXrefStream(s); return s.Dict; }
        return null;
    }

    PdfDict? ReadTable(PdfLexer lx)
    {
        while (true)
        {
            lx.SkipWhitespace();
            if (lx.Peek("trailer"))
            {
                lx.Pos += 7;
                return lx.Parse() as PdfDict ?? PdfDict.Empty();
            }
            var first = lx.Word();
            var count = lx.Word();
            if (first == null || count == null || !int.TryParse(first, out var start) || !int.TryParse(count, out var n) || start < 0 || n < 0) return null;
            for (var i = 0; i < n; i++)
            {
                lx.SkipWhitespace();
                if (lx.Peek("trailer")) break;   // a count larger than the entries: tolerated
                var o = lx.Word(); var g = lx.Word(); var t = lx.Word();
                if (o == null || g == null || t == null) return null;
                var num = start + i;
                if (t == "n" && long.TryParse(o, out var off) && !_offsets.ContainsKey(num) && !_inStreams.ContainsKey(num)) _offsets[num] = off;
                else if (t == "f" && !_offsets.ContainsKey(num) && !_inStreams.ContainsKey(num)) _offsets[num] = -1;
            }
        }
    }

    void ReadXrefStream(PdfStream s)
    {
        var data = Decoded(s);
        if (data == null) return;
        var w = (s.Dict["W"] as PdfArray)?.Items.Select(x => (int)((x as PdfNumber)?.Value ?? 0)).ToArray() ?? [1, 1, 1];
        if (w.Length < 3) w = [.. w, .. new int[3 - w.Length]];
        var size = (int)((s.Dict["Size"] as PdfNumber)?.Value ?? 0);
        var index = (s.Dict["Index"] as PdfArray)?.Items.Select(x => (int)((x as PdfNumber)?.Value ?? 0)).ToArray() ?? [0, size];
        var rowLength = w.Sum();
        if (rowLength <= 0) return;
        var p = 0;
        for (var k = 0; k + 1 < index.Length; k += 2)
        {
            var start = index[k];
            var count = index[k + 1];
            for (var i = 0; i < count && p + rowLength <= data.Length; i++, p += rowLength)
            {
                long Field(int at, int width) { long v = 0; for (var j = 0; j < width; j++) v = (v << 8) | data[p + at + j]; return v; }
                var type = w[0] == 0 ? 1 : Field(0, w[0]);
                var f2 = Field(w[0], w[1]);
                var f3 = Field(w[0] + w[1], w[2]);
                var num = start + i;
                if (_offsets.ContainsKey(num) || _inStreams.ContainsKey(num)) continue;
                switch (type)
                {
                    case 1: _offsets[num] = f2; break;
                    case 2: _inStreams[num] = ((int)f2, (int)f3); break;
                    default: _offsets[num] = -1; break;
                }
            }
        }
    }

    /// <summary>Every "n g obj" in the file, the last occurrence of a number winning; the trailer's Root from the last trailer, else the catalog found among the objects.</summary>
    void Reconstruct()
    {
        if (_reconstructed) return;
        _reconstructed = true;
        _offsets.Clear(); _inStreams.Clear(); _cache.Clear(); _objectStreams.Clear();
        var span = _d.AsSpan();
        var pattern = Encoding.Latin1.GetBytes("obj");
        var from = 0;
        var streams = new List<int>();
        while (true)
        {
            var i = span[from..].IndexOf(pattern);
            if (i < 0) break;
            var at = from + i;
            from = at + 3;
            // Walk back over "n g " to the number.
            var p = at - 1;
            while (p >= 0 && PdfLexer.IsWhite(_d[p])) p--;
            var genEnd = p + 1;
            while (p >= 0 && char.IsAsciiDigit((char)_d[p])) p--;
            var genStart = p + 1;
            if (genStart == genEnd || p < 0 || !PdfLexer.IsWhite(_d[p])) continue;
            while (p >= 0 && PdfLexer.IsWhite(_d[p])) p--;
            var numEnd = p + 1;
            while (p >= 0 && char.IsAsciiDigit((char)_d[p])) p--;
            var numStart = p + 1;
            if (numStart == numEnd || (p >= 0 && PdfLexer.IsRegular(_d[p]))) continue;
            if (at + 3 < _d.Length && PdfLexer.IsRegular(_d[at + 3])) continue;   // "object", "objstm"
            if (!int.TryParse(Encoding.Latin1.GetString(_d, numStart, numEnd - numStart), out var num)) continue;
            _offsets[num] = numStart;
            // Object streams found here are opened later, for the objects no top-level definition gives.
            var lx = new PdfLexer(_d, at + 3);
            lx.SkipWhitespace();
            if (lx.Peek("<<"))
            {
                var head = Encoding.Latin1.GetString(_d, lx.Pos, Math.Min(300, _d.Length - lx.Pos));
                if (head.Contains("/ObjStm")) streams.Add(num);
            }
        }
        foreach (var sn in streams)
        {
            var members = ObjectStream(sn);
            if (members == null) continue;
            foreach (var (num, _) in members) if (!_offsets.ContainsKey(num)) _inStreams[num] = (sn, 0);
        }
        // The trailer: the last "trailer" dictionary with a Root, else the last xref stream's dictionary, else the catalog itself.
        var trailer = new Dictionary<string, PdfObj>();
        var t = _d.Length;
        while ((t = _lex.LastIndexOf("trailer", t)) >= 0)
        {
            if (new PdfLexer(_d, t + 7).Parse() is PdfDict td && td.Has("Root")) { foreach (var (k, v) in td.Map) trailer.TryAdd(k, v); break; }
        }
        if (!trailer.ContainsKey("Root"))
            foreach (var num in _offsets.Keys.OrderByDescending(x => x))
                if (Get(num) is PdfStream { Dict: var xd } && xd["Type"] is PdfName { Value: "XRef" } && xd.Has("Root")) { foreach (var (k, v) in xd.Map) trailer.TryAdd(k, v); break; }
        if (!trailer.ContainsKey("Root"))
            foreach (var num in _offsets.Keys.Concat(_inStreams.Keys).Distinct().OrderBy(x => x))
                if (Get(num) is PdfDict cd && cd["Type"] is PdfName { Value: "Catalog" }) { trailer["Root"] = new PdfRef(num, 0); break; }
        Trailer = new PdfDict(trailer);
    }

    // ------------------------------------------------------------ objects

    /// <summary>The object with this number (null when there is none), newest definition first; a wrong offset makes the document scan the file.</summary>
    public PdfObj? Get(int num)
    {
        if (_cache.TryGetValue(num, out var cached)) return cached;
        if (!_parsing.Add(num)) return null;   // a reference back into the object being parsed (a /Length pointing at itself)
        try
        {
            PdfObj? o = null;
            var found = false;
            // A number no section lists is null (§7.3.10), not a reason to scan the file.
            if (!_offsets.ContainsKey(num) && !_inStreams.ContainsKey(num)) { _cache[num] = null; return null; }
            if (_offsets.TryGetValue(num, out var offset))
            {
                if (offset >= 0 && offset < _d.Length)
                {
                    o = ReadIndirectAt((int)offset, out var readNum);
                    found = o != null && readNum == num;
                }
                else found = offset < 0;   // free
            }
            else if (_inStreams.TryGetValue(num, out var at))
            {
                var members = ObjectStream(at.Stream);
                found = members != null && members.TryGetValue(num, out o);
            }
            if (!found && !_reconstructed)
            {
                Warnings.Add($"Object {num} was not where the cross-reference said: the file was scanned.");
                Reconstruct();
                _parsing.Remove(num);
                return Get(num);
            }
            _cache[num] = o;
            return o;
        }
        finally { _parsing.Remove(num); }
    }

    /// <summary>"n g obj … endobj" at an offset: the object, with its number; null when the header is not there.</summary>
    PdfObj? ReadIndirectAt(int offset, out int num)
    {
        num = -1;
        var lx = new PdfLexer(_d, offset);
        var w1 = lx.Word(); var w2 = lx.Word(); var w3 = lx.Word();
        if (w1 == null || w2 == null || w3 != "obj" || !int.TryParse(w1, out num) || !int.TryParse(w2, out _)) return null;
        var o = lx.Parse();
        if (o is PdfDict dict)
        {
            lx.SkipWhitespace();
            if (lx.Peek("stream"))
            {
                // The length when it is at hand: direct, or a reference the cross-reference already knows (never a scan from here).
                var length = dict["Length"] switch
                {
                    PdfNumber ln => (long?)ln.Value,
                    PdfRef r when _offsets.ContainsKey(r.Num) || _inStreams.ContainsKey(r.Num) => Number(Get(r.Num)) is { } v ? (long?)v : null,
                    _ => null,
                };
                return (PdfObj?)lx.Stream(dict, length) ?? dict;
            }
        }
        return o is PdfKeyword ? PdfNull.Value : o;
    }

    /// <summary>The members of an object stream (§7.5.7) by number; null when it is not one or cannot be decoded.</summary>
    Dictionary<int, PdfObj>? ObjectStream(int streamNum)
    {
        if (_objectStreams.TryGetValue(streamNum, out var cached)) return cached;
        _objectStreams[streamNum] = null;
        if (Get(streamNum) is not PdfStream s || s.Dict["Type"] is not PdfName { Value: "ObjStm" }) return null;
        var data = Decoded(s);
        if (data == null) return null;
        var n = (int)((Resolve(s.Dict["N"]) as PdfNumber)?.Value ?? 0);
        var first = (int)((Resolve(s.Dict["First"]) as PdfNumber)?.Value ?? 0);
        var header = new PdfLexer(data, 0);
        var members = new Dictionary<int, PdfObj>();
        for (var i = 0; i < n; i++)
        {
            var a = header.Word(); var b = header.Word();
            if (a == null || b == null || !int.TryParse(a, out var num) || !int.TryParse(b, out var off)) break;
            var at = first + off;
            if (at < 0 || at >= data.Length) continue;
            var o = new PdfLexer(data, at).Parse();
            members[num] = o is PdfKeyword ? PdfNull.Value : o;
        }
        _objectStreams[streamNum] = members;
        return members;
    }

    public PdfObj? Resolve(PdfObj? o)
    {
        var guard = 0;
        while (o is PdfRef r && guard++ < 32) o = Get(r.Num);
        return o is PdfRef ? null : o;
    }

    public PdfDict? Dict(PdfObj? o) => Resolve(o) switch { PdfDict d => d, PdfStream s => s.Dict, _ => null };
    public PdfArray? Array(PdfObj? o) => Resolve(o) as PdfArray;
    public string? Name(PdfObj? o) => (Resolve(o) as PdfName)?.Value;
    public double? Number(PdfObj? o) => (Resolve(o) as PdfNumber)?.Value;
    public string? Text(PdfObj? o) => Resolve(o) is PdfString s ? PdfText.Decode(s.Bytes) : null;
    public double[]? Numbers(PdfObj? o) => Array(o) is { } a ? a.Items.Select(x => Number(x)).Where(x => x != null).Select(x => x!.Value).ToArray() : null;

    public PdfDict? Root() => Dict(Trailer["Root"]);

    // ------------------------------------------------------------ streams

    /// <summary>A stream's data decoded: FlateDecode (with a PNG or TIFF predictor), none, or null with a warning for a filter the reader does not have.</summary>
    public byte[]? Decoded(PdfStream s)
    {
        var filters = Resolve(s.Dict["Filter"]) switch { PdfName n => [n.Value], PdfArray a => a.Items.Select(x => Name(x) ?? "").ToList(), _ => new List<string>() };
        var parms = Resolve(s.Dict["DecodeParms"]) switch { PdfDict d => [d], PdfArray a => a.Items.Select(x => Dict(x)).ToList(), _ => new List<PdfDict?>() };
        var data = s.Raw;
        for (var i = 0; i < filters.Count; i++)
        {
            var f = filters[i];
            var parm = i < parms.Count ? parms[i] : null;
            switch (f)
            {
                case "FlateDecode" or "Fl":
                    data = Inflate(data);
                    if (parm != null) data = Predict(data, parm);
                    break;
                case "": break;
                default:
                    Warnings.Add($"A stream uses the filter {f}, which the reader does not decode.");
                    return null;
            }
        }
        return data;
    }

    /// <summary>zlib data inflated; raw deflate when the header is missing; a corrupt tail keeps what was read.</summary>
    public static byte[] Inflate(byte[] data)
    {
        if (data.Length == 0) return data;
        var start = 0;
        while (start < data.Length && PdfLexer.IsWhite(data[start])) start++;
        byte[] Run(Func<Stream, Stream> open)
        {
            using var input = new MemoryStream(data, start, data.Length - start);
            using var z = open(input);
            using var o = new MemoryStream();
            var buf = new byte[16384];
            try { int n; while ((n = z.Read(buf, 0, buf.Length)) > 0) o.Write(buf, 0, n); }
            catch (Exception) { /* a truncated or corrupt stream: what came out stays */ }
            return o.ToArray();
        }
        var zlib = Run(s => new ZLibStream(s, CompressionMode.Decompress));
        if (zlib.Length > 0) return zlib;
        return Run(s => new DeflateStream(s, CompressionMode.Decompress));
    }

    /// <summary>The PNG predictors (10–15) and TIFF predictor 2 undone (§7.4.4.4), as xref streams use them.</summary>
    public static byte[] Predict(byte[] data, PdfDict parm)
    {
        var predictor = (int)((parm["Predictor"] as PdfNumber)?.Value ?? 1);
        if (predictor <= 1) return data;
        var colors = (int)((parm["Colors"] as PdfNumber)?.Value ?? 1);
        var bpc = (int)((parm["BitsPerComponent"] as PdfNumber)?.Value ?? 8);
        var columns = (int)((parm["Columns"] as PdfNumber)?.Value ?? 1);
        var bpp = Math.Max(1, colors * bpc / 8);
        var rowLength = (columns * colors * bpc + 7) / 8;
        if (predictor == 2)
        {
            if (bpc != 8) return data;
            var rows = data.Length / rowLength;
            for (var r = 0; r < rows; r++)
                for (var i = bpp; i < rowLength; i++) data[r * rowLength + i] += data[r * rowLength + i - bpp];
            return data;
        }
        var o = new MemoryStream();
        var prev = new byte[rowLength];
        var row = new byte[rowLength];
        for (var p = 0; p + 1 <= data.Length; p += rowLength + 1)
        {
            var ft = data[p];
            var n = Math.Min(rowLength, data.Length - p - 1);
            if (n <= 0) break;
            System.Array.Clear(row);
            System.Array.Copy(data, p + 1, row, 0, n);
            for (var i = 0; i < rowLength; i++)
            {
                int a = i >= bpp ? row[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                row[i] = ft switch
                {
                    1 => (byte)(row[i] + a),
                    2 => (byte)(row[i] + b),
                    3 => (byte)(row[i] + ((a + b) >> 1)),
                    4 => (byte)(row[i] + Paeth(a, b, c)),
                    _ => row[i],
                };
            }
            o.Write(row, 0, rowLength);
            (prev, row) = (row, prev);
        }
        return o.ToArray();
    }

    static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    // ------------------------------------------------------------ pages

    /// <summary>A page: its dictionary, its boxes and rotation as inherited down the tree (points, user space).</summary>
    public sealed record Page(PdfDict Dict, double[] MediaBox, double[]? CropBox, int Rotate);

    /// <summary>The pages in order, from the catalog's tree; when the tree is broken, every /Type /Page object by number.</summary>
    public List<Page> Pages()
    {
        var pages = new List<Page>();
        var seen = new HashSet<PdfDict>(ReferenceEqualityComparer.Instance);
        void Walk(PdfDict? node, double[]? media, double[]? crop, int rotate, int depth)
        {
            if (node == null || depth > 64 || !seen.Add(node) || pages.Count > 10_000) return;
            media = Box(node["MediaBox"]) ?? media;
            crop = Box(node["CropBox"]) ?? crop;
            if (Number(node["Rotate"]) is { } r) rotate = (int)r;
            var type = Name(node["Type"]);
            var kids = Array(node["Kids"]);
            if (type == "Page" || (type != "Pages" && kids == null && node.Has("Contents")))
            {
                pages.Add(new Page(node, media ?? [0, 0, 612, 792], crop, NormalRotate(rotate)));
                return;
            }
            if (kids == null) return;
            foreach (var kid in kids.Items) Walk(Dict(kid), media, crop, rotate, depth + 1);
        }
        Walk(Dict(Root()?["Pages"]), null, null, 0, 0);
        if (pages.Count > 0) return pages;
        Warnings.Add("The page tree could not be followed: the pages were taken as found in the file.");
        if (!_reconstructed) Reconstruct();
        foreach (var num in _offsets.Keys.Concat(_inStreams.Keys).Distinct().OrderBy(x => x))
            if (Get(num) is PdfDict d && Name(d["Type"]) == "Page")
            {
                // Inherited attributes through /Parent.
                double[]? media = Box(d["MediaBox"]), crop = Box(d["CropBox"]);
                var rotate = Number(d["Rotate"]);
                var parent = Dict(d["Parent"]);
                for (var guard = 0; parent != null && guard < 32; guard++, parent = Dict(parent["Parent"]))
                {
                    media ??= Box(parent["MediaBox"]); crop ??= Box(parent["CropBox"]); rotate ??= Number(parent["Rotate"]);
                }
                pages.Add(new Page(d, media ?? [0, 0, 612, 792], crop, NormalRotate((int)(rotate ?? 0))));
            }
        return pages;
    }

    static int NormalRotate(int r) { r %= 360; if (r < 0) r += 360; return r / 90 * 90; }

    double[]? Box(PdfObj? o)
    {
        var a = Numbers(o);
        if (a is not { Length: 4 } || a.Any(x => !double.IsFinite(x))) return null;
        return [Math.Min(a[0], a[2]), Math.Min(a[1], a[3]), Math.Max(a[0], a[2]), Math.Max(a[1], a[3])];
    }
}
