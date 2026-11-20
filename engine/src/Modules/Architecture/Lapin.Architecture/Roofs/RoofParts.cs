// Ported from BubbleBIM Pro (the user's own engine, engine/src/Modules/Building/BubbleBim.Building/Roofs), adapted to Lapin's model.
using Lapin.Geometry;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// What a roof carries and drains, all derived from its solved shape — never
/// nodes in the graph:
/// <list type="bullet">
/// <item>the timber structure: wall plates on the eave walls, rafters at a spacing on every plane, purlins under them, a ridge beam under each ridge, hip and valley rafters (port of Standalone's roof system structure); or couples with collar ties, or trusses, along the main ridge (decision 229);</item>
/// <item>gutters along the eaves, downpipes from them down the walls to the ground, one per 10 m of gutter (Drainage, decision 235);</item>
/// <item>snow guards: rows along the eaves, on the covering.</item>
/// </list>
/// The planes are the rafters' underside; the covering lies on the rafters and battens.
/// </summary>
public static class RoofParts
{
    /// <summary>
    /// A member: a centre line with a width × depth section (depth across the slope, or vertical for a level member). A rafter
    /// or a chord is cut to its <see cref="Profile"/> (its face, in the vertical plane it stands in: plumb or square ends, the
    /// bird's mouth over the wall plate), swept across by <see cref="Side"/>; anything else is a box along its line. A truss's
    /// members carry its number.
    /// </summary>
    public sealed record Member(string Role, Vec3 A, Vec3 B, double Width, double Depth)
    {
        public double Length => Path is { Count: > 1 } p ? p.Zip(p.Skip(1), (x, y) => (y - x).Length).Sum() : (B - A).Length;
        public double Volume => Length * Width * Depth;
        public int Truss { get; init; } = -1;
        public IReadOnlyList<Vec3>? Profile { get; init; }
        public Vec3 Side { get; init; }
        /// <summary>A level member along a curved edge (a wall plate, a purlin): its centre line through the chords, A to B (decision 741).</summary>
        public IReadOnlyList<Vec3>? Path { get; init; }
        public Solid? Body(string tag) => Profile is { } p ? Prism.Extrude(p, null, Side, tag)
            : Path is { Count: > 2 } path ? RoofCurves.PathBody(path, Width, Depth, tag)
            : Prism.SweepBox(A, B, Width, Depth, tag);
    }

    /// <summary>A level member along a line of points (one member, however many chords).</summary>
    static Member Level(string role, IReadOnlyList<Vec3> path, double w, double d) =>
        path.Count == 2 ? new Member(role, path[0], path[1], w, d) : new Member(role, path[0], path[^1], w, d) { Path = path };

    /// <summary>The members' roles, as their parts and the schedule name them.</summary>
    public static string RoleLabel(string role) => role switch
    {
        "rafter" => "Rafter", "trimmer" => "Trimmer", "purlin" => "Purlin", "ridge" => "Ridge beam", "hip" => "Hip rafter", "valley" => "Valley rafter",
        "plate" => "Wall plate", "collar" => "Collar tie", "topChord" => "Top chord", "bottomChord" => "Bottom chord", "web" => "Web", _ => role,
    };

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

    /// <summary>How a rafter is cut: its section, its tail profile, the wall plate's width, and whether it is notched over the wall plate.</summary>
    sealed record Cut(double W, double D, string Tail, bool Seat, double PlateTop, double PlateW);

    /// <summary>The built-in rafter tail cut types.</summary>
    public static readonly string[] TailTypes = ["plumb", "square", "level", "bevel", "stepped", "curved"];

