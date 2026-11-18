using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// The building's outline on a storey (forum topic 158, the Floor tool's "Trace building"): the exterior face of the core of the
/// walls that make the building's main shape, leaving out finishes and insulation (the layers outside the core) and the walls that
/// enclose nothing (stubs, fences, free-standing walls).
/// <list type="number">
/// <item>The storey's walls make a plan graph: their axes, split where they meet. Where they meet is what their joins say (the joint
/// of each <see cref="Joins"/> relation: an end, or a point on a run), else where an end touches another wall (within its half
/// thickness) or two straight walls cross.</item>
/// <item>Bridges are taken out (a wall whose removal parts the graph: a stub, a fence, a free wall, a wall between two blocks),
/// which leaves the parts that enclose area.</item>
/// <item>Each part's faces are walked, always turning as far right as it can: the one face walked counter-clockwise is its outside.
/// The part round the point clicked is taken (the smallest), else the biggest.</item>
/// <item>Each wall on that outside is set off to the exterior face of its core (<see cref="Walls.Section"/>: the layers marked core;
/// the thickest when none is), an arc as a concentric arc; consecutive faces meet where their lines or circles cross.</item>
/// </list>
/// A free wall end within <see cref="GapReach"/> of another wall or end is a gap: closed in thought, and when the outside then runs
/// through it the trace is refused, naming it (no partial floor). Courtyards (an inner loop of exterior walls) are not told from
/// rooms, so they are not made holes.
/// </summary>
public static class BuildingTrace
{
    /// <summary>How far a free wall end may be from another wall (axis to axis) and still be a gap in the building's outline (m).</summary>
    public const double GapReach = 1.5;
    const double Tol = 0.01;

    /// <summary>The outline: its corners (counter-clockwise), each edge's rise (null when all are straight), and the walls traced.</summary>
    public sealed record Traced(List<Vec2> Corners, double[]? Rises, IReadOnlyList<string> Walls)
    {
        /// <summary>Its area, arcs included (m²).</summary>
        public double Area => Math.Abs(ArcOutline.SignedArea(Corners, Rises));
    }

    sealed class W(Entity e, PathCurve c, double lo, double hi, double half)
    {
        public readonly Entity E = e;
        public readonly PathCurve C = c;
        public readonly double CoreLo = lo, CoreHi = hi, Half = half;
        public double Length => C.Length;
    }

    /// <summary>A piece of the graph: a wall between two stations (or, wall −1, a gap closed in thought, straight).</summary>
    sealed record Edge(int Wall, double S0, double S1, int N0, int N1, Vec2 P0, Vec2 P1);

