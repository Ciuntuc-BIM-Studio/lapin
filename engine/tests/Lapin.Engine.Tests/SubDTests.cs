using Lapin.Geometry;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// Catmull–Clark subdivision with creases (docs/plan-studio-freeform.md §8.2, decision 772): the limits worked out by hand
/// from the published masks, the sharp rules, borders, levels by the chord rule, thickness, symmetry and refused cages.
/// </summary>
public class SubDTests(ITestOutputHelper output)
{
    /// <summary>The unit cube's cage, its base centred on the origin (z from 0 to 1).</summary>
    static SubDCage Cube() => SubDCages.Box(1, 1, 1);

    static SubDCage WithCreases(SubDCage c, IEnumerable<(int A, int B)> edges, double s) =>
        SubDCages.SetCreases(c, edges.ToList(), s).Cage!;

    static bool Has(Solid s, Vec3 p, double tol = 1e-9) => s.Vertices.Any(v => (v - p).Length <= tol);

    static int Euler(Solid s) => s.Vertices.Length - Topology.Build(s).ByEdge.Count + s.Faces.Length;

    static List<BrepDiagnostic> Errors(Solid s) => Topology.Validate(s).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

    // ------------------------------------------------------------ one step

    [Fact]
    public void One_step_of_a_cube_gives_its_points_edges_and_faces_as_quads_and_keeps_the_euler_characteristic()
    {
        var once = SubD.Step(Cube());
        // 8 points + 12 edge points + 6 face points; each square into 4 quads.
        Assert.Equal(26, once.Points.Length);
        Assert.Equal(24, once.Faces.Length);
        Assert.All(once.Faces, f => Assert.Equal(4, f.Length));
        Assert.Equal(2, once.Points.Length - once.Edges().Count + once.Faces.Length);
        // A corner (valence 3): Q = (1/6, 1/6, 1/6) round the centre, R = (1/3, 1/3, 1/3): (Q + 2R) / 3 = 5/18 from the centre.
        Assert.Contains(once.Points, p => (p - new Vec3(5.0 / 18, 5.0 / 18, 0.5 + 5.0 / 18)).Length < 1e-12);
        // An edge point: the mean of its two ends and the two face centres beside it, 0.375 out on two axes.
        Assert.Contains(once.Points, p => (p - new Vec3(0.375, 0.375, 0.5)).Length < 1e-12);
    }

    // ------------------------------------------------------------ the cube's limit

