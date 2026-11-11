using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Furniture;

/// <summary>
/// The kitchen's layouts (report #124): one wall (the straight run), galley (two parallel runs), L-shaped, U-shaped, island (a
/// run and a separate island) and peninsula (an L with a run reaching out from the leg). Each is built from straight runs
/// of base units with a worktop, wall units on those against a wall, a tall unit at a free end, and the sink and the hob on
/// the run or on the other element. The footprint is the run's length by <see cref="Depth"/>, the back wall at its back
/// (+y), the room in front (−y); an L and a peninsula with their corner on the right are built as the left ones, mirrored.
/// </summary>
public static partial class KitchenLayouts
{
    public static readonly string[] Keys = ["one-wall", "galley", "l-shaped", "u-shaped", "island", "peninsula"];

    public static string Label(string key) => key switch
    {
        "galley" => "Galley", "l-shaped" => "L-shaped", "u-shaped" => "U-shaped", "island" => "Island", "peninsula" => "Peninsula", _ => "One wall",
    };

    /// <summary>The parameters a layout starts from (the type takes them when it is chosen), after the sizes kitchens are drawn at.</summary>
    public static JsonObject Preset(string key) => key switch
    {
        "galley" => new JsonObject { ["layout"] = key, ["width"] = 3.0, ["gap"] = 1.2, ["secondRun"] = 0, ["sinkOn"] = "run", ["hobOn"] = "other", ["tall"] = "right" },
        "l-shaped" => new JsonObject { ["layout"] = key, ["width"] = 3.0, ["corner"] = "left", ["leg"] = 2.4, ["sinkOn"] = "other", ["hobOn"] = "run", ["tall"] = "right" },
        "u-shaped" => new JsonObject { ["layout"] = key, ["width"] = 3.0, ["leg"] = 2.4, ["leg2"] = 2.4, ["sinkOn"] = "other", ["hobOn"] = "run", ["tall"] = "none" },
        "island" => new JsonObject { ["layout"] = key, ["width"] = 3.6, ["gap"] = 1.1, ["islandLength"] = 1.8, ["islandDepth"] = 0.9, ["islandAt"] = 0.5, ["sinkOn"] = "other", ["hobOn"] = "run", ["tall"] = "right" },
        "peninsula" => new JsonObject { ["layout"] = key, ["width"] = 3.0, ["corner"] = "left", ["leg"] = 2.4, ["peninsula"] = 1.2, ["overhang"] = 0.3, ["sinkOn"] = "other", ["hobOn"] = "run", ["tall"] = "right" },
        _ => new JsonObject { ["layout"] = "one-wall", ["width"] = 3.0, ["sinkOn"] = "run", ["hobOn"] = "run", ["tall"] = "right" },
    };

    public static JsonArray Catalog() => new([.. Keys.Select(k => (JsonNode)new JsonObject { ["key"] = k, ["label"] = Label(k), ["preset"] = Preset(k) })]);

    public static string Of(Spec s) => s.Kind.Key == "kitchen" && Keys.Contains(s.Str("layout")) ? s.Str("layout") : "one-wall";

    /// <summary>Whether the layout is built from its left-cornered form, mirrored, for its corner on the right.</summary>
    public static bool Handed(Spec s) => Of(s) is "l-shaped" or "peninsula" && s.Str("corner") == "right";

    const double Tall = 0.6, TallHeight = 2.15, MinFree = 0.6;

    /// <summary>The depth a peninsula's run takes at the front of the leg: its units and the worktop reaching past them for stools.</summary>
    static double PeninsulaZone(Spec s) => s.D + Math.Clamp(s.Num("overhang", 0.3), 0, 0.5);

    /// <summary>The footprint's depth: from the back wall to the front of the furthest leg, run or island.</summary>
    public static double Depth(Spec s)
    {
        double d = s.D, gap = s.Num("gap", 1.2);
        return Of(s) switch
        {
            "galley" => 2 * d + gap,
            "l-shaped" => Math.Max(s.Num("leg", 2.4), d + MinFree),
            "u-shaped" => Math.Max(Math.Max(s.Num("leg", 2.4), s.Num("leg2", 2.4)), d + MinFree),
            "island" => d + gap + Math.Max(d, s.Num("islandDepth", 0.9)),
            "peninsula" => Math.Max(s.Num("leg", 2.4), d + MinFree + PeninsulaZone(s)),
            _ => d,
        };
    }

