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
    public bool Covers(string ifcClass) => ifcClass is IfcClass.Beam or IfcClass.Column or IfcClass.Footing;

    public IReadOnlyList<ElementFace> FacesOf(Entity e, IModelView m) => e.Class switch
    {
        IfcClass.Beam => Beam(e, m),
        IfcClass.Column => Column(e, m),
        IfcClass.Footing => Footing(e, m),
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

    /// <summary>
    /// A footing's faces. A pad: each step's sides (Step[k].Side[i]), the top
    /// step's top, the bottom step's bottom. A straight strip: the sole's and
    /// the stem's sides along the axis (Sole.Left/Right, Stem.Left/Right), the
    /// stem's top, the sole's bottom.
    /// </summary>
    static List<ElementFace> Footing(Entity footing, IModelView m)
    {
        if (Place.TypeOf(m, footing.Id) is not { } type) return [];
        var bottom = Place.Elevation(m, footing) + type.Num("bottom", -1.8);
        var faces = new List<ElementFace>();
        if (PadFootings.IsPad(type))
        {
            var z = bottom;
            var steps = PadFootings.Steps(type);
            for (var k = 0; k < steps.Count; k++)
            {
                var fp = Polygon2.EnsureCcw(Place.Placed(Section.Rectangle(steps[k].Length, steps[k].Width).Outline(), Place.Point(footing), footing.Num("rotation")));
                if (k == 0) faces.Add(ElementFaces.Level("Bottom", "Bottom", fp, z, true));
                for (var i = 0; i < fp.Count; i++)
                {
                    var (a, b) = (fp[i], fp[(i + 1) % fp.Count]);
                    faces.Add(ElementFaces.Upright($"Step[{k}].Side[{i}]", $"Step {k + 1}, side {i + 1}", a, b, z, z + steps[k].Height, -(b - a).Normalized().Perp));
                }
                z += steps[k].Height;
                if (k == steps.Count - 1) faces.Add(ElementFaces.Level("Top", "Top", fp, z, false));
            }
            return faces;
        }
        if (!StripFootings.IsStrip(type) || PathCurve.Of(footing).IsArc || StripFootings.Frame(footing, m) is not { } stem) return [];
        var (s, e) = (Joins.Start(footing), Joins.End(footing));
        var left = (e - s).Normalized().Perp;
        var (fh, sh, w) = (type.Num("footingHeight", 0.4), type.Num("stemHeight", 1.4), type.Num("footingWidth", 0.6));
        Vec2 P(Vec2 at, double y) => at + left * y;
        faces.Add(ElementFaces.Level("Bottom", "Bottom", [P(s, -w / 2), P(e, -w / 2), P(e, w / 2), P(s, w / 2)], bottom, true));
        faces.Add(ElementFaces.Upright("Sole.Left", "Sole, left", P(s, w / 2), P(e, w / 2), bottom, bottom + fh, left));
        faces.Add(ElementFaces.Upright("Sole.Right", "Sole, right", P(s, -w / 2), P(e, -w / 2), bottom, bottom + fh, -left));
        if (sh > 0)
        {
            faces.Add(ElementFaces.Upright("Stem.Left", "Stem, left", P(s, stem.YMax), P(e, stem.YMax), bottom + fh, bottom + fh + sh, left));
            faces.Add(ElementFaces.Upright("Stem.Right", "Stem, right", P(s, stem.YMin), P(e, stem.YMin), bottom + fh, bottom + fh + sh, -left));
        }
        faces.Add(ElementFaces.Level("Top", "Top", [P(s, stem.YMin), P(e, stem.YMin), P(e, stem.YMax), P(s, stem.YMax)], bottom + fh + sh, false));
        return faces;
    }
}
