using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mesh.Fragments;

/// <summary>An attribute as Fragments writes it: <c>[name, value, IFC type]</c>.</summary>
public sealed record FragValue(string Name, JsonNode? Value, string Type);

/// <summary>
/// A property set or a quantity set of an element: its items are IFCPROPERTYSINGLEVALUE (<c>NominalValue</c>) or
/// IFCQUANTITYLENGTH/AREA/VOLUME/WEIGHT/COUNT (<c>LengthValue</c>…), each with its name and, for a quantity, its description.
/// </summary>
public sealed record FragSet(string Name, bool Quantities, string? Description, IReadOnlyList<FragSetItem> Items);

public sealed record FragSetItem(string Category, string Name, FragValue Value, string? Description = null);

/// <summary>What an element carries besides its geometry: its own attributes, its type, its materials, its sets.</summary>
public sealed record FragElementData(IReadOnlyList<FragValue> Attributes, (string Id, string Name)? Type, IReadOnlyList<string> Materials, IReadOnlyList<FragSet> Sets);

/// <summary>
/// What a Fragments file says of a project beyond its triangles (decision 323): the GlobalIds of the project,
/// site and building, the map conversion, and each element's attributes, type, materials, property and
/// quantity sets, the way That Open's own IfcImporter writes them from an IFC file, so a viewer (the
/// properties panel of BubbleBIM Standalone) shows the same for Lapin's file as for the IFC of the same project.
/// </summary>
public sealed class FragContext
{
    public string? ProjectGuid { get; init; }
    public string? SiteGuid { get; init; }
    public string? BuildingGuid { get; init; }
    /// <summary>The metadata's <c>crs</c> (That Open's CRSData), or null.</summary>
    public JsonObject? Crs { get; init; }
    public Func<string, FragElementData?> Element { get; init; } = _ => null;

    /// <summary>The context of a scene built from <paramref name="session"/> with <paramref name="o"/>.</summary>
    public static FragContext Of(Session session, MeshScene scene, MeshExportOptions o)
    {
        var m = session.Model;
        string? Gid(Entity? e) => e?.Id is { Length: 22 } id ? id : null;
        return new FragContext
        {
            ProjectGuid = Gid(Spatial.Project(m)),
            SiteGuid = Gid(m.OfClass(IfcClass.Site).FirstOrDefault()),
            BuildingGuid = Gid(Spatial.Building(m)),
            Crs = CrsOf(m, scene.Origin),
            Element = id => m.Get(id) is { } e ? ElementOf(session, e) : null,
        };
    }

    /// <summary>
    /// The project's tie to the map, as the IFC export's IfcMapConversion has it (the survey point's eastings and
    /// northings, the base point's angle to true north and elevation), moved to the export's origin: Fragments'
    /// CRSData, with the system's name, datum and projection when Lapin knows it. Null without a survey point.
    /// </summary>
    public static JsonObject? CrsOf(IModelView m, (double X, double Y, double Z) origin)
    {
        if (Location.Survey(m) is not { } sv) return null;
        var basePoint = Location.Base(m);
        var th = (basePoint.NorthAngle ?? 0) * Math.PI / 180;
        var (c, sn) = (Math.Cos(th), -Math.Sin(th));
        // map(p) = (E0, N0) + (c·x − sn·y, sn·x + c·y): the project's origin, then the export's.
        var e0 = (sv.Easting ?? 0) - (c * sv.At.X - sn * sv.At.Y);
        var n0 = (sv.Northing ?? 0) - (sn * sv.At.X + c * sv.At.Y);
        var (ox, oy, oz) = origin;
        var known = GeoRef.Get(Location.Crs(m));
        static double R(double v) => Math.Round(v, 6);
        return new JsonObject
        {
            ["name"] = known?.Id ?? Location.Crs(m) ?? "Site grid",
            ["description"] = known?.Name,
            ["geodeticDatum"] = known?.Datum,
            ["verticalDatum"] = null,
            ["mapProjection"] = known?.Projection,
            ["mapZone"] = null,
            ["mapUnit"] = "METRE",
            ["mapConversion"] = new JsonObject
            {
                ["eastings"] = R(e0 + c * ox - sn * oy), ["northings"] = R(n0 + sn * ox + c * oy), ["orthogonalHeight"] = R(basePoint.Elevation + oz),
                ["xAxisAbscissa"] = R(c), ["xAxisOrdinate"] = R(sn), ["scale"] = 1.0,
            },
        };
    }

    static FragElementData ElementOf(Session session, Entity e)
    {
        var m = session.Model;
        var attrs = new List<FragValue>();
        if (e.Str("description") is { Length: > 0 } d) attrs.Add(new("Description", d, "IFCTEXT"));
        attrs.Add(new("Tag", e.Id, "IFCIDENTIFIER"));
        if (e.PredefinedType is { Length: > 0 } p) attrs.Add(new("PredefinedType", p, "IFCLABEL"));
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } t ? (t.Id, t.Name) : ((string, string)?)null;

