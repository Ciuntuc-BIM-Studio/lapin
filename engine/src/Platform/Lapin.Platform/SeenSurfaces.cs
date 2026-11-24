using Lapin.Drawing.Views;
using Lapin.Geometry;

namespace Lapin.Platform;

/// <summary>
/// What a drawing shows of its elements' materials (forum topic 255, material keys): the material seen at a point (a cut area's,
/// else the nearest face's there: a layered wall's layers are parts of their own, so an elevation sees its render, not its core)
/// and the materials its cuts and the faces it sees show at all. Made with the drawing (<see cref="Views.Render"/>), from the
/// faces its surface hatches and shadows are worked out on, so nothing is swept twice; the visible faces are kept in the
/// surface hatches' cache of the view.
/// </summary>
public sealed class SeenSurfaces
{
    readonly ViewDrawing drawing;
    readonly IReadOnlyList<SurfaceHatches.Piece> pieces;
    readonly IReadOnlyList<DrawPart> parts;
    readonly string viewId;
    List<string>? seen;

    public SeenSurfaces(ViewDrawing drawing, IReadOnlyList<SurfaceHatches.Piece> pieces, IReadOnlyList<DrawPart> parts, string viewId)
    {
        this.drawing = drawing;
        this.pieces = pieces;
        this.parts = parts;
        this.viewId = viewId;
    }

    /// <summary>What is seen at a point: the element and its material there (a cut area under it, else the nearest face).</summary>
    public readonly record struct Hit(string Element, string Material);

    /// <summary>
    /// The material seen at <paramref name="at"/> (the view's coordinates): a cut area's, else the nearest face's; of
    /// <paramref name="element"/> only when one is named (its own face there, though another stands in front). Null: none.
    /// </summary>
    public Hit? At(Vec2 at, string? element = null)
    {
        foreach (var c in drawing.Cuts)
            if (c.Material is { Length: > 0 } cm && (element == null || c.Element == element) && Polygon2.Inside(c.Loops, at)) return new Hit(c.Element, cm);
        Hit? best = null;
        var depth = double.PositiveInfinity;
        foreach (var piece in pieces)
        {
            var q = at - new Vec2(piece.Station, 0);
            if (SurfaceHatches.WindowOf(piece.Spec) is { } w && (q.X < w.X.Min || q.X > w.X.Max || q.Y < w.Y.Min || q.Y > w.Y.Max)) continue;
            var faces = piece.Scene();
            foreach (var s in faces.SeenFaces)
            {
                if (s.Face.Owner < 0 || s.Face.Owner >= parts.Count) continue;
                var part = parts[s.Face.Owner];
                if (element != null && part.Element != element) continue;
                if (q.X < s.Box.Min.X - 1e-9 || q.X > s.Box.Max.X + 1e-9 || q.Y < s.Box.Min.Y - 1e-9 || q.Y > s.Box.Max.Y + 1e-9) continue;
                if (!Polygon2.Inside(s.View, q)) continue;
                var d = faces.DepthOn(s.Face.Normal, s.P0, q);
                if (!(d < depth - 1e-6)) continue;
                depth = d;
                best = part.Material is { Length: > 0 } pm ? new Hit(part.Element, pm) : null;
            }
        }
        return best;
    }

    /// <summary>The materials the drawing's cut areas are of (those a cut names), in the drawing's order.</summary>
    public IEnumerable<string> Cut() => drawing.Cuts.Select(c => c.Material).OfType<string>().Where(x => x.Length > 0).Distinct();

    /// <summary>The materials of the faces the drawing sees where no nearer face hides them, in the parts' order.</summary>
    public IReadOnlyList<string> Seen()
    {
        if (seen != null) return seen;
        var o = new List<string>();
        var have = new HashSet<string>();
        foreach (var piece in pieces)
        {
            var faces = piece.Scene();
            var cache = SurfaceHatches.CacheOf(viewId + (pieces.Count > 1 ? "#" + piece.Index : "")).For(faces);
            var window = SurfaceHatches.WindowOf(piece.Spec);
            foreach (var (s, cells) in faces.VisibleAll(s => s.Face.Owner < parts.Count && parts[s.Face.Owner].Material is { Length: > 0 }, cache))
            {
                var mat = parts[s.Face.Owner].Material!;
                if (have.Contains(mat)) continue;
                var area = 0.0;
                foreach (var c in cells)
                {
                    if (window is { } w && c.All(p => p.X < w.X.Min || p.X > w.X.Max || p.Y < w.Y.Min || p.Y > w.Y.Max)) continue;
                    area += Math.Abs(Polygon2.SignedArea(c));
                }
                // A sliver of a face (a millimetre by a metre) is not what the drawing shows of a material.
                if (area < 1e-4) continue;
                have.Add(mat);
                o.Add(mat);
            }
        }
        return seen = o;
    }

    /// <summary>Nothing seen (a plan, a 3D view, a sheet's paper): cut areas only.</summary>
    public static SeenSurfaces CutsOnly(ViewDrawing drawing, string viewId) => new(drawing, [], [], viewId);
}
