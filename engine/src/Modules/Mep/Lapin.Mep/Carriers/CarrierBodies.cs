using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// A cable carrier's body and plan symbol. Its profile is swept along its centreline: a tray's open U, a ladder's two
/// rails and its rungs, trunking's box and lid, a conduit's tube (its circle by <see cref="Chords"/>). Each corner is a
/// bend of the type's inside radius (round the width for a turn in plan, round the height for a turn up or down; a
/// corner too short for it takes the largest radius that fits), each piece mitred onto the next. A tee (a cross) is
/// the trunk's piece across a branch's mouth with the branch's stub; a reducer the branch's own piece where it meets a
/// carrier of another size end to end. Fittings are parts keyed <c>fitting:&lt;kind&gt;:…</c>, written to IFC as
/// IfcCableCarrierFittings aggregated in the carrier. The plan: its outline and centreline, a vertical leg's section
/// crossed.
/// </summary>
public static class CarrierBodies
{
    public sealed record Fittings(int Bends, int Tees, int Crosses, int Reducers);

    sealed record Seg(Vec3 A, Vec3 B, Vec3 U, string Key)
    {
        public Vec3 T => (B - A).Normalized();
        public double Length => (B - A).Length;
    }

    sealed record Junction(double Station, List<(Entity Branch, Vec2 Toward)> Branches)
    {
        public string Key => Branches.Count > 1 ? "fitting:cross:" + string.Join("+", Branches.Select(b => b.Branch.Id)) : "fitting:tee:" + Branches[0].Branch.Id;
    }

    /// <summary>The least straight a branch leaves its trunk with, past the trunk's side (m).</summary>
    const double Mouth = 0.05;

    static double MouthOf(CarrierSpec trunk) => Math.Max(trunk.BendRadius, Mouth);

    const string Steel = "#a7b0b8", Lid = "#c5ccd3", Pvc = "#e6e6e1", Conduit = "#7f8c8d";
    static string ColorOf(CarrierSpec s) => s.Kind.Key switch { "trunking" => Pvc, "conduit" => Conduit, _ => Steel };

    // ─── What the carrier meets ─────────────────────────────────────────────

    static Vec2 Toward(IModelView m, Entity branch, string end)
    {
        var p = CableCarriers.Followed(m, branch).Points;
        if (p.Count < 2) return new Vec2(1, 0);
        var d = end == "start" ? p[1] - p[0] : p[^2] - p[^1];
        return d.Length > 1e-9 ? d * (1 / d.Length) : new Vec2(1, 0);
    }

    /// <summary>The branches that leave the carrier inside its length, by station: one a tee, two across from each other a cross.</summary>
    static List<Junction> JunctionsOf(IModelView m, Entity e)
    {
        var o = new List<Junction>();
        foreach (var (branch, end, station) in CableCarriers.BranchesOf(m, e.Id).Where(b => CableCarriers.Inside(e, b.Station)).OrderBy(b => b.Station))
        {
            var toward = Toward(m, branch, end);
            if (o.Count > 0 && o[^1] is { Branches.Count: 1 } last && Math.Abs(last.Station - station) < 0.02 && Vec2.Dot(last.Branches[0].Toward, toward) < -0.9)
                last.Branches.Add((branch, toward));
            else o.Add(new Junction(station, [(branch, toward)]));
        }
        return o;
    }

    /// <summary>The carrier an end meets end to end with another size (a reducer there), and that carrier's size.</summary>
    static (Entity Trunk, CarrierSpec Spec)? ReducerAt(IModelView m, Entity e, CarrierSpec s, string end)
    {
        if (CableCarriers.EndOf(m, e, end) is not { } c || m.Get(c.CarrierId) is not { } trunk || CableCarriers.Inside(trunk, c.Station)) return null;
        var t = CableCarriers.SpecOf(m, trunk);
        return Math.Abs(t.Across - s.Across) > 1e-4 || Math.Abs(t.Up - s.Up) > 1e-4 ? (trunk, t) : null;
    }

    static double ReducerLength(CarrierSpec s, CarrierSpec t) => Math.Max(0.1, Math.Abs(t.Across - s.Across));

