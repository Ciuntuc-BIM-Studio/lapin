using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// A roof's curved edges as its members and lines see them (decision 741). A curved edge is laid in chords by the one rule
/// (<see cref="Chords"/>, through <see cref="ArcOutline"/>), and the solver gives every chord a plane of its own: the facets
/// of a cone. What stands on the edge follows the curve, not the chords: the creases between facets of one curve (and where
/// a curve runs on tangentially into a straight edge, as a fillet does) are no hips or valleys; the wall plate along a
/// curved edge and the purlins over it are each one member along the curve; the rafters stand at the spacing along the
/// curve, not two per chord.
/// </summary>
public static class RoofCurves
{
    /// <summary>For each edge of the solved (counter-clockwise) outline: its logical edge (the roof's own corner it starts at) and whether that edge is curved; null when the roof has no curved edge.</summary>
    public static (int[] Logical, bool[] Curved)? Edges(Roof.Resolved r, int n)
    {
        if (r.EdgeOf == null || Slabs.Rises(r.Roof) is not { } rises || r.Outline.Count != n) return null;
        var reversed = n >= 3 && Polygon2.SignedArea(r.Outline) < 0;
        var logical = new int[n];
        var curved = new bool[n];
        for (var j = 0; j < n; j++)
        {
            // Orient reverses a clockwise outline: its edge j is the given outline's edge n − 2 − j.
            var given = reversed ? (2 * n - 2 - j) % n : j;
            logical[j] = r.LogicalEdge(given);
            curved[j] = logical[j] >= 0 && logical[j] < rises.Length && Math.Abs(rises[logical[j]]) > 1e-6;
        }
        return (logical, curved);
    }

    /// <summary>
    /// The runs of the solved outline's edges that lay one curved edge in chords, each in order (counter-clockwise), with
    /// the run's points (its first edge's start to its last edge's end). Edges of no curve are in none.
    /// </summary>
    public static List<List<int>> Chains(Roof.Resolved r, RoofShape s)
    {
        var n = s.Outline.Count;
        var o = new List<List<int>>();
        if (Edges(r, n) is not var (logical, curved)) return o;
        bool Same(int j, int k) => curved[j] && curved[k] && logical[j] == logical[k];
        // Start where a chain begins: an edge whose previous one is not of its curve (a closed round outline of one curve: 0).
        var start = Enumerable.Range(0, n).FirstOrDefault(j => !Same((j + n - 1) % n, j));
        for (var c = 0; c < n; c++)
        {
            var j = (start + c) % n;
            if (!curved[j]) continue;
            if (o.Count > 0 && Same(o[^1][^1], j)) o[^1].Add(j);
            else o.Add([j]);
        }
        return o;
    }

    /// <summary>
    /// The runs the rafters are spaced along: a curved edge's chords, with the edges it runs on into smoothly (turning less
    /// than <see cref="Drainage.SmoothTurn"/>: a fillet's straight edges), each in order; closed when it goes all round.
    /// </summary>
    public static List<(List<int> Edges, bool Closed)> SmoothRuns(Roof.Resolved r, RoofShape s)
    {
        var n = s.Outline.Count;
        var o = new List<(List<int>, bool)>();
        if (Edges(r, n) is not var (_, curved) || !curved.Any(c => c)) return o;
        var cos = Math.Cos(Drainage.SmoothTurn * Math.PI / 180);
        Vec2 Dir(int j) => (s.Outline[(j + 1) % n] - s.Outline[j]).Normalized();
        // The joint at corner j, from edge j − 1 into edge j.
        bool Smooth(int j) { var i = (j + n - 1) % n; return (curved[i] || curved[j]) && Vec2.Dot(Dir(i), Dir(j)) >= cos; }
        var breaks = Enumerable.Range(0, n).Where(j => !Smooth(j)).ToList();
        if (breaks.Count == 0) return [([.. Enumerable.Range(0, n)], true)];
        var run = new List<int>();
        for (var c = 0; c < n; c++)
        {
            var j = (breaks[0] + c) % n;
            if (!Smooth(j) && run.Count > 0) { if (run.Any(k => curved[k])) o.Add((run, false)); run = []; }
            run.Add(j);
        }
        if (run.Any(k => curved[k])) o.Add((run, false));
        return o;
    }

    /// <summary>The chain's edge of each solved outline edge (−1 when it is straight).</summary>
    public static int[] ChainOf(Roof.Resolved r, RoofShape s, List<List<int>> chains)
    {
        var o = Enumerable.Repeat(-1, s.Outline.Count).ToArray();
        for (var c = 0; c < chains.Count; c++) foreach (var j in chains[c]) o[j] = c;
        return o;
    }

