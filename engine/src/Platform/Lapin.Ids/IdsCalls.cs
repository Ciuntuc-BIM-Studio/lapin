using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Cde;

namespace Lapin.Ids;

/// <summary>
/// The IDS calls (docs/ids.md), in this lazy part with the reader and the checker (its code interpreted in the browser, out of the
/// first download), reached from the dispatcher (Lapin.Protocol's <c>EngineDispatcher.IdsCall</c>)
/// through one method never inlined, so a session that never adds or runs an IDS touches nothing of Lapin.Ids (nor of the XML it reads
/// with), which the browser's engine fetches the first time (docs/plan-browser-engine.md, Lazy parts).
/// <list type="bullet">
/// <item><c>ids.add</c>: an .ids file (its bytes as base64 in <c>data</c>, its <c>text</c>, or a <c>path</c> on the desktop) read as
/// IDS 1.0 (refused with every problem and its line when it is not) and kept in the project (<c>ids.store</c>: one undo step); with
/// <c>files</c> (each its <c>name</c> and <c>text</c>), several in one step (W6d: a CDE server's IDS files brought in).</item>
/// <item><c>ids.read</c>: one kept file as read: its info and its specifications in words.</item>
/// <item><c>ids.run</c>: the project's IDS files (all, or <c>ids</c>) over the model as its IFC export writes it (or over an IFC file
/// given as <c>data</c> or <c>path</c>): per specification what it applies to, passes and fails; kept for <c>ids.results</c>,
/// <c>ids.makeRemarks</c> and <c>ids.report</c> until the model changes. Undo-free; stopped by cancel.long.</item>
/// <item><c>ids.results</c>: the last run, or one specification's failures a page at a time (<c>file</c>, <c>spec</c>, <c>offset</c>, <c>limit</c>).</item>
/// <item><c>ids.makeRemarks</c>: a remark (BCF topic) per failing specification of the last run, its failing elements selected, in one undo
/// step; a remark made before for the same specification is brought up to date. A CDE server's remarks sync sends them.</item>
/// <item><c>ids.report</c>: the last run as an XLSX workbook, to <c>path</c> or as bytes.</item>
/// </list>
/// </summary>
public sealed class IdsCalls(Session session, Action<string, int, int> ReportProgress)
{
    /// <summary>The last IDS run: the model's revision it saw, what it checked, and each file's outcome.</summary>
    sealed class IdsLast(long Revision, string? Project, string Source, long ExportMs, long Ms, List<(Entity File, IdsDocument Doc, IdsRun Run)> Runs)
{
    public long Revision { get; set; } = Revision;
    public string? Project { get; } = Project;
    public string Source { get; } = Source;
    public long ExportMs { get; } = ExportMs;
    public long Ms { get; } = Ms;
    public List<(Entity File, IdsDocument Doc, IdsRun Run)> Runs { get; } = Runs;
}

    /// <summary>The last IDS run.</summary>
    IdsLast? _idsLast;

    static IdsDocument ReadIds(byte[] bytes)
    {
        try { return IdsReader.Read(bytes); }
        catch (IdsFormatException ex) { throw new CommandException("Not an IDS 1.0 file. " + string.Join(" ", ex.Problems.Take(12)) + (ex.Problems.Count > 12 ? $" (and {ex.Problems.Count - 12} more)" : "")); }
    }

    /// <summary>The arguments of <c>ids.store</c> for an .ids file's bytes, read first (refused, naming the file, when it is not IDS 1.0).</summary>
    static (JsonObject Args, IdsDocument Doc) StoreArgs(JsonObject p, byte[] bytes, string name)
    {
        if (bytes.Length > IdsFiles.MaxBytes) throw new CommandException($"The IDS file is {bytes.Length / 1e6:0.0} MB: more than the {IdsFiles.MaxBytes / 1_000_000} MB an IDS is kept up to.");
        var doc = ReadIds(bytes);
        // Kept as text, as the file wrote it (its BOM dropped).
        var xml = new UTF8Encoding(false).GetString(bytes).TrimStart('﻿');
        return (new JsonObject
        {
            ["file"] = name, ["xml"] = xml, ["title"] = doc.Info.Title, ["version"] = doc.Info.Version, ["specifications"] = doc.Specifications.Count,
            ["id"] = (string?)p["id"], ["author"] = (string?)p["author"],
        }, doc);
    }