    [Fact]
    public void A_cube_cage_converges_to_its_known_limit_symmetric_about_its_centre()
    {
        var r = SubD.Build(Cube());
        Assert.Null(r.Problem);
        var s = r.Solid!;
        output.WriteLine($"level {r.Level}, deviation {r.Deviation:0.000000} m, volume {Measure.Volume(s):0.000000} m³, faces {s.Faces.Length}");
        // A face's centre goes to its limit by the valence-4 mask on the first level: (16·½ + 4·4·⅜ + 4·5/18) / 36 = 34/81 from the
        // centre; a corner by the valence-3 mask: (9·5/18 + 4·¾ + ½) / 24 = ¼ on each axis. Both are points of every finer level.
        var (lo, hi) = Measure.Bounds(s)!.Value;
        Assert.Equal(34.0 / 81, hi.X, 9); Assert.Equal(-34.0 / 81, lo.X, 9);
        Assert.Equal(34.0 / 81, hi.Y, 9); Assert.Equal(-34.0 / 81, lo.Y, 9);
        Assert.Equal(0.5 + 34.0 / 81, hi.Z, 9); Assert.Equal(0.5 - 34.0 / 81, lo.Z, 9);
        Assert.True(Has(s, new Vec3(0.25, 0.25, 0.75)), "the corner's limit point");
        Assert.True(Has(s, new Vec3(-0.25, -0.25, 0.25)), "the opposite corner's");
        // Nearly a ball: every point lies between the faces' limit (34/81 from the centre) and the corners' (√3/4), so the volume
        // lies between those balls' (measured 0.32618 m³ at level 4).
        var centre = new Vec3(0, 0, 0.5);
        Assert.All(s.Vertices, p => Assert.InRange((p - centre).Length, 34.0 / 81 - 1e-9, Math.Sqrt(3) / 4 + 1e-9));
        var v = Measure.Volume(s);
        Assert.InRange(v, 4.0 / 3 * Math.PI * Math.Pow(34.0 / 81, 3), 4.0 / 3 * Math.PI * Math.Pow(Math.Sqrt(3) / 4, 3));
        Assert.Equal(0.32618, v, 4);
        // Finer levels close in from inside (their points on the limit, their facets within it): level 3 holds a little less,
        // within 1.5 % (measured 1.2 %, the gap falling about four times a level).
        var v3 = Measure.Volume(SubD.Build(Cube(), new SubDOptions(MaxLevel: 3)).Solid!);
        Assert.True(v3 < v && (v - v3) / v < 0.015, $"{v3} against {v}");
        // Symmetric: every point has its mirror images across the three planes through the centre.
        foreach (var p in s.Vertices.Where((_, i) => i % 7 == 0))
        {
            Assert.True(Has(s, new Vec3(-p.X, p.Y, p.Z), 1e-9));
            Assert.True(Has(s, new Vec3(p.X, -p.Y, p.Z), 1e-9));
            Assert.True(Has(s, new Vec3(p.X, p.Y, 1 - p.Z), 1e-9));
        }
    }

    [Fact]
    public void A_fully_creased_cube_stays_the_cube_exactly()
    {
        var cage = Cube();
        var r = SubD.Build(WithCreases(cage, cage.Edges(), SubD.Infinite));
        var s = r.Solid!;
        Assert.Equal(1, Measure.Volume(s), 9);
        var (lo, hi) = Measure.Bounds(s)!.Value;
        Assert.Equal(new Vec3(-0.5, -0.5, 0), lo);
        Assert.Equal(new Vec3(0.5, 0.5, 1), hi);
        // Nothing to gain from a finer level: the chord rule stops at the first.
        Assert.Equal(1, r.Level);
        Assert.Equal(0, r.Deviation, 12);
        Assert.Empty(Errors(s));
    }

    [Fact]
    public void Creased_edges_between_corners_stay_straight_and_their_face_flat()
    {
        // The bottom's four edges and the four uprights sharp: each bottom corner is on three creases (a corner, kept), each bottom
        // edge a crease between two corners, so it stays a straight line and the bottom face stays a flat 1 × 1 square.
        var cage = Cube();
        var bottom = cage.Edges().Where(e => cage.Points[e.A].Z == 0 && cage.Points[e.B].Z == 0).ToList();
        var upright = cage.Edges().Where(e => cage.Points[e.A].Z != cage.Points[e.B].Z).ToList();
        Assert.Equal(4, bottom.Count); Assert.Equal(4, upright.Count);
        var s = SubD.Build(WithCreases(cage, bottom.Concat(upright), SubD.Infinite)).Solid!;
        foreach (var (x, y) in new[] { (-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5) }) Assert.True(Has(s, new Vec3(x, y, 0)), $"corner {x}, {y}");
        var (lo, hi) = Measure.Bounds(s)!.Value;
        Assert.Equal(0, lo.Z, 12);
        Assert.Equal(-0.5, lo.X, 12); Assert.Equal(0.5, hi.X, 12);
        // Points on the front bottom edge: all on the line y = −0.5, z = 0, spread along it.
        var front = s.Vertices.Where(v => Math.Abs(v.Y + 0.5) < 1e-9 && Math.Abs(v.Z) < 1e-9).ToList();
        Assert.True(front.Count >= 5, $"{front.Count} points on the front edge");
        Assert.Equal(-0.5, front.Min(v => v.X), 12); Assert.Equal(0.5, front.Max(v => v.X), 12);
        // The bottom is one flat face at z = 0 (its quads kept whole), and the top rounds off below 1.
        Assert.All(s.Vertices.Where(v => v.Z < 1e-9), v => Assert.True(Math.Abs(v.X) <= 0.5 + 1e-12 && Math.Abs(v.Y) <= 0.5 + 1e-12));
        Assert.True(hi.Z < 1);
        Assert.Empty(Errors(s));
    }

