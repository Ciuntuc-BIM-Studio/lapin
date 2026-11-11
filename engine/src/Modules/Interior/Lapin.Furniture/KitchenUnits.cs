using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Furniture;

/// <summary>A kitchen unit as laid (decision 472): its line (base or wall), its place from the left, its kind, size and what its front is; <see cref="At"/> its middle across the piece as seen from its front.</summary>
public sealed record KitchenUnitRow(string Line, int Index, string Type, string Kind, double Width, double Height, string Detail, double At);

/// <summary>A field of a unit, as the kitchen configurator edits it; <see cref="When"/>: shown only while another field has one of these values.</summary>
public sealed record UnitField(string Key, string Label, string Kind, double? Min = null, double? Max = null, string[]? Choices = null, (string Key, string[] Values)? When = null);

/// <summary>A kind of kitchen unit: on the base line or the wall line, the widths it takes (a range, or the sizes appliances come in), its fields and the values a new one starts from.</summary>
public sealed record UnitType(string Key, string Label, string Line, double Min, double Max, double[]? Sizes, UnitField[] Fields, JsonObject Defaults, bool Tall = false);

/// <summary>
/// A kitchen's own units (a tester's report #76, decision 472): the main run as a sequence of base units, tall units,
/// appliances and fillers (<c>units</c> in the type's spec, from the left as seen from the front), and the wall line
/// above it (<c>upper</c>: wall units, hoods and gaps). Cabinets take any width from 100 to 800 mm; appliances the
/// sizes they come in (a dishwasher 450 or 600, a hob 600, 800 or 900, a cooker 500, 600 or 900). Whatever of the run
/// the units leave is split evenly as before; a type without units is built as kitchens were before.
/// </summary>
public static class KitchenUnits
{
    public const string BaseKey = "units", UpperKey = "upper";

    /// <summary>The widths the configurator offers first (any other in the range is typed).</summary>
    public static readonly double[] Steps = [0.15, 0.2, 0.3, 0.4, 0.45, 0.5, 0.6, 0.8];

    static UnitField L(string k, string l, double min, double max, (string, string[])? when = null) => new(k, l, "length", min, max, When: when);
    static UnitField N(string k, string l, double min, double max, (string, string[])? when = null) => new(k, l, "number", min, max, When: when);
    static UnitField C(string k, string l, string[] c, (string, string[])? when = null) => new(k, l, "choice", Choices: c, When: when);
    static UnitField B(string k, string l, (string, string[])? when = null) => new(k, l, "bool", When: when);
    static JsonObject D(params (string Key, JsonNode? Value)[] v) { var o = new JsonObject(); foreach (var (k, x) in v) o[k] = x; return o; }

    static readonly string[] Hands = ["left", "right"];
    static UnitField Doors((string, string[])? when = null) => N("doors", "Doors", 1, 2, when);
    static UnitField Hand((string, string[])? when = null) => C("hand", "Hinged on the", Hands, when);

    public static readonly IReadOnlyList<UnitType> Types =
    [
        new("base", "Base unit", "base", 0.1, 0.8, null,
        [
            C("front", "Front", ["drawerDoor", "doors", "drawers", "pan", "open"]),
            Doors(("front", ["doors"])), Hand(("front", ["doors"])), N("drawers", "Drawers", 1, 4, ("front", ["drawers"])),
        ], D(("width", 0.6), ("front", "drawerDoor"))),
        new("sink", "Sink unit", "base", 0.1, 0.8, null,
            [C("sink", "Sink", ["single", "one-half", "double", "corner"]), C("tap", "Tap", ["standard", "pull-out", "none"]), Doors(), Hand()],
            D(("width", 0.6), ("sink", "single"), ("tap", "standard"))),
        new("hob", "Hob over an oven", "base", 0.6, 0.9, [0.6, 0.8, 0.9],
            [C("fuel", "Hob", ["induction", "gas"]), B("oven", "Oven under it")],
            D(("width", 0.6), ("fuel", "induction"), ("oven", true))),
        new("cooker", "Freestanding cooker", "base", 0.5, 0.9, [0.5, 0.6, 0.9],
            [C("fuel", "Hob", ["induction", "gas"])],
            D(("width", 0.6), ("fuel", "induction"))),
        new("dishwasher", "Dishwasher", "base", 0.45, 0.6, [0.45, 0.6], [], D(("width", 0.6))),
        new("fridge", "Fridge", "base", 0.45, 0.95, [0.5, 0.6, 0.7, 0.9],
            [C("fridge", "Fridge", ["under-counter", "tall", "freestanding"]), Hand(), L("height", "Height", 0.8, 2.7, ("fridge", ["tall", "freestanding"]))],
            D(("width", 0.6), ("fridge", "under-counter")), Tall: true),
        new("oven", "Oven tall unit", "base", 0.45, 0.8, null, [L("height", "Height", 1.4, 2.7)], D(("width", 0.6)), Tall: true),
        new("tall", "Tall unit", "base", 0.1, 0.8, null,
            [C("front", "Front", ["doors", "open"]), Doors(("front", ["doors"])), Hand(("front", ["doors"])), L("height", "Height", 1.4, 2.7)],
            D(("width", 0.6), ("front", "doors")), Tall: true),
        new("filler", "Filler", "base", 0.01, 0.3, null, [], D(("width", 0.05))),
        new("gap", "Gap", "base", 0.01, 2.4, null, [], D(("width", 0.6))),
        new("wall", "Wall unit", "upper", 0.1, 0.8, null,
        [
            C("front", "Front", ["doors", "glass", "open"]), Doors(("front", ["doors", "glass"])), Hand(("front", ["doors", "glass"])),
            L("height", "Height", 0.2, 1.4), L("from", "From the floor", 0.9, 2.4),
        ], D(("width", 0.6), ("front", "doors"))),
        new("hood", "Hood", "upper", 0.5, 1.2, [0.6, 0.9], [C("hood", "Hood", ["chimney", "integrated"]), L("from", "From the floor", 0.9, 2.4)], D(("width", 0.6), ("hood", "chimney"))),
        new("space", "Gap", "upper", 0.01, 2.4, null, [], D(("width", 0.6))),
    ];

