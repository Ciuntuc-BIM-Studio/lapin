using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Classifications and keynotes (docs/plan-user-extensibility.md §7.3): a system the user brings (Uniclass, OmniClass, CCI, the
/// country's or the office's; their licences are theirs, Lapin ships none) as a <c>LapinClassification</c> of the project — its
/// name, source, edition and items (code, title, parent code) —, read from a table; elements and types carry a code of each system
/// (<c>classification</c>: [{system, code}]), set by hand or from a table; tags read <c>{classification.code}</c>,
/// <c>{classification.title}</c> or a system's own <c>{classification.Uniclass}</c> (a keynote is a tag of its code); the IFC
/// export writes IfcClassification, IfcClassificationReference and IfcRelAssociatesClassification.
/// </summary>
public static class Classifications
{
    public const string Class = "LapinClassification";
    public const string Key = "classification";
    public const int MaxItems = 100_000;

    public sealed record Item(string Code, string Title, string? Parent);

    public static IEnumerable<Entity> Systems(IModelView m) => m.OfClass(Class);

    public static Entity? System(IModelView m, string? idOrName) =>
        m.Get(idOrName ?? "") is { Class: Class } e ? e : Systems(m).FirstOrDefault(s => string.Equals(s.Name, idOrName, StringComparison.OrdinalIgnoreCase));

    static Entity Require(IModelView m, string? idOrName) => System(m, idOrName) ?? throw new CommandException($"The project has no classification „{idOrName}”.");

    public static List<Item> Items(Entity system) =>
        [.. (system.Attrs["items"] as JsonArray ?? []).OfType<JsonObject>().Select(o => new Item((string?)o["code"] ?? "", (string?)o["title"] ?? "", (string?)o["parent"])).Where(i => i.Code.Length > 0)];

    static JsonArray ItemsJson(IEnumerable<Item> items) => new(items.Select(i =>
    {
        var o = new JsonObject { ["code"] = i.Code, ["title"] = i.Title };
        if (i.Parent is { Length: > 0 }) o["parent"] = i.Parent;
        return (JsonNode)o;
    }).ToArray());

    /// <summary>An element's references: (system id, code), each system once.</summary>
    public static List<(string System, string Code)> RefsOf(Entity? e) =>
        [.. (e?.Attrs[Key] as JsonArray ?? []).OfType<JsonObject>().Select(o => ((string?)o["system"] ?? "", (string?)o["code"] ?? "")).Where(r => r.Item1.Length > 0 && r.Item2.Length > 0)];

    /// <summary>
    /// What a tag reads of an element's classification (its own, else its type's): <c>classification</c> or <c>classification.code</c>
    /// the first code, <c>classification.title</c> its title, <c>classification.&lt;system&gt;</c> that system's code; null when the key is none of these.
    /// </summary>
    public static string? TagValue(IModelView m, Entity e, Entity? type, string key)
    {
        if (!key.StartsWith(Key, StringComparison.OrdinalIgnoreCase)) return null;
        var rest = key.Length > Key.Length ? key[(Key.Length + 1)..] : "code";
        if (key.Length > Key.Length && key[Key.Length] != '.') return null;
        var refs = RefsOf(e) is { Count: > 0 } own ? own : RefsOf(type);
        if (rest.Equals("code", StringComparison.OrdinalIgnoreCase)) return refs.FirstOrDefault().Code ?? "";
        if (rest.Equals("title", StringComparison.OrdinalIgnoreCase))
        {
            if (refs.Count == 0) return "";
            var (sys, code) = refs[0];
            return m.Get(sys) is { Class: Class } s ? Items(s).FirstOrDefault(i => i.Code == code)?.Title ?? "" : "";
        }
        var system = System(m, rest);
        return system == null ? null : refs.FirstOrDefault(r => r.System == system.Id).Code ?? "";
    }

    static void SetRefs(ICommandContext ctx, Entity e, List<(string System, string Code)> refs)
    {
        var attrs = e.CloneAttrs();
        if (refs.Count == 0) attrs.Remove(Key);
        else attrs[Key] = new JsonArray(refs.Select(r => (JsonNode)new JsonObject { ["system"] = r.System, ["code"] = r.Code }).ToArray());
        ctx.Replace(e.With(attrs: attrs));
    }

    /// <summary>Items read from a command ([{code, title, parent?}]), each checked.</summary>
    static List<Item> ParseItems(JsonNode? n)
    {
        if (n is null) return [];
        if (n is not JsonArray a) throw new CommandException("Items are a list of {code, title, parent?}.");
        var o = new List<Item>();
        foreach (var x in a)
        {
            if (x is not JsonObject io || ((string?)io["code"])?.Trim() is not { Length: > 0 and <= 80 } code) throw new CommandException("Each item has a code (80 characters at most).");
            o.Add(new Item(code, ((string?)io["title"])?.Trim() ?? "", ((string?)io["parent"])?.Trim() is { Length: > 0 } p ? p : null));
        }
        return o;
    }

    // ------------------------------------------------------------ tables

    public static readonly TableTarget ItemsTable = new("core.classification", "Classification items", "core",
    [
        new("code", "Code", Required: true, Aliases: ["Code", "Cod", "Number", "Nummer", "Numéro", "Ref", "Reference"]),
        new("title", "Title", Aliases: ["Title", "Titlu", "Denumire", "Name", "Description", "Descriere", "Bezeichnung", "Libellé"]),
        new("parent", "Parent code", Aliases: ["Parent", "Parinte", "Părinte", "Group", "Grup", "Gruppe"]),
        new("system", "Classification", Aliases: ["System", "Sistem", "Classification", "Clasificare", "Klassifikation"], Help: "the system's name; none: the one chosen for the table"),
    ],
    (q, row) =>
    {
        var name = row.Need("system", "The classification");
        var steps = new List<TableStep>();
        var made = row.Shared.TryGetValue("systems", out var o) ? (HashSet<string>)o : (HashSet<string>)(row.Shared["systems"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        if (System(q.Model, name) == null && made.Add(name)) steps.Add(new("classification.create", new JsonObject { ["name"] = name }, $"Classification {name}: new"));
        var item = new JsonObject { ["code"] = row.Need("code", "The code"), ["title"] = row.Text("title") ?? "" };
        if (row.Text("parent") is { } p) item["parent"] = p;
        steps.Add(new("classification.addItems", new JsonObject { ["system"] = name, ["items"] = new JsonArray(item) }, $"{name} {item["code"]}: {item["title"]}"));
        return steps;
    }, Description: "A classification system's items (code, title, parent), made or completed.",
    Example: [["Ss_25", "Wall and barrier systems", "Ss", "Uniclass"], ["Ss_25_10", "Framed wall systems", "Ss_25", "Uniclass"]]);

    public static readonly TableTarget AssignTable = new("core.classify", "Classification codes on elements", "core",
    [
        new("element", "Element", Required: true, Aliases: ["GlobalId", "IfcGUID", "Guid", "Id", "Mark", "Tag", "Marca", "Marcă", "Name", "Nume", "Element", "Type", "Tip"]),
        new("code", "Code", Required: true, Aliases: ["Code", "Cod", "Classification code", "Keynote", "Nota", "Notă"]),
        new("system", "Classification", Aliases: ["System", "Sistem", "Classification", "Clasificare"], Help: "the system's name; none: the one chosen for the table"),
    ],
    (q, row) =>
    {
        var key = row.Need("element", "The element");
        var found = PropertyTable.Find(q.Model, key);
        // A type's name names the type.
        if (found.Count == 0) found = [.. q.Model.Entities.Where(e => e.Class.EndsWith("Type", StringComparison.Ordinal) && string.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase))];
        if (found.Count == 0) throw new CommandException($"No element has the GlobalId, mark or name „{key}”.");
        var system = row.Need("system", "The classification");
        var code = row.Need("code", "The code");
        if (System(q.Model, system) is { } s && !Items(s).Any(i => i.Code == code)) throw new CommandException($"{s.Name} has no code „{code}”.");
        return [new("classification.assign", new JsonObject { ["ids"] = new JsonArray(found.Select(e => (JsonNode)e.Id).ToArray()), ["system"] = system, ["code"] = code },
            $"{(found.Count == 1 ? $"„{found[0].Name}”" : $"{found.Count} elements „{key}”")}: {system} {code}")];
    }, Description: "Classification codes put on elements and types found by GlobalId, mark or name.",
    Example: [["W-01", "Ss_25_10", "Uniclass"]]);

    // ------------------------------------------------------------ commands

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Classification", [], "#555555", Kind: "resource"));
        r.Service<TableTarget>(ItemsTable);
        r.Service<TableTarget>(AssignTable);

        r.Query("classification.list", (q, _) => new JsonObject
        {
            ["systems"] = new JsonArray(Systems(q.Model).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Select(s => (JsonNode)new JsonObject
            {
                ["id"] = s.Id, ["name"] = s.Name, ["source"] = s.Str("source"), ["edition"] = s.Str("edition"), ["items"] = Items(s).Count,
                ["uses"] = q.Model.Entities.Count(e => RefsOf(e).Any(x => x.System == s.Id)),
            }).ToArray()),
        });

        // A system's items: under a parent (none: the top ones), or those matching a search in code or title; at most 500.
        r.Query("classification.items", (q, a) =>
        {
            var s = Require(q.Model, (string?)a["id"] ?? (string?)a["system"]);
            var items = Items(s);
            var search = ((string?)a["search"])?.Trim();
            IEnumerable<Item> pick = search is { Length: > 0 }
                ? items.Where(i => i.Code.Contains(search, StringComparison.OrdinalIgnoreCase) || i.Title.Contains(search, StringComparison.OrdinalIgnoreCase))
                : a.ContainsKey("parent") ? items.Where(i => i.Parent == (string?)a["parent"]) : items;
            var codes = items.Select(i => i.Parent).OfType<string>().ToHashSet();
            return new JsonObject
            {
                ["items"] = new JsonArray(pick.Take(500).Select(i => (JsonNode)new JsonObject { ["code"] = i.Code, ["title"] = i.Title, ["parent"] = i.Parent, ["children"] = codes.Contains(i.Code) }).ToArray()),
                ["count"] = items.Count,
            };
        });

        r.Command("classification.create", "New classification", (ctx, a) =>
        {
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 and <= 80 } n ? n : throw new CommandException("A classification needs a name.");
            if (System(ctx.Model, name) != null) throw new CommandException($"There is a classification „{name}” already.");
            var attrs = new JsonObject { ["items"] = ItemsJson(ParseItems(a["items"])) };
            foreach (var k in new[] { "source", "edition", "description" }) if ((string?)a[k] is { Length: > 0 } v) attrs[k] = v;
            var e = new Entity(ctx.NewId(), Class, name, attrs);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });

        r.Command("classification.update", "Change classification", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var attrs = s.CloneAttrs();
            foreach (var k in new[] { "source", "edition", "description" })
                if (a.ContainsKey(k)) { if ((string?)a[k] is { Length: > 0 } v) attrs[k] = v; else attrs.Remove(k); }
            var name = (string?)a["name"] is { } nn ? nn.Trim() is { Length: > 0 } t ? t : throw new CommandException("A classification needs a name.") : null;
            if (name != null && System(ctx.Model, name) is { } other && other.Id != s.Id) throw new CommandException($"There is a classification „{name}” already.");
            ctx.Replace(s.With(name: name, attrs: attrs));
            return null;
        });

        // Items added to a system, or their title and parent changed (by code).
        r.Command("classification.addItems", "Classification items", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"] ?? (string?)a["system"]);
            var items = Items(s);
            var at = items.Select((x, i) => (x.Code, i)).ToDictionary(p => p.Code, p => p.i);
            foreach (var item in ParseItems(a["items"]))
                if (at.TryGetValue(item.Code, out var i)) items[i] = item;
                else { at[item.Code] = items.Count; items.Add(item); }
            if (items.Count > MaxItems) throw new CommandException($"A classification has at most {MaxItems} items.");
            var attrs = s.CloneAttrs();
            attrs["items"] = ItemsJson(items);
            ctx.Replace(s.With(attrs: attrs));
            return new JsonObject { ["count"] = items.Count };
        });

        // A system deleted, and its codes from every element.
        r.Command("classification.delete", "Delete classification", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            foreach (var e in ctx.Model.Entities.Where(e => RefsOf(e).Any(x => x.System == s.Id)).ToList())
                SetRefs(ctx, e, RefsOf(e).Where(x => x.System != s.Id).ToList());
            ctx.Remove(s.Id);
            return null;
        });

        // A code of a system put on elements or types ({ids, system, code}); one of that system they had is replaced. A code the
        // system lacks is refused; with `add`, it is added to the system (a keynote of the office's own).
        r.Command("classification.assign", "Classify", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["system"]);
            var code = ((string?)a["code"])?.Trim() is { Length: > 0 } c ? c : throw new CommandException("Which code?");
            if (!Items(s).Any(i => i.Code == code))
            {
                if (!(a["add"] is JsonValue av && av.TryGetValue<bool>(out var add) && add)) throw new CommandException($"{s.Name} has no code „{code}”.");
                var attrs = s.CloneAttrs();
                attrs["items"] = ItemsJson([.. Items(s), new Item(code, ((string?)a["title"])?.Trim() ?? "", null)]);
                ctx.Replace(s.With(attrs: attrs));
            }
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException("Something chosen is gone.");
                SetRefs(ctx, e, [.. RefsOf(e).Where(x => x.System != s.Id), (s.Id, code)]);
            }
            return null;
        });

        r.Command("classification.unassign", "Unclassify", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["system"]);
            foreach (var id in Args.Ids(a))
                if (ctx.Model.Get(id) is { } e) SetRefs(ctx, e, RefsOf(e).Where(x => x.System != s.Id).ToList());
            return null;
        });

        // A system's items as CSV (code; title; parent), the table import's own columns.
        r.Query("classification.export", (q, a) =>
        {
            var s = Require(q.Model, (string?)a["id"]);
            static string Cell(string v) => v.Contains(';') || v.Contains('"') ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
            var sb = new StringBuilder("Code;Title;Parent\n");
            foreach (var i in Items(s)) sb.Append(Cell(i.Code)).Append(';').Append(Cell(i.Title)).Append(';').Append(Cell(i.Parent ?? "")).Append('\n');
            return new JsonObject { ["text"] = sb.ToString(), ["file"] = s.Name + ".csv" };
        });

        ParamDefs.Register(r);
    }
}

