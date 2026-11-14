using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// The rules the user owns for the dimensions made at once (forum topic 78): which strings, in what order, how far from
/// the wall and from one another (paper mm), in which dimension style, measured to the walls' faces, their core or their
/// axes, and how narrow an opening is left out. Kept on the project (<c>dimensionRules</c>: undone, saved, carried by a
/// <c>.lapinstandards</c> file with its options); what is not given is the built-in default, which makes what Lapin made
/// before the rules (room strings 5 mm in from the side, the jambs on them).
/// </summary>
public sealed record DimensionRules(IReadOnlyList<(string Kind, bool On)> Strings, double Offset, double Spacing, string? DimStyle, string MeasureTo, double MinOpening)
{
    public const string Key = "dimensionRules";

    /// <summary>The exterior strings, from the wall outward: every feature, the openings' centres, the grid axes, the rooms behind, the whole facade.</summary>
    public static readonly string[] ExteriorKinds = ["details", "openings", "axes", "rooms", "overall"];
    /// <summary>A room's strings, from its side inward: the side with its openings' jambs, the side's whole length.</summary>
    public static readonly string[] RoomKinds = ["details", "overall"];
    public static readonly string[] MeasureTos = ["faces", "core", "axes"];

    public static DimensionRules ExteriorDefault => new(ExteriorKinds.Select(k => (k, true)).ToList(), 10, 7, null, "faces", 0);
    public static DimensionRules RoomDefault => new([("details", true), ("overall", false)], 5, 7, null, "faces", 0);

    public bool Has(string kind) => Strings.Any(s => s.Kind == kind && s.On);

    /// <summary>The strings turned on, in their order.</summary>
    public List<string> Order => Strings.Where(s => s.On).Select(s => s.Kind).ToList();

    public JsonObject ToJson(bool exterior)
    {
        var o = new JsonObject
        {
            ["strings"] = new JsonArray(Strings.Select(s => (JsonNode)new JsonObject { ["kind"] = s.Kind, ["on"] = s.On }).ToArray()),
            ["offset"] = Offset,
            ["spacing"] = Spacing,
            ["dimStyle"] = DimStyle,
            ["minOpening"] = MinOpening,
        };
        if (exterior) o["measureTo"] = MeasureTo;
        return o;
    }

    /// <summary>
    /// Rules read over <paramref name="basis"/>: each field given checked (a string kind known, each once; the strings not
    /// listed keep the basis's place after those listed, turned off), the others the basis's. Throws on what cannot be.
    /// </summary>
    public static DimensionRules Read(JsonNode? n, DimensionRules basis, bool exterior)
    {
        if (n is not JsonObject o) return basis;
        var kinds = exterior ? ExteriorKinds : RoomKinds;
        var what = exterior ? "Exterior dimensions" : "Room dimensions";
        var strings = basis.Strings.ToList();
        if (o["strings"] is JsonArray sa)
        {
            var list = new List<(string, bool)>();
            foreach (var x in sa)
            {
                var (kind, on) = x switch
                {
                    JsonObject so => ((string?)so["kind"], so["on"] is not JsonValue v || !v.TryGetValue<bool>(out var b) || b),
                    JsonValue sv when sv.TryGetValue<string>(out var str) => (str, true),
                    _ => (null, false),
                };
                if (kind == null || !kinds.Contains(kind)) throw new CommandException($"{what}: „{kind}” is not a dimension string; they are {string.Join(", ", kinds)}.");
                if (list.Any(l => l.Item1 == kind)) throw new CommandException($"{what}: the string „{kind}” is listed twice.");
                list.Add((kind, on));
            }
            foreach (var k in kinds) if (!list.Any(l => l.Item1 == k)) list.Add((k, false));
            strings = list;
        }
        double Mm(string key, double old, double min)
        {
            if (!o.ContainsKey(key)) return old;
            return Json.Number(o[key]) is { } v && double.IsFinite(v) && v >= min && v <= 200 ? v : throw new CommandException($"{what}: the {key} is {min} to 200 mm on the paper.");
        }
        var offset = Mm("offset", basis.Offset, 0.5);
        var spacing = Mm("spacing", basis.Spacing, 1);
        var style = o.ContainsKey("dimStyle") ? ((string?)o["dimStyle"] is { Length: > 0 } ds ? ds : null) : basis.DimStyle;
        var to = basis.MeasureTo;
        if (exterior && o.ContainsKey("measureTo"))
            to = (string?)o["measureTo"] is { } mt && MeasureTos.Contains(mt) ? mt : throw new CommandException($"{what}: measure to the walls' faces, their core or their axes.");
        var min = basis.MinOpening;
        if (o.ContainsKey("minOpening"))
            min = Json.Number(o["minOpening"]) is { } mo && mo >= 0 && mo < 100 ? mo : throw new CommandException($"{what}: the narrowest opening dimensioned is 0 m or more.");
        return new DimensionRules(strings, offset, spacing, style, to, min);
    }

    static JsonObject? Stored(IModelView m) => Spatial.Project(m)?.Attrs[Key] as JsonObject;

    /// <summary>The project's exterior rules (the defaults where it gives none), with <paramref name="over"/> on top for one run.</summary>
    public static DimensionRules Exterior(IModelView m, JsonNode? over = null) => Read(over, Safe(Stored(m)?["exterior"], ExteriorDefault, true), true);

    /// <summary>The project's room rules, with <paramref name="over"/> on top for one run.</summary>
    public static DimensionRules Rooms(IModelView m, JsonNode? over = null) => Read(over, Safe(Stored(m)?["rooms"], RoomDefault, false), false);

    /// <summary>Stored rules a file brought in may have spoiled: the defaults rather than a failure on every run.</summary>
    static DimensionRules Safe(JsonNode? n, DimensionRules basis, bool exterior)
    {
        try { return Read(n, basis, exterior); }
        catch (CommandException) { return basis; }
    }

    public static void Register(IModuleRegistry r)
    {
        // The rules as they apply now, and the built-in ones.
        r.Query("dimensionRules", (q, _) => new JsonObject
        {
            ["exterior"] = Exterior(q.Model).ToJson(true),
            ["rooms"] = Rooms(q.Model).ToJson(false),
            ["defaults"] = new JsonObject { ["exterior"] = ExteriorDefault.ToJson(true), ["rooms"] = RoomDefault.ToJson(false) },
        });

        // The rules set, one undo step: {exterior?, rooms?} (null: back to the built-in rules), each checked.
        r.Command("setDimensionRules", "Dimension rules", (ctx, a) =>
        {
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = project.CloneAttrs();
            var stored = attrs[Key] as JsonObject is { } had ? (JsonObject)had.DeepClone() : new JsonObject();
            foreach (var (part, exterior, given) in new[] { ("exterior", true, a["exterior"]), ("rooms", false, a["rooms"]) })
            {
                if (!a.ContainsKey(part)) continue;
                if (given is null) { stored.Remove(part); continue; }
                var basis = exterior ? Exterior(ctx.Model) : Rooms(ctx.Model);
                stored[part] = Read(given, basis, exterior).ToJson(exterior);
            }
            if (stored.Count > 0) attrs[Key] = stored; else attrs.Remove(Key);
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });
    }
}
