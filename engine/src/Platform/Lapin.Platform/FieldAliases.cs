using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The office's own names for fields (docs/plan-user-extensibility.md, stage „Extrase și câmpuri configurabile”): a table kept
/// on the IfcProject (<c>fieldAliases</c>), each alias a name and the fields it stands for, in order — <c>Length</c> ←
/// <c>Pset_X.L_m</c>, <c>Qto_WallBaseQuantities.Length</c>; <c>Phase</c> ← <c>Pset_Firma.Etapa</c>, <c>ArchiCADProperties.Faza</c> —
/// so one schedule, tag or formula reads files that name a property each their own way. An alias reads as the first of its
/// sources the element has (a value not empty); it is a field like any (<see cref="FieldCatalog"/>): tags, live texts, formulas,
/// schedules and filters all see it, before the element's own field of that name (a source may name it: the element's own
/// then, not the alias again). One named like a standard quantity (<see cref="ReferenceQuantities.Standard"/>) fills it in for
/// references whose file lacks it. Carried between projects as a <c>.lapinaliases</c> file.
/// </summary>
public static class FieldAliases
{
    public const string Key = "fieldAliases";
    public const string FileFormat = "lapin-field-aliases";
    public const int MaxSources = 16;

    public sealed record Alias(string Name, IReadOnlyList<string> Sources, string? Description = null);

    static readonly ConditionalWeakTable<JsonArray, Dictionary<string, Alias>> Tables = new();
    static readonly Dictionary<string, Alias> None = [];

    /// <summary>The aliases the alias being read is in the middle of (a source naming it reads the element's own field).</summary>
    [ThreadStatic] static HashSet<string>? _busy;

    /// <summary>The project's aliases, in their order.</summary>
    public static List<Alias> All(IModelView m) =>
        [.. (Spatial.Project(m)?.Attrs[Key] as JsonArray ?? []).OfType<JsonObject>().Select(Read).OfType<Alias>()];

    static Dictionary<string, Alias> Table(IModelView m)
    {
        if (Spatial.Project(m)?.Attrs[Key] is not JsonArray a || a.Count == 0) return None;
        return Tables.GetValue(a, list =>
        {
            var d = new Dictionary<string, Alias>(StringComparer.Ordinal);
            foreach (var x in list.OfType<JsonObject>().Select(Read).OfType<Alias>()) d.TryAdd(TagText.Squash(x.Name), x);
            return d;
        });
    }

    /// <summary>The alias <paramref name="key"/> names (case and spaces aside), unless it is being read already; null when none.</summary>
    public static Alias? Find(IModelView m, string key)
    {
        var t = Table(m);
        if (t.Count == 0) return null;
        var k = TagText.Squash(key);
        return t.TryGetValue(k, out var a) && _busy?.Contains(k) != true ? a : null;
    }

    /// <summary>
    /// The alias's value: the first of its sources <paramref name="read"/> gives and <paramref name="has"/> takes; false when
    /// <paramref name="key"/> is no alias (or the one being read), <paramref name="value"/> then the default.
    /// </summary>
    public static bool TryResolve<T>(IModelView m, string key, Func<string, T> read, Func<T, bool> has, out T value)
    {
        value = default!;
        if (Find(m, key) is not { } alias) return false;
        var k = TagText.Squash(alias.Name);
        _busy ??= [];
        _busy.Add(k);
        try
        {
            foreach (var s in alias.Sources)
            {
                var v = read(s);
                if (has(v)) { value = v; return true; }
            }
            return true;
        }
        finally { _busy.Remove(k); }
    }

    static Alias? Read(JsonObject o)
    {
        if (((string?)o["name"])?.Trim() is not { Length: > 0 } name) return null;
        var sources = (o["sources"] as JsonArray ?? []).Select(x => ((string?)x)?.Trim()).OfType<string>().Where(s => s.Length > 0).ToList();
        return sources.Count == 0 ? null : new Alias(name, sources, ((string?)o["description"])?.Trim() is { Length: > 0 } d ? d : null);
    }

    public static JsonObject Json(Alias a)
    {
        var o = new JsonObject { ["name"] = a.Name, ["sources"] = new JsonArray([.. a.Sources.Select(s => (JsonNode)s)]) };
        if (a.Description != null) o["description"] = a.Description;
        return o;
    }

