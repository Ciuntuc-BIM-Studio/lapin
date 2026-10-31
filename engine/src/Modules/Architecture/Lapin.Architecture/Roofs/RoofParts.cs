// Ported from BubbleBIM Pro (the user's own engine, engine/src/Modules/Building/BubbleBim.Building/Roofs), adapted to Lapin's model.
using Lapin.Geometry;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// What a roof carries and drains, all derived from its solved shape — never
/// nodes in the graph:
/// <list type="bullet">
/// <item>the timber structure: wall plates on the eave walls, rafters at a spacing on every plane, purlins under them, a ridge beam under each ridge, hip and valley rafters (port of Standalone's roof system structure);</item>
/// <item>gutters along the eaves, downpipes from them down the walls to the ground, one per 10 m of gutter;</item>
/// <item>snow guards: rows along the eaves, on the covering.</item>
/// </list>
/// The planes are the rafters' underside; the covering lies on the rafters and battens.
/// </summary>
public static class RoofParts
{
    /// <summary>A member: a centre line with a width × depth section (depth across the slope, or vertical for a level member).</summary>
    public sealed record Member(string Role, Vec3 A, Vec3 B, double Width, double Depth)
    {
        public double Length => (B - A).Length;
        public double Volume => Length * Width * Depth;
    }

    sealed record Frame(RoofPlane Plane, List<Vec2> Plan, Vec2 D, Vec2 N, Func<Vec2, double> Z, Vec3 Normal, double Tan);

    static List<Frame> Frames(RoofShape s) => s.Planes.Select(p =>
    {
        int n = s.Outline.Count;
        var a = s.Outline[p.Edge]; var b = s.Outline[(p.Edge + 1) % n];
        var d = (b - a).Normalized();
        var nrm = VecMath.NewellNormal(p.Points).Normalized();
        if (nrm.Z < 0) nrm = -nrm;
        return new Frame(p, p.Points.Select(q => q.Xy).ToList(), d, d.Perp, RoofSolver.PlaneZ(p.Points), nrm, Math.Tan(p.Pitch * Math.PI / 180));
    }).ToList();

    /// <summary>The intervals of the line p + t·u inside a polygon, as (t0, t1).</summary>
    public static List<(double T0, double T1)> Crossings(IReadOnlyList<Vec2> poly, Vec2 p, Vec2 u)
    {
        var ts = new List<double>();
        for (int i = 0; i < poly.Count; i++)
        {
            var (a, b) = (poly[i], poly[(i + 1) % poly.Count]);
            var e = b - a;
            var den = Vec2.Cross(u, e);
            if (Math.Abs(den) < 1e-12) continue;
            var w = a - p;
            var t = Vec2.Cross(w, e) / den;
            var s = Vec2.Cross(w, u) / den;
            if (s >= 0 && s < 1) ts.Add(t);   // half-open, so a vertex counts once
        }
        ts.Sort();
        var o = new List<(double, double)>();
        for (int i = 0; i + 1 < ts.Count; i += 2) if (ts[i + 1] - ts[i] > 1e-6) o.Add((ts[i], ts[i + 1]));
        return o;
    }

    /// <summary>The parts of intervals outside others (a rafter's run less the holes it crosses).</summary>
    static List<(double T0, double T1)> Minus(List<(double T0, double T1)> runs, List<(double T0, double T1)> cuts)
    {
        foreach (var (c0, c1) in cuts)
            runs = runs.SelectMany(r => r.T1 <= c0 || r.T0 >= c1 ? [r]
                : new[] { (r.T0, Math.Min(r.T1, c0)), (Math.Max(r.T0, c1), r.T1) }.Where(x => x.Item2 - x.Item1 > 1e-6)).ToList();
        return runs;
    }

    static List<List<Vec2>> HolesOn(Frame f, IReadOnlyList<RoofOpenings.Placed>? openings) =>
        openings?.Where(o => ReferenceEquals(o.Host.Plane, f.Plane))
            .Select(o => o.Hole.Select(q => q.Xy).ToList()).ToList() ?? [];

