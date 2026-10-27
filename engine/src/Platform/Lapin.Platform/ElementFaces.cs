using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// One named face of an element, or one named edge (two points): a floor's
/// soffit, a beam's bottom or its left side, the bottom edge a chamfer runs
/// along. <see cref="Role"/> is stable (<c>Soffit</c>, <c>Edge[2]</c>,
/// <c>Side.Left</c>): it is worked out from the element's attributes, never
/// from its B-rep, so the face is the same face after the element moves, its
/// section changes or a boolean cuts it. The outline is in model coordinates
/// (plan x, y; height z), counter-clockwise seen from outside (along
/// <see cref="Normal"/>, towards the face); holes, if any, as rings of their own.
/// </summary>
public sealed record ElementFace(string Role, string Label, IReadOnlyList<Vec3> Outline, Vec3 Normal, IReadOnlyList<IReadOnlyList<Vec3>>? Holes = null, bool Curve = false)
{
    /// <summary>An edge, or a curve along a curved face (a curved beam's side, its axis): a line of points, not an area. Its normal is the outward one at its middle.</summary>
    public bool IsEdge => Curve || Outline.Count == 2;

    public Vec3 Centre => Outline.Count == 0 ? Vec3.Zero : new(Outline.Average(p => p.X), Outline.Average(p => p.Y), Outline.Average(p => p.Z));

    /// <summary>The face's area (its holes taken out); an edge's length.</summary>
    public double Size
    {
        get
        {
            if (IsEdge) return Outline.Zip(Outline.Skip(1), (a, b) => (b - a).Length).Sum();
            double Area(IReadOnlyList<Vec3> ring) => Math.Abs(Vec3.Dot(VecMath.NewellNormal(ring), Normal.Normalized()));
            return Area(Outline) - (Holes?.Sum(Area) ?? 0);
        }
    }

    public JsonObject ToJson() => new()
    {
        ["role"] = Role, ["label"] = Label, ["edge"] = IsEdge, ["curve"] = Curve, ["size"] = Math.Round(Size, 6),
        ["normal"] = new JsonArray(Normal.X, Normal.Y, Normal.Z),
        ["outline"] = new JsonArray(Outline.Select(p => (JsonNode)new JsonArray(p.X, p.Y, p.Z)).ToArray()),
    };
}

/// <summary>
/// The named faces of a module's classes (a floor's soffit and edges, a beam's
/// bottom, sides and bottom edges), offered as a service: what formwork and
/// scaffolding are placed on. Each module declares the faces of the classes it
/// builds, from their attributes.
/// </summary>
public interface IElementFaces
{
    bool Covers(string ifcClass);
    IReadOnlyList<ElementFace> FacesOf(Entity entity, IModelView model);
}

public static class ElementFaces
{
    /// <summary>
    /// The queries: <c>core.faces {id}</c>, an element's named faces; and
    /// <c>core.faceAt {id, point: [x, y, z], normal?: [x, y, z]}</c>, the face
    /// a pick lands on (in 3D with the triangle's normal; in plan without it),
    /// or null.
    /// </summary>
    public static void Register(IModuleRegistry r)
    {
        r.Query("core.faces", (q, a) => q.Model.Get((string?)a["id"] ?? "") is { } e
            ? new JsonArray(Of(q.Services, e, q.Model).Select(f => (JsonNode)f.ToJson()).ToArray()) : null);
        r.Query("core.faceAt", (q, a) =>
        {
            if (q.Model.Get((string?)a["id"] ?? "") is not { } e || Vec(a["point"]) is not { } p) return null;
            return At(Of(q.Services, e, q.Model), p, Vec(a["normal"]))?.ToJson();
        });
    }

    static Vec3? Vec(JsonNode? n) =>
        n is JsonArray { Count: >= 2 } v && Json.Number(v[0]) is { } x && Json.Number(v[1]) is { } y ? new Vec3(x, y, v.Count > 2 ? Json.Number(v[2]) ?? 0 : 0) : null;

    /// <summary>An element's named faces, from whichever module declares its class; none when no module does.</summary>
    public static IReadOnlyList<ElementFace> Of(IServices services, Entity e, IModelView m) =>
        services.All<IElementFaces>().FirstOrDefault(f => f.Covers(e.Class))?.FacesOf(e, m) ?? [];

    public static ElementFace? Find(IServices services, Entity e, IModelView m, string role) =>
        Of(services, e, m).FirstOrDefault(f => f.Role == role);