    /// <summary>
    /// Computes the horizontal pullback <c>TailX</c> along the rafter's underside and any intermediate profile points
    /// from the top-left corner <c>(0, Zs(0) + v)</c> down to <c>(TailX, Zs(TailX))</c>. Bounded by <c>run</c> so short
    /// rafters never invert or self-intersect.
    /// </summary>
    static (double TailX, List<(double Sv, double Z)> TailPts) TailProfile(
        string tail, double run, double d, double v, double tan, double secant, Func<double, double> zs)
    {
        var pts = new List<(double Sv, double Z)>();
        if (run < 1e-3) return (0, pts);

        switch (tail)
        {
            case "square":
            {
                var sin = tan / secant;
                var tx = d * sin < run * 0.5 ? d * sin : 0;
                return (tx, pts);
            }
            case "level":
            {
                if (tan <= 1e-4) return (0, pts);
                var h = Math.Min(v * 0.5, run * 0.45 * tan);
                var tx = h / tan;
                if (tx < 1e-4) return (0, pts);
                pts.Add((0, zs(0) + h));
                return (tx, pts);
            }
            case "bevel":
            {
                var tx = Math.Min(d * 0.85, run * 0.45);
                if (tx < 1e-4) return (0, pts);
                pts.Add((0, zs(0) + v * 0.30));
                return (tx, pts);
            }
            case "stepped":
            {
                var tx = Math.Min(d * 1.0, Math.Min(run * 0.45, tan > 1e-6 ? 0.45 * v / tan : d * 1.0));
                if (tx < 2e-4) return (0, pts);
                var s1 = tx * 0.5;
                var z1 = zs(0) + v * 0.68;
                var z2 = zs(0) + v * 0.48;
                pts.Add((0, z1));
                pts.Add((s1, z1));
                pts.Add((s1, z2));
                pts.Add((tx, z2));
                return (tx, pts);
            }
            case "curved":
            {
                var tx = Math.Min(d * 1.2, Math.Min(run * 0.45, tan > 1e-6 ? 0.55 * v / tan : d * 1.2));
                if (tx < 2e-4) return (0, pts);
                var zStart = zs(0) + v * 0.72;
                var zEnd = zs(tx);
                var dz = zStart - zEnd;
                if (dz < 1e-4) return (0, pts);
                var zMid = (zStart + zEnd) * 0.5;
                var n = Chords.Ellipse(tx * 0.5, dz * 0.5, Math.PI / 2, min: 2);
                pts.Add((0, zStart));
                for (int i = 1; i <= n; i++)
                {
                    var t = Math.PI / 2 * (1 - (double)i / n);
                    pts.Add((tx * 0.5 * Math.Cos(t), zMid + dz * 0.5 * Math.Sin(t)));
                }
                for (int i = 1; i < n; i++)
                {
                    var t = Math.PI / 2 * ((double)i / n);
                    pts.Add((tx - tx * 0.5 * Math.Cos(t), zMid - dz * 0.5 * Math.Sin(t)));
                }
                return (tx, pts);
            }
            default: // "plumb"
                return (0, pts);
        }
    }

    /// <summary>
    /// A rafter (or a top chord) in the vertical plane through <paramref name="p"/> along <paramref name="u"/>, from k0 to k1,
    /// its underside on z(k): its high end cut plumb, its low end (the eave) cut to <see cref="Cut.Tail"/>, and, when <paramref name="wall"/>
    /// (the wall line's k) lies on it, a bird's mouth there: a level seat on the plate from the wall line out to the plate's
    /// outer face and a plumb heel, at most a third of the rafter's depth.
    /// </summary>
    static Member Rafter(string role, Vec2 p, Vec2 u, double k0, double k1, Func<double, double> z, Cut c, double? wall, int truss = -1)
    {
        var (z0, z1) = (z(k0), z(k1));
        var g = (z1 - z0) / (k1 - k0);
        var (low, dir) = z0 <= z1 ? (k0, 1.0) : (k1, -1.0);
        var run = k1 - k0;
        var tan = Math.Abs(g);
        var secant = Math.Sqrt(1 + g * g);
        var v = c.D * secant;
        var side = new Vec2(-u.Y, u.X);
        double Zs(double sv) => z(low + dir * sv);
        Vec3 P(double sv, double zv) => (p + u * (low + dir * sv) - side * (c.W / 2)).At(zv);
        var face = new List<Vec3>();
        var (tail, tailPts) = TailProfile(c.Tail, run, c.D, v, tan, secant, Zs);
        face.Add(P(tail, Zs(tail)));
        if (wall is { } kw && c.Seat && tan > 1e-6)
        {
            var sw = Math.Abs(kw - low);
            var so = Math.Max(Math.Max(tail, 0), sw - c.PlateW / 2);
            so = Math.Max(so, sw - v / 3 / tan);
            if (sw > so + 1e-4 && sw < run - 1e-4)
            {
                face.Add(P(so, Zs(so)));
                face.Add(P(so, Zs(sw)));
                face.Add(P(sw, Zs(sw)));
            }
        }
        face.Add(P(run, Zs(run)));
        face.Add(P(run, Zs(run) + v));
        face.Add(P(0, Zs(0) + v));
        foreach (var (sv, zv) in tailPts) face.Add(P(sv, zv));
        Vec3 Axis(double k) => (p + u * k).At(z(k) + v / 2);
        return new Member(role, Axis(k0), Axis(k1), c.W, c.D) { Profile = face, Side = (side * c.W).At(0), Truss = truss };
    }

