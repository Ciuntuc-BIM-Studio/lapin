using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Mesh;
using Lapin.Mesh.Bos;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// BIM Open Schema (docs/bos.md): Lapin's own Parquet reader on files pyarrow wrote (Snappy and dictionaries, GZIP,
/// DELTA_BINARY_PACKED and pages v2), its writer read back, Snappy by hand; a project exported as a <c>.bos</c> the way
/// Ara 3D's converter writes one, and imported back where it was; with LAPIN_BOS_SAMPLE=&lt;file.bos&gt;, a real file.
/// </summary>
public class BosTests
{
    // pyarrow 21: seven rows of int32, int64, float, double, bool and an optional string; Snappy, dictionaries, pages v1.
    const string SnappyDictionary =
        "UEFSMRUEFSgVLEwVChUAEgAAFEwFAAAA/f///wcAAABAQg8A/v///xUAFRYVGiwVDhUQFQYVBhwYBEBCDwAYBP3///8WACgEQEIPABgE/f///wAAAAsoAgAA" +
        "AA4BAwOIpBEVBBVwFVBMFQ4VABIAADgEAQAJAQACCQcEAAMNCAAEDQgABQ0IPAYAAAAAAAAAAIY7oQEAAAAVABUWFRosFQ4VEBUGFQYcGAgAhjuhAQAAABgI" +
        "AQAAAAAAAAAWACgIAIY7oQEAAAAYCAEAAAAAAAAAAAAACygCAAAADgEDA4jGGhUEFTgVPEwVDhUAEgAAHGwAAAA/AACgvwAAQEAAAAAAAAAgQAAAHEEAAAC+" +
        "FQAVFhUaLBUOFRAVBhUGHBgEAAAcQRgEAACgvxYAKAQAABxBGAQAAKC/AAAACygCAAAADgEDA4jGGhUEFXAVVEwVDhUAEgAAOASamQEBBLk/CQgIyT8zBQEA" +
        "0w0QCNk/AAUBAOANGCTjP2ZmZmZmZuY/FQAVFhUaLBUOFRAVBhUGHBgIZmZmZmZm5j8YCJqZmZmZmbk/FgAoCGZmZmZmZuY/GAiamZmZmZm5PwAAAAsoAgAA" +
        "AA4BAwOIxhoVABUOFRIsFQ4VABUGFQYcGAEBGAEAFgAoAQEYAQAAAAAHGAIAAAAOAU0VBBU6FT5MFQgVABIAAB1wBAAAAHdhbGwEAAAAc2xhYgUAAAB1yJnE" +
        "gwAAAAAVABUUFRgsFQ4VEBUGFQYcNgIoBHdhbGwYAAAAAAokAgAAAAN9AgOQDBUEGXw1ABgGc2NoZW1hFQwAFQIlAhgBaQAVBCUCGAFsABUIJQIYAWYAFQol" +
        "AhgBZAAVACUCGAFiABUMJQIYAXMlAEwcAAAAFg4ZHBlsJgAcFQIZNQAGEBkYAWkVAhYOFrQBFrwBJlAmCBwYBEBCDwAYBP3///8WACgEQEIPABgE/f///wAZ" +
        "LBUEFQAVAgAVABUQFQIAPCkGGSYADgAAACYAHBUEGTUABhAZGAFsFQIWDhacAhaAAiawAibEARwYCACGO6EBAAAAGAgBAAAAAAAAABYAKAgAhjuhAQAAABgI" +
        "AQAAAAAAAAAAGSwVBBUAFQIAFQAVEBUCADwpBhkmAA4AAAAmABwVCBk1AAYQGRgBZhUCFg4WxAEWzAEmnAQmxAMcGAQAABxBGAQAAKC/FgAoBAAAHEEYBAAA" +
        "oL8AGSwVBBUAFQIAFQAVEBUCADwpBhkmAA4AAAAmABwVChk1AAYQGRgBZBUCFg4WnAIWhAImgAYmkAUcGAhmZmZmZmbmPxgImpmZmZmZuT8WACgIZmZmZmZm" +
        "5j8YCJqZmZmZmbk/ABksFQQVABUCABUAFRAVAgA8KQYZJgAOAAAAJgAcFQAZJQYAGRgBYhUCFg4WUBZUJpQHPBgBARgBABYAKAEBGAEAABkcFQAVABUCADwp" +
        "BhkmAA4AAAAmABwVDBk1AAYQGRgBcxUCFg4WpAEWrAEmwggm6AccNgIoBHdhbGwYAAAZLBUEFQAVAgAVABUQFQIAPBYqGQYZJgIMAAAAFqQJFg4mCBaMCQAZ" +
        "HBgMQVJST1c6c2NoZW1hGNgDLy8vLy8xZ0JBQUFRQUFBQUFBQUtBQXdBQmdBRkFBZ0FDZ0FBQUFBQkJBQU1BQUFBQ0FBSUFBQUFCQUFJQUFBQUJBQUFBQVlB" +
        "QUFEMEFBQUF0QUFBQUlRQUFBQllBQUFBTEFBQUFBUUFBQUEwLy8vL0FBQUJCUkFBQUFBVUFBQUFCQUFBQUFBQUFBQUJBQUFBY3dBQUFOei8vLzlZLy8vL0FB" +
        "QUJCaEFBQUFBWUFBQUFCQUFBQUFBQUFBQUJBQUFBWWdBQUFBUUFCQUFFQUFBQWdQLy8vd0FBQVFNUUFBQUFGQUFBQUFRQUFBQUFBQUFBQVFBQUFHUUFBQURh" +
        "Ly8vL0FBQUNBS2ovLy84QUFBRURFQUFBQUJnQUFBQUVBQUFBQUFBQUFBRUFBQUJtQUFZQUNBQUdBQVlBQUFBQUFBRUExUC8vL3dBQUFRSVFBQUFBRkFBQUFB" +
        "UUFBQUFBQUFBQUFRQUFBR3dBQUFERS8vLy9BQUFBQVVBQUFBQVFBQlFBQ0FBR0FBY0FEQUFBQUJBQUVBQUFBQUFBQVFJUUFBQUFIQUFBQUFRQUFBQUFBQUFB" +
        "QVFBQUFHa0FBQUFJQUF3QUNBQUhBQWdBQUFBQUFBQUJJQUFBQUE9PQAYIHBhcnF1ZXQtY3BwLWFycm93IHZlcnNpb24gMjEuMC4wGWwcAAAcAAAcAAAcAAAc" +
        "AAAcAAAAagQAAFBBUjE=";

