using System.Text.Json.Nodes;
using Lapin.Geometry;
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
    public static IReadOnlyList<Band> Bands(string kind, Terrain t, LevelDatum? datum = null)
    {
        // As the project's datum writes a level (decision 867), two decimals in metres.
        string L(double z) => datum?.Text(z, null) ?? LevelDatum.Number(z, null);
        if (kind == "slope") return SlopeBands;
        if (kind == "cutfill") return CutFillBands;
        var (lo, hi) = (t.Bounds.Min.Z, Math.Max(t.Bounds.Max.Z, t.Bounds.Min.Z + 0.01));
        var n = ElevationColors.Length;
        return Enumerable.Range(0, n).Select(i => new Band($"e{i}", $"{L(lo + (hi - lo) * i / n)} … {L(lo + (hi - lo) * (i + 1) / n)}", ElevationColors[i], i == n - 1 ? double.PositiveInfinity : lo + (hi - lo) * (i + 1) / n)).ToList();
    }

    public static Band BandOf(IReadOnlyList<Band> bands, double v) => bands.FirstOrDefault(b => v <= b.UpTo + 1e-9) ?? bands[^1];

    /// <summary>
    /// The ground as designed (the worked TIN, then what the platforms and the ditches make of it), read as the terrain's
    /// solid is built: a platform's level inside it and its cut and fill slopes round it (<see cref="Terrains.PadShape"/>),
    /// a ditch's trapezoid under its axis, its bottom on the red line of a designed profile. What the maps, the spot
    /// levels, the slope tags and the cut/fill grid read, so they agree with the 3D body and the volumes.
    /// </summary>
    public sealed class Designed
    {
        readonly Terrain _t;
        readonly List<Terrains.PadShape> _pads;
        readonly List<(Entity Ditch, Chain Chain, Func<double, double>? Red)> _ditches;

        public Designed(IModelView m, Entity terrain, Terrain t)
        {
            _t = t;
            _pads = Terrains.PadShapes(m, terrain, t, Terrains.BaseZ(terrain));
            _ditches = [.. Terrains.DitchesOf(m, terrain.Id).Select(d => (d, Corridors.ChainOf(d), Ditches.DesignOf(m, d, Corridors.ChainOf(d)))).Where(x => x.Item2.Segments > 0)];
        }

        /// <summary>The designed ground at a point, or null outside the terrain.</summary>
        public double? At(Vec2 p)
        {
            if (_t.ElevationAt(p.X, p.Y) is not { } g) return null;
            foreach (var pad in _pads) if (pad.DesignedAt(p, g) is { } z) return z;
            foreach (var (d, c, red) in _ditches) if (Ditches.DesignedAt(d, c, red, _t, p, g) is { } z) return z;
            return g;
        }

        /// <summary>The designed ground less the existing one at a point (+ fill, − cut), or null outside the terrain.</summary>
        public double? CutFill(Vec2 p, Terrain existing) => At(p) is { } z ? z - (existing.ElevationAt(p.X, p.Y) ?? z) : null;
    }

    /// <summary>The ground as designed at a point (<see cref="Designed"/>).</summary>
    public static double? Proposed(IModelView m, Entity terrain, Terrain t, Vec2 p) => new Designed(m, terrain, t).At(p);

    /// <summary>
    /// The cells a terrain is sampled in, from the lowest corner of its box: their corner and their middle, every
    /// <paramref name="cell"/>. The one grid the analysis and the brush's measured volumes read the ground on.
    /// </summary>
    public static IEnumerable<(Vec2 Min, Vec2 Mid)> Cells(Terrain t, double cell)
    {
        var (min, max) = t.Bounds;
        for (var x = min.X; x < max.X; x += cell)
            for (var y = min.Y; y < max.Y; y += cell)
                yield return (new Vec2(x, y), new Vec2(x + cell / 2, y + cell / 2));
    }

    /// <summary>The terrain sampled for a kind of analysis: cells of about a sixtieth of its longer side, those over the ground.</summary>
    public static Result? Of(IModelView m, Entity terrain, string kind)
    {
        if (!Kinds.Contains(kind) || kind == "none") return null;
        var t = Terrains.Of(terrain);
        if (t.Tin.Triangles.Count == 0) return null;
        var existing = kind == "cutfill" ? Terrains.Existing(terrain) : null;
        var designed = new Designed(m, terrain, t);
        var bands = Bands(kind, t, LevelDatum.Of(m));
        var (min, max) = t.Bounds;
        var span = Math.Max(max.X - min.X, max.Y - min.Y);
        var cell = Math.Max(span / 60, 0.25);
        var cells = new List<Cell>();
        foreach (var (corner, c) in Cells(t, cell))
        {
            if (designed.At(c) is not { } z) continue;
            double value;
            switch (kind)
            {
                case "slope":
                {
                    // On the designed ground (a platform is flat); at the terrain's edge, over the half span that is there.
                    var h = cell / 2;
                    double Fall(double? up, double? down) =>
                        up is { } a && down is { } b ? (a - b) / (2 * h) : up is { } a1 ? (a1 - z) / h : down is { } b1 ? (z - b1) / h : 0;
                    var gx = Fall(designed.At(new Vec2(c.X + h, c.Y)), designed.At(new Vec2(c.X - h, c.Y)));
                    var gy = Fall(designed.At(new Vec2(c.X, c.Y + h)), designed.At(new Vec2(c.X, c.Y - h)));
                    value = Math.Sqrt(gx * gx + gy * gy) * 100;
                    break;
                }
                case "cutfill": value = z - (existing!.ElevationAt(c.X, c.Y) ?? z); break;
                default: value = z; break;
            }
            cells.Add(new Cell(corner, new Vec2(corner.X + cell, corner.Y + cell), value, z, BandOf(bands, value)));
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