    /// <summary>
    /// The timber structure (empty when the roof has none). Rafters on purlins with a ridge beam; or, along the main ridge,
    /// couples with collar ties or trusses at the spacing on the slopes that run with it (the hip ends keep rafters, and so does
    /// a station a hip slope or an opening crosses); hip and valley rafters, trimmers round the openings, wall plates.
    /// </summary>
    public static List<Member> Framing(Roof.Resolved r, RoofShape s, IReadOnlyList<RoofOpenings.Placed>? openings = null)
    {
        var o = new List<Member>();
        if (!r.Framed) return o;
        var (rw, rd) = r.Rafter;
        var (wpw, wpd) = r.WallPlateSection;
        var (pw, pd) = r.PurlinSection;
        var (rgw, rgd) = r.RidgeBeamSection;
        var spacing = Math.Max(0.3, r.Roof.NumOrNull("rafterSpacing") ?? 0.8);
        var purlins = r.Scheme == "rafters" ? Math.Clamp((int)(r.Roof.NumOrNull("purlins") ?? 1), 0, 3) : 0;
        var cut = new Cut(rw, rd, r.Roof.Str("rafterTail") ?? "plumb", r.Roof.Str("rafterSeat") != "none", r.BaseZ, wpw);
        var frames = Frames(s);
        var minNz = frames.Count == 0 ? 1 : frames.Min(f => f.Normal.Z);

        // The stations' axis: the longest ridge, or a lone slope's eave; the slopes that run with it are the main ones.
        Vec2? axis = null;
        if (r.Scheme != "rafters")
        {
            var ridge = s.Lines.Where(l => l.Kind == LineKind.Ridge && l.Length > 0.1).MaxBy(l => l.Length);
            axis = ridge != null ? (ridge.B - ridge.A).Xy.Normalized() : frames.Count == 1 ? frames[0].D : null;
        }
        // A curved edge's chords (decision 741): its facets take their rafters along the curve, its purlins at one height.
        var chains = RoofCurves.Chains(r, s);
        var chainOf = RoofCurves.ChainOf(r, s, chains);
        var chainTop = chains.Select(c => frames.Where(f => c.Contains(f.Plane.Edge)).Select(f => f.Plane.Points.Max(q => q.Z)).DefaultIfEmpty(0).Max()).ToList();
        var curvedPurlins = new List<(int Chain, int Row, Vec3 A, Vec3 B)>();
        var smooth = RoofCurves.SmoothRuns(r, s);
        var alongCurve = new bool[s.Outline.Count];
        foreach (var (edges, _) in smooth) foreach (var j in edges) alongCurve[j] = true;
        bool Main(Frame f) => !alongCurve[f.Plane.Edge] && axis is { } ax && Math.Abs(Vec2.Dot(f.D, ax)) > Math.Cos(10 * Math.PI / 180);

        foreach (var f in frames)
        {
            var a = s.Outline[f.Plane.Edge];
            var holes = HolesOn(f, openings);
            var chain = chainOf[f.Plane.Edge];
            if (!Main(f) && !alongCurve[f.Plane.Edge])
            {
                // Rafters: across the plane along its eave, every `spacing`, up the slope; their underside on the plane.
                var along = f.Plan.Select(q => Vec2.Dot(q - a, f.D)).ToList();
                double lo = along.Min() + rw / 2, hi = along.Max() - rw / 2;
                int count = Math.Max(1, (int)Math.Round((hi - lo) / spacing));
                for (int j = 0; j <= count; j++)
                {
                    var t = hi > lo ? lo + (hi - lo) * j / count : (lo + hi) / 2;
                    var p = a + f.D * t;
                    var cuts = holes.SelectMany(h => Crossings(h, p, f.N)).ToList();
                    foreach (var (t0, t1) in Minus(Crossings(f.Plan, p, f.N), cuts))
                        o.Add(Rafter("rafter", p, f.N, t0, t1, k => f.Z(p + f.N * k), cut, t0 < 0 && t1 > 0 ? 0 : null));
                }
            }
            // Trimmers: across each opening, under and over it, carrying the rafters it cut — a spacing past each side.
            foreach (var h in holes)
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
            var top = chain >= 0 ? chainTop[chain] : f.Plane.Points.Max(q => q.Z);
            var eaveZ = f.Z(a);
            for (int j = 1; j <= purlins; j++)
            {
                var h = eaveZ + (top - eaveZ) * j / (purlins + 1);
                var p = a + f.N * ((h - eaveZ) / f.Tan);
                foreach (var (t0, t1) in Crossings(f.Plan, p, f.D))
                {
                    var (pa, pb) = ((p + f.D * t0).At(h - pd / 2), (p + f.D * t1).At(h - pd / 2));
                    if (chain >= 0) curvedPurlins.Add((chain, j, pa, pb));
                    else o.Add(new Member("purlin", pa, pb, pw, pd));
                }
            }
        }

        // Over a curved edge: each row of purlin one member along the curve, its facets' pieces joined.
        foreach (var row in curvedPurlins.GroupBy(x => (x.Chain, x.Row)))
            foreach (var line in RoofCurves.Join(row.Select(x => (x.A, x.B))))
                o.Add(Level("purlin", line, pw, pd));

        // Rafters along a curved edge and the edges it runs on into smoothly: at the spacing along the curve (not two per
        // chord, nor two where a fillet meets its straight edge), each square to the chord it stands on, from the eave up
        // through the run's planes to where they end.
        foreach (var (c, closed) in smooth)
        {
            var cf = c.Select(j => frames.FirstOrDefault(f => f.Plane.Edge == j)).ToList();
            var n = s.Outline.Count;
            var lens = c.Select(j => (s.Outline[(j + 1) % n] - s.Outline[j]).Length).ToList();
            var total = lens.Sum();
            double lo = closed ? 0 : rw / 2, hi = closed ? total : total - rw / 2;
            int count = Math.Max(1, (int)Math.Round((hi - lo) / spacing));
            var plans = cf.OfType<Frame>().ToList();
            var holes = plans.SelectMany(f => HolesOn(f, openings)).ToList();
            for (int j = 0; j <= (closed ? count - 1 : count); j++)
            {
                var st = hi > lo ? lo + (hi - lo) * j / count : total / 2;
                var k = 0;
                while (k < lens.Count - 1 && st > lens[k]) { st -= lens[k]; k++; }
                if (cf[k] is not { } f) continue;
                var p = s.Outline[c[k]] + f.D * Math.Clamp(st, 0, lens[k]);
                // The run up the slope through the curve's facets (those turned little from its own): the one that starts at the eave.
                var near = plans.Where(x => Vec2.Dot(x.N, f.N) >= Math.Cos(Drainage.SmoothTurn * Math.PI / 180));
                var runs = Merge(near.SelectMany(x => Crossings(x.Plan, p, f.N)));
                if (runs.Where(x => x.T0 <= 1e-6 && x.T1 > 1e-6).Cast<(double T0, double T1)?>().FirstOrDefault() is not { } run) continue;
                var cuts = holes.SelectMany(h => Crossings(h, p, f.N)).ToList();
                foreach (var (t0, t1) in Minus([run], cuts))
                    o.Add(Rafter("rafter", p, f.N, t0, t1, q => f.Z(p + f.N * q), cut, t0 < 0 && t1 > 0 ? 0 : null));
            }
        }

        if (axis is { } u && frames.Where(Main).ToList() is { Count: > 0 } main)
            Stations(r, s, frames, main, u, spacing, cut, openings, o);

        // Ridge beams under the ridges (rafters on purlins only); hip and valley rafters along hips and valleys, flush with the rafters' tops.
        var curvedHips = new List<(string Role, Vec3 A, Vec3 B)>();
        foreach (var l in s.Lines)
        {
            if (l.Kind == LineKind.Ridge && r.Scheme == "rafters")
                o.Add(new Member("ridge", l.A with { Z = l.A.Z - rgd / 2 }, l.B with { Z = l.B.Z - rgd / 2 }, rgw, rgd));
            else if (l.Kind is LineKind.Hip or LineKind.Valley)
            {
                var dz = rd / minNz - Roof.HipRafter.D / 2;
                var (role, ha, hb) = (l.Kind == LineKind.Hip ? "hip" : "valley", l.A with { Z = l.A.Z + dz }, l.B with { Z = l.B.Z + dz });
                if (chains.Count > 0) curvedHips.Add((role, ha, hb));
                else o.Add(new Member(role, ha, hb, Roof.HipRafter.W, Roof.HipRafter.D));
            }
        }
        // Where a curve's facets meet a plane the hip is curved, one piece per facet: one member along them (decision 741).
        foreach (var g in curvedHips.GroupBy(x => x.Role))
            foreach (var line in RoofCurves.Join(g.Select(x => (x.A, x.B)), Drainage.SmoothTurn))
                o.Add(line.Count == 2 ? new Member(g.Key, line[0], line[1], Roof.HipRafter.W, Roof.HipRafter.D)
                    : new Member(g.Key, line[0], line[^1], Roof.HipRafter.W, Roof.HipRafter.D) { Path = line });
        // Wall plates on the eave walls: the roof rests on them.
        // A curved eave's plate is one member along the curve (decision 741).
        var plateZ = r.PlateZ + wpd / 2;
        for (int i = 0; i < s.Outline.Count; i++)
        {
            if (s.Rules[i].Kind != EdgeKind.Eave || chainOf[i] >= 0) continue;
            o.Add(new Member("plate", s.Outline[i].At(plateZ), s.Outline[(i + 1) % s.Outline.Count].At(plateZ), wpw, wpd));
        }
        foreach (var c in chains)
        {
            if (s.Rules[c[0]].Kind != EdgeKind.Eave) continue;
            o.Add(Level("plate", [.. c.Select(j => s.Outline[j].At(plateZ)), s.Outline[(c[^1] + 1) % s.Outline.Count].At(plateZ)], wpw, wpd));
        }
        return o.Where(m => m.Length > 0.05).ToList();
    }

