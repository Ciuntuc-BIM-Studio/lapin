using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Mesh.Bos;

/// <summary>A parameter's value kind (BIM Open Schema's ParameterType): the value is the number itself or an index into the table it names.</summary>
public enum BosParameterType { Int = 0, Number = 1, Entity = 2, String = 3, Point = 4 }

/// <summary>How two entities relate, A to B (BIM Open Schema's RelationType): an element ContainedIn its storey, an opening Voids its wall, a window Fills its opening.</summary>
public enum BosRelation { PartOf = 0, MemberOf = 1, ContainedIn = 2, HostedBy = 3, ChildOf = 4, HasLayer = 5, HasMaterial = 6, ConnectsTo = 7, HasConnector = 8, BoundedBy = 9, TraverseTo = 10, Voids = 11, Fills = 12, Covers = 13, Serves = 14 }

/// <summary>
/// A BIM Open Schema file's tables (ara3d/bim-open-schema, MIT; docs/bos.md): a <c>.bos</c> is a zip of Parquet files, one
/// per table, named after it. Every index is into the table it names; −1 is absent. Geometry: meshes of integer vertices
/// (tenths of a millimetre, Z up, metres once divided) with local indices, placed by instances (an entity, a mesh, a
/// material and a transform: translation, quaternion, scale).
/// </summary>
public sealed class BosData
{
    public const float VertexScale = 10_000f;
    public const string Version = "0.3";

    public List<string> Strings { get; } = [];
    public List<float> Numbers { get; } = [];
    public List<(int Title, int Path)> Documents { get; } = [];
    public List<(float X, float Y, float Z)> Points { get; } = [];
    public List<(int Name, int Units, int Group, BosParameterType Type)> Descriptors { get; } = [];
    public List<(int Entity, int Descriptor, int Value)> Parameters { get; } = [];
    public List<(long LocalId, int GlobalId, int Document, int Name, int Category, int Type)> Entities { get; } = [];
    public List<(int A, int B, BosRelation Type)> Relations { get; } = [];
    public List<(int Type, int Document, int Entity, int Message)> Diagnostics { get; } = [];

    public List<(int Entity, int Material, int Mesh, int Transform, byte Flags)> Instances { get; } = [];
    public List<int> VertexX { get; } = [];
    public List<int> VertexY { get; } = [];
    public List<int> VertexZ { get; } = [];
    public List<int> Indices { get; } = [];
    public List<(int IndexOffset, int VertexOffset)> Meshes { get; } = [];
    public List<(byte R, byte G, byte B, byte A, byte Roughness, byte Metallic)> Materials { get; } = [];
    public List<(float TX, float TY, float TZ, float QX, float QY, float QZ, float QW, float SX, float SY, float SZ)> Transforms { get; } = [];

    /// <summary>The generator the file names (its manifest), when it has one.</summary>
    public string? Generator { get; set; }

    public string? Text(int i) => i >= 0 && i < Strings.Count ? Strings[i] : null;

    /// <summary>A mesh's vertex and index ranges: from its offsets to the next mesh's (the last to the end).</summary>
    public (int V0, int V1, int I0, int I1) MeshRange(int m)
    {
        var (i0, v0) = Meshes[m];
        var (i1, v1) = m + 1 < Meshes.Count ? Meshes[m + 1] : (Indices.Count, VertexX.Count);
        return (v0, v1, i0, i1);
    }

    // ------------------------------------------------------------ reading

