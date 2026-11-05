using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Wood;

/// <summary>
/// A CLT wall or floor type's rules (docs/plan-wood.md stage 5, decision 241), kept on the type as <c>wood</c> with
/// <c>construction: clt</c>: the build-up (each ply's thickness, the outer ones' grain along the panel's length), the grade,
/// the largest panel the press makes, the joint between panels, how far an opening keeps from a panel's edge, and the
/// fitting gaps round the openings. The joints are put where the panels may end; a panel is the core's own body within its
/// box, less its rough openings.
/// </summary>
public sealed record CltSpec(double[] Plies, string Grade, double MaxLength, double MaxWidth, string Joint, double MinEdge, double Gap, string Direction,
    string Corners = "square", double CornerRadius = 0.02, double LapWidth = 0.1, double SplineWidth = 0.12, double SplineDepth = 0.027)
{
    // Decision 246: the joint modelled (a half-lap's overlap, a spline's rebate and its plywood strip), the openings'
    // corners as the router leaves them ("square", "round": the cutter's radius left in the corner, "dogbone": overcut so
    // a square frame fits), and the same panels on roofs.
    public static readonly string[] Joints = ["butt", "halfLap", "spline"];
    public static readonly string[] CornerKinds = ["square", "round", "dogbone"];
    public static readonly string[] Directions = ["auto", "x", "y"];

    /// <summary>Five plies of 20, 20, 20, 20, 20 mm would be 100 mm: the usual 3 × 40 (120 mm) for a wall, 5 plies (160 mm) for a floor.</summary>
    public static CltSpec Wall => new([40, 40, 40], "C24", 16, 3.5, "halfLap", 0.3, 0.01, "auto");
    public static CltSpec Floor => new([40, 20, 40, 20, 40], "C24", 16, 3.5, "halfLap", 0.3, 0.01, "auto");

    public double Thickness => Plies.Sum() / 1000;

    public JsonObject ToJson() => new()
    {
        ["construction"] = "clt", ["plies"] = new JsonArray([.. Plies.Select(p => (JsonNode)p)]), ["grade"] = Grade, ["maxLength"] = MaxLength, ["maxWidth"] = MaxWidth,
        ["joint"] = Joint, ["minEdge"] = MinEdge, ["gap"] = Gap, ["direction"] = Direction,
        ["corners"] = Corners, ["cornerRadius"] = CornerRadius, ["lapWidth"] = LapWidth, ["splineWidth"] = SplineWidth, ["splineDepth"] = SplineDepth,
    };

    public static CltSpec From(JsonNode? n, CltSpec d)
    {
        if (n is not JsonObject o) return d;
        return new CltSpec(
            (o["plies"] as JsonArray)?.Select(x => Json.Number(x) ?? 0).Where(x => x > 0).ToArray() is { Length: > 0 } p ? p : d.Plies,
            (string?)o["grade"] ?? d.Grade, Json.Number(o["maxLength"]) ?? d.MaxLength, Json.Number(o["maxWidth"]) ?? d.MaxWidth,
            Joints.Contains((string?)o["joint"] ?? "") ? (string)o["joint"]! : d.Joint, Json.Number(o["minEdge"]) ?? d.MinEdge, Json.Number(o["gap"]) ?? d.Gap,
            Directions.Contains((string?)o["direction"] ?? "") ? (string)o["direction"]! : d.Direction,
            CornerKinds.Contains((string?)o["corners"] ?? "") ? (string)o["corners"]! : d.Corners, Json.Number(o["cornerRadius"]) ?? d.CornerRadius,
            Json.Number(o["lapWidth"]) ?? d.LapWidth, Json.Number(o["splineWidth"]) ?? d.SplineWidth, Json.Number(o["splineDepth"]) ?? d.SplineDepth);
    }

    /// <summary>A roof's CLT, kept on the roof itself (roofs have no types): its spec, else null.</summary>
    public const string RoofKey = "woodClt";
    public static CltSpec? OfRoof(Entity roof) => roof.Attrs[RoofKey] is JsonObject o ? From(o, Floor) : null;

    public static CltSpec? Of(Entity? type) => type?.Attrs[FrameSpec.Key] is JsonObject o && (string?)o["construction"] == "clt"
        ? From(o, type.Class == IfcClass.SlabType ? Floor : Wall) : null;

    public string? Problem()
    {
        if (Plies.Length is < 3 or > 9 || Plies.Length % 2 == 0) return "A CLT panel has 3, 5, 7 or 9 plies.";
        if (Plies.Any(p => p < 10 || p > 60)) return "A ply is 10 to 60 mm thick.";
        if (MaxLength < 2 || MaxLength > 30 || MaxWidth < 1 || MaxWidth > 4) return "A panel is at most 2 to 30 m long and 1 to 4 m wide.";
        if (MinEdge < 0 || MinEdge > 1 || Gap < 0 || Gap > 0.05) return "An opening keeps 0 to 1000 mm from a panel's edge; its fitting gap is 0 to 50 mm.";
        if (CornerRadius < 0.003 || CornerRadius > 0.1) return "The router's radius is 3 to 100 mm.";
        if (LapWidth < 0.02 || LapWidth > 0.3) return "A half-lap overlaps 20 to 300 mm.";
        if (SplineWidth < 0.04 || SplineWidth > 0.3 || SplineDepth < 0.009 || SplineDepth > Thickness / 2) return "A spline is 40 to 300 mm wide and 9 mm to half the panel deep.";
        return null;
    }

    /// <summary>The plies as a label: "5 × 20/40/20/40/20".</summary>
    public string Label => $"{Plies.Length} plies {string.Join("/", Plies.Select(p => p.ToString("0", CultureInfo.InvariantCulture)))} mm";
}

