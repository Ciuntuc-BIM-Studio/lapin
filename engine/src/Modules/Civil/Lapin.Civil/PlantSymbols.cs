using Lapin.Geometry;

namespace Lapin.Civil;

/// <summary>One stroke of a plant's plan symbol: a polyline (closed or open), or an area filled solid; <see cref="Detail"/> marks the inner lines a small-scale plan leaves out.</summary>
public sealed record SymbolStroke(IReadOnlyList<Vec2> Points, bool Closed, bool Fill = false, bool Detail = false);

/// <summary>
/// A plant seen from above, as a landscape plan draws it (decision 234): a
/// symbol by the species' family, in the plant's own coordinates (its centre
/// at the origin), as wide as its crown, made by Lapin from a seed so the same
/// plant always draws the same way and its neighbours differ a little (turned,
/// lobes and branches varied). Nothing is taken from a block library.
/// <list type="bullet">
/// <item><c>fruit</c>: a lobed crown, branches from the centre forking once;</item>
/// <item><c>broadleaf</c>: a cloud-like scalloped crown round a dark filled core and its branches;</item>
/// <item><c>fir</c>: a star of needle sprays, each a spine with barbs, the tips a spiky outline;</item>
/// <item><c>pine</c>: a ragged round crown of needle tufts, a dot at the centre;</item>
/// <item><c>shrub</c>: a cloud outline with a few curls inside;</item>
/// <item><c>hedge</c>: a small scalloped circle (a clipped bush); <c>ground</c>: nothing.</item>
/// </list>
/// </summary>
public static class PlantSymbols
{
    public static readonly IReadOnlyList<string> Families = ["fruit", "broadleaf", "fir", "pine", "shrub", "hedge", "ground"];

    /// <summary>The symbol of a plant of <paramref name="family"/> with a crown of <paramref name="radius"/>, from <paramref name="seed"/>.</summary>
    public static List<SymbolStroke> Symbol(string family, double radius, int seed)
    {
        var rng = new SiteGeometry.Random(Mix(seed, family));
        var turn = rng.Next() * 2 * Math.PI;
        var o = family switch
        {
            "fruit" => Fruit(radius, rng),
            "broadleaf" => Broadleaf(radius, rng),
            "fir" => Fir(radius, rng),
            "pine" => Pine(radius, rng),
            "shrub" => Shrub(radius, rng),
            "hedge" => Hedge(radius, rng),
            _ => [],
        };
        var (c, s) = (Math.Cos(turn), Math.Sin(turn));
        return [.. o.Select(st => st with { Points = [.. st.Points.Select(p => new Vec2(p.X * c - p.Y * s, p.X * s + p.Y * c))] })];
    }

    /// <summary>The family a species draws by when it names none: trees broadleaf, bushes shrubs, grass the ground.</summary>
    public static string DefaultFamily(string type) => type switch { "bush" => "shrub", "grass" => "ground", _ => "broadleaf" };

    /// <summary>A seed for a family: the same plant drawn as another family is not its mirror.</summary>
    static int Mix(int seed, string family)
    {
        unchecked
        {
            var h = (uint)seed * 2654435761u;
            foreach (var ch in family) h = (h ^ ch) * 16777619u;
            return (int)(h == 0 ? 1 : h);
        }
    }

    static Vec2 Polar(double r, double a) => new(r * Math.Cos(a), r * Math.Sin(a));

    /// <summary>A soft lobed outline: <paramref name="lobes"/> bulges between shallow notches, each its own size.</summary>
    static List<Vec2> Lobed(double r, int lobes, double notch, int per, SiteGeometry.Random rng)
    {
        var sizes = Enumerable.Range(0, lobes).Select(_ => 0.93 + rng.Next() * 0.07).ToArray();
        var widths = Enumerable.Range(0, lobes).Select(_ => 0.75 + rng.Next() * 0.5).ToArray();
        var total = widths.Sum();
        var o = new List<Vec2>();
        var a = 0.0;
        for (var k = 0; k < lobes; k++)
        {
            var span = 2 * Math.PI * widths[k] / total;
            for (var j = 0; j < per; j++)
            {
                var t = (double)j / per;
                // A bulge: the lobe's top in its middle, a notch where it meets the next.
                var bulge = Math.Pow(Math.Sin(Math.PI * t), 0.6);
                o.Add(Polar(r * sizes[k] * (1 - notch + notch * bulge), a + span * t));
            }
            a += span;
        }
        return o;
    }

    static List<SymbolStroke> Fruit(double r, SiteGeometry.Random rng)
    {
        var o = new List<SymbolStroke> { new(Lobed(r, 8 + (int)(rng.Next() * 4), 0.12, 7, rng), true) };
        var n = 5 + (int)(rng.Next() * 3);
        for (var k = 0; k < n; k++)
        {
            var a = 2 * Math.PI * (k + 0.3 * (rng.Next() - 0.5)) / n;
            var fork = Polar(r * (0.38 + 0.12 * rng.Next()), a);
            o.Add(new([Polar(r * 0.06, a), fork], false, Detail: true));
            var spread = 0.2 + 0.12 * rng.Next();
            o.Add(new([fork, Polar(r * (0.68 + 0.12 * rng.Next()), a - spread)], false, Detail: true));
            o.Add(new([fork, Polar(r * (0.68 + 0.12 * rng.Next()), a + spread)], false, Detail: true));
        }
        return o;
    }

