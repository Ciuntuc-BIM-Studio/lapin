using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Components (docs/components.md), SketchUp's way, for what stands on its own: Studio solids, floors and rooms drawn by
/// their outline; never walls, whose joins and hosted doors belong to their neighbours. A component is a
/// <c>LapinComponent</c> and its instances are groups (<see cref="Groups"/>) carrying it and where they stand, so they are
/// picked, moved, copied and opened as groups are, and their members stay real elements: plans, quantities, schedules and
/// exports read them like any other. What changes inside one instance is carried to the others in the same undo step, each
/// member keeping its GlobalId (found by <c>componentKey</c>, the same in every instance).
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

    public static string? Of(Entity group) => group.Class == Groups.Class ? group.Str(Key) : null;

    public static IEnumerable<Entity> Instances(IModelView m, string component) => Groups.All(m).Where(g => Of(g) == component);

    /// <summary>Whether an element can be part of a component: what stands on its own.</summary>
    public static bool Takes(Entity e) =>
        !StoredBodies.Has(e) && (e.Class is IfcClass.Slab or IfcClass.Space || e.Str("operation") != null);

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
        var changed = edit.EntityChanges;
        // Instances this command made, moved or took away: their members changed with them.
        var whole = changed.Where(c => (c.After ?? c.Before)?.Class == Groups.Class && (c.After?.Str(Key) != null || c.Before?.Str(Key) != null)
            && (c.Before == null || c.After == null || c.Before.Attrs[XfKey]?.ToJsonString() != c.After.Attrs[XfKey]?.ToJsonString() || c.Before.Str(Key) != c.After.Str(Key))).Select(c => c.Id).ToHashSet();
        var touched = new List<Entity>();
        void Touch(string? groupId)
        {
            if (groupId == null || whole.Contains(groupId) || m.Get(groupId) is not { } g || Of(g) == null) return;
            if (touched.All(x => Of(x) != Of(g))) touched.Add(g);
        }
        var ids = changed.Select(c => c.Id).ToHashSet();
        foreach (var c in changed) if (c.After != null) Touch(Groups.Of(m, c.Id)?.Id);
        // A member joining or leaving shows on the group's relationship.
        foreach (var r in edit.RelationChanges) if ((r.After ?? r.Before)?.Class == Groups.Rel) Touch((r.After ?? r.Before)!.Relating);
        // A member's sketch redrawn: the solid itself did not change.
        foreach (var g in Groups.All(m).Where(g => Of(g) != null && touched.All(x => Of(x) != Of(g))).ToList())
            foreach (var id in Groups.MembersOf(m, g.Id).ToList())
                if (m.Get(id) is { } member && DependentsOf(ctx, member).Any(ids.Contains)) { Touch(g.Id); break; }
        foreach (var source in touched) Carry(ctx, source);
        foreach (var c in m.OfClass(Class).ToList()) if (!Instances(m, c.Id).Any()) ctx.Remove(c.Id);
    }

    /// <summary>The other instances made again from this one, member by member, under the ids they had.</summary>
    static void Carry(ICommandContext ctx, Entity source)
    {
        var m = ctx.Model;
        var catalog = ctx.Services as ModuleCatalog ?? throw new CommandException("The modules are not at hand.");
        var from = Xf(source);
        var members = Groups.MembersOf(m, source.Id).ToList();
        foreach (var id in members)
        {
            if (m.Get(id) is not { } e) continue;
            if (!Takes(e)) throw new CommandException($"„{e.Name}” cannot be part of a component: only Studio solids, floors and rooms can.");
            if (e.Str(MemberKey) == null) { var a = e.CloneAttrs(); a[MemberKey] = ctx.NewId(); ctx.Replace(e.With(attrs: a)); }
        }
        foreach (var other in Instances(m, Of(source)!).Where(g => g.Id != source.Id).ToList())
        {
            var to = Xf(other);
            var old = Groups.MembersOf(m, other.Id).Select(m.Get).OfType<Entity>().ToList();
            var storey = old.Select(e => Spatial.StoreyOf(m, e.Id)?.Id).FirstOrDefault(s => s != null);
            var result = ElementTransform.Run(ctx, new JsonObject { ["ids"] = new JsonArray([.. members.Select(x => (JsonNode)x)]), ["op"] = "copy" }, Between(from, to));
            var copies = result.Copies;
            // On the instance's own storey, with the storey's heights.
            if (storey != null && m.Get(storey) is { } target)
                StoreyMove.Move(ctx, [.. copies.Values.Select(m.Get).OfType<Entity>().Where(e => Spatial.StoreyOf(m, e.Id)?.Id != storey)], target, "storey");
            // Under the ids the instance had, its GlobalIds staying: the copies join it, what it had goes with its sketches.
            ctx.RelateAll(Groups.Rel, other.Id, copies.Values);
            var keep = new Dictionary<string, string>();
            foreach (var (original, copy) in copies)
                if (m.Get(original)?.Str(MemberKey) is { } key && old.FirstOrDefault(e => e.Str(MemberKey) == key) is { } was) keep[copy] = was.Id;
            var gone = old.Select(e => e.Id).ToList();
            var sketches = old.SelectMany(e => DependentsOf(ctx, e)).Distinct().ToList();
            catalog.Command("element.delete")!.Handler(ctx, new JsonObject { ["ids"] = new JsonArray([.. gone.Select(x => (JsonNode)x)]) });
            foreach (var s in sketches) if (m.Get(s) != null) ctx.Remove(s);
            foreach (var (copy, id) in keep) Rename(ctx, copy, id, keep);
            // At the instance's own height.
            foreach (var id in copies.Values.Select(c => keep.GetValueOrDefault(c, c)))
            {
                var dz = to[2] - from[2];
                if (Math.Abs(dz) > 1e-9 && m.Get(id) is { } placed) ElementRise.Lift(ctx, placed, dz);
            }
        }
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
        r.Class(new(Class, "Component", [], "#888888", Kind: "resource"));

        // What is selected made a component: one instance, standing where it is.
        r.Command("component.make", "Make component", (ctx, a) =>
        {
            var (elements, groups) = Groups.Expand(ctx.Model, Args.Ids(a));
            var entities = elements.Select(ctx.Model.Get).OfType<Entity>().ToList();
            if (entities.Count == 0) throw new CommandException("Select Studio solids, floors or rooms to make a component.");
            if (entities.FirstOrDefault(e => !Takes(e)) is { } no)
                throw new CommandException($"„{no.Name}” cannot be part of a component: only Studio solids, floors and rooms can.");
            if (groups.Select(ctx.Model.Get).OfType<Entity>().Concat(entities.Select(e => Groups.Of(ctx.Model, e.Id)).OfType<Entity>()).FirstOrDefault(g => Of(g) != null) is { } taken)
                throw new CommandException($"„{taken.Name}” is already an instance of a component.");
            var name = (string?)a["name"] is { Length: > 0 } n ? n : NextName(ctx.Model);
            var component = new Entity(ctx.NewId(), Class, name, []);
            ctx.Add(component);
            var group = Groups.Make(ctx, name, elements) ?? throw new CommandException("Select elements placed on a storey to make a component.");
            Groups.DropEmpty(ctx, groups);
            foreach (var e in entities)
            {
                var attrs = e.CloneAttrs();
                attrs[MemberKey] = ctx.NewId();
                ctx.Replace(e.With(attrs: attrs));
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
            var group = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Groups.Class } x && Of(x) != null ? x : throw new CommandException("Select an instance of a component.");
            var from = ctx.Model.Get(Of(group)!);
            var name = $"{from?.Name ?? group.Name} (unique)";
            var component = new Entity(ctx.NewId(), Class, name, []);
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
                ["id"] = c.Id, ["name"] = c.Name,
                ["instances"] = new JsonArray([.. instances.Select(g => (JsonNode)g.Id)]),
                ["members"] = instances.Select(g => Groups.MembersOf(q.Model, g.Id).Count()).FirstOrDefault(),
            };
        })]));
    }

    static Vec2 Middle(ICommandContext ctx, List<Entity> entities)
    {
        var points = new List<Vec2>();
        foreach (var e in entities)
        {
            foreach (var key in new[] { "contour", "outline" })
                if (e.Attrs[key] is JsonArray list)
                    points.AddRange(list.OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0)));
            if (e.Attrs["at"] is JsonArray { Count: >= 2 } at) points.Add(new Vec2(Json.Number(at[0]) ?? 0, Json.Number(at[1]) ?? 0));
        }
        return points.Count == 0 ? new Vec2(0, 0) : new Vec2(points.Average(p => p.X), points.Average(p => p.Y));
    }

    static string NextName(IModelView m)
    {
        var taken = m.OfClass(Class).Select(c => c.Name).ToHashSet();
        for (var i = 1; ; i++) if (!taken.Contains($"Component {i}")) return $"Component {i}";
    }
}