    /// <summary>Intervals joined where they meet or overlap.</summary>
    static List<(double T0, double T1)> Merge(IEnumerable<(double T0, double T1)> xs)
    {
        var o = new List<(double T0, double T1)>();
        foreach (var x in xs.OrderBy(x => x.T0))
            if (o.Count > 0 && x.T0 <= o[^1].T1 + 1e-4) o[^1] = (o[^1].T0, Math.Max(o[^1].T1, x.T1));
            else o.Add(x);
        return o;
    }

    /// <summary>
    /// Couples or trusses at the spacing along the axis, across the main slopes. A station that also crosses a hip slope or an
    /// opening, or whose slopes do not meet at a ridge (two) or stand alone (one), gets the main slopes' rafters instead.
    /// </summary>
    static void Stations(Roof.Resolved r, RoofShape s, List<Frame> frames, List<Frame> main, Vec2 u, double spacing, Cut cut, IReadOnlyList<RoofOpenings.Placed>? openings, List<Member> o)
    {
        var n = new Vec2(-u.Y, u.X);
        var (rw, rd) = (cut.W, cut.D);
        var along = main.SelectMany(f => f.Plan).Select(q => Vec2.Dot(q, u)).ToList();
        double lo = along.Min() + rw / 2, hi = along.Max() - rw / 2;
        int count = Math.Max(1, (int)Math.Round((hi - lo) / spacing));
        var others = frames.Where(f => !main.Contains(f)).ToList();
        var holes = main.ToDictionary(f => f, f => HolesOn(f, openings));
        var truss = 0;
        for (int j = 0; j <= count; j++)
        {
            var p = u * (hi > lo ? lo + (hi - lo) * j / count : (lo + hi) / 2);
            var segs = main.SelectMany(f => Crossings(f.Plan, p, n).Select(c => (F: f, K0: c.T0, K1: c.T1))).OrderBy(x => x.K0).ToList();
            if (segs.Count == 0) continue;
            double Z(Frame f, double k) => f.Z(p + n * k);
            var walls = Crossings(s.Outline, p, n).OrderByDescending(w => Math.Min(w.T1, segs[^1].K1) - Math.Max(w.T0, segs[0].K0)).FirstOrDefault();
            var crossesHip = others.Any(f => Crossings(f.Plan, p, n).Count > 0);
            var crossesHole = segs.Any(x => holes[x.F].Any(h => Crossings(h, p, n).Count > 0));
            var duo = segs.Count == 2 && Math.Abs(segs[0].K1 - segs[1].K0) < 1e-4
                && Z(segs[0].F, segs[0].K1) > Z(segs[0].F, segs[0].K0) && Z(segs[1].F, segs[1].K0) > Z(segs[1].F, segs[1].K1);
            var mono = segs.Count == 1;
            var ok = !crossesHip && !crossesHole && walls.T1 > walls.T0 && (duo || mono && r.Scheme == "trusses");
            if (!ok)
            {
                foreach (var x in segs)
                {
                    var cuts = holes[x.F].SelectMany(h => Crossings(h, p, n)).ToList();
                    foreach (var (t0, t1) in Minus([(x.K0, x.K1)], cuts))
                        o.Add(Rafter("rafter", p, n, t0, t1, k => Z(x.F, k), cut, WallOn(walls, t0, t1, k => Z(x.F, k))));
                }
                continue;
            }
            var (w0, w1) = walls;
            if (r.Scheme == "collar")
            {
                var (a, b) = (segs[0], segs[1]);
                o.Add(Rafter("rafter", p, n, a.K0, a.K1, k => Z(a.F, k), cut, WallOn(walls, a.K0, a.K1, k => Z(a.F, k))));
                o.Add(Rafter("rafter", p, n, b.K0, b.K1, k => Z(b.F, k), cut, WallOn(walls, b.K0, b.K1, k => Z(b.F, k))));
                // The collar tie: level, beside the couple, at its share of the rise, from the one rafter's centre line to the other's.
                var (cw, cd) = r.CollarSection;
                var peak = Z(a.F, a.K1);
                var share = Math.Clamp(r.Roof.NumOrNull("collarHeight") ?? 0.66, 0.3, 0.9);
                var zc = r.BaseZ + (peak - r.BaseZ) * share + cd / 2;
                var (ga, gb) = ((Z(a.F, a.K1) - Z(a.F, a.K0)) / (a.K1 - a.K0), (Z(b.F, b.K1) - Z(b.F, b.K0)) / (b.K1 - b.K0));
                var (va, vb) = (rd * Math.Sqrt(1 + ga * ga), rd * Math.Sqrt(1 + gb * gb));
                var ka = a.K0 + (zc - va / 2 - Z(a.F, a.K0)) / ga;
                var kb = b.K0 + (zc - vb / 2 - Z(b.F, b.K0)) / gb;
                if (kb - ka > 0.2)
                    o.Add(new Member("collar", (p + n * ka).At(zc) + (u * ((rw + cw) / 2)).At(0), (p + n * kb).At(zc) + (u * ((rw + cw) / 2)).At(0), cw, cd));
                continue;
            }
            // A truss: the top chords on the slopes, the bottom chord on the plates, the webs between.
            var chord = cut with { Seat = false };
            var zb = r.BaseZ + rd / 2;
            var (ww, wd) = r.Web;
            var (bl, bh) = (w0 - cut.PlateW / 2, w1 + cut.PlateW / 2);
            foreach (var x in segs) o.Add(Rafter("topChord", p, n, x.K0, x.K1, k => Z(x.F, k), chord, null, truss));
            o.Add(new Member("bottomChord", (p + n * bl).At(zb), (p + n * bh).At(zb), rw, rd) { Truss = truss });
            Vec3 Bottom(double k) => (p + n * k).At(zb);
            void Web(Vec3 from, Vec3 to) { if ((to - from).Length > 0.1) o.Add(new Member("web", from, to, ww, wd) { Truss = truss }); }
            var span = w1 - w0;
            if (duo)
            {
                var (a, b) = (segs[0], segs[1]);
                var kp = a.K1;
                var under = Z(a.F, kp);
                Vec3 TopA(double k) => (p + n * k).At(Z(a.F, k));
                Vec3 TopB(double k) => (p + n * k).At(Z(b.F, k));
                if (span < 3) Web(Bottom(kp), (p + n * kp).At(under));
                else
                {
                    // Fink (W): the bottom chord in thirds, the top chords at their middles, two webs to the peak.
                    var (b1, b2) = (w0 + span / 3, w0 + span * 2 / 3);
                    Web(Bottom(b1), TopA((w0 + kp) / 2));
                    Web(Bottom(b1), (p + n * kp).At(under));
                    Web(Bottom(b2), (p + n * kp).At(under));
                    Web(Bottom(b2), TopB((kp + w1) / 2));
                }
            }
            else
            {
                var x = segs[0];
                var high = Z(x.F, w1) > Z(x.F, w0) ? w1 - cut.PlateW / 2 : w0 + cut.PlateW / 2;
                var lowEnd = high > (w0 + w1) / 2 ? w0 : w1;
                Vec3 Top(double k) => (p + n * k).At(Z(x.F, k));
                // A mono truss: a post at the high end; over 3 m a post at the middle and two diagonals (N).
                Web(Bottom(high), Top(high));
                if (span >= 3)
                {
                    var mid = (w0 + w1) / 2;
                    Web(Bottom(mid), Top(mid));
                    Web(Bottom(mid), Top((mid + high) / 2));
                    Web(Bottom((lowEnd + mid) / 2), Top(mid));
                }
            }
            truss++;
        }
    }

