using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform.Lighting;

/// <summary>
/// A light's data (docs/plan-render.md, S2): what a light fixture, a lamp or a glowing material gives off. Held as the
/// entity's <c>light</c> attribute on an element, its type (every element of the type lights so) or a material (whatever is
/// made of it glows), and written to IFC as <c>Lapin_Light</c> and an <c>IfcLightSource…</c> (IfcExport). The element's own
/// values go over its type's. Without it nothing lights: projects saved before stay as they were.
/// </summary>
/// <param name="Kind">point (a bare lamp: every way), spot (a cone: <see cref="Beam"/> full inside, nothing past <see cref="Field"/>), area (a panel, <see cref="Width"/> × <see cref="Length"/>, out of its face).</param>
/// <param name="Lumens">Its output (lm), as makers give it.</param>
/// <param name="Kelvin">Its colour temperature (K, 1000–12000), the colour after Planck's law.</param>
/// <param name="Profile">Its distribution (<see cref="LightProfiles"/>): a built-in, or an IES file brought into the project; null, the kind's own.</param>
/// <param name="At">Where in the fixture's box the light comes from: bottom (a ceiling fixture's), centre, top (a floor lamp's shade).</param>
/// <param name="Aim">Which way a spot, a panel or a profile points: down or up.</param>
public sealed record LightSpec(string Kind, double Lumens, double Kelvin, double Beam, double Field, double? Width, double? Length, string? Profile, bool On, string At, string Aim)
{
    /// <summary>BimGo's defaults: 1000 lm at 3500 K, a point; a spot's 60° beam in a 90° field.</summary>
    public static readonly LightSpec Default = new("point", 1000, 3500, 60, 90, null, null, null, true, "bottom", "down");

    public static readonly string[] Kinds = ["point", "spot", "area"];
    public static readonly string[] Ats = ["bottom", "centre", "top"];
    public static readonly string[] Aims = ["down", "up"];

    /// <summary>The data over a base (the type's, or the defaults): the fields <paramref name="o"/> holds replace the base's.</summary>
    public static LightSpec Read(JsonObject? o, LightSpec? under = null)
    {
        var b = under ?? Default;
        if (o == null) return b;
        double N(string k, double v) => Json.Number(o[k]) is { } x && double.IsFinite(x) ? x : v;
        double? M(string k, double? v) => o.ContainsKey(k) ? Json.Number(o[k]) : v;
        string S(string k, string[] allowed, string v) => (string?)o[k] is { } s && allowed.Contains(s) ? s : v;
        return new LightSpec(S("kind", Kinds, b.Kind), N("lumens", b.Lumens), N("kelvin", b.Kelvin), N("beam", b.Beam), N("field", b.Field),
            M("width", b.Width), M("length", b.Length), o.ContainsKey("profile") ? (string?)o["profile"] : b.Profile,
            o["on"] is JsonValue on && on.TryGetValue<bool>(out var x) ? x : b.On, S("at", Ats, b.At), S("aim", Aims, b.Aim));
    }

    public JsonObject ToJson() => new()
    {
        ["kind"] = Kind, ["lumens"] = Lumens, ["kelvin"] = Kelvin, ["beam"] = Beam, ["field"] = Field, ["width"] = Width, ["length"] = Length,
        ["profile"] = Profile, ["on"] = On, ["at"] = At, ["aim"] = Aim,
    };
}

/// <summary>One light as the 3D view draws it: where it is, which way it points, and its data.</summary>
public sealed record LightSource(string ElementId, LightSpec Spec, Vec3 Position, Vec3 Aim, Vec3 Across, double Width, double Length, string? From);

public static class Lights
{
    public const string Key = "light";
    /// <summary>An IES file brought into the project: its text, its key (<c>ies:&lt;hash&gt;</c>) and name, saved with it as images are.</summary>
    public const string ProfileClass = "LapinLightProfile";

