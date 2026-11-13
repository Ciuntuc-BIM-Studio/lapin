using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// A part's own sketches and bodies edited in the edit mode (decision 520, docs/plan-studio-parametric.md §9): a point dragged
/// (previewed, then committed), sketches added, renamed and removed, bodies pushed from them added, changed and removed. Every
/// command takes <c>partId</c>, the part or a placed part of it, is one undo step, and keeps the definition as every edit does
/// (<see cref="Commit"/>: solved with the defaults, every placed part's values building, or refused).
/// </summary>
public static partial class PartSketches
{
    /// <summary>The values a part's sketch is solved with for a preview: a placed part's own over the defaults, the defaults for the part.</summary>
    static Dictionary<string, double> ValuesFor(IModelView m, string? partId, JsonObject def) =>
        m.Get(partId ?? "") is { } e && Parts.IsInstance(e) ? Parts.Values(def, e.Attrs["values"] as JsonObject) : Parts.Values(def, null);

    static JsonArray ShapesJson(IReadOnlyList<(string Id, Shape Shape)> order, IReadOnlyDictionary<string, Shape> solved) =>
        new(order.Select(s => (JsonNode)ShapeJson(s.Id, solved.TryGetValue(s.Id, out var sv) ? sv : s.Shape)).ToArray());

    /// <summary>A drag's arguments: the shape of the sketch, its point part written the one way, where to.</summary>
    static (string Shape, string Part, Vec2 To) DragArgs(JsonObject sketch, JsonObject a)
    {
        var id = (string?)a["shape"];
        var shape = ShapesOf(sketch).FirstOrDefault(s => s.Id == id).Shape ?? throw new CommandException($"The sketch has no shape „{id}”.");
        if (SketchConstraints.Classify(shape, (string?)a["part"] ?? "") is not (SketchConstraints.RefKind.Point, var part))
            throw new CommandException("Drag a point: start, end, center or vertex:i.");
        var to = Args.TryPoint(a["to"]) ?? throw new CommandException("Where to: [u, v] on the sketch's plane.");
        return (id!, part, to);
    }

    /// <summary>
    /// {partId, sketchId, shape, part, to: [u, v], values?: "instance" | "defaults"} → {ok, reason?, shapes}: the sketch's shapes after
    /// the drag, solved with the placed part's values (or the defaults for the part itself); nothing kept. <c>values: "defaults"</c>
    /// solves with the part's defaults whatever <c>partId</c> names: what <c>partSketchDrag</c> keeps, so the sketch editor (which edits
    /// the part's definition, its stored shapes) previews exactly what is committed.
    /// </summary>
    public static JsonNode DragPreview(IQueryContext q, JsonObject a)
    {
        var m = q.Model;
        var (type, def) = PartOf(m, (string?)a["partId"]);
        var sketch = Find(def, (string?)a["sketchId"]) ?? throw new CommandException($"„{type.Name}” has no sketch „{(string?)a["sketchId"]}”.");
        var (shape, part, to) = DragArgs(sketch, a);
        var order = ShapesOf(sketch);
        var values = (string?)a["values"] switch
        {
            null or "instance" => ValuesFor(m, (string?)a["partId"], def),
            "defaults" => Parts.Values(def, null),
            var other => throw new CommandException($"values is „instance” or „defaults”, not „{other}”."),
        };
        var r = SketchConstraints.DragLocal(order, ConstraintsOf(sketch), values, shape, part, to);
        if (r.Error != null) return new JsonObject { ["ok"] = false, ["reason"] = r.Error, ["shapes"] = new JsonArray() };
        return new JsonObject { ["ok"] = true, ["shapes"] = ShapesJson(order, r.Shapes()) };
    }

    /// <summary>{partId, sketchId, shape, part, to: [u, v]} → {ok, shapes}: the drag kept, solved with the defaults; refused with why when the goal cannot be reached.</summary>
    public static JsonNode? Drag(ICommandContext ctx, JsonObject a)
    {
        var (type, def, sketch) = Target(ctx, a);
        var (shape, part, to) = DragArgs(sketch, a);
        var order = ShapesOf(sketch);
        var r = SketchConstraints.DragLocal(order, ConstraintsOf(sketch), Parts.Values(def, null), shape, part, to);
        if (r.Error != null) throw new CommandException(r.Error + " Nothing was changed.");
        sketch["shapes"] = ShapesJson(order, r.Shapes());
        Commit(ctx, type, def);
        return new JsonObject { ["ok"] = true, ["shapes"] = sketch["shapes"]!.DeepClone() };
    }

    // ------------------------------------------------------------ sketches