    /// <summary>The wall line's k on a rafter from k0 to k1, when it lies on it at the rafter's low end side (where its bird's mouth goes).</summary>
    static double? WallOn((double T0, double T1) walls, double k0, double k1, Func<double, double> z)
    {
        if (walls.T1 <= walls.T0) return null;
        var lowAtStart = z(k0) <= z(k1);
        var w = lowAtStart ? walls.T0 : walls.T1;
        return w > k0 && w < k1 ? w : null;
    }

    /// <summary>
    /// A gutter run: one eave line, where the covering ends, outward from the roof; the outline edge it belongs to (decision 235).
    /// <see cref="StartMiter"/> and <see cref="EndMiter"/> are the slopes along the run per unit outward width at each end where
    /// it miters into an adjacent gutter; <see cref="CapStart"/> and <see cref="CapEnd"/> are false at joined corners.
    /// </summary>
    public sealed record Gutter(Vec3 A, Vec3 B, Vec2 Out, int Edge = -1, double StartMiter = 0, double EndMiter = 0, bool CapStart = true, bool CapEnd = true)
    {
        public double Length => (B - A).Length;
    }

    /// <summary>
    /// Gutters under the eave lines (none when the roof has none, nor on an eave edge whose gutter is off): just below the
    /// covering's edge, set out from it by the gutter's offset and down by its drop (decision 235). Adjacent eave gutters at
    /// the same height are mitered at their shared corner and omit their end caps there.
    /// </summary>
    public static List<Gutter> Gutters(Roof.Resolved r, RoofShape s)
    {
        if ((r.Roof.Str("gutterProfile") ?? "half_round") == "none") return [];
        var spec = Drainage.Spec.Of(r.Roof);
        var inside = s.Outline;
        // Only where the water goes: an eave line that is its slope's lowest edge (a shed roof's high edge is drawn as an eave too, but takes no gutter).
        bool Low(RoofLine l) => s.Planes.FirstOrDefault(p => p.Points.Any(q => (q - l.A).Length < 1e-6) && p.Points.Any(q => (q - l.B).Length < 1e-6)) is not { } plane
            || Math.Max(l.A.Z, l.B.Z) <= plane.Points.Min(q => q.Z) + 1e-6;
        var items = s.Lines.Where(l => l.Kind == LineKind.Eave && Low(l)).Select(l =>
        {
            var d = (l.B - l.A).Xy.Normalized();
            var outward = d.Perp;
            var mid = ((l.A + l.B) * 0.5).Xy;
            // Outward: away from the walls (the eave line stands its overhang out of them, so look that far past it).
            var edge = Drainage.EdgeOf(r, l);
            var reach = (edge >= 0 ? r.Rules[edge].Overhang : 0) + 0.05;
            if (Polygon2.Contains(inside, mid + outward * reach)) outward = -outward;
            var shift = new Vec3(outward.X, outward.Y, 0) * spec.Offset + new Vec3(0, 0, r.StructureDepth - spec.Drop);
            return (G: new Gutter(l.A + shift, l.B + shift, outward, edge), RawA: l.A, RawB: l.B);
        }).Where(x => x.G.Length > (r.EdgeOf != null ? 0.02 : 0.3) && Drainage.HasGutter(r.Roof, r.LogicalEdge(x.G.Edge))).ToList();

        for (var i = 0; i < items.Count; i++)
        {
            for (var j = i + 1; j < items.Count; j++)
            {
                var (gi, ai, bi) = items[i];
                var (gj, aj, bj) = items[j];
                (int EndI, int EndJ)? match =
                    (ai - aj).Length < 1e-4 ? (0, 0) :
                    (ai - bj).Length < 1e-4 ? (0, 1) :
                    (bi - aj).Length < 1e-4 ? (1, 0) :
                    (bi - bj).Length < 1e-4 ? (1, 1) : null;
                if (match is not var (endI, endJ)) continue;

                var di = (bi - ai).Xy.Normalized();
                var dj = (bj - aj).Xy.Normalized();
                var den = Vec2.Cross(di, dj);
                double mi = 0, mj = 0;
                if (Math.Abs(den) < 1e-4)
                {
                    // Collinear split eave: suppress internal end caps without mitering; ignore 180° hairpins.
                    if (Vec2.Dot(gi.Out, gj.Out) < 0.999) continue;
                }
                else
                {
                    mi = Vec2.Cross(gj.Out - gi.Out, dj) / den;
                    mj = Vec2.Cross(gi.Out - gj.Out, di) / -den;
                }

                var shiftI = new Vec3(di.X, di.Y, 0) * (spec.Offset * mi);
                var shiftJ = new Vec3(dj.X, dj.Y, 0) * (spec.Offset * mj);
                items[i] = items[i] with
                {
                    G = endI == 0
                        ? gi with { A = gi.A + shiftI, StartMiter = mi, CapStart = false }
                        : gi with { B = gi.B + shiftI, EndMiter = mi, CapEnd = false },
                };
                items[j] = items[j] with
                {
                    G = endJ == 0
                        ? gj with { A = gj.A + shiftJ, StartMiter = mj, CapStart = false }
                        : gj with { B = gj.B + shiftJ, EndMiter = mj, CapEnd = false },
                };
            }
        }
        return items.Select(x => x.G).ToList();
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

    /// <summary>A gutter's body along its run: a half-round or box channel, its back against the fascia (a 125 system unless its radius is given).</summary>
    public static Solid? Channel(Gutter g, bool box, string? tag = null, double R = 0.0625)
    {
        const double wall = 0.006;
        var outV = new Vec3(g.Out.X, g.Out.Y, 0);
        List<(double X, double Z)> xz = [];
        if (box)
        {
            double w = 2 * R, h = 0.09;
            xz = [(0, 0), (0, -h), (w, -h), (w, 0), (w - wall, 0), (w - wall, -h + wall), (wall, -h + wall), (wall, 0)];
        }
        else
        {
            // A half annulus, open at the top.
            var k = Chords.Count(R, Math.PI, 2);
            for (var i = 0; i <= k; i++) { var t = Math.PI * i / k; xz.Add((R * (1 - Math.Cos(t)), -R * Math.Sin(t))); }
            for (var i = k; i >= 0; i--) { var t = Math.PI * i / k; xz.Add((R - (R - wall) * Math.Cos(t), -(R - wall) * Math.Sin(t))); }
        }
        if (Math.Abs(g.StartMiter) < 1e-9 && Math.Abs(g.EndMiter) < 1e-9)
        {
            var profile = xz.Select(p => g.A + outV * p.X + new Vec3(0, 0, p.Z)).ToList();
            return Prism.Extrude(profile, null, g.B - g.A, tag);
        }
        var dirXY = new Vec3(g.B.X - g.A.X, g.B.Y - g.A.Y, 0);
        if (dirXY.Length < 1e-9) return null;
        dirXY = dirXY.Normalized();
        var lo = xz.Select(p => g.A + outV * p.X + dirXY * (p.X * g.StartMiter) + new Vec3(0, 0, p.Z)).ToList();
        var hi = xz.Select(p => g.B + outV * p.X + dirXY * (p.X * g.EndMiter) + new Vec3(0, 0, p.Z)).ToList();
        var nStart = VecMath.NewellNormal(lo);
        if (nStart.Length < Tolerance.Area) return null;
        if (Vec3.Dot(nStart, dirXY) < 0) { lo.Reverse(); hi.Reverse(); nStart = -nStart; }
        var nEnd = VecMath.NewellNormal(hi);
        if (nEnd.Length < Tolerance.Area) return null;
        if (Vec3.Dot(nEnd, dirXY) < 0) nEnd = -nEnd;
        var n = lo.Count;
        var verts = new List<Vec3>(n * 2);
        verts.AddRange(lo);
        verts.AddRange(hi);
        var faces = new List<FaceInput>
        {
            new(Enumerable.Range(0, n).Reverse().ToArray(), null, -nStart, "start"),
            new(Enumerable.Range(n, n).ToArray(), null, nEnd, "end"),
        };
        var sloping = Math.Abs(g.B.Z - g.A.Z) > 1e-9;
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            if (sloping)
            {
                faces.Add(new FaceInput([i, j, n + j], null, Vec3.Cross(lo[j] - lo[i], hi[j] - lo[i]), "side"));
                faces.Add(new FaceInput([i, n + j, n + i], null, Vec3.Cross(hi[j] - lo[i], hi[i] - lo[i]), "side"));
            }
            else
            {
                faces.Add(new FaceInput([i, j, n + j, n + i], null, Vec3.Cross(lo[j] - lo[i], hi[i] - lo[i]), "side"));
            }
        }
        return SolidBuilder.Make(verts, faces, tag);
    }

    /// <summary>A gutter's end cap at A, 4 mm thick towards B: its channel's outline filled.</summary>
    public static Solid? Cap(Gutter g, bool box, double R, string? tag = null)
    {
        var outV = new Vec3(g.Out.X, g.Out.Y, 0);
        var c = g.A + outV * R;
        List<Vec3> face;
        if (box)
        {
            Vec3 P(double x, double z) => c + outV * (x - R) + new Vec3(0, 0, z);
            face = [P(0, 0), P(0, -0.09), P(2 * R, -0.09), P(2 * R, 0)];
        }
        else
        {
            var k = Chords.Count(R, Math.PI, 2);
            face = [.. Enumerable.Range(0, k + 1).Select(i => { var t = Math.PI * i / k; return c + outV * (-R * Math.Cos(t)) + new Vec3(0, 0, -R * Math.Sin(t)); })];
        }
        var dir = (g.B - g.A).Normalized();
        return Prism.Extrude(face, null, dir * 0.004, tag);
    }
}
