using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Coordination;
using Lapin.Architecture.Roofs;

namespace Lapin.Architecture;

/// <summary>
/// The voids of the openings in walls, floors and roofs (decision 289): each
/// opening's own body (its head's outline, a curved wall's band, a floor
/// opening's outline, a roof window's or dormer's hole), exactly through the
/// host's whole thickness, finishes included, its sides moved by the offset.
/// </summary>
sealed class ArchitectureVoids : IOpeningVoids
{
    public IEnumerable<OpeningVoid> Voids(IModelView m, double offset)
    {
        foreach (var wall in m.OfClass(IfcClass.Wall))
            foreach (var opening in Openings.Of(m, wall.Id))
                if (InWall(m, wall, opening, offset) is { } v) yield return v;
        foreach (var slab in m.OfClass(IfcClass.Slab))
            foreach (var (opening, contour) in Slabs.Holes(m, slab.Id))
                if (InSlab(m, slab, opening, contour, offset) is { } v) yield return v;
        foreach (var roof in m.OfClass(IfcClass.Roof))
            foreach (var v in InRoof(m, roof, offset)) yield return v;
    }

    static OpeningVoid? InWall(IModelView m, Entity wall, Entity opening, double offset)
    {
        var p0 = Openings.Place(m, opening);
        var p = p0 with { Width = p0.Width + 2 * offset, Height = p0.Height + 2 * offset, Sill = p0.Sill - offset };
        if (p.Width < 0.005 || p.Height < 0.005) return null;
        var bands = Walls.Section(m, wall);
        if (bands.Count == 0) return null;
        var f = Openings.FrameOf(m, wall, p.Along);
        var shaped = p.Spec ?? (p.Shape is { } bare ? JoinerySpec.DefaultWindow("Opening") with { Shape = bare } : null);
        var isShaped = shaped is { } sp && JoineryShapes.Shaped(sp);
        Solid? solid;
        if (PathCurve.Of(wall) is { IsArc: true } c)
            solid = Prism.ExtrudeFootprint(c.Band(_ => p.Along - p.Width / 2, _ => p.Along + p.Width / 2, f.YMin, f.YMax), f.Base + p.Sill, p.Height, "void");
        else if (isShaped)
        {
            var sized = shaped! with { Width = p.Width, Height = p.Height };
            var outline = JoineryShapes.Outline(sized, 0, 0).Select(q => f.At(p.Along + q.X, f.YMin, p.Sill + q.Y)).ToList();
            solid = Prism.Extrude(outline, null, f.At(0, f.YMax, 0) - f.At(0, f.YMin, 0), "void");
        }
        else
        {
            double x0 = p.Along - p.Width / 2, x1 = p.Along + p.Width / 2;
            Vec2[] footprint = [f.Start + f.Dir * x0 + f.Left * f.YMin, f.Start + f.Dir * x1 + f.Left * f.YMin, f.Start + f.Dir * x1 + f.Left * f.YMax, f.Start + f.Dir * x0 + f.Left * f.YMax];
            solid = Prism.ExtrudeFootprint(footprint, f.Base + p.Sill, p.Height, "void");
        }
        if (solid == null) return null;
        var kind = p.Fill == null ? OpeningVoids.Wall : p.Fill.Class == IfcClass.Door ? OpeningVoids.Door : OpeningVoids.Window;
        var shape = !isShaped ? OpeningVoids.Rectangle : shaped!.Shape?.Kind == "circle" ? OpeningVoids.Round : OpeningVoids.Undefined;
        return new OpeningVoid(opening.Id, wall.Id, opening.Id, p.Fill?.Id, kind, shape, p.Width, p.Height, f.YMax - f.YMin, solid, Spatial.StoreyOf(m, wall.Id)?.Id);
    }

    static OpeningVoid? InSlab(IModelView m, Entity slab, Entity opening, IReadOnlyList<Vec2> contour, double offset)
    {
        var section = Slabs.Section(m, slab);
        if (section.Count == 0) return null;
        var ccw = Polygon2.EnsureCcw(contour);
        IReadOnlyList<Vec2> outline = offset != 0 ? Polygon2.Offset(ccw, [offset]) : ccw;
        if (Math.Abs(Polygon2.SignedArea(outline)) < 1e-6 || Polygon2.SignedArea(outline) < 0) return null;
        var top = Slabs.Top(m, slab);
        var (z0, z1) = (top + section.Min(b => b.From), top + section.Max(b => b.To));
        if (Prism.ExtrudeFootprint(outline, z0, z1 - z0, "void") is not { } solid) return null;
        var (w, h) = (outline.Max(q => q.X) - outline.Min(q => q.X), outline.Max(q => q.Y) - outline.Min(q => q.Y));
        var shape = IsRound(outline) ? OpeningVoids.Round : outline.Count == 4 && IsRectangle(outline) ? OpeningVoids.Rectangle : OpeningVoids.Undefined;
        if (shape == OpeningVoids.Rectangle) (w, h) = ((outline[1] - outline[0]).Length, (outline[2] - outline[1]).Length);
        return new OpeningVoid(opening.Id, slab.Id, opening.Id, null, OpeningVoids.Slab, shape, w, h, z1 - z0, solid, Spatial.StoreyOf(m, slab.Id)?.Id);
    }

    static IEnumerable<OpeningVoid> InRoof(IModelView m, Entity roof, double offset)
    {
        var r = Roof.Resolve(roof, m);
        if (r == null || r.Flat || r.Barrel) yield break;
        var (shape, _) = Roof.Solve(r);
        if (shape == null) yield break;
        foreach (var p in RoofOpenings.Place(roof, shape, m, []))
        {
            var cos = Math.Cos(p.Host.Plane.Pitch * Math.PI / 180);
            var plan = Polygon2.EnsureCcw(p.Hole.Select(q => q.Xy).ToList());
            IReadOnlyList<Vec2> grown = offset != 0 ? Polygon2.Offset(plan, [offset]) : plan;
            if (Polygon2.SignedArea(grown) <= 1e-6) continue;
            var hole = grown.Select(q => q.At(p.Host.Z(q))).ToList();
            if (Prism.Extrude(hole, null, new Vec3(0, 0, r.Thickness / cos), "void") is not { } solid) continue;
            yield return new OpeningVoid($"{roof.Id}#{p.Opening.Id}", roof.Id, null, null, OpeningVoids.Roof, OpeningVoids.Rectangle,
                p.Opening.Width + 2 * offset, p.Opening.Length + 2 * offset, r.Thickness, solid, Spatial.StoreyOf(m, roof.Id)?.Id);
        }
    }

    static bool IsRectangle(IReadOnlyList<Vec2> q)
    {
        for (var i = 0; i < 4; i++)
        {
            var a = q[(i + 1) % 4] - q[i];
            var b = q[(i + 2) % 4] - q[(i + 1) % 4];
            if (Math.Abs(Vec2.Dot(a.Normalized(), b.Normalized())) > 1e-3) return false;
        }
        return true;
    }

    /// <summary>An outline of a dozen points or more all as far from their middle: a round opening (a core drill's).</summary>
    static bool IsRound(IReadOnlyList<Vec2> q)
    {
        if (q.Count < 12) return false;
        var c = new Vec2(q.Average(p => p.X), q.Average(p => p.Y));
        var radii = q.Select(p => (p - c).Length).ToList();
        return radii.Max() - radii.Min() < Math.Max(0.002, radii.Average() * 0.01);
    }
}
