using System.Text.Json.Nodes;
using Lapin.Platform.Cde;
using Lapin.Platform;
using Lapin.Platform.Cde.Remarks;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Remarks tied to documents and issues re-issued as revisions (decision 824): a remark made on a sheet (or on a view placed on one)
/// carries <c>lapin-document:&lt;document&gt;</c>, the very name <c>remote.submitIssue</c> sends in <c>documents</c>, so the server's
/// publish gate finds it; the same documents issued again are a new revision of their issue (its number and GlobalId kept, the revision
/// after its last), on a CDE server, a folder remote and in Lapin's own register.
/// </summary>
public sealed partial class CdeTests
{
    static string[] Strings(JsonNode? n) => [.. (n as JsonArray ?? []).Select(x => (string?)x).OfType<string>()];

    [Fact]
    public void A_remark_made_on_a_sheet_or_on_a_view_placed_on_it_is_tied_to_the_document_an_issue_submits()
    {
        var f = F();
        var s = Sheet(f);
        var other = Sheet(f, "A-102", "First floor plan");
        var document = (string)Cde.Names(f.Model, new JsonObject { ["sheets"] = new JsonArray(s) })["sheets"]![0]!["name"]!;
        Assert.Equal("PRJ-ORG-ZZ-00-DR-A-0101", document);

        // On the sheet: its document, as a reference link, besides any the caller gives.
        var g = Guid.Parse((string)f.Run("remarks.create", new JsonObject { ["title"] = "Dimension missing", ["sheetId"] = s, ["referenceLinks"] = new JsonArray("https://example.com/x") })!["guid"]!);
        var t = Remarks.Read(f.Model.Get(Remarks.IdOf(g))!);
        Assert.Equal(["https://example.com/x", "lapin-document:PRJ-ORG-ZZ-00-DR-A-0101"], t.Links);
        Assert.Equal([document], t.Documents);

        // On a view placed on the other sheet: that sheet's document.
        var view = Sheets.Viewports(f.Model.Get(other)!).Single().ViewId;
        var g2 = Guid.Parse((string)f.Run("remarks.create", new JsonObject { ["title"] = "Grid label", ["viewId"] = view })!["guid"]!);
        Assert.Equal(["PRJ-ORG-ZZ-00-DR-A-0102"], Remarks.Read(f.Model.Get(Remarks.IdOf(g2))!).Documents);
        // A view on no sheet ties to nothing.
        var loose = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Loose", ["scale"] = 100 })!["id"]!;
        var g3 = Guid.Parse((string)f.Run("remarks.create", new JsonObject { ["title"] = "Nowhere", ["viewId"] = loose })!["guid"]!);
        Assert.Empty(Remarks.Read(f.Model.Get(Remarks.IdOf(g3))!).Links);

        // The documents changed by hand: the other links stay; one undo step.
        f.Run("remarks.update", new JsonObject { ["id"] = Remarks.IdOf(g), ["documents"] = new JsonArray(document, "PRJ-ORG-ZZ-00-DR-A-0102"), ["author"] = "me" });
        Assert.Equal(["https://example.com/x", $"lapin-document:{document}", "lapin-document:PRJ-ORG-ZZ-00-DR-A-0102"], Remarks.Read(f.Model.Get(Remarks.IdOf(g))!).Links);
        f.Session.Undo();
        Assert.Equal([document], Remarks.Read(f.Model.Get(Remarks.IdOf(g))!).Documents);
        f.Run("remarks.update", new JsonObject { ["id"] = Remarks.IdOf(g), ["documents"] = new JsonArray(), ["author"] = "me" });
        Assert.Equal(["https://example.com/x"], Remarks.Read(f.Model.Get(Remarks.IdOf(g))!).Links);

        // The choices the dialog offers: the sheets, the written documents and the model, by the names an issue gives them; and those of a sheet in front.
        var q = f.Session.Query("remarks.documents", new JsonObject { ["sheetId"] = s }) as JsonObject;
        var choices = (q!["documents"] as JsonArray)!.Select(x => ((string?)x!["kind"], (string?)x["document"])).ToList();
        Assert.Contains(("sheet", document), choices);
        Assert.Contains(("model", "PRJ-ORG-ZZ-ZZ-M3-A-0001"), choices);
        Assert.Equal([document], Strings(q["at"]));
        // The list shows them.
        var listed = (f.Session.Query("remarks.list", []) as JsonObject)!["remarks"]!.AsArray().First(x => (string?)x!["guid"] == g.ToString("D"))!;
        Assert.Equal(["https://example.com/x"], Strings(listed["referenceLinks"]));
    }

