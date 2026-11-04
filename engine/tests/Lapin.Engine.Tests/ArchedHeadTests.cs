using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Arched heads drawn true (a tester's report, #70: a semicircle seen as a few straight pieces, nearly pointed,
/// its reveal flat): the head follows the chord rule, and the screen's coarser copies of the wall say honestly how
/// far they stray, so a copy that cuts the arch is drawn only where that is under a pixel.
/// </summary>
public sealed class ArchedHeadTests
{
    [Theory]
    [InlineData("round", null, 1.0)]
    [InlineData("round", null, 4.0)]
    [InlineData("segmental", 0.3, 3.0)]
    [InlineData("pointed", null, 2.0)]
    [InlineData("radiused", 0.6, 2.0)]
    public void A_heads_curve_strays_from_its_arc_no_more_than_the_chord_rule_allows(string kind, double? rise, double width)
    {
        var spec = JoinerySpec.DefaultWindow() with { Width = width, Height = 3, Shape = new JoineryShape(kind, rise) };
        var head = JoineryShapes.Head(spec, 0);
        Assert.True(head.Count >= JoineryShapes.MinPieces / 2, $"{head.Count} points");
        // Every piece's middle lies within the tolerance of the arc it is a chord of.
        var (f, zs) = (JoineryShapes.Rise(spec), JoineryShapes.Spring(spec));
        for (var i = 0; i + 1 < head.Count; i++)
        {
            var m = (head[i] + head[i + 1]) * 0.5;
            var r = kind switch { "segmental" => (width * width / 4 + f * f) / (2 * f), "pointed" => (f * f + width * width / 4) / width, "radiused" => f, _ => width / 2 };
            Vec2 c = kind switch
            {
                "segmental" => new(0, zs + f - r),
                "pointed" => new(m.X >= 0 ? width / 2 - r : r - width / 2, zs),
                "radiused" => new(Math.Sign(m.X) * (width / 2 - f), zs),
                _ => new(0, zs),
            };
            if (kind == "radiused" && Math.Abs(m.X) <= width / 2 - f + 1e-9) { Assert.Equal(zs + f, m.Y, 9); continue; }
            var sag = r - (m - c).Length;
            Assert.True(sag >= -1e-9 && sag <= Chords.Tolerance + 1e-9, $"{kind} {width}: piece {i} sags {sag}");
        }
        // The ends on the jambs, the springing, a semicircle's top exactly at the middle.
        Assert.Equal(width / 2, head[0].X, 9);
        Assert.Equal(-width / 2, head[^1].X, 9);
        if (kind is "round" or "pointed") Assert.Contains(head, p => Math.Abs(p.X) < 1e-9 && Math.Abs(p.Y - 3) < 1e-9);
    }

    [Fact]
    public void A_walls_coarser_copies_with_arched_openings_stray_no_more_than_they_say()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 6, 0);
        f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 1.5, ["width"] = 1.0, ["height"] = 2.1, ["shape"] = "round" });
        f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 4, ["width"] = 1.2, ["height"] = 2.1, ["shape"] = "segmental", ["rise"] = 0.2 });
        var parts = f.Session.Snapshot().Render.Single(r => r.NodeId == wall).Parts!;
        var copied = parts.Where(p => p.Lods is { Count: > 0 }).ToList();
        Assert.NotEmpty(copied);
        foreach (var p in copied)
            foreach (var l in p.Lods!)
            {
                // Both ways: the part's corners to the copy, and the copy's corners, edges' middles and centres to the part.
                var away = Farthest(Corners(p.Positions, null), Triangles(l.Positions, null));
                var back = Farthest(Samples(l.Positions), Triangles(p.Positions, p.Indices));
                Assert.True(Math.Max(away, back) <= l.Error + 1e-4, $"{p.Name}: a copy said {l.Error} and strays {away} / {back}");
                // A copy that changes the part never says it is exact (it was then drawn at every distance).
                Assert.True(l.Error > 0, $"{p.Name}: a copy said it was exact");
            }
    }

    static List<Vec3> Corners(float[] pos, int[]? _) => [.. Enumerable.Range(0, pos.Length / 3).Select(i => new Vec3(pos[3 * i], pos[3 * i + 1], pos[3 * i + 2]))];

    static List<(Vec3 A, Vec3 B, Vec3 C)> Triangles(float[] pos, int[]? idx)
    {
        Vec3 P(int i) => new(pos[3 * i], pos[3 * i + 1], pos[3 * i + 2]);
        return idx is { Length: > 0 }
            ? [.. Enumerable.Range(0, idx.Length / 3).Select(t => (P(idx[3 * t]), P(idx[3 * t + 1]), P(idx[3 * t + 2])))]
            : [.. Enumerable.Range(0, pos.Length / 9).Select(t => (P(3 * t), P(3 * t + 1), P(3 * t + 2)))];
    }

    static List<Vec3> Samples(float[] pos)
    {
        var o = new List<Vec3>();
        foreach (var (a, b, c) in Triangles(pos, null)) o.AddRange([a, b, c, (a + b) * 0.5, (b + c) * 0.5, (c + a) * 0.5, (a + b + c) * (1.0 / 3)]);
        return o;
    }

    static double Farthest(List<Vec3> points, List<(Vec3 A, Vec3 B, Vec3 C)> tris) => points.Max(p => tris.Min(t => Distance(p, t.A, t.B, t.C)));

    /// <summary>A point's distance to a triangle (Ericson's closest point).</summary>
    static double Distance(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
    {
        Vec3 ab = b - a, ac = c - a, ap = p - a;
        double d1 = Vec3.Dot(ab, ap), d2 = Vec3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return (p - a).Length;
        var bp = p - b;
        double d3 = Vec3.Dot(ab, bp), d4 = Vec3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return (p - b).Length;
        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return (p - (a + ab * (d1 / (d1 - d3)))).Length;
        var cp = p - c;
        double d5 = Vec3.Dot(ab, cp), d6 = Vec3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return (p - c).Length;
        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return (p - (a + ac * (d2 / (d2 - d6)))).Length;
        var va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return (p - (b + (c - b) * ((d4 - d3) / (d4 - d3 + (d5 - d6))))).Length;
        var den = 1 / (va + vb + vc);
        return (p - (a + ab * (vb * den) + ac * (vc * den))).Length;
    }
}
