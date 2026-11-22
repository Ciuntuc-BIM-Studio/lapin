namespace Lapin.Geometry;

/// <summary>
/// A right-handed frame: its origin, its axis <see cref="Z"/> and its reference direction <see cref="X"/> (both unit, at right angles);
/// Y = Z × X. As ISO 10303-42 and IFC place a surface or a conic (axis2_placement_3d, IfcAxis2Placement3D).
/// </summary>
public readonly record struct Frame3(Vec3 Origin, Vec3 Z, Vec3 X)
{
    public Vec3 Y => Vec3.Cross(Z, X);

    /// <summary>A frame on an axis, its X the given direction made square to it (or any square one when it runs along the axis).</summary>
    public static Frame3 Of(Vec3 origin, Vec3 z, Vec3? xHint = null)
    {
        var zz = z.Normalized();
        var x = xHint ?? default;
        x -= zz * Vec3.Dot(x, zz);
        if (x.Length < 1e-9) x = VecMath.PlaneBasis(zz).U;
        return new Frame3(origin, zz, x.Normalized());
    }

    public Vec3 World(double x, double y, double z) => Origin + X * x + Y * y + Z * z;

    /// <summary>The frame moved by a map of points and its linear part; <paramref name="flip"/> turns its axis over (a mirror keeps a circle's way round so).</summary>
    public Frame3 Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir, bool flip = false)
    {
        var z = dir(Z).Normalized();
        return Of(point(Origin), flip ? -z : z, dir(X));
    }
}

// ------------------------------------------------------------------ curves

/// <summary>The exact curve an edge lies on (ISO 10303-42's line, circle and B-spline curve, as IFC has them too).</summary>
public abstract record ExactCurve
{
    /// <summary>Points from <paramref name="a"/> to <paramref name="b"/> along the curve, by the chord rule; both ends exactly the vertices.</summary>
    public abstract List<Vec3> Between(Vec3 a, Vec3 b);
    public abstract ExactCurve Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir, bool mirror);
}

/// <summary>A straight line through <see cref="Origin"/> along the unit <see cref="Direction"/>.</summary>
public sealed record LineCurve(Vec3 Origin, Vec3 Direction) : ExactCurve
{
    public override List<Vec3> Between(Vec3 a, Vec3 b) => [a, b];
    public override ExactCurve Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir, bool mirror) => new LineCurve(point(Origin), dir(Direction).Normalized());
}

/// <summary>A circle about its frame's origin in its X–Y plane; an edge on it runs anticlockwise about Z from its start to its end.</summary>
public sealed record CircleCurve(Frame3 Frame, double Radius) : ExactCurve
{
    public double Angle(Vec3 p) { var d = p - Frame.Origin; return Math.Atan2(Vec3.Dot(d, Frame.Y), Vec3.Dot(d, Frame.X)); }
    public Vec3 At(double t) => Frame.Origin + Frame.X * (Radius * Math.Cos(t)) + Frame.Y * (Radius * Math.Sin(t));

    /// <summary>The turn from a to b anticlockwise (a whole turn when they meet).</summary>
    public double Sweep(Vec3 a, Vec3 b)
    {
        var s = Angle(b) - Angle(a);
        while (s <= 1e-12) s += 2 * Math.PI;
        while (s > 2 * Math.PI + 1e-12) s -= 2 * Math.PI;
        return s;
    }

    public override List<Vec3> Between(Vec3 a, Vec3 b)
    {
        var t0 = Angle(a);
        var sweep = Sweep(a, b);
        var n = Chords.Count(Radius, sweep, 2);
        var o = new List<Vec3>(n + 1);
        for (var k = 0; k <= n; k++) o.Add(At(t0 + sweep * k / n));
        o[0] = a;
        o[^1] = b;
        return o;
    }

    public override ExactCurve Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir, bool mirror) => new CircleCurve(Frame.Moved(point, dir, mirror), Radius);
}

/// <summary>A B-spline or NURBS curve; an edge on it runs from its start to its end (the curve spans exactly its edge).</summary>
public sealed record SplineCurve(BSplineCurve Curve) : ExactCurve
{
    public override List<Vec3> Between(Vec3 a, Vec3 b)
    {
        var o = Curve.Polyline();
        if ((o[0] - a).Length > (o[^1] - a).Length) o.Reverse();
        o[0] = a;
        o[^1] = b;
        return o;
    }

