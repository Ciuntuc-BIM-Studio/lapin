using Lapin.Ifc;

namespace Lapin.Ids;

/// <summary>A logical's third value (.U.): neither true nor false, so it meets no value and counts as empty.</summary>
public sealed class Unknown
{
    public static readonly Unknown Value = new();
    public override string ToString() => "UNKNOWN";
}

/// <summary>
/// One value as the IFC file holds it: <see cref="Raw"/> a string (an enumeration's too), a double, a long, a bool, <see cref="Unknown"/>,
/// a <see cref="StepRef"/> (an object) or a list; <see cref="Type"/> the defined type it was written as (IFCLABEL), when it was;
/// <see cref="Si"/> the number in the SI unit IDS states values in (its project's unit converted), for a measure.
/// </summary>
public readonly struct IfcVal(object? Raw, string? Type = null, double? Si = null)
{
    public object? Raw { get; init; } = Raw;
    public string? Type { get; init; } = Type;
    public double? Si { get; init; } = Si;
    public bool IsNull => Raw is null;
    /// <summary>Empty, as IDS counts it: nothing, an empty text, an unknown logical, an empty list.</summary>
    public bool IsEmpty => Raw is null || Raw is string { Length: 0 } || Raw is Unknown || Raw is List<object?> { Count: 0 };

    public override string ToString() => Raw switch
    {
        null => "(none)",
        bool b => b ? "true" : "false",
        double => (Si ?? (double)Raw).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture),
        long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
        StepRef => "(an object)",
        List<object?> l => $"(a list of {l.Count})",
        _ => Raw.ToString() ?? "",
    };
}

/// <summary>A property (or quantity) of an element as IDS reads it: its set and name, how it is written, and its values.</summary>
public sealed class IfcProp(string Set, string Name, string Kind, IReadOnlyList<IfcVal> Values, IfcVal? Lower = null, IfcVal? Upper = null)
{
    public string Set { get; } = Set;
    public string Name { get; } = Name;
    public string Kind { get; } = Kind;
    public IReadOnlyList<IfcVal> Values { get; } = Values;
    public IfcVal? Lower { get; } = Lower;
    public IfcVal? Upper { get; } = Upper;
    public const string Single = "single", Enumerated = "enumerated", List = "list", Bounded = "bounded", Table = "table", Quantity = "quantity", Unsupported = "unsupported";
}

/// <summary>
/// What an IFC file says of its objects, read once for every specification of a run: each object's type, property sets and
/// quantities (its own over its type's), classifications, materials, and the wholes it is part of, by the five relations IDS
/// names; and the project's units, so measures compare in SI. Built from <see cref="StepFile.Rooted"/> in one pass over the
/// relationships; values are read when a facet asks, and kept for the run.
/// </summary>
public sealed class IfcFacts
{
    public StepFile File { get; }
    /// <summary>The file's schema as IDS names them: IFC2X3, IFC4, IFC4X3_ADD2 (an IFC4X3 of another addendum is taken as ADD2).</summary>
    public string Schema { get; }
    /// <summary>The objects a specification may apply to: every rooted entity that is not a relationship nor a property definition, in the file's order.</summary>
    public IReadOnlyList<StepEntity> Objects { get; }

    readonly Dictionary<int, StepEntity> _byId = [];
    readonly Dictionary<int, int> _typeOf = [];
    readonly Dictionary<int, List<int>> _definitions = [];
    readonly Dictionary<int, List<int>> _classifications = [];
    readonly Dictionary<int, List<int>> _materials = [];
    /// <summary>Each object's wholes: the relation's IDS name and the whole.</summary>
    readonly Dictionary<int, List<(string Relation, int Whole)>> _parents = [];
    readonly Dictionary<int, List<int>> _materialProperties = [];
    readonly Dictionary<string, double> _units = [];

