using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The shell mesher (docs/plan-fem.md §10.3): a rectangle into quadrilaterals only; pieces meeting along an edge (in a T,
/// with a column's node on it) sharing its nodes; any convex piece into mostly quadrilaterals, none folded, the area kept; a
/// wall's faces turned to the normal asked; the nodes along a bar; and a plate meshed this way, triangles and all, solved
/// against plate theory.
/// </summary>
public class ShellMesherTests(ITestOutputHelper output)
{
    static double FaceArea(ShellMesh m, MeshFace f, Vec3 normal) => Vec3.Dot(VecMath.NewellNormal(f.Nodes.Select(i => m.Nodes[i]).ToList()), normal);

    /// <summary>Each face's corners turn about the normal (positive area), no face is folded, and the faces cover the area.</summary>
    static void Sound(ShellMesh m, Vec3 normal, double area)
    {
        var sum = 0.0;
        foreach (var f in m.Faces)
        {
            var a = FaceArea(m, f, normal);
            Assert.True(a > 0, $"a face turned over ({a})");
            // Convex: every corner turns the same way.
            var p = f.Nodes.Select(i => m.Nodes[i]).ToArray();
            for (var i = 0; i < p.Length; i++)
                Assert.True(Vec3.Dot(Vec3.Cross(p[(i + 1) % p.Length] - p[i], p[(i + 2) % p.Length] - p[(i + 1) % p.Length]), normal) > 0, "a face not convex");
            sum += a;
        }
        Assert.Equal(area, sum, 9);
    }

    /// <summary>Each side of a face is either another face's too (once, the other way round) or on the outline; no node hangs on a side.</summary>
    static void Conforming(ShellMesh m)
    {
        var sides = new Dictionary<(int, int), int>();
        foreach (var f in m.Faces)
            for (var i = 0; i < f.Nodes.Length; i++)
            {
                var (a, b) = (f.Nodes[i], f.Nodes[(i + 1) % f.Nodes.Length]);
                sides[(a, b)] = sides.GetValueOrDefault((a, b)) + 1;
            }
        Assert.All(sides.Values, c => Assert.Equal(1, c));
        var used = m.Faces.SelectMany(f => f.Nodes).ToHashSet();
        foreach (var ((a, b), _) in sides)
        {
            if (sides.ContainsKey((b, a))) continue;
            // An outline side: no other mesh node lies inside it.
            var (pa, pb) = (m.Nodes[a], m.Nodes[b]);
            foreach (var n in used)
            {
                if (n == a || n == b) continue;
                var d = pb - pa;
                var t = Vec3.Dot(m.Nodes[n] - pa, d) / Vec3.Dot(d, d);
                if (t <= 1e-9 || t >= 1 - 1e-9) continue;
                Assert.False((pa + d * t - m.Nodes[n]).Length < 1e-7, $"node {n} {m.Nodes[n]} hangs on side {a} {m.Nodes[a]} – {b} {m.Nodes[b]}");
            }
        }
    }

    [Fact]
    public void A_rectangle_is_meshed_in_quadrilaterals_only()
    {
        List<Vec3> nodes = [new(0, 0, 3), new(6, 0, 3), new(6, 4, 3), new(0, 4, 3)];
        var m = ShellMesher.Mesh(nodes, [new MeshPiece([0, 1, 2, 3], "slab")], [], 0.5);
        Assert.Equal(12 * 8, m.Quads);
        Assert.Equal(0, m.Triangles);
        Sound(m, Vec3.UnitZ, 24);
        Conforming(m);
        Assert.All(m.Nodes, p => Assert.Equal(3, p.Z, 12));
    }

    [Fact]
    public void Pieces_meeting_in_a_T_with_a_column_on_the_edge_share_their_nodes()
    {
        // A slab cut in three: A (0..6 × 0..3) below, B (0..2.5 × 3..5) and C (2.5..6 × 3..5) above, so A's top edge meets two
        // pieces; a column's node at (4.2, 3) sits on the edge they share, and a beam runs along it.
        List<Vec3> nodes = [new(0, 0, 0), new(6, 0, 0), new(6, 3, 0), new(0, 3, 0), new(2.5, 3, 0), new(0, 5, 0), new(2.5, 5, 0), new(6, 5, 0), new(4.2, 3, 0)];
        MeshPiece[] pieces = [new([0, 1, 2, 3], "slab"), new([3, 4, 6, 5], "slab"), new([4, 2, 7, 6], "slab")];
        var m = ShellMesher.Mesh(nodes, pieces, [8], 0.4);
        Sound(m, Vec3.UnitZ, 6 * 5);
        Conforming(m);
        Assert.Contains(m.Faces, f => f.Nodes.Contains(8));
        // The beam along the shared edge from (0, 3) to (6, 3): its nodes in order, the column's among them.
        var along = m.NodesAlong(3, 2);
        Assert.Equal(3, along[0]);
        Assert.Equal(2, along[^1]);
        Assert.Contains(4, along);
        Assert.Contains(8, along);
        for (var i = 1; i < along.Count; i++)
        {
            Assert.True(m.Nodes[along[i]].X > m.Nodes[along[i - 1]].X);
            Assert.True(m.Nodes[along[i]].X - m.Nodes[along[i - 1]].X <= 0.4 + 1e-9);
        }
        output.WriteLine($"T: {m.Quads} quadrilaterals, {m.Triangles} triangles");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(3)]
    [InlineData(8)]
    public void A_convex_piece_is_meshed_in_mostly_quadrilaterals(int sides)
    {
        // A regular polygon of radius 3, a little squashed.
        var nodes = Enumerable.Range(0, sides).Select(i => new Vec3(3 * Math.Cos(2 * Math.PI * i / sides + 0.2), 2.2 * Math.Sin(2 * Math.PI * i / sides + 0.2), 1)).ToList();
        var m = ShellMesher.Mesh(nodes, [new MeshPiece(Enumerable.Range(0, sides).ToList(), "slab")], [], 0.35);
        var area = VecMath.NewellNormal(nodes).Length;
        Sound(m, Vec3.UnitZ, area);
        Conforming(m);
        var quadArea = m.Faces.Where(f => f.Nodes.Length == 4).Sum(f => FaceArea(m, f, Vec3.UnitZ));
        output.WriteLine($"{sides} sides: {m.Quads} quadrilaterals, {m.Triangles} triangles, {quadArea / area:P0} of the area in quadrilaterals");
        Assert.True(quadArea > 0.85 * area, $"{quadArea / area:P0}");
    }

    [Fact]
    public void A_walls_faces_turn_to_the_normal_asked()
    {
        // A wall along X, 5 m long and 3 m high, its corners round it clockwise seen from +Y; its normal asked as −Y.
        List<Vec3> nodes = [new(0, 0, 0), new(0, 0, 3), new(5, 0, 3), new(5, 0, 0)];
        var normal = new Vec3(0, -1, 0);
        var m = ShellMesher.Mesh(nodes, [new MeshPiece([0, 1, 2, 3], "wall", normal)], [], 0.5);
        Sound(m, normal, 15);
        var asked = ShellMesher.Mesh(nodes, [new MeshPiece([0, 1, 2, 3], "wall", -normal)], [], 0.5);
        Sound(asked, -normal, 15);
    }

    [Fact]
    public void A_plate_meshed_with_triangles_left_over_still_follows_plate_theory()
    {
        // A simply supported square 4 × 4 cut along its diagonal into two triangular pieces: each meshed unstructured
        // (quadrilaterals and some triangles); the deflection at the centre within 2 % of Navier's 0.00406 q a⁴/D.
        const double a = 4, t = 0.04, q = 10, E = 30e6, nu = 0.3;
        List<Vec3> corners = [new(0, 0, 0), new(a, 0, 0), new(a, a, 0), new(0, a, 0), new(a / 2, a / 2, 0)];
        var m = ShellMesher.Mesh(corners, [new MeshPiece([0, 1, 2], "plate"), new MeshPiece([0, 2, 3], "plate")], [4], 0.25);
        Sound(m, Vec3.UnitZ, a * a);
        Conforming(m);
        output.WriteLine($"diagonal: {m.Quads} quadrilaterals, {m.Triangles} triangles");
        var p = new FrameProblem();
        for (var i = 0; i < m.Nodes.Count; i++)
        {
            var x = m.Nodes[i];
            bool onX = Math.Abs(x.X) < 1e-9 || Math.Abs(x.X - a) < 1e-9, onY = Math.Abs(x.Y) < 1e-9 || Math.Abs(x.Y - a) < 1e-9;
            p.AddNode(x, $"N{i}", [true, true, onX || onY, onX, onY, true]);
        }
        var c = new CaseLoads("q");
        foreach (var f in m.Faces)
        {
            p.Shells.Add(new ShellElement { Id = $"s{p.Shells.Count}", Nodes = f.Nodes, E = E, Nu = nu, Thickness = t, Group = "plate" });
            c.Surface.Add((p.Shells.Count - 1, new Vec3(0, 0, -q)));
        }
        p.Cases.Add(c);
        var s = FrameSolver.Solve(p);
        Assert.True(s.Ok);
        Assert.True(s.Cases[0].Balance < 1e-9);
        var d = E * t * t * t / (12 * (1 - nu * nu));
        var w = -s.Cases[0].U[4 * 6 + 2];
        var expected = 0.00406235 * q * Math.Pow(a, 4) / d;
        output.WriteLine($"w {w / expected:F4}");
        Assert.True(Math.Abs(w / expected - 1) < 0.02, $"{w / expected}");
    }
}