    public static UnitType? Find(string? key, bool upper) => Types.FirstOrDefault(t => t.Key == key && (t.Line == "upper") == upper);

    /// <summary>Whether the kitchen lays its main run from its own units.</summary>
    public static bool Has(Spec s) => s.List(BaseKey) is { Count: > 0 } || s.List(UpperKey) is { Count: > 0 };

    /// <summary>A run the configurator starts from: a sink, a dishwasher, a hob over an oven and a unit of drawers (2.4 m, with the tall unit at its end 3 m).</summary>
    // new JsonArray(…), not a collection expression: that one calls JsonArray.Add<T>, which trimming refuses (the browser's build).
    public static JsonArray Typical() => new(
        D(("type", "sink"), ("width", 0.6), ("sink", "single"), ("tap", "standard")),
        D(("type", "dishwasher"), ("width", 0.6)),
        D(("type", "base"), ("width", 0.6), ("front", "drawers"), ("drawers", 3)),
        D(("type", "hob"), ("width", 0.6), ("fuel", "induction"), ("oven", true)));

    static string Mm(double v) => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A list of units checked and cleaned: each of a known type for its line, its width in its range (an appliance's
    /// one of its sizes), its fields among their choices and within their ranges, nothing it does not have.
    /// </summary>
    public static JsonArray Normalise(JsonNode? given, bool upper)
    {
        var o = new JsonArray();
        if (given == null) return o;
        if (given is not JsonArray list) throw new CommandException("A kitchen's units are a list.");
        foreach (var item in list)
        {
            if (item is not JsonObject u) throw new CommandException("A kitchen unit is an object with its type and width.");
            var t = Find((string?)u["type"], upper)
                ?? throw new CommandException($"A {(upper ? "wall line's" : "base line's")} unit is one of {string.Join(", ", Types.Where(x => (x.Line == "upper") == upper).Select(x => x.Key))}.");
            var w = Json.Number(u["width"]) ?? Json.Number(t.Defaults["width"])!.Value;
            if (t.Sizes is { } sizes && t.Key is "dishwasher" or "hob" or "cooker")
            {
                if (!sizes.Any(x => Math.Abs(x - w) < 1e-6)) throw new CommandException($"A {t.Label.ToLowerInvariant()} is {string.Join(", ", sizes.Take(sizes.Length - 1).Select(Mm))} or {Mm(sizes[^1])} mm wide.");
            }
            else if (w < t.Min - 1e-9 || w > t.Max + 1e-9)
                throw new CommandException(t.Key is "base" or "sink" or "tall" or "wall" ? "A cabinet is 100 to 800 mm wide." : $"A {t.Label.ToLowerInvariant()} is {Mm(t.Min)} to {Mm(t.Max)} mm wide.");
            var clean = new JsonObject { ["type"] = t.Key, ["width"] = Math.Round(w, 4) };
            foreach (var (key, v) in u)
            {
                if (key is "type" or "width" || v == null) continue;
                var f = t.Fields.FirstOrDefault(x => x.Key == key) ?? throw new CommandException($"A {t.Label.ToLowerInvariant()} has no „{key}”.");
                switch (f.Kind)
                {
                    case "length" or "number":
                        var n = Json.Number(v) ?? throw new CommandException($"{f.Label} is a number.");
                        if (f.Min is { } lo && n < lo - 1e-9 || f.Max is { } hi && n > hi + 1e-9)
                            throw new CommandException(f.Kind == "length"
                                ? $"{t.Label}: {f.Label.ToLowerInvariant()} is between {Mm(f.Min ?? 0)} and {Mm(f.Max ?? 0)} mm."
                                : $"{t.Label}: {f.Label.ToLowerInvariant()} is between {f.Min} and {f.Max}.");
                        clean[key] = f.Kind == "number" ? Math.Round(n) : Math.Round(n, 4);
                        break;
                    case "choice":
                        var c = (string?)v;
                        clean[key] = c != null && f.Choices!.Contains(c) ? c : throw new CommandException($"{t.Label}, {f.Label.ToLowerInvariant()}: one of {string.Join(", ", f.Choices!)}.");
                        break;
                    case "bool":
                        clean[key] = v is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : throw new CommandException($"{f.Label} is on or off.");
                        break;
                }
            }
            if (t.Key == "sink")
            {
                var style = (string?)clean["sink"] ?? "single";
                var min = style switch { "one-half" => 0.6, "double" => 0.8, _ => 0.4 };
                if (w < min - 1e-9) throw new CommandException($"A {Label(style).ToLowerInvariant()} needs a unit of at least {Mm(min)} mm.");
            }
            o.Add((JsonNode)clean);
        }
        return o;
    }

    /// <summary>The configurator's catalog: the unit types with their fields, the widths offered first, a typical run.</summary>
    public static JsonObject Catalog() => new()
    {
        ["steps"] = new JsonArray([.. Steps.Select(x => (JsonNode)x)]),
        ["typical"] = Typical(),
        ["types"] = new JsonArray([.. Types.Select(t => (JsonNode)new JsonObject
        {
            ["key"] = t.Key, ["label"] = t.Label, ["line"] = t.Line, ["min"] = t.Min, ["max"] = t.Max, ["tall"] = t.Tall,
            ["sizes"] = t.Sizes is { } z ? new JsonArray([.. z.Select(x => (JsonNode)x)]) : null,
            ["defaults"] = t.Defaults.DeepClone(),
            ["fields"] = new JsonArray([.. t.Fields.Select(f => (JsonNode)new JsonObject
            {
                ["key"] = f.Key, ["label"] = f.Label, ["kind"] = f.Kind, ["min"] = f.Min, ["max"] = f.Max,
                ["choices"] = f.Choices is { } c ? new JsonArray([.. c.Select(x => (JsonNode)x)]) : null,
                ["when"] = f.When is { } w ? new JsonObject { ["key"] = w.Key, ["values"] = new JsonArray([.. w.Values.Select(x => (JsonNode)x)]) } : null,
            })]),
        })]),
    };

    /// <summary>A choice's words, for a unit's detail in the schedule.</summary>
    public static string Label(string key) => key switch
    {
        "drawerDoor" => "Drawer over a door", "doors" => "Doors", "drawers" => "Drawers", "pan" => "Pan drawers", "open" => "Open shelves", "glass" => "Glazed doors",
        "single" => "Single sink", "one-half" => "1½ bowl sink", "double" => "Double sink", "corner" => "Corner sink",
        "standard" => "standard tap", "pull-out" => "pull-out tap", "none" => "no tap",
        "induction" => "Induction", "gas" => "Gas",
        "under-counter" => "Under-counter", "tall" => "Tall integrated", "freestanding" => "Freestanding",
        "chimney" => "Chimney hood", "integrated" => "Integrated hood",
        _ => key,
    };

    /// <summary>The length a kitchen's main run needs for its units: theirs, its blind corners and the tall unit at its end.</summary>
    public static double RequiredLength(Spec s)
    {
        var units = s.List(BaseKey)?.OfType<JsonObject>().Sum(u => Json.Number(u["width"]) ?? 0.6) ?? 0;
        var layout = KitchenLayouts.Of(s);
        var tall = s.Str("tall", "none");
        if (KitchenLayouts.Handed(s)) tall = tall switch { "left" => "right", "right" => "left", _ => tall };
        double extra = layout switch
        {
            "l-shaped" or "peninsula" => s.D + (tall == "right" ? 0.6 : 0),
            "u-shaped" => 2 * s.D,
            _ => tall is "left" or "right" ? 0.6 : 0,
        };
        return Math.Round(Math.Max(0.6, units + extra), 4);
    }
}

public static partial class KitchenLayouts
{
    /// <summary>A unit read from the spec, its fields under its type's defaults.</summary>
    sealed record Unit(UnitType T, JsonObject O)
    {
        public double W => Json.Number(O["width"]) ?? Json.Number(T.Defaults["width"]) ?? 0.6;
        public string Str(string k) => (string?)O[k] ?? (string?)T.Defaults[k] ?? "";
        public double? Num(string k) => Json.Number(O[k]) ?? Json.Number(T.Defaults[k]);
        public bool Bool(string k) => O[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : T.Defaults[k] is JsonValue d && d.TryGetValue<bool>(out var db) && db;
        public int Doors => (int)(Num("doors") ?? (W > 0.6 + 1e-6 ? 2 : 1));
        public bool IsTall => T.Key is "tall" or "oven" || T.Key == "fridge" && Str("fridge") is "tall" or "freestanding";
    }