    /// <summary>The timber structure (empty when the roof has none); rafters stop at the openings, trimmers frame them.</summary>
    public static List<Member> Framing(Roof.Resolved r, RoofShape s, IReadOnlyList<RoofOpenings.Placed>? openings = null)
    {
        var o = new List<Member>();
        if (!r.Framed) return o;
        var (rw, rd) = r.Rafter;
        var spacing = Math.Max(0.3, r.Roof.NumOrNull("rafterSpacing") ?? 0.8);
        var purlins = Math.Clamp((int)(r.Roof.NumOrNull("purlins") ?? 1), 0, 3);
        var frames = Frames(s);
        var minNz = frames.Count == 0 ? 1 : frames.Min(f => f.Normal.Z);

        foreach (var f in frames)
        {
            // Rafters: across the plane along its eave, every `spacing`, up the slope; their underside on the plane.
            var a = s.Outline[f.Plane.Edge];
            var along = f.Plan.Select(p => Vec2.Dot(p - a, f.D)).ToList();
            double lo = along.Min() + rw / 2, hi = along.Max() - rw / 2;
            int count = Math.Max(1, (int)Math.Round((hi - lo) / spacing));
            for (int j = 0; j <= count; j++)
            {
                var t = hi > lo ? lo + (hi - lo) * j / count : (lo + hi) / 2;
                var p = a + f.D * t;
                var cuts = HolesOn(f, openings).SelectMany(h => Crossings(h, p, f.N)).ToList();
                foreach (var (t0, t1) in Minus(Crossings(f.Plan, p, f.N), cuts))
                {
                    Vec3 On(double k) { var q = p + f.N * k; return q.At(f.Z(q)) + f.Normal * (rd / 2); }
                    o.Add(new Member("rafter", On(t0), On(t1), rw, rd));
                }
            }
            // Trimmers: across each opening, under and over it, carrying the rafters it cut — a spacing past each side.
            foreach (var h in HolesOn(f, openings))
            {
                double u0 = h.Min(q => Vec2.Dot(q - a, f.D)) - spacing, u1 = h.Max(q => Vec2.Dot(q - a, f.D)) + spacing;
                foreach (var sUp in new[] { h.Min(q => Vec2.Dot(q - a, f.N)) - rw / 2, h.Max(q => Vec2.Dot(q - a, f.N)) + rw / 2 })
                {
                    var p = a + f.N * sUp;
                    foreach (var (t0, t1) in Crossings(f.Plan, p, f.D).Select(x => (Math.Max(x.T0, u0), Math.Min(x.T1, u1))).Where(x => x.Item2 > x.Item1))
                    {
                        Vec3 On(double k) { var q = p + f.D * k; return q.At(f.Z(q)) + f.Normal * (rd / 2); }
                        o.Add(new Member("trimmer", On(t0), On(t1), rw, rd));
                    }
                }
            }
            // Purlins: level, under the rafters, spread up the slope.
            var top = f.Plane.Points.Max(q => q.Z);
            var eaveZ = f.Z(a);
            for (int j = 1; j <= purlins; j++)
            {
                var h = eaveZ + (top - eaveZ) * j / (purlins + 1);
                var p = a + f.N * ((h - eaveZ) / f.Tan);
                foreach (var (t0, t1) in Crossings(f.Plan, p, f.D))
                    o.Add(new Member("purlin", (p + f.D * t0).At(h - Roof.Purlin.D / 2), (p + f.D * t1).At(h - Roof.Purlin.D / 2), Roof.Purlin.W, Roof.Purlin.D));
            }
        }
        // Ridge beams under the ridges; hip and valley rafters along hips and valleys, flush with the rafters' tops.
        foreach (var l in s.Lines)
        {
            if (l.Kind == LineKind.Ridge)
                o.Add(new Member("ridge", l.A with { Z = l.A.Z - Roof.RidgeBeam.D / 2 }, l.B with { Z = l.B.Z - Roof.RidgeBeam.D / 2 }, Roof.RidgeBeam.W, Roof.RidgeBeam.D));
            else if (l.Kind is LineKind.Hip or LineKind.Valley)
            {
                var dz = rd / minNz - Roof.HipRafter.D / 2;
                o.Add(new Member(l.Kind == LineKind.Hip ? "hip" : "valley", l.A with { Z = l.A.Z + dz }, l.B with { Z = l.B.Z + dz }, Roof.HipRafter.W, Roof.HipRafter.D));
            }
        }
        // Wall plates on the eave walls: the roof rests on them.
        for (int i = 0; i < s.Outline.Count; i++)
        {
            if (s.Rules[i].Kind != EdgeKind.Eave) continue;
            var z = r.PlateZ + Roof.WallPlate.D / 2;
            o.Add(new Member("plate", s.Outline[i].At(z), s.Outline[(i + 1) % s.Outline.Count].At(z), Roof.WallPlate.W, Roof.WallPlate.D));
        }
        return o.Where(m => m.Length > 0.05).ToList();
    }

