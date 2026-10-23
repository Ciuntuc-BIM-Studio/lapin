using BubbleBim.Geometry;

namespace Lapin.Platform;

/// <summary>
/// What a join leaves at one end of a wall: how far the body goes on past the
/// axis end at the left face and at the right face (negative: it stops before
/// the end). The same value on both faces is a square end.
/// </summary>
public readonly record struct EndCut(double Left, double Right)
{
    public static readonly EndCut Square = new(0, 0);
}

/// <summary>
/// A straight wall reduced to what a join needs: its axis in plan and the
/// band its body covers across the axis, in the axis frame (Y grows to the
/// left of Start → End), plus whatever cuts its ends already carry.
/// </summary>
public sealed record JoinWall(Vec2 Start, Vec2 End, double YMin, double YMax, EndCut? StartCut = null, EndCut? EndCut = null)
{
    public double Thickness => YMax - YMin;

    /// <summary>A body <paramref name="thickness"/> wide placed on its axis by <paramref name="alignment"/> (centre, left face or right face on the axis) and pushed <paramref name="offset"/> to the left.</summary>
    public static JoinWall FromThickness(Vec2 start, Vec2 end, double thickness, string alignment = "center", double offset = 0)
    {
        var (lo, hi) = alignment switch
        {
            "left" => (-thickness, 0d),
            "right" => (0d, thickness),
            _ => (-thickness / 2, thickness / 2),
        };
        return new JoinWall(start, end, lo + offset, hi + offset);
    }
}

public enum JoinKind { L, T, Butt }
public enum JoinStyle { Butt, Mitre }
public enum Connection { AtStart, AtEnd, AtPath }

/// <summary>One wall's part in a join: where the join takes it, whether it is the one running through, and the wall as the join leaves it.</summary>
public sealed record JoinSide(Connection Connection, bool RunsThrough, JoinWall Wall);

/// <summary>A join: its kind and style, the joint, which wall relates (runs through; the first one in an in-line join), and both walls as the join leaves them.</summary>
public sealed record WallJoin(JoinKind Kind, JoinStyle Style, Vec2 Point, char Relating, JoinSide A, JoinSide B);

/// <param name="Tolerance">How far an axis end may be from the joint and still count as being there. Default: twice the thicker wall.</param>
/// <param name="Priority">Which wall runs through a corner and relates an in-line join: a or b. Default: the thicker, a on a tie. The crossed wall of a T always runs through.</param>
/// <param name="Style">Corners only: butt (one wall through) or mitre (both on the diagonal). A T and an in-line join are always butt.</param>
public sealed record JoinOptions(double? Tolerance = null, char? Priority = null, JoinStyle Style = JoinStyle.Butt);

/// <summary>A wall body's plan outline in its own frame (origin at Start, X along the axis, Y to the left): right-start, right-end, left-end, left-start.</summary>
public sealed record WallOutline(Vec2[] Corners, bool Rectangular, double Length);

/// <summary>
/// Joins two straight walls in plan (docs/transfer.md, "Îmbinări de pereți").
/// Every join comes down to the same move: a joined end has its axis tip put
/// on the joint and its body cut by one straight line of the plan.
/// <list type="bullet">
/// <item><b>Corner (L)</b>, both axes ending at the joint. Butt: the wall with
/// priority is cut by the other's outer face, the other by the first one's
/// near face. Mitre: both are cut by the diagonal from the inner corner to the
/// outer corner.</item>
/// <item><b>T</b>, one axis ending on the other's run. The crossed wall is left
/// as it is; the ending wall is cut by the crossed wall's face on its side.</item>
/// <item><b>In line (butt)</b>, the axes on one line, end to end. Both are cut
/// square at the point halfway between the two tips.</item>
/// </list>
/// Cuts are kept as the reach of each face past the tip, so a square cut keeps
/// the body a rectangle and a slanted one makes it a four-point polygon. No
/// solid booleans anywhere.
/// </summary>
public static class WallJoins
{
    /// <summary>Lengths below this are nothing.</summary>
    const double Tiny = 1e-9;
    /// <summary>Axes whose directions differ by less than this sine are treated as parallel.</summary>
    const double ParallelSin = 1e-5;

