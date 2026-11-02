namespace Lapin.Wood;

/// <summary>
/// A timber-frame wall's members, laid out in its own elevation (docs/plan-wood.md): <c>u</c> along the wall from its axis's
/// start, <c>z</c> up from its base, each member a face in that plane (a rectangle, or a slanted top plate under a roof)
/// standing across the core between <c>y0</c> and <c>y1</c>. The plates, the end studs, the studs on the module, and round
/// each opening (by its rough opening: the door or window and its fitting gaps) the king and jack studs, the header, the
/// rough sill and the cripples on the module; openings closer than the narrowest pier share a header; the noggin rows
/// between the uprights; the insulation in what is left of the stud zone. A pure function: the configurator's preview and
/// the walls use it alike.
/// </summary>
public static class Framing
{
    public sealed record Member(string Role, IReadOnlyList<(double U, double Z)> Face, double Y0, double Y1, string Section, string? Opening = null)
    {
        public double U0 => Face.Min(p => p.U);
        public double U1 => Face.Max(p => p.U);
        public double Z0 => Face.Min(p => p.Z);
        public double Z1 => Face.Max(p => p.Z);
        public bool Upright => Role is "stud" or "end" or "king" or "jack" or "cripple";
        /// <summary>Its length as it is cut: an upright's height, a plate's or header's run (along its slope).</summary>
        public double Length => Upright ? Z1 - Z0 : Face.Count == 4 ? Math.Sqrt(Math.Pow(Face[1].U - Face[0].U, 2) + Math.Pow(Face[1].Z - Face[0].Z, 2)) : U1 - U0;
        public double Volume => Math.Abs(Area(Face)) * (Y1 - Y0);
    }

    /// <summary>An opening in the wall: its centre along the axis, its bottom, the door's or window's size, whether it is a door, and its own rule if it has one.</summary>
    public sealed record Hole(string Id, double Along, double Sill, double Width, double Height, bool Door, OpeningRule? Own = null);

    /// <summary>A rectangle of the stud zone filled with insulation (its top may slant).</summary>
    public sealed record Cavity(double U0, double U1, double Z0, double Z1Left, double Z1Right);

    public sealed record Result(List<Member> Members, List<Cavity> Insulation, List<string> Warnings, Dictionary<string, (OpeningRule Rule, double U0, double U1, double Z0, double Z1)> Openings);

    public static readonly string[] Roles = ["plate", "end", "stud", "king", "jack", "header", "sill", "cripple", "noggin"];

    static double Area(IReadOnlyList<(double U, double Z)> f)
    {
        double a = 0;
        for (var i = 0; i < f.Count; i++) { var (p, q) = (f[i], f[(i + 1) % f.Count]); a += p.U * q.Z - q.U * p.Z; }
        return a / 2;
    }

    static List<(double, double)> Rect(double u0, double u1, double z0, double z1) => [(u0, z0), (u1, z0), (u1, z1), (u0, z1)];

