using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.PointClouds;

namespace Lapin.Civil;

/// <summary>
/// Generates a Lapin 3D terrain (TIN) from a linked LiDAR / point cloud (LAS, LAZ, E57, PLY, XYZ)
/// via <see cref="PointCloudLinks"/>: filters by ASPRS ground classification (class 2) when present,
/// bins onto a regular grid using a robust lower-median elevation per cell (suppressing stray noise),
/// and runs <see cref="ElevationGrid.Simplify"/> to keep the terrain light and accurate.
/// <para>All <c>Lapin.PointClouds</c> types stay inside <see cref="Impl"/> so the browser engine only
/// downloads <c>Lapin.PointClouds.wasm</c> when a point cloud is actually used.</para>
/// </summary>
public static class PointCloudTerrain
{
    public sealed record Result(List<Vec3> Points, long SourcePoints, bool UsedGroundClass, double GridStep, int GridWidth, int GridHeight);

    public static JsonObject Info(IModelView m, Entity link) => Impl.Info(m, link);

    public static Result Build(IModelView m, Entity link, bool groundOnly, HashSet<byte>? classes, double gridStep, double maxError, int maxPoints) =>
        Impl.Build(m, link, groundOnly, classes, gridStep, maxError, maxPoints);

    static class Impl
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static JsonObject Info(IModelView m, Entity link)
        {
            var store = PointCloudLinks.StoreOf(link)
                ?? throw new CommandException("The point cloud is still indexing or missing; wait until it finishes loading.");
            var ground = store.HasClassification ? store.Classes[2] : 0;
            var buildings = store.HasClassification ? store.Classes[6] : 0;
            return new JsonObject
            {
                ["name"] = link.Name,
                ["points"] = store.Points,
                ["groundPoints"] = ground,
                ["buildingPoints"] = buildings,
                ["hasClassification"] = store.HasClassification,
                ["crs"] = store.Crs,
            };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Result Build(IModelView m, Entity link, bool groundOnly, HashSet<byte>? classes, double gridStep, double maxError, int maxPoints)
        {
            var store = PointCloudLinks.StoreOf(link)
                ?? throw new CommandException("The point cloud is still indexing or missing; wait until it finishes loading.");
            var t = PointCloudLinks.TransformOf(m, link);
            var hasGround = store.HasClassification && store.Classes[2] >= 3;
            var filterGround = classes == null && groundOnly && hasGround;

            // Compute projected XY bounds from the cloud's 3D bounding box.
            var box = PointCloudLinks.BoxOf(t, store.Bounds[0], store.Bounds[1], store.Bounds[2], store.Bounds[3], store.Bounds[4], store.Bounds[5]);
            var minX = box[0]; var minY = box[1];
            var maxX = box[3]; var maxY = box[4];
            var spanX = maxX - minX;
            var spanY = maxY - minY;
            if (spanX < 1e-3 || spanY < 1e-3)
                throw new CommandException("The point cloud has no horizontal extent to build a terrain from.");

            // Clamp gridStep so the binned grid stays within a reasonable memory budget (<= 1024×1024 cells).
            var step = Math.Clamp(gridStep, 0.1, 100.0);
            var minStep = Math.Max(spanX, spanY) / 1024.0;
            if (step < minStep) step = minStep;

            var cols = Math.Max(2, (int)Math.Ceiling(spanX / step) + 1);
            var rows = Math.Max(2, (int)Math.Ceiling(spanY / step) + 1);
            var cells = new List<double>?[cols * rows];
            long used = 0;

            foreach (var node in store.Nodes)
            {
                var pts = store.Read(node.Name);
                for (var i = 0; i < pts.Count; i++)
                {
                    if (classes != null)
                    {
                        if (!classes.Contains(pts.Class(i))) continue;
                    }
                    else if (filterGround && pts.Class(i) != 2)
                        continue;

                    var (x, y, z) = PointCloudLinks.Apply(t, pts.X(i), pts.Y(i), pts.Z(i));
                    if (!(double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z))) continue;
                    var c = Math.Clamp((int)Math.Floor((x - minX) / step), 0, cols - 1);
                    var r = Math.Clamp((int)Math.Floor((maxY - y) / step), 0, rows - 1);
                    var idx = r * cols + c;
                    var list = cells[idx];
                    if (list == null) cells[idx] = list = new List<double>(4);
                    // Cap per-cell samples to avoid huge allocations on ultra-dense scans.
                    if (list.Count < 32) list.Add(z);
                    used++;
                }
            }

            if (used < 3)
                throw new CommandException(filterGround
                    ? "The point cloud has fewer than 3 ground-classified points."
                    : "The point cloud has fewer than 3 valid points.");

            var heights = new double[cols * rows];
            for (var i = 0; i < heights.Length; i++)
            {
                var list = cells[i];
                if (list == null || list.Count == 0) { heights[i] = double.NaN; continue; }
                list.Sort();
                // 25th percentile when many samples exist (suppresses low vegetation/noise), median for small cells.
                var pick = list.Count >= 4 ? list.Count / 4 : list.Count / 2;
                heights[i] = list[pick];
            }

            // Transform maps grid (col, row) directly into project plan coordinates (already transformed by PointCloudLinks.TransformOf).
            double[] transform = [step, 0, 0, -step, minX + 0.5 * step, maxY - 0.5 * step];
            var grid = new ElevationGrid(cols, rows, heights, transform);
            var simplified = grid.Simplify((x, y) => new Vec2(x, y), maxError, maxPoints);
            if (simplified.Count < 3)
                throw new CommandException("Could not extract a terrain surface from the point cloud.");

            return new Result(simplified, used, filterGround, step, cols, rows);
        }
    }
}
