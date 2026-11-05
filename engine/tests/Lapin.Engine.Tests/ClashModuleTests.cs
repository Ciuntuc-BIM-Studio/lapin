using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Clash;
using Lapin.Mep;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Cde.Remarks;
using Lapin.Drawing.Views;

namespace Lapin.Engine.Tests;

/// <summary>
/// The clash module on a Lapin model (docs/plan-visualisation.md, stage 2): the sets and tests a project starts with,
/// a pipe through a wall found, followed from run to run (new, active, resolved, open again), pairs meant to meet left
/// out (a wall and its door, walls joined at a corner), set rules on Lapin's fields, remarks made of clashes, and a run
/// out of date once the model changes.
/// </summary>
public class ClashModuleTests
{
    sealed class F
    {
        public readonly Session Session;
        public F()
        {
            ILapinModule[] use = [new ArchitectureModule(), new MepModule(), new ClashModule()];
            Session = new Session(ModuleCatalog.Create(use), new ProductManifest("test", "Test", ["core", .. use.Select(m => m.Id)]));
        }
        public IModelView Model => Session.Model;
        public string Storey => Spatial.Storeys(Model).First().Id;
        public JsonNode? Run(string c, JsonObject a) { Session.Execute(c, a, out var r); return r; }
        public JsonNode? Query(string q, JsonObject? a = null) => Session.Query(q, a ?? []);
        public string Wall(double x1, double y1, double x2, double y2, bool join = true) =>
            (string)Run("architecture.addWall", new JsonObject { ["storeyId"] = Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["join"] = join })!["id"]!;
        /// <summary>A heating pipe from (x, −1) to (x, 3) at 1 m, 160 mm, no insulation: across a wall along y = 1.</summary>
        public string Pipe(double x)
        {
            var system = (string)Run("mep.createSystem", new JsonObject { ["service"] = Services.Heating, ["defaults"] = new JsonObject { ["insulation"] = 0 } })!["id"]!;
            var run = (string)Run("mep.drawRun", new JsonObject { ["systemId"] = system, ["points"] = new JsonArray(new JsonArray(x, -1), new JsonArray(x, 3)), ["pair"] = false })!["id"]!;
            Session.Edit("set", e => { var p = e.Model.Get(run)!; var a = p.CloneAttrs(); a["size"] = 0.16; a["elevation"] = 1.0; e.Replace(p.With(attrs: a)); });
            return run;
        }
        public JsonObject List() => (JsonObject)Query("clash.list")!;
        public string TestId(string name) => (string)List()["tests"]!.AsArray().OfType<JsonObject>().Single(t => (string?)t["name"] == name)["id"]!;
        public string SetId(string name) => (string)List()["sets"]!.AsArray().OfType<JsonObject>().Single(t => (string?)t["name"] == name)["id"]!;
        public List<JsonObject> Clashes(string test) => ((JsonArray)((JsonObject)Query("clash.results", new JsonObject { ["id"] = test })!)["clashes"]!).OfType<JsonObject>().ToList();
    }

