using Lapin.Geometry;
using Lapin.Platform.Analysis;

namespace Lapin.Analysis.Solver;

/// <summary>A piece of a member (a bar of the analytical model) in the solver: its element, and the stretch of the member it spans (metres from its i end).</summary>
public sealed record MemberPiece(int Element, double X0, double X1);

/// <summary>
/// A shell element as the loads find it: its index in the solver's shells, its outline in plan (a slab's) or in its plane (a
/// wall's) anticlockwise (<see cref="Poly"/>, to clip by) and in its nodes' order (<see cref="ByNode"/>, to weigh them), its
/// area there and its box.
/// </summary>
public sealed record MeshedFace(int Shell, Vec2[] Poly, Vec2[] ByNode, double Area, Vec2 Min, Vec2 Max)
{
    public static MeshedFace Of(int shell, Vec2[] byNode)
    {
        var area = Polygon2.SignedArea(byNode);
        var poly = area < 0 ? byNode.Reverse().ToArray() : byNode;
        return new(shell, poly, byNode, Math.Abs(area), new Vec2(byNode.Min(q => q.X), byNode.Min(q => q.Y)), new Vec2(byNode.Max(q => q.X), byNode.Max(q => q.Y)));
    }
}

/// <summary>What the shells of an analysis came to: the surfaces, the elements and the nodes the mesh added.</summary>
public sealed record ShellSummary(int Surfaces, int Elements, int Quads, int Triangles, int Nodes, double MeshSize);

/// <summary>
/// S2b (docs/plan-fem.md §10.10): the slabs and walls of the analytical model as shells in the solver's problem. Which ones
/// (<see cref="ShellsTaken"/>): concrete slabs and walls with an E, as the project's options say (<see cref="AnalysisSettings"/>);
/// a slab of another material (a timber floor) keeps the 45° rule, a masonry wall stays a line support (§10.12). Their pieces
/// are meshed together (<see cref="ShellMesher"/>, the bars' nodes and the pieces' rings hard points; the target size the
/// options', smaller on a narrow piece: at least <see cref="AnalysisSettings.PerSide"/> elements across it); the bars along
/// the mesh's edges are cut at its nodes (<see cref="SplitBars"/>), their releases kept at their own ends, their span loads
/// shared by position, and each member keeps its pieces (<see cref="PieceMap"/>) so its stations and diagrams stay whole.
/// A shell wall's foot takes its footing's support, or a line support where nothing is under it; a line support (a masonry
/// wall) holds the mesh's nodes along it too. The loads on a meshed piece go on its elements: an area load as a uniform load
/// on each element it covers (its consistent shares), partly covered ones by the shape functions integrated over the part; a
/// line or point load by the shape functions of the element under it; wind on a façade on the shell walls in its plane.
/// </summary>
public sealed partial class StructuralProblem
{
    public AnalysisSettings Settings { get; init; } = new();
    /// <summary>The bars as members (the analytical model's pieces), before the shells' nodes cut them.</summary>
    public List<FrameElement> Members { get; } = [];
    /// <summary>Each member's pieces in the solver, in order from its i end.</summary>
    public List<List<MemberPiece>> PieceMap { get; } = [];
    public ShellSummary? ShellInfo { get; private set; }
    /// <summary>Each shell surface (<see cref="ShellElement.Group"/>): its element (GlobalId), name, kind (slab, wall) and normal.</summary>
    public Dictionary<string, (string SourceId, string Name, string Kind, Vec3 Normal)> Surfaces { get; } = [];

    readonly Dictionary<AShell, List<MeshedFace>> _faces = [];
    readonly HashSet<int> _shellNodes = [];
    readonly List<(int Shell, Vec3[] Pts, Vec3 Normal)> _wallFaces = [];
    List<int>?[] _paths = [];

