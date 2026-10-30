using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.Json.Nodes;

namespace Lapin.Mesh.Fragments;

/// <summary>A Transform struct: a position (double) and the x and y directions (float); z is their cross product.</summary>
public readonly record struct FragTransform(double X, double Y, double Z, float Xx, float Xy, float Xz, float Yx, float Yy, float Yz)
{
    public static readonly FragTransform Identity = new(0, 0, 0, 1, 0, 0, 0, 1, 0);
    public bool IsIdentity => X == 0 && Y == 0 && Z == 0 && Xx == 1 && Xy == 0 && Xz == 0 && Yx == 0 && Yy == 1 && Yz == 0;

    /// <summary>A point placed by the transform (the file's frame).</summary>
    public (double X, double Y, double Z) Apply(double x, double y, double z)
    {
        double zx = Xy * Yz - Xz * Yy, zy = Xz * Yx - Xx * Yz, zz = Xx * Yy - Xy * Yx;
        return (X + Xx * x + Yx * y + zx * z, Y + Xy * x + Yy * y + zy * z, Z + Xz * x + Yz * y + zz * z);
    }
}

/// <summary>A shell: points (x y z … float, relative to its element's transform), a profile per polygon (indices), holes (indices, the profile they cut), the face of each profile.</summary>
public sealed record FragShell(float[] Points, int[][] Profiles, (int[] Indices, int Profile)[] Holes, ushort[] FaceIds, bool Big);

public sealed record FragRepresentation(uint Id, float[] Min, float[] Max, sbyte Class);

/// <summary>A material: its colour (0–255), whether both faces are drawn, the stroke.</summary>
public readonly record struct FragMaterial(byte R, byte G, byte B, byte A, sbyte RenderedFaces, sbyte Stroke);

/// <summary>A sample: which item, with which material, which representation, placed by which local transform (indexes into their vectors).</summary>
public readonly record struct FragSample(uint Item, uint Material, uint Representation, uint LocalTransform);

public sealed record FragMeshes(FragTransform? Coordinates, uint[] MeshesItems, FragSample[] Samples, FragRepresentation[] Representations, FragMaterial[] Materials,
    FragShell[] Shells, int CircleExtrusions, FragTransform[] LocalTransforms, FragTransform[] GlobalTransforms,
    uint[]? MaterialIds, uint[]? RepresentationIds, uint[]? SampleIds, uint[]? LocalTransformIds, uint[]? GlobalTransformIds);

/// <summary>A node of the spatial tree: an entity (its local id) or a category (its name), with its children.</summary>
public sealed record FragSpatial(uint? LocalId, string? Category, FragSpatial[] Children);

/// <summary>A Fragments model as the file holds it, decoded to plain data (docs/fragments.md).</summary>
public sealed record FragModel(string? Metadata, string[] Guids, uint[] GuidsItems, uint MaxLocalId, uint[] LocalIds, string[] Categories, FragMeshes Meshes,
    string[][]? Attributes, string[][]? Relations, int[]? RelationsItems, string? Guid, FragSpatial? SpatialStructure, string[]? UniqueAttributes, string[]? RelationNames, int Indexes)
{
    public JsonObject? MetadataJson
    {
        get { try { return Metadata == null ? null : JsonNode.Parse(Metadata) as JsonObject; } catch (System.Text.Json.JsonException) { return null; } }
    }

    /// <summary>The attributes of an item (by its index in <see cref="LocalIds"/>) as name → value: <c>["Name","Wall","IFCLABEL"]</c> gives Name = "Wall".</summary>
    public Dictionary<string, JsonNode?> AttributesOf(int index)
    {
        var o = new Dictionary<string, JsonNode?>();
        if (Attributes == null || index >= Attributes.Length) return o;
        foreach (var s in Attributes[index])
        {
            try
            {
                if (JsonNode.Parse(s) is JsonArray { Count: >= 2 } a && (string?)a[0] is { } name) o[name] = a[1]?.DeepClone();
            }
            catch (System.Text.Json.JsonException) { }
        }
        return o;
    }

    /// <summary>The relations of an item (by its local id) as name → the local ids it points at.</summary>
    public Dictionary<string, List<uint>> RelationsOf(uint localId)
    {
        var o = new Dictionary<string, List<uint>>();
        if (Relations == null || RelationsItems == null) return o;
        var k = Array.IndexOf(RelationsItems, (int)localId);
        if (k < 0 || k >= Relations.Length) return o;
        foreach (var s in Relations[k])
        {
            try
            {
                if (JsonNode.Parse(s) is not JsonArray { Count: >= 1 } a || (string?)a[0] is not { } name) continue;
                var ids = new List<uint>();
                for (var i = 1; i < a.Count; i++) if (a[i] is JsonValue v && v.TryGetValue<double>(out var d) && d >= 0) ids.Add((uint)d);
                o[name] = ids;
            }
            catch (System.Text.Json.JsonException) { }
        }
        return o;
    }
}

