using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The session: commands in, diffs out; geometry rebuilt exactly where an edit reached.</summary>
public class SessionTests
{
    [Fact]
    public void A_new_project_has_its_spatial_structure_and_a_ground_floor_and_nothing_to_undo()
    {
        var f = new Fixture();
        Assert.Single(f.Model.OfClass(IfcClass.Project));
        Assert.Single(f.Model.OfClass(IfcClass.Building));
        var storey = Spatial.Storeys(f.Model).Single();
        Assert.Equal("Ground floor", storey.Name);
        Assert.Equal(IfcClass.Building, Spatial.ParentOf(f.Model, storey.Id)!.Class);
        Assert.False(f.Session.History.CanUndo);
        Assert.False(f.Session.History.Dirty);
    }

    [Fact]
    public void Adding_a_wall_sends_its_entities_and_its_layers_to_draw_and_undo_takes_them_back()
    {
        var f = new Fixture();
        var diff = f.RunDiff("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 0 });
        var item = Assert.Single(diff.Render);
        Assert.Equal(3, item.Parts!.Count);
        Assert.Equal(f.Storey, item.StoreyId);
        Assert.Contains(diff.Nodes, n => n.Type == IfcClass.Wall && n.StoreyId == f.Storey && n.Module == "architecture");
        Assert.Contains(diff.Nodes, n => n.Type == IfcClass.WallType);
        Assert.Contains(diff.Relations, r => r.Class == IfcClass.RelDefinesByType);
        Assert.True(diff.State.CanUndo);
        Assert.Equal("Wall", diff.State.UndoLabel);

        var undo = f.Session.Undo();
        Assert.Equal([item.NodeId], undo.RenderRemoved);
        Assert.Contains(item.NodeId, undo.RemovedNodes);
        Assert.Empty(f.Model.OfClass(IfcClass.Wall));
    }

    [Fact]
    public void An_edit_rebuilds_exactly_the_elements_that_read_what_it_changed()
    {
        var f = new Fixture();
        var a = f.Type("A", ("Brick", 0.25, true));
        var b = f.Type("B", ("Concrete", 0.2, true));
        var w1 = f.Wall(0, 0, 4, 0, a);
        var w2 = f.Wall(0, 3, 4, 3, a);
        var w3 = f.Wall(0, 6, 4, 6, b);

        // A changed type: its walls, not the other type's.
        var diff = f.RunDiff("architecture.updateWallType", new JsonObject { ["id"] = a, ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.3 }) });
        Assert.Equal(new[] { w1, w2 }.Order(), diff.Render.Select(r => r.NodeId).Order());

        // One wall moved: only that wall.
        diff = f.RunDiff("architecture.updateWall", new JsonObject { ["id"] = w3, ["x2"] = 5 });
        Assert.Equal([w3], diff.Render.Select(r => r.NodeId));

        // The storey's elevation: everything on it.
        diff = f.RunDiff("storey.update", new JsonObject { ["id"] = f.Storey, ["elevation"] = 1 });
        Assert.Equal(new[] { w1, w2, w3 }.Order(), diff.Render.Select(r => r.NodeId).Order());
        Assert.Equal(1, f.Bounds(w1).Min.Z, 9);
    }

    [Fact]
    public void A_class_no_module_of_the_product_owns_is_kept_and_shown_but_never_edited()
    {
        var full = new Fixture();
        var wall = full.Wall(0, 0, 4, 0);
        var m = (ModelStore)full.Model;

        // The same model opened by a product without architecture.
        var bare = new Fixture(new NoModule());
        bare.Session.Load(m.Entities.ToList(), m.Relations.ToList(), null, new Dictionary<string, string> { [IfcClass.Wall] = "architecture", [IfcClass.WallType] = "architecture" });
        Assert.NotNull(bare.Model.Get(wall));
        Assert.Equal("architecture", bare.Session.ModuleOf(IfcClass.Wall));
        Assert.Throws<CommandException>(() => bare.Run("element.delete", new JsonObject { ["id"] = wall }));
        Assert.Throws<CommandException>(() => bare.Run("element.rename", new JsonObject { ["id"] = wall, ["name"] = "x" }));
        var node = bare.Session.Snapshot().Nodes.Single(n => n.Id == wall);
        Assert.False(node.Editable);
        Assert.Null(node.Module);
    }

    [Fact]
    public void Unknown_commands_and_missing_storeys_are_refused()
    {
        var f = new Fixture();
        Assert.Throws<CommandException>(() => f.Run("architecture.nothing", []));
        Assert.Throws<CommandException>(() => f.Run("architecture.addWall", new JsonObject { ["storeyId"] = "nope", ["x1"] = 0, ["y1"] = 0, ["x2"] = 1, ["y2"] = 0 }));
    }

    [Fact]
    public void Storeys_stack_and_one_with_elements_is_not_deleted()
    {
        var f = new Fixture();
        var id = (string)f.Run("storey.create", [])!["id"]!;
        var s = f.Model.Get(id)!;
        Assert.Equal(3, s.Num("elevation"), 9);
        Assert.Equal("Level 1", s.Name);
        f.Wall(0, 0, 1, 0);
        Assert.Throws<CommandException>(() => f.Run("element.delete", new JsonObject { ["id"] = f.Storey }));
        f.Run("element.delete", new JsonObject { ["id"] = id });
        Assert.Null(f.Model.Get(id));
    }

    sealed class NoModule : ILapinModule
    {
        public string Id => "nothing";
        public string Name => "Nothing";
        public void Register(IModuleRegistry registry) { }
    }
}
