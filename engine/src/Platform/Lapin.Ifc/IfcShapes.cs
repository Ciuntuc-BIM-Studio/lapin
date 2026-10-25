using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Ifc;

/// <summary>
/// The body of a product read as prisms, before any triangulation, for the
/// recognition of foreign elements (docs/spec-ifc.md, Recunoașterea): an
/// extrusion is one prism; a faceted B-rep or a polygonal face set whose
/// faces split into two parallel congruent caps and sides perpendicular to
/// them is a prism too, one per such pair of caps (a box gives three, the
/// caller picks by the class). A boolean cut keeps its first operand, marked
/// clipped. Anything else (sweeps, revolutions, triangle meshes without
/// structure) gives nothing, with the reason.
/// </summary>
public sealed partial class IfcModel
{
    /// <summary>The product's 'Axis' representation (a wall's reference line) as world points, when it has one.</summary>
    public IReadOnlyList<Vec3>? AxisCurve(int product)
    {
        var e = File.Entities.GetValueOrDefault(product);
        if (e == null) return null;
        var place = Placement(e[5]);
        foreach (var rep in Items(File.Get(e[6])?[2]))
        {
            if (rep[1] is not string id || !string.Equals(id, "Axis", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var item in Items(rep[3]))
            {
                var pts = Curve(item);
                if (pts.Count >= 2) return [.. pts.Select(place.Apply)];
            }
        }
        return null;
    }

    /// <summary>The body's extents in the frame of a line in plan (from <paramref name="origin"/> along <paramref name="dir"/>, a unit vector), from its triangles; null without a body.</summary>
    public AxisExtents? Extents(int product, Vec2 origin, Vec2 dir)
    {
        var (body, _) = Shape(product);
        var n = new Vec2(-dir.Y, dir.X);
        double a0 = double.MaxValue, a1 = double.MinValue, c0 = double.MaxValue, c1 = double.MinValue, z0 = double.MaxValue, z1 = double.MinValue;
        var any = false;
        foreach (var (mesh, _) in body)
            foreach (var v in mesh.V)
            {
                any = true;
                var q = new Vec2(v.X, v.Y) - origin;
                var a = Vec2.Dot(q, dir); var c = Vec2.Dot(q, n);
                a0 = Math.Min(a0, a); a1 = Math.Max(a1, a); c0 = Math.Min(c0, c); c1 = Math.Max(c1, c); z0 = Math.Min(z0, v.Z); z1 = Math.Max(z1, v.Z);
            }
        return any ? new AxisExtents(a0, a1, c0, c1, z0, z1) : null;
    }

    /// <summary>The body's bounding box in world coordinates (from its triangles, booleans left aside), or null without a body.</summary>
    public (Vec3 Min, Vec3 Max)? Bounds(int product)
    {
        var (body, _) = Shape(product);
        Vec3 min = new(double.MaxValue, double.MaxValue, double.MaxValue), max = new(double.MinValue, double.MinValue, double.MinValue);
        var any = false;
        foreach (var (mesh, _) in body)
            foreach (var v in mesh.V)
            {
                any = true;
                min = new Vec3(Math.Min(min.X, v.X), Math.Min(min.Y, v.Y), Math.Min(min.Z, v.Z));
                max = new Vec3(Math.Max(max.X, v.X), Math.Max(max.Y, v.Y), Math.Max(max.Z, v.Z));
            }
        return any ? (min, max) : null;
    }

    /// <summary>The product's body as prisms; empty with <paramref name="why"/> set when it is not made of prisms.</summary>
    public IReadOnlyList<ShapePrism> Prisms(int product, out string? why)
    {
        why = null;
        var e = File.Entities.GetValueOrDefault(product);
        if (e == null) return [];
        var place = Placement(e[5]);
        var o = new List<ShapePrism>();
        var reasons = new List<string>();
        var items = 0;
        foreach (var rep in Items(File.Get(e[6])?[2]))
        {
            if (rep[1] is string id && NotBody.Contains(id.ToUpperInvariant())) continue;
            foreach (var item in Items(rep[3])) { items++; PrismItem(item, place, false, o, reasons); }
        }
        if (items == 0) why = "no body";
        else if (o.Count == 0) why = reasons.Count > 0 ? reasons[0] : "not a prism";
        return o;
    }

    void PrismItem(StepEntity item, Xf at, bool clipped, List<ShapePrism> o, List<string> reasons)
    {
        switch (item.Type)
        {
            case "IFCMAPPEDITEM":
            {
                var map = File.Get(item[0]);
                var xf = at.Then(Operator(File.Get(item[1]))).Then(Axis2(map?[0]));
                foreach (var sub in Items(File.Get(map?[1])?[3])) PrismItem(sub, xf, clipped, o, reasons);
                return;
            }
            case "IFCBOOLEANCLIPPINGRESULT":
            case "IFCBOOLEANRESULT":
            {
                var op = (item[0] as StepEnum)?.Value;
                if (File.Get(item[1]) is { } a) PrismItem(a, at, clipped || op != "UNION", o, reasons);
                if (op == "UNION" && File.Get(item[2]) is { } b) PrismItem(b, at, clipped, o, reasons);
                return;
            }
            case "IFCEXTRUDEDAREASOLID":
            {
                var areas = Profile(File.Get(item[0]));
                var dir = Direction(item[2]) ?? new Vec3(0, 0, 1);
                var depth = Num(item[3]) * _len;
                if (areas.Count == 0 || !(depth > 0)) { reasons.Add("an extrusion without a readable profile"); return; }
                var xf = at.Then(Axis2(item[1]));
                var sweep = xf.Dir(dir * depth);
                foreach (var (outer, holes) in areas)
                    o.Add(new ShapePrism([.. outer.Select(xf.Apply)], [.. holes.Select(h => (IReadOnlyList<Vec3>)[.. h.Select(xf.Apply)])], sweep, clipped, "extrusion"));
                return;
            }
            case "IFCFACETEDBREP":
            case "IFCFACETEDBREPWITHVOIDS":
            {
                var faces = BrepFaces(Items(File.Get(item[0])?[0]), at);
                var found = PrismsOfFaces(faces, clipped);
                if (found.Count == 0) reasons.Add("a B-rep that is not a prism"); else o.AddRange(found);
                return;
            }
            case "IFCPOLYGONALFACESET":
            {
                var faces = FaceSetFaces(item, at);
                var found = PrismsOfFaces(faces, clipped);
                if (found.Count == 0) reasons.Add("a face set that is not a prism"); else o.AddRange(found);
                return;
            }
            default:
                reasons.Add($"a {ClassName(item.Type)[3..].ToLowerInvariant()} body");
                return;
        }
    }

    /// <summary>A face: its outer loop and its inner loops (holes), in world coordinates.</summary>
    readonly record struct PolyFace(List<Vec3> Outer, List<List<Vec3>> Holes);

    List<PolyFace> BrepFaces(IEnumerable<StepEntity> faces, Xf at)
    {
        var o = new List<PolyFace>();
        List<Vec3> Loop(StepEntity bound)
        {
            var loop = File.Get(bound[0]);
            var pts = loop?.Type == "IFCPOLYLOOP" ? Refs(loop[0]).Select(p => at.Apply(Point(new StepRef(p)))).ToList() : [];
            if ((bound[1] as StepEnum)?.Value == "F") pts.Reverse();
            return pts;
        }
        foreach (var f in faces)
        {
            var bounds = Items(f[0]);
            var outer = bounds.FirstOrDefault(b => b.Type == "IFCFACEOUTERBOUND") ?? bounds.FirstOrDefault();
            if (outer == null) continue;
            var ring = Loop(outer);
            if (ring.Count < 3) continue;
            o.Add(new PolyFace(ring, [.. bounds.Where(b => b != outer).Select(Loop).Where(h => h.Count >= 3)]));
        }
        return o;
    }

    List<PolyFace> FaceSetFaces(StepEntity s, Xf at)
    {
        var pts = PointList(s[0]).Select(at.Apply).ToList();
        var pn = Indices(s[3]);
        List<Vec3> At(int[] ix) => [.. ix.Select(i => (pn.Length > 0 ? pn[i - 1] : i) - 1).Where(i => i >= 0 && i < pts.Count).Select(i => pts[i])];
        var o = new List<PolyFace>();
        foreach (var f in Items(s[2]))
        {
            var outer = At(Indices(f[0]));
            if (outer.Count < 3) continue;
            var holes = f.Type == "IFCINDEXEDPOLYGONALFACEWITHVOIDS" && f[1] is List<object?> inner ? inner.Select(i => At(Indices(i))).Where(h => h.Count >= 3).ToList() : [];
            o.Add(new PolyFace(outer, holes));
        }
        return o;
    }

    static Vec3 Newell(List<Vec3> loop)
    {
        double x = 0, y = 0, z = 0;
        for (var i = 0; i < loop.Count; i++)
        {
            var a = loop[i]; var b = loop[(i + 1) % loop.Count];
            x += (a.Y - b.Y) * (a.Z + b.Z); y += (a.Z - b.Z) * (a.X + b.X); z += (a.X - b.X) * (a.Y + b.Y);
        }
        var n = new Vec3(x, y, z);
        return n.Length < 1e-14 ? Vec3.Zero : n.Normalized();
    }

    /// <summary>
    /// The prisms a closed set of planar faces describes: for every pair of
    /// faces with opposite normals whose vertices match under one translation,
    /// with every other face perpendicular to them and every vertex on one of
    /// the two planes. The base is the lower cap when the sweep is vertical.
    /// </summary>
    /// <summary>A loop without repeated or collinear vertices (other programs leave them where neighbours touch).</summary>
    static List<Vec3> Clean(List<Vec3> loop, double tol)
    {
        var o = new List<Vec3>();
        foreach (var p in loop) if (o.Count == 0 || (o[^1] - p).Length > tol) o.Add(p);
        if (o.Count > 1 && (o[0] - o[^1]).Length <= tol) o.RemoveAt(o.Count - 1);
        for (var pass = 0; pass < 3 && o.Count > 3; pass++)
        {
            var changed = false;
            for (var i = 0; i < o.Count && o.Count > 3; i++)
            {
                Vec3 a = o[(i + o.Count - 1) % o.Count], b = o[i], c = o[(i + 1) % o.Count];
                // b within tol of the line a–c: collinear (the distance, not the angle, so a short chamfer stays).
                var ac = c - a;
                if (ac.Length > tol && Vec3.Cross(ac, b - a).Length / ac.Length <= tol) { o.RemoveAt(i); i--; changed = true; }
            }
            if (!changed) break;
        }
        return o;
    }

    static List<ShapePrism> PrismsOfFaces(List<PolyFace> faces, bool clipped)
    {
        var o = new List<ShapePrism>();
        if (faces.Count < 5) return o;
        faces = [.. faces.Select(f => new PolyFace(Clean(f.Outer, 2e-4), [.. f.Holes.Select(h => Clean(h, 2e-4)).Where(h => h.Count >= 3)])).Where(f => f.Outer.Count >= 3)];
        if (faces.Count < 5) return o;
        var normals = faces.Select(f => Newell(f.Outer)).ToList();
        var size = 0.0;
        var first = faces[0].Outer[0];
        foreach (var f in faces) foreach (var p in f.Outer) size = Math.Max(size, (p - first).Length);
        var tol = Math.Max(5e-4, size * 1e-6);
        bool Same(Vec3 a, Vec3 b) => (a - b).Length <= tol * 5;
        for (var i = 0; i < faces.Count; i++)
        {
            var n = normals[i];
            if (n.Length == 0) continue;
            for (var j = i + 1; j < faces.Count; j++)
            {
                if (normals[j].Length == 0 || Vec3.Dot(n, normals[j]) > -0.9999) continue;
                var h = Vec3.Dot(n, faces[j].Outer[0] - faces[i].Outer[0]);
                if (Math.Abs(h) <= tol) continue;
                var sweep = n * h;
                // Every vertex of the shell on one of the two planes; every other face perpendicular, or part of the opposite cap (coplanar with j).
                var ok = true; var jSplit = false;
                for (var k = 0; k < faces.Count && ok; k++)
                {
                    if (k == i || k == j) continue;
                    var dot = Vec3.Dot(normals[k], n);
                    if (dot < -0.9999 && Math.Abs(Vec3.Dot(n, faces[k].Outer[0] - faces[j].Outer[0])) <= tol) { jSplit = true; }
                    else if (dot > 0.9999) { ok = false; break; } // another face in the base's plane: the base is not one face
                    else if (Math.Abs(dot) > 1e-3) { ok = false; break; }
                    foreach (var p in faces[k].Outer)
                    {
                        var d = Vec3.Dot(n, p - faces[i].Outer[0]);
                        if (Math.Abs(d) > tol && Math.Abs(d - h) > tol) { ok = false; break; }
                    }
                }
                if (!ok) continue;
                // The caps congruent: every vertex of i, moved by the sweep, is a vertex of the opposite cap (one face, or several in its plane).
                if (!jSplit && (faces[i].Outer.Count != faces[j].Outer.Count || faces[i].Holes.Count != faces[j].Holes.Count)) continue;
                var opposite = jSplit ? faces.Where((f, k) => k != i && Vec3.Dot(normals[k], n) < -0.9999).SelectMany(f => f.Outer).ToList() : faces[j].Outer;
                if (!faces[i].Outer.All(p => opposite.Any(q => Same(p + sweep, q)))) continue;
                var (baseFace, vec) = sweep.Z < -1e-9 && !jSplit ? (faces[j], -sweep) : (faces[i], sweep);
                o.Add(new ShapePrism([.. baseFace.Outer], [.. baseFace.Holes.Select(hole => (IReadOnlyList<Vec3>)[.. hole])], vec, clipped, "brep"));
            }
        }
        return o;
    }
}
