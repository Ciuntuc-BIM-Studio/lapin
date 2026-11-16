using Lapin.Geometry;

namespace Lapin.Drafting;

/// <summary>
/// What stands on a view that a dimension's moved value keeps clear of (forum topic 78): the values of every dimension
/// that stay where their style puts them (they fit, or the user moved them), their opening heights, the dimension lines
/// (hard: a value never goes over them) and the extension lines of other dimensions (soft: avoided when a place is free
/// of them). Filled for a whole view before any value is moved, then each value placed goes in as it is placed, so values
/// of different strings (an exterior or a room dimension's strings, any dimension of the view) stay clear of one another.
/// </summary>
public sealed class TextRoom
{
    public enum Kind { Text, Line, Extension }

    /// <summary>A rectangle: its centre, its unit direction along and up, its half width and half height (plan metres).</summary>
    public readonly record struct Box(string Owner, Kind What, Vec2 C, Vec2 R, Vec2 Up, double Hw, double Hh)
    {
        public Box Grown(double by) => this with { Hw = Hw + by, Hh = Hh + by };

        /// <summary>Whether two rectangles overlap (separating axes).</summary>
        public bool Overlaps(in Box o)
        {
            foreach (var ax in new[] { R, Up, o.R, o.Up })
            {
                var d = Math.Abs(Vec2.Dot(o.C - C, ax));
                var ra = Hw * Math.Abs(Vec2.Dot(R, ax)) + Hh * Math.Abs(Vec2.Dot(Up, ax));
                var rb = o.Hw * Math.Abs(Vec2.Dot(o.R, ax)) + o.Hh * Math.Abs(Vec2.Dot(o.Up, ax));
                if (d >= ra + rb) return false;
            }
            return true;
        }

        /// <summary>The point of the rectangle nearest <paramref name="p"/>.</summary>
        public Vec2 Nearest(Vec2 p)
        {
            var d = p - C;
            return C + R * Math.Clamp(Vec2.Dot(d, R), -Hw, Hw) + Up * Math.Clamp(Vec2.Dot(d, Up), -Hh, Hh);
        }

        public static Box Segment(string owner, Kind what, Vec2 a, Vec2 b)
        {
            var l = (b - a).Length;
            var r = l < 1e-12 ? new Vec2(1, 0) : (b - a) * (1 / l);
            return new Box(owner, what, (a + b) * 0.5, r, r.Perp, l / 2, 0);
        }
    }

    public readonly List<Box> Boxes = [];

    /// <summary>
    /// How badly a box would sit: 10 for every value or dimension line it overlaps, 1 for every other dimension's extension
    /// line; a dimension's own lines do not count against its values.
    /// </summary>
    public int Score(in Box b)
    {
        var s = 0;
        foreach (var o in Boxes)
        {
            if (o.What != Kind.Text && o.Owner == b.Owner) continue;
            if (!b.Overlaps(o)) continue;
            s += o.What == Kind.Extension ? 1 : 10;
        }
        return s;
    }

    /// <summary>How many other dimensions' lines <paramref name="path"/> crosses.</summary>
    public int Crossings(in Box path)
    {
        var n = 0;
        foreach (var o in Boxes) if (o.What == Kind.Line && o.Owner != path.Owner && path.Overlaps(o)) n++;
        return n;
    }
}

/// <summary>
/// Where a string's values go when they do not fit (forum topic 78): a value wider than its segment (between its witness
/// lines, with a little room) moves just outside the segment on the free side (the string's end, or a neighbour wide
/// enough to keep its own value where it is), else onto the rows above and below the line in turn (staggered), the first
/// place clear of everything on the view; a value the user moved keeps its place, and a value that fits never moves, so
/// a drawing whose values all fit is drawn exactly as before.
/// </summary>
public static class DimensionTextPlacement
{
    /// <summary>One segment's value as its style would place it.</summary>
    public sealed record Seg(int Index, Vec2 Mid, double Length, Vec2 R, Vec2 Up, Vec2 Centre, double W, double H, bool Fits, bool Moved);

