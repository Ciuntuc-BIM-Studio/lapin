using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;

namespace Lapin.Civil;

/// <summary>
/// One piece of a lining's ring in its cross-section (x across, + left of the axis; y up from the red line): a straight segment
/// or an arc of a circle about <see cref="Centre"/> from angle <see cref="From"/> turning <see cref="Sweep"/> (+ anticlockwise).
/// Parameter t runs over [0, 1] from <see cref="A"/> to <see cref="B"/>.
/// </summary>
public sealed record Piece2(Vec2 A, Vec2 B, Vec2 Centre, double Radius, double From, double Sweep, bool Arc)
{
    public static Piece2 Line(Vec2 a, Vec2 b) => new(a, b, (a + b) * 0.5, 0, 0, 0, false);

    public static Piece2 ArcOf(Vec2 c, double r, double from, double sweep) =>
        new(c + new Vec2(Math.Cos(from), Math.Sin(from)) * r, c + new Vec2(Math.Cos(from + sweep), Math.Sin(from + sweep)) * r, c, r, from, sweep, true);

    public Vec2 At(double t) => Arc ? Centre + new Vec2(Math.Cos(From + Sweep * t), Math.Sin(From + Sweep * t)) * Radius : A + (B - A) * t;

    /// <summary>dP/dt.</summary>
    public Vec2 Tangent(double t) => Arc ? new Vec2(-Math.Sin(From + Sweep * t), Math.Cos(From + Sweep * t)) * (Radius * Sweep) : B - A;

    public double Length => Arc ? Math.Abs(Sweep) * Radius : (B - A).Length;

    /// <summary>The piece as a curve in the plane z = 0 (an arc exactly, as a rational quadratic), on [0, 1].</summary>
    public BSplineCurve Curve() => Arc
        ? BSplineCurve.Arc(new Vec3(Centre.X, Centre.Y, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0), Radius, From, Sweep)
        : BSplineCurve.Line(new Vec3(A.X, A.Y, 0), new Vec3(B.X, B.Y, 0));

    /// <summary>The piece's parameters by the chord rule (<see cref="Chords"/>): both ends, and as many between as its bending needs.</summary>
    public double[] Samples()
    {
        if (!Arc) return [0, 1];
        var n = Chords.Count(Radius, Math.Abs(Sweep), 1);
        return [.. Enumerable.Range(0, n + 1).Select(k => (double)k / n)];
    }

    /// <summary>The piece mirrored across the axis (x → −x), run the other way so the ring it is in keeps turning anticlockwise.</summary>
    public Piece2 Mirrored()
    {
        static Vec2 M(Vec2 p) => new(-p.X, p.Y);
        return Arc ? ArcOf(M(Centre), Radius, Math.PI - (From + Sweep), Sweep) : Line(M(B), M(A));
    }

    /// <summary>The piece moved by (dx, dy).</summary>
    public Piece2 Moved(double dx, double dy)
    {
        var d = new Vec2(dx, dy);
        return this with { A = A + d, B = B + d, Centre = Centre + d };
    }
}

/// <summary>A closed ring of pieces, anticlockwise (x, y as the section draws them): its area and its first moment about the axis.</summary>
public sealed record LiningRing(IReadOnlyList<Piece2> Pieces)
{
    /// <summary>Whether its first piece starts on the axis at its bottom and its pieces reach the axis again at its top (a symmetric ring, cut in halves there).</summary>
    public int? Half { get; init; }

    static readonly double[] GaussX = [-0.9602898564975363, -0.7966664774136267, -0.5255324099163290, -0.1834346424956498, 0.1834346424956498, 0.5255324099163290, 0.7966664774136267, 0.9602898564975363];
    static readonly double[] GaussW = [0.1012285362903763, 0.2223810344533745, 0.3137066458778873, 0.3626837833783620, 0.3626837833783620, 0.3137066458778873, 0.2223810344533745, 0.1012285362903763];

    /// <summary>∮ f(x, y) dy over the ring (Gauss, 8 points a piece, two halves an arc of more than 45°).</summary>
    double LineIntegral(Func<double, double, double> f)
    {
        var sum = 0.0;
        foreach (var p in Pieces)
        {
            var parts = p.Arc ? Math.Max(1, (int)Math.Ceiling(Math.Abs(p.Sweep) / (Math.PI / 4))) : 1;
            for (var k = 0; k < parts; k++)
                for (var g = 0; g < GaussX.Length; g++)
                {
                    var t = (k + (GaussX[g] + 1) / 2) / parts;
                    var q = p.At(t);
                    sum += GaussW[g] / 2 / parts * f(q.X, q.Y) * p.Tangent(t).Y;
                }
        }
        return sum;
    }

