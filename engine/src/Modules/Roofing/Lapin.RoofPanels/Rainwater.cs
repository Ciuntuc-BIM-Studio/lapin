using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.RoofPanels;

/// <summary>
/// A roof's rainwater system sized and counted (docs/plan-roof-panels.md §2.4, stage S5), the Panotaj Bilka app's
/// (pluvial.sizing, accessories.drainage_areas, the stats of deliver.export_data and the "Pluvial" rows of
/// estimate.make_estimate) rewritten on the layout's faces and edges and on the roof's own gutters and downpipes
/// (<see cref="Drainage"/>): each slope's area is routed to the eaves that have a gutter (a slope with none drains over a
/// pitch break to the slope under it, or down a valley to the eaves at its foot); a slope's least number of downpipes is
/// the most of one, one per 10 m of its gutter and its flow over a downpipe's capacity; the system is the one asked, or
/// the larger above 100 m² of roof or a heavier rain than the default; the downpipes are the number asked or the least.
/// The roof's native gutters and downpipes are the model: what has no gutter has no outlet, the downpipes' average
/// length is the model's, and the sizing says where the model differs (fewer downpipes, another gutter size). Worked out
/// on every ask, never stored.
/// </summary>
public static class Rainwater
{
    /// <summary>A rainwater system: its id (<c>125/90</c>), its gutter's and downpipe's sizes (mm), a downpipe's capacity (l/s).</summary>
    public sealed record System(string Id, int Gutter, int Pipe, double Capacity);

    /// <summary>A line of the rainwater system, data: its code and name (<c>{g}</c> the gutter's size, <c>{p}</c> the downpipe's), unit, coded rule, how many a unit counts (<see cref="Per"/>), its pack and what it is made from.</summary>
    public sealed record ItemDef(string Code, string Name, string Unit, string Rule, double Per, int Pack, string? MadeFrom, string Note);

    /// <summary>The sizing's data (profiles.json, <c>common.rainwater</c>): the default rain (l/s·m²), the longest gutter a downpipe takes (m), the roof area above which the larger system is recommended (m²), the systems, the lengths (mm) and the lines.</summary>
    public sealed record Data(double Rain, double MaxRun, double LargerAbove, IReadOnlyList<System> Systems, int GutterLength, int HookSpacing, int HookEnd,
        int PipeLength, int PipeDefault, int ClampSpacing, int ClampFirst, IReadOnlyList<ItemDef> Items, string Note, string Source)
    {
        public System? Find(string? id) => Systems.FirstOrDefault(s => s.Id == id);

        static int I(JsonNode? n, int fallback) => Json.Number(n) is { } v ? (int)Math.Round(v) : fallback;

        public static Data Read(JsonObject o) => new(
            Json.Number(o["rain"]) ?? 0.05, Json.Number(o["maxRun"]) ?? 10, Json.Number(o["largerAbove"]) ?? 100,
            [.. ((o["systems"] as JsonArray) ?? []).OfType<JsonObject>().Select(s => new System((string?)s["id"] ?? "", I(s["gutter"], 125), I(s["pipe"], 90), Json.Number(s["capacity"]) ?? 1.5))],
            I(o["gutterLength"], 4000), I(o["hookSpacing"], 600), I(o["hookEnd"], 200), I(o["pipeLength"], 3000), I(o["pipeDefault"], 3000),
            I(o["clampSpacing"], 2000), I(o["clampFirst"], 150),
            [.. ((o["items"] as JsonArray) ?? []).OfType<JsonObject>().Select(i => new ItemDef((string?)i["code"] ?? "", (string?)i["name"] ?? "", (string?)i["unit"] ?? "piece",
                (string?)i["rule"] ?? "", Json.Number(i["per"]) ?? 1, I(i["pack"], 1), (string?)i["madeFrom"] is { Length: > 0 } mf ? mf : null, (string?)i["note"] ?? ""))],
            (string?)o["note"] ?? "", (string?)o["source"] ?? "");
    }

