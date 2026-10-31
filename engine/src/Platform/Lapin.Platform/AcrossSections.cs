using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A section across an element at a station (docs/plan-rebar.md, decision 210): every axis is a plane of section
/// without being drawn. A path element (a beam, a wall, a strip footing) is cut by the plane perpendicular to its
/// axis at <c>station</c> metres from its start, looked at along the axis (towards its end; <c>flip</c>: towards its
/// start); an element standing at a point (a column) by a horizontal plane <c>station</c> metres over its base, looked
/// at from above. The drawing is a window: the element's section with <c>margin</c> (0.5 m) of context each way,
/// sideways and up and down, seeing <c>depth</c> (the margin) behind the plane. The view keeps <c>across</c> (the element)
/// and follows it: the plane is read from the element as it is now.
/// </summary>
public static class AcrossSections
{
    public const string Key = "across";
    public const double DefaultMargin = 0.5;

    public static bool Is(Entity view) => view.Str("kind") == "section" && view.Str(Key) is { Length: > 0 };

    static bool IsPath(Entity e) => e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray;

    /// <summary>The station the section stands at, held within the element: along a path's length, or up a standing element's height.</summary>
    public static double Station(IModelView m, Entity e, Entity view)
    {
        var s = view.Num("station");
        if (IsPath(e)) return Math.Clamp(s, 0, PathCurve.Of(e).Length);
        var height = Spatial.StoreyOf(m, e.Id)?.Num("height", 3) ?? 3;
        return Math.Clamp(s, 0, e.NumOrNull("height") ?? height);
    }

    /// <summary>
    /// The section's line in plan for a path element: across the axis at the station, as wide as the element and its
    /// margins, drawn so that it looks along the axis (left of the line); null for a standing element (its cut is horizontal) or one that is gone.
    /// </summary>
    public static List<Vec2>? Line(IModelView m, IServices? services, Entity view)
    {
        if (!Is(view) || m.Get(view.Str(Key)!) is not { } e || !IsPath(e)) return null;
        var c = PathCurve.Of(e);
        if (c.Length < 1e-9) return null;
        var s = Station(m, e, view);
        var look = c.TangentAt(s);
        if (view.Bool("flip")) look = -look;
        var along = -look.Perp;
        var margin = view.NumOrNull("margin") ?? DefaultMargin;
        var frame = services?.Get<IPathElements>()?.FrameOf(e, m);
        var (y0, y1) = frame is { } f ? (f.YMin, f.YMax) : (-0.15, 0.15);
        // The frame's y grows to the left of the axis; along runs the other way when not flipped.
        var (lo, hi) = view.Bool("flip") ? (y0, y1) : (-y1, -y0);
        var p = c.PointAt(s);
        return [p + along * (lo - margin), p + along * (hi + margin)];
    }

