using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Platform.Cde;

namespace Lapin.Protocol;

/// <summary>
/// An issue (docs/plan-cde.md): the host's part, because it makes files and
/// exports. The checks run first and a blocking finding refuses the issue;
/// then each sheet's PDF (and the set, the IFC model, the DXFs when asked) is
/// made, named by the convention with the status and revision, and hashed;
/// the transmittal (PDF and CSV) and the manifest are made beside them. Every
/// file is kept in the project's file (<see cref="PackageIssues"/>, the IFC
/// only on request) and, when the project has a folder on this computer, also
/// written into its container. The issue is recorded in the project (one undo
/// step) with the sheets' drawing hashes and the number of the version the
/// dispatcher saves right after, in the same call.
/// </summary>
public static class CdeIssue
{
    public static (JsonObject Result, Diff Diff) Run(Session session, PackageIssues store, JsonObject p, int? version = null)
    {
        var m = session.Model;
        var q = session.Reader;
        // The folder is optional: the project's file holds the issue; a folder is where it is also exchanged. A browser has none (its file system is the page's own).
        var toFolder = !OperatingSystem.IsBrowser() && (p["toFolder"] is not JsonValue tv || !tv.TryGetValue<bool>(out var t) || t);
        var folder = !toFolder ? null : (string?)p["folder"] is { Length: > 0 } given ? given : Cde.Folder(m);
        var sheets = Cde.SheetsOf(m, p["sheets"]);
        var model = Flag(p, "model");
        if (sheets.Count == 0 && !model) throw new CommandException("Choose at least one sheet, or the model.");
        var names = Cde.Names(m, p);
        var revision = (string)names["revision"]!;
        var status = (string)names["status"]!;
        if (!CdeRevisions.IsRevision(revision)) throw new CommandException("A revision is P01, P02… (preliminary) or C01, C02… (contractual).");
        if (!CdeRevisions.IsStatus(status)) throw new CommandException("A status code is S0–S7, A1–A9, B1–B9 or CR.");
        var container = (string?)p["container"] is { Length: > 0 } c ? c : CdeRevisions.ContainerFor(status);
        if (!CdeRevisions.Containers.Contains(container)) throw new CommandException($"The container is one of {string.Join(", ", CdeRevisions.Containers)}.");
        var number = (int)names["number"]!;
        var name = (string?)p["name"] is { Length: > 0 } nm ? nm.Trim() : $"Issue {number}";
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
        IssuedFile Write(string fileName, byte[] bytes, string kind, Entity? sheet = null, string? hash = null)
        {
            var stored = kind != "ifc" || keepModel;
            var sha = stored ? store.Put(bytes) : CdeTransmittal.Sha256(bytes);
            if (into != null) File.WriteAllBytes(Path.Combine(into, fileName), bytes);
            var f = new IssuedFile(fileName, kind, sheet?.Id, sha, bytes.LongLength, hash, stored);
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
        if (model)
        {
            using var ms = new MemoryStream();
            var options = new IfcExportOptions((string?)p["schema"] ?? "IFC4", (string?)p["detail"] ?? "group", (string?)p["phase"]);
            IfcExport.Write(session, ms, (string)names["model"]!, options);
            Put((string)names["model"]!, ms.ToArray(), "ifc", null);
        }

        // The transmittal and the manifest, named as an information exchange of this issue's number.
        var naming = CdeHooks.Naming?.Invoke(CdeNaming.Of(m)) ?? CdeNaming.Of(m);
        var header = new CdeTransmittal.Header(Sheets.ProjectName(q), name, number, DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), revision, status, container, note, naming.Originator);
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
        var diff = session.Execute("cde.recordIssue", args, out var recorded);
        var result = recorded as JsonObject ?? new JsonObject();
        result["path"] = into;
        if (folderProblem != null) result["folderProblem"] = folderProblem;
        result["warnings"] = new JsonArray(findings.Where(f => !f.Blocking).Select(f => (JsonNode)f.ToJson()).ToArray());
        return (result, diff);
    }

    static bool Flag(JsonObject p, string key) => p[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
}
