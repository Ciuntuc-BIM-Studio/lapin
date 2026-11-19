using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>
/// Columns and beams as temporary-dimension lines (decision 733, <see cref="DimLines"/>). A column is two lines through its
/// insertion point, along its own x (its rotation) and its own y, each as long as the column is across that way: its core
/// faces the extent of its section's outline, its faces that of its finish layers' outermost outline (decision 719), the
/// core's when it has none. A straight beam is its axis with its section's width either side (core and faces alike) and its
/// joins, as a wall is. Columns placed by an IFC (no point of their own) and curved beams are left out.
/// </summary>
public sealed class MemberDimLines : IDimLineProvider
{
    public IEnumerable<DimLine> Lines(IModelView model, string storeyId)
    {
        foreach (var e in model.OfClass(IfcClass.Column))
        {
            if (Spatial.StoreyOf(model, e.Id)?.Id != storeyId || e.Attrs["at"] is null) continue;
            foreach (var l in ColumnLines(model, e)) yield return l;
        }
        foreach (var b in model.OfClass(IfcClass.Beam))
        {
            if (Spatial.StoreyOf(model, b.Id)?.Id != storeyId || PathCurve.Of(b).IsArc || Beams.Frame(b, model) is not { } f) continue;
            var (atStart, atEnd, along) = DimLines.JoinsOf(model, b.Id);
            yield return new DimLine(b.Id, b.Name, "beam", f.Start, f.End, f.YMin, f.YMax, f.YMin, f.YMax, null, null, atStart, atEnd, along);
        }
    }

    /// <summary>The column's two lines, x then y, in plan.</summary>
    public static IEnumerable<DimLine> ColumnLines(IModelView m, Entity column)
    {
        var (section, _, _) = Columns.Spec(m, column);
        var core = section.Outline();
        if (core.Count < 3) yield break;
        var layers = Columns.Layers(m, column);
        var outer = layers.Count > 0 ? ColumnWraps.Shapes(section, layers).Outline : core;
        var (cx0, cx1, cy0, cy1) = (core.Min(p => p.X), core.Max(p => p.X), core.Min(p => p.Y), core.Max(p => p.Y));
        var (fx0, fx1, fy0, fy1) = (outer.Min(p => p.X), outer.Max(p => p.X), outer.Min(p => p.Y), outer.Max(p => p.Y));
        var (at, rot) = (Place.Point(column), column.Num("rotation"));
        var p = Place.Placed([new Vec2(fx0, 0), new Vec2(fx1, 0), new Vec2(0, fy0), new Vec2(0, fy1)], at, rot);
        // Along x: left of it is the column's +y. Along y: left of it is its −x.
        yield return new DimLine(column.Id, column.Name, "column", p[0], p[1], fy0, fy1, cy0, cy1, null, "x");
        yield return new DimLine(column.Id, column.Name, "column", p[2], p[3], -fx1, -fx0, -cx1, -cx0, null, "y");
    }
}
