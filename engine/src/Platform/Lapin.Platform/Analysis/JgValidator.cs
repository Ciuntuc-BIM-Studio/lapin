using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Platform.Analysis;

/// <summary>
/// The contract's validation rules (JG_Structures_v2_exchange 1.0.0, "validation_rules") checked on a document before it is
/// written: the required fields of each entity; ids unique within their entity; every node_i, node_j, node_ids, story_id,
/// section_id and material_id resolving; node_j unlike node_i, three nodes at least to a shell, each once; coordinates
/// exactly [X, Y, Z]; restraints six booleans or null; every number finite; the units declared; property_status one of the
/// three; a catalogue section with its standard, family, designation and source; verified_tabulated only with a table or a
/// page; a frame's end_offsets null or {system: global, unit: the length unit, i, j: [dx, dy, dz]}. Each failure is a
/// sentence naming what and where.
/// </summary>
public static class JgValidator
{
    static readonly Dictionary<string, string[]> Required = new()
    {
        ["project"] = ["id", "name", "source_application"],
        ["coordinate_system"] = ["handedness", "x_axis", "y_axis", "z_axis"],
        ["stories"] = ["id", "name", "elevation"],
        ["grids"] = ["id", "type", "x", "y"],
        ["materials"] = ["id", "name", "kind", "elastic_modulus", "poisson_ratio", "source"],
        ["sections"] = ["id", "name", "category", "kind", "geometry", "properties", "property_status", "source"],
        ["nodes"] = ["id", "coordinates", "story_id"],
        ["frames"] = ["id", "kind", "node_i", "node_j", "section_id", "material_id"],
        ["shells"] = ["id", "kind", "node_ids", "section_id", "material_id"],
    };

    static readonly string[] UnitKeys = ["length", "force", "mass", "temperature", "stress", "weight_density"];