    /// <summary>The area inside it (∮ x dy).</summary>
    public double Area => LineIntegral((x, _) => x);

    /// <summary>The first moment of its area about the axis, ∫ x dA = ∮ x²/2 dy.</summary>
    public double Qx => LineIntegral((x, _) => x * x / 2);

    public double Perimeter => Pieces.Sum(p => p.Length);

    /// <summary>The ring's points by the chord rule, each piece's last left for the next one's first.</summary>
    public List<Vec2> Points() => [.. Pieces.SelectMany(p => p.Samples().SkipLast(1).Select(p.At))];

    public (double MinX, double MaxX, double MinY, double MaxY) Box()
    {
        var pts = Pieces.SelectMany(p => p.Samples().Select(p.At)).ToList();
        // An arc's extremes between its samples: at its quarter turns.
        foreach (var p in Pieces.Where(p => p.Arc))
            for (var q = -4; q <= 8; q++)
            {
                var a = q * Math.PI / 2;
                var (lo, hi) = p.Sweep >= 0 ? (p.From, p.From + p.Sweep) : (p.From + p.Sweep, p.From);
                if (a > lo && a < hi) pts.Add(p.Centre + new Vec2(Math.Cos(a), Math.Sin(a)) * p.Radius);
            }
        return (pts.Min(p => p.X), pts.Max(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.Y));
    }

    /// <summary>
    /// The ring offset outward by <paramref name="d"/>: each arc about its centre (its radius grown, or shrunk where it turns inward),
    /// each line along its normal, and an arc of radius d round every corner where two pieces do not meet tangent.
    /// </summary>
    public LiningRing Offset(double d)
    {
        var o = new List<Piece2>();
        for (var i = 0; i < Pieces.Count; i++)
        {
            var p = Pieces[i];
            var next = Pieces[(i + 1) % Pieces.Count];
            if (p.Arc) o.Add(Piece2.ArcOf(p.Centre, p.Radius + Math.Sign(p.Sweep) * d, p.From, p.Sweep));
            else
            {
                var dir = (p.B - p.A).Normalized();
                var n = new Vec2(dir.Y, -dir.X);
                o.Add(Piece2.Line(p.A + n * d, p.B + n * d));
            }
            // The corner after it: the turn from its end direction to the next one's start.
            var t0 = p.Tangent(1).Normalized();
            var t1 = next.Tangent(0).Normalized();
            var turn = Math.Atan2(t0.X * t1.Y - t0.Y * t1.X, Vec2.Dot(t0, t1));
            if (turn > 1e-9)
            {
                var a0 = Math.Atan2(-t0.X, t0.Y);
                o.Add(Piece2.ArcOf(p.B, d, a0, turn));
            }
        }
        var bottom = Half;
        // Kept halved: a corner's arc added before the half's piece shifts it on.
        if (bottom is { } h)
        {
            var count = 0;
            for (var i = 0; i < h; i++)
            {
                count++;
                var p = Pieces[i];
                var t0 = p.Tangent(1).Normalized();
                var t1 = Pieces[(i + 1) % Pieces.Count].Tangent(0).Normalized();
                if (Math.Atan2(t0.X * t1.Y - t0.Y * t1.X, Vec2.Dot(t0, t1)) > 1e-9) count++;
            }
            return new LiningRing(o) { Half = count };
        }
        return new LiningRing(o);
    }
}

/// <summary>A lining's section: the clear opening's ring (the intrados), the lining's outside (the extrados), and the outer shell's outside when it has one.</summary>
public sealed record LiningSection(LiningRing Inner, LiningRing Outer, LiningRing? Primary, IReadOnlyList<string> Problems)
{
    /// <summary>The outermost ring.</summary>
    public LiningRing Outside => Primary ?? Outer;
}

/// <summary>
/// The tunnel lining's cross-sections (docs/plan-wing-walls.md, S4): the coded presets — a horseshoe (NATM: crown, sides, corners
/// and invert as tangent arcs), a circle (a TBM's segmental ring), a cut-and-cover box with haunches, a vault on upright walls — each
/// from a few numbers that may be formulas, or a profile drawn and saved (its outside and its hole). Every section is in x across
/// (+ left of the axis) and y up from the red line (the road's level on the axis).
/// </summary>
public static class TunnelSections
{
    public static readonly string[] Presets = ["horseshoe", "circular", "box", "vault"];

