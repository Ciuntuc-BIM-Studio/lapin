using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Package;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The generator's contributors (docs/plan-extension-modules.md §6.3: an extension's procurement chains): a module adds tasks
/// and links after the steps, from its own part of the spec, which the generator keeps as it came; its tasks carry
/// <c>gen: "&lt;key&gt;/…"</c> and <c>genBy</c>, keep their ids when generated again, and stay with their links when the
/// schedule is generated again in an edition without the contributor, so a schedule does not depend on what is installed.
/// </summary>
public sealed class PlanningContributorTests
{
    /// <summary>A test contributor: an order task (its days from its part of the spec) before the first step's first task.</summary>
    sealed class Chain : IGeneratorContributor
    {
        public string Key => "chain";
        public JsonNode? Default => new JsonObject { ["on"] = false, ["days"] = 3 };

        public void Check(JsonNode? spec)
        {
            if (spec is JsonObject o && Json.Number(o["days"]) is { } d && d < 0) throw new CommandException("A chain's days are not negative.");
        }

        public void Contribute(GenContext ctx, JsonNode? spec)
        {
            if (spec is not JsonObject o || o["on"] is not JsonValue on || !on.GetValue<bool>()) return;
            var days = (int)(Json.Number(o["days"]) ?? 3);
            var first = ctx.Steps.FirstOrDefault(s => s.ByStorey.Count > 0 && s.Step.Classes.Count > 0)?.Step.Key;
            var summary = ctx.Task("", "Procurement", null, 0.5, new JsonObject { ["duration"] = 1 }, summary: true);
            var order = ctx.Task("order", "Order bricks", summary.Id, 1, new JsonObject { ["duration"] = days, ["phase"] = "procurement", [Plan.ChartKey] = false }, "LOGISTIC");
            if (first != null && ctx.FirstOf(first) is { } install) ctx.Link(order.Id, install);
            else ctx.Note("The order precedes no task.");
        }
    }

    sealed class ChainModule : ILapinModule
    {
        public string Id => "test-chain";
        public string Name => "Test chain";
        public void Register(IModuleRegistry r) => r.Service<IGeneratorContributor>(new Chain());
    }

    static Fixture F(bool chain)
    {
        ILapinModule[] modules = chain
            ? [new ArchitectureModule(), new PhasesModule(), new PlanningModule(), new ChainModule()]
            : [new ArchitectureModule(), new PhasesModule(), new PlanningModule()];
        var f = new Fixture(modules);
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01" });
        return f;
    }

    static JsonObject Schedule(Fixture f) => (JsonObject)f.Session.Query("planning.schedule", new JsonObject())!;
    static List<JsonObject> Tasks(Fixture f) => Schedule(f)["tasks"]!.AsArray().OfType<JsonObject>().ToList();
    static JsonObject Task(Fixture f, string name) => Tasks(f).First(t => (string)t["name"]! == name);

    static List<string> Links(Fixture f)
    {
        var s = Schedule(f);
        var names = s["tasks"]!.AsArray().ToDictionary(t => (string)t!["id"]!, t => (string)t!["name"]!);
        return s["links"]!.AsArray().Select(l => $"{names[(string)l!["from"]!]} → {names[(string)l["to"]!]}").Order().ToList();
    }

    static JsonObject Spec(Fixture f, bool on, int days = 4)
    {
        var spec = (JsonObject)((JsonObject)f.Session.Query("planning.generatorOptions", new JsonObject())!)["spec"]!.DeepClone();
        spec["chain"] = new JsonObject { ["on"] = on, ["days"] = days };
        return spec;
    }

