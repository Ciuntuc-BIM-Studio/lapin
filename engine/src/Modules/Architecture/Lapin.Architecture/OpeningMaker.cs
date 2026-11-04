using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Coordination;

namespace Lapin.Architecture;

/// <summary>
/// Openings made from the clash report (decision 293): round what of an MEP element passes through a wall or a floor,
/// its sides the clearance away and its size rounded up to the cut order's step. In a wall a bare opening (round,
/// "circle", for a pipe or a cable; rectangular for a duct or a tray) at the element's place along the wall and up it;
/// in a floor an opening of its own outline (a circle of 32 sides, or the smallest rectangle round the element).
/// Enlarging keeps what the opening had and takes in what passes: a bare wall opening grows to the box of both, a
/// floor opening to a rectangle (or circle) round both. A door's or a window's opening is sized by its type: refused.
/// </summary>
sealed class OpeningMaker : IOpeningMaker
{
    const int CircleSides = 32;

    public string? Make(ICommandContext ctx, Entity host, IReadOnlyList<Solid> through, OpeningRequest r)
    {
        var m = ctx.Model;
        if (host.Class == IfcClass.Wall)
        {
            var (along, sill, w, h, circle) = InWall(m, host, through, r, null);
            return Openings.AddBare(ctx, host, along, sill, w, h, circle ? "circle" : null).Id;
        }
        if (host.Class == IfcClass.Slab)
            return Slabs.AddOpening(ctx, host, InSlab(through.SelectMany(s => s.Vertices).Select(p => p.Xy).ToList(), r, null)).Id;
        return null;
    }

    public bool Enlarge(ICommandContext ctx, Entity host, Entity opening, IReadOnlyList<Solid> through, OpeningRequest r)
    {
        var m = ctx.Model;
        if (host.Class == IfcClass.Wall)
        {
            if (Openings.FillOf(m, opening.Id) is { } fill) throw new CommandException($"„{fill.Name}” sets its opening's size: change its type, or make an opening of its own.");
            var p = Openings.Place(m, opening);
            var (along, sill, w, h, circle) = InWall(m, host, through, r, p);
            Openings.CheckFits(m, host, along, sill, w, h, opening.Id);
            var attrs = opening.CloneAttrs();
            attrs["along"] = along; attrs["sill"] = sill; attrs["width"] = w; attrs["height"] = h;
            attrs.Remove("rise");
            if (circle) attrs["shape"] = "circle"; else attrs.Remove("shape");
            ctx.Replace(opening.With(attrs: attrs));
            return true;
        }
        if (host.Class == IfcClass.Slab)
        {
            if (opening.Str("stairId") != null) throw new CommandException("A stairwell follows its stair: move the pipe or make an opening of its own.");
            var contour = InSlab(through.SelectMany(s => s.Vertices).Select(p => p.Xy).ToList(), r, Slabs.Contour(opening));
            var attrs = opening.CloneAttrs();
            attrs["contour"] = Slabs.ContourJson(Slabs.CheckContour(contour));
            ctx.Replace(opening.With(attrs: attrs));
            return true;
        }
        return false;
    }

    /// <summary>
    /// The opening in a wall: what passes through seen along the wall (along it and up it from its base), grown by the
    /// clearance, rounded up to the step; with <paramref name="keep"/>, the box of that and the opening it had. Round
    /// when asked and the element's own section is about as wide as high (a pipe square through the wall); never past the wall.
    /// </summary>
    static (double Along, double Sill, double Width, double Height, bool Circle) InWall(IModelView m, Entity wall, IReadOnlyList<Solid> through, OpeningRequest r, Openings.Placed? keep)
    {
        var pts = through.SelectMany(s => s.Vertices).ToList();
        if (pts.Count == 0) throw new CommandException("Nothing passes through the wall there.");
        var mid = new Vec2(pts.Average(p => p.X), pts.Average(p => p.Y));
        var f = Openings.FrameOf(m, wall, Openings.AlongOf(wall, mid));
        var curved = PathCurve.Of(wall) is { IsArc: true };
        double U(Vec3 p) => curved ? Openings.AlongOf(wall, p.Xy) : Vec2.Dot(p.Xy - f.Start, f.Dir);
        double a0 = pts.Min(U), a1 = pts.Max(U), z0 = pts.Min(p => p.Z) - f.Base, z1 = pts.Max(p => p.Z) - f.Base;
        var (ew, eh) = (a1 - a0, z1 - z0);
        var circle = r.Round && (keep is { } kept ? kept.Shape?.Kind == "circle" : Math.Abs(ew - eh) <= Math.Max(r.Rule.Step, 0.005) + 1e-6);
        double w, h, along, zc;
        if (keep is { } k)
        {
            // The box of both: what the opening was and what must pass now (with its clearance), then rounded up.
            double ka0 = k.Along - k.Width / 2, ka1 = k.Along + k.Width / 2, kz0 = k.Sill, kz1 = k.Sill + k.Height;
            a0 = Math.Min(ka0, a0 - r.Clearance); a1 = Math.Max(ka1, a1 + r.Clearance); z0 = Math.Min(kz0, z0 - r.Clearance); z1 = Math.Max(kz1, z1 + r.Clearance);
            (w, h) = (r.Rule.Size(a1 - a0, 0), r.Rule.Size(z1 - z0, 0));
            if (circle) w = h = Math.Max(w, h);
            (along, zc) = ((a0 + a1) / 2, (z0 + z1) / 2);
        }
        else
        {
            (w, h) = (r.Rule.Size(ew, r.Clearance), r.Rule.Size(eh, r.Clearance));
            if (circle) w = h = Math.Max(w, h);
            (along, zc) = ((a0 + a1) / 2, (z0 + z1) / 2);
        }
        var length = PathCurve.Of(wall).Length;
        var height = wall.Num("height", Walls.DefaultHeight);
        // Kept within the wall: a pipe at the floor or by an end has its opening pushed in, not cut off.
        along = Math.Clamp(along, w / 2, Math.Max(w / 2, length - w / 2));
        var sill = Math.Clamp(zc - h / 2, 0, Math.Max(0, height - h));
        return (along, sill, Math.Min(w, length), Math.Min(h, height), circle);
    }

