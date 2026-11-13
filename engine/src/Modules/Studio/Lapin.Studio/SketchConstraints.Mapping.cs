using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// Constraints carried through the commands that change a sketch's shapes (§4.2, risk 1 of §7): each ref found again on what
/// replaced its shape, by geometry, or the constraint dropped with the reason in the command's result, never left wrong.
///
/// <list type="bullet">
/// <item>TRIM, EXTEND, FILLET, CHAMFER, JOIN, EXPLODE, delete (<see cref="Mode.Geometric"/>): a point ref goes to the point of the
/// changed or new shapes that stands where it stood (a trimmed line's kept end, the piece of a split line holding it, a joined
/// polyline's vertex, an exploded polyline's line end); an edge ref to the straight edge lying along it (the same id first); a circle
/// or arc to the one of the same centre and radius. An end that moved (trimmed, extended, filleted away) and a shape deleted drop
/// what held them; a dimension of an edge's length (distance, distanceX/Y of a line, equal, midpoint) drops when its edge changed
/// length; two refs become one (a coincidence welded by JOIN) drop it. EXPLODE keeps a constrained polyline's corners together
/// with coincidences between its new lines.</item>
/// <item>OFFSET makes a new, free shape: the original's constraints stay where they are, the copy has none.</item>
/// <item>Move, rotate, mirror, scale (<see cref="Mode.Transform"/>): the ids stay; an arc mirrored swaps its start and end; a constraint
/// whose shapes all moved follows the turn: horizontal and vertical (and distanceX/Y) swap under a quarter turn and drop under any
/// other, a one-line angle turns, a two-line angle changes sign in a mirror, lengths scale (a formula that would no longer hold
/// drops). Moving a whole sketch keeps all its constraints.</item>
/// <item>Copies (drafting.transform and element.transform with copy, a Studio solid copied with its sketch): the constraints among
/// the copied shapes only are copied onto the copies, carried as a move carries them.</item>
/// <item>A face's region pushed or pulled drafts its outline on the face's plane: tied to the constrained sketch by coincidences
/// (and points on edges) where its corners stand on the sketch's shapes (<see cref="TieRegion"/>).</item>
/// <item>A grip edit (updateDraft) or any other command: a constraint is kept while its shapes keep their sort and their number of
/// points, else found again by geometry as above.</item>
/// </list>
/// <para>All of it runs after the command, in its undo step (<see cref="FollowUp"/>, the platform's <c>ICommandFollowUp</c>), from the
/// command's own changes: the drafting commands know nothing of constraints.</para>
/// </summary>
public static partial class SketchConstraints
{
    const double Tol = 1e-6;

    internal enum Mode { Geometric, Reshape, Transform }

    /// <summary>A transform: where points go (<see cref="Placement2D"/>), by how much lengths scale, whether it copies, and the copies made.</summary>
    internal sealed record Carry(Placement2D T, double Scale, bool Copy, IReadOnlyList<string> Inputs, IReadOnlyDictionary<string, string>? Copies);

    /// <summary>The constrained sketches before a command: each view's shapes (with names, to pair copies) and constraints.</summary>
    internal sealed record Snapshot(Dictionary<string, Dictionary<string, (Shape Shape, string Name)>> Shapes, Dictionary<string, List<JsonObject>> Constraints);

