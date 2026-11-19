using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// The stair by sketch's board (users asked for Revit's "stair by sketch", 19.11.2026): a plan view of the stair's storey of its
/// own, where the stair is drawn with the Draft commands as lines of three roles: <c>boundary</c> (the stair's two sides; a line
/// with no role counts as one), <c>riser</c> (each riser, from one side to the other; none: spread as the rules ask) and
/// <c>path</c> (which way it climbs, from its start). Construction lines and shapes of other roles are left out.
///
/// <para><c>studio.editStairSketch {id}</c> opens a stair's sketch (or its outline, for a stair of its type's shape: its sides and
/// risers as drawn), the stair hidden there; <c>{storeyId, typeId?}</c> opens an empty one for a new stair. <c>studio.finishStairSketch
/// {viewId, flip?}</c> (one undo step) gives the lines to <c>architecture.addSketchStair</c> or <c>architecture.updateStairSketch</c>,
/// which check them (a sketch that makes no stair is refused, saying why, and the board stays open) and keep the sketch on the stair;
/// the view goes. <c>studio.cancelStairSketch {viewId}</c> drops it.</para>
/// </summary>
public static class StairSketches
{
    /// <summary>The attribute marking a stair's sketch view: the stair's id, or, for a new stair, <c>{storeyId, typeId?}</c>.</summary>
    public const string OfKey = "stairSketchOf";

    /// <summary>The attribute holding whether the board's stair climbs the other way round.</summary>
    public const string FlipKey = "stairSketchFlip";

    static CommandEntry Command(ICommandContext ctx, string name) =>
        (ctx.Services as ModuleCatalog)?.Command(name) ?? throw new CommandException($"A stair by sketch needs {name}, not in this product.");

    public static Entity? ViewOf(IModelView m, string stairId) => m.OfClass(Views.Class).FirstOrDefault(v => v.Str(OfKey) == stairId);

    /// <summary>A sketch's line as the stair takes it: <c>[ax, ay, bx, by, rise]</c>, an arc anticlockwise from its start.</summary>
    public static IEnumerable<JsonArray> Pieces(Shape s)
    {
        static JsonArray P(Vec2 a, Vec2 b, double rise) => new(a.X, a.Y, b.X, b.Y, rise);
        switch (s)
        {
            case Line l: yield return P(l.A, l.B, 0); break;
            case Polyline p:
                for (var i = 0; i < (p.Closed ? p.Pts.Count : p.Pts.Count - 1); i++) yield return P(p.Pts[i], p.Pts[(i + 1) % p.Pts.Count], 0);
                break;
            // Anticlockwise from its start: bulging to the right of start → end, a negative rise.
            case Arc a: yield return P(a.At(a.Start), a.At(a.End), -a.R * (1 - Math.Cos(a.Sweep / 2))); break;
            case Circle: throw new CommandException("A circle is no line of a stair's sketch: draw the sides as arcs, each from one end to the other.");
            case Ellipse: throw new CommandException("An ellipse is no line of a stair's sketch: draw the sides as lines and arcs.");
        }
    }

    /// <summary>A line of the stair's sketch as a shape to draw: a line, or an arc anticlockwise from its start.</summary>
    public static Shape ShapeOf(Vec2 a, Vec2 b, double rise)
    {
        var c = new PathCurve(a, b, rise);
        if (!c.IsArc) return new Line(a, b);
        double Angle(Vec2 p) => Math.Atan2(p.Y - c.Centre.Y, p.X - c.Centre.X);
        return rise < 0 ? new Arc(c.Centre, c.Radius, Angle(a), Angle(b)) : new Arc(c.Centre, c.Radius, Angle(b), Angle(a));
    }

