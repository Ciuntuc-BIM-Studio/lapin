using Lapin.Geometry;

namespace Lapin.Civil;

/// <summary>
/// A regular elevation grid (from a GeoTIFF DEM, a grayscale heightmap, online elevation tiles, or a binned LiDAR cloud)
/// with an affine transform mapping pixel coordinates (col, row) to map coordinates (E, N), and adaptive error-driven
/// TIN simplification (Delatin / Garland–Heckbert style) so dense rasters become light Lapin terrains.
/// </summary>
public sealed class ElevationGrid
{
    public int Width { get; }
    public int Height { get; }

    /// <summary>Row-major elevations (row 0 = top/north); <see cref="double.NaN"/> marks nodata.</summary>
    public double[] Heights { get; }

    /// <summary>Six-number affine transform (A, D, B, E, C, F) as a world file: pixel centre (col, row) → map (X, Y).</summary>
    public double[] Transform { get; }

    /// <summary>Optional EPSG code (e.g. "EPSG:4326", "EPSG:3844") read from the file.</summary>
    public string? Crs { get; }

    public ElevationGrid(int width, int height, double[] heights, double[] transform, string? crs = null)
    {
        if (width < 2 || height < 2) throw new ArgumentException("An elevation grid needs at least 2×2 cells.");
        if (heights.Length != width * height) throw new ArgumentException("Height array length must equal width × height.");
        if (transform.Length < 6) throw new ArgumentException("Transform needs six affine coefficients.");
        Width = width;
        Height = height;
        Heights = heights;
        Transform = transform[..6];
        Crs = crs;
    }

    public double At(int col, int row) => Heights[row * Width + col];

    public (double X, double Y) MapAt(double col, double row) =>
        (Transform[0] * col + Transform[2] * row + Transform[4],
         Transform[1] * col + Transform[3] * row + Transform[5]);

    /// <summary>Builds an affine transform from four map corners (top-left, top-right, bottom-right, bottom-left) for pixel edges.</summary>
    public static double[] TransformFromCorners((double X, double Y)[] corners, int w, int h)
    {
        // Pixel edges span [-0.5 .. w - 0.5] × [-0.5 .. h - 0.5]; corners[0]=TL, corners[1]=TR, corners[3]=BL.
        var (tl, tr, bl) = (corners[0], corners[1], corners[3]);
        var a = (tr.X - tl.X) / w;
        var d = (tr.Y - tl.Y) / w;
        var b = (bl.X - tl.X) / h;
        var e = (bl.Y - tl.Y) / h;
        var c = tl.X + 0.5 * a + 0.5 * b;
        var f = tl.Y + 0.5 * d + 0.5 * e;
        return [a, d, b, e, c, f];
    }

    /// <summary>
    /// Samples valid map coordinates from the grid (used to guess the CRS and tie the project's survey point).
    /// </summary>
    public List<(double X, double Y)> SampleMapCoords(int maxSamples = 256)
    {
        var list = new List<(double X, double Y)>(Math.Min(maxSamples, Width * Height));
        var stepX = Math.Max(1, Width / 16);
        var stepY = Math.Max(1, Height / 16);
        for (var r = 0; r < Height && list.Count < maxSamples; r += stepY)
            for (var c = 0; c < Width && list.Count < maxSamples; c += stepX)
                if (double.IsFinite(At(c, r))) list.Add(MapAt(c, r));
        if (list.Count == 0)
            for (var r = 0; r < Height && list.Count < maxSamples; r++)
                for (var c = 0; c < Width && list.Count < maxSamples; c++)
                    if (double.IsFinite(At(c, r))) list.Add(MapAt(c, r));
        return list;
    }

