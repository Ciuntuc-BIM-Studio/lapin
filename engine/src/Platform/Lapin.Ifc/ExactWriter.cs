using Lapin.Geometry;
using static Lapin.Ifc.Step;

namespace Lapin.Ifc;

/// <summary>
/// An exact body (<see cref="ExactBody"/>) written as the B-rep of ISO 10303-42 (docs/plan-studio-freeform.md §9, F2c): a closed shell of
/// advanced faces on planes, cylindrical, conical, spherical and toroidal surfaces and B-spline surfaces (rational ones too), bounded by
/// edge loops of oriented edges on lines, circles and B-spline curves, their vertices points. IFC4 has the same entities under its own
/// names (IfcAdvancedBrep, IfcAdvancedFace, IfcBSplineSurfaceWithKnots…); STEP AP214 gives every representation item a name first and
/// writes a rational B-spline as a complex instance of its partial records, in alphabetical order. Written after the public schemas
/// (IFC4 ADD2 TC1, ISO 10303-42 and -214), no code of anyone else's. Shared geometry (points, directions, edges) is written once.
/// </summary>
public sealed class ExactWriter(Step file, bool ifc, double scale = 1)
{
    readonly Dictionary<(long, long, long), string> _points = [];
    readonly Dictionary<(long, long, long), string> _dirs = [];

    string Add(string ifcName, string stepName, params string[] args) => ifc ? file.Add(ifcName, args) : file.Add(stepName, [S(""), .. args]);

    static (long, long, long) Key(Vec3 p, double q) => ((long)Math.Round(p.X * q), (long)Math.Round(p.Y * q), (long)Math.Round(p.Z * q));

    string Coords(Vec3 p, double s) => L(new[] { R(p.X * s), R(p.Y * s), R(p.Z * s) });

    public string Point(Vec3 p)
    {
        var k = Key(p, 1e9);
        if (_points.TryGetValue(k, out var known)) return known;
        return _points[k] = Add("IFCCARTESIANPOINT", "CARTESIAN_POINT", Coords(p, scale));
    }

    public string Direction(Vec3 d)
    {
        var u = d.Normalized();
        var k = Key(u, 1e12);
        if (_dirs.TryGetValue(k, out var known)) return known;
        return _dirs[k] = Add("IFCDIRECTION", "DIRECTION", L(new[] { R(u.X), R(u.Y), R(u.Z) }));
    }

    public string Placement(Frame3 f) => Add("IFCAXIS2PLACEMENT3D", "AXIS2_PLACEMENT_3D", Point(f.Origin), Direction(f.Z), Direction(f.X));

    string Len(double v) => R(v * scale);

    public string Surface(ExactSurface s) => s switch
    {
        PlaneSurface p => Add("IFCPLANE", "PLANE", Placement(p.Frame)),
        CylinderSurface c => Add("IFCCYLINDRICALSURFACE", "CYLINDRICAL_SURFACE", Placement(c.Frame), Len(c.Radius)),
        ConeSurface c => Add("IFCCONICALSURFACE", "CONICAL_SURFACE", Placement(c.Frame), Len(c.Radius), R(c.SemiAngle)),
        SphereSurface c => Add("IFCSPHERICALSURFACE", "SPHERICAL_SURFACE", Placement(c.Frame), Len(c.Radius)),
        TorusSurface t => Add("IFCTOROIDALSURFACE", "TOROIDAL_SURFACE", Placement(t.Frame), Len(t.Major), Len(t.Minor)),
        SplineSurface b => SplineSurface(b.Surface),
        _ => throw new ArgumentException($"No surface entity for {s.GetType().Name}."),
    };

    public string Curve(ExactCurve c) => c switch
    {
        LineCurve l => Add("IFCLINE", "LINE", Point(l.Origin), Add("IFCVECTOR", "VECTOR", Direction(l.Direction), Len(1))),
        CircleCurve k => Add("IFCCIRCLE", "CIRCLE", Placement(k.Frame), Len(k.Radius)),
        SplineCurve b => SplineCurve(b.Curve),
        _ => throw new ArgumentException($"No curve entity for {c.GetType().Name}."),
    };

    static string Knots(IReadOnlyList<double> U, out string mults)
    {
        var (values, m) = BSpline.Distinct(U);
        mults = L(m.Select(I));
        return L(values.Select(R));
    }

