using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// What a symbol set draws for one role (or for a drop, <see cref="MepSymbols.Drop"/>): Lapin's coded drawing of that name
/// (<see cref="Coded"/>, sized on paper or on the model), a symbol definition of the project's or Lapin's (<see cref="SymbolId"/>,
/// its own scale saying which: annotation on paper, model at its size), or a definition given in the set itself (<see cref="Def"/>,
/// a built-in set's data). A symbol's insertion point is the node's point, its +x along the node's X (its rotation).
/// </summary>
public sealed record MepSymbolEntry(string? Coded = null, bool Paper = true, string? SymbolId = null, SymbolDef? Def = null)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject();
        if (Coded != null) { o["coded"] = Coded; o["size"] = Paper ? "paper" : "model"; }
        if (SymbolId != null) o["symbol"] = SymbolId;
        if (Def != null) o["def"] = Def.ToJson();
        return o;
    }

    public static MepSymbolEntry? From(JsonNode? n)
    {
        if (n is not JsonObject o) return null;
        if ((string?)o["symbol"] is { Length: > 0 } id) return new(SymbolId: id);
        if (o["def"] is JsonObject d && SymbolDef.From(d) is { Items.Count: > 0 } def) return new(Def: def);
        if ((string?)o["coded"] is { Length: > 0 } c) return new(c, (string?)o["size"] != "model");
        return null;
    }
}

