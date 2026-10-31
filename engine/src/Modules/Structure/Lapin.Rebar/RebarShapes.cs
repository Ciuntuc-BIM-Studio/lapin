using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// A hook at a bar's end: its angle (degrees), its straight length (m) and the side it turns to:
/// the left of the bar walked from its start (as the end's hook turns counter-clockwise), or,
/// <see cref="Flip"/>, the right. Both hooks of a straight bar come out on one side by default.
/// </summary>
public sealed record Hook(double Angle, double Length, bool Flip = false)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["angle"] = Angle, ["length"] = Math.Round(Length, 6) };
        if (Flip) o["flip"] = true;
        return o;
    }

    public static Hook? From(JsonNode? n) => n is JsonObject o && Json.Number(o["angle"]) is { } a && a >= 0
        ? new Hook(a, Math.Max(0, Json.Number(o["length"]) ?? 0), o["flip"] is JsonValue v && v.TryGetValue<bool>(out var f) && f) : null;
}

/// <summary>One parameter of a shape: its key, label and default (m), and the least it may be.</summary>
public sealed record ShapeParam(string Key, string Label, double Default, double Min = 0.05);

/// <summary>A grip of a shape in its own frame: where it stands and the parameter it drives, read along one axis (x or y) of that frame.</summary>
public sealed record ShapeGrip(string Key, Vec2 At, string Param, char Axis);

/// <summary>
/// A shape of the catalogue (the configurator's "dynamic block"): its parameters, the nominal vertices
/// they give (on the bar's axis, meeting at the corners), its grips, the hooks it is born with (a stirrup's two
/// 135° hooks), the layer it draws on and the diameter it starts with.
/// </summary>
public sealed record ShapeDef(string Code, string Label, IReadOnlyList<ShapeParam> Params, string Layer, double DefaultDiameter,
    Func<IReadOnlyDictionary<string, double>, IReadOnlyList<Vec2>> Vertices, Func<IReadOnlyDictionary<string, double>, IReadOnlyList<ShapeGrip>> Grips,
    Func<double, (Hook? Start, Hook? End)>? BornHooks = null, bool FreeVertices = false);

/// <summary>A bar as drawn: its shape and parameters (m), its own vertices (a polyline), diameter (m), hooks, and how it is laid in its view.</summary>
public sealed record RebarSpec(string Shape, IReadOnlyDictionary<string, double> Params, IReadOnlyList<Vec2>? Vertices, double Diameter,
    Hook? Start, Hook? End, double Rotation = 0, bool MirrorX = false, bool MirrorY = false, double? BendRadius = null)
{
    public double this[string key] => Params.TryGetValue(key, out var v) ? v : RebarShapes.Def(Shape).Params.FirstOrDefault(p => p.Key == key)?.Default ?? 0;
}

/// <summary>A piece of the bar's real (bent) geometry: a straight length or an arc (radians; the sweep signed, counter-clockwise positive).</summary>
public abstract record Seg
{
    public abstract double Length { get; }
}
public sealed record SegLine(Vec2 A, Vec2 B) : Seg { public override double Length => (B - A).Length; }
public sealed record SegArc(Vec2 C, double R, double A0, double Sweep) : Seg
{
    public override double Length => R * Math.Abs(Sweep);
    public Vec2 At(double t) { var a = A0 + Sweep * t; return C + new Vec2(Math.Cos(a), Math.Sin(a)) * R; }
}

/// <summary>
/// The shapes of reinforcement (docs/plan-rebar.md, decision 209), after the configurator: parameters give the
/// nominal vertices on the bar's axis; the hooks add their straight tails as vertices; the corners are then bent
/// at the Eurocode radius (SR EN 1992-1-1 8.3: a mandrel of 4Ø up to Ø16, 7Ø over). The schedule's length is the
/// nominal one (the segments between the axis corners, plus the hooks, plus π·r for a 180° hook), as bar lists are
/// made; the real developed length is told beside it.
/// </summary>
public static class RebarShapes
{
    public static readonly double[] Diameters = [0.006, 0.008, 0.010, 0.012, 0.014, 0.016, 0.018, 0.020, 0.022, 0.025, 0.028, 0.032];
    public static readonly string[] Steels = ["B500C", "B500B", "BST 500S", "PC52", "OB37"];
    public const string DefaultSteel = "B500C";
    public const double SteelDensity = 7850;
    /// <summary>A hook of this angle or more is a 180° hook: a half turn, then its tail back along the bar.</summary>
    public const double HalfTurn = 175;
    public const string LongitudinalLayer = "Bare_Longitudinale";
    public const string StirrupLayer = "Etrieri";
    public const string AnnotationLayer = "Adnotatii";

