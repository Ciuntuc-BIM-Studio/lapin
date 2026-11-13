using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A path in plan an array lays its copies along: its length (measured along it), the point and the unit tangent at a
/// distance from its start, and its line as drawn (by the chord rule), which is also what tells a path edited from one
/// left alone. <see cref="Closed"/>: it ends where it starts (a circle, a closed polyline), so a count is shared out
/// round it rather than putting one copy on top of another.
/// </summary>
public sealed record ArrayPath(double Length, Func<double, (Vec2 Point, Vec2 Tangent)> At, IReadOnlyList<Vec2> Drawn, bool Closed = false)
{
    /// <summary>A polyline: straight between its points, its tangent the piece's (at a corner, the piece after it).</summary>
    public static ArrayPath Polyline(IReadOnlyList<Vec2> points)
    {
        var pts = SectionPaths.Clean(points);
        if (pts.Count < 2) throw new CommandException("A path needs two different points.");
        var at = new double[pts.Count];
        for (var i = 1; i < pts.Count; i++) at[i] = at[i - 1] + (pts[i] - pts[i - 1]).Length;
        var length = at[^1];
        (Vec2, Vec2) At(double s)
        {
            var i = 0;
            while (i < pts.Count - 2 && s >= at[i + 1] - 1e-9) i++;
            var d = (pts[i + 1] - pts[i]).Normalized();
            return (pts[i] + d * Math.Clamp(s - at[i], 0, at[i + 1] - at[i]), d);
        }
        return new ArrayPath(length, At, pts, (pts[0] - pts[^1]).Length < 1e-6);
    }

    /// <summary>A straight or arched axis (<see cref="PathCurve"/>), exact: a wall's, a beam's.</summary>
    public static ArrayPath Curve(PathCurve c) =>
        new(c.Length, s => (c.PointAt(s), c.TangentAt(s)), c.IsArc ? c.Line(0, c.Length, 0) : [c.Start, c.End]);

    /// <summary>What tells this path from the same path edited: its drawn line to a tenth of a millimetre.</summary>
    public string Key => string.Join(";", Drawn.Select(p => $"{Math.Round(p.X, 4)},{Math.Round(p.Y, 4)}"));
}

/// <summary>
/// A module's elements an array can follow (a road's axis exactly along its curves, a drafted arc): the element's path,
/// or null when it has none. Without one, an element is followed as a section follows it (<see cref="SectionPaths"/>):
/// a path element's axis exactly, anything else by its line.
/// </summary>
public interface IArrayPaths
{
    ArrayPath? PathOf(IModelView m, Entity e);
}

/// <summary>
/// An array that keeps its arrangement (decision 481): Modify → Array in a row, round a centre or along a path makes a
/// <c>LapinArray</c> beside its copies, holding the rule (the count, the step or the extent filled, the angle and its
/// centre, the path with its offset, its ends and the copies' turn) and its items (the original first, then each copy:
/// the ids it is and where the rule put it). Changing the rule (array.update) places the items again: those that stay
/// are moved by the difference, keeping their ids (and whatever was done to one of them since, a copy moved by hand
/// keeping its offset); those added are copied from the original; those past the count are deleted. A path element
/// edited by any command moves the items along it in the same undo step. Every copy is made as element.transform makes
/// one, so what a module drives is copied its own way. Exploded (array.explode, or the array deleted), the copies stay,
/// plain. A Lapin resource: never exported to IFC, and the elements carry nothing of it.
/// </summary>
public static class Arrays
{
    public const string Class = "LapinArray";
    public const int Most = 1000;

