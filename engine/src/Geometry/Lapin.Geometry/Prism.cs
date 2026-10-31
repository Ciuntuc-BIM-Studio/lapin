namespace Lapin.Geometry;

/// <summary>
/// Sweeps of planar profiles: nearly everything a building model authors
/// (walls, slabs, beams, columns) is one of these.
/// </summary>
public static class Prism
{
    /// <summary>
    /// Sweep a planar polygon (optionally with holes) along <paramref name="d"/>.
    /// Caps are tagged "start" / "end", the outer sides "side", the hole sides
    /// "reveal". Null for a degenerate profile or a sweep inside its own plane.
    /// </summary>
    public static Solid? Extrude(IReadOnlyList<Vec3> profile, IReadOnlyList<IReadOnlyList<Vec3>>? holes, Vec3 d, string? tag = null)
    {
        if (profile.Count < 3) return null;
        var raw = VecMath.NewellNormal(profile);
        var area = raw.Length;
        if (area < Tolerance.Area) return null;
        var n = raw * (1 / area);
        var along = Vec3.Dot(n, d);
        if (Math.Abs(along) < 1e-12) return null;

        // Orient the outer ring so its normal runs with the sweep; holes the other way.
        var outer = along < 0 ? profile.Reverse().ToList() : profile.ToList();
        if (along < 0) n = -n;
        var rings = new List<List<Vec3>> { outer };
        foreach (var h in holes ?? [])
        {
            if (h.Count < 3) continue;
            var hn = VecMath.NewellNormal(h);
            rings.Add(Vec3.Dot(hn, n) > 0 ? h.Reverse().ToList() : h.ToList());
        }

        var verts = new List<Vec3>();
        var lo = new List<int[]>();
        var hi = new List<int[]>();
        foreach (var r in rings)
        {
            lo.Add(r.Select(p => { verts.Add(p); return verts.Count - 1; }).ToArray());
            hi.Add(r.Select(p => { verts.Add(p + d); return verts.Count - 1; }).ToArray());
        }

        var faces = new List<FaceInput>
        {
            new(lo[0].Reverse().ToArray(), lo.Skip(1).Select(l => l.Reverse().ToArray()).ToArray(), -n, "start"),
            new(hi[0], hi.Skip(1).ToArray(), n, "end"),
        };
        for (int r = 0; r < rings.Count; r++)
        {
            var ring = rings[r];
            for (int i = 0; i < ring.Count; i++)
            {
                int j = (i + 1) % ring.Count;
                // (edge) × (sweep) points away from the material for the outer ring
                // and, with the hole wound the other way, into the hole for a hole.
                var outward = Vec3.Cross(ring[j] - ring[i], d);
                faces.Add(new FaceInput([lo[r][i], lo[r][j], hi[r][j], hi[r][i]], null, outward, r == 0 ? "side" : "reveal"));
            }
        }
        return SolidBuilder.Make(verts, faces, tag);
    }

    /// <summary>Extrude a plan footprint up from <paramref name="baseZ"/>; caps tagged "bottom" / "top".</summary>
    public static Solid? ExtrudeFootprint(IReadOnlyList<Vec2> footprint, double baseZ, double height, string? tag = null)
    {
        if (footprint.Count < 3 || !(height > 0)) return null;
        var s = Extrude(footprint.Select(p => p.At(baseZ)).ToArray(), null, new Vec3(0, 0, height), tag);
        return s == null ? null : s with
        {
            Faces = s.Faces.Select(f => f.Tag switch { "start" => f with { Tag = "bottom" }, "end" => f with { Tag = "top" }, _ => f }).ToArray(),
        };
    }

    public static Solid? Box(Vec3 center, double sx, double sy, double sz, string? tag = null)
    {
        if (!(sx > 0) || !(sy > 0) || !(sz > 0)) return null;
        double hx = sx / 2, hy = sy / 2;
        Vec2[] fp = [new(center.X - hx, center.Y - hy), new(center.X + hx, center.Y - hy), new(center.X + hx, center.Y + hy), new(center.X - hx, center.Y + hy)];
        return ExtrudeFootprint(fp, center.Z - sz / 2, sz, tag);
    }

    /// <summary>Rectangular member from a to b (beams, rafters): width across, height up-ish.</summary>
    public static Solid? SweepBox(Vec3 a, Vec3 b, double width, double height, string? tag = null)
    {
        var axis = b - a;
        var l = axis.Length;
        if (l < 1e-12 || !(width > 0) || !(height > 0)) return null;
        var dir = axis * (1 / l);
        var worldUp = Math.Abs(dir.Z) > 0.99 ? new Vec3(1, 0, 0) : Vec3.UnitZ;
        var side = Vec3.Cross(dir, worldUp).Normalized();
        var up = Vec3.Cross(side, dir);
        double hw = width / 2, hh = height / 2;
        Vec3 C(double sw, double sh) => a + side * (sw * hw) + up * (sh * hh);
        return Extrude([C(-1, -1), C(1, -1), C(1, 1), C(-1, 1)], null, axis, tag);
    }

    /// <summary>Regular n-gon prism, the stand-in for round columns (by the chord rule when <paramref name="sides"/> is 0).</summary>
    public static Solid? Cylinder(Vec2 center, double baseZ, double radius, double height, int sides = 0, string? tag = null)
    {
        if (sides == 0) sides = Chords.Circle(radius);
        if (!(radius > 0) || !(height > 0) || sides < 3) return null;
        var fp = Enumerable.Range(0, sides).Select(i =>
        {
            var a = 2 * Math.PI * i / sides;
            return new Vec2(center.X + radius * Math.Cos(a), center.Y + radius * Math.Sin(a));
        }).ToArray();
        return ExtrudeFootprint(fp, baseZ, height, tag);
    }
}
