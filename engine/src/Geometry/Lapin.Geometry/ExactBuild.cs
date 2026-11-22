namespace Lapin.Geometry;

/// <summary>A piece of a profile in space as it was drawn: a straight segment or an arc of a circle.</summary>
public abstract record ExactSeg
{
    public abstract Vec3 Start { get; }
    public abstract Vec3 End { get; }
    /// <summary>The point halfway along it.</summary>
    public abstract Vec3 Mid { get; }
    public abstract ExactSeg Reversed();
    /// <summary>The piece as a B-spline curve from its start to its end (an arc exactly, A7.1).</summary>
    public abstract BSplineCurve Curve();
    /// <summary>Its length.</summary>
    public abstract double Length { get; }
    /// <summary>Its points by the chord rule, both ends included.</summary>
    public abstract List<Vec3> Points();
    /// <summary>The piece cut at the fraction <paramref name="t"/> of its length.</summary>
    public abstract (ExactSeg A, ExactSeg B) Split(double t);
    public abstract ExactSeg Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir);
}

public sealed record LineSeg(Vec3 A, Vec3 B) : ExactSeg
{
    public override Vec3 Start => A;
    public override Vec3 End => B;
    public override Vec3 Mid => (A + B) * 0.5;
    public override ExactSeg Reversed() => new LineSeg(B, A);
    public override BSplineCurve Curve() => BSplineCurve.Line(A, B);
    public override double Length => (B - A).Length;
    public override List<Vec3> Points() => [A, B];
    public override (ExactSeg A, ExactSeg B) Split(double t) { var m = A + (B - A) * t; return (new LineSeg(A, m), new LineSeg(m, B)); }
    public override ExactSeg Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir) => new LineSeg(point(A), point(B));
}

/// <summary>An arc about <see cref="Centre"/> in the plane of the unit, square <see cref="X"/> and <see cref="Y"/>: from angle <see cref="From"/> turning <see cref="Sweep"/> (towards Y when positive).</summary>
public sealed record ArcSeg(Vec3 Centre, Vec3 X, Vec3 Y, double Radius, double From, double Sweep) : ExactSeg
{
    public Vec3 At(double a) => Centre + X * (Radius * Math.Cos(a)) + Y * (Radius * Math.Sin(a));
    public override Vec3 Start => At(From);
    public override Vec3 End => At(From + Sweep);
    public override Vec3 Mid => At(From + Sweep / 2);
    public bool Whole => Math.Abs(Math.Abs(Sweep) - 2 * Math.PI) < 1e-9;
    public override ExactSeg Reversed() => new ArcSeg(Centre, X, Y, Radius, From + Sweep, -Sweep);
    public override BSplineCurve Curve() => BSplineCurve.Arc(Centre, X, Y, Radius, From, Sweep);
    public override double Length => Math.Abs(Sweep) * Radius;
    public override List<Vec3> Points()
    {
        var n = Chords.Count(Radius, Sweep, 1);
        return [.. Enumerable.Range(0, n + 1).Select(k => At(From + Sweep * k / n))];
    }
    public override (ExactSeg A, ExactSeg B) Split(double t) => (this with { Sweep = Sweep * t }, this with { From = From + Sweep * t, Sweep = Sweep * (1 - t) });
    public override ExactSeg Moved(Func<Vec3, Vec3> point, Func<Vec3, Vec3> dir)
    {
        var (x, y) = (dir(X), dir(Y));
        return new ArcSeg(point(Centre), x.Normalized(), y.Normalized(), Radius * x.Length, From, Sweep);
    }
    /// <summary>The frame of its circle (its axis X × Y).</summary>
    public Frame3 Frame => new(Centre, Vec3.Cross(X, Y).Normalized(), X);
}

