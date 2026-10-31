using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Ifc;

/// <summary>
/// A foreign element's IFC carried through Lapin (<see cref="IfcSource"/>): read from its file as the file wrote it,
/// and written back by the export, so what Lapin does not model leaves as it came.
/// <para>
/// An entity is kept as a snapshot, <c>{"t": TYPE, "a": [...]}</c>, each attribute either its STEP text
/// (<c>IFCPRESSUREMEASURE(600000.123456)</c>, <c>'P-001'</c>, <c>.T.</c>), a snapshot of the entity it refers to,
/// or a list of them. Only what describes the element follows a reference (properties, quantities, units,
/// materials, profiles); a reference to anything else (an owner history, a placement, a representation map)
/// is left empty. Read from IFC2X3, an entity IFC4 gave more attributes is padded to IFC4's count.
/// </para>
/// </summary>
public static class IfcCarry
{
    /// <summary>What a record holds: its snapshot, and for a system the GlobalId of its IfcRelServicesBuildings.</summary>
    public const string Snap = "snap", Services = "services";

    /// <summary>The relationships of the file, by the element (or port, or system) they concern.</summary>
    public sealed class Index
    {
        public readonly Dictionary<int, List<(string? Rel, int Def)>> Defs = [];
        public readonly Dictionary<int, (string? Rel, int Type)> TypeOf = [];
        public readonly Dictionary<int, (string? Rel, int Material)> MaterialOf = [];
        public readonly Dictionary<int, List<(string? Rel, int Port)>> Ports = [];
        public readonly Dictionary<int, List<(string? Rel, int Other)>> Connects = [];
        public readonly Dictionary<int, List<(string? Rel, int Group)>> Groups = [];
        public readonly Dictionary<int, string?> ServicesOf = [];
        /// <summary>The records made so far, by the file's entity: a type many elements share is read once.</summary>
        internal readonly System.Collections.Concurrent.ConcurrentDictionary<int, Shared?> Records = [];

        public Index(StepFile f)
        {
            foreach (var r in f.OfType("IFCRELDEFINESBYPROPERTIES"))
                if (r[5] is StepRef d) foreach (var p in Refs(r[4])) Add(Defs, p, (Gid(r), d.Id));
            foreach (var r in f.OfType("IFCRELDEFINESBYTYPE"))
                if (r[5] is StepRef t) foreach (var p in Refs(r[4])) TypeOf[p] = (Gid(r), t.Id);
            foreach (var r in f.OfType("IFCRELASSOCIATESMATERIAL"))
                if (r[5] is StepRef m) foreach (var p in Refs(r[4])) MaterialOf[p] = (Gid(r), m.Id);
            // Ports: nested in their element (IFC4), or connected to it (IFC2X3).
            foreach (var r in f.OfType("IFCRELNESTS"))
                if (r[4] is StepRef whole)
                    foreach (var p in Refs(r[5])) if (f.Entities.GetValueOrDefault(p) is { Type: "IFCDISTRIBUTIONPORT" }) Add(Ports, whole.Id, (Gid(r), p));
            foreach (var r in f.OfType("IFCRELCONNECTSPORTTOELEMENT"))
                if (r[4] is StepRef port && r[5] is StepRef element) Add(Ports, element.Id, (Gid(r), port.Id));
            foreach (var r in f.OfType("IFCRELCONNECTSPORTS"))
                if (r[4] is StepRef a && r[5] is StepRef b) Add(Connects, a.Id, (Gid(r), b.Id));
            foreach (var r in f.OfType("IFCRELASSIGNSTOGROUP"))
                if (r[6] is StepRef g) foreach (var p in Refs(r[4])) Add(Groups, p, (Gid(r), g.Id));
            foreach (var r in f.OfType("IFCRELSERVICESBUILDINGS"))
                if (r[4] is StepRef sys) ServicesOf[sys.Id] = Gid(r);
        }

