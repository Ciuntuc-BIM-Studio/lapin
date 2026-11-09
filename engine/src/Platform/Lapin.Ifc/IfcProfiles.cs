using Lapin.Geometry;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;
using static Lapin.Ifc.Step;

namespace Lapin.Ifc;

/// <summary>The profiles an IFC file was read into, what was left out, and the values that differ from Lapin's.</summary>
public sealed record IfcProfilesRead(List<SectionProfile> Profiles, int Skipped, List<string> Notes, List<ProfileCheck> Checks);

/// <summary>
/// Profile libraries from any IFC file (docs/plan-profile-libraries.md): every <c>IfcProfileDef</c> subtype it holds, named or
/// used by an element, with the mechanical values of <c>IfcProfileProperties</c> / <c>Pset_ProfileMechanical</c> (IFC4) and
/// <c>IfcGeneralProfileProperties</c>, <c>IfcStructuralProfileProperties</c>, <c>IfcStructuralSteelProfileProperties</c>
/// (IFC2x3). A parametric profile keeps its kind and sizes; one with a position that is not the identity, and arbitrary,
/// composite and derived ones, become outlines. Exporters disagree on the units of these values and on which of Y and Z is the
/// strong axis, so both are settled by the geometry: per measure across the file the unit nearest Lapin's values, per profile
/// the pairing of Y and Z nearest them.
/// </summary>
public sealed partial class IfcModel
{
    static readonly string[] ProfileTypes =
    [
        "IFCISHAPEPROFILEDEF", "IFCASYMMETRICISHAPEPROFILEDEF", "IFCUSHAPEPROFILEDEF", "IFCTSHAPEPROFILEDEF", "IFCLSHAPEPROFILEDEF", "IFCCSHAPEPROFILEDEF", "IFCZSHAPEPROFILEDEF",
        "IFCRECTANGLEHOLLOWPROFILEDEF", "IFCCIRCLEHOLLOWPROFILEDEF", "IFCRECTANGLEPROFILEDEF", "IFCROUNDEDRECTANGLEPROFILEDEF", "IFCCIRCLEPROFILEDEF", "IFCELLIPSEPROFILEDEF",
        "IFCARBITRARYCLOSEDPROFILEDEF", "IFCARBITRARYPROFILEDEFWITHVOIDS", "IFCCOMPOSITEPROFILEDEF", "IFCDERIVEDPROFILEDEF",
    ];

    /// <summary>A value read from the file before its unit is settled: its number, its dimension, its own unit's size if it says one.</summary>
    readonly record struct RawValue(double Value, string Dimension, double? Scale);

    public IfcProfilesRead ReadProfiles(string material = "steel", string? source = null)
    {
        var notes = new List<string>();
        // Profiles that are parts of another (a composite's, a derived one's parent) are not profiles of the library.
        var parts = new HashSet<int>();
        foreach (var c in File.OfType("IFCCOMPOSITEPROFILEDEF")) foreach (var id in Refs(c[2])) parts.Add(id);
        foreach (var d in File.OfType("IFCDERIVEDPROFILEDEF")) if (d[2] is StepRef r) parts.Add(r.Id);

        var raw = RawProfileValues();
        var lapin = LapinProfileValues();
        var materials = new Dictionary<int, (string? Name, string? Material)>();
        foreach (var m in File.OfType("IFCMATERIALPROFILE"))
            if (m[3] is StepRef pr) materials.TryAdd(pr.Id, (Str(m[0]), Str(File.Get(m[2])?[0])));
        Dictionary<int, string>? owners = null;

        var read = new List<(SectionProfile P, Dictionary<string, RawValue> Values)>();
        var skipped = 0;
        var curves = 0;
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in ProfileTypes)
            foreach (var e in File.OfType(type))
            {
                if (parts.Contains(e.Id)) continue;
                if ((e[0] as StepEnum)?.Value == "CURVE") { curves++; continue; }
                materials.TryGetValue(e.Id, out var mat);
                var name = Str(e[1]) is { Length: > 0 } own ? own.Trim() : mat.Name is { Length: > 0 } mn ? mn.Trim() : (owners ??= ProfileOwners()).GetValueOrDefault(e.Id);
                var extra = lapin.GetValueOrDefault(e.Id);
                var p = ProfileOf(e, name, extra, mat.Material is { } mm ? ProfileTables.MaterialOf(mm) ?? material : material, source);
                if (p == null || p.Problem() != null) { skipped++; continue; }
                // The same name and size many times over (every element's own copy): one profile. The same name with another size: its own.
                var key = p.ToJson().ToJsonString();
                var label = p.Name;
                if (seen.TryGetValue(label, out var known))
                {
                    if (known == key) continue;
                    for (var n = 2; seen.TryGetValue(label = $"{p.Name} ({n})", out known); n++) if (known == key) break;
                    if (known == key) continue;
                    p = p with { Name = label };
                }
                seen[label] = key;
                read.Add((p, raw.GetValueOrDefault(e.Id) ?? []));
            }

