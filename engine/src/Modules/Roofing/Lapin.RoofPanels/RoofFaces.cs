using Lapin.Geometry;

namespace Lapin.RoofPanels;

/// <summary>The kinds of a roof covering's edges, as the panels and their accessories see them.</summary>
public static class EdgeKinds
{
    public const string Eave = "eave", Verge = "verge", Ridge = "ridge", Hip = "hip", Valley = "valley", TopEdge = "topEdge",
        ConcaveBreak = "concaveBreak", ConvexBreak = "convexBreak", WallTop = "wallTop", WallSide = "wallSide",
        WindowTop = "windowTop", WindowBottom = "windowBottom", WindowSide = "windowSide";

    public static readonly string[] All = [Eave, Verge, Ridge, Hip, Valley, TopEdge, ConcaveBreak, ConvexBreak, WallTop, WallSide, WindowTop, WindowBottom, WindowSide];

    /// <summary>The edges round a roof window: its flashing kit's, not lengths of flashing.</summary>
    public static bool IsWindow(string k) => k is WindowTop or WindowBottom or WindowSide;

    public static bool IsBreak(string k) => k is ConcaveBreak or ConvexBreak;
}

/// <summary>
/// One sloped plane of a roof as the layout sees it: <see cref="Origin"/> at the start of its longest lowest edge (the eave),
/// <see cref="X"/> along it, <see cref="Y"/> up the slope, <see cref="N"/> its normal (up); <see cref="Poly"/> its outline in
/// (u, v) metres; <see cref="Target"/> what the panels cover (the outline less the gap left along its valleys);
/// <see cref="Eave"/> how long its eaves are.
/// </summary>
public sealed class RoofFace
{
    public required string Id { get; init; }
    public required int Plane { get; init; }
    public required Vec3 Origin { get; init; }
    public required Vec3 X { get; init; }
    public required Vec3 Y { get; init; }
    public required Vec3 N { get; init; }
    public required IReadOnlyList<Vec3> World { get; init; }
    public required IReadOnlyList<Vec2> Poly { get; init; }
    public List<Vec2> Target { get; set; } = [];
    public double Eave { get; set; }
    /// <summary>Where along u its eaves run (from, to): one span from 0 on a whole plane, several on one another roof cuts into.</summary>
    public List<(double From, double To)> EaveSpans { get; } = [];
    /// <summary>The openings in it (roof windows, dormers, another roof standing in the slope): the panels are cut round them.</summary>
    public List<FaceHole> Holes { get; } = [];

    /// <summary>
    /// The phase of its columns the user fixed (S7, metres: the columns' left sides that far from its origin along u, taken modulo the
    /// useful width); null, the layout tries a phase every 15 mm and keeps the cheapest.
    /// </summary>
    public double? FixedPhase { get; set; }

    /// <summary>What the panels cover: the target less its holes (m²).</summary>
    public double TargetArea => Clip.Area(Target) - Holes.Sum(h => h.Area);

    public double PitchDegrees => Math.Acos(Math.Clamp(N.Z, -1, 1)) * 180 / Math.PI;

    public Vec2 Uv(Vec3 p) { var d = p - Origin; return new Vec2(Vec3.Dot(d, X), Vec3.Dot(d, Y)); }
    public Vec3 Xyz(Vec2 uv, double h = 0) => Origin + X * uv.X + Y * uv.Y + N * h;
}

/// <summary>An edge of the covering: its kind, the faces it bounds (one or two), its ends; <see cref="Opening"/>, the hole it runs round (its id).</summary>
public sealed record RoofEdge(string Kind, IReadOnlyList<string> Faces, Vec3 A, Vec3 B, string? Opening = null)
{
    public double Length => (B - A).Length;
}

/// <summary>
/// A vertical face of a wall as the abutments see it (layout.against_wall): a point on it, along it (<see cref="Tangent"/>, from
/// <see cref="Lo"/> to <see cref="Hi"/>), across it (<see cref="Normal"/>, level), its bottom and top.
/// </summary>
public sealed record WallFace(Vec3 Origin, Vec3 Tangent, Vec3 Normal, double Lo, double Hi, double Z0, double Z1);

