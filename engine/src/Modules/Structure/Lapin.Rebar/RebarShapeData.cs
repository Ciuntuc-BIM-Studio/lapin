using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// Bar shapes as data (docs/plan-user-extensibility.md §6.3): the shape codes of BS 8666, ISO 4066, a country's or the office's,
/// made by the user and kept in the project (<c>LapinBarShape</c>) or a <c>.lapinbarshapes</c> file. A shape is its parameters
/// (A, B, C… in metres, with their defaults and least values) and its legs, drawn as a pen walks: each leg's length a parameter or
/// a formula of them (<c>A - 2 * B</c>), then a turn (°, counter-clockwise; a number or a formula); the hooks it is born with, its
/// layer and diameter. A bar of it keeps the definition it was made with (<c>shapeDef</c>), under a code its content gives
/// (<c>user:…</c>), so a project opens with its bars whole anywhere; Lapin's own shapes stay the code's.
/// </summary>
public static class RebarShapeData
{
    public const string Class = "LapinBarShape";
    public const string Prefix = "user:";
    public const string FileFormat = "lapin-bar-shapes";
    public const int MaxLegs = 24;

    static readonly ConcurrentDictionary<string, ShapeDef> Known = new();

    /// <summary>A content code for a definition: the same definition the same code, anywhere.</summary>
    public static string CodeOf(JsonObject def)
    {
        var text = def.ToJsonString();
        return Prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
    }

    public static ShapeDef? Find(string? code) => code != null && Known.TryGetValue(code, out var d) ? d : null;

    /// <summary>A definition checked and known by its code (the shape a bar names), its code returned.</summary>
    public static string Register(JsonNode? n)
    {
        var def = Check(n);
        var code = CodeOf(def);
        Known.GetOrAdd(code, _ => Build(code, def));
        return code;
    }

    static double Num(JsonNode? n, double d) => Json.Number(n) ?? d;

    /// <summary>A definition as a command or a file gives it, checked: its parameters, legs that read, hooks; refused with why.</summary>
    public static JsonObject Check(JsonNode? n)
    {
        if (n is not JsonObject o) throw new CommandException("A bar shape is {code, label, params, legs}.");
        var code = ((string?)o["code"])?.Trim() is { Length: > 0 and <= 40 } c ? c : throw new CommandException("A bar shape needs its code (as the standard names it: 21, 11, L1…).");
        var label = ((string?)o["label"])?.Trim() is { Length: > 0 } l ? l : $"Shape {code}";
        var ps = new JsonArray();
        var keys = new List<string>();
        foreach (var p in o["params"] as JsonArray ?? [])
        {
            if (p is not JsonObject po || ((string?)po["key"])?.Trim() is not { Length: > 0 and <= 12 } key || !key.All(ch => char.IsLetterOrDigit(ch) || ch == '_') || !char.IsLetter(key[0]))
                throw new CommandException("Each parameter has a key of letters and digits (A, B, R1).");
            if (keys.Contains(key, StringComparer.OrdinalIgnoreCase)) throw new CommandException($"The parameter „{key}” is there twice.");
            keys.Add(key);
            var def = Num(po["default"], 0.5);
            var min = Num(po["min"], 0.01);
            if (!(def > 0 && def <= 50) || !(min >= 0 && min <= def)) throw new CommandException($"{key}: its default is above 0 and at most 50 m, its least value at most its default.");
            ps.Add((JsonNode)new JsonObject { ["key"] = key, ["label"] = ((string?)po["label"])?.Trim() is { Length: > 0 } pl ? pl : key, ["default"] = def, ["min"] = min });
        }
        var values = keys.ToDictionary(k => k, k => Num(((JsonArray)ps).OfType<JsonObject>().First(x => (string?)x["key"] == k)["default"], 0.5), StringComparer.OrdinalIgnoreCase);
        double? Name(string k) => values.TryGetValue(k, out var v) ? v : null;
        if (o["legs"] is not JsonArray legs || legs.Count == 0 || legs.Count > MaxLegs) throw new CommandException($"A bar shape has one to {MaxLegs} legs.");
        var checkedLegs = new JsonArray();
        for (var i = 0; i < legs.Count; i++)
        {
            if (legs[i] is not JsonObject lo) throw new CommandException($"Leg {i + 1} is {{length, turn}}.");
            var length = lo["length"]?.ToString()?.Trim() ?? throw new CommandException($"Leg {i + 1} needs its length (a parameter or a formula).");
            var (lv, le) = Expr.TryEval(length, Name);
            if (le != null) throw new CommandException($"Leg {i + 1}'s length: {le}");
            if (!(lv > 0)) throw new CommandException($"Leg {i + 1} is not longer than 0 with the defaults.");
            var leg = new JsonObject { ["length"] = length };
            if (i < legs.Count - 1)
            {
                var turn = lo["turn"]?.ToString()?.Trim() ?? "90";
                var (tv, te) = Expr.TryEval(turn, Name);
                if (te != null) throw new CommandException($"Leg {i + 1}'s turn: {te}");
                if (tv is not (>= -180 and <= 180)) throw new CommandException($"Leg {i + 1}'s turn is −180° to 180°.");
                leg["turn"] = turn;
            }
            checkedLegs.Add((JsonNode)leg);
        }
        var o2 = new JsonObject { ["code"] = code, ["label"] = label, ["params"] = ps, ["legs"] = checkedLegs };
        if (((string?)o["standard"])?.Trim() is { Length: > 0 } st) o2["standard"] = st;
        o2["layer"] = ((string?)o["layer"])?.Trim() is { Length: > 0 } ly ? ly : RebarShapes.LongitudinalLayer;
        var d = Num(o["diameter"], 0.012);
        o2["diameter"] = d is >= 0.004 and <= 0.05 ? d : throw new CommandException("A shape's diameter is 4 to 50 mm.");
        if (o["hooks"] is JsonObject h)
        {
            var hooks = new JsonObject();
            foreach (var end in new[] { "start", "end" })
                if (Json.Number(h[end]?["angle"]) is { } a && a is >= 10 and <= 180) hooks[end] = new JsonObject { ["angle"] = a };
            if (hooks.Count > 0) o2["hooks"] = hooks;
        }
        return o2;
    }