    /// <summary>The mandrel's diameter (m): 4Ø up to Ø16, 7Ø over (SR EN 1992-1-1 table 8.1N).</summary>
    public static double Mandrel(double d) => d <= 0.016 + 1e-9 ? 4 * d : 7 * d;
    /// <summary>The bend's radius on the bar's axis (m): half the mandrel.</summary>
    public static double BendRadius(double d) => Mandrel(d) / 2;
    /// <summary>A hook's straight tail as practice has it (m): 12Ø up to 90°, 10Ø up to 135°, 5Ø beyond, never under 50 mm.</summary>
    public static double HookLength(double d, double angle) => Math.Max(0.05, angle <= 90 + 1e-9 ? 12 * d : angle <= 135 + 1e-9 ? 10 * d : 5 * d);
    /// <summary>kg per metre of a bar of diameter <paramref name="d"/> (m).</summary>
    public static double MassPerMetre(double d) => Math.PI * (d * 1000) * (d * 1000) / 4 * 1e-6 * SteelDensity;

    /// <summary>The colour a diameter is drawn in (the configurator's), when the project asks for colours by diameter.</summary>
    public static string ColorOf(double d) => (int)Math.Round(d * 1000) switch
    {
        6 => "#7c3aed", 8 => "#1d4ed8", 10 => "#0369a1", 12 => "#0f766e", 14 => "#15803d", 16 => "#ca8a04",
        18 => "#c2410c", 20 => "#b91c1c", 22 => "#be185d", 25 => "#6d28d9", 28 => "#0e7490", 32 => "#78350f", _ => "#000000",
    };

    static double P(IReadOnlyDictionary<string, double> p, string k, double d) => p.TryGetValue(k, out var v) ? v : d;

    static readonly ShapeParam A = new("a", "a", 0.8), B = new("b", "b", 0.4);

    public static readonly IReadOnlyList<ShapeDef> Catalogue =
    [
        new("straight", "Straight bar", [new("length", "Length", 1.0)], LongitudinalLayer, 0.012,
            p => [new(0, 0), new(P(p, "length", 1), 0)],
            p => [new("end", new(P(p, "length", 1), 0), "length", 'x')]),
        new("L", "L bar", [A, B], LongitudinalLayer, 0.012,
            p => [new(0, 0), new(P(p, "a", 0.8), 0), new(P(p, "a", 0.8), P(p, "b", 0.4))],
            p => [new("corner", new(P(p, "a", 0.8), 0), "a", 'x'), new("tip", new(P(p, "a", 0.8), P(p, "b", 0.4)), "b", 'y')]),
        new("U", "U bar", [new("a", "a", 0.6), new("b", "b", 0.5)], LongitudinalLayer, 0.012,
            p => [new(0, P(p, "b", 0.5)), new(0, 0), new(P(p, "a", 0.6), 0), new(P(p, "a", 0.6), P(p, "b", 0.5))],
            p => [new("width", new(P(p, "a", 0.6), 0), "a", 'x'), new("height", new(P(p, "a", 0.6), P(p, "b", 0.5)), "b", 'y')]),
        // A closed stirrup: round its four corners from the top left and back, each end hooked at 135° into the inside.
        new("stirrup", "Stirrup", [new("a", "Width a", 0.4), new("b", "Height b", 0.6)], StirrupLayer, 0.008,
            p => [new(0, P(p, "b", 0.6)), new(P(p, "a", 0.4), P(p, "b", 0.6)), new(P(p, "a", 0.4), 0), new(0, 0), new(0, P(p, "b", 0.6))],
            p => [new("width", new(P(p, "a", 0.4), P(p, "b", 0.6) / 2), "a", 'x'), new("height", new(P(p, "a", 0.4) / 2, P(p, "b", 0.6)), "b", 'y')],
            d => (new Hook(135, HookLength(d, 135), true), new Hook(135, HookLength(d, 135), true))),
        new("tie", "Tie", [new("height", "Height", 0.6)], StirrupLayer, 0.008,
            p => [new(0, 0), new(0, P(p, "height", 0.6))],
            p => [new("end", new(0, P(p, "height", 0.6)), "height", 'y')]),
        new("polyline", "Polyline bar", [new("bendRadius", "Bend radius", 0.04, 0)], LongitudinalLayer, 0.012,
            _ => [new(0, 0), new(0.6, 0), new(0.6, 0.4), new(1.0, 0.4)], _ => [], FreeVertices: true),
        // A bar seen end on: a dot of its diameter; its length is what it measures along the bar.
        new("dot", "Bar in section", [new("length", "Bar length", 1.0)], LongitudinalLayer, 0.012, _ => [], _ => []),
    ];

