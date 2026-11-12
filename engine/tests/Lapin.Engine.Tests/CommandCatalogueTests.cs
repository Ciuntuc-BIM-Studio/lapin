using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Furniture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>
/// The command catalogue (decision 489, docs/python.md): what scripts/command-catalogue.mjs finds in the source, embedded and
/// served as session.catalogue; and edit.batch's own label, the name a script's batch gives its undo step.
/// </summary>
public class CommandCatalogueTests
{
    static ILapinModule[] Modules() =>
    [
        new ArchitectureModule(), new StructureModule(), new DraftingModule(), new CivilModule(), new Lapin.Phases.PhasesModule(), new StudioModule(),
        new WoodModule(), new SteelModule(), new FurnitureModule(), new Lapin.Mep.MepModule(), new Lapin.Rebar.RebarModule(), new Lapin.SiteWorks.SiteWorksModule(),
        new Lapin.Planning.PlanningModule(), new Lapin.Cost.CostModule(), new Lapin.Transport.TransportModule(), new Lapin.Clash.ClashModule(),
        new Lapin.Analysis.AnalysisModule(),
    ];

    static JsonObject Reply(EngineDispatcher d, string method, JsonObject p, Caller caller)
    {
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString(), caller);
        return JsonNode.Parse(BinaryFrame.HeaderText(reply))!.AsObject();
    }

    [Fact]
    public void Every_command_and_query_of_the_open_modules_is_in_the_source_catalogue()
    {
        var f = new Fixture(Modules());
        // A command added without a build of Lapin.Protocol (which writes the catalogue) shows here.
        var missing = f.Session.Catalog.Commands.Select(c => c.Name).Where(n => !CommandCatalogue.SourceCommands.Contains(n)).ToList();
        Assert.True(missing.Count == 0, $"Not in catalogue.json (run node scripts/command-catalogue.mjs): {string.Join(", ", missing)}");
        var queries = f.Session.Catalog.QueryNames.Where(n => !CommandCatalogue.SourceQueries.Contains(n)).ToList();
        Assert.True(queries.Count == 0, $"Not in catalogue.json (run node scripts/command-catalogue.mjs): {string.Join(", ", queries)}");
    }

    [Fact]
    public void Session_catalogue_lists_the_products_commands_with_their_arguments_label_and_module()
    {
        var f = new Fixture(new ArchitectureModule());
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var r = Reply(d, "session.catalogue", [], Caller.Local);
        Assert.Null((string?)r["error"]);
        var c = r["result"]!.AsObject();
        Assert.Equal(1, (int)c["format"]!);
        var commands = c["commands"]!.AsArray().Select(x => x!.AsObject()).ToList();
        // The product's own commands only: architecture and the platform's, nothing of a module it lacks.
        Assert.Equal(f.Session.Catalog.Commands.Count(), commands.Count);
        Assert.DoesNotContain(commands, x => ((string)x["name"]!).StartsWith("structure.", StringComparison.Ordinal));
        var wall = commands.Single(x => (string?)x["name"] == "architecture.addWall");
        Assert.Equal("architecture", (string?)wall["module"]);
        Assert.Equal("Wall", (string?)wall["label"]);
        var args = wall["args"]!.AsArray().ToDictionary(x => (string)x!["name"]!, x => (string?)x!["type"]);
        foreach (var k in new[] { "x1", "y1", "x2", "y2", "height" }) Assert.Equal("float", args[k]);
        Assert.Equal("str", args["storeyId"]);
        Assert.Equal("bool", args["join"]);
        // Keys read by the helper a handler hands its arguments to (Args.Ids).
        var delete = commands.Single(x => (string?)x["name"] == "element.delete")["args"]!.AsArray().Select(x => (string?)x!["name"]).ToList();
        Assert.Contains("ids", delete);
        Assert.Contains("id", delete);
        var batch = commands.Single(x => (string?)x["name"] == "edit.batch")["args"]!.AsArray().Select(x => (string?)x!["name"]).ToList();
        Assert.Contains("steps", batch);
        Assert.Contains(c["queries"]!.AsArray(), x => (string?)x!["name"] == "params.standardSets");
        // Static, read-only: a guest of a shared session may read it as they read session.info.
        Assert.Null(EngineDispatcher.AllowedRemote("session.catalogue", [], "viewer"));
    }

    [Fact]
    public void A_batch_named_by_its_label_is_one_undo_step_of_that_name()
    {
        var f = new Fixture(new ArchitectureModule());
        JsonObject Step(double x) => new() { ["command"] = "architecture.addWall", ["args"] = new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x, ["y1"] = 0, ["x2"] = x, ["y2"] = 3 } };
        var r = f.Run("edit.batch", new JsonObject { ["steps"] = new JsonArray(Step(0), Step(2)), ["label"] = "  Facades " })!.AsArray();
        Assert.Equal(2, r.Count);
        Assert.Equal("Facades", f.Session.History.UndoLabel);
        Assert.Equal(2, f.Model.OfClass(IfcClass.Wall).Count());
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(IfcClass.Wall));
        Assert.Equal("Facades", f.Session.History.RedoLabel);
        f.Session.Redo();
        // No label, or an empty one: the batch's own, "Edit"; a long one cut.
        f.Run("edit.batch", new JsonObject { ["steps"] = new JsonArray(Step(4)) });
        Assert.Equal("Edit", f.Session.History.UndoLabel);
        f.Run("edit.batch", new JsonObject { ["steps"] = new JsonArray(Step(6)), ["label"] = "   " });
        Assert.Equal("Edit", f.Session.History.UndoLabel);
        f.Run("edit.batch", new JsonObject { ["steps"] = new JsonArray(Step(8)), ["label"] = new string('x', 500) });
        Assert.Equal(Session.MaxBatchLabel, f.Session.History.UndoLabel!.Length);
        // A label that is not a text is refused, and nothing is made.
        var walls = f.Model.OfClass(IfcClass.Wall).Count();
        Assert.Throws<CommandException>(() => f.Run("edit.batch", new JsonObject { ["steps"] = new JsonArray(Step(10)), ["label"] = 3 }));
        Assert.Equal(walls, f.Model.OfClass(IfcClass.Wall).Count());
    }

    /// <summary>A module built in another repository: its commands' arguments from the catalogue its assembly carries.</summary>
    sealed class PremiumModule : ILapinModule
    {
        public string Id => "premium";
        public string Name => "Premium";
        public void Register(IModuleRegistry r)
        {
            r.Command("place", "Place", (ctx, a) => null);
            r.Query("report", (ctx, a) => null);
        }
    }

    [Fact]
    public void A_module_built_elsewhere_brings_its_own_catalogue_and_Lapins_own_entries_win()
    {
        var f = new Fixture(new ArchitectureModule(), new PremiumModule());
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var c = Reply(d, "session.catalogue", [], Caller.Local)["result"]!;
        var place = c["commands"]!.AsArray().Single(x => (string?)x!["name"] == "premium.place")!;
        Assert.Equal(["storeyId", "count"], place["args"]!.AsArray().Select(x => (string?)x!["name"]));
        var report = c["queries"]!.AsArray().Single(x => (string?)x!["name"] == "premium.report")!;
        Assert.Equal("premium", (string?)report["module"]);
        Assert.Equal(["id"], report["args"]!.AsArray().Select(x => (string?)x!["name"]));
        var wall = c["commands"]!.AsArray().Single(x => (string?)x!["name"] == "architecture.addWall")!;
        Assert.DoesNotContain("ignored", wall["args"]!.AsArray().Select(x => (string?)x!["name"]));
    }
}
