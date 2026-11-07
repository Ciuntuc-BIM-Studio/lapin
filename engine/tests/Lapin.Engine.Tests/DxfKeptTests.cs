using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Host;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// A tester's report #113 („every time I close and reopen Lapin, I have to import and group the floor plans again”):
/// an imported DXF, grouped, saved and opened again as the interface does it, comes back whole.
/// </summary>
public class DxfKeptTests
{
    static string Dxf(string entities, string tables) => string.Join("\n", new[]
    {
        "0", "SECTION", "2", "HEADER", "9", "$INSUNITS", "70", "4", "0", "ENDSEC",
        "0", "SECTION", "2", "TABLES", tables, "0", "ENDSEC",
        "0", "SECTION", "2", "ENTITIES", entities, "0", "ENDSEC", "0", "EOF",
    });

    const string Tables = "0\nTABLE\n2\nLAYER\n0\nLAYER\n2\nWALLS\n62\n1\n70\n0\n0\nLAYER\n2\nTEXT\n62\n3\n70\n0\n0\nENDTAB";

    const string Entities =
        "0\nLINE\n8\nWALLS\n10\n0\n20\n0\n11\n5000\n21\n0\n" +
        "0\nLINE\n8\nWALLS\n10\n5000\n20\n0\n11\n5000\n21\n4000\n" +
        "0\nCIRCLE\n8\nWALLS\n10\n2000\n20\n2000\n40\n300\n" +
        "0\nSOLID\n8\nFILL\n10\n0\n20\n0\n11\n1000\n21\n0\n12\n0\n22\n1000\n13\n1000\n23\n1000\n" +
        "0\nTEXT\n8\nTEXT\n10\n100\n20\n100\n40\n250\n1\nLiving";

    /// <summary>A call as the interface makes it (File → Save, File → Open).</summary>
    static void Call(EngineDispatcher d, string method, JsonObject p)
    {
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
        Assert.DoesNotContain("\"error\"", Encoding.UTF8.GetString(reply));
    }

    [Fact]
    public void An_imported_and_grouped_dxf_is_saved_and_opened_again_whole()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var r = (JsonObject)f.Run("drafting.importDxf",
            new JsonObject { ["viewId"] = plan, ["name"] = "ground floor.dxf", ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Dxf(Entities, Tables))) })!;
        var importId = (string)r["importId"]!;
        // The import is an edit: the project has unsaved changes, so closing asks to save them.
        Assert.True(f.Session.History.Dirty);
        var drafted = Drafts.On(f.Model, plan).Select(e => e.Id).ToList();
        Assert.True(drafted.Count >= 4);
        var group = (string)f.Run("group.create", new JsonObject { ["ids"] = new JsonArray([.. drafted.Select(x => (JsonNode)x)]), ["name"] = "Ground floor plan" })!["id"]!;

        var dir = Directory.CreateTempSubdirectory("lapin-dxf-kept");
        var path = Path.Combine(dir.FullName, "house.lapin");
        Call(d, "project.save", new JsonObject { ["path"] = path });
        Assert.False(f.Session.History.Dirty);

        // Lapin closed and opened again: a new engine opens the file.
        var g = new Fixture(new ArchitectureModule(), new DraftingModule());
        var e = new EngineDispatcher(g.Session, PackageFiles.Create());
        Call(e, "project.open", new JsonObject { ["path"] = path });
        Assert.False(g.Session.History.Dirty);

        // Every entity and relation as it was: the drafting, its layers, the hatch, the text, the group.
        foreach (var x in f.Model.Entities)
            Assert.True(g.Model.Get(x.Id) is { } y && ModelCompare.Same(x, y), $"{x.Class} {x.Id} {x.Name} changed or missing");
        foreach (var x in f.Model.Relations)
            Assert.True(g.Model.GetRelation(x.Id) is { } y && ModelCompare.Same(x, y), $"{x.Class} {x.Id} changed or missing");
        Assert.Equal(f.Model.Entities.Count(), g.Model.Entities.Count());

        Assert.Equal(drafted.Order(), Drafts.On(g.Model, plan).Select(x => x.Id).Order());
        Assert.Equal(drafted.Order(), Groups.MembersOf(g.Model, group).Order());
        Assert.Equal("Ground floor plan", g.Model.Get(group)!.Name);
        Assert.Equal("ground floor.dxf", (string?)Underlays3d.Imports(g.Model)[importId]!["file"]);
        Assert.Contains(Drafts.Layers(g.Model), l => l.Name == "WALLS" && l.Color == "#ff0000");
        // Still drawn in its plan and in 3D.
        Assert.NotEmpty(((JsonArray)g.Session.Query("drafting.underlays3d", [])!).OfType<JsonObject>());
        dir.Delete(true);
    }

    /// <summary>The same with a real drawing: <c>LAPIN_DXF_KEPT=&lt;file.dxf&gt;</c> (skipped without it).</summary>
    [Fact]
    public void A_real_dxf_is_kept_by_the_project_file()
    {
        if (Environment.GetEnvironmentVariable("LAPIN_DXF_KEPT") is not { Length: > 0 } file) return;
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("drafting.importDxf", new JsonObject { ["viewId"] = plan, ["name"] = Path.GetFileName(file), ["data"] = Convert.ToBase64String(File.ReadAllBytes(file)) });
        var drafted = Drafts.On(f.Model, plan).Select(e => e.Id).ToList();
        f.Run("group.create", new JsonObject { ["ids"] = new JsonArray([.. drafted.Select(x => (JsonNode)x)]) });
        var dir = Directory.CreateTempSubdirectory("lapin-dxf-kept");
        var path = Path.Combine(dir.FullName, "real.lapin");
        Call(d, "project.save", new JsonObject { ["path"] = path });
        var g = new Fixture(new ArchitectureModule(), new DraftingModule());
        Call(new EngineDispatcher(g.Session, PackageFiles.Create()), "project.open", new JsonObject { ["path"] = path });
        var lost = f.Model.Entities.Count(x => g.Model.Get(x.Id) is not { } y || !ModelCompare.Same(x, y));
        Assert.Equal(0, lost);
        Assert.Equal(f.Model.Relations.Count(), g.Model.Relations.Count());
        Console.WriteLine($"{drafted.Count} drafted, {new FileInfo(path).Length / 1024} KB");
        dir.Delete(true);
    }
}