    [Fact]
    public void Semi_sharp_creases_fall_between_smooth_and_sharp_in_order_of_their_sharpness()
    {
        var cage = Cube();
        double V(double sharp) => Measure.Volume(SubD.Build(sharp > 0 ? WithCreases(cage, cage.Edges(), sharp) : cage).Solid!);
        var vs = new[] { 0, 0.5, 1, 2, 3, SubD.Infinite }.Select(V).ToArray();
        output.WriteLine(string.Join(", ", vs.Select(v => v.ToString("0.000000"))));
        for (var i = 1; i < vs.Length; i++) Assert.True(vs[i] > vs[i - 1] + 1e-4, $"sharpness step {i}: {vs[i - 1]} then {vs[i]}");
        Assert.Equal(1, vs[^1], 9);
    }

    // ------------------------------------------------------------ open borders

    [Fact]
    public void An_open_sheet_keeps_its_border_a_cubic_b_spline_of_its_border_points_and_its_corners()
    {
        // 3 × 3 quads, the front border (y = 0) lifted 0, 1, 0, 1 at x = 0…3.
        var pts = new List<Vec3>();
        for (var j = 0; j <= 3; j++)
            for (var i = 0; i <= 3; i++)
                pts.Add(new Vec3(i, j, j == 0 && i % 2 == 1 ? 1 : 0));
        var faces = new List<int[]>();
        for (var j = 0; j < 3; j++)
            for (var i = 0; i < 3; i++)
                faces.Add([j * 4 + i, j * 4 + i + 1, (j + 1) * 4 + i + 1, (j + 1) * 4 + i]);
        var r = SubD.Build(new SubDCage(pts, faces));
        Assert.False(r.Closed);
        var s = r.Solid!;
        // An open shell: only open edges are wrong with it.
        Assert.All(Errors(s), d => Assert.Equal("not_closed", d.Code));
        // Corners stay; a border point goes to (A + 4S + B) / 6; the middle of the span between them is the uniform cubic
        // B-spline's (P0 + 23 P1 + 23 P2 + P3) / 48.
        Assert.True(Has(s, new Vec3(0, 0, 0)));
        Assert.True(Has(s, new Vec3(3, 0, 1)));
        Assert.True(Has(s, new Vec3(1, 0, 2.0 / 3)));
        Assert.True(Has(s, new Vec3(2, 0, 1.0 / 3)));
        Assert.True(Has(s, new Vec3(1.5, 0, 0.5)));
        Assert.True(Has(s, new Vec3(3, 3, 0)));
        // The border stays on the border planes of its points: every open edge runs along y = 0, y = 3, x = 0 or x = 3.
        var t = Topology.Build(s);
        foreach (var uses in t.ByEdge.Values.Where(u => u.Count == 1))
        {
            var he = t.HalfEdges[uses[0]];
            var (a, b) = (s.Vertices[he.From], s.Vertices[he.To]);
            Assert.True(new[] { a.Y + b.Y, a.Y + b.Y - 6, a.X + b.X, a.X + b.X - 6 }.Any(x => Math.Abs(x) < 1e-9) || Math.Abs(a.Y) < 1e-9 && Math.Abs(b.Y) < 1e-9,
                $"{a}–{b} is on the border");
        }
    }

    // ------------------------------------------------------------ topology