    /// <summary>How much of the centreline an end leaves to what it meets: a tee's mouth on the trunk, a reducer.</summary>
    static double TrimAt(IModelView m, Entity e, CarrierSpec s, string end)
    {
        if (CableCarriers.EndOf(m, e, end) is not { } c || m.Get(c.CarrierId) is not { } trunk) return 0;
        var t = CableCarriers.SpecOf(m, trunk);
        if (CableCarriers.Inside(trunk, c.Station)) return t.Across / 2 + MouthOf(t);
        return ReducerAt(m, e, s, end) is { } r ? ReducerLength(s, r.Spec) : 0;
    }

    public static Fittings FittingsOf(IModelView m, Entity e)
    {
        var s = CableCarriers.SpecOf(m, e);
        var bends = Math.Max(0, CableCarriers.Centreline(m, e).Count - 2);
        var j = JunctionsOf(m, e);
        var reducers = new[] { "start", "end" }.Count(end => ReducerAt(m, e, s, end) != null);
        return new Fittings(bends, j.Count(x => x.Branches.Count == 1), j.Count(x => x.Branches.Count > 1), reducers);
    }

    // ─── The path ───────────────────────────────────────────────────────────

    static List<Vec3> TrimStart(List<Vec3> line, double d)
    {
        if (d <= 1e-9 || line.Count < 2) return line;
        d = Math.Min(d, CableCarriers.Length(line) - 1e-3);
        if (d <= 0) return line;
        for (var i = 1; i < line.Count; i++)
        {
            var l = (line[i] - line[i - 1]).Length;
            if (d < l - 1e-9) return [line[i - 1] + (line[i] - line[i - 1]).Normalized() * d, .. line.Skip(i)];
            d -= l;
        }
        return line;
    }

    static List<Vec3> TrimEnd(List<Vec3> line, double d)
    {
        var r = Enumerable.Reverse(line).ToList();
        r = TrimStart(r, d);
        r.Reverse();
        return r;
    }

    static Vec3 Rotate(Vec3 v, Vec3 n, double a) => v * Math.Cos(a) + Vec3.Cross(n, v) * Math.Sin(a) + n * (Vec3.Dot(n, v) * (1 - Math.Cos(a)));

    /// <summary>Each leg's up: a level leg's is up; a vertical leg's faces back along the level leg it turns from (or into), as a tray bent up keeps its floor.</summary>
    static List<Vec3> Ups(List<Vec3> line)
    {
        var n = line.Count - 1;
        var t = Enumerable.Range(0, n).Select(i => (line[i + 1] - line[i]).Normalized()).ToList();
        bool Vertical(int i) => Math.Abs(t[i].Z) > 0.9;
        var o = new List<Vec3>();
        for (var i = 0; i < n; i++)
        {
            if (!Vertical(i)) { o.Add(Vec3.UnitZ); continue; }
            Vec3? d = null;
            for (var k = i - 1; k >= 0 && d == null; k--) if (!Vertical(k)) d = new Vec3(t[k].X, t[k].Y, 0).Normalized();
            for (var k = i + 1; k < n && d == null; k++) if (!Vertical(k)) d = -new Vec3(t[k].X, t[k].Y, 0).Normalized();
            o.Add((d ?? new Vec3(1, 0, 0)) * -Math.Sign(t[i].Z));
        }
        return o;
    }