    /// <summary>The storey's building outline, or why there is none.</summary>
    public static (Traced? Outline, string? Problem) Of(IModelView m, IServices services, string storeyId, Vec2? at = null)
    {
        if (m.Get(storeyId) is not { Class: IfcClass.BuildingStorey }) return (null, "The storey is not in the project.");
        var walls = new List<W>();
        foreach (var e in m.OfClass(IfcClass.Wall))
        {
            if (Spatial.StoreyOf(m, e.Id)?.Id != storeyId || e.Attrs["start"] == null || e.Attrs["end"] == null) continue;
            var c = PathCurve.Of(e);
            if (c.Chord < 1e-3) continue;
            var bands = Walls.Section(m, e);
            if (bands.Count == 0) continue;
            var cores = bands.Where(b => b.Core && b.To - b.From > 1e-12).ToList();
            if (cores.Count == 0) cores = [bands.MaxBy(b => b.To - b.From)];
            var half = Math.Max(Math.Abs(bands.Min(b => b.From)), Math.Abs(bands.Max(b => b.To)));
            walls.Add(new W(e, c, cores.Min(b => b.From), cores.Max(b => b.To), half));
        }
        if (walls.Count == 0) return (null, "There are no walls on this storey to trace.");

        var graph = new Graph(walls);
        graph.Meet(m, services);
        var (edges, degree) = graph.Build([]);
        // Gaps: each free end's nearest other wall or free end within reach, closed in thought.
        var gaps = new List<(int Anchor, int Wall, double S, int OtherAnchor)>();
        var nodesOfWall = new Dictionary<int, HashSet<int>>();
        foreach (var e in edges)
        {
            if (!nodesOfWall.TryGetValue(e.Wall, out var set)) nodesOfWall[e.Wall] = set = [];
            set.Add(e.N0); set.Add(e.N1);
        }
        var free = edges.SelectMany(e => new[] { (e.N0, e.Wall, e.P0), (e.N1, e.Wall, e.P1) }).Where(x => degree[x.Item1] == 1).ToList();
        foreach (var (node, wi, p) in free)
        {
            double best = GapReach + 1e-9;
            (int Wall, double S, int Other)? pick = null;
            foreach (var (n2, w2, p2) in free)
                if (w2 != wi && n2 != node && (p2 - p).Length < best) { best = (p2 - p).Length; pick = (-1, 0, n2); }
            for (var o = 0; o < walls.Count; o++)
            {
                if (o == wi || !nodesOfWall.TryGetValue(o, out var theirs) || theirs.Overlaps(nodesOfWall[wi])) continue;
                var d = walls[o].C.Distance(p);
                if (d < best - 1e-9) { best = d; pick = (o, Math.Clamp(walls[o].C.Project(p), 0, walls[o].Length), -1); }
            }
            if (pick is { } g) gaps.Add((graph.AnchorOfNode(node), g.Wall, g.S, g.Other >= 0 ? graph.AnchorOfNode(g.Other) : -1));
        }
        var virtuals = new List<(int A, int B)>();
        foreach (var g in gaps)
            virtuals.Add((g.Anchor, g.OtherAnchor >= 0 ? g.OtherAnchor : graph.AddAnchor(g.Wall, g.S)));
        (edges, degree) = graph.Build(virtuals);

        var kept = Bridges.Without(edges, graph.NodeCount);
        if (kept.Count == 0)
            return (null, free.Count >= 2
                ? $"The walls do not close round the building: no closed loop of walls. Free wall ends at {string.Join(", ", free.Take(4).Select(x => Pt(x.Item3)))}."
                : "The walls do not close round any area.");

        // The parts that enclose area, each with its outside.
        var parts = new UnionFind(graph.NodeCount);
        foreach (var e in kept) parts.Union(e.N0, e.N1);
        var outsides = new List<(List<(Edge E, bool Fwd)> Face, List<Vec2> Poly, double Area)>();
        foreach (var group in kept.GroupBy(e => parts.Find(e.N0)))
        {
            var faces = Faces(group.ToList(), walls);
            var outer = faces.MaxBy(f => f.Area);
            if (outer.Area > 1e-6) outsides.Add(outer);
        }
        if (outsides.Count == 0) return (null, "The walls do not close round any area.");
        var chosen = at is { } q && outsides.Where(o => Polygon2.Contains(o.Poly, q)).OrderBy(o => o.Area).FirstOrDefault() is { Face: not null } hit
            ? hit : outsides.MaxBy(o => o.Area);
        if (chosen.Face.FirstOrDefault(h => h.E.Wall < 0) is { E: { } gap })
            return (null, $"The walls do not close round the building: there is a gap of {Math.Round((gap.P1 - gap.P0).Length, 3)} m between {Pt(gap.P0)} and {Pt(gap.P1)}. Close it, or draw the floor's outline.");

        var (corners, rises) = Offset(chosen.Face, walls);
        var ids = chosen.Face.Select(h => walls[h.E.Wall].E.Id).Distinct().ToList();
        if (corners.Count < 3) return (null, "The building's outline encloses nothing.");
        return (new Traced(corners, rises.Any(r => Math.Abs(r) > 1e-9) ? rises : null, ids), null);
    }

    static string Pt(Vec2 p) => $"({Math.Round(p.X, 3)}, {Math.Round(p.Y, 3)})";

