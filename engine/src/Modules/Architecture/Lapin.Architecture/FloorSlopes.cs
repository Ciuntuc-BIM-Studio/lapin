using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Floor slopes and falls (docs/plan-floor-slopes.md; site grading T7; forum topic 157 / issue #96): a floor's top shaped by
/// the slope engine hardscapes use (<see cref="TopSurface"/>), its definition kept on the floor's <c>surface</c> attribute and
/// the shape worked out on every ask. A floor without it is built exactly as before.
/// <list type="bullet">
/// <item><b>Bend</b> (<c>how: bend</c>, the core plane): every layer follows the shaped top at its own thickness, measured
/// vertically (the faces stay plumb at the edges, the volumes are area × thickness exactly; at floor falls of a few percent the
/// thickness across the slope differs by less than 0.05 %).</item>
/// <item><b>Sculpt a layer</b> (<c>how: layer</c>, a screed to falls): one layer takes the shape in its thickness; the layers
/// above ride on it at theirs, the layers below stay level. Its thickness never goes below its minimum: there it is held and
/// the floor says over how much area.</item>
/// <item>Offset 0 is the floor's top as today (its storey plus its offset): falls go down from it, so the walls, doors and rooms
/// that read the floor's level are unchanged where the outline is held.</item>
/// </list>
/// Regions inside the outline (drawn, or a room's outline on every ask) carry their own top and step from the rest along their
/// outline. Each command is one undo step.
/// </summary>
public static class FloorSlopes
{
    public const string Key = TopSurface.SurfaceKey;
    public static readonly string[] Kinds = ["point", "line", "drain", "edge"];
    static readonly Dictionary<string, string> ListOf = new() { ["point"] = "points", ["line"] = "lines", ["drain"] = "drains", ["edge"] = "edges" };

    /// <summary>The layer a floor's slope marks draw on (DXF, the PDF's groups).</summary>
    public const string Layer = "LEVELS";
    const string Green = "#2d5a27";

    public static bool Has(Entity slab) => slab.Attrs[Key] is JsonObject;

    /// <summary>
    /// A floor's shaped top worked out: its pieces (the main one and its regions'), each with the way its build-up follows
    /// (<paramref name="How"/> per piece), the sculpted layer's place in the section and its minimum, what it says.
    /// </summary>
    public sealed record Built(IReadOnlyList<Shaped> Pieces, IReadOnlyList<string> Problems, double Top)
    {
        /// <summary>The top's height above the floor's reference at each point of a piece (the shaped top the finish lies on).</summary>
        public double TopShift(Shaped p, int i) => p.Shift(0, i, top: true);
    }

    /// <summary>One piece and how its layers follow it: their shifts at its points.</summary>
    public sealed record Shaped(TopSurface.Piece Piece, string How, int SculptAt, double Hold, IReadOnlyList<(double From, double To)> Bands)
    {
        /// <summary>The shaped height (held at the sculpted layer's minimum).</summary>
        public double H(int i) => How == TopSurface.Sculpt ? Math.Max(Piece.H(i), Hold) : Piece.H(i);

        /// <summary>How far the band at section position <paramref name="j"/> is moved at point <paramref name="i"/>, at its top or its bottom.</summary>
        public double Shift(int j, int i, bool top)
        {
            if (How != TopSurface.Sculpt) return H(i);
            if (j < SculptAt) return H(i);
            if (j == SculptAt) return top ? H(i) : 0;
            return 0;
        }
    }

    /// <summary>The section's layers (top first) with their core.</summary>
    static IReadOnlyList<(double From, double To, string? Material, string Name, int Index)> Section(IModelView m, Entity slab) => Slabs.Section(m, slab);

    /// <summary>
    /// The layer a floor sculpts when it is not told which (its position in the section, top first): the one above the core
    /// whose name or material says screed, else the one just above the core; -1 when no layer with a thickness lies above it.
    /// </summary>
    public static int DefaultSculpt(IModelView m, Entity slab)
    {
        var section = Section(m, slab);
        var bands = LayerSets.BandsOf(m, slab.Id);
        var core = bands?.ToList().FindIndex(b => b.Core) ?? -1;
        if (core < 0) core = section.Count - 1;
        var above = Enumerable.Range(0, Math.Max(0, core)).Where(j => section[j].To - section[j].From > 1e-9).ToList();
        if (above.Count == 0) return -1;
        foreach (var j in above) if (IsScreed(m, section[j].Name, section[j].Material)) return j;
        return above[^1];
    }

    static bool IsScreed(IModelView m, string name, string? material) =>
        name.Contains("screed", StringComparison.OrdinalIgnoreCase) || name.Contains("șapă", StringComparison.OrdinalIgnoreCase)
        || (material != null && (m.Get(material)?.Name ?? material).Contains("screed", StringComparison.OrdinalIgnoreCase));

    /// <summary>The minimum thickness a sculpted layer gets unless told: 30 mm for a screed, else 10 mm (never above the layer's own).</summary>
    public static double DefaultMin(IModelView m, Entity slab, int at)
    {
        var s = Section(m, slab);
        if (at < 0 || at >= s.Count) return 0.01;
        return Math.Min(IsScreed(m, s[at].Name, s[at].Material) ? 0.03 : 0.01, s[at].To - s[at].From);
    }

    /// <summary>The section position of a layer given by its index in the type's set; -1 when there is none.</summary>
    static int PositionOf(IModelView m, Entity slab, int index) => Section(m, slab).ToList().FindIndex(b => b.Index == index);

    /// <summary>
    /// The floor's shaped top over <paramref name="contour"/> (its outline as built by default, or a layer's own): null for a
    /// floor without slopes.
    /// </summary>
    public static Built? Solve(IModelView m, Entity slab, IServices? services, IReadOnlyList<Vec2>? contour = null)
    {
        var corners = Slabs.Corners(slab);
        if (corners.Count < 3 || TopSurface.ReadShaped(slab.Attrs, corners.Count) is not { } shaped) return null;
        var laid = contour ?? Slabs.Contour(slab);
        var top = Slabs.Top(m, slab);
        var holes = OutlineHoles.Built(slab);
        IReadOnlyList<Vec2>? RoomOutline(string id) =>
            m.Get(id) is { Class: IfcClass.Space } room ? Rooms.Live(room, m, services)?.Outer : null;
        var pieces = TopSurface.SolveShaped(shaped, laid, corners, holes, top, RoomOutline, Slabs.Rises(slab));
        var section = Section(m, slab);
        var bands = section.Select(b => (b.From, b.To)).ToList();
        var problems = new List<string>();
        var main = shaped.Main;
        var beamBlock = BeamBlock.Of(m, slab) != null;
        var o = new List<Shaped>();
        foreach (var p in pieces)
        {
            problems.AddRange(p.Notes);
            var how = p.Shape.How ?? main.How;
            var layer = p.Shape.Layer ?? main.Layer;
            var at = layer is { } li ? PositionOf(m, slab, li) : DefaultSculpt(m, slab);
            if (how == null) how = at >= 0 ? TopSurface.Sculpt : TopSurface.Bend;
            if (how == TopSurface.Sculpt && (at < 0 || bands[at].To - bands[at].From <= 1e-9))
            {
                problems.Add(layer != null ? "The sculpted layer is not in the floor's type now (or has no thickness): the whole floor follows the slope instead." : "No layer above the core to sculpt: the whole floor follows the slope instead.");
                how = TopSurface.Bend;
            }
            if (how == TopSurface.Bend && beamBlock) problems.Add("A beam and block floor is not bent: sculpt its screed instead.");
            var hold = double.NegativeInfinity;
            if (how == TopSurface.Sculpt)
            {
                var t = bands[at].To - bands[at].From;
                var min = Math.Clamp(p.Shape.MinThickness ?? main.MinThickness ?? DefaultMin(m, slab, at), 0, t);
                hold = min - t;
                var held = TopSurface.Hold(p, hold);
                if (held > 1e-6)
                    problems.Add($"{section[at].Name}: held at its {min * 1000:0} mm minimum over {held:0.0#} m²{(p.Region >= 0 ? $" (region {p.Region + 1})" : "")}.");
            }
            o.Add(new Shaped(p, how, at, hold, bands));
        }
        return new Built(o, problems, top);
    }

    // ─── Bodies ──────────────────────────────────────────────────────────

    /// <summary>
    /// A shaped floor's layers as parts, top first: a layer that stays level in every piece is one prism over its outline as
    /// today; one that follows is a solid over each piece, its top and bottom on the piece's triangles.
    /// </summary>
    public static List<GeometryPart> Parts(Entity slab, IModelView m, IServices? services, IReadOnlyList<Vec2> contour,
        Func<int, IReadOnlyList<Vec2>?> layerContour, IReadOnlyList<IReadOnlyList<Vec2>> own, List<string> problems)
    {
        var parts = new List<GeometryPart>();
        var section = Section(m, slab);
        var top = Slabs.Top(m, slab);
        var solved = new List<(IReadOnlyList<Vec2> Contour, Built? Built)>();
        Built? For(IReadOnlyList<Vec2> c)
        {
            foreach (var (k, b) in solved) if (Same(k, c)) return b;
            var built = Solve(m, slab, services, c);
            solved.Add((c, built));
            return built;
        }
        var whole = For(contour);
        if (whole == null) return parts;
        problems.AddRange(whole.Problems);
        for (var j = 0; j < section.Count; j++)
        {
            var (from, to, material, name, index) = section[j];
            if (!(to - from > 1e-9)) continue;
            var layer = layerContour(index) ?? contour;
            var built = For(layer) ?? whole;
            var color = Materials.ColorOf(m, material);
            var moves = built.Pieces.Any(p => Enumerable.Range(0, p.Piece.Mesh.Points.Count).Any(i => Math.Abs(p.Shift(j, i, true)) > 1e-12 || Math.Abs(p.Shift(j, i, false)) > 1e-12));
            if (!moves)
            {
                if (Prism.ExtrudeFootprint(layer, own, top + from, to - from, name) is { } flat)
                    parts.Add(new GeometryPart(name, color, flat, material, $"layer:{index}"));
                continue;
            }
            foreach (var p in built.Pieces)
            {
                var jj = j;
                if (p.Piece.Mesh.Body(i => top + from + p.Shift(jj, i, false), i => top + to + p.Shift(jj, i, true), name) is { } s)
                    parts.Add(new GeometryPart(name, color, s, material, $"layer:{index}"));
            }
        }
        return parts;
    }

    static bool Same(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b) => ReferenceEquals(a, b) || (a.Count == b.Count && a.Zip(b).All(p => (p.First - p.Second).Length < 1e-9));

    /// <summary>The floor's whole build-up following its shaped top: what it cuts lower priorities with. Null when it does not build.</summary>
    public static Solid? Envelope(Entity slab, IModelView m)
    {
        if (Solve(m, slab, null) is not { } b || b.Pieces.Count == 0) return null;
        var section = Section(m, slab);
        var (lo, hi) = (section.Min(x => x.From), section.Max(x => x.To));
        var verts = new List<Vec3>();
        var tris = new List<(int, int, int)>();
        foreach (var p in b.Pieces)
        {
            // The top follows the top layer's top; the bottom the lowest layer's bottom.
            var last = section.Count - 1;
            if (p.Piece.Mesh.Body(i => b.Top + lo + p.Shift(last, i, false), i => b.Top + hi + p.Shift(0, i, true), "slab") is not { } s) continue;
            if (b.Pieces.Count == 1) return s;
            var at = verts.Count;
            verts.AddRange(s.Vertices);
            foreach (var f in s.Faces)
                foreach (var t in Triangulator.Face(s.Vertices, f.Outer, f.Holes, f.Normal)) tris.Add((at + t.A, at + t.B, at + t.C));
        }
        return verts.Count > 0 ? SolidFromMesh.Build(verts, tris, "slab") : null;
    }

    /// <summary>How far the shaped build-up reaches above and below the flat one (for the openings' cutters).</summary>
    public static (double Down, double Up) Reach(IModelView m, Entity slab, IServices? services = null)
    {
        if (Solve(m, slab, services) is not { } b || b.Pieces.Count == 0) return (0, 0);
        double lo = 0, hi = 0;
        foreach (var p in b.Pieces)
            for (var i = 0; i < p.Piece.Mesh.Points.Count; i++) { lo = Math.Min(lo, p.H(i)); hi = Math.Max(hi, p.H(i)); }
        return (lo, hi);
    }

    // ─── Quantities ──────────────────────────────────────────────────────

    /// <summary>
    /// A shaped floor's own quantities: per layer its average, least and greatest thickness (the volume over the plan area), and
    /// for the floor the shaped top's true area, its fall (highest less lowest) and, when it is one plane, its slope.
    /// </summary>
    public static IEnumerable<QuantitySet> Quantities(Entity slab, ElementGeometry g, IModelView m)
    {
        if (Solve(m, slab, null) is not { } b || b.Pieces.Count == 0) yield break;
        var section = Section(m, slab);
        var area = b.Pieces.Sum(p => p.Piece.Mesh.Area);
        for (var j = 0; j < section.Count; j++)
        {
            var (from, to, material, name, index) = section[j];
            if (!(to - from > 1e-9)) continue;
            var v = g.Parts.Where(p => p.Key == $"layer:{index}").Sum(p => Measure.Volume(p.Solid));
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            foreach (var p in b.Pieces)
                for (var i = 0; i < p.Piece.Mesh.Points.Count; i++)
                {
                    var t = to - from + p.Shift(j, i, true) - p.Shift(j, i, false);
                    min = Math.Min(min, t); max = Math.Max(max, t);
                }
            yield return new QuantitySet("Lapin_SlabLayerThickness",
            [
                new("AverageThickness", area > 0 ? v / area : 0, "LENGTH", "Average thickness"),
                new("MinThickness", min, "LENGTH", "Least thickness"),
                new("MaxThickness", max, "LENGTH", "Greatest thickness"),
            ], $"layer:{index}", name, material);
        }
        double surface = 0, lo = double.PositiveInfinity, hi = double.NegativeInfinity;
        foreach (var p in b.Pieces)
        {
            var mesh = p.Piece.Mesh;
            foreach (var (x, y, z) in mesh.Triangles)
            {
                var (pa, pb, pc) = (mesh.Points[x].At(b.TopShift(p, x)), mesh.Points[y].At(b.TopShift(p, y)), mesh.Points[z].At(b.TopShift(p, z)));
                surface += Vec3.Cross(pb - pa, pc - pa).Length / 2;
            }
            for (var i = 0; i < mesh.Points.Count; i++) { lo = Math.Min(lo, b.TopShift(p, i)); hi = Math.Max(hi, b.TopShift(p, i)); }
        }
        var qs = new List<Quantity>
        {
            new("SurfaceArea", surface, "AREA", "Surface area (on the slope)"),
            new("Fall", hi - lo, "LENGTH", "Fall"),
        };
        if (b.Pieces.Count == 1 && b.Pieces[0].Piece.Plane is var (_, gx, gy)) qs.Add(new("Slope", Math.Sqrt(gx * gx + gy * gy), "RATIO", "Slope"));
        yield return new QuantitySet("Lapin_SlabSlopeQuantities", qs);
    }

    /// <summary>The greatest volume a flat build-up would have (area × width) replaced by the shaped envelope's.</summary>
    public static double? GrossVolume(Entity slab, IModelView m) => Envelope(slab, m) is { } s ? Measure.Volume(s) : null;

    // ─── Plans ───────────────────────────────────────────────────────────

    /// <summary>
    /// In a plan of its storey, a shaped floor's falls: an arrow down each plane (its grade in %), the spot levels of its drains,
    /// points, lines' ends and its highest and lowest points, the drains as a square with a cross, the lines and the regions'
    /// outlines thin and dashed. Under the model's lines, picked with the floor.
    /// </summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        var m = c.Model;
        var storey = c.View.Str("storeyId");
        if (storey == null) yield break;
        var hidden = (c.View.Attrs["hiddenElements"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        var graphics = VisibilityGraphics.Of(m, c.View);
        var text = Standards.ResolveText(c.Book, new JsonObject());
        // The fall arrows and the levels only at the scales the standards draw them at (1:50 and larger unless they say otherwise).
        var annotate = c.Book.ShowsFloorSlopeMarks(c.Scale);
        List<(Vec2 Min, Vec2 Max)>? stamps = null;
        foreach (var slab in m.OfClass(IfcClass.Slab))
        {
            if (!Has(slab) || hidden.Contains(slab.Id) || graphics.Hides(slab) || Spatial.StoreyOf(m, slab.Id)?.Id != storey) continue;
            Built? b;
            // With the view's services: a room's region takes the room's outline now, as the floor's body does, not the one the
            // room stored when it was placed (its marks were drawn where the room had been once its walls moved).
            try { b = Solve(m, slab, c.Services); } catch (Exception) { continue; }
            if (b == null) continue;
            // Its outline as a flat floor's is drawn: a top falling below the plan's level would leave the view's range there
            // and its edges with it, so the outline is drawn here too (under the model's lines, which cover it where they draw it).
            var outline = new ViewMark(slab.Id) { Under = true };
            outline.Polyline(Slabs.Contour(slab), true, "FLOORS", Pens.Default.Seen);
            foreach (var h in OutlineHoles.Built(slab)) outline.Polyline(h, true, "FLOORS", Pens.Default.Seen);
            yield return outline;
            var mk = new ViewMark(slab.Id);
            var shaped = TopSurface.ReadShaped(slab.Attrs, Slabs.Corners(slab).Count)!;
            var levels = new List<(Vec2 At, double H)>();
            foreach (var p in b.Pieces)
            {
                if (annotate) Arrows(mk, b, p, c.Mm, text, stamps ??= Rooms.StampBoxes(c));
                var sh = p.Piece.Shape;
                foreach (var d in sh.Drains)
                {
                    var r = Math.Max(d.Grate / 2, 1.2 * c.Mm);
                    if (d.To is { } to)
                    {
                        var n = (to - d.At).Normalized().Perp * r;
                        mk.Polyline([d.At + n, to + n, to - n, d.At - n], true, Layer, Pens.Default.Seen, Green);
                        levels.Add((d.At, b.TopShift(p, Near(p, d.At))));
                        levels.Add((to, b.TopShift(p, Near(p, to))));
                        continue;
                    }
                    var sq = new[] { d.At + new Vec2(-r, -r), d.At + new Vec2(r, -r), d.At + new Vec2(r, r), d.At + new Vec2(-r, r) };
                    mk.Polyline(sq, true, Layer, Pens.Default.Seen, Green);
                    mk.Line(sq[0], sq[2], Layer, Pens.Default.Hatch, Green);
                    mk.Line(sq[1], sq[3], Layer, Pens.Default.Hatch, Green);
                    levels.Add((d.At, b.TopShift(p, Near(p, d.At))));
                }
                foreach (var pt in sh.Points) levels.Add((pt.At, b.TopShift(p, Near(p, pt.At))));
                foreach (var l in sh.Lines)
                {
                    mk.Line(l.A, l.B, Layer, Pens.Default.Hatch, Green, [3, 1.5]);
                    levels.Add((l.A, b.TopShift(p, Near(p, l.A))));
                    levels.Add((l.B, b.TopShift(p, Near(p, l.B))));
                }
                // The highest and lowest points of a piece shaped by a plane or edge slopes (its drains and points say their own).
                if (sh.Drains.Count == 0 && sh.Points.Count == 0 && sh.Lines.Count == 0 && p.Piece.Mesh.Points.Count > 0)
                {
                    var idx = Enumerable.Range(0, p.Piece.Mesh.Points.Count).ToList();
                    var hiI = idx.MaxBy(i => b.TopShift(p, i));
                    var loI = idx.MinBy(i => b.TopShift(p, i));
                    if (b.TopShift(p, hiI) - b.TopShift(p, loI) > 1e-6)
                    {
                        levels.Add((p.Piece.Mesh.Points[hiI], b.TopShift(p, hiI)));
                        levels.Add((p.Piece.Mesh.Points[loI], b.TopShift(p, loI)));
                    }
                }
            }
            foreach (var reg in shaped.Regions)
                if (reg.Outline is { } ol) mk.Polyline(ol, true, Layer, Pens.Default.Hatch, Green, [3, 1.5]);
            foreach (var p in b.Pieces.Where(x => x.Piece.Region >= 0))
                foreach (var (x, y) in p.Piece.Mesh.BoundaryEdges()) mk.Line(p.Piece.Mesh.Points[x], p.Piece.Mesh.Points[y], Layer, Pens.Default.Hatch, Green, [3, 1.5]);
            var placed = new List<Vec2>();
            foreach (var (at, h) in annotate ? levels : [])
            {
                if (placed.Any(q => (q - at).Length < 4 * c.Mm)) continue;
                placed.Add(at);
                var t = 0.9 * c.Mm;
                mk.Line(at + new Vec2(-t, 0), at + new Vec2(t, 0), Layer, Pens.Default.Hatch, Green);
                mk.Line(at + new Vec2(0, -t), at + new Vec2(0, t), Layer, Pens.Default.Hatch, Green);
                mk.Text(new PenText(at + new Vec2(1.2 * c.Mm, 0.6 * c.Mm), LevelDatum.Text(c, b.Top + h), text.Height * 0.75, 0, "start", "bottom", false, text.Font), Green);
                mk.Snaps.Add(at);
            }
            if (mk.Lines.Count > 0 || mk.Texts.Count > 0) yield return mk;
        }
    }

    static int Near(Shaped p, Vec2 at)
    {
        var pts = p.Piece.Mesh.Points;
        var best = 0;
        for (var i = 1; i < pts.Count; i++) if ((pts[i] - at).Length < (pts[best] - at).Length) best = i;
        return best;
    }

    /// <summary>
    /// An arrow down each plane of a piece: one for a plane; else one per group of triangles falling the same way (at least 4 % of
    /// the piece), at the largest's middle. An arrow that would cover a room's stamp (<paramref name="avoid"/>) is moved clear of
    /// it when it can be: a little way along or across its fall, else to another triangle of its group; else it stays.
    /// </summary>
    static void Arrows(ViewMark mk, Built b, Shaped p, double mm, TextStyle text, IReadOnlyList<(Vec2 Min, Vec2 Max)> avoid)
    {
        var mesh = p.Piece.Mesh;
        var total = mesh.Area;
        var groups = new Dictionary<(long, long), (double Area, Vec2 At, double BigArea, Vec2 Down, double Grade, List<(double Area, Vec2 Mid)> Tris)>();
        foreach (var (x, y, z) in mesh.Triangles)
        {
            var (pa, pb, pc) = (mesh.Points[x], mesh.Points[y], mesh.Points[z]);
            var (ha, hb, hc) = (b.TopShift(p, x), b.TopShift(p, y), b.TopShift(p, z));
            var det = Vec2.Cross(pb - pa, pc - pa);
            if (Math.Abs(det) < 1e-12) continue;
            // The gradient of the plane through the three: (dz/dx, dz/dy).
            var gx = ((hb - ha) * (pc.Y - pa.Y) - (hc - ha) * (pb.Y - pa.Y)) / det;
            var gy = ((hc - ha) * (pb.X - pa.X) - (hb - ha) * (pc.X - pa.X)) / det;
            var grade = Math.Sqrt(gx * gx + gy * gy);
            if (grade < 1e-5) continue;
            var key = ((long)Math.Round(gx * 1e4), (long)Math.Round(gy * 1e4));
            var area = Math.Abs(det) / 2;
            var mid = (pa + pb + pc) * (1.0 / 3);
            if (groups.TryGetValue(key, out var gr))
            {
                gr.Tris.Add((area, mid));
                groups[key] = (gr.Area + area, area > gr.BigArea ? mid : gr.At, Math.Max(area, gr.BigArea), gr.Down, gr.Grade, gr.Tris);
            }
            else groups[key] = (area, mid, area, new Vec2(-gx, -gy) * (1 / grade), grade, [(area, mid)]);
        }
        var len = 7.0 * mm;
        var h = text.Height * 0.8 * mm;
        foreach (var g in groups.Values.Where(g => g.Area >= 0.04 * total).OrderByDescending(g => g.Area).Take(12))
        {
            var label = GradeText(g.Grade);
            var at = g.At;
            if (avoid.Count > 0 && Covers(at, g.Down, len, label, h, mm, avoid))
            {
                var perp = g.Down.Perp;
                var near = new List<Vec2>();
                foreach (var k in new[] { 1.0, 2.0, 3.0, 4.0 })
                    foreach (var d in new[] { perp, -perp, g.Down, -g.Down })
                        near.Add(g.At + d * (k * 4 * mm));
                var others = g.Tris.OrderByDescending(t => t.Area).Take(40).Select(t => t.Mid);
                var clear = near.Where(q => mesh.Locate(q) != null).Concat(others).FirstOrDefault(q => !Covers(q, g.Down, len, label, h, mm, avoid), new Vec2(double.NaN, double.NaN));
                if (double.IsFinite(clear.X)) at = clear;
            }
            SlopeArrow(mk, at - g.Down * (len * 0.5), at + g.Down * (len * 0.5), mm, label, text.Height, text.Font);
        }
    }

    /// <summary>Whether an arrow at <paramref name="at"/> (its shaft and its label, as <see cref="SlopeArrow"/> draws them) meets one of the boxes, a millimetre on paper round each.</summary>
    static bool Covers(Vec2 at, Vec2 down, double len, string label, double h, double mm, IReadOnlyList<(Vec2 Min, Vec2 Max)> boxes)
    {
        var perp = new Vec2(-down.Y, down.X);
        var (a, e) = (at - down * (len * 0.5), at + down * (len * 0.5));
        var c = at + perp * (2.2 * mm);
        var w = label.Length * h * 0.6 / 2;
        Vec2[] pts = [a, e, a + perp * (1.2 * mm), e + perp * (1.2 * mm), c + down * w + perp * (h / 2), c - down * w + perp * (h / 2), c + down * w - perp * (h / 2), c - down * w - perp * (h / 2)];
        var (x0, y0, x1, y1) = (pts.Min(q => q.X), pts.Min(q => q.Y), pts.Max(q => q.X), pts.Max(q => q.Y));
        foreach (var (min, max) in boxes)
            if (x1 > min.X - mm && x0 < max.X + mm && y1 > min.Y - mm && y0 < max.Y + mm) return true;
        return false;
    }

    /// <summary>A grade as plans write it: 1.5 %; under one percent with two decimals.</summary>
    public static string GradeText(double g) => g * 100 < 1 ? $"{g * 100:0.00} %" : $"{g * 100:0.0#} %";

    /// <summary>A slope arrow from its high end to its low end, its label beside it (the terrain's and hardscapes' look).</summary>
    public static void SlopeArrow(ViewMark mk, Vec2 a, Vec2 b, double mm, string label, double textHeight, string font)
    {
        var dir = (b - a).Normalized();
        var perp = new Vec2(-dir.Y, dir.X);
        var head = 1.2 * mm;
        mk.Line(a, b, Layer, Pens.Default.Seen, Green);
        mk.Line(b, b - dir * head + perp * (head * 0.45), Layer, Pens.Default.Seen, Green);
        mk.Line(b, b - dir * head - perp * (head * 0.45), Layer, Pens.Default.Seen, Green);
        var angle = -Standards.ReadableDeg(-Math.Atan2(dir.Y, dir.X) * 180 / Math.PI);
        mk.Text(new PenText((a + b) * 0.5 + perp * (2.2 * mm), label, textHeight * 0.8, angle, "middle", "middle", true, font), Green);
    }

    // ─── Commands ────────────────────────────────────────────────────────

    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));

    static Vec2 Point(JsonNode? n, string what) =>
        n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y && double.IsFinite(x) && double.IsFinite(y)
            ? new Vec2(x, y) : throw new CommandException($"{what}: a point in plan, [x, y] in metres.");

    /// <summary>The floor a command names (not one kept as it came from its file).</summary>
    static Entity FloorOf(ICommandContext ctx, JsonObject a)
    {
        var slab = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Slab);
        if (StoredBodies.Has(slab) || slab.Attrs["reference"] != null) throw new CommandException("A floor kept as it came from its file is not shaped here.");
        return slab;
    }

    /// <summary>The object a command edits: the floor's surface, or one of its regions (<c>region</c>, from 0); made when missing.</summary>
    static JsonObject Target(JsonObject surface, JsonObject a)
    {
        if (Json.Number(a["region"]) is not { } r) return surface;
        var regs = surface["regions"] as JsonArray ?? [];
        var i = (int)r;
        if (i < 0 || i >= regs.Count || regs[i] is not JsonObject o) throw new CommandException("The floor has no such region.");
        return o;
    }

    static void Write(ICommandContext ctx, Entity slab, JsonObject surface)
    {
        var attrs = slab.CloneAttrs();
        attrs[Key] = surface;
        ctx.Replace(slab.With(attrs: attrs));
    }

    static JsonObject SurfaceOf(Entity slab) => slab.Attrs[Key] is JsonObject s ? (JsonObject)s.DeepClone() : new JsonObject();

    /// <summary>The outline a region or the floor has, for checking a point lies in it.</summary>
    static IReadOnlyList<Vec2> OutlineFor(IModelView m, IServices services, Entity slab, JsonObject target, JsonObject surface)
    {
        if (ReferenceEquals(target, surface)) return Slabs.Contour(slab);
        if ((string?)target["roomId"] is { } room && m.Get(room) is { } r) return Rooms.Live(r, m, services)?.Outer ?? [];
        return target["outline"] is JsonArray arr ? [.. arr.Select(n => Point(n, "A region's corner"))] : [];
    }

    static double GradeArg(JsonNode? n)
    {
        var g = TopSurface.Grade(n) ?? throw new CommandException("A grade is a fraction (0.015), a percentage (1.5 %) or one in n (1:50).");
        g = Math.Abs(g);
        if (!(g > 0) || g > 1) throw new CommandException("A floor's slope is more than 0 and at most 100 %.");
        return g;
    }

    /// <summary>An item of a kind as a command gives it, checked; its stored form.</summary>
    static JsonObject Item(string kind, JsonObject a, Entity slab, IReadOnlyList<Vec2> outline, int corners)
    {
        var o = new JsonObject();
        bool In(Vec2 p) => outline.Count < 3 || Polygon2.Contains(outline, p, 1e-6);
        switch (kind)
        {
            case "point":
            {
                var at = Point(a["at"], "A point");
                if (!In(at)) throw new CommandException("The point lies outside the floor.");
                o["at"] = J(at);
                if (Json.Number(a["level"]) is { } lv) o["level"] = lv;
                else o["offset"] = Json.Number(a["offset"]) ?? throw new CommandException("At what height? An offset from the floor's top (m), or a level.");
                break;
            }
            case "line":
            {
                var (p, q) = (Point(a["a"], "A line's start"), Point(a["b"], "A line's end"));
                if ((q - p).Length < 1e-3) throw new CommandException("A line needs two different points.");
                if (!In(p) || !In(q)) throw new CommandException("The line's ends lie outside the floor.");
                (o["a"], o["b"]) = (J(p), J(q));
                static JsonArray? Two(JsonNode? n) => n is JsonArray { Count: 2 } x && Json.Number(x[0]) is { } u && Json.Number(x[1]) is { } v ? new JsonArray(u, v) : Json.Number(n) is { } w ? new JsonArray(w, w) : null;
                if (Two(a["level"]) is { } lv) o["level"] = lv;
                else o["offset"] = Two(a["offset"]) ?? new JsonArray(0.0, 0.0);
                break;
            }
            case "drain":
            {
                // At the middle of its outline (a room's), wherever that is on every ask.
                if (a["centre"] is JsonValue cv && cv.TryGetValue<bool>(out var cb) && cb && a["at"] == null)
                {
                    o["centre"] = true;
                    var dc = Json.Number(a["depth"]) ?? 0.02;
                    if (!(dc > 0) || dc > 0.2) throw new CommandException("A drain sits more than 0 and at most 200 mm below the floor round it.");
                    o["depth"] = dc;
                    if (Json.Number(a["grate"]) is { } gc && gc > 0 && gc <= 1) o["grate"] = gc;
                    break;
                }
                var at = Point(a["at"], "A drain");
                if (!In(at)) throw new CommandException("The drain lies outside the floor.");
                o["at"] = J(at);
                if (a["to"] != null)
                {
                    var to = Point(a["to"], "A channel's end");
                    if (!In(to)) throw new CommandException("The channel's end lies outside the floor.");
                    if ((to - at).Length < 0.05) throw new CommandException("A channel drain is at least 50 mm long.");
                    o["to"] = J(to);
                }
                var depth = Json.Number(a["depth"]) ?? 0.02;
                if (!(depth > 0) || depth > 0.2) throw new CommandException("A drain sits more than 0 and at most 200 mm below the floor round it.");
                o["depth"] = depth;
                if (Json.Number(a["grate"]) is { } g && g > 0)
                {
                    if (g > 1) throw new CommandException("A drain's grate is at most 1 m across.");
                    o["grate"] = g;
                }
                break;
            }
            case "edge":
            {
                var e = Json.Number(a["edge"]) is { } ev && ev >= 0 && ev == Math.Floor(ev) && ev < corners ? (int)ev : throw new CommandException("Which edge? Its index, from 0.");
                o["edge"] = e;
                o["grade"] = GradeArg(a["grade"]);
                o["fall"] = (string?)a["fall"] == "out" ? "out" : "in";
                if (Json.Number(a["offset"]) is { } off) o["offset"] = off;
                if (a["until"] is JsonObject u)
                {
                    if (Json.Number(u["offset"]) is { } uo) o["until"] = new JsonObject { ["offset"] = uo };
                    else if (Json.Number(u["distance"]) is { } ud)
                    {
                        if (!(ud > 0)) throw new CommandException("A slope runs a distance of more than 0 from its edge.");
                        o["until"] = new JsonObject { ["distance"] = ud, ["along"] = (string?)u["along"] == "slope" ? "slope" : "plan" };
                    }
                }
                break;
            }
            default: throw new CommandException("A floor slope is a point, a line, a drain or an edge.");
        }
        return o;
    }

    static string KindArg(JsonObject a) => (string?)a["kind"] is { } k && Kinds.Contains(k) ? k : throw new CommandException("Which kind? point, line, drain or edge.");

    public static void Register(IModuleRegistry r)
    {
        // How a floor's build-up follows its slopes and its base: {id, region?, how? (bend | layer), layer? (an index in the type's
        // set; null: the default), minThickness? (m), topMode? (level | slope | corners), elevation? (m from the floor's top),
        // grade?, slopeDirection? (degrees), slopeOrigin?, cornerZ?, arrow? {from, to, grade | fromZ, toZ}}. Starts the slopes.
        r.Command("setFloorSlope", "Floor slope", (ctx, a) =>
        {
            var slab = FloorOf(ctx, a);
            var surface = SurfaceOf(slab);
            var t = Target(surface, a);
            var main = ReferenceEquals(t, surface);
            if (a.ContainsKey("how"))
            {
                var how = (string?)a["how"];
                if (how is not (null or TopSurface.Bend or TopSurface.Sculpt)) throw new CommandException("A floor follows its slope bent (bend) or by a sculpted layer (layer).");
                if (how == TopSurface.Bend && BeamBlock.Of(ctx.Model, slab) != null) throw new CommandException("A beam and block floor is not bent: sculpt its screed.");
                if (how == null) t.Remove("how"); else t["how"] = how;
            }
            if (a.ContainsKey("layer"))
            {
                if (Json.Number(a["layer"]) is { } li)
                {
                    var at = PositionOf(ctx.Model, slab, (int)li);
                    var s = Section(ctx.Model, slab);
                    if (at < 0 || s[at].To - s[at].From <= 1e-9) throw new CommandException("The floor's type has no such layer with a thickness.");
                    t["layer"] = (int)li;
                }
                else t.Remove("layer");
            }
            if (a.ContainsKey("minThickness"))
            {
                if (Json.Number(a["minThickness"]) is { } mt)
                {
                    if (!(mt >= 0) || mt > 0.5) throw new CommandException("A minimum thickness is between 0 and 500 mm.");
                    t["minThickness"] = mt;
                }
                else t.Remove("minThickness");
            }
            var corners = main ? Slabs.Corners(slab) : [.. OutlineFor(ctx.Model, ctx.Services, slab, t, surface)];
            var spec = TopSurface.Read(t, corners.Count);
            if ((string?)a["topMode"] is { } mode)
            {
                if (!TopSurface.Modes.Contains(mode)) throw new CommandException("A floor's base is level, a slope or by its corners' heights.");
                spec = spec with { Mode = mode };
            }
            if (Json.Number(a["elevation"]) is { } el) spec = spec with { Elevation = el };
            if (a["grade"] != null)
            {
                spec = spec with { Grade = GradeArg(a["grade"]) };
                if ((string?)a["topMode"] == null) spec = spec with { Mode = TopSurface.Plane };
            }
            if (Json.Number(a["slopeDirection"]) is { } dir) spec = spec with { Direction = dir };
            if (a["slopeOrigin"] is JsonArray) spec = spec with { Origin = Point(a["slopeOrigin"], "The slope's origin") };
            if (a["cornerZ"] is JsonArray cz)
            {
                spec = spec with { CornerZ = [.. Enumerable.Range(0, corners.Count).Select(i => i < cz.Count ? Json.Number(cz[i]) : null)] };
                if ((string?)a["topMode"] == null) spec = spec with { Mode = TopSurface.Corners };
            }
            if (a["arrow"] is JsonObject arrow)
            {
                spec = TopSurface.Arrow(spec, Point(arrow["from"], "The arrow's start"), Point(arrow["to"], "The arrow's end"),
                    arrow["grade"] != null ? GradeArg(arrow["grade"]) : null, Json.Number(arrow["fromZ"]), Json.Number(arrow["toZ"]));
            }
            if (spec.Mode == TopSurface.Plane && spec.Origin == null && corners.Count >= 3) spec = spec with { Origin = Polygon2.Centroid(corners) };
            TopSurface.Write(t, spec);
            Write(ctx, slab, surface);
            return null;
        });

        // A point, a line, a drain or an edge slope added to a floor or one of its regions: {id, region?, kind, …its fields} → {index}.
        r.Command("addFloorSlope", "Floor slope", (ctx, a) =>
        {
            var slab = FloorOf(ctx, a);
            var surface = SurfaceOf(slab);
            var t = Target(surface, a);
            var kind = KindArg(a);
            var outline = OutlineFor(ctx.Model, ctx.Services, slab, t, surface);
            var corners = ReferenceEquals(t, surface) ? Slabs.Corners(slab).Count : outline.Count;
            var list = t[ListOf[kind]] as JsonArray ?? [];
            var item = Item(kind, a, slab, outline, corners);
            // One slope per edge: a second on the same edge replaces it.
            if (kind == "edge" && list.OfType<JsonObject>().ToList().FindIndex(x => Json.Number(x["edge"]) == Json.Number(item["edge"])) is var at and >= 0)
            {
                list[at] = item;
                t[ListOf[kind]] = list;
                Write(ctx, slab, surface);
                return new JsonObject { ["index"] = at };
            }
            list.Add((JsonNode)item);
            t[ListOf[kind]] = list;
            Write(ctx, slab, surface);
            return new JsonObject { ["index"] = list.Count - 1 };
        });

        // One of them changed: {id, region?, kind, index, …its fields (all of them, as when added)}.
        r.Command("updateFloorSlope", "Floor slope", (ctx, a) =>
        {
            var slab = FloorOf(ctx, a);
            var surface = SurfaceOf(slab);
            var t = Target(surface, a);
            var kind = KindArg(a);
            var list = t[ListOf[kind]] as JsonArray ?? [];
            var i = Json.Number(a["index"]) is { } iv ? (int)iv : -1;
            if (i < 0 || i >= list.Count) throw new CommandException($"The floor has no such {kind}.");
            var outline = OutlineFor(ctx.Model, ctx.Services, slab, t, surface);
            var corners = ReferenceEquals(t, surface) ? Slabs.Corners(slab).Count : outline.Count;
            // What is not given stays as it was.
            var merged = (JsonObject)list[i]!.DeepClone();
            foreach (var (k, v) in a) if (k is not ("id" or "region" or "kind" or "index")) merged[k] = v?.DeepClone();
            if (kind == "point" && a.ContainsKey("offset") && !a.ContainsKey("level")) merged.Remove("level");
            if (kind == "point" && a.ContainsKey("level")) merged.Remove("offset");
            if (kind == "line" && a.ContainsKey("offset") && !a.ContainsKey("level")) merged.Remove("level");
            if (kind == "line" && a.ContainsKey("level")) merged.Remove("offset");
            if (kind == "edge" && a.ContainsKey("until") && a["until"] == null) merged.Remove("until");
            list[i] = Item(kind, merged, slab, outline, corners);
            t[ListOf[kind]] = list;
            Write(ctx, slab, surface);
            return null;
        });

        // One taken away: {id, region?, kind, index}.
        r.Command("removeFloorSlope", "Remove floor slope", (ctx, a) =>
        {
            var slab = FloorOf(ctx, a);
            var surface = SurfaceOf(slab);
            var t = Target(surface, a);
            var kind = KindArg(a);
            var list = t[ListOf[kind]] as JsonArray ?? [];
            var i = Json.Number(a["index"]) is { } iv ? (int)iv : -1;
            if (i < 0 || i >= list.Count) throw new CommandException($"The floor has no such {kind}.");
            list.RemoveAt(i);
            if (list.Count == 0) t.Remove(ListOf[kind]); else t[ListOf[kind]] = list;
            Write(ctx, slab, surface);
            return null;
        });

        // A region of the floor with its own top: {id, points | roomId, elevation? (m from the floor's top)} → {region}.
        r.Command("addFloorSlopeRegion", "Floor region", (ctx, a) =>
        {
            var slab = FloorOf(ctx, a);
            var surface = SurfaceOf(slab);
            var reg = AddRegion(ctx, slab, surface, a);
            Write(ctx, slab, surface);
            return new JsonObject { ["region"] = reg };
        });

        r.Command("removeFloorSlopeRegion", "Remove floor region", (ctx, a) =>
        {
            var slab = FloorOf(ctx, a);
            var surface = SurfaceOf(slab);
            var regs = surface["regions"] as JsonArray ?? [];
            var i = Json.Number(a["region"]) is { } iv ? (int)iv : -1;
            if (i < 0 || i >= regs.Count) throw new CommandException("The floor has no such region.");
            regs.RemoveAt(i);
            if (regs.Count == 0) surface.Remove("regions"); else surface["regions"] = regs;
            Write(ctx, slab, surface);
            return null;
        });

        // The floor flat again, as it was before any slope: {id | ids}.
        r.Command("resetFloorSlope", "Reset floor shape", (ctx, a) =>
        {
            var ids = (string?)a["id"] is { } one ? [one] : Args.Ids(a).ToList();
            foreach (var id in ids)
            {
                var slab = Spatial.Require(ctx.Model, id, IfcClass.Slab);
                if (!Has(slab)) continue;
                var attrs = slab.CloneAttrs();
                attrs.Remove(Key);
                ctx.Replace(slab.With(attrs: attrs));
            }
            return null;
        });

        // A room's floor falling to a drain (forum topic 157 §4): {roomId, floorId? (else the floor under the room's middle),
        // drain? {at?, depth?, grate?} (else at the room's middle, 20 mm), edge? {edge, grade, fall, until?} (an edge slope of
        // the room's outline instead of a drain), how?, layer?} → {id, region?}. The room's outline is the region's on every ask
        // (it follows the walls); a room that is the whole floor shapes the floor itself.
        r.Command("slopeRoom", "Slope room", (ctx, a) =>
        {
            var m = ctx.Model;
            var room = Spatial.Require(m, (string?)a["roomId"], IfcClass.Space);
            var outline = Rooms.Live(room, m, ctx.Services) ?? throw new CommandException("The room has no outline: place it again.");
            var mid = Polygon2.InteriorPoint(outline.Outer);
            var storey = Spatial.StoreyOf(m, room.Id)?.Id;
            var slab = (string?)a["floorId"] is { } fid ? Spatial.Require(m, fid, IfcClass.Slab)
                : m.OfClass(IfcClass.Slab).Where(s => Spatial.StoreyOf(m, s.Id)?.Id == storey && Polygon2.Contains(Slabs.Contour(s), mid))
                    .OrderBy(s => Math.Abs(s.Num("offset"))).FirstOrDefault()
                  ?? throw new CommandException("No floor lies under the room on its storey: draw the floor first.");
            if (StoredBodies.Has(slab) || slab.Attrs["reference"] != null) throw new CommandException("A floor kept as it came from its file is not shaped here.");
            var surface = SurfaceOf(slab);
            var floorArea = Math.Abs(Polygon2.SignedArea(Slabs.Contour(slab)));
            var roomArea = Math.Abs(Polygon2.SignedArea(outline.Outer));
            // The room is the whole floor (a floor drawn inside its walls): the floor's own top takes the slope.
            var whole = roomArea >= floorArea * 0.98;
            JsonObject t;
            int? region = null;
            if (whole) t = surface;
            else
            {
                region = AddRegion(ctx, slab, surface, new JsonObject { ["roomId"] = room.Id });
                t = (JsonObject)((JsonArray)surface["regions"]!)[region.Value]!;
            }
            foreach (var k in new[] { "how", "layer" }) if (a[k] != null) t[k] = a[k]!.DeepClone();
            var loop = outline.Outer;
            if (a["edge"] is JsonObject e)
            {
                var corners = whole ? Slabs.Corners(slab).Count : loop.Count;
                var item = Item("edge", e, slab, [], corners);
                t["edges"] = new JsonArray(item);
            }
            else
            {
                var d = a["drain"] as JsonObject ?? new JsonObject();
                // No point given: at the room's middle, kept there as the room changes.
                var args = new JsonObject { ["depth"] = d["depth"]?.DeepClone() ?? 0.02 };
                if (d["at"] != null) args["at"] = d["at"]!.DeepClone(); else args["centre"] = true;
                if (d["grate"] != null) args["grate"] = d["grate"]!.DeepClone();
                var item = Item("drain", args, slab, loop, 0);
                t["drains"] = new JsonArray(item);
            }
            Write(ctx, slab, surface);
            var o = new JsonObject { ["id"] = slab.Id };
            if (region != null) o["region"] = region.Value;
            return o;
        });

        // What the properties show: {id} → {how, layer, layerAt, minThickness, layers: [{index, name, thickness, core}], pieces: [{region,
        // min, max, grade?, surfaceArea}], problems, top}.
        r.Query("floorSlope", (q, a) =>
        {
            var slab = Spatial.Require(q.Model, (string?)a["id"], IfcClass.Slab);
            var section = Section(q.Model, slab);
            var bands = LayerSets.BandsOf(q.Model, slab.Id);
            var o = new JsonObject
            {
                ["layers"] = new JsonArray([.. section.Select((b, j) => (JsonNode)new JsonObject
                {
                    ["index"] = b.Index, ["name"] = b.Name, ["thickness"] = Math.Round(b.To - b.From, 9), ["core"] = bands?.ElementAtOrDefault(j).Core ?? false,
                })]),
                ["defaultLayer"] = DefaultSculpt(q.Model, slab) is var d and >= 0 ? section[d].Index : null,
            };
            if (Solve(q.Model, slab, q.Services) is not { } b) return o;
            o["top"] = b.Top;
            o["problems"] = new JsonArray([.. b.Problems.Select(p => (JsonNode)p)]);
            o["pieces"] = new JsonArray([.. b.Pieces.Select(p => (JsonNode)new JsonObject
            {
                ["region"] = p.Piece.Region, ["how"] = p.How, ["layer"] = p.SculptAt >= 0 && p.SculptAt < section.Count ? section[p.SculptAt].Index : null,
                ["minThickness"] = p.How == TopSurface.Sculpt && p.SculptAt >= 0 ? Math.Round(p.Hold + section[p.SculptAt].To - section[p.SculptAt].From, 9) : null,
                ["min"] = Math.Round(Enumerable.Range(0, p.Piece.Mesh.Points.Count).Select(i => b.TopShift(p, i)).DefaultIfEmpty(0).Min(), 9),
                ["max"] = Math.Round(Enumerable.Range(0, p.Piece.Mesh.Points.Count).Select(i => b.TopShift(p, i)).DefaultIfEmpty(0).Max(), 9),
                ["grade"] = p.Piece.Plane is var (_, gx, gy) ? Math.Round(Math.Sqrt(gx * gx + gy * gy), 9) : null,
            })]);
            return o;
        });
    }

    /// <summary>A region added to a surface (checked: inside the floor, clear of its holes and other regions); its index.</summary>
    static int AddRegion(ICommandContext ctx, Entity slab, JsonObject surface, JsonObject a)
    {
        var m = ctx.Model;
        var reg = new JsonObject();
        IReadOnlyList<Vec2> loop;
        if ((string?)a["roomId"] is { } roomId)
        {
            var room = Spatial.Require(m, roomId, IfcClass.Space);
            loop = Rooms.Live(room, m, ctx.Services)?.Outer ?? throw new CommandException("The room has no outline: place it again.");
            reg["roomId"] = room.Id;
        }
        else
        {
            var pts = (a["points"] as JsonArray ?? throw new CommandException("A region needs its outline: points, [[x, y], …].")).Select(n => Point(n, "A region's corner")).ToList();
            if (pts.Count < 3 || Math.Abs(Polygon2.SignedArea(pts)) < 1e-6) throw new CommandException("A region needs three corners at least, round an area.");
            if (Polygon2.SignedArea(pts) < 0) pts.Reverse();
            loop = pts;
            reg["outline"] = new JsonArray([.. pts.Select(p => (JsonNode)J(p))]);
        }
        if (Json.Number(a["elevation"]) is { } el) reg["elevation"] = el;
        // Checked by the engine itself: a region it would leave out is refused here.
        var regs = surface["regions"] as JsonArray ?? [];
        var trial = (JsonObject)surface.DeepClone();
        var trialRegs = trial["regions"] as JsonArray ?? [];
        trialRegs.Add(reg.DeepClone());
        trial["regions"] = trialRegs;
        var attrs = slab.CloneAttrs();
        attrs[Key] = trial;
        var shaped = TopSurface.ReadShaped(attrs, Slabs.Corners(slab).Count)!;
        var test = TopSurface.SolveShaped(shaped, Slabs.Contour(slab), Slabs.Corners(slab), OutlineHoles.Built(slab), 0,
            id => m.Get(id) is { } r ? Rooms.Live(r, m, ctx.Services)?.Outer : null);
        if (!test.Any(p => p.Region == shaped.Regions.Count - 1))
            throw new CommandException(loop.Count >= 3 && !loop.All(p => Polygon2.Contains(Slabs.Contour(slab), p))
                ? "The region must lie inside the floor: to give part of a floor running to its edge its own slope, split the floor (Modify → Split)."
                : "The region must lie inside the floor, clear of its holes and its other regions.");
        regs.Add((JsonNode)reg);
        surface["regions"] = regs;
        return regs.Count - 1;
    }
}
