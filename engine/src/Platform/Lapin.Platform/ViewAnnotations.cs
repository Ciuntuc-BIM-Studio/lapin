using BubbleBim.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What a view is drawing, for the annotations a module adds to it: the view,
/// its kind and frame (world → drawing), its scale, the standards and the
/// generated drawing (cut areas by element: what associative dimensions hold on to).
/// </summary>
public sealed record ViewContext(IModelView Model, Entity View, string Kind, ViewFrame Frame, double Scale, StyleBook Book, ViewDrawing Drawing)
{
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
    public List<(Vec2 A, Vec2 B, string Layer, double Width, string Color, double[]? Dash)> Lines { get; } = [];
    public List<(PenText Text, string Color)> Texts { get; } = [];
    public List<Vec2> Snaps { get; } = [];
    /// <summary>Filled areas (even-odd loops) and their colour, under the lines.</summary>
    public List<(IReadOnlyList<IReadOnlyList<Vec2>> Loops, string Color)> Fills { get; } = [];
    /// <summary>The exact shapes a click or a snap uses (drafted lines, arcs, circles), as their JSON.</summary>
    public List<System.Text.Json.Nodes.JsonObject> Shapes { get; } = [];

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
