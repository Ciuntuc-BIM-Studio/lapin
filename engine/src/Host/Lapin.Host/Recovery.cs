using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Host;

/// <summary>A journal the user can bring back: unsaved work from a session that ended without saving.</summary>
public sealed record RecoverableWork(string File, string? Path, DateTimeOffset At, int Edits, string? LastEdit);

/// <summary>
/// The recovery journal: every edit of the open project, appended as it
/// happens to <c>&lt;user-dir&gt;/recovery/&lt;session&gt;.jsonl</c>, so a crash
/// loses nothing. The first line says what the project started from:
/// <code>
/// {"op":"start","at":…,"product":…,"path":"/x.lapin","sha256":"…"}            an opened (or saved) file
/// {"op":"start","at":…,"product":…,"path":null,"base":{"entities":[…],"relations":[…]}}   a new project
/// {"op":"tx","label":…,"entities":[{id,before,after,order}],"relations":[…]}
/// {"op":"undo"}  {"op":"redo"}
/// </code>
/// Saving starts the journal again (the file now holds everything). A
/// journal with no edit is not worth keeping and is removed.
/// </summary>
public sealed class Recovery : IDisposable
{
    readonly string _dir;
    readonly Session _session;
    string? _file;
    StreamWriter? _writer;
    int _edits;

    public Recovery(string userDir, Session session)
    {
        _dir = System.IO.Path.Combine(userDir, "recovery");
        Directory.CreateDirectory(_dir);
        _session = session;
        session.HistoryChanged += OnHistory;
        Start();
    }

    public string? CurrentFile => _file;

    void OnHistory(HistoryEvent e)
    {
        if (e.Kind == "start") { Start(); return; }
        var line = e.Kind == "tx" ? TxLine(e.Transaction!) : new JsonObject { ["op"] = e.Kind };
        Write(line);
        _edits++;
    }

    /// <summary>A new journal for the project as it is now; the previous one is no longer needed.</summary>
    void Start()
    {
        Close(delete: true);
        _file = System.IO.Path.Combine(_dir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.jsonl");
        _writer = new StreamWriter(new FileStream(_file, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        _edits = 0;
        var path = _session.Path;
        var start = new JsonObject { ["op"] = "start", ["at"] = DateTimeOffset.UtcNow, ["product"] = _session.Product.Id, ["path"] = path };
        if (path != null && File.Exists(path)) start["sha256"] = Hash(path);
        else
        {
            start["path"] = null;
            start["base"] = new JsonObject
            {
                ["entities"] = new JsonArray(_session.Model.Entities.Select(e => (JsonNode)EntityJson(e)).ToArray()),
                ["relations"] = JsonSerializer.SerializeToNode(_session.Model.Relations.ToList(), Json.Options),
                ["classes"] = JsonSerializer.SerializeToNode(_session.Model.Entities.Select(e => e.Class).Distinct().ToDictionary(c => c, _session.ModuleOf), Json.Options),
            };
        }
        Write(start);
    }

    void Write(JsonObject line)
    {
        if (_writer == null) return;
        _writer.WriteLine(line.ToJsonString(Json.Options));
        _writer.Flush();
        ((FileStream)_writer.BaseStream).Flush(flushToDisk: true);
    }

    void Close(bool delete)
    {
        _writer?.Dispose();
        _writer = null;
        if (delete && _file != null && File.Exists(_file)) File.Delete(_file);
        _file = null;
    }

    /// <summary>Ends cleanly: with nothing unsaved, the journal goes; with unsaved edits, it stays for next time.</summary>
    public void Dispose()
    {
        _session.HistoryChanged -= OnHistory;
        Close(delete: _edits == 0 || !_session.History.Dirty);
    }

    // ------------------------------------------------------------ bringing work back

    /// <summary>The journals of earlier sessions with edits in them, newest first. Empty ones are removed.</summary>
    public IReadOnlyList<RecoverableWork> List()
    {
        var o = new List<RecoverableWork>();
        foreach (var file in Directory.GetFiles(_dir, "*.jsonl"))
        {
            if (file == _file) continue;
            try
            {
                var lines = File.ReadAllLines(file).Where(l => l.Length > 0).ToList();
                var start = lines.Count > 0 ? JsonNode.Parse(lines[0]) as JsonObject : null;
                var edits = lines.Skip(1).Select(l => JsonNode.Parse(l) as JsonObject).OfType<JsonObject>().ToList();
                if (start == null || edits.Count == 0) { File.Delete(file); continue; }
                o.Add(new RecoverableWork(file, (string?)start["path"], start["at"]?.GetValue<DateTimeOffset>() ?? File.GetLastWriteTimeUtc(file),
                    edits.Count, (string?)edits.LastOrDefault(e => (string?)e["op"] == "tx")?["label"]));
            }
            catch (Exception ex) when (ex is JsonException or IOException or FormatException or InvalidOperationException)
            {
                // A journal cut off mid-line by the crash still counts: its complete lines are read on restore.
                o.Add(new RecoverableWork(file, null, File.GetLastWriteTimeUtc(file), 0, null));
            }
        }
        return o.OrderByDescending(w => w.At).ToList();
    }

    /// <summary>Brings the work of <paramref name="file"/> back into the session, which goes on journalling in it.</summary>
    public Diff Restore(string file)
    {
        if (System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file)) != System.IO.Path.GetFullPath(_dir)) throw new CommandException("Not a recovery journal.");
        var lines = File.ReadAllLines(file).Where(l => l.Length > 0).ToList();
        var parsed = new List<JsonObject>();
        foreach (var l in lines)
        {
            try { if (JsonNode.Parse(l) is JsonObject o) parsed.Add(o); }
            catch (JsonException) { break; }   // the last line, cut off by the crash
        }
        if (parsed.Count == 0 || (string?)parsed[0]["op"] != "start") throw new InvalidDataException("The journal has no start.");
        var start = parsed[0];
        var path = (string?)start["path"];
        IReadOnlyList<Entity> entities;
        IReadOnlyList<Relation> relations;
        IReadOnlyDictionary<string, string>? classes;
        if (path != null)
        {
            if (!File.Exists(path)) throw new CommandException($"The project the work was done on is gone: {path}.");
            if ((string?)start["sha256"] is { } sha && Hash(path) != sha) throw new CommandException($"{System.IO.Path.GetFileName(path)} changed since: the unsaved work no longer fits it.");
            var content = LapinPackage.Read(path);
            (entities, relations, classes) = (content.Entities, content.Relations, content.Classes);
        }
        else
        {
            var b = start["base"] as JsonObject ?? throw new InvalidDataException("The journal has no starting model.");
            entities = (b["entities"] as JsonArray ?? []).Select(n => ReadEntity(n!.AsObject())).ToList();
            relations = b["relations"].Deserialize<List<Relation>>(Json.Options) ?? [];
            classes = b["classes"].Deserialize<Dictionary<string, string>>(Json.Options);
        }
        var edits = parsed.Skip(1).Select(ReadEvent).ToList();
        var diff = _session.Recover(entities, relations, path, classes, edits);

        // Go on in that journal: a second crash still has everything.
        Close(delete: true);
        _file = file;
        _writer = new StreamWriter(new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read));
        _edits = edits.Count;
        return diff;
    }