/// <summary>
/// Shared parameters (docs/plan-user-extensibility.md §7.3): the project's parameter definitions — name, kind, property set, choices,
/// the classes they are for, a description, a stable id —, kept on the IfcProject (<c>paramDefs</c>) and carried between projects
/// as a <c>.lapinparams</c> file, so tags and schedules find the same parameters everywhere. A parameter set on an element by a
/// definition's name takes its kind and set when the command gives none.
/// </summary>
public static class ParamDefs
{
    public const string Key = "paramDefs";
    public const string FileFormat = "lapin-parameters";

    public sealed record Def(string Id, string Name, string Kind, string? Pset, JsonArray? Options, JsonArray? Classes, string? Description);

    public static List<Def> All(IModelView m) =>
        [.. (Spatial.Project(m)?.Attrs[Key] as JsonArray ?? []).OfType<JsonObject>().Select(Read).OfType<Def>()];

    static Def? Read(JsonObject o)
    {
        if (((string?)o["name"])?.Trim() is not { Length: > 0 } name || (string?)o["kind"] is not { } kind || !Params.Kinds.Contains(kind)) return null;
        return new((string?)o["id"] ?? GlobalId.New(), name, kind, (string?)o["pset"] is { Length: > 0 } p ? p : null, o["options"] as JsonArray, o["classes"] as JsonArray, (string?)o["description"]);
    }

