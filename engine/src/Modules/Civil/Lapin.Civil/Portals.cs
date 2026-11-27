using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;

namespace Lapin.Civil;

/// <summary>
/// The tunnel-portal and bridge-abutment configurator (docs/plan-wing-walls.md, S3): a face wall across the road with a wing at
/// each end, laid out from a few numbers and built as a battered retaining wall (<see cref="BatteredWalls"/>), so its corners,
/// batters, warped wings, quantities and exact body are the wall's own. Presets give the usual shapes (straight wings in line with
/// the face, U wings parallel to the road, flared wings at an angle, warped wings, an abutment with its wings); every number may be
/// a formula (<c>{= embankment + 0.5}</c>) reading the others and the project's parameters. The configurator's numbers are kept on
/// the wall (<c>portal</c>), so it opens again on it and rebuilds it in place (same element, same GlobalIds).
/// <para>
/// The road runs along <c>direction</c> from <c>origin</c> (the face wall's middle, on the road's axis), the way the wings go. A
/// tunnel portal holds the hill behind its face and the cutting outside its wings; an abutment holds the embankment behind its face,
/// between its wings. Left and right are as seen from the face looking along the wings.
/// </para>
/// </summary>
public static class Portals
{
    public const string Key = "portal";
    public const string PreviewClass = "LapinPortal";

    public static readonly string[] Presets = ["straight", "u", "flared", "warped", "abutment"];

    /// <summary>A parameter of the configurator: its key, its label, its kind (length, number, angle, choice, bool), its group, when it applies, its default.</summary>
    public sealed record Param(string Key, string Label, string Kind, string Group, JsonNode? Default, double? Min = null, double? Max = null,
        string[]? Choices = null, Dictionary<string, string[]>? ShowIf = null, bool Formula = true);

    static Dictionary<string, string[]> When(string key, params string[] values) => new() { [key] = values };

    public static readonly Param[] Params =
    [
        new("kind", "Structure", "choice", "Layout", "portal", Choices: ["portal", "abutment"], Formula: false),
        new("faceLength", "Face wall length", "length", "Layout", 10.0, Min: 1, Max: 200),
        new("crown", "Crown elevation", "length", "Layout", 7.5),
        new("footingLevel", "Footing underside elevation", "length", "Layout", -1.5),
        new("wingLength", "Wing length", "length", "Wings", "{= (crown - embankment) * slope}", Min: 0.5, Max: 200),
        new("wingAngle", "Wing angle to the road (°)", "angle", "Wings", 30.0, Min: 0, Max: 90),
        new("endCrown", "Crown at the free end", "length", "Wings", "{= embankment + 0.5}"),
        new("embankment", "Embankment level at the free end", "length", "Wings", 1.0),
        new("slope", "Embankment slope 1:m", "number", "Wings", 1.5, Min: 0.2, Max: 10),
        new("mirror", "Right wing mirrors the left", "bool", "Wings", true, Formula: false),
        new("wingLengthRight", "Right wing length", "length", "Wings", "{= wingLength}", Min: 0.5, Max: 200, ShowIf: When("mirror", "false")),
        new("wingAngleRight", "Right wing angle (°)", "angle", "Wings", "{= wingAngle}", Min: 0, Max: 90, ShowIf: When("mirror", "false")),
        new("endCrownRight", "Right crown at the free end", "length", "Wings", "{= endCrown}", ShowIf: When("mirror", "false")),
        new("thickness", "Crown thickness", "length", "Section", 0.6, Min: 0.1, Max: 5),
        new("frontBatter", "Front batter n:1 (0: upright)", "number", "Section", 10.0, Min: -100, Max: 100),
        new("backBatter", "Back batter n:1 (0: upright)", "number", "Section", 0.0, Min: -100, Max: 100),
        new("wingFrontBatter", "Wing front batter n:1", "number", "Section", "{= frontBatter}", Min: -100, Max: 100),
        new("wingParallelBack", "Parallel wing back face", "bool", "Section", false, Formula: false),
        new("wingBackBatter", "Wing back batter n:1", "number", "Section", "{= backBatter}", Min: -100, Max: 100, ShowIf: When("wingParallelBack", "false")),
        new("footingThickness", "Footing thickness", "length", "Section", 1.0, Min: 0, Max: 5),
        new("toe", "Toe", "length", "Section", 0.6, Min: 0, Max: 10),
        new("heel", "Heel", "length", "Section", 1.2, Min: 0, Max: 10),
        new("warp", "Warped wings", "bool", "Warp", false, Formula: false),
        new("warpBatter", "Batter at the free end n:1", "number", "Warp", "{= 1 / slope}", Min: 0.2, Max: 100, ShowIf: When("warp", "true")),
        new("twist", "Twist along", "choice", "Warp", "slope", Choices: ["slope", "segment"], ShowIf: When("warp", "true"), Formula: false),
        new("endStyle", "Free end", "choice", "Free end", "square", Choices: ["square", "battered", "parallel"], Formula: false),
        new("endBatter", "End batter n:1", "number", "Free end", "{= 1 / slope}", Min: 0.2, Max: 100, ShowIf: When("endStyle", "battered")),
        new("endTurn", "End turned by (°; empty: parallel to the road)", "angle", "Free end", null, Min: -60, Max: 60, ShowIf: When("endStyle", "parallel")),
        new("ifc", "IFC", "choice", "Output", "assembly", Choices: ["assembly", "wall"], Formula: false),
        new("material", "Material", "text", "Output", "Reinforced concrete", Formula: false),
    ];