    /// <summary>An alias as a command gives it, checked: a name of its own (no dot, no brace; not a field every element has), one to sixteen sources.</summary>
    public static Alias Check(JsonObject a)
    {
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 and <= 80 } n ? n : throw new CommandException("An alias needs a name (at most 80 characters).");
        if (name.IndexOfAny(['.', '{', '}', '|', '=']) >= 0) throw new CommandException($"„{name}”: an alias's name has no dot, brace, bar or equals sign (a dot starts a set or a scope).");
        if (TagText.Squash(name).Length == 0) throw new CommandException($"„{name}”: an alias's name needs a letter or a digit.");
        if (TagText.Builtin.Concat(["tag"]).Any(b => TagText.Squash(b) == TagText.Squash(name))) throw new CommandException($"„{name}” is a field every element has: an alias takes another name.");
        if (ParamScopes.IsScope(name)) throw new CommandException($"„{name}” names a scope (its storey, its type…): an alias takes another name.");
        var sources = (a["sources"] as JsonArray ?? []).Select(x => ((string?)x)?.Trim()).OfType<string>().Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (sources.Count == 0) throw new CommandException($"„{name}”: an alias stands for at least one field.");
        if (sources.Count > MaxSources) throw new CommandException($"„{name}”: an alias stands for at most {MaxSources} fields.");
        if (sources.Any(s => s.Length > 200 || s.Contains('{') || s.Contains('}'))) throw new CommandException($"„{name}”: a source is a field's name (Set.Property), at most 200 characters.");
        return new Alias(name, sources, ((string?)a["description"])?.Trim() is { Length: > 0 } d ? d : null);
    }

    static void Write(ICommandContext ctx, List<Alias> aliases)
    {
        var p = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var dup = aliases.GroupBy(a => TagText.Squash(a.Name)).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) throw new CommandException($"Two aliases are named „{dup.First().Name}”.");
        var attrs = p.CloneAttrs();
        if (aliases.Count == 0) attrs.Remove(Key); else attrs[Key] = new JsonArray([.. aliases.Select(a => (JsonNode)Json(a))]);
        ctx.Replace(p.With(attrs: attrs));
    }

    static List<Alias> FromFile(JsonNode? given)
    {
        JsonNode? n = given;
        if (given is JsonValue v && v.TryGetValue<string>(out var text))
        {
            try { n = JsonNode.Parse(text); } catch (JsonException) { throw new CommandException("That is not an aliases file (.lapinaliases)."); }
        }
        if (n is JsonArray list) return [.. list.OfType<JsonObject>().Select(Check)];
        if (n is not JsonObject o || (string?)o["format"] != FileFormat) throw new CommandException("That is not an aliases file (.lapinaliases).");
        return [.. (o["aliases"] as JsonArray ?? []).OfType<JsonObject>().Select(Check)];
    }

    /// <summary>The aliases as a <c>.lapinaliases</c> file holds them.</summary>
    public static JsonObject File(IEnumerable<Alias> aliases) =>
        new() { ["format"] = FileFormat, ["version"] = 1, ["aliases"] = new JsonArray([.. aliases.Select(a => (JsonNode)Json(a))]) };

    public static void Register(IModuleRegistry r)
    {
        r.Query("fields.aliases", (q, _) => new JsonObject { ["aliases"] = new JsonArray([.. All(q.Model).Select(a => (JsonNode)Json(a))]) });

        // One alias made or changed ({name, sources, description}; `rename`: the name it had).
        r.Command("fields.setAlias", "Field alias", (ctx, a) =>
        {
            var alias = Check(new JsonObject { ["name"] = (string?)a["name"], ["sources"] = (a["sources"] as JsonArray)?.DeepClone(), ["description"] = (string?)a["description"] });
            var all = All(ctx.Model);
            var old = ((string?)a["rename"])?.Trim() is { Length: > 0 } rn ? rn : alias.Name;
            var i = all.FindIndex(x => TagText.Squash(x.Name) == TagText.Squash(old));
            if (i < 0 && old != alias.Name) throw new CommandException($"There is no alias „{old}”.");
            if (i >= 0) all[i] = alias; else all.Add(alias);
            Write(ctx, all);
            return new JsonObject { ["name"] = alias.Name };
        });

        r.Command("fields.removeAlias", "Remove field alias", (ctx, a) =>
        {
            var all = All(ctx.Model);
            var name = (string?)a["name"] ?? "";
            if (all.RemoveAll(x => TagText.Squash(x.Name) == TagText.Squash(name)) == 0) throw new CommandException($"There is no alias „{name}”.");
            Write(ctx, all);
            return null;
        });

        // The whole table at once ({aliases: [...]}, in order), as a table editor saves it.
        r.Command("fields.setAliases", "Field aliases", (ctx, a) =>
        {
            Write(ctx, [.. (a["aliases"] as JsonArray ?? throw new CommandException("Which aliases?")).OfType<JsonObject>().Select(Check)]);
            return null;
        });

        r.Query("fields.exportAliases", (q, _) =>
        {
            var o = File(All(q.Model));
            return new JsonObject { ["text"] = o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ["file"] = "aliases.lapinaliases", ["aliases"] = o["aliases"]!.DeepClone() };
        });

        // A .lapinaliases file (`text`) or a library's list (`aliases`) brought in: each alias by its name made or, when it differs, changed (`keep`: left as the project has it).
        r.Command("fields.importAliases", "Import field aliases", (ctx, a) =>
        {
            var given = a["text"] is JsonValue ? FromFile(a["text"]) : a["aliases"] is JsonArray l ? FromFile(l) : throw new CommandException("Which aliases?");
            var keep = a["keep"] is JsonValue kv && kv.TryGetValue<bool>(out var k) && k;
            var all = All(ctx.Model);
            int added = 0, updated = 0;
            foreach (var x in given)
            {
                var i = all.FindIndex(y => TagText.Squash(y.Name) == TagText.Squash(x.Name));
                if (i < 0) { all.Add(x); added++; }
                else if (!keep && !JsonNode.DeepEquals(Json(all[i]), Json(x))) { all[i] = x; updated++; }
            }
            Write(ctx, all);
            return new JsonObject { ["added"] = added, ["updated"] = updated };
        });
    }
}
