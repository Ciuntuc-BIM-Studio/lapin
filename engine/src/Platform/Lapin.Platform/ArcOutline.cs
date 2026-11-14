using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A closed outline whose edges may be arcs (forum topic 97, stage 2): its corners (the element's <c>contour</c>) and, beside
/// them, the rise of each edge (<see cref="Key"/>, <c>arcs</c>): edge i runs from corner i to corner i + 1 (the last back to
/// the first) and rises that many metres off its chord at its middle, to the left of the way it runs when positive, as a
/// wall's <c>arc</c> does (<see cref="PathCurve"/>); 0 or none is straight. An outline with no arc keeps no <c>arcs</c>, so
/// what was saved before reads unchanged. Its arcs are laid in chords by the one rule (<see cref="Chords"/>, through
/// <see cref="PathCurve.Stations"/>) for the body, plans and sections; its area and perimeter are worked out exactly.
/// </summary>
public static class ArcOutline
{
    /// <summary>The attribute holding the edges' rises.</summary>
    public const string Key = "arcs";

    /// <summary>The rises an element's attribute holds, one per corner (missing ones straight); null when every edge is straight.</summary>
    public static double[]? Rises(JsonNode? node, int corners)
    {
        if (node is not JsonArray arr || corners == 0) return null;
        var o = new double[corners];
        var any = false;
        for (var i = 0; i < corners && i < arr.Count; i++)
        {
            var v = Json.Number(arr[i]) ?? 0;
            o[i] = double.IsFinite(v) && Math.Abs(v) > 1e-6 ? v : 0;
            any |= o[i] != 0;
        }
        return any ? o : null;
    }

    /// <summary>The rises as the attribute keeps them (rounded to a nanometre); null when every edge is straight.</summary>
    public static JsonArray? ToJson(IReadOnlyList<double>? rises) =>
        rises == null || rises.All(r => Math.Abs(r) <= 1e-6) ? null : new JsonArray([.. rises.Select(r => (JsonNode)Math.Round(Math.Abs(r) <= 1e-6 ? 0 : r, 9))]);

    /// <summary>Writes the rises into the attributes, or takes them away when every edge is straight.</summary>
    public static void Write(JsonObject attrs, IReadOnlyList<double>? rises)
    {
        if (ToJson(rises) is { } j) attrs[Key] = j; else attrs.Remove(Key);
    }

    public static bool HasArcs(IReadOnlyList<double>? rises) => rises != null && rises.Any(r => Math.Abs(r) > 1e-6);

    static double At(IReadOnlyList<double>? rises, int i) => rises != null && i < rises.Count ? rises[i] : 0;

    /// <summary>Edge i as an arc (or a straight line).</summary>
    public static PathCurve Edge(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, int i) =>
        new(corners[i], corners[(i + 1) % corners.Count], At(rises, i));

    /// <summary>
    /// The outline as points, each arc laid in chords no more than <see cref="Chords.Tolerance"/> off it; <paramref name="edgeOf"/>
    /// gets, for each edge of what comes out, the edge of the outline it lies on.
    /// </summary>
    public static List<Vec2> Points(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, List<int>? edgeOf = null)
    {
        var o = new List<Vec2>();
        var n = corners.Count;
        for (var i = 0; i < n; i++)
        {
            var c = Edge(corners, rises, i);
            if (!c.IsArc) { o.Add(corners[i]); edgeOf?.Add(i); continue; }
            var stations = c.Stations(0, c.Length);
            // Its start and the points along it; its end is the next edge's start.
            for (var k = 0; k + 1 < stations.Count; k++) { o.Add(k == 0 ? corners[i] : c.PointAt(stations[k])); edgeOf?.Add(i); }
        }
        return o;
    }

    /// <summary>The area the outline encloses, positive counter-clockwise, exactly: its corners' polygon and, for each arc, the circular segment off its chord.</summary>
    public static double SignedArea(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises)
    {
        var a = Polygon2.SignedArea(corners);
        if (rises == null) return a;
        for (var i = 0; i < corners.Count; i++)
        {
            var c = Edge(corners, rises, i);
            if (!c.IsArc) continue;
            var (r, t) = (c.Radius, c.Sweep);
            // A rise to the left takes the segment out of what the path encloses going round anticlockwise.
            a -= Math.Sign(c.Rise) * r * r / 2 * (t - Math.Sin(t));
        }
        return a;
    }

    /// <summary>The length round the outline, its arcs at their own length.</summary>
    public static double Perimeter(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises)
    {
        double p = 0;
        for (var i = 0; i < corners.Count; i++) p += Edge(corners, rises, i).Length;
        return p;
    }

    /// <summary>The rises of the outline run the other way round: each edge's own, the other side of it.</summary>
    public static double[]? Reversed(IReadOnlyList<double>? rises)
    {
        if (rises == null) return null;
        var n = rises.Count;
        // Corners reversed: edge j of the new outline (corner n-1-j to n-2-j) is edge n-2-j of the old, run back.
        return [.. Enumerable.Range(0, n).Select(j => -rises[((n - 2 - j) % n + n) % n])];
    }

    /// <summary>
    /// An outline as a command gives it: repeated corners dropped (the edge that leaves a corner keeps its arc), at least three
    /// corners, each rise a number not past ten times its chord, made counter-clockwise (its rises turned with it); refused
    /// when it encloses nothing. <paramref name="what"/> names it in a refusal ("A floor").
    /// </summary>
    public static (List<Vec2> Corners, double[]? Rises) Check(IReadOnlyList<Vec2> pts, IReadOnlyList<double>? rises, string what = "A floor")
    {
        var clean = new List<Vec2>();
        var r = new List<double>();
        for (var i = 0; i < pts.Count; i++)
        {
            var rise = At(rises, i);
            if (!double.IsFinite(rise)) throw new CommandException("An edge's arc must be a number.");
            if (clean.Count > 0 && (pts[i] - clean[^1]).Length <= 1e-6) { r[^1] = rise; continue; }
            clean.Add(pts[i]);
            r.Add(rise);
        }
        if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 1e-6) { clean.RemoveAt(clean.Count - 1); r.RemoveAt(r.Count - 1); }
        if (clean.Count < 3) throw new CommandException($"{what} needs at least three corners.");
        for (var i = 0; i < clean.Count; i++) r[i] = PathCurve.CheckRise(r[i], (clean[(i + 1) % clean.Count] - clean[i]).Length);
        double[]? kept = r.Any(x => x != 0) ? [.. r] : null;
        var area = SignedArea(clean, kept);
        if (Math.Abs(area) < 1e-6) throw new CommandException($"The outline of {Lower(what)} encloses nothing.");
        if (kept != null && !Simple(Points(clean, kept))) throw new CommandException("An arc makes the outline cross itself: a smaller rise.");
        if (area > 0) return (clean, kept);
        clean.Reverse();
        return (clean, Reversed(kept));
    }

    static string Lower(string what) => what.Length > 0 ? char.ToLowerInvariant(what[0]) + what[1..] : what;

    /// <summary>Whether a closed outline crosses itself nowhere (edges that are not neighbours do not touch).</summary>
    public static bool Simple(IReadOnlyList<Vec2> p)
    {
        var n = p.Count;
        static double O(Vec2 a, Vec2 b, Vec2 c) => Vec2.Cross(b - a, c - a);
        for (var i = 0; i < n; i++)
            for (var j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue;
                var (a, b, c, d) = (p[i], p[(i + 1) % n], p[j], p[(j + 1) % n]);
                var (d1, d2, d3, d4) = (O(c, d, a), O(c, d, b), O(a, b, c), O(a, b, d));
                if (((d1 > 1e-12 && d2 < -1e-12) || (d1 < -1e-12 && d2 > 1e-12)) && ((d3 > 1e-12 && d4 < -1e-12) || (d3 < -1e-12 && d4 > 1e-12))) return false;
            }
        return true;
    }

    /// <summary>The rise that makes the edge from <paramref name="a"/> to <paramref name="b"/> a tangent arc of <paramref name="sweep"/> radians turning left (positive) or right.</summary>
    static double RiseOf(Vec2 a, Vec2 b, double sweep) => -(b - a).Length / 2 * Math.Tan(sweep / 4);

    /// <summary>
    /// Corner <paramref name="i"/> rounded by an arc of <paramref name="radius"/> tangent to its two edges, which must be
    /// straight: the corner becomes two, where the arc meets each edge. Refused when an edge is curved or too short for it.
    /// </summary>
    public static (List<Vec2> Corners, double[] Rises) Fillet(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, int i, double radius)
    {
        if (!(radius > 1e-4) || !double.IsFinite(radius)) throw new CommandException("A fillet's radius is a length above nothing.");
        var (t1, t2, turn) = Cut(corners, rises, i, (u1, u2, half) => radius / Math.Tan(half));
        // The arc turns as the corner did, through the angle between the edges' directions.
        var o = Insert(corners, rises, i, t1, t2, RiseOf(t1, t2, turn));
        return o;
    }

    /// <summary>Corner <paramref name="i"/> cut straight across, <paramref name="distance"/> back along each of its (straight) edges.</summary>
    public static (List<Vec2> Corners, double[] Rises) Chamfer(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, int i, double distance)
    {
        if (!(distance > 1e-4) || !double.IsFinite(distance)) throw new CommandException("A chamfer's distance is a length above nothing.");
        var (t1, t2, _) = Cut(corners, rises, i, (_, _, _) => distance);
        return Insert(corners, rises, i, t1, t2, 0);
    }

    /// <summary>Where a corner's cut meets its edges, <paramref name="back"/> from the corner along each, and the angle the outline turns there (signed, left positive).</summary>
    static (Vec2 T1, Vec2 T2, double Turn) Cut(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, int i, Func<Vec2, Vec2, double, double> back)
    {
        var n = corners.Count;
        if (i < 0 || i >= n) throw new CommandException($"Which corner? The outline has {n}, from 0.");
        var prev = (i - 1 + n) % n;
        if (Math.Abs(At(rises, prev)) > 1e-6 || Math.Abs(At(rises, i)) > 1e-6) throw new CommandException("Only a corner between two straight edges can be rounded or cut.");
        var (p, c, q) = (corners[prev], corners[i], corners[(i + 1) % n]);
        var (l1, l2) = ((p - c).Length, (q - c).Length);
        if (l1 < 1e-6 || l2 < 1e-6) throw new CommandException("The corner has an edge of no length.");
        var (u1, u2) = ((p - c) * (1 / l1), (q - c) * (1 / l2));
        var between = Math.Acos(Math.Clamp(Vec2.Dot(u1, u2), -1, 1));
        if (between < 1e-4 || Math.PI - between < 1e-4) throw new CommandException("The corner is straight: there is nothing to round or cut.");
        var d = back(u1, u2, between / 2);
        if (d > l1 - 1e-6 || d > l2 - 1e-6) throw new CommandException($"Too large for the corner: its edges are {l1:0.###} m and {l2:0.###} m, and it needs {d:0.###} m along each.");
        var turn = Math.Sign(Vec2.Cross(c - p, q - c)) * (Math.PI - between);
        return (c + u1 * d, c + u2 * d, turn);
    }

    static (List<Vec2>, double[]) Insert(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, int i, Vec2 t1, Vec2 t2, double rise)
    {
        var pts = new List<Vec2>(corners);
        var r = Enumerable.Range(0, corners.Count).Select(k => At(rises, k)).ToList();
        pts[i] = t1;
        pts.Insert(i + 1, t2);
        // The edge from t1 to t2 is the cut; the one from t2 on is the corner's old edge out (straight).
        r[i] = rise;
        r.Insert(i + 1, 0);
        return (pts, [.. r]);
    }

    /// <summary>A circle as an outline: four corners a quarter turn apart, each edge a quarter of it (counter-clockwise).</summary>
    public static (List<Vec2> Corners, double[] Rises) Circle(Vec2 centre, double radius)
    {
        if (!(radius > 1e-4) || !double.IsFinite(radius)) throw new CommandException("A circle's radius is a length above nothing.");
        var pts = Enumerable.Range(0, 4).Select(k => centre + new Vec2(Math.Cos(k * Math.PI / 2), Math.Sin(k * Math.PI / 2)) * radius).ToList();
        // Bulging out of an anticlockwise outline: to the right of each edge.
        var rise = -radius * (1 - Math.Cos(Math.PI / 4));
        return (pts, [rise, rise, rise, rise]);
    }
}