    /// <summary>The centreline as straight legs and bends' chords, each with its up, keyed by what it is a piece of.</summary>
    static List<Seg> Fillet(List<Vec3> line, CarrierSpec s)
    {
        var segs = new List<Seg>();
        if (line.Count < 2) return segs;
        var ups = Ups(line);
        var cur = line[0];
        var bend = 0;
        void Leg(Vec3 a, Vec3 b, Vec3 u) { if ((b - a).Length > 1e-6) segs.Add(new Seg(a, b, u, "leg")); }
        for (var i = 1; i < line.Count - 1; i++)
        {
            Vec3 v = line[i], d1 = (v - line[i - 1]).Normalized(), d2 = (line[i + 1] - v).Normalized();
            var angle = Math.Acos(Math.Clamp(Vec3.Dot(d1, d2), -1, 1));
            var key = $"fitting:bend:{bend++}";
            if (angle < 1e-6 || angle > Math.PI - 1e-6) { Leg(cur, v, ups[i - 1]); cur = v; continue; }
            var nrm = Vec3.Cross(d1, d2).Normalized();
            var r = s.BendRadius + (Math.Abs(nrm.Z) > 0.7 ? s.Across : s.Up) / 2;
            var avail = Math.Min((v - cur).Length, (line[i + 1] - v).Length * (i == line.Count - 2 ? 1 : 0.5));
            var tan = r * Math.Tan(angle / 2);
            if (tan > avail) { tan = avail * 0.999; r = tan / Math.Tan(angle / 2); }
            if (tan < 1e-4) { Leg(cur, v, ups[i - 1]); cur = v; continue; }
            Vec3 t1 = v - d1 * tan, t2 = v + d2 * tan;
            Leg(cur, t1, ups[i - 1]);
            var c = t1 + Vec3.Cross(nrm, d1).Normalized() * r;
            var steps = Chords.Count(r, angle, 2);
            Vec3 At(int k) { var f = angle * k / steps; return k == steps ? t2 : c + (t1 - c) * Math.Cos(f) + d1 * (r * Math.Sin(f)); }
            for (var k = 0; k < steps; k++)
                segs.Add(new Seg(At(k), At(k + 1), Rotate(ups[i - 1], nrm, angle * (k + 0.5) / steps), key));
            cur = t2;
        }
        Leg(cur, line[^1], ups[^1]);
        return segs;
    }

    /// <summary>A tee's (a cross's) piece of the trunk, cut out of the leg it is on.</summary>
    static void SplitAt(List<Seg> segs, Vec3 p, double half, string key)
    {
        int best = -1; double bd = 0.05, at = 0;
        for (var i = 0; i < segs.Count; i++)
        {
            if (segs[i].Key != "leg") continue;
            var sg = segs[i];
            var t = Math.Clamp(Vec3.Dot(p - sg.A, sg.T), 0, sg.Length);
            var d = (sg.A + sg.T * t - p).Length;
            if (d < bd) { bd = d; best = i; at = t; }
        }
        if (best < 0) return;
        var g = segs[best];
        double a = Math.Max(0, at - half), b = Math.Min(g.Length, at + half);
        var parts = new List<Seg>();
        if (a > 1e-6) parts.Add(g with { B = g.A + g.T * a });
        parts.Add(new Seg(g.A + g.T * a, g.A + g.T * b, g.U, key));
        if (g.Length - b > 1e-6) parts.Add(g with { A = g.A + g.T * b });
        segs.RemoveAt(best);
        segs.InsertRange(best, parts);
    }

    // ─── Profiles and sweeps ────────────────────────────────────────────────

    /// <summary>The profile's rings, counter-clockwise across (x) and up (y), the centreline in the middle; a lid's ring said.</summary>
    static List<(List<Vec2> Ring, bool Lid)> Profile(CarrierSpec s)
    {
        double w = s.Width / 2, h = s.Height / 2, t = Math.Max(s.Thickness, 0.0005);
        static List<Vec2> Rect(double x0, double x1, double y0, double y1) => [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];
        List<Vec2> U(double top) => [new(-w, top), new(-w, -h), new(w, -h), new(w, top), new(w - t, top), new(w - t, -h + t), new(-w + t, -h + t), new(-w + t, top)];
        switch (s.Kind.Key)
        {
            case "conduit":
                var r = s.Diameter / 2;
                var n = Chords.Circle(r);
                return [([.. Enumerable.Range(0, n).Select(i => new Vec2(r * Math.Cos(2 * Math.PI * i / n), r * Math.Sin(2 * Math.PI * i / n)))], false)];
            case "ladder":
                return [(Rect(-w, -w + t, -h, h), false), (Rect(w - t, w, -h, h), false)];
            case "trunking":
                return [(U(h - t), false), (Rect(-w, w, h - t, h), true)];
            default:
                return [(U(h), false)];
        }
    }

