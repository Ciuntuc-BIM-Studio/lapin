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
public sealed record CltSpec(double[] Plies, string Grade, double MaxLength, double MaxWidth, string Joint, double MinEdge, double Gap, string Direction)
{
    public static readonly string[] Joints = ["butt", "halfLap", "spline"];
    public static readonly string[] Directions = ["auto", "x", "y"];

    /// <summary>Five plies of 20, 20, 20, 20, 20 mm would be 100 mm: the usual 3 × 40 (120 mm) for a wall, 5 plies (160 mm) for a floor.</summary>
    public static CltSpec Wall => new([40, 40, 40], "C24", 16, 3.5, "halfLap", 0.3, 0.01, "auto");
    public static CltSpec Floor => new([40, 20, 40, 20, 40], "C24", 16, 3.5, "halfLap", 0.3, 0.01, "auto");

    public double Thickness => Plies.Sum() / 1000;

    public JsonObject ToJson() => new()
    {
        ["construction"] = "clt", ["plies"] = new JsonArray([.. Plies.Select(p => (JsonNode)p)]), ["grade"] = Grade, ["maxLength"] = MaxLength, ["maxWidth"] = MaxWidth,
        ["joint"] = Joint, ["minEdge"] = MinEdge, ["gap"] = Gap, ["direction"] = Direction,
    };

    public static CltSpec From(JsonNode? n, CltSpec d)
    {
        if (n is not JsonObject o) return d;
        return new CltSpec(
            (o["plies"] as JsonArray)?.Select(x => Json.Number(x) ?? 0).Where(x => x > 0).ToArray() is { Length: > 0 } p ? p : d.Plies,
            (string?)o["grade"] ?? d.Grade, Json.Number(o["maxLength"]) ?? d.MaxLength, Json.Number(o["maxWidth"]) ?? d.MaxWidth,
            Joints.Contains((string?)o["joint"] ?? "") ? (string)o["joint"]! : d.Joint, Json.Number(o["minEdge"]) ?? d.MinEdge, Json.Number(o["gap"]) ?? d.Gap,
            Directions.Contains((string?)o["direction"] ?? "") ? (string)o["direction"]! : d.Direction);
    }

    public static CltSpec? Of(Entity? type) => type?.Attrs[FrameSpec.Key] is JsonObject o && (string?)o["construction"] == "clt"
        ? From(o, type.Class == IfcClass.SlabType ? Floor : Wall) : null;

    public string? Problem()
    {
        if (Plies.Length is < 3 or > 9 || Plies.Length % 2 == 0) return "A CLT panel has 3, 5, 7 or 9 plies.";
        if (Plies.Any(p => p < 10 || p > 60)) return "A ply is 10 to 60 mm thick.";
        if (MaxLength < 2 || MaxLength > 30 || MaxWidth < 1 || MaxWidth > 4) return "A panel is at most 2 to 30 m long and 1 to 4 m wide.";
        if (MinEdge < 0 || MinEdge > 1 || Gap < 0 || Gap > 0.05) return "An opening keeps 0 to 1000 mm from a panel's edge; its fitting gap is 0 to 50 mm.";
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

    /// <summary>A panel cut: the core's body within a box (its own joins, openings and top already in it), less the rough openings.</summary>
    static Solid? Cut(ISolidBooleans booleans, Solid core, Solid box, IReadOnlyList<Solid> openings)
    {
        try
        {
            var piece = booleans.Intersection(core, box, "clt");
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
        var cutters = ros.Select(o => Box(o.L, o.R, o.B, o.T, core.From - 0.05, core.To + 0.05)).OfType<Solid>().ToList();
        var parts = g.Parts.Where(p => p.Key != corePart.Key && !(p.Key?.StartsWith("lintel", StringComparison.Ordinal) ?? false)).ToList();
        var edges = new List<double> { u0 - 0.01 }.Concat(joints).Append(u1 + 0.01).ToList();
        for (var i = 0; i + 1 < edges.Count; i++)
            if (Box(edges[i], edges[i + 1], z0 - 0.01, z1 + 0.01, core.From - 0.01, core.To + 0.01) is { } box && Cut(booleans, corePart.Solid, box, cutters) is { } panel && Measure.Volume(panel) > 1e-6)
                parts.Add(Part(wall.Name, i + 1, panel, spec, corePart.Material, Math.Min(edges[i + 1], u1) - Math.Max(edges[i], u0), z1 - z0));
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
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                var (a0, a1) = (Math.Min(span, c * spec.MaxLength), Math.Min(span, (c + 1) * spec.MaxLength));
                var (b0, b1) = (r * spec.MaxWidth, Math.Min(across, (r + 1) * spec.MaxWidth));
                var (bx0, bx1, by0, by1) = spanX ? (x0 + a0, x0 + a1, y0 + b0, y0 + b1) : (x0 + b0, x0 + b1, y0 + a0, y0 + a1);
                if (Prism.Box(new Vec3((bx0 + bx1) / 2, (by0 + by1) / 2, (z0 + z1) / 2), bx1 - bx0 + (c == 0 ? 0.02 : 0) + (c == cols - 1 ? 0.02 : 0), by1 - by0 + 0.02, z1 - z0 + 0.02, "box") is { } box
                    && Cut(booleans, corePart.Solid, box, []) is { } panel && Measure.Volume(panel) > 1e-6)
                    parts.Add(Part(slab.Name, ++n, panel, spec, corePart.Material, a1 - a0, b1 - b0));
            }
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
        foreach (var e in q.Model.Entities.Where(e => e.Class is IfcClass.Wall or IfcClass.Slab).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
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