    static readonly HashSet<string> PropertyDefinitions =
    [
        "IFCPROPERTYSET", "IFCELEMENTQUANTITY", "IFCPROPERTYSETTEMPLATE", "IFCSIMPLEPROPERTYTEMPLATE", "IFCCOMPLEXPROPERTYTEMPLATE", "IFCPERMEABLECOVERINGPROPERTIES",
        "IFCDOORLININGPROPERTIES", "IFCDOORPANELPROPERTIES", "IFCWINDOWLININGPROPERTIES", "IFCWINDOWPANELPROPERTIES", "IFCREINFORCEMENTDEFINITIONPROPERTIES",
        "IFCPREDEFINEDPROPERTYSET", "IFCQUANTITYSET", "IFCPROPERTYSETDEFINITION",
    ];

    public IfcFacts(StepFile file, CancellationToken stop = default)
    {
        File = file;
        var s = file.Schema.ToUpperInvariant();
        Schema = s.StartsWith("IFC4X3", StringComparison.Ordinal) ? "IFC4X3_ADD2" : s.StartsWith("IFC2X3", StringComparison.Ordinal) ? "IFC2X3" : s.StartsWith("IFC4", StringComparison.Ordinal) ? "IFC4" : s;
        var objects = new List<StepEntity>();
        var n = 0;
        foreach (var e in file.Rooted)
        {
            if ((++n & 4095) == 0) stop.ThrowIfCancellationRequested();
            _byId[e.Id] = e;
            if (e.Type.StartsWith("IFCREL", StringComparison.Ordinal)) Relationship(e);
            else if (!PropertyDefinitions.Contains(e.Type)) objects.Add(e);
        }
        Objects = objects;
        foreach (var mp in file.OfType("IFCMATERIALPROPERTIES").Concat(file.OfType("IFCEXTENDEDMATERIALPROPERTIES")))
            if (mp[mp.Type == "IFCMATERIALPROPERTIES" ? 3 : 0] is StepRef m) Add(_materialProperties, m.Id, mp.Id);
        Units();
    }

    static void Add<T>(Dictionary<int, List<T>> d, int key, T value) { if (!d.TryGetValue(key, out var l)) d[key] = l = []; l.Add(value); }
    static IEnumerable<int> Refs(object? v) => v is List<object?> l ? l.OfType<StepRef>().Select(r => r.Id) : v is StepRef r1 ? [r1.Id] : [];

    void Relationship(StepEntity r)
    {
        switch (r.Type)
        {
            case "IFCRELDEFINESBYTYPE":
                if (r[5] is StepRef t) foreach (var o in Refs(r[4])) _typeOf[o] = t.Id;
                break;
            case "IFCRELDEFINESBYPROPERTIES":
                // IFC4: the definition is a select (one set, or a set of sets in 4.3's IfcPropertySetDefinitionSet).
                foreach (var d in Refs(r[5])) foreach (var o in Refs(r[4])) Add(_definitions, o, d);
                break;
            case "IFCRELASSOCIATESCLASSIFICATION":
                if (r[5] is StepRef c) foreach (var o in Refs(r[4])) Add(_classifications, o, c.Id);
                break;
            case "IFCRELASSOCIATESMATERIAL":
                if (r[5] is StepRef m) foreach (var o in Refs(r[4])) Add(_materials, o, m.Id);
                break;
            case "IFCRELAGGREGATES":
                if (r[4] is StepRef w) foreach (var o in Refs(r[5])) Add(_parents, o, ("IFCRELAGGREGATES", w.Id));
                break;
            case "IFCRELNESTS":
                if (r[4] is StepRef h) foreach (var o in Refs(r[5])) Add(_parents, o, ("IFCRELNESTS", h.Id));
                break;
            case "IFCRELCONTAINEDINSPATIALSTRUCTURE":
                if (r[5] is StepRef st) foreach (var o in Refs(r[4])) Add(_parents, o, ("IFCRELCONTAINEDINSPATIALSTRUCTURE", st.Id));
                break;
            case "IFCRELASSIGNSTOGROUP":
            case "IFCRELASSIGNSTOGROUPBYFACTOR":
                if (r[6] is StepRef g) foreach (var o in Refs(r[4])) Add(_parents, o, ("IFCRELASSIGNSTOGROUP", g.Id));
                break;
            case "IFCRELFILLSELEMENT":
                // The filler (a door, a window) is part of the opening it fills, which is part of what it voids.
                if (r[4] is StepRef op && r[5] is StepRef filler) Add(_parents, filler.Id, (VoidsFills, op.Id));
                break;
            case "IFCRELVOIDSELEMENT":
                if (r[4] is StepRef host && r[5] is StepRef opening) Add(_parents, opening.Id, (VoidsFills, host.Id));
                break;
        }
    }

