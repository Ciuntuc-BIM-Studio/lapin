using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.PointClouds;

/// <summary>
/// A cloud indexed into an octree of tiles (Potree's scheme, written by Lapin): <c>meta.json</c> says what the cloud is and
/// lists the nodes, <c>points.bin</c> holds each node's points one after another. A node's points are a sample of its cube
/// on a grid of <see cref="Resolution"/>³ cells; the points no ancestor took are in its descendants, so a node and its
/// ancestors together never repeat a point. A node is named by its path from the root (<c>r</c>, then a digit 0–7 a level:
/// 4·x + 2·y + z, the upper half of each axis 1).
/// <para>Per node, in <c>points.bin</c>: its coordinates as three int32 a point (steps of <see cref="Step"/> from
/// <see cref="Min"/>), then red, green, blue a byte each, then intensity as uint16, then the class as a byte: 18 bytes a point.</para>
/// </summary>
public sealed class PointStore : IDisposable
{
    public const int Version = 1;
    public const int Resolution = 128;
    public const int BytesPerPoint = 18;

    public sealed record Node(string Name, long Offset, int Count);

    public string Directory { get; }
    public string Format { get; init; } = "";
    public long Points { get; init; }
    /// <summary>The octree's cube: its least corner and its edge, in the file's units.</summary>
    public double[] Min { get; init; } = [0, 0, 0];
    public double Size { get; init; }
    /// <summary>The coordinates' step per axis, in the file's units.</summary>
    public double[] Step { get; init; } = [1e-3, 1e-3, 1e-3];
    /// <summary>The points' own box (min x, y, z, max x, y, z), in the file's units.</summary>
    public double[] Bounds { get; init; } = new double[6];
    public bool HasColor { get; init; }
    public bool HasIntensity { get; init; }
    public bool HasClassification { get; init; }
    public ushort IntensityMin { get; init; }
    public ushort IntensityMax { get; init; }
    /// <summary>How many points of each ASPRS class (0–255).</summary>
    public long[] Classes { get; init; } = new long[256];
    public string? Crs { get; init; }
    public string? CrsText { get; init; }
    public string? Units { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
    public IReadOnlyList<Node> Nodes { get; init; } = [];
    /// <summary>What the index was made from: the source's length and its key (the caller's: a path and its time, or a hash).</summary>
    public long SourceLength { get; init; }
    public string SourceKey { get; init; } = "";

    readonly Dictionary<string, Node> _byName = [];
    SafeHandleHolder? _data;

    internal PointStore(string dir) { Directory = dir; }

    public Node? Get(string name) => _byName.TryGetValue(name, out var n) ? n : null;

    /// <summary>A node's cube (min x, y, z, edge) in the file's units, from its name.</summary>
    public (double X, double Y, double Z, double Edge) CubeOf(string name)
    {
        double x = Min[0], y = Min[1], z = Min[2], e = Size;
        for (var i = 1; i < name.Length; i++)
        {
            e /= 2;
            var k = name[i] - '0';
            if ((k & 4) != 0) x += e;
            if ((k & 2) != 0) y += e;
            if ((k & 1) != 0) z += e;
        }
        return (x, y, z, e);
    }

    /// <summary>The spacing a node's sample keeps (its cube's edge over the grid), in the file's units.</summary>
    public double SpacingOf(string name) => CubeOf(name).Edge / Resolution;

    /// <summary>A node's points as stored.</summary>
    public NodePoints Read(string name)
    {
        var node = Get(name) ?? throw new PointCloudException($"The cloud has no tile {name}.");
        var bytes = new byte[(long)node.Count * BytesPerPoint];
        if (node.Count > 0)
        {
            _data ??= new SafeHandleHolder(Path.Combine(Directory, "points.bin"));
            RandomAccess.Read(_data.Handle, bytes, node.Offset);
        }
        return new NodePoints(this, node.Count, bytes);
    }

    /// <summary>A node's points, as their bytes: read through the accessors.</summary>
    public sealed class NodePoints(PointStore store, int count, byte[] bytes)
    {
        public int Count { get; } = count;
        public byte[] Bytes { get; } = bytes;
        public double X(int i) => store.Min[0] + BinaryPrimitives.ReadInt32LittleEndian(Bytes.AsSpan(12 * i)) * store.Step[0];
        public double Y(int i) => store.Min[1] + BinaryPrimitives.ReadInt32LittleEndian(Bytes.AsSpan(12 * i + 4)) * store.Step[1];
        public double Z(int i) => store.Min[2] + BinaryPrimitives.ReadInt32LittleEndian(Bytes.AsSpan(12 * i + 8)) * store.Step[2];
        public byte R(int i) => Bytes[12 * Count + 3 * i];
        public byte G(int i) => Bytes[12 * Count + 3 * i + 1];
        public byte B(int i) => Bytes[12 * Count + 3 * i + 2];
        public ushort Intensity(int i) => BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan(15 * Count + 2 * i));
        public byte Class(int i) => Bytes[17 * Count + i];
    }

