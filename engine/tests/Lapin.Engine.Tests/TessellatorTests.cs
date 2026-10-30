using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// The edges a solid draws in 3D (decision 175): where its surface folds by
/// more than <see cref="Tessellator.CreaseDeg"/>, or ends; not the facets of a
/// curved surface, nor the triangles of a body read from a file.
/// </summary>
public sealed class TessellatorTests
{
    static int Edges(Solid s) => Tessellator.Tessellate(s).Edges.Length / 6;

    [Fact]
    public void A_box_draws_its_twelve_edges()
    {
        Assert.Equal(12, Edges(Prism.Box(new Vec3(0, 0, 0), 2, 1, 3)!));
    }

    [Fact]
    public void A_cylinder_draws_its_rims_but_not_its_facets_unless_they_fold_enough()
    {
        // 24 facets fold 15° each: under the crease, so only the two rims (24 edges each) draw.
        Assert.Equal(48, Edges(Prism.Cylinder(new Vec2(0, 0), 0, 1, 2, 24)!));
        // 8 facets fold 45°: the rims (8 edges each) and the eight verticals.
        Assert.Equal(24, Edges(Prism.Cylinder(new Vec2(0, 0), 0, 1, 2, 8)!));
        // Asked for every edge, the 24 verticals come back.
        Assert.Equal(72, Tessellator.Tessellate(Prism.Cylinder(new Vec2(0, 0), 0, 1, 2, 24)!, 0).Edges.Length / 6);
    }

    [Fact]
    public void An_open_mesh_of_triangles_draws_its_boundary_and_its_folds_not_the_diagonals()
    {
        // A square of two coplanar triangles and a third triangle folded up square from one side: open, as a body read as it is.
        Vec3[] v = [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(1, 0, 1), new(0, 0, 1)];
        var up = Vec3.UnitZ;
        var side = new Vec3(0, -1, 0);
        var s = new Solid(v, [new Face([0, 1, 2], [], up), new Face([0, 2, 3], [], up), new Face([0, 5, 4], [], side), new Face([0, 4, 1], [], side)]);
        var edges = Tessellator.Tessellate(s).Edges;
        // The square's boundary (4) less its edge shared with the folded side counted once (a fold of 90°), the side's boundary (3 more): no diagonal 0–2 nor 0–4.
        var segs = new HashSet<(Vec3, Vec3)>();
        for (var i = 0; i + 5 < edges.Length; i += 6) segs.Add((new Vec3(edges[i], edges[i + 1], edges[i + 2]), new Vec3(edges[i + 3], edges[i + 4], edges[i + 5])));
        Assert.Equal(7, segs.Count);
        Assert.DoesNotContain((v[0], v[2]), segs);
        Assert.DoesNotContain((v[0], v[4]), segs);
        Assert.Contains((v[0], v[1]), segs);
    }
}
