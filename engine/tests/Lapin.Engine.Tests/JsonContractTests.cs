using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Architecture.Stairs;
using Lapin.Civil;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Package;

namespace Lapin.Engine.Tests;

/// <summary>
/// The JSON contracts generated at build time write exactly what reflection
/// wrote before them, with the same options: the files, the catalogues the
/// interface reads and the specs the types keep stay as they were.
/// </summary>
public class JsonContractTests
{
    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    static readonly JsonSerializerOptions WebNoNulls = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    static void Same<T>(T value, JsonSerializerOptions reflection, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> generated)
    {
        var before = JsonSerializer.Serialize(value, reflection);
        var after = JsonSerializer.Serialize(value, generated);
        Assert.Equal(before, after);
        // And it reads back to what it was.
        Assert.Equal(after, JsonSerializer.Serialize(JsonSerializer.Deserialize(after, generated)!, generated));
    }

    [Fact]
    public void The_specs_types_keep_are_written_as_before()
    {
        Same(JoinerySpec.DefaultDoor(), Json.Options, ArchitectureJson.Default.JoinerySpec);
        Same(JoinerySpec.DefaultWindow(), Json.Options, ArchitectureJson.Default.JoinerySpec);
        Same(LintelRule.Default, Json.Options, ArchitectureJson.Default.LintelRule);
        Same(new LintelProduct("PL 25", 0.12, 0.25, [1.0, 1.25, 1.5], "Maker"), Json.Options, ArchitectureJson.Default.LintelProduct);
        Same(StairSpec.Default(), Json.Options, ArchitectureJson.Default.StairSpec);
        Same(new List<MaterialLayer> { new("m1", 0.02, false, "Render"), new("m2", 0.25, true) }, Json.Options, ModelJson.Default.ListMaterialLayer);
    }

    [Fact]
    public void The_catalogues_the_interface_reads_are_written_as_before()
    {
        Same(RoofCatalog.Styles, Web, ArchitectureCatalogJson.Default.IReadOnlyListStyle);
        Same(RoofCatalog.Coverings, Web, ArchitectureCatalogJson.Default.IReadOnlyListCovering);
        Same(StairRules.Residential, Web, ArchitectureCatalogJson.Default.StairRules);
        Same(Plantings.Catalog[0], Web, CivilJson.Default.Species);
        Same(Standards.Read(null), WebNoNulls, DrawingJson.Default.StyleBook);
    }

    [Fact]
    public void The_package_manifest_and_the_records_the_journal_keeps_are_written_as_before()
    {
        var manifest = new PackageManifest("lapin-project", 3, "Lapin 1", "lapin", [new PackageModule("architecture", 1)], [new PackagePart("project/document.json", "abc", 2)], new Dictionary<string, string> { ["IfcWall"] = "architecture" });
        Same(manifest, Json.Options, PackageJson.Default.PackageManifest);
        Same(new Entity("e1", "IfcWall", "Wall", new JsonObject { ["height"] = 3.0 }, "STANDARD"), Json.Options, ModelJson.Default.Entity);
        Same(new Relation("r1", "IfcRelDefinesByType", "t1", ["e1", "e2"]), Json.Options, ModelJson.Default.Relation);
    }
}
