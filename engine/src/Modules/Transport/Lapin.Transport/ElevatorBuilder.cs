using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Transport;

/// <summary>One stop of a lift: its storey, its level, whether the lift serves it (its landing door there).</summary>
public sealed record Stop(Entity Storey, double Z, bool Served);

/// <summary>
/// A lift laid out (docs/plan-elevators.md). Its local frame stands at <c>at</c>, the middle of the shaft's clear
/// footprint, turned by <c>rotation</c>: x across the shaft, the doors on its front at −y. The shaft's clear size is the
/// car with the clearances its drive needs round it (the counterweight's or jack's zone at a side or behind, the doors'
/// sills and panels in front), widened when the landing doors' panels need more wall to slide along.
/// </summary>
public sealed class ElevatorLayout
{
    public required ElevatorSpec Spec { get; init; }
    public required Vec2 At { get; init; }
    public required double Rotation { get; init; }
    /// <summary>The shaft's clear width (x) and depth (y).</summary>
    public required double Wi { get; init; }
    public required double Di { get; init; }
    /// <summary>The shaft walls' thickness, the pit floor's and the cap's.</summary>
    public required double T { get; init; }
    public required double Tp { get; init; }
    public required double Tc { get; init; }
    /// <summary>The car's clear rectangle: its middle across, its front and back.</summary>
    public required double Cx { get; init; }
    public required double Cy0 { get; init; }
    public required double Cy1 { get; init; }
    /// <summary>The door's opening across (the car's and the landings').</summary>
    public required double Dx0 { get; init; }
    public required double Dx1 { get; init; }
    public required IReadOnlyList<Stop> Stops { get; init; }
    /// <summary>The material of the shaft's walls: the core of its wall type, else concrete (glass for a glass shaft).</summary>
    public required string? WallMaterial { get; init; }

    public double ZB => Stops[0].Z;
    public double ZT => Stops[^1].Z;
    public double Travel => ZT - ZB;
    public double ZPit => ZB - Spec.Pit;
    /// <summary>The underside of the shaft's cap: the headroom over the top stop.</summary>
    public double ZHead => ZT + Spec.Headroom;
    public const double MachineRoomHeight = 2.4;
    /// <summary>The highest point of the lift: its cap, or its machine room's roof.</summary>
    public double ZTop => ZHead + Tc + (Spec.MachineRoom == "top" ? MachineRoomHeight + 0.2 : 0);
    public double ZBottom => ZPit - Tp;
    public double CabinW => Spec.CabinWidth;
    public double OuterW => Wi + 2 * T;
    public double OuterD => Di + 2 * T;

    double Cos => Math.Cos(Rotation * Math.PI / 180);
    double Sin => Math.Sin(Rotation * Math.PI / 180);
    /// <summary>A local point in the plan.</summary>
    public Vec2 W(double x, double y) => new(At.X + Cos * x - Sin * y, At.Y + Sin * x + Cos * y);
    public Vec2[] Rect(double x0, double y0, double x1, double y1) => [W(x0, y0), W(x1, y0), W(x1, y1), W(x0, y1)];

    /// <summary>The shaft's outer footprint (counter-clockwise): what the floors it passes through open for.</summary>
    public Vec2[] Outer => Rect(-Wi / 2 - T, -Di / 2 - T, Wi / 2 + T, Di / 2 + T);
    public Vec2[] Inner => Rect(-Wi / 2, -Di / 2, Wi / 2, Di / 2);

