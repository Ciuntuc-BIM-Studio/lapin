using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A prism found in a foreign IFC element's body: a planar base polygon with
/// its holes (world coordinates, metres) swept by a vector. An extrusion gives
/// one directly; a B-rep box gives up to three (one per pair of opposite faces).
/// <see cref="Clipped"/>: the body was the first operand of a boolean cut
/// (a wall under a roof), so the prism is the uncut shape.
/// </summary>
public sealed record ShapePrism(IReadOnlyList<Vec3> Outer, IReadOnlyList<IReadOnlyList<Vec3>> Holes, Vec3 Sweep, bool Clipped, string Source)
{
    public double Height => Sweep.Length;
    public bool Vertical => Math.Abs(Sweep.X) < 1e-6 * Math.Max(1, Height) && Math.Abs(Sweep.Y) < 1e-6 * Math.Max(1, Height);
    public bool Horizontal => Math.Abs(Sweep.Z) < 1e-6 * Math.Max(1, Height);
    public double BaseZ => Outer.Count == 0 ? 0 : Outer.Min(p => p.Z);
    public double TopZ => Math.Max(BaseZ, BaseZ + Sweep.Z);
}

/// <summary>What fills a foreign opening: a door or a window, with what the file says of it.</summary>
public sealed record ForeignFill(string GlobalId, string IfcClass, string? Name, double? Width, double? Height, string? Operation, string? TypeName, string? TypeGlobalId, JsonObject Properties);

/// <summary>An opening that voids a foreign element: its body as prisms, and its fill when it has one.</summary>
public sealed record ForeignOpening(string GlobalId, string? Name, IReadOnlyList<ShapePrism> Prisms, ForeignFill? Fill);

/// <summary>
/// A foreign IFC element offered to the modules, with everything the file
/// says about it that a module may need to rebuild it natively: its class,
/// its body as prisms, its openings and their fills, its type's name, its
/// material or layers, its storey and its property sets (as text).
/// </summary>
public sealed record ForeignElement(
    string GlobalId, string IfcClass, string? PredefinedType, string? Name, string? ObjectType, string StoreyId,
    IReadOnlyList<ShapePrism> Prisms, string? ShapeNote,
    IReadOnlyList<ForeignOpening> Openings,
    string? TypeName, string? TypeGlobalId,
    IReadOnlyList<(string Material, double Thickness, bool Core)>? Layers, string? Material,
    JsonObject Properties,
    /// <summary>The 'Axis' representation (a wall's reference line), world points, when the file has one.</summary>
    IReadOnlyList<Vec3>? Axis = null,
    /// <summary>The body's bounding box, from its triangles, when it has a body at all.</summary>
    (Vec3 Min, Vec3 Max)? Box = null,
    /// <summary>The file's layer set usage: whether it runs the negative way and its offset from the reference line.</summary>
    (bool Negative, double Offset)? Usage = null,
    /// <summary>Parts aggregated under the element (a wall's layers as IfcBuildingElementParts), each with its body and material.</summary>
    IReadOnlyList<ForeignPart>? Parts = null,
    /// <summary>The body's extents in the frame of the 'Axis' (along it from its start, across it to the left, up), from its triangles, when it has both.</summary>
    AxisExtents? Extents = null);

/// <summary>A part aggregated under a foreign element: its body as prisms, its material, its extents in the whole's axis frame.</summary>
public sealed record ForeignPart(string GlobalId, string? Name, IReadOnlyList<ShapePrism> Prisms, string? Material, AxisExtents? Extents = null);

/// <summary>A body's extents in a wall's axis frame: along the axis from its start, across it (left positive), and up.</summary>
public readonly record struct AxisExtents(double Along0, double Along1, double Across0, double Across1, double Z0, double Z1);

/// <summary>
/// What one import's recognition shares between the recognisers: the services,
/// a cache for the types a recogniser makes (keyed as it likes), the ids of
/// what was made (for joins at the end), and the reasons elements were left
/// as references.
/// </summary>
public sealed class RecognitionContext(IServices services)
{
    public IServices Services { get; } = services;
    public Dictionary<string, string> TypeIds { get; } = [];
    public List<string> Made { get; } = [];
    public List<string> Notes { get; } = [];
    /// <summary>Why an element stays a reference, counted by reason.</summary>
    public Dictionary<string, int> Skipped { get; } = [];

    public void Skip(string reason) => Skipped[reason] = Skipped.GetValueOrDefault(reason) + 1;
}

/// <summary>
/// Offered by a module that can rebuild foreign IFC elements as its own
/// (docs/spec-ifc.md, Recunoașterea). The importer asks the recognisers in
/// turn; the first that returns an id has made the element.
/// </summary>
public interface IIfcRecognizer
{
    /// <summary>The native element made for <paramref name="element"/> (its id, the file's GlobalId when it can be kept), or null with a reason in <see cref="RecognitionContext.Skip"/>.</summary>
    string? Recognize(IEdit edit, ForeignElement element, RecognitionContext ctx);

    /// <summary>After every element: what needs them all (joins between walls).</summary>
    void Finish(IEdit edit, RecognitionContext ctx) { }
}

/// <summary>What a prism's base polygon says, for the recognisers: a wall's axis and thickness, a column's rectangle or circle.</summary>
public static class Footprints
{
    /// <summary>The base polygon in plan (z dropped), without repeated or collinear points.</summary>
    public static List<Vec2> Plan(IReadOnlyList<Vec3> loop)
    {
        var pts = loop.Select(p => new Vec2(p.X, p.Y)).ToList();
        return Clean(pts);
    }

