using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// Entourage (docs/plan-visualisation.md, stage 4): people and cars that give a site its scale, in 3D, in plans,
/// elevations and sections, on sheets. Each is a <c>LapinEntourage</c> at a point (<c>at</c>), turned by
/// <c>rotation</c> (degrees), standing on the ground (the terrain, else its storey) raised by <c>offset</c>, of a
/// <c>kind</c>, as tall as its <c>height</c> (the kind's when not set), its clothes or body in <c>color</c>.
/// <para>
/// The figures follow BimGo's entourage (Aussie BIM Guru, MIT-0; docs/acknowledgements.md): its people's proportions
/// (the head about 1/7.5 of the height, a child's 1/5.5), their joints and stances, and its cars' side profiles, there
/// drawn as pictures and here built as low-poly bodies: a person's limbs tapered from joint to joint, the torso in
/// rings, the head an ellipsoid under its hair; a car's profile carried across its width, its cabin glass under the
/// roof, its wheels.
/// </para>
/// In the local frame x runs to the right of a person (along a car, its front at +x), a person faces −y, z is up.
/// Exported as IfcBuildingElementProxy USERDEFINED "Entourage", and left out of an IFC export unless asked for.
/// </summary>
public static class Entourage
{
    public const string Class = "LapinEntourage";
    public const string ObjectType = "Entourage";

    /// <summary>A kind: its key, label, category (person or car), default height, length and width (a car's; a person's from the height) and main colour.</summary>
    public sealed record Kind(string Key, string Label, string Category, double Height, double Length, double Width, string Color);

    public static readonly IReadOnlyList<Kind> Kinds =
    [
        new("man", "Man standing", "person", 1.78, 0, 0, "#3e5c7a"),
        new("woman", "Woman walking", "person", 1.66, 0, 0, "#b5483b"),
        new("child", "Child", "person", 1.15, 0, 0, "#e0a93b"),
        new("phone", "Person on the phone", "person", 1.72, 0, 0, "#6b7f5a"),
        new("car", "Car", "car", 1.45, 4.6, 1.8, "#8c96a0"),
        new("suv", "SUV / van", "car", 1.85, 4.9, 1.95, "#5c6e7e"),
    ];

    public static Kind KindOf(Entity e) => Kinds.FirstOrDefault(k => k.Key == e.Str("kind")) ?? Kinds[0];
    public static Kind? Find(string? key) => Kinds.FirstOrDefault(k => k.Key == key);

    /// <summary>How tall it is: its own height, else its kind's.</summary>
    public static double HeightOf(Entity e) => e.NumOrNull("height") is { } h && h > 0.2 ? h : KindOf(e).Height;

    static readonly string[] Skins = ["#e0b39a", "#c68e6a", "#8d5b3e", "#f1cdb3"];
    const string Shoes = "#2a2a2a", Glass = "#2e3a44", Tyre = "#1c1c1e", Rim = "#a9afb5";

    /// <summary>Where it stands and how it is turned: the local frame's mapping to the world.</summary>
    readonly record struct Frame(Vec2 At, double Z, double Cos, double Sin)
    {
        public Vec3 P(double x, double y, double z) => new(At.X + Cos * x - Sin * y, At.Y + Sin * x + Cos * y, Z + z);
        public Vec3 D(double x, double y, double z) => new(Cos * x - Sin * y, Sin * x + Cos * y, z);
        public Vec2 Q(double x, double y) => new(At.X + Cos * x - Sin * y, At.Y + Sin * x + Cos * y);
    }

    static Frame FrameOf(IModelView m, Entity e)
    {
        var at = Args.Point(e.Attrs["at"], "the entourage");
        var terrain = Plantings.TerrainFor(m, e);
        var ground = terrain?.ElevationAt(at.X, at.Y) ?? Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0;
        var r = e.Num("rotation") * Math.PI / 180;
        return new Frame(at, ground + e.Num("offset"), Math.Cos(r), Math.Sin(r));
    }