    /// <summary>Every face of a part, walked turning as far right as it can (its outside counter-clockwise, rooms clockwise), with its outline sampled.</summary>
    static List<(List<(Edge E, bool Fwd)> Face, List<Vec2> Poly, double Area)> Faces(List<Edge> edges, List<W> walls)
    {
        // Half i*2 runs N0→N1, i*2+1 back.
        var outgoing = new Dictionary<int, List<(int Half, double Angle)>>();
        for (var i = 0; i < edges.Count; i++)
            for (var k = 0; k < 2; k++)
            {
                var (from, dir) = Leave(edges[i], k == 0, walls);
                if (!outgoing.TryGetValue(from, out var l)) outgoing[from] = l = [];
                l.Add((i * 2 + k, Math.Atan2(dir.Y, dir.X)));
            }
        foreach (var l in outgoing.Values) l.Sort((a, b) => a.Angle.CompareTo(b.Angle));
        var seen = new bool[edges.Count * 2];
        var faces = new List<(List<(Edge, bool)>, List<Vec2>, double)>();
        for (var h0 = 0; h0 < seen.Length; h0++)
        {
            if (seen[h0]) continue;
            var face = new List<(Edge, bool)>();
            var h = h0;
            while (!seen[h])
            {
                seen[h] = true;
                var e = edges[h / 2];
                var fwd = h % 2 == 0;
                face.Add((e, fwd));
                var at = fwd ? e.N1 : e.N0;
                var twin = h ^ 1;
                var l = outgoing[at];
                var idx = l.FindIndex(x => x.Half == twin);
                h = l[(idx + 1) % l.Count].Half;
            }
            var poly = new List<Vec2>();
            foreach (var (e, fwd) in face)
            {
                var pts = e.Wall < 0 ? new List<Vec2> { e.P0, e.P1 } : walls[e.Wall].C.Line(e.S0, e.S1, 0);
                if (!fwd) pts.Reverse();
                poly.AddRange(pts.Take(pts.Count - 1));
            }
            faces.Add((face, poly, poly.Count >= 3 ? Polygon2.SignedArea(poly) : 0));
        }
        return faces;
    }

    /// <summary>Where a half leaves from, and its direction there.</summary>
    static (int Node, Vec2 Dir) Leave(Edge e, bool fwd, List<W> walls)
    {
        if (e.Wall < 0) return fwd ? (e.N0, (e.P1 - e.P0).Normalized()) : (e.N1, (e.P0 - e.P1).Normalized());
        var c = walls[e.Wall].C;
        return fwd ? (e.N0, c.TangentAt(e.S0)) : (e.N1, c.TangentAt(e.S1) * -1);
    }

    /// <summary>A face of the core: a line (point, direction of travel) or a circle (centre, radius, sense of travel: +1 counter-clockwise).</summary>
    readonly record struct Face(Vec2 From, Vec2 To, Vec2 Dir, bool Arc, Vec2 Centre, double Radius, int Sense);

    /// <summary>The outside's walls each set off to its core's exterior face (on the right of the way round), joined corner to corner.</summary>
    static (List<Vec2> Corners, double[] Rises) Offset(List<(Edge E, bool Fwd)> outside, List<W> walls)
    {
        var faces = outside.Select(h =>
        {
            var w = walls[h.E.Wall];
            var y = h.Fwd ? w.CoreLo : w.CoreHi;
            var (s0, s1) = h.Fwd ? (h.E.S0, h.E.S1) : (h.E.S1, h.E.S0);
            var (a, b) = (w.C.At(s0, y), w.C.At(s1, y));
            if (!w.C.IsArc) return new Face(a, b, (b - a).Normalized(), false, default, 0, 0);
            var wallSense = w.C.Rise > 0 ? -1 : 1;
            return new Face(a, b, default, true, w.C.Centre, (a - w.C.Centre).Length, h.Fwd ? wallSense : -wallSense);
        }).ToList();
        // Each corner: where face i−1 meets face i (one point, or two when they do not meet: a step between faces in line).
        var n = faces.Count;
        var corners = new List<List<Vec2>>();
        for (var i = 0; i < n; i++)
        {
            var (f1, f2) = (faces[(i + n - 1) % n], faces[i]);
            var near = (f1.To + f2.From) * 0.5;
            corners.Add(Meet(f1, f2, near) is { } p ? [p] : (f1.To - f2.From).Length < 1e-6 ? [near] : [f1.To, f2.From]);
        }
        var pts = new List<Vec2>();
        var rises = new List<double>();
        for (var i = 0; i < n; i++)
        {
            var c = corners[i];
            for (var k = 0; k < c.Count; k++)
            {
                pts.Add(c[k]);
                if (k < c.Count - 1) { rises.Add(0); continue; }
                var next = corners[(i + 1) % n][0];
                rises.Add(faces[i].Arc ? Rise(faces[i], c[k], next) : 0);
            }
        }
        return Clean(pts, rises);
    }

