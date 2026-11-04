using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Coordination;

namespace Lapin.Mep;

/// <summary>
/// Runs that cut their walls and floors (decision 293, "the cut order should solve it from the start"): where a run's
/// centreline crosses a wall (straight or curved: decision 296), goes through a floor or through a roof's slope (decision 296),
/// a provision opening round it — round for a pipe or a
/// cable (and a round duct) square through the wall, rectangular for a duct with a height or a run crossing at a slant —
/// sized by the cut order's MEP rule (the run's outside with its insulation, a gas pipe's sleeve, grown by the clearance
/// and rounded up to the step). Worked out from the run each time its host is built, never stored: it moves and grows
/// with the run and goes when the run does. A run cuts when its system says so (<c>cutHosts</c>), else when the
/// project's rule does; a demolished run cuts nothing; where the run already passes through an opening of the host
/// none is cut (the check measures that one). A load-bearing host (unless the rule cuts those too) and a host the cut
/// order ranks over the run are not cut: held, and the clash check reports them.
/// <para>
/// A host's build reads only the runs near it (decision 296): their places in plan kept per model revision, the host's
/// cells noted (<see cref="Provisions.ReadNear"/>), so a run's edit builds again only the hosts it crossed or crosses.
/// </para>
/// </summary>
sealed class MepProvisions : IProvisionOpenings
{
    /// <summary>How far past the host's faces the cutter reaches, so no skin is left.</summary>
    const double Proud = 0.05;
    /// <summary>A crossing squarer than this (cos of the angle to the wall's normal) is square through: a round opening.</summary>
    const double Square = 0.985;
    const int CircleSides = 32;

    public IEnumerable<ProvisionOpening> Of(IModelView m, IServices services, Entity? host, double offset = 0)
    {
        if (host != null && host.Class is not (IfcClass.Wall or IfcClass.Slab or IfcClass.Roof)) return [];
        var rule = CutOrder.Mep(m);
        var systems = Systems.All(m).ToList();
        if (!rule.Cut && !systems.Any(s => s.Attrs["cutHosts"] is { } v && v.GetValueKind() == System.Text.Json.JsonValueKind.True)) return [];
        // A host's build reads the runs near it only (decision 296): the runs listed and placed without noting it, the host's cells noted.
        var raw = Provisions.Untracked(m);
        IEnumerable<Entity> near = Runs.All(raw);
        if (host != null)
        {
            if (PlanBox(m, host) is not { } box) return [];
            Provisions.ReadNear(m, (box.Min.At(0), box.Max.At(0)));
            var extents = Provisions.Building(m) ? ExtentsOf(raw) : null;
            near = near.Where(r => (extents != null ? extents.GetOrAdd(r.Id, _ => Extent(raw, r)) : Extent(raw, r)) is { } e
                && e.Min.X <= box.Max.X && box.Min.X <= e.Max.X && e.Min.Y <= box.Max.Y && box.Min.Y <= e.Max.Y);
        }
        // Read again through the model as given: what a near run's opening comes from is noted.
        var runs = near.Select(r => m.Get(r.Id)).OfType<Entity>().Where(r => r.Str("phaseDemolished") == null && Cuts(m, r, rule)).ToList();
        if (runs.Count == 0) return [];
        var walls = host != null ? (host.Class == IfcClass.Wall ? [host] : []) : m.OfClass(IfcClass.Wall).ToList();
        var slabs = host != null ? (host.Class == IfcClass.Slab ? [host] : []) : m.OfClass(IfcClass.Slab).ToList();
        var roofs = host != null ? (host.Class == IfcClass.Roof ? [host] : []) : m.OfClass(IfcClass.Roof).ToList();
        var roofBands = roofs.Select(r => (Roof: r, Bands: RoofBands(m, r))).Where(x => x.Bands.Count > 0).ToList();
        var cutters = services.Get<ICutters>();
        var o = new List<ProvisionOpening>();
        foreach (var run in runs)
        {
            var s = Runs.SettingOf(m, run);
            var line = Runs.Centreline(m, run, s, out _);
            if (line.Count < 2) continue;
            var (w, h, round) = Section(m, run, s);
            var lines = s.Pair ? new[] { ("supply", Runs.Shift(line, s.Separation / 2)), ("return", Runs.Shift(line, -s.Separation / 2)) } : [("pipe", line)];
            foreach (var wall in walls)
                foreach (var (name, pts) in lines)
                    o.AddRange(InWall(m, run, wall, name, pts, w, h, round, rule, offset));
            foreach (var slab in slabs)
                foreach (var (name, pts) in lines)
                    o.AddRange(InSlab(m, run, slab, name, pts, w, h, round, rule, offset));
            foreach (var (roof, bands) in roofBands)
                foreach (var (name, pts) in lines)
                    o.AddRange(InRoof(m, run, roof, bands, name, pts, w, h, round, rule, offset));
        }
        // Who wins: a host the cut order ranks over the run (or a load-bearing one, unless the rule cuts those) is held, not cut.
        return o.Select(p =>
        {
            var hostEntity = m.Get(p.HostId)!;
            var runEntity = m.Get(p.SourceId)!;
            if (cutters?.ClassPriority(runEntity.Class, m) is { } rp && cutters.PriorityOf(hostEntity, m) is { } hp && rp <= hp) return p with { Held = ProvisionOpening.Order };
            if (!rule.CutStructural && Provisions.LoadBearing(m, hostEntity)) return p with { Held = ProvisionOpening.Structural };
            return p;
        }).ToList();
    }