/// <summary>
/// Exact bodies of what is made exactly (docs/plan-studio-freeform.md §9, F2b): a polyhedron's planes, a profile of lines and arcs extruded
/// (planes and cylinders) or turned about an axis (planes, cylinders, cones, spheres, tori), a skinned surface between two caps, a Catmull–Clark
/// limit surface's regular faces as bicubic patches. Faces come in any orientation; <see cref="ExactCheck.Fit"/> turns and checks them against the
/// body they stand for. Whole circles and turns are cut in halves (no face meets itself along a seam).
/// </summary>
public static class ExactBuild
{
    /// <summary>A polyhedron as it is: each face a plane, each edge a line (for bodies whose faces are planar by design: a box, a wedge, a prism of a polygon).</summary>
    public static ExactBody Planar(Solid s)
    {
        var b = new ExactBuilder();
        foreach (var f in s.Faces)
        {
            List<(int, bool)> Loop(int[] ring) => [.. ring.Select((v, k) => b.Line(s.Vertices[v], s.Vertices[ring[(k + 1) % ring.Length]]))];
            var loops = new List<IReadOnlyList<(int, bool)>> { Loop(f.Outer) };
            loops.AddRange(f.Holes.Where(h => h.Length >= 3).Select(Loop));
            b.PlaneFace(loops, f.Normal);
        }
        return b.Body();
    }

    /// <summary>A loop with its whole circles cut in two and arcs over half a turn halved (so no edge closes on itself and no face turns more than half round).</summary>
    public static List<ExactSeg> Halved(IEnumerable<ExactSeg> loop)
    {
        var o = new List<ExactSeg>();
        foreach (var s in loop)
        {
            if (s is ArcSeg a && Math.Abs(a.Sweep) > Math.PI + 1e-9) { var (x, y) = a.Split(0.5); o.Add(x); o.Add(y); }
            else if (s.Length > 1e-12) o.Add(s);
        }
        return o;
    }

    /// <summary>
    /// A profile (its outer loop first, then its holes, each closed, of lines and arcs in one plane) extruded along <paramref name="d"/>:
    /// the two caps planes, each line's side a plane, each arc's a cylinder (the extrusion must run along the profile's normal for that).
    /// </summary>
    public static ExactBody? Extrusion(IReadOnlyList<IReadOnlyList<ExactSeg>> loops, Vec3 d)
    {
        if (d.Length < 1e-12 || loops.Count == 0) return null;
        var dir = d.Normalized();
        var b = new ExactBuilder();
        var bottom = new List<IReadOnlyList<(int, bool)>>();
        var top = new List<IReadOnlyList<(int, bool)>>();
        foreach (var loop0 in loops)
        {
            var loop = Halved(loop0);
            if (loop.Count == 0) return null;
            var lb = new List<(int, bool)>();
            var lt = new List<(int, bool)>();
            foreach (var s in loop)
            {
                var (s0, s1) = (s.Start, s.End);
                var (t0, t1) = (s0 + d, s1 + d);
                (int, bool) eb, et;
                if (s is ArcSeg a)
                {
                    // An arc's side is a cylinder only when the extrusion runs along its axis.
                    if (Math.Abs(Math.Abs(Vec3.Dot(a.Frame.Z, dir)) - 1) > 1e-9) return null;
                    var f = a.Frame;
                    eb = b.Arc(f, a.Radius, s0, s1, a.Sweep);
                    et = b.Arc(f with { Origin = f.Origin + d }, a.Radius, t0, t1, a.Sweep);
                    var up1 = b.Line(s1, t1);
                    var up0 = b.Line(s0, t0);
                    b.Face(new CylinderSurface(Frame3.Of(a.Centre, dir, a.X), a.Radius), [[eb, up1, Flip(et), Flip(up0)]], a.Mid + d * 0.5);
                }
                else
                {
                    eb = b.Line(s0, s1);
                    et = b.Line(t0, t1);
                    var up1 = b.Line(s1, t1);
                    var up0 = b.Line(s0, t0);
                    b.PlaneFace([[eb, up1, Flip(et), Flip(up0)]]);
                }
                lb.Add(eb);
                lt.Add(et);
            }
            bottom.Add(lb);
            top.Add(lt);
        }
        b.PlaneFace(bottom, -dir);
        b.PlaneFace(top, dir);
        return b.Body();
    }

