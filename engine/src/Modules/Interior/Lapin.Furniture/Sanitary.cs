using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Furniture;

/// <summary>A connection point of a sanitary fixture in its own frame (x across, y to its back, z up from where it stands): what it takes, which way.</summary>
public sealed record SanitaryPort(string Key, string Service, string Flow, Vec3 At, Vec3 Dir, double Size = 0);

/// <summary>A sanitary fixture's connection point in the project's frame, turned and raised with the piece.</summary>
public sealed record PlacedPort(string Key, string Service, string Flow, Vec3 At, Vec3 Dir, double Size);

/// <summary>
/// Sanitary fixtures (forum topic #245, docs/plan-furniture.md, Obiectele sanitare): the furniture module's Sanitary kinds — a WC,
/// a bidet, a wash basin, a vanity unit, a shower, a bath, a kitchen sink and the bathroom accessories. Each is a type of
/// the furniture module (its size, its kind's parameters, its materials by role, a plan symbol of the user's if one is named),
/// placed as furniture is (its back on a wall's face), written to IFC as the class IFC gives it: an <c>IfcSanitaryTerminal</c> of an
/// <c>IfcSanitaryTerminalType</c> with its predefined type (TOILETPAN, BIDET, WASHHANDBASIN, SHOWER, BATH, SINK), through the
/// element's <c>exportAs</c>; an accessory stays <c>IfcFurniture</c>, USERDEFINED by its item. A fixture's connection points
/// (cold and hot water in, waste out) are data the IFC export writes as <c>IfcDistributionPort</c>s, with the services the
/// open MEP module names, so a network can join them later. Bodies are simple and recognisable, every curve through
/// <see cref="Chords"/>; the plan symbols follow the usual drafting (the outline, a bowl's or a seat's opening, a drain, the taps,
/// a tray's cross, a door's swing). The common sizes each kind offers are indicative: a manufacturer's are its own.
/// </summary>
public static class Sanitary
{
    public const string Group = "Sanitary";
    public const string SanitaryClass = "IfcSanitaryTerminal";
    public const string AccessoryKey = "bathAccessory";

    // MEP's services (Lapin.Mep.Services), named here so the module needs no MEP.
    public const string ColdWater = "coldWater", HotWater = "hotWater", Drainage = "drainage";

    const string Sym = "symbol";
    const string Ceramic = "ceramic", Seat = "seat", Fittings = "fittings", Screen = "screen", Steel = "steel", MirrorRole = "mirror";

    static readonly Vec3 Back = new(0, 1, 0), Down = new(0, 0, -1);

    static KindParam L(string k, string l, double min = 0, double max = 3) => new(k, l, "length", min, max);
    static KindParam N(string k, string l, double min, double max) => new(k, l, "number", min, max);
    static KindParam C(string k, string l, params string[] c) => new(k, l, "choice", Choices: c);
    static KindParam B(string k, string l) => new(k, l, "bool");
    static JsonObject D(params (string Key, JsonNode? Value)[] v) { var o = new JsonObject(); foreach (var (k, x) in v) o[k] = x; return o; }
    static KindSize S(string label, params (string Key, JsonNode? Value)[] v) => new(label, D(v));

    /// <summary>An accessory's item: its label (its IFC object type), its size and the height of its underside.</summary>
    public sealed record AccessoryItem(string Key, string Label, double Width, double Depth, double Height, double Mount);

    public static readonly IReadOnlyList<AccessoryItem> Items =
    [
        new("towelRail", "Towel rail", 0.60, 0.08, 0.05, 1.10),
        new("grabBar", "Grab bar", 0.60, 0.08, 0.05, 0.80),
        new("mirror", "Mirror", 0.60, 0.02, 0.80, 1.10),
        new("paperHolder", "Toilet roll holder", 0.15, 0.12, 0.12, 0.65),
    ];

    public static AccessoryItem Item(Spec s) => Items.FirstOrDefault(i => i.Key == s.Str("item")) ?? Items[0];