    /// <summary>The view's spec: the plane across the element, the window round its section; null with the reason when the element is gone.</summary>
    public static (ViewSpec? Spec, string? Problem) Spec(IModelView m, Entity view, IReadOnlyList<DrawPart> parts, IServices? services, bool hidden)
    {
        if (m.Get(view.Str(Key) ?? "") is not { } e) return (null, "The element the section cuts across is gone.");
        var margin = view.NumOrNull("margin") ?? DefaultMargin;
        var depth = view.NumOrNull("depth") ?? margin;
        var own = parts.Where(p => p.Element == e.Id).SelectMany(p => p.Solid.Vertices).ToList();
        if (IsPath(e))
        {
            if (Line(m, services, view) is not { Count: 2 } line) return (null, "The element the section cuts across has no length.");
            var spec = ViewSpec.Section(line[0], line[1], depth, hidden);
            // The window: the element's own body about the plane, with the margin; without a body, the line's width and the storey's height.
            double x0, x1, z0, z1;
            if (own.Count > 0)
            {
                var xs = own.Select(v => spec.Frame.Map(v).X).ToList();
                (x0, x1) = (Math.Min(xs.Min(), margin), Math.Max(xs.Max(), (line[1] - line[0]).Length - margin));
                (z0, z1) = (own.Min(v => v.Z), own.Max(v => v.Z));
            }
            else
            {
                (x0, x1) = (margin, (line[1] - line[0]).Length - margin);
                var storey = Spatial.StoreyOf(m, e.Id);
                (z0, z1) = (storey?.Num("elevation") ?? 0, (storey?.Num("elevation") ?? 0) + (storey?.Num("height", 3) ?? 3));
            }
            return (spec with { Span = (x0 - margin, x1 + margin), Rise = (z0 - margin, z1 + margin) }, null);
        }
        // A standing element: a horizontal cut over its base, looked at from above, its x along its rotation.
        var baseZ = (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("baseOffset");
        var z = baseZ + Station(m, e, view);
        var rot = e.Num("rotation") * Math.PI / 180;
        var (cx, cy) = e.Attrs["at"] is JsonArray at && at.Count >= 2 ? (Json.Number(at[0]) ?? 0, Json.Number(at[1]) ?? 0)
            : own.Count > 0 ? ((own.Min(v => v.X) + own.Max(v => v.X)) / 2, (own.Min(v => v.Y) + own.Max(v => v.Y)) / 2) : (0.0, 0.0);
        var right = new Vec3(Math.Cos(rot), Math.Sin(rot), 0);
        var up = new Vec3(-Math.Sin(rot), Math.Cos(rot), 0);
        var frame = new ViewFrame(new Vec3(cx, cy, z), right, up, -Vec3.UnitZ);
        var pts = own.Count > 0 ? own.Select(frame.Map).ToList() : [new Vec2(-0.15, -0.15), new Vec2(0.15, 0.15)];
        var plane = new ViewSpec("section", frame, true, depth, Hidden: hidden)
        {
            Span = (pts.Min(p => p.X) - margin, pts.Max(p => p.X) + margin),
            Rise = (pts.Min(p => p.Y) - margin, pts.Max(p => p.Y) + margin),
        };
        return (plane, null);
    }

    /// <summary>
    /// The view's fields from a command's arguments: <c>across</c> (the element), <c>station</c> (default: the middle),
    /// <c>flip</c>, <c>margin</c>, <c>depth</c>; its line in plan for what reads a section as one. Returns the view's name.
    /// </summary>
    public static string Set(ICommandContext ctx, JsonObject attrs, JsonObject a)
    {
        var m = ctx.Model;
        var id = (string?)a[Key] ?? attrs.Str(Key) ?? throw new CommandException("A section across needs its element.");
        var e = m.Get(id) ?? throw new CommandException("There is no such element to cut across.");
        if (Spatial.StoreyOf(m, e.Id) == null && !IsPath(e)) throw new CommandException($"„{e.Name}” cannot be cut across: it has no axis and stands on no storey.");
        attrs[Key] = e.Id;
        var length = IsPath(e) ? PathCurve.Of(e).Length : e.NumOrNull("height") ?? Spatial.StoreyOf(m, e.Id)?.Num("height", 3) ?? 3;
        if (a.ContainsKey("station")) attrs["station"] = Math.Round(Math.Clamp(Json.Number(a["station"]) ?? length / 2, 0, length), 4);
        else if (attrs["station"] == null) attrs["station"] = Math.Round(length / 2, 4);
        if (a.ContainsKey("flip")) { if (a["flip"] is JsonValue fv && fv.TryGetValue<bool>(out var f) && f) attrs["flip"] = true; else attrs.Remove("flip"); }
        if (a.ContainsKey("margin"))
        {
            var margin = Json.Number(a["margin"]) ?? DefaultMargin;
            if (margin < 0 || margin > 20) throw new CommandException("The margin is between 0 and 20 m.");
            attrs["margin"] = margin;
        }
        if (a.ContainsKey("depth")) { if (Json.Number(a["depth"]) is { } d && d > 0) attrs["depth"] = d; else attrs.Remove("depth"); }
        var view = new Entity("", Views.Class, "", attrs);
        if (Line(m, ctx.Services, view) is { Count: 2 } line)
        {
            attrs["start"] = new JsonArray(Math.Round(line[0].X, 6), Math.Round(line[0].Y, 6));
            attrs["end"] = new JsonArray(Math.Round(line[1].X, 6), Math.Round(line[1].Y, 6));
            attrs["pts"] = SectionPaths.PointsJson(line);
        }
        else { attrs.Remove("start"); attrs.Remove("end"); attrs.Remove("pts"); }
        return (string?)a["name"] ?? $"{e.Name} · {attrs.Num("station").ToString("0.00", CultureInfo.InvariantCulture)} m";
    }

    /// <summary>A section across <paramref name="elementId"/> at <paramref name="station"/> (null: the middle), as a module makes one.</summary>
    public static Entity Create(ICommandContext ctx, string elementId, double? station, double? margin = null, bool flip = false, string? discipline = null, double scale = 20)
    {
        var attrs = new JsonObject { ["kind"] = "section", ["scale"] = scale };
        if (discipline is { Length: > 0 }) attrs["discipline"] = discipline;
        var a = new JsonObject { [Key] = elementId };
        if (station is { } s) a["station"] = s;
        if (margin is { } mg) a["margin"] = mg;
        if (flip) a["flip"] = true;
        var name = Set(ctx, attrs, a);
        var v = new Entity(ctx.NewId(), Views.Class, name, attrs);
        ctx.Add(v);
        return v;
    }

    static string? Str(this JsonObject o, string k) => o[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    static double Num(this JsonObject o, string k) => Json.Number(o[k]) ?? 0;
}
