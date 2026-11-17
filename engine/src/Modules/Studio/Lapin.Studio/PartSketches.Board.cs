using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// A part's own sketch edited on the shared sketch canvas (audit S11 stage 1, docs/plan-sketch-canvas.md): "edit in a view", as an
/// outline's Edit sketch is (<see cref="OutlineSketches"/>).
///
/// <para><b>Opening</b> (<c>studio.editPartSketch {partId, sketchId}</c>, one undo step) makes a reference plane of its own lying where
/// the sketch lies (for a placed part, where that placed part puts it; the plane's x and y are the sketch's u and v, so the shapes are
/// copied as they are), marked with the part and the sketch (<see cref="Key"/>), the placed part hidden in its 2D drawing, and draws
/// the sketch's shapes on it (each draft remembering its shape's id, <see cref="ShapeKey"/>) with its constraints and dimensions (their
/// formulas naming the part's parameters: <see cref="SketchConstraints.ParamsOf"/> gives the defaults for such a view). Every drafting
/// command, snap, grip, constraint and dimension then works on it as on any sketch, each its own undo step. Opened again while it is
/// open, the same view is answered.</para>
///
/// <para><b>Finishing</b> (<c>studio.finishPartSketch {viewId}</c>, one undo step) writes the view's shapes and constraints back into
/// the part's definition: a shape keeps its id (what the bodies' outlines and holes name), a new one takes the next free <c>pN</c>, a
/// constraint brought from the part keeps its id; the sketch is solved with the defaults and the definition kept as every part edit
/// is (<see cref="Commit"/>: refused, nothing changed and the view left open, when it does not solve, a body loses its outline or a
/// placed part's values would not build). The view goes. <c>studio.cancelPartSketch {viewId}</c> drops the view: the part as it was.
/// A part's sketch takes lines, polylines, circles and arcs: an ellipse, a hatch, a text or a construction line is refused at Finish,
/// saying which.</para>
/// </summary>
public static partial class PartSketches
{
    /// <summary>The attribute marking a part sketch's board: {partId (what it was opened from), typeId, sketchId}.</summary>
    public const string Key = "partSketchOf";

    /// <summary>On a draft of the board: the id of the sketch's shape it stands for.</summary>
    public const string ShapeKey = "partShape";

    /// <summary>On the board: the view's constraint ids → the part's constraint ids they came from.</summary>
    const string ConstraintsKey = "partConstraints";

    static CommandEntry Catalogued(ICommandContext ctx, string name) =>
        (ctx.Services as ModuleCatalog)?.Command(name) ?? throw new CommandException($"Editing a part's sketch needs {name}, not in this product.");

    /// <summary>The definition of the part whose sketch a view is the board of, or null for any other view.</summary>
    internal static JsonObject? BoardPart(IModelView m, string viewId) =>
        m.Get(viewId) is { } v && v.Attrs[Key] is JsonObject of && m.Get((string?)of["typeId"] ?? "") is { } type ? Parts.DefOf(type) : null;

    /// <summary>The board a part's sketch is open on, when it is.</summary>
    public static Entity? BoardOf(IModelView m, string typeId, string sketchId) =>
        m.OfClass(Views.Class).FirstOrDefault(v => v.Attrs[Key] is JsonObject of && (string?)of["typeId"] == typeId && (string?)of["sketchId"] == sketchId);