    public static readonly IReadOnlyList<FurnitureKind> Kinds =
    [
        new("toilet", "WC", Group, "TOILETPAN", null, 0.36, 0.54, 0.40,
            [C("mount", "Mounting", "wallHung", "floor"), C("cistern", "Cistern", "concealed", "closeCoupled")],
            D(("mount", "wallHung"), ("cistern", "concealed")), [Ceramic, Seat, Fittings])
        {
            IfcClass = SanitaryClass,
            Sizes =
            [
                S("Wall-hung", ("width", 0.36), ("depth", 0.54), ("height", 0.40), ("mount", "wallHung"), ("cistern", "concealed")),
                S("Compact", ("width", 0.35), ("depth", 0.48), ("height", 0.40), ("mount", "wallHung"), ("cistern", "concealed")),
                S("Accessible", ("width", 0.38), ("depth", 0.70), ("height", 0.48), ("mount", "wallHung"), ("cistern", "concealed")),
                S("Floor-standing", ("width", 0.36), ("depth", 0.56), ("height", 0.40), ("mount", "floor"), ("cistern", "concealed")),
                S("Floor-standing, close-coupled", ("width", 0.37), ("depth", 0.65), ("height", 0.40), ("mount", "floor"), ("cistern", "closeCoupled")),
            ],
        },
        new("bidet", "Bidet", Group, "BIDET", null, 0.36, 0.54, 0.40,
            [C("mount", "Mounting", "wallHung", "floor"), B("tap", "Tap")],
            D(("mount", "wallHung"), ("tap", true)), [Ceramic, Fittings])
        {
            IfcClass = SanitaryClass,
            Sizes =
            [
                S("Wall-hung", ("width", 0.36), ("depth", 0.54), ("height", 0.40), ("mount", "wallHung")),
                S("Compact", ("width", 0.35), ("depth", 0.48), ("height", 0.40), ("mount", "wallHung")),
                S("Floor-standing", ("width", 0.36), ("depth", 0.56), ("height", 0.40), ("mount", "floor")),
            ],
        },
        new("washbasin", "Wash basin", Group, "WASHHANDBASIN", null, 0.60, 0.46, 0.85,
            [C("shape", "Shape", "rectangular", "oval"), C("mount", "Mounting", "wall", "pedestal", "semiPedestal"), C("tap", "Tap", "deck", "wall", "none")],
            D(("shape", "rectangular"), ("mount", "wall"), ("tap", "deck")), [Ceramic, Fittings])
        {
            IfcClass = SanitaryClass,
            Sizes =
            [
                S("Hand-rinse basin", ("width", 0.45), ("depth", 0.30), ("height", 0.85)),
                S("", ("width", 0.55), ("depth", 0.45), ("height", 0.85)),
                S("", ("width", 0.60), ("depth", 0.46), ("height", 0.85)),
                S("", ("width", 0.65), ("depth", 0.48), ("height", 0.85)),
                S("Accessible", ("width", 0.60), ("depth", 0.55), ("height", 0.80), ("mount", "wall")),
            ],
        },
        new("vanity", "Vanity unit", Group, "WASHHANDBASIN", null, 0.80, 0.48, 0.85,
            [C("base", "Base", "wallHung", "floor"), N("drawers", "Drawers", 1, 3), B("tap", "Tap")],
            D(("base", "wallHung"), ("drawers", 2), ("tap", true)), [Ceramic, "body", "fronts", Fittings])
        {
            IfcClass = SanitaryClass,
            Sizes =
            [
                S("", ("width", 0.60), ("depth", 0.46), ("height", 0.85)),
                S("", ("width", 0.80), ("depth", 0.48), ("height", 0.85)),
                S("", ("width", 1.00), ("depth", 0.48), ("height", 0.85)),
                S("", ("width", 1.20), ("depth", 0.48), ("height", 0.85)),
            ],
        },
        new("shower", "Shower", Group, "SHOWER", null, 0.90, 0.90, 2.0,
            [C("shape", "Tray", "rectangular", "quadrant"), L("trayHeight", "Tray height", 0.02, 0.25), C("enclosure", "Enclosure", "none", "door", "sliding", "walkIn")],
            D(("shape", "rectangular"), ("trayHeight", 0.04), ("enclosure", "door")), [Ceramic, Fittings, Screen])
        {
            IfcClass = SanitaryClass,
            Sizes =
            [
                S("", ("width", 0.80), ("depth", 0.80), ("height", 2.0), ("shape", "rectangular")),
                S("", ("width", 0.90), ("depth", 0.90), ("height", 2.0), ("shape", "rectangular")),
                S("", ("width", 1.20), ("depth", 0.80), ("height", 2.0), ("shape", "rectangular"), ("enclosure", "sliding")),
                S("Quadrant", ("width", 0.90), ("depth", 0.90), ("height", 2.0), ("shape", "quadrant"), ("enclosure", "door")),
                S("Walk-in", ("width", 1.40), ("depth", 0.90), ("height", 2.0), ("shape", "rectangular"), ("enclosure", "walkIn"), ("trayHeight", 0.03)),
            ],
        },
        new("bath", "Bath", Group, "BATH", null, 1.70, 0.75, 0.55,
            [B("panel", "Front panel"), C("tap", "Tap", "deck", "wall", "none")],
            D(("panel", true), ("tap", "deck")), [Ceramic, Fittings])
        {
            IfcClass = SanitaryClass,
            Sizes =
            [
                S("", ("width", 1.50), ("depth", 0.70), ("height", 0.55)),
                S("", ("width", 1.60), ("depth", 0.70), ("height", 0.55)),
                S("", ("width", 1.70), ("depth", 0.75), ("height", 0.55)),
                S("", ("width", 1.80), ("depth", 0.80), ("height", 0.58)),
            ],
        },
        new("kitchenSink", "Kitchen sink", Group, "SINK", null, 0.86, 0.50, 0.90,
            [N("bowls", "Bowls", 1, 2), C("drainer", "Drainer", "none", "left", "right"), B("tap", "Tap")],
            D(("bowls", 1), ("drainer", "right"), ("tap", true)), [Steel, Fittings])
        {
            IfcClass = SanitaryClass,
            Sizes =
            [
                S("Single bowl", ("width", 0.60), ("depth", 0.50), ("height", 0.90), ("bowls", 1), ("drainer", "none")),
                S("Single bowl and drainer", ("width", 0.86), ("depth", 0.50), ("height", 0.90), ("bowls", 1), ("drainer", "right")),
                S("Double bowl and drainer", ("width", 1.16), ("depth", 0.50), ("height", 0.90), ("bowls", 2), ("drainer", "right")),
            ],
        },
        new(AccessoryKey, "Bathroom accessory", Group, "USERDEFINED", Items[0].Label, Items[0].Width, Items[0].Depth, Items[0].Height,
            [C("item", "Item", [.. Items.Select(i => i.Key)]), L("mount", "Hung at (its underside)", 0, 2.4)],
            D(("item", Items[0].Key), ("mount", Items[0].Mount)), [Fittings, MirrorRole])
        {
            Sizes =
            [
                S("Towel rail", ("item", "towelRail"), ("width", 0.60), ("depth", 0.08), ("height", 0.05), ("mount", 1.10)),
                S("Grab bar", ("item", "grabBar"), ("width", 0.60), ("depth", 0.08), ("height", 0.05), ("mount", 0.80)),
                S("Grab bar", ("item", "grabBar"), ("width", 0.90), ("depth", 0.08), ("height", 0.05), ("mount", 0.80)),
                S("Mirror", ("item", "mirror"), ("width", 0.60), ("depth", 0.02), ("height", 0.80), ("mount", 1.10)),
                S("Toilet roll holder", ("item", "paperHolder"), ("width", 0.15), ("depth", 0.12), ("height", 0.12), ("mount", 0.65)),
            ],
        },
    ];