    // ------------------------------------------------------------------------------------------------ the roof's own gutters

    /// <summary>A native gutter in plan (along an eave line, set out by its offset) and the outline edge it is on.</summary>
    public sealed record Gutter(Vec2 A, Vec2 B, int Edge);

    /// <summary>A native downpipe: the gutter it leaves, where (in plan), its length, whether it is an element of its own.</summary>
    public sealed record Pipe(int Gutter, Vec2 At, double Length, bool Element);

    /// <summary>The roof's own gutters and downpipes: whether it has gutters, their size and how far out they are set, the downpipes' size and mode (auto, elements, none).</summary>
    public sealed record Native(List<Gutter> Gutters, List<Pipe> Pipes, double GutterWidth, double PipeDiameter, double Offset, string Mode)
    {
        public bool HasGutters => Gutters.Count > 0;
        public static readonly Native None = new([], [], 0.125, Drainage.DefaultDiameter, 0, "auto");
    }

    /// <summary>The roof's gutters and downpipes as the architecture builds them; the automatic downpipes' length as its quantities count it (to the ground floor).</summary>
    public static Native Read(Entity roof, IModelView m)
    {
        if (Drainage.DrainOf(roof, m) is not { } d) return Native.None with { Mode = roof.Str("downpipes") ?? "auto" };
        var spec = Drainage.Spec.Of(roof);
        var diameter = Drainage.PipeDiameterOf(roof);
        var gutters = d.Gutters.Select(g => new Gutter(g.A.Xy, g.B.Xy, g.Edge)).ToList();
        var ground = Drainage.GroundAt(m, null);
        var pipes = new List<Pipe>();
        foreach (var (gi, station, element) in d.Outlets)
        {
            var g = d.Gutters[gi];
            var at = (g.A + (g.B - g.A) * (station / Math.Max(g.Length, 1e-12))).Xy;
            double length;
            if (element != null) length = Drainage.Place(m, element) is { } p ? Drainage.LengthOf(p.Chain) : 0;
            else
            {
                // As Roof.Quantities counts an automatic downpipe: from its outlet, in to the wall at 45°, down to the ground floor.
                var outlet = Drainage.OutletAt(g, station, d.Outlets.Where(y => y.Gutter == gi).Select(y => y.Station).ToList(), spec);
                length = Drainage.LengthOf(Drainage.Chain(outlet, g.Out, Drainage.DefaultRoute(Drainage.OutFromWall(d.R, g, spec)), ground, false, diameter / 2));
            }
            pipes.Add(new Pipe(gi, at, length, element != null));
        }
        return new Native(gutters, pipes, spec.Width, diameter, spec.Offset, roof.Str("downpipes") ?? "auto");
    }

    /// <summary>The native gutter an eave edge of the covering runs along (in plan, parallel, within the gutter's offset), or −1.</summary>
    public static int GutterOf(RoofEdge e, Native n)
    {
        var (a, b) = (e.A.Xy, e.B.Xy);
        var d = b - a;
        if (d.Length < 1e-9) return -1;
        var mid = (a + b) * 0.5;
        var u = d.Normalized();
        for (var i = 0; i < n.Gutters.Count; i++)
        {
            var g = n.Gutters[i];
            var gd = g.B - g.A;
            var len = gd.Length;
            if (len < 1e-9 || Math.Abs(Vec2.Cross(u, gd * (1 / len))) > 0.02) continue;
            var t = Vec2.Dot(mid - g.A, gd) / (len * len);
            if (t < -0.01 || t > 1.01) continue;
            if (Math.Abs(Vec2.Cross(gd * (1 / len), mid - g.A)) <= n.Offset + 0.05) return i;
        }
        return -1;
    }

    // ------------------------------------------------------------------------------------------------ the sizing