    /// <summary>A gutter run: one eave line, where the covering ends.</summary>
    public sealed record Gutter(Vec3 A, Vec3 B, Vec2 Out)
    {
        public double Length => (B - A).Length;
    }

    /// <summary>Gutters under the eave lines (none when the roof has none), just below the covering's edge.</summary>
    public static List<Gutter> Gutters(Roof.Resolved r, RoofShape s)
    {
        if ((r.Roof.Str("gutterProfile") ?? "half_round") == "none") return [];
        var inside = s.Outline;
        return s.Lines.Where(l => l.Kind == LineKind.Eave).Select(l =>
        {
            var d = (l.B - l.A).Xy.Normalized();
            var outward = d.Perp;
            var mid = ((l.A + l.B) * 0.5).Xy;
            if (Polygon2.Contains(inside, mid + outward * 0.01)) outward = -outward;
            var z = r.StructureDepth;
            return new Gutter(l.A with { Z = l.A.Z + z }, l.B with { Z = l.B.Z + z }, outward);
        }).Where(g => g.Length > 0.3).ToList();
    }

    /// <summary>Downpipe outlets: one per 10 m of gutter, at the run's ends first; where each one meets the wall below.</summary>
    public static List<(Vec3 Outlet, Vec2 Foot)> Downpipes(Roof.Resolved r, RoofShape s, IReadOnlyList<Gutter> gutters)
    {
        var o = new List<(Vec3, Vec2)>();
        foreach (var g in gutters)
        {
            int n = Math.Max(1, (int)Math.Ceiling(g.Length / 10));
            var d = (g.B - g.A) * (1 / g.Length);
            for (int k = 0; k < n; k++)
            {
                // At the end (water runs to it), the others spread back along the run, 0.3 m in from the corner.
                var u = n == 1 ? g.Length - 0.3 : 0.3 + (g.Length - 0.6) * (n - 1 - k) / (n - 1);
                var outlet = g.A + d * u;
                // Down the wall: the eave line's overhang back toward the building, 0.1 m off the wall line.
                var foot = Nearest(s.Outline, outlet.Xy) is var w ? w + (outlet.Xy - w).Normalized() * 0.1 : outlet.Xy;
                o.Add((outlet, foot));
            }
        }
        return o;
    }

    static Vec2 Nearest(IReadOnlyList<Vec2> poly, Vec2 p)
    {
        Vec2 best = poly[0]; double bd = double.PositiveInfinity;
        for (int i = 0; i < poly.Count; i++)
        {
            var (a, b) = (poly[i], poly[(i + 1) % poly.Count]);
            var e = b - a; var l2 = Vec2.Dot(e, e);
            var t = l2 < 1e-12 ? 0 : Math.Clamp(Vec2.Dot(p - a, e) / l2, 0, 1);
            var q = a + e * t;
            var dd = (p - q).Length;
            if (dd < bd) { bd = dd; best = q; }
        }
        return best;
    }

