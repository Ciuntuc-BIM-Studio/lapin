using System.Globalization;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.RoofPanels;

/// <summary>
/// The panels as the covering's geometry (docs/plan-roof-panels.md §2.5, stage S2): on a roof that names a profile, the
/// covering's part (or a layered roof's top layer) is replaced by a part per panel, <c>panel:A01-02-01</c>, each in the
/// profile's colour, the offcuts reused in orange. A panel's part is two bodies of one outline: in 3D its sheet profiled as
/// the Panotaj Bilka app draws it (deliver.mesh: the waves across, a tile's steps on its module, a standing seam's ribs,
/// each row and column a little proud of the one it laps so they never fight), simplified to a few facets a wave; in plans,
/// elevations and sections a flat plate of the covering's thickness over what the panel covers, so a drawing shows every
/// joint and the covering as thick as before, never thousands of lines of corrugation. Worked out on every ask from the
/// layout (cached per model revision), never stored. The IFC export (stage S6) writes each panel's plate as an IfcCovering
/// ROOFING aggregated by the roof, in place of the covering slab, with its own set, quantities and its roof plane as an IfcGroup.
/// </summary>
public sealed class PanelBodies : IGeometryFilter
{
    /// <summary>A reused offcut's colour (the app's „Decupaj recuperat pentru dolie”).</summary>
    public const string Reused = "#a15c29";

    public ElementGeometry? Apply(Entity entity, ElementGeometry geometry, IModelView model, IServices services)
    {
        if (entity.Class != IfcClass.Roof || RoofPanels.ChoiceOf(entity) == null) return null;
        // The IFC export (stage S6) writes each panel as an IfcCovering ROOFING of the roof in place of its covering: its body the
        // plate the drawings show (the profiled sheet, thousands of facets a panel, is the 3D view's alone).
        var ifc = services.Get<GeometryOptions>()?.CutOpenings == false;
        if (Roof.Resolve(entity, model) is not { } r || r.Flat || r.Barrel) return null;
        if (RoofPanels.Of(entity, model) is not { } layout) return null;
        string key;
        double below, thickness;
        if (!r.HasLayers) (key, below, thickness) = ("covering", r.StructureDepth, r.Covering.Thickness);
        else
        {
            var top = r.Layers[^1];
            (key, below, thickness) = ($"layer:{r.Layers.Count - 1}", r.Thickness - top.Thickness, top.Thickness);
        }
        var covering = geometry.Parts.FirstOrDefault(p => p.Key == key);
        if (covering == null) return null;
        var parts = geometry.Parts.Where(p => p != covering).ToList();
        var plan = (geometry.Plan ?? []).ToList();
        var colour = layout.Profile.Colors.FirstOrDefault(c => c.Code == layout.Color)?.Rgb ?? covering.Color;
        // The panels' own painted material (made by the command); the covering's when a project from before it has none.
        var material = Materials.Find(model, RoofPanels.MaterialName(layout.Profile, layout.Color))?.Id ?? covering.Material;
        foreach (var part in Parts(layout, below, Math.Max(0.002, thickness), colour, material, ifc))
            parts.Add(part);
        // In plan each panel's outline (what it covers), overhead as the roof's other lines, on plans of 1:200 and larger.
        foreach (var p in layout.Result.Panels)
            plan.Add(new PlanItem("overhead", true, [.. p.Net.Select(v => p.Face.Xyz(v).Xy)], PlanDetail.Fine));
        return geometry with { Parts = parts, Plan = plan, Instance = null };
    }

