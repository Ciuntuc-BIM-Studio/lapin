using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// The layered types taken out of a project and brought into another (report #52, decision 242): wall and floor types
/// with their layers (material, thickness, the core, a layer's own name, its height on a wall), as JSON or as CSV a
/// spreadsheet writes, one row a layer, a type's rows one after another. Brought in, a type of that kind and name is
/// changed to the file's layers, any other made; materials the project lacks are made by name. The assemblies are also
/// a schedule (<see cref="TypeAssemblies"/>).
/// <para>The JSON (version 2, a tester's report: nothing lost in silence) also carries each type's parameters (its IFC
/// properties, instance defaults marked) and the definition of every material its layers use (hatches, fills, pens,
/// colour, parameters), so another project makes them as they were; a material or a type the project has by name that
/// differs is the user's to settle (<see cref="LibraryMerge"/>: <c>architecture.readTypes</c> says what differs). The CSV
/// is a spreadsheet's: layers only. A whole library (any kind of type, profiles, zones) is a type pack (<see cref="TypePacks"/>).</para>
/// </summary>
public static class TypeExchange
{
    public const string Format = "lapin-types";
    static readonly (string Kind, string Class)[] Kinds = [("wall", IfcClass.WallType), ("floor", IfcClass.SlabType)];
    static readonly string[] Header = ["Kind", "Type", "Mark", "Layer", "Material", "Thickness (mm)", "Core", "From (mm)", "To (mm)", "Note"];

