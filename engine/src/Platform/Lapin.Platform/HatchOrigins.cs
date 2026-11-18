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

    public static void Register(IModuleRegistry r)
    {
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