    /// <summary>A run's axes in the layout's frame: u along it as seen from its front (left to right), v from its front to its back.</summary>
    readonly record struct Frame(double Ox, double Oy, double Ux, double Uy, double Wx, double Wy)
    {
        /// <summary>A run whose front looks along −y (the back wall behind it), +y, +x or −x, its front-left corner at (x, y).</summary>
        public static Frame Facing(char front, double x, double y) => front switch
        {
            'S' => new(x, y, 1, 0, 0, 1),
            'N' => new(x, y, -1, 0, 0, -1),
            'E' => new(x, y, 0, 1, -1, 0),
            _ => new(x, y, 0, -1, 1, 0),
        };

        public (double X, double Y) P(double u, double v) => (Ox + Ux * u + Wx * v, Oy + Uy * u + Wy * v);

        (double X0, double X1, double Y0, double Y1) Box(double u0, double u1, double v0, double v1)
        {
            var (xa, ya) = P(u0, v0);
            var (xb, yb) = P(u1, v1);
            return (Math.Min(xa, xb), Math.Max(xa, xb), Math.Min(ya, yb), Math.Max(ya, yb));
        }

        public void Block(Builder b, string role, double u0, double u1, double v0, double v1, double z0, double z1, double r = 0)
        {
            var q = Box(u0, u1, v0, v1);
            b.Block(role, q.X0, q.X1, q.Y0, q.Y1, z0, z1, r);
        }

        public void Rect(Builder b, string kind, double u0, double u1, double v0, double v1, double r = 0)
        {
            var q = Box(u0, u1, v0, v1);
            b.PlanRect(kind, q.X0, q.X1, q.Y0, q.Y1, r);
        }

        public void Line(Builder b, string kind, double u0, double v0, double u1, double v1) => b.Line(kind, P(u0, v0), P(u1, v1));

        public void Circle(Builder b, string kind, double u, double v, double r, int sides) { var (x, y) = P(u, v); b.Circle(kind, x, y, r, sides); }

        public void Cylinder(Builder b, string role, double u, double v, double r, double z0, double z1, int sides) { var (x, y) = P(u, v); b.Cylinder(role, x, y, r, z0, z1, sides); }
    }

    /// <summary>What a run has: where its sink and hob go (0–1 along its free span), a tall unit at one end, blind corner squares, wall units, a worktop reaching past its back.</summary>
    /// <summary><paramref name="Main"/>: the run the type's own units are laid along (decision 472); <paramref name="IslandHood"/>: a hood hung over its hob.</summary>
    sealed record Run(double Length, string? TallAt = null, double CapStart = 0, double CapEnd = 0, bool WallUnits = false, double? Sink = null, double? Hob = null, double TopDepth = 0,
        bool Main = false, bool IslandHood = false);

    /// <summary>One run of base units: the carcass, a drawer over a door per unit, the worktop, the sink and hob, wall units, a tall unit.</summary>
    static void Build(Builder b, Frame f, Spec s, Run run, double depth)
    {
        var d = depth;
        var tall = run.TallAt;
        var len = run.Length;
        double a = 0, e = len;
        if (tall == "start" && len > Tall + 0.3) a = Tall;
        else if (tall == "end" && len > Tall + 0.3) e = len - Tall;
        else tall = null;
        double fa = a + run.CapStart, fe = e - run.CapEnd;
        (double, double)? tallSpan = tall == null ? null : tall == "start" ? (0, Tall) : (e, e + Tall);
        if (run.Main && KitchenUnits.Has(s)) BuildUnits(b, f, s, run, d, a, e, fa, fe, tallSpan);
        else
        {
            BuildAuto(b, f, s, run, d, a, e, fa, fe);
            if (run.Main) AutoRows(b, f, s, run, fa, fe, tallSpan);
        }
        if (tall != null) BuildTall(b, f, s, d, tall == "start" ? 0 : e, tall);
    }

    /// <summary>The plinth's and handles' roles: their own when the type names materials for them, else the carcass's and the metal's (as kitchens were before decision 472).</summary>
    static string PlinthRole(Spec s) => s.HasMaterial("plinth") ? "plinth" : "body";
    static string HandleRole(Spec s) => s.HasMaterial("handles") ? "handles" : "metal";

