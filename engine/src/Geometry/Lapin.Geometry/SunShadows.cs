namespace Lapin.Geometry;

/// <summary>
/// Vector sun shadows in a drawing (docs/plan-visualisation.md, vector shadows; as Archicad draws them in elevations and
/// sections): the sun's rays are parallel, so the shadow a face casts on another's plane is its projection along them.
/// For each face the drawing sees and the sun lights, the lit faces between it and the sun are projected onto its plane;
/// what of that lies on the face, is not hidden in the view by a nearer face, is shadow. A face the sun does not reach
/// (turned away from it) is in shade whole. The result is filled areas in the view's coordinates, each owned by the
/// element whose face it lies on.
/// <para>
/// No 2D boolean library: what the drawing sees and what hides it is <see cref="ViewFaces"/>' (the surface hatches use the
/// same): the face, the shadows and the faces in front are cut into trapezoids by a sweep, and each trapezoid is tested at
/// its middle: inside the face (even-odd), inside any shadow, behind no nearer face.
/// </para>
/// </summary>
public static class SunShadows
{
    /// <summary>A shadow area in the view: its outline (view metres) and the element it lies on.</summary>
    public sealed record Area(Vec2[] Outline, int Owner);

    /// <summary>
    /// What to cast and receive: the solids with their owners; the view; the direction towards the sun (unit, world); whether
    /// the view cuts (only what lies beyond its plane receives and hides, within <paramref name="depth"/>); whether the casters
    /// are cut too (a section: what the cut took away casts nothing). Stops at <paramref name="maxAreas"/>.
    /// </summary>
    public static List<Area> Draw(IReadOnlyList<(Solid Solid, int Owner)> solids, ViewFrame view, Vec3 sun, bool cut = false, double depth = double.PositiveInfinity, bool cutCasters = true, int maxAreas = 200_000) =>
        Draw(new ViewFaces(solids, view, cut, depth), sun, cutCasters, maxAreas);