    public override ExactCurve Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir, bool mirror) => new SplineCurve(Curve.Transformed(point));
}

// ------------------------------------------------------------------ surfaces

/// <summary>
/// The exact surface a face lies on: ISO 10303-42's elementary surfaces (plane, cylindrical, conical, spherical, toroidal) and B-spline
/// surfaces, as IFC4 has them too. Each has its own (natural) normal; a face says whether it agrees (its same sense).
/// </summary>
public abstract record ExactSurface
{
    /// <summary>The surface's own normal at the point of it nearest <paramref name="p"/>.</summary>
    public abstract Vec3 NormalAt(Vec3 p);
    /// <summary>How far <paramref name="p"/> is from the surface (untrimmed).</summary>
    public abstract double Distance(Vec3 p);
    public abstract ExactSurface Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir, bool mirror);

    protected static Vec3 Radial(Frame3 f, Vec3 p) { var d = p - f.Origin; return d - f.Z * Vec3.Dot(d, f.Z); }
    protected static Vec3 RadialDir(Frame3 f, Vec3 p) { var r = Radial(f, p); return r.Length > 1e-12 ? r.Normalized() : f.X; }
}

/// <summary>A plane through the frame's origin, its normal the frame's Z.</summary>
public sealed record PlaneSurface(Frame3 Frame) : ExactSurface
{
    public override Vec3 NormalAt(Vec3 p) => Frame.Z;
    public override double Distance(Vec3 p) => Math.Abs(Vec3.Dot(p - Frame.Origin, Frame.Z));
    public override ExactSurface Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir, bool mirror) => new PlaneSurface(Frame.Moved(point, dir));
}

/// <summary>A cylinder of <see cref="Radius"/> about the frame's Z; its normal points away from the axis.</summary>
public sealed record CylinderSurface(Frame3 Frame, double Radius) : ExactSurface
{
    public override Vec3 NormalAt(Vec3 p) => RadialDir(Frame, p);
    public override double Distance(Vec3 p) => Math.Abs(Radial(Frame, p).Length - Radius);
    public override ExactSurface Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir, bool mirror) => new CylinderSurface(Frame.Moved(point, dir), Radius);
}

/// <summary>
/// A cone about the frame's Z: <see cref="Radius"/> at the origin, growing by tan(<see cref="SemiAngle"/>) a unit along Z
/// (ISO 10303-42: σ(u, v) = C + (R + v tan α)(cos u x + sin u y) + v z); its normal points away from the axis.
/// </summary>
public sealed record ConeSurface(Frame3 Frame, double Radius, double SemiAngle) : ExactSurface
{
    public override Vec3 NormalAt(Vec3 p) => (RadialDir(Frame, p) * Math.Cos(SemiAngle) - Frame.Z * Math.Sin(SemiAngle)).Normalized();
    public override double Distance(Vec3 p)
    {
        var h = Vec3.Dot(p - Frame.Origin, Frame.Z);
        var rho = Radial(Frame, p).Length;
        return Math.Abs((rho - Radius - h * Math.Tan(SemiAngle)) * Math.Cos(SemiAngle));
    }
    public override ExactSurface Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir, bool mirror) => new ConeSurface(Frame.Moved(point, dir), Radius, SemiAngle);
}

/// <summary>A sphere of <see cref="Radius"/> about the frame's origin; its normal points out.</summary>
public sealed record SphereSurface(Frame3 Frame, double Radius) : ExactSurface
{
    public override Vec3 NormalAt(Vec3 p) { var d = p - Frame.Origin; return d.Length > 1e-12 ? d.Normalized() : Frame.Z; }
    public override double Distance(Vec3 p) => Math.Abs((p - Frame.Origin).Length - Radius);
    public override ExactSurface Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir, bool mirror) => new SphereSurface(Frame.Moved(point, dir), Radius);
}

/// <summary>A torus about the frame's Z: its tube's middle <see cref="Major"/> from the axis, the tube <see cref="Minor"/> round; its normal points out of the tube.</summary>
public sealed record TorusSurface(Frame3 Frame, double Major, double Minor) : ExactSurface
{
    Vec3 TubeCentre(Vec3 p) => Frame.Origin + RadialDir(Frame, p) * Major;
    public override Vec3 NormalAt(Vec3 p) { var d = p - TubeCentre(p); return d.Length > 1e-12 ? d.Normalized() : Frame.Z; }
    public override double Distance(Vec3 p) => Math.Abs((p - TubeCentre(p)).Length - Minor);
    public override ExactSurface Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir, bool mirror) => new TorusSurface(Frame.Moved(point, dir), Major, Minor);
}

