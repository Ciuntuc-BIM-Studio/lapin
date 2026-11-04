using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Coordination;

namespace Lapin.Mep;

/// <summary>
/// Runs that cut their walls and floors (decision 293, "the cut order should solve it from the start"): where a run's
/// centreline crosses a straight wall or goes through a floor, a provision opening round it — round for a pipe or a
/// cable (and a round duct) square through the wall, rectangular for a duct with a height or a run crossing at a slant —
/// sized by the cut order's MEP rule (the run's outside with its insulation, a gas pipe's sleeve, grown by the clearance
/// and rounded up to the step). Worked out from the run each time its host is built, never stored: it moves and grows
/// with the run and goes when the run does. A run cuts when its system says so (<c>cutHosts</c>), else when the
/// project's rule does; a demolished run cuts nothing; where the run already passes through an opening of the host
/// none is cut (the check measures that one). A load-bearing host (unless the rule cuts those too) and a host the cut
/// order ranks over the run are not cut: held, and the clash check reports them.
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
        if (host != null && host.Class is not (IfcClass.Wall or IfcClass.Slab)) return [];
        var rule = CutOrder.Mep(m);
        var systems = Systems.All(m).ToList();
        if (!rule.Cut && !systems.Any(s => s.Attrs["cutHosts"] is { } v && v.GetValueKind() == System.Text.Json.JsonValueKind.True)) return [];
        var runs = Runs.All(m).Where(r => r.Str("phaseDemolished") == null && Cuts(m, r, rule)).ToList();
        if (runs.Count == 0) return [];
        var walls = host != null ? (host.Class == IfcClass.Wall ? [host] : []) : m.OfClass(IfcClass.Wall).ToList();
        var slabs = host != null ? (host.Class == IfcClass.Slab ? [host] : []) : m.OfClass(IfcClass.Slab).ToList();
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
        if (PathCurve.Of(wall) is { IsArc: true } || Walls.Frame(wall, m) is not { } jf) yield break;
        var f = Openings.FrameOf(m, wall);
        var length = (jf.End - jf.Start).Length;
        if (length < 1e-6) yield break;
        var thickness = f.YMax - f.YMin;
        var z0 = f.Base;
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
            var across = Vec2.Dot(dir, f.Left);
            if (Math.Abs(across) < 0.05) continue;
            // Where the leg meets the wall's axis (the frame's y = 0 line), and whether that lies within the leg, the wall and its height.
            var t = Vec2.Dot(f.Start - a, f.Left) / Vec2.Dot(leg, f.Left);
            if (t < 0 || t > 1) continue;
            var hit = a + leg * t;
            var u = Vec2.Dot(hit - f.Start, f.Dir);
            if (u < -1e-6 || u > length + 1e-6) continue;
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
            var key = $"{run.Id}#{wall.Id}#{name}#{i}";
            var exact = WallBody(f, u, zc, ow, oh, isRound, f.YMin, f.YMax);
            var cutter = WallBody(f, u, zc, ow, oh, isRound, f.YMin - Proud, f.YMax + Proud);
            if (exact == null || cutter == null) continue;
            yield return new ProvisionOpening(key, run.Id, wall.Id, OpeningVoids.Wall, isRound ? OpeningVoids.Round : OpeningVoids.Rectangle, ow, oh, thickness, exact, cutter, storey) { Along = f.Dir };
        }
    }

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
}
