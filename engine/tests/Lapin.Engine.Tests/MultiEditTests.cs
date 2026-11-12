using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Several elements selected and one field changed (a forum user's report, topic #29: 23 beams given a phase, only the first took
/// it): the change goes to every one of them, as one undo step. And Match properties (decision 490): what one element carries
/// given to others in one edit.batch, so one undo step, a pinned element's parameters too (decision 449).
/// </summary>
public class MultiEditTests
{
    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());
    static JsonObject Step(string command, JsonObject args) => new() { ["command"] = command, ["args"] = args };
    static double Num(Fixture f, string id, string key) => (double)f.Model.Get(id)!.Attrs[key]!;
    static string? Str(Fixture f, string id, string key) => (string?)f.Model.Get(id)!.Attrs[key];
    static string? TypeId(Fixture f, string id) => f.Model.RelatingOf(id, IfcClass.RelDefinesByType)?.Id;
    static double Sill(Fixture f, string fill) => (double)f.Model.RelatingOf(fill, IfcClass.RelFillsElement)!.Attrs["sill"]!;

    /// <summary>A second door or window type: the first's spec, wider.</summary>
    static string Wider(Fixture f, string fill)
    {
        var spec = (JsonObject)f.Model.RelatingOf(fill, IfcClass.RelDefinesByType)!.Attrs["spec"]!.DeepClone();
        spec["width"] = 1.2;
        return (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec })!["id"]!;
    }

    [Fact]
    public void A_field_changed_with_three_walls_selected_changes_all_three_in_one_undo_step()
    {
        var f = new Fixture(new ArchitectureModule());
        string[] walls = [f.Wall(0, 0, 4, 0), f.Wall(0, 3, 4, 3), f.Wall(0, 6, 4, 6)];
        string?[] Of(string key) => walls.Select(w => f.Model.Get(w)!.Attrs[key]?.ToJsonString()).ToArray();
        var (heights, offsets) = (Of("height"), Of("baseOffset"));
        f.Run("element.update", new JsonObject { ["ids"] = Ids(walls), ["attrs"] = new JsonObject { ["height"] = 3.3, ["baseOffset"] = 0.1 } });
        Assert.All(walls, w => { Assert.Equal(3.3, Num(f, w, "height"), 9); Assert.Equal(0.1, Num(f, w, "baseOffset"), 9); });
        f.Session.Undo();
        Assert.Equal(heights, Of("height"));
        Assert.Equal(offsets, Of("baseOffset"));
    }

    [Fact]
    public void A_phase_given_to_walls_doors_and_a_window_selected_together_goes_to_each_in_one_undo_step()
    {
        var f = new Fixture(new ArchitectureModule(), new PhasesModule());
        var a = f.Wall(0, 0, 6, 0);
        var b = f.Wall(0, 4, 6, 4);
        var door1 = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = a, ["along"] = 1.5 })!["id"]!;
        var door2 = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = b, ["along"] = 1.5 })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = a, ["along"] = 4 })!["id"]!;
        string[] all = [a, b, door1, door2, window];
        f.Run("phases.assign", new JsonObject { ["ids"] = Ids(all), ["created"] = "existing", ["demolished"] = "new-construction" });
        Assert.All(all, id => { Assert.Equal("existing", Str(f, id, "phaseCreated")); Assert.Equal("new-construction", Str(f, id, "phaseDemolished")); });
        f.Session.Undo();
        Assert.All(all, id => { Assert.Null(Str(f, id, "phaseCreated")); Assert.Null(Str(f, id, "phaseDemolished")); });
    }

    [Fact]
    public void A_type_sill_and_hand_given_to_doors_and_windows_selected_together_reach_each_one()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 10, 0);
        var doors = new[] { 1.5, 4.0 }.Select(x => (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = x })!["id"]!).ToArray();
        var windows = new[] { 6.5, 8.5 }.Select(x => (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = x, ["sill"] = 0.9 })!["id"]!).ToArray();
        var wideDoor = Wider(f, doors[0]);
        f.Run("architecture.setJoineryType", new JsonObject { ["ids"] = Ids(doors), ["typeId"] = wideDoor });
        Assert.All(doors, d => Assert.Equal(wideDoor, TypeId(f, d)));
        f.Run("architecture.moveOpening", new JsonObject { ["ids"] = Ids(windows), ["sill"] = 1.1 });
        Assert.All(windows, w => Assert.Equal(1.1, Sill(f, w), 9));
        f.Session.Undo();
        Assert.All(windows, w => Assert.Equal(0.9, Sill(f, w), 9));
        Assert.All(doors, d => Assert.Equal(wideDoor, TypeId(f, d)));
        f.Session.Undo();
        Assert.All(doors, d => Assert.NotEqual(wideDoor, TypeId(f, d)));
    }

    [Fact]
    public void Match_properties_gives_the_type_fields_parameters_and_phases_of_a_wall_to_others_as_one_undo_step_pinned_too()
    {
        var f = new Fixture(new ArchitectureModule(), new PhasesModule());
        var thick = f.Type("Brick 30", ("Brick", 0.3, true));
        var source = f.Wall(0, 0, 4, 0, thick);
        f.Run("element.update", new JsonObject { ["id"] = source, ["attrs"] = new JsonObject { ["height"] = 3.1, ["baseOffset"] = -0.2 } });
        f.Run("element.setParam", new JsonObject { ["id"] = source, ["name"] = "Fire rating", ["kind"] = "text", ["value"] = "EI 60" });
        f.Run("phases.assign", new JsonObject { ["id"] = source, ["created"] = "existing" });
        var thin = f.Type("Block 20", ("Block", 0.2, true));
        var t1 = f.Wall(0, 3, 4, 3, thin);
        var t2 = f.Wall(0, 6, 4, 6, thin);
        f.Run("element.pin", new JsonObject { ["ids"] = Ids(t2), ["pinned"] = true });
        var ends = f.Model.Get(t2)!.Attrs["end"]!.ToJsonString();
        // What the interface sends for one click on each target (core/matchProperties.ts): one batch.
        JsonObject Batch(string t) => new()
        {
            ["steps"] = new JsonArray(
                Step("architecture.setWallType", new JsonObject { ["ids"] = Ids(t), ["typeId"] = thick }),
                Step("element.update", new JsonObject { ["ids"] = Ids(t), ["attrs"] = new JsonObject { ["height"] = 3.1, ["baseOffset"] = -0.2 } }),
                Step("element.setParam", new JsonObject { ["ids"] = Ids(t), ["name"] = "Fire rating", ["kind"] = "text", ["value"] = "EI 60" }),
                Step("phases.assign", new JsonObject { ["ids"] = Ids(t), ["created"] = "existing", ["demolished"] = null })),
        };
        f.Run("edit.batch", Batch(t1));
        f.Run("edit.batch", Batch(t2));
        foreach (var t in new[] { t1, t2 })
        {
            Assert.Equal(thick, TypeId(f, t));
            Assert.Equal(3.1, Num(f, t, "height"), 9);
            Assert.Equal(-0.2, Num(f, t, "baseOffset"), 9);
            Assert.Equal("existing", Str(f, t, "phaseCreated"));
            Assert.Contains(f.Model.Get(t)!.Attrs["params"]!.AsArray(), p => (string?)p!["name"] == "Fire rating" && (string?)p["value"] == "EI 60");
        }
        // The pinned wall kept where it stands.
        Assert.Equal(ends, f.Model.Get(t2)!.Attrs["end"]!.ToJsonString());
        // One click, one undo step: the second wall back as it was, the first still matched.
        f.Session.Undo();
        Assert.Equal(thin, TypeId(f, t2));
        Assert.Null(f.Model.Get(t2)!.Attrs["params"]);
        Assert.Null(Str(f, t2, "phaseCreated"));
        Assert.Equal(thick, TypeId(f, t1));
        Assert.Equal("existing", Str(f, t1, "phaseCreated"));
    }

    [Fact]
    public void Match_properties_gives_a_doors_type_frame_position_and_parameters_to_another_door_and_its_sill_to_a_window()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 10, 0);
        var source = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 })!["id"]!;
        var target = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 4.5 })!["id"]!;
        var wide = Wider(f, source);
        f.Run("architecture.setJoineryType", new JsonObject { ["ids"] = Ids(source), ["typeId"] = wide });
        f.Run("architecture.setJoineryPosition", new JsonObject { ["ids"] = Ids(source), ["from"] = "exterior", ["distance"] = 0.05 });
        f.Run("element.setParam", new JsonObject { ["id"] = source, ["pset"] = "Pset_DoorCommon", ["name"] = "FireRating", ["value"] = "EI 30" });
        f.Run("edit.batch", new JsonObject
        {
            ["steps"] = new JsonArray(
                Step("architecture.setJoineryType", new JsonObject { ["ids"] = Ids(target), ["typeId"] = wide }),
                Step("architecture.setJoineryPosition", new JsonObject { ["ids"] = Ids(target), ["from"] = "exterior", ["distance"] = 0.05 }),
                Step("element.setParam", new JsonObject { ["ids"] = Ids(target), ["pset"] = "Pset_DoorCommon", ["name"] = "FireRating", ["kind"] = "text", ["value"] = "EI 30" })),
        });
        Assert.Equal(wide, TypeId(f, target));
        Assert.Equal("exterior", (string?)f.Model.Get(target)!.Attrs["position"]!["from"]);
        Assert.Contains(f.Model.Get(target)!.Attrs["params"]!.AsArray(), p => (string?)p!["pset"] == "Pset_DoorCommon" && (string?)p["value"] == "EI 30");
        // Where it stands in its wall is its own.
        Assert.NotEqual((double)f.Model.RelatingOf(source, IfcClass.RelFillsElement)!.Attrs["along"]!, (double)f.Model.RelatingOf(target, IfcClass.RelFillsElement)!.Attrs["along"]!, 9);
        f.Session.Undo();
        Assert.NotEqual(wide, TypeId(f, target));
        Assert.Null(f.Model.Get(target)!.Attrs["position"]);
    }
}
