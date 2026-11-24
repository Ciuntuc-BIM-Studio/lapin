namespace Lapin.PointClouds;

/// <summary>
/// Where a cloud's points are dense: the box Fit frames and "Fit to cloud" proposes as a crop, so that the floaters round a
/// Gaussian splat capture (sky, background, a few metres of noise scattered over hundreds) do not make the cloud look ten times
/// its size, while a scan without outliers keeps its whole box.
/// <para>Counted once, while the cloud is indexed (<see cref="Indexer"/>: every point into the octree's cube cut into
/// <see cref="Grid"/>³ cells, kept as <c>core</c> in the index's meta), or for an index made before, from its node counts
/// alone; a cell's count is the density there. The cells at least 1/<see cref="Thin"/> as dense as where a typical point lies (the
/// count-weighted median of the cells) are the cloud's body; per axis, the box holding all of the body's points but the
/// <see cref="Trim"/> at each end (a dense clump far away, a few cells, does not stretch it); a side within
/// <see cref="Snap"/> of the cloud's own side is the cloud's (a scan without outliers gets its box exactly). The precision is
/// a cell, 1/128 of the cube.</para>
/// </summary>
public static class DenseBox
{
    public const int Depth = 7;
    public const int Grid = 1 << Depth;
    /// <summary>How much thinner than a typical point's cell a cell may be and still count as the cloud's body.</summary>
    public const double Thin = 50;
    /// <summary>The share of the body's points left out at each end of an axis.</summary>
    public const double Trim = 0.001;
    /// <summary>A side this close to the cloud's own (a share of its extent on that axis) is the cloud's.</summary>
    public const double Snap = 0.05;

    /// <summary>The dense box of an index without one kept (min x, y, z, max x, y, z), in the file's units; its whole box when it is too small to tell.</summary>
    public static double[] Of(PointStore s) => Of(s.Min, s.Size, s.Bounds, s.Nodes.Select(n => (n.Name, (long)n.Count)));

    /// <summary>
    /// The same from an octree's cube (its least corner, its edge), the points' own box and the nodes' names and counts (an
    /// index made before the dense box was kept: each node's points spread evenly over the cells its cube covers, so the
    /// precision is the leaves' cubes').
    /// </summary>
    public static double[] Of(double[] min, double size, double[] bounds, IEnumerable<(string Name, long Count)> nodes)
    {
        var cells = new float[Grid * Grid * Grid];
        foreach (var (name, count) in nodes)
        {
            if (count <= 0 || name.Length == 0) continue;
            // The node's cell coordinates at its own depth, to the grid's at most.
            int x = 0, y = 0, z = 0, depth = Math.Min(name.Length - 1, Depth);
            for (var i = 1; i <= depth; i++)
            {
                var k = name[i] - '0';
                x = 2 * x + ((k >> 2) & 1); y = 2 * y + ((k >> 1) & 1); z = 2 * z + (k & 1);
            }
            var span = 1 << (Depth - depth);
            x *= span; y *= span; z *= span;
            var share = (float)(count / ((double)span * span * span));
            for (var i = 0; i < span; i++)
                for (var j = 0; j < span; j++)
                {
                    var row = ((x + i) * Grid + y + j) * Grid + z;
                    for (var k = 0; k < span; k++) cells[row + k] += share;
                }
        }
        return FromCells(cells, min, size, bounds);
    }

    /// <summary>The cell of a point given in steps from the cube's corner (<paramref name="steps"/>: the cube's edge in steps, per axis), as the indexer counts them.</summary>
    public static int CellOf(long x, long y, long z, long[] steps) =>
        ((int)Math.Min(Grid - 1, x * Grid / steps[0]) * Grid + (int)Math.Min(Grid - 1, y * Grid / steps[1])) * Grid + (int)Math.Min(Grid - 1, z * Grid / steps[2]);

    /// <summary>The dense box from the points counted in the cube's <see cref="Grid"/>³ cells (x major, then y, then z).</summary>
    public static double[] FromCells(float[] cells, double[] min, double size, double[] bounds)
    {
        var whole = (double[])bounds.Clone();
        if (!(size > 0) || bounds.Length != 6 || min.Length != 3 || cells.Length != Grid * Grid * Grid) return whole;
        double total = 0;
        foreach (var c in cells) total += c;
        if (total < 100) return whole;

        // A typical point's cell: the count-weighted median of the cells' counts.
        var filled = cells.Where(c => c > 0).ToArray();
        Array.Sort(filled);
        double acc = 0, typical = filled[^1];
        for (var i = 0; i < filled.Length; i++) { acc += filled[i]; if (acc >= total / 2) { typical = filled[i]; break; } }
        var floor = typical / Thin;

        // The body's points along each axis.
        var hx = new double[Grid]; var hy = new double[Grid]; var hz = new double[Grid];
        double body = 0;
        for (var i = 0; i < Grid; i++)
            for (var j = 0; j < Grid; j++)
            {
                var row = (i * Grid + j) * Grid;
                for (var k = 0; k < Grid; k++)
                {
                    var c = cells[row + k];
                    if (c < floor || c <= 0) continue;
                    hx[i] += c; hy[j] += c; hz[k] += c;
                    body += c;
                }
            }
        if (!(body > 0)) return whole;
        var cell = size / Grid;
        var box = new double[6];
        double[][] hist = [hx, hy, hz];
        for (var a = 0; a < 3; a++)
        {
            int lo = 0, hi = Grid - 1;
            for (double run = 0; lo < Grid - 1; lo++) { run += hist[a][lo]; if (run > Trim * body) break; }
            for (double run = 0; hi > 0; hi--) { run += hist[a][hi]; if (run > Trim * body) break; }
            if (hi < lo) (lo, hi) = (hi, lo);
            var extent = Math.Max(bounds[a + 3] - bounds[a], 1e-12);
            var b0 = Math.Clamp(min[a] + lo * cell, bounds[a], bounds[a + 3]);
            var b1 = Math.Clamp(min[a] + (hi + 1) * cell, bounds[a], bounds[a + 3]);
            box[a] = b0 - bounds[a] <= Snap * extent ? bounds[a] : b0;
            box[a + 3] = bounds[a + 3] - b1 <= Snap * extent ? bounds[a + 3] : b1;
        }
        return box;
    }
}
