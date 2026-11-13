using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Host;

/// <summary>
/// What an external program keeps live with the engine (docs/plan-external-geometry.md, L2 and L3): the selection told with
/// what it is (<see cref="Selection"/>), named measures (<c>measure.define</c>, <c>measure.get</c>, <c>measure.remove</c>,
/// <c>measure.list</c>) recomputed after a change only when it touched what they read, and watches (<c>script.watch</c>):
/// a connection's, told by an event, and a project script's (kept on its <c>LapinScript</c>), which the hub runs again.
/// Each connection's measures and watches are its own and go with it; nothing here is in the model or in undo.
/// The hub calls it under its gate (the session is single-threaded); its own tables are locked for <see cref="Forget"/>.
/// </summary>
public sealed class LiveLinks(Session session)
{
    public const int MaxMeasures = 200;
    public const int MaxWatches = 100;
    public const string ScriptClass = "LapinScript";
    public const string WatchKey = "watch";

    sealed class LiveMeasure(string name)
    {
        public string Name { get; } = name;
        public string? Query { get; init; }
        public JsonObject? Args { get; init; }
        public string? Expr { get; init; }
        public string? Id { get; init; }
        public JsonNode? Value { get; set; }
        public string? Error { get; set; }
        /// <summary>The entities it reads: recomputed when a change touches one of them.</summary>
        public HashSet<string> Reads { get; set; } = [];
        /// <summary>A query naming no element: recomputed after every change.</summary>
        public bool Always { get; set; }
    }

    sealed record Watch(string Id, HashSet<string> Ids, List<string> Measures);

    sealed class Owner
    {
        public Dictionary<string, LiveMeasure> Measures { get; } = [];
        public Dictionary<string, Watch> Watches { get; } = [];
    }

    readonly object _sync = new();
    readonly Dictionary<object, Owner> _owners = new(ReferenceEqualityComparer.Instance);
    /// <summary>A project script's watched measures as last worked out, by script id (its first values are the baseline).</summary>
    readonly Dictionary<string, Dictionary<string, LiveMeasure>> _scripts = [];
    int _nextWatch;

    Owner Of(object owner)
    {
        lock (_sync)
        {
            if (!_owners.TryGetValue(owner, out var o)) _owners[owner] = o = new Owner();
            return o;
        }
    }

    /// <summary>The connection gone: its measures and watches with it.</summary>
    public void Forget(object owner) { lock (_sync) _owners.Remove(owner); }

    // ------------------------------------------------------------ measures

    /// <summary>
    /// <c>measure.define {name, query?, args?, expr?, id?}</c>: a catalogue query by name with its arguments (its result is
    /// the value), a formula over one element (<c>attr.&lt;field&gt;</c>, <c>qty.&lt;Quantity&gt;</c>, <c>bbox.xmin</c> …
    /// <c>bbox.zmax</c>), or neither: a built-in name (<see cref="Builtin"/>). Answers its value now.
    /// </summary>
    public JsonObject Define(object owner, JsonObject p)
    {
        var name = ((string?)p["name"])?.Trim() is { Length: > 0 and <= 300 } n ? n : throw new CommandException("A measure needs a name (at most 300 characters).");
        var query = ((string?)p["query"])?.Trim() is { Length: > 0 } q ? q : null;
        var expr = ((string?)p["expr"])?.Trim() is { Length: > 0 } x ? x : null;
        if (query != null && expr != null) throw new CommandException("A measure is a query or a formula, not both.");
        var id = (string?)p["id"];
        if (expr != null && (id == null || session.Model.Get(id) == null)) throw new CommandException("A formula measures one element: give its id.");
        if (query == null && expr == null && !IsBuiltin(name))
            throw new CommandException($"„{name}” is no built-in measure (storey.<name>.elevation, storey.<name>.height, element.<id>.bbox, wall.<id>.top): give a query or a formula.");
        var o = Of(owner);
        var m = new LiveMeasure(name) { Query = query, Args = (p["args"] as JsonObject)?.DeepClone() as JsonObject, Expr = expr, Id = id };
        lock (_sync)
        {
            if (!o.Measures.ContainsKey(name) && o.Measures.Count >= MaxMeasures) throw new CommandException($"A connection keeps at most {MaxMeasures} measures.");
            o.Measures[name] = m;
        }
        Compute(m);
        return Json(m);
    }

    /// <summary><c>measure.get {name}</c>: a measure of the connection's, else a built-in worked out now.</summary>
    public JsonObject Get(object owner, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new CommandException("Which measure? (name)");
        LiveMeasure? m;
        lock (_sync) Of(owner).Measures.TryGetValue(name, out m);
        if (m == null)
        {
            if (!IsBuiltin(name)) throw new CommandException($"There is no measure „{name}”.");
            m = new LiveMeasure(name);
        }
        Compute(m);
        return Json(m);
    }