    sealed class SafeHandleHolder(string path) : IDisposable
    {
        public readonly Microsoft.Win32.SafeHandles.SafeFileHandle Handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        public void Dispose() => Handle.Dispose();
    }

    public void Dispose() { _data?.Dispose(); _data = null; }

    /// <summary>The index in <paramref name="dir"/>, or null when there is none (or an older one, or one cut short).</summary>
    public static PointStore? Open(string dir)
    {
        var meta = Path.Combine(dir, "meta.json");
        if (!File.Exists(meta) || !File.Exists(Path.Combine(dir, "points.bin"))) return null;
        try
        {
            var o = JsonNode.Parse(File.ReadAllText(meta)) as JsonObject;
            if (o == null || (int?)o["version"] != Version) return null;
            static double[] Nums(JsonNode? n) => n is JsonArray a ? a.Select(x => (double)x!).ToArray() : [];
            var nodes = (o["nodes"] as JsonArray ?? []).Select(n => new Node((string)n![0]!, (long)n[1]!, (int)n[2]!)).ToList();
            var classes = new long[256];
            if (o["classes"] is JsonObject co) foreach (var (k, v) in co) if (int.TryParse(k, out var c) && c is >= 0 and < 256) classes[c] = (long)v!;
            var s = new PointStore(dir)
            {
                Format = (string?)o["format"] ?? "", Points = (long?)o["points"] ?? 0, Min = Nums(o["min"]), Size = (double?)o["size"] ?? 1,
                Step = Nums(o["step"]), Bounds = Nums(o["bounds"]), HasColor = (bool?)o["color"] ?? false, HasIntensity = (bool?)o["intensity"] ?? false,
                HasClassification = (bool?)o["classification"] ?? false, IntensityMin = (ushort)((int?)o["intensityMin"] ?? 0), IntensityMax = (ushort)((int?)o["intensityMax"] ?? 65535),
                Classes = classes, Crs = (string?)o["crs"], CrsText = (string?)o["crsText"], Units = (string?)o["units"],
                Notes = (o["notes"] as JsonArray ?? []).Select(x => (string)x!).ToList(), Nodes = nodes,
                SourceLength = (long?)o["sourceLength"] ?? 0, SourceKey = (string?)o["sourceKey"] ?? "",
            };
            foreach (var n in nodes) s._byName[n.Name] = n;
            return s;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or NullReferenceException or IOException)
        {
            return null;
        }
    }

    internal static void WriteMeta(string dir, PointStore s)
    {
        var classes = new JsonObject();
        for (var c = 0; c < 256; c++) if (s.Classes[c] > 0) classes[c.ToString()] = s.Classes[c];
        var o = new JsonObject
        {
            ["version"] = Version, ["format"] = s.Format, ["points"] = s.Points, ["min"] = Arr(s.Min), ["size"] = s.Size, ["step"] = Arr(s.Step),
            ["bounds"] = Arr(s.Bounds), ["color"] = s.HasColor, ["intensity"] = s.HasIntensity, ["classification"] = s.HasClassification,
            ["intensityMin"] = (int)s.IntensityMin, ["intensityMax"] = (int)s.IntensityMax, ["classes"] = classes,
            ["crs"] = s.Crs, ["crsText"] = s.CrsText, ["units"] = s.Units, ["notes"] = new JsonArray(s.Notes.Select(n => (JsonNode)n).ToArray()),
            ["sourceLength"] = s.SourceLength, ["sourceKey"] = s.SourceKey,
            ["nodes"] = new JsonArray(s.Nodes.Select(n => (JsonNode)new JsonArray(n.Name, n.Offset, n.Count)).ToArray()),
        };
        var tmp = Path.Combine(dir, "meta.json.tmp");
        File.WriteAllText(tmp, o.ToJsonString());
        File.Move(tmp, Path.Combine(dir, "meta.json"), overwrite: true);
        static JsonArray Arr(double[] a) => new(a.Select(x => (JsonNode)x).ToArray());
    }

}
