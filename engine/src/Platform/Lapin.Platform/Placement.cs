using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A plan transformation of elements: where a point goes, how much directions
/// turn (radians), and whether it mirrors (directions then reflect about the
/// mirror line's angle <see cref="Axis"/>).
/// </summary>
public sealed record Placement2D(Func<Vec2, Vec2> Point, double Turn, bool Mirrored, double Axis = 0)
{
    public Vec2 Dir(Vec2 d)
    {
        if (Mirrored)
        {
            var a = 2 * Axis - Math.Atan2(d.Y, d.X);
            return new Vec2(Math.Cos(a), Math.Sin(a)) * d.Length;
        }
        var (c, s) = (Math.Cos(Turn), Math.Sin(Turn));
        return new Vec2(d.X * c - d.Y * s, d.X * s + d.Y * c);
    }

    /// <summary>An angle (radians) the way the transformation carries it.</summary>
    public double Angle(double a) => Mirrored ? 2 * Axis - a : a + Turn;

    public static Placement2D Move(Vec2 d) => new(p => p + d, 0, false);
    public static Placement2D Rotate(Vec2 c, double angle) => new(p =>
    {
        var (co, s) = (Math.Cos(angle), Math.Sin(angle));
        var v = p - c;
        return new Vec2(c.X + v.X * co - v.Y * s, c.Y + v.X * s + v.Y * co);
    }, angle, false);
    public static Placement2D Mirror(Vec2 a, Vec2 b)
    {
        var d = b - a;
        var l2 = Vec2.Dot(d, d);
        return new(p => { var f = a + d * (Vec2.Dot(p - a, d) / l2); return f * 2 - p; }, 0, true, Math.Atan2(d.Y, d.X));
    }
}

/// <summary>How a module moves, turns, mirrors or copies its own elements, when the attributes alone do not say it (a wall's joined neighbours, a solid's sketch).</summary>
public interface IElementPlacement
{
    bool Handles(Entity e);
    /// <summary>The element carried by <paramref name="t"/>; <paramref name="moving"/> is everything carried with it.</summary>
    void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving);
    /// <summary>A copy carried by <paramref name="t"/>: its new id (null: the platform's own copy will do).</summary>
    string? Copy(ICommandContext ctx, Entity e, Placement2D t) => null;
}

/// <summary>
/// The platform's own placement: the plan attributes elements share
/// (<c>start</c>, <c>end</c>, <c>at</c> with <c>rotation</c>, <c>contour</c>,
/// survey <c>points</c>, a <c>ridgeDirection</c>), carried by the
/// transformation; what voids an element (a floor's opening, a terrain's
/// platform) goes with it. A copy takes the element's type, storey, material
/// usage and the openings and doors or windows it hosts.
/// </summary>
public static class Placement
{
    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
    static JsonArray J(Vec2 p) => [p.X, p.Y];

