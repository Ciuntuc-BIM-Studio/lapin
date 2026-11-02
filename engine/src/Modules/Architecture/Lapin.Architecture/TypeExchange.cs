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
/// </summary>
public static class TypeExchange
{
    public const string Format = "lapin-types";
    static readonly (string Kind, string Class)[] Kinds = [("wall", IfcClass.WallType), ("floor", IfcClass.SlabType)];
    static readonly string[] Header = ["Kind", "Type", "Mark", "Layer", "Material", "Thickness (mm)", "Core", "From (mm)", "To (mm)"];

    public static IEnumerable<(string Kind, Entity Type)> Types(IModelView m, IReadOnlyCollection<string>? kinds = null) =>
        Kinds.Where(k => kinds is null || kinds.Count == 0 || kinds.Contains(k.Kind))
             .SelectMany(k => m.OfClass(k.Class).OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).Select(t => (k.Kind, t)));

    /// <summary>The types as a JSON document: <c>{format, version, types: [{kind, name, mark?, layers: [{material, thickness, core?, color?, name?, from?, to?}]}]}</c>, metres.</summary>
    public static JsonObject Json(IModelView m, IReadOnlyCollection<string>? kinds = null)
    {
        var types = new JsonArray();
        foreach (var (kind, t) in Types(m, kinds))
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
                layers.Add((JsonNode)o);
            }
            var type = new JsonObject { ["kind"] = kind, ["name"] = t.Name };
            if (t.Str("mark") is { Length: > 0 } mark) type["mark"] = mark;
            type["layers"] = layers;
            types.Add((JsonNode)type);
        }
        return new JsonObject { ["format"] = Format, ["version"] = 1, ["types"] = types };
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
                    (string)l["material"]!, Mm(l["thickness"]), l["core"] is not null ? "yes" : "", Mm(l["from"]), Mm(l["to"]),
                }.Select(Cell)));
        }
        return sb.ToString();
    }

    static string Mm(JsonNode? metres) => Lapin.Model.Json.Number(metres) is { } v ? Math.Round(v * 1000, 3).ToString(CultureInfo.InvariantCulture) : "";
    static string Cell(string s) => s.IndexOfAny([',', ';', '"', '\n', '\r']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

    /// <summary>
    /// Types from a JSON document (as <see cref="Json"/> writes it, or a bare array of types) or CSV text: one row a
    /// layer under the header's names (any order, comma, semicolon or tab between cells, a decimal comma where the
    /// separator is not one), a type's layers in the order given; the kind "wall" when the file says none.
    /// </summary>
    public static List<(string Kind, string Name, string? Mark, IReadOnlyList<LayerSpec> Layers)> Read(string text)
    {
        var trimmed = text.TrimStart('﻿', ' ', '\t', '\r', '\n');
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            JsonNode? doc;
            try { doc = JsonNode.Parse(trimmed); } catch (Exception e) { throw new CommandException($"The file is not JSON a type list reads: {e.Message}"); }
            var list = doc is JsonObject o ? o["types"] as JsonArray : doc as JsonArray;
            if (list is null) throw new CommandException("The JSON has no list of types.");
            return list.OfType<JsonObject>().Select(t =>
            {
                var name = ((string?)t["name"])?.Trim();
                if (string.IsNullOrEmpty(name)) throw new CommandException("Every type needs a name.");
                return (KindOf((string?)t["kind"]), name, ((string?)t["mark"])?.Trim() is { Length: > 0 } mk ? mk : null, LayerSets.Parse(t["layers"], $"The type „{name}”"));
            }).ToList();
        }
        return ReadCsv(trimmed);
    }

    static string KindOf(string? kind) => (kind ?? "wall").Trim().ToLowerInvariant() switch
    {
        "" or "wall" or "walls" or "perete" => "wall",
        "floor" or "floors" or "slab" or "slabs" or "placa" or "placă" => "floor",
        var k => throw new CommandException($"„{k}” is not a kind of type Lapin brings in: wall or floor."),
    };

    static List<(string, string, string?, IReadOnlyList<LayerSpec>)> ReadCsv(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count < 2) throw new CommandException("The CSV has no layers under its header.");
        var sep = new[] { ';', '\t', ',' }.MaxBy(c => lines[0].Count(x => x == c));
        var head = Cells(lines[0], sep).Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names) => head.FindIndex(h => names.Any(n => h == n || h.StartsWith(n + " ")));
        int kind = Col("kind"), type = Col("type", "name"), mark = Col("mark"), layer = Col("layer"), material = Col("material"), thick = Col("thickness"), core = Col("core"), from = Col("from"), to = Col("to");
        if (type < 0 || material < 0 || thick < 0) throw new CommandException("The CSV needs at least the columns Type, Material and Thickness (mm).");
        var order = new List<(string Kind, string Name)>();
        var found = new Dictionary<(string, string), (string? Mark, List<JsonObject> Layers)>();
        foreach (var line in lines.Skip(1))
        {
            var c = Cells(line, sep);
            string At(int i) => i >= 0 && i < c.Count ? c[i].Trim() : "";
            var name = At(type);
            if (name.Length == 0) continue;
            var key = (KindOf(At(kind)), name);
            if (!found.TryGetValue(key, out var t)) { found[key] = t = (At(mark) is { Length: > 0 } mk ? mk : null, []); order.Add(key); }
            var l = new JsonObject { ["material"] = At(material), ["thickness"] = Metres(At(thick), sep, name) };
            if (At(core).ToLowerInvariant() is "yes" or "y" or "x" or "1" or "true" or "da" or "core") l["core"] = true;
            if (layer >= 0 && At(layer) is { Length: > 0 } ln && !int.TryParse(ln, out _)) l["name"] = ln;
            if (At(from).Length > 0) l["from"] = Metres(At(from), sep, name);
            if (At(to).Length > 0) l["to"] = Metres(At(to), sep, name);
            t.Layers.Add(l);
        }
        return order.Select(k => (k.Kind, k.Name, found[k].Mark, LayerSets.Parse(new JsonArray([.. found[k].Layers]), $"The type „{k.Name}”"))).ToList();
    }

    static double Metres(string mm, char sep, string type)
    {
        var s = sep == ',' ? mm : mm.Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v / 1000 : throw new CommandException($"„{mm}” is not a thickness in millimetres (type „{type}”).");
    }

    /// <summary>A CSV line's cells, quotes kept as spreadsheets write them.</summary>
    static List<string> Cells(string line, char sep)
    {
        var o = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else sb.Append(ch);
            }
            else if (ch == '"' && sb.Length == 0) quoted = true;   // a quote opens a cell only at its start
            else if (ch == sep) { o.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        o.Add(sb.ToString());
        return o;
    }

    /// <summary>The types brought in: each kind's type of that name changed to the file's layers (and mark), any other made; one undo step.</summary>
    public static (int Created, int Updated) Import(ICommandContext ctx, string text)
    {
        var types = Read(text);
        if (types.Count == 0) throw new CommandException("The file has no types.");
        var (created, updated) = (0, 0);
        foreach (var (kind, name, mark, layers) in types)
        {
            var cls = kind == "floor" ? IfcClass.SlabType : IfcClass.WallType;
            var existing = ctx.Model.OfClass(cls).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                if (kind == "floor") Slabs.CreateType(ctx, name, layers); else WallTypes.Create(ctx, name, layers, mark);
                created++;
                continue;
            }
            if (kind == "floor") Slabs.UpdateType(ctx, existing, null, layers); else WallTypes.Update(ctx, existing, null, layers);
            if (mark != null && ctx.Model.Get(existing.Id) is { } now && now.Str("mark") != mark)
            {
                var attrs = now.CloneAttrs();
                attrs["mark"] = mark;
                ctx.Replace(now.With(attrs: attrs));
            }
            updated++;
        }
        return (created, updated);
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
        // {text} → {created, updated}
        r.Command("importTypes", "Import types", (ctx, a) =>
        {
            var (created, updated) = Import(ctx, (string?)a["text"] ?? throw new CommandException("Nothing to bring in."));
            return new JsonObject { ["created"] = created, ["updated"] = updated };
        });
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
        foreach (var (kind, t) in TypeExchange.Types(m))
        {
            var layers = LayerSets.LayersOf(m, t.Id);
            var used = m.RelatedOf(t.Id, IfcClass.RelDefinesByType).Count();
            rows.Add([kind == "floor" ? "Floor" : "Wall", t.Name, t.Str("mark") ?? "", "", "", Mm(layers.Sum(l => l.Thickness)), "", used.ToString(CultureInfo.InvariantCulture)]);
            for (var i = 0; i < layers.Count; i++)
                rows.Add(["", "", "", layers[i].Name is { Length: > 0 } n ? n : (i + 1).ToString(CultureInfo.InvariantCulture), m.Get(layers[i].Material)?.Name ?? "", Mm(layers[i].Thickness), layers[i].Core ? "core" : "", ""]);
        }
        return new ScheduleGrid(Label, ["Kind", "Type", "Mark", "Layer", "Material", "Thickness (mm)", "Core", "Elements"], rows);
    }
}