    /// <summary>The rise of the arc from p to q round the face's circle the way it is travelled (left of p → q positive).</summary>
    static double Rise(Face f, Vec2 p, Vec2 q)
    {
        var chord = q - p;
        if (chord.Length < 1e-9) return 0;
        double A(Vec2 v) => Math.Atan2(v.Y - f.Centre.Y, v.X - f.Centre.X);
        var sweep = f.Sense * (A(q) - A(p));
        sweep -= 2 * Math.PI * Math.Floor(sweep / (2 * Math.PI));
        var mid = A(p) + f.Sense * sweep / 2;
        var m = f.Centre + new Vec2(Math.Cos(mid), Math.Sin(mid)) * f.Radius;
        return Vec2.Dot(m - (p + q) * 0.5, chord.Normalized().Perp);
    }

    /// <summary>Where two faces meet nearest <paramref name="near"/>; null when they do not (parallel lines, the same circle, circles apart).</summary>
    static Vec2? Meet(Face a, Face b, Vec2 near)
    {
        var hits = new List<Vec2>();
        if (!a.Arc && !b.Arc)
        {
            var sin = Vec2.Cross(a.Dir, b.Dir);
            if (Math.Abs(sin) < 1e-6) return null;
            hits.Add(a.From + a.Dir * (Vec2.Cross(b.From - a.From, b.Dir) / sin));
        }
        else if (a.Arc && b.Arc)
        {
            var d = b.Centre - a.Centre;
            var dist = d.Length;
            if (dist < 1e-9 || dist > a.Radius + b.Radius + 1e-9 || dist < Math.Abs(a.Radius - b.Radius) - 1e-9) return null;
            var x = (dist * dist + a.Radius * a.Radius - b.Radius * b.Radius) / (2 * dist);
            var h = Math.Sqrt(Math.Max(0, a.Radius * a.Radius - x * x));
            var u = d * (1 / dist);
            hits.Add(a.Centre + u * x + u.Perp * h);
            hits.Add(a.Centre + u * x - u.Perp * h);
        }
        else
        {
            var (line, circle) = a.Arc ? (b, a) : (a, b);
            var f = line.From - circle.Centre;
            var t0 = -Vec2.Dot(f, line.Dir);
            var disc = t0 * t0 - (f.Length * f.Length - circle.Radius * circle.Radius);
            if (disc < -1e-9) return null;
            var r = Math.Sqrt(Math.Max(0, disc));
            hits.Add(line.From + line.Dir * (t0 + r));
            hits.Add(line.From + line.Dir * (t0 - r));
        }
        return hits.MinBy(p => (p - near).Length);
    }

    /// <summary>Corners on top of each other merged, and a corner between two straight edges in line dropped.</summary>
    static (List<Vec2>, double[]) Clean(List<Vec2> pts, List<double> rises)
    {
        bool changed = true;
        while (changed && pts.Count > 3)
        {
            changed = false;
            for (var i = 0; i < pts.Count && pts.Count > 3; i++)
            {
                var j = (i + 1) % pts.Count;
                if ((pts[j] - pts[i]).Length < 1e-6)
                {
                    // Edge i is nothing: corner j goes, edge i takes edge j's rise.
                    rises[i] = rises[j];
                    pts.RemoveAt(j); rises.RemoveAt(j);
                    changed = true; break;
                }
                var h = (i + pts.Count - 1) % pts.Count;
                if (Math.Abs(rises[h]) < 1e-9 && Math.Abs(rises[i]) < 1e-9)
                {
                    var (u, v) = (pts[i] - pts[h], pts[j] - pts[i]);
                    if (u.Length > 1e-9 && v.Length > 1e-9 && Math.Abs(Vec2.Cross(u.Normalized(), v.Normalized())) < 1e-9 && Vec2.Dot(u, v) > 0)
                    {
                        pts.RemoveAt(i); rises.RemoveAt(i);
                        changed = true; break;
                    }
                }
            }
        }
        return (pts, rises.ToArray());
    }

