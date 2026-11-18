using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>A level an end may go to: a storey's id, <c>"above"</c> or <c>"below"</c> (the element's own storey's neighbour), and an offset (m).</summary>
public sealed record AttachLevel(string Storey, double Offset);

/// <summary>
/// What <c>element.attachTo</c> asks of one end (docs/plan-attach-to.md §3.1): the bodies it goes to (<see cref="Targets"/>), or
/// a level; a gap from the target's face (<see cref="Offset"/>, m, + away from it); and what the target may do to the element's
/// own end (<see cref="Mode"/>: <c>both</c>, <c>extend</c> only, <c>trim</c> only).
/// </summary>
public sealed record AttachSpec(string End, IReadOnlyList<string> Targets, AttachLevel? Level, double Offset = 0, string Mode = Attachments.Both);

/// <summary>
/// What one end of an element is attached to as it holds it: the targets (the walk for circles reads these), the level, the
/// offset and mode; <see cref="Attached"/> also when it follows bodies it does not name (a wall attached to every roof);
/// <see cref="Extra"/> what its class says besides (a wall's <c>roofTop</c>).
/// </summary>
public sealed record AttachState(string End, IReadOnlyList<string> Targets, AttachLevel? Level, double Offset, string Mode, bool Attached, JsonObject? Extra = null);

/// <summary>
/// A class whose top or bottom can be attached to a body or a level (docs/plan-attach-to.md §3.2), registered once by the
/// module that builds it. It stores the attachment its own way (a wall in <c>roofTop</c> / <c>roofIds</c>, a new class as
/// relations, <see cref="AttachRelations"/>) and its body builder reads it, the targets through <see cref="BodySampler.Sample"/>.
/// Attaching changes only the element's own body and what is measured from it, never what other elements read of it (its
/// envelope, its drawn height): that rule, the command's walk and the sampler's guard keep builds from going round in a circle.
/// </summary>
public interface IAttachable
{
    /// <summary>The classes it attaches, for the tool's selection filter.</summary>
    IReadOnlyList<string> Classes { get; }
    bool Handles(Entity e);
    /// <summary>The ends this element has to attach: <c>top</c>, <c>bottom</c>.</summary>
    IReadOnlyList<string> Ends(Entity e);
    /// <summary>Why the class refuses this attachment (a wall to a wall, a gap it cannot keep); null when it may. The targets exist.</summary>
    string? Refuse(IModelView m, Entity e, AttachSpec spec, IReadOnlyList<Entity> targets);
    /// <summary>Writes the attachment of <paramref name="spec"/>'s end, replacing that end's previous one.</summary>
    void Attach(ICommandContext ctx, Entity e, AttachSpec spec);
    /// <summary>Lets the end go: the element stands at its own end as drawn.</summary>
    void Detach(ICommandContext ctx, Entity e, string end);
    AttachState? StateOf(IModelView m, Entity e, string end);
    /// <summary>
    /// The end as built, worked out now, or as it would be attached so (<paramref name="asIf"/>, the tool's ghost: nothing is
    /// written): what the class can say of it (a wall's profile and top outline, a column's axis length) and its
    /// <c>problems</c>.
    /// </summary>
    JsonObject Result(IModelView m, IServices services, Entity e, string end, AttachSpec? asIf);
}

/// <summary>
/// Attaching an element's top or bottom to bodies or a level, for every class that implements <see cref="IAttachable"/>
/// (docs/plan-attach-to.md, stage A1). Each command is one undo step for any number of elements; an element whose class
/// has no such end refuses the whole command, naming it.
/// <list type="bullet">
/// <item><c>element.attachTo {ids, end: top | bottom, targets: [ids] | level: {storey: id | above | below, offset}, offset?, mode?: both | extend | trim, add?: true}</c>.
/// The targets replace the end's own (<c>add</c>: join them). A target exists, has a body, is not the element, and is not
/// attached back to it through others (the walk names the circle); the element is no reference and no component's member.</item>
/// <item><c>element.detach {ids, end?}</c>: both ends when none is given.</item>
/// <item><c>element.attachments {id | ids}</c>: per element and end what it is attached to, the end as built and its problems;
/// with no id, the classes that attach (<c>classes</c>).</item>
/// <item><c>element.attachPreview {ids, end, targets | level, offset?, mode?}</c>: the end as it would be, for the tool's ghost.</item>
/// </list>
/// </summary>
public static class Attachments
{
    public const string Top = "top", Bottom = "bottom";
    public const string Both = "both", Extend = "extend", Trim = "trim";
    public static readonly string[] Modes = [Both, Extend, Trim];

