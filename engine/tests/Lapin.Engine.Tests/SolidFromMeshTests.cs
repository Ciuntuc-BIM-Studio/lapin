using System.Diagnostics;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>A triangle mesh read as a solid (SolidFromMesh): coplanar triangles merged into faces, in time that grows with the mesh, open meshes too.</summary>
public class SolidFromMeshTests
{
    /// <summary>A height field over n × n squares, two triangles each, on a curved surface (no two triangles coplanar), closed underneath by nothing: open.</summary>
    static (List<Vec3> V, List<(int, int, int)> T) Dome(int n)
    {
        var v = new List<Vec3>();
        var t = new List<(int, int, int)>();
        for (var j = 0; j <= n; j++)
            for (var i = 0; i <= n; i++)
            {
                double x = (double)i / n - 0.5, y = (double)j / n - 0.5;
                v.Add(new Vec3(x, y, 1 - x * x - y * y * 1.3));
            }
        for (var j = 0; j < n; j++)
            for (var i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                t.Add((a, b, d)); t.Add((a, d, c));
            }
        return (v, t);
    }

    [Fact]
    public void A_big_mesh_is_read_in_time_that_grows_with_it()
    {
        // 180,000 triangles: their edges once crowded a few hash buckets (a long's hash is its halves XOR'd), which took minutes.
        var (v, t) = Dome(300);
        var sw = Stopwatch.StartNew();
        var s = SolidFromMesh.Build(v, t);
        Assert.InRange(sw.Elapsed.TotalSeconds, 0, 15);
        Assert.NotNull(s);
        // Its squares are flat (a separable height), so each square's two triangles are one face.
        Assert.Equal(90_000, s!.Faces.Length);
    }

    [Fact]
    public void An_open_mesh_whose_boundary_ends_nowhere_is_read_without_failing()
    {
        // Two triangles sharing an edge, and a third hanging off one corner by a single vertex: boundaries that stop.
        var v = new List<Vec3> { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 0.2), new(2, 0, 0.5), new(2, 1, 1) };
        var t = new List<(int, int, int)> { (0, 1, 2), (1, 3, 2), (1, 4, 5), (0, 2, 5) };
        var ex = Record.Exception(() => SolidFromMesh.Build(v, t));
        Assert.Null(ex);
    }
}
