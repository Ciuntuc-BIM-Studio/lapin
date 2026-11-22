using System.Globalization;
using System.Text;
using Lapin.Geometry;
using static Lapin.Ifc.Step;

namespace Lapin.Ifc;

/// <summary>
/// The solids of a STEP file (AP203, AP214, AP242: MANIFOLD_SOLID_BREP of advanced faces, or of planar faces on poly loops) read as
/// triangles, in metres (docs/plan-studio-parametric.md X2, its start). ISO 10303-42's entities are IFC4's geometry under other names, with
/// a name first: each is read as the IFC entity it is, a rational B-spline's complex instance as IFC's rational subtype, and tessellated by
/// the IFC reader's advanced B-rep (<see cref="IfcModel"/>), so both formats come in the one way.
/// </summary>
public static class StepBreps
{
    /// <summary>Each solid: its name and its triangles (metres).</summary>
    public static List<(string Name, MeshBuilder Mesh)> Read(byte[] bytes)
    {
        var step = StepFile.Read(bytes);
        var unit = LengthUnit(step);
        var text = new StringBuilder("ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION((''),'2;1');\nFILE_NAME('','',(''),(''),'','','');\nFILE_SCHEMA(('IFC4'));\nENDSEC;\nDATA;\n");
        // The file's unit as IFC's, so the reader lays its curves by the chord rule in metres; any other unit scaled afterwards.
        var prefix = unit switch { 0.001 => ".MILLI.", 0.01 => ".CENTI.", 0.1 => ".DECI.", 1 => "$", 1000 => ".KILO.", _ => null };
        var top = step.Entities.Keys.DefaultIfEmpty(0).Max() + 1;
        text.Append('#').Append(top).Append("=IFCSIUNIT(*,.LENGTHUNIT.,").Append(prefix ?? "$").Append(",.METRE.);\n");
        var after = prefix == null ? unit : 1;
        var solids = new List<(int Id, string Name)>();
        foreach (var id in step.Entities.Keys)
        {
            var e = step.Entities[id];
            if (e.Type == StepFile.Complex)
            {
                if (step.Partials(id) is { } parts && Rational(parts) is { } r) text.Append('#').Append(id).Append('=').Append(r).Append(";\n");
                continue;
            }
            if (e.Type == "MANIFOLD_SOLID_BREP") solids.Add((id, e[0] as string ?? ""));
            var name = e.Type switch
            {
                "MANIFOLD_SOLID_BREP" => "IFCADVANCEDBREP",
                "FACE_SURFACE" => "IFCFACESURFACE",
                _ when Known.Contains(e.Type) => "IFC" + e.Type.Replace("_", "", StringComparison.Ordinal),
                _ => null,
            };
            if (name == null) continue;
            text.Append('#').Append(id).Append('=').Append(name).Append('(').Append(string.Join(',', e.Args.Skip(1).Select(Value))).Append(");\n");
        }
        text.Append("ENDSEC;\nEND-ISO-10303-21;\n");
        var model = new IfcModel(StepFile.Parse(text.ToString()));
        var o = new List<(string, MeshBuilder)>();
        foreach (var (id, name) in solids)
        {
            if (model.ItemMesh(id) is not { } mesh) continue;
            if (after != 1) for (var i = 0; i < mesh.V.Count; i++) mesh.V[i] *= after;
            o.Add((name, mesh));
        }
        return o;
    }

    /// <summary>The entities read as IFC's of the same name (each a representation item or topology with its name first).</summary>
    static readonly HashSet<string> Known =
    [
        "CARTESIAN_POINT", "DIRECTION", "VECTOR", "AXIS2_PLACEMENT_3D", "LINE", "CIRCLE", "ELLIPSE", "B_SPLINE_CURVE_WITH_KNOTS", "POLYLINE",
        "PLANE", "CYLINDRICAL_SURFACE", "CONICAL_SURFACE", "SPHERICAL_SURFACE", "TOROIDAL_SURFACE", "B_SPLINE_SURFACE_WITH_KNOTS",
        "VERTEX_POINT", "EDGE_CURVE", "ORIENTED_EDGE", "EDGE_LOOP", "POLY_LOOP", "FACE_OUTER_BOUND", "FACE_BOUND", "ADVANCED_FACE", "CLOSED_SHELL",
    ];

    /// <summary>A rational B-spline curve or surface's partial records as IFC's rational entity, or null for another complex instance.</summary>
    static string? Rational(List<(string Type, List<object?> Args)> parts)
    {
        List<object?>? Of(string t) => parts.FirstOrDefault(p => p.Type == t).Args;
        if (Of("B_SPLINE_SURFACE") is { } s && Of("B_SPLINE_SURFACE_WITH_KNOTS") is { } sk && Of("RATIONAL_B_SPLINE_SURFACE") is { } sw)
            return "IFCRATIONALBSPLINESURFACEWITHKNOTS(" + string.Join(',', s.Concat(sk).Concat(sw).Select(Value)) + ")";
        if (Of("B_SPLINE_CURVE") is { } c && Of("B_SPLINE_CURVE_WITH_KNOTS") is { } ck && Of("RATIONAL_B_SPLINE_CURVE") is { } cw)
            return "IFCRATIONALBSPLINECURVEWITHKNOTS(" + string.Join(',', c.Concat(ck).Concat(cw).Select(Value)) + ")";
        return null;
    }

    /// <summary>A value as STEP writes it again.</summary>
    static string Value(object? v) => v switch
    {
        null => Null,
        string s => S(s),
        double d => R(d),
        long l => l.ToString(CultureInfo.InvariantCulture),
        StepRef r => "#" + r.Id.ToString(CultureInfo.InvariantCulture),
        StepEnum en => E(en.Value),
        StepTyped t => $"{t.Type}({Value(t.Value)})",
        List<object?> l => L(l.Select(Value)),
        _ => Null,
    };

    /// <summary>
    /// Metres per length unit of the file: its LENGTH_UNIT (an SI unit by its prefix, a conversion-based inch or foot by its name), as the
    /// geometric context assigns it; 1 mm when the file says none (as most CAD files are).
    /// </summary>
    public static double LengthUnit(StepFile f)
    {
        foreach (var id in f.Entities.Keys)
        {
            if (f.Entities[id].Type != StepFile.Complex || f.Partials(id) is not { } parts || !parts.Any(p => p.Type == "LENGTH_UNIT")) continue;
            if (parts.FirstOrDefault(p => p.Type == "SI_UNIT").Args is { } si)
                return (si.ElementAtOrDefault(0) as StepEnum)?.Value switch
                {
                    "MILLI" => 0.001, "CENTI" => 0.01, "DECI" => 0.1, "KILO" => 1000, "MICRO" => 1e-6, _ => 1,
                };
            if (parts.FirstOrDefault(p => p.Type == "CONVERSION_BASED_UNIT").Args is { } cb)
                return (cb.ElementAtOrDefault(0) as string)?.ToUpperInvariant() switch { "INCH" => 0.0254, "FOOT" => 0.3048, _ => 0.001 };
        }
        return 0.001;
    }
}
