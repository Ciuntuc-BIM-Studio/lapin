using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>A roof window or a dormer, kept on its roof (<c>openings</c>): where it sits in plan and its sizes (m, degrees).</summary>
public sealed record RoofOpening(string Id, string Kind, string Name, Vec2 At, double Width, double Length, string DormerShape, double WallHeight, double Pitch, double Overhang, double Thickness)
{
    public const string Window = "window", Dormer = "dormer";

    public static RoofOpening? From(JsonNode? n)
    {
        if (n is not JsonObject o || (string?)o["id"] is not { } id) return null;
        var kind = (string?)o["kind"] == Dormer ? Dormer : Window;
        var shed = (string?)o["dormerShape"] == "shed";
        return new RoofOpening(id, kind, (string?)o["name"] ?? (kind == Window ? "Roof window" : "Dormer"),
            new Vec2(Json.Number(o["x"]) ?? 0, Json.Number(o["y"]) ?? 0),
            Json.Number(o["width"]) ?? (kind == Window ? 0.78 : 1.6), Json.Number(o["length"]) ?? 1.18,
            shed ? "shed" : "gable", Json.Number(o["wallHeight"]) ?? 1.2, Json.Number(o["pitch"]) ?? (shed ? 15 : 40),
            Json.Number(o["overhang"]) ?? 0.2, Json.Number(o["thickness"]) ?? 0.15);
    }
}

/// <summary>
/// Openings a roof hosts (BubbleBIM Pro's, ported): roof windows (a hole in a
/// slope, a frame and a pane) and dormers (a front wall with its window, two
/// cheeks, their own small roof meeting the slope in exact valleys, and the
/// notch they cut). Each sits on the plane under its plan point; everything is
/// built in that plane's frame: along the eave (D) and up the slope (N, in plan).
/// </summary>
public static class RoofOpenings
{
    /// <summary>A plane's frame: z over the plan, the eave direction, the inward (up-slope) plan direction, its tangent.</summary>
    public sealed record Host(int PlaneIndex, RoofPlane Plane, Func<Vec2, double> Z, Vec2 D, Vec2 N, double Tan)
    {
        public Vec3 At(Vec2 c, double along, double up) { var p = c + D * along + N * up; return p.At(Z(p)); }
    }

    /// <summary>An opening placed on its plane: the hole it cuts (on the plane, counter-clockwise in plan).</summary>
    public sealed record Placed(RoofOpening Opening, Host Host, Vec2 Center, IReadOnlyList<Vec3> Hole);

    public static List<RoofOpening> Of(Entity roof) =>
        (roof.Attrs["openings"] as JsonArray)?.Select(RoofOpening.From).OfType<RoofOpening>().ToList() ?? [];

    public static Host? HostAt(RoofShape s, Vec2 p)
    {
        for (var i = 0; i < s.Planes.Count; i++)
        {
            var pl = s.Planes[i];
            if (!Polygon2.Contains(pl.Points.Select(q => q.Xy).ToList(), p)) continue;
            var a = s.Outline[pl.Edge]; var b = s.Outline[(pl.Edge + 1) % s.Outline.Count];
            var d = (b - a).Normalized();
            return new Host(i, pl, RoofSolver.PlaneZ(pl.Points), d, d.Perp, Math.Tan(pl.Pitch * Math.PI / 180));
        }
        return null;
    }

    /// <summary>The roof's openings that fit, each with its hole; the others are reported.</summary>
    public static List<Placed> Place(Entity roof, RoofShape s, IModelView m, List<string> diags)
    {
        var o = new List<Placed>();
        foreach (var n in Of(roof))
        {
            var host = HostAt(s, n.At);
            if (host == null) { diags.Add($"„{n.Name}” lies on no slope of the roof."); continue; }
            var hole = n.Kind == RoofOpening.Window ? WindowHole(n, host, n.At) : DormerShape(n, host, n.At)?.Notch;
            if (hole == null) { diags.Add($"„{n.Name}”: the dormer is taller than the slope it sits on."); continue; }
            var plan = host.Plane.Points.Select(q => q.Xy).ToList();
            var inset = Polygon2.Offset(Polygon2.EnsureCcw(plan).ToList(), plan.Select(_ => -0.15).ToArray());
            if (hole.Any(q => !Polygon2.Contains(inset, q.Xy))) { diags.Add($"„{n.Name}” does not fit on the slope (at least 150 mm from its edges)."); continue; }
            if (o.Any(x => x.Host.PlaneIndex == host.PlaneIndex && Overlap(x.Hole, hole))) { diags.Add($"„{n.Name}” overlaps another opening."); continue; }
            o.Add(new Placed(n, host, n.At, hole));
        }
        return o;
    }

