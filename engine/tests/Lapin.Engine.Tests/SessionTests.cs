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
    public void A_template_starts_a_new_project_with_its_own_levels_and_nothing_to_undo()
    {
        // Decision 261: the start screen's apartment block, five floors over the foundations.
        var f = new Fixture();
        var levels = Lapin.Protocol.EngineDispatcher.TemplateStoreys(JsonNode.Parse("""
            [{"name":"Foundations","elevation":-1.5,"height":1.5},{"name":"Ground floor","elevation":0,"height":3},
             {"name":"Floor 1","elevation":3,"height":3},{"name":"Floor 2","elevation":6,"height":3},
             {"name":"Floor 3","elevation":9,"height":3},{"name":"Floor 4","elevation":12,"height":3}]
            """));
        f.Session.New(storeys: levels);
        var storeys = Spatial.Storeys(f.Model).ToList();
        Assert.Equal(["Foundations", "Ground floor", "Floor 1", "Floor 2", "Floor 3", "Floor 4"], storeys.Select(x => x.Name));
        Assert.Equal(12.0, storeys[^1].Attrs["elevation"]!.GetValue<double>());
        Assert.False(f.Session.History.CanUndo);
        // A level without a name or a height, or none at all: the edition's own levels.
        Assert.Null(Lapin.Protocol.EngineDispatcher.TemplateStoreys(JsonNode.Parse("""[{"name":"","elevation":0,"height":3}]""")));
        Assert.Null(Lapin.Protocol.EngineDispatcher.TemplateStoreys(JsonNode.Parse("""[{"name":"A","elevation":0,"height":0}]""")));
        Assert.Null(Lapin.Protocol.EngineDispatcher.TemplateStoreys(JsonNode.Parse("[]")));
        f.Session.New();
        Assert.Equal(["Foundations", "Ground floor", "First floor"], Spatial.Storeys(f.Model).Select(x => x.Name));
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
    public void A_new_wall_on_a_storey_leaves_the_walls_already_there_alone()
    {
        var f = new Fixture();
        for (var i = 0; i < 5; i++) f.Wall(0, i, 4, i);
        var diff = f.RunDiff("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 9, ["x2"] = 4, ["y2"] = 9 });
        Assert.Single(diff.Render);
        Assert.Single(diff.Nodes, n => n.Type == IfcClass.Wall);
        // Undo takes only it away, too.
        var undo = f.Session.Undo();
        Assert.Single(undo.RenderRemoved);
        Assert.Empty(undo.Render);
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

/// <summary>The inspector's edits: element.update changes declared fields only, checked against them.</summary>
public class ElementUpdateTests
{
    [Fact]
    public void Changes_a_declared_field_and_the_geometry_follows()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 4, 0);
        f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["height"] = 2.5 } });
        Assert.Equal(2.5, f.Model.Get(wall)!.Num("height"), 9);
        Assert.Equal(2.5, f.Bounds(wall).Max.Z, 9);
        f.Run("element.update", new JsonObject { ["id"] = f.Storey, ["attrs"] = new JsonObject { ["elevation"] = -0.5 } });
        Assert.Equal(-0.5, f.Bounds(wall).Min.Z, 9);
    }

    [Fact]
    public void Refuses_unknown_fields_values_out_of_range_and_wrong_kinds()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 4, 0);
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["start"] = new JsonArray(1, 2) } }));
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["height"] = 0 } }));
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["height"] = "tall" } }));
        var brick = Materials.Find(f.Model, "Brick")!.Id;
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = brick, ["attrs"] = new JsonObject { ["color"] = "red" } }));
        f.Run("element.update", new JsonObject { ["id"] = brick, ["attrs"] = new JsonObject { ["color"] = "#ff0000" } });
        Assert.Equal("#ff0000", f.Session.Geometry(wall)!.Parts.Single(p => p.Material == brick).Color);
    }
}

/// <summary>A module's geometry with a bug in it: the element shows its problem; the edit, the diff and the undo history are untouched.</summary>
public class GeometryFailureTests
{
    [Fact]
    public void A_producer_that_crashes_marks_its_element_and_the_edit_stays_in_the_diff_the_model_and_the_history()
    {
        var f = new Fixture(new CursedModule());
        var diff = f.RunDiff("cursed.add", new JsonObject { ["name"] = "bad" });
        var item = Assert.Single(diff.Render);
        Assert.Contains(item.Diagnostics, d => d.Contains("NullReferenceException"));
        Assert.Empty(item.Parts!);
        Assert.Contains(diff.Nodes, n => n.Id == item.NodeId);
        Assert.NotNull(f.Model.Get(item.NodeId));
        Assert.True(diff.State.CanUndo);

        var undo = f.Session.Undo();
        Assert.Equal([item.NodeId], undo.RenderRemoved);
        Assert.Null(f.Model.Get(item.NodeId));
    }