    [Fact]
    public void A_project_starts_with_three_sets_three_hard_tests_at_5_mm_and_the_openings_check()
    {
        var f = new F();
        f.Run("clash.seed", new JsonObject());
        var list = f.List();
        Assert.Equal(["Architecture", "Services", "Structure"], list["sets"]!.AsArray().Select(s => (string)s!["name"]!));
        var tests = list["tests"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal(4, tests.Count);
        Assert.Equal("openings", (string?)tests.Single(t => (string?)t["name"] == "Openings for MEP")["kind"]);
        Assert.All(tests.Where(t => (string?)t["kind"] != "openings"), t => { Assert.Equal("hard", (string?)t["kind"]); Assert.Equal(0.005, (double)t["tolerance"]!); });
        Assert.Null(tests.Single(t => (string?)t["name"] == "Services × Services")["setB"]);
        // Asked again with sets in the project: nothing more.
        f.Run("clash.seed", new JsonObject());
        Assert.Equal(4, f.List()["tests"]!.AsArray().Count);
    }

    [Fact]
    public void A_pipe_through_a_wall_is_found_and_followed_from_run_to_run()
    {
        var f = new F();
        var wall = f.Wall(-2, 1, 8, 1, join: false);
        var pipe = f.Pipe(0);
        f.Run("clash.seed", new JsonObject());
        var test = f.TestId("Architecture × Services");
        var summary = (JsonArray)f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(test) })!;
        Assert.Equal(1, (int)summary[0]!["found"]!);
        var c = Assert.Single(f.Clashes(test));
        Assert.Equal(1, (int)c["n"]!);
        Assert.Equal("new", (string?)c["status"]);
        Assert.Equal(wall, (string?)c["a"]);
        Assert.Equal(pipe, (string?)c["b"]);
        Assert.True((bool)c["crossing"]!);
        // As deep as the pipe is wide where it passes (160 mm), at its axis 1 m up.
        Assert.Equal(0.16, (double)c["d"]!, 0.01);
        Assert.Equal(1.0, (double)c["at"]![2]!, 0.05);
        Assert.Equal("Wall", (string?)c["aClass"]);
        // Found again: active, the same number.
        f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(test) });
        Assert.Equal("active", (string?)Assert.Single(f.Clashes(test))["status"]);
        // Reviewed by hand, with who has it: kept by the next run.
        f.Run("clash.setStatus", new JsonObject { ["id"] = test, ["numbers"] = new JsonArray(1), ["status"] = "reviewed", ["assignedTo"] = "MEP", ["notes"] = "Sleeve to come" });
        f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(test) });
        var kept = Assert.Single(f.Clashes(test));
        Assert.Equal("reviewed", (string?)kept["status"]);
        Assert.Equal("MEP", (string?)kept["assignedTo"]);
        // The pipe moved clear of the wall: resolved; back: open again, still number 1.
        f.Session.Edit("move", e => { var p = e.Model.Get(pipe)!; var a = p.CloneAttrs(); a["elevation"] = 4.0; e.Replace(p.With(attrs: a)); });
        Assert.True((bool)f.List()["tests"]!.AsArray().OfType<JsonObject>().Single(t => (string?)t["id"] == test)["stale"]!);
        f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(test) });
        var resolved = Assert.Single(f.Clashes(test));
        Assert.Equal("resolved", (string?)resolved["status"]);
        Assert.NotNull(resolved["resolved"]);
        f.Session.Edit("back", e => { var p = e.Model.Get(pipe)!; var a = p.CloneAttrs(); a["elevation"] = 1.0; e.Replace(p.With(attrs: a)); });
        f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(test) });
        var again = Assert.Single(f.Clashes(test));
        Assert.Equal(1, (int)again["n"]!);
        Assert.Equal("active", (string?)again["status"]);
        Assert.False((bool)f.List()["tests"]!.AsArray().OfType<JsonObject>().Single(t => (string?)t["id"] == test)["stale"]!);
    }

    [Fact]
    public void A_wall_and_its_door_and_walls_joined_at_a_corner_are_meant_to_meet()
    {
        var f = new F();
        var a = f.Wall(0, 0, 6, 0);
        var b = f.Wall(6, 0, 6, 4);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = a, ["x"] = 2, ["y"] = 0 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = b, ["x"] = 6, ["y"] = 2 });
        f.Run("clash.seed", new JsonObject());
        var arch = f.SetId("Architecture");
        var self = (string)f.Run("clash.setTest", new JsonObject { ["setA"] = arch, ["kind"] = "hard", ["tolerance"] = 0.001 })!["id"]!;
        f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(self) });
        Assert.Empty(f.Clashes(self));
        // A wall laid across the first, not joined to it: a clash.
        var across = f.Wall(3, -1, 3, 1, join: false);
        f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(self) });
        var c = Assert.Single(f.Clashes(self));
        Assert.Contains(across, new[] { (string)c["a"]!, (string)c["b"]! });
    }

    [Fact]
    public void Search_sets_read_Lapin_fields_and_name_their_elements()
    {
        var f = new F();
        f.Wall(0, 0, 6, 0);
        f.Wall(6, 0, 6, 4);
        var ground = Spatial.Storeys(f.Model).First().Name!;
        var up = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor" })!["id"]!;
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = up, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 });
        f.Pipe(1);
        int Count(JsonObject set) => (int)((JsonObject)f.Query("clash.members", new JsonObject { ["set"] = set })!)["count"]!;
        JsonObject Rule(string field, string op, params string[] values) => new() { ["field"] = field, ["op"] = op, ["values"] = new JsonArray(values.Select(v => (JsonNode)v).ToArray()) };
        JsonObject Set(bool all, params JsonObject[] rules) => new() { ["matchAll"] = all, ["rules"] = new JsonArray(rules) };
        Assert.Equal(3, Count(Set(false, Rule("class", "is", "Wall"))));
        Assert.Equal(3, Count(Set(false, Rule("class", "is", "IfcWall"))));
        Assert.Equal(2, Count(Set(true, Rule("class", "is", "wall"), Rule("storey", "is", ground))));
        // A "not" rule takes out, even in a set of any rule.
        Assert.Equal(1, Count(Set(false, Rule("class", "isOneOf", "IfcWall", "IfcPipeSegment"), Rule("storey", "isNot", ground))));
        Assert.Equal(1, Count(Set(false, Rule("class", "contains", "Pipe"))));
        // No adding rule: everything (the walls and the pipe), less what a not rule takes out.
        Assert.Equal(4, Count(Set(false)));
        Assert.Equal(3, Count(Set(false, Rule("class", "isNot", "IfcPipeSegment"))));
        // A number on the pipe's size: 160 mm (0.16 m).
        Assert.Equal(1, Count(Set(false, Rule("parameter", "atLeast", "0.1") .Also("parameter", "size"))));
        // The values a field takes, for a rule's picker.
        var values = ((JsonArray)f.Query("clash.values", new JsonObject { ["field"] = "storey" })!).OfType<JsonObject>().ToList();
        Assert.Equal(3, (int)values.Single(v => (string?)v["value"] == ground)["count"]!);
    }

    [Fact]
    public void Clashes_become_remarks_of_type_clash_made_once_and_updated()
    {
        var f = new F();
        f.Wall(-2, 1, 8, 1, join: false);
        var pipe = f.Pipe(0);
        f.Run("clash.seed", new JsonObject());
        var test = f.TestId("Architecture × Services");
        f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(test) });
        var made = (JsonObject)f.Run("clash.remarks", new JsonObject { ["id"] = test })!;
        Assert.Equal(1, (int)made["added"]!);
        var remark = Remarks.Read(Assert.Single(f.Model.OfClass(Remarks.Class)));
        Assert.Equal("Clash", remark.Type);
        Assert.StartsWith("Clash 1:", remark.Title);
        Assert.Contains(pipe, Assert.Single(remark.Viewpoints).Selection);
        Assert.NotNull(Assert.Single(f.Clashes(test))["remark"]);
        var again = (JsonObject)f.Run("clash.remarks", new JsonObject { ["id"] = test })!;
        Assert.Equal(0, (int)again["added"]!);
        Assert.Equal(1, (int)again["updated"]!);
    }

    [Fact]
    public void A_test_changed_in_its_sets_starts_afresh_and_a_used_set_is_not_deleted()
    {
        var f = new F();
        f.Wall(-2, 1, 8, 1, join: false);
        f.Pipe(0);
        f.Run("clash.seed", new JsonObject());
        var test = f.TestId("Architecture × Services");
        f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(test) });
        Assert.Single(f.Clashes(test));
        Assert.Throws<CommandException>(() => f.Run("clash.deleteSet", new JsonObject { ["id"] = f.SetId("Services") }));
        f.Run("clash.setTest", new JsonObject { ["id"] = test, ["kind"] = "clearance", ["tolerance"] = 0.05 });
        Assert.Empty(f.Clashes(test));
        // Run all takes every test that is on.
        var all = (JsonArray)f.Run("clash.run", new JsonObject())!;
        Assert.Equal(4, all.Count);
    }
}

