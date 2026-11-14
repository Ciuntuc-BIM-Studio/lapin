using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Transport;

/// <summary>
/// A circular pneumatic vacuum lift (tester's report #149, docs/plan-elevators.md): no shaft and no pit, a transparent
/// tube (polycarbonate or glass) standing on a base ring on the lowest floor, rising through the floors above in ring
/// beams that fill their openings, a head unit (the turbines) on top, frame columns round it, and at each landing a door
/// (curved sliding, swing or none) facing its own angle, with its own frame. The cabin is a disc floor and roof with
/// clear curved walls and a curved sliding door, or none. Its frame: <c>Wi</c> = <c>Di</c> the tube's outside diameter,
/// <c>T</c> how far the floors' openings reach out of the tube, <c>Tc</c> the head unit's height. Every curve is made by
/// <see cref="Chords"/>; every piece is a closed prism.
/// </summary>
public sealed partial class ElevatorLayout
{
    /// <summary>The tube's outside radius.</summary>
    public double TubeR => Wi / 2;
    public double CabinR => Spec.CabinDiameter / 2;
    /// <summary>The cabin's floor at the lowest stop: on the base ring.</summary>
    public double ZCabin => ZB + Spec.BaseRingHeight;
    /// <summary>The landing door's height: no higher than the cabin.</summary>
    public double DoorH => Math.Min(Spec.DoorHeight, Spec.CabinHeight);

    /// <summary>A door's angle (0° the front, −y local; 90° the right, +x) as a local polar angle (radians, from +x).</summary>
    public static double Phi(double angleDeg) => (angleDeg - 90) * Math.PI / 180;

    Vec2 Polar(double r, double phi) => W(r * Math.Cos(phi), r * Math.Sin(phi));

    Vec2[] CirclePts(double r)
    {
        var n = Chords.Circle(r);
        return [.. Enumerable.Range(0, n).Select(i => Polar(r, 2 * Math.PI * i / n))];
    }

    List<Vec2> ArcPts(double r, double f0, double f1)
    {
        var n = Chords.Count(r, f1 - f0);
        return [.. Enumerable.Range(0, n + 1).Select(i => Polar(r, f0 + (f1 - f0) * i / n))];
    }

    /// <summary>A ring's sector between two radii and two angles, as a plan polygon (a pie when <paramref name="r0"/> is 0).</summary>
    List<Vec2> SectorPts(double r0, double r1, double f0, double f1)
    {
        var pts = ArcPts(r1, f0, f1);
        if (r0 < 1e-6) pts.Add(W(0, 0));
        else { var inner = ArcPts(r0, f0, f1); inner.Reverse(); pts.AddRange(inner); }
        return pts;
    }

    static ElevatorLayout ResolveVacuum(IModelView m, ElevatorSpec spec, Vec2 at, double rotation, List<Stop> stops)
    {
        var tube = spec.TubeDiameter;
        var rc = spec.CabinDiameter / 2;
        var dw = Math.Min(spec.DoorWidth, tube * 0.9);
        var material = Materials.Find(m, spec.TubeMaterial == "glass" ? TransportModule.GlassMaterial : TransportModule.PolycarbonateMaterial)?.Id;
        return new ElevatorLayout
        {
            Spec = spec, At = at, Rotation = rotation, Wi = tube, Di = tube, T = spec.OpeningMargin, Tp = 0, Tc = spec.HeadUnitHeight,
            Cx = 0, Cy0 = -rc, Cy1 = rc, Dx0 = -dw / 2, Dx1 = dw / 2, Stops = stops, WallMaterial = material,
        };
    }

    /// <summary>Half the angle a door's opening (a chord of its width) takes on a circle of <paramref name="r"/>.</summary>
    public double HalfArc(double r) => Math.Asin(Math.Min(0.95, (Dx1 - Dx0) / 2 / r));

    /// <summary>The served stops: the storey, the angle its door faces (local, radians), its sill (the lowest on the base ring).</summary>
    public List<(Stop Stop, double Phi, double Z0)> Landings() =>
        [.. Stops.Select((st, i) => (st, i)).Where(x => x.st.Served).Select(x => (x.st, Phi(Spec.LandingAngle(x.st.Storey.Id)), x.i == 0 ? ZCabin : x.st.Z))];

