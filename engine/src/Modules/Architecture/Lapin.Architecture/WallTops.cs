using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A wall's top under what is over it: Lapin's own roofs (<see cref="RoofTop"/>,
/// by their solved planes) and any other body the wall is attached to — a Studio
/// solid, a slab, a beam, an element imported from IFC, a roof a file brought
/// without Lapin's rules — read from the body's underside: straight up from the
/// wall's base along its axis, the lowest face that looks down. In <c>auto</c>
/// the storey's roofs count, imported ones with them; attached, the elements
/// named (<c>roofIds</c>), else every roof. Where the two kinds overlap the
/// lower one wins. A wall recognised from a file (its record kept under
/// <c>imported</c>) never rises above the top the file gives it, its height:
/// what is over it may only cut it (docs/spec-ifc.md, Recunoașterea).
/// </summary>
public static class WallTops
{
    /// <summary>The wall's top as (u along the axis, v above its base), or null when nothing over it changes it.</summary>
    public static List<(double U, double V)>? Profile(Entity wall, RoofTop.WallAxis w, IModelView m, IServices? services)
    {
        var roofs = RoofTop.Profile(wall, w, m);
        var bodies = Bodies(wall, m, services);
        var top = bodies.Count == 0 ? roofs : BodySampler.Underside(bodies, w.A, w.Dir, w.Length, w.Base, w.Height) is { } under ? roofs == null ? under : BodySampler.Merge(roofs, under) : roofs;
        if (Own(wall, w) is { } own) top = top == null ? own : BodySampler.Merge(top, own);
        return top != null && wall.Attrs["imported"] != null ? Capped(top, w.Height) : top;
    }

    /// <summary>
    /// A wall's own top along its axis (<c>topProfile</c>: [[u, v], …], u from its start, v over its base; a step is two
    /// points at one u), as a file gives it (a gable a wall of parts has: <see cref="ForeignWallParts"/>); flat past its
    /// ends. Lower than whatever else is over the wall, it wins; a curved wall has none.
    /// </summary>
    public const string TopProfileKey = "topProfile";

    public static JsonArray ProfileJson(IEnumerable<(double U, double V)> p) => new([.. p.Select(q => (JsonNode)new JsonArray(Math.Round(q.U, 6), Math.Round(q.V, 6)))]);

    /// <summary>The wall's own top (<see cref="TopProfileKey"/>) in the frame of <paramref name="w"/> (its u from w's start), or null.</summary>
    static List<(double U, double V)>? Own(Entity wall, RoofTop.WallAxis w)
    {
        if (wall.Attrs[TopProfileKey] is not JsonArray pts || pts.Count < 2 || PathCurve.Of(wall).IsArc) return null;
        var shift = Vec2.Dot(w.A - Walls.Start(wall), w.Dir);
        var o = new List<(double U, double V)>();
        foreach (var q in pts)
            if (q is JsonArray { Count: >= 2 } a && (double?)a[0] is { } u && (double?)a[1] is { } v) o.Add((u - shift, v));
        return o.Count >= 2 ? o : null;
    }

    /// <summary>A profile no higher than <paramref name="h"/> (where it crosses it, the crossing kept); null when nothing of it is left under.</summary>
    static List<(double U, double V)>? Capped(List<(double U, double V)> p, double h)
    {
        if (p.All(q => q.V <= h + 1e-9)) return p;
        var o = new List<(double U, double V)>();
        for (var i = 0; i < p.Count; i++)
        {
            if (i > 0 && (p[i - 1].V - h) * (p[i].V - h) < 0 && p[i].U - p[i - 1].U > 1e-12)
                o.Add((p[i - 1].U + (p[i].U - p[i - 1].U) * (h - p[i - 1].V) / (p[i].V - p[i - 1].V), h));
            o.Add((p[i].U, Math.Min(p[i].V, h)));
        }
        return o.All(q => Math.Abs(q.V - h) < 1e-6) ? null : BodySampler.Simplify(o);
    }

    /// <summary>
    /// The triangles of the bodies the wall rises to, besides Lapin's own roofs, read by the platform's sampler
    /// (<see cref="BodySampler.Sample"/>, docs/plan-attach-to.md A1): none when they come back round to the wall (a circle of
    /// attachments a script or an old file made), which then stands at its own height.
    /// </summary>
    static List<BodySampler.Tri> Bodies(Entity wall, IModelView m, IServices? services)
    {
        var mode = wall.Str("roofTop") ?? "auto";
        if (mode == "none" || services == null) return [];
        var targets = Targets(wall, m, mode);
        return targets.Count == 0 ? [] : BodySampler.Sample(wall, targets, m, services).Tris;
    }

    /// <summary>The bodies the wall rises to besides Lapin's own roofs: the elements it names, else (attached) every roof, else its storey's roofs.</summary>
    static List<Entity> Targets(Entity wall, IModelView m, string mode)
    {
        IEnumerable<Entity> targets;
        if (mode == "attach")
        {
            // Named, only what is still there: a target gone never widens it to every roof. Naming none, every roof.
            targets = Named(wall) is { } named ? named.Select(m.Get).OfType<Entity>().ToList() : m.OfClass(IfcClass.Roof);
        }
        else targets = RoofTop.RoofsOver(wall.Id, m);
        var o = new List<Entity>();
        foreach (var t in targets)
        {
            if (t.Class == IfcClass.Wall || t.Id == wall.Id) continue;
            // Lapin's own roofs are read by their planes (RoofTop), not by their covering; a flat one, which has no planes, by its
            // deck's underside when the wall is attached to it (decision 329).
            if (t.Class == IfcClass.Roof && !StoredBodies.Has(t) && Roof.Resolve(t, m) is { } own && !(own.Flat && mode == "attach")) continue;
            o.Add(t);
        }
        return o;
    }

    /// <summary>The ids an attached wall names (<c>roofIds</c>), or null when it names none (it then takes every roof).</summary>
    public static List<string>? Named(Entity wall) =>
        wall.Attrs["roofIds"] is JsonArray ids && ids.Select(x => (string?)x).OfType<string>().ToList() is { Count: > 0 } named ? named : null;

    /// <summary>The names of what a wall's top was attached to before they were all deleted, kept for the Problems panel (<see cref="AttachedLinks"/>).</summary>
    public const string GoneKey = "roofTopGone";

    /// <summary>
    /// What is wrong with what the wall's top is attached to (for the Problems panel); null when nothing is. With the services,
    /// also a circle of attachments back to the wall (<see cref="Attachments.CycleProblem"/>).
    /// </summary>
    public static string? Problem(IModelView m, Entity wall, IServices? services = null)
    {
        var mode = wall.Str("roofTop") ?? "auto";
        if (services != null && mode == "attach" && Attachments.CycleProblem(m, services, wall, Attachments.Top) is { } cycle) return cycle;
        if (mode == "attach" && Named(wall) is { } named && named.Where(id => m.Get(id) == null).ToList() is { Count: > 0 } missing)
            return missing.Count == named.Count
                ? $"What its top is attached to is gone ({string.Join(", ", missing)}): it stands at its own height."
                : $"Part of what its top is attached to is gone ({string.Join(", ", missing)}): it follows the rest.";
        if (mode == "none" && wall.Attrs[GoneKey] is JsonArray gone && gone.Count > 0)
            return $"What its top was attached to was deleted ({string.Join(", ", gone.Select(x => $"„{(string?)x}”"))}): it stands at its own height. Attach it again, or detach it.";
        return null;
    }
}

/// <summary>
/// What an attached wall names goes with it (docs/plan-attach-to.md §1.1, stage A0): a target deleted is taken out of
/// every wall's <c>roofIds</c> in the delete's undo step. A wall left naming nothing stands at its own height
/// (<c>roofTop</c> none), never rising to every roof as a wall attached without names does, and keeps the names of what
/// it lost for the Problems panel (<see cref="WallTops.GoneKey"/>) until it is attached again or detached.
/// </summary>
sealed class AttachedLinks : IDeletionFollowers
{
    public void Deleting(ICommandContext ctx, IReadOnlySet<string> deleting)
    {
        foreach (var wall in ctx.Model.OfClass(IfcClass.Wall).Where(w => !deleting.Contains(w.Id)).ToList())
        {
            if (WallTops.Named(wall) is not { } named || !named.Any(deleting.Contains)) continue;
            var attrs = wall.CloneAttrs();
            var left = named.Where(id => !deleting.Contains(id)).ToList();
            if (left.Count > 0) attrs["roofIds"] = new JsonArray([.. left.Select(x => (JsonNode)x)]);
            else
            {
                attrs.Remove("roofIds");
                attrs["roofTop"] = "none";
                attrs[WallTops.GoneKey] = new JsonArray([.. named.Select(id => (JsonNode)(ctx.Model.Get(id)?.Name ?? id))]);
            }
            ctx.Replace(wall.With(attrs: attrs));
        }
    }
}
