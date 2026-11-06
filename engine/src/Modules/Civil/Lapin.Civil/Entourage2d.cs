using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// People, cars and plants as drawings show them (decision 341): simple symbols, as an architect draws entourage, never
/// the detailed bodies' edges. Each figure gives its plan items and its silhouettes in every mode a view style may ask
/// (<see cref="ViewStyles.EntourageModes"/>), each item marked with its mode:
/// <list type="bullet">
/// <item><c>outline</c> (the default): in plan a person's shoulders and head, a car's outline with its corners rounded and
/// its windscreen, a plant's crown outline by its family with a cross at a tree's trunk; in elevations and sections one
/// contour each, a person's figure, a car's side or front, a tree's crown and trunk, a bush's dome;</item>
/// <item><c>abstract</c>: circles and boxes, a person a circle, a car a rounded box, a tree a circle and its trunk;</item>
/// <item><c>detailed</c>: the symbols drawn before (the plants' branches and needles, decision 234) and the bodies'
/// edges in elevations; <c>none</c>: nothing.</item>
/// </list>
/// A few segments each, so a site plan of hundreds of trees stays light.
/// </summary>
public static class Entourage2d
{
    public const string Outline = "outline", Abstract = "abstract", Detailed = "detailed";

    static Vec2 Polar(double r, double a) => new(r * Math.Cos(a), r * Math.Sin(a));

    static List<Vec2> Ring(double rx, double ry, int n, double turn = 0) =>
        [.. Enumerable.Range(0, n).Select(i => { var w = turn + 2 * Math.PI * i / n; return new Vec2(rx * Math.Cos(w), ry * Math.Sin(w)); })];

    static PlanItem Item(IEnumerable<Vec2> pts, bool closed, string mode) => new("symbol", closed, [.. pts], Mode: mode);

    static Silhouette Facing(Vec3 at, string mode, params SilhouetteStroke[] strokes) => new(at, strokes, Mode: mode);

    /// <summary>A contour symmetric about its axis: its right half from the top down; the left mirrored back up.</summary>
    static List<Vec2> Mirrored(IReadOnlyList<Vec2> right)
    {
        var o = new List<Vec2>(right);
        for (var i = right.Count - 1; i >= 0; i--)
            if (Math.Abs(right[i].X) > 1e-9) o.Add(new Vec2(-right[i].X, right[i].Y));
        return o;
    }

    // ---- People ----

    /// <summary>A figure's proportions in units of its height: the head's radius and centre, the shoulders' half-width and height, the chest's half-depth (plan), the hips', a dress's hem (0: none), the crotch's height.</summary>
    sealed record Figure(double Head, double HeadZ, double Shoulder, double ShoulderZ, double Chest, double Hip, double Hem, double Crotch);

    static Figure FigureOf(string kind) => kind switch
    {
        "child" => new(0.085, 0.9, 0.13, 0.735, 0.075, 0.115, 0, 0.42),
        "woman" => new(0.062, 0.93, 0.117, 0.82, 0.07, 0.1, 0.135, 0.46),
        _ => new(0.063, 0.93, 0.132, 0.82, 0.08, 0.105, 0, 0.46),
    };

    /// <summary>A person's plan items, in its own frame (x to its right, facing −y), placed by <paramref name="q"/>.</summary>
    public static IEnumerable<PlanItem> PersonPlan(string kind, double height, Func<double, double, Vec2> q)
    {
        var f = FigureOf(kind);
        IEnumerable<Vec2> Placed(IEnumerable<Vec2> pts) => pts.Select(p => q(p.X, p.Y));
        yield return Item(Placed(Ring(f.Shoulder * height, f.Chest * height, 12)), true, Outline);
        yield return Item(Placed(Ring(f.Head * height * 0.9, f.Head * height * 0.9, 8)), true, Outline);
        yield return Item(Placed(Ring(f.Shoulder * height, f.Shoulder * height, 12)), true, Abstract);
    }

