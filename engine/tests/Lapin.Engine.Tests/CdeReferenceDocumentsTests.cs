using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Platform.Cde;
using Lapin.Platform.Documents;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The generator reading the reference documents (docs/plan-cde-references.md §6, decision 878, stage R5): the fields
/// <c>ref.&lt;type&gt;.*</c> (the latest received, valid document of the type; expired, superseded, withdrawn and requested ones
/// never), the collections <c>references</c> and <c>checklist</c> with their <c>where</c>, the tables <i>Permits and agreements</i>
/// and <i>Reference documents</i> with their filters, the starter templates (indicative) filled and written as DOCX, ODT and PDF,
/// the architecture report's certificate rows, and an issue that sends reference documents as copies of their files (hashed in
/// the manifest, rows of the transmittal; a file not held here left out and said, or sent from the bytes the interface fetched).
/// </summary>
public sealed class CdeReferenceDocumentsTests
{
    static Fixture F() => new(new ArchitectureModule());

    /// <summary>A PDF of <paramref name="pages"/> pages, each showing the text given.</summary>
    static byte[] Pdf(int pages, string text)
    {
        var stream = Encoding.Latin1.GetBytes($"BT /F1 11 Tf 72 790 Td ({text}) Tj ET\n");
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pages).Select(i => $"{5 + i} 0 R"))}] /Count {pages} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            $"<< /Length {stream.Length} >>\nstream\n{Encoding.Latin1.GetString(stream)}endstream",
        };
        for (var i = 0; i < pages; i++) objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents 4 0 R >>");
        using var ms = new MemoryStream();
        void W(string s) => ms.Write(Encoding.Latin1.GetBytes(s));
        W("%PDF-1.4\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++) { offsets.Add(ms.Position); W($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = ms.Position;
        W($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) W($"{o:D10} 00000 n \n");
        W($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    static JsonObject File(string name, byte[] bytes) => new() { ["name"] = name, ["data"] = Convert.ToBase64String(bytes) };

    static string Add(Fixture f, JsonObject a) => (string)f.Run("references.add", a)!["id"]!;

    static readonly byte[] CuPdf = Pdf(2, "CERTIFICAT DE URBANISM Nr. 45 din 15.03.2026");
    static readonly byte[] WaterPdf = Pdf(1, "AVIZ Apa-Canal Nr. A-7");
    static readonly byte[] GasPdf = Pdf(1, "AVIZ gaze Nr. G-3");
    static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 0];

    /// <summary>The documents of a permit application half way: two certificates (the older expired), two utility permits received, the fire permit asked for, an agreement withdrawn.</summary>
    sealed record Docs(string CuOld, string Cu, string Water, string Gas, string Fire, string Neighbour);

    static Docs Setup(Fixture f)
    {
        var cuOld = Add(f, new JsonObject { ["title"] = "CU 12/2024", ["type"] = "cu", ["status"] = "received", ["number"] = "12", ["date"] = "2024-01-10", ["validUntil"] = "2025-01-10", ["issuer"] = "Primăria Huși" });
        var cu = Add(f, new JsonObject
        {
            ["title"] = "Certificat de urbanism nr. 45", ["type"] = "cu", ["number"] = "45", ["date"] = "2026-03-15", ["validUntil"] = "2099-03-15", ["issuer"] = "Primăria Huși",
            ["files"] = new JsonArray(File("cu.pdf", CuPdf)), ["fields"] = new JsonObject { ["purpose"] = "Construire locuință" },
        });
        var water = Add(f, new JsonObject
        {
            ["title"] = "Aviz Apă-Canal", ["type"] = "aviz", ["folder"] = "permits-utilities", ["number"] = "A-7", ["date"] = "2026-05-02", ["issuer"] = "Apă-Canal",
            ["files"] = new JsonArray(File("aviz.pdf", WaterPdf), File("plan.png", Png)),
        });
        var gas = Add(f, new JsonObject
        {
            ["title"] = "Aviz gaze", ["type"] = "aviz", ["folder"] = "permits-utilities", ["number"] = "G-3", ["date"] = "2026-04-01", ["issuer"] = "Distrigaz",
            ["files"] = new JsonArray(File("aviz.pdf", GasPdf)),
        });
        var fire = Add(f, new JsonObject { ["title"] = "Aviz ISU", ["type"] = "aviz", ["issuer"] = "ISU" });
        var neighbour = Add(f, new JsonObject { ["title"] = "Acord vecin", ["type"] = "acord", ["number"] = "1", ["date"] = "2026-02-01", ["files"] = new JsonArray(File("acord.pdf", Pdf(1, "ACORD"))) });
        f.Run("references.withdraw", new JsonObject { ["id"] = neighbour, ["reason"] = "Signed for another parcel" });
        f.Run("references.update", new JsonObject
        {
            ["id"] = cu,
            ["requires"] = new JsonArray(
                new JsonObject { ["key"] = "water", ["document"] = water }, new JsonObject { ["key"] = "fire", ["document"] = fire },
                new JsonObject { ["key"] = "gas", ["document"] = gas }, "environment"),
        });
        return new Docs(cuOld, cu, water, gas, fire, neighbour);
    }

    static Dictionary<string, string?> Fields(Fixture f, DocFormat format) =>
        DocumentFill.AllFields(f.Session.Reader, format).ToDictionary(x => x.Key, x => x.Value);

    static readonly DocFormat Ro = DocFormat.Ro;
    static readonly DocFormat En = new("en", "metric");

    static string Doc(Fixture f, JsonArray blocks, string lang = "ro") =>
        (string)f.Run("document.create", new JsonObject { ["template"] = new JsonObject { ["id"] = "t", ["lang"] = lang, ["blocks"] = blocks } })!["id"]!;

    static (Lapin.Drawing.Documents.FilledDocument Doc, IReadOnlyList<string> Missing) Fill(Fixture f, string id) => DocumentFill.Fill(f.Session.Reader, f.Model.Get(id)!);

    static List<string> Paragraphs(Lapin.Drawing.Documents.FilledDocument d) =>
        d.Blocks.OfType<Lapin.Drawing.Documents.DocParagraph>().Select(p => DocumentJson.Text(p.Runs)).ToList();

    static List<Lapin.Drawing.Documents.DocTable> Tables(IEnumerable<Lapin.Drawing.Documents.DocBlock> blocks) => blocks.OfType<Lapin.Drawing.Documents.DocTable>().ToList();

    static List<string> Row(Lapin.Drawing.Documents.DocTable t, int i) => t.Rows[i].Select(c => DocumentJson.Text(c)).ToList();

    // ------------------------------------------------------------ fields

    [Fact]
    public void The_ref_fields_read_the_latest_received_valid_document_of_each_type_in_the_documents_date_format()
    {
        var f = F();
        Setup(f);
        var ro = Fields(f, Ro);
        // The certificate: the 2026 one (the 2024 one has expired), its dates as a Romanian document writes them.
        Assert.Equal("45", ro["ref.cu.number"]);
        Assert.Equal("15.03.2026", ro["ref.cu.date"]);
        Assert.Equal("15.03.2099", ro["ref.cu.validUntil"]);
        Assert.Equal("Primăria Huși", ro["ref.cu.issuer"]);
        Assert.Equal("Certificat de urbanism nr. 45", ro["ref.cu.title"]);
        Assert.Equal("1", ro["ref.cu.count"]);
        // Two permits received (the latest by its own date wins); the one asked for is not counted.
        Assert.Equal("A-7", ro["ref.aviz.number"]);
        Assert.Equal("2", ro["ref.aviz.count"]);
        // The withdrawn agreement is never read; a count of none is 0, never to complete.
        Assert.Null(ro["ref.acord.number"]);
        Assert.Equal("0", ro["ref.acord.count"]);
        // Every type of the project's list has its fields, even with no document yet.
        Assert.True(ro.ContainsKey("ref.studiu-geo.number"));
        Assert.Null(ro["ref.studiu-geo.number"]);
        // An English document writes the days as ISO.
        Assert.Equal("2026-03-15", Fields(f, En)["ref.cu.date"]);

        // A newer certificate supersedes it: the fields follow the new one; withdrawn, none is left.
        var cu2 = Add(f, new JsonObject { ["title"] = "CU 60", ["type"] = "cu", ["status"] = "received", ["number"] = "60", ["date"] = "2026-09-01" });
        Assert.Equal("60", Fields(f, Ro)["ref.cu.number"]);
        Assert.Equal("2", Fields(f, Ro)["ref.cu.count"]);
        f.Run("references.withdraw", new JsonObject { ["id"] = cu2, ["reason"] = "Issued by mistake" });
        Assert.Equal("45", Fields(f, Ro)["ref.cu.number"]);

        // document.fields lists them for Insert → Field, with their labels and values.
        var listed = (JsonArray)f.Session.Query("document.fields", new JsonObject { ["lang"] = "ro" })!["fields"]!;
        var number = listed.OfType<JsonObject>().Single(x => (string?)x["key"] == "ref.cu.number");
        Assert.Equal("Urbanism certificate: number", (string?)number["label"]);
        Assert.Equal("45", (string?)number["value"]);
    }

    // ------------------------------------------------------------ collections

    [Fact]
    public void The_references_and_checklist_collections_repeat_with_where_like_the_others()
    {
        var f = F();
        var d = Setup(f);
        var items = DocumentFill.Items(f.Session.Reader, "references", Ro);
        // Every document, in the folder tree's order then by date (undated last): the certificates, the utilities' permits, the others.
        Assert.Equal([d.CuOld, d.Cu, d.Gas, d.Water, d.Neighbour, d.Fire], items.Select(i => i["id"]));
        var water = items.Single(i => i["id"] == d.Water);
        Assert.Equal(("aviz", "Aviz", "received", "primit"), (water["type"], water["typeName"], water["status"], water["statusText"]));
        Assert.Equal(("permits-utilities", "permits"), (water["folder"], water["topFolder"]));
        Assert.Equal("02 Avize și acorduri / Utilități", water["folderPath"]);
        // Its pages: the PDF's one and the picture's one.
        Assert.Equal(("2", "2", "aviz.pdf, plan.png"), (water["pages"], water["files"], water["fileNames"]));
        Assert.Equal("2", items.Single(i => i["id"] == d.Cu)["pages"]);
        Assert.Equal("", items.Single(i => i["id"] == d.Fire)["pages"]);
        Assert.Equal(("expired", "expirat"), (items[0]["status"], items[0]["statusText"]));
        // The type's extra fields by their keys, the empty ones too.
        Assert.Equal(("Construire locuință", ""), (items[1]["purpose"], items[1]["parcel"]));

        var doc = Doc(f, new JsonArray(
            new JsonObject
            {
                ["kind"] = "repeat", ["over"] = "references", ["where"] = new JsonObject { ["type"] = "aviz", ["status"] = "received" },
                ["blocks"] = new JsonArray(new JsonObject { ["kind"] = "paragraph", ["text"] = "{{reference.index}}. {{reference.title}} nr. {{reference.number}} din {{reference.date}} ({{reference.pages}} p.)" }),
            },
            new JsonObject
            {
                ["kind"] = "repeat", ["over"] = "references", ["where"] = new JsonObject { ["topFolder"] = "input" },
                ["blocks"] = new JsonArray(new JsonObject { ["kind"] = "paragraph", ["text"] = "{{reference.title}}" }),
            },
            new JsonObject
            {
                ["kind"] = "repeat", ["over"] = "checklist",
                ["blocks"] = new JsonArray(new JsonObject { ["kind"] = "paragraph", ["text"] = "{{check.label}}: {{check.stateText}}" }),
            },
            new JsonObject
            {
                ["kind"] = "repeat", ["over"] = "checklist", ["where"] = new JsonObject { ["state"] = "received" },
                ["blocks"] = new JsonArray(new JsonObject { ["kind"] = "paragraph", ["text"] = "{{check.label}} · {{check.issuer}} · {{check.number}} · {{check.date}}" }),
            }));
        var (filled, missing) = Fill(f, doc);
        Assert.Equal([
            "1. Aviz gaze nr. G-3 din 01.04.2026 (1 p.)", "2. Aviz Apă-Canal nr. A-7 din 02.05.2026 (2 p.)",
            "CU 12/2024", "Certificat de urbanism nr. 45",
            "Alimentare cu apă: primit", "Securitate la incendiu: solicitat", "Alimentare cu gaze naturale: primit", "Actul administrativ al autorității pentru protecția mediului: lipsă",
            "Alimentare cu apă · Apă-Canal · A-7 · 02.05.2026", "Alimentare cu gaze naturale · Distrigaz · G-3 · 01.04.2026",
        ], Paragraphs(filled));
        Assert.Empty(missing);

        // The checklist is the valid certificate's: without one, it has no items.
        f.Run("references.withdraw", new JsonObject { ["id"] = d.Cu, ["reason"] = "Annulled" });
        Assert.Empty(DocumentFill.Items(f.Session.Reader, "checklist", Ro));
        var collections = (JsonArray)f.Session.Query("document.fields", new JsonObject { ["lang"] = "ro" })!["collections"]!;
        Assert.Contains(collections.OfType<JsonObject>(), c => (string?)c["key"] == "references" && (string?)c["itemPrefix"] == "reference");
        Assert.Contains(collections.OfType<JsonObject>(), c => (string?)c["key"] == "checklist" && (string?)c["itemPrefix"] == "check");
    }

    // ------------------------------------------------------------ tables

    [Fact]
    public void The_permits_table_and_the_reference_documents_table_with_its_filters()
    {
        var f = F();
        var d = Setup(f);
        var q = f.Session.Reader;
        var permits = ReferenceTables.Table(q, ReferenceTables.Permits, Ro)!;
        Assert.Equal(["Nr.", "Aviz / acord", "Emitent", "Număr", "Data", "Valabil până la", "Stare"], permits.Columns);
        Assert.Equal("Avize și acorduri cerute prin certificatul de urbanism nr. 45", permits.Title);
        Assert.Equal(4, permits.Rows.Count);
        Assert.Equal(["1", "Alimentare cu apă", "Apă-Canal", "A-7", "02.05.2026", "", "primit"], permits.Rows[0]);
        Assert.Equal(["2", "Securitate la incendiu", "ISU", "", "", "", "solicitat"], permits.Rows[1]);
        Assert.Equal("lipsă", permits.Rows[3][6]);
        var en = ReferenceTables.Table(q, ReferenceTables.Permits, En)!;
        Assert.Equal(["No.", "Permit or agreement", "Issuer", "Number", "Date", "Valid until", "State"], en.Columns);
        Assert.Equal(["1", "Water supply", "Apă-Canal", "A-7", "2026-05-02", "", "received"], en.Rows[0]);

        string[] Titles(string source) => [.. ReferenceTables.Table(q, source, Ro)!.Rows.Select(r => r[2])];
        // Without a status: the live ones (the withdrawn agreement left out, the expired certificate and the permit asked for kept).
        Assert.Equal(["CU 12/2024", "Certificat de urbanism nr. 45", "Aviz gaze", "Aviz Apă-Canal", "Aviz ISU"], Titles("references:documents"));
        Assert.Equal(["Certificat de urbanism nr. 45", "Aviz gaze", "Aviz Apă-Canal"], Titles("references:documents?status=received"));
        Assert.Equal(["Aviz gaze", "Aviz Apă-Canal", "Aviz ISU"], Titles("references:documents?type=aviz"));
        // A folder with its subfolders; every status with all; two types at once.
        Assert.Equal(["Aviz gaze", "Aviz Apă-Canal", "Acord vecin", "Aviz ISU"], Titles("references:documents?folder=permits&status=all"));
        Assert.Equal(["Aviz gaze", "Aviz Apă-Canal"], Titles("references:documents?folder=permits-utilities"));
        Assert.Equal(["CU 12/2024", "Certificat de urbanism nr. 45", "Acord vecin"], Titles("references:documents?type=cu,acord&status=all"));
        var list = ReferenceTables.Table(q, "references:documents?status=received", Ro)!;
        Assert.Equal(["Nr.", "Tip", "Titlu", "Emitent", "Număr", "Data", "Valabil până la", "Stare", "Pagini"], list.Columns);
        Assert.Equal(["1", "Certificat de urbanism", "Certificat de urbanism nr. 45", "Primăria Huși", "45", "15.03.2026", "15.03.2099", "primit", "2"], list.Rows[0]);

        // The editor offers both, beside the schedules; a document shows them with the columns it keeps, its numbers as they are.
        var tables = (JsonArray)f.Session.Query("document.fields", new JsonObject { ["lang"] = "ro" })!["tables"]!;
        Assert.Contains(tables.OfType<JsonObject>(), t => (string?)t["source"] == ReferenceTables.Permits && (string?)t["label"] == "Permits and agreements");
        Assert.Contains(tables.OfType<JsonObject>(), t => (string?)t["source"] == ReferenceTables.List && (string?)t["label"] == "Reference documents");
        var doc = Doc(f, new JsonArray(
            new JsonObject { ["kind"] = "table", ["source"] = ReferenceTables.Permits },
            new JsonObject { ["kind"] = "table", ["source"] = "references:documents?type=aviz&status=received", ["columns"] = new JsonArray("Nr.", "Titlu", "Număr", "Pagini") }));
        var (filled, missing) = Fill(f, doc);
        Assert.Empty(missing);
        var shown = Tables(filled.Blocks);
        Assert.Equal(2, shown.Count);
        Assert.Equal(["Nr.", "Titlu", "Număr", "Pagini"], shown[1].Header);
        Assert.Equal(["1", "Aviz gaze", "G-3", "1"], Row(shown[1], 0));

        // Without a valid certificate, the permits table is to complete.
        f.Run("references.withdraw", new JsonObject { ["id"] = d.Cu, ["reason"] = "Annulled" });
        Assert.Null(ReferenceTables.Table(q, ReferenceTables.Permits, Ro));
        Assert.Contains("table:references:permits", Fill(f, doc).Missing);
        Assert.Null(ReferenceTables.Table(q, "references:nothing", Ro));
    }

    // ------------------------------------------------------------ the templates

    static readonly string[] Starters = ["borderou-avize", "opis-dosar-autorizare", "foaie-de-capat", "permits-agreements-en", "permit-application-index-en", "cover-page-en"];

    static void Info(Fixture f) => f.Run("project.setInfo", new JsonObject
    {
        ["number"] = "12/2026", ["phase"] = "DTAC", ["client"] = "Ionescu Maria", ["address"] = "Str. Florilor 12, Huși", ["cadastral"] = "70123",
        ["designer"] = "Ciuntuc BIM Studio SRL", ["chiefArchitect"] = "arh. Ion Popescu", ["author"] = "IC", ["checkedBy"] = "AB",
    });

    [Fact]
    public void The_starter_templates_are_indicative_in_both_languages_and_fill_from_the_reference_documents()
    {
        var f = F();
        var templates = ((JsonArray)f.Session.Query("document.templates", [])!).OfType<JsonObject>().ToDictionary(t => (string)t["id"]!);
        foreach (var id in Starters)
        {
            Assert.True(templates.ContainsKey(id), id);
            Assert.True((bool?)templates[id]["indicative"], id);
            Assert.Equal(id.EndsWith("-en", StringComparison.Ordinal) ? "en" : "ro", (string?)templates[id]["lang"]);
        }
        // The official application form is not one of them (plan §6): its wording is the authority's.
        Assert.DoesNotContain(templates.Keys, k => k.Contains("cerere", StringComparison.Ordinal) || k.Contains("form", StringComparison.Ordinal));
        Assert.Null(templates["memoriu-arhitectura"]["indicative"]);

        // A project without a certificate: what the templates show of it is to complete, or said in a note.
        var empty = F();
        Info(empty);
        foreach (var id in Starters)
        {
            var doc = (string)empty.Run("document.create", new JsonObject { ["template"] = id })!["id"]!;
            var (_, missing) = Fill(empty, doc);
            Assert.DoesNotContain(missing, k => k.StartsWith("collection:", StringComparison.Ordinal));
            // Each template tests the certificate before reading it: nothing of it is left to complete.
            Assert.DoesNotContain(missing, k => k.StartsWith("ref.", StringComparison.Ordinal) || k.StartsWith("table:", StringComparison.Ordinal));
        }
        var memo = (string)empty.Run("document.create", new JsonObject { ["template"] = "memoriu-arhitectura" })!["id"]!;
        var memoMissing = Fill(empty, memo).Missing;
        Assert.Contains("ref.cu.number", memoMissing);
        Assert.Contains("ref.cu.date", memoMissing);
        Assert.Contains("ref.cu.issuer", memoMissing);

        // With the reference documents: filled, nothing of theirs to complete.
        Setup(f);
        Info(f);
        f.Run("sheet.create", new JsonObject { ["name"] = "Plan parter", ["number"] = "A-101", ["paper"] = "A3" });
        foreach (var id in Starters.Append("memoriu-arhitectura"))
        {
            var doc = (string)f.Run("document.create", new JsonObject { ["template"] = id })!["id"]!;
            var (d, missing) = Fill(f, doc);
            Assert.DoesNotContain(missing, k => k.StartsWith("ref.", StringComparison.Ordinal) || k.StartsWith("table:", StringComparison.Ordinal) || k.StartsWith("collection:", StringComparison.Ordinal));
            var text = DocumentJson.Text(d.Blocks.SelectMany(Runs));
            switch (id)
            {
                case "borderou-avize":
                    Assert.Contains("Alimentare cu apă", text);
                    Assert.Contains("15.03.2026", text);
                    Assert.Equal(2, Tables(d.Blocks).Count(t => t.Header.Count > 0));
                    break;
                case "opis-dosar-autorizare":
                    var refs = Tables(d.Blocks).Single(t => t.Header.Count > 0);
                    Assert.Equal(["Nr.", "Tip", "Titlu", "Emitent", "Număr", "Data", "Pagini"], refs.Header);
                    Assert.Equal(["1", "Certificat de urbanism", "Certificat de urbanism nr. 45", "Primăria Huși", "45", "15.03.2026", "2"], Row(refs, 0));
                    Assert.Contains("A-101", text);
                    break;
                case "foaie-de-capat":
                    Assert.Contains("nr. 45 din 15.03.2026, emis de Primăria Huși", text);
                    break;
                case "cover-page-en":
                    Assert.Contains("no. 45 of 2026-03-15, issued by Primăria Huși", text);
                    break;
                case "permits-agreements-en":
                    Assert.Contains("Water supply", text);
                    break;
                case "memoriu-arhitectura":
                    Assert.Contains("nr. 45 din 15.03.2026", text);
                    Assert.Contains("Primăria Huși", text);
                    break;
            }
        }
    }

    static IEnumerable<Lapin.Drawing.Documents.DocRun> Runs(Lapin.Drawing.Documents.DocBlock b) => b switch
    {
        Lapin.Drawing.Documents.DocParagraph p => p.Runs,
        Lapin.Drawing.Documents.DocHeading h => h.Runs,
        Lapin.Drawing.Documents.DocTable t => t.Rows.SelectMany(r => r.SelectMany(c => c.Append(new Lapin.Drawing.Documents.DocRun(" ")))),
        Lapin.Drawing.Documents.DocList l => l.Items.SelectMany(i => i),
        _ => [],
    };

    [Fact]
    public void A_template_with_the_reference_tables_writes_as_DOCX_ODT_and_PDF()
    {
        var f = F();
        Setup(f);
        Info(f);
        foreach (var id in new[] { "borderou-avize", "permit-application-index-en" })
        {
            var doc = (string)f.Run("document.create", new JsonObject { ["template"] = id })!["id"]!;
            byte[] Export(string format) => Convert.FromBase64String((string)((JsonObject)f.Session.Query("document.export", new JsonObject { ["id"] = doc, ["format"] = format })!)["data"]!);
            string Entry(byte[] zip, string name)
            {
                using var z = new ZipArchive(new MemoryStream(zip));
                using var r = new StreamReader(z.GetEntry(name)!.Open());
                return r.ReadToEnd();
            }
            var docx = Entry(Export("docx"), "word/document.xml");
            var odt = Entry(Export("odt"), "content.xml");
            var pdf = Export("pdf");
            Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
            Assert.True(pdf.Length > 1000);
            // The tables' rows are in the files: the certificate, the permits' numbers.
            foreach (var xml in new[] { docx, odt })
            {
                Assert.Contains("Primăria Huși", xml);
                if (id == "borderou-avize") { Assert.Contains("A-7", xml); Assert.Contains("Alimentare cu apă", xml); }
                else Assert.Contains("Certificat de urbanism nr. 45", xml);
            }
        }
    }

    // ------------------------------------------------------------ the issue

    [Fact]
    public void An_issue_sends_reference_documents_as_copies_listed_and_hashed_in_the_transmittal_and_the_manifest()
    {
        var f = F();
        var d = Setup(f);
        Info(f);
        var borderou = (string)f.Run("document.create", new JsonObject { ["template"] = "borderou-avize" })!["id"]!;
        // A geotechnical study above the project's keep limit: its hash and size kept, its bytes not.
        f.Run("references.setKeepLimit", new JsonObject { ["bytes"] = 2000 });
        var studyBytes = Pdf(30, new string('x', 120));
        Assert.True(studyBytes.Length > 2000);
        var study = Add(f, new JsonObject { ["title"] = "Studiu geotehnic", ["type"] = "studiu-geo", ["number"] = "G-12", ["files"] = new JsonArray(File("studiu.pdf", studyBytes)) });
        Assert.False(Lapin.Platform.Cde.References.References.Files(f.Model.Get(study)!).Single().Held);

        var store = PackageIssues.Empty();
        var (r, _) = CdeIssue.Run(f.Session, store, new JsonObject
        {
            ["documents"] = new JsonArray(borderou), ["references"] = new JsonArray(d.Cu, study), ["referenceFolders"] = new JsonArray("permits"),
            ["revision"] = "P01", ["status"] = "S2", ["toFolder"] = false,
        });
        var files = ((JsonArray)r["files"]!).OfType<JsonObject>().ToList();
        var refs = files.Where(x => (string?)x["kind"] == "reference").ToList();
        // The certificate, then the utilities' permits (two named aviz.pdf: the second numbered), the picture; the permit asked for has
        // no file and the withdrawn agreement does not go through a folder; the study is not held here, so it is left out and said.
        Assert.Equal(["cu.pdf", "aviz.pdf", "aviz (2).pdf", "plan.png"], refs.Select(x => (string)x["name"]!));
        Assert.Equal([d.Cu, d.Gas, d.Water, d.Water], refs.Select(x => (string)x["referenceId"]!));
        Assert.Equal([CuPdf, GasPdf, WaterPdf, Png], refs.Select(x => Hashes.Sha256Hex(store.Get((string)x["sha256"]!)) == (string)x["sha256"]! ? store.Get((string)x["sha256"]!) : null));
        Assert.All(refs, x => Assert.True((bool?)x["stored"]));
        var skipped = ((JsonArray)r["skipped"]!).Select(x => (string)x!).ToList();
        Assert.Single(skipped);
        Assert.Contains("studiu.pdf", skipped[0]);

        // The manifest: each copy with its SHA-256, the document's number and title; the transmittal's CSV has their rows.
        var manifest = JsonNode.Parse(store.Get((string)files.Single(x => (string?)x["kind"] == "json")["sha256"]!))!;
        var cuEntry = manifest["files"]!.AsArray().OfType<JsonObject>().Single(x => (string?)x["name"] == "cu.pdf");
        Assert.Equal(Hashes.Sha256Hex(CuPdf), (string?)cuEntry["sha256"]);
        Assert.Equal(("reference", "45", "Certificat de urbanism nr. 45", d.Cu), ((string?)cuEntry["kind"], (string?)cuEntry["documentNumber"], (string?)cuEntry["documentTitle"], (string?)cuEntry["referenceId"]));
        Assert.Null(cuEntry["sheetNumber"]);
        var csv = Encoding.UTF8.GetString(store.Get((string)files.Single(x => (string?)x["kind"] == "csv")["sha256"]!));
        Assert.Contains($"cu.pdf,REFERENCE,45,Certificat de urbanism nr. 45,P01,S2,{Hashes.Sha256Hex(CuPdf)},{CuPdf.Length}", csv);
        Assert.Contains($"aviz (2).pdf,REFERENCE,A-7,Aviz Apă-Canal,P01,S2,{Hashes.Sha256Hex(WaterPdf)},", csv);
        Assert.Contains(files, x => (string?)x["kind"] == "document" && (string?)x["documentId"] == borderou);
        // The issue's record keeps them as its files, by their document (a revision series is still its own deliverables').
        var record = CdeIssues.All(f.Model).Single();
        Assert.Equal(4, record.Files.Count(x => x.ReferenceId != null));
        Assert.DoesNotContain(record.DocumentKeys, k => k.StartsWith("reference:", StringComparison.Ordinal));

        // The interface fetched the study's bytes from the server (remote.referenceFile): they go, checked against the hash, not kept again.
        var (r2, _) = CdeIssue.Run(f.Session, store, new JsonObject
        {
            ["references"] = new JsonArray(study), ["referenceData"] = new JsonObject { [Hashes.Sha256Hex(studyBytes)] = Convert.ToBase64String(studyBytes) },
            ["revision"] = "P01", ["status"] = "S2", ["toFolder"] = false,
        });
        var sent = ((JsonArray)r2["files"]!).OfType<JsonObject>().Single(x => (string?)x["kind"] == "reference");
        Assert.Equal(("studiu.pdf", Hashes.Sha256Hex(studyBytes), (long)studyBytes.Length), ((string)sent["name"]!, (string)sent["sha256"]!, (long)sent["size"]!));
        Assert.Null(sent["stored"]);
        Assert.Null(r2["skipped"]);
        // Bytes that are not the file's are refused: the file is left out again.
        var (r3, _) = CdeIssue.Run(f.Session, store, new JsonObject
        {
            ["references"] = new JsonArray(study), ["referenceData"] = new JsonObject { [Hashes.Sha256Hex(studyBytes)] = Convert.ToBase64String(CuPdf) },
            ["revision"] = "P01", ["status"] = "S2", ["toFolder"] = false,
        });
        Assert.Single((JsonArray)r3["skipped"]!);

        // Nothing named: refused; an unknown document or folder: refused.
        Assert.Throws<CommandException>(() => CdeIssue.Run(f.Session, store, new JsonObject { ["revision"] = "P01", ["status"] = "S2", ["toFolder"] = false }));
        Assert.Throws<CommandException>(() => CdeIssue.Run(f.Session, store, new JsonObject { ["references"] = new JsonArray("nope"), ["revision"] = "P01", ["status"] = "S2", ["toFolder"] = false }));
        Assert.Throws<CommandException>(() => CdeIssue.Run(f.Session, store, new JsonObject { ["referenceFolders"] = new JsonArray("nope"), ["revision"] = "P01", ["status"] = "S2", ["toFolder"] = false }));
    }

    [Fact]
    public void The_names_preview_lists_the_reference_files_as_the_issue_names_them()
    {
        var f = F();
        var d = Setup(f);
        // The same choice as the issue above, without the study: the certificate, then the utilities' folder in the tree's order.
        var names = f.Session.Query("cde.naming", new JsonObject
        {
            ["references"] = new JsonArray(d.Cu), ["referenceFolders"] = new JsonArray("permits"), ["revision"] = "P01", ["status"] = "S2",
        })!;
        var refs = ((JsonArray)names["references"]!).OfType<JsonObject>().ToList();
        Assert.Equal([d.Cu, d.Gas, d.Water], refs.Select(x => (string)x["id"]!));
        Assert.Equal(["cu.pdf", "aviz.pdf", "aviz (2).pdf", "plan.png"], refs.SelectMany(x => ((JsonArray)x["files"]!).Select(y => (string)y!["file"]!)));
        Assert.Equal(["cu.pdf", "aviz.pdf", "aviz.pdf", "plan.png"], refs.SelectMany(x => ((JsonArray)x["files"]!).Select(y => (string)y!["name"]!)));
        Assert.Equal("45", (string?)refs[0]["number"]);

        // The names the issue writes are the same ones.
        var (r, _) = CdeIssue.Run(f.Session, PackageIssues.Empty(), new JsonObject
        {
            ["references"] = new JsonArray(d.Cu), ["referenceFolders"] = new JsonArray("permits"), ["revision"] = "P01", ["status"] = "S2", ["toFolder"] = false,
        });
        Assert.Equal(["cu.pdf", "aviz.pdf", "aviz (2).pdf", "plan.png"], ((JsonArray)r["files"]!).OfType<JsonObject>().Where(x => (string?)x["kind"] == "reference").Select(x => (string)x["name"]!));

        // Without reference documents asked, the names have none.
        Assert.Null(f.Session.Query("cde.naming", new JsonObject { ["revision"] = "P01" })!["references"]);
    }

    [Fact]
    public void The_transmittal_pdf_lists_the_reference_documents()
    {
        var header = new CdeTransmittal.Header("Casa", "Issue 1", 1, "26.11.2026", "P01", "S2", "Shared", null, "CBS");
        var rows = new List<CdeTransmittal.Row> { new(new IssuedFile("cu.pdf", "reference", null, new string('a', 64), 1234, ReferenceId: "r1"), "45", "Certificat de urbanism") };
        var pdf = CdeTransmittal.Pdf(header, rows);
        var text = string.Join("\n", Lapin.Platform.Cde.PdfRemarks.PdfPageText.Read(pdf, null).Pages.Select(p => p.Text));
        Assert.Contains("cu.pdf", text);
        Assert.Contains("REF", text);
        Assert.Contains("a reference document the project received", text);
    }
}
