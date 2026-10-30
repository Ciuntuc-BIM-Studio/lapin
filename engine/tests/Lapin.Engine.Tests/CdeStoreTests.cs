using System.IO.Compression;
using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Platform.Cde;
using Lapin.Platform.Cde.PdfRemarks;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The CDE in the project's file (decision 185): an issue's deliverables kept
/// in the .lapin under issues/blobs/ by their SHA-256 (once however many
/// issues share them, the IFC only on request), read back byte for byte after
/// the file is opened again, as one zip, published to a folder later; the
/// project folder optional; the project clean after an issue (its version
/// saved in the same call); and the fixes found while filming: the title
/// block's project name, the issued sheet printing its own revision.
/// </summary>
public sealed partial class CdeTests
{
    sealed class Protocol(Fixture f, ProjectFiles files)
    {
        readonly EngineDispatcher _d = new(f.Session, files);
        public ProjectFiles Files => files;

        public JsonNode? Call(string method, JsonObject p)
        {
            var (reply, _) = _d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
            var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
            if (o["error"] is { } err) throw new InvalidOperationException((string)err!);
            return o["result"];
        }

        public JsonObject Issue(JsonObject p) => (JsonObject)Call("cde.issue", p)!;
    }

    static List<string> Blobs(string package)
    {
        using var zip = ZipFile.OpenRead(package);
        return zip.Entries.Where(e => e.FullName.StartsWith(PackageIssues.Prefix, StringComparison.Ordinal)).Select(e => e.FullName[PackageIssues.Prefix.Length..]).Order().ToList();
    }

    [Fact]
    public void An_unsaved_project_without_a_folder_issues_into_memory_then_its_file_keeps_the_deliverables_byte_for_byte()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var s = Sheet(f);
        var proto = new Protocol(f, PackageFiles.Create());

        // No folder, no file: the issue goes; no version (there is no file to keep one), the files held in memory.
        var r = proto.Issue(new JsonObject { ["sheets"] = new JsonArray(s), ["model"] = true });
        Assert.Null(r["path"]);
        Assert.Null(r["version"]);
        var issue = CdeIssues.All(f.Model).Single();
        Assert.Null(issue.Folder);
        Assert.Equal(4, issue.Files.Count(x => x.Stored));
        var pdf = issue.Files.First(x => x.SheetId == s);
        var issued = Convert.FromBase64String((string)proto.Call("cde.issueFile", new JsonObject { ["id"] = issue.Id, ["name"] = pdf.Name })!["data"]!);
        Assert.Equal(pdf.Sha256, CdeTransmittal.Sha256(issued));
        Assert.True(f.Session.StateFor(History.Me).Dirty);

        // The first save writes them into the package, under issues/blobs/<sha256>, with the readable index.
        var path = Path.Combine(_dir, "house.lapin");
        proto.Files.Save(f.Session, path);
        Assert.Equal(issue.Files.Where(x => x.Stored).Select(x => x.Sha256).Order().ToList(), Blobs(path));
        using (var zip = ZipFile.OpenRead(path))
        {
            var index = JsonNode.Parse(zip.GetEntry(PackageIssues.IndexPath)!.Open())!;
            Assert.Equal("lapin-issues/1", (string)index["format"]!);
            Assert.Equal(5, index["issues"]![0]!["files"]!.AsArray().Count);
        }