public static class Clt
{
    /// <summary>
    /// Where the panels of a run from <paramref name="u0"/> to <paramref name="u1"/> meet: none past the longest panel, none
    /// within <c>MinEdge</c> of a rough opening; a joint that would fall there moves back before it (or past it, when that
    /// would leave a panel shorter than 0.6 m). Warnings for an opening wider than a panel.
    /// </summary>
    public static (List<double> Joints, List<string> Warnings) Joints(CltSpec spec, double u0, double u1, IReadOnlyList<(string Id, double L, double R)> openings)
    {
        var joints = new List<double>();
        var warnings = new List<string>();
        var zones = openings.Select(o => (o.Id, L: o.L - spec.MinEdge, R: o.R + spec.MinEdge)).OrderBy(z => z.L).ToList();
        foreach (var z in zones) if (z.R - z.L > spec.MaxLength) warnings.Add($"{z.Id}: wider than a panel, with its edges, can be.");
        var at = u0;
        while (u1 - at > spec.MaxLength + 1e-9)
        {
            var next = at + spec.MaxLength;
            foreach (var z in zones)
                if (next > z.L && next < z.R)
                    next = z.L - at >= 0.6 ? z.L : Math.Min(z.R, at + spec.MaxLength);
            if (next <= at + 1e-6) next = at + spec.MaxLength;
            joints.Add(next);
            at = next;
        }
        return (joints, warnings);
    }

