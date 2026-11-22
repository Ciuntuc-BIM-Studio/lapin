using Lapin.Geometry;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// Exact bodies beside the tessellation (docs/plan-studio-freeform.md §9, F2b): built from what made a body, turned and checked against
/// that body, refused when they are not it; SubD's regular faces as bicubic patches, the rest as the body's facets.
/// </summary>
public class ExactBodyTests(ITestOutputHelper output)
{
    static readonly Vec3 X = new(1, 0, 0), Y = new(0, 1, 0);

    static Solid Cylinder(double r, double h) => Prism.Extrude([.. Chords.Ring(new Vec2(0, 0), r).Select(p => new Vec3(p.X, p.Y, 0))], null, new Vec3(0, 0, h), "t")!;

    [Fact]
    public void A_box_is_six_planes_and_twelve_lines_in_a_closed_shell()
    {
        var box = Prism.Extrude([new(0, 0, 0), new(2, 0, 0), new(2, 1, 0), new(0, 1, 0)], null, new Vec3(0, 0, 3), "t")!;
        var (body, why) = ExactCheck.Fit(ExactBuild.Planar(box), box, 1e-6);
        Assert.Null(why);
        Assert.Equal(6, body!.Faces.Count);
        Assert.Equal(12, body.Edges.Count);
        Assert.Equal(8, body.Vertices.Count);
        Assert.All(body.Edges, e => Assert.IsType<LineCurve>(e.Curve));
    }

    [Fact]
    public void An_extruded_circle_is_two_planes_and_two_half_cylinders_facing_out()
    {
        var (body, why) = ExactCheck.Fit(ExactBuild.Extrusion([[new ArcSeg(Vec3.Zero, X, Y, 0.5, 0, 2 * Math.PI)]], new Vec3(0, 0, 2))!, Cylinder(0.5, 2), 0.003);
        Assert.Null(why);
        Assert.Equal(new Dictionary<string, int> { ["cylinder"] = 2, ["plane"] = 2 }, body!.SurfaceKinds());
        // The cylinders' own normals point out: they agree with the body.
        Assert.All(body.Faces.Where(f => f.Surface is CylinderSurface), f => Assert.True(f.SameSense));
        // The caps: the top's plane normal and the bottom's, each the way out.
        foreach (var f in body.Faces.Where(f => f.Surface is PlaneSurface))
        {
            var n = ((PlaneSurface)f.Surface).Frame.Z * (f.SameSense ? 1 : -1);
            Assert.Equal(f.Inside.Z > 1 ? 1 : -1, Math.Sign(n.Z));
        }
        // Edges: two half circles below, two above, two straight seams up the side.
        Assert.Equal(4, body.Edges.Count(e => e.Curve is CircleCurve));
        Assert.Equal(2, body.Edges.Count(e => e.Curve is LineCurve));
    }

    [Fact]
    public void An_exact_body_that_is_not_the_body_is_refused()
    {
        // A cylinder's exact faces against a box: they stray from it.
        var box = Prism.Extrude([new(-0.5, -0.5, 0), new(0.5, -0.5, 0), new(0.5, 0.5, 0), new(-0.5, 0.5, 0)], null, new Vec3(0, 0, 2), "t")!;
        var exact = ExactBuild.Extrusion([[new ArcSeg(Vec3.Zero, X, Y, 0.5, 0, 2 * Math.PI)]], new Vec3(0, 0, 2))!;
        Assert.NotNull(ExactCheck.Fit(exact, box, 0.003).Problem);
        // The cylinder with a notch cut out of it: its facets on no exact surface.
        var cut = Prism.Extrude([.. Chords.Ring(new Vec2(0, 0), 0.5).Select(p => new Vec3(p.X, p.Y, 0)).Where(p => p.X < 0.3 || Math.Abs(p.Y) > 0.2), new Vec3(0.3, 0.2, 0), new Vec3(0.3, -0.2, 0)], null, new Vec3(0, 0, 2), "t");
        Assert.NotNull(cut);
        Assert.NotNull(ExactCheck.Fit(exact, cut!, 0.003).Problem);
    }

