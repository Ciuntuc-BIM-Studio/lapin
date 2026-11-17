using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// Native elements held by a part (audit S6, docs/plan-studio-parametric.md §10): walls, floors, furniture, MEP equipment and
/// whatever else stands on a storey, kept in the part's definition (<c>def.members</c>) and built again for every placed part.
/// <para>
/// <b>The definition.</b> Each member is a snapshot of the element as it stood in the placed part it was taken from:
/// <c>{key, class, name, predefinedType?, attrs, frame: [x, y, z, degrees, mirrored], type?, material?, usage?, openings?, drives?}</c>.
/// <c>frame</c> is where that placed part stood (its point, its height over its storey, its turn and whether it was mirrored), so
/// a member is never turned into the part's own coordinates: it is carried from its frame to each placed part's by the platform's
/// own move (<see cref="ElementTransform.Run"/>), which knows every module's elements (a wall's layers turned over by a mirror,
/// its openings with it). <c>type</c> and <c>material</c> name the project's type and material; <c>usage</c> is a wall's own layer
/// set usage; <c>openings</c> the openings it hosts with the doors or windows filling them. <c>drives</c> maps an attribute to a
/// formula of the part's parameters (<c>{"height": "H"}</c>; <c>length</c> moves a path element's end along its axis). The joins
/// among members are <c>def.memberJoins</c>, by key.
/// </para>
/// <para>
/// <b>A placed part's members</b> are real elements, aggregated by it (<c>IfcRelAggregates</c>, as a steel hall's members), each with
/// <c>partKey</c>; the placed part keeps their ids by slot (<c>memberIds</c>: the key, <c>key/o0</c> an opening, <c>key/o0/f0</c> its
/// door, <c>key/u</c> a usage) so they keep their GlobalIds when built again, and <c>membersBuilt</c>, what they were built from.
/// After every command (<see cref="IDrivenElements.After"/>) a member edited by any command is written back into the definition
/// and every placed part whose definition, values, place or storey changed is built again, in the same undo step.
/// </para>
/// </summary>
public static class PartMembers
{
    public const string KeyAttr = "partKey";
    public const string IdsAttr = "memberIds";
    public const string BuiltAttr = "membersBuilt";

    /// <summary>Set while members are being built: the generic commands this calls do not ask the module about them.</summary>
    [ThreadStatic] static bool building;

    public static JsonArray MembersOf(JsonObject? def) => def?["members"] as JsonArray ?? [];
    public static bool HasMembers(JsonObject? def) => MembersOf(def).Count > 0;

    /// <summary>The placed part a member belongs to: the one that aggregates it and keeps it in its slots.</summary>
    public static Entity? OwnerOf(IModelView m, Entity e) =>
        e.Str(KeyAttr) != null && m.RelatingOf(e.Id, IfcClass.RelAggregates) is { } p && Parts.IsInstance(p) && Slots(p).ContainsValue(e.Id) ? p : null;

    static Dictionary<string, string> Slots(Entity part) =>
        (part.Attrs[IdsAttr] as JsonObject ?? []).Where(kv => kv.Value is JsonValue).ToDictionary(kv => kv.Key, kv => (string)kv.Value!);

    /// <summary>Whether an element can be a part's member: one of the project's own standing on a storey, not hosted (it comes with its host), not a part or a Studio solid itself.</summary>
    public static string? Refusal(IModelView m, Entity e)
    {
        if (Parts.IsInstance(e)) return $"„{e.Name}” is a placed part: parts do not hold parts yet.";
        if (e.Str("operation") != null) return $"„{e.Name}” is a Studio solid: draw it as a body of the part instead.";
        if (StoredBodies.Has(e) || e.Str(Links.Key) != null) return $"„{e.Name}” came from a file: only the project's own elements can be held by a part.";
        if (Spatial.HostOf(m, e) != null || e.Class == IfcClass.OpeningElement) return $"„{e.Name}” is in a wall: choose its wall, which brings it.";
        if (e.Class == IfcClass.Annotation || e.Str("viewId") != null || Spatial.StoreyOf(m, e.Id) == null) return $"„{e.Name}” does not stand on a storey.";
        if (e.Str(KeyAttr) != null && OwnerOf(m, e) != null) return $"„{e.Name}” is held by a part already.";
        if (Groups.Of(m, e.Id) != null) return $"„{e.Name}” is in a group: take it out first.";
        return null;
    }

    // ------------------------------------------------------------ frames

    /// <summary>Where a placed part stands: [x, y, z over its storey, degrees, mirrored (0 or 1)].</summary>
    public static double[] FrameOf(Entity part)
    {
        var at = Args.TryPoint(part.Attrs["at"]) ?? default;
        return [at.X, at.Y, part.Num("z"), part.Num("rotation"), part.Bool("mirror") ? 1 : 0];
    }

    static double[] FrameOf(JsonNode? n)
    {
        var x = (n as JsonArray ?? []).Select(v => Json.Number(v) ?? 0).ToList();
        while (x.Count < 5) x.Add(0);
        return [.. x.Take(5)];
    }

