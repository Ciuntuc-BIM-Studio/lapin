using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// A mesh simplified within a tolerance (docs/spec-ifc.md, Geometria la export): fewer triangles, never further
/// from the surface than asked; flat areas lose their inner vertices; an outline stays.
/// </summary>
public class SimplifierTests
{
    /// <summary>An open tube (a duct as a file meshes it): radius r, length l, <paramref name="around"/> facets round, <paramref name="along"/> rings.</summary>
    static (List<Vec3> V, List<(int A, int B, int C)> T) Tube(double r, double l, int around, int along)
    {
        var v = new List<Vec3>();
        var t = new List<(int A, int B, int C)>();
        for (var j = 0; j <= along; j++)
            for (var i = 0; i < around; i++)
            {
                var a = 2 * Math.PI * i / around;
                v.Add(new Vec3(r * Math.Cos(a), r * Math.Sin(a), l * j / along));
            }
        for (var j = 0; j < along; j++)
            for (var i = 0; i < around; i++)
            {
                int p = j * around + i, q = j * around + (i + 1) % around, s = p + around, u = q + around;
                t.Add((p, q, u)); t.Add((p, u, s));
            }
        return (v, t);
    }

    [Fact]
    public void A_finely_meshed_duct_keeps_its_shape_within_the_tolerance_with_far_fewer_triangles()
    {
        const double r = 0.1, tol = 0.002;
        var (v, t) = Tube(r, 2, 128, 40);
        var (sv, st) = Simplifier.Simplify(v, t, tol);
        // Fewer: the rings along the length go (it is straight), and round it as many facets as 2 mm allows.
        Assert.True(st.Count < t.Count / 20, $"{t.Count} → {st.Count}");
        // Never further than asked: every vertex within the tolerance of the surface, every face's middle too.
        foreach (var p in sv) Assert.InRange(Math.Sqrt(p.X * p.X + p.Y * p.Y), r - tol, r + tol);
        foreach (var (a, b, c) in st)
        {
            var m = (sv[a] + sv[b] + sv[c]) * (1.0 / 3);
            Assert.InRange(Math.Sqrt(m.X * m.X + m.Y * m.Y), r - tol, r + tol);
        }
        // The ends stay where they were: the tube is as long as it was.
        Assert.Equal(0, sv.Min(p => p.Z), 9);
        Assert.Equal(2, sv.Max(p => p.Z), 9);
        // No face turned over: each faces out from the axis.
        foreach (var (a, b, c) in st)
        {
            var n = Vec3.Cross(sv[b] - sv[a], sv[c] - sv[a]);
            var m = (sv[a] + sv[b] + sv[c]) * (1.0 / 3);
            Assert.True(n.X * m.X + n.Y * m.Y > 0);
        }
    }

    [Fact]
    public void A_tighter_tolerance_keeps_more_and_zero_keeps_a_curve_as_it_is()
    {
        var (v, t) = Tube(0.1, 1, 128, 10);
        var fine = Simplifier.Simplify(v, t, 0.0002).Triangles.Count;
        var coarse = Simplifier.Simplify(v, t, 0.005).Triangles.Count;
        Assert.True(coarse < fine && fine < t.Count, $"{t.Count}, {fine}, {coarse}");
        // At zero only what is flat goes: along the straight length, never round the curve.
        var (zv, zt) = Simplifier.Simplify(v, t, 0);
        Assert.Equal(128, zv.Select(p => Math.Round(Math.Atan2(p.Y, p.X), 6)).Distinct().Count());
        Assert.True(zt.Count < t.Count);
    }

    [Fact]
    public void A_big_mesh_of_separate_pieces_is_simplified_piece_by_piece_as_it_would_be_whole()
    {
        // Two tubes, 20,480 triangles each, sharing no vertex (in the same place, so their arithmetic is the same):
        // over the size where the pieces go to every core.
        var (v, t) = Tube(0.1, 2, 128, 80);
        var one = Simplifier.Simplify(v, t, 0.002);
        var both = new List<Vec3>(v);
        both.AddRange(v);
        var tris = new List<(int A, int B, int C)>(t);
        tris.AddRange(t.Select(x => (x.A + v.Count, x.B + v.Count, x.C + v.Count)));
        var two = Simplifier.Simplify(both, tris, 0.002);
        Assert.Equal(2 * one.Triangles.Count, two.Triangles.Count);
        Assert.Equal(2 * one.Vertices.Length, two.Vertices.Length);
    }

    [Fact]
    public void A_box_meshed_finely_comes_back_as_six_faces()
    {
        // A closed box whose faces are grids of triangles, as an exporter might mesh it.
        var verts = new List<Vec3>();
        var tris = new List<(int A, int B, int C)>();
        var index = new Dictionary<(long, long, long), int>();
        int At(double x, double y, double z)
        {
            var k = ((long)Math.Round(x * 1e6), (long)Math.Round(y * 1e6), (long)Math.Round(z * 1e6));
            if (!index.TryGetValue(k, out var i)) { index[k] = i = verts.Count; verts.Add(new Vec3(x, y, z)); }
            return i;
        }
        const int n = 8;
        void Side(Func<double, double, Vec3> p)
        {
            for (var i = 0; i < n; i++)
                for (var j = 0; j < n; j++)
                {
                    var a = p(i / (double)n, j / (double)n); var b = p((i + 1) / (double)n, j / (double)n);
                    var c = p((i + 1) / (double)n, (j + 1) / (double)n); var d = p(i / (double)n, (j + 1) / (double)n);
                    int ia = At(a.X, a.Y, a.Z), ib = At(b.X, b.Y, b.Z), ic = At(c.X, c.Y, c.Z), id = At(d.X, d.Y, d.Z);
                    tris.Add((ia, ib, ic)); tris.Add((ia, ic, id));
                }
        }
        Side((u, w) => new Vec3(w, u, 0)); Side((u, w) => new Vec3(u, w, 1));
        Side((u, w) => new Vec3(u, 0, w)); Side((u, w) => new Vec3(w, 1, u));
        Side((u, w) => new Vec3(0, w, u)); Side((u, w) => new Vec3(1, u, w));
        var solid = new Solid([.. verts], [.. tris.Select(x => new Face([x.A, x.B, x.C], [], Vec3.Cross(verts[x.B] - verts[x.A], verts[x.C] - verts[x.A]).Normalized()))]);
        var simple = Simplifier.Simplify(solid, 0.001);
        Assert.Equal(6, simple.Faces.Length);
        Assert.Equal(8, simple.Faces.SelectMany(f => f.Outer).Distinct().Count());
        Assert.Equal(1.0, Measure.Volume(simple), 9);
    }
}
