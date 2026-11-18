using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Where an element's hatches start (decision 671): its attribute <c>hatchOrigin</c>, <c>[u, v]</c> in metres in the
/// element's own frame, so it moves with the element.
/// <list type="bullet">
/// <item>An element with an axis (<c>start</c>, <c>end</c>: a wall, a beam): <c>u</c> along the axis from its start; in a
/// plan whose element hatches along its axis (<c>hatchAlign</c> axis) <c>v</c> is across it, to the axis's left; in an
/// elevation or a section <c>v</c> is up from the element's base (the lowest point of what the view sees of it).</item>
/// <item>Any other element, in an elevation or a section: <c>u</c> right from its leftmost point the view sees, <c>v</c>
/// up from its lowest.</item>
/// </list>
/// Its surface hatches in elevations and sections (from the view's origin or along the layer) and its cut bands in a plan
/// along its axis start there. Without one, a real-size pattern on a facade starts its courses at the element's base (from
/// the view's origin across); a paper pattern keeps the view's origin, as before (so nothing an older project drew moves:
/// it had no real-size patterns). <c>element.setHatchOrigin</c> sets or clears it, one undo step.
/// </summary>
public static class HatchOrigins
{
    public const string Key = "hatchOrigin";

    /// <summary>The element's own origin, [u, v] metres; null: none.</summary>
    public static Vec2? Of(Entity e) => Args.TryPoint(e.Attrs[Key]);

    /// <summary>In a plan along the axis a→b: the origin's point (along from a, to the left across); null without one.</summary>
    public static Vec2? OnAxis(Entity e, Vec2 a, Vec2 b)
    {
        if (Of(e) is not { } o || (b - a).Length < 1e-9) return null;
        var d = (b - a) * (1 / (b - a).Length);
        return a + d * o.X + new Vec2(-d.Y, d.X) * o.Y;
    }

    /// <summary>
    /// In an elevation or a section seen through <paramref name="frame"/>: where an element's surface hatch starts (view
    /// coordinates), given the lowest left corner of what the view sees of it; null when it keeps the view's origin (no
    /// origin of its own and a paper pattern).
    /// </summary>
    public static Vec2? InView(Entity? e, ViewFrame frame, Vec2 lowest, bool model)
    {
        if (e != null && Of(e) is { } o)
        {
            double x;
            if (e.Attrs["start"] != null && e.Attrs["end"] != null)
            {
                var (a, b) = (Args.PointOf(e.Attrs["start"]), Args.PointOf(e.Attrs["end"]));
                var at = (b - a).Length < 1e-9 ? a : a + (b - a) * (o.X / (b - a).Length);
                x = frame.Map(new Vec3(at.X, at.Y, 0)).X;
            }
            else x = lowest.X + o.X;
            return new Vec2(x, lowest.Y + o.Y);
        }
        // A real-size pattern's courses start at the element's base; across, from the view's origin (they run on from one wall to the next).
        return model ? new Vec2(0, lowest.Y) : null;
    }

    /// <summary>The element, the view (its frame as it draws, its kind) a grip of the origin is shown in.</summary>
    static (Entity E, ViewFrame Frame, bool Plan) Context(IQueryContext q, JsonObject a)
    {
        var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("The element is not in the project.");
        var view = Views.Find(q.Model, (string?)a["view"] ?? "") ?? throw new CommandException("There is no such view.");
        var frame = Views.Render(q, view).Frame ?? throw new CommandException("The view draws no model (a sheet's paper, a legend).");
        return (e, frame, (view.Str("kind") ?? "plan") == "plan");
    }

    /// <summary>The lowest left corner of what the element is, in the view (its geometry's vertices mapped by the frame).</summary>
    static Vec2 LowestIn(IQueryContext q, Entity e, ViewFrame f)
    {
        var lo = new Vec2(double.MaxValue, double.MaxValue);
        foreach (var part in q.Geometry(e.Id)?.Parts ?? [])
            foreach (var v in part.Solid.Vertices) { var p = f.Map(v); lo = new Vec2(Math.Min(lo.X, p.X), Math.Min(lo.Y, p.Y)); }
        return lo.X == double.MaxValue ? f.Map(new Vec3(0, 0, 0)) : lo;
    }