    static Dictionary<string, string[]> When(params string[] presets) => new() { ["preset"] = presets };

    public static readonly Portals.Param[] Params =
    [
        new("clearWidth", "Clear width", "length", "Section", 10.6, Min: 1, Max: 40, ShowIf: When("horseshoe", "box", "vault")),
        new("clearHeight", "Clear height over the road", "length", "Section", 7.0, Min: 1, Max: 30, ShowIf: When("horseshoe", "box")),
        new("crownRadius", "Crown radius", "length", "Section", 5.0, Min: 0.5, Max: 100, ShowIf: When("horseshoe", "vault")),
        new("sideRadius", "Side radius", "length", "Section", 7.0, Min: 0.5, Max: 200, ShowIf: When("horseshoe")),
        new("cornerRadius", "Corner radius", "length", "Section", 1.0, Min: 0, Max: 20, ShowIf: When("horseshoe")),
        new("invertRadius", "Invert radius", "length", "Section", 14.0, Min: 1, Max: 1000, ShowIf: When("horseshoe")),
        new("springHeight", "Springing over the road", "length", "Section", 3.2, Min: 0, Max: 30, ShowIf: When("vault")),
        new("innerRadius", "Inner radius", "length", "Section", 4.75, Min: 0.5, Max: 30, ShowIf: When("circular")),
        new("centreHeight", "Centre over the road", "length", "Section", 2.2, Min: -30, Max: 30, ShowIf: When("circular")),
        new("haunch", "Haunch", "length", "Section", 0.5, Min: 0, Max: 5, ShowIf: When("box")),
        new("invertDepth", "Invert under the road", "length", "Section", 1.6, Min: 0, Max: 20, ShowIf: When("horseshoe", "box", "vault")),
        new("thickness", "Lining thickness (crown, roof, ring)", "length", "Lining", 0.35, Min: 0.05, Max: 5),
        new("wallThickness", "Wall thickness", "length", "Lining", "{= thickness}", Min: 0.05, Max: 5, ShowIf: When("horseshoe", "box", "vault")),
        new("invertThickness", "Invert thickness", "length", "Lining", "{= thickness}", Min: 0.05, Max: 5, ShowIf: When("horseshoe", "box", "vault")),
        new("primary", "Outer shell (shotcrete; 0: none)", "length", "Lining", 0.0, Min: 0, Max: 2),
        new("blockLength", "Casting block length", "length", "Blocks", 12.5, Min: 0.5, Max: 100, ShowIf: When("horseshoe", "box", "vault")),
        new("ringWidth", "Ring width", "length", "Blocks", 1.8, Min: 0.3, Max: 5, ShowIf: When("circular")),
        new("outerFormwork", "Outer face formworked", "bool", "Blocks", false, Formula: false),
        new("ifc", "IFC", "choice", "Output", "assembly", Choices: ["assembly", "body"], Formula: false),
        new("material", "Material", "text", "Output", "Reinforced concrete", Formula: false),
        new("primaryMaterial", "Outer shell material", "text", "Output", "Shotcrete", Formula: false),
    ];

