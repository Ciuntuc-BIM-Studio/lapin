using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>
/// The recovery journal: every edit of the open project, appended as it
/// happens to <c>&lt;user-dir&gt;/recovery/&lt;session&gt;.jsonl</c>, so a crash
/// loses nothing. The first line says what the project started from:
/// <code>
/// {"op":"start","at":…,"product":…,"path":"/x.lapin","sha256":"…"}            an opened (or saved) file
/// {"op":"start","at":…,"product":…,"path":null,"base":{"entities":[…],"relations":[…]}}   a new project
/// {"op":"tx","label":…,"entities":[{id,before,after,order}],"relations":[…]}
/// {"op":"import","label":…,"source":{"import":"/x.ifc","sha256":…,"mode":…,"seed":…}}   an import from a file, done again on recovery (its every entity, a big model's bodies among them, is not copied)
/// {"op":"undo"}  {"op":"redo"}
/// </code>
/// Saving starts the journal again (the file now holds everything). A
/// journal with no edit is not worth keeping and is removed. The open journal
/// is held exclusively: another Lapin (or recovery.list) sees it is in use
/// and leaves it alone. Where the journals live is the host's
/// (<see cref="IJournalStore"/>): files on the desktop, the browser's own storage there.
/// </summary>
public sealed class Recovery : IRecoveryJournal, IDisposable
{
    readonly IJournalStore _store;
    readonly Session _session;
    string? _file;
    IJournalWriter? _writer;
    int _edits;

    /// <summary>A journal without edits younger than this may be one a Lapin is just opening: left alone.</summary>
    static readonly TimeSpan Grace = TimeSpan.FromSeconds(10);

    /// <summary>The journals in <c>&lt;userDir&gt;/recovery</c>, as files (the desktop).</summary>
    public Recovery(string userDir, Session session) : this(new FileJournalStore(System.IO.Path.Combine(userDir, "recovery")), session) { }

    public Recovery(IJournalStore store, Session session)
    {
        _store = store;
        _session = session;
        session.HistoryChanged += OnHistory;
        Start();
    }

    public string? CurrentFile => _file;

    void OnHistory(HistoryEvent e)
    {
        switch (e.Kind)
        {
            case "saved": Start(keep: false); return;        // the file holds everything the journal held
            case "start": Start(keep: _edits > 0); return;   // another project over unsaved edits: their journal is the only trace of them
        }
        var line = e.Kind == "tx" ? (_session.EditSource is { } source ? ImportLine(e.Transaction!, source) : TxLine(e.Transaction!)) : new JsonObject { ["op"] = e.Kind, ["author"] = e.Author };
        Write(line);
        _edits++;
    }

    /// <summary>A new journal for the project as it is now; the previous one goes, unless <paramref name="keep"/> says it holds work saved nowhere else.</summary>
    void Start(bool keep = false)
    {
        Close(delete: !keep);
        (_file, _writer) = _store.Create($"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.jsonl");
        _edits = 0;
        var path = _session.Path;
        var start = new JsonObject { ["op"] = "start", ["at"] = DateTimeOffset.UtcNow, ["product"] = _session.Product.Id, ["path"] = path };
        if (path != null && _store.ProjectHash(path) is { } sha) start["sha256"] = sha;
        else
        {
            start["path"] = null;
            start["base"] = new JsonObject
            {
                ["entities"] = new JsonArray(_session.Model.Entities.Select(e => (JsonNode)EntityJson(e)).ToArray()),
                ["relations"] = JsonSerializer.SerializeToNode(_session.Model.Relations.ToList(), ModelJson.Default.ListRelation),
                ["classes"] = JsonSerializer.SerializeToNode(_session.Model.Entities.Select(e => e.Class).Distinct().ToDictionary(c => c, _session.ModuleOf), ModelJson.Default.DictionaryStringString),
            };
        }
        Write(start);
    }