    [Fact]
    public void A_contributor_adds_its_tasks_after_the_steps_under_its_key_and_they_keep_their_ids()
    {
        var f = F(chain: true);
        f.Wall(0, 0, 6, 0);
        var options = (JsonObject)f.Session.Query("planning.generatorOptions", new JsonObject())!;
        Assert.Equal(3, (int)options["contributors"]!["chain"]!["days"]!);
        // The preview counts its tasks (not its summaries).
        var preview = (JsonObject)f.Session.Query("planning.preview", new JsonObject { ["spec"] = Spec(f, true) })!;
        Assert.Equal(1, (int)preview["summary"]!["contributed"]!["chain"]!);
        f.Run("planning.generate", new JsonObject { ["spec"] = Spec(f, true) });
        var order = Task(f, "Order bricks");
        Assert.Equal(4, (int)order["duration"]!);
        var e = f.Model.Get((string)order["id"]!)!;
        Assert.Equal("chain/order", e.Str("gen"));
        Assert.Equal("chain", e.Str(Generator.GenByKey));
        Assert.Equal("chain/", f.Model.Get((string)Task(f, "Procurement")["id"]!)!.Str("gen"));
        Assert.Equal("LOGISTIC", e.PredefinedType);
        Assert.Contains(Links(f), l => l.StartsWith("Order bricks → ", StringComparison.Ordinal));
        // Its part of the spec is kept on the schedule as given, and the configurator opens on it again.
        Assert.Equal(4, (int)Plan.ScheduleOf(f.Model)!.Attrs["generator"]!["chain"]!["days"]!);
        // Generated again: the same task, the same id.
        f.Run("planning.generate", new JsonObject { ["spec"] = Spec(f, true, 6) });
        Assert.Equal((string)order["id"]!, (string)Task(f, "Order bricks")["id"]!);
        Assert.Equal(6, (int)Task(f, "Order bricks")["duration"]!);
        // Switched off with the contributor there: its tasks go, as any task no longer generated.
        f.Run("planning.generate", new JsonObject { ["spec"] = Spec(f, false) });
        Assert.DoesNotContain(Tasks(f), t => (string)t["name"]! == "Order bricks");
        // Its part is checked by it.
        Assert.Throws<CommandException>(() => f.Run("planning.generate", new JsonObject { ["spec"] = Spec(f, true, -1) }));
    }

    [Fact]
    public void Generated_again_without_the_contributor_its_tasks_and_links_stay_and_the_others_are_made_as_before()
    {
        var f = F(chain: true);
        f.Wall(0, 0, 6, 0);
        f.Run("planning.generate", new JsonObject { ["spec"] = Spec(f, true) });
        var before = Tasks(f).ToDictionary(t => (string)t["name"]!, t => (string)t["id"]!);
        var links = Links(f);

        // The project opened by an edition without the contributor (an extension not installed), and generated again.
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        var content = LapinPackage.Read(new MemoryStream(ms.ToArray()));
        var g = F(chain: false);
        g.Session.Load(content.Entities, content.Relations, null, content.Classes, content.Extensions);
        var options = (JsonObject)g.Session.Query("planning.generatorOptions", new JsonObject())!;
        Assert.Empty(options["contributors"]!.AsObject());
        // The spec it opens on still has the contributor's part, unread.
        Assert.Equal(4, (int)options["spec"]!["chain"]!["days"]!);
        var preview = (JsonObject)g.Session.Query("planning.preview", new JsonObject())!;
        Assert.Contains(preview["tasks"]!.AsArray(), t => (string)t!["name"]! == "Order bricks" && (bool?)t["kept"] == true);
        g.Run("planning.generate", new JsonObject());
        var after = Tasks(g).ToDictionary(t => (string)t["name"]!, t => (string)t["id"]!);
        Assert.Equal(before, after);
        Assert.Equal(links, Links(g));
        Assert.Equal("Procurement", Task(g, "Order bricks")["parent"] is { } p ? (string)Tasks(g).First(t => (string)t["id"]! == (string)p!)["name"]! : null);
        Assert.Equal(4, (int)Plan.ScheduleOf(g.Model)!.Attrs["generator"]!["chain"]!["days"]!);

        // A schedule never generated with a contributor regenerates as before, with or without one installed.
        var plain = F(chain: true);
        plain.Wall(0, 0, 6, 0);
        plain.Run("planning.generate", new JsonObject());
        var bare = F(chain: false);
        bare.Wall(0, 0, 6, 0);
        bare.Run("planning.generate", new JsonObject());
        Assert.Equal(Tasks(bare).Select(t => (string)t["name"]!), Tasks(plain).Select(t => (string)t["name"]!));
        Assert.Equal(Links(bare), Links(plain));
        Assert.Null(Plan.ScheduleOf(plain.Model)!.Attrs["generator"]!["chain"]);
    }
}