    /// <summary>Whether a run cuts: its system's own say (<c>cutHosts</c>), else the project's rule.</summary>
    static bool Cuts(IModelView m, Entity run, CutOrder.MepCut rule) =>
        Runs.SystemOf(m, run)?.Attrs["cutHosts"] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<bool>(out var own) ? own : rule.Cut;

    /// <summary>The runs' places in plan, kept per model (and revision) while hosts are built: each host filters every run by it.</summary>
    sealed record RunExtents(long Revision, ConcurrentDictionary<string, (Vec2 Min, Vec2 Max)?> Boxes);
    static readonly ConditionalWeakTable<IModelView, RunExtents> Kept = new();

    static ConcurrentDictionary<string, (Vec2 Min, Vec2 Max)?> ExtentsOf(IModelView raw)
    {
        lock (Kept)
        {
            if (Kept.TryGetValue(raw, out var k) && k.Revision == raw.Revision) return k.Boxes;
            var fresh = new RunExtents(raw.Revision, new());
            Kept.AddOrUpdate(raw, fresh);
            return fresh.Boxes;
        }
    }

    /// <summary>Where a run is in plan: its centreline (both pipes of a pair) grown by its outside, its insulation and a margin.</summary>
    static (Vec2 Min, Vec2 Max)? Extent(IModelView m, Entity run)
    {
        try
        {
            var s = Runs.SettingOf(m, run);
            var line = Runs.Centreline(m, run, s, out _);
            if (line.Count == 0) return null;
            var (w, h, _) = Section(m, run, s);
            var grow = Math.Max(w, h) / 2 + (s.Pair ? s.Separation / 2 : 0) + ProvisionCells.Margin;
            return (new Vec2(line.Min(p => p.X) - grow, line.Min(p => p.Y) - grow), new Vec2(line.Max(p => p.X) + grow, line.Max(p => p.Y) + grow));
        }
        catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException) { return null; }
    }

    /// <summary>A host's box in plan: a wall's band (curved or not), a floor's outline, a roof's eaves.</summary>
    static (Vec2 Min, Vec2 Max)? PlanBox(IModelView m, Entity host)
    {
        IReadOnlyList<Vec2> pts;
        if (host.Class == IfcClass.Wall)
        {
            var f = Openings.FrameOf(m, host);
            var c = PathCurve.Of(host);
            pts = c.Band(_ => 0, _ => c.Length, f.YMin, f.YMax);
        }
        else if (host.Class == IfcClass.Roof) pts = Roof.Resolve(host, m) is { } r ? Roof.EaveOutline(r) : [];
        else pts = Slabs.Contour(host);
        if (pts.Count == 0) return null;
        return (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y)));
    }

    /// <summary>The run's outside: width and height across it (its insulation round it; a gas pipe its sleeve), and whether it is round.</summary>
    static (double W, double H, bool Round) Section(IModelView m, Entity run, Runs.Setting s)
    {
        var insulation = Runs.SystemOf(m, run) is { } system ? Systems.Of(system).Insulation : 0;
        if (Services.IsAir(s.Service) && run.NumOrNull("height") is { } height) return (s.Size + 2 * insulation, height + 2 * insulation, false);
        var d = Services.IsFuel(s.Service) ? s.Size + 2 * Sleeves.Gap : s.Size + 2 * insulation;
        return (d, d, true);
    }

    static IEnumerable<ProvisionOpening> InWall(IModelView m, Entity run, Entity wall, string name, List<Vec3> line, double w, double h, bool round, CutOrder.MepCut rule, double offset)
    {
        var curve = PathCurve.Of(wall);
        if (Walls.Frame(wall, m) is null) yield break;
        var length = curve.Length;
        if (length < 1e-6) yield break;
        var straight = Openings.FrameOf(m, wall);
        var thickness = straight.YMax - straight.YMin;
        var z0 = straight.Base;
        var z1 = z0 + wall.Num("height", Walls.DefaultHeight);
        var openings = Openings.PlacedOn(m, wall.Id);
        var storey = Spatial.StoreyOf(m, wall.Id)?.Id;
        for (var i = 1; i < line.Count; i++)
        {
            Vec2 a = line[i - 1].Xy, b = line[i].Xy;
            var leg = b - a;
            var legLength = leg.Length;
            if (legLength < 1e-6) continue;
            var dir = leg * (1 / legLength);
            // Where the leg meets the wall's axis: the straight axis, or the arc's circle within its sweep (a curved wall, decision 296).
            var hits = AxisHits(curve, straight, a, leg);
            for (var hk = 0; hk < hits.Count; hk++)
            {
                var t = hits[hk];
                var hit = a + leg * t;
                var u = curve.IsArc ? curve.Project(hit) : Vec2.Dot(hit - straight.Start, straight.Dir);
                if (u < -1e-6 || u > length + 1e-6) continue;
                // The wall's frame where the leg meets it: its tangent there on an arc.
                var f = curve.IsArc ? Openings.FrameOf(m, wall, u) : straight;
                var across = Vec2.Dot(dir, f.Left);
                if (Math.Abs(across) < 0.05) continue;
                var z = line[i - 1].Z + (line[i].Z - line[i - 1].Z) * t;
                if (z < z0 || z > z1) continue;
                var zc = z - f.Base;
                // Already through an opening of the wall: that one serves it (the check measures whether it is big enough).
                if (openings.Any(op => Math.Abs(u - op.Along) <= op.Width / 2 && zc >= op.Sill && zc <= op.Sill + op.Height)) continue;
                // Seen along the wall: a slanting crossing is longer along it (the section's ellipse, the axis's travel through the thickness).
                var c = Math.Abs(across);
                var tan = Math.Sqrt(Math.Max(0, 1 - c * c)) / c;
                var slope = (line[i].Z - line[i - 1].Z) / legLength / c;
                var alongSize = w / c + thickness * tan;
                var upSize = h * Math.Sqrt(1 + slope * slope) + thickness * Math.Abs(slope);
                var isRound = round && c >= Square && Math.Abs(slope) < 0.05;
                var (ow, oh) = (rule.Size(alongSize, rule.Clearance) + 2 * offset, rule.Size(upSize, rule.Clearance) + 2 * offset);
                if (isRound) ow = oh = Math.Max(ow, oh);
                if (ow < 0.005 || oh < 0.005) continue;
                // A curved wall: the second time a leg meets the same arc is another opening.
                var key = $"{run.Id}#{wall.Id}#{name}#{i}" + (hk > 0 ? "b" : "");
                // A curved wall takes a rectangular one as a door or a window does: its band between radial sides (the width along
                // the axis); a round one square through it as in a straight wall, its ends past both faces where they bend away.
                var exact = curve.IsArc && !isRound ? ArcBody(curve, f, u, zc, ow, oh, f.YMin, f.YMax) : WallBody(f, u, zc, ow, oh, isRound, f.YMin, f.YMax);
                var cutter = curve.IsArc && !isRound ? ArcBody(curve, f, u, zc, ow, oh, f.YMin - Proud, f.YMax + Proud) : WallBody(f, u, zc, ow, oh, isRound, f.YMin - Proud, f.YMax + Proud);
                if (exact == null || cutter == null) continue;
                yield return new ProvisionOpening(key, run.Id, wall.Id, OpeningVoids.Wall, isRound ? OpeningVoids.Round : OpeningVoids.Rectangle, ow, oh, thickness, exact, cutter, storey) { Along = f.Dir };
            }
        }
    }

    /// <summary>Where a leg (from <paramref name="a"/>, <paramref name="leg"/> long) meets a wall's axis, as fractions of the leg: a straight axis once, an arc's circle up to twice.</summary>
    static List<double> AxisHits(PathCurve c, Openings.WallFrame f, Vec2 a, Vec2 leg)
    {
        var o = new List<double>();
        if (!c.IsArc)
        {
            var den = Vec2.Dot(leg, f.Left);
            if (Math.Abs(den) < 1e-12) return o;
            var t = Vec2.Dot(f.Start - a, f.Left) / den;
            if (t >= 0 && t <= 1) o.Add(t);
            return o;
        }
        // |a + t·leg − centre|² = r²
        var d = a - c.Centre;
        double qa = Vec2.Dot(leg, leg), qb = 2 * Vec2.Dot(d, leg), qc = Vec2.Dot(d, d) - c.Radius * c.Radius;
        var disc = qb * qb - 4 * qa * qc;
        if (qa < 1e-12 || disc < 0) return o;
        var root = Math.Sqrt(disc);
        foreach (var t in new[] { (-qb - root) / (2 * qa), (-qb + root) / (2 * qa) })
            if (t >= 0 && t <= 1 && c.Distance(a + leg * t) < 1e-4 && !o.Any(x => Math.Abs(x - t) < 1e-9)) o.Add(t);
        return o;
    }

    /// <summary>A rectangular opening in a curved wall: the wall's band between radial sides <paramref name="w"/> apart on the axis, between two offsets across it.</summary>
    static Solid? ArcBody(PathCurve c, Openings.WallFrame f, double u, double zc, double w, double h, double y0, double y1) =>
        Prism.ExtrudeFootprint(c.Band(_ => u - w / 2, _ => u + w / 2, y0, y1), f.Base + zc - h / 2, h, "provision");

    /// <summary>A wall opening's body between two offsets across the wall: a box, or a cylinder square through it.</summary>
    static Solid? WallBody(Openings.WallFrame f, double u, double zc, double w, double h, bool round, double y0, double y1)
    {
        if (round)
        {
            var r = w / 2 / Math.Cos(Math.PI / CircleSides);
            var outline = Enumerable.Range(0, CircleSides).Select(k => f.At(u + r * Math.Cos(2 * Math.PI * k / CircleSides), y0, zc + r * Math.Sin(2 * Math.PI * k / CircleSides))).ToList();
            return Prism.Extrude(outline, null, f.At(0, y1, 0) - f.At(0, y0, 0), "provision");
        }
        double x0 = u - w / 2, x1 = u + w / 2;
        Vec2[] footprint = [f.Start + f.Dir * x0 + f.Left * y0, f.Start + f.Dir * x1 + f.Left * y0, f.Start + f.Dir * x1 + f.Left * y1, f.Start + f.Dir * x0 + f.Left * y1];
        return Prism.ExtrudeFootprint(footprint, f.Base + zc - h / 2, h, "provision");
    }

    static IEnumerable<ProvisionOpening> InSlab(IModelView m, Entity run, Entity slab, string name, List<Vec3> line, double w, double h, bool round, CutOrder.MepCut rule, double offset)
    {
        var contour = Slabs.Contour(slab);
        var section = Slabs.Section(m, slab);
        if (contour.Count < 3 || section.Count == 0) yield break;
        var top = Slabs.Top(m, slab);
        var (zb, zt) = (top + section.Min(x => x.From), top + section.Max(x => x.To));
        var rings = new[] { Polygon2.EnsureCcw(contour).ToArray() };
        var holes = Slabs.Holes(m, slab.Id).Select(x => new[] { Polygon2.EnsureCcw(x.Contour).ToArray() }).ToList();
        var storey = Spatial.StoreyOf(m, slab.Id)?.Id;
        for (var i = 1; i < line.Count; i++)
        {
            Vec3 a = line[i - 1], b = line[i];
            var dz = b.Z - a.Z;
            if (Math.Abs(dz) < 1e-6 || Math.Max(a.Z, b.Z) <= zb + 1e-6 || Math.Min(a.Z, b.Z) >= zt - 1e-6) continue;
            Vec3 At(double z) => a + (b - a) * Math.Clamp((z - a.Z) / dz, 0, 1);
            var (pb, pt) = (At(zb), At(zt));
            var mid = ((pb + pt) * 0.5).Xy;
            if (!Polygon2.Inside(rings, mid) || holes.Any(hole => Polygon2.Inside(hole, mid))) continue;
            var travel = pt.Xy - pb.Xy;
            var vertical = travel.Length < 1e-4;
            if (vertical) travel = new Vec2(0, 0);
            var along = vertical ? new Vec2(1, 0) : travel.Normalized();
            var normal = new Vec2(-along.Y, along.X);
            var isRound = round && vertical;
            // The pipe square through the floor: its section; at a slant: its travel through the slab added along it.
            var (l, wd) = (rule.Size(travel.Length + w, rule.Clearance) + 2 * offset, rule.Size(h, rule.Clearance) + 2 * offset);
            if (vertical && !round) (l, wd) = (rule.Size(w, rule.Clearance) + 2 * offset, rule.Size(h, rule.Clearance) + 2 * offset);
            if (isRound) l = wd = Math.Max(l, wd);
            if (l < 0.005 || wd < 0.005) continue;
            IReadOnlyList<Vec2> footprint = isRound
                ? Enumerable.Range(0, CircleSides).Select(k => mid + new Vec2(Math.Cos(2 * Math.PI * k / CircleSides), Math.Sin(2 * Math.PI * k / CircleSides)) * (l / 2 / Math.Cos(Math.PI / CircleSides))).ToList()
                : [mid - along * (l / 2) - normal * (wd / 2), mid + along * (l / 2) - normal * (wd / 2), mid + along * (l / 2) + normal * (wd / 2), mid - along * (l / 2) + normal * (wd / 2)];
            if (Prism.ExtrudeFootprint(footprint, zb, zt - zb, "provision") is not { } exact || Prism.ExtrudeFootprint(footprint, zb - Proud, zt - zb + 2 * Proud, "provision") is not { } cutter) continue;
            yield return new ProvisionOpening($"{run.Id}#{slab.Id}#{name}#{i}", run.Id, slab.Id, OpeningVoids.Slab, isRound ? OpeningVoids.Round : OpeningVoids.Rectangle, l, wd, zt - zb, exact, cutter, storey);
        }
    }

    /// <summary>A roof's slope as a provision cuts it: its outline in plan, its underside's height over the plan (z = Z0 + G·p), how deep it is upright, and its thickness square to it.</summary>
    sealed record RoofBand(Vec2[] Outline, double Z0, Vec2 G, double Depth, double Thickness)
    {
        public double Z(Vec2 p) => Z0 + Vec2.Dot(G, p);
    }

    /// <summary>
    /// A roof's slopes (decision 296): each plane from its underside (the rafters' or the deck's) to the covering's top,
    /// upright as the roof builds them; a flat roof one level slab over its eaves. A vault is left out (its strips bend).
    /// </summary>
    static List<RoofBand> RoofBands(IModelView m, Entity roof)
    {
        var o = new List<RoofBand>();
        try
        {
            if (Roof.Resolve(roof, m) is not { } r || r.Barrel) return o;
            if (r.Flat)
            {
                o.Add(new RoofBand([.. Polygon2.EnsureCcw(Roof.EaveOutline(r))], r.BaseZ, new Vec2(0, 0), r.Thickness, r.Thickness));
                return o;
            }
            if (Roof.Solve(r).Shape is not { } shape) return o;
            foreach (var plane in shape.Planes)
            {
                if (plane.Points.Count < 3) continue;
                var z = RoofSolver.PlaneZ(plane.Points);
                var p0 = plane.Points[0].Xy;
                var g = new Vec2(z(p0 + new Vec2(1, 0)) - z(p0), z(p0 + new Vec2(0, 1)) - z(p0));
                var cos = Math.Cos(plane.Pitch * Math.PI / 180);
                if (cos < 0.05) continue;
                o.Add(new RoofBand([.. Polygon2.EnsureCcw(plane.Points.Select(q => q.Xy).ToList())], z(p0) - Vec2.Dot(g, p0), g, r.Thickness / cos, r.Thickness));
            }
        }
        catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException) { }
        return o;
    }

    /// <summary>
    /// The provisions a run cuts in a roof (decision 296): where a leg goes through a slope, as through a floor — round for
    /// a pipe standing upright, else the footprint drawn out along its travel between the slope's underside and its top —
    /// cut on the slope itself, from its underside up through its depth (upright), so a pitched plane is cut square to the plan.
    /// </summary>
    static IEnumerable<ProvisionOpening> InRoof(IModelView m, Entity run, Entity roof, List<RoofBand> bands, string name, List<Vec3> line, double w, double h, bool round, CutOrder.MepCut rule, double offset)
    {
        var storey = Spatial.StoreyOf(m, roof.Id)?.Id;
        for (var bi = 0; bi < bands.Count; bi++)
        {
            var band = bands[bi];
            for (var i = 1; i < line.Count; i++)
            {
                Vec3 a = line[i - 1], b = line[i];
                var leg = b - a;
                // Height over the underside along the leg: f(s) = f0 + k·s; the leg goes in at f = 0 and out at f = depth.
                var f0 = a.Z - band.Z(a.Xy);
                var k = leg.Z - Vec2.Dot(band.G, leg.Xy);
                if (Math.Abs(k) < 1e-9) continue;
                double s0 = -f0 / k, s1 = (band.Depth - f0) / k;
                if (Math.Max(s0, s1) <= 1e-6 || Math.Min(s0, s1) >= 1 - 1e-6) continue;
                Vec3 At(double s) => a + leg * Math.Clamp(s, 0, 1);
                var (pb, pt) = (At(s0), At(s1));
                var mid = ((pb + pt) * 0.5).Xy;
                if (!Polygon2.Contains(band.Outline, mid)) continue;
                var travel = pt.Xy - pb.Xy;
                var vertical = travel.Length < 1e-4;
                if (vertical) travel = new Vec2(0, 0);
                var along = vertical ? new Vec2(1, 0) : travel.Normalized();
                var normal = new Vec2(-along.Y, along.X);
                var isRound = round && vertical;
                var (l, wd) = (rule.Size(travel.Length + w, rule.Clearance) + 2 * offset, rule.Size(h, rule.Clearance) + 2 * offset);
                if (vertical && !round) (l, wd) = (rule.Size(w, rule.Clearance) + 2 * offset, rule.Size(h, rule.Clearance) + 2 * offset);
                if (isRound) l = wd = Math.Max(l, wd);
                if (l < 0.005 || wd < 0.005) continue;
                IReadOnlyList<Vec2> footprint = isRound
                    ? Enumerable.Range(0, CircleSides).Select(q => mid + new Vec2(Math.Cos(2 * Math.PI * q / CircleSides), Math.Sin(2 * Math.PI * q / CircleSides)) * (l / 2 / Math.Cos(Math.PI / CircleSides))).ToList()
                    : [mid - along * (l / 2) - normal * (wd / 2), mid + along * (l / 2) - normal * (wd / 2), mid + along * (l / 2) + normal * (wd / 2), mid - along * (l / 2) + normal * (wd / 2)];
                // On the slope: the footprint lifted onto the underside, pushed up through the depth (past both faces for the cutter).
                Vec3[] On(double lift) => [.. footprint.Select(q => q.At(band.Z(q) + lift))];
                if (Prism.Extrude(On(0), null, new Vec3(0, 0, band.Depth), "provision") is not { } exact
                    || Prism.Extrude(On(-Proud), null, new Vec3(0, 0, band.Depth + 2 * Proud), "provision") is not { } cutter) continue;
                yield return new ProvisionOpening($"{run.Id}#{roof.Id}#{name}#{i}#{bi}", run.Id, roof.Id, OpeningVoids.Roof, isRound ? OpeningVoids.Round : OpeningVoids.Rectangle, l, wd, band.Thickness, exact, cutter, storey);
            }
        }
    }
}
