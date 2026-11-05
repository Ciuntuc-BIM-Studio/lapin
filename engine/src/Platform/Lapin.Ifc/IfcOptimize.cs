using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;

namespace Lapin.Ifc;

/// <summary>
/// What an IFC file's optimisation does (docs/ifc-optimize.md): every option on its own, each with a safe default.
/// Lengths in millimetres, whatever the file's unit.
/// </summary>
public sealed record IfcOptimizeOptions
{
    /// <summary>Tessellated bodies (face sets, faceted B-reps and surface models of plain faces) made coarser within <see cref="ToleranceMm"/>.</summary>
    public bool Simplify { get; init; } = true;
    /// <summary>The most a simplified body may stray from the original (measured both ways), in mm.</summary>
    public double ToleranceMm { get; init; } = 1;
    /// <summary>Duplicate vertices welded (within 0.01 mm, or the tolerance when it is less), degenerate and duplicate triangles and unused coordinates dropped.</summary>
    public bool Clean { get; init; } = true;
    /// <summary>Identical bodies (the same mesh, moved) written once as an IfcRepresentationMap, each product's an IfcMappedItem; identical maps made one.</summary>
    public bool Share { get; init; } = true;
    /// <summary>Geometry and presentation resources nothing references removed (never an IfcRoot, never anything referenced).</summary>
    public bool Orphans { get; init; } = true;
    /// <summary>The coordinates of the tessellated bodies rounded to this many mm; 0: not rounded.</summary>
    public double RoundMm { get; init; }
    /// <summary>IFC4 and IFC4X3 only: faceted B-reps written as IfcTriangulatedFaceSet (IFC2X3 keeps its B-reps).</summary>
    public bool BrepsToFaceSets { get; init; }
    /// <summary>The copy written as .ifczip (the .ifc inside a zip).</summary>
    public bool Zip { get; init; }

    /// <summary>The presets, like View → Display quality's: a tolerance in mm.</summary>
    public static double Preset(string? name) => name switch { "fast" => 5, "precise" => 0.1, _ => 1 };

    public static IfcOptimizeOptions FromJson(JsonObject? o)
    {
        var d = new IfcOptimizeOptions();
        if (o == null) return d;
        bool B(string k, bool def) => o[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : def;
        double N(string k, double def) => o[k] is JsonValue v && v.TryGetValue<double>(out var x) && double.IsFinite(x) ? x : def;
        var tol = N("toleranceMm", o["preset"] is JsonValue pv && pv.TryGetValue<string>(out var preset) ? Preset(preset) : d.ToleranceMm);
        return new IfcOptimizeOptions
        {
            Simplify = B("simplify", d.Simplify), ToleranceMm = Math.Clamp(tol, 0.001, 100), Clean = B("clean", d.Clean), Share = B("share", d.Share),
            Orphans = B("orphans", d.Orphans), RoundMm = Math.Clamp(N("roundMm", 0), 0, 10), BrepsToFaceSets = B("brepsToFaceSets", false), Zip = B("zip", false),
        };
    }
}

/// <summary>What an optimisation did to one file, for the report the user reads.</summary>
public sealed class IfcOptimizeReport
{
    public string Schema { get; set; } = "";
    public long BytesBefore { get; set; }
    public long BytesAfter { get; set; }
    public int EntitiesBefore { get; set; }
    public int EntitiesAfter { get; set; }
    public long TrianglesBefore { get; set; }
    public long TrianglesAfter { get; set; }
    /// <summary>Tessellated bodies found (face sets and shells of plain faces).</summary>
    public int Bodies { get; set; }
    public int Simplified { get; set; }
    public int Cleaned { get; set; }
    /// <summary>Representations made mapped items of a shared body, and the bodies they share.</summary>
    public int Shared { get; set; }
    public int SharedBodies { get; set; }
    /// <summary>Representation maps made one with an identical one.</summary>
    public int MapsMerged { get; set; }
    public int Orphans { get; set; }
    /// <summary>Faceted B-reps written as triangulated face sets.</summary>
    public int Converted { get; set; }
    /// <summary>Face sets kept as written: with their own normals, or colours or textures by their triangles.</summary>
    public int Kept { get; set; }
    /// <summary>The most a body written strays from the file's, measured, in mm.</summary>
    public double DeviationMm { get; set; }
    public double Seconds { get; set; }
    public List<string> Notes { get; } = [];

    public JsonObject ToJson()
    {
        var notes = new JsonArray();
        foreach (var n in Notes) notes.Add((JsonNode)n);
        return new JsonObject
        {
            ["schema"] = Schema, ["bytesBefore"] = BytesBefore, ["bytesAfter"] = BytesAfter, ["entitiesBefore"] = EntitiesBefore, ["entitiesAfter"] = EntitiesAfter,
            ["trianglesBefore"] = TrianglesBefore, ["trianglesAfter"] = TrianglesAfter, ["bodies"] = Bodies, ["simplified"] = Simplified, ["cleaned"] = Cleaned,
            ["shared"] = Shared, ["sharedBodies"] = SharedBodies, ["mapsMerged"] = MapsMerged, ["orphans"] = Orphans, ["converted"] = Converted, ["kept"] = Kept,
            ["deviationMm"] = Math.Round(DeviationMm, 4), ["seconds"] = Math.Round(Seconds, 2), ["notes"] = notes,
        };
    }
}

/// <summary>
/// An IFC file cleaned and optimised into a copy (docs/ifc-optimize.md): a pass from file to file over the original
/// STEP, not Lapin's export. Every entity keeps its number and, unless it is geometry this pass rewrites, its very
/// bytes: the header, the products and their GlobalIds, names, property sets, quantities, relationships, types,
/// materials and placements are copied as they are, and nothing is added but geometry (no Lapin_* set, no quantity).
/// What it may change: tessellated bodies (welded, cleaned, simplified within a measured tolerance), identical
/// bodies written once as mapped items, and resources nothing references removed. New entities (a shared body's map,
/// simplified faces) take numbers after the file's last. The original is never written.
/// </summary>
public static class IfcOptimize
{
    public const string DefaultSuffix = ".optimized";

    /// <summary>
    /// The copy's path: the original's name with <paramref name="suffix"/> before its extension, in <paramref name="folder"/>
    /// or the original's own; .ifczip when zipped.
    /// </summary>
    public static string Target(string source, string suffix, string? folder, bool zip)
    {
        var name = Path.GetFileName(source);
        var stem = name.EndsWith(".ifczip", StringComparison.OrdinalIgnoreCase) ? name[..^7] : name.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase) ? name[..^4] : Path.GetFileNameWithoutExtension(name);
        var dir = string.IsNullOrWhiteSpace(folder) ? Path.GetDirectoryName(Path.GetFullPath(source)) ?? "." : folder;
        return Path.Combine(dir, stem + suffix + (zip ? ".ifczip" : ".ifc"));
    }