    [Fact]
    public void A_profile_of_lines_and_an_arc_extruded_has_planes_and_a_cylinder_on_the_arc()
    {
        // A D shape: a straight side and a half circle of radius 1 about the origin, extruded 0.5.
        var segs = new List<ExactSeg> { new LineSeg(new Vec3(0, -1, 0), new Vec3(0, 1, 0)), new ArcSeg(Vec3.Zero, X, Y, 1, Math.PI / 2, Math.PI) };
        // Its tessellation as the model makes it.
        var ring = new List<Vec3> { new(0, -1, 0) };
        ring.AddRange(Chords.Arc(new Vec2(0, 0), 1, Math.PI / 2, Math.PI).Select(p => new Vec3(p.X, p.Y, 0)).SkipLast(1));
        ring.Reverse();
        var solid = Prism.Extrude(ring, null, new Vec3(0, 0, 0.5), "t")!;
        var (body, why) = ExactCheck.Fit(ExactBuild.Extrusion([segs], new Vec3(0, 0, 0.5))!, solid, 0.003);
        Assert.Null(why);
        Assert.Equal(new Dictionary<string, int> { ["cylinder"] = 1, ["plane"] = 3 }, body!.SurfaceKinds());
    }

    // ------------------------------------------------------------ SubD

    /// <summary>A torus-shaped cage of n × m quads: every point of valence 4, so every face is regular.</summary>
    static SubDCage TorusCage(int n, int m, double big = 2, double small = 0.6)
    {
        var pts = new List<Vec3>();
        for (var i = 0; i < n; i++)
            for (var j = 0; j < m; j++)
            {
                double u = 2 * Math.PI * i / n, v = 2 * Math.PI * j / m;
                pts.Add(new Vec3((big + small * Math.Cos(v)) * Math.Cos(u), (big + small * Math.Cos(v)) * Math.Sin(u), small * Math.Sin(v)));
            }
        int P(int i, int j) => (i % n) * m + j % m;
        var faces = new List<int[]>();
        for (var i = 0; i < n; i++) for (var j = 0; j < m; j++) faces.Add([P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1)]);
        return new SubDCage(pts, faces);
    }

    [Fact]
    public void A_regular_cage_is_one_bicubic_patch_a_face_and_nothing_else()
    {
        var cage = TorusCage(8, 6);
        Assert.Null(SubD.Problem(cage));
        Assert.All(SubDExact.Regular(cage), Assert.True);
        var built = SubD.Build(cage);
        var r = SubDExact.Of(cage, built.Level)!;
        Assert.Equal(48, r.Patches);
        Assert.Equal(0, r.Facets);
        Assert.Equal(0, r.PatchLevel);
        Assert.All(r.Body.Faces, f => Assert.IsType<SplineSurface>(f.Surface));
        var (body, why) = ExactCheck.Fit(r.Body, built.Solid!, Math.Max(0.003, 2 * built.Deviation + 0.001));
        Assert.Null(why);
        // The patch is the limit surface: the body's limit points lie on it, to the last digits.
        var limit = SubD.Limit(SubD.Step(cage)).Points;
        foreach (var p in limit.Take(20))
            Assert.True(body!.Faces.Min(f => f.Surface.Distance(p)) < 1e-9);
    }

    [Fact]
    public void A_box_cage_is_patches_away_from_its_corners_and_the_bodys_facets_round_them()
    {
        var cage = SubDCages.Box(1, 1, 1);
        var built = SubD.Build(cage);
        var r = SubDExact.Of(cage, built.Level)!;
        output.WriteLine($"level {built.Level}, deviation {built.Deviation * 1000:0.##} mm, {r.Patches} patches, {r.Facets} facets at patch level {r.PatchLevel}");
        // Two steps: 96 faces, the 24 round the eight corners irregular.
        Assert.Equal(2, r.PatchLevel);
        Assert.Equal(72, r.Patches);
        Assert.True(r.Facets > 0);
        var (body, why) = ExactCheck.Fit(r.Body, built.Solid!, Math.Max(0.003, 2 * built.Deviation + 0.001));
        Assert.Null(why);
        Assert.Equal(72, body!.Faces.Count(f => f.Surface is SplineSurface));
    }

    [Fact]
    public void An_open_or_creased_cage_keeps_what_it_can()
    {
        Assert.Null(SubDExact.Of(SubDCages.Plane(2, 2, 3, 3), 3));
        // A fully creased cube: no regular face at any level, nothing exact.
        var cube = SubDCages.Box(1, 1, 1);
        var sharp = SubDCages.SetCreases(cube, cube.Edges(), SubD.Infinite).Cage!;
        Assert.Null(SubDExact.Of(sharp, SubD.Build(sharp).Level));
    }
}