    static (Vec2 A, Vec2 B)? AxisOf(Entity e) =>
        e.Attrs["start"] != null && e.Attrs["end"] != null && Args.PointOf(e.Attrs["start"]) is var a && Args.PointOf(e.Attrs["end"]) is var b && (b - a).Length > 1e-9 ? (a, b) : null;

    /// <summary>
    /// Where the element's origin <paramref name="o"/> ([u, v]) lies in a view (view coordinates): in a plan along its axis (u along,
    /// v to the left), in an elevation or a section u along its axis (or right from its leftmost point) and v up from its base.
    /// </summary>
    public static Vec2 PointIn(IQueryContext q, Entity e, ViewFrame f, bool plan, Vec2 o)
    {
        if (AxisOf(e) is var (a, b))
        {
            var d = (b - a) * (1 / (b - a).Length);
            if (plan) return f.Map(new Vec3(a.X + d.X * o.X - d.Y * o.Y, a.Y + d.Y * o.X + d.X * o.Y, 0));
            var at = a + d * o.X;
            return new Vec2(f.Map(new Vec3(at.X, at.Y, 0)).X, LowestIn(q, e, f).Y + o.Y);
        }
        return LowestIn(q, e, f) + o;
    }

    /// <summary>The element's origin ([u, v], metres in its frame) that lies at a view's point <paramref name="p"/>: <see cref="PointIn"/> turned round.</summary>
    public static Vec2 OriginAt(IQueryContext q, Entity e, ViewFrame f, bool plan, Vec2 p)
    {
        if (AxisOf(e) is var (a, b))
        {
            var len = (b - a).Length;
            var d = (b - a) * (1 / len);
            if (plan)
            {
                var (pa, pb) = (f.Map(new Vec3(a.X, a.Y, 0)), f.Map(new Vec3(b.X, b.Y, 0)));
                var dv = (pb - pa) * (1 / Math.Max(1e-12, (pb - pa).Length));
                var w = p - pa;
                return new Vec2(Vec2.Dot(w, dv), dv.X * w.Y - dv.Y * w.X);
            }
            var (xa, xb) = (f.Map(new Vec3(a.X, a.Y, 0)).X, f.Map(new Vec3(b.X, b.Y, 0)).X);
            var u = Math.Abs(xb - xa) < 1e-9 ? 0 : (p.X - xa) / (xb - xa) * len;
            return new Vec2(u, p.Y - LowestIn(q, e, f).Y);
        }
        return p - LowestIn(q, e, f);
    }

    public static void Register(IModuleRegistry r)
    {
        // {id, view} → {point: [x, y] in the view, origin: [u, v], own}: where the element's hatch origin is (its own, else [0, 0]:
        // its axis's start or its lowest left corner), for a grip.
        r.Query("element.hatchOriginPoint", (q, a) =>
        {
            var (e, f, plan) = Context(q, a);
            var own = Of(e);
            var o = own ?? new Vec2(0, 0);
            var p = PointIn(q, e, f, plan, o);
            return new JsonObject { ["point"] = new JsonArray(p.X, p.Y), ["origin"] = new JsonArray(o.X, o.Y), ["own"] = own != null };
        });

        // {id, view, point: [x, y]} → {origin: [u, v]}: the origin a grip dropped at that point of the view sets (element.setHatchOrigin).
        r.Query("element.hatchOriginAt", (q, a) =>
        {
            var (e, f, plan) = Context(q, a);
            var p = Args.TryPoint(a["point"]) ?? throw new CommandException("A point is [x, y] in the view.");
            var o = OriginAt(q, e, f, plan, p);
            return new JsonObject { ["origin"] = new JsonArray(Math.Round(o.X, 6), Math.Round(o.Y, 6)) };
        });

        // {id | ids, origin: [u, v] (metres, the element's frame) | null (clears it)} → null. One undo step.
        r.Command("element.setHatchOrigin", "Hatch origin", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            if (ids.Count == 0) throw new CommandException("Name the element (id or ids).");
            if (!a.ContainsKey("origin")) throw new CommandException("Give the origin, [u, v] in metres, or null to clear it.");
            var origin = a["origin"] == null ? (Vec2?)null : Args.TryPoint(a["origin"]) ?? throw new CommandException("A hatch origin is [u, v] in metres, or null.");
            foreach (var id in ids)
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException("The element is not in the project.");
                var attrs = e.CloneAttrs();
                if (origin is { } o) attrs[Key] = new JsonArray(o.X, o.Y); else attrs.Remove(Key);
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });
    }
}
