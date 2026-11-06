using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A foreign wall whose layers are parts (IfcBuildingElementParts, as ArchiCAD writes a composite wall), read layer by
/// layer from each part's profile (<see cref="PartProfile"/>; docs/spec-ifc.md, Pereții din piese) into what a native
/// layered wall can be: the parts side by side across the axis are its layers; each part's bottom flat, the lowest the
/// wall's base and a higher one the layer's own bottom; the parts' tops together the wall's top (flat, or the file's
/// outline along the axis, a gable's, kept as the wall's <c>topProfile</c>), each part's top that outline or flat under
/// it (the layer's own top); the ends square across all the layers, or else stepped or slanted layer by layer
/// (each layer's faces then run on past the core's ends as far as the file's: <see cref="Walls.LayerEndsKey"/>). A part that is no such layer — notched across, with a gap along
/// the axis, recesses or holes the wall's openings do not make, a top of its own — leaves the whole wall a reference,
/// with the reason: a box at the highest point would not be the file's wall.
/// </summary>
internal static class ForeignWallParts
{
    /// <summary>The 1 mm the fidelity gate allows (docs/spec-ifc.md, Poarta de fidelitate): what is under it is the file's.</summary>
    const double Tol = 1e-3;

    /// <summary>
    /// What the parts make: the layers (first the one furthest right of the axis), their usage, the base (world z) and the
    /// height, the top along the axis (u from the wall's start, v over the base) when it is not flat, the footprint round the
    /// outer faces (for the joins), and where the wall starts and ends along the file's axis. When its layers do not all end
    /// square on one line, the wall runs between its core's ends and <see cref="LayerEnds"/> says how far each layer's two
    /// faces go on past them (<see cref="Walls.LayerEndsKey"/>): such a wall's ends are the file's, not a join's.
    /// </summary>
    internal sealed record Wall(List<LayerSpec> Layers, (bool Negative, double Offset) Usage, double BaseZ, double Height,
        List<(double U, double V)>? Top, IReadOnlyList<Vec2> Footprint, double Start, double End, List<double[]>? LayerEnds);