    /// <summary>
    /// A slope with an eave that has a gutter: its eave's length and the area that drains to it (its own and what slopes
    /// without a gutter pass on), the flow (l/s), the least downpipes for each system, the downpipes the estimate gives it
    /// (the least, or the number asked shared out by each slope's least) and those the model has on it.
    /// </summary>
    public sealed record Run(string Face, double Eave, double Area, double Flow, IReadOnlyDictionary<string, int> Minimum, int Planned, int Model);

    /// <summary>
    /// The sizing (pluvial.sizing): the rain, the area drained, the recommended system, the one asked (<c>auto</c>) and the one
    /// used, the least downpipes for each system, the number asked and the number counted, whether that is under the least,
    /// whether the system is not the recommended one, the area no gutter takes and its slopes, each run.
    /// </summary>
    public sealed record Sizing(double Rain, double RoofArea, string Recommended, string Selection, string Selected, IReadOnlyDictionary<string, int> Minimum,
        int? Override, int Downpipes, bool BelowMinimum, bool Differs, double Unrouted, List<string> NoOutlet, List<Run> Runs);

    /// <summary>The gutters counted as the app counts them along the eaves: 4 m pieces, hooks, outer and inner 90° corners, odd corners, eaves, corners, straight joints, free ends, the odd corners' angles, joints.</summary>
    public sealed record Counts(int Pieces, int Hooks, int Outer, int Inner, int Special, int Eaves, int Corners, int Straight, int Ends, List<double> Angles, int Joints);

    /// <summary>The roof's rainwater: the sizing, the gutters' counts, the model's gutters and downpipes, a downpipe's average length (m) and clamps, the lines, what to check.</summary>
    public sealed record Result(Sizing Sizing, Counts Counts, Native Native, double AveragePipe, int Clamps, List<Takeoff.Item> Items, List<string> Warnings, System System);

