using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Apache.Arrow;
using Apache.Arrow.Ipc;

namespace Lapin.Package;

/// <summary>
/// A project of the old Lapin (format 2: the old app's "bimvoice-project"
/// zip), as plain rows: each model's native tables (storeys, materials, types,
/// and one table per element kind: wall, slab, beam, column, opening, door,
/// window), and the project's views. Its IFC copy is not read: the native
/// tables carry every parameter (docs/format-lapin.md, "Formatul 2").
/// </summary>
public sealed record LegacyProject(string Name, List<JsonObject> Storeys, List<JsonObject> Materials, List<JsonObject> Types,
    Dictionary<string, List<JsonObject>> Elements, List<JsonObject> Views, List<string> Skipped)
{
    public IEnumerable<JsonObject> Of(string kind) => Elements.GetValueOrDefault(kind) ?? [];
}

public static class LegacyPackage
{
    /// <summary>Whether a .lapin file is the old Lapin's (format 2) rather than format 3.</summary>
    public static bool Is(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        if (zip.GetEntry("manifest.json") is not { } m) return false;
        using var s = m.Open();
        var manifest = JsonNode.Parse(s) as JsonObject;
        var format = (string?)manifest?["format"];
        var version = Lapin.Model.Json.Number(manifest?["version"]) ?? 0;
        return format == "bimvoice-project" || (format == LapinPackage.Format && version < LapinPackage.Version);
    }

    /// <summary>The rows of an Arrow table, every cell as JSON (the old "json" columns parsed).</summary>
    static List<JsonObject> Rows(byte[] bytes, bool jsonCells)
    {
        var rows = new List<JsonObject>();
        IEnumerable<RecordBatch> Batches()
        {
            var list = new List<RecordBatch>();
            try
            {
                using var r = new ArrowFileReader(new MemoryStream(bytes));
                while (r.ReadNextRecordBatch() is { } b) list.Add(b);
            }
            catch (Exception)
            {
                list.Clear();
                using var r = new ArrowStreamReader(new MemoryStream(bytes));
                while (r.ReadNextRecordBatch() is { } b) list.Add(b);
            }
            return list;
        }
        foreach (var batch in Batches())
            for (var i = 0; i < batch.Length; i++)
            {
                var o = new JsonObject();
                for (var c = 0; c < batch.ColumnCount; c++)
                {
                    var name = batch.Schema.GetFieldByIndex(c).Name;
                    JsonNode? v = batch.Column(c) switch
                    {
                        DoubleArray d => d.GetValue(i) is { } x ? JsonValue.Create(x) : null,
                        BooleanArray b => b.GetValue(i) is { } x ? JsonValue.Create(x) : null,
                        StringArray s => s.GetString(i) is { } x ? JsonValue.Create(x) : null,
                        _ => null,
                    };
                    // A JSON column (or, in the types table, every column) holds JSON text.
                    if (v is JsonValue sv && sv.TryGetValue<string>(out var text) && (jsonCells || LooksJson(text)))
                        try { v = JsonNode.Parse(text); } catch (JsonException) { }
                    o[name] = v;
                }
                rows.Add(o);
            }
        return rows;
    }

    static bool LooksJson(string s) => s.Length > 1 && (s[0] is '{' or '[') && (s[^1] is '}' or ']');

    static readonly string[] Kinds = ["wall", "slab", "beam", "column", "opening", "door", "window", "solid", "system"];

    public static LegacyProject Read(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        byte[] Bytes(ZipArchiveEntry e) { using var s = e.Open(); using var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray(); }
        JsonNode? Json(string name) => zip.GetEntry(name) is { } e ? JsonNode.Parse(Bytes(e)) : null;
        var storeys = new List<JsonObject>(); var materials = new List<JsonObject>(); var types = new List<JsonObject>();
        var elements = new Dictionary<string, List<JsonObject>>();
        var skipped = new List<string>();
        foreach (var native in zip.Entries.Where(e => e.FullName.EndsWith("/native/manifest.json", StringComparison.Ordinal)))
        {
            var dir = native.FullName[..^"manifest.json".Length];
            List<JsonObject> Table(string name, bool json = false) => zip.GetEntry(dir + name) is { } e ? Rows(Bytes(e), json) : [];
            storeys.AddRange(Table("storeys.arrow"));
            materials.AddRange(Table("materials.arrow"));
            types.AddRange(Table("types.arrow", json: true));
            foreach (var kind in Kinds)
            {
                var rows = Table($"elements/{kind}.arrow");
                if (rows.Count == 0) continue;
                if (kind is "solid" or "system") { skipped.Add($"{rows.Count} {kind} element(s)"); continue; }
                (elements.TryGetValue(kind, out var l) ? l : elements[kind] = []).AddRange(rows);
            }
        }
        if (storeys.Count == 0 && zip.Entries.Any(e => e.FullName.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase)))
            skipped.Add("a model without native tables (an IFC kept as it was): open it as IFC");
        var views = (Json("project/views.json")?["views"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        var name = (string?)Json("project/document.json")?["name"] ?? (string?)Json("manifest.json")?["projectName"] ?? "Project";
        if (Json("project/sheets.json") is JsonObject sh && sh["sheets"] is JsonArray { Count: > 0 } sheets) skipped.Add($"{sheets.Count} sheet(s)");
        return new LegacyProject(name, storeys, materials, types, elements, views, skipped);
    }
}
