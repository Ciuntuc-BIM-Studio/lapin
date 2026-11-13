using System.Globalization;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;

namespace Lapin.Analysis;

/// <summary>
/// The snow and the wind derived from the site's climate (docs/plan-fem.md §9.9), on every ask, never stored, indicative.
/// <para>
/// <b>Snow</b> (CR 1-1-3/2012 = EN 1991-1-3 §5.2, 5.3): on every roof plane s = γIs · μ1(α) · Ce · Ct · s0,k per m² of its
/// plan, μ1 = 0.8 up to 30°, 0.8 (60 − α) / 30 to 60°, 0 past (Table 5.2); a floor nothing covers (a terrace, a flat roof)
/// μ1 = 0.8. The undrifted arrangement (i) in the case of the undrifted snow; a duo-pitch roof's drifted ones (ii), (iii) in
/// the cases of the wind blowing towards +X, −X, +Y, −Y: the planes facing the wind (within 60°) take 0.5 μ1, the others
/// μ1 (a hipped roof's planes the same way, Lapin's reading); a mono-pitch roof and a flat one have no drift.
/// </para>
/// <para>
/// <b>Wind</b> (CR 1-1-4/2012 = EN 1991-1-4 §7.2): the building's outline is the convex hull of its walls and columns, its
/// height h from its lowest node to its top; for each direction (towards +X, −X, +Y, −Y) d its depth along the wind, b its
/// width across, e = min(b, 2h). The façades (the hull's sides, from the ground to the roof over them): the windward one
/// zone D, the leeward E, the sides A, B, C from the windward edge (e/5, e), cpe,10 by Table 7.1 for h/d, D and E times the
/// lack of correlation (0.85 for h/d ≤ 1 to 1 at 5); ze = h when h ≤ b, else the bands of Figure 7.4. The roofs: flat ones
/// by Table 7.2 (sharp eaves: F, G, H, I), duo-pitch by Tables 7.4a (the wind square to the ridge: F, G, H on the windward
/// plane, J and I on the leeward) and 7.4b (along it), mono-pitch by Tables 7.3a and 7.3b, zones from the windward edge,
/// ze = h; the roofs' least coefficients in the suction cases, their greatest in the pressure cases. w = γIw · cpe · qp(ze),
/// on the surface square to it (cpe &gt; 0 pushes on it).
/// </para>
/// </summary>
public static class ClimateLoads
{
    static string N(double v, int d = 3) => Math.Round(v, d).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>μ1 for a pitch α (degrees): EN 1991-1-3 Table 5.2.</summary>
    public static double Mu1(double alpha) => alpha <= 30 ? 0.8 : alpha < 60 ? 0.8 * (60 - alpha) / 30 : 0;

    static double TrueArea(RoofPlane p, IReadOnlyList<Vec2> plan) => Math.Abs(Polygon2.SignedArea(plan)) / Math.Abs(p.Normal.Z);

    // ------------------------------------------------------------ snow

