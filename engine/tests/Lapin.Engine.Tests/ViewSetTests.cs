using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Duplicating views, sheets and storeys, saved 3D views and the browser's
/// sets (docs/plan-views-and-joinery.md §1, pending decision 125).
/// </summary>
public class ViewSetTests
{
    static Fixture Full() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());
    static string Id(JsonNode? r) => (string)r!["id"]!;

    [Fact]
    public void A_view_duplicates_with_its_settings_and_with_detailing_its_annotations_too()
    {
        var f = Full();
        var s = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, -1), ["end"] = new JsonArray(0, 5), ["depth"] = 4, ["scale"] = 50, ["name"] = "A-A" }));
        f.Run("element.update", new JsonObject { ["id"] = s, ["attrs"] = new JsonObject { ["discipline"] = "Structure" } });
        f.Run("drafting.addDimension", new JsonObject { ["viewId"] = s, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(3, 0), ["at"] = new JsonArray(0, -0.5) });
        f.Run("drafting.addText", new JsonObject { ["viewId"] = s, ["at"] = new JsonArray(1, 1), ["text"] = "Note" });
        var plain = Id(f.Run("view.duplicate", new JsonObject { ["id"] = s }));
        var detailed = Id(f.Run("view.duplicate", new JsonObject { ["id"] = s, ["withDetailing"] = true }));
        var (p, d) = (f.Model.Get(plain)!, f.Model.Get(detailed)!);
        Assert.Equal("A-A copy", p.Name);
        Assert.Equal("A-A copy 2", d.Name);
        foreach (var v in new[] { p, d })
        {
            Assert.Equal("section", v.Str("kind"));
            Assert.Equal(4, v.Num("depth"));
            Assert.Equal(50, v.Num("scale"));
            Assert.Equal("Structure", v.Str("discipline"));
            Assert.Equal("[0,5]", v.Attrs["end"]!.ToJsonString());
        }
        int Annotations(string viewId) => f.Model.OfClass(IfcClass.Annotation).Count(x => x.Str("viewId") == viewId);
        Assert.Equal(0, Annotations(plain));
        Assert.Equal(2, Annotations(detailed));
        Assert.Equal(2, Annotations(s));
        f.Session.Undo();
        Assert.Null(f.Model.Get(detailed));
        Assert.Equal(2, Annotations(s));
    }

    [Fact]
    public void The_implicit_plan_of_a_storey_duplicates_as_a_plan_of_its_own_and_3D_as_a_saved_3D_view()
    {
        var f = Full();
        var plan = f.Model.Get(Id(f.Run("view.duplicate", new JsonObject { ["id"] = "plan:" + f.Storey })))!;
        Assert.Equal("plan", plan.Str("kind"));
        Assert.Equal(f.Storey, plan.Str("storeyId"));
        Assert.Equal(Views.DefaultCut, plan.Num("cutHeight"));
        Assert.Equal("Ground floor copy", plan.Name);
        f.Run("view.graphics", new JsonObject { ["id"] = "3d", ["category"] = "walls", ["value"] = new JsonObject { ["hidden"] = true } });
        var v3 = f.Model.Get(Id(f.Run("view.duplicate", new JsonObject { ["id"] = "3d", ["name"] = "Structure only" })))!;
        Assert.Equal("3d", v3.Str("kind"));
        Assert.True((bool)v3.Attrs["graphics"]!["categories"]!["walls"]!["hidden"]!);
        var camera = new JsonObject { ["azimuth"] = 0.8, ["pitch"] = -0.4, ["distance"] = 25, ["target"] = new JsonArray(3, 2, 1), ["ortho"] = true };
        f.Run("view.setCamera", new JsonObject { ["id"] = v3.Id, ["camera"] = camera.DeepClone() });
        Assert.Equal(camera.ToJsonString(), f.Model.Get(v3.Id)!.Attrs["camera"]!.ToJsonString());
        Assert.Throws<CommandException>(() => f.Run("view.setCamera", new JsonObject { ["id"] = "3d", ["camera"] = camera.DeepClone() }));
        var made = f.Model.Get(Id(f.Run("view.create", new JsonObject { ["kind"] = "3d", ["camera"] = camera.DeepClone() })))!;
        Assert.Equal("3D view 2", made.Name);
        Assert.Equal(25, Lapin.Model.Json.Number(made.Attrs["camera"]!["distance"]));
    }

    [Fact]
    public void A_sheet_duplicates_with_its_paper_and_title_block_the_next_number_and_no_views()
    {
        var f = Full();
        var sheet = Id(f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A2", ["orientation"] = "portrait", ["discipline"] = "Architecture" }));
        f.Run("sheet.titleBlock", new JsonObject { ["id"] = sheet, ["fields"] = new JsonObject { ["drawnBy"] = "IC" } });
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = "plan:" + f.Storey });
        var copy = f.Model.Get(Id(f.Run("sheet.duplicate", new JsonObject { ["id"] = sheet })))!;
        Assert.Equal("Plans copy", copy.Name);
        Assert.Equal("A-102", copy.Str("number"));
        Assert.Equal("A2", copy.Str("paper"));
        Assert.Equal("portrait", copy.Str("orientation"));
        Assert.Equal("Architecture", copy.Str("discipline"));
        Assert.Equal("IC", (string?)copy.Attrs["titleBlock"]!["drawnBy"]);
        Assert.Empty(Sheets.Viewports(copy));
    }

    [Fact]
    public void A_storey_duplicates_with_its_contents_above_itself_the_storeys_above_moving_up()
    {
        var f = Full();
        var t = f.Type("W", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Render", 0.02, false));
        var w1 = f.Wall(0, 0, 6, 0, t);
        var w2 = f.Wall(6, 0, 6, 4, t);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = w1, ["along"] = 2 });
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 4), new JsonArray(0, 4)) });
        var top = Id(f.Run("storey.create", new JsonObject { ["name"] = "Roof level", ["elevation"] = 3.0, ["height"] = 2.5 }));
        var before = f.Model.Entities.Count();
        var r = f.Run("storey.duplicate", new JsonObject { ["id"] = f.Storey })!;
        var copy = f.Model.Get(Id(r))!;
        Assert.Equal("Ground floor copy", copy.Name);
        Assert.Equal(3.0, copy.Num("elevation"));
        Assert.Equal(3.0, copy.Num("height"));
        Assert.Equal(6.0, f.Model.Get(top)!.Num("elevation"));
        Assert.Equal(["Ground floor", "Ground floor copy", "Roof level"], Spatial.Storeys(f.Model).Select(s => s.Name));
        // Two walls and a slab copied, the door with its wall; all on the new storey.
        Assert.Equal(3, r["copied"]!.AsArray().Count);
        var on = f.Model.Entities.Where(e => Spatial.StoreyOf(f.Model, e.Id)?.Id == copy.Id).ToList();
        Assert.Equal(2, on.Count(e => e.Class == IfcClass.Wall));
        Assert.Equal(1, on.Count(e => e.Class == IfcClass.Slab));
        Assert.Equal(1, on.Count(e => e.Class == IfcClass.Door));
        // The copies are joined as the originals are, among themselves only.
        var walls = on.Where(e => e.Class == IfcClass.Wall).Select(e => e.Id).ToList();
        Assert.NotNull(Joins.Between(f.Model, walls[0], walls[1]));
        Assert.All(walls, id => Assert.All(Joins.Of(f.Model, id), j => Assert.Contains(Joins.Other(j, id), walls)));
        Assert.NotNull(Joins.Between(f.Model, w1, w2));
        // The copied wall stands on the new storey: its body is 3 m up.
        Assert.Equal(3.0, f.Bounds(walls[0]).Min.Z, 6);
        f.Session.Undo();
        Assert.Equal(before, f.Model.Entities.Count());
        Assert.Equal(3.0, f.Model.Get(top)!.Num("elevation"));
        var empty = f.Model.Get(Id(f.Run("storey.duplicate", new JsonObject { ["id"] = f.Storey, ["withContents"] = false })))!;
        Assert.DoesNotContain(f.Model.Entities, e => Spatial.StoreyOf(f.Model, e.Id)?.Id == empty.Id);
    }

    [Fact]
    public void The_browser_sets_default_to_the_disciplines_and_the_projects_own_round_trip_through_the_package()
    {
        var f = Full();
        var sets = (JsonObject)f.Session.Query("browser.sets", new JsonObject())!;
        Assert.False((bool)sets["own"]!);
        Assert.Equal(["Architecture", "Structure", "MEP"], sets["sets"]!.AsArray().Select(s => (string)s!["name"]!));
        var made = Id(f.Run("browser.setSet", new JsonObject { ["name"] = "Permit set", ["filter"] = new JsonObject { ["kinds"] = new JsonArray("plan", "section"), ["scales"] = new JsonArray(100), ["nameContains"] = "permit", ["params"] = new JsonObject { ["Stage"] = "DTAC" }, ["junk"] = 1 } }));
        f.Run("browser.setSet", new JsonObject { ["id"] = "mep", ["name"] = "Installations" });
        f.Run("browser.moveSet", new JsonObject { ["id"] = made, ["before"] = "architecture" });
        f.Run("browser.removeSet", new JsonObject { ["id"] = "structure" });
        sets = (JsonObject)f.Session.Query("browser.sets", new JsonObject())!;
        Assert.True((bool)sets["own"]!);
        Assert.Equal(["Permit set", "Architecture", "Installations"], sets["sets"]!.AsArray().Select(s => (string)s!["name"]!));
        var filter = sets["sets"]![0]!["filter"]!;
        Assert.Equal("""{"kinds":["plan","section"],"scales":["100"],"nameContains":"permit","params":{"Stage":"DTAC"}}""", filter.ToJsonString());
        Assert.Throws<CommandException>(() => f.Run("browser.setSet", new JsonObject { ["filter"] = new JsonObject() }));
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        ms.Position = 0;
        var content = LapinPackage.Read(ms);
        var project = content.Entities.Single(e => e.Class == IfcClass.Project);
        Assert.Equal(Spatial.Project(f.Model)!.Attrs["browserSets"]!.ToJsonString(), project.Attrs["browserSets"]!.ToJsonString());
    }
}
