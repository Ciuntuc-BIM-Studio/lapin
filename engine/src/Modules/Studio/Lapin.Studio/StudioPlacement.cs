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

    /// <summary>The sketch that is the solid's own: on a plan it goes with the solid; on another view a copy shares it.</summary>
    public IEnumerable<string> Dependents(IModelView m, Entity e) => OnPlan(m, e) ? Sketch(m, e).Select(x => x.Id) : [];

    /// <summary>Whether the solid is drawn from a sketch on a plan (it then moves with its sketch).</summary>
    internal static bool OnPlan(IModelView m, Entity e)
    {
        if (!FromSketch(e)) return false;
        var profile = m.Get(e.Str("profile") ?? "");
        var view = profile == null ? null : Views.Find(m, profile.Str("viewId") ?? "");
        return view?.Str("kind") is null or "plan";
    }

    internal static bool FromSketch(Entity e) => e.Str("operation") is "extrude" or "sweep" or "revolve" or "loft" or "followMe" or Thick.Ring or Thick.Box or Thick.Wall;

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
            case "primitive" or "body" or StudioModule.SubDOperation:
                if (Features.P(attrs["at"]) is { } at) attrs["at"] = Features.J(Pt(at));
                attrs["rotation"] = t.Angle(e.Num("rotation") * Math.PI / 180) * 180 / Math.PI;
                break;
            case "prism":
                if (attrs["outline"] is JsonArray outline) attrs["outline"] = new JsonArray(outline.Select(Features.P).OfType<Vec3>().Select(p => (JsonNode)Features.J(Pt(p))).ToArray());
                if (attrs["holes"] is JsonArray holes)
                    attrs["holes"] = new JsonArray(holes.OfType<JsonArray>().Select(h => (JsonNode)new JsonArray(h.Select(Features.P).OfType<Vec3>().Select(p => (JsonNode)Features.J(Pt(p))).ToArray())).ToArray());
                if (Features.P(attrs["normal"]) is { } n) attrs["normal"] = Features.J(Dir(n));
                break;
            case Parts.Operation:
            {
                // A placed part: its point, its angle, a mirror turning it over.
                var q = t.Point(Args.TryPoint(e.Attrs["at"]) ?? default);
                attrs["at"] = new JsonArray(q.X, q.Y);
                var turned = t.Angle(e.Num("rotation") * Math.PI / 180) * 180 / Math.PI;
                attrs["rotation"] = Math.Round(((turned % 360) + 360) % 360, 9);
                if (t.Mirrored) { attrs["mirror"] = !e.Bool("mirror"); attrs["rotation"] = Math.Round((((turned + 180) % 360) + 360) % 360, 9); }
                break;
            }
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
            SketchConstraints.CopyAmong(ctx, map, t);
        }
        var copy = new Entity(ctx.NewId(), e.Class, e.Name, attrs, e.PredefinedType);
        ctx.Add(copy);
        if (Spatial.StoreyOf(ctx.Model, e.Id) is { } st) Spatial.Contain(ctx, st.Id, copy.Id);
        // Its type (a placed part's part) with it.
        if (ctx.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } type) ctx.Relate(IfcClass.RelDefinesByType, type.Id, copy.Id);
        if (e.Str("operation") == Thick.Box) Boxes.Reconcile(ctx, copy);
        return copy.Id;
    }
}

/// <summary>
/// A Studio solid raised or lowered by a 3D move (docs/plan-3d-modelling.md): a move step up or down, merged into the
/// last step when that is already an upright move, so a solid moved up twice keeps one step.
/// </summary>
public sealed class StudioStoreyShift : IStoreyShift
{
    /// <summary>A primitive or a body stands on a point in the world: it goes up with the storey.</summary>
    public JsonObject? Shifted(Entity e, double dz)
    {
        if (e.Str("operation") is not ("primitive" or "body" or StudioModule.SubDOperation) || Features.P(e.Attrs["at"]) is not { } at) return null;
        var attrs = e.CloneAttrs();
        attrs["at"] = Features.J(new Vec3(at.X, at.Y, at.Z + dz));
        return attrs;
    }

    /// <summary>
    /// Besides a primitive's point: a pushed face's outline (in the world, on its host's face), and a solid sketched on a
    /// reference plane, a section or an elevation (placed in the world): each goes up with the storey, as its host does.
    /// </summary>
    public JsonObject? Shifted(IModelView m, Entity e, double dz)
    {
        if (Shifted(e, dz) is { } own) return own;
        if (Math.Abs(dz) < 1e-12) return null;
        static JsonNode Up(JsonNode? p, double dz) => Features.P(p) is { } q ? Features.J(new Vec3(q.X, q.Y, q.Z + dz)) : p!.DeepClone();
        if (e.Str("operation") == "prism")
        {
            var attrs = e.CloneAttrs();
            if (attrs["outline"] is JsonArray outline) attrs["outline"] = new JsonArray(outline.Select(p => Up(p, dz)).ToArray());
            if (attrs["holes"] is JsonArray holes) attrs["holes"] = new JsonArray(holes.OfType<JsonArray>().Select(h => (JsonNode)new JsonArray(h.Select(p => Up(p, dz)).ToArray())).ToArray());
            return attrs;
        }
        if (StudioPlacement.FromSketch(e) && !StudioPlacement.OnPlan(m, e))
        {
            var x = StudioModule.Placed(e) ?? [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0];
            var attrs = e.CloneAttrs();
            attrs["placed"] = StoredBodies.WriteXf([x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7], x[8], x[9], x[10], x[11] + dz]);
            return attrs;
        }
        return null;
    }
}