    static (int, bool) Flip((int E, bool F) e) => (e.E, !e.F);

    /// <summary>
    /// A profile (outer loop first, holes after; lines and arcs in a plane holding the axis, none crossing it) turned about the axis through
    /// <paramref name="axisAt"/> along <paramref name="axisDir"/> by <paramref name="angle"/> radians (a whole turn when 2π): each line makes a plane
    /// (square to the axis), a cylinder (along it) or a cone; each arc a sphere (centred on the axis) or a torus. A part turn has the profile as
    /// its two end caps. Null when a piece makes a surface this does not know (an arc whose circle crosses the axis).
    /// </summary>
    public static ExactBody? Revolution(IReadOnlyList<IReadOnlyList<ExactSeg>> loops, Vec3 axisAt, Vec3 axisDir, double angle)
    {
        if (axisDir.Length < 1e-12 || Math.Abs(angle) < 1e-9) return null;
        var z = axisDir.Normalized();
        var whole = Math.Abs(Math.Abs(angle) - 2 * Math.PI) < 1e-9;
        // The turn in sectors of at most half a turn.
        var sectors = whole ? 2 : Math.Abs(angle) > Math.PI + 1e-9 ? 2 : 1;
        var steps = Enumerable.Range(0, sectors + 1).Select(k => angle * k / sectors).ToArray();
        var b = new ExactBuilder();
        Vec3 Foot(Vec3 p) => axisAt + z * Vec3.Dot(p - axisAt, z);
        double Rho(Vec3 p) => (p - Foot(p)).Length;
        Vec3 Rot(Vec3 p, double t)
        {
            var v = p - axisAt;
            var (c, s) = (Math.Cos(t), Math.Sin(t));
            return axisAt + v * c + Vec3.Cross(z, v) * s + z * (Vec3.Dot(z, v) * (1 - c));
        }
        Vec3 RotD(Vec3 v, double t) => Rot(axisAt + v, t) - axisAt;
        const double OnAxis = 1e-9;
        // The edge a profile piece makes turned by t.
        (int, bool) SegEdge(ExactSeg s, double t)
        {
            var (a, e) = (Rot(s.Start, t), Rot(s.End, t));
            if (s is ArcSeg arc)
                return b.Arc(new Frame3(Rot(arc.Centre, t), RotD(arc.Frame.Z, t), RotD(arc.X, t)), arc.Radius, a, e, arc.Sweep);
            return b.Line(a, e);
        }
        // The circle a profile point runs on from t0 to t1, or none on the axis.
        (int, bool)? Ring(Vec3 p, double t0, double t1)
        {
            var r = Rho(p);
            if (r < OnAxis) return null;
            var foot = Foot(p);
            return b.Arc(new Frame3(foot, z, (p - foot).Normalized()), r, Rot(p, t0), Rot(p, t1), t1 - t0);
        }
        var caps = new List<List<(int, bool)>>[2] { [], [] };
        foreach (var loop0 in loops)
        {
            var loop = Halved(loop0);
            if (loop.Count == 0) return null;
            var cap0 = new List<(int, bool)>();
            var cap1 = new List<(int, bool)>();
            foreach (var s in loop)
            {
                var (p, q) = (s.Start, s.End);
                var onAxis = Rho(p) < OnAxis && Rho(q) < OnAxis && (s is LineSeg || Rho(s.Mid) < OnAxis);
                if (!whole) { cap0.Add(onAxis ? b.Line(p, q) : SegEdge(s, 0)); cap1.Add(onAxis ? b.Line(Rot(p, angle), Rot(q, angle)) : SegEdge(s, angle)); }
                if (onAxis) continue;
                ExactSurface surface;
                var xRef = (s.Mid - Foot(s.Mid)).Normalized();
                if (s is ArcSeg arc)
                {
                    if (Math.Abs(Vec3.Dot(arc.Frame.Z, z)) > 1e-6) return null; // its plane must hold the axis
                    var cr = Rho(arc.Centre);
                    if (cr < OnAxis) surface = new SphereSurface(Frame3.Of(arc.Centre, z, xRef), arc.Radius);
                    else if (arc.Radius < cr - 1e-9) surface = new TorusSurface(Frame3.Of(Foot(arc.Centre), z, xRef), cr, arc.Radius);
                    else return null;
                }
                else
                {
                    var (hp, hq) = (Vec3.Dot(p - axisAt, z), Vec3.Dot(q - axisAt, z));
                    var (rp, rq) = (Rho(p), Rho(q));
                    if (Math.Abs(hq - hp) < 1e-12) surface = new PlaneSurface(Frame3.Of(p, z, xRef));
                    else if (Math.Abs(rq - rp) < 1e-12) surface = new CylinderSurface(Frame3.Of(Foot(p), z, xRef), rp);
                    else
                    {
                        // The cone's axis the way its radius grows; placed at the end that is off the axis.
                        var grows = (rq - rp) / (hq - hp);
                        var at = rp >= rq ? p : q;
                        var cz = grows > 0 ? z : -z;
                        surface = new ConeSurface(Frame3.Of(Foot(at), cz, xRef), Rho(at), Math.Atan(Math.Abs(grows)));
                    }
                }
                for (var k = 0; k < sectors; k++)
                {
                    var (t0, t1) = (steps[k], steps[k + 1]);
                    var e0 = SegEdge(s, t0);
                    var e1 = SegEdge(s, t1);
                    var loopEdges = new List<(int, bool)> { e0 };
                    if (Ring(q, t0, t1) is { } rq1) loopEdges.Add(rq1);
                    loopEdges.Add(Flip(e1));
                    if (Ring(p, t0, t1) is { } rp1) loopEdges.Add(Flip(rp1));
                    b.Face(surface, [loopEdges], Rot(s.Mid, (t0 + t1) / 2));
                }
            }
            caps[0].Add(cap0);
            caps[1].Add(cap1);
        }
        if (!whole)
        {
            b.PlaneFace([.. caps[0]]);
            b.PlaneFace([.. caps[1]]);
        }
        return b.Body();
    }