    /// <summary>
    /// Where each slope's water goes (accessories.drainage_areas): the area that reaches each slope with a guttered eave, and
    /// what reaches none (with the slopes it comes from). A slope without a gutter drains over a pitch break to the slope
    /// under it, else down a valley to the guttered eaves at the valley's foot, shared by length.
    /// </summary>
    public static (Dictionary<string, double> Areas, double Missing, List<string> NoOutlet) Route(IReadOnlyList<RoofFace> faces, IReadOnlyList<RoofEdge> edges,
        IReadOnlyDictionary<string, double> eaves)
    {
        var byId = faces.ToDictionary(f => f.Id);
        var paths = faces.ToDictionary(f => f.Id, _ => new List<(string To, double Length)>());
        foreach (var e in edges.Where(e => EdgeKinds.IsBreak(e.Kind) && e.Faces.Count == 2))
        {
            var o = e.Faces.OrderBy(k => RoofFaces.InwardHeight(byId[k], e.A, e.B)).ToList();
            paths[o[1]].Add((o[0], e.Length));
        }
        static bool Same(Vec3 a, Vec3 b) => (a - b).Length < 1e-6;
        var guttered = edges.Where(e => e.Kind == EdgeKinds.Eave && eaves.GetValueOrDefault(e.Faces[0]) > 0).ToList();
        foreach (var e in edges.Where(e => e.Kind == EdgeKinds.Valley))
        {
            var foot = e.A.Z < e.B.Z ? e.A : e.B;
            var at = guttered.Where(x => Same(x.A, foot) || Same(x.B, foot)).Select(x => x.Faces[0]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            foreach (var face in e.Faces)
                if (eaves.GetValueOrDefault(face) <= 0 && paths[face].Count == 0) paths[face].AddRange(at.Select(x => (x, e.Length)));
        }
        var areas = faces.Where(f => eaves.GetValueOrDefault(f.Id) > 0).ToDictionary(f => f.Id, _ => 0.0);
        double Go(string face, double amount, HashSet<string> seen)
        {
            if (seen.Contains(face)) return amount;
            if (areas.ContainsKey(face)) { areas[face] += amount; return 0; }
            var to = paths[face];
            if (to.Count == 0) return amount;
            var total = to.Sum(x => x.Length);
            return to.Sum(x => Go(x.To, amount * x.Length / total, [.. seen, face]));
        }
        double missing = 0;
        var none = new List<string>();
        foreach (var f in faces)
        {
            var lost = Go(f.Id, Clip.Area(f.Poly), []);
            if (lost > 0.01) none.Add(f.Id);
            missing += lost;
        }
        return (areas, missing, none);
    }

    /// <summary>pluvial.sizing on the runs (the slopes with a guttered eave: its length and the area that reaches it, both as the app rounds them).</summary>
    public static Sizing Size(Data d, IReadOnlyList<(string Face, double Eave, double Area, int Model)> runs, double rain, string? system, int? downpipes, double unrouted, List<string> noOutlet)
    {
        var area = runs.Sum(r => r.Area);
        var small = d.Systems[0];
        var large = d.Systems[^1];
        var recommended = area > d.LargerAbove || rain > d.Rain ? large.Id : small.Id;
        var selected = d.Find(system)?.Id ?? recommended;
        var mins = runs.Select(r =>
        {
            var flow = r.Area * rain;
            return (r, flow, Min: (IReadOnlyDictionary<string, int>)d.Systems.ToDictionary(s => s.Id, s => Math.Max(1, Math.Max((int)Math.Ceiling(r.Eave / d.MaxRun), (int)Math.Ceiling(flow / s.Capacity)))));
        }).ToList();
        var minimum = d.Systems.ToDictionary(s => s.Id, s => mins.Sum(x => x.Min[s.Id]));
        var count = downpipes ?? minimum[selected];
        var planned = Share(count, [.. mins.Select(x => x.Min[selected])], downpipes == null);
        var out_ = mins.Select((x, i) => new Run(x.r.Face, x.r.Eave, x.r.Area, Math.Round(x.flow, 4), x.Min, planned[i], x.r.Model)).ToList();
        return new Sizing(rain, area, recommended, d.Find(system) == null ? "auto" : selected, selected, minimum, downpipes, count,
            count < minimum[selected], selected != recommended, unrouted, noOutlet, out_);
    }

    /// <summary>
    /// The downpipes shared out over the slopes (the app's README: by gutter length and flow, which each slope's least already
    /// weighs): each its least when the number is the least; else in proportion to them, the largest remainders first, and
    /// one on every slope while there are enough.
    /// </summary>
    public static int[] Share(int count, int[] weights, bool exact)
    {
        if (exact || weights.Length == 0) return weights;
        var total = weights.Sum();
        var o = new int[weights.Length];
        if (total <= 0) return o;
        var rest = new List<(int I, double R)>();
        for (var i = 0; i < weights.Length; i++)
        {
            var share = (double)count * weights[i] / total;
            o[i] = (int)Math.Floor(share);
            rest.Add((i, share - o[i]));
        }
        foreach (var (i, _) in rest.OrderByDescending(x => x.R).ThenBy(x => x.I).Take(count - o.Sum())) o[i]++;
        if (count >= weights.Length)
            for (var i = 0; i < o.Length; i++)
                while (o[i] == 0)
                {
                    var j = Array.IndexOf(o, o.Max());
                    if (o[j] <= 1) break;
                    o[j]--; o[i]++;
                }
        return o;
    }

    /// <summary>The gutters along the guttered eaves (deliver.export_data): pieces, hooks, corners by angle, free ends, joints.</summary>
    public static Counts Count(Data d, IReadOnlyList<RoofEdge> eaves, IReadOnlyList<RoofEdge> edges)
    {
        static double R6(double v) => Math.Round(v, 6);
        static bool Same(Vec3 a, Vec3 b) => (a - b).Length < 1e-6;
        var stock = d.GutterLength / 1000.0;
        var (hookEnd, hookStep) = (d.HookEnd / 1000.0, d.HookSpacing / 1000.0);
        var pieces = eaves.Sum(e => (int)Math.Ceiling(R6(e.Length) / stock));
        var hooks = eaves.Sum(e => (int)Math.Ceiling((R6(e.Length) - hookEnd) / hookStep) + 1);
        // The eaves' ends as nodes, each with the far ends of the eaves meeting there.
        var nodes = new List<(Vec3 P, List<Vec3> To)>();
        void Link(Vec3 p, Vec3 q)
        {
            var i = nodes.FindIndex(n => Same(n.P, p));
            if (i < 0) nodes.Add((p, [q])); else nodes[i].To.Add(q);
        }
        foreach (var e in eaves) { Link(e.A, e.B); Link(e.B, e.A); }
        var interior = edges.Where(e => e.Kind == EdgeKinds.Valley).Select(e => e.A.Z < e.B.Z ? e.A : e.B).ToList();
        var angles = new List<(Vec3 P, double A)>();
        foreach (var (p, to) in nodes)
        {
            if (to.Count != 2) continue;
            var (va, vb) = (to[0] - p, to[1] - p);
            angles.Add((p, Math.Acos(Math.Clamp(Vec3.Dot(va, vb) / (va.Length * vb.Length), -1, 1)) * 180 / Math.PI));
        }
        bool Inner(Vec3 p) => interior.Any(q => Same(q, p));
        var special = angles.Count(x => Math.Abs(x.A - 90) > 1 && Math.Abs(x.A - 180) > 1);
        var outer = angles.Count(x => !Inner(x.P) && Math.Abs(x.A - 90) <= 1);
        var inner = angles.Count(x => Inner(x.P) && Math.Abs(x.A - 90) <= 1);
        var straight = angles.Count(x => Math.Abs(x.A - 180) <= 1);
        var corners = angles.Count - straight;
        var ends = nodes.Count(n => n.To.Count == 1);
        return new Counts(pieces, hooks, outer, inner, special, eaves.Count, corners, straight, ends,
            [.. angles.Where(x => Math.Abs(x.A - 90) > 1 && Math.Abs(x.A - 180) > 1).Select(x => Math.Round(x.A, 1))],
            pieces - eaves.Count + 2 * corners + straight);
    }

    /// <summary>
    /// The roof's rainwater (estimate.make_estimate's "Pluvial" rows and their sizing): routed over the layout's faces and edges
    /// to the eaves the roof's gutters run along, sized as asked, counted, with what to check.
    /// </summary>
    public static Result Of(Data d, IReadOnlyList<RoofFace> faces, IReadOnlyList<RoofEdge> edges, Native native, IReadOnlyDictionary<string, double> stats, Takeoff.Options o)
    {
        var rain = o.Rain is > 0 ? o.Rain.Value : d.Rain;
        // The eaves with a gutter: each slope's guttered length; a slope all of whose eaves have one keeps its own eave length.
        var eaveEdges = edges.Where(e => e.Kind == EdgeKinds.Eave).ToList();
        var guttered = eaveEdges.Where(e => native.HasGutters && GutterOf(e, native) >= 0).ToList();
        var eaves = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var f in faces)
        {
            var all = eaveEdges.Where(e => e.Faces[0] == f.Id).ToList();
            var mine = guttered.Where(e => e.Faces[0] == f.Id).ToList();
            eaves[f.Id] = mine.Count == 0 ? 0 : mine.Count == all.Count ? f.Eave : mine.Sum(e => e.Length);
        }
        var (areas, missing, none) = Route(faces, edges, eaves);
        // The model's downpipes on each slope: by the guttered eave nearest to where each leaves its gutter.
        var model = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pipe in native.Pipes)
        {
            static double Dist(Vec2 p, Vec2 a, Vec2 b) { var ab = b - a; var t = Math.Clamp(Vec2.Dot(p - a, ab) / Math.Max(1e-12, Vec2.Dot(ab, ab)), 0, 1); return (a + ab * t - p).Length; }
            if (guttered.MinBy(e => Dist(pipe.At, e.A.Xy, e.B.Xy)) is { } near) model[near.Faces[0]] = model.GetValueOrDefault(near.Faces[0]) + 1;
        }
        var runs = faces.Where(f => areas.ContainsKey(f.Id)).Select(f => (f.Id, Math.Round(eaves[f.Id], 5), Math.Round(areas[f.Id], 5), model.GetValueOrDefault(f.Id))).ToList();
        var sizing = Size(d, runs, rain, o.System, o.Downpipes, missing, none);
        var system = d.Find(sizing.Selected)!;
        var counts = Count(d, guttered, edges);