    /// <summary>Several .ids files (<c>files</c>: each its <c>name</c> and <c>text</c>) kept in one undo step (W6d: a CDE server's files brought in).</summary>
    public (object? Result, Diff? Diff) AddMany(JsonObject p, JsonArray files)
    {
        var list = new JsonArray();
        foreach (var f in files.OfType<JsonObject>())
        {
            var name = (string?)f["name"] ?? "requirements.ids";
            try { var (a, _) = StoreArgs(p, Encoding.UTF8.GetBytes((string?)f["text"] ?? ""), name); a.Remove("id"); list.Add((JsonNode)a); }
            catch (CommandException ex) { throw new CommandException($"{name}: {ex.Message}"); }
        }
        if (list.Count == 0) throw new CommandException("No IDS file was given.");
        var diff = session.Execute("ids.store", new JsonObject { ["files"] = list }, out var stored);
        _idsLast = null;
        return (new JsonObject { ["files"] = stored?.DeepClone() }, diff);
    }

    public (object? Result, Diff? Diff) Add(JsonObject p, byte[] bytes, string name)
    {
        var (args, doc) = StoreArgs(p, bytes, name);
        var diff = session.Execute("ids.store", args, out var stored);
        _idsLast = null;
        var o = stored as JsonObject ?? [];
        o["ids"] = doc.ToJson();
        return (o, diff);
    }

    public (object? Result, Diff? Diff) Read(JsonObject p)
    {
        var e = IdsFile(session.Model, (string?)p["id"]);
        var o = IdsFiles.ToJson(e);
        o["ids"] = ReadIds(Encoding.UTF8.GetBytes(IdsFiles.Text(e) ?? "")).ToJson();
        return (o, null);
    }

    static Entity IdsFile(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: IdsFiles.Class } e ? e : throw new CommandException("There is no such IDS file in the project.");

    public (object? Result, Diff? Diff) Run(JsonObject p, (byte[] Bytes, string Name)? given)
    {
        var m = session.Model;
        var wanted = p["ids"] is JsonArray a ? a.Select(x => (string?)x).OfType<string>().ToHashSet() : null;
        var files = IdsFiles.All(m).Where(e => wanted == null || wanted.Contains(e.Id)).ToList();
        if (files.Count == 0) throw new CommandException(wanted == null ? "The project has no IDS file: add one first." : "There is no such IDS file in the project.");
        var docs = files.Select(f => (File: f, Doc: ReadIds(Encoding.UTF8.GetBytes(IdsFiles.Text(f) ?? "")))).ToList();
        var stop = session.Cancellation;
        var sw = Stopwatch.StartNew();
        StepFile ifc;
        string source;
        long exportMs;
        if (given is var (bytes, name))
        {
            ReportProgress("Reading the IFC file", 0, 0);
            ifc = StepFile.Read(bytes);
            source = name;
        }
        else
        {
            // The model as its IFC export writes it (what a delivery hands over): IFC4, or IFC 4.3 when every specification asks for it
            // alone; bodies as face sets, the cheapest to write, since IDS 1.0 reads no geometry.
            var versions = docs.SelectMany(d => d.Doc.Specifications).SelectMany(s => s.IfcVersions).ToHashSet();
            var schema = !versions.Contains("IFC4") && versions.Contains("IFC4X3_ADD2") ? "IFC4X3_ADD2" : "IFC4";
            ReportProgress("Writing the model as IFC", 0, 0);
            var (exported, _) = IfcExport.Bytes(session, "model.ifc", new IfcExportOptions(Schema: schema, Geometry: "faceset", DerivedLoadBearing: true));
            stop.ThrowIfCancellationRequested();
            ifc = StepFile.Read(exported);
            source = "model";
        }
        exportMs = sw.ElapsedMilliseconds;
        bool InModel(string gid) => m.Get(gid) != null;
        var runs = new List<(Entity, IdsDocument, IdsRun)>();
        try
        {
            foreach (var (file, doc) in docs)
                runs.Add((file, doc, IdsChecker.Run(ifc, doc, source == "model" ? InModel : g => m.Get(g) != null, stop,
                    (done, total) => ReportProgress($"Checking {file.Name}", done, total))));
        }
        catch (OperationCanceledException) { throw new CommandException("Stopped: the IDS check did not finish."); }
        var last = new IdsLast(m.Revision, Spatial.Project(m)?.Id, source, exportMs, sw.ElapsedMilliseconds, runs);
        _idsLast = last;
        return (IdsJson(last, limit: 50), null);
    }

