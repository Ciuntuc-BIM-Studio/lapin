using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Furniture;

/// <summary>
/// A board of a casework piece, for its cut list (decision 475): its part, the carcass it belongs to (0: the piece's own,
/// a top, a plinth, a sliding door), its material role, its length along the grain, its width and thickness (metres), the
/// grain's axis in the piece's frame (x across, y in depth, z up) and whether it is a front (listed apart).
/// </summary>
public sealed record BoardRow(string Part, int Carcass, string Role, double Length, double Width, double Thickness, string Grain, bool Front);

/// <summary>A casework preset (a wardrobe, a sideboard, a desk…): the values it starts a type from and its IFC type.</summary>
public sealed record CaseworkPreset(string Key, string Label, string Predefined, string? ObjectType, JsonObject Values);

/// <summary>A style applied to a casework type as a patch of its values (handleless, slim legs, fluted fronts…).</summary>
public sealed record CaseworkStyle(string Key, string Label, JsonObject Patch);

/// <summary>
/// Casework (docs/plan-furniture.md, Configuratorul flexibil, stage 1; decision 475): furniture made of boards. A piece
/// is one or more <b>carcasses</b> side by side (<c>carcasses</c> in the type's spec, from the left as seen from the
/// front; those without a width share what the others leave of the piece's width; a <c>gap</c> is a knee space under
/// the top), each a box of boards (sides, bottom, top, back) holding <b>zones</b> stacked from the bottom, each with its
/// front (open, doors, drawers by ratio, a flap) and its interior (shelves, fixed or adjustable, a hanging rail, inner
/// drawers), a divider between them. The piece stands on a plinth, legs or nothing, or hangs on the wall; it has its
/// own top or not; its handles are one style for all its fronts, or none (push to open), or a J-profile milled into
/// them. A bed is its own form: a frame, a headboard, drawers under it. Every board is a solid of its material role,
/// and is listed in the cut list with its sizes and grain. Ideas on joinery taken from FreeCAD's Woodworking workbench
/// (dprojects, MIT), rewritten; no code of it is used.
/// </summary>
public static class Casework
{
    public const string Key = "casework", CarcassesKey = "carcasses";

    /// <summary>The gap round every front (3 mm: 1.5 mm either side of a joint).</summary>
    public const double Gap = 0.003;

    static JsonObject D(params (string Key, JsonNode? Value)[] v) { var o = new JsonObject(); foreach (var (k, x) in v) o[k] = x; return o; }
    static JsonObject Z(string front, params (string Key, JsonNode? Value)[] more) { var o = D(more); o["front"] = front; return o; }
    static JsonObject C(params JsonObject[] zones) => new() { ["zones"] = new JsonArray([.. zones]) };
    static JsonObject Cw(double width, params JsonObject[] zones) => new() { ["width"] = width, ["zones"] = new JsonArray([.. zones]) };
    static JsonArray A(params JsonNode[] items) => new([.. items]);

    public static readonly string[] Fronts = ["open", "doors", "drawers", "flap"];

    static UnitField L(string k, string l, double min, double max, (string, string[])? when = null) => new(k, l, "length", min, max, When: when);
    static UnitField N(string k, string l, double min, double max, (string, string[])? when = null) => new(k, l, "number", min, max, When: when);
    static UnitField Ch(string k, string l, string[] c, (string, string[])? when = null) => new(k, l, "choice", Choices: c, When: when);
    static UnitField B(string k, string l, (string, string[])? when = null) => new(k, l, "bool", When: when);

    /// <summary>A zone's fields, as the configurator edits them.</summary>
    public static readonly UnitField[] ZoneFields =
    [
        Ch("front", "Front", Fronts),
        L("height", "Height", 0.05, 3),
        new("share", "Share of the rest", "number", 0.1, 10),
        N("doors", "Doors", 1, 4, ("front", ["doors"])),
        Ch("hand", "Hinged on the", ["left", "right"], ("front", ["doors"])),
        B("glazed", "Glazed", ("front", ["doors"])),
        N("drawers", "Drawers", 1, 6, ("front", ["drawers"])),
        new("ratios", "Heights by ratio, from the top", "ratios", When: ("front", ["drawers"])),
        Ch("flap", "Flap opens", ["down", "up"], ("front", ["flap"])),
        N("shelves", "Shelves", 0, 12, ("front", ["open", "doors", "flap"])),
        B("adjustable", "Adjustable shelves", ("front", ["open", "doors", "flap"])),
        B("rail", "Hanging rail", ("front", ["open", "doors"])),
        N("innerDrawers", "Inner drawers", 0, 4, ("front", ["doors"])),
    ];

    /// <summary>A carcass's own fields: a box or a knee space, its width (none: a share of what is left) and its height (none: the piece's).</summary>
    public static readonly UnitField[] CarcassFields =
    [
        Ch("kind", "Carcass", ["box", "gap"]),
        L("width", "Width", 0.1, 6),
        L("height", "Height", 0.1, 3, ("kind", ["box"])),
    ];

    /// <summary>The carcasses a new casework type starts with: a door, three drawers, a door.</summary>
    public static JsonArray DefaultCarcasses() => A(
        C(Z("doors", ("doors", 1), ("hand", "left"), ("shelves", 1), ("adjustable", true))),
        C(Z("drawers", ("drawers", 3))),
        C(Z("doors", ("doors", 1), ("hand", "right"), ("shelves", 1), ("adjustable", true))));