    /// <summary>
    /// Adaptive error-driven TIN simplification (greedy insertion of the pixel with maximum vertical error
    /// inside the current Delaunay triangulation). Flat areas stay coarse while ridges, slopes and valleys
    /// receive dense vertices. Returns points in plan coordinates via <paramref name="place"/>.
    /// </summary>
    public List<Vec3> Simplify(Func<double, double, Vec2> place, double maxError = 0.25, int maxPoints = 3000)
    {
        maxError = Math.Max(1e-4, maxError);
        maxPoints = Math.Clamp(maxPoints, 4, 25000);

        // Find the valid bounding box (skipping nodata borders).
        int minC = Width, maxC = -1, minR = Height, maxR = -1;
        var validCount = 0;
        for (var r = 0; r < Height; r++)
        {
            var rowOff = r * Width;
            for (var c = 0; c < Width; c++)
            {
                if (!double.IsFinite(Heights[rowOff + c])) continue;
                validCount++;
                if (c < minC) minC = c;
                if (c > maxC) maxC = c;
                if (r < minR) minR = r;
                if (r > maxR) maxR = r;
            }
        }
        if (validCount < 3 || maxC <= minC || maxR <= minR) return [];

        // If the grid is already small enough, return all valid cells directly (no simplification loss).
        if (validCount <= maxPoints && validCount <= 400)
        {
            var direct = new List<Vec3>(validCount);
            for (var r = minR; r <= maxR; r++)
                for (var c = minC; c <= maxC; c++)
                {
                    var z = At(c, r);
                    if (!double.IsFinite(z)) continue;
                    var (mx, my) = MapAt(c, r);
                    var q = place(mx, my);
                    direct.Add(new Vec3(q.X, q.Y, z));
                }
            return direct;
        }

        // Fill isolated nodata corners by nearest valid pixel so the initial 2-triangle frame always covers [minC..maxC]×[minR..maxR].
        double CornerZ(int tc, int tr)
        {
            var z = At(tc, tr);
            if (double.IsFinite(z)) return z;
            var bestD = long.MaxValue;
            var bestZ = 0.0;
            for (var r = minR; r <= maxR; r++)
            {
                var off = r * Width;
                for (var c = minC; c <= maxC; c++)
                {
                    var v = Heights[off + c];
                    if (!double.IsFinite(v)) continue;
                    var d = (long)(c - tc) * (c - tc) + (long)(r - tr) * (r - tr);
                    if (d < bestD) { bestD = d; bestZ = v; }
                }
            }
            return bestZ;
        }

        // Incremental Delaunay in grid-index space (col, row) with priority queue on max vertical error per triangle.
        var pts = new List<(int C, int R, double Z)>(Math.Min(maxPoints, validCount))
        {
            (minC, minR, CornerZ(minC, minR)),
            (maxC, minR, CornerZ(maxC, minR)),
            (maxC, maxR, CornerZ(maxC, maxR)),
            (minC, maxR, CornerZ(minC, maxR)),
        };
        var used = new HashSet<long>
        {
            Key(minC, minR), Key(maxC, minR), Key(maxC, maxR), Key(minC, maxR),
        };
        static long Key(int c, int r) => ((long)r << 32) | (uint)c;

        var tris = new List<Tri>();
        var owner = new Dictionary<(int, int), int>();
        var queue = new PriorityQueue<(int TriIdx, int ErrC, int ErrR, double Err), double>();

        void AddTri(int a, int b, int c)
        {
            var (ax, ay, _) = pts[a];
            var (bx, by, _) = pts[b];
            var (cx, cy, _) = pts[c];
            // Ensure CCW in (col, row) plane (positive signed area).
            if ((long)(bx - ax) * (cy - ay) - (long)(by - ay) * (cx - ax) < 0) (b, c) = (c, b);
            (ax, ay, _) = pts[a]; (bx, by, _) = pts[b]; (cx, cy, _) = pts[c];
            var d = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(d) < 1e-12) return;
            double a2 = (double)ax * ax + (double)ay * ay;
            double b2 = (double)bx * bx + (double)by * by;
            double c2 = (double)cx * cx + (double)cy * cy;
            var ox = (a2 * (by - cy) + b2 * (cy - ay) + c2 * (ay - by)) / d;
            var oy = (a2 * (cx - bx) + b2 * (ax - cx) + c2 * (bx - ax)) / d;
            var r2 = (ax - ox) * (ax - ox) + (ay - oy) * (ay - oy);

            var idx = tris.Count;
            tris.Add(new Tri(a, b, c, ox, oy, r2, true));
            owner[(a, b)] = idx;
            owner[(b, c)] = idx;
            owner[(c, a)] = idx;

            var (ec, er, err) = WorstPixel(pts[a], pts[b], pts[c]);
            if (err > maxError && !used.Contains(Key(ec, er)))
                queue.Enqueue((idx, ec, er, err), -err);
        }