    static Vec3 Across(Vec3 t, Vec3 u) => Vec3.Cross(u, t).Normalized();

    /// <summary>A profile point in space at <paramref name="v"/>, in the frame (t, u), on the mitre plane through v whose normal is <paramref name="mitre"/>.</summary>
    static Vec3 On(Vec3 v, Vec3 t, Vec3 u, Vec3 mitre, Vec2 q)
    {
        var o = Across(t, u) * q.X + u * q.Y;
        var den = Vec3.Dot(t, mitre);
        var s = Math.Abs(den) > 1e-6 ? -Vec3.Dot(o, mitre) / den : 0;
        return v + o + t * s;
    }

    /// <summary>Rings joined into a closed solid: its two ends capped, a quad between each two rings for each edge.</summary>
    static Solid? Loft(IReadOnlyList<Vec3[]> rings, Vec3 t0, Vec3 t1, string tag)
    {
        if (rings.Count < 2 || rings[0].Length < 3) return null;
        var n = rings[0].Length;
        var verts = new List<Vec3>();
        foreach (var r in rings) verts.AddRange(r);
        var faces = new List<FaceInput>
        {
            new([.. Enumerable.Range(0, n).Reverse()], null, -t0, "start"),
            new([.. Enumerable.Range((rings.Count - 1) * n, n)], null, t1, "end"),
        };
        for (var k = 0; k + 1 < rings.Count; k++)
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                var outward = Vec3.Cross(rings[k][j] - rings[k][i], rings[k + 1][i] - rings[k][i]);
                faces.Add(new FaceInput([k * n + i, k * n + j, (k + 1) * n + j, (k + 1) * n + i], null, outward.Length > 1e-12 ? outward : null, "side"));
            }
        return SolidBuilder.Make(verts, faces, tag);
    }

    static Vec3 Mitre(Vec3 a, Vec3 b) => (a + b).Length > 1e-6 ? (a + b).Normalized() : b;

    /// <summary>Sweeps of the profile along consecutive pieces (one section), mitred onto what comes before and after.</summary>
    static List<(Solid Solid, bool Lid)> Sweep(List<Seg> segs, int from, int to, CarrierSpec s, string tag)
    {
        var prev = from > 0 && (segs[from - 1].B - segs[from].A).Length < 1e-6 ? segs[from - 1] : null;
        var next = to + 1 < segs.Count && (segs[to].B - segs[to + 1].A).Length < 1e-6 ? segs[to + 1] : null;
        var o = new List<(Solid, bool)>();
        foreach (var (ring, lid) in Profile(s))
        {
            var rings = new List<Vec3[]>();
            for (var j = from; j <= to + 1; j++)
            {
                Seg g; Vec3 v, mitre;
                if (j == from) { g = segs[from]; v = g.A; mitre = prev != null ? Mitre(prev.T, g.T) : g.T; }
                else
                {
                    g = segs[j - 1]; v = g.B;
                    mitre = j <= to ? Mitre(g.T, segs[j].T) : next != null ? Mitre(g.T, next.T) : g.T;
                }
                rings.Add([.. ring.Select(q => On(v, g.T, g.U, mitre, q))]);
            }
            if (Loft(rings, segs[from].T, segs[to].T, tag) is { } solid) o.Add((solid, lid));
        }
        return o;
    }

    /// <summary>A straight piece of a profile between two points (a stub, a rung).</summary>
    static List<(Solid Solid, bool Lid)> Straight(Vec3 a, Vec3 b, Vec3 up, CarrierSpec s, string tag)
    {
        if ((b - a).Length < 1e-6) return [];
        return Sweep([new Seg(a, b, up, tag)], 0, 0, s, tag);
    }

    // ─── The element ────────────────────────────────────────────────────────

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        var s = CableCarriers.SpecOf(m, e);
        var full = CableCarriers.Centreline(m, e);
        if (full.Count < 2) return new ElementGeometry([], Diagnostics: ["A cable carrier needs two points apart."]);
        if (s.Across < 1e-3 || s.Up < 1e-3) return new ElementGeometry([], Diagnostics: ["A cable carrier needs a size."]);
        var line = CableCarriers.Clean(TrimEnd(TrimStart(full, TrimAt(m, e, s, "start")), TrimAt(m, e, s, "end")));
        var segs = Fillet(line, s);
        var z0 = Nodes.StoreyZ(m, e);
        var extra = new Dictionary<string, List<Solid>>();
        void Add(string key, IEnumerable<Solid> solids) { if (!extra.TryGetValue(key, out var l)) extra[key] = l = []; l.AddRange(solids); }
        var plan = new List<PlanItem>();
        var mouth = MouthOf(s);

        // Tees and crosses: the trunk's piece across the mouth, and each branch's stub out of its side.
        foreach (var j in JunctionsOf(m, e))
        {
            var (at, level, _) = CableCarriers.OnTrunk(e, j.Station);
            var p = new Vec3(at.X, at.Y, z0 + level);
            var half = j.Branches.Max(b => CableCarriers.SpecOf(m, b.Branch).Across) / 2 + mouth;
            SplitAt(segs, p, half, j.Key);
            foreach (var (branch, toward) in j.Branches)
            {
                var bs = CableCarriers.SpecOf(m, branch);
                var n = new Vec3(toward.X, toward.Y, 0).Normalized();
                Vec3 a = p + n * Math.Max(0, s.Across / 2 - s.Thickness), b = p + n * (s.Across / 2 + mouth);
                Add(j.Key, Straight(a, b, Vec3.UnitZ, bs, "tee").Select(x => x.Solid));
                var side = new Vec2(-n.Y, n.X) * (bs.Across / 2);
                plan.Add(new PlanItem("symbol", false, [a.Xy + side, b.Xy + side]));
                plan.Add(new PlanItem("symbol", false, [a.Xy - side, b.Xy - side]));
            }
        }

        // Reducers: this carrier's own piece where it meets another size end to end.
        foreach (var end in new[] { "start", "end" })
        {
            if (ReducerAt(m, e, s, end) is not { } red || line.Count < 2) continue;
            var (q, pEnd) = end == "start" ? (line[0], full[0]) : (line[^1], full[^1]);
            if ((pEnd - q).Length < 1e-6) continue;
            var t = (pEnd - q).Normalized();
            var up = Math.Abs(t.Z) > 0.9 ? new Vec3(1, 0, 0) : Vec3.UnitZ;
            var big = s with { Width = red.Spec.Width, Height = red.Spec.Height, Diameter = red.Spec.Diameter };
            var mine = Profile(s);
            var theirs = Profile(big);
            var key = "fitting:reducer:" + red.Trunk.Id;
            for (var i = 0; i < mine.Count && i < theirs.Count; i++)
            {
                Vec3[] r0 = [.. mine[i].Ring.Select(x => On(q, t, up, t, x))], r1 = [.. theirs[i].Ring.Select(x => On(pEnd, t, up, t, x))];
                if (Loft([r0, r1], t, t, "reducer") is { } solid) Add(key, [solid]);
            }
            var across = new Vec2(-t.Y, t.X);
            if (across.Length > 1e-6)
            {
                across = across * (1 / across.Length);
                plan.Add(new PlanItem("symbol", false, [q.Xy + across * (s.Across / 2), pEnd.Xy + across * (red.Spec.Across / 2)]));
                plan.Add(new PlanItem("symbol", false, [q.Xy - across * (s.Across / 2), pEnd.Xy - across * (red.Spec.Across / 2)]));
            }
        }

        // The sweeps, section by section: the legs one part (a trunking's lid another), each fitting its own.
        var legs = new List<Solid>();
        var lids = new List<Solid>();
        for (var i = 0; i < segs.Count;)
        {
            var k = i;
            while (k + 1 < segs.Count && segs[k + 1].Key == segs[i].Key && (segs[k].B - segs[k + 1].A).Length < 1e-6) k++;
            var key = segs[i].Key;
            var swept = Sweep(segs, i, k, s, key == "leg" ? "carrier" : "fitting");
            if (key == "leg") foreach (var (solid, lid) in swept) (lid ? lids : legs).Add(solid);
            else Add(key, swept.Select(x => x.Solid));
            i = k + 1;
        }
        // A ladder's rungs, every 300 mm along its straight legs.
        var rungs = new List<Solid>();
        if (s.Kind.Key == "ladder")
        {
            double w = s.Width / 2 - s.Thickness, h = s.Height / 2;
            List<Vec2> ring = [new(-w, -h), new(w, -h), new(w, -h + 0.012), new(-w, -h + 0.012)];
            foreach (var g in segs.Where(x => x.Key == "leg"))
                for (var at = 0.15; at + 0.03 < g.Length - 0.02; at += 0.3)
                {
                    var a = g.A + g.T * at;
                    if (Prism.Extrude([.. ring.Select(q => On(a, g.T, g.U, g.T, q))], null, g.T * 0.03, "rung") is { } rs) rungs.Add(rs);
                }
        }

        var color = ColorOf(s);
        var parts = new List<GeometryPart>();
        if (legs.Count > 0) parts.Add(new GeometryPart(s.Kind.Label, color, Openings.Combine(legs, "carrier"), null, "carrier"));
        if (lids.Count > 0) parts.Add(new GeometryPart("Lid", s.Kind.Key == "trunking" ? Pvc : Lid, Openings.Combine(lids, "lid"), null, "lid"));
        if (rungs.Count > 0) parts.Add(new GeometryPart("Rungs", color, Openings.Combine(rungs, "rung"), null, "rungs"));
        foreach (var (key, solids) in extra.OrderBy(x => x.Key, StringComparer.Ordinal))
            if (solids.Count > 0) parts.Add(new GeometryPart(FittingLabel(key), color, Openings.Combine(solids, "fitting"), null, key));

        // The plan: the outline either side of the path as built, the centreline dash-dotted, a vertical leg's section crossed.
        var path = new List<Vec2>();
        void Spot(Vec2 p) { if (path.Count == 0 || (p - path[^1]).Length > 1e-6) path.Add(p); }
        foreach (var g in segs) { Spot(g.A.Xy); Spot(g.B.Xy); }
        if (path.Count >= 2)
        {
            plan.Add(new PlanItem("symbol", false, Pipes.Offset(path, s.Across / 2)));
            plan.Add(new PlanItem("symbol", false, Pipes.Offset(path, -s.Across / 2)));
        }
        var centre = new List<Vec2>();
        void C(Vec2 p) { if (centre.Count == 0 || (p - centre[^1]).Length > 1e-6) centre.Add(p); }
        C(full[0].Xy); foreach (var p in path) C(p); C(full[^1].Xy);
        if (centre.Count >= 2) plan.Add(new PlanItem("symbol", false, centre, Style: new Lapin.Drawing.LineStyleSpec(Pattern: "center")));
        foreach (var g in segs.Where(x => x.Key == "leg" && Math.Abs(x.T.Z) > 0.9))
        {
            var a = Across(g.T, g.U);
            Vec2 P(double x, double y) => (g.A + a * x + g.U * y).Xy;
            double hw = s.Across / 2, hh = s.Up / 2;
            plan.Add(new PlanItem("symbol", true, [P(-hw, -hh), P(hw, -hh), P(hw, hh), P(-hw, hh)]));
            plan.Add(new PlanItem("symbol", false, [P(-hw, -hh), P(hw, hh)]));
            plan.Add(new PlanItem("symbol", false, [P(hw, -hh), P(-hw, hh)]));
        }
        plan.Add(new PlanItem("hit", false, centre.Count >= 2 ? centre : [full[0].Xy, full[^1].Xy]));
        return new ElementGeometry(parts, plan, centre);
    }

    static string FittingLabel(string key) => key.Split(':') is [_, var kind, ..] ? kind switch
    {
        "bend" => "Bend", "tee" => "Tee", "cross" => "Cross", "reducer" => "Reducer", _ => "Fitting",
    } : "Fitting";
}
