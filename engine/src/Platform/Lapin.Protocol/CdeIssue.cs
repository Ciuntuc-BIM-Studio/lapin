using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Platform.Cde;
using Lapin.Platform.Documents;
using Refs = Lapin.Platform.Cde.References.References;

namespace Lapin.Protocol;

/// <summary>
/// An issue (docs/plan-cde.md): the host's part, because it makes files and
/// exports. The checks run first and a blocking finding refuses the issue;
/// then each sheet's PDF (and the set, the IFC model, the DXFs when asked) is
/// made, named by the convention with the status and revision, and hashed;
/// the transmittal (PDF and CSV) and the manifest are made beside them. The
/// reference documents asked for (stage R5: <see cref="ReferencesOf"/>) go
/// with it as copies of their files under their own names, listed and hashed
/// like the rest (<see cref="IssuedFile.ReferenceId"/>). Every
/// file is kept in the project's file (<see cref="PackageIssues"/>, the IFC
/// only on request) and, when the project has a folder on this computer, also
/// written into its container. The issue is recorded in the project (one undo
/// step) with the sheets' drawing hashes and the number of the version the
/// dispatcher saves right after, in the same call.
/// </summary>
public static class CdeIssue
{
    /// <param name="writeDocument">How a written document becomes a file (its PDF, or DOCX): the writers of Lapin.Drawing by default; a test's own.</param>
    public static (JsonObject Result, Diff Diff) Run(Session session, PackageIssues store, JsonObject p, int? version = null, DocumentExport.Writer? writeDocument = null)
    {
        var m = session.Model;
        var q = session.Reader;
        // The folder is optional: the project's file holds the issue; a folder is where it is also exchanged. A browser has none (its file system is the page's own).
        var toFolder = !OperatingSystem.IsBrowser() && (p["toFolder"] is not JsonValue tv || !tv.TryGetValue<bool>(out var t) || t);
        var folder = !toFolder ? null : (string?)p["folder"] is { Length: > 0 } given ? HostFiles.Check(given) : Cde.Folder(m);
        var sheets = Cde.SheetsOf(m, p["sheets"]);
        var model = Flag(p, "model");
        // The written documents only when asked (docs/plan-documents.md): each as a PDF, and as a DOCX too with documentsDocx.
        var documents = p["documents"] is JsonArray ? Cde.DocumentsOf(m, p["documents"]) : [];
        var references = ReferencesOf(m, p["references"], p["referenceFolders"]);
        if (sheets.Count == 0 && !model && documents.Count == 0 && references.Count == 0) throw new CommandException("Choose at least one sheet, a document, a reference document, or the model.");
        var names = Cde.Names(m, p);
        var revision = (string)names["revision"]!;
        var status = (string)names["status"]!;
        if (!CdeRevisions.IsRevision(revision)) throw new CommandException("A revision is P01, P02… (preliminary) or C01, C02… (contractual).");
        if (!CdeRevisions.IsStatus(status)) throw new CommandException("A status code is S0–S7, A1–A9, B1–B9 or CR.");
        var container = (string?)p["container"] is { Length: > 0 } c ? c : CdeRevisions.ContainerFor(status);
        if (!CdeRevisions.Containers.Contains(container)) throw new CommandException($"The container is one of {string.Join(", ", CdeRevisions.Containers)}.");
        var number = (int)names["number"]!;
        // A new revision of an earlier issue (its number kept; the revision after its last), checked before any file is made.
        var of = (string?)names["of"];
        if (of != null) CdeIssues.CheckRevision(m, of, revision);
        var name = (string?)p["name"] is { Length: > 0 } nm ? nm.Trim() : of != null ? CdeIssues.SeriesOf(m, of)!.Name : $"Issue {number}";
        var note = ((string?)p["note"])?.Trim();
        // The IFC is kept in the project's file only on request: a large model is tens of MB an issue. Its name and hash are recorded either way.
        var keepModel = Flag(p, "keepModel");

        // The checks: a blocking finding refuses the issue; warnings are the interface's to override (it ran them already).
        var findings = CdeChecks.Run(q, sheets, revision);
        if (findings.Any(f => f.Blocking))
            throw new CommandException($"The checks found what must be fixed first: {string.Join(" ", findings.Where(f => f.Blocking).Take(3).Select(f => f.Message))}{(findings.Count(f => f.Blocking) > 3 ? " …" : "")}");

        // The folder's container, made when the folder is reachable; a folder that is not on this computer leaves the issue in the project's file alone.
        string? into = null;
        string? folderProblem = null;
        if (folder != null)
        {
            try
            {
                Directory.CreateDirectory(folder);
                foreach (var cont in CdeRevisions.Containers) Directory.CreateDirectory(Path.Combine(folder, cont));
                into = Path.Combine(folder, container);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                folderProblem = $"The project folder could not be written ({e.Message}): the issue is kept in the project file; publish it to the folder later.";
            }
        }

        var files = new List<IssuedFile>();
        var rows = new List<CdeTransmittal.Row>();
        // A file kept in the project (by its hash, once), written into the container when there is one; `Put` also lists it in the transmittal
        // (the transmittal's own files are not).
        IssuedFile Write(string fileName, byte[] bytes, string kind, Entity? sheet = null, string? hash = null, string? documentId = null, string? referenceId = null, bool? keep = null)
        {
            var stored = keep ?? (kind != "ifc" || keepModel);
            var sha = stored ? store.Put(bytes) : CdeTransmittal.Sha256(bytes);
            if (into != null) File.WriteAllBytes(Path.Combine(into, fileName), bytes);
            var f = new IssuedFile(fileName, kind, sheet?.Id, sha, bytes.LongLength, hash, stored, documentId, referenceId);
            files.Add(f);
            return f;
        }
        void Put(string fileName, byte[] bytes, string kind, Entity? sheet, string? hash = null) =>
            rows.Add(new CdeTransmittal.Row(Write(fileName, bytes, kind, sheet, hash), sheet?.Str("number"), sheet?.Name));

        var dxf = Flag(p, "dxf");
        // Each sheet printed as this issue makes it: its revision and status in the title block, its row in the revision table.
        var issuing = new Sheets.Issuing(revision, status, name, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        var byId = (names["sheets"] as JsonArray ?? []).OfType<JsonObject>().ToDictionary(o => (string)o["id"]!, o => (string)o["name"]!);
        foreach (var sheet in sheets)
        {
            var stem = byId[sheet.Id];
            var hash = Sheets.DrawingHash(q, sheet);
            Put(CdeNaming.WithRevision(stem, status, revision, "pdf"), Sheets.Pdf(q, [sheet], issuing), "pdf", sheet, hash);
            if (dxf) Put(CdeNaming.WithRevision(stem, status, revision, "dxf"), Sheets.Dxf(q, sheet, issuing), "dxf", sheet, hash);
        }
        if (sheets.Count > 1 && Flag(p, "set"))
            Put((string)names["set"]!, Sheets.Pdf(q, sheets, issuing), "pdf", null);
        // Each document filled as the issue sends it (its revision and status the issue's), written, named by its template's type.
        var writer = writeDocument ?? DocumentExport.Write;
        var naming0 = CdeHooks.Naming?.Invoke(CdeNaming.Of(m)) ?? CdeNaming.Of(m);
        var docx = Flag(p, "documentsDocx");
        // A DOCX's pictures as the interface made them, by document: {documentId: {key: {svg?, png?}}} (the engine has no rasteriser).
        var pictures = p["documentImages"] as JsonObject;
        foreach (var doc in documents)
        {
            var issued = doc.CloneAttrs();
            issued["revision"] = revision;
            issued["status"] = status;
            var (filled, _) = DocumentFill.Fill(q, doc.With(attrs: issued));
            var stem = Cde.DocumentName(m, naming0, doc);
            rows.Add(new CdeTransmittal.Row(Write(CdeNaming.WithRevision(stem, status, revision, "pdf"), writer(filled, "pdf", null), "document", documentId: doc.Id), null, doc.Name));
            if (docx) rows.Add(new CdeTransmittal.Row(Write(CdeNaming.WithRevision(stem, status, revision, "docx"), writer(filled, "docx", DocumentExport.Pictures(pictures?[doc.Id])), "document", documentId: doc.Id), null, doc.Name));
        }
        // The reference documents asked for: each file a copy under its own name (a number after it when two share one), its bytes from
        // the project's file, the project folder's copy or where it was added from, else as the interface fetched them from the server
        // (referenceData: {sha256: base64}, checked against the hash). A file found nowhere is left out and said, never the issue refused.
        // A file above the project's keep limit goes into the folder and is listed with its hash, but is not kept again in the project's file.
        var skipped = new List<string>();
        var fetched = p["referenceData"] as JsonObject;
        var keepLimit = Refs.KeepLimit(m);
        var used = new HashSet<string>(files.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var e in references)
            foreach (var rf in Refs.Files(e))
            {
                byte[]? bytes = null;
                try { bytes = Refs.Bytes(q, e, rf.Name).Bytes; }
                catch (CommandException) { }
                if (bytes == null && (string?)fetched?[rf.Sha256] is { } b64)
                {
                    try { bytes = Convert.FromBase64String(b64); }
                    catch (FormatException) { bytes = null; }
                    if (bytes != null && CdeTransmittal.Sha256(bytes) != rf.Sha256) bytes = null;
                }
                if (bytes == null)
                {
                    skipped.Add($"{e.Name}: {rf.Name} is not held here (larger than the project keeps, and not in the project folder): it was left out of the issue.");
                    continue;
                }
                var fileName = rf.Name;
                for (var i = 2; !used.Add(fileName); i++) fileName = $"{Path.GetFileNameWithoutExtension(rf.Name)} ({i}){Path.GetExtension(rf.Name)}";
                rows.Add(new CdeTransmittal.Row(Write(fileName, bytes, "reference", referenceId: e.Id, keep: bytes.LongLength <= keepLimit), e.Str("number"), e.Name));
            }
        if (model)
        {
            using var ms = new MemoryStream();
            // An issue for a BIM check: LoadBearing on every element that may bear load, from the rule where nothing says (docs/plan-structural-usage.md).
            var options = new IfcExportOptions((string?)p["schema"] ?? "IFC4", (string?)p["detail"] ?? "group", (string?)p["phase"], DerivedLoadBearing: p["derivedLoadBearing"] is not JsonValue dv || !dv.TryGetValue<bool>(out var d) || d);
            IfcExport.Write(session, ms, (string)names["model"]!, options);
            Put((string)names["model"]!, ms.ToArray(), "ifc", null);
        }

        // The transmittal and the manifest, named as an information exchange of this issue's number.
        var naming = CdeHooks.Naming?.Invoke(CdeNaming.Of(m)) ?? CdeNaming.Of(m);
        var header = new CdeTransmittal.Header(Sheets.ProjectName(q), name, number, Lapin.Drawing.DrawingDates.Format(DraftingResources.Book(m), DateTime.Now), revision, status, container, note, naming.Originator);
        var exchange = naming.ForTransmittal(number);
        var transmittalPdf = CdeNaming.WithRevision(exchange, status, revision, "pdf");
        Write(transmittalPdf, CdeTransmittal.Pdf(header, rows), "pdf");
        Write(CdeNaming.WithRevision(exchange, status, revision, "csv"), Encoding.UTF8.GetBytes(CdeTransmittal.Csv(header, rows)), "csv");
        Write(CdeNaming.WithRevision(exchange, status, revision, "json"), new UTF8Encoding(false).GetBytes(CdeTransmittal.Manifest(header, rows, transmittalPdf)), "json");

        var args = new JsonObject
        {
            ["name"] = name, ["revision"] = revision, ["status"] = status, ["container"] = container, ["folder"] = into != null ? folder : null, ["note"] = note,
            ["files"] = new JsonArray(files.Select(f => (JsonNode)f.ToJson()).ToArray()),
        };
        if (version is { } v) args["version"] = v;
        if (of != null) args["of"] = of;
        var diff = session.Execute("cde.recordIssue", args, out var recorded);
        var result = recorded as JsonObject ?? new JsonObject();
        result["path"] = into;
        if (folderProblem != null) result["folderProblem"] = folderProblem;
        result["warnings"] = new JsonArray(findings.Where(f => !f.Blocking).Select(f => (JsonNode)f.ToJson()).ToArray());
        if (skipped.Count > 0) result["skipped"] = new JsonArray([.. skipped.Select(x => (JsonNode)x)]);
        return (result, diff);
    }

    /// <summary>
    /// The reference documents an issue sends (stage R5): those named (<c>references</c>: ids) and those in the folders named
    /// (<c>referenceFolders</c>: ids, each with its subfolders), once each, in the folder tree's order; superseded and withdrawn ones
    /// only when named, never through a folder, and requested ones (no file yet) never.
    /// </summary>
    public static List<Entity> ReferencesOf(IModelView m, JsonNode? ids, JsonNode? folders)
    {
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in (ids as JsonArray ?? []).Select(x => (string?)x).OfType<string>())
        {
            if (m.Get(id) is not { Class: Refs.Class }) throw new CommandException("There is no such reference document.");
            named.Add(id);
        }
        var tree = Refs.Index(Refs.Folders(m));
        var inFolders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in (folders as JsonArray ?? []).Select(x => (string?)x).OfType<string>())
        {
            if (!tree.ContainsKey(f)) throw new CommandException($"There is no folder {f} in the project's tree.");
            inFolders.Add(f);
            foreach (var fi in tree.Values)
                for (var p = fi.Parent; p != null; p = tree.GetValueOrDefault(p)?.Parent)
                    if (p == f) { inFolders.Add(fi.Id); break; }
        }
        var today = ReferenceDocumentFields.Today();
        return ReferenceDocumentFields.Ordered(m, tree).Where(e => Refs.Files(e).Count > 0 && (named.Contains(e.Id)
            || inFolders.Contains(e.Str("folder") ?? "") && Refs.StatusOf(e, today) is not ("superseded" or "withdrawn"))).ToList();
    }

    static bool Flag(JsonObject p, string key) => p[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
}