    /// <summary>{partId, name?, plane: xy | xz | yz, offset?: [x, y, z]} → {id}: a new, empty sketch of the part.</summary>
    public static JsonNode? AddSketch(ICommandContext ctx, JsonObject a)
    {
        var (type, def) = PartOf(ctx.Model, (string?)a["partId"]);
        var plane = (string?)a["plane"] ?? "xy";
        if (!Parts.Planes.Contains(plane)) throw new CommandException($"A sketch's plane is {string.Join(", ", Parts.Planes)}.");
        var list = def["sketches"] as JsonArray ?? (JsonArray)(def["sketches"] = new JsonArray());
        var id = Next(list.OfType<JsonObject>().Select(s => (string?)s["id"]).OfType<string>(), "s");
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : $"Sketch {list.Count + 1}";
        var sketch = new JsonObject { ["id"] = id, ["name"] = name, ["plane"] = plane };
        if (a["offset"] is { } off)
        {
            if (off is not JsonArray { Count: 3 } oa) throw new CommandException("A sketch's offset is [x, y, z], numbers or formulas.");
            sketch["offset"] = oa.DeepClone();
        }
        sketch["shapes"] = new JsonArray();
        sketch["constraints"] = new JsonArray();
        list.Add((JsonNode)sketch);
        Commit(ctx, type, def);
        return new JsonObject { ["id"] = id };
    }

    /// <summary>{partId, sketchId, name}.</summary>
    public static JsonNode? RenameSketch(ICommandContext ctx, JsonObject a)
    {
        var (type, def, sketch) = Target(ctx, a);
        sketch["name"] = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A sketch needs a name.");
        Commit(ctx, type, def);
        return null;
    }

    /// <summary>{partId, sketchId}: refused while a body is drawn from it (the message names them).</summary>
    public static JsonNode? RemoveSketch(ICommandContext ctx, JsonObject a)
    {
        var (type, def, sketch) = Target(ctx, a);
        var sid = (string?)sketch["id"];
        var users = Bodies(def).Where(b => (string?)b.Body["sketch"] == sid).Select(b => b.Name).ToList();
        if (users.Count > 0)
            throw new CommandException($"„{(string?)sketch["name"]}” is the outline of {string.Join(", ", users.Select(u => $"„{u}”"))}: remove those bodies or draw them from another sketch first.");
        var list = (JsonArray)def["sketches"]!;
        list.Remove(sketch);
        if (list.Count == 0) def.Remove("sketches");
        Commit(ctx, type, def);
        return null;
    }

    // ------------------------------------------------------------ bodies

    /// <summary>A definition's bodies with their names (given, or <c>Body n</c> by their place).</summary>
    static List<(string Name, JsonObject Body)> Bodies(JsonObject def) =>
        [.. (def["bodies"] as JsonArray ?? []).OfType<JsonObject>().Select((b, i) => (Parts.BodyName(b, i + 1), b))];

    /// <summary>A depth or a condition as given: a number, or a formula of the part's parameters (a yes or no is 1 or 0).</summary>
    static JsonNode Formula(JsonNode? n, IReadOnlyDictionary<string, double> defaults, string what)
    {
        JsonNode o = n switch
        {
            JsonValue v when v.TryGetValue<bool>(out var b) => JsonValue.Create(b ? 1 : 0),
            JsonValue v when v.TryGetValue<string>(out var s) && s.Trim().Length > 0 => JsonValue.Create(s.Trim()),
            JsonValue v when Json.Number(v) is { } x && double.IsFinite(x) => JsonValue.Create(x),
            _ => throw new CommandException($"{what} is a number or a formula of the part's parameters."),
        };
        Parts.Eval(o, defaults, what);
        return o;
    }

    static JsonArray? HoleIds(JsonNode? n) => n switch
    {
        null => null,
        JsonArray arr => new JsonArray([.. arr.Select(h => (JsonNode)((string?)h ?? throw new CommandException("A body's holes name shapes of its sketch.")))]),
        _ => throw new CommandException("A body's holes are [shape ids]."),
    };

