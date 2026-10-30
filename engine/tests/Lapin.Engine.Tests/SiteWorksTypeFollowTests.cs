using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Phases;
using Lapin.SiteWorks;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>A scaffold's or a formwork's type changed in its configurator: the elements of the type are rebuilt in the same diff (the interface sees them change).</summary>
public class SiteWorksTypeFollowTests
{
    static Fixture Site()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new PhasesModule(), new CivilModule(), new SiteWorksModule());
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["name"] = "Ground floor", ["elevation"] = 0, ["height"] = 3 });
        return f;
    }

    [Fact]
    public void A_scaffold_follows_its_type_changed_in_the_configurator()
    {
        var f = Site();
        var id = (string)f.Run("sitework.addScaffold", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0)), ["height"] = 6 })!["id"]!;
        var before = f.Bounds(id);
        var type = Scaffolds.TypeOf(f.Model, f.Model.Get(id)!)!;
        var spec = (JsonObject)(type.Attrs["spec"]?.DeepClone() ?? new JsonObject());
        spec["width"] = 1.5;
        var diff = f.RunDiff("sitework.updateScaffoldType", new JsonObject { ["id"] = type.Id, ["spec"] = spec });
        Assert.Contains(diff.Render, r => r.NodeId == id);
        var after = f.Bounds(id);
        Assert.Equal((before.Max.Y - before.Min.Y) + (1.5 - Scaffolds.Read(null).Width), after.Max.Y - after.Min.Y, 3);
    }

    [Fact]
    public void A_formwork_follows_its_type_changed_in_the_configurator()
    {
        var f = Site();
        var slabType = (string)f.Run("architecture.createSlabType", new JsonObject { ["name"] = "RC 200", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Reinforced concrete", ["thickness"] = 0.20, ["core"] = true }) })!["id"]!;
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = slabType, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 4), new JsonArray(0, 4)) })!["id"]!;
        var fw = ((JsonArray)f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(slab), ["faces"] = new JsonArray("Soffit") })!["ids"]!).Select(x => (string)x!).First();
        var partsBefore = f.Session.Geometry(fw)!.Parts.Count;
        var type = Formwork.TypeOf(f.Model, f.Model.Get(fw)!)!;
        var spec = (JsonObject)(type.Attrs["spec"]?.DeepClone() ?? new JsonObject());
        spec["system"] = "panels";
        var diff = f.RunDiff("sitework.updateFormworkType", new JsonObject { ["id"] = type.Id, ["spec"] = spec });
        Assert.Contains(diff.Render, r => r.NodeId == fw);
        Assert.NotEqual(partsBefore, f.Session.Geometry(fw)!.Parts.Count);
    }
}
