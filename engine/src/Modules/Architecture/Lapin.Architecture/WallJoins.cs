using BubbleBim.Geometry;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// How far a wall's body reaches past one end of its axis, measured along the
/// axis at each face: positive past the end, negative short of it. Equal
/// values are a square cut.
/// </summary>
public readonly record struct EndCut(double Left, double Right)
{
    public static readonly EndCut Square = new(0, 0);
}

/// <summary>
/// A straight wall as a join sees it: its axis in plan and its body's extent
/// across the axis (left of Start → End positive), with what is already cut
/// at its ends.
/// </summary>
public sealed record JoinWall(Vec2 Start, Vec2 End, double YMin, double YMax, EndCut? StartCut = null, EndCut? EndCut = null)
{
    public double Thickness => YMax - YMin;

    /// <summary>A body of <paramref name="thickness"/> with the axis on its centre, its left or its right face, shifted by <paramref name="offset"/> (to the left).</summary>
    public static JoinWall FromThickness(Vec2 start, Vec2 end, double thickness, string alignment = "center", double offset = 0)
    {
        var half = thickness / 2;
        var centre = offset + alignment switch { "left" => -half, "right" => half, _ => 0 };
        return new JoinWall(start, end, centre - half, centre + half);
    }
}

public enum JoinKind { L, T, Butt }
public enum JoinStyle { Butt, Mitre }
public enum Connection { AtStart, AtEnd, AtPath }

/// <summary>One wall's part in a join: where it takes part, whether it runs through, and the wall after the join.</summary>
public sealed record JoinSide(Connection Connection, bool RunsThrough, JoinWall Wall);

/// <summary>A join: its kind and style, where the axes meet, which wall relates (runs through; a for a butt join), and both walls after it.</summary>
public sealed record WallJoin(JoinKind Kind, JoinStyle Style, Vec2 Point, char Relating, JoinSide A, JoinSide B);

/// <param name="Tolerance">How far an axis end may sit from the crossing and still be joined there. Default twice the thicker wall.</param>
/// <param name="Priority">Which wall runs through an L and relates a butt join. Default the thicker, a on a tie. A T's path wall always runs through.</param>
/// <param name="Style">How an L is cut. A T and an in-line join are always butt.</param>
public sealed record JoinOptions(double? Tolerance = null, char? Priority = null, JoinStyle Style = JoinStyle.Butt);

/// <summary>A wall body's plan outline in its own frame (origin at Start, X along the axis, Y to the left): right-start, right-end, left-end, left-start.</summary>
public sealed record WallOutline(Vec2[] Corners, bool Rectangular, double Length);

/// <summary>
/// Where two straight walls meet: which one runs through and how far each
/// body reaches past, or stops short of, its axis end, so the two bodies
/// meet with no overlap and no gap. Plan geometry only. Specified by the old
/// Lapin's wall joins (docs/transfer.md):
/// <list type="bullet">
/// <item><b>L</b>: both walls end where their axes cross. One runs through to
/// the other's outer face, the other stops at the first one's inner face.
/// Or, mitred, both are cut on the corner's diagonal (inner corner to outer
/// corner).</item>
/// <item><b>T</b>: one wall ends on the other's path. The path wall stays as
/// it is; the ending wall stops at its near face.</item>
/// <item><b>Butt</b>: the walls are in line and meet end to end; both stop
/// square at the point between their ends.</item>
/// </list>
/// The joined axis ends are brought to the crossing. A cut at any other
/// angle than square follows the other wall's face; no boolean is needed.
/// </summary>
public static class WallJoins
{
    const double ParallelSin = 1e-6;
    const double Eps = 1e-9;

    sealed record Frame(JoinWall Wall, Vec2 Origin, Vec2 Dir, Vec2 Normal, double Length)
    {
        public double YMin => Wall.YMin;
        public double YMax => Wall.YMax;
        public Vec2 Outward(Connection end) => end == Connection.AtEnd ? Dir : -Dir;
        /// <summary>+1 when <paramref name="v"/> points to the left of the axis, -1 to the right.</summary>
        public int SideOf(Vec2 v) => Vec2.Dot(v, Normal) >= 0 ? 1 : -1;
        public Vec2 EndPoint(Connection end) => end == Connection.AtStart ? Wall.Start : Wall.End;
    }

    static Frame FrameOf(JoinWall w)
    {
        var d = w.End - w.Start;
        var length = d.Length;
        var dir = d * (1 / length);
        return new Frame(w, w.Start, dir, dir.Perp, length);
    }

    static void Check(JoinWall w, string label)
    {
        double[] values = [w.Start.X, w.Start.Y, w.End.X, w.End.Y, w.YMin, w.YMax,
            w.StartCut?.Left ?? 0, w.StartCut?.Right ?? 0, w.EndCut?.Left ?? 0, w.EndCut?.Right ?? 0];
        if (values.Any(v => !double.IsFinite(v))) throw new CommandException($"Wall {label} has a non-finite coordinate or dimension.");
        if (!(w.Thickness > 0)) throw new CommandException($"Wall {label}: its thickness must be positive.");
        if ((w.End - w.Start).Length <= Eps) throw new CommandException($"Wall {label}: its start and end must be distinct points.");
    }