    /// <summary>The arcs of a circle left closed round the open ones (centre, half angle), each (from, to) with to &gt; from.</summary>
    public static List<(double A, double B)> ClosedArcs(IEnumerable<(double C, double H)> open)
    {
        const double full = 2 * Math.PI;
        var arcs = open.Where(o => o.H > 1e-9).Select(o => { var s = ((o.C - o.H) % full + full) % full; return (S: s, E: s + 2 * o.H); }).OrderBy(x => x.S).ToList();
        if (arcs.Count == 0) return [(0, full)];
        var merged = new List<(double S, double E)>();
        foreach (var (s0, e0) in arcs)
        {
            if (merged.Count > 0 && s0 <= merged[^1].E + 1e-9) merged[^1] = (merged[^1].S, Math.Max(merged[^1].E, e0));
            else merged.Add((s0, e0));
        }
        var o = new List<(double, double)>();
        for (var i = 0; i < merged.Count; i++)
        {
            var from = merged[i].E;
            var to = i + 1 < merged.Count ? merged[i + 1].S : merged[0].S + full;
            if (to - from > 1e-6) o.Add((from, to));
        }
        return o;
    }

    static double Between(double a, double b) => Math.Abs((((a - b) % (2 * Math.PI)) + 3 * Math.PI) % (2 * Math.PI) - Math.PI);

    /// <summary>
    /// The frame's columns: two flank the lowest door just outside its frame, the others evenly round the rest of the
    /// tube (one alone stands at the back); a column that would stand in another landing's door is left out.
    /// </summary>
    public (List<double> Kept, int Skipped) ColumnAngles()
    {
        var n = Spec.Columns;
        var kept = new List<double>();
        if (n == 0) return (kept, 0);
        var start = Phi(Spec.LandingAngle(Stops[0].Storey.Id));
        var clear = HalfArc(TubeR) + (Spec.DoorFrame + Spec.ColumnSize / 2 + 0.01) / TubeR;
        var landings = Landings();
        var skipped = 0;
        for (var k = 0; k < n; k++)
        {
            var phi = n == 1 ? start + Math.PI : start + clear + k * (2 * Math.PI - 2 * clear) / (n - 1);
            if (landings.Any(l => Between(phi, l.Phi) < clear - 1e-6)) skipped++; else kept.Add(phi);
        }
        return (kept, skipped);
    }

    /// <summary>A column's square footprint, outside the tube and facing its middle.</summary>
    public Vec2[] ColumnPts(double phi)
    {
        var c = Spec.ColumnSize;
        var r = TubeR + c / 2 + 0.002;
        var (cx, cy) = (r * Math.Cos(phi), r * Math.Sin(phi));
        var (ux, uy, vx, vy) = (Math.Cos(phi), Math.Sin(phi), -Math.Sin(phi), Math.Cos(phi));
        Vec2 P(double du, double dv) => W(cx + du * ux + dv * vx, cy + du * uy + dv * vy);
        return [P(-c / 2, -c / 2), P(c / 2, -c / 2), P(c / 2, c / 2), P(-c / 2, c / 2)];
    }

    /// <summary>A closed sector of a ring between two levels (a whole ring a prism with its hole, a disc when <paramref name="r0"/> is 0).</summary>
    Solid? Sector(double r0, double r1, double f0, double f1, double z0, double z1, string tag)
    {
        if (r1 - r0 < 1e-6 || z1 - z0 < 1e-6 || f1 - f0 < 1e-6) return null;
        if (f1 - f0 >= 2 * Math.PI - 1e-9)
        {
            var outer = CirclePts(r1);
            if (r0 < 1e-6) return Prism.ExtrudeFootprint(outer, z0, z1 - z0, tag);
            var n = outer.Length;
            List<Vec3> inner = [.. Enumerable.Range(0, n).Select(i => Polar(r0, 2 * Math.PI * i / n).At(z0))];
            return Prism.Extrude([.. outer.Select(p => p.At(z0))], [inner], new Vec3(0, 0, z1 - z0), tag);
        }
        return Prism.ExtrudeFootprint(SectorPts(r0, r1, f0, f1), z0, z1 - z0, tag);
    }