        // A downpipe's average length: the model's (its automatic downpipes and its elements), else the default.
        var built = native.Pipes.Where(p => p.Length > 0).ToList();
        var average = built.Count > 0 ? built.Sum(p => p.Length) / built.Count : d.PipeDefault / 1000.0;
        var clamps = Math.Max(2, (int)Math.Ceiling(Math.Max(average - d.ClampFirst / 1000.0, 0) / (d.ClampSpacing / 1000.0)) + 1);
        var down = sizing.Downpipes;
        var sealant = stats.GetValueOrDefault("threeWay") + stats.GetValueOrDefault("valleyHeads") + stats.GetValueOrDefault("hipCaps") + counts.Special;

        var items = new List<Takeoff.Item>();
        string Fill(string s) => s.Replace("{g}", system.Gutter.ToString(CultureInfo.InvariantCulture)).Replace("{p}", system.Pipe.ToString(CultureInfo.InvariantCulture));
        foreach (var x in d.Items)
        {
            double? need = null;
            string? needUnit = null;
            double qty = x.Rule switch
            {
                "gutters" => counts.Pieces,
                "hooks" => counts.Hooks,
                "outerCorners" => counts.Outer,
                "innerCorners" => counts.Inner,
                "specialCorners" => counts.Special,
                "joints" => counts.Joints,
                "ends" => counts.Ends,
                "downpipes" => down * x.Per,
                "pipes" => Math.Ceiling(down * average / (d.PipeLength / 1000.0)),
                "clamps" => Math.Ceiling(clamps * down / (double)x.Pack),
                "sealant" => Math.Ceiling(sealant / x.Pack),
                _ => 0,
            };
            if (x.Rule == "gutters") { need = guttered.Sum(e => Math.Round(e.Length, 6)); needUnit = "m"; }
            if (x.Rule == "pipes") { need = down * average; needUnit = "m"; }
            if (x.Rule == "clamps" && x.Pack > 1) { need = clamps * down; needUnit = "pcs"; }
            if (qty > 0) items.Add(new Takeoff.Item(Fill(x.Code), Fill(x.Name), "rainwater", qty, x.Unit, need, needUnit, x.Note, x.MadeFrom));
        }