    [Fact]
    public void Reference_links_go_out_and_come_back_in_BCF_2_1_and_3_0()
    {
        var topic = new BcfTopic(Guid.NewGuid(), "Linked", null, "Issue", "Open", "Normal", null, "2026-11-23T10:00:00Z", "ionut", null, null, null, null, null, ["Architecture"], [], [],
            ["https://example.com/a.pdf", "lapin-document:PRJ-ORG-ZZ-00-DR-A-0101"]);
        foreach (var v in new[] { Bcf.Version21, Bcf.Version30 })
        {
            var bytes = BcfWriter.Write(new BcfFile(v, [topic]));
            var back = BcfReader.Read(bytes).Topics.Single();
            Assert.Equal(topic.Links, back.Links);
            Assert.Equal(["PRJ-ORG-ZZ-00-DR-A-0101"], back.Documents);
            using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
            using var markup = new StreamReader(zip.GetEntry($"{topic.Guid:D}/markup.bcf")!.Open());
            var xml = System.Xml.Linq.XDocument.Parse(markup.ReadToEnd());
            var t = xml.Root!.Element("Topic")!;
            // Before the title, as both schemas order them; wrapped in <ReferenceLinks> in 3.0 only.
            Assert.Equal(v == Bcf.Version30 ? "ReferenceLinks" : "ReferenceLink", t.Elements().First().Name.LocalName);
        }
        // A remark without links keeps the shape it had: no referenceLinks member at all.
        Assert.False((topic with { ReferenceLinks = null }).ToJson().ContainsKey("referenceLinks"));
    }

    [Fact]
    public void The_same_documents_issued_again_are_a_new_revision_of_their_issue_in_Lapins_own_register()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var s = Sheet(f);
        var s2 = Sheet(f, "A-102", "First floor plan");
        var store = Lapin.Package.PackageIssues.Empty();
        CdeIssue.Run(f.Session, store, new JsonObject { ["sheets"] = new JsonArray(s), ["revision"] = "P01", ["status"] = "S3", ["toFolder"] = false });
        var first = CdeIssues.All(f.Model).Single();

        // The same sheet again: the names propose issue 1's next revision; another set of sheets matches nothing.
        var names = Cde.Names(f.Model, new JsonObject { ["sheets"] = new JsonArray(s) });
        Assert.Equal((1, first.Id, "P01"), ((int)names["match"]!["number"]!, (string?)names["match"]!["issueId"], (string?)names["match"]!["lastRevision"]));
        Assert.Null(Cde.Names(f.Model, new JsonObject { ["sheets"] = new JsonArray(s, s2) })["match"]);
        Assert.Null(Cde.Names(f.Model, new JsonObject { ["sheets"] = new JsonArray(s), ["model"] = true })["match"]);
        var of = Cde.Names(f.Model, new JsonObject { ["sheets"] = new JsonArray(s), ["of"] = first.Id });
        Assert.Equal((1, "P02", first.Id), ((int)of["number"]!, (string?)of["revision"], (string?)of["of"]));
        Assert.Contains("IE-A-0001_S2_P02", (string)of["transmittal"]!);

        // The same revision again, or an earlier one, is refused before any file is made.
        Assert.Contains("at P01 already", Assert.Throws<CommandException>(() => CdeIssue.Run(f.Session, store, new JsonObject { ["sheets"] = new JsonArray(s), ["revision"] = "P01", ["status"] = "S3", ["toFolder"] = false, ["of"] = first.Id })).Message);
        Assert.Single(CdeIssues.All(f.Model));