    /// <summary>
    /// The plan placement that takes what stands in a part placed at <paramref name="from"/> to where it stands in one placed at
    /// <paramref name="to"/>. A part goes to the world as at + R(θ)·M(p), M turning x over when mirrored; one of the two mirrored and
    /// the other not makes a reflection about the line at 90° + (θfrom + θto) / 2, else a turn by θto − θfrom.
    /// </summary>
    public static Placement2D Between(double[] from, double[] to)
    {
        double R(double d) => d * Math.PI / 180;
        Vec2 Rot(Vec2 v, double a) => new(v.X * Math.Cos(a) - v.Y * Math.Sin(a), v.X * Math.Sin(a) + v.Y * Math.Cos(a));
        var (mf, mt) = (from[4] != 0, to[4] != 0);
        Vec2 Point(Vec2 q)
        {
            var local = Rot(q - new Vec2(from[0], from[1]), -R(from[3]));
            if (mf) local = new Vec2(-local.X, local.Y);
            if (mt) local = new Vec2(-local.X, local.Y);
            return new Vec2(to[0], to[1]) + Rot(local, R(to[3]));
        }
        return mf != mt ? new Placement2D(Point, 0, true, Math.PI / 2 + (R(to[3]) + R(from[3])) / 2) : new Placement2D(Point, R(to[3] - from[3]), false);
    }

    // ------------------------------------------------------------ snapshots

    static JsonObject Plain(Entity e)
    {
        var a = e.CloneAttrs();
        a.Remove(KeyAttr);
        return a;
    }

