using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A window type's outline of its own, drawn on the shared sketch canvas (audit S11 stage 2, docs/plan-sketch-canvas.md §4): the
/// configurator's grid stays the default way; what the grid and the heads cannot make (an odd pane, a shape of the user's) is drawn
/// here. The board is a paper view of its own (<see cref="PaperViews.IsBoard"/>) in the type's elevation, in metres, x across from
/// its middle, y up from its bottom, seen from outside; its guides are the type's outline as it is and its frame's inner line.
///
/// <para><c>architecture.editJoineryOutline {typeId}</c> opens it with the outline as one closed polyline (opened again, the same
/// board). <c>architecture.finishJoineryOutline {viewId}</c> takes one closed outline from what is drawn there (a closed polyline, a
/// circle, an ellipse, or lines, arcs and polylines meeting end to end; construction lines left out), and writes the type in one
/// undo step: its width and height the outline's, its shape <c>outline</c> with the loop in the unit box (<see cref="JoineryShape.Profile"/>,
/// so a resize stretches it), a frame following it all round and one fixed light inside (<see cref="JoineryShapes.Ringed"/>); every
/// door or window of the type follows; a rectangle drawn back gives the type its rectangle again. A door is refused (its outline is
/// a rectangle or a head). <c>architecture.cancelJoineryOutline</c> drops the board.</para>
/// </summary>
public static class JoineryOutlineBoard
{
    /// <summary>The attribute marking the board: {typeId}.</summary>
    public const string Key = "joineryOutlineOf";

    const string Draft = "DRAFT";

    static (Entity Type, JoinerySpec Spec) TypeOf(IModelView m, string? id)
    {
        var type = m.Get(id ?? "") is { Class: IfcClass.DoorType or IfcClass.WindowType } t ? t : throw new CommandException("There is no such door or window type.");
        return (type, JoinerySpec.From(type.Attrs["spec"]) ?? throw new CommandException($"„{type.Name}” has no configuration to draw the outline of."));
    }

