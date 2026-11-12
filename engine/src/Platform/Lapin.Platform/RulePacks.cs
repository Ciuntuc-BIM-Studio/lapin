using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A section of a rule pack a module reads (docs/plan-user-extensibility.md §5.4): its key (<c>stairs</c>, <c>rebar</c>, <c>bolts</c>,
/// <c>roofs</c>), its label, Lapin's own values (the ones the module's code has always used, the built-in pack's) and how a pack's
/// section is checked and filled in (a missing value takes Lapin's).
/// </summary>
public interface IRuleSection
{
    string Key { get; }
    string Label { get; }
    JsonObject Default { get; }
    /// <summary>A section as a pack gives it, checked and completed; refused with why when it cannot be read.</summary>
    JsonObject Check(JsonNode? section);
}

/// <summary>What a module finds against the project's rules: an element (or a type), how bad, what.</summary>
public sealed record RuleFinding(string Module, string? ElementId, string Severity, string Message);

/// <summary>A module's check of the project against the active rule pack (the Check against rules report).</summary>
public interface IRuleCheck
{
    IEnumerable<RuleFinding> Check(IQueryContext q);
}

/// <summary>
/// Rule packs (docs/plan-user-extensibility.md §5.4): the norms the configurators use as data — stair limits, reinforcement steels,
/// diameters, mandrels and hook tails, bolt grades, coverings' least pitches —, each a section a module reads. Lapin's own,
/// „Romania (indicative)”, holds the values its code has always had; a project's own packs (<c>LapinRulePack</c>) are copies the
/// user changes, or files brought in (<c>.lapinrules</c>); the project names the one it uses (<c>rulePack</c> on the IfcProject).
/// Every pack is indicative: the norms are the user's to check. Configurators propose and warn from the pack; they never refuse.
/// </summary>
public static class RulePacks
{
    public const string Class = "LapinRulePack";
    public const string Key = "rulePack";
    public const string Builtin = "builtin:rules:ro";
    public const string BuiltinName = "Romania (indicative)";
    public const string FileFormat = "lapin-rules";
    public const int FileVersion = 1;

    public static IEnumerable<IRuleSection> Sections(IServices s) => s.All<IRuleSection>();

    /// <summary>The pack the project uses: its own named one, else Lapin's.</summary>
    public static string ActiveId(IModelView m) =>
        Spatial.Project(m)?.Str(Key) is { Length: > 0 } id && m.Get(id) is { Class: Class } ? id : Builtin;

    /// <summary>A pack's definition (its sections as stored; Lapin's is its sections' defaults).</summary>
    static JsonObject? Def(IModelView m, IServices s, string id)
    {
        if (id == Builtin) return new JsonObject(Sections(s).Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Default.DeepClone())));
        return m.Get(id) is { Class: Class } e ? e.Attrs["def"] as JsonObject : null;
    }

    /// <summary>A section of the project's pack, completed with Lapin's values where it says nothing.</summary>
    public static JsonObject Section(IModelView m, IServices s, string key)
    {
        var section = Sections(s).FirstOrDefault(x => x.Key == key) ?? throw new CommandException($"No module of this edition reads the rules' „{key}”.");
        var mine = Def(m, s, ActiveId(m))?[key];
        try { return section.Check(mine); }
        catch (CommandException) { return section.Default; }
    }

    /// <summary>The project's pack's own section as stored, read from the model alone (null: Lapin's pack, or the section left to Lapin's values); the module completes it.</summary>
    public static JsonObject? RawSection(IModelView m, string key) =>
        ActiveId(m) is var id && id != Builtin && m.Get(id)?.Attrs["def"]?[key] is JsonObject o ? o : null;

    /// <summary>A section of the project's pack when the edition has it; null otherwise (a module reading another's).</summary>
    public static JsonObject? TrySection(IModelView m, IServices? s, string key) =>
        s != null && Sections(s).Any(x => x.Key == key) ? Section(m, s, key) : null;

    /// <summary>A whole pack as a command gives it: every section checked; one the edition does not know kept as it is (another edition's).</summary>
    static JsonObject Parse(IServices s, JsonNode? n)
    {
        if (n is not JsonObject o) throw new CommandException("A rule pack is an object of sections (stairs, rebar, bolts, roofs).");
        var checkedPack = new JsonObject();
        foreach (var (k, v) in o)
        {
            if (k is "format" or "version" or "name" or "indicative" or "description") continue;
            var section = Sections(s).FirstOrDefault(x => x.Key == k);
            try { checkedPack[k] = section != null ? section.Check(v) : v?.DeepClone(); }
            catch (CommandException e) { throw new CommandException($"{section?.Label ?? k}: {e.Message}"); }
        }
        return checkedPack;
    }

    static string FreeName(IModelView m, string name, string? except = null)
    {
        var taken = m.OfClass(Class).Where(e => e.Id != except).Select(e => e.Name).Append(BuiltinName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name)) return name;
        for (var n = 2; ; n++) if (!taken.Contains($"{name} {n}")) return $"{name} {n}";
    }

    static Entity Require(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: Class } e ? e
        : id == Builtin ? throw new CommandException("Lapin's rules stay as they are: duplicate them to change them.")
        : throw new CommandException("There is no such rule pack.");

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Rule pack", [], "#555555", Kind: "resource"));

        // The packs: Lapin's, then the project's; which one the project uses; the sections this edition reads.
        r.Query("rules.list", (q, _) =>
        {
            var m = q.Model;
            var active = ActiveId(m);
            var items = new JsonArray((JsonNode)new JsonObject { ["id"] = Builtin, ["name"] = BuiltinName, ["builtin"] = true, ["active"] = active == Builtin });
            foreach (var e in m.OfClass(Class).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                items.Add((JsonNode)new JsonObject { ["id"] = e.Id, ["name"] = e.Name, ["builtin"] = false, ["active"] = active == e.Id, ["description"] = e.Str("description") });
            return new JsonObject
            {
                ["items"] = items, ["active"] = active,
                ["sections"] = new JsonArray(Sections(q.Services).Select(x => (JsonNode)new JsonObject { ["key"] = x.Key, ["label"] = x.Label }).ToArray()),
            };
        });

        // A pack whole (the project's by default), each section completed with Lapin's values.
        r.Query("rules.get", (q, a) =>
        {
            var id = (string?)a["id"] is { Length: > 0 } given ? given : ActiveId(q.Model);
            var def = Def(q.Model, q.Services, id) ?? throw new CommandException("There is no such rule pack.");
            var o = new JsonObject { ["id"] = id, ["name"] = id == Builtin ? BuiltinName : q.Model.Get(id)?.Name, ["builtin"] = id == Builtin, ["indicative"] = true };
            var sections = new JsonObject();
            foreach (var s in Sections(q.Services))
                try { sections[s.Key] = s.Check(def[s.Key]); } catch (CommandException) { sections[s.Key] = s.Default; }
            o["sections"] = sections;
            o["defaults"] = new JsonObject(Sections(q.Services).Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Default.DeepClone())));
            return o;
        });

        // A pack of the project's: a copy of another (Lapin's too), or sections given; its name.
        r.Command("rules.create", "New rule pack", (ctx, a) =>
        {
            JsonObject def;
            string name;
            if ((string?)a["from"] is { Length: > 0 } from)
            {
                def = (JsonObject)(Def(ctx.Model, ctx.Services, from) ?? throw new CommandException("There is no such rule pack to copy.")).DeepClone();
                name = (string?)a["name"] is { Length: > 0 } n0 ? n0 : (from == Builtin ? BuiltinName : ctx.Model.Get(from)!.Name) + " copy";
            }
            else
            {
                def = Parse(ctx.Services, a["def"] ?? new JsonObject());
                name = (string?)a["name"] ?? "Rules";
            }
            var attrs = new JsonObject { ["def"] = def };
            if ((string?)a["description"] is { Length: > 0 } d) attrs["description"] = d;
            var e = new Entity(ctx.NewId(), Class, FreeName(ctx.Model, name.Trim()), attrs);
            ctx.Add(e);
            if (a["activate"] is JsonValue av && av.TryGetValue<bool>(out var act) && act) SetActive(ctx, e.Id);
            return new JsonObject { ["id"] = e.Id, ["name"] = e.Name };
        });

        // A pack's name, description or sections changed: {id, name?, description?, def? (whole), section?, values? (one section's)}.
        r.Command("rules.update", "Edit rule pack", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var attrs = e.CloneAttrs();
            var def = (attrs["def"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
            if (a["def"] is JsonObject whole) def = Parse(ctx.Services, whole);
            if ((string?)a["section"] is { Length: > 0 } key)
            {
                var section = Sections(ctx.Services).FirstOrDefault(x => x.Key == key) ?? throw new CommandException($"No module of this edition reads the rules' „{key}”.");
                def[key] = a["values"] == null ? null : section.Check(a["values"]);
                if (def[key] == null) def.Remove(key);
            }
            attrs["def"] = def;
            if (a.ContainsKey("description")) { if ((string?)a["description"] is { Length: > 0 } d) attrs["description"] = d; else attrs.Remove("description"); }
            var name = (string?)a["name"] is { } n ? n.Trim() is { Length: > 0 } nn ? FreeName(ctx.Model, nn, e.Id) : throw new CommandException("A rule pack needs a name.") : null;
            ctx.Replace(e.With(name: name, attrs: attrs));
            return null;
        });

        r.Command("rules.delete", "Delete rule pack", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            if (ActiveId(ctx.Model) == e.Id) SetActive(ctx, Builtin);
            ctx.Remove(e.Id);
            return null;
        });

        // The pack the project uses (Lapin's: none named).
        r.Command("rules.setActive", "Project rules", (ctx, a) =>
        {
            var id = (string?)a["id"] is { Length: > 0 } given ? given : Builtin;
            if (id != Builtin) Require(ctx.Model, id);
            SetActive(ctx, id);
            return null;
        });

        // A pack as a .lapinrules file: its sections as the project reads them, marked indicative.
        r.Query("rules.export", (q, a) =>
        {
            var id = (string?)a["id"] is { Length: > 0 } given ? given : ActiveId(q.Model);
            var def = Def(q.Model, q.Services, id) ?? throw new CommandException("There is no such rule pack.");
            var name = id == Builtin ? BuiltinName : q.Model.Get(id)!.Name;
            var o = new JsonObject { ["format"] = FileFormat, ["version"] = FileVersion, ["name"] = name, ["indicative"] = true };
            foreach (var (k, v) in Parse(q.Services, def)) o[k] = v?.DeepClone();
            return new JsonObject { ["text"] = o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ["file"] = name + ".lapinrules" };
        });

        // A .lapinrules file brought in as a pack of the project's (its name free), used at once when asked.
        r.Command("rules.import", "Import rule pack", (ctx, a) =>
        {
            JsonNode? n;
            try { n = JsonNode.Parse((string?)a["text"] ?? ""); }
            catch (JsonException) { throw new CommandException("That is not a rule pack (.lapinrules): it is not JSON."); }
            if (n is not JsonObject o || (string?)o["format"] != FileFormat) throw new CommandException("That is not a rule pack (.lapinrules).");
            if ((Json.Number(o["version"]) ?? 0) > FileVersion) throw new CommandException("That rule pack was written by a newer Lapin: update Lapin to read it.");
            var e = new Entity(ctx.NewId(), Class, FreeName(ctx.Model, ((string?)o["name"])?.Trim() is { Length: > 0 } nm ? nm : "Rules"), new JsonObject { ["def"] = Parse(ctx.Services, o) });
            if ((string?)o["description"] is { Length: > 0 } d) e.Attrs["description"] = d;
            ctx.Add(e);
            if (a["activate"] is JsonValue av && av.TryGetValue<bool>(out var act) && act) SetActive(ctx, e.Id);
            return new JsonObject { ["id"] = e.Id, ["name"] = e.Name };
        });

        // The project checked against its rules: each module's findings.
        r.Query("rules.check", (q, _) =>
        {
            var findings = new List<RuleFinding>();
            foreach (var c in q.Services.All<IRuleCheck>())
                try { findings.AddRange(c.Check(q)); } catch (CommandException) { }
            return new JsonObject
            {
                ["pack"] = ActiveId(q.Model) == Builtin ? BuiltinName : q.Model.Get(ActiveId(q.Model))!.Name,
                ["findings"] = new JsonArray(findings.Select(f => (JsonNode)new JsonObject { ["module"] = f.Module, ["element"] = f.ElementId, ["severity"] = f.Severity, ["message"] = f.Message }).ToArray()),
            };
        });
    }

    static void SetActive(ICommandContext ctx, string id)
    {
        var p = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = p.CloneAttrs();
        if (id == Builtin) attrs.Remove(Key); else attrs[Key] = id;
        ctx.Replace(p.With(attrs: attrs));
    }

    // ------------------------------------------------------------ for sections

    /// <summary>A number of a section: given, a finite number within [min, max]; absent: the default's.</summary>
    public static double Num(JsonObject given, JsonObject fallback, string key, double min, double max, string label)
    {
        if (given[key] is not { } v) return Json.Number(fallback[key]) ?? min;
        var n = Json.Number(v) ?? throw new CommandException($"{label} is a number.");
        return double.IsFinite(n) && n >= min && n <= max ? n : throw new CommandException($"{label} is between {min} and {max}.");
    }
}
