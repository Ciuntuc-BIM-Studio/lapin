using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>One value of a colour scheme and how its rooms look: a fill colour, a hatch (its scale on paper and its colour), either or both.</summary>
public sealed record SchemeEntry(string Value, string? Color, string? Hatch = null, double HatchScale = 1, string? HatchColor = null);

/// <summary>A colour scheme: rooms coloured by one of their fields (their function, name, department…), one entry per value.</summary>
public sealed record ColorScheme(string Id, string Name, string By, IReadOnlyList<SchemeEntry> Entries)
{
    public SchemeEntry? Entry(string value) => Entries.FirstOrDefault(e => string.Equals(e.Value, value, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Room colour schemes (docs/rooms.md): the project keeps them
/// (<c>colorSchemes</c>; until one is changed, the defaults: by function and
/// by department), a plan view names the one it fills its rooms with
/// (<c>colorScheme</c>; none by default). A room's own colour or hatch wins
/// over its scheme's. A colour legend (an annotation of kind LEGEND on a
/// view) lists the entries the view's rooms use, with their swatches.
/// </summary>
public static class ColorSchemes
{
    public const string Key = "colorSchemes";
    public const string Legend = "LEGEND";

    /// <summary>Soft colours that read under black linework and print well; new values take the first one not used yet.</summary>
    public static readonly string[] Palette =
    [
        "#f7e1a0", "#a7c7e7", "#f4c7a1", "#a8d5ba", "#d4b8e0", "#f2a7a7", "#9fd8df", "#e6c9a8",
        "#c7ceea", "#b5d99c", "#ffd6e0", "#fbe3b9", "#b8e0d2", "#e2c2b9", "#cde7b0", "#d9d9d9",
    ];

    /// <summary>The fields a scheme can colour by, and how the interface names them.</summary>
    public static readonly (string Key, string Label)[] Fields =
        [("function", "Function"), ("name", "Name"), ("department", "Department"), ("number", "Number"), ("finishFloor", "Floor finish")];

    public static readonly IReadOnlyList<ColorScheme> Defaults =
    [
        new("by-function", "By function", "function",
        [
            new("Living", "#f7e1a0"), new("Bedroom", "#a7c7e7"), new("Kitchen", "#f4c7a1"), new("Dining", "#fbe3b9"),
            new("Bath", "#9fd8df"), new("WC", "#b8e0d2"), new("Circulation", "#d9d9d9"), new("Storage", "#e6c9a8"),
            new("Technical", "#c7ceea"), new("Office", "#d4b8e0"), new("Balcony", "#b5d99c"), new("Garage", "#e2c2b9"),
        ]),
        new("by-department", "By department", "department", []),
    ];

    // ─── Reading and writing ─────────────────────────────────────────────

    static SchemeEntry? ReadEntry(JsonNode? n)
    {
        if (n is not JsonObject o || ((string?)o["value"])?.Trim() is not { } value) return null;
        static string? Colour(JsonNode? c) => (string?)c is { Length: 7 } s && s[0] == '#' && s[1..].All(Uri.IsHexDigit) ? s.ToLowerInvariant() : null;
        return new SchemeEntry(value, Colour(o["color"]), ((string?)o["hatch"])?.Trim() is { Length: > 0 } h ? h : null,
            Json.Number(o["hatchScale"]) is { } sc && sc > 0 ? sc : 1, Colour(o["hatchColor"]));
    }

    static ColorScheme? Read(JsonNode? n)
    {
        if (n is not JsonObject o || (string?)o["id"] is not { Length: > 0 } id) return null;
        return new ColorScheme(id, (string?)o["name"] ?? "Scheme", (string?)o["by"] is { Length: > 0 } by ? by : "function",
            (o["entries"] as JsonArray)?.Select(ReadEntry).OfType<SchemeEntry>().ToList() ?? []);
    }

    public static JsonObject ToJson(SchemeEntry e)
    {
        var o = new JsonObject { ["value"] = e.Value };
        if (e.Color != null) o["color"] = e.Color;
        if (e.Hatch != null) { o["hatch"] = e.Hatch; o["hatchScale"] = e.HatchScale; }
        if (e.HatchColor != null) o["hatchColor"] = e.HatchColor;
        return o;
    }

    public static JsonObject ToJson(ColorScheme s) => new()
    {
        ["id"] = s.Id, ["name"] = s.Name, ["by"] = s.By,
        ["entries"] = new JsonArray(s.Entries.Select(e => (JsonNode)ToJson(e)).ToArray()),
    };

    /// <summary>The project's schemes, else the defaults.</summary>
    public static IReadOnlyList<ColorScheme> Of(IModelView m) =>
        Spatial.Project(m)?.Attrs[Key] is JsonArray arr ? arr.Select(Read).OfType<ColorScheme>().ToList() : Defaults;

    public static ColorScheme? Find(IModelView m, string? id) => id == null ? null : Of(m).FirstOrDefault(s => s.Id == id);

    /// <summary>The scheme a plan view fills its rooms with; null: none.</summary>
    public static ColorScheme? OfView(IModelView m, Entity view) => Find(m, view.Str("colorScheme"));

    static void Write(ICommandContext ctx, IEnumerable<ColorScheme> schemes)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        attrs[Key] = new JsonArray(schemes.Select(s => (JsonNode)ToJson(s)).ToArray());
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>"name copy", then "name copy 2"…, the first no other scheme has.</summary>
    static string CopyName(string name, IEnumerable<string> taken)
    {
        var set = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = name + " copy";
        for (var n = 2; set.Contains(candidate); n++) candidate = $"{name} copy {n}";
        return candidate;
    }

    static ColorScheme Require(IModelView m, string? id) => Find(m, id) ?? throw new CommandException("There is no such colour scheme.");

    /// <summary>The value a room has for a field: its name, else the attribute (a number written as it reads).</summary>
    public static string ValueOf(Entity room, string by)
    {
        if (by == "name") return room.Name;
        return room.Attrs[by] switch
        {
            JsonValue v when v.TryGetValue<string>(out var s) => s.Trim(),
            JsonValue v when Json.Number(v) is { } d => d.ToString("0.###", CultureInfo.InvariantCulture),
            _ => "",
        };
    }

    /// <summary>How a room looks under a scheme: its own colour and hatch over its entry's.</summary>
    public sealed record Look(string? Color, string? Hatch, double HatchScale, string HatchColor);

    public static Look? LookOf(ColorScheme s, Entity room)
    {
        var e = s.Entry(ValueOf(room, s.By));
        var color = room.Str("fillColor") ?? e?.Color;
        var hatch = room.Str("fillHatch") ?? e?.Hatch;
        if (color == null && hatch == null) return null;
        var scale = room.Str("fillHatch") != null ? room.NumOrNull("hatchScale") ?? 1 : e?.HatchScale ?? 1;
        return new Look(color, hatch, scale > 0 ? scale : 1, room.Str("hatchColor") ?? e?.HatchColor ?? "#555555");
    }

    /// <summary>The scheme's entries with a colour each for every value the rooms have: new values get palette colours, entries without a colour get one.</summary>
    public static ColorScheme Filled(ColorScheme s, IModelView m)
    {
        var entries = s.Entries.ToList();
        var used = entries.Select(e => e.Color).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        string Next()
        {
            var c = Palette.FirstOrDefault(p => !used.Contains(p)) ?? Palette[used.Count % Palette.Length];
            used.Add(c);
            return c;
        }
        for (var i = 0; i < entries.Count; i++)
            if (entries[i].Color == null && entries[i].Hatch == null) entries[i] = entries[i] with { Color = Next() };
        foreach (var value in m.OfClass(Rooms.Class).Select(r => ValueOf(r, s.By)).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
            if (!entries.Any(e => string.Equals(e.Value, value, StringComparison.OrdinalIgnoreCase))) entries.Add(new SchemeEntry(value, Next()));
        return s with { Entries = entries };
    }

    static IReadOnlyList<SchemeEntry> ParseEntries(JsonNode? n) =>
        n is JsonArray arr ? arr.Select(ReadEntry).OfType<SchemeEntry>().GroupBy(e => e.Value, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).ToList()
            : throw new CommandException("A scheme's entries are a list of {value, color, hatch, hatchScale, hatchColor}.");

    static string CheckBy(string? by) => by is { Length: > 0 } b && (Fields.Any(f => f.Key == b) || b.All(ch => char.IsLetterOrDigit(ch) || ch == '_'))
        ? b : throw new CommandException("A scheme colours rooms by a field: function, name, department, number or floor finish.");

    // ─── Commands and queries ────────────────────────────────────────────

    public static void Register(IModuleRegistry r)
    {
        // A new scheme (a copy of another with from), its entries filled from the values the rooms have.
        r.Command("createColorScheme", "New colour scheme", (ctx, a) =>
        {
            var schemes = Of(ctx.Model).ToList();
            var from = (string?)a["from"] is { } fid ? Require(ctx.Model, fid) : null;
            var by = CheckBy((string?)a["by"] ?? from?.By ?? "function");
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : from != null ? CopyName(from.Name, schemes.Select(s => s.Name)) : $"By {Fields.FirstOrDefault(f => f.Key == by).Label?.ToLowerInvariant() ?? by}";
            var made = Filled(new ColorScheme(ctx.NewId(), name, by, from != null && from.By == by ? from.Entries : []), ctx.Model);
            Write(ctx, [.. schemes, made]);
            return new JsonObject { ["id"] = made.Id };
        });

        // Its name, the field it colours by (its entries stay; fill them again for the new field's values), its entries (all of them).
        r.Command("updateColorScheme", "Change colour scheme", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            if (((string?)a["name"])?.Trim() is { Length: > 0 } n) s = s with { Name = n };
            if ((string?)a["by"] is { } by) s = s with { By = CheckBy(by), Entries = by == s.By ? s.Entries : [] };
            if (a.ContainsKey("entries")) s = s with { Entries = ParseEntries(a["entries"]) };
            if (a["fill"] is JsonValue fv && fv.TryGetValue<bool>(out var fill) && fill) s = Filled(s, ctx.Model);
            Write(ctx, Of(ctx.Model).Select(x => x.Id == s.Id ? s : x));
            return null;
        });

        // One value's look: colour, hatch, hatch scale and colour (null clears a field); the value is added when the scheme has none.
        r.Command("setColorSchemeEntry", "Room colour", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var value = ((string?)a["value"])?.Trim() ?? throw new CommandException("Which value?");
            var e = s.Entry(value) ?? new SchemeEntry(value, null);
            var patch = new JsonObject();
            foreach (var (k, v) in ToJson(e)) patch[k] = v?.DeepClone();
            foreach (var k in new[] { "color", "hatch", "hatchScale", "hatchColor" })
                if (a.ContainsKey(k)) { if (a[k] == null) patch.Remove(k); else patch[k] = a[k]!.DeepClone(); }
            var next = ReadEntry(patch) ?? e;
            List<SchemeEntry> entries = s.Entry(value) != null
                ? s.Entries.Select(x => string.Equals(x.Value, value, StringComparison.OrdinalIgnoreCase) ? next : x).ToList()
                : [.. s.Entries, next];
            if (a["remove"] is JsonValue rv && rv.TryGetValue<bool>(out var remove) && remove) entries = entries.Where(x => !string.Equals(x.Value, value, StringComparison.OrdinalIgnoreCase)).ToList();
            Write(ctx, Of(ctx.Model).Select(x => x.Id == s.Id ? s with { Entries = entries } : x));
            return null;
        });

        // The values the rooms have that the scheme does not list, added with colours of the palette (and entries without one given one).
        r.Command("fillColorScheme", "Fill missing colours", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            var filled = Filled(s, ctx.Model);
            Write(ctx, Of(ctx.Model).Select(x => x.Id == s.Id ? filled : x));
            return new JsonObject { ["added"] = filled.Entries.Count - s.Entries.Count };
        });

        // A scheme removed: the views that used it fill no rooms, the legends that named it follow their view's.
        r.Command("deleteColorScheme", "Delete colour scheme", (ctx, a) =>
        {
            var s = Require(ctx.Model, (string?)a["id"]);
            Write(ctx, Of(ctx.Model).Where(x => x.Id != s.Id));
            foreach (var v in ctx.Model.OfClass(Views.Class).Where(v => v.Str("colorScheme") == s.Id).ToList())
            {
                var attrs = v.CloneAttrs();
                attrs.Remove("colorScheme");
                ctx.Replace(v.With(attrs: attrs));
            }
            foreach (var l in ctx.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == Legend && x.Str("scheme") == s.Id).ToList())
            {
                var attrs = l.CloneAttrs();
                attrs.Remove("scheme");
                ctx.Replace(l.With(attrs: attrs));
            }
            return null;
        });

        // The scheme a plan fills its rooms with (null: none).
        r.Command("viewColorScheme", "Room colours", (ctx, a) =>
        {
            var view = ctx.Model.Get((string?)a["viewId"] ?? (string?)a["id"] ?? "") is { Class: Views.Class } v ? v : throw new CommandException("Room colours are a view's: open a plan.");
            var attrs = view.CloneAttrs();
            if ((string?)a["schemeId"] is { } sid) attrs["colorScheme"] = Require(ctx.Model, sid).Id; else attrs.Remove("colorScheme");
            ctx.Replace(view.With(attrs: attrs));
            return null;
        });

        // A colour legend where the user clicks on a view: the entries its rooms use (scheme: the view's unless named; showArea: each entry's total area).
        r.Command("addColorLegend", "Colour legend", (ctx, a) =>
        {
            var view = ctx.Model.Get((string?)a["viewId"] ?? "") is { Class: Views.Class } v ? v : throw new CommandException("A legend goes on a view: open a plan.");
            var at = Args.Point(a, "x", "y");
            var attrs = new JsonObject { ["viewId"] = view.Id, ["at"] = new JsonArray(at.X, at.Y), ["showArea"] = a["showArea"] is JsonValue sa && sa.TryGetValue<bool>(out var show) && show };
            if ((string?)a["schemeId"] is { } sid) attrs["scheme"] = Require(ctx.Model, sid).Id;
            var legend = new Entity(ctx.NewId(), IfcClass.Annotation, "Colour legend", attrs, Legend);
            ctx.Add(legend);
            return new JsonObject { ["id"] = legend.Id };
        });

        // What the configurator shows: the schemes, the fields, the values the rooms have for each (how many rooms, how much area), the view's scheme.
        r.Query("colorSchemes", (q, a) =>
        {
            var m = q.Model;
            var rooms = m.OfClass(Rooms.Class).Select(r => (Room: r, Outline: Rooms.Of(r, m, q.Geometry))).ToList();
            var values = new JsonObject();
            foreach (var (key, _) in Fields)
                values[key] = new JsonArray(rooms.Select(x => (Value: ValueOf(x.Room, key), Area: x.Outline?.NetArea ?? 0)).Where(x => x.Value.Length > 0)
                    .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(g => (JsonNode)new JsonObject { ["value"] = g.First().Value, ["count"] = g.Count(), ["area"] = Math.Round(g.Sum(x => x.Area), 4) }).ToArray());
            var view = (string?)a["viewId"] is { } vid ? Views.Find(m, vid) : null;
            return new JsonObject
            {
                ["schemes"] = new JsonArray(Of(m).Select(s => (JsonNode)ToJson(s)).ToArray()),
                ["fields"] = new JsonArray(Fields.Select(f => (JsonNode)new JsonObject { ["key"] = f.Key, ["label"] = f.Label }).ToArray()),
                ["values"] = values,
                ["palette"] = new JsonArray(Palette.Select(p => (JsonNode)p).ToArray()),
                ["functions"] = new JsonArray(Rooms.Functions.Select(f => (JsonNode)f).ToArray()),
                ["viewScheme"] = view?.Str("colorScheme"),
                ["rooms"] = rooms.Count,
            };
        });
    }

