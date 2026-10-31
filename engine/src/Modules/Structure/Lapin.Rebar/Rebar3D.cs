using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// A bar drawn on a view stands in space (decision 216): on the view's plane, and, laid <c>across</c> the drawing,
/// so many times along the view's direction — by the cover inside the element it was drawn in (its <c>host</c>: a
/// beam's width between the cover, the stirrup and half a bar off each face, the first and the last at the faces,
/// the rest spread evenly), or at a step about the plane. Every other view draws it where it is: a dot of its diameter
/// where it runs through the view's plane (the cross-section), a line along it (a structural plan). The schedule
/// counts every copy.
/// </summary>
public static class Rebar3D
{
    /// <summary>How many times a bar is laid across its drawing's plane, and how: by the cover in its host, or at a step about the plane.</summary>
    public sealed record Across(int Count, string Mode, double Step)
    {
        public JsonObject ToJson() => new() { ["count"] = Count, ["mode"] = Mode, ["step"] = Math.Round(Step, 6) };
        public static Across? Of(Entity e) => e.Attrs["across"] is JsonObject o
            ? new Across((int)Math.Max(1, Json.Number(o["count"]) ?? 1), (string?)o["mode"] is "step" ? "step" : "cover", Json.Number(o["step"]) ?? 0.05) : null;
    }

    /// <summary>The even spread of <paramref name="n"/> places between <paramref name="lo"/> and <paramref name="hi"/> (the ends included); one in the middle; none for 0.</summary>
    public static List<double> Spread(int n, double lo, double hi) => n <= 0 ? [] : n == 1 ? [(lo + hi) / 2] : Enumerable.Range(0, n).Select(i => lo + (hi - lo) * i / (n - 1)).ToList();

    static readonly HashSet<string> Hosts = [IfcClass.Beam, IfcClass.Column, IfcClass.Wall, IfcClass.Footing, IfcClass.Slab];

    /// <summary>
    /// The element the point lies in on the view, to be the bar's host: the one whose cut holds it (a beam in a section
    /// through it), else the nearest one seen there whose body, projected, holds it (a beam seen from the side, the smallest first).
    /// </summary>
    public static string? HostAt(ICommandContext ctx, Entity view, Vec2 at)
    {
        var (drawing, spec, _) = Views.Generate(ctx.Query, view);
        if (drawing == null || spec == null) return null;
        foreach (var cut in drawing.Cuts)
            if (cut.Loops.Count > 0 && Polygon2.Inside(cut.Loops.Select(l => l.ToArray()).ToArray(), at) && ctx.Model.Get(cut.Element) is { } ce && Hosts.Contains(ce.Class))
                return cut.Element;
        string? best = null;
        var bestArea = double.PositiveInfinity;
        foreach (var e in ctx.Model.Entities)
        {
            if (!Hosts.Contains(e.Class) || ctx.Query.Geometry(e.Id) is not { Parts.Count: > 0 } g) continue;
            var pts = g.Parts.SelectMany(p => p.Solid.Vertices).ToList();
            if (pts.Count == 0 || pts.Min(spec.Frame.Depth) < -1e-6) continue;
            var (x0, x1, y0, y1) = (pts.Min(p => spec.Frame.Map(p).X), pts.Max(p => spec.Frame.Map(p).X), pts.Min(p => spec.Frame.Map(p).Y), pts.Max(p => spec.Frame.Map(p).Y));
            if (at.X < x0 || at.X > x1 || at.Y < y0 || at.Y > y1) continue;
            var area = (x1 - x0) * (y1 - y0);
            if (area < bestArea) { bestArea = area; best = e.Id; }
        }
        return best;
    }

    /// <summary>The frame of the view a bar is drawn on, so its drawing stands in space; null when the view cannot give one (an elevation, a view gone).</summary>
    public static ViewFrame? FrameOf(IModelView m, IServices? services, Entity? view) =>
        view == null || view.Str("kind") is "elevation" or "3d" ? null : Views.Spec(m, view, [], services).Spec?.Frame;