    static List<Unit> Units(Spec s, bool upper, bool handed)
    {
        var o = new List<Unit>();
        foreach (var n in s.List(upper ? KitchenUnits.UpperKey : KitchenUnits.BaseKey) ?? [])
            if (n is JsonObject u && KitchenUnits.Find((string?)u["type"], upper) is { } t)
            {
                var obj = (JsonObject)u.DeepClone();
                // Built mirrored, a door hinged on the left is hinged on its right as built.
                if (handed && obj["hand"] is JsonValue h) obj["hand"] = (string?)h == "right" ? "left" : "right";
                else if (handed && t.Fields.Any(f => f.Key == "hand")) obj["hand"] = "right";
                o.Add(new Unit(t, obj));
            }
        // Listed from the left as seen from the front: a mirrored build lays them from its other end.
        if (handed) o.Reverse();
        return o;
    }

    const string Sym = "symbol";

    /// <summary>
    /// The main run from the type's own units (decision 472): each laid from the free span's start (after a blind
    /// corner), the rest split evenly as before, the worktop over all but tall units and freestanding appliances, the
    /// wall line above (its own units, else a wall unit over each base unit and a hood over a hob).
    /// </summary>
    static void BuildUnits(Builder b, Frame f, Spec s, Run run, double d, double a, double e, double fa, double fe, (double, double)? tallSpan)
    {
        double top = s.H, wt = s.Num("worktop", 0.04), pl = s.Num("plinth", 0.1), unitW = s.Num("unit", 0.6);
        string pr = PlinthRole(s), hr = HandleRole(s);
        // Listed from the left as seen in the type: an L cornered on the right is built as the left one mirrored, so its units are laid from the other end.
        // (A piece's own Flip mirrors it whole, its units with it.)
        var handed = Handed(s);
        var laid = new List<(Unit U, double U0, double U1, bool Auto)>();
        var pos = fa;
        foreach (var u in Units(s, false, handed))
        {
            if (pos + u.W > fe + 1e-6)
            {
                b.Warnings.Add($"The kitchen's units need {KitchenUnits.RequiredLength(s) * 1000:0} mm; those past its run's end are left out: lengthen it (Fit to units) or take some out.");
                break;
            }
            laid.Add((u, pos, pos + u.W, false));
            pos += u.W;
        }
        var rest = fe - pos;
        // The rest of the run split evenly, as a kitchen without units is; less than 100 mm is a filler.
        var auto = KitchenUnits.Find("base", false)!;
        if (rest >= 0.1 - 1e-6)
        {
            var n = Math.Max(1, (int)Math.Round(rest / unitW));
            for (var i = 0; i < n; i++) laid.Add((new Unit(auto, new JsonObject { ["type"] = "base", ["width"] = rest / n }), pos + i * rest / n, pos + (i + 1) * rest / n, true));
        }
        else if (rest > 1e-4) laid.Add((new Unit(KitchenUnits.Find("filler", false)!, new JsonObject { ["type"] = "filler", ["width"] = rest }), pos, fe, true));

        // The blind corners: a carcass with no front, under the worktop.
        foreach (var (c0, c1) in new[] { (a, fa), (fe, e) })
            if (c1 - c0 > 1e-4) { f.Block(b, pr, c0, c1, 0.07, d, 0, pl); f.Block(b, "body", c0, c1, 0.04, d, pl, top - wt); }

        var covered = new List<(double, double)>();
        if (fa - a > 1e-4) covered.Add((a, fa));
        var cornerFirst = run.CapStart > 1e-4;
        for (var i = 0; i < laid.Count; i++)
        {
            var (u, u0, u1, _) = laid[i];
            var under = BaseUnit(b, f, s, u, u0, u1, d, top, wt, pl, pr, hr, cornerFirst && i == 0 ? a : (double?)null);
            if (under) covered.Add((u0, u1));
            if (i > 0) f.Line(b, Sym, u0, 0, u0, 0.04);
        }
        if (e - fe > 1e-4) covered.Add((fe, e));
        // The worktop over each stretch of units it covers.
        var depthTop = Math.Max(d, run.TopDepth);
        foreach (var (w0, w1) in Merge(covered))
        {
            f.Block(b, "top", w0, w1, 0, depthTop, top - wt, top, 0.003);
            f.Rect(b, Sym, w0, w1, 0, depthTop);
        }
        // Rows for the schedule, from the left as seen.
        var rows = laid.Select(x => (x.U, x.U0, x.U1)).ToList();
        if (tallSpan is { } ts) rows.Add((new Unit(KitchenUnits.Find("tall", false)!, new JsonObject { ["type"] = "tall", ["width"] = Tall }), ts.Item1, ts.Item2));
        Rows(b, f, s, "Base", rows, top);

        if (!run.WallUnits) return;
        double wf = s.Num("wallFrom", 1.45), wh = s.Num("wallHeight", 0.72), wd = Math.Min(d, s.Num("wallDepth", 0.35));
        var talls = laid.Where(x => x.U.IsTall).Select(x => (x.U0, x.U1)).ToList();
        var upper = Units(s, true, handed);
        var hung = new List<(Unit U, double U0, double U1)>();
        if (upper.Count > 0)
        {
            // From the free span's start, each slid past a tall unit it would stand in.
            var at = fa;
            foreach (var u in upper)
            {
                foreach (var (t0, t1) in talls) if (at < t1 - 1e-6 && at + u.W > t0 + 1e-6) at = t1;
                if (at + u.W > fe + 1e-6) { b.Warnings.Add("The kitchen's wall units past its run's end are left out."); break; }
                hung.Add((u, at, at + u.W));
                at += u.W;
            }
        }
        else
        {
            // A wall unit over each base unit, a chimney hood over a hob or a cooker.
            var wall = KitchenUnits.Find("wall", true)!;
            var hood = KitchenUnits.Find("hood", true)!;
            foreach (var (u, u0, u1, _) in laid)
                if (!u.IsTall && u.T.Key != "filler")
                    hung.Add((u.T.Key is "hob" or "cooker" ? new Unit(hood, new JsonObject { ["type"] = "hood", ["width"] = u1 - u0 }) : new Unit(wall, new JsonObject { ["type"] = "wall", ["width"] = u1 - u0 }), u0, u1));
        }
        // The wall line's blind corners: a carcass with no front.
        foreach (var (c0, c1) in new[] { (a, fa), (fe, e) })
            if (c1 - c0 > 1e-4) { f.Block(b, "body", c0, c1, d - wd + 0.018, d, wf, wf + wh); f.Rect(b, "overhead", c0, c1, d - wd, d); }
        foreach (var (u, u0, u1) in hung) UpperUnit(b, f, s, u, u0, u1, d, wd, wf, wh, top, hr);
        Rows(b, f, s, "Wall", hung, top);
    }

