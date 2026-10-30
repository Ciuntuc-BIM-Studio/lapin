using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Platform;
using Lapin.Platform.Cde;
using Lapin.Platform.Cde.PdfRemarks;

namespace Lapin.Protocol;

/// <summary>
/// The host's part of the remarks on sheets (docs/plan-cde.md, stage 3): a
/// PDF read from a path (the desktop) or from its bytes (a browser), the
/// issue manifests beside it read for the sheet it prints, and the project
/// folder scanned for the issued PDFs a reviewer has touched since the
/// issue (Shared and Published, by their modification time against the
/// issue's). The remarks themselves are the platform's (<see cref="CdeRemarks"/>).
/// </summary>
public static class CdePdfRemarks
{
    /// <summary>cde.readPdf {path | data, name, sheetId?}: the file's remarks mapped onto the sheet matched (or given), for the interface to confirm and import.</summary>
    public static JsonObject Read(Session session, JsonObject p)
    {
        byte[] bytes;
        string name;
        List<ManifestFile>? manifest = null;
        if ((string?)p["data"] is { } data)
        {
            try { bytes = Convert.FromBase64String(data); }
            catch (FormatException) { throw new CommandException("The file's bytes are not base64."); }
            name = (string?)p["name"] ?? "file.pdf";
        }
        else
        {
            var path = (string?)p["path"] ?? throw new CommandException("The file's path is missing.");
            if (!File.Exists(path)) throw new CommandException($"There is no file at {path}.");
            bytes = File.ReadAllBytes(path);
            name = Path.GetFileName(path);
            manifest = Manifests(Path.GetDirectoryName(path));
        }
        if (bytes.Length == 0) throw new CommandException("The file is empty.");
        var r = CdeRemarks.FromPdf(session.Model, bytes, name, manifest, (string?)p["sheetId"]);
        if ((string?)p["path"] is { } at) r["path"] = at;
        return r;
    }

    /// <summary>The files every issue manifest (<c>lapin-issue/1</c>) in a folder lists, with their sheets and revisions; empty when there is none.</summary>
    public static List<ManifestFile> Manifests(string? folder)
    {
        var o = new List<ManifestFile>();
        if (folder == null || !Directory.Exists(folder)) return o;
        foreach (var json in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                if (new FileInfo(json).Length > 4_000_000) continue;
                if (JsonNode.Parse(File.ReadAllText(json)) is not JsonObject m || (string?)m["format"] != "lapin-issue/1") continue;
                var revision = (string?)m["revision"] ?? "";
                var issueId = $"{Path.GetFileName(json)}";
                var transmittal = (string?)m["transmittal"];
                // The drawings alone: the sheets' PDFs and the set; not the model, not the transmittal.
                foreach (var f in (m["files"] as JsonArray ?? []).OfType<JsonObject>())
                    if ((string?)f["name"] is { Length: > 0 } fn && ((string?)f["kind"] ?? "pdf") == "pdf" && !string.Equals(fn, transmittal, StringComparison.OrdinalIgnoreCase))
                        o.Add(new ManifestFile(fn, (string?)f["sheetId"], revision, issueId));
            }
            catch (Exception) { /* a JSON that is not a manifest, or unreadable: not ours */ }
        }
        return o;
    }

    /// <summary>
    /// cde.scanRemarks {folder?}: the PDFs in the project folder's Shared and Published containers that an issue of the project (or a manifest beside
    /// them) lists for a sheet or the set, each with the sheet, the revision, when it was last changed, whether that is after the issue, and when
    /// its remarks were last read into the project.
    /// </summary>
    public static JsonArray Scan(Session session, JsonObject p)
    {
        var m = session.Model;
        var folder = (string?)p["folder"] is { Length: > 0 } given ? given : Cde.Folder(m) ?? throw new CommandException("Choose the project folder first (Manage → Project folder).");
        if (!Directory.Exists(folder)) throw new CommandException("The project folder is not on this computer right now.");
        var issues = CdeIssues.All(m);
        var lastRead = CdeRemarks.Of(m)
            .Select(e => (File: ((string?)(e.Attrs["source"] as JsonObject)?["file"] ?? "").ToLowerInvariant(), At: (string?)(e.Attrs["source"] as JsonObject)?["read"] ?? ""))
            .Where(x => x.File.Length > 0).GroupBy(x => x.File).ToDictionary(g => g.Key, g => g.Max(x => x.At));
        var o = new JsonArray();
        foreach (var container in new[] { "Shared", "Published" })
        {
            var dir = Path.Combine(folder, container);
            if (!Directory.Exists(dir)) continue;
            var manifest = Manifests(dir);
            foreach (var path in Directory.EnumerateFiles(dir, "*.pdf").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(path);
                var issue = issues.AsEnumerable().Reverse().FirstOrDefault(i => i.Files.Any(f => f.Kind == "pdf" && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)));
                var file = issue?.Files.First(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
                var fromManifest = file == null ? manifest.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) : null;
                if (file == null && fromManifest == null) continue;   // not an issued drawing (a transmittal, someone else's file)
                var sheetId = file?.SheetId ?? fromManifest?.SheetId;
                var isSet = (file != null && file.SheetId == null) || (fromManifest != null && fromManifest.SheetId == null);
                if (sheetId == null && !isSet) continue;
                if (file != null && file.SheetId == null && (string?)(Cde.Names(m, new JsonObject { ["revision"] = issue!.Revision, ["status"] = issue.Status })["set"]) is { } setName && !string.Equals(setName, name, StringComparison.OrdinalIgnoreCase)) continue;
                var sheet = sheetId != null && m.Get(sheetId) is { Class: Sheets.Class } s ? s : null;
                var modified = File.GetLastWriteTimeUtc(path);
                var issuedAt = issue != null && DateTime.TryParse(issue.At, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at) ? at : (DateTime?)null;
                var read = lastRead.GetValueOrDefault(name.ToLowerInvariant());
                var readAt = read != null && DateTime.TryParse(read, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var ra) ? ra : (DateTime?)null;
                o.Add((JsonNode)new JsonObject
                {
                    ["path"] = path, ["name"] = name, ["container"] = container,
                    ["sheetId"] = sheet?.Id, ["sheetNumber"] = sheet?.Str("number"), ["sheetName"] = sheet?.Name, ["set"] = isSet,
                    ["revision"] = issue?.Revision ?? fromManifest?.Revision, ["issue"] = issue?.Name,
                    ["modified"] = modified.ToString("o", CultureInfo.InvariantCulture),
                    // Touched after the issue (a minute's slack for the writing itself), and not read since.
                    ["newer"] = issuedAt == null || modified > issuedAt.Value.AddMinutes(1),
                    ["read"] = read,
                    ["unread"] = (issuedAt == null || modified > issuedAt.Value.AddMinutes(1)) && (readAt == null || modified > readAt.Value),
                });
            }
        }
        return o;
    }
}
