using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Platform.Cde.PdfRemarks;
using Lapin.Platform.Cde.References;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The reference documents (docs/plan-cde-references.md, decision 874, stage R1): a document added with its file (kept once
/// by its SHA-256) or requested without one, updated, moved, superseded, withdrawn and deleted, each one undo step; expired
/// derived from its date; the folder tree and the type list as the project's own copy; a certificate's checklist; the files
/// in the package under references/blobs/ (a round trip byte for byte, the unreferenced dropped, an older package opening
/// unchanged, the keep limit); the copies in the project folder and the scan of it; the PDF's text and the suggestions.
/// </summary>
public sealed class CdeReferencesTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-refs-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static Fixture F() => new(new ArchitectureModule());

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

        public JsonNode? Run(string command, JsonObject args) => Call("doc.execute", new JsonObject { ["command"] = command, ["args"] = args });
        public JsonNode? Ask(string query, JsonObject args) => Call("doc.query", new JsonObject { ["name"] = query, ["args"] = args });
    }

    static JsonObject File(string name, byte[] bytes) => new() { ["name"] = name, ["data"] = Convert.ToBase64String(bytes) };

    static string Add(Fixture f, JsonObject a) => (string)f.Run("references.add", a)!["id"]!;

    static JsonObject Get(Fixture f, string id, string? today = null) =>
        (JsonObject)f.Session.Query("references.get", today == null ? new JsonObject { ["id"] = id } : new JsonObject { ["id"] = id, ["today"] = today })!;

    static JsonArray List(Fixture f, JsonObject a) => (JsonArray)f.Session.Query("references.list", a)!;

    /// <summary>A one-page PDF whose page shows the lines given, in Helvetica (WinAnsi), one under the other.</summary>
    static byte[] Pdf(params string[] lines)
    {
        var content = new StringBuilder("BT /F1 11 Tf 72 790 Td 14 TL\n");
        foreach (var line in lines) content.Append('(').Append(line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")).Append(") Tj T*\n");
        content.Append("ET\n");
        var stream = Encoding.Latin1.GetBytes(content.ToString());
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            $"<< /Length {stream.Length} >>\nstream\n{Encoding.Latin1.GetString(stream)}endstream",
        ];
        using var ms = new MemoryStream();
        void W(string s) => ms.Write(Encoding.Latin1.GetBytes(s));
        W("%PDF-1.4\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Length; i++) { offsets.Add(ms.Position); W($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = ms.Position;
        W($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) W($"{o:D10} 00000 n \n");
        W($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    static readonly byte[] Certificate = Pdf("PRIMARIA MUNICIPIULUI IASI", "CERTIFICAT DE URBANISM", "Nr. 1234 din 15.03.2025", "In scopul: construire locuinta P+1",
        "Prezentul certificat de urbanism are valabilitate de 24 de luni de la data emiterii.");

    static List<string> Entries(string package, string prefix)
    {
        using var zip = ZipFile.OpenRead(package);
        return zip.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal)).Select(e => e.FullName).Order().ToList();
    }

    // ------------------------------------------------------------ add, dedup, requested

    [Fact]
    public void A_document_added_with_its_file_goes_into_its_types_folder_and_its_bytes_are_kept_once()
    {
        var f = F();
        var id = Add(f, new JsonObject { ["files"] = new JsonArray(File("CU 1234.pdf", Certificate)), ["type"] = "cu", ["issuer"] = "Primăria Iași", ["number"] = "1234", ["date"] = "15.03.2025", ["phase"] = "DTAC" });
        var doc = Get(f, id);
        Assert.Equal("CU 1234", (string)doc["title"]!);
        Assert.Equal("input-cu", (string)doc["folder"]!);
        Assert.Equal("01 Input data / Urbanism certificate", (string)doc["folderPath"]!);
        Assert.Equal("received", (string)doc["status"]!);
        Assert.Equal("2025-03-15", (string)doc["date"]!);
        Assert.Equal(["DTAC"], doc["phase"]!.AsArray().Select(x => (string?)x));
        var file = doc["files"]![0]!;
        Assert.Equal(Hashes.Sha256Hex(Certificate), (string)file["sha256"]!);
        Assert.True((bool)file["held"]!);
        Assert.Equal("application/pdf", (string)file["mime"]!);
        var back = (JsonObject)f.Session.Query("references.file", new JsonObject { ["id"] = id })!;
        Assert.Equal(Certificate, Convert.FromBase64String((string)back["data"]!));

        // The same bytes again, in another document: kept once, and said.
        var again = (JsonObject)f.Run("references.add", new JsonObject { ["files"] = new JsonArray(File("copy.pdf", Certificate)), ["title"] = "Copy" })!;
        Assert.Equal([id], again["duplicates"]!.AsArray().Select(x => (string?)x));
        Assert.Equal("altul", (string)Get(f, (string)again["id"]!)["type"]!);
        Assert.Single(References.HeldHashes(f.Model));

        // A type, a folder or a date the project does not know: refused.
        Assert.Throws<CommandException>(() => f.Run("references.add", new JsonObject { ["title"] = "X", ["type"] = "nope" }));
        Assert.Throws<CommandException>(() => f.Run("references.add", new JsonObject { ["title"] = "X", ["folder"] = "nope" }));
        Assert.Throws<CommandException>(() => f.Run("references.add", new JsonObject { ["title"] = "X", ["folder"] = "issued-sheets" }));
        Assert.Throws<CommandException>(() => f.Run("references.add", new JsonObject { ["title"] = "X", ["date"] = "31.02.2025" }));
        Assert.Throws<CommandException>(() => f.Run("references.add", new JsonObject()));
    }

    [Fact]
    public void A_requested_document_has_no_file_until_one_is_attached_then_it_is_received()
    {
        var f = F();
        var id = Add(f, new JsonObject { ["title"] = "Aviz ISU", ["type"] = "aviz", ["folder"] = "permits-fire", ["requestedBy"] = "Ionuț", ["requestedAt"] = "2026-11-01" });
        var doc = Get(f, id);
        Assert.Equal("requested", (string)doc["status"]!);
        Assert.Equal("2026-11-01", (string)doc["requestedAt"]!);
        Assert.Equal("Ionuț", (string)doc["requestedBy"]!);
        Assert.Empty(doc["files"]!.AsArray());
        Assert.Throws<CommandException>(() => f.Session.Query("references.file", new JsonObject { ["id"] = id }));

        f.Run("references.attach", new JsonObject { ["id"] = id, ["files"] = new JsonArray(File("aviz.pdf", [1, 2, 3]), File("aviz.pdf", [4, 5])) });
        doc = Get(f, id);
        Assert.Equal("received", (string)doc["status"]!);
        Assert.Equal(["aviz.pdf", "aviz (2).pdf"], doc["files"]!.AsArray().Select(x => (string?)x!["name"]));
        f.Run("references.detach", new JsonObject { ["id"] = id, ["name"] = "aviz (2).pdf" });
        Assert.Single(Get(f, id)["files"]!.AsArray());
        f.Session.Undo();
        Assert.Equal(2, Get(f, id)["files"]!.AsArray().Count);
    }

    // ------------------------------------------------------------ update, move, supersede, withdraw, delete, each undone

    [Fact]
    public void Update_move_supersede_withdraw_and_delete_are_each_one_undo_step()
    {
        var f = F();
        var old = Add(f, new JsonObject { ["files"] = new JsonArray(File("aviz apa 2024.pdf", [1])), ["type"] = "aviz", ["number"] = "77", ["validUntil"] = "2027-01-01" });
        var renewed = Add(f, new JsonObject { ["files"] = new JsonArray(File("aviz apa 2026.pdf", [2])), ["type"] = "aviz", ["number"] = "912" });
        var cu = Add(f, new JsonObject { ["title"] = "CU", ["type"] = "cu", ["requires"] = new JsonArray(new JsonObject { ["key"] = "water", ["document"] = old }), ["relatesTo"] = new JsonArray(old) });

        f.Run("references.update", new JsonObject { ["id"] = old, ["issuer"] = "Apavital", ["number"] = "", ["fields"] = new JsonObject { ["branch"] = "Iași" }, ["title"] = "Aviz apă" });
        var doc = Get(f, old);
        Assert.Equal("Apavital", (string)doc["issuer"]!);
        Assert.Null(doc["number"]);
        Assert.Equal("Aviz apă", (string)doc["title"]!);
        Assert.Equal("Iași", (string)doc["fields"]!["branch"]!);
        Assert.NotNull(doc["modifiedAt"]);
        f.Session.Undo();
        Assert.Equal("77", (string)Get(f, old)["number"]!);
        Assert.Null(Get(f, old)["issuer"]);

        f.Run("references.move", new JsonObject { ["ids"] = new JsonArray(old, renewed), ["folder"] = "permits-utilities" });
        Assert.Equal("permits-utilities", (string)Get(f, old)["folder"]!);
        Assert.Equal("permits-utilities", (string)Get(f, renewed)["folder"]!);
        Assert.Equal(2, ((JsonArray)List(f, new JsonObject { ["folder"] = "permits-utilities" })).Count);
        Assert.Equal(2, ((JsonArray)List(f, new JsonObject { ["folder"] = "permits", ["deep"] = true })).Count);
        Assert.Empty(List(f, new JsonObject { ["folder"] = "permits" }));
        f.Session.Undo();
        Assert.Equal("permits-other", (string)Get(f, old)["folder"]!);

        f.Run("references.supersede", new JsonObject { ["id"] = old, ["by"] = renewed });
        Assert.Equal("superseded", (string)Get(f, old)["status"]!);
        Assert.Equal(renewed, (string)Get(f, old)["supersededBy"]!);
        Assert.Equal([old], Get(f, renewed)["supersedes"]!.AsArray().Select(x => (string?)x));
        // The checklist follows to the newer document.
        var item = f.Session.Query("references.checklist", new JsonObject { ["id"] = cu })!["items"]![0]!;
        Assert.Equal(renewed, (string)item["document"]!);
        Assert.Equal("912", (string)item["number"]!);
        Assert.Throws<CommandException>(() => f.Run("references.supersede", new JsonObject { ["id"] = renewed, ["by"] = old }));
        Assert.Throws<CommandException>(() => f.Run("references.supersede", new JsonObject { ["id"] = old, ["by"] = old }));
        f.Session.Undo();
        Assert.Equal("received", (string)Get(f, old)["status"]!);
        Assert.Null(Get(f, old)["supersededBy"]);

        Assert.Throws<CommandException>(() => f.Run("references.withdraw", new JsonObject { ["id"] = old }));
        f.Run("references.withdraw", new JsonObject { ["id"] = old, ["reason"] = "Annulled by the operator" });
        Assert.Equal("withdrawn", (string)Get(f, old)["status"]!);
        Assert.Equal("Annulled by the operator", (string)Get(f, old)["reason"]!);
        Assert.Equal("missing", (string)f.Session.Query("references.checklist", new JsonObject { ["id"] = cu })!["items"]![0]!["state"]!);
        f.Session.Undo();
        Assert.Equal("received", (string)Get(f, old)["status"]!);

        // Deleted: gone, and the links to it with it; undone, all back.
        f.Run("references.delete", new JsonObject { ["id"] = old });
        Assert.Throws<CommandException>(() => Get(f, old));
        Assert.Null(Get(f, cu)["requires"]![0]!["document"]);
        Assert.Null(Get(f, cu)["relatesTo"]);
        f.Session.Undo();
        Assert.Equal(old, (string)Get(f, cu)["requires"]![0]!["document"]!);
        Assert.Equal([old], Get(f, cu)["relatesTo"]!.AsArray().Select(x => (string?)x));
        Assert.Equal("77", (string)Get(f, old)["number"]!);
    }

    // ------------------------------------------------------------ expiry, filters

    [Fact]
    public void Expired_is_derived_from_valid_until_and_expiring_within_filters_the_list()
    {
        var f = F();
        var past = Add(f, new JsonObject { ["title"] = "Old permit", ["type"] = "aviz", ["files"] = new JsonArray(File("a.pdf", [1])), ["validUntil"] = "2026-10-31" });
        var soon = Add(f, new JsonObject { ["title"] = "Permit soon", ["type"] = "aviz", ["files"] = new JsonArray(File("b.pdf", [2])), ["validUntil"] = "2026-12-10" });
        var later = Add(f, new JsonObject { ["title"] = "Permit later", ["type"] = "acord", ["files"] = new JsonArray(File("c.pdf", [3])), ["validUntil"] = "2027-06-01", ["phase"] = new JsonArray("PT", "DE") });
        var asked = Add(f, new JsonObject { ["title"] = "Asked", ["type"] = "aviz", ["validUntil"] = "2026-12-01" });
        const string today = "2026-11-25";

        Assert.Equal("expired", (string)Get(f, past, today)["status"]!);
        Assert.True((bool)Get(f, past, today)["expired"]!);
        // Never stored: the entity keeps "received".
        Assert.Equal("received", f.Model.Get(past)!.Str("status"));
        Assert.Equal("received", (string)Get(f, past, "2026-10-31")["status"]!);

        static List<string?> Ids(JsonArray a) => [.. a.Select(x => (string?)x!["id"])];
        Assert.Equal([soon], Ids(List(f, new JsonObject { ["expiringWithin"] = 30, ["today"] = today })));
        Assert.Equal([soon, later], Ids(List(f, new JsonObject { ["expiringWithin"] = 365, ["today"] = today })));
        Assert.Equal([past], Ids(List(f, new JsonObject { ["status"] = "expired", ["today"] = today })));
        Assert.Equal([asked], Ids(List(f, new JsonObject { ["status"] = "requested", ["today"] = today })));
        Assert.Equal([later], Ids(List(f, new JsonObject { ["type"] = "acord" })));
        Assert.Equal([later], Ids(List(f, new JsonObject { ["phase"] = "de" })));
        Assert.Equal([soon], Ids(List(f, new JsonObject { ["q"] = "SOON" })));
    }

    // ------------------------------------------------------------ the checklist

    [Fact]
    public void A_certificates_checklist_says_what_is_missing_requested_received_or_expired()
    {
        var f = F();
        var water = Add(f, new JsonObject { ["title"] = "Aviz apă", ["type"] = "aviz", ["files"] = new JsonArray(File("w.pdf", [1])), ["number"] = "12", ["date"] = "2026-05-02" });
        var fire = Add(f, new JsonObject { ["title"] = "Aviz ISU", ["type"] = "aviz" });
        var env = Add(f, new JsonObject { ["title"] = "APM", ["type"] = "acord", ["files"] = new JsonArray(File("e.pdf", [2])), ["validUntil"] = "2026-01-01" });
        var cu = Add(f, new JsonObject
        {
            ["title"] = "CU 1234", ["type"] = "cu",
            ["requires"] = new JsonArray("water", new JsonObject { ["key"] = "fire", ["document"] = fire }, new JsonObject { ["key"] = "environment", ["document"] = env },
                new JsonObject { ["label"] = "Aviz drumuri județene" }),
        });
        var list = (JsonObject)f.Session.Query("references.checklist", new JsonObject { ["id"] = cu, ["today"] = "2026-11-25" })!;
        var items = list["items"]!.AsArray();
        Assert.Equal(["water", "fire", "environment", "aviz-drumuri-judetene"], items.Select(x => (string?)x!["key"]));
        Assert.Equal(["missing", "requested", "expired", "missing"], items.Select(x => (string?)x!["state"]));
        Assert.Equal("Water supply", (string)items[0]!["text"]!);
        Assert.Equal(3, (int)list["missing"]!);
        Assert.False((bool)list["complete"]!);
        Assert.DoesNotContain("water", list["options"]!.AsArray().Select(x => (string?)x!["key"]));
        Assert.Contains("gas", list["options"]!.AsArray().Select(x => (string?)x!["key"]));

        // The water permit answers its item: received, with its number and date.
        var requires = (JsonArray)Get(f, cu)["requires"]!.DeepClone();
        requires[0]!["document"] = water;
        f.Run("references.update", new JsonObject { ["id"] = cu, ["requires"] = requires });
        var first = f.Session.Query("references.checklist", new JsonObject { ["id"] = cu })!["items"]![0]!;
        Assert.Equal("received", (string)first["state"]!);
        Assert.Equal("12", (string)first["number"]!);
        Assert.Equal("2026-05-02", (string)first["date"]!);

        // A checklist naming a document that is not one, or a key twice: refused.
        Assert.Throws<CommandException>(() => f.Run("references.update", new JsonObject { ["id"] = cu, ["requires"] = new JsonArray(new JsonObject { ["key"] = "gas", ["document"] = "nope" }) }));
        Assert.Throws<CommandException>(() => f.Run("references.update", new JsonObject { ["id"] = cu, ["requires"] = new JsonArray("gas", "gas") }));
    }

    // ------------------------------------------------------------ folders and types

    [Fact]
    public void The_folder_tree_and_type_list_are_the_defaults_until_the_project_changes_its_own_copy()
    {
        var f = F();
        var tree = (JsonObject)f.Session.Query("references.folders", [])!;
        var top = tree["folders"]!.AsArray();
        Assert.Equal(["admin", "input", "permits", "authorisations", "models", "issued-documents", "issued-sheets"], top.Select(x => (string?)x!["id"]));
        Assert.Equal("01 Date de intrare", (string)top[1]!["name"]!["ro"]!);
        Assert.Equal("input-cu", (string)tree["typeFolders"]!["cu"]!);
        Assert.Equal(References.DefaultKeepLimit, (long)tree["keepLimit"]!);
        Assert.Null(References.Settings(f.Model)["folders"]);

        var id = Add(f, new JsonObject { ["title"] = "Topo", ["type"] = "topo", ["files"] = new JsonArray(File("t.pdf", [9])) });
        tree = (JsonObject)f.Session.Query("references.folders", [])!;
        var input = tree["folders"]![1]!;
        Assert.Equal(0, (int)input["count"]!);
        Assert.Equal(1, (int)input["total"]!);
        Assert.Equal(1, (int)input["children"]![2]!["count"]!);

        // Renamed (its id stays) and a folder added: the documents keep their folder.
        var folders = (JsonArray)top.DeepClone();
        folders[1]!["children"]![2]!["name"] = new JsonObject { ["ro"] = "Topografie", ["en"] = "Survey" };
        folders.Add((JsonNode)new JsonObject { ["id"] = "site", ["name"] = new JsonObject { ["en"] = "Site photos" }, ["children"] = new JsonArray() });
        f.Run("references.setFolders", new JsonObject { ["folders"] = folders, ["typeFolders"] = new JsonObject { ["cu"] = "admin" } });
        Assert.Equal("01 Input data / Survey", (string)Get(f, id)["folderPath"]!);
        Assert.Equal("admin", (string)References.TypeFolders(f.Model)["cu"]!);
        Assert.Equal("admin", (string)Get(f, Add(f, new JsonObject { ["title"] = "CU", ["type"] = "cu" }))["folder"]!);
        f.Session.Undo();
        f.Session.Undo();
        Assert.Null(References.Settings(f.Model)["folders"]);

        // A folder holding documents stays; ids twice are refused.
        var without = (JsonArray)top.DeepClone();
        without[1]!["children"]!.AsArray().RemoveAt(2);
        Assert.Throws<CommandException>(() => f.Run("references.setFolders", new JsonObject { ["folders"] = without }));
        var twice = (JsonArray)top.DeepClone();
        twice.Add((JsonNode)new JsonObject { ["id"] = "admin", ["name"] = "Again" });
        Assert.Throws<CommandException>(() => f.Run("references.setFolders", new JsonObject { ["folders"] = twice }));

        // The type list: a type in use stays; a new type with its extra fields and validity.
        var types = (JsonArray)f.Session.Query("references.types", [])!["types"]!.DeepClone();
        Assert.Throws<CommandException>(() => f.Run("references.setTypes", new JsonObject { ["types"] = new JsonArray([.. types.Where(t => (string?)t!["id"] != "topo").Select(t => t!.DeepClone())]) }));
        types.Add((JsonNode)new JsonObject { ["id"] = "foto", ["name"] = new JsonObject { ["en"] = "Site photo" }, ["folder"] = "input-topo", ["validityMonths"] = 6, ["fields"] = new JsonArray(new JsonObject { ["key"] = "camera" }) });
        f.Run("references.setTypes", new JsonObject { ["types"] = types });
        Assert.Equal("input-topo", (string)Get(f, Add(f, new JsonObject { ["title"] = "Photo", ["type"] = "foto" }))["folder"]!);
        Assert.Throws<CommandException>(() => f.Run("references.setTypes", new JsonObject { ["types"] = new JsonArray(new JsonObject { ["id"] = "a", ["name"] = "A" }, new JsonObject { ["id"] = "a", ["name"] = "B" }) }));
    }

    // ------------------------------------------------------------ the package

    [Fact]
    public void The_files_go_into_the_package_under_references_blobs_and_come_back_byte_for_byte()
    {
        var f = F();
        var proto = new Protocol(f, PackageFiles.Create());
        var big = new byte[3000];
        new Random(7).NextBytes(big);
        proto.Run("references.setKeepLimit", new JsonObject { ["bytes"] = 2000 });
        var cu = (string)proto.Run("references.add", new JsonObject { ["files"] = new JsonArray(File("cu.pdf", Certificate)), ["type"] = "cu", ["number"] = "1234" })!["id"]!;
        var both = (string)proto.Run("references.add", new JsonObject { ["files"] = new JsonArray(File("small.txt", [1, 2, 3]), File("large.bin", big)), ["title"] = "Study" })!["id"]!;
        var large = Get(f, both)["files"]![1]!;
        Assert.False((bool)large["held"]!);
        Assert.Equal(3000, (long)large["size"]!);
        Assert.Equal(Hashes.Sha256Hex(big), (string)large["sha256"]!);

        var path = Path.Combine(_dir, "house.lapin");
        proto.Files.Save(f.Session, path);
        // The held files (the large one is not), and the readable index; nothing of them in the project state's parts.
        Assert.Equal(new byte[][] { Certificate, [1, 2, 3] }.Select(b => PackageIssues.ReferencesPrefix + Hashes.Sha256Hex(b)).Order().ToList(), Entries(path, PackageIssues.ReferencesPrefix));
        using (var zip = ZipFile.OpenRead(path))
        {
            var index = JsonNode.Parse(zip.GetEntry(PackageIssues.ReferencesIndexPath)!.Open())!;
            Assert.Equal("lapin-references/1", (string)index["format"]!);
            Assert.Equal(2, index["documents"]!.AsArray().Count);
            Assert.Equal("01 Input data / Urbanism certificate", (string)index["documents"]![0]!["folder"]!);
            var manifest = JsonNode.Parse(zip.GetEntry("manifest.json")!.Open())!;
            Assert.DoesNotContain(manifest["parts"]!.AsArray(), p => ((string)p!["path"]!).StartsWith("references/", StringComparison.Ordinal));
            Assert.Null(manifest["foreign"]);
        }

        // Opened again elsewhere: the same register and the same bytes.
        var g = F();
        var again = new Protocol(g, PackageFiles.Create());
        again.Call("project.open", new JsonObject { ["path"] = path });
        Assert.True(JsonNode.DeepEquals(proto.Ask("references.list", []), again.Ask("references.list", [])));
        Assert.Equal(Certificate, Convert.FromBase64String((string)again.Ask("references.file", new JsonObject { ["id"] = cu })!["data"]!));
        Assert.Equal(new byte[] { 1, 2, 3 }, Convert.FromBase64String((string)again.Ask("references.file", new JsonObject { ["id"] = both, ["name"] = "small.txt" })!["data"]!));
        Assert.Contains("larger than the project keeps", Assert.Throws<InvalidOperationException>(() => again.Ask("references.file", new JsonObject { ["id"] = both, ["name"] = "large.bin" })).Message);

        // Saved again: carried over; a file let go is dropped from the next save.
        var copy = Path.Combine(_dir, "copy.lapin");
        again.Files.Save(g.Session, copy);
        Assert.Equal(Entries(path, PackageIssues.ReferencesPrefix), Entries(copy, PackageIssues.ReferencesPrefix));
        again.Run("references.detach", new JsonObject { ["id"] = both, ["name"] = "small.txt" });
        again.Files.Save(g.Session, copy);
        Assert.Equal([PackageIssues.ReferencesPrefix + Hashes.Sha256Hex(Certificate)], Entries(copy, PackageIssues.ReferencesPrefix));
        // The issues' store is not the references': nothing under issues/.
        Assert.Empty(Entries(copy, "issues/"));
    }

    [Fact]
    public void A_package_without_references_opens_unchanged_and_writes_none()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var proto = new Protocol(f, PackageFiles.Create());
        var path = Path.Combine(_dir, "old.lapin");
        proto.Files.Save(f.Session, path);
        Assert.Empty(Entries(path, "references/"));
        var bytes = System.IO.File.ReadAllBytes(path);

        var g = F();
        var again = new Protocol(g, PackageFiles.Create());
        again.Call("project.open", new JsonObject { ["path"] = path });
        Assert.Empty((JsonArray)again.Ask("references.list", [])!);
        Assert.Equal(f.Model.Entities.Count(), g.Model.Entities.Count());
        again.Files.Save(g.Session, path);
        Assert.Empty(Entries(path, "references/"));
        // Another tool's references/ folder in a file is Lapin's name now: not kept as a foreign entry.
        Assert.Contains("references", PackageForeign.Owned);
        Assert.NotEmpty(bytes);
    }

    // ------------------------------------------------------------ the project folder

    [Fact]
    public void With_a_project_folder_each_file_is_copied_under_Reference_moved_with_its_document_and_the_folder_scanned()
    {
        var f = F();
        var folder = Path.Combine(_dir, "Project");
        Directory.CreateDirectory(folder);
        f.Run("cde.setFolder", new JsonObject { ["path"] = folder });
        var id = Add(f, new JsonObject { ["files"] = new JsonArray(File("CU 1234.pdf", Certificate)), ["type"] = "cu" });
        var copy = Path.Combine(folder, "Reference", "01 Input data", "Urbanism certificate", "CU 1234.pdf");
        Assert.Equal(Certificate, System.IO.File.ReadAllBytes(copy));

        f.Run("references.move", new JsonObject { ["id"] = id, ["folder"] = "authorisations" });
        var moved = Path.Combine(folder, "Reference", "03 Authorisations", "CU 1234.pdf");
        Assert.False(System.IO.File.Exists(copy));
        Assert.Equal(Certificate, System.IO.File.ReadAllBytes(moved));

        // A folder renamed: the copies follow. The language of the folders on disk: the project's.
        var folders = References.Folders(f.Model);
        folders[3]!["name"] = new JsonObject { ["ro"] = "03 Autorizații de construire", ["en"] = "03 Building permits" };
        f.Run("references.setFolders", new JsonObject { ["folders"] = folders, ["language"] = "ro" });
        var renamed = Path.Combine(folder, "Reference", "03 Autorizații de construire", "CU 1234.pdf");
        Assert.True(System.IO.File.Exists(renamed));
        Assert.False(System.IO.File.Exists(moved));

        // Files the register does not hold, found with a folder and a type from their path; a copy gone is listed as missing.
        var fire = Path.Combine(folder, "Reference", "02 Avize și acorduri", "Securitate la incendiu");
        Directory.CreateDirectory(fire);
        System.IO.File.WriteAllBytes(Path.Combine(fire, "Aviz ISU 55.pdf"), [5, 5, 5]);
        System.IO.File.WriteAllBytes(Path.Combine(folder, "Reference", "notes.txt"), [7]);
        var scan = (JsonObject)f.Session.Query("references.scan", [])!;
        var found = scan["files"]!.AsArray().OrderBy(x => (string?)x!["name"]).ToList();
        Assert.Equal(["Aviz ISU 55.pdf", "notes.txt"], found.Select(x => (string?)x!["name"]));
        Assert.Equal("permits-fire", (string)found[0]!["folder"]!);
        Assert.Equal("aviz", (string)found[0]!["type"]!);
        Assert.Null(found[1]!["folder"]);
        Assert.Empty(scan["missing"]!.AsArray());
        System.IO.File.Delete(renamed);
        scan = (JsonObject)f.Session.Query("references.scan", [])!;
        Assert.Equal(id, (string)scan["missing"]![0]!["id"]!);
        // The register is the truth: the file still comes from the project's file.
        Assert.Equal(Certificate, Convert.FromBase64String((string)f.Session.Query("references.file", new JsonObject { ["id"] = id })!["data"]!));

        // The project folder gone: a warning, never an error.
        Directory.Delete(folder, recursive: true);
        f.Session.TakeWarnings();
        var r = (JsonObject)f.Run("references.add", new JsonObject { ["files"] = new JsonArray(File("x.pdf", [8])), ["title"] = "X" })!;
        Assert.Contains("is not on this computer", (string)r["warnings"]![0]!);
        Assert.False((bool)f.Session.Query("references.scan", [])!["exists"]!);
    }

    [Fact]
    public void A_file_above_the_keep_limit_is_read_from_its_project_folder_copy()
    {
        var f = F();
        var folder = Path.Combine(_dir, "Big");
        Directory.CreateDirectory(folder);
        f.Run("cde.setFolder", new JsonObject { ["path"] = folder });
        f.Run("references.setKeepLimit", new JsonObject { ["bytes"] = 10 });
        var bytes = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
        var r = (JsonObject)f.Run("references.add", new JsonObject { ["files"] = new JsonArray(File("geo.pdf", bytes)), ["type"] = "studiu-geo" })!;
        Assert.Contains("larger than the project keeps", (string)r["warnings"]![0]!);
        var id = (string)r["id"]!;
        Assert.Empty(References.HeldHashes(f.Model));
        Assert.Equal(bytes, Convert.FromBase64String((string)f.Session.Query("references.file", new JsonObject { ["id"] = id })!["data"]!));
        f.Run("references.setKeepLimit", new JsonObject { ["bytes"] = null });
        Assert.Equal(References.DefaultKeepLimit, References.KeepLimit(f.Model));
    }

    [Fact]
    public void A_file_added_by_path_on_the_desktop_and_guests_kept_from_the_hosts_disk()
    {
        var f = F();
        var source = Path.Combine(_dir, "Extras CF.pdf");
        System.IO.File.WriteAllBytes(source, [1, 2, 3, 4]);
        var id = Add(f, new JsonObject { ["files"] = new JsonArray(new JsonObject { ["path"] = source }), ["type"] = "cf" });
        Assert.Equal("Extras CF", (string)Get(f, id)["title"]!);
        Assert.Equal("input-cf", (string)Get(f, id)["folder"]!);

        // A guest: reads the register, never the host's disk, never deletes outright.
        Assert.Null(EngineDispatcher.AllowedRemote("doc.query", new JsonObject { ["name"] = "references.list" }, "viewer"));
        Assert.Null(EngineDispatcher.AllowedRemote("doc.query", new JsonObject { ["name"] = "references.text", ["args"] = new JsonObject { ["id"] = id } }, "viewer"));
        Assert.NotNull(EngineDispatcher.AllowedRemote("doc.query", new JsonObject { ["name"] = "references.scan" }, "editor"));
        Assert.NotNull(EngineDispatcher.AllowedRemote("doc.execute", new JsonObject { ["command"] = "references.add", ["args"] = new JsonObject { ["title"] = "X" } }, "viewer"));
        Assert.Null(EngineDispatcher.AllowedRemote("doc.execute", new JsonObject { ["command"] = "references.add", ["args"] = new JsonObject { ["title"] = "X" } }, "editor"));
        Assert.NotNull(EngineDispatcher.AllowedRemote("doc.execute", new JsonObject { ["command"] = "references.add", ["args"] = new JsonObject { ["files"] = new JsonArray(new JsonObject { ["path"] = source }) } }, "editor"));
        Assert.NotNull(EngineDispatcher.AllowedRemote("doc.execute", new JsonObject { ["command"] = "references.delete", ["args"] = new JsonObject { ["id"] = id } }, "owner"));
    }

    // ------------------------------------------------------------ text and suggestions

    [Fact]
    public void A_PDFs_text_is_read_by_pages_and_suggests_the_number_date_issuer_validity_and_type()
    {
        var f = F();
        var id = Add(f, new JsonObject { ["files"] = new JsonArray(File("scan.pdf", Certificate)), ["title"] = "Received" });
        var text = (JsonObject)f.Session.Query("references.text", new JsonObject { ["id"] = id })!;
        Assert.Equal(1, (int)text["pageCount"]!);
        var page = (string)text["pages"]![0]!["text"]!;
        Assert.Equal("PRIMARIA MUNICIPIULUI IASI\nCERTIFICAT DE URBANISM\nNr. 1234 din 15.03.2025", string.Join('\n', page.Split('\n').Take(3)));

        var s = (JsonObject)f.Session.Query("references.suggest", new JsonObject { ["id"] = id })!;
        Assert.Equal("1234", (string)s["number"]!);
        Assert.Equal("2025-03-15", (string)s["date"]!);
        Assert.Equal("cu", (string)s["type"]!);
        Assert.Equal("PRIMARIA MUNICIPIULUI IASI", (string)s["issuer"]!);
        Assert.Equal(24, (int)s["validityMonths"]!);
        Assert.Equal("2027-03-15", (string)s["validUntil"]!);
        Assert.Null(s["validUntilIndicative"]);
        Assert.Equal("Nr. 1234 din 15.03.2025", (string)s["evidence"]!["number"]!);

        // A scan without a text layer: no text, no suggestion, said.
        var blank = Add(f, new JsonObject { ["files"] = new JsonArray(File("blank.pdf", Pdf())), ["title"] = "Blank" });
        var none = (JsonObject)f.Session.Query("references.suggest", new JsonObject { ["id"] = blank })!;
        Assert.Null(none["number"]);
        Assert.Contains("no text layer", (string)none["warning"]!);
        // An encrypted file is refused with why.
        var encrypted = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(Pdf("x")).Replace("/Root 1 0 R", "/Root 1 0 R /Encrypt << /Filter /Standard >>"));
        Assert.Equal("it is encrypted", PdfPageText.Read(encrypted).Unreadable);
    }

    [Theory]
    // Romanian forms.
    [InlineData("PRIMĂRIA ORAȘULUI HUȘI\nCERTIFICAT DE URBANISM\nNr. 56/12.05.2025\nValabil până la 12.05.2027.", "56", "2025-05-12", "2027-05-12", "cu", "PRIMĂRIA ORAȘULUI HUȘI")]
    [InlineData("AVIZ DE PRINCIPIU\nnr. AV-312 din 3 martie 2026\nEmitent: Delgaz Grid SA\nAvizul este valabil 12 luni de la emitere.", "AV-312", "2026-03-03", "2027-03-03", "aviz", "Delgaz Grid SA")]
    [InlineData("EXTRAS DE CARTE FUNCIARĂ PENTRU INFORMARE\nCarte Funciară Nr. 60123 Iași\nData: 02.10.2026", "60123", "2026-10-02", null, "cf", null)]
    [InlineData("Nr. crt. | Denumire\n1 | Proiect\nAUTORIZAȚIE DE CONSTRUIRE Nr. 88 din 01/09/2026", "88", "2026-09-01", null, "autorizatie", null)]
    // English forms.
    [InlineData("Geotechnical Report\nNo. GT-2026-17 dated 14 July 2026\nIssued by: Ground Lab Ltd", "GT-2026-17", "2026-07-14", null, "studiu-geo", "Ground Lab Ltd")]
    [InlineData("BUILDING PERMIT\nNumber: 4471\nDate of issue: November 3, 2026\nThis permit is valid until 2028-11-03.", "4471", "2026-11-03", "2028-11-03", "autorizatie", null)]
    [InlineData("Topographic survey, valid for 6 months.\nDate: 2026-01-31", null, "2026-01-31", "2026-07-31", "topo", null)]
    public void Suggestions_are_read_from_Romanian_and_English_texts(string text, string? number, string? date, string? validUntil, string? type, string? issuer)
    {
        var s = ReferenceSuggest.From(text);
        Assert.Equal(number, (string?)s["number"]);
        Assert.Equal(date, (string?)s["date"]);
        Assert.Equal(validUntil, (string?)s["validUntil"]);
        Assert.Equal(type, (string?)s["type"]);
        Assert.Equal(issuer, (string?)s["issuer"]);
    }

    [Fact]
    public void Nothing_is_suggested_from_a_text_without_the_patterns()
    {
        var s = ReferenceSuggest.From("A letter about the weather, 12 apples and the number of floors.");
        Assert.Null(s["number"]);
        Assert.Null(s["date"]);
        Assert.Null(s["validUntil"]);
        Assert.Null(s["type"]);
        Assert.Null(s["issuer"]);
        Assert.Equal("2026-11-25", ReferenceDates.Parse("25.11.2026"));
        Assert.Equal("2026-11-25", ReferenceDates.Parse("25 noiembrie 2026"));
        Assert.Equal("2026-11-25", ReferenceDates.Parse("November 25, 2026"));
        Assert.Null(ReferenceDates.Parse("29.02.2026"));
    }
}