    /// <summary>The corners of a definition's legs for the parameters given: a pen walking from the origin along +x.</summary>
    public static List<Vec2> Corners(JsonObject def, IReadOnlyDictionary<string, double> p)
    {
        double? Name(string k) => p.TryGetValue(k, out var v) ? v
            : (def["params"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(x => string.Equals((string?)x["key"], k, StringComparison.OrdinalIgnoreCase)) is { } d ? Num(d["default"], 0.5) : null;
        var pts = new List<Vec2> { default };
        var heading = 0.0;
        foreach (var leg in (def["legs"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var length = Expr.TryEval(leg["length"]?.ToString() ?? "0", Name).Value ?? 0;
            pts.Add(pts[^1] + new Vec2(Math.Cos(heading), Math.Sin(heading)) * Math.Max(length, 0));
            if (leg["turn"] != null) heading += (Expr.TryEval(leg["turn"]!.ToString(), Name).Value ?? 0) * Math.PI / 180;
        }
        return pts;
    }

    static ShapeDef Build(string code, JsonObject def)
    {
        var ps = (def["params"] as JsonArray ?? []).OfType<JsonObject>().Select(p => new ShapeParam((string)p["key"]!, (string?)p["label"] ?? (string)p["key"]!, Num(p["default"], 0.5), Num(p["min"], 0.01))).ToList();
        var hooks = def["hooks"] as JsonObject;
        Func<double, (Hook? Start, Hook? End)>? born = hooks == null ? null : d =>
            (Json.Number(hooks["start"]?["angle"]) is { } a0 ? new Hook(a0, RebarShapes.HookLength(d, a0), true) : null,
             Json.Number(hooks["end"]?["angle"]) is { } a1 ? new Hook(a1, RebarShapes.HookLength(d, a1), true) : null);
        var label = $"{(string?)def["code"]} {(string?)def["label"]}".Trim();
        return new ShapeDef(code, label, ps, (string?)def["layer"] ?? RebarShapes.LongitudinalLayer, Num(def["diameter"], 0.012),
            p => Corners(def, p), _ => [], born);
    }

    /// <summary>A project shape's definition by id; null when there is none.</summary>
    public static JsonObject? DefOf(IModelView m, string? id) => m.Get(id ?? "") is { Class: Class } e ? e.Attrs["def"] as JsonObject : null;

    /// <summary>The A, B, C… of a bar as a schedule writes them (mm): each leg's length between the corners, hooks apart.</summary>
    public static List<(string Name, double Mm)> Dims(RebarSpec s)
    {
        var def = RebarShapes.Def(s.Shape);
        var pts = def.FreeVertices && s.Vertices is { Count: >= 2 } ? s.Vertices.ToList() : def.Vertices(s.Params).ToList();
        var o = new List<(string, double)>();
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            var len = (pts[i + 1] - pts[i]).Length;
            if (len < 1e-9) continue;
            // A closed stirrup's last leg is its first again: not a dimension of its own.
            if (s.Shape == "stirrup" && i == pts.Count - 2) break;
            o.Add((i < 26 ? ((char)('A' + o.Count)).ToString() : $"L{o.Count + 1}", Math.Round(len * 1000)));
        }
        return o;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Bar shape", [], "#1d4ed8", Kind: "resource"));

        // The shapes to draw bars with: Lapin's own, then the project's (with their standard's codes).
        r.Query("shapeLibrary", (q, _) => new JsonObject
        {
            ["builtin"] = new JsonArray(RebarShapes.Catalogue.Select(s => (JsonNode)new JsonObject { ["code"] = s.Code, ["label"] = s.Label }).ToArray()),
            ["shapes"] = new JsonArray(q.Model.OfClass(Class).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Select(e => (JsonNode)new JsonObject
            {
                ["id"] = e.Id, ["name"] = e.Name, ["def"] = e.Attrs["def"]?.DeepClone(),
                ["uses"] = q.Model.OfClass(Rebars.Class).Concat(q.Model.OfClass(Rebars.RunClass)).Count(b => b.Str("shapeId") == e.Id),
            }).ToArray()),
        });

        // A shape's corners for its defaults (or parameters given): the editor's preview.
        r.Query("shapePreview", (q, a) =>
        {
            var def = Check(a["def"] ?? DefOf(q.Model, (string?)a["id"]));
            var p = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in a["params"] as JsonObject ?? []) if (Json.Number(v) is { } x) p[k] = x;
            return new JsonObject { ["corners"] = new JsonArray(Corners(def, p).Select(c => (JsonNode)new JsonArray(Math.Round(c.X, 6), Math.Round(c.Y, 6))).ToArray()) };
        });

        r.Command("createShape", "New bar shape", (ctx, a) =>
        {
            var def = Check(a["def"]);
            var name = $"{(string)def["code"]!} {(string)def["label"]!}".Trim();
            var e = new Entity(ctx.NewId(), Class, name, new JsonObject { ["def"] = def });
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });

        // A shape changed: its bars take the new definition, their parameters kept where it still has them; one undo step.
        r.Command("updateShape", "Change bar shape", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("There is no such bar shape.");
            var def = Check(a["def"]);
            var code = Register(def);
            ctx.Replace(e.With(name: $"{(string)def["code"]!} {(string)def["label"]!}".Trim(), attrs: new JsonObject { ["def"] = def }));
            foreach (var b in ctx.Model.OfClass(Rebars.Class).Concat(ctx.Model.OfClass(Rebars.RunClass)).Where(b => b.Str("shapeId") == e.Id).ToList())
            {
                var attrs = b.CloneAttrs();
                attrs["shape"] = code;
                attrs["shapeDef"] = def.DeepClone();
                ctx.Replace(b.With(attrs: attrs));
            }
            return null;
        });

        r.Command("deleteShape", "Delete bar shape", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("There is no such bar shape.");
            // Its bars keep their own copy of it: they stay as they are.
            ctx.Remove(e.Id);
            return null;
        });

        r.Query("exportShapes", (q, a) =>
        {
            var ids = a["ids"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToHashSet() : null;
            var list = q.Model.OfClass(Class).Where(e => ids == null || ids.Contains(e.Id)).Select(e => (JsonNode?)e.Attrs["def"]?.DeepClone()).ToArray();
            if (list.Length == 0) throw new CommandException("The project has no bar shapes of its own to write.");
            var o = new JsonObject { ["format"] = FileFormat, ["version"] = 1, ["shapes"] = new JsonArray(list) };
            return new JsonObject { ["text"] = o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ["file"] = "shapes.lapinbarshapes" };
        });

        // A .lapinbarshapes file brought in: each shape whose code the project lacks added; one it has with the same code changed when `update`, else left.
        r.Command("importShapes", "Import bar shapes", (ctx, a) =>
        {
            JsonNode? n;
            try { n = JsonNode.Parse((string?)a["text"] ?? ""); } catch (JsonException) { throw new CommandException("That is not a bar shapes file (.lapinbarshapes)."); }
            if (n is not JsonObject o || (string?)o["format"] != FileFormat) throw new CommandException("That is not a bar shapes file (.lapinbarshapes).");
            var update = a["update"] is JsonValue uv && uv.TryGetValue<bool>(out var u) && u;
            int added = 0, updated = 0;
            foreach (var s in o["shapes"] as JsonArray ?? [])
            {
                var def = Check(s);
                var name = $"{(string)def["code"]!} {(string)def["label"]!}".Trim();
                var mine = ctx.Model.OfClass(Class).FirstOrDefault(e => string.Equals((string?)e.Attrs["def"]?["code"], (string?)def["code"], StringComparison.OrdinalIgnoreCase)
                    && string.Equals((string?)e.Attrs["def"]?["standard"], (string?)def["standard"], StringComparison.OrdinalIgnoreCase));
                if (mine == null) { ctx.Add(new Entity(ctx.NewId(), Class, name, new JsonObject { ["def"] = def })); added++; }
                else if (update && !JsonNode.DeepEquals(mine.Attrs["def"], def)) { ctx.Replace(mine.With(name: name, attrs: new JsonObject { ["def"] = def })); updated++; }
            }
            return new JsonObject { ["added"] = added, ["updated"] = updated };
        });
    }
}
