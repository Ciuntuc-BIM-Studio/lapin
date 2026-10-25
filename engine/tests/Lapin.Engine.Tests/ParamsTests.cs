using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The user's own parameters on elements: set, changed, checked by kind, removed, on several at once.</summary>
public class ParamsTests
{
    static JsonArray Params(Fixture f, string id) => (JsonArray)f.Model.Get(id)!.Attrs["params"]!;

    [Fact]
    public void Set_change_check_and_remove()
    {
        var f = new Fixture(new ArchitectureModule());
        var (a, b) = (f.Wall(0, 0, 4, 0), f.Wall(0, 2, 4, 2));
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(a, b), ["name"] = "Finish height", ["kind"] = "length", ["value"] = 2.1 });
        Assert.Equal(2.1, (double)Params(f, b)[0]!["value"]!, 9);
        // Changed by name (any case), its kind kept.
        f.Run("element.setParam", new JsonObject { ["id"] = a, ["name"] = "finish HEIGHT", ["value"] = 2.4 });
        Assert.Single(Params(f, a));
        Assert.Equal("length", (string)Params(f, a)[0]!["kind"]!);
        Assert.Equal(2.4, (double)Params(f, a)[0]!["value"]!, 9);
        // Checked by kind.
        Assert.Throws<CommandException>(() => f.Run("element.setParam", new JsonObject { ["id"] = a, ["name"] = "Finish height", ["value"] = -1 }));
        Assert.Throws<CommandException>(() => f.Run("element.setParam", new JsonObject { ["id"] = a, ["name"] = "Fire", ["kind"] = "choice", ["options"] = new JsonArray("EI30"), ["value"] = "EI90" }));
        Assert.Throws<CommandException>(() => f.Run("element.setParam", new JsonObject { ["id"] = a, ["name"] = "New one", ["value"] = 1 }));
        Assert.Throws<CommandException>(() => f.Run("element.setParam", new JsonObject { ["id"] = a, ["name"] = "X", ["kind"] = "colour", ["value"] = 1 }));
        // Removed; undo brings it back.
        f.Run("element.removeParam", new JsonObject { ["ids"] = new JsonArray(a, b), ["name"] = "Finish height" });
        Assert.Null(f.Model.Get(a)!.Attrs["params"]);
        f.Session.Undo();
        Assert.Single(Params(f, a));
    }
}