    public static IEnumerable<(string Kind, Entity Type)> Types(IModelView m, IReadOnlyCollection<string>? kinds = null) =>
        Kinds.Where(k => kinds is null || kinds.Count == 0 || kinds.Contains(k.Kind))
             .SelectMany(k => m.OfClass(k.Class).OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).Select(t => (k.Kind, t)));

    /// <summary>
    /// The types as a JSON document: <c>{format, version: 2, types: [{kind, name, mark?, layers: [{material, thickness, core?, color?, name?, from?, to?,
    /// priority?, note?, category?, extendDown?, extendUp?, parts?: [{material, height}]}], sweeps?: [{profile: {name, points, insertion}, mode, side, height, fromTop?, offset?, material?, flip?,
    /// setbackStart?, setbackEnd?, returns?, returnsAtOpenings?, gaps?}], params?: [{name, kind, value, pset?, options?, instance?}]}], materials: [{name, attrs}]}</c>,
    /// metres; a material by its name, its attrs as the project holds them.
    /// </summary>
    public static JsonObject Json(IModelView m, IReadOnlyCollection<string>? kinds = null)
    {
        var types = new JsonArray();
        var used = new List<string>();
        foreach (var (kind, t) in Types(m, kinds))
        {
            types.Add((JsonNode)TypeJson(m, kind, t));
            foreach (var l in LayerSets.LayersOf(m, t.Id))
            {
                used.Add(l.Material);
                foreach (var p in l.Parts ?? []) used.Add(p.Material);
            }
            foreach (var d in WallSweepTypes.Defs(t)) if ((string?)d["material"] is { } mid) used.Add(mid);
        }
        var materials = new JsonArray();
        foreach (var id in used.Distinct())
            if (m.Get(id) is { Class: IfcClass.Material } mat)
            {
                var attrs = mat.CloneAttrs();
                foreach (var k in Volatile) attrs.Remove(k);
                materials.Add((JsonNode)new JsonObject { ["name"] = mat.Name, ["attrs"] = attrs });
            }
        return LibraryFile.Write(Spec, new JsonObject { ["types"] = types, ["materials"] = materials }, LibraryFile.SourceOf(m));
    }

    public static LibraryFile.Spec Spec => LibraryFile.Of(Format);

    /// <summary>What a project writes of a material for itself: not carried, not compared.</summary>
    static readonly string[] Volatile = ["added", "imported"];

    /// <summary>One type as the JSON has it.</summary>
    static JsonObject TypeJson(IModelView m, string kind, Entity t)
    {
        var layers = new JsonArray();
        foreach (var l in LayerSets.LayersOf(m, t.Id))
        {
            var mat = m.Get(l.Material);
            var o = new JsonObject { ["material"] = mat?.Name ?? l.Material, ["thickness"] = Math.Round(l.Thickness, 6) };
            if (l.Core) o["core"] = true;
            if (mat?.Str("color") is { } c) o["color"] = c;
            if (l.Name is { Length: > 0 } n) o["name"] = n;
            if (l.From is { } f) o["from"] = Math.Round(f, 6);
            if (l.To is { } to) o["to"] = Math.Round(to, 6);
            if (l.Priority is { } pr) o["priority"] = pr;
            if (l.Note is { Length: > 0 } note) o["note"] = note;
            if (l.Category is { Length: > 0 } cat) o["category"] = cat;
            if (l.ExtendDown == true) o["extendDown"] = true;
            if (l.ExtendUp == true) o["extendUp"] = true;
            // Its own graphics over its material's (decision 756): its pens by scale travel with the type.
            if (l.Graphics is { Count: > 0 } lg) o["graphics"] = lg.DeepClone();
            if (l.Parts is { Count: > 0 } parts)
                o["parts"] = new JsonArray(parts.Select(p => (JsonNode)new JsonObject
                {
                    ["material"] = m.Get(p.Material)?.Name ?? p.Material, ["height"] = Math.Round(p.Height, 6),
                    ["color"] = m.Get(p.Material)?.Str("color"), ["name"] = p.Name,
                }).ToArray());
            layers.Add((JsonNode)o);
        }
        var type = new JsonObject { ["kind"] = kind, ["name"] = t.Name };
        if (t.Str("mark") is { Length: > 0 } mark) type["mark"] = mark;
        type["layers"] = layers;
        // A wall type's sweeps and reveals (decision 298): each profile's outline with its name, the material by name.
        if (kind == "wall" && WallSweepTypes.Export(m, t) is { } sweeps) type[WallSweepTypes.Key] = sweeps;
        // Its parameters (its IFC properties: Pset_WallCommon.FireRating, a set of the user's; instance defaults marked).
        if (t.Attrs["params"] is JsonArray { Count: > 0 } ps) type["params"] = ps.DeepClone();
        return type;
    }

    /// <summary>The types as CSV (UTF-8, comma-separated, a header row): one row a layer, thicknesses in millimetres.</summary>
    public static string Csv(IModelView m, IReadOnlyCollection<string>? kinds = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', Header.Select(Cell)));
        foreach (var t in (Json(m, kinds)["types"] as JsonArray)!.OfType<JsonObject>())
        {
            var i = 0;
            foreach (var l in (t["layers"] as JsonArray)!.OfType<JsonObject>())
                sb.AppendLine(string.Join(',', new[]
                {
                    (string)t["kind"]!, (string)t["name"]!, (string?)t["mark"] ?? "", (string?)l["name"] ?? (++i).ToString(CultureInfo.InvariantCulture),
                    (string)l["material"]!, Mm(l["thickness"]), l["core"] is not null ? "yes" : "", Mm(l["from"]), Mm(l["to"]), ((string?)l["note"] ?? "").Replace("\r\n", " ").Replace('\n', ' '),
                }.Select(Cell)));
        }
        return sb.ToString();
    }

    static string Mm(JsonNode? metres) => Lapin.Model.Json.Number(metres) is { } v ? Math.Round(v * 1000, 3).ToString(CultureInfo.InvariantCulture) : "";
    static string Cell(string s) => CsvText.Field(s);

    /// <summary>A type as a file gives it: its layers checked, its sweeps and parameters as written (null: the file says nothing of them), the file's own object for comparing.</summary>
    public sealed record FileType(string Kind, string Name, string? Mark, IReadOnlyList<LayerSpec> Layers, JsonArray? Sweeps, JsonArray? Params, JsonObject Source);

    /// <summary>
    /// Types from a JSON document (as <see cref="Json"/> writes it, or a bare array of types) or CSV text: one row a
    /// layer under the header's names (any order, comma, semicolon or tab between cells, a decimal comma where the
    /// separator is not one), a type's layers in the order given; the kind "wall" when the file says none.
    /// <paramref name="rename"/>: a material's name as the project will have it (a library's kept beside the project's).
    /// </summary>
    public static List<FileType> Read(string text, IReadOnlyDictionary<string, string>? rename = null)
    {
        var trimmed = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            var list = Doc(trimmed) is JsonObject o ? o["types"] as JsonArray : Doc(trimmed) as JsonArray;
            if (list is null) throw new CommandException("The JSON has no list of types.");
            return list.OfType<JsonObject>().Select(t =>
            {
                if (rename is { Count: > 0 }) t = (JsonObject)Renamed(t, rename, false)!;
                var name = ((string?)t["name"])?.Trim();
                if (string.IsNullOrEmpty(name)) throw new CommandException("Every type needs a name.");
                if (t["params"] is not (null or JsonArray)) throw new CommandException($"The parameters of „{name}” are not a list.");
                return new FileType(KindOf((string?)t["kind"]), name, ((string?)t["mark"])?.Trim() is { Length: > 0 } mk ? mk : null, LayerSets.Parse(t["layers"], $"The type „{name}”"),
                    t[WallSweepTypes.Key] as JsonArray, t["params"] as JsonArray, t);
            }).ToList();
        }
        return ReadCsv(trimmed);
    }

    static JsonNode? Doc(string json)
    {
        JsonNode? n;
        try { n = JsonNode.Parse(json); } catch (Exception e) { throw new CommandException($"The file is not JSON a type list reads: {e.Message}"); }
        // A file that says it is Lapin's (any other JSON with a list of types reads as it always did) through the one envelope: one from a newer Lapin refused.
        return n is JsonObject o && (string?)(o["format"] as JsonValue) == Format ? LibraryFile.Read(o, Spec).Root : n;
    }

    /// <summary>
    /// What an import would do (<see cref="LibraryMerge"/>): each type new, the same as the project's of its kind and name, or of
    /// the same name made otherwise (updated by default, as this import always did); each material new, the same, or of the same
    /// name defined otherwise (the project's kept by default: „skip”).
    /// </summary>
    static List<LibraryMerge.Row> Rows(IModelView m, string text)
    {
        var preview = Preview(m, text);
        var rows = new List<LibraryMerge.Row>();
        foreach (var t in (preview["types"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var differs = (t["differs"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList();
            var match = (bool?)t["exists"] != true ? LibraryMerge.Match.New : differs is { Count: > 0 } ? LibraryMerge.Match.Name : LibraryMerge.Match.Same;
            rows.Add(new LibraryMerge.Row("type:" + (string?)t["name"], (string?)t["kind"] == "floor" ? "Floor type" : "Wall type", (string?)t["name"] ?? "", match, differs, Default: LibraryMerge.Update));
        }
        var conflicts = (preview["materials"] as JsonArray ?? []).OfType<JsonObject>().ToDictionary(x => (string?)x["name"] ?? "", x => (x["differs"] as JsonArray)?.Select(d => (string?)d).OfType<string>().ToList(), StringComparer.OrdinalIgnoreCase);
        foreach (var (name, _) in ReadMaterials(text))
        {
            var d = conflicts.GetValueOrDefault(name);
            rows.Add(new LibraryMerge.Row("material:" + name, "Material", name,
                Materials.Find(m, name) is null ? LibraryMerge.Match.New : conflicts.ContainsKey(name) ? LibraryMerge.Match.Name : LibraryMerge.Match.Same, d, Default: LibraryMerge.Skip));
        }
        return rows;
    }

    static JsonObject ImportAll(ICommandContext ctx, string text, JsonObject? choices, string? all)
    {
        var materials = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Rows(ctx.Model, text).Where(x => x.Conflict))
        {
            var c = LibraryMerge.Decide(row, choices, all);
            if (row.Key.StartsWith("material:", StringComparison.Ordinal)) materials[row.Key[9..]] = LibraryMerge.Word(c, LibraryMerge.MaterialChoices);
            else types[row.Key[5..]] = c;
        }
        var result = Import(ctx, text, materials, types);
        return ResultJson(result);
    }

    static JsonObject ResultJson(Result result) => new()
    {
        ["created"] = result.Created, ["updated"] = result.Updated, ["skipped"] = result.Skipped,
        ["renamed"] = new JsonArray(result.Renamed.Concat(result.Materials.Renamed).Select(n => (JsonNode)n).ToArray()),
        ["materials"] = result.Materials.ToJson(),
        ["added"] = result.Created + result.Materials.Made, ["kept"] = result.Materials.Kept,
    };

    /// <summary>The materials a JSON file defines (version 2): each name and its attrs; none for CSV or an older file.</summary>
    public static List<(string Name, JsonObject Attrs)> ReadMaterials(string text)
    {
        var trimmed = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (!trimmed.StartsWith('{') || Doc(trimmed) is not JsonObject o || o["materials"] is not JsonArray list) return [];
        var found = new List<(string, JsonObject)>();
        foreach (var x in list.OfType<JsonObject>())
        {
            if (((string?)x["name"])?.Trim() is not { Length: > 0 } name) throw new CommandException("Every material of the file needs a name.");
            if (x["attrs"] is not JsonObject attrs) throw new CommandException($"The material „{name}” has no definition (attrs).");
            if (!found.Any(f => string.Equals(f.Item1, name, StringComparison.OrdinalIgnoreCase))) found.Add((name, (JsonObject)attrs.DeepClone()));
        }
        return found;
    }

    /// <summary>A type's JSON with the materials it names (under a key naming a material) as <paramref name="rename"/> has them.</summary>
    static JsonNode? Renamed(JsonNode? n, IReadOnlyDictionary<string, string> rename, bool material) => n switch
    {
        JsonObject o => new JsonObject(o.Select(kv => KeyValuePair.Create(kv.Key, Renamed(kv.Value, rename, material || kv.Key.Contains("material", StringComparison.OrdinalIgnoreCase))))),
        JsonArray a => new JsonArray(a.Select(v => Renamed(v, rename, material)).ToArray()),
        JsonValue v when material && v.TryGetValue<string>(out var s) && rename.TryGetValue(s.Trim(), out var to) => JsonValue.Create(to),
        _ => n?.DeepClone(),
    };

    static string KindOf(string? kind) => (kind ?? "wall").Trim().ToLowerInvariant() switch
    {
        "" or "wall" or "walls" or "perete" => "wall",
        "floor" or "floors" or "slab" or "slabs" or "placa" or "placă" => "floor",
        var k => throw new CommandException($"„{k}” is not a kind of type Lapin brings in: wall or floor."),
    };

    static List<FileType> ReadCsv(string text)
    {
        var lines = CsvText.Read(text).Where(r => r.Any(c => c.Trim().Length > 0)).ToList();
        if (lines.Count < 2) throw new CommandException("The CSV has no layers under its header.");
        var head = lines[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names) => head.FindIndex(h => names.Any(n => h == n || h.StartsWith(n + " ")));
        int kind = Col("kind"), type = Col("type", "name"), mark = Col("mark"), layer = Col("layer"), material = Col("material"), thick = Col("thickness"), core = Col("core"), from = Col("from"), to = Col("to"), note = Col("note");
        if (type < 0 || material < 0 || thick < 0) throw new CommandException("The CSV needs at least the columns Type, Material and Thickness (mm).");
        var order = new List<(string Kind, string Name)>();
        var found = new Dictionary<(string, string), (string? Mark, List<JsonObject> Layers)>();
        foreach (var line in lines.Skip(1))
        {
            var c = line;
            string At(int i) => i >= 0 && i < c.Count ? c[i].Trim() : "";
            var name = At(type);
            if (name.Length == 0) continue;
            var key = (KindOf(At(kind)), name);
            if (!found.TryGetValue(key, out var t)) { found[key] = t = (At(mark) is { Length: > 0 } mk ? mk : null, []); order.Add(key); }
            var l = new JsonObject { ["material"] = At(material), ["thickness"] = Metres(At(thick), name) };
            if (At(core).ToLowerInvariant() is "yes" or "y" or "x" or "1" or "true" or "da" or "si" or "sì" or "tak" or "sim" or "core") l["core"] = true;
            if (layer >= 0 && At(layer) is { Length: > 0 } ln && !int.TryParse(ln, out _)) l["name"] = ln;
            if (At(from).Length > 0) l["from"] = Metres(At(from), name);
            if (At(to).Length > 0) l["to"] = Metres(At(to), name);
            if (At(note).Length > 0) l["note"] = At(note);
            t.Layers.Add(l);
        }
        return order.Select(k =>
        {
            var source = new JsonObject { ["kind"] = k.Kind, ["name"] = k.Name, ["layers"] = new JsonArray([.. found[k].Layers.Select(l => l.DeepClone())]) };
            if (found[k].Mark is { } mk) source["mark"] = mk;
            return new FileType(k.Kind, k.Name, found[k].Mark, LayerSets.Parse(new JsonArray([.. found[k].Layers]), $"The type „{k.Name}”"), null, null, source);
        }).ToList();
    }

    static double Metres(string mm, string type) =>
        Numbers.Parse(mm, NumberMode.Strict) is { } v ? v / 1000 : throw new CommandException($"„{mm}” is not a thickness in millimetres (type „{type}”).");

    /// <summary>What an import did: types made (some beside the project's, renamed), changed, skipped; the materials settled.</summary>
    public sealed record Result(int Created, int Updated, int Skipped, List<string> Renamed, LibraryMerge.MaterialTally Materials);

    /// <summary>
    /// Before the import (<c>architecture.readTypes</c>): each type of the file, whether the project has one of its kind and
    /// name and what differs from it (layers, mark, sweeps, params: only what the file says), and each material the file
    /// defines that the project has by name with another definition, and what differs.
    /// </summary>
    public static JsonObject Preview(IModelView m, string text)
    {
        var types = Read(text);
        var o = new JsonArray();
        foreach (var t in types)
        {
            var cls = t.Kind == "floor" ? IfcClass.SlabType : IfcClass.WallType;
            var existing = m.OfClass(cls).FirstOrDefault(x => string.Equals(x.Name, t.Name, StringComparison.OrdinalIgnoreCase));
            var item = new JsonObject { ["kind"] = t.Kind, ["name"] = t.Name, ["exists"] = existing != null };
            if (existing != null)
            {
                var ours = TypeJson(m, t.Kind, existing);
                var differs = new JsonArray();
                foreach (var k in new[] { "layers", "mark", WallSweepTypes.Key, "params" })
                {
                    // What the file says nothing of stays the project's: not a difference.
                    if (k != "layers" && !t.Source.ContainsKey(k)) continue;
                    if (!JsonNode.DeepEquals(Comparable(ours[k]), Comparable(t.Source[k]))) differs.Add((JsonNode)LibraryMerge.Label(k));
                }
                if (differs.Count > 0) item["differs"] = differs;
            }
            o.Add((JsonNode)item);
        }
        var materials = new JsonArray();
        foreach (var (name, attrs) in ReadMaterials(text))
            if (Materials.Find(m, name) is { } found && LibraryMerge.Differs(found.Attrs, attrs, Volatile) is { Count: > 0 } d)
                materials.Add((JsonNode)new JsonObject { ["name"] = name, ["differs"] = new JsonArray(d.Select(x => (JsonNode)x).ToArray()) });
        return new JsonObject { ["types"] = o, ["materials"] = materials };
    }

    /// <summary>A type's part for comparing: a layer's colour left out (its material's, compared with the materials), numbers to the micrometre.</summary>
    static JsonNode? Comparable(JsonNode? n) => n switch
    {
        JsonObject o => new JsonObject(o.Where(kv => kv.Key != "color" && kv.Value is not null).Select(kv => KeyValuePair.Create(kv.Key, Comparable(kv.Value)))),
        JsonArray a => new JsonArray(a.Select(Comparable).ToArray()),
        JsonValue v when v.GetValueKind() == System.Text.Json.JsonValueKind.Number && Lapin.Model.Json.Number(v) is { } d => JsonValue.Create(Math.Round(d, 6)),
        _ => n?.DeepClone(),
    };

    /// <summary>The types brought in: each kind's type of that name changed to the file's layers (and mark), any other made; one undo step.</summary>
    public static (int Created, int Updated) Import(ICommandContext ctx, string text)
    {
        var r = Import(ctx, text, null, null);
        return (r.Created, r.Updated);
    }

    /// <summary>
    /// The types brought in, one undo step. The file's materials first (version 2): one the project lacks made as the file
    /// defines it; one it has that differs kept (the default), updated in place or made beside as „Name (2)”, as
    /// <paramref name="materialChoices"/> say by name. Then each type: one of that kind and name changed to the file's
    /// layers, mark, sweeps and parameters (the default, as before), made beside as „Name (2)” or skipped, as
    /// <paramref name="typeChoices"/> say by name; any other made.
    /// </summary>
    public static Result Import(ICommandContext ctx, string text, IReadOnlyDictionary<string, string>? materialChoices, IReadOnlyDictionary<string, string>? typeChoices)
    {
        if (Read(text).Count == 0) throw new CommandException("The file has no types.");
        var tally = new LibraryMerge.MaterialTally();
        var rename = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, attrs) in ReadMaterials(text))
        {
            foreach (var k in Volatile) attrs.Remove(k);
            if (Materials.Find(ctx.Model, name) is not { } found)
            {
                ctx.Add(new Entity(ctx.NewId(), IfcClass.Material, name, attrs));
                tally.Made++;
                continue;
            }
            if (LibraryMerge.Differs(found.Attrs, attrs, Volatile).Count == 0) continue;
            switch (materialChoices?.GetValueOrDefault(name) ?? LibraryMerge.Keep)
            {
                case LibraryMerge.Update:
                    foreach (var k in Volatile) if (found.Attrs[k] is { } v) attrs[k] = v.DeepClone();
                    ctx.Replace(found.With(attrs: attrs));
                    tally.Updated++;
                    break;
                case LibraryMerge.Both:
                    var beside = LibraryMerge.FreeName(ctx.Model, IfcClass.Material, name);
                    ctx.Add(new Entity(ctx.NewId(), IfcClass.Material, beside, attrs));
                    rename[name] = beside;
                    tally.Made++;
                    tally.Renamed.Add(beside);
                    break;
                default:
                    tally.Kept++;
                    break;
            }
        }
        var (created, updated, skipped) = (0, 0, 0);
        var renamed = new List<string>();
        foreach (var t in Read(text, rename))
        {
            var cls = t.Kind == "floor" ? IfcClass.SlabType : IfcClass.WallType;
            var existing = ctx.Model.OfClass(cls).FirstOrDefault(x => string.Equals(x.Name, t.Name, StringComparison.OrdinalIgnoreCase));
            var choice = existing is null ? null : typeChoices?.GetValueOrDefault(t.Name) ?? LibraryMerge.Update;
            if (choice == LibraryMerge.Skip) { skipped++; continue; }
            var name = t.Name;
            if (choice == LibraryMerge.Both) { name = LibraryMerge.FreeName(ctx.Model, cls, t.Name); renamed.Add(name); existing = null; }
            var ps = t.Params is null ? null : Params.ParseList(t.Params, cls);
            // A wall type's sweeps: its profiles the project's, its materials by name (a file without them leaves the type's own).
            var parsed = t.Kind == "wall" && t.Sweeps != null ? WallSweepTypes.Parse(ctx, existing, WallSweepTypes.Import(ctx, t.Sweeps, existing)) : null;
            if (existing is null)
            {
                var made = t.Kind == "floor" ? Slabs.CreateType(ctx, name, t.Layers) : WallTypes.Create(ctx, name, t.Layers, t.Mark, sweeps: parsed);
                if (t.Kind == "floor" && t.Mark != null) SetAttr(ctx, made.Id, "mark", JsonValue.Create(t.Mark));
                if (ps is { Count: > 0 }) SetAttr(ctx, made.Id, "params", ps);
                created++;
                continue;
            }
            if (t.Kind == "floor") Slabs.UpdateType(ctx, existing, null, t.Layers); else WallTypes.Update(ctx, existing, null, t.Layers, sweeps: parsed);
            if (t.Mark != null) SetAttr(ctx, existing.Id, "mark", JsonValue.Create(t.Mark));
            if (ps != null) SetAttr(ctx, existing.Id, "params", ps.Count > 0 ? ps : null);
            updated++;
        }
        return new Result(created, updated, skipped, renamed, tally);
    }

    /// <summary>One attribute of an entity set (null: taken off), when it differs.</summary>
    static void SetAttr(ICommandContext ctx, string id, string key, JsonNode? value)
    {
        var now = ctx.Model.Get(id)!;
        if (JsonNode.DeepEquals(now.Attrs[key], value)) return;
        var attrs = now.CloneAttrs();
        if (value is null) attrs.Remove(key); else attrs[key] = value;
        ctx.Replace(now.With(attrs: attrs));
    }

    public static void Register(IModuleRegistry r)
    {
        // {kinds?: ["wall", "floor"], format?: "json" | "csv"} → {text}
        r.Query("exportTypes", (q, a) =>
        {
            var kinds = (a["kinds"] as JsonArray)?.Select(k => (string?)k).OfType<string>().Select(KindOf).ToList();
            var text = (string?)a["format"] == "csv" ? Csv(q.Model, kinds) : Json(q.Model, kinds).ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            return new JsonObject { ["text"] = text };
        });
        // {text} → {types: [{kind, name, exists, differs?}], materials: [{name, differs}]}: what the file holds and what of it differs from the project's of the same name.
        r.Query("readTypes", (q, a) => Preview(q.Model, (string?)a["text"] ?? throw new CommandException("Nothing to read.")));
        // {text, materials?: {name: keep|update|both}, types?: {name: update|both|skip}} → {created, updated, skipped, renamed, materials: {made, updated, kept, renamed}}
        r.Command("importTypes", "Import types", (ctx, a) =>
        {
            var result = Import(ctx, (string?)a["text"] ?? throw new CommandException("Nothing to bring in."),
                LibraryMerge.Choices(a["materials"], LibraryMerge.MaterialChoices), LibraryMerge.Choices(a["types"], LibraryMerge.TypeChoices));
            return ResultJson(result);
        });
        r.Service<ILibraryFormat>(new LibraryFormat(Spec, (q, text) => Rows(q.Model, text), ImportAll));
        r.Service<IScheduleSource>(new TypeAssemblies());
    }
}