        static void Add<T>(Dictionary<int, List<T>> d, int key, T value) => (d.TryGetValue(key, out var l) ? l : d[key] = []).Add(value);
    }

    /// <summary>What is shared between elements, to be put in the model once as a record: its id, and the snapshot (and services) it holds.</summary>
    public sealed record Shared(string Id, JsonObject Attrs);

    /// <summary>
    /// What an element's file said of it: its own (<paramref name="own"/>, kept on the element) and what it shares
    /// with others (records, by id). Its intent set, Lapin's own, is not carried: an export writes a new one.
    /// </summary>
    /// <param name="keep">Whether a property or quantity set of this name is carried (the import's <see cref="IfcImportOptions.SkipSets"/>).</param>
    /// <param name="numbers">A number written as text carried as the number it is (<see cref="IfcValueText"/>).</param>
    public static (JsonObject Own, List<Shared> Shared) Capture(StepFile f, Index ix, StepEntity e, string seed, Func<string?, bool>? keep = null, bool numbers = false)
    {
        var shared = new List<Shared>();
        var own = new JsonObject { ["e"] = Snapshot(f, e.Id, root: true) };
        var defs = new JsonArray();
        foreach (var (rel, def) in ix.Defs.GetValueOrDefault(e.Id) ?? [])
        {
            if (f.Entities.GetValueOrDefault(def) is not { } set || set.Type == "IFCPROPERTYSET" && IfcExport.IsIntentSet(IfcModel.Str(set[2]))) continue;
            if (keep != null && !keep(IfcModel.Str(set[2]))) continue;
            if (Snapshot(f, def, root: true, numbers: numbers) is { } snap) defs.Add((JsonNode)new JsonObject { ["rel"] = rel, ["d"] = snap });
        }
        if (defs.Count > 0) own["defs"] = defs;
        if (ix.TypeOf.TryGetValue(e.Id, out var type) && Record(f, ix, type.Type, seed, "type", shared) is { } typeId)
            own["type"] = new JsonObject { ["rel"] = type.Rel, ["record"] = typeId };
        if (ix.MaterialOf.TryGetValue(e.Id, out var mat) && Record(f, ix, mat.Material, seed, "material", shared) is { } matId)
            own["material"] = new JsonObject { ["rel"] = mat.Rel, ["record"] = matId };
        var ports = new JsonArray();
        var connects = new JsonArray();
        foreach (var (rel, port) in ix.Ports.GetValueOrDefault(e.Id) ?? [])
        {
            if (Snapshot(f, port, root: true) is not { } snap) continue;
            ports.Add((JsonNode)new JsonObject { ["rel"] = rel, ["p"] = snap });
            foreach (var (crel, other) in ix.Connects.GetValueOrDefault(port) ?? [])
                if (GidOf(f, other) is { } og && GidOf(f, port) is { } pg) connects.Add((JsonNode)new JsonObject { ["rel"] = crel, ["a"] = pg, ["b"] = og });
        }
        if (ports.Count > 0) own["ports"] = ports;
        if (connects.Count > 0) own["connects"] = connects;
        var groups = new JsonArray();
        foreach (var (rel, group) in ix.Groups.GetValueOrDefault(e.Id) ?? [])
            if (Record(f, ix, group, seed, "group", shared, ix.ServicesOf.GetValueOrDefault(group)) is { } gid) groups.Add((JsonNode)new JsonObject { ["rel"] = rel, ["record"] = gid });
        if (groups.Count > 0) own["groups"] = groups;
        return (own, shared);
    }

    /// <summary>A shared entity as a record: its id from the file and what it is (its GlobalId, or its content), the same at every import.</summary>
    static string? Record(StepFile f, Index ix, int id, string seed, string kind, List<Shared> into, string? services = null)
    {
        var made = ix.Records.GetOrAdd(id, _ =>
        {
            if (Snapshot(f, id, root: true) is not { } snap) return null;
            var key = GidOf(f, id) ?? snap.ToJsonString();
            var attrs = new JsonObject { [Snap] = snap };
            if (services != null) attrs[Services] = services;
            return new Shared(Step.Guid(seed, $"ifc-record\n{kind}\n{key}"), attrs);
        });
        if (made == null) return null;
        into.Add(made);
        return made.Id;
    }