    /// <summary>The constrained sketches as they were before the command: their shapes now, with those the command changed, removed or made taken back from its changes.</summary>
    internal static Snapshot? Take(IModelView m, IEdit edit)
    {
        var carriers = m.OfClass(Class).ToList();
        if (carriers.Count == 0) return null;
        static bool IsDraft(Entity? e) => e is { Class: IfcClass.Annotation, PredefinedType: Drafts.Predefined };
        var changes = edit.EntityChanges.Where(c => IsDraft(c.Before) || IsDraft(c.After)).ToList();
        if (changes.Count == 0) return null;
        var shapes = new Dictionary<string, Dictionary<string, (Shape, string)>>();
        var cs = new Dictionary<string, List<JsonObject>>();
        foreach (var carrier in carriers)
        {
            var view = carrier.Str("viewId") ?? "";
            var d = new Dictionary<string, (Shape, string)>();
            foreach (var e in Drafts.On(m, view)) if (e.Attrs["hatch"] == null && Drafts.ShapeOf(e) is { } s) d[e.Id] = (s, e.Name);
            foreach (var c in changes)
            {
                d.Remove(c.Id);
                if (IsDraft(c.Before) && c.Before!.Str("viewId") == view && c.Before.Attrs["hatch"] == null && Drafts.ShapeOf(c.Before) is { } s) d[c.Id] = (s, c.Before.Name);
            }
            shapes[view] = d;
            cs[view] = ListOf(carrier);
        }
        return new Snapshot(shapes, cs);
    }

    static string Key(Shape s) => s.ToJson().ToJsonString();

    /// <summary>Each constrained sketch the command changed: its constraints carried onto what its shapes are now.</summary>
    internal static void Remap(ICommandContext ctx, Snapshot before, Mode mode, Carry? carry = null, bool lenient = false)
    {
        // What the solve wrote in this command is its own (C1): never taken for a user's edit, so it is not solved again.
        var solved = Report(ctx).Solved;
        foreach (var (view, was) in before.Shapes)
        {
            var now = new Dictionary<string, (Shape Shape, string Name)>();
            var order = new List<string>();
            foreach (var e in Drafts.On(ctx.Model, view))
                if (e.Attrs["hatch"] == null && Drafts.ShapeOf(e) is { } s) { now[e.Id] = (s, e.Name); order.Add(e.Id); }
            var removed = was.Keys.Where(id => !now.ContainsKey(id)).ToHashSet();
            var changed = was.Keys.Where(id => now.TryGetValue(id, out var n) && Key(n.Shape) is var k && k != Key(was[id].Shape)
                && !(solved.TryGetValue(id, out var mine) && mine == k)).ToHashSet();
            var added = order.Where(id => !was.ContainsKey(id)).ToList();
            var list = before.Constraints.GetValueOrDefault(view) ?? [];
            if (carry is { Copy: true }) CopyWithin(ctx, view, list, Pairs(carry, was, now, added), carry);
            if (removed.Count == 0 && changed.Count == 0) continue;
            // What a ref may move to: the changed and new shapes (never one the command left alone).
            var candidates = order.Where(id => changed.Contains(id) || added.Contains(id)).ToList();
            var kept = new List<JsonObject>();
            foreach (var c in ListOf(CarrierOf(ctx.Model, view)))
            {
                if (!Refs(c).Any(r => removed.Contains(r.Shape) || changed.Contains(r.Shape))) { kept.Add(c); continue; }
                var (mapped, reason) = mode switch
                {
                    Mode.Transform when carry != null => Carried(c, carry, changed, r => (r.Shape, Swap(was, r, carry.T))),
                    Mode.Reshape => Reshaped(c, was, now, removed, changed, candidates),
                    _ => Geometric(c, was, now, removed, changed, candidates),
                };
                // A dimension's place follows a move, turn, mirror or scale of all its shapes; else it keeps its numbers.
                if (mapped != null && mode == Mode.Transform && carry != null && Refs(c).All(r => changed.Contains(r.Shape)))
                    CarryPlace(c, mapped, id => was.TryGetValue(id, out var w) ? w.Shape : null, id => now.TryGetValue(id, out var n) ? n.Shape : null, carry.T, carry.Scale);
                if (mapped != null) kept.Add(mapped);
                else Report(ctx).Dropped.Add(Dropped(c, reason ?? "it no longer holds"));
            }
            if (mode == Mode.Geometric) KeepCorners(ctx, view, was, now, candidates, list, kept);
            Write(ctx, view, kept);
            // The user's edit as drag goals: the shapes it changed, from where they were. A whole sketch moved, turned or scaled
            // keeps its constraints carried and needs no solve.
            var whole = mode == Mode.Transform && kept.SelectMany(Refs).All(r => changed.Contains(r.Shape));
            AfterChange(ctx, view, changed.ToDictionary(id => id, id => was[id].Shape), lenient, solve: !whole);
        }
    }