    public static void Register(IModuleRegistry r)
    {
        // {id} | {storeyId, typeId?} → {viewId, ids}.
        r.Command("editStairSketch", "Stair by sketch", (ctx, a) =>
        {
            var m = ctx.Model;
            Entity? stair = null;
            Entity storey;
            JsonNode of;
            if ((string?)a["id"] is { } id)
            {
                stair = m.Get(id) ?? throw new CommandException("The stair is not in the project.");
                if (stair.Class != IfcClass.Stair) throw new CommandException("Only a stair is drawn by sketch.");
                if (stair.Attrs["reference"] != null) throw new CommandException("A stair kept as a reference from its file is not drawn by sketch.");
                if (ViewOf(m, id) is { } open) return new JsonObject { ["viewId"] = open.Id, ["ids"] = new JsonArray([.. Drafts.On(m, open.Id).Select(d => (JsonNode)d.Id)]) };
                storey = Spatial.StoreyOf(m, id) ?? throw new CommandException("The stair stands on no storey.");
                of = id;
            }
            else
            {
                storey = ((string?)a["storeyId"] is { } sid ? m.Get(sid) : Spatial.Storeys(m).FirstOrDefault()) ?? throw new CommandException("The project has no storey.");
                if (storey.Class != IfcClass.BuildingStorey) throw new CommandException("A stair stands on a storey.");
                var o = new JsonObject { ["storeyId"] = storey.Id };
                if ((string?)a["typeId"] is { } tid) o["typeId"] = tid;
                of = o;
            }
            var made = Command(ctx, "view.create").Handler(ctx, new JsonObject { ["kind"] = "plan", ["storeyId"] = storey.Id, ["name"] = stair != null ? $"Sketch: {stair.Name}" : "Sketch: new stair" }) as JsonObject;
            var view = m.Get((string?)made?["id"] ?? "") ?? throw new CommandException("The sketch's view could not be made.");
            var vattrs = view.CloneAttrs();
            vattrs[OfKey] = of.DeepClone();
            if (stair != null) vattrs[ViewHiding.Key] = new JsonArray(stair.Id);
            var ids = new List<string>();
            if (stair != null)
            {
                // Its sketch in plan, or, for a stair of its type's shape, nothing to start from: the board opens empty over the stair's place.
                var catalog = ctx.Services as ModuleCatalog;
                if (catalog?.Query("architecture.stairSketch")?.Invoke(ctx.Query, new JsonObject { ["id"] = stair.Id }) is JsonObject sketch)
                {
                    if (sketch["flip"] is JsonValue fv && fv.TryGetValue(out bool flip) && flip) vattrs[FlipKey] = true;
                    foreach (var (key, role) in new[] { ("boundary", "boundary"), ("riser", "riser"), ("path", "path") })
                        foreach (var p in (sketch[key] as JsonArray ?? []).OfType<JsonArray>())
                        {
                            var n = p.Select(x => Json.Number(x) ?? 0).ToArray();
                            ids.Add(Drafts.Add(ctx, view.Id, "0", ShapeOf(new Vec2(n[0], n[1]), new Vec2(n[2], n[3]), n.Length > 4 ? n[4] : 0), role).Id);
                        }
                }
                else vattrs.Remove(ViewHiding.Key);
            }
            ctx.Replace(view.With(attrs: vattrs));
            return new JsonObject { ["viewId"] = view.Id, ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]) };
        });

        // {viewId, flip?} → {id}: the stair made or drawn again from the board, one undo step.
        r.Command("finishStairSketch", "Finish stair sketch", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
            var of = view.Attrs[OfKey] ?? throw new CommandException("That view is not a stair's sketch.");
            var drafts = Drafts.On(ctx.Model, view.Id).ToList();
            var args = Lines(drafts);
            var flip = a["flip"] is JsonValue fv && fv.TryGetValue(out bool f) ? f : view.Bool(FlipKey);
            if (flip) args["flip"] = true;
            string id;
            if (of is JsonObject fresh)
            {
                args["storeyId"] = (string?)fresh["storeyId"];
                if ((string?)fresh["typeId"] is { } tid && ctx.Model.Get(tid) != null) args["typeId"] = tid;
                id = (string)(Command(ctx, "architecture.addSketchStair").Handler(ctx, args) as JsonObject)!["id"]!;
            }
            else
            {
                id = (string?)of ?? "";
                if (ctx.Model.Get(id) == null) throw new CommandException("The stair whose sketch this is is gone.");
                args["id"] = id;
                Command(ctx, "architecture.updateStairSketch").Handler(ctx, args);
            }
            Drop(ctx, view, drafts);
            return new JsonObject { ["id"] = id };
        });

        // {viewId, flip}: the board's stair climbs the other way round (or back).
        r.Command("flipStairSketch", "Stair sketch climbs the other way", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
            if (view.Attrs[OfKey] == null) throw new CommandException("That view is not a stair's sketch.");
            var attrs = view.CloneAttrs();
            if (a["flip"] is JsonValue fv && fv.TryGetValue(out bool flip) ? flip : !view.Bool(FlipKey)) attrs[FlipKey] = true; else attrs.Remove(FlipKey);
            ctx.Replace(view.With(attrs: attrs));
            return null;
        });

        // {viewId}: the board dropped, the stair as it was.
        r.Command("cancelStairSketch", "Cancel stair sketch", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
            if (view.Attrs[OfKey] == null) throw new CommandException("That view is not a stair's sketch.");
            Drop(ctx, view, Drafts.On(ctx.Model, view.Id).ToList());
            return null;
        });

        // {viewId, flip?} → what the board's lines make so far (architecture.stairSketchCheck): the risers, how many the height asks, what is wrong.
        r.Query("stairSketchCheck", (q, a) =>
        {
            var view = Views.Find(q.Model, (string?)a["viewId"] ?? "");
            if (view?.Attrs[OfKey] is not { } of) return null;
            var args = Lines(Drafts.On(q.Model, view.Id));
            if (a["flip"] is JsonValue fv && fv.TryGetValue(out bool f) ? f : view.Bool(FlipKey)) args["flip"] = true;
            if (of is JsonObject fresh) { args["storeyId"] = (string?)fresh["storeyId"]; if ((string?)fresh["typeId"] is { } tid) args["typeId"] = tid; }
            else args["id"] = (string?)of;
            try { return (q.Services as ModuleCatalog)?.Query("architecture.stairSketchCheck")?.Invoke(q, args); }
            catch (CommandException e) { return new JsonObject { ["ok"] = false, ["error"] = e.Message }; }
        });
    }

    /// <summary>The board's lines by role, as the stair's commands take them.</summary>
    static JsonObject Lines(IEnumerable<Entity> drafts)
    {
        var o = new JsonObject { ["boundary"] = new JsonArray(), ["riser"] = new JsonArray(), ["path"] = new JsonArray() };
        foreach (var d in drafts)
        {
            var key = d.Str("role") switch { null or "boundary" => "boundary", "riser" => "riser", "path" => "path", _ => null };
            if (key == null || d.Attrs["hatch"] != null || Drafts.ShapeOf(d) is not { } shape) continue;
            foreach (var p in Pieces(shape)) ((JsonArray)o[key]!).Add((JsonNode)p);
        }
        return o;
    }

    static void Drop(ICommandContext ctx, Entity view, IEnumerable<Entity> drafts)
    {
        foreach (var d in drafts) ctx.Remove(d.Id);
        if (SketchConstraints.CarrierOf(ctx.Model, view.Id) is { } carrier) ctx.Remove(carrier.Id);
        ctx.Remove(view.Id);
    }
}