    public JsonObject Remove(object owner, string? name)
    {
        bool had;
        lock (_sync) had = name != null && Of(owner).Measures.Remove(name);
        return new JsonObject { ["removed"] = had };
    }

    public JsonObject List(object owner)
    {
        lock (_sync)
            return new JsonObject { ["measures"] = new JsonArray([.. Of(owner).Measures.Values.Select(m => (JsonNode)Json(m))]), ["watches"] = new JsonArray([.. Of(owner).Watches.Values.Select(w => (JsonNode)WatchJson(w))]) };
    }

    static JsonObject Json(LiveMeasure m)
    {
        var o = new JsonObject { ["name"] = m.Name, ["value"] = m.Value?.DeepClone() };
        if (m.Error != null) o["error"] = m.Error;
        return o;
    }

    /// <summary>Whether a name is a built-in measure's.</summary>
    public static bool IsBuiltin(string name) =>
        (name.StartsWith("storey.", StringComparison.Ordinal) && (name.EndsWith(".elevation", StringComparison.Ordinal) || name.EndsWith(".height", StringComparison.Ordinal)) && name.Length > "storey..height".Length)
        || (name.StartsWith("element.", StringComparison.Ordinal) && name.EndsWith(".bbox", StringComparison.Ordinal) && name.Length > "element..bbox".Length)
        || (name.StartsWith("wall.", StringComparison.Ordinal) && name.EndsWith(".top", StringComparison.Ordinal) && name.Length > "wall..top".Length);

    /// <summary>A measure's value worked out now, with what it read; an error kept as its <see cref="LiveMeasure.Error"/>.</summary>
    void Compute(LiveMeasure m)
    {
        m.Error = null;
        m.Always = false;
        var reads = new HashSet<string>();
        try
        {
            if (m.Query != null)
            {
                var args = (JsonObject?)m.Args?.DeepClone() ?? [];
                foreach (var id in Args.Ids(args)) reads.Add(id);
                if ((string?)args["id"] is { } one) reads.Add(one);
                m.Always = reads.Count == 0;
                m.Value = session.Query(m.Query, args)?.DeepClone();
            }
            else if (m.Expr != null) m.Value = Formula(m.Expr, m.Id!, reads);
            else m.Value = Builtin(m.Name, reads);
        }
        catch (Exception ex) when (ex is CommandException or ExprException or ModelException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            m.Value = null;
            m.Error = ex.Message;
        }
        m.Reads = reads;
    }

    /// <summary>
    /// <c>storey.&lt;name or id&gt;.elevation</c> and <c>.height</c>, <c>element.&lt;id&gt;.bbox</c> (<c>[[xmin, ymin, zmin],
    /// [xmax, ymax, zmax]]</c> of its body as built) and <c>wall.&lt;id&gt;.top</c> (the highest point of its body).
    /// </summary>
    JsonNode? Builtin(string name, HashSet<string> reads)
    {
        if (name.StartsWith("storey.", StringComparison.Ordinal))
        {
            var height = name.EndsWith(".height", StringComparison.Ordinal);
            var key = name["storey.".Length..^(height ? ".height".Length : ".elevation".Length)];
            var storey = session.Model.Get(key) is { Class: IfcClass.BuildingStorey } byId ? byId
                : Spatial.Storeys(session.Model).FirstOrDefault(s => string.Equals(s.Name, key, StringComparison.OrdinalIgnoreCase))
                  ?? throw new CommandException($"There is no storey „{key}”.");
            reads.Add(storey.Id);
            return Math.Round(storey.Num(height ? "height" : "elevation"), 9);
        }
        if (name.StartsWith("element.", StringComparison.Ordinal))
        {
            var id = name["element.".Length..^".bbox".Length];
            reads.Add(id);
            var (lo, hi) = Box(id) ?? throw new CommandException($"{id} has no body.");
            return new JsonArray(new JsonArray(R(lo.X), R(lo.Y), R(lo.Z)), new JsonArray(R(hi.X), R(hi.Y), R(hi.Z)));
        }
        if (name.StartsWith("wall.", StringComparison.Ordinal))
        {
            var id = name["wall.".Length..^".top".Length];
            reads.Add(id);
            return R((Box(id) ?? throw new CommandException($"{id} has no body.")).Max.Z);
        }
        throw new CommandException($"There is no measure „{name}”.");
    }