    /// <summary>The schedule's rows of a kitchen without units of its own: its run split evenly, each a drawer over a door, its tall unit, its wall units.</summary>
    static void AutoRows(Builder b, Frame f, Spec s, Run run, double fa, double fe, (double, double)? tallSpan)
    {
        var n = Math.Max(1, (int)Math.Round((fe - fa) / s.Num("unit", 0.6)));
        var uw = (fe - fa) / n;
        var baseT = KitchenUnits.Find("base", false)!;
        var spans = Enumerable.Range(0, n).Select(i => (fa + i * uw, fa + (i + 1) * uw)).ToList();
        var rows = spans.Select(x => (new Unit(baseT, new JsonObject { ["type"] = "base", ["width"] = uw }), x.Item1, x.Item2)).ToList();
        if (tallSpan is { } ts) rows.Add((new Unit(KitchenUnits.Find("tall", false)!, new JsonObject { ["type"] = "tall", ["width"] = Tall }), ts.Item1, ts.Item2));
        Rows(b, f, s, "Base", rows, s.H);
        if (!run.WallUnits) return;
        var wallT = KitchenUnits.Find("wall", true)!;
        Rows(b, f, s, "Wall", [.. spans.Select(x => (new Unit(wallT, new JsonObject { ["type"] = "wall", ["width"] = uw }), x.Item1, x.Item2))], s.H);
    }

    /// <summary>The schedule's rows of a line, from the left as seen from the front.</summary>
    static void Rows(Builder b, Frame f, Spec s, string line, List<(Unit U, double U0, double U1)> units, double top)
    {
        var placed = units.Select(x => (x.U, x.U0, x.U1, At: Visual(b, f, (x.U0 + x.U1) / 2))).OrderBy(x => x.At).ToList();
        for (var i = 0; i < placed.Count; i++)
        {
            var (u, u0, u1, at) = placed[i];
            b.Units.Add(new KitchenUnitRow(line, i + 1, u.T.Key, u.T.Label, Math.Round(u1 - u0, 4), Math.Round(HeightOf(u, s, top), 4), Detail(u, s, b.Mirrored), Math.Round(at, 4)));
        }
    }

    /// <summary>Where a point of the run is across the piece as seen from its front (mirrored builds turned back).</summary>
    static double Visual(Builder b, Frame f, double u) { var x = f.P(u, 0).X; return b.Mirrored ? -x : x; }

    static double HeightOf(Unit u, Spec s, double top) => u.T.Key switch
    {
        "tall" or "oven" => u.Num("height") ?? TallHeight,
        "fridge" when u.Str("fridge") == "tall" => u.Num("height") ?? TallHeight,
        "fridge" when u.Str("fridge") == "freestanding" => u.Num("height") ?? 1.85,
        "wall" => u.Num("height") ?? s.Num("wallHeight", 0.72),
        "hood" => u.Str("hood") == "integrated" ? u.Num("height") ?? 0.36 : 0.1,
        "space" or "gap" => 0,
        _ => top,
    };

    /// <summary>A unit's doors in words, its hand as seen (a mirrored build's turned back).</summary>
    static string Hinge(Unit u, bool mirrored) => u.Doors == 2 ? "2 doors" : $"1 door, hinged {(mirrored ^ (u.Str("hand") == "right") ? "right" : "left")}";