    // The same table: GZIP, no dictionaries, the integers DELTA_BINARY_PACKED, pages v2.
    const string GzipDeltaV2 =
        "UEFSMRUGFcQBFWZcFQ4VABUOFQoVBBUAERwYBEBCDwAYBP3///8WACgEQEIPABgE/f///wAAAA4BH4sIAAAAAAAAE2tgZGHnau6sEmVgYLBy6vd8wclpq7hw" +
        "u/sLRgaqAADJHkYSYAAAABUGFagEFVJcFQ4VABUOFQoVBBUAERwYCACGO6EBAAAAGAgBAAAAAAAAABYAKAgAhjuhAQAAABgIAQAAAAAAAAAAAAAOAR+LCAAA" +
        "AAAAABNrYGJhZ2JSZMAOFPYXqJvgkBuuAAAf7GEUEgEAABUGFTwVPFwVDhUAFQ4VABUEFQASHBgEAAAcQRgEAACgvxYAKAQAABxBGAQAAKC/AAAADgEAAAA/" +
        "AACgvwAAQEAAAAAAAAAgQAAAHEEAAAC+FQYVdBVoXBUOFQAVDhUAFQQVABEcGAhmZmZmZmbmPxgImpmZmZmZuT8WACgIZmZmZmZm5j8YCJqZmZmZmbk/AAAA" +
        "DgEfiwgAAAAAAAATmzUTBHbazwLTJ+2NweAylH/TngEMHkDFH9ungcEzewBozJPXOAAAABUGFRAVEFwVDhUAFQ4VBhUEFQASHBgBARgBABYAKAEBGAEAAAAA" +
        "DgECAAAAA00VBhVeFV5cFQ4VAhUOFQAVBBUAEhw2AigEd2FsbBgAAAAAA30EAAAAd2FsbAQAAAB3YWxsBAAAAHNsYWIFAAAAdciZxIMEAAAAd2FsbAAAAAAV" +
        "BBl8NQAYBnNjaGVtYRUMABUCJQIYAWkAFQQlAhgBbAAVCCUCGAFmABUKJQIYAWQAFQAlAhgBYgAVDCUCGAFzJQBMHAAAABYOGRwZbCYAHBUCGSUGChkYAWkV" +
        "BBYOFqoCFswBJgg8GARAQg8AGAT9////FgAoBEBCDwAYBP3///8AGRwVABUKFQIAPCkGGSYADgAAACYAHBUEGSUGChkYAWwVBBYOFq4FFtgBJtQBPBgIAIY7" +
        "oQEAAAAYCAEAAAAAAAAAFgAoCACGO6EBAAAAGAgBAAAAAAAAAAAZHBUAFQoVAgA8KQYZJgAOAAAAJgAcFQgZJQYAGRgBZhUEFg4WoAEWoAEmrAM8GAQAABxB" +
        "GAQAAKC/FgAoBAAAHEEYBAAAoL8AGRwVABUAFQIAPCkGGSYADgAAACYAHBUKGSUGABkYAWQVBBYOFvgBFuwBJswEPBgIZmZmZmZm5j8YCJqZmZmZmbk/FgAo" +
        "CGZmZmZmZuY/GAiamZmZmZm5PwAZHBUAFQAVAgA8KQYZJgAOAAAAJgAcFQAZFQYZGAFiFQQWDhZcFlwmuAY8GAEBGAEAFgAoAQEYAQAAGRwVABUGFQIAPCkG" +
        "GSYADgAAACYAHBUMGSUGABkYAXMVBBYOFqIBFqIBJpQHPDYCKAR3YWxsGAAAGRwVABUAFQIAPBYqGQYZJgIMAAAAFu4MFg4mCBauCAAZHBgMQVJST1c6c2No" +
        "ZW1hGNgDLy8vLy8xZ0JBQUFRQUFBQUFBQUtBQXdBQmdBRkFBZ0FDZ0FBQUFBQkJBQU1BQUFBQ0FBSUFBQUFCQUFJQUFBQUJBQUFBQVlBQUFEMEFBQUF0QUFB" +
        "QUlRQUFBQllBQUFBTEFBQUFBUUFBQUEwLy8vL0FBQUJCUkFBQUFBVUFBQUFCQUFBQUFBQUFBQUJBQUFBY3dBQUFOei8vLzlZLy8vL0FBQUJCaEFBQUFBWUFB" +
        "QUFCQUFBQUFBQUFBQUJBQUFBWWdBQUFBUUFCQUFFQUFBQWdQLy8vd0FBQVFNUUFBQUFGQUFBQUFRQUFBQUFBQUFBQVFBQUFHUUFBQURhLy8vL0FBQUNBS2ov" +
        "Ly84QUFBRURFQUFBQUJnQUFBQUVBQUFBQUFBQUFBRUFBQUJtQUFZQUNBQUdBQVlBQUFBQUFBRUExUC8vL3dBQUFRSVFBQUFBRkFBQUFBUUFBQUFBQUFBQUFR" +
        "QUFBR3dBQUFERS8vLy9BQUFBQVVBQUFBQVFBQlFBQ0FBR0FBY0FEQUFBQUJBQUVBQUFBQUFBQVFJUUFBQUFIQUFBQUFRQUFBQUFBQUFBQVFBQUFHa0FBQUFJ" +
        "QUF3QUNBQUhBQWdBQUFBQUFBQUJJQUFBQUE9PQAYIHBhcnF1ZXQtY3BwLWFycm93IHZlcnNpb24gMjEuMC4wGWwcAAAcAAAcAAAcAAAcAAAcAAAAMwQAAFBB" +
        "UjE=";

