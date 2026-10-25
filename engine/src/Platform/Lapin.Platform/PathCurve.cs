using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A path element's axis: straight from its start to its end, or an arc
/// through them that rises <see cref="Rise"/> metres off the chord at its
/// middle (to the left of start → end when positive, as the element's
/// <c>arc</c> attribute holds it). Distances <c>s</c> run along the axis from
/// the start; past either end the axis goes on straight, along its tangent
/// there (where joins reach). <c>y</c> runs across it, to the left.
/// </summary>
public readonly record struct PathCurve(Vec2 Start, Vec2 End, double Rise)
{
    /// <summary>The attribute holding the rise.</summary>
    public const string Key = "arc";

    /// <summary>A step along an arc, at most (about 5°): what its plan, body and IFC axis are drawn with.</summary>
    const double MaxStep = Math.PI / 36;

    public static PathCurve Of(Entity e) => new(Joins.Start(e), Joins.End(e), e.NumOrNull(Key) ?? 0);

    public double Chord => (End - Start).Length;
    public bool IsArc => Math.Abs(Rise) > 1e-6 && Chord > 1e-9;
    Vec2 ChordDir => (End - Start).Normalized();
    Vec2 Middle => (Start + End) * 0.5;

    public double Radius => IsArc ? (Chord * Chord / 4 + Rise * Rise) / (2 * Math.Abs(Rise)) : double.PositiveInfinity;
    public Vec2 Centre => Middle + ChordDir.Perp * (Rise - Math.Sign(Rise) * Radius);
    /// <summary>The angle the arc turns through, radians.</summary>
    public double Sweep => IsArc ? 4 * Math.Atan(2 * Math.Abs(Rise) / Chord) : 0;
    public double Length => IsArc ? Radius * Sweep : Chord;
    /// <summary>+1 counter-clockwise (an arc bulging to the right), −1 clockwise.</summary>
    int Sense => Rise > 0 ? -1 : 1;
    double StartAngle { get { var d = Start - Centre; return Math.Atan2(d.Y, d.X); } }

    /// <summary>The point <paramref name="s"/> along the axis (straight on past its ends).</summary>
    public Vec2 PointAt(double s)
    {
        if (!IsArc) return Start + ChordDir * s;
        if (s < 0) return Start + TangentAt(0) * s;
        var length = Length;
        if (s > length) return End + TangentAt(length) * (s - length);
        var a = StartAngle + Sense * s / Radius;
        return Centre + new Vec2(Math.Cos(a), Math.Sin(a)) * Radius;
    }

    /// <summary>The axis's direction at <paramref name="s"/> (the end's past the ends).</summary>
    public Vec2 TangentAt(double s)
    {
        if (!IsArc) return ChordDir;
        var a = StartAngle + Sense * Math.Clamp(s, 0, Length) / Radius;
        return new Vec2(-Math.Sin(a), Math.Cos(a)) * Sense;
    }

    /// <summary>The point <paramref name="s"/> along and <paramref name="y"/> to the left of the axis.</summary>
    public Vec2 At(double s, double y) => PointAt(s) + TangentAt(s).Perp * y;

    /// <summary>Where the point nearest <paramref name="p"/> lies along the axis (beyond the ends on their tangents).</summary>
    public double Project(Vec2 p)
    {
        if (!IsArc) return Vec2.Dot(p - Start, ChordDir);
        var length = Length;
        // Before the start or past the end, along the tangent there.
        var t0 = TangentAt(0);
        var before = Vec2.Dot(p - Start, t0);
        var t1 = TangentAt(length);
        var after = Vec2.Dot(p - End, t1);
        var d = p - Centre;
        // The angle from the start, the way the arc runs, in [0, 2π).
        var turned = Sense * (Math.Atan2(d.Y, d.X) - StartAngle);
        turned -= 2 * Math.PI * Math.Floor(turned / (2 * Math.PI));
        if (turned <= Sweep) return turned * Radius;
        // Off the arc: on the side of the end it is nearer to, along its tangent.
        return turned > Sweep + (2 * Math.PI - Sweep) / 2 ? Math.Min(before, 0) : length + Math.Max(after, 0);
    }

    /// <summary>How far <paramref name="p"/> is from the axis between its ends.</summary>
    public double Distance(Vec2 p) => (PointAt(Math.Clamp(Project(p), 0, Length)) - p).Length;

    /// <summary>The distances it is drawn at from <paramref name="s0"/> to <paramref name="s1"/>: the two only, straight; on an arc, steps of at most 5° (straight past the ends).</summary>
    public List<double> Stations(double s0, double s1)
    {
        if (!IsArc || s1 - s0 < 1e-12) return [s0, s1];
        var o = new List<double> { s0 };
        var (a, b) = (Math.Max(s0, 0), Math.Min(s1, Length));
        if (b > a)
        {
            var n = Math.Max(1, (int)Math.Ceiling((b - a) / Radius / MaxStep));
            for (var i = 0; i <= n; i++)
            {
                var s = a + (b - a) * i / n;
                if (s - o[^1] > 1e-9) o.Add(s);
            }
        }
        if (s1 - o[^1] > 1e-9) o.Add(s1);
        return o;
    }

    /// <summary>The line <paramref name="y"/> to the left of the axis from <paramref name="s0"/> to <paramref name="s1"/>.</summary>
    public List<Vec2> Line(double s0, double s1, double y) { var c = this; return Stations(s0, s1).Select(s => c.At(s, y)).ToList(); }

    /// <summary>
    /// The band between <paramref name="y0"/> and <paramref name="y1"/> across
    /// the axis, as a closed outline counter-clockwise: from <paramref name="from"/>
    /// to <paramref name="to"/> along it, each a function of y (an end cut
    /// slanting across the band).
    /// </summary>
    public List<Vec2> Band(Func<double, double> from, Func<double, double> to, double y0, double y1)
    {
        var o = Line(from(y0), to(y0), y0);
        var back = Line(from(y1), to(y1), y1);
        back.Reverse();
        o.AddRange(back);
        if (Polygon2.SignedArea(o) < 0) o.Reverse();
        return o;
    }

    /// <summary>
    /// Where the body ends along the axis, on each line across it (a function
    /// of y), as its joins leave it: a joint's cut is a straight line in plan
    /// through the joint (on the tangent there), slanting across the body by
    /// its cut; each line across meets it where it crosses. No joint: square at
    /// the axis's own end.
    /// </summary>
    public (Func<double, double> From, Func<double, double> To) Ends((Vec2 Point, EndCut Cut)? start, (Vec2 Point, EndCut Cut)? end, double yMin, double yMax)
    {
        var c = this;
        var length = Length;
        double At(EndCut cut, double y) => yMax - yMin < 1e-12 ? cut.Right : cut.Right + (cut.Left - cut.Right) * (y - yMin) / (yMax - yMin);
        Func<double, double> Side((Vec2 Point, EndCut Cut)? joint, double s, int sign)
        {
            if (joint is not { } j) return _ => s;
            var t = c.TangentAt(s);
            var along = s + Vec2.Dot(j.Point - c.PointAt(s), t);
            // The cut line, through its points on the two faces.
            var p1 = j.Point + t * (sign * At(j.Cut, yMin)) + t.Perp * yMin;
            var p2 = j.Point + t * (sign * At(j.Cut, yMax)) + t.Perp * yMax;
            var normal = (p2 - p1).Normalized().Perp;
            return y => c.Meet(along + sign * At(j.Cut, y), y, p1, normal);
        }
        return (Side(start, 0, -1), Side(end, length, 1));
    }

    /// <summary>Where the line <paramref name="y"/> to the left of the axis crosses the line through <paramref name="p"/> across <paramref name="normal"/>, near <paramref name="guess"/>.</summary>
    double Meet(double guess, double y, Vec2 p, Vec2 normal)
    {
        var s = guess;
        for (var i = 0; i < 30; i++)
        {
            var f = Vec2.Dot(At(s, y) - p, normal);
            if (Math.Abs(f) < 1e-12) break;
            const double h = 1e-6;
            var df = (Vec2.Dot(At(s + h, y) - p, normal) - f) / h;
            if (Math.Abs(df) < 1e-12) break;
            s -= f / df;
        }
        return double.IsFinite(s) ? s : guess;
    }

    /// <summary>The rise of the arc through <paramref name="a"/>, <paramref name="b"/> and <paramref name="p"/> (0: in line).</summary>
    public static double RiseThrough(Vec2 a, Vec2 b, Vec2 p)
    {
        var chord = b - a;
        var c = chord.Length;
        if (c < 1e-9) return 0;
        var left = chord.Normalized().Perp;
        var mid = (a + b) * 0.5;
        var side = Vec2.Dot(p - mid, left);
        if (Math.Abs(Vec2.Cross(chord, p - a)) < 1e-9 * c * Math.Max(1, (p - a).Length)) return 0;
        // The centre is on the chord's bisector, as far from p as from a.
        var along = Vec2.Dot(p - mid, chord.Normalized());
        var d = (along * along + side * side - c * c / 4) / (2 * side);
        var r = Math.Sqrt(c * c / 4 + d * d);
        return d + Math.Sign(side) * r;
    }

    /// <summary>A rise checked: a number not past ten times the chord.</summary>
    public static double CheckRise(double rise, double chord)
    {
        if (!double.IsFinite(rise)) throw new CommandException("The arc's rise must be a number.");
        if (Math.Abs(rise) > 10 * Math.Max(chord, 1e-3)) throw new CommandException("The arc rises too far for its ends.");
        return Math.Abs(rise) < 1e-6 ? 0 : rise;
    }
}
