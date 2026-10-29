using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Parameters in named property sets (docs/plan-assistant.md): IFC's own only with their names, the user's never „Pset_…”.</summary>
public class StandardPsetsTests
{
    static (Fixture F, string Door) WithDoor()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 4, 0);
        return (f, (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 2 })!["id"]!);
    }

    static JsonArray Params(Fixture f, string id) => (JsonArray)f.Model.Get(id)!.Attrs["params"]!;

    [Fact]
    public void A_standard_property_takes_its_kind_from_the_set()
    {
        var (f, door) = WithDoor();
        f.Run("element.setParam", new JsonObject { ["id"] = door, ["pset"] = "Pset_DoorCommon", ["name"] = "FireRating", ["value"] = "EI30" });
        f.Run("element.setParam", new JsonObject { ["id"] = door, ["pset"] = "Pset_DoorCommon", ["name"] = "SelfClosing", ["value"] = true });
        var p = Params(f, door).OfType<JsonObject>().ToList();
        Assert.Equal(("Pset_DoorCommon", "text", "EI30"), ((string)p[0]["pset"]!, (string)p[0]["kind"]!, (string)p[0]["value"]!));
        Assert.Equal("bool", (string)p[1]["kind"]!);
    }

    [Fact]
    public void A_standard_set_refuses_names_it_does_not_have_and_classes_it_is_not_for()
    {
        var (f, door) = WithDoor();
        var wall = f.Model.OfClass(IfcClass.Wall).First().Id;
        Assert.Throws<CommandException>(() => f.Run("element.setParam", new JsonObject { ["id"] = door, ["pset"] = "Pset_DoorCommon", ["name"] = "FireResistance", ["value"] = "EI30" }));
        Assert.Throws<CommandException>(() => f.Run("element.setParam", new JsonObject { ["id"] = wall, ["pset"] = "Pset_DoorCommon", ["name"] = "FireRating", ["value"] = "EI30" }));
        Assert.Throws<CommandException>(() => f.Run("element.setParam", new JsonObject { ["id"] = door, ["pset"] = "Pset_MyOwn", ["name"] = "X", ["kind"] = "text", ["value"] = "y" }));
        Assert.Throws<CommandException>(() => f.Run("element.setParam", new JsonObject { ["id"] = door, ["pset"] = "Qto_DoorBaseQuantities", ["name"] = "Width", ["kind"] = "length", ["value"] = 1 }));
        Assert.Throws<CommandException>(() => f.Run("element.setParam", new JsonObject { ["id"] = door, ["pset"] = "Lapin_Intent", ["name"] = "X", ["kind"] = "text", ["value"] = "y" }));
        Assert.Null(f.Model.Get(door)!.Attrs["params"]);
    }

    [Fact]
    public void The_same_name_in_two_sets_is_two_parameters_and_each_is_removed_on_its_own()
    {
        var (f, door) = WithDoor();
        f.Run("element.setParam", new JsonObject { ["id"] = door, ["name"] = "FireRating", ["kind"] = "text", ["value"] = "mine" });
        f.Run("element.setParam", new JsonObject { ["id"] = door, ["pset"] = "Pset_DoorCommon", ["name"] = "FireRating", ["value"] = "EI30" });
        f.Run("element.setParam", new JsonObject { ["id"] = door, ["pset"] = "Pset_DoorCommon", ["name"] = "FireRating", ["value"] = "EI60" });
        Assert.Equal(2, Params(f, door).Count);
        f.Run("element.removeParam", new JsonObject { ["id"] = door, ["pset"] = "Pset_DoorCommon", ["name"] = "FireRating" });
        Assert.Equal("mine", (string)Assert.Single(Params(f, door))!["value"]!);
    }

    [Fact]
    public void Several_values_in_a_batch_are_one_undo_step()
    {
        var (f, door) = WithDoor();
        var steps = new JsonArray(
            new JsonObject { ["command"] = "element.setParam", ["args"] = new JsonObject { ["id"] = door, ["pset"] = "Pset_DoorCommon", ["name"] = "FireRating", ["value"] = "EI30" } },
            new JsonObject { ["command"] = "element.setParam", ["args"] = new JsonObject { ["id"] = door, ["pset"] = "Pset_DoorCommon", ["name"] = "SmokeStop", ["value"] = true } });
        f.Run("edit.batch", new JsonObject { ["steps"] = steps });
        Assert.Equal(2, Params(f, door).Count);
        f.Session.Undo();
        Assert.Null(f.Model.Get(door)!.Attrs["params"]);
    }

    [Fact]
    public void The_table_is_offered_to_the_interface()
    {
        var f = new Fixture();
        var t = (JsonObject)f.Session.Query("params.standardSets", [])!;
        var door = ((JsonArray)t["sets"]!).OfType<JsonObject>().Single(s => (string?)s["name"] == "Pset_DoorCommon");
        Assert.Equal("text", (string?)door["properties"]!["FireRating"]);
        Assert.Equal("Lapin_Parameters", (string?)t["own"]);
    }
}
