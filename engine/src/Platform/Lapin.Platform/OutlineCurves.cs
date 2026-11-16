using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The exact curves of drawn outlines (an element's <c>contour</c> with its <c>arcs</c> and <c>ellipses</c>: floors, ceilings,
/// drawn rooms, balconies, platforms), for DXF to write a plan's chords along them as ARC and ELLIPSE (<see cref="PenCurve"/>).
/// The drawing itself is unchanged: its chords are laid by the one rule, and lie on these curves.
/// </summary>
public static class OutlineCurves
{
    /// <summary>An outline's curved edges, in its own (plan) coordinates.</summary>
    public static IEnumerable<PenCurve> Of(Entity e)
    {
        if (e.Attrs["contour"] == null) return [];
        var loops = new List<PenCurve>();
        if (e.Attrs[ArcOutline.Key] != null || e.Attrs[EllipseEdge.Key] != null) loops.AddRange(Of(ArcOutline.CornersOf(e), ArcOutline.RisesOf(e), EllipseEdge.Of(e)));
        // Its own holes' curved edges too.
        if (e.Attrs[OutlineHoles.ArcsKey] != null || e.Attrs[OutlineHoles.EllipsesKey] != null)
            foreach (var h in OutlineHoles.Of(e)) loops.AddRange(Of(h.Corners, h.Rises, h.Ellipses));
        return loops;
    }

    /// <summary>A loop's curved edges: its corners, their rises, its elliptical edges.</summary>
    public static IEnumerable<PenCurve> Of(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, IReadOnlyList<EllipseEdge?>? ellipses)
    {
        if (corners.Count < 3) yield break;
        for (var i = 0; i < corners.Count; i++)
        {
            var (a, b) = (corners[i], corners[(i + 1) % corners.Count]);
            if (ellipses != null && i < ellipses.Count && ellipses[i] is { } el)
            {
                var (t0, s) = el.Span(a, b);
                yield return s >= 0 ? new PenCurve(el.C, el.M, el.N, t0, s) : new PenCurve(el.C, el.M, el.N, t0 + s, -s);
                continue;
            }
            var c = ArcOutline.Edge(corners, rises, i);
            if (!c.IsArc) continue;
            yield return Arc(c);
        }
    }

    /// <summary>An arc edge as an anticlockwise arc of its circle, whichever way the edge runs.</summary>
    public static PenCurve Arc(PathCurve c)
    {
        var (centre, r) = (c.Centre, c.Radius);
        double Angle(Vec2 p) => Math.Atan2(p.Y - centre.Y, p.X - centre.X);
        static double Ccw(double from, double to) { var s = (to - from) % (2 * Math.PI); return s < 0 ? s + 2 * Math.PI : s; }
        var (a0, a1, am) = (Angle(c.Start), Angle(c.End), Angle(c.PointAt(c.Length / 2)));
        return Ccw(a0, am) <= Ccw(a0, a1) ? PenCurve.Arc(centre, r, a0, Ccw(a0, a1)) : PenCurve.Arc(centre, r, a1, Ccw(a1, a0));
    }

    /// <summary>
    /// The model's drawn outlines' curves, mapped into a plan's drawing by <paramref name="map"/> (affine), given to every pen that
    /// strokes: DXF writes only the chords that lie on one (an element the view does not show has none there).
    /// </summary>
    public static void Attach(IModelView m, IReadOnlyList<Pen> pens, Func<Vec2, Vec2> map)
    {
        var curves = m.Entities.SelectMany(Of).Select(k => k.Map(map)).ToList();
        if (curves.Count == 0) return;
        foreach (var pen in pens) if (pen.Stroke != null && pen.Segments.Count > 0) pen.Curves.AddRange(curves);
    }
}
