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
    public static void Write(JsonObject attrs, IReadOnlyList<double>? rises, string arcsKey = Key)
    {
        if (ToJson(rises) is { } j) attrs[arcsKey] = j; else attrs.Remove(arcsKey);
    }

    public static bool HasArcs(IReadOnlyList<double>? rises) => rises != null && rises.Any(r => Math.Abs(r) > 1e-6);

    /// <summary>
    /// The corners an element keeps under <paramref name="key"/> (a floor's, a room's, a platform's <c>contour</c>; a terrain's
    /// <c>parcel</c>), whatever its edges.
    /// </summary>
    public static List<Vec2> CornersOf(Entity e, string key = "contour") => Args.StoredPoints(e.Attrs[key]);

    /// <summary>The rises an element keeps under <paramref name="arcsKey"/> beside its corners under <paramref name="key"/>; null when every edge is straight.</summary>
    public static double[]? RisesOf(Entity e, string key = "contour", string arcsKey = Key) =>
        e.Attrs[arcsKey] is JsonArray ? Rises(e.Attrs[arcsKey], CornersOf(e, key).Count) : null;

    /// <summary>The outline an element keeps, as built: its corners, each arc laid in chords by the one rule (the corners alone when every edge is straight).</summary>
    public static IReadOnlyList<Vec2> BuiltOf(Entity e, string key = "contour", string arcsKey = Key)
    {
        var c = CornersOf(e, key);
        var el = key == "contour" ? EllipseEdge.Of(e) : null;
        return c.Count >= 3 && (RisesOf(e, key, arcsKey) is { } || el != null) ? Points(c, RisesOf(e, key, arcsKey), null, el) : c;
    }

    /// <summary>
    /// The rises a command gives (<paramref name="arcs"/>: one per corner given), else, when only the corners are given, the ones the
    /// element had (<paramref name="was"/>) while it has as many corners; null when every edge is straight (the element's own
    /// straight-outline check then applies, unchanged).
    /// </summary>
    public static double[]? RisesArg(JsonNode? arcs, int corners, double[]? was = null)
    {
        double[]? r = arcs is JsonArray arr ? [.. arr.Select(x => Json.Number(x) ?? 0)] : was != null && was.Length == corners ? was : null;
        return r != null && HasArcs(r) ? r : null;
    }

    /// <summary>The area an element's kept outline encloses, exactly (its arcs as arcs), unsigned.</summary>
    public static double AreaOf(Entity e, string key = "contour", string arcsKey = Key) => Math.Abs(SignedArea(CornersOf(e, key), RisesOf(e, key, arcsKey), key == "contour" ? EllipseEdge.Of(e) : null));

    /// <summary>The length round an element's kept outline, its arcs at their own length.</summary>
    public static double PerimeterOf(Entity e, string key = "contour", string arcsKey = Key) => Perimeter(CornersOf(e, key), RisesOf(e, key, arcsKey), key == "contour" ? EllipseEdge.Of(e) : null);

    static double At(IReadOnlyList<double>? rises, int i) => rises != null && i < rises.Count ? rises[i] : 0;

    /// <summary>Edge i as an arc (or a straight line).</summary>
    public static PathCurve Edge(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, int i) =>
        new(corners[i], corners[(i + 1) % corners.Count], At(rises, i));

    /// <summary>
    /// The outline as points, each arc laid in chords no more than <see cref="Chords.Tolerance"/> off it; <paramref name="edgeOf"/>
    /// gets, for each edge of what comes out, the edge of the outline it lies on.
    /// </summary>
    public static List<Vec2> Points(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, List<int>? edgeOf = null, IReadOnlyList<EllipseEdge?>? ellipses = null)
    {
        var o = new List<Vec2>();
        var n = corners.Count;
        for (var i = 0; i < n; i++)
        {
            // An edge along an ellipse (audit S4), laid by the one rule too.
            if (ellipses != null && i < ellipses.Count && ellipses[i] is { } el)
            {
                var along = el.Points(corners[i], corners[(i + 1) % n]);
                for (var k = 0; k + 1 < along.Count; k++) { o.Add(along[k]); edgeOf?.Add(i); }
                continue;
            }
            var c = Edge(corners, rises, i);
            if (!c.IsArc) { o.Add(corners[i]); edgeOf?.Add(i); continue; }
            var stations = c.Stations(0, c.Length);
            // Its start and the points along it; its end is the next edge's start.
            for (var k = 0; k + 1 < stations.Count; k++) { o.Add(k == 0 ? corners[i] : c.PointAt(stations[k])); edgeOf?.Add(i); }
        }
        return o;
    }

    /// <summary>The area the outline encloses, positive counter-clockwise, exactly: its corners' polygon and, for each arc, the circular segment off its chord.</summary>
    public static double SignedArea(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, IReadOnlyList<EllipseEdge?>? ellipses = null)
    {
        var a = Polygon2.SignedArea(corners);
        // An elliptical edge (audit S4): its own term in place of its chord's, exactly.
        if (ellipses != null)
            for (var i = 0; i < corners.Count && i < ellipses.Count; i++)
                if (ellipses[i] is { } el)
                {
                    var (p, q) = (corners[i], corners[(i + 1) % corners.Count]);
                    a += el.AreaTerm(p, q) - Vec2.Cross(p, q) / 2;
                }
        if (rises == null) return a;
        for (var i = 0; i < corners.Count; i++)
        {
            if (ellipses != null && i < ellipses.Count && ellipses[i] != null) continue;
            var c = Edge(corners, rises, i);
            if (!c.IsArc) continue;
            var (r, t) = (c.Radius, c.Sweep);
            // A rise to the left takes the segment out of what the path encloses going round anticlockwise.
            a -= Math.Sign(c.Rise) * r * r / 2 * (t - Math.Sin(t));
        }
        return a;
    }

    /// <summary>The length round the outline, its arcs at their own length.</summary>
    public static double Perimeter(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, IReadOnlyList<EllipseEdge?>? ellipses = null)
    {
        double p = 0;
        for (var i = 0; i < corners.Count; i++)
            p += ellipses != null && i < ellipses.Count && ellipses[i] is { } el ? el.Length(corners[i], corners[(i + 1) % corners.Count]) : Edge(corners, rises, i).Length;
        return p;
    }

    /// <summary>The rises of the outline run the other way round: each edge's own, the other side of it.</summary>
    public static double[]? Reversed(IReadOnlyList<double>? rises) => rises == null ? null : RunBack(rises, r => -r);

    /// <summary>
    /// The edge of an outline of <paramref name="n"/> corners that edge <paramref name="j"/> of it run the other way round was:
    /// corners reversed, edge j of the new outline (corner n-1-j to n-2-j) is edge n-2-j of the old, run back.
    /// </summary>
    public static int BackEdge(int n, int j) => ((n - 2 - j) % n + n) % n;

    /// <summary>Data kept by edge (one per corner) in the order of the outline run the other way round, each item turned by <paramref name="turn"/>.</summary>
    public static T[] RunBack<T>(IReadOnlyList<T> edges, Func<T, T>? turn = null)
    {
        var n = edges.Count;
        return [.. Enumerable.Range(0, n).Select(j => turn == null ? edges[BackEdge(n, j)] : turn(edges[BackEdge(n, j)]))];
    }

    /// <summary>
    /// An outline as a command gives it: repeated corners dropped (the edge that leaves a corner keeps its arc), at least three
    /// corners, each rise a number not past ten times its chord, made counter-clockwise (its rises turned with it); refused
    /// when it encloses nothing. <paramref name="what"/> names it in a refusal ("A floor"). Its elliptical edges
    /// (<paramref name="ellipses"/>, one per corner given) count in what it encloses and which way it runs, so a half ellipse
    /// on three corners in a line is an outline; the caller fits them to the corners checked (<see cref="EllipseEdge.Fit"/>).
    /// </summary>
    public static (List<Vec2> Corners, double[]? Rises) Check(IReadOnlyList<Vec2> pts, IReadOnlyList<double>? rises, string what = "A floor", IReadOnlyList<EllipseEdge?>? ellipses = null)
    {
        var clean = new List<Vec2>();
        var r = new List<double>();
        var el = new List<EllipseEdge?>();
        for (var i = 0; i < pts.Count; i++)
        {
            var rise = At(rises, i);
            var e = ellipses != null && i < ellipses.Count ? ellipses[i] : null;
            if (!double.IsFinite(rise)) throw new CommandException("An edge's arc must be a number.");
            if (clean.Count > 0 && (pts[i] - clean[^1]).Length <= 1e-6) { r[^1] = rise; el[^1] = e; continue; }
            clean.Add(pts[i]);
            r.Add(rise);
            el.Add(e);
        }
        if (clean.Count > 1 && (clean[0] - clean[^1]).Length <= 1e-6) { clean.RemoveAt(clean.Count - 1); r.RemoveAt(r.Count - 1); el.RemoveAt(el.Count - 1); }
        if (clean.Count < 3) throw new CommandException($"{what} needs at least three corners.");
        for (var i = 0; i < clean.Count; i++) r[i] = el[i] != null ? 0 : PathCurve.CheckRise(r[i], (clean[(i + 1) % clean.Count] - clean[i]).Length);
        double[]? kept = r.Any(x => x != 0) ? [.. r] : null;
        EllipseEdge?[]? curved = el.Any(x => x != null) ? [.. el] : null;
        var area = SignedArea(clean, kept, curved);
        if (Math.Abs(area) < 1e-6) throw new CommandException($"The outline of {Lower(what)} encloses nothing.");
        if ((kept != null || curved != null) && !Simple(Points(clean, kept, null, curved))) throw new CommandException("An arc makes the outline cross itself: a smaller rise.");
        if (area > 0) return (clean, kept);
        clean.Reverse();
        return (clean, Reversed(kept));
    }

    static string Lower(string what) => what.Length > 0 ? char.ToLowerInvariant(what[0]) + what[1..] : what;

    /// <summary>Whether a closed outline crosses itself nowhere (edges that are not neighbours do not touch).</summary>
    public static bool Simple(IReadOnlyList<Vec2> p)
    {
        var n = p.Count;
        for (var i = 0; i < n; i++)
            for (var j = i + 2; j < n; j++)
                if (!(i == 0 && j == n - 1) && Polyline2.Crosses(p[i], p[(i + 1) % n], p[j], p[(j + 1) % n])) return false;
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

    /// <summary>
    /// An edge offset: a straight one's ends moved across it; an arc's centre, its radius offset, its ends on that circle, the
    /// way it turns (+1 anticlockwise) and how far.
    /// </summary>
    readonly record struct Shifted(bool Arc, Vec2 Start, Vec2 End, Vec2 Centre = default, double Radius = 0, int Sense = 0, double Sweep = 0);

    /// <summary>
    /// Every edge of the outline moved <paramref name="distance"/> outward (inward when negative), as Archicad's "Offset all
    /// edges": a straight edge parallel to itself, an arc round its own centre (a concentric arc). Two straight edges meet
    /// again at a mitre; where an arc meets an edge, the two offset edges meet where they cross (an outline shrinking), else a
    /// round of the distance about the old corner joins them (an outline growing), as a true offset does: that corner becomes
    /// two. The outline keeps its corners' order and its way round; <c>From</c> gives, for each edge of what comes out, the
    /// edge it came from (a round: the edge before it), so data kept by edge (a roof's rules) follows. Refused when an edge
    /// would vanish or turn back, an arc shrink to nothing, or the outline cross itself or turn inside out.
    /// An edge along an ellipse (<paramref name="ellipses"/>, audit S4) has no ellipse at a distance from it: it is laid in chords
    /// within the one rule's tolerance of the curve at that distance (<see cref="Laid"/>), offset with the rest, and each corner
    /// between two of its chords put on that curve (the ellipse's point moved along its normal), so the new edges are its chords,
    /// straight, as Draft offsets an ellipse; inward past its tightest curvature (b²/a) its chords turn back, refused.
    /// </summary>
    public static (List<Vec2> Corners, double[]? Rises, List<int> From) Offset(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, double distance, IReadOnlyList<EllipseEdge?>? ellipses = null)
    {
        if (ellipses != null && ellipses.Any(x => x != null)) return OffsetElliptical(corners, rises, distance, ellipses);
        if (!double.IsFinite(distance)) throw new CommandException("An offset's distance must be a number.");
        var n = corners.Count;
        if (n < 3) throw new CommandException("An outline needs at least three corners.");
        var all = Enumerable.Range(0, n).ToList();
        if (Math.Abs(distance) < 1e-9) return ([.. corners], HasArcs(rises) ? [.. all.Select(k => At(rises, k))] : null, all);
        var area = SignedArea(corners, rises);
        if (Math.Abs(area) < 1e-9) throw new CommandException("The outline encloses nothing.");
        // To the left of each edge by y: outward is to the right of an anticlockwise outline, to the left of a clockwise one.
        var y = area > 0 ? -distance : distance;
        var d = Math.Abs(distance);
        var off = new Shifted[n];
        for (var i = 0; i < n; i++)
        {
            var c = Edge(corners, rises, i);
            if (c.Chord < 1e-9) throw new CommandException($"Edge {i + 1} has no length.");
            if (!c.IsArc) { var nrm = (c.End - c.Start).Normalized().Perp * y; off[i] = new(false, c.Start + nrm, c.End + nrm); continue; }
            var (centre, r) = (c.Centre, c.Radius);
            // A rise to the left has its centre to the right: going left takes the arc away from its centre.
            var r2 = r + y * Math.Sign(c.Rise);
            if (r2 < 1e-6) throw new CommandException($"Offsetting by {distance:0.###} m shrinks the arc of edge {i + 1} to nothing (its radius is {r:0.###} m): a smaller distance.");
            off[i] = new(true, centre + (c.Start - centre) * (r2 / r), centre + (c.End - centre) * (r2 / r), centre, r2, c.Rise > 0 ? -1 : 1, c.Sweep);
        }
        // Each corner's points: one where the offset edges meet, or two joined by a round.
        var at = new List<Vec2>[n];
        var round = new double[n];
        for (var i = 0; i < n; i++)
        {
            var (a, b) = (off[(i - 1 + n) % n], off[i]);
            var (q1, q2, p) = (a.End, b.Start, corners[i]);
            if ((q1 - q2).Length < 1e-9) { at[i] = [q1]; continue; }
            if (!a.Arc && !b.Arc)
            {
                var (u, v) = ((a.End - a.Start).Normalized(), (b.End - b.Start).Normalized());
                var den = Vec2.Cross(u, v);
                if (Math.Abs(den) < 1e-12)
                {
                    if (Vec2.Dot(u, v) < 0) throw new CommandException($"Corner {i + 1} turns back on itself: the outline cannot be offset there.");
                    at[i] = [(q1 + q2) * 0.5];
                    continue;
                }
                at[i] = [a.Start + u * (Vec2.Cross(b.Start - a.Start, v) / den)];
                continue;
            }
            // Where the offset edges cross within both (the outline shrinks there), the crossing nearest the corner; else a round.
            Vec2? best = null;
            foreach (var x in Crossings(a, b))
                if (Within(a, x) && Within(b, x) && (best == null || (x - p).Length < (best.Value - p).Length)) best = x;
            if (best is { } hit) { at[i] = [hit]; continue; }
            var m = (q1 + q2) * 0.5 - p;
            if (m.Length < 1e-9) throw new CommandException($"Corner {i + 1} turns back on itself: the outline cannot be offset there.");
            at[i] = [q1, q2];
            round[i] = PathCurve.RiseThrough(q1, q2, p + m.Normalized() * d);
        }
        var pts = new List<Vec2>();
        var rs = new List<double>();
        var from = new List<int>();
        for (var i = 0; i < n; i++)
        {
            if (at[i].Count == 2) { pts.Add(at[i][0]); rs.Add(round[i]); from.Add((i - 1 + n) % n); }
            var (s, e) = (at[i][^1], at[(i + 1) % n][0]);
            pts.Add(s);
            from.Add(i);
            var c = Edge(corners, rises, i);
            var gone = $"Offsetting by {distance:0.###} m makes edge {i + 1} vanish: a smaller distance.";
            if (!off[i].Arc)
            {
                var (now, was) = (e - s, c.End - c.Start);
                if (now.Length < 1e-4 || Vec2.Dot(now, was) <= 0) throw new CommandException(gone);
                rs.Add(0);
                continue;
            }
            var turned = Turn(off[i], s, e);
            if (turned < 1e-6 || turned > c.Sweep + (2 * Math.PI - c.Sweep) / 2 || (e - s).Length < 1e-4) throw new CommandException(gone);
            rs.Add(Math.Sign(c.Rise) * off[i].Radius * (1 - Math.Cos(turned / 2)));
        }
        double[]? kept = rs.Any(x => Math.Abs(x) > 1e-6) ? [.. rs.Select(x => Math.Abs(x) > 1e-6 ? x : 0)] : null;
        var after = SignedArea(pts, kept);
        if (Math.Sign(after) != Math.Sign(area) || Math.Abs(after) < 1e-6) throw new CommandException($"Offsetting by {distance:0.###} m leaves nothing of the outline: a smaller distance.");
        if (!Simple(Points(pts, kept))) throw new CommandException($"Offsetting by {distance:0.###} m makes the outline cross itself: a smaller distance.");
        return (pts, kept, from);
    }

    /// <summary>
    /// <see cref="Offset"/> of an outline with elliptical edges: those laid in chords and offset with the rest; then each corner
    /// beside an elliptical edge put where the offset lines meet when the ellipse's own tangent stands for its chord there: on the
    /// true curve at the distance between two of its chords (the ellipse's point moved along its normal), the true mitre where
    /// it meets a straight edge. Beside an arc the corner stays as offset (a round about the old corner, or where they cross).
    /// </summary>
    static (List<Vec2> Corners, double[]? Rises, List<int> From) OffsetElliptical(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, double distance, IReadOnlyList<EllipseEdge?> ellipses)
    {
        if (!double.IsFinite(distance)) throw new CommandException("An offset's distance must be a number.");
        var n = corners.Count;
        // Left of the way it runs by y (as Offset): outward is to the right of an anticlockwise outline.
        var y = SignedArea(corners, rises, ellipses) > 0 ? -distance : distance;
        var pts = new List<Vec2>();
        var rs = new List<double>();
        var edgeOf = new List<int>();
        // Each laid edge's ellipse (null for one that is not along an ellipse).
        var along = new List<EllipseEdge?>();
        for (var i = 0; i < n; i++)
        {
            if (i < ellipses.Count && ellipses[i] is { } el)
            {
                foreach (var q in Laid(el, corners[i], corners[(i + 1) % n], y)) { pts.Add(q); rs.Add(0); edgeOf.Add(i); along.Add(el); }
                continue;
            }
            pts.Add(corners[i]); rs.Add(At(rises, i)); edgeOf.Add(i); along.Add(null);
        }
        var laidRises = HasArcs(rs) ? rs : null;
        var (c, r, from) = Offset(pts, laidRises, distance);
        var (m, l) = (c.Count, pts.Count);
        // Laid edge j's offset line at corner p (one of its ends): through the point moved y to its left, along its way there.
        (Vec2 At, Vec2 Dir)? Line(int j, Vec2 p)
        {
            Vec2 dir;
            if (along[j] is { } e)
            {
                var t = e.Param(p);
                dir = ((e.M * -Math.Sin(t) + e.N * Math.Cos(t)) * (e.Ccw ? 1 : -1)).Normalized();
                return (e.At(t) + dir.Perp * y, dir);
            }
            if (Math.Abs(At(laidRises, j)) > 1e-6) return null;
            dir = (pts[(j + 1) % l] - pts[j]).Normalized();
            return (p + dir.Perp * y, dir);
        }
        for (var k = 0; k < m; k++)
        {
            var j = from[k];
            var before = (j - 1 + l) % l;
            // A round put in before it, or no ellipse beside it: as offset.
            if (from[(k - 1 + m) % m] != before || (along[j] == null && along[before] == null)) continue;
            if (Line(before, pts[j]) is not { } a || Line(j, pts[j]) is not { } b) continue;
            var den = Vec2.Cross(a.Dir, b.Dir);
            // Tangent there (along one ellipse, or an edge going on along its tangent): the point moved along the normal.
            c[k] = Math.Abs(den) < 1e-9 ? b.At : a.At + a.Dir * (Vec2.Cross(b.At - a.At, b.Dir) / den);
        }
        if (!Simple(Points(c, r))) throw new CommandException($"Offsetting by {distance:0.###} m makes the outline cross itself: a smaller distance.");
        return (c, r, [.. from.Select(k => edgeOf[k])]);
    }

    /// <summary>
    /// An elliptical edge's points for its offset by <paramref name="y"/> to its left, its end left out (the next edge's start): the
    /// one rule's chords (<see cref="Chords.Ellipse"/>), each halved until the offset curve's middle lies within the rule's
    /// tolerance of the chord between its ends' offsets (as Draft lays an ellipse's offset), the curve at a distance bending
    /// more tightly than the ellipse where it grows.
    /// </summary>
    static List<Vec2> Laid(EllipseEdge e, Vec2 a, Vec2 b, double y)
    {
        var (t0, sweep) = e.Span(a, b);
        Vec2 Off(double t)
        {
            var dir = ((e.M * -Math.Sin(t) + e.N * Math.Cos(t)) * (e.Ccw ? 1 : -1)).Normalized();
            return e.At(t) + dir.Perp * y;
        }
        var o = new List<Vec2> { a };
        void Lay(double ta, double tb, int depth)
        {
            var tm = (ta + tb) / 2;
            if (depth < 12 && Polyline2.SegmentDistance(Off(tm), Off(ta), Off(tb)) > Chords.Tolerance * 0.9)
            {
                Lay(ta, tm, depth + 1);
                Lay(tm, tb, depth + 1);
                return;
            }
            o.Add(e.At(tb));
        }
        var count = Chords.Ellipse(e.A, e.B, sweep, 2);
        for (var k = 0; k < count; k++) Lay(t0 + sweep * k / count, t0 + sweep * (k + 1) / count, 0);
        // Its end is the next edge's start.
        o.RemoveAt(o.Count - 1);
        return o;
    }

    static double Norm(double t) => t - 2 * Math.PI * Math.Floor(t / (2 * Math.PI));

    /// <summary>How far an offset arc turns, its own way, from <paramref name="s"/> to <paramref name="e"/> on its circle, in [0, 2π).</summary>
    static double Turn(Shifted a, Vec2 s, Vec2 e) =>
        Norm(a.Sense * (Math.Atan2(e.Y - a.Centre.Y, e.X - a.Centre.X) - Math.Atan2(s.Y - a.Centre.Y, s.X - a.Centre.X)));

    /// <summary>Whether a point of an offset edge's line or circle lies on the edge between its ends.</summary>
    static bool Within(Shifted e, Vec2 x)
    {
        if (e.Arc) { var t = Turn(e, e.Start, x); return t <= e.Sweep + 1e-9 || t >= 2 * Math.PI - 1e-9; }
        var v = e.End - e.Start;
        var k = Vec2.Dot(x - e.Start, v) / Vec2.Dot(v, v);
        return k >= -1e-9 && k <= 1 + 1e-9;
    }

    /// <summary>Where two offset edges' lines or circles cross (<see cref="PathEdits.Cross"/>).</summary>
    static IEnumerable<Vec2> Crossings(Shifted a, Shifted b)
    {
        static PathEdits.Curve Of(Shifted e) => e.Arc ? PathEdits.Curve.Round(e.Centre, e.Radius) : PathEdits.Curve.Line(e.Start, e.End);
        return PathEdits.Cross(Of(a), Of(b));
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