    /// <summary>
    /// As the other, on the faces a drawing sees as already worked out (<see cref="ViewFaces"/>: a section's caps among
    /// what hides), so the surface hatches and the shadows of one drawing share them.
    /// </summary>
    public static List<Area> Draw(ViewFaces scene, Vec3 sun, bool cutCasters = true, int maxAreas = 200_000)
    {
        sun = sun.Normalized();
        var view = scene.Frame;
        var casters = new List<(ViewFaces.Face Face, List<Vec3[]> Loops, Vec3 Min, Vec3 Max)>();
        foreach (var f in scene.Faces)
        {
            // A face nearly along the rays casts a sliver its neighbours' shadows cover anyway.
            if (Vec3.Dot(f.Normal, sun) <= 0.02) continue;
            var loops = cutCasters ? f.Loops.Select(scene.Beyond).Where(l => l.Length >= 3).ToList() : f.Loops;
            if (loops.Count == 0) continue;
            var all = loops.SelectMany(l => l).ToList();
            casters.Add((f, loops, new Vec3(all.Min(p => p.X), all.Min(p => p.Y), all.Min(p => p.Z)), new Vec3(all.Max(p => p.X), all.Max(p => p.Y), all.Max(p => p.Z))));
        }
        // Each face seen is worked out on its own, in parallel; the areas kept in the faces' order (a section's caps receive nothing).
        var seen = scene.SeenFaces.Where(s => s.Face.Owner >= 0).ToList();
        var results = new List<Vec2[]>[seen.Count];
        Parallel.For(0, seen.Count, k => results[k] = Receive(seen[k]));
        var o = new List<Area>();
        for (var k = 0; k < seen.Count && o.Count < maxAreas; k++)
            foreach (var t in results[k]) { o.Add(new Area(t, seen[k].Face.Owner)); if (o.Count >= maxAreas) break; }
        return o;

        List<Vec2[]> Receive(ViewFaces.Seen rs)
        {
            var r = rs.Face;
            var p0 = rs.P0;
            var n = r.Normal;
            var rBox = rs.Box;
            List<List<Vec2[]>> shadows = [];
            var shade = Vec3.Dot(n, sun) <= 1e-6;
            if (!shade)
            {
                var dn = Vec3.Dot(sun, n);
                foreach (var (c, cLoops, cMin, cMax) in casters)
                {
                    if (ReferenceEquals(c, r)) continue;
                    // Only what stands in front of the face's plane, on the sun's side, casts on it.
                    if (MaxSigned(cMin, cMax, p0, n) <= 1e-6) continue;
                    // Its box's corners carried onto the plane first: far from the face, nothing more to do.
                    if (!Near(cMin, cMax, p0, n, dn, rBox)) continue;
                    var projected = new List<Vec2[]>();
                    foreach (var loop in cLoops)
                    {
                        var front = ViewFaces.ClipPlane(loop, p0, n);
                        if (front.Length < 3) continue;
                        projected.Add(front.Select(v => view.Map(v - sun * (Vec3.Dot(v - p0, n) / dn))).ToArray());
                    }
                    if (projected.Count == 0) continue;
                    var b = ViewFaces.Box2(projected.SelectMany(l => l));
                    if (b.Max.X <= rBox.Min.X || b.Min.X >= rBox.Max.X || b.Max.Y <= rBox.Min.Y || b.Min.Y >= rBox.Max.Y) continue;
                    if (Math.Abs(Area2(projected[0])) < 1e-10) continue;
                    shadows.Add(projected);
                }
                if (shadows.Count == 0) return [];
            }
            // The nearer faces over this one in the view, where it may be in shadow.
            var zone = rBox;
            if (!shade)
            {
                var sb = ViewFaces.Box2(shadows.SelectMany(x => x).SelectMany(l => l));
                zone = (new Vec2(Math.Max(rBox.Min.X, sb.Min.X), Math.Max(rBox.Min.Y, sb.Min.Y)), new Vec2(Math.Min(rBox.Max.X, sb.Max.X), Math.Min(rBox.Max.Y, sb.Max.Y)));
                if (zone.Min.X >= zone.Max.X || zone.Min.Y >= zone.Max.Y) return [];
            }
            // Each place once: two flush faces that overlap (two walls, a wall and a slab's edge) would give the same shadow twice,
            // and the drawing fills its shadows even-odd, so the overlap would cancel out. A face is also hidden by the flush faces
            // before it in the drawing's order, which keep the overlap (and its owner); the surface hatches, which share
            // ViewFaces, are left as they were.
            var over = scene.Over(rs, zone);
            var nearer = over.Count;
            foreach (var h in scene.SeenFaces)
            {
                if (ReferenceEquals(h, rs)) break;
                if (h.Face.Owner >= 0 && Vec3.Dot(h.Face.Normal, n) > 1 - 1e-9 && Math.Abs(Vec3.Dot(h.P0 - p0, n)) < 1e-4
                    && !(h.Box.Max.X <= zone.Min.X || h.Box.Min.X >= zone.Max.X || h.Box.Max.Y <= zone.Min.Y || h.Box.Min.Y >= zone.Max.Y)) over.Add(h);
            }
            if (over.Count == nearer) return scene.Visible(rs, shade ? null : shadows, zone);
            return ViewFaces.Cells(rs.View, shade ? null : shadows, over.Select(h => h.View).ToList(),
                (k, at) => k >= nearer || scene.DepthOn(over[k].Face.Normal, over[k].P0, at) < scene.DepthOn(n, p0, at) - 1e-4, zone).ToList();
        }

        // Whether a caster's box, carried along the rays onto a plane, may reach a face's box in the view.
        bool Near(Vec3 min, Vec3 max, Vec3 p0, Vec3 n, double dn, (Vec2 Min, Vec2 Max) box)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            for (var i = 0; i < 8; i++)
            {
                var c = new Vec3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
                var d = Vec3.Dot(c - p0, n);
                // A corner behind the plane stands where it is (its part is clipped away); one in front, carried onto it.
                var q = view.Map(d > 0 ? c - sun * (d / dn) : c);
                x0 = Math.Min(x0, q.X); y0 = Math.Min(y0, q.Y); x1 = Math.Max(x1, q.X); y1 = Math.Max(y1, q.Y);
            }
            return !(x1 <= box.Min.X || x0 >= box.Max.X || y1 <= box.Min.Y || y0 >= box.Max.Y);
        }
    }

    static double MaxSigned(Vec3 min, Vec3 max, Vec3 p0, Vec3 n)
    {
        var best = double.NegativeInfinity;
        for (var i = 0; i < 8; i++)
        {
            var c = new Vec3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
            best = Math.Max(best, Vec3.Dot(c - p0, n));
        }
        return best;
    }

    /// <summary>A loop kept where it lies on the positive side of a plane (Sutherland–Hodgman).</summary>
    public static Vec3[] ClipPlane(Vec3[] loop, Vec3 p0, Vec3 n) => ViewFaces.ClipPlane(loop, p0, n);

    static double Area2(Vec2[] l)
    {
        var a = 0.0;
        for (var i = 0; i < l.Length; i++) { var p = l[i]; var q = l[(i + 1) % l.Length]; a += p.X * q.Y - q.X * p.Y; }
        return a / 2;
    }
}