    static JsonObject Json(Def d)
    {
        var o = new JsonObject { ["id"] = d.Id, ["name"] = d.Name, ["kind"] = d.Kind };
        if (d.Pset != null) o["pset"] = d.Pset;
        if (d.Options != null) o["options"] = d.Options.DeepClone();
        if (d.Classes != null) o["classes"] = d.Classes.DeepClone();
        if (d.Description != null) o["description"] = d.Description;
        return o;
    }

    static void Write(ICommandContext ctx, List<Def> defs)
    {
        var p = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = p.CloneAttrs();
        if (defs.Count == 0) attrs.Remove(Key); else attrs[Key] = new JsonArray(defs.Select(d => (JsonNode)Json(d)).ToArray());
        ctx.Replace(p.With(attrs: attrs));
    }

    /// <summary>The definition of a parameter by name (and set, when given).</summary>
    public static Def? Find(IModelView m, string name, string? pset = null) =>
        All(m).FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase) && (pset == null || string.Equals(d.Pset, pset, StringComparison.OrdinalIgnoreCase)));

    static Def Check(JsonObject o, string? id)
    {
        var name = ((string?)o["name"])?.Trim() is { Length: > 0 and <= 80 } n ? n : throw new CommandException("A parameter needs a name.");
        var kind = (string?)o["kind"] is { } k && Params.Kinds.Contains(k) ? k : throw new CommandException($"A parameter's kind is one of {string.Join(", ", Params.Kinds)}.");
        var pset = ((string?)o["pset"])?.Trim() is { Length: > 0 } p ? p : null;
        if (pset != null && StandardPsets.IsStandard(pset) && StandardPsets.All.All(x => !string.Equals(x.Name, pset, StringComparison.Ordinal))) throw new CommandException("„Pset_” is IFC's own prefix: a set of the user's never starts with it.");
        return new(id ?? (string?)o["id"] ?? GlobalId.New(), name, kind, pset, o["options"] as JsonArray, o["classes"] as JsonArray, ((string?)o["description"])?.Trim() is { Length: > 0 } d ? d : null);
    }

    public static void Register(IModuleRegistry r)
    {
        r.Query("params.defs", (q, _) => new JsonObject { ["defs"] = new JsonArray(All(q.Model).Select(d => (JsonNode)Json(d)).ToArray()) });

        // A definition made or changed ({def}: by its id, else its name and set).
        r.Command("params.setDef", "Parameter definition", (ctx, a) =>
        {
            var given = a["def"] as JsonObject ?? throw new CommandException("Which definition?");
            var defs = All(ctx.Model);
            var i = defs.FindIndex(d => d.Id == (string?)given["id"] || (string.Equals(d.Name, (string?)given["name"], StringComparison.OrdinalIgnoreCase) && string.Equals(d.Pset, (string?)given["pset"], StringComparison.OrdinalIgnoreCase)));
            var def = Check(given, i >= 0 ? defs[i].Id : null);
            if (i >= 0) defs[i] = def; else defs.Add(def);
            Write(ctx, defs);
            return new JsonObject { ["id"] = def.Id };
        });

        r.Command("params.removeDef", "Remove parameter definition", (ctx, a) =>
        {
            var defs = All(ctx.Model);
            if (defs.RemoveAll(d => d.Id == (string?)a["id"]) == 0) throw new CommandException("There is no such definition.");
            Write(ctx, defs);
            return null;
        });

        r.Query("params.exportDefs", (q, _) =>
        {
            var o = new JsonObject { ["format"] = FileFormat, ["version"] = 1, ["parameters"] = new JsonArray(All(q.Model).Select(d => (JsonNode)Json(d)).ToArray()) };
            return new JsonObject { ["text"] = o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ["file"] = "parameters.lapinparams" };
        });

        // A .lapinparams file brought in: each definition by its id (else its name and set) made or, when it differs, changed (`keep`: left as the project has it).
        r.Command("params.importDefs", "Import parameter definitions", (ctx, a) =>
        {
            JsonNode? n;
            try { n = JsonNode.Parse((string?)a["text"] ?? ""); } catch (JsonException) { throw new CommandException("That is not a parameters file (.lapinparams)."); }
            if (n is not JsonObject o || (string?)o["format"] != FileFormat) throw new CommandException("That is not a parameters file (.lapinparams).");
            var keep = a["keep"] is JsonValue kv && kv.TryGetValue<bool>(out var k) && k;
            var defs = All(ctx.Model);
            int added = 0, updated = 0;
            foreach (var x in (o["parameters"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var def = Check(x, null);
                var i = defs.FindIndex(d => d.Id == def.Id || (string.Equals(d.Name, def.Name, StringComparison.OrdinalIgnoreCase) && string.Equals(d.Pset, def.Pset, StringComparison.OrdinalIgnoreCase)));
                if (i < 0) { defs.Add(def); added++; }
                else if (!keep && !JsonNode.DeepEquals(Json(defs[i] with { Id = def.Id }), Json(def))) { defs[i] = def with { Id = defs[i].Id }; updated++; }
            }
            Write(ctx, defs);
            return new JsonObject { ["added"] = added, ["updated"] = updated };
        });
    }
}
