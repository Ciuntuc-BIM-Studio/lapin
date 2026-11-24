using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Lapin.Host;
using Lapin.Package;
using Lapin.Platform.Cde.Remarks;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The handover report (<c>remote.handoverReport</c>; Atrium docs/plan-cde-web.md W4): an XLSX written by Lapin's own OOXML writer, opened
/// here as a zip and read part by part: the package's parts and content types, the sheets in order, the issues with status, priority,
/// assignee (by name) and dates, every comment, the server's issue journal, and the handover diff between two issue tags (read from the
/// server, or given by the caller). Without a server the report has the project's remarks and says what it lacks.
/// </summary>
public sealed class HandoverReportTests
{
    const string Root = "http://127.0.0.1:5998";
    const string Project = "0b7c6f8e-1111-4222-8333-944455556666";
    static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>The fake Atrium Server of the issue sync, plus the handover diff of W4 (<c>…/handover-diff?from=&amp;to=</c>).</summary>
    sealed class WithDiff(IssueSyncTests.FakeCde inner) : DelegatingHandler(inner)
    {
        public string? AskedFrom, AskedTo;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == $"/atrium/1.0/projects/{Project}/handover-diff")
            {
                var q = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
                (AskedFrom, AskedTo) = (q["from"], q["to"]);
                // The shape of Atrium docs/atrium-server.md § The handover diff.
                static JsonObject File(string document, string name, char sha) => new() { ["document"] = document, ["name"] = name, ["sha256"] = new string(sha, 64), ["size"] = 1 };
                var body = new JsonObject
                {
                    ["from"] = new JsonObject { ["name"] = "refs/tags/issue/1-P01", ["commit"] = new string('1', 64), ["revision"] = "P01", ["at"] = "2026-11-20T10:00:00Z" },
                    ["to"] = new JsonObject { ["name"] = "refs/tags/issue/2-P02", ["commit"] = new string('2', 64), ["revision"] = "P02", ["at"] = "2026-11-23T10:00:00Z" },
                    ["documents"] = new JsonObject
                    {
                        ["added"] = new JsonArray(File("PRJ-ORG-ZZ-01-DR-A-0102.pdf", "PRJ-ORG-ZZ-01-DR-A-0102_S2_P02.pdf", 'b')),
                        ["removed"] = new JsonArray(File("PRJ-ORG-ZZ-00-DR-A-0100.pdf", "PRJ-ORG-ZZ-00-DR-A-0100_S2_P01.pdf", 'c')),
                        ["changed"] = new JsonArray(new JsonObject
                        {
                            ["document"] = "PRJ-ORG-ZZ-00-DR-A-0101.pdf",
                            ["from"] = new JsonObject { ["name"] = "PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf", ["sha256"] = new string('d', 64), ["size"] = 1 },
                            ["to"] = new JsonObject { ["name"] = "PRJ-ORG-ZZ-00-DR-A-0101_S3_P02.pdf", ["sha256"] = new string('e', 64), ["size"] = 1 },
                        }),
                        ["unchanged"] = new JsonArray("PRJ-ORG-ZZ-00-M3-A-0001.ifc"),
                    },
                    ["parts"] = new JsonObject { ["added"] = new JsonArray(), ["removed"] = new JsonArray(), ["changed"] = new JsonArray("model/drafting/LapinSheet/_.arrow") },
                };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") });
            }
            return base.SendAsync(request, cancellationToken);
        }
    }

    static JsonObject Call(EngineDispatcher d, string method, JsonObject? p = null, string? token = null)
    {
        p ??= [];
        if (token != null) p["token"] = token;
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
        var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
        if (o["error"] is { } err) throw new InvalidOperationException((string)err!);
        return o["result"] as JsonObject ?? [];
    }

    static Guid Remark(Fixture f, string title, string? priority = null, string? assignedTo = null, string? status = null)
    {
        var args = new JsonObject { ["title"] = title, ["author"] = "mihai@example.com" };
        if (priority != null) args["priority"] = priority;
        if (assignedTo != null) args["assignedTo"] = assignedTo;
        if (status != null) args["status"] = status;
        return Guid.Parse((string)f.Run("remarks.create", args)!["guid"]!);
    }

    /// <summary>The workbook as its parts, and each sheet (by name, in order) as rows of cell texts.</summary>
    sealed record Book(Dictionary<string, string> Parts, List<(string Name, List<List<string>> Rows)> Sheets)
    {
        public List<List<string>> Sheet(string name) => Sheets.First(s => s.Name == name).Rows;

        public static Book Open(byte[] bytes)
        {
            var parts = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
                foreach (var e in zip.Entries)
                {
                    using var r = new StreamReader(e.Open(), Encoding.UTF8);
                    parts[e.FullName] = r.ReadToEnd();
                }
            var workbook = XDocument.Parse(parts["xl/workbook.xml"]);
            var rels = XDocument.Parse(parts["xl/_rels/workbook.xml.rels"]).Root!.Elements().ToDictionary(x => (string)x.Attribute("Id")!, x => (string)x.Attribute("Target")!);
            var sheets = new List<(string, List<List<string>>)>();
            foreach (var s in workbook.Descendants(S + "sheet"))
            {
                var target = "xl/" + rels[(string)s.Attribute(R + "id")!];
                var rows = new List<List<string>>();
                foreach (var row in XDocument.Parse(parts[target]).Descendants(S + "row"))
                {
                    var cells = new List<string>();
                    foreach (var c in row.Elements(S + "c"))
                    {
                        var col = Column((string)c.Attribute("r")!);
                        while (cells.Count < col) cells.Add("");
                        cells.Add((string?)c.Attribute("t") == "inlineStr" ? string.Concat(c.Descendants(S + "t").Select(t => t.Value)) : c.Element(S + "v")?.Value ?? "");
                    }
                    rows.Add(cells);
                }
                sheets.Add(((string)s.Attribute("name")!, rows));
            }
            return new(parts, sheets);
        }

        static int Column(string reference)
        {
            var n = 0;
            foreach (var ch in reference.TakeWhile(char.IsLetter)) n = n * 26 + (ch - 'A' + 1);
            return n - 1;
        }
    }

    static string Cell(List<List<string>> rows, int row, string column) => rows[row].ElementAtOrDefault(rows[0].IndexOf(column)) ?? "";

    [Fact]
    public void Without_a_server_the_report_holds_the_remarks_and_their_comments_and_says_the_journal_is_the_servers()
    {
        var f = new Fixture();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var a = Remark(f, "Check the lintel", priority: "High", assignedTo: "ana@example.com");
        Remark(f, "Door swing", status: "Closed");
        f.Run("remarks.comment", new JsonObject { ["id"] = Remarks.IdOf(a), ["text"] = "Too short over the door.", ["author"] = "ana@example.com" });

        var r = Call(d, "remote.handoverReport");
        Assert.EndsWith(".xlsx", (string)r["name"]!);
        Assert.Equal((2, 1), ((int)r["issues"]!, (int)r["comments"]!));
        Assert.Null(r["journal"]);
        var book = Book.Open(Convert.FromBase64String((string)r["data"]!));

        // The package: its parts and content types, as ECMA-376 asks.
        foreach (var part in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml", "xl/worksheets/sheet4.xml" })
            Assert.True(book.Parts.ContainsKey(part), part);
        Assert.Contains("spreadsheetml.sheet.main+xml", book.Parts["[Content_Types].xml"]);
        Assert.Equal(["Report", "Issues", "Comments", "Journal"], book.Sheets.Select(s => s.Name));

        var issues = book.Sheet("Issues");
        Assert.Equal(Lapin.Platform.Cde.CdeHandoverReport.IssueColumns, issues[0]);
        Assert.Equal(3, issues.Count);
        var lintel = issues.FindIndex(x => x.Contains("Check the lintel"));
        Assert.Equal("High", Cell(issues, lintel, "Priority"));
        Assert.Equal("Open", Cell(issues, lintel, "Status"));
        Assert.Equal("ana@example.com", Cell(issues, lintel, "Assigned to"));
        Assert.Equal("1", Cell(issues, lintel, "Comments"));
        Assert.Equal(a.ToString("D"), Cell(issues, lintel, "GUID"));
        Assert.Matches(@"^\d{4}-\d\d-\d\dT", Cell(issues, lintel, "Created"));
        Assert.Equal("Closed", Cell(issues, issues.FindIndex(x => x.Contains("Door swing")), "Status"));
        // Numbers are numbers (no inline string), so a sheet sorts them as numbers.
        Assert.Contains("<c r=\"N2\"><v>", book.Parts["xl/worksheets/sheet2.xml"]);

        var comments = book.Sheet("Comments");
        Assert.Equal(2, comments.Count);
        Assert.Equal("Too short over the door.", Cell(comments, 1, "Comment"));
        Assert.Equal("Check the lintel", Cell(comments, 1, "Issue"));

        var report = book.Sheet("Report");
        Assert.Equal("2", report.First(x => x[0] == "Issues")[1]);
        Assert.Equal("1", report.First(x => x[0] == "Open issues")[1]);
        Assert.Contains(report, x => x[0] == "Note" && x[1].Contains("no CDE server", StringComparison.Ordinal));
        Assert.Single(book.Sheet("Journal"));
    }

    [Fact]
    public void With_a_server_the_report_reads_the_journal_and_the_handover_diff_between_two_issue_tags()
    {
        var server = new IssueSyncTests.FakeCde();
        var handler = new WithDiff(server);
        var http = new HttpClient(handler);
        var f = new Fixture();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create()) { RemoteHttp = http };
        Call(d, "remote.add", new JsonObject { ["kind"] = "server", ["url"] = Root, ["project"] = Project }, "tok-mihai");
        var g = Remark(f, "Check the lintel", assignedTo: "ana@example.com");
        f.Run("remarks.comment", new JsonObject { ["id"] = Remarks.IdOf(g), ["text"] = "Too short.", ["author"] = "Mihai" });
        Call(d, "remote.syncIssues", null, "tok-mihai");
        server.ServerEdit(g.ToString("D"), "topic_status", "In progress", "ana@example.com");
        Call(d, "remote.syncIssues", null, "tok-mihai");

        var r = Call(d, "remote.handoverReport", new JsonObject { ["from"] = "1-P01", ["to"] = "2-P02", ["project"] = "Bloc P+4" }, "tok-mihai");
        Assert.Equal(("1-P01", "2-P02"), (handler.AskedFrom, handler.AskedTo));
        Assert.Equal(server.Journal.Count, (int)r["journal"]!);
        Assert.Equal(4, (int)r["diff"]!);
        Assert.StartsWith("Bloc P+4 handover ", (string)r["name"]!);
        var book = Book.Open(Convert.FromBase64String((string)r["data"]!));
        Assert.Equal(["Report", "Issues", "Comments", "Journal", "Handover diff"], book.Sheets.Select(s => s.Name));

        // People by name (the members the sync read), the status the server's.
        var issues = book.Sheet("Issues");
        Assert.Equal("Ana Popescu", Cell(issues, 1, "Assigned to"));
        Assert.Equal("In progress", Cell(issues, 1, "Status"));
        Assert.Equal("Mihai Ionescu", Cell(book.Sheet("Comments"), 1, "Author"));

        var journal = book.Sheet("Journal");
        Assert.Equal(server.Journal.Count + 1, journal.Count);
        var status = journal.FindIndex(x => x.Contains("status_updated"));
        Assert.Equal(("Ana Popescu", "Open", "In progress", "Check the lintel"), (Cell(journal, status, "Author"), Cell(journal, status, "Old value"), Cell(journal, status, "New value"), Cell(journal, status, "Issue")));
        Assert.Equal(g.ToString("D"), Cell(journal, status, "Issue GUID"));

        var diff = book.Sheet("Handover diff");
        Assert.Equal(["added", "removed", "changed", "unchanged"], diff.Skip(1).Select(x => x[0]));
        Assert.Equal(("PRJ-ORG-ZZ-00-DR-A-0101.pdf", "P01", "P02", "1-P01", "2-P02"), (Cell(diff, 3, "Document"), Cell(diff, 3, "From revision"), Cell(diff, 3, "To revision"), Cell(diff, 3, "From issue"), Cell(diff, 3, "To issue")));
        Assert.Equal(("S2", "S3"), (Cell(diff, 3, "From status"), Cell(diff, 3, "To status")));
        Assert.Equal(("P02", "S2", "2-P02"), (Cell(diff, 1, "To revision"), Cell(diff, 1, "To status"), Cell(diff, 1, "To issue")));
        Assert.Equal(new string('e', 64), Cell(diff, 3, "To SHA-256"));
        Assert.Equal("1", book.Sheet("Report").First(x => x[0] == "Documents removed")[1]);
        Assert.Equal("Mihai Ionescu", book.Sheet("Report").First(x => x[0] == "By")[1]);
    }

    [Fact]
    public void A_diff_the_caller_gives_is_used_without_asking_the_server_and_a_path_gets_the_file()
    {
        var server = new IssueSyncTests.FakeCde();
        var handler = new WithDiff(server);
        var f = new Fixture();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create()) { RemoteHttp = new HttpClient(handler) };
        Call(d, "remote.add", new JsonObject { ["kind"] = "server", ["url"] = Root, ["project"] = Project }, "tok-ana");
        var diff = new JsonObject { ["to"] = new JsonObject { ["name"] = "refs/tags/issue/2-C01", ["revision"] = "C01" }, ["documents"] = new JsonObject { ["added"] = new JsonArray(new JsonObject { ["document"] = "A.pdf", ["name"] = "A_A1_C01.pdf" }) } };
        var path = Path.Combine(Path.GetTempPath(), $"lapin-handover-{Guid.NewGuid():N}.xlsx");
        try
        {
            var r = Call(d, "remote.handoverReport", new JsonObject { ["from"] = "1-P01", ["to"] = "2-C01", ["diff"] = diff, ["journal"] = false, ["path"] = path }, "tok-ana");
            Assert.Null(handler.AskedFrom);
            Assert.Equal(path, (string)r["path"]!);
            var book = Book.Open(File.ReadAllBytes(path));
            Assert.Equal(["Report", "Issues", "Comments", "Journal", "Handover diff"], book.Sheets.Select(s => s.Name));
            Assert.Equal(("added", "A.pdf", "C01"), (book.Sheet("Handover diff")[1][0], book.Sheet("Handover diff")[1][1], Cell(book.Sheet("Handover diff"), 1, "To revision")));
            Assert.Single(book.Sheet("Journal"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_server_that_does_not_compare_issues_leaves_the_diff_out_and_says_so()
    {
        var server = new IssueSyncTests.FakeCde();
        var f = new Fixture();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create()) { RemoteHttp = new HttpClient(server) };
        Call(d, "remote.add", new JsonObject { ["kind"] = "server", ["url"] = Root, ["project"] = Project }, "tok-ana");
        var r = Call(d, "remote.handoverReport", new JsonObject { ["from"] = "1-P01", ["to"] = "2-P02" }, "tok-ana");
        Assert.Null(r["diff"]);
        Assert.Contains((r["notes"] as JsonArray)!, n => ((string?)n)!.Contains("does not compare", StringComparison.Ordinal));
        Assert.DoesNotContain(Book.Open(Convert.FromBase64String((string)r["data"]!)).Sheets, s => s.Name == "Handover diff");
    }
}