    /// <summary>Every part of a vacuum lift: the tube with the landings' openings, the frame (base ring, ring beams, columns, door frames), the head unit, the cabin at the lowest stop, its door, the landing doors.</summary>
    List<(string Key, string Name, List<Solid> Solids, string Role)> VacuumBodies()
    {
        var s = Spec;
        var (r, tw, rc, dh, f) = (TubeR, s.TubeWall, CabinR, DoorH, s.DoorFrame);
        const double full = 2 * Math.PI;
        var o = new List<(string, string, List<Solid>, string)>();
        var landings = Landings();
        var alpha = HalfArc(r);

        // The tube in bands between the doors' sills and heads: whole where no door opens, round the door where one does.
        var tube = new List<Solid>();
        var cuts = new SortedSet<double> { ZCabin, ZHead };
        foreach (var l in landings) { cuts.Add(Math.Clamp(l.Z0, ZCabin, ZHead)); cuts.Add(Math.Clamp(l.Z0 + dh, ZCabin, ZHead)); }
        var zs = cuts.ToList();
        for (var i = 0; i + 1 < zs.Count; i++)
        {
            var (za, zb) = (zs[i], zs[i + 1]);
            if (zb - za < 1e-6) continue;
            var mid = (za + zb) / 2;
            var open = landings.Where(l => mid > l.Z0 && mid < l.Z0 + dh).Select(l => (l.Phi, alpha));
            foreach (var (a, b) in ClosedArcs(open)) Add(tube, Sector(r - tw, r, a, b, za, zb, "tube"));
        }
        o.Add(("shaft", "Tube", tube, "tube"));

        // The frame: the base ring, a ring beam in each floor above, the columns, each landing door's frame.
        var frame = new List<Solid>();
        Add(frame, Sector(0, r + 0.03, 0, full, ZB, ZCabin, "baseRing"));
        foreach (var st in Stops.Skip(1)) Add(frame, Sector(r, r + T, 0, full, st.Z - s.RingBeamHeight, st.Z, "ringBeam"));
        foreach (var phi in ColumnAngles().Kept) Add(frame, Prism.ExtrudeFootprint(ColumnPts(phi), ZB, ZHead - ZB, "column"));
        if (f > 1e-6)
            foreach (var l in landings)
            {
                var df = f / r;
                Add(frame, Sector(r, r + f, l.Phi - alpha - df, l.Phi - alpha, l.Z0, l.Z0 + dh + f, "doorFrame"));
                Add(frame, Sector(r, r + f, l.Phi + alpha, l.Phi + alpha + df, l.Z0, l.Z0 + dh + f, "doorFrame"));
                Add(frame, Sector(r, r + f, l.Phi - alpha, l.Phi + alpha, l.Z0 + dh, l.Z0 + dh + f, "doorFrame"));
            }
        o.Add(("frame", "Frame", frame, "frame"));
        var head = new List<Solid>();
        Add(head, Sector(0, r + 0.03, 0, full, ZHead, ZHead + Tc, "headUnit"));
        o.Add(("headUnit", "Head unit", head, "frame"));

        // The cabin at the lowest stop: its floor and roof, its clear walls open towards the lowest door, its door.
        var bottom = landings[0];
        var ac = Math.Asin(Math.Min(0.95, (Dx1 - Dx0) / 2 / rc));
        var cabin = new List<Solid>();
        var walls = new List<Solid>();
        Add(cabin, Sector(0, rc, 0, full, ZCabin - 0.06, ZCabin, "cabinFloor"));
        Add(cabin, Sector(0, rc, 0, full, ZCabin + s.CabinHeight, ZCabin + s.CabinHeight + 0.1, "cabinRoof"));
        foreach (var (a, b) in ClosedArcs([(bottom.Phi, ac)])) Add(walls, Sector(rc - 0.015, rc, a, b, ZCabin, ZCabin + s.CabinHeight, "cabinWall"));
        o.Add(("cabin", "Cabin", cabin, "cabin"));
        o.Add(("cabinGlass", "Cabin walls", walls, "glass"));
        if (s.CabinDoor == "sliding")
        {
            var carDoor = new List<Solid>();
            Add(carDoor, Sector(rc + 0.004, rc + 0.02, bottom.Phi - ac - 0.03 / rc, bottom.Phi + ac + 0.03 / rc, ZCabin, ZCabin + dh, "carDoor"));
            o.Add(("carDoors", "Cabin door", carDoor, "doors"));
        }

        // The landing doors closed: a curved sliding panel outside the frame, or a swing door in the tube's line.
        var doors = new List<Solid>();
        foreach (var l in landings)
        {
            if (s.DoorKind == "sliding")
            {
                var r0 = r + f + 0.004;
                Add(doors, Sector(r0, r0 + 0.018, l.Phi - alpha - 0.03 / r, l.Phi + alpha + 0.03 / r, l.Z0, l.Z0 + dh, "landingDoor"));
            }
            else if (s.DoorKind == "swing") Add(doors, Sector(r - tw, r, l.Phi - alpha, l.Phi + alpha, l.Z0, l.Z0 + dh, "landingDoor"));
        }
        o.Add(("landingDoors", "Landing doors", doors, "doors"));
        return o.Where(x => x.Item3.Count > 0).ToList();
    }