    /// <summary>
    /// The opening in a floor: a circle round what passes (round asked and about as wide as long), else the smallest
    /// rectangle round it, grown by the clearance and rounded up to the step; with <paramref name="keep"/>, round both.
    /// </summary>
    static IReadOnlyList<Vec2> InSlab(IReadOnlyList<Vec2> pts, OpeningRequest r, IReadOnlyList<Vec2>? keep)
    {
        if (pts.Count == 0) throw new CommandException("Nothing passes through the floor there.");
        var all = keep is { Count: > 0 } ? pts.Concat(keep).ToList() : pts.ToList();
        var hull = Polygon2.ConvexHull(all);
        var (centre, along, across, len, wid) = MinRectangle(hull);
        var grow = keep is { Count: > 0 } ? 0 : r.Clearance;
        var roundEnough = r.Round && Math.Abs(len - wid) <= Math.Max(r.Rule.Step, 0.005) + 1e-6;
        if (roundEnough)
        {
            var c = new Vec2(hull.Average(p => p.X), hull.Average(p => p.Y));
            var radius = hull.Max(p => (p - c).Length);
            var d = r.Rule.Size(2 * radius, grow);
            return Enumerable.Range(0, CircleSides).Select(i => c + new Vec2(Math.Cos(2 * Math.PI * i / CircleSides), Math.Sin(2 * Math.PI * i / CircleSides)) * (d / 2 / Math.Cos(Math.PI / CircleSides))).ToList();
        }
        var (l, w) = (r.Rule.Size(len, grow) / 2, r.Rule.Size(wid, grow) / 2);
        return [centre - along * l - across * w, centre + along * l - across * w, centre + along * l + across * w, centre - along * l + across * w];
    }

    /// <summary>The smallest rectangle round a convex hull (one of its sides on a hull edge): centre, its axes, length and width.</summary>
    static (Vec2 Centre, Vec2 Along, Vec2 Across, double Length, double Width) MinRectangle(Vec2[] hull)
    {
        if (hull.Length < 2)
        {
            var p = hull.Length == 1 ? hull[0] : new Vec2(0, 0);
            return (p, new Vec2(1, 0), new Vec2(0, 1), 0, 0);
        }
        var best = (Centre: new Vec2(0, 0), Along: new Vec2(1, 0), Across: new Vec2(0, 1), Length: 0.0, Width: 0.0, Area: double.MaxValue);
        // The axes first (most pipes run square to the grid), then each hull edge's direction.
        var dirs = new List<Vec2> { new(1, 0) };
        for (var i = 0; i < hull.Length; i++) if ((hull[(i + 1) % hull.Length] - hull[i]).Length > 1e-9) dirs.Add((hull[(i + 1) % hull.Length] - hull[i]).Normalized());
        foreach (var d in dirs)
        {
            var n = new Vec2(-d.Y, d.X);
            double u0 = hull.Min(p => Vec2.Dot(p, d)), u1 = hull.Max(p => Vec2.Dot(p, d)), v0 = hull.Min(p => Vec2.Dot(p, n)), v1 = hull.Max(p => Vec2.Dot(p, n));
            var area = (u1 - u0) * (v1 - v0);
            if (area < best.Area - 1e-9) best = (d * ((u0 + u1) / 2) + n * ((v0 + v1) / 2), d, n, u1 - u0, v1 - v0, area);
        }
        return (best.Centre, best.Along, best.Across, best.Length, best.Width);
    }
}