    /// <summary>
    /// The depths (along the view's direction, from its plane) a bar is laid at: by the cover across its host's width as the
    /// view sees it (the host's body between its nearest and farthest point), or at its step about the plane; the plane alone otherwise.
    /// </summary>
    public static List<double> Depths(IModelView m, Func<string, ElementGeometry?>? geometry, Entity e, ViewFrame frame, RebarSettings st)
    {
        if (Across.Of(e) is not { } across) return [0];
        if (across.Mode == "cover" && e.Str("host") is { } hostId && m.Get(hostId) is { } host && geometry?.Invoke(hostId) is { Parts.Count: > 0 } g)
        {
            var depths = g.Parts.SelectMany(p => p.Solid.Vertices).Select(frame.Depth).ToList();
            var stirrups = TypeStirrups.SpecOf(m, host);
            var d = e.NumOrNull("diameter") ?? 0.012;
            var inset = (stirrups?.Cover ?? st.Cover) + (stirrups is { Enabled: true } s ? s.Diameter : 0) + d / 2;
            var (lo, hi) = (depths.Min() + inset, depths.Max() - inset);
            if (hi >= lo) return Spread(across.Count, lo, hi);
        }
        // At a step, centred on the plane.
        return Enumerable.Range(0, across.Count).Select(k => (k - (across.Count - 1) / 2.0) * across.Step).ToList();
    }

    /// <summary>The bar's copies in space: its drawn chain on its view's plane, each at its depth and in-plane array place.</summary>
    public static List<Vec3[]> Bars(IModelView m, IServices? services, Func<string, ElementGeometry?>? geometry, Entity e, RebarSettings st)
    {
        var view = Views.Find(m, e.Str("viewId") ?? "");
        if (FrameOf(m, services, view) is not { } frame) return [];
        var s = Rebars.SpecOf(e);
        var at = Rebars.At(e);
        var chain = s.Shape == "dot" ? [at] : RebarShapes.Chain(RebarShapes.Segments(s).Select(x => RebarShapes.Place(s, at, x)).ToList());
        if (chain.Count == 0) return [];
        var (nx, dx, ny, dy) = Rebars.Array(e);
        var o = new List<Vec3[]>();
        foreach (var depth in Depths(m, geometry, e, frame, st))
            for (var ix = 0; ix < nx; ix++)
                for (var iy = 0; iy < ny; iy++)
                {
                    var off = new Vec2(ix * dx, iy * dy);
                    o.Add(chain.Select(p => frame.Origin + frame.Right * (p.X + off.X) + frame.Up * (p.Y + off.Y) + frame.Dir * depth).ToArray());
                }
        return o;
    }

    /// <summary>Whether a view shows bars in space at all (sections; plans and elevations only the structure's), how deep it sees, and whether it is a plan (no depth: its storey's).</summary>
    public static bool Shows(ViewContext c, out double limit, out bool plan)
    {
        var kind = c.View.Str("kind") ?? "plan";
        plan = kind == "plan";
        limit = kind == "section" ? c.View.NumOrNull("depth") ?? (AcrossSections.Is(c.View) ? c.View.NumOrNull("margin") ?? AcrossSections.DefaultMargin : double.PositiveInfinity) : double.PositiveInfinity;
        if (kind is "plan" or "elevation" && !string.Equals(c.View.Str("discipline"), "Structure", StringComparison.OrdinalIgnoreCase)) return false;
        return kind is "plan" or "elevation" or "section";
    }

    /// <summary>
    /// Bars in space as a view sees them: each segment within the view's depth drawn along; the point where a segment
    /// pierces the view's plane a dot; a segment that comes to a point (seen end on) a dot too.
    /// </summary>
    public static (List<Vec2> Dots, List<(Vec2 A, Vec2 B)> Lines) Project(ViewContext c, IEnumerable<Vec3[]> bars, double limit, bool plan)
    {
        var dots = new List<Vec2>();
        var lines = new List<(Vec2 A, Vec2 B)>();
        foreach (var bar in bars)
            for (var i = 0; i + 1 < bar.Length; i++)
            {
                var (p, q) = (bar[i], bar[i + 1]);
                var (dp, dq) = (c.Frame.Depth(p), c.Frame.Depth(q));
                if (!plan && (dp < -1e-9) != (dq < -1e-9) && Math.Abs(dq - dp) > 1e-12)
                {
                    var t = dp / (dp - dq);
                    dots.Add(c.Map(p + (q - p) * t));
                    continue;
                }
                if (!plan && (dp < -1e-9 || dp > limit + 1e-9) && (dq < -1e-9 || dq > limit + 1e-9)) continue;
                var (a, b) = (c.Map(p), c.Map(q));
                if ((b - a).Length < 1e-6) dots.Add(a); else lines.Add((a, b));
            }
        return (dots, lines);
    }