    public static readonly IReadOnlyList<CaseworkPreset> Presets =
    [
        new("sideboard", "Sideboard", "USERDEFINED", "Sideboard", D(("width", 1.8), ("depth", 0.45), ("height", 0.8), ("carcasses", DefaultCarcasses()))),
        new("wardrobe", "Wardrobe", "USERDEFINED", "Wardrobe", D(
            ("width", 2.0), ("depth", 0.6), ("height", 2.3), ("base", "plinth"), ("plinthHeight", 0.08), ("plinthRecess", 0.03), ("top", "none"),
            ("carcasses", A(C(Z("doors", ("doors", 2), ("rail", true))), C(Z("doors", ("doors", 2), ("shelves", 4), ("adjustable", true), ("innerDrawers", 2))))))),
        new("wardrobeSliding", "Wardrobe, sliding doors", "USERDEFINED", "Wardrobe", D(
            ("width", 2.0), ("depth", 0.65), ("height", 2.3), ("base", "plinth"), ("plinthHeight", 0.08), ("plinthRecess", 0.03), ("top", "none"), ("sliding", 2),
            ("carcasses", A(C(Z("open", ("rail", true))), C(Z("open", ("shelves", 4), ("adjustable", true)))))) ),
        new("bookcase", "Bookcase", "SHELF", null, D(
            ("width", 1.0), ("depth", 0.35), ("height", 2.0), ("base", "plinth"), ("plinthHeight", 0.06), ("plinthRecess", 0.02), ("top", "none"), ("handles", "knob"),
            ("carcasses", A(C(Z("doors", ("doors", 2), ("height", 0.75), ("shelves", 1)), Z("open", ("shelves", 3), ("adjustable", true))))))),
        new("tvUnit", "TV unit", "USERDEFINED", "TV unit", D(
            ("width", 1.8), ("depth", 0.4), ("height", 0.4), ("base", "floating"), ("mount", 0.3), ("top", "none"), ("handles", "none"),
            ("carcasses", A(C(Z("flap", ("flap", "down"))), Cw(0.6, Z("open", ("shelves", 0))), C(Z("flap", ("flap", "down"))))))),
        new("chest", "Chest of drawers", "USERDEFINED", "Chest of drawers", D(
            ("width", 1.0), ("depth", 0.48), ("height", 0.85), ("base", "plinth"), ("plinthHeight", 0.08), ("plinthRecess", 0.03), ("top", "overhang"), ("overhang", 0.01),
            ("carcasses", A(C(Z("drawers", ("drawers", 4), ("ratios", A(1, 1, 1.3, 1.3)))))))),
        new("vanity", "Bathroom vanity", "USERDEFINED", "Bathroom vanity", D(
            ("width", 0.8), ("depth", 0.48), ("height", 0.5), ("base", "floating"), ("mount", 0.35), ("top", "worktop"), ("topThickness", 0.02), ("overhang", 0.01),
            ("basin", true), ("handles", "jprofile"), ("carcasses", A(C(Z("drawers", ("drawers", 2), ("ratios", A(1, 1.5)))))))),
        new("desk", "Desk", "DESK", null, D(
            ("width", 1.4), ("depth", 0.7), ("height", 0.75), ("base", "none"), ("top", "overhang"), ("overhang", 0.0), ("topThickness", 0.03),
            ("carcasses", A(Cw(0.42, Z("drawers", ("drawers", 3))), new JsonObject { ["kind"] = "gap" })))),
        new("hallUnit", "Hall unit", "USERDEFINED", "Hall unit", D(
            ("width", 1.2), ("depth", 0.35), ("height", 1.0), ("base", "legs"), ("legs", "metal"), ("legHeight", 0.12), ("top", "overhang"), ("overhang", 0.01), ("handles", "edge"),
            ("carcasses", A(C(Z("flap", ("flap", "down")), Z("flap", ("flap", "down"))), C(Z("flap", ("flap", "down")), Z("flap", ("flap", "down"))))))),
        new("bed", "Bed", "BED", null, D(
            ("form", "bed"), ("width", 1.65), ("depth", 2.1), ("height", 1.0), ("carcassBoard", 0.025), ("mattressHeight", 0.5), ("bedDrawers", 0), ("handles", "none"),
            ("carcasses", A()))),
        new("nightstand", "Nightstand", "USERDEFINED", "Bedside table", D(
            ("width", 0.45), ("depth", 0.4), ("height", 0.4), ("base", "legs"), ("legs", "metal"), ("legHeight", 0.16), ("top", "none"),
            ("carcasses", A(C(Z("open"), Z("drawers", ("drawers", 1), ("height", 0.15))))))),
    ];

    public static readonly IReadOnlyList<CaseworkStyle> Styles =
    [
        new("handleless", "Handleless, push to open", D(("handles", "none"))),
        new("jprofile", "J-profile grip", D(("handles", "jprofile"))),
        new("slimLegs", "Slim metal legs", D(("base", "legs"), ("legs", "metal"), ("legHeight", 0.15), ("materials", D(("legs", "Black powder-coated steel"))))),
        new("floating", "Floating, hung on the wall", D(("base", "floating"))),
        new("fluted", "Fluted fronts", D(("frontStyle", "fluted"))),
        new("twoTone", "Two-tone, wood and colour", D(("materials", D(("body", "Oak veneer"), ("top", "Oak solid wood"), ("fronts", "Lacquered MDF sage"))))),
        new("framed", "Framed fronts, knobs", D(("frontStyle", "framed"), ("handles", "knob"))),
    ];

    public static string[] PresetKeys => [.. Presets.Select(p => p.Key)];

    public static CaseworkPreset? FindPreset(string? key) => Presets.FirstOrDefault(p => p.Key == key);

    /// <summary>A preset's values over the kind's: what a type of it starts from (or is reset to when its preset is changed).</summary>
    public static JsonObject Preset(string key)
    {
        var p = FindPreset(key) ?? throw new CommandException($"A casework preset is one of {string.Join(", ", PresetKeys)}.");
        var k = FurnitureKinds.Find(Key)!;
        var o = new JsonObject { ["width"] = k.Width, ["depth"] = k.Depth, ["height"] = k.Height };
        foreach (var (kk, v) in k.Defaults) o[kk] = v?.DeepClone();
        foreach (var (kk, v) in p.Values) o[kk] = v?.DeepClone();
        o["preset"] = key;
        return o;
    }

    /// <summary>The IFC type a casework spec is: its preset's (a bed BED, a desk DESK, a bookcase SHELF, the others USERDEFINED with their name).</summary>
    public static (string Predefined, string? ObjectType) Ifc(Spec s)
    {
        if (s.Str("form") == "bed") return ("BED", null);
        var p = FindPreset(s.Str("preset"));
        return p == null ? ("USERDEFINED", "Casework") : (p.Predefined, p.ObjectType);
    }

    /// <summary>The words a type of the spec is named and listed by: its preset's.</summary>
    public static string Label(Spec s) => s.Str("form") == "bed" ? "Bed" : FindPreset(s.Str("preset"))?.Label ?? "Casework";