    static (Entity View, JsonObject Of) Board(IModelView m, JsonObject a)
    {
        var view = Views.Find(m, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
        return view.Attrs[Key] is JsonObject of ? (view, of) : throw new CommandException("That view is not a part's sketch.");
    }

    /// <summary>{partId, sketchId} → {viewId, ids (the drafts), constraints (how many came)}.</summary>
    public static JsonNode? EditSketch(ICommandContext ctx, JsonObject a)
    {
        var (type, def) = PartOf(ctx.Model, (string?)a["partId"]);
        var sid = (string?)a["sketchId"];
        var sketch = Find(def, sid) ?? throw new CommandException($"„{type.Name}” has no sketch „{sid}”.");
        if (BoardOf(ctx.Model, type.Id, sid!) is { } open)
            return new JsonObject { ["viewId"] = open.Id, ["ids"] = new JsonArray([.. Drafts.On(ctx.Model, open.Id).Select(d => (JsonNode)d.Id)]), ["constraints"] = 0 };
        var instance = ctx.Model.Get((string?)a["partId"] ?? "") is { } e && Parts.IsInstance(e) ? e : null;
        var (origin, right, up) = Parts.SketchFrame(ctx.Model, instance, def, sketch);
        // Facing right × up, so the plane's own up is the sketch's v (a mirrored placed part included).
        var attrs = new JsonObject { ["kind"] = RefPlanes.Kind, ["scale"] = 20 };
        foreach (var (k, v) in RefPlanes.Attrs(origin, Vec3.Cross(right, up), right)) attrs[k] = v?.DeepClone();
        attrs[Key] = new JsonObject { ["partId"] = instance?.Id ?? type.Id, ["typeId"] = type.Id, ["sketchId"] = sid };
        // The part is not drawn on its sketch's 2D view: the sketch is what it will be.
        if (instance != null) attrs[ViewHiding.Key] = new JsonArray(instance.Id);
        var view = new Entity(ctx.NewId(), Views.Class, $"Sketch: {type.Name}, {(string?)sketch["name"] ?? sid}", attrs);
        ctx.Add(view);
        var map = new Dictionary<string, string>();
        var ids = new List<string>();
        foreach (var (id, shape) in ShapesOf(sketch))
        {
            var d = Drafts.Add(ctx, view.Id, "0", shape);
            var da = d.CloneAttrs();
            da[ShapeKey] = id;
            ctx.Replace(d.With(attrs: da));
            map[id] = d.Id;
            ids.Add(d.Id);
        }
        var cs = new JsonArray();
        var from = new List<string>();
        foreach (var c in ConstraintsOf(sketch))
        {
            var refs = c["refs"] as JsonArray ?? [];
            if (refs.OfType<JsonObject>().Any(x => !map.ContainsKey((string?)x["shape"] ?? ""))) continue;
            var copy = new JsonObject { ["kind"] = (string?)c["kind"], ["viewId"] = view.Id };
            copy["refs"] = new JsonArray([.. refs.OfType<JsonObject>().Select(x => (JsonNode)new JsonObject { ["shape"] = map[(string)x["shape"]!], ["part"] = (string?)x["part"] })]);
            foreach (var k in new[] { "value", "formula", "driving", "place" })
                if (c[k] is { } v) copy[k] = v.DeepClone();
            cs.Add((JsonNode)copy);
            from.Add((string?)c["id"] ?? "");
        }
        if (cs.Count > 0)
        {
            var r = Catalogued(ctx, "studio.addConstraint").Handler(ctx, new JsonObject { ["constraints"] = cs }) as JsonObject;
            var made = (r?["ids"] as JsonArray ?? []).Select(x => (string?)x).ToList();
            var back = new JsonObject();
            for (var i = 0; i < made.Count && i < from.Count; i++) if (made[i] is { } m) back[m] = from[i];
            var now = ctx.Model.Get(view.Id)!;
            var va = now.CloneAttrs();
            va[ConstraintsKey] = back;
            ctx.Replace(now.With(attrs: va));
        }
        return new JsonObject { ["viewId"] = view.Id, ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["constraints"] = cs.Count };
    }

    /// <summary>{viewId} → {partId, sketchId, ids}: the board written back into the part, one undo step.</summary>
    public static JsonNode? FinishSketch(ICommandContext ctx, JsonObject a)
    {
        var (view, of) = Board(ctx.Model, a);
        var type = ctx.Model.Get((string?)of["typeId"] ?? "") ?? throw new CommandException("The part whose sketch this is is gone.");
        var def = (JsonObject)(Parts.DefOf(type) ?? throw new CommandException($"„{type.Name}” has no part definition.")).DeepClone();
        var sid = (string?)of["sketchId"];
        var sketch = Find(def, sid) ?? throw new CommandException($"„{type.Name}” has no sketch „{sid}” any more.");
        var drafts = Drafts.On(ctx.Model, view.Id).ToList();
        // What a part's sketch takes: lines, polylines, circles and arcs.
        var taken = new List<(Entity Draft, Shape Shape)>();
        foreach (var d in drafts)
        {
            var s = Drafts.ShapeOf(d);
            if (s is null or Ellipse) throw new CommandException($"A part's sketch takes lines, polylines, circles and arcs: „{d.Name}” is not one. Delete it, or draw it with those, then Finish.");
            if (d.Str("role") == "draft" || d.Str("layer") == Drafts.ConstructionLayer)
                throw new CommandException($"A part's sketch has no construction lines: „{d.Name}” is one. Delete it, or draw it as a line, then Finish.");
            taken.Add((d, s));
        }
        // Ids: a draft keeps the shape it came from (once), the others take the next free.
        var ids = new Dictionary<string, string>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (d, _) in taken)
            if (d.Str(ShapeKey) is { } k && GoodId(k) && used.Add(k)) ids[d.Id] = k;
        foreach (var (d, _) in taken)
            if (!ids.ContainsKey(d.Id)) { var k = Next(used, "p"); used.Add(k); ids[d.Id] = k; }
        var now = taken.Select(x => (Id: ids[x.Draft.Id], x.Shape)).ToList();
        // Constraints: refs named by the shapes' ids, those brought from the part under their own.
        var back = view.Attrs[ConstraintsKey] as JsonObject;
        var cs = new List<JsonObject>();
        var cids = new HashSet<string>(StringComparer.Ordinal);
        var list = SketchConstraints.ListOf(SketchConstraints.CarrierOf(ctx.Model, view.Id));
        var pending = new List<JsonObject>();
        foreach (var c in list)
        {
            var refs = (c["refs"] as JsonArray ?? []).OfType<JsonObject>().ToList();
            if (refs.Any(x => !ids.ContainsKey((string?)x["shape"] ?? ""))) continue;
            var o = new JsonObject { ["kind"] = (string?)c["kind"] };
            o["refs"] = new JsonArray([.. refs.Select(x => (JsonNode)new JsonObject { ["shape"] = ids[(string)x["shape"]!], ["part"] = (string?)x["part"] })]);
            foreach (var k in new[] { "value", "formula", "driving", "place" })
                if (c[k] is { } v) o[k] = v.DeepClone();
            var was = (string?)back?[(string?)c["id"] ?? ""];
            if (was != null && GoodId(was) && cids.Add(was)) { var stored = new JsonObject { ["id"] = was }; foreach (var (k, v) in o) stored[k] = v?.DeepClone(); cs.Add(stored); }
            else pending.Add(o);
        }
        foreach (var o in pending)
        {
            var k = Next(cids, "c");
            cids.Add(k);
            var stored = new JsonObject { ["id"] = k };
            foreach (var (kk, v) in o) stored[kk] = v?.DeepClone();
            cs.Add(stored);
        }
        var defaults = Parts.Values(def, null);
        if (now.Count > 0)
        {
            var r = SketchConstraints.SolveLocal(now, cs, defaults);
            if (r.Error != null) throw new CommandException(r.Error + " Nothing was changed: the sketch stays open.");
            var solved = r.Shapes();
            sketch["shapes"] = new JsonArray([.. now.Select(s => (JsonNode)ShapeJson(s.Id, solved.TryGetValue(s.Id, out var sv) ? sv : s.Shape))]);
        }
        else sketch["shapes"] = new JsonArray();
        sketch["constraints"] = new JsonArray([.. cs.Select(c => (JsonNode)c)]);
        Commit(ctx, type, def);
        DropBoard(ctx, view);
        return new JsonObject { ["partId"] = (string?)of["partId"], ["sketchId"] = sid, ["ids"] = new JsonArray([.. now.Select(s => (JsonNode)s.Id)]) };
    }