    void Write(JsonObject line)
    {
        _writer?.WriteLine(line.ToJsonString(Json.Options));
    }

    void Close(bool delete)
    {
        _writer?.Dispose();
        _writer = null;
        if (delete && _file != null) _store.Delete(_file);
        _file = null;
    }

    /// <summary>Ends cleanly: with nothing unsaved, the journal goes; with unsaved edits, it stays for next time.</summary>
    public void Dispose()
    {
        _session.HistoryChanged -= OnHistory;
        Close(delete: _edits == 0 || !_session.History.Dirty);
    }

    // ------------------------------------------------------------ bringing work back

    /// <summary>The journals of earlier sessions with edits in them, newest first. Empty ones are removed; those of running sessions are left alone.</summary>
    public IReadOnlyList<RecoverableWork> List()
    {
        var o = new List<RecoverableWork>();
        foreach (var file in _store.Journals())
        {
            if (file == _file || _store.InUse(file)) continue;
            try
            {
                var lines = _store.ReadLines(file).Where(l => l.Length > 0).ToList();
                var start = lines.Count > 0 ? JsonNode.Parse(lines[0]) as JsonObject : null;
                var edits = lines.Skip(1).Select(l => JsonNode.Parse(l) as JsonObject).OfType<JsonObject>().ToList();
                if (start == null || edits.Count == 0)
                {
                    if (DateTime.UtcNow - _store.LastWrite(file) > Grace) _store.Delete(file);
                    continue;
                }
                o.Add(new RecoverableWork(file, (string?)start["path"], start["at"]?.GetValue<DateTimeOffset>() ?? _store.LastWrite(file),
                    edits.Count, (string?)edits.LastOrDefault(e => (string?)e["op"] == "tx")?["label"]));
            }
            catch (Exception ex) when (ex is JsonException or IOException or FormatException or InvalidOperationException)
            {
                // A journal cut off mid-line by the crash still counts: its complete lines are read on restore.
                o.Add(new RecoverableWork(file, null, _store.LastWrite(file), 0, null));
            }
        }
        return o.OrderByDescending(w => w.At).ToList();
    }

    /// <summary>Brings the work of <paramref name="file"/> back into the session, which goes on journalling in it.</summary>
    public Diff Restore(string file)
    {
        if (!_store.Holds(file)) throw new CommandException("Not a recovery journal.");
        var lines = _store.ReadLines(file).Where(l => l.Length > 0).ToList();
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
            var now = _store.ProjectHash(path) ?? throw new CommandException($"The project the work was done on is gone: {path}.");
            if ((string?)start["sha256"] is { } sha && now != sha) throw new CommandException($"{System.IO.Path.GetFileName(path)} changed since: the unsaved work no longer fits it.");
            var content = _store.ReadProject(path);
            (entities, relations, classes) = (content.Entities, content.Relations, content.Classes);
        }
        else
        {
            var b = start["base"] as JsonObject ?? throw new InvalidDataException("The journal has no starting model.");
            entities = (b["entities"] as JsonArray ?? []).Select(n => ReadEntity(n!.AsObject())).ToList();
            relations = b["relations"].Deserialize(ModelJson.Default.ListRelation) ?? [];
            classes = b["classes"].Deserialize(ModelJson.Default.DictionaryStringString);
        }
        var edits = parsed.Skip(1).Select(ReadEvent).ToList();
        var diff = _session.Recover(entities, relations, path, classes, edits);