    // ------------------------------------------------------------ by geometry

    static (JsonObject?, string?) Geometric(JsonObject c, Dictionary<string, (Shape Shape, string Name)> was, Dictionary<string, (Shape Shape, string Name)> now,
        HashSet<string> removed, HashSet<string> changed, List<string> candidates)
    {
        var kind = (string)c["kind"]!;
        var refs = new List<(string Shape, string Part)>();
        var reversed = new List<bool>();
        var lengthChanged = false;
        foreach (var (sid, part) in Refs(c))
        {
            if (!removed.Contains(sid) && !changed.Contains(sid)) { refs.Add((sid, part)); reversed.Add(false); continue; }
            var bs = was[sid].Shape;
            // Its own shape first, then the others the command changed or made.
            var order = (now.ContainsKey(sid) ? new[] { sid } : []).Concat(candidates.Where(x => x != sid)).ToList();
            var gone = removed.Contains(sid) && candidates.Count == 0;
            if (PointOf(bs, part) is { } q)
            {
                var hit = order.SelectMany(id => PointParts(now[id].Shape).Select(pp => (id, pp)))
                    .FirstOrDefault(x => PointOf(now[x.id].Shape, x.pp) is { } at && (at - q).Length <= Tol);
                if (hit.id == null) return (null, gone ? "its shape was deleted" : "the point it held is gone");
                refs.Add((hit.id, hit.pp)); reversed.Add(false);
            }
            else if (SegmentOf(bs, part) is (var a, var b))
            {
                if (FindEdge(a, b, order, now) is not (var id, var ep, var rev, var changedLength)) return (null, gone ? "its shape was deleted" : "its edge is gone");
                refs.Add((id, ep)); reversed.Add(rev);
                lengthChanged |= changedLength;
            }
            else if (part == "whole" && bs is Circle or Arc)
            {
                var (cc, rr) = bs is Circle ci ? (ci.C, ci.R) : (((Arc)bs).C, ((Arc)bs).R);
                var id = order.FirstOrDefault(x => now[x].Shape switch
                {
                    Circle o => (o.C - cc).Length <= Tol && Math.Abs(o.R - rr) <= Tol,
                    Arc o => (o.C - cc).Length <= Tol && Math.Abs(o.R - rr) <= Tol,
                    _ => false,
                });
                if (id == null) return (null, gone ? "its shape was deleted" : "its circle or arc is gone");
                refs.Add((id, "whole")); reversed.Add(false);
            }
            else
            {
                // A polyline whole: itself while it is one.
                if (!now.TryGetValue(sid, out var n) || n.Shape is not Polyline) return (null, gone || !now.ContainsKey(sid) ? "its shape was deleted" : "its polyline is gone");
                refs.Add((sid, part)); reversed.Add(false);
            }
        }
        if (refs.Distinct().Count() < refs.Count) return (null, kind == "coincident" ? "the edit made its points one" : "its references became one");
        var single = refs.Count == 1;
        if (lengthChanged && (kind is "equal" or "midpoint" || kind is "distance" or "distanceX" or "distanceY" && single))
            return (null, "its edge changed length");
        var o = (JsonObject)c.DeepClone();
        o["refs"] = RefsJson(refs);
        // An edge that now runs the other way: what is measured along it changes sign or turns by a half turn.
        if (reversed.Any(x => x))
        {
            if (kind is "distanceX" or "distanceY" && single) Negate(o);
            if (kind == "angle" && reversed.Count(x => x) % 2 == 1) Turn(o, 180);
        }
        return (o, null);
    }