    /// <summary>The walls' axes with the points they meet at: anchors (a wall and a station on it) merged into nodes.</summary>
    sealed class Graph(List<W> walls)
    {
        readonly List<(int Wall, double S)> _anchors = [];
        readonly UnionFind _uf = new(0);
        int[] _nodeOf = [];
        int[] _anchorOfNode = [];
        public int NodeCount { get; private set; }

        public int AddAnchor(int wall, double s)
        {
            _anchors.Add((wall, s));
            _uf.Add();
            return _anchors.Count - 1;
        }

        Vec2 Pos(int a) => walls[_anchors[a].Wall].C.PointAt(_anchors[a].S);

        public int AnchorOfNode(int node) => _anchorOfNode[node];

        /// <summary>Where the walls meet: their joins, else ends touching another wall, else straight walls crossing.</summary>
        public void Meet(IModelView m, IServices services)
        {
            var index = new Dictionary<string, int>();
            for (var i = 0; i < walls.Count; i++)
            {
                index[walls[i].E.Id] = i;
                AddAnchor(i, 0);
                AddAnchor(i, walls[i].Length);
            }
            int End(int w, int end) => w * 2 + end;
            int On(int w, Vec2 p) => AddAnchor(w, Math.Clamp(walls[w].C.Project(p), 0, walls[w].Length));
            if (services.Get<IPathElements>() is { } paths)
                foreach (var w in walls)
                    foreach (var r in Joins.Of(m, w.E.Id))
                    {
                        if (r.Relating != w.E.Id || r.Related.Count == 0 || !index.TryGetValue(r.Relating, out var a) || !index.TryGetValue(r.Related[0], out var b)) continue;
                        if (Joins.Compute(m, r, paths, out _) is not { } j) continue;
                        int Side(int wi, JoinSide side) => side.Connection switch { Connection.AtStart => End(wi, 0), Connection.AtEnd => End(wi, 1), _ => On(wi, j.Point) };
                        _uf.Union(Side(a, j.A), Side(b, j.B));
                    }
            // Ends touching another wall (within its half thickness) that no join ties.
            for (var i = 0; i < walls.Count; i++)
                for (var end = 0; end < 2; end++)
                {
                    var p = Pos(End(i, end));
                    for (var o = 0; o < walls.Count; o++)
                    {
                        if (o == i) continue;
                        var reach = Math.Max(Tol, walls[o].Half);
                        if (walls[o].C.Distance(p) > reach) continue;
                        var s = Math.Clamp(walls[o].C.Project(p), 0, walls[o].Length);
                        _uf.Union(End(i, end), s <= reach ? End(o, 0) : s >= walls[o].Length - reach ? End(o, 1) : AddAnchor(o, s));
                    }
                }
            // Straight walls crossing each other's runs.
            for (var i = 0; i < walls.Count; i++)
                for (var o = i + 1; o < walls.Count; o++)
                {
                    var (a, b) = (walls[i].C, walls[o].C);
                    if (a.IsArc || b.IsArc) continue;
                    var (da, db) = (a.End - a.Start, b.End - b.Start);
                    var den = Vec2.Cross(da, db);
                    if (Math.Abs(den) < 1e-12) continue;
                    var t = Vec2.Cross(b.Start - a.Start, db) / den;
                    var u = Vec2.Cross(b.Start - a.Start, da) / den;
                    var (ta, tb) = (Tol / da.Length, Tol / db.Length);
                    if (t <= ta || t >= 1 - ta || u <= tb || u >= 1 - tb) continue;
                    _uf.Union(AddAnchor(i, t * a.Length), AddAnchor(o, u * b.Length));
                }
        }