    /// <summary>The slabs and walls analysed as shells, each with its material; what is left as in S1 is said.</summary>
    static Dictionary<AShell, StructuralMaterial> ShellsTaken(AnalyticalModel am, AnalysisSettings settings, Func<string?, StructuralMaterial?> materialOf, List<string> notes)
    {
        var o = new Dictionary<AShell, StructuralMaterial>();
        var (otherSlabs, masonry, otherWalls, noE) = (new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>());
        foreach (var s in am.Shells)
        {
            var wall = s.Source.Kind == "wall";
            if (wall ? !settings.Walls : !settings.Slabs) continue;
            var mat = materialOf(s.Source.MaterialId);
            if (mat is not { Kind: "concrete" })
            {
                (wall ? mat?.Kind == "masonry" ? masonry : otherWalls : otherSlabs).Add(s.Source.Name);
                continue;
            }
            if (mat.E == null) { noE.Add(s.Source.Name); continue; }
            o[s] = mat;
        }
        static string List(HashSet<string> xs) => string.Join(", ", xs.Take(6)) + (xs.Count > 6 ? "…" : "");
        if (!settings.Slabs && am.Shells.Any(s => s.Source.Kind == "slab")) notes.Add("The options leave the slabs out of the shells: their loads go to what holds them by the 45° rule (as in S1).");
        if (!settings.Walls && am.Shells.Any(s => s.Source.Kind == "wall")) notes.Add("The options leave the walls out of the shells: the load-bearing walls are line supports (as in S1).");
        if (otherSlabs.Count > 0) notes.Add($"Slabs not of concrete ({List(otherSlabs)}): not shells; their loads go to what holds them by the 45° rule.");
        if (masonry.Count > 0) notes.Add($"Masonry walls ({List(masonry)}): line supports until their values are given (CR 6).");
        if (otherWalls.Count > 0) notes.Add($"Walls not of concrete ({List(otherWalls)}): line supports.");
        if (noE.Count > 0) notes.Add($"No E for {List(noE)}: not shells (as in S1).");
        return o;
    }