    /// <summary>The straight edge along a segment: the same segment first (either way), else one lying along it on the same shape, else the longest of the others; whether it runs the other way and whether its length changed.</summary>
    static (string Id, string Part, bool Reversed, bool LengthChanged)? FindEdge(Vec2 a, Vec2 b, List<string> order, Dictionary<string, (Shape Shape, string Name)> now)
    {
        var edges = order.SelectMany(id => EdgeParts(now[id].Shape).Select(ep => (id, ep, seg: SegmentOf(now[id].Shape, ep)!.Value))).ToList();
        foreach (var (id, ep, seg) in edges)
        {
            if ((seg.A - a).Length <= Tol && (seg.B - b).Length <= Tol) return (id, ep, false, false);
            if ((seg.A - b).Length <= Tol && (seg.B - a).Length <= Tol) return (id, ep, true, false);
        }
        var d = b - a;
        var len = d.Length;
        if (len < Tol) return null;
        var u = d * (1 / len);
        double Off(Vec2 p) => Math.Abs(u.X * (p.Y - a.Y) - u.Y * (p.X - a.X));
        double Along(Vec2 p) => Vec2.Dot(p - a, u);
        (string, string, bool, bool)? best = null;
        var bestOverlap = 0.0;
        foreach (var (id, ep, seg) in edges)
        {
            if (Off(seg.A) > Tol || Off(seg.B) > Tol) continue;
            var (t0, t1) = (Along(seg.A), Along(seg.B));
            var overlap = Math.Min(Math.Max(t0, t1), len) - Math.Max(Math.Min(t0, t1), 0);
            if (overlap <= Tol) continue;
            // The same shape wins over any other; among the others the longest overlap.
            var score = overlap + (id == order[0] ? 1e9 : 0);
            if (best == null || score > bestOverlap) { best = (id, ep, t1 < t0, Math.Abs((seg.B - seg.A).Length - len) > Tol); bestOverlap = score; }
        }
        return best;
    }

    /// <summary>A grip edit (updateDraft): kept while the shape is the same sort with as many points; else found again by geometry.</summary>
    static (JsonObject?, string?) Reshaped(JsonObject c, Dictionary<string, (Shape Shape, string Name)> was, Dictionary<string, (Shape Shape, string Name)> now,
        HashSet<string> removed, HashSet<string> changed, List<string> candidates)
    {
        static int Count(Shape s) => s is Polyline p ? p.Pts.Count * (p.Closed ? -1 : 1) : 0;
        var same = Refs(c).All(r => !removed.Contains(r.Shape) && (!changed.Contains(r.Shape) || was[r.Shape].Shape.GetType() == now[r.Shape].Shape.GetType() && Count(was[r.Shape].Shape) == Count(now[r.Shape].Shape)));
        return same ? (c, null) : Geometric(c, was, now, removed, changed, candidates);
    }

    /// <summary>
    /// An exploded polyline that had constraints: its new lines kept meeting at its corners, a coincidence each (they were one
    /// shape; without them a solve could pull the corners apart).
    /// </summary>
    static void KeepCorners(ICommandContext ctx, string view, Dictionary<string, (Shape Shape, string Name)> was, Dictionary<string, (Shape Shape, string Name)> now,
        List<string> candidates, List<JsonObject> before, List<JsonObject> kept)
    {
        foreach (var (id, (shape, _)) in was)
        {
            if (shape is not Polyline p || now.TryGetValue(id, out var n) && n.Shape is Polyline) continue;
            if (!before.Any(c => Refs(c).Any(r => r.Shape == id))) continue;
            var lines = candidates.Where(x => now[x].Shape is Line).ToList();
            string? Starting(Vec2 a, Vec2 b) => lines.FirstOrDefault(x => now[x].Shape is Line l && (l.A - a).Length <= Tol && (l.B - b).Length <= Tol);
            var count = p.Pts.Count;
            for (var i = p.Closed ? 0 : 1; i < (p.Closed ? count : count - 1); i++)
            {
                var prev = p.Pts[(i - 1 + count) % count];
                var at = p.Pts[i];
                var next = p.Pts[(i + 1) % count];
                if (Starting(prev, at) is not { } inLine || Starting(at, next) is not { } outLine || inLine == outLine) continue;
                var cid = ctx.NewId();
                kept.Add(new JsonObject { ["id"] = cid, ["kind"] = "coincident", ["refs"] = RefsJson([(inLine, "end"), (outLine, "start")]), ["driving"] = true });
                Report(ctx).Added.Add(cid);
            }
        }
    }