    /// <summary>
    /// A panel's cross-section as slices between its joints (decision 246): along the run, from its left joint (or
    /// <paramref name="lo"/>) to its right one (or <paramref name="hi"/>), through the thickness from <paramref name="t0"/> to
    /// <paramref name="t1"/>. A butt joint is one plane; a half-lap's outer half (<c>t0</c> side) runs half the lap past it
    /// and its inner half stops half the lap short, so the next panel's lap rests on it; a spline's rebate stops each
    /// panel half the strip short on the <c>t0</c> face, the strip (<see cref="Splines"/>) in it.
    /// </summary>
    public static List<(double A, double B, double T0, double T1)> Slices(CltSpec spec, double? left, double? right, double lo, double hi, double t0, double t1, bool rebateOnT1 = false)
    {
        var levels = new List<double> { t0, t1 };
        if (spec.Joint == "halfLap") levels.Add((t0 + t1) / 2);
        if (spec.Joint == "spline") levels.Add(rebateOnT1 ? t1 - spec.SplineDepth : t0 + spec.SplineDepth);
        levels = [.. levels.Distinct().OrderBy(x => x)];
        (double End, double Start) At(double j, double t) => spec.Joint switch
        {
            "halfLap" => t < (t0 + t1) / 2 ? (j + spec.LapWidth / 2, j + spec.LapWidth / 2) : (j - spec.LapWidth / 2, j - spec.LapWidth / 2),
            "spline" => (rebateOnT1 ? t > t1 - spec.SplineDepth : t < t0 + spec.SplineDepth) ? (j - spec.SplineWidth / 2, j + spec.SplineWidth / 2) : (j, j),
            _ => (j, j),
        };
        var o = new List<(double, double, double, double)>();
        for (var i = 0; i + 1 < levels.Count; i++)
        {
            var tm = (levels[i] + levels[i + 1]) / 2;
            var a = left is { } l ? At(l, tm).Start : lo;
            var b = right is { } r ? At(r, tm).End : hi;
            if (b - a > 1e-6) o.Add((a, b, levels[i], levels[i + 1]));
        }
        return o;
    }

    /// <summary>The slices as one stepped outline in the (run, thickness) plane: up the left ends, down the right ones.</summary>
    public static List<(double A, double T)> Outline(IReadOnlyList<(double A, double B, double T0, double T1)> slices)
    {
        var s = slices.OrderBy(x => x.T0).ToList();
        var o = new List<(double A, double T)>();
        void Add(double a, double t) { if (o.Count == 0 || Math.Abs(o[^1].A - a) > 1e-9 || Math.Abs(o[^1].T - t) > 1e-9) o.Add((a, t)); }
        foreach (var x in s) { Add(x.A, x.T0); Add(x.A, x.T1); }
        for (var i = s.Count - 1; i >= 0; i--) { Add(s[i].B, s[i].T1); Add(s[i].B, s[i].T0); }
        // Drop the points in the middle of a straight run.
        for (var i = o.Count - 1; i >= 0 && o.Count > 4; i--)
        {
            var (p, q, r) = (o[(i - 1 + o.Count) % o.Count], o[i], o[(i + 1) % o.Count]);
            if (Math.Abs((q.A - p.A) * (r.T - p.T) - (q.T - p.T) * (r.A - p.A)) < 1e-12) o.RemoveAt(i);
        }
        return o;
    }

    /// <summary>A spline joint's plywood strip in its rebate: along the run round the joint, through the thickness from <paramref name="t0"/> (the rebate's floor or face).</summary>
    public static (double A, double B, double T0, double T1)? Splines(CltSpec spec, double joint, double t0) =>
        spec.Joint == "spline" ? (joint - spec.SplineWidth / 2, joint + spec.SplineWidth / 2, t0, t0 + spec.SplineDepth) : null;