    static string? Gid(StepEntity r) => r[0] is string { Length: 22 } g && GlobalId.IsValid(g) ? g : null;
    static string? GidOf(StepFile f, int id) => f.Entities.GetValueOrDefault(id) is { } e ? Gid(e) : null;
    static IEnumerable<int> Refs(object? v) => v is List<object?> l ? l.OfType<StepRef>().Select(r => r.Id) : v is StepRef r1 ? [r1.Id] : [];

    /// <summary>IFC4's attribute counts for what IFC2X3 wrote with fewer.</summary>
    static readonly Dictionary<string, int> Ifc4Counts = new()
    {
        ["IFCMATERIAL"] = 3, ["IFCMATERIALLAYER"] = 7, ["IFCMATERIALLAYERSET"] = 3, ["IFCMATERIALLAYERSETUSAGE"] = 5,
        ["IFCDISTRIBUTIONPORT"] = 10, ["IFCPROPERTYBOUNDEDVALUE"] = 6, ["IFCPROPERTYTABLEVALUE"] = 8,
        ["IFCQUANTITYLENGTH"] = 5, ["IFCQUANTITYAREA"] = 5, ["IFCQUANTITYVOLUME"] = 5, ["IFCQUANTITYCOUNT"] = 5, ["IFCQUANTITYWEIGHT"] = 5, ["IFCQUANTITYTIME"] = 5,
    };

    /// <summary>IFC2X3's entities IFC4 has no more: what was one of them is written as Lapin would write it.</summary>
    static readonly HashSet<string> Ifc4Lacks = ["IFCELECTRICALELEMENT", "IFCEQUIPMENTELEMENT", "IFCELECTRICDISTRIBUTIONPOINT", "IFCELECTRICHEATERTYPE", "IFCGASTERMINALTYPE", "IFCELECTRICALBASEPROPERTIES"];

    /// <summary>What a reference is followed into: what describes the element, never its placement or geometry.</summary>
    static bool Follows(string type) =>
        type.StartsWith("IFCPROPERTY", StringComparison.Ordinal) || type.StartsWith("IFCQUANTITY", StringComparison.Ordinal)
        || type is "IFCCOMPLEXPROPERTY" or "IFCPHYSICALCOMPLEXQUANTITY" or "IFCELEMENTQUANTITY"
        || type.EndsWith("UNIT", StringComparison.Ordinal) || type is "IFCDERIVEDUNITELEMENT" or "IFCDIMENSIONALEXPONENTS" or "IFCMEASUREWITHUNIT"
        || type.StartsWith("IFCMATERIAL", StringComparison.Ordinal)
        || type.EndsWith("PROFILEDEF", StringComparison.Ordinal) || type is "IFCCARTESIANPOINT" or "IFCDIRECTION" or "IFCAXIS2PLACEMENT2D" or "IFCPOLYLINE";

