using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drafting.Cad;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// Studio's parametric parts (docs/plan-studio-parametric.md §3.1, §9; decision 517, stage P0).
///
/// <para><b>The part</b> is a <see cref="StudioModule.TypeClass"/> (<c>LapinSolidType</c>) grown: besides its name and description
/// it may carry a part definition, <c>def</c>: <c>{ifcClass, params, bodies, description?, planSymbol?}</c>, the schema of the light
/// families of decision 515. Each parameter is <c>{key, label, kind (length, number, angle, bool), default}</c> with the optional
/// <c>unit</c>, <c>instance</c>, <c>published</c>, <c>group</c> and <c>description</c> kept for the parameter table (P1); each body a
/// closed outline on an origin plane of the part (xy lying, xz or yz standing) whose corners are numbers or formulas of the
/// parameters (<c>W - T</c>), pushed by a depth (a formula too), set off by an offset, made only when its <c>when</c> holds.</para>
///
/// <para><b>A placed part</b> is an ordinary Studio solid: class <see cref="InstanceClass"/> (a proxy, with <c>exportAs</c> the class the
/// definition names, so it is written to IFC as that class with the part's IfcTypeProduct; <c>studio.setClass</c> changes it as any
/// solid's), <c>operation: "part"</c>, typed by its part through <c>IfcRelDefinesByType</c> as <c>studio.setType</c> types any solid,
/// standing at <c>at: [x, y]</c> and <c>z</c> above its storey, turned by <c>rotation</c> (°), maybe <c>mirror</c>ed, with
/// <c>values</c> of its own over the part's defaults, and Studio's steps (<c>features</c>) done after its bodies. Its body is worked
/// out on every ask, never stored.</para>
///
/// <para><b>Every other Studio solid is a one-step part</b>: its recipe (operation, profile, guide, rail, features) stays on the
/// solid, and a type without <c>def</c> is the named type it always was; nothing of theirs changes. The part tree
/// (<c>studio.partTree</c>) shows both kinds the same way.</para>
/// </summary>
public static class Parts
{
    public const string TypeClass = StudioModule.TypeClass;
    public const string InstanceClass = IfcClass.BuildingElementProxy;
    public const string Operation = "part";
    public const string FileFormat = "lapinpart";
    public const string FileExtension = ".lapinpart";
    public const int FileVersion = 1;
    public static readonly string[] Planes = ["xy", "xz", "yz"];
    public static readonly string[] ParamKinds = ["length", "number", "angle", "bool", "text", "integer"];

