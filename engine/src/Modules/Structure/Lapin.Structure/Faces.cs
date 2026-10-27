using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>
/// The named faces of beams and columns (<see cref="IElementFaces"/>): a
/// beam's bottom, top, left and right sides (seen along its axis), its two
/// ends, its axis and its two bottom edges (a curved beam's sides, axis and
/// edges as curves along the arc); a column's sides (one per side of
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
        if (Beams.Frame(beam, m) is not { } f) return [];
        var c = PathCurve.Of(beam);
        var zt = Beams.Top(m, beam);
        var zb = zt - Beams.Spec(m, beam).Section.Depth;
        var down = -Vec3.UnitZ;
        Vec3 Flat(Vec2 v) => new(v.X, v.Y, 0);
        if (c.IsArc)
        {
            // A curved beam: its bottom and top as bands along the arc, its ends upright; its sides, axis and bottom edges as curves.
            var L = c.Length;
            var band = c.Band(_ => 0, _ => L, f.YMin, f.YMax);
            var (t0, t1) = (c.TangentAt(0), c.TangentAt(L));
            var mid = c.TangentAt(L / 2).Perp;
            return
            [
                ElementFaces.Level("Bottom", "Bottom", band, zb, true),
                ElementFaces.Level("Top", "Top", band, zt, false),
                ElementFaces.Upright("End.Start", "Start", c.At(0, f.YMin), c.At(0, f.YMax), zb, zt, -t0),
                ElementFaces.Upright("End.End", "End", c.At(L, f.YMin), c.At(L, f.YMax), zb, zt, t1),
                ElementFaces.Along("Axis", "Axis", c.Line(0, L, 0), zb, down),
                ElementFaces.Along("Side.Left", "Left side", c.Line(0, L, f.YMax), zb, Flat(mid)),
                ElementFaces.Along("Side.Right", "Right side", c.Line(0, L, f.YMin), zb, Flat(-mid)),
                ElementFaces.Along("Edge.BottomLeft", "Bottom left edge", c.Line(0, L, f.YMax), zb, (Flat(mid) + down).Normalized()),
                ElementFaces.Along("Edge.BottomRight", "Bottom right edge", c.Line(0, L, f.YMin), zb, (Flat(-mid) + down).Normalized()),
            ];
        }
        var dir = (f.End - f.Start).Normalized();
        var left = dir.Perp;
        Vec2 P(Vec2 at, double y) => at + left * y;
        var (sl, el, sr, er) = (P(f.Start, f.YMax), P(f.End, f.YMax), P(f.Start, f.YMin), P(f.End, f.YMin));
        IReadOnlyList<Vec2> rect = [sr, er, el, sl];
        return
        [
            ElementFaces.Level("Bottom", "Bottom", rect, zb, true),
            ElementFaces.Level("Top", "Top", rect, zt, false),
            ElementFaces.Upright("Side.Left", "Left side", sl, el, zb, zt, left),
            ElementFaces.Upright("Side.Right", "Right side", sr, er, zb, zt, -left),
            ElementFaces.Upright("End.Start", "Start", sr, sl, zb, zt, -dir),
            ElementFaces.Upright("End.End", "End", er, el, zb, zt, dir),
            ElementFaces.Along("Axis", "Axis", [f.Start, f.End], zb, down),
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
