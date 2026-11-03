using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Steel;

/// <summary>A plane an end is cut on: the member keeps what lies behind it, against <see cref="Normal"/> (a unit vector pointing out of the end).</summary>
public readonly record struct CutPlane(Vec3 Point, Vec3 Normal)
{
    public double Side(Vec3 p) => Vec3.Dot(Normal, p - Point);
}

/// <summary>
/// A steel member's body from its axis and its two ends (decision 268): the section swept along the axis between the two
/// cut planes, each lateral edge of the section running from where it meets the start's plane to where it meets the end's;
/// built directly (side faces and two capping faces in the planes), so no boolean is needed but for the copes, which
/// are boxes taken off at the end.
/// </summary>
public static class SteelBody
{
    /// <summary>The steepest a cut may be on the axis: a plane closer to it than this (degrees) is refused.</summary>
    const double MinAngleToAxis = 10;

    /// <summary>A body and what an end could not be (a member to mitre with that is gone, a cut parallel to the axis).</summary>
    public sealed record Built(Solid? Solid, CutPlane Start, CutPlane End, double S0, double S1, IReadOnlyList<(SteelEnd End, bool AtEnd, double Station)> Copes, IReadOnlyList<string> Problems)
    {
        /// <summary>Where the axis meets each end's plane: the stations of the member's length.</summary>
        public double Length => S1 - S0;
    }

    /// <summary>
    /// The plane an end is cut on. <paramref name="atEnd"/>: the end's end (else its start). A square or plane cut stands at
    /// the axis's point moved out by the extension; a mitre on the plane halving the angle between the two axes, at the
    /// point where they come closest; a fit on the face of the other member's section box it comes to; a mitre and a fit
    /// moved out by the extension along their normal. A member to mitre or fit to that is gone leaves the end square.
    /// </summary>
    public static CutPlane PlaneOf(IModelView m, MemberAxis ax, SteelEnd end, bool atEnd, List<string> problems)
    {
        var u = ax.U;
        var outward = atEnd ? u : -u;
        var point = atEnd ? ax.End : ax.Start;
        var (v, w) = ax.Frame;
        if (end.Cut is "mitre" or "fit")
        {
            var other = end.Ref != null && m.Get(end.Ref) is { } o ? MemberAxes.Of(m, o) : null;
            if (other == null) problems.Add(end.Cut == "mitre" ? "The member it was mitred with is gone: the end is square." : "The member it was fitted to is gone: the end is square.");
            else if ((end.Cut == "mitre" ? Mitre(ax, other, atEnd) : Fit(ax, other, atEnd)) is { } plane)
                return plane with { Point = plane.Point + plane.Normal * end.Extension };
            else problems.Add(end.Cut == "mitre" ? "The members it is mitred with run in line: the end is square." : "The member it is fitted to runs along it: the end is square.");
            return new CutPlane(point + outward * end.Extension, outward);
        }
        var n = outward;
        if (end.Cut == "plane")
        {
            var (a, b) = (end.PlanAngle * Math.PI / 180, end.ElevAngle * Math.PI / 180);
            n = (outward * Math.Cos(a) + v * Math.Sin(a)) * Math.Cos(b) + w * Math.Sin(b);
        }
        return new CutPlane(point + outward * end.Extension, n.Normalized());
    }

    /// <summary>Where two lines come closest: the stations along each (from their starts, in their own lengths' units).</summary>
    static (double Ta, double Tb)? Closest(Vec3 a0, Vec3 da, Vec3 b0, Vec3 db)
    {
        var r = a0 - b0;
        double aa = Vec3.Dot(da, da), bb = Vec3.Dot(db, db), ab = Vec3.Dot(da, db), ar = Vec3.Dot(da, r), br = Vec3.Dot(db, r);
        var den = aa * bb - ab * ab;
        if (Math.Abs(den) < 1e-12 * aa * bb) return null;
        return ((ab * br - bb * ar) / den, (aa * br - ab * ar) / den);
    }

    /// <summary>The mitre: through the point where the axes come closest, its normal the other's way less this one's, so each keeps its own side.</summary>
    static CutPlane? Mitre(MemberAxis a, MemberAxis b, bool atEnd)
    {
        if (Closest(a.Start, a.End - a.Start, b.Start, b.End - b.Start) is not var (ta, tb)) return null;
        var p = (a.Start + (a.End - a.Start) * ta + b.Start + (b.End - b.Start) * tb) * 0.5;
        // Each member runs from the meeting point towards its far end.
        var da = (atEnd ? -a.U : a.U);
        var db = tb > 0.5 ? -b.U : b.U;
        var n = db - da;
        if (n.Length < 1e-6) return null;
        return new CutPlane(p, n.Normalized());
    }