    /// <summary>Where the rule puts an item: a turn (radians) about the origin, then a shift, and a rise.</summary>
    public readonly record struct Pose(double X, double Y, double Turn, double Z)
    {
        public static readonly Pose Identity = new(0, 0, 0, 0);

        public static Pose Of(JsonNode? n) => n is JsonArray a && a.Count >= 4
            ? new(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0, (Json.Number(a[2]) ?? 0) * Math.PI / 180, Json.Number(a[3]) ?? 0) : Identity;

        public JsonArray ToJson() => new(Math.Round(X, 9), Math.Round(Y, 9), Math.Round(Turn * 180 / Math.PI, 9), Math.Round(Z, 9));

        public Vec2 Apply(Vec2 p) => Rot(p, Turn) + new Vec2(X, Y);

        public bool Same(Pose o) => Math.Abs(X - o.X) < 1e-7 && Math.Abs(Y - o.Y) < 1e-7 && Math.Abs(Z - o.Z) < 1e-7 && Math.Abs(Math.IEEERemainder(Turn - o.Turn, 2 * Math.PI)) < 1e-9;

        /// <summary>The plan transformation carrying what stands at <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static Placement2D Between(Pose from, Pose to)
        {
            var d = to.Turn - from.Turn;
            var (f, t) = (new Vec2(from.X, from.Y), new Vec2(to.X, to.Y));
            return new Placement2D(p => Rot(p - f, d) + t, d, false);
        }
    }

    static Vec2 Rot(Vec2 p, double a)
    {
        var (c, s) = (Math.Cos(a), Math.Sin(a));
        return new Vec2(p.X * c - p.Y * s, p.X * s + p.Y * c);
    }

    public sealed record Item(List<string> Ids, Pose At);

    public static bool Is(Entity? e) => e?.Class == Class;

    public static IEnumerable<Entity> All(IModelView m) => m.OfClass(Class);