    public static ShapeDef Def(string code) => Catalogue.FirstOrDefault(s => s.Code == code) ?? throw new CommandException($"„{code}” is not a bar shape ({string.Join(", ", Catalogue.Select(s => s.Code))}).");
    public static bool IsShape(string? code) => code != null && Catalogue.Any(s => s.Code == code);

    /// <summary>The bend radius the bar is bent at: its own (a polyline's parameter) or the Eurocode's for its diameter.</summary>
    public static double RadiusOf(RebarSpec s) => s.BendRadius ?? (s.Shape == "polyline" && s.Params.TryGetValue("bendRadius", out var r) ? r : BendRadius(s.Diameter));

    /// <summary>The bar's nominal vertices in its own frame, hooks included as straight tails (a 180° hook is left to <see cref="Segments"/>).</summary>
    public static List<Vec2> Nominal(RebarSpec s)
    {
        var def = Def(s.Shape);
        var pts = new List<Vec2>(def.FreeVertices && s.Vertices is { Count: >= 2 } ? s.Vertices : def.Vertices(s.Params));
        if (pts.Count < 2) return pts;
        var (start, end) = HooksOf(s);
        if (start is { Angle: < HalfTurn } h0 && h0.Length > 1e-9)
        {
            var u = (pts[1] - pts[0]).Normalized();
            pts.Insert(0, pts[0] + Rot(-u, (h0.Flip ? 1 : -1) * h0.Angle * Math.PI / 180) * h0.Length);
        }
        if (end is { Angle: < HalfTurn } h1 && h1.Length > 1e-9)
        {
            var u = (pts[^1] - pts[^2]).Normalized();
            pts.Add(pts[^1] + Rot(u, (h1.Flip ? -1 : 1) * h1.Angle * Math.PI / 180) * h1.Length);
        }
        return pts;
    }

    /// <summary>The hooks the bar has: its own over the ones its shape is born with (a stirrup's).</summary>
    public static (Hook? Start, Hook? End) HooksOf(RebarSpec s)
    {
        var born = Def(s.Shape).BornHooks?.Invoke(s.Diameter) ?? (null, null);
        // A hook of angle 0 set on the bar: none, over the one its shape is born with.
        static Hook? Own(Hook? own, Hook? bornHook) => own == null ? bornHook : own.Angle <= 0 ? null : own;
        return (Own(s.Start, born.Start), Own(s.End, born.End));
    }

    /// <summary>The bar's length for the schedule (m): the nominal segments and hooks, plus π·r for each 180° hook; a dot's is its bar length.</summary>
    public static double Length(RebarSpec s)
    {
        if (s.Shape == "dot") return s["length"];
        var pts = Nominal(s);
        var total = 0.0;
        for (var i = 0; i + 1 < pts.Count; i++) total += (pts[i + 1] - pts[i]).Length;
        var (start, end) = HooksOf(s);
        foreach (var h in new[] { start, end })
            if (h is { Angle: >= HalfTurn }) total += Math.PI * RadiusOf(s) + h.Length;
        return total;
    }

    /// <summary>The real geometry in the bar's own frame: the corners bent at the radius, the 180° hooks as half turns with their tails.</summary>
    public static List<Seg> Segments(RebarSpec s)
    {
        var pts = Nominal(s);
        var r = RadiusOf(s);
        if (pts.Count < 2) return [];
        var segs = Fillet(pts, r);
        var (start, end) = HooksOf(s);
        if (end is { Angle: >= HalfTurn } h1) segs.AddRange(HalfTurnHook(pts[^1], (pts[^1] - pts[^2]).Normalized(), r, h1));
        if (start is { Angle: >= HalfTurn } h0) segs.InsertRange(0, HalfTurnHook(pts[0], (pts[0] - pts[1]).Normalized(), r, h0 with { Flip = !h0.Flip }).AsEnumerable().Reverse());
        return segs;
    }

    static List<Seg> HalfTurnHook(Vec2 e, Vec2 u, double r, Hook h)
    {
        var n = h.Flip ? -u.Perp : u.Perp;
        var c = e + n * r;
        var a0 = Math.Atan2(e.Y - c.Y, e.X - c.X);
        var far = e + n * (2 * r);
        return [new SegArc(c, r, a0, h.Flip ? -Math.PI : Math.PI), new SegLine(far, far - u * h.Length)];
    }