    static bool Overlap(IReadOnlyList<Vec3> a, IReadOnlyList<Vec3> b)
    {
        var (pa, pb) = (a.Select(p => p.Xy).ToList(), b.Select(p => p.Xy).ToList());
        return pa.Any(p => Polygon2.Contains(pb, p)) || pb.Any(p => Polygon2.Contains(pa, p));
    }

    static double Area(IReadOnlyList<Vec3> p) => VecMath.NewellNormal(p).Length;

    // ─── Roof windows ──────────────────────────────────────────────────────

    /// <summary>The hole: width along the eave, length up the slope (measured on the slope), centred on the plan point.</summary>
    static Vec3[] WindowHole(RoofOpening n, Host h, Vec2 c)
    {
        var up = n.Length / 2 * Math.Cos(Math.Atan(h.Tan));
        return [h.At(c, -n.Width / 2, -up), h.At(c, n.Width / 2, -up), h.At(c, n.Width / 2, up), h.At(c, -n.Width / 2, up)];
    }

    /// <summary>A roof window: a frame standing on the covering round its hole, and its pane.</summary>
    public static (List<Solid> Frame, List<Solid> Glass, PlanItem Plan) WindowGeometry(Placed p, double coverTop)
    {
        var frameSolids = new List<Solid>();
        var glass = new List<Solid>();
        var nrm = VecMath.NewellNormal(p.Hole.ToList()).Normalized();
        if (nrm.Z < 0) nrm = -nrm;
        // Lifted vertically onto the covering, like the covering itself.
        var lift = new Vec3(0, 0, coverTop / nrm.Z);
        var ring = p.Hole.Select(q => q + lift).ToList();
        const double curb = 0.12, frame = 0.07;
        for (var i = 0; i < 4; i++)
        {
            var (a, b) = (ring[i], ring[(i + 1) % 4]);
            var inward = Vec3.Cross(nrm, b - a).Normalized();
            if (Prism.SweepBox(a + nrm * (curb / 2) + inward * (frame / 2), b + nrm * (curb / 2) + inward * (frame / 2), frame, curb, "frame") is { } bar) frameSolids.Add(bar);
        }
        var pane = ring.Select((q, i) => q + nrm * (curb * 0.8) + (ring[(i + 2) % 4] - q).Normalized() * frame).ToArray();
        if (Prism.Extrude(pane, null, nrm * 0.02, "glass") is { } g) glass.Add(g);
        return (frameSolids, glass, new PlanItem("overhead", true, p.Hole.Select(q => q.Xy).ToArray()));
    }

    // ─── Dormers ───────────────────────────────────────────────────────────

    /// <summary>A dormer resolved in its host plane's frame (u along the eave, s up the slope in plan, from the front wall).</summary>
    public sealed record DormerParts(Vec3[] Notch, List<Vec3[]> RoofPlanes, Vec3[] Front, Vec3[] CheekL, Vec3[] CheekR, Vec3[]? FrontWindow, double Thickness);