    /// <summary>
    /// A rough opening's cutter in the wall's plane, as (u, z) loops: the rectangle; with <c>round</c> corners, the
    /// router's radius left in each corner; with <c>dogbone</c>, the rectangle and a circle at each corner, centred inside
    /// it on the diagonal so it passes through the corner.
    /// </summary>
    public static List<List<(double U, double Z)>> Cutters(CltSpec spec, double l, double r, double b, double t)
    {
        List<(double, double)> Rect() => [(l, b), (r, b), (r, t), (l, t)];
        var rad = Math.Min(spec.CornerRadius, Math.Min(r - l, t - b) / 2 - 1e-4);
        if (spec.Corners == "square" || rad <= 0.001) return [Rect()];
        const int steps = 8;
        if (spec.Corners == "round")
        {
            var loop = new List<(double, double)>();
            foreach (var (cu, cz, a0) in new[] { (r - rad, b + rad, -90.0), (r - rad, t - rad, 0.0), (l + rad, t - rad, 90.0), (l + rad, b + rad, 180.0) })
                for (var k = 0; k <= steps; k++)
                {
                    var a = (a0 + 90.0 * k / steps) * Math.PI / 180;
                    loop.Add((cu + rad * Math.Cos(a), cz + rad * Math.Sin(a)));
                }
            return [loop];
        }
        var o = new List<List<(double, double)>> { Rect() };
        var d = rad / Math.Sqrt(2);
        foreach (var (cu, cz) in new[] { (l + d, b + d), (r - d, b + d), (r - d, t - d), (l + d, t - d) })
            o.Add([.. Enumerable.Range(0, 16).Select(k => (cu + rad * Math.Cos(k * Math.PI / 8), cz + rad * Math.Sin(k * Math.PI / 8)))]);
        return o;
    }

    /// <summary>A panel cut: the core's body within a box (its own joins, openings and top already in it), less the rough openings.</summary>
    static Solid? Cut(ISolidBooleans booleans, Solid core, IReadOnlyList<Solid> boxes, IReadOnlyList<Solid> openings)
    {
        try
        {
            if (boxes.Count == 0) return null;
            var region = boxes.Count == 1 ? boxes[0] : booleans.Union(boxes, "clt-box");
            if (region == null) return null;
            var piece = booleans.Intersection(core, region, "clt");
            if (piece == null || openings.Count == 0) return piece;
            return booleans.Difference(piece, openings, "clt");
        }
        catch (InvalidOperationException) { return null; }
    }

    static GeometryPart Part(string element, int n, Solid s, CltSpec spec, string? material, double length, double width) =>
        new($"CLT panel {element}-{n}", "#d9b27c", s, material, $"clt:{n}")
        {
            PropertySet = "Lapin_CLT",
            Properties =
            [
                ("Mark", $"{element}-{n}"), ("Plies", spec.Label), ("Grade", spec.Grade), ("Joint", spec.Joint),
                ("Length", Math.Round(length, 3).ToString(CultureInfo.InvariantCulture)), ("Width", Math.Round(width, 3).ToString(CultureInfo.InvariantCulture)),
                ("Thickness", Math.Round(spec.Thickness, 3).ToString(CultureInfo.InvariantCulture)),
                ("Area", Math.Round(Measure.Volume(s) / Math.Max(1e-9, spec.Thickness), 3).ToString(CultureInfo.InvariantCulture)),
            ],
        };

    /// <summary>A slice's face on the thickness's outer face moved 10 mm out, so the element's own body bounds it.</summary>
    static double Pad(double t, double t0, double t1) => Math.Abs(t - t0) < 1e-9 ? t0 - 0.01 : Math.Abs(t - t1) < 1e-9 ? t1 + 0.01 : t;

    static GeometryPart Spline(string element, int n, Solid s, CltSpec spec) =>
        new($"Spline {element}-{n}", "#c8a777", s, null, $"clt-spline:{n}")
        {
            PropertySet = "Lapin_CLT",
            Properties = [("Mark", $"{element}-S{n}"), ("Joint", "spline"), ("Section", $"{Math.Round(spec.SplineWidth * 1000)}x{Math.Round(spec.SplineDepth * 1000)} mm"), ("Material", "Plywood")],
        };