    /// <summary>The counterweight (or jack) in the shaft: its rectangle, local.</summary>
    public (double X0, double Y0, double X1, double Y1) Counterweight
    {
        get
        {
            var cyc = (Cy0 + Cy1) / 2;
            var (_, zone, _, _) = Spec.Clearances;
            var thick = Math.Min(0.14, zone * 0.5);
            switch (Spec.Counterweight)
            {
                case "back":
                {
                    var len = Math.Min(0.9, CabinW * 0.6);
                    var y1 = Di / 2 - 0.06;
                    return (Cx - len / 2, y1 - thick, Cx + len / 2, y1);
                }
                case "right":
                {
                    var len = Math.Min(0.9, Spec.CabinDepth * 0.6);
                    var x1 = Wi / 2 - 0.06;
                    return (x1 - thick, cyc - len / 2, x1, cyc + len / 2);
                }
                default:
                {
                    var len = Math.Min(0.9, Spec.CabinDepth * 0.6);
                    var x0 = -Wi / 2 + 0.06;
                    return (x0, cyc - len / 2, x0 + thick, cyc + len / 2);
                }
            }
        }
    }

    /// <summary>A hydraulic lift's power unit room beside the shaft at the bottom stop (clear 1.6 × 2.0 m, walls 150 mm), on the jack's side.</summary>
    public (double X0, double Y0, double X1, double Y1)? BesideRoom
    {
        get
        {
            if (Spec.MachineRoom != "beside") return null;
            const double w = 1.6, d = 2.0, t = 0.15;
            var left = Spec.Counterweight == "left";
            var x0 = left ? -Wi / 2 - T - w - 2 * t : Wi / 2 + T;
            return (x0, -Di / 2 - T, x0 + w + 2 * t, -Di / 2 - T + d + 2 * t);
        }
    }