    // ------------------------------------------------------------ moved, turned, mirrored, scaled

    /// <summary>An arc's start and end swap in a mirror (Lapin keeps arcs counter-clockwise).</summary>
    static string Swap(Dictionary<string, (Shape Shape, string Name)> was, (string Shape, string Part) r, Placement2D t) =>
        t.Mirrored && was.TryGetValue(r.Shape, out var w) && w.Shape is Arc ? r.Part switch { "start" => "end", "end" => "start", _ => r.Part } : r.Part;

    /// <summary>
    /// A constraint carried by a transform (or its copy): its refs mapped; when all its shapes moved, what depends on the view's
    /// axes, angles and lengths carried too, or the reason it can no longer hold.
    /// </summary>
    static (JsonObject?, string?) Carried(JsonObject c, Carry carry, IReadOnlySet<string> moved, Func<(string Shape, string Part), (string, string)> map)
    {
        var o = (JsonObject)c.DeepClone();
        var refs = Refs(c).ToList();
        o["refs"] = RefsJson(refs.Select(map));
        if (!refs.All(r => moved.Contains(r.Shape))) return (o, null);
        var t = carry.T;
        var kind = (string)c["kind"]!;
        var ex = new Vec2(1, 0);
        var ey = new Vec2(0, 1);
        // Where an axis goes: ±x, ±y, or neither.
        static (char Axis, int Sign) Along(Vec2 d) =>
            Math.Abs(Math.Abs(d.X) - 1) < 1e-9 ? ('x', Math.Sign(d.X)) : Math.Abs(Math.Abs(d.Y) - 1) < 1e-9 ? ('y', Math.Sign(d.Y)) : ('-', 0);
        var formula = (string?)c["formula"];
        switch (kind)
        {
            case "horizontal" or "vertical":
            {
                var (axis, _) = Along(t.Dir(kind == "horizontal" ? ex : ey));
                if (axis == '-') return (null, "turned off the view's axes");
                o["kind"] = axis == 'x' ? "horizontal" : "vertical";
                return (o, null);
            }
            case "distanceX" or "distanceY" when refs.Count == 1 && !refs[0].Part.StartsWith("edge:", StringComparison.Ordinal) && refs[0].Part != "whole":
            {
                // A point's distance from the sketch's origin: the line it holds the point on, carried.
                var v = Json.Number(c["value"]);
                var x = kind == "distanceX";
                if (v == null)
                {
                    // A formula holds while the line stays where it was: no shift, turn or scale across it.
                    double Across(Vec2 p) => x ? p.X : p.Y;
                    var still = Math.Abs(Across(t.Point(default))) < 1e-9 && Math.Abs(Across(t.Point(x ? ex : ey)) - 1) < 1e-9 && Along(t.Dir(x ? ey : ex)).Axis == (x ? 'y' : 'x');
                    return still ? (o, null) : (null, "moved: its formula no longer holds");
                }
                var on = t.Point(x ? new Vec2(v.Value, 0) : new Vec2(0, v.Value));
                var (axis, _) = Along(t.Dir(x ? ey : ex));
                if (axis == '-') return (null, "turned off the view's axes");
                o["kind"] = axis == 'y' ? "distanceX" : "distanceY";
                o["value"] = Math.Round(axis == 'y' ? on.X : on.Y, 9);
                return (o, null);
            }
            case "distanceX" or "distanceY":
            {
                var (axis, sign) = Along(t.Dir(kind == "distanceX" ? ex : ey));
                if (axis == '-') return (null, "turned off the view's axes");
                o["kind"] = axis == 'x' ? "distanceX" : "distanceY";
                if (formula != null)
                {
                    if (carry.Scale != 1) return (null, "scaled: its formula no longer holds");
                    if (sign < 0) Negate(o);
                }
                else if (Json.Number(c["value"]) is { } v) o["value"] = Math.Round(v * sign * carry.Scale, 9);
                return (o, null);
            }
            case "distance" or "radius" or "diameter":
                if (carry.Scale == 1) return (o, null);
                if (formula != null) return (null, "scaled: its formula no longer holds");
                if (Json.Number(c["value"]) is { } len) o["value"] = Math.Round(len * carry.Scale, 9);
                return (o, null);
            case "angle":
            {
                // One line: its angle from the x axis turns with it (a mirror reflects it); two: a mirror changes the sign.
                var single = refs.Count == 1;
                var turn = Math.Atan2(t.Dir(ex).Y, t.Dir(ex).X) * 180 / Math.PI;
                if (single && t.Mirrored)
                {
                    if (formula != null) return (null, "mirrored: its formula no longer holds");
                    if (Json.Number(c["value"]) is { } v) o["value"] = Angle(2 * t.Axis * 180 / Math.PI - v);
                    return (o, null);
                }
                if (single && Math.Abs(Angle(turn)) > 1e-9)
                {
                    if (formula != null) return (null, "turned: its formula no longer holds");
                    Turn(o, turn);
                    return (o, null);
                }
                if (!single && t.Mirrored) Negate(o);
                return (o, null);
            }
        }
        return (o, null);
    }

