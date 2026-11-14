using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A beam-and-block floor type's rules (a tester's report, #134): precast joists (a POT joist: an inverted T, its foot
/// <see cref="JoistWidth"/> wide and <see cref="FlangeHeight"/> high, its web <see cref="WebWidth"/> wide, <see cref="JoistHeight"/>
/// in all) laid parallel at <see cref="Spacing"/> axis to axis, ceramic infill blocks between them (<see cref="BlockHeight"/>
/// high, <see cref="BlockLength"/> along the joists, as wide as the gap between the joists' feet), and a cast topping
/// <see cref="Topping"/> thick over them; the joists run the short way of the floor, along x or y, or at <see cref="Angle"/>,
/// and go <see cref="Bearing"/> into the supports past the floor's edge. Kept on the floor type as <c>beamBlock</c>; the
/// type's core is the structure, as deep as the blocks and the topping (<see cref="Depth"/>).
/// </summary>
public sealed record BeamBlockSpec(string Preset, double JoistWidth, double JoistHeight, double FlangeHeight, double WebWidth, string JoistMaterial,
    double Spacing, double BlockHeight, double BlockLength, string BlockMaterial, double Topping, string ToppingMaterial,
    string Direction, double Angle, double Bearing, string Mesh)
{
    public const string Key = "beamBlock";
    public static readonly string[] Directions = ["auto", "x", "y", "angle"];

    /// <summary>The structure's depth: the blocks and the topping.</summary>
    public double Depth => BlockHeight + Topping;
    /// <summary>A block's width across the joists: the gap between their feet.</summary>
    public double BlockWidth => Spacing - JoistWidth;

    /// <summary>
    /// A few systems to start from, indicative only (after the usual POT joist with MIAKO blocks): each the user's to change,
    /// and the user's own catalogue comes in as floor types from a CSV (<see cref="BeamBlock.ImportCsv"/>).
    /// </summary>
    public static readonly IReadOnlyList<BeamBlockSpec> Presets =
    [
        new("POT + MIAKO 19/62.5", 0.16, 0.175, 0.06, 0.06, "Precast joist", 0.625, 0.19, 0.25, "Ceramic infill block", 0.06, "Concrete C20/25", "auto", 0, 0.125, "Mesh 6/100-6/100"),
        new("POT + MIAKO 19/50", 0.16, 0.175, 0.06, 0.06, "Precast joist", 0.5, 0.19, 0.25, "Ceramic infill block", 0.06, "Concrete C20/25", "auto", 0, 0.125, "Mesh 6/100-6/100"),
        new("POT + MIAKO 23/62.5", 0.16, 0.175, 0.06, 0.06, "Precast joist", 0.625, 0.23, 0.25, "Ceramic infill block", 0.06, "Concrete C20/25", "auto", 0, 0.125, "Mesh 6/100-6/100"),
        new("POT + MIAKO 25/50", 0.16, 0.175, 0.06, 0.06, "Precast joist", 0.5, 0.25, 0.25, "Ceramic infill block", 0.05, "Concrete C20/25", "auto", 0, 0.125, "Mesh 6/100-6/100"),
    ];

    public static BeamBlockSpec Default => Presets[0];

    public JsonObject ToJson() => new()
    {
        ["preset"] = Preset, ["joistWidth"] = JoistWidth, ["joistHeight"] = JoistHeight, ["flangeHeight"] = FlangeHeight, ["webWidth"] = WebWidth,
        ["joistMaterial"] = JoistMaterial, ["spacing"] = Spacing, ["blockHeight"] = BlockHeight, ["blockLength"] = BlockLength, ["blockMaterial"] = BlockMaterial,
        ["topping"] = Topping, ["toppingMaterial"] = ToppingMaterial, ["direction"] = Direction, ["angle"] = Angle, ["bearing"] = Bearing, ["mesh"] = Mesh,
        ["depth"] = Math.Round(Depth, 6),
    };

    public static BeamBlockSpec From(JsonNode? n)
    {
        var d = Default;
        if (n is not JsonObject o) return d;
        double N(string k, double def) => Json.Number(o[k]) ?? def;
        string S(string k, string def) => o[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : def;
        var dir = S("direction", d.Direction);
        return new BeamBlockSpec(S("preset", ""), N("joistWidth", d.JoistWidth), N("joistHeight", d.JoistHeight), N("flangeHeight", d.FlangeHeight), N("webWidth", d.WebWidth),
            S("joistMaterial", d.JoistMaterial), N("spacing", d.Spacing), N("blockHeight", d.BlockHeight), N("blockLength", d.BlockLength), S("blockMaterial", d.BlockMaterial),
            N("topping", d.Topping), S("toppingMaterial", d.ToppingMaterial), Directions.Contains(dir) ? dir : "auto", N("angle", 0), N("bearing", d.Bearing), S("mesh", ""));
    }

    public static BeamBlockSpec? Of(Entity? type) => type?.Attrs[Key] is JsonObject o ? From(o) : null;

    public string? Problem()
    {
        if (JoistWidth < 0.06 || JoistWidth > 0.4) return "A joist is 60 to 400 mm wide.";
        if (JoistHeight < 0.05 || JoistHeight > 0.5) return "A joist is 50 to 500 mm high.";
        if (FlangeHeight <= 0 || FlangeHeight >= JoistHeight) return "A joist's foot is lower than the joist.";
        if (WebWidth <= 0 || WebWidth > JoistWidth) return "A joist's web is no wider than its foot.";
        if (Spacing < 0.3 || Spacing > 1.2) return "Joists are 300 to 1200 mm apart, axis to axis.";
        if (BlockWidth < 0.1) return "The joists are too close for a block between them: the spacing is at least the joist's width and 100 mm.";
        if (BlockHeight < 0.05 || BlockHeight > 0.5) return "A block is 50 to 500 mm high.";
        if (BlockLength < 0.1 || BlockLength > 1) return "A block is 100 to 1000 mm long along the joists.";
        if (Topping < 0.02 || Topping > 0.2) return "The topping is 20 to 200 mm thick.";
        if (JoistHeight >= Depth) return "The joist must stay under the top of the topping.";
        if (Bearing < 0 || Bearing > 0.5) return "The bearing is 0 to 500 mm.";
        if (string.IsNullOrWhiteSpace(JoistMaterial) || string.IsNullOrWhiteSpace(BlockMaterial) || string.IsNullOrWhiteSpace(ToppingMaterial)) return "The joists, the blocks and the topping each need a material.";
        return null;
    }

    /// <summary>The joist's section as a schedule writes it: "160×175".</summary>
    public string JoistLabel => $"{Mm(JoistWidth)}×{Mm(JoistHeight)}";
    public string BlockLabel => $"{Mm(BlockHeight)}/{Mm(Spacing)}";
    static string Mm(double m) => Math.Round(m * 1000, 1).ToString(CultureInfo.InvariantCulture);
}

public static class BeamBlock
{
    /// <summary>A floor's own span direction (degrees from +x, the joists' way), over its type's.</summary>
    public const string SpanKey = "spanAngle";
    /// <summary>Precast joists are made in lengths of this step: a joist is ordered at its length rounded up.</summary>
    public const double LengthStep = 0.25;

    /// <summary>One joist: its axis across the floor (v), its run along it (u), whether each end goes into a support.</summary>
    public sealed record Joist(int Index, double V, double U0, double U1)
    {
        public double Length => U1 - U0;
        public double Ordered => Math.Ceiling(Length / LengthStep - 1e-6) * LengthStep;
    }

    /// <summary>One block (whole, or cut at the end of its row) between two joists.</summary>
    public sealed record Block(double U0, double U1, double V0, double V1, bool Whole);

    public sealed record Result(List<Joist> Joists, List<Block> Blocks, List<(double U0, double U1, double V0, double V1)> Rows, List<string> Warnings, Vec2 Along)
    {
        public Vec2 Across => Along.Perp;
        public int WholeBlocks => Blocks.Count(b => b.Whole);
        public int CutBlocks => Blocks.Count(b => !b.Whole);
        public Vec2 W(double u, double v) => Along * u + Across * v;
    }

    /// <summary>The joists' way in a floor: the floor's own angle, else the type's (the short way of the outline's box by default).</summary>
    public static Vec2 Direction(BeamBlockSpec spec, IReadOnlyList<Vec2> outline, double? angle = null)
    {
        if (angle is { } a) return new Vec2(Math.Cos(a * Math.PI / 180), Math.Sin(a * Math.PI / 180));
        switch (spec.Direction)
        {
            case "x": return new Vec2(1, 0);
            case "y": return new Vec2(0, 1);
            case "angle": return new Vec2(Math.Cos(spec.Angle * Math.PI / 180), Math.Sin(spec.Angle * Math.PI / 180));
        }
        var (w, h) = (outline.Max(p => p.X) - outline.Min(p => p.X), outline.Max(p => p.Y) - outline.Min(p => p.Y));
        return w <= h + 1e-9 ? new Vec2(1, 0) : new Vec2(0, 1);
    }

    static List<(double T0, double T1)> Minus(List<(double T0, double T1)> runs, IEnumerable<(double T0, double T1)> cuts)
    {
        foreach (var (c0, c1) in cuts)
            runs = runs.SelectMany(r => r.T1 <= c0 || r.T0 >= c1 ? [r]
                : new[] { (r.T0, Math.Min(r.T1, c0)), (Math.Max(r.T0, c1), r.T1) }.Where(x => x.Item2 - x.Item1 > 1e-6)).ToList();
        return runs;
    }

    static List<(double T0, double T1)> Intersect(List<(double T0, double T1)> a, List<(double T0, double T1)> b) =>
        a.SelectMany(x => b.Select(y => (Math.Max(x.T0, y.T0), Math.Min(x.T1, y.T1)))).Where(r => r.Item2 - r.Item1 > 1e-6).ToList();

    /// <summary>
    /// The layout of a beam-and-block floor over its outline and its openings: the joists at the spacing across the floor,
    /// centred so the strips left at its two sides are equal (cast in the topping's concrete); each joist from edge to edge
    /// of the outline and on into the supports by the bearing, stopped short of an opening by a cast header as wide as a
    /// joist's foot; between neighbouring joists rows of blocks, whole ones from the row's start and one cut at its end;
    /// round an opening the blocks and joists left out over its box and the headers, the gap cast.
    /// </summary>
    public static Result Layout(BeamBlockSpec spec, IReadOnlyList<Vec2> outline, IReadOnlyList<IReadOnlyList<Vec2>> holes, Vec2 along)
    {
        var d = along.Normalized();
        var n = d.Perp;
        var warnings = new List<string>();
        var (bf, s, lb) = (spec.JoistWidth, spec.Spacing, spec.BlockLength);
        var vs = outline.Select(p => Vec2.Dot(p, n)).ToList();
        var (vmin, vmax) = (vs.Min(), vs.Max());
        var width = vmax - vmin;
        List<(double T0, double T1)> Runs(double v) => RoofParts.Crossings(outline, n * v, d);
        // Each opening's box in the joists' frame, with the cast headers at its ends.
        var boxes = holes.Where(h => h.Count >= 3).Select(h => (U0: h.Min(p => Vec2.Dot(p, d)) - bf, U1: h.Max(p => Vec2.Dot(p, d)) + bf,
            V0: h.Min(p => Vec2.Dot(p, n)), V1: h.Max(p => Vec2.Dot(p, n)))).ToList();
        IEnumerable<(double, double)> Cuts(double v0, double v1) => boxes.Where(b => v1 > b.V0 && v0 < b.V1).Select(b => (b.U0, b.U1));

        var joists = new List<Joist>();
        var lines = new List<double>();
        if (width >= bf)
        {
            var count = (int)Math.Floor((width - bf) / s + 1e-9) + 1;
            var edge = (width - (count - 1) * s) / 2;
            for (var k = 0; k < count; k++) lines.Add(vmin + edge + k * s);
        }
        else warnings.Add("The floor is narrower than a joist.");
        foreach (var v in lines)
        {
            // The run along the axis, into the supports by the bearing; then the openings taken out with their headers.
            var runs = Runs(v).Select(r => (r.T0 - spec.Bearing, r.T1 + spec.Bearing)).ToList();
            foreach (var (u0, u1) in Minus(runs, Cuts(v - bf / 2, v + bf / 2)))
                if (u1 - u0 >= 0.3) joists.Add(new Joist(joists.Count, v, u0, u1));
        }

        var blocks = new List<Block>();
        var rows = new List<(double, double, double, double)>();
        for (var k = 0; k + 1 < lines.Count; k++)
        {
            var (v0, v1) = (lines[k] + bf / 2, lines[k + 1] - bf / 2);
            // A row lies where both its sides are in the floor and both its joists carry it.
            var under = Intersect(Runs(v0 + 1e-4), Runs(v1 - 1e-4));
            var carriedA = joists.Where(j => Math.Abs(j.V - lines[k]) < 1e-9).Select(j => (j.U0, j.U1)).ToList();
            var carriedB = joists.Where(j => Math.Abs(j.V - lines[k + 1]) < 1e-9).Select(j => (j.U0, j.U1)).ToList();
            var runs = Minus(Intersect(Intersect(under, carriedA), carriedB), Cuts(v0, v1));
            foreach (var (a, b) in runs)
            {
                var whole = (int)Math.Floor((b - a) / lb + 1e-9);
                for (var i = 0; i < whole; i++) blocks.Add(new Block(a + i * lb, a + (i + 1) * lb, v0, v1, true));
                var end = a + whole * lb;
                // A piece under 20 mm is not cut: the concrete fills it.
                if (b - end > 0.02) blocks.Add(new Block(end, b, v0, v1, false));
                var filled = b - end > 0.02 ? b : end;
                if (filled - a > 1e-6) rows.Add((a, filled, v0, v1));
            }
        }
        return new Result(joists, blocks, rows, warnings, d);
    }

    /// <summary>A beam-and-block floor where it is: its spec, its layout, and its structure between z0 and z0 + depth.</summary>
    public sealed record Floor(Entity Slab, BeamBlockSpec Spec, Result Result, double Z0, int CoreIndex, double Area, double CoreThickness);

    public static Floor? Of(IModelView m, Entity slab)
    {
        if (slab.Class != IfcClass.Slab || BeamBlockSpec.Of(m.RelatingOf(slab.Id, IfcClass.RelDefinesByType)) is not { } spec) return null;
        if (LayerSets.BandsOf(m, slab.Id) is not { } bands || !bands.Any(b => b.Core)) return null;
        var core = bands.First(b => b.Core);
        var outline = Slabs.Contour(slab);
        if (outline.Count < 3) return null;
        var holes = Slabs.Holes(m, slab.Id).Select(h => h.Contour).ToList();
        var dir = Direction(spec, outline, slab.NumOrNull(SpanKey));
        var top = Slabs.Top(m, slab) + core.To;
        var area = Math.Abs(Polygon2.SignedArea(outline)) - holes.Sum(h => Math.Abs(Polygon2.SignedArea(h)));
        return new Floor(slab, spec, Layout(spec, outline, holes, dir), top - spec.Depth, core.Index, area, core.To - core.From);
    }

    /// <summary>A joist's body: its inverted T swept along its run.</summary>
    static Solid? JoistSolid(Result r, BeamBlockSpec spec, Joist j, double z0)
    {
        var (bf, bw, hf, h) = (spec.JoistWidth / 2, spec.WebWidth / 2, spec.FlangeHeight, spec.JoistHeight);
        (double V, double Z)[] t = [(-bf, 0), (bf, 0), (bf, hf), (bw, hf), (bw, h), (-bw, h), (-bw, hf), (-bf, hf)];
        var profile = t.Select(p => r.W(j.U0, j.V + p.V).At(z0 + p.Z)).ToList();
        var dir = r.Along * j.Length;
        return Prism.Extrude(profile, null, new Vec3(dir.X, dir.Y, 0), "joist");
    }

    static Solid? Box(Result r, double u0, double u1, double v0, double v1, double z0, double h, string tag) =>
        Prism.ExtrudeFootprint([r.W(u0, v0), r.W(u1, v0), r.W(u1, v1), r.W(u0, v1)], z0, h, tag);

    /// <summary>
    /// The floor's core as its joists (one part each), its whole blocks (one part of copies of one block), its cut blocks, and
    /// the cast concrete: the core less the joists and the rows of blocks (the topping, the ribs over the joists' feet, the
    /// side strips and the headers round the openings). Without the boolean library the concrete is the topping alone.
    /// </summary>
    public static ElementGeometry Apply(Entity slab, ElementGeometry g, IModelView m, IServices? services, Floor f)
    {
        var core = g.Parts.FirstOrDefault(p => p.Key == $"layer:{f.CoreIndex}");
        var parts = g.Parts.Where(p => p != core).ToList();
        var spec = f.Spec;
        var r = f.Result;
        var mat = (string name) => Materials.Find(m, name)?.Id;
        var (jm, bm, tm) = (mat(spec.JoistMaterial), mat(spec.BlockMaterial), mat(spec.ToppingMaterial));
        var diagnostics = new List<string>(g.Diagnostics ?? []);
        diagnostics.AddRange(r.Warnings);
        if (Math.Abs(f.CoreThickness - spec.Depth) > 0.001)
            diagnostics.Add($"The core is {Math.Round(f.CoreThickness * 1000)} mm thick and the beam-and-block floor {Math.Round(spec.Depth * 1000)} mm deep: save the type again to match them.");
        var cutters = new List<Solid>();
        foreach (var j in r.Joists)
        {
            if (JoistSolid(r, spec, j, f.Z0) is not { } body) continue;
            cutters.Add(body);
            parts.Add(new GeometryPart($"Joist {spec.JoistLabel}", jm != null ? Materials.ColorOf(m, jm) : "#c9a27a", body, jm, $"bb-joist:{j.Index + 1}")
            {
                PropertySet = "Lapin_BeamBlock",
                Properties = [("Role", "Joist"), ("Section", spec.JoistLabel), ("Length", Num(j.Length)), ("Ordered length", Num(j.Ordered))],
            });
        }
        var unit = Prism.ExtrudeFootprint([new(0, 0), new(spec.BlockLength, 0), new(spec.BlockLength, spec.BlockWidth), new(0, spec.BlockWidth)], 0, spec.BlockHeight, "block");
        var whole = r.Blocks.Where(b => b.Whole).ToList();
        var blockColor = bm != null ? Materials.ColorOf(m, bm) : "#c8643c";
        if (whole.Count > 0 && unit != null)
        {
            var bodies = whole.Select(b => Box(r, b.U0, b.U1, b.V0, b.V1, f.Z0, spec.BlockHeight, "block")).OfType<Solid>().ToList();
            var d3 = new Vec3(r.Along.X, r.Along.Y, 0);
            var n3 = new Vec3(r.Across.X, r.Across.Y, 0);
            var copies = whole.Select(b => new PartInstance(r.W(b.U0, b.V0).At(f.Z0), d3, n3, Vec3.UnitZ)).ToList();
            parts.Add(new GeometryPart($"Infill blocks {spec.BlockLabel}", blockColor, LocalBuilder.Combine(bodies, "blocks"), bm, "bb-blocks", unit, copies));
        }
        var cut = r.Blocks.Where(b => !b.Whole).Select(b => Box(r, b.U0, b.U1, b.V0, b.V1, f.Z0, spec.BlockHeight, "block")).OfType<Solid>().ToList();
        if (cut.Count > 0) parts.Add(new GeometryPart($"Cut infill blocks {spec.BlockLabel}", blockColor, LocalBuilder.Combine(cut, "cut-blocks"), bm, "bb-cut-blocks"));
        cutters.AddRange(r.Rows.Select(w => Box(r, w.U0, w.U1, w.V0, w.V1, f.Z0, spec.BlockHeight, "row")).OfType<Solid>());

        Solid? concrete = null;
        var toppingOnly = false;
        if (core != null && services?.Get<ISolidBooleans>() is { } booleans)
        {
            try { concrete = booleans.Difference(core.Solid, cutters, "concrete"); }
            catch (InvalidOperationException ex) { diagnostics.Add($"The cast concrete could not be shaped ({ex.Message}): the topping only."); }
        }
        if (concrete == null)
        {
            toppingOnly = true;
            var outline = Slabs.Contour(slab);
            var holes = Slabs.Holes(m, slab.Id).Select(h => (IReadOnlyList<Vec3>)h.Contour.Select(p => p.At(f.Z0 + spec.BlockHeight)).ToList()).ToList();
            concrete = Prism.Extrude(outline.Select(p => p.At(f.Z0 + spec.BlockHeight)).ToList(), holes, new Vec3(0, 0, spec.Topping), "topping");
        }
        if (concrete != null)
            parts.Add(new GeometryPart(toppingOnly ? "Topping" : "Cast concrete", tm != null ? Materials.ColorOf(m, tm) : "#a8a8a8", concrete, tm, "bb-concrete")
            {
                PropertySet = "Lapin_BeamBlock",
                Properties = [("Role", "Topping and ribs"), ("Topping", Num(spec.Topping)), ("Mesh", spec.Mesh)],
            });

        // In plan: the joists' axes (a detail plan), the span arrow across the middle.
        var plan = new List<PlanItem>(g.Plan ?? []);
        foreach (var j in r.Joists) plan.Add(new PlanItem("symbol", false, [r.W(j.U0, j.V), r.W(j.U1, j.V)], PlanDetail.Fine));
        plan.AddRange(SpanArrow(Slabs.Contour(slab), r.Along));
        return g with { Parts = parts, Plan = plan, Diagnostics = diagnostics.Count > 0 ? diagnostics : null };
    }

    /// <summary>The one-way span symbol: a line through the floor's middle along the joists, a filled arrowhead at each end.</summary>
    public static IEnumerable<PlanItem> SpanArrow(IReadOnlyList<Vec2> outline, Vec2 along)
    {
        var d = along.Normalized();
        var n = d.Perp;
        var c = new Vec2(outline.Average(p => p.X), outline.Average(p => p.Y));
        if (!Polygon2.Contains(outline, c)) c = outline[0] * 0.5 + outline[outline.Count / 2] * 0.5;
        var runs = RoofParts.Crossings(outline, c, d);
        var run = runs.FirstOrDefault(x => x.T0 <= 0 && x.T1 >= 0);
        if (run == default) run = runs.FirstOrDefault();
        var len = Math.Min(run.T1 - run.T0, 6) * 0.6;
        if (len < 0.3) yield break;
        var (a, b) = (c + d * (-len / 2), c + d * (len / 2));
        var head = Math.Min(0.3, len / 5);
        yield return new PlanItem("symbol", false, [a, b]);
        yield return new PlanItem("symbol", true, [b, b - d * head + n * (head / 3), b - d * head - n * (head / 3)]);
        yield return new PlanItem("symbol", true, [a, a + d * head - n * (head / 3), a + d * head + n * (head / 3)]);
    }

    static string Num(double v) => Math.Round(v, 3).ToString(CultureInfo.InvariantCulture);

    /// <summary>The floor's beam-and-block quantities: the joists (count, length), the blocks (whole, cut), the cast concrete, the area, the mesh.</summary>
    public static IEnumerable<QuantitySet> Quantities(Floor f, ElementGeometry g, IModelView m)
    {
        var r = f.Result;
        var concrete = g.Parts.FirstOrDefault(p => p.Key == "bb-concrete") is { } c ? Measure.Volume(c.Solid) : 0;
        var joistVolume = g.Parts.Where(p => p.Key?.StartsWith("bb-joist:", StringComparison.Ordinal) == true).Sum(p => Measure.Volume(p.Solid));
        var blockVolume = g.Parts.Where(p => p.Key is "bb-blocks" or "bb-cut-blocks").Sum(p => Measure.Volume(p.Solid));
        var qs = new List<Quantity>
        {
            new("JoistCount", r.Joists.Count, "COUNT", "Joists"),
            new("JoistLength", r.Joists.Sum(j => j.Length), "LENGTH", "Joists' length"),
            new("BlockCount", r.WholeBlocks, "COUNT", "Whole blocks"),
            new("CutBlockCount", r.CutBlocks, "COUNT", "Cut blocks"),
            new("ConcreteVolume", concrete, "VOLUME", "Cast concrete"),
            new("Area", f.Area, "AREA", "Area"),
        };
        if (f.Spec.Mesh.Length > 0) qs.Add(new("MeshArea", f.Area, "AREA", "Mesh area"));
        yield return new QuantitySet("Lapin_BeamBlockQuantities", qs);
        var mat = (string name) => Materials.Find(m, name)?.Id;
        yield return new QuantitySet("Lapin_SlabLayerQuantities", [new("NetVolume", joistVolume, "VOLUME", "Net volume"), new("Count", r.Joists.Count, "COUNT", "Pieces")], "bb-joists", "Joists", mat(f.Spec.JoistMaterial));
        yield return new QuantitySet("Lapin_SlabLayerQuantities", [new("NetVolume", blockVolume, "VOLUME", "Net volume"), new("Count", r.Blocks.Count, "COUNT", "Pieces")], "bb-blocks", "Infill blocks", mat(f.Spec.BlockMaterial));
        yield return new QuantitySet("Lapin_SlabLayerQuantities", [new("NetArea", f.Area, "AREA", "Net area"), new("NetVolume", concrete, "VOLUME", "Net volume")], "bb-concrete", "Cast concrete", mat(f.Spec.ToppingMaterial));
    }

    /// <summary>
    /// Sets a floor type's beam-and-block rules (null: a layered floor again): the materials made by name when the project
    /// lacks them, the type's core made as deep as the structure (its other layers kept), and the timber rules a floor type
    /// may have had taken off (a floor is one or the other).
    /// </summary>
    public static void Set(IEdit edit, Entity type, BeamBlockSpec? spec)
    {
        var attrs = type.CloneAttrs();
        if (spec == null)
        {
            attrs.Remove(BeamBlockSpec.Key);
            edit.Replace(type.With(attrs: attrs));
            return;
        }
        if (spec.Problem() is { } problem) throw new CommandException(problem);
        Materials.Ensure(edit, spec.JoistMaterial, "#c9a27a");
        Materials.Ensure(edit, spec.BlockMaterial, "#c8643c");
        Materials.Ensure(edit, spec.ToppingMaterial);
        attrs[BeamBlockSpec.Key] = spec.ToJson();
        attrs.Remove("wood");
        edit.Replace(type.With(attrs: attrs));
        var layers = LayerSets.LayersOf(edit.Model, type.Id).Select(l => LayerSets.SpecOf(edit.Model, l)).ToList();
        var core = layers.FindIndex(l => l.Core);
        if (core < 0 && layers.Count > 0) core = layers.IndexOf(layers.MaxBy(l => l.Thickness)!);
        if (core < 0) return;
        if (Math.Abs(layers[core].Thickness - spec.Depth) < 1e-9 && layers[core].Core) return;
        layers[core] = layers[core] with { Thickness = Math.Round(spec.Depth, 6), Core = true };
        Slabs.UpdateType(edit, edit.Model.Get(type.Id)!, null, layers);
    }

    static readonly string[] CsvColumns = ["Name", "Joist width (mm)", "Joist height (mm)", "Foot height (mm)", "Web width (mm)", "Spacing (mm)", "Block height (mm)", "Block length (mm)", "Topping (mm)", "Bearing (mm)", "Joist material", "Block material", "Topping material", "Mesh"];

    /// <summary>The catalogue's CSV header, for a sheet to fill in.</summary>
    public static string CsvTemplate()
    {
        var p = BeamBlockSpec.Default;
        string M(double v) => Math.Round(v * 1000, 1).ToString(CultureInfo.InvariantCulture);
        return string.Join(";", CsvColumns) + "\n" + string.Join(";", [p.Preset, M(p.JoistWidth), M(p.JoistHeight), M(p.FlangeHeight), M(p.WebWidth), M(p.Spacing), M(p.BlockHeight),
            M(p.BlockLength), M(p.Topping), M(p.Bearing), p.JoistMaterial, p.BlockMaterial, p.ToppingMaterial, p.Mesh]) + "\n";
    }

    /// <summary>
    /// The user's own catalogue as a spreadsheet writes it (semicolon, comma or tab separated, a header row naming the columns
    /// of <see cref="CsvColumns"/> in any order, lengths in mm, a decimal point or comma): each row a beam-and-block system.
    /// A missing column takes the first preset's value; a row with a problem is refused with its line.
    /// </summary>
    public static List<BeamBlockSpec> ParseCsv(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count < 2) throw new CommandException("The file has no rows under its header.");
        var sep = lines[0].Contains(';') ? ';' : lines[0].Contains('\t') ? '\t' : ',';
        string[] Cells(string l) => l.Split(sep).Select(c => c.Trim().Trim('"').Trim()).ToArray();
        string Fold(string h) => new string(h.ToLowerInvariant().Where(char.IsLetter).ToArray());
        var header = Cells(lines[0]).Select(Fold).ToList();
        int Col(string name) { var k = Fold(name); var i = header.IndexOf(k); return i >= 0 ? i : header.FindIndex(h => h.Length > 0 && (h.StartsWith(k.Replace("mm", ""), StringComparison.Ordinal))); }
        if (Col("Name") < 0) throw new CommandException("The header has no Name column.");
        var d = BeamBlockSpec.Default;
        var o = new List<BeamBlockSpec>();
        for (var li = 1; li < lines.Count; li++)
        {
            var cells = Cells(lines[li]);
            string? Cell(string name) { var i = Col(name); return i >= 0 && i < cells.Length && cells[i].Length > 0 ? cells[i] : null; }
            double Mm(string name, double def)
            {
                if (Cell(name) is not { } s) return def;
                if (sep != ',') s = s.Replace(',', '.');
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v / 1000 : throw new CommandException($"Line {li + 1}: „{s}” is not a number ({name}).");
            }
            var name = Cell("Name") ?? throw new CommandException($"Line {li + 1}: no name.");
            var spec = new BeamBlockSpec(name, Mm("Joist width (mm)", d.JoistWidth), Mm("Joist height (mm)", d.JoistHeight), Mm("Foot height (mm)", d.FlangeHeight), Mm("Web width (mm)", d.WebWidth),
                Cell("Joist material") ?? d.JoistMaterial, Mm("Spacing (mm)", d.Spacing), Mm("Block height (mm)", d.BlockHeight), Mm("Block length (mm)", d.BlockLength), Cell("Block material") ?? d.BlockMaterial,
                Mm("Topping (mm)", d.Topping), Cell("Topping material") ?? d.ToppingMaterial, "auto", 0, Mm("Bearing (mm)", d.Bearing), Cell("Mesh") ?? "");
            if (spec.Problem() is { } problem) throw new CommandException($"Line {li + 1} ({name}): {problem}");
            o.Add(spec);
        }
        return o;
    }

    /// <summary>The catalogue's rows as floor types (one undo step): a type of that name changed, any other made with the structure as its only layer.</summary>
    public static List<string> ImportCsv(IEdit edit, string text)
    {
        var names = new List<string>();
        foreach (var spec in ParseCsv(text))
        {
            var type = edit.Model.OfClass(IfcClass.SlabType).FirstOrDefault(t => string.Equals(t.Name, spec.Preset, StringComparison.OrdinalIgnoreCase))
                ?? Slabs.CreateType(edit, spec.Preset, [new LayerSpec(spec.ToppingMaterial, Math.Round(spec.Depth, 6), Core: true)]);
            Set(edit, type, spec);
            names.Add(type.Name);
        }
        return names;
    }

    /// <summary>
    /// The configurator's preview, built by the same layout as a floor: a sample floor 5 × 4.2 m with an opening, its joists
    /// and blocks in plan, two bays in section, the counts.
    /// </summary>
    public static JsonObject Preview(BeamBlockSpec spec)
    {
        if (spec.Problem() is { } problem) return new JsonObject { ["problem"] = problem };
        Vec2[] outline = [new(0, 0), new(5, 0), new(5, 4.2), new(0, 4.2)];
        Vec2[] hole = [new(3.4, 2.6), new(4.4, 2.6), new(4.4, 3.8), new(3.4, 3.8)];
        var r = Layout(spec, outline, [hole], Direction(spec, outline));
        JsonArray Pts(IEnumerable<Vec2> ps) => new([.. ps.SelectMany(p => new[] { (JsonNode)Math.Round(p.X, 4), Math.Round(p.Y, 4) })]);
        var (bf, bw, hf, h) = (spec.JoistWidth / 2, spec.WebWidth / 2, spec.FlangeHeight, spec.JoistHeight);
        var section = new JsonArray();
        // Two bays across: three joists, two blocks, the concrete outline over the whole.
        for (var k = 0; k < 3; k++)
        {
            var v = k * spec.Spacing;
            section.Add((JsonNode)new JsonObject { ["role"] = "joist", ["pts"] = Pts(new (double, double)[] { (-bf, 0), (bf, 0), (bf, hf), (bw, hf), (bw, h), (-bw, h), (-bw, hf), (-bf, hf) }.Select(p => new Vec2(v + p.Item1, p.Item2))) });
            if (k < 2) section.Add((JsonNode)new JsonObject { ["role"] = "block", ["pts"] = Pts([new(v + bf, 0), new(v + spec.Spacing - bf, 0), new(v + spec.Spacing - bf, spec.BlockHeight), new(v + bf, spec.BlockHeight)]) });
        }
        section.Insert(0, (JsonNode)new JsonObject { ["role"] = "concrete", ["pts"] = Pts([new(-spec.Spacing / 2, 0), new(2.5 * spec.Spacing, 0), new(2.5 * spec.Spacing, spec.Depth), new(-spec.Spacing / 2, spec.Depth)]) });
        return new JsonObject
        {
            ["depth"] = Math.Round(spec.Depth, 6), ["outline"] = Pts(outline), ["hole"] = Pts(hole),
            ["joists"] = new JsonArray([.. r.Joists.Select(j => (JsonNode)Pts([r.W(j.U0, j.V - spec.JoistWidth / 2), r.W(j.U1, j.V - spec.JoistWidth / 2), r.W(j.U1, j.V + spec.JoistWidth / 2), r.W(j.U0, j.V + spec.JoistWidth / 2)]))]),
            ["blocks"] = new JsonArray([.. r.Blocks.Select(b => (JsonNode)new JsonObject { ["whole"] = b.Whole, ["pts"] = Pts([r.W(b.U0, b.V0), r.W(b.U1, b.V0), r.W(b.U1, b.V1), r.W(b.U0, b.V1)]) })]),
            ["section"] = section,
            ["counts"] = new JsonObject { ["joists"] = r.Joists.Count, ["blocks"] = r.WholeBlocks, ["cutBlocks"] = r.CutBlocks },
            ["warnings"] = new JsonArray([.. r.Warnings.Select(w => (JsonNode)w)]),
        };
    }
}