    /// <summary>
    /// A CLT roof (decision 246): on each plane, in place of its rafters and trusses (the wall plates kept), a plate of the
    /// panels' thickness on the plane, its roof windows' and dormers' holes cut, laid in strips the panel's width wide
    /// along the eave, each strip running down the slope and joined end to end where the slope is longer than a panel.
    /// </summary>
    public static ElementGeometry? Roof(Entity roof, ElementGeometry g, IModelView m, IServices services, CltSpec spec)
    {
        if (services.Get<ISolidBooleans>() is not { } booleans || Lapin.Architecture.Roofs.Roof.Resolve(roof, m) is not { Flat: false, Barrel: false } r) return null;
        if (Lapin.Architecture.Roofs.Roof.Solve(r).Shape is not { } shape) return null;
        var openings = Lapin.Architecture.Roofs.RoofOpenings.Place(roof, shape, m, []);
        // The CLT in place of the rafters and trusses (the plates kept), or of a deck (decision 329).
        var parts = g.Parts.Where(p => p.Key != "deck" && !(p.Key ?? "").StartsWith("truss:", StringComparison.Ordinal) && !((p.Key ?? "").StartsWith("timber:", StringComparison.Ordinal) && !(p.Key ?? "").StartsWith("timber:plate", StringComparison.Ordinal))).ToList();
        var warnings = new List<string>();
        if (spec.Thickness > r.StructureDepth + 1e-6) warnings.Add(r.Framed
            ? $"The CLT ({Math.Round(spec.Thickness * 1000)} mm) is deeper than the roof's structure ({Math.Round(r.StructureDepth * 1000)} mm): set the rafters' depth to it."
            : $"The CLT ({Math.Round(spec.Thickness * 1000)} mm) is deeper than the roof's deck ({Math.Round(r.StructureDepth * 1000)} mm): set the deck's thickness to it.");
        var n = 0;
        for (var i = 0; i < shape.Planes.Count; i++)
        {
            var p = shape.Planes[i];
            var cos = Math.Cos(p.Pitch * Math.PI / 180);
            var holes = openings.Where(o => o.Host.PlaneIndex == i).Select(o => (IReadOnlyList<Vec3>)o.Hole.ToArray()).ToList();
            if (Prism.Extrude(p.Points.ToArray(), holes, new Vec3(0, 0, spec.Thickness / cos), "clt") is not { } plate) continue;
            var (a, b) = (shape.Outline[p.Edge], shape.Outline[(p.Edge + 1) % shape.Outline.Count]);
            var d = (b - a).Normalized();
            var nrm = d.Perp;
            var (s0, s1) = (p.Points.Min(q => Vec2.Dot(q.Xy, d)), p.Points.Max(q => Vec2.Dot(q.Xy, d)));
            var (f0, f1) = (p.Points.Min(q => Vec2.Dot(q.Xy, nrm)), p.Points.Max(q => Vec2.Dot(q.Xy, nrm)));
            var (zl, zh) = (p.Points.Min(q => q.Z) - 1, p.Points.Max(q => q.Z) + spec.Thickness / cos + 1);
            var slope = (f1 - f0) / Math.Max(1e-6, cos);
            if (slope > spec.MaxLength + 1e-6) warnings.Add($"A slope is {Math.Round(slope, 2)} m long, more than a panel's {spec.MaxLength} m: its panels are joined end to end.");
            var step = spec.MaxLength * cos;
            for (var s = s0; s < s1 - 1e-6; s += spec.MaxWidth)
                for (var f = f0; f < f1 - 1e-6; f += step)
                {
                    var (sa, sb, fa, fb) = (s - (s <= s0 ? 0.05 : 0), Math.Min(s1, s + spec.MaxWidth) + (s + spec.MaxWidth >= s1 ? 0.05 : 0), f - (f <= f0 ? 0.05 : 0), Math.Min(f1, f + step) + (f + step >= f1 ? 0.05 : 0));
                    Vec3 P(double u, double v, double z) { var q = d * u + nrm * v; return new Vec3(q.X, q.Y, z); }
                    if (Prism.Extrude([P(sa, fa, zl), P(sb, fa, zl), P(sb, fb, zl), P(sa, fb, zl)], null, new Vec3(0, 0, zh - zl), "box") is not { } box) continue;
                    if (Cut(booleans, plate, [box], []) is { } panel && Measure.Volume(panel) > 1e-6)
                        parts.Add(Part(roof.Name, ++n, panel, spec, null, (Math.Min(f1, f + step) - f) / cos, Math.Min(s1, s + spec.MaxWidth) - s));
                }
        }
        return g with { Parts = parts, Diagnostics = warnings.Count > 0 ? [.. g.Diagnostics ?? [], .. warnings] : g.Diagnostics };
    }

