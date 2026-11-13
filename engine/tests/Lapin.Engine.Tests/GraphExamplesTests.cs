using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Graphs;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The example graphs the Graphs panel offers (ui/src/modules/graphs/examples/*.graph.json, one copy, read here from the
/// UI folder): each parses, evaluates without an error on a new project, and bakes in one undo step.
/// </summary>
public sealed class GraphExamplesTests
{
    /// <summary>The UI's examples folder, found from the test's folder upwards.</summary>
    static string Folder
    {
        get
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            {
                var p = Path.Combine(d.FullName, "ui", "src", "modules", "graphs", "examples");
                if (Directory.Exists(p)) return p;
            }
            throw new DirectoryNotFoundException("ui/src/modules/graphs/examples was not found above the tests.");
        }
    }

    public static IEnumerable<object[]> Names => Directory.GetFiles(Folder, "*.graph.json").Select(f => new object[] { Path.GetFileName(f)[..^".graph.json".Length] }).OrderBy(x => (string)x[0]);

    public static JsonObject Read(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(Folder, name + ".graph.json")))!.AsObject();

    static Fixture NewProject() => new(new ArchitectureModule(), new StructureModule(), new GraphsModule());

    [Fact]
    public void There_are_examples() => Assert.True(Names.Count() >= 3);

    [Theory]
    [MemberData(nameof(Names))]
    public void An_example_parses_evaluates_without_errors_and_bakes_in_one_step(string name)
    {
        var f = NewProject();
        var g = Read(name);
        var doc = GraphDoc.Read(g);
        Assert.True((bool)doc.Tool!["show"]!);
        Assert.False(string.IsNullOrWhiteSpace((string?)doc.Tool["name"]));

        var id = (string)f.Run("graphs.save", new JsonObject { ["name"] = name, ["graph"] = g })!["id"]!;
        var r = f.Session.Query("graphs.evaluate", new JsonObject { ["id"] = id })!.AsObject();
        Assert.All(r["nodes"]!.AsObject(), n => Assert.Null(n.Value!["error"]));

        var before = f.Model.Entities.Count();
        var undo = f.Session.History.UndoLabel;
        f.Run("graphs.bake", new JsonObject { ["id"] = id });
        Assert.Equal("Bake graph", f.Session.History.UndoLabel);
        Assert.True(f.Model.Entities.Count() > before);
        f.Session.Undo();
        Assert.Equal(undo, f.Session.History.UndoLabel);
    }

    [Fact]
    public void The_twisted_tower_makes_twelve_floors_on_twelve_storeys_with_columns()
    {
        var f = NewProject();
        var id = (string)f.Run("graphs.save", new JsonObject { ["name"] = "Twisted tower", ["graph"] = Read("twisted-tower") })!["id"]!;
        f.Run("graphs.bake", new JsonObject { ["id"] = id });
        Assert.Equal(12, Spatial.Storeys(f.Model).Count());
        Assert.Equal(12, f.Model.OfClass(IfcClass.Slab).Count());
        Assert.Equal(48, f.Model.OfClass(IfcClass.Column).Count());
    }

    [Fact]
    public void The_twisted_tower_puts_its_first_floor_on_the_storey_at_level_zero_even_when_the_project_has_others_below_and_above()
    {
        // A new project in the app: a basement, the ground storey and one above.
        var f = NewProject();
        f.Run("storey.create", new JsonObject { ["elevation"] = -1.5 });
        f.Run("storey.create", new JsonObject { ["elevation"] = 3 });
        var ground = Spatial.Storeys(f.Model).Single(s => Math.Abs(s.Num("elevation")) < 1e-9);
        var id = (string)f.Run("graphs.save", new JsonObject { ["name"] = "Twisted tower", ["graph"] = Read("twisted-tower") })!["id"]!;
        f.Run("graphs.bake", new JsonObject { ["id"] = id });
        Assert.Equal(3 + 11, Spatial.Storeys(f.Model).Count());
        var onGround = f.Model.OfClass(IfcClass.Slab).Where(s => Spatial.StoreyOf(f.Model, s.Id)!.Id == ground.Id).ToList();
        Assert.Single(onGround);
        Assert.Equal(12, f.Model.OfClass(IfcClass.Slab).Count());
        Assert.Equal(48, f.Model.OfClass(IfcClass.Column).Count());
    }

    [Fact]
    public void The_grid_makes_twenty_columns_and_the_rectangle_four_walls()
    {
        var f = NewProject();
        f.Run("graphs.bake", new JsonObject { ["graph"] = Read("columns-on-a-grid") });
        Assert.Equal(20, f.Model.OfClass(IfcClass.Column).Count());
        f.Run("graphs.bake", new JsonObject { ["graph"] = Read("walls-round-a-rectangle") });
        Assert.Equal(4, f.Model.OfClass(IfcClass.Wall).Count());
    }
}