    [Theory]
    [InlineData(SnappyDictionary)]
    [InlineData(GzipDeltaV2)]
    public void Lapins_reader_reads_what_pyarrow_writes(string file)
    {
        var cols = Parquet.Read(Convert.FromBase64String(file)).ToDictionary(c => c.Name);
        Assert.Equal([5, -3, 7, 7, 7, 1000000, -2], cols["i"].Ints!);
        Assert.Equal([1L, 2, 3, 4, 5, 6, 7000000000], cols["l"].Longs!);
        Assert.Equal([0.5f, -1.25f, 3f, 0f, 2.5f, 9.75f, -0.125f], cols["f"].Floats!);
        Assert.Equal([0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7], cols["d"].Doubles!);
        Assert.Equal([true, false, true, true, false, false, true], cols["b"].Bools!);
        Assert.Equal(new string?[] { "wall", null, "wall", "slab", "ușă", "wall", "" }.AsEnumerable(), cols["s"].Strings!.AsEnumerable());
    }

    [Theory]
    [InlineData(Parquet.Brotli)]
    [InlineData(Parquet.Uncompressed)]
    public void What_Lapin_writes_it_reads_back(int codec)
    {
        var n = 5000;
        var columns = new List<ParquetColumn>
        {
            new("i") { Ints = [.. Enumerable.Range(0, n).Select(i => i * 7 - 300)] },
            new("l") { Longs = [.. Enumerable.Range(0, n).Select(i => (long)i << 33)] },
            new("f") { Floats = [.. Enumerable.Range(0, n).Select(i => i / 8f)] },
            new("b") { Ints = [.. Enumerable.Range(0, n).Select(i => i % 256)], Byte = true },
            new("s") { Strings = [.. Enumerable.Range(0, n).Select(i => i % 10 == 0 ? null : $"wall {i}")] },
        };
        var back = Parquet.Read(Parquet.Write(columns, codec)).ToDictionary(c => c.Name);
        Assert.Equal(columns[0].Ints, back["i"].Ints);
        Assert.Equal(columns[1].Longs, back["l"].Longs);
        Assert.Equal(columns[2].Floats, back["f"].Floats);
        Assert.Equal(columns[3].Ints, back["b"].Ints);
        Assert.True(back["b"].Byte);
        Assert.Equal(columns[4].Strings!.AsEnumerable(), back["s"].Strings!.AsEnumerable());
    }

