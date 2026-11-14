using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// An opening that cuts every wall it passes through, not only its host (the forum's topic 64, a structural engineer's: „a door
/// which starts in a reinforced concrete wall and goes through the upper masonry part”). A door, window or bare opening hosted by
/// one wall also cuts each straight wall lying along its host — parallel to it, touching or overlapping its body (a wall stacked on
/// it, a lining against its face) — where its box reaches into that wall's body: through that wall's whole thickness, at the same
/// place in plan and in height. Which walls it cuts is worked out on every build from where the walls and the opening are now,
/// never stored, so an opening moved out of a wall no longer cuts it. The host's limits are those of the walls together: an
/// opening may run out of its host into the walls it cuts (<see cref="Reach"/>).
/// <para>
/// On by default (<see cref="Key"/> absent); off for one opening, it cuts its host alone, as before. A wall recognised from an IFC
/// file (its <c>imported</c> record) neither cuts nor is cut so: an import reproduces the file's voids. Only walls: a beam, slab or
/// column the box reaches is left to the cut order. Curved walls neither cut nor are cut so.
/// </para>
/// </summary>
public static class ThroughOpenings
{
    /// <summary>The opening's own choice (bool): false, it cuts its host alone; absent or true, every wall it passes through.</summary>
    public const string Key = "cutThrough";

    /// <summary>How far apart two walls' faces may be and still touch (m); how parallel their axes must be (the sine of the angle).</summary>
    const double Touch = 1e-3, Parallel = 1e-4, Eps = 1e-6;

    public static bool On(Entity opening) => opening.Attrs[Key] is not JsonValue v || !v.TryGetValue<bool>(out var b) || b;

    /// <summary>A straight wall drawn in Lapin, in its own frame: its axis, its base (absolute), its height, its body across the axis.</summary>
    readonly record struct Span(Entity Wall, Vec2 Start, Vec2 Dir, double Length, double Base, double Height, double YMin, double YMax)
    {
        public Vec2 Left => Dir.Perp;
    }

