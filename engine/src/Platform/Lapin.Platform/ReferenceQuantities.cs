using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What a reference measures (the user's report "can't get quantities"): an element imported with its file's body (an IFC
/// reference, a Fragments or BIM Open Schema item) keeps its IFC class, but its class's own quantities are worked out from
/// a native element's attributes (an axis, a profile, layers) it does not have, and gave zeros or nothing. Its quantities
/// are the file's own instead: the quantity sets the file gave it (Qto_WallBaseQuantities…), as the file typed them, in SI
/// units (the import converts them by the file's units into <see cref="Key"/>); then what can be measured on the stored body
/// for what the file lacks: its net volume when the body is closed, and the count.
/// Nothing else is made up: a length, an area or a type is the file's or none. Every reference is measured here, whatever
/// its class, before any module's producer (<see cref="Session.Quantities"/>).
/// </summary>
public static class ReferenceQuantities
{
    /// <summary>The file's quantity sets, written by the import: set name → quantity name → [value in SI, kind (LENGTH, AREA, VOLUME, WEIGHT, COUNT, TIME)].</summary>
    public const string Key = "fileQuantities";

    /// <summary>The set the measured quantities go in when the file gave none.</summary>
    public const string MeasuredSet = "Lapin_BodyQuantities";

    /// <summary>Whether an element is measured as a reference: its body came from a file (an IFC reference, a Fragments or BOS item).</summary>
    public static bool Applies(Entity e) =>
        StoredBodies.Has(e) && (ReferenceLayers.IsReference(e) || e.Attrs["fragment"] is JsonObject || e.Attrs["bos"] is JsonObject);

    /// <summary>The quantities in the form <see cref="Key"/> holds, from (set, name, kind, value in SI).</summary>
    public static JsonObject Write(IEnumerable<(string Set, string Name, string Kind, double Value)> quantities)
    {
        var o = new JsonObject();
        foreach (var (set, name, kind, value) in quantities)
        {
            if (!double.IsFinite(value) || string.IsNullOrEmpty(name)) continue;
            var s = o[set] as JsonObject ?? (JsonObject)(o[set] = new JsonObject());
            s[name] = new JsonArray(Math.Round(value, 9), kind);
        }
        return o;
    }

    /// <summary>The file's quantity sets as they are kept (<see cref="Key"/>), in their order.</summary>
    public static List<QuantitySet> FileSets(Entity e)
    {
        var o = new List<QuantitySet>();
        if (e.Attrs[Key] is not JsonObject sets) return o;
        foreach (var (name, node) in sets)
        {
            if (node is not JsonObject set) continue;
            var qs = new List<Quantity>();
            foreach (var (qn, v) in set)
                if (v is JsonArray { Count: 2 } pair && Json.Number(pair[0]) is { } value && (string?)pair[1] is { Length: > 0 } kind)
                    qs.Add(new Quantity(qn, value, kind, qn));
            if (qs.Count > 0) o.Add(new QuantitySet(name, qs));
        }
        return o;
    }

    /// <summary>
    /// The quantities IFC's base sets name, which an alias of the same name fills in for a reference whose file lacks them
    /// (<see cref="FieldAliases"/>), each with its kind.
    /// </summary>
    public static readonly (string Name, string Kind)[] Standard =
    [
        ("Length", "LENGTH"), ("Width", "LENGTH"), ("Height", "LENGTH"), ("Depth", "LENGTH"), ("Thickness", "LENGTH"), ("Perimeter", "LENGTH"),
        ("GrossSideArea", "AREA"), ("NetSideArea", "AREA"), ("GrossArea", "AREA"), ("NetArea", "AREA"), ("GrossFloorArea", "AREA"), ("NetFloorArea", "AREA"),
        ("GrossSurfaceArea", "AREA"), ("NetSurfaceArea", "AREA"), ("GrossFootprintArea", "AREA"), ("NetFootprintArea", "AREA"), ("CrossSectionArea", "AREA"),
        ("OuterSurfaceArea", "AREA"), ("Area", "AREA"),
        ("GrossVolume", "VOLUME"), ("NetVolume", "VOLUME"), ("Volume", "VOLUME"),
        ("GrossWeight", "WEIGHT"), ("NetWeight", "WEIGHT"), ("Weight", "WEIGHT"),
    ];

    /// <summary>
    /// A number an alias's source names on a reference, read without its quantities (they are being made): a quantity of its
    /// file's sets (<c>Qto_WallBaseQuantities.Length</c>, or by its name in any), else the field as a formula reads it (an
    /// imported property <c>Pset_X.L_m</c>, a parameter, an attribute).
    /// </summary>
    static double? SourceNumber(IModelView m, Entity e, List<QuantitySet> file, string source)
    {
        var dot = source.LastIndexOf('.');
        var (set, name) = dot > 0 ? (source[..dot], source[(dot + 1)..]) : ((string?)null, source);
        foreach (var s in file)
        {
            if (set != null && !string.Equals(s.Name, set, StringComparison.OrdinalIgnoreCase)) continue;
            if (s.Quantities.FirstOrDefault(q => string.Equals(q.Name, name, StringComparison.OrdinalIgnoreCase)) is { } hit) return hit.Value;
        }
        if (set != null && file.Any(s => string.Equals(s.Name, set, StringComparison.OrdinalIgnoreCase))) return null;
        return ParamFormulas.Read(m, e, ReferenceTypes.TypeOf(m, e), null, source) is { Number: { } n } x && (x.Text == null || x.IsNumericText) ? n : null;
    }

    /// <summary>
    /// A reference's quantities: its file's sets, the base quantities first (Qto_…BaseQuantities, else the first), with what
    /// the body gives that no set of the file has added to that first one (or, when the file gave none, in <see cref="MeasuredSet"/>).
    /// </summary>
    public static IReadOnlyList<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        var file = FileSets(e);
        var main = file.FirstOrDefault(s => s.Name.StartsWith("Qto_", StringComparison.Ordinal) && s.Name.EndsWith("BaseQuantities", StringComparison.Ordinal))
            ?? file.FirstOrDefault(s => s.Name.StartsWith("Qto_", StringComparison.Ordinal))
            ?? file.FirstOrDefault();
        var have = file.SelectMany(s => s.Quantities).Select(q => q.Name).ToHashSet(StringComparer.Ordinal);
        var measured = new List<Quantity>();
        // The office's aliases named like a standard quantity (FieldAliases): what the file lacks, from the sources they name.
        foreach (var alias in FieldAliases.All(m))
        {
            if (Standard.FirstOrDefault(x => TagText.Squash(x.Name) == TagText.Squash(alias.Name)) is not { Name: { } std } stdq || have.Contains(std)) continue;
            foreach (var source in alias.Sources)
                if (SourceNumber(m, e, file, source) is { } v && double.IsFinite(v))
                {
                    measured.Add(new Quantity(std, v, stdq.Kind, std));
                    have.Add(std);
                    break;
                }
        }
        if (g is { Parts.Count: > 0 } && ExternalBodies.HasClosedBody(e, m))
        {
            var volume = g.Parts.Sum(p => Measure.Volume(p.Solid));
            // The body as it stands, its openings cut: its net volume. A gross one is not told by the body (whether a void was cut
            // out of it depends on how it was imported), so it is the file's or none.
            if (volume > 1e-12 && !have.Contains("NetVolume")) measured.Add(new Quantity("NetVolume", volume, "VOLUME", "Net volume"));
        }
        if (!have.Contains("Count")) measured.Add(new Quantity("Count", 1, "COUNT", "Count"));
        var o = new List<QuantitySet>();
        if (main != null) o.Add(main with { Quantities = [.. main.Quantities, .. measured] });
        else o.Add(new QuantitySet(MeasuredSet, measured));
        o.AddRange(file.Where(s => !ReferenceEquals(s, main)));
        return o;
    }
}