    /// <summary>What each preset sets over the defaults.</summary>
    public static JsonObject PresetOf(string preset) => preset switch
    {
        "straight" => new() { ["kind"] = "portal", ["wingAngle"] = 90.0, ["warp"] = false },
        "u" => new() { ["kind"] = "portal", ["wingAngle"] = 0.0, ["warp"] = false },
        "warped" => new() { ["kind"] = "portal", ["wingAngle"] = 30.0, ["warp"] = true },
        "abutment" => new() { ["kind"] = "abutment", ["wingAngle"] = 0.0, ["warp"] = false, ["faceLength"] = 12.0, ["crown"] = 6.0, ["thickness"] = 0.8, ["frontBatter"] = 0.0, ["backBatter"] = 0.0 },
        _ => new() { ["kind"] = "portal", ["wingAngle"] = 30.0, ["warp"] = false },
    };

    /// <summary>The configurator's catalogue: its parameters and presets (served to the interface).</summary>
    public static JsonObject Catalog() => new()
    {
        ["params"] = new JsonArray([.. Params.Select(p =>
        {
            var o = new JsonObject { ["key"] = p.Key, ["label"] = p.Label, ["kind"] = p.Kind, ["group"] = p.Group, ["default"] = p.Default?.DeepClone(), ["formula"] = p.Formula };
            if (p.Min != null) o["min"] = p.Min;
            if (p.Max != null) o["max"] = p.Max;
            if (p.Choices != null) o["choices"] = new JsonArray([.. p.Choices.Select(c => (JsonNode)c)]);
            if (p.ShowIf != null) o["showIf"] = new JsonObject(p.ShowIf.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)new JsonArray([.. kv.Value.Select(v => (JsonNode)v)]))));
            return (JsonNode)o;
        })]),
        ["presets"] = new JsonArray([.. Presets.Select(k => (JsonNode)new JsonObject { ["key"] = k, ["set"] = PresetOf(k) })]),
    };

    /// <summary>A spec whole: the defaults, under its preset's, under what it gives.</summary>
    public static JsonObject Full(JsonObject? spec)
    {
        var o = new JsonObject();
        foreach (var p in Params) o[p.Key] = p.Default?.DeepClone();
        var preset = (string?)spec?["preset"] is { } pr && Presets.Contains(pr) ? pr : "flared";
        foreach (var (k, v) in PresetOf(preset)) o[k] = v?.DeepClone();
        o["preset"] = preset;
        if (spec != null) foreach (var (k, v) in spec) if (k != "preset") o[k] = v?.DeepClone();
        return o;
    }

    /// <summary>Whether a value is a formula: <c>{= …}</c>, or a text starting with <c>=</c>.</summary>
    public static string? FormulaOf(JsonNode? n)
    {
        if (n is not JsonValue v || !v.TryGetValue<string>(out var s)) return null;
        var t = s.Trim();
        if (t.StartsWith("{=", StringComparison.Ordinal) && t.EndsWith('}')) return t[2..^1].Trim();
        if (t.StartsWith('=')) return t[1..].Trim();
        return null;
    }

    /// <summary>The configurator's numbers worked out: every number (or formula) of the spec as a value, and what did not read.</summary>
    public sealed record Values(IReadOnlyDictionary<string, double> Numbers, JsonObject Spec, IReadOnlyList<string> Problems)
    {
        public double this[string key] => Numbers.TryGetValue(key, out var v) ? v : double.NaN;
        public double? Maybe(string key) => Numbers.TryGetValue(key, out var v) && double.IsFinite(v) ? v : null;
        public string Text(string key, string fallback) => (string?)Spec[key] is { Length: > 0 } s ? s : fallback;
        public bool Flag(string key) => Spec[key] is JsonValue v && (v.TryGetValue<bool>(out var b) ? b : v.TryGetValue<string>(out var s) && s == "true");
    }

    /// <summary>
    /// The spec's numbers: plain ones as they are, formulas worked out (each reading the others by key, <c>height</c> the face wall's
    /// crown over its footing's underside, and last the project's parameters by key), a formula reading itself through others refused.
    /// </summary>
    public static Values Evaluate(JsonObject? given, IModelView? m) =>
        EvaluateWith(Params, Full(given), m, (key, get) => key.Equals("height", StringComparison.OrdinalIgnoreCase) ? get("crown") - get("footingLevel") : null);

    /// <summary>
    /// A configurator's numbers (its parameters <paramref name="ps"/>, its spec whole): plain ones as they are, formulas worked out (each
    /// reading the others by key, then <paramref name="extra"/>'s names, then the project's parameters by key), a formula reading itself
    /// through others refused. The tunnel lining's configurator (S4) works out its numbers the same way.
    /// </summary>
    public static Values EvaluateWith(Param[] ps, JsonObject spec, IModelView? m, Func<string, Func<string, double?>, double?>? extra = null)
    {
        var numbers = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        var busy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kinds = ps.ToDictionary(p => p.Key, p => p, StringComparer.OrdinalIgnoreCase);
        double? Get(string key)
        {
            if (numbers.TryGetValue(key, out var known)) return known;
            if (extra?.Invoke(key, Get) is { } named) return named;
            if (!kinds.TryGetValue(key, out var p) || p.Kind is "choice" or "bool" or "text") return null;
            var node = spec[p.Key];
            double? v;
            if (FormulaOf(node) is { } f)
            {
                if (!busy.Add(p.Key)) { problems.Add($"{p.Label}: its formula reads itself."); return null; }
                var (x, error) = Expr.TryEvalValue(f, n => Get(n) is { } y ? XValue.Of(y) : m != null ? ParamScopes.ProjectFormulaValue(m, n) : null);
                busy.Remove(p.Key);
                v = x?.Number;
                if (v == null) problems.Add($"{p.Label}: {error ?? "the formula gives no number."}");
            }
            else v = node == null ? null : Json.Number(node) ?? (node is JsonValue sv && sv.TryGetValue<string>(out var str) && double.TryParse(str.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null);
            if (v is { } val && double.IsFinite(val))
            {
                if (p.Min is { } lo && val < lo) { problems.Add($"{p.Label}: at least {lo.ToString(CultureInfo.InvariantCulture)}."); val = lo; }
                if (p.Max is { } hi && val > hi) { problems.Add($"{p.Label}: at most {hi.ToString(CultureInfo.InvariantCulture)}."); val = hi; }
                numbers[p.Key] = val;
                return val;
            }
            return null;
        }
        foreach (var p in ps) Get(p.Key);
        return new(numbers, spec, problems.Distinct().ToList());
    }

    /// <summary>The wall the configurator lays out: its path (right wing's free end, the face wall, the left wing's free end) and its numbers.</summary>
    public sealed record Layout(List<Vec2> Path, bool HighOnLeft, int FaceLeg, List<string> Names, List<string> Notes);

    public static Layout LayOut(Values v, Vec2 origin, Vec2 direction)
    {
        var notes = new List<string>();
        var abutment = v.Text("kind", "portal") == "abutment";
        if (v.Spec["path"] is JsonArray drawn && Args.StoredPoints(drawn) is { Count: >= 2 } given)
        {
            // A path drawn: its first and last segments the wings (when it has three or more), the rest the face wall.
            var clean = Chain.Clean(given, null).Path.ToList();
            var names = Enumerable.Range(0, clean.Count - 1).Select(i => clean.Count >= 4 && i == 0 ? "Right wing" : clean.Count >= 4 && i == clean.Count - 2 ? "Left wing" : "Face wall").ToList();
            return new(clean, !abutment, clean.Count >= 4 ? 1 : 0, names, notes);
        }
        var r = direction.Length < 1e-9 ? new Vec2(0, 1) : direction.Normalized();
        var a = r.Perp;
        var half = v["faceLength"] / 2;
        var (cr, cl) = (origin - a * half, origin + a * half);
        double Rad(double deg) => deg * Math.PI / 180;
        var mirror = v.Flag("mirror");
        var (alphaL, alphaR) = (Rad(v["wingAngle"]), Rad(mirror ? v["wingAngle"] : v.Maybe("wingAngleRight") ?? v["wingAngle"]));
        var (lenL, lenR) = (v["wingLength"], mirror ? v["wingLength"] : v.Maybe("wingLengthRight") ?? v["wingLength"]);
        var dl = r * Math.Cos(alphaL) + a * Math.Sin(alphaL);
        var dr = r * Math.Cos(alphaR) - a * Math.Sin(alphaR);
        var path = new List<Vec2> { cr + dr * lenR, cr, cl, cl + dl * lenL };
        return new(path, !abutment, 1, ["Right wing", "Face wall", "Left wing"], notes);
    }

    /// <summary>
    /// The wall's attributes from the configurator: its path, its section, each segment's crown, batters, warp and name, its free ends,
    /// its IFC mapping, and the spec itself (to open again on it).
    /// </summary>
    public static (JsonObject Attrs, string Predefined, List<string> Notes) Attrs(Values v, JsonObject raw, Vec2 origin, Vec2 direction, string? materialId, string? profileId)
    {
        var lay = LayOut(v, origin, direction);
        var notes = new List<string>(lay.Notes);
        var path = lay.Path;
        var n = path.Count - 1;
        var crown = v["crown"];
        var bottom = v["footingLevel"];
        var tf = v["footingThickness"];
        if (crown < bottom + tf + 0.3) notes.Add("The crown is less than 30 cm over the footing.");
        var mirror = v.Flag("mirror");
        var endL = v["endCrown"];
        var endR = mirror ? endL : v.Maybe("endCrownRight") ?? endL;
        var warp = v.Flag("warp");
        var fb = v["frontBatter"];
        var bb = v["backBatter"];
        var wfb = v.Maybe("wingFrontBatter") ?? fb;
        var wbb = v.Maybe("wingBackBatter") ?? bb;
        var wpb = v.Flag("wingParallelBack");
        var warpN = v.Maybe("warpBatter") ?? 1 / Math.Max(0.2, v["slope"]);
        var style = v.Text("endStyle", "square");
        var spec = new RetainingWalls.Spec(v["thickness"], 1.0, crown, 0.3, v["thickness"] + v["toe"] + v["heel"], tf, lay.HighOnLeft);
        var attrs = RetainingWalls.Attrs(path, spec, materialId);
        attrs["frontBatter"] = fb;
        attrs["backBatter"] = bb;
        attrs["toe"] = Math.Max(0, v["toe"]);
        attrs["heel"] = Math.Max(0, v["heel"]);
        var segs = new JsonArray();
        for (var i = 0; i < n; i++)
        {
            var o = new JsonObject { ["name"] = lay.Names[i], ["footingLevel"] = bottom };
            var wing = n >= 3 && (i == 0 || i == n - 1);
            // The wings run from the face wall's crown to their free end's; drawn towards the corner, the right wing starts at its free end.
            if (wing && i == 0) { o["topStart"] = Math.Min(endR, crown); o["topEnd"] = crown; }
            else if (wing) { o["topStart"] = crown; o["topEnd"] = Math.Min(endL, crown); }
            else { o["topStart"] = crown; o["topEnd"] = crown; }
            if (wing && warp)
            {
                o["warp"] = true;
                // The face goes from the wing's batter at the corner to the embankment's at the free end.
                if (i == 0) { o["frontBatter"] = warpN; o["frontBatterEnd"] = wfb; }
                else { o["frontBatter"] = wfb; o["frontBatterEnd"] = warpN; }
                if (v.Text("twist", "slope") == "segment") o["twist"] = "segment";
            }
            else if (wing && Math.Abs(wfb - fb) > 1e-12) o["frontBatter"] = wfb;
            // The wings' back: parallel to their front (constant thickness), or their own batter where it differs from the face wall's.
            if (wing && wpb) o["parallelBack"] = true;
            else if (wing && Math.Abs(wbb - bb) > 1e-12) o["backBatter"] = wbb;
            if (i == 0 || i == n - 1)
            {
                var d = (path[i + 1] - path[i]).Normalized();
                var h = lay.HighOnLeft ? d.Perp : -d.Perp;
                switch (style)
                {
                    case "battered":
                        o["endBatter"] = v.Maybe("endBatter") ?? 1 / Math.Max(0.2, v["slope"]);
                        break;
                    case "parallel":
                        if (v.Maybe("endTurn") is { } given) o["endTurn"] = Math.Clamp(given, -60, 60);
                        else if (EndTurnParallel(path, i, n, d, h, direction) is { } t) o["endTurn"] = t;
                        else notes.Add($"{lay.Names[i]}: its end is square to it, already parallel to the road, or would turn more than 60°.");
                        break;
                }
            }
            segs.Add((JsonNode)o);
        }
        attrs["segments"] = segs;
        attrs["portal"] = raw.DeepClone();
        if (profileId != null) attrs["profileId"] = profileId;
        // The IFC mapping: an assembly of the wings and the face wall (an abutment in a bridge's part in IFC 4.3), or one retaining wall.
        var abutment = v.Text("kind", "portal") == "abutment";
        var predefined = "RETAININGWALL";
        if (v.Text("ifc", "assembly") == "assembly")
        {
            attrs["ifcMapping"] = "assembly";
            attrs["exportAs"] = "IfcElementAssembly";
            attrs["objectType"] = abutment ? "Abutment" : "Tunnel portal";
            predefined = abutment ? "ABUTMENT" : "USERDEFINED";
            if (abutment) attrs["ifcBridgePart"] = "ABUTMENT";
        }
        return (attrs, predefined, notes);
    }

    /// <summary>The turn (degrees) of a free end that makes it parallel to the road; null when the wing is (nearly) parallel to the road itself or it would turn past 60°.</summary>
    static double? EndTurnParallel(List<Vec2> path, int i, int n, Vec2 d, Vec2 h, Vec2 road)
    {
        var r = road.Length < 1e-9 ? new Vec2(0, 1) : road.Normalized();
        var sgn = i == n - 1 ? 1.0 : -1.0;
        // The end's plane leans by t: its normal s·D − t·H square to the road.
        var hr = Vec2.Dot(h, r);
        if (Math.Abs(hr) < 1e-6) return null;
        var t = sgn * Vec2.Dot(d, r) / hr;
        var deg = Math.Atan(t) * 180 / Math.PI;
        return Math.Abs(deg) > 60 + 1e-9 ? null : Math.Abs(deg) < 1e-9 ? null : deg;
    }

    /// <summary>The placement a command gives: an origin and a direction, two points, or a point on an axis picked (a road, a path, a wall).</summary>
    public static (Vec2 Origin, Vec2 Direction) Placement(IModelView m, JsonObject a, JsonObject? stored)
    {
        static Vec2? P(JsonNode? n) => n is JsonArray x && x.Count >= 2 && Json.Number(x[0]) is { } px && Json.Number(x[1]) is { } py ? new Vec2(px, py) : null;
        if (a["axis"] is JsonObject axis && (string?)axis["id"] is { } aid && m.Get(aid) is { } ax && P(axis["at"]) is { } at)
        {
            var chain = Corridors.Is(ax) ? Corridors.ChainOf(ax) : new Chain(Terrains.Contour(ax, "path") is { Count: >= 2 } pp ? pp : Args.StoredPoints(ax.Attrs["points"]));
            if (chain.Segments == 0) throw new CommandException("The axis picked has no line to follow.");
            var (station, _) = chain.Project(at);
            var dir = chain.DirectionAt(station);
            if (axis["reverse"] is JsonValue rv && rv.TryGetValue<bool>(out var rev) && rev) dir = -dir;
            return (chain.PointAt(station), dir);
        }
        var origin = P(a["origin"]) ?? P(stored?["origin"]) ?? new Vec2(0, 0);
        var direction = P(a["towards"]) is { } towards ? towards - origin : P(a["direction"]) ?? P(stored?["direction"]) ?? new Vec2(0, 1);
        if (direction.Length < 1e-6) throw new CommandException("The road's direction is not given: the second point is on the first.");
        return (origin, direction.Normalized());
    }

    public static void Register(IModuleRegistry r)
    {
        r.Catalog("portal", Catalog());

        // A tunnel portal or a bridge abutment, laid out by the configurator: one undo step, a retaining wall whose spec it keeps.
        r.Command("addPortal", "Portal or abutment", (ctx, a) =>
        {
            var raw = (a["spec"] as JsonObject)?.DeepClone() as JsonObject ?? [];
            var (origin, direction) = Placement(ctx.Model, a, null);
            var e = Add(ctx, raw, origin, direction, (string?)a["name"], SiteCommandsStorey(ctx.Model, a));
            return new JsonObject { ["id"] = e.Id };
        });

        // The configurator again on a wall it made: the same element rebuilt (its GlobalIds kept), its placement kept unless a new one is given.
        r.Command("updatePortal", "Change portal or abutment", (ctx, a) =>
        {
            var e = Spatial.Require(ctx.Model, (string?)a["id"], RetainingWalls.Class);
            var stored = e.Attrs[Key] as JsonObject;
            var raw = (a["spec"] as JsonObject)?.DeepClone() as JsonObject ?? (stored?.DeepClone() as JsonObject) ?? [];
            var (origin, direction) = Placement(ctx.Model, a, stored);
            raw["origin"] = new JsonArray(origin.X, origin.Y);
            raw["direction"] = new JsonArray(direction.X, direction.Y);
            var v = Evaluate(raw, ctx.Model);
            var material = Materials.Ensure(ctx, v.Text("material", "Reinforced concrete"), RetainingWalls.Color);
            var (attrs, predefined, _) = Attrs(v, raw, origin, direction, material.Id, ProfileOf(ctx.Model, raw));
            // What the configurator does not set stays (the corridor's joints, its name, its own parameters).
            foreach (var (k, val) in e.Attrs) if (!attrs.ContainsKey(k) && !ConfiguratorKeys.Contains(k)) attrs[k] = val?.DeepClone();
            ctx.Replace(e.With(attrs: attrs, predefinedType: predefined));
            return null;
        });

        // The configurator's numbers worked out (formulas and all), the path it would lay, and what does not read.
        r.Query("civil.portalValues", (q, a) =>
        {
            var raw = a["spec"] as JsonObject ?? [];
            var v = Evaluate(raw, q.Model);
            var (origin, direction) = Placement(q.Model, a, raw);
            var lay = LayOut(v, origin, direction);
            var (_, _, notes) = Attrs(v, raw, origin, direction, null, null);
            return new JsonObject
            {
                ["values"] = new JsonObject(v.Numbers.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)Math.Round(kv.Value, 6)))),
                ["problems"] = new JsonArray([.. v.Problems.Concat(notes).Select(p => (JsonNode)p)]),
                ["path"] = new JsonArray([.. lay.Path.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 6), Math.Round(p.Y, 6)))]),
            };
        });
        r.Service<ITypePreview>(new PortalPreview());
    }

    /// <summary>
    /// A portal or an abutment laid out by the configurator at <paramref name="origin"/>, its wings along <paramref name="direction"/>:
    /// a retaining wall that keeps its spec, added on the storey given. <paramref name="more"/> sets attributes of its own beside the
    /// configurator's (a tunnel lining's opening through its face wall, S4).
    /// </summary>
    public static Entity Add(ICommandContext ctx, JsonObject raw, Vec2 origin, Vec2 direction, string? name, string? storeyId, JsonObject? more = null)
    {
        raw.Remove("origin");
        raw.Remove("direction");
        raw["origin"] = new JsonArray(origin.X, origin.Y);
        raw["direction"] = new JsonArray(direction.X, direction.Y);
        var v = Evaluate(raw, ctx.Model);
        var material = Materials.Ensure(ctx, v.Text("material", "Reinforced concrete"), RetainingWalls.Color);
        var (attrs, predefined, _) = Attrs(v, raw, origin, direction, material.Id, ProfileOf(ctx.Model, raw));
        if (more != null) foreach (var (k, val) in more) attrs[k] = val?.DeepClone();
        var kind = v.Text("kind", "portal") == "abutment" ? "Abutment" : "Tunnel portal";
        var count = ctx.Model.OfClass(RetainingWalls.Class).Count(x => x.Attrs[Key] is JsonObject) + 1;
        var e = new Entity(ctx.NewId(), RetainingWalls.Class, name?.Trim() is { Length: > 0 } nm ? nm : $"{kind} {count}", attrs, predefined);
        ctx.Add(e);
        if (storeyId != null) Spatial.Contain(ctx, storeyId, e.Id);
        return e;
    }

    /// <summary>The keys the configurator owns: those it does not set again on a rebuild are dropped (a profile taken off, an assembly made a wall).</summary>
    static readonly HashSet<string> ConfiguratorKeys = ["profileId", "ifcMapping", "objectType", "ifcBridgePart", "segments", "baseThickness", "keyWidth", "keyDepth", "keyAt", "topElevation", "height"];

    /// <summary>The drawn profile the stem takes for its section (a project's profile copy of an outline), when the spec names one.</summary>
    static string? ProfileOf(IModelView m, JsonObject raw) =>
        (string?)raw["profileId"] is { Length: > 0 } id && m.Get(id) is { Class: ProjectProfiles.Class } p && ProjectProfiles.Read(p) is { Rings.Count: > 0 } ? id : null;

    static string? SiteCommandsStorey(IModelView m, JsonObject a) => (string?)a["storeyId"] ?? Spatial.Storeys(m).FirstOrDefault()?.Id;
}