    void MeshShells(Dictionary<AShell, StructuralMaterial> shellOf, Func<string?, StructuralMaterial?> materialOf)
    {
        var p = Problem;
        _paths = new List<int>?[p.Elements.Count];
        if (shellOf.Count == 0) return;
        var pieces = new List<MeshPiece>();
        var owners = new List<(AShell Shell, Vec3 Normal, Vec3? Axis, string Group)>();
        var refused = new List<string>();
        foreach (var s in Model.Shells)
        {
            if (!shellOf.ContainsKey(s)) continue;
            var ring = s.Nodes.Select(n => NodeIndex[n]).ToList();
            var pts = ring.Select(i => p.Nodes[i]).ToList();
            var normal = VecMath.NewellNormal(pts);
            if (normal.Length < 1e-9) { refused.Add(s.Id); continue; }
            normal = normal.Normalized();
            Vec3? axis = null;
            string group;
            if (s.Source.Kind == "wall")
            {
                // A wall's normal is horizontal, to the right of its line's direction, so its x runs along the line and its y up.
                var h = new Vec2(normal.X, normal.Y);
                if (h.Length < 0.7 || s.Source.Line is not { Count: >= 2 } line) { refused.Add(s.Id); continue; }
                h = h.Normalized();
                var seg = Enumerable.Range(0, line.Count - 1).Where(i => (line[i + 1] - line[i]).Length > 1e-9)
                    .MaxBy(i => Math.Abs(Vec2.Cross((line[i + 1] - line[i]).Normalized(), h)));
                var d = (line[seg + 1] - line[seg]).Normalized();
                normal = new Vec3(d.Y, -d.X, 0);
                axis = new Vec3(d.X, d.Y, 0);
                group = $"{s.Source.SourceId}:{seg}";
            }
            else
            {
                if (Math.Abs(normal.Z) < 1e-6) { refused.Add(s.Id); continue; }
                if (normal.Z < 0) normal = -normal;
                group = s.Source.SourceId;
            }
            // The corners (where the ring turns), the piece in its plane: convex, with an area, its narrowest width.
            var corners = new List<int>();
            for (var i = 0; i < ring.Count; i++)
            {
                var (a, b, c) = (pts[(i + ring.Count - 1) % ring.Count], pts[i], pts[(i + 1) % ring.Count]);
                var (d1, d2) = (b - a, c - b);
                if (d1.Length < 1e-9 || d2.Length < 1e-9) continue;
                if (Vec3.Cross(d1.Normalized(), d2.Normalized()).Length > 1e-4 || Vec3.Dot(d1, d2) < 0) corners.Add(i);
            }
            if (corners.Count < 3) { refused.Add(s.Id); continue; }
            var (ex, ey) = VecMath.PlaneBasis(normal);
            var plane = corners.Select(i => VecMath.Project(pts[i], ex, ey)).ToList();
            var area = Polygon2.SignedArea(plane);
            if (area < 0) { plane.Reverse(); area = -area; }
            var convex = Enumerable.Range(0, plane.Count).All(i => Vec2.Cross(plane[(i + 1) % plane.Count] - plane[i], plane[(i + 2) % plane.Count] - plane[(i + 1) % plane.Count]) >= -1e-9);
            if (area < 1e-6 || !convex) { refused.Add(s.Id); continue; }
            var width = double.PositiveInfinity;
            for (var i = 0; i < plane.Count; i++)
            {
                var (a, b) = (plane[i], plane[(i + 1) % plane.Count]);
                var len = (b - a).Length;
                if (len < 1e-9) continue;
                width = Math.Min(width, plane.Max(q => Math.Abs(Vec2.Cross((b - a) * (1 / len), q - a))));
            }
            var size = Math.Clamp(width / AnalysisSettings.PerSide, AnalysisSettings.MinMesh, Settings.MeshSize);
            pieces.Add(new MeshPiece(corners.Select(i => ring[i]).ToList(), group, normal, size));
            owners.Add((s, normal, axis, group));
        }
        if (refused.Count > 0)
            Notes.Add($"{refused.Count} shell piece{(refused.Count == 1 ? "" : "s")} with no area, not plane or not convex: left out of the mesh ({string.Join(", ", refused.Take(6))}{(refused.Count > 6 ? "…" : "")}).");
        if (pieces.Count == 0) return;

        var hard = new HashSet<int>();
        foreach (var e in p.Elements) { hard.Add(e.I); hard.Add(e.J); }
        foreach (var (s, _, _, _) in owners) foreach (var n in s.Nodes) hard.Add(NodeIndex[n]);
        ShellMesh mesh;
        try { mesh = ShellMesher.Mesh(p.Nodes, pieces, hard, Settings.MeshSize); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IndexOutOfRangeException)
        {
            Notes.Add($"The shells could not be meshed ({ex.Message}): the slabs and walls are taken as in S1.");
            return;
        }
        for (var i = mesh.Given; i < mesh.Nodes.Count; i++) p.AddNode(mesh.Nodes[i], $"m{i - mesh.Given + 1}");

        var dropped = 0;
        var count = new Dictionary<AShell, int>();
        foreach (var face in mesh.Faces)
        {
            var (s, normal, axis, group) = owners[face.Piece];
            var mat = shellOf[s];
            var pts = face.Nodes.Select(n => p.Nodes[n]).ToArray();
            Vec2[] poly;
            if (axis is { } ax) poly = pts.Select(q => new Vec2(q.X * ax.X + q.Y * ax.Y, q.Z)).ToArray();
            else poly = pts.Select(q => q.Xy).ToArray();
            var mf = MeshedFace.Of(p.Shells.Count, poly);
            if (mf.Area < 1e-10) { dropped++; continue; }
            var n = count[s] = count.GetValueOrDefault(s) + 1;
            p.Shells.Add(new ShellElement
            {
                Id = $"{s.Id}/{n}", Nodes = face.Nodes, E = mat.E!.Value / 1000, Nu = mat.Poisson?.Value ?? 0.2, Thickness = s.Source.Thickness,
                Axis = axis, Group = group, SourceId = s.Source.SourceId, Name = s.Source.Name,
            });
            var k = p.Shells.Count - 1;
            if (!_faces.TryGetValue(s, out var list)) _faces[s] = list = [];
            list.Add(mf);
            if (axis != null) _wallFaces.Add((k, pts, normal));
            foreach (var x in face.Nodes) _shellNodes.Add(x);
            Surfaces.TryAdd(group, (s.Source.SourceId, s.Source.Name, s.Source.Kind, normal));
        }
        if (dropped > 0) Notes.Add($"{dropped} mesh element{(dropped == 1 ? "" : "s")} with no area left out.");

        // The bars along the mesh's edges, through its nodes.
        var alongBars = new HashSet<int>();
        for (var k = 0; k < p.Elements.Count; k++)
        {
            var e = p.Elements[k];
            if (!_shellNodes.Contains(e.I) || !_shellNodes.Contains(e.J)) continue;
            var path = mesh.NodesAlong(e.I, e.J);
            if (path.Count <= 2) continue;
            _paths[k] = path;
            foreach (var x in path) alongBars.Add(x);
        }
        var barNodes = p.Elements.SelectMany(e => new[] { e.I, e.J }).Concat(alongBars).ToHashSet();

        // A line support (a wall that is not a shell) holds the mesh's nodes along it as it holds its own.
        var lined = 0;
        foreach (var s in Model.Shells.Where(s => s.Source.Kind == "wall" && !_faces.ContainsKey(s)))
        {
            var ring = s.Nodes.Select(n => NodeIndex[n]).ToList();
            for (var i = 0; i < ring.Count; i++)
            {
                var (a, b) = (ring[i], ring[(i + 1) % ring.Count]);
                if (p.Held[a] == null || p.Held[b] == null) continue;
                foreach (var x in mesh.NodesAlong(a, b)) if (Hold(x, null)) lined++;
            }
        }

        // A shell wall's foot: its footing's support all along it; where nothing is under it, a line support.
        var slabNodes = _faces.Where(x => x.Key.Source.Kind == "slab").SelectMany(x => x.Value).SelectMany(f => p.Shells[f.Shell].Nodes).ToHashSet();
        var standing = new HashSet<string>();
        foreach (var g in _faces.Keys.Where(s => s.Source.Kind == "wall").GroupBy(s => s.Source.SourceId))
        {
            var z0 = g.Min(s => s.Nodes.Min(n => n.At.Z));
            var feet = g.SelectMany(s => s.Nodes).Where(n => n.At.Z <= z0 + AnalyticalModel.Tolerance).Select(n => NodeIndex[n]).Distinct().ToList();
            var spec = feet.Select(n => p.Held[n]).FirstOrDefault(h => h != null);
            var along = new HashSet<int>(feet);
            foreach (var s in g)
            {
                var ring = s.Nodes.Select(n => NodeIndex[n]).ToList();
                for (var i = 0; i < ring.Count; i++)
                {
                    var (a, b) = (ring[i], ring[(i + 1) % ring.Count]);
                    if (feet.Contains(a) && feet.Contains(b)) foreach (var x in mesh.NodesAlong(a, b)) along.Add(x);
                }
            }
            foreach (var x in along)
            {
                if (p.Held[x] != null) continue;
                if (spec != null) { p.Held[x] = (bool[])spec.Clone(); continue; }
                if (barNodes.Contains(x) || slabNodes.Contains(x)) continue;
                if (Hold(x, null)) standing.Add(g.First().Source.Name);
            }
        }
        if (lined > 0) Notes.Add($"{lined} mesh node{(lined == 1 ? "" : "s")} along walls that are line supports held as the walls' own nodes are.");
        if (standing.Count > 0) Notes.Add($"Shell walls with nothing modelled under their foot ({string.Join(", ", standing.Take(6))}{(standing.Count > 6 ? "…" : "")}): their foot held as a line support (translations).");

        ShellInfo = new ShellSummary(Surfaces.Count, p.Shells.Count, p.Shells.Count(x => x.Count == 4), p.Shells.Count(x => x.Count == 3), mesh.Nodes.Count - mesh.Given, Settings.MeshSize);
        Notes.Add($"Shells (S2): {p.Shells.Count} elements ({ShellInfo.Quads} MITC4 quadrilaterals, {ShellInfo.Triangles} MITC3 triangles) on {Surfaces.Count} surface{(Surfaces.Count == 1 ? "" : "s")}, mesh {Settings.MeshSize:0.###} m (at least {AnalysisSettings.PerSide} elements across a narrow piece); the slabs' loads stay on them, the 45° rule only where a slab is not a shell.");

        bool Hold(int x, bool[]? spec)
        {
            if (p.Held[x] is { } h) { if (h[0] && h[1] && h[2]) return false; h[0] = h[1] = h[2] = true; return true; }
            p.Held[x] = spec ?? [true, true, true, false, false, false];
            return true;
        }
    }