    /// <summary>A straight line of the plan: a point on it and its unit direction.</summary>
    readonly record struct Line(Vec2 Point, Vec2 Dir)
    {
        /// <summary>Where this line meets another; null when they run parallel.</summary>
        public Vec2? Meet(Line other)
        {
            var sin = Vec2.Cross(Dir, other.Dir);
            if (Math.Abs(sin) < Tiny) return null;
            return Point + Dir * (Vec2.Cross(other.Point - Point, other.Dir) / sin);
        }
    }

    /// <summary>A wall laid out in the plan: its tips, its direction and the lines of its faces.</summary>
    sealed class Laid
    {
        public readonly JoinWall Wall;
        public readonly Vec2 Along;
        public readonly double Length;

        public Laid(JoinWall wall)
        {
            Wall = wall;
            var run = wall.End - wall.Start;
            Length = run.Length;
            Along = run.Normalized();
        }

        public Vec2 Left => Along.Perp;
        public Line Axis => new(Wall.Start, Along);
        public Vec2 Tip(Connection end) => end == Connection.AtStart ? Wall.Start : Wall.End;
        /// <summary>The tip at the other end.</summary>
        public Vec2 FarTip(Connection end) => end == Connection.AtStart ? Wall.End : Wall.Start;
        /// <summary>How far a point is to the left of the axis (negative: to the right).</summary>
        public double Lateral(Vec2 p) => Vec2.Dot(p - Wall.Start, Left);
        /// <summary>The line of the face at lateral position <paramref name="y"/>.</summary>
        public Line Face(double y) => new(Wall.Start + Left * y, Along);
        /// <summary>The face on the same side of the axis as <paramref name="p"/>.</summary>
        public Line FaceNear(Vec2 p) => Face(Lateral(p) >= 0 ? Wall.YMax : Wall.YMin);
        /// <summary>The face on the other side of the axis from <paramref name="p"/>.</summary>
        public Line FaceAwayFrom(Vec2 p) => Face(Lateral(p) >= 0 ? Wall.YMin : Wall.YMax);

        /// <summary>
        /// Where the joint <paramref name="x"/>, a point of the axis line, falls
        /// on this wall: at a tip (within the tolerance; the nearer one when
        /// both qualify), on the run between the tips, or nowhere (null).
        /// </summary>
        public Connection? Reach(Vec2 x, double tolerance)
        {
            var toStart = (x - Wall.Start).Length;
            var toEnd = (x - Wall.End).Length;
            if (Math.Min(toStart, toEnd) <= tolerance) return toStart <= toEnd ? Connection.AtStart : Connection.AtEnd;
            var s = Vec2.Dot(x - Wall.Start, Along);
            return s > 0 && s < Length ? Connection.AtPath : null;
        }

        /// <summary>
        /// This wall with the tip at <paramref name="end"/> put on <paramref name="joint"/>
        /// and its body there cut by <paramref name="cutter"/>: each face is
        /// followed from the new tip to the cutter, and the distance it covers,
        /// outward positive, is that face's reach.
        /// </summary>
        public JoinWall CutBy(Connection end, Vec2 joint, Line cutter)
        {
            var outward = end == Connection.AtEnd ? Along : -Along;
            double Reach(double y)
            {
                var face = new Line(joint + Left * y, Along);
                var hit = face.Meet(cutter) ?? throw new CommandException("The cut runs along the wall; it cannot end it.");
                return Vec2.Dot(hit - face.Point, outward);
            }
            return Moved(end, joint, new EndCut(Reach(Wall.YMax), Reach(Wall.YMin)));
        }

