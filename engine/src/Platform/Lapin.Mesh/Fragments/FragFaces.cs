namespace Lapin.Mesh.Fragments;

/// <summary>
/// A shell's triangles as Fragments wants its faces: one profile per planar
/// region, holes as holes (decision 323). The official importer writes a
/// wall's side as one polygon with its window as a hole; Lapin wrote every
/// triangle as a profile of its own, a FlatBuffer table each: twice the profiles,
/// the file a third larger, the viewer triangulating three points at a time.
/// <para>
/// The triangles of one face (one face id: one plane) are welded already;
/// the edges used once are the region's boundary, chained into loops; a loop
/// turning with the face's normal is an outer boundary, one turning against
/// it a hole, given to the outer loop that holds it. Anything that is not
/// a clean region (an edge used twice the same way, a vertex the boundary
/// passes twice, a hole held by no loop, the area not the triangles') keeps
/// its triangles, so the merge can only lose size, never geometry.
/// </para>
/// </summary>
public static class FragFaces
{
    /// <summary>The polygons of a shell: profiles (point indices, one loop each), holes (indices, the profile they are cut from) and a face id per profile.</summary>
    public sealed record Polygons(List<int[]> Profiles, List<(int[] Indices, int Profile)> Holes, List<ushort> FaceIds);

    public static Polygons Of(float[] pts, int[] tris, ushort[] faceIds)
    {
        var o = new Polygons([], [], []);
        var byFace = new Dictionary<ushort, List<int>>();
        var order = new List<ushort>();
        for (var t = 0; t < faceIds.Length; t++)
        {
            if (!byFace.TryGetValue(faceIds[t], out var list)) { byFace[faceIds[t]] = list = []; order.Add(faceIds[t]); }
            list.Add(t);
        }
        foreach (var face in order)
        {
            var list = byFace[face];
            if (list.Count == 1 || !Merge(pts, tris, list, face, o))
                foreach (var t in list) { o.Profiles.Add([tris[t * 3], tris[t * 3 + 1], tris[t * 3 + 2]]); o.FaceIds.Add(face); }
        }
        return o;
    }

    /// <summary>One face's triangles merged into polygons with holes, added to <paramref name="o"/>; false (nothing added) when the region is not clean.</summary>
    static bool Merge(float[] pts, int[] tris, List<int> faceTris, ushort face, Polygons o)
    {
        // The face's normal (the triangles' summed), its plane basis, the triangles' area.
        double nx = 0, ny = 0, nz = 0, area = 0;
        var edges = new HashSet<(int, int)>();
        foreach (var t in faceTris)
        {
            int a = tris[t * 3], b = tris[t * 3 + 1], c = tris[t * 3 + 2];
            if (a == b || b == c || c == a) return false;
            var (cx, cy, cz) = Cross(pts, a, b, c);
            nx += cx; ny += cy; nz += cz;
            area += Math.Sqrt(cx * cx + cy * cy + cz * cz) / 2;
            if (!edges.Add((a, b)) || !edges.Add((b, c)) || !edges.Add((c, a))) return false;
        }
        var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        if (len < 1e-12 || area < 1e-12) return false;
        (nx, ny, nz) = (nx / len, ny / len, nz / len);

        // The boundary: the edges whose reverse the face does not have; at most one leaving each vertex.
        var next = new Dictionary<int, int>();
        foreach (var (a, b) in edges)
        {
            if (edges.Contains((b, a))) continue;
            if (!next.TryAdd(a, b)) return false;
        }
        if (next.Count < 3) return false;
        var loops = new List<List<int>>();
        var used = new HashSet<int>();
        foreach (var start in next.Keys)
        {
            if (used.Contains(start)) continue;
            var loop = new List<int>();
            var v = start;
            while (used.Add(v))
            {
                loop.Add(v);
                if (!next.TryGetValue(v, out v)) return false;
            }
            if (v != start || loop.Count < 3) return false;
            loops.Add(loop);
        }

        // Outer loops turn with the normal, holes against it; the areas must be the triangles'.
        var (ux, uy, uz, vx, vy, vz) = Basis(nx, ny, nz);
        var outers = new List<(List<int> Loop, double Area)>();
        var holes = new List<List<int>>();
        double net = 0;
        foreach (var loop in loops)
        {
            var a = SignedArea(pts, loop, ux, uy, uz, vx, vy, vz);
            if (Math.Abs(a) < 1e-14) return false;
            net += a;
            if (a > 0) outers.Add((loop, a)); else holes.Add(loop);
        }
        if (outers.Count == 0 || Math.Abs(net - area) > 1e-6 * Math.Max(area, 1e-3)) return false;
        var holder = new int[holes.Count];
        for (var h = 0; h < holes.Count; h++)
        {
            holder[h] = -1;
            if (outers.Count == 1) { holder[h] = 0; continue; }
            // The smallest outer loop holding the hole's first point.
            var (px, py) = Flat(pts, holes[h][0], ux, uy, uz, vx, vy, vz);
            var best = double.MaxValue;
            for (var k = 0; k < outers.Count; k++)
                if (outers[k].Area < best && Contains(pts, outers[k].Loop, px, py, ux, uy, uz, vx, vy, vz)) { best = outers[k].Area; holder[h] = k; }
            if (holder[h] < 0) return false;
        }
        // A hole names its profile by a ushort: past that, the triangles.
        if (o.Profiles.Count + outers.Count > ushort.MaxValue) return false;
        var first = o.Profiles.Count;
        foreach (var (loop, _) in outers) { o.Profiles.Add([.. loop]); o.FaceIds.Add(face); }
        for (var h = 0; h < holes.Count; h++) o.Holes.Add(([.. holes[h]], first + holder[h]));
        return true;
    }