/// <summary>The type assemblies schedule (report #52): every wall and floor type, its mark, its total thickness and how many elements use it, then its layers in order with their materials and thicknesses.</summary>
public sealed class TypeAssemblies : IScheduleSource
{
    public string Kind => "types";
    public string Label => "Type assemblies";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        var rows = new List<IReadOnlyList<string>>();
        string Mm(double v) => Math.Round(v * 1000, 1).ToString("0.#", CultureInfo.InvariantCulture);
        // Its R and U (issue #67), worked out from its layers; a layer's λ (an asterisk: Lapin's indicative one) and R; U blank when a λ is missing.
        string N(double? v, string format = "0.000") => v is { } x ? x.ToString(format, CultureInfo.InvariantCulture) : "";
        foreach (var (kind, t) in TypeExchange.Types(m))
        {
            var layers = LayerSets.LayersOf(m, t.Id);
            var used = m.RelatedOf(t.Id, IfcClass.RelDefinesByType).Count();
            var th = Thermal.Of(m, t);
            rows.Add([kind == "floor" ? "Floor" : "Wall", t.Name, t.Str("mark") ?? "", "", "", Mm(layers.Sum(l => l.Thickness)), "", used.ToString(CultureInfo.InvariantCulture),
                "", th is { Complete: true } ? N(th.RTotal) : "", th?.U is { } u ? N(u) : th != null ? "incomplete" : ""]);
            for (var i = 0; i < layers.Count; i++)
            {
                var tl = th?.Layers.FirstOrDefault(x => x.Index == i);
                rows.Add(["", "", "", layers[i].Name is { Length: > 0 } n ? n : (i + 1).ToString(CultureInfo.InvariantCulture), m.Get(layers[i].Material)?.Name ?? "", Mm(layers[i].Thickness), layers[i].Core ? "core" : "", "",
                    tl?.Lambda is { } lam ? N(lam, "0.0##") + (tl.Source == "indicative" ? "*" : "") : tl?.Source == "air" ? "air" : "", tl?.Source == "skipped" ? "" : N(tl?.R), ""]);
            }
        }
        return new ScheduleGrid(Label, ["Kind", "Type", "Mark", "Layer", "Material", "Thickness (mm)", "Core", "Elements", "λ (W/mK)", "R (m²K/W)", "U (W/m²K)"], rows);
    }
}
