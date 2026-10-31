using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// IFC and Fragments chosen alike (docs/fragments.md, Exchange): the project surveyed before an export as a file is
/// before an import, its kinds and storeys chosen, what is left out left out of the file.
/// </summary>
public class ExchangeChoiceTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-exchange").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void An_export_takes_the_kinds_and_storeys_chosen()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        f.Wall(0, 0, 4, 0);
        f.Wall(4, 0, 4, 3);
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 1, ["y"] = 1 });
        var survey = ModelSurvey.Of(f.Session);
        var kinds = survey["groups"]!.AsArray().SelectMany(g => g!["kinds"]!.AsArray()).ToDictionary(k => (string)k!["key"]!, k => (int)k!["count"]!);
        Assert.Equal(2, kinds["IfcWall"]);
        Assert.Equal(1, kinds["IfcColumn"]);
        Assert.Equal(3, (int)survey["storeys"]!.AsArray().Single()!["count"]!);

        // The walls alone.
        var p = new JsonObject { ["kinds"] = new JsonArray("IfcWall") };
        var path = Path.Combine(_dir, "walls.ifc");
        IfcExport.Write(f.Session, path, new IfcExportOptions(Exclude: ModelSurvey.Excluded(f.Session, p)));
        var text = File.ReadAllText(path);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(text, "=IFCWALL\\(").Count);
        Assert.DoesNotContain("=IFCCOLUMN(", text);
        // No storey chosen: no element.
        Assert.Equal(3, ModelSurvey.Excluded(f.Session, new JsonObject { ["storeys"] = new JsonArray() }).Count);
        // Nothing asked: nothing left out.
        Assert.Empty(ModelSurvey.Excluded(f.Session, new JsonObject()));
    }
}