    /// <summary>A person's silhouettes, standing at <paramref name="at"/>, always turned to the viewer.</summary>
    public static IEnumerable<Silhouette> PersonSilhouettes(string kind, double height, Vec3 at)
    {
        var f = FigureOf(kind);
        double H = f.Head, S = f.Shoulder, P = f.Hip;
        var half = new List<Vec2>();
        // The head from its top round its right side to the neck.
        foreach (var deg in new[] { 90.0, 50, 10, -30, -62 })
        {
            var w = deg * Math.PI / 180;
            half.Add(new Vec2(H * Math.Cos(w), f.HeadZ + H * Math.Sin(w)));
        }
        half.Add(new(H * 0.42, f.ShoulderZ + 0.025));
        // The shoulder, the arm down the side to the hand, back in to the hip.
        half.Add(new(S * 0.82, f.ShoulderZ));
        half.Add(new(S, f.ShoulderZ - 0.04));
        half.Add(new(S + 0.008, f.ShoulderZ - 0.2));
        var hand = f.Crotch + 0.01;
        half.Add(new(S - 0.004, hand + 0.02));
        half.Add(new(S - 0.03, hand));
        half.Add(new(P, f.Crotch + 0.07));
        if (f.Hem > 0)
        {
            // A dress flaring to the knee, the legs under it.
            half.Add(new(f.Hem, 0.32));
            half.Add(new(P * 0.55, 0.31));
        }
        else half.Add(new(P * 0.95, 0.4));
        half.Add(new(P * 0.6, 0.05));
        half.Add(new(P * 0.75, 0));
        half.Add(new(0.015, 0));
        half.Add(new(0.015, f.Hem > 0 ? 0.3 : f.Crotch));
        var contour = Mirrored(half).Select(p => p * height).ToList();
        yield return Facing(at, Outline, new SilhouetteStroke(contour));
        // Abstract: the head a circle over a tapered body.
        var head = Ring(H * height, H * height, 10).Select(p => p + new Vec2(0, f.HeadZ * height)).ToList();
        List<Vec2> body = [new(-S * 0.8, f.ShoulderZ), new(S * 0.8, f.ShoulderZ), new(S, f.ShoulderZ - 0.06), new(S * 0.55, 0), new(-S * 0.55, 0), new(-S, f.ShoulderZ - 0.06)];
        yield return Facing(at, Abstract, new SilhouetteStroke(head), new SilhouetteStroke([.. body.Select(p => p * height)]));
    }

    // ---- Cars ----

    /// <summary>A car's sizes in its own frame (x along it, its front at +x; z up): its ends, half-width, side profile, sill, belt and roof, the wheels' radius and offset, the windscreen's x.</summary>
    public sealed record CarShape(double Back, double Front, double HalfWidth, IReadOnlyList<(double X, double Z)> Profile, double Sill, double Belt, double Top, double WheelR, double WheelX, double Windscreen);

    /// <summary>A rounded rectangle from <paramref name="x0"/> to <paramref name="x1"/> and ±<paramref name="hw"/>, each corner cut by <paramref name="r"/> in <paramref name="steps"/> steps.</summary>
    static List<Vec2> Rounded(double x0, double x1, double hw, double r, int steps)
    {
        var o = new List<Vec2>();
        (double X, double Y, double A)[] corners = [(x1 - r, -hw + r, -Math.PI / 2), (x1 - r, hw - r, 0), (x0 + r, hw - r, Math.PI / 2), (x0 + r, -hw + r, Math.PI)];
        foreach (var (cx, cy, a0) in corners)
            for (var j = 0; j <= steps; j++) o.Add(new Vec2(cx, cy) + Polar(r, a0 + Math.PI / 2 * j / steps));
        return o;
    }

    public static IEnumerable<PlanItem> CarPlan(CarShape c, Func<double, double, Vec2> q)
    {
        IEnumerable<Vec2> Placed(IEnumerable<Vec2> pts) => pts.Select(p => q(p.X, p.Y));
        var r = Math.Min(0.35, c.HalfWidth * 0.4);
        yield return Item(Placed(Rounded(c.Back, c.Front, c.HalfWidth, r, 2)), true, Outline);
        // The windscreen: one line across where the cabin's glass starts.
        var g = c.HalfWidth * 0.82;
        yield return Item(Placed([new(c.Windscreen, -g), new(c.Windscreen, g)]), false, Outline);
        yield return Item(Placed(Rounded(c.Back, c.Front, c.HalfWidth, r, 1)), true, Abstract);
    }