    /// <summary>
    /// A part per panel of the layout, its bottom <paramref name="below"/> over its plane (along the normal), its plate <paramref name="thickness"/> thick.
    /// Each says what IFC writes of it (stage S6, plan §2.6): the set <c>Lapin_RoofPanel</c> (its code, profile, colour, plane, the
    /// offcut it is cut from or gives, its nominal length, widths, slope lap, covered area, the plane's pitch, its column and row),
    /// <c>Qto_CoveringBaseQuantities</c> (the covering's thickness, the sheet's area as laid and what it covers) and its roof plane
    /// as a group. <paramref name="ifc"/>: for the IFC export, the plate alone (no profiled sheet made).
    /// </summary>
    public static IEnumerable<GeometryPart> Parts(RoofPanels.Layout l, double below, double thickness, string colour, string? material, bool ifc = false)
    {
        var reuse = l.Result.Reuse;
        var gives = reuse.GroupBy(kv => kv.Value.Donor).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(kv => kv.Key).Order(StringComparer.Ordinal)));
        var lastColumn = l.Result.Panels.GroupBy(p => p.Face.Id).ToDictionary(g => g.Key, g => g.Max(p => p.Col));
        var planes = new Dictionary<string, PartGroup>();
        PartGroup Plane(RoofFace face)
        {
            if (planes.TryGetValue(face.Id, out var g)) return g;
            var mine = l.Result.Panels.Where(x => x.Face == face && x.Net.Count >= 3 && Clip.Area(x.Net) >= 1e-8).ToList();
            return planes[face.Id] = new PartGroup(face.Id, face.Id, l.Roof.Name, "Roof plane", "Lapin_RoofPlane",
                [("RoofPlane", face.Id), ("Profile", l.Profile.Name)],
                [new("Pitch", face.PitchDegrees, "ANGLE"), new("Area", Clip.Area(face.Poly), "AREA"), new("CoveredArea", mine.Sum(x => Clip.Area(x.Net)), "AREA"), new("Panels", mine.Count, "COUNT")]);
        }
        foreach (var p in l.Result.Panels)
        {
            var donor = reuse.TryGetValue(p.Id, out var d) ? d.Donor : null;
            var order = l.Profile.OnOrder ? lastColumn[p.Face.Id] - p.Col + 1 : p.Col;
            var mesh = ifc ? default : Sheet(p, l.Profile, below, order);
            if (!ifc && mesh.Tris.Length == 0) continue;
            var face = p.Face;
            var cos = Math.Max(1e-6, face.N.Z);
            var net = p.Net.Select(v => face.Xyz(v, below)).ToList();
            Solid Plate() => Prism.Extrude(net, null, new Vec3(0, 0, thickness / cos), "panel") ?? Prism.Extrude(p.Poly.Select(v => face.Xyz(v, below)).ToList(), null, new Vec3(0, 0, thickness / cos), "panel")!;
            if (net.Count < 3 || Clip.Area(p.Net) < 1e-8) continue;
            // Each panel a shade apart from its neighbours, as the app colours them; a reused offcut orange.
            var shade = donor != null ? Reused : (p.Col + p.Row) % 2 == 1 ? HexColor.Mix(colour, 0.08) : colour;
            List<(string, string)> props = [("Code", p.Id), ("Profile", l.Profile.Name), ("RoofPlane", face.Id)];
            if (l.Color != null) props.Add(("Colour", l.Color));
            if (donor != null) props.Add(("CutFrom", donor));
            if (gives.TryGetValue(p.Id, out var to)) props.Add(("OffcutTo", to));
            List<PartMeasure> measures =
            [
                new("Length", p.Length / 1000.0, "LENGTH"), new("GrossWidth", l.Profile.Gross, "LENGTH"), new("UsefulWidth", l.Profile.Width, "LENGTH"),
                new("SlopeLap", l.Profile.Lap, "LENGTH"), new("CoveredArea", Clip.Area(p.Net), "AREA"), new("Pitch", face.PitchDegrees, "ANGLE"),
                new("Column", p.Col, "COUNT"), new("Row", p.Row, "COUNT"),
            ];
            if (d.Donor != null) measures.Add(new("Shift", d.Shift / 1000.0, "LENGTH"));
            QuantitySet qto = new("Qto_CoveringBaseQuantities",
            [
                new("Width", thickness, "LENGTH", "Thickness"),
                new("GrossArea", Clip.Area(p.Poly), "AREA", "The sheet as laid"),
                new("NetArea", Clip.Area(p.Net), "AREA", "What it covers"),
            ]);
            var name = $"Panel {p.Id} · {p.Length} mm";
            var key = $"panel:{p.Id}";
            yield return (ifc ? new GeometryPart(name, shade, Plate(), material, key) : new GeometryPart(name, shade, mesh, Plate, material, key)) with
            {
                PropertySet = "Lapin_RoofPanel",
                Properties = props,
                Measures = measures,
                Quantities = [qto],
                Group = Plane(face),
            };
        }
    }

    /// <summary>
    /// A panel's sheet as a closed triangle mesh (deliver.mesh): its cut outline split into cells where the profile's height
    /// changes slope across (the waves, a standing seam's ribs) and up the slope (a tile's steps on its module), each cell flat,
    /// the sheet 0.5 mm thick, its edges closed; world metres. <paramref name="order"/>: the column counted from the side the
    /// side laps run from, so each column lies a little over the one it laps.
    /// </summary>
    public static (double[] Xyz, int[] Tris) Sheet(PanelLayout.Panel p, PanelProfile profile, double below, int order)
    {
        var poly = p.Poly;
        if (poly.Count < 3 || Clip.Area(poly) < 1e-8) return ([], []);
        var (x, y) = (p.X, p.Y);
        var gross = profile.Gross;
        var end = y + profile.EffectiveLength(p.Length);
        var height0 = 0.002 + 0.00085 * (p.Row + order);
        var wave = profile.WaveHeight / 1000.0;
        List<double> xx, yy;
        Func<double, double, double> height;
        if (profile.Staggered || profile.Waves <= 0)
        {
            // A standing seam's ribs (the app's Retro Panel), scaled to the profile's width and height.
            double[] at = [0, .010, .017, .024, .032, .127, .130, .139, .142, .262, .265, .274, .277, .493, .500, .508, .515, .523, .542];
            double[] hs = [0, 0, .024, .025, .002, 0, .003, .003, 0, 0, .003, .003, 0, 0, .025, .025, .020, 0, 0];
            var (ku, kh) = (gross / .542, wave / .025);
            var us = at.Select(a => a * ku).ToArray();
            var hk = hs.Select(h => h * kh).ToArray();
            xx = [.. us.Select(u => x + u)];
            yy = [y, end];
            height = (u, _) => height0 + Interp(u - x, us, hk);
        }
        else
        {
            // A tile: its waves across (crest, flank, trough), a step at each module up the slope.
            var pitch = profile.Width / profile.Waves;
            // Rounded enough that no fold between two facets passes 30°: the 3D view draws no crease line along each crest.
            double[] fr = [0, .15, .35, .5, .65, .85, 1];
            double[] wh = [1, .85, .25, 0, .25, .85, 1];
            const double phase = .15;
            var set = new SortedSet<double> { x, x + gross };
            for (var j = -1; j <= profile.Waves + 1; j++)
                foreach (var f in fr) { var u = x + (j + f - phase) * pitch; if (u > x + 1e-9 && u < x + gross - 1e-9) set.Add(u); }
            xx = [.. set];
            var step = profile.Pitch;
            var vs = new SortedSet<double> { y, end };
            for (var j = 0; j <= Math.Ceiling((end - y) / step); j++)
                foreach (var k in new[] { 0.0, 0.018 }) { var v = y + j * step + k; if (v > y + 1e-9 && v < end - 1e-9) vs.Add(v); }
            yy = [.. vs];
            height = (u, v) =>
            {
                var t = ((u - x) / pitch + phase) % 1;
                if (t < 0) t += 1;
                var s = ((v - y) % step + step) % step;
                return height0 + wave * Interp(t, fr, wh) + 0.020 * Interp(s, [0, .018, step], [0, .96, 0]);
            };
        }
        double u0 = Clip.MinX(poly), u1 = Clip.MaxX(poly), v0 = Clip.MinY(poly), v1 = Clip.MaxY(poly);
        var coords = new List<(double U, double V, double H)>();
        var index = new Dictionary<(long, long), int>();
        int Vertex(Vec2 q)
        {
            var k = ((long)Math.Round(q.X * 1e8), (long)Math.Round(q.Y * 1e8));
            if (!index.TryGetValue(k, out var i)) { i = coords.Count; coords.Add((q.X, q.Y, height(q.X, q.Y))); index[k] = i; }
            return i;
        }
        var top = new List<(int A, int B, int C)>();
        for (var i = 0; i + 1 < xx.Count; i++)
        {
            if (xx[i] >= u1 || xx[i + 1] <= u0) continue;
            for (var j = 0; j + 1 < yy.Count; j++)
            {
                if (yy[j] >= v1 || yy[j + 1] <= v0) continue;
                var cell = Clip.Strip(poly, xx[i], xx[i + 1], yy[j], yy[j + 1]);
                if (cell.Count < 3 || Clip.Area(cell) < 1e-10) continue;
                foreach (var (a, b, c) in Triangles(cell))
                {
                    int ia = Vertex(a), ib = Vertex(b), ic = Vertex(c);
                    if (ia != ib && ib != ic && ia != ic) top.Add((ia, ib, ic));
                }
            }
        }
        if (top.Count == 0) return ([], []);
        // The sheet's open edges: the sides no other triangle shares.
        var shell = new Dictionary<(int, int), int>();
        foreach (var (a, b, c) in top)
            foreach (var e in new[] { (a, b), (b, c), (c, a) }) shell[e] = shell.GetValueOrDefault(e) + 1;
        var n = coords.Count;
        var tris = new List<int>(top.Count * 6 + 64);
        foreach (var (a, b, c) in top) tris.AddRange([a, b, c]);
        foreach (var (a, b, c) in top) tris.AddRange([c + n, b + n, a + n]);
        foreach (var ((i, j), k) in shell)
            if (k > shell.GetValueOrDefault((j, i))) tris.AddRange([j, i, i + n, j, i + n, j + n]);
        var face = p.Face;
        var xyz = new double[coords.Count * 6];
        for (var s = 0; s < 2; s++)
            for (var i = 0; i < coords.Count; i++)
            {
                var (u, v, h) = coords[i];
                var w = face.Xyz(new Vec2(u, v), below + h - s * 0.0005);
                var o = (s * coords.Count + i) * 3;
                xyz[o] = w.X; xyz[o + 1] = w.Y; xyz[o + 2] = w.Z;
            }
        return (xyz, [.. tris]);
    }

    /// <summary>A cell as counter-clockwise triangles: a fan when it is convex (the usual), else ear clipped.</summary>
    static IEnumerable<(Vec2, Vec2, Vec2)> Triangles(List<Vec2> cell)
    {
        if (Polygon2.SignedArea(cell) < 0) cell.Reverse();
        var convex = true;
        for (var i = 0; i < cell.Count && convex; i++)
            if (Vec2.Cross(cell[(i + 1) % cell.Count] - cell[i], cell[(i + 2) % cell.Count] - cell[(i + 1) % cell.Count]) < -1e-12) convex = false;
        if (convex)
        {
            for (var i = 1; i + 1 < cell.Count; i++)
                if (Vec2.Cross(cell[i] - cell[0], cell[i + 1] - cell[0]) > 1e-14) yield return (cell[0], cell[i], cell[i + 1]);
            yield break;
        }
        var flat = cell.Select(q => new Vec3(q.X, q.Y, 0)).ToList();
        foreach (var (a, b, c) in Triangulator.Face(flat, [.. Enumerable.Range(0, cell.Count)], null, Vec3.UnitZ))
        {
            var (pa, pb, pc) = (cell[a], cell[b], cell[c]);
            var cr = Vec2.Cross(pb - pa, pc - pa);
            if (cr > 1e-14) yield return (pa, pb, pc);
            else if (cr < -1e-14) yield return (pa, pc, pb);
        }
    }

    /// <summary>Linear interpolation through (<paramref name="xs"/>, <paramref name="ys"/>), held at the ends.</summary>
    static double Interp(double t, IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        if (t <= xs[0]) return ys[0];
        for (var i = 1; i < xs.Count; i++)
            if (t <= xs[i]) return xs[i] - xs[i - 1] < 1e-15 ? ys[i] : ys[i - 1] + (ys[i] - ys[i - 1]) * (t - xs[i - 1]) / (xs[i] - xs[i - 1]);
        return ys[^1];
    }
}
