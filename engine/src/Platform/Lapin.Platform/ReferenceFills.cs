using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The doors and windows that fill a reference's kept openings (docs/spec-ifc.md, Golurile referințelor; <see cref="StoredBodies.OpeningsKey"/>),
/// handled as a native wall's are: a door in a referenced wall goes with it when the wall moves, turns, mirrors, rises or
/// is copied (its copy fills the copy's opening), and goes when the wall is deleted. A door moved or copied on its own
/// leaves the opening (it no longer fills it: a reference's hole is the file's, it does not slide), and a door deleted
/// leaves the opening empty, as an IfcOpeningElement without a filling is. The door stays its own element all along.
/// </summary>
public sealed class ReferenceFills : IHostedPlacement, IDeletion
{
    /// <summary>The ids of the elements filling the kept openings of <paramref name="e"/>.</summary>
    public static IEnumerable<string> Fillers(Entity e) =>
        (e.Attrs[StoredBodies.OpeningsKey] as JsonArray ?? []).OfType<JsonObject>()
            .SelectMany(o => (o["fills"] as JsonArray ?? []).OfType<JsonObject>())
            .Select(f => (string?)f["id"]).OfType<string>();

    /// <summary>The reference whose kept opening <paramref name="filler"/> fills, if any.</summary>
    public static Entity? HostOf(IModelView m, string filler)
    {
        foreach (var e in m.Entities)
            if (e.Attrs[StoredBodies.OpeningsKey] is JsonArray && Fillers(e).Contains(filler)) return e;
        return null;
    }

    /// <summary>The reference's kept openings with <paramref name="filler"/> no longer filling them; null when it filled none.</summary>
    public static JsonObject? Without(Entity host, string filler)
    {
        var attrs = host.CloneAttrs();
        var changed = false;
        foreach (var o in (attrs[StoredBodies.OpeningsKey] as JsonArray ?? []).OfType<JsonObject>())
            if (o["fills"] is JsonArray fills)
                foreach (var f in fills.OfType<JsonObject>().Where(f => (string?)f["id"] == filler).ToList()) { fills.Remove(f); changed = true; }
        return changed ? attrs : null;
    }

    static void Unfill(ICommandContext ctx, string filler)
    {
        if (HostOf(ctx.Model, filler) is { } host && Without(host, filler) is { } attrs) ctx.Replace(host.With(attrs: attrs));
    }

    // ─── Moved or copied on its own, its host staying ─────────────────────

    public bool Handles(IModelView m, Entity e) => StoredBodies.Has(e) && HostOf(m, e.Id) != null;

    string? IHostedPlacement.HostOf(IModelView m, Entity e) => StoredBodies.Has(e) ? HostOf(m, e.Id)?.Id : null;

    public IReadOnlyList<string> Transform(ICommandContext ctx, IReadOnlyList<Entity> elements, Placement2D t, bool copy)
    {
        var made = new List<string>();
        var moving = elements.Select(e => e.Id).ToHashSet();
        foreach (var e in elements)
        {
            if (ctx.Model.Get(e.Id) is not { } now) continue;
            // A copy is a reference of its own, filling nothing; the door moved leaves the file's opening behind.
            if (copy) made.Add(Placement.Copy(ctx, now, t));
            else
            {
                Unfill(ctx, now.Id);
                Placement.Transform(ctx, ctx.Model.Get(now.Id)!, t, moving);
            }
        }
        return made;
    }

    // ─── Deleted ──────────────────────────────────────────────────────────

    public bool Delete(ICommandContext ctx, string id)
    {
        if (ctx.Model.Get(id) is not { } e || !StoredBodies.Has(e)) return false;
        // A referenced wall takes the doors and windows in its openings with it, as a native wall does.
        var bodies = new HashSet<string>();
        foreach (var filler in Fillers(e).Distinct().ToList())
            if (ctx.Model.Get(filler) is { } door && StoredBodies.Has(door))
            {
                if (StoredBodies.InstanceOf(door) is var (body, _)) bodies.Add(body);
                ctx.Remove(filler);
            }
        // A shared body none of what stays names any more goes with them.
        if (bodies.Count > 0)
        {
            var self = StoredBodies.InstanceOf(e)?.Body;
            foreach (var x in ctx.Model.Entities)
                if ((string?)x.Attrs[StoredBodies.BodyOf] is { } b) bodies.Remove(b);
            foreach (var b in bodies) if (b != self) ctx.Remove(b);
        }
        // A door deleted leaves its opening empty.
        Unfill(ctx, id);
        return false;
    }
}
