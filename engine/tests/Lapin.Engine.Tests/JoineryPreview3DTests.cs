using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The Doors and windows configurator's 3D preview (report #108): the draft in a piece of wall of the project's first wall
/// type, built in a model of its own, the project untouched.
/// </summary>
public class JoineryPreview3DTests
{
    static Fixture Full() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());
    static JsonObject Q(Fixture f, JsonNode spec) => (JsonObject)f.Session.Query("architecture.joineryPreview3d", new JsonObject { ["spec"] = spec.DeepClone() })!;

    [Fact]
    public void A_window_stands_in_a_cut_wall_of_the_projects_first_type_and_the_project_is_untouched()
    {
        var f = Full();
        f.Wall(0, 0, 5, 0);
        var before = (f.Model.Entities.Count(), f.Model.Relations.Count(), f.Model.Revision);
        var spec = JoinerySpec.DefaultWindow();
        var p = Q(f, spec.ToJson());
        Assert.Null((string?)p["problem"]);
        var parts = p["parts"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Contains(parts, x => (string)x["element"]! == "wall");
        Assert.Contains(parts, x => (string)x["element"]! == "fill" && (string)x["name"]! == "glass");
        foreach (var x in parts) Assert.True(x["positions"]!.AsArray().Count >= 9 && x["positions"]!.AsArray().Count % 3 == 0);
        // The wall runs the window's width and 0.6 m each side, its thickness the type's build-up.
        var size = p["size"]!.AsArray();
        Assert.Equal(spec.Width + 2 * JoineryPreview3D.Margin, (double)size[0]!, 6);
        var type = f.Model.OfClass(IfcClass.WallType).First();
        Assert.Equal(LayerSets.LayersOf(f.Model, type.Id).Sum(l => l.Thickness), (double)size[1]!, 6);
        // The wall is cut: no wall vertex lies inside the opening's middle.
        var wallXs = parts.Where(x => (string)x["element"]! == "wall").SelectMany(x => Triples(x["positions"]!.AsArray()))
            .Where(v => v.Z > spec.SillHeight + 0.1 && v.Z < spec.SillHeight + spec.Height - 0.1).Select(v => v.X).ToList();
        Assert.DoesNotContain(wallXs, x => Math.Abs(x) < spec.Width / 2 - 0.1);
        // The project's materials keep their ids, so the interface gives them their looks.
        var materialIds = f.Model.OfClass(IfcClass.Material).Select(m => m.Id).ToHashSet();
        Assert.Contains(parts, x => (string?)x["material"] is { } id && materialIds.Contains(id));
        Assert.Equal(before, (f.Model.Entities.Count(), f.Model.Relations.Count(), f.Model.Revision));
    }

    [Fact]
    public void A_door_a_bay_window_and_a_spec_that_cannot_be_built()
    {
        var f = Full();
        var door = Q(f, JoinerySpec.DefaultDoor().ToJson());
        Assert.Null((string?)door["problem"]);
        Assert.Contains(door["parts"]!.AsArray(), x => (string)x!["name"]! == "leaf");
        var bay = BayWindows.Styles.First().Spec;
        var b = Q(f, bay.ToJson());
        Assert.Null((string?)b["problem"]);
        // A bay projects out of the wall's exterior face (−y).
        var minY = b["parts"]!.AsArray().OfType<JsonObject>().Where(x => (string)x["element"]! == "fill").SelectMany(x => Triples(x["positions"]!.AsArray())).Min(v => v.Y);
        Assert.True(minY < -(double)b["size"]![1]! / 2 - bay.Bay!.Projection * 0.9);
        var bad = JoinerySpec.DefaultWindow() with { Width = -1 };
        Assert.NotNull((string?)Q(f, bad.ToJson())["problem"]);
    }

    static IEnumerable<(double X, double Y, double Z)> Triples(JsonArray a)
    {
        for (var i = 0; i + 2 < a.Count; i += 3) yield return ((double)a[i]!, (double)a[i + 1]!, (double)a[i + 2]!);
    }
}