    public const string VoidsFills = "IFCRELVOIDSELEMENT IFCRELFILLSELEMENT";

    public StepEntity? Get(int id) => _byId.TryGetValue(id, out var e) ? e : File.Entities.Parse(id);
    public StepEntity? Get(object? r) => r is StepRef x ? Get(x.Id) : null;

    /// <summary>The object's type object (IfcRelDefinesByType), if any.</summary>
    public StepEntity? TypeOf(StepEntity e) => _typeOf.TryGetValue(e.Id, out var t) ? Get(t) : null;

    public IReadOnlyList<(string Relation, int Whole)> Parents(int id) => _parents.TryGetValue(id, out var l) ? l : [];

    // ------------------------------------------------------------ attributes

    public static bool IsType(StepEntity e) => (e.Type.EndsWith("TYPE", StringComparison.Ordinal) || e.Type.EndsWith("STYLE", StringComparison.Ordinal)) && e.Args.Count >= 6;

    /// <summary>The object's attributes by name, as IDS's attribute facet reads them (<see cref="IfcAttributes"/>).</summary>
    public IReadOnlyList<(string Name, IfcVal Value)> Attributes(StepEntity e)
    {
        var names = IfcAttributes.Of(e.Type, e.Args.Count, Schema);
        var o = new List<(string, IfcVal)>(names.Count);
        for (var i = 0; i < names.Count && i < e.Args.Count; i++)
        {
            if (names[i] == null) continue;
            var raw = Plain(e.Args[i]);
            var si = IfcAttributes.IsLength(names[i]!) && raw.Raw is double or long ? Number(raw.Raw) * Unit("LENGTHUNIT") : (double?)null;
            o.Add((names[i]!, si != null ? raw with { Si = si } : raw));
        }
        return o;
    }

    static double Number(object? v) => v switch { double d => d, long l => l, _ => 0 };

    /// <summary>A value as read: booleans and logicals from their enumeration, a typed value's type kept.</summary>
    public static IfcVal Plain(object? v) => v switch
    {
        StepEnum { Value: "T" } => new(true),
        StepEnum { Value: "F" } => new(false),
        StepEnum { Value: "U" } => new(Unknown.Value),
        StepEnum en => new(en.Value),
        StepTyped t => Plain(t.Value) with { Type = t.Type },
        _ => new(v),
    };

    /// <summary>The value of a property in SI: its defined type's unit (the project's, or the property's own) applied when it is a measure.</summary>
    IfcVal Measured(object? v, StepEntity? ownUnit)
    {
        var x = Plain(v);
        if (x.Raw is double or long && x.Type is { } t && IfcUnits.UnitTypeOf(t) is { } unitType)
            return x with { Si = Number(x.Raw) * (ownUnit != null ? IfcUnits.Factor(this, ownUnit, 0) : Unit(unitType)) };
        return x;
    }

    // ------------------------------------------------------------ predefined type

    /// <summary>
    /// The object's predefined type as IDS reads it: its type object's when that names one (its ElementType, or ProcessType, when
    /// USERDEFINED), else its own (its ObjectType when USERDEFINED); null when neither has one. <c>UserDefined</c>: it was written as
    /// USERDEFINED, which a facet asking for USERDEFINED meets as well as the name it gives.
    /// </summary>
    public (string? Value, bool UserDefined) PredefinedType(StepEntity e)
    {
        if (TypeOf(e) is { } type && Own(type, true) is { Value: not null } fromType) return fromType;
        return Own(e, IsType(e));
    }

