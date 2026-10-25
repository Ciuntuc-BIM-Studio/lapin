using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A view's line edits (the old Lapin's LW, LWTRIM, LWEXTEND, LWJOIN,
/// LWRESET): kept on the view (<c>linework</c>), each on an element's edge,
/// picked in the view's generated drawing.
/// </summary>
public static class ViewLinework
{
    public static List<LineEdit> Edits(Entity view) =>
        (view.Attrs["linework"] as JsonArray)?.Select(LineEdit.From).OfType<LineEdit>().ToList() ?? [];

    static Entity Real(ICommandContext ctx, string? id) =>
        ctx.Model.Get(id ?? "") is { Class: Views.Class } v ? v
        : Views.Find(ctx.Model, id ?? "") != null ? throw new CommandException("Line edits are kept on a plan view: make the storey's plan a view first.")
        : throw new CommandException("There is no such view.");

    static void Write(ICommandContext ctx, Entity view, IEnumerable<LineEdit> edits)
    {
        var attrs = view.CloneAttrs();
        var list = edits.ToList();
        if (list.Count > 0) attrs["linework"] = new JsonArray(list.Select(e => (JsonNode)e.ToJson()).ToArray()); else attrs.Remove("linework");
        ctx.Replace(view.With(attrs: attrs));
    }

    static Vec2 P(JsonNode? n, string what) => n is JsonArray a && a.Count >= 2 && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y
        ? new Vec2(x, y) : throw new CommandException($"The {what} is [x, y].");

    /// <summary>The view's drawing as generated (before its edits), to pick edges in.</summary>
    static ViewDrawing Generated(ICommandContext ctx, Entity view)
    {
        var (drawing, _, problem) = Views.Generate(ctx.Query, view);
        return drawing ?? throw new CommandException(problem ?? "The view cannot be drawn.");
    }

    /// <summary>The edge at a point of the drawing, within the tolerance (m; default 3 paper mm).</summary>
    static Edge Pick(ViewDrawing drawing, Entity view, Vec2 p, JsonObject a)
    {
        var tol = Json.Number(a["tolerance"]) ?? (view.NumOrNull("scale") ?? 100) * 0.003;
        return Linework.EdgeAt(drawing, p, tol) ?? throw new CommandException("There is no line of the model there.");
    }

    static Edge Pick(ICommandContext ctx, Entity view, Vec2 p, JsonObject a) => Pick(Generated(ctx, view), view, p, a);

    public static void Register(IModuleRegistry r)
    {
        // An edge hidden, shown, restyled ({pen, color, pattern}).
        r.Command("view.linework", "Linework", (ctx, a) =>
        {
            var view = Real(ctx, (string?)a["id"]);
            var action = (string?)a["action"] ?? "hide";
            if (action is not ("hide" or "show" or "style")) throw new CommandException("Linework hides, shows or restyles an edge.");
            var style = LineStyleSpec.From(a["style"]);
            if (action == "style" && (style == null || style.Empty)) throw new CommandException("A style needs a pen, a colour or a pattern.");
            if (style?.Pattern is { } pat && !LineStyles.Patterns.Contains(pat)) throw new CommandException($"„{pat}” is not a line pattern.");
            var edge = Pick(ctx, view, P(a["at"], "point"), a);
            Write(ctx, view, Linework.Add(Edits(view), new LineEdit(ctx.NewId(), edge.Element, edge.A, edge.B, action, style)));
            return new JsonObject { ["element"] = edge.Element };
        });

        // Two edges to a corner: JOIN brings both, TRIM cuts the second at the first (the side picked goes), EXTEND carries the second to the first.
        r.Command("view.lineworkCorner", "Linework corner", (ctx, a) =>
        {
            var view = Real(ctx, (string?)a["id"]);
            var mode = (string?)a["mode"] ?? "join";
            // One drawing for both picks.
            var drawing = Generated(ctx, view);
            var first = Pick(drawing, view, P(a["first"], "first point"), a);
            var second = Pick(drawing, view, P(a["second"], "second point"), a);
            var x = Linework.Crossing(first.A, first.B, second.A, second.B) ?? throw new CommandException("The two edges are parallel: they never meet.");
            var changes = mode switch
            {
                "join" => [.. Linework.ToCorner(first, x, "picked").Select(c => (first, c)), .. Linework.ToCorner(second, x, "picked").Select(c => (second, c))],
                "trim" => Linework.ToCorner(second, x, "other").Select(c => (second, c)).ToList(),
                "extend" => Linework.ToCorner(second, x, "picked").Select(c => (second, c)).ToList(),
                _ => throw new CommandException($"„{mode}” is not join, trim or extend."),
            };
            var edits = Edits(view);
            foreach (var (edge, (action, ea, eb)) in changes) edits = Linework.Add(edits, new LineEdit(ctx.NewId(), edge.Element, ea, eb, action));
            Write(ctx, view, edits);
            return new JsonObject { ["edits"] = changes.Count };
        });

        // The edits on an edge removed; all the view's (all); or those whose edge is gone (orphans).
        r.Command("view.lineworkReset", "Reset linework", (ctx, a) =>
        {
            var view = Real(ctx, (string?)a["id"]);
            var edits = Edits(view);
            List<LineEdit> keep;
            if (a["all"] is JsonValue av && av.TryGetValue<bool>(out var all) && all) keep = [];
            else if (a["orphans"] is JsonValue ov && ov.TryGetValue<bool>(out var orph) && orph)
            {
                var (drawing, _, _) = Views.Generate(ctx.Query, view);
                var lost = drawing == null ? [] : Linework.Apply(drawing, edits).Orphans.ToHashSet();
                keep = edits.Where(e => !lost.Contains(e.Id)).ToList();
            }
            else
            {
                var edge = Pick(ctx, view, P(a["at"], "point"), a);
                var u = (edge.B - edge.A).Normalized();
                bool On(Vec2 p) => Math.Abs(Vec2.Cross(p - edge.A, u)) < 1e-4;
                keep = edits.Where(e => !(e.Element == edge.Element && On(e.A) && On(e.B))).ToList();
            }
            Write(ctx, view, keep);
            return new JsonObject { ["removed"] = edits.Count - keep.Count };
        });
    }
}