    /// <summary>A CLT wall's core in panels along it; its lintels (a masonry rule's) gone.</summary>
    public static ElementGeometry? Wall(Entity wall, ElementGeometry g, IModelView m, IServices services, CltSpec spec)
    {
        var bands = Walls.Section(m, wall);
        if (!bands.Any(b => b.Core) || PathCurve.Of(wall) is { IsArc: true } || services.Get<ISolidBooleans>() is not { } booleans) return null;
        var core = bands.First(b => b.Core);
        var corePart = g.Parts.FirstOrDefault(p => p.Key == $"layer:{core.Index}");
        if (corePart == null) return null;
        var start = Walls.Start(wall);
        var dir = (Walls.End(wall) - start).Normalized();
        var left = dir.Perp;
        var baseZ = (Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");
        Vec3 At(double u, double z, double y) { var p = start + dir * u + left * y; return new Vec3(p.X, p.Y, baseZ + z); }
        var pts = corePart.Solid.Vertices.Select(v => (U: Vec2.Dot(v.Xy - start, dir), Z: v.Z - baseZ)).ToList();
        var (u0, u1, z0, z1) = (pts.Min(p => p.U), pts.Max(p => p.U), pts.Min(p => p.Z), pts.Max(p => p.Z));
        var placed = Openings.PlacedOn(m, wall.Id);
        var ros = placed.Select(p => (Id: p.Fill?.Name ?? "An opening", L: p.Along - p.Width / 2 - spec.Gap, R: p.Along + p.Width / 2 + spec.Gap,
            B: p.Sill - (p.Fill?.Class == IfcClass.Door || p.Sill <= 1e-6 ? 0.05 : spec.Gap), T: p.Sill + p.Height + spec.Gap)).ToList();
        var (joints, warnings) = Joints(spec, u0, u1, ros.Select(o => (o.Id, o.L, o.R)).ToList());
        if (Math.Abs(core.To - core.From - spec.Thickness) > 0.003) warnings.Add($"The core is {Math.Round((core.To - core.From) * 1000)} mm thick and the panel {Math.Round(spec.Thickness * 1000)} mm.");
        if (z1 - z0 > spec.MaxWidth + 1e-6) warnings.Add($"The wall is {Math.Round((z1 - z0) * 1000)} mm high, more than a panel's {Math.Round(spec.MaxWidth * 1000)} mm.");
        Solid? Box(double a, double b, double za, double zb, double ya, double yb) =>
            Prism.Extrude([At(a, za, ya), At(b, za, ya), At(b, zb, ya), At(a, zb, ya)], null, At(0, 0, yb) - At(0, 0, ya), "box");
        var cutters = ros.SelectMany(o => Cutters(spec, o.L, o.R, o.B, o.T))
            .Select(loop => Prism.Extrude([.. loop.Select(p => At(p.U, p.Z, core.From - 0.05))], null, At(0, 0, core.To + 0.05) - At(0, 0, core.From - 0.05), "box")).OfType<Solid>().ToList();
        var parts = g.Parts.Where(p => p.Key != corePart.Key && !(p.Key?.StartsWith("lintel", StringComparison.Ordinal) ?? false)).ToList();
        var edges = new List<double> { u0 - 0.01 }.Concat(joints).Append(u1 + 0.01).ToList();
        for (var i = 0; i + 1 < edges.Count; i++)
        {
            var slices = Slices(spec, i == 0 ? null : edges[i], i + 2 == edges.Count ? null : edges[i + 1], u0 - 0.01, u1 + 0.01, core.From, core.To);
            // The outer slices reach past the core's faces, so the core's own body bounds them.
            var outline = Outline([.. slices.Select(s => (s.A, s.B, Pad(s.T0, core.From, core.To), Pad(s.T1, core.From, core.To)))]);
            var boxes = Prism.Extrude([.. outline.Select(p => At(p.A, z0 - 0.01, p.T))], null, new Vec3(0, 0, z1 - z0 + 0.02), "box") is { } prism ? new List<Solid> { prism } : [];
            if (Cut(booleans, corePart.Solid, boxes, cutters) is { } panel && Measure.Volume(panel) > 1e-6)
                parts.Add(Part(wall.Name, i + 1, panel, spec, corePart.Material, Math.Min(edges[i + 1], u1) - Math.Max(edges[i], u0), z1 - z0));
        }
        var k = 0;
        foreach (var j in joints)
            if (Splines(spec, j, core.From) is { } sp && Box(sp.A, sp.B, z0 - 0.01, z1 + 0.01, sp.T0 - 0.01, sp.T1) is { } sbox && Cut(booleans, corePart.Solid, [sbox], cutters) is { } strip)
                parts.Add(Spline(wall.Name, ++k, strip, spec));
        return g with { Parts = parts, Diagnostics = warnings.Count > 0 ? [.. g.Diagnostics ?? [], .. warnings] : g.Diagnostics };
    }

    /// <summary>A CLT floor's core in strips across its span, each at most the panel's width wide and its length long.</summary>
    public static ElementGeometry? Floor(Entity slab, ElementGeometry g, IModelView m, IServices services, CltSpec spec)
    {
        if (services.Get<ISolidBooleans>() is not { } booleans || LayerSets.BandsOf(m, slab.Id) is not { } bands || !bands.Any(b => b.Core)) return null;
        var core = bands.First(b => b.Core);
        var corePart = g.Parts.FirstOrDefault(p => p.Key == $"layer:{core.Index}");
        if (corePart == null) return null;
        var v = corePart.Solid.Vertices;
        var (x0, x1, y0, y1, z0, z1) = (v.Min(p => p.X), v.Max(p => p.X), v.Min(p => p.Y), v.Max(p => p.Y), v.Min(p => p.Z), v.Max(p => p.Z));
        // The panels span the short way unless told: their length along the span, laid side by side across it.
        var spanX = spec.Direction == "x" || spec.Direction == "auto" && x1 - x0 <= y1 - y0;
        var (span, across) = spanX ? (x1 - x0, y1 - y0) : (y1 - y0, x1 - x0);
        var warnings = new List<string>();
        if (span > spec.MaxLength + 1e-6) warnings.Add($"The floor spans {Math.Round(span, 2)} m, more than a panel's {spec.MaxLength} m: its panels are joined end to end.");
        if (Math.Abs(z1 - z0 - spec.Thickness) > 0.003) warnings.Add($"The core is {Math.Round((z1 - z0) * 1000)} mm thick and the panel {Math.Round(spec.Thickness * 1000)} mm.");
        var parts = g.Parts.Where(p => p.Key != corePart.Key).ToList();
        var n = 0;
        var rows = Math.Max(1, (int)Math.Ceiling(across / spec.MaxWidth - 1e-9));
        var cols = Math.Max(1, (int)Math.Ceiling(span / spec.MaxLength - 1e-9));
        // The long edges between strips take the joint (a spline in a rebate on the top face); the ends meet butt.
        Solid? Box(double a0, double a1, double b0, double b1, double t0, double t1)
        {
            var (bx0, bx1, by0, by1) = spanX ? (x0 + a0, x0 + a1, y0 + b0, y0 + b1) : (x0 + b0, x0 + b1, y0 + a0, y0 + a1);
            return Prism.Box(new Vec3((bx0 + bx1) / 2, (by0 + by1) / 2, (t0 + t1) / 2), bx1 - bx0, by1 - by0, t1 - t0, "box");
        }
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                var (a0, a1) = (Math.Min(span, c * spec.MaxLength), Math.Min(span, (c + 1) * spec.MaxLength));
                var (b0, b1) = (r * spec.MaxWidth, Math.Min(across, (r + 1) * spec.MaxWidth));
                var slices = Slices(spec, r == 0 ? null : b0, r == rows - 1 ? null : b1, -0.01, across + 0.01, z0, z1, rebateOnT1: true);
                var outline = Outline([.. slices.Select(s => (s.A, s.B, Pad(s.T0, z0, z1), Pad(s.T1, z0, z1)))]);
                var (sa, sb) = (a0 - (c == 0 ? 0.01 : 0), a1 + (c == cols - 1 ? 0.01 : 0));
                Vec3 W(double sp, double ac, double z) => spanX ? new Vec3(x0 + sp, y0 + ac, z) : new Vec3(x0 + ac, y0 + sp, z);
                var boxes = Prism.Extrude([.. outline.Select(p => W(sa, p.A, p.T))], null, W(sb, 0, 0) - W(sa, 0, 0), "box") is { } prism ? new List<Solid> { prism } : [];
                if (Cut(booleans, corePart.Solid, boxes, []) is { } panel && Measure.Volume(panel) > 1e-6)
                    parts.Add(Part(slab.Name, ++n, panel, spec, corePart.Material, a1 - a0, b1 - b0));
            }
        var k = 0;
        for (var r = 1; r < rows; r++)
            if (Splines(spec, r * spec.MaxWidth, z1 - spec.SplineDepth) is { } sp && Box(-0.01, span + 0.01, sp.A, sp.B, sp.T0, sp.T1 + 0.01) is { } sbox && Cut(booleans, corePart.Solid, [sbox], []) is { } strip)
                parts.Add(Spline(slab.Name, ++k, strip, spec));
        return g with { Parts = parts, Diagnostics = warnings.Count > 0 ? [.. g.Diagnostics ?? [], .. warnings] : g.Diagnostics };
    }
}