    (string? Value, bool UserDefined) Own(StepEntity e, bool isType)
    {
        var attrs = Attributes(e);
        var pre = attrs.FirstOrDefault(a => a.Name == "PredefinedType").Value.Raw as string;
        if (pre == null) return (null, false);
        if (pre != "USERDEFINED") return (pre, false);
        var named = isType ? attrs.FirstOrDefault(a => a.Name is "ElementType" or "ProcessType").Value.Raw as string
            : attrs.FirstOrDefault(a => a.Name == "ObjectType").Value.Raw as string;
        return (named is { Length: > 0 } ? named : pre, true);
    }

    // ------------------------------------------------------------ properties

    readonly Dictionary<int, List<IfcProp>> _props = [];

    /// <summary>
    /// Every property and quantity of the object: its type's (the type's HasPropertySets and its own definitions), overridden one by one
    /// by the object's own of the same set and name; a material's from its IfcMaterialProperties.
    /// </summary>
    public IReadOnlyList<IfcProp> Properties(StepEntity e)
    {
        if (_props.TryGetValue(e.Id, out var cached)) return cached;
        var mine = new List<IfcProp>();
        if (e.Type is "IFCMATERIAL" or "IFCMATERIALLAYER" or "IFCMATERIALPROFILE" or "IFCMATERIALCONSTITUENT")
        {
            foreach (var mp in _materialProperties.GetValueOrDefault(e.Id) ?? [])
                if (Get(mp) is { } set) Set(set, set.Type == "IFCMATERIALPROPERTIES" ? IfcAttributesText(set[0]) : IfcAttributesText(set[3]), set.Type == "IFCMATERIALPROPERTIES" ? set[2] : set[1], mine);
            return _props[e.Id] = mine;
        }
        foreach (var d in _definitions.GetValueOrDefault(e.Id) ?? []) Definition(Get(d), mine);
        var list = mine;
        if (TypeOf(e) is { } type)
        {
            var theirs = new List<IfcProp>();
            foreach (var d in Refs(type[5])) Definition(Get(d), theirs);
            foreach (var d in _definitions.GetValueOrDefault(type.Id) ?? []) Definition(Get(d), theirs);
            var own = mine.Select(p => (p.Set, p.Name)).ToHashSet();
            list = [.. mine, .. theirs.Where(p => !own.Contains((p.Set, p.Name)))];
        }
        if (IsType(e)) foreach (var d in Refs(e[5])) Definition(Get(d), list);
        return _props[e.Id] = list;
    }

    static string IfcAttributesText(object? v) => v as string ?? (v as StepTyped)?.Value as string ?? "";

    void Definition(StepEntity? d, List<IfcProp> into)
    {
        if (d == null) return;
        switch (d.Type)
        {
            case "IFCPROPERTYSET": Set(d, IfcAttributesText(d[2]), d[4], into); break;
            case "IFCELEMENTQUANTITY": Quantities(d, into); break;
            default:
                // A predefined property set (IfcDoorLiningProperties…): its attributes as its properties, as the IDS manual allows.
                if (d.Type.EndsWith("PROPERTIES", StringComparison.Ordinal) && IfcAttributesText(d[2]) is { Length: > 0 } name)
                {
                    var names = IfcAttributes.Of(d.Type, d.Args.Count, Schema);
                    for (var i = 4; i < names.Count && i < d.Args.Count; i++)
                        if (names[i] is { } pn && d.Args[i] is not (null or StepRef or List<object?>))
                            into.Add(new IfcProp(name, pn, IfcProp.Single, [Measured(d.Args[i], null)]));
                }
                break;
        }
    }