/// <summary>
/// A set of MEP plan symbols (docs/plan-mep.md): its name, the standard it follows, the set it falls back on for the roles it
/// does not draw (<see cref="Base"/>; every chain ends in Lapin's ISO 4067 / IEC 60617 set), and what it draws for each role.
/// Built-in sets are data (<c>SymbolSets/*.json</c>); a project's are on the project (<c>mepSymbols.sets</c>). A pending set
/// (Romanian STAS) waits for its symbols: it lists the roles it needs and draws its base's meanwhile.
/// </summary>
public sealed record MepSymbolSet(string Id, string Name, string? Standard, string? Base, IReadOnlyDictionary<string, MepSymbolEntry> Symbols,
    bool Builtin = false, bool Pending = false, string? Description = null, IReadOnlyList<string>? Needs = null)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["id"] = Id, ["name"] = Name };
        if (Standard is { Length: > 0 }) o["standard"] = Standard;
        if (Base != null) o["base"] = Base;
        var syms = new JsonObject();
        foreach (var (k, v) in Symbols.OrderBy(x => x.Key, StringComparer.Ordinal)) syms[k] = v.ToJson();
        o["symbols"] = syms;
        return o;
    }

    public static MepSymbolSet? From(JsonNode? n, bool builtin = false)
    {
        if (n is not JsonObject o || (string?)o["id"] is not { Length: > 0 } id) return null;
        var syms = new Dictionary<string, MepSymbolEntry>(StringComparer.Ordinal);
        foreach (var (k, v) in o["symbols"] as JsonObject ?? []) if (MepSymbolEntry.From(v) is { } en) syms[k] = en;
        return new(id, (string?)o["name"] is { Length: > 0 } name ? name : id, (string?)o["standard"], (string?)o["base"], syms, builtin,
            o["pending"] is JsonValue pv && pv.TryGetValue<bool>(out var p) && p, (string?)o["description"],
            (o["needs"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList());
    }
}

/// <summary>What draws a role's symbol on a plan, worked out: the entry, the definition it draws (none: the coded drawing), the set it comes from.</summary>
public sealed record MepSymbolChoice(string Key, MepSymbolEntry Entry, SymbolDef? Def, string SetId)
{
    /// <summary>Sized on paper (drawn as at 1:50, <see cref="Nodes.SymbolScale"/>), else at the model's size.</summary>
    public bool Paper => Def != null ? Def.Scale != "model" : Entry.Paper;
    public string Coded => Entry.Coded ?? Key;
}

/// <summary>
/// The MEP symbol sets and the project's choice (<c>mepSymbols</c> on the project: <c>set</c>, the chosen set's id; <c>sets</c>,
/// the project's own). A role draws the first symbol its set chain gives it: the chosen set, its base, …, Lapin's ISO set; a
/// symbol of the user's own that is gone falls back the same way, so removing one brings the built-in back. Sets travel as
/// <c>.lapinsymbols</c> files (the symbols library format) carrying the set itself under <c>mepSymbolSet</c>.
/// </summary>
public static class MepSymbols
{
    public const string Key = "mepSymbols";
    public const string Default = "iso";
    public const string FileKey = "mepSymbolSet";
    /// <summary>A rise going down: its symbol's own key (ISO draws a cross where a rise draws a dot).</summary>
    public const string Drop = "drop";

    public static readonly IReadOnlyList<MepSymbolSet> Builtin = LoadBuiltin();

    static List<MepSymbolSet> LoadBuiltin()
    {
        var asm = typeof(MepSymbols).Assembly;
        var o = new List<MepSymbolSet>();
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith("Lapin.Mep.SymbolSets.", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            if (MepSymbolSet.From(JsonNode.Parse(stream), builtin: true) is { } set) o.Add(set);
        }
        // ISO first: the default and every chain's end.
        return [.. o.OrderBy(s => s.Id == Default ? 0 : 1).ThenBy(s => s.Id, StringComparer.Ordinal)];
    }

    /// <summary>The keys a set draws: every role's, and the drop's.</summary>
    public static IEnumerable<string> Keys => Roles.All.Select(r => r.Id).Append(Drop);
    public static bool IsKey(string? key) => key == Drop || Roles.Get(key) != null;
    public static string Label(string key) => key == Drop ? "Drop (going down)" : key == "rise" ? "Rise (going up)" : Roles.Get(key)?.Label ?? key;
    /// <summary>The key a node's symbol is drawn by: its role's, a rise going down a drop's.</summary>
    public static string KeyOf(Role role, bool down = false) => role.IsRise && down ? Drop : role.Id;

    static JsonObject? Settings(IModelView m) => Spatial.Project(m)?.Attrs[Key] as JsonObject;

    public static IReadOnlyList<MepSymbolSet> ProjectSets(IModelView m) =>
        [.. (Settings(m)?["sets"] as JsonArray ?? []).Select(n => MepSymbolSet.From(n)).OfType<MepSymbolSet>()];

    public static IEnumerable<MepSymbolSet> All(IModelView m) => Builtin.Concat(ProjectSets(m));

    public static MepSymbolSet? Find(IModelView m, string? id) => id == null ? null : Builtin.FirstOrDefault(s => s.Id == id) ?? ProjectSets(m).FirstOrDefault(s => s.Id == id);

    /// <summary>The set the project draws with: its choice, else Lapin's ISO set.</summary>
    public static string Chosen(IModelView m) => (string?)Settings(m)?["set"] is { } id && Find(m, id) != null ? id : Default;

    /// <summary>A set and those it falls back on, to Lapin's ISO set (a loop cut).</summary>
    public static List<MepSymbolSet> Chain(IModelView m, string setId)
    {
        var o = new List<MepSymbolSet>();
        for (var s = Find(m, setId); s != null && o.All(x => x.Id != s.Id); s = Find(m, s.Base ?? (s.Id == Default ? null : Default))) o.Add(s);
        if (o.All(s => s.Id != Default)) o.Add(Find(m, Default)!);
        return o;
    }

    /// <summary>What draws <paramref name="key"/> in the project's set.</summary>
    public static MepSymbolChoice For(IModelView m, string key) => For(m, key, Chosen(m));

    /// <summary>What draws <paramref name="key"/> in <paramref name="setId"/>: the first of its chain that draws it, a symbol that is gone skipped.</summary>
    public static MepSymbolChoice For(IModelView m, string key, string setId)
    {
        foreach (var set in Chain(m, setId))
        {
            if (!set.Symbols.TryGetValue(key, out var en)) continue;
            if (en.SymbolId != null)
            {
                if (Symbols.DefOf(m, en.SymbolId) is { } def) return new(key, en, def, set.Id);
                continue;
            }
            return new(key, en, en.Def, set.Id);
        }
        return new(key, new MepSymbolEntry(key == Drop ? "rise" : Roles.Get(key)?.Symbol ?? key), null, Default);
    }

    // ------------------------------------------------------------ writing

    static void Write(ICommandContext ctx, Action<JsonObject> change)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        var settings = attrs[Key] is JsonObject s ? (JsonObject)s.DeepClone() : new JsonObject();
        change(settings);
        if (settings["sets"] is JsonArray { Count: 0 }) settings.Remove("sets");
        if (settings.Count > 0) attrs[Key] = settings; else attrs.Remove(Key);
        ctx.Replace(project.With(attrs: attrs));
    }

    static void WriteSets(JsonObject settings, IEnumerable<MepSymbolSet> sets) =>
        settings["sets"] = new JsonArray(sets.Select(s => (JsonNode)s.ToJson()).ToArray());

    static string FreeName(IModelView m, string name, string? except = null) =>
        Names.Free(n => ProjectSets(m).Any(s => s.Id != except && s.Name.Equals(n, StringComparison.OrdinalIgnoreCase)), name.Trim() is { Length: > 0 } t ? t : "MEP symbols");

    static string NewId(IModelView m)
    {
        for (var i = 1; ; i++) if (Find(m, $"set{i}") == null) return $"set{i}";
    }

    /// <summary>A new set of the project's (one change of the command's step): its base, the entries copied from <paramref name="from"/>'s own, chosen when asked.</summary>
    public static MepSymbolSet Create(ICommandContext ctx, string name, string? baseId, string? from, string? standard, bool use)
    {
        var m = ctx.Model;
        var source = from != null ? Find(m, from) ?? throw new CommandException("There is no such symbol set to copy.") : null;
        // A pending built-in (STAS) is a starting point: its name and standard, falling back on its base.
        var b = baseId ?? (source is { Builtin: true } ? source.Base ?? source.Id : source?.Base) ?? Default;
        if (Find(m, b) == null) throw new CommandException("There is no such symbol set to start from.");
        var set = new MepSymbolSet(NewId(m), FreeName(m, name), standard ?? source?.Standard, b,
            source != null ? new Dictionary<string, MepSymbolEntry>(source.Symbols.Where(x => !source.Builtin || x.Value.Coded == null), StringComparer.Ordinal) : new Dictionary<string, MepSymbolEntry>(StringComparer.Ordinal));
        Write(ctx, s =>
        {
            WriteSets(s, ProjectSets(m).Append(set));
            if (use) s["set"] = set.Id;
        });
        return set;
    }

    static MepSymbolSet Own(IModelView m, string? id) =>
        ProjectSets(m).FirstOrDefault(s => s.Id == id) ?? throw new CommandException(Builtin.Any(s => s.Id == id) ? "A built-in set stays as it is: make a set of your own from it." : "There is no such symbol set.");

    static void Replace(ICommandContext ctx, MepSymbolSet set) =>
        Write(ctx, s => WriteSets(s, ProjectSets(ctx.Model).Select(x => x.Id == set.Id ? set : x)));

    /// <summary>
    /// The symbol a role draws in a set of the project's: a symbol's id, or none (back to what the set's base draws). No set
    /// named: the chosen one; a built-in chosen: a set of the project's made from it and chosen, in the same step.
    /// </summary>
    public static string SetRoleSymbol(ICommandContext ctx, string? setId, string key, string? symbolId)
    {
        var m = ctx.Model;
        if (!IsKey(key)) throw new CommandException($"„{key}” is not an MEP role.");
        if (symbolId != null && Symbols.DefOf(m, symbolId) == null) throw new CommandException("There is no such symbol.");
        var id = setId ?? Chosen(m);
        if (setId == null && Find(m, id) is { Builtin: true } builtin)
        {
            var made = Create(ctx, builtin.Pending ? builtin.Name : "Project symbols", null, builtin.Pending ? builtin.Id : null, builtin.Pending ? builtin.Standard : null, use: true);
            id = made.Id;
        }
        var set = Own(ctx.Model, id);
        var syms = new Dictionary<string, MepSymbolEntry>(set.Symbols, StringComparer.Ordinal);
        if (symbolId == null) syms.Remove(key); else syms[key] = new MepSymbolEntry(SymbolId: symbolId);
        Replace(ctx, set with { Symbols = syms });
        return set.Id;
    }

    // ------------------------------------------------------------ files

    /// <summary>A set of the project's as a symbols library file: the symbols it draws, and the set naming them by name.</summary>
    public static JsonObject Export(IModelView m, string id)
    {
        var set = Own(m, id);
        var used = set.Symbols.Where(x => x.Value.SymbolId != null && Symbols.DefOf(m, x.Value.SymbolId) != null).ToList();
        if (used.Count == 0) throw new CommandException("The set draws no symbol of its own yet: give a role a drawn symbol first.");
        var file = Symbols.Export(m, used.Select(x => x.Value.SymbolId!).Distinct(), set.Name);
        var syms = new JsonObject();
        foreach (var (k, v) in used.OrderBy(x => x.Key, StringComparer.Ordinal)) syms[k] = new JsonObject { ["symbol"] = Symbols.NameOf(m, v.SymbolId) };
        var o = new JsonObject { ["name"] = set.Name };
        if (set.Standard is { Length: > 0 }) o["standard"] = set.Standard;
        if (set.Base != null && Builtin.Any(b => b.Id == set.Base)) o["base"] = set.Base;
        o["symbols"] = syms;
        file[FileKey] = o;
        return file;
    }

    /// <summary>A file's set: its name, standard, base and the symbol each role names (by the symbol's name in the file).</summary>
    public static (string Name, string? Standard, string? Base, Dictionary<string, string> Roles) ReadSet(string text)
    {
        var root = LibraryFile.Read(text, Symbols.Spec).Root;
        if (root[FileKey] is not JsonObject so) throw new CommandException("This symbols file holds no MEP symbol set: import it in Manage → Symbols….");
        var roles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in so["symbols"] as JsonObject ?? [])
            if (IsKey(k) && (string?)v?["symbol"] is { Length: > 0 } name) roles[k] = name;
        return ((string?)so["name"] is { Length: > 0 } n ? n : "MEP symbols", (string?)so["standard"], (string?)so["base"], roles);
    }

    /// <summary>
    /// A set's file brought in, one step: its symbols as Symbols' import brings them (the choices for those of a name the project
    /// has), and a new set of the project's naming them, chosen unless <paramref name="use"/> is false.
    /// </summary>
    public static JsonObject Import(ICommandContext ctx, string text, JsonObject? choices, string? all, bool use)
    {
        var (name, standard, baseId, roles) = ReadSet(text);
        var read = Symbols.Read(text);
        // What the import does with each symbol, decided before it runs, so the ids it gives back line up with the file's symbols.
        var decisions = read.Select(s => LibraryMerge.Decide(Symbols.Rows(ctx.Model, [s])[0], choices, all)).ToList();
        var result = Symbols.Import(ctx, text, choices, all, null);
        var ids = (result["ids"] as JsonArray ?? []).Select(x => (string?)x).ToList();
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var j = 0;
        for (var i = 0; i < read.Count; i++)
        {
            if (decisions[i] == LibraryMerge.Skip)
            {
                if (ctx.Model.OfClass(Symbols.Class).FirstOrDefault(e => e.Name.Equals(read[i].Name, StringComparison.OrdinalIgnoreCase)) is { } mine) byName.TryAdd(read[i].Name, mine.Id);
                continue;
            }
            if (j < ids.Count && ids[j] is { } id) byName.TryAdd(read[i].Name, id);
            j++;
        }
        var set = Create(ctx, name, baseId != null && Builtin.Any(b => b.Id == baseId) ? baseId : Default, null, standard, use);
        var syms = new Dictionary<string, MepSymbolEntry>(StringComparer.Ordinal);
        foreach (var (k, symbolName) in roles) if (byName.TryGetValue(symbolName, out var sid)) syms[k] = new MepSymbolEntry(SymbolId: sid);
        Replace(ctx, set with { Symbols = syms });
        result["id"] = set.Id;
        result["name"] = set.Name;
        result["roles"] = syms.Count;
        return result;
    }

    // ------------------------------------------------------------ commands

    static JsonObject SetJson(IModelView m, MepSymbolSet s) => new()
    {
        ["id"] = s.Id, ["name"] = s.Name, ["standard"] = s.Standard, ["base"] = s.Base, ["builtin"] = s.Builtin, ["pending"] = s.Pending,
        ["description"] = s.Description,
        ["needs"] = s.Needs is { } needs ? new JsonArray(needs.Select(x => (JsonNode)x).ToArray()) : null,
        ["own"] = s.Symbols.Count(x => x.Value.Coded == null),
    };

    public static void Register(IModuleRegistry r)
    {
        // The sets (Lapin's and the project's), the one chosen, and for a set (the chosen one unless named) what each role draws:
        // where from (its own symbol, or the set it falls back on), sized on paper or on the model, and its picture in paper mm.
        r.Query("symbolSets", (q, a) =>
        {
            var m = q.Model;
            var chosen = Chosen(m);
            var setId = (string?)a["setId"] is { } sid && Find(m, sid) != null ? sid : chosen;
            var previews = !(a["previews"] is JsonValue pv && pv.TryGetValue<bool>(out var p) && !p);
            var keys = new JsonArray();
            foreach (var key in Keys)
            {
                var c = For(m, key, setId);
                var o = new JsonObject
                {
                    ["key"] = key, ["label"] = Label(key), ["from"] = c.SetId, ["paper"] = c.Paper,
                    ["symbolId"] = c.Entry.SymbolId, ["symbolName"] = c.Entry.SymbolId != null ? Symbols.NameOf(m, c.Entry.SymbolId) : null,
                    ["own"] = c.SetId == setId && c.Entry.Coded == null,
                    ["services"] = key == Drop ? new JsonArray("*") : new JsonArray(Roles.Require(key).Ports.Select(x => x.Service).Distinct().Select(x => (JsonNode)x).ToArray()),
                };
                if (previews) o["glyph"] = MepGlyphs.ToJson(MepGlyphs.Of(m, key, setId, 100));
                keys.Add((JsonNode)o);
            }
            return new JsonObject
            {
                ["set"] = chosen, ["shown"] = setId,
                ["sets"] = new JsonArray(All(m).Select(s => (JsonNode)SetJson(m, s)).ToArray()),
                ["keys"] = keys,
            };
        });

        // The set the project's plans draw with (null: Lapin's ISO set), one step.
        r.Command("setSymbolSet", "MEP symbol set", (ctx, a) =>
        {
            var id = (string?)a["id"] is { Length: > 0 } x ? x : null;
            if (id != null && Find(ctx.Model, id) == null) throw new CommandException("There is no such symbol set.");
            if (id != null && Find(ctx.Model, id) is { Builtin: true, Pending: true } p) throw new CommandException($"„{p.Name}” has no symbols yet: make a set of your own from it and draw its symbols.");
            Write(ctx, s => { if (id == null || id == Default) s.Remove("set"); else s["set"] = id; });
            return null;
        });

        // A set of the project's: {name, base? (the set it falls back on: ISO by default), from? (a set whose own symbols it copies;
        // a pending built-in such as STAS starts one), standard?, use? (chosen; default yes)}.
        r.Command("createSymbolSet", "New MEP symbol set", (ctx, a) =>
        {
            var from = (string?)a["from"] is { Length: > 0 } f ? f : null;
            var name = (string?)a["name"] is { Length: > 0 } n ? n : from != null && Find(ctx.Model, from) is { } src ? (src.Pending ? src.Name : src.Name + " copy") : "MEP symbols";
            var use = !(a["use"] is JsonValue uv && uv.TryGetValue<bool>(out var u) && !u);
            var set = Create(ctx, name, (string?)a["base"] is { Length: > 0 } b ? b : null, from, (string?)a["standard"], use);
            return new JsonObject { ["id"] = set.Id, ["name"] = set.Name };
        });

        // A set's name, standard or base changed.
        r.Command("updateSymbolSet", "Change MEP symbol set", (ctx, a) =>
        {
            var set = Own(ctx.Model, (string?)a["id"]);
            if ((string?)a["name"] is { } nm) set = set with { Name = nm.Trim() is { Length: > 0 } t ? FreeName(ctx.Model, t, set.Id) : throw new CommandException("A symbol set needs a name.") };
            if (a.ContainsKey("standard")) set = set with { Standard = (string?)a["standard"] is { Length: > 0 } st ? st.Trim() : null };
            if ((string?)a["base"] is { Length: > 0 } b)
            {
                if (Find(ctx.Model, b) == null || b == set.Id || Chain(ctx.Model, b).Any(x => x.Id == set.Id)) throw new CommandException("A set falls back on another set, never on itself.");
                set = set with { Base = b };
            }
            Replace(ctx, set);
            return null;
        });

        // A set of the project's deleted (its symbols stay in the project); chosen, the plans draw Lapin's again.
        r.Command("deleteSymbolSet", "Delete MEP symbol set", (ctx, a) =>
        {
            var set = Own(ctx.Model, (string?)a["id"]);
            var m = ctx.Model;
            Write(ctx, s =>
            {
                // The sets falling back on it fall back on its base.
                WriteSets(s, ProjectSets(m).Where(x => x.Id != set.Id).Select(x => x.Base == set.Id ? x with { Base = set.Base ?? Default } : x));
                if ((string?)s["set"] == set.Id) s.Remove("set");
            });
            return null;
        });

        // A role's symbol in a set: {setId? (the chosen one; a built-in chosen: a set of the project's made from it), role, symbolId (null: none, its base's)}.
        r.Command("setRoleSymbol", "MEP role symbol", (ctx, a) =>
        {
            var key = (string?)a["role"] ?? throw new CommandException("Which role?");
            var id = SetRoleSymbol(ctx, (string?)a["setId"] is { Length: > 0 } s ? s : null, key, (string?)a["symbolId"] is { Length: > 0 } y ? y : null);
            return new JsonObject { ["setId"] = id };
        });

        // A set as a file to share (.lapinsymbols: its symbols, and the set).
        r.Query("exportSymbolSet", (q, a) =>
        {
            var o = Export(q.Model, (string?)a["id"] ?? "");
            var name = (string?)o[FileKey]?["name"] ?? "MEP symbols";
            return new JsonObject { ["text"] = LibraryFile.Text(o), ["file"] = string.Concat(name.Select(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '_' ? ch : '_')).Trim() + ".lapinsymbols" };
        });

        // A set's file read: its name, standard, the roles it draws, and its symbols as an import would take them.
        r.Query("readSymbolSet", (q, a) =>
        {
            var text = (string?)a["text"] ?? "";
            var (name, standard, baseId, roles) = ReadSet(text);
            var read = Symbols.Read(text);
            return new JsonObject
            {
                ["name"] = name, ["standard"] = standard, ["base"] = baseId,
                ["roles"] = new JsonArray(roles.Select(x => (JsonNode)new JsonObject { ["role"] = x.Key, ["label"] = Label(x.Key), ["symbol"] = x.Value }).ToArray()),
                ["preview"] = LibraryMerge.Preview(Symbols.Rows(q.Model, read)),
            };
        });

        // A set's file brought in, one step: {text, choices?, all? (for symbols of a name the project has), use? (chosen; default yes)}.
        r.Command("importSymbolSet", "Import MEP symbol set", (ctx, a) =>
        {
            var use = !(a["use"] is JsonValue uv && uv.TryGetValue<bool>(out var u) && !u);
            return Import(ctx, (string?)a["text"] ?? "", a["choices"] as JsonObject, (string?)a["all"], use);
        });
    }
}