/// <summary>Reads a <c>.frag</c> (zlib or raw) into a <see cref="FragModel"/>: the mirror of <see cref="FragWriter"/>, sharing <see cref="FragSchema"/>.</summary>
public static class FragReader
{
    /// <summary>The FlatBuffer itself: a file written compressed starts with zlib's 0x78.</summary>
    public static byte[] Decompress(byte[] data)
    {
        if (data.Length > 1 && data[0] == 0x78)
        {
            try
            {
                using var z = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);
                using var ms = new MemoryStream();
                z.CopyTo(ms);
                return ms.ToArray();
            }
            catch (InvalidDataException) { }
        }
        return data;
    }

    public static FragModel Read(byte[] data)
    {
        var buf = Decompress(data);
        if (buf.Length < 8) throw new FormatException("Not a Fragments model: the file is too short.");
        var root = FlatTable.Root(buf);
        var meshes = root.Table(FragSchema.Model.Meshes) ?? throw new FormatException("Not a Fragments model: it has no meshes table.");
        return new FragModel(
            root.String(FragSchema.Model.Metadata),
            root.Strings(FragSchema.Model.Guids) ?? [],
            root.Uints(FragSchema.Model.GuidsItems) ?? [],
            root.Uint(FragSchema.Model.MaxLocalId),
            root.Uints(FragSchema.Model.LocalIds) ?? [],
            root.Strings(FragSchema.Model.Categories) ?? [],
            Meshes(meshes),
            root.Tables(FragSchema.Model.Attributes)?.Select(a => a.Strings(FragSchema.Attribute.Data) ?? []).ToArray(),
            root.Tables(FragSchema.Model.Relations)?.Select(r => r.Strings(FragSchema.Relation.Data) ?? []).ToArray(),
            root.Ints(FragSchema.Model.RelationsItems),
            root.String(FragSchema.Model.Guid),
            root.Table(FragSchema.Model.SpatialStructure) is { } sp ? Spatial(sp) : null,
            root.Strings(FragSchema.Model.UniqueAttributes),
            root.Strings(FragSchema.Model.RelationNames),
            root.Tables(FragSchema.Model.Indexes)?.Length ?? 0);
    }

    static FragTransform Transform(byte[] buf, int at)
    {
        var s = buf.AsSpan(at);
        return new FragTransform(
            BinaryPrimitives.ReadDoubleLittleEndian(s), BinaryPrimitives.ReadDoubleLittleEndian(s[8..]), BinaryPrimitives.ReadDoubleLittleEndian(s[16..]),
            BinaryPrimitives.ReadSingleLittleEndian(s[24..]), BinaryPrimitives.ReadSingleLittleEndian(s[28..]), BinaryPrimitives.ReadSingleLittleEndian(s[32..]),
            BinaryPrimitives.ReadSingleLittleEndian(s[36..]), BinaryPrimitives.ReadSingleLittleEndian(s[40..]), BinaryPrimitives.ReadSingleLittleEndian(s[44..]));
    }

    static FragTransform[] Transforms(FlatTable t, int slot)
    {
        if (t.Vector(slot) is not var (start, n)) return [];
        var o = new FragTransform[n];
        for (var i = 0; i < n; i++) o[i] = Transform(t.Buf, start + i * FragSchema.SizeTransform);
        return o;
    }

    static FragMeshes Meshes(FlatTable t)
    {
        var buf = t.Buf;
        var coordinatesAt = t.Struct(FragSchema.Meshes.Coordinates);
        FragSample[] samples = [];
        if (t.Vector(FragSchema.Meshes.Samples) is var (ss, sn))
        {
            samples = new FragSample[sn];
            for (var i = 0; i < sn; i++)
            {
                var s = buf.AsSpan(ss + i * FragSchema.SizeSample);
                samples[i] = new(BinaryPrimitives.ReadUInt32LittleEndian(s), BinaryPrimitives.ReadUInt32LittleEndian(s[4..]), BinaryPrimitives.ReadUInt32LittleEndian(s[8..]), BinaryPrimitives.ReadUInt32LittleEndian(s[12..]));
            }
        }
        FragRepresentation[] reps = [];
        if (t.Vector(FragSchema.Meshes.Representations) is var (rs, rn))
        {
            reps = new FragRepresentation[rn];
            for (var i = 0; i < rn; i++)
            {
                var at = rs + i * FragSchema.SizeRepresentation;
                float F(int o) => BinaryPrimitives.ReadSingleLittleEndian(buf.AsSpan(at + o));
                reps[i] = new(BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(at)), [F(4), F(8), F(12)], [F(16), F(20), F(24)], unchecked((sbyte)buf[at + 28]));
            }
        }
        FragMaterial[] mats = [];
        if (t.Vector(FragSchema.Meshes.Materials) is var (ms, mn))
        {
            mats = new FragMaterial[mn];
            for (var i = 0; i < mn; i++)
            {
                var s = buf.AsSpan(ms + i * FragSchema.SizeMaterial);
                mats[i] = new(s[0], s[1], s[2], s[3], unchecked((sbyte)s[4]), unchecked((sbyte)s[5]));
            }
        }
        return new FragMeshes(
            coordinatesAt >= 0 ? Transform(buf, coordinatesAt) : null,
            t.Uints(FragSchema.Meshes.MeshesItems) ?? [],
            samples, reps, mats,
            t.Tables(FragSchema.Meshes.Shells)?.Select(Shell).ToArray() ?? [],
            t.Tables(FragSchema.Meshes.CircleExtrusions)?.Length ?? 0,
            Transforms(t, FragSchema.Meshes.LocalTransforms),
            Transforms(t, FragSchema.Meshes.GlobalTransforms),
            t.Uints(FragSchema.Meshes.MaterialIds), t.Uints(FragSchema.Meshes.RepresentationIds), t.Uints(FragSchema.Meshes.SampleIds),
            t.Uints(FragSchema.Meshes.LocalTransformIds), t.Uints(FragSchema.Meshes.GlobalTransformIds));
    }

    static FragShell Shell(FlatTable t)
    {
        var points = t.Floats(FragSchema.Shell.Points, 3) ?? [];
        var big = t.Sbyte(FragSchema.Shell.Type) == FragSchema.ShellTypeBig;
        int[][] profiles;
        (int[], int)[] holes;
        if (big)
        {
            profiles = (t.Tables(FragSchema.Shell.BigProfiles) ?? []).Select(p => (p.Uints(FragSchema.ShellProfile.Indices) ?? []).Select(x => (int)x).ToArray()).ToArray();
            holes = (t.Tables(FragSchema.Shell.BigHoles) ?? []).Select(h => ((h.Uints(FragSchema.ShellHole.Indices) ?? []).Select(x => (int)x).ToArray(), (int)h.Ushort(FragSchema.ShellHole.ProfileId))).ToArray();
        }
        else
        {
            profiles = (t.Tables(FragSchema.Shell.Profiles) ?? []).Select(p => (p.Ushorts(FragSchema.ShellProfile.Indices) ?? []).Select(x => (int)x).ToArray()).ToArray();
            holes = (t.Tables(FragSchema.Shell.Holes) ?? []).Select(h => ((h.Ushorts(FragSchema.ShellHole.Indices) ?? []).Select(x => (int)x).ToArray(), (int)h.Ushort(FragSchema.ShellHole.ProfileId))).ToArray();
        }
        return new FragShell(points, profiles, holes, t.Ushorts(FragSchema.Shell.ProfilesFaceIds) ?? [], big);
    }

    static FragSpatial Spatial(FlatTable t) =>
        new(t.Has(FragSchema.Spatial.LocalId) ? t.Uint(FragSchema.Spatial.LocalId) : null, t.String(FragSchema.Spatial.Category),
            (t.Tables(FragSchema.Spatial.Children) ?? []).Select(Spatial).ToArray());
}