    static int StableHash(string s) { unchecked { var h = 2166136261u; foreach (var c in s) h = (h ^ c) * 16777619u; return (int)(h & 0x7fffffff); } }

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        var kind = KindOf(e);
        var f = FrameOf(m, e);
        var color = e.Str("color") is { Length: 7 } c && c[0] == '#' ? c : kind.Color;
        var parts = new Dictionary<string, (string Color, List<Solid> Solids)>();
        void Add(string part, string col, Solid? s)
        {
            if (s == null) return;
            if (!parts.TryGetValue(part, out var p)) parts[part] = p = (col, []);
            p.Solids.Add(s);
        }
        var plan = new List<PlanItem>();
        var silhouettes = new List<Lapin.Drawing.Views.Silhouette>();
        var foot = new Vec3(f.At.X, f.At.Y, f.Z);
        if (kind.Category == "car")
        {
            var shape = Car(f, kind, HeightOf(e), color, Add, plan);
            plan.AddRange(Entourage2d.CarPlan(shape, f.Q));
            silhouettes.AddRange(Entourage2d.CarSilhouettes(shape, foot, new Vec2(f.Cos, f.Sin)));
        }
        else
        {
            Person(f, kind.Key, HeightOf(e), color, Skins[StableHash(e.Id) % Skins.Length], Add, plan);
            plan.AddRange(Entourage2d.PersonPlan(kind.Key, HeightOf(e), f.Q));
            silhouettes.AddRange(Entourage2d.PersonSilhouettes(kind.Key, HeightOf(e), foot));
        }
        var list = parts.Select(p => new GeometryPart(p.Key, p.Value.Color, Paths.Combine(p.Value.Solids, "entourage"), null, p.Key)).ToList();
        return new ElementGeometry(list, plan, [f.At], Silhouettes: silhouettes);
    }

    // ---- People ----

    /// <summary>A limb along a chain of joints [u, v, r, …] (units of the height, in the figure's plane): rings across the chain, tapered joint to joint.</summary>
    static Solid? Limb(Frame f, double k, double[] c, int sides = 6)
    {
        var n = c.Length / 3;
        if (n < 2) return null;
        var pts = Enumerable.Range(0, n).Select(i => new Vec3(c[i * 3] * k, 0, c[i * 3 + 1] * k)).ToArray();
        var rings = new List<IReadOnlyList<Vec3>>();
        for (var i = 0; i < n; i++)
        {
            var t = (pts[Math.Min(n - 1, i + 1)] - pts[Math.Max(0, i - 1)]).Normalized();
            if (t == Vec3.Zero) t = -Vec3.UnitZ;
            var a = new Vec3(0, 1, 0);
            var b = Vec3.Cross(t, a).Normalized();
            var r = Math.Max(0.004, c[i * 3 + 2] * k);
            rings.Add([.. Enumerable.Range(0, sides).Select(j =>
            {
                var w = 2 * Math.PI * j / sides;
                var p = pts[i] + b * (r * Math.Cos(w)) + a * (r * Math.Sin(w));
                return f.P(p.X, p.Y, p.Z);
            })]);
        }
        return Lofts.Rings(rings, "limb");
    }

    /// <summary>A body of rings down its side [u, v, …] (half-widths at heights, top to bottom), as deep as <paramref name="depth"/> times its width.</summary>
    static Solid? Torso(Frame f, double k, double[] side, double depth = 0.62, double y = 0, int sides = 10)
    {
        var rings = new List<IReadOnlyList<Vec3>>();
        for (var i = 0; i + 1 < side.Length; i += 2)
        {
            var hw = Math.Max(0.012, side[i]) * k;
            var hd = Math.Max(0.012, side[i] * depth) * k;
            var z = side[i + 1] * k;
            rings.Add([.. Enumerable.Range(0, sides).Select(j => { var w = 2 * Math.PI * j / sides; return f.P(hw * Math.Cos(w), y * k + hd * Math.Sin(w), z); })]);
        }
        return Lofts.Rings(rings, "torso");
    }

    /// <summary>An ellipsoid about (u, y, v), its half-sizes ru across, rd deep and rv high (units of the height).</summary>
    static Solid? Ellipsoid(Frame f, double k, double u, double y, double v, double ru, double rd, double rv, int sides = 10, int bands = 7)
    {
        var rings = new List<IReadOnlyList<Vec3>>();
        for (var i = 0; i <= bands; i++)
        {
            var th = Math.PI * (0.04 + 0.92 * i / bands);
            var s = Math.Sin(th);
            var z = (v - rv * Math.Cos(th)) * k;
            rings.Add([.. Enumerable.Range(0, sides).Select(j => { var w = 2 * Math.PI * j / sides; return f.P((u + ru * s * Math.Cos(w)) * k, (y + rd * s * Math.Sin(w)) * k, z); })]);
        }
        return Lofts.Rings(rings, "head");
    }

    /// <summary>A box in the local frame from its centre and sizes.</summary>
    static Solid? Box(Frame f, double x, double y, double z, double sx, double sy, double sz) =>
        Prism.Extrude([f.P(x - sx / 2, y - sy / 2, z - sz / 2), f.P(x + sx / 2, y - sy / 2, z - sz / 2), f.P(x + sx / 2, y + sy / 2, z - sz / 2), f.P(x - sx / 2, y + sy / 2, z - sz / 2)], null, f.D(0, 0, sz), "box");

    static void Person(Frame f, string kind, double height, string top, string skin, Action<string, string, Solid?> add, List<PlanItem> plan)
    {
        // BimGo's figures stand in units of their height, the top of the head at 0.993.
        var k = height / 0.993;
        string hair = "#2b2018", bottom = "#2e3440";
        void Shoe(double u, double lift = 0) => add("shoes", Shoes, Box(f, u * k, -0.03 * k, (0.018 + lift) * k, 0.055 * k, 0.15 * k, 0.036 * k));
        void Head(double cv, double ru, double rv, bool longHair = false)
        {
            add("skin", skin, Ellipsoid(f, k, 0, 0, cv - rv * 0.06, ru * 0.94, ru * 0.9, rv * 0.92));
            add("hair", hair, Ellipsoid(f, k, 0, ru * 0.22, cv + rv * 0.08, ru * 1.04, ru * 0.9, rv * 1.0));
            if (longHair) add("hair", hair, Torso(f, k, [ru * 0.9, cv + rv * 0.3, ru * 1.05, cv - rv * 0.4, ru * 0.95, cv - rv * 1.6], 0.45, ru * 0.55, 8));
        }
        double shoulder, chest;
        switch (kind)
        {
            case "woman":
                hair = "#5a3a22";
                add("skin", skin, Limb(f, k, [0.045, 0.5, 0.046, 0.06, 0.39, 0.04, 0.072, 0.29, 0.028, 0.082, 0.2, 0.029, 0.092, 0.1, 0.017]));
                Shoe(0.094, 0.05);
                add("skin", skin, Limb(f, k, [-0.04, 0.5, 0.047, -0.042, 0.39, 0.041, -0.04, 0.285, 0.029, -0.042, 0.19, 0.03, -0.04, 0.045, 0.018]));
                Shoe(-0.04);
                add("skin", skin, Limb(f, k, [0.1, 0.79, 0.03, 0.118, 0.70, 0.026, 0.122, 0.625, 0.02, 0.112, 0.56, 0.021, 0.098, 0.5, 0.015, 0.094, 0.465, 0.019, 0.09, 0.44, 0.012]));
                add("skin", skin, Limb(f, k, [-0.1, 0.79, 0.03, -0.122, 0.70, 0.026, -0.135, 0.625, 0.02, -0.148, 0.56, 0.021, -0.158, 0.5, 0.015, -0.162, 0.465, 0.019, -0.163, 0.44, 0.012]));
                // The dress to the knee, flaring.
                add("top", top, Torso(f, k, [0.012, 0.845, 0.03, 0.843, 0.098, 0.82, 0.116, 0.792, 0.112, 0.75, 0.1, 0.70, 0.078, 0.62, 0.098, 0.53, 0.12, 0.4, 0.135, 0.31], 0.7));
                add("skin", skin, Limb(f, k, [0.0, 0.885, 0.019, 0.0, 0.835, 0.021]));
                Head(0.93, 0.045, 0.062, longHair: true);
                shoulder = 0.116; chest = 0.07;
                break;
            case "child":
                hair = "#7a5230"; bottom = "#3a5a8a";
                add("skin", skin, Limb(f, k, [-0.055, 0.44, 0.055, -0.058, 0.33, 0.046, -0.06, 0.22, 0.036, -0.06, 0.13, 0.038, -0.06, 0.05, 0.026]));
                add("skin", skin, Limb(f, k, [0.055, 0.44, 0.055, 0.062, 0.33, 0.046, 0.07, 0.22, 0.036, 0.074, 0.13, 0.038, 0.078, 0.05, 0.026]));
                Shoe(-0.06); Shoe(0.078);
                add("bottom", bottom, Torso(f, k, [0.11, 0.53, 0.118, 0.42, 0.122, 0.32], 0.6));
                // One arm down, one waving.
                add("skin", skin, Limb(f, k, [0.11, 0.69, 0.038, 0.13, 0.6, 0.033, 0.138, 0.52, 0.027, 0.14, 0.45, 0.026, 0.141, 0.4, 0.019, 0.144, 0.36, 0.024, 0.142, 0.335, 0.015]));
                add("skin", skin, Limb(f, k, [-0.11, 0.69, 0.038, -0.15, 0.62, 0.032, -0.19, 0.58, 0.027, -0.215, 0.64, 0.026, -0.235, 0.7, 0.019, -0.245, 0.74, 0.025, -0.25, 0.77, 0.016]));
                add("top", top, Torso(f, k, [0.012, 0.76, 0.035, 0.758, 0.11, 0.735, 0.13, 0.68, 0.112, 0.645, 0.11, 0.56, 0.115, 0.5], 0.62));
                add("skin", skin, Limb(f, k, [0.0, 0.8, 0.025, 0.0, 0.75, 0.028]));
                Head(0.895, 0.078, 0.098);
                shoulder = 0.13; chest = 0.075;
                break;
            case "phone":
                hair = "#1e1a16"; bottom = "#4a4038";
                add("bottom", bottom, Limb(f, k, [-0.048, 0.53, 0.05, -0.05, 0.39, 0.044, -0.05, 0.28, 0.031, -0.052, 0.19, 0.032, -0.054, 0.05, 0.02]));
                add("bottom", bottom, Limb(f, k, [0.048, 0.53, 0.05, 0.05, 0.39, 0.044, 0.05, 0.28, 0.031, 0.052, 0.19, 0.032, 0.054, 0.05, 0.02]));
                Shoe(-0.056); Shoe(0.056);
                add("top", top, Limb(f, k, [-0.11, 0.79, 0.034, -0.128, 0.70, 0.03, -0.136, 0.62, 0.024, -0.142, 0.55, 0.025, -0.145, 0.49, 0.017]));
                add("skin", skin, Limb(f, k, [-0.145, 0.495, 0.018, -0.148, 0.45, 0.021, -0.146, 0.425, 0.013]));
                add("bag", "#6b4a33", Box(f, -0.175 * k, 0, 0.455 * k, 0.03 * k, 0.09 * k, 0.15 * k));
                add("bag", "#6b4a33", Limb(f, k, [0.06, 0.84, 0.009, -0.04, 0.7, 0.009, -0.16, 0.53, 0.009], 4));
                // The coat to mid-thigh.
                add("top", top, Torso(f, k, [0.012, 0.848, 0.034, 0.846, 0.112, 0.82, 0.132, 0.79, 0.118, 0.70, 0.104, 0.6, 0.112, 0.5, 0.118, 0.4], 0.64));
                // The hand at the ear, the phone in it.
                add("top", top, Limb(f, k, [0.11, 0.79, 0.034, 0.138, 0.71, 0.03, 0.146, 0.665, 0.026, 0.11, 0.76, 0.025, 0.07, 0.86, 0.019]));
                add("skin", skin, Limb(f, k, [0.072, 0.855, 0.02, 0.06, 0.9, 0.022, 0.05, 0.925, 0.014]));
                add("phone", Tyre, Box(f, 0.046 * k, 0, 0.91 * k, 0.012 * k, 0.03 * k, 0.07 * k));
                add("skin", skin, Limb(f, k, [0.0, 0.885, 0.021, 0.0, 0.835, 0.024]));
                Head(0.93, 0.046, 0.063);
                shoulder = 0.132; chest = 0.08;
                break;
            default:
                // Standing at ease, the weight on one leg, a hand in a pocket.
                add("bottom", bottom, Limb(f, k, [-0.05, 0.55, 0.052, -0.05, 0.40, 0.046, -0.046, 0.285, 0.032, -0.047, 0.19, 0.034, -0.046, 0.05, 0.021]));
                add("bottom", bottom, Limb(f, k, [0.05, 0.55, 0.052, 0.058, 0.40, 0.045, 0.068, 0.285, 0.031, 0.075, 0.19, 0.033, 0.083, 0.055, 0.021]));
                Shoe(-0.05); Shoe(0.088, 0.006);
                add("bottom", bottom, Torso(f, k, [0.098, 0.585, 0.103, 0.5, 0.07, 0.47], 0.6));
                add("top", top, Limb(f, k, [0.11, 0.79, 0.034, 0.124, 0.70, 0.03, 0.13, 0.615, 0.024, 0.134, 0.55, 0.025, 0.136, 0.485, 0.017]));
                add("skin", skin, Limb(f, k, [0.136, 0.49, 0.018, 0.139, 0.445, 0.021, 0.137, 0.415, 0.013]));
                add("top", top, Limb(f, k, [-0.11, 0.79, 0.034, -0.128, 0.70, 0.03, -0.138, 0.625, 0.024, -0.118, 0.585, 0.025, -0.092, 0.555, 0.02]));
                add("top", top, Torso(f, k, [0.012, 0.848, 0.032, 0.846, 0.112, 0.82, 0.132, 0.79, 0.116, 0.70, 0.097, 0.61, 0.1, 0.555], 0.6));
                add("skin", skin, Limb(f, k, [0.0, 0.885, 0.021, 0.0, 0.835, 0.024]));
                Head(0.93, 0.047, 0.063);
                shoulder = 0.132; chest = 0.08;
                break;
        }
        // In plan, detailed (decision 341: the views' simpler symbols are Entourage2d's): the shoulders seen from above and the head, the nose's side marked by a tick.
        plan.Add(new PlanItem("symbol", true, Ellipse(f, 0, 0, shoulder * k, chest * k, 16), Mode: Entourage2d.Detailed));
        plan.Add(new PlanItem("symbol", true, Ellipse(f, 0, 0, 0.05 * k, 0.05 * k, 12), Mode: Entourage2d.Detailed));
        plan.Add(new PlanItem("symbol", false, [f.Q(0, -0.05 * k), f.Q(0, -(chest + 0.03) * k)], Mode: Entourage2d.Detailed));
    }

    static List<Vec2> Ellipse(Frame f, double x, double y, double rx, double ry, int n) =>
        [.. Enumerable.Range(0, n).Select(i => { var w = 2 * Math.PI * i / n; return f.Q(x + rx * Math.Cos(w), y + ry * Math.Sin(w)); })];

    // ---- Cars ----

    /// <summary>A profile's part below (<paramref name="below"/>) or above a level (Sutherland–Hodgman on one line).</summary>
    static List<(double X, double Z)> Clip(IReadOnlyList<(double X, double Z)> poly, double level, bool below)
    {
        var o = new List<(double X, double Z)>();
        bool In((double X, double Z) p) => below ? p.Z <= level : p.Z >= level;
        for (var i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            if (In(a)) o.Add(a);
            if (In(a) != In(b)) { var t = (level - a.Z) / (b.Z - a.Z); o.Add((a.X + (b.X - a.X) * t, level)); }
        }
        return o;
    }

    static Solid? Across(Frame f, IReadOnlyList<(double X, double Z)> profile, double width, string tag) =>
        profile.Count < 3 ? null : Prism.Extrude([.. profile.Select(p => f.P(p.X, -width / 2, p.Z))], null, f.D(0, width, 0), tag);

    static Entourage2d.CarShape Car(Frame f, Kind kind, double height, string body, Action<string, string, Solid?> add, List<PlanItem> plan)
    {
        var suv = kind.Key == "suv";
        // BimGo's side profiles (its canvas units), front to the right.
        double[] raw = suv
            ? [-1.5, 0.2, 1.5, 0.2, 1.55, 0.55, 1.35, 0.68, 0.95, 0.72, 0.62, 1.0, -1.45, 1.0, -1.55, 0.7]
            : [-1.55, 0.18, 1.55, 0.18, 1.6, 0.42, 1.45, 0.55, 0.75, 0.6, 0.35, 0.95, -0.75, 0.95, -1.15, 0.62, -1.58, 0.55];
        double x0 = suv ? -1.55 : -1.58, x1 = suv ? 1.55 : 1.6, top = suv ? 1.0 : 0.95, belt = suv ? 0.72 : 0.6;
        var scale = height / top;
        var sx = kind.Length / (x1 - x0) * (height / kind.Height);
        var width = kind.Width * (height / kind.Height);
        var cx = (x0 + x1) / 2;
        var profile = Enumerable.Range(0, raw.Length / 2).Select(i => ((raw[i * 2] - cx) * sx, raw[i * 2 + 1] * scale)).ToList();
        add("body", body, Across(f, Clip(profile, belt * scale, true), width, "body"));
        var cabin = Clip(profile, belt * scale, false);
        add("glass", Glass, Across(f, Clip(cabin, (top - 0.05) * scale, true), width * 0.84, "glass"));
        add("body", body, Across(f, Clip(cabin, (top - 0.05) * scale, false), width * 0.88, "roof"));
        // The wheels, standing on the ground, a little inside the body's sides.
        var wr = (suv ? 0.24 : 0.21) * scale;
        var wx = (suv ? 0.95 : 1.0) * sx;
        foreach (var x in new[] { -wx, wx })
            foreach (var side in new[] { -1, 1 })
            {
                double y0 = side * (width / 2 + 0.01), y1 = side * (width / 2 - 0.22);
                IReadOnlyList<Vec3> Ring(double y, double r) => [.. Enumerable.Range(0, 14).Select(j => { var w = 2 * Math.PI * (j + 0.5) / 14; return f.P(x + r * Math.Cos(w), y, wr + r * Math.Sin(w)); })];
                add("tyres", Tyre, Lofts.Rings([Ring(y0, wr), Ring(y1, wr)], "tyre"));
                add("rims", Rim, Lofts.Rings([Ring(y0 + side * 0.005, wr * 0.55), Ring(y0 - side * 0.02, wr * 0.55)], "rim"));
            }
        // Head and tail lights.
        double front = (x1 - cx) * sx, back = (x0 - cx) * sx;
        foreach (var side in new[] { -1, 1 })
        {
            add("lights", "#f2e6b0", Box(f, front - 0.05, side * (width / 2 - 0.3), 0.47 * scale, 0.12, 0.3, 0.07 * scale));
            add("lights", "#b03028", Box(f, back + 0.04, side * (width / 2 - 0.25), 0.48 * scale, 0.1, 0.25, 0.07 * scale));
        }
        // In plan, detailed: its outline with the corners rounded off, and the cabin's glass.
        double hw = width / 2, ch = Math.Min(0.3, hw * 0.35);
        const string detailed = Entourage2d.Detailed;
        plan.Add(new PlanItem("symbol", true, [f.Q(back + ch, -hw), f.Q(front - ch, -hw), f.Q(front, -hw + ch), f.Q(front, hw - ch), f.Q(front - ch, hw), f.Q(back + ch, hw), f.Q(back, hw - ch), f.Q(back, -hw + ch)], Mode: detailed));
        var glass = cabin.Count >= 3 ? (cabin.Min(p => p.X), cabin.Max(p => p.X)) : (back * 0.5, front * 0.3);
        var gh = hw * 0.84;
        plan.Add(new PlanItem("symbol", true, [f.Q(glass.Item1, -gh), f.Q(glass.Item2, -gh * 0.92), f.Q(glass.Item2, gh * 0.92), f.Q(glass.Item1, gh)], Mode: detailed));
        var roof = Clip(cabin, (top - 0.05) * scale, false);
        if (roof.Count >= 3) plan.Add(new PlanItem("symbol", true, [f.Q(roof.Min(p => p.X), -gh * 0.9), f.Q(roof.Max(p => p.X), -gh * 0.9), f.Q(roof.Max(p => p.X), gh * 0.9), f.Q(roof.Min(p => p.X), gh * 0.9)], Mode: detailed));
        // What the drawings' simple symbols are made from (decision 341).
        return new Entourage2d.CarShape(back, front, hw, profile, profile.Min(p => p.Item2), belt * scale, height, wr, wx, glass.Item2);
    }

    // ---- Commands ----

    public static void Register(IModuleRegistry r)
    {
        // People and cars: one at a point ({x, y, rotation?}), or a row along a line ({points: [[x, y], [x, y]], spacing?}),
        // cars parked across it, people walking along it; {kind, height?, color?, offset?, storeyId?}. One undo step.
        r.Command("addEntourage", "Entourage", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var kind = Find((string?)a["kind"] ?? "man") ?? throw new CommandException($"An entourage is one of {string.Join(", ", Kinds.Select(k => k.Key))}.");
            var places = new List<(Vec2 At, double Rotation)>();
            if (a["points"] is JsonArray { Count: >= 2 })
            {
                var pts = Args.Points(a);
                var d = pts[^1] - pts[0];
                var length = d.Length;
                if (length < 0.1) throw new CommandException("Draw the row longer.");
                var spacing = Json.Number(a["spacing"]) is { } s && s > 0.3 ? s : kind.Category == "car" ? 2.5 : 1.2;
                var dir = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
                // A car stands across the row (parked side by side, its front to the row's left); a person faces along it.
                var rot = dir + 90;
                var n = Math.Min(200, (int)Math.Floor(length / spacing) + 1);
                for (var i = 0; i < n; i++) places.Add((pts[0] + d * (i * spacing / length), rot));
            }
            else places.Add((Args.Point(a, "x", "y"), Json.Number(a["rotation"]) ?? 0));
            var ids = new JsonArray();
            var count = ctx.Model.OfClass(Class).Count(x => KindOf(x).Key == kind.Key);
            foreach (var (at, rot) in places)
            {
                var attrs = new JsonObject
                {
                    ["kind"] = kind.Key, ["at"] = new JsonArray(at.X, at.Y), ["rotation"] = Math.Round(((rot % 360) + 360) % 360, 3),
                    ["exportAs"] = IfcClass.BuildingElementProxy, ["objectType"] = ObjectType,
                };
                if (Json.Number(a["height"]) is { } h && h > 0.2) attrs["height"] = h;
                if (Json.Number(a["offset"]) is { } off) attrs["offset"] = off;
                if ((string?)a["color"] is { Length: 7 } col && col[0] == '#') attrs["color"] = col;
                var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"{kind.Label} {++count}";
                var e = new Entity(ctx.NewId(), Class, name, attrs, "USERDEFINED");
                ctx.Add(e);
                Spatial.Contain(ctx, storeyId, e.Id);
                ids.Add((JsonNode)e.Id);
            }
            return new JsonObject { ["id"] = (string?)ids[0], ["ids"] = ids };
        });
    }
}