    public static BosData Read(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var tables = new Dictionary<string, List<ParquetColumn>>(StringComparer.OrdinalIgnoreCase);
        var d = new BosData();
        foreach (var e in zip.Entries)
        {
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            var name = Path.GetFileNameWithoutExtension(e.Name);
            if (e.Name.EndsWith(".parquet", StringComparison.OrdinalIgnoreCase))
            {
                try { tables[name] = Parquet.Read(ms.ToArray()); }
                catch (InvalidDataException ex) { throw new InvalidDataException($"{e.Name}: {ex.Message}"); }
            }
            else if (e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                try { d.Generator = JsonNode.Parse(ms.ToArray()) is JsonObject m ? (string?)m["GeneratorApplication"] ?? (string?)m["generatorApplication"] : null; }
                catch (JsonException) { }
            }
        }
        if (!tables.ContainsKey("Entities")) throw new InvalidDataException("it has no Entities table");

        // A table's column by its name, else by its place (the reference reader goes by place).
        ParquetColumn Col(string table, string name, int at)
        {
            var cols = tables[table];
            return cols.FirstOrDefault(c => c.Name == name) ?? (at < cols.Count ? cols[at] : throw new InvalidDataException($"{table} has no column {name}"));
        }
        bool Has(string t) => tables.ContainsKey(t) && tables[t].Count > 0;

        if (Has("Strings")) d.Strings.AddRange(Col("Strings", "Strings", 0).Strings?.Select(s => s ?? "") ?? []);
        if (Has("Numbers")) d.Numbers.AddRange(Col("Numbers", "Numbers", 0).AsFloats());
        if (Has("Documents")) Zip2(Col("Documents", "Title", 0).AsInts(), Col("Documents", "Path", 1).AsInts(), d.Documents);
        if (Has("Points"))
        {
            var (x, y, z) = (Col("Points", "X", 0).AsFloats(), Col("Points", "Y", 1).AsFloats(), Col("Points", "Z", 2).AsFloats());
            for (var i = 0; i < x.Length; i++) d.Points.Add((x[i], y[i], z[i]));
        }
        if (Has("Descriptors"))
        {
            var (n, u, g, t) = (Col("Descriptors", "Name", 0).AsInts(), Col("Descriptors", "Units", 1).AsInts(), Col("Descriptors", "Group", 2).AsInts(), Col("Descriptors", "Type", 3).AsInts());
            for (var i = 0; i < n.Length; i++) d.Descriptors.Add((n[i], u[i], g[i], (BosParameterType)t[i]));
        }
        if (Has("Parameters")) Param3("Parameters");
        // Older files: a parameter table per value kind; single (float) values inline, interned here into Numbers.
        foreach (var t in new[] { "IntegerParameters", "StringParameters", "EntityParameters", "PointParameters" }) if (Has(t)) Param3(t);
        if (Has("SingleParameters"))
        {
            var (e, de, v) = (Col("SingleParameters", "Entity", 0).AsInts(), Col("SingleParameters", "Descriptor", 1).AsInts(), Col("SingleParameters", "Value", 2).AsFloats());
            for (var i = 0; i < e.Length; i++) { d.Parameters.Add((e[i], de[i], d.Numbers.Count)); d.Numbers.Add(v[i]); }
        }
        {
            var (l, gid, doc, n, c, t) = (Col("Entities", "LocalId", 0).AsLongs(), Col("Entities", "GlobalId", 1).AsInts(), Col("Entities", "Document", 2).AsInts(),
                Col("Entities", "Name", 3).AsInts(), Col("Entities", "Category", 4).AsInts(), Col("Entities", "Type", 5).AsInts());
            for (var i = 0; i < l.Length; i++) d.Entities.Add((l[i], gid[i], doc[i], n[i], c[i], t[i]));
        }
        if (Has("Relations"))
        {
            var (a, b, t) = (Col("Relations", "EntityA", 0).AsInts(), Col("Relations", "EntityB", 1).AsInts(), Col("Relations", "RelationType", 2).AsInts());
            for (var i = 0; i < a.Length; i++) d.Relations.Add((a[i], b[i], (BosRelation)t[i]));
        }
        if (Has("Instances"))
        {
            var (e, m, me, t) = (Col("Instances", "InstanceEntityIndex", 0).AsInts(), Col("Instances", "InstanceMaterialIndex", 1).AsInts(),
                Col("Instances", "InstanceMeshIndex", 2).AsInts(), Col("Instances", "InstanceTransformIndex", 3).AsInts());
            // Older files have no flags: nothing hidden.
            var f = tables["Instances"].FirstOrDefault(c => c.Name == "InstanceFlags")?.AsInts();
            for (var i = 0; i < e.Length; i++) d.Instances.Add((e[i], m[i], me[i], t[i], (byte)(f != null && i < f.Length ? f[i] : 0)));
        }
        if (Has("VertexBuffer"))
        {
            d.VertexX.AddRange(Col("VertexBuffer", "VertexX", 0).AsInts());
            d.VertexY.AddRange(Col("VertexBuffer", "VertexY", 1).AsInts());
            d.VertexZ.AddRange(Col("VertexBuffer", "VertexZ", 2).AsInts());
        }
        if (Has("IndexBuffer")) d.Indices.AddRange(Col("IndexBuffer", "IndexBuffer", 0).AsInts());
        if (Has("Meshes")) Zip2(Col("Meshes", "MeshIndexOffset", 0).AsInts(), Col("Meshes", "MeshVertexOffset", 1).AsInts(), d.Meshes);
        if (Has("Materials"))
        {
            int[] C(string n, int at) => Col("Materials", n, at).AsInts();
            var (r, g, b, a, me, ro) = (C("MaterialRed", 0), C("MaterialGreen", 1), C("MaterialBlue", 2), C("MaterialAlpha", 3), C("MaterialMetallic", 4), C("MaterialRoughness", 5));
            for (var i = 0; i < r.Length; i++) d.Materials.Add(((byte)r[i], (byte)g[i], (byte)b[i], (byte)a[i], (byte)ro[i], (byte)me[i]));
        }
        if (Has("Transforms"))
        {
            float[] C(string n, int at) => Col("Transforms", n, at).AsFloats();
            var c = new[] { C("TransformTX", 0), C("TransformTY", 1), C("TransformTZ", 2), C("TransformQX", 3), C("TransformQY", 4), C("TransformQZ", 5), C("TransformQW", 6), C("TransformSX", 7), C("TransformSY", 8), C("TransformSZ", 9) };
            for (var i = 0; i < c[0].Length; i++) d.Transforms.Add((c[0][i], c[1][i], c[2][i], c[3][i], c[4][i], c[5][i], c[6][i], c[7][i], c[8][i], c[9][i]));
        }
        return d;

        void Param3(string table)
        {
            var (e, de, v) = (Col(table, "Entity", 0).AsInts(), Col(table, "Descriptor", 1).AsInts(), Col(table, "Value", 2).AsInts());
            for (var i = 0; i < e.Length; i++) d.Parameters.Add((e[i], de[i], v[i]));
        }
        static void Zip2(int[] a, int[] b, List<(int, int)> o) { for (var i = 0; i < a.Length && i < b.Length; i++) o.Add((a[i], b[i])); }
    }

