using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// Where two roofs meet (a tester's report, decision 847): a roof stops at another one over it instead of running on
/// through it to the wall behind. Worked out on every ask from the two roofs' planes, never stored, so plans, sections,
/// 3D, IFC and the covering's areas follow, and an older project needs no upgrade.
/// <list type="bullet">
/// <item>A roof under another one (its wall plate lower by more than <see cref="Stacked"/>: a lean-to or a wing a storey
/// down) ends where it would meet the upper roof's underside, its soffit or its fascia; where it stays below them (a
/// lean-to under a deep eave) it runs on to the wall, as it is built, with its flashing there.</item>
/// <item>Two roofs on plates at one height (an L-shaped house with two ridges) meet where their top surfaces cross:
/// wherever they overlap in plan, the one whose covering is higher stays and the other is taken away, so a lower ridge
/// drawn into the higher roof ends on its valleys, and the higher roof's eave stops at the lower roof it runs into.</item>
/// </list>
/// What is taken away is the roof's whole depth over that part of the plan: covering, battens or deck, rafters, plates and
/// gutters (its downpipes stay). A roof recognised from a file is never cut (an import reproduces the file), though it cuts.
/// </summary>
public static class RoofJunctions
{
    /// <summary>Two plates this much apart are two roofs one over the other (m).</summary>
    public const double Stacked = 0.05;
    /// <summary>Surfaces this close are taken as touching, not as one in the other (m).</summary>
    const double Tol = 0.001;
    /// <summary>A cut reaches this far past its own region, so its sides never lie on the roof's own (m).</summary>
    const double Proud = 0.001;

    /// <summary>A height over the plan, linear: z = A x + B y + C.</summary>
    readonly record struct Lin(double A, double B, double C)
    {
        public double At(Vec2 p) => A * p.X + B * p.Y + C;
        public static Lin operator -(Lin x, Lin y) => new(x.A - y.A, x.B - y.B, x.C - y.C);
        public Lin Plus(double d) => this with { C = C + d };
    }

    /// <summary>A convex piece of a roof in plan (counter-clockwise), on its plane <see cref="Plane"/> (−1 flat): its underside and its top.</summary>
    sealed record Piece(Vec2[] Ring, Lin Bottom, Lin Top, int Plane, double Cos);

    sealed record Body(Entity Roof, double PlateZ, List<Piece> Pieces, Vec2 Min, Vec2 Max);

    /// <summary>A region of a roof's plan taken away where it meets another roof: convex, counter-clockwise, on its plane <see cref="Plane"/> (−1 flat) whose slope's cosine is <see cref="Cos"/>.</summary>
    public sealed record Cut(int Plane, Vec2[] Region, double Cos)
    {
        /// <summary>Taken by a roof over this one (its underside, soffit or fascia); otherwise by one at its height, where their tops cross (a valley between them).</summary>
        public bool Under { get; init; }
        public double ProjectedArea => Math.Abs(Polygon2.SignedArea(Region));
        public double Area => ProjectedArea / Cos;
    }

    /// <summary>The parts of roof <paramref name="r"/>'s plan taken away by the other roofs of the model; none for a roof recognised from a file.</summary>
    public static List<Cut> Cuts(Roof.Resolved r, RoofShape? shape, IModelView m)
    {
        var o = new List<Cut>();
        if (r.Roof.Attrs["imported"] != null || BodyOf(r, shape) is not { } me) return o;
        foreach (var other in m.OfClass(IfcClass.Roof))
        {
            if (other.Id == r.Roof.Id || Roof.Resolve(other, m) is not { } or) continue;
            // The other's plates clearly higher: it is over this one (the soffit rule); clearly lower: it never cuts this one.
            var dz = or.PlateZ - r.PlateZ;
            if (dz < -Stacked) continue;
            var over = dz > Stacked;
            var eaves = Roof.EaveOutline(or);
            if (!Overlap(me, eaves)) continue;
            if (BodyOf(or, or.Flat ? null : Roof.Solve(or).Shape) is not { } it) continue;
            foreach (var a in me.Pieces)
                foreach (var b in it.Pieces)
                {
                    // Over the plan both share: under a higher roof this one goes where its top rises past the other's underside;
                    // beside one at its height, where its top is under the other's top.
                    var both = Convex(a.Ring, b.Ring);
                    if (both.Count < 3) continue;
                    var keep = over ? a.Top - b.Bottom : b.Top - a.Top;
                    var region = Polygon2.ClipHalf(both, p => keep.At(p) - Tol);
                    if (region.Count >= 3 && Math.Abs(Polygon2.SignedArea(region)) > 1e-6) o.Add(new Cut(a.Plane, region.ToArray(), a.Cos) { Under = over });
                }
        }
        return o;
    }