    /// <summary>A car's silhouettes: its side when seen more from the side, its front otherwise.</summary>
    public static IEnumerable<Silhouette> CarSilhouettes(CarShape c, Vec3 at, Vec2 axis)
    {
        // The side: the body's profile, its bottom running over the wheels, each an arc down to the ground.
        var side = new List<Vec2> { new(c.Profile[0].X, c.Profile[0].Z) };
        var below = Math.Clamp((c.Sill - c.WheelR) / c.WheelR, -0.95, 0.95);
        var a = Math.Asin(below);
        foreach (var wx in new[] { -c.WheelX, c.WheelX })
            for (var j = 0; j <= 6; j++)
            {
                // From the arc's back crossing of the sill, under the wheel, to its front crossing.
                var w = Math.PI - a + (Math.PI + 2 * a) * j / 6;
                side.Add(new Vec2(wx, c.WheelR) + Polar(c.WheelR, w));
            }
        side.AddRange(c.Profile.Skip(1).Select(p => new Vec2(p.X, p.Z)));
        // The front: the body to its belt, the cabin narrower over it, the tyres under the sill at the sides.
        double hw = c.HalfWidth, t = Math.Min(0.24, hw * 0.25);
        var front = Mirrored([
            new(0, c.Top), new(hw * 0.7, c.Top * 0.985), new(hw * 0.86, c.Belt + 0.04), new(hw, c.Belt - 0.04), new(hw, c.Sill + 0.08),
            new(hw - 0.02, c.Sill), new(hw - 0.02, 0), new(hw - 0.02 - t, 0), new(hw - 0.02 - t, c.Sill), new(0, c.Sill)]);
        yield return new Silhouette(at, [new(side)], [new(front)], axis, Outline);
        // Abstract: the profile alone standing on the ground, the front a box under its cabin.
        var box = c.Profile.Select(p => new Vec2(p.X, p.Z <= c.Sill + 1e-9 ? 0 : p.Z)).ToList();
        List<Vec2> boxFront = [new(-hw, 0), new(hw, 0), new(hw, c.Belt), new(hw * 0.78, c.Top), new(-hw * 0.78, c.Top), new(-hw, c.Belt)];
        yield return new Silhouette(at, [new(box)], [new(boxFront)], axis, Abstract);
    }

    // ---- Plants ----

    /// <summary>A soft lobed ring: <paramref name="lobes"/> rounded bulges of four points, a shallow notch between them.</summary>
    static List<Vec2> Lobes(double r, int lobes, double notch, double turn)
    {
        double[] k = [1 - notch, 1 - notch * 0.25, 1, 1 - notch * 0.25];
        return [.. Enumerable.Range(0, lobes * 4).Select(i => Polar(r * k[i % 4], turn + 2 * Math.PI * i / (lobes * 4)))];
    }

    /// <summary>A plant's plan items at its place: its crown's outline by its family, a cross at a tree's trunk; the detailed symbol (decision 234) for that mode.</summary>
    public static IEnumerable<PlanItem> PlantPlan(Species sp, Vec2 at, double r, int seed)
    {
        var family = sp.DrawnAs;
        if (family == "ground" || sp.Type == "grass") yield break;
        var turn = new SiteGeometry.Random(seed).Next() * 2 * Math.PI;
        var crown = family switch
        {
            "broadleaf" => Lobes(r, 7, 0.1, turn),
            "shrub" => Lobes(r, 6, 0.14, turn),
            "fir" => [.. Enumerable.Range(0, 20).Select(i => Polar(r * (i % 2 == 0 ? 1 : 0.8), turn + Math.PI * i / 10))],
            "pine" => [.. Enumerable.Range(0, 24).Select(i => Polar(r * (i % 2 == 0 ? 1 : 0.95), turn + Math.PI * i / 12))],
            "hedge" => Ring(r, r, 12),
            _ => Ring(r, r, 16, turn),
        };
        yield return new PlanItem("symbol", true, [.. crown.Select(p => p + at)], Mode: Outline);
        yield return new PlanItem("symbol", true, [.. Ring(r, r, sp.Type == "tree" ? 16 : 12).Select(p => p + at)], Mode: Abstract);
        if (sp.Type == "tree")
        {
            var c = Math.Clamp(r * 0.1, 0.1, 0.3);
            foreach (var mode in new[] { Outline, Abstract })
            {
                yield return new PlanItem("symbol", false, [at + new Vec2(-c, 0), at + new Vec2(c, 0)], Mode: mode);
                yield return new PlanItem("symbol", false, [at + new Vec2(0, -c), at + new Vec2(0, c)], Mode: mode);
            }
        }
        foreach (var item in Plantings.Symbol(family, at, r, seed)) yield return item with { Mode = Detailed };
    }