    // ------------------------------------------------------------ loads on the shells

    /// <summary>Within how far of a face (metres) a point load is still put on it: past every element's outline, on the nearest.</summary>
    const double FaceReach = 1e-6;

    static bool Inside(Vec2[] poly, Vec2 q, double tolerance)
    {
        for (var i = 0; i < poly.Length; i++)
        {
            var (a, b) = (poly[i], poly[(i + 1) % poly.Length]);
            var d = b - a;
            var len = d.Length;
            if (len < 1e-12) continue;
            if (Vec2.Cross(d, q - a) / len < -tolerance) return false;
        }
        return true;
    }

    /// <summary>A force at a point of a face's outline, on its nodes by its shape functions (statically consistent).</summary>
    void OnFace(MappedCase mc, MeshedFace face, Vec2 at, Vec3 f)
    {
        var nodes = Problem.Shells[face.Shell].Nodes;
        var w = CornerWeights(face.ByNode, at);
        for (var i = 0; i < nodes.Length; i++) if (Math.Abs(w[i]) > 1e-15) mc.Loads.AddNodal(nodes[i], f * w[i]);
    }

    /// <summary>A point load on a meshed piece: on the element under it (in plan), else the nearest one.</summary>
    double PointOnMesh(MappedCase mc, List<MeshedFace> faces, Vec2 at, Vec3 f)
    {
        var face = faces.FirstOrDefault(x => at.X >= x.Min.X - 1e-6 && at.X <= x.Max.X + 1e-6 && at.Y >= x.Min.Y - 1e-6 && at.Y <= x.Max.Y + 1e-6 && Inside(x.Poly, at, FaceReach))
            ?? faces.MinBy(x => (Centroid(x.Poly) - at).Length)!;
        OnFace(mc, face, at, f);
        return f.Length;
    }