    public static List<string> Validate(JsonObject doc)
    {
        var errors = new List<string>();
        if ((string?)doc["schema_version"] != JgExchange.SchemaVersion) errors.Add($"schema_version is not {JgExchange.SchemaVersion}.");
        if (doc["units"] is not JsonObject units) errors.Add("The units are not declared.");
        else foreach (var k in UnitKeys) if (units[k] is not JsonValue v || !v.TryGetValue<string>(out var su) || su.Length == 0) errors.Add($"units.{k} is not declared.");
        foreach (var single in new[] { "project", "coordinate_system" })
        {
            if (doc[single] is not JsonObject o) { errors.Add($"{single} is missing."); continue; }
            foreach (var f in Required[single]) if (!o.ContainsKey(f) || o[f] == null) errors.Add($"{single}.{f} is missing.");
        }
        var ids = new Dictionary<string, HashSet<string>>();
        foreach (var entity in new[] { "stories", "grids", "materials", "sections", "nodes", "frames", "shells" })
        {
            var set = ids[entity] = [];
            if (doc[entity] is not JsonArray arr) { errors.Add($"{entity} is not a list."); continue; }
            foreach (var item in arr)
            {
                if (item is not JsonObject o) { errors.Add($"An item of {entity} is not an object."); continue; }
                var id = (string?)o["id"];
                foreach (var f in Required[entity])
                    if (!o.ContainsKey(f)) errors.Add($"{entity} {id ?? "?"}: {f} is missing.");
                if (string.IsNullOrEmpty(id)) { errors.Add($"An item of {entity} has no id."); continue; }
                if (!set.Add(id)) errors.Add($"{entity}: the id {id} is used twice.");
            }
        }
        string? Ref(string entity, JsonNode? n, string where, bool nullable = false)
        {
            if (n == null) { if (!nullable) errors.Add($"{where} is missing."); return null; }
            var id = n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
            if (id == null) errors.Add($"{where} is not an id.");
            else if (!ids[entity].Contains(id)) errors.Add($"{where} = {id}: there is no such {entity[..^1]}.");
            return id;
        }
        foreach (var n in (doc["nodes"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var id = (string?)n["id"];
            if (n["coordinates"] is not JsonArray { Count: 3 } c || c.Any(x => x is not JsonValue)) errors.Add($"Node {id}: coordinates are not exactly [X, Y, Z].");
            Ref("stories", n["story_id"], $"Node {id} story_id", nullable: true);
            if (n["restraints"] is JsonArray r && (r.Count != 6 || r.Any(x => x is not JsonValue bv || !bv.TryGetValue<bool>(out _)))) errors.Add($"Node {id}: restraints are not six booleans [UX, UY, UZ, RX, RY, RZ].");
            else if (n["restraints"] is not (null or JsonArray)) errors.Add($"Node {id}: restraints are neither six booleans nor null.");
        }
        foreach (var f in (doc["frames"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var id = (string?)f["id"];
            var i = Ref("nodes", f["node_i"], $"Frame {id} node_i");
            var j = Ref("nodes", f["node_j"], $"Frame {id} node_j");
            if (i != null && i == j) errors.Add($"Frame {id}: node_j is node_i.");
            Ref("sections", f["section_id"], $"Frame {id} section_id");
            Ref("materials", f["material_id"], $"Frame {id} material_id");
            if ((string?)f["kind"] is not ("beam" or "column")) errors.Add($"Frame {id}: kind is neither beam nor column.");
            // end_offsets (Lapin's shape of the contract's object): from each node to the member's own end, global, in the length unit.
            if (f["end_offsets"] is JsonObject eo)
            {
                if ((string?)eo["system"] != "global") errors.Add($"Frame {id}: end_offsets.system is not global.");
                if ((string?)eo["unit"] != (string?)doc["units"]?["length"]) errors.Add($"Frame {id}: end_offsets.unit is not the length unit.");
                foreach (var k in new[] { "i", "j" })
                    if (eo[k] is not JsonArray { Count: 3 } v || v.Any(x => x is not JsonValue)) errors.Add($"Frame {id}: end_offsets.{k} is not [dx, dy, dz].");
            }
            else if (f["end_offsets"] != null) errors.Add($"Frame {id}: end_offsets is neither an object nor null.");
        }
        foreach (var s in (doc["shells"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var id = (string?)s["id"];
            if (s["node_ids"] is not JsonArray ns) { errors.Add($"Shell {id}: node_ids is not a list."); continue; }
            if (ns.Count < 3) errors.Add($"Shell {id}: fewer than three nodes.");
            var seen = new HashSet<string>();
            foreach (var n in ns) if (Ref("nodes", n, $"Shell {id} node_ids") is { } nid && !seen.Add(nid)) errors.Add($"Shell {id}: node {nid} twice.");
            Ref("sections", s["section_id"], $"Shell {id} section_id");
            Ref("materials", s["material_id"], $"Shell {id} material_id");
            if ((string?)s["kind"] is not ("slab" or "deck" or "wall")) errors.Add($"Shell {id}: kind is not slab, deck or wall.");
        }
        foreach (var s in (doc["sections"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var id = (string?)s["id"];
            var status = (string?)s["property_status"];
            if (status is not ("verified_tabulated" or "derived_user_geometry" or "pending")) errors.Add($"Section {id}: property_status is not verified_tabulated, derived_user_geometry or pending.");
            if ((string?)s["kind"] == "catalog")
            {
                if (s["catalog"] is not JsonObject c || new[] { "standard", "family", "designation" }.Any(k => c[k] is not JsonValue cv || !cv.TryGetValue<string>(out var cs) || cs.Length == 0) || c["source"] is not JsonObject)
                    errors.Add($"Section {id}: a catalogue section needs its standard, family, exact designation and source.");
            }
            if (status == "verified_tabulated" && (s["source"] is not JsonObject src || (src["table"] == null && src["page"] == null)))
                errors.Add($"Section {id}: verified_tabulated needs its source's table or page.");
        }
        Finite(doc, "", errors);
        return errors;
    }

    /// <summary>Every number finite (System.Text.Json writes no NaN, but a hand-made document may carry one as a double).</summary>
    static void Finite(JsonNode? n, string path, List<string> errors)
    {
        switch (n)
        {
            case JsonObject o: foreach (var (k, v) in o) Finite(v, $"{path}.{k}", errors); break;
            case JsonArray a: for (var i = 0; i < a.Count; i++) Finite(a[i], $"{path}[{i}]", errors); break;
            case JsonValue v when v.GetValueKind() == JsonValueKind.Number:
                if (v.TryGetValue<double>(out var d) && !double.IsFinite(d)) errors.Add($"{path.TrimStart('.')} is not a finite number.");
                break;
        }
    }
}