        var scales = SettleScales(read);
        var checks = new List<ProfileCheck>();
        var profiles = new List<SectionProfile>();
        foreach (var (p, values) in read)
        {
            var given = GivenValues(p, values, scales);
            var done = p with { Given = given };
            profiles.Add(done);
            checks.AddRange(ProfileProps.Checks(done));
        }
        if (curves > 0) notes.Add($"{curves} centre-line or curve profile(s) left out: they have no area.");
        var unknown = File.OfType("IFCCENTERLINEPROFILEDEF").Count() + File.OfType("IFCTRAPEZIUMPROFILEDEF").Count() + File.OfType("IFCOPENCROSSPROFILEDEF").Count();
        if (unknown > 0) notes.Add($"{unknown} profile(s) of a kind Lapin does not read (centre-line, trapezium, open cross) left out.");
        return new(profiles, skipped, notes, checks);
    }

    SectionProfile? ProfileOf(StepEntity e, string? name, Dictionary<string, string>? extra, string material, string? source)
    {
        double L(int i) => e[i] is null ? 0 : Math.Abs(Num(e[i])) * _len;
        double Slope(int i) => e[i] is null ? 0 : Math.Tan(Num(e[i]) * _angle);
        SectionProfile With(string kind, double h, double b, double tw = 0, double tf = 0) => new()
        {
            Name = name ?? SectionProfile.LabelOf(kind, h, b, tw, tf), Kind = kind, H = h, B = b, Tw = tw, Tf = tf, Material = material, Source = source,
        };
        SectionProfile? p = null;
        switch (e.Type)
        {
            case "IFCISHAPEPROFILEDEF": p = With("I", L(4), L(3), L(5), L(6)) with { R1 = L(7), R2 = L(8), Slope = Slope(9) }; break;
            case "IFCASYMMETRICISHAPEPROFILEDEF":
                // IFC4 and IFC2x3 agree on the places used here: the bottom flange at 3 and 6, the top at 8 and 9. Lapin's B is the top flange.
                p = With("asymI", L(4), e[8] is null ? L(3) : L(8), L(5), e[9] is null ? L(6) : L(9)) with { B2 = L(3), Tf2 = L(6), R1 = L(7) };
                if (Math.Abs(p.B2 - p.B) < 1e-9 && Math.Abs(p.Tf2 - p.Tf) < 1e-9) p = p with { Kind = "I", B2 = 0, Tf2 = 0 };
                break;
            case "IFCUSHAPEPROFILEDEF": p = With("U", L(3), L(4), L(5), L(6)) with { R1 = L(7), R2 = L(8), Slope = Slope(9) }; break;
            case "IFCTSHAPEPROFILEDEF": p = With("T", L(3), L(4), L(5), L(6)) with { R1 = L(7), R2 = L(8) }; break;
            case "IFCLSHAPEPROFILEDEF": p = With("L", L(3), e[4] is null ? L(3) : L(4), L(5), L(5)) with { R1 = L(6), R2 = L(7) }; break;
            case "IFCCSHAPEPROFILEDEF": p = With("C", L(3), L(4), L(5), L(5)) with { Lip = L(6) }; break;
            case "IFCZSHAPEPROFILEDEF":
                // IFC's flange runs from the web's far face; Lapin's width is the whole section's. Unequal thicknesses only as an outline.
                if (Math.Abs(L(5) - L(6)) < 1e-9) p = With("Z", L(3), 2 * L(4) - L(5), L(5), L(5));
                break;
            case "IFCRECTANGLEHOLLOWPROFILEDEF": p = With("RHS", L(4), L(3), L(5), L(5)) with { R1 = L(7), R2 = L(6) }; break;
            case "IFCCIRCLEHOLLOWPROFILEDEF": p = With("CHS", 2 * L(3), 2 * L(3), L(4), L(4)); break;
            case "IFCRECTANGLEPROFILEDEF" or "IFCROUNDEDRECTANGLEPROFILEDEF": p = With("rectangle", L(4), L(3)); break;
            case "IFCCIRCLEPROFILEDEF": p = With("circle", 2 * L(3), 2 * L(3)); break;
            case "IFCELLIPSEPROFILEDEF": p = With("ellipse", 2 * L(4), 2 * L(3)); break;
        }
        // Lapin's own file: an outline that was a Z, a Σ or a lipless C keeps its kind and sizes in Lapin_Profile.
        if (extra != null && p == null && extra.TryGetValue("Kind", out var k) && SectionProfile.KindOf(k) is { } kind && kind != "outline")
        {
            double X(string f) => extra.TryGetValue(f, out var v) && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
            p = With(kind, X("Depth"), X("Width"), X("Web"), X("Flange")) with { Lip = X("Lip"), R1 = X("Root"), B2 = X("Width2"), Tf2 = X("Flange2") };
        }
        // Any other, or one placed off its origin: its outline as the geometry reads it.
        var placed = p != null && e.Type is not ("IFCARBITRARYCLOSEDPROFILEDEF" or "IFCARBITRARYPROFILEDEFWITHVOIDS" or "IFCCOMPOSITEPROFILEDEF" or "IFCDERIVEDPROFILEDEF") && !IsIdentity(e[2]);
        if (p == null || placed)
        {
            var loops = new List<List<Vec2>>();
            foreach (var (outer, holes) in Profile(e))
            {
                loops.Add([.. outer.Select(v => new Vec2(v.X, v.Y))]);
                foreach (var h in holes) loops.Add([.. h.Select(v => new Vec2(v.X, v.Y))]);
            }
            if (loops.Count == 0 || !loops.Any(l => l.Count >= 3 && Math.Abs(Polygon2.SignedArea(l)) > 1e-12)) return null;
            p = ProjectProfiles.FromOutlines(loops, name ?? "Outline", material, source);
            if (name == null) p = p with { Name = SectionProfile.LabelOf("outline", p.H, p.B, 0, 0) };
        }
        if (extra != null)
        {
            string? E(string f) => extra.TryGetValue(f, out var v) && v.Length > 0 ? v : null;
            double? D(string f) => E(f) is { } v && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
            p = p with
            {
                Family = E("Family") ?? p.Family, Standard = E("Standard") ?? p.Standard, Material = E("Material") ?? p.Material, Category = E("Category") ?? p.Category,
                Density = D("Density") ?? p.Density, R2 = D("ToeRadius") ?? p.R2,
            };
        }
        return p with { Family = p.Family ?? ProfileTables.FamilyOrKind(p.Name, p.Kind) };
    }

    bool IsIdentity(object? position)
    {
        if (position is null) return true;
        var xf = Axis2(position);
        return xf.O.Length < 1e-6 && (xf.X - new Vec3(1, 0, 0)).Length < 1e-6;
    }

    /// <summary>What names an unnamed profile: the type or element whose body it is swept from (its type's name, else its object type, else its name).</summary>
    Dictionary<int, string> ProfileOwners()
    {
        var o = new Dictionary<int, string>();
        var solidOf = new Dictionary<int, int>();
        foreach (var t in new[] { "IFCEXTRUDEDAREASOLID", "IFCEXTRUDEDAREASOLIDTAPERED", "IFCREVOLVEDAREASOLID", "IFCFIXEDREFERENCESWEPTAREASOLID", "IFCSURFACECURVESWEPTAREASOLID" })
            foreach (var s in File.OfType(t)) if (s[0] is StepRef r) solidOf.TryAdd(s.Id, r.Id);
        if (solidOf.Count == 0) return o;
        var repOf = new Dictionary<int, List<int>>();
        foreach (var rep in File.OfType("IFCSHAPEREPRESENTATION"))
            foreach (var item in Refs(rep[3]))
                if (solidOf.TryGetValue(item, out var profile)) (repOf.TryGetValue(rep.Id, out var l) ? l : repOf[rep.Id] = []).Add(profile);
        var byShape = new Dictionary<int, List<int>>();
        foreach (var pds in File.OfType("IFCPRODUCTDEFINITIONSHAPE"))
            foreach (var rep in Refs(pds[2]))
                if (repOf.TryGetValue(rep, out var ps)) (byShape.TryGetValue(pds.Id, out var l) ? l : byShape[pds.Id] = []).AddRange(ps);
        var byMap = new Dictionary<int, List<int>>();
        foreach (var map in File.OfType("IFCREPRESENTATIONMAP"))
            if (map[1] is StepRef mr && repOf.TryGetValue(mr.Id, out var ps)) byMap[map.Id] = ps;
        var typeOf = new Dictionary<int, string>();
        foreach (var rel in File.OfType("IFCRELDEFINESBYTYPE"))
            if (Str(File.Get(rel[5])?[2]) is { Length: > 0 } tn) foreach (var e in Refs(rel[4])) typeOf.TryAdd(e, tn);
        foreach (var e in File.Rooted)
        {
            if (e.Type.EndsWith("TYPE", StringComparison.Ordinal))
            {
                // A type's body by its representation maps (IfcTypeProduct's seventh attribute).
                if (Str(e[2]) is { Length: > 0 } tn)
                    foreach (var m in Refs(e[6])) if (byMap.TryGetValue(m, out var ps)) foreach (var p in ps) o.TryAdd(p, tn);
            }
            else if (e[6] is StepRef shape && byShape.TryGetValue(shape.Id, out var ps))
            {
                var n = typeOf.GetValueOrDefault(e.Id) ?? (Str(e[4]) is { Length: > 0 } ot ? ot : Str(e[2]));
                if (n is { Length: > 0 }) foreach (var p in ps) o.TryAdd(p, n.Trim());
            }
        }
        return o;
    }

    /// <summary>Lapin's own property set on a profile (Family, Standard, Material…): the strings it wrote.</summary>
    Dictionary<int, Dictionary<string, string>> LapinProfileValues()
    {
        var o = new Dictionary<int, Dictionary<string, string>>();
        foreach (var pp in File.OfType("IFCPROFILEPROPERTIES"))
        {
            if (Str(pp[0]) != ProfileIfc.LapinPset || pp[3] is not StepRef r) continue;
            var d = o.TryGetValue(r.Id, out var x) ? x : o[r.Id] = [];
            foreach (var prop in Items(pp[2]))
                if (Str(prop[0]) is { } n && prop[2] is StepTyped t)
                    d[n] = t.Value switch
                    {
                        string s => s,
                        double v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                        long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        _ => "",
                    };
        }
        return o;
    }

    /// <summary>The mechanical values of every profile, as the file writes them, under Lapin's working names (Y, Z not yet paired).</summary>
    Dictionary<int, Dictionary<string, RawValue>> RawProfileValues()
    {
        var o = new Dictionary<int, Dictionary<string, RawValue>>();
        Dictionary<string, RawValue> Of(int id) => o.TryGetValue(id, out var d) ? d : o[id] = [];
        foreach (var pp in File.OfType("IFCPROFILEPROPERTIES"))
        {
            if (pp[3] is not StepRef r || Str(pp[0]) == ProfileIfc.LapinPset) continue;
            var d = Of(r.Id);
            foreach (var prop in Items(pp[2]))
            {
                if (prop.Type != "IFCPROPERTYSINGLEVALUE" || Str(prop[0]) is not { } name || !PsetNames.TryGetValue(name, out var m) || prop[2] is not StepTyped t) continue;
                var scale = File.Get(prop[3]) is { } unit ? UnitSize(unit, 0) : (double?)null;
                d[m.Key] = new RawValue(Num(t), m.Dimension, scale);
            }
        }
        // IFC2x3: positional. General, then structural, then steel.
        void Add(Dictionary<string, RawValue> d, StepEntity e, int at, string key, string dim) { if (e[at] is not null && Num(e[at]) is var v && v != 0) d[key] = new RawValue(v, dim, null); }
        foreach (var type in new[] { "IFCGENERALPROFILEPROPERTIES", "IFCSTRUCTURALPROFILEPROPERTIES", "IFCSTRUCTURALSTEELPROFILEPROPERTIES" })
            foreach (var e in File.OfType(type))
            {
                if (e[1] is not StepRef r) continue;
                var d = Of(r.Id);
                Add(d, e, 2, "mass", "mass"); Add(d, e, 3, "perimeter", "length"); Add(d, e, 6, "A", "area");
                if (type == "IFCGENERALPROFILEPROPERTIES") continue;
                Add(d, e, 7, "It", "inertia"); Add(d, e, 9, "IY", "inertia"); Add(d, e, 10, "IZ", "inertia"); Add(d, e, 11, "Iw", "warping");
                Add(d, e, 16, "WmaxY", "modulus"); Add(d, e, 17, "WminY", "modulus"); Add(d, e, 18, "WmaxZ", "modulus"); Add(d, e, 19, "WminZ", "modulus");
                if (type == "IFCSTRUCTURALPROFILEPROPERTIES") continue;
                Add(d, e, 25, "psY", "ratio"); Add(d, e, 26, "psZ", "ratio");
            }
        return o;
    }

    static readonly Dictionary<string, (string Key, string Dimension)> PsetNames = new()
    {
        ["MassPerLength"] = ("mass", "mass"), ["CrossSectionArea"] = ("A", "area"), ["Perimeter"] = ("perimeter", "length"),
        ["MomentOfInertiaY"] = ("IY", "inertia"), ["MomentOfInertiaZ"] = ("IZ", "inertia"), ["TorsionalConstantX"] = ("It", "inertia"), ["WarpingConstant"] = ("Iw", "warping"),
        ["MaximumSectionModulusY"] = ("WmaxY", "modulus"), ["MinimumSectionModulusY"] = ("WminY", "modulus"), ["MaximumSectionModulusZ"] = ("WmaxZ", "modulus"), ["MinimumSectionModulusZ"] = ("WminZ", "modulus"),
        ["PlasticShapeFactorY"] = ("psY", "ratio"), ["PlasticShapeFactorZ"] = ("psZ", "ratio"),
    };

    /// <summary>A unit's size in SI: an SI unit by its prefix to its name's power (MILLI SQUARE_METRE: 10⁻⁶), a derived one by its elements, a conversion-based one by its factor.</summary>
    double UnitSize(StepEntity u, int depth)
    {
        if (depth > 4) return 1;
        switch (u.Type)
        {
            case "IFCSIUNIT":
            {
                var prefix = UnitFactor(File, u, 0);
                return (u[3] as StepEnum)?.Value switch
                {
                    "SQUARE_METRE" => prefix * prefix, "CUBIC_METRE" => prefix * prefix * prefix,
                    "GRAM" => prefix * 1e-3,
                    _ => prefix,
                };
            }
            case "IFCDERIVEDUNIT":
            {
                var s = 1.0;
                foreach (var el in Items(u[0]))
                    if (File.Get(el[0]) is { } of) s *= Math.Pow(UnitSize(of, depth + 1), Num(el[1]));
                return s;
            }
            case "IFCCONVERSIONBASEDUNIT" or "IFCCONVERSIONBASEDUNITWITHOFFSET":
                return File.Get(u[3]) is { } m && Num(m[0]) is var v && v > 0 ? v * (File.Get(m[1]) is { } of2 ? UnitSize(of2, depth + 1) : 1) : UnitFactor(File, u, 0);
            default: return 1;
        }
    }

    /// <summary>The project's assignment for a dimension (its measure's unit type), its size in SI; null when it has none.</summary>
    double? AssignedUnit(string dimension)
    {
        var type = dimension switch
        {
            "area" => "AREAUNIT", "inertia" => "MOMENTOFINERTIAUNIT", "modulus" => "SECTIONMODULUSUNIT", "warping" => "WARPINGCONSTANTUNIT", "mass" => "MASSPERLENGTHUNIT", "length" => "LENGTHUNIT", _ => null,
        };
        if (type == null) return null;
        var context = File.OfType("IFCPROJECT").FirstOrDefault() ?? File.OfType("IFCPROJECTLIBRARY").FirstOrDefault();
        foreach (var u in Items(File.Get(context?[8])?[0]))
        {
            if ((u[1] as StepEnum)?.Value == type) return UnitSize(u, 0);
        }
        return null;
    }

    /// <summary>For each dimension without a unit of its own: the size (the project's assignment, SI, or the length unit's power) that brings the file's values nearest Lapin's.</summary>
    Dictionary<string, double> SettleScales(List<(SectionProfile P, Dictionary<string, RawValue> Values)> read)
    {
        double Power(string dim) => dim switch { "area" => _len * _len, "inertia" => Math.Pow(_len, 4), "modulus" => Math.Pow(_len, 3), "warping" => Math.Pow(_len, 6), "length" => _len, _ => 1 };
        var o = new Dictionary<string, double>();
        var computed = read.Select(r => ProfileProps.Compute(r.P).Values).ToList();
        foreach (var dim in new[] { "area", "inertia", "modulus", "warping", "mass", "length" })
        {
            var options = new[] { AssignedUnit(dim), Power(dim), 1.0 }.OfType<double>().Distinct().ToList();
            var best = options[0];
            var bestError = double.MaxValue;
            foreach (var option in options)
            {
                var errors = new List<double>();
                for (var i = 0; i < read.Count; i++)
                    foreach (var (key, v) in read[i].Values)
                    {
                        if (v.Dimension != dim || v.Scale != null) continue;
                        var target = ComputedFor(key, computed[i]);
                        if (target is > 0 && Math.Abs(v.Value) > 0) errors.Add(Math.Abs(Math.Log10(Math.Abs(v.Value) * option / target.Value)));
                    }
                if (errors.Count == 0) continue;
                errors.Sort();
                var median = errors[errors.Count / 2];
                // Nearly as good: the first (the file's own word) stands.
                if (median < bestError - 0.05) (best, bestError) = (option, median);
            }
            o[dim] = best;
        }
        return o;
    }

    static double? ComputedFor(string key, IReadOnlyDictionary<string, double> c) => key switch
    {
        "A" => c.GetValueOrDefault("A"), "mass" => c.GetValueOrDefault("mass"), "perimeter" => c.GetValueOrDefault("paint"), "It" => c.GetValueOrDefault("It"), "Iw" => c.GetValueOrDefault("Iw"),
        // Whichever axis: only a size is asked of the unit.
        "IY" or "IZ" => Math.Sqrt(c.GetValueOrDefault("Iy") * c.GetValueOrDefault("Iz")),
        "WminY" or "WminZ" or "WmaxY" or "WmaxZ" => Math.Sqrt(c.GetValueOrDefault("Wely") * c.GetValueOrDefault("Welz")),
        _ => null,
    };

    /// <summary>A profile's values in SI under Lapin's names, Y and Z paired with the strong and weak axes by the geometry.</summary>
    static Dictionary<string, double> GivenValues(SectionProfile p, Dictionary<string, RawValue> values, Dictionary<string, double> scales)
    {
        var o = new Dictionary<string, double>();
        if (values.Count == 0) return o;
        double? V(string key) => values.TryGetValue(key, out var v) ? Math.Abs(v.Value) * (v.Scale ?? scales.GetValueOrDefault(v.Dimension, 1)) : null;
        var c = ProfileProps.Compute(p).Values;
        if (V("A") is { } a) o["A"] = a;
        if (V("mass") is { } m) o["mass"] = m;
        if (V("perimeter") is { } per) o["paint"] = per;
        if (V("It") is { } it) o["It"] = it;
        if (V("Iw") is { } iw && iw > 0) o["Iw"] = iw;
        // Which of the file's Y and Z is the strong axis: the pairing nearer Lapin's values.
        static double Miss(double? given, double computed) => given is { } g && g > 0 && computed > 0 ? Math.Abs(Math.Log(g / computed)) : 0;
        bool Swapped(double? y, double? z, double strong, double weak) => Miss(y, weak) + Miss(z, strong) < Miss(y, strong) + Miss(z, weak);
        var (iy, iz) = (V("IY"), V("IZ"));
        var swapI = Swapped(iy, iz, c.GetValueOrDefault("Iy"), c.GetValueOrDefault("Iz"));
        if ((swapI ? iz : iy) is { } strongI) o["Iy"] = strongI;
        if ((swapI ? iy : iz) is { } weakI) o["Iz"] = weakI;
        // The elastic modulus is the least of a section's two (to its farthest fibre).
        static double? Least(double? a, double? b) => a is { } x && b is { } y ? Math.Min(x, y) : a ?? b;
        var (wy, wz) = (Least(V("WminY"), V("WmaxY")), Least(V("WminZ"), V("WmaxZ")));
        var swapW = Swapped(wy, wz, c.GetValueOrDefault("Wely"), c.GetValueOrDefault("Welz"));
        var (wely, welz) = swapW ? (wz, wy) : (wy, wz);
        if (wely is { } we) o["Wely"] = we;
        if (welz is { } wz2) o["Welz"] = wz2;
        // A plastic modulus only as a shape factor over the elastic one.
        var (psy, psz) = swapW ? (V("psZ"), V("psY")) : (V("psY"), V("psZ"));
        if (wely is { } w1 && psy is { } f1 && f1 > 0) o["Wply"] = w1 * f1;
        if (welz is { } w2 && psz is { } f2 && f2 > 0) o["Wplz"] = w2 * f2;
        return o;
    }
}

