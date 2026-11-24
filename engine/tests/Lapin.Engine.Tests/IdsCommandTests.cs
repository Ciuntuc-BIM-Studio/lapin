using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform.Cde;
using Lapin.Platform.Cde.Remarks;
using Lapin.Protocol;
using Lapin.Structure;
using Lapin.Host;

namespace Lapin.Engine.Tests;

/// <summary>
/// The IDS calls over a project (docs/ids.md): an .ids kept in the project in one undo step, refused when it is not IDS 1.0, run over
/// the model as its IFC export writes it (Lapin's own sets, its types, materials and layer sets addressable), its results, remarks made
/// in one step and brought up to date by a run again, the report, the project's rule, and the Bloc-P-4 timing.
/// </summary>
public class IdsCommandTests(Xunit.Abstractions.ITestOutputHelper output)
{
    static JsonObject Call(EngineDispatcher d, string method, JsonObject? p = null)
    {
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p ?? [] }.ToJsonString());
        var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
        if (o["error"] is { } err) throw new InvalidOperationException((string)err!);
        return o["result"] as JsonObject ?? [];
    }

    static string Failure(EngineDispatcher d, string method, JsonObject? p = null)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Call(d, method, p));
        return ex.Message;
    }

    const string Head = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><ids xmlns=\"http://standards.buildingsmart.org/IDS\" xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"><info><title>Office rules</title><version>1</version></info><specifications>";
    const string Tail = "</specifications></ids>";

    static string Spec(string name, string id, string app, string req) =>
        $"<specification name=\"{name}\" identifier=\"{id}\" ifcVersion=\"IFC4\"><applicability minOccurs=\"1\" maxOccurs=\"unbounded\">{app}</applicability><requirements>{req}</requirements></specification>";
    const string Walls = "<entity><name><simpleValue>IFCWALL</simpleValue></name></entity>";

    static readonly string Rules = Head +
        Spec("Walls are named", "W-01", Walls, "<attribute><name><simpleValue>Name</simpleValue></name></attribute>") +
        Spec("Walls have a material", "W-02", Walls, "<material/>") +
        Spec("Walls carry Lapin's intent", "W-03", Walls, "<property><propertySet><simpleValue>Lapin_Intent</simpleValue></propertySet><baseName><xs:restriction base=\"xs:string\"><xs:pattern value=\".+\"/></xs:restriction></baseName></property>") +
        Spec("Walls say whether they are external, if they do as a boolean", "W-04", Walls, "<property dataType=\"IFCBOOLEAN\" cardinality=\"optional\"><propertySet><simpleValue>Pset_WallCommon</simpleValue></propertySet><baseName><simpleValue>IsExternal</simpleValue></baseName></property>") +
        Spec("Walls have a code", "W-05", Walls, "<property><propertySet><simpleValue>MyOffice_Data</simpleValue></propertySet><baseName><simpleValue>Code</simpleValue></baseName></property>") +
        Spec("Walls are on a storey", "W-06", Walls, "<partOf relation=\"IFCRELCONTAINEDINSPATIALSTRUCTURE\"><entity><name><simpleValue>IFCBUILDINGSTOREY</simpleValue></name></entity></partOf>") +
        Tail;

    static (Fixture F, EngineDispatcher D, string Wall, string Other) Project()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var type = f.Type("Brick 250", ("Brick", 0.25, true));
        var wall = f.Wall(0, 0, 5, 0, type);
        var other = f.Wall(5, 0, 5, 4, type);
        return (f, new EngineDispatcher(f.Session, PackageFiles.Create()), wall, other);
    }

    static JsonObject Spec(JsonObject run, string identifier) =>
        run["files"]!.AsArray().SelectMany(x => x!["specifications"]!.AsArray()).OfType<JsonObject>().Single(s => (string?)s["identifier"] == identifier);

    [Fact]
    public void An_IDS_is_kept_in_the_project_in_one_undo_step_and_a_file_that_is_not_one_is_refused()
    {
        var (f, d, _, _) = Project();
        var added = Call(d, "ids.add", new JsonObject { ["text"] = Rules, ["name"] = "office.ids" });
        var id = (string)added["id"]!;
        Assert.True(GlobalId.IsValid(id));
        Assert.Equal(("Office rules", 6), ((string?)added["title"], (int)added["specifications"]!));
        Assert.Equal(6, added["ids"]!["specifications"]!.AsArray().Count);
        Assert.Single(f.Model.OfClass(IdsFiles.Class));
        Assert.Equal("Add IDS file", f.Session.StateFor(Lapin.Model.History.Me).UndoLabel);
        // The same text again: refused. A newer copy of the same file: the same id.
        Assert.Contains("already", Failure(d, "ids.add", new JsonObject { ["text"] = Rules, ["name"] = "other.ids" }));
        var newer = Call(d, "ids.add", new JsonObject { ["text"] = Rules.Replace("<version>1</version>", "<version>2</version>"), ["name"] = "office.ids" });
        Assert.Equal(id, (string)newer["id"]!);
        Assert.Equal("2", (string?)newer["version"]);
        Assert.Single(f.Model.OfClass(IdsFiles.Class));
        // Not IDS: every problem said, nothing kept.
        var why = Failure(d, "ids.add", new JsonObject { ["text"] = Head + "<specification name=\"x\" ifcVersion=\"IFC9\"><applicability/></specification>" + Tail, ["name"] = "bad.ids" });
        Assert.Contains("Not an IDS 1.0 file", why);
        Assert.Contains("IFC9", why);
        Assert.Single(f.Model.OfClass(IdsFiles.Class));
        // Read back as the file says; listed; removed in one step.
        var read = Call(d, "ids.read", new JsonObject { ["id"] = id });
        Assert.Equal("Walls are named", (string?)read["ids"]!["specifications"]![0]!["name"]);
        Assert.Single(Call(d, "doc.query", new JsonObject { ["name"] = "ids.files" })["files"]!.AsArray());
        f.Session.Undo();
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(IdsFiles.Class));
    }

    [Fact]
    public void A_servers_IDS_files_come_in_together_in_one_undo_step_and_the_files_text_goes_out_as_kept()
    {
        var (f, d, _, _) = Project();
        var id = (string)Call(d, "ids.add", new JsonObject { ["text"] = Rules, ["name"] = "Office.ids" })["id"]!;
        var newer = Rules.Replace("<version>1</version>", "<version>2</version>");
        var other = Head + Spec("Walls are named", "N-01", Walls, "<attribute><name><simpleValue>Name</simpleValue></name></attribute>") + Tail;
        // Two files brought in (a newer copy of one by its name, ignoring case, and a new one) in one step.
        var many = Call(d, "ids.add", new JsonObject { ["files"] = new JsonArray(new JsonObject { ["name"] = "office.ids", ["text"] = newer }, new JsonObject { ["name"] = "names.ids", ["text"] = other }) });
        Assert.Equal(2, many["files"]!.AsArray().Count);
        Assert.Equal(id, (string)many["files"]![0]!["id"]!);
        Assert.Equal(2, f.Model.OfClass(IdsFiles.Class).Count());
        f.Session.Undo();
        Assert.Equal("1", Assert.Single(f.Model.OfClass(IdsFiles.Class)).Str("version"));
        // One that is not IDS: nothing kept, and the refusal names it.
        var why = Failure(d, "ids.add", new JsonObject { ["files"] = new JsonArray(new JsonObject { ["name"] = "names.ids", ["text"] = other }, new JsonObject { ["name"] = "bad.ids", ["text"] = "<x/>" }) });
        Assert.StartsWith("bad.ids:", why);
        Assert.Single(f.Model.OfClass(IdsFiles.Class));
        // The text as kept, with its hash, for a server: only when asked.
        var files = Call(d, "doc.query", new JsonObject { ["name"] = "ids.files", ["args"] = new JsonObject { ["text"] = true } })["files"]!.AsArray();
        Assert.Equal(Rules, (string?)files[0]!["xml"]);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Rules))), (string?)files[0]!["sha256"]);
        Assert.Null(Call(d, "doc.query", new JsonObject { ["name"] = "ids.files" })["files"]![0]!["xml"]);
    }

    [Fact]
    public void A_run_checks_the_model_as_its_IFC_says_it_with_each_failing_wall_and_its_element()
    {
        var (f, d, wall, other) = Project();
        Call(d, "ids.add", new JsonObject { ["text"] = Rules, ["name"] = "office.ids" });
        var run = Call(d, "ids.run");
        output.WriteLine(run.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true })[..Math.Min(4000, run.ToJsonString().Length)]);
        Assert.Equal("model", (string?)run["source"]);
        foreach (var ok in new[] { "W-01", "W-02", "W-03", "W-04", "W-06" })
            Assert.True((string?)Spec(run, ok)["status"] == "pass", $"{ok}: {Spec(run, ok).ToJsonString()}");
        var code = Spec(run, "W-05");
        Assert.Equal(("fail", 2, 0, 2), ((string?)code["status"], (int)code["applicable"]!, (int)code["passed"]!, (int)code["failed"]!));
        var failures = code["failures"]!.AsArray();
        Assert.Equal(new[] { wall, other }.Order(), failures.Select(x => (string)x!["globalId"]!).Order());
        Assert.All(failures, x => Assert.Equal((string?)x!["globalId"], (string?)x["elementId"]));
        Assert.Equal("no MyOffice_Data.Code", (string?)failures[0]!["actual"]);
        Assert.Equal(new[] { wall, other }.Order(), code["elements"]!.AsArray().Select(x => (string)x!).Order());
        Assert.Equal(1, (int)run["failing"]!);
        Assert.True((bool)run["blocking"]!);
        // Undo-free: the run changed nothing.
        Assert.Equal("Add IDS file", f.Session.StateFor(Lapin.Model.History.Me).UndoLabel);

        // The user's own parameter in the office's set: now it passes.
        foreach (var w in new[] { wall, other })
            f.Run("element.setParam", new JsonObject { ["id"] = w, ["pset"] = "MyOffice_Data", ["name"] = "Code", ["kind"] = "text", ["value"] = "PE-1" });
        Assert.True((bool)Call(d, "ids.results")["stale"]!);
        var again = Call(d, "ids.run");
        Assert.Equal("pass", (string?)Spec(again, "W-05")["status"]);
        Assert.False((bool)again["blocking"]!);
        // The project's rule: failures need not block.
        f.Run("ids.setRules", new JsonObject { ["blocks"] = false });
        f.Run("element.setParam", new JsonObject { ["id"] = wall, ["pset"] = "MyOffice_Data", ["name"] = "Code", ["kind"] = "text", ["value"] = "" });
        var third = Call(d, "ids.run");
        Assert.Equal(1, (int)third["failing"]!);
        Assert.False((bool)third["blocking"]!);
        // One specification's failures a page at a time.
        var page = Call(d, "ids.results", new JsonObject { ["file"] = third["files"]![0]!["id"]!.DeepClone(), ["spec"] = 4, ["offset"] = 0, ["limit"] = 1 });
        Assert.Single(page["failures"]!.AsArray());
        Assert.Equal(wall, (string?)page["failures"]![0]!["globalId"]);
    }

    [Fact]
    public void Remarks_are_made_per_failing_specification_in_one_step_and_brought_up_to_date_by_a_run_again()
    {
        var (f, d, wall, other) = Project();
        Call(d, "ids.add", new JsonObject { ["text"] = Rules, ["name"] = "office.ids" });
        Assert.Contains("Run the IDS check first", Failure(d, "ids.makeRemarks"));
        Call(d, "ids.run");
        var made = Call(d, "ids.makeRemarks", new JsonObject { ["author"] = "ana@example.com" });
        Assert.Equal((1, 0), ((int)made["made"]!, (int)made["updated"]!));
        Assert.Equal("IDS remarks", f.Session.StateFor(Lapin.Model.History.Me).UndoLabel);
        var topic = Remarks.All(f.Model).Single();
        Assert.Equal("IDS: Walls have a code", topic.Title);
        Assert.Contains("2 of 2 elements fail", topic.Description);
        Assert.Contains("MyOffice_Data.Code", topic.Description);
        Assert.Equal(new[] { wall, other }.Order(), topic.Viewpoints[0].Selection.Order());
        // A camera on them: a CDE server takes no viewpoint without one (or a snapshot).
        Assert.NotNull(topic.Viewpoints[0].Camera);
        Assert.True(topic.Viewpoints[0].Camera!.Direction.Z < 0, "looking down at the walls");
        Assert.Equal("ana@example.com", topic.CreationAuthor);
        // No label: a CDE server takes only the project's own labels (the title says IDS).
        Assert.Empty(topic.Labels);
        // The model changed: a run again first.
        f.Run("element.setParam", new JsonObject { ["id"] = wall, ["pset"] = "MyOffice_Data", ["name"] = "Code", ["kind"] = "text", ["value"] = "PE-1" });
        Assert.Contains("run it again", Failure(d, "ids.makeRemarks"));
        Call(d, "ids.run");
        var updated = Call(d, "ids.makeRemarks");
        Assert.Equal((0, 1), ((int)updated["made"]!, (int)updated["updated"]!));
        var same = Remarks.All(f.Model).Single();
        Assert.Equal(topic.Guid, same.Guid);
        Assert.Equal(new[] { other }, same.Viewpoints[0].Selection);
        Assert.Contains("1 of 2 elements fail", same.Description);
        // One undo step each.
        f.Session.Undo();
        Assert.Equal(new[] { wall, other }.Order(), Remarks.All(f.Model).Single().Viewpoints[0].Selection.Order());
        f.Session.Undo();
        f.Session.Undo();
        Assert.Empty(Remarks.All(f.Model));
    }

    [Fact]
    public void The_report_is_a_workbook_and_the_files_travel_with_the_project()
    {
        var (f, d, _, _) = Project();
        Call(d, "ids.add", new JsonObject { ["text"] = Rules, ["name"] = "office.ids" });
        Call(d, "ids.run");
        var xlsx = Convert.FromBase64String((string)Call(d, "ids.report", new JsonObject { ["format"] = "xlsx" })["data"]!);
        using (var zip = new ZipArchive(new MemoryStream(xlsx)))
        {
            Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
            var workbook = new StreamReader(zip.GetEntry("xl/workbook.xml")!.Open()).ReadToEnd();
            foreach (var sheet in new[] { "Report", "Specifications", "Failures" }) Assert.Contains($"name=\"{sheet}\"", workbook);
            var strings = new StreamReader(zip.GetEntry("xl/sharedStrings.xml")?.Open() ?? zip.GetEntry("xl/worksheets/sheet3.xml")!.Open()).ReadToEnd();
            Assert.Contains("Walls have a code", strings);
        }
        // Saved and opened again: the IDS is there, with its id.
        var id = IdsFiles.All(f.Model).Single().Id;
        var bytes = Convert.FromBase64String((string)Call(d, "project.saveBytes")["data"]!);
        var g = new Fixture(new ArchitectureModule(), new StructureModule());
        var d2 = new EngineDispatcher(g.Session, PackageFiles.Create());
        Call(d2, "project.openBytes", new JsonObject { ["data"] = Convert.ToBase64String(bytes), ["name"] = "p.lapin" });
        Assert.Equal(id, IdsFiles.All(g.Model).Single().Id);
        Assert.Equal("fail", (string?)Spec(Call(d2, "ids.run"), "W-05")["status"]);
    }

    [Fact]
    public void A_submission_despite_failing_IDS_says_why_after_the_issues_note()
    {
        Assert.Equal("For review", EngineDispatcher.IssueNote(new JsonObject(), "For review"));
        Assert.Equal("For review\nSubmitted despite 2 failing IDS specifications: agreed with the client",
            EngineDispatcher.IssueNote(new JsonObject { ["idsOverride"] = " agreed with the client ", ["idsFailing"] = 2 }, "For review"));
        Assert.Equal("Submitted despite 1 failing IDS specification: x", EngineDispatcher.IssueNote(new JsonObject { ["idsOverride"] = "x", ["idsFailing"] = 1 }, null));
        Assert.Equal("Mine", EngineDispatcher.IssueNote(new JsonObject { ["note"] = "Mine", ["idsOverride"] = "  " }, "For review"));
    }

    [Fact]
    public void A_run_over_an_IFC_file_and_the_Bloc_P_4_timing()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_IDS_BENCH_IFC") ?? "/Users/ionutciuntuc/Documents/GitHub/Lapin/modele/Bloc-P-4.ifc";
        if (!File.Exists(path)) { output.WriteLine($"No {path}: skipped."); return; }
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var rules = Head +
            Spec("Walls are named", "B-01", Walls, "<attribute><name><simpleValue>Name</simpleValue></name></attribute>") +
            Spec("Walls have a material", "B-02", Walls, "<material/>") +
            Spec("Walls are on a storey", "B-03", Walls, "<partOf relation=\"IFCRELCONTAINEDINSPATIALSTRUCTURE\"><entity><name><simpleValue>IFCBUILDINGSTOREY</simpleValue></name></entity></partOf>") +
            Spec("Slabs say whether they bear loads", "B-04", "<entity><name><simpleValue>IFCSLAB</simpleValue></name></entity>", "<property dataType=\"IFCBOOLEAN\"><propertySet><simpleValue>Pset_SlabCommon</simpleValue></propertySet><baseName><simpleValue>LoadBearing</simpleValue></baseName></property>") +
            Spec("Anything with a fire rating", "B-05", "<entity><name><xs:restriction base=\"xs:string\"><xs:pattern value=\"IFC.*\"/></xs:restriction></name></entity>", "<property><propertySet><xs:restriction base=\"xs:string\"><xs:pattern value=\"Pset_.*Common\"/></xs:restriction></propertySet><baseName><simpleValue>FireRating</simpleValue></baseName></property>") +
            Tail;
        Call(d, "ids.add", new JsonObject { ["text"] = rules, ["name"] = "bloc.ids" });
        // The file itself, as it came.
        var sw = Stopwatch.StartNew();
        var onFile = Call(d, "ids.run", new JsonObject { ["path"] = path });
        var fileMs = sw.ElapsedMilliseconds;
        Assert.Equal(Path.GetFileName(path), (string?)onFile["source"]);
        Assert.True((int)Spec(onFile, "B-01")["applicable"]! >= 400);
        // Imported, then the model as its export writes it.
        IfcImport.Run(f.Session, path, null, out _);
        sw.Restart();
        var onModel = Call(d, "ids.run");
        var modelMs = sw.ElapsedMilliseconds;
        foreach (var s in onModel["files"]![0]!["specifications"]!.AsArray())
            output.WriteLine($"{s!["identifier"]}: {s["status"]} {s["applicable"]} applicable, {s["failed"]} failed");
        output.WriteLine($"Bloc-P-4: the file checked in {fileMs} ms; the imported model ({f.Model.Entities.Count()} entities) written as IFC in {onModel["exportMs"]} ms and checked in {modelMs} ms all told.");
        Assert.True((int)Spec(onModel, "B-01")["applicable"]! >= 400);
        // Bounded: the file in a few seconds even in Debug under a loaded machine; the model's run is its export and a check.
        Assert.InRange(fileMs, 0, 20_000);
        Assert.InRange(modelMs, 0, 60_000);
    }
}