    /// <summary>A copy's ending as the user typed it, made safe: no folder, no extension of its own; null when nothing is left.</summary>
    public static string? CleanSuffix(string? suffix)
    {
        var s = (suffix ?? DefaultSuffix).Trim();
        foreach (var c in Path.GetInvalidFileNameChars().Concat(['/', '\\', ':'])) s = s.Replace(c.ToString(), "");
        if (s.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase)) s = s[..^4];
        return s.Length == 0 ? null : s;
    }

    /// <summary>
    /// A file on disk optimised into <paramref name="target"/>: written beside it under a temporary name first, then moved
    /// into place, so a cancelled or failed pass leaves nothing half written. Refused when the copy would be the original.
    /// </summary>
    public static IfcOptimizeReport RunFile(string source, string target, IfcOptimizeOptions o, Action<string, int, int>? progress = null, CancellationToken ct = default)
    {
        if (SamePath(source, target)) throw new InvalidOperationException("The copy would overwrite the original: choose another ending or folder.");
        var bytes = ReadSource(source);
        var temp = target + ".part";
        try
        {
            IfcOptimizeReport report;
            using (var output = File.Create(temp)) report = Run(bytes, output, o, progress, ct, Path.GetFileName(source));
            File.Move(temp, target, overwrite: true);
            report.BytesAfter = new FileInfo(target).Length;
            return report;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    /// <summary>An .ifc file's bytes, or the .ifc inside an .ifczip.</summary>
    public static byte[] ReadSource(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return Unzipped(bytes);
    }

    /// <summary>The .ifc inside a zip's bytes (an .ifczip), else the bytes as they are.</summary>
    public static byte[] Unzipped(byte[] bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 'P' || bytes[1] != 'K') return bytes;
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var entry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidDataException("The zip holds no .ifc file.");
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The file's bytes optimised into <paramref name="output"/> (zipped when the options say, the .ifc inside named after
    /// <paramref name="name"/>). Cancelled by <paramref name="ct"/> (an OperationCanceledException).
    /// </summary>
    public static IfcOptimizeReport Run(byte[] input, Stream output, IfcOptimizeOptions o, Action<string, int, int>? progress = null, CancellationToken ct = default, string name = "model.ifc")
    {
        var sw = Stopwatch.StartNew();
        input = Unzipped(input);
        var pass = new Pass(input, o, progress, ct);
        var report = pass.Run();
        var counter = new CountingStream(output);
        if (o.Zip)
        {
            using var zip = new ZipArchive(counter, ZipArchiveMode.Create, leaveOpen: true);
            var inner = name.EndsWith(".ifczip", StringComparison.OrdinalIgnoreCase) ? name[..^3] : name.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase) ? name : name + ".ifc";
            using var s = zip.CreateEntry(inner, CompressionLevel.Optimal).Open();
            pass.Write(s);
        }
        else pass.Write(counter);
        report.BytesBefore = input.Length;
        report.BytesAfter = counter.Written;
        report.Seconds = sw.Elapsed.TotalSeconds;
        return report;
    }

    /// <summary>A file's entities as written: id → its type and its text from '#' to ';' (for comparing a copy with its original).</summary>
    public static Dictionary<int, (string Type, string Text)> Entities(byte[] bytes)
    {
        var s = Scan.Of(Unzipped(bytes), CancellationToken.None);
        var o = new Dictionary<int, (string, string)>(s.Count);
        for (var k = 0; k < s.Count; k++) o[s.Id[k]] = (s.Type[k], Encoding.Latin1.GetString(bytes, s.Start[k], s.End[k] - s.Start[k]));
        return o;
    }

    /// <summary>A stream that counts what goes through it.</summary>
    sealed class CountingStream(Stream inner) : Stream
    {
        public long Written { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => Written;
        public override long Position { get => Written; set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) { inner.Write(buffer, offset, count); Written += count; }
        public override void Write(ReadOnlySpan<byte> buffer) { inner.Write(buffer); Written += buffer.Length; }
    }

    // ─── The file's entities as written ──────────────────────────────────────

    /// <summary>
    /// The DATA section read once, in the file's order: each entity's id, type, where its text starts and ends (after its
    /// ';'), whether it is rooted (a GlobalId first), and what it references. A reference from a styled item to the item it
    /// styles, or from a layer assignment to what it assigns, is an annotation: it does not keep its target alive.
    /// </summary>
    sealed class Scan
    {
        public int Count;
        public int[] Id = [], Start = [], End = [];
        public string[] Type = [];
        public bool[] Rooted = [];
        /// <summary>References by entity (CSR): ids; an annotation's negative.</summary>
        public int[] RefAt = [], Refs = [];
        public int HeaderEnd, DataEnd;
        public int MaxId;

        public static Scan Of(byte[] b, CancellationToken ct)
        {
            var data = b.AsSpan().IndexOf("DATA;"u8);
            if (data < 0) throw new FormatException("The file has no DATA section.");
            var ids = new List<int>(); var starts = new List<int>(); var ends = new List<int>(); var types = new List<string>(); var rooted = new List<bool>();
            var refAt = new List<int> { 0 }; var refs = new List<int>();
            var names = new Dictionary<string, string>();
            var i = data + 5;
            var dataEnd = b.Length;
            var maxId = 0;
            while (i < b.Length)
            {
                var c = b[i];
                if (c is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') { i++; continue; }
                if (c == '/' && i + 1 < b.Length && b[i + 1] == '*') { var e = b.AsSpan(i + 2).IndexOf("*/"u8); i = e < 0 ? b.Length : i + 2 + e + 2; continue; }
                if (b.AsSpan(i).StartsWith("ENDSEC"u8)) { dataEnd = i; break; }
                if (c != '#') { var k = Array.IndexOf(b, (byte)';', i); i = k < 0 ? b.Length : k + 1; continue; }
                if ((ids.Count & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
                var start = i++;
                long id = 0;
                while (i < b.Length && b[i] is >= (byte)'0' and <= (byte)'9') { id = id * 10 + (b[i++] - '0'); if (id > int.MaxValue) throw new FormatException($"STEP: an entity id too large at byte {start}."); }
                while (i < b.Length && b[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') i++;
                if (i < b.Length && b[i] == '=') i++;
                while (i < b.Length && b[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') i++;
                var t0 = i;
                while (i < b.Length && (char.IsAsciiLetterOrDigit((char)b[i]) || b[i] == '_')) i++;
                var raw = Encoding.ASCII.GetString(b, t0, i - t0).ToUpperInvariant();
                if (!names.TryGetValue(raw, out var type)) names[raw] = type = raw;
                // Which attribute's references are annotations.
                var annotation = type == "IFCSTYLEDITEM" ? 0 : type.StartsWith("IFCPRESENTATIONLAYERASSIGNMENT", StringComparison.Ordinal) ? 2 : -1;
                var p = i;
                while (p < b.Length && b[p] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') p++;
                var isRooted = p + 24 < b.Length && b[p] == '(' && Quote22(b, p + 1);
                var depth = 0; var attr = 0; var inString = false;
                while (i < b.Length)
                {
                    var ch = b[i++];
                    if (inString) { if (ch == '\'') inString = false; continue; }
                    if (ch == '\'') { inString = true; continue; }
                    if (ch == ';' && depth <= 0) break;
                    if (ch == '(') depth++;
                    else if (ch == ')') depth--;
                    else if (ch == ',' && depth == 1) attr++;
                    else if (ch == '#')
                    {
                        long r = 0; var any = false;
                        while (i < b.Length && b[i] is >= (byte)'0' and <= (byte)'9') { r = r * 10 + (b[i++] - '0'); any = true; }
                        if (any && r <= int.MaxValue) refs.Add(attr == annotation ? -(int)r : (int)r);
                    }
                }
                ids.Add((int)id); starts.Add(start); ends.Add(i); types.Add(type); rooted.Add(isRooted); refAt.Add(refs.Count);
                maxId = Math.Max(maxId, (int)id);
            }
            return new Scan
            {
                Count = ids.Count, Id = [.. ids], Start = [.. starts], End = [.. ends], Type = [.. types], Rooted = [.. rooted], RefAt = [.. refAt], Refs = [.. refs],
                HeaderEnd = starts.Count > 0 ? starts[0] : dataEnd, DataEnd = dataEnd, MaxId = maxId,
            };
        }

        /// <summary>Whether a 22-character string (a GlobalId) starts at <paramref name="p"/>, past blanks.</summary>
        static bool Quote22(byte[] b, int p)
        {
            while (p < b.Length && b[p] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') p++;
            return p + 23 < b.Length && b[p] == '\'' && b[p + 23] == '\'' && b.AsSpan(p + 1, 22).IndexOf((byte)'\'') < 0;
        }
    }

    /// <summary>The references in an entity's text (an annotation's negative), as <see cref="Scan"/> reads them.</summary>
    static void RefsOf(string type, IReadOnlyList<string> args, List<int> into)
    {
        var annotation = type == "IFCSTYLEDITEM" ? 0 : type.StartsWith("IFCPRESENTATIONLAYERASSIGNMENT", StringComparison.Ordinal) ? 2 : -1;
        for (var a = 0; a < args.Count; a++)
        {
            var s = args[a];
            var inString = false;
            for (var i = 0; i < s.Length; i++)
            {
                var ch = s[i];
                if (inString) { if (ch == '\'') inString = false; continue; }
                if (ch == '\'') { inString = true; continue; }
                if (ch != '#') continue;
                long r = 0; var any = false;
                while (i + 1 < s.Length && char.IsAsciiDigit(s[i + 1])) { r = r * 10 + (s[++i] - '0'); any = true; }
                if (any && r <= int.MaxValue) into.Add(a == annotation ? -(int)r : (int)r);
            }
        }
    }

    /// <summary>An entity's attributes as written, split at its top-level commas (strings and nested lists honoured); and its type as written.</summary>
    static (string Type, List<string> Args) RawArgs(byte[] b, int start, int end)
    {
        var text = Encoding.Latin1.GetString(b, start, end - start);
        var eq = text.IndexOf('=');
        var open = text.IndexOf('(', eq + 1);
        var type = text[(eq + 1)..open].Trim();
        var args = new List<string>();
        var depth = 0; var inString = false; var from = open + 1;
        for (var i = open + 1; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString) { if (ch == '\'') inString = false; continue; }
            if (ch == '\'') { inString = true; continue; }
            if (ch == '(') depth++;
            else if (ch == ')')
            {
                if (depth == 0) { var last = text[from..i].Trim(); if (last.Length > 0 || args.Count > 0) args.Add(last); break; }
                depth--;
            }
            else if (ch == ',' && depth == 0) { args.Add(text[from..i].Trim()); from = i + 1; }
        }
        return (type, args);
    }

    /// <summary>The ids a list attribute written as text names (#1,#2).</summary>
    static List<int> ListRefs(string raw)
    {
        var o = new List<int>();
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] != '#') continue;
            var n = 0; var any = false;
            while (i + 1 < raw.Length && char.IsAsciiDigit(raw[i + 1])) { n = n * 10 + (raw[++i] - '0'); any = true; }
            if (any) o.Add(n);
        }
        return o;
    }

    static string RefList(IEnumerable<int> ids) => "(" + string.Join(",", ids.Select(i => "#" + i.ToString(CultureInfo.InvariantCulture))) + ")";

    /// <summary>What may be removed when nothing references it: geometry and presentation resources, never an IfcRoot (they have no GlobalId).</summary>
    public static IReadOnlySet<string> Resources => Removable;

    static readonly HashSet<string> Removable =
    [
        "IFCCARTESIANPOINT", "IFCCARTESIANPOINTLIST2D", "IFCCARTESIANPOINTLIST3D", "IFCDIRECTION", "IFCVECTOR", "IFCAXIS1PLACEMENT", "IFCAXIS2PLACEMENT2D", "IFCAXIS2PLACEMENT3D",
        "IFCLOCALPLACEMENT", "IFCPOLYLOOP", "IFCFACE", "IFCFACEOUTERBOUND", "IFCFACEBOUND", "IFCCLOSEDSHELL", "IFCOPENSHELL", "IFCCONNECTEDFACESET",
        "IFCFACETEDBREP", "IFCFACETEDBREPWITHVOIDS", "IFCSHELLBASEDSURFACEMODEL", "IFCFACEBASEDSURFACEMODEL", "IFCTRIANGULATEDFACESET", "IFCPOLYGONALFACESET",
        "IFCINDEXEDPOLYGONALFACE", "IFCINDEXEDPOLYGONALFACEWITHVOIDS", "IFCPOLYLINE", "IFCSHAPEREPRESENTATION", "IFCPRODUCTDEFINITIONSHAPE", "IFCREPRESENTATIONMAP",
        "IFCMAPPEDITEM", "IFCCARTESIANTRANSFORMATIONOPERATOR3D", "IFCCARTESIANTRANSFORMATIONOPERATOR3DNONUNIFORM", "IFCEXTRUDEDAREASOLID",
        "IFCRECTANGLEPROFILEDEF", "IFCARBITRARYCLOSEDPROFILEDEF", "IFCARBITRARYPROFILEDEFWITHVOIDS", "IFCCIRCLEPROFILEDEF", "IFCBOOLEANRESULT", "IFCBOOLEANCLIPPINGRESULT",
        "IFCHALFSPACESOLID", "IFCPOLYGONALBOUNDEDHALFSPACE", "IFCPLANE", "IFCSURFACESTYLE", "IFCSURFACESTYLERENDERING", "IFCSURFACESTYLESHADING", "IFCCOLOURRGB",
        "IFCPRESENTATIONSTYLEASSIGNMENT",
    ];

    // ─── The pass ────────────────────────────────────────────────────────────

    enum Kind { Tri, Poly, Shell }

    /// <summary>A tessellated body's mesh, as the file has it: a face set, or one shell of plain faces.</summary>
    sealed class Unit
    {
        public int Ord;
        public Kind Kind;
        public double[] P = [];
        public int[] T = [];
        /// <summary>A shell's vertices' point ids.</summary>
        public int[] PointIds = [];
        /// <summary>A shell's faces: the face's ord, its bounds (ord, loop ord, vertex indices, outer).</summary>
        public List<(int Face, List<(int Bound, int Loop, int[] Ring, bool Outer, bool Forward)> Bounds)> Faces = [];
        public int TrianglesBefore;
        /// <summary>Whether it may be rewritten (its faces, bounds and loops its own; no normals, no colour or texture map).</summary>
        public bool Editable;
        /// <summary>A shell's own faces, bounds and loops (nothing else references them): cleaned in place.</summary>
        public bool OwnsFaces;
        public string Closed = "$";
        /// <summary>Dropped: its body is shared from another's.</summary>
        public bool Dropped;
        // The outcome.
        public double[]? NewP;
        public int[]? NewT;
        public double Stray;
        public bool Simplified, Cleaned, Written;
        /// <summary>A shell cleaned in place: each loop's new ring (point ids), the faces left out.</summary>
        public Dictionary<int, int[]>? Loops;
        public HashSet<int>? DroppedFaces;
        public Dictionary<int, List<int>>? FaceBounds;
        public int TrianglesAfter;
    }

    sealed class Pass(byte[] b, IfcOptimizeOptions o, Action<string, int, int>? progress, CancellationToken ct)
    {
        Scan _s = null!;
        StepFile _f = null!;
        int[]? _ordById;
        Dictionary<int, int>? _ordSparse;
        /// <summary>Who references each entity (CSR by ord): ords; an annotation's negative minus one.</summary>
        int[] _byAt = [], _by = [];
        double _len = 1;
        string _schema = "";
        readonly IfcOptimizeReport _r = new();
        /// <summary>Entities rewritten (ord → type as written and attributes).</summary>
        readonly Dictionary<int, (string Type, List<string> Args)> _edit = [];
        /// <summary>Entities added: id → text from the type on.</summary>
        readonly List<(int Id, string Type, List<string> Args)> _added = [];
        readonly HashSet<int> _removed = [];
        int _nextId;
        string _nl = "\n";
        readonly Dictionary<int, Unit> _units = [];

        int Ord(int id)
        {
            if (_ordById != null) return id >= 0 && id < _ordById.Length ? _ordById[id] - 1 : -1;
            return _ordSparse!.TryGetValue(id, out var k) ? k : -1;
        }

        string TypeOf(int id) { var k = Ord(id); return k < 0 ? "" : _s.Type[k]; }

        IEnumerable<int> RefsOf(int ord) { for (var k = _s.RefAt[ord]; k < _s.RefAt[ord + 1]; k++) yield return _s.Refs[k]; }

        /// <summary>Who references an entity, structurally (ords).</summary>
        IEnumerable<int> Users(int ord) { for (var k = _byAt[ord]; k < _byAt[ord + 1]; k++) if (_by[k] >= 0) yield return _by[k]; }

        /// <summary>Who annotates an entity: the styled items that style it, the layer assignments that hold it (ords).</summary>
        IEnumerable<int> Annotators(int ord) { for (var k = _byAt[ord]; k < _byAt[ord + 1]; k++) if (_by[k] < 0) yield return -_by[k] - 1; }

        (string Type, List<string> Args) Args(int ord) => _edit.TryGetValue(ord, out var e) ? e : RawArgs(b, _s.Start[ord], _s.End[ord]);

        void Edit(int ord, string type, List<string> args) => _edit[ord] = (type, args);

        int Add(string type, params string[] args) { var id = _nextId++; _added.Add((id, type, [.. args])); return id; }

        void Progress(string stage, int done = 0, int total = 0) { ct.ThrowIfCancellationRequested(); progress?.Invoke(stage, done, total); }

        public IfcOptimizeReport Run()
        {
            Progress("Reading the file");
            _s = Scan.Of(b, ct);
            if (b.AsSpan(0, Math.Min(b.Length, 1 << 16)).IndexOf("\r\n"u8) >= 0) _nl = "\r\n";
            _r.EntitiesBefore = _s.Count;
            if ((long)_s.MaxId <= 4L * _s.Count + 1_000_000)
            {
                _ordById = new int[_s.MaxId + 1];
                for (var k = 0; k < _s.Count; k++) _ordById[_s.Id[k]] = k + 1;
            }
            else
            {
                _ordSparse = new Dictionary<int, int>(_s.Count);
                for (var k = 0; k < _s.Count; k++) _ordSparse[_s.Id[k]] = k;
            }
            _nextId = _s.MaxId + 1;
            // Who references whom.
            var counts = new int[_s.Count + 1];
            foreach (var r in _s.Refs) { var t = Ord(Math.Abs(r)); if (t >= 0) counts[t + 1]++; }
            for (var k = 0; k < _s.Count; k++) counts[k + 1] += counts[k];
            _byAt = counts;
            _by = new int[counts[_s.Count]];
            var fill = (int[])counts.Clone();
            for (var k = 0; k < _s.Count; k++)
                foreach (var r in RefsOf(k)) { var t = Ord(Math.Abs(r)); if (t >= 0) _by[fill[t]++] = r < 0 ? -k - 1 : k; }
            _f = StepFile.Read(b);
            _schema = _f.Schema;
            _r.Schema = _schema;
            _len = IfcModel.ProjectUnits(_f).Length;
            if (!(_len > 0)) _len = 1;

            ReadUnits();
            if (o.Share) { Share(); MergeMaps(); }
            Process();
            WriteUnits();
            if (o.Orphans) DropEmptyRepresentations();
            RemoveOrphans();
            _r.EntitiesAfter = _s.Count - _removed.Count + _added.Count;
            return _r;
        }

        // ─── The tessellated bodies ──────────────────────────────────────────

        bool Ifc4 => !_schema.StartsWith("IFC2X", StringComparison.Ordinal);

        void ReadUnits()
        {
            var shells = new List<int>();
            for (var k = 0; k < _s.Count; k++)
                switch (_s.Type[k])
                {
                    case "IFCTRIANGULATEDFACESET": _units[k] = new Unit { Ord = k, Kind = Kind.Tri }; break;
                    case "IFCPOLYGONALFACESET": _units[k] = new Unit { Ord = k, Kind = Kind.Poly }; break;
                    case "IFCFACETEDBREP" or "IFCFACETEDBREPWITHVOIDS" or "IFCSHELLBASEDSURFACEMODEL" or "IFCFACEBASEDSURFACEMODEL":
                        foreach (var r in RefsOf(k)) { var t = Ord(r); if (t >= 0 && _s.Type[t] is "IFCCLOSEDSHELL" or "IFCOPENSHELL" or "IFCCONNECTEDFACESET") shells.Add(t); }
                        break;
                }
            foreach (var sh in shells.Distinct()) _units[sh] = new Unit { Ord = sh, Kind = Kind.Shell };
            var list = _units.Values.ToList();
            var done = 0;
            Progress("Reading the bodies", 0, list.Count);
            Parallel.ForEach(list, new ParallelOptions { CancellationToken = ct }, u =>
            {
                try { Read(u); }
                catch (Exception e) when (e is FormatException or InvalidCastException or IndexOutOfRangeException or ArgumentException) { u.Editable = false; }
                var n = Interlocked.Increment(ref done);
                if ((n & 255) == 0) progress?.Invoke("Reading the bodies", n, list.Count);
            });
            _r.Bodies = list.Count;
            _r.TrianglesBefore = list.Sum(u => (long)u.TrianglesBefore);
            _r.Kept = list.Count(u => u.Kind == Kind.Tri && !u.Editable && u.T.Length > 0);
        }

        static double N(object? v) => IfcModel.Num(v);

        void Read(Unit u)
        {
            var e = _f.Entities.Parse(_s.Id[u.Ord])!;
            switch (u.Kind)
            {
                case Kind.Tri:
                {
                    var pts = PointList(e[0]);
                    var pn = e[4] is List<object?> pl ? pl.Select(x => (int)N(x)).ToArray() : [];
                    var tris = new List<int>();
                    if (e[3] is List<object?> rows)
                        foreach (var row in rows.OfType<List<object?>>())
                        {
                            if (row.Count != 3) continue;
                            int a = V((int)N(row[0])), c1 = V((int)N(row[1])), c2 = V((int)N(row[2]));
                            if (a >= 0 && c1 >= 0 && c2 >= 0) { tris.Add(a); tris.Add(c1); tris.Add(c2); }
                        }
                    int V(int i)
                    {
                        if (pn.Length > 0) i = i >= 1 && i <= pn.Length ? pn[i - 1] : 0;
                        return i >= 1 && i <= pts.Length / 3 ? i - 1 : -1;
                    }
                    u.P = pts; u.T = [.. tris];
                    u.TrianglesBefore = tris.Count / 3;
                    // Normals: the list attribute between the coordinates and the indices (IFC4: second, IFC4X3: third).
                    var hasNormals = e[1] is List<object?> { Count: > 0 } || e[2] is List<object?> { Count: > 0 };
                    u.Closed = Args(u.Ord).Args.ElementAtOrDefault(X3 ? 1 : 2) ?? "$";
                    u.Editable = !hasNormals && !Mapped(u.Ord) && pts.Length > 0;
                    break;
                }
                case Kind.Poly:
                {
                    var pts = PointList(e[0]);
                    var pn = e[3] is List<object?> pl ? pl.Select(x => (int)N(x)).ToArray() : [];
                    var count = pts.Length / 3;
                    var m = new MeshBuilder();
                    for (var i = 0; i < count; i++) m.Add(new Vec3(pts[3 * i], pts[3 * i + 1], pts[3 * i + 2]));
                    int V(int i)
                    {
                        if (pn.Length > 0) i = i >= 1 && i <= pn.Length ? pn[i - 1] : 0;
                        return i >= 1 && i <= count ? i - 1 : -1;
                    }
                    int[] At(object? l) => l is List<object?> x ? [.. x.Select(v => V((int)N(v))).Where(v => v >= 0)] : [];
                    if (e[2] is List<object?> faces)
                        foreach (var fr in faces)
                        {
                            var f = _f.Get(fr);
                            if (f == null) continue;
                            m.Polygon(At(f[0]), f.Type == "IFCINDEXEDPOLYGONALFACEWITHVOIDS" && f[1] is List<object?> inner ? [.. inner.Select(At)] : []);
                        }
                    u.P = pts; u.T = Flat(m.T);
                    u.TrianglesBefore = m.T.Count;
                    u.Closed = Args(u.Ord).Args.ElementAtOrDefault(1) ?? "$";
                    u.Editable = !Mapped(u.Ord) && pts.Length > 0;
                    break;
                }
                case Kind.Shell:
                {
                    var index = new Dictionary<int, int>();
                    var pts = new List<double>();
                    var ids = new List<int>();
                    var m = new MeshBuilder();
                    var ok = true;
                    var owns = true;
                    var faces = e[0] is List<object?> fl ? fl : [];
                    foreach (var fr in faces)
                    {
                        var face = _f.Get(fr);
                        if (face is not { Type: "IFCFACE" } || face[0] is not List<object?> bl) { ok = false; break; }
                        var fOrd = Ord(face.Id);
                        owns &= Users(fOrd).Count() == 1 && !Annotators(fOrd).Any();
                        var bounds = new List<(int, int, int[], bool, bool)>();
                        foreach (var br in bl)
                        {
                            var bound = _f.Get(br);
                            var loop = _f.Get(bound?[0]);
                            if (bound is not { Type: "IFCFACEOUTERBOUND" or "IFCFACEBOUND" } || loop is not { Type: "IFCPOLYLOOP" } || loop[0] is not List<object?> ring) { ok = false; break; }
                            owns &= Users(Ord(bound.Id)).Count() == 1 && Users(Ord(loop.Id)).Count() == 1;
                            var vs = new int[ring.Count];
                            for (var i = 0; i < ring.Count; i++)
                            {
                                if (ring[i] is not StepRef pr) { ok = false; break; }
                                if (!index.TryGetValue(pr.Id, out var vi))
                                {
                                    var pt = _f.Get(pr);
                                    if (pt is not { Type: "IFCCARTESIANPOINT" } || pt[0] is not List<object?> { Count: 3 } c) { ok = false; break; }
                                    vi = index[pr.Id] = ids.Count;
                                    ids.Add(pr.Id);
                                    pts.Add(N(c[0])); pts.Add(N(c[1])); pts.Add(N(c[2]));
                                    m.Add(new Vec3(N(c[0]), N(c[1]), N(c[2])));
                                }
                                vs[i] = vi;
                            }
                            if (!ok) break;
                            var forward = (bound[1] as StepEnum)?.Value != "F";
                            bounds.Add((Ord(bound.Id), Ord(loop.Id), vs, bound.Type == "IFCFACEOUTERBOUND", forward));
                        }
                        if (!ok) break;
                        u.Faces.Add((fOrd, bounds));
                        var outer = bounds.FirstOrDefault(x => x.Item4);
                        if (outer.Item3 == null) outer = bounds.FirstOrDefault();
                        if (outer.Item3 == null) continue;
                        static int[] Oriented((int, int, int[] Ring, bool, bool Forward) x) => x.Forward ? x.Ring : [.. x.Ring.Reverse()];
                        m.Polygon(Oriented(outer), [.. bounds.Where(x => x.Item1 != outer.Item1).Select(Oriented)]);
                    }
                    if (!ok) { u.Editable = false; u.Faces.Clear(); return; }
                    u.P = [.. pts]; u.PointIds = [.. ids]; u.T = Flat(m.T);
                    u.TrianglesBefore = m.T.Count;
                    u.OwnsFaces = owns;
                    u.Closed = _s.Type[u.Ord];
                    u.Editable = u.T.Length > 0;
                    break;
                }
            }
        }

        /// <summary>Whether a face set is given colours or textures by its triangles' indices (a colour or texture map references it).</summary>
        bool Mapped(int ord) => Users(ord).Any(k => _s.Type[k] is "IFCINDEXEDCOLOURMAP" or "IFCINDEXEDTRIANGLETEXTUREMAP" or "IFCINDEXEDPOLYGONALTEXTUREMAP");

        static int[] Flat(List<(int A, int B, int C)> t)
        {
            var o = new int[t.Count * 3];
            for (var i = 0; i < t.Count; i++) { o[3 * i] = t[i].A; o[3 * i + 1] = t[i].B; o[3 * i + 2] = t[i].C; }
            return o;
        }

        double[] PointList(object? r)
        {
            if (_f.Get(r) is not { } pl || pl[0] is not List<object?> rows) return [];
            var o = new double[rows.Count * 3];
            for (var i = 0; i < rows.Count; i++)
                if (rows[i] is List<object?> c) { o[3 * i] = N(c.ElementAtOrDefault(0)); o[3 * i + 1] = N(c.ElementAtOrDefault(1)); o[3 * i + 2] = c.Count > 2 ? N(c[2]) : 0; }
            return o;
        }

        // ─── Shared bodies ───────────────────────────────────────────────────

        /// <summary>A body item's units, in order; null when an item is not a tessellated body this pass reads.</summary>
        List<Unit>? ItemUnits(int itemOrd)
        {
            if (_units.TryGetValue(itemOrd, out var u)) return u.T.Length > 0 ? [u] : null;
            if (_s.Type[itemOrd] is not ("IFCFACETEDBREP" or "IFCFACETEDBREPWITHVOIDS" or "IFCSHELLBASEDSURFACEMODEL" or "IFCFACEBASEDSURFACEMODEL")) return null;
            var o = new List<Unit>();
            foreach (var r in RefsOf(itemOrd))
            {
                var t = Ord(r);
                if (t < 0) return null;
                if (_units.TryGetValue(t, out var su)) { if (su.T.Length == 0) return null; o.Add(su); }
            }
            return o.Count > 0 ? o : null;
        }

        /// <summary>How an item is styled: its styled items' styles and names as written (the item itself left out), sorted.</summary>
        string? StyleOf(int itemOrd)
        {
            var parts = new List<string>();
            foreach (var a in Annotators(itemOrd))
            {
                if (_s.Type[a] != "IFCSTYLEDITEM" || Users(a).Any()) return null; // a layer, or a styled item another uses: kept as it is
                // The styles by what they say (two styled items alike may each have their own style entities).
                var args = Args(a).Args;
                parts.Add(string.Join(",", args.Skip(1).Select(x => ListRefs(x) is { Count: > 0 } refs ? string.Join("+", refs.Select(r => Deep(Ord(r), 0))) : x)));
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        /// <summary>A representation's bodies for sharing: its items' units (null when one is not a body, or another uses it) and their styles.</summary>
        (List<int> Items, List<List<Unit>> Units, string Style)? Bodies(int repOrd, bool structuralOnlyHere)
        {
            var (_, args) = Args(repOrd);
            if (args.Count < 4) return null;
            var items = ListRefs(args[3]).Select(Ord).ToList();
            if (items.Count == 0 || items.Any(i => i < 0)) return null;
            var units = new List<List<Unit>>();
            var style = new StringBuilder();
            foreach (var it in items)
            {
                if (ItemUnits(it) is not { } us || us.Any(x => !x.Editable)) return null;
                if (structuralOnlyHere && Users(it).Any(x => x != repOrd)) return null;
                if (StyleOf(it) is not { } st) return null;
                units.Add(us); style.Append(_s.Type[it]).Append(':').Append(st).Append(';');
            }
            return (items, units, style.ToString());
        }

        /// <summary>A body's geometry as a key, moved so its first corner is at <paramref name="t"/>: corners within 0.001 mm and the triangles as they are.</summary>
        ulong Hash(List<List<Unit>> units, string style, string head, (double X, double Y, double Z) t, double eps)
        {
            var h = new XxHash();
            h.Add(head); h.Add(style);
            foreach (var us in units)
                foreach (var u in us)
                {
                    h.Add((int)u.Kind); h.Add(u.Closed); h.Add(u.P.Length); h.Add(u.T.Length);
                    for (var i = 0; i < u.P.Length; i += 3)
                    {
                        h.Add((long)Math.Round((u.P[i] - t.X) / eps)); h.Add((long)Math.Round((u.P[i + 1] - t.Y) / eps)); h.Add((long)Math.Round((u.P[i + 2] - t.Z) / eps));
                    }
                    foreach (var x in u.T) h.Add(x);
                }
            return h.Value;
        }

        static bool Same(List<List<Unit>> a, (double X, double Y, double Z) ta, List<List<Unit>> b, (double X, double Y, double Z) tb, double eps)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++)
            {
                if (a[i].Count != b[i].Count) return false;
                for (var j = 0; j < a[i].Count; j++)
                {
                    var (x, y) = (a[i][j], b[i][j]);
                    if (x.Kind != y.Kind || x.Closed != y.Closed || x.P.Length != y.P.Length || !x.T.AsSpan().SequenceEqual(y.T)) return false;
                    for (var k = 0; k < x.P.Length; k += 3)
                        if (Math.Abs(x.P[k] - ta.X - (y.P[k] - tb.X)) > eps || Math.Abs(x.P[k + 1] - ta.Y - (y.P[k + 1] - tb.Y)) > eps || Math.Abs(x.P[k + 2] - ta.Z - (y.P[k + 2] - tb.Z)) > eps) return false;
                }
            }
            return true;
        }

        static (double, double, double) First(List<List<Unit>> units) { var u = units[0][0]; return (u.P[0], u.P[1], u.P[2]); }

        /// <summary>The bytes a body's entities take in the file (its face sets and point lists, or its shells' faces, bounds, loops and the points only it uses).</summary>
        long Bytes(List<List<Unit>> units)
        {
            long n = 0;
            foreach (var us in units)
                foreach (var u in us)
                {
                    n += _s.End[u.Ord] - _s.Start[u.Ord];
                    if (u.Kind != Kind.Shell) { foreach (var r in RefsOf(u.Ord).Take(1)) { var t = Ord(r); if (t >= 0) n += _s.End[t] - _s.Start[t]; } continue; }
                    foreach (var (face, bounds) in u.Faces)
                    {
                        n += _s.End[face] - _s.Start[face];
                        foreach (var bd in bounds) n += _s.End[bd.Bound] - _s.Start[bd.Bound] + _s.End[bd.Loop] - _s.Start[bd.Loop];
                    }
                    n += u.PointIds.Length * 40L;
                }
            return n;
        }

        /// <summary>The bytes a mapped item costs each product: the item, its operator and its point.</summary>
        const int MappedCost = 160;

        int _origin = -1, _identity = -1;

        /// <summary>
        /// Identical bodies (the same mesh, moved) written once: each group's first representation's items moved into a new
        /// IfcRepresentationMap, and every representation of the group (the first too) made one IfcMappedItem of it, moved by
        /// the difference. A representation takes part when products alone use it, each of its items is a tessellated body
        /// only it uses, styled alike, on no layer of its own. The representations keep their numbers, the products are untouched.
        /// </summary>
        void Share()
        {
            Progress("Finding shared bodies");
            var eps = 1e-6 / _len;
            var reps = new List<(int Rep, List<int> Items, List<List<Unit>> Units, (double, double, double) T, ulong Key)>();
            for (var k = 0; k < _s.Count; k++)
            {
                if (_s.Type[k] != "IFCSHAPEREPRESENTATION") continue;
                // A layer may hold the representation: it keeps its number, so the layer still holds it.
                if (!Users(k).Any() || Users(k).Any(x => _s.Type[x] != "IFCPRODUCTDEFINITIONSHAPE")) continue;
                if (Bodies(k, true) is not { } body) continue;
                var (_, args) = Args(k);
                var head = string.Join(",", args.Take(3));
                var t = First(body.Units);
                reps.Add((k, body.Items, body.Units, t, Hash(body.Units, body.Style, head, t, eps)));
            }
            foreach (var group in reps.GroupBy(r => r.Key).Where(g => g.Count() > 1))
            {
                ct.ThrowIfCancellationRequested();
                // Within the key, the bodies that are the same as the first (a key's collision splits into another round).
                var rest = group.ToList();
                while (rest.Count > 1)
                {
                    var a = rest[0];
                    var same = rest.Where(r => Same(a.Units, a.T, r.Units, r.T, eps)).ToList();
                    rest = [.. rest.Except(same)];
                    if (same.Count < 2) continue;
                    var size = Bytes(a.Units);
                    if ((same.Count - 1) * size <= same.Count * MappedCost) continue;
                    var (_, aArgs) = Args(a.Rep);
                    if (_origin < 0)
                    {
                        _origin = Add("IFCCARTESIANPOINT", "(0.,0.,0.)");
                        _identity = Add("IFCCARTESIANTRANSFORMATIONOPERATOR3D", "$", "$", "#" + _origin, "1.", "$");
                    }
                    var placement = Add("IFCAXIS2PLACEMENT3D", "#" + _origin, "$", "$");
                    var mapRep = Add("IFCSHAPEREPRESENTATION", aArgs[0], aArgs[1], aArgs[2], aArgs[3]);
                    var map = Add("IFCREPRESENTATIONMAP", "#" + placement, "#" + mapRep);
                    foreach (var m in same)
                    {
                        string op;
                        if (m.Rep == a.Rep) op = "#" + _identity;
                        else
                        {
                            var (dx, dy, dz) = (m.T.Item1 - a.T.Item1, m.T.Item2 - a.T.Item2, m.T.Item3 - a.T.Item3);
                            var p = Add("IFCCARTESIANPOINT", $"({Num(Math.Round(dx, 9))},{Num(Math.Round(dy, 9))},{Num(Math.Round(dz, 9))})");
                            op = "#" + Add("IFCCARTESIANTRANSFORMATIONOPERATOR3D", "$", "$", "#" + p, "1.", "$");
                            foreach (var us in m.Units) foreach (var u in us) u.Dropped = true;
                        }
                        var item = Add("IFCMAPPEDITEM", "#" + map, op);
                        var (type, args) = Args(m.Rep);
                        var na = new List<string>(args) { [2] = "'MappedRepresentation'", [3] = $"(#{item})" };
                        Edit(m.Rep, type, na);
                    }
                    _r.Shared += same.Count;
                    _r.SharedBodies++;
                }
            }
            // A unit shared by a kept body stays (a shell two bodies use).
            foreach (var u in _units.Values.Where(u => u.Dropped))
                if (Users(u.Ord).Any(x => !_units.ContainsKey(x) && !_edit.ContainsKey(x) && !DroppedItem(x))) u.Dropped = false;
        }

        bool DroppedItem(int ord) => ItemUnits(ord) is { } us && us.All(u => u.Dropped);

        /// <summary>
        /// Identical representation maps made one: a map whose placement and bodies are another's, used by mapped items only,
        /// has its mapped items point to the other; it is left to nothing and goes with what only it used.
        /// </summary>
        void MergeMaps()
        {
            var eps = 1e-6 / _len;
            var maps = new List<(int Map, List<List<Unit>> Units, ulong Key)>();
            for (var k = 0; k < _s.Count; k++)
            {
                if (_s.Type[k] != "IFCREPRESENTATIONMAP") continue;
                var (_, args) = Args(k);
                if (args.Count < 2) continue;
                var rep = Ord(ListRefs(args[1]).FirstOrDefault());
                if (rep < 0 || _s.Type[rep] != "IFCSHAPEREPRESENTATION" || Users(rep).Any(x => x != k)) continue;
                if (Bodies(rep, true) is not { } body) continue;
                var head = Deep(Ord(ListRefs(args[0]).FirstOrDefault()), 0) + "|" + string.Join(",", Args(rep).Args.Take(3));
                maps.Add((k, body.Units, Hash(body.Units, body.Style, head, (0, 0, 0), eps)));
            }
            foreach (var group in maps.GroupBy(m => m.Key).Where(g => g.Count() > 1))
            {
                var rest = group.ToList();
                while (rest.Count > 1)
                {
                    var a = rest[0];
                    var same = rest.Where(r => Same(a.Units, (0, 0, 0), r.Units, (0, 0, 0), eps)).ToList();
                    rest = [.. rest.Except(same)];
                    foreach (var m in same.Skip(1))
                    {
                        var users = Users(m.Map).ToList();
                        if (users.Count == 0 || users.Any(x => _s.Type[x] != "IFCMAPPEDITEM")) continue;
                        foreach (var mi in users)
                        {
                            var (type, args) = Args(mi);
                            Edit(mi, type, new List<string>(args) { [0] = "#" + _s.Id[a.Map] });
                        }
                        foreach (var us in m.Units) foreach (var u in us) u.Dropped = true;
                        _r.MapsMerged++;
                    }
                }
            }
        }

        /// <summary>An entity's text with every entity it references written out in place (a placement compared by what it says).</summary>
        string Deep(int ord, int depth)
        {
            if (ord < 0 || depth > 8) return "?";
            var (type, args) = Args(ord);
            var sb = new StringBuilder(type).Append('(');
            foreach (var a in args)
            {
                var refs = ListRefs(a);
                sb.Append(refs.Count == 0 ? a : string.Join("+", refs.Select(r => Deep(Ord(r), depth + 1)))).Append(',');
            }
            return sb.Append(')').ToString();
        }

        // ─── Each body made smaller ──────────────────────────────────────────

        void Process()
        {
            var tol = o.ToleranceMm / 1000 / _len;
            var weld = Math.Min(1e-5 / _len, tol);
            var round = o.RoundMm > 0 ? o.RoundMm / 1000 / _len : 0;
            // The largest first, so the cores end together.
            var list = _units.Values.Where(u => u.Editable && !u.Dropped).OrderByDescending(u => u.T.Length).ToList();
            var done = 0;
            Progress("Simplifying", 0, list.Count);
            Parallel.ForEach(list, new ParallelOptions { CancellationToken = ct }, u =>
            {
                Improve(u, tol, weld, round);
                var n = Interlocked.Increment(ref done);
                if ((n & 63) == 0) progress?.Invoke("Simplifying", n, list.Count);
            });
        }

        void Improve(Unit u, double tol, double weld, double round)
        {
            u.TrianglesAfter = u.TrianglesBefore;
            var p = u.P;
            var t = u.T;
            var stray = 0.0;
            var changed = false;
            // Rounded first: what follows works on what the copy will hold.
            if (round > 0)
            {
                var r = new double[p.Length];
                for (var i = 0; i < p.Length; i++) { r[i] = Math.Round(p[i] / round) * round; stray = Math.Max(stray, Math.Abs(r[i] - p[i]) * Math.Sqrt(3)); }
                changed |= !r.AsSpan().SequenceEqual(p);
                p = r;
            }
            int[]? weldMap = null;
            var welded = 0.0;
            if (o.Clean || o.Simplify)
            {
                var (wp, wt, map, moved) = Weld(p, t, weld);
                var cleanChanged = wt.Length != t.Length || wp.Length != p.Length;
                if (o.Clean && cleanChanged) { u.Cleaned = true; changed = true; stray = Math.Max(stray, moved); }
                if (o.Clean || cleanChanged) { p = wp; t = wt; weldMap = map; welded = moved; }
            }
            if (o.Simplify && t.Length >= 24 && MeshSimplify.Reduce(p, t, tol) is { } red)
            {
                p = red.Positions; t = red.Indices;
                stray = Math.Max(stray, red.Stray + welded);
                u.Simplified = true; changed = true;
                weldMap = null;
            }
            if (!changed) return;
            u.Stray = stray;
            // A shell cleaned but not simplified keeps its faces: its loops point to the welded points, its degenerate and duplicate faces go.
            if (u.Kind == Kind.Shell && !u.Simplified && !(o.BrepsToFaceSets && Ifc4))
            {
                if (!u.OwnsFaces || weldMap == null || round > 0) { u.Cleaned = false; return; }
                CleanShell(u, weldMap);
                return;
            }
            u.NewP = p; u.NewT = t;
            u.TrianglesAfter = t.Length / 3;
        }

        /// <summary>
        /// Corners within <paramref name="tol"/> made one (the first of them stays where it is), triangles with two corners alike or no
        /// area dropped, a triangle repeated (the same corners, either way round) kept once, corners no triangle uses dropped.
        /// </summary>
        static (double[] P, int[] T, int[] Map, double Moved) Weld(double[] p, int[] t, double tol)
        {
            var n = p.Length / 3;
            var at = new Dictionary<(long, long, long), int>();
            var map = new int[n];
            var moved = 0.0;
            for (var i = 0; i < n; i++)
            {
                var key = ((long)Math.Floor(p[3 * i] / tol), (long)Math.Floor(p[3 * i + 1] / tol), (long)Math.Floor(p[3 * i + 2] / tol));
                if (at.TryGetValue(key, out var k)) { map[i] = k; moved = Math.Max(moved, Dist(p, i, k)); }
                else at[key] = map[i] = i;
            }
            var seen = new HashSet<(int, int, int)>();
            var tris = new List<int>(t.Length);
            for (var k = 0; k + 2 < t.Length; k += 3)
            {
                int a = map[t[k]], b = map[t[k + 1]], c = map[t[k + 2]];
                if (a == b || b == c || a == c) continue;
                if (Area2(p, a, b, c) <= tol * tol * 1e-6) continue;
                // The same triangle again, the same way round (one the other way is a face's back, kept).
                var s = a < b && a < c ? (a, b, c) : b < c ? (b, c, a) : (c, a, b);
                if (!seen.Add(s)) continue;
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
            // The corners used, in their first order.
            var used = new int[n];
            Array.Fill(used, -1);
            var pts = new List<double>();
            var order = new List<int>();
            foreach (var v in tris) if (used[v] == -1) { used[v] = -2; order.Add(v); }
            order.Sort();
            for (var i = 0; i < order.Count; i++) { used[order[i]] = i; pts.Add(p[3 * order[i]]); pts.Add(p[3 * order[i] + 1]); pts.Add(p[3 * order[i] + 2]); }
            var nt = new int[tris.Count];
            for (var i = 0; i < tris.Count; i++) nt[i] = used[tris[i]];
            var full = new int[n];
            for (var i = 0; i < n; i++) full[i] = map[i];
            return ([.. pts], nt, full, moved);
        }

        static double Dist(double[] p, int i, int k)
        {
            double dx = p[3 * i] - p[3 * k], dy = p[3 * i + 1] - p[3 * k + 1], dz = p[3 * i + 2] - p[3 * k + 2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        static double Area2(double[] p, int a, int b, int c)
        {
            double ax = p[3 * b] - p[3 * a], ay = p[3 * b + 1] - p[3 * a + 1], az = p[3 * b + 2] - p[3 * a + 2];
            double bx = p[3 * c] - p[3 * a], by = p[3 * c + 1] - p[3 * a + 1], bz = p[3 * c + 2] - p[3 * a + 2];
            double nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
            return Math.Sqrt(nx * nx + ny * ny + nz * nz);
        }

        /// <summary>A shell's loops pointed at the welded points, faces left with no area or repeated left out.</summary>
        void CleanShell(Unit u, int[] weldMap)
        {
            var loops = new Dictionary<int, int[]>();
            var dropped = new HashSet<int>();
            var bounds = new Dictionary<int, List<int>>();
            var seen = new HashSet<string>();
            var tris = 0;
            foreach (var (face, fb) in u.Faces)
            {
                var keep = new List<int>();
                int[]? outerRing = null;
                var faceBuilder = new MeshBuilder();
                foreach (var bd in fb)
                {
                    var ring = new List<int>();
                    foreach (var v in bd.Ring) { var w = weldMap[v]; if (ring.Count == 0 || ring[^1] != w) ring.Add(w); }
                    while (ring.Count > 1 && ring[0] == ring[^1]) ring.RemoveAt(ring.Count - 1);
                    if (ring.Count < 3) { if (bd.Outer || fb.Count == 1) { outerRing = null; keep.Clear(); break; } continue; }
                    if (!ring.SequenceEqual(bd.Ring.Select(v => v))) loops[bd.Loop] = [.. ring.Select(v => u.PointIds[v])];
                    keep.Add(bd.Bound);
                    if (bd.Outer || outerRing == null) outerRing = [.. ring];
                }
                if (outerRing == null || keep.Count == 0) { dropped.Add(face); continue; }
                // No area, or the same face again.
                var mb = new MeshBuilder();
                foreach (var v in outerRing) mb.Add(new Vec3(u.P[3 * v], u.P[3 * v + 1], u.P[3 * v + 2]));
                mb.Polygon([.. Enumerable.Range(0, outerRing.Length)], []);
                if (mb.T.Count == 0) { dropped.Add(face); continue; }
                if (fb.Count == 1 && !seen.Add(string.Join(",", outerRing.Order()))) { dropped.Add(face); continue; }
                if (keep.Count != fb.Count) bounds[face] = keep;
                tris += mb.T.Count;
            }
            if (loops.Count == 0 && dropped.Count == 0 && bounds.Count == 0) { u.Cleaned = false; return; }
            u.Loops = loops; u.DroppedFaces = dropped; u.FaceBounds = bounds;
            u.TrianglesAfter = tris;
        }

        // ─── What is written ─────────────────────────────────────────────────

        /// <summary>A real as STEP writes it, in the fewest digits that read back the same (a coordinate the file had comes out as it was written).</summary>
        internal static string Num(double v)
        {
            if (!double.IsFinite(v)) v = 0;
            var t = v.ToString("R", CultureInfo.InvariantCulture);
            var e = t.IndexOfAny(['E', 'e']);
            if (e >= 0) { var m = t[..e]; return (m.Contains('.') ? m : m + ".") + "E" + t[(e + 1)..]; }
            return t.Contains('.') ? t : t + ".";
        }

        static string Coords(double[] p)
        {
            var sb = new StringBuilder(p.Length * 8).Append('(');
            for (var i = 0; i < p.Length; i += 3)
            {
                if (i > 0) sb.Append(',');
                sb.Append('(').Append(Num(p[i])).Append(',').Append(Num(p[i + 1])).Append(',').Append(Num(p[i + 2])).Append(')');
            }
            return sb.Append(')').ToString();
        }

        static string Indices(int[] t)
        {
            var sb = new StringBuilder(t.Length * 4).Append('(');
            for (var i = 0; i < t.Length; i += 3)
            {
                if (i > 0) sb.Append(',');
                sb.Append('(').Append(t[i] + 1).Append(',').Append(t[i + 1] + 1).Append(',').Append(t[i + 2] + 1).Append(')');
            }
            return sb.Append(')').ToString();
        }

        bool X3 => _schema.StartsWith("IFC4X3", StringComparison.Ordinal);

        /// <summary>A point list for a face set: the one it had, rewritten when only it uses it, else a new one.</summary>
        string PointListFor(int ownerOrd, double[] p)
        {
            var (_, args) = Args(ownerOrd);
            var old = Ord(ListRefs(args.ElementAtOrDefault(0) ?? "").FirstOrDefault());
            var coords = Coords(p);
            if (old >= 0 && _s.Type[old] == "IFCCARTESIANPOINTLIST3D" && Users(old).Count() == 1 && !_edit.ContainsKey(old))
            {
                var (type, pa) = Args(old);
                Edit(old, type, new List<string>(pa) { [0] = coords });
                return "#" + _s.Id[old];
            }
            return "#" + (X3 ? Add("IFCCARTESIANPOINTLIST3D", coords, "$") : Add("IFCCARTESIANPOINTLIST3D", coords));
        }

        /// <summary>A new face set's attributes: the point list, closed as it was, no normals, the triangles, no PnIndex.</summary>
        List<string> FaceSetArgs(string pointList, string closed, int[] t) =>
            X3 ? [pointList, closed, "$", Indices(t), "$"] : [pointList, "$", closed, Indices(t), "$"];

        void WriteUnits()
        {
            Progress("Writing the bodies");
            var converted = new HashSet<int>();
            if (o.BrepsToFaceSets && Ifc4) converted = BrepsToConvert();
            foreach (var u in _units.Values)
            {
                if (u.Dropped) { continue; }
                switch (u.Kind)
                {
                    case Kind.Tri when u.NewP != null:
                    {
                        // Written only when smaller (a rounding that lengthens the numbers is not worth it).
                        if (o.RoundMm <= 0 && Coords(u.NewP).Length + Indices(u.NewT!).Length >= Bytes([[u]])) break;
                        var (type, args) = Args(u.Ord);
                        var na = new List<string>(args);
                        na[0] = PointListFor(u.Ord, u.NewP);
                        if (na.Count > 3) na[3] = Indices(u.NewT!);
                        if (na.Count > 4) na[4] = "$";
                        Edit(u.Ord, type, na);
                        u.Written = true;
                        break;
                    }
                    case Kind.Poly when u.NewP != null:
                    {
                        // Written as triangles only when that is smaller than its polygons.
                        var size = Bytes([[u]]);
                        foreach (var r in RefsOf(u.Ord).Skip(1)) { var t = Ord(r); if (t >= 0 && Users(t).Count() == 1) size += _s.End[t] - _s.Start[t]; }
                        if (Coords(u.NewP).Length + u.NewT!.Length * 4 >= size) { u.Simplified = false; u.Cleaned = false; u.TrianglesAfter = u.TrianglesBefore; break; }
                        Edit(u.Ord, "IFCTRIANGULATEDFACESET", FaceSetArgs(PointListFor(u.Ord, u.NewP), u.Closed, u.NewT));
                        u.Written = true;
                        break;
                    }
                    case Kind.Shell when u.Loops != null:
                    {
                        foreach (var (loop, ring) in u.Loops) Edit(loop, Args(loop).Type, [RefList(ring)]);
                        foreach (var (face, keep) in u.FaceBounds!) { var (type, args) = Args(face); Edit(face, type, new List<string>(args) { [0] = RefList(keep.Select(k => _s.Id[k])) }); }
                        if (u.DroppedFaces!.Count > 0)
                        {
                            var (type, args) = Args(u.Ord);
                            Edit(u.Ord, type, new List<string>(args) { [0] = RefList(ListRefs(args[0]).Where(id => !u.DroppedFaces.Contains(Ord(id)))) });
                        }
                        u.Written = true;
                        break;
                    }
                    case Kind.Shell when u.NewP != null:
                    {
                        // A B-rep converted is written as a face set below; else the shell's faces are new triangles, when they are fewer bytes.
                        if (converted.Count > 0 && Users(u.Ord).Any(converted.Contains)) break;
                        var estimate = u.NewP.Length / 3 * 40L + u.NewT!.Length / 3 * 90L;
                        if (estimate >= Bytes([[u]])) { u.Simplified = false; u.Cleaned = false; u.TrianglesAfter = u.TrianglesBefore; break; }
                        var pts = new int[u.NewP.Length / 3];
                        for (var i = 0; i < pts.Length; i++) pts[i] = Add("IFCCARTESIANPOINT", $"({Num(u.NewP[3 * i])},{Num(u.NewP[3 * i + 1])},{Num(u.NewP[3 * i + 2])})");
                        var faces = new List<int>(u.NewT.Length / 3);
                        for (var i = 0; i < u.NewT.Length; i += 3)
                        {
                            var loop = Add("IFCPOLYLOOP", RefList([pts[u.NewT[i]], pts[u.NewT[i + 1]], pts[u.NewT[i + 2]]]));
                            var bound = Add("IFCFACEOUTERBOUND", "#" + loop, ".T.");
                            faces.Add(Add("IFCFACE", $"(#{bound})"));
                        }
                        var (type, args) = Args(u.Ord);
                        Edit(u.Ord, type, new List<string>(args) { [0] = RefList(faces) });
                        u.Written = true;
                        break;
                    }
                }
            }
            // Faceted B-reps as face sets (IFC4, on request): the B-rep's number now a triangulated face set, its representations' type Tessellation.
            foreach (var brep in converted)
            {
                var shell = _units[Ord(ListRefs(Args(brep).Args[0]).First())];
                var p = shell.NewP ?? shell.P;
                var t = shell.NewT ?? shell.T;
                if (shell.NewP == null) { var (wp, wt, _, _) = Weld(p, t, 1e-9); p = wp; t = wt; }
                Edit(brep, "IFCTRIANGULATEDFACESET", FaceSetArgs("#" + (X3 ? Add("IFCCARTESIANPOINTLIST3D", Coords(p), "$") : Add("IFCCARTESIANPOINTLIST3D", Coords(p))), _s.Type[shell.Ord] == "IFCCLOSEDSHELL" ? ".T." : ".F.", t));
                shell.TrianglesAfter = t.Length / 3;
                shell.Written = true;
                foreach (var rep in Users(brep).Where(x => _s.Type[x] == "IFCSHAPEREPRESENTATION"))
                {
                    var (type, args) = Args(rep);
                    if (args.Count > 2 && args[2].Trim('\'').Equals("Brep", StringComparison.OrdinalIgnoreCase)) Edit(rep, type, new List<string>(args) { [2] = "'Tessellation'" });
                }
                _r.Converted++;
            }
            foreach (var u in _units.Values)
            {
                if (u.Dropped || u.Written) continue;
                // Not written: as the file has it.
                u.Simplified = u.Cleaned = false;
                u.TrianglesAfter = u.TrianglesBefore;
            }
            _r.TrianglesAfter = _units.Values.Where(u => !u.Dropped).Sum(u => (long)u.TrianglesAfter);
            _r.Simplified = _units.Values.Count(u => u.Written && u.Simplified);
            _r.Cleaned = _units.Values.Count(u => u.Written && u.Cleaned);
            _r.DeviationMm = _units.Values.Where(u => u.Written).Select(u => u.Stray * _len * 1000).DefaultIfEmpty(0).Max();
        }

        /// <summary>
        /// The faceted B-reps that may become face sets: one shell each, read; and each representation that holds one holds
        /// nothing but such B-reps and face sets (its type then says Tessellation), and anything else that uses it takes a face set.
        /// </summary>
        HashSet<int> BrepsToConvert()
        {
            var o2 = new HashSet<int>();
            for (var k = 0; k < _s.Count; k++)
            {
                if (_s.Type[k] != "IFCFACETEDBREP") continue;
                var shells = RefsOf(k).Select(Ord).ToList();
                if (shells.Count != 1 || !_units.TryGetValue(shells[0], out var u) || !u.Editable || u.Dropped || Users(shells[0]).Count() != 1) continue;
                var ok = true;
                foreach (var user in Users(k))
                {
                    if (_s.Type[user] is "IFCBOOLEANRESULT" or "IFCBOOLEANCLIPPINGRESULT") continue;
                    if (_s.Type[user] != "IFCSHAPEREPRESENTATION") { ok = false; break; }
                    var items = ListRefs(Args(user).Args.ElementAtOrDefault(3) ?? "").Select(Ord);
                    if (items.Any(i => i < 0 || _s.Type[i] is not ("IFCFACETEDBREP" or "IFCTRIANGULATEDFACESET" or "IFCPOLYGONALFACESET"))) { ok = false; break; }
                }
                if (ok) o2.Add(k);
            }
            return o2;
        }

        /// <summary>A shape representation with no items, taken out of the product shapes that have others (then nothing uses it).</summary>
        void DropEmptyRepresentations()
        {
            for (var k = 0; k < _s.Count; k++)
            {
                if (_s.Type[k] != "IFCSHAPEREPRESENTATION") continue;
                var (_, args) = Args(k);
                if (args.Count < 4 || ListRefs(args[3]).Count > 0 || args[3].Trim() != "()") continue;
                var users = Users(k).ToList();
                if (users.Count == 0 || users.Any(u => _s.Type[u] != "IFCPRODUCTDEFINITIONSHAPE")) continue;
                if (users.Any(u => ListRefs(Args(u).Args.ElementAtOrDefault(2) ?? "").Count < 2)) continue;
                foreach (var u in users)
                {
                    var (type, pa) = Args(u);
                    Edit(u, type, new List<string>(pa) { [2] = RefList(ListRefs(pa[2]).Where(id => id != _s.Id[k])) });
                }
            }
        }

        /// <summary>
        /// What nothing references, removed: the resources the pass left unused (a shared body's old items, a shell's old faces
        /// and points) always, and with <see cref="IfcOptimizeOptions.Orphans"/> those the file had unused too; each removal may
        /// leave more unused. A styled item goes with the item it styles; a layer assignment lets go of what went, and goes when empty.
        /// </summary>
        void RemoveOrphans()
        {
            Progress("Removing what nothing uses");
            var n = _s.Count;
            var count = new int[n];
            var original = new int[n];
            for (var k = 0; k < n; k++) original[k] = Users(k).Count();
            var addedRefs = new List<int>();
            var scratch = new List<int>();
            for (var k = 0; k < n; k++)
            {
                if (_edit.TryGetValue(k, out var e)) { scratch.Clear(); IfcOptimize.RefsOf(e.Type.ToUpperInvariant(), e.Args, scratch); foreach (var r in scratch) Count(r, 1); }
                else foreach (var r in RefsOf(k)) Count(r, 1);
            }
            foreach (var (_, type, args) in _added) { scratch.Clear(); IfcOptimize.RefsOf(type, args, scratch); foreach (var r in scratch) Count(r, 1); }
            void Count(int r, int d) { if (r < 0) return; var t = Ord(r); if (t >= 0) count[t] += d; }

            var queue = new Queue<int>();
            // Never what a styled item another entity uses styles (the styled item would point at nothing).
            bool Candidate(int k) => !_removed.Contains(k) && count[k] == 0 && !_s.Rooted[k] && Removable.Contains(_s.Type[k]) && (o.Orphans || original[k] > 0 || _edit.ContainsKey(k))
                && Annotators(k).All(a => _s.Type[a] != "IFCSTYLEDITEM" || count[a] == 0);
            for (var k = 0; k < n; k++) if (Candidate(k)) queue.Enqueue(k);
            var removedOrphans = 0;
            while (true)
            {
                while (queue.Count > 0)
                {
                    var k = queue.Dequeue();
                    if (!Candidate(k)) continue;
                    _removed.Add(k);
                    removedOrphans++;
                    scratch.Clear();
                    if (_edit.TryGetValue(k, out var e)) IfcOptimize.RefsOf(e.Type.ToUpperInvariant(), e.Args, scratch); else scratch.AddRange(RefsOf(k));
                    foreach (var r in scratch)
                    {
                        if (r < 0) continue;
                        var t = Ord(r);
                        if (t < 0) continue;
                        count[t]--;
                        if (Candidate(t)) queue.Enqueue(t);
                    }
                }
                // What annotated a removed entity.
                var more = false;
                foreach (var k in _removed.ToList())
                    foreach (var a in Annotators(k))
                    {
                        if (_removed.Contains(a)) continue;
                        var (type, args) = Args(a);
                        if (_s.Type[a] == "IFCSTYLEDITEM")
                        {
                            if (count[a] > 0) continue;
                            _removed.Add(a);
                            removedOrphans++;
                            scratch.Clear(); IfcOptimize.RefsOf("IFCSTYLEDITEM", args, scratch);
                            foreach (var r in scratch) { if (r < 0) continue; var t = Ord(r); if (t < 0) continue; count[t]--; if (Candidate(t)) { queue.Enqueue(t); more = true; } }
                        }
                        else if (_s.Type[a].StartsWith("IFCPRESENTATIONLAYERASSIGNMENT", StringComparison.Ordinal) && args.Count > 2)
                        {
                            var left = ListRefs(args[2]).Where(id => { var t = Ord(id); return t >= 0 && !_removed.Contains(t); }).ToList();
                            if (left.Count > 0) Edit(a, type, new List<string>(args) { [2] = RefList(left) });
                            else if (count[a] == 0) { _removed.Add(a); removedOrphans++; }
                        }
                    }
                if (!more && queue.Count == 0) break;
            }
            // An added entity nothing uses (a map's point when its group went): there is none by construction.
            _r.Orphans = removedOrphans;
        }

        // ─── The copy ────────────────────────────────────────────────────────

        public void Write(Stream output)
        {
            Progress("Writing the copy");
            using var w = new BufferedStream(output, 1 << 20);
            w.Write(b, 0, _s.HeaderEnd);
            var lastEnd = _s.HeaderEnd;
            for (var k = 0; k < _s.Count; k++)
            {
                if ((k & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
                // What follows the entity up to the next (its line's end): kept with it, left out with it.
                var next = k + 1 < _s.Count ? _s.Start[k + 1] : _s.DataEnd;
                if (_removed.Contains(k)) { lastEnd = next; continue; }
                if (_edit.TryGetValue(k, out var e))
                {
                    w.Write(Encoding.Latin1.GetBytes($"#{_s.Id[k]}={e.Type}({string.Join(",", e.Args)});"));
                    w.Write(b, _s.End[k], next - _s.End[k]);
                }
                else w.Write(b, _s.Start[k], next - _s.Start[k]);
                lastEnd = next;
            }
            if (_added.Count > 0)
            {
                // After the last entity's line: the new entities, each on its own.
                var nl = Encoding.ASCII.GetBytes(_nl);
                var tail = Encoding.ASCII.GetString(b, Math.Max(_s.HeaderEnd, lastEnd - 2), Math.Min(2, lastEnd - _s.HeaderEnd));
                if (_s.Count > 0 && !tail.EndsWith('\n')) w.Write(nl);
                foreach (var (id, type, args) in _added)
                {
                    w.Write(Encoding.Latin1.GetBytes($"#{id}={type}({string.Join(",", args)});"));
                    w.Write(nl);
                }
            }
            w.Write(b, _s.DataEnd, b.Length - _s.DataEnd);
            w.Flush();
        }
    }

    /// <summary>A 64-bit hash over numbers and strings (xxHash64's mixing), for telling equal bodies apart quickly.</summary>
    sealed class XxHash
    {
        const ulong P1 = 11400714785074694791UL, P2 = 14029467366897019727UL, P3 = 1609587929392839161UL;
        ulong _h = P3;
        public void Add(long v) { _h ^= Round((ulong)v); _h = ((_h << 27) | (_h >> 37)) * P1 + P3; }
        public void Add(int v) => Add((long)v);
        public void Add(string s) { Add(s.Length); foreach (var c in s) Add((long)c); }
        static ulong Round(ulong v) { v *= P2; v = (v << 31) | (v >> 33); return v * P1; }
        public ulong Value { get { var h = _h; h ^= h >> 33; h *= P2; h ^= h >> 29; h *= P3; h ^= h >> 32; return h; } }
    }
}