    /// <summary>
    /// The fit: the face of the other's section box (its sides and its top and bottom, along its axis) that this member
    /// comes to — the one facing back along it the most — the end cut on it.
    /// </summary>
    static CutPlane? Fit(MemberAxis a, MemberAxis b, bool atEnd)
    {
        var outward = atEnd ? a.U : -a.U;
        var (v, w) = b.Frame;
        var (outline, _) = b.Profile;
        double x0 = outline.Min(p => p.X), x1 = outline.Max(p => p.X), y0 = outline.Min(p => p.Y), y1 = outline.Max(p => p.Y);
        (Vec3 N, Vec2 At)[] faces = [(v, new(x1, 0)), (-v, new(x0, 0)), (w, new(0, y1)), (-w, new(0, y0))];
        var (n, at) = faces.MaxBy(f => -Vec3.Dot(f.N, outward));
        if (-Vec3.Dot(n, outward) < Math.Sin(MinAngleToAxis * Math.PI / 180)) return null;
        return new CutPlane(b.At(at, 0), -n);
    }

    /// <summary>The body of a member with its ends, its copes taken off where the booleans are.</summary>
    public static Built Build(IModelView m, MemberAxis ax, SteelEnd start, SteelEnd end, ISolidBooleans? booleans, string tag = "member")
    {
        var problems = new List<string>();
        var p0 = PlaneOf(m, ax, start, false, problems);
        var p1 = PlaneOf(m, ax, end, true, problems);
        var u = ax.U;
        var min = Math.Sin(MinAngleToAxis * Math.PI / 180);
        if (Vec3.Dot(p0.Normal, -u) < min || Vec3.Dot(p1.Normal, u) < min)
            return new Built(null, p0, p1, 0, ax.Length, [], [.. problems, "An end's cut lies along the axis."]);
        // Where a line along the axis from (x, y) of the section meets a plane.
        double Station(CutPlane c, Vec2 p) => Vec3.Dot(c.Normal, c.Point - ax.At(p, 0)) / Vec3.Dot(c.Normal, u);
        double AxisStation(CutPlane c) => Vec3.Dot(c.Normal, c.Point - ax.Start) / Vec3.Dot(c.Normal, u);
        var (s0, s1) = (AxisStation(p0), AxisStation(p1));
        var (outline, holes) = ax.Profile;
        var rings = new List<IReadOnlyList<Vec2>> { outline };
        rings.AddRange(holes);
        if (rings.SelectMany(r => r).Any(p => Station(p1, p) - Station(p0, p) < 1e-4))
            return new Built(null, p0, p1, s0, s1, [], [.. problems, "The ends' cuts cross inside the member: it is too short for them."]);

        var verts = new List<Vec3>();
        var lo = new List<int[]>();
        var hi = new List<int[]>();
        foreach (var r in rings)
        {
            lo.Add(r.Select(p => { verts.Add(ax.At(p, Station(p0, p))); return verts.Count - 1; }).ToArray());
            hi.Add(r.Select(p => { verts.Add(ax.At(p, Station(p1, p))); return verts.Count - 1; }).ToArray());
        }
        var faces = new List<FaceInput>
        {
            new(lo[0].Reverse().ToArray(), lo.Skip(1).Select(l => l.Reverse().ToArray()).ToArray(), p0.Normal, "start"),
            new(hi[0], hi.Skip(1).ToArray(), p1.Normal, "end"),
        };
        for (var k = 0; k < rings.Count; k++)
        {
            var ring = rings[k];
            for (var i = 0; i < ring.Count; i++)
            {
                var j = (i + 1) % ring.Count;
                var outward = Vec3.Cross(ax.At(ring[j], 0) - ax.At(ring[i], 0), u);
                faces.Add(new FaceInput([lo[k][i], lo[k][j], hi[k][j], hi[k][i]], null, outward, k == 0 ? "side" : "reveal"));
            }
        }
        Solid? solid = SolidBuilder.Make(verts, faces, tag);
        var copes = new List<(SteelEnd, bool, double)>();
        if (start.Coped) copes.Add((start, false, s0));
        if (end.Coped) copes.Add((end, true, s1));
        if (copes.Count > 0)
        {
            if (booleans == null) problems.Add("Copes need the boolean library (manifold), missing here.");
            else
            {
                var boxes = copes.SelectMany(c => CopeBoxes(ax, c.Item1, c.Item2, c.Item3)).Select(b => Box(ax, b)).OfType<Solid>().ToList();
                try { solid = booleans.Difference(solid, boxes, tag) ?? solid; }
                catch (InvalidOperationException ex) { problems.Add($"The copes could not be cut: {ex.Message}"); }
            }
        }
        return new Built(solid, p0, p1, s0, s1, copes, problems);
    }

