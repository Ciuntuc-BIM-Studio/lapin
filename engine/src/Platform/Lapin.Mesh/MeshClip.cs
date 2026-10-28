using Lapin.Geometry;

namespace Lapin.Mesh;

/// <summary>
/// A mesh cut as the 3D view cuts what it draws (three.js's clipping: what has
/// Normal·p + Constant ≥ 0 is kept), so an export "as the view shows it" holds
/// the same model the screen does: triangles wholly kept stay as they are, a
/// triangle a plane crosses is cut along it (Sutherland–Hodgman, the polygon
/// left fanned back into triangles), what is wholly cut away goes; the edges
/// are cut the same way. The caps that close the cut come from <see cref="SectionCaps"/>.
/// </summary>
public static class MeshClip
{
    /// <summary>Points this little outside a plane still count as on it (a face lying on the cut stays).</summary>
    const double Eps = 1e-6;

    /// <summary>The planes with unit normals, so that distances are metres.</summary>
    public static ClipPlane[] Unit(IReadOnlyList<ClipPlane> planes) =>
        planes.Where(p => p.Normal.Length > 1e-12).Select(p => new ClipPlane(p.Normal * (1 / p.Normal.Length), p.Constant / p.Normal.Length)).ToArray();

    /// <summary>The mesh less what the planes cut away (its normals kept: each piece of a face keeps the face's).</summary>
    public static TriMesh Clip(TriMesh mesh, IReadOnlyList<ClipPlane> planes)
    {
        if (planes.Count == 0 || mesh.Indices.Length == 0) return mesh;
        var pos = mesh.Positions;
        Vec3 At(int i) => new(pos[3 * i], pos[3 * i + 1], pos[3 * i + 2]);
        bool Inside(int i) { var v = At(i); foreach (var p in planes) if (p.Distance(v) < -Eps) return false; return true; }

        var inside = new bool[pos.Length / 3];
        var allIn = true;
        for (var i = 0; i < inside.Length; i++) { inside[i] = Inside(i); allIn &= inside[i]; }
        if (allIn) return mesh with { Edges = ClipSegments(mesh.Edges, planes) };

        var p2 = new List<float>(); var n2 = new List<float>(); var ix = new List<int>();
        var reused = new Dictionary<int, int>();
        int Keep(int i)
        {
            if (reused.TryGetValue(i, out var k)) return k;
            k = p2.Count / 3;
            p2.Add(pos[3 * i]); p2.Add(pos[3 * i + 1]); p2.Add(pos[3 * i + 2]);
            n2.Add(mesh.Normals[3 * i]); n2.Add(mesh.Normals[3 * i + 1]); n2.Add(mesh.Normals[3 * i + 2]);
            return reused[i] = k;
        }
        int Add(Vec3 v, int normalOf)
        {
            var k = p2.Count / 3;
            p2.Add((float)v.X); p2.Add((float)v.Y); p2.Add((float)v.Z);
            n2.Add(mesh.Normals[3 * normalOf]); n2.Add(mesh.Normals[3 * normalOf + 1]); n2.Add(mesh.Normals[3 * normalOf + 2]);
            return k;
        }
        for (var t = 0; t + 2 < mesh.Indices.Length; t += 3)
        {
            int a = mesh.Indices[t], b = mesh.Indices[t + 1], c = mesh.Indices[t + 2];
            if (inside[a] && inside[b] && inside[c]) { ix.Add(Keep(a)); ix.Add(Keep(b)); ix.Add(Keep(c)); continue; }
            var poly = ClipPolygon([At(a), At(b), At(c)], planes);
            if (poly.Count < 3) continue;
            var first = Add(poly[0], a);
            var prev = Add(poly[1], a);
            for (var i = 2; i < poly.Count; i++)
            {
                var next = Add(poly[i], a);
                ix.Add(first); ix.Add(prev); ix.Add(next);
                prev = next;
            }
        }
        return new TriMesh([.. p2], [.. n2], [.. ix], ClipSegments(mesh.Edges, planes));
    }

    /// <summary>A convex polygon less what the planes cut away (Sutherland–Hodgman), its corners in order.</summary>
    public static List<Vec3> ClipPolygon(List<Vec3> poly, IReadOnlyList<ClipPlane> planes)
    {
        foreach (var p in planes)
        {
            if (poly.Count < 3) break;
            var next = new List<Vec3>(poly.Count + 1);
            for (var i = 0; i < poly.Count; i++)
            {
                Vec3 a = poly[i], b = poly[(i + 1) % poly.Count];
                double da = p.Distance(a), db = p.Distance(b);
                bool ina = da >= -Eps, inb = db >= -Eps;
                if (ina) next.Add(a);
                if (ina != inb) next.Add(a + (b - a) * (da / (da - db)));
            }
            poly = next;
        }
        return poly;
    }

    /// <summary>Segments (six floats each) less what the planes cut away.</summary>
    public static float[] ClipSegments(float[] seg, IReadOnlyList<ClipPlane> planes)
    {
        if (planes.Count == 0 || seg.Length == 0) return seg;
        var o = new List<float>(seg.Length);
        for (var i = 0; i + 5 < seg.Length; i += 6)
        {
            var a = new Vec3(seg[i], seg[i + 1], seg[i + 2]);
            var b = new Vec3(seg[i + 3], seg[i + 4], seg[i + 5]);
            double t0 = 0, t1 = 1;
            foreach (var p in planes)
            {
                double da = p.Distance(a), db = p.Distance(b);
                if (da < -Eps && db < -Eps) { t0 = 1; t1 = 0; break; }
                if (da < -Eps) t0 = Math.Max(t0, da / (da - db));
                else if (db < -Eps) t1 = Math.Min(t1, da / (da - db));
            }
            if (t1 - t0 < 1e-9) continue;
            var p0 = a + (b - a) * t0;
            var p1 = a + (b - a) * t1;
            o.AddRange([(float)p0.X, (float)p0.Y, (float)p0.Z, (float)p1.X, (float)p1.Y, (float)p1.Z]);
        }
        return [.. o];
    }
}