    double Formula(string expr, string id, HashSet<string> reads)
    {
        reads.Add(id);
        var e = session.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
        (Vec3 Min, Vec3 Max)? box = null;
        IReadOnlyList<QuantitySet>? qty = null;
        return Expr.Eval(expr, n =>
        {
            if (n.StartsWith("attr.", StringComparison.Ordinal)) return Lapin.Model.Json.Number(e.Attrs[n[5..]]);
            if (n.StartsWith("qty.", StringComparison.Ordinal))
            {
                qty ??= session.Quantities(id);
                return qty.SelectMany(s => s.Quantities).FirstOrDefault(q => q.Name == n[4..])?.Value;
            }
            if (n.StartsWith("bbox.", StringComparison.Ordinal))
            {
                box ??= Box(id);
                if (box is not { } b) return null;
                return n[5..] switch
                {
                    "xmin" => b.Min.X, "ymin" => b.Min.Y, "zmin" => b.Min.Z, "xmax" => b.Max.X, "ymax" => b.Max.Y, "zmax" => b.Max.Z, _ => null,
                };
            }
            return null;
        });
    }

    static double R(double v) => Math.Round(v, 6);

    /// <summary>The box of an element's body as built (every part), or null when it has none.</summary>
    (Vec3 Min, Vec3 Max)? Box(string id)
    {
        if (session.Model.Get(id) == null || session.Geometry(id) is not { } g) return null;
        var boxes = g.Parts.Select(p => Lapin.Geometry.Measure.Bounds(p.Solid)).OfType<(Vec3 Min, Vec3 Max)>().ToList();
        if (boxes.Count == 0) return null;
        return (new Vec3(boxes.Min(b => b.Min.X), boxes.Min(b => b.Min.Y), boxes.Min(b => b.Min.Z)), new Vec3(boxes.Max(b => b.Max.X), boxes.Max(b => b.Max.Y), boxes.Max(b => b.Max.Z)));
    }

    // ------------------------------------------------------------ the selection

    /// <summary>
    /// The window's selection as an external program wants it: each element's id (its GlobalId), class, name, storey,
    /// assembly path (outermost first, itself last) and the box of its body.
    /// </summary>
    public JsonObject Selection(IReadOnlyList<string> ids)
    {
        var items = new JsonArray();
        foreach (var id in ids.Take(10_000))
        {
            if (session.Model.Get(id) is not { } e) continue;
            var o = new JsonObject
            {
                ["id"] = e.Id, ["globalId"] = e.Id, ["ifcClass"] = e.Class, ["name"] = e.Name,
                ["storey"] = Spatial.StoreyOf(session.Model, e.Id)?.Id,
                ["path"] = new JsonArray([.. Assemblies.PathOf(session.Model, e.Id).Select(x => (JsonNode)x)]),
            };
            if (Box(e.Id) is var (lo, hi)) o["bbox"] = new JsonArray(new JsonArray(R(lo.X), R(lo.Y), R(lo.Z)), new JsonArray(R(hi.X), R(hi.Y), R(hi.Z)));
            items.Add((JsonNode)o);
        }
        return new JsonObject { ["ids"] = new JsonArray([.. ids.Take(100_000).Select(x => (JsonNode)x)]), ["items"] = items };
    }

    // ------------------------------------------------------------ watches