    /// <summary>The real developed length (m): along the axis, round the bends.</summary>
    public static double TrueLength(RebarSpec s) => s.Shape == "dot" ? s["length"] : Segments(s).Sum(x => x.Length);

    /// <summary>
    /// A polyline's corners bent at radius <paramref name="r"/>: the tangent points stand r·tan(turn/2) from the corner;
    /// a corner with no room for that (a leg shorter than the tangent distance) stays sharp.
    /// </summary>
    public static List<Seg> Fillet(IReadOnlyList<Vec2> pts, double r)
    {
        var segs = new List<Seg>();
        var cur = pts[0];
        for (var i = 1; i + 1 < pts.Count; i++)
        {
            var (p, b) = (pts[i], pts[i + 1]);
            var u = (p - cur).Normalized();
            var v = (b - p).Normalized();
            var cross = Vec2.Cross(u, v);
            var turn = Math.Atan2(Math.Abs(cross), Vec2.Dot(u, v));
            var t = r * Math.Tan(turn / 2);
            if (r <= 1e-9 || turn < 1e-6 || Math.PI - turn < 1e-6 || t > (p - cur).Length + 1e-9 || t > (b - p).Length + 1e-9)
            {
                segs.Add(new SegLine(cur, p));
                cur = p;
                continue;
            }
            var t1 = p - u * t;
            if ((t1 - cur).Length > 1e-9) segs.Add(new SegLine(cur, t1));
            var n = cross > 0 ? u.Perp : -u.Perp;
            var c = t1 + n * r;
            segs.Add(new SegArc(c, r, Math.Atan2(t1.Y - c.Y, t1.X - c.X), cross > 0 ? turn : -turn));
            cur = p + v * t;
        }
        if ((pts[^1] - cur).Length > 1e-9) segs.Add(new SegLine(cur, pts[^1]));
        return segs;
    }

    /// <summary>The bar's frame in its view: mirrored, turned, then set at <paramref name="at"/> (the configurator's R(θ)·diag(sx, sy)).</summary>
    public static Vec2 Place(RebarSpec s, Vec2 at, Vec2 p) => at + Rot(new Vec2(s.MirrorX ? -p.X : p.X, s.MirrorY ? -p.Y : p.Y), s.Rotation * Math.PI / 180);

    /// <summary>A point of the view back in the bar's own frame.</summary>
    public static Vec2 Local(RebarSpec s, Vec2 at, Vec2 p)
    {
        var q = Rot(p - at, -s.Rotation * Math.PI / 180);
        return new Vec2(s.MirrorX ? -q.X : q.X, s.MirrorY ? -q.Y : q.Y);
    }

    public static Seg Place(RebarSpec s, Vec2 at, Seg seg)
    {
        switch (seg)
        {
            case SegLine l: return new SegLine(Place(s, at, l.A), Place(s, at, l.B));
            case SegArc a:
            {
                var c = Place(s, at, a.C);
                var p0 = Place(s, at, a.At(0));
                var mirrored = s.MirrorX != s.MirrorY;
                return new SegArc(c, a.R, Math.Atan2(p0.Y - c.Y, p0.X - c.X), mirrored ? -a.Sweep : a.Sweep);
            }
            default: return seg;
        }
    }

    /// <summary>The segments as one chain of points to draw, arcs sampled at 7.5° or finer.</summary>
    public static List<Vec2> Chain(IReadOnlyList<Seg> segs)
    {
        var o = new List<Vec2>();
        void Add(Vec2 p) { if (o.Count == 0 || (o[^1] - p).Length > 1e-9) o.Add(p); }
        foreach (var seg in segs)
            switch (seg)
            {
                case SegLine l: Add(l.A); Add(l.B); break;
                case SegArc a:
                    var n = Math.Max(2, (int)Math.Ceiling(Math.Abs(a.Sweep) / (7.5 * Math.PI / 180)));
                    for (var k = 0; k <= n; k++) Add(a.At((double)k / n));
                    break;
            }
        return o;
    }