    /// <summary>The element's attributes carried by the transformation.</summary>
    public static JsonObject Carry(Entity e, Placement2D t)
    {
        var a = e.CloneAttrs();
        foreach (var k in new[] { "start", "end", "at", "a", "b" })
            if (a[k] is JsonArray { Count: >= 2 } p && e.Class != IfcClass.Annotation) a[k] = J(t.Point(P(p)));
        if (a["rotation"] != null && e.Class != IfcClass.Annotation) a["rotation"] = t.Angle(Json.Number(a["rotation"]) ?? 0);
        if (a["contour"] is JsonArray c)
        {
            var pts = c.OfType<JsonArray>().Select(p => t.Point(P(p))).ToList();
            if (t.Mirrored) pts.Reverse(); // a mirrored outline keeps running counter-clockwise
            a["contour"] = new JsonArray(pts.Select(p => (JsonNode)J(p)).ToArray());
        }
        if (a["points"] is JsonArray pts3 && pts3.OfType<JsonArray>().All(p => p.Count >= 3))
            a["points"] = new JsonArray(pts3.OfType<JsonArray>().Select(p =>
            {
                var q = t.Point(new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0));
                return (JsonNode)new JsonArray(q.X, q.Y, Json.Number(p[2]) ?? 0);
            }).ToArray());
        if (a["ridgeDirection"] is JsonArray { Count: >= 2 } rd) a["ridgeDirection"] = J(t.Dir(P(rd)));
        return a;
    }

    /// <summary>What goes with an element: the openings and platforms that void it and have their own outline.</summary>
    static IEnumerable<Entity> Riders(IModelView m, Entity e) =>
        m.RelatedOf(e.Id, IfcClass.RelVoidsElement).Where(o => o.Attrs["contour"] != null);

    public static void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        if (ctx.Services.All<IElementPlacement>().FirstOrDefault(p => p.Handles(e)) is { } own) { own.Transform(ctx, e, t, moving); return; }
        Own(ctx, e, t, moving);
    }

    /// <summary>
    /// The platform's own move of an element and what voids it. A path
    /// element's joined neighbours that stay keep their join: their ends on its
    /// axis go with it (a corner, a T).
    /// </summary>
    public static void Own(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string>? moving = null)
    {
        var m = ctx.Model;
        if (e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray && m.RelationsOf(e.Id, IfcClass.RelConnectsPathElements).Any())
        {
            var (a, b) = (P(e.Attrs["start"]), P(e.Attrs["end"]));
            foreach (var r in m.RelationsOf(e.Id, IfcClass.RelConnectsPathElements).ToList())
                foreach (var pid in r.Related.Append(r.Relating).Where(x => x != e.Id && moving?.Contains(x) != true).Distinct())
                {
                    if (m.Get(pid) is not { } partner || partner.Attrs["start"] is not JsonArray || partner.Attrs["end"] is not JsonArray) continue;
                    var attrs = partner.CloneAttrs();
                    var changed = false;
                    foreach (var k in new[] { "start", "end" })
                    {
                        var q = P(attrs[k]);
                        if (DistToSegment(q, a, b) < 1e-6) { attrs[k] = J(t.Point(q)); changed = true; }
                    }
                    if (changed) ctx.Replace(partner.With(attrs: attrs));
                }
        }
        ctx.Replace(e.With(attrs: Carry(e, t)));
        foreach (var r in Riders(m, e).ToList()) ctx.Replace(r.With(attrs: Carry(r, t)));
    }

    static double DistToSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        var d = b - a;
        var k = Math.Clamp(Vec2.Dot(p - a, d) / Math.Max(Vec2.Dot(d, d), 1e-18), 0, 1);
        return (p - (a + d * k)).Length;
    }

    public static string Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        if (ctx.Services.All<IElementPlacement>().FirstOrDefault(p => p.Handles(e)) is { } own && own.Copy(ctx, e, t) is { } id) return id;
        return DeepCopy(ctx, e, t);
    }

    /// <summary>A copy with its type, storey, material (its own usage), and the openings and fills it hosts.</summary>
    public static string DeepCopy(ICommandContext ctx, Entity e, Placement2D t)
    {
        var m = ctx.Model;
        var copy = new Entity(ctx.NewId(), e.Class, e.Name, Carry(e, t), e.PredefinedType);
        ctx.Add(copy);
        foreach (var r in m.RelationsOf(e.Id).ToList())
        {
            switch (r.Class)
            {
                case IfcClass.RelContainedInSpatialStructure or IfcClass.RelDefinesByType when r.Related.Contains(e.Id):
                    ctx.Relate(r.Class, r.Relating, copy.Id);
                    break;
                case IfcClass.RelAssociatesMaterial when r.Related.Contains(e.Id):
                {
                    var relating = m.Get(r.Relating);
                    if (relating?.Class == IfcClass.MaterialLayerSetUsage)
                    {
                        // A usage belongs to one element: the copy gets its own.
                        var usage = new Entity(ctx.NewId(), relating.Class, relating.Name, relating.CloneAttrs(), relating.PredefinedType);
                        ctx.Add(usage);
                        foreach (var ur in m.RelationsOf(relating.Id).Where(x => x.Related.Contains(relating.Id)).ToList()) ctx.Relate(ur.Class, ur.Relating, usage.Id);
                        ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelAssociatesMaterial, usage.Id, [copy.Id]));
                    }
                    else ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelAssociatesMaterial, r.Relating, [copy.Id]));
                    break;
                }
                case IfcClass.RelVoidsElement when r.Relating == e.Id:
                    foreach (var oid in r.Related)
                    {
                        if (m.Get(oid) is not { } o) continue;
                        var oc = new Entity(ctx.NewId(), o.Class, o.Name, Carry(o, t), o.PredefinedType);
                        ctx.Add(oc);
                        ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, copy.Id, [oc.Id]));
                        foreach (var fr in m.RelationsOf(o.Id, IfcClass.RelFillsElement).Where(x => x.Relating == o.Id).ToList())
                            foreach (var fid in fr.Related)
                                if (m.Get(fid) is { } fill)
                                {
                                    var fc = new Entity(ctx.NewId(), fill.Class, fill.Name, fill.CloneAttrs(), fill.PredefinedType);
                                    ctx.Add(fc);
                                    ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelFillsElement, oc.Id, [fc.Id]));
                                    foreach (var tr in m.RelationsOf(fill.Id).Where(x => x.Related.Contains(fill.Id) && x.Class is IfcClass.RelDefinesByType or IfcClass.RelContainedInSpatialStructure).ToList())
                                        ctx.Relate(tr.Class, tr.Relating, fc.Id);
                                }
                    }
                    break;
            }
        }
        return copy.Id;
    }
}
