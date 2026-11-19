using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// A locked angle (decision 739, Revit's padlock on an angular dimension): an angular dimension held on two straight elements
/// (<see cref="DimensionKinds.Held"/>, decision 738) with <c>locked</c> keeps the angle between them at <c>lockAngle</c>
/// (degrees, the smaller angle, recorded when it is locked and when its value is typed). After every command, in its undo step
/// (<see cref="ICommandFollowUp"/>), an element whose direction the command changed (a turn, an end dragged, an align, a
/// transform, a batch) has its partner turned about the point where their axes cross, by <c>element.transform</c>'s rotate with
/// its joins worked out again, so every command keeps the lock, not one. Through chains (A–B, B–C) each element turns at most
/// once; when both of a pair changed and disagree, the one the command did not name (else the one that changed less) follows.
/// A lock that cannot be kept (a cycle that disagrees, a pinned element or one locked to a grid that would have to turn, two
/// elements turned parallel) fails the command with its reason, so nothing changes. A lock whose element is gone is an orphan,
/// as the dimension is; unlocked (the default, every project before), nothing happens.
/// </summary>
public static class AngleLocks
{
    public const string Key = "locked";
    public const string AngleKey = "lockAngle";
    const double Tolerance = 1e-6;

    /// <summary>Whether the dimension is an angle held on two elements with its padlock closed.</summary>
    public static bool IsLocked(Entity? dim) =>
        dim is { Class: IfcClass.Annotation, PredefinedType: "DIMENSION" } && dim.Str("dimKind") == "angular"
        && dim.Attrs["holds"] is JsonArray { Count: 2 } && dim.Attrs[Key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>The held angle's ids, first and second.</summary>
    static (string A, string B) Pair(Entity dim)
    {
        var h = (JsonArray)dim.Attrs["holds"]!;
        return ((string)h[0]!["element"]!, (string)h[1]!["element"]!);
    }

    /// <summary>The angle from the first ray to the second, signed (degrees, counter-clockwise positive); null when it cannot be read.</summary>
    static (double Signed, Vec2 C)? Angle(IModelView m, Entity dim)
    {
        if (DimensionKinds.Held(m, dim) is not { } h) return null;
        var (u, v) = (h.A - h.C, h.B - h.C);
        return (Math.Atan2(Vec2.Cross(u, v), Vec2.Dot(u, v)) * 180 / Math.PI, h.C);
    }

    /// <summary>The smaller angle the dimension reads now (degrees); null when it cannot be read.</summary>
    public static double? Now(IModelView m, Entity dim) => Angle(m, dim) is { } a ? Math.Abs(a.Signed) : null;

    /// <summary>A path element's direction (start → end, unit); null when it has none.</summary>
    static Vec2? Dir(Entity? e)
    {
        if (e?.Attrs["start"] is not JsonArray || e.Attrs["end"] is not JsonArray) return null;
        var d = Args.PointOf(e.Attrs["end"]) - Args.PointOf(e.Attrs["start"]);
        return d.Length < 1e-9 ? null : d.Normalized();
    }

    /// <summary>How far a direction turned (radians, 0…π).</summary>
    static double Turn(Vec2 from, Vec2 to) => Math.Abs(Math.Atan2(Vec2.Cross(from, to), Vec2.Dot(from, to)));

    /// <summary>The ids a command names, anywhere in its arguments (ids, id, wallId…: a batch's steps too).</summary>
    static void Named(JsonNode? n, HashSet<string> into, int depth = 0)
    {
        if (depth > 6) return;
        switch (n)
        {
            case JsonObject o:
                foreach (var (k, v) in o)
                {
                    if ((k == "id" || k.EndsWith("Id", StringComparison.Ordinal)) && v is JsonValue sv && sv.TryGetValue<string>(out var s)) into.Add(s);
                    else if (k == "ids" && v is JsonArray arr) foreach (var x in arr) if (x is JsonValue xv && xv.TryGetValue<string>(out var xs)) into.Add(xs);
                    else Named(v, into, depth + 1);
                }
                break;
            case JsonArray a:
                foreach (var x in a) Named(x, into, depth + 1);
                break;
        }
    }

    static string Deg(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The lock recorded at <paramref name="value"/> (a value typed, a lock closed).</summary>
    public static void Record(ICommandContext ctx, Entity dim, double value)
    {
        var attrs = dim.CloneAttrs();
        attrs[AngleKey] = Math.Round(value, 9);
        ctx.Replace(dim.With(attrs: attrs));
    }

    public static void Register(IModuleRegistry r) => r.Service<ICommandFollowUp>(new FollowUp());

    sealed class FollowUp : ICommandFollowUp
    {
        public JsonNode? After(ICommandContext ctx, IEdit edit, string command, JsonObject args, JsonNode? result)
        {
            var changes = edit.EntityChanges;
            if (changes.Count == 0) return result;
            // Nothing with an axis or a dimension changed: no lock to look at (most commands, without reading the annotations).
            if (!changes.Any(ch => (ch.After ?? ch.Before) is { } e && (e.Attrs["start"] is JsonArray || e.PredefinedType == "DIMENSION"))) return result;
            var m = ctx.Model;
            var locks = m.OfClass(IfcClass.Annotation).Where(IsLocked).ToList();
            if (locks.Count == 0) return result;
            // Each entity's state before the command: its first change's.
            var before = new Dictionary<string, Entity?>();
            foreach (var ch in changes)
                if (!before.ContainsKey(ch.Id)) before[ch.Id] = ch.Before;
            // A lock closed in this command (or one an older caller left without its value): its angle now is the one kept.
            var active = new List<Entity>();
            foreach (var dim in locks)
            {
                var newly = dim.Attrs[AngleKey] is null || before.TryGetValue(dim.Id, out var was) && !IsLocked(was);
                if (newly) { if (Now(m, dim) is { } now) Record(ctx, dim, now); continue; }
                active.Add(m.Get(dim.Id)!);
            }
            if (active.Count == 0) return result;
            var heldIds = active.SelectMany(d => { var (a, b) = Pair(d); return new[] { a, b }; }).ToHashSet();
            if (!heldIds.Any(before.ContainsKey)) return result;
            // Each held element's direction before the command.
            var dir0 = new Dictionary<string, Vec2?>();
            foreach (var id in heldIds) dir0[id] = Dir(before.TryGetValue(id, out var b0) ? b0 : m.Get(id));
            bool Turned(string id) => dir0[id] is { } d0 && Dir(m.Get(id)) is { } d1 && Turn(d0, d1) > 1e-9;
            var named = new HashSet<string>();
            Named(args, named);
            var turnedByLock = new HashSet<string>();
            int State(string id) => turnedByLock.Contains(id) ? 2 : Turned(id) ? 1 : 0;
            string NameOf(string id) => m.Get(id)?.Name is { Length: > 0 } n ? n : id;

            for (var pass = 0; pass <= active.Count * 2 + 2; pass++)
            {
                var progress = false;
                foreach (var dim in active)
                {
                    var (a, b) = Pair(dim);
                    if (m.Get(a) == null || m.Get(b) == null) continue;   // orphaned, as before
                    var (sa, sb) = (State(a), State(b));
                    if (sa == 0 && sb == 0) continue;
                    var lockAt = dim.Num(AngleKey);
                    if (Angle(m, dim) is not { } now)
                        throw new CommandException($"The angle between „{NameOf(a)}” and „{NameOf(b)}” is locked at {Deg(lockAt)}°: this would turn them parallel. Unlock it first.");
                    var target = (now.Signed < 0 ? -1 : 1) * lockAt;
                    if (Math.Abs(target - now.Signed) < Tolerance) continue;
                    string follower;
                    if (sa == 0) follower = a;
                    else if (sb == 0) follower = b;
                    else if (sa == 2 && sb == 2)
                        throw new CommandException($"The locked angles cannot all be kept: „{NameOf(a)}” and „{NameOf(b)}” would have to be turned twice (a loop of locks that disagree). Unlock one of them first.");
                    else if (sa == 2) follower = b;
                    else if (sb == 2) follower = a;
                    else if (named.Contains(a) != named.Contains(b)) follower = named.Contains(a) ? b : a;
                    else
                    {
                        var (ta, tb) = (Turn(dir0[a]!.Value, Dir(m.Get(a))!.Value), Turn(dir0[b]!.Value, Dir(m.Get(b))!.Value));
                        follower = ta < tb - 1e-9 ? a : b;
                    }
                    var f = m.Get(follower)!;
                    if (Pins.Is(f))
                        throw new CommandException($"The angle between „{NameOf(a)}” and „{NameOf(b)}” is locked at {Deg(lockAt)}°: „{NameOf(follower)}” would have to turn, but it is pinned{(f.Attrs[Pins.LockedToKey] != null ? " (locked to a grid or a reference plane)" : "")}. Unlock the angle or unpin it first.");
                    // Turning the second changes the signed angle by as much; turning the first, by as much the other way.
                    var by = follower == b ? target - now.Signed : now.Signed - target;
                    var t = ElementTransform.Run(ctx, new JsonObject { ["ids"] = new JsonArray(follower), ["op"] = "rotate", ["center"] = new JsonArray(now.C.X, now.C.Y), ["angle"] = by });
                    ElementTransform.JoinWhatMeets(ctx, t);
                    turnedByLock.Add(follower);
                    // Its own direction now counts from where it stood before the command (it may have been there already).
                    if (!dir0.ContainsKey(follower)) dir0[follower] = Dir(f);
                    progress = true;
                }
                if (!progress) break;
            }
            // Every lock that moved is kept, or the command does not happen.
            foreach (var dim in active)
            {
                var (a, b) = Pair(dim);
                if (m.Get(a) == null || m.Get(b) == null || State(a) == 0 && State(b) == 0) continue;
                if (Now(m, dim) is not { } v || Math.Abs(v - dim.Num(AngleKey)) > 1e-4)
                    throw new CommandException($"The angle between „{NameOf(a)}” and „{NameOf(b)}” is locked at {Deg(dim.Num(AngleKey))}° and cannot be kept by this change. Unlock it first.");
            }
            return result;
        }
    }
}