    static (double, double, double) Cross(float[] p, int a, int b, int c)
    {
        double ux = p[b * 3] - p[a * 3], uy = p[b * 3 + 1] - p[a * 3 + 1], uz = p[b * 3 + 2] - p[a * 3 + 2];
        double vx = p[c * 3] - p[a * 3], vy = p[c * 3 + 1] - p[a * 3 + 1], vz = p[c * 3 + 2] - p[a * 3 + 2];
        return (uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx);
    }

    /// <summary>Two axes in the plane, (u, v, n) right-handed, so a loop turning with n has a positive area.</summary>
    static (double, double, double, double, double, double) Basis(double nx, double ny, double nz)
    {
        // Any axis not along n, crossed with it.
        var (ax, ay, az) = Math.Abs(nx) < 0.9 ? (1.0, 0.0, 0.0) : (0.0, 1.0, 0.0);
        double ux = ay * nz - az * ny, uy = az * nx - ax * nz, uz = ax * ny - ay * nx;
        var l = Math.Sqrt(ux * ux + uy * uy + uz * uz);
        (ux, uy, uz) = (ux / l, uy / l, uz / l);
        // v = n × u
        return (ux, uy, uz, ny * uz - nz * uy, nz * ux - nx * uz, nx * uy - ny * ux);
    }

    static (double X, double Y) Flat(float[] p, int i, double ux, double uy, double uz, double vx, double vy, double vz) =>
        (p[i * 3] * ux + p[i * 3 + 1] * uy + p[i * 3 + 2] * uz, p[i * 3] * vx + p[i * 3 + 1] * vy + p[i * 3 + 2] * vz);

    static double SignedArea(float[] p, List<int> loop, double ux, double uy, double uz, double vx, double vy, double vz)
    {
        double s = 0;
        for (var i = 0; i < loop.Count; i++)
        {
            var (x0, y0) = Flat(p, loop[i], ux, uy, uz, vx, vy, vz);
            var (x1, y1) = Flat(p, loop[(i + 1) % loop.Count], ux, uy, uz, vx, vy, vz);
            s += x0 * y1 - x1 * y0;
        }
        return s / 2;
    }

    static bool Contains(float[] p, List<int> loop, double px, double py, double ux, double uy, double uz, double vx, double vy, double vz)
    {
        var inside = false;
        for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
        {
            var (xi, yi) = Flat(p, loop[i], ux, uy, uz, vx, vy, vz);
            var (xj, yj) = Flat(p, loop[j], ux, uy, uz, vx, vy, vz);
            if ((yi > py) != (yj > py) && px < (xj - xi) * (py - yi) / (yj - yi) + xi) inside = !inside;
        }
        return inside;
    }
}