        // Opened again elsewhere: every kept file comes back as issued, byte for byte; the IFC, not kept, is refused with a reason.
        var g = F();
        var again = new Protocol(g, PackageFiles.Create());
        again.Call("project.open", new JsonObject { ["path"] = path });
        var reopened = CdeIssues.All(g.Model).Single();
        foreach (var file in reopened.Files.Where(x => x.Stored))
        {
            var bytes = Convert.FromBase64String((string)again.Call("cde.issueFile", new JsonObject { ["id"] = reopened.Id, ["name"] = file.Name })!["data"]!);
            Assert.Equal(file.Sha256, CdeTransmittal.Sha256(bytes));
            Assert.Equal(file.Size, bytes.LongLength);
        }
        Assert.Equal(issued, Convert.FromBase64String((string)again.Call("cde.issueFile", new JsonObject { ["id"] = reopened.Id, ["name"] = pdf.Name })!["data"]!));
        var ifc = reopened.Files.Single(x => x.Kind == "ifc");
        Assert.Contains("kept only when asked", Assert.Throws<InvalidOperationException>(() => again.Call("cde.issueFile", new JsonObject { ["id"] = reopened.Id, ["name"] = ifc.Name })).Message);
        // Saved again: the blobs carried over from the file read.
        again.Files.Save(g.Session, Path.Combine(_dir, "copy.lapin"));
        Assert.Equal(Blobs(path), Blobs(Path.Combine(_dir, "copy.lapin")));
    }

    [Fact]
    public void Identical_bytes_are_kept_once_and_the_IFC_only_when_asked()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var s = Sheet(f);
        var proto = new Protocol(f, PackageFiles.Create());
        var path = Path.Combine(_dir, "house.lapin");
        proto.Files.Save(f.Session, path);

        // Made, taken back, made again: the same sheet printed the same, the same bytes once.
        proto.Issue(new JsonObject { ["sheets"] = new JsonArray(s), ["saveVersion"] = false });
        var first = CdeIssues.All(f.Model).Single().Files.First(x => x.SheetId == s).Sha256;
        f.Session.Undo();
        proto.Issue(new JsonObject { ["sheets"] = new JsonArray(s), ["model"] = true, ["keepModel"] = true });
        var one = CdeIssues.All(f.Model).Single();
        Assert.Equal(first, one.Files.First(x => x.SheetId == s).Sha256);
        Assert.True(one.Files.Single(x => x.Kind == "ifc").Stored);
        Assert.Equal(one.Files.Select(x => x.Sha256).Distinct().Order().ToList(), Blobs(path));

        // A second issue that names the same files (a record carrying them forward): no second copy, and the storage counts them once.
        f.Run("cde.recordIssue", new JsonObject
        {
            ["name"] = "Same files", ["revision"] = "P01", ["status"] = "S3", ["container"] = "Shared",
            ["files"] = new JsonArray(one.Files.Select(x => (JsonNode)x.ToJson()).ToArray()),
        });
        proto.Files.Save(f.Session, path);
        Assert.Equal(one.Files.Select(x => x.Sha256).Distinct().Order().ToList(), Blobs(path));
        var storage = (JsonObject)f.Session.Query("cde.storage", [])!;
        Assert.Equal(one.Files.Sum(x => x.Size), (long)storage["size"]!);
        Assert.Equal(0, (long)storage["issues"]![1]!["size"]!);
        Assert.Equal(one.Files.Sum(x => x.Size), (long)storage["issues"]![1]!["shared"]!);

        // An issue's stored files let go: the record and its hashes stay; the bytes another issue keeps stay too.
        f.Run("cde.dropIssueFiles", new JsonObject { ["id"] = one.Id });
        var dropped = CdeIssues.All(f.Model).First();
        Assert.Equal(one.Files.Select(x => (x.Name, x.Sha256)), dropped.Files.Select(x => (x.Name, x.Sha256)));
        Assert.All(dropped.Files, x => Assert.False(x.Stored));
        proto.Files.Save(f.Session, path);
        Assert.Equal(one.Files.Select(x => x.Sha256).Distinct().Order().ToList(), Blobs(path));
        // Once the other issue lets go of the IFC alone, its bytes leave the file; the rest stays.
        f.Run("cde.dropIssueFiles", new JsonObject { ["id"] = CdeIssues.All(f.Model)[1].Id, ["kinds"] = new JsonArray("ifc") });
        proto.Files.Save(f.Session, path);
        var ifcSha = one.Files.Single(x => x.Kind == "ifc").Sha256;
        Assert.DoesNotContain(ifcSha, Blobs(path));
        Assert.Equal(one.Files.Count(x => x.Kind != "ifc"), Blobs(path).Count);
        Assert.Equal(2, CdeIssues.All(f.Model).Count);
    }

    [Fact]
    public void An_issue_is_published_to_a_folder_later_and_handed_over_as_one_zip()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var s = Sheet(f);
        var proto = new Protocol(f, PackageFiles.Create());
        proto.Files.Save(f.Session, Path.Combine(_dir, "house.lapin"));
        var r = proto.Issue(new JsonObject { ["sheets"] = new JsonArray(s), ["model"] = true, ["dxf"] = true });
        var issue = CdeIssues.All(f.Model).Single();
        var kept = issue.Files.Where(x => x.Stored).Select(x => x.Name).Order().ToList();

        // The zip: the kept files, named after the transmittal; the IFC (not kept) listed as missing.
        var zipped = (JsonObject)proto.Call("cde.issueZip", new JsonObject { ["id"] = issue.Id })!;
        Assert.Equal("PRJ-ORG-ZZ-ZZ-IE-A-0001_S2_P01.zip", (string)zipped["name"]!);
        Assert.Equal(["PRJ-ORG-ZZ-ZZ-M3-A-0001_S2_P01.ifc"], zipped["missing"]!.AsArray().Select(x => (string)x!));
        using (var zip = new ZipArchive(new MemoryStream(Convert.FromBase64String((string)zipped["data"]!))))
        {
            Assert.Equal(kept, zip.Entries.Select(e => e.FullName).Order().ToList());
            foreach (var e in zip.Entries)
            {
                using var ms = new MemoryStream();
                e.Open().CopyTo(ms);
                Assert.Equal(issue.Files.Single(x => x.Name == e.FullName).Sha256, CdeTransmittal.Sha256(ms.ToArray()));
            }
        }

        // Published later: into the project folder's container (made when needed), dated as issued; a second time, nothing to write.
        var folder = Path.Combine(_dir, "cde");
        Directory.CreateDirectory(folder);
        f.Run("cde.setFolder", new JsonObject { ["path"] = folder });
        var published = (JsonObject)proto.Call("cde.publishIssue", new JsonObject { ["id"] = issue.Id })!;
        Assert.Equal(Path.Combine(folder, "Shared"), (string)published["path"]!);
        Assert.Equal(kept, Directory.GetFiles(Path.Combine(folder, "Shared")).Select(x => Path.GetFileName(x)!).Order().ToList());
        Assert.Single(published["missing"]!.AsArray());
        var pdfPath = Path.Combine(folder, "Shared", issue.Files.First(x => x.SheetId == s).Name);
        Assert.True(Math.Abs((File.GetLastWriteTimeUtc(pdfPath) - DateTime.Parse(issue.At, null, System.Globalization.DateTimeStyles.AdjustToUniversal)).TotalSeconds) < 2);
        Assert.Equal(kept.Count, (int)proto.Call("cde.publishIssue", new JsonObject { ["id"] = issue.Id })!["same"]!);
        // Or into any folder picked, flat.
        var picked = Path.Combine(_dir, "for the client");
        proto.Call("cde.publishIssue", new JsonObject { ["id"] = issue.Id, ["folder"] = picked, ["flat"] = true });
        Assert.Equal(kept, Directory.GetFiles(picked).Select(x => Path.GetFileName(x)!).Order().ToList());
        Assert.Null(r["file"]);   // the desktop: the version written in place, no bytes back
    }

    [Fact]
    public void After_an_issue_the_project_is_saved_with_the_version_it_names_on_the_desktop_and_in_the_browser()
    {
        // The desktop: the version saved in the same call, recorded on the issue, the project clean, the record in the file.
        var f = F();
        var s = Sheet(f);
        var proto = new Protocol(f, PackageFiles.Create());
        var path = Path.Combine(_dir, "house.lapin");
        proto.Files.Save(f.Session, path);
        var r = proto.Issue(new JsonObject { ["sheets"] = new JsonArray(s), ["name"] = "For the client" });
        Assert.True((bool)r["saved"]!);
        Assert.False(f.Session.StateFor(History.Me).Dirty);
        Assert.Equal(1, CdeIssues.All(f.Model).Single().Version);
        var read = F();
        PackageFiles.Open(read.Session, path);
        Assert.Equal(1, CdeIssues.All(read.Model).Single().Version);

        // The browser: no file of its own; the package comes back with the reply, and opens with the issue's files in it.
        var b = F();
        var bs = Sheet(b);
        var browser = new Protocol(b, new ProjectFiles((_, _) => throw new InvalidOperationException("no files"), (_, _, _, _) => throw new InvalidOperationException("no files")));
        var br = browser.Issue(new JsonObject { ["sheets"] = new JsonArray(bs), ["bytes"] = true, ["toFolder"] = false });
        Assert.False(b.Session.StateFor(History.Me).Dirty);
        var bytes = Convert.FromBase64String((string)br["file"]!["data"]!);
        var c = F();
        var other = new Protocol(c, new ProjectFiles((_, _) => throw new InvalidOperationException("no files"), (_, _, _, _) => throw new InvalidOperationException("no files")));
        other.Call("project.openBytes", new JsonObject { ["name"] = "house.lapin", ["data"] = Convert.ToBase64String(bytes) });
        var issue = CdeIssues.All(c.Model).Single();
        Assert.Equal(1, issue.Version);
        var sheetPdf = issue.Files.First(x => x.SheetId == bs);
        Assert.Equal(sheetPdf.Sha256, CdeTransmittal.Sha256(Convert.FromBase64String((string)other.Call("cde.issueFile", new JsonObject { ["id"] = issue.Id, ["name"] = sheetPdf.Name })!["data"]!)));
        Assert.Empty(other.Call("cde.missing", [])!.AsArray());
    }

    [Fact]
    public void A_file_saved_before_issues_were_kept_opens_as_before()
    {
        var f = F();
        Sheet(f);
        var path = Path.Combine(_dir, "old.lapin");
        // Written as an older Lapin wrote it: no issues/ folder.
        PackageFiles.Save(f.Session, path);
        using (var zip = ZipFile.OpenRead(path)) Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("issues/", StringComparison.Ordinal));
        var g = F();
        var proto = new Protocol(g, PackageFiles.Create());
        proto.Call("project.open", new JsonObject { ["path"] = path });
        Assert.Single(g.Model.OfClass(Sheets.Class));
        Assert.Equal(0, (long)((JsonObject)g.Session.Query("cde.storage", [])!)["size"]!);
        Assert.False(proto.Files.Issues(g.Session).Has(new string('0', 64)));
        // Saved again without an issue: still none.
        proto.Files.Save(g.Session, path);
        Assert.Empty(Blobs(path));
    }

    [Fact]
    public void An_empty_project_field_shows_the_project_name_and_an_issued_sheet_prints_its_own_revision()
    {
        var f = F();
        var s = Sheet(f);
        var sheet = f.Model.Get(s)!;
        // No file, no name: "Project"; the file's name ("House" for House.lapin); a name the project was given wins; the title block's field wins over all.
        Assert.Equal("Project", Sheets.ProjectName(f.Session.Reader));
        PackageFiles.Create().Save(f.Session, Path.Combine(_dir, "House.lapin"));
        Assert.Equal("House", Sheets.ProjectName(f.Session.Reader));
        Assert.Contains(Texts(f, sheet), t => t == "House");
        var project = Spatial.Project(f.Model)!;
        f.Run("element.rename", new JsonObject { ["id"] = project.Id, ["name"] = "Casa Pop" });
        Assert.Equal("Casa Pop", Sheets.ProjectName(f.Session.Reader));
        f.Run("sheet.titleBlock", new JsonObject { ["id"] = s, ["fields"] = new JsonObject { ["project"] = "Locuință P+1" } });
        Assert.Contains(Texts(f, f.Model.Get(s)!), t => t == "Locuință P+1");

        // The sheet as an issue prints it: its revision and status in the title block and its row in the table, before the record exists.
        var issuing = new Sheets.Issuing("P01", "S2", "Stage 2", "2026-10-31T09:00:00Z");
        var texts = Sheets.Compose(f.Session.Reader, f.Model.Get(s)!, null, issuing).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        Assert.Equal(2, texts.Count(t => t == "P01"));   // the title block's REV and the table's row
        Assert.Contains("Stage 2", texts);
        Assert.DoesNotContain("P01", Texts(f, f.Model.Get(s)!));
    }

    static List<string> Texts(Fixture f, Entity sheet) => Sheets.Compose(f.Session.Reader, sheet).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();

    [Fact]
    public void A_reviewed_pdf_renamed_on_the_way_is_matched_by_the_issued_bytes_it_begins_with()
    {
        var f = F();
        var s = Sheet(f);
        var proto = new Protocol(f, PackageFiles.Create());
        proto.Files.Save(f.Session, Path.Combine(_dir, "house.lapin"));
        proto.Issue(new JsonObject { ["sheets"] = new JsonArray(s) });
        var issue = CdeIssues.All(f.Model).Single();
        var pdf = issue.Files.First(x => x.SheetId == s);
        var issued = Convert.FromBase64String((string)proto.Call("cde.issueFile", new JsonObject { ["id"] = issue.Id, ["name"] = pdf.Name })!["data"]!);
        // A reviewer's incremental save: the issued bytes, then more; under a name of their own.
        var reviewed = issued.Concat(System.Text.Encoding.ASCII.GetBytes("\n% reviewed\n")).ToArray();
        var match = CdeRemarks.Match(f.Model, "comments from the client.pdf", 1, null, null, reviewed);
        Assert.Equal(("issue", s, issue.Id), (match.By, match.PageSheets[0], match.IssueId));
        Assert.Equal("none", CdeRemarks.Match(f.Model, "comments from the client.pdf", 1, null, null, [.. "%PDF-1.7 other"u8]).By);
    }
}