    static string Mm(double v) => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);

    // ---- Checking ----

    static JsonNode? Checked(UnitField f, JsonNode v, string what)
    {
        switch (f.Kind)
        {
            case "length" or "number":
                var n = Json.Number(v) ?? throw new CommandException($"{what}: {f.Label.ToLowerInvariant()} is a number.");
                if (f.Min is { } lo && n < lo - 1e-9 || f.Max is { } hi && n > hi + 1e-9)
                    throw new CommandException(f.Kind == "length"
                        ? $"{what}: {f.Label.ToLowerInvariant()} is between {Mm(f.Min ?? 0)} and {Mm(f.Max ?? 0)} mm."
                        : $"{what}: {f.Label.ToLowerInvariant()} is between {f.Min} and {f.Max}.");
                return f.Kind == "number" && f.Key != "share" ? Math.Round(n) : Math.Round(n, 4);
            case "choice":
                var c = (string?)v;
                return c != null && f.Choices!.Contains(c) ? c : throw new CommandException($"{what}, {f.Label.ToLowerInvariant()}: one of {string.Join(", ", f.Choices!)}.");
            case "bool":
                return v is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : throw new CommandException($"{what}: {f.Label.ToLowerInvariant()} is on or off.");
            case "ratios":
                if (v is not JsonArray list || list.Count == 0) throw new CommandException($"{what}: the drawers' ratios are a list of numbers.");
                var o = new JsonArray();
                foreach (var x in list)
                {
                    var r = Json.Number(x) ?? 0;
                    if (r <= 0 || r > 20) throw new CommandException($"{what}: a drawer's ratio is a number over 0, up to 20.");
                    o.Add((JsonNode)Math.Round(r, 4));
                }
                return o;
        }
        return null;
    }

    /// <summary>A list of carcasses checked and cleaned: each a box or a gap, its width and height in range, its zones' fields among their choices and within their ranges, nothing it does not have.</summary>
    public static JsonArray Normalise(JsonNode? given)
    {
        var o = new JsonArray();
        if (given == null) return o;
        if (given is not JsonArray list) throw new CommandException("A piece's carcasses are a list.");
        var i = 0;
        foreach (var item in list)
        {
            i++;
            if (item is not JsonObject c) throw new CommandException("A carcass is an object with its zones.");
            var what = $"Carcass {i}";
            var clean = new JsonObject();
            var gap = (string?)c["kind"] == "gap";
            foreach (var (key, v) in c)
            {
                if (v == null) continue;
                if (key == "zones")
                {
                    if (gap) continue;
                    if (v is not JsonArray zs) throw new CommandException($"{what}: its zones are a list.");
                    var zones = new JsonArray();
                    var k = 0;
                    foreach (var zn in zs)
                    {
                        k++;
                        if (zn is not JsonObject z) throw new CommandException($"{what}: a zone is an object with its front.");
                        var zw = $"{what}, zone {k}";
                        var cz = new JsonObject();
                        foreach (var (zk, zv) in z)
                        {
                            if (zv == null) continue;
                            var f = ZoneFields.FirstOrDefault(x => x.Key == zk) ?? throw new CommandException($"A zone has no „{zk}”.");
                            cz[zk] = Checked(f, zv, zw);
                        }
                        cz["front"] ??= "doors";
                        if (cz["ratios"] is JsonArray rs && rs.Count != (int)(Json.Number(cz["drawers"]) ?? 3))
                            throw new CommandException($"{zw}: as many ratios as drawers ({(int)(Json.Number(cz["drawers"]) ?? 3)}).");
                        zones.Add((JsonNode)cz);
                    }
                    clean["zones"] = zones;
                    continue;
                }
                var cf = CarcassFields.FirstOrDefault(x => x.Key == key) ?? throw new CommandException($"A carcass has no „{key}”.");
                if (gap && key == "height") continue;
                clean[key] = Checked(cf, v, what);
            }
            if ((string?)clean["kind"] == "box") clean.Remove("kind");
            o.Add((JsonNode)clean);
        }
        return o;
    }

    /// <summary>The configurator's catalog: the presets with their values, the styles, the fields of a zone and of a carcass.</summary>
    public static JsonObject Catalog()
    {
        static JsonArray Fields(IEnumerable<UnitField> fs) => new([.. fs.Select(f => (JsonNode)new JsonObject
        {
            ["key"] = f.Key, ["label"] = f.Label, ["kind"] = f.Kind, ["min"] = f.Min, ["max"] = f.Max,
            ["choices"] = f.Choices is { } c ? new JsonArray([.. c.Select(x => (JsonNode)x)]) : null,
            ["when"] = f.When is { } w ? new JsonObject { ["key"] = w.Key, ["values"] = new JsonArray([.. w.Values.Select(x => (JsonNode)x)]) } : null,
        })]);
        return new JsonObject
        {
            ["presets"] = new JsonArray([.. Presets.Select(p => (JsonNode)new JsonObject
            {
                ["key"] = p.Key, ["label"] = p.Label, ["predefined"] = p.Predefined, ["objectType"] = p.ObjectType, ["spec"] = Preset(p.Key),
            })]),
            ["styles"] = new JsonArray([.. Styles.Select(s => (JsonNode)new JsonObject { ["key"] = s.Key, ["label"] = s.Label, ["patch"] = s.Patch.DeepClone() })]),
            ["zoneFields"] = Fields(ZoneFields),
            ["carcassFields"] = Fields(CarcassFields),
        };
    }

    // ---- Building ----

    public static void Build(Builder b, Spec s)
    {
        var w = new Work(b, s);
        if (s.Str("form") == "bed") w.Bed(); else w.Cabinet();
    }

    /// <summary>A zone read from its object, its fields under the zone's defaults.</summary>
    sealed record Zone(JsonObject O)
    {
        public string Front => (string?)O["front"] ?? "doors";
        public double? Height => Json.Number(O["height"]);
        public double Share => Json.Number(O["share"]) ?? 1;
        public int Int(string k, int d) => (int)(Json.Number(O[k]) ?? d);
        public bool Bool(string k) => O[k] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        public string Str(string k, string d) => (string?)O[k] ?? d;
        /// <summary>Shelves are adjustable unless the zone says they are fixed.</summary>
        public bool Adjustable => O["adjustable"] is not JsonValue v || !v.TryGetValue<bool>(out var b) || b;
    }

    sealed class Work(Builder b, Spec s)
    {
        const string Sym = "symbol";
        readonly double W = s.W, Dp = s.D, H = s.H;
        readonly double t = s.Num("carcassBoard", 0.018), tb = s.Num("backBoard", 0.008), ts = s.Num("shelfBoard", 0.018), tf = s.Num("frontBoard", 0.018);
        readonly string handles = s.Str("handles", "bar"), style = s.Str("frontStyle", "slab"), baseMode = s.Str("base", "legs"), topMode = s.Str("top", "none");
        double zBottom, zb, zt, tt, ov, yF, yC, yBoardBack, yInnerBack;
        string pk = Sym;
        int sliding;
        bool SeparateTop => topMode != "none";

        static double R4(double v) => Math.Round(v, 4);

        /// <summary>A board's sizes into the cut list: its thickness along one axis, its length along the grain, its width the third.</summary>
        void Record(string part, int carcass, string role, double dx, double dy, double dz, char thick, char grain, bool front)
        {
            double Of(char a) => a switch { 'x' => dx, 'y' => dy, _ => dz };
            var other = "xyz".First(ch => ch != thick && ch != grain);
            b.Boards.Add(new BoardRow(part, carcass, role, R4(Of(grain)), R4(Of(other)), R4(Of(thick)), grain.ToString(), front));
        }

        void Board(string part, int carcass, string role, double x0, double x1, double y0, double y1, double z0, double z1, char thick, char grain, bool front = false, double r = 0)
        {
            if (x1 - x0 < 1e-4 || y1 - y0 < 1e-4 || z1 - z0 < 1e-4) return;
            b.Block(role, x0, x1, y0, y1, z0, z1, r);
            Record(part, carcass, role, x1 - x0, y1 - y0, z1 - z0, thick, grain, front);
        }

        void Frame()
        {
            ov = s.Num("overhang", 0.01);
            tt = s.Num("topThickness", 0.025);
            zBottom = baseMode == "floating" ? s.Num("mount", 0.3) : 0;
            var top = zBottom + H;
            zb = zBottom + baseMode switch { "plinth" => s.Num("plinthHeight", 0.1), "legs" => s.Num("legHeight", 0.15), _ => 0 };
            zt = top - (SeparateTop ? tt : 0);
            if (zt - zb < 0.1) { b.Warnings.Add("The piece is too low for its base and top: its body is at least 100 mm high."); zt = zb + 0.1; }
            sliding = s.Int("sliding", 0);
            yF = -Dp / 2;
            yC = yF + (sliding > 0 ? 2 * tf + 0.014 : tf);
            var inset = s.Str("back", "inset") == "inset";
            yBoardBack = inset ? Dp / 2 : Dp / 2 - tb;
            yInnerBack = inset ? Dp / 2 - 0.01 - tb : Dp / 2 - tb;
            // Hung above the plan's cut, it is drawn as what is overhead.
            pk = zBottom >= 1.2 ? "overhead" : Sym;
        }

        // ---- A cabinet: carcasses side by side ----

        public void Cabinet()
        {
            Frame();
            var items = (s.List(CarcassesKey) ?? []).OfType<JsonObject>().ToList();
            if (items.Count == 0) items = [new JsonObject { ["zones"] = new JsonArray(new JsonObject { ["front"] = "doors" }) }];
            double fixedW = items.Sum(c => Json.Number(c["width"]) ?? 0);
            var autos = items.Count(c => Json.Number(c["width"]) == null);
            var auto = autos > 0 ? Math.Max(0.1, (W - fixedW) / autos) : 0;
            var x = -W / 2;
            var idx = 0;
            var spans = new List<(double A, double E)>();
            var tops = new List<(double A, double E)>();
            var gaps = new List<(int I, double A, double E)>();
            foreach (var c in items)
            {
                var cw = Json.Number(c["width"]) ?? auto;
                if (x + cw > W / 2 + 1e-6)
                {
                    b.Warnings.Add($"The carcasses need {Mm(fixedW + autos * Math.Max(0.1, auto))} mm; those past the piece's {Mm(W)} are left out: widen it (Fit width) or narrow them.");
                    break;
                }
                idx++;
                double a = x, e = x + cw;
                x = e;
                b.Carcasses.Add((idx, R4(a), R4(e)));
                if ((string?)c["kind"] == "gap") { gaps.Add((idx, a, e)); tops.Add((a, e)); continue; }
                var ct = Json.Number(c["height"]) is { } ch ? Math.Min(zt, zb + ch) : zt;
                Box(idx, a, e, ct);
                Zones(idx, a, e, ct, c["zones"] as JsonArray);
                spans.Add((a, e));
                if (ct >= zt - 1e-6) tops.Add((a, e));
                if (idx > 1) b.Line(pk, (a, yC), (a, Dp / 2));
            }
            // What the carcasses leave of the width: a filler panel at the right end.
            if (W / 2 - x > 1e-4)
            {
                Board("Filler", 0, "fronts", x, W / 2, yF, Dp / 2, zb, zt, 'x', 'z', true);
                spans.Add((x, W / 2));
                tops.Add((x, W / 2));
            }
            // A knee space at an end of the piece: a gable under the top there.
            foreach (var (gi, a, e) in gaps)
            {
                if (a <= -W / 2 + 1e-6) Board("Gable", gi, "body", a, a + t, yF, Dp / 2, zBottom, zt, 'x', 'z');
                if (e >= x - 1e-6 && e >= W / 2 - 1e-4) Board("Gable", gi, "body", e - t, e, yF, Dp / 2, zBottom, zt, 'x', 'z');
            }
            if (gaps.Count > 0 && !SeparateTop) b.Warnings.Add("A knee space needs a top over it: choose an overhanging top or a worktop.");
            if (sliding > 0) Sliding(-W / 2, x);
            if (SeparateTop) foreach (var (a, e) in Merge(tops)) Top(a, e);
            Base(Merge(spans));
            // The plan: the footprint, the fronts' line, the top's outline when it overhangs.
            b.PlanRect(pk, -W / 2, W / 2, yF, Dp / 2);
            if (sliding == 0) b.Line(pk, (-W / 2, yC), (x, yC));
            if (SeparateTop && ov > 1e-4)
                foreach (var (a, e) in Merge(tops))
                    b.PlanRect(pk, a <= -W / 2 + 1e-6 && topMode == "overhang" ? a - ov : a, e >= W / 2 - 1e-6 && topMode == "overhang" ? e + ov : e, yF - ov, Dp / 2);
        }

        static List<(double A, double E)> Merge(List<(double A, double E)> spans)
        {
            var o = new List<(double A, double E)>();
            foreach (var sp in spans.OrderBy(x => x.A))
                if (o.Count > 0 && sp.A <= o[^1].E + 1e-6) o[^1] = (o[^1].A, Math.Max(o[^1].E, sp.E));
                else o.Add(sp);
            return o;
        }

        /// <summary>A carcass over a…e from its bottom to <paramref name="ct"/>: sides, bottom, top (between the sides or over them), the back set in or laid on.</summary>
        void Box(int i, double a, double e, double ct)
        {
            double y0 = yC, y1 = yBoardBack;
            var basin = !SeparateTop && s.Bool("basin") && a < -0.1 && e > 0.1;
            if (s.Str("topOn", "between") == "between")
            {
                Board("Side", i, "body", a, a + t, y0, y1, zb, ct, 'x', 'z');
                Board("Side", i, "body", e - t, e, y0, y1, zb, ct, 'x', 'z');
                Board("Bottom", i, "body", a + t, e - t, y0, y1, zb, zb + t, 'z', 'x');
                TopBoard(i, a + t, e - t, y0, y1, ct - t, ct, basin);
            }
            else
            {
                Board("Bottom", i, "body", a, e, y0, y1, zb, zb + t, 'z', 'x');
                TopBoard(i, a, e, y0, y1, ct - t, ct, basin);
                Board("Side", i, "body", a, a + t, y0, y1, zb + t, ct - t, 'x', 'z');
                Board("Side", i, "body", e - t, e, y0, y1, zb + t, ct - t, 'x', 'z');
            }
            if (s.Str("back", "inset") == "inset") Board("Back", i, "back", a + t, e - t, yInnerBack, yInnerBack + tb, zb + t, ct - t, 'y', 'z');
            else Board("Back", i, "back", a, e, Dp / 2 - tb, Dp / 2, zb, ct, 'y', 'z');
        }

        void TopBoard(int i, double x0, double x1, double y0, double y1, double z0, double z1, bool basin)
        {
            if (!basin) { Board("Top", i, "body", x0, x1, y0, y1, z0, z1, 'z', 'x'); return; }
            var hole = BasinHole(x0, x1, y0, y1);
            b.Slab("body", x0, x1, y0, y1, z0, z1, hole);
            Record("Top", i, "body", x1 - x0, y1 - y0, z1 - z0, 'z', 'x', false);
            BasinSymbol(hole, y1, z1);
        }

        (double, double, double, double) BasinHole(double x0, double x1, double y0, double y1)
        {
            double hw = Math.Min(0.25, (x1 - x0) / 2 - 0.08), hd = Math.Min(0.19, (y1 - y0) / 2 - 0.06), yc = (y0 + y1) / 2 - 0.02;
            return (-hw, hw, yc - hd, yc + hd);
        }

        /// <summary>The basin's placeholder: its outline and its tap in plan, the tap on the top.</summary>
        void BasinSymbol((double X0, double X1, double Y0, double Y1) h, double yBack, double z)
        {
            b.PlanRect(pk, h.X0, h.X1, h.Y0, h.Y1, 0.04);
            b.PlanRect(pk, h.X0 + 0.025, h.X1 - 0.025, h.Y0 + 0.025, h.Y1 - 0.025, 0.06);
            var ty = (h.Y1 + yBack) / 2;
            b.Circle(pk, 0, ty, 0.02, 12);
            b.Cylinder("metal", 0, ty, 0.016, z, z + 0.22, 10);
        }

        /// <summary>The separate top over a…e: overhanging at the piece's ends (an overhanging top) and in front (both), its basin cut out.</summary>
        void Top(double a, double e)
        {
            double x0 = a <= -W / 2 + 1e-6 && topMode == "overhang" ? a - ov : a, x1 = e >= W / 2 - 1e-6 && topMode == "overhang" ? e + ov : e;
            double y0 = yF - ov, y1 = Dp / 2;
            if (s.Bool("basin") && a < -0.1 && e > 0.1)
            {
                var hole = BasinHole(x0, x1, y0, y1);
                b.Slab("top", x0, x1, y0, y1, zt, zt + tt, hole);
                Record("Top", 0, "top", x1 - x0, y1 - y0, tt, 'z', 'x', false);
                BasinSymbol(hole, y1, zt + tt);
            }
            else Board("Top", 0, "top", x0, x1, y0, y1, zt, zt + tt, 'z', 'x');
        }

        /// <summary>The base under each run of carcasses: a plinth set back (front and back boards), legs at its ends and every metre between, or nothing.</summary>
        void Base(List<(double A, double E)> runs)
        {
            if (zb - zBottom < 1e-4) return;
            foreach (var (r0, r1) in runs)
            {
                if (baseMode == "plinth")
                {
                    var rec = s.Num("plinthRecess", 0.05);
                    Board("Plinth", 0, "plinth", r0, r1, yF + rec, yF + rec + t, zBottom, zb, 'y', 'x');
                    Board("Plinth", 0, "plinth", r0, r1, Dp / 2 - 0.05 - t, Dp / 2 - 0.05, zBottom, zb, 'y', 'x');
                    continue;
                }
                if (baseMode != "legs") continue;
                var kind = s.Str("legs", "metal");
                var inset = kind == "sled" ? 0.03 : 0.04;
                var len = r1 - r0 - 2 * inset;
                var n = Math.Max(1, (int)Math.Ceiling(len / 1.0 - 1e-6));
                for (var k = 0; k <= n; k++)
                {
                    var lx = r0 + inset + len * k / n;
                    if (kind == "sled")
                    {
                        // A flat bar frame across the depth: on the floor, under the body, two uprights.
                        double fy0 = yC + 0.02, fy1 = Dp / 2 - 0.02;
                        b.Block("legs", lx - 0.006, lx + 0.006, fy0, fy1, zBottom, zBottom + 0.012);
                        b.Block("legs", lx - 0.006, lx + 0.006, fy0, fy1, zb - 0.012, zb);
                        b.Block("legs", lx - 0.006, lx + 0.006, fy0, fy0 + 0.03, zBottom + 0.012, zb - 0.012);
                        b.Block("legs", lx - 0.006, lx + 0.006, fy1 - 0.03, fy1, zBottom + 0.012, zb - 0.012);
                        continue;
                    }
                    foreach (var ly in new[] { yC + 0.04, Dp / 2 - 0.04 })
                        if (kind == "tapered") b.Frustum("legs", lx, ly, 0.012, 0.02, zBottom, zb);
                        else b.Cylinder("legs", lx, ly, 0.012, zBottom, zb, 8);
                }
            }
        }

        /// <summary>A carcass's zones stacked from its bottom: their heights given or shares of the rest, a divider between each two, each its front and interior.</summary>
        void Zones(int i, double a, double e, double ct, JsonArray? list)
        {
            var zones = (list ?? []).OfType<JsonObject>().Select(o => new Zone(o)).ToList();
            if (zones.Count == 0) zones = [new Zone(new JsonObject { ["front"] = "doors" })];
            var total = ct - zb;
            var fixedH = zones.Sum(z => z.Height ?? 0);
            var shares = zones.Where(z => z.Height == null).Sum(z => z.Share);
            var scale = 1.0;
            if (fixedH > total + 1e-6 || shares == 0 && fixedH < total - 1e-6)
            {
                if (fixedH > total + 1e-6) b.Warnings.Add($"Carcass {i}'s zones need {Mm(fixedH)} mm; it is {Mm(total)}: they are fitted to it.");
                scale = total / fixedH;
            }
            var rest = Math.Max(0, total - fixedH * scale);
            var bounds = new List<double> { zb };
            foreach (var z in zones) bounds.Add(bounds[^1] + (z.Height is { } h ? h * scale : rest * z.Share / shares));
            bounds[^1] = ct;
            for (var k = 0; k < zones.Count; k++)
            {
                double z0 = bounds[k], z1 = bounds[k + 1];
                double ix0 = a + t, ix1 = e - t, iz0 = k == 0 ? zb + t : z0 + ts / 2, iz1 = k == zones.Count - 1 ? ct - t : z1 - ts / 2;
                if (k > 0) Board("Divider", i, "body", ix0, ix1, yC, yInnerBack, z0 - ts / 2, z0 + ts / 2, 'z', 'x');
                Zone(i, zones[k], a + Gap / 2, e - Gap / 2, z0 + Gap / 2, z1 - Gap / 2, ix0, ix1, iz0, iz1);
            }
        }

        void Zone(int i, Zone z, double fx0, double fx1, double fz0, double fz1, double ix0, double ix1, double iz0, double iz1)
        {
            var front = z.Front;
            var open = sliding > 0;
            switch (front)
            {
                case "doors" when !open: Doors(i, z, fx0, fx1, fz0, fz1); break;
                case "drawers":
                    if (open) { InnerDrawers(i, z.Int("drawers", 3), ix0, ix1, iz0, iz1); return; }
                    Drawers(i, z, fx0, fx1, fz0 - Gap / 2, fz1 + Gap / 2, ix0, ix1, iz0, iz1);
                    return;
                case "flap" when !open:
                    var up = z.Str("flap", "down") == "up";
                    FrontPanel("Flap", i, fx0, fx1, fz0, fz1, 'x', false, yF);
                    Handle("flap", fx0, fx1, fz0, fz1, false, up, yF);
                    break;
            }
            // The interior: inner drawers at the bottom (behind doors), a rail, shelves in what is left.
            if (front == "doors" && z.Int("innerDrawers", 0) is var m && m > 0) iz0 = InnerDrawers(i, m, ix0, ix1, iz0, iz1);
            var top = iz1;
            if (front is "open" or "doors" && z.Bool("rail"))
            {
                var rz = iz1 - 0.06;
                var ry = (yC + yInnerBack) / 2;
                b.SweepX("metal", ix0, ix1, [.. Enumerable.Range(0, 8).Select(k => { var an = Math.PI / 8 + k * Math.PI / 4; return (ry + 0.0125 * Math.Cos(an), rz + 0.0125 * Math.Sin(an)); })]);
                if (pk == Sym) b.Line("dashed", (ix0, ry), (ix1, ry));
                // Shelves under the hanging space, when a metre is left under the rail.
                top = rz - 1.0;
            }
            Shelves(i, z.Int("shelves", 0), z.Adjustable, ix0, ix1, iz0, top);
        }

        void Shelves(int i, int n, bool adjustable, double ix0, double ix1, double iz0, double iz1)
        {
            if (n < 1 || iz1 - iz0 < (n + 1) * ts) return;
            for (var k = 1; k <= n; k++)
            {
                var zc = iz0 + (iz1 - iz0) * k / (n + 1);
                if (adjustable) Board("Shelf (adjustable)", i, "body", ix0 + 0.001, ix1 - 0.001, yC + 0.02, yInnerBack, zc - ts / 2, zc + ts / 2, 'z', 'x');
                else Board("Shelf (fixed)", i, "body", ix0, ix1, yC, yInnerBack, zc - ts / 2, zc + ts / 2, 'z', 'x');
            }
        }

        /// <summary>Drawers behind doors from the inside's bottom: each a front of the carcass's board and its box; the height left over them.</summary>
        double InnerDrawers(int i, int m, double ix0, double ix1, double iz0, double iz1)
        {
            const double h = 0.16;
            var z = iz0;
            for (var k = 0; k < m && z + h <= iz1 + 1e-6; k++)
            {
                // Clear of the hinges: 30 mm in from either side, behind the doors.
                Board("Inner drawer front", i, "body", ix0 + 0.03, ix1 - 0.03, yC + 0.025, yC + 0.025 + t, z + 0.005, z + h - 0.005, 'y', 'x');
                DrawerBox(i, ix0 + 0.03 - 0.013, ix1 - 0.03 + 0.013, z + 0.02, h - 0.05, yC + 0.025 + t);
                z += h + 0.005;
            }
            return z;
        }

        /// <summary>One door or more over a zone, each with its handle by the edge it opens at; a pair from the middle; their swings in plan at the finest detail.</summary>
        void Doors(int i, Zone z, double fx0, double fx1, double fz0, double fz1)
        {
            var n = Math.Clamp(z.Int("doors", fx1 - fx0 > 0.6 + 1e-6 ? 2 : 1), 1, 4);
            var hingeLeft = z.Str("hand", "left") != "right";
            var lw = (fx1 - fx0) / n;
            for (var k = 0; k < n; k++)
            {
                double l0 = fx0 + k * lw + (k > 0 ? Gap / 2 : 0), l1 = fx0 + (k + 1) * lw - (k < n - 1 ? Gap / 2 : 0);
                var left = n == 1 ? hingeLeft : k < n / 2.0;
                FrontPanel("Door", i, l0, l1, fz0, fz1, 'z', z.Bool("glazed"), yF);
                Handle("door", l0, l1, fz0, fz1, left, false, yF);
                if (fz0 >= 1.2 || pk != Sym) continue;
                var hinge = left ? l0 : l1;
                var dir = left ? 1 : -1;
                var r = l1 - l0;
                b.LineFrom(PlanDetail.Fine, "dashed", Enumerable.Range(0, 9).Select(q => { var an = q * Math.PI / 16; return (hinge + dir * r * Math.Cos(an), yF - r * Math.Sin(an)); }));
                b.LineFrom(PlanDetail.Fine, "dashed", [(hinge, yF), (hinge, yF - r)]);
            }
        }

        /// <summary>Drawers over a zone (z0…z1 its bounds), from the top down by their ratios, each a front and a box behind it.</summary>
        void Drawers(int i, Zone z, double fx0, double fx1, double z0, double z1, double ix0, double ix1, double iz0, double iz1)
        {
            var n = Math.Clamp(z.Int("drawers", 3), 1, 6);
            var ratios = z.O["ratios"] is JsonArray rs && rs.Count == n ? rs.Select(x => Json.Number(x) ?? 1).ToArray() : Enumerable.Repeat(1.0, n).ToArray();
            var sum = ratios.Sum();
            var top = z1;
            foreach (var r in ratios)
            {
                var bottom = top - (z1 - z0) * r / sum;
                double a = bottom + Gap / 2, c = top - Gap / 2;
                FrontPanel("Drawer front", i, fx0, fx1, a, c, 'x', false, yF);
                Handle("drawer", fx0, fx1, a, c, false, false, yF);
                var bz0 = Math.Max(a + 0.02, iz0 + 0.005);
                var bh = Math.Min(c - 0.04, iz1 - 0.005) - bz0;
                if (bh >= 0.04) DrawerBox(i, ix0, ix1, bz0, bh, yC);
                top = bottom;
            }
        }

        /// <summary>A drawer's box between the inside's sides (13 mm either side for its runners): two sides, two ends, a bottom; as deep as the runners' 50 mm steps allow.</summary>
        void DrawerBox(int i, double ix0, double ix1, double z0, double h, double y)
        {
            const double bt = 0.016;
            double ox0 = ix0 + 0.013, ox1 = ix1 - 0.013;
            var room = yInnerBack - y - 0.01;
            var depth = room >= 0.27 ? Math.Floor(room / 0.05 + 1e-6) * 0.05 : room;
            if (depth < 0.1 || ox1 - ox0 < 0.1) return;
            double y0 = y + 0.002, y1 = y0 + depth;
            Board("Drawer side", i, "body", ox0, ox0 + bt, y0, y1, z0, z0 + h, 'x', 'y');
            Board("Drawer side", i, "body", ox1 - bt, ox1, y0, y1, z0, z0 + h, 'x', 'y');
            Board("Drawer end", i, "body", ox0 + bt, ox1 - bt, y0, y0 + bt, z0, z0 + h, 'y', 'x');
            Board("Drawer end", i, "body", ox0 + bt, ox1 - bt, y1 - bt, y1, z0, z0 + h, 'y', 'x');
            Board("Drawer bottom", i, "back", ox0 + bt, ox1 - bt, y0 + bt, y1 - bt, z0, z0 + tb, 'z', 'x');
        }

        /// <summary>
        /// A front over x0…x1, z0…z1 standing at <paramref name="y0"/>: a slab, fluted (its face grooved upright), or a
        /// frame of stiles and rails round a panel set back (glass when glazed); a J-profile grip milled along its top
        /// edge (its bottom edge when it is above eye height).
        /// </summary>
        void FrontPanel(string part, int i, double x0, double x1, double z0, double z1, char grain, bool glazed, double y0)
        {
            double w = x1 - x0, h = z1 - z0, y1 = y0 + tf;
            if (w < 0.01 || h < 0.01) return;
            if ((glazed || style == "framed") && w >= 0.15 && h >= 0.15)
            {
                var fw = glazed ? 0.05 : Math.Min(0.07, Math.Min(w, h) / 4);
                Board($"{part} stile", i, "fronts", x0, x0 + fw, y0, y1, z0, z1, 'y', 'z', true, 0.001);
                Board($"{part} stile", i, "fronts", x1 - fw, x1, y0, y1, z0, z1, 'y', 'z', true, 0.001);
                Board($"{part} rail", i, "fronts", x0 + fw, x1 - fw, y0, y1, z0, z0 + fw, 'y', 'x', true);
                Board($"{part} rail", i, "fronts", x0 + fw, x1 - fw, y0, y1, z1 - fw, z1, 'y', 'x', true);
                if (glazed) Board($"{part} glass", i, "glass", x0 + fw, x1 - fw, y0 + tf / 2 - 0.002, y0 + tf / 2 + 0.002, z0 + fw, z1 - fw, 'y', 'z', true);
                else Board($"{part} panel", i, "fronts", x0 + fw, x1 - fw, y0 + tf * 0.4, y1, z0 + fw, z1 - fw, 'y', 'z', true);
                return;
            }
            Record(part, i, "fronts", w, tf, h, 'y', grain, true);
            var j = handles == "jprofile" && h > 0.06;
            var high = (z0 + z1) / 2 > 1.3;
            const double g = 0.022;
            double m0 = j && high ? z0 + g : z0, m1 = j && !high ? z1 - g : z1;
            if (style == "fluted" && w >= 0.06)
            {
                // Upright grooves 14 wide, 4 deep, every 30 mm, centred on the front.
                const double pitch = 0.03, half = 0.007, depth = 0.004;
                var n = Math.Max(1, (int)Math.Floor((w - 0.02) / pitch));
                var margin = (w - n * pitch) / 2;
                var pts = new List<(double X, double Y)> { (x0, y1), (x0, y0) };
                for (var k = 0; k < n; k++)
                {
                    var cx = x0 + margin + pitch * (k + 0.5);
                    pts.AddRange([(cx - half, y0), (cx - half, y0 + depth), (cx + half, y0 + depth), (cx + half, y0)]);
                }
                pts.AddRange([(x1, y0), (x1, y1)]);
                b.PlanPrism("fronts", pts, m0, m1);
            }
            else b.Block("fronts", x0, x1, y0, y1, m0, m1, 0.002);
            // The grip: the front's edge set back to a lip, a finger's room behind it.
            if (j) b.Block("fronts", x0, x1, y0 + tf * 0.6, y1, high ? z0 : z1 - g, high ? z0 + g : z1, 0.002);
        }

        /// <summary>A front's handle by its style: a bar, a knob, an edge pull; none for push to open or a J-profile.</summary>
        void Handle(string kind, double x0, double x1, double z0, double z1, bool opensRight, bool flapUp, double y)
        {
            if (handles is "none" or "jprofile") return;
            double w = x1 - x0, h = z1 - z0;
            const string R = "handles";
            if (kind == "door")
            {
                var hx = opensRight ? x1 - 0.035 : x0 + 0.035;
                // A tall door's handle at a metre; a low one's near its top, a high one's near its bottom.
                var hz = h > 1.2 ? Math.Clamp(1.0, z0 + 0.2, z1 - 0.2) : z0 >= 1.2 ? z0 + 0.08 : z1 - 0.08;
                switch (handles)
                {
                    case "bar": { var l = h > 1.2 ? Math.Min(0.3, h / 4) : Math.Min(0.16, h / 3); b.Block(R, hx - 0.006, hx + 0.006, y - 0.028, y, hz - l / 2, hz + l / 2); break; }
                    case "knob": b.Block(R, hx - 0.0125, hx + 0.0125, y - 0.025, y, hz - 0.0125, hz + 0.0125, 0.006); break;
                    case "edge":
                        if (h > 1.2) b.Block(R, opensRight ? x1 - 0.012 : x0, opensRight ? x1 : x0 + 0.012, y - 0.012, y, hz - 0.15, hz + 0.15);
                        else { var ex = opensRight ? x1 - 0.11 : x0 + 0.01; b.Block(R, ex, ex + 0.1, y - 0.012, y, z0 >= 1.2 ? z0 : z1 - 0.03, z0 >= 1.2 ? z0 + 0.03 : z1); }
                        break;
                }
                return;
            }
            var mid = (x0 + x1) / 2;
            var at = flapUp ? z0 + Math.Min(0.04, h / 3) : z1 - Math.Min(0.04, h / 3);
            switch (handles)
            {
                case "bar": { var l = Math.Min(0.16, w / 2); b.Block(R, mid - l / 2, mid + l / 2, y - 0.028, y, at - 0.006, at + 0.006); break; }
                case "knob": b.Block(R, mid - 0.0125, mid + 0.0125, y - 0.025, y, at - 0.0125, at + 0.0125, 0.006); break;
                case "edge": b.Block(R, mid - 0.06, mid + 0.06, y - 0.012, y, flapUp ? z0 : z1 - 0.03, flapUp ? z0 + 0.03 : z1); break;
            }
        }

        /// <summary>Sliding doors across the carcasses on two tracks, each overlapping the next by 30 mm, every other one in front; their handles at their free edges.</summary>
        void Sliding(double x0, double x1)
        {
            var n = Math.Clamp(sliding, 1, 6);
            var span = x1 - x0;
            var dw = (span + 0.03 * (n - 1)) / n;
            double z0 = zb + Gap / 2, z1 = zt - Gap / 2;
            for (var k = 0; k < n; k++)
            {
                var a = x0 + k * (dw - 0.03);
                var y0 = k % 2 == 0 ? yF : yF + tf + 0.006;
                FrontPanel("Sliding door", 0, a, a + dw, z0, z1, 'z', false, y0);
                var y = k % 2 == 0 ? y0 : yF;
                if (n == 1 || k == 0) Handle("door", a, a + dw, z0, z1, false, false, y);
                else if (k == n - 1) Handle("door", a, a + dw, z0, z1, true, false, y);
                else Handle("door", a + dw / 2 - 0.035, a + dw / 2 + 0.035, z0, z1, false, false, y);
            }
            b.Line(pk, (x0, yF + tf / 2), (x1, yF + tf / 2));
            b.Line(pk, (x0, yF + tf * 1.5 + 0.006), (x1, yF + tf * 1.5 + 0.006));
        }

        // ---- A bed ----

        /// <summary>A bed of boards: its side rails, foot board and headboard (as high as the type), a base board for the mattress, a centre rail, drawers under it along its sides.</summary>
        public void Bed()
        {
            double mh = s.Num("mattressHeight", 0.5), rt = Math.Max(0.15, mh - 0.2);
            int n = s.Int("bedDrawers", 0);
            var on = s.Str("drawersOn", "both");
            bool dl = n > 0 && on is "both" or "left", dr = n > 0 && on is "both" or "right";
            double x0 = -W / 2, x1 = W / 2, y0 = -Dp / 2, y1 = Dp / 2;
            const double dz0 = 0.03;
            var dz1 = rt - 0.06;
            Board("Headboard", 0, "body", x0, x1, y1 - t, y1, 0, H, 'y', 'x');
            Board("Foot board", 0, "body", x0, x1, y0, y0 + t, 0, rt, 'y', 'x');
            Board("Side rail", 0, "body", x0, x0 + t, y0 + t, y1 - t, dl ? dz1 : 0, rt, 'x', 'y');
            Board("Side rail", 0, "body", x1 - t, x1, y0 + t, y1 - t, dr ? dz1 : 0, rt, 'x', 'y');
            var baseTop = rt - 0.03;
            Board("Bed base", 0, "back", x0 + t, x1 - t, y0 + t, y1 - t, baseTop - ts, baseTop, 'z', 'y');
            if (W >= 1.2) Board("Centre rail", 0, "body", -t / 2, t / 2, y0 + t, y1 - t, 0, baseTop - ts, 'x', 'y');
            // The mattress in the frame, 30 mm down in it, and its pillows.
            b.Block("linen", x0 + t + 0.005, x1 - t - 0.005, y0 + t + 0.005, y1 - t - 0.005, baseTop, mh, 0.04);
            var pillows = W >= 1.2 ? 2 : 1;
            var pw = (W - 2 * t - 0.08) / pillows;
            for (var k = 0; k < pillows; k++)
            {
                double p0 = x0 + t + 0.04 + k * pw + 0.02, p1 = x0 + t + 0.04 + (k + 1) * pw - 0.02;
                b.Block("linen", p0, p1, y1 - t - 0.5, y1 - t - 0.06, mh, mh + 0.12, 0.07);
                b.PlanRect(Sym, p0, p1, y1 - t - 0.5, y1 - t - 0.06, 0.07);
            }
            if (dz1 - dz0 > 0.08)
            {
                if (dl) SideDrawers(n, x0, 1, y0 + t, y1 - t, dz0, dz1);
                if (dr) SideDrawers(n, x1, -1, y0 + t, y1 - t, dz0, dz1);
            }
            else if (n > 0) b.Warnings.Add("The bed is too low for drawers under it: its mattress top at 450 mm at least.");
            b.PlanRect(Sym, x0, x1, y0, y1);
            b.Line(Sym, (x0, y1 - t), (x1, y1 - t));
            b.PlanRect(Sym, x0 + t, x1 - t, y0 + t, y1 - t);
        }

        /// <summary>Drawers along a bed's side, their fronts in the side's plane (at <paramref name="xs"/>, facing out along −<paramref name="inward"/>), their boxes under the base.</summary>
        void SideDrawers(int n, double xs, int inward, double ya, double yb, double z0, double z1)
        {
            ya += 0.1;
            yb -= 0.1;
            var len = (yb - ya) / n;
            // The boxes reach in to just short of the centre rail.
            var reach = Math.Min(0.6, W / 2 - t - 0.02 - t / 2);
            for (var k = 0; k < n; k++)
            {
                double a = ya + k * len + Gap / 2, e = ya + (k + 1) * len - Gap / 2;
                double f0 = inward > 0 ? xs : xs - tf, f1 = inward > 0 ? xs + tf : xs;
                b.Block("fronts", f0, f1, a, e, z0, z1, 0.002);
                Record("Drawer front", 0, "fronts", tf, e - a, z1 - z0, 'x', 'y', true);
                var mid = (a + e) / 2;
                var hz = z1 - Math.Min(0.04, (z1 - z0) / 3);
                double ho0 = inward > 0 ? xs - 0.028 : xs, ho1 = inward > 0 ? xs : xs + 0.028;
                switch (handles)
                {
                    case "bar": b.Block("handles", ho0, ho1, mid - 0.08, mid + 0.08, hz - 0.006, hz + 0.006); break;
                    case "knob": b.Block("handles", inward > 0 ? xs - 0.025 : xs, inward > 0 ? xs : xs + 0.025, mid - 0.0125, mid + 0.0125, hz - 0.0125, hz + 0.0125, 0.006); break;
                    case "edge": b.Block("handles", inward > 0 ? xs - 0.012 : xs, inward > 0 ? xs : xs + 0.012, mid - 0.06, mid + 0.06, z1 - 0.03, z1); break;
                }
                // The box behind it, across the bed.
                const double bt = 0.016;
                double bx0 = inward > 0 ? f1 + 0.002 : f0 - 0.002 - reach, bx1 = bx0 + reach;
                double by0 = a + 0.013, by1 = e - 0.013, bz0 = z0 + 0.01, bz1 = z1 - 0.02;
                if (bz1 - bz0 < 0.04 || by1 - by0 < 0.1) continue;
                Board("Drawer side", 0, "body", bx0, bx1, by0, by0 + bt, bz0, bz1, 'y', 'x');
                Board("Drawer side", 0, "body", bx0, bx1, by1 - bt, by1, bz0, bz1, 'y', 'x');
                Board("Drawer end", 0, "body", bx0, bx0 + bt, by0 + bt, by1 - bt, bz0, bz1, 'x', 'y');
                Board("Drawer end", 0, "body", bx1 - bt, bx1, by0 + bt, by1 - bt, bz0, bz1, 'x', 'y');
                Board("Drawer bottom", 0, "back", bx0 + bt, bx1 - bt, by0 + bt, by1 - bt, bz0, bz0 + tb, 'z', 'y');
            }
        }
    }
}