    static Vec2 Centroid(Vec2[] poly) => poly.Aggregate(new Vec2(0, 0), (a, b) => a + b) * (1.0 / poly.Length);

    /// <summary>A line load from a to b (plan), its intensity per metre going linearly from wa to wb: in short stretches, three Gauss points each, on the elements under them.</summary>
    double SegmentOnMesh(MappedCase mc, List<MeshedFace> faces, Vec2 a, Vec2 b, Vec3 wa, Vec3 wb)
    {
        var len = (b - a).Length;
        if (len < 1e-12) return 0;
        var n = Math.Max(1, (int)Math.Ceiling(len / (Settings.MeshSize / 2)));
        var total = Vec3.Zero;
        for (var k = 0; k < n; k++)
        {
            var (t0, t1) = ((double)k / n, (double)(k + 1) / n);
            foreach (var (g, w) in FrameElement.Gauss3)
            {
                var t = t0 + (t1 - t0) * (1 + g) / 2;
                var f = (wa + (wb - wa) * t) * (len * (t1 - t0) * w / 2);
                PointOnMesh(mc, faces, a + (b - a) * t, f);
                total += f;
            }
        }
        return total.Length;
    }

    /// <summary>A side's line load (trapezoids per unit intensity along it) times q, on a meshed piece's elements along it.</summary>
    double LineOnMesh(MappedCase mc, Side side, List<MeshedFace> faces, List<Trapezoid> profile, Vec3 q)
    {
        var placed = 0.0;
        foreach (var t in profile)
        {
            var (s0, s1) = (Math.Clamp(t.S0, 0, side.Length), Math.Clamp(t.S1, 0, side.Length));
            if (t.S1 - t.S0 < 1e-12)
            {
                PointOnMesh(mc, faces, side.A + side.Dir * s0, q * t.Integral);
                placed += Math.Abs(t.Integral) * q.Length;
                continue;
            }
            // A part past the side's ends is put at its end, its intensity kept (the side's own length carries it).
            var k = (t.S1 - t.S0) / Math.Max(s1 - s0, 1e-12);
            if (s1 - s0 < 1e-12) { PointOnMesh(mc, faces, side.A + side.Dir * s0, q * t.Integral); placed += Math.Abs(t.Integral) * q.Length; continue; }
            SegmentOnMesh(mc, faces, side.A + side.Dir * s0, side.A + side.Dir * s1, q * (t.W0 * k), q * (t.W1 * k));
            placed += Math.Abs(t.Integral) * q.Length;
        }
        return placed;
    }