    // ------------------------------------------------------------ writing

    /// <summary>The tables as a <c>.bos</c>: a Parquet file per table (Brotli pages, the zip itself stored), and a manifest.</summary>
    public byte[] Write(string generatorVersion)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Table(string name, params ParquetColumn[] cols)
            {
                var entry = zip.CreateEntry($"{name}.parquet", CompressionLevel.NoCompression);
                using var s = entry.Open();
                s.Write(Parquet.Write(cols));
            }
            static ParquetColumn I(string n, IEnumerable<int> v) => new(n) { Ints = v.ToArray() };
            static ParquetColumn B(string n, IEnumerable<byte> v) => new(n) { Ints = v.Select(x => (int)x).ToArray(), Byte = true };
            static ParquetColumn F(string n, IEnumerable<float> v) => new(n) { Floats = v.ToArray() };

            Table("Diagnostics", I("Type", Diagnostics.Select(x => x.Type)), I("Document", Diagnostics.Select(x => x.Document)), I("Entity", Diagnostics.Select(x => x.Entity)), I("Message", Diagnostics.Select(x => x.Message)));
            Table("Points", F("X", Points.Select(p => p.X)), F("Y", Points.Select(p => p.Y)), F("Z", Points.Select(p => p.Z)));
            Table("Strings", new ParquetColumn("Strings") { Strings = Strings.Select(s => (string?)s).ToArray() });
            Table("Descriptors", I("Name", Descriptors.Select(x => x.Name)), I("Units", Descriptors.Select(x => x.Units)), I("Group", Descriptors.Select(x => x.Group)), I("Type", Descriptors.Select(x => (int)x.Type)));
            Table("Documents", I("Title", Documents.Select(x => x.Title)), I("Path", Documents.Select(x => x.Path)));
            Table("Entities", new ParquetColumn("LocalId") { Longs = Entities.Select(x => x.LocalId).ToArray() }, I("GlobalId", Entities.Select(x => x.GlobalId)), I("Document", Entities.Select(x => x.Document)),
                I("Name", Entities.Select(x => x.Name)), I("Category", Entities.Select(x => x.Category)), I("Type", Entities.Select(x => x.Type)));
            Table("Relations", I("EntityA", Relations.Select(x => x.A)), I("EntityB", Relations.Select(x => x.B)), I("RelationType", Relations.Select(x => (int)x.Type)));
            Table("Parameters", I("Entity", Parameters.Select(x => x.Entity)), I("Descriptor", Parameters.Select(x => x.Descriptor)), I("Value", Parameters.Select(x => x.Value)));
            Table("Numbers", F("Numbers", Numbers));
            Table("Materials", B("MaterialRed", Materials.Select(m => m.R)), B("MaterialGreen", Materials.Select(m => m.G)), B("MaterialBlue", Materials.Select(m => m.B)),
                B("MaterialAlpha", Materials.Select(m => m.A)), B("MaterialMetallic", Materials.Select(m => m.Metallic)), B("MaterialRoughness", Materials.Select(m => m.Roughness)));
            Table("VertexBuffer", I("VertexX", VertexX), I("VertexY", VertexY), I("VertexZ", VertexZ));
            Table("IndexBuffer", I("IndexBuffer", Indices));
            Table("Instances", I("InstanceEntityIndex", Instances.Select(x => x.Entity)), I("InstanceMaterialIndex", Instances.Select(x => x.Material)),
                I("InstanceMeshIndex", Instances.Select(x => x.Mesh)), I("InstanceTransformIndex", Instances.Select(x => x.Transform)), B("InstanceFlags", Instances.Select(x => x.Flags)));
            Table("Meshes", I("MeshIndexOffset", Meshes.Select(m => m.IndexOffset)), I("MeshVertexOffset", Meshes.Select(m => m.VertexOffset)));
            Table("Transforms", F("TransformTX", Transforms.Select(t => t.TX)), F("TransformTY", Transforms.Select(t => t.TY)), F("TransformTZ", Transforms.Select(t => t.TZ)),
                F("TransformQX", Transforms.Select(t => t.QX)), F("TransformQY", Transforms.Select(t => t.QY)), F("TransformQZ", Transforms.Select(t => t.QZ)), F("TransformQW", Transforms.Select(t => t.QW)),
                F("TransformSX", Transforms.Select(t => t.SX)), F("TransformSY", Transforms.Select(t => t.SY)), F("TransformSZ", Transforms.Select(t => t.SZ)));
            var manifest = zip.CreateEntry("Manifest.json", CompressionLevel.Optimal);
            using (var s = manifest.Open())
                s.Write(System.Text.Encoding.UTF8.GetBytes(new JsonObject { ["BimOpenSchemaVersion"] = Version, ["GeneratorApplication"] = "Lapin", ["GeneratorVersion"] = generatorVersion }.ToJsonString()));
        }
        return ms.ToArray();
    }

    // ------------------------------------------------------------ building

    readonly Dictionary<string, int> stringIndex = [];
    readonly Dictionary<(int, int, int, BosParameterType), int> descriptorIndex = [];

    /// <summary>A string's index, interned (null: −1, absent).</summary>
    public int Intern(string? s)
    {
        if (s == null) return -1;
        if (stringIndex.TryGetValue(s, out var i)) return i;
        stringIndex[s] = Strings.Count;
        Strings.Add(s);
        return Strings.Count - 1;
    }

    public int Descriptor(string name, string? group, BosParameterType type, string? units = null)
    {
        var key = (Intern(name), Intern(units), Intern(group), type);
        if (descriptorIndex.TryGetValue(key, out var i)) return i;
        descriptorIndex[key] = Descriptors.Count;
        Descriptors.Add(key);
        return Descriptors.Count - 1;
    }

    public int Entity(string? name, int category = -1, string? globalId = null, int type = -1, int document = 0)
    {
        Entities.Add((-1, Intern(globalId), document, Intern(name), category, type));
        return Entities.Count - 1;
    }
}