    public static List<Item> Items(Entity array) =>
        (array.Attrs["items"] as JsonArray)?.OfType<JsonObject>()
            .Select(o => new Item((o["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [], Pose.Of(o["at"]))).ToList() ?? [];

    static JsonArray ItemsJson(IEnumerable<Item> items) =>
        new(items.Select(i => (JsonNode)new JsonObject { ["ids"] = new JsonArray(i.Ids.Select(x => (JsonNode)x).ToArray()), ["at"] = i.At.ToJson() }).ToArray());

    /// <summary>The array an element (or group) is an item of, and which item (0 the original); null when it is in none.</summary>
    public static (Entity Array, int Index)? Of(IModelView m, string id)
    {
        foreach (var a in All(m))
        {
            var items = Items(a);
            for (var i = 0; i < items.Count; i++) if (items[i].Ids.Contains(id)) return (a, i);
        }
        return null;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Array", [], "#8a94a6", Kind: "resource", Template: false));
        r.Service<IDrivenElements>(new Watch());

        // An array kept as an arrangement (docs: Modify → Array): {ids, count, step} in a row, {kind: "polar", count, angle, center}
        // round a centre, or {kind: "path", along (an element) | points, count | spacing, start, end, offset, orient, flip, base}.
        r.Command("array.update", "Change array", (ctx, a) =>
        {
            var array = Get(ctx.Model, a);
            var attrs = array.CloneAttrs();
            foreach (var key in RuleKeys)
                if (a.ContainsKey(key))
                {
                    if (a[key] is null) attrs.Remove(key); else attrs[key] = a[key]!.DeepClone();
                }
            // A new path replaces the old (an element or points, not both); the base point and the reference direction stay, so
            // each copy turns from the original as before.
            if (a.ContainsKey("along") || a.ContainsKey("points"))
            {
                if (a.ContainsKey("along") && a["along"] is not null) attrs.Remove("points");
                else if (a.ContainsKey("points") && a["points"] is not null) attrs.Remove("along");
            }
            var rule = Rule.Of(ctx.Model, ctx.Services, attrs);
            return Place(ctx, array, attrs, rule);
        });

        // The copies left as they are, plain; the arrangement gone.
        r.Command("array.explode", "Explode array", (ctx, a) =>
        {
            var array = Get(ctx.Model, a);
            var ids = Items(array).SelectMany(i => i.Ids).Where(id => ctx.Model.Get(id) != null).ToList();
            ctx.Remove(array.Id);
            return new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()) };
        });

        // The array an element is in: its rule, its items, which one this is.
        r.Query("array.of", (q, a) =>
        {
            var m = q.Model;
            var id = (string?)a["id"] ?? throw new CommandException("Which element?");
            if (Is(m.Get(id))) return Describe(m.Get(id)!, null);
            return Of(m, id) is var (array, index) ? Describe(array, index) : null;
        });

        // Where an array would put its items, for the tool's ghost (the command's own rule): {places: [{x, y (where the base
        // point goes), angle (the copy's direction: along a path the path's there, degrees)}], path, length}.
        r.Query("array.preview", (q, a) =>
        {
            var m = q.Model;
            JsonObject attrs;
            if ((string?)a["id"] is { } id && m.Get(id) is { Class: Class } array)
            {
                attrs = array.CloneAttrs();
                foreach (var key in RuleKeys) if (a.ContainsKey(key)) { if (a[key] is null) attrs.Remove(key); else attrs[key] = a[key]!.DeepClone(); }
            }
            else attrs = RuleAttrs(a);
            var rule = Rule.Of(m, q.Services, attrs);
            var poses = rule.Poses(m, out var path);
            var o = new JsonObject
            {
                ["count"] = poses.Count,
                ["places"] = new JsonArray(poses.Select(p => (JsonNode)new JsonObject
                {
                    ["x"] = Math.Round(p.Apply(rule.Base).X, 6), ["y"] = Math.Round(p.Apply(rule.Base).Y, 6), ["angle"] = Math.Round((p.Turn + (rule.RefAngle ?? 0)) * 180 / Math.PI, 6),
                }).ToArray()),
            };
            if (path != null)
            {
                o["path"] = SectionPaths.PointsJson(path.Drawn);
                o["length"] = Math.Round(path.Length, 6);
            }
            return o;
        });
    }

    static Entity Get(IModelView m, JsonObject a) =>
        (string?)a["id"] is { } id && m.Get(id) is var e && e != null
            ? Is(e) ? e : Of(m, id)?.Array ?? throw new CommandException($"„{e.Name}” is not in an array.")
            : throw new CommandException("Which array?");

    static readonly string[] RuleKeys = ["kind", "count", "step", "fit", "angle", "center", "along", "points", "spacing", "start", "end", "offset", "orient", "flip", "base", "refAngle"];

    static JsonObject RuleAttrs(JsonObject a)
    {
        var o = new JsonObject();
        foreach (var key in RuleKeys) if (a[key] is { } v) o[key] = v.DeepClone();
        o["kind"] ??= "row";
        return o;
    }

    static JsonObject Describe(Entity array, int? index)
    {
        var o = array.CloneAttrs();
        var items = Items(array);
        o["id"] = array.Id;
        o["name"] = array.Name;
        o["items"] = items.Count;
        o["ids"] = new JsonArray(items.SelectMany(i => i.Ids).Select(x => (JsonNode)x).ToArray());
        if (index != null) o["index"] = index;
        return o;
    }

    /// <summary>The rule of an array, read from its attributes and checked.</summary>
    public sealed class Rule
    {
        public string Kind = "row";
        public int Count;
        public Vec3 Step;
        public Vec3? Fit;
        public double Angle = 360;
        public Vec2 Center;
        public string? Along;
        public List<Vec2>? Points;
        public double? Spacing;
        public double Start, End, Offset;
        public bool Tangent = true, Flip;
        public Vec2 Base;
        public double? RefAngle;
        public bool HasBase;
        IModelView? _m;
        IServices? _services;

        public static Rule Of(IModelView m, IServices? services, JsonObject a)
        {
            var r = new Rule { _m = m, _services = services, Kind = (string?)a["kind"] ?? "row" };
            if (r.Kind is not ("row" or "polar" or "path")) throw new CommandException($"„{r.Kind}” is not a row, a polar array or one along a path.");
            r.Count = (int)Math.Round(Json.Number(a["count"]) ?? 0);
            switch (r.Kind)
            {
                case "row":
                {
                    r.Fit = V3(a["fit"]);
                    if (r.Count < 2 || r.Count > Most) throw new CommandException($"An array has 2 to {Most} items, the first among them.");
                    r.Step = r.Fit is { } fit ? fit * (1.0 / (r.Count - 1)) : V3(a["step"]) ?? default;
                    if (!double.IsFinite(r.Step.X + r.Step.Y + r.Step.Z) || Math.Abs(r.Step.X) + Math.Abs(r.Step.Y) + Math.Abs(r.Step.Z) < 1e-9)
                        throw new CommandException("An array's step cannot be nothing.");
                    break;
                }
                case "polar":
                    if (r.Count < 2 || r.Count > Most) throw new CommandException($"An array has 2 to {Most} items, the first among them.");
                    r.Angle = Json.Number(a["angle"]) ?? 360;
                    if (!double.IsFinite(r.Angle) || Math.Abs(r.Angle) < 1e-9 || Math.Abs(r.Angle) > 360) throw new CommandException("A polar array fills an angle of up to 360°.");
                    r.Center = Args.Point(a["center"], "centre");
                    break;
                default:
                    r.Along = (string?)a["along"] is { Length: > 0 } along ? along : null;
                    r.Points = a["points"] is JsonArray pts ? pts.Select(p => Args.TryPoint(p) ?? throw new CommandException("A path's points are [[x, y], …].")).ToList() : null;
                    if (r.Along == null && r.Points == null) throw new CommandException("An array along a path needs its path: an element to follow, or points.");
                    r.Spacing = Json.Number(a["spacing"]) is { } sp and > 0 ? sp : null;
                    if (a["spacing"] != null && r.Spacing == null) throw new CommandException("The spacing along a path must be more than nothing.");
                    if (r.Spacing == null && (r.Count < 2 || r.Count > Most)) throw new CommandException($"An array has 2 to {Most} items, the first among them.");
                    if (r.Spacing != null && r.Count != 0 && (r.Count < 2 || r.Count > Most)) throw new CommandException($"An array has 2 to {Most} items, the first among them.");
                    r.Start = Math.Max(0, Json.Number(a["start"]) ?? 0);
                    r.End = Math.Max(0, Json.Number(a["end"]) ?? 0);
                    r.Offset = Json.Number(a["offset"]) ?? 0;
                    r.Tangent = (string?)a["orient"] != "fixed";
                    r.Flip = a["flip"] is JsonValue fv && fv.TryGetValue<bool>(out var fb) && fb;
                    r.RefAngle = Json.Number(a["refAngle"]) is { } ra ? ra * Math.PI / 180 : null;
                    if (!double.IsFinite(r.Offset + r.Start + r.End)) throw new CommandException("A path's offset and ends must be numbers.");
                    break;
            }
            if (Args.TryPoint(a["base"]) is { } b) { r.Base = b; r.HasBase = true; }
            return r;
        }

        static Vec3? V3(JsonNode? n) => n is JsonArray s ? new Vec3(Json.Number(s.ElementAtOrDefault(0)) ?? 0, Json.Number(s.ElementAtOrDefault(1)) ?? 0, Json.Number(s.ElementAtOrDefault(2)) ?? 0) : null;

        /// <summary>The path followed, as it is now.</summary>
        public ArrayPath? Path()
        {
            if (Kind != "path") return null;
            if (Along != null)
            {
                var e = _m?.Get(Along) ?? throw new CommandException("The path the array follows is not in the model.");
                return Follow(_m!, _services, e) ?? throw new CommandException($"An array cannot follow „{e.Name}”: it has no line in plan (a road, a wall, a beam, a drafted line, polyline or arc can be followed).");
            }
            return ArrayPath.Polyline(Points!);
        }

        /// <summary>
        /// Where the rule puts each item, the original first. A row and a polar array leave the original where it is; along a
        /// path the original's base point goes to the first place, turned (with the tangent) from the path's direction there.
        /// </summary>
        public List<Pose> Poses(IModelView m, out ArrayPath? path)
        {
            path = null;
            var o = new List<Pose>();
            switch (Kind)
            {
                case "row":
                    for (var i = 0; i < Count; i++) o.Add(new Pose(Step.X * i, Step.Y * i, 0, Step.Z * i));
                    Base = HasBase ? Base : default;
                    return o;
                case "polar":
                {
                    // A full turn shares it out; a part of one puts the last copy at its end.
                    var step = (Math.Abs(Math.Abs(Angle) - 360) < 1e-9 ? Angle / Count : Angle / (Count - 1)) * Math.PI / 180;
                    for (var i = 0; i < Count; i++)
                    {
                        var t = step * i;
                        var shift = Center - Rot(Center, t);
                        o.Add(new Pose(shift.X, shift.Y, t, 0));
                    }
                    Base = HasBase ? Base : Center;
                    return o;
                }
            }
            var p = Path()!;
            path = p;
            var (s0, s1) = (Start, p.Length - End);
            if (s1 < s0 - 1e-9) throw new CommandException($"The path is {p.Length:0.###} m long: its start and end distances leave nothing to put the copies along.");
            var span = Math.Max(0, s1 - s0);
            var places = new List<double>();
            if (Spacing is { } sp)
            {
                var n = Count >= 2 ? Count : (int)Math.Floor(span / sp + 1e-9) + 1;
                if (n > Most) throw new CommandException($"At {sp:0.###} m along {span:0.###} m there would be more than {Most} items.");
                if (n < 2) throw new CommandException($"At {sp:0.###} m apart only one fits along {span:0.###} m of the path.");
                if (sp * (n - 1) > span + 1e-6) throw new CommandException($"{n} items {sp:0.###} m apart need {sp * (n - 1):0.###} m; the path has {span:0.###} m for them.");
                for (var i = 0; i < n; i++) places.Add(s0 + sp * i);
            }
            else
            {
                // Round a closed path its whole length, the count shares it out (the last not on the first).
                var shared = p.Closed && Start < 1e-9 && End < 1e-9;
                for (var i = 0; i < Count; i++) places.Add(s0 + span * i / (shared ? Count : Count - 1));
            }
            double Dir(double s) { var t = p.At(s).Tangent; return Math.Atan2(t.Y, t.X); }
            var reference = RefAngle ?? (Tangent ? Dir(places[0]) : 0);
            RefAngle = reference;
            var first = Q(places[0]);
            Vec2 Q(double s) { var (pt, t) = p.At(s); return pt + t.Perp * Offset; }
            if (!HasBase) { Base = first; HasBase = true; }
            foreach (var s in places)
            {
                var turn = (Tangent ? Dir(s) - reference : 0) + (Flip ? Math.PI : 0);
                var shift = Q(s) - Rot(Base, turn);
                o.Add(new Pose(shift.X, shift.Y, turn, 0));
            }
            return o;
        }

        /// <summary>What the rule is, to keep on the array (the base point and the reference direction fixed when it was made).</summary>
        public void Write(JsonObject attrs)
        {
            if (Kind == "path")
            {
                attrs["base"] = new JsonArray(Math.Round(Base.X, 9), Math.Round(Base.Y, 9));
                if (RefAngle is { } ra) attrs["refAngle"] = Math.Round(ra * 180 / Math.PI, 9);
            }
        }
    }

    /// <summary>The path of an element, as an array follows it: a module's own, else a path element's axis exactly, else its line as a section follows it.</summary>
    public static ArrayPath? Follow(IModelView m, IServices? services, Entity e)
    {
        if (services != null)
            foreach (var s in services.All<IArrayPaths>())
                if (s.PathOf(m, e) is { } p) return p;
        if (services?.Get<IPathElements>()?.IsPath(e.Class) == true && e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray)
        {
            var c = PathCurve.Of(e);
            if (c.Length > 1e-9) return ArrayPath.Curve(c);
        }
        return SectionPaths.PathOfElement(m, services, e) is { Points.Count: >= 2 } f && SectionPaths.Clean(f.Points).Count >= 2 ? ArrayPath.Polyline(f.Points) : null;
    }

    // ------------------------------------------------------------ making and placing

    /// <summary>element.array: the copies made and the arrangement kept beside them; returns {ids (the copies), array}.</summary>
    public static JsonObject Make(ICommandContext ctx, JsonObject a)
    {
        var given = Args.Ids(a).Distinct().Where(id => ctx.Model.Get(id) != null).ToList();
        if (given.Count == 0) throw new CommandException("Select what to array.");
        var attrs = RuleAttrs(a);
        var rule = Rule.Of(ctx.Model, ctx.Services, attrs);
        var array = new Entity(ctx.NewId(), Class, NextName(ctx.Model), attrs);
        ctx.Add(array);
        var original = new Item(Roots(ctx.Model, given), Pose.Identity);
        attrs = array.CloneAttrs();
        attrs["items"] = ItemsJson([original]);
        ctx.Replace(array.With(attrs: attrs));
        var result = Place(ctx, ctx.Model.Get(array.Id)!, attrs, rule);
        var made = new JsonArray(Items(ctx.Model.Get(array.Id)!).Skip(1).SelectMany(i => i.Ids).Select(x => (JsonNode)x).ToArray());
        return new JsonObject { ["ids"] = made, ["array"] = array.Id, ["count"] = result["count"]?.DeepClone() };
    }

    static string NextName(IModelView m)
    {
        var n = All(m).Count() + 1;
        while (All(m).Any(x => x.Name == $"Array {n}")) n++;
        return $"Array {n}";
    }

    /// <summary>
    /// The items placed as the rule says now: those that stay moved by the difference (ids kept), new ones copied from the
    /// original (or the first item still there), those past the count deleted; one undo step with the command.
    /// </summary>
    static JsonObject Place(ICommandContext ctx, Entity array, JsonObject attrs, Rule rule)
    {
        var poses = rule.Poses(ctx.Model, out var path);
        rule.Write(attrs);
        if (path != null && rule.Along != null) attrs["pathKey"] = path.Key; else attrs.Remove("pathKey");
        var items = Items(array).Select(i => i with { Ids = i.Ids.Where(id => ctx.Model.Get(id) != null).ToList() }).ToList();
        var (moved, added, removed) = (0, 0, 0);
        var carried = new List<ElementTransform.Result>();
        // Past the count: deleted (one removed by hand stays a gap).
        if (items.Count > poses.Count)
        {
            var surplus = items.Skip(poses.Count).SelectMany(i => i.Ids).Where(id => ctx.Model.Get(id) != null).ToList();
            items = items.Take(poses.Count).ToList();
            if (surplus.Count > 0) { Delete(ctx, surplus); removed = surplus.Count; }
        }
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Ids.Count == 0 || item.At.Same(poses[i])) { items[i] = item with { At = poses[i] }; continue; }
            // What is pinned stands where it is (decision 449): it keeps its place, and where the rule last put it.
            if (item.Ids.Any(id => Pins.Is(ctx.Model.Get(id)) || Groups.Expand(ctx.Model, [id]).Elements.Any(x => Pins.Is(ctx.Model.Get(x))))) continue;
            Carry(ctx, item.Ids, item.At, poses[i], copy: false, carried);
            items[i] = item with { At = poses[i] };
            moved++;
        }
        if (poses.Count > items.Count)
        {
            var source = items.FirstOrDefault(i => i.Ids.Count > 0) ?? throw new CommandException("Nothing of the array is left to copy.");
            for (var i = items.Count; i < poses.Count; i++)
            {
                var made = Carry(ctx, source.Ids, source.At, poses[i], copy: true, carried);
                items.Add(new Item(made, poses[i]));
                added++;
            }
        }
        // Walls, beams and strip footings carried join what they now meet outside the array; its items keep their joins
        // among them as they were, so each stays where the rule puts it, and none joins the path it follows (forum 47, decision 531).
        if (carried.Count > 0)
        {
            var inArray = Groups.Expand(ctx.Model, items.SelectMany(i => i.Ids)).Elements.ToHashSet();
            if (rule.Along != null) inArray.Add(rule.Along);
            foreach (var r in carried) ElementTransform.JoinWhatMeets(ctx, r, inArray);
        }
        attrs["items"] = ItemsJson(items);
        // A count follows from a spacing along a path when none was given: worked out each time, not kept.
        if (!(rule.Kind == "path" && rule.Spacing != null && rule.Count < 2)) attrs["count"] = poses.Count;
        ctx.Replace(ctx.Model.Get(array.Id)!.With(attrs: attrs));
        return new JsonObject { ["count"] = poses.Count, ["moved"] = moved, ["added"] = added, ["removed"] = removed };
    }