    JsonObject IdsJson(IdsLast last, int limit)
    {
        var (blocks, onSubmit) = IdsFiles.Rules(session.Model);
        var failing = last.Runs.Sum(r => r.Run.Failing);
        return new JsonObject
        {
            ["source"] = last.Source, ["exportMs"] = last.ExportMs, ["ms"] = last.Ms,
            ["stale"] = last.Revision != session.Model.Revision || last.Project != Spatial.Project(session.Model)?.Id,
            ["failing"] = failing, ["blocks"] = blocks, ["onSubmit"] = onSubmit, ["blocking"] = blocks && failing > 0,
            ["files"] = new JsonArray([.. last.Runs.Select(r => (JsonNode)new JsonObject
            {
                ["id"] = r.File.Id, ["name"] = r.File.Name, ["file"] = r.File.Str("file"), ["title"] = r.Doc.Info.Title, ["schema"] = r.Run.Schema,
                ["objects"] = r.Run.Objects, ["ms"] = r.Run.Milliseconds,
                ["failing"] = r.Run.Failing, ["passing"] = r.Run.Passing, ["skipped"] = r.Run.Skipped,
                ["specifications"] = new JsonArray([.. r.Run.Specifications.Select(s => (JsonNode)s.ToJson(limit))]),
            })]),
        };
    }

    IdsLast LastIds() => _idsLast ?? throw new CommandException("Run the IDS check first.");

    public (object? Result, Diff? Diff) Results(JsonObject p)
    {
        if (_idsLast is not { } last) return (new JsonObject { ["files"] = new JsonArray(), ["none"] = true }, null);
        if ((string?)p["file"] is not { } fileId) return (IdsJson(last, Math.Clamp((int?)Json.Number(p["limit"]) ?? 50, 0, IdsChecker.MaxFailures)), null);
        var run = last.Runs.FirstOrDefault(r => r.File.Id == fileId);
        if (run.File == null) throw new CommandException("The last run did not check that IDS file.");
        var index = (int?)Json.Number(p["spec"]) ?? 0;
        var spec = run.Run.Specifications.FirstOrDefault(s => s.Index == index) ?? throw new CommandException("There is no such specification.");
        var offset = Math.Max(0, (int?)Json.Number(p["offset"]) ?? 0);
        var limit = Math.Clamp((int?)Json.Number(p["limit"]) ?? 100, 1, IdsChecker.MaxFailures);
        var o = spec.ToJson(0);
        o["failures"] = new JsonArray([.. spec.Failures.Skip(offset).Take(limit).Select(f => (JsonNode)f.ToJson())]);
        o["offset"] = offset;
        o["total"] = spec.Failures.Count;
        o["stale"] = last.Revision != session.Model.Revision;
        return (o, null);
    }

    /// <summary>A failing specification's remark: its GUID from the IDS file's id and the specification (its identifier, else its name), so a run again finds it.</summary>
    static Guid IdsRemarkGuid(string fileId, IdsSpecResult s) => GlobalId.ToGuid(GlobalId.Derive(fileId, "ids-spec:" + (s.Identifier is { Length: > 0 } id ? id : s.Name)));