    /// <summary>What each preset sets over the defaults.</summary>
    public static JsonObject PresetOf(string preset) => preset switch
    {
        "circular" => new() { ["thickness"] = 0.4, ["primary"] = 0.0, ["outerFormwork"] = false },
        "box" => new() { ["clearWidth"] = 10.0, ["clearHeight"] = 5.4, ["haunch"] = 0.5, ["invertDepth"] = 0.3, ["thickness"] = 0.8, ["wallThickness"] = 0.6, ["invertThickness"] = 0.9, ["primary"] = 0.0, ["blockLength"] = 10.0, ["outerFormwork"] = true },
        "vault" => new() { ["clearWidth"] = 9.0, ["springHeight"] = 3.2, ["crownRadius"] = "{= clearWidth / 2}", ["invertDepth"] = 0.5, ["thickness"] = 0.4, ["wallThickness"] = 0.5, ["invertThickness"] = 0.6, ["primary"] = 0.0, ["blockLength"] = 10.0, ["outerFormwork"] = true },
        _ => new() { ["invertThickness"] = 0.5, ["primary"] = 0.25, ["outerFormwork"] = false },
    };

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
        var preset = (string?)spec?["preset"] is { } pr && Presets.Contains(pr) ? pr : "horseshoe";
        foreach (var (k, v) in PresetOf(preset)) o[k] = v?.DeepClone();
        o["preset"] = preset;
        if (spec != null) foreach (var (k, v) in spec) if (k != "preset") o[k] = v?.DeepClone();
        return o;
    }

    /// <summary>The spec's numbers: plain ones as they are, formulas worked out (reading each other by key, then the project's parameters).</summary>
    public static Portals.Values Evaluate(JsonObject? given, IModelView? m) => Portals.EvaluateWith(Params, Full(given), m);

    /// <summary>The section the values make: the preset's, or the profile drawn (its outside and its hole) when one is given; problems said, defaults standing for what does not hold.</summary>
    public static LiningSection Build(Portals.Values v, IReadOnlyList<IReadOnlyList<Vec2>>? drawn = null)
    {
        var problems = new List<string>(v.Problems);
        var preset = v.Text("preset", "horseshoe");
        double N(string key, double fallback) => v.Maybe(key) ?? fallback;
        var t = N("thickness", 0.35);
        var tw = N("wallThickness", t);
        var ti = N("invertThickness", t);
        LiningRing inner, outer;
        if (drawn != null && Drawn(drawn, N("invertDepth", 0.5), problems) is { } d) (inner, outer) = d;
        else
            switch (preset)
            {
                case "circular":
                {
                    var r = N("innerRadius", 4.75);
                    var h = N("centreHeight", 2.2);
                    inner = Circle(new Vec2(0, h), r);
                    outer = Circle(new Vec2(0, h), r + t);
                    break;
                }
                case "box":
                    (inner, outer) = Box(N("clearWidth", 10), N("clearHeight", 5.4), N("invertDepth", 0.3), N("haunch", 0.5), t, tw, ti, problems);
                    break;
                case "vault":
                    (inner, outer) = Vault(N("clearWidth", 9), N("springHeight", 3.2), N("crownRadius", 4.5), N("invertDepth", 0.5), t, tw, ti, problems);
                    break;
                default:
                {
                    var (w, hc, r1, r2, r4, r3, dd) = (N("clearWidth", 10.6), N("clearHeight", 7), N("crownRadius", 5), N("sideRadius", 7), N("cornerRadius", 1), N("invertRadius", 14), N("invertDepth", 1.6));
                    var i = Horseshoe(w, hc, r1, r2, r4, r3, dd, problems, "");
                    var o = Horseshoe(w + 2 * tw, hc + t, r1 + t, r2 + tw, r4 + (tw + ti) / 2, r3 + ti, dd + ti, problems, "Outside: ");
                    if (i == null || o == null)
                    {
                        problems.Add("The horseshoe does not close with these radii: the default one is shown.");
                        i = Horseshoe(10.6, 7, 5, 7, 1, 14, 1.6, [], "")!;
                        o = Horseshoe(10.6 + 2 * tw, 7 + t, 5 + t, 7 + tw, 1 + (tw + ti) / 2, 14 + ti, 1.6 + ti, [], "") ?? i.Offset(t);
                    }
                    (inner, outer) = (i, o);
                    break;
                }
            }
        var tp = N("primary", 0);
        LiningRing? primary = null;
        if (tp > 1e-6)
        {
            if (drawn != null) problems.Add("A drawn section has no outer shell: draw it as its own ring.");
            else primary = outer.Offset(tp);
        }
        return new LiningSection(inner, outer, primary, problems.Distinct().ToList());
    }

    /// <summary>A whole circle in four quarter arcs, from its bottom on.</summary>
    public static LiningRing Circle(Vec2 c, double r) =>
        new([.. Enumerable.Range(0, 4).Select(k => Piece2.ArcOf(c, r, -Math.PI / 2 + k * Math.PI / 2, Math.PI / 2))]) { Half = 2 };

    /// <summary>A ring's right half (from its bottom on the axis up to its top on it) and its mirror, anticlockwise.</summary>
    static LiningRing Symmetric(List<Piece2> right) =>
        new([.. right, .. Enumerable.Reverse(right).Select(p => p.Mirrored())]) { Half = right.Count };

    /// <summary>
    /// A horseshoe (NATM): the crown arc about the axis reaching <paramref name="hc"/> over the road; the side arcs, tangent to it
    /// inside, widest at <paramref name="w"/>; the invert arc reaching <paramref name="d"/> under the road; a corner arc tangent to
    /// both (none at radius 0). Null when the radii do not close.
    /// </summary>
    public static LiningRing? Horseshoe(double w, double hc, double r1, double r2, double r4, double r3, double d, List<string> problems, string prefix)
    {
        var half = w / 2;
        if (r1 > half + 1e-9) { problems.Add($"{prefix}the crown radius is at most half the clear width."); r1 = half; }
        if (r2 < half - 1e-9) { problems.Add($"{prefix}the side radius is at least half the clear width."); r2 = half; }
        if (r2 < r1) r2 = r1;
        var c1 = new Vec2(0, hc - r1);
        Vec2 c2;
        Vec2 u;
        if (r2 - r1 < 1e-9) { c2 = c1; u = new Vec2(1, 0); }
        else
        {
            var c2x = half - r2;
            var dd = r2 - r1;
            c2 = new Vec2(c2x, c1.Y - Math.Sqrt(Math.Max(0, dd * dd - c2x * c2x)));
            u = (c1 - c2).Normalized();
        }
        var a1 = Math.Atan2(u.Y, u.X);
        var c3 = new Vec2(0, -d + r3);
        if (r3 <= r2) { problems.Add($"{prefix}the invert radius is more than the side radius."); return null; }
        List<Piece2> right;
        if (r4 < 1e-6)
        {
            // The side and the invert meet at a corner: where their circles cross, on the right.
            var x = Crossing(c2, r2, c3, r3);
            if (x == null) { problems.Add($"{prefix}the side and the invert do not meet."); return null; }
            var a2 = Math.Atan2(x.Value.Y - c2.Y, x.Value.X - c2.X);
            var a3 = Math.Atan2(x.Value.Y - c3.Y, x.Value.X - c3.X);
            right = [Piece2.ArcOf(c3, r3, -Math.PI / 2, a3 + Math.PI / 2), Piece2.ArcOf(c2, r2, a2, a1 - a2)];
        }
        else
        {
            if (r4 >= r2) { problems.Add($"{prefix}the corner radius is less than the side radius."); return null; }
            var c4n = Crossing(c2, r2 - r4, c3, r3 - r4);
            if (c4n == null) { problems.Add($"{prefix}the corner arc cannot be tangent to both the side and the invert."); return null; }
            var c4 = c4n.Value;
            var v24 = (c4 - c2).Normalized();
            var v34 = (c4 - c3).Normalized();
            var a2 = Math.Atan2(v24.Y, v24.X);
            var a3 = Math.Atan2(v34.Y, v34.X);
            right = [Piece2.ArcOf(c3, r3, -Math.PI / 2, a3 + Math.PI / 2), Piece2.ArcOf(c4, r4, a3, a2 - a3), Piece2.ArcOf(c2, r2, a2, a1 - a2)];
        }
        if (r2 - r1 >= 1e-9) right.Add(Piece2.ArcOf(c1, r1, a1, Math.PI / 2 - a1));
        else right[^1] = Piece2.ArcOf(c1, r1, right[^1].From, Math.PI / 2 - right[^1].From);
        if (right.Any(p => p.Sweep <= 1e-9) || right.Any(p => p.A.X < -1e-9 || p.B.X < -1e-9)) { problems.Add($"{prefix}the arcs do not follow round in order with these radii."); return null; }
        return Symmetric(right);
    }

    /// <summary>The crossing of two circles on the right (the larger x), or null.</summary>
    static Vec2? Crossing(Vec2 a, double ra, Vec2 b, double rb)
    {
        var dv = b - a;
        var d = dv.Length;
        if (d < 1e-12 || d > ra + rb || d < Math.Abs(ra - rb)) return null;
        var x = (ra * ra - rb * rb + d * d) / (2 * d);
        var h = Math.Sqrt(Math.Max(0, ra * ra - x * x));
        var e = dv * (1 / d);
        var m = a + e * x;
        var p1 = m + e.Perp * h;
        var p2 = m - e.Perp * h;
        return p1.X >= p2.X ? p1 : p2;
    }

    /// <summary>A cut-and-cover box: its floor <paramref name="d"/> under the road, its clear height over the floor, 45° haunches under its roof (and a third of them over its floor).</summary>
    static (LiningRing, LiningRing) Box(double w, double h, double d, double haunch, double tr, double tw, double tb, List<string> problems)
    {
        var half = w / 2;
        var hb = Math.Min(haunch / 3, half / 4);
        if (haunch > Math.Min(half, h) / 2) { problems.Add("The haunch is at most a quarter of the clear width or height."); haunch = Math.Min(half, h) / 2; }
        var (y0, y1) = (-d, -d + h);
        var pts = new List<Vec2> { new(0, y0), new(half - hb, y0), new(half, y0 + hb), new(half, y1 - haunch), new(half - haunch, y1), new(0, y1) };
        var right = Lines(pts);
        var outer = Lines([new(0, y0 - tb), new(half + tw, y0 - tb), new(half + tw, y1 + tr), new(0, y1 + tr)]);
        return (Symmetric(right), Symmetric(outer));
    }

    /// <summary>A vault on upright walls: the walls up to the springing, an arc of the crown radius over them (a half circle at half the width); the floor flat.</summary>
    static (LiningRing, LiningRing) Vault(double w, double hs, double r, double d, double tc, double tw, double tb, List<string> problems)
    {
        var half = w / 2;
        if (r < half - 1e-9) { problems.Add("The crown radius is at least half the clear width."); r = half; }
        var cy = hs - Math.Sqrt(Math.Max(0, r * r - half * half));
        var a0 = Math.Atan2(hs - cy, half);
        var right = Lines([new(0, -d), new(half, -d), new(half, hs)]);
        right.Add(Piece2.ArcOf(new Vec2(0, cy), r, a0, Math.PI / 2 - a0));
        var ow = half + tw;
        var ro = r + tc;
        var outer = Lines([new(0, -d - tb), new(ow, -d - tb)]);
        if (ro > ow + 1e-9)
        {
            // Concentric over the inside: the arc meets the outer wall where it crosses it.
            var y = cy + Math.Sqrt(ro * ro - ow * ow);
            outer.Add(Piece2.Line(new(ow, -d - tb), new(ow, y)));
            var a = Math.Atan2(y - cy, ow);
            outer.Add(Piece2.ArcOf(new Vec2(0, cy), ro, a, Math.PI / 2 - a));
        }
        else
        {
            // A half circle on the outer walls, tangent to them, its crown the crown's thickness over the inside's.
            var oc = cy + r + tc - ow;
            outer.Add(Piece2.Line(new(ow, -d - tb), new(ow, oc)));
            outer.Add(Piece2.ArcOf(new Vec2(0, oc), ow, 0, Math.PI / 2));
        }
        return (Symmetric(right), Symmetric(outer));
    }

    static List<Piece2> Lines(List<Vec2> pts)
    {
        var o = new List<Piece2>();
        for (var i = 0; i + 1 < pts.Count; i++) if ((pts[i + 1] - pts[i]).Length > 1e-9) o.Add(Piece2.Line(pts[i], pts[i + 1]));
        return o;
    }

    /// <summary>
    /// A drawn section: its biggest ring the outside, the ring inside it the clear opening; centred across on the outside's box, the
    /// opening's lowest point <paramref name="d"/> under the road. Null (and why) when it has no hole.
    /// </summary>
    static (LiningRing, LiningRing)? Drawn(IReadOnlyList<IReadOnlyList<Vec2>> rings, double d, List<string> problems)
    {
        var ordered = rings.Where(r => r.Count >= 3).OrderByDescending(r => Math.Abs(Polygon2.SignedArea(r))).ToList();
        if (ordered.Count < 2) { problems.Add("A drawn section needs its outside and the clear opening inside it: the preset is shown."); return null; }
        List<Vec2> Ccw(IReadOnlyList<Vec2> r) { var l = r.ToList(); if (Polygon2.SignedArea(l) < 0) l.Reverse(); return l; }
        var outside = Ccw(ordered[0]);
        var hole = Ccw(ordered[1]);
        var cx = (outside.Min(p => p.X) + outside.Max(p => p.X)) / 2;
        var dy = -d - hole.Min(p => p.Y);
        LiningRing Ring(List<Vec2> pts) => new([.. pts.Select((p, i) => Piece2.Line(p, pts[(i + 1) % pts.Count]).Moved(-cx, dy)).Where(p => p.Length > 1e-9)]);
        return (Ring(hole), Ring(outside));
    }

    /// <summary>The rings of a saved profile (a project's copy), or null.</summary>
    public static IReadOnlyList<IReadOnlyList<Vec2>>? ProfileRings(IModelView m, string? id) =>
        id is { Length: > 0 } && m.Get(id) is { Class: ProjectProfiles.Class } p && ProjectProfiles.Read(p)?.Rings is { Count: >= 2 } rings ? [.. rings.Select(r => (IReadOnlyList<Vec2>)r.ToList())] : null;
}
