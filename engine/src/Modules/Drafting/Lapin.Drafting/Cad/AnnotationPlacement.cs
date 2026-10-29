using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting.Cad;

/// <summary>Moving, turning and mirroring what drafting draws: shapes and hatches, texts, dimensions, grid axes.</summary>
public sealed class AnnotationPlacement : IElementPlacement
{
    public bool Handles(Entity e) => e.Class is IfcClass.Annotation or IfcClass.Grid;

    public static Shape Map(Shape s, Placement2D t) => s switch
    {
        Line l => new Line(t.Point(l.A), t.Point(l.B)),
        Polyline p => new Polyline(p.Pts.Select(t.Point).ToList(), p.Closed),
        Circle c => c with { C = t.Point(c.C) },
        Arc a => t.Mirrored
            ? new Arc(t.Point(a.C), a.R, Geo.NormAngle(t.Angle(a.End)), Geo.NormAngle(t.Angle(a.Start)))
            : new Arc(t.Point(a.C), a.R, Geo.NormAngle(t.Angle(a.Start)), Geo.NormAngle(t.Angle(a.End))),
        _ => s,
    };

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
    static JsonArray J(Vec2 p) => new(p.X, p.Y);

    public static JsonObject Carry(IModelView m, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        var attrs = e.CloneAttrs();
        if (e.Class == IfcClass.Grid)
        {
            if (attrs["axes"] is JsonArray axes)
                foreach (var ax in axes.OfType<JsonObject>()) { ax["a"] = J(t.Point(P(ax["a"]))); ax["b"] = J(t.Point(P(ax["b"]))); }
            return attrs;
        }
        switch (e.PredefinedType)
        {
            case Drafts.Predefined:
                if (Drafts.ShapeOf(e) is { } s) attrs["shape"] = Map(s, t).ToJson();
                if (attrs["hatch"] is JsonObject h)
                    h["loops"] = new JsonArray(Drafts.HatchLoops(e).Select(l => (JsonNode)new JsonArray(l.Select(q => (JsonNode)J(t.Point(q))).ToArray())).ToArray());
                break;
            case "TAG" or "LEGEND":
                attrs["at"] = J(t.Point(P(attrs["at"])));
                break;
            case "TEXT":
                attrs["at"] = J(t.Point(P(attrs["at"])));
                var deg = Json.Number(attrs["angle"]) ?? 0;
                attrs["angle"] = t.Angle(deg * Math.PI / 180) * 180 / Math.PI;
                break;
            case "DIMENSION" when Dimensions.IsLinear(e) && Held(e, t, moving) is { } shift:
                // Moved on its own while it measures elements that stay: it keeps measuring them, and only its line slides square to it (report #37).
                attrs["at"] = J(P(attrs["at"]) + shift);
                break;
            case "DIMENSION" when Dimensions.IsLinear(e):
            {
                // The whole string: every point, its line, its direction; a point whose element stays behind measures where it is now.
                var stations = Dimensions.Stations(e).Select(s => (t.Point(s.P), s.Ref is { } r && moving.Contains(r.Element) ? r : null)).ToList();
                Dimensions.Write(attrs, stations);
                if (attrs["at"] is JsonArray) attrs["at"] = J(t.Point(P(attrs["at"])));
                if (attrs["dir"] is JsonArray) attrs["dir"] = J(t.Dir(P(attrs["dir"])));
                // Texts moved by hand turn and mirror with the string.
                Dimensions.WriteOffsets(attrs, Dimensions.OffsetsOf(attrs, stations.Count - 1).Select(o => o is { } v ? t.Dir(v) : (Vec2?)null).ToList());
                break;
            }
            case "DIMENSION":
                foreach (var k in new[] { "a", "b", "at" }) if (attrs[k] is JsonArray) attrs[k] = J(t.Point(P(attrs[k])));
                break;
        }
        return attrs;
    }

    /// <summary>
    /// A move of a string some of whose points hang on elements that are not moving with it: the part of the move
    /// square to the string, which is all that moving it may do; null for a turn, a mirror, a copy or a free string.
    /// </summary>
    static Vec2? Held(Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        if (t.Turn != 0 || t.Mirrored || moving.Count == 0 || e.Attrs["at"] is not JsonArray) return null;
        var stations = Dimensions.Stations(e);
        if (stations.Count < 2 || !stations.Any(s => s.Ref is { } r && !moving.Contains(r.Element))) return null;
        if (Dimensions.Along(e.Attrs, stations[0].P, stations[^1].P) is not { } u) return null;
        var by = t.Point(default);
        var n = u.Perp;
        return n * Vec2.Dot(by, n);
    }

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        if (e.PredefinedType == Drafts.Predefined) Drafts.Require(ctx.Model, e.Id);
        ctx.Replace(e.With(attrs: Carry(ctx.Model, e, t, moving)));
    }

    public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        var copy = new Entity(ctx.NewId(), e.Class, e.Name, Carry(ctx.Model, e, t, new HashSet<string>()), e.PredefinedType);
        ctx.Add(copy);
        return copy.Id;
    }
}