    /// <summary>
    /// The frame of a wall: its spec; the core's run along the wall (<paramref name="u0"/> to <paramref name="u1"/>) and across it
    /// (<paramref name="y0"/> to <paramref name="y1"/>); the core's top over its base at any u, with the u where it bends; its
    /// openings; the u the studs' module starts from.
    /// </summary>
    public static Result Layout(FrameSpec spec, double u0, double u1, Func<double, double> top, IReadOnlyList<double> bends, double y0, double y1, IReadOnlyList<Hole> holes, double origin)
    {
        var members = new List<Member>();
        var warnings = new List<string>();
        var rules = new Dictionary<string, (OpeningRule, double, double, double, double)>();
        var (t, depth) = spec.Stud;
        var stud = spec.StudSection;
        if (Math.Abs((y1 - y0) - depth) > 0.003) warnings.Add($"The core is {Math.Round((y1 - y0) * 1000)} mm thick and the studs {Math.Round(depth * 1000)} mm deep.");
        var zA = spec.BottomPlates * t;
        double ZB(double u) => top(u) - spec.TopPlates * t;
        void Add(string role, List<(double, double)> face, string section, string? opening = null, double? ya = null, double? yb = null) =>
            members.Add(new Member(role, face, ya ?? y0, yb ?? y1, section, opening));

        // The openings' rough openings and rules, and the groups that share a header.
        var ros = holes.Select(h =>
        {
            var l = h.Along - h.Width / 2 - spec.GapSide;
            var r = h.Along + h.Width / 2 + spec.GapSide;
            var bottom = h.Door ? h.Sill : h.Sill - spec.GapBottom;
            var roTop = h.Sill + h.Height + spec.GapTop;
            var rule = h.Own ?? spec.RuleFor(r - l);
            if (!spec.LoadBearing && h.Own == null) rule = rule with { Header = new HeaderRule("none", stud, 1) };
            rules[h.Id] = (rule, l, r, bottom, roTop);
            return (Hole: h, L: l, R: r, Bottom: Math.Max(0, bottom), Top: roTop, Rule: rule,
                ZoneL: l - (rule.Jacks + rule.Kings) * t, ZoneR: r + (rule.Jacks + rule.Kings) * t);
        }).OrderBy(x => x.L).ToList();
        var groups = new List<List<int>>();
        for (var i = 0; i < ros.Count; i++)
        {
            if (groups.Count > 0 && ros[i].ZoneL - ros[groups[^1][^1]].ZoneR < spec.MinPier) groups[^1].Add(i);
            else groups.Add([i]);
        }
        var endZone = spec.EndStuds * t;
        foreach (var o in ros)
            if (o.ZoneL < u0 + endZone + spec.MinGap - 1e-9 || o.ZoneR > u1 - endZone - spec.MinGap + 1e-9)
                warnings.Add($"{o.Hole.Id}: too close to the wall's end for its king and jack studs.");

        // Bottom plates, cut at the doors; top plates along the top, bent where it bends.
        var doorGaps = ros.Where(o => o.Hole.Door || o.Bottom <= 1e-6).Select(o => (o.L, o.R)).OrderBy(x => x.L).ToList();
        for (var i = 0; i < spec.BottomPlates; i++)
        {
            var at = u0;
            foreach (var (l, r) in doorGaps.Append((u1, u1)))
            {
                var end = Math.Min(l, u1);
                if (end - at > 0.01) Add("plate", Rect(at, end, i * t, (i + 1) * t), stud);
                at = Math.Max(at, r);
            }
        }
        var knots = new List<double> { u0 };
        knots.AddRange(bends.Where(b => b > u0 + 1e-6 && b < u1 - 1e-6).OrderBy(b => b));
        knots.Add(u1);
        for (var i = 0; i < spec.TopPlates; i++)
            for (var k = 0; k + 1 < knots.Count; k++)
            {
                var (a, b) = (knots[k], knots[k + 1]);
                Add("plate", [(a, top(a) - (i + 1) * t), (b, top(b) - (i + 1) * t), (b, top(b) - i * t), (a, top(a) - i * t)], stud);
            }

        // The end studs.
        for (var i = 0; i < spec.EndStuds; i++)
        {
            Add("end", Rect(u0 + i * t, u0 + (i + 1) * t, zA, ZB(u0 + (i + 0.5) * t)), stud);
            Add("end", Rect(u1 - (i + 1) * t, u1 - i * t, zA, ZB(u1 - (i + 0.5) * t)), stud);
        }

        // Round each group of openings: kings at its outer sides, jacks at every rough opening's sides (merged where they meet),
        // one header over them all, each window's sill, cripples on the module above the header and under the sills.
        var grid = new List<double>();
        if (spec.Spacing > 0)
            for (var g = origin + Math.Ceiling((u0 - origin) / spec.Spacing) * spec.Spacing; g <= u1 + 1e-9; g += spec.Spacing) grid.Add(g);
        var blocked = new List<(double L, double R)>();
        var openingsRects = new List<(double U0, double U1, double Z0, double Z1)>();
        foreach (var grp in groups)
        {
            var first = ros[grp[0]];
            var last = ros[grp[^1]];
            var kingsL = first.Rule.Kings;
            var kingsR = last.Rule.Kings;
            var jacksOf = grp.Select(i => ros[i].Rule.Jacks).ToList();
            // The header: over every rough opening of the group, on the jacks, by the widest one's rule.
            var span0 = first.L - first.Rule.Jacks * t;
            var span1 = last.R + last.Rule.Jacks * t;
            var rule = spec.Openings.Length == 0 ? first.Rule : grp.Select(i => ros[i].Rule).MaxBy(r => r.MaxWidth)!;
            if (grp.Count > 1 && grp.All(i => ros[i].Hole.Own == null)) rule = spec.LoadBearing ? spec.RuleFor(span1 - span0) : rule;
            var hz0 = grp.Max(i => ros[i].Top);
            var (hw, hd) = FrameSpec.Size(rule.Header.Section, (t, 0.195));
            var flat = rule.Header.Kind == "none";
            var hh = flat ? hw : hd;
            var room = Math.Min(ZB(span0), ZB(span1)) - hz0;
            if (room < hh - 1e-6)
            {
                warnings.Add($"{first.Hole.Id}: the header ({Math.Round(hh * 1000)} mm) does not fit under the top plate ({Math.Round(Math.Max(0, room) * 1000)} mm).");
                hh = Math.Max(0, room);
            }
            var openingId = first.Hole.Id;
            if (hh > 0.005)
            {
                if (flat) Add("header", Rect(span0, span1, hz0, hz0 + hh), rule.Header.Section, openingId);
                else
                {
                    var plies = Math.Max(1, rule.Header.Plies);
                    var total = plies * hw;
                    if (total > (y1 - y0) + 1e-6) warnings.Add($"{first.Hole.Id}: {plies} × {Math.Round(hw * 1000)} mm of header is wider than the core.");
                    var ya = (y0 + y1) / 2 - total / 2;
                    for (var p = 0; p < plies; p++) Add("header", Rect(span0, span1, hz0, hz0 + hh), rule.Header.Section, openingId, ya + p * hw, ya + (p + 1) * hw);
                }
            }
            var headerTop = hz0 + hh;
            // Kings, full height, outside the jacks.
            for (var k = 0; k < kingsL; k++) { var a = span0 - (k + 1) * t; Add("king", Rect(a, a + t, zA, ZB(a + t / 2)), stud, openingId); }
            for (var k = 0; k < kingsR; k++) { var a = span1 + k * t; Add("king", Rect(a, a + t, zA, ZB(a + t / 2)), stud, openingId); }
            // Jacks, at each rough opening's sides, up to the header; where two meet between openings, one block.
            var jacks = new List<(double L, double R)>();
            for (var j = 0; j < grp.Count; j++)
            {
                var o = ros[grp[j]];
                if (jacksOf[j] > 0) { jacks.Add((o.L - jacksOf[j] * t, o.L)); jacks.Add((o.R, o.R + jacksOf[j] * t)); }
            }
            jacks = jacks.OrderBy(x => x.L).Aggregate(new List<(double L, double R)>(), (acc, x) => { if (acc.Count > 0 && x.L <= acc[^1].R + 1e-6) acc[^1] = (acc[^1].L, Math.Max(acc[^1].R, x.R)); else acc.Add(x); return acc; });
            foreach (var (a, b) in jacks)
                for (var x = a; x < b - 1e-6; x += t) Add("jack", Rect(x, Math.Min(b, x + t), zA, hz0), stud, openingId);
            // Cripples above the header, on the module.
            foreach (var g in grid.Where(g => g - t / 2 > span0 + spec.MinGap && g + t / 2 < span1 - spec.MinGap))
                if (ZB(g) - headerTop > 0.05) Add("cripple", Rect(g - t / 2, g + t / 2, headerTop, ZB(g)), stud, openingId);
            // Each window's rough sill and the cripples under it.
            foreach (var i in grp)
            {
                var o = ros[i];
                openingsRects.Add((o.L, o.R, o.Bottom, o.Top));
                if (o.Hole.Door || o.Bottom <= zA + 1e-6) continue;
                var sillBottom = o.Bottom - o.Rule.Sill * t;
                for (var p = 0; p < o.Rule.Sill; p++) Add("sill", Rect(o.L, o.R, o.Bottom - (p + 1) * t, o.Bottom - p * t), stud, o.Hole.Id);
                foreach (var g in grid.Where(g => g - t / 2 > o.L + spec.MinGap && g + t / 2 < o.R - spec.MinGap))
                    if (sillBottom - zA > 0.05) Add("cripple", Rect(g - t / 2, g + t / 2, zA, sillBottom), stud, o.Hole.Id);
            }
            blocked.Add((span0 - kingsL * t - spec.MinGap, span1 + kingsR * t + spec.MinGap));
        }

        // The studs on the module, where nothing else stands.
        foreach (var g in grid)
        {
            var (a, b) = (g - t / 2, g + t / 2);
            if (a < u0 + endZone + spec.MinGap || b > u1 - endZone - spec.MinGap) continue;
            if (blocked.Any(x => b > x.L && a < x.R)) continue;
            Add("stud", Rect(a, b, zA, ZB(g)), stud);
        }

        // Noggin rows: between the uprights standing at their height, not across an opening or its header and sill.
        foreach (var z in spec.Noggins)
        {
            var ups = members.Where(m => m.Upright && m.Z0 < z - t / 2 && m.Z1 > z + t / 2 && m.Y0 <= y0 + 1e-9).Select(m => (m.U0, m.U1)).Distinct().OrderBy(x => x.U0).ToList();
            for (var i = 0; i + 1 < ups.Count; i++)
            {
                var (a, b) = (ups[i].U1, ups[i + 1].U0);
                if (b - a < 0.02) continue;
                var mid = (a + b) / 2;
                if (members.Any(m => !m.Upright && m.Role != "plate" && m.U0 < mid && m.U1 > mid && m.Z0 < z + t && m.Z1 > z - t)) continue;
                if (openingsRects.Any(o => o.U0 < mid && o.U1 > mid && o.Z0 < z && o.Z1 > z)) continue;
                if (z + t / 2 > ZB(mid)) continue;
                Add("noggin", Rect(a, b, z - t / 2, z + t / 2), stud);
            }
        }

        // The insulation: the stud zone less every member and every rough opening, strip by strip.
        var solids = members.Where(m => m.Role != "plate").Select(m => (m.U0, m.U1, m.Z0, m.Z1)).Concat(openingsRects).ToList();
        var cuts = solids.SelectMany(s => new[] { s.Item1, s.Item2 }).Append(u0).Append(u1).Where(u => u >= u0 - 1e-9 && u <= u1 + 1e-9).Concat(knots).Distinct().OrderBy(u => u).ToList();
        var insulation = new List<Cavity>();
        for (var i = 0; i + 1 < cuts.Count; i++)
        {
            var (a, b) = (cuts[i], cuts[i + 1]);
            if (b - a < 0.005) continue;
            var mid = (a + b) / 2;
            var over = solids.Where(s => s.Item1 < mid && s.Item2 > mid).Select(s => (Z0: s.Item3, Z1: s.Item4)).OrderBy(s => s.Z0).ToList();
            var at = zA;
            var topA = ZB(a);
            var topB = ZB(b);
            foreach (var (s0, s1) in over.Append((Z0: double.PositiveInfinity, Z1: double.PositiveInfinity)))
            {
                var stop = Math.Min(s0, Math.Min(topA, topB));
                if (double.IsPositiveInfinity(s0))
                {
                    if (Math.Min(topA, topB) - at > 0.01) insulation.Add(new Cavity(a, b, at, topA, topB));
                    break;
                }
                if (stop - at > 0.01) insulation.Add(new Cavity(a, b, at, stop, stop));
                at = Math.Max(at, s1);
            }
        }
        return new Result(members, insulation, warnings, rules);
    }
}
