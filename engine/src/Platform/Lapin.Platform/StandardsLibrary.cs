using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Import;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Line types of the project's own and the drafting standards as a library (docs/plan-user-extensibility.md §3.6–3.7). Line types:
/// made in Lapin or read from AutoCAD's <c>.lin</c>, kept in the project's standards (<c>drafting.lineTypes</c>), named by drafting
/// layers, the standards' layers, axis styles and object styles. The standards' file, <c>.lapinstandards</c>: text, dimension and
/// axis styles, tag types, layers and their groups, line types, hatch patterns, symbols and the marks' symbols, and with
/// <c>options</c> the project's units, scales, marker sizes, object styles and pen table; brought into a project entry by entry,
/// one that differs taking the choice given (update, keep both, skip). Each command one undo step.
/// </summary>
public static class StandardsLibrary
{
    public const string FileFormat = "lapin-standards";
    public const int FileVersion = 1;

    static Entity Project(IModelView m) => Spatial.Project(m) ?? throw new CommandException("The model has no project.");

    static void WriteBook(ICommandContext ctx, JsonObject raw)
    {
        var project = Project(ctx.Model);
        var attrs = project.CloneAttrs();
        attrs["drafting"] = Standards.ToJson(Standards.Read(raw));
        ctx.Replace(project.With(attrs: attrs));
    }

    static JsonObject LineTypeJson(LineTypeDef l)
    {
        var o = new JsonObject { ["id"] = l.Name, ["name"] = l.Name, ["dashes"] = new JsonArray(l.Dashes.Select(d => (JsonNode)d).ToArray()) };
        if (l.Description != null) o["description"] = l.Description;
        return o;
    }

    static double[] CheckDashes(JsonNode? n)
    {
        if (n is not JsonArray a || a.Count < 2 || a.Count > 24) throw new CommandException("A line type is dashes and gaps (mm), two to 24 of them.");
        var d = a.Select(x => Json.Number(x) ?? double.NaN).ToArray();
        if (d.Any(x => !(x > 0 && x < 100))) throw new CommandException("Each dash and gap is above 0 and under 100 mm.");
        return d;
    }

    /// <summary>A line type's name changed or gone everywhere it is named: drafting layers, the standards' layers and axis styles, object styles (gone: continuous).</summary>
    static void Repoint(ICommandContext ctx, JsonObject raw, string from, string? to)
    {
        foreach (var x in (raw["layers"] as JsonArray ?? []).OfType<JsonObject>()) if (string.Equals((string?)x["lineType"], from, StringComparison.OrdinalIgnoreCase)) x["lineType"] = to;
        foreach (var x in (raw["axisStyles"] as JsonArray ?? []).OfType<JsonObject>()) if (string.Equals((string?)x["lineType"], from, StringComparison.OrdinalIgnoreCase)) x["lineType"] = to ?? "dashdot";
        var project = Project(ctx.Model);
        var attrs = project.CloneAttrs();
        if (attrs["draftLayers"] is JsonArray dl)
            foreach (var x in dl.OfType<JsonObject>()) if (string.Equals((string?)x["lineType"], from, StringComparison.OrdinalIgnoreCase)) x["lineType"] = to ?? "continuous";
        if (attrs["objectStyles"] is JsonObject os)
            foreach (var (_, cat) in os) foreach (var (_, spec) in cat as JsonObject ?? []) if (spec is JsonObject so && string.Equals((string?)so["pattern"], from, StringComparison.OrdinalIgnoreCase)) { if (to == null) so.Remove("pattern"); else so["pattern"] = to; }
        ctx.Replace(project.With(attrs: attrs));
    }

    // ------------------------------------------------------------ the standards' file