    void Set(StepEntity set, string setName, object? props, List<IfcProp> into)
    {
        foreach (var pid in Refs(props))
        {
            if (Get(pid) is not { } p) continue;
            var name = IfcAttributesText(p[0]);
            switch (p.Type)
            {
                case "IFCPROPERTYSINGLEVALUE":
                    into.Add(new IfcProp(setName, name, IfcProp.Single, [Measured(p[2], Get(p[3]))]));
                    break;
                case "IFCPROPERTYENUMERATEDVALUE":
                    into.Add(new IfcProp(setName, name, IfcProp.Enumerated, p[2] is List<object?> ev ? [.. ev.Select(v => Measured(v, null))] : []));
                    break;
                case "IFCPROPERTYLISTVALUE":
                    into.Add(new IfcProp(setName, name, IfcProp.List, p[2] is List<object?> lv ? [.. lv.Select(v => Measured(v, Get(p[3])))] : []));
                    break;
                case "IFCPROPERTYBOUNDEDVALUE":
                {
                    var unit = Get(p[4]);
                    var upper = p[2] == null ? (IfcVal?)null : Measured(p[2], unit);
                    var lower = p[3] == null ? (IfcVal?)null : Measured(p[3], unit);
                    into.Add(new IfcProp(setName, name, IfcProp.Bounded, [.. new[] { lower, upper }.Where(v => v != null).Select(v => v!.Value)], lower, upper));
                    break;
                }
                case "IFCPROPERTYTABLEVALUE":
                {
                    var values = new List<IfcVal>();
                    if (p[2] is List<object?> defining) values.AddRange(defining.Select(v => Measured(v, Get(p[5]))));
                    if (p[3] is List<object?> defined) values.AddRange(defined.Select(v => Measured(v, Get(p[6]))));
                    into.Add(new IfcProp(setName, name, IfcProp.Table, values));
                    break;
                }
                default:
                    // A complex property, a reference value: IDS 1.0 does not read them.
                    into.Add(new IfcProp(setName, name, IfcProp.Unsupported, []));
                    break;
            }
        }
    }

    void Quantities(StepEntity q, List<IfcProp> into)
    {
        var setName = IfcAttributesText(q[2]);
        foreach (var qid in Refs(q[5]))
        {
            if (Get(qid) is not { } x) continue;
            var measure = x.Type switch
            {
                "IFCQUANTITYLENGTH" => "IFCLENGTHMEASURE", "IFCQUANTITYAREA" => "IFCAREAMEASURE", "IFCQUANTITYVOLUME" => "IFCVOLUMEMEASURE",
                "IFCQUANTITYCOUNT" => "IFCCOUNTMEASURE", "IFCQUANTITYWEIGHT" => "IFCMASSMEASURE", "IFCQUANTITYTIME" => "IFCTIMEMEASURE",
                "IFCQUANTITYNUMBER" => "IFCNUMERICMEASURE", _ => null,
            };
            if (measure == null) { into.Add(new IfcProp(setName, IfcAttributesText(x[0]), IfcProp.Unsupported, [])); continue; }
            var raw = x[3];
            var v = new IfcVal(raw is long l && measure != "IFCCOUNTMEASURE" ? (double)l : raw, measure);
            if (v.Raw is double or long && IfcUnits.UnitTypeOf(measure) is { } ut) v = v with { Si = Number(v.Raw) * (Get(x[2]) is { } own ? IfcUnits.Factor(this, own, 0) : Unit(ut)) };
            into.Add(new IfcProp(setName, IfcAttributesText(x[0]), IfcProp.Quantity, [v]));
        }
    }

    // ------------------------------------------------------------ classifications

    /// <summary>A classification reference as IDS reads it: its system's name, and its code with the codes of the references above it.</summary>
    public sealed class Classified(string? System, IReadOnlyList<string> Codes)
{
    public string? System { get; } = System;
    public IReadOnlyList<string> Codes { get; } = Codes;
}

    /// <summary>The object's classifications: its own, and its type's in the systems it has none of its own in (an occurrence overrides its type per system).</summary>
    public IReadOnlyList<Classified> Classifications(StepEntity e)
    {
        var own = (_classifications.GetValueOrDefault(e.Id) ?? []).Select(Classification).OfType<Classified>().ToList();
        if (TypeOf(e) is { } type)
        {
            var systems = own.Select(c => c.System).ToHashSet();
            own.AddRange((_classifications.GetValueOrDefault(type.Id) ?? []).Select(Classification).OfType<Classified>().Where(c => !systems.Contains(c.System)));
        }
        return own;
    }