        /// <summary>This wall with the tip at <paramref name="end"/> on <paramref name="joint"/> and the given cut there.</summary>
        public JoinWall Moved(Connection end, Vec2 joint, EndCut cut)
        {
            var moved = end == Connection.AtStart
                ? Wall with { Start = joint, StartCut = cut }
                : Wall with { End = joint, EndCut = cut };
            Outline(moved);
            return moved;
        }
    }

    static void Validate(JoinWall w, string label)
    {
        var name = label.Length == 0 ? "The wall" : $"Wall {label}";
        double[] numbers = [w.Start.X, w.Start.Y, w.End.X, w.End.Y, w.YMin, w.YMax,
            w.StartCut?.Left ?? 0, w.StartCut?.Right ?? 0, w.EndCut?.Left ?? 0, w.EndCut?.Right ?? 0];
        if (numbers.Any(n => !double.IsFinite(n))) throw new CommandException($"{name} has a non-finite coordinate or size.");
        if (!(w.Thickness > 0)) throw new CommandException($"{name} needs a positive thickness.");
        if ((w.End - w.Start).Length <= Tiny) throw new CommandException($"{name} needs two distinct axis points.");
    }

    /// <summary>The body's outline from its axis and cuts; refused when a face would have no length left (the wall is too short for its joins).</summary>
    public static WallOutline Outline(JoinWall w)
    {
        Validate(w, "");
        var length = (w.End - w.Start).Length;
        var start = w.StartCut ?? EndCut.Square;
        var end = w.EndCut ?? EndCut.Square;
        var rightFace = length + start.Right + end.Right;
        var leftFace = length + start.Left + end.Left;
        if (rightFace <= Tiny || leftFace <= Tiny) throw new CommandException("The wall is too short for this join: its cut ends would cross.");
        Vec2[] corners =
        [
            new(-start.Right, w.YMin),
            new(length + end.Right, w.YMin),
            new(length + end.Left, w.YMax),
            new(-start.Left, w.YMax),
        ];
        var square = Math.Abs(start.Left - start.Right) <= Tiny && Math.Abs(end.Left - end.Right) <= Tiny;
        return new WallOutline(corners, square, length);
    }

