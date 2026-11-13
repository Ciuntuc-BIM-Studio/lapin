using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// A part's own sketches (docs/plan-studio-parametric.md §9 point 2, decision 520): a formula corner becomes a constraint.
///
/// <para><b>The schema.</b> A part definition may hold <c>sketches: [{id, name, plane (xy, xz, yz), offset?: [x, y, z] (numbers or
/// formulas), shapes: [{id, type: line | polyline | circle | arc, …}], constraints: [{id, kind, refs: [{shape, part}], value?,
/// formula?, driving}]}]</c>: shapes in the sketch's plane (metres), constraints in the view store's schema
/// (<see cref="SketchConstraints"/>) with refs naming the sketch's own shape ids, formulas naming the part's parameters. A body may
/// take its outline from a sketch, <c>{name, sketch, shape?, holes?: [shape ids], depth, when?, offset?, material?}</c> (a closed
/// polyline, a circle, or a closed chain of lines and arcs that <c>shape</c> names one of, <see cref="Loops"/>; without
/// <c>shape</c>, the sketch's first closed one), instead of its <c>points</c>; bodies of <c>points</c> stay as they are, read
/// forever. The stored shapes are the sketch solved with the part's defaults.</para>
///
/// <para><b>Coordinates.</b> A sketch's shapes are 2D, <c>(u, v)</c> on its plane: <c>xy</c> takes the part's x and y, <c>xz</c> its
/// x and z, <c>yz</c> its y and z (<c>Parts.Frame</c>), from the sketch's <c>offset</c>, so a point is <c>offset + u·U + v·V</c> in the
/// part's frame. For a placed part, <c>studio.partTree</c>'s <c>frame {origin, right, up, normal}</c> takes it into the world:
/// <c>origin + u·right + v·up</c> (the instance's point, height, rotation and mirror applied, <c>Parts.Tree</c>). A drag's
/// <c>to</c> is in the same <c>(u, v)</c>.</para>
///
/// <para><b>Per instance.</b> The sketch is solved again with the instance's values (its own over the defaults) for its formula
/// dimensions, from the stored shapes, by Lapin's solver; the bodies are pushed from the solved outlines. The solve is cached per
/// sketch (its JSON node: a changed part is a new one) and per set of the values its formulas name, so instances sharing them never
/// solve twice (§7.3). A solve that fails for some values throws (cached too): <c>setPartValues</c> and the part's edits refuse such
/// values, and an instance that meets one anyway (a part changed by <c>updatePart</c>) builds no body and tells why under
/// Problems.</para>
///
/// <para><b>The conversion</b> is explicit, <c>studio.constrainPart</c>: each body of <c>points</c> becomes a sketch of one closed
/// polyline (and one per hole) whose every corner has a driving <c>distanceX</c> and <c>distanceY</c> from the sketch's origin
/// carrying its formula or number (FreeCAD's Lock), so the bodies and volumes stay the same. Nothing is converted on opening a
/// project: the families' editor edits corners, and a saved part changes only when its user asks.</para>
/// </summary>
public static partial class PartSketches
{
    /// <summary>How many parameter sets a sketch keeps solved (a flex run asks many; the oldest go first).</summary>
    const int CacheSize = 256;

    public static JsonArray SketchesOf(JsonObject def) => def["sketches"] as JsonArray ?? [];

    public static JsonObject? Find(JsonObject def, string? id) =>
        id == null ? null : SketchesOf(def).OfType<JsonObject>().FirstOrDefault(s => (string?)s["id"] == id);

    internal static List<(string Id, Shape Shape)> ShapesOf(JsonObject sketch) =>
        [.. (sketch["shapes"] as JsonArray ?? []).OfType<JsonObject>().Select(o => ((string?)o["id"] ?? "", Shape.From(o))).Where(x => x.Item2 != null).Select(x => (x.Item1, x.Item2!))];

