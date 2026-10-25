using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Protocol;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The frames a trimmed engine (the browser's) writes, with the generated
/// contracts alone, are byte for byte the desktop's: every reply and diff of
/// a session that draws, measures, queries and exports.
/// </summary>
public class ProtocolContractTests
{
    [Fact]
    public void A_session_s_frames_come_out_the_same_by_the_generated_contracts_alone()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new Lapin.Drafting.DraftingModule());
        var g = new Fixture(new ArchitectureModule(), new StructureModule(), new Lapin.Drafting.DraftingModule());
        var desktop = new EngineDispatcher(f.Session, PackageFiles.Create());
        var trimmed = new EngineDispatcher(g.Session, PackageFiles.Create()) { ContractsOnly = true };
        string[] calls =
        [
            """{"id":1,"method":"session.info"}""",
            """{"id":2,"method":"doc.execute","params":{"command":"architecture.addWall","args":{"x1":0,"y1":0,"x2":5,"y2":0}}}""",
            """{"id":3,"method":"doc.execute","params":{"command":"architecture.addWall","args":{"x1":5,"y1":0,"x2":5,"y2":4,"arc":1}}}""",
            """{"id":4,"method":"doc.execute","params":{"command":"structure.addBeam","args":{"x1":0,"y1":2,"x2":5,"y2":2}}}""",
            """{"id":5,"method":"doc.query","params":{"name":"cutOrder.list"}}""",
            """{"id":6,"method":"doc.snapshot"}""",
            """{"id":7,"method":"doc.undo"}""",
            """{"id":8,"method":"recovery.list"}""",
            """{"id":9,"method":"doc.execute","params":{"command":"architecture.flipWall","args":{"id":"nope"}}}""",
            """{"id":10,"method":"project.exportIfc"}""",
        ];
        foreach (var call in calls)
        {
            var (a, da) = desktop.Handle(call);
            var (b, db) = trimmed.Handle(call);
            // The ids the two sessions make differ: compare with them taken out.
            Assert.Equal(Normalise(a, f, g), Normalise(b, f, g));
            Assert.Equal(da == null, db == null);
            if (da != null) Assert.Equal(Normalise(desktop.DiffFrame(da), f, g), Normalise(trimmed.DiffFrame(db!), f, g));
        }
        // The quantities of an element, by the trimmed dispatcher.
        var wall = g.Model.OfClass("IfcWall").First().Id;
        var (q, _) = trimmed.Handle(new JsonObject { ["id"] = 11, ["method"] = "quantities.of", ["params"] = new JsonObject { ["id"] = wall } }.ToJsonString());
        Assert.Contains("Qto_WallBaseQuantities", BinaryFrame.HeaderText(q));
    }

    /// <summary>The header as text, the ids and the dates a session makes for itself taken out; the buffers as they are.</summary>
    static string Normalise(byte[] frame, Fixture f, Fixture g)
    {
        var header = BinaryFrame.HeaderText(frame);
        header = System.Text.RegularExpressions.Regex.Replace(header, "\"[0-9A-Za-z_$]{22}\"", "\"<id>\"");
        header = System.Text.RegularExpressions.Regex.Replace(header, "\\d{4}-\\d{2}-\\d{2}T[0-9:.]+Z?", "<date>");
        // An exported file names the temporary file it was written to.
        header = System.Text.RegularExpressions.Regex.Replace(header, "\"data\":\"[^\"]*\"", "\"data\":\"<file>\"");
        var (_, buffers) = BinaryFrame.Decode(frame);
        return header + "|" + Convert.ToBase64String(buffers.ToArray());
    }
}

/// <summary>A host without files (the browser): projects and IFC files go in and out as bytes.</summary>
public class ProtocolBytesTests
{
    static JsonObject Result(byte[] frame) => (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(frame))!["result"]!;

    [Fact]
    public void A_project_saved_to_bytes_opens_from_them_as_it_was()
    {
        var f = new Fixture(new ArchitectureModule());
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        d.Handle("""{"id":1,"method":"doc.execute","params":{"command":"architecture.addWall","args":{"x1":0,"y1":0,"x2":5,"y2":0}}}""");
        var (saved, diff) = d.Handle("""{"id":2,"method":"project.saveBytes","params":{"name":"house"}}""");
        var r = Result(saved);
        Assert.Equal("house.lapin", (string?)r["name"]);
        Assert.False(diff!.State.Dirty);
        var g = new Fixture(new ArchitectureModule());
        var e = new EngineDispatcher(g.Session, PackageFiles.Create());
        e.Handle(new JsonObject { ["id"] = 3, ["method"] = "project.openBytes", ["params"] = new JsonObject { ["name"] = "house.lapin", ["data"] = r["data"]!.DeepClone() } }.ToJsonString());
        Assert.Equal(f.Model.Entities.Select(x => x.Id).Order(), g.Model.Entities.Select(x => x.Id).Order());
        Assert.Equal("house.lapin", g.Session.Path);
    }