    /// <summary>The fields a command may give, checked: the number's range, or the choices.</summary>
    static readonly (string Key, double Min, double Max)[] Numbers =
        [("lumens", 0, 1_000_000), ("kelvin", 1000, 12000), ("beam", 1, 179), ("field", 1, 179), ("width", 0.01, 20), ("length", 0.01, 20)];

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(ProfileClass, "Light profile", [new("key", "Key", "text", ReadOnly: true)], "#c8a040", Kind: "resource"));

        // The light data of elements, types or materials, one step: what is given over what they had (else the defaults); clear: no light.
        r.Command("light.set", "Set light", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            if (ids.Count == 0) throw new CommandException("Which elements, types or materials?");
            var clear = a["clear"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c;
            var patch = clear ? null : Checked(ctx.Model, a);
            foreach (var id in ids)
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException("There is no such element.");
                if (e.Class is IfcClass.Project or IfcClass.Site or IfcClass.Building or IfcClass.BuildingStorey)
                    throw new CommandException($"A {e.Class[3..].ToLowerInvariant()} gives no light.");
                var attrs = e.CloneAttrs();
                if (clear) attrs.Remove(Key);
                else
                {
                    var own = attrs[Key] as JsonObject ?? new JsonObject();
                    foreach (var (k, v) in patch!) own[k] = v?.DeepClone();
                    attrs[Key] = own;
                }
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        // An IES file (LM-63, type C) brought into the project: its key, what it says; the same text brought again is the same profile.
        r.Command("light.addProfile", "Import light profile", (ctx, a) =>
        {
            var text = (string?)a["text"] ?? throw new CommandException("The file's text?");
            var key = LightProfiles.KeyOf(text);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : "Light profile";
            var profile = LightProfile.FromIes(key, name, text, out var error) ?? throw new CommandException(error ?? "Not an IES file.");
            var existing = ctx.Model.OfClass(ProfileClass).FirstOrDefault(p => p.Str("key") == key);
            if (existing == null)
            {
                existing = new Entity(ctx.NewId(), ProfileClass, name, new JsonObject { ["key"] = key, ["text"] = text.Replace("\r\n", "\n") });
                ctx.Add(existing);
            }
            return new JsonObject { ["id"] = existing.Id, ["key"] = key, ["name"] = existing.Name, ["lumens"] = Math.Round(profile.Lumens), ["beam"] = Math.Round(profile.BeamAngle, 1) };
        });

        // Every light of the model, as the 3D view draws them.
        r.Query("light.sources", (q, _) => new JsonArray(Sources(q).Select(s => (JsonNode)ToJson(s)).ToArray()));

        // A profile's table for the view (rounded: three decimals are finer than any picture).
        r.Query("light.profile", (q, a) => Profile(q.Model, (string?)a["key"]) is { } p ? ProfileJson(p, table: true) : null);

        // The profiles one may choose: the built-ins and the project's files.
        r.Query("light.profiles", (q, _) => new JsonArray(
            LightProfiles.BuiltIns.Select(p => (JsonNode)ProfileJson(p, table: false))
                .Concat(q.Model.OfClass(ProfileClass).Select(e => Profile(q.Model, e.Str("key")) is { } p ? (JsonNode)ProfileJson(p, table: false) : null).OfType<JsonNode>())
                .ToArray()));
    }

    static JsonObject ProfileJson(LightProfile p, bool table)
    {
        var o = new JsonObject
        {
            ["key"] = p.Key, ["name"] = p.Name, ["scale"] = Math.Round(p.Scale, 4), ["lumens"] = Math.Round(p.Lumens), ["beam"] = Math.Round(p.BeamAngle, 1),
            ["field"] = Math.Round(p.FieldAngle, 1), ["file"] = p.IsFile,
        };
        if (table)
        {
            o["vertical"] = LightProfile.Vertical;
            o["horizontal"] = LightProfile.Horizontal;
            o["table"] = new JsonArray(p.Table.Select(x => (JsonNode)Math.Round(x, 3)).ToArray());
        }
        return o;
    }

    /// <summary>A profile by its key: a built-in, a generated one, or a file in the project; null when there is none.</summary>
    public static LightProfile? Profile(IModelView m, string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (LightProfiles.BuiltIn(key) is { } b) return b;
        if (LightProfiles.Generated(key) is { } g) return g;
        var e = m.OfClass(ProfileClass).FirstOrDefault(p => p.Str("key") == key);
        return e?.Str("text") is { } text ? LightProfile.FromIes(key, e.Name, text, out _) : null;
    }

    /// <summary>A command's light fields checked: numbers in range, a kind, a profile the project has; null clears one (width, length, profile).</summary>
    static JsonObject Checked(IModelView m, JsonObject a)
    {
        var o = new JsonObject();
        foreach (var (k, min, max) in Numbers)
        {
            if (!a.ContainsKey(k)) continue;
            if (a[k] == null && k is "width" or "length") { o[k] = null; continue; }
            var v = Json.Number(a[k]) ?? throw new CommandException($"„{k}” is a number.");
            if (!double.IsFinite(v) || v < min || v > max) throw new CommandException($"„{k}” is between {min} and {max}.");
            o[k] = v;
        }
        foreach (var (k, allowed) in new[] { ("kind", LightSpec.Kinds), ("at", LightSpec.Ats), ("aim", LightSpec.Aims) })
            if (a.ContainsKey(k))
                o[k] = (string?)a[k] is { } s && allowed.Contains(s) ? s : throw new CommandException($"„{k}” is one of {string.Join(", ", allowed)}.");
        if (a.ContainsKey("on")) o["on"] = a["on"] is JsonValue v && v.TryGetValue<bool>(out var on) ? on : throw new CommandException("„on” is true or false.");
        if (a.ContainsKey("profile"))
        {
            var key = (string?)a["profile"];
            if (key is { Length: > 0 } && Profile(m, key) == null) throw new CommandException($"There is no light profile „{key}”.");
            o["profile"] = key is { Length: > 0 } ? key : null;
        }
        if (o["beam"] is { } beam && o["field"] is { } field && (double)beam > (double)field) throw new CommandException("The beam is inside the field: it cannot be wider.");
        return o;
    }

    /// <summary>The element's light: its own data over its type's; null when neither has any.</summary>
    public static LightSpec? Of(IModelView m, Entity e)
    {
        var own = e.Attrs[Key] as JsonObject;
        var type = Types.Of(m, e)?.Attrs[Key] as JsonObject;
        if (own == null && type == null) return null;
        return LightSpec.Read(own, type != null ? LightSpec.Read(type) : null);
    }

    /// <summary>
    /// The model's lights: the elements with light data (their own or their type's), and those with a part made of a material
    /// that glows as a light. Each placed in its body's box (<see cref="LightSpec.At"/>), aimed down or up, turned with the
    /// element (its rotation); an area's size its own, else the box's.
    /// </summary>
    public static IReadOnlyList<LightSource> Sources(IQueryContext q)
    {
        var m = q.Model;
        var o = new List<LightSource>();
        var typed = m.Entities.Where(e => e.Class.EndsWith("Type", StringComparison.Ordinal) && e.Attrs[Key] is JsonObject).Select(e => e.Id).ToHashSet();
        var glowing = m.OfClass(IfcClass.Material).Where(e => e.Attrs[Key] is JsonObject).ToDictionary(e => e.Id);
        foreach (var e in m.Entities)
        {
            if (e.Class == IfcClass.Material || e.Class.EndsWith("Type", StringComparison.Ordinal) || e.Class == ProfileClass) continue;
            LightSpec? spec = null;
            string? from = null;
            if (e.Attrs[Key] is JsonObject || (typed.Count > 0 && Types.Of(m, e) is { } t && typed.Contains(t.Id))) spec = Of(m, e);
            ElementGeometry? g = null;
            if (spec == null && glowing.Count > 0)
            {
                g = q.Geometry(e.Id);
                if (g?.Parts.FirstOrDefault(p => p.Material != null && glowing.ContainsKey(p.Material)) is { } part)
                {
                    spec = LightSpec.Read(glowing[part.Material!].Attrs[Key] as JsonObject);
                    from = part.Material;
                }
            }
            if (spec == null) continue;
            g ??= q.Geometry(e.Id);
            if (g != null && Place(e, spec, g, from) is { } source) o.Add(source);
        }
        return o;
    }

    /// <summary>
    /// Where an element's light is: in its body's box (in the parts of the glowing material <paramref name="from"/> when it is one)
    /// by <see cref="LightSpec.At"/>, a hair outside the face it leaves (so the fixture never shades it) or inside a lamp's shade,
    /// aimed down or up, its C 0° along the element's rotation; an area's size its own, else the box's. Null without a body.
    /// </summary>
    public static LightSource? Place(Entity e, LightSpec spec, ElementGeometry g, string? from = null)
    {
        Vec3 lo = new(double.MaxValue, double.MaxValue, double.MaxValue), hi = new(double.MinValue, double.MinValue, double.MinValue);
        foreach (var p in g.Parts)
        {
            if (from != null && p.Material != from) continue;
            if (p.Bounds() is not { } b) continue;
            lo = new(Math.Min(lo.X, b.Min.X), Math.Min(lo.Y, b.Min.Y), Math.Min(lo.Z, b.Min.Z));
            hi = new(Math.Max(hi.X, b.Max.X), Math.Max(hi.Y, b.Max.Y), Math.Max(hi.Z, b.Max.Z));
        }
        if (lo.X > hi.X) return null;
        var down = spec.Aim == "down";
        var z = spec.At switch { "top" => hi.Z - 0.15 * (hi.Z - lo.Z), "centre" => (lo.Z + hi.Z) / 2, _ => down ? lo.Z - 0.01 : hi.Z + 0.01 };
        var turn = (e.NumOrNull("rotation") ?? 0) * Math.PI / 180;
        var across = new Vec3(Math.Cos(turn), Math.Sin(turn), 0);
        var w = spec.Width ?? Math.Max(0.05, Math.Abs((hi.X - lo.X) * across.X) + Math.Abs((hi.Y - lo.Y) * across.Y));
        var l = spec.Length ?? Math.Max(0.05, Math.Abs((hi.X - lo.X) * across.Y) + Math.Abs((hi.Y - lo.Y) * across.X));
        return new LightSource(e.Id, spec, new Vec3((lo.X + hi.X) / 2, (lo.Y + hi.Y) / 2, z), new Vec3(0, 0, down ? -1 : 1), across, w, l, from);
    }

    public static JsonObject ToJson(LightSource s)
    {
        var o = s.Spec.ToJson();
        o["id"] = s.ElementId;
        o["position"] = new JsonArray(Math.Round(s.Position.X, 4), Math.Round(s.Position.Y, 4), Math.Round(s.Position.Z, 4));
        o["direction"] = new JsonArray(s.Aim.X, s.Aim.Y, s.Aim.Z);
        o["across"] = new JsonArray(Math.Round(s.Across.X, 6), Math.Round(s.Across.Y, 6), Math.Round(s.Across.Z, 6));
        o["width"] = Math.Round(s.Width, 4);
        o["length"] = Math.Round(s.Length, 4);
        o["material"] = s.From;
        return o;
    }
}