/// <summary>A B-spline or NURBS surface; its normal Su × Sv.</summary>
public sealed record SplineSurface(BSplineSurface Surface) : ExactSurface
{
    (List<double> U, List<double> V, Vec3[,] P)? _grid;

    (List<double> U, List<double> V, Vec3[,] P) Grid()
    {
        if (_grid is { } g) return g;
        var (us, vs) = Surface.Grid();
        var pts = new Vec3[us.Count, vs.Count];
        for (var i = 0; i < us.Count; i++) for (var j = 0; j < vs.Count; j++) pts[i, j] = Surface.At(us[i], vs[j]);
        _grid = (us, vs, pts);
        return _grid.Value;
    }

    /// <summary>The parameters of the point of the surface nearest <paramref name="p"/>: the nearest of its grid, then Gauss–Newton steps.</summary>
    public (double U, double V) Nearest(Vec3 p)
    {
        var (us, vs, pts) = Grid();
        var (bi, bj, best) = (0, 0, double.PositiveInfinity);
        for (var i = 0; i < us.Count; i++)
            for (var j = 0; j < vs.Count; j++)
            {
                var d = Vec3.DistanceSquared(pts[i, j], p);
                if (d < best) (bi, bj, best) = (i, j, d);
            }
        var (u, v) = (us[bi], vs[bj]);
        var (u0, u1) = Surface.DomainU;
        var (v0, v1) = Surface.DomainV;
        for (var it = 0; it < 12; it++)
        {
            var d = Surface.Derivatives(u, v, 1);
            var r = d[0, 0] - p;
            var (su, sv) = (d[1, 0], d[0, 1]);
            double a = Vec3.Dot(su, su), b = Vec3.Dot(su, sv), c = Vec3.Dot(sv, sv);
            double g1 = Vec3.Dot(r, su), g2 = Vec3.Dot(r, sv);
            var det = a * c - b * b;
            if (Math.Abs(det) < 1e-30) break;
            var du = -(c * g1 - b * g2) / det;
            var dv = -(a * g2 - b * g1) / det;
            u = Math.Clamp(u + du, u0, u1);
            v = Math.Clamp(v + dv, v0, v1);
            if (Math.Abs(du) < 1e-12 * (u1 - u0) && Math.Abs(dv) < 1e-12 * (v1 - v0)) break;
        }
        return (u, v);
    }

    public override Vec3 NormalAt(Vec3 p) { var (u, v) = Nearest(p); return Surface.Normal(u, v); }
    public override double Distance(Vec3 p) { var (u, v) = Nearest(p); return (Surface.At(u, v) - p).Length; }
    public override ExactSurface Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir, bool mirror) => new SplineSurface(Surface.Transformed(point));
}

// ------------------------------------------------------------------ topology

/// <summary>An edge from vertex <see cref="Start"/> to vertex <see cref="End"/> along its curve (which runs the same way: its same sense).</summary>
public sealed record ExactEdge(int Start, int End, ExactCurve Curve);

/// <summary>A loop of edges, each taken forward (start to end) or backward.</summary>
public sealed record ExactLoop(IReadOnlyList<(int Edge, bool Forward)> Edges)
{
    public ExactLoop Reversed() => new([.. Edges.Reverse().Select(e => (e.Edge, !e.Forward))]);
}

/// <summary>
/// A face: its surface, whether its outward side is the surface's own normal (<see cref="SameSense"/>), its loops (the first the outer one,
/// anticlockwise seen from outside; holes the other way), and a point inside it (on the surface) that stands for it.
/// </summary>
public sealed record ExactFace(ExactSurface Surface, bool SameSense, IReadOnlyList<ExactLoop> Loops, Vec3 Inside);

