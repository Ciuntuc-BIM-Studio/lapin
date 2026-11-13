using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A door's or window's data as free text (report #76): U-value, g-value, safety glass, fire rating and fittings, and a few
/// parameters of the user's own ("sauna window", "frosted glass"). They are the user's parameters of the type, with an element's
/// own value over its type's, so tags (<c>{U-value}</c>), schedules and the properties panel read them as they read any;
/// the fire rating is <c>Pset_DoorCommon</c> / <c>Pset_WindowCommon</c>'s FireRating, the rest are in <c>Lapin_Joinery</c>.
/// IFC takes the U-value as ThermalTransmittance when it reads as a number (<see cref="JoineryDataSets"/>).
/// </summary>
public static class JoineryData
{
    /// <summary>The set the data is written to in IFC; the user's, so never „Pset_”.</summary>
    public const string Set = "Lapin_Joinery";

    /// <summary>The fixed fields: the command's key, the parameter's name.</summary>
    public static readonly (string Key, string Name)[] Fields =
    [
        ("uValue", "U-value"), ("gValue", "g-value"), ("safetyGlass", "Safety glass"), ("fireRating", "FireRating"), ("fittings", "Fittings"),
    ];

    static bool IsFixed(string name) => Fields.Any(f => TagText.Squash(f.Name) == TagText.Squash(name));

    /// <summary>The set a field is written to for an entity: the fire rating in the class's common set, the rest in <see cref="Set"/>.</summary>
    public static string SetOf(string key, string ifcClass) => key == "fireRating" ? (ifcClass is IfcClass.Door or IfcClass.DoorType ? "Pset_DoorCommon" : "Pset_WindowCommon") : Set;

    /// <summary>A stored data value of an entity (its type's when it has none of its own).</summary>
    public static string? Get(IModelView m, Entity e, string key)
    {
        var (_, name) = Fields.First(f => f.Key == key);
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        return Of(e, key, name) ?? Of(type, key, name);
    }

    static string? Of(Entity? e, string key, string name)
    {
        if (e == null) return null;
        var pset = SetOf(key, e.Class);
        return (e.Attrs["params"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(p => Params.Same(p, pset, name)) is { } p && ((string?)p["value"]) is { Length: > 0 } v ? v : null;
    }

    /// <summary>
    /// Writes the data to a type or an element: each key present in <paramref name="data"/> is set to its text (empty or null: taken off,
    /// so an element falls back to its type's); with <paramref name="extras"/> ([{name, value}]) the extra parameters are exactly those.
    /// </summary>
    public static void Apply(IEdit edit, Entity target, JsonObject? data, JsonArray? extras)
    {
        var list = target.Attrs["params"] is JsonArray old ? (JsonArray)old.DeepClone() : [];
        void Remove(string? pset, string name)
        {
            foreach (var p in list.OfType<JsonObject>().Where(p => Params.Same(p, pset, name)).ToList()) list.Remove(p);
        }
        void Put(string? pset, string name, string value)
        {
            Remove(pset, name);
            var entry = new JsonObject { ["name"] = name, ["kind"] = "text", ["value"] = value };
            if (pset != null) entry["pset"] = pset;
            list.Add((JsonNode)entry);
        }
        foreach (var (key, name) in Fields)
        {
            if (data == null || !data.ContainsKey(key)) continue;
            var pset = SetOf(key, target.Class);
            var text = ((string?)data[key])?.Trim();
            if (string.IsNullOrEmpty(text)) Remove(pset, name); else Put(pset, name, text);
        }
        if (extras != null)
        {
            foreach (var p in list.OfType<JsonObject>().Where(p => Params.SetOf(p) == Set && !IsFixed((string?)p["name"] ?? "")).ToList()) list.Remove(p);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in extras)
            {
                var name = ((string?)n?["name"])?.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                if (IsFixed(name)) throw new CommandException($"„{name}” is one of the fields above.");
                if (!seen.Add(name)) throw new CommandException($"„{name}” is there twice.");
                Put(Set, name, ((string?)n!["value"])?.Trim() ?? "");
            }
        }
        var attrs = target.CloneAttrs();
        if (list.Count == 0) attrs.Remove("params"); else attrs["params"] = list;
        edit.Replace(target.With(attrs: attrs));
    }

    static readonly Regex Number = new(@"^\s*[-+]?\d+(?:[.,]\d+)?", RegexOptions.Compiled);

    /// <summary>A U-value as text reads as a number: its leading number, a comma taken as the decimal point ("1,1 W/m²K" is 1.1); null when it does not.</summary>
    public static double? Parse(string? text)
    {
        if (text == null || Number.Match(text) is not { Success: true } m) return null;
        return double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : null;
    }

    /// <summary>Whether the type or the element has any of the data: only then are the common set's computed properties written.</summary>
    public static bool Any(IModelView m, Entity e)
    {
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        bool Has(Entity? x) => x != null && (x.Attrs["params"] as JsonArray)?.OfType<JsonObject>().Any(p => Params.SetOf(p) == Set || Fields.Any(f => Params.Same(p, SetOf(f.Key, x.Class), f.Name))) == true;
        return Has(e) || Has(type);
    }
}

/// <summary>
/// A door's or window's standard properties from its data (report #76): ThermalTransmittance when the U-value reads as a number,
/// and for a window GlazingAreaFraction (the glass's share of the opening). Written only for elements that have data, so a project
/// without any exports as it did.
/// </summary>
sealed class JoineryDataSets : IIfcElementSets
{
    public IEnumerable<IfcStandardSet> Sets(IModelView model, Entity element)
    {
        if (element.Class is not (IfcClass.Door or IfcClass.Window)) yield break;
        // The glazing's build-up (forum topic 34) as IFC's Pset_DoorWindowGlazingType names it: the panes, the first three's
        // thicknesses, laminated or not; the g-value as its solar heat gain when it reads as a ratio.
        if (Openings.SpecOf(model, element.Id) is { Frameless: null } gs && GlazingBuildUp.Parse(gs.Sash.Glazing) is { } g)
        {
            var glazing = new List<IfcStandardProperty> { new("GlassLayers", "IFCCOUNTMEASURE", g.Panes.Count) };
            for (var i = 0; i < Math.Min(3, g.Panes.Count); i++) glazing.Add(new($"GlassThickness{i + 1}", "IFCPOSITIVELENGTHMEASURE", Math.Round(g.Panes[i], 5)));
            glazing.Add(new("IsLaminated", "IFCBOOLEAN", g.Laminated));
            if (JoineryData.Parse(JoineryData.Get(model, element, "gValue")) is { } gv && gv <= 1) glazing.Add(new("SolarHeatGainTransmittance", "IFCNORMALISEDRATIOMEASURE", gv));
            yield return new IfcStandardSet("Pset_DoorWindowGlazingType", glazing);
        }
        if (!JoineryData.Any(model, element)) yield break;
        var props = new List<IfcStandardProperty>();
        if (JoineryData.Parse(JoineryData.Get(model, element, "uValue")) is { } u) props.Add(new("ThermalTransmittance", "IFCTHERMALTRANSMITTANCEMEASURE", u));
        if (element.Class == IfcClass.Window && Openings.SpecOf(model, element.Id) is { } spec && spec.Width * spec.Height > 1e-9)
        {
            var glass = Joinery.Boxes(spec).Where(b => b.Role == "glass").Sum(b => (b.Max.X - b.Min.X) * (b.Max.Z - b.Min.Z));
            if (glass > 0) props.Add(new("GlazingAreaFraction", "IFCNORMALISEDRATIOMEASURE", Math.Round(Math.Min(1, glass / (spec.Width * spec.Height)), 4)));
        }
        if (props.Count > 0) yield return new IfcStandardSet(element.Class == IfcClass.Door ? "Pset_DoorCommon" : "Pset_WindowCommon", props);
    }
}
