using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// The terrain read in colours: its slopes, the cut and fill between the
/// surveyed ground and the ground as designed (the brush, the platforms, the
/// ditches), or its elevations in bands. Sampled on a grid over the terrain;
/// each cell takes its band's colour, in 3D as a film over the ground and in
/// plan as a fill; the legend tells each band's area.
/// </summary>
public static class Analysis
{
    public static readonly string[] Kinds = ["none", "slope", "cutfill", "elevation"];

    public sealed record Band(string Key, string Label, string Color, double UpTo);

    public static readonly Band[] SlopeBands =
    [
        new("flat", "0 – 5 %", "#3a8f4e", 5), new("gentle", "5 – 10 %", "#9ccc3c", 10), new("medium", "10 – 25 %", "#f2c53d", 25),
        new("steep", "25 – 50 %", "#ea8a2f", 50), new("verySteep", "over 50 %", "#c0392b", double.PositiveInfinity),
    ];

    public static readonly Band[] CutFillBands =
    [
        new("cut2", "cut over 1 m", "#8b1a1a", -1), new("cut1", "cut 0,5 – 1 m", "#d9534f", -0.5), new("cut0", "cut 0,1 – 0,5 m", "#f4a582", -0.1),
        new("level", "within 0,1 m", "#ededed", 0.1), new("fill0", "fill 0,1 – 0,5 m", "#92c5de", 0.5), new("fill1", "fill 0,5 – 1 m", "#4393c3", 1),
        new("fill2", "fill over 1 m", "#2166ac", double.PositiveInfinity),
    ];

    static readonly string[] ElevationColors = ["#2b6f3e", "#4f9b4a", "#9bc45a", "#e3d26a", "#d9a856", "#c27c47", "#a4573a", "#f2f2f2"];

    public sealed record Cell(Vec2 Min, Vec2 Max, double Value, double Z, Band Band);

    public sealed record Result(string Kind, IReadOnlyList<Band> Bands, IReadOnlyList<Cell> Cells, double CellArea);

    /// <summary>The bands of a kind (elevation bands span the ground from its lowest to its highest point).</summary>
    public static IReadOnlyList<Band> Bands(string kind, Terrain t)
    {
        if (kind == "slope") return SlopeBands;
        if (kind == "cutfill") return CutFillBands;
        var (lo, hi) = (t.Bounds.Min.Z, Math.Max(t.Bounds.Max.Z, t.Bounds.Min.Z + 0.01));
        var n = ElevationColors.Length;
        return Enumerable.Range(0, n).Select(i => new Band($"e{i}", $"{Standards.FormatLevel(lo + (hi - lo) * i / n)} … {Standards.FormatLevel(lo + (hi - lo) * (i + 1) / n)}", ElevationColors[i], i == n - 1 ? double.PositiveInfinity : lo + (hi - lo) * (i + 1) / n)).ToList();
    }

    public static Band BandOf(IReadOnlyList<Band> bands, double v) => bands.FirstOrDefault(b => v <= b.UpTo + 1e-9) ?? bands[^1];

    /// <summary>The ground as designed at a point: a platform's level inside it, a ditch's bottom along it, else the worked TIN.</summary>
    public static double? Proposed(IModelView m, Entity terrain, Terrain t, Vec2 p)
    {
        if (t.ElevationAt(p.X, p.Y) is not { } z) return null;
        foreach (var pad in Terrains.Pads(m, terrain.Id))
        {
            var o = Terrains.Contour(pad);
            if (o.Count >= 3 && Polygon2.Contains(o, p)) return pad.Num("elevation");
        }
        foreach (var d in Terrains.DitchesOf(m, terrain.Id))
        {
            var path = Ditches.Path(d);
            var half = Ditches.TopWidth(d) / 2;
            var (depth, bottom, _) = Ditches.Section(d);
            for (var i = 0; i + 1 < path.Count; i++)
            {
                var dist = DistanceToSegment(p, path[i], path[i + 1]);
                if (dist > half) continue;
                // The trapezoid: flat over the bottom, sloping up to the ground at the edge.
                var inner = bottom / 2;
                return z - (dist <= inner ? depth : depth * (1 - (dist - inner) / Math.Max(half - inner, 1e-9)));
            }
        }
        return z;
    }