    /// <summary>The roof's built parts less what the other roofs take away (its downpipes left whole); the parts as they are when nothing is.</summary>
    public static List<GeometryPart> Apply(IReadOnlyList<GeometryPart> parts, IReadOnlyList<Cut> cuts, ISolidBooleans booleans, List<string> diags)
    {
        if (cuts.Count == 0) return parts.ToList();
        static bool Cuttable(GeometryPart p) => p.Key?.StartsWith("downpipe:") != true;
        var bounds = parts.Where(Cuttable).Select(p => Measure.Bounds(p.Solid)).OfType<(Vec3 Min, Vec3 Max)>().ToList();
        if (bounds.Count == 0) return parts.ToList();
        var (z0, z1) = (bounds.Min(b => b.Min.Z) - 0.1, bounds.Max(b => b.Max.Z) + 0.1);
        var tools = cuts.Select(c => Prism.ExtrudeFootprint(Polygon2.Offset(c.Region, c.Region.Select(_ => Proud).ToArray()), z0, z1 - z0, "roof-junction"))
            .OfType<Solid>().ToList();
        var boxes = tools.Select(t => Measure.Bounds(t)!.Value).ToList();
        var o = new List<GeometryPart>();
        foreach (var part in parts)
        {
            if (!Cuttable(part) || Measure.Bounds(part.Solid) is not { } pb) { o.Add(part); continue; }
            var mine = tools.Where((_, k) => Meets(boxes[k], pb)).ToList();
            if (mine.Count == 0) { o.Add(part); continue; }
            try { if (booleans.Difference(part.Solid, mine, part.Solid.Tag) is { } cut) o.Add(part with { Solid = cut, Pieces = CutPriorities.CutPieces(part.Pieces, mine, booleans) }); }
            catch (InvalidOperationException ex) { diags.Add($"{part.Name}: could not be cut where it meets another roof ({ex.Message})."); o.Add(part); }
        }
        return o;
    }

    /// <summary>The roof's lines in plan (its overhead outline, ridges, hips, valleys) less what the cuts took away; the items as they are when nothing is.</summary>
    public static List<PlanItem> Plan(IReadOnlyList<PlanItem> items, Roof.Resolved r, RoofShape? shape, IReadOnlyList<Cut> cuts)
    {
        if (cuts.Count == 0) return items.ToList();
        var own = Own(r, shape);
        var o = new List<PlanItem>();
        foreach (var item in items)
        {
            if (item.Kind is not ("overhead" or "ridge" or "hip" or "valley" or "break") || item.Points.Count < 2) { o.Add(item); continue; }
            var n = item.Points.Count;
            var kept = Enumerable.Range(0, item.Closed ? n : n - 1).Select(i => (A: item.Points[i], B: item.Points[(i + 1) % n]))
                .Select(s => (s.A, s.B, Kept: Kept(cuts, own, s.A, s.B))).ToList();
            if (kept.All(s => s.Kept is [(0, 1)])) { o.Add(item); continue; }
            foreach (var (a, b, intervals) in kept)
                foreach (var (t0, t1) in intervals)
                    if (t1 - t0 > 1e-9) o.Add(item with { Closed = false, Points = [a + (b - a) * t0, a + (b - a) * t1] });
        }
        return o;
    }

    /// <summary>The share of a line of the roof (segment a→b in plan) left by the cuts: 1 when none takes any of it.</summary>
    public static double ShareKept(Roof.Resolved r, RoofShape? shape, IReadOnlyList<Cut> cuts, Vec2 a, Vec2 b) =>
        cuts.Count == 0 ? 1 : Kept(cuts, Own(r, shape), a, b).Sum(x => x.B - x.A);

    /// <summary>The roof's own plan: its planes (its eave outline, flat).</summary>
    static List<Vec2[]> Own(Roof.Resolved r, RoofShape? shape) =>
        r.Flat || shape == null ? [Roof.EaveOutline(r)] : shape.Planes.Select(p => p.Points.Select(q => q.Xy).ToArray()).ToList();

