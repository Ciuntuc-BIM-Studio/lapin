using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Cde;

namespace Lapin.Protocol;

/// <summary>
/// An issue into the project folder (docs/plan-cde.md): the host's part,
/// because it writes files and exports. The checks run first and a blocking
/// finding refuses the issue; then each sheet's PDF (and the set, the IFC
/// model, the DXFs when asked) goes into the container, named by the
/// convention with the status and revision; each file is hashed; the
/// transmittal (PDF and CSV) and the manifest are written beside them; the
/// issue is recorded in the project (one undo step) with the sheets' drawing
/// hashes. The interface saves the version after, through the versions'
/// own path (project.saveVersion), named after the issue.
/// </summary>
public static class CdeIssue
{
    public static (JsonObject Result, Diff Diff) Run(Session session, JsonObject p)
    {
        var m = session.Model;
        var q = session.Reader;
        var folder = (string?)p["folder"] is { Length: > 0 } given ? given : Cde.Folder(m) ?? throw new CommandException("Choose the project folder first (Manage → Project folder).");
        if (session.Path == null) throw new CommandException("Save the project first: an issue is kept as a version in its file.");
        var sheets = Cde.SheetsOf(m, p["sheets"]);
        var model = p["model"] is JsonValue mv && mv.TryGetValue<bool>(out var wantModel) && wantModel;
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

        // The checks: a blocking finding refuses the issue; warnings are the interface's to override (it ran them already).
        var findings = CdeChecks.Run(q, sheets, revision);
        if (findings.Any(f => f.Blocking))
            throw new CommandException($"The checks found what must be fixed first: {string.Join(" ", findings.Where(f => f.Blocking).Take(3).Select(f => f.Message))}{(findings.Count(f => f.Blocking) > 3 ? " …" : "")}");

        Directory.CreateDirectory(folder);
        foreach (var cont in CdeRevisions.Containers) Directory.CreateDirectory(Path.Combine(folder, cont));
        var into = Path.Combine(folder, container);

        var files = new List<IssuedFile>();
        var rows = new List<CdeTransmittal.Row>();
        // A file written into the container and hashed; `Put` also lists it in the transmittal (the transmittal's own files are not).
        IssuedFile Write(string fileName, byte[] bytes, string kind, Entity? sheet = null, string? hash = null)
        {
            File.WriteAllBytes(Path.Combine(into, fileName), bytes);
            var f = new IssuedFile(fileName, kind, sheet?.Id, CdeTransmittal.Sha256(bytes), bytes.LongLength, hash);
            files.Add(f);
            return f;
        }
        void Put(string fileName, byte[] bytes, string kind, Entity? sheet, string? hash = null) =>
            rows.Add(new CdeTransmittal.Row(Write(fileName, bytes, kind, sheet, hash), sheet?.Str("number"), sheet?.Name));

        var dxf = p["dxf"] is JsonValue dv && dv.TryGetValue<bool>(out var wantDxf) && wantDxf;
        var byId = (names["sheets"] as JsonArray ?? []).OfType<JsonObject>().ToDictionary(o => (string)o["id"]!, o => (string)o["name"]!);
        foreach (var sheet in sheets)
        {
            var stem = byId[sheet.Id];
            var hash = Sheets.DrawingHash(q, sheet);
            Put(CdeNaming.WithRevision(stem, status, revision, "pdf"), Sheets.Pdf(q, [sheet]), "pdf", sheet, hash);
            if (dxf) Put(CdeNaming.WithRevision(stem, status, revision, "dxf"), Sheets.Dxf(q, sheet), "dxf", sheet, hash);
        }
        if (sheets.Count > 1 && p["set"] is JsonValue sv && sv.TryGetValue<bool>(out var wantSet) && wantSet)
            Put((string)names["set"]!, Sheets.Pdf(q, sheets), "pdf", null);
        if (model)
        {
            using var ms = new MemoryStream();
            var options = new IfcExportOptions((string?)p["schema"] ?? "IFC4", (string?)p["detail"] ?? "group", (string?)p["phase"]);
            IfcExport.Write(session, ms, (string)names["model"]!, options);
            Put((string)names["model"]!, ms.ToArray(), "ifc", null);
        }

        // The transmittal and the manifest, named as an information exchange of this issue's number.
        var naming = CdeHooks.Naming?.Invoke(CdeNaming.Of(m)) ?? CdeNaming.Of(m);
        var header = new CdeTransmittal.Header(Spatial.Project(m)?.Name ?? "Project", name, number, DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), revision, status, container, note, naming.Originator);
        var exchange = naming.ForTransmittal(number);
        var transmittalPdf = CdeNaming.WithRevision(exchange, status, revision, "pdf");
        Write(transmittalPdf, CdeTransmittal.Pdf(header, rows), "pdf");
        Write(CdeNaming.WithRevision(exchange, status, revision, "csv"), Encoding.UTF8.GetBytes(CdeTransmittal.Csv(header, rows)), "csv");
        Write(CdeNaming.WithRevision(exchange, status, revision, "json"), new UTF8Encoding(false).GetBytes(CdeTransmittal.Manifest(header, rows, transmittalPdf)), "json");

        var args = new JsonObject
        {
            ["name"] = name, ["revision"] = revision, ["status"] = status, ["container"] = container, ["folder"] = folder, ["note"] = note,
            ["files"] = new JsonArray(files.Select(f => (JsonNode)f.ToJson()).ToArray()),
        };
        var diff = session.Execute("cde.recordIssue", args, out var recorded);
        var result = recorded as JsonObject ?? new JsonObject();
        result["path"] = into;
        result["warnings"] = new JsonArray(findings.Where(f => !f.Blocking).Select(f => (JsonNode)f.ToJson()).ToArray());
        return (result, diff);
    }
}
