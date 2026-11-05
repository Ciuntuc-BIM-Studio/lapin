using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Mep;
using Lapin.Mesh;
using Lapin.Mesh.Fragments;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// The files of the cross-check with the official library (docs/fragments.md, scripts/fragments-check.mjs,
/// scripts/fragments-render.mjs): with LAPIN_FRAG_CHECK_OUT=&lt;folder&gt; each scene is written there as
/// <c>&lt;name&gt;.frag</c> (Lapin's Fragments), <c>&lt;name&gt;.glb</c> (the same mesh scene, Lapin's geometry as its 3D view
/// draws it) and <c>&lt;name&gt;.ifc</c> (Lapin's IFC, for That Open's own IfcImporter to make the reference .frag).
/// LAPIN_FRAG_CHECK_FILES=&lt;a.lapin;b.ifc&gt; adds projects or IFC files (an IFC file is imported first). Otherwise nothing.
/// </summary>
public class FragmentsCrossCheckWriter
{
    [Fact]
    public void Writes_the_cross_check_files_when_asked()
    {
        var dir = Environment.GetEnvironmentVariable("LAPIN_FRAG_CHECK_OUT");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        Write(dir, "mixed", Mixed(), "base");
        var geo = Mixed();
        geo.Run("project.location", new JsonObject { ["crs"] = "EPSG:3844", ["survey"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["easting"] = 587900, ["northing"] = 325820, ["elevation"] = 80 } });
        Write(dir, "geo", geo, "survey");
        var shared = All();
        IfcImport.Run(shared.Session, Encoding.UTF8.GetBytes(SharedBodyTests.Cubes(40, turned: true, mapped: true, opening: true, oblique: true, stacked: 2)), "cubes.ifc", null, out _);
        Write(dir, "shared", shared, "base");
        foreach (var path in (Environment.GetEnvironmentVariable("LAPIN_FRAG_CHECK_FILES") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = All();
            if (path.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase)) IfcImport.Run(f.Session, path, null, out _);
            else Lapin.Host.PackageFiles.Open(f.Session, path);
            Write(dir, Path.GetFileNameWithoutExtension(path).Replace(' ', '-'), f, "base");
        }
        // The reverse: each .frag named (Lapin's own, or That Open's) imported into an empty project, written back as GLB to be seen beside the file.
        foreach (var path in (Environment.GetEnvironmentVariable("LAPIN_FRAG_CHECK_IMPORT") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = All();
            var r = FragImport.Run(f.Session, File.ReadAllBytes(path), Path.GetFileName(path), out _);
            var name = Path.GetFileNameWithoutExtension(path) + "-imported";
            File.WriteAllBytes(Path.Combine(dir, name + ".glb"), MeshExport.Write(f.Session, new MeshExportOptions("glb", Origin: "internal")).Bytes);
            File.WriteAllText(Path.Combine(dir, name + ".json"), r.ToJson().ToJsonString());
        }
    }

    static Fixture All() => new(new ArchitectureModule(), new StructureModule(), new StudioModule(), new SteelModule(), new MepModule());

    static void Write(string dir, string name, Fixture f, string origin)
    {
        var o = new MeshExportOptions("frag", Origin: origin);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var (bytes, r) = MeshExport.Write(f.Session, o);
        var json = r.ToJson();
        json["seconds"] = Math.Round(clock.Elapsed.TotalSeconds, 2);
        json["bytes"] = bytes.Length;
        File.WriteAllBytes(Path.Combine(dir, name + ".frag"), bytes);
        File.WriteAllText(Path.Combine(dir, name + ".json"), json.ToJsonString());
        // LAPIN_FRAG_CHECK_FRAG_ONLY: a big model's .frag alone (its GLB and IFC take long and are not needed to measure it).
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LAPIN_FRAG_CHECK_FRAG_ONLY"))) return;
        File.WriteAllBytes(Path.Combine(dir, name + ".glb"), MeshExport.Write(f.Session, o with { Format = "glb" }).Bytes);
        IfcExport.Write(f.Session, Path.Combine(dir, name + ".ifc"));
    }

    /// <summary>Two storeys: layered walls with a door and windows, a curved wall, a floor, a roof, columns and a beam, a Studio solid, a steel member, a heating run.</summary>
    static Fixture Mixed()
    {
        var f = All();
        var ground = f.Storey;
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
        string W(string storey, double x1, double y1, double x2, double y2, JsonObject? more = null)
        {
            var a = new JsonObject { ["storeyId"] = storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 };
            foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
            return (string)f.Run("architecture.addWall", a)!["id"]!;
        }
        var south = W(ground, 0, 0, 10, 0);
        W(ground, 10, 0, 10, 6);
        W(ground, 10, 6, 0, 6, new JsonObject { ["arc"] = 1.5 });
        W(ground, 0, 6, 0, 0);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = south, ["along"] = 2 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = south, ["along"] = 6 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = south, ["along"] = 8.5 });
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = ground, ["points"] = MepFixture.Pts((0, 0), (10, 0), (10, 6), (0, 6)) });
        var upper = W(first, 0, 0, 10, 0);
        W(first, 10, 0, 10, 6);
        W(first, 10, 6, 0, 6);
        W(first, 0, 6, 0, 0);
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = upper, ["along"] = 3 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = upper, ["along"] = 7 });
        f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = first, ["points"] = MepFixture.Pts((0, 0), (10, 0), (10, 6), (0, 6)) });
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        foreach (var x in new[] { 13.0, 15.0, 17.0 })
            f.Run("structure.addColumn", new JsonObject { ["storeyId"] = ground, ["typeId"] = type, ["x"] = x, ["y"] = 2 });
        f.Run("structure.addBeam", new JsonObject { ["storeyId"] = ground, ["x1"] = 13, ["y1"] = 2, ["x2"] = 17, ["y2"] = 2 });
        f.Run("studio.primitive", new JsonObject { ["storeyId"] = ground, ["shape"] = "cylinder", ["at"] = new JsonArray(5, 10), ["radius"] = 0.8, ["height"] = 2 });
        f.Run("studio.primitive", new JsonObject { ["storeyId"] = ground, ["shape"] = "sphere", ["at"] = new JsonArray(8, 10), ["radius"] = 1 });
        f.Run("steel.addMember", new JsonObject { ["storeyId"] = ground, ["x1"] = 12, ["y1"] = 5, ["z1"] = 0, ["x2"] = 18, ["y2"] = 5, ["z2"] = 3 });
        f.Run("mep.placeSystem", new JsonObject { ["service"] = Services.Heating, ["storeyId"] = ground, ["x"] = 2, ["y"] = 2 });
        return f;
    }
}