    /// <summary>An entity as the file wrote it; null when it is not there (or, not the root, not something to follow).</summary>
    public static JsonObject? Snapshot(StepFile f, int id, bool root = false, int depth = 0, bool numbers = false)
    {
        if (depth > 16 || f.Entities.GetValueOrDefault(id) is not { } e || !root && !Follows(e.Type) || Ifc4Lacks.Contains(e.Type)) return null;
        var a = new JsonArray();
        for (var i = 0; i < e.Args.Count; i++)
            a.Add(e.Type == "IFCSIUNIT" && i == 0 ? (JsonNode)"*"
                : numbers && i == 2 && e.Type == "IFCPROPERTYSINGLEVALUE" && e.Args[i] is StepTyped { Value: string txt } && IfcValueText.Parse(txt) is { } n ? (JsonNode)n.Step
                : Arg(f, e.Args[i], depth, numbers));
        if (f.Schema.StartsWith("IFC2X3", StringComparison.OrdinalIgnoreCase))
        {
            // An element IFC4 gave a PredefinedType (a door and a window three attributes more), and what IFC4 describes more fully.
            var n = Ifc4Counts.TryGetValue(e.Type, out var c) ? c
                : root && e.Type is "IFCDOOR" or "IFCWINDOW" && a.Count == 10 ? 13
                : root && a.Count == 8 && IfcExport.HasPredefined(e.Type) ? 9 : 0;
            while (a.Count < n) a.Add((JsonNode)"$");
        }
        return new JsonObject { ["t"] = e.Type, ["a"] = a };
    }

    static JsonNode Arg(StepFile f, object? v, int depth, bool numbers = false)
    {
        if (v is StepRef r) return (JsonNode?)Snapshot(f, r.Id, depth: depth + 1, numbers: numbers) ?? "$";
        if (v is List<object?> l && l.Any(x => x is StepRef))
        {
            var items = l.Select(x => x is StepRef xr ? Snapshot(f, xr.Id, depth: depth + 1, numbers: numbers) : null).ToList();
            return items.All(x => x != null) ? new JsonArray([.. items]) : "$";
        }
        return Text(v);
    }

    /// <summary>A value in STEP form, as it was read.</summary>
    public static string Text(object? v) => v switch
    {
        null => Step.Null,
        string s => Step.S(s),
        double d => Real(d),
        long l => l.ToString(CultureInfo.InvariantCulture),
        StepEnum e => Step.E(e.Value),
        StepTyped t => $"{t.Type}({Text(t.Value)})",
        List<object?> l => Step.L(l.Select(Text)),
        _ => Step.Null,
    };

    /// <summary>A real exactly as read (the shortest text that reads back the same), with the point STEP asks for.</summary>
    static string Real(double d)
    {
        if (!double.IsFinite(d)) return "0.";
        var t = d.ToString("R", CultureInfo.InvariantCulture);
        if (t.Contains('.')) return t;
        var e = t.IndexOfAny(['E', 'e']);
        return e < 0 ? t + "." : t[..e] + "." + t[e..];
    }

    /// <summary>
    /// The carried IFC written into an export: entities written once (a property, a unit, a type the same
    /// as one already written is that one), each GlobalId once (an element copied in Lapin carries its
    /// original's: the copy's sets get ids of their own), the relationships gathered by their GlobalId.
    /// </summary>
    public sealed class Writer(Step f)
    {
        readonly Dictionary<string, string> written = [];
        readonly Dictionary<string, string> gidContent = [];
        readonly Dictionary<string, string> ports = [];
        readonly Dictionary<(string Class, string Gid), (string Relating, List<string> Related)> rels = [];
        readonly List<(string Gid, string A, string B)> connects = [];
        readonly Dictionary<string, string?> services = [];

        /// <summary>A snapshot written (or found written); <paramref name="owner"/> seeds a new GlobalId for a root whose id is taken by other content.</summary>
        public string Emit(JsonObject snap, string owner, Func<int, string?>? replace = null)
        {
            var type = (string)snap["t"]!;
            var a = (snap["a"] as JsonArray) ?? [];
            var args = new string[a.Count];
            for (var i = 0; i < a.Count; i++) args[i] = replace?.Invoke(i) ?? ArgText(a[i], owner);
            var gid = args.Length > 0 && args[0].Length == 24 && args[0][0] == '\'' ? args[0][1..^1] : null;
            var content = type + "(" + string.Join(",", args) + ")";
            if (written.TryGetValue(content, out var known)) return known;
            if (gid != null && GlobalId.IsValid(gid))
            {
                var body = type + "(" + string.Join(",", args.Skip(1)) + ")";
                if (gidContent.TryGetValue(gid, out var other) && other != body)
                {
                    args[0] = Step.S(Step.Guid(owner, gid));
                    content = type + "(" + string.Join(",", args) + ")";
                    if (written.TryGetValue(content, out known)) return known;
                }
                else gidContent[gid] = body;
            }
            return written[content] = f.Add(type, args);
        }