        /// <summary>The graph's edges (with <paramref name="virtuals"/>, gaps closed in thought, straight between two anchors) and each node's degree.</summary>
        public (List<Edge> Edges, int[] Degree) Build(List<(int A, int B)> virtuals)
        {
            // Anchors of one wall at (nearly) one station are one point.
            foreach (var g in Enumerable.Range(0, _anchors.Count).GroupBy(a => _anchors[a].Wall))
            {
                var sorted = g.OrderBy(a => _anchors[a].S).ToList();
                for (var k = 1; k < sorted.Count; k++)
                    if (_anchors[sorted[k]].S - _anchors[sorted[k - 1]].S < 1e-4) _uf.Union(sorted[k], sorted[k - 1]);
            }
            var roots = new Dictionary<int, int>();
            _nodeOf = new int[_anchors.Count];
            var firstAnchor = new List<int>();
            for (var a = 0; a < _anchors.Count; a++)
            {
                var r = _uf.Find(a);
                if (!roots.TryGetValue(r, out var node)) { roots[r] = node = roots.Count; firstAnchor.Add(a); }
                _nodeOf[a] = node;
            }
            _anchorOfNode = [.. firstAnchor];
            NodeCount = roots.Count;
            var edges = new List<Edge>();
            foreach (var g in Enumerable.Range(0, _anchors.Count).GroupBy(a => _anchors[a].Wall))
            {
                var sorted = g.OrderBy(a => _anchors[a].S).ToList();
                for (var k = 1; k < sorted.Count; k++)
                {
                    var (a, b) = (sorted[k - 1], sorted[k]);
                    if (_nodeOf[a] == _nodeOf[b]) continue;
                    edges.Add(new Edge(g.Key, _anchors[a].S, _anchors[b].S, _nodeOf[a], _nodeOf[b], Pos(a), Pos(b)));
                }
            }
            var closed = new HashSet<(int, int)>();
            foreach (var (a, b) in virtuals)
                if (_nodeOf[a] != _nodeOf[b] && closed.Add((Math.Min(_nodeOf[a], _nodeOf[b]), Math.Max(_nodeOf[a], _nodeOf[b])))) edges.Add(new Edge(-1, 0, 0, _nodeOf[a], _nodeOf[b], Pos(a), Pos(b)));
            var degree = new int[NodeCount];
            foreach (var e in edges) { degree[e.N0]++; degree[e.N1]++; }
            return (edges, degree);
        }
    }

    /// <summary>Bridges of a graph (Tarjan): the edges whose removal parts it; what is left are the parts that enclose area.</summary>
    static class Bridges
    {
        public static List<Edge> Without(List<Edge> edges, int nodes)
        {
            var adj = new List<(int To, int Edge)>[nodes];
            for (var i = 0; i < nodes; i++) adj[i] = [];
            for (var i = 0; i < edges.Count; i++) { adj[edges[i].N0].Add((edges[i].N1, i)); adj[edges[i].N1].Add((edges[i].N0, i)); }
            var (tin, low) = (new int[nodes], new int[nodes]);
            Array.Fill(tin, -1);
            var bridge = new bool[edges.Count];
            var timer = 0;
            for (var s = 0; s < nodes; s++)
            {
                if (tin[s] >= 0) continue;
                // Iterative DFS: (node, the edge it was reached by, next neighbour to look at).
                var stack = new Stack<(int V, int ByEdge, int Next)>();
                tin[s] = low[s] = timer++;
                stack.Push((s, -1, 0));
                while (stack.Count > 0)
                {
                    var (v, by, next) = stack.Pop();
                    if (next < adj[v].Count)
                    {
                        stack.Push((v, by, next + 1));
                        var (to, e) = adj[v][next];
                        if (e == by) continue;
                        if (tin[to] >= 0) low[v] = Math.Min(low[v], tin[to]);
                        else { tin[to] = low[to] = timer++; stack.Push((to, e, 0)); }
                    }
                    else if (by >= 0)
                    {
                        var parent = edges[by].N0 == v ? edges[by].N1 : edges[by].N0;
                        low[parent] = Math.Min(low[parent], low[v]);
                        if (low[v] > tin[parent]) bridge[by] = true;
                    }
                }
            }
            return edges.Where((_, i) => !bridge[i]).ToList();
        }
    }

    sealed class UnionFind
    {
        readonly List<int> _p = [];
        public UnionFind(int n) { for (var i = 0; i < n; i++) _p.Add(i); }
        public int Count => _p.Count;
        /// <summary>One more element, on its own.</summary>
        public void Add() => _p.Add(_p.Count);
        public int Find(int x) { while (_p[x] != x) x = _p[x] = _p[_p[x]]; return x; }
        public void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) _p[a] = b; }
    }
}