/// <summary>
/// What a body is exactly (docs/plan-studio-freeform.md §9, F2): a closed shell of faces on exact surfaces bounded by edges on exact
/// curves, kept beside the tessellation the model uses (<see cref="Chords"/>), never instead of it. Derived on demand from what made the
/// body, checked against that body (<see cref="ExactCheck"/>), and written to IFC4 as IfcAdvancedBrep and to STEP as MANIFOLD_SOLID_BREP.
/// </summary>
public sealed record ExactBody(IReadOnlyList<Vec3> Vertices, IReadOnlyList<ExactEdge> Edges, IReadOnlyList<ExactFace> Faces)
{
    /// <summary>An edge's points from its start to its end, by the chord rule.</summary>
    public List<Vec3> EdgePoints(int e) { var x = Edges[e]; return x.Curve.Between(Vertices[x.Start], Vertices[x.End]); }

    /// <summary>A loop's points in its order (each vertex once).</summary>
    public List<Vec3> LoopPoints(ExactLoop loop)
    {
        var o = new List<Vec3>();
        foreach (var (e, fwd) in loop.Edges)
        {
            var pts = EdgePoints(e);
            if (!fwd) pts.Reverse();
            o.AddRange(pts.Take(pts.Count - 1));
        }
        return o;
    }

    /// <summary>
    /// The body moved by a map of points and its linear part (a placement); a mirror (a map turning space over) turns the circles' axes
    /// over so their edges keep their way round. Faces are oriented again against the body they stand for (<see cref="ExactCheck.Orient"/>).
    /// </summary>
    public ExactBody Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir)
    {
        var (x, y, z) = (dir(new Vec3(1, 0, 0)), dir(new Vec3(0, 1, 0)), dir(new Vec3(0, 0, 1)));
        var mirror = Vec3.Dot(Vec3.Cross(x, y), z) < 0;
        return new ExactBody([.. Vertices.Select(point)], [.. Edges.Select(e => e with { Curve = e.Curve.Moved(point, dir, mirror) })],
            [.. Faces.Select(f => f with { Surface = f.Surface.Moved(point, dir, mirror), Inside = point(f.Inside) })]);
    }

    /// <summary>How many faces lie on each kind of surface (for notes and tests): "plane", "cylinder", "cone", "sphere", "torus", "bspline".</summary>
    public Dictionary<string, int> SurfaceKinds() => Faces.GroupBy(f => f.Surface switch
    {
        PlaneSurface => "plane",
        CylinderSurface => "cylinder",
        ConeSurface => "cone",
        SphereSurface => "sphere",
        TorusSurface => "torus",
        _ => "bspline",
    }).ToDictionary(g => g.Key, g => g.Count());
}

/// <summary>
/// A body being made: vertices welded, edges found again by their ends and their middle (so two faces share one), faces added with
/// any orientation (<see cref="ExactCheck.Orient"/> sets it against the body).
/// </summary>
public sealed class ExactBuilder
{
    readonly List<Vec3> _v = [];
    readonly List<ExactEdge> _e = [];
    readonly List<Vec3> _mid = [];
    readonly List<ExactFace> _f = [];
    readonly Dictionary<(long, long, long), List<int>> _byPoint = [];
    readonly Dictionary<(int, int), List<int>> _byEnds = [];
    const double Weld = 1e-7;

    public int FaceCount => _f.Count;

    /// <summary>The vertex an edge starts at.</summary>
    public int StartOf(int edge) => _e[edge].Start;

    static (long, long, long) Cell(Vec3 p) => ((long)Math.Floor(p.X / Weld / 10), (long)Math.Floor(p.Y / Weld / 10), (long)Math.Floor(p.Z / Weld / 10));

    /// <summary>The vertex at <paramref name="p"/> (one already there within a ten-millionth of a metre, or a new one).</summary>
    public int Vertex(Vec3 p)
    {
        var (cx, cy, cz) = Cell(p);
        for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
                for (var dz = -1; dz <= 1; dz++)
                    if (_byPoint.TryGetValue((cx + dx, cy + dy, cz + dz), out var l))
                        foreach (var i in l) if ((_v[i] - p).Length <= Weld) return i;
        _v.Add(p);
        var key = (cx, cy, cz);
        if (!_byPoint.TryGetValue(key, out var list)) _byPoint[key] = list = [];
        list.Add(_v.Count - 1);
        return _v.Count - 1;
    }

