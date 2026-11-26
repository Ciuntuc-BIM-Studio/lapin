using System.Runtime.CompilerServices;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.PointClouds;

namespace Lapin.Civil;

/// <summary>
/// Extracts volumetric context buildings from a linked classified LiDAR / point cloud (ASPRS class 6 = Building):
/// bins building points onto a 2D occupancy &amp; roof-elevation grid, segments individual structures via 4-connected
/// components, traces each cluster's outer boundary loop, simplifies with Douglas–Peucker, and extrudes LoD1 prisms.
/// <para>All <c>Lapin.PointClouds</c> references stay inside <see cref="Impl"/> for browser WASM lazy-loading.</para>
/// </summary>
public static class PointCloudBuildings
{
    public sealed record Building(string Name, List<Vec2> Footprint, double BaseZ, double TopZ, double Area);
    public sealed record Result(List<Building> Buildings, long SourcePoints, double GridStep);

    public static Result Extract(IModelView m, Entity link, double gridStep = 1.0, double minArea = 12.0, double minHeight = 2.0) =>
        Impl.Extract(m, link, gridStep, minArea, minHeight);

    static class Impl
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Result Extract(IModelView m, Entity link, double gridStep, double minArea, double minHeight)
        {
            var store = PointCloudLinks.StoreOf(link)
                ?? throw new CommandException("The point cloud is still indexing or missing; wait until it finishes loading.");
            if (!store.HasClassification || store.Classes[6] < 3)
                throw new CommandException("The point cloud has no building-classified points (ASPRS class 6).");

            var t = PointCloudLinks.TransformOf(m, link);
            var box = PointCloudLinks.BoxOf(t, store.Bounds[0], store.Bounds[1], store.Bounds[2], store.Bounds[3], store.Bounds[4], store.Bounds[5]);
            double minX = box[0], minY = box[1], maxX = box[3], maxY = box[4];
            double spanX = maxX - minX, spanY = maxY - minY;
            if (spanX < 1 || spanY < 1) throw new CommandException("The point cloud has no horizontal extent.");

            var step = Math.Clamp(gridStep, 0.25, 10.0);
            var minStep = Math.Max(spanX, spanY) / 1024.0;
            if (step < minStep) step = minStep;

            var cols = Math.Max(2, (int)Math.Ceiling(spanX / step) + 1);
            var rows = Math.Max(2, (int)Math.Ceiling(spanY / step) + 1);
            var roofZ = new double[cols * rows];
            var groundZ = new double[cols * rows];
            Array.Fill(roofZ, double.NaN);
            Array.Fill(groundZ, double.NaN);

            long buildingPts = 0;
            double globalGroundMin = double.PositiveInfinity;

            foreach (var node in store.Nodes)
            {
                var pts = store.Read(node.Name);
                for (var i = 0; i < pts.Count; i++)
                {
                    var cls = pts.Class(i);
                    if (cls != 6 && cls != 2) continue;
                    var (x, y, z) = PointCloudLinks.Apply(t, pts.X(i), pts.Y(i), pts.Z(i));
                    if (!(double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z))) continue;
                    var c = Math.Clamp((int)Math.Floor((x - minX) / step), 0, cols - 1);
                    var r = Math.Clamp((int)Math.Floor((y - minY) / step), 0, rows - 1);
                    var idx = r * cols + c;
                    if (cls == 6)
                    {
                        buildingPts++;
                        if (double.IsNaN(roofZ[idx]) || z > roofZ[idx]) roofZ[idx] = z;
                    }
                    else
                    {
                        if (z < globalGroundMin) globalGroundMin = z;
                        if (double.IsNaN(groundZ[idx]) || z < groundZ[idx]) groundZ[idx] = z;
                    }
                }
            }

            if (buildingPts < 3)
                throw new CommandException("The point cloud has fewer than 3 building-classified points.");