    /// <summary>What a unit's front is, or what the appliance is, in words.</summary>
    static string Detail(Unit u, Spec s, bool m) => u.T.Key switch
    {
        "base" => u.Str("front") switch
        {
            "doors" => Hinge(u, m),
            "drawers" => $"{(int)(u.Num("drawers") ?? 3)} drawers",
            var x => KitchenUnits.Label(x),
        },
        "sink" => $"{KitchenUnits.Label(u.Str("sink"))}, {KitchenUnits.Label(u.Str("tap"))}",
        "hob" => $"{KitchenUnits.Label(u.Str("fuel"))} hob {Math.Round(u.W * 1000)}{(u.Bool("oven") ? ", oven under it" : "")}",
        "cooker" => $"{KitchenUnits.Label(u.Str("fuel"))} hob, oven",
        "dishwasher" => $"Integrated {Math.Round(u.W * 1000)}",
        "fridge" => KitchenUnits.Label(u.Str("fridge")),
        "tall" => u.Str("front") == "open" ? "Open shelves" : Hinge(u, m),
        "wall" => u.Str("front") == "open" ? "Open shelves" : u.Str("front") == "glass" ? $"Glazed, {Hinge(u, m)}" : Hinge(u, m),
        "hood" => KitchenUnits.Label(u.Str("hood")),
        _ => "",
    };

    static IEnumerable<(double, double)> Merge(List<(double A, double B)> spans)
    {
        (double A, double B)? cur = null;
        foreach (var sp in spans.OrderBy(x => x.A))
        {
            if (cur is { } c && sp.A <= c.B + 1e-6) cur = (c.A, Math.Max(c.B, sp.B));
            else { if (cur is { } done) yield return done; cur = sp; }
        }
        if (cur is { } last) yield return last;
    }

    // ---- Base units ----