    /// <summary>
    /// A vacuum lift's symbols on a storey: on every stop the cabin's circle with the lift's cross; where it stops the
    /// landing door (a sliding door's panel with, dashed, where it slides; a swing door's leaf open with its swing) and the
    /// cabin's door. The tube, the columns and the door frames are cut from the bodies.
    /// </summary>
    List<PlanItem> VacuumPlanOn(string storeyId)
    {
        var o = new List<PlanItem>();
        var stop = Stops.FirstOrDefault(x => x.Storey.Id == storeyId);
        if (stop == null) return o;
        var s = Spec;
        var (r, rc) = (TubeR, CabinR);
        o.Add(new PlanItem("symbol", true, CirclePts(rc)));
        o.Add(new PlanItem("symbol", false, [Polar(rc, Math.PI / 4), Polar(rc, 5 * Math.PI / 4)]));
        o.Add(new PlanItem("symbol", false, [Polar(rc, 3 * Math.PI / 4), Polar(rc, 7 * Math.PI / 4)]));
        if (!stop.Served) return o;
        var phi = Phi(s.LandingAngle(storeyId));
        var alpha = HalfArc(r);
        switch (s.DoorKind)
        {
            case "sliding":
            {
                var rr = r + s.DoorFrame + 0.013;
                o.Add(new PlanItem("symbol", false, ArcPts(rr, phi - alpha, phi + alpha)));
                o.Add(new PlanItem("dashed", false, ArcPts(rr, phi + alpha, phi + 3 * alpha)));
                break;
            }
            case "swing":
            {
                // Hinged at the opening's one side, the leaf open square to the door, its swing from the other side.
                var (hx, hy) = (r * Math.Cos(phi - alpha), r * Math.Sin(phi - alpha));
                var (ex, ey) = (r * Math.Cos(phi + alpha), r * Math.Sin(phi + alpha));
                var w = 2 * r * Math.Sin(alpha);
                var (lx, ly) = (hx + w * Math.Cos(phi), hy + w * Math.Sin(phi));
                o.Add(new PlanItem("symbol", false, [W(hx, hy), W(lx, ly)]));
                var t0 = Math.Atan2(ey - hy, ex - hx);
                var t1 = Math.Atan2(ly - hy, lx - hx);
                var d = (((t1 - t0) % (2 * Math.PI)) + 3 * Math.PI) % (2 * Math.PI) - Math.PI;
                var n = Chords.Count(w, d);
                o.Add(new PlanItem("symbol", false, [.. Enumerable.Range(0, n + 1).Select(i => W(hx + w * Math.Cos(t0 + d * i / n), hy + w * Math.Sin(t0 + d * i / n)))]));
                break;
            }
        }
        if (s.CabinDoor == "sliding")
        {
            var ac = Math.Asin(Math.Min(0.95, (Dx1 - Dx0) / 2 / rc));
            var rr = rc + 0.012;
            o.Add(new PlanItem("symbol", false, ArcPts(rr, phi - ac, phi + ac)));
            o.Add(new PlanItem("dashed", false, ArcPts(rr, phi + ac, phi + 3 * ac)));
        }
        return o;
    }