    public static void Snow(IModelView m, List<LoadCase> cases, List<RoofSurface> surfaces, List<DerivedLoad> derived, List<string> notes)
    {
        var snowCases = cases.Where(c => c.Takes(LoadCases.Snow)).ToList();
        if (snowCases.Count == 0) return;
        if (Climate.Snow(m) is not { } site)
        {
            if (surfaces.Count > 0) notes.Add("No snow on the roofs: the site's ground snow load s0,k is not set (Loads → Snow and wind, analysis.setClimate).");
            return;
        }
        var planes = 0;
        foreach (var c in snowCases)
        {
            var dir = c.WindDirection is { } d ? Climate.Vector(d) : (Vec2?)null;
            var made = new List<DerivedLoad>();
            var drifted = 0;
            foreach (var s in surfaces)
            {
                var drifts = s.Shape is "duo" or "other";
                for (var i = 0; i < s.Planes.Count; i++)
                {
                    var p = s.Planes[i];
                    var mu = s.Floor || p.Slope < RoofSurface.FlatBelow ? 0.8 : Mu1(p.Slope);
                    if (dir is { } w && drifts && p.Slope >= RoofSurface.FlatBelow && Vec2.Dot(p.Downslope, w) < -0.5) { mu *= 0.5; drifted++; }
                    var q = site.S(mu);
                    if (q <= 1e-9) continue;
                    var sloped = p.Slope >= 0.01;
                    made.Add(new DerivedLoad(LoadCases.Snow, c.Id, s.SourceId, s.Name, s.StoreyId, "area", p.Plan, p.Outline[0].Z, q, "kN/m²", q * p.PlanArea)
                    {
                        Points3 = p.Plan.Select(p.Lift).ToList(), Key = $"snow:{c.Id}:{i + 1}", Projected = sloped ? true : null,
                    });
                }
            }
            if (dir != null && drifted == 0)
            {
                notes.Add($"„{c.Code}” is the undrifted snow here (no roof plane faces the wind towards {Climate.Label(c.WindDirection!)}): left out.");
                continue;
            }
            planes = Math.Max(planes, made.Count);
            derived.AddRange(made);
        }
        if (planes > 0)
            notes.Add($"Snow on {planes} roof plane{(planes == 1 ? "" : "s")} and uncovered floor piece{(planes == 1 ? "" : "s")}: s = γIs·μ1·Ce·Ct·s0,k = {N(site.Importance)} × μ1 × {N(site.Ce)} × {N(site.Ct)} × {N(site.S0k)} kN/m² per m² of plan, "
                + $"μ1 0.8 to 30°, 0.8(60 − α)/30 to 60°, 0 past; drifted with 0.5 μ1 on the planes facing the wind; {Climate.Indicative}.");
        notes.Add("Not derived yet: snow drifts at parapets and changes of level (EN 1991-1-3 §6.2), snow overhanging the eaves (§6.3), the exceptional snow loads.");
    }

    // ------------------------------------------------------------ wind: the tables

    static readonly double[] Pitches = [5, 15, 30, 45, 60, 75];

    static double At(double[] ys, double alpha)
    {
        var a = Math.Clamp(alpha, Pitches[0], Pitches[^1]);
        for (var i = 0; i + 1 < Pitches.Length; i++)
            if (a <= Pitches[i + 1]) return ys[i] + (ys[i + 1] - ys[i]) * (a - Pitches[i]) / (Pitches[i + 1] - Pitches[i]);
        return ys[^1];
    }

    // EN 1991-1-4 Table 7.4a (duo-pitch, θ = 0°): the least (first line) and the greatest (second) of each zone.
    static readonly Dictionary<string, (double[] Min, double[] Max)> Duo0 = new()
    {
        ["F"] = ([-1.7, -0.9, -0.5, 0.0, 0.7, 0.8], [0.0, 0.2, 0.7, 0.7, 0.7, 0.8]),
        ["G"] = ([-1.2, -0.8, -0.5, 0.0, 0.7, 0.8], [0.0, 0.2, 0.7, 0.7, 0.7, 0.8]),
        ["H"] = ([-0.6, -0.3, -0.2, 0.0, 0.7, 0.8], [0.0, 0.2, 0.4, 0.6, 0.7, 0.8]),
        ["I"] = ([-0.6, -0.4, -0.4, -0.2, -0.2, -0.2], [-0.6, 0.0, 0.0, 0.0, -0.2, -0.2]),
        ["J"] = ([-0.6, -1.0, -0.5, -0.3, -0.3, -0.3], [0.2, 0.0, 0.0, 0.0, -0.3, -0.3]),
    };
    // Table 7.4b (duo-pitch, θ = 90°).
    static readonly Dictionary<string, double[]> Duo90 = new()
    {
        ["F"] = [-1.6, -1.3, -1.1, -1.1, -1.1, -1.1], ["G"] = [-1.3, -1.3, -1.4, -1.4, -1.2, -1.2],
        ["H"] = [-0.7, -0.6, -0.8, -0.9, -0.8, -0.8], ["I"] = [-0.6, -0.5, -0.5, -0.5, -0.5, -0.5],
    };
    // Table 7.3a (mono-pitch): θ = 0° (the wind on the low eave), two lines; θ = 180° (on the high one).
    static readonly Dictionary<string, (double[] Min, double[] Max)> Mono0 = new()
    {
        ["F"] = ([-1.7, -0.9, -0.5, 0.0, 0.7, 0.8], [0.0, 0.2, 0.7, 0.7, 0.7, 0.8]),
        ["G"] = ([-1.2, -0.8, -0.5, 0.0, 0.7, 0.8], [0.0, 0.2, 0.7, 0.7, 0.7, 0.8]),
        ["H"] = ([-0.6, -0.3, -0.2, 0.0, 0.7, 0.8], [0.0, 0.2, 0.4, 0.6, 0.7, 0.8]),
    };
    static readonly Dictionary<string, double[]> Mono180 = new()
    {
        ["F"] = [-2.3, -2.5, -1.1, -0.6, -0.5, -0.5], ["G"] = [-1.3, -1.3, -0.8, -0.6, -0.5, -0.5], ["H"] = [-0.8, -0.9, -0.8, -0.7, -0.5, -0.5],
    };
    // Table 7.3b (mono-pitch, θ = 90°).
    static readonly Dictionary<string, double[]> Mono90 = new()
    {
        ["Fup"] = [-2.1, -2.4, -2.1, -1.5, -1.2, -1.2], ["Flow"] = [-2.1, -1.6, -1.3, -1.3, -1.2, -1.2], ["G"] = [-1.8, -1.9, -1.5, -1.4, -1.2, -1.2],
        ["H"] = [-0.6, -0.8, -1.0, -1.0, -1.0, -1.0], ["I"] = [-0.5, -0.7, -0.8, -0.9, -0.7, -0.5],
    };

