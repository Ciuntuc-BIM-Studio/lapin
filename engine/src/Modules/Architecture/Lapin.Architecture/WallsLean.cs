using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

public static partial class Walls
{
    /// <summary>
    /// A leaning wall's body (docs/plan-wall-lean-taper.md, L1), or a plumb wall's joined to one: its layout worked out at its
    /// base and at its top (<see cref="WallLean.At"/>: every leaning wall's layers as they are at that height, so each join's
    /// corner is where the faces' planes cross there), each band's pieces lofted between the two (their corners move up in
    /// straight lines, as the planes' crossings do); its zones and its layers' pieces in height cut from the same lofts. Its
    /// openings, lintels and the reveals' linings are the plumb wall's leaned by the wall's map; the plan cuts it at the plan's
    /// height, with its base line seen below and its top edge overhead where they lie outside the cut. Null when it cannot be
    /// laid out (then it is built as a plumb wall).
    /// </summary>
    static ElementGeometry? LeanGeometry(Entity wall, IModelView m, IServices? services, List<string> problems)
    {
        var lean = WallLean.Of(m, wall);
        var z = Spatial.Base(m, wall);
        var height = HeightOf(m, wall);
        Layout? bottom, top;
        using (WallLean.At(z)) bottom = LayoutOf(wall, m, services, problems);
        using (WallLean.At(z + height)) top = LayoutOf(wall, m, services, []);
        if (bottom == null || top == null || bottom.Bands.Count != top.Bands.Count) return null;
        var (start, dir) = (bottom.Start, bottom.Dir);
        var left = dir.Perp;
        var axisStart = Start(wall);
        var axisDir = (End(wall) - axisStart).Normalized();
        var plumbFrame = Openings.FrameOf(m, wall);
        var map = lean != null ? WallLean.Map(lean, axisStart, axisDir, z) : null;
        Solid Leaned(Solid s) => map == null ? s : WallLean.Mapped(s, map);

        // Each band's pieces at the base and the top, vertex for vertex; where the joins leave them different, the base's moved across.
        var moved = lean != null ? left * (lean.Shift(height) - lean.Shift(0)) : new Vec2(0, 0);
        var mismatch = false;
        List<(IReadOnlyList<Vec2> A, IReadOnlyList<Vec2> B)> Pairs(int index)
        {
            var (b0, b1) = (bottom.Bands[index], top.Bands[index]);
            var (p0, p1) = (bottom.Footprints(b0), top.Footprints(b1));
            var o = new List<(IReadOnlyList<Vec2>, IReadOnlyList<Vec2>)>();
            if (p0.Count == p1.Count && p0.Zip(p1).All(x => x.First.Count == x.Second.Count))
            {
                for (var i = 0; i < p0.Count; i++) o.Add((p0[i], Aligned(p0[i], p1[i])));
                return o;
            }
            mismatch = true;
            foreach (var a in p0) o.Add((a, [.. a.Select(q => q + moved)]));
            return o;
        }

        var zones = TypeOf(m, wall.Id) is { } type ? WallZones.Bands(WallZones.Of(type), height) : [];
        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();
        // Openings: the plumb wall's boxes, leaned with it (the hole the same whichever way its door or window stands).
        var openings = Openings.PlacedOn(m, wall.Id);
        var cutters = openings.SelectMany(o => Openings.Cutters(m, wall, plumbFrame, o)).Select(c => (o: c.Reach, Box: Leaned(c.Box))).ToList();
        var through = ThroughOpenings.Into(m, wall).Select(x => x.Placed).ToList();
        foreach (var o in through)
            if (Openings.Cutter(plumbFrame, o) is { } box) cutters.Add((o, Leaned(box)));
        if (!(services?.Get<GeometryOptions>()?.CutOpenings ?? true)) cutters.Clear();
        var booleans = services?.Get<ISolidBooleans>();
        if (cutters.Count > 0 && booleans == null) problems.Add("Openings need the boolean library (manifold), missing here: the wall is drawn uncut.");
        var lintelBoxes = new List<(Lintel Lintel, int Piece, Solid Box)>();
        var lintelsIn = LintelJoins.Resolve(wall, LintelPieces(wall, m, services, through), m, services, out var intoCore);
        foreach (var r in lintelsIn)
        {
            if (r.Piece.Box() is not { } box) continue;
            if (r.CutBy.Count > 0 && booleans != null)
            {
                try { box = booleans.Difference(box, r.CutBy, box.Tag) ?? box; }
                catch (InvalidOperationException ex) { problems.Add($"A lintel could not be cut at its corner ({ex.Message})."); }
            }
            if (Measure.Volume(box) > 1e-9) lintelBoxes.Add((r.Piece.Lintel, r.Piece.Piece, Leaned(box)));
        }
        // A tapered wall's lintels (L2): the base's boxes kept inside its core as it tapers (its leaning face would leave them sticking out).
        if (lean == null && booleans != null && lintelBoxes.Count > 0 && WallTaper.Of(m, wall) != null && bottom.Bands.ToList().FindIndex(b => b.Core) is var ci and >= 0)
        {
            var cores = Pairs(ci).Select(p => WallLean.Loft(p.A, z, p.B, z + height, "core")).OfType<Solid>().ToList();
            for (var i = 0; i < lintelBoxes.Count; i++)
            {
                var (l, piece, box) = lintelBoxes[i];
                try
                {
                    var kept = cores.Select(c => booleans.Intersection(box, c, box.Tag)).OfType<Solid>().MaxBy(Measure.Volume);
                    if (kept != null && Measure.Volume(kept) > 1e-9) lintelBoxes[i] = (l, piece, kept);
                }
                catch (InvalidOperationException ex) { problems.Add($"A lintel could not follow the wall's taper ({ex.Message})."); }
            }
        }
        GeometryPart Cut(GeometryPart part, double z0, double z1, bool isCore = false)
        {
            if (booleans == null) return part;
            var reach = cutters.Where(c => c.o.Sill < z1 - 1e-9 && c.o.Sill + c.o.Height > z0 + 1e-9).Select(c => c.Box)
                .Concat(isCore ? lintelBoxes.Where(b => b.Lintel.Z0 < z1 - 1e-9 && b.Lintel.Z1 > z0 + 1e-9).Select(b => b.Box) : [])
                .Concat(isCore ? intoCore.Where(b => b.Z0 - z < z1 - 1e-9 && b.Z1 - z > z0 + 1e-9).Select(b => b.Box) : [])
                .ToList();
            if (reach.Count == 0) return part;
            try { return booleans.Difference(part.Solid, reach, part.Solid.Tag) is { } cut ? part with { Solid = cut } : part; }
            catch (InvalidOperationException ex) { problems.Add($"{part.Name}: an opening could not be cut ({ex.Message})."); return part; }
        }
        var layered = LayerSets.BandsOf(m, wall.Id);
        var planCut = PlanCutAbove(m, wall);
        var shift = Vec2.Dot(start - axisStart, dir);
        var gaps = openings.Concat(through).Where(o => o.Sill < planCut && o.Sill + o.Height > planCut)
            .Select(o => (From: o.Along - o.Width / 2 - shift, To: o.Along + o.Width / 2 - shift)).OrderBy(g => g.From).ToList();
        for (var bi = 0; bi < bottom.Bands.Count; bi++)
        {
            // The band as the base has it: its index, material, core, pieces in height (only its edges differ at the top).
            var band = bottom.Bands[bi];
            if (!(band.To - band.From > 1e-9)) continue;
            foreach (var (a, b) in Pairs(bi))
            {
                IReadOnlyList<Vec2> At(double h) => [.. a.Zip(b, (p, q) => p + (q - p) * (h / height))];
                Solid? Piece(double h0, double h1, string tag) => WallLean.Loft(At(h0), z + h0, At(h1), z + h1, tag);
                // The core of a zoned wall is its zones, bottom to top; every other layer runs the whole height, in its pieces in height.
                if (band.Core && zones.Count > 0)
                {
                    foreach (var zb in zones)
                        if (zb.Z1 - zb.Z0 > 1e-6 && Piece(zb.Z0, zb.Z1, zb.Zone.Name) is { } piece)
                            parts.Add(Cut(new GeometryPart(zb.Zone.Name, Materials.ColorOf(m, zb.Zone.Material), piece, zb.Zone.Material, "zone:" + zb.Zone.Key), zb.Z0, zb.Z1, isCore: true));
                }
                else if (LayerSpan(band, height) is var (lz0, lz1))
                    foreach (var h in HeightPieces(m, band, lz0, lz1))
                        if (Piece(h.Z0, h.Z1, h.Name) is { } solid)
                            parts.Add(Cut(new GeometryPart(h.Name, Materials.ColorOf(m, h.Material), solid, h.Material, $"layer:{band.Index}"), h.Z0, h.Z1, isCore: band.Core));
                // In plan, the band where the plan's cut meets it, between the gaps the openings leave there.
                var footprint = At(Math.Clamp(planCut, 0, height));
                var x0 = footprint.Min(q => Vec2.Dot(q - start, dir));
                var x1 = footprint.Max(q => Vec2.Dot(q - start, dir));
                var at = x0;
                foreach (var g in gaps.Append((From: x1, To: x1)))
                {
                    var cutPiece = ClipX(footprint, start, dir, at, g.From);
                    if (cutPiece.Count >= 3) plan.Add(new PlanItem("cut", true, cutPiece, Sub: layered != null && band.Index < layered.Count ? LayerSets.SubOf(m, layered[band.Index]) : null));
                    at = Math.Max(at, g.To);
                }
            }
        }
        if (mismatch) problems.Add("Its layers could not follow its joins all the way up: they are drawn from the base's corners, moved by its lean.");
        // A leaning wall's faces outside the cut: its base line seen below, its top edge overhead.
        if (lean != null && bottom.BaseReach is { } reach0 && top.BaseReach is { } reach1)
        {
            var (lowBase, highBase) = (bottom.Bands.MinBy(b => b.From), bottom.Bands.MaxBy(b => b.To));
            var (lowTop, highTop) = (top.Bands.MinBy(b => b.From), top.Bands.MaxBy(b => b.To));
            IReadOnlyList<Vec2> Edge(Layout l, Func<int, Band, bool, double> r, Band b, bool atFrom)
            {
                var y = atFrom ? b.From : b.To;
                return [l.Start + l.Dir * -r(0, b, atFrom) + l.Dir.Perp * y, l.Start + l.Dir * (l.Length + r(1, b, atFrom)) + l.Dir.Perp * y];
            }
            // Leaning to its left: the right face's base and the left face's top lie outside the cut; to its right, the other two.
            if (lean.Tan > 0)
            {
                plan.Add(new PlanItem("outline", false, Edge(bottom, reach0, lowBase, true)));
                plan.Add(new PlanItem("overhead", false, Edge(top, reach1, highTop, false)));
            }
            else
            {
                plan.Add(new PlanItem("outline", false, Edge(bottom, reach0, highBase, false)));
                plan.Add(new PlanItem("overhead", false, Edge(top, reach1, lowTop, true)));
            }
        }
        // A tapered wall's leaning face where it lies outside the cut: its base line seen below when it thins upwards, its top edge overhead when it thickens.
        if (lean == null && WallTaper.Of(m, wall) is { } taper && bottom.BaseReach is { } tReach0 && top.BaseReach is { } tReach1)
        {
            var (l, r, b, atFrom) = taper.Top < taper.Bottom
                ? (bottom, tReach0, taper.LeansAtMax ? bottom.Bands.MaxBy(x => x.To) : bottom.Bands.MinBy(x => x.From), !taper.LeansAtMax)
                : (top, tReach1, taper.LeansAtMax ? top.Bands.MaxBy(x => x.To) : top.Bands.MinBy(x => x.From), !taper.LeansAtMax);
            var y = atFrom ? b.From : b.To;
            plan.Add(new PlanItem(taper.Top < taper.Bottom ? "outline" : "overhead", false,
                [l.Start + l.Dir * -r(0, b, atFrom) + l.Dir.Perp * y, l.Start + l.Dir * (l.Length + r(1, b, atFrom)) + l.Dir.Perp * y]));
        }
        parts = CutPriorities.Apply(wall, m, services, parts, problems);
        if (booleans != null)
        {
            var ring = zones.FirstOrDefault(zb => zb.Zone.Role == "ringBeam");
            foreach (var (l, i, box) in lintelBoxes)
            {
                var material = l.Kind == "downstand" ? ring?.Zone.Material : l.Rule?.Material is { } name ? Materials.Find(m, name)?.Id : Materials.Find(m, "Reinforced concrete")?.Id;
                var color = material != null ? Materials.ColorOf(m, material) : "#a8a8a8";
                var label = l.Kind == "downstand" ? "Ring beam over opening" : l.Precast ? "Lintel (precast)" : "Lintel (cast)";
                parts.Add(new GeometryPart(label, color, box, material, $"{l.Kind}:{l.Key}:{i}") { TypeId = l.Kind == "lintel" ? l.Rule?.BeamTypeIn(m) : null });
            }
        }
        foreach (var o in openings)
            if (BareOpenings.Lining(m, o, plumbFrame, o.Along) is { } lining) parts.Add(lining with { Solid = Leaned(lining.Solid) });
        parts = LayerExtents.CutBy(wall, m, services, parts, problems);
        return new ElementGeometry(parts, plan, [start, bottom.End], problems.Count > 0 ? problems : null);
    }

    /// <summary>The top's piece turned to start at the vertex nearest the base's first: the two the same piece, vertex for vertex.</summary>
    static IReadOnlyList<Vec2> Aligned(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b)
    {
        var n = a.Count;
        var (best, at) = (double.MaxValue, 0);
        for (var k = 0; k < n; k++)
        {
            var d = 0.0;
            for (var i = 0; i < n; i++) d += (b[(i + k) % n] - a[i]).Length;
            if (d < best - 1e-12) (best, at) = (d, k);
        }
        return at == 0 ? b : [.. Enumerable.Range(0, n).Select(i => b[(i + at) % n])];
    }
}