    [Fact]
    public void An_ifc_file_goes_out_and_comes_back_as_bytes_without_a_temporary_file()
    {
        var f = new Fixture(new ArchitectureModule());
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        d.Handle("""{"id":1,"method":"doc.execute","params":{"command":"architecture.addWall","args":{"x1":0,"y1":0,"x2":5,"y2":0}}}""");
        var exported = Result(d.Handle("""{"id":2,"method":"project.exportIfc"}""").Reply);
        var bytes = Convert.FromBase64String((string)exported["data"]!);
        Assert.StartsWith("ISO-10303-21;", System.Text.Encoding.UTF8.GetString(bytes));
        var g = new Fixture(new ArchitectureModule());
        var e = new EngineDispatcher(g.Session, PackageFiles.Create());
        var (reply, imported) = e.Handle(new JsonObject { ["id"] = 3, ["method"] = "project.importIfc", ["params"] = new JsonObject { ["name"] = "house.ifc", ["data"] = exported["data"]!.DeepClone() } }.ToJsonString());
        Assert.NotNull(imported);
        Assert.Single(g.Model.OfClass("IfcWall"));
        Assert.Null(JsonNode.Parse(BinaryFrame.HeaderText(reply))!["error"]);
    }

    [Fact]
    public void An_old_project_opens_from_its_bytes_as_from_its_path()
    {
        var old = Path.Combine(AppContext.BaseDirectory, "Fixtures", "old-format2.lapin");
        var f = new Fixture(new ArchitectureModule(), new Lapin.Structure.StructureModule(), new Lapin.Drafting.DraftingModule());
        var byPath = new EngineDispatcher(f.Session, PackageFiles.Create());
        var pathNotes = Result(byPath.Handle(new JsonObject { ["id"] = 1, ["method"] = "project.open", ["params"] = new JsonObject { ["path"] = old } }.ToJsonString()).Reply);
        var g = new Fixture(new ArchitectureModule(), new Lapin.Structure.StructureModule(), new Lapin.Drafting.DraftingModule());
        var byBytes = new EngineDispatcher(g.Session, PackageFiles.Create());
        var (reply, diff) = byBytes.Handle(new JsonObject { ["id"] = 1, ["method"] = "project.openBytes", ["params"] = new JsonObject { ["name"] = "old.lapin", ["data"] = Convert.ToBase64String(File.ReadAllBytes(old)) } }.ToJsonString());
        Assert.NotNull(diff);
        Assert.Equal(pathNotes["notes"]!.ToJsonString(), Result(reply)["notes"]!.ToJsonString());
        Assert.Equal(f.Model.Entities.Select(e => (e.Class, e.Name)), g.Model.Entities.Select(e => (e.Class, e.Name)));
        Assert.Equal(3, g.Model.OfClass(Lapin.Model.IfcClass.Wall).Count());
        Assert.Null(g.Session.Path);
    }

    [Fact]
    public void A_report_is_the_same_body_from_every_host_and_needs_a_title_and_a_description()
    {
        var p = new JsonObject { ["kind"] = "idea", ["title"] = " Roofs ", ["description"] = "Hip roofs", ["errors"] = new JsonArray("e1"), ["website"] = "" };
        var body = SupportReport.Body(p, "1.2", "browser", "Chrome");
        Assert.Equal("Roofs", (string?)body["title"]);
        Assert.Equal("lapin-pro 1.2", (string?)body["context"]!["version"]);
        Assert.Equal("browser", (string?)body["context"]!["url"]);
        Assert.Equal("""["e1"]""", body["context"]!["errors"]!.ToJsonString());
        Assert.Throws<Lapin.Platform.CommandException>(() => SupportReport.Body(new JsonObject { ["title"] = "x" }, "1", "desktop", "mac"));
        Assert.Throws<Lapin.Platform.CommandException>(() => SupportReport.Body(new JsonObject { ["kind"] = "rant", ["title"] = "x", ["description"] = "y" }, "1", "desktop", "mac"));
    }
}