    public static void Register(IModuleRegistry r)
    {
        r.Command("element.attachTo", "Attach top or bottom", (ctx, a) =>
        {
            var spec = Spec(a);
            var add = a["add"] is JsonValue av && av.TryGetValue<bool>(out var ab) && ab;
            var elements = Elements(ctx.Model, ctx.Services, Args.Ids(a), spec.End);
            foreach (var (e0, x) in elements)
            {
                // Read again: an element named twice, or attached by an earlier one of the list, as it is now.
                var e = ctx.Model.Get(e0.Id)!;
                var mine = spec;
                if (add && spec.Level == null && x.StateOf(ctx.Model, e, spec.End) is { } held)
                    mine = spec with { Targets = [.. held.Targets.Where(t => ctx.Model.Get(t) != null).Concat(spec.Targets).Distinct()] };
                Check(ctx.Model, ctx.Services, e, x, mine);
                x.Attach(ctx, e, mine);
            }
            return new JsonObject { ["ids"] = new JsonArray([.. elements.Select(p => (JsonNode)p.E.Id)]) };
        });

        r.Command("element.detach", "Detach top or bottom", (ctx, a) =>
        {
            var end = (string?)a["end"] is { Length: > 0 } given ? End(given) : null;
            var elements = Elements(ctx.Model, ctx.Services, Args.Ids(a), end);
            foreach (var (e0, x) in elements)
                foreach (var which in end != null ? [end] : x.Ends(e0))
                    x.Detach(ctx, ctx.Model.Get(e0.Id)!, which);
            return new JsonObject { ["ids"] = new JsonArray([.. elements.Select(p => (JsonNode)p.E.Id)]) };
        });

        r.Query("element.attachments", (q, a) =>
        {
            var ids = Args.Ids(a);
            if (ids.Count == 0) return new JsonObject { ["classes"] = new JsonArray([.. ClassesOf(q.Services).Select(c => (JsonNode)c)]) };
            var o = new JsonArray();
            foreach (var id in ids)
            {
                var e = q.Model.Get(id) ?? throw new CommandException($"There is no element {id}.");
                var x = For(q.Services, e);
                var row = new JsonObject { ["id"] = e.Id, ["name"] = e.Name, ["class"] = e.Class, ["attachable"] = x != null };
                if (x != null)
                    row["ends"] = new JsonArray([.. x.Ends(e).Select(end => (JsonNode)Describe(q.Model, q.Services, e, x, end))]);
                o.Add((JsonNode)row);
            }
            return new JsonObject { ["elements"] = o };
        });

        r.Query("element.attachPreview", (q, a) =>
        {
            var spec = Spec(a);
            var o = new JsonArray();
            foreach (var (e, x) in Elements(q.Model, q.Services, Args.Ids(a), spec.End))
            {
                Check(q.Model, q.Services, e, x, spec);
                var row = x.Result(q.Model, q.Services, e, spec.End, spec);
                row["id"] = e.Id;
                row["end"] = spec.End;
                o.Add((JsonNode)row);
            }
            return new JsonObject { ["elements"] = o };
        });
    }

    /// <summary>The service that attaches <paramref name="e"/>'s ends; null when its class has none.</summary>
    public static IAttachable? For(IServices services, Entity e) => services.All<IAttachable>().FirstOrDefault(x => x.Handles(e));

    /// <summary>The classes whose ends attach, in the order their modules registered them.</summary>
    public static IReadOnlyList<string> ClassesOf(IServices services) => [.. services.All<IAttachable>().SelectMany(x => x.Classes).Distinct()];

    static string End(string? end) => end switch
    {
        Top or null or "" => Top,
        Bottom => Bottom,
        _ => throw new CommandException($"„{end}” is no end: an element's top or its bottom."),
    };

