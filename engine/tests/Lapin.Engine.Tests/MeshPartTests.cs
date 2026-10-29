using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A big stored body is a mesh part (docs/plan-instancing.md, "A real model"): drawn as a mesh with shared vertices,
/// smooth normals and only its crease edges; its solid made when a consumer asks, not before.
/// </summary>
public class MeshPartTests
{
    /// <summary>A V-shaped sheet (z = |x|) over an n × n grid, two triangles a square: its only fold is along x = 0.</summary>
    static (List<Vec3> V, List<(int, int, int)> T) Fold(int n)
    {
        var v = new List<Vec3>();
        var t = new List<(int, int, int)>();
        for (var j = 0; j <= n; j++)
            for (var i = 0; i <= n; i++)
            {
                double x = (double)i / n - 0.5, y = (double)j / n - 0.5;
                v.Add(new Vec3(x, y, Math.Abs(x)));
            }
        for (var j = 0; j < n; j++)
            for (var i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                t.Add((a, b, d)); t.Add((a, d, c));
            }
        return (v, t);
    }

    static Entity Body(List<Vec3> v, List<(int, int, int)> t) =>
        new("m", "IfcBuildingElementProxy", "m", new JsonObject { [StoredBodies.Key] = StoredBodies.Write([("Body", "#c9ced6", v, t)]) });

    [Fact]
    public void A_big_body_is_drawn_as_a_mesh_with_shared_vertices_and_its_crease_edges_only()
    {
        const int n = 240; // 115,200 triangles: over the mesh threshold
        var (v, t) = Fold(n);
        var g = StoredBodies.Geometry(Body(v, t), new ModelStore(), null!)!;
        var part = Assert.Single(g.Parts);
        Assert.NotNull(part.Mesh);
        Assert.False(part.HasSolid);
        Assert.Equal(n * n * 2, part.FaceCount);
        var m = part.Tessellate();
        // Each vertex once, not once per triangle; every triangle; a unit normal each.
        Assert.Equal(v.Count * 3, m.Positions.Length);
        Assert.Equal(n * n * 2 * 3, m.Indices.Length);
        for (var k = 0; k < m.Normals.Length; k += 3)
            Assert.Equal(1, Math.Sqrt(m.Normals[k] * m.Normals[k] + m.Normals[k + 1] * m.Normals[k + 1] + m.Normals[k + 2] * m.Normals[k + 2]), 4);
        // The edges: the four sides (4n) and the fold (n); not the triangles' own.
        Assert.Equal((4 * n + n) * 6, m.Edges.Length);
        var onFold = 0;
        for (var k = 0; k < m.Edges.Length; k += 6) if (Math.Abs(m.Edges[k]) < 1e-9 && Math.Abs(m.Edges[k + 3]) < 1e-9) onFold++;
        Assert.Equal(n, onFold);
        // On the fold, the normal is the mean of both sides: straight up.
        var mid = (n / 2) * (n + 1) + n / 2;
        Assert.Equal(0, m.Normals[3 * mid], 6);
        Assert.Equal(1, m.Normals[3 * mid + 2], 6);
        // The box without the solid; the solid when asked, then kept.
        Assert.False(part.HasSolid);
        var box = part.Bounds()!.Value;
        Assert.Equal(-0.5, box.Min.X, 9); Assert.Equal(0.5, box.Max.Z, 9);
        Assert.Equal(n * n * 2, part.Solid.Faces.Length);
        Assert.True(part.HasSolid);
        Assert.Same(part.Solid, part.Solid);
        // A solid given anew (a cut) stands on its own: the mesh goes.
        var cut = part with { Solid = new Solid([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)], [new Face([0, 2, 1], [], new(0, 0, -1)), new Face([0, 1, 3], [], new(0, -1, 0)), new Face([1, 2, 3], [], new Vec3(1, 1, 1).Normalized()), new Face([0, 3, 2], [], new(-1, 0, 0))]) };
        Assert.Null(cut.Mesh);
        Assert.Equal(4, cut.FaceCount);
    }

    [Fact]
    public void A_small_body_is_a_solid_as_before()
    {
        // A unit cube of twelve triangles.
        List<Vec3> v = [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)];
        List<(int, int, int)> t = [(0, 2, 1), (0, 3, 2), (4, 5, 6), (4, 6, 7), (0, 1, 5), (0, 5, 4), (1, 2, 6), (1, 6, 5), (2, 3, 7), (2, 7, 6), (3, 0, 4), (3, 4, 7)];
        var part = Assert.Single(StoredBodies.Geometry(Body(v, t), new ModelStore(), null!)!.Parts);
        Assert.Null(part.Mesh);
        Assert.True(part.HasSolid);
        // Coplanar triangles merged: six faces.
        Assert.Equal(6, part.Solid.Faces.Length);
        // An open sheet too small to be a solid stays triangle by triangle, as before.
        var (sv, st) = Fold(20);
        Assert.Equal(800, Assert.Single(StoredBodies.Geometry(Body(sv, st), new ModelStore(), null!)!.Parts).FaceCount);
    }
}
