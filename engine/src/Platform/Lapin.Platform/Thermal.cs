using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>One layer as the thermal calculation reads it: its name, its material (an IfcMaterial's id, null when it has none), its thickness (m).</summary>
public sealed record ThermalLayerInput(string Name, string? MaterialId, double Thickness);

/// <summary>One layer worked out: its λ (W/mK) and where it came from (material, indicative, air, none, skipped), what Lapin took it for, its R (m²K/W).</summary>
public sealed record ThermalLayer(int Index, string Name, string? Material, string? MaterialId, double Thickness, double? Lambda, string Source, string? As, double? R);

/// <summary>A surface resistances case (EN ISO 6946 Table 7): its id, label, heat flow and Lapin's indicative Rsi and Rse (m²K/W).</summary>
public sealed record ThermalCase(string Id, string Label, string Flow, double Rsi, double Rse);

/// <summary>An element's or a type's thermal resistance and U-value, as <see cref="Thermal.Of"/> works it out.</summary>
public sealed record ThermalResult(string Id, string Kind, string Case, string? CaseOwn, string From, double Rsi, double Rse, IReadOnlyList<ThermalLayer> Layers,
    double RLayers, double RTotal, double? U, IReadOnlyList<string> Missing, double? Typed, bool Indicative, IReadOnlyList<string> Notes)
{
    public bool Complete => Missing.Count == 0;
    /// <summary>What tags and IFC take: the value the user typed, else the worked-out one.</summary>
    public double? Effective => Typed ?? U;

    public JsonObject ToJson() => new()
    {
        ["id"] = Id, ["kind"] = Kind, ["case"] = Case, ["caseOwn"] = CaseOwn, ["from"] = From, ["rsi"] = Rsi, ["rse"] = Rse,
        ["layers"] = new JsonArray([.. Layers.Select(l => (JsonNode)new JsonObject
        {
            ["index"] = l.Index, ["name"] = l.Name, ["material"] = l.Material, ["materialId"] = l.MaterialId, ["thickness"] = l.Thickness,
            ["lambda"] = l.Lambda, ["source"] = l.Source, ["as"] = l.As, ["r"] = l.R is { } r ? Math.Round(r, 4) : null,
        })]),
        ["rLayers"] = Math.Round(RLayers, 4), ["rTotal"] = Math.Round(RTotal, 4), ["u"] = U is { } u ? Math.Round(u, 4) : null, ["complete"] = Complete,
        ["missing"] = new JsonArray([.. Missing.Select(x => (JsonNode)x)]), ["typed"] = Typed, ["effective"] = Effective is { } ef ? Math.Round(ef, 4) : null,
        ["indicative"] = Indicative, ["notes"] = new JsonArray([.. Notes.Select(x => (JsonNode)x)]),
    };
}

/// <summary>
/// The thermal resistance and U-value of a layered wall, floor or roof (issue #67, decision 717), after EN ISO 6946: each layer's
/// R = d/λ, R total = Rsi + Σ R + Rse, U = 1/R. Worked out on every ask from the layers the element has now (its own layer set
/// when it has one, else its type's; a type from its own), never stored. λ is the material's own (Pset_MaterialThermal.ThermalConductivity,
/// or a parameter Thermal conductivity / λ), else — when the project allows it — Lapin's indicative λ by the material's name
/// (<see cref="IndicativeLambda"/>); an unventilated air layer takes its R from Table 8. A layer without λ is listed as missing and
/// U is not given: Lapin does not guess. The surface resistances come by case (heat flow, what is on the other side), the
/// project's own over Lapin's indicative ones. A ThermalTransmittance the user typed on the element or its type wins (tags, IFC).
/// </summary>
public static class Thermal
{
    public const string SettingsKey = "thermalSettings", CaseKey = "thermalCase";

    /// <summary>Lapin's surface resistances (EN ISO 6946:2017 Table 7, the other side's case as § 6.8 takes it), indicative.</summary>
    public static readonly ThermalCase[] Cases =
    [
        new("wall", "External wall", "horizontal", 0.13, 0.04),
        new("wallUnheated", "Wall to an unheated space", "horizontal", 0.13, 0.13),
        new("wallGround", "Wall against the ground", "horizontal", 0.13, 0),
        new("roof", "Roof", "upward", 0.10, 0.04),
        new("ceilingUnheated", "Ceiling under an unheated space", "upward", 0.10, 0.10),
        new("floor", "Floor over outside air", "downward", 0.17, 0.04),
        new("floorUnheated", "Floor over an unheated space", "downward", 0.17, 0.17),
        new("floorGround", "Floor on the ground", "downward", 0.17, 0),
    ];

    /// <summary>Lapin's indicative λ (W/mK) by a material's name, the first that matches (typical tabulated values after EN ISO 10456 and C107/3; not normative).</summary>
    static readonly (Regex Name, double Lambda, string As)[] Table =
    [
        (R(@"gypsum ?(plaster)?board|plasterboard|drywall|rigips|gips ?carton|placa de gips"), 0.25, "gypsum board"),
        (R(@"gypsum plaster|tencuiala (de )?gips|ipsos"), 0.40, "gypsum plaster"),
        (R(@"reinforced concrete|beton armat|\bc\d\d/\d\d"), 2.30, "reinforced concrete"),
        (R(@"aerated|\baac\b|\bbca\b|ytong|gasbeton|celular"), 0.16, "autoclaved aerated concrete"),
        (R(@"screed|\bsapa\b"), 1.40, "cement screed"),
        (R(@"concrete|beton"), 2.00, "concrete"),
        (R(@"hollow brick|perforated brick|caramida cu goluri|\bgvp\b"), 0.46, "hollow brick masonry"),
        (R(@"ceramic block|bloc(uri)? ceramic|porotherm|thermoblock"), 0.30, "ceramic block masonry"),
        (R(@"brick|caramid|clinker"), 0.80, "solid brick masonry"),
        (R(@"\bxps\b|extruded|extrudat"), 0.035, "extruded polystyrene (XPS)"),
        (R(@"graphite|grafit|\beps ?(gri|grey|gray)"), 0.032, "graphite EPS"),
        (R(@"\beps\b|polystyren|polistiren"), 0.040, "expanded polystyrene (EPS)"),
        (R(@"\bpir\b|\bpur\b|polyurethane|poliuretan"), 0.024, "PIR / PUR board"),
        (R(@"mineral wool|rock ?wool|stone ?wool|glass ?wool|\bwool\b|vata"), 0.040, "mineral wool"),
        (R(@"wood fibre|wood fiber|fibra lemnoasa"), 0.045, "wood fibre board"),
        (R(@"\bosb\b|plywood|placaj|\bclt\b|glulam|timber|softwood|lumber|\bwood\b|\blemn\b|rasinoase"), 0.13, "softwood timber"),
        (R(@"\boak\b|stejar|hardwood|parquet|parchet"), 0.18, "hardwood"),
        (R(@"laminate|laminat"), 0.17, "laminate flooring"),
        (R(@"ceramic tile|floor tile|\btiles?\b|gresie|faianta"), 1.30, "ceramic tiles"),
        (R(@"carpet|mocheta"), 0.06, "carpet"),
        (R(@"render|tencuiala|plaster|mortar"), 0.87, "lime-cement plaster"),
        (R(@"vapou?r|foil|folie|polyethylene|polietilena"), 0.33, "polyethylene foil"),
        (R(@"bitum|waterproof|hidroizola|membrane|membrana"), 0.23, "bitumen"),
        (R(@"glass|sticla|\bgeam\b"), 1.00, "glass"),
        (R(@"alumin"), 160, "aluminium"),
        (R(@"stainless|\binox\b"), 17, "stainless steel"),
        (R(@"steel|\botel\b"), 50, "steel"),
    ];

    static readonly Regex Air = R(@"\bair\b|cavity|\baer\b|lama de aer");

    static Regex R(string p) => new(p, RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Lapin's indicative λ for a material's name, and what it took it for; null when the name says nothing Lapin knows.</summary>
    public static (double Lambda, string As)? IndicativeLambda(string? name)
    {
        var n = Lapin.Platform.MaterialLook.Fold(name);
        foreach (var (re, lambda, what) in Table) if (re.IsMatch(n)) return (lambda, what);
        return null;
    }

    /// <summary>Whether a material's name is an air layer's (an unventilated cavity).</summary>
    public static bool IsAir(string? name) => Air.IsMatch(Lapin.Platform.MaterialLook.Fold(name));

    /// <summary>An unventilated air layer's R (m²K/W) by its thickness and the heat flow (EN ISO 6946 Table 8, between the rows linearly), indicative.</summary>
    public static double AirResistance(double thickness, string flow)
    {
        double[] mm = [0, 5, 7, 10, 15, 25, 50, 100, 300];
        double[] r = flow switch
        {
            "upward" => [0, 0.11, 0.13, 0.15, 0.16, 0.16, 0.16, 0.16, 0.16],
            "downward" => [0, 0.11, 0.13, 0.15, 0.17, 0.19, 0.21, 0.22, 0.23],
            _ => [0, 0.11, 0.13, 0.15, 0.17, 0.18, 0.18, 0.18, 0.18],
        };
        var d = thickness * 1000;
        if (d >= mm[^1]) return r[^1];
        for (var i = 1; i < mm.Length; i++)
            if (d <= mm[i]) return r[i - 1] + (r[i] - r[i - 1]) * (d - mm[i - 1]) / (mm[i] - mm[i - 1]);
        return r[^1];
    }

    /// <summary>The material's own λ (W/mK): Pset_MaterialThermal.ThermalConductivity, else a parameter named Thermal conductivity, λ or Lambda; null without one.</summary>
    public static double? OwnLambda(Entity? material)
    {
        if (material == null) return null;
        var f = MaterialFields.Of(material);
        foreach (var key in new[] { "Pset_MaterialThermal.ThermalConductivity", "ThermalConductivity", "λ", "lambda" })
            if (f.Param(key) is { } p && Number(p.Value) is { } v && v > 0 && double.IsFinite(v)) return v;
        return null;
    }

    static double? Number(JsonNode? v) =>
        v is JsonValue jv && jv.TryGetValue<string>(out var s) ? FieldValue.StrictNumber(s.Replace("W/mK", "").Replace("W/m²K", "").Trim()) : Json.Number(v);

    // ─── the layers of each class

    static readonly ConcurrentDictionary<string, Func<IModelView, Entity, (string Kind, IReadOnlyList<ThermalLayerInput> Layers, string From, IReadOnlyList<string> Notes)?>> Providers = new(StringComparer.Ordinal);

    /// <summary>A module's layered class (a roof's own build-up): its kind (wall, floor, roof), its layers, whose they are, notes.</summary>
    public static void AddLayers(string cls, Func<IModelView, Entity, (string Kind, IReadOnlyList<ThermalLayerInput> Layers, string From, IReadOnlyList<string> Notes)?> layers) =>
        Providers[cls] = layers;

    /// <summary>The classes worked out.</summary>
    public static IEnumerable<string> Classes => new[] { IfcClass.Wall, IfcClass.WallType, IfcClass.Slab, IfcClass.SlabType }.Concat(Providers.Keys).Distinct();

    static IReadOnlyList<ThermalLayerInput> Inputs(IModelView m, IReadOnlyList<MaterialLayer> layers) =>
        layers.Select((l, i) => new ThermalLayerInput(l.Name is { Length: > 0 } n ? n : m.Get(l.Material)?.Name ?? $"Layer {i + 1}", l.Material, l.Thickness)).ToList();

    /// <summary>The kind and the layers of a wall, a slab or their types (the element's own set through its usage, else its type's).</summary>
    static (string Kind, IReadOnlyList<ThermalLayerInput> Layers, string From, IReadOnlyList<string> Notes)? LayersOf(IModelView m, Entity e)
    {
        if (Providers.TryGetValue(e.Class, out var p)) return p(m, e);
        string? kind = e.Class switch { IfcClass.Wall or IfcClass.WallType => "wall", IfcClass.Slab or IfcClass.SlabType => "floor", _ => null };
        if (kind == null) return null;
        if (kind == "floor" && SlabKind(m, e) == "ROOF") kind = "roof";
        if (e.Class is IfcClass.WallType or IfcClass.SlabType)
            return LayerSets.LayersOf(m, e.Id) is { Count: > 0 } own ? (kind, Inputs(m, own), "type", []) : null;
        if (LayerSets.UsageOf(m, e.Id) is { } u && Layers.ReadUsage(u) is { } usage && m.Get(usage.ForLayerSet) is { } set)
        {
            var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
            var from = type != null && LayerSets.Of(m, type.Id)?.Id == set.Id ? "type" : "element";
            var layers = Layers.Read(set);
            return layers.Count > 0 ? (kind, Inputs(m, layers), from, []) : null;
        }
        if (m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } t && LayerSets.LayersOf(m, t.Id) is { Count: > 0 } typed) return (kind, Inputs(m, typed), "type", []);
        return null;
    }

    static string? SlabKind(IModelView m, Entity e) =>
        e.PredefinedType is { Length: > 0 } p && p != "NOTDEFINED" ? p : m.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.PredefinedType;

    /// <summary>The case an element or a type takes by its kind: a wall's external wall, a roof's, a floor on the ground (a base slab) or over outside air.</summary>
    static string DefaultCase(IModelView m, Entity e, string kind) => kind switch
    {
        "wall" => "wall",
        "roof" => "roof",
        _ => SlabKind(m, e) == "BASESLAB" ? "floorGround" : "floor",
    };

    /// <summary>The case chosen on the element, else on its type; null when neither chose one.</summary>
    static string? OwnCase(IModelView m, Entity e)
    {
        if (e.Str(CaseKey) is { } c && Cases.Any(x => x.Id == c)) return c;
        return m.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.Str(CaseKey) is { } tc && Cases.Any(x => x.Id == tc) ? tc : null;
    }

    // ─── the project's settings

    /// <summary>The project's surface resistances for a case, and whether they are Lapin's (indicative).</summary>
    public static (double Rsi, double Rse, bool Own) Resistances(IModelView m, string caseId)
    {
        var c = Cases.First(x => x.Id == caseId);
        if (Settings(m)?["cases"]?[caseId] is JsonObject o && Json.Number(o["rsi"]) is { } rsi && Json.Number(o["rse"]) is { } rse) return (rsi, rse, true);
        return (c.Rsi, c.Rse, false);
    }

    static JsonObject? Settings(IModelView m) => Spatial.Project(m)?.Attrs[SettingsKey] as JsonObject;

    /// <summary>Whether materials without their own λ take Lapin's indicative one by name (yes unless the project said no).</summary>
    public static bool UsesIndicative(IModelView m) => !(Settings(m)?["indicative"] is JsonValue v && v.TryGetValue<bool>(out var b) && !b);

    /// <summary>A ThermalTransmittance (or U-value) the user typed as a number on the element, else on its type, else one its file gave it.</summary>
    public static double? Typed(IModelView m, Entity e)
    {
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        foreach (var x in new[] { e, type })
            foreach (var p in (x?.Attrs["params"] as JsonArray)?.OfType<JsonObject>() ?? [])
                if (TagText.Squash((string?)p["name"] ?? "") is "thermaltransmittance" or "uvalue" && !ParamFormulas.IsFormula(p) && Number(p["value"]) is { } v && v > 0) return v;
        foreach (var x in new[] { e, type })
            if (TagText.ImportedNode(x, "ThermalTransmittance") is { } node && Number(node is JsonObject o ? o["value"] : node) is { } iv && iv > 0) return iv;
        return null;
    }

    // ─── the calculation

    /// <summary>The element's or type's thermal resistance and U-value; null when it has no layers (or is not of a layered class).</summary>
    public static ThermalResult? Of(IModelView m, Entity e)
    {
        if (LayersOf(m, e) is not { } found || found.Layers.Count == 0 || found.Layers.Sum(l => l.Thickness) <= 0) return null;
        var own = OwnCase(m, e);
        var caseId = own ?? DefaultCase(m, e, found.Kind);
        var flow = Cases.First(x => x.Id == caseId).Flow;
        var (rsi, rse, ownResistances) = Resistances(m, caseId);
        var indicative = !ownResistances;
        var useIndicative = UsesIndicative(m);
        var layers = new List<ThermalLayer>();
        var missing = new List<string>();
        var sum = 0.0;
        for (var i = 0; i < found.Layers.Count; i++)
        {
            var l = found.Layers[i];
            var mat = l.MaterialId != null ? m.Get(l.MaterialId) : null;
            var name = mat?.Name;
            if (l.Thickness <= 1e-9) { layers.Add(new(i, l.Name, name, l.MaterialId, l.Thickness, null, "skipped", null, 0)); continue; }
            if (OwnLambda(mat) is { } lambda)
            {
                var r = l.Thickness / lambda;
                sum += r;
                layers.Add(new(i, l.Name, name, l.MaterialId, l.Thickness, lambda, "material", null, r));
            }
            else if (IsAir(name) || IsAir(l.Name))
            {
                var r = AirResistance(l.Thickness, flow);
                sum += r;
                indicative = true;
                layers.Add(new(i, l.Name, name, l.MaterialId, l.Thickness, null, "air", "unventilated air layer", r));
            }
            else if (useIndicative && (IndicativeLambda(name) ?? IndicativeLambda(l.Name)) is { } ind)
            {
                var r = l.Thickness / ind.Lambda;
                sum += r;
                indicative = true;
                layers.Add(new(i, l.Name, name, l.MaterialId, l.Thickness, ind.Lambda, "indicative", ind.As, r));
            }
            else
            {
                missing.Add(l.Name);
                layers.Add(new(i, l.Name, name, l.MaterialId, l.Thickness, null, "none", null, null));
            }
        }
        var total = rsi + sum + rse;
        double? u = missing.Count == 0 && total > 0 ? 1 / total : null;
        return new ThermalResult(e.Id, found.Kind, caseId, own, found.From, rsi, rse, layers, sum, total, u, missing, Typed(m, e), indicative, found.Notes);
    }

    static string N(double v, int digits = 3) => Math.Round(v, digits).ToString("0.###", CultureInfo.InvariantCulture);

    // ─── registration

    public static void Register(IModuleRegistry r)
    {
        foreach (var cls in new[] { IfcClass.Wall, IfcClass.WallType, IfcClass.Slab, IfcClass.SlabType, IfcClass.Roof }) AddFields(cls);

        // An element's or a type's R and U (issue #67): its layers with λ and R, the case and its surface resistances, what is missing; null without layers.
        r.Query("thermal.of", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which element?");
            return Of(q.Model, e)?.ToJson();
        });

        // The surface resistances by case (the project's over Lapin's indicative ones) and whether Lapin's indicative λ fill in.
        r.Query("thermal.settings", (q, _) => new JsonObject
        {
            ["indicative"] = UsesIndicative(q.Model),
            ["cases"] = new JsonArray([.. Cases.Select(c =>
            {
                var (rsi, rse, own) = Resistances(q.Model, c.Id);
                return (JsonNode)new JsonObject { ["id"] = c.Id, ["label"] = c.Label, ["flow"] = c.Flow, ["rsi"] = rsi, ["rse"] = rse, ["defaultRsi"] = c.Rsi, ["defaultRse"] = c.Rse, ["own"] = own };
            })]),
        });

        // {cases?: {id: {rsi, rse} | null}, indicative?}: the project's surface resistances (null: back to Lapin's) and whether indicative λ fill in.
        r.Command("thermal.setSettings", "Thermal settings", (ctx, a) =>
        {
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = project.CloneAttrs();
            var s = attrs[SettingsKey] as JsonObject ?? [];
            if (a["cases"] is JsonObject cases)
            {
                var own = s["cases"] as JsonObject ?? [];
                foreach (var (id, v) in cases)
                {
                    if (!Cases.Any(c => c.Id == id)) throw new CommandException($"„{id}” is not a case ({string.Join(", ", Cases.Select(c => c.Id))}).");
                    if (v is null) { own.Remove(id); continue; }
                    if (Json.Number(v["rsi"]) is not { } rsi || Json.Number(v["rse"]) is not { } rse || !(rsi >= 0 && rsi <= 1 && rse >= 0 && rse <= 1))
                        throw new CommandException("Rsi and Rse are resistances from 0 to 1 m²K/W.");
                    own[id] = new JsonObject { ["rsi"] = rsi, ["rse"] = rse };
                }
                s.Remove("cases");
                if (own.Count > 0) s["cases"] = own;
            }
            else if (a.ContainsKey("cases") && a["cases"] is not null) throw new CommandException("The cases are {id: {rsi, rse}}.");
            if (a["indicative"] is JsonValue iv)
            {
                if (!iv.TryGetValue<bool>(out var b)) throw new CommandException("Indicative is yes or no.");
                if (b) s.Remove("indicative"); else s["indicative"] = false;
            }
            if (s.Count > 0) attrs[SettingsKey] = s; else attrs.Remove(SettingsKey);
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });

        // {ids, case}: the surface resistances' case of elements or types (null: by its kind).
        r.Command("thermal.setCase", "Thermal case", (ctx, a) =>
        {
            var ids = a["ids"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().Distinct().ToList() : (string?)a["id"] is { } one ? [one] : [];
            if (ids.Count == 0) throw new CommandException("Which elements?");
            var c = (string?)a["case"];
            if (c is { Length: > 0 } && !Cases.Any(x => x.Id == c)) throw new CommandException($"„{c}” is not a case ({string.Join(", ", Cases.Select(x => x.Id))}).");
            foreach (var id in ids)
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException("No such element.");
                if (LayersOf(ctx.Model, e) == null) throw new CommandException($"„{e.Name}” has no layers to work out.");
                var attrs = e.CloneAttrs();
                if (c is { Length: > 0 }) attrs[CaseKey] = c; else attrs.Remove(CaseKey);
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });
    }

    /// <summary>The fields tags, schedules and formulas read: UValue, RValue, UValueMissing and ThermalTransmittance (the typed one first).</summary>
    public static void AddFields(string cls)
    {
        DerivedFields.Add(cls, new("UValue", "U-value, worked out (W/m²K)", (m, e) => Of(m, e)?.U is { } u ? DerivedFields.Number(u) : null));
        DerivedFields.Add(cls, new("RValue", "Thermal resistance R, worked out (m²K/W)", (m, e) => Of(m, e) is { Complete: true } t ? DerivedFields.Number(t.RTotal) : null));
        DerivedFields.Add(cls, new("UValueMissing", "U-value: layers without λ", (m, e) => Of(m, e) is { Missing.Count: > 0 } t ? string.Join(", ", t.Missing) : null));
        DerivedFields.Add(cls, new("ThermalTransmittance", "Thermal transmittance U (typed, else worked out)", (m, e) => (Typed(m, e) ?? Of(m, e)?.U) is { } v ? N(v) : null));
    }
}

/// <summary>Pset_WallCommon / Pset_SlabCommon / Pset_RoofCommon's ThermalTransmittance from the worked-out U, when nothing was typed and every layer has its λ.</summary>
public sealed class ThermalIfcSets : IIfcElementSets
{
    public IEnumerable<IfcStandardSet> Sets(IModelView model, Entity element)
    {
        if (element.Class is not (IfcClass.Wall or IfcClass.Slab or IfcClass.Roof)) yield break;
        if (Thermal.Of(model, element) is not { U: { } u, Typed: null }) yield break;
        yield return new IfcStandardSet($"Pset_{element.Class[3..]}Common", [new("ThermalTransmittance", "IFCTHERMALTRANSMITTANCEMEASURE", Math.Round(u, 4))]);
    }
}