    /// <summary>
    /// The shape less the creases between the facets of a curve: a hip or valley line between the planes of two edges next
    /// to each other, one of them a chord of a curved edge, turning less than <see cref="Drainage.SmoothTurn"/> (a curve's
    /// chords, a fillet meeting its straight edges). The shape itself when the roof has no curved edge.
    /// </summary>
    public static RoofShape WithoutCreases(Roof.Resolved r, RoofShape s)
    {
        var n = s.Outline.Count;
        if (Edges(r, n) is not var (_, curved) || !curved.Any(c => c)) return s;
        var cos = Math.Cos(Drainage.SmoothTurn * Math.PI / 180);
        Vec2 Dir(int j) => (s.Outline[(j + 1) % n] - s.Outline[j]).Normalized();
        bool Crease(RoofLine l)
        {
            if (l.Kind is not (LineKind.Hip or LineKind.Valley)) return false;
            var owners = s.Planes.Where(p => Has(p.Points, l.A, l.B)).Select(p => p.Edge).Distinct().ToList();
            if (owners.Count != 2) return false;
            var (j, k) = (owners[0], owners[1]);
            var next = (j + 1) % n == k || (k + 1) % n == j;
            return next && (curved[j] || curved[k]) && Vec2.Dot(Dir(j), Dir(k)) >= cos;
        }
        var lines = s.Lines.Where(l => !Crease(l)).ToList();
        return lines.Count == s.Lines.Count ? s : s with { Lines = lines };
    }

    /// <summary>Whether a plane's loop has a and b as two of its corners next to each other.</summary>
    static bool Has(IReadOnlyList<Vec3> loop, Vec3 a, Vec3 b)
    {
        for (var i = 0; i < loop.Count; i++)
        {
            var (p, q) = (loop[i], loop[(i + 1) % loop.Count]);
            if ((p - a).Length < 1e-6 && (q - b).Length < 1e-6 || (p - b).Length < 1e-6 && (q - a).Length < 1e-6) return true;
        }
        return false;
    }

    /// <summary>
    /// Pieces (a purlin's, one per facet; a curved hip's, one per facet it meets) joined end to end into lines: pieces whose
    /// ends meet (within a millimetre), turning there by no more than <paramref name="maxTurn"/> degrees, become one
    /// polyline, in order.
    /// </summary>
    public static List<List<Vec3>> Join(IEnumerable<(Vec3 A, Vec3 B)> pieces, double maxTurn = 180)
    {
        var lines = pieces.Where(p => (p.B - p.A).Length > 1e-9).Select(p => new List<Vec3> { p.A, p.B }).ToList();
        var cos = Math.Cos(maxTurn * Math.PI / 180) - 1e-9;
        // a's end runs on into b's start.
        bool Smooth(List<Vec3> a, List<Vec3> b) => Vec3.Dot((a[^1] - a[^2]).Normalized(), (b[1] - b[0]).Normalized()) >= cos;
        var merged = true;
        while (merged)
        {
            merged = false;
            for (var i = 0; i < lines.Count && !merged; i++)
                for (var j = 0; j < lines.Count && !merged; j++)
                {
                    if (i == j) continue;
                    var (a, b) = (lines[i], lines[j]);
                    if ((a[^1] - b[0]).Length < 1e-3 && Smooth(a, b)) { }
                    else if ((a[^1] - b[^1]).Length < 1e-3 && Smooth(a, Enumerable.Reverse(b).ToList())) b.Reverse();
                    else if ((a[0] - b[0]).Length < 1e-3 && Smooth(Enumerable.Reverse(a).ToList(), b)) a.Reverse();
                    else continue;
                    a.AddRange(b.Skip(1));
                    lines.RemoveAt(j);
                    merged = true;
                }
        }
        return lines;
    }

    /// <summary>
    /// A member along a polyline: level (a curved wall plate or purlin) its plan band, mitred at every chord, w wide and d
    /// deep about the line; sloping (a curved hip) its pieces' boxes as one body.
    /// </summary>
    public static Solid? PathBody(IReadOnlyList<Vec3> path, double w, double d, string? tag)
    {
        if (path.Any(p => Math.Abs(p.Z - path[0].Z) > 1e-6))
        {
            var boxes = path.Zip(path.Skip(1), (a, b) => Prism.SweepBox(a, b, w, d, tag)).OfType<Solid>().ToList();
            return boxes.Count == 0 ? null : Openings.Combine(boxes, tag ?? "");
        }
        return LevelBody(path, w, d, tag);
    }

    /// <summary>A level member along a polyline (a curved wall plate or purlin): its plan band, mitred at every chord, w wide and d deep about the line.</summary>
    public static Solid? LevelBody(IReadOnlyList<Vec3> path, double w, double d, string? tag)
    {
        var plan = Polyline2.Dedupe(path.Select(p => p.Xy).ToList());
        if (plan.Count < 2) return null;
        var left = Polyline2.Offset(plan, w / 2);
        var right = Polyline2.Offset(plan, -w / 2);
        right.Reverse();
        var band = left.Concat(right).ToList();
        if (Polygon2.SignedArea(band) < 0) band.Reverse();
        return Prism.ExtrudeFootprint(band, path[0].Z - d / 2, d, tag);
    }
}
