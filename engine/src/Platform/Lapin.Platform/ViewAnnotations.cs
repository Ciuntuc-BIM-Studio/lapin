using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What a view is drawing, for the annotations a module adds to it: the view,
/// its kind and frame (world → drawing), its scale, the standards and the
/// generated drawing (cut areas by element: what associative dimensions hold on to).
/// </summary>
public sealed record ViewContext(IModelView Model, Entity View, string Kind, ViewFrame Frame, double Scale, StyleBook Book, ViewDrawing Drawing, Func<string, ElementGeometry?>? Geometry = null)
{
    /// <summary>The modules' services (a section's line along an element is read through them); null when a caller has none.</summary>
    public IServices? Services { get; init; }

    /// <summary>
    /// Whether something stands between the eye and <paramref name="point"/>: a face of
    /// another element, turned to the eye, nearer, with the point inside its outline.
    /// </summary>
    public bool Occluded(Vec3 point, IReadOnlySet<string> except)
    {
        if (Geometry == null) return false;
        var at = Map(point);
        var depth = Frame.Depth(point);
        var eye = Frame.Origin + Frame.Right * at.X + Frame.Up * at.Y;
        foreach (var e in Model.Entities)
        {
            if (except.Contains(e.Id) || e.Class is IfcClass.OpeningElement or IfcClass.Space || Spatial.StoreyOf(Model, e.Id) == null) continue;
            if (Geometry(e.Id) is not { } g) continue;
            foreach (var part in g.Parts)
            {
                var s = part.Solid;
                foreach (var f in s.Faces)
                {
                    var nd = Vec3.Dot(f.Normal, Frame.Dir);
                    if (nd > -1e-6 || f.Outer.Length < 3) continue;
                    var p0 = s.Vertices[f.Outer[0]];
                    var t = Vec3.Dot(f.Normal, p0 - eye) / nd;
                    if (t > depth - 1e-3) continue;
                    var rings = new Vec2[1 + f.Holes.Length][];
                    rings[0] = f.Outer.Select(i => Map(s.Vertices[i])).ToArray();
                    for (var h = 0; h < f.Holes.Length; h++) rings[h + 1] = f.Holes[h].Select(i => Map(s.Vertices[i])).ToArray();
                    if (Polygon2.Inside(rings, at)) return true;
                }
            }
        }
        return false;
    }

    /// <summary>Metres of the drawing per paper millimetre.</summary>
    public double Mm => Scale / 1000;
    public Vec2 Map(Vec3 p) => Frame.Map(p);
    public Vec2 Map(Vec2 p, double z = 0) => Frame.Map(new Vec3(p.X, p.Y, z));
}

/// <summary>
/// One annotation drawn on a view: its owner (the entity a click selects, or
/// null), its lines (layer, paper width and dashes, colour) and texts, and what
/// a click hits (segments in the view's coordinates).
/// </summary>
public sealed class ViewMark(string? owner)
{
    public string? Owner { get; } = owner;
    /// <summary>Drawn under the model's own pens, not over them: a room's colour fill and hatch, which walls and furniture stand on.</summary>
    public bool Under { get; init; }
    public List<(Vec2 A, Vec2 B, string Layer, double Width, string Color, double[]? Dash)> Lines { get; } = [];
    public List<(PenText Text, string Color)> Texts { get; } = [];
    public List<Vec2> Snaps { get; } = [];
    /// <summary>Filled areas (even-odd loops) and their colour, under the lines.</summary>
    public List<(IReadOnlyList<IReadOnlyList<Vec2>> Loops, string Color)> Fills { get; } = [];
    /// <summary>Pictures under everything (an orthophoto), by their corners in the view's coordinates; not counted in the view's extent.</summary>
    public List<PenImage> Images { get; } = [];
    /// <summary>The exact shapes a click or a snap uses (drafted lines, arcs, circles), as their JSON.</summary>
    public List<System.Text.Json.Nodes.JsonObject> Shapes { get; } = [];
    /// <summary>What the mark tells the interface of itself (a dimension string: its points as drawn now, for its grips).</summary>
    public System.Text.Json.Nodes.JsonObject? Data { get; set; }

    public ViewMark Line(Vec2 a, Vec2 b, string layer, double width, string color = "#000000", double[]? dash = null)
    {
        Lines.Add((a, b, layer, width, color, dash is { Length: > 0 } ? dash : null));
        return this;
    }

    public ViewMark Polyline(IReadOnlyList<Vec2> pts, bool closed, string layer, double width, string color = "#000000", double[]? dash = null)
    {
        for (var i = 0; i + 1 < pts.Count + (closed ? 1 : 0); i++) Line(pts[i], pts[(i + 1) % pts.Count], layer, width, color, dash);
        return this;
    }

    public ViewMark Text(PenText t, string color = "#000000") { Texts.Add((t, color)); return this; }

    /// <summary>A circle as a polygon of <paramref name="n"/> sides.</summary>
    public static Vec2[] Circle(Vec2 c, double r, int n = 32) => Enumerable.Range(0, n).Select(k => c + new Vec2(Math.Cos(2 * Math.PI * k / n), Math.Sin(2 * Math.PI * k / n)) * r).ToArray();
}

/// <summary>A module's annotations on views: grids, dimensions, texts…</summary>
public interface IViewAnnotations
{
    IEnumerable<ViewMark> Marks(ViewContext c);
}

/// <summary>How a module shows an element in a view: hidden, or drawn in another colour; null: as it is (phases).</summary>
public interface IViewElementFilter
{
    (bool Hide, string? Color)? For(IModelView model, Entity view, Entity element);
}

/// <summary>
/// Elements that follow another's phases: generated from it and living with
/// it (a turnout's routes, a crossover's turnouts and connection, a
/// crossing's slips). Assigning a phase to the owner assigns it to them too;
/// the module that generates them keeps the phases on them when it re-makes
/// them. Offered as a service; the phases work without any.
/// </summary>
public interface IPhaseFollowers
{
    IEnumerable<string> FollowersOf(IModelView model, Entity element);
}