    public static bool Is(FurnitureKind k) => k.Group == Group;

    /// <summary>The IFC predefined type and object type of a sanitary kind's type: an accessory's by its item.</summary>
    public static (string Predefined, string? ObjectType) Ifc(Spec s) => s.Kind.Key == AccessoryKey ? ("USERDEFINED", Item(s).Label) : (s.Kind.Predefined, s.Kind.ObjectType);

    /// <summary>
    /// An accessory's item changed: its size and hanging height become the item's, unless the same change gives them.
    /// </summary>
    public static void StartItem(JsonObject spec, JsonObject given)
    {
        if ((string?)given["item"] is not { } key) return;
        var item = Items.FirstOrDefault(i => i.Key == key) ?? throw new CommandException($"An accessory is one of {string.Join(", ", Items.Select(i => i.Key))}.");
        if ((string?)spec["item"] == item.Key) return;
        spec["item"] = item.Key;
        if (given["width"] == null) spec["width"] = item.Width;
        if (given["depth"] == null) spec["depth"] = item.Depth;
        if (given["height"] == null) spec["height"] = item.Height;
        if (given["mount"] == null) spec["mount"] = item.Mount;
    }

    // ---- Shapes, every curve through Chords ----

    static List<(double X, double Y)> Pts(IEnumerable<Vec2> v) => [.. v.Select(p => (p.X, p.Y))];

    /// <summary>A whole ellipse about (cx, cy), semi-axes a across and b along y, counter-clockwise.</summary>
    static List<(double X, double Y)> Ellipse(double cx, double cy, double a, double b)
    {
        var pts = a >= b ? Chords.EllipseArc(new Vec2(cx, cy), new Vec2(a, 0), b / a, 0, 2 * Math.PI, 12) : Chords.EllipseArc(new Vec2(cx, cy), new Vec2(0, b), a / b, 0, 2 * Math.PI, 12);
        pts.RemoveAt(pts.Count - 1);
        return Pts(pts);
    }

    static List<(double X, double Y)> Disc(double cx, double cy, double r) => Pts(Chords.Ring(new Vec2(cx, cy), r));