    /// <summary>{viewId}: the board dropped, the part as it was.</summary>
    public static JsonNode? CancelSketch(ICommandContext ctx, JsonObject a)
    {
        var (view, _) = Board(ctx.Model, a);
        DropBoard(ctx, view);
        return null;
    }

    static void DropBoard(ICommandContext ctx, Entity view)
    {
        foreach (var d in Drafts.On(ctx.Model, view.Id).ToList()) ctx.Remove(d.Id);
        if (SketchConstraints.CarrierOf(ctx.Model, view.Id) is { } carrier) ctx.Remove(carrier.Id);
        ctx.Remove(view.Id);
    }

    static void RegisterBoard(IModuleRegistry r)
    {
        r.Command("editPartSketch", "Edit part sketch", EditSketch);
        r.Command("finishPartSketch", "Finish sketch", FinishSketch);
        r.Command("cancelPartSketch", "Cancel sketch", CancelSketch);
        // {partId, sketchId} → {viewId?}: the board a part's sketch is open on.
        r.Query("partSketchView", (q, a) =>
        {
            try
            {
                var (type, _) = PartOf(q.Model, (string?)a["partId"]);
                return new JsonObject { ["viewId"] = BoardOf(q.Model, type.Id, (string?)a["sketchId"] ?? "")?.Id };
            }
            catch (CommandException) { return new JsonObject { ["viewId"] = null }; }
        });
    }
}