/// <summary>
/// What the panels are cut round on a roof plane (S3): a roof window's or a dormer's hole, or another roof standing inside the slope
/// (decision 847); its outline on the plane (<see cref="Ring"/>, world) and the convex regions it is made of. <see cref="Kind"/>:
/// <c>window</c>, <c>dormer</c>, <c>roof</c> (a roof at this one's height, met in valleys) or <c>under</c> (a roof over this one).
/// </summary>
public sealed record RoofHole(string Id, string Name, string Kind, int Plane, IReadOnlyList<Vec3> Ring, IReadOnlyList<IReadOnlyList<Vec3>> Regions)
{
    public const string Window = "window", Dormer = "dormer", Roof = "roof", Under = "under";
}

/// <summary>A hole on a face, in its (u, v): the outline, its convex regions, how far up it reaches.</summary>
public sealed record FaceHole(RoofHole Of, List<Vec2> Ring, List<List<Vec2>> Regions)
{
    public double MinV => Clip.MinY(Ring);
    public double MaxV => Clip.MaxY(Ring);
    public double Area => Clip.Area(Ring);
}

/// <summary>
/// A roof's planes read for the layout, and every edge of the covering classified (docs/plan-roof-panels.md §2.2): a free
/// edge is an eave (level, the face above it), a top edge (level, the face below it: a mono-pitch roof's top) or a verge; an
/// edge two faces share is a break (one face below it, one above: concave when the upper is the steeper, else convex), a
/// valley (the faces fold up from it) or a ridge, level, or a hip. Edges are split at every corner lying on them, so a hip
/// meeting a ridge part way is two edges. The layout of the Panotaj Bilka app (layout.read_roof), on the planes themselves.
/// </summary>
public static class RoofFaces
{
    /// <summary>
    /// The faces and edges of a roof's planes (each counter-clockwise from above); <paramref name="valleyGap"/> metres left along
    /// valleys. <paramref name="junctions"/>: lines in plan where another roof at this one's height takes it away (decision 847):
    /// a free edge along one is a valley with that roof, the panels kept the gap from it. <paramref name="planeOf"/>: each
    /// plane's index in the roof's shape (its own place in the list when not given).
    /// </summary>
    /// <remarks>
    /// Stage S3: a free edge along a wall rising over it (<paramref name="walls"/>: within 35 mm of its face, along it, the wall's
    /// top 60 mm over it; the edge lifted <paramref name="wallLift"/> to the covering's top, as the app reads it from the IFC) or
    /// along a line where a roof over this one takes it away (<paramref name="abutments"/>, decision 847's <c>Under</c>) is a wall
    /// abutment, top when level, else side; each of <paramref name="holes"/> goes to the face it lies on, its sides classified.
    /// </remarks>
    public static (List<RoofFace> Faces, List<RoofEdge> Edges) Read(IReadOnlyList<IReadOnlyList<Vec3>> planes, double valleyGap,
        IReadOnlyList<(Vec2 A, Vec2 B)>? junctions = null, IReadOnlyList<int>? planeOf = null, IReadOnlyList<WallFace>? walls = null,
        IReadOnlyList<(Vec2 A, Vec2 B)>? abutments = null, double wallLift = 0, IReadOnlyList<RoofHole>? holes = null)
    {
        // One point for corners within 2 µm of each other: the planes' shared corners, computed plane by plane.
        var verts = new List<Vec3>();
        Vec3 Snap(Vec3 p)
        {
            foreach (var v in verts) if (Vec3.DistanceSquared(v, p) < 4e-12) return v;
            verts.Add(p);
            return p;
        }
        var faces = new List<RoofFace>();
        foreach (var (plane, index) in planes.Select((p, i) => (p, i)))
        {
            var w = Simplify([.. plane.Select(Snap)]);
            if (w.Count < 3) continue;
            var n = VecMath.NewellNormal(w);
            if (n.Length < 1e-9) continue;
            n = n.Normalized();
            if (n.Z < 0) n = -n;
            // Sloped faces only: flat parts and walls carry no panels.
            if (!(n.Z > 0.05 && n.Z < 0.995)) continue;
            var lowest = w.Min(p => p.Z);
            var low = Pairs(w).Where(e => Math.Max(e.A.Z, e.B.Z) <= lowest + 0.002).ToList();
            Vec3 x, y, e0;
            if (low.Count > 0)
            {
                var best = low[0];
                foreach (var e in low) if ((e.B - e.A).Length > (best.B - best.A).Length) best = e;
                e0 = best.A;
                x = (best.B - best.A).Normalized();
                y = Vec3.Cross(n, x);
            }
            else
            {
                y = (Vec3.UnitZ - n * n.Z).Normalized();
                x = Vec3.Cross(y, n);
                e0 = w.MinBy(p => p.Z);
            }
            if (y.Z <= 0) throw new ArgumentException("A roof plane's slope cannot be told.");
            faces.Add(new RoofFace
            {
                Id = $"A{faces.Count + 1:00}", Plane = planeOf != null && index < planeOf.Count ? planeOf[index] : index, Origin = e0, X = x, Y = y, N = n, World = w,
                Poly = [.. w.Select(p => { var d = p - e0; return new Vec2(Vec3.Dot(d, x), Vec3.Dot(d, y)); })],
            });
        }
        foreach (var f in faces) f.Target = [.. f.Poly];
        if (faces.Count == 0) return (faces, []);

        // Every face's edges split at the corners on them, each piece with the faces it bounds, in the order first met.
        var sortedVerts = verts.Distinct().OrderBy(v => v.X).ThenBy(v => v.Y).ThenBy(v => v.Z).ToList();
        var shared = new Dictionary<(Vec3, Vec3), List<RoofFace>>();
        var order = new List<(Vec3, Vec3)>();
        foreach (var f in faces)
            foreach (var (a, b) in Pairs(f.World))
            {
                var d = b - a;
                var norm = Vec3.Dot(d, d);
                var points = new List<(double T, Vec3 P)>();
                foreach (var p in sortedVerts)
                {
                    var v = p - a;
                    var t = Vec3.Dot(v, d) / norm;
                    if (t >= -1e-6 && t <= 1 + 1e-6 && (v - d * t).Length < 2e-5) points.Add((t, p));
                }
                points.Sort((l, r) => l.T.CompareTo(r.T));
                for (var i = 0; i + 1 < points.Count; i++)
                {
                    var (u, q) = (points[i].P, points[i + 1].P);
                    var key = Less(u, q) ? (u, q) : (q, u);
                    if (!shared.TryGetValue(key, out var list)) { shared[key] = list = []; order.Add(key); }
                    list.Add(f);
                }
            }

        var edges = new List<RoofEdge>();
        foreach (var key in order)
        {
            var (a, b) = key;
            var rs = shared[key];
            var length = (b - a).Length;
            var horizontal = Math.Abs(a.Z - b.Z) < 0.002;
            string kind;
            if (rs.Count == 1)
            {
                var r = rs[0];
                var side = InwardHeight(r, a, b);
                if (junctions != null && OnJunction(a.Xy, b.Xy, junctions))
                {
                    // Where another roof at its height takes it away: a valley between the two roofs, the panels short of it.
                    kind = EdgeKinds.Valley;
                    TrimValley(r, a, b, valleyGap, strict: false);
                }
                else if ((walls != null && AgainstWall(a + Vec3.UnitZ * wallLift, b + Vec3.UnitZ * wallLift, walls))
                    || (abutments != null && OnJunction(a.Xy, b.Xy, abutments)))
                    kind = horizontal ? EdgeKinds.WallTop : EdgeKinds.WallSide;
                else if (horizontal && side > 1e-5)
                {
                    kind = EdgeKinds.Eave;
                    r.Eave += length;
                    var (ua, ub) = (r.Uv(a).X, r.Uv(b).X);
                    r.EaveSpans.Add((Math.Min(ua, ub), Math.Max(ua, ub)));
                }
                else if (horizontal) kind = EdgeKinds.TopEdge;
                else kind = EdgeKinds.Verge;
            }
            else
            {
                if (rs.Count != 2) throw new ArgumentException("More than two roof planes share an edge.");
                var (first, second) = (rs[0], rs[1]);
                double p = InwardHeight(first, a, b), q = InwardHeight(second, a, b);
                if (p * q < -1e-9)
                {
                    var (lower, upper) = p < 0 ? (first, second) : (second, first);
                    kind = upper.N.Z < lower.N.Z ? EdgeKinds.ConcaveBreak : EdgeKinds.ConvexBreak;
                }
                else
                {
                    var own = first.World.MaxBy(v => Math.Abs(Vec3.Dot(v - a, second.N)));
                    kind = Vec3.Dot(own - a, second.N) > 0 ? EdgeKinds.Valley : horizontal ? EdgeKinds.Ridge : EdgeKinds.Hip;
                }
                if (kind == EdgeKinds.Valley)
                    foreach (var r in rs) TrimValley(r, a, b, valleyGap, strict: true);
            }
            edges.Add(new RoofEdge(kind, [.. rs.Select(r => r.Id)], a, b));
        }
        if (holes != null) foreach (var h in holes) AddHole(faces, edges, h);
        return (faces, edges);
    }