    /// <summary>
    /// <c>script.watch {ids?, measures?}</c> for a connection: told by a <c>watch</c> event when one of the elements changes
    /// (its attributes, or its body rebuilt) or one of the measures takes another value. A measure named that the
    /// connection has not defined is a built-in, defined for it. Answers the watch's id.
    /// </summary>
    public JsonObject AddWatch(object owner, JsonObject p)
    {
        var ids = (p["ids"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>().Distinct().ToList();
        foreach (var id in ids) if (session.Model.Get(id) == null) throw new CommandException($"There is no {id} to watch.");
        var measures = (p["measures"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>().Distinct().ToList();
        if (ids.Count == 0 && measures.Count == 0) throw new CommandException("Watch what? (ids or measures)");
        var o = Of(owner);
        foreach (var m in measures)
        {
            bool known;
            lock (_sync) known = o.Measures.ContainsKey(m);
            if (!known) Define(owner, new JsonObject { ["name"] = m });
        }
        Watch w;
        lock (_sync)
        {
            if (o.Watches.Count >= MaxWatches) throw new CommandException($"A connection keeps at most {MaxWatches} watches.");
            w = new Watch($"w{++_nextWatch}", [.. ids], measures);
            o.Watches[w.Id] = w;
        }
        return WatchJson(w);
    }

    public JsonObject Unwatch(object owner, string? id)
    {
        bool had;
        lock (_sync) had = id != null && Of(owner).Watches.Remove(id);
        return new JsonObject { ["removed"] = had };
    }

    static JsonObject WatchJson(Watch w) => new()
    {
        ["watch"] = w.Id, ["ids"] = new JsonArray([.. w.Ids.Select(x => (JsonNode)x)]), ["measures"] = new JsonArray([.. w.Measures.Select(x => (JsonNode)x)]),
    };

    // ------------------------------------------------------------ after a change

    /// <summary>An event for one connection: its name and data.</summary>
    public sealed record Note(object Owner, string Event, JsonObject Data);

    /// <summary>
    /// After a change (<paramref name="diff"/>, made by <paramref name="author"/>'s connection, null when none): the
    /// measures whose reads it touched recomputed, a <c>measure</c> event for each whose value changed; a <c>watch</c>
    /// event for each connection's watch it hit (never for its own change); and the project scripts to run again (none
    /// when a script made the change, <paramref name="byScript"/>, so a script's edits never set off a script).
    /// </summary>
    public (List<Note> Notes, List<string> Scripts) Changed(Diff diff, object? author, bool byScript)
    {
        var notes = new List<Note>();
        var scripts = new List<string>();
        var all = diff.Reset;
        var touched = new HashSet<string>(StringComparer.Ordinal);
        if (!all)
        {
            foreach (var n in diff.Nodes) touched.Add(n.Id);
            touched.UnionWith(diff.RemovedNodes);
            foreach (var r in diff.Render) touched.Add(r.NodeId);
            touched.UnionWith(diff.RenderRemoved);
            if (touched.Count == 0) return (notes, scripts);
        }
        bool Hit(LiveMeasure m) => all || m.Always || m.Reads.Overlaps(touched);
        List<(object Owner, Owner Data)> owners;
        lock (_sync) owners = [.. _owners.Select(kv => (kv.Key, kv.Value))];
        foreach (var (key, o) in owners)
        {
            List<LiveMeasure> measures;
            List<Watch> watches;
            lock (_sync) { measures = [.. o.Measures.Values]; watches = [.. o.Watches.Values]; }
            var changed = new Dictionary<string, LiveMeasure>();
            foreach (var m in measures.Where(Hit))
            {
                var (was, wasError) = (m.Value?.DeepClone(), m.Error);
                Compute(m);
                if (JsonNode.DeepEquals(was, m.Value) && wasError == m.Error) continue;
                changed[m.Name] = m;
                var data = Json(m);
                data["was"] = was;
                notes.Add(new Note(key, "measure", data));
            }
            if (ReferenceEquals(key, author)) continue;
            foreach (var w in watches)
            {
                var ids = all ? w.Ids.ToList() : w.Ids.Where(touched.Contains).ToList();
                var hit = w.Measures.Where(changed.ContainsKey).ToList();
                if (ids.Count == 0 && hit.Count == 0) continue;
                var values = new JsonObject();
                foreach (var name in hit) values[name] = changed[name].Value?.DeepClone();
                notes.Add(new Note(key, "watch", new JsonObject
                {
                    ["watch"] = w.Id, ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["measures"] = values,
                }));
            }
        }
        if (!byScript) scripts.AddRange(ScriptsHit(all, touched));
        return (notes, scripts);
    }

    /// <summary>The project's scripts whose watch the change hit: an element it names touched, or a measure it names changed.</summary>
    IEnumerable<string> ScriptsHit(bool all, HashSet<string> touched)
    {
        var hit = new List<string>();
        var seen = new HashSet<string>();
        foreach (var s in session.Model.OfClass(ScriptClass).ToList())
        {
            if (s.Attrs[WatchKey] is not JsonObject w) continue;
            seen.Add(s.Id);
            var ids = (w["ids"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>();
            var names = (w["measures"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>().Where(IsBuiltin).ToList();
            Dictionary<string, LiveMeasure> mine;
            lock (_sync) if (!_scripts.TryGetValue(s.Id, out mine!)) _scripts[s.Id] = mine = [];
            var fired = !all && ids.Any(touched.Contains);
            foreach (var name in names)
            {
                if (!mine.TryGetValue(name, out var m))
                {
                    // The first time: its value now is the baseline (the change that set the watch fires nothing).
                    mine[name] = m = new LiveMeasure(name);
                    Compute(m);
                    continue;
                }
                if (!all && !m.Reads.Overlaps(touched)) continue;
                var (was, wasError) = (m.Value?.DeepClone(), m.Error);
                Compute(m);
                if (!JsonNode.DeepEquals(was, m.Value) || wasError != m.Error) fired = true;
            }
            if (fired) hit.Add(s.Id);
        }
        lock (_sync) foreach (var gone in _scripts.Keys.Where(k => !seen.Contains(k)).ToList()) _scripts.Remove(gone);
        return hit;
    }
}