    /// <summary>The project's standards as a file: the drafting standards, its hatch patterns, its symbols and the marks' symbols by name, and its options.</summary>
    public static JsonObject Export(IModelView m, string? name)
    {
        var project = Project(m);
        var o = new JsonObject { ["format"] = FileFormat, ["version"] = FileVersion };
        if (name is { Length: > 0 }) o["name"] = name;
        o["drafting"] = Standards.ToJson(DraftingResources.Book(m));
        if (project.Str("hatchPatterns") is { Length: > 0 } pat) o["hatchPatterns"] = pat;
        var symbols = m.OfClass(Symbols.Class).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (symbols.Count > 0) o["symbols"] = new JsonArray(symbols.Select(e => (JsonNode)new JsonObject { ["name"] = e.Name, ["def"] = e.Attrs["def"]?.DeepClone() }).ToArray());
        // The marks' symbols by name (a built-in by its id), so the file carries no ids of this project.
        if (project.Attrs[SymbolSlots.Key] is JsonObject slots)
            o["symbolSlots"] = new JsonObject(slots.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)(BuiltinSymbols.Is((string?)kv.Value) ? (string?)kv.Value : Symbols.NameOf(m, (string?)kv.Value)))));
        if (project.Attrs["objectStyles"] is JsonObject ost) o["objectStyles"] = ost.DeepClone();
        if (project.Attrs["penTable"] is JsonObject pt) o["penTable"] = pt.DeepClone();
        return o;
    }

    static JsonObject ReadFile(string text)
    {
        JsonNode? n;
        try { n = JsonNode.Parse(text); }
        catch (JsonException) { throw new CommandException("That is not a standards file (.lapinstandards): it is not JSON."); }
        if (n is not JsonObject o || (string?)o["format"] != FileFormat) throw new CommandException("That is not a standards file (.lapinstandards).");
        if ((Json.Number(o["version"]) ?? 0) > FileVersion) throw new CommandException("That standards file was written by a newer Lapin: update Lapin to read it.");
        return o;
    }

    /// <summary>The lists the file and the project both hold, by kind: (kind, the list's key in the standards).</summary>
    static readonly (string Kind, string Key)[] Lists =
        [("text", "textStyles"), ("dim", "dimStyles"), ("axis", "axisStyles"), ("tag", "tagTypes"), ("layer", "layers"), ("layerGroup", "layerGroups"), ("lineType", "lineTypes")];

    /// <summary>Every entry of the file with whether the project has one of its id (a line type: its name) and whether it is the same.</summary>
    static List<(string Kind, string Id, string Name, bool Exists, bool Same)> Compare(IModelView m, JsonObject file)
    {
        var o = new List<(string, string, string, bool, bool)>();
        // Both read as the project reads them, so a field written another way compares the same.
        var theirs = Standards.ToJson(Standards.Read(file["drafting"]));
        var mine = Standards.ToJson(DraftingResources.Book(m));
        foreach (var (kind, key) in Lists)
            foreach (var e in (theirs[key] as JsonArray ?? []).OfType<JsonObject>())
            {
                var id = (string?)e["id"] ?? (string?)e["name"] ?? "";
                var have = (mine[key] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(x => string.Equals((string?)x["id"] ?? (string?)x["name"], id, StringComparison.OrdinalIgnoreCase));
                o.Add((kind, id, (string?)e["name"] ?? id, have != null, have != null && JsonNode.DeepEquals(have, e)));
            }
        var myPatterns = DraftingResources.Patterns(m);
        if ((string?)file["hatchPatterns"] is { Length: > 0 } pt)
            foreach (var p in Pat.Parse(pt).Patterns)
            {
                var have = myPatterns.FirstOrDefault(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                o.Add(("pattern", p.Name, p.Name, have != null, have != null && Pat.Serialize([have]) == Pat.Serialize([p])));
            }
        foreach (var s in (file["symbols"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var name = (string?)s["name"] ?? "";
            var have = m.OfClass(Symbols.Class).FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            o.Add(("symbol", name, name, have != null, have != null && SymbolDef.From(have.Attrs["def"]) is { } d && SymbolDef.From(s["def"]) is { } fd && JsonNode.DeepEquals(d.ToJson(), fd.ToJson())));
        }
        return o;
    }

    /// <summary>A free id and name in a list for a copy kept beside the project's: „Name 2”, id-2.</summary>
    static (string Id, string Name) Free(JsonArray list, string id, string name)
    {
        var ids = list.OfType<JsonObject>().Select(x => (string?)x["id"] ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = list.OfType<JsonObject>().Select(x => (string?)x["name"] ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var n = 2; ; n++) if (!ids.Contains($"{id}-{n}") && !names.Contains($"{name} {n}")) return ($"{id}-{n}", $"{name} {n}");
    }

    public static void Register(IModuleRegistry r)
    {
        // Lapin's line types and the project's, each with its dashes (mm).
        r.Query("standards.lineTypes", (q, _) => new JsonObject
        {
            ["builtin"] = new JsonArray(Standards.LineTypes.Concat(LineStyles.Patterns).Distinct().Where(n => n is not ("continuous" or "solid"))
                .Select(n => (JsonNode)new JsonObject { ["name"] = n, ["dashes"] = new JsonArray(Dashes.Of(n).Select(d => (JsonNode)d).ToArray()) }).ToArray()),
            ["own"] = new JsonArray((DraftingResources.Book(q.Model).LineTypes ?? []).Select(l => (JsonNode)LineTypeJson(l)).ToArray()),
        });

        // A line type of the project's made or changed: {name, dashes, description?, newName?} (renamed, what names it follows).
        r.Command("standards.setLineType", "Line type", (ctx, a) =>
        {
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 and <= 64 } n ? n : throw new CommandException("A line type needs a name (64 characters at most).");
            var newName = ((string?)a["newName"])?.Trim() is { Length: > 0 and <= 64 } nn ? nn : name;
            foreach (var x in new[] { name, newName })
                if (Standards.LineTypes.Contains(x, StringComparer.OrdinalIgnoreCase) || LineStyles.Patterns.Contains(x, StringComparer.OrdinalIgnoreCase))
                    throw new CommandException($"„{x}” is one of Lapin's own line types.");
            var raw = Standards.ToJson(DraftingResources.Book(ctx.Model));
            var list = raw["lineTypes"] as JsonArray ?? [];
            var at = list.OfType<JsonObject>().FirstOrDefault(x => string.Equals((string?)x["name"], name, StringComparison.OrdinalIgnoreCase));
            if (newName != name && list.OfType<JsonObject>().Any(x => string.Equals((string?)x["name"], newName, StringComparison.OrdinalIgnoreCase)))
                throw new CommandException($"There is a line type „{newName}” already.");
            var dashes = a["dashes"] != null ? CheckDashes(a["dashes"]) : at?["dashes"] is JsonArray old ? CheckDashes(old) : throw new CommandException("A new line type needs its dashes.");
            var entry = LineTypeJson(new LineTypeDef(newName, newName, dashes, ((string?)a["description"])?.Trim() is { Length: > 0 } d ? d : (string?)at?["description"]));
            if (at != null) list[list.IndexOf(at)] = entry; else list.Add((JsonNode)entry);
            raw["lineTypes"] = list;
            if (newName != name) Repoint(ctx, raw, name, newName);
            WriteBook(ctx, raw);
            return new JsonObject { ["name"] = newName };
        });

        // A line type of the project's removed: what named it draws continuous.
        r.Command("standards.removeLineType", "Remove line type", (ctx, a) =>
        {
            var name = (string?)a["name"] ?? throw new CommandException("Which line type?");
            var raw = Standards.ToJson(DraftingResources.Book(ctx.Model));
            var list = raw["lineTypes"] as JsonArray ?? [];
            if (!list.OfType<JsonObject>().Any(x => string.Equals((string?)x["name"], name, StringComparison.OrdinalIgnoreCase))) throw new CommandException("The project has no such line type.");
            raw["lineTypes"] = new JsonArray(list.OfType<JsonObject>().Where(x => !string.Equals((string?)x["name"], name, StringComparison.OrdinalIgnoreCase)).Select(x => (JsonNode)x.DeepClone()).ToArray());
            Repoint(ctx, raw, name, null);
            WriteBook(ctx, raw);
            return null;
        });

        static double Unit(JsonObject a) => (string?)a["units"] is "in" or "inch" or "inches" ? 25.4 : 1;

        // A .lin file read: its line types with their dashes (mm), whether each had text or shapes left out, whether the project has one of that name and the same.
        r.Query("standards.readLin", (q, a) =>
        {
            var skipped = new List<string>();
            var read = LinFile.Parse((string?)a["text"] ?? "", Unit(a), skipped);
            var own = DraftingResources.Book(q.Model).LineTypes ?? [];
            return new JsonObject
            {
                ["lineTypes"] = new JsonArray(read.Select(l =>
                {
                    var have = own.FirstOrDefault(x => string.Equals(x.Name, l.Name, StringComparison.OrdinalIgnoreCase));
                    return (JsonNode)new JsonObject
                    {
                        ["name"] = l.Name, ["description"] = l.Description, ["complex"] = l.Complex, ["builtin"] = Standards.LineTypes.Contains(l.Name, StringComparer.OrdinalIgnoreCase) || LineStyles.Patterns.Contains(l.Name, StringComparer.OrdinalIgnoreCase),
                        ["dashes"] = new JsonArray(l.Dashes.Select(d => (JsonNode)d).ToArray()), ["exists"] = have != null, ["same"] = have != null && have.Dashes.SequenceEqual(l.Dashes),
                    };
                }).ToArray()),
                ["skipped"] = new JsonArray(skipped.Select(s => (JsonNode)s).ToArray()),
            };
        });

        // A .lin file's line types brought in ({text, units: mm | in, names?, choices?: {name: update | both | skip}, all?}): one the project has that differs takes its choice (default: update).
        r.Command("standards.importLin", "Import line types", (ctx, a) =>
        {
            var read = LinFile.Parse((string?)a["text"] ?? "", Unit(a));
            if (read.Count == 0) throw new CommandException("The file has no line types Lapin can read (a .lin file: *NAME, then A, dash, -gap, …).");
            var only = a["names"] is JsonArray na ? na.Select(x => (string?)x).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
            var raw = Standards.ToJson(DraftingResources.Book(ctx.Model));
            var list = raw["lineTypes"] as JsonArray ?? [];
            int added = 0, updated = 0, skipped = 0;
            foreach (var l in read)
            {
                if (only != null && !only.Contains(l.Name)) continue;
                // Lapin's own names stay Lapin's: the file's comes in beside them.
                var name = Standards.LineTypes.Contains(l.Name, StringComparer.OrdinalIgnoreCase) || LineStyles.Patterns.Contains(l.Name, StringComparer.OrdinalIgnoreCase) ? l.Name + " (file)" : l.Name;
                var at = list.OfType<JsonObject>().FirstOrDefault(x => string.Equals((string?)x["name"], name, StringComparison.OrdinalIgnoreCase));
                var entry = LineTypeJson(new LineTypeDef(name, name, l.Dashes, l.Description));
                if (at == null) { list.Add((JsonNode)entry); added++; continue; }
                if (JsonNode.DeepEquals(at["dashes"], entry["dashes"])) continue;
                switch ((string?)a["choices"]?[l.Name] ?? (string?)a["all"] ?? "update")
                {
                    case "skip": skipped++; break;
                    case "both":
                        var (_, free) = Free(list, name, name);
                        list.Add((JsonNode)LineTypeJson(new LineTypeDef(free, free, l.Dashes, l.Description)));
                        added++;
                        break;
                    default: list[list.IndexOf(at)] = entry; updated++; break;
                }
            }
            raw["lineTypes"] = list;
            WriteBook(ctx, raw);
            return new JsonObject { ["added"] = added, ["updated"] = updated, ["skipped"] = skipped };
        });

        // The project's line types (all, or those named) as a .lin file in mm.
        r.Query("standards.exportLin", (q, a) =>
        {
            var only = a["names"] is JsonArray na ? na.Select(x => (string?)x).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
            var types = (DraftingResources.Book(q.Model).LineTypes ?? []).Where(l => only == null || only.Contains(l.Name)).ToList();
            if (types.Count == 0) throw new CommandException("The project has no line types of its own to write.");
            return new JsonObject { ["text"] = LinFile.Write(types.Select(l => (l.Name, l.Description, l.Dashes))), ["file"] = "lapin.lin" };
        });

        // The project's standards as a .lapinstandards file.
        r.Query("standards.export", (q, a) =>
        {
            var o = Export(q.Model, (string?)a["name"]);
            return new JsonObject { ["text"] = o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ["file"] = ((string?)a["name"] is { Length: > 0 } n ? n : "standards") + ".lapinstandards" };
        });

        // A standards file read: every entry with whether the project has it and whether it is the same; whether it carries options.
        r.Query("standards.readPack", (q, a) =>
        {
            var file = ReadFile((string?)a["text"] ?? "");
            return new JsonObject
            {
                ["name"] = (string?)file["name"],
                ["entries"] = new JsonArray(Compare(q.Model, file).Select(e => (JsonNode)new JsonObject { ["kind"] = e.Kind, ["id"] = e.Id, ["name"] = e.Name, ["exists"] = e.Exists, ["same"] = e.Same }).ToArray()),
                ["options"] = file["objectStyles"] != null || file["penTable"] != null || file["symbolSlots"] != null,
            };
        });

        // A standards file brought in, one undo step: each entry the project lacks added, one the same kept, one that differs taking
        // its choice ({"kind:id": update | both | skip}, else `all`, else update); `options`: the units, scales, marker sizes, object
        // styles, pen table and the marks' symbols too.
        r.Command("standards.importFrom", "Import drafting standards", (ctx, a) =>
        {
            var file = ReadFile((string?)a["text"] ?? "");
            var choices = a["choices"] as JsonObject;
            string Choice(string kind, string id) => (string?)choices?[$"{kind}:{id}"] ?? (string?)a["all"] ?? "update";
            var withOptions = a["options"] is JsonValue ov && ov.TryGetValue<bool>(out var opt) && opt;
            int added = 0, updated = 0, kept = 0, skipped = 0;

            // The drafting standards' lists.
            var theirs = Standards.ToJson(Standards.Read(file["drafting"]));
            var raw = Standards.ToJson(DraftingResources.Book(ctx.Model));
            foreach (var (kind, key) in Lists)
            {
                var list = raw[key] as JsonArray ?? [];
                foreach (var e in (theirs[key] as JsonArray ?? []).OfType<JsonObject>())
                {
                    var id = (string?)e["id"] ?? (string?)e["name"] ?? "";
                    var at = list.OfType<JsonObject>().FirstOrDefault(x => string.Equals((string?)x["id"] ?? (string?)x["name"], id, StringComparison.OrdinalIgnoreCase));
                    if (at == null) { list.Add((JsonNode)e.DeepClone()); added++; continue; }
                    if (JsonNode.DeepEquals(at, e)) { kept++; continue; }
                    switch (Choice(kind, id))
                    {
                        case "skip": skipped++; break;
                        case "both":
                        {
                            var copy = (JsonObject)e.DeepClone();
                            var (nid, nname) = Free(list, id, (string?)e["name"] ?? id);
                            copy["id"] = kind == "lineType" ? nname : nid;
                            copy["name"] = nname;
                            list.Add((JsonNode)copy);
                            added++;
                            break;
                        }
                        default: list[list.IndexOf(at)] = e.DeepClone(); updated++; break;
                    }
                }
                raw[key] = list;
            }
            if (withOptions)
                foreach (var k in new[] { "units", "lineCap", "areaRounding", "suppressZeros", "levelDecimals", "levelZeros", "sectionMarkSize", "sectionMarkText", "elevationMarkSize", "elevationMarkText", "scales", "planScale", "sectionScale", "elevationScale", "defaultAxisStyle" })
                    if (theirs[k] != null) raw[k] = theirs[k]!.DeepClone();
            WriteBook(ctx, raw);

            // Hatch patterns by name.
            if ((string?)file["hatchPatterns"] is { Length: > 0 } pt)
            {
                var mine = DraftingResources.Patterns(ctx.Model).ToList();
                foreach (var p in Pat.Parse(pt).Patterns)
                {
                    var i = mine.FindIndex(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                    if (i < 0) { mine.Add(p); added++; continue; }
                    if (Pat.Serialize([mine[i]]) == Pat.Serialize([p])) { kept++; continue; }
                    switch (Choice("pattern", p.Name))
                    {
                        case "skip": skipped++; break;
                        case "both":
                            var n = 2;
                            while (mine.Any(x => string.Equals(x.Name, $"{p.Name}_{n}", StringComparison.OrdinalIgnoreCase))) n++;
                            mine.Add(p with { Name = $"{p.Name}_{n}" });
                            added++;
                            break;
                        default: mine[i] = p; updated++; break;
                    }
                }
                var project = Project(ctx.Model);
                var attrs = project.CloneAttrs();
                attrs["hatchPatterns"] = Pat.Serialize(mine);
                ctx.Replace(project.With(attrs: attrs));
            }

            // Symbols, through the symbols' own import (its choices by name).
            if (file["symbols"] is JsonArray { Count: > 0 } syms)
            {
                var catalog = ctx.Services as ModuleCatalog ?? throw new CommandException("No commands to import through here.");
                var pack = new JsonObject { ["format"] = Symbols.FileFormat, ["version"] = Symbols.FileVersion, ["symbols"] = syms.DeepClone() };
                var symChoices = new JsonObject();
                foreach (var s in syms.OfType<JsonObject>()) if ((string?)s["name"] is { } sn) symChoices[sn] = Choice("symbol", sn);
                var result = catalog.Command("symbols.importFrom")!.Handler(ctx, new JsonObject { ["text"] = pack.ToJsonString(), ["choices"] = symChoices });
                added += (int?)result?["added"] ?? 0; updated += (int?)result?["updated"] ?? 0; kept += (int?)result?["kept"] ?? 0; skipped += (int?)result?["skipped"] ?? 0;
            }

            if (withOptions)
            {
                var project = Project(ctx.Model);
                var attrs = project.CloneAttrs();
                if (file["objectStyles"] is JsonObject os) attrs["objectStyles"] = os.DeepClone();
                if (file["penTable"] is JsonObject ptab) attrs["penTable"] = ptab.DeepClone();
                // The marks' symbols, named: a built-in by its id, else the project's symbol of that name.
                if (file["symbolSlots"] is JsonObject slots)
                {
                    var mine = attrs[SymbolSlots.Key] as JsonObject is { } had ? (JsonObject)had.DeepClone() : new JsonObject();
                    foreach (var (slot, v) in slots)
                    {
                        if (!SymbolSlots.Known(slot) || (string?)v is not { Length: > 0 } named) continue;
                        var id = BuiltinSymbols.Is(named) ? named : ctx.Model.OfClass(Symbols.Class).FirstOrDefault(e => e.Name.Equals(named, StringComparison.OrdinalIgnoreCase))?.Id;
                        if (id != null) mine[slot] = id;
                    }
                    if (mine.Count > 0) attrs[SymbolSlots.Key] = mine;
                }
                ctx.Replace(project.With(attrs: attrs));
            }
            return new JsonObject { ["added"] = added, ["updated"] = updated, ["kept"] = kept, ["skipped"] = skipped };
        });
    }
}
