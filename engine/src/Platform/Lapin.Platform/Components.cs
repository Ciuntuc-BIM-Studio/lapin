using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>What a module keeps right round an instance's members, in the command's undo step (a room's doors, which stand in walls that are not members).</summary>
public interface IInstanceMembers
{
    /// <summary>Before instances are compared: what an edit said of something outside them written on the member it belongs to, so it is carried as any edit is.</summary>
    void Edited(ICommandContext ctx, IEdit edit) { }
    /// <summary>An instance placed, moved, brought up to another or with a building wall changed near it, its inner walls already stopped.</summary>
    void Placed(ICommandContext ctx, Entity group) { }
}

/// <summary>
/// Components (docs/components.md), SketchUp's way, for what stands on its own: Studio solids, floors and rooms drawn by
/// their outline; never walls, whose joins and hosted doors belong to their neighbours. A component is a
/// <c>LapinComponent</c> and its instances are groups (<see cref="Groups"/>) carrying it and where they stand, so they are
/// picked, moved, copied and opened as groups are, and their members stay real elements: plans, quantities, schedules and
/// exports read them like any other. What changes inside one instance is carried to the others in the same undo step, each
/// member keeping its GlobalId (found by <c>componentKey</c>, the same in every instance). An apartment type
/// (docs/plan-units.md) is a component of <c>kind: "unit"</c>, which takes walls too, with the doors and windows they host.
/// Where such a wall meets the building is the instance's own: it stops in the building's wall, found after each placing and
/// never carried to the others (<see cref="StopInnerWalls"/>).
/// </summary>
public static class Components
{
    public const string Class = "LapinComponent";
    /// <summary>On an instance's group: its component.</summary>
    public const string Key = "component";
    /// <summary>On an instance's group: where it stands, [x, y, z, degrees, mirrored (0 or 1)]; mirrored first, about its own x axis, then turned.</summary>
    public const string XfKey = "componentXf";
    /// <summary>On a member: which member it is, the same in every instance.</summary>
    public const string MemberKey = "componentKey";
    /// <summary>On a component: "unit" for an apartment type (docs/plan-units.md).</summary>
    public const string KindKey = "kind";
    public const string UnitKind = "unit";

    public static string? Of(Entity group) => group.Class == Groups.Class ? group.Str(Key) : null;

    public static IEnumerable<Entity> Instances(IModelView m, string component) => Groups.All(m).Where(g => Of(g) == component);

    /// <summary>Whether an element can be part of a component: what stands on its own.</summary>
    public static bool Takes(Entity e) =>
        !StoredBodies.Has(e) && (e.Class is IfcClass.Slab or IfcClass.Space || e.Str("operation") != null);

    static bool IsUnit(IModelView m, string? component) => component != null && m.Get(component)?.Str(KindKey) == UnitKind;

    /// <summary>Whether an element can be part of an apartment type: anything of the project's own that stands on a storey; what a wall hosts comes with its wall.</summary>
    static bool TakesInUnit(IModelView m, Entity e) =>
        !StoredBodies.Has(e) && Hosts(m, e) == null && Groups.Groupable(m, e);

    /// <summary>The wall an element is hosted by: a door through its opening, an opening directly.</summary>
    static Entity? Hosts(IModelView m, Entity e) =>
        m.RelatingOf(e.Id, IfcClass.RelFillsElement) is { } opening ? m.RelatingOf(opening.Id, IfcClass.RelVoidsElement) ?? opening : m.RelatingOf(e.Id, IfcClass.RelVoidsElement);

    /// <summary>What a member hosts, its openings and what fills them, each found again by its own key.</summary>
    static IEnumerable<Entity> HostedBy(IModelView m, string member)
    {
        foreach (var o in m.RelatedOf(member, IfcClass.RelVoidsElement).ToList())
        {
            yield return o;
            foreach (var f in m.RelatedOf(o.Id, IfcClass.RelFillsElement).ToList()) yield return f;
        }
    }

    static void GiveKey(ICommandContext ctx, Entity e)
    {
        if (e.Str(MemberKey) != null) return;
        var a = e.CloneAttrs();
        a[MemberKey] = ctx.NewId();
        ctx.Replace(e.With(attrs: a));
    }

    static double[] Xf(Entity g)
    {
        if (g.Attrs[XfKey] is not JsonArray { Count: >= 4 } a) return [0, 0, 0, 0, 0];
        var x = a.Select(v => Json.Number(v) ?? 0).ToList();
        return [x[0], x[1], x[2], x[3], x.Count > 4 ? x[4] : 0];
    }

    static JsonArray Write(double[] x) => new(Math.Round(x[0], 9), Math.Round(x[1], 9), Math.Round(x[2], 9), Math.Round(x[3], 9), x[4]);

    /// <summary>Where an instance stands once carried by a placement; a mirror turns its handedness over.</summary>
    static double[] Carry(double[] xf, Placement2D t, double up)
    {
        var p = t.Point(new Vec2(xf[0], xf[1]));
        return t.Mirrored
            ? [p.X, p.Y, xf[2] + up, 2 * t.Axis * 180 / Math.PI - xf[3], xf[4] == 0 ? 1 : 0]
            : [p.X, p.Y, xf[2] + up, xf[3] + t.Turn * 180 / Math.PI, xf[4]];
    }

    /// <summary>An instance moved or turned as a whole: where it stands now. Its members are as they were, seen from it, so nothing follows.</summary>
    public static void MovedInstance(ICommandContext ctx, string groupId, Placement2D t, double up)
    {
        if (ctx.Model.Get(groupId) is not { } g || Of(g) == null) return;
        var attrs = g.CloneAttrs();
        attrs[XfKey] = Write(Carry(Xf(g), t, up));
        ctx.Replace(g.With(attrs: attrs));
    }

    /// <summary>An instance copied: the copy is another instance of the same component.</summary>
    public static void CopiedInstance(ICommandContext ctx, Entity group, Entity twin, Placement2D t, double up)
    {
        if (Of(group) is not { } c) return;
        var attrs = twin.CloneAttrs();
        attrs[Key] = c;
        attrs[XfKey] = Write(Carry(Xf(group), t, up));
        ctx.Replace(twin.With(attrs: attrs));
    }

    /// <summary>The apartment type a group is an instance of, and where it stands; null for any other group.</summary>
    public static (Entity Type, double X, double Y, double Z, double Degrees, bool Mirrored)? UnitOf(IModelView m, Entity group)
    {
        if (Of(group) is not { } c || m.Get(c) is not { } type || !IsUnit(m, c)) return null;
        var x = Xf(group);
        return (type, x[0], x[1], x[2], x[3], x[4] != 0);
    }

    /// <summary>An instance read back from a file that says so, under the group's GlobalId: its type made once, the members its own again.</summary>
    public static void Restore(IEdit edit, string groupId, string name, string typeId, string typeName, IReadOnlyList<string> members, double[] xf)
    {
        var m = edit.Model;
        var there = members.Select(m.Get).OfType<Entity>().Where(e => Groups.Groupable(m, e) && e.Str(MemberKey) != null).Select(e => e.Id).ToList();
        if (there.Count == 0 || m.Get(groupId) != null) return;
        if (m.Get(typeId) == null) edit.Add(new Entity(typeId, Class, typeName, new JsonObject { [KindKey] = UnitKind }));
        edit.Add(new Entity(groupId, Groups.Class, name, new JsonObject { [Key] = typeId, [XfKey] = Write(xf) }));
        edit.RelateAll(Groups.Rel, groupId, there);
    }

    /// <summary>The placement that takes what is in one instance to where it is in another.</summary>
    static Placement2D Between(double[] from, double[] to)
    {
        // One mirrored and the other not: a reflection about the line halfway between their turns; else a turn.
        if ((from[4] != 0) != (to[4] != 0))
        {
            var axis = (to[3] + from[3]) * Math.PI / 360;
            var (c2, s2) = (Math.Cos(2 * axis), Math.Sin(2 * axis));
            return new Placement2D(p =>
            {
                var (dx, dy) = (p.X - from[0], p.Y - from[1]);
                return new Vec2(to[0] + dx * c2 + dy * s2, to[1] + dx * s2 - dy * c2);
            }, 0, true, axis);
        }
        var turn = (to[3] - from[3]) * Math.PI / 180;
        var (c, s) = (Math.Cos(turn), Math.Sin(turn));
        return new Placement2D(p =>
        {
            var (dx, dy) = (p.X - from[0], p.Y - from[1]);
            return new Vec2(to[0] + dx * c - dy * s, to[1] + dx * s + dy * c);
        }, turn, false);
    }

    static IEnumerable<string> DependentsOf(ICommandContext ctx, Entity e) =>
        ctx.Services.All<IElementPlacement>().FirstOrDefault(p => p.Handles(e))?.Dependents(ctx.Model, e) ?? [];

    /// <summary>
    /// After a command: the instances it changed from within (a member edited, added or taken out, a member's sketch redrawn)
    /// carried to the other instances of their components. An instance moved, copied or just made is not an edit of its
    /// component; a component left without instances goes.
    /// </summary>
    public static void Settle(ICommandContext ctx, IEdit edit)
    {
        var m = ctx.Model;
        if (!m.OfClass(Class).Any()) return;
        var hooks = ctx.Services.All<IInstanceMembers>();
        foreach (var h in hooks) h.Edited(ctx, edit);
        var changed = edit.EntityChanges;
        // Instances this command made, moved or took away: their members changed with them.
        var whole = changed.Where(c => (c.After ?? c.Before)?.Class == Groups.Class && (c.After?.Str(Key) != null || c.Before?.Str(Key) != null)
            && (c.Before == null || c.After == null || c.Before.Attrs[XfKey]?.ToJsonString() != c.After.Attrs[XfKey]?.ToJsonString() || c.Before.Str(Key) != c.After.Str(Key))).Select(c => c.Id).ToHashSet();
        // The instances whose place in the building is to be looked at again.
        var placed = new List<string>(whole);
        var touched = new List<Entity>();
        // The members of each instance that changed: only they are made again in the others.
        var dirty = new Dictionary<string, HashSet<string>>();
        void Touch(string? groupId, string? member = null)
        {
            if (groupId == null || whole.Contains(groupId) || m.Get(groupId) is not { } g || Of(g) == null) return;
            if (touched.All(x => Of(x) != Of(g))) touched.Add(g);
            if (touched.First(x => Of(x) == Of(g)).Id != g.Id) return;
            if (!dirty.TryGetValue(g.Id, out var set)) dirty[g.Id] = set = [];
            if (member != null) set.Add(member);
        }
        void TouchMember(string id) { if (Groups.Of(m, id) is { } g) Touch(g.Id, id); }
        var ids = changed.Select(c => c.Id).ToHashSet();
        foreach (var c in changed)
        {
            if (c.After == null) continue;
            // A wall's end following the building's wall it stops in is that instance's own.
            if (!whole.Contains(Groups.Of(m, c.Id)?.Id ?? "") && FollowsLimit(ctx, edit, c)) continue;
            TouchMember(c.Id);
            // What a wall hosts, and what belongs to a member without being one (its layer usage).
            if (Hosts(m, c.After) is { } host) TouchMember(host.Id);
            else if (c.After.Class == IfcClass.MaterialLayerSetUsage)
                foreach (var e in m.RelatedOf(c.Id, IfcClass.RelAssociatesMaterial).ToList()) TouchMember(e.Id);
        }
        // A member joining or leaving shows on the group's relationship, a door coming or going on its wall's, a type changed on the element's.
        foreach (var r in edit.RelationChanges)
        {
            var rel = (r.After ?? r.Before)!;
            if (rel.Class == Groups.Rel)
            {
                Touch(rel.Relating);
                foreach (var e in rel.Related) if (m.Get(e) != null && Groups.Of(m, e)?.Id == rel.Relating) Touch(rel.Relating, e);
            }
            else if (rel.Class == IfcClass.RelVoidsElement) TouchMember(rel.Relating);
            else if (rel.Class == IfcClass.RelFillsElement && m.RelatingOf(rel.Relating, IfcClass.RelVoidsElement) is { } wall) TouchMember(wall.Id);
            else if (rel.Class is IfcClass.RelDefinesByType or IfcClass.RelAssociatesMaterial)
                foreach (var e in rel.Related.Append(rel.Relating).Where(x => m.Get(x) != null && Groups.Of(m, x) != null)) TouchMember(e);
            // A join inside an instance is the type's; one that leaves it, to the building, is the instance's own.
            else if (rel.Class == IfcClass.RelConnectsPathElements)
            {
                var ends = rel.Related.Append(rel.Relating).ToList();
                if (ends.All(x => m.Get(x) != null) && ends.Select(x => Groups.Of(m, x)?.Id).Distinct().ToList() is [{ }]) foreach (var e in ends) TouchMember(e);
            }
            // A member on another storey stands among that storey's walls.
            else if (rel.Class == IfcClass.RelContainedInSpatialStructure)
                foreach (var e in (r.After?.Related ?? []).Except(r.Before?.Related ?? []))
                    if (Groups.Of(m, e) is { } g && Of(g) != null) placed.Add(g.Id);
        }
        // A member's sketch redrawn: the solid itself did not change.
        foreach (var g in Groups.All(m).Where(g => Of(g) != null).ToList())
            foreach (var id in Groups.MembersOf(m, g.Id).ToList())
                if (m.Get(id) is { } member && DependentsOf(ctx, member).Any(ids.Contains)) Touch(g.Id, id);
        foreach (var source in touched)
        {
            placed.Add(source.Id);
            placed.AddRange(Carry(ctx, source, dirty.GetValueOrDefault(source.Id) ?? []));
        }
        placed.AddRange(NearChangedWalls(ctx, changed));
        foreach (var id in placed.Distinct().ToList())
            if (m.Get(id) is { } g && Of(g) != null)
            {
                StopInnerWalls(ctx, g);
                foreach (var h in hooks) h.Placed(ctx, m.Get(id)!);
            }
        foreach (var c in m.OfClass(Class).ToList()) if (!Instances(m, c.Id).Any()) ctx.Remove(c.Id);
    }

    /// <summary>Whether an element is a member of an instance of a component.</summary>
    static bool InInstance(IModelView m, string id) => Groups.Of(m, id) is { } g && Of(g) != null;

    /// <summary>The members of every apartment type's instances: their inner walls stop in the building as <see cref="StopInnerWalls"/> says, not by <see cref="Joins.Auto"/>.</summary>
    public static IEnumerable<string> UnitMembers(IModelView m) =>
        Groups.All(m).Where(g => IsUnit(m, Of(g))).SelectMany(g => Groups.MembersOf(m, g.Id));

    /// <summary>The instances near a wall of the building this command made, moved or took away (their members may meet it).</summary>
    static IEnumerable<string> NearChangedWalls(ICommandContext ctx, IReadOnlyList<Change<Entity>> changed)
    {
        var m = ctx.Model;
        if (ctx.Services.Get<IPathElements>() is not { } paths) return [];
        var ends = new List<(Vec2 A, Vec2 B)>();
        foreach (var c in changed)
        {
            if (c.After != null && InInstance(m, c.Id)) continue;
            foreach (var e in new[] { c.Before, c.After })
                if (e != null && paths.IsPath(e.Class) && e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray) ends.Add((Joins.Start(e), Joins.End(e)));
        }
        if (ends.Count == 0) return [];
        const double Reach = 1.5;
        var near = new List<string>();
        foreach (var g in Groups.All(m).Where(g => Of(g) != null))
        {
            var points = Groups.MembersOf(m, g.Id).Select(m.Get).OfType<Entity>().SelectMany(PointsOf).ToList();
            if (points.Count == 0) continue;
            var (x0, x1, y0, y1) = (points.Min(p => p.X) - Reach, points.Max(p => p.X) + Reach, points.Min(p => p.Y) - Reach, points.Max(p => p.Y) + Reach);
            if (ends.Any(w => Math.Max(w.A.X, w.B.X) >= x0 && Math.Min(w.A.X, w.B.X) <= x1 && Math.Max(w.A.Y, w.B.Y) >= y0 && Math.Min(w.A.Y, w.B.Y) <= y1)) near.Add(g.Id);
        }
        return near;
    }

    /// <summary>
    /// What a command left of a member's axis, when all it did was take an end along with the building's wall the end stops in:
    /// that end moved along the member's own line, or the wall moved too (its end, carried with the wall, then put back on the
    /// member's line, so a wall slid along itself does not skew it). Not an edit of the type. Anything else (the member turned,
    /// its free end moved, a free end drawn out to the wall) is one.
    /// </summary>
    static bool FollowsLimit(ICommandContext ctx, IEdit edit, Change<Entity> c)
    {
        var m = ctx.Model;
        if (c.Before is not { } before || c.After is not { } after || before.Attrs["start"] is not JsonArray || before.Attrs["end"] is not JsonArray
            || after.Attrs["start"] is not JsonArray || after.Attrs["end"] is not JsonArray) return false;
        if (Groups.Of(m, c.Id) is not { } g || !IsUnit(m, Of(g)) || PathCurve.Of(after).IsArc) return false;
        var (was, now) = (before.CloneAttrs(), after.CloneAttrs());
        foreach (var k in new[] { "start", "end" }) { was.Remove(k); now.Remove(k); }
        if (!JsonNode.DeepEquals(was, now)) return false;
        var line = Joins.End(before) - Joins.Start(before);
        if (line.Length < 1e-9) return false;
        var dir = line.Normalized();
        // Both ends gone the same way: the member itself was moved, whatever moved with it.
        var (byStart, byEnd) = (Joins.Start(after) - Joins.Start(before), Joins.End(after) - Joins.End(before));
        if (byStart.Length > 1e-9 && (byStart - byEnd).Length < 1e-9) return false;
        var moved = false;
        var put = new List<(string Key, Vec2 At)>();
        foreach (var (key, end) in new[] { ("start", 0), ("end", 1) })
        {
            var (from, to) = (end == 0 ? Joins.Start(before) : Joins.End(before), end == 0 ? Joins.Start(after) : Joins.End(after));
            if ((to - from).Length < 1e-9) continue;
            moved = true;
            if (LimitAt(m, edit, c.Id, g.Id, end) is not { } wall) return false;
            if (Math.Abs(Vec2.Cross(dir, to - from)) < 1e-6) continue;
            if (!edit.EntityChanges.Any(x => x.Id == wall.Id)) return false;
            var along = Joins.End(wall) - Joins.Start(wall);
            var sin = Vec2.Cross(dir, along.Normalized());
            if (Math.Abs(sin) < 1e-6) return false;
            put.Add((key, from + dir * (Vec2.Cross(Joins.Start(wall) - from, along.Normalized()) / sin)));
        }
        if (!moved) return false;
        if (put.Count > 0)
        {
            var attrs = after.CloneAttrs();
            foreach (var (key, at) in put) attrs[key] = new JsonArray(at.X, at.Y);
            ctx.Replace(after.With(attrs: attrs));
        }
        return true;
    }

    /// <summary>The wall outside the instance a member's end is joined to, or was until this command: one the join did not just reach, unless that wall changed too (one just drawn or moved).</summary>
    static Entity? LimitAt(IModelView m, IEdit edit, string member, string group, int end)
    {
        var at = end == 0 ? "ATSTART" : "ATEND";
        var changes = edit.RelationChanges;
        var gone = changes.Where(x => x.Before is { Class: IfcClass.RelConnectsPathElements } && x.After == null && x.Before.Touches(member)).Select(x => x.Before!);
        foreach (var r in Joins.Of(m, member).Concat(gone))
        {
            if ((string?)r.Attrs?[r.Relating == member ? "relatingConnection" : "relatedConnection"] != at) continue;
            if (m.Get(Joins.Other(r, member)) is not { } wall || Groups.Of(m, wall.Id)?.Id == group) continue;
            var isNew = changes.Any(x => x.Id == r.Id && x.Before == null);
            if (!isNew || edit.EntityChanges.Any(x => x.Id == wall.Id)) return wall;
        }
        return null;
    }

    /// <summary>
    /// An apartment type's inner walls in their place: each end joined outside the instance and no longer meeting goes free,
    /// a T drawn on a building wall becomes a stop, and each free end stops in the nearest wall of the building on its storey
    /// (not another flat's). Only the member changes; the building's wall is left as it is.
    /// </summary>
    static void StopInnerWalls(ICommandContext ctx, Entity group)
    {
        var m = ctx.Model;
        if (!IsUnit(m, Of(group)) || ctx.Services.Get<IPathElements>() is not { } paths) return;
        var members = Groups.MembersOf(m, group.Id).ToHashSet();
        foreach (var id in members)
        {
            if (m.Get(id) is not { } e || !paths.IsPath(e.Class) || e.Attrs["start"] is not JsonArray || e.Attrs["end"] is not JsonArray) continue;
            foreach (var r in Joins.Of(m, id).ToList())
            {
                var other = Joins.Other(r, id);
                if (members.Contains(other)) continue;
                if (Joins.Compute(m, r, paths, out _) is not { } join) { ctx.RemoveRelation(r.Id); continue; }
                if (r.Related[0] != id || m.Get(other) is not { } wall || InInstance(m, other) || (string?)r.Attrs?["kind"] != "T") continue;
                var tip = join.B.Connection == Connection.AtStart ? join.B.Wall.Start : join.B.Wall.End;
                var mine = join.B.Connection == Connection.AtStart ? Joins.Start(m.Get(id)!) : Joins.End(m.Get(id)!);
                if (!Joins.Stops(r) || (tip - mine).Length > 1e-9) Joins.Join(ctx, paths, wall, m.Get(id)!, JoinStyle.Butt, stop: true);
            }
            if (Spatial.StoreyOf(m, id) is not { } storey) continue;
            var building = m.RelatedOf(storey.Id, IfcClass.RelContainedInSpatialStructure).Where(w => w.Class == e.Class && !InInstance(m, w.Id) && !StoredBodies.Has(w)).ToList();
            foreach (var end in new[] { 0, 1 })
                if (!Joins.JoinedEnds(m, m.Get(id)!)[end]) Joins.Stop(ctx, paths, m.Get(id)!, end, building);
        }
    }

    /// <summary>An instance about to move away: its joins to walls that stay go, so nothing of the building is dragged; found again once it stands (<see cref="StopInnerWalls"/>).</summary>
    public static void Leaving(ICommandContext ctx, IEnumerable<string> groups, IReadOnlySet<string> moving)
    {
        var m = ctx.Model;
        foreach (var g in groups)
        {
            if (m.Get(g) is not { } group || !IsUnit(m, Of(group))) continue;
            var members = Groups.MembersOf(m, g).ToHashSet();
            foreach (var id in members)
                foreach (var r in Joins.Of(m, id).ToList())
                    if (Joins.Other(r, id) is var other && !members.Contains(other) && !moving.Contains(other)) ctx.RemoveRelation(r.Id);
        }
    }

    /// <summary>The other instances brought up to this one: the members that changed made again, under the ids they had; the rest left alone.</summary>
    static List<string> Carry(ICommandContext ctx, Entity source, IReadOnlySet<string> changedMembers)
    {
        var reached = new List<string>();
        var m = ctx.Model;
        var catalog = ctx.Services as ModuleCatalog ?? throw new CommandException("The modules are not at hand.");
        var from = Xf(source);
        var unit = IsUnit(m, Of(source));
        var members = Groups.MembersOf(m, source.Id).ToList();
        foreach (var id in members)
        {
            if (m.Get(id) is not { } e) continue;
            if (unit ? !TakesInUnit(m, e) : !Takes(e))
                throw new CommandException($"„{e.Name}” cannot be part of a component: only Studio solids, floors and rooms can.");
            GiveKey(ctx, e);
            foreach (var hosted in HostedBy(m, id).ToList()) GiveKey(ctx, hosted);
        }
        foreach (var other in Instances(m, Of(source)!).Where(g => g.Id != source.Id).ToList())
        {
            var to = Xf(other);
            var all = Groups.MembersOf(m, other.Id).Select(m.Get).OfType<Entity>().ToList();
            var storey = all.Select(e => Spatial.StoreyOf(m, e.Id)?.Id).FirstOrDefault(s => s != null);
            var keys = members.Select(id => m.Get(id)?.Str(MemberKey)).ToHashSet();
            var there = all.Select(e => e.Str(MemberKey)).ToHashSet();
            // What changed, and what the other instance does not have yet; what it has and this one no longer does goes.
            var carried = members.Where(id => changedMembers.Contains(id) || !there.Contains(m.Get(id)?.Str(MemberKey))).ToList();
            var carriedKeys = carried.Select(id => m.Get(id)?.Str(MemberKey)).ToHashSet();
            var old = all.Where(e => carriedKeys.Contains(e.Str(MemberKey)) || !keys.Contains(e.Str(MemberKey))).ToList();
            if (carried.Count == 0 && old.Count == 0) continue;
            reached.Add(other.Id);
            var result = ElementTransform.Run(ctx, new JsonObject { ["ids"] = new JsonArray([.. carried.Select(x => (JsonNode)x)]), ["op"] = "copy" }, Between(from, to));
            var copies = result.Copies;
            // A join between what was carried and what stayed: the copy joins the member that stayed.
            var stayed = all.Where(e => !old.Contains(e) && e.Str(MemberKey) != null).ToDictionary(e => e.Str(MemberKey)!, e => e.Id);
            foreach (var r in copies.Keys.SelectMany(id => m.RelationsOf(id, IfcClass.RelConnectsPathElements)).DistinctBy(r => r.Id).ToList())
            {
                var ends = r.Related.Append(r.Relating).ToList();
                if (ends.All(copies.ContainsKey) || !ends.All(e => copies.ContainsKey(e) || (m.Get(e)?.Str(MemberKey) is { } k && stayed.ContainsKey(k)))) continue;
                string Target(string e) => copies.TryGetValue(e, out var c) ? c : stayed[m.Get(e)!.Str(MemberKey)!];
                ctx.AddRelation(new Relation(ctx.NewId(), r.Class, Target(r.Relating), [.. r.Related.Select(Target)], r.Attrs?.DeepClone() as JsonObject));
            }
            // On the instance's own storey, with the storey's heights.
            if (storey != null && m.Get(storey) is { } target)
                StoreyMove.Move(ctx, [.. copies.Values.Select(m.Get).OfType<Entity>().Where(e => Spatial.StoreyOf(m, e.Id)?.Id != storey)], target, "storey");
            // Under the ids the instance had, its GlobalIds staying: the copies join it, what it had goes with its sketches.
            ctx.RelateAll(Groups.Rel, other.Id, copies.Values);
            var keep = new Dictionary<string, string>();
            foreach (var (original, copy) in copies)
                if (m.Get(original)?.Str(MemberKey) is { } key && old.FirstOrDefault(e => e.Str(MemberKey) == key) is { } was)
                {
                    keep[copy] = was.Id;
                    // What it hosts keeps its ids too, found by key.
                    var hostedThere = HostedBy(m, was.Id).Where(h => h.Str(MemberKey) != null).ToDictionary(h => h.Str(MemberKey)!, h => h.Id);
                    foreach (var h in HostedBy(m, copy).ToList())
                        if (h.Str(MemberKey) is { } hk && hostedThere.TryGetValue(hk, out var hid)) keep[h.Id] = hid;
                }
            var gone = old.Select(e => e.Id).ToList();
            var sketches = old.SelectMany(e => DependentsOf(ctx, e)).Distinct().ToList();
            catalog.Command("element.delete")!.Handler(ctx, new JsonObject { ["ids"] = new JsonArray([.. gone.Select(x => (JsonNode)x)]) });
            foreach (var s in sketches) if (m.Get(s) != null) ctx.Remove(s);
            // What the copies say of the source's ids (a room naming its door) says it of this instance's.
            var names = new Dictionary<string, string>(keep);
            foreach (var (original, copy) in copies)
            {
                if (keep.TryGetValue(copy, out var kept)) names[original] = kept;
                var hostedHere = HostedBy(m, copy).Where(h => h.Str(MemberKey) != null).ToDictionary(h => h.Str(MemberKey)!, h => keep.GetValueOrDefault(h.Id, h.Id));
                foreach (var h in HostedBy(m, original).ToList())
                    if (h.Str(MemberKey) is { } hk && hostedHere.TryGetValue(hk, out var here)) names[h.Id] = here;
            }
            foreach (var e in members.Select(m.Get).OfType<Entity>())
                if (!names.ContainsKey(e.Id) && e.Str(MemberKey) is { } mk && stayed.TryGetValue(mk, out var stay)) names[e.Id] = stay;
            foreach (var (copy, id) in keep) Rename(ctx, copy, id, names);
            // A copy with nothing to replace still names the source's ids.
            foreach (var copy in copies.Values.Where(c => !keep.ContainsKey(c))) if (m.Get(copy) is { } fresh) Reword(ctx, fresh, names);
            foreach (var hostedId in copies.Values.Select(c => keep.GetValueOrDefault(c, c)).SelectMany(c => HostedBy(m, c).ToList()).Where(h => h.Str("roomOpening") != null).Select(h => h.Id).ToList())
                if (m.Get(hostedId) is { } h) Reword(ctx, h, names);
            // At the instance's own height.
            foreach (var id in copies.Values.Select(c => keep.GetValueOrDefault(c, c)))
            {
                var dz = to[2] - from[2];
                if (Math.Abs(dz) > 1e-9 && m.Get(id) is { } placed) ElementRise.Lift(ctx, placed, dz);
            }
        }
        return reached;
    }

    static void Reword(ICommandContext ctx, Entity e, IReadOnlyDictionary<string, string> names)
    {
        var text = e.Attrs.ToJsonString();
        var next = text;
        foreach (var (a, b) in names) next = next.Replace(a, b, StringComparison.Ordinal);
        if (next != text) ctx.Replace(e.With(attrs: JsonNode.Parse(next)!.AsObject()));
    }

    /// <summary>A copy takes the id of the member it replaces, in its relationships and wherever the instance's members name it.</summary>
    static void Rename(ICommandContext ctx, string from, string to, IReadOnlyDictionary<string, string> names)
    {
        var m = ctx.Model;
        if (m.Get(from) is not { } e) return;
        var relations = m.RelationsOf(from).ToList();
        foreach (var r in relations) ctx.RemoveRelation(r.Id);
        ctx.Remove(from);
        var text = e.Attrs.ToJsonString();
        foreach (var (a, b) in names) text = text.Replace(a, b, StringComparison.Ordinal);
        ctx.Add(new Entity(to, e.Class, e.Name, JsonNode.Parse(text)!.AsObject(), e.PredefinedType));
        foreach (var r in relations)
            ctx.AddRelation(r with { Relating = r.Relating == from ? to : r.Relating, Related = [.. r.Related.Select(x => x == from ? to : x)] });
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Component", [], "#888888", Kind: "resource", Template: false));

        // What is selected made a component: one instance, standing where it is.
        r.Command("component.make", "Make component", (ctx, a) =>
        {
            // A module's own elements or groups: its module says (docs/overrides.md, Driven elements).
            var (elements, groups) = Groups.Expand(ctx.Model, Driven.Rest(Args.Ids(a), Driven.Before(ctx, new DrivenEdit("component.make", Args.Ids(a), a))));
            var unit = (string?)a["kind"] == UnitKind;
            var selected = elements.Select(ctx.Model.Get).OfType<Entity>().ToList();
            // A door selected with its wall comes with it; on its own it would be left in a wall that is not part of the type.
            if (unit && selected.FirstOrDefault(e => Hosts(ctx.Model, e) is { } h && !selected.Any(x => x.Id == h.Id)) is { } alone)
                throw new CommandException($"„{alone.Name}” is in a wall that is not selected: select the wall too, or let a room carry it.");
            var entities = unit ? selected.Where(e => Hosts(ctx.Model, e) == null).ToList() : selected;
            elements = [.. entities.Select(e => e.Id)];
            if (entities.Count == 0) throw new CommandException(unit ? "Select the apartment's walls, rooms and furniture to make an apartment type." : "Select Studio solids, floors or rooms to make a component.");
            if (entities.FirstOrDefault(e => unit ? !TakesInUnit(ctx.Model, e) : !Takes(e)) is { } no)
                throw new CommandException(unit ? $"„{no.Name}” cannot be part of an apartment type." : $"„{no.Name}” cannot be part of a component: only Studio solids, floors and rooms can.");
            if (groups.Select(ctx.Model.Get).OfType<Entity>().Concat(entities.Select(e => Groups.Of(ctx.Model, e.Id)).OfType<Entity>()).FirstOrDefault(g => Of(g) != null) is { } taken)
                throw new CommandException($"„{taken.Name}” is already an instance of a component.");
            var name = (string?)a["name"] is { Length: > 0 } n ? n : NextName(ctx.Model, unit);
            var component = new Entity(ctx.NewId(), Class, name, unit ? new JsonObject { [KindKey] = UnitKind } : []);
            ctx.Add(component);
            var group = Groups.Make(ctx, name, elements) ?? throw new CommandException("Select elements placed on a storey to make a component.");
            Groups.DropEmpty(ctx, groups);
            foreach (var e in entities)
            {
                var attrs = e.CloneAttrs();
                attrs[MemberKey] = ctx.NewId();
                ctx.Replace(e.With(attrs: attrs));
                foreach (var hosted in HostedBy(ctx.Model, e.Id).ToList()) GiveKey(ctx, hosted);
            }
            var at = Middle(ctx, entities);
            var g = group.CloneAttrs();
            g[Key] = component.Id;
            g[XfKey] = Write([at.X, at.Y, 0, 0, 0]);
            ctx.Replace(group.With(attrs: g));
            return new JsonObject { ["id"] = group.Id, ["component"] = component.Id };
        });

        // An instance on its own: a component of its own, the others left as they are.
        r.Command("component.makeUnique", "Make unique", (ctx, a) =>
        {
            if (Driven.Before(ctx, new DrivenEdit("component.makeUnique", Args.Ids(a), a)).Handled.Count > 0) return null;
            var group = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Groups.Class } x && Of(x) != null ? x : throw new CommandException("Select an instance of a component.");
            var from = ctx.Model.Get(Of(group)!);
            var name = $"{from?.Name ?? group.Name} (unique)";
            var component = new Entity(ctx.NewId(), Class, name, from?.Str(KindKey) is { } keptKind ? new JsonObject { [KindKey] = keptKind } : []);
            ctx.Add(component);
            var attrs = group.CloneAttrs();
            attrs[Key] = component.Id;
            ctx.Replace(new Entity(group.Id, group.Class, name, attrs, group.PredefinedType));
            return new JsonObject { ["component"] = component.Id };
        });

        r.Command("component.rename", "Rename component", (ctx, a) =>
        {
            var c = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("There is no such component.");
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A component needs a name.");
            ctx.Replace(new Entity(c.Id, c.Class, name, c.CloneAttrs(), c.PredefinedType));
            foreach (var g in Instances(ctx.Model, c.Id).ToList()) ctx.Replace(new Entity(g.Id, g.Class, name, g.CloneAttrs(), g.PredefinedType));
            return null;
        });

        r.Query("component.list", (q, _) => new JsonArray([.. q.Model.OfClass(Class).OrderBy(c => c.Name).Select(c =>
        {
            var instances = Instances(q.Model, c.Id).ToList();
            return (JsonNode)new JsonObject
            {
                ["id"] = c.Id, ["name"] = c.Name, ["kind"] = c.Str(KindKey),
                ["instances"] = new JsonArray([.. instances.Select(g => (JsonNode)g.Id)]),
                ["members"] = instances.Select(g => Groups.MembersOf(q.Model, g.Id).Count()).FirstOrDefault(),
            };
        })]));
    }

    static Vec2 Middle(ICommandContext ctx, List<Entity> entities)
    {
        var points = entities.SelectMany(PointsOf).ToList();
        return points.Count == 0 ? new Vec2(0, 0) : new Vec2(points.Average(p => p.X), points.Average(p => p.Y));
    }

    /// <summary>The plan points an element's attributes place it by.</summary>
    static IEnumerable<Vec2> PointsOf(Entity e)
    {
        if (e.Attrs["start"] is JsonArray { Count: >= 2 } s0) yield return new Vec2(Json.Number(s0[0]) ?? 0, Json.Number(s0[1]) ?? 0);
        if (e.Attrs["end"] is JsonArray { Count: >= 2 } e0) yield return new Vec2(Json.Number(e0[0]) ?? 0, Json.Number(e0[1]) ?? 0);
        foreach (var key in new[] { "contour", "outline" })
            if (e.Attrs[key] is JsonArray list)
                foreach (var p in list.OfType<JsonArray>().Where(p => p.Count >= 2)) yield return new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0);
        if (e.Attrs["at"] is JsonArray { Count: >= 2 } at) yield return new Vec2(Json.Number(at[0]) ?? 0, Json.Number(at[1]) ?? 0);
    }

    static string NextName(IModelView m, bool unit)
    {
        var taken = m.OfClass(Class).Select(c => c.Name).ToHashSet();
        var word = unit ? "Apartment type" : "Component";
        for (var i = 1; ; i++) if (!taken.Contains($"{word} {i}")) return $"{word} {i}";
    }
}