    internal static List<JsonObject> ConstraintsOf(JsonObject sketch) => [.. (sketch["constraints"] as JsonArray ?? []).OfType<JsonObject>()];

    static JsonObject ShapeJson(string id, Shape s)
    {
        var o = new JsonObject { ["id"] = id };
        foreach (var (k, v) in s.ToJson()) o[k] = v?.DeepClone();
        return o;
    }

    static bool GoodId(string id) => id.Length is > 0 and <= 32 && char.IsLetter(id[0]) && id.All(c => char.IsLetterOrDigit(c) || c is '_' or '-');

    /// <summary>The next free id of the form <paramref name="prefix"/>N.</summary>
    static string Next(IEnumerable<string> used, string prefix)
    {
        var max = used.Select(u => u.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(u[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return prefix + (max + 1);
    }

    // ------------------------------------------------------------ checking

    /// <summary>
    /// A definition's sketches checked and written the one way: ids (given or <c>s1</c>, <c>s2</c>…; shapes' <c>p1</c>…; constraints'
    /// <c>c1</c>…), planes, shapes that read, constraints checked against them and the parameters, and the sketch solved with the
    /// defaults (refused, naming the constraints, when it does not solve), its shapes stored as solved.
    /// </summary>
    internal static JsonArray Check(JsonNode? n, IReadOnlyDictionary<string, double> defaults)
    {
        var o = new JsonArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var given = (n as JsonArray ?? []).OfType<JsonObject>().Select(s => ((string?)s["id"])?.Trim()).OfType<string>().ToList();
        var i = 0;
        foreach (var x in n as JsonArray ?? [])
        {
            i++;
            if (x is not JsonObject so) throw new CommandException("Each sketch is {id, plane, shapes, constraints}.");
            var id = ((string?)so["id"])?.Trim() is { Length: > 0 } sid ? sid : Next(given.Concat(ids), "s");
            if (!GoodId(id)) throw new CommandException($"A sketch's id is letters and digits („{id}”).");
            if (!ids.Add(id)) throw new CommandException($"The sketch „{id}” is there twice.");
            var plane = (string?)so["plane"] ?? "xy";
            if (!Parts.Planes.Contains(plane)) throw new CommandException($"A sketch's plane is {string.Join(", ", Parts.Planes)}.");
            var name = ((string?)so["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Sketch {i}";
            // Its shapes.
            var shapes = new List<(string Id, Shape Shape)>();
            var shapeIds = (so["shapes"] as JsonArray ?? []).OfType<JsonObject>().Select(s => ((string?)s["id"])?.Trim()).OfType<string>().ToList();
            foreach (var sx in so["shapes"] as JsonArray ?? [])
            {
                var shape = Shape.From(sx) ?? throw new CommandException($"„{name}”: a shape is a line, a polyline, a circle or an arc.");
                var shid = ((string?)sx!["id"])?.Trim() is { Length: > 0 } s0 ? s0 : Next(shapeIds.Concat(shapes.Select(s => s.Id)), "p");
                if (!GoodId(shid)) throw new CommandException($"„{name}”: a shape's id is letters and digits („{shid}”).");
                if (shapes.Any(s => s.Id == shid)) throw new CommandException($"„{name}”: the shape „{shid}” is there twice.");
                shapes.Add((shid, shape));
            }
            var dict = shapes.ToDictionary(s => s.Id, s => s.Shape);
            // Its constraints.
            var cs = new List<JsonObject>();
            var cids = (so["constraints"] as JsonArray ?? []).OfType<JsonObject>().Select(c => ((string?)c["id"])?.Trim()).OfType<string>().ToList();
            foreach (var cx in so["constraints"] as JsonArray ?? [])
            {
                if (cx is not JsonObject co) throw new CommandException($"„{name}”: each constraint is {{kind, refs, …}}.");
                var c = new JsonObject { ["kind"] = (string?)co["kind"], ["refs"] = (co["refs"] as JsonArray)?.DeepClone() };
                if (co["value"] is { } value) c["value"] = value.DeepClone();
                if (co["formula"] is { } formula) c["formula"] = formula.DeepClone();
                if (co["driving"] is { } driving) c["driving"] = driving.DeepClone();
                try { SketchConstraints.CheckLocal(dict, c, defaults); }
                catch (CommandException ex) { throw new CommandException($"„{name}”: {ex.Message}"); }
                var cid = ((string?)co["id"])?.Trim() is { Length: > 0 } c0 ? c0 : Next(cids.Concat(cs.Select(z => (string)z["id"]!)), "c");
                if (cs.Any(z => (string?)z["id"] == cid)) throw new CommandException($"„{name}”: the constraint „{cid}” is there twice.");
                var stored = new JsonObject { ["id"] = cid };
                foreach (var (k, v) in c) stored[k] = v?.DeepClone();
                cs.Add(stored);
            }
            // Solved with the defaults: the shapes stored as the solve leaves them (a new sketch has none yet).
            var solved = new Dictionary<string, Shape>();
            if (shapes.Count > 0)
            {
                var r = SketchConstraints.SolveLocal(shapes, cs, defaults);
                if (r.Error != null) throw new CommandException($"„{name}”: {r.Error}");
                solved = r.Shapes();
            }
            var sk = new JsonObject { ["id"] = id, ["name"] = name, ["plane"] = plane };
            if (so["offset"] is JsonArray { Count: 3 } off)
            {
                foreach (var ox in off) Parts.Eval(ox, defaults, $"„{name}”'s offset");
                sk["offset"] = off.DeepClone();
            }
            else if (so["offset"] != null) throw new CommandException($"„{name}”'s offset is [x, y, z], numbers or formulas.");
            sk["shapes"] = new JsonArray(shapes.Select(s => (JsonNode)ShapeJson(s.Id, solved.TryGetValue(s.Id, out var sv) ? sv : s.Shape)).ToArray());
            sk["constraints"] = new JsonArray(cs.Select(c => (JsonNode)c).ToArray());
            o.Add((JsonNode)sk);
        }
        return o;
    }

    /// <summary>A body drawn from a sketch checked: its sketch among <paramref name="sketches"/>, its outline and holes closed outlines of it (<see cref="Loops"/>).</summary>
    internal static JsonObject CheckBody(JsonObject b, JsonArray sketches)
    {
        var sid = (string)b["sketch"]!;
        var sketch = sketches.OfType<JsonObject>().FirstOrDefault(s => (string?)s["id"] == sid) ?? throw new CommandException($"A body's sketch „{sid}” is not among the part's sketches.");
        var given = new List<string>();
        foreach (var h in b["holes"] as JsonArray ?? []) given.Add((string?)h ?? throw new CommandException("A body's holes name shapes of its sketch."));
        var shapes = ShapesOf(sketch);
        var what = (string?)b["name"] is { Length: > 0 } bn ? $"„{bn}”" : "A body";
        var (outer, _) = ProfileLoops(shapes, (string?)b["shape"], given, what);
        var outline = (string?)b["shape"] ?? outer.Ids[0];
        var holes = new JsonArray(given.Select(h => (JsonNode)h).ToArray());
        var o = (JsonObject)b.DeepClone();
        o.Remove("points");
        o.Remove("plane");
        o["shape"] = outline;
        if (holes.Count > 0) o["holes"] = holes; else o.Remove("holes");
        if (o["depth"] == null) throw new CommandException("A body needs its depth.");
        return o;
    }

    // ------------------------------------------------------------ solving, cached

    static readonly ConditionalWeakTable<JsonObject, Cache> Caches = new();

    /// <summary>The parameters a sketch's formulas name, in order: what its solve depends on.</summary>
    static string[] NamesOf(JsonObject sketch) =>
        [.. ConstraintsOf(sketch).Select(c => (string?)c["formula"]).OfType<string>().SelectMany(Expr.Names)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// A part's sketch solved with some values: its shapes by id. Cached per sketch and per the values its formulas name; a failure
    /// (cached too) throws with why.
    /// </summary>
    public static IReadOnlyDictionary<string, Shape> Solve(JsonObject sketch, IReadOnlyDictionary<string, double> values)
    {
        var cache = Caches.GetValue(sketch, _ => new Cache());
        string key;
        lock (cache)
        {
            cache.Names ??= NamesOf(sketch);
            key = string.Join(";", cache.Names.Select(n => n + "=" + (values.TryGetValue(n, out var v) ? v.ToString("R", CultureInfo.InvariantCulture) : "?")));
            if (cache.By.TryGetValue(key, out var hit)) return hit.Shapes ?? throw new CommandException(hit.Error!);
        }
        (Dictionary<string, Shape>? Shapes, string? Error) res;
        var name = (string?)sketch["name"] ?? (string?)sketch["id"];
        try
        {
            var r = SketchConstraints.SolveLocal(ShapesOf(sketch), ConstraintsOf(sketch), values);
            res = r.Error == null ? (r.Shapes(), null) : (null, $"„{name}”: {r.Error}");
        }
        catch (CommandException ex) { res = (null, $"„{name}”: {ex.Message}"); }
        lock (cache)
        {
            if (!cache.By.ContainsKey(key))
            {
                cache.By[key] = res;
                cache.Order.Enqueue(key);
                cache.Solves++;
                while (cache.Order.Count > CacheSize) cache.By.Remove(cache.Order.Dequeue());
            }
        }
        return res.Shapes ?? throw new CommandException(res.Error!);
    }

    /// <summary>How many times a sketch was solved for its instances (tests: instances sharing values never solve twice).</summary>
    public static int SolveCount(JsonObject sketch) => Caches.TryGetValue(sketch, out var c) ? c.Solves : 0;

    /// <summary>A body drawn from a sketch: its sketch's plane and offset, and its outline and holes solved with the values.</summary>
    internal static (string Plane, JsonArray? Offset, List<Vec2> Outer, List<List<Vec2>> Holes) Profile(JsonObject def, JsonObject body, IReadOnlyDictionary<string, double> values, string name)
    {
        var sketch = Find(def, (string?)body["sketch"]) ?? throw new CommandException($"{name}'s sketch is gone.");
        var solved = Solve(sketch, values);
        // In the sketch's order, as solved with these values: a chain closed for the defaults may open for others, and is told.
        var shapes = ShapesOf(sketch).Select(s => (s.Id, solved.TryGetValue(s.Id, out var sv) ? sv : s.Shape)).ToList();
        var (outer, holes) = ProfileLoops(shapes, (string?)body["shape"], (body["holes"] as JsonArray ?? []).Select(h => (string?)h).OfType<string>(), name);
        return ((string?)sketch["plane"] ?? "xy", sketch["offset"] as JsonArray, outer.Ring, [.. holes.Select(h => h.Ring)]);
    }

    // ------------------------------------------------------------ the conversion

    /// <summary>
    /// A definition whose bodies of <c>points</c> are made sketches: each body a sketch (its plane, its offset) of one closed polyline
    /// <c>outline</c> (and <c>hole1</c>… for its holes) whose corners carry a driving distanceX and distanceY from the origin, each
    /// with the corner's formula or number. Returns the new definition (to be checked) and the sketches made.
    /// </summary>
    public static (JsonObject Def, List<string> Made) Constrain(JsonObject def)
    {
        var values = Parts.Values(def, null);
        var o = (JsonObject)def.DeepClone();
        if (o["sketches"] is not JsonArray sketches) o["sketches"] = sketches = [];
        var used = sketches.OfType<JsonObject>().Select(s => (string?)s["id"]).OfType<string>().ToList();
        var made = new List<string>();
        var i = 0;
        foreach (var b in (o["bodies"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var name = Parts.BodyName(b, ++i);
            if (b["sketch"] != null || b["points"] is not JsonArray points) continue;
            var sid = Next(used, "s");
            used.Add(sid);
            var shapes = new JsonArray();
            var cs = new JsonArray();
            var ci = 0;
            void Outline(string shapeId, JsonArray corners)
            {
                var pts = corners.OfType<JsonArray>().Select(p => new Vec2(Parts.Eval(p[0], values, $"{name}'s corners"), Parts.Eval(p[1], values, $"{name}'s corners"))).ToList();
                shapes.Add((JsonNode)ShapeJson(shapeId, new Polyline(pts, true)));
                var k = 0;
                foreach (var p in corners.OfType<JsonArray>())
                {
                    foreach (var (axis, kind) in new[] { (0, "distanceX"), (1, "distanceY") })
                    {
                        var c = new JsonObject
                        {
                            ["id"] = $"c{++ci}", ["kind"] = kind,
                            ["refs"] = new JsonArray(new JsonObject { ["shape"] = shapeId, ["part"] = $"vertex:{k}" }),
                        };
                        var cell = p[axis];
                        if (cell is JsonValue v && v.TryGetValue<double>(out var x)) c["value"] = x;
                        else if (cell is JsonValue sv && sv.TryGetValue<string>(out var f) && f.Trim().Length > 0) c["formula"] = f.Trim();
                        else c["value"] = Parts.Eval(cell, values, $"{name}'s corners");
                        c["driving"] = true;
                        cs.Add((JsonNode)c);
                    }
                    k++;
                }
            }
            Outline("outline", points);
            var holes = new JsonArray();
            var h = 0;
            foreach (var hole in (b["holes"] as JsonArray ?? []).OfType<JsonArray>())
            {
                if (hole.Count < 3) continue;
                var hid = $"hole{++h}";
                Outline(hid, hole);
                holes.Add((JsonNode)hid);
            }
            var sketch = new JsonObject { ["id"] = sid, ["name"] = name, ["plane"] = (string?)b["plane"] ?? "xy" };
            if (b["offset"] is JsonArray { Count: 3 } off) sketch["offset"] = off.DeepClone();
            sketch["shapes"] = shapes;
            sketch["constraints"] = cs;
            sketches.Add((JsonNode)sketch);
            foreach (var key in new[] { "points", "plane", "offset", "holes" }) b.Remove(key);
            b["sketch"] = sid;
            b["shape"] = "outline";
            if (holes.Count > 0) b["holes"] = holes;
            made.Add(sid);
        }
        return (o, made);
    }

    // ------------------------------------------------------------ the part's tree

    /// <summary>
    /// What the edit mode shows of a part's sketch for an instance's values: its shapes solved, its constraints resolved on them (a
    /// formula's value, a dimension's <c>measured</c>), how it stands (<c>status</c>, <c>dof</c>, <c>freeShapes</c>, <c>redundant</c>,
    /// <c>conflicting</c>; <c>problem</c> when it does not solve).
    /// </summary>
    internal static JsonObject Describe(JsonObject sketch, IReadOnlyDictionary<string, double> values)
    {
        var o = new JsonObject();
        var stored = ConstraintsOf(sketch);
        if (ShapesOf(sketch).Count == 0)
        {
            // A new sketch, nothing drawn yet: nothing to solve.
            o["shapes"] = new JsonArray();
            o["constraints"] = new JsonArray();
            o["constraintCount"] = 0;
            o["status"] = "solved";
            o["dof"] = 0;
            o["freeShapes"] = new JsonArray();
            o["redundant"] = new JsonArray();
            o["conflicting"] = new JsonArray();
            return o;
        }
        SketchConstraints.LocalSolve r;
        try { r = SketchConstraints.SolveLocal(ShapesOf(sketch), stored, values); }
        catch (CommandException ex)
        {
            o["shapes"] = sketch["shapes"]?.DeepClone() ?? new JsonArray();
            o["constraints"] = sketch["constraints"]?.DeepClone() ?? new JsonArray();
            o["constraintCount"] = stored.Count;
            o["status"] = "invalid";
            o["problem"] = ex.Message;
            return o;
        }
        var shapes = r.Shapes();
        o["shapes"] = new JsonArray(ShapesOf(sketch).Select(s => (JsonNode)ShapeJson(s.Id, shapes.TryGetValue(s.Id, out var sv) ? sv : s.Shape)).ToArray());
        o["constraints"] = new JsonArray(stored.Select(c =>
        {
            var i = r.S.Stored.IndexOf(c);
            return (JsonNode)SketchConstraints.ResolveLocal(c, shapes, values, i >= 0 && r.Error == null ? r.Measured(i) : null);
        }).ToArray());
        o["constraintCount"] = stored.Count;
        foreach (var (k, v) in r.Status()) o[k] = v?.DeepClone();
        return o;
    }

    // ------------------------------------------------------------ the commands

    /// <summary>The part a command names ({partId}: the part, or a placed part of it) and its sketch ({sketchId}), the definition a copy to change.</summary>
    static (Entity Type, JsonObject Def, JsonObject Sketch) Target(ICommandContext ctx, JsonObject a)
    {
        var (type, def) = PartOf(ctx.Model, (string?)a["partId"]);
        var sid = (string?)a["sketchId"];
        var sketch = Find(def, sid) ?? throw new CommandException($"„{type.Name}” has no sketch „{sid}”.");
        return (type, def, sketch);
    }

    static (Entity Type, JsonObject Def) PartOf(IModelView m, string? id)
    {
        var e = m.Get(id ?? "") ?? throw new CommandException("There is no such part.");
        var type = Parts.IsInstance(e) ? Parts.TypeOf(m, e) ?? throw new CommandException("Its part is gone.") : Parts.Require(m, e.Id);
        var def = (JsonObject)(Parts.DefOf(type) ?? throw new CommandException($"„{type.Name}” is a solid type with no part definition.")).DeepClone();
        return (type, def);
    }

    /// <summary>
    /// A changed definition kept: checked (every sketch solved with the defaults, a conflict refused), every placed part's values
    /// building with it (or refused, naming the first that does not), then written; its instances rebuild from it.
    /// </summary>
    static JsonObject Commit(ICommandContext ctx, Entity type, JsonObject def)
    {
        var done = Parts.Check(def);
        foreach (var e in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(Parts.IsInstance))
        {
            if (e.Attrs["values"] is not JsonObject { Count: > 0 } own) continue;
            try { Parts.Bodies(done, Parts.Values(done, own)); }
            catch (CommandException ex)
            {
                var vals = string.Join(", ", own.Select(kv => $"{kv.Key} = {kv.Value}"));
                throw new CommandException($"„{e.Name}” ({vals}) would not build: {ex.Message} Nothing was changed.");
            }
        }
        var attrs = type.CloneAttrs();
        attrs["def"] = done;
        ctx.Replace(type.With(attrs: attrs));
        return done;
    }

    static JsonObject Item(JsonObject a) => new()
    {
        ["kind"] = (string?)a["kind"], ["refs"] = (a["refs"] as JsonArray)?.DeepClone(), ["value"] = a["value"]?.DeepClone(), ["formula"] = (string?)a["formula"],
        ["driving"] = a["driving"] is JsonValue dv && dv.TryGetValue<bool>(out var driving) ? driving : null,
    };

    static JsonObject Clean(JsonObject c)
    {
        foreach (var k in c.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) c.Remove(k);
        return c;
    }

    /// <summary>{partId, sketchId, kind, refs, value?, formula?, driving?} → {id}; or {partId, sketchId, constraints: [{…}]} → {ids}.</summary>
    public static JsonNode? AddConstraint(ICommandContext ctx, JsonObject a)
    {
        var (type, def, sketch) = Target(ctx, a);
        var many = a["constraints"] as JsonArray;
        var items = many?.Select(x => x as JsonObject ?? throw new CommandException("Each constraint is {kind, refs, …}.")).Select(Item).ToList() ?? [Item(a)];
        if (items.Count == 0) throw new CommandException("Which constraints?");
        var shapes = ShapesOf(sketch).ToDictionary(s => s.Id, s => s.Shape);
        var defaults = Parts.Values(def, null);
        var list = sketch["constraints"] as JsonArray ?? (JsonArray)(sketch["constraints"] = new JsonArray());
        var ids = new List<string>();
        foreach (var item in items)
        {
            var c = Clean(item);
            SketchConstraints.CheckLocal(shapes, c, defaults);
            var id = Next(list.OfType<JsonObject>().Select(x => (string?)x["id"]).OfType<string>(), "c");
            var stored = new JsonObject { ["id"] = id };
            foreach (var (k, v) in c) stored[k] = v?.DeepClone();
            list.Add((JsonNode)stored);
            ids.Add(id);
        }
        Commit(ctx, type, def);
        return many != null ? new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()) } : new JsonObject { ["id"] = ids[0] };
    }

    /// <summary>{partId, sketchId, id, value?, formula?, driving?}: a value takes the place of a formula and the other way round; null clears.</summary>
    public static JsonNode? UpdateConstraint(ICommandContext ctx, JsonObject a)
    {
        var (type, def, sketch) = Target(ctx, a);
        var id = (string?)a["id"];
        var list = sketch["constraints"] as JsonArray ?? [];
        var at = list.OfType<JsonObject>().ToList().FindIndex(c => (string?)c["id"] == id);
        if (at < 0) throw new CommandException("There is no such constraint.");
        var c = (JsonObject)list[at]!.DeepClone();
        if (a.ContainsKey("value")) { c.Remove("value"); if (a["value"] != null) { c["value"] = a["value"]!.DeepClone(); c.Remove("formula"); } }
        if (a.ContainsKey("formula")) { c.Remove("formula"); if (a["formula"] != null) { c["formula"] = a["formula"]!.DeepClone(); c.Remove("value"); } }
        if (a["driving"] != null) c["driving"] = a["driving"]!.DeepClone();
        SketchConstraints.CheckLocal(ShapesOf(sketch).ToDictionary(s => s.Id, s => s.Shape), c, Parts.Values(def, null));
        list[at] = c;
        Commit(ctx, type, def);
        return null;
    }

    /// <summary>{partId, sketchId, id} or {…, ids}: what is left held before, so the shapes stay.</summary>
    public static JsonNode? RemoveConstraint(ICommandContext ctx, JsonObject a)
    {
        var (type, def, sketch) = Target(ctx, a);
        var ids = Args.Ids(a);
        if (ids.Count == 0) throw new CommandException("Which constraints?");
        var list = sketch["constraints"] as JsonArray ?? [];
        foreach (var id in ids)
        {
            var at = list.OfType<JsonObject>().ToList().FindIndex(c => (string?)c["id"] == id);
            if (at < 0) throw new CommandException("There is no such constraint.");
            list.RemoveAt(at);
        }
        Commit(ctx, type, def);
        return null;
    }

    /// <summary>
    /// {partId, sketchId, shapes: [{id?, type, …}]}: the sketch's shapes replaced. A shape keeping its id and its sort is the same
    /// shape moved: the points it moved are the edit's goals and the rest follows the constraints, or the edit is refused with the
    /// constraints that hold it; constraints naming a shape gone (or a part it no longer has) are dropped and listed
    /// (<c>dropped: [{id, kind, reason}]</c>). A shape without an id is new.
    /// </summary>
    public static JsonNode? SetShapes(ICommandContext ctx, JsonObject a)
    {
        var (type, def, sketch) = Target(ctx, a);
        var before = ShapesOf(sketch).ToDictionary(s => s.Id, s => s.Shape);
        var given = a["shapes"] as JsonArray ?? throw new CommandException("Which shapes: {shapes: [{id?, type, …}]}?");
        var named = given.OfType<JsonObject>().Select(s => ((string?)s["id"])?.Trim()).OfType<string>().ToList();
        var now = new List<(string Id, Shape Shape)>();
        foreach (var sx in given)
        {
            var shape = Shape.From(sx) ?? throw new CommandException("A shape is a line, a polyline, a circle or an arc.");
            var id = ((string?)sx!["id"])?.Trim() is { Length: > 0 } s0 ? s0 : Next(named.Concat(before.Keys).Concat(now.Select(s => s.Id)), "p");
            if (!GoodId(id)) throw new CommandException($"A shape's id is letters and digits („{id}”).");
            if (now.Any(s => s.Id == id)) throw new CommandException($"The shape „{id}” is there twice.");
            now.Add((id, shape));
        }
        var dict = now.ToDictionary(s => s.Id, s => s.Shape);
        var defaults = Parts.Values(def, null);
        var kept = new JsonArray();
        var dropped = new JsonArray();
        foreach (var c in ConstraintsOf(sketch))
        {
            if (SketchConstraints.InvalidLocal(dict, c, defaults) is { } why)
                dropped.Add((JsonNode)new JsonObject { ["id"] = (string?)c["id"], ["kind"] = (string?)c["kind"], ["reason"] = why });
            else kept.Add(c.DeepClone());
        }
        var r = SketchConstraints.SolveLocal(now, [.. kept.OfType<JsonObject>()], defaults, before);
        if (r.Error != null) throw new CommandException(r.Error + " Nothing was changed.");
        var solved = r.Shapes();
        sketch["shapes"] = new JsonArray(now.Select(s => (JsonNode)ShapeJson(s.Id, solved.TryGetValue(s.Id, out var sv) ? sv : s.Shape)).ToArray());
        sketch["constraints"] = kept;
        Commit(ctx, type, def);
        var o = new JsonObject { ["ids"] = new JsonArray(now.Select(s => (JsonNode)s.Id).ToArray()) };
        if (dropped.Count > 0) o["dropped"] = dropped;
        return o;
    }

    /// <summary>{id}: the part's bodies of formula corners made sketches with their formulas as dimensions → {sketches: [ids]}.</summary>
    public static JsonNode? ConstrainPart(ICommandContext ctx, JsonObject a)
    {
        var (type, def) = PartOf(ctx.Model, (string?)a["id"]);
        var (made, sketches) = Constrain(def);
        if (sketches.Count == 0) throw new CommandException($"„{type.Name}” has no body of formula corners left: its bodies are drawn from sketches already.");
        Commit(ctx, type, made);
        return new JsonObject { ["sketches"] = new JsonArray(sketches.Select(x => (JsonNode)x).ToArray()) };
    }

    public static void Register(IModuleRegistry r)
    {
        // A part's bodies of formula corners made constrained sketches (§9 point 2): {id} (the part or a placed part of it).
        r.Command("constrainPart", "Constrain part", ConstrainPart);
        // A part's own sketch edited (the edit mode): {partId, sketchId, …}; one undo step each, solved with the defaults, every
        // placed part rebuilding; a conflict, or values of a placed part the sketch cannot take, refused.
        r.Command("partSketchAddConstraint", "Constraint", AddConstraint);
        r.Command("partSketchUpdateConstraint", "Change constraint", UpdateConstraint);
        r.Command("partSketchRemoveConstraint", "Remove constraint", RemoveConstraint);
        r.Command("partSketchSetShapes", "Edit part sketch", SetShapes);
        RegisterEdit(r);
    }

    /// <summary>A sketch's solves by the values its formulas name (declared last, so the catalogue's reading of this file finds the commands' class).</summary>
    sealed class Cache
    {
        public readonly Dictionary<string, (Dictionary<string, Shape>? Shapes, string? Error)> By = [];
        public readonly Queue<string> Order = new();
        public string[]? Names;
        public int Solves;
    }
}
