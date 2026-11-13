using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Graphs;
using Lapin.Host;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Visual graphs (docs/plan-visual-graphs.md, decision 522): the graph read and checked, evaluated by the engine with lists
/// mapped by the longest, a failing node saying why while the rest goes on, the limits; the preview leaving the model as
/// it was and drawing a ghost of what the commands would make; Bake one undo step, refused whole when a command fails;
/// graphs kept in the project and in the .lapin as graphs/&lt;name&gt;.json.
/// </summary>
public sealed class GraphTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-graphs-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static Fixture Project()
    {
        var f = new Fixture(new ArchitectureModule(), new GraphsModule());
        f.Run("storey.create", []);
        return f;
    }

    static JsonObject N(string id, string kind, JsonObject? values = null, params string[] whole)
    {
        var o = new JsonObject { ["id"] = id, ["kind"] = kind, ["x"] = 0, ["y"] = 0 };
        if (values != null) o["values"] = values;
        if (whole.Length > 0) o["whole"] = new JsonArray(whole.Select(w => (JsonNode)w).ToArray());
        return o;
    }

    static JsonObject W(string from, string output, string to, string input) => new() { ["from"] = from, ["out"] = output, ["to"] = to, ["in"] = input };

    static JsonObject G(JsonObject[] nodes, params JsonObject[] wires) => new()
    {
        ["format"] = 1, ["nodes"] = new JsonArray(nodes.Cast<JsonNode>().ToArray()), ["wires"] = new JsonArray(wires.Cast<JsonNode>().ToArray()),
    };

    static JsonObject Evaluate(Fixture f, JsonObject graph) => f.Session.Query("graphs.evaluate", new JsonObject { ["graph"] = graph })!.AsObject();

    static JsonObject NodeOf(JsonObject r, string id) => r["nodes"]![id]!.AsObject();

    static JsonNode? Out(JsonObject r, string id, string port) => NodeOf(r, id)["outputs"]![port];

    static double[] Numbers(JsonNode? n) => n!.AsArray().Select(x => (double)x!).ToArray();

    [Fact]
    public void Nodes_run_in_the_order_their_wires_give_and_lists_map_by_the_longest()
    {
        var f = Project();
        // Listed out of order: the adder after the range that feeds it.
        var g = G(
            [N("add", "add", new() { ["b"] = 10 }), N("r", "range", new() { ["start"] = 1, ["end"] = 3 }), N("short", "series", new() { ["start"] = 100, ["step"] = 1, ["count"] = 2 }), N("add2", "add")],
            W("r", "list", "add", "a"), W("r", "list", "add2", "a"), W("short", "list", "add2", "b"));
        var r = Evaluate(f, g);
        Assert.Equal([11, 12, 13], Numbers(Out(r, "add", "value")));
        // [1, 2, 3] + [100, 101]: the shorter repeats its last item.
        Assert.Equal([101, 103, 104], Numbers(Out(r, "add2", "value")));
        Assert.Equal(3, (int)NodeOf(r, "add")["counts"]!["value"]!);
        Assert.Equal(3, (int)NodeOf(r, "add")["items"]!);
    }

    [Fact]
    public void A_list_of_lists_maps_one_level_at_a_time_and_a_whole_input_takes_the_list_as_it_is()
    {
        var f = Project();
        var g = G(
            [N("pts", "grid", new() { ["nx"] = 3, ["ny"] = 1, ["dx"] = 2 }), N("count", "count"), N("xyz", "xyz"), N("sum", "sum"), N("nested", "formula", new() { ["formula"] = "a * 2" })],
            W("pts", "points", "count", "list"), W("pts", "points", "xyz", "point"), W("xyz", "x", "sum", "list"), W("xyz", "x", "nested", "a"));
        var r = Evaluate(f, g);
        Assert.Equal(3, (double)Out(r, "count", "count")!);
        Assert.Equal([0, 2, 4], Numbers(Out(r, "xyz", "x")));
        Assert.Equal(6, (double)Out(r, "sum", "sum")!);
        Assert.Equal([0, 4, 8], Numbers(Out(r, "nested", "value")));

        // A list of lists into a single input: the result keeps its shape.
        var deep = new Evaluator(GraphDoc.Read(G([N("a", "add", new() { ["a"] = new JsonArray(new JsonArray(1, 2), new JsonArray(3)), ["b"] = 1 })])), f.Model,
            (ModuleCatalog)f.Session.Reader.Services, null, f.Session.Reader, GraphLimits.PreviewTime).RunAll();
        var outer = (List<object?>)deep.Results["a"].Outputs["value"]!;
        Assert.Equal([2.0, 3.0], ((List<object?>)outer[0]!).Cast<double>());
        Assert.Equal([4.0], ((List<object?>)outer[1]!).Cast<double>());
    }

    [Fact]
    public void A_failing_node_says_why_the_nodes_after_it_say_which_input_has_nothing_and_the_rest_goes_on()
    {
        var f = Project();
        var g = G(
            [N("zero", "divide", new() { ["a"] = 1, ["b"] = 0 }), N("after", "add"), N("fine", "multiply", new() { ["a"] = 3, ["b"] = 4 }),
             N("some", "divide", new() { ["a"] = 1 }), N("list", "series", new() { ["start"] = 0, ["count"] = 3 })],
            W("zero", "value", "after", "a"), W("list", "list", "some", "b"));
        var r = Evaluate(f, g);
        Assert.Equal("Division by zero.", (string)NodeOf(r, "zero")["error"]!);
        Assert.Contains("„Divide” failed", (string)NodeOf(r, "after")["error"]!);
        Assert.Equal(12, (double)Out(r, "fine", "value")!);
        // One item of three failed: the others kept, the failure told.
        Assert.StartsWith("1 of 3 failed: Division by zero.", (string)NodeOf(r, "some")["warning"]!);
        Assert.Null(Out(r, "some", "value")!.AsArray()[0]);
        Assert.Equal(1.0, (double)Out(r, "some", "value")!.AsArray()[1]!);
    }

    [Fact]
    public void A_graph_is_checked_within_its_limits()
    {
        Assert.Contains("circle", Assert.Throws<CommandException>(() => GraphDoc.Read(G([N("a", "add"), N("b", "add")], W("a", "value", "b", "a"), W("b", "value", "a", "a")))).Message);
        Assert.Contains("one wire", Assert.Throws<CommandException>(() => GraphDoc.Read(G([N("a", "add"), N("b", "add"), N("c", "add")], W("a", "value", "c", "a"), W("b", "value", "c", "a")))).Message);
        Assert.Contains("same id", Assert.Throws<CommandException>(() => GraphDoc.Read(G([N("a", "add"), N("a", "add")]))).Message);
        Assert.Contains("does not have", Assert.Throws<CommandException>(() => GraphDoc.Read(G([N("a", "add")], W("a", "value", "z", "a")))).Message);
        var many = Enumerable.Range(0, GraphLimits.MaxNodes + 1).Select(i => N($"n{i}", "add")).ToArray();
        Assert.Contains("at most", Assert.Throws<CommandException>(() => GraphDoc.Read(G(many))).Message);

        var f = Project();
        var r = Evaluate(f, G([N("big", "series", new() { ["count"] = GraphLimits.MaxItems + 1 })]));
        Assert.Contains("at most", (string)NodeOf(r, "big")["error"]!);
        // Commands that read files or run scripts never run in a graph.
        r = Evaluate(f, G([N("imp", "command:project.importIfc")]));
        Assert.Contains("cannot run in a graph", (string)NodeOf(r, "imp")["error"]!);
        Assert.Null(GraphLimits.Refused("architecture.addWall"));
        Assert.NotNull(GraphLimits.Refused("drafting.importDxf"));
        Assert.NotNull(GraphLimits.Refused("scripting.save"));
        r = Evaluate(f, G([N("w", "command:architecture.addWall", new() { ["path"] = "/etc/passwd" })]));
        Assert.Contains("path", (string)NodeOf(r, "w")["error"]!);
    }

    static JsonObject Walls() => G(
        [N("xs", "series", new() { ["start"] = 0, ["step"] = 2, ["count"] = 3 }), N("a", "point"), N("b", "point", new() { ["y"] = 4 }),
         N("wall", "command:architecture.addWall", new() { ["height"] = 2.8 })],
        W("xs", "list", "a", "x"), W("xs", "list", "b", "x"), W("a", "point", "wall", "pt:1"), W("b", "point", "wall", "pt:2"));

    [Fact]
    public void A_preview_leaves_the_model_as_it_was_and_draws_the_ghost_of_what_the_commands_would_make()
    {
        var f = Project();
        f.Wall(10, 0, 10, 5);
        var revision = f.Model.Revision;
        var before = f.Model.Entities.Select(e => (e.Id, e.Version)).ToList();
        var undo = f.Session.History.UndoLabel;

        var r = Evaluate(f, Walls());
        var ids = Out(r, "wall", "ids")!.AsArray();
        Assert.Equal(3, ids.Count);
        Assert.Single(ids[0]!.AsArray());
        Assert.Equal(3, (int)r["calls"]!);
        var parts = r["ghost"]!["parts"]!.AsArray();
        Assert.Equal(3, parts.Select(p => (string)p!["element"]!).Distinct().Count(x => ids.Any(i => (string)i!.AsArray()[0]! == x)));
        Assert.All(parts, p => Assert.True(p!["positions"]!.AsArray().Count > 0));
        Assert.NotEmpty(r["ghost"]!["plan"]!.AsArray());
        // The points of the geometry nodes are drawn too.
        Assert.Equal(6 * 3, r["ghost"]!["points"]!.AsArray().Count);

        // Nothing changed: the same entities, the same objects, the same history.
        Assert.Equal(revision, f.Model.Revision);
        Assert.Equal(before, f.Model.Entities.Select(e => (e.Id, e.Version)).ToList());
        Assert.Equal(undo, f.Session.History.UndoLabel);
        Assert.DoesNotContain(f.Model.OfClass(IfcClass.Wall), w => ids.Any(i => (string)i!.AsArray()[0]! == w.Id));

        // Again, on the copy kept: the same ghost, fresh ids.
        var again = Evaluate(f, Walls());
        Assert.Equal(parts.Count, again["ghost"]!["parts"]!.AsArray().Count);
        Assert.Single(f.Model.OfClass(IfcClass.Wall));
        // The model changed: the copy follows it (the new wall is in the next preview's model).
        f.Wall(20, 0, 20, 5);
        var after = Evaluate(f, G([N("walls", "ofClass", new() { ["class"] = IfcClass.Wall })]));
        Assert.Equal(2, Out(after, "walls", "ids")!.AsArray().Count);
    }

    [Fact]
    public void Bake_makes_it_in_one_undo_step_and_is_refused_whole_when_a_command_fails()
    {
        var f = Project();
        var id = (string)f.Run("graphs.save", new JsonObject { ["name"] = "Row of walls", ["graph"] = Walls() })!["id"]!;
        var r = f.Run("graphs.bake", new JsonObject { ["id"] = id })!;
        Assert.Equal(3, r["made"]!.AsArray().Count);
        Assert.Equal(3, f.Model.OfClass(IfcClass.Wall).Count());
        Assert.Equal("Bake graph", f.Session.History.UndoLabel);
        Assert.Equal([0.0, 2, 4], f.Model.OfClass(IfcClass.Wall).Select(w => Lapin.Model.Json.Number(w.Attrs["start"]!.AsArray()[0]) ?? -1).Order());
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(IfcClass.Wall));
        Assert.Equal("Save graph", f.Session.History.UndoLabel);

        // A wall of no length fails: nothing of the graph is made.
        var bad = Walls();
        bad["nodes"]!.AsArray()[2]!["values"] = new JsonObject { ["y"] = 0 };
        var revision = f.Model.Revision;
        var e = Assert.Throws<CommandException>(() => f.Run("graphs.bake", new JsonObject { ["graph"] = bad }));
        Assert.StartsWith("The graph was not baked", e.Message);
        Assert.Empty(f.Model.OfClass(IfcClass.Wall));
        Assert.Equal(revision, f.Model.Revision);
    }

    [Fact]
    public void Saving_renaming_and_deleting_a_graph_are_one_undo_step_each_and_the_graph_is_written_as_read()
    {
        var f = Project();
        var r = f.Run("graphs.save", new JsonObject { ["name"] = "Walls", ["graph"] = Walls() })!;
        var id = (string)r["id"]!;
        Assert.True((bool)r["created"]!);
        Assert.Equal(4, (int)r["nodes"]!);
        Assert.Equal("Save graph", f.Session.History.UndoLabel);
        Assert.Contains("already", Assert.Throws<CommandException>(() => f.Run("graphs.save", new JsonObject { ["name"] = "WALLS", ["graph"] = Walls() })).Message);
        Assert.Throws<CommandException>(() => f.Run("graphs.save", new JsonObject { ["name"] = "../x", ["graph"] = Walls() }));
        // A graph that does not read is refused, the saved one kept.
        Assert.Throws<CommandException>(() => f.Run("graphs.save", new JsonObject { ["id"] = id, ["graph"] = G([N("a", "add"), N("a", "add")]) }));

        // Moved node: the same graph by id, its name kept.
        var moved = Walls();
        moved["nodes"]!.AsArray()[0]!["x"] = 120;
        f.Run("graphs.save", new JsonObject { ["id"] = id, ["graph"] = moved });
        var got = f.Session.Query("graphs.get", new JsonObject { ["id"] = id })!;
        Assert.Equal("Walls", (string)got["name"]!);
        Assert.Equal(120, (double)got["graph"]!["nodes"]![0]!["x"]!);
        f.Session.Undo();
        Assert.Equal(0, (double)f.Session.Query("graphs.get", new JsonObject { ["id"] = id })!["graph"]!["nodes"]![0]!["x"]!);

        f.Run("graphs.rename", new JsonObject { ["id"] = id, ["name"] = "Row" });
        Assert.Equal("Rename graph", f.Session.History.UndoLabel);
        Assert.Equal("Row", (string)f.Session.Query("graphs.list", [])!.AsArray()[0]!["name"]!);
        // The core's edits do not reach a graph's attributes; deleting it does.
        Assert.Throws<CommandException>(() => f.Run("element.rename", new JsonObject { ["id"] = id, ["name"] = "x" }));
        f.Run("graphs.delete", new JsonObject { ["id"] = id });
        Assert.Empty(f.Model.OfClass(GraphsModule.Class));
        f.Session.Undo();
        Assert.Single(f.Model.OfClass(GraphsModule.Class));
    }

    [Fact]
    public void A_graph_is_in_the_package_as_graphs_name_json_and_comes_back_with_it()
    {
        var f = Project();
        f.Run("graphs.save", new JsonObject { ["name"] = "Row of walls", ["graph"] = Walls() });
        var path = Path.Combine(_dir, "graphs.lapin");
        PackageFiles.Save(f.Session, path);
        using (var zip = ZipFile.OpenRead(path))
        {
            var entry = zip.GetEntry("graphs/Row of walls.json") ?? throw new Xunit.Sdk.XunitException("graphs/Row of walls.json is missing.");
            using var s = entry.Open();
            using var reader = new StreamReader(s, Encoding.UTF8);
            var text = JsonNode.Parse(reader.ReadToEnd())!;
            Assert.Equal(4, text["nodes"]!.AsArray().Count);
        }
        var g = Project();
        PackageFiles.Open(g.Session, path);
        var e = Assert.Single(g.Model.OfClass(GraphsModule.Class));
        Assert.Equal("Row of walls", e.Name);
        Assert.Equal(GraphDoc.Read(Walls()).ToJson().ToJsonString(), GraphDoc.Read(e.Attrs["graph"]).ToJson().ToJsonString());
        // It bakes in the project it was opened in.
        g.Run("graphs.bake", new JsonObject { ["id"] = e.Id });
        Assert.Equal(3, g.Model.OfClass(IfcClass.Wall).Count());
    }

    [Fact]
    public void The_nodes_query_lists_the_modules_own_nodes_and_the_commands_a_graph_may_run()
    {
        var f = Project();
        var r = f.Session.Query("graphs.nodes", [])!;
        var kinds = r["nodes"]!.AsArray().Select(n => (string)n!["kind"]!).ToList();
        Assert.Contains("range", kinds);
        Assert.Contains("formula", kinds);
        var commands = r["commands"]!.AsArray().Select(n => (string)n!["name"]!).ToList();
        Assert.Contains("architecture.addWall", commands);
        Assert.DoesNotContain(commands, c => c.StartsWith("graphs.", StringComparison.Ordinal) || c == "edit.batch");
        // Every command of the module is in the source catalogue (node:check keeps it fresh).
        foreach (var c in new[] { "graphs.save", "graphs.rename", "graphs.delete", "graphs.bake" }) Assert.Contains(c, CommandCatalogue.SourceCommands);
    }
}
