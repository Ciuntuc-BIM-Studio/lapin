using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// One list of drafting layers (forum #168, decision 714): a layer made by the layer command, Draft → Layers or Manage → Drafting
/// standards is the same layer, with the same colour, pen, line type and switches, and drafted shapes draw by it. An older
/// project's standards' own layers merge into it on open; a .lapinstandards file carries it.
/// </summary>
public class DraftLayerListTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static JsonObject Book(Fixture f) => (JsonObject)f.Session.Query("standards.get", new JsonObject())!;
    static List<JsonObject> StdLayers(Fixture f) => Book(f)["layers"]!.AsArray().OfType<JsonObject>().ToList();
    static JsonObject? Pen(Fixture f, string view, string layer) =>
        ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!)["pens"]!.AsArray().OfType<JsonObject>().FirstOrDefault(p => (string?)p["layer"] == layer);

    [Fact]
    public void A_layer_made_with_the_layer_command_is_in_the_standards_and_an_edit_there_changes_how_its_lines_draw()
    {
        var f = F();
        var plan = Plan(f);
        // Fernando's: `layer`, named LINE 0.6.
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "LINE 0.6" });
        var line = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["layer"] = "LINE 0.6", ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = J(0, 0), ["b"] = J(4, 0) }) })!["ids"]![0]!;
        var std = StdLayers(f).Single(l => (string)l["name"]! == "LINE 0.6");
        Assert.Equal("LINE 0.6", (string)std["id"]!);
        Assert.Equal(0.25, (double)std["lineWeight"]!);
        Assert.Contains(StdLayers(f), l => (string)l["id"]! == "0");

        // Its pen and colour set in Drafting standards: the line draws by them, and Draft → Layers shows them. One undo step.
        f.Run("standards.set", new JsonObject { ["kind"] = "layer", ["entry"] = new JsonObject { ["id"] = "LINE 0.6", ["lineWeight"] = 0.6, ["color"] = "#c0392b", ["lineType"] = "dashed" } });
        var pen = Pen(f, plan, "LINE 0.6")!;
        Assert.Equal(0.6, (double)pen["width"]!);
        Assert.Equal("#c0392b", (string)pen["stroke"]!);
        Assert.NotNull(pen["dash"]);
        var drafted = Drafts.Layers(f.Model).Single(l => l.Name == "LINE 0.6");
        Assert.Equal((0.6, "#c0392b", "dashed"), (drafted.Weight, drafted.Color, drafted.LineType));
        Assert.Single(Drafts.Layers(f.Model), l => l.Name == "LINE 0.6");
        f.Session.Undo();
        Assert.Equal(0.25, Drafts.LayerOf(f.Model, "LINE 0.6").Weight);
        f.Session.Redo();

        // Switched off there: not drawn. Renamed there: its line follows. Removed there: its line goes to layer 0.
        f.Run("standards.set", new JsonObject { ["kind"] = "layer", ["entry"] = new JsonObject { ["id"] = "LINE 0.6", ["visible"] = false } });
        Assert.Null(Pen(f, plan, "LINE 0.6"));
        f.Run("standards.set", new JsonObject { ["kind"] = "layer", ["entry"] = new JsonObject { ["id"] = "LINE 0.6", ["visible"] = true, ["name"] = "LINE 0.60" } });
        Assert.Equal("LINE 0.60", f.Model.Get(line)!.Str("layer"));
        Assert.Equal(0.6, Drafts.LayerOf(f.Model, "LINE 0.60").Weight);
        Assert.DoesNotContain(Drafts.Layers(f.Model), l => l.Name == "LINE 0.6");
        Assert.Throws<CommandException>(() => f.Run("standards.set", new JsonObject { ["kind"] = "layer", ["entry"] = new JsonObject { ["id"] = "0", ["name"] = "Zero" } }));
        f.Run("standards.remove", new JsonObject { ["kind"] = "layer", ["id"] = "LINE 0.60" });
        Assert.Equal("0", f.Model.Get(line)!.Str("layer"));
        Assert.DoesNotContain(Drafts.Layers(f.Model), l => l.Name == "LINE 0.60");
    }

    [Fact]
    public void A_layer_made_in_the_standards_is_a_drafting_layer_and_its_group_hides_it()
    {
        var f = F();
        var plan = Plan(f);
        var id = (string)f.Run("standards.set", new JsonObject { ["kind"] = "layer", ["entry"] = new JsonObject { ["name"] = "Layer 2", ["lineWeight"] = 0.5 } })!["id"]!;
        Assert.Equal("Layer 2", id);
        Assert.Equal(0.5, Drafts.LayerOf(f.Model, "Layer 2").Weight);
        // A second New of the same name takes a free one.
        Assert.Equal("Layer 2 (2)", (string)f.Run("standards.set", new JsonObject { ["kind"] = "layer", ["entry"] = new JsonObject { ["name"] = "Layer 2" } })!["id"]!);
        // The layer command keeps its group.
        var group = (string)f.Run("standards.set", new JsonObject { ["kind"] = "layerGroup", ["entry"] = new JsonObject { ["name"] = "Details" } })!["id"]!;
        f.Run("standards.set", new JsonObject { ["kind"] = "layer", ["entry"] = new JsonObject { ["id"] = "Layer 2", ["group"] = group } });
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "Layer 2", ["color"] = "#2266aa" });
        Assert.Equal(group, Drafts.LayerOf(f.Model, "Layer 2").Group);
        f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["layer"] = "Layer 2", ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = J(0, 0), ["b"] = J(4, 0) }) });
        Assert.NotNull(Pen(f, plan, "Layer 2"));
        f.Run("standards.set", new JsonObject { ["kind"] = "layerGroup", ["entry"] = new JsonObject { ["id"] = group, ["visible"] = false } });
        Assert.Null(Pen(f, plan, "Layer 2"));
        // Other standards' edits leave the layers as stored.
        var before = f.Model.Get(Spatial.Project(f.Model)!.Id)!.Attrs[DraftLayers.Key]!.ToJsonString();
        f.Run("standards.options", new JsonObject { ["units"] = "cm" });
        Assert.Equal(before, Spatial.Project(f.Model)!.Attrs[DraftLayers.Key]!.ToJsonString());
        Assert.Null(Spatial.Project(f.Model)!.Attrs["drafting"]!["layers"]);
    }

    [Fact]
    public void An_older_project_s_standards_layers_merge_into_the_drafting_layers_the_drafting_layer_winning()
    {
        var f = F();
        var project = Spatial.Project(f.Model)!;
        var attrs = project.CloneAttrs();
        attrs["draftLayers"] = new JsonArray(
            new JsonObject { ["name"] = "LINE 0.6", ["color"] = "#ff0000", ["weight"] = 0.6, ["lineType"] = "continuous", ["visible"] = true, ["locked"] = false },
            new JsonObject { ["name"] = "A-DXF", ["color"] = "#00ff00", ["weight"] = 0.18, ["lineType"] = "HIDDEN2", ["visible"] = true, ["locked"] = false });
        attrs["drafting"] = new JsonObject
        {
            ["layerGroups"] = new JsonArray(new JsonObject { ["id"] = "g1", ["name"] = "Pens", ["visible"] = true, ["locked"] = false }),
            ["layers"] = new JsonArray(
                new JsonObject { ["id"] = "0", ["name"] = "0", ["color"] = "#18181b", ["visible"] = true, ["locked"] = false },
                // Both lists hold it (its name in other letters): the drafting layer's values stay, it takes the group.
                new JsonObject { ["id"] = "layer-1a2b3c4d", ["name"] = "line 0.6", ["color"] = "#0000ff", ["lineWeight"] = 0.35, ["group"] = "g1", ["visible"] = true, ["locked"] = false },
                // Only the standards held it: added with its values.
                new JsonObject { ["id"] = "layer-5e6f7a8b", ["name"] = "Hatches", ["color"] = "#888888", ["lineWeight"] = 0.13, ["lineType"] = "dotted", ["visible"] = false, ["locked"] = true }),
        };
        var old = project.With(attrs: attrs);
        var (es, _) = new DraftLayers.ListUpgrade().Upgrade([.. f.Model.Entities.Select(e => e.Id == project.Id ? old : e)], []);
        var up = es.Single(e => e.Class == IfcClass.Project);
        Assert.Null(up.Attrs["drafting"]!["layers"]);
        Assert.NotNull(up.Attrs["drafting"]!["layerGroups"]);
        var list = up.Attrs["draftLayers"]!.AsArray().OfType<JsonObject>().Select(CadLayer.From).OfType<CadLayer>().ToList();
        Assert.Equal(["LINE 0.6", "A-DXF", "Hatches"], list.Select(l => l.Name));
        Assert.Equal(new CadLayer("LINE 0.6", "#ff0000", 0.6, "continuous", true, false, "g1"), list[0]);
        Assert.Equal("HIDDEN2", list[1].LineType);
        Assert.Equal(new CadLayer("Hatches", "#888888", 0.13, "dotted", false, true), list[2]);
        // Once.
        var (again, _) = new DraftLayers.ListUpgrade().Upgrade(es, []);
        Assert.Same(es.Single(e => e.Class == IfcClass.Project), again.Single(e => e.Class == IfcClass.Project));
    }

    [Fact]
    public void A_standards_file_carries_the_drafting_layers_and_an_older_file_s_layers_come_in_by_name()
    {
        var f = F();
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "LINE 0.6", ["color"] = "#c0392b", ["weight"] = 0.6, ["lineType"] = "dashed" });
        var text = (string)f.Session.Query("standards.export", new JsonObject())!["text"]!;
        var g = F();
        g.Run("drafting.setLayer", new JsonObject { ["name"] = "Mine", ["weight"] = 0.7 });
        g.Run("standards.importFrom", new JsonObject { ["text"] = text });
        var got = Drafts.LayerOf(g.Model, "LINE 0.6");
        Assert.Equal(("LINE 0.6", "#c0392b", 0.6, "dashed"), (got.Name, got.Color, got.Weight, got.LineType));
        Assert.Equal(0.7, Drafts.LayerOf(g.Model, "Mine").Weight);
        // Read again: the same, nothing to update.
        var pack = (JsonObject)g.Session.Query("standards.readPack", new JsonObject { ["text"] = text })!;
        Assert.All(pack["entries"]!.AsArray().OfType<JsonObject>().Where(e => (string?)e["kind"] == "layer"), e => Assert.True((bool)e["same"]!));

        // A file written before decision 714: its layers by their standards' ids; they come in by name onto the one list.
        var older = JsonNode.Parse(text)!.AsObject();
        older["drafting"]!["layers"] = new JsonArray(
            new JsonObject { ["id"] = "layer-1a2b3c4d", ["name"] = "line 0.6", ["color"] = "#0000ff", ["lineWeight"] = 0.35, ["visible"] = true, ["locked"] = false },
            new JsonObject { ["id"] = "layer-5e6f7a8b", ["name"] = "Hatches", ["color"] = "#888888", ["lineWeight"] = 0.13, ["visible"] = true, ["locked"] = false });
        g.Run("standards.importFrom", new JsonObject { ["text"] = older.ToJsonString() });
        // Updated, the project's name kept (its shapes name it); the new one added.
        got = Drafts.LayerOf(g.Model, "LINE 0.6");
        Assert.Equal(("LINE 0.6", "#0000ff", 0.35), (got.Name, got.Color, got.Weight));
        Assert.Equal(0.13, Drafts.LayerOf(g.Model, "Hatches").Weight);
        Assert.DoesNotContain(Drafts.Layers(g.Model), l => l.Name == "line 0.6");
        Assert.Equal(0.25, Drafts.LayerOf(g.Model, "0").Weight);

        // A whole file loaded (standards.replace): its layers laid over the project's, the project's others kept.
        g.Run("standards.replace", new JsonObject { ["standards"] = JsonNode.Parse(text)!["drafting"]!.DeepClone() });
        Assert.Equal(0.6, Drafts.LayerOf(g.Model, "LINE 0.6").Weight);
        Assert.Contains(Drafts.Layers(g.Model), l => l.Name == "Mine");
    }
}
