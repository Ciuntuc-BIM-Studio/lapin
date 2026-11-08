using System.Buffers.Binary;

namespace Lapin.PointClouds;

/// <summary>
/// A cloud into its octree (<see cref="PointStore"/>), in the background, with memory bounded whatever its size (Potree
/// Converter 2's scheme, written by Lapin):
/// <list type="number">
/// <item>the points read once for their box, how many there are, the colours' depth, intensity's range and the classes; a
/// cloud of at most <see cref="InMemory"/> points is kept from that pass and indexed at once;</item>
/// <item>a larger one is counted on a grid of 128³ cells, and the cells gathered into chunks of at most
/// <see cref="ChunkMax"/> points (an octree of counts, cut where a node holds few enough);</item>
/// <item>the points read again and written to their chunk's file;</item>
/// <item>each chunk indexed alone, top down: a node keeps one point a cell of its grid and hands the rest to its eight
/// children, until a node has at most <see cref="LeafMax"/>;</item>
/// <item>the levels above the chunks made bottom up, each node a sample of its children's samples taken from them.</item>
/// </list>
/// </summary>
public static class Indexer
{
    public const int InMemory = 2_000_000;
    public const int ChunkMax = 2_000_000;
    public const int LeafMax = 30_000;
    public const int NodeCap = 90_000;
    public const int MaxDepth = 18;
    const int Grid = 128;

    /// <summary>How large a cloud is indexed in memory, a chunk at most, a leaf at most (the constants; tests take smaller ones).</summary>
    public sealed record Limits(int InMemory = InMemory, int ChunkMax = ChunkMax, int LeafMax = LeafMax);

    /// <summary>What a build reports: its stage and how far it is (0–1).</summary>
    public delegate void Progress(string stage, double done);

    /// <summary>The points of a node being built, as stored: quantized coordinates, colour, intensity, class.</summary>
    sealed class Set(int capacity)
    {
        public int[] X = new int[capacity], Y = new int[capacity], Z = new int[capacity];
        public byte[] R = new byte[capacity], G = new byte[capacity], B = new byte[capacity];
        public ushort[] I = new ushort[capacity];
        public byte[] C = new byte[capacity];
        public int Count;

        public void Add(int x, int y, int z, byte r, byte g, byte b, ushort i, byte c)
        {
            if (Count == X.Length) Grow(Math.Max(16, Count * 2));
            var k = Count++;
            X[k] = x; Y[k] = y; Z[k] = z; R[k] = r; G[k] = g; B[k] = b; I[k] = i; C[k] = c;
        }

        public void Add(Set s, int k) => Add(s.X[k], s.Y[k], s.Z[k], s.R[k], s.G[k], s.B[k], s.I[k], s.C[k]);

        void Grow(int n)
        {
            Array.Resize(ref X, n); Array.Resize(ref Y, n); Array.Resize(ref Z, n);
            Array.Resize(ref R, n); Array.Resize(ref G, n); Array.Resize(ref B, n);
            Array.Resize(ref I, n); Array.Resize(ref C, n);
        }

        public Set Pick(List<int> idx)
        {
            var o = new Set(idx.Count);
            foreach (var k in idx) o.Add(this, k);
            return o;
        }
    }