    static double? Num(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? (b ? 1 : 0) : Json.Number(n);
    static bool? Flag(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    static List<Param> Params(JsonObject def) =>
        [.. (def["params"] as JsonArray ?? []).OfType<JsonObject>().Select(p => new Param((string)p["key"]!, (string?)p["label"] ?? (string)p["key"]!, (string?)p["kind"] ?? "length", Num(p["default"]) ?? 0, p))];

    /// <summary>
    /// The values a body is built with: the part's defaults, an instance's own over them for its instance parameters, the derived
    /// ones worked out (<see cref="PartParams.Resolve"/>); texts are left out (formulas read numbers). Throws with the first value
    /// that does not hold (out of its bounds, a formula that does not read).
    /// </summary>
    public static Dictionary<string, double> Values(JsonObject def, JsonObject? own, JsonObject? over = null)
    {
        var r = PartParams.Resolve(def, own, over);
        if (r.Problems.Count > 0) throw new CommandException(r.Problems.Values.First());
        return r.Numbers;
    }

    internal static double Eval(JsonNode? n, IReadOnlyDictionary<string, double> values, string what)
    {
        if (n is null) return 0;
        var text = n is JsonValue jv && jv.TryGetValue<double>(out var d) ? d.ToString("R", CultureInfo.InvariantCulture) : n.ToString();
        var (v, error) = Expr.TryEval(text, k => values.TryGetValue(k, out var x) ? x : null);
        return v ?? throw new CommandException($"{what}: {error}");
    }

    /// <summary>A body's local frame: its outline's x and y on the plane, its depth along the plane's normal.</summary>
    static (Vec3 U, Vec3 V, Vec3 N) Frame(string plane) => plane switch
    {
        "xz" => (new Vec3(1, 0, 0), Vec3.UnitZ, new Vec3(0, 1, 0)),
        "yz" => (new Vec3(0, 1, 0), Vec3.UnitZ, new Vec3(1, 0, 0)),
        _ => (new Vec3(1, 0, 0), new Vec3(0, 1, 0), Vec3.UnitZ),
    };

    internal static string BodyName(JsonObject b, int i) => (string?)b["name"] is { Length: > 0 } nm ? nm : $"Body {i}";

    /// <summary>The bodies of a part for some values, in the part's own frame (metres): each with its name and material role.</summary>
    public static List<(string Name, string? Material, Solid Solid)> Bodies(JsonObject def, IReadOnlyDictionary<string, double> values)
    {
        var o = new List<(string, string?, Solid)>();
        var i = 0;
        foreach (var b in (def["bodies"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var name = BodyName(b, ++i);
            if (b["when"] is { } when && Eval(when, values, $"{name}'s condition") == 0) continue;
            Vec3 Offset(JsonNode? o) => o is JsonArray oa && oa.Count == 3 ? new Vec3(Eval(oa[0], values, name), Eval(oa[1], values, name), Eval(oa[2], values, name)) : default;
            var off = Offset(b["offset"]);
            List<Vec3> outer;
            List<IReadOnlyList<Vec3>> holes;
            Vec3 n;
            if (b["sketch"] != null)
            {
                // Drawn from a part's own sketch (§9 point 2): its outline solved with these values, on the sketch's plane.
                var (plane, sketchOffset, ring, inner) = PartSketches.Profile(def, b, values, name);
                var (su, sv, sn) = Frame(plane);
                off += Offset(sketchOffset);
                n = sn;
                outer = [.. ring.Select(p => off + su * p.X + sv * p.Y)];
                holes = [.. inner.Select(h => (IReadOnlyList<Vec3>)h.Select(p => off + su * p.X + sv * p.Y).ToList())];
            }
            else
            {
                var (u, v, pn) = Frame((string?)b["plane"] ?? "xy");
                n = pn;
                List<Vec3> Ring(JsonNode? pts) => [.. (pts as JsonArray ?? []).OfType<JsonArray>().Select(p => off + u * Eval(p[0], values, $"{name}'s corners") + v * Eval(p[1], values, $"{name}'s corners"))];
                outer = Ring(b["points"]);
                holes = (b["holes"] as JsonArray ?? []).Select(h => (IReadOnlyList<Vec3>)Ring(h)).Where(h => h.Count >= 3).ToList();
            }
            if (outer.Count < 3) continue;
            var depth = Eval(b["depth"], values, $"{name}'s depth");
            if (Math.Abs(depth) < 1e-6) continue;
            if (Prism.Extrude(outer, holes, n * depth, "part") is { } solid) o.Add((name, (string?)b["material"], solid));
        }
        return o;
    }

    /// <summary>A part definition as a command or a file gives it, checked: its parameters, bodies whose corners and depths read and make a body with the defaults.</summary>
    public static JsonObject Check(JsonNode? n)
    {
        if (n is not JsonObject o) throw new CommandException("A part is {params, bodies}.");
        var ps = new JsonArray();
        var keys = new List<string>();
        foreach (var p in o["params"] as JsonArray ?? [])
        {
            if (p is not JsonObject po) throw new CommandException("Each parameter has a key of letters and digits (W, Height, HasShelf).");
            var key = PartParams.ValidKey((string?)po["key"]);
            if (keys.Contains(key, StringComparer.OrdinalIgnoreCase)) throw new CommandException($"The parameter „{key}” is there twice.");
            keys.Add(key);
            // The parameter table (P1): kind, default or formula, unit, group, description, instance, published, min, max.
            ps.Add((JsonNode)PartParams.CheckOne(po, key));
        }
        // Derived parameters: their names known, no cycle (its path named); then every value worked out with the defaults.
        var checkedParams = ps.OfType<JsonObject>().ToList();
        PartParams.Order(checkedParams, strict: true);
        var resolved = PartParams.Resolve(new JsonObject { ["params"] = ps.DeepClone() }, null);
        if (resolved.Problems.Count > 0) throw new CommandException($"With its defaults: {resolved.Problems.Values.First()}");
        // A part's own sketches (§9 point 2), checked and solved with the defaults.
        var defaults = resolved.Numbers;
        var sketches = o["sketches"] is JsonArray { Count: > 0 } given ? PartSketches.Check(given, defaults) : null;
        var bodies = new JsonArray();
        foreach (var b in o["bodies"] as JsonArray ?? [])
        {
            if (b is not JsonObject bo) throw new CommandException("Each body is {plane, points, depth}.");
            if (bo["sketch"] is JsonValue sv && sv.TryGetValue<string>(out _))
            {
                bodies.Add((JsonNode)PartSketches.CheckBody(bo, sketches ?? []));
                continue;
            }
            var plane = (string?)bo["plane"] ?? "xy";
            if (!Planes.Contains(plane)) throw new CommandException($"A body's plane is {string.Join(", ", Planes)}.");
            if (bo["points"] is not JsonArray { Count: >= 3 } pts || pts.Any(p => p is not JsonArray { Count: 2 })) throw new CommandException("A body's outline has three corners at least, each [x, y].");
            bodies.Add((JsonNode)bo.DeepClone());
        }
        if (bodies.Count == 0) throw new CommandException("A part has one body at least.");
        var cls = ((string?)o["ifcClass"])?.Trim() is { Length: > 3 } c && c.StartsWith("Ifc", StringComparison.Ordinal) ? c : IfcClass.BuildingElementProxy;
        var def = new JsonObject { ["ifcClass"] = cls, ["params"] = ps };
        if (sketches != null) def["sketches"] = sketches;
        def["bodies"] = bodies;
        if (((string?)o["description"])?.Trim() is { Length: > 0 } desc) def["description"] = desc;
        if (((string?)o["planSymbol"])?.Trim() is { Length: > 0 } sym) def["planSymbol"] = sym;
        // Built with its defaults: every corner, depth and condition reads, and something is made.
        if (Bodies(def, Values(def, null)).Count == 0) throw new CommandException("With its defaults the part makes no body.");
        return def;
    }

    /// <summary>
    /// Drafted closed outlines made a part (a plan's lying, a section's or an elevation's standing): their corners from the lowest
    /// left one, in metres; the right-most corners following W and the top-most H when <paramref name="parameterize"/> (the width and
    /// height stretch the outline), each outline a body pushed by <paramref name="depth"/> (a parameter D).
    /// </summary>
    public static JsonObject FromOutlines(IReadOnlyList<IReadOnlyList<Vec2>> outlines, bool standing, double depth, bool parameterize)
    {
        var all = outlines.SelectMany(o => o).ToList();
        var (x0, y0, x1, y1) = (all.Min(p => p.X), all.Min(p => p.Y), all.Max(p => p.X), all.Max(p => p.Y));
        var (w, h) = (x1 - x0, y1 - y0);
        static JsonNode N(double v) => JsonValue.Create(Math.Round(v, 6))!;
        JsonNode X(double x) => parameterize && Math.Abs(x - x1) < 1e-6 ? "W" : parameterize && Math.Abs(x - x0) < 1e-6 ? N(0) : N(x - x0);
        JsonNode Y(double y) => parameterize && Math.Abs(y - y1) < 1e-6 ? "H" : parameterize && Math.Abs(y - y0) < 1e-6 ? N(0) : N(y - y0);
        var bodies = new JsonArray();
        var i = 0;
        foreach (var o in outlines)
            bodies.Add((JsonNode)new JsonObject
            {
                ["name"] = $"Body {++i}", ["plane"] = standing ? "xz" : "xy",
                ["points"] = new JsonArray(o.Select(p => (JsonNode)new JsonArray(X(p.X), Y(p.Y))).ToArray()),
                ["depth"] = parameterize ? "D" : N(depth),
            });
        var ps = new JsonArray();
        if (parameterize)
        {
            ps.Add((JsonNode)new JsonObject { ["key"] = "W", ["label"] = "Width", ["kind"] = "length", ["default"] = N(w) });
            ps.Add((JsonNode)new JsonObject { ["key"] = "H", ["label"] = standing ? "Height" : "Depth", ["kind"] = "length", ["default"] = N(h) });
            ps.Add((JsonNode)new JsonObject { ["key"] = "D", ["label"] = standing ? "Thickness" : "Height", ["kind"] = "length", ["default"] = N(depth) });
        }
        return new JsonObject { ["params"] = ps, ["bodies"] = bodies };
    }

    // ------------------------------------------------------------ instances

    /// <summary>A Studio solid placed from a part definition.</summary>
    public static bool IsInstance(Entity e) => e.Str("operation") == Operation;

    /// <summary>An element's part type: the <c>LapinSolidType</c> it is typed by, if any.</summary>
    public static Entity? TypeOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { Class: TypeClass } t ? t : null;

    /// <summary>A part type's definition, when it has one (a type without is a plain solid type).</summary>
    public static JsonObject? DefOf(Entity? type) => type?.Attrs["def"] as JsonObject;

    static (Vec2 At, double Rotation, bool Mirror) Pose(Entity e) =>
        (Args.TryPoint(e.Attrs["at"]) ?? default, e.Num("rotation"), e.Bool("mirror"));

    /// <summary>How an instance takes its part's frame into the world: points and directions, and whether it is turned over.</summary>
    static (Func<Vec3, Vec3> Place, Func<Vec3, Vec3> Dir, bool Mirror) Placing(Entity e, IModelView m)
    {
        var (at, rotation, mirror) = Pose(e);
        var z = (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("z");
        var (c, s) = (Math.Cos(rotation * Math.PI / 180), Math.Sin(rotation * Math.PI / 180));
        Vec3 Dir(Vec3 d)
        {
            var x = mirror ? -d.X : d.X;
            return new Vec3(x * c - d.Y * s, x * s + d.Y * c, d.Z);
        }
        Vec3 Place(Vec3 p)
        {
            var x = mirror ? -p.X : p.X;
            return new Vec3(at.X + x * c - p.Y * s, at.Y + x * s + p.Y * c, z + p.Z);
        }
        return (Place, Dir, mirror);
    }

    /// <summary>A placed part's bodies in the world, one geometry part each; Studio's steps are done on each after.</summary>
    public static (List<GeometryPart> Parts, List<string> Problems) Build(Entity e, IModelView m)
    {
        var (parts, problems, _) = BuildShared(e, m);
        return (parts, problems);
    }

    /// <summary>
    /// A placed part's bodies in the world, and the shared body they are placed copies of (docs/plan-instancing.md, decision 520
    /// P3): placed parts of one part with the same values (each instance's own, the defaults, the type's and the derived ones
    /// worked out) share one body, built once (<see cref="SetOf"/>), its key <see cref="SharedKey"/>; each is that body moved and
    /// turned (a mirrored one shares the mirrored body). Null shared when it builds no body.
    /// </summary>
    public static (List<GeometryPart> Parts, List<string> Problems, SharedInstance? Shared) BuildShared(Entity e, IModelView m)
    {
        var type = TypeOf(m, e);
        if (DefOf(type) is not { } def) return ([], ["Its part is gone, or no longer has a definition."], null);
        Dictionary<string, double> values;
        try { values = Values(def, e.Attrs["values"] as JsonObject); }
        catch (CommandException ex) { return ([], [ex.Message], null); }
        var set = SetOf(type!.Id, def, values);
        if (set.Error is { } error) return ([], [error], null);
        var (at, rotation, mirror) = Pose(e);
        // The placed part's own material over every body's, its part's for the bodies without one (forum topic 60).
        var (over, fallback) = (StudioModule.OwnMaterial(m, e), StudioModule.OwnMaterial(m, type));
        var local = set.Local(m, mirror, over, fallback);
        if (local.Count == 0) return ([], [], null);
        var z = (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("z");
        var xf = Xf(at, z, rotation);
        var parts = local.Select(p => p with { Solid = StoredBodies.Place(p.Solid, xf) }).ToList();
        return (parts, [], new SharedInstance(set.KeyOf(mirror, over, fallback), xf) { Local = local });
    }

    /// <summary>A placed part's placement: 12 numbers, the images of x, y, z and the origin (<see cref="StoredBodies.Place"/>), a turn about z and a move.</summary>
    static double[] Xf(Vec2 at, double z, double rotation)
    {
        var (c, s) = (Math.Cos(rotation * Math.PI / 180), Math.Sin(rotation * Math.PI / 180));
        return [c, s, 0, -s, c, 0, 0, 0, 1, at.X, at.Y, z];
    }

    // ------------------------------------------------------------ shared bodies (P3)

    const int BodyCacheSize = 256;

    /// <summary>A part's bodies per set of values, by the part's definition (its JSON node: a changed part is a new node).</summary>
    static readonly ConditionalWeakTable<JsonObject, BodyCache> BodyCaches = new();

    /// <summary>A body turned over x (x → −x): its faces' loops run the other way, their normals mirrored.</summary>
    static Solid Mirrored(Solid s) => new(
        [.. s.Vertices.Select(v => new Vec3(-v.X, v.Y, v.Z))],
        [.. s.Faces.Select(f => f with { Outer = [.. f.Outer.Reverse()], Holes = [.. f.Holes.Select(h => h.Reverse().ToArray())], Normal = new Vec3(-f.Normal.X, f.Normal.Y, f.Normal.Z) })],
        s.Tag);

    /// <summary>Values written one way: keys in order, numbers round-trip.</summary>
    internal static string Canonical(IReadOnlyDictionary<string, double> values) =>
        string.Join(";", values.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Key + "=" + kv.Value.ToString("R", CultureInfo.InvariantCulture)));

    static string Hex(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();

    /// <summary>
    /// The key placed parts share a body by: the part's id, then a hash of its definition and of the values the body is built with
    /// (those its bodies and sketches name, derived ones worked out; a count or a text they never read splits nothing), the same
    /// in every session for the same part and values.
    /// </summary>
    public static string SharedKey(string partId, JsonObject def, IReadOnlyDictionary<string, double> values) => SetOf(partId, def, values).Key;

    /// <summary>A part's bodies for some values, built once per definition and set of values (256 sets kept per definition).</summary>
    internal static BodySet SetOf(string partId, JsonObject def, IReadOnlyDictionary<string, double> values)
    {
        var cache = BodyCaches.GetValue(def, _ => new BodyCache());
        string canonical;
        lock (cache)
        {
            cache.Names ??= PartParams.Sites(def).Where(x => !x.Where.StartsWith("the formula of ", StringComparison.Ordinal))
                .SelectMany(x => PartParams.NamesIn(x.Text)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var names = cache.Names;
            canonical = Canonical(values.Where(kv => names.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));
            if (cache.By.TryGetValue(canonical, out var hit)) return hit;
            cache.Hash ??= Hex(def.ToJsonString());
            var frozen = new Dictionary<string, double>(values, StringComparer.OrdinalIgnoreCase);
            var set = new BodySet($"part:{partId}:{Hex(cache.Hash + "|" + canonical)}", () =>
            {
                Interlocked.Increment(ref cache.Builds);
                try { return (Bodies(def, frozen), null); }
                catch (CommandException ex) { return (null, ex.Message); }
            });
            cache.By[canonical] = set;
            cache.Order.Enqueue(canonical);
            while (cache.Order.Count > BodyCacheSize) cache.By.Remove(cache.Order.Dequeue());
            return set;
        }
    }

    /// <summary>How many sets of values a part's definition has had its bodies built for (the tests' counter).</summary>
    public static int BodyBuilds(JsonObject def) => BodyCaches.TryGetValue(def, out var c) ? Volatile.Read(ref c.Builds) : 0;

    /// <summary>An instance's own values over <paramref name="had"/>: each the part's parameter of that key, checked a number; null takes the default back.</summary>
    static JsonObject Own(Entity type, JsonObject values, JsonObject? had)
    {
        var keys = Params(DefOf(type)!).ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
        var o = had?.DeepClone() as JsonObject ?? new JsonObject();
        foreach (var (k, v) in values)
        {
            if (!keys.TryGetValue(k, out var p)) throw new CommandException($"„{type.Name}” has no parameter „{k}”.");
            if (v == null) { o.Remove(p.Key); continue; }
            // A derived parameter is its formula's; a type parameter is the part's, the same for every placed part.
            if (PartParams.FormulaOf(p.Source) is { } f) throw new CommandException($"{p.Label} is worked out by its formula ({f}): a placed part takes no value of its own for it.");
            if (!PartParams.IsInstance(p.Source)) throw new CommandException($"{p.Label} is a type parameter, one value for every placed part: change it on the part.");
            if (p.Kind == "text") { o[p.Key] = v is JsonValue tv && tv.TryGetValue<string>(out var s) ? s : v.ToJsonString(); continue; }
            var x = Num(v) ?? throw new CommandException($"{p.Label} is a number.");
            if (PartParams.OutOfBounds(p.Source, x) is { } why) throw new CommandException(why);
            o[p.Key] = x;
        }
        return o;
    }

    /// <summary>The values an instance keeps when it takes another part: those the new part has a parameter for.</summary>
    public static JsonObject? Kept(Entity type, JsonObject? values)
    {
        if (values == null || DefOf(type) is not { } def) return null;
        var keys = Params(def).Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var o = new JsonObject();
        foreach (var (k, v) in values) if (keys.Contains(k)) o[k] = v?.DeepClone();
        return o.Count == 0 ? null : o;
    }

    /// <summary>A part type, one with a definition when <paramref name="withDef"/>.</summary>
    public static Entity Require(IModelView m, string? id, bool withDef = true)
    {
        var e = m.Get(id ?? "") is { Class: TypeClass } t ? t : throw new CommandException("There is no such part.");
        if (withDef && DefOf(e) == null) throw new CommandException($"„{e.Name}” is a solid type with no part definition.");
        return e;
    }

    /// <summary>How many elements a part types.</summary>
    public static int Uses(IModelView m, Entity type) => m.RelatedOf(type.Id, IfcClass.RelDefinesByType).Count();

    // ------------------------------------------------------------ the commands (the families' shortcuts call these)

    public static JsonNode? Create(ICommandContext ctx, JsonObject a, string fallbackName)
    {
        var def = Check(a["def"]);
        var e = new Entity(ctx.NewId(), TypeClass, ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : fallbackName, new JsonObject { ["def"] = def });
        ctx.Add(e);
        return new JsonObject { ["id"] = e.Id };
    }

    /// <summary>A part changed ({name?, def?}): its instances follow (their own values kept where it still has the parameter).</summary>
    public static JsonNode? Update(ICommandContext ctx, JsonObject a, Entity e)
    {
        var attrs = e.CloneAttrs();
        if (a["def"] != null) attrs["def"] = Check(a["def"]);
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : null;
        ctx.Replace(e.With(name: name, attrs: attrs));
        return null;
    }

    public static JsonNode? Delete(ICommandContext ctx, Entity e)
    {
        var used = Uses(ctx.Model, e);
        if (used > 0) throw new CommandException($"{used} object{(used == 1 ? " is" : "s are")} of „{e.Name}”: delete them first.");
        ctx.Remove(e.Id);
        return null;
    }

    /// <summary>A part placed: {storeyId, at: [x, y], z?, rotation?, mirror?, values?}.</summary>
    public static JsonNode? Place(ICommandContext ctx, JsonObject a, Entity type)
    {
        var def = DefOf(type)!;
        var storey = Spatial.Require(ctx.Model, (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
        var at = Args.Point(a["at"], "where it stands");
        var attrs = new JsonObject { ["operation"] = Operation, ["at"] = new JsonArray(at.X, at.Y), ["rotation"] = Json.Number(a["rotation"]) ?? 0 };
        if ((string?)def["ifcClass"] is { } cls && cls != InstanceClass) attrs["exportAs"] = cls;
        if (Json.Number(a["z"]) is { } z) attrs["z"] = z;
        if (a["mirror"] is JsonValue mv && mv.TryGetValue<bool>(out var mirror) && mirror) attrs["mirror"] = true;
        if (a["values"] is JsonObject values && Own(type, values, null) is { Count: > 0 } own) attrs["values"] = own;
        var e = new Entity(ctx.NewId(), InstanceClass, type.Name, attrs);
        ctx.Add(e);
        Spatial.Contain(ctx, storey.Id, e.Id);
        ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        return new JsonObject { ["id"] = e.Id };
    }

    /// <summary>Instances' own values ({ids, values}: a value null goes back to the part's default).</summary>
    public static JsonNode? SetValues(ICommandContext ctx, JsonObject a)
    {
        foreach (var id in Args.Ids(a))
        {
            var e = ctx.Model.Get(id) is { } x && IsInstance(x) ? x : throw new CommandException("Not a placed part.");
            var type = TypeOf(ctx.Model, e) is { } t && DefOf(t) != null ? t : throw new CommandException("Its part is gone.");
            var attrs = e.CloneAttrs();
            var own = Own(type, a["values"] as JsonObject ?? [], e.Attrs["values"] as JsonObject);
            if (own.Count == 0) attrs.Remove("values"); else attrs["values"] = own;
            ctx.Replace(e.With(attrs: attrs));
            // Built with them, or refused with why.
            if (Build(ctx.Model.Get(id)!, ctx.Model).Problems is [{ } problem, ..]) throw new CommandException(problem);
        }
        return null;
    }

    /// <summary>Drafted closed outlines of a view made a part ({viewId, ids, name, depth, parameterize? (default true), ifcClass?}); the drafting stays.</summary>
    public static JsonNode? FromSketch(ICommandContext ctx, JsonObject a, string fallbackName)
    {
        var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
        var outlines = new List<IReadOnlyList<Vec2>>();
        foreach (var id in Args.Ids(a))
        {
            var e = ctx.Model.Get(id) ?? throw new CommandException("Something chosen is gone.");
            if (e.Attrs["shape"] is not JsonObject shape) continue;
            if ((string?)shape["type"] == "polyline" && shape["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var closed) && closed)
                outlines.Add([.. Args.StoredPoints(shape["pts"])]);
            else if ((string?)shape["type"] == "circle" && Args.TryPoint(shape["c"]) is { } c && Json.Number(shape["r"]) is { } rad)
                outlines.Add([.. Enumerable.Range(0, Chords.Circle(rad)).Select(k => c + new Vec2(Math.Cos(2 * Math.PI * k / Chords.Circle(rad)), Math.Sin(2 * Math.PI * k / Chords.Circle(rad))) * rad)]);
        }
        if (outlines.Count == 0) throw new CommandException("A part is made of closed outlines: closed polylines, rectangles or circles.");
        var standing = view.Str("kind") is "section" or "elevation";
        var def = FromOutlines(outlines, standing, Json.Number(a["depth"]) ?? 0.5, a["parameterize"] is not JsonValue pv || !pv.TryGetValue<bool>(out var p) || p);
        if ((string?)a["ifcClass"] is { Length: > 3 } cls) def["ifcClass"] = cls;
        def = Check(def);
        var part = new Entity(ctx.NewId(), TypeClass, ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : fallbackName, new JsonObject { ["def"] = def });
        ctx.Add(part);
        return new JsonObject { ["id"] = part.Id, ["def"] = def.DeepClone() };
    }

    /// <summary>A part as a file's text: <c>{format, version, name, def}</c>, as a .lapinpart, or as a .lapinfamily for the families' export.</summary>
    public static JsonObject FileText(Entity e, string format, int version, string extension)
    {
        var def = DefOf(e) ?? throw new CommandException($"„{e.Name}” is a solid drawn from a sketch: only a part of formula bodies goes to a file yet.");
        var o = new JsonObject { ["format"] = format, ["version"] = version, ["name"] = e.Name, ["def"] = def.DeepClone() };
        return new JsonObject { ["text"] = o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ["file"] = e.Name + extension };
    }

    /// <summary>A .lapinpart's text (or a .lapinfamily's, which is read as a part) brought in as a part ({text}).</summary>
    public static JsonNode? Import(ICommandContext ctx, JsonObject a)
    {
        const string Refused = "That is not a part file (.lapinpart or .lapinfamily).";
        JsonNode? n;
        try { n = JsonNode.Parse((string?)a["text"] ?? ""); } catch (JsonException) { throw new CommandException(Refused); }
        if (n is not JsonObject o || (string?)o["format"] is not (FileFormat or Families.FileFormat)) throw new CommandException(Refused);
        if ((string?)o["format"] == FileFormat && Json.Number(o["version"]) is { } v && v > FileVersion) throw new CommandException("That part file was written by a newer Lapin.");
        var e = new Entity(ctx.NewId(), TypeClass, ((string?)o["name"])?.Trim() is { Length: > 0 } nm ? nm : "Part", new JsonObject { ["def"] = Check(o["def"]) });
        ctx.Add(e);
        return new JsonObject { ["id"] = e.Id };
    }

    /// <summary>
    /// A part's bodies for some values: the editor's preview and the flex ghost. <c>{id, values?}</c> or <c>{def, values?}</c>: with
    /// <c>id</c> a part, its definition with its defaults under the values; with <c>id</c> a placed part, its own values under them
    /// (the given values may also flex a type parameter; derived ones are worked out, never given), the part's own sketches solved
    /// with them. → <c>{ok: true, frame: "part" | "world", bodies: [{name, edges, volume}]}</c>, or <c>{ok: false, reason, bodies: []}</c>
    /// when the values do not build (out of a parameter's bounds, a formula or a sketch that does not solve).
    /// <para><b>Edges</b>: each body's face outlines as segments, every edge <c>[x1, y1, z1, x2, y2, z2]</c> in metres (rounded to
    /// 1e-6), an edge shared by two faces listed once per face. Their frame is the part's own (its origin, x along its width) for a
    /// part or a <c>def</c>; for a placed part they are in the <b>world</b>: its point, height above its storey, rotation and mirror
    /// applied, as its body is built. <c>volume</c> is m³ (in either frame).</para>
    /// <para>The families' <c>studio.familyPreview</c> calls this with <paramref name="strict"/>: values that do not build throw,
    /// as they always did.</para>
    /// </summary>
    public static JsonNode Preview(JsonObject a, IModelView m, bool strict = true)
    {
        JsonObject def;
        JsonObject? own = null;
        Func<Vec3, Vec3>? place = null;
        if (a["def"] != null) def = Check(a["def"]);
        else if (m.Get((string?)a["id"] ?? "") is { } x && IsInstance(x))
        {
            // The stored definition (not checked again, so the sketches' solves stay cached across a flex).
            def = DefOf(TypeOf(m, x)) ?? throw new CommandException("Its part is gone, or no longer has a definition.");
            own = x.Attrs["values"] as JsonObject;
            place = Placing(x, m).Place;
        }
        else def = DefOf(Require(m, (string?)a["id"]))!;
        List<(string Name, string? Material, Solid Solid)> bodies;
        try { bodies = Bodies(def, Values(def, own, a["values"] as JsonObject)); }
        catch (CommandException ex) when (!strict)
        {
            return new JsonObject { ["ok"] = false, ["reason"] = ex.Message, ["bodies"] = new JsonArray() };
        }
        var to = place ?? (v => v);
        static JsonNode Edge(Vec3 p, Vec3 q) => new JsonArray(Math.Round(p.X, 6), Math.Round(p.Y, 6), Math.Round(p.Z, 6), Math.Round(q.X, 6), Math.Round(q.Y, 6), Math.Round(q.Z, 6));
        return new JsonObject
        {
            ["ok"] = true,
            ["frame"] = place != null ? "world" : "part",
            ["bodies"] = new JsonArray(bodies.Select(b => (JsonNode)new JsonObject
            {
                ["name"] = b.Name,
                ["edges"] = new JsonArray(b.Solid.Faces.SelectMany(f => f.Outer.Select((v, k) => (v, w: f.Outer[(k + 1) % f.Outer.Length])))
                    .Select(e => Edge(to(b.Solid.Vertices[e.v]), to(b.Solid.Vertices[e.w]))).ToArray()),
                ["volume"] = Math.Round(Measure.Volume(b.Solid), 9),
            }).ToArray()),
        };
    }

    // ------------------------------------------------------------ the part's tree

    static JsonArray V(Vec3 v) => new(Math.Round(v.X, 9), Math.Round(v.Y, 9), Math.Round(v.Z, 9));
    static JsonObject FrameJson(Vec3 origin, Vec3 right, Vec3 up, Vec3 normal) =>
        new() { ["origin"] = V(origin), ["right"] = V(right), ["up"] = V(up), ["normal"] = V(normal) };

    /// <summary>
    /// A part's own sketch as stored (solved with the part's defaults): its shapes (with <c>inProfile</c>, <c>inLoop</c>), constraints
    /// resolved on them, how it stands for the defaults, and its frame with the defaults' offset. The same as <paramref name="described"/>
    /// when the instance's values are the defaults for everything the sketch names.
    /// </summary>
    static JsonObject StoredSketch(JsonObject def, JsonObject sk, JsonObject described, Dictionary<string, double> values, Func<Vec3, Vec3> place, Func<Vec3, Vec3> dir)
    {
        var defaults = Values(def, null);
        var o = new JsonObject();
        if (values.Count == defaults.Count && defaults.All(kv => values.TryGetValue(kv.Key, out var v) && v == kv.Value))
        {
            foreach (var key in new[] { "shapes", "constraints", "constraintCount", "status", "dof", "freeShapes", "redundant", "conflicting", "problem", "frame" })
                if (described[key] is { } n) o[key] = n.DeepClone();
            return o;
        }
        foreach (var (key, val) in PartSketches.Describe(sk, defaults)) o[key] = val?.DeepClone();
        PartSketches.Annotate(def, sk, o);
        o.Remove("bodies");
        var (u, v, n2) = Frame((string?)sk["plane"] ?? "xy");
        Vec3 off = default;
        try { if (sk["offset"] is JsonArray oa && oa.Count == 3) off = new Vec3(Eval(oa[0], defaults, "offset"), Eval(oa[1], defaults, "offset"), Eval(oa[2], defaults, "offset")); }
        catch (CommandException) { }
        o["frame"] = FrameJson(place(off), dir(u), dir(v), dir(n2));
        return o;
    }

    /// <summary>
    /// What the edit mode shows of a Studio solid (docs/plan-studio-parametric.md §3.2): its part, the origin planes, the sketches,
    /// the steps and the parameters. A solid drawn from a sketch: its drafted profile, path, rail… as sketches, its operation and
    /// Studio steps as steps, no parameters yet. A placed part: each body's outline a sketch on its origin plane and a step, the
    /// definition's parameters with the instance's values.
    /// </summary>
    public static JsonObject Tree(IModelView m, IServices? services, Entity e)
    {
        var type = TypeOf(m, e);
        var sketches = new JsonArray();
        var steps = new JsonArray();
        var ps = new JsonArray();
        var k = 0;
        if (IsInstance(e))
        {
            var def = DefOf(type);
            // Never throws: a value that does not hold shows its problem on its parameter.
            var resolved = def != null ? PartParams.Resolve(def, e.Attrs["values"] as JsonObject) : null;
            var values = resolved?.Numbers ?? [];
            var (place, dir, _) = Placing(e, m);
            // The part's own sketches (§9 point 2): solved with this instance's values, their constraints and how they stand. Their
            // shapes are (u, v) on the sketch's plane; the frame takes them to the world, origin + u·right + v·up (place is affine,
            // dir its linear part, so place(off + u·U + v·V) = place(off) + u·dir(U) + v·dir(V)).
            foreach (var sk in (def != null ? PartSketches.SketchesOf(def) : []).OfType<JsonObject>())
            {
                var plane = (string?)sk["plane"] ?? "xy";
                var (u, v, n) = Frame(plane);
                Vec3 off = default;
                try { if (sk["offset"] is JsonArray oa && oa.Count == 3) off = new Vec3(Eval(oa[0], values, "offset"), Eval(oa[1], values, "offset"), Eval(oa[2], values, "offset")); }
                catch (CommandException) { }
                var o = new JsonObject
                {
                    ["id"] = (string?)sk["id"], ["name"] = (string?)sk["name"], ["plane"] = plane, ["local"] = true, ["shapeIds"] = new JsonArray(),
                    ["frame"] = FrameJson(place(off), dir(u), dir(v), dir(n)),
                };
                foreach (var (key, val) in PartSketches.Describe(sk, values)) o[key] = val?.DeepClone();
                // The bodies drawn from it, and on each shape whether it is in their outlines (inProfile) or in any closed one (inLoop).
                PartSketches.Annotate(def!, sk, o);
                // The sketch as the part stores it, solved with the defaults: what editing the sketch edits (the definition), so the
                // sketch editor draws and sends these, its dimensions showing the defaults; `shapes` above stay this object's.
                o["stored"] = StoredSketch(def!, sk, o, values, place, dir);
                sketches.Add((JsonNode)o);
            }
            var i = 0;
            foreach (var b in (def?["bodies"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var name = BodyName(b, ++i);
                if ((string?)b["sketch"] is { } bodySketch)
                {
                    var made = new JsonObject { ["index"] = k++, ["kind"] = "extrude", ["label"] = name, ["sketch"] = bodySketch };
                    if (b["when"] is { } bw)
                    {
                        made["when"] = bw.DeepClone();
                        try { if (Eval(bw, values, name) == 0) made["suppressed"] = true; } catch (CommandException) { }
                    }
                    steps.Add((JsonNode)made);
                    continue;
                }
                var plane = (string?)b["plane"] ?? "xy";
                var (u, v, n) = Frame(plane);
                Vec3 off = default;
                try { if (b["offset"] is JsonArray oa && oa.Count == 3) off = new Vec3(Eval(oa[0], values, name), Eval(oa[1], values, name), Eval(oa[2], values, name)); }
                catch (CommandException) { }
                sketches.Add((JsonNode)new JsonObject
                {
                    ["id"] = $"body:{i}", ["name"] = name, ["plane"] = plane, ["shapeIds"] = new JsonArray(),
                    ["points"] = b["points"]?.DeepClone(), ["frame"] = FrameJson(place(off), dir(u), dir(v), dir(n)),
                    // A body's corners stay formulas until studio.constrainPart makes them a sketch of the part's own with distanceX/Y
                    // constraints (PartSketches): every corner given, nothing left free.
                    ["constraintCount"] = 0, ["constraints"] = new JsonArray(), ["status"] = "solved", ["dof"] = 0,
                });
                var step = new JsonObject { ["index"] = k++, ["kind"] = "extrude", ["label"] = name, ["sketch"] = $"body:{i}" };
                if (b["when"] is { } when)
                {
                    step["when"] = when.DeepClone();
                    try { if (Eval(when, values, name) == 0) step["suppressed"] = true; } catch (CommandException) { }
                }
                steps.Add((JsonNode)step);
            }
            var defaults = def != null ? PartParams.Resolve(def, null) : null;
            foreach (var p in def != null ? Params(def) : [])
            {
                var derived = PartParams.IsDerived(p.Source);
                var text = p.Kind == "text";
                var shown = PartParams.ValueJson(p.Source, resolved!);
                var o = new JsonObject
                {
                    ["key"] = p.Key, ["label"] = p.Label, ["kind"] = p.Kind,
                    // A derived parameter's default: its formula worked out with the defaults.
                    ["default"] = text ? p.Source["default"]?.DeepClone() ?? "" : derived ? (defaults!.Numbers.TryGetValue(p.Key, out var dv) ? Math.Round(dv, 9) : null) : p.Default,
                    ["value"] = shown["value"]?.DeepClone() ?? (text ? null : p.Default),
                    // Every parameter of a light family was an instance's own and shown: so they stay unless the table says otherwise.
                    ["instance"] = Flag(p.Source["instance"]) ?? true, ["published"] = Flag(p.Source["published"]) ?? true,
                    ["derived"] = derived, ["source"] = shown["source"]?.DeepClone(),
                };
                if (PartParams.FormulaOf(p.Source) is { } formula) o["formula"] = formula;
                foreach (var bound in new[] { "min", "max" }) if (Num(p.Source[bound]) is { } bv) o[bound] = bv;
                foreach (var t in new[] { "unit", "group", "description" }) if ((string?)p.Source[t] is { } s) o[t] = s;
                if (shown["problem"] is { } problem) o["problem"] = problem.DeepClone();
                ps.Add((JsonNode)o);
            }
        }
        else if (e.Str("operation") is { } op)
        {
            // A box's bottom or top: its box's sketch.
            var source = op == Thick.BoxSlab ? m.Get(e.Str("box") ?? "") ?? e : e;
            var roles = new List<(string Role, string Id)>();
            foreach (var key in new[] { "profile", "guide", "endProfile", "rail" }) if (source.Str(key) is { } id) roles.Add((key, id));
            foreach (var id in (source.Attrs["profiles"] as JsonArray)?.Select(x => (string?)x).OfType<string>() ?? []) roles.Add(("profiles", id));
            foreach (var (role, id) in roles.DistinctBy(r => r.Id))
            {
                var draft = m.Get(id);
                var o = new JsonObject { ["id"] = id, ["role"] = role };
                var shapeIds = new List<string> { id };
                if (draft != null)
                {
                    if (role is "profile" or "endProfile" or "profiles" && Sketches.OutlineOf(m, draft) is { } outline) shapeIds = [.. outline.Ids];
                    if (role == "profile") shapeIds.AddRange(Sketches.HoleOutlines(m, draft, [draft.Id]).SelectMany(h => h.Ids));
                    var view = Views.Find(m, draft.Str("viewId") ?? "");
                    o["viewId"] = view?.Id;
                    o["plane"] = view?.Str("kind") ?? "plan";
                    if (view != null && Sketches.PlaneOf(m, view, services, Sketches.MidX(Drafts.ShapeOf(draft))) is { } sp) o["frame"] = FrameJson(sp.Origin, sp.Right, sp.Up, sp.Normal);
                }
                else o["missing"] = true;
                o["shapeIds"] = new JsonArray(shapeIds.Distinct().Select(x => (JsonNode)x).ToArray());
                // Its constraints (stage C0), resolved for the edit mode's glyphs.
                var constraints = draft?.Str("viewId") is { } vid ? SketchConstraints.Touching(m, vid, shapeIds) : [];
                o["constraintCount"] = constraints.Count;
                o["constraints"] = constraints;
                // How it stands (C1): derived on every ask, for the degrees-of-freedom chip.
                if (draft?.Str("viewId") is { } sv && SketchConstraints.StatusOf(m, sv, shapeIds) is { } st)
                {
                    o["status"] = st["status"]?.DeepClone();
                    o["dof"] = st["dof"]?.DeepClone();
                }
                sketches.Add((JsonNode)o);
            }
            steps.Add((JsonNode)new JsonObject { ["index"] = k++, ["kind"] = op, ["label"] = OperationLabel(op) });
        }
        // Studio's steps, after the operation or the bodies.
        var f = 0;
        foreach (var feature in (e.Attrs["features"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var step = new JsonObject { ["index"] = k++, ["kind"] = (string?)feature["op"] ?? "step", ["label"] = Features.Label(feature), ["feature"] = f++ };
            if (Features.Off(feature)) step["suppressed"] = true;
            steps.Add((JsonNode)step);
        }
        var tree = new JsonObject
        {
            ["id"] = e.Id, ["name"] = type?.Name ?? e.Name, ["kind"] = IsInstance(e) ? "formula" : "recipe",
            ["planes"] = new JsonArray(Planes.Select(p => (JsonNode)p).ToArray()),
            ["sketches"] = sketches, ["steps"] = steps, ["params"] = ps,
        };
        if (type != null) tree["partTypeId"] = type.Id;
        return tree;
    }

    static string OperationLabel(string op) => op switch
    {
        "followMe" => "Follow Me",
        Thick.Wall => "Wall strip",
        Thick.BoxSlab => "Box bottom or top",
        "body" => "Body",
        "prism" => "Push/pull on a face",
        _ => char.ToUpperInvariant(op[0]) + op[1..],
    };

    public static void Register(IModuleRegistry r)
    {
        // Every part type of the project: the formula ones with their definition, the plain solid types without.
        r.Query("parts", (q, _) => new JsonObject
        {
            ["parts"] = new JsonArray(q.Model.OfClass(TypeClass).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Select(e => (JsonNode)new JsonObject
            {
                ["id"] = e.Id, ["name"] = e.Name, ["description"] = e.Str("description"), ["def"] = DefOf(e)?.DeepClone(), ["uses"] = Uses(q.Model, e),
            }).ToArray()),
        });
        r.Query("partPreview", (q, a) => Preview(a, q.Model, strict: false));
        r.Query("partTree", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") is { } x && x.Str("operation") != null ? x : throw new CommandException("Not a Studio solid or a placed part.");
            return Tree(q.Model, q.Services, e);
        });
        r.Command("createPart", "New part", (ctx, a) => Create(ctx, a, "Part"));
        r.Command("updatePart", "Change part", (ctx, a) => Update(ctx, a, Require(ctx.Model, (string?)a["id"], withDef: false)));
        r.Command("deletePart", "Delete part", (ctx, a) => Delete(ctx, Require(ctx.Model, (string?)a["id"], withDef: false)));
        // A part placed: {typeId, storeyId, at: [x, y], z?, rotation?, mirror?, values?}.
        r.Command("placePart", "Place part", (ctx, a) => Place(ctx, a, Require(ctx.Model, (string?)a["typeId"])));
        r.Command("setPartValues", "Part values", SetValues);
        r.Command("partFromSketch", "Part from sketch", (ctx, a) => FromSketch(ctx, a, "Part"));
        r.Query("exportPart", (q, a) => FileText(Require(q.Model, (string?)a["id"], withDef: false), FileFormat, FileVersion, FileExtension));
        r.Command("importPart", "Import part", Import);
        // A part's own sketches: the conversion of formula corners and their editing (§9 point 2).
        PartSketches.Register(r);
        // The parameter table (P1).
        PartParams.Register(r);
    }

    // The shared bodies' cache (declared here, after the commands, so the catalogue's reading of this file finds the commands' class).

    sealed class BodyCache
    {
        public string? Hash;
        /// <summary>The names the bodies and sketches read (their corners, depths, conditions, offsets, constraints): values outside them build the same body.</summary>
        public HashSet<string>? Names;
        public readonly Dictionary<string, BodySet> By = [];
        public readonly Queue<string> Order = new();
        public int Builds;
    }

    /// <summary>
    /// One set of values' bodies in the part's own frame, built once on first ask (a failure kept as its reason), with the
    /// geometry parts made of them (by the materials' colours as they stand) and their mirror image, made when first asked.
    /// </summary>
    internal sealed class BodySet(string key, Func<(List<(string Name, string? Material, Solid Solid)>? Bodies, string? Error)> build)
    {
        readonly Lazy<(List<(string Name, string? Material, Solid Solid)>? Bodies, string? Error)> _built = new(build, LazyThreadSafetyMode.ExecutionAndPublication);
        readonly Dictionary<(bool Mirror, string? Over, string? Fallback), (string Signature, IReadOnlyList<GeometryPart> Parts)> _local = [];

        public string Key { get; } = key;
        public string? Error => _built.Value.Error;

        /// <summary>
        /// The shared body's key: turned over, and in the material the placed part gives every body (<paramref name="over"/>), else
        /// with its type's for the bodies without one (<paramref name="fallback"/>; forum topic 60).
        /// </summary>
        public string KeyOf(bool mirror, string? over = null, string? fallback = null) =>
            (mirror ? Key + ":m" : Key) + (over != null ? "@" + over : fallback != null ? "~" + fallback : "");

        /// <summary>
        /// The bodies as geometry parts in the part's frame (turned over when mirrored): the same list for as long as the materials
        /// are. Each in its own material (a name of the project's), unless <paramref name="over"/> (a material id) takes them all;
        /// one without takes <paramref name="fallback"/>.
        /// </summary>
        public IReadOnlyList<GeometryPart> Local(IModelView m, bool mirror, string? over = null, string? fallback = null)
        {
            var bodies = _built.Value.Bodies ?? [];
            Entity? Id(string? id) => id != null && m.Get(id) is { Class: IfcClass.Material } x ? x : null;
            var (overMat, fallbackMat) = (Id(over), Id(fallback));
            var mats = bodies.Select(b => overMat ?? (b.Material != null ? Materials.Find(m, b.Material) : null) ?? fallbackMat).ToList();
            var signature = string.Join("|", mats.Select(x => x == null ? "" : x.Id + "=" + x.Str("color")));
            lock (this)
            {
                if (_local.TryGetValue((mirror, over, fallback), out var k) && k.Signature == signature) return k.Parts;
                var parts = bodies.Select((b, i) => new GeometryPart(b.Name, mats[i]?.Str("color") ?? "#b9b2a6", mirror ? Mirrored(b.Solid) : b.Solid, mats[i]?.Id, "part:" + b.Name)).ToList();
                _local[(mirror, over, fallback)] = (signature, parts);
                return parts;
            }
        }
    }

    /// <summary>A parameter of a definition as read (declared last, so the catalogue's reading of this file finds the commands' class).</summary>
    sealed record Param(string Key, string Label, string Kind, double Default, JsonObject Source);
}

/// <summary>
/// The light families of decision 515 made parts (docs/plan-studio-parametric.md §9): a saved <c>LapinFamily</c> becomes a
/// <c>LapinSolidType</c> with the same id, name and definition; each <c>LapinFamilyInstance</c> a placed part (a proxy Studio
/// solid of <c>operation: "part"</c>) with the same id, point, height, rotation, mirror, values and class, typed by its part through
/// one <c>IfcRelDefinesByType</c> per part (its GlobalId derived from the part's, so opening twice gives the same one).
/// </summary>
public sealed class FamilyPartUpgrade : IProjectUpgrade
{
    public const string OldClass = "LapinFamily";
    public const string OldInstanceClass = "LapinFamilyInstance";

    public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
    {
        if (!entities.Any(e => e.Class is OldClass or OldInstanceClass)) return (entities, relations);
        var families = entities.Where(e => e.Class == OldClass).Select(e => e.Id).ToHashSet();
        var typed = new Dictionary<string, List<string>>();
        var es = entities.Select(e =>
        {
            if (e.Class == OldClass) return new Entity(e.Id, Parts.TypeClass, e.Name, e.CloneAttrs(), e.PredefinedType);
            if (e.Class != OldInstanceClass) return e;
            var attrs = e.CloneAttrs();
            var family = e.Str("familyId");
            attrs.Remove("familyId");
            attrs["operation"] = Parts.Operation;
            if (attrs["exportAs"] is JsonValue x && x.TryGetValue<string>(out var cls) && cls == Parts.InstanceClass) attrs.Remove("exportAs");
            if (family != null && families.Contains(family)) (typed.TryGetValue(family, out var l) ? l : typed[family] = []).Add(e.Id);
            return new Entity(e.Id, Parts.InstanceClass, e.Name, attrs, e.PredefinedType);
        }).ToList();
        var rs = relations.ToList();
        foreach (var (family, ids) in typed)
            rs.Add(new Relation(GlobalId.FromGuid(new Guid(Md5.HashData(Encoding.UTF8.GetBytes("lapin-family-part:" + family)))), IfcClass.RelDefinesByType, family, ids));
        return (es, rs);
    }
}
