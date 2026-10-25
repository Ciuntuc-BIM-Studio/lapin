using System.Runtime.InteropServices;
using Lapin.Geometry;

namespace Lapin.Booleans;

/// <summary>
/// Booleans by manifold, in double precision (MeshGL64). A solid goes in as
/// its faces triangulated on its own vertex pool (so the mesh is closed), the
/// result comes back as planar faces (<see cref="SolidFromMesh"/>).
/// </summary>
public sealed class ManifoldBooleans : ISolidBooleans
{
    const string Lib = "manifoldc";

    [DllImport(Lib)] static extern IntPtr manifold_alloc_meshgl64();
    [DllImport(Lib)] static extern IntPtr manifold_alloc_manifold();
    [DllImport(Lib)] static extern unsafe IntPtr manifold_meshgl64(IntPtr mem, double* vertProps, nuint nVerts, nuint nProps, ulong* triVerts, nuint nTris);
    [DllImport(Lib)] static extern IntPtr manifold_of_meshgl64(IntPtr mem, IntPtr mesh);
    [DllImport(Lib)] static extern IntPtr manifold_get_meshgl64(IntPtr mem, IntPtr m);
    [DllImport(Lib)] static extern int manifold_status(IntPtr m);
    [DllImport(Lib)] static extern int manifold_is_empty(IntPtr m);
    [DllImport(Lib)] static extern IntPtr manifold_difference(IntPtr mem, IntPtr a, IntPtr b);
    [DllImport(Lib)] static extern IntPtr manifold_union(IntPtr mem, IntPtr a, IntPtr b);
    [DllImport(Lib)] static extern IntPtr manifold_intersection(IntPtr mem, IntPtr a, IntPtr b);
    [DllImport(Lib)] static extern nuint manifold_meshgl64_num_prop(IntPtr m);
    [DllImport(Lib)] static extern nuint manifold_meshgl64_num_vert(IntPtr m);
    [DllImport(Lib)] static extern nuint manifold_meshgl64_num_tri(IntPtr m);
    [DllImport(Lib)] static extern unsafe double* manifold_meshgl64_vert_properties(double* mem, IntPtr m);
    [DllImport(Lib)] static extern unsafe ulong* manifold_meshgl64_tri_verts(ulong* mem, IntPtr m);
    [DllImport(Lib)] static extern void manifold_delete_manifold(IntPtr m);
    [DllImport(Lib)] static extern void manifold_delete_meshgl64(IntPtr m);

    /// <summary>Whether the native library is there (a build without it still runs; booleans are then unavailable).</summary>
    public static bool Available { get; } = Probe();

    static bool Probe() => NativeLibrary.TryLoad(Lib, typeof(ManifoldBooleans).Assembly, null, out _);

    public Solid? Difference(Solid a, IReadOnlyList<Solid> cutters, string? tag = null) =>
        Run(a, cutters, manifold_difference, tag ?? a.Tag);

    public Solid? Union(IReadOnlyList<Solid> solids, string? tag = null) =>
        solids.Count == 0 ? null : Run(solids[0], solids.Skip(1).ToList(), manifold_union, tag ?? solids[0].Tag);

    public Solid? Intersection(Solid a, Solid b, string? tag = null) =>
        Run(a, [b], manifold_intersection, tag ?? a.Tag);

    static Solid? Run(Solid first, IReadOnlyList<Solid> others, Func<IntPtr, IntPtr, IntPtr, IntPtr> op, string? tag)
    {
        var live = new List<IntPtr>();
        try
        {
            var acc = ToManifold(first, live);
            foreach (var o in others)
            {
                var m = ToManifold(o, live);
                var r = op(manifold_alloc_manifold(), acc, m);
                live.Add(r);
                Check(r);
                acc = r;
            }
            return manifold_is_empty(acc) != 0 ? null : FromManifold(acc, tag);
        }
        finally
        {
            foreach (var p in live) manifold_delete_manifold(p);
        }
    }

    static void Check(IntPtr m)
    {
        var status = manifold_status(m);
        if (status != 0) throw new InvalidOperationException($"manifold: error {status} (the solid is not closed and orientable).");
    }

    static unsafe IntPtr ToManifold(Solid s, List<IntPtr> live)
    {
        var tris = new List<ulong>();
        foreach (var f in s.Faces)
            foreach (var (a, b, c) in Triangulator.Face(s.Vertices, f.Outer, f.Holes, f.Normal))
                tris.AddRange([(ulong)a, (ulong)b, (ulong)c]);
        var props = s.Vertices.SelectMany(v => new[] { v.X, v.Y, v.Z }).ToArray();
        var triArr = tris.ToArray();
        IntPtr mesh;
        fixed (double* pp = props)
        fixed (ulong* tp = triArr)
            mesh = manifold_meshgl64(manifold_alloc_meshgl64(), pp, (nuint)s.Vertices.Length, 3, tp, (nuint)(triArr.Length / 3));
        try
        {
            var m = manifold_of_meshgl64(manifold_alloc_manifold(), mesh);
            live.Add(m);
            Check(m);
            return m;
        }
        finally { manifold_delete_meshgl64(mesh); }
    }

    static unsafe Solid? FromManifold(IntPtr m, string? tag)
    {
        var mesh = manifold_get_meshgl64(manifold_alloc_meshgl64(), m);
        try
        {
            int np = (int)manifold_meshgl64_num_prop(mesh), nv = (int)manifold_meshgl64_num_vert(mesh), nt = (int)manifold_meshgl64_num_tri(mesh);
            var props = new double[nv * np];
            var tris = new ulong[nt * 3];
            fixed (double* pp = props) manifold_meshgl64_vert_properties(pp, mesh);
            fixed (ulong* tp = tris) manifold_meshgl64_tri_verts(tp, mesh);
            var verts = Enumerable.Range(0, nv).Select(i => new Vec3(props[i * np], props[i * np + 1], props[i * np + 2])).ToList();
            var triangles = Enumerable.Range(0, nt).Select(i => ((int)tris[3 * i], (int)tris[3 * i + 1], (int)tris[3 * i + 2])).ToList();
            return SolidFromMesh.Build(verts, triangles, tag);
        }
        finally { manifold_delete_meshgl64(mesh); }
    }
}