    /// <summary>
    /// A hole onto the face it lies on, and its sides as edges: round a roof window its kit's top, bottom and sides; round a
    /// dormer the front wall's apron (top abutment), the cheeks (side abutments), the valleys of a gable dormer's roof and the
    /// line a shed dormer's flatter roof meets the slope (a concave break); round another roof valleys, round a roof over this
    /// one abutments.
    /// </summary>
    static void AddHole(List<RoofFace> faces, List<RoofEdge> edges, RoofHole h)
    {
        if (h.Ring.Count < 3) return;
        var centre = new Vec3(h.Ring.Average(p => p.X), h.Ring.Average(p => p.Y), h.Ring.Average(p => p.Z));
        var face = faces.FirstOrDefault(f => f.Plane == h.Plane && Polygon2.Contains(f.Poly.ToList(), f.Uv(centre)));
        if (face == null) return;
        List<Vec2> Ccw(IEnumerable<Vec3> pts) { var o = pts.Select(face.Uv).ToList(); if (Polygon2.SignedArea(o) < 0) o.Reverse(); return o; }
        var hole = new FaceHole(h, Ccw(h.Ring), [.. h.Regions.Select(Ccw)]);
        face.Holes.Add(hole);
        var lo = hole.MinV;
        for (var i = 0; i < h.Ring.Count; i++)
        {
            var (a, b) = (h.Ring[i], h.Ring[(i + 1) % h.Ring.Count]);
            if ((b - a).Length < 1e-6) continue;
            var (ua, ub) = (face.Uv(a), face.Uv(b));
            var level = Math.Abs(ua.Y - ub.Y) < 0.002;
            var bottom = level && Math.Abs((ua.Y + ub.Y) / 2 - lo) < 0.002;
            var across = Math.Abs(ua.X - ub.X) < 0.002;
            var kind = h.Kind switch
            {
                RoofHole.Window => level ? (bottom ? EdgeKinds.WindowBottom : EdgeKinds.WindowTop) : EdgeKinds.WindowSide,
                RoofHole.Dormer => bottom ? EdgeKinds.WallTop : across ? EdgeKinds.WallSide : level ? EdgeKinds.ConcaveBreak : EdgeKinds.Valley,
                RoofHole.Under => level ? EdgeKinds.WallTop : EdgeKinds.WallSide,
                _ => EdgeKinds.Valley,
            };
            edges.Add(new RoofEdge(kind, [face.Id], a, b, h.Id));
        }
    }