    /// <summary>EN 1991-1-4 Table 7.1: a vertical wall's cpe,10 in zone A–E for h/d (linear between 0.25, 1 and 5).</summary>
    public static double WallCpe(string zone, double hd)
    {
        double By(double at025, double at1, double at5) =>
            hd <= 0.25 ? at025 : hd <= 1 ? at025 + (at1 - at025) * (hd - 0.25) / 0.75 : hd <= 5 ? at1 + (at5 - at1) * (hd - 1) / 4 : at5;
        return zone switch { "A" => -1.2, "B" => -0.8, "C" => -0.5, "D" => By(0.7, 0.8, 0.8), _ => By(-0.3, -0.5, -0.7) };
    }

    /// <summary>EN 1991-1-4 7.2.2(3): the lack of correlation between the windward and leeward faces, 0.85 for h/d ≤ 1 to 1 for h/d ≥ 5.</summary>
    public static double Correlation(double hd) => hd <= 1 ? 0.85 : hd >= 5 ? 1 : 0.85 + 0.15 * (hd - 1) / 4;

    // ------------------------------------------------------------ wind

    sealed record Building(List<Vec2> Hull, double Ground, double Height, List<(Vec2 A, Vec2 B, List<Vec2> Facade)> Sides);

    /// <summary>The building as the wind sees it: the hull of its walls and columns, its ground, its height, each side's façade in (along, z).</summary>
    static Building? BuildingOf(AnalyticalModel model, AnalyticalContribution contributed, List<RoofSurface> surfaces, AnalyticalOptions opts)
    {
        var walls = contributed.Shells.Where(s => s.Kind == "wall" && s.Line is { Count: >= 2 } && opts.Keeps(s.SourceId)).ToList();
        var pts = walls.SelectMany(w => w.Line!).ToList();
        pts.AddRange(model.Frames.Where(f => f.Source.Kind == "column").SelectMany(f => new[] { f.I.At.Xy, f.J.At.Xy }));
        if (pts.Count < 3) pts.AddRange(model.Frames.SelectMany(f => new[] { f.I.At.Xy, f.J.At.Xy }));
        var hull = Polygon2.ConvexHull(pts).ToList();
        if (hull.Count < 3 || Math.Abs(Polygon2.SignedArea(hull)) < 0.5) return null;
        hull = Polygon2.EnsureCcw(hull).ToList();
        var zs = model.Nodes.Select(n => n.At.Z).Concat(walls.Select(w => w.Z0)).ToList();
        if (zs.Count == 0) return null;
        var ground = zs.Min();
        var tops = model.Nodes.Select(n => n.At.Z).Concat(walls.Select(w => w.Top is { Count: > 0 } t ? t.Max(p => p.Z) : w.Z1))
            .Concat(surfaces.SelectMany(s => s.Planes.SelectMany(p => p.Outline.Select(q => q.Z))));
        var height = tops.Max() - ground;
        if (height < 0.5) return null;
        // Each side's top along it: what roof, wall or member is over it, sampled every 25 cm and at the roof's corners.
        double TopAt(Vec2 p, Vec2 outward)
        {
            var q = p - outward * 0.05;
            var top = double.NegativeInfinity;
            foreach (var s in surfaces)
                foreach (var pl in s.Planes)
                    if (Polygon2.Contains(pl.Plan, q) || Enumerable.Range(0, pl.Plan.Count).Any(i => ShellPieces.DistToSegment(q, pl.Plan[i], pl.Plan[(i + 1) % pl.Plan.Count]) <= 0.1))
                        top = Math.Max(top, pl.ZAt(q));
            foreach (var w in walls)
                for (var i = 0; i + 1 < w.Line!.Count; i++)
                    if (ShellPieces.DistToSegment(q, w.Line[i], w.Line[i + 1]) <= w.Thickness / 2 + 0.3)
                    {
                        var zt = w.Z1;
                        if (w.Top is { Count: >= 2 } t)
                        {
                            var best = t.MinBy(x => (x.Xy - q).Length)!;
                            zt = best.Z;
                            for (var k = 0; k + 1 < t.Count; k++)
                            {
                                var ab = t[k + 1].Xy - t[k].Xy;
                                var l2 = Vec2.Dot(ab, ab);
                                if (l2 < 1e-12) continue;
                                var u = Vec2.Dot(q - t[k].Xy, ab) / l2;
                                if (u >= -1e-9 && u <= 1 + 1e-9) { zt = t[k].Z + (t[k + 1].Z - t[k].Z) * Math.Clamp(u, 0, 1); break; }
                            }
                        }
                        top = Math.Max(top, zt);
                    }
            foreach (var f in model.Frames)
            {
                var (a, b) = (f.I.At + f.OffI, f.J.At + f.OffJ);
                var ab = b.Xy - a.Xy;
                var l2 = Vec2.Dot(ab, ab);
                var t = l2 > 1e-12 ? Math.Clamp(Vec2.Dot(q - a.Xy, ab) / l2, 0, 1) : 0;
                if ((a.Xy + ab * t - q).Length <= 0.6) top = Math.Max(top, l2 > 1e-12 ? a.Z + (b.Z - a.Z) * t : Math.Max(a.Z, b.Z));
            }
            return top;
        }
        var sides = new List<(Vec2, Vec2, List<Vec2>)>();
        for (var i = 0; i < hull.Count; i++)
        {
            var (a, b) = (hull[i], hull[(i + 1) % hull.Count]);
            var len = (b - a).Length;
            if (len < 0.3) continue;
            var u = (b - a) * (1 / len);
            var outward = new Vec2(u.Y, -u.X);
            var stations = new List<double>();
            var n = Math.Max(1, (int)Math.Ceiling(len / 0.25));
            for (var k = 0; k <= n; k++) stations.Add(len * k / n);
            foreach (var s in surfaces) foreach (var pl in s.Planes) foreach (var c in pl.Plan)
                if (Math.Abs(Vec2.Dot(c - a, outward)) <= 1.0 && Vec2.Dot(c - a, u) is var sc && sc > 0 && sc < len) stations.Add(sc);
            stations = stations.Distinct().Order().ToList();
            var top = stations.Select(sv => (S: sv, Z: TopAt(a + u * sv, outward))).Where(x => double.IsFinite(x.Z) && x.Z > ground + 0.01).ToList();
            if (top.Count < 2) continue;
            var poly = new List<Vec2> { new(top[0].S, ground), new(top[^1].S, ground) };
            for (var k = top.Count - 1; k >= 0; k--) poly.Add(new Vec2(top[k].S, top[k].Z));
            var clean = ShellPieces.Clean(poly);
            if (clean.Count >= 3 && Polygon2.SignedArea(clean) > 0.01) sides.Add((a, b, clean));
        }
        return new Building(hull, ground, height, sides);
    }