    /// <summary>
    /// A base-line unit over u0…u1 (its front at v 0, the wall at v d): its carcass, fronts, handles, appliance and plan
    /// symbol. Whether the worktop runs over it. <paramref name="corner"/>: where the blind corner before it starts (a corner sink reaches into it).
    /// </summary>
    static bool BaseUnit(Builder b, Frame f, Spec s, Unit u, double u0, double u1, double d, double top, double wt, double pl, string pr, string hr, double? corner)
    {
        double ua = u0 + 0.0015, ub = u1 - 0.0015, w = u1 - u0, zf0 = pl + 0.003, zf1 = top - wt - 0.003, vm = d / 2;
        void Carcass(double z1) { f.Block(b, pr, u0, u1, 0.07, d, 0, pl); f.Block(b, "body", u0, u1, 0.04, d, pl, z1); }
        switch (u.T.Key)
        {
            case "base":
                switch (u.Str("front"))
                {
                    case "open": Plinth(); OpenBox(b, f, u0, u1, 0.04, d, pl, top - wt, 1); break;
                    case "doors": Carcass(top - wt); DoorLeaves(b, f, u, ua, ub, zf0, zf1, hr, zf1 - 0.13, zf1 - 0.03, true); break;
                    case "drawers": Carcass(top - wt); Drawers(b, f, ua, ub, zf0, zf1, Enumerable.Repeat(1.0, (int)(u.Num("drawers") ?? 3)).ToArray(), hr); break;
                    case "pan": Carcass(top - wt); Drawers(b, f, ua, ub, zf0, zf1, [0.16, (zf1 - zf0 - 0.16) / 2, (zf1 - zf0 - 0.16) / 2], hr); break;
                    default: Carcass(top - wt); DrawerOverDoor(); break;
                }
                return true;
            case "filler":
                Plinth();
                f.Block(b, "fronts", u0, u1, 0.022, d, pl, top - wt);
                return true;
            case "gap":
                return true;
            case "sink":
            {
                Carcass(top - wt);
                DoorLeaves(b, f, u, ua, ub, zf0, zf1, hr, zf1 - 0.13, zf1 - 0.03, true);
                Sink(b, f, u, u0, u1, d, top, corner);
                return true;
            }
            case "hob":
            {
                Carcass(top - wt);
                if (u.Bool("oven"))
                {
                    // An oven 600 wide under the hob (a wider hob: fronts either side), a drawer under it.
                    var ow = Math.Min(w, 0.6);
                    double o0 = (u0 + u1) / 2 - ow / 2 + 0.0015, o1 = (u0 + u1) / 2 + ow / 2 - 0.0015, oz0 = Math.Max(zf0 + 0.1, zf1 - 0.6);
                    f.Block(b, "glass", o0, o1, 0.022, 0.04, oz0, zf1, 0.002);
                    f.Block(b, hr, o0 + 0.05, o1 - 0.05, 0.0, 0.022, zf1 - 0.06, zf1 - 0.045);
                    f.Block(b, "fronts", ua, ub, 0.022, 0.04, zf0, oz0 - 0.003, 0.002);
                    if (o0 - ua > 0.01) { f.Block(b, "fronts", ua, o0 - 0.003, 0.022, 0.04, oz0, zf1, 0.002); f.Block(b, "fronts", o1 + 0.003, ub, 0.022, 0.04, oz0, zf1, 0.002); }
                }
                else DrawerOverDoor();
                Hob(b, f, u, u0, u1, d, top);
                return true;
            }
            case "cooker":
            {
                // Freestanding: its own body to the worktop's height, an oven door, the hob on it; no carcass, no worktop over it.
                f.Block(b, "metal", u0 + 0.002, u1 - 0.002, 0.02, d - 0.02, 0, top, 0.004);
                f.Block(b, "glass", ua + 0.03, ub - 0.03, 0.004, 0.02, 0.12, top - 0.16, 0.003);
                f.Block(b, hr, ua + 0.06, ub - 0.06, -0.016, 0.004, top - 0.13, top - 0.115);
                Hob(b, f, u, u0, u1, d, top);
                f.Rect(b, Sym, u0 + 0.002, u1 - 0.002, 0.02, d - 0.02);
                return false;
            }
            case "dishwasher":
            {
                // Integrated: the machine behind a full-height front matching the others.
                f.Block(b, pr, u0, u1, 0.07, d, 0, pl);
                f.Block(b, "metal", u0 + 0.004, u1 - 0.004, 0.04, d - 0.02, pl, top - wt);
                f.Block(b, "fronts", ua, ub, 0.022, 0.04, zf0, zf1, 0.002);
                var mid = (ua + ub) / 2;
                f.Block(b, hr, mid - Math.Min(0.07, w / 4), mid + Math.Min(0.07, w / 4), 0.0, 0.022, zf1 - 0.05, zf1 - 0.038);
                Letters(b, f, "DW", (u0 + u1) / 2, vm, 0.1);
                f.Line(b, Sym, u0, 0.04, u1, d);
                return true;
            }
            case "fridge":
                switch (u.Str("fridge"))
                {
                    case "tall":
                    {
                        var h = u.Num("height") ?? TallHeight;
                        TallCarcass(h);
                        DoorLeaves(b, f, u, ua, ub, zf0, pl + 0.72, hr, pl + 0.55, pl + 0.7, false);
                        DoorLeaves(b, f, u, ua, ub, pl + 0.723, h - 0.003, hr, pl + 0.78, pl + 1.0, true);
                        TallSymbol(u0, u1, "F");
                        return false;
                    }
                    case "freestanding":
                    {
                        var h = u.Num("height") ?? 1.85;
                        f.Block(b, "metal", u0 + 0.005, u1 - 0.005, 0.0, d, 0, h, 0.01);
                        var hx = u.Str("hand") == "right" ? u0 + 0.05 : u1 - 0.05;
                        f.Block(b, hr, hx - 0.008, hx + 0.008, -0.03, 0, 0.75, Math.Min(h - 0.1, 1.45));
                        f.Rect(b, Sym, u0 + 0.005, u1 - 0.005, 0, d);
                        f.Line(b, Sym, u0 + 0.005, 0, u1 - 0.005, d);
                        f.Line(b, Sym, u0 + 0.005, d, u1 - 0.005, 0);
                        Letters(b, f, "F", (u0 + u1) / 2, d * 0.25, 0.1);
                        return false;
                    }
                    default:
                        Carcass(top - wt);
                        DoorLeaves(b, f, u, ua, ub, zf0, zf1, hr, zf1 - 0.13, zf1 - 0.03, true);
                        Letters(b, f, "F", (u0 + u1) / 2, vm, 0.1);
                        return true;
                }
            case "oven":
            {
                var h = u.Num("height") ?? TallHeight;
                TallCarcass(h);
                // Two drawers under the oven at eye height, a door over it.
                double oz0 = Math.Min(h - 0.9, 0.85), oz1 = oz0 + 0.6;
                Drawers(b, f, ua, ub, zf0, oz0 - 0.003, [1, 1], hr);
                f.Block(b, "glass", ua, ub, 0.022, 0.04, oz0, oz1, 0.002);
                f.Block(b, hr, ua + 0.05, ub - 0.05, 0.0, 0.022, oz1 - 0.06, oz1 - 0.045);
                if (h - 0.003 - (oz1 + 0.003) > 0.1) DoorLeaves(b, f, u, ua, ub, oz1 + 0.003, h - 0.003, hr, oz1 + 0.05, oz1 + 0.25, false);
                TallSymbol(u0, u1, "OV");
                return false;
            }
            case "tall":
            {
                var h = u.Num("height") ?? TallHeight;
                if (u.Str("front") == "open") { Plinth(); OpenBox(b, f, u0, u1, 0.04, d, pl, h, 4); }
                else
                {
                    TallCarcass(h);
                    var split = Math.Min(pl + 1.2, h - 0.3);
                    DoorLeaves(b, f, u, ua, ub, zf0, split, hr, split - 0.35, split - 0.1, true);
                    DoorLeaves(b, f, u, ua, ub, split + 0.003, h - 0.003, hr, split + 0.05, split + 0.3, false);
                }
                TallSymbol(u0, u1, null);
                return false;
            }
        }
        return true;

        void Plinth() => f.Block(b, pr, u0, u1, 0.07, d, 0, pl);
        void TallCarcass(double h) { f.Block(b, pr, u0, u1, 0.07, d, 0, pl); f.Block(b, "body", u0, u1, 0.04, d, pl, h); }
        void TallSymbol(double t0, double t1, string? label)
        {
            f.Rect(b, Sym, t0, t1, 0, d);
            f.Line(b, Sym, t0, 0, t1, d);
            f.Line(b, Sym, t0, d, t1, 0);
            if (label != null) Letters(b, f, label, (t0 + t1) / 2, d * 0.25, 0.1);
        }
        void DrawerOverDoor()
        {
            f.Block(b, "fronts", ua, ub, 0.022, 0.04, top - wt - 0.16, top - wt - 0.003, 0.002);
            f.Block(b, "fronts", ua, ub, 0.022, 0.04, pl + 0.003, top - wt - 0.163, 0.002);
            var mid = (ua + ub) / 2;
            f.Block(b, hr, mid - Math.Min(0.07, w / 4), mid + Math.Min(0.07, w / 4), 0.0, 0.022, top - wt - 0.05, top - wt - 0.038);
        }
    }

    /// <summary>One door or a pair over ua…ub, z0…z1, each with its handle by the edge it opens at; a base or tall unit's swing drawn in plan at the finest detail.</summary>
    static void DoorLeaves(Builder b, Frame f, Unit u, double ua, double ub, double z0, double z1, string hr, double hz0, double hz1, bool swing, string role = "fronts", double v0 = 0.022, double v1 = 0.04)
    {
        if (z1 - z0 < 0.05) return;
        var n = Math.Clamp(u.Doors, 1, 2);
        var hingeLeft = u.Str("hand") != "right";
        var lw = (ub - ua) / n;
        for (var i = 0; i < n; i++)
        {
            double l0 = ua + i * lw + (i > 0 ? 0.0015 : 0), l1 = ua + (i + 1) * lw - (i < n - 1 ? 0.0015 : 0);
            // A pair opens from the middle; one door at its hand.
            var left = n == 2 ? i == 0 : hingeLeft;
            f.Block(b, role, l0, l1, v0, v1, z0, z1, 0.002);
            var hx = left ? l1 - 0.035 : l0 + 0.035;
            f.Block(b, hr, hx - 0.006, hx + 0.006, v0 - 0.022, v0, Math.Max(z0 + 0.02, hz0), Math.Min(z1 - 0.02, hz1));
            if (!swing) continue;
            var hinge = left ? l0 : l1;
            var dir = left ? 1 : -1;
            var r = l1 - l0;
            var arc = Enumerable.Range(0, 9).Select(k => { var t = k * Math.PI / 16; return f.P(hinge + dir * r * Math.Cos(t), -r * Math.Sin(t)); }).ToList();
            b.LineFrom(PlanDetail.Fine, "dashed", arc);
            b.LineFrom(PlanDetail.Fine, "dashed", [f.P(hinge, 0), f.P(hinge, -r)]);
        }
    }