    static Span? SpanOf(IModelView m, Entity wall)
    {
        if (wall.Class != IfcClass.Wall || wall.Attrs["imported"] != null || PathCurve.Of(wall) is { IsArc: true }) return null;
        var (a, b) = (Walls.Start(wall), Walls.End(wall));
        var length = (b - a).Length;
        if (length < 1e-3) return null;
        var bands = Walls.Section(m, wall);
        if (bands.Count == 0) return null;
        var z = (Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");
        return new Span(wall, a, (b - a) * (1 / length), length, z, Walls.HeightOf(m, wall), bands.Min(x => x.From), bands.Max(x => x.To));
    }

    /// <summary><paramref name="o"/>'s axis range and body across, in <paramref name="s"/>'s frame; null when the two are not parallel.</summary>
    static (double X0, double X1, double Y0, double Y1)? In(Span s, Span o)
    {
        if (Math.Abs(Vec2.Cross(s.Dir, o.Dir)) > Parallel) return null;
        var (t0, t1) = (Vec2.Dot(o.Start - s.Start, s.Dir), Vec2.Dot(o.Start + o.Dir * o.Length - s.Start, s.Dir));
        var off = Vec2.Dot(o.Start - s.Start, s.Left);
        var same = Vec2.Dot(s.Dir, o.Dir) > 0;
        var (y0, y1) = same ? (off + o.YMin, off + o.YMax) : (off - o.YMax, off - o.YMin);
        return (Math.Min(t0, t1), Math.Max(t0, t1), y0, y1);
    }

    /// <summary>Whether two ranges overlap by more than nothing.</summary>
    static bool Overlap(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0) > Eps;

    /// <summary>Whether two ranges overlap or touch.</summary>
    static bool Meet(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0) >= -Touch;

    /// <summary>
    /// How far from a wall's axis another wall's axis may lie and their bodies still meet: the wall's own half body and this much
    /// more, the other's (a wall up to 3 m thick, or drawn on a face up to 1.5 m off its axis). A wall farther is not looked at.
    /// </summary>
    const double OtherReach = 1.5;

    /// <summary>
    /// The straight walls round <paramref name="s"/> that lie along it (over it, under it, against its face, in line with it), their
    /// bodies touching or overlapping its own, each in its frame. Only the walls near it are looked at (<see cref="PlaceReads"/>),
    /// sorted by where their attributes put them (axis, base, height: what the cells' stamps go by); only those that meet it are
    /// then read through the model, so an edit of a wall elsewhere (on another storey) does not build this one again.
    /// </summary>
    static IEnumerable<(Span Other, (double X0, double X1, double Y0, double Y1) At)> Along(IModelView m, Span s)
    {
        var end = s.Start + s.Dir * s.Length;
        var reach = Math.Max(Math.Abs(s.YMin), Math.Abs(s.YMax)) + OtherReach;
        foreach (var found in PlaceReads.Candidates(m, IfcClass.Wall, s.Start, end, reach))
        {
            if (found.Id == s.Wall.Id || found.Attrs["imported"] != null) continue;
            // Parallel and near, by its attributes alone.
            var (a, b) = (Walls.Start(found), Walls.End(found));
            var d = b - a;
            if (d.Length < 1e-3 || Math.Abs(Vec2.Cross(s.Dir, d.Normalized())) > Parallel || Math.Abs(Vec2.Cross(s.Dir, a - s.Start)) > reach) continue;
            var (t0, t1) = (Vec2.Dot(a - s.Start, s.Dir), Vec2.Dot(b - s.Start, s.Dir));
            if (!Meet(Math.Min(t0, t1), Math.Max(t0, t1), 0, s.Length)) continue;
            // Where it is in height and across (its storey and type read, the wall itself not yet): only one meeting this wall is read.
            if (SpanOf(m, found) is not { } o || In(s, o) is not { } at) continue;
            if (!Meet(at.X0, at.X1, 0, s.Length) || !Meet(at.Y0, at.Y1, s.YMin, s.YMax) || !Meet(o.Base, o.Base + o.Height, s.Base, s.Base + s.Height)) continue;
            if (m.Get(found.Id) == null) continue;
            yield return (o, at);
        }
    }

    /// <summary>
    /// The openings hosted by other walls that cut <paramref name="wall"/>: each placed on it (its <c>along</c> from the wall's start,
    /// its sill over the wall's base, which may lie under it), with its host. None for a wall recognised from a file, or curved.
    /// </summary>
    public static List<(Openings.Placed Placed, Entity Host)> Into(IModelView m, Entity wall)
    {
        var o = new List<(Openings.Placed, Entity)>();
        if (SpanOf(m, wall) is not { } s) return o;
        foreach (var (other, at) in Along(m, s))
            foreach (var opening in Openings.Of(m, other.Wall.Id))
            {
                if (!On(opening)) continue;
                var p = Openings.Place(m, opening);
                if (!(p.Width > 0 && p.Height > 0)) continue;
                // Where it stands, in this wall's frame: its middle in plan, its bottom over this wall's base.
                var along = Vec2.Dot(other.Start + other.Dir * p.Along - s.Start, s.Dir);
                var sill = other.Base + p.Sill - s.Base;
                // Its box runs through its host's body, which meets this wall's across: it cuts this wall where it reaches into it along and up.
                if (!Overlap(along - p.Width / 2, along + p.Width / 2, 0, s.Length) || !Overlap(sill, sill + p.Height, 0, s.Height)) continue;
                o.Add((p with { Along = along, Sill = sill }, other.Wall));
            }
        return o;
    }

    /// <summary>
    /// Where an opening in <paramref name="wall"/> at <paramref name="along"/> and <paramref name="sill"/> may reach, in the wall's
    /// measure: the wall's length and height, widened by the walls along it that the opening's box reaches into (a wall stacked on
    /// it, under it, or one continuing it in line), which it then cuts too. The wall's own for an opening that cuts its host alone.
    /// </summary>
    public static (double X0, double X1, double Z0, double Z1) Reach(IModelView m, Entity wall, double along, double sill, double width, double height, bool through = true)
    {
        var (x0, x1, z0, z1) = (0.0, PathCurve.Of(wall).Length, 0.0, Walls.HeightOf(m, wall));
        if (!through || SpanOf(m, wall) is not { } s) return (x0, x1, z0, z1);
        // Walls continuing it or over it reach further, each as far as it goes; taken again while one widens it (a wall on a wall on it).
        var spans = WallsBeside(m, s).ToList();
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var (other, at) in spans)
            {
                var (b0, b1) = (other.Base - s.Base, other.Base - s.Base + other.Height);
                // The opening's box, as far as it has grown, must reach this wall's body (in plan, across, and in height).
                if (!Overlap(along - width / 2, along + width / 2, at.X0, at.X1) || !Overlap(sill, sill + height, b0, b1)) continue;
                if (!Meet(at.X0, at.X1, x0, x1) || !Meet(b0, b1, z0, z1)) continue;
                var (n0, n1, n2, n3) = (Math.Min(x0, at.X0), Math.Max(x1, at.X1), Math.Min(z0, b0), Math.Max(z1, b1));
                if (n0 < x0 - Eps || n1 > x1 + Eps || n2 < z0 - Eps || n3 > z1 + Eps) { (x0, x1, z0, z1) = (n0, n1, n2, n3); grew = true; }
            }
        }
        return (x0, x1, z0, z1);
    }

    /// <summary>The straight walls drawn in Lapin parallel to <paramref name="s"/> whose bodies meet its own across it (in line with it, or over it): any along it.</summary>
    static IEnumerable<(Span Other, (double X0, double X1, double Y0, double Y1) At)> WallsBeside(IModelView m, Span s)
    {
        var reach = Math.Max(Math.Abs(s.YMin), Math.Abs(s.YMax)) + OtherReach;
        foreach (var other in m.OfClass(IfcClass.Wall))
        {
            if (other.Id == s.Wall.Id || other.Attrs["imported"] != null) continue;
            // Parallel and near by its attributes before its section is worked out (this runs at every step of a drag).
            var (a, b) = (Walls.Start(other), Walls.End(other));
            var d = b - a;
            if (d.Length < 1e-3 || Math.Abs(Vec2.Cross(s.Dir, d.Normalized())) > Parallel || Math.Abs(Vec2.Cross(s.Dir, a - s.Start)) > reach) continue;
            if (SpanOf(m, other) is not { } o || In(s, o) is not { } at) continue;
            if (!Meet(at.Y0, at.Y1, s.YMin, s.YMax)) continue;
            yield return (o, at);
        }
    }
}