    static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        var d = b - a;
        var l2 = Vec2.Dot(d, d);
        var t = l2 < 1e-12 ? 0 : Math.Clamp(Vec2.Dot(p - a, d) / l2, 0, 1);
        return (p - (a + d * t)).Length;
    }

    /// <summary>The terrain sampled for a kind of analysis: cells of about a sixtieth of its longer side, those over the ground.</summary>
    public static Result? Of(IModelView m, Entity terrain, string kind)
    {
        if (!Kinds.Contains(kind) || kind == "none") return null;
        var t = Terrains.Of(terrain);
        if (t.Tin.Triangles.Count == 0) return null;
        var existing = kind == "cutfill" ? Terrains.Existing(terrain) : null;
        var bands = Bands(kind, t);
        var (min, max) = t.Bounds;
        var span = Math.Max(max.X - min.X, max.Y - min.Y);
        var cell = Math.Max(span / 60, 0.25);
        var cells = new List<Cell>();
        for (var x = min.X; x < max.X; x += cell)
            for (var y = min.Y; y < max.Y; y += cell)
            {
                var c = new Vec2(x + cell / 2, y + cell / 2);
                if (Proposed(m, terrain, t, c) is not { } z) continue;
                double value;
                switch (kind)
                {
                    case "slope":
                    {
                        var h = cell / 2;
                        var zx1 = t.ElevationAt(c.X + h, c.Y) ?? z; var zx0 = t.ElevationAt(c.X - h, c.Y) ?? z;
                        var zy1 = t.ElevationAt(c.X, c.Y + h) ?? z; var zy0 = t.ElevationAt(c.X, c.Y - h) ?? z;
                        value = Math.Sqrt(Math.Pow((zx1 - zx0) / (2 * h), 2) + Math.Pow((zy1 - zy0) / (2 * h), 2)) * 100;
                        break;
                    }
                    case "cutfill": value = z - (existing!.ElevationAt(c.X, c.Y) ?? z); break;
                    default: value = z; break;
                }
                cells.Add(new Cell(new Vec2(x, y), new Vec2(x + cell, y + cell), value, z, BandOf(bands, value)));
            }
        return new Result(kind, bands, cells, cell * cell);
    }

    /// <summary>In 3D: a thin film over the ground in each band's colour, cell by cell.</summary>
    public static IEnumerable<GeometryPart> Overlay(Result r)
    {
        foreach (var band in r.Bands)
        {
            var solids = new List<Solid>();
            foreach (var c in r.Cells.Where(c => c.Band == band))
                if (Prism.ExtrudeFootprint([c.Min, new Vec2(c.Max.X, c.Min.Y), c.Max, new Vec2(c.Min.X, c.Max.Y)], c.Z + 0.02, 0.02, "analysis") is { } s) solids.Add(s);
            if (solids.Count > 0) yield return new GeometryPart(band.Label, band.Color, Paths.Combine(solids, "analysis"), null, "analysis:" + band.Key);
        }
    }

    /// <summary>In plan: each band's cells as fills, neighbours in a row merged into one rectangle.</summary>
    public static IEnumerable<(IReadOnlyList<IReadOnlyList<Vec2>> Loops, string Color)> Fills(Result r)
    {
        foreach (var band in r.Bands)
        {
            var loops = new List<IReadOnlyList<Vec2>>();
            foreach (var row in r.Cells.Where(c => c.Band == band).GroupBy(c => Math.Round(c.Min.Y, 6)))
            {
                var run = row.OrderBy(c => c.Min.X).ToList();
                var i = 0;
                while (i < run.Count)
                {
                    var j = i;
                    while (j + 1 < run.Count && Math.Abs(run[j + 1].Min.X - run[j].Max.X) < 1e-6) j++;
                    loops.Add([run[i].Min, new Vec2(run[j].Max.X, run[i].Min.Y), run[j].Max, new Vec2(run[i].Min.X, run[j].Max.Y)]);
                    i = j + 1;
                }
            }
            if (loops.Count > 0) yield return (loops, band.Color);
        }
    }

    /// <summary>The legend: each band with its colour and the area it covers.</summary>
    public static JsonObject Legend(Result r) => new()
    {
        ["kind"] = r.Kind,
        ["bands"] = new JsonArray(r.Bands.Select(b => (JsonNode)new JsonObject { ["key"] = b.Key, ["label"] = b.Label, ["color"] = b.Color, ["area"] = Math.Round(r.Cells.Count(c => c.Band == b) * r.CellArea, 2) }).ToArray()),
        ["area"] = Math.Round(r.Cells.Count * r.CellArea, 2),
    };
}