public class ClashPartBTests
{
    sealed class F
    {
        public readonly Session Session;
        public F()
        {
            ILapinModule[] use = [new ArchitectureModule(), new MepModule(), new ClashModule()];
            Session = new Session(ModuleCatalog.Create(use), new ProductManifest("test", "Test", ["core", .. use.Select(m => m.Id)]));
        }
        public IModelView Model => Session.Model;
        public string Storey => Spatial.Storeys(Model).First().Id;
        public JsonNode? Run(string c, JsonObject a) { Session.Execute(c, a, out var r); return r; }
        public JsonNode? Query(string q, JsonObject? a = null) => Session.Query(q, a ?? []);
        public string Wall(double x1, double y1, double x2, double y2) =>
            (string)Run("architecture.addWall", new JsonObject { ["storeyId"] = Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["join"] = false })!["id"]!;
        public string Pipe(double x)
        {
            var system = (string)Run("mep.createSystem", new JsonObject { ["service"] = Services.Heating, ["defaults"] = new JsonObject { ["insulation"] = 0 } })!["id"]!;
            var run = (string)Run("mep.drawRun", new JsonObject { ["systemId"] = system, ["points"] = new JsonArray(new JsonArray(x, -1), new JsonArray(x, 3)), ["pair"] = false })!["id"]!;
            Session.Edit("set", e => { var p = e.Model.Get(run)!; var a = p.CloneAttrs(); a["size"] = 0.16; a["elevation"] = 1.0; e.Replace(p.With(attrs: a)); });
            return run;
        }
        public string Test(string name)
        {
            Run("clash.seed", new JsonObject());
            return (string)((JsonObject)Query("clash.list")!)["tests"]!.AsArray().OfType<JsonObject>().Single(t => (string?)t["name"] == name)["id"]!;
        }
        public List<JsonObject> Results(string test, string? groupBy = null) =>
            ((JsonArray)((JsonObject)Query("clash.results", new JsonObject { ["id"] = test, ["groupBy"] = groupBy })!)["clashes"]!).OfType<JsonObject>().ToList();
        public byte[] File(string test, string format, string? groupBy = null) =>
            Convert.FromBase64String((string)((JsonObject)Query("clash.reportFile", new JsonObject { ["id"] = test, ["format"] = format, ["groupBy"] = groupBy })!)["data"]!);
    }