        // Go on in that journal: a second crash still has everything. Unsaved work of this session's own stays in its journal.
        Close(delete: _edits == 0);
        _file = file;
        _writer = _store.Append(file);
        _edits = edits.Count;
        return diff;
    }

    public void Discard(string file)
    {
        if (!_store.Holds(file)) throw new CommandException("Not a recovery journal.");
        if (file == _file || _store.InUse(file)) throw new CommandException("That journal is in use.");
        _store.Delete(file);
    }

    // ------------------------------------------------------------ lines

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
        ["seq"] = t.Seq,
        ["author"] = t.Author == History.Me ? null : t.Author,
        ["reverts"] = t.Reverts,
        ["redoes"] = t.Redoes,
        ["entities"] = new JsonArray(t.Entities.Select(c => (JsonNode)new JsonObject
        {
            ["id"] = c.Id, ["order"] = c.Order,
            ["before"] = c.Before == null ? null : EntityJson(c.Before),
            ["after"] = c.After == null ? null : EntityJson(c.After),
        }).ToArray()),
        ["relations"] = new JsonArray(t.Relations.Select(c => (JsonNode)new JsonObject
        {
            ["id"] = c.Id, ["order"] = c.Order,
            ["before"] = c.Before == null ? null : JsonSerializer.SerializeToNode(c.Before, ModelJson.Default.Relation),
            ["after"] = c.After == null ? null : JsonSerializer.SerializeToNode(c.After, ModelJson.Default.Relation),
        }).ToArray()),
    };

    /// <summary>An import recorded by its source: the file and its hash, the mode and the seed of its ids.</summary>
    static JsonObject ImportLine(Transaction t, JsonObject source) => new()
    {
        ["op"] = "import", ["label"] = t.Label, ["at"] = t.At, ["seq"] = t.Seq, ["author"] = t.Author == History.Me ? null : t.Author, ["source"] = source.DeepClone(),
    };

    /// <summary>The import a journal line records, done again: the same file (its hash checked), the same mode, the same ids.</summary>
    static void Reimport(Session s, JsonObject source)
    {
        var path = (string?)source["import"] ?? throw new InvalidDataException("An import without its file.");
        if (!File.Exists(path)) throw new CommandException($"The file that was imported is gone: {path}.");
        var bytes = File.ReadAllBytes(path);
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        if ((string?)source["sha256"] is { } sha && sha != hash) throw new CommandException($"{System.IO.Path.GetFileName(path)} changed since it was imported: the unsaved work cannot be brought back.");
        var mode = (string?)source["mode"] ?? "auto";
        var options = new IfcImportOptions(mode == "link" ? "auto" : mode);
        using (s.SeededIds((long?)source["seed"] ?? 0))
        {
            if (mode == "link") IfcImport.Link(s, bytes, System.IO.Path.GetFileName(path), path, options, out _);
            else IfcImport.Run(s, bytes, System.IO.Path.GetFileName(path), options, out _);
        }
    }

    static HistoryEvent ReadEvent(JsonObject o)
    {
        var op = (string?)o["op"] ?? "";
        if (op == "import")
        {
            var source = o["source"] as JsonObject ?? throw new InvalidDataException("An import without its source.");
            return new HistoryEvent("replay", null, (string?)o["author"], s => Reimport(s, source));
        }
        if (op != "tx") return new HistoryEvent(op, null, (string?)o["author"]);
        var entities = (o["entities"] as JsonArray ?? []).Select(n => n!.AsObject()).Select(c => new Change<Entity>(
            (string)c["id"]!, c["before"] is JsonObject b ? ReadEntity(b) : null, c["after"] is JsonObject a ? ReadEntity(a) : null, (int)c["order"]!)).ToList();
        var relations = (o["relations"] as JsonArray ?? []).Select(n => n!.AsObject()).Select(c => new Change<Relation>(
            (string)c["id"]!, c["before"].Deserialize(ModelJson.Default.Relation), c["after"].Deserialize(ModelJson.Default.Relation), (int)c["order"]!)).ToList();
        return new HistoryEvent("tx", new Transaction((long?)o["seq"] ?? 0, (string?)o["label"] ?? "", entities, relations, o["at"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.UtcNow,
            (string?)o["author"] ?? History.Me, (long?)o["reverts"], (long?)o["redoes"]));
    }
}