    public string SplineCurve(BSplineCurve c)
    {
        var pts = L(c.Points.Select(Point));
        var knots = Knots(c.Knots, out var mults);
        var deg = I(c.Degree);
        var (form, closed, self, spec) = (E("UNSPECIFIED"), False, False, E("UNSPECIFIED"));
        if (!c.Rational) return Add("IFCBSPLINECURVEWITHKNOTS", "B_SPLINE_CURVE_WITH_KNOTS", deg, pts, form, closed, self, mults, knots, spec);
        var w = L(c.Weights!.Select(R));
        if (ifc) return file.Add("IFCRATIONALBSPLINECURVEWITHKNOTS", deg, pts, form, closed, self, mults, knots, spec, w);
        return file.AddComplex(
            ("BOUNDED_CURVE", []), ("B_SPLINE_CURVE", [deg, pts, form, closed, self]), ("B_SPLINE_CURVE_WITH_KNOTS", [mults, knots, spec]),
            ("CURVE", []), ("GEOMETRIC_REPRESENTATION_ITEM", []), ("RATIONAL_B_SPLINE_CURVE", [w]), ("REPRESENTATION_ITEM", [S("")]));
    }

    public string SplineSurface(BSplineSurface s)
    {
        var rows = L(Enumerable.Range(0, s.CountU).Select(i => L(Enumerable.Range(0, s.CountV).Select(j => Point(s.Points[i, j])))));
        var uk = Knots(s.KnotsU, out var um);
        var vk = Knots(s.KnotsV, out var vm);
        var (pu, pv) = (I(s.DegreeU), I(s.DegreeV));
        var (form, spec) = (E("UNSPECIFIED"), E("UNSPECIFIED"));
        if (!s.Rational) return Add("IFCBSPLINESURFACEWITHKNOTS", "B_SPLINE_SURFACE_WITH_KNOTS", pu, pv, rows, form, False, False, False, um, vm, uk, vk, spec);
        var w = L(Enumerable.Range(0, s.CountU).Select(i => L(Enumerable.Range(0, s.CountV).Select(j => R(s.Weight(i, j))))));
        if (ifc) return file.Add("IFCRATIONALBSPLINESURFACEWITHKNOTS", pu, pv, rows, form, False, False, False, um, vm, uk, vk, spec, w);
        return file.AddComplex(
            ("BOUNDED_SURFACE", []), ("B_SPLINE_SURFACE", [pu, pv, rows, form, False, False, False]), ("B_SPLINE_SURFACE_WITH_KNOTS", [um, vm, uk, vk, spec]),
            ("GEOMETRIC_REPRESENTATION_ITEM", []), ("RATIONAL_B_SPLINE_SURFACE", [w]), ("REPRESENTATION_ITEM", [S("")]), ("SURFACE", []));
    }

    /// <summary>
    /// The body as an IfcAdvancedBrep (IFC) or a MANIFOLD_SOLID_BREP (STEP) of its closed shell; <paramref name="name"/> the solid's name in
    /// STEP. Its reference is what a shape representation lists.
    /// </summary>
    public string Brep(ExactBody b, string? name = null)
    {
        var vertices = b.Vertices.Select(v => Add("IFCVERTEXPOINT", "VERTEX_POINT", Point(v))).ToArray();
        var edges = b.Edges.Select(e => Add("IFCEDGECURVE", "EDGE_CURVE", vertices[e.Start], vertices[e.End], Curve(e.Curve), True)).ToArray();
        string Loop(ExactLoop l) => Add("IFCEDGELOOP", "EDGE_LOOP", L(l.Edges.Select(x => Add("IFCORIENTEDEDGE", "ORIENTED_EDGE", Derived, Derived, edges[x.Edge], x.Forward ? True : False))));
        var faces = b.Faces.Select(f =>
        {
            var bounds = f.Loops.Select((l, k) => k == 0 ? Add("IFCFACEOUTERBOUND", "FACE_OUTER_BOUND", Loop(l), True) : Add("IFCFACEBOUND", "FACE_BOUND", Loop(l), True));
            return Add("IFCADVANCEDFACE", "ADVANCED_FACE", L(bounds.ToList()), Surface(f.Surface), f.SameSense ? True : False);
        }).ToList();
        var shell = Add("IFCCLOSEDSHELL", "CLOSED_SHELL", L(faces));
        return ifc ? file.Add("IFCADVANCEDBREP", shell) : file.Add("MANIFOLD_SOLID_BREP", S(name ?? ""), shell);
    }
}