        string ArgText(JsonNode? n, string owner) => n switch
        {
            JsonObject o => Emit(o, owner),
            JsonArray l => Step.L(l.OfType<JsonObject>().Select(x => Emit(x, owner))),
            _ => (string?)n ?? Step.Null,
        };

        /// <summary>The element's own attribute <paramref name="i"/> as the file wrote it ($ when it had none).</summary>
        public static string? ArgOf(JsonObject own, int i) =>
            own["e"]?["a"] is JsonArray a && i < a.Count && a[i] is JsonValue v ? (string?)v : null;

        /// <summary>The element's IFC class as the file wrote it (upper case), and how many attributes it had.</summary>
        public static (string Type, int Count)? ClassOf(JsonObject own) =>
            own["e"] is JsonObject e && (string?)e["t"] is { } t ? (t, (e["a"] as JsonArray)?.Count ?? 0) : null;

        /// <summary>The names of the element's carried property and quantity sets.</summary>
        public static IEnumerable<string> SetNames(JsonObject own) =>
            (own["defs"] as JsonArray)?.OfType<JsonObject>().Select(d => d["d"] is JsonObject set ? Name(set) : null).OfType<string>() ?? [];

        /// <summary>A snapshot's Name: its third attribute for a set, its first for a property.</summary>
        static string? Name(JsonObject snap, int at = 2) => snap["a"] is JsonArray a && at < a.Count && a[at] is JsonValue v ? Unquote((string?)v) : null;

        static string? Unquote(string? literal) => literal is { Length: >= 2 } t && t[0] == '\'' && t[^1] == '\'' ? StepFile.Decode(t[1..^1].Replace("''", "'")) : null;