    /// <summary>
    /// An area load over a meshed piece (its rings in plan, clipped to the piece; q a global vector per plan m²): a uniform
    /// load on each element it covers whole (per its true area: q times the plane's cosine), the shape functions integrated over
    /// the covered part of the others (a fan of triangles, three points each). Returns the force placed (kN).
    /// </summary>
    double AreaOnMesh(MappedCase mc, Piece piece, List<MeshedFace> faces, IReadOnlyList<IReadOnlyList<Vec2>> rings, Vec3 q)
    {
        var cos = Math.Abs(piece.Normal.Z);
        var area = Tributary.Area(rings);
        if (Math.Abs(area) < 1e-12) return 0;
        if (area >= piece.Area * (1 - 1e-9))
        {
            foreach (var f in faces) mc.Loads.Surface.Add((f.Shell, q * cos));
            return q.Length * area;
        }
        var placed = 0.0;
        var (lo, hi) = (new Vec2(rings.Min(r => r.Min(x => x.X)), rings.Min(r => r.Min(x => x.Y))), new Vec2(rings.Max(r => r.Max(x => x.X)), rings.Max(r => r.Max(x => x.Y))));
        foreach (var f in faces)
        {
            if (f.Max.X < lo.X || f.Min.X > hi.X || f.Max.Y < lo.Y || f.Min.Y > hi.Y) continue;
            placed += OnFaceArea(mc, f, rings, q, cos);
        }
        return placed;
    }

    /// <summary>The part of rings over a face: whole, a uniform load; else the shape functions integrated over it. Returns the force placed.</summary>
    double OnFaceArea(MappedCase mc, MeshedFace f, IReadOnlyList<IReadOnlyList<Vec2>> rings, Vec3 q, double cos)
    {
        var clipped = rings.Select(r => Tributary.ClipTo(r, f.Poly)).Where(r => r.Count >= 3).ToList();
        var a = Tributary.Area(clipped.Select(r => (IReadOnlyList<Vec2>)r));
        if (Math.Abs(a) < 1e-12) return 0;
        if (a >= f.Area * (1 - 1e-9)) { mc.Loads.Surface.Add((f.Shell, q * cos)); return q.Length * f.Area; }
        foreach (var r in clipped)
            for (var i = 1; i + 1 < r.Count; i++)
            {
                var (p0, p1, p2) = (r[0], r[i], r[i + 1]);
                var ta = Vec2.Cross(p1 - p0, p2 - p0) / 2;
                if (Math.Abs(ta) < 1e-15) continue;
                // The edges' midpoints: exact for a quadratic over the triangle.
                foreach (var m in new[] { (p0 + p1) * 0.5, (p1 + p2) * 0.5, (p2 + p0) * 0.5 }) OnFace(mc, f, m, q * (ta / 3));
            }
        return q.Length * a;
    }

    /// <summary>The shell walls' elements in a façade's plane (parallel within 10°, within <see cref="FacadeReach"/> of it), each with its outline in the façade's (along, up) coordinates.</summary>
    List<MeshedFace> WallFacesAlong(Vec3 p0, Vec2 u, Func<Vec3, Vec2> sz)
    {
        var o = new List<MeshedFace>();
        foreach (var (k, pts, n) in _wallFaces)
        {
            if (Math.Abs(Vec2.Dot(new Vec2(n.X, n.Y), u)) > Math.Sin(10 * Math.PI / 180)) continue;
            if (pts.Any(x => Math.Abs(Vec2.Cross(u, x.Xy - p0.Xy)) > FacadeReach)) continue;
            var mf = MeshedFace.Of(k, pts.Select(sz).ToArray());
            if (mf.Area >= 1e-10) o.Add(mf);
        }
        return o;
    }