    static void Negate(JsonObject o)
    {
        if ((string?)o["formula"] is { } f) o["formula"] = $"-({f})";
        else if (Json.Number(o["value"]) is { } v) o["value"] = (string?)o["kind"] == "angle" ? Angle(-v) : -v;
    }

    static void Turn(JsonObject o, double deg)
    {
        if ((string?)o["formula"] is { } f) o["formula"] = $"({f}) + {deg.ToString("R", CultureInfo.InvariantCulture)}";
        else if (Json.Number(o["value"]) is { } v) o["value"] = Angle(v + deg);
    }

    // ------------------------------------------------------------ copies

    /// <summary>Each copied shape's copy: as the command said (element.transform), else the new shapes paired with the shapes given, in order, by name.</summary>
    static Dictionary<string, string> Pairs(Carry carry, Dictionary<string, (Shape Shape, string Name)> was, Dictionary<string, (Shape Shape, string Name)> now, List<string> added)
    {
        var o = new Dictionary<string, string>();
        if (carry.Copies != null)
        {
            foreach (var (from, to) in carry.Copies) if (was.ContainsKey(from) && now.ContainsKey(to)) o[from] = to;
            return o;
        }
        var left = carry.Inputs.Where(was.ContainsKey).Distinct().ToList();
        foreach (var id in added)
        {
            var i = left.FindIndex(x => was[x].Name == now[id].Name);
            if (i < 0) continue;
            o[left[i]] = id;
            left.RemoveAt(i);
        }
        return o;
    }

    /// <summary>The constraints among copied shapes only, copied onto the copies (as a move carries them).</summary>
    static void CopyWithin(ICommandContext ctx, string view, List<JsonObject> before, Dictionary<string, string> pairs, Carry carry)
    {
        if (pairs.Count == 0) return;
        var copies = new List<JsonObject>();
        foreach (var c in before)
        {
            if (!Refs(c).All(r => pairs.ContainsKey(r.Shape))) continue;
            var arcs = Refs(c).Where(r => ctx.Model.Get(r.Shape) is { } e && Drafts.ShapeOf(e) is Arc).Select(r => r.Shape).ToHashSet();
            var (copy, reason) = Carried(c, carry, pairs.Keys.ToHashSet(), r =>
                (pairs[r.Shape], carry.T.Mirrored && arcs.Contains(r.Shape) ? r.Part switch { "start" => "end", "end" => "start", _ => r.Part } : r.Part));
            if (copy == null) { Report(ctx).Dropped.Add(Dropped(c, $"not copied: {reason}")); continue; }
            Shape? InModel(string id) => ctx.Model.Get(id) is { } e ? Drafts.ShapeOf(e) : null;
            CarryPlace(c, copy, InModel, InModel, carry.T, carry.Scale);
            copy["id"] = ctx.NewId();
            copies.Add(copy);
        }
        if (copies.Count == 0) return;
        var list = ListOf(CarrierOf(ctx.Model, view));
        list.AddRange(copies);
        Write(ctx, view, list);
        Report(ctx).Copied += copies.Count;
    }

