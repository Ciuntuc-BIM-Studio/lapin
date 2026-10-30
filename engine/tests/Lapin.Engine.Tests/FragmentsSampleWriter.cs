using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Mesh;
using Lapin.Mesh.Fragments;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>Writes a sample .frag to LAPIN_FRAG_SAMPLE_OUT when set, for the official library to read (scripts/fragments-check.mjs); otherwise nothing.</summary>
public class FragmentsSampleWriter
{
    [Fact]
    public void A_sample_file_for_the_official_library()
    {
        var out_ = Environment.GetEnvironmentVariable("LAPIN_FRAG_SAMPLE_OUT");
        if (string.IsNullOrEmpty(out_)) return;
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        f.Wall(0, 0, 6, 0);
        f.Wall(6, 0, 6, 4);
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        foreach (var x in new[] { 1.0, 3.0, 5.0 })
            f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = x, ["y"] = 2 });
        var scene = MeshScene.Build(f.Session, new MeshExportOptions("frag"));
        File.WriteAllBytes(out_, FragWriter.Write(scene, modelGuid: "lapin-sample"));
    }
}