    /// <summary>A façade's load (rings in its plane, q a global vector per m²) on the shell walls' elements it covers. Returns the area covered.</summary>
    double OnWallFaces(MappedCase mc, List<MeshedFace> faces, IReadOnlyList<IReadOnlyList<Vec2>> rings, Vec3 q)
    {
        var covered = 0.0;
        var (lo, hi) = (new Vec2(rings.Min(r => r.Min(x => x.X)), rings.Min(r => r.Min(x => x.Y))), new Vec2(rings.Max(r => r.Max(x => x.X)), rings.Max(r => r.Max(x => x.Y))));
        foreach (var f in faces)
        {
            if (f.Max.X < lo.X || f.Min.X > hi.X || f.Max.Y < lo.Y || f.Min.Y > hi.Y) continue;
            covered += OnFaceArea(mc, f, rings, q, 1) / Math.Max(q.Length, 1e-300);
        }
        return covered;
    }

    // ------------------------------------------------------------ the bars cut at the shells' nodes

    /// <summary>
    /// Each bar lying along the mesh's edges cut at its nodes (§10.4): the pieces keep the bar's section, axes and arms (an
    /// eccentric end's arm going linearly to the other's), its releases at its own ends only; its first piece keeps its index,
    /// the others go at the end; its span loads go to the pieces they lie on.
    /// </summary>
    void SplitBars()
    {
        var p = Problem;
        var count = p.Elements.Count;
        for (var k = 0; k < count; k++)
        {
            var e = p.Elements[k];
            e.Prepare(p.Nodes);
            PieceMap[k] = [new MemberPiece(k, 0, e.L)];
            if (k >= _paths.Length || _paths[k] is not { Count: > 2 } path) continue;
            var (pa, pb) = (p.Nodes[e.I], p.Nodes[e.J]);
            var d = pb - pa;
            var len2 = Vec3.Dot(d, d);
            var ts = path.Select(n => Math.Clamp(Vec3.Dot(p.Nodes[n] - pa, d) / len2, 0, 1)).ToList();
            var list = new List<MemberPiece>();
            for (var s = 0; s + 1 < path.Count; s++)
            {
                var (t0, t1) = (ts[s], ts[s + 1]);
                var piece = new FrameElement
                {
                    Id = $"{e.Id}~{s + 1}", Name = e.Name, SourceId = e.SourceId, I = path[s], J = path[s + 1],
                    OffI = e.OffI + (e.OffJ - e.OffI) * t0, OffJ = e.OffI + (e.OffJ - e.OffI) * t1, Local2 = e.Local2,
                    E = e.E, G = e.G, A = e.A, I22 = e.I22, I33 = e.I33, Jt = e.Jt, As2 = e.As2, As3 = e.As3,
                    ReleaseI = s == 0 ? e.ReleaseI : new bool[6], ReleaseJ = s == path.Count - 2 ? e.ReleaseJ : new bool[6],
                };
                int index;
                if (s == 0) { p.Elements[k] = piece; index = k; }
                else { p.Elements.Add(piece); Frames.Add(Frames[k]); index = p.Elements.Count - 1; }
                list.Add(new MemberPiece(index, t0 * e.L, t1 * e.L));
            }
            PieceMap[k] = list;
        }
        // The span loads on the members, onto their pieces.
        foreach (var mc in Cases)
        {
            var span = mc.Loads.Span;
            var moved = new List<(int, SpanLoad)>();
            for (var i = span.Count - 1; i >= 0; i--)
            {
                var (k, l) = span[i];
                if (k >= count || PieceMap[k].Count < 2) continue;
                span.RemoveAt(i);
                var pieces = PieceMap[k];
                if (l.Point)
                {
                    var at = pieces.FirstOrDefault(x => l.A >= x.X0 - 1e-12 && l.A < x.X1) ?? (l.A < pieces[0].X0 ? pieces[0] : pieces[^1]);
                    moved.Add((at.Element, SpanLoad.Force(Math.Clamp(l.A - at.X0, 0, at.X1 - at.X0), l.Qa)));
                    continue;
                }
                foreach (var x in pieces)
                {
                    var (a, b) = (Math.Max(l.A, x.X0), Math.Min(l.B, x.X1));
                    if (b - a < 1e-12) continue;
                    Vec3 Q(double s) => l.B - l.A < 1e-12 ? l.Qa : l.Qa + (l.Qb - l.Qa) * ((s - l.A) / (l.B - l.A));
                    moved.Add((x.Element, new SpanLoad(a - x.X0, b - x.X0, Q(a), Q(b))));
                }
            }
            span.AddRange(moved);
        }
    }
}