        /// <summary>
        /// The element's carried sets, its type, material, ports and systems, for <paramref name="entity"/>:
        /// a set the user gave values in (<paramref name="user"/>, by set name) gets them over the file's; a set
        /// named in <paramref name="skip"/> is Lapin's own this time and stays out.
        /// </summary>
        public void Element(IModelView m, JsonObject own, string entity, string owner, IReadOnlyDictionary<string, List<(string Name, string Value)>> user, IReadOnlySet<string> skip, Func<string, string, string> single)
        {
            foreach (var d in (own["defs"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if (d["d"] is not JsonObject set) continue;
                var name = Name(set);
                if (name != null && skip.Contains(name)) continue;
                var def = name != null && user.TryGetValue(name, out var values) && (string?)set["t"] == "IFCPROPERTYSET"
                    ? Emit(set, owner, i => i == 4 ? Patched(set, owner, values, single) : null)
                    : Emit(set, owner);
                Relate("IFCRELDEFINESBYPROPERTIES", (string?)d["rel"], owner, "defs:" + name, def, entity);
            }
            if (own["type"] is JsonObject t && Record(m, t) is { } type)
                Relate("IFCRELDEFINESBYTYPE", (string?)t["rel"], owner, "type", Emit(type, owner), entity);
            if (own["material"] is JsonObject mt && Record(m, mt) is { } mat)
                Relate("IFCRELASSOCIATESMATERIAL", (string?)mt["rel"], owner, "material", Emit(mat, owner), entity);
            foreach (var p in (own["ports"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if (p["p"] is not JsonObject port) continue;
                var r = Emit(port, owner);
                if (ArgTextOf(port, 0) is { } pg) ports[pg] = r;
                Relate("IFCRELNESTS", (string?)p["rel"], owner, "ports", entity, r);
            }
            foreach (var c in (own["connects"] as JsonArray)?.OfType<JsonObject>() ?? [])
                if ((string?)c["a"] is { } a && (string?)c["b"] is { } b) connects.Add(((string?)c["rel"] ?? Step.Guid(owner, $"connects/{a}/{b}"), a, b));
            foreach (var g in (own["groups"] as JsonArray)?.OfType<JsonObject>() ?? [])
                if (Record(m, g) is { } group)
                {
                    var r = Emit(group, owner);
                    var rec = m.Get((string?)g["record"] ?? "");
                    Relate("IFCRELASSIGNSTOGROUP", (string?)g["rel"], owner, "group", r, entity);
                    if (!services.ContainsKey(r)) services[r] = rec?.Str(Services);
                }
        }

        static string? ArgTextOf(JsonObject snap, int i) => snap["a"] is JsonArray a && i < a.Count && (string?)a[i] is { Length: 24 } s ? s[1..^1] : null;

        /// <summary>A property set's properties with the user's values over the file's of the same name, then the user's others.</summary>
        string Patched(JsonObject set, string owner, List<(string Name, string Value)> values, Func<string, string, string> single)
        {
            var items = new List<string>();
            var done = new HashSet<string>();
            foreach (var p in (set["a"] as JsonArray)?[4] as JsonArray ?? [])
            {
                if (p is not JsonObject prop) continue;
                var pname = Name(prop, 0);
                var i = values.FindIndex(v => v.Name == pname);
                if (pname != null && i >= 0) { items.Add(single(pname, values[i].Value)); done.Add(pname); }
                else items.Add(Emit(prop, owner));
            }
            foreach (var (n, v) in values) if (done.Add(n)) items.Add(single(n, v));
            return Step.L(items);
        }

        static JsonObject? Record(IModelView m, JsonObject link) =>
            (string?)link["record"] is { } id && m.Get(id) is { Class: IfcSource.RecordClass } rec ? rec.Attrs[Snap] as JsonObject : null;

        void Relate(string cls, string? gid, string owner, string key, string relating, string related)
        {
            var id = gid != null && GlobalId.IsValid(gid) ? gid : Step.Guid(owner, "carried/" + key);
            var k = (cls, id);
            if (!rels.TryGetValue(k, out var r)) rels[k] = r = (relating, []);
            if (r.Relating != relating)
            {
                // The same relationship id over another relating entity (a copy's own set): a relationship of its own.
                k = (cls, Step.Guid(owner, id));
                if (!rels.TryGetValue(k, out r)) rels[k] = r = (relating, []);
            }
            if (!r.Related.Contains(related)) r.Related.Add(related);
        }

        /// <summary>The relationships gathered, written once every element is: a connection only between two ports written, a system serving <paramref name="building"/>.</summary>
        public void Finish(string building)
        {
            foreach (var ((cls, gid), (relating, related)) in rels)
            {
                var id = Step.S(gid);
                var list = Step.L(related);
                _ = cls switch
                {
                    "IFCRELNESTS" => f.Add(cls, id, Step.Null, Step.Null, Step.Null, relating, list),
                    "IFCRELASSIGNSTOGROUP" => f.Add(cls, id, Step.Null, Step.Null, Step.Null, list, Step.Null, relating),
                    _ => f.Add(cls, id, Step.Null, Step.Null, Step.Null, list, relating),
                };
            }
            var seen = new HashSet<string>();
            foreach (var (gid, a, b) in connects)
                if (ports.TryGetValue(a, out var pa) && ports.TryGetValue(b, out var pb) && seen.Add(gid))
                    f.Add("IFCRELCONNECTSPORTS", Step.S(gid), Step.Null, Step.Null, Step.Null, pa, pb, Step.Null);
            foreach (var (system, rel) in services)
                if (rel != null && GlobalId.IsValid(rel) && building.Length > 0) f.Add("IFCRELSERVICESBUILDINGS", Step.S(rel), Step.Null, Step.Null, Step.Null, system, Step.L([building]));
        }
    }
}
