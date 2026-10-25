using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// Where a dimension's end holds on:
/// <list type="bullet">
/// <item>"edge": an edge an element's cut draws: the point and the edge's
/// direction kept in the element's own frame (its axis, or its placement and
/// rotation), so the end follows a move, a turn, a thicker wall; elements
/// without a frame keep the point from their middle (the old way);</item>
/// <item>"grid": a grid axis (by its tag), how far along it;</item>
/// <item>"axis": a wall's (or beam's) centre line, how far along it.</item>
/// </list>
/// </summary>
public sealed record DimEndRef(string Kind, string Element, Vec2 Offset, Vec2 Dir, Vec2? Local = null, Vec2? LocalDir = null, string? Tag = null, double S = 0)
{
    static JsonArray J(Vec2 p) => [p.X, p.Y];
    static Vec2? V(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : null;

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["kind"] = Kind };
        switch (Kind)
        {
            case "grid": o["grid"] = Element; o["tag"] = Tag; o["s"] = S; break;
            case "axis": o["element"] = Element; o["s"] = S; break;
            default:
                o["element"] = Element; o["offset"] = J(Offset); o["dir"] = J(Dir);
                if (Local is { } l && LocalDir is { } ld) { o["local"] = J(l); o["localDir"] = J(ld); }
                break;
        }
        return o;
    }

    public static DimEndRef? From(JsonNode? n)
    {
        if (n is not JsonObject o) return null;
        switch ((string?)o["kind"] ?? "edge")
        {
            case "grid" when (string?)o["grid"] is { } g && (string?)o["tag"] is { } t: return new("grid", g, default, default, Tag: t, S: Json.Number(o["s"]) ?? 0);
            case "axis" when (string?)o["element"] is { } e: return new("axis", e, default, default, S: Json.Number(o["s"]) ?? 0);
            case "edge" when (string?)o["element"] is { Length: > 0 } e && V(o["offset"]) is { } off && V(o["dir"]) is { } d:
                return new("edge", e, off, d, V(o["local"]), V(o["localDir"]));
        }
        return null;
    }
}

/// <summary>
/// Dimensions on a view (the old Lapin's associative dimensions): aligned, or
/// linear along a direction (<c>dir</c>). Each end on an element, a grid axis
/// or a wall's centre line is found anew each time the view is drawn; an end
/// whose element is gone is an orphan, drawn red where it was, until freed. A
/// dimension in a string (<c>chain</c>, <c>chainIx</c>) drives its string:
/// typing its value moves what lies after it.
/// </summary>
public static class Dimensions
{
    /// <summary>How far (m) an end may be from an edge of its direction and still lie on it.</summary>
    const double Reach = 0.6;
    /// <summary>Directions this close (sine of the angle) are the same.</summary>
    const double Parallel = 0.01;

    static IEnumerable<(Vec2 A, Vec2 B)> Edges(ViewDrawing d, string element) =>
        d.Cuts.Where(p => p.Element == element).SelectMany(p => p.Loops.SelectMany(l => l.Select((q, i) => (q, l[(i + 1) % l.Length]))));

    /// <summary>The middle of what an element draws: its cut areas' box centre.</summary>
    static Vec2? Middle(ViewDrawing d, string element)
    {
        var pts = d.Cuts.Where(p => p.Element == element).SelectMany(p => p.Loops.SelectMany(l => l)).ToList();
        return pts.Count == 0 ? null : new Vec2((pts.Min(p => p.X) + pts.Max(p => p.X)) / 2, (pts.Min(p => p.Y) + pts.Max(p => p.Y)) / 2);
    }

    static (double Dist, Vec2 Foot) Foot(Vec2 p, Vec2 a, Vec2 b)
    {
        var d = b - a;
        var t = Math.Clamp(Vec2.Dot(p - a, d) / Math.Max(Vec2.Dot(d, d), 1e-18), 0, 1);
        var f = a + d * t;
        return ((p - f).Length, f);
    }

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;

    /// <summary>An element's frame in plan: its axis (start → end), or its placement and rotation; null when it has none.</summary>
    public static (Vec2 O, Vec2 U)? Frame(Entity? e)
    {
        if (e == null) return null;
        if (e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray)
        {
            var (a, b) = (P(e.Attrs["start"]), P(e.Attrs["end"]));
            return (b - a).Length < 1e-9 ? null : (a, (b - a).Normalized());
        }
        if (e.Attrs["at"] is JsonArray)
        {
            var r = e.Num("rotation");
            return (P(e.Attrs["at"]), new Vec2(Math.Cos(r), Math.Sin(r)));
        }
        return null;
    }