    /// <summary>Drawers stacked over ua…ub, z0…z1, from the top down by their shares of it, each with a handle near its top.</summary>
    static void Drawers(Builder b, Frame f, double ua, double ub, double z0, double z1, double[] shares, string hr)
    {
        var total = shares.Sum();
        var z = z1;
        var mid = (ua + ub) / 2;
        var hw = Math.Min(0.07, (ub - ua) / 4);
        foreach (var sh in shares)
        {
            var h = (z1 - z0) * sh / total;
            double hi = z - 0.0015, lo = Math.Max(z0, z - h + 0.0015);
            f.Block(b, "fronts", ua, ub, 0.022, 0.04, lo, hi, 0.002);
            f.Block(b, hr, mid - hw, mid + hw, 0.0, 0.022, hi - Math.Min(0.05, (hi - lo) / 3) - 0.006, hi - Math.Min(0.05, (hi - lo) / 3) + 0.006);
            z -= h;
        }
    }

    /// <summary>An open unit: its sides, back, bottom, top and shelves, no front.</summary>
    static void OpenBox(Builder b, Frame f, double u0, double u1, double v0, double v1, double z0, double z1, int shelves)
    {
        const double t = 0.018;
        f.Block(b, "body", u0, u0 + t, v0, v1, z0, z1);
        f.Block(b, "body", u1 - t, u1, v0, v1, z0, z1);
        f.Block(b, "body", u0 + t, u1 - t, v1 - 0.008, v1, z0 + t, z1 - t);
        f.Block(b, "body", u0 + t, u1 - t, v0, v1, z0, z0 + t);
        f.Block(b, "body", u0 + t, u1 - t, v0, v1, z1 - t, z1);
        for (var i = 1; i <= shelves; i++)
        {
            var z = z0 + (z1 - z0) * i / (shelves + 1);
            f.Block(b, "body", u0 + t, u1 - t, v0 + 0.01, v1 - 0.008, z - t / 2, z + t / 2);
        }
    }

    /// <summary>A sink in its unit: the plate on the worktop, its bowls in plan, its tap at the back. A corner sink reaches into the blind corner before it.</summary>
    static void Sink(Builder b, Frame f, Unit u, double u0, double u1, double d, double top, double? corner)
    {
        var style = u.Str("sink");
        var vm = d / 2;
        double pd = Math.Min(0.5, d - 0.1), p0 = u0 + 0.02, p1 = u1 - 0.02;
        if (style == "corner" && corner is { } c0) p0 = c0 + 0.05;
        f.Block(b, "metal", p0, p1, vm - pd / 2, vm + pd / 2, top, top + 0.004, 0.02);
        f.Rect(b, Sym, p0, p1, vm - pd / 2, vm + pd / 2, 0.02);
        // The bowls: their spans across the plate.
        var inner = (p1 - p0) - 0.08;
        var bowls = style switch
        {
            "one-half" => new[] { (p0 + 0.04, p0 + 0.04 + inner * 0.62), (p0 + 0.04 + inner * 0.66, p1 - 0.04) },
            "double" => [(p0 + 0.04, p0 + 0.04 + inner * 0.48), (p0 + 0.04 + inner * 0.52, p1 - 0.04)],
            "corner" when corner != null => [(p0 + 0.03, u0 - 0.03), (u0 + 0.03, p1 - 0.04)],
            _ => [(p0 + 0.04, p1 - 0.04)],
        };
        foreach (var (b0, b1) in bowls)
        {
            if (b1 - b0 < 0.08) continue;
            f.Rect(b, Sym, b0, b1, vm - pd / 2 + 0.06, vm + pd / 2 - 0.08, 0.04);
            f.Circle(b, Sym, (b0 + b1) / 2, vm, 0.022, 12);
        }
        var tap = u.Str("tap");
        if (tap == "none") return;
        var ut = style == "corner" && corner != null ? u0 : (bowls[0].Item1 + bowls[0].Item2) / 2;
        var pull = tap == "pull-out";
        f.Cylinder(b, "metal", ut, d - 0.07, pull ? 0.024 : 0.016, top, top + (pull ? 0.42 : 0.3), 10);
        if (pull) f.Block(b, "metal", ut - 0.02, ut + 0.02, d - 0.27, d - 0.07, top + 0.38, top + 0.42, 0.01);
        f.Circle(b, Sym, ut, d - 0.07, pull ? 0.028 : 0.02, 12);
    }

    /// <summary>A hob on a unit's top: induction glass with its zones, or gas with its burners and pan supports.</summary>
    static void Hob(Builder b, Frame f, Unit u, double u0, double u1, double d, double top)
    {
        var gas = u.Str("fuel") == "gas";
        double vm = d / 2, hw = Math.Min(u1 - u0 - 0.02, 0.88) / 2, hd = Math.Min(0.26, d / 2 - 0.04), uc = (u0 + u1) / 2;
        f.Block(b, gas ? "metal" : "glass", uc - hw, uc + hw, vm - hd, vm + hd, top, top + 0.006, 0.01);
        f.Rect(b, Sym, uc - hw, uc + hw, vm - hd, vm + hd, 0.01);
        var wide = hw > 0.35;
        var zones = new List<(double X, double Y, double R)> { (-hw * 0.5, -hd * 0.45, 0.09), (hw * 0.5, -hd * 0.45, 0.07), (-hw * 0.5, hd * 0.45, 0.07), (hw * 0.5, hd * 0.45, 0.09) };
        if (wide) { zones = [(-hw * 0.62, -hd * 0.45, 0.08), (hw * 0.62, -hd * 0.45, 0.07), (-hw * 0.62, hd * 0.45, 0.07), (hw * 0.62, hd * 0.45, 0.08), (0, 0, 0.11)]; }
        foreach (var (x, y, r) in zones)
        {
            f.Circle(b, Sym, uc + x, vm + y, gas ? r * 0.55 : r, 16);
            if (gas) f.Cylinder(b, "metal", uc + x, vm + y, r * 0.45, top + 0.006, top + 0.026, 10);
        }
        if (gas)
        {
            // The pan supports, as drawn: a grid over the burners.
            f.Line(b, Sym, uc - hw + 0.02, vm, uc + hw - 0.02, vm);
            f.Line(b, Sym, uc, vm - hd + 0.02, uc, vm + hd - 0.02);
        }
    }