    /// <summary>
    /// The cloud indexed into <paramref name="dir"/> (made if missing; what was there replaced). <paramref name="sourceKey"/>
    /// is written into the index so the caller can tell later whether it still matches its file.
    /// </summary>
    public static PointStore Build(IPointReader reader, string dir, string sourceKey, long sourceLength, Progress? progress = null, CancellationToken cancel = default, Limits? limits = null)
    {
        var lim = limits ?? new Limits();
        System.IO.Directory.CreateDirectory(dir);
        foreach (var f in new[] { "meta.json", "points.bin" }) { var p = Path.Combine(dir, f); if (File.Exists(p)) File.Delete(p); }
        var h = reader.Header;
        var expected = Math.Max(1, h.Count ?? 0);

        // 1. The box and the statistics; a small cloud kept as it is read.
        double[] lo = [double.MaxValue, double.MaxValue, double.MaxValue], hi = [double.MinValue, double.MinValue, double.MinValue];
        long count = 0;
        ushort colorMax = 0, iMin = ushort.MaxValue, iMax = 0;
        var classes = new long[256];
        var raw = new List<(double[] X, double[] Y, double[] Z, ushort[] R, ushort[] G, ushort[] B, ushort[] I, byte[] C)>();
        long kept = 0;
        var small = true;
        foreach (var b in reader.Read(65536, cancel))
        {
            for (var i = 0; i < b.Count; i++)
            {
                double x = b.X[i], y = b.Y[i], z = b.Z[i];
                if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)) continue;
                if (x < lo[0]) lo[0] = x; if (x > hi[0]) hi[0] = x;
                if (y < lo[1]) lo[1] = y; if (y > hi[1]) hi[1] = y;
                if (z < lo[2]) lo[2] = z; if (z > hi[2]) hi[2] = z;
                var m = Math.Max(b.R[i], Math.Max(b.G[i], b.B[i]));
                if (m > colorMax) colorMax = m;
                if (b.Intensity[i] < iMin) iMin = b.Intensity[i];
                if (b.Intensity[i] > iMax) iMax = b.Intensity[i];
                classes[b.Class[i]]++;
                count++;
            }
            if (small && kept + b.Count <= lim.InMemory)
            {
                raw.Add((b.X[..b.Count], b.Y[..b.Count], b.Z[..b.Count], b.R[..b.Count], b.G[..b.Count], b.B[..b.Count], b.Intensity[..b.Count], b.Class[..b.Count]));
                kept += b.Count;
            }
            else { small = false; raw.Clear(); }
            progress?.Invoke("Reading", Math.Min(1, (double)count / expected) * (small ? 0.5 : 0.3));
        }
        if (count == 0) throw new PointCloudException("The file holds no points.");
        if (iMin > iMax) iMin = iMax = 0;

        // The cube, and the coordinates' step: the file's own when it has one fine enough, else a billionth of the cube.
        var edge = Math.Max(Math.Max(hi[0] - lo[0], hi[1] - lo[1]), hi[2] - lo[2]);
        if (!(edge > 0)) edge = 1;
        edge *= 1.0001;
        var centre = new[] { (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (lo[2] + hi[2]) / 2 };
        var min = new[] { centre[0] - edge / 2, centre[1] - edge / 2, centre[2] - edge / 2 };
        var step = new double[3];
        for (var k = 0; k < 3; k++) step[k] = Math.Max(edge / (1 << 30), h.Precision is { Length: 3 } pr && pr[k] > 0 ? pr[k] : 0);
        // The cube's corner on the file's own grid, so its coordinates come back as they were.
        for (var k = 0; k < 3; k++) if (h.Precision is { Length: 3 }) min[k] = Math.Floor(min[k] / step[k]) * step[k];
        var shift = colorMax > 255 ? 8 : 0;

        int Q(double v, int k) => (int)Math.Clamp(Math.Round((v - min[k]) / step[k]), 0, int.MaxValue);
        byte Col(ushort v) => (byte)(v >> shift);

        var nodes = new List<PointStore.Node>();
        var dataPath = Path.Combine(dir, "points.bin");
        using var data = new FileStream(dataPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
        void Emit(string name, Set s)
        {
            nodes.Add(new PointStore.Node(name, data.Position, s.Count));
            var buf = new byte[(long)s.Count * PointStore.BytesPerPoint];
            for (var i = 0; i < s.Count; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(12 * i), s.X[i]);
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(12 * i + 4), s.Y[i]);
                BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(12 * i + 8), s.Z[i]);
                buf[12 * s.Count + 3 * i] = s.R[i]; buf[12 * s.Count + 3 * i + 1] = s.G[i]; buf[12 * s.Count + 3 * i + 2] = s.B[i];
                BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(15 * s.Count + 2 * i), s.I[i]);
                buf[17 * s.Count + i] = s.C[i];
            }
            data.Write(buf);
        }

        // Cell of a quantized point in a node's grid (the node's cube, in steps).
        var builder = new Builder(edge, step, Emit, lim.LeafMax, cancel);

        if (small)
        {
            var all = new Set((int)count);
            foreach (var r in raw)
                for (var i = 0; i < r.X.Length; i++)
                {
                    if (!double.IsFinite(r.X[i]) || !double.IsFinite(r.Y[i]) || !double.IsFinite(r.Z[i])) continue;
                    all.Add(Q(r.X[i], 0), Q(r.Y[i], 1), Q(r.Z[i], 2), Col(r.R[i]), Col(r.G[i]), Col(r.B[i]), r.I[i], r.C[i]);
                }
            raw.Clear();
            progress?.Invoke("Indexing", 0.6);
            builder.Top("r", all, hold: false);
            progress?.Invoke("Indexing", 0.95);
        }
        else
        {
            // 2. Counted on the grid.
            var cells = new int[Grid * Grid * Grid];
            int Cell(int x, int y, int z) => (int)Math.Min(Grid - 1, (long)x * Grid / CubeSteps(0)) * Grid * Grid + (int)Math.Min(Grid - 1, (long)y * Grid / CubeSteps(1)) * Grid + (int)Math.Min(Grid - 1, (long)z * Grid / CubeSteps(2));
            long CubeSteps(int k) => Math.Max(1, (long)Math.Ceiling(edge / step[k]));
            long seen = 0;
            foreach (var b in reader.Read(65536, cancel))
            {
                for (var i = 0; i < b.Count; i++)
                {
                    if (!double.IsFinite(b.X[i]) || !double.IsFinite(b.Y[i]) || !double.IsFinite(b.Z[i])) continue;
                    cells[Cell(Q(b.X[i], 0), Q(b.Y[i], 1), Q(b.Z[i], 2))]++;
                }
                seen += b.Count;
                progress?.Invoke("Counting", 0.3 + 0.15 * Math.Min(1, (double)seen / count));
            }
            // The chunks: an octree of the counts, cut where a node holds at most ChunkMax (or is one cell).
            var chunkOf = new int[cells.Length];
            var chunks = new List<(string Name, int Level, int X0, int Y0, int Z0)>();
            void Cut(string name, int level, int x0, int y0, int z0)
            {
                var size = Grid >> level;
                long n = 0;
                for (var x = x0; x < x0 + size; x++) for (var y = y0; y < y0 + size; y++) for (var z = z0; z < z0 + size; z++) n += cells[(x * Grid + y) * Grid + z];
                if (n == 0) return;
                if (n <= lim.ChunkMax || size == 1)
                {
                    for (var x = x0; x < x0 + size; x++) for (var y = y0; y < y0 + size; y++) for (var z = z0; z < z0 + size; z++) chunkOf[(x * Grid + y) * Grid + z] = chunks.Count;
                    chunks.Add((name, level, x0, y0, z0));
                    return;
                }
                var half = size / 2;
                for (var k = 0; k < 8; k++) Cut(name + (char)('0' + k), level + 1, x0 + ((k & 4) != 0 ? half : 0), y0 + ((k & 2) != 0 ? half : 0), z0 + ((k & 1) != 0 ? half : 0));
            }
            Cut("r", 0, 0, 0, 0);

            // 3. Each point into its chunk's file.
            var tmp = Path.Combine(dir, "chunks");
            if (System.IO.Directory.Exists(tmp)) System.IO.Directory.Delete(tmp, true);
            System.IO.Directory.CreateDirectory(tmp);
            const int Rec = 18;
            var buffers = new MemoryStream?[chunks.Count];
            void Flush(int c)
            {
                if (buffers[c] is not { Length: > 0 } ms) return;
                using (var f = new FileStream(Path.Combine(tmp, $"{c}.bin"), FileMode.Append, FileAccess.Write)) ms.WriteTo(f);
                ms.SetLength(0);
            }
            var rec = new byte[Rec];
            seen = 0;
            foreach (var b in reader.Read(65536, cancel))
            {
                for (var i = 0; i < b.Count; i++)
                {
                    if (!double.IsFinite(b.X[i]) || !double.IsFinite(b.Y[i]) || !double.IsFinite(b.Z[i])) continue;
                    int x = Q(b.X[i], 0), y = Q(b.Y[i], 1), z = Q(b.Z[i], 2);
                    var c = chunkOf[Cell(x, y, z)];
                    BinaryPrimitives.WriteInt32LittleEndian(rec, x);
                    BinaryPrimitives.WriteInt32LittleEndian(rec.AsSpan(4), y);
                    BinaryPrimitives.WriteInt32LittleEndian(rec.AsSpan(8), z);
                    rec[12] = Col(b.R[i]); rec[13] = Col(b.G[i]); rec[14] = Col(b.B[i]);
                    BinaryPrimitives.WriteUInt16LittleEndian(rec.AsSpan(15), b.Intensity[i]);
                    rec[17] = b.Class[i];
                    var ms = buffers[c] ??= new MemoryStream(1 << 16);
                    ms.Write(rec);
                    if (ms.Length >= 1 << 18) Flush(c);
                }
                seen += b.Count;
                progress?.Invoke("Sorting", 0.45 + 0.2 * Math.Min(1, (double)seen / count));
            }
            for (var c = 0; c < chunks.Count; c++) { Flush(c); buffers[c] = null; }

            // 4. Each chunk alone; its root held for the levels above.
            var pending = new Dictionary<string, Set>();
            for (var c = 0; c < chunks.Count; c++)
            {
                cancel.ThrowIfCancellationRequested();
                var path = Path.Combine(tmp, $"{c}.bin");
                if (!File.Exists(path)) continue;
                var bytes = File.ReadAllBytes(path);
                File.Delete(path);
                var n = bytes.Length / Rec;
                var set = new Set(n);
                for (var i = 0; i < n; i++)
                {
                    var r = bytes.AsSpan(i * Rec, Rec);
                    set.Add(BinaryPrimitives.ReadInt32LittleEndian(r), BinaryPrimitives.ReadInt32LittleEndian(r[4..]), BinaryPrimitives.ReadInt32LittleEndian(r[8..]),
                        r[12], r[13], r[14], BinaryPrimitives.ReadUInt16LittleEndian(r[15..]), r[17]);
                }
                pending[chunks[c].Name] = builder.Top(chunks[c].Name, set, hold: true)!;
                progress?.Invoke("Indexing", 0.65 + 0.3 * (c + 1) / chunks.Count);
            }
            System.IO.Directory.Delete(tmp, true);

            // 5. The levels above the chunks, deepest first: a parent samples its children's held points, taking them from them.
            while (pending.Count > 0 && !(pending.Count == 1 && pending.ContainsKey("r")))
            {
                var deepest = pending.Keys.Max(k => k.Length);
                if (deepest == 1) break;
                foreach (var group in pending.Keys.Where(k => k.Length == deepest).GroupBy(k => k[..^1]).ToList())
                {
                    var parent = new Set(1024);
                    builder.Sample(group.Key, group.Select(k => pending[k]).ToList(), parent);
                    foreach (var k in group) { Emit(k, pending[k]); pending.Remove(k); }
                    pending[group.Key] = parent;
                }
            }
            if (pending.TryGetValue("r", out var root)) Emit("r", root);
        }
        data.Flush();

        var store = new PointStore(dir)
        {
            Format = h.Format, Points = count, Min = min, Size = edge, Step = step, Bounds = [lo[0], lo[1], lo[2], hi[0], hi[1], hi[2]],
            HasColor = h.HasColor && colorMax > 0, HasIntensity = h.HasIntensity && iMax > iMin, HasClassification = h.HasClassification && classes.Skip(2).Any(n => n > 0),
            IntensityMin = iMin, IntensityMax = iMax, Classes = classes, Crs = h.Crs, CrsText = h.CrsText, Units = h.Units, Notes = h.Notes,
            Nodes = nodes, SourceLength = sourceLength, SourceKey = sourceKey,
        };
        data.Dispose();
        PointStore.WriteMeta(dir, store);
        progress?.Invoke("Done", 1);
        return PointStore.Open(dir) ?? throw new PointCloudException("The index could not be read back.");
    }

    /// <summary>The top-down sampling of a node, and the bottom-up one of a parent from its children.</summary>
    sealed class Builder(double edge, double[] step, Action<string, Set> emit, int leafMax, CancellationToken cancel)
    {
        /// <summary>The grid's cells taken, cleared after each node by the list of those set.</summary>
        readonly bool[] _taken = new bool[PointStore.Resolution * PointStore.Resolution * PointStore.Resolution];
        readonly List<int> _set = [];

        (double X, double Y, double Z, double E) Cube(string name)
        {
            double x = 0, y = 0, z = 0, e = edge;
            for (var i = 1; i < name.Length; i++)
            {
                e /= 2;
                var k = name[i] - '0';
                if ((k & 4) != 0) x += e;
                if ((k & 2) != 0) y += e;
                if ((k & 1) != 0) z += e;
            }
            return (x, y, z, e);
        }

        int CellIn((double X, double Y, double Z, double E) c, Set s, int k)
        {
            const int R = PointStore.Resolution;
            var f = R / c.E;
            var cx = (int)Math.Clamp((s.X[k] * step[0] - c.X) * f, 0, R - 1);
            var cy = (int)Math.Clamp((s.Y[k] * step[1] - c.Y) * f, 0, R - 1);
            var cz = (int)Math.Clamp((s.Z[k] * step[2] - c.Z) * f, 0, R - 1);
            return (cx * R + cy) * R + cz;
        }

        int ChildIn((double X, double Y, double Z, double E) c, Set s, int k)
        {
            var h = c.E / 2;
            return (s.X[k] * step[0] - c.X >= h ? 4 : 0) | (s.Y[k] * step[1] - c.Y >= h ? 2 : 0) | (s.Z[k] * step[2] - c.Z >= h ? 1 : 0);
        }

        void Clear() { foreach (var c in _set) _taken[c] = false; _set.Clear(); }

        /// <summary>A node and its descendants from its points; with <paramref name="hold"/> the node itself is returned, not written.</summary>
        public Set? Top(string name, Set s, bool hold)
        {
            cancel.ThrowIfCancellationRequested();
            if (s.Count <= leafMax || name.Length > MaxDepth)
            {
                if (hold) return s;
                emit(name, s);
                return null;
            }
            var cube = Cube(name);
            var keep = new List<int>();
            var children = new List<int>?[8];
            for (var k = 0; k < s.Count; k++)
            {
                var cell = CellIn(cube, s, k);
                if (!_taken[cell] && keep.Count < NodeCap) { _taken[cell] = true; _set.Add(cell); keep.Add(k); }
                else (children[ChildIn(cube, s, k)] ??= []).Add(k);
            }
            Clear();
            var node = s.Pick(keep);
            if (!hold) emit(name, node);
            for (var c = 0; c < 8; c++)
                if (children[c] is { Count: > 0 } idx) Top(name + (char)('0' + c), s.Pick(idx), hold: false);
            return hold ? node : null;
        }

        /// <summary>A parent's sample from its children's held points, which keep what it does not take.</summary>
        public void Sample(string name, List<Set> children, Set parent)
        {
            var cube = Cube(name);
            for (var c = 0; c < children.Count; c++)
            {
                var s = children[c];
                var rest = new List<int>(s.Count);
                for (var k = 0; k < s.Count; k++)
                {
                    var cell = CellIn(cube, s, k);
                    if (!_taken[cell] && parent.Count < NodeCap) { _taken[cell] = true; _set.Add(cell); parent.Add(s, k); }
                    else rest.Add(k);
                }
                children[c] = s.Pick(rest);
                // The child's points stay its own (the list passed is the caller's: written back by reference below).
                CopyInto(s, children[c]);
            }
            Clear();
        }

        static void CopyInto(Set to, Set from)
        {
            to.X = from.X; to.Y = from.Y; to.Z = from.Z; to.R = from.R; to.G = from.G; to.B = from.B; to.I = from.I; to.C = from.C; to.Count = from.Count;
        }
    }
}