    /// <summary>What the command or the preview asks, read and checked for form; the elements and targets are checked by <see cref="Check"/>.</summary>
    static AttachSpec Spec(JsonObject a)
    {
        var end = End((string?)a["end"]);
        var targets = a["targets"] is JsonArray ts ? ts.Select(t => (string?)t).OfType<string>().Distinct().ToList() : [];
        AttachLevel? level = null;
        if (a["level"] is JsonObject lv)
        {
            var storey = (string?)lv["storey"] is { Length: > 0 } s ? s : throw new CommandException("Say which level: a storey, or the storey above or below.");
            level = new AttachLevel(storey, Json.Number(lv["offset"]) ?? 0);
        }
        if (targets.Count == 0 && level == null) throw new CommandException("Say what to attach it to: elements with a body, or a level.");
        if (targets.Count > 0 && level != null) throw new CommandException("An end goes to bodies or to a level, not to both.");
        var offset = Json.Number(a["offset"]) ?? 0;
        if (!double.IsFinite(offset)) throw new CommandException("The offset is a number of metres.");
        var mode = (string?)a["mode"] is { Length: > 0 } m ? m : Both;
        if (!Modes.Contains(mode)) throw new CommandException($"„{mode}” is no mode: both, extend or trim.");
        return new AttachSpec(end, targets, level, offset, mode);
    }

    /// <summary>The elements named, each with the service that attaches it; refused, naming it, when one has no such end (or no end at all).</summary>
    static List<(Entity E, IAttachable X)> Elements(IModelView m, IServices services, IReadOnlyList<string> ids, string? end)
    {
        if (ids.Count == 0) throw new CommandException("Select the elements whose top or bottom to attach.");
        var o = new List<(Entity, IAttachable)>();
        foreach (var id in ids)
        {
            var e = m.Get(id) ?? throw new CommandException($"There is no element {id}.");
            var x = For(services, e);
            if (x == null || (end != null && !x.Ends(e).Contains(end)) || x.Ends(e).Count == 0)
                throw new CommandException($"„{e.Name}” has no {end ?? "top or bottom"} to attach.");
            o.Add((e, x));
        }
        return o;
    }

    /// <summary>Whether <paramref name="e"/> may be attached as <paramref name="spec"/> says; a <see cref="CommandException"/> saying why not.</summary>
    static void Check(IModelView m, IServices services, Entity e, IAttachable x, AttachSpec spec)
    {
        if (StoredBodies.Has(e)) throw new CommandException($"„{e.Name}” is a reference: its body is the file's, and stays so.");
        if (Groups.Of(m, e.Id) is { } g && Components.Of(g) != null)
            throw new CommandException($"„{e.Name}” is a member of a component: its instances stay the same, so one alone is not attached.");
        var targets = new List<Entity>();
        foreach (var id in spec.Targets)
        {
            var t = m.Get(id) ?? throw new CommandException($"There is no element {id}.");
            if (t.Id == e.Id) throw new CommandException($"„{e.Name}” cannot be attached to itself.");
            targets.Add(t);
        }
        if (x.Refuse(m, e, spec, targets) is { } refused) throw new CommandException(refused);
        foreach (var t in targets)
            if (!StoredBodies.Has(t) && services.Get<IGeometryProducers>()?.Geometry(t.Class) == null)
                throw new CommandException($"„{t.Name}” has no body {(spec.End == Top ? "a top" : "a bottom")} could go to.");
        foreach (var t in targets)
            if (PathBack(m, services, t.Id, e.Id) is { } path)
                throw new CommandException($"„{e.Name}” cannot be attached to „{t.Name}”: {Circle(m, path)}.");
    }

    /// <summary>„A” is attached to „B”, which is attached to „C”: the path in words.</summary>
    static string Circle(IModelView m, IReadOnlyList<string> path)
    {
        string N(string id) => $"„{m.Get(id)?.Name ?? id}”";
        var s = $"{N(path[0])} is attached to {N(path[1])}";
        for (var i = 2; i < path.Count; i++) s += $", which is attached to {N(path[i])}";
        return s;
    }

    /// <summary>What <paramref name="id"/>'s ends are attached to, by name: the edges the walk for circles follows.</summary>
    static IEnumerable<string> TargetsOf(IModelView m, IServices services, string id)
    {
        if (m.Get(id) is not { } e || For(services, e) is not { } x) return [];
        return x.Ends(e).SelectMany(end => x.StateOf(m, e, end)?.Targets ?? []).Distinct();
    }

