using System.IO.Compression;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Platform.Cde;

namespace Lapin.Protocol;

/// <summary>
/// An issue's deliverables as the project's file keeps them (docs/plan-cde.md,
/// decision 185): one file read back byte for byte (to open or save), all of
/// them as one zip (what the browser hands over), or written out to a folder
/// later (the project folder's container, or any folder picked). A file the
/// project does not keep (the IFC unless asked, an old issue's files let go)
/// is listed as missing, never made again: the model may have changed since.
/// </summary>
public static class CdeDeliverables
{
    static CdeIssues.Issue Issue(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: CdeIssues.Class } e ? CdeIssues.Read(e) : throw new CommandException("There is no such issue.");

    /// <summary>The bytes of one of an issue's files, by its name.</summary>
    public static (string Name, byte[] Bytes) File(IModelView m, PackageIssues store, string? id, string? name)
    {
        var issue = Issue(m, id);
        var f = issue.Files.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)) ?? throw new CommandException($"Issue {issue.Number} has no file {name}.");
        if (!f.Stored || !store.Has(f.Sha256)) throw new CommandException($"{f.Name} is not kept in the project file{(f.Kind == "ifc" ? " (the IFC is kept only when asked)" : "")}.");
        return (f.Name, store.Get(f.Sha256));
    }

    /// <summary>The files an issue keeps, and those it only lists (by name).</summary>
    static (List<(IssuedFile File, byte[] Bytes)> Kept, List<string> Missing) Files(CdeIssues.Issue issue, PackageIssues store)
    {
        var kept = new List<(IssuedFile, byte[])>();
        var missing = new List<string>();
        foreach (var f in issue.Files)
        {
            if (f.Stored && store.Has(f.Sha256)) kept.Add((f, store.Get(f.Sha256)));
            else missing.Add(f.Name);
        }
        return (kept, missing);
    }

    /// <summary>An issue's kept files as one zip, named after its transmittal (…-IE-…_S2_P01.zip); a file it only lists is not in it.</summary>
    public static (string Name, byte[] Bytes, List<string> Missing) Zip(IModelView m, PackageIssues store, string? id)
    {
        var issue = Issue(m, id);
        var (kept, missing) = Files(issue, store);
        if (kept.Count == 0) throw new CommandException($"Issue {issue.Number} keeps no file in the project file.");
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (f, bytes) in kept)
            {
                using var s = zip.CreateEntry(f.Name, CompressionLevel.Optimal).Open();
                s.Write(bytes);
            }
        var manifest = issue.Files.FirstOrDefault(f => f.Kind == "json")?.Name;
        var name = manifest != null ? Path.ChangeExtension(manifest, ".zip") : $"Issue {issue.Number} {issue.Revision}.zip";
        return (name, ms.ToArray(), missing);
    }

    /// <summary>
    /// An issue written out to a folder: the folder given, else the project folder; into the issue's container under it (as an issue writes it),
    /// unless <paramref name="flat"/> (a folder picked for this alone). A file already there with the same bytes is left as it is.
    /// </summary>
    public static JsonObject Publish(IModelView m, PackageIssues store, string? id, string? folder, bool flat)
    {
        var issue = Issue(m, id);
        var root = folder is { Length: > 0 } ? folder : Cde.Folder(m) ?? throw new CommandException("Choose a folder, or the project folder first (Manage → Project folder).");
        var into = flat ? root : Path.Combine(root, issue.Container);
        if (!flat && !Directory.Exists(root)) throw new CommandException("The project folder is not on this computer right now.");
        Directory.CreateDirectory(into);
        if (!flat) foreach (var c in CdeRevisions.Containers) Directory.CreateDirectory(Path.Combine(root, c));
        var (kept, missing) = Files(issue, store);
        var written = new JsonArray();
        var same = 0;
        DateTime? issuedAt = DateTime.TryParse(issue.At, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
        foreach (var (f, bytes) in kept)
        {
            var target = Path.Combine(into, f.Name);
            if (System.IO.File.Exists(target) && new FileInfo(target).Length == bytes.LongLength && CdeTransmittal.Sha256(System.IO.File.ReadAllBytes(target)) == f.Sha256) { same++; continue; }
            System.IO.File.WriteAllBytes(target, bytes);
            // Dated as issued: the folder's scan for reviewed PDFs (cde.scanRemarks) tells a file touched after its issue by its time.
            if (issuedAt is { } at) try { System.IO.File.SetLastWriteTimeUtc(target, at); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            written.Add((JsonNode)f.Name);
        }
        return new JsonObject
        {
            ["path"] = into, ["written"] = written, ["same"] = same,
            ["missing"] = new JsonArray(missing.Select(x => (JsonNode)x).ToArray()),
        };
    }
}
