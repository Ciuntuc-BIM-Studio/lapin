using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Furniture;

/// <summary>
/// Furniture models of the user's own (docs/plan-user-extensibility.md §5.5): a GLB brought in with its licence and source, kept
/// in the project (<c>LapinModelAsset</c>: the file and how it stretches — its extent, the size a type starts at, its least and
/// most, a stretch zone on each axis, its materials mapped to Lapin's roles, how high it hangs). It is a model as Lapin's own are:
/// a type of it is stretched by its zones, its plan the body seen from above (or a symbol chosen for it). Its id is its content's
/// (<c>user:…</c>, the file and its settings), so a project, its types and the furniture library carry it whole anywhere; a change
/// of its settings is a new id its types follow.
/// </summary>
public static class UserModels
{
    public const string Class = "LapinModelAsset";
    public const string Prefix = "user:";
    public const int MaxBytes = 16 * 1024 * 1024;

    static readonly ConcurrentDictionary<string, (FurnitureModel Model, byte[] Bytes)> Known = new();

    public static bool Is(string? id) => id?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    public static FurnitureModel? Find(string? id) => id != null && Known.TryGetValue(id, out var k) ? k.Model : null;
    public static byte[]? Bytes(string id) => Known.TryGetValue(id, out var k) ? k.Bytes : null;

    /// <summary>The settings a model is known by (all but its file), as stored.</summary>
    static readonly string[] Settings = ["name", "group", "predefined", "objectType", "natural", "size", "min", "max", "zones", "slots", "mount", "against", "seats", "licence", "source"];

