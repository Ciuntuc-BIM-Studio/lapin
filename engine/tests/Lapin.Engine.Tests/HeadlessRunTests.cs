using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Clash;
using Lapin.Host;
using Lapin.Mep;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The engine without its window (docs/headless-run.md): a job makes a project of two walls a beam crosses, sets up a clash
/// test, runs it and saves; a second job opens the file and reads every test with its clashes (clash.report) as a CDE server
/// does. A step takes an earlier one's answer; a failing step leaves the others; what a job may not call is refused.
/// </summary>
public class HeadlessRunTests
{
    static Session NewSession()
    {
        ILapinModule[] use = [new ArchitectureModule(), new StructureModule(), new MepModule(), new ClashModule()];
        return new Session(ModuleCatalog.Create(use), new ProductManifest("test", "Test", ["core", .. use.Select(m => m.Id)]));
    }

    static JsonObject Step(string method, JsonObject p) => new() { ["method"] = method, ["params"] = p };
    static JsonObject Exec(string command, JsonObject args) => Step("doc.execute", new JsonObject { ["command"] = command, ["args"] = args });
    static JsonObject Ask(string query, JsonObject? args = null) => Step("doc.query", new JsonObject { ["name"] = query, ["args"] = args ?? [] });
    static JsonObject From(int step, string path) => new() { ["$step"] = step, ["path"] = path };

    /// <summary>Two walls 4 m apart along y, a beam across them at y = 3, the starting sets and tests, and Architecture × Structure.</summary>
    static JsonArray MakeSteps() =>
    [
        Exec("architecture.addWall", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 0, ["y2"] = 6, ["join"] = false }),
        Exec("architecture.addWall", new JsonObject { ["x1"] = 4, ["y1"] = 0, ["x2"] = 4, ["y2"] = 6, ["join"] = false }),
        Exec("structure.addBeam", new JsonObject { ["x1"] = -2, ["y1"] = 3, ["x2"] = 6, ["y2"] = 3, ["topOffset"] = -0.3 }),
        Exec("clash.seed", []),
        Ask("clash.list"),
        // Sets by name: Architecture, Services, Structure.
        Exec("clash.setTest", new JsonObject { ["name"] = "Architecture × Structure", ["kind"] = "hard", ["setA"] = From(4, "sets.0.id"), ["setB"] = From(4, "sets.2.id") }),
        Exec("clash.run", []),
    ];

    [Fact]
    public async Task A_job_makes_a_project_and_another_reads_its_clashes_as_a_server_does()
    {
        var dir = Directory.CreateTempSubdirectory("lapin-headless-").FullName;
        try
        {
            var file = Path.Combine(dir, "walls-beam.lapin");
            var made = await HeadlessRun.Run(NewSession(), PackageFiles.Create(), new JsonObject { ["steps"] = MakeSteps(), ["saveAs"] = file });
            Assert.True((bool)made["ok"]!, made.ToJsonString());
            Assert.True(File.Exists(file));
            Assert.Equal("test", (string?)made["engine"]!["product"]);
            Assert.Contains("clash", made["engine"]!["modules"]!.AsArray().Select(m => (string?)m));
            var steps = made["steps"]!.AsArray();
            Assert.All(steps, s => Assert.Null(s!["error"]));
            var walls = new[] { (string)steps[0]!["result"]!["id"]!, (string)steps[1]!["result"]!["id"]! };
            var beam = (string)steps[2]!["result"]!["id"]!;
            var run = steps[6]!["result"]!.AsArray().Single(r => (string?)r!["name"] == "Architecture × Structure")!;
            Assert.Equal(2, (int)run["found"]!);

            // The server's run: the file opened, its tests run again (their clashes kept from run to run), all of it read in one answer.
            var read = await HeadlessRun.Run(NewSession(), PackageFiles.Create(), new JsonObject
            {
                ["open"] = file,
                ["steps"] = new JsonArray(Exec("clash.run", []), Ask("clash.report")),
            });
            Assert.True((bool)read["ok"]!, read.ToJsonString());
            Assert.Empty(read["open"]!["absent"]!.AsArray());
            var report = read["steps"]![1]!["result"]!;
            Assert.Equal(64, ((string)report["hash"]!).Length);
            var tests = report["tests"]!.AsArray();
            Assert.Equal(5, tests.Count);
            var test = tests.Single(t => (string?)t!["name"] == "Architecture × Structure")!;
            Assert.Equal("hard", (string?)test["kind"]);
            Assert.False((bool)test["stale"]!);
            var clashes = test["clashes"]!.AsArray();
            Assert.Equal(2, clashes.Count);
            // Found again: active, the same numbers; each element by its GlobalId, its name, class and storey; where; how deep.
            Assert.All(clashes, c => Assert.Equal("active", (string?)c!["status"]));
            Assert.Equal(walls.Order(), clashes.Select(c => (string)c!["a"]!).Order());
            Assert.All(clashes, c => Assert.Equal(beam, (string?)c!["b"]));
            Assert.All(clashes, c => Assert.True(GlobalId.IsValid((string)c!["a"]!)));
            Assert.All(clashes, c => Assert.False(string.IsNullOrEmpty((string?)c!["aStorey"])));
            Assert.All(clashes, c => Assert.Equal(3, c!["at"]!.AsArray().Count));
            Assert.All(clashes, c => Assert.True((bool)c!["crossing"]! && (double)c["d"]! > 0.1));
            Assert.Equal([1, 2], clashes.Select(c => (int)c!["n"]!).Order());
            // Nothing was written: the file is as the first job saved it.
            Assert.Null(read["saved"]);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task A_failing_step_leaves_the_others_and_what_a_job_may_not_call_is_refused()
    {
        var answer = await HeadlessRun.Run(NewSession(), PackageFiles.Create(), new JsonObject
        {
            ["steps"] = new JsonArray(
                Ask("clash.nope"),
                Exec("clash.setTest", new JsonObject { ["setA"] = From(0, "id") }),
                Step("project.save", new JsonObject { ["path"] = "/tmp/x.lapin" }),
                Exec("architecture.addWall", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 3, ["y2"] = 0 }),
                Step("project.exportIfc", new JsonObject { ["path"] = "/tmp/x.ifc" }),
                Step("session.info", []),
                Exec("clash.setTest", new JsonObject { ["setA"] = From(9, "id") })),
        });
        Assert.True((bool)answer["ok"]!);
        var steps = answer["steps"]!.AsArray();
        Assert.Contains("clash.nope", (string?)steps[0]!["error"]);
        Assert.Contains("Step 0", (string?)steps[1]!["error"]);
        Assert.Contains("doc.query", (string?)steps[2]!["error"]);
        Assert.NotNull(steps[3]!["result"]!["id"]);
        Assert.NotNull(steps[4]!["error"]);
        Assert.Equal("test", (string?)steps[5]!["result"]!["product"]!["id"]);
        Assert.Contains("not an earlier step", (string?)steps[6]!["error"]);
    }

    [Fact]
    public async Task A_project_that_does_not_open_fails_the_job()
    {
        var answer = await HeadlessRun.Run(NewSession(), PackageFiles.Create(), new JsonObject { ["open"] = Path.Combine(Path.GetTempPath(), $"none-{Guid.NewGuid():N}.lapin"), ["steps"] = new JsonArray() });
        Assert.False((bool)answer["ok"]!);
        Assert.StartsWith("The project did not open", (string?)answer["error"]);
    }
}