/// <summary>
/// A reference's type: the native type it is related to; else, for an IFC reference, the record of the type its file
/// relates it to (<see cref="IfcSource"/>: kept so the export writes it back), which an estimate's articles can be set on
/// like a type's; its name, else the type's name the file gave a Fragments or BOS item, else its ObjectType.
/// </summary>
public static class ReferenceTypes
{
    /// <summary>The element's type: its IfcRelDefinesByType's, else a reference's file type record.</summary>
    public static Entity? TypeOf(IModelView m, Entity e) =>
        m.RelatingOf(e.Id, IfcClass.RelDefinesByType)
        ?? ((string?)e.Attrs[IfcSource.Key]?["type"]?["record"] is { Length: > 0 } rec && m.Get(rec) is { Class: IfcSource.RecordClass } record ? record : null);

    /// <summary>The name of the element's type (null when it has none): its type's, else what a reference's file says of it.</summary>
    public static string? NameOf(IModelView m, Entity e)
    {
        if (TypeOf(m, e) is { } t && Name(t) is { Length: > 0 } n) return n;
        if (!ReferenceQuantities.Applies(e)) return null;
        if (e.Attrs["properties"] is JsonObject props)
            foreach (var set in new[] { "Fragments", "Attributes" })
                if ((string?)(props[set] as JsonObject)?["Type"] is { Length: > 0 } given) return given;
        return (string?)e.Attrs["objectType"] is { Length: > 0 } ot ? ot : null;
    }

    /// <summary>A type's name: its own; for a file's type record, the Name its snapshot holds (a record kept before it was named after it).</summary>
    public static string? Name(Entity type)
    {
        if (type.Class != IfcSource.RecordClass || type.Name is { Length: > 0 } and not RecordDefault) return type.Name;
        return type.Attrs["snap"]?["a"] is JsonArray a && a.Count > 2 && (string?)a[2] is { Length: >= 2 } lit && lit[0] == '\'' && lit[^1] == '\''
            ? lit[1..^1].Replace("''", "'") : null;
    }

    /// <summary>The name a file record was given before the import named type records after their type.</summary>
    public const string RecordDefault = "IFC record";
}