    /// <summary>Snow guard rows: along every eave plane, 0.6 m up the slope from the eave (a second row on long slopes), on the covering.</summary>
    public static List<(Vec3 A, Vec3 B)> SnowGuards(Roof.Resolved r, RoofShape s, IReadOnlyList<RoofOpenings.Placed>? openings = null)
    {
        var o = new List<(Vec3, Vec3)>();
        if (r.Roof.Str("snowGuards") != "True") return o;
        var lift = r.StructureDepth + r.Covering.Thickness + 0.03;
        foreach (var f in Frames(s))
        {
            var a = s.Outline[f.Plane.Edge];
            var overhang = s.Rules[f.Plane.Edge].Overhang;
            var slope = f.Plan.Max(p => Vec2.Dot(p - a, f.N)) + overhang;
            var rows = slope > 6 ? new[] { 0.6, slope / 2 } : [0.6];
            foreach (var up in rows)
            {
                var p = a + f.N * (up - overhang);
                var cuts = HolesOn(f, openings).SelectMany(h => Crossings(h, p, f.D)).Select(x => (x.T0 - 0.15, x.T1 + 0.15)).ToList();
                foreach (var (t0, t1) in Minus(Crossings(f.Plan, p, f.D), cuts))
                {
                    if (t1 - t0 < 0.5) continue;
                    Vec3 At(double t) { var q = p + f.D * t; return q.At(f.Z(q)) + f.Normal * lift; }
                    o.Add((At(t0 + 0.15), At(t1 - 0.15)));
                }
            }
        }
        return o;
    }

    /// <summary>A round bar from a to b (gutter outlets, downpipes, snow guard tubes): an n-gon (by the chord rule when 0) swept along the axis.</summary>
    public static Solid? Tube(Vec3 a, Vec3 b, double radius, int sides = 0, string? tag = null)
    {
        var axis = b - a;
        if (axis.Length < 1e-6) return null;
        if (sides <= 0) sides = Chords.Circle(radius);
        var (u, v) = VecMath.PlaneBasis(axis.Normalized());
        var ring = Enumerable.Range(0, sides).Select(k => { var t = 2 * Math.PI * k / sides; return a + u * (radius * Math.Cos(t)) + v * (radius * Math.Sin(t)); }).ToArray();
        return Prism.Extrude(ring, null, axis, tag);
    }

    /// <summary>A gutter's body along its run: a half-round or box channel, its back against the fascia.</summary>
    public static Solid? Channel(Gutter g, bool box, string? tag = null)
    {
        const double R = 0.0625, wall = 0.006;   // a 125 system
        var dir = (g.B - g.A).Normalized();
        var outV = new Vec3(g.Out.X, g.Out.Y, 0);
        var c = g.A + outV * R;                   // the channel's axis, at the covering's edge level
        List<Vec3> profile;
        if (box)
        {
            double w = 2 * R, h = 0.09;
            Vec3 P(double x, double z) => c + outV * (x - R) + new Vec3(0, 0, z);
            profile = [P(0, 0), P(0, -h), P(w, -h), P(w, 0), P(w - wall, 0), P(w - wall, -h + wall), P(wall, -h + wall), P(wall, 0)];
        }
        else
        {
            // A half annulus, open at the top.
            var k = Chords.Count(R, Math.PI, 2);
            var outer = Enumerable.Range(0, k + 1).Select(i => { var t = Math.PI * i / k; return c + outV * (-R * Math.Cos(t)) + new Vec3(0, 0, -R * Math.Sin(t)); });
            var inner = Enumerable.Range(0, k + 1).Reverse().Select(i => { var t = Math.PI * i / k; return c + outV * (-(R - wall) * Math.Cos(t)) + new Vec3(0, 0, -(R - wall) * Math.Sin(t)); });
            profile = [.. outer, .. inner];
        }
        return Prism.Extrude(profile, null, g.B - g.A, tag);
    }
}