    /// <summary>A Studio solid copied with its sketch on a plan (<see cref="StudioPlacement"/>): the sketch's constraints among the copied shapes, onto the copies.</summary>
    internal static void CopyAmong(ICommandContext ctx, IReadOnlyDictionary<string, string> map, Placement2D t)
    {
        foreach (var view in map.Keys.Select(id => ctx.Model.Get(id)?.Str("viewId")).OfType<string>().Distinct().ToList())
        {
            var list = ListOf(CarrierOf(ctx.Model, view));
            if (list.Count == 0) continue;
            CopyWithin(ctx, view, list, map.ToDictionary(kv => kv.Key, kv => kv.Value), new Carry(t, 1, true, [], map));
        }
    }

    // ------------------------------------------------------------ a face's region

    /// <summary>
    /// A region's outline drafted on a face's plane by a push or pull (decision 375) tied to the plane's constrained sketch: each
    /// corner standing on a shape's point is coincident with it, one on a shape's edge or curve lies on it, so the region follows
    /// the sketch once it is solved. Nothing when the plane's sketch has no constraints.
    /// </summary>
    internal static void TieRegion(ICommandContext ctx, string? viewId, string? outlineId)
    {
        if (viewId == null || outlineId == null || CarrierOf(ctx.Model, viewId) == null) return;
        if (ctx.Model.Get(outlineId) is not { } outline || Drafts.ShapeOf(outline) is not Polyline region) return;
        var others = Drafts.On(ctx.Model, viewId).Where(e => e.Id != outlineId && e.Attrs["hatch"] == null && Drafts.ShapeOf(e) != null).Select(e => (e.Id, Shape: Drafts.ShapeOf(e)!)).ToList();
        var list = ListOf(CarrierOf(ctx.Model, viewId));
        for (var i = 0; i < region.Pts.Count; i++)
        {
            var q = region.Pts[i];
            var me = (outlineId, $"vertex:{i}");
            (string Id, string Part, string Kind)? tie = null;
            foreach (var (id, s) in others)
                foreach (var pp in PointParts(s))
                    if (tie == null && PointOf(s, pp) is { } at && (at - q).Length <= Tol) tie = (id, pp, "coincident");
            if (tie == null)
                foreach (var (id, s) in others)
                {
                    if (tie != null) break;
                    foreach (var ep in EdgeParts(s))
                        if (SegmentOf(s, ep) is (var a, var b) && Geo.NearestOnSegment(q, a, b).Dist <= Tol) { tie = (id, ep, "pointOn"); break; }
                    if (tie == null && s is Circle ci && Math.Abs((q - ci.C).Length - ci.R) <= Tol) tie = (id, "whole", "pointOn");
                    if (tie == null && s is Arc ar && Math.Abs((q - ar.C).Length - ar.R) <= Tol && Geo.AngleInArc(Math.Atan2(q.Y - ar.C.Y, q.X - ar.C.X), ar.Start, ar.End)) tie = (id, "whole", "pointOn");
                }
            if (tie is not (var tid, var tpart, var tkind)) continue;
            var cid = ctx.NewId();
            list.Add(new JsonObject { ["id"] = cid, ["kind"] = tkind, ["refs"] = RefsJson([me, (tid, tpart)]), ["driving"] = true });
            Report(ctx).Added.Add(cid);
        }
        Write(ctx, viewId, list);
        AfterChange(ctx, viewId);
    }