    /// <summary>The run split evenly into units of about the type's unit width, each a drawer over a door; the sink, the hob and wall units where the type puts them.</summary>
    static void BuildAuto(Builder b, Frame f, Spec s, Run run, double d, double a, double e, double fa, double fe)
    {
        double top = s.H, wt = s.Num("worktop", 0.04), pl = s.Num("plinth", 0.1), unit = s.Num("unit", 0.6);
        string pr = PlinthRole(s), hr = HandleRole(s);
        var n = Math.Max(1, (int)Math.Round((fe - fa) / unit));
        var uw = (fe - fa) / n;
        f.Block(b, pr, a, e, 0.07, d, 0, pl);
        f.Block(b, "body", a, e, 0.04, d, pl, top - wt);
        for (var i = 0; i < n; i++)
        {
            double ua = fa + i * uw + 0.0015, ub = fa + (i + 1) * uw - 0.0015;
            f.Block(b, "fronts", ua, ub, 0.022, 0.04, top - wt - 0.16, top - wt - 0.003, 0.002);
            f.Block(b, "fronts", ua, ub, 0.022, 0.04, pl + 0.003, top - wt - 0.163, 0.002);
            var mid = (ua + ub) / 2;
            f.Block(b, hr, mid - 0.07, mid + 0.07, 0.0, 0.022, top - wt - 0.05, top - wt - 0.038);
            if (i > 0) f.Line(b, "symbol", ua - 0.0015, 0, ua - 0.0015, 0.04);
        }
        f.Block(b, "top", a, e, 0, Math.Max(d, run.TopDepth), top - wt, top, 0.003);
        f.Rect(b, "symbol", a, e, 0, Math.Max(d, run.TopDepth));
        double At(double frac, double half) => Math.Clamp(fa + frac * (fe - fa), fa + half + 0.03, fe - half - 0.03);
        var vm = d / 2;
        if (run.Sink is { } sf && fe - fa >= 0.6)
        {
            var us = At(sf, 0.25);
            f.Block(b, "metal", us - 0.25, us + 0.25, vm - 0.2, vm + 0.2, top, top + 0.004, 0.02);
            f.Cylinder(b, "metal", us, d - 0.07, 0.016, top, top + 0.3, 10);
            f.Rect(b, "symbol", us - 0.25, us + 0.25, vm - 0.2, vm + 0.2, 0.02);
            f.Rect(b, "symbol", us - 0.2, us + 0.2, vm - 0.16, vm + 0.16, 0.05);
            f.Circle(b, "symbol", us, vm + 0.06, 0.025, 12);
        }
        if (run.Hob is { } hf && fe - fa >= 1.2)
        {
            var uh = At(hf, 0.3);
            f.Block(b, "glass", uh - 0.29, uh + 0.29, vm - 0.25, vm + 0.25, top, top + 0.006, 0.01);
            f.Rect(b, "symbol", uh - 0.29, uh + 0.29, vm - 0.25, vm + 0.25, 0.01);
            foreach (var (cx, cy, r) in new[] { (-0.14, -0.11, 0.09), (0.14, -0.11, 0.07), (-0.14, 0.12, 0.07), (0.14, 0.12, 0.09) })
                f.Circle(b, "symbol", uh + cx, vm + cy, r, 16);
        }
        if (run.WallUnits)
        {
            double wf = s.Num("wallFrom", 1.45), wh = s.Num("wallHeight", 0.72), wd = Math.Min(d, s.Num("wallDepth", 0.35));
            f.Block(b, "body", a, e, d - wd + 0.018, d, wf, wf + wh);
            for (var i = 0; i < n; i++)
            {
                double ua = fa + i * uw + 0.0015, ub = fa + (i + 1) * uw - 0.0015;
                f.Block(b, "fronts", ua, ub, d - wd, d - wd + 0.018, wf + 0.002, wf + wh - 0.002, 0.002);
            }
            f.Rect(b, "overhead", a, e, d - wd, d);
        }
        if (run.IslandHood && run.Hob is { } ih && fe - fa >= 1.2) IslandHood(b, f, s, At(ih, 0.3), d);
    }

    /// <summary>The tall unit at a run's end (the fridge's, as kitchens had it before their own units).</summary>
    static void BuildTall(Builder b, Frame f, Spec s, double d, double ta, string tall)
    {
        double pl = s.Num("plinth", 0.1), te = ta + Tall;
        {
            f.Block(b, PlinthRole(s), ta, te, 0.04, d, 0, pl);
            f.Block(b, "body", ta, te, 0.022, d, pl, TallHeight);
            f.Block(b, "fronts", ta + 0.0015, te - 0.0015, 0, 0.022, pl + 0.003, pl + 1.2, 0.002);
            f.Block(b, "fronts", ta + 0.0015, te - 0.0015, 0, 0.022, pl + 1.203, TallHeight - 0.003, 0.002);
            var hx = tall == "start" ? te - 0.05 : ta + 0.05;
            f.Block(b, HandleRole(s), hx - 0.007, hx + 0.007, -0.025, 0, pl + 0.9, pl + 1.15);
            f.Rect(b, "symbol", ta, te, 0, d);
            f.Line(b, "symbol", ta, 0, te, d);
            f.Line(b, "symbol", ta, d, te, 0);
        }
    }