public sealed class StudioRise : IElementRise
{
    public bool Handles(Entity e) => e.Str("operation") != null && StudioModule.Classes.Contains(e.Class);

    public bool Lift(ICommandContext ctx, Entity e, double dz)
    {
        var list = (e.Attrs["features"] as JsonArray)?.OfType<JsonObject>().Select(f => (JsonObject)f.DeepClone()).ToList() ?? [];
        var last = list.Count > 0 ? list[^1] : null;
        if (last != null && (string?)last["op"] == "move" && !Features.Off(last) && Features.P(last["by"]) is { } by && Math.Abs(by.X) < 1e-12 && Math.Abs(by.Y) < 1e-12)
        {
            var z = Math.Round((by.Z + dz) * 1e6) / 1e6;
            if (Math.Abs(z) < 1e-9) list.RemoveAt(list.Count - 1);
            else last["by"] = new JsonArray(0.0, 0.0, z);
        }
        else list.Add(new JsonObject { ["op"] = "move", ["by"] = new JsonArray(0.0, 0.0, dz) });
        var attrs = e.CloneAttrs();
        if (list.Count == 0) attrs.Remove("features");
        else attrs["features"] = new JsonArray(list.Select(f => (JsonNode)f).ToArray());
        ctx.Replace(e.With(attrs: attrs));
        return true;
    }
}

/// <summary>
/// What goes with a solid or an element carried whole (a user's report: a group moved left the void cut into its member's
/// face where it was): the Studio voids cutting it (a shape drawn on its face and pushed in, a face pushed into a wall, a
/// solid made to cut), when everything they cut goes too; a union's or an intersection's tools (drawn inside their target);
/// and a face pushed or pulled out of it (its <c>host</c>). Each is in world coordinates, so it is carried as the element
/// is (<see cref="StudioPlacement"/>), keeping its id; copied together, the copy of a void cuts the copy of what it cut.
/// </summary>
public sealed class StudioAttached : IAttachedElements, ITransformFollowers
{
    static string TypeOf(Relation r) => (string?)r.Attrs?["interferenceType"] ?? "Subtract";

    public IEnumerable<string> Attached(IModelView m, IReadOnlySet<string> carried)
    {
        var o = new HashSet<string>();
        foreach (var id in carried)
            foreach (var r in m.RelationsOf(id, IfcClass.RelInterferesElements).Where(x => x.Relating == id))
                foreach (var t in r.Related)
                {
                    if (carried.Contains(t) || m.Get(t) is not { } tool || tool.Str("operation") == null) continue;
                    // A plain solid subtracted stays the user's own; a void or a union's tool is the target's.
                    if (!StudioModule.IsVoid(tool) && TypeOf(r) == "Subtract") continue;
                    // A cut into more than one goes only when all of them go.
                    if (m.RelationsOf(t, IfcClass.RelInterferesElements).Where(x => x.Related.Contains(t)).All(x => carried.Contains(x.Relating))) o.Add(t);
                }
        foreach (var e in StudioModule.Classes.SelectMany(m.OfClass))
            if (e.Str("operation") == "prism" && e.Str("host") is { } host && carried.Contains(host) && !carried.Contains(e.Id)) o.Add(e.Id);
        return o;
    }

    public object? Before(ICommandContext ctx, IReadOnlySet<string> moving, IReadOnlySet<string> selected) => null;

    /// <summary>Copied together, a boolean between copies: the copy of a void cuts the copy of its target.</summary>
    public void After(ICommandContext ctx, object? state, Placement2D t, Placement2D plan, IReadOnlySet<string> moving, IReadOnlySet<string> selected, IReadOnlyDictionary<string, string> copies)
    {
        if (copies.Count == 0) return;
        var m = ctx.Model;
        string? CopyOf(string id) => copies.TryGetValue(id, out var c) && c != id && m.Get(c) != null ? c : null;
        var rels = copies.Keys.SelectMany(id => m.RelationsOf(id, IfcClass.RelInterferesElements)).DistinctBy(r => r.Id)
            .Where(r => CopyOf(r.Relating) != null && r.Related.All(x => CopyOf(x) != null)).ToList();
        foreach (var r in rels)
        {
            var (target, tools) = (CopyOf(r.Relating)!, r.Related.Select(x => CopyOf(x)!).ToList());
            if (m.RelationsOf(target, IfcClass.RelInterferesElements).Any(x => x.Relating == target && tools.All(x.Related.Contains))) continue;
            ctx.AddRelation(new Relation(ctx.NewId(), r.Class, target, tools, r.Attrs?.DeepClone() as JsonObject));
        }
    }
}