    /// <summary>{partId, sketchId, shape?, holes?, depth, name?, when?} → {name}: a body pushed from a closed outline of the sketch.</summary>
    public static JsonNode? AddBody(ICommandContext ctx, JsonObject a)
    {
        var (type, def, sketch) = Target(ctx, a);
        var defaults = Parts.Values(def, null);
        var names = Bodies(def).Select(b => b.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : null;
        if (name != null && names.Contains(name)) throw new CommandException($"The part has a body „{name}” already.");
        if (name == null)
            for (var k = names.Count + 1; name == null; k++)
                if (!names.Contains($"Body {k}")) name = $"Body {k}";
        var body = new JsonObject { ["name"] = name, ["sketch"] = (string?)sketch["id"] };
        if (((string?)a["shape"])?.Trim() is { Length: > 0 } shape) body["shape"] = shape;
        if (HoleIds(a["holes"]) is { Count: > 0 } holes) body["holes"] = holes;
        body["depth"] = Formula(a["depth"], defaults, $"„{name}”'s depth");
        if (a["when"] != null) body["when"] = Formula(a["when"], defaults, $"„{name}”'s condition");
        var list = def["bodies"] as JsonArray ?? (JsonArray)(def["bodies"] = new JsonArray());
        list.Add((JsonNode)body);
        Commit(ctx, type, def);
        return new JsonObject { ["name"] = name };
    }

    static (Entity Type, JsonObject Def, JsonObject Body, string Name) BodyTarget(ICommandContext ctx, JsonObject a)
    {
        var (type, def) = PartOf(ctx.Model, (string?)a["partId"]);
        var name = (string?)a["name"];
        var (found, body) = Bodies(def).FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
        return body != null ? (type, def, body, found) : throw new CommandException($"„{type.Name}” has no body „{name}”.");
    }

    /// <summary>
    /// {partId, name, depth?, when?, newName?, shape?, holes?, sketchId?}: a body changed. <c>when</c>, <c>shape</c> or <c>holes</c>
    /// given as null are cleared (the sketch's first closed outline, no condition, no holes); <c>sketchId</c> draws it from another
    /// sketch (its outline and holes cleared unless given). A body of corners takes depth, when and a new name only.
    /// </summary>
    public static JsonNode? UpdateBody(ICommandContext ctx, JsonObject a)
    {
        var (type, def, body, name) = BodyTarget(ctx, a);
        var defaults = Parts.Values(def, null);
        if (((string?)a["newName"])?.Trim() is { Length: > 0 } newName && newName != name)
        {
            if (Bodies(def).Any(b => b.Body != body && string.Equals(b.Name, newName, StringComparison.OrdinalIgnoreCase)))
                throw new CommandException($"The part has a body „{newName}” already.");
            body["name"] = name = newName;
        }
        if (a["depth"] != null) body["depth"] = Formula(a["depth"], defaults, $"„{name}”'s depth");
        if (a.ContainsKey("when"))
        {
            if (a["when"] == null) body.Remove("when");
            else body["when"] = Formula(a["when"], defaults, $"„{name}”'s condition");
        }
        var drawn = a.ContainsKey("shape") || a.ContainsKey("holes") || a["sketchId"] != null;
        if (drawn && body["sketch"] == null) throw new CommandException($"„{name}” is drawn by its corners: only its depth, condition and name change here.");
        if ((string?)a["sketchId"] is { } sid && sid != (string?)body["sketch"])
        {
            if (Find(def, sid) == null) throw new CommandException($"„{type.Name}” has no sketch „{sid}”.");
            body["sketch"] = sid;
            body.Remove("shape");
            body.Remove("holes");
        }
        if (a.ContainsKey("shape"))
        {
            if (((string?)a["shape"])?.Trim() is { Length: > 0 } shape) body["shape"] = shape; else body.Remove("shape");
        }
        if (a.ContainsKey("holes"))
        {
            if (HoleIds(a["holes"]) is { Count: > 0 } holes) body["holes"] = holes; else body.Remove("holes");
        }
        Commit(ctx, type, def);
        return new JsonObject { ["name"] = name };
    }

    /// <summary>{partId, name}: refused for the part's last body.</summary>
    public static JsonNode? RemoveBody(ICommandContext ctx, JsonObject a)
    {
        var (type, def, body, name) = BodyTarget(ctx, a);
        var list = (JsonArray)def["bodies"]!;
        if (list.Count <= 1) throw new CommandException($"„{name}” is the part's last body: a part has one body at least.");
        // The bodies after it named by their place keep their names.
        var named = Bodies(def);
        foreach (var (n, b) in named) if (b != body && (string?)b["name"] is not { Length: > 0 }) b["name"] = n;
        list.Remove(body);
        Commit(ctx, type, def);
        return null;
    }

    static void RegisterEdit(IModuleRegistry r)
    {
        // A point of a part's sketch dragged: previewed with a placed part's values (nothing kept), then kept with the defaults.
        r.Query("partSketchDragPreview", DragPreview);
        r.Command("partSketchDrag", "Drag in part sketch", Drag);
        // A part's sketches: added (empty, on an origin plane), renamed, removed while no body is drawn from them.
        r.Command("addPartSketch", "New part sketch", AddSketch);
        r.Command("renamePartSketch", "Rename part sketch", RenameSketch);
        r.Command("removePartSketch", "Remove part sketch", RemoveSketch);
        // A part's bodies pushed from its sketches' closed outlines.
        r.Command("addPartBody", "New body", AddBody);
        r.Command("updatePartBody", "Change body", UpdateBody);
        r.Command("removePartBody", "Remove body", RemoveBody);
    }
}