    /// <summary>A polygon in the plane of a vertical face, as (along, up) coordinates: <paramref name="along"/> a unit vector in plan.</summary>
    public static List<Vec2> Elevation(IReadOnlyList<Vec3> loop, Vec2 origin, Vec2 along) =>
        Clean(loop.Select(p => new Vec2(Vec2.Dot(new Vec2(p.X, p.Y) - origin, along), p.Z)).ToList());

    public static List<Vec2> Clean(List<Vec2> pts, double tol = 1e-6)
    {
        var o = new List<Vec2>();
        foreach (var p in pts) if (o.Count == 0 || (o[^1] - p).Length > tol) o.Add(p);
        if (o.Count > 1 && (o[0] - o[^1]).Length <= tol) o.RemoveAt(o.Count - 1);
        for (var guard = 0; guard < 3; guard++)
        {
            var changed = false;
            for (var i = 0; i < o.Count && o.Count > 3; i++)
            {
                Vec2 a = o[(i + o.Count - 1) % o.Count], b = o[i], c = o[(i + 1) % o.Count];
                var ac = c - a; var ab = b - a;
                if (ac.Length > tol && Math.Abs(ac.X * ab.Y - ac.Y * ab.X) / ac.Length <= Math.Max(tol, 2e-4)) { o.RemoveAt(i); i--; changed = true; }
            }
            if (!changed) break;
        }
        return o;
    }

    public static double Area(IReadOnlyList<Vec2> poly)
    {
        double a = 0;
        for (var i = 0; i < poly.Count; i++) { var p = poly[i]; var q = poly[(i + 1) % poly.Count]; a += p.X * q.Y - q.X * p.Y; }
        return a / 2;
    }

    /// <summary>
    /// A thin polygon's axis: along its longest edge; the thickness across it; the
    /// ends where the polygon ends along it; <c>Coverage</c>, how much of the two
    /// long sides is made of edges parallel to the axis (1 for a rectangle, less
    /// for mitred ends). Null for a degenerate polygon.
    /// </summary>
    public static (Vec2 Start, Vec2 End, double Thickness, double Coverage)? Axis(IReadOnlyList<Vec2> poly)
    {
        if (poly.Count < 3) return null;
        Vec2 dir = default; double best = 0;
        for (var i = 0; i < poly.Count; i++)
        {
            var e = poly[(i + 1) % poly.Count] - poly[i];
            if (e.Length > best) { best = e.Length; dir = e; }
        }
        if (best <= 0) return null;
        dir = dir.Normalized();
        var nrm = new Vec2(-dir.Y, dir.X);
        double aMin = double.MaxValue, aMax = double.MinValue, nMin = double.MaxValue, nMax = double.MinValue;
        foreach (var p in poly)
        {
            var a = Vec2.Dot(p, dir); var n = Vec2.Dot(p, nrm);
            aMin = Math.Min(aMin, a); aMax = Math.Max(aMax, a); nMin = Math.Min(nMin, n); nMax = Math.Max(nMax, n);
        }
        var length = aMax - aMin; var thickness = nMax - nMin;
        if (!(length > 0) || !(thickness > 0)) return null;
        double parallel = 0;
        for (var i = 0; i < poly.Count; i++)
        {
            var e = poly[(i + 1) % poly.Count] - poly[i];
            if (e.Length > 0 && Math.Abs(Vec2.Dot(e.Normalized(), nrm)) < 0.02) parallel += e.Length;
        }
        var mid = (nMin + nMax) / 2;
        return (dir * aMin + nrm * mid, dir * aMax + nrm * mid, thickness, Math.Min(1, parallel / (2 * length)));
    }

    /// <summary>A rectangle: its centre, its sides (the first along the first edge) and that edge's angle (degrees). Null unless four corners at right angles.</summary>
    public static (Vec2 Centre, double Width, double Depth, double RotationDeg)? Rectangle(IReadOnlyList<Vec2> poly)
    {
        if (poly.Count != 4) return null;
        for (var i = 0; i < 4; i++)
        {
            var a = poly[(i + 1) % 4] - poly[i]; var b = poly[(i + 2) % 4] - poly[(i + 1) % 4];
            if (a.Length <= 0 || b.Length <= 0 || Math.Abs(Vec2.Dot(a.Normalized(), b.Normalized())) > 0.01) return null;
        }
        var e0 = poly[1] - poly[0]; var e1 = poly[2] - poly[1];
        var centre = new Vec2((poly[0].X + poly[1].X + poly[2].X + poly[3].X) / 4, (poly[0].Y + poly[1].Y + poly[2].Y + poly[3].Y) / 4);
        return (centre, e0.Length, e1.Length, Math.Atan2(e0.Y, e0.X) * 180 / Math.PI);
    }

    /// <summary>A circle drawn as a polygon: its centre and diameter. Null unless eight or more vertices at one radius (within 2 %).</summary>
    public static (Vec2 Centre, double Diameter)? Circle(IReadOnlyList<Vec2> poly)
    {
        if (poly.Count < 8) return null;
        var c = new Vec2(poly.Average(p => p.X), poly.Average(p => p.Y));
        var radii = poly.Select(p => (p - c).Length).ToList();
        var r = radii.Average();
        return r > 0 && radii.All(x => Math.Abs(x - r) <= 0.02 * r) ? (c, 2 * r) : null;
    }
}