    /// <summary>The wall the parts make on the axis from <paramref name="a"/> to <paramref name="b"/>, or null with the reason it stays a reference.</summary>
    internal static Wall? Read(ForeignElement e, Vec2 a, Vec2 b, out string? reason)
    {
        reason = null;
        var parts = (e.Parts ?? []).Where(p => p.Extents != null).ToList();
        if (parts.Any(p => p.Profile == null)) { reason = "a wall whose parts are too detailed to read as layers"; return null; }
        var bands = parts.Select(p => (Part: p, Prof: p.Profile!)).Where(x => x.Prof.Thickness >= 0.003).OrderBy(x => x.Prof.V0).ToList();
        if (bands.Count == 0) { reason = "a wall whose parts are not layers side by side"; return null; }
        for (var i = 0; i + 1 < bands.Count; i++)
        {
            var gap = bands[i + 1].Prof.V0 - bands[i].Prof.V1;
            if (gap > Tol) { reason = "a wall whose parts leave a gap between them (an air layer the file does not model)"; return null; }
            if (gap < -Tol) { reason = "a wall whose parts overlap across it (studs within the insulation)"; return null; }
        }
        // The wall's openings along the axis and up: where a part's outline may leave the band (a door down to the floor).
        var dir = (b - a).Normalized();
        var openings = Boxes(e, a, dir);
        bool InOpening(double u) => openings.Any(o => u > o.U0 - Tol && u < o.U1 + Tol);
        foreach (var (_, p) in bands)
        {
            if (p.Top == null || p.Bottom == null || (p.Gaps ?? []).Any(g => !openings.Any(o => g.From >= o.U0 - Tol && g.To <= o.U1 + Tol)))
            { reason = "a wall whose parts are in pieces along it"; return null; }
            if (!p.Band) { reason = "a wall whose parts are not plain layers (notched or stepped across)"; return null; }
            var floor = p.Bottom.Where(q => !InOpening(q.X)).Select(q => q.Y).ToList();
            if (floor.Count == 0 || floor.Max() - floor.Min() > Tol) { reason = "a wall whose layers' bottoms are not flat (stepped or sloped)"; return null; }
        }
        // Each part's volume is the band its outlines and faces say, less the wall's openings where the file cut them out of it.
        foreach (var (_, p) in bands)
        {
            if (p.BandVolume() is not { } band) { reason = "a wall whose parts are in pieces along it"; return null; }
            var holes = Holes(openings, p);
            bool Near(double expected) => Math.Abs(p.Volume - expected) <= Math.Max(1e-4, 0.005 * p.Volume);
            if (!Near(band) && !Near(band - holes)) { reason = "a wall whose parts are not bands along it (recesses, or holes its openings do not make)"; return null; }
        }
        double Floor(PartProfile p) => p.Bottom!.Where(q => !InOpening(q.X)).Min(q => q.Y);
        var baseZ = bands.Min(x => Floor(x.Prof));
        // The wall's top: the parts' tops together, the highest of them along the axis.
        var top = Upper(bands.Select(x => x.Prof.Top!).ToList());
        if (top == null) { reason = "a wall whose parts are in pieces along it"; return null; }
        var high = top.Max(q => q.Y);
        var layers = new List<LayerSpec>();
        var core = bands.OrderByDescending(x => x.Prof.Thickness).First();
        foreach (var (part, p) in bands)
        {
            // Its top: the wall's, or flat under it (the layer's own height).
            var own = p.Top!.Where(q => !InOpening(q.X)).Select(q => q.Y).DefaultIfEmpty(p.Top!.Max(q => q.Y)).Max();
            foreach (var u in p.Top!.Select(q => q.X).Concat(top.Select(q => q.X)).Where(u => u > p.Top![0].X + 1e-6 && u < p.Top![^1].X - 1e-6)
                .SelectMany(u => new[] { u - 1e-5, u + 1e-5 }).Append((p.Top![0].X + p.Top![^1].X) / 2).Where(u => !InOpening(u)))
                if (Math.Abs(PartProfiles.At(p.Top!, u) - Math.Min(PartProfiles.At(top, u), own)) > Tol)
                { reason = "a wall whose layers have tops of their own (neither the wall's top nor flat under it)"; return null; }
            var from = Floor(p) - baseZ;
            layers.Add(new LayerSpec(part.Material ?? part.Name ?? "Material", Math.Round(p.Thickness, 4), part == core.Part,
                From: from > Tol ? Math.Round(from, 4) : null, To: high - own > Tol ? Math.Round(own - baseZ, 4) : null));
        }
        // The ends: square across every layer, the wall's (a join may take them); else the core's, each layer's faces going on past them.
        var squareStart = Square(bands.Select(x => x.Prof), p => p.Right.Start, p => p.Left.Start);
        var squareEnd = Square(bands.Select(x => x.Prof), p => p.Right.End, p => p.Left.End);
        var c = core.Prof;
        double u0 = bands.Min(x => Math.Min(x.Prof.Right.Start, x.Prof.Left.Start)), u1 = bands.Max(x => Math.Max(x.Prof.Right.End, x.Prof.Left.End));
        List<double[]>? ends = null;
        if (!squareStart || !squareEnd)
        {
            (u0, u1) = ((c.Right.Start + c.Left.Start) / 2, (c.Right.End + c.Left.End) / 2);
            ends = [.. bands.Select(x => new[] { u0 - x.Prof.Right.Start, u0 - x.Prof.Left.Start, x.Prof.Right.End - u1, x.Prof.Left.End - u1 }.Select(v => Math.Round(v, 6)).ToArray())];
        }
        var flat = high - top.Min(q => q.Y) <= Tol;
        var (r, l) = (bands[0].Prof, bands[^1].Prof);
        var n = dir.Perp;
        Vec2 P(double u, double v) => a + dir * u + n * v;
        IReadOnlyList<Vec2> footprint = [P(r.Right.Start, r.V0), P(r.Right.End, r.V0), P(l.Left.End, l.V1), P(l.Left.Start, l.V1)];
        return new Wall(layers, (false, bands[0].Prof.V0), baseZ, high - baseZ,
            flat ? null : PartProfiles.Simplify([.. top.Select(q => new Vec2(Math.Round(q.X - u0, 6), Math.Round(q.Y - baseZ, 6)))]).Select(q => (q.X, q.Y)).ToList(),
            footprint, u0, u1, ends);
    }