    public static ElevatorLayout? Resolve(IModelView m, Entity e, out string? problem)
    {
        problem = null;
        var spec = new ElevatorSpec(e.Attrs);
        if (Args.TryPoint(e.Attrs["at"]) is not { } at) { problem = "The elevator has no place."; return null; }
        var bottom = Spatial.StoreyOf(m, e.Id);
        if (bottom == null) { problem = "The elevator stands on no storey."; return null; }
        var top = e.Str("topStoreyId") is { } tid && m.Get(tid) is { Class: IfcClass.BuildingStorey } ts ? ts : bottom;
        var (zb, zt) = (bottom.Num("elevation"), top.Num("elevation"));
        if (zt < zb) (zb, zt) = (zt, zb);
        var unserved = (e.Attrs["unserved"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        var stops = Spatial.Storeys(m).Where(s => s.Num("elevation") >= zb - 1e-6 && s.Num("elevation") <= zt + 1e-6)
            .Select(s => new Stop(s, s.Num("elevation"), !unserved.Contains(s.Id))).ToList();
        if (stops.Count == 0) { problem = "The elevator has no stops."; return null; }
        // The lowest and the highest stop are always served: a lift travels to them.
        stops[0] = stops[0] with { Served = true };
        stops[^1] = stops[^1] with { Served = true };

        // The shaft's walls: its wall type's whole build-up with its core's material; else the thickness asked for.
        double t;
        string? material = null;
        if (spec.ShaftGlass) { t = Math.Min(spec.ShaftThickness, 0.08); material = Materials.Find(m, TransportModule.GlassMaterial)?.Id; }
        else if (spec.ShaftWallType is { } wt && m.Get(wt) is { } type && WallTypes.LayersOf(m, type.Id) is { Count: > 0 } layers)
        {
            t = layers.Sum(l => l.Thickness);
            material = (layers.FirstOrDefault(l => l.Core) ?? layers.OrderByDescending(l => l.Thickness).First()).Material;
        }
        else { t = spec.ShaftThickness; material = Materials.Find(m, TransportModule.ShaftMaterial)?.Id; }
        t = Math.Max(0.03, t);

        var (side, zone, front, back) = spec.Clearances;
        var cw = spec.Counterweight;
        if (spec.Through) back = front;
        var w = spec.CabinWidth + (cw == "back" ? 2 * side : side + zone);
        var d = spec.CabinDepth + front + (cw == "back" ? zone : back);
        var wi = Math.Max(w, spec.DoorWallNeed + 0.1);
        // The car between its clearances, in the middle of what the doors widened.
        var (xa, xb) = (cw == "left" ? -wi / 2 + zone : -wi / 2 + side, cw == "right" ? wi / 2 - zone : wi / 2 - side);
        var cx = (xa + xb) / 2;
        var cy0 = -d / 2 + front;
        var dw = Math.Min(spec.DoorWidth, spec.CabinWidth);
        return new ElevatorLayout
        {
            Spec = spec, At = at, Rotation = e.Num("rotation"), Wi = wi, Di = d, T = t, Tp = Math.Max(0.2, Math.Min(t, 0.3)), Tc = Math.Max(0.15, Math.Min(t, 0.25)),
            Cx = cx, Cy0 = cy0, Cy1 = cy0 + spec.CabinDepth, Dx0 = cx - dw / 2, Dx1 = cx + dw / 2, Stops = stops, WallMaterial = material,
        };
    }

    // ---- Bodies ----

    /// <summary>A box of the local frame, or none when it has no size.</summary>
    Solid? Box(double x0, double y0, double x1, double y1, double z0, double z1, string tag)
    {
        if (x1 - x0 < 1e-6 || y1 - y0 < 1e-6 || z1 - z0 < 1e-6) return null;
        return Prism.ExtrudeFootprint(Rect(x0, y0, x1, y1), z0, z1 - z0, tag);
    }

    /// <summary>A wall along x (its y fixed) with holes across [hx0, hx1] at the given heights: its piers full height, the strip between them in pieces round the holes.</summary>
    void WallAlongX(List<Solid> into, double x0, double x1, double y0, double y1, double z0, double z1, double hx0, double hx1, IReadOnlyList<(double Z0, double Z1)> holes, string tag)
    {
        if (holes.Count == 0) { Add(into, Box(x0, y0, x1, y1, z0, z1, tag)); return; }
        Add(into, Box(x0, y0, hx0, y1, z0, z1, tag));
        Add(into, Box(hx1, y0, x1, y1, z0, z1, tag));
        var z = z0;
        foreach (var (h0, h1) in holes.OrderBy(h => h.Z0))
        {
            Add(into, Box(hx0, y0, hx1, y1, z, Math.Max(z, h0), tag));
            z = Math.Max(z, h1);
        }
        Add(into, Box(hx0, y0, hx1, y1, z, z1, tag));
    }

    static void Add(List<Solid> into, Solid? s) { if (s != null) into.Add(s); }

    /// <summary>The landing doors' panels closed in the shaft's front (or back) wall at a stop: a centre door's halves, a side door's panels telescoped one behind the other.</summary>
    void DoorPanels(List<Solid> into, double z, bool back, double inset, string tag)
    {
        var s = Spec;
        var h = s.DoorHeight;
        var n = s.DoorPanels;
        const double thick = 0.03, step = 0.035;
        double Y(int i) => back ? inset - i * step : inset + i * step;
        if (s.DoorOpening == "centre")
        {
            var mid = (Dx0 + Dx1) / 2;
            var per = n / 2;
            for (var i = 0; i < per; i++)
            {
                var y = Y(i);
                var (ya, yb) = back ? (y - thick, y) : (y, y + thick);
                var share = (mid - Dx0) / per;
                Add(into, Box(Dx0 + i * share, ya, Dx0 + (i + 1) * share - 0.003, yb, z, z + h, tag));
                Add(into, Box(Dx1 - (i + 1) * share + 0.003, ya, Dx1 - i * share, yb, z, z + h, tag));
            }
            return;
        }
        var width = (Dx1 - Dx0) / n;
        for (var i = 0; i < n; i++)
        {
            var y = Y(i);
            var (ya, yb) = back ? (y - thick, y) : (y, y + thick);
            Add(into, Box(Dx0 + i * width, ya, Dx0 + (i + 1) * width + (i < n - 1 ? 0.02 : 0), yb, z, z + h, tag));
        }
    }

    /// <summary>Every part of the lift: the shaft (its pit floor, walls with the landings' openings, cap), the machine room, the car at the lowest stop with its doors, the landing doors, the counterweight or jack.</summary>
    public List<(string Key, string Name, List<Solid> Solids, string Role)> Bodies()
    {
        var s = Spec;
        var o = new List<(string, string, List<Solid>, string)>();
        var (x0, x1, y0, y1) = (-Wi / 2, Wi / 2, -Di / 2, Di / 2);
        var (z0, z1) = (ZPit, ZHead);
        var served = Stops.Where(x => x.Served).ToList();
        var holes = served.Select(x => (x.Z, x.Z + s.DoorHeight)).ToList();

        var shaft = new List<Solid>();
        // Its pit floor under the walls, its walls up to the cap, the cap over them.
        Add(shaft, Box(x0 - T, y0 - T, x1 + T, y1 + T, ZPit - Tp, ZPit, "shaft"));
        WallAlongX(shaft, x0 - T, x1 + T, y0 - T, y0, z0, z1, Dx0, Dx1, holes, "shaft");
        WallAlongX(shaft, x0 - T, x1 + T, y1, y1 + T, z0, z1, Dx0, Dx1, s.Through ? holes : [], "shaft");
        Add(shaft, Box(x0 - T, y0, x0, y1, z0, z1, "shaft"));
        Add(shaft, Box(x1, y0, x1 + T, y1, z0, z1, "shaft"));
        Add(shaft, Box(x0 - T, y0 - T, x1 + T, y1 + T, ZHead, ZHead + Tc, "shaft"));
        o.Add(("shaft", "Shaft", shaft, "shaft"));

        var machine = new List<Solid>();
        if (s.MachineRoom == "top")
        {
            // On the cap: its walls on the shaft's, its roof; the door in its front wall.
            const double mt = 0.2;
            var (b0, b1) = (ZHead + Tc, ZHead + Tc + MachineRoomHeight);
            var (mx0, mx1, my0, my1) = (x0 - T, x1 + T, y0 - T, y1 + T);
            WallAlongX(machine, mx0, mx1, my0, my0 + mt, b0, b1, -0.45, 0.45, [(b0, b0 + 2.0)], "machine");
            Add(machine, Box(mx0, my1 - mt, mx1, my1, b0, b1, "machine"));
            Add(machine, Box(mx0, my0 + mt, mx0 + mt, my1 - mt, b0, b1, "machine"));
            Add(machine, Box(mx1 - mt, my0 + mt, mx1, my1 - mt, b0, b1, "machine"));
            Add(machine, Box(mx0, my0, mx1, my1, b1, b1 + 0.2, "machine"));
        }
        else if (BesideRoom is { } r)
        {
            const double mt = 0.15;
            var (b0, b1) = (ZB, ZB + MachineRoomHeight);
            WallAlongX(machine, r.X0, r.X1, r.Y0, r.Y0 + mt, b0, b1, (r.X0 + r.X1) / 2 - 0.45, (r.X0 + r.X1) / 2 + 0.45, [(b0, b0 + 2.0)], "machine");
            Add(machine, Box(r.X0, r.Y1 - mt, r.X1, r.Y1, b0, b1, "machine"));
            Add(machine, Box(r.X0, r.Y0 + mt, r.X0 + mt, r.Y1 - mt, b0, b1, "machine"));
            Add(machine, Box(r.X1 - mt, r.Y0 + mt, r.X1, r.Y1 - mt, b0, b1, "machine"));
            Add(machine, Box(r.X0, r.Y0, r.X1, r.Y1, b1, b1 + 0.15, "machine"));
        }
        if (machine.Count > 0) o.Add(("machineRoom", "Machine room", machine, "shaft"));

        // The car at the lowest stop: its floor, its walls (glass at the sides and back of a panoramic car), its roof.
        const double cwall = 0.04, slab = 0.1;
        var (ca, cb) = (Cx - CabinW / 2, Cx + CabinW / 2);
        var zb = ZB;
        var ch = s.CabinHeight;
        var car = new List<Solid>();
        var glass = new List<Solid>();
        Add(car, Box(ca - cwall, Cy0 - cwall, cb + cwall, Cy1 + cwall, zb - slab, zb, "cabin"));
        Add(car, Box(ca - cwall, Cy0 - cwall, cb + cwall, Cy1 + cwall, zb + ch, zb + ch + slab, "cabin"));
        WallAlongX(car, ca - cwall, cb + cwall, Cy0 - cwall, Cy0, zb, zb + ch, Dx0, Dx1, [(zb, zb + s.DoorHeight)], "cabin");
        var sides = s.Glass ? glass : car;
        WallAlongX(s.Through ? car : sides, ca - cwall, cb + cwall, Cy1, Cy1 + cwall, zb, zb + ch, Dx0, Dx1, s.Through ? [(zb, zb + s.DoorHeight)] : [], "cabin");
        Add(sides, Box(ca - cwall, Cy0, ca, Cy1, zb, zb + ch, "cabin"));
        Add(sides, Box(cb, Cy0, cb + cwall, Cy1, zb, zb + ch, "cabin"));
        o.Add(("cabin", "Car", car, "cabin"));
        if (glass.Count > 0) o.Add(("cabinGlass", "Car glass", glass, "glass"));

        var carDoors = new List<Solid>();
        DoorPanels(carDoors, zb, false, Cy0 - cwall - 0.045, "carDoor");
        if (s.Through) DoorPanels(carDoors, zb, true, Cy1 + cwall + 0.045, "carDoor");
        o.Add(("carDoors", "Car doors", carDoors, "doors"));

        var landing = new List<Solid>();
        foreach (var stop in served)
        {
            DoorPanels(landing, stop.Z, false, y0 + 0.005, "landingDoor");
            if (s.Through) DoorPanels(landing, stop.Z, true, y1 - 0.005, "landingDoor");
        }
        o.Add(("landingDoors", "Landing doors", landing, "doors"));

        var weight = new List<Solid>();
        var (wx0, wy0, wx1, wy1) = Counterweight;
        if (s.Drive == "hydraulic")
        {
            // The jack: a ram from the pit floor up to the car's floor.
            var (jx, jy) = ((wx0 + wx1) / 2, (wy0 + wy1) / 2);
            var c = W(jx, jy);
            Add(weight, Prism.Cylinder(c, ZPit, 0.08, zb - ZPit + 0.4, 12, "jack"));
        }
        else
        {
            // The counterweight hangs high while the car stands at the bottom.
            var h = Math.Clamp(Travel * 0.3, 1.0, 2.5);
            Add(weight, Box(wx0, wy0, wx1, wy1, ZHead - 0.4 - h, ZHead - 0.4, "counterweight"));
        }
        o.Add(("counterweight", s.Drive == "hydraulic" ? "Jack" : "Counterweight", weight, "metal"));
        return o.Where(x => x.Item3.Count > 0).ToList();
    }

    // ---- The plan ----

    /// <summary>
    /// The lift's symbols on a storey: on every stop the car with its diagonal cross, the counterweight with its diagonal
    /// (a hydraulic lift's jack as a circle); on a served stop the landing door's panels in the shaft wall and the car's
    /// door, dashed where the panels slide to. None on a storey the shaft only passes.
    /// </summary>
    public List<PlanItem> PlanOn(string storeyId)
    {
        var o = new List<PlanItem>();
        var stop = Stops.FirstOrDefault(x => x.Storey.Id == storeyId);
        if (stop == null) return o;
        var s = Spec;
        const double cwall = 0.04;
        var (ca, cb) = (Cx - CabinW / 2 - cwall, Cx + CabinW / 2 + cwall);
        var (cy0, cy1) = (Cy0 - cwall, Cy1 + cwall);
        o.Add(new PlanItem("symbol", true, Rect(ca, cy0, cb, cy1)));
        o.Add(new PlanItem("symbol", false, [W(ca, cy0), W(cb, cy1)]));
        o.Add(new PlanItem("symbol", false, [W(cb, cy0), W(ca, cy1)]));
        var (wx0, wy0, wx1, wy1) = Counterweight;
        if (s.Drive == "hydraulic")
        {
            var (jx, jy) = ((wx0 + wx1) / 2, (wy0 + wy1) / 2);
            o.Add(new PlanItem("symbol", true, Enumerable.Range(0, 16).Select(i => W(jx + 0.08 * Math.Cos(i * Math.PI / 8), jy + 0.08 * Math.Sin(i * Math.PI / 8))).ToList()));
        }
        else
        {
            o.Add(new PlanItem("symbol", true, Rect(wx0, wy0, wx1, wy1)));
            o.Add(new PlanItem("symbol", false, [W(wx0, wy0), W(wx1, wy1)]));
        }
        if (!stop.Served) return o;
        void Doors(bool back)
        {
            var y = back ? Di / 2 : -Di / 2;
            var dir = back ? -1 : 1;
            const double thick = 0.03, step = 0.035;
            var n = s.DoorPanels;
            // The landing door's panels just inside the shaft, the car door's in front of the car.
            foreach (var (baseY, sign) in new[] { (y + dir * 0.005, dir), (back ? cy1 + 0.045 : cy0 - 0.045, dir) })
            {
                if (s.DoorOpening == "centre")
                {
                    var mid = (Dx0 + Dx1) / 2;
                    for (var i = 0; i < n / 2; i++)
                    {
                        var ya = baseY + sign * i * step;
                        var yb = ya + sign * thick;
                        var share = (mid - Dx0) / (n / 2);
                        o.Add(new PlanItem("symbol", true, Rect(Dx0 + i * share, Math.Min(ya, yb), Dx0 + (i + 1) * share, Math.Max(ya, yb))));
                        o.Add(new PlanItem("symbol", true, Rect(Dx1 - (i + 1) * share, Math.Min(ya, yb), Dx1 - i * share, Math.Max(ya, yb))));
                    }
                }
                else
                {
                    var width = (Dx1 - Dx0) / n;
                    for (var i = 0; i < n; i++)
                    {
                        var ya = baseY + sign * i * step;
                        var yb = ya + sign * thick;
                        o.Add(new PlanItem("symbol", true, Rect(Dx0 + i * width, Math.Min(ya, yb), Dx0 + (i + 1) * width, Math.Max(ya, yb))));
                    }
                }
            }
            // Where the panels go when open: dashed beside the opening.
            var depth = n * step + thick;
            var (ly0, ly1) = back ? (y - 0.005 - depth, y - 0.005) : (y + 0.005, y + 0.005 + depth);
            if (s.DoorOpening == "centre")
            {
                o.Add(new PlanItem("dashed", true, Rect(Dx0 - (Dx1 - Dx0) / 2, ly0, Dx0, ly1)));
                o.Add(new PlanItem("dashed", true, Rect(Dx1, ly0, Dx1 + (Dx1 - Dx0) / 2, ly1)));
            }
            else o.Add(new PlanItem("dashed", true, Rect(Dx1, ly0, Dx1 + (Dx1 - Dx0) / n, ly1)));
        }
        Doors(false);
        if (s.Through) Doors(true);
        return o;
    }

    /// <summary>Whether a plan of a storey at <paramref name="level"/> sees the lift: its cut (a metre or so over the level) within the lift's height.</summary>
    public bool Crosses(double level) => level + 1.0 > ZBottom && level < ZTop - 0.2;
}