/// <summary>The CLT panels of the project (decision 241): mark, element, length, width, thickness, plies, grade, joint, area and volume.</summary>
public sealed class CltSchedule : IScheduleSource
{
    public string Kind => "clt";
    public string Label => "CLT panels";

    public ScheduleGrid Grid(IQueryContext q)
    {
        string P(GeometryPart p, string n) => (p.Properties ?? []).FirstOrDefault(x => x.Name == n).Value ?? "";
        var rows = new List<IReadOnlyList<string>>();
        double area = 0, volume = 0;
        foreach (var e in q.Model.Entities.Where(e => e.Class is IfcClass.Wall or IfcClass.Slab or IfcClass.Roof).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            foreach (var p in q.Geometry(e.Id)?.Parts.Where(x => x.Key?.StartsWith("clt:", StringComparison.Ordinal) == true) ?? [])
            {
                var vol = Measure.Volume(p.Solid);
                var a = double.TryParse(P(p, "Area"), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : 0;
                (area, volume) = (area + a, volume + vol);
                rows.Add([P(p, "Mark"), e.Name, P(p, "Length"), P(p, "Width"), P(p, "Thickness"), P(p, "Plies"), P(p, "Grade"), P(p, "Joint"), a.ToString("0.00", CultureInfo.InvariantCulture), vol.ToString("0.000", CultureInfo.InvariantCulture)]);
            }
        if (rows.Count > 0) rows.Add(["Total", "", "", "", "", "", "", "", area.ToString("0.00", CultureInfo.InvariantCulture), volume.ToString("0.000", CultureInfo.InvariantCulture)]);
        return new ScheduleGrid(Label, ["Mark", "Element", "Length (m)", "Width (m)", "Thickness (m)", "Plies", "Grade", "Joint", "Area (m²)", "Volume (m³)"], rows);
    }
}