    [Fact]
    public void A_snapshot_with_a_crashing_element_draws_the_others_and_marks_it()
    {
        var f = new Fixture(new CursedModule());
        var bad = (string)f.Run("cursed.add", new JsonObject { ["name"] = "bad" })!["id"]!;
        var good = (string)f.Run("cursed.add", new JsonObject { ["name"] = "good" })!["id"]!;
        var snapshot = f.Session.Snapshot();
        Assert.Equal(new[] { bad, good }.Order(), snapshot.Render.Select(r => r.NodeId).Order());
        Assert.NotEmpty(snapshot.Render.Single(r => r.NodeId == bad).Diagnostics);
        Assert.Empty(snapshot.Render.Single(r => r.NodeId == good).Diagnostics);
    }

    /// <summary>A module whose geometry producer throws for the element named "bad".</summary>
    sealed class CursedModule : ILapinModule
    {
        const string Class = "IfcCursed";
        public string Id => "cursed";
        public string Name => "Cursed";

        public void Register(IModuleRegistry r)
        {
            r.Class(new(Class, "Cursed", []));
            r.Command("add", "Cursed", (ctx, args) =>
            {
                var e = new Entity(ctx.NewId(), Class, (string?)args["name"] ?? "", []);
                ctx.Add(e);
                return new JsonObject { ["id"] = e.Id };
            });
            r.Geometry(Class, (e, _, _) => e.Name == "bad" ? throw new NullReferenceException("The producer's bug.") : new ElementGeometry([]));
        }
    }

    [Fact]
    public void A_cached_value_computed_through_the_cutters_still_reads_them_under_their_narrow_key()
    {
        var f = new Fixture(new ArchitectureModule(), new ProbeModule());
        var wall = f.Wall(0, 0, 4, 0);
        var p1 = (string)f.Run("probe.add", [])!["id"]!;
        var p2 = (string)f.Run("probe.add", [])!["id"]!;
        // One value for both (the second gets the first's reads): no wall ranks over them.
        Assert.Equal("0", f.Session.Geometry(p1)!.Diagnostics![0]);
        // The wall moved: neither is rebuilt (they read the walls' cut key, not the class).
        var diff = f.RunDiff("architecture.updateWall", new JsonObject { ["id"] = wall, ["x2"] = 5 });
        Assert.DoesNotContain(diff.Render, r => r.NodeId == p1 || r.NodeId == p2);
        // Raised over them: both are.
        diff = f.RunDiff("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["cutPriority"] = 80 } });
        Assert.Contains(diff.Render, r => r.NodeId == p1);
        Assert.Contains(diff.Render, r => r.NodeId == p2);
        Assert.Equal("1", f.Session.Geometry(p2)!.Diagnostics![0]);
    }

    /// <summary>A class that takes part in cutting, whose elements share one cached value: how many cutters rank over them.</summary>
    sealed class ProbeModule : ILapinModule
    {
        const string Class = "IfcProbe";
        static readonly RevisionCache<int> Shared = new();
        public string Id => "probe";
        public string Name => "Probe";

        public void Register(IModuleRegistry r)
        {
            r.Class(new(Class, "Probe", []));
            r.Command("add", "Probe", (ctx, args) =>
            {
                var e = new Entity(ctx.NewId(), Class, "", []);
                ctx.Add(e);
                return new JsonObject { ["id"] = e.Id };
            });
            r.Cutter(Class, 70, (_, _) => null);
            r.Geometry(Class, (e, m, services) =>
            {
                var project = Spatial.Project(m)!;
                var n = Shared.Get(project, m, v => services.Get<ICutters>()!.For(e, v, (new Lapin.Geometry.Vec3(-1e3, -1e3, -1e3), new Lapin.Geometry.Vec3(1e3, 1e3, 1e3))).Count);
                return new ElementGeometry([], Diagnostics: [n.ToString()]);
            });
        }
    }
}
