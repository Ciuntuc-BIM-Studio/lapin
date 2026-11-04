using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>What depends on what (docs/dependencies.md): read from what each element's geometry read, the same record an edit rebuilds by.</summary>
public sealed class DependencyTests
{
    static List<string> Ids(JsonNode? n) => n!.AsArray().Select(x => (string)x!).ToList();

    [Fact]
    public void Joined_walls_and_a_door_depend_on_each_other_and_an_edit_rebuilds_exactly_its_dependents()
    {
        var f = new Fixture(new ArchitectureModule());
        var a = f.Wall(0, 0, 6, 0);
        var b = f.Wall(6, 0, 6, 4);
        var alone = f.Wall(20, 0, 24, 0);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = a, ["along"] = 2 })!["id"]!;

        var ofA = f.Session.Query("core.dependents", new JsonObject { ["id"] = a })!;
        Assert.Equal(new[] { b, door }.Order(StringComparer.Ordinal), Ids(ofA["dependents"]));
        // Built from its type, its storey, the wall it joins and the door it holds; never from itself.
        var from = Ids(ofA["dependencies"]);
        Assert.Contains(b, from);
        Assert.Contains(door, from);
        Assert.Contains(f.Model.RelatingOf(a, IfcClass.RelDefinesByType)!.Id, from);
        Assert.Contains(f.Storey, from);
        Assert.DoesNotContain(a, from);
        Assert.DoesNotContain(alone, from);

        // The door: the wall it cuts. A wall standing alone: nothing.
        Assert.Equal([a], Ids(f.Session.Query("core.dependents", new JsonObject { ["id"] = door })!["dependents"]));
        Assert.Empty(Ids(f.Session.Query("core.dependents", new JsonObject { ["id"] = alone })!["dependents"]));

        // Several at once: what depends on any of them, they themselves left out.
        var both = f.Session.Query("core.dependents", new JsonObject { ["ids"] = new JsonArray(a, door) })!;
        Assert.Equal([b], Ids(both["dependents"]));
        Assert.Null(both["dependencies"]);

        // An edit of the door rebuilds the wall it depends on, and the standing wall is left alone.
        var diff = f.RunDiff("element.rename", new JsonObject { ["id"] = door, ["name"] = "D-01" });
        var rebuilt = diff.Render.Select(r => r.NodeId).ToHashSet();
        Assert.Contains(a, rebuilt);
        Assert.DoesNotContain(alone, rebuilt);

        // A deleted door is no longer a dependent of anything.
        f.Run("element.delete", new JsonObject { ["id"] = door });
        Assert.Equal([b], Ids(f.Session.Query("core.dependents", new JsonObject { ["id"] = a })!["dependents"]));
    }
}