    // ------------------------------------------------------------ hooked into the commands

    /// <summary>The commands that replace shapes: their refs found again by geometry.</summary>
    static readonly string[] Replacing = ["drafting.trim", "drafting.extend", "drafting.fillet", "drafting.chamfer", "drafting.join", "drafting.explode", "drafting.offset", "element.delete"];

    static void RegisterMapping(IModuleRegistry r)
    {
        r.Service<ITransformFollowers>(new TransformWatch());
        r.Service<ICommandFollowUp>(new FollowUp());
    }

    /// <summary>
    /// After every command: the constrained sketches whose shapes it changed, their constraints carried (by geometry for the
    /// commands that replace shapes, by the transform for a move, turn, mirror, scale or copy; a grip edit or any other command
    /// keeps them while the shape keeps its sort and its points, else finds them by geometry), what was dropped, copied or added
    /// told in the command's result.
    /// </summary>
    sealed class FollowUp : ICommandFollowUp
    {
        public JsonNode? After(ICommandContext ctx, IEdit edit, string command, JsonObject args, JsonNode? result)
        {
            if (Take(ctx.Model, edit) is { } before)
            {
                var o = Report(ctx);
                if (command == "drafting.transform") Remap(ctx, before, Mode.Transform, CarryOf(args));
                else if (command == "element.transform" && o.Transform is { } t)
                {
                    var copy = (string?)args["op"] == "copy" || args["copy"] is JsonValue cv && cv.TryGetValue<bool>(out var cb) && cb;
                    Remap(ctx, before, Mode.Transform, new Carry(t, 1, copy, Args.Ids(args), o.Copies));
                }
                else Remap(ctx, before, Replacing.Contains(command) ? Mode.Geometric : Mode.Reshape, lenient: command == "element.delete");
            }
            return Attach(ctx, result);
        }
    }

    /// <summary>drafting.transform's own transform, from its arguments (the command has checked them).</summary>
    static Carry CarryOf(JsonObject a)
    {
        static Vec2 Pt(JsonNode? n) => Args.TryPoint(n) ?? default;
        var op = (string?)a["op"] ?? "move";
        var copy = op == "copy" || a["copy"] is JsonValue cv && cv.TryGetValue<bool>(out var cb) && cb;
        var ids = a["ids"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToList() : (string?)a["id"] is { } one ? [one] : new List<string>();
        switch (op)
        {
            case "rotate": return new Carry(Placement2D.Rotate(Pt(a["center"]), (Json.Number(a["angle"]) ?? 0) * Math.PI / 180), 1, copy, ids, null);
            case "mirror": return new Carry(Placement2D.Mirror(Pt(a["a"]), Pt(a["b"])), 1, copy, ids, null);
            case "scale":
            {
                var c = Pt(a["center"]);
                var k = Json.Number(a["factor"]) ?? 1;
                return new Carry(new Placement2D(p => c + (p - c) * k, k < 0 ? Math.PI : 0, false), Math.Abs(k), copy, ids, null);
            }
            default: return new Carry(Placement2D.Move(Pt(a["by"])), 1, copy, ids, null);
        }
    }

    /// <summary>element.transform's placement and copies, kept for the mapping that follows the command.</summary>
    sealed class TransformWatch : ITransformFollowers
    {
        public object? Before(ICommandContext ctx, IReadOnlySet<string> moving, IReadOnlySet<string> selected) => null;

        public void After(ICommandContext ctx, object? state, Placement2D t, Placement2D plan, IReadOnlySet<string> moving, IReadOnlySet<string> selected, IReadOnlyDictionary<string, string> copies)
        {
            if (!ctx.Model.OfClass(Class).Any()) return;
            var o = Report(ctx);
            o.Transform = t;
            o.Copies = copies.ToDictionary(kv => kv.Key, kv => kv.Value);
        }
    }
}