    static JsonArray Pts(IEnumerable<Vec2> pts) => new([.. pts.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 9), Math.Round(p.Y, 9)))]);

    static JsonObject Guide(IEnumerable<Vec2> pts, bool closed, string color, bool dashed) =>
        new() { ["pts"] = Pts(pts), ["closed"] = closed, ["color"] = color, ["dash"] = dashed ? new JsonArray(2, 1.5) : null };

    /// <summary>{typeId} → {viewId}: the board, one undo step.</summary>
    public static JsonNode? Edit(ICommandContext ctx, JsonObject a)
    {
        var (type, spec) = TypeOf(ctx.Model, (string?)a["typeId"]);
        if (spec.IsDoor) throw new CommandException("A door's outline is a rectangle or one of the heads (the form's Shape): an outline of its own is a window's.");
        if (ctx.Model.OfClass(Views.Class).FirstOrDefault(v => (string?)v.Attrs[Key]?["typeId"] == type.Id) is { } open) return new JsonObject { ["viewId"] = open.Id };
        var outline = JoineryShapes.Outline(spec, 0);
        var guides = new JsonArray(
            (JsonNode)Guide(outline, true, "#6b9bd1", true),
            (JsonNode)Guide(JoineryShapes.Outline(spec, spec.Frame.Width), true, "#9aa3ad", true),
            (JsonNode)Guide([new Vec2(-spec.Width, 0), new Vec2(spec.Width, 0)], false, "#9aa3ad", false));
        var attrs = new JsonObject
        {
            ["kind"] = PaperViews.Kind, ["scale"] = 20, [Key] = new JsonObject { ["typeId"] = type.Id }, [PaperViews.UnderlayKey] = guides,
        };
        var view = new Entity(ctx.NewId(), Views.Class, $"Outline: {type.Name}", attrs);
        ctx.Add(view);
        ctx.Add(new Entity(ctx.NewId(), IfcClass.Annotation, "Polygon 1", new JsonObject
        {
            ["viewId"] = view.Id, ["layer"] = "0", ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = Pts(outline), ["closed"] = true },
        }, Draft));
        return new JsonObject { ["viewId"] = view.Id };
    }

    static (Entity View, Entity Type, JoinerySpec Spec) Board(IModelView m, JsonObject a)
    {
        var view = Views.Find(m, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
        if (view.Attrs[Key] is not JsonObject of) throw new CommandException("That view is not a door or window outline's board.");
        var (type, spec) = TypeOf(m, (string?)of["typeId"]);
        return (view, type, spec);
    }

    static IEnumerable<Entity> On(IModelView m, string viewId) => m.OfClass(IfcClass.Annotation).Where(e => e.Str("viewId") == viewId);

    static double Norm(double a) { var t = a % (2 * Math.PI); return t < 0 ? t + 2 * Math.PI : t; }

    /// <summary>How far an arc runs anticlockwise from one angle to the other (a whole turn when they are the same).</summary>
    static double Sweep(double from, double to) { var s = Norm(to - from); return s < 1e-12 ? 2 * Math.PI : s; }

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;

    /// <summary>A drafted shape as its run of points (curves by the one rule), and whether it closes on itself; null for what is not a shape.</summary>
    static (List<Vec2> Pts, bool Closed)? Run(JsonObject? s)
    {
        if (s == null) return null;
        switch ((string?)s["type"])
        {
            case "line": return ([P(s["a"]), P(s["b"])], false);
            case "polyline" when s["pts"] is JsonArray pts && pts.Count >= 2:
                return ([.. pts.Select(P)], s["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c);
            case "circle" when Json.Number(s["r"]) is > 0 and var r:
            {
                var (cc, n) = (P(s["c"]), Chords.Count(r, 2 * Math.PI, 16));
                return ([.. Enumerable.Range(0, n).Select(i => cc + new Vec2(Math.Cos(2 * Math.PI * i / n), Math.Sin(2 * Math.PI * i / n)) * r)], true);
            }
            case "arc" when Json.Number(s["r"]) is > 0 and var r:
            {
                var (cc, a0) = (P(s["c"]), Json.Number(s["start"]) ?? 0);
                var sweep = Sweep(a0, Json.Number(s["end"]) ?? 0);
                var n = Chords.Count(r, sweep, 2);
                return ([.. Enumerable.Range(0, n + 1).Select(i => cc + new Vec2(Math.Cos(a0 + sweep * i / n), Math.Sin(a0 + sweep * i / n)) * r)], false);
            }
            case "ellipse" when Json.Number(s["ratio"]) is > 0 and var q:
            {
                var (st, en) = (Json.Number(s["start"]) ?? 0, Json.Number(s["end"]) ?? 0);
                var whole = Math.Abs(Norm(en - st)) < 1e-12;
                var pts = Chords.EllipseArc(P(s["c"]), P(s["m"]), q, whole ? 0 : st, whole ? 2 * Math.PI : Sweep(st, en), whole ? 4 : 2);
                if (whole) pts.RemoveAt(pts.Count - 1);
                return (pts, whole);
            }
            default: return null;
        }
    }

    /// <summary>The one closed outline the board's drafted shapes make (pieces meeting end to end within a tenth of a millimetre), counter-clockwise.</summary>
    public static List<Vec2> LoopOf(IModelView m, string viewId)
    {
        var runs = new List<List<Vec2>>();
        foreach (var e in On(m, viewId))
        {
            if (e.PredefinedType != Draft) throw new CommandException($"The outline is drawn with lines, arcs, polylines, circles or ellipses: „{e.Name}” is none of them. Delete it, then Finish.");
            if (e.Str("role") == "draft" || e.Attrs["hatch"] != null) continue;
            var r = Run(e.Attrs["shape"] as JsonObject) ?? throw new CommandException($"„{e.Name}” is not a shape the outline can take.");
            if (r.Closed)
            {
                if (runs.Count > 0 || On(m, viewId).Count(x => x.PredefinedType == Draft && x.Str("role") != "draft" && x.Attrs["hatch"] == null) > 1)
                    throw new CommandException("The outline is one closed shape: delete the others (construction lines may stay), then Finish.");
                return Ccw(r.Pts);
            }
            runs.Add(r.Pts);
        }
        if (runs.Count == 0) throw new CommandException("Draw the outline: one closed shape (a polyline, a circle, an ellipse, or lines and arcs end to end), then Finish.");
        const double tol = 1e-4;
        var loop = new List<Vec2>(runs[0]);
        runs.RemoveAt(0);
        while (runs.Count > 0)
        {
            var end = loop[^1];
            var i = runs.FindIndex(r => (r[0] - end).Length < tol || (r[^1] - end).Length < tol);
            if (i < 0) throw new CommandException("The lines do not meet end to end in one closed outline: join their ends (snap to them), then Finish.");
            var next = runs[i];
            runs.RemoveAt(i);
            if ((next[0] - end).Length >= tol) next = Enumerable.Reverse(next).ToList();
            loop.AddRange(next.Skip(1));
        }
        if ((loop[0] - loop[^1]).Length >= tol) throw new CommandException("The outline does not close: join its last end to its first, then Finish.");
        loop.RemoveAt(loop.Count - 1);
        return Ccw(loop);
    }

    static List<Vec2> Ccw(List<Vec2> pts)
    {
        var o = JoineryShapes.Clean(pts);
        if (o.Count < 3 || Math.Abs(Polygon2.SignedArea(o)) < 1e-6) throw new CommandException("The outline encloses nothing: draw a closed shape.");
        if (Polygon2.SignedArea(o) < 0) o.Reverse();
        return o;
    }

    /// <summary>{viewId} → {typeId, width, height, shape}: the type written with the outline, one undo step; the board gone.</summary>
    public static JsonNode? Finish(ICommandContext ctx, JsonObject a)
    {
        var (view, type, spec) = Board(ctx.Model, a);
        var loop = LoopOf(ctx.Model, view.Id);
        var (x0, x1, y0, y1) = (loop.Min(p => p.X), loop.Max(p => p.X), loop.Min(p => p.Y), loop.Max(p => p.Y));
        var (w, h) = (Math.Round(x1 - x0, 4), Math.Round(y1 - y0, 4));
        if (w < 0.1 || h < 0.1) throw new CommandException("A window is 10 cm across and high at least.");
        var mid = (x0 + x1) / 2;
        // A rectangle drawn: the type's rectangle again; else its outline in the unit box (a resize stretches it).
        var rect = loop.Count == 4 && loop.All(p => (Math.Abs(p.X - x0) < 1e-6 || Math.Abs(p.X - x1) < 1e-6) && (Math.Abs(p.Y - y0) < 1e-6 || Math.Abs(p.Y - y1) < 1e-6));
        var shape = rect ? null : new JoineryShape("outline", Profile: [.. loop.Select(p => new Vec2(Math.Round((p.X - mid) / (x1 - x0), 6), Math.Round((p.Y - y0) / (y1 - y0), 6)))]);
        var next = spec with { Width = w, Height = h, Shape = shape };
        Openings.UpdateType(ctx, type, next);
        foreach (var e in On(ctx.Model, view.Id).ToList()) ctx.Remove(e.Id);
        ctx.Remove(view.Id);
        return new JsonObject { ["typeId"] = type.Id, ["width"] = w, ["height"] = h, ["shape"] = rect ? "rect" : "outline" };
    }

    public static void Register(IModuleRegistry r)
    {
        r.Command("architecture.editJoineryOutline", "Edit outline", Edit);
        r.Command("architecture.finishJoineryOutline", "Finish outline", Finish);
        r.Command("architecture.cancelJoineryOutline", "Cancel outline", (ctx, a) =>
        {
            var (view, _, _) = Board(ctx.Model, a);
            foreach (var e in On(ctx.Model, view.Id).ToList()) ctx.Remove(e.Id);
            ctx.Remove(view.Id);
            return null;
        });
    }
}