    // ---- The wall line ----

    static void UpperUnit(Builder b, Frame f, Spec s, Unit u, double u0, double u1, double d, double wd, double wf, double wh, double top, string hr)
    {
        double ua = u0 + 0.0015, ub = u1 - 0.0015, v0 = d - wd;
        switch (u.T.Key)
        {
            case "wall":
            {
                double z0 = u.Num("from") ?? wf, h = u.Num("height") ?? wh, z1 = z0 + h;
                if (u.Str("front") == "open") OpenBox(b, f, u0, u1, v0, d, z0, z1, h > 0.5 ? 2 : 1);
                else
                {
                    f.Block(b, "body", u0, u1, v0 + 0.018, d, z0, z1);
                    DoorLeaves(b, f, u, ua, ub, z0 + 0.002, z1 - 0.002, hr, z0 + 0.03, z0 + 0.13, false, u.Str("front") == "glass" ? "glass" : "fronts", v0, v0 + 0.018);
                }
                f.Rect(b, "overhead", u0, u1, v0, d);
                break;
            }
            case "hood":
            {
                if (u.Str("hood") == "integrated")
                {
                    // In a wall unit: the slide-out visor under its door.
                    double z0 = u.Num("from") ?? wf, z1 = z0 + (u.Num("height") ?? 0.36);
                    f.Block(b, "metal", ua, ub, v0, d, z0, z0 + 0.03);
                    f.Block(b, "body", u0, u1, v0 + 0.018, d, z0 + 0.03, z1);
                    f.Block(b, "fronts", ua, ub, v0, v0 + 0.018, z0 + 0.032, z1 - 0.002, 0.002);
                    f.Rect(b, "overhead", u0, u1, v0, d);
                }
                else
                {
                    // A chimney hood: its canopy 650 over the worktop, its flue up to the wall units' top.
                    var hz = u.Num("from") ?? top + 0.65;
                    var cd = Math.Min(0.5, d);
                    f.Block(b, "metal", u0 + 0.005, u1 - 0.005, d - cd, d, hz, hz + 0.1, 0.01);
                    var uc = (u0 + u1) / 2;
                    f.Block(b, "metal", uc - 0.14, uc + 0.14, d - 0.26, d, hz + 0.1, Math.Max(hz + 0.4, wf + wh));
                    f.Rect(b, "overhead", u0 + 0.005, u1 - 0.005, d - cd, d);
                    f.Line(b, "overhead", u0 + 0.005, d - cd, u1 - 0.005, d);
                    f.Line(b, "overhead", u0 + 0.005, d, u1 - 0.005, d - cd);
                }
                break;
            }
        }
    }

    /// <summary>A hood hung over an island's (or a peninsula's) hob: its canopy 650 over the worktop, its flue up to 2.5 m.</summary>
    static void IslandHood(Builder b, Frame f, Spec s, double uh, double d)
    {
        double top = s.H, hz = top + 0.65, vm = d / 2;
        f.Block(b, "metal", uh - 0.45, uh + 0.45, vm - 0.3, vm + 0.3, hz, hz + 0.1, 0.01);
        f.Block(b, "metal", uh - 0.14, uh + 0.14, vm - 0.14, vm + 0.14, hz + 0.1, Math.Max(hz + 0.4, 2.5));
        f.Rect(b, "overhead", uh - 0.45, uh + 0.45, vm - 0.3, vm + 0.3);
        f.Line(b, "overhead", uh - 0.45, vm - 0.3, uh + 0.45, vm + 0.3);
        f.Line(b, "overhead", uh - 0.45, vm + 0.3, uh + 0.45, vm - 0.3);
    }

    // ---- Labels ----

    /// <summary>The strokes of the letters an appliance is marked with in plan (DW, F, OV), in a unit square, y up.</summary>
    static readonly Dictionary<char, (double X, double Y)[][]> Glyphs = new()
    {
        ['D'] = [[(0, 0), (0, 1), (0.55, 1), (0.9, 0.75), (0.9, 0.25), (0.55, 0), (0, 0)]],
        ['W'] = [[(0, 1), (0.22, 0), (0.45, 0.65), (0.68, 0), (0.9, 1)]],
        ['F'] = [[(0, 0), (0, 1), (0.8, 1)], [(0, 0.52), (0.6, 0.52)]],
        ['O'] = [[(0.3, 0), (0.6, 0), (0.9, 0.3), (0.9, 0.7), (0.6, 1), (0.3, 1), (0, 0.7), (0, 0.3), (0.3, 0)]],
        ['V'] = [[(0, 1), (0.45, 0), (0.9, 1)]],
    };

    /// <summary>Letters of height <paramref name="h"/> centred at (u, v) on a run, readable from its front (and from it when the piece is mirrored).</summary>
    static void Letters(Builder b, Frame f, string text, double u, double v, double h)
    {
        var adv = h * 0.9 + h * 0.3;
        var total = text.Length * adv - h * 0.3;
        for (var i = 0; i < text.Length; i++)
        {
            if (!Glyphs.TryGetValue(text[i], out var strokes)) continue;
            // Built mirrored, the plan is turned over: the letters are laid from the other end and each turned back.
            var slot = b.Mirrored ? text.Length - 1 - i : i;
            var x0 = u - total / 2 + slot * adv;
            foreach (var st in strokes)
                b.Line(Sym, [.. st.Select(p => f.P(x0 + (b.Mirrored ? 0.9 - p.X : p.X) * h, v + (p.Y - 0.5) * h))]);
        }
    }
}