    /// <summary>
    /// The edge from <paramref name="a"/> to <paramref name="b"/> on <paramref name="curve"/> (the same one when a face before asked for it,
    /// either way round), and whether it runs a to b.
    /// </summary>
    public (int Edge, bool Forward) Edge(Vec3 a, Vec3 b, ExactCurve curve)
    {
        var (ia, ib) = (Vertex(a), Vertex(b));
        var pts = curve.Between(_v[ia], _v[ib]);
        var mid = Middle(pts);
        var key = (Math.Min(ia, ib), Math.Max(ia, ib));
        if (_byEnds.TryGetValue(key, out var known))
            foreach (var k in known)
                if ((_mid[k] - mid).Length <= 1e-6) return (k, _e[k].Start == ia);
        _e.Add(new ExactEdge(ia, ib, curve));
        _mid.Add(mid);
        if (!_byEnds.TryGetValue(key, out var list)) _byEnds[key] = list = [];
        list.Add(_e.Count - 1);
        return (_e.Count - 1, true);
    }

    /// <summary>A straight edge.</summary>
    public (int Edge, bool Forward) Line(Vec3 a, Vec3 b) => Edge(a, b, new LineCurve(a, (b - a).Normalized()));

    /// <summary>An edge on a circle of the frame given, from a to b turning <paramref name="sweep"/> (its sign the way round about the frame's Z).</summary>
    public (int Edge, bool Forward) Arc(Frame3 frame, double radius, Vec3 a, Vec3 b, double sweep)
    {
        // The circle's axis so the edge runs anticlockwise about it: its same sense true.
        var f = sweep >= 0 ? frame : new Frame3(frame.Origin, -frame.Z, frame.X);
        return Edge(a, b, new CircleCurve(f, radius));
    }

    /// <summary>The point halfway along a polyline, by its length.</summary>
    static Vec3 Middle(List<Vec3> pts)
    {
        var total = 0.0;
        for (var i = 0; i + 1 < pts.Count; i++) total += (pts[i + 1] - pts[i]).Length;
        var at = total / 2;
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            var s = (pts[i + 1] - pts[i]).Length;
            if (at <= s) return pts[i] + (pts[i + 1] - pts[i]) * (s < 1e-15 ? 0 : at / s);
            at -= s;
        }
        return pts[^1];
    }

    /// <summary>A face on <paramref name="surface"/>, its loops (the outer first), <paramref name="inside"/> a point on it within its outer loop.</summary>
    public void Face(ExactSurface surface, IReadOnlyList<IReadOnlyList<(int Edge, bool Forward)>> loops, Vec3 inside) =>
        _f.Add(new ExactFace(surface, true, [.. loops.Select(l => new ExactLoop(l))], inside));

    /// <summary>A planar face through its loops, its inside point found well within its outer loop (not in a hole).</summary>
    public void PlaneFace(IReadOnlyList<IReadOnlyList<(int Edge, bool Forward)>> loops, Vec3? normalHint = null)
    {
        var body = new ExactBody(_v, _e, []);
        var rings = loops.Select(l => body.LoopPoints(new ExactLoop(l))).ToList();
        var n = VecMath.NewellNormal(rings[0]);
        if (normalHint is { } h && Vec3.Dot(n, h) < 0) n = -n;
        if (n.Length < 1e-15) n = normalHint ?? Vec3.UnitZ;
        var origin = rings[0][0];
        _f.Add(new ExactFace(new PlaneSurface(Frame3.Of(origin, n, rings[0].Count > 1 ? rings[0][1] - rings[0][0] : null)), true,
            [.. loops.Select(l => new ExactLoop(l))], InsidePoint(rings, n)));
    }

    /// <summary>A point well inside a planar region (outer ring, holes): the middle of its largest triangle.</summary>
    internal static Vec3 InsidePoint(List<List<Vec3>> rings, Vec3 normal)
    {
        var pts = rings.SelectMany(r => r).ToList();
        var at = 0;
        var idx = rings.Select(r => { var a = Enumerable.Range(at, r.Count).ToArray(); at += r.Count; return a; }).ToList();
        var tris = Triangulator.Face(pts, idx[0], [.. idx.Skip(1)], normal);
        if (tris.Count == 0) return pts[0];
        var best = tris.MaxBy(t => Vec3.Cross(pts[t.B] - pts[t.A], pts[t.C] - pts[t.A]).Length);
        return (pts[best.A] + pts[best.B] + pts[best.C]) * (1.0 / 3);
    }

    public ExactBody Body() => new([.. _v], [.. _e], [.. _f]);
}