    public (object? Result, Diff? Diff) MakeRemarks(JsonObject p)
    {
        var last = LastIds();
        if (last.Revision != session.Model.Revision) throw new CommandException("The model changed since the IDS check: run it again first.");
        if (last.Source != "model") throw new CommandException("The last run checked a file, not the model: remarks are made from a run over the model.");
        var only = p["specs"] is JsonArray sa ? sa.Select(x => (string?)x).OfType<string>().ToHashSet() : null;
        var remarks = new JsonArray();
        foreach (var (file, doc, run) in last.Runs)
            foreach (var s in run.Specifications.Where(s => s.Fails))
            {
                if (only != null && !only.Contains($"{file.Id}:{s.Index}")) continue;
                var b = new StringBuilder();
                b.Append($"IDS „{doc.Info.Title}” ({file.Str("file")}), specification „{s.Name}”");
                if (s.Identifier is { Length: > 0 }) b.Append($" [{s.Identifier}]");
                b.Append(s.Usage == "prohibited" ? $": {s.Applicable} elements it prohibits are in the model." : s.Applicable == 0 ? ": required, and nothing in the model is what it applies to." : $": {s.Failed} of {s.Applicable} elements fail.");
                if (s.Description is { Length: > 0 }) b.Append("\n\n").Append(s.Description);
                if (s.Instructions is { Length: > 0 }) b.Append("\n\nInstructions: ").Append(s.Instructions);
                if (s.Requirements.Count > 0) b.Append("\n\nRequirements:\n").Append(string.Join("\n", s.Requirements.Select(r => "- " + r)));
                if (s.Failures.Count > 0)
                {
                    b.Append("\n\nFailures:\n");
                    foreach (var f in s.Failures.Take(20)) b.Append($"- {(f.Name is { Length: > 0 } n ? n + " " : "")}({f.Class} {f.GlobalId}): {f.Requirement}: has {f.Actual}, needs {f.Expected}\n");
                    if (s.Failures.Count > 20 || s.Dropped > 0) b.Append($"… and {s.Failures.Count - 20 + s.Dropped} more.");
                }
                var selection = (s.FailedElements.Count > 0 ? s.FailedElements : s.FailedGlobalIds).Take(5000);
                remarks.Add((JsonNode)new JsonObject
                {
                    ["guid"] = IdsRemarkGuid(file.Id, s).ToString("D"),
                    ["title"] = $"IDS: {s.Name}",
                    ["description"] = b.ToString().TrimEnd(),
                    ["selection"] = new JsonArray([.. selection.Select(x => (JsonNode)x)]),
                    ["priority"] = s.Usage == "required" ? "High" : "Normal",
                    ["camera"] = CameraOn(selection),
                });
            }
        if (remarks.Count == 0) return (new JsonObject { ["made"] = 0, ["updated"] = 0, ["ids"] = new JsonArray() }, null);
        var diff = session.Execute("ids.writeRemarks", new JsonObject { ["remarks"] = remarks, ["author"] = (string?)p["author"] }, out var result);
        // The remarks are a change to the model: the run still stands for everything else in it.
        last.Revision = session.Model.Revision;
        return (result, diff);
    }

    /// <summary>
    /// A camera looking at the elements from the south-east, above, far enough to see them all: a BCF viewpoint needs a camera (or a
    /// snapshot) for a CDE server to take it, and this one opens on the failing elements. Their bodies' box, at most 300 of them.
    /// </summary>
    JsonObject CameraOn(IEnumerable<string> ids)
    {
        var solids = ids.Take(300).Select(session.Geometry).Where(g => g != null).SelectMany(g => g!.Parts.Select(p => p.Solid));
        var box = Lapin.Geometry.Measure.Bounds(solids);
        var (min, max) = box ?? (new Lapin.Geometry.Vec3(-5, -5, 0), new Lapin.Geometry.Vec3(5, 5, 3));
        var c = (min + max) * 0.5;
        var size = Math.Max(3, (max - min).Length);
        var eye = c + new Lapin.Geometry.Vec3(1, -1, 0.8).Normalized() * (size * 1.2);
        var dir = (c - eye).Normalized();
        var up = Lapin.Geometry.Vec3.Cross(Lapin.Geometry.Vec3.Cross(dir, new Lapin.Geometry.Vec3(0, 0, 1)), dir).Normalized();
        static JsonArray P(Lapin.Geometry.Vec3 v) => new((JsonNode)Math.Round(v.X, 4), (JsonNode)Math.Round(v.Y, 4), (JsonNode)Math.Round(v.Z, 4));
        return new JsonObject { ["perspective"] = true, ["viewPoint"] = P(eye), ["direction"] = P(dir), ["up"] = P(up), ["fieldOfView"] = 60.0, ["viewToWorldScale"] = size };
    }

    public (object? Result, Diff? Diff) Report(JsonObject p, Func<ArraySegment<byte>, string, JsonObject> deliver)
    {
        var last = LastIds();
        var project = Spatial.Project(session.Model)?.Name ?? "Project";
        var made = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        var runs = last.Runs.Select(r => new IdsReport.Checked(r.Doc.Info.Title, r.File.Str("file") ?? r.File.Name, r.Run)).ToList();
        var stem = FileNames.Stem(project, "project");
        return (deliver(IdsReport.Xlsx(project, made, runs), $"{stem}-ids.xlsx"), null);
    }
}