    Classified? Classification(int id)
    {
        var x = Get(id);
        if (x == null) return null;
        if (x.Type == "IFCCLASSIFICATION") return new Classified(IfcAttributesText(x[3]), []);
        if (x.Type != "IFCCLASSIFICATIONREFERENCE") return null;
        // Up the references (EF_25_10_25 under EF_25_10) to the system: each code on the way counts, so EF_25_10 meets EF_25_10_25.
        var codes = new List<string>();
        var at = x;
        for (var depth = 0; at != null && depth < StepFile.MaxDepth; depth++)
        {
            if (at.Type == "IFCCLASSIFICATION") return new Classified(IfcAttributesText(at[3]), codes);
            if (at.Type != "IFCCLASSIFICATIONREFERENCE") break;
            // IFC4: Location, Identification, Name, ReferencedSource; IFC2X3: Location, ItemReference, Name, ReferencedSource.
            if (IfcAttributesText(at[1]) is { Length: > 0 } code) codes.Add(code);
            at = Get(at[3]);
        }
        return new Classified(null, codes);
    }

    // ------------------------------------------------------------ materials

    /// <summary>
    /// Every name and category the object's materials give: a material's name and category; a layer set's, profile set's or constituent set's
    /// name, and each of its layers', profiles' or constituents' name, category and material; a list's materials. The type's when the object
    /// has none of its own.
    /// </summary>
    public IReadOnlyList<string>? Materials(StepEntity e)
    {
        var assoc = _materials.GetValueOrDefault(e.Id);
        if ((assoc == null || assoc.Count == 0) && TypeOf(e) is { } type) assoc = _materials.GetValueOrDefault(type.Id);
        if (assoc == null || assoc.Count == 0) return null;
        var names = new List<string>();
        foreach (var m in assoc) Material(Get(m), names, 0);
        return names;
    }

    void Material(StepEntity? m, List<string> names, int depth)
    {
        if (m == null || depth > 8) return;
        void Text(object? v) { if (IfcAttributesText(v) is { Length: > 0 } s) names.Add(s); }
        switch (m.Type)
        {
            case "IFCMATERIAL": Text(m[0]); if (m.Args.Count > 2) Text(m[2]); break;
            case "IFCMATERIALLIST": foreach (var x in Refs(m[0])) Material(Get(x), names, depth + 1); break;
            case "IFCMATERIALLAYERSETUSAGE": Material(Get(m[0]), names, depth + 1); break;
            case "IFCMATERIALPROFILESETUSAGE": case "IFCMATERIALPROFILESETUSAGETAPERING": Material(Get(m[0]), names, depth + 1); break;
            case "IFCMATERIALLAYERSET":
                Text(m[1]);
                foreach (var l in Refs(m[0])) Material(Get(l), names, depth + 1);
                break;
            case "IFCMATERIALLAYER": case "IFCMATERIALLAYERWITHOFFSETS":
                // Material, LayerThickness, IsVentilated, Name, Description, Category, Priority.
                Material(Get(m[0]), names, depth + 1);
                if (m.Args.Count > 3) Text(m[3]);
                if (m.Args.Count > 5) Text(m[5]);
                break;
            case "IFCMATERIALPROFILESET":
                Text(m[0]);
                foreach (var p in Refs(m[2])) Material(Get(p), names, depth + 1);
                break;
            case "IFCMATERIALPROFILE": case "IFCMATERIALPROFILEWITHOFFSETS":
                // Name, Description, Material, Profile, Priority, Category.
                Text(m[0]); Material(Get(m[2]), names, depth + 1); if (m.Args.Count > 5) Text(m[5]);
                break;
            case "IFCMATERIALCONSTITUENTSET":
                Text(m[0]);
                foreach (var c in Refs(m[2])) Material(Get(c), names, depth + 1);
                break;
            case "IFCMATERIALCONSTITUENT":
                // Name, Description, Material, Fraction, Category.
                Text(m[0]); Material(Get(m[2]), names, depth + 1); if (m.Args.Count > 4) Text(m[4]);
                break;
        }
    }

    // ------------------------------------------------------------ units

    void Units()
    {
        var project = File.OfType("IFCPROJECT").FirstOrDefault() ?? File.OfType("IFCPROJECTLIBRARY").FirstOrDefault();
        if (Get(project?[8]) is not { } assignment) return;
        foreach (var u in Refs(assignment[0]).Select(Get).OfType<StepEntity>())
            if (u.Type is "IFCSIUNIT" or "IFCCONVERSIONBASEDUNIT" or "IFCCONVERSIONBASEDUNITWITHOFFSET" && (u[1] as StepEnum)?.Value is { } kind)
                _units[kind] = IfcUnits.Factor(this, u, 0);
    }