/// <summary>The configurator's preview (decision 529's): the portal laid out in a scratch project by its own command, cut across its left wing.</summary>
sealed class PortalPreview : ITypePreview
{
    public bool Handles(string typeClass) => typeClass == Portals.PreviewClass;

    public TypePreviewSample Build(TypePreviewScratch s, JsonObject type, JsonObject sample)
    {
        var spec = (JsonObject)type.DeepClone();
        // Laid out about the origin, the road along +y, whatever placement it has in the project (a drawn path is moved there too).
        var origin = new Vec2(0, 0);
        var direction = new Vec2(0, 1);
        if (spec["path"] is JsonArray drawn && Args.StoredPoints(drawn) is { Count: >= 2 } pts)
        {
            var c = pts.Aggregate(new Vec2(0, 0), (a, p) => a + p) * (1.0 / pts.Count);
            spec["path"] = new JsonArray([.. pts.Select(p => (JsonNode)new JsonArray(p.X - c.X, p.Y - c.Y))]);
        }
        spec.Remove("origin");
        spec.Remove("direction");
        var r = s.Run("civil.addPortal", new JsonObject { ["spec"] = spec, ["origin"] = new JsonArray(origin.X, origin.Y), ["direction"] = new JsonArray(direction.X, direction.Y), ["storeyId"] = s.Storey });
        var id = (string?)r?["id"] ?? throw new CommandException("The portal could not be laid out.");
        var path = RetainingWalls.Path(s.Model.Get(id)!);
        // The section across the last segment (the left wing) at its middle, looking back along it.
        var (a0, b0) = (path[^2], path[^1]);
        var mid = (a0 + b0) * 0.5;
        var d = (b0 - a0).Normalized();
        var across = d.Perp;
        var reach = 4 + Portals.Evaluate(spec, s.Model)["toe"] + Portals.Evaluate(spec, s.Model)["heel"];
        return new TypePreviewSample([id], mid - across * reach, mid + across * reach, 1.0);
    }
}