    public void Discard(string file)
    {
        if (file == _file) throw new CommandException("That journal is in use.");
        if (System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file)) != System.IO.Path.GetFullPath(_dir)) throw new CommandException("Not a recovery journal.");
        File.Delete(file);
    }

    // ------------------------------------------------------------ lines

    static string Hash(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(s));
    }

    static JsonObject EntityJson(Entity e) => new()
    {
        ["id"] = e.Id, ["class"] = e.Class, ["name"] = e.Name, ["predefinedType"] = e.PredefinedType, ["attrs"] = e.CloneAttrs(),
    };

    static Entity ReadEntity(JsonObject o) =>
        new((string)o["id"]!, (string)o["class"]!, (string?)o["name"] ?? "", o["attrs"] is JsonObject a ? (JsonObject)a.DeepClone() : [], (string?)o["predefinedType"]);

    static JsonObject TxLine(Transaction t) => new()
    {
        ["op"] = "tx",
        ["label"] = t.Label,
        ["at"] = t.At,
        ["entities"] = new JsonArray(t.Entities.Select(c => (JsonNode)new JsonObject
        {
            ["id"] = c.Id, ["order"] = c.Order,
            ["before"] = c.Before == null ? null : EntityJson(c.Before),
            ["after"] = c.After == null ? null : EntityJson(c.After),
        }).ToArray()),
        ["relations"] = new JsonArray(t.Relations.Select(c => (JsonNode)new JsonObject
        {
            ["id"] = c.Id, ["order"] = c.Order,
            ["before"] = JsonSerializer.SerializeToNode(c.Before, Json.Options),
            ["after"] = JsonSerializer.SerializeToNode(c.After, Json.Options),
        }).ToArray()),
    };

    static HistoryEvent ReadEvent(JsonObject o)
    {
        var op = (string?)o["op"] ?? "";
        if (op != "tx") return new HistoryEvent(op);
        var entities = (o["entities"] as JsonArray ?? []).Select(n => n!.AsObject()).Select(c => new Change<Entity>(
            (string)c["id"]!, c["before"] is JsonObject b ? ReadEntity(b) : null, c["after"] is JsonObject a ? ReadEntity(a) : null, (int)c["order"]!)).ToList();
        var relations = (o["relations"] as JsonArray ?? []).Select(n => n!.AsObject()).Select(c => new Change<Relation>(
            (string)c["id"]!, c["before"].Deserialize<Relation>(Json.Options), c["after"].Deserialize<Relation>(Json.Options), (int)c["order"]!)).ToList();
        return new HistoryEvent("tx", new Transaction(0, (string?)o["label"] ?? "", entities, relations, o["at"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.UtcNow));
    }
}