    [Fact]
    public void Snappy_literals_and_copies()
    {
        // "abc", then nine bytes copied from three back: abcabcabcabc.
        byte[] block = [12, 0x08, (byte)'a', (byte)'b', (byte)'c', 0x15, 3];
        Assert.Equal("abcabcabcabc", System.Text.Encoding.ASCII.GetString(SnappyCodec.Decompress(block)));
    }

    /// <summary>Two storeys: walls with a door and windows, a floor, columns of one type.</summary>
    static Fixture House()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
        var w0 = f.Wall(0, 0, 8, 0);
        f.Wall(8, 0, 8, 6);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = w0, ["along"] = 2 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = w0, ["along"] = 5.5 });
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(8, 0), new JsonArray(8, 6), new JsonArray(0, 6)) });
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        foreach (var (x, y) in new[] { (0.0, 6.0), (4.0, 6.0), (8.0, 6.0) }) f.Run("structure.addColumn", new JsonObject { ["storeyId"] = first, ["typeId"] = type, ["x"] = x, ["y"] = y });
        return f;
    }

    [Fact]
    public void A_project_goes_out_as_the_converter_writes_and_comes_back_where_it_was()
    {
        var f = House();
        var (bytes, result) = MeshExport.Write(f.Session, new MeshExportOptions("bos", Origin: "internal"));
        Assert.Equal("bos", result.Format);
        var d = BosData.Read(bytes);
        var v = new BosView(d);

        // Categories are entities named after the IFC class; elements keep their GlobalIds and stand ContainedIn their storeys.
        var walls = Enumerable.Range(0, d.Entities.Count).Where(e => v.Class[e] == IfcClass.Wall).ToList();
        Assert.Equal(2, walls.Count);
        Assert.All(walls, w => Assert.NotNull(f.Session.Model.Get(v.Guid(w)!)));
        Assert.All(walls, w => Assert.True(v.StoreyOf.ContainsKey(w)));
        Assert.Contains(Enumerable.Range(0, d.Entities.Count), e => v.Class[e] == IfcClass.BuildingStorey && v.Name(e) == "First floor");
        Assert.Equal(3, Enumerable.Range(0, d.Entities.Count).Count(e => v.Class[e] == IfcClass.Column));
        // The columns of one type share one mesh, each placed by its own translation.
        var columnMeshes = d.Instances.Where(i => v.Class[i.Entity] == IfcClass.Column).Select(i => i.Mesh).Distinct().Count();
        Assert.Equal(1, columnMeshes);
        // Their type is an entity; the storey's elevation an Ifc: parameter grouped by its class.
        Assert.All(Enumerable.Range(0, d.Entities.Count).Where(e => v.Class[e] == IfcClass.Column), c => Assert.Equal("C30", v.Name(d.Entities[c].Type)));
        var first = Enumerable.Range(0, d.Entities.Count).First(e => v.Name(e) == "First floor" && v.Class[e] == IfcClass.BuildingStorey);
        Assert.Equal(3.0, v.Elevation(first)!.Value, 6);

        // The door's and the window's openings: IFCOPENINGELEMENT entities that void their wall, filled by them, sized, hidden in a viewer.
        var openings = Enumerable.Range(0, d.Entities.Count).Where(e => v.Category[e] == "IFCOPENINGELEMENT").ToList();
        Assert.Equal(2, openings.Count);
        foreach (var o in openings)
        {
            Assert.Contains(d.Relations, r => r.A == o && r.Type == BosRelation.Voids && walls.Contains(r.B));
            Assert.Contains(d.Relations, r => r.B == o && r.Type == BosRelation.Fills && v.Class[r.A] is "IfcDoor" or "IfcWindow");
            Assert.Contains(v.Params(o), p => p.Name == "Width" && p.Group == "Qto_OpeningElementBaseQuantities");
            Assert.All(d.Instances.Where(i => i.Entity == o), i => Assert.Equal(1, i.Flags));
            Assert.False(v.InstancesOf.ContainsKey(o));
        }

        // Imported into an empty project: every element with a body, the storeys made, the columns instances of one shared body.
        var g = new Fixture(new ArchitectureModule(), new StructureModule());
        var r = BosImport.Run(g.Session, v, "house.bos", out _);
        Assert.Equal(result.Elements, r.Elements);
        Assert.Equal(1, r.Shared);
        Assert.Contains(Spatial.Storeys(g.Session.Model), s => s.Name == "First floor" && Math.Abs(s.Num("elevation") - 3) < 1e-6);
        // Where it was: the walls' boxes as Lapin draws them, to a tenth of a millimetre.
        foreach (var w in walls)
        {
            var id = v.Guid(w)!;
            Assert.Equal(IfcClass.Wall, g.Session.Model.Get(id)!.Class);
            var (a0, a1) = Box(f.Session, id);
            var (b0, b1) = Box(g.Session, id);
            Assert.True((a0 - b0).Length < 2e-4 && (a1 - b1).Length < 2e-4, $"{a0}–{a1} became {b0}–{b1}");
        }
        // The window's properties come back as sets; its class as written.
        Assert.Contains(g.Session.Model.Entities, e => e.Class == IfcClass.Window);
    }

    /// <summary>An element's box as the exports see it (its triangles, the internal origin).</summary>
    static (Lapin.Geometry.Vec3 Min, Lapin.Geometry.Vec3 Max) Box(Session s, string id)
    {
        var scene = MeshScene.Build(s, new MeshExportOptions("glb", Origin: "internal"));
        var el = scene.Groups.SelectMany(g => g.Elements).SelectMany(e => e.Flatten()).First(e => e.GlobalId == id);
        var p = el.Parts.SelectMany(x => x.Positions).ToArray();
        var xs = Enumerable.Range(0, p.Length / 3).Select(i => new Lapin.Geometry.Vec3(p[i * 3], p[i * 3 + 1], p[i * 3 + 2])).ToList();
        return (new(xs.Min(q => q.X), xs.Min(q => q.Y), xs.Min(q => q.Z)), new(xs.Max(q => q.X), xs.Max(q => q.Y), xs.Max(q => q.Z)));
    }

    [Fact]
    public void A_real_file_when_given()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_BOS_SAMPLE");
        if (string.IsNullOrEmpty(path)) return;
        var v = BosImport.Read(File.ReadAllBytes(path), Path.GetFileName(path));
        Assert.NotEmpty(v.InstancesOf);
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var r = BosImport.Run(f.Session, v, Path.GetFileName(path), out _);
        Assert.True(r.Elements > 0);
        var report = Environment.GetEnvironmentVariable("LAPIN_BOS_REPORT");
        if (!string.IsNullOrEmpty(report))
        {
            var json = r.ToJson();
            json["survey"] = BosSurvey.Of(v, Path.GetFileName(path), 0);
            File.WriteAllText(report, json.ToJsonString());
            File.WriteAllBytes(Path.ChangeExtension(report, ".glb"), MeshExport.Write(f.Session, new MeshExportOptions("glb", Origin: "internal")).Bytes);
            File.WriteAllBytes(Path.ChangeExtension(report, ".bos"), MeshExport.Write(f.Session, new MeshExportOptions("bos", Origin: "internal")).Bytes);
        }
    }
}