    /// <summary>A plant's silhouettes, standing at <paramref name="at"/> (its foot), <paramref name="h"/> tall and <paramref name="r"/> wide, always turned to the viewer.</summary>
    public static IEnumerable<Silhouette> PlantSilhouettes(Species sp, Vec3 at, double h, double r)
    {
        var family = sp.DrawnAs;
        if (family == "ground" || sp.Type == "grass") yield break;
        if (sp.Type == "bush")
        {
            // A dome on the ground; a clipped hedge a box with its top corners rounded.
            List<Vec2> dome = family == "hedge"
                ? [new(r, 0), new(r, h * 0.85), new(r * 0.85, h), new(-r * 0.85, h), new(-r, h * 0.85), new(-r, 0)]
                : [.. Enumerable.Range(0, 13).Select(i => { var w = Math.PI * i / 12; var k = family == "shrub" && i % 2 == 1 ? 0.95 : 1; return new Vec2(r * k * Math.Cos(w), h * k * Math.Sin(w)); })];
            yield return Facing(at, Outline, new SilhouetteStroke(dome));
            yield return Facing(at, Abstract, new SilhouetteStroke([.. Enumerable.Range(0, 9).Select(i => new Vec2(r * Math.Cos(Math.PI * i / 8), h * Math.Sin(Math.PI * i / 8)))]));
            yield break;
        }
        var t = Math.Clamp(r * 0.06, 0.08, 0.25);
        if (family == "fir")
        {
            // A cone in three tiers over a short trunk.
            var z0 = h * 0.12;
            yield return Facing(at, Outline, new SilhouetteStroke(Mirrored([
                new(0, h), new(r * 0.5, h * 0.63), new(r * 0.3, h * 0.65), new(r * 0.75, h * 0.38), new(r * 0.45, h * 0.4), new(r, z0), new(t, h * 0.1), new(t, 0)])));
            yield return Facing(at, Abstract, new SilhouetteStroke([new(r, z0), new(0, h), new(-r, z0)]), new SilhouetteStroke([new(0, 0), new(0, z0)], false));
            yield break;
        }
        // A rounded crown on its trunk: a pine's high on a long one, a fruit tree's low, a broadleaf's from a third of its height.
        var c0 = h * (family == "pine" ? 0.55 : family == "fruit" ? 0.3 : 0.35);
        double zc = (c0 + h) / 2, ry = (h - c0) / 2, rx = r;
        var p0 = Math.Acos(Math.Min(0.9, t / rx));
        var crown = new List<Vec2> { new(t, 0) };
        const int steps = 28;
        for (var i = 0; i <= steps; i++)
        {
            var w = -p0 + (Math.PI + 2 * p0) * i / steps;
            // A broadleaf's crown softly scalloped (seven lobes round it), a pine's ragged.
            var k = family == "broadleaf" ? 0.95 + 0.05 * Math.Abs(Math.Sin(3.5 * (w + p0))) : family == "pine" ? (i % 2 == 0 ? 1 : 0.94) : 1;
            crown.Add(new Vec2(rx * k * Math.Cos(w), zc + ry * k * Math.Sin(w)));
        }
        crown.Add(new(-t, 0));
        yield return Facing(at, Outline, new SilhouetteStroke(crown));
        yield return Facing(at, Abstract,
            new SilhouetteStroke([.. Ring(rx, ry, 12).Select(p => p + new Vec2(0, zc))]),
            new SilhouetteStroke([new(0, 0), new(0, zc - ry)], false));
    }
}