    /// <summary>The nominal vertices' box in the bar's own frame (a dot: its diameter about the origin).</summary>
    public static (Vec2 Min, Vec2 Max) Extent(RebarSpec s)
    {
        var pts = Nominal(s);
        if (pts.Count == 0) { var r = s.Diameter / 2; return (new Vec2(-r, -r), new Vec2(r, r)); }
        return (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y)));
    }

    /// <summary>The grips in the bar's own frame: its shape's, or a polyline's vertices (v0, v1…).</summary>
    public static List<ShapeGrip> Grips(RebarSpec s)
    {
        var def = Def(s.Shape);
        if (def.FreeVertices) return (s.Vertices ?? def.Vertices(s.Params)).Select((p, i) => new ShapeGrip($"v{i}", p, "", 'f')).ToList();
        return def.Grips(s.Params).ToList();
    }

    /// <summary>
    /// A grip dragged to <paramref name="local"/> (the bar's frame): the parameter it drives becomes the coordinate along
    /// its axis (never under the parameter's least), or the polyline's vertex moves there.
    /// </summary>
    public static RebarSpec Apply(RebarSpec s, string key, Vec2 local)
    {
        var def = Def(s.Shape);
        if (def.FreeVertices)
        {
            var pts = new List<Vec2>(s.Vertices ?? def.Vertices(s.Params));
            if (key.Length > 1 && key[0] == 'v' && int.TryParse(key[1..], out var i) && i >= 0 && i < pts.Count) pts[i] = local;
            return s with { Vertices = pts };
        }
        var grip = def.Grips(s.Params).FirstOrDefault(g => g.Key == key) ?? throw new CommandException($"The bar has no grip „{key}”.");
        var param = def.Params.First(p => p.Key == grip.Param);
        var value = Math.Max(param.Min, grip.Axis == 'x' ? local.X : local.Y);
        var next = new Dictionary<string, double>(s.Params) { [grip.Param] = Math.Round(value, 4) };
        return s with { Params = next };
    }

    public static Vec2 Rot(Vec2 v, double a)
    {
        var (c, sn) = (Math.Cos(a), Math.Sin(a));
        return new Vec2(v.X * c - v.Y * sn, v.X * sn + v.Y * c);
    }

    /// <summary>Millimetres as a label writes them ("2450").</summary>
    public static string Mm(double m) => Math.Round(m * 1000).ToString(CultureInfo.InvariantCulture);
    /// <summary>A diameter as a label writes it ("Ø12").</summary>
    public static string Dia(double d) => "Ø" + Math.Round(d * 1000).ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Where the bars of a distribution stand along a path of a length (m): at a step, the positions spread so the first
/// and the last fall on the ends (n = ⌈L/step + 1⌉, two at least); or by zones (a third at each end closer, for
/// a seismic beam), each zone with ⌈lz/step⌉ steps of at most the step asked, the last zone stretched to the end.
/// </summary>
public static class Runs
{
    public sealed record Zone(double Length, double Step)
    {
        public JsonObject ToJson() => new() { ["length"] = Math.Round(Length, 6), ["step"] = Math.Round(Step, 6) };
        public static Zone? From(JsonNode? n) => n is JsonObject o && Json.Number(o["length"]) is { } l && Json.Number(o["step"]) is { } s && l > 0 && s > 0 ? new Zone(l, s) : null;
    }

    public static List<double> Positions(double length, double step, IReadOnlyList<Zone>? zones = null)
    {
        if (length <= 1e-9) return [0];
        if (zones is not { Count: > 0 })
        {
            if (step <= 1e-9) return [0, length];
            var n = Math.Max(2, (int)Math.Ceiling(length / step + 1 - 1e-9));
            return Enumerable.Range(0, n).Select(k => length * k / (n - 1)).ToList();
        }
        var o = new List<double>();
        var at = 0.0;
        for (var z = 0; z < zones.Count && at < length - 1e-9; z++)
        {
            var last = z == zones.Count - 1;
            var lz = last ? length - at : Math.Min(zones[z].Length, length - at);
            var steps = Math.Max(1, (int)Math.Ceiling(lz / zones[z].Step - 1e-9));
            for (var k = 0; k < steps; k++) o.Add(at + lz * k / steps);
            at += lz;
        }
        o.Add(length);
        return o;
    }

    /// <summary>The step as a label writes it: one value, or the least and the greatest when the zones differ ("100-200").</summary>
    public static string StepText(double length, double step, IReadOnlyList<Zone>? zones)
    {
        if (zones is not { Count: > 0 }) return RebarShapes.Mm(step);
        var steps = new List<double>();
        var at = 0.0;
        for (var z = 0; z < zones.Count && at < length - 1e-9; z++)
        {
            var last = z == zones.Count - 1;
            var lz = last ? length - at : Math.Min(zones[z].Length, length - at);
            var n = Math.Max(1, (int)Math.Ceiling(lz / zones[z].Step - 1e-9));
            steps.Add(lz / n);
            at += lz;
        }
        var (lo, hi) = (steps.Min(), steps.Max());
        return Math.Abs(hi - lo) < 1e-6 ? RebarShapes.Mm(lo) : $"{RebarShapes.Mm(lo)}-{RebarShapes.Mm(hi)}";
    }
}