    /// <summary>The SI size of the project's unit of a kind (LENGTHUNIT…); 1 when the project states none.</summary>
    public double Unit(string unitType) => _units.TryGetValue(unitType, out var f) ? f : 1;
}

/// <summary>Units: which unit a measure is in, and an IfcSIUnit's or a conversion-based unit's size in SI.</summary>
static class IfcUnits
{
    static readonly Dictionary<string, string> MeasureUnits = new()
    {
        ["IFCLENGTHMEASURE"] = "LENGTHUNIT", ["IFCPOSITIVELENGTHMEASURE"] = "LENGTHUNIT", ["IFCNONNEGATIVELENGTHMEASURE"] = "LENGTHUNIT",
        ["IFCAREAMEASURE"] = "AREAUNIT", ["IFCVOLUMEMEASURE"] = "VOLUMEUNIT", ["IFCMASSMEASURE"] = "MASSUNIT", ["IFCTIMEMEASURE"] = "TIMEUNIT",
        ["IFCPLANEANGLEMEASURE"] = "PLANEANGLEUNIT", ["IFCPOSITIVEPLANEANGLEMEASURE"] = "PLANEANGLEUNIT",
        ["IFCFORCEMEASURE"] = "FORCEUNIT", ["IFCPRESSUREMEASURE"] = "PRESSUREUNIT", ["IFCENERGYMEASURE"] = "ENERGYUNIT", ["IFCPOWERMEASURE"] = "POWERUNIT",
        ["IFCELECTRICCURRENTMEASURE"] = "ELECTRICCURRENTUNIT", ["IFCELECTRICVOLTAGEMEASURE"] = "ELECTRICVOLTAGEUNIT", ["IFCFREQUENCYMEASURE"] = "FREQUENCYUNIT",
        ["IFCTHERMODYNAMICTEMPERATUREMEASURE"] = "THERMODYNAMICTEMPERATUREUNIT", ["IFCLUMINOUSFLUXMEASURE"] = "LUMINOUSFLUXUNIT", ["IFCILLUMINANCEMEASURE"] = "ILLUMINANCEUNIT",
    };

    public static string? UnitTypeOf(string measure) => MeasureUnits.GetValueOrDefault(measure);

    static double Prefix(string? p) => p switch
    {
        "EXA" => 1e18, "PETA" => 1e15, "TERA" => 1e12, "GIGA" => 1e9, "MEGA" => 1e6, "KILO" => 1e3, "HECTO" => 1e2, "DECA" => 10,
        "DECI" => 0.1, "CENTI" => 1e-2, "MILLI" => 1e-3, "MICRO" => 1e-6, "NANO" => 1e-9, "PICO" => 1e-12, "FEMTO" => 1e-15, "ATTO" => 1e-18, _ => 1,
    };

    /// <summary>A unit's size in the SI unit IDS states values in: a prefix squared for square metres, cubed for cubic; a gram a thousandth of a kilogram.</summary>
    public static double Factor(IfcFacts f, StepEntity u, int depth)
    {
        if (u.Type == "IFCSIUNIT")
        {
            var prefix = Prefix((u[2] as StepEnum)?.Value);
            return (u[3] as StepEnum)?.Value switch
            {
                "SQUARE_METRE" => prefix * prefix,
                "CUBIC_METRE" => prefix * prefix * prefix,
                "GRAM" => prefix * 1e-3,
                _ => prefix,
            };
        }
        if (u.Type.StartsWith("IFCCONVERSIONBASEDUNIT", StringComparison.Ordinal) && depth < 4 && f.Get(u[3]) is { } measure)
        {
            var v = IfcFacts.Plain(measure[0]).Raw switch { double d => d, long l => l, _ => 0 };
            if (v > 0) return v * (f.Get(measure[1]) is { } of ? Factor(f, of, depth + 1) : 1);
        }
        return 1;
    }
}