    /// <summary>The dots and lines of one group of bars drawn, with its label beside the dots (to the right) or over the lines, and the mark's balloon.</summary>
    public static void Draw(ViewMark mk, ViewContext c, RebarSettings st, List<Vec2> dots, List<(Vec2 A, Vec2 B)> lines, double d, string layer, string label, int mark)
    {
        var color = st.ColorByDiameter ? RebarShapes.ColorOf(d) : "#000000";
        var width = Math.Max(0.25, d * 1000 / c.Scale);
        foreach (var p in dots)
        {
            var circle = ViewMark.Circle(p, Math.Max(d / 2, 0.6 * c.Mm), 24);
            mk.Fills.Add(([circle], color));
            mk.Polyline(circle, true, layer, 0.18, color);
        }
        foreach (var (a, b) in lines) mk.Line(a, b, layer, width, color);
        mk.Snaps.AddRange(dots);
        var r = st.Balloon / 2 * c.Mm;
        Vec2 lab, target;
        if (dots.Count > 0)
        {
            var right = dots.MaxBy(p => p.X);
            lab = right + new Vec2(10 * c.Mm, 0);
            target = right;
        }
        else
        {
            var all = lines.SelectMany(l => new[] { l.A, l.B }).ToList();
            var mid = new Vec2((all.Min(p => p.X) + all.Max(p => p.X)) / 2, all.Max(p => p.Y));
            lab = mid + new Vec2(0, 8 * c.Mm);
            target = lines.Select(l => RebarModule.Nearest([l.A, l.B], lab, l.A)).MinBy(p => (p - lab).Length);
        }
        mk.Polyline(ViewMark.Circle(lab, r, 32), true, RebarShapes.AnnotationLayer, 0.35, color);
        if (mark > 0) mk.Text(new PenText(lab, mark.ToString(CultureInfo.InvariantCulture), st.TextHeight, 0, "middle", "middle", true), color);
        if ((target - lab).Length > r) mk.Line(lab + (target - lab).Normalized() * r, target, RebarShapes.AnnotationLayer, 0.25, color);
        mk.Text(new PenText(lab + new Vec2(r + 1.0 * c.Mm, 0), label, st.TextHeight, 0, "start", "middle"), color);
    }

    /// <summary>
    /// The bars drawn on other views, as this view sees them (<see cref="Project"/>), each with its label and mark; a bar's own view draws it itself.
    /// </summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c, RebarSettings st)
    {
        var m = c.Model;
        if (!Shows(c, out var limit, out var plan)) yield break;
        var hidden = (c.View.Attrs["hiddenLayers"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        foreach (var e in m.OfClass(Rebars.Class))
        {
            if (e.Str("viewId") == c.View.Id || e.Bool("excludeSchedule")) continue;
            var s = Rebars.SpecOf(e);
            var layer = e.Str("layer") ?? RebarShapes.Def(s.Shape).Layer;
            if (hidden.Contains(layer)) continue;
            var bars = Bars(m, c.Services, c.Geometry, e, st);
            if (bars.Count == 0) continue;
            var (dots, lines) = Project(c, bars, limit, plan);
            if (dots.Count == 0 && lines.Count == 0) continue;
            var mk = new ViewMark(e.Id);
            Draw(mk, c, st, dots, lines, s.Diameter, layer, Rebars.Label(e, s), Rebars.Mark(e));
            yield return mk;
        }
    }
}