    /// <summary>
    /// The parts of [0, 1] along a→b that stay: a line of the roof goes where the roof is gone on both its sides (cut, or
    /// not the roof: an eave whose plane is cut goes with it; a ridge goes only when both its planes are cut there).
    /// </summary>
    static List<(double A, double B)> Kept(IReadOnlyList<Cut> cuts, List<Vec2[]> own, Vec2 a, Vec2 b)
    {
        var d = b - a;
        if (d.Length < 1e-12) return [(0, 1)];
        var side = d.Normalized().Perp * 1e-4;
        var ts = new List<double> { 0, 1 };
        var scratch = new List<(double, double)>();
        foreach (var ring in cuts.Select(c => c.Region).Concat(own))
            foreach (var off in new[] { side, -side })
            {
                scratch.Clear();
                Polygon2.InsideIntervals([ring], a + off, b + off, 0, 1, scratch);
                foreach (var (t0, t1) in scratch) { ts.Add(t0); ts.Add(t1); }
            }
        ts.Sort();
        bool Gone(Vec2 p) => cuts.Any(c => Polygon2.Contains(c.Region, p)) || !own.Any(x => Polygon2.Contains(x, p));
        var o = new List<(double A, double B)>();
        for (var k = 0; k + 1 < ts.Count; k++)
        {
            var (t0, t1) = (ts[k], ts[k + 1]);
            if (t1 - t0 < 1e-12) continue;
            var mid = a + d * ((t0 + t1) / 2);
            if (Gone(mid + side) && Gone(mid - side)) continue;
            if (o.Count > 0 && Math.Abs(o[^1].B - t0) < 1e-12) o[^1] = (o[^1].A, t1); else o.Add((t0, t1));
        }
        return o;
    }

    /// <summary>The roof as convex pieces in plan, each with its underside and its top; null without a body.</summary>
    static Body? BodyOf(Roof.Resolved r, RoofShape? shape)
    {
        var pieces = new List<Piece>();
        if (r.Flat)
        {
            var bottom = new Lin(0, 0, r.BaseZ);
            foreach (var tri in Triangles(Roof.EaveOutline(r))) pieces.Add(new Piece(tri, bottom, bottom.Plus(r.Thickness), -1, 1));
        }
        else if (shape != null)
        {
            for (var i = 0; i < shape.Planes.Count; i++)
            {
                var pts = shape.Planes[i].Points;
                var n = VecMath.NewellNormal(pts);
                if (pts.Count < 3 || n.Length < 1e-12 || Math.Abs(n.Z) < 1e-6 * n.Length) continue;
                var cos = Math.Abs(n.Z) / n.Length;
                var o = pts[0];
                var bottom = new Lin(-n.X / n.Z, -n.Y / n.Z, o.Z + (n.X * o.X + n.Y * o.Y) / n.Z);
                foreach (var tri in Triangles(pts.Select(p => p.Xy).ToList())) pieces.Add(new Piece(tri, bottom, bottom.Plus(r.Thickness / cos), i, cos));
            }
        }
        if (pieces.Count == 0) return null;
        var all = pieces.SelectMany(p => p.Ring).ToList();
        return new Body(r.Roof, r.PlateZ, pieces, new Vec2(all.Min(p => p.X), all.Min(p => p.Y)), new Vec2(all.Max(p => p.X), all.Max(p => p.Y)));
    }

    static bool Overlap(Body me, IReadOnlyList<Vec2> eaves)
    {
        if (eaves.Count < 3) return false;
        var (x0, y0, x1, y1) = (eaves.Min(p => p.X), eaves.Min(p => p.Y), eaves.Max(p => p.X), eaves.Max(p => p.Y));
        return x0 < me.Max.X - Tol && x1 > me.Min.X + Tol && y0 < me.Max.Y - Tol && y1 > me.Min.Y + Tol;
    }

    /// <summary>A ring in plan as counter-clockwise triangles.</summary>
    static IEnumerable<Vec2[]> Triangles(IReadOnlyList<Vec2> ring)
    {
        var pts = ring.ToList();
        if (pts.Count > 1 && (pts[0] - pts[^1]).Length < 1e-9) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < 3) yield break;
        if (Polygon2.SignedArea(pts) < 0) pts.Reverse();
        var flat = pts.Select(p => new Vec3(p.X, p.Y, 0)).ToList();
        foreach (var (a, b, c) in Triangulator.Face(flat, Enumerable.Range(0, pts.Count).ToArray(), null, Vec3.UnitZ))
        {
            Vec2[] t = [pts[a], pts[b], pts[c]];
            var area = Polygon2.SignedArea(t);
            if (Math.Abs(area) < 1e-10) continue;
            yield return area > 0 ? t : [t[0], t[2], t[1]];
        }
    }

    /// <summary>Convex ring <paramref name="a"/> clipped by convex counter-clockwise ring <paramref name="b"/>.</summary>
    static List<Vec2> Convex(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b)
    {
        var o = a.ToList();
        for (var i = 0; i < b.Count && o.Count >= 3; i++)
        {
            var (p, q) = (b[i], b[(i + 1) % b.Count]);
            var e = q - p;
            o = Polygon2.ClipHalf(o, x => Vec2.Cross(e, x - p));
        }
        return o;
    }

    static bool Meets((Vec3 Min, Vec3 Max) a, (Vec3 Min, Vec3 Max) b) =>
        a.Min.X < b.Max.X && a.Max.X > b.Min.X && a.Min.Y < b.Max.Y && a.Max.Y > b.Min.Y && a.Min.Z < b.Max.Z && a.Max.Z > b.Min.Z;
}