    public static void Wind(IModelView m, List<LoadCase> cases, AnalyticalModel model, AnalyticalContribution contributed, List<RoofSurface> surfaces,
        List<DerivedLoad> derived, List<string> notes, AnalyticalOptions opts)
    {
        var windCases = cases.Where(c => c.Takes(LoadCases.Wind) && c.WindDirection != null).ToList();
        if (windCases.Count == 0) return;
        if (Climate.Wind(m) is not { } site)
        {
            notes.Add("No wind: the site's reference wind pressure qb is not set (Loads → Snow and wind, analysis.setClimate).");
            return;
        }
        if (BuildingOf(model, contributed, surfaces, opts) is not { } bld)
        {
            notes.Add("No wind: the model has no walls or columns to make a building's outline of.");
            return;
        }
        var project = Spatial.Project(m)?.Id ?? "project";
        var h = bld.Height;
        var qpRoof = site.Importance * site.Qp(h);
        var byCase = new Dictionary<string, List<DerivedLoad>>();
        var others = new HashSet<string>();
        foreach (var c in windCases)
        {
            var made = byCase[c.Id] = [];
            var dir = Climate.Vector(c.WindDirection!);
            var across = dir.Perp;
            var (sMin, sMax) = (bld.Hull.Min(p => Vec2.Dot(p, dir)), bld.Hull.Max(p => Vec2.Dot(p, dir)));
            var (tMin, tMax) = (bld.Hull.Min(p => Vec2.Dot(p, across)), bld.Hull.Max(p => Vec2.Dot(p, across)));
            var (depth, width) = (sMax - sMin, tMax - tMin);
            var e = Math.Min(width, 2 * h);
            var hd = depth > 1e-6 ? h / depth : 1;
            var maxSet = c.RoofCpe == "max";

            // The façades.
            for (var k = 0; k < bld.Sides.Count; k++)
            {
                var (a, b, facade) = bld.Sides[k];
                var u = (b - a).Normalized();
                var outward = new Vec2(u.Y, -u.X);
                var facing = Vec2.Dot(outward, dir);
                var zones = new List<(string Zone, List<Vec2> Poly)>();
                if (facing <= -0.5) zones.Add(("D", facade));
                else if (facing >= 0.5) zones.Add(("E", facade));
                else
                {
                    // Along the side, x from the windward edge: A to e/5, B to e, C past.
                    double X(double s) => Vec2.Dot(a + u * s, dir) - sMin;
                    var slope = X(1) - X(0);
                    List<Vec2> Between(double x0, double x1)
                    {
                        if (Math.Abs(slope) < 1e-9) return X(0) >= x0 && X(0) < x1 ? facade : [];
                        // x0 ≤ X(s) < x1 ⇔ s between the two stations, on whichever side the slope puts them.
                        var (s0, s1) = ((x0 - X(0)) / slope, (x1 - X(0)) / slope);
                        if (s0 > s1) (s0, s1) = (s1, s0);
                        var r = Tributary.Clip(facade, new Vec2(-1, 0), -s0);
                        return r.Count >= 3 ? Tributary.Clip(r, new Vec2(1, 0), s1) : [];
                    }
                    zones.Add(("A", Between(double.NegativeInfinity, e / 5)));
                    zones.Add(("B", Between(e / 5, e)));
                    zones.Add(("C", Between(e, double.PositiveInfinity)));
                }
                foreach (var (zone, poly) in zones)
                {
                    if (poly.Count < 3 || Math.Abs(Polygon2.SignedArea(poly)) < 1e-4) continue;
                    var cpe = WallCpe(zone, hd) * (zone is "D" or "E" ? Correlation(hd) : 1);
                    // The heights it is taken at (ze, Figure 7.4): h for a building no taller than wide, else bands.
                    foreach (var (z0, z1, ze) in Bands(h, width))
                    {
                        var band = Tributary.Clip(Tributary.Clip(poly, new Vec2(0, -1), -(bld.Ground + z0)), new Vec2(0, 1), bld.Ground + z1);
                        if (band.Count < 3 || Math.Abs(Polygon2.SignedArea(band)) < 1e-4) continue;
                        var w = site.Importance * site.Qp(ze) * cpe;
                        var force = cpe > 0 ? -outward : outward;
                        var pts3 = band.Select(p => (a + u * p.X).At(p.Y)).ToList();
                        made.Add(new DerivedLoad(LoadCases.Wind, c.Id, project, $"Façade {k + 1}, zone {zone}", null, "area", pts3.Select(p => p.Xy).ToList(), pts3.Min(p => p.Z),
                            Math.Abs(w), "kN/m²", 0)
                        {
                            Points3 = pts3, Direction = force.At(0), Key = $"wind:{c.Id}:facade{k + 1}:{zone}:{N(z0, 2)}", Projected = false,
                        });
                    }
                }
            }

            // The roofs.
            foreach (var s in surfaces)
            {
                var shape = s.Shape;
                if (shape == "other") { others.Add(s.Name); continue; }
                var plan = s.Planes.SelectMany(p => p.Plan).ToList();
                double Xof(Vec2 p) => Vec2.Dot(p, dir);
                double Yof(Vec2 p) => Vec2.Dot(p, across);
                var (yLo, yHi) = (plan.Min(Yof), plan.Max(Yof));
                List<Vec2> Rect(double x0, double x1, double y0, double y1) =>
                    [dir * x0 + across * y0, dir * x1 + across * y0, dir * x1 + across * y1, dir * x0 + across * y1];
                const double Far = 1e4;
                void Zone(RoofPlane p, int index, string zone, double cpe, List<Vec2> rect)
                {
                    var part = Tributary.ClipTo(p.Plan, rect);
                    if (part.Count < 3 || Math.Abs(Polygon2.SignedArea(part)) < 1e-4) return;
                    var w = qpRoof * cpe;
                    if (Math.Abs(w) < 1e-12) return;
                    var force = cpe > 0 ? -p.Normal : p.Normal;
                    var area = TrueArea(p, part);
                    made.Add(new DerivedLoad(LoadCases.Wind, c.Id, s.SourceId, $"{s.Name}, zone {zone}", s.StoreyId, "area", part, p.Outline[0].Z, Math.Abs(w), "kN/m²",
                        Math.Abs(w) * area * -force.Z)
                    {
                        Points3 = part.Select(p.Lift).ToList(), Direction = force, Key = $"wind:{c.Id}:{index + 1}:{zone}", Projected = false,
                    });
                }
                // The corners, the strip along the windward edge and what lies past it, from x0 (the edge) for a roof whose
                // planes all take them: F (e/4 wide at each end, e/10 deep), G between, H to e/2 (or to the plane's end), I past.
                void FromEdge(RoofPlane p, int index, double x0, Func<string, double> cpe, bool withI, string fLow = "F", string fHigh = "F", bool lowAtYLo = true)
                {
                    var (lo, hi) = lowAtYLo ? (fLow, fHigh) : (fHigh, fLow);
                    Zone(p, index, lo, cpe(lo), Rect(x0 - Far, x0 + e / 10, yLo - Far, yLo + e / 4));
                    Zone(p, index, hi, cpe(hi), Rect(x0 - Far, x0 + e / 10, yHi - e / 4, yHi + Far));
                    if (yHi - yLo > e / 2) Zone(p, index, "G", cpe("G"), Rect(x0 - Far, x0 + e / 10, yLo + e / 4, yHi - e / 4));
                    if (withI)
                    {
                        Zone(p, index, "H", cpe("H"), Rect(x0 + e / 10, x0 + e / 2, yLo - Far, yHi + Far));
                        Zone(p, index, "I", cpe("I"), Rect(x0 + e / 2, x0 + Far, yLo - Far, yHi + Far));
                    }
                    else Zone(p, index, "H", cpe("H"), Rect(x0 + e / 10, x0 + Far, yLo - Far, yHi + Far));
                }
                var x0All = plan.Min(Xof);
                switch (shape)
                {
                    case "flat":
                        for (var i = 0; i < s.Planes.Count; i++)
                            FromEdge(s.Planes[i], i, x0All, z => z switch { "F" => -1.8, "G" => -1.2, "H" => -0.7, _ => maxSet ? 0.2 : -0.2 }, true);
                        break;
                    case "duo":
                    {
                        var sloped = s.Planes.Select((p, i) => (P: p, I: i)).Where(x => x.P.Slope >= RoofSurface.FlatBelow).ToList();
                        var facing = Vec2.Dot(sloped[0].P.Downslope, dir);
                        if (Math.Abs(facing) >= 0.5)
                        {
                            // Square to the ridge (Table 7.4a): the plane falling towards the wind is windward.
                            var (windward, leeward) = facing < 0 ? (sloped[0], sloped[1]) : (sloped[1], sloped[0]);
                            double Cpe(RoofPlane p, string z) => maxSet ? At(Duo0[z].Max, p.Slope) : At(Duo0[z].Min, p.Slope);
                            FromEdge(windward.P, windward.I, windward.P.Plan.Min(Xof), z => Cpe(windward.P, z), false);
                            var ridge = leeward.P.Plan.Min(Xof);
                            Zone(leeward.P, leeward.I, "J", Cpe(leeward.P, "J"), Rect(ridge - Far, ridge + e / 10, yLo - Far, yHi + Far));
                            Zone(leeward.P, leeward.I, "I", Cpe(leeward.P, "I"), Rect(ridge + e / 10, ridge + Far, yLo - Far, yHi + Far));
                        }
                        else
                            // Along the ridge (Table 7.4b), both planes from the windward gable.
                            foreach (var (p, i) in sloped) FromEdge(p, i, x0All, z => At(Duo90[z], p.Slope), true);
                        break;
                    }
                    case "mono":
                    {
                        foreach (var (p, i) in s.Planes.Select((p, i) => (p, i)).Where(x => x.p.Slope >= RoofSurface.FlatBelow))
                        {
                            var facing = Vec2.Dot(p.Downslope, dir);
                            if (facing <= -0.5)
                                FromEdge(p, i, p.Plan.Min(Xof), z => maxSet ? At(Mono0[z].Max, p.Slope) : At(Mono0[z].Min, p.Slope), false);
                            else if (facing >= 0.5)
                                FromEdge(p, i, p.Plan.Min(Xof), z => At(Mono180[z], p.Slope), false);
                            else
                            {
                                // Along the slope (Table 7.3b): F up at the high side's corner, F low at the low side's.
                                var highAtYHi = Vec2.Dot(p.Downslope, across) < 0;
                                FromEdge(p, i, p.Plan.Min(Xof), z => At(Mono90[z], p.Slope), true, "Flow", "Fup", highAtYHi);
                            }
                        }
                        break;
                    }
                }
            }
        }
        // A pressure set that is its suction set here (no pitched roof): left out.
        foreach (var c in windCases.Where(c => c.RoofCpe == "max"))
        {
            var twin = windCases.FirstOrDefault(x => x.WindDirection == c.WindDirection && x.RoofCpe != "max");
            if (twin == null) continue;
            static string Sig(IEnumerable<DerivedLoad> l) => string.Join(";", l.Select(d => $"{d.SourceId}:{d.Key?[(d.Key.IndexOf(':', 5) + 1)..]}:{Math.Round(d.Value, 9)}:{d.Direction}"));
            if (Sig(byCase[c.Id]) == Sig(byCase[twin.Id]))
            {
                byCase[c.Id].Clear();
                notes.Add($"„{c.Code}” is „{twin.Code}” here (no roof whose pressure coefficients differ): left out.");
            }
        }
        foreach (var l in byCase.Values) derived.AddRange(l);
        notes.Add($"Wind: qp(ze) = ce(ze)·qb, ce from terrain category {site.Terrain} (z0 = {N(site.Z0)} m), qb = {N(site.Qb)} kN/m², γIw = {N(site.Importance)}; "
            + $"the building {N(h, 2)} m high from its lowest node, its outline the hull of its walls and columns; cpe,10 by EN 1991-1-4 Tables 7.1–7.4; {Climate.Indicative}.");
        notes.Add("Wind on the façades goes to the beams, rails and floor edges in their plane by vertical strips (half way to the next), its lowest strip to the ground; wind on a roof to its rafters or purlins as the snow does, per area of the roof.");
        if (others.Count > 0) notes.Add($"No wind derived on {string.Join(", ", others.Take(6))}{(others.Count > 6 ? "…" : "")}: hipped roofs and roofs of more than two planes (EN 1991-1-4 Table 7.5) are not made yet.");
        notes.Add("Not derived yet: the internal pressure cpi, friction, canopies and free-standing walls, the dynamic factor of tall or slender buildings.");
    }

    /// <summary>EN 1991-1-4 Figure 7.4: the height bands of a façade over the ground (from, to, ze): one for h ≤ b, two for h ≤ 2b, else strips of 3 m between.</summary>
    static List<(double Z0, double Z1, double Ze)> Bands(double h, double b)
    {
        if (h <= b) return [(-1e3, 1e3, h)];
        if (h <= 2 * b) return [(-1e3, b, b), (b, 1e3, h)];
        var o = new List<(double, double, double)> { (-1e3, b, b) };
        var n = Math.Max(1, (int)Math.Ceiling((h - 2 * b) / 3));
        for (var i = 0; i < n; i++)
        {
            var (z0, z1) = (b + (h - 2 * b) * i / n, b + (h - 2 * b) * (i + 1) / n);
            o.Add((z0, z1, z1));
        }
        o.Add((h - b, 1e3, h));
        return o;
    }
}