            // Fill isolated 1-cell interior holes (e.g. skylights or sparse scan gaps) so footprints stay solid.
            var occ = new bool[cols * rows];
            for (var i = 0; i < occ.Length; i++) occ[i] = double.IsFinite(roofZ[i]);
            var filled = (bool[])occ.Clone();
            for (var r = 1; r < rows - 1; r++)
                for (var c = 1; c < cols - 1; c++)
                {
                    var idx = r * cols + c;
                    if (!occ[idx] && occ[idx - 1] && occ[idx + 1] && occ[idx - cols] && occ[idx + cols])
                    {
                        filled[idx] = true;
                        roofZ[idx] = (roofZ[idx - 1] + roofZ[idx + 1] + roofZ[idx - cols] + roofZ[idx + cols]) * 0.25;
                    }
                }

            var terrains = ContextBuildings.AllTerrains(m);
            var label = new int[cols * rows];
            var queue = new Queue<(int C, int R)>();
            var buildings = new List<Building>();
            var cellArea = step * step;
            var nextLabel = 0;

            for (var r0 = 0; r0 < rows; r0++)
            {
                for (var c0 = 0; c0 < cols; c0++)
                {
                    var startIdx = r0 * cols + c0;
                    if (!filled[startIdx] || label[startIdx] != 0) continue;
                    nextLabel++;
                    var cells = new List<(int C, int R)>();
                    var heights = new List<double>();
                    label[startIdx] = nextLabel;
                    queue.Enqueue((c0, r0));

                    while (queue.Count > 0)
                    {
                        var (c, r) = queue.Dequeue();
                        cells.Add((c, r));
                        var z = roofZ[r * cols + c];
                        if (double.IsFinite(z)) heights.Add(z);

                        TryPush(c + 1, r);
                        TryPush(c - 1, r);
                        TryPush(c, r + 1);
                        TryPush(c, r - 1);
                    }

                    void TryPush(int nc, int nr)
                    {
                        if (nc < 0 || nc >= cols || nr < 0 || nr >= rows) return;
                        var ni = nr * cols + nc;
                        if (filled[ni] && label[ni] == 0) { label[ni] = nextLabel; queue.Enqueue((nc, nr)); }
                    }

                    var clusterArea = cells.Count * cellArea;
                    if (clusterArea < minArea || heights.Count == 0) continue;

                    var footprint = TraceOuterBoundary(cells, label, cols, rows, nextLabel, minX, minY, step);
                    if (footprint.Count < 3) continue;
                    footprint = SimplifyClosed(footprint, step * 0.85);
                    if (footprint.Count < 3) continue;
                    var polyArea = Math.Abs(Polygon2.SignedArea(footprint));
                    if (polyArea < minArea) continue;

                    heights.Sort();
                    var topZ = heights[(int)Math.Floor((heights.Count - 1) * 0.85)];

                    // Estimate baseZ from terrain, else nearest ground class cells around the cluster, else global ground.
                    var baseZ = ContextBuildings.GroundUnder(terrains, footprint, double.NaN);
                    if (!double.IsFinite(baseZ))
                        baseZ = NearbyGround(cells, groundZ, cols, rows, double.IsFinite(globalGroundMin) ? globalGroundMin : topZ - 6.0);

                    if (topZ - baseZ < minHeight) continue;
                    buildings.Add(new Building($"Building {buildings.Count + 1}", footprint, baseZ, topZ, polyArea));
                }
            }

            if (buildings.Count == 0)
                throw new CommandException("No building volumes larger than the minimum area were found in class 6.");