    /// <summary>A member of a placed part as the definition keeps it, in that part's frame; its drives kept from <paramref name="had"/>.</summary>
    public static JsonObject Snapshot(IModelView m, Entity part, Entity e, string key, JsonObject? had)
    {
        var o = new JsonObject { ["key"] = key, ["class"] = e.Class, ["name"] = e.Name };
        if (e.PredefinedType != null) o["predefinedType"] = e.PredefinedType;
        o["attrs"] = Plain(e);
        o["frame"] = new JsonArray([.. FrameOf(part).Select(x => (JsonNode)Math.Round(x, 9))]);
        if (m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } type) o["type"] = type.Id;
        if (m.RelatingOf(e.Id, IfcClass.RelAssociatesMaterial) is { } mat)
        {
            if (mat.Class == IfcClass.MaterialLayerSetUsage)
            {
                var usage = new JsonObject { ["class"] = mat.Class, ["name"] = mat.Name, ["attrs"] = mat.CloneAttrs() };
                if (mat.PredefinedType != null) usage["predefinedType"] = mat.PredefinedType;
                usage["rels"] = new JsonArray([.. m.RelationsOf(mat.Id).Where(r => r.Related.Contains(mat.Id)).Select(r => (JsonNode)new JsonObject { ["class"] = r.Class, ["relating"] = r.Relating })]);
                o["usage"] = usage;
            }
            else o["material"] = mat.Id;
        }
        var openings = new JsonArray();
        foreach (var op in m.RelatedOf(e.Id, IfcClass.RelVoidsElement).ToList())
        {
            var oo = Bare(op);
            if (Spatial.StoreyOf(m, op.Id) != null) oo["storey"] = true;
            var fills = new JsonArray();
            foreach (var f in m.RelatedOf(op.Id, IfcClass.RelFillsElement).ToList())
            {
                var fo = Bare(f);
                if (m.RelatingOf(f.Id, IfcClass.RelDefinesByType) is { } ft) fo["type"] = ft.Id;
                fills.Add((JsonNode)fo);
            }
            if (fills.Count > 0) oo["fills"] = fills;
            openings.Add((JsonNode)oo);
        }
        if (openings.Count > 0) o["openings"] = openings;
        if (had?["drives"] is JsonObject drives && drives.Count > 0) o["drives"] = drives.DeepClone();
        return o;
    }

    static JsonObject Bare(Entity e)
    {
        var o = new JsonObject { ["class"] = e.Class, ["name"] = e.Name, ["attrs"] = Plain(e) };
        if (e.PredefinedType != null) o["predefinedType"] = e.PredefinedType;
        return o;
    }

    /// <summary>The joins among a placed part's members, by key.</summary>
    static JsonArray JoinsOf(IModelView m, Entity part)
    {
        var keyOf = Slots(part).Where(kv => !kv.Key.Contains('/')).ToDictionary(kv => kv.Value, kv => kv.Key);
        var o = new JsonArray();
        foreach (var r in keyOf.Keys.SelectMany(id => m.RelationsOf(id, IfcClass.RelConnectsPathElements)).DistinctBy(r => r.Id))
            if (keyOf.ContainsKey(r.Relating) && r.Related.All(keyOf.ContainsKey))
            {
                var j = new JsonObject { ["relating"] = keyOf[r.Relating], ["related"] = new JsonArray([.. r.Related.Select(x => (JsonNode)keyOf[x])]) };
                if (r.Attrs != null) j["attrs"] = r.Attrs.DeepClone();
                o.Add((JsonNode)j);
            }
        return o;
    }

    // ------------------------------------------------------------ the definition's members checked

    /// <summary>A definition's members as a command or a file gives them, checked: keys, classes, attributes, frames, drives reading with the defaults.</summary>
    public static JsonArray Check(JsonArray given, IReadOnlyDictionary<string, double> defaults)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var o = new JsonArray();
        foreach (var n in given)
        {
            if (n is not JsonObject mo || (string?)mo["key"] is not { Length: > 0 } key || (string?)mo["class"] is not { Length: > 3 } cls || !cls.StartsWith("Ifc", StringComparison.Ordinal) || mo["attrs"] is not JsonObject)
                throw new CommandException("Each member of a part is {key, class, attrs, frame}.");
            if (!keys.Add(key)) throw new CommandException($"The member „{key}” is there twice.");
            var c = (JsonObject)mo.DeepClone();
            c["frame"] = new JsonArray([.. FrameOf(mo["frame"]).Select(x => (JsonNode)x)]);
            if (c["drives"] is JsonObject drives)
                foreach (var (attr, formula) in drives.ToList())
                {
                    if (formula is null) { drives.Remove(attr); continue; }
                    DriveTarget(c, attr);
                    Parts.Eval(formula, defaults, $"{(string?)mo["name"] ?? key}'s {attr}");
                }
            if (c["drives"] is JsonObject { Count: 0 }) c.Remove("drives");
            o.Add((JsonNode)c);
        }
        return o;
    }

    public static JsonArray CheckJoins(JsonArray given, JsonArray members)
    {
        var keys = members.OfType<JsonObject>().Select(x => (string?)x["key"]).OfType<string>().ToHashSet();
        var o = new JsonArray();
        foreach (var j in given.OfType<JsonObject>())
            if ((string?)j["relating"] is { } a && keys.Contains(a) && j["related"] is JsonArray rel && rel.All(x => (string?)x is { } k && keys.Contains(k)))
                o.Add(j.DeepClone());
        return o;
    }

    /// <summary>What a drive may set: <c>length</c> on a straight path element, or a numeric attribute the member has.</summary>
    static void DriveTarget(JsonObject member, string attr)
    {
        var attrs = member["attrs"] as JsonObject ?? [];
        if (attr == "length")
        {
            if (attrs["start"] is JsonArray && attrs["end"] is JsonArray && (Json.Number(attrs[PathCurve.Key]) ?? 0) == 0) return;
            throw new CommandException($"„{(string?)member["name"]}” has no straight axis whose length a parameter could drive.");
        }
        if (attr is "start" or "end" or "at" or "rotation" or "contour" || Json.Number(attrs[attr]) == null)
            throw new CommandException($"„{(string?)member["name"]}” has no number „{attr}” a parameter could drive (its length, height or another value of its own).");
    }

    /// <summary>A member's attributes with its drives worked out for these values.</summary>
    static JsonObject Driven(JsonObject member, IReadOnlyDictionary<string, double> values)
    {
        var a = (JsonObject)member["attrs"]!.DeepClone();
        if (member["drives"] is not JsonObject drives) return a;
        foreach (var (attr, formula) in drives)
        {
            if (formula is null) continue;
            var v = Math.Round(Parts.Eval(formula, values, $"{(string?)member["name"]}'s {attr}"), 9);
            if (attr == "length")
            {
                if (a["start"] is not JsonArray || a["end"] is not JsonArray || v <= 0) continue;
                var (s, e) = (Args.PointOf(a["start"]), Args.PointOf(a["end"]));
                if ((e - s).Length < 1e-9) continue;
                var end = s + (e - s).Normalized() * v;
                a["end"] = new JsonArray(end.X, end.Y);
            }
            else a[attr] = v;
        }
        return a;
    }

    // ------------------------------------------------------------ building

    /// <summary>What a placed part's members are built from: its definition's members and joins, its values, place and storey.</summary>
    static string Signature(IModelView m, Entity part, JsonObject def, IReadOnlyDictionary<string, double> values)
    {
        var text = new StringBuilder();
        text.Append(MembersOf(def).ToJsonString()).Append('|').Append((def["memberJoins"] as JsonArray)?.ToJsonString()).Append('|');
        text.Append(Parts.Canonical(values)).Append('|');
        text.Append(string.Join(",", FrameOf(part).Select(x => x.ToString("R", CultureInfo.InvariantCulture)))).Append('|');
        text.Append(Spatial.StoreyOf(m, part.Id)?.Id);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..16].ToLowerInvariant();
    }

    /// <summary>Whether a placed part's members stand as its definition says: built from what it is now, each slot its own.</summary>
    static bool UpToDate(IModelView m, Entity part, JsonObject? def, IReadOnlyDictionary<string, double>? values)
    {
        var slots = Slots(part);
        if (def == null || !HasMembers(def)) return slots.Count == 0;
        if (values == null || part.Str(BuiltAttr) != Signature(m, part, def, values)) return false;
        return slots.All(kv => Mine(m, part, kv.Key, kv.Value, slots));
    }

    /// <summary>Whether a slot's entity is this placed part's own: a member it aggregates under its key, or what such a member hosts or uses.</summary>
    static bool Mine(IModelView m, Entity part, string slot, string id, IReadOnlyDictionary<string, string> slots)
    {
        if (m.Get(id) is not { } x) return false;
        if (!slot.Contains('/')) return x.Str(KeyAttr) == slot && m.RelatingOf(id, IfcClass.RelAggregates)?.Id == part.Id;
        var key = slot.Split('/')[0];
        if (!slots.TryGetValue(key, out var top) || !Mine(m, part, key, top, slots)) return false;
        if (slot.EndsWith("/u", StringComparison.Ordinal)) return m.RelatedOf(id, IfcClass.RelAssociatesMaterial).Any(w => w.Id == top);
        return Spatial.HostOf(m, x)?.Id == top;
    }

    /// <summary>The members a placed part has now, by slot, taken away; each id kept for the one built in its slot.</summary>
    static Dictionary<string, string> Clear(ICommandContext ctx, Entity part)
    {
        var m = ctx.Model;
        var slots = Slots(part);
        var kept = new Dictionary<string, string>();
        foreach (var (slot, id) in slots)
        {
            // An id another placed part holds (this one a copy of it) is not this one's: its slot is built under a new id.
            if (m.Get(id) != null && !Mine(m, part, slot, id, slots)) continue;
            kept[slot] = id;
        }
        foreach (var (slot, id) in kept.OrderBy(kv => kv.Key.Count(ch => ch == '/')).ToList()) if (m.Get(id) != null) ctx.Remove(id);
        return kept;
    }

    /// <summary>A placed part's members built again from its definition, under the ids their slots had.</summary>
    public static void Build(ICommandContext ctx, Entity part)
    {
        var m = ctx.Model;
        var type = Parts.TypeOf(m, part);
        var def = Parts.DefOf(type);
        Dictionary<string, double>? values = null;
        if (def != null && HasMembers(def))
            try { values = Parts.Values(def, part.Attrs["values"] as JsonObject); }
            catch (CommandException) { return; } // its values do not build: its members stay as they were, the part tells why (Problems)
        var was = building;
        building = true;
        try
        {
            var kept = Clear(ctx, part);
            var attrs = m.Get(part.Id)!.CloneAttrs();
            if (def == null || values == null || !HasMembers(def) || Spatial.StoreyOf(m, part.Id) is not { } storey)
            {
                attrs.Remove(IdsAttr);
                attrs.Remove(BuiltAttr);
                ctx.Replace(m.Get(part.Id)!.With(attrs: attrs));
                return;
            }
            var slots = new JsonObject();
            string Id(string slot)
            {
                var id = kept.TryGetValue(slot, out var k) && m.Get(k) == null ? k : ctx.NewId();
                slots[slot] = id;
                return id;
            }
            var to = FrameOf(part);
            var byFrame = new Dictionary<string, List<string>>();
            var keyed = new Dictionary<string, string>();
            foreach (var mo in MembersOf(def).OfType<JsonObject>())
            {
                var key = (string)mo["key"]!;
                var a = Driven(mo, values);
                a[KeyAttr] = key;
                // A pin keeps the user's own edits away; the part's build moves it all the same, and pins it again.
                a.Remove(Pins.Key);
                var e = new Entity(Id(key), (string)mo["class"]!, (string?)mo["name"] ?? key, a, (string?)mo["predefinedType"]);
                ctx.Add(e);
                Spatial.Contain(ctx, storey.Id, e.Id);
                if ((string?)mo["type"] is { } t && m.Get(t) != null) ctx.Relate(IfcClass.RelDefinesByType, t, e.Id);
                if ((string?)mo["material"] is { } mat && m.Get(mat) != null) ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelAssociatesMaterial, mat, [e.Id]));
                if (mo["usage"] is JsonObject uo)
                {
                    var usage = new Entity(Id(key + "/u"), (string?)uo["class"] ?? IfcClass.MaterialLayerSetUsage, (string?)uo["name"] ?? "", (uo["attrs"] as JsonObject)?.DeepClone() as JsonObject ?? [], (string?)uo["predefinedType"]);
                    ctx.Add(usage);
                    foreach (var r in (uo["rels"] as JsonArray ?? []).OfType<JsonObject>())
                        if ((string?)r["class"] is { } rc && (string?)r["relating"] is { } rr && m.Get(rr) != null) ctx.Relate(rc, rr, usage.Id);
                    ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelAssociatesMaterial, usage.Id, [e.Id]));
                }
                var oi = 0;
                foreach (var oo in (mo["openings"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    var slot = $"{key}/o{oi++}";
                    var op = new Entity(Id(slot), (string?)oo["class"] ?? IfcClass.OpeningElement, (string?)oo["name"] ?? "", (JsonObject)oo["attrs"]!.DeepClone(), (string?)oo["predefinedType"]);
                    ctx.Add(op);
                    ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, e.Id, [op.Id]));
                    if (oo["storey"] is JsonValue sv && sv.TryGetValue<bool>(out var onStorey) && onStorey) Spatial.Contain(ctx, storey.Id, op.Id);
                    var fi = 0;
                    foreach (var fo in (oo["fills"] as JsonArray ?? []).OfType<JsonObject>())
                    {
                        var fill = new Entity(Id($"{slot}/f{fi++}"), (string?)fo["class"] ?? IfcClass.Door, (string?)fo["name"] ?? "", (JsonObject)fo["attrs"]!.DeepClone(), (string?)fo["predefinedType"]);
                        ctx.Add(fill);
                        ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelFillsElement, op.Id, [fill.Id]));
                        Spatial.Contain(ctx, storey.Id, fill.Id);
                        if ((string?)fo["type"] is { } ft && m.Get(ft) != null) ctx.Relate(IfcClass.RelDefinesByType, ft, fill.Id);
                    }
                }
                ctx.Relate(IfcClass.RelAggregates, part.Id, e.Id);
                var fk = string.Join(",", FrameOf(mo["frame"]).Select(x => x.ToString("R", CultureInfo.InvariantCulture)));
                (byFrame.TryGetValue(fk, out var l) ? l : byFrame[fk] = []).Add(e.Id);
                keyed[key] = e.Id;
            }
            foreach (var j in (def["memberJoins"] as JsonArray ?? []).OfType<JsonObject>())
                if ((string?)j["relating"] is { } a && keyed.TryGetValue(a, out var ra) && j["related"] is JsonArray rel && rel.All(x => (string?)x is { } k && keyed.ContainsKey(k)))
                    ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelConnectsPathElements, ra, [.. rel.Select(x => keyed[(string)x!])], (j["attrs"] as JsonObject)?.DeepClone() as JsonObject));
            // From the frame each was taken in to this part's: the platform's move, which carries every module's element whole.
            foreach (var (fk, ids) in byFrame)
            {
                var from = fk.Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                var t = Between(from, to);
                var identity = Math.Abs(t.Turn) < 1e-12 && !t.Mirrored && (t.Point(new Vec2(0, 0)) - new Vec2(0, 0)).Length < 1e-12 && (t.Point(new Vec2(1, 0)) - new Vec2(1, 0)).Length < 1e-12;
                if (!identity) ElementTransform.Run(ctx, new JsonObject { ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["op"] = "move" }, t);
                var dz = to[2] - from[2];
                if (Math.Abs(dz) > 1e-9) foreach (var id in ids) if (m.Get(id) is { } placed) ElementRise.Lift(ctx, placed, dz);
            }
            // Pinned again where the definition says so.
            foreach (var mo in MembersOf(def).OfType<JsonObject>())
                if (mo["attrs"]?[Pins.Key] is JsonValue pv && pv.TryGetValue<bool>(out var pinned) && pinned && m.Get(keyed[(string)mo["key"]!]) is { } pe)
                {
                    var pa = pe.CloneAttrs();
                    pa[Pins.Key] = true;
                    ctx.Replace(pe.With(attrs: pa));
                }
            attrs = m.Get(part.Id)!.CloneAttrs();
            attrs[IdsAttr] = slots;
            attrs[BuiltAttr] = Signature(m, part, def, values);
            ctx.Replace(m.Get(part.Id)!.With(attrs: attrs));
        }
        finally { building = was; }
    }

    // ------------------------------------------------------------ the commands

    static (Entity Part, Entity Type, JsonObject Def) PlacedPart(IModelView m, string? id)
    {
        var part = m.Get(id ?? "") is { } x && Parts.IsInstance(x) ? x : throw new CommandException("Choose a placed part: its members are taken from where it stands.");
        var type = Parts.TypeOf(m, part) ?? throw new CommandException("Its part is gone.");
        var def = (JsonObject)(Parts.DefOf(type) ?? throw new CommandException($"„{type.Name}” has no part definition.")).DeepClone();
        return (part, type, def);
    }

    static string FreeKey(JsonObject def, string cls)
    {
        var used = MembersOf(def).OfType<JsonObject>().Select(x => (string?)x["key"]).OfType<string>().ToHashSet();
        var stem = cls.StartsWith("Ifc", StringComparison.Ordinal) ? cls[3..].ToLowerInvariant() : "member";
        for (var i = 1; ; i++) if (!used.Contains($"{stem}{i}")) return $"{stem}{i}";
    }

    /// <summary>
    /// {partId, ids}: elements standing in the model made members of the part, taken from the placed part <c>partId</c> (they become its
    /// members, keeping their ids; every other placed part of the part gets them too). → {keys}.
    /// </summary>
    public static JsonNode? Add(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        var (part, type, def) = PlacedPart(m, (string?)a["partId"]);
        var ids = Args.Ids(a).Where(id => id != part.Id).Distinct().ToList();
        if (ids.Count == 0) throw new CommandException("Choose the elements the part is to hold.");
        if (Spatial.StoreyOf(m, part.Id) is not { } storey) throw new CommandException($"„{part.Name}” stands on no storey.");
        var members = MembersOf(def);
        var keys = new JsonArray();
        var slots = part.Attrs[IdsAttr] as JsonObject is { } s ? (JsonObject)s.DeepClone() : [];
        foreach (var id in ids)
        {
            var e = m.Get(id) ?? throw new CommandException("Something chosen is gone.");
            if (Refusal(m, e) is { } why) throw new CommandException(why);
            if (Spatial.StoreyOf(m, id)?.Id != storey.Id) throw new CommandException($"„{e.Name}” is on another storey than „{part.Name}”.");
            var key = FreeKey(def, e.Class);
            var attrs = e.CloneAttrs();
            attrs[KeyAttr] = key;
            ctx.Replace(e.With(attrs: attrs));
            ctx.Relate(IfcClass.RelAggregates, part.Id, id);
            slots[key] = id;
            members.Add((JsonNode)Snapshot(m, part, m.Get(id)!, key, null));
            if (members.Parent == null) def["members"] = members;
            keys.Add((JsonNode)key);
        }
        var pa = m.Get(part.Id)!.CloneAttrs();
        pa[IdsAttr] = slots;
        ctx.Replace(m.Get(part.Id)!.With(attrs: pa));
        RecordSlots(ctx, m.Get(part.Id)!);
        def["memberJoins"] = JoinsOf(m, m.Get(part.Id)!);
        var done = PartSketches.Commit(ctx, type, def);
        // This placed part's members are the ones just taken: it is up to date with what it was given.
        Stamp(ctx, m.Get(part.Id)!, done);
        return new JsonObject { ["keys"] = keys };
    }

    /// <summary>The slots of the openings, doors and usages the members have now (their ids, so they are kept when built again).</summary>
    static void RecordSlots(ICommandContext ctx, Entity part)
    {
        var m = ctx.Model;
        var slots = part.Attrs[IdsAttr] as JsonObject is { } s ? (JsonObject)s.DeepClone() : [];
        foreach (var (key, id) in Slots(part).Where(kv => !kv.Key.Contains('/')).ToList())
        {
            foreach (var k in slots.Select(kv => kv.Key).Where(k => k.StartsWith(key + "/", StringComparison.Ordinal)).ToList()) slots.Remove(k);
            if (m.RelatingOf(id, IfcClass.RelAssociatesMaterial) is { Class: IfcClass.MaterialLayerSetUsage } u) slots[key + "/u"] = u.Id;
            var oi = 0;
            foreach (var op in m.RelatedOf(id, IfcClass.RelVoidsElement).ToList())
            {
                var slot = $"{key}/o{oi++}";
                slots[slot] = op.Id;
                var fi = 0;
                foreach (var f in m.RelatedOf(op.Id, IfcClass.RelFillsElement).ToList()) slots[$"{slot}/f{fi++}"] = f.Id;
            }
        }
        var attrs = part.CloneAttrs();
        attrs[IdsAttr] = slots;
        ctx.Replace(part.With(attrs: attrs));
    }

    static void Stamp(ICommandContext ctx, Entity part, JsonObject def)
    {
        Dictionary<string, double> values;
        try { values = Parts.Values(def, part.Attrs["values"] as JsonObject); }
        catch (CommandException) { return; }
        var attrs = part.CloneAttrs();
        attrs[BuiltAttr] = Signature(ctx.Model, part, def, values);
        ctx.Replace(part.With(attrs: attrs));
    }

    static List<string> KeysGiven(IModelView m, Entity part, JsonObject a)
    {
        var slots = Slots(part);
        var keys = new List<string>();
        foreach (var k in (a["keys"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>()) keys.Add(k);
        if ((string?)a["key"] is { } one) keys.Add(one);
        foreach (var id in Args.Ids(a))
            keys.Add(slots.FirstOrDefault(kv => kv.Value == id && !kv.Key.Contains('/')).Key ?? throw new CommandException($"„{m.Get(id)?.Name ?? id}” is not one of „{part.Name}”'s members."));
        if (keys.Count == 0) throw new CommandException("Choose the members.");
        return keys;
    }

    /// <summary>
    /// {partId, ids | keys}: members let go. In the placed part <c>partId</c> they stay where they are as plain elements; the part no
    /// longer holds them, so the other placed parts lose theirs. → {keys}.
    /// </summary>
    public static JsonNode? Remove(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        var (part, type, def) = PlacedPart(m, (string?)a["partId"]);
        var keys = KeysGiven(m, part, a);
        var members = MembersOf(def);
        var slots = part.Attrs[IdsAttr] as JsonObject is { } s ? (JsonObject)s.DeepClone() : [];
        foreach (var key in keys)
        {
            var mo = members.OfType<JsonObject>().FirstOrDefault(x => (string?)x["key"] == key) ?? throw new CommandException($"„{type.Name}” has no member „{key}”.");
            members.Remove(mo);
            if ((string?)slots[key] is { } id && m.Get(id) is { } e)
            {
                var attrs = e.CloneAttrs();
                attrs.Remove(KeyAttr);
                ctx.Replace(e.With(attrs: attrs));
                foreach (var r in m.RelationsOf(id, IfcClass.RelAggregates).Where(r => r.Relating == part.Id).ToList())
                {
                    var rest = r.Related.Where(x => x != id).ToList();
                    if (rest.Count == 0) ctx.RemoveRelation(r.Id); else ctx.ReplaceRelation(r with { Related = rest });
                }
            }
            foreach (var k in slots.Select(kv => kv.Key).Where(k => k == key || k.StartsWith(key + "/", StringComparison.Ordinal)).ToList()) slots.Remove(k);
        }
        if (members.Parent == null && members.Count > 0) def["members"] = members;
        if (members.Count == 0) { def.Remove("members"); def.Remove("memberJoins"); }
        else def["memberJoins"] = CheckJoins(def["memberJoins"] as JsonArray ?? [], members);
        var pa = m.Get(part.Id)!.CloneAttrs();
        if (slots.Count == 0) pa.Remove(IdsAttr); else pa[IdsAttr] = slots;
        if (slots.Count == 0) pa.Remove(BuiltAttr);
        ctx.Replace(m.Get(part.Id)!.With(attrs: pa));
        var done = PartSketches.Commit(ctx, type, def);
        if (HasMembers(done)) Stamp(ctx, m.Get(part.Id)!, done);
        return new JsonObject { ["keys"] = new JsonArray([.. keys.Select(k => (JsonNode)k)]) };
    }

    /// <summary>
    /// {partId, id | key, drives: {attribute: formula | null}}: what of a member the part's parameters drive (its <c>length</c>, its
    /// <c>height</c>…), each a formula of the parameters checked with the defaults; null takes a drive off. Every placed part follows.
    /// </summary>
    public static JsonNode? SetDrives(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        var (part, type, def) = PlacedPart(m, (string?)a["partId"]);
        var key = KeysGiven(m, part, a) is [var k] ? k : throw new CommandException("Choose one member.");
        var mo = MembersOf(def).OfType<JsonObject>().FirstOrDefault(x => (string?)x["key"] == key) ?? throw new CommandException($"„{type.Name}” has no member „{key}”.");
        if (a["drives"] is not JsonObject given) throw new CommandException("Give {drives: {attribute: formula}}.");
        var drives = mo["drives"] as JsonObject ?? [];
        foreach (var (attr, f) in given)
        {
            if (f is null || (f is JsonValue fv && fv.TryGetValue<string>(out var fs) && fs.Trim().Length == 0)) { drives.Remove(attr); continue; }
            drives[attr] = f is JsonValue nv && nv.TryGetValue<double>(out var d) ? d : ((string?)f)!.Trim().TrimStart('=').Trim();
        }
        if (drives.Count == 0) mo.Remove("drives"); else mo["drives"] = drives;
        PartSketches.Commit(ctx, type, def);
        return null;
    }

    // ------------------------------------------------------------ after every command

    /// <summary>What a command changed of parts and their members, written back and built again (see the class).</summary>
    public static void Settle(ICommandContext ctx, IEdit edit)
    {
        if (building) return;
        var m = ctx.Model;
        var changes = edit.EntityChanges;
        if (changes.Count == 0 && edit.RelationChanges.Count == 0) return;
        // Placed parts that hold members, or held them before this command, found by what changed.
        Dictionary<string, Entity>? partsOf = null; // member or slot id -> placed part, read once
        Entity? OwnerById(string id)
        {
            if (partsOf == null)
            {
                partsOf = [];
                foreach (var x in m.OfClass(Parts.InstanceClass).Where(Parts.IsInstance))
                    foreach (var (_, sid) in Slots(x)) partsOf.TryAdd(sid, x);
            }
            return partsOf.GetValueOrDefault(id);
        }
        var anyMembers = m.OfClass(Parts.TypeClass).Any(t => HasMembers(Parts.DefOf(t)));
        var anySlots = changes.Any(c => (c.Before ?? c.After)?.Attrs[IdsAttr] != null || (c.Before ?? c.After)?.Str(KeyAttr) != null);
        if (!anyMembers && !anySlots) return;

        // 1. Members edited by the command, by any command: written back into their part's definition.
        var dirty = new Dictionary<string, (Entity Part, HashSet<string> Keys, HashSet<string> Gone)>();
        void Mark(Entity part, string key, bool gone)
        {
            if (!dirty.TryGetValue(part.Id, out var d)) dirty[part.Id] = d = (part, [], []);
            (gone ? d.Gone : d.Keys).Add(key);
        }
        void Touch(string id, bool gone = false)
        {
            if (OwnerById(id) is not { } part || m.Get(part.Id) is not { } now) return;
            var slot = Slots(now).FirstOrDefault(kv => kv.Value == id).Key;
            if (slot == null) return;
            var key = slot.Split('/')[0];
            Mark(now, key, gone && !slot.Contains('/'));
        }
        foreach (var c in changes)
        {
            var e = c.After ?? c.Before!;
            if (Parts.IsInstance(e)) continue;
            if (c.After == null) { Touch(c.Id, gone: true); continue; }
            // A copy of a member (copied on its own) is a plain element: its key goes.
            if (c.After.Str(KeyAttr) != null && OwnerById(c.Id) == null)
            {
                var attrs = c.After.CloneAttrs();
                attrs.Remove(KeyAttr);
                ctx.Replace(c.After.With(attrs: attrs));
                foreach (var r in m.RelationsOf(c.Id, IfcClass.RelAggregates).Where(r => r.Related.Contains(c.Id) && m.Get(r.Relating) is { } w && Parts.IsInstance(w)).ToList())
                {
                    var rest = r.Related.Where(x => x != c.Id).ToList();
                    if (rest.Count == 0) ctx.RemoveRelation(r.Id); else ctx.ReplaceRelation(r with { Related = rest });
                }
                continue;
            }
            Touch(c.Id);
            // A usage changed: its wall's.
            if (c.After.Class == IfcClass.MaterialLayerSetUsage) foreach (var w in m.RelatedOf(c.Id, IfcClass.RelAssociatesMaterial).ToList()) Touch(w.Id);
        }
        foreach (var rc in edit.RelationChanges)
        {
            var r = (rc.After ?? rc.Before)!;
            switch (r.Class)
            {
                case IfcClass.RelVoidsElement or IfcClass.RelDefinesByType or IfcClass.RelAssociatesMaterial or IfcClass.RelConnectsPathElements:
                    foreach (var id in r.Related.Append(r.Relating)) if (m.Get(id) != null) Touch(id);
                    break;
                case IfcClass.RelFillsElement:
                    if (m.RelatingOf(r.Relating, IfcClass.RelVoidsElement) is { } host) Touch(host.Id);
                    break;
            }
        }
        var changedTypes = new HashSet<string>();
        // A member deleted in one placed part is gone from the part, whatever else touched the others' in the same command.
        var goneOf = new Dictionary<string, HashSet<string>>();
        foreach (var (pid, (_, _, gone)) in dirty)
            if (gone.Count > 0 && m.Get(pid) is { } gp && Parts.TypeOf(m, gp) is { } gt)
                (goneOf.TryGetValue(gt.Id, out var gs) ? gs : goneOf[gt.Id] = []).UnionWith(gone);
        foreach (var (pid, (part0, keys, gone0)) in dirty)
        {
            if (m.Get(pid) is not { } part || Parts.TypeOf(m, part) is not { } type || Parts.DefOf(type) is not { } def0) continue;
            // A placed part deleted with its members is not an edit of its part.
            var def = (JsonObject)def0.DeepClone();
            var members = MembersOf(def);
            var slots = Slots(part);
            var gone = goneOf.GetValueOrDefault(type.Id) ?? gone0;
            foreach (var key in gone)
                if (members.OfType<JsonObject>().FirstOrDefault(x => (string?)x["key"] == key) is { } mo) members.Remove(mo);
            foreach (var key in keys.Except(gone))
            {
                if (!slots.TryGetValue(key, out var id) || m.Get(id) is not { } e) continue;
                var at = members.OfType<JsonObject>().Select((x, i) => (x, i)).FirstOrDefault(t => (string?)t.x["key"] == key);
                var snap = Snapshot(m, part, e, key, at.x);
                if (at.x != null) members[at.i] = snap; else members.Add((JsonNode)snap);
            }
            RecordSlots(ctx, part);
            var pa = m.Get(pid)!.CloneAttrs();
            // Only this placed part's own deleted members leave its slots; another's go when it is built again below.
            if (pa[IdsAttr] is JsonObject so) foreach (var key in gone0) foreach (var k in so.Select(kv => kv.Key).Where(k => k == key || k.StartsWith(key + "/", StringComparison.Ordinal)).ToList()) so.Remove(k);
            ctx.Replace(m.Get(pid)!.With(attrs: pa));
            if (members.Count == 0) { def.Remove("members"); def.Remove("memberJoins"); }
            else def["memberJoins"] = JoinsOf(m, m.Get(pid)!);
            if (JsonNode.DeepEquals(def, def0)) continue;
            try { PartSketches.Commit(ctx, type, def); changedTypes.Add(type.Id); }
            catch (CommandException ex) { throw new CommandException($"„{type.Name}” could not take the change of its member: {ex.Message}"); }
        }

        // 2. Placed parts built again where their definition, values, place or storey changed, or they are new, or their part lost its members.
        var candidates = new HashSet<string>();
        foreach (var c in changes)
        {
            var e = c.After ?? c.Before!;
            if (e.Class == Parts.TypeClass) changedTypes.Add(c.Id);
            else if (Parts.IsInstance(e))
            {
                if (c.After == null)
                {
                    // A placed part gone: its members with it.
                    foreach (var (slot, id) in Slots(c.Before!).Where(kv => !kv.Key.Contains('/')))
                        if (m.Get(id) is { } x && x.Str(KeyAttr) != null && m.RelatingOf(id, IfcClass.RelAggregates) == null)
                            using (Building()) ctx.Remove(id);
                }
                else candidates.Add(c.Id);
            }
        }
        foreach (var rc in edit.RelationChanges)
            if ((rc.After ?? rc.Before)!.Class is IfcClass.RelContainedInSpatialStructure or IfcClass.RelDefinesByType)
                foreach (var id in (rc.After?.Related ?? []).Concat(rc.Before?.Related ?? [])) if (m.Get(id) is { } x && Parts.IsInstance(x)) candidates.Add(id);
        foreach (var t in changedTypes)
            foreach (var x in m.RelatedOf(t, IfcClass.RelDefinesByType).Where(Parts.IsInstance)) candidates.Add(x.Id);
        foreach (var id in candidates)
        {
            if (m.Get(id) is not { } part) continue;
            var def = Parts.DefOf(Parts.TypeOf(m, part));
            Dictionary<string, double>? values = null;
            if (HasMembers(def)) try { values = Parts.Values(def!, part.Attrs["values"] as JsonObject); } catch (CommandException) { continue; }
            if (!UpToDate(m, part, def, values)) Build(ctx, part);
        }
    }

    readonly struct Guard(bool was) : IDisposable { public void Dispose() => building = was; }
    static Guard Building() { var g = new Guard(building); building = true; return g; }

    // ------------------------------------------------------------ the generic commands on members

    /// <summary>The module's answer to the core commands on members (docs/overrides.md, Driven elements).</summary>
    public sealed class Drive : IDrivenElements
    {
        public bool Drives(IModelView m, Entity e) => !building && e.Str(KeyAttr) != null && OwnerOf(m, e) != null;

        public void Before(ICommandContext ctx, DrivenEdit edit, DrivenOutcome outcome)
        {
            var m = ctx.Model;
            var given = edit.Args["ids"] is JsonArray ? Args.Ids(edit.Args).ToHashSet() : [];
            foreach (var id in edit.Ids)
            {
                if (m.Get(id) is not { } e || OwnerOf(m, e) is not { } part) continue;
                var withPart = given.Contains(part.Id);
                switch (edit.Command)
                {
                    case "group.create" or "group.add" or "component.make" or "component.makeUnique":
                        if (withPart) { outcome.Handled.Add(id); break; }
                        throw new CommandException($"„{e.Name}” is held by the part „{part.Name}”: group the placed part, or let the member go first.");
                    case "element.moveToStorey":
                        if (withPart) { outcome.Handled.Add(id); break; }
                        throw new CommandException($"„{e.Name}” is held by the part „{part.Name}”: move the placed part to the storey.");
                    // With its placed part, the part's own move, copy or delete takes it: built again where the part stands.
                    case "element.delete" or "element.transform" or "element.copyToStoreys" or "storey.duplicate" when withPart:
                        outcome.Handled.Add(id);
                        break;
                }
            }
        }

        public void After(ICommandContext ctx, IEdit edit, string command) => Settle(ctx, edit);
    }

    // ------------------------------------------------------------ the part's tree

    /// <summary>The members of a placed part's definition as the part's tree lists them: key, class, name, drives, and this placed part's element.</summary>
    public static JsonArray Tree(Entity part, JsonObject? def)
    {
        var slots = Slots(part);
        return new JsonArray([.. MembersOf(def).OfType<JsonObject>().Select(mo =>
        {
            var key = (string)mo["key"]!;
            var o = new JsonObject { ["key"] = key, ["class"] = (string?)mo["class"], ["name"] = (string?)mo["name"], ["id"] = slots.GetValueOrDefault(key) };
            if (mo["drives"] is JsonObject d) o["drives"] = d.DeepClone();
            var attrs = mo["attrs"] as JsonObject ?? [];
            // What a parameter could drive: its length (a straight axis), and its numbers.
            var can = new JsonArray();
            if (attrs["start"] is JsonArray && attrs["end"] is JsonArray && (Json.Number(attrs[PathCurve.Key]) ?? 0) == 0) can.Add((JsonNode)"length");
            foreach (var (k, v) in attrs)
                if (k is not ("rotation" or "z") && Json.Number(v) != null && v is JsonValue jv && !jv.TryGetValue<bool>(out _)) can.Add((JsonNode)k);
            o["drivable"] = can;
            return (JsonNode)o;
        })]);
    }

    public static void Register(IModuleRegistry r)
    {
        r.Service<IDrivenElements>(new Drive());
        // A part's native members (audit S6): {partId (a placed part), ids} taken in, {partId, ids | keys} let go, {partId, id | key, drives} driven.
        r.Command("addPartMembers", "Add to part", Add);
        r.Command("removePartMembers", "Let go of part members", Remove);
        r.Command("setPartMemberDrives", "Part member drives", SetDrives);
    }
}