    /// <summary>Whether every layer's two faces end at one u (within the 1 mm).</summary>
    static bool Square(IEnumerable<PartProfile> parts, Func<PartProfile, double> right, Func<PartProfile, double> left)
    {
        var at = parts.SelectMany(p => new[] { right(p), left(p) }).ToList();
        return at.Max() - at.Min() <= Tol;
    }

    /// <summary>The wall's openings as boxes along the axis (u from its start) and up (world z), from their prisms.</summary>
    static List<(double U0, double U1, double Z0, double Z1)> Boxes(ForeignElement e, Vec2 a, Vec2 dir)
    {
        var o = new List<(double, double, double, double)>();
        foreach (var op in e.Openings)
        {
            if (op.Prisms.Count == 0) continue;
            var pts = op.Prisms.SelectMany(q => q.Outer.Concat(q.Outer.Select(x => x + q.Sweep))).ToList();
            var us = pts.Select(q => Vec2.Dot(new Vec2(q.X, q.Y) - a, dir)).ToList();
            o.Add((us.Min(), us.Max(), pts.Min(q => q.Z), pts.Max(q => q.Z)));
        }
        return o;
    }

    /// <summary>The volume the wall's openings take out of a part: each opening's box over the part's span and height, across its thickness.</summary>
    static double Holes(List<(double U0, double U1, double Z0, double Z1)> openings, PartProfile p)
    {
        double sum = 0;
        var (u0, u1) = (p.Top![0].X, p.Top![^1].X);
        var (z0, z1) = (p.Bottom!.Min(q => q.Y), p.Top!.Max(q => q.Y));
        foreach (var o in openings)
        {
            var du = Math.Min(u1, o.U1) - Math.Max(u0, o.U0);
            var dz = Math.Min(z1, o.Z1) - Math.Max(z0, o.Z0);
            if (du > 0 && dz > 0) sum += du * dz * p.Thickness;
        }
        return sum;
    }

    /// <summary>The highest of several outlines along the axis, over the span they cover together; null where they leave a gap.</summary>
    internal static List<Vec2>? Upper(IReadOnlyList<IReadOnlyList<Vec2>> outlines)
    {
        const double eps = 1e-6;
        var breaks = outlines.SelectMany(o => o.Select(q => q.X)).OrderBy(u => u).ToList();
        var us = new List<double>();
        foreach (var u in breaks) if (us.Count == 0 || u - us[^1] > 1e-6) us.Add(u);
        double? Reach(double u)
        {
            double? best = null;
            foreach (var o in outlines)
                if (u >= o[0].X && u <= o[^1].X && PartProfiles.At(o, u, low: false) is var z && (best == null || z > best)) best = z;
            return best;
        }
        var top = new List<Vec2>();
        for (var i = 0; i < us.Count; i++)
        {
            double? before = i == 0 ? null : Reach(us[i] - eps), after = i == us.Count - 1 ? null : Reach(us[i] + eps);
            if (i > 0 && before == null || i < us.Count - 1 && after == null) return null;
            if (i > 0 && Reach((us[i - 1] + us[i]) / 2) is { } zm && top.Count > 0)
            {
                var (p, q) = (top[^1], new Vec2(us[i], before!.Value));
                var mid = (us[i - 1] + us[i]) / 2;
                if (Math.Abs(zm - (p.Y + (q.Y - p.Y) * (mid - p.X) / Math.Max(q.X - p.X, 1e-12))) > 2e-4) top.Add(new Vec2(mid, zm));
            }
            if (before is { } bz) top.Add(new Vec2(us[i], bz));
            if (after is { } az && (before == null || Math.Abs(az - before.Value) > 1e-4)) top.Add(new Vec2(us[i], az));
        }
        return top.Count >= 2 ? PartProfiles.Simplify(top) : null;
    }
}
