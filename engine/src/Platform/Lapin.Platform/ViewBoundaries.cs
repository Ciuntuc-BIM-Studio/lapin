using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A view's named boundary (decision 236): a rectangle drawn on a plan, section or elevation (<c>LapinViewBoundary</c> on its
/// view by <c>viewId</c>, corners <c>min</c> and <c>max</c> in the view's coordinates) with a name unique in the project,
/// so a sheet's viewport can show exactly that part of the view instead of being cropped blind on the sheet. Drawn dashed with
/// its name on the screen only, never printed; a viewport bound to it takes its box and middle from it, follows it when it
/// is moved or resized in the view, and cropped on the sheet resizes it. One view may go on several sheets, a boundary each.
/// </summary>
public static class ViewBoundaries
{
    public const string Class = "LapinViewBoundary";
    public const string Color = "#b4502a";

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6));

    /// <summary>A boundary's rectangle in its view's coordinates.</summary>
    public static (Vec2 Min, Vec2 Max) Box(Entity b)
    {
        var (a, c) = (P(b.Attrs["min"]), P(b.Attrs["max"]));
        return (new Vec2(Math.Min(a.X, c.X), Math.Min(a.Y, c.Y)), new Vec2(Math.Max(a.X, c.X), Math.Max(a.Y, c.Y)));
    }

    public static IEnumerable<Entity> Of(IModelView m, string viewId) => m.OfClass(Class).Where(b => b.Str("viewId") == viewId);

    static string NextName(IModelView m)
    {
        var used = m.OfClass(Class).Select(b => b.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; ; i++) if (!used.Contains($"Boundary {i}")) return $"Boundary {i}";
    }

    static string CheckName(IModelView m, string name, string? self)
    {
        name = name.Trim();
        if (name.Length == 0) throw new CommandException("A view boundary needs a name.");
        if (m.OfClass(Class).Any(b => b.Id != self && string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new CommandException($"There is a view boundary named „{name}” already.");
        return name;
    }

    static (Vec2 Min, Vec2 Max) CheckBox(Vec2 a, Vec2 b)
    {
        var (min, max) = (new Vec2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Vec2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
        if (max.X - min.X < 0.05 || max.Y - min.Y < 0.05) throw new CommandException("A view boundary is 5 cm across at least.");
        return (min, max);
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "View boundary", [], Color, Kind: "annotation"));

        // A named boundary on a view: {viewId, a: [x, y], b: [x, y], name?} (a name of its own unless given).
        r.Command("view.addBoundary", "View boundary", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("A view boundary is drawn on a view.");
            if (view.Str("kind") is "3d") throw new CommandException("A view boundary is drawn on a plan, a section or an elevation.");
            var (min, max) = CheckBox(Args.Point(a["a"], "a corner"), Args.Point(a["b"], "the opposite corner"));
            var name = (string?)a["name"] is { Length: > 0 } n ? CheckName(ctx.Model, n, null) : NextName(ctx.Model);
            var e = new Entity(ctx.NewId(), Class, name, new JsonObject { ["viewId"] = view.Id, ["min"] = J(min), ["max"] = J(max) });
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id, ["name"] = name };
        });

        // Its corners or its name: {id, min?, max?, name?}.
        r.Command("view.updateBoundary", "View boundary", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("Not a view boundary.");
            var (min, max) = Box(e);
            if (a.ContainsKey("min") || a.ContainsKey("max"))
                (min, max) = CheckBox(a.ContainsKey("min") ? Args.Point(a["min"], "a corner") : min, a.ContainsKey("max") ? Args.Point(a["max"], "a corner") : max);
            var attrs = e.CloneAttrs();
            attrs["min"] = J(min);
            attrs["max"] = J(max);
            var name = (string?)a["name"] is { } nm ? CheckName(ctx.Model, nm, e.Id) : e.Name;
            ctx.Replace(e.With(name: name, attrs: attrs));
            return null;
        });
    }

    /// <summary>The view's boundaries, on the screen only: a dashed rectangle with its name above its upper-left corner, picked as the boundary.</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        var k = c.Scale / 100;
        var text = Standards.ResolveText(c.Book, []);
        foreach (var b in Of(c.Model, c.View.Id))
        {
            var (min, max) = Box(b);
            var mk = new ViewMark(b.Id);
            Vec2[] corners = [min, new(max.X, min.Y), max, new(min.X, max.Y)];
            for (var i = 0; i < 4; i++) mk.Line(corners[i], corners[(i + 1) % 4], Views.ConstructionLayer, 0.35, Color, LineStyles.DashOf("dashed"));
            mk.Text(new PenText(new Vec2(min.X, max.Y + 0.25 * k), b.Name, text.Height, Anchor: "start", Font: text.Font), Color);
            mk.Snaps.AddRange(corners);
            yield return mk;
        }
    }
}