    /// <summary>
    /// The shortest path of attachments from <paramref name="from"/> to <paramref name="to"/> (both in it), or null when there is
    /// none: attaching <paramref name="to"/> to <paramref name="from"/> would then close a circle.
    /// </summary>
    public static IReadOnlyList<string>? PathBack(IModelView m, IServices services, string from, string to)
    {
        var parent = new Dictionary<string, string?> { [from] = null };
        var queue = new Queue<string>([from]);
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            if (at == to)
            {
                var path = new List<string>();
                for (string? p = at; p != null; p = parent[p]) path.Add(p);
                path.Reverse();
                return path;
            }
            foreach (var next in TargetsOf(m, services, at))
                if (parent.TryAdd(next, at)) queue.Enqueue(next);
        }
        return null;
    }

    /// <summary>
    /// The problem of an end that is attached, through its targets, back to its own element (a project edited by a script, an
    /// old file: the command refuses it): it stands at its own end, and says so. Null when it is in no circle.
    /// </summary>
    public static string? CycleProblem(IModelView m, IServices services, Entity e, string end)
    {
        if (For(services, e) is not { } x || x.StateOf(m, e, end) is not { } state) return null;
        foreach (var t in state.Targets)
            if (m.Get(t) is { } target && PathBack(m, services, t, e.Id) != null)
                return $"Its {end} and „{target.Name}” depend on each other: it stands at its own {(end == Top ? "height" : "base")}.";
        return null;
    }

    static JsonObject Describe(IModelView m, IServices services, Entity e, IAttachable x, string end)
    {
        var state = x.StateOf(m, e, end);
        var o = new JsonObject { ["end"] = end, ["attached"] = state?.Attached == true };
        if (state != null)
        {
            o["targets"] = new JsonArray([.. state.Targets.Select(t => (JsonNode)new JsonObject
            {
                ["id"] = t, ["name"] = m.Get(t)?.Name, ["gone"] = m.Get(t) == null,
            })]);
            if (state.Level != null) o["level"] = new JsonObject { ["storey"] = state.Level.Storey, ["offset"] = state.Level.Offset };
            o["offset"] = state.Offset;
            o["mode"] = state.Mode;
            if (state.Extra != null) foreach (var (k, v) in state.Extra) o[k] = v?.DeepClone();
        }
        var result = x.Result(m, services, e, end, null);
        var problems = result["problems"] as JsonArray ?? [];
        result.Remove("problems");
        o["result"] = result;
        o["problems"] = problems;
        return o;
    }
}

/// <summary>
/// An end attached by a relation (docs/plan-attach-to.md §3.4): <c>IfcRelConnectsElements</c>, Relating the target, Related
/// the attached element, its attributes <c>{attach: top | bottom, offset, mode}</c>, named "Lapin attach top". A target deleted
/// takes its relation with it, so nothing dangles. The storage the classes after walls use (decision D2: walls keep theirs).
/// </summary>
public static class AttachRelations
{
    public const string EndKey = "attach";

    /// <summary>The relations attaching <paramref name="id"/>'s <paramref name="end"/>, in the model's order.</summary>
    public static IEnumerable<Relation> Of(IModelView m, string id, string end) =>
        m.RelationsOf(id, IfcClass.RelConnectsElements).Where(r => r.Related.Contains(id) && r.Relating != id && (string?)r.Attrs?[EndKey] == end);

    public static AttachState? State(IModelView m, Entity e, string end)
    {
        var rels = Of(m, e.Id, end).ToList();
        if (rels.Count == 0) return null;
        var first = rels[0].Attrs!;
        return new AttachState(end, [.. rels.Select(r => r.Relating).Distinct()], null, Json.Number(first["offset"]) ?? 0, (string?)first["mode"] ?? Attachments.Both, true);
    }

    /// <summary>The end's relations replaced by one per target of <paramref name="spec"/>.</summary>
    public static void Set(IEdit edit, Entity e, AttachSpec spec)
    {
        Clear(edit, e, spec.End);
        foreach (var t in spec.Targets)
            edit.AddRelation(new Relation(edit.NewId(), IfcClass.RelConnectsElements, t, [e.Id], new JsonObject
            {
                [EndKey] = spec.End, ["offset"] = spec.Offset, ["mode"] = spec.Mode, ["name"] = $"Lapin attach {spec.End}",
            }));
    }

    public static void Clear(IEdit edit, Entity e, string end)
    {
        foreach (var r in Of(edit.Model, e.Id, end).ToList()) edit.RemoveRelation(r.Id);
    }
}
