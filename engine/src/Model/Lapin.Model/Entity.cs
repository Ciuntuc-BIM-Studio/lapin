using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lapin.Model;

/// <summary>
/// One IFC instance of the Lapin model: an object (IfcWall, IfcWallType,
/// IfcBuildingStorey) or a resource it uses (IfcMaterial,
/// IfcMaterialLayerSet, IfcMaterialLayerSetUsage).
///
/// <para><see cref="Id"/> is stable for the entity's whole life: the IFC
/// GlobalId of a rooted entity, a Lapin id for a resource (written as a step
/// id on export). <see cref="Attrs"/> hold what the entity IS, as Lapin
/// authors it (an IfcWall's axis and height, a layer set's layers), in
/// metres; geometry is derived from them, never stored.</para>
///
/// Immutable: an edit replaces the entity (<see cref="With"/>).
/// </summary>
public sealed class Entity(string id, string @class, string name, JsonObject attrs, string? predefinedType = null)
{
    public string Id { get; } = id;
    /// <summary>The IFC entity: "IfcWall".</summary>
    public string Class { get; } = @class;
    public string Name { get; } = name;
    /// <summary>The IFC PredefinedType ("STANDARD", "FLOOR"), or null (NOTDEFINED).</summary>
    public string? PredefinedType { get; } = predefinedType;
    public JsonObject Attrs { get; } = attrs;

    public Entity With(string? name = null, JsonObject? attrs = null, string? predefinedType = null) =>
        new(Id, Class, name ?? Name, attrs ?? Attrs, predefinedType ?? PredefinedType);

    public JsonObject CloneAttrs() => (JsonObject)Attrs.DeepClone();

    public double Num(string key, double fallback = 0) => Json.Number(Attrs[key]) ?? fallback;
    public double? NumOrNull(string key) => Json.Number(Attrs[key]);
    public string? Str(string key) => Attrs[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    public bool Bool(string key) => Attrs[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
    public T? Get<T>(string key) => Attrs[key] is { } n ? n.Deserialize<T>(Json.Options) : default;
}

/// <summary>
/// An IFC objectified relationship (IfcRelContainedInSpatialStructure,
/// IfcRelDefinesByType, IfcRelAssociatesMaterial, IfcRelVoidsElement…):
/// one relating entity and the entities it relates, in order. A
/// relationship is rooted in IFC, so <see cref="Id"/> is its GlobalId.
/// </summary>
public sealed record Relation(string Id, string Class, string Relating, IReadOnlyList<string> Related, JsonObject? Attrs = null)
{
    public bool Touches(string id) => Relating == id || Related.Contains(id);
}

/// <summary>The one JSON convention of the engine: camelCase, numbers read leniently.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Any JSON number (however it was written) or numeric string, as a double.</summary>
    public static double? Number(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<double>(out var d)) return d;
        if (v.TryGetValue<int>(out var i)) return i;
        if (v.TryGetValue<long>(out var l)) return l;
        if (v.TryGetValue<float>(out var f)) return f;
        if (v.TryGetValue<decimal>(out var m)) return (double)m;
        if (v.TryGetValue<JsonElement>(out var e) && e.ValueKind == JsonValueKind.Number) return e.GetDouble();
        return v.TryGetValue<string>(out var s) && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : null;
    }

    public static JsonObject Object<T>(T value) => (JsonObject)JsonSerializer.SerializeToNode(value, Options)!;
}