    static string CodeOf(string glb, JsonObject attrs)
    {
        var sb = new StringBuilder(glb);
        foreach (var k in Settings) sb.Append('|').Append(k).Append('=').Append(attrs[k]?.ToJsonString() ?? "");
        return Prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16].ToLowerInvariant();
    }

    static double[] V(JsonNode? n, double[] d) => n is JsonArray { Count: 3 } a && a.All(x => Json.Number(x) is > 0) ? [.. a.Select(x => Json.Number(x)!.Value)] : d;

    /// <summary>An asset known by its code (its model and its file), from its entity.</summary>
    static string Register(Entity e)
    {
        var code = e.Str("code") ?? "";
        if (Known.ContainsKey(code)) return code;
        var bytes = Convert.FromBase64String(e.Str("glb") ?? "");
        var natural = V(e.Attrs["natural"], [1, 1, 1]);
        var zones = new (double From, double To)?[3];
        for (var k = 0; k < 3; k++)
            if (e.Attrs["zones"]?["xyz"[k].ToString()] is JsonArray { Count: 2 } z && Json.Number(z[0]) is { } f && Json.Number(z[1]) is { } t && f >= 0 && t <= 1 && f <= t) zones[k] = (f, t);
        var slots = (e.Attrs["slots"] as JsonObject ?? []).Where(p => p.Value is JsonValue).ToDictionary(p => p.Key, p => (string)p.Value!);
        var model = new FurnitureModel(code, e.Name, e.Str("group") ?? "My models", e.Str("predefined") ?? "NOTDEFINED", e.Str("objectType"),
            natural, V(e.Attrs["size"], natural), V(e.Attrs["min"], [.. natural.Select(x => x * 0.25)]), V(e.Attrs["max"], [.. natural.Select(x => x * 4)]), zones, slots,
            e.Num("mount"), e.Attrs["against"] is not JsonValue av || !av.TryGetValue<bool>(out var ag) || ag, e.Bool("seats"), e.Str("licence") ?? "", e.Str("source") ?? "");
        Known[code] = (model, bytes);
        return code;
    }

    /// <summary>The project's model of that code made known (a type of it about to be built); false when the project has none.</summary>
    public static bool Ensure(IModelView m, string? code)
    {
        if (!Is(code)) return false;
        if (Known.ContainsKey(code!)) return true;
        var e = m.OfClass(Class).FirstOrDefault(x => x.Str("code") == code);
        if (e == null) return false;
        try { Register(e); return true; }
        catch (FormatException) { return false; }
    }

    /// <summary>Every model of the project made known (a type about to name one).</summary>
    public static void EnsureAll(IModelView m)
    {
        foreach (var e in m.OfClass(Class)) Ensure(m, e.Str("code"));
    }

    /// <summary>A GLB's extent (metres, Lapin's frame: Z up) and its material names.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static (double[] Natural, List<string> Materials) Read(byte[] bytes)
    {
        var pieces = Lapin.Mesh.MeshImport.Gltf(bytes, true, null, []);
        var all = pieces.SelectMany(p => p.Vertices).Select(v => new Vec3(v.X, -v.Z, v.Y)).ToList();
        if (all.Count == 0) throw new CommandException("The file has no triangles to make a model of.");
        double[] ext = [all.Max(v => v.X) - all.Min(v => v.X), all.Max(v => v.Y) - all.Min(v => v.Y), all.Max(v => v.Z) - all.Min(v => v.Z)];
        if (ext.Any(x => x < 1e-4)) throw new CommandException("The model is flat on one axis: a piece of furniture has width, depth and height.");
        return ([.. ext.Select(x => Math.Round(x, 6))], [.. pieces.Where(p => p.Triangles.Count > 0).Select(p => p.Material ?? "_defaultMat").Distinct()]);
    }

    /// <summary>A model's settings from a command over <paramref name="attrs"/>, checked: sizes, zones within 0–1, roles of Lapin's.</summary>
    static void Apply(JsonObject attrs, JsonObject a)
    {
        foreach (var k in new[] { "group", "predefined", "objectType", "licence", "source" })
            if (a.ContainsKey(k)) { if ((string?)a[k] is { Length: > 0 } v) attrs[k] = v.Trim(); else attrs.Remove(k); }
        foreach (var k in new[] { "size", "min", "max" })
            if (a[k] is JsonArray arr)
            {
                if (arr.Count != 3 || arr.Any(x => Json.Number(x) is not (> 0 and <= 50))) throw new CommandException($"A model's {k} is three lengths above 0 (m).");
                attrs[k] = arr.DeepClone();
            }
        if (a["zones"] is JsonObject zones)
        {
            var o = new JsonObject();
            foreach (var axis in new[] { "x", "y", "z" })
                if (zones[axis] is JsonArray { Count: 2 } z)
                {
                    var (f, t) = (Json.Number(z[0]) ?? -1, Json.Number(z[1]) ?? -1);
                    if (!(f >= 0 && t <= 1 && f <= t)) throw new CommandException($"A stretch zone on {axis} is two fractions of the model, 0 to 1, the first not above the second.");
                    o[axis] = new JsonArray(f, t);
                }
            attrs["zones"] = o;
        }
        if (a["slots"] is JsonObject slots)
        {
            var o = new JsonObject();
            foreach (var (k, v) in slots)
            {
                var role = (string?)v ?? "body";
                if (!FurnitureModels.Roles.Contains(role) && !FurnitureKinds.RoleOrder.Contains(role)) throw new CommandException($"„{role}” is not a material role ({string.Join(", ", FurnitureModels.Roles)}).");
                o[k] = role;
            }
            attrs["slots"] = o;
        }
        if (Json.Number(a["mount"]) is { } mount) attrs["mount"] = Math.Clamp(mount, 0, 5);
        foreach (var k in new[] { "against", "seats" })
            if (a[k] is JsonValue bv && bv.TryGetValue<bool>(out var b)) attrs[k] = b;
    }

    /// <summary>Every type of the project of a model's old code takes its new one (its settings changed).</summary>
    static void Repoint(ICommandContext ctx, string from, string to)
    {
        foreach (var t in ctx.Model.OfClass(FurnitureModule.TypeClass).Where(t => (string?)t.Attrs["spec"]?[FurnitureModels.Key] == from).ToList())
        {
            var attrs = t.CloneAttrs();
            attrs["spec"]![FurnitureModels.Key] = to;
            ctx.Replace(t.With(attrs: attrs));
        }
    }

    /// <summary>A model as the furniture library carries it: its settings and its file.</summary>
    public static JsonObject? Carry(IModelView m, string? code)
    {
        if (m.OfClass(Class).FirstOrDefault(x => x.Str("code") == code) is not { } e) return null;
        var o = e.CloneAttrs();
        o["name"] = e.Name;
        return o;
    }

    /// <summary>A model a library item carries, brought into the project when it lacks it (by its code).</summary>
    public static void Bring(IEdit edit, JsonObject? carried)
    {
        if (carried == null || (string?)carried["code"] is not { } code || !Is(code)) return;
        if (edit.Model.OfClass(Class).Any(x => x.Str("code") == code)) return;
        var attrs = (JsonObject)carried.DeepClone();
        var name = (string?)attrs["name"] ?? "Model";
        attrs.Remove("name");
        if ((string?)attrs["glb"] is not { Length: > 0 } glb || glb.Length > MaxBytes * 4 / 3 + 4) return;
        // Its code is its content's: a carried model whose content does not give its code is not taken.
        if (CodeOf(glb, attrs) != code) return;
        edit.Add(new Entity(edit.NewId(), Class, name, attrs));
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Furniture model", [], "#8a6a48", Kind: "resource"));

        // The project's own models, with their settings (not their files).
        r.Query("models", (q, _) => new JsonObject
        {
            ["models"] = new JsonArray(q.Model.OfClass(Class).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Select(e =>
            {
                var o = e.CloneAttrs();
                o.Remove("glb");
                o["id"] = e.Id;
                o["name"] = e.Name;
                o["types"] = q.Model.OfClass(FurnitureModule.TypeClass).Count(t => (string?)t.Attrs["spec"]?[FurnitureModels.Key] == e.Str("code"));
                return (JsonNode)o;
            }).ToArray()),
        });

        // A GLB brought in as a model: {data (base64), name, licence (required: whose it is and on what terms), source?, group?,
        // predefined?, size?, min?, max?, zones?, slots?, mount?, against?}; its extent and materials read from the file.
        r.Command("importModel", "Import furniture model", (ctx, a) =>
        {
            var data = (string?)a["data"] ?? throw new CommandException("Which file? A GLB, base64.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(data); } catch (FormatException) { throw new CommandException("The file is not base64."); }
            if (bytes.Length == 0 || bytes.Length > MaxBytes) throw new CommandException("A model's GLB is at most 16 MB.");
            if (bytes.Length < 12 || bytes[0] != (byte)'g' || bytes[1] != (byte)'l' || bytes[2] != (byte)'T' || bytes[3] != (byte)'F') throw new CommandException("That is not a GLB (binary glTF) file.");
            if (((string?)a["licence"])?.Trim() is not { Length: > 0 }) throw new CommandException("Say the model's licence (CC0, CC-BY-4.0, your own…): it travels with it.");
            var (natural, materials) = Read(bytes);
            var attrs = new JsonObject
            {
                ["glb"] = Convert.ToBase64String(bytes), ["natural"] = new JsonArray(natural.Select(x => (JsonNode)x).ToArray()),
                ["slots"] = new JsonObject(materials.Select(n => KeyValuePair.Create(n, (JsonNode?)"body"))),
            };
            Apply(attrs, a);
            attrs["code"] = CodeOf((string)attrs["glb"]!, attrs);
            var e = new Entity(ctx.NewId(), Class, ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : "Model", attrs);
            ctx.Add(e);
            Register(e);
            return new JsonObject { ["id"] = e.Id, ["model"] = (string)attrs["code"]!, ["natural"] = attrs["natural"]!.DeepClone(), ["materials"] = new JsonArray(materials.Select(x => (JsonNode)x).ToArray()) };
        });

        // A model's settings changed: its code is new, its types follow; one undo step.
        r.Command("updateModel", "Change furniture model", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("There is no such furniture model.");
            var attrs = e.CloneAttrs();
            Apply(attrs, a);
            var old = e.Str("code") ?? "";
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : e.Name;
            attrs["code"] = CodeOf((string?)attrs["glb"] ?? "", attrs);
            ctx.Replace(e.With(name: name, attrs: attrs));
            Register(ctx.Model.Get(e.Id)!);
            if ((string)attrs["code"]! != old) Repoint(ctx, old, (string)attrs["code"]!);
            return new JsonObject { ["model"] = (string)attrs["code"]! };
        });

        r.Command("deleteModel", "Delete furniture model", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("There is no such furniture model.");
            var code = e.Str("code");
            var used = ctx.Model.OfClass(FurnitureModule.TypeClass).Count(t => (string?)t.Attrs["spec"]?[FurnitureModels.Key] == code);
            if (used > 0) throw new CommandException($"{used} furniture type{(used == 1 ? " is" : "s are")} made of „{e.Name}”: delete or change them first.");
            ctx.Remove(e.Id);
            return null;
        });
    }
}