            return new Result(buildings, buildingPts, step);
        }

        static double NearbyGround(List<(int C, int R)> cells, double[] groundZ, int cols, int rows, double fallback)
        {
            double sum = 0;
            int cnt = 0;
            foreach (var (c, r) in cells)
            {
                for (var dr = -2; dr <= 2; dr++)
                    for (var dc = -2; dc <= 2; dc++)
                    {
                        int nc = c + dc, nr = r + dr;
                        if (nc < 0 || nc >= cols || nr < 0 || nr >= rows) continue;
                        var g = groundZ[nr * cols + nc];
                        if (double.IsFinite(g)) { sum += g; cnt++; }
                    }
            }
            return cnt > 0 ? sum / cnt : fallback;
        }

        /// <summary>Traces the largest CCW outer boundary loop of a labelled grid cluster.</summary>
        static List<Vec2> TraceOuterBoundary(List<(int C, int R)> cells, int[] label, int cols, int rows, int id, double minX, double minY, double step)
        {
            bool In(int c, int r) => c >= 0 && c < cols && r >= 0 && r < rows && label[r * cols + c] == id;

            // Collect directed boundary edges (CCW around occupied cells in XY: +X right, +Y up).
            var nextMap = new Dictionary<(int X, int Y), List<(int X, int Y)>>();
            void AddEdge((int, int) a, (int, int) b)
            {
                if (!nextMap.TryGetValue(a, out var l)) nextMap[a] = l = [];
                l.Add(b);
            }

            foreach (var (c, r) in cells)
            {
                if (!In(c, r - 1)) AddEdge((c, r), (c + 1, r));         // bottom edge → right
                if (!In(c + 1, r)) AddEdge((c + 1, r), (c + 1, r + 1)); // right edge → up
                if (!In(c, r + 1)) AddEdge((c + 1, r + 1), (c, r + 1)); // top edge → left
                if (!In(c - 1, r)) AddEdge((c, r + 1), (c, r));         // left edge → down
            }

            var used = new HashSet<((int, int), (int, int))>();
            List<Vec2>? best = null;
            double bestArea = 0;

            foreach (var (start, targets) in nextMap)
            {
                foreach (var first in targets)
                {
                    if (!used.Add((start, first))) continue;
                    var loop = new List<(int X, int Y)> { start };
                    var prev = start;
                    var cur = first;
                    var guard = nextMap.Count * 4 + 16;
                    while (cur != start && guard-- > 0)
                    {
                        loop.Add(cur);
                        if (!nextMap.TryGetValue(cur, out var outs)) break;
                        // Pick an unused outgoing edge (preferring right-turn / outer contour at pinch vertices).
                        (int X, int Y)? chosen = null;
                        foreach (var cand in outs)
                            if (used.Add((cur, cand))) { chosen = cand; break; }
                        if (chosen == null) break;
                        prev = cur;
                        cur = chosen.Value;
                    }
                    if (cur != start || loop.Count < 4) continue;
                    var poly = loop.Select(v => new Vec2(minX + v.X * step, minY + v.Y * step)).ToList();
                    var area = Polygon2.SignedArea(poly);
                    if (area > bestArea) { bestArea = area; best = poly; }
                }
            }
            return best ?? [];
        }

        /// <summary>Simplifies a closed polygon with Douglas–Peucker (removes stair-stepping from grid cells).</summary>
        static List<Vec2> SimplifyClosed(List<Vec2> poly, double eps)
        {
            if (poly.Count <= 4) return poly;
            // Find the vertex farthest from vertex 0 to split the closed ring into two open chains.
            var far = 1;
            double maxD = -1;
            for (var i = 1; i < poly.Count; i++)
            {
                var d = (poly[i] - poly[0]).Length;
                if (d > maxD) { maxD = d; far = i; }
            }
            var a = Dp(poly.GetRange(0, far + 1), eps);
            var rest = poly.GetRange(far, poly.Count - far);
            rest.Add(poly[0]);
            var b = Dp(rest, eps);
            var merged = new List<Vec2>(a.Count + b.Count);
            merged.AddRange(a);
            for (var i = 1; i < b.Count - 1; i++) merged.Add(b[i]);
            return merged;
        }

        static List<Vec2> Dp(List<Vec2> pts, double eps)
        {
            if (pts.Count <= 2) return pts;
            double maxD = -1;
            var idx = 0;
            var (a, b) = (pts[0], pts[^1]);
            var ab = b - a;
            var len = ab.Length;
            for (var i = 1; i < pts.Count - 1; i++)
            {
                var d = len > 1e-9 ? Math.Abs(ab.X * (pts[i].Y - a.Y) - ab.Y * (pts[i].X - a.X)) / len : (pts[i] - a).Length;
                if (d > maxD) { maxD = d; idx = i; }
            }
            if (maxD <= eps) return [a, b];
            var left = Dp(pts.GetRange(0, idx + 1), eps);
            var right = Dp(pts.GetRange(idx, pts.Count - idx), eps);
            left.RemoveAt(left.Count - 1);
            left.AddRange(right);
            return left;
        }
    }
}