        (int C, int R, double Err) WorstPixel((int C, int R, double Z) pa, (int C, int R, double Z) pb, (int C, int R, double Z) pc)
        {
            var c0 = Math.Max(minC, Math.Min(pa.C, Math.Min(pb.C, pc.C)));
            var c1 = Math.Min(maxC, Math.Max(pa.C, Math.Max(pb.C, pc.C)));
            var r0 = Math.Max(minR, Math.Min(pa.R, Math.Min(pb.R, pc.R)));
            var r1 = Math.Min(maxR, Math.Max(pa.R, Math.Max(pb.R, pc.R)));
            if (c1 - c0 <= 1 && r1 - r0 <= 1) return (c0, r0, 0);

            // Plane equation z = za + dzdc * (c - ca) + dzdr * (r - ra).
            double v1c = pb.C - pa.C, v1r = pb.R - pa.R, v1z = pb.Z - pa.Z;
            double v2c = pc.C - pa.C, v2r = pc.R - pa.R, v2z = pc.Z - pa.Z;
            var det = v1c * v2r - v1r * v2c;
            if (Math.Abs(det) < 1e-12) return (c0, r0, 0);
            var invDet = 1.0 / det;
            var dzdc = (v1z * v2r - v2z * v1r) * invDet;
            var dzdr = (v1c * v2z - v2c * v1z) * invDet;

            // For very large triangles on huge rasters, stride slightly to stay O(N) fast while never missing features.
            var areaPixels = (long)(c1 - c0 + 1) * (r1 - r0 + 1);
            var stride = areaPixels > 120_000 ? 4 : areaPixels > 30_000 ? 2 : 1;

            int bestC = c0, bestR = r0;
            var bestErr = 0.0;
            // Edge functions for CCW triangle (a, b, c): e >= 0 means inside.
            long abC = pb.C - pa.C, abR = pb.R - pa.R;
            long bcC = pc.C - pb.C, bcR = pc.R - pb.R;
            long caC = pa.C - pc.C, caR = pa.R - pc.R;

            for (var r = r0; r <= r1; r += stride)
            {
                var rowOff = r * Width;
                var zRowBase = pa.Z + dzdr * (r - pa.R);
                for (var c = c0; c <= c1; c += stride)
                {
                    if (abC * (r - pa.R) - abR * (c - pa.C) < 0) continue;
                    if (bcC * (r - pb.R) - bcR * (c - pb.C) < 0) continue;
                    if (caC * (r - pc.R) - caR * (c - pc.C) < 0) continue;
                    var actual = Heights[rowOff + c];
                    if (!double.IsFinite(actual)) continue;
                    var interp = zRowBase + dzdc * (c - pa.C);
                    var err = Math.Abs(actual - interp);
                    if (err > bestErr) { bestErr = err; bestC = c; bestR = r; }
                }
            }
            return (bestC, bestR, bestErr);
        }

        // Initial 2 triangles covering the bounding box.
        AddTri(0, 1, 2);
        AddTri(0, 2, 3);

        var cavity = new List<int>();
        var queued = new HashSet<int>();
        var stack = new Stack<int>();
        var edgeCount = new Dictionary<(int, int), int>();
        var directed = new List<(int A, int B)>();

        while (pts.Count < maxPoints && queue.TryDequeue(out var item, out _))
        {
            if (!tris[item.TriIdx].Alive) continue;
            if (item.Err <= maxError) break;
            if (!used.Add(Key(item.ErrC, item.ErrR))) continue;

            var pIdx = pts.Count;
            pts.Add((item.ErrC, item.ErrR, At(item.ErrC, item.ErrR)));

            // Bowyer–Watson cavity starting from item.TriIdx (guaranteed to contain the new point).
            cavity.Clear();
            queued.Clear();
            stack.Clear();
            stack.Push(item.TriIdx);
            queued.Add(item.TriIdx);

            while (stack.TryPop(out var t))
            {
                var tr = tris[t];
                if (!tr.Alive) continue;
                if (t != item.TriIdx)
                {
                    var dx = item.ErrC - tr.Ox;
                    var dy = item.ErrR - tr.Oy;
                    if (dx * dx + dy * dy > tr.R2 * (1 + 1e-11)) continue;
                }
                cavity.Add(t);
                TryPushNeighbor(tr.B, tr.A);
                TryPushNeighbor(tr.C, tr.B);
                TryPushNeighbor(tr.A, tr.C);
            }

            void TryPushNeighbor(int u, int v)
            {
                if (owner.TryGetValue((u, v), out var nb) && tris[nb].Alive && queued.Add(nb))
                    stack.Push(nb);
            }

            edgeCount.Clear();
            directed.Clear();
            foreach (var t in cavity)
            {
                var tr = tris[t];
                tris[t] = tr with { Alive = false };
                RemoveEdge(tr.A, tr.B, t);
                RemoveEdge(tr.B, tr.C, t);
                RemoveEdge(tr.C, tr.A, t);
            }

            void RemoveEdge(int u, int v, int t)
            {
                if (owner.TryGetValue((u, v), out var o) && o == t) owner.Remove((u, v));
                var k = u < v ? (u, v) : (v, u);
                edgeCount[k] = edgeCount.GetValueOrDefault(k) + 1;
                directed.Add((u, v));
            }

            foreach (var (u, v) in directed)
            {
                var k = u < v ? (u, v) : (v, u);
                if (edgeCount[k] == 1) AddTri(u, v, pIdx);
            }
        }

        // Convert selected grid points to model plan coordinates.
        var result = new List<Vec3>(pts.Count);
        foreach (var (c, r, z) in pts)
        {
            if (!double.IsFinite(z)) continue;
            var (mx, my) = MapAt(c, r);
            var q = place(mx, my);
            result.Add(new Vec3(q.X, q.Y, z));
        }
        return result;
    }

    readonly record struct Tri(int A, int B, int C, double Ox, double Oy, double R2, bool Alive);
}