/// <summary>A profile library written as an IFC4 file (docs/plan-profile-libraries.md): an <c>IfcProjectLibrary</c> in millimetres, each profile in its parametric subtype, outlines and the shapes IFC has no form for as arbitrary profiles, with <c>Pset_ProfileMechanical</c> and Lapin's own <c>Lapin_Profile</c>.</summary>
public static class ProfileIfc
{
    public const string LapinPset = "Lapin_Profile";

    public static byte[] Write(ProfileLibrary library, string application = "Lapin")
    {
        var f = new Step { Application = application };
        const string Metre = "$";
        var mm = f.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), E("MILLI"), E("METRE"));
        var area = f.Add("IFCSIUNIT", Derived, E("AREAUNIT"), E("MILLI"), E("SQUARE_METRE"));
        var angle = f.Add("IFCSIUNIT", Derived, E("PLANEANGLEUNIT"), Metre, E("RADIAN"));
        var kg = f.Add("IFCSIUNIT", Derived, E("MASSUNIT"), E("KILO"), E("GRAM"));
        var metre = f.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), Metre, E("METRE"));
        string Power(string type, int power) => f.Add("IFCDERIVEDUNIT", L(new[] { f.Add("IFCDERIVEDUNITELEMENT", mm, I(power)) }), E(type), Null);
        var inertia = Power("MOMENTOFINERTIAUNIT", 4);
        var modulus = Power("SECTIONMODULUSUNIT", 3);
        var warping = Power("WARPINGCONSTANTUNIT", 6);
        var massLength = f.Add("IFCDERIVEDUNIT", L(new[] { f.Add("IFCDERIVEDUNITELEMENT", kg, I(1)), f.Add("IFCDERIVEDUNITELEMENT", metre, I(-1)) }), E("MASSPERLENGTHUNIT"), Null);
        var units = f.Add("IFCUNITASSIGNMENT", L(new[] { mm, area, angle, inertia, modulus, warping, massLength }));
        f.Add("IFCPROJECTLIBRARY", S(Guid("lapin-profiles", library.Name)), Null, S(library.Name), S(library.Description), Null, Null, Null, Null, units);
        foreach (var p in library.Profiles)
        {
            string? id = Parametric(f, p, 1000) ?? Arbitrary(f, p);
            if (id == null) continue;
            WriteProperties(f, p, id);
        }
        using var ms = new MemoryStream();
        f.Write(ms, library.Name, "Lapin profile library");
        return ms.ToArray();
    }

    /// <summary>
    /// A profile in its parametric subtype, centred on its box as IFC defines them (Position left out: it is the identity),
    /// the sizes in the file's length (<paramref name="perMetre"/> of them to a metre); null when IFC has no form for its kind
    /// (an outline, a Σ, a C without lips, a Z with lips or unequal thicknesses).
    /// </summary>
    public static string? Parametric(Step f, SectionProfile p, double perMetre)
    {
        string V(double m) => R(m * perMetre);
        string Opt(double m) => m > 0 ? V(m) : Null;
        string Slope() => p.Slope > 0 ? R(Math.Atan(p.Slope)) : Null;
        return p.Kind switch
        {
            "I" => f.Add("IFCISHAPEPROFILEDEF", E("AREA"), S(p.Name), Null, V(p.B), V(p.H), V(p.Tw), V(p.Tf), Opt(p.R1), Opt(p.R2), Slope()),
            "asymI" => f.Add("IFCASYMMETRICISHAPEPROFILEDEF", E("AREA"), S(p.Name), Null, V(p.B2), V(p.H), V(p.Tw), V(p.Tf2), Opt(p.R1), V(p.B), V(p.Tf), Opt(p.R1), Null, Null, Null, Null),
            "U" => f.Add("IFCUSHAPEPROFILEDEF", E("AREA"), S(p.Name), Null, V(p.H), V(p.B), V(p.Tw), V(p.Tf), Opt(p.R1), Opt(p.R2), Slope()),
            "T" => f.Add("IFCTSHAPEPROFILEDEF", E("AREA"), S(p.Name), Null, V(p.H), V(p.B), V(p.Tw), V(p.Tf), Opt(p.R1), Opt(p.R2), Null, Null, Null),
            "L" => f.Add("IFCLSHAPEPROFILEDEF", E("AREA"), S(p.Name), Null, V(p.H), V(p.B), V(p.Tw), Opt(p.R1), Opt(p.R2), Null),
            "RHS" => f.Add("IFCRECTANGLEHOLLOWPROFILEDEF", E("AREA"), S(p.Name), Null, V(p.B), V(p.H), V(p.Tw), Opt(p.R2), Opt(p.R1)),
            "CHS" => f.Add("IFCCIRCLEHOLLOWPROFILEDEF", E("AREA"), S(p.Name), Null, V(p.B / 2), V(p.Tw)),
            "C" when p.Lip > 0 => f.Add("IFCCSHAPEPROFILEDEF", E("AREA"), S(p.Name), Null, V(p.H), V(p.B), V(p.Tw), V(p.Lip), Null),
            // IFC's flange runs from the web's far face; Lapin's width is the whole section's.
            "Z" when p.Lip <= 0 => f.Add("IFCZSHAPEPROFILEDEF", E("AREA"), S(p.Name), Null, V(p.H), V((p.B + p.Tw) / 2), V(p.Tw), V(p.Tw), Null, Null),
            "rectangle" => f.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), S(p.Name), Null, V(p.B), V(p.H)),
            "circle" => f.Add("IFCCIRCLEPROFILEDEF", E("AREA"), S(p.Name), Null, V(p.B / 2)),
            "ellipse" => f.Add("IFCELLIPSEPROFILEDEF", E("AREA"), S(p.Name), Null, V(p.B / 2), V(p.H / 2)),
            _ => null,
        };
    }

    /// <summary>A profile by its rings: one outside, with or without holes; several as a composite of arbitrary profiles.</summary>
    static string? Arbitrary(Step f, SectionProfile p)
    {
        var rings = p.Kind == "outline" ? p.Rings ?? [] : p.Outline();
        var outsides = rings.Where(r => r.Count >= 3 && Polygon2.SignedArea(r) > 0).ToList();
        if (outsides.Count == 0) return null;
        var holes = rings.Where(r => r.Count >= 3 && Polygon2.SignedArea(r) < 0).ToList();
        string Line(IReadOnlyList<Vec2> r) =>
            f.Add("IFCPOLYLINE", L(r.Concat(r.Take(1)).Select(v => f.Add("IFCCARTESIANPOINT", L(new[] { R(v.X * 1000), R(v.Y * 1000) })))));
        string One(IReadOnlyList<Vec2> outside, string? name)
        {
            var inside = holes.Where(h => Polygon2.Contains(outside, h[0])).ToList();
            return inside.Count == 0
                ? f.Add("IFCARBITRARYCLOSEDPROFILEDEF", E("AREA"), S(name), Line(outside))
                : f.Add("IFCARBITRARYPROFILEDEFWITHVOIDS", E("AREA"), S(name), Line(outside), L(inside.Select(Line)));
        }
        if (outsides.Count == 1) return One(outsides[0], p.Name);
        return f.Add("IFCCOMPOSITEPROFILEDEF", E("AREA"), S(p.Name), L(outsides.Select(o => One(o, null))), Null);
    }

    /// <summary>The units a model's profile values are written in when its project has no assignment for them (SI, as its lengths): by a property's dimension.</summary>
    public sealed class SiUnits(Step f)
    {
        string? _inertia, _modulus, _warping, _area, _mass, _metre, _kg;
        string Metre => _metre ??= f.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), Null, E("METRE"));
        string Power(ref string? slot, string type, int power) => slot ??= f.Add("IFCDERIVEDUNIT", L(new[] { f.Add("IFCDERIVEDUNITELEMENT", Metre, I(power)) }), E(type), Null);
        public string? Of(string name) => name switch
        {
            "MassPerLength" => _mass ??= f.Add("IFCDERIVEDUNIT", L(new[] { f.Add("IFCDERIVEDUNITELEMENT", _kg ??= f.Add("IFCSIUNIT", Derived, E("MASSUNIT"), E("KILO"), E("GRAM")), I(1)), f.Add("IFCDERIVEDUNITELEMENT", Metre, I(-1)) }), E("MASSPERLENGTHUNIT"), Null),
            "CrossSectionArea" => _area ??= f.Add("IFCSIUNIT", Derived, E("AREAUNIT"), Null, E("SQUARE_METRE")),
            "MomentOfInertiaY" or "MomentOfInertiaZ" or "TorsionalConstantX" => Power(ref _inertia, "MOMENTOFINERTIAUNIT", 4),
            "WarpingConstant" => Power(ref _warping, "WARPINGCONSTANTUNIT", 6),
            "MinimumSectionModulusY" or "MinimumSectionModulusZ" => Power(ref _modulus, "SECTIONMODULUSUNIT", 3),
            "Perimeter" => Metre,
            _ => null,
        };
    }

    /// <summary>A profile's <c>Pset_ProfileMechanical</c> (the file's values where it gave them, else Lapin's) and <c>Lapin_Profile</c> on <paramref name="id"/>; in the library's millimetres, or in SI with the units said (<paramref name="si"/>: a model's).</summary>
    public static void WriteProperties(Step f, SectionProfile p, string id, SiUnits? si = null)
    {
        var c = ProfileProps.Compute(p).Values;
        double? V(string k) => p.Given.TryGetValue(k, out var g) ? g : c.TryGetValue(k, out var x) ? x : null;
        var props = new List<string>();
        void Prop(string name, string type, double? v) { if (v is { } x && double.IsFinite(x) && x > 0) props.Add(f.Add("IFCPROPERTYSINGLEVALUE", S(name), Null, Typed(type, R(x)), si?.Of(name) ?? Null)); }
        var (mm, mm2, mm3, mm4, mm6) = si != null ? (1.0, 1.0, 1.0, 1.0, 1.0) : (1e3, 1e6, 1e9, 1e12, 1e18);
        Prop("MassPerLength", "IFCMASSPERLENGTHMEASURE", V("mass"));
        Prop("CrossSectionArea", "IFCAREAMEASURE", V("A") * mm2);
        Prop("Perimeter", "IFCPOSITIVELENGTHMEASURE", V("paint") * mm);
        Prop("MomentOfInertiaY", "IFCMOMENTOFINERTIAMEASURE", V("Iy") * mm4);
        Prop("MomentOfInertiaZ", "IFCMOMENTOFINERTIAMEASURE", V("Iz") * mm4);
        Prop("TorsionalConstantX", "IFCMOMENTOFINERTIAMEASURE", V("It") * mm4);
        if (V("Iw") is > 0) Prop("WarpingConstant", "IFCWARPINGCONSTANTMEASURE", V("Iw") * mm6);
        Prop("MinimumSectionModulusY", "IFCSECTIONMODULUSMEASURE", V("Wely") * mm3);
        Prop("MinimumSectionModulusZ", "IFCSECTIONMODULUSMEASURE", V("Welz") * mm3);
        if (V("Wply") is { } wpy && V("Wely") is > 0) Prop("PlasticShapeFactorY", "IFCPOSITIVERATIOMEASURE", wpy / V("Wely"));
        if (V("Wplz") is { } wpz && V("Welz") is > 0) Prop("PlasticShapeFactorZ", "IFCPOSITIVERATIOMEASURE", wpz / V("Welz"));
        if (props.Count > 0) f.Add("IFCPROFILEPROPERTIES", S("Pset_ProfileMechanical"), Null, L(props), id);

        var own = new List<string>();
        void Text(string name, string? v) { if (!string.IsNullOrEmpty(v)) own.Add(f.Add("IFCPROPERTYSINGLEVALUE", S(name), Null, Typed("IFCLABEL", S(v)), Null)); }
        void Size(string name, double v) { if (v > 0) own.Add(f.Add("IFCPROPERTYSINGLEVALUE", S(name), Null, Typed("IFCREAL", v.ToString("R", System.Globalization.CultureInfo.InvariantCulture) is var t && t.Contains('.') ? t : t + "."), Null)); }
        Text("Family", p.Family); Text("Standard", p.Standard); Text("Material", p.Material); Text("Category", p.Category);
        if (p.Density is { } d) Size("Density", d);
        // What IFC's own form of it cannot hold: a T's or an arbitrary's kind and sizes (metres).
        if (p.Kind is "Z" or "SIGMA" || p.Kind == "C" && p.Lip <= 0)
        {
            Text("Kind", p.Kind);
            Size("Depth", p.H); Size("Width", p.B); Size("Web", p.Tw); Size("Flange", p.Tf); Size("Lip", p.Lip); Size("Root", p.R1);
        }
        if (own.Count > 0) f.Add("IFCPROFILEPROPERTIES", S(LapinPset), Null, L(own), id);
    }
}