    [Fact]
    public void The_openings_check_is_a_test_too_its_clashes_followed_like_the_others()
    {
        var f = new F();
        var wall = f.Wall(-2, 1, 8, 1);
        var pipe = f.Pipe(0);
        var test = f.Test("Openings for MEP");
        f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(test) });
        var c = Assert.Single(f.Results(test));
        Assert.Equal(wall, (string?)c["a"]);
        Assert.Equal(pipe, (string?)c["b"]);
        Assert.Equal("new", (string?)c["status"]);
        // The pipe's opening cut by the cut order: no longer a clash.
        f.Run("cutOrder.setMep", new JsonObject { ["cut"] = true });
        f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(test) });
        Assert.Equal("resolved", (string?)Assert.Single(f.Results(test))["status"]);
    }

    [Fact]
    public void Clashes_group_by_element_storey_or_classes_and_go_out_as_navisworks_xml_and_pdf()
    {
        var f = new F();
        var wall = f.Wall(-2, 1, 8, 1);
        var other = f.Wall(-2, 2.5, 8, 2.5);
        f.Pipe(0);
        f.Pipe(4);
        var test = f.Test("Architecture × Services");
        f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(test) });
        Assert.Equal(4, f.Results(test).Count);
        // By element: each wall takes its two (the one in most first), named as the wall.
        var byElement = f.Results(test, "element");
        Assert.Equal(2, byElement.Select(c => (string?)c["group"]).Distinct().Count());
        // The first group's two clashes share their element.
        var first = byElement.Take(2).ToList();
        Assert.Single(new[] { (string)first[0]["a"]!, (string)first[0]["b"]! }.Intersect(new[] { (string)first[1]["a"]!, (string)first[1]["b"]! }));
        Assert.Single(f.Results(test, "classes").Select(c => (string?)c["group"]).Distinct());
        // A third run through only one wall: alone, in Other clashes, last.
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 8, ["y1"] = -2, ["x2"] = 8, ["y2"] = 4, ["join"] = false });
        var system = (string)f.Run("mep.createSystem", new JsonObject { ["service"] = Services.Heating })!["id"]!;
        var lone = (string)f.Run("mep.drawRun", new JsonObject { ["systemId"] = system, ["points"] = new JsonArray(new JsonArray(7, 0), new JsonArray(9, 0)), ["pair"] = false })!["id"]!;
        f.Session.Edit("set", e => { var p = e.Model.Get(lone)!; var a = p.CloneAttrs(); a["size"] = 0.16; a["elevation"] = 1.0; e.Replace(p.With(attrs: a)); });
        f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(test) });
        var regrouped = f.Results(test, "element");
        Assert.Equal(5, regrouped.Count);
        Assert.Equal("Other clashes", (string?)regrouped[^1]["group"]);
        Assert.Single(f.Results(test, "storey").Select(c => (string?)c["group"]).Distinct());
        var xml = System.Text.Encoding.UTF8.GetString(f.File(test, "xml", "element"));
        // Two walls of two runs each, the lone run apart.
        Assert.Contains("<exchange units=\"m\"", xml);
        Assert.Contains("<clashtest name=\"Architecture × Services\"", xml);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(xml, "<clashgroup ").Count);
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(xml, "<clashresult ").Count);
        // A crossing's distance written negative, as Navisworks writes a hard clash's.
        Assert.Matches("distance=\"-0\\.1[56]", xml);
        Assert.Contains(wall, xml);
        Assert.Contains(other, xml);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(f.File(test, "pdf", "storey"), 0, 4));
    }

    [Fact]
    public void Open_clashes_are_red_clouds_in_a_plan_that_asks_labelled_by_test_and_number()
    {
        var f = new F();
        f.Wall(-2, 1, 8, 1);
        var pipe = f.Pipe(0);
        var test = f.Test("Architecture × Services");
        f.Run("clash.run", new JsonObject { ["ids"] = new JsonArray(test) });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        List<ViewMark> Clouds() => Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!).Marks.Where(m => m.Data?["test"] != null).ToList();
        Assert.Empty(Clouds());
        f.Run("coordination.markView", new JsonObject { ["id"] = plan, ["on"] = true });
        var cloud = Assert.Single(Clouds());
        Assert.Equal(pipe, cloud.Owner);
        // "Architecture × Services" is the first test by name: 1.1.
        Assert.Contains("1.1", Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!).Pens.SelectMany(p => p.Texts).Select(t => t.Text));
        // Reviewed: no longer marked.
        f.Run("clash.setStatus", new JsonObject { ["id"] = test, ["numbers"] = new JsonArray(1), ["status"] = "reviewed" });
        Assert.Empty(Clouds());
    }
}

static class RuleExtensions
{
    /// <summary>A rule with one more key (a parameter's name).</summary>
    public static JsonObject Also(this JsonObject rule, string key, string value) { rule[key] = value; return rule; }
}
