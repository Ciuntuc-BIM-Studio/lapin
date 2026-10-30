using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drafting.Cad;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// A Studio solid moves, turns and copies in plan: one sketched on a plan with
/// its sketch (outline, holes, path or axis: the solid follows it); one sketched
/// on a section, an elevation or a reference plane by where it is placed (its sketch
/// stays on its view; a copy shares it); a primitive, a pushed face or a body by
/// its own numbers. Its steps (push/pull, fillets, slices…) go with it.
/// </summary>
public sealed class StudioPlacement : IElementPlacement
{
    public bool Handles(Entity e) => e.Str("operation") != null && StudioModule.Classes.Contains(e.Class);

    /// <summary>The sketch's shapes: the outline (every piece of it), its holes, the path or axis, an end profile, a rail.</summary>
    static List<Entity> Sketch(IModelView m, Entity e)
    {
        var o = new List<Entity>();
        if (m.Get(e.Str("profile") ?? "") is not { } profile) return o;
        var ids = (Sketches.OutlineOf(m, profile)?.Ids ?? [profile.Id]).ToList();
        ids.AddRange(Sketches.HoleOutlines(m, profile, [profile.Id]).SelectMany(h => h.Ids));
        ids.AddRange(new[] { e.Str("guide"), e.Str("endProfile"), e.Str("rail") }.OfType<string>());
        ids.AddRange((e.Attrs["profiles"] as JsonArray)?.Select(x => (string?)x).OfType<string>() ?? []);
        o.AddRange(ids.Distinct().Select(m.Get).OfType<Entity>());
        return o;
    }

    /// <summary>Whether the solid is drawn from a sketch on a plan (it then moves with its sketch).</summary>
    static bool OnPlan(IModelView m, Entity e)
    {
        if (!FromSketch(e)) return false;
        var profile = m.Get(e.Str("profile") ?? "");
        var view = profile == null ? null : Views.Find(m, profile.Str("viewId") ?? "");
        return view?.Str("kind") is null or "plan";
    }

    static bool FromSketch(Entity e) => e.Str("operation") is "extrude" or "sweep" or "revolve" or "loft" or Thick.Ring or Thick.Box or Thick.Wall;

    /// <summary>A thick solid (docs/plan-studio-thick.md) is drawn from its shape where it lies: one off a plan moves with its plane.</summary>
    static void ThickOnPlan(IModelView m, Entity e)
    {
        if (e.Str("operation") is Thick.Ring or Thick.Box or Thick.Wall && !OnPlan(m, e))
            throw new CommandException($"„{e.Name}” is sketched on a section, an elevation or a reference plane: move its sketch there, or move the plane it lies on.");
    }

    /// <summary>The solid's own numbers carried by the move: a primitive's or a body's point and turn, a pushed face's outline, the steps; a solid on another view's placement.</summary>
    static JsonObject Carried(IModelView m, Entity e, Placement2D t)
    {
        var attrs = e.CloneAttrs();
        Vec3 Pt(Vec3 p) { var q = t.Point(p.Xy); return new Vec3(q.X, q.Y, p.Z); }
        Vec3 Dir(Vec3 d) { var q = t.Dir(d.Xy); return new Vec3(q.X, q.Y, d.Z); }
        switch (e.Str("operation"))
        {
            case "primitive" or "body":
                if (Features.P(attrs["at"]) is { } at) attrs["at"] = Features.J(Pt(at));
                attrs["rotation"] = t.Angle(e.Num("rotation") * Math.PI / 180) * 180 / Math.PI;
                break;
            case "prism":
                if (attrs["outline"] is JsonArray outline) attrs["outline"] = new JsonArray(outline.Select(Features.P).OfType<Vec3>().Select(p => (JsonNode)Features.J(Pt(p))).ToArray());
                if (attrs["holes"] is JsonArray holes)
                    attrs["holes"] = new JsonArray(holes.OfType<JsonArray>().Select(h => (JsonNode)new JsonArray(h.Select(Features.P).OfType<Vec3>().Select(p => (JsonNode)Features.J(Pt(p))).ToArray())).ToArray());
                if (Features.P(attrs["normal"]) is { } n) attrs["normal"] = Features.J(Dir(n));
                break;
        }
        if (FromSketch(e) && !OnPlan(m, e))
        {
            // Placed: the move after whatever placed it before.
            var old = StudioModule.Placed(e) ?? [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0];
            Vec3 X(int k) => new(old[k], old[k + 1], old[k + 2]);
            var (x, y, z, o) = (Dir(X(0)), Dir(X(3)), Dir(X(6)), Pt(X(9)));
            attrs["placed"] = StoredBodies.WriteXf([x.X, x.Y, x.Z, y.X, y.Y, y.Z, z.X, z.Y, z.Z, o.X, o.Y, o.Z]);
        }
        if (Features.Map(e.Attrs["features"] as JsonArray, t) is { } fs) attrs["features"] = fs;
        return attrs;
    }

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        ThickOnPlan(ctx.Model, e);
        if (OnPlan(ctx.Model, e))
            foreach (var d in Sketch(ctx.Model, e).Where(d => !moving.Contains(d.Id)))
                if (Drafts.ShapeOf(d) is { } s) Drafts.Set(ctx, d, AnnotationPlacement.Map(s, t));
        ctx.Replace(e.With(attrs: Carried(ctx.Model, e, t)));
    }

    public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        // A box's bottom or top is copied with its box (the copy makes its own).
        if (e.Str("operation") == Thick.BoxSlab) return e.Id;
        ThickOnPlan(ctx.Model, e);
        var attrs = Carried(ctx.Model, e, t);
        if (OnPlan(ctx.Model, e))
        {
            var map = new Dictionary<string, string>();
            foreach (var d in Sketch(ctx.Model, e))
                if (Drafts.ShapeOf(d) is { } s) map[d.Id] = Drafts.Add(ctx, d.Str("viewId")!, d.Str("layer"), AnnotationPlacement.Map(s, t)).Id;
            foreach (var key in new[] { "profile", "guide", "endProfile", "rail" })
                if (e.Str(key) is { } id) attrs[key] = map.GetValueOrDefault(id) ?? id;
            if (e.Attrs["profiles"] is JsonArray ps) attrs["profiles"] = new JsonArray(ps.Select(x => (JsonNode?)(map.GetValueOrDefault((string?)x ?? "") ?? (string?)x)).ToArray());
        }
        var copy = new Entity(ctx.NewId(), e.Class, e.Name, attrs, e.PredefinedType);
        ctx.Add(copy);
        if (Spatial.StoreyOf(ctx.Model, e.Id) is { } st) Spatial.Contain(ctx, st.Id, copy.Id);
        if (e.Str("operation") == Thick.Box) Boxes.Reconcile(ctx, copy);
        return copy.Id;
    }
}
