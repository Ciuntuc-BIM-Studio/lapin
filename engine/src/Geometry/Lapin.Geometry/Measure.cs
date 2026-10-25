namespace Lapin.Geometry;

/// <summary>Measurements, all from the face triangulation (m, m², m³).</summary>
public static class Measure
{
    public static IEnumerable<(int A, int B, int C)> Triangles(Solid s) =>
        s.Faces.SelectMany(f => Triangulator.Face(s.Vertices, f.Outer, f.Holes, f.Normal));

    /// <summary>Area of one face: outer loop minus its holes.</summary>
    public static double FaceArea(Solid s, int faceIndex)
    {
        var f = s.Faces[faceIndex];
        var outer = VecMath.NewellNormal(f.Outer.Select(i => s.Vertices[i]).ToArray()).Length;
        var holes = f.Holes.Sum(h => VecMath.NewellNormal(h.Select(i => s.Vertices[i]).ToArray()).Length);
        return Math.Max(0, outer - holes);
    }

    public static double SurfaceArea(Solid s) => Enumerable.Range(0, s.Faces.Length).Sum(i => FaceArea(s, i));

    /// <summary>Area of the faces carrying <paramref name="tag"/>.</summary>
    public static double TaggedArea(Solid s, string tag) =>
        Enumerable.Range(0, s.Faces.Length).Where(i => s.Faces[i].Tag == tag).Sum(i => FaceArea(s, i));

    /// <summary>Signed volume by the divergence theorem; negative means inward normals.</summary>
    public static double SignedVolume(Solid s)
    {
        double v = 0;
        foreach (var (ia, ib, ic) in Triangles(s))
            v += Vec3.Dot(s.Vertices[ia], Vec3.Cross(s.Vertices[ib], s.Vertices[ic]));
        return v / 6;
    }

    public static double Volume(Solid s) => Math.Abs(SignedVolume(s));

    public static (Vec3 Min, Vec3 Max)? Bounds(Solid s)
    {
        if (s.Vertices.Length == 0) return null;
        var min = new Vec3(s.Vertices.Min(p => p.X), s.Vertices.Min(p => p.Y), s.Vertices.Min(p => p.Z));
        var max = new Vec3(s.Vertices.Max(p => p.X), s.Vertices.Max(p => p.Y), s.Vertices.Max(p => p.Z));
        return (min, max);
    }

    /// <summary>Largest distance from a loop vertex to the face's own plane.</summary>
    public static double FacePlanarity(Solid s, int faceIndex)
    {
        var f = s.Faces[faceIndex];
        var origin = s.Vertices[f.Outer[0]];
        double worst = 0;
        foreach (var loop in new[] { f.Outer }.Concat(f.Holes))
            foreach (var id in loop)
                worst = Math.Max(worst, Math.Abs(Vec3.Dot(s.Vertices[id] - origin, f.Normal)));
        return worst;
    }
}