        var warnings = new List<string>();
        var inv = CultureInfo.InvariantCulture;
        if (!native.HasGutters) warnings.Add("The roof has no gutters: no outlet drains it and no gutter is counted.");
        if (sizing.BelowMinimum)
            warnings.Add(string.Create(inv, $"{sizing.Downpipes} downpipe(s) are fewer than the {sizing.Minimum[sizing.Selected]} the {sizing.Selected} system needs."));
        if (missing > 0.01 && native.HasGutters)
            warnings.Add(string.Create(inv, $"{missing:0.00} m² of roof ({string.Join(", ", none)}) drain to no gutter: size their outlets yourself."));
        if (native.HasGutters && Math.Abs(native.GutterWidth * 1000 - system.Gutter) > 0.5)
            warnings.Add(string.Create(inv, $"The roof's gutters are {native.GutterWidth * 1000:0} mm; the estimate counts the {system.Id} system."));
        if (native.HasGutters && native.Pipes.Count < down)
            warnings.Add(string.Create(inv, $"The model has {native.Pipes.Count} downpipe(s); the estimate counts {down}."));
        return new Result(sizing, counts, native, average, clamps, items, warnings, system);
    }

    /// <summary>The system a roof's drainage is sized for as it stands (the one asked, else the recommended), without laying its panels; null when it has no slopes.</summary>
    public static System? SystemFor(Entity roof, IModelView m, PanelProfile profile, Takeoff.Options o)
    {
        var (set, _) = RoofPanels.Planes(roof, m);
        if (set == null) return null;
        var (faces, edges) = RoofFaces.Read(set.Planes, profile.ValleyGap / 1000.0, set.Junctions, set.PlaneOf, set.Walls, set.Abutments, set.Lift, set.Holes);
        if (faces.Count == 0) return null;
        var d = PanelProfiles.Rain;
        return d.Find(Of(d, faces, edges, Read(roof, m), new Dictionary<string, double>(), o).Sizing.Selected);
    }

    static double R(double v, int d = 4) => Math.Round(v, d);

    /// <summary>The sizing as the interface reads it (the app's "Pluvial" sheet).</summary>
    public static JsonObject ToJson(Result r, Data d)
    {
        var s = r.Sizing;
        return new JsonObject
        {
            ["rain"] = s.Rain, ["defaultRain"] = d.Rain, ["roofArea"] = R(s.RoofArea), ["recommended"] = s.Recommended, ["selection"] = s.Selection, ["selected"] = s.Selected,
            ["systems"] = new JsonArray([.. d.Systems.Select(x => (JsonNode)new JsonObject { ["id"] = x.Id, ["gutter"] = x.Gutter, ["pipe"] = x.Pipe, ["capacity"] = x.Capacity })]),
            ["minimum"] = new JsonObject([.. s.Minimum.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))]),
            ["override"] = s.Override, ["downpipes"] = s.Downpipes, ["belowMinimum"] = s.BelowMinimum, ["differs"] = s.Differs,
            ["unrouted"] = R(s.Unrouted), ["noOutlet"] = new JsonArray([.. s.NoOutlet.Select(x => (JsonNode)x)]),
            ["runs"] = new JsonArray([.. s.Runs.Select(x => (JsonNode)new JsonObject
            {
                ["face"] = x.Face, ["eave"] = R(x.Eave, 5), ["area"] = R(x.Area, 5), ["flow"] = R(x.Flow), ["planned"] = x.Planned, ["model"] = x.Model,
                ["minimum"] = new JsonObject([.. x.Minimum.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))]),
            })]),
            ["model"] = new JsonObject
            {
                ["gutters"] = r.Native.HasGutters, ["gutterWidth"] = R(r.Native.GutterWidth * 1000, 1), ["pipeDiameter"] = R(r.Native.PipeDiameter * 1000, 1),
                ["downpipes"] = r.Native.Pipes.Count, ["mode"] = r.Native.Mode,
            },
            ["averagePipe"] = R(r.AveragePipe), ["clamps"] = r.Clamps,
            ["counts"] = new JsonObject
            {
                ["pieces"] = r.Counts.Pieces, ["hooks"] = r.Counts.Hooks, ["outer"] = r.Counts.Outer, ["inner"] = r.Counts.Inner, ["special"] = r.Counts.Special,
                ["eaves"] = r.Counts.Eaves, ["ends"] = r.Counts.Ends, ["joints"] = r.Counts.Joints, ["angles"] = new JsonArray([.. r.Counts.Angles.Select(a => (JsonNode)a)]),
            },
            ["items"] = new JsonArray([.. r.Items.Select(i => (JsonNode)new JsonObject
            {
                ["code"] = i.Code, ["name"] = i.Name, ["group"] = i.Group, ["quantity"] = i.Quantity, ["unit"] = i.Unit,
                ["need"] = i.Need is { } n ? R(n, 2) : null, ["needUnit"] = i.NeedUnit, ["note"] = i.Note, ["madeFrom"] = i.MadeFrom,
            })]),
            ["warnings"] = new JsonArray([.. r.Warnings.Select(w => (JsonNode)w)]),
            ["source"] = d.Source,
        };
    }
}