    /// <summary>The body outline; refused when the cuts leave a face with no length (the wall is too short for its joins).</summary>
    public static WallOutline Outline(JoinWall w)
    {
        Check(w, "");
        var length = (w.End - w.Start).Length;
        var s = w.StartCut ?? EndCut.Square;
        var e = w.EndCut ?? EndCut.Square;
        Vec2[] corners = [new(-s.Right, w.YMin), new(length + e.Right, w.YMin), new(length + e.Left, w.YMax), new(-s.Left, w.YMax)];
        if (corners[1].X - corners[0].X <= Eps || corners[2].X - corners[3].X <= Eps)
            throw new CommandException("The wall is too short for this join: its ends cross.");
        var rectangular = Math.Abs(s.Left - s.Right) <= Eps && Math.Abs(e.Left - e.Right) <= Eps;
        return new WallOutline(corners, rectangular, length);
    }

    /// <summary>Where a point <paramref name="s"/> metres along an axis falls: at an end (within the tolerance), on the path, or off it.</summary>
    static Connection? Place(double s, double length, double tolerance)
    {
        var fromStart = Math.Abs(s);
        var fromEnd = Math.Abs(s - length);
        if (fromStart <= tolerance || fromEnd <= tolerance) return fromStart <= fromEnd ? Connection.AtStart : Connection.AtEnd;
        return s > 0 && s < length ? Connection.AtPath : null;
    }

    /// <summary>The cut at a joined end of <paramref name="f"/> lying along a line: how far past the joint each face reaches, outward.</summary>
    static EndCut CutAlong(Frame f, Connection end, Vec2 joint, Vec2 linePoint, Vec2 lineDir)
    {
        var u = f.Outward(end);
        var denom = Vec2.Cross(u, lineDir);
        // joint + s·u + y·n lies on the line  ⇒  s = cross(linePoint − joint − y·n, lineDir) / cross(u, lineDir)
        double At(double y) => Vec2.Cross(linePoint - joint - f.Normal * y, lineDir) / denom;
        return new EndCut(At(f.YMax), At(f.YMin));
    }

    /// <summary>The wall with its joined end at the joint and cut; refused when that leaves it no body.</summary>
    static JoinWall Joined(Frame f, Connection end, Vec2 joint, EndCut cut)
    {
        var w = end == Connection.AtStart ? f.Wall with { Start = joint, StartCut = cut } : f.Wall with { End = joint, EndCut = cut };
        Outline(w);
        return w;
    }

    public static WallJoin Compute(JoinWall a, JoinWall b, JoinOptions? options = null)
    {
        options ??= new JoinOptions();
        Check(a, "a");
        Check(b, "b");
        var tolerance = options.Tolerance ?? 2 * Math.Max(a.Thickness, b.Thickness);
        if (!double.IsFinite(tolerance) || tolerance < 0) throw new CommandException("The join tolerance must be a finite, non-negative length.");
        if (options.Priority is { } p && p != 'a' && p != 'b') throw new CommandException("The join priority is a or b.");

        var fa = FrameOf(a);
        var fb = FrameOf(b);
        var denom = Vec2.Cross(fa.Dir, fb.Dir);
        if (Math.Abs(denom) <= ParallelSin) return InLine(fa, fb, tolerance, options);

        var w = fb.Origin - fa.Origin;
        var sA = Vec2.Cross(w, fb.Dir) / denom;
        var sB = Vec2.Cross(w, fa.Dir) / denom;
        var point = fa.Origin + fa.Dir * sA;
        var placeA = Place(sA, fa.Length, tolerance);
        var placeB = Place(sB, fb.Length, tolerance);
        if (placeA == null || placeB == null) throw new CommandException($"The walls do not meet: their axes cross more than {tolerance:0.###} m past an end.");
        if (placeA == Connection.AtPath && placeB == Connection.AtPath) throw new CommandException("The walls cross each other: split one of them at the crossing first.");

        if (placeA == Connection.AtPath || placeB == Connection.AtPath)
        {
            // T: the path wall stays; the other stops at its near face.
            var throughIsA = placeA == Connection.AtPath;
            var (fr, fw) = throughIsA ? (fa, fb) : (fb, fa);
            var ending = Ending(fr, fw, (throughIsA ? placeB : placeA)!.Value, point);
            var through = new JoinSide(Connection.AtPath, true, fr.Wall);
            return new WallJoin(JoinKind.T, JoinStyle.Butt, point, throughIsA ? 'a' : 'b', throughIsA ? through : ending, throughIsA ? ending : through);
        }

        // L: both end at the corner.
        var endA = placeA!.Value;
        var endB = placeB!.Value;
        var aThrough = (options.Priority ?? (b.Thickness > a.Thickness ? 'b' : 'a')) == 'a';
        if (options.Style == JoinStyle.Mitre) return Mitre(fa, fb, endA, endB, point, aThrough ? 'a' : 'b');
        {
            var (fr, fw, endR, endW) = aThrough ? (fa, fb, endA, endB) : (fb, fa, endB, endA);
            var ending = Ending(fr, fw, endW, point);
            // The through wall reaches the ending wall's outer face: the one away from where the through wall comes in.
            var comesFrom = fw.SideOf(-fr.Outward(endR));
            var outerY = comesFrom > 0 ? fw.YMin : fw.YMax;
            var cut = CutAlong(fr, endR, point, point + fw.Normal * outerY, fw.Dir);
            var through = new JoinSide(endR, true, Joined(fr, endR, point, cut));
            return new WallJoin(JoinKind.L, JoinStyle.Butt, point, aThrough ? 'a' : 'b', aThrough ? through : ending, aThrough ? ending : through);
        }
    }