        // Issue 2 in between, then P02 of issue 1: number 1 kept, its GlobalId named, the name kept; the register in the order of making.
        CdeIssue.Run(f.Session, store, new JsonObject { ["sheets"] = new JsonArray(s2), ["revision"] = "P01", ["status"] = "S3", ["toFolder"] = false });
        var (r, _) = CdeIssue.Run(f.Session, store, new JsonObject { ["sheets"] = new JsonArray(s), ["revision"] = "P02", ["status"] = "S3", ["toFolder"] = false, ["of"] = first.Id });
        Assert.Equal((1, "P02", first.Id, first.Name), ((int)r["number"]!, (string?)r["revision"], (string?)r["issueId"], (string?)r["name"]));
        var all = CdeIssues.All(f.Model);
        Assert.Equal([(1, "P01"), (2, "P01"), (1, "P02")], all.Select(i => (i.Number, i.Revision)));
        Assert.Equal(first.Id, all[^1].IssueId);
        Assert.Equal(3, CdeIssues.NextNumber(f.Model));
        // The sheet's last issue is the new revision, and its title block says so.
        Assert.Equal("P02", CdeIssues.LastOf(f.Model, s)!.Value.Issue.Revision);
        Assert.Equal("P02", (string?)f.Model.Get(s)!.Attrs["titleBlock"]!["revision"]);
        var series = CdeIssues.AllSeries(f.Model);
        Assert.Equal([(1, "P02"), (2, "P01")], series.Select(x => (x.Number, x.LastRevision)));
        // P → C: the first contractual revision is C01, and comes after P02.
        Assert.Equal("C01", CdeRevisions.Next(series[0].LastRevision, contractual: true));
        CdeIssue.Run(f.Session, store, new JsonObject { ["sheets"] = new JsonArray(s), ["revision"] = "C01", ["status"] = "A1", ["toFolder"] = false, ["of"] = all[^1].Id });
        Assert.Equal(["P01", "P02", "C01"], CdeIssues.AllSeries(f.Model)[0].Revisions.Select(x => x.Revision));
        // A project saved before this kept no issueId: each record is its own issue, as it was.
        Assert.Null(CdeIssues.All(f.Model)[0].Of);
    }

    [Fact]
    public void A_new_revision_is_submitted_and_tagged_with_its_issues_number_and_GlobalId()
    {
        var server = new PublishServer(new IssueSyncTests.FakeCde());
        var c = Issued(new HttpClient(server), _ => Server(), container: "Published", status: "A1");
        SaveToRemote(c);
        var first = CdeIssues.All(c.F.Model).Single();
        Assert.True((bool)c.Call("remote.issueTag")["ok"]!);

        var sheet = first.Sheets.Single();
        c.Call("cde.issue", new JsonObject { ["sheets"] = new JsonArray(sheet), ["revision"] = "P02", ["status"] = "A1", ["container"] = "Published", ["toFolder"] = false, ["of"] = first.Id });
        c.Call("history.commit", new JsonObject { ["message"] = "Issue 1 P02" });
        Assert.True((bool)c.Call("remote.push", new JsonObject { ["noViewer"] = true })["pushed"]!);
        // Even asked to let the server number it, a new revision keeps its issue's number.
        var r = c.Call("remote.submitIssue", new JsonObject { ["next"] = true });
        Assert.True((bool)r["ok"]!, r.ToJsonString());
        Assert.Equal("refs/tags/issue/1-P02", (string?)r["tag"]);
        var sub = server.Submissions.Single();
        Assert.Equal((1, "P02", first.Id), ((int)sub["issue"]!, (string?)sub["revision"], (string?)sub["issueId"]));

        // Made directly instead: the tag names the issue's GlobalId, the first revision's.
        var t = c.Call("remote.issueTag");
        Assert.True((bool)t["ok"]!, t.ToJsonString());
        var json = JsonNode.Parse(server.Objects[server.Refs["refs/tags/issue/1-P02"]])!;
        Assert.Equal((1, "P02", first.Id), ((int)json["number"]!, (string?)json["revision"], (string?)json["issue"]));
        Assert.Equal(first.Id, (string?)JsonNode.Parse(server.Objects[server.Refs["refs/tags/issue/1-P01"]])!["issue"]);
    }

    [Fact]
    public void A_remark_on_a_sheet_synced_stops_the_submission_of_that_sheet_at_the_servers_gate()
    {
        var server = new PublishServer(new IssueSyncTests.FakeCde());
        var c = Issued(new HttpClient(server), _ => Server());
        SaveToRemote(c);
        var sheet = CdeIssues.All(c.F.Model).Single().Sheets.Single();
        var g = (string)c.F.Run("remarks.create", new JsonObject { ["title"] = "Door tag missing", ["sheetId"] = sheet, ["author"] = "Mihai" })!["guid"]!;
        c.Call("remote.syncIssues");
        Assert.Equal(["lapin-document:PRJ-ORG-ZZ-00-DR-A-0101"], Strings(server.InnerTopics[g]["reference_links"]));

        var r = c.Call("remote.submitIssue");
        Assert.True((bool)r["ok"]!, r.ToJsonString());
        var gate = r["submission"]!["gate"]!;
        Assert.False((bool)gate["passed"]!);
        Assert.Equal((g, "PRJ-ORG-ZZ-00-DR-A-0101"), ((string?)gate["topics"]![0]!["guid"], (string?)gate["topics"]![0]!["document"]));
    }
}