    /// <summary>The ids that stand for a selection as a whole: a group's members go with their group.</summary>
    static List<string> Roots(IModelView m, IEnumerable<string> ids)
    {
        var all = ids.Distinct().ToList();
        var set = all.ToHashSet();
        return all.Where(id => !(Groups.Of(m, id) is { } g && set.Contains(g.Id))).ToList();
    }

    /// <summary>What stands at <paramref name="from"/> carried, or copied, to <paramref name="to"/>, as element.transform does it (what a module drives, its own way): the copy's ids.</summary>
    static List<string> Carry(ICommandContext ctx, List<string> ids, Pose from, Pose to, bool copy, List<ElementTransform.Result>? carried = null)
    {
        var args = new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()), ["op"] = copy ? "copy" : "move", ["up"] = to.Z - from.Z };
        var r = ElementTransform.Run(ctx, args, Pose.Between(from, to));
        carried?.Add(r);
        return copy ? Roots(ctx.Model, r.Made.Select(x => (string?)x).OfType<string>()) : ids;
    }

    static void Delete(ICommandContext ctx, List<string> ids)
    {
        var catalog = ctx.Services as ModuleCatalog ?? throw new CommandException("Nothing can delete here.");
        var delete = catalog.Command("element.delete") ?? throw new CommandException("Nothing can delete here.");
        delete.Handler(ctx, new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()) });
    }

    /// <summary>
    /// After every command: an array whose path was edited places its items along it again; one whose path is gone keeps
    /// its last line; ids gone are left out, and an array with nothing left goes. Nothing of the elements is driven.
    /// </summary>
    sealed class Watch : IDrivenElements
    {
        public bool Drives(IModelView m, Entity e) => false;

        public void Before(ICommandContext ctx, DrivenEdit edit, DrivenOutcome outcome) { }

        public void After(ICommandContext ctx, IEdit edit, string command)
        {
            if (command is "array.update" or "array.explode") return;
            foreach (var array in All(ctx.Model).ToList())
            {
                var items = Items(array);
                var alive = items.Select(i => i with { Ids = i.Ids.Where(id => ctx.Model.Get(id) != null).ToList() }).ToList();
                if (alive.All(i => i.Ids.Count == 0)) { ctx.Remove(array.Id); continue; }
                var attrs = array.CloneAttrs();
                var changed = alive.Zip(items).Any(p => p.First.Ids.Count != p.Second.Ids.Count);
                if (changed) attrs["items"] = ItemsJson(alive);
                if (array.Str("along") is { } along)
                {
                    if (ctx.Model.Get(along) is not { } e || Follow(ctx.Model, ctx.Services, e) is not { } path)
                    {
                        // The path gone: its last line kept, so the array can still be changed.
                        var last = (string?)attrs["pathKey"];
                        attrs.Remove("along");
                        attrs.Remove("pathKey");
                        attrs["points"] = last is { Length: > 0 } ? new JsonArray(last.Split(';').Select(p => p.Split(',')).Select(xy => (JsonNode)new JsonArray(double.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture))).ToArray()) : new JsonArray();
                        ctx.Replace(array.With(attrs: attrs));
                        continue;
                    }
                    if (path.Key != (string?)attrs["pathKey"])
                    {
                        ctx.Replace(array.With(attrs: attrs));
                        var now = ctx.Model.Get(array.Id)!;
                        Place(ctx, now, now.CloneAttrs(), Rule.Of(ctx.Model, ctx.Services, now.CloneAttrs()));
                        continue;
                    }
                }
                if (changed) ctx.Replace(array.With(attrs: attrs));
            }
        }
    }
}