    /// <summary>
    /// A skinned side surface (closed round its u, from section 0 at its v start to the last at its v end) between two planar caps: the side
    /// in two halves across u (so no face meets itself), each bounded by its sections' halves and its two seams.
    /// </summary>
    public static ExactBody? Skinned(BSplineSurface side)
    {
        var (u0, u1) = side.DomainU;
        var (v0, v1) = side.DomainV;
        if ((side.At(u0, v0) - side.At(u1, v0)).Length > 1e-9 || (side.At(u0, v1) - side.At(u1, v1)).Length > 1e-9) return null;
        var um = (u0 + u1) / 2;
        var (a, c) = side.SplitU(um);
        var b = new ExactBuilder();
        var bottoms = new List<(int, bool)>();
        var tops = new List<(int, bool)>();
        foreach (var half in new[] { a, c })
        {
            var (h0, h1) = half.DomainU;
            var bottom = half.IsoV(v0);
            var top = half.IsoV(v1);
            var seam0 = half.IsoU(h0);
            var seam1 = half.IsoU(h1);
            var eb = b.Edge(half.At(h0, v0), half.At(h1, v0), new SplineCurve(bottom));
            var e1 = b.Edge(half.At(h1, v0), half.At(h1, v1), new SplineCurve(seam1));
            var et = b.Edge(half.At(h0, v1), half.At(h1, v1), new SplineCurve(top));
            var e0 = b.Edge(half.At(h0, v0), half.At(h0, v1), new SplineCurve(seam0));
            b.Face(new SplineSurface(half), [[eb, e1, Flip(et), Flip(e0)]], half.At((h0 + h1) / 2, (v0 + v1) / 2));
            bottoms.Add(eb);
            tops.Add(et);
        }
        b.PlaneFace([bottoms]);
        b.PlaneFace([tops]);
        return b.Body();
    }
}