    // ─── Legends ─────────────────────────────────────────────────────────

    /// <summary>
    /// The colour legends on a view: the scheme's name, then a swatch and the
    /// value of each entry the view's rooms use (in the scheme's order), and
    /// their total area when asked; at the legend's point, its top left.
    /// </summary>
    public static IEnumerable<ViewMark> Legends(ViewContext c)
    {
        var m = c.Model;
        var legends = m.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == Legend && x.Str("viewId") == c.View.Id).ToList();
        if (legends.Count == 0) yield break;
        var storeyId = c.View.Str("storeyId");
        var hiddenElements = (c.View.Attrs["hiddenElements"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        var rooms = m.OfClass(Rooms.Class).Where(r => !hiddenElements.Contains(r.Id) && (c.Kind != "plan" || Spatial.StoreyOf(m, r.Id)?.Id == storeyId)).ToList();
        var patterns = DraftingResources.Patterns(m);
        var style = Standards.TextOf(c.Book, null);
        foreach (var legend in legends)
        {
            if (legend.Attrs["at"] is not JsonArray { Count: >= 2 } atj) continue;
            var at = new Vec2(Json.Number(atj[0]) ?? 0, Json.Number(atj[1]) ?? 0);
            var scheme = Find(m, legend.Str("scheme")) ?? OfView(m, c.View);
            var mark = new ViewMark(legend.Id);
            var h = style.Height * c.Mm;
            var color = style.Color ?? "#000000";
            if (scheme == null)
            {
                mark.Text(new PenText(at, "Colour legend: the view has no colour scheme", style.Height, 0, "start", "bottom", false, style.Font), "#888888");
                mark.Line(at, at + new Vec2(h * 20, 0), "PICK", 0);
                mark.Snaps.Add(at);
                yield return mark;
                continue;
            }
            mark.Text(new PenText(at, scheme.Name, style.Height, 0, "start", "bottom", true, style.Font), color);
            var used = rooms.GroupBy(r => ValueOf(r, scheme.By), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            var (sw, sh, row) = (10 * c.Mm, 5 * c.Mm, 7 * c.Mm);
            var y = at.Y - row;
            var showArea = legend.Bool("showArea");
            var width = h * 0.6 * scheme.Name.Length;
            foreach (var e in scheme.Entries)
            {
                if (!used.TryGetValue(e.Value, out var its) || (e.Color == null && e.Hatch == null)) continue;
                var box = new List<Vec2> { new(at.X, y), new(at.X + sw, y), new(at.X + sw, y + sh), new(at.X, y + sh) };
                IReadOnlyList<IReadOnlyList<Vec2>> loops = [box];
                if (e.Color != null) mark.Fills.Add((loops, e.Color));
                if (e.Hatch != null && HatchLibrary.Find(e.Hatch, patterns) is { } pattern)
                {
                    if (pattern.Solid) mark.Fills.Add((loops, e.HatchColor ?? "#555555"));
                    else foreach (var s in HatchFill.Segments(loops, pattern, new HatchOptions(e.HatchScale * c.Mm, 0, null, 4_000)).Segments) mark.Line(s.A, s.B, "LEGEND", Pens.Default.Hatch, e.HatchColor ?? "#555555");
                }
                mark.Polyline(box, true, "LEGEND", Pens.Default.Hatch, color);
                var label = e.Value;
                if (showArea) label += "   " + Rooms.FormatArea(its.Sum(r => Rooms.Of(r, m, c.Geometry)?.NetArea ?? 0), c.Book.Units);
                mark.Text(new PenText(new Vec2(at.X + sw + 2 * c.Mm, y + sh / 2), label, style.Height, 0, "start", "middle", false, style.Font), color);
                width = Math.Max(width, sw + 2 * c.Mm + h * 0.6 * label.Length);
                y -= row;
            }
            // What a click takes: the legend's frame.
            mark.Polyline([new(at.X - c.Mm, y + row - c.Mm), new(at.X + width + c.Mm, y + row - c.Mm), new(at.X + width + c.Mm, at.Y + h + c.Mm), new(at.X - c.Mm, at.Y + h + c.Mm)], true, "PICK", 0);
            mark.Snaps.Add(at);
            yield return mark;
        }
    }
}