    /// <summary>A rectangle with its corners rounded by r (each corner's quarter through Chords).</summary>
    static List<(double X, double Y)> Rounded(double x0, double x1, double y0, double y1, double r)
    {
        r = Math.Max(0, Math.Min(r, Math.Min(x1 - x0, y1 - y0) / 2 - 1e-4));
        if (r < 1e-4) return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)];
        var o = new List<(double, double)>();
        (double Cx, double Cy, double A)[] corners = [(x1 - r, y0 + r, -Math.PI / 2), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, Math.PI / 2), (x0 + r, y0 + r, Math.PI)];
        foreach (var (cx, cy, a0) in corners)
        {
            var arc = Chords.Arc(new Vec2(cx, cy), r, a0, Math.PI / 2, 2);
            o.AddRange(arc.Select(p => (p.X, p.Y)));
        }
        return o;
    }

    /// <summary>
    /// A pan's or a bowl's outline against the wall: straight at the back from <paramref name="split"/> to <paramref name="back"/>, a half
    /// ellipse in front of it to <paramref name="front"/>, as wide as <paramref name="w"/>.
    /// </summary>
    static List<(double X, double Y)> Egg(double w, double front, double back, double split)
    {
        var o = new List<(double X, double Y)> { (-w / 2, back) };
        var arc = Chords.EllipseArc(new Vec2(0, split), new Vec2(w / 2, 0), (split - front) / (w / 2), Math.PI, Math.PI, 12);
        o.AddRange(arc.Select(p => (p.X, p.Y)));
        o.Add((w / 2, back));
        return o;
    }

    /// <summary>A quadrant tray's outline: its corner at the back left, its front a quarter ellipse of semi-axes w and d.</summary>
    static List<(double X, double Y)> Quadrant(double x0, double y1, double w, double d)
    {
        var o = new List<(double X, double Y)> { (x0, y1) };
        var arc = Chords.EllipseArc(new Vec2(x0, y1), new Vec2(w, 0), d / w, 0, -Math.PI / 2, 6);
        o.AddRange(arc.Select(p => (p.X, p.Y)));
        return o;
    }

    /// <summary>A shape grown (or shrunk, negative) about the middle of its bounds by <paramref name="by"/> each way, for a bowl's wall round its opening.</summary>
    static List<(double X, double Y)> Grow(List<(double X, double Y)> pts, double by)
    {
        double x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X), y0 = pts.Min(p => p.Y), y1 = pts.Max(p => p.Y);
        double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, hx = (x1 - x0) / 2, hy = (y1 - y0) / 2;
        double kx = hx > 1e-6 ? (hx + by) / hx : 1, ky = hy > 1e-6 ? (hy + by) / hy : 1;
        return [.. pts.Select(p => (cx + (p.X - cx) * kx, cy + (p.Y - cy) * ky))];
    }

    /// <summary>A bowl: its wall from z0 to z1 round its opening, its floor under it.</summary>
    static void Bowl(Builder b, string role, List<(double X, double Y)> opening, double z0, double z1, double wall)
    {
        var outer = Grow(opening, wall);
        b.PlanPrism(role, outer, [opening], z0 + wall, z1);
        b.PlanPrism(role, outer, z0, z0 + wall);
    }

    /// <summary>A round bar along x at height z, its middle <paramref name="y"/> from the front (a towel rail, a grab bar, a roll).</summary>
    static void Bar(Builder b, string role, double x0, double x1, double y, double z, double r)
    {
        var ring = Chords.Ring(new Vec2(y, z), r);
        b.SweepX(role, x0, x1, [.. ring.Select(p => (p.X, p.Y))]);
    }

    static void Round(Builder b, string role, double cx, double cy, double r, double z0, double z1) => b.PlanPrism(role, Disc(cx, cy, r), z0, z1);

    // ---- The bodies ----

    public static void Build(Builder b, Spec s)
    {
        switch (s.Kind.Key)
        {
            case "toilet": Toilet(b, s); break;
            case "bidet": Bidet(b, s); break;
            case "washbasin": Washbasin(b, s); break;
            case "vanity": Vanity(b, s); break;
            case "shower": Shower(b, s); break;
            case "bath": Bath(b, s); break;
            case "kitchenSink": KitchenSink(b, s); break;
            case AccessoryKey: Accessory(b, s); break;
        }
    }

    /// <summary>Where a pan's straight back part begins: as deep as its cistern and hinge need, the rest the front's half ellipse.</summary>
    static double PanSplit(double d) => d / 2 - Math.Min(0.22, d * 0.4);

    /// <summary>A pan's opening (a WC's, a bidet's): an ellipse from near its front to past where its back begins.</summary>
    static List<(double X, double Y)> PanOpening(double w, double d, double backAt)
    {
        double front = -d / 2 + 0.035, back = Math.Max(front + 0.1, backAt);
        return Ellipse(0, (front + back) / 2, Math.Max(0.05, w / 2 - 0.05), (back - front) / 2);
    }

    /// <summary>
    /// A WC: its pan (its foot, narrower, and its bowl round its opening) wall-hung or on the floor, its seat on top (the type's
    /// height is the seat's), a close-coupled cistern on its back or a concealed one's flush plate on the wall.
    /// </summary>
    static void Toilet(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H;
        var floor = s.Str("mount") == "floor";
        var coupled = s.Str("cistern") == "closeCoupled";
        var split = PanSplit(d);
        var outline = Egg(w, -d / 2, d / 2, split);
        var opening = PanOpening(w, d, split + 0.03);
        double rim = h - 0.03, bowl = h - 0.20, foot = floor ? 0 : h - 0.34;
        // The foot: narrower, set back from the front.
        b.PlanPrism(Ceramic, Egg(w * 0.62, -d / 2 + 0.08, d / 2, split + 0.02), foot, bowl);
        // The bowl: its floor, then its wall round the opening up to the rim.
        b.PlanPrism(Ceramic, outline, bowl, bowl + 0.02);
        b.PlanPrism(Ceramic, outline, [opening], bowl + 0.02, rim);
        // The seat (and its lid, open): on the pan, short of the cistern.
        var seatBack = coupled ? d / 2 - 0.18 : d / 2 - 0.05;
        var seat = Egg(w - 0.01, -d / 2 + 0.005, seatBack, Math.Min(split, seatBack - 0.02));
        b.PlanPrism(Seat, seat, [opening], rim, h);
        if (coupled)
        {
            b.Block(Ceramic, -w / 2, w / 2, d / 2 - 0.17, d / 2, rim, h + 0.40, 0.02);
            b.Block(Fittings, -0.03, 0.03, d / 2 - 0.1, d / 2 - 0.07, h + 0.40, h + 0.415);
        }
        else b.Block(Fittings, -0.12, 0.12, d / 2 - 0.012, d / 2, 0.98, 1.15, 0.005);
        // In plan: the pan's outline, the seat's opening, the cistern; a concealed one's plate on the wall's face.
        b.Loop(Sym, outline);
        b.Loop(Sym, opening);
        if (coupled) b.PlanRect(Sym, -w / 2, w / 2, d / 2 - 0.17, d / 2, 0.02);
        else b.Line(Sym, (-0.12, d / 2 - 0.012), (0.12, d / 2 - 0.012));
    }

    /// <summary>A bidet: a pan as a WC's with no seat, its tap on the deck behind its bowl.</summary>
    static void Bidet(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H;
        var floor = s.Str("mount") == "floor";
        var split = PanSplit(d);
        var outline = Egg(w, -d / 2, d / 2, split);
        var opening = PanOpening(w, d, d / 2 - 0.15);
        double bowl = h - 0.17, foot = floor ? 0 : h - 0.32;
        b.PlanPrism(Ceramic, Egg(w * 0.62, -d / 2 + 0.08, d / 2, split + 0.02), foot, bowl);
        b.PlanPrism(Ceramic, outline, bowl, bowl + 0.02);
        b.PlanPrism(Ceramic, outline, [opening], bowl + 0.02, h);
        var tapY = d / 2 - 0.09;
        if (s.Bool("tap"))
        {
            Round(b, Fittings, 0, tapY, 0.025, h, h + 0.10);
            b.Block(Fittings, -0.012, 0.012, tapY - 0.11, tapY, h + 0.075, h + 0.095, 0.005);
        }
        b.Loop(Sym, outline);
        b.Loop(Sym, opening);
        b.Loop(Sym, Disc(0, opening.Average(p => p.Y), 0.018));
        if (s.Bool("tap")) b.Loop(Sym, Disc(0, tapY, 0.025));
    }

    /// <summary>A basin's opening: an ellipse in front of the deck its tap stands on.</summary>
    static List<(double X, double Y)> BasinOpening(double w, double d, double deck)
    {
        double front = -d / 2 + 0.05, back = d / 2 - deck;
        return Ellipse(0, (front + back) / 2, Math.Max(0.08, w / 2 - 0.06), Math.Max(0.06, (back - front) / 2));
    }

    /// <summary>A basin's mixer: on its deck, or its spout out of the wall over the bowl.</summary>
    static void Tap(Builder b, string tap, double x, double back, double deckY, double rim)
    {
        if (tap == "deck")
        {
            Round(b, Fittings, x, deckY, 0.025, rim, rim + 0.13);
            b.Block(Fittings, x - 0.012, x + 0.012, deckY - 0.13, deckY, rim + 0.10, rim + 0.122, 0.005);
            b.Loop(Sym, Disc(x, deckY, 0.025));
        }
        else if (tap == "wall")
        {
            b.Block(Fittings, x - 0.035, x + 0.035, back - 0.012, back, rim + 0.13, rim + 0.20, 0.005);
            b.Block(Fittings, x - 0.012, x + 0.012, back - 0.17, back - 0.012, rim + 0.15, rim + 0.172, 0.005);
            b.Line(Sym, (x, back), (x, back - 0.17));
        }
    }

    /// <summary>A wash basin: its rim round its bowl, its tap, on the wall alone or on a pedestal (to the floor) or a half pedestal.</summary>
    static void Washbasin(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H;
        var outline = s.Str("shape") == "oval" ? Egg(w, -d / 2, d / 2, d / 2 - Math.Min(0.12, d * 0.3)) : Rounded(-w / 2, w / 2, -d / 2, d / 2, 0.04);
        var deck = Math.Min(0.13, d * 0.3);
        var opening = BasinOpening(w, d, deck);
        double bowl = h - 0.17;
        var under = Grow(opening, 0.015);
        b.PlanPrism(Ceramic, under, bowl, bowl + 0.02);
        b.PlanPrism(Ceramic, under, [opening], bowl + 0.02, h - 0.03);
        b.PlanPrism(Ceramic, outline, [opening], h - 0.03, h);
        // The pedestal under its waste, or the half pedestal hiding the trap.
        var py = Math.Min(d / 2 - 0.1, opening.Max(p => p.Y));
        if (s.Str("mount") == "pedestal") b.PlanPrism(Ceramic, Ellipse(0, py, 0.09, 0.08), 0, bowl);
        else if (s.Str("mount") == "semiPedestal") b.PlanPrism(Ceramic, Ellipse(0, py, 0.09, 0.08), bowl - 0.30, bowl);
        Tap(b, s.Str("tap"), 0, d / 2, d / 2 - deck / 2, h);
        b.Loop(Sym, outline);
        b.Loop(Sym, opening);
        b.Loop(Sym, Disc(0, opening.Average(p => p.Y), 0.02));
    }

    /// <summary>A vanity unit: a cabinet of drawers (on the wall or on a plinth), its top with the basin set in it, a tap on its deck.</summary>
    static void Vanity(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H;
        var hung = s.Str("base") != "floor";
        double z0 = hung ? Math.Max(0.05, h - 0.55) : 0.10, top = h - 0.04, bowl = h - 0.18;
        const double front = 0.018;
        var deck = Math.Min(0.12, d * 0.3);
        var opening = BasinOpening(Math.Min(w, 0.62), d, deck);
        var under = Grow(opening, 0.015);
        double hx0 = under.Min(p => p.X), hx1 = under.Max(p => p.X), hy0 = under.Min(p => p.Y), hy1 = under.Max(p => p.Y);
        // The carcass under the bowl, its frame round the bowl up to the top.
        if (!hung) b.Block("body", -w / 2 + 0.02, w / 2 - 0.02, -d / 2 + 0.06, d / 2, 0, z0);
        b.Block("body", -w / 2, w / 2, -d / 2 + front, d / 2, z0, bowl);
        b.Slab("body", -w / 2, w / 2, -d / 2 + front, d / 2, bowl, top, (hx0, hx1, hy0, hy1));
        b.PlanPrism(Ceramic, under, bowl, bowl + 0.02);
        b.PlanPrism(Ceramic, under, [opening], bowl + 0.02, top);
        b.PlanPrism(Ceramic, Rounded(-w / 2, w / 2, -d / 2, d / 2, 0.01), [opening], top, h);
        // The drawers' fronts, one over the other with a 3 mm gap.
        var n = Math.Clamp(s.Int("drawers", 2), 1, 3);
        var step = (top - z0) / n;
        for (var i = 0; i < n; i++) b.Block("fronts", -w / 2 + 0.002, w / 2 - 0.002, -d / 2, -d / 2 + front, z0 + i * step + 0.0015, z0 + (i + 1) * step - 0.0015);
        if (s.Bool("tap")) Tap(b, "deck", 0, d / 2, d / 2 - deck / 2, h);
        b.PlanRect(Sym, -w / 2, w / 2, -d / 2, d / 2, 0.01);
        b.Loop(Sym, opening);
        b.Loop(Sym, Disc(0, opening.Average(p => p.Y), 0.02));
    }

    /// <summary>Where a shower's drain is: the tray's middle, a quadrant's a third of the way out of its corner.</summary>
    static (double X, double Y) DrainOf(Spec s) => s.Str("shape") == "quadrant" ? (-s.W / 2 + s.W * 0.36, s.D / 2 - s.D * 0.36) : (0, 0);

    /// <summary>
    /// A shower: its tray (a rim round a recess, the drain), the mixer on the wall behind, the riser and the head at the type's
    /// height; its enclosure of glass across the front: a hinged door, two sliding panels, a walk-in's fixed screen, or a
    /// quadrant's curved front.
    /// </summary>
    static void Shower(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H, th = Math.Clamp(s.Num("trayHeight", 0.04), 0.02, 0.25);
        var quadrant = s.Str("shape") == "quadrant";
        const double rim = 0.04;
        var outline = quadrant ? Quadrant(-w / 2, d / 2, w, d) : Rounded(-w / 2, w / 2, -d / 2, d / 2, 0.01);
        var inner = quadrant ? Quadrant(-w / 2 + rim, d / 2 - rim, w - 2 * rim, d - 2 * rim) : Rounded(-w / 2 + rim, w / 2 - rim, -d / 2 + rim, d / 2 - rim, 0.02);
        b.PlanPrism(Ceramic, outline, 0, th - 0.012);
        b.PlanPrism(Ceramic, outline, [inner], th - 0.012, th);
        var (dx, dy) = DrainOf(s);
        Round(b, Fittings, dx, dy, 0.05, th - 0.012, th - 0.008);
        // The mixer, the riser and the head.
        b.Block(Fittings, -0.08, 0.08, d / 2 - 0.06, d / 2, 1.05, 1.15, 0.01);
        Round(b, Fittings, 0, d / 2 - 0.03, 0.012, 1.15, h - 0.04);
        b.Block(Fittings, -0.01, 0.01, d / 2 - 0.25, d / 2 - 0.03, h - 0.045, h - 0.025);
        Round(b, Fittings, 0, Math.Max(-d / 2 + 0.1, d / 2 - 0.25), 0.1, h - 0.03, h);
        // The glass, 8 mm, on the tray up to 1.95 m over it (no higher than the type).
        var gz1 = Math.Min(h, th + 1.95);
        const double g = 0.008;
        switch (s.Str("enclosure"))
        {
            case "door" when quadrant:
            case "sliding" when quadrant:
            case "walkIn" when quadrant:
            {
                var outer = Quadrant(-w / 2, d / 2, w - 0.004, d - 0.004).Skip(1).ToList();
                var innerArc = Quadrant(-w / 2, d / 2, w - 0.004 - g, d - 0.004 - g).Skip(1).Reverse().ToList();
                var band = outer.Concat(innerArc).ToList();
                b.PlanPrism(Screen, band, th, gz1);
                b.Line(Sym, [.. outer]);
                break;
            }
            case "door":
                b.Block(Screen, -w / 2 + 0.004, w / 2 - 0.004, -d / 2 + 0.004, -d / 2 + 0.004 + g, th, gz1);
                b.Line(Sym, (-w / 2, -d / 2 + 0.008), (w / 2, -d / 2 + 0.008));
                // Its swing, out of the shower, hinged on the left (drawn from 1:200 on).
                var leaf = w - 0.008;
                b.LineFrom(PlanDetail.Fine, Sym, [(-w / 2 + 0.004, -d / 2), (-w / 2 + 0.004, -d / 2 - leaf)]);
                b.LineFrom(PlanDetail.Fine, Sym, Chords.Arc(new Vec2(-w / 2 + 0.004, -d / 2), leaf, 0, -Math.PI / 2, 6).Select(p => (p.X, p.Y)));
                break;
            case "sliding":
                b.Block(Screen, -w / 2 + 0.004, 0.03, -d / 2 + 0.004, -d / 2 + 0.004 + g, th, gz1);
                b.Block(Screen, -0.03, w / 2 - 0.004, -d / 2 + 0.016, -d / 2 + 0.016 + g, th, gz1);
                b.Line(Sym, (-w / 2, -d / 2 + 0.008), (0.03, -d / 2 + 0.008));
                b.Line(Sym, (-0.03, -d / 2 + 0.02), (w / 2, -d / 2 + 0.02));
                break;
            case "walkIn":
                // A fixed screen over most of the front, the way in on the right.
                var x1 = w / 2 - Math.Min(0.6, Math.Max(0.4, w * 0.4));
                b.Block(Screen, -w / 2 + 0.004, x1, -d / 2 + 0.004, -d / 2 + 0.004 + g, th, gz1);
                b.Line(Sym, (-w / 2, -d / 2 + 0.008), (x1, -d / 2 + 0.008));
                break;
        }
        // In plan: the tray, its rim, the cross falling to the drain, the drain.
        b.Loop(Sym, outline);
        b.Loop(Sym, inner);
        if (quadrant) b.Line(Sym, (-w / 2 + rim, d / 2 - rim), (dx, dy));
        else
        {
            b.Line(Sym, (-w / 2 + rim, -d / 2 + rim), (w / 2 - rim, d / 2 - rim));
            b.Line(Sym, (-w / 2 + rim, d / 2 - rim), (w / 2 - rim, -d / 2 + rim));
        }
        b.Loop(Sym, Disc(dx, dy, 0.05));
    }

    /// <summary>Where a bath's taps and waste are: its left end, as seen from its front (mirrored: its right).</summary>
    const double BathTapEnd = 0.25;

    /// <summary>A bath: its shell round the tub (on its panel to the floor, or on four feet), the tap at its left end, the waste under it.</summary>
    static void Bath(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H;
        var outline = Rounded(-w / 2, w / 2, -d / 2, d / 2, 0.03);
        var tub = Rounded(-w / 2 + 0.14, w / 2 - 0.07, -d / 2 + 0.07, d / 2 - 0.07, Math.Min(0.18, d / 2 - 0.1));
        var floorZ = Math.Max(0.08, h - 0.42);
        if (s.Bool("panel")) b.PlanPrism(Ceramic, outline, 0, floorZ);
        else
        {
            b.PlanPrism(Ceramic, Rounded(-w / 2 + 0.05, w / 2 - 0.05, -d / 2 + 0.05, d / 2 - 0.05, 0.1), 0.06, floorZ);
            foreach (var (fx, fy) in new[] { (-w / 2 + 0.12, -d / 2 + 0.1), (w / 2 - 0.12, -d / 2 + 0.1), (-w / 2 + 0.12, d / 2 - 0.1), (w / 2 - 0.12, d / 2 - 0.1) })
                Round(b, Fittings, fx, fy, 0.03, 0, 0.06);
        }
        b.PlanPrism(Ceramic, outline, [tub], floorZ, h);
        var tapX = -w / 2 + 0.07;
        var tap = s.Str("tap");
        if (tap == "deck")
        {
            Round(b, Fittings, tapX, 0, 0.025, h, h + 0.12);
            b.Block(Fittings, tapX, tapX + 0.15, -0.012, 0.012, h + 0.09, h + 0.112, 0.005);
            b.Loop(Sym, Disc(tapX, 0, 0.025));
        }
        else if (tap == "wall")
        {
            var x = -w / 2 + BathTapEnd;
            b.Block(Fittings, x - 0.035, x + 0.035, d / 2 - 0.012, d / 2, h + 0.15, h + 0.22, 0.005);
            b.Block(Fittings, x - 0.012, x + 0.012, d / 2 - 0.16, d / 2 - 0.012, h + 0.17, h + 0.192, 0.005);
            b.Line(Sym, (x, d / 2), (x, d / 2 - 0.16));
        }
        b.Loop(Sym, outline);
        b.Loop(Sym, tub);
        b.Loop(Sym, Disc(-w / 2 + BathTapEnd, 0, 0.025));
    }

    /// <summary>A kitchen sink's bowls across its width (the drainer's side left out), each its x0, x1.</summary>
    static List<(double X0, double X1)> SinkBowls(Spec s)
    {
        var w = s.W;
        var drainer = s.Str("drainer");
        var dw = drainer == "none" ? 0 : Math.Min(0.40, w * 0.4);
        double x0 = -w / 2 + 0.04 + (drainer == "left" ? dw : 0), x1 = w / 2 - 0.04 - (drainer == "right" ? dw : 0);
        var n = Math.Clamp(s.Int("bowls", 1), 1, 2);
        var bw = (x1 - x0 - 0.04 * (n - 1)) / n;
        return [.. Enumerable.Range(0, n).Select(i => (x0 + i * (bw + 0.04), x0 + i * (bw + 0.04) + bw))];
    }

    /// <summary>A kitchen sink set in a worktop (its rim at the type's height): its top round its bowls, the drainer's ribs, the tap behind.</summary>
    static void KitchenSink(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H;
        var bowls = SinkBowls(s);
        double by0 = -d / 2 + 0.06, by1 = d / 2 - 0.09;
        var openings = bowls.Select(x => Rounded(x.X0, x.X1, by0, by1, 0.04)).ToList();
        var outline = Rounded(-w / 2, w / 2, -d / 2, d / 2, 0.01);
        b.PlanPrism(Steel, outline, [.. openings], h - 0.012, h);
        foreach (var o in openings) Bowl(b, Steel, o, h - 0.20, h - 0.012, 0.008);
        var tapX = (bowls[0].X0 + bowls[0].X1) / 2;
        if (s.Bool("tap"))
        {
            Round(b, Fittings, tapX, d / 2 - 0.045, 0.025, h, h + 0.25);
            b.Block(Fittings, tapX - 0.012, tapX + 0.012, d / 2 - 0.045 - 0.20, d / 2 - 0.045, h + 0.22, h + 0.245, 0.005);
            b.Loop(Sym, Disc(tapX, d / 2 - 0.045, 0.025));
        }
        b.Loop(Sym, outline);
        foreach (var (o, x) in openings.Zip(bowls))
        {
            b.Loop(Sym, o);
            b.Loop(Sym, Disc((x.X0 + x.X1) / 2, (by0 + by1) / 2, 0.03));
        }
        // The drainer's ribs, running towards the bowls.
        var drainer = s.Str("drainer");
        if (drainer != "none")
        {
            double dx0 = drainer == "right" ? bowls[^1].X1 + 0.04 : -w / 2 + 0.04, dx1 = drainer == "right" ? w / 2 - 0.04 : bowls[0].X0 - 0.04;
            for (var i = 1; i <= 5; i++)
            {
                var y = by0 + (by1 - by0) * i / 6;
                b.Line(Sym, (dx0, y), (dx1, y));
            }
        }
    }

    /// <summary>A bathroom accessory hung on the wall at its height: a rail or a bar on two brackets, a mirror, a roll on its holder.</summary>
    static void Accessory(Builder b, Spec s)
    {
        double w = s.W, d = s.D, h = s.H, z = s.Num("mount", Item(s).Mount);
        switch (Item(s).Key)
        {
            case "mirror":
                b.Block(MirrorRole, -w / 2, w / 2, -d / 2, d / 2 - 0.004, z, z + h);
                b.Block(Fittings, -w / 2 + 0.03, w / 2 - 0.03, d / 2 - 0.004, d / 2, z + 0.03, z + h - 0.03);
                b.PlanRect(Sym, -w / 2, w / 2, -d / 2, d / 2);
                b.Line(Sym, (-w / 2, d / 2 - 0.004), (w / 2, d / 2 - 0.004));
                break;
            case "paperHolder":
            {
                var r = Math.Min(h, d) / 2 - 0.005;
                b.Block(Fittings, -w / 2, w / 2, d / 2 - 0.01, d / 2, z, z + h, 0.005);
                Bar(b, Fittings, -w / 2 + 0.02, w / 2 - 0.02, -d / 2 + r, z + h / 2, r);
                b.PlanRect(Sym, -w / 2, w / 2, d / 2 - 0.01, d / 2);
                b.PlanRect(Sym, -w / 2 + 0.02, w / 2 - 0.02, -d / 2, -d / 2 + 2 * r);
                break;
            }
            default:
            {
                // A rail or bar: round, its middle on the front, on a bracket at each end back to the wall.
                var r = Item(s).Key == "grabBar" ? 0.016 : 0.0125;
                r = Math.Min(r, Math.Min(h, d) / 2);
                Bar(b, Fittings, -w / 2, w / 2, -d / 2 + r, z + h / 2, r);
                foreach (var x in new[] { -w / 2 + 0.035, w / 2 - 0.035 })
                    b.Block(Fittings, x - 0.012, x + 0.012, -d / 2 + r, d / 2, z + h / 2 - 0.012, z + h / 2 + 0.012, 0.004);
                b.PlanRect(Sym, -w / 2, w / 2, -d / 2, -d / 2 + 2 * r, r * 0.9);
                b.Line(Sym, (-w / 2 + 0.035, -d / 2 + 2 * r), (-w / 2 + 0.035, d / 2));
                b.Line(Sym, (w / 2 - 0.035, -d / 2 + 2 * r), (w / 2 - 0.035, d / 2));
                break;
            }
        }
    }

    // ---- The connection points ----

    /// <summary>
    /// A fixture's connection points in its own frame (an accessory has none): cold and hot water in at the wall behind it
    /// (a WC's cold to its cistern), the waste out into the wall (wall-hung, a basin's trap) or down through the floor (a tray's,
    /// a bath's, a floor-standing pan's). Sizes are nominal: a WC's waste 110 mm, the others' 40 or 50 mm.
    /// </summary>
    public static IReadOnlyList<SanitaryPort> Ports(Spec s)
    {
        double w = s.W, d = s.D, h = s.H;
        SanitaryPort Cold(double x, double y, double z, Vec3? dir = null) => new("cold", ColdWater, "SINK", new(x, y, z), dir ?? Back);
        SanitaryPort Hot(double x, double y, double z, Vec3? dir = null) => new("hot", HotWater, "SINK", new(x, y, z), dir ?? Back);
        SanitaryPort Waste(string key, double x, double y, double z, Vec3 dir, double size) => new(key, Drainage, "SOURCE", new(x, y, z), dir, size);
        switch (s.Kind.Key)
        {
            case "toilet":
            {
                var floor = s.Str("mount") == "floor";
                var cold = s.Str("cistern") == "closeCoupled" ? Cold(-0.15, d / 2, 0.15) : Cold(-0.15, d / 2, 1.0);
                return [cold, floor ? Waste("waste", 0, d / 2 - 0.18, 0, Down, 0.11) : Waste("waste", 0, d / 2, 0.23, Back, 0.11)];
            }
            case "bidet":
            {
                var floor = s.Str("mount") == "floor";
                return [Cold(-0.05, d / 2, h - 0.15), Hot(0.05, d / 2, h - 0.15), floor ? Waste("waste", 0, d / 2 - 0.2, 0, Down, 0.04) : Waste("waste", 0, d / 2, h - 0.22, Back, 0.04)];
            }
            case "washbasin":
            case "vanity":
                return [Cold(-0.08, d / 2, h - 0.25), Hot(0.08, d / 2, h - 0.25), Waste("waste", 0, d / 2, h - 0.30, Back, 0.04)];
            case "shower":
            {
                var (dx, dy) = DrainOf(s);
                return [Cold(-0.075, d / 2, 1.1), Hot(0.075, d / 2, 1.1), Waste("waste", dx, dy, 0, Down, 0.05)];
            }
            case "bath":
            {
                var x = -w / 2 + BathTapEnd;
                var z = s.Str("tap") == "wall" ? h + 0.185 : h - 0.10;
                return [Cold(x - 0.075, d / 2, z), Hot(x + 0.075, d / 2, z), Waste("waste", x, 0, 0, Down, 0.05)];
            }
            case "kitchenSink":
            {
                var bowls = SinkBowls(s);
                var o = new List<SanitaryPort> { Cold(-0.08, d / 2, h - 0.40), Hot(0.08, d / 2, h - 0.40) };
                for (var i = 0; i < bowls.Count; i++) o.Add(Waste(i == 0 ? "waste" : $"waste{i + 1}", (bowls[i].X0 + bowls[i].X1) / 2, d / 2, h - 0.45, Back, 0.04));
                return o;
            }
            default: return [];
        }
    }

    /// <summary>A placed fixture's connection points in the project's frame: turned, mirrored and raised with it.</summary>
    public static IReadOnlyList<PlacedPort> PortsOf(IModelView m, Entity e)
    {
        if (e.Class != FurnitureModule.Class || Args.TryPoint(e.Attrs["at"]) is not { } at) return [];
        var spec = new Spec(FurnitureModule.TypeOf(m, e)?.Attrs["spec"] as JsonObject);
        if (!Is(spec.Kind)) return [];
        var rot = e.Num("rotation") * Math.PI / 180;
        var (c, sn) = (Math.Cos(rot), Math.Sin(rot));
        var mirror = e.Bool("mirror") ^ FurnitureBodies.Handed(spec);
        var z = Spatial.Elevation(m, e) + e.Num("offset");
        Vec3 Turn(Vec3 v) => new(c * v.X - sn * v.Y, sn * v.X + c * v.Y, v.Z);
        return [.. Ports(spec).Select(p =>
        {
            var local = mirror ? p.At with { X = -p.At.X } : p.At;
            var dir = mirror ? p.Dir with { X = -p.Dir.X } : p.Dir;
            var t = Turn(local);
            return new PlacedPort(p.Key, p.Service, p.Flow, new Vec3(at.X + t.X, at.Y + t.Y, z + t.Z), Turn(dir), p.Size);
        })];
    }

    /// <summary>The service as IFC's distribution system type names it (as the MEP module's network does).</summary>
    public static string SystemTypeOf(string service) => service switch
    {
        ColdWater => "DOMESTICCOLDWATER",
        HotWater => "DOMESTICHOTWATER",
        Drainage => "WASTEWATER",
        _ => "USERDEFINED",
    };
}

/// <summary>
/// The sanitary fixtures' connection points for the IFC export (IIfcNetwork): each an <c>IfcDistributionPort</c> nested in its
/// fixture, a pipe's, of the domestic water's or the waste water's system; no connections or systems of their own: an MEP
/// network that joins them declares those.
/// </summary>
sealed class SanitaryNetwork : IIfcNetwork
{
    public IEnumerable<IfcPort> PortsOf(IModelView model, Entity element) =>
        Sanitary.PortsOf(model, element).Select(p => new IfcPort(p.Key, p.At, p.Dir, p.Flow, "PIPE", Sanitary.SystemTypeOf(p.Service)));

    public IEnumerable<IfcPortConnection> Connections(IModelView model) => [];

    public IEnumerable<IfcSystemGroup> Systems(IModelView model) => [];
}