/// <summary>A level's plan is its first floor plan view once it has one (decision 386): what lay on its default plan goes onto it.</summary>
public class StoreyPlanTests
{
    [Fact]
    public void Drafting_on_the_default_plan_moves_onto_the_levels_first_floor_plan_view()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var implicitPlan = StoreyPlans.Prefix + f.Storey;
        f.Run("drafting.draw", new JsonObject { ["viewId"] = implicitPlan, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(1, 0) }) });
        Assert.Single(Drafts.On(f.Model, implicitPlan));
        // A foundation plan is not the level's plan: nothing moves.
        var foundation = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["foundation"] = true })!["id"]!;
        Assert.True(f.Model.Get(foundation)!.Bool("foundation"));
        Assert.Single(Drafts.On(f.Model, implicitPlan));
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        Assert.Empty(Drafts.On(f.Model, implicitPlan));
        Assert.Single(Drafts.On(f.Model, plan));
        Assert.Equal(plan, StoreyPlans.Of(f.Model, f.Storey)!.Id);
        // A second plan view takes nothing.
        f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, 1), ["b"] = new JsonArray(1, 1) }) });
        var second = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Furniture" })!["id"]!;
        Assert.Empty(Drafts.On(f.Model, second));
        Assert.Equal(2, Drafts.On(f.Model, plan).Count());
        // One undo step: the view and the move.
        f.Session.Undo(f.Session.Author);
        f.Session.Undo(f.Session.Author);
        f.Session.Undo(f.Session.Author);
        Assert.Single(Drafts.On(f.Model, implicitPlan));
    }

    [Fact]
    public void A_project_saved_before_opens_with_its_default_plan_drafting_on_the_levels_plan_view()
    {
        var storey = new Entity("st", IfcClass.BuildingStorey, "Ground floor", []);
        var view = new Entity("v1", Views.Class, "Ground floor", new JsonObject { ["kind"] = "plan", ["storeyId"] = "st" });
        var site = new Entity("v0", Views.Class, "Site plan", new JsonObject { ["kind"] = "plan", ["storeyId"] = "st", ["site"] = "situation" });
        var line = new Entity("l1", IfcClass.Annotation, "Line", new JsonObject { ["viewId"] = "plan:st" });
        var other = new Entity("l2", IfcClass.Annotation, "Line", new JsonObject { ["viewId"] = "plan:elsewhere" });
        var (es, _) = new StoreyPlans.DefaultPlanUpgrade().Upgrade([storey, site, view, line, other], []);
        Assert.Equal("v1", es.Single(e => e.Id == "l1").Str("viewId"));
        Assert.Equal("plan:elsewhere", es.Single(e => e.Id == "l2").Str("viewId"));
        var (same, _) = new StoreyPlans.DefaultPlanUpgrade().Upgrade([storey, line], []);
        Assert.Equal("plan:st", same.Single(e => e.Id == "l1").Str("viewId"));
    }
}