    /// <summary>A torus cage: 4 × 4 quads round a ring of radius 2, a tube of radius 0.5.</summary>
    static SubDCage Torus()
    {
        var pts = new List<Vec3>();
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
            {
                var (u, v) = (2 * Math.PI * i / 4, 2 * Math.PI * j / 4);
                var rr = 2 + 0.5 * Math.Cos(v);
                pts.Add(new Vec3(rr * Math.Cos(u), rr * Math.Sin(u), 0.5 * Math.Sin(v)));
            }
        int P(int i, int j) => (i % 4) * 4 + j % 4;
        var faces = new List<int[]>();
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
                faces.Add([P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1)]);
        return new SubDCage(pts, faces);
    }

    [Fact]
    public void Closed_cages_stay_closed_and_manifold_with_their_euler_characteristic()
    {
        var cube = SubD.Build(Cube()).Solid!;
        Assert.Empty(Errors(cube));
        Assert.Equal(2, Euler(cube));
        var torusCage = Torus();
        Assert.Null(SubD.Problem(torusCage));
        Assert.Equal(0, torusCage.Points.Length - torusCage.Edges().Count + torusCage.Faces.Length);
        var torus = SubD.Build(torusCage);
        Assert.True(torus.Closed);
        Assert.Empty(Errors(torus.Solid!));
        Assert.Equal(0, Euler(torus.Solid!));
        Assert.True(Measure.SignedVolume(torus.Solid!) > 0);
        // A cage given inside out comes out facing outward.
        var inward = new SubDCage(Cube().Points, Cube().Faces.Select(f => f.Reverse().ToArray()).ToList());
        Assert.True(Measure.SignedVolume(SubD.Build(inward).Solid!) > 0);
    }

    [Fact]
    public void Levels_follow_the_chord_rule_within_their_cap()
    {
        var cage = Cube();
        var r = SubD.Build(cage);
        var coarse = SubD.Build(cage, new SubDOptions(Tolerance: 0.01));
        var capped = SubD.Build(cage, new SubDOptions(MaxLevel: 2));
        output.WriteLine($"1 mm: level {r.Level} at {r.Deviation:0.000000}; 10 mm: level {coarse.Level} at {coarse.Deviation:0.000000}; capped: level {capped.Level} at {capped.Deviation:0.000000}");
        // The deviation falls each level, some 3.5 times while regular quads lead (a chord's sag goes as the square of its
        // length: 4), less once the corners' star points (valence 3, curvature converging more slowly) are the worst.
        var d = Enumerable.Range(1, 4).Select(k => SubD.Build(cage, new SubDOptions(MaxLevel: k, Tolerance: 1e-9)).Deviation).ToArray();
        output.WriteLine(string.Join(", ", d.Select(x => x.ToString("0.000000"))));
        for (var k = 1; k < d.Length; k++) Assert.InRange(d[k - 1] / d[k], 2, 4.5);
        // Each body is at the first level within its tolerance (or at the cap).
        Assert.True(coarse.Level < r.Level);
        Assert.True(coarse.Deviation <= 0.01);
        Assert.True(d[coarse.Level - 2] > 0.01, "the level before was not fine enough");
        Assert.True(r.Deviation <= Chords.Tolerance || r.Level == SubD.DefaultLevels);
        Assert.Equal(2, capped.Level);
        // A budget stops it sooner: 24, 96, 384 quads a level, 300 at most.
        var small = SubD.Build(cage, new SubDOptions(FaceBudget: 300));
        Assert.Equal(2, small.Level);
        Assert.True(small.Solid!.Faces.Length <= 2 * 96);
        // A semi-sharp crease of 3 needs three steps to show, whatever the tolerance.
        var semi = SubD.Build(WithCreases(cage, cage.Edges(), 3), new SubDOptions(Tolerance: 1));
        Assert.Equal(3, semi.Level);
    }

    // ------------------------------------------------------------ thickness

    static double Distance(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
    {
        // Closest point on a triangle (Ericson, Real-Time Collision Detection 5.1.5).
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
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return (p - (b + (c - b) * ((d4 - d3) / (d4 - d3 + d5 - d6)))).Length;
        var denom = 1 / (va + vb + vc);
        return (p - (a + ab * (vb * denom) + ac * (vc * denom))).Length;
    }

    /// <summary>A 2 × 2 m sheet of 4 × 4 quads with its nine inner points raised 0.4 m: a gentle dome.</summary>
    static SubDCage Dome()
    {
        var sheet = SubDCages.Plane(2, 2, 4, 4);
        var inner = Enumerable.Range(0, sheet.Points.Length).Where(i => Math.Abs(sheet.Points[i].X) < 0.9 && Math.Abs(sheet.Points[i].Y) < 0.9).ToList();
        Assert.Equal(9, inner.Count);
        return SubDCages.Move(sheet, inner, p => p + new Vec3(0, 0, 0.4));
    }

    [Theory]
    [InlineData(SubDSide.Out, 0.0, 0.2)]
    [InlineData(SubDSide.In, -0.2, 0.0)]
    [InlineData(SubDSide.Centre, -0.1, 0.1)]
    public void A_thickened_open_sheet_is_a_closed_solid_its_thickness_everywhere(SubDSide side, double below, double above)
    {
        var cage = Dome();
        var o = new SubDOptions(MaxLevel: 3, Thickness: 0.2, Side: side);
        var surface = SubD.Build(cage, o with { Thickness = 0 }).Solid!;
        var r = SubD.Build(cage, o);
        Assert.True(r.Closed);
        var s = r.Solid!;
        Assert.Empty(Errors(s));
        Assert.Equal(2, Euler(s));
        var tris = Measure.Triangles(surface).Select(t => (surface.Vertices[t.A], surface.Vertices[t.B], surface.Vertices[t.C])).ToList();
        // Every point of the solid lies off the surface by one of its sides' offsets, to the chord rule.
        var tol = Math.Max(r.Deviation, 1e-6) * 2;
        var worst = 0.0;
        foreach (var p in s.Vertices)
        {
            var d = tris.Min(t => Distance(p, t.Item1, t.Item2, t.Item3));
            var off = Math.Min(Math.Abs(d - Math.Abs(above)), Math.Abs(d - Math.Abs(below)));
            worst = Math.Max(worst, off);
        }
        output.WriteLine($"{side}: level {r.Level}, chord {r.Deviation:0.000000}, worst {worst:0.000000}");
        Assert.True(worst <= tol + 1e-4, $"{worst} off the thickness");
        var area = Measure.SurfaceArea(surface);
        var ratio = Measure.Volume(s) / (area * 0.2);
        output.WriteLine($"volume / (area × thickness) = {ratio:0.0000}");
        // Steiner: a shell of thickness t over a surface of mean curvature H holds A·t·(1 + H·(a + b)) for offsets a above and
        // b below; centred, the t² term cancels. The dome (radius of curvature about 2 m) gives some 9 % either way.
        if (side == SubDSide.Centre) Assert.InRange(ratio, 0.995, 1.005);
        else if (side == SubDSide.Out) Assert.InRange(ratio, 1.05, 1.15);
        else Assert.InRange(ratio, 0.85, 0.95);
    }

    // ------------------------------------------------------------ refused cages

    [Fact]
    public void Cages_that_are_not_manifold_or_not_wound_one_way_are_refused_with_the_reason()
    {
        var p = new List<Vec3> { new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(2, 2, 0), new(1, 2, 0), new(2, 1, 0) };
        // Three faces on the edge 0–1.
        Assert.Contains("three faces", SubD.Problem(new SubDCage(p, [[0, 1, 2], [1, 0, 4], [0, 1, 5]]))!);
        // Two faces running the same way along their edge.
        Assert.Contains("same way", SubD.Problem(new SubDCage(p, [[0, 1, 2, 3], [0, 1, 4]]))!);
        // Two fans meeting at point 2 only.
        Assert.Contains("corners only", SubD.Problem(new SubDCage(p, [[0, 1, 2, 3], [2, 8, 6, 7]]))!);
        Assert.Contains("does not have", SubD.Problem(new SubDCage(p, [[0, 1, 42]]))!);
        Assert.Contains("twice", SubD.Problem(new SubDCage(p, [[0, 1, 2, 1]]))!);
        Assert.Contains("fewer than 3", SubD.Problem(new SubDCage(p, [[0, 1]]))!);
        Assert.Contains("crease", SubD.Problem(new SubDCage(p, [[0, 1, 2, 3]], [new SubDCrease(0, 2, 1)]))!);
        Assert.Contains("no faces", SubD.Problem(new SubDCage(p, []))!);
        Assert.Null(SubD.Problem(new SubDCage(p, [[0, 1, 2, 3], [0, 4, 1]])));
        Assert.NotNull(SubD.Build(new SubDCage(p, [[0, 1, 2], [1, 0, 4], [0, 1, 5]])).Problem);
    }

    // ------------------------------------------------------------ symmetry

    [Fact]
    public void A_half_cage_mirrored_is_welded_on_the_plane_and_gives_a_mirrored_body()
    {
        // A 1 × 1 × 1 box cut in two along x, its right half kept (x ≥ 0), one top corner pulled up 0.3 and a point on the plane
        // a hair off it.
        var box = SubDCages.Box(1, 1, 1, nx: 2);
        Assert.Equal(12, box.Points.Length); Assert.Equal(10, box.Faces.Length);
        var corner = Array.FindIndex(box.Points, q => q == new Vec3(0.5, 0.5, 1));
        var onPlane = Array.FindIndex(box.Points, q => q == new Vec3(0, -0.5, 0));
        var half = SubDCages.Move(box, [corner], q => q + new Vec3(0, 0, 0.3));
        half = SubDCages.Move(half, [onPlane], q => q + new Vec3(1e-7, 0, 0));
        var (whole, problem) = SubD.Mirror(half, Vec3.Zero, new Vec3(1, 0, 0));
        Assert.Null(problem);
        Assert.Equal(12, whole!.Points.Length);
        Assert.Equal(10, whole.Faces.Length);
        Assert.True(whole.IsClosed);
        Assert.Contains(whole.Points, q => q == new Vec3(-0.5, 0.5, 1.3));
        Assert.Contains(whole.Points, q => q == new Vec3(0, -0.5, 0));
        var s = SubD.Build(whole).Solid!;
        Assert.Empty(Errors(s));
        foreach (var v in s.Vertices) Assert.True(Has(s, new Vec3(-v.X, v.Y, v.Z), 1e-9), $"{v} has its mirror image");
        // Mirroring the whole again changes nothing; the partners pair each point with its image.
        var again = SubD.Mirror(whole, Vec3.Zero, new Vec3(1, 0, 0)).Cage!;
        Assert.Equal(Measure.Volume(s), Measure.Volume(SubD.Build(again).Solid!), 9);
        var partners = SubD.Partners(whole, Vec3.Zero, new Vec3(1, 0, 0));
        Assert.All(Enumerable.Range(0, whole.Points.Length), i => Assert.True(partners[i] >= 0 && (whole.Points[partners[i]] - new Vec3(-whole.Points[i].X, whole.Points[i].Y, whole.Points[i].Z)).Length < 1e-9));
        // A face across the plane is refused: there is no loop on the plane to weld along.
        Assert.Contains("crosses", SubD.Mirror(Cube(), Vec3.Zero, new Vec3(1, 0, 0)).Problem!);
    }

    // ------------------------------------------------------------ shapes and edits

    [Fact]
    public void Shapes_are_valid_cages_and_a_creased_cylinder_keeps_flat_ends()
    {
        Assert.Null(SubD.Problem(SubDCages.Box(2, 1, 3, 2, 3, 4)));
        Assert.True(SubDCages.Box(2, 1, 3, 2, 3, 4).IsClosed);
        var sheet = SubDCages.Plane(4, 2, 4, 2);
        Assert.Null(SubD.Problem(sheet));
        Assert.False(sheet.IsClosed);
        var cyl = SubDCages.Cylinder(0.5, 2, 8);
        Assert.Null(SubD.Problem(cyl));
        var s = SubD.Build(cyl).Solid!;
        Assert.Empty(Errors(s));
        var (lo, hi) = Measure.Bounds(s)!.Value;
        Assert.Equal(0, lo.Z, 9); Assert.Equal(2, hi.Z, 9);
        // The rim, the cubic B-spline of a regular octagon of radius ½: its points at (½ + 4·½·cos 45° … ) — between the octagon's
        // inscribed circle and its corners.
        var rim = s.Vertices.Where(v => Math.Abs(v.Z) < 1e-9).Select(v => v.Xy.Length).ToList();
        Assert.InRange(rim.Max(), 0.5 * Math.Cos(Math.PI / 8) * 0.95, 0.5);
        var sm = SubD.Build(SubDCages.Cylinder(0.5, 2, 8, creasedCaps: false)).Solid!;
        Assert.True(Measure.Bounds(sm)!.Value.Min.Z > 0, "without creases the ends round off");
    }

    [Fact]
    public void Extrude_loop_delete_and_crease_reshape_a_cage()
    {
        var box = Cube();
        var top = Array.FindIndex(box.Faces, f => f.All(k => box.Points[k].Z == 1));
        var (up, p1) = SubDCages.Extrude(box, [top], 0.5);
        Assert.Null(p1);
        Assert.Equal(10, up!.Faces.Length); Assert.Equal(12, up.Points.Length);
        Assert.True(up.IsClosed);
        Assert.All(up.Faces[top], k => Assert.Equal(1.5, up.Points[k].Z, 12));
        var sharpUp = SubD.Build(WithCreases(up, up.Edges(), SubD.Infinite)).Solid!;
        Assert.Equal(1.5, Measure.Volume(sharpUp), 9);

        // A loop round the four sides, a quarter of the way up from the bottom.
        var upright = box.Edges().First(e => box.Points[e.A].Z != box.Points[e.B].Z);
        var low = box.Points[upright.A].Z == 0 ? upright.A : upright.B;
        var (looped, p2) = SubDCages.InsertLoop(box, low, low == upright.A ? upright.B : upright.A, 0.25);
        Assert.Null(p2);
        Assert.Equal(10, looped!.Faces.Length);
        Assert.Equal(12, looped.Points.Length);
        Assert.Equal(4, looped.Points.Count(q => Math.Abs(q.Z - 0.25) < 1e-12));
        Assert.True(looped.IsClosed);
        // The extra loop pulls the smooth body towards the cage near the bottom: more volume.
        Assert.True(Measure.Volume(SubD.Build(looped).Solid!) > Measure.Volume(SubD.Build(box).Solid!));

        var (open, p3) = SubDCages.DeleteFaces(box, [top]);
        Assert.Null(p3);
        Assert.False(open!.IsClosed);
        Assert.False(SubD.Build(open).Closed);
        Assert.True(SubD.Build(open, new SubDOptions(Thickness: 0.05)).Closed);
        Assert.NotNull(SubDCages.DeleteFaces(box, Enumerable.Range(0, 6).ToList()).Problem);

        var (creased, p4) = SubDCages.SetCreases(box, [box.Edges()[0]], 2);
        Assert.Null(p4);
        Assert.Equal(2, creased!.SharpnessOf(box.Edges()[0].A, box.Edges()[0].B));
        Assert.Empty(SubDCages.SetCreases(creased, [box.Edges()[0]], 0).Cage!.Creases);
        var f0 = box.Faces[0];
        Assert.NotNull(SubDCages.SetCreases(box, [(f0[0], f0[2])], 1).Problem);
    }
}