    /// <summary>The wall that stops at the through wall's face: the face on the side it comes in from.</summary>
    static JoinSide Ending(Frame fr, Frame fw, Connection endW, Vec2 point)
    {
        var comesFrom = fr.SideOf(-fw.Outward(endW));
        var faceY = comesFrom > 0 ? fr.YMax : fr.YMin;
        var cut = CutAlong(fw, endW, point, point + fr.Normal * faceY, fr.Dir);
        return new JoinSide(endW, false, Joined(fw, endW, point, cut));
    }

    /// <summary>Both cut along the corner's diagonal, from where the inner faces cross to where the outer faces cross.</summary>
    static WallJoin Mitre(Frame fa, Frame fb, Connection endA, Connection endB, Vec2 point, char relating)
    {
        // A face of one wall on the side the other comes in from is its inner face.
        double Inner(Frame f, Frame other, Connection otherEnd) => f.SideOf(-other.Outward(otherEnd)) > 0 ? f.YMax : f.YMin;
        double Outer(Frame f, Frame other, Connection otherEnd) => f.SideOf(-other.Outward(otherEnd)) > 0 ? f.YMin : f.YMax;
        static Vec2 Crossing(Vec2 p, Vec2 u, Vec2 q, Vec2 v) => p + u * (Vec2.Cross(q - p, v) / Vec2.Cross(u, v));
        var inner = Crossing(point + fa.Normal * Inner(fa, fb, endB), fa.Dir, point + fb.Normal * Inner(fb, fa, endA), fb.Dir);
        var outer = Crossing(point + fa.Normal * Outer(fa, fb, endB), fa.Dir, point + fb.Normal * Outer(fb, fa, endA), fb.Dir);
        var diagonal = outer - inner;
        if (diagonal.Length <= Eps) throw new CommandException("The mitre has no length.");
        JoinSide Side(Frame f, Connection end) => new(end, false, Joined(f, end, point, CutAlong(f, end, point, inner, diagonal)));
        return new WallJoin(JoinKind.L, JoinStyle.Mitre, point, relating, Side(fa, endA), Side(fb, endB));
    }

    static WallJoin InLine(Frame fa, Frame fb, double tolerance, JoinOptions options)
    {
        var reach = Math.Max(fa.Length, fb.Length);
        if (Math.Abs(Vec2.Cross(fb.Origin - fa.Origin, fa.Dir)) > Math.Max(1e-6 * reach, Eps))
            throw new CommandException("The walls are parallel but not in line: a join needs their axes to meet.");
        (Connection A, Connection B, double Gap)? best = null;
        foreach (var endA in new[] { Connection.AtStart, Connection.AtEnd })
            foreach (var endB in new[] { Connection.AtStart, Connection.AtEnd })
            {
                // End to end: the two bodies leave the joint in opposite directions.
                if (Vec2.Dot(fa.Outward(endA), fb.Outward(endB)) >= 0) continue;
                var gap = (fa.EndPoint(endA) - fb.EndPoint(endB)).Length;
                if (best == null || gap < best.Value.Gap) best = (endA, endB, gap);
            }
        var overlap = best == null || Vec2.Dot(fb.EndPoint(best.Value.B) - fa.EndPoint(best.Value.A), fa.Outward(best.Value.A)) < -tolerance;
        if (best == null || best.Value.Gap > tolerance || overlap)
            throw new CommandException($"The walls are in line but do not meet end to end within {tolerance:0.###} m.");
        var (ea, eb, _) = best.Value;
        var pa = fa.EndPoint(ea);
        var pb = fb.EndPoint(eb);
        var point = new Vec2((pa.X + pb.X) / 2, (pa.Y + pb.Y) / 2);
        return new WallJoin(JoinKind.Butt, JoinStyle.Butt, point, options.Priority ?? 'a',
            new JoinSide(ea, false, Joined(fa, ea, point, EndCut.Square)),
            new JoinSide(eb, false, Joined(fb, eb, point, EndCut.Square)));
    }
}