    static List<SymbolStroke> Broadleaf(double r, SiteGeometry.Random rng)
    {
        // The crown: many small round scallops, a cloud seen from above.
        var o = new List<SymbolStroke> { new(Lobed(r, 14 + (int)(rng.Next() * 4), 0.1, 5, rng), true) };
        // The dark core: a star of limbs filled solid, each going on as a branch that forks, then forks again.
        var arms = 5 + (int)(rng.Next() * 2);
        var star = new List<Vec2>();
        var tips = new List<double>();
        for (var k = 0; k < arms; k++)
        {
            var a = 2 * Math.PI * (k + 0.25 * (rng.Next() - 0.5)) / arms;
            tips.Add(a);
            star.Add(Polar(r * (0.22 + 0.06 * rng.Next()), a));
            star.Add(Polar(r * 0.05, a + Math.PI / arms));
        }
        o.Add(new(star, true, Fill: true, Detail: true));
        foreach (var a in tips)
        {
            var from = Polar(r * 0.24, a);
            var fork = Polar(r * 0.48, a + 0.12 * (rng.Next() - 0.5));
            o.Add(new([from, fork], false, Detail: true));
            foreach (var side in new[] { -1, 1 })
            {
                // Each its own length and turn, so neighbouring limbs never close into a pattern.
                var b = a + side * (0.12 + 0.12 * rng.Next());
                var twig = Polar(r * (0.6 + 0.12 * rng.Next()), b);
                o.Add(new([fork, twig], false, Detail: true));
                o.Add(new([twig, Polar(r * (0.76 + 0.1 * rng.Next()), b + side * (0.04 + 0.1 * rng.Next()))], false, Detail: true));
            }
        }
        return o;
    }

    static List<SymbolStroke> Fir(double r, SiteGeometry.Random rng)
    {
        var n = 12 + (int)(rng.Next() * 3);
        var o = new List<SymbolStroke>();
        var outline = new List<Vec2>();
        for (var k = 0; k < n; k++)
        {
            var a = 2 * Math.PI * (k + 0.15 * (rng.Next() - 0.5)) / n;
            var len = r * (0.9 + 0.1 * rng.Next());
            var dir = Polar(1, a);
            var side = dir.Perp;
            // The spine, and three pairs of barbs swept back towards the centre.
            o.Add(new([Polar(r * 0.04, a), dir * len], false, Detail: true));
            for (var j = 1; j <= 3; j++)
            {
                var t = 0.3 + 0.2 * j;
                var at = dir * (len * t);
                var barb = 0.2 * r * (1.15 - t);
                o.Add(new([at - dir * (barb * 0.75) + side * (barb * 0.65), at, at - dir * (barb * 0.75) - side * (barb * 0.65)], false, Detail: true));
            }
            // The spiky outline: out to the tip, back in between two sprays.
            outline.Add(dir * len);
            outline.Add(Polar(r * (0.62 + 0.08 * rng.Next()), a + Math.PI / n));
        }
        o.Insert(0, new(outline, true));
        return o;
    }

    static List<SymbolStroke> Pine(double r, SiteGeometry.Random rng)
    {
        var o = new List<SymbolStroke>();
        // A ragged round crown: small irregular teeth.
        var ring = new List<Vec2>();
        var m = 28;
        for (var k = 0; k < m; k++) ring.Add(Polar(r * (k % 2 == 0 ? 0.97 + 0.03 * rng.Next() : 0.86 + 0.05 * rng.Next()), 2 * Math.PI * k / m));
        o.Add(new(ring, true));
        // Needle tufts: a fan of four needles from a twig's end, on two rings.
        var tufts = 20 + (int)(rng.Next() * 5);
        for (var k = 0; k < tufts; k++)
        {
            var inner = k % 3 == 0;
            var a = 2 * Math.PI * (k + 0.4 * (rng.Next() - 0.5)) / tufts;
            var at = Polar(r * (inner ? 0.3 : 0.56) + r * 0.05 * rng.Next(), a);
            var len = r * (inner ? 0.18 : 0.26);
            foreach (var d in new[] { -0.5, -0.17, 0.17, 0.5 }) o.Add(new([at, at + Polar(len * (1 - 0.3 * Math.Abs(d)), a + d)], false, Detail: true));
        }
        // The trunk seen from above: a dot.
        o.Add(new([.. Enumerable.Range(0, 8).Select(k => Polar(r * 0.06, 2 * Math.PI * k / 8))], true, Fill: true));
        return o;
    }

    static List<SymbolStroke> Shrub(double r, SiteGeometry.Random rng)
    {
        var o = new List<SymbolStroke> { new(Lobed(r, 8 + (int)(rng.Next() * 3), 0.2, 6, rng), true) };
        // A few curls inside: short arcs turned each their own way.
        var curls = 3 + (int)(rng.Next() * 2);
        for (var k = 0; k < curls; k++)
        {
            var c = Polar(r * (0.25 + 0.2 * rng.Next()), 2 * Math.PI * (k + rng.Next() * 0.5) / curls);
            var start = rng.Next() * 2 * Math.PI;
            var cr = r * 0.16;
            o.Add(new([.. Enumerable.Range(0, 6).Select(j => c + Polar(cr, start + j * 0.6))], false, Detail: true));
        }
        return o;
    }

    static List<SymbolStroke> Hedge(double r, SiteGeometry.Random rng) =>
        [new(Lobed(r, 12, 0.12, 3, rng), true), new([.. Enumerable.Range(0, 6).Select(k => Polar(r * 0.05, 2 * Math.PI * k / 6))], true, Fill: true, Detail: true)];
}