    public static DormerParts? DormerShape(RoofOpening n, Host h, Vec2 c)
    {
        var w = n.Width;
        var hw = n.WallHeight;           // the front wall above the slope
        var shed = n.DormerShape == "shed";
        var pd = n.Pitch * Math.PI / 180;
        var o = n.Overhang;
        var t = n.Thickness;
        var tanD = Math.Tan(pd);
        var z0 = h.Z(c);
        var ze = z0 + hw;
        Vec3 P(double u, double s, double z) { var q = c + h.D * u + h.N * s; return q.At(z); }
        var front = new List<Vec3> { P(-w / 2, 0, z0), P(w / 2, 0, z0) };
        // The slope reaches the dormer's eave height this far back:
        var se = hw / h.Tan;
        Vec3[] notch, cheekL, cheekR;
        var planes = new List<Vec3[]>();
        if (shed)
        {
            // One plane rising back from the front eave, flatter than the slope, until it meets it.
            if (tanD >= h.Tan - 1e-3) return null;
            var sm = (hw + o * tanD) / (h.Tan - tanD);
            var zm = z0 + sm * h.Tan;
            double Zs(double s) => ze + s * tanD;
            notch = [P(-w / 2, 0, z0), P(w / 2, 0, z0), P(w / 2, sm, zm), P(-w / 2, sm, zm)];
            planes.Add([P(-w / 2 - o, -o, Zs(-o)), P(w / 2 + o, -o, Zs(-o)), P(w / 2 + o, sm, zm), P(-w / 2 - o, sm, zm)]);
            front.AddRange([P(w / 2, 0, ze), P(-w / 2, 0, ze)]);
            cheekL = [P(-w / 2, 0, z0), P(-w / 2, sm, zm), P(-w / 2, 0, ze)];
            cheekR = [P(w / 2, 0, z0), P(w / 2, 0, ze), P(w / 2, sm, zm)];
        }
        else
        {
            // Two planes from eaves at the walls' tops to a ridge up the slope; each meets the slope in a valley.
            var zr = ze + w / 2 * tanD;
            var sr = (zr - z0) / h.Tan;
            var ze2 = ze - o * tanD;                               // the eave, o outside the cheek
            var se2 = (ze2 - z0) / h.Tan;
            if (se2 <= -o) return null;
            notch = [P(-w / 2, 0, z0), P(w / 2, 0, z0), P(w / 2, se, ze), P(0, sr, zr), P(-w / 2, se, ze)];
            planes.Add([P(-w / 2 - o, -o, ze2), P(0, -o, zr), P(0, sr, zr), P(-w / 2 - o, se2, ze2)]);
            planes.Add([P(w / 2 + o, -o, ze2), P(w / 2 + o, se2, ze2), P(0, sr, zr), P(0, -o, zr)]);
            front.AddRange([P(w / 2, 0, ze), P(0, 0, zr), P(-w / 2, 0, ze)]);
            cheekL = [P(-w / 2, 0, z0), P(-w / 2, se, ze), P(-w / 2, 0, ze)];
            cheekR = [P(w / 2, 0, z0), P(w / 2, 0, ze), P(w / 2, se, ze)];
        }
        // The front window: centred, 0.25 m in from the cheeks, from 0.15 m over the slope to 0.15 m under the eave.
        var ww = w - 0.5;
        Vec3[]? win = ww > 0.3 && hw > 0.6 ? [P(-ww / 2, 0, z0 + 0.15), P(ww / 2, 0, z0 + 0.15), P(ww / 2, 0, ze - 0.15), P(-ww / 2, 0, ze - 0.15)] : null;
        return new DormerParts(notch, planes, [.. front], cheekL, cheekR, win, t);
    }

    /// <summary>A dormer: its walls (the front with its window), its roof, its pane.</summary>
    public static (List<Solid> Walls, List<Solid> Roof, List<Solid> Glass, PlanItem Plan) DormerGeometry(Placed p, double coveringThickness)
    {
        var d = DormerShape(p.Opening, p.Host, p.Center)!;
        var walls = new List<Solid>();
        var roof = new List<Solid>();
        var glass = new List<Solid>();
        var back = new Vec3(p.Host.N.X, p.Host.N.Y, 0) * d.Thickness;
        var side = new Vec3(p.Host.D.X, p.Host.D.Y, 0) * d.Thickness;
        if (Prism.Extrude(d.Front, d.FrontWindow is { } fw ? [fw] : null, back, "dormer") is { } front) walls.Add(front);
        if (Prism.Extrude(d.CheekL, null, side, "dormer") is { } l) walls.Add(l);
        if (Prism.Extrude(d.CheekR, null, -side, "dormer") is { } r) walls.Add(r);
        foreach (var pl in d.RoofPlanes)
        {
            var cos = Math.Abs(VecMath.NewellNormal(pl).Normalized().Z);
            if (Prism.Extrude(pl, null, new Vec3(0, 0, (coveringThickness + 0.12) / cos), "dormer-roof") is { } s) roof.Add(s);
        }
        if (d.FrontWindow is { } win && Prism.Extrude(win.Select(q => q + back * 0.4).ToArray(), null, back * 0.2, "glass") is { } g) glass.Add(g);
        return (walls, roof, glass, new PlanItem("overhead", true, d.Notch.Select(q => q.Xy).ToArray()));
    }

    /// <summary>What the openings count and cover: roof windows by size; dormers' walls (less their windows) and their covering.</summary>
    public static IEnumerable<Quantity> Quantities(IReadOnlyList<Placed> placed)
    {
        var windows = placed.Where(p => p.Opening.Kind == RoofOpening.Window).ToList();
        var dormers = placed.Where(p => p.Opening.Kind == RoofOpening.Dormer).ToList();
        if (windows.Count > 0) yield return new Quantity("RoofWindows", windows.Count, "COUNT", "Roof windows");
        if (dormers.Count == 0) yield break;
        yield return new Quantity("Dormers", dormers.Count, "COUNT", "Dormers");
        var parts = dormers.Select(p => DormerShape(p.Opening, p.Host, p.Center)!).ToList();
        yield return new Quantity("DormerWallArea", parts.Sum(d => Area(d.Front) - (d.FrontWindow is { } fw ? Area(fw) : 0) + Area(d.CheekL) + Area(d.CheekR)), "AREA", "Dormer walls (front and cheeks)");
        yield return new Quantity("DormerRoofArea", parts.Sum(d => d.RoofPlanes.Sum(Area)), "AREA", "Dormer covering");
    }
}