    public static WallJoin Compute(JoinWall a, JoinWall b, JoinOptions? options = null)
    {
        options ??= new JoinOptions();
        Validate(a, "a");
        Validate(b, "b");
        var tolerance = options.Tolerance ?? 2 * Math.Max(a.Thickness, b.Thickness);
        if (!(tolerance >= 0) || double.IsInfinity(tolerance)) throw new CommandException("The join tolerance must be a finite length, zero or more.");
        if (options.Priority is { } p && p != 'a' && p != 'b') throw new CommandException("The join priority names wall a or wall b.");

        var la = new Laid(a);
        var lb = new Laid(b);
        if (Math.Abs(Vec2.Cross(la.Along, lb.Along)) <= ParallelSin) return EndToEnd(la, lb, tolerance, options);

        var joint = la.Axis.Meet(lb.Axis)!.Value;
        var onA = la.Reach(joint, tolerance);
        var onB = lb.Reach(joint, tolerance);
        if (onA == null || onB == null)
            throw new CommandException($"The walls do not meet: where their axes would cross is more than {tolerance:0.###} m from an end.");
        if (onA == Connection.AtPath && onB == Connection.AtPath)
            throw new CommandException("The walls cross each other; split one at the crossing before joining them.");

        var firstIsA = onA == Connection.AtPath || (onB != Connection.AtPath && (options.Priority ?? (b.Thickness > a.Thickness ? 'b' : 'a')) == 'a');
        var (first, second, onFirst, onSecond) = firstIsA ? (la, lb, onA.Value, onB.Value) : (lb, la, onB.Value, onA.Value);
        var relating = firstIsA ? 'a' : 'b';

        JoinSide sideFirst, sideSecond;
        JoinKind kind;
        var style = JoinStyle.Butt;
        if (onFirst == Connection.AtPath)
        {
            // A T: the crossed wall is untouched; the other ends on the face it arrives at.
            kind = JoinKind.T;
            sideFirst = new JoinSide(Connection.AtPath, true, first.Wall);
            sideSecond = new JoinSide(onSecond, false, second.CutBy(onSecond, joint, first.FaceNear(second.FarTip(onSecond))));
        }
        else if (options.Style == JoinStyle.Mitre)
        {
            // A mitred corner: the inner faces cross at the inner corner, the outer faces at the outer corner; both walls end on the line between them.
            kind = JoinKind.L;
            style = JoinStyle.Mitre;
            var farFirst = first.FarTip(onFirst);
            var farSecond = second.FarTip(onSecond);
            var inner = first.FaceNear(farSecond).Meet(second.FaceNear(farFirst));
            var outer = first.FaceAwayFrom(farSecond).Meet(second.FaceAwayFrom(farFirst));
            if (inner == null || outer == null || (outer.Value - inner.Value).Length <= Tiny) throw new CommandException("The corner has no diagonal to mitre on.");
            var diagonal = new Line(inner.Value, (outer.Value - inner.Value).Normalized());
            sideFirst = new JoinSide(onFirst, false, first.CutBy(onFirst, joint, diagonal));
            sideSecond = new JoinSide(onSecond, false, second.CutBy(onSecond, joint, diagonal));
        }
        else
        {
            // A butt corner: the first wall runs on to the second's outer face; the second ends on the first's near face.
            kind = JoinKind.L;
            var farFirst = first.FarTip(onFirst);
            var farSecond = second.FarTip(onSecond);
            sideFirst = new JoinSide(onFirst, true, first.CutBy(onFirst, joint, second.FaceAwayFrom(farFirst)));
            sideSecond = new JoinSide(onSecond, false, second.CutBy(onSecond, joint, first.FaceNear(farSecond)));
        }
        return firstIsA
            ? new WallJoin(kind, style, joint, relating, sideFirst, sideSecond)
            : new WallJoin(kind, style, joint, relating, sideSecond, sideFirst);
    }

    /// <summary>
    /// Two walls on one line: as intervals along a's axis, b lies before a or
    /// after it; the space between the facing tips (negative when they overlap)
    /// must be within the tolerance; both then stop square at its middle.
    /// </summary>
    static WallJoin EndToEnd(Laid la, Laid lb, double tolerance, JoinOptions options)
    {
        var sStart = Vec2.Dot(lb.Wall.Start - la.Wall.Start, la.Along);
        var sEnd = Vec2.Dot(lb.Wall.End - la.Wall.Start, la.Along);
        var bLow = Math.Min(sStart, sEnd);
        var bHigh = Math.Max(sStart, sEnd);
        var bAfterA = (bLow + bHigh) / 2 >= la.Length / 2;
        var space = bAfterA ? bLow - la.Length : -bHigh;
        var endA = bAfterA ? Connection.AtEnd : Connection.AtStart;
        var bStartsLow = sStart <= sEnd;
        var endB = bAfterA == bStartsLow ? Connection.AtStart : Connection.AtEnd;
        var tipA = la.Tip(endA);
        var tipB = lb.Tip(endB);

        var reach = Math.Max(la.Length, lb.Length);
        if (Math.Abs(la.Lateral(tipB)) > Tiny + ParallelSin * reach)
            throw new CommandException("The walls are parallel but not in line: a join needs their axes to meet.");
        if (space > tolerance || space < -tolerance)
            throw new CommandException($"The walls are in line but their ends are not within {tolerance:0.###} m of each other.");

        var joint = (tipA + tipB) * 0.5;
        return new WallJoin(JoinKind.Butt, JoinStyle.Butt, joint, options.Priority ?? 'a',
            new JoinSide(endA, false, la.Moved(endA, joint, EndCut.Square)),
            new JoinSide(endB, false, lb.Moved(endB, joint, EndCut.Square)));
    }
}
