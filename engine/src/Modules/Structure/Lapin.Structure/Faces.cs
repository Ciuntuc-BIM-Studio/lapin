using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>
/// The named faces of beams and columns (<see cref="IElementFaces"/>): a
/// straight beam's bottom, top, left and right sides (seen along its axis),
/// its two ends and its two bottom edges; a column's sides (one per side of
/// its section, in its order), its top and its bottom.
/// </summary>
public sealed class StructureFaces : IElementFaces
{
    public bool Covers(string ifcClass) => ifcClass is IfcClass.Beam or IfcClass.Column;

    public IReadOnlyList<ElementFace> FacesOf(Entity e, IModelView m) => e.Class switch
    {
        IfcClass.Beam => Beam(e, m),
        IfcClass.Column => Column(e, m),
        _ => [],
    };

    static List<ElementFace> Beam(Entity beam, IModelView m)
    {
        if (PathCurve.Of(beam).IsArc || Beams.Frame(beam, m) is not { } f) return [];
        var dir = (f.End - f.Start).Normalized();
        var left = dir.Perp;
        var zt = Beams.Top(m, beam);
        var zb = zt - Beams.Spec(m, beam).Section.Depth;
        Vec2 P(Vec2 at, double y) => at + left * y;
        var (sl, el, sr, er) = (P(f.Start, f.YMax), P(f.End, f.YMax), P(f.Start, f.YMin), P(f.End, f.YMin));
        IReadOnlyList<Vec2> band = [sr, er, el, sl];
        var down = -Vec3.UnitZ;
        Vec3 Flat(Vec2 v) => new(v.X, v.Y, 0);
        return
        [
            ElementFaces.Level("Bottom", "Bottom", band, zb, true),
            ElementFaces.Level("Top", "Top", band, zt, false),
            ElementFaces.Upright("Side.Left", "Left side", sl, el, zb, zt, left),
            ElementFaces.Upright("Side.Right", "Right side", sr, er, zb, zt, -left),
            ElementFaces.Upright("End.Start", "Start", sr, sl, zb, zt, -dir),
            ElementFaces.Upright("End.End", "End", er, el, zb, zt, dir),
            new("Edge.BottomLeft", "Bottom left edge", [sl.At(zb), el.At(zb)], (Flat(left) + down).Normalized()),
            new("Edge.BottomRight", "Bottom right edge", [sr.At(zb), er.At(zb)], (Flat(-left) + down).Normalized()),
        ];
    }

    static List<ElementFace> Column(Entity column, IModelView m)
    {
        var (section, _, height) = Columns.Spec(m, column);
        var fp = Polygon2.EnsureCcw(Place.Placed(section.Outline(), Place.Point(column), column.Num("rotation")));
        if (fp.Count < 3 || !(height > 0)) return [];
        var z0 = Place.Elevation(m, column) + column.Num("baseOffset");
        var z1 = z0 + height;
        var faces = new List<ElementFace>
        {
            ElementFaces.Level("Bottom", "Bottom", fp, z0, true),
            ElementFaces.Level("Top", "Top", fp, z1, false),
        };
        for (var i = 0; i < fp.Count; i++)
        {
            var (a, b) = (fp[i], fp[(i + 1) % fp.Count]);
            if ((b - a).Length < 1e-6) continue;
            faces.Add(ElementFaces.Upright($"Side[{i}]", $"Side {i + 1}", a, b, z0, z1, -(b - a).Normalized().Perp));
        }
        return faces;
    }
}