/// <summary>
/// The beam-and-block floors as an order list (a tester's report, #134): per floor, the joists by their ordered length
/// (rounded up to the makers' 250 mm step), the whole and the cut blocks, the cast concrete and the mesh's area.
/// </summary>
public sealed class BeamBlockSchedule : IScheduleSource
{
    public string Kind => "beamBlock";
    public string Label => "Beam and block floors";

    public sealed record Row(string Floor, string Item, string Size, int Count, double Length, double Quantity, string Unit);

    public static List<Row> Rows(IQueryContext q)
    {
        var o = new List<Row>();
        foreach (var slab in q.Model.OfClass(IfcClass.Slab).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Id))
        {
            if (BeamBlock.Of(q.Model, slab) is not { } f) continue;
            var name = string.IsNullOrWhiteSpace(slab.Name) ? slab.Id : slab.Name;
            foreach (var g in f.Result.Joists.GroupBy(j => Math.Round(j.Ordered, 3)).OrderByDescending(g => g.Key))
                o.Add(new Row(name, "Joist", $"{f.Spec.JoistLabel} · {g.Key.ToString("0.00", CultureInfo.InvariantCulture)} m", g.Count(), g.Key, g.Sum(j => j.Length), "m"));
            o.Add(new Row(name, "Infill block", f.Spec.BlockLabel, f.Result.WholeBlocks, f.Spec.BlockLength, f.Result.WholeBlocks, "pcs"));
            if (f.Result.CutBlocks > 0) o.Add(new Row(name, "Cut infill block", f.Spec.BlockLabel, f.Result.CutBlocks, 0, f.Result.CutBlocks, "pcs"));
            var concrete = q.Geometry(slab.Id)?.Parts.FirstOrDefault(p => p.Key == "bb-concrete") is { } c ? Measure.Volume(c.Solid) : 0;
            o.Add(new Row(name, "Cast concrete", f.Spec.ToppingMaterial, 0, 0, concrete, "m³"));
            if (f.Spec.Mesh.Length > 0) o.Add(new Row(name, "Mesh", f.Spec.Mesh, 0, 0, f.Area, "m²"));
        }
        return o;
    }

    public ScheduleGrid Grid(IQueryContext q)
    {
        string N(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);
        var rows = Rows(q);
        var grid = rows.Select(x => (IReadOnlyList<string>)[x.Floor, x.Item, x.Size, x.Count > 0 ? x.Count.ToString(CultureInfo.InvariantCulture) : "", x.Length > 0 ? N(x.Length) : "",
            x.Unit == "pcs" ? N(x.Quantity, "0") : N(x.Quantity, x.Unit == "m³" ? "0.000" : "0.00"), x.Unit]).ToList();
        return new ScheduleGrid(Label, ["Floor", "Item", "Size", "Pieces", "Length (m)", "Quantity", "Unit"], grid);
    }
}
