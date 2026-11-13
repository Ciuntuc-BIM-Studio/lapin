using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Graphs;
using Lapin.Host;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Visual graphs (docs/plan-visual-graphs.md, decision 527): the graph read and checked, evaluated by the engine with lists
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

    static JsonObject Evaluate(Fixture f, JsonObject graph) => f.Session.Query("graphs.evaluate", new JsonObject { ["graph"] = graph.DeepClone() })!.AsObject();

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

        // A command node without a storey of its own works on the storey given (the view's), as a tool draws on the storey shown.
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
        f.Run("graphs.bake", new JsonObject { ["id"] = id, ["storey"] = upper });
        Assert.All(f.Model.OfClass(IfcClass.Wall), w => Assert.Equal(upper, Spatial.StoreyOf(f.Model, w.Id)!.Id));
        Assert.Equal(3, Out(Evaluate(f, Walls()), "wall", "ids")!.AsArray().Count);
        f.Session.Undo();

        // A wall of no length fails: nothing of the graph is made.
        var bad = Walls();
        bad["nodes"]!.AsArray()[2]!["values"] = new JsonObject { ["y"] = 0 };
        // The model as it was (its entities; the revision only ever grows, rolled back or not).
        var before = f.Model.Entities.Select(x => x.Id).ToList();
        var e = Assert.Throws<CommandException>(() => f.Run("graphs.bake", new JsonObject { ["graph"] = bad }));
        Assert.StartsWith("The graph was not baked", e.Message);
        Assert.Empty(f.Model.OfClass(IfcClass.Wall));
        Assert.Equal(before, f.Model.Entities.Select(x => x.Id).ToList());
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
        foreach (var c in new[] { "graphs.save", "graphs.rename", "graphs.delete", "graphs.bake", "graphs.set" }) Assert.Contains(c, CommandCatalogue.SourceCommands);
    }

    static JsonObject Exec(string command, JsonObject args) => new() { ["command"] = command, ["args"] = args };

    [Fact]
    public void Graphs_are_the_hosts_a_guest_neither_runs_evaluates_nor_touches_one()
    {
        var f = Project();
        var graph = (string)f.Run("graphs.save", new JsonObject { ["name"] = "Row of walls", ["graph"] = Walls() })!["id"]!;
        var other = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0 })!["id"]!;
        var wall = f.Wall(0, 0, 5, 0);
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        string? Error(string method, JsonObject p, string role = "editor")
        {
            var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString(), new Caller("guest-1", role));
            var h = BinaryFrame.Decode(reply).Header.RootElement;
            return h.TryGetProperty("error", out var e) ? e.GetString() : null;
        }
        var refused = new (string Method, JsonObject Params)[]
        {
            ("doc.execute", Exec("graphs.save", new JsonObject { ["name"] = "Mine", ["graph"] = Walls() })),
            ("doc.execute", Exec("graphs.bake", new JsonObject { ["id"] = graph })),
            ("doc.execute", Exec("graphs.set", new JsonObject { ["id"] = graph, ["values"] = new JsonObject() })),
            ("doc.execute", Exec("graphs.rename", new JsonObject { ["id"] = graph, ["name"] = "x" })),
            ("doc.execute", Exec("graphs.delete", new JsonObject { ["id"] = graph })),
            ("doc.execute", Exec("edit.batch", new JsonObject { ["steps"] = new JsonArray(Exec("graphs.bake", new JsonObject { ["id"] = graph })) })),
            ("doc.execute", Exec("element.delete", new JsonObject { ["ids"] = new JsonArray(graph) })),
            ("doc.execute", Exec("element.delete", new JsonObject { ["ids"] = new JsonArray(wall, graph) })),
            ("doc.execute", Exec("element.rename", new JsonObject { ["id"] = graph, ["name"] = "x" })),
            ("doc.execute", Exec("group.create", new JsonObject { ["ids"] = new JsonArray(graph, wall) })),
            ("doc.execute", Exec("element.copyToStoreys", new JsonObject { ["ids"] = new JsonArray(graph), ["storeyIds"] = new JsonArray(other) })),
            ("doc.execute", Exec("element.moveToStorey", new JsonObject { ["ids"] = new JsonArray(graph), ["storeyId"] = other })),
            ("doc.execute", Exec("element.transform", new JsonObject { ["ids"] = new JsonArray(graph), ["op"] = "copy", ["by"] = new JsonArray(1.0, 0.0) })),
            ("doc.execute", Exec("edit.batch", new JsonObject { ["steps"] = new JsonArray(Exec("element.delete", new JsonObject { ["ids"] = new JsonArray(graph) })) })),
            ("doc.query", new JsonObject { ["name"] = "graphs.evaluate", ["args"] = new JsonObject { ["id"] = graph } }),
            ("doc.query", new JsonObject { ["name"] = "graphs.get", ["args"] = new JsonObject { ["id"] = graph } }),
            ("doc.query", new JsonObject { ["name"] = "graphs.list", ["args"] = new JsonObject() }),
        };
        foreach (var (method, p) in refused) Assert.Equal("Only the host can do that.", Error(method, p));
        // One who joined to look is refused the queries too.
        Assert.Equal("Only the host can do that.", Error("doc.query", new JsonObject { ["name"] = "graphs.nodes", ["args"] = new JsonObject() }, "viewer"));
        Assert.Equal("Only the host can do that.", EngineDispatcher.AllowedRemote("doc.query", new JsonObject { ["name"] = "graphs.evaluate" }, "owner"));
        Assert.Null(EngineDispatcher.AllowedRemote("doc.query", new JsonObject { ["name"] = "architecture.wallTypes" }, "viewer"));
        Assert.Single(f.Model.OfClass(GraphsModule.Class));
        Assert.Equal("Row of walls", f.Model.Get(graph)!.Name);
        Assert.Single(f.Model.OfClass(IfcClass.Wall));
        // The guest's own work goes on; the host bakes and deletes as before.
        Assert.Null(Error("doc.execute", Exec("element.delete", new JsonObject { ["ids"] = new JsonArray(wall) })));
        f.Run("graphs.bake", new JsonObject { ["id"] = graph });
        Assert.Equal(3, f.Model.OfClass(IfcClass.Wall).Count());
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(graph) });
        Assert.Empty(f.Model.OfClass(GraphsModule.Class));
    }

    // ── the list nodes (decision 527, stage 1 extended) ──

    static string Json(JsonNode? n) => n?.ToJsonString() ?? "null";

    static JsonObject Typed(string id, string items, string kind = "Auto") => N(id, "list", new() { ["items"] = items, ["kind"] = kind });

    [Fact]
    public void A_list_typed_by_hand_gives_numbers_when_every_item_is_one_else_texts()
    {
        var f = Project();
        var many = string.Join(";", Enumerable.Repeat("1", GraphLimits.MaxItems + 1));
        var r = Evaluate(f, G([Typed("n", "3; 4,5\n 6;"), Typed("t", "W1; 2"), Typed("forced", "3; 4", "Texts"), Typed("bad", "3; x", "Numbers"),
            Typed("empty", " ; "), Typed("big", many), Typed("inf", "1; Infinity")]));
        Assert.Equal("[3,4.5,6]", Json(Out(r, "n", "list")));
        Assert.Equal("[\"W1\",\"2\"]", Json(Out(r, "t", "list")));
        Assert.Equal("[\"3\",\"4\"]", Json(Out(r, "forced", "list")));
        Assert.Equal("„x” is not a number.", (string)NodeOf(r, "bad")["error"]!);
        Assert.Equal("[]", Json(Out(r, "empty", "list")));
        Assert.Contains("at most", (string)NodeOf(r, "big")["error"]!);
        // Only finite numbers: „Infinity” is a text, so the list is of texts.
        Assert.Equal("[\"1\",\"Infinity\"]", Json(Out(r, "inf", "list")));
    }

    [Fact]
    public void Join_lists_puts_them_one_after_the_other_a_single_value_as_one_item_an_empty_input_left_out()
    {
        var f = Project();
        var r = Evaluate(f, G(
            [Typed("a", "1; 2"), N("five", "number", new() { ["value"] = 5 }), Typed("d", "x"), N("join", "joinLists"), N("none", "joinLists"),
             N("s1", "series", new() { ["count"] = 6000 }), N("s2", "series", new() { ["count"] = 6000 }), N("big", "joinLists")],
            W("a", "list", "join", "a"), W("five", "value", "join", "b"), W("d", "list", "join", "d"), W("s1", "list", "big", "a"), W("s2", "list", "big", "b")));
        Assert.Equal("[1,2,5,\"x\"]", Json(Out(r, "join", "list")));
        Assert.Equal("[]", Json(Out(r, "none", "list")));
        Assert.Contains("at most", (string)NodeOf(r, "big")["error"]!);
    }

    [Fact]
    public void Sort_orders_numbers_by_size_texts_naturally_and_items_by_another_lists_values()
    {
        var f = Project();
        var r = Evaluate(f, G(
            [Typed("n", "3; 1; 2"), N("up", "sort"), N("down", "sort", new() { ["descending"] = true }),
             Typed("t", "W10; w2; W1; Beam"), N("texts", "sort"), Typed("mixed", "2; a; 10"), N("mix", "sort"),
             Typed("names", "a; b; c"), Typed("heights", "2.8; 3,1; 2.5"), N("by", "sort"), N("byDown", "sort", new() { ["descending"] = true }),
             Typed("ties", "b; a; c"), Typed("tieKeys", "1; 2; 1"), N("stable", "sort"),
             Typed("two", "1; 2"), N("short", "sort"),
             N("nothing", "sort", new() { ["list"] = new JsonArray(3, null, 1) }), N("nothingDown", "sort", new() { ["list"] = new JsonArray(3, null, 1), ["descending"] = true }),
             N("pts", "grid", new() { ["nx"] = 2, ["ny"] = 1 }), N("points", "sort"), N("empty", "sort")],
            W("n", "list", "up", "list"), W("n", "list", "down", "list"), W("t", "list", "texts", "list"), W("mixed", "list", "mix", "list"),
            W("names", "list", "by", "list"), W("heights", "list", "by", "by"), W("names", "list", "byDown", "list"), W("heights", "list", "byDown", "by"),
            W("ties", "list", "stable", "list"), W("tieKeys", "list", "stable", "by"), W("names", "list", "short", "list"), W("two", "list", "short", "by"),
            W("pts", "points", "points", "list")));
        Assert.Equal("[1,2,3]", Json(Out(r, "up", "list")));
        Assert.Equal("[1,2,0]", Json(Out(r, "up", "order")));
        Assert.Equal("[3,2,1]", Json(Out(r, "down", "list")));
        Assert.Equal("[\"Beam\",\"W1\",\"w2\",\"W10\"]", Json(Out(r, "texts", "list")));
        Assert.Equal("[\"2\",\"10\",\"a\"]", Json(Out(r, "mix", "list")));
        Assert.Equal("[\"c\",\"a\",\"b\"]", Json(Out(r, "by", "list")));
        Assert.Equal("[2,0,1]", Json(Out(r, "by", "order")));
        Assert.Equal("[\"b\",\"a\",\"c\"]", Json(Out(r, "byDown", "list")));
        // Equal keys keep their order, whichever way.
        Assert.Equal("[\"b\",\"c\",\"a\"]", Json(Out(r, "stable", "list")));
        Assert.Equal("By has 2 values for 3 items: one each.", (string)NodeOf(r, "short")["error"]!);
        // Nothing goes last either way.
        Assert.Equal("[1,3,null]", Json(Out(r, "nothing", "list")));
        Assert.Equal("[3,1,null]", Json(Out(r, "nothingDown", "list")));
        Assert.Contains("cannot be sorted", (string)NodeOf(r, "points")["error"]!);
        Assert.Equal("[]", Json(Out(r, "empty", "list")));
        Assert.Equal("[]", Json(Out(r, "empty", "order")));
    }

    [Fact]
    public void Sort_puts_elements_in_the_order_of_their_heights()
    {
        var f = Project();
        var tall = f.Wall(0, 0, 5, 0, height: 3.2);
        var low = f.Wall(0, 2, 5, 2, height: 2.4);
        var mid = f.Wall(0, 4, 5, 4, height: 2.8);
        var r = Evaluate(f, G(
            [N("walls", "ofClass", new() { ["class"] = IfcClass.Wall }), N("h", "property", new() { ["key"] = "height" }), N("sort", "sort")],
            W("walls", "ids", "h", "id"), W("walls", "ids", "sort", "list"), W("h", "value", "sort", "by")));
        Assert.Equal([low, mid, tall], Out(r, "sort", "list")!.AsArray().Select(x => (string)x!));
    }

    [Fact]
    public void Part_of_a_list_takes_count_items_from_start_a_negative_start_counting_from_the_end()
    {
        var f = Project();
        JsonObject Part(string id, int start, int? count = null)
        {
            var v = new JsonObject { ["start"] = start };
            if (count != null) v["count"] = count;
            return N(id, "slice", v);
        }
        string[] ids = ["mid", "last", "lastTwo", "past", "before", "bad", "all", "empty"];
        var r = Evaluate(f, G(
            [N("r", "range", new() { ["start"] = 0, ["end"] = 9 }), Part("mid", 2, 3), Part("last", -3), Part("lastTwo", -3, 2), Part("past", 20),
             Part("before", -20, 2), Part("bad", 0, -1), Part("all", 0), Part("empty", 1, 2)],
            [.. ids.Where(i => i != "empty").Select(i => W("r", "list", i, "list"))]));
        Assert.Equal("[2,3,4]", Json(Out(r, "mid", "list")));
        Assert.Equal("[7,8,9]", Json(Out(r, "last", "list")));
        Assert.Equal("[7,8]", Json(Out(r, "lastTwo", "list")));
        Assert.Equal("[]", Json(Out(r, "past", "list")));
        Assert.Equal("[0,1]", Json(Out(r, "before", "list")));
        Assert.Contains("fewer than no items", (string)NodeOf(r, "bad")["error"]!);
        Assert.Equal(10, Out(r, "all", "list")!.AsArray().Count);
        Assert.Equal("[]", Json(Out(r, "empty", "list")));
    }

    [Fact]
    public void Unique_keeps_each_value_once_in_the_order_first_seen_and_counts_them()
    {
        var f = Project();
        var r = Evaluate(f, G(
            [N("u", "unique", new() { ["list"] = new JsonArray(1, 1.0000000001, 2, "1", "a", "A", "a", 2, 1.0000001, true, true) }),
             N("p1", "point", new() { ["x"] = 1, ["y"] = 2 }), N("p2", "point", new() { ["x"] = 1, ["y"] = 2.0000000004 }), N("p3", "point", new() { ["x"] = 3 }),
             N("pts", "joinLists"), N("up", "unique"), N("empty", "unique")],
            W("p1", "point", "pts", "a"), W("p2", "point", "pts", "b"), W("p3", "point", "pts", "c"), W("pts", "list", "up", "list")));
        // Numbers a billionth apart are one; a text „1” is not the number 1; texts keep their case.
        Assert.Equal("[1,2,\"1\",\"a\",\"A\",1.0000001,true]", Json(Out(r, "u", "list")));
        Assert.Equal("[2,2,1,2,1,1,2]", Json(Out(r, "u", "times")));
        Assert.Equal(2, Out(r, "up", "list")!.AsArray().Count);
        Assert.Equal("[2,1]", Json(Out(r, "up", "times")));
        Assert.Equal("[]", Json(Out(r, "empty", "list")));
    }

    [Fact]
    public void Every_with_every_feeds_the_next_node_every_pair_so_a_command_makes_every_combination()
    {
        var f = Project();
        var g = G(
            [Typed("xs", "0; 5"), Typed("ys", "0; 3; 6"), N("cross", "cross"), N("a", "point"), N("end", "add", new() { ["b"] = 2 }), N("b", "point"),
             N("wall", "command:architecture.addWall", new() { ["height"] = 2.8 })],
            W("xs", "list", "cross", "a"), W("ys", "list", "cross", "b"), W("cross", "a", "a", "x"), W("cross", "b", "a", "y"),
            W("cross", "a", "end", "a"), W("end", "value", "b", "x"), W("cross", "b", "b", "y"), W("a", "point", "wall", "pt:1"), W("b", "point", "wall", "pt:2"));
        var r = Evaluate(f, g);
        Assert.Equal("[0,0,0,5,5,5]", Json(Out(r, "cross", "a")));
        Assert.Equal("[0,3,6,0,3,6]", Json(Out(r, "cross", "b")));
        Assert.Equal(6, (double)Out(r, "cross", "count")!);
        Assert.Equal(6, Out(r, "wall", "ids")!.AsArray().Count);

        f.Run("graphs.bake", new JsonObject { ["graph"] = g.DeepClone() });
        var starts = f.Model.OfClass(IfcClass.Wall).Select(w => w.Attrs["start"]!.AsArray()).Select(p => ((double)p[0]!, (double)p[1]!)).Order().ToList();
        Assert.Equal([(0.0, 0.0), (0, 3), (0, 6), (5, 0), (5, 3), (5, 6)], starts);

        r = Evaluate(f, G([N("s1", "series", new() { ["count"] = 101 }), N("s2", "series", new() { ["count"] = 100 }), N("big", "cross"), Typed("e", ""), N("none", "cross")],
            W("s1", "list", "big", "a"), W("s2", "list", "big", "b"), W("e", "list", "none", "a"), W("s2", "list", "none", "b")));
        Assert.Equal($"Every with every makes 10100 pairs: at most {GraphLimits.MaxItems}.", (string)NodeOf(r, "big")["error"]!);
        Assert.Equal("[]", Json(Out(r, "none", "a")));
        Assert.Equal(0, (double)Out(r, "none", "count")!);

        var kinds = f.Session.Query("graphs.nodes", [])!["nodes"]!.AsArray().Where(n => (string)n!["group"]! == "Lists").Select(n => (string)n!["kind"]!).ToList();
        foreach (var k in new[] { "list", "joinLists", "sort", "slice", "unique", "cross" }) Assert.Contains(k, kinds);
    }

    // ── the model's geometry read as references (decision 534) ──

    static Fixture Site()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Structure.StructureModule(), new Lapin.Drafting.DraftingModule(), new Lapin.Furniture.FurnitureModule(), new Lapin.Civil.CivilModule(), new GraphsModule());
        f.Run("storey.create", []);
        return f;
    }

    static double[] Pt(JsonNode? n) => n!["$pt"]!.AsArray().Select(x => (double)x!).ToArray();
    static List<double[]> LineOf(JsonNode? n) => n!["$line"]!.AsArray().Select(p => p!.AsArray().Select(x => (double)x!).ToArray()).ToList();
    static JsonArray P(params double[][] p) => new(p.Select(x => (JsonNode)new JsonArray(x[0], x[1])).ToArray());
    static JsonObject Picked(string id, params string[] ids) => N(id, "pick", new() { ["ids"] = new JsonArray(ids.Select(i => (JsonNode)i).ToArray()) });
    static string Id(JsonNode? r) => (string)r!["id"]!;

    static void Near(double[] want, double[] got) { Assert.Equal(want.Length, got.Length); for (var i = 0; i < want.Length; i++) Assert.Equal(want[i], got[i], 6); }

    [Fact]
    public void Axis_reads_straight_and_curved_walls_beams_railings_and_grids_and_an_element_without_one_fails_on_its_item()
    {
        var f = Site();
        var wall = f.Wall(0, 0, 6, 0);
        var curved = Id(f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 10, ["x2"] = 10, ["y2"] = 10, ["x3"] = 5, ["y3"] = 12, ["join"] = false }));
        var beam = Id(f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 2, ["x2"] = 4, ["y2"] = 2 }));
        var railing = Id(f.Run("architecture.addRailing", new JsonObject { ["storeyId"] = f.Storey, ["points"] = P([0, 1], [3, 1], [3, 4]) }));
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = 0, ["y1"] = -2, ["x2"] = 6, ["y2"] = -2, ["tag"] = "A" });
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = 0, ["y1"] = -2, ["x2"] = 0, ["y2"] = 8, ["tag"] = "1" });
        var grid = f.Model.OfClass(IfcClass.Grid).Single().Id;
        var column = Id(f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 1, ["y"] = 1 }));
        var upper = Id(f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0 }));
        var high = Id(f.Run("architecture.addWall", new JsonObject { ["storeyId"] = upper, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 0, ["baseOffset"] = 0.5, ["join"] = false }));

        var r = Evaluate(f, G(
            [Picked("w", wall), Picked("c", curved), Picked("b", beam), Picked("r", railing), Picked("g", grid), Picked("mixed", wall, column), Picked("h", high),
             N("aw", "axis"), N("ac", "axis"), N("ab", "axis"), N("ar", "axis"), N("ag", "axis"), N("am", "axis"), N("ah", "axis")],
            W("w", "ids", "aw", "id"), W("c", "ids", "ac", "id"), W("b", "ids", "ab", "id"), W("r", "ids", "ar", "id"), W("g", "ids", "ag", "id"), W("mixed", "ids", "am", "id"), W("h", "ids", "ah", "id")));
        var straight = LineOf(Out(r, "aw", "line")![0]);
        Assert.Equal(2, straight.Count);
        Near([0, 0, 0], straight[0]);
        Near([6, 0, 0], straight[1]);
        Near([6, 0, 0], Pt(Out(r, "aw", "end")![0]));

        // A curved wall: its arc through the chord rule, from end to end, every point on the circle.
        var arc = LineOf(Out(r, "ac", "line")![0]);
        Assert.True(arc.Count > 8);
        Near([0, 10, 0], arc[0]);
        Near([10, 10, 0], arc[^1]);
        var (cx, cy, radius) = (5.0, 10 + 2 - (25 + 4) / 4.0, (25 + 4) / 4.0); // rise 2 over a 10 m chord
        Assert.All(arc, p => Assert.Equal(radius, Math.Sqrt((p[0] - cx) * (p[0] - cx) + (p[1] - cy) * (p[1] - cy)), 5));
        Assert.Contains(arc, p => Math.Abs(p[1] - 12) < 0.01);

        // A beam at its top, under the level above; a railing's points; a grid's every axis.
        Near([0, 2, 3], Pt(Out(r, "ab", "start")![0]));
        Assert.Equal(3, LineOf(Out(r, "ar", "line")![0]).Count);
        var axes = Out(r, "ag", "line")![0]!.AsArray();
        Assert.Equal(2, axes.Count);
        Near([0, 8, 0], Pt(Out(r, "ag", "end")![0]![1]));
        // A wall on the upper storey, at its base.
        Near([0, 0, 3.5], Pt(Out(r, "ah", "start")![0]));

        // A column has no axis: that item fails, the wall's is read.
        Assert.StartsWith("1 of 2 failed:", (string)NodeOf(r, "am")["warning"]!);
        Assert.Contains("has no axis", (string)NodeOf(r, "am")["warning"]!);
        Assert.NotNull(Out(r, "am", "line")![0]);
    }

    [Fact]
    public void Outline_reads_floors_rooms_roofs_and_site_zones_at_their_level()
    {
        var f = Site();
        var sq = P([0, 0], [6, 0], [6, 4], [0, 4]);
        var slab = Id(f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = sq.DeepClone(), ["offset"] = -0.1 }));
        var room = Id(f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["points"] = sq.DeepClone() }));
        var roof = Id(f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = sq.DeepClone() }));
        var planting = Id(f.Run("civil.addPlanting", new JsonObject { ["storeyId"] = f.Storey, ["points"] = sq.DeepClone() }));
        var wall = f.Wall(0, 0, 6, 0);
        var r = Evaluate(f, G([Picked("all", slab, room, roof, planting), N("o", "outline"), Picked("w", wall), N("ow", "outline")],
            W("all", "ids", "o", "id"), W("w", "ids", "ow", "id")));
        var lines = Out(r, "o", "line")!.AsArray();
        Assert.Equal(4, lines.Count);
        Assert.All(lines, l => { Assert.True((bool)l!["closed"]!); Assert.Equal(4, LineOf(l).Count); });
        Assert.All(LineOf(lines[0]), p => Assert.Equal(-0.1, p[2], 6));
        Assert.All(LineOf(lines[1]), p => Assert.Equal(0, p[2], 6));
        Near([6, 4, 0], Pt(Out(r, "o", "points")![1]![2]));
        Assert.Contains("has no outline", (string)NodeOf(r, "ow")["error"]!);
    }

    [Fact]
    public void Location_reads_columns_footings_furniture_doors_and_windows()
    {
        var f = Site();
        var wall = f.Wall(0, 0, 6, 0);
        var column = Id(f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 1, ["y"] = 1, ["rotation"] = 30 }));
        var pad = Id(f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 2 }));
        var chair = Id(f.Run("furniture.place", new JsonObject { ["storeyId"] = f.Storey, ["kind"] = "armchair", ["x"] = 3, ["y"] = 3, ["against"] = false, ["rotation"] = 90 }));
        var door = Id(f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 2 }));
        var window = Id(f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4, ["sill"] = 0.9 }));
        var r = Evaluate(f, G([Picked("all", column, pad, chair, door, window, wall), N("l", "location")], W("all", "ids", "l", "id")));
        var pts = Out(r, "l", "point")!.AsArray();
        var turns = Out(r, "l", "rotation")!.AsArray();
        Near([1, 1, 0], Pt(pts[0]));
        Assert.Equal(30, (double)turns[0]!, 6);
        Near([2, 2, 0], Pt(pts[1]));
        Near([3, 3, 0], Pt(pts[2]));
        Assert.Equal(90, (double)turns[2]!, 6);
        Near([2, 0, 0], Pt(pts[3]));
        Assert.Equal(0, (double)turns[3]!, 6);
        Near([4, 0, 0.9], Pt(pts[4]));
        Assert.Null(pts[5]);
        Assert.Contains("is not placed at a point", (string)NodeOf(r, "l")["warning"]!);
    }

    [Fact]
    public void Bounding_box_measures_any_elements_body_and_a_drafted_shape()
    {
        var f = Site();
        var wall = f.Wall(0, 0, 6, 0, height: 2.5);
        var plan = Id(f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey }));
        var circle = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = new JsonObject { ["type"] = "circle", ["c"] = new JsonArray(5, 5), ["r"] = 1 } })!["ids"]![0]!;
        var r = Evaluate(f, G([Picked("p", wall, circle), N("b", "box")], W("p", "ids", "b", "id")));
        var (lo, hi) = f.Bounds(wall);
        Near([lo.X, lo.Y, lo.Z], Pt(Out(r, "b", "min")![0]));
        Near([hi.X, hi.Y, hi.Z], Pt(Out(r, "b", "max")![0]));
        Assert.Equal(2.5, (double)Out(r, "b", "z")![0]!, 6);
        Assert.Equal(6, (double)Out(r, "b", "x")![0]!, 3);
        // A drafted circle by its chords (within a millimetre of the curve).
        Assert.All(Pt(Out(r, "b", "center")![1]).Zip(new[] { 5.0, 5, 0 }), p => Assert.Equal(p.Second, p.First, 2));
        Assert.Equal(2, (double)Out(r, "b", "x")![1]!, 2);
    }

    [Fact]
    public void Drafting_geometry_reads_lines_polylines_arcs_and_circles_at_their_views_level_and_feeds_the_tools()
    {
        var f = Site();
        var upper = Id(f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0 }));
        var plan = Id(f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = upper }));
        string Draw(JsonObject shape) => (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = shape })!["ids"]![0]!;
        var line = Draw(new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(8, 0) });
        var rect = Draw(new JsonObject { ["type"] = "polyline", ["pts"] = P([0, 2], [5, 2], [5, 6], [0, 6]), ["closed"] = true });
        var arc = Draw(new JsonObject { ["type"] = "arc", ["c"] = new JsonArray(10, 10), ["r"] = 4, ["start"] = 0, ["end"] = Math.PI / 2 });
        var circle = Draw(new JsonObject { ["type"] = "circle", ["c"] = new JsonArray(-5, 0), ["r"] = 2 });
        var text = Id(f.Run("drafting.addText", new JsonObject { ["viewId"] = plan, ["text"] = "Note", ["at"] = new JsonArray(1, 1) }));
        var wall = f.Wall(0, 0, 6, 0);

        var r = Evaluate(f, G([Picked("p", line, rect, arc, circle), N("d", "drafted"), Picked("t", text), N("dt", "drafted"), Picked("w", wall), N("dw", "drafted")],
            W("p", "ids", "d", "id"), W("t", "ids", "dt", "id"), W("w", "ids", "dw", "id")));
        var lines = Out(r, "d", "line")!.AsArray();
        Near([0, 0, 3], LineOf(lines[0])[0]);
        Near([8, 0, 3], Pt(Out(r, "d", "end")![0]));
        Assert.Null(Out(r, "d", "center")![0]);
        // The rectangle closed, its four corners once each; its end is its start.
        Assert.True((bool)lines[1]!["closed"]!);
        Assert.Equal(4, LineOf(lines[1]).Count);
        Near([0, 2, 3], Pt(Out(r, "d", "end")![1]));
        // The arc through the chord rule, from its start to its end, every point on its circle at the plan's level.
        var a = LineOf(lines[2]);
        Assert.Equal(Chords.Count(4, Math.PI / 2, 3) + 1, a.Count);
        Near([14, 10, 3], a[0]);
        Near([10, 14, 3], a[^1]);
        Assert.All(a, p => { Assert.Equal(4, Math.Sqrt((p[0] - 10) * (p[0] - 10) + (p[1] - 10) * (p[1] - 10)), 5); Assert.Equal(3, p[2], 6); });
        Near([10, 10, 3], Pt(Out(r, "d", "center")![2]));
        // The circle closed, as many sides as the rule gives.
        Assert.True((bool)lines[3]!["closed"]!);
        Assert.Equal(Chords.Circle(2), LineOf(lines[3]).Count);
        Near([-5, 0, 3], Pt(Out(r, "d", "center")![3]));
        Assert.Contains("texts, dimensions and tags", (string)NodeOf(r, "dt")["error"]!);
        Assert.Contains("is not drafted", (string)NodeOf(r, "dw")["error"]!);

        // Walls along the drafted rectangle (its segments), columns at the drafted line's division points.
        var g = G(
            [Picked("rect", rect), N("dr", "drafted"), N("corners", "corners"), N("wall", "command:architecture.addWall", new() { ["height"] = 2.8, ["join"] = false }),
             Picked("ln", line), N("dl", "drafted"), N("div", "divideLine", new() { ["count"] = 4 }), N("xyz", "xyz"), N("col", "command:structure.addColumn")],
            W("rect", "ids", "dr", "id"), W("dr", "line", "corners", "line"), W("corners", "starts", "wall", "pt:1"), W("corners", "ends", "wall", "pt:2"),
            W("ln", "ids", "dl", "id"), W("dl", "start", "div", "a"), W("dl", "end", "div", "b"), W("div", "points", "xyz", "point"), W("xyz", "x", "col", "x"), W("xyz", "y", "col", "y"));
        f.Run("graphs.bake", new JsonObject { ["graph"] = g, ["storey"] = upper });
        Assert.Equal(5, f.Model.OfClass(IfcClass.Wall).Count());
        var cols = f.Model.OfClass(IfcClass.Column).Select(c => Lapin.Model.Json.Number(c.Attrs["at"]!.AsArray()[0]) ?? -1).Order().ToList();
        Assert.Equal([0.0, 2, 4, 6, 8], cols);
    }

    [Fact]
    public void What_a_graph_makes_from_an_element_follows_it_when_it_moves()
    {
        var f = Site();
        var wall = f.Wall(0, 0, 6, 0);
        var g = G([Picked("w", wall), N("axis", "axis"), N("xyz", "xyz"), N("col", "command:structure.addColumn")],
            W("w", "ids", "axis", "id"), W("axis", "end", "xyz", "point"), W("xyz", "x", "col", "x"), W("xyz", "y", "col", "y"));
        Near([6, 0, 0], Pt(Out(Evaluate(f, g), "axis", "end")![0]));
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(wall), ["op"] = "move", ["by"] = new JsonArray(1.0, 2.0) });
        // The preview reads the wall where it is now.
        Near([7, 2, 0], Pt(Out(Evaluate(f, g), "axis", "end")![0]));
        f.Run("graphs.bake", new JsonObject { ["graph"] = g.DeepClone() });
        var at = f.Model.OfClass(IfcClass.Column).Single().Attrs["at"]!.AsArray();
        Assert.Equal([7.0, 2.0], at.Select(x => (double)x!));
    }

    [Fact]
    public void A_commands_list_argument_takes_the_whole_list_once_and_maps_only_over_a_list_of_lists()
    {
        var f = Site();
        var cols = Enumerable.Range(0, 4).Select(i => Id(f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = i * 5.0, ["y"] = 0 }))).ToList();
        // Filmed: 12 picked columns into Pad footings under columns answered „11 of 12 failed”, mapped column by column. The
        // node here does not say ids is whole (a graph made by a script): the engine knows the argument is a list.
        var g = G([Picked("c", [.. cols]), N("pads", "command:structure.padsUnderColumns")], W("c", "ids", "pads", "ids"));
        var r = Evaluate(f, g);
        Assert.Null(NodeOf(r, "pads")["error"]);
        Assert.Null(NodeOf(r, "pads")["warning"]);
        Assert.Equal(1, (int)r["calls"]!);
        Assert.Equal(4, Out(r, "pads", "ids")!.AsArray().Count);
        Assert.True(Evaluator.ListArgument("ids") && Evaluator.ListArgument("storeyIds") && Evaluator.ListArgument("points") && !Evaluator.ListArgument("id"));

        // A list of lists: one run for each list.
        r = Evaluate(f, G([N("pads", "command:structure.padsUnderColumns", new() { ["ids"] = new JsonArray(new JsonArray(cols[0], cols[1]), new JsonArray(cols[2], cols[3])) })]));
        Assert.Equal(2, (int)r["calls"]!);
        Assert.Equal(2, (int)NodeOf(r, "pads")["items"]!);
        Assert.All(Out(r, "pads", "ids")!.AsArray(), made => Assert.Equal(2, made!.AsArray().Count));

        // Points written as [x, y] are one list of points, not a list of lists to map over: one floor.
        r = Evaluate(f, G([N("slab", "command:architecture.addSlab", new() { ["points"] = P([20, 0], [26, 0], [26, 4], [20, 4]) })]));
        Assert.Equal(1, (int)r["calls"]!);
        Assert.Null(NodeOf(r, "slab")["error"]);
    }

    // ── stage 2: the palette, format 2, groups (decision 536) ──

    /// <summary>Walls() with its count an input: Number „Walls” → the series' count, the wall's height a Number too.</summary>
    static JsonObject PaletteWalls()
    {
        var g = Walls();
        var nodes = g["nodes"]!.AsArray();
        nodes.Add(new JsonObject { ["id"] = "count", ["kind"] = "integer", ["x"] = 0, ["y"] = 0, ["values"] = new JsonObject { ["value"] = 3 }, ["label"] = "Walls",
            ["palette"] = new JsonObject { ["min"] = 1, ["max"] = 10, ["order"] = 1, ["junk"] = "x" } });
        nodes.Add(N("h", "number", new() { ["value"] = 2.8, ["unit"] = "length" }));
        nodes.Add(N("total", "count"));
        nodes.Add(N("watch", "watch"));
        g["wires"]!.AsArray().Add(W("count", "value", "xs", "count"));
        g["wires"]!.AsArray().Add(W("h", "value", "wall", "height"));
        g["wires"]!.AsArray().Add(W("wall", "ids", "total", "list"));
        g["wires"]!.AsArray().Add(W("total", "count", "watch", "value"));
        g["format"] = 2;
        g["tool"] = new JsonObject { ["show"] = true, ["name"] = "Row of walls", ["icon"] = "wall", ["group"] = "Graphs", ["other"] = 1 };
        return g;
    }

    [Fact]
    public void A_graph_of_format_1_is_read_and_written_as_format_2_and_a_newer_one_refused()
    {
        var one = Walls();
        Assert.Equal(1, (int)one["format"]!);
        var doc = GraphDoc.Read(one);
        var two = doc.ToJson();
        Assert.Equal(2, (int)two["format"]!);
        Assert.Equal(4, two["nodes"]!.AsArray().Count);
        Assert.Null(two["tool"]);
        // What format 1 did not have is not read from it: a group refused, palette settings left out.
        var withGroup = G([N("g", "group")]);
        Assert.Contains("format 1", Assert.Throws<CommandException>(() => GraphDoc.Read(withGroup)).Message);
        var settings = Walls();
        settings["nodes"]![0]!["palette"] = new JsonObject { ["hide"] = true };
        Assert.Null(GraphDoc.Read(settings).Nodes[0].Palette);
        var newer = Walls();
        newer["format"] = 3;
        Assert.Contains("newer Lapin", Assert.Throws<CommandException>(() => GraphDoc.Read(newer)).Message);
        // A format-1 graph evaluates as before.
        var f = Project();
        Assert.Equal(3, Out(Evaluate(f, Walls()), "wall", "ids")!.AsArray().Count);
    }

    [Fact]
    public void Palette_settings_and_the_tool_are_kept_with_the_graph_only_what_is_known()
    {
        var f = Project();
        var id = (string)f.Run("graphs.save", new JsonObject { ["name"] = "Row", ["graph"] = PaletteWalls() })!["id"]!;
        var g = f.Session.Query("graphs.get", new JsonObject { ["id"] = id })!["graph"]!;
        Assert.Equal(2, (int)g["format"]!);
        Assert.Equal("""{"show":true,"name":"Row of walls","icon":"wall","group":"Graphs"}""", g["tool"]!.ToJsonString());
        var count = g["nodes"]!.AsArray().First(n => (string)n!["id"]! == "count")!;
        Assert.Equal("""{"order":1,"min":1,"max":10}""", count["palette"]!.ToJsonString());
    }

    [Fact]
    public void A_palettes_values_preview_without_a_step_and_are_written_into_the_graph_as_one()
    {
        var f = Project();
        var id = (string)f.Run("graphs.save", new JsonObject { ["name"] = "Row", ["graph"] = PaletteWalls() })!["id"]!;
        var undo = f.Session.History.UndoLabel;
        var revision = f.Model.Revision;
        // Changed in the palette: the preview takes the values, the graph and the model stay as they were.
        var r = f.Session.Query("graphs.evaluate", new JsonObject { ["id"] = id, ["values"] = new JsonObject { ["count"] = 5, ["h"] = 3.1 } })!.AsObject();
        Assert.Equal(5, (double)Out(r, "watch", "value")!);
        Assert.Equal(5, Out(r, "wall", "ids")!.AsArray().Count);
        Assert.Equal(revision, f.Model.Revision);
        Assert.Equal(undo, f.Session.History.UndoLabel);
        Assert.Equal(3, (double)Out(f.Session.Query("graphs.evaluate", new JsonObject { ["id"] = id })!.AsObject(), "watch", "value")!);

        // Let go: written into the graph's input nodes, one step.
        f.Run("graphs.set", new JsonObject { ["id"] = id, ["values"] = new JsonObject { ["count"] = 4 } });
        Assert.Equal("Change graph inputs", f.Session.History.UndoLabel);
        Assert.Equal(4, (double)Out(f.Session.Query("graphs.evaluate", new JsonObject { ["id"] = id })!.AsObject(), "watch", "value")!);
        f.Session.Undo();
        Assert.Equal(3, (double)Out(f.Session.Query("graphs.evaluate", new JsonObject { ["id"] = id })!.AsObject(), "watch", "value")!);

        // Only an input node takes a value, and only one of its kind.
        Assert.Contains("not an input", Assert.Throws<CommandException>(() => f.Run("graphs.set", new JsonObject { ["id"] = id, ["values"] = new JsonObject { ["wall"] = 1 } })).Message);
        Assert.Contains("does not take", Assert.Throws<CommandException>(() => f.Run("graphs.set", new JsonObject { ["id"] = id, ["values"] = new JsonObject { ["count"] = 2.5 } })).Message);
        Assert.Contains("no node", Assert.Throws<CommandException>(() => f.Run("graphs.set", new JsonObject { ["id"] = id, ["values"] = new JsonObject { ["nope"] = 1 } })).Message);

        // Bake from the palette: its values written and the walls made in one step; Undo takes both back.
        f.Run("graphs.bake", new JsonObject { ["id"] = id, ["values"] = new JsonObject { ["count"] = 2 } });
        Assert.Equal("Bake graph", f.Session.History.UndoLabel);
        Assert.Equal(2, f.Model.OfClass(IfcClass.Wall).Count());
        Assert.Equal(2, Lapin.Model.Json.Number(GraphDoc.Read(f.Model.Get(id)!.Attrs["graph"]).Find("count")!.Values["value"]));
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(IfcClass.Wall));
        Assert.Equal(3, Lapin.Model.Json.Number(GraphDoc.Read(f.Model.Get(id)!.Attrs["graph"]).Find("count")!.Values["value"]));
    }

    [Fact]
    public void A_type_node_gives_the_type_chosen_else_the_first_of_its_class_by_name()
    {
        var f = Project();
        var thin = f.Type("B thin", ("Brick", 0.1, true));
        var thick = f.Type("A thick", ("Brick", 0.3, true));
        Assert.Equal(thick, (string)Out(Evaluate(f, G([N("t", "type")])), "t", "value")!);
        Assert.Equal(thin, (string)Out(Evaluate(f, G([N("t", "type", new() { ["value"] = thin })])), "t", "value")!);
        Assert.Contains("no type", (string)NodeOf(Evaluate(f, G([N("t", "type", new() { ["class"] = IfcClass.StairType })])), "t")["error"]!);
        // Into a wall's Type: the wall is of it.
        var g = Walls();
        g["nodes"]!.AsArray().Add(N("t", "type", new() { ["value"] = thin }));
        g["wires"]!.AsArray().Add(W("t", "value", "wall", "typeId"));
        f.Run("graphs.bake", new JsonObject { ["graph"] = g });
        Assert.All(f.Model.OfClass(IfcClass.Wall), w => Assert.Equal(thin, f.Model.RelatingOf(w.Id, IfcClass.RelDefinesByType)?.Id));
    }

    /// <summary>Walls() with its series and two points collapsed into the group „row”: the series' count fed from outside, the points read out.</summary>
    static JsonObject Grouped()
    {
        var inner = G([N("xs", "series", new() { ["start"] = 0, ["step"] = 2 }), N("a", "point"), N("b", "point", new() { ["y"] = 4 })],
            W("xs", "list", "a", "x"), W("xs", "list", "b", "x"));
        inner.Remove("format");
        var group = new JsonObject
        {
            ["id"] = "row", ["kind"] = "group", ["x"] = 0, ["y"] = 0, ["label"] = "Row", ["graph"] = inner,
            ["inputs"] = new JsonArray(new JsonObject { ["node"] = "xs", ["port"] = "count", ["label"] = "How many" }),
            ["outputs"] = new JsonArray(new JsonObject { ["node"] = "a", ["port"] = "point" }, new JsonObject { ["node"] = "b", ["port"] = "point" }),
        };
        var g = G([N("n", "integer", new() { ["value"] = 3 }), group, N("wall", "command:architecture.addWall", new() { ["height"] = 2.8 })],
            W("n", "value", "row", "xs.count"), W("row", "a.point", "wall", "pt:1"), W("row", "b.point", "wall", "pt:2"));
        g["format"] = 2;
        return g;
    }

    [Fact]
    public void A_group_evaluates_as_its_nodes_would_outside_and_keeps_their_results_by_path()
    {
        var f = Project();
        var r = Evaluate(f, Grouped());
        Assert.Equal(3, Out(r, "wall", "ids")!.AsArray().Count);
        Assert.Equal(3, Out(r, "row", "a.point")!.AsArray().Count);
        Assert.Equal([0.0, 2, 4], Numbers(Out(r, "row/xs", "list")));
        // The points inside are drawn in the ghost, as outside.
        Assert.Equal(6 * 3, r["ghost"]!["points"]!.AsArray().Count);
        // Written and read again as it was; baked in one step, the same walls as the graph unfolded.
        Assert.Equal(GraphDoc.Read(Grouped()).ToJson().ToJsonString(), GraphDoc.Read(GraphDoc.Read(Grouped()).ToJson()).ToJson().ToJsonString());
        f.Run("graphs.bake", new JsonObject { ["graph"] = Grouped() });
        Assert.Equal([0.0, 2, 4], f.Model.OfClass(IfcClass.Wall).Select(w => Lapin.Model.Json.Number(w.Attrs["start"]!.AsArray()[0]) ?? -1).Order());

        // A palette's value reaches a node inside a group by its path.
        Assert.Contains("not an input", Assert.Throws<CommandException>(() => GraphDoc.Read(Grouped()).WithValues(new JsonObject { ["row/xs"] = 4 })).Message);
        var withInput = Grouped();
        withInput["nodes"]![1]!["graph"]!["nodes"]!.AsArray().Add(N("y", "number", new() { ["value"] = 4 }));
        withInput["nodes"]![1]!["graph"]!["wires"]!.AsArray().Add(W("y", "value", "b", "y"));
        var moved = GraphDoc.Read(withInput).WithValues(new JsonObject { ["row/y"] = 7 });
        Assert.Equal(7, Lapin.Model.Json.Number(moved.Find("row/y")!.Values["value"]));

        // A failure inside shows on its node, and on the group as a warning or, when nothing comes out, an error.
        var bad = Grouped();
        bad["wires"]!.AsArray()[0] = W("n", "value", "row", "xs.count");
        bad["nodes"]![0]!["values"] = new JsonObject { ["value"] = -1 };
        var rb = Evaluate(f, bad);
        Assert.NotNull(NodeOf(rb, "row/xs")["error"]);
        Assert.Contains("inside failed", (string)NodeOf(rb, "row")["error"]!);
        Assert.Contains("not baked", Assert.Throws<CommandException>(() => f.Run("graphs.bake", new JsonObject { ["graph"] = bad })).Message);
    }

    [Fact]
    public void A_group_is_checked_its_ports_name_nodes_it_holds_and_a_wire_names_a_port_it_has()
    {
        var unknownPort = Grouped();
        unknownPort["wires"]!.AsArray()[0] = W("n", "value", "row", "xs.start2");
        Assert.Contains("no input", Assert.Throws<CommandException>(() => GraphDoc.Read(unknownPort)).Message);
        var missing = Grouped();
        missing["nodes"]![1]!["inputs"]!.AsArray().Add(new JsonObject { ["node"] = "zz", ["port"] = "x" });
        Assert.Contains("does not hold", Assert.Throws<CommandException>(() => GraphDoc.Read(missing)).Message);
        var wiredInside = Grouped();
        wiredInside["nodes"]![1]!["inputs"]!.AsArray().Add(new JsonObject { ["node"] = "a", ["port"] = "x" });
        Assert.Contains("wired inside", Assert.Throws<CommandException>(() => GraphDoc.Read(wiredInside)).Message);
    }
}