        // The materials its parts are made of, by name, in the order of its parts.
        var materials = new List<string>();
        try
        {
            foreach (var part in session.Geometry(e.Id)?.Parts ?? [])
                if (part.Material != null && m.Get(part.Material) is { } mat && !materials.Contains(mat.Name)) materials.Add(mat.Name);
        }
        catch (Exception ex) when (Unreadable(ex)) { }

        var sets = new List<FragSet>();
        // The sets an imported element brought from its file (a reference's, a recognised element's), as they came.
        var imported = e.Attrs["properties"] as JsonObject ?? (e.Attrs["imported"] as JsonObject)?["properties"] as JsonObject;
        foreach (var (setName, set) in imported ?? [])
        {
            if (set is not JsonObject props || setName == "Fragments") continue;
            var items = props.Where(kv => kv.Value is JsonValue).Select(kv => Single(kv.Key, kv.Value!.DeepClone(), TypeOf(kv.Value!))).ToList();
            if (items.Count > 0) sets.Add(new FragSet(setName, false, null, items));
        }
        // The user's parameters: those in a named set (Pset_DoorCommon, their own) and the others, Lapin_Parameters, as the IFC export writes them.
        var own = new List<FragSetItem>();
        var named = new Dictionary<string, List<FragSetItem>>();
        foreach (var param in (e.Attrs["params"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            if ((string?)param["name"] is not { } pname || param["value"] is not JsonValue pv) continue;
            var item = Single(pname, pv.DeepClone(), (string?)param["kind"] switch
            {
                "length" => "IFCLENGTHMEASURE", "area" => "IFCAREAMEASURE", "angle" or "number" => "IFCREAL", "bool" => "IFCBOOLEAN", "choice" => "IFCLABEL", _ => TypeOf(pv),
            });
            if (Params.SetOf(param) is { } set) (named.TryGetValue(set, out var l) ? l : named[set] = []).Add(item);
            else own.Add(item);
        }
        foreach (var (set, items) in named) if (!sets.Any(s => s.Name == set)) sets.Add(new FragSet(set, false, null, items));
        if (own.Count > 0) sets.Add(new FragSet("Lapin_Parameters", false, null, own));
        // The quantities the modules measure (Qto_WallBaseQuantities…), a part's named after it.
        IReadOnlyList<QuantitySet> quantities;
        // An element its module cannot measure (a stair without its foot) goes without its quantities, as without a body it would not go at all.
        try { quantities = session.Quantities(e.Id); }
        catch (Exception ex) when (Unreadable(ex)) { quantities = []; }
        foreach (var q in quantities)
        {
            var items = new List<FragSetItem>();
            var seen = new Dictionary<string, int>();
            foreach (var x in q.Quantities.Where(x => double.IsFinite(x.Value)))
            {
                var (category, attr, measure) = x.Kind switch
                {
                    "LENGTH" => ("IFCQUANTITYLENGTH", "LengthValue", "IFCLENGTHMEASURE"),
                    "AREA" => ("IFCQUANTITYAREA", "AreaValue", "IFCAREAMEASURE"),
                    "VOLUME" => ("IFCQUANTITYVOLUME", "VolumeValue", "IFCVOLUMEMEASURE"),
                    "WEIGHT" => ("IFCQUANTITYWEIGHT", "WeightValue", "IFCMASSMEASURE"),
                    "COUNT" => ("IFCQUANTITYCOUNT", "CountValue", "IFCCOUNTMEASURE"),
                    _ => (null, null, null),
                };
                if (category == null) continue;
                var n = seen[x.Name] = seen.GetValueOrDefault(x.Name) + 1;
                JsonNode value = x.Kind == "COUNT" ? (int)Math.Round(x.Value) : Math.Round(x.Value, 9);
                items.Add(new FragSetItem(category, n == 1 ? x.Name : $"{x.Name} ({n})", new FragValue(attr!, value, measure!), x.Label));
            }
            if (items.Count > 0) sets.Add(new FragSet(q.Name, true, q.Part != null ? q.PartName ?? q.Part : null, items));
        }
        return new FragElementData(attrs, type, materials, sets);
    }

    static bool Unreadable(Exception ex) => ex is CommandException or ModelException or InvalidOperationException or ArgumentException or FormatException;

    static FragSetItem Single(string name, JsonNode? value, string type) => new("IFCPROPERTYSINGLEVALUE", name, new FragValue("NominalValue", value, type));

    /// <summary>The IFC type a plain value is written as: a label, a real, a boolean.</summary>
    static string TypeOf(JsonNode v) => v is JsonValue jv && jv.TryGetValue<bool>(out _) ? "IFCBOOLEAN" : Json.Number(v) is not null && v.GetValueKind() == System.Text.Json.JsonValueKind.Number ? "IFCREAL" : "IFCLABEL";
}