    /// <summary>The kitchen of <paramref name="len"/> (the main run's length) in the layout, into the builder.</summary>
    public static void Build(Builder b, Spec s, double len)
    {
        var layout = Of(s);
        double d = s.D, dt = Depth(s);
        var handed = Handed(s);
        // The sides as built: an L or a peninsula is built with its corner on the left, mirrored for the right.
        string tall = s.Str("tall", "none");
        if (handed) tall = tall switch { "left" => "right", "right" => "left", _ => tall };
        var wall = s.Bool("wallUnits");
        bool sinkOther = s.Str("sinkOn", "run") == "other", hobOther = s.Str("hobOn", "run") == "other";
        double sinkAt = s.Num("sinkAt", 0.3), hobAt = s.Num("hobAt", 0.72);
        // Fractions are from the left as seen from the front: a mirrored build counts from its other end.
        double Along(double f) => handed ? 1 - f : f;
        var back = dt / 2;
        var main = Frame.Facing('S', -len / 2, back - d);
        string? MainTall(bool leftFree, bool rightFree) => tall == "left" && leftFree ? "start" : tall == "right" && rightFree ? "end" : null;

        switch (layout)
        {
            case "galley":
            {
                Build(b, main, s, new Run(len, MainTall(true, true), WallUnits: wall, Sink: sinkOther ? null : Along(sinkAt), Hob: hobOther ? null : Along(hobAt), Main: true), d);
                var l2 = s.Num("secondRun", 0) is > 0.3 and var v ? Math.Min(v, len) : len;
                var opposite = Frame.Facing('N', -len / 2 + l2, -back + d);
                Build(b, opposite, s, new Run(l2, null, WallUnits: wall, Sink: sinkOther ? 1 - sinkAt : null, Hob: hobOther ? 1 - hobAt : null), d);
                break;
            }
            case "l-shaped":
            case "peninsula":
            {
                var pen = layout == "peninsula";
                var pc = pen ? PeninsulaZone(s) : 0;
                var main1 = new Run(len, MainTall(false, true), CapStart: d, WallUnits: wall, Sink: sinkOther ? null : Along(sinkAt), Hob: hobOther ? null : Along(hobAt), Main: true);
                Build(b, main, s, main1, d);
                // The leg runs from the corner to the front; a peninsula's run closes it across, so the leg stops short of it by a unit's depth.
                var legLen = dt - d - pc;
                var legFrame = Frame.Facing('E', -len / 2 + d, -back + pc);
                // Along the leg a fraction counts from the corner, at its end.
                Build(b, legFrame, s, new Run(legLen, tall == "left" && !pen ? "start" : null, WallUnits: wall, Sink: sinkOther && !pen ? 1 - sinkAt : null, Hob: hobOther && !pen ? 1 - hobAt : null), d);
                if (pen)
                {
                    var p = Math.Clamp(s.Num("peninsula", 1.2), 0.6, Math.Max(0.6, len - d - 0.3));
                    var over = Math.Clamp(s.Num("overhang", 0.3), 0, 0.5);
                    var close = Frame.Facing('N', -len / 2 + d + p, -back + pc);
                    Build(b, close, s, new Run(p + d, null, CapEnd: d, Sink: sinkOther ? 1 - sinkAt : null, Hob: hobOther ? 1 - hobAt : null, TopDepth: d + over, IslandHood: hobOther && s.Bool("islandHood")), d);
                }
                break;
            }
            case "u-shaped":
            {
                double leg1 = Math.Max(s.Num("leg", 2.4), d + MinFree), leg2 = Math.Max(s.Num("leg2", 2.4), d + MinFree);
                Build(b, main, s, new Run(len, null, CapStart: d, CapEnd: d, WallUnits: wall, Sink: sinkOther ? null : sinkAt, Hob: hobOther ? null : hobAt, Main: true), d);
                Build(b, Frame.Facing('E', -len / 2 + d, back - leg1), s, new Run(leg1 - d, tall == "left" ? "start" : null, WallUnits: wall, Sink: sinkOther ? 1 - sinkAt : null, Hob: hobOther ? 1 - hobAt : null), d);
                Build(b, Frame.Facing('W', len / 2 - d, back - d), s, new Run(leg2 - d, tall == "right" ? "end" : null, WallUnits: wall), d);
                break;
            }
            case "island":
            {
                Build(b, main, s, new Run(len, MainTall(true, true), WallUnits: wall, Sink: sinkOther ? null : sinkAt, Hob: hobOther ? null : hobAt, Main: true), d);
                var li = Math.Clamp(s.Num("islandLength", 1.8), 0.6, len);
                var di = Math.Max(d, s.Num("islandDepth", 0.9));
                var xc = -len / 2 + li / 2 + Math.Clamp(s.Num("islandAt", 0.5), 0, 1) * (len - li);
                Build(b, Frame.Facing('N', xc + li / 2, back - d - s.Num("gap", 1.1)), s, new Run(li, null, Sink: sinkOther ? 1 - sinkAt : null, Hob: hobOther ? 1 - hobAt : null, TopDepth: di, IslandHood: hobOther && s.Bool("islandHood")), d);
                break;
            }
            default:
                Build(b, main, s, new Run(len, MainTall(true, true), WallUnits: wall, Sink: Along(sinkAt), Hob: Along(hobAt), Main: true), d);
                break;
        }
    }
}
