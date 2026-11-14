using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// A part as IFC writes it (decision 520, stage P3; docs/spec-ifc.md, "Parts"): the part is its class's IfcTypeProduct whose one
/// representation map is the body its defaults build (its block definition); a placed part built with the same values is an
/// IfcMappedItem of that map, any other its own body. The parameters go in Lapin's own set <see cref="SetName"/>: the type
/// parameters on the type, the instance parameters and the derived ones (described by their formula) on each placed part, each
/// with the measure of its kind. The definition itself rides in <c>Lapin_Intent</c> with the part's id, as before.
/// </summary>
public sealed class PartIfc : IIfcTypeShapes, IIfcElementSets
{
    /// <summary>Lapin's own set of a part's parameters (never Pset_: IFC keeps that prefix for its own sets).</summary>
    public const string SetName = "Lapin_PartParameters";

    public IfcTypeBody? TypeBody(IModelView model, Entity type)
    {
        if (type.Class != Parts.TypeClass || Parts.DefOf(type) is not { } def) return null;
        Dictionary<string, double> values;
        try { values = Parts.Values(def, null); }
        catch (CommandException) { return null; }
        var set = Parts.SetOf(type.Id, def, values);
        if (set.Error != null) return null;
        // In the part's own material where a body names none (forum topic 60), as its placed parts without one of their own are.
        var fallback = StudioModule.OwnMaterial(model, type);
        var local = set.Local(model, mirror: false, fallback: fallback);
        return local.Count == 0 ? null : new IfcTypeBody(set.KeyOf(false, fallback: fallback), local);
    }

    public IfcStandardSet? TypeSet(IModelView model, Entity type)
    {
        if (type.Class != Parts.TypeClass || Parts.DefOf(type) is not { } def) return null;
        var r = PartParams.Resolve(def, null);
        var props = Params(def).Where(p => !PartParams.IsInstance(p) && !PartParams.IsDerived(p)).Select(p => Property(p, r)).OfType<IfcStandardProperty>().ToList();
        return props.Count > 0 ? new IfcStandardSet(SetName, props) : null;
    }

    public IReadOnlyList<(string Name, string Value)> TypeIntent(IModelView model, Entity type) =>
        type.Class == Parts.TypeClass && Parts.DefOf(type) != null ? [("LapinPartId", type.Id)] : [];

    public IEnumerable<IfcStandardSet> Sets(IModelView model, Entity element)
    {
        if (!Parts.IsInstance(element) || Parts.DefOf(Parts.TypeOf(model, element)) is not { } def) yield break;
        var r = PartParams.Resolve(def, element.Attrs["values"] as JsonObject);
        var props = Params(def).Where(p => PartParams.IsInstance(p) || PartParams.IsDerived(p)).Select(p => Property(p, r)).OfType<IfcStandardProperty>().ToList();
        if (props.Count > 0) yield return new IfcStandardSet(SetName, props);
    }

    static List<JsonObject> Params(JsonObject def) => [.. (def["params"] as JsonArray ?? []).OfType<JsonObject>()];

    /// <summary>
    /// A parameter as a property, its value resolved: a length as IfcLengthMeasure (metres), an angle as IfcPlaneAngleMeasure
    /// (radians, the file's unit; Lapin holds degrees), yes or no as IfcBoolean, a whole number as IfcInteger, a number as IfcReal,
    /// a text as IfcLabel. Its description: a derived one's formula, else the parameter's own description or label. Null when
    /// its value does not hold.
    /// </summary>
    static IfcStandardProperty? Property(JsonObject p, PartParams.Resolved r)
    {
        var key = (string)p["key"]!;
        if (r.Problems.ContainsKey(key)) return null;
        var description = PartParams.FormulaOf(p) is { } formula ? $"Derived: {formula}"
            : (string?)p["description"] is { Length: > 0 } d ? d
            : (string?)p["label"] is { Length: > 0 } label && label != key ? label : null;
        if (PartParams.IsText(p)) return r.Texts.TryGetValue(key, out var t) ? new IfcStandardProperty(key, "IFCLABEL", t, description) : null;
        if (!r.Numbers.TryGetValue(key, out var v)) return null;
        return (string?)p["kind"] switch
        {
            "angle" => new IfcStandardProperty(key, "IFCPLANEANGLEMEASURE", v * Math.PI / 180, description),
            "bool" => new IfcStandardProperty(key, "IFCBOOLEAN", v != 0, description),
            "integer" => new IfcStandardProperty(key, "IFCINTEGER", (int)Math.Round(v), description),
            "number" => new IfcStandardProperty(key, "IFCREAL", v, description),
            _ => new IfcStandardProperty(key, "IFCLENGTHMEASURE", v, description),
        };
    }
}