    static Vec2 ToLocal((Vec2 O, Vec2 U) f, Vec2 p) { var v = p - f.O; return new Vec2(Vec2.Dot(v, f.U), Vec2.Dot(v, f.U.Perp)); }
    static Vec2 FromLocal((Vec2 O, Vec2 U) f, Vec2 l) => f.O + f.U * l.X + f.U.Perp * l.Y;
    static Vec2 DirToLocal((Vec2 O, Vec2 U) f, Vec2 d) => new(Vec2.Dot(d, f.U), Vec2.Dot(d, f.U.Perp));
    static Vec2 DirFromLocal((Vec2 O, Vec2 U) f, Vec2 l) => f.U * l.X + f.U.Perp * l.Y;

    /// <summary>
    /// What an end placed at <paramref name="p"/> holds on: a cut edge within
    /// <paramref name="tolerance"/>, else (in a plan) a grid axis, else a wall's
    /// or beam's centre line; null: a free end.
    /// </summary>
    public static DimEndRef? Associate(IModelView m, string viewKind, ViewDrawing d, Vec2 p, double tolerance)
    {
        (DPolygon Poly, Vec2 A, Vec2 B, double Dist)? best = null;
        foreach (var poly in d.Cuts)
            foreach (var l in poly.Loops)
                for (var i = 0; i < l.Length; i++)
                {
                    var (a, b) = (l[i], l[(i + 1) % l.Length]);
                    if ((b - a).Length < 1e-6) continue;
                    var (dist, _) = Foot(p, a, b);
                    if (dist <= tolerance && (best == null || dist < best.Value.Dist)) best = (poly, a, b, dist);
                }
        if (best is { } hit && Middle(d, hit.Poly.Element) is { } mid)
        {
            var dir = (hit.B - hit.A).Normalized();
            if (viewKind == "plan" && Frame(m.Get(hit.Poly.Element)) is { } f)
                return new DimEndRef("edge", hit.Poly.Element, p - mid, dir, ToLocal(f, p), DirToLocal(f, dir));
            return new DimEndRef("edge", hit.Poly.Element, p - mid, dir);
        }
        if (viewKind != "plan") return null;
        foreach (var grid in m.OfClass(IfcClass.Grid))
            foreach (var ax in Grids.Axes(grid))
                if (Foot(p, ax.A, ax.B).Dist <= tolerance)
                    return new DimEndRef("grid", grid.Id, default, default, Tag: ax.Tag, S: Vec2.Dot(p - ax.A, ax.Dir));
        foreach (var e in m.Entities.Where(e => e.Class is IfcClass.Wall or IfcClass.Beam && e.Attrs["start"] is JsonArray))
            if (Frame(e) is { } f && Foot(p, P(e.Attrs["start"]), P(e.Attrs["end"])).Dist <= tolerance)
                return new DimEndRef("axis", e.Id, default, default, S: Vec2.Dot(p - f.O, f.U));
        return null;
    }

    /// <summary>The end now, or null when what it holds on is gone.</summary>
    public static Vec2? Resolve(IModelView m, string viewKind, ViewDrawing d, DimEndRef r)
    {
        switch (r.Kind)
        {
            case "grid":
            {
                if (m.Get(r.Element) is not { Class: IfcClass.Grid } grid || Grids.Axes(grid).FirstOrDefault(a => a.Tag == r.Tag) is not { } ax) return null;
                return ax.A + ax.Dir * r.S;
            }
            case "axis":
                return Frame(m.Get(r.Element)) is { } f ? f.O + f.U * r.S : null;
        }
        Vec2 q, dir;
        if (viewKind == "plan" && r.Local is { } local && r.LocalDir is { } ld && Frame(m.Get(r.Element)) is { } frame && Middle(d, r.Element) != null)
            (q, dir) = (FromLocal(frame, local), DirFromLocal(frame, ld).Normalized());
        else if (Middle(d, r.Element) is { } mid)
            (q, dir) = (mid + r.Offset, r.Dir);
        else return null;
        (double Dist, Vec2 P)? best = null;
        foreach (var (a, b) in Edges(d, r.Element))
        {
            var e = b - a;
            if (e.Length < 1e-9 || Math.Abs(Vec2.Cross(e.Normalized(), dir)) > Parallel) continue;
            // On the edge's line: the end slides along it, so a wall made longer keeps its end where it measured.
            var u = e.Normalized();
            var onLine = a + u * Vec2.Dot(q - a, u);
            var dist = (q - onLine).Length;
            if (dist <= Reach && (best == null || dist < best.Value.Dist)) best = (dist, onLine);
        }
        return best?.P;
    }