    /// <summary>
    /// The face at a picked point. In 3D (a normal given): the face whose plane
    /// the point lies on, facing the same way, the nearest if several; else
    /// the nearest face facing that way. In plan (no normal): the nearest
    /// upright face to the point, measured in plan, when within
    /// <paramref name="reach"/>; else the face under the point looking down or up
    /// (a floor's outline picked inside it: its soffit).
    /// </summary>
    public static ElementFace? At(IReadOnlyList<ElementFace> faces, Vec3 point, Vec3? normal, double reach = 0.3)
    {
        var areas = faces.Where(f => !f.IsEdge).ToList();
        if (normal is { } n0)
        {
            var n = n0.Normalized();
            return areas.Where(f => Vec3.Dot(f.Normal.Normalized(), n) > 0.7)
                .OrderBy(f => Math.Abs(Vec3.Dot(point - f.Outline[0], f.Normal.Normalized())) + (Inside(f, point) ? 0 : 0.5 + PlanDistance(f, point.Xy)))
                .FirstOrDefault();
        }
        var upright = areas.Where(f => Math.Abs(f.Normal.Normalized().Z) < 0.3)
            .Select(f => (f, d: PlanDistance(f, point.Xy))).Where(x => x.d <= reach).OrderBy(x => x.d).Select(x => x.f).FirstOrDefault();
        if (upright != null) return upright;
        return areas.Where(f => Math.Abs(f.Normal.Normalized().Z) > 0.7 && Polygon2.Contains(f.Outline.Select(p => p.Xy).ToList(), point.Xy))
            .OrderBy(f => f.Normal.Z).FirstOrDefault();
    }

    /// <summary>Whether a point on a face's plane lies within its outline (in the plane's own axes).</summary>
    static bool Inside(ElementFace f, Vec3 p)
    {
        var (u, v) = VecMath.PlaneBasis(f.Normal.Normalized());
        var ring = f.Outline.Select(q => VecMath.Project(q, u, v)).ToList();
        var at = VecMath.Project(p, u, v);
        if (Polygon2.Contains(ring, at)) return true;
        // On its boundary, within a centimetre.
        for (var i = 0; i < ring.Count; i++)
            if (SegmentDistance(ring[i], ring[(i + 1) % ring.Count], at) < 0.01) return true;
        return false;
    }

    /// <summary>The distance in plan from a point to the face's outline seen from above.</summary>
    static double PlanDistance(ElementFace f, Vec2 p)
    {
        var ring = f.Outline.Select(q => q.Xy).ToList();
        var best = double.MaxValue;
        for (var i = 0; i < ring.Count; i++) best = Math.Min(best, SegmentDistance(ring[i], ring[(i + 1) % ring.Count], p));
        return best;
    }

    public static double SegmentDistance(Vec2 a, Vec2 b, Vec2 p)
    {
        var d = b - a;
        var l2 = Vec2.Dot(d, d);
        var t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p - a, d) / l2, 0, 1);
        return (a + d * t - p).Length;
    }

    /// <summary>A curve at a height (a curved face's bottom line, an axis, an edge), its outward normal at its middle.</summary>
    public static ElementFace Along(string role, string label, IReadOnlyList<Vec2> line, double z, Vec3 normal) =>
        new(role, label, line.Select(p => p.At(z)).ToList(), normal, null, true);

    /// <summary>An upright rectangle on a line in plan, from z0 to z1, facing <paramref name="outward"/> (counter-clockwise seen from there).</summary>
    public static ElementFace Upright(string role, string label, Vec2 a, Vec2 b, double z0, double z1, Vec2 outward)
    {
        // Seen from outside, counter-clockwise: along the line with the outside on the right goes up on the far end.
        var n = new Vec3(outward.X, outward.Y, 0).Normalized();
        var along = new Vec3(b.X - a.X, b.Y - a.Y, 0);
        var ccw = Vec3.Dot(Vec3.Cross(along, Vec3.UnitZ), n) > 0;
        var (p, q) = ccw ? (a, b) : (b, a);
        return new(role, label, [p.At(z0), q.At(z0), q.At(z1), p.At(z1)], n);
    }

    /// <summary>A level face over an outline in plan at z, facing down (a soffit) or up (a top).</summary>
    public static ElementFace Level(string role, string label, IReadOnlyList<Vec2> outline, double z, bool down, IReadOnlyList<IReadOnlyList<Vec2>>? holes = null)
    {
        var ccw = Polygon2.EnsureCcw(outline);
        IReadOnlyList<Vec3> Ring(IReadOnlyList<Vec2> r, bool reverse) => (reverse ? r.Reverse() : r).Select(p => p.At(z)).ToList();
        return new(role, label, Ring(ccw, down), down ? -Vec3.UnitZ : Vec3.UnitZ,
            holes?.Select(h => Ring(Polygon2.EnsureCcw(h), !down)).ToList());
    }
}