    /// <summary>Room left either side of a value inside its segment, and around it for collisions (paper mm).</summary>
    public const double Pad = 0.4, Margin = 0.25;

    /// <summary>Whether a value of width <paramref name="w"/> fits a segment of <paramref name="length"/> (plan metres; mm: plan metres a paper mm).</summary>
    public static bool Fits(double w, double length, double mm) => w + 2 * Pad * mm <= length + 1e-12;

    /// <summary>The row shifts (plan metres up from the value's own place) in the order they are tried: further out first, then across the line, and so on.</summary>
    public static List<double> Rows(string placement, double h, double gap)
    {
        var step = h + gap;
        var o = new List<double>();
        for (var k = 1; k <= 3; k++)
            switch (placement)
            {
                case "below": o.Add(-k * step); o.Add(h + 2 * gap + (k - 1) * step); break;
                case "centered": o.Add(k * step); o.Add(-k * step); break;
                default: o.Add(k * step); o.Add(-(h + 2 * gap) - (k - 1) * step); break;
            }
        return o;
    }

    /// <summary>
    /// The values of <paramref name="segs"/> that must move, each moved (by how much, plan metres) to the best place in
    /// <paramref name="room"/>, which takes them in as placed. The values that stay are expected in the room already.
    /// </summary>
    public static Dictionary<int, Vec2> Place(string owner, IReadOnlyList<Seg> segs, TextRoom room, string placement, double gap, double mm)
    {
        var o = new Dictionary<int, Vec2>();
        // Along the line, in order; the values to move from the string's ends inward, so the ends take the outside places.
        var along = segs.OrderBy(s => Vec2.Dot(s.Mid, s.R)).ToList();
        var pos = along.Select((s, i) => (s.Index, i)).ToDictionary(p => p.Index, p => p.i);
        var moving = along.Where(s => !s.Fits && !s.Moved)
            .OrderBy(s => Math.Min(pos[s.Index], along.Count - 1 - pos[s.Index])).ThenBy(s => pos[s.Index]).ToList();
        foreach (var s in moving)
        {
            var i = pos[s.Index];
            var outside = s.Length / 2 + Pad * mm + s.W / 2;
            // A side is free when the string ends there, or its neighbour's value keeps its own place.
            bool Free(int side) => i + side < 0 || i + side >= along.Count || along[i + side].Fits || along[i + side].Moved;
            double Room(int side) => i + side < 0 || i + side >= along.Count ? double.PositiveInfinity : along[i + side].Length;
            var sides = new[] { 1, -1 }.OrderByDescending(Room).ToList();
            // Each place with what it costs beyond what it overlaps: off the middle of a row a little, a row further out more
            // (it reads less as its segment's, and a far row may lie over the model's own drawing).
            var candidates = new List<(Vec2 By, int Cost)>();
            foreach (var side in sides) if (Free(side)) candidates.Add((s.R * (side * outside), 0));
            var rows = Rows(placement, s.H, gap);
            for (var k = 0; k < rows.Count; k++)
            {
                var far = 3 * (k / 2);
                candidates.Add((s.Up * rows[k], far));
                foreach (var side in sides) candidates.Add((s.Up * rows[k] + s.R * (side * outside), far + 2));
            }
            var (best, score) = (candidates[0].By, int.MaxValue);
            foreach (var (c, cost) in candidates)
            {
                // Never across another dimension's line: a value stays on its own string's side of its neighbours.
                var sc = cost + room.Score(Box(owner, s, c).Grown(Margin * mm)) + 10 * room.Crossings(TextRoom.Box.Segment(owner, TextRoom.Kind.Text, s.Mid, s.Centre + c));
                if (sc < score) (best, score) = (c, sc);
                if (sc == 0) break;
            }
            o[s.Index] = best;
            room.Boxes.Add(Box(owner, s, best));
        }
        return o;
    }

    /// <summary>A value's rectangle, moved by <paramref name="by"/>.</summary>
    public static TextRoom.Box Box(string owner, Seg s, Vec2 by) => new(owner, TextRoom.Kind.Text, s.Centre + by, s.R, s.Up, s.W / 2, s.H / 2);
}