    /// <summary>The configurator's plan of a vacuum lift at its lowest stop: the tube cut round its door, the columns, the door's frame (as walls), then its symbols.</summary>
    public List<PlanItem> VacuumPlanPreview()
    {
        var o = new List<PlanItem>();
        var (r, tw, f) = (TubeR, Spec.TubeWall, Spec.DoorFrame);
        var bottom = Landings()[0];
        var alpha = HalfArc(r);
        foreach (var (a, b) in ClosedArcs([(bottom.Phi, alpha)])) o.Add(new PlanItem("wall", true, SectorPts(r - tw, r, a, b)));
        foreach (var phi in ColumnAngles().Kept) o.Add(new PlanItem("wall", true, ColumnPts(phi)));
        if (f > 1e-6)
        {
            o.Add(new PlanItem("wall", true, SectorPts(r, r + f, bottom.Phi - alpha - f / r, bottom.Phi - alpha)));
            o.Add(new PlanItem("wall", true, SectorPts(r, r + f, bottom.Phi + alpha, bottom.Phi + alpha + f / r)));
        }
        o.Add(new PlanItem("symbol", true, CirclePts(r + 0.03)));
        o.AddRange(VacuumPlanOn(Stops[0].Storey.Id));
        return o;
    }

    /// <summary>A section through a vacuum lift, front to back: boxes (role, y0, y1, z0, z1).</summary>
    public List<(string Role, double Y0, double Y1, double Z0, double Z1)> VacuumSection()
    {
        var s = Spec;
        var (r, tw, rc, dh) = (TubeR, s.TubeWall, CabinR, DoorH);
        var o = new List<(string, double, double, double, double)>();
        void Box(string role, double a0, double a1, double z0, double z1) { if (a1 - a0 > 1e-6 && z1 - z0 > 1e-6) o.Add((role, a0, a1, z0, z1)); }
        Box("frame", -r - 0.03, r + 0.03, ZB, ZCabin);
        var landings = Landings();
        // A landing's door shows on the side it faces more (its front within 90° of the lift's front).
        foreach (var front in new[] { true, false })
        {
            var (a0, a1) = front ? (-r, -r + tw) : (r - tw, r);
            var z = ZCabin;
            foreach (var l in landings.Where(l => (Math.Sin(l.Phi) < 1e-9) == front).OrderBy(l => l.Z0))
            {
                Box("tube", a0, a1, z, l.Z0);
                Box("door", front ? a0 - 0.03 : a1, front ? a0 : a1 + 0.03, l.Z0, l.Z0 + dh);
                z = Math.Max(z, l.Z0 + dh);
            }
            Box("tube", a0, a1, z, ZHead);
        }
        foreach (var st in Stops.Skip(1))
        {
            Box("frame", -r - T, -r, st.Z - s.RingBeamHeight, st.Z);
            Box("frame", r, r + T, st.Z - s.RingBeamHeight, st.Z);
        }
        Box("machine", -r - 0.03, r + 0.03, ZHead, ZHead + Tc);
        Box("cabin", -rc, rc, ZCabin - 0.06, ZCabin + s.CabinHeight + 0.1);
        return o;
    }

    /// <summary>What the model says of a vacuum lift: columns left out of the doors, a head unit above the next floor, many stops.</summary>
    public List<string> VacuumNotes(IModelView m)
    {
        var o = new List<string>();
        var skipped = ColumnAngles().Skipped;
        if (skipped > 0) o.Add($"{skipped} frame column(s) would stand in a landing door: left out.");
        var above = Spatial.Storeys(m).Select(x => x.Num("elevation")).Where(z => z > ZT + 1e-6).Select(z => (double?)z).Min();
        if (above is { } za && za < ZTop - 1e-6)
            o.Add($"The head unit rises to +{(ZTop - ZT).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} m over the top stop, above the next floor: it opens that floor too.");
        if (Stops.Count(x => x.Served) > 5) o.Add("Vacuum lifts serve up to about five stops (indicative).");
        return o;
    }
}
