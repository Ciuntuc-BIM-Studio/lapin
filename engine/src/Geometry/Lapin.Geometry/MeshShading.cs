namespace Lapin.Geometry;

/// <summary>
/// A triangle mesh (a machine exported as millions of triangles) made ready to draw as a
/// mesh, not as a solid of faces: its vertices shared, each with the mean of its triangles'
/// normals (a curved surface shades smoothly), and only the edges where the surface folds
/// (<see cref="TriMesh.Edges"/>), or ends. Tessellating such a body face by face repeated
/// every vertex three times and drew every triangle's three edges: a black blob, four
/// times the memory.
/// </summary>
public static class MeshShading
{
    /// <summary>A mesh's render: shared vertices, averaged normals, crease edges (a fold of more than <paramref name="creaseDeg"/>, or an open edge).</summary>
    public static TriMesh Smooth(ReadOnlySpan<double> xyz, ReadOnlySpan<int> tris, double creaseDeg = 30)
    {
        var n = xyz.Length / 3;
        var positions = new float[n * 3];
        for (var i = 0; i < positions.Length; i++) positions[i] = (float)xyz[i];
        var normals = new float[n * 3];
        var triangles = tris.Length / 3;
        // Each triangle's normal (its area in it), added to its corners.
        var faceNormal = new (float X, float Y, float Z)[triangles];
        for (var t = 0; t < triangles; t++)
        {
            int a = tris[3 * t], b = tris[3 * t + 1], c = tris[3 * t + 2];
            if (a < 0 || b < 0 || c < 0 || a >= n || b >= n || c >= n) continue;
            double ux = xyz[3 * b] - xyz[3 * a], uy = xyz[3 * b + 1] - xyz[3 * a + 1], uz = xyz[3 * b + 2] - xyz[3 * a + 2];
            double vx = xyz[3 * c] - xyz[3 * a], vy = xyz[3 * c + 1] - xyz[3 * a + 1], vz = xyz[3 * c + 2] - xyz[3 * a + 2];
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len < 1e-18) continue;
            faceNormal[t] = ((float)(nx / len), (float)(ny / len), (float)(nz / len));
            foreach (var k in new[] { a, b, c }) { normals[3 * k] += (float)nx; normals[3 * k + 1] += (float)ny; normals[3 * k + 2] += (float)nz; }
        }
        for (var k = 0; k < n; k++)
        {
            var len = MathF.Sqrt(normals[3 * k] * normals[3 * k] + normals[3 * k + 1] * normals[3 * k + 1] + normals[3 * k + 2] * normals[3 * k + 2]);
            if (len > 0) { normals[3 * k] /= len; normals[3 * k + 1] /= len; normals[3 * k + 2] /= len; }
            else normals[3 * k + 2] = 1;
        }
        // The triangles on each undirected edge: a crease where the two fold past the angle, an edge where there is one.
        var onEdge = new Dictionary<long, (int First, int Second)>(triangles * 3 / 2, EdgeKeys.Comparer);
        for (var t = 0; t < triangles; t++)
            for (var e = 0; e < 3; e++)
            {
                int a = tris[3 * t + e], b = tris[3 * t + (e + 1) % 3];
                if (a == b || a < 0 || b < 0 || a >= n || b >= n) continue;
                var key = EdgeKeys.Undirected(a, b);
                if (onEdge.TryGetValue(key, out var had)) { if (had.Second < 0) onEdge[key] = (had.First, t); }
                else onEdge[key] = (t, -1);
            }
        var cos = (float)Math.Cos(creaseDeg * Math.PI / 180);
        var edges = new List<float>();
        foreach (var (key, (first, second)) in onEdge)
        {
            var crease = second < 0;
            if (!crease)
            {
                var (p, q) = (faceNormal[first], faceNormal[second]);
                crease = p.X * q.X + p.Y * q.Y + p.Z * q.Z < cos;
            }
            if (!crease) continue;
            var (a, b) = EdgeKeys.Ends(key);
            edges.Add(positions[3 * a]); edges.Add(positions[3 * a + 1]); edges.Add(positions[3 * a + 2]);
            edges.Add(positions[3 * b]); edges.Add(positions[3 * b + 1]); edges.Add(positions[3 * b + 2]);
        }
        var indices = new int[tris.Length];
        tris.CopyTo(indices);
        return new TriMesh(positions, normals, indices, [.. edges]);
    }

    /// <summary>A mesh's box from its coordinates (x y z …); null when empty.</summary>
    public static (Vec3 Min, Vec3 Max)? Bounds(ReadOnlySpan<double> xyz)
    {
        if (xyz.Length < 3) return null;
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        for (var i = 0; i + 2 < xyz.Length; i += 3)
        {
            x0 = Math.Min(x0, xyz[i]); y0 = Math.Min(y0, xyz[i + 1]); z0 = Math.Min(z0, xyz[i + 2]);
            x1 = Math.Max(x1, xyz[i]); y1 = Math.Max(y1, xyz[i + 1]); z1 = Math.Max(z1, xyz[i + 2]);
        }
        return (new Vec3(x0, y0, z0), new Vec3(x1, y1, z1));
    }
}

/// <summary>
/// An edge as one number, hashed by both ends mixed: a long's own hash XORs its halves, which for
/// an edge is a ^ b, small for nearby vertex numbers, so a mesh's edges crowded into a few buckets
/// and every lookup walked them (a Revit fan of 92,000 triangles took 4 s).
/// </summary>
public static class EdgeKeys
{
    public static long Pack(int a, int b) => ((long)a << 32) | (uint)b;
    public static long Undirected(int a, int b) => a < b ? Pack(a, b) : Pack(b, a);
    public static (int A, int B) Ends(long e) => ((int)(e >> 32), (int)e);

    public static readonly IEqualityComparer<long> Comparer = new Mixed();

    sealed class Mixed : IEqualityComparer<long>
    {
        public bool Equals(long a, long b) => a == b;
        public int GetHashCode(long e)
        {
            var h = (ulong)e * 0x9E3779B97F4A7C15UL;
            return (int)(h >> 32) ^ (int)h;
        }
    }
}