    /// <summary>A dimension's ends as drawn now, and whether any end lost what it held on.</summary>
    public static (Vec2 A, Vec2 B, bool Orphan) Ends(IModelView m, string viewKind, Entity dim, ViewDrawing d)
    {
        var (a, b) = (P(dim.Attrs["a"]), P(dim.Attrs["b"]));
        var orphan = false;
        if (DimEndRef.From(dim.Attrs["refA"]) is { } ra) { if (Resolve(m, viewKind, d, ra) is { } p) a = p; else orphan = true; }
        if (DimEndRef.From(dim.Attrs["refB"]) is { } rb) { if (Resolve(m, viewKind, d, rb) is { } p) b = p; else orphan = true; }
        return (a, b, orphan);
    }

    /// <summary>The direction a dimension measures along: its own <c>dir</c> (linear), else from end to end (aligned).</summary>
    public static Vec2? Along(Entity dim, Vec2 a, Vec2 b)
    {
        if (dim.Attrs["dir"] is JsonArray && P(dim.Attrs["dir"]) is var d && d.Length > 1e-9) return d.Normalized();
        return (b - a).Length < 1e-9 ? null : (b - a).Normalized();
    }

    /// <summary>What a dimension measures: the distance along its direction.</summary>
    public static double Value(Entity dim, Vec2 a, Vec2 b) => Along(dim, a, b) is { } u ? Math.Abs(Vec2.Dot(b - a, u)) : 0;

    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        foreach (var dim in c.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "DIMENSION" && x.Str("viewId") == c.View.Id && !DimensionKinds.IsKind(x)))
        {
            var (a, b, orphan) = Ends(c.Model, c.Kind, dim, c.Drawing);
            if (Along(dim, a, b) is not { } u) continue;
            var len = Math.Abs(Vec2.Dot(b - a, u));
            if (len < 1e-6) continue;
            var style = Standards.ResolveDim(c.Book, ParamsOf(dim));
            var text = Standards.ResolveText(c.Book, ParamsOf(dim));
            var color = orphan ? "#c0392b" : style.Color ?? "#000000";
            var n = u.Perp;
            var at = P(dim.Attrs["at"]);
            // Each end carried square to the dimension line through `at`.
            var (offA, offB) = (Vec2.Dot(at - a, n), Vec2.Dot(at - b, n));
            var (da, db) = (a + n * offA, b + n * offB);
            if (Vec2.Dot(db - da, u) < 0) u = -u;
            var mm = c.Mm;
            var mk = new ViewMark(dim.Id);
            mk.Line(da, db, "DIMENSION", Pens.Default.Hatch, color);
            foreach (var (p, q, off) in new[] { (a, da, offA), (b, db, offB) })
            {
                var side = off >= 0 ? 1 : -1;
                // Extension lines: a gap from the measured point, past the dimension line by the overshoot.
                if (Math.Abs(off) > style.ExtGap * mm) mk.Line(p + n * (side * style.ExtGap * mm), q + n * (side * style.ExtOver * mm), "DIMENSION", Pens.Default.Hatch, color);
                var s = style.ArrowSize * mm;
                switch (style.Arrow)
                {
                    case "tick": { var t = (u + n).Normalized() * (s / 2); mk.Line(q - t, q + t, "DIMENSION", Pens.Default.Seen, color); break; }
                    case "arrow":
                    {
                        var inward = q == da ? u : -u;
                        mk.Line(q, q + inward * s + n * (s * 0.3), "DIMENSION", Pens.Default.Hatch, color).Line(q, q + inward * s - n * (s * 0.3), "DIMENSION", Pens.Default.Hatch, color);
                        break;
                    }
                    case "dot": mk.Polyline(ViewMark.Circle(q, s / 4, 12), true, "DIMENSION", Pens.Default.Seen, color); break;
                }
            }
            // The value: in the style's unit (or the text written over it), reading left to right (bottom to top), above, on or below the line.
            var value = dim.Str("text") is { Length: > 0 } over ? over : Standards.FormatDimension(len, style.Unit, style.Precision);
            var angle = Math.Atan2(u.Y, u.X) * 180 / Math.PI;
            var readable = -Standards.ReadableDeg(-angle);
            var up = new Vec2(-Math.Sin(readable * Math.PI / 180), Math.Cos(readable * Math.PI / 180));
            var mid = (da + db) * 0.5;
            var (tat, baseline) = style.Placement switch
            {
                "centered" => (mid, "middle"),
                "below" => (mid - up * (style.TextGap * mm + text.Height * mm), "bottom"),
                _ => (mid + up * (style.TextGap * mm), "bottom"),
            };
            mk.Text(new PenText(tat, value, text.Height, readable, "middle", baseline, text.Bold, text.Font), color);
            mk.Snaps.AddRange([da, db]);
            yield return mk;
        }
    }

    /// <summary>The style params an annotation names (its styles and o.* overrides).</summary>
    public static JsonObject ParamsOf(Entity e)
    {
        var p = new JsonObject();
        foreach (var k in new[] { "textStyle", "dimStyle" }) if (e.Str(k) is { } v) p[k] = v;
        if (e.Attrs["overrides"] is JsonObject o) foreach (var (k, v) in o) p["o." + k] = v?.DeepClone();
        return p;
    }

    /// <summary>
    /// A dimension driven to <paramref name="value"/>: what its second end holds
    /// on moves along it (its first end's, when the second is free), and in a
    /// string so does everything after it, so the other dimensions keep theirs.
    /// </summary>
    public static void Drive(ICommandContext ctx, Entity dim, ViewDrawing d, string viewKind, double value)
    {
        var m = ctx.Model;
        var (a, b, orphan) = Ends(m, viewKind, dim, d);
        if (orphan) throw new CommandException("The dimension lost its element: free it first.");
        if (Along(dim, a, b) is not { } u) throw new CommandException("The dimension measures nothing.");
        var signed = Vec2.Dot(b - a, u);
        var delta = value - Math.Abs(signed);
        if (Math.Abs(delta) < 1e-9) return;
        var dir = u * Math.Sign(signed == 0 ? 1 : signed);
        var refA = DimEndRef.From(dim.Attrs["refA"]);
        var refB = DimEndRef.From(dim.Attrs["refB"]);
        DimEndRef? mover = refB;
        var push = dir * delta;
        if (mover == null) { mover = refA; push = dir * -delta; }
        if (mover == null) throw new CommandException("Neither end holds on an element: there is nothing to move.");
        // In a string: what the later dimensions' second ends hold on goes too.
        var movers = new List<DimEndRef> { mover };
        if (dim.Str("chain") is { } chain && mover == refB)
        {
            var ix = dim.Num("chainIx");
            foreach (var other in m.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "DIMENSION" && x.Str("chain") == chain && x.Num("chainIx") > ix))
                if (DimEndRef.From(other.Attrs["refB"]) is { } r) movers.Add(r);
        }
        var anchor = mover == refB ? refA : refB;
        var moved = new HashSet<string>();
        foreach (var r in movers)
        {
            if (anchor != null && anchor.Element == r.Element) throw new CommandException("Both ends hold on the same element: it cannot move against itself.");
            if (!moved.Add(r.Kind == "grid" ? $"{r.Element}:{r.Tag}" : r.Element)) continue;
            Move(ctx, r, push);
        }
    }

    static void Move(ICommandContext ctx, DimEndRef r, Vec2 by)
    {
        if (r.Kind == "grid")
        {
            var grid = ctx.Model.Get(r.Element)!;
            var attrs = grid.CloneAttrs();
            foreach (var ax in (attrs["axes"] as JsonArray)?.OfType<JsonObject>() ?? [])
                if ((string?)ax["tag"] == r.Tag) { ax["a"] = new JsonArray(P(ax["a"]).X + by.X, P(ax["a"]).Y + by.Y); ax["b"] = new JsonArray(P(ax["b"]).X + by.X, P(ax["b"]).Y + by.Y); }
            ctx.Replace(grid.With(attrs: attrs));
            return;
        }
        var e = ctx.Model.Get(r.Element) ?? throw new CommandException("The dimension's element is gone.");
        Placement.Transform(ctx, e, Placement2D.Move(by), new HashSet<string> { e.Id });
    }
}