    /// <summary>A box in the member's frame: x and y across the section, from station <c>A</c> to <c>B</c>.</summary>
    public readonly record struct FrameBox(double X0, double X1, double Y0, double Y1, double A, double B);

    /// <summary>
    /// The boxes a cope takes off: across the whole section and a margin, from the profile's top (or bottom) down (or up)
    /// the cope's depth, from the cope's length back from the end's point to past the farthest the end reaches.
    /// </summary>
    public static IEnumerable<FrameBox> CopeBoxes(MemberAxis ax, SteelEnd e, bool atEnd, double station)
    {
        var (outline, _) = ax.Profile;
        double x0 = outline.Min(p => p.X), x1 = outline.Max(p => p.X), y0 = outline.Min(p => p.Y), y1 = outline.Max(p => p.Y);
        var margin = 0.01 + Math.Max(x1 - x0, y1 - y0);
        var (a, b) = atEnd ? (station - e.CopeLength, station + margin * 10) : (station - margin * 10, station + e.CopeLength);
        if (e.CopeTop) yield return new FrameBox(x0 - margin, x1 + margin, y1 - e.CopeDepth, y1 + margin, a, b);
        if (e.CopeBottom) yield return new FrameBox(x0 - margin, x1 + margin, y0 - margin, y0 + e.CopeDepth, a, b);
    }

    /// <summary>A frame box as a solid in the world.</summary>
    public static Solid? Box(MemberAxis ax, FrameBox b) =>
        Prism.Extrude([ax.At(new(b.X0, b.Y0), b.A), ax.At(new(b.X1, b.Y0), b.A), ax.At(new(b.X1, b.Y1), b.A), ax.At(new(b.X0, b.Y1), b.A)], null, ax.U * (b.B - b.A), "cope");

    /// <summary>A frame box as IFC sweeps it: its rectangle across the axis, swept along it.</summary>
    public static IfcSweep Sweep(MemberAxis ax, FrameBox b) =>
        new([new(b.X0, b.Y0), new(b.X1, b.Y0), new(b.X1, b.Y1), new(b.X0, b.Y1)], [], ax.At(default, b.A), ax.Frame.V, ax.U, b.B - b.A);

    /// <summary>
    /// The body as IFC's constructive solids: the section swept from the nearest point of the start's plane to the farthest
    /// of the end's, each end's plane clipping it unless it is square on the sweep's own end; the copes as openings.
    /// </summary>
    public static IfcSweptBody? Swept(IModelView m, MemberAxis ax, SteelEnd start, SteelEnd end)
    {
        var built = Build(m, ax, start, end, null);
        if (built.Problems.Any(p => p.StartsWith("An end's cut") || p.StartsWith("The ends' cuts"))) return null;
        var u = ax.U;
        var (outline, holes) = ax.Profile;
        var pts = outline.Concat(holes.SelectMany(h => h)).ToList();
        double Station(CutPlane c, Vec2 p) => Vec3.Dot(c.Normal, c.Point - ax.At(p, 0)) / Vec3.Dot(c.Normal, u);
        var a = pts.Min(p => Station(built.Start, p));
        var b = pts.Max(p => Station(built.End, p));
        var clips = new List<IfcClip>();
        if (pts.Max(p => Station(built.Start, p)) - a > 1e-9) clips.Add(new IfcClip(built.Start.Point, built.Start.Normal));
        if (b - pts.Min(p => Station(built.End, p)) > 1e-9) clips.Add(new IfcClip(built.End.Point, built.End.Normal));
        var body = new IfcSweep(outline, holes, ax.At(default, a), ax.Frame.V, u, b - a);
        var openings = built.Copes.SelectMany(c => CopeBoxes(ax, c.End, c.AtEnd, c.Station)).Select(x => Sweep(ax, x)).ToList();
        return new IfcSweptBody(body, clips, openings);
    }
}