    /// <summary>
    /// Whether a–b lies against one of the walls (layout.against_wall): within 35 mm of its face, along it (5 cm to spare at
    /// its ends), no lower than 25 mm under its bottom and at least 60 mm under its top: the wall rises over the edge.
    /// </summary>
    public static bool AgainstWall(Vec3 a, Vec3 b, IReadOnlyList<WallFace> walls)
    {
        foreach (var w in walls)
        {
            var (oa, ob) = (a - w.Origin, b - w.Origin);
            double ta = Vec3.Dot(oa, w.Tangent), tb = Vec3.Dot(ob, w.Tangent);
            if (Math.Max(Math.Abs(Vec3.Dot(oa, w.Normal)), Math.Abs(Vec3.Dot(ob, w.Normal))) <= 0.035
                && Math.Min(ta, tb) >= w.Lo - 0.05 && Math.Max(ta, tb) <= w.Hi + 0.05
                && Math.Min(a.Z, b.Z) >= w.Z0 - 0.025 && Math.Max(a.Z, b.Z) < w.Z1 - 0.06)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The face's target less the gap along a valley a–b. A valley the face lies wholly on one side of (its own roof's);
    /// with <paramref name="strict"/> off (a valley with another roof, along a plane trimmed by it) a face reaching past the
    /// line's run keeps its target there whole rather than lose what lies beyond.
    /// </summary>
    static void TrimValley(RoofFace r, Vec3 a, Vec3 b, double gap, bool strict)
    {
        var (aa, bb) = (r.Uv(a), r.Uv(b));
        var d = bb - aa;
        var inward = new Vec2(-d.Y, d.X) * (1 / d.Length);
        var mean = new Vec2(r.Poly.Average(v => v.X), r.Poly.Average(v => v.Y));
        if (Vec2.Dot(mean - aa, inward) < 0) inward = -inward;
        if (r.Poly.Min(v => Vec2.Dot(v - aa, inward)) >= -1e-5)
        {
            r.Target = Clip.Trim(r.Target, inward, Vec2.Dot(aa, inward) + gap);
            return;
        }
        if (strict) throw new ArgumentException("A valley crosses a roof plane where the layout cannot follow it.");
        // A plane another roof cuts a notch into (a wing's valleys in the main slope): the gap taken along the valley's run
        // only, on the side the plane lies, the rest of the plane as it is.
        var mid = (aa + bb) * 0.5;
        if (!Polygon2.Contains(r.Poly, mid + inward * 1e-4)) inward = -inward;
        var along = d * (1 / d.Length) * gap;
        Vec2[] strip = [aa - along, bb + along, bb + along + inward * gap, aa - along + inward * gap];
        var (rings, _) = PlanTrim.Difference(r.Target, [strip]);
        if (rings.Count > 0) r.Target = rings.MaxBy(Clip.Area)!;
    }

    /// <summary>Whether a–b (in plan) runs along one of the lines, over most of its length.</summary>
    static bool OnJunction(Vec2 a, Vec2 b, IReadOnlyList<(Vec2 A, Vec2 B)> lines)
    {
        var mid = (a + b) * 0.5;
        var d = b - a;
        if (d.Length < 1e-6) return false;
        foreach (var (p, q) in lines)
        {
            var e = q - p;
            var len = e.Length;
            if (len < 1e-6) continue;
            var axis = e * (1 / len);
            if (Math.Abs(Vec2.Cross(axis, d.Normalized())) > 1e-3) continue;
            if (Math.Abs(Vec2.Cross(axis, mid - p)) > 2e-4) continue;
            var t = Vec2.Dot(mid - p, axis);
            if (t > 1e-4 && t < len - 1e-4) return true;
        }
        return false;
    }

    static IEnumerable<(Vec3 A, Vec3 B)> Pairs(IReadOnlyList<Vec3> p)
    {
        for (var i = 0; i < p.Count; i++) yield return (p[i], p[(i + 1) % p.Count]);
    }

    static bool Less(Vec3 a, Vec3 b) => a.X != b.X ? a.X < b.X : a.Y != b.Y ? a.Y < b.Y : a.Z < b.Z;

    /// <summary>Corners in line with their neighbours dropped.</summary>
    static List<Vec3> Simplify(List<Vec3> pts)
    {
        var o = new List<Vec3>();
        foreach (var p in pts) if (o.Count == 0 || (o[^1] - p).Length > 1e-8) o.Add(p);
        if (o.Count > 1 && (o[0] - o[^1]).Length <= 1e-8) o.RemoveAt(o.Count - 1);
        var changed = true;
        while (changed && o.Count > 3)
        {
            changed = false;
            for (var i = 0; i < o.Count; i++)
            {
                var (a, p, b) = (o[(i - 1 + o.Count) % o.Count], o[i], o[(i + 1) % o.Count]);
                if (Vec3.Cross(p - a, b - a).Length < 1e-5) { o.RemoveAt(i); changed = true; break; }
            }
        }
        return o;
    }

    /// <summary>
    /// How the face rises from the edge a–b: positive when the face lies above it (an eave's), negative below it; the
    /// z of the inward perpendicular, the sign from the face's own perimeter (the face on its left), else from its corners.
    /// </summary>
    public static double InwardHeight(RoofFace r, Vec3 a, Vec3 b)
    {
        var d = b - a;
        var perpendicular = Vec3.Cross(r.N, d);
        for (var i = 0; i < r.World.Count; i++)
        {
            var (u, v) = (r.World[i], r.World[(i + 1) % r.World.Count]);
            var edge = v - u;
            if (Vec3.Cross(edge, a - u).Length < 2e-4 && Vec3.Cross(edge, b - u).Length < 2e-4)
            {
                var t = Vec3.Dot((a + b) * 0.5 - u, edge) / Vec3.Dot(edge, edge);
                if (t > -1e-5 && t < 1 + 1e-5) return perpendicular.Z * Math.Sign(Vec3.Dot(d, edge)) / d.Length;
            }
        }
        var inside = r.World.Select(p => Vec3.Dot(p - a, perpendicular)).MaxBy(Math.Abs);
        return perpendicular.Z * Math.Sign(inside) / d.Length;
    }
}
