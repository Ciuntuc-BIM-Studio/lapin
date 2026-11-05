using System.Collections.Concurrent;
using System.Diagnostics;
using Lapin.Geometry;

namespace Lapin.Clash;

/// <summary>One thing a clash test takes: an element's id, its box and its triangles in the world (asked once a run, when a pair needs them).</summary>
public sealed record ClashBody(string Id, Vec3 Min, Vec3 Max, Func<IReadOnlyList<Tri>> Triangles);

/// <summary>
/// What one run tests: the bodies, set A and set B (indices; B null: A against itself), hard or clearance, the
/// tolerance (hard: a crossing this deep or less is ignored; clearance: the distance), the most clashes it reports,
/// and which pairs are never tested (an element and its own opening's filling, the members of one assembly...).
/// </summary>
public sealed record ClashJob(IReadOnlyList<ClashBody> Items, IReadOnlyList<int> SetA, IReadOnlyList<int>? SetB, bool Clearance = false, double Tolerance = ClashEngine.HardTolerance,
    int MaxClashes = ClashEngine.MaxClashesPerTest, Func<int, int, bool>? Skip = null, int Threads = 0, bool Contained = true);

/// <summary>A clash found: the two bodies (set A's first), where, how deep (a crossing) or how far apart (a gap), and whether they cross.</summary>
public readonly record struct ClashHit(int A, int B, Vec3 Point, double Distance, bool Crossing);

public sealed record ClashRun(IReadOnlyList<ClashHit> Hits, bool Truncated, long Candidates, int CountA, int CountB, double Seconds);

/// <summary>
/// Clash detection (docs/plan-visualisation.md, stage 2), after BimGo's ClashEngine (MIT-0), cheap at scale and costing
/// nothing until asked:
/// <list type="number">
/// <item><b>Broad phase</b>: sweep and prune over the bodies' boxes along the axis their centres spread most (a clearance
/// test grows each box by half the distance), keeping only the pairs the test asks for and the job does not skip.</item>
/// <item><b>Narrow phase</b>, in parallel: each body's triangles inside the pair's common box (a body of many triangles
/// keeps a <see cref="TriangleTree"/> for the run), a second sweep over those triangles' boxes, then exact tests
/// (<see cref="Collisions"/>).</item>
/// <item><b>Hard</b>: the depth is the narrowest width of where the faces cross, over the axes and the crossing faces'
/// normals; when that is within the tolerance, corners of one body inside the other deeper than the tolerance still make
/// a clash (a pipe sunk into a slab). Faces that only touch never clash.</item>
/// <item><b>Clearance</b>: crossings (marked) and gaps under the distance, with the nearest points.</item>
/// </list>
/// A body wholly inside another, no face crossing (a pipe forgotten inside a slab), is found too, unlike BimGo: its box
/// lies within the other's and a corner of it is inside the other; its depth is its own least width (<see cref="ClashJob.Contained"/>).
/// </summary>
public static class ClashEngine
{
    public const double HardTolerance = 0.005, ClearanceDistance = 0.05, MaxTolerance = 2.0;
    public const int MaxClashesPerTest = 10_000;
    /// <summary>Bodies of at least this many triangles get a tree for the run.</summary>
    const int TreeMin = 384;
    const int MaxInsideCorners = 512, MaxCrossingPoints = 4096;

    public static ClashRun Run(ClashJob job, CancellationToken cancel = default)
    {
        var clock = Stopwatch.StartNew();
        var items = job.Items;
        var self = job.SetB == null;
        var tolerance = Math.Max(0, job.Tolerance);
        // Membership: 1 set A, 2 set B (a test of A against itself puts A in both).
        var member = new byte[items.Count];
        int countA = 0, countB = 0;
        foreach (var i in job.SetA)
        {
            if (i < 0 || i >= items.Count || (member[i] & 1) != 0) continue;
            member[i] |= (byte)(self ? 3 : 1);
            countA++;
        }
        if (self) countB = countA;
        else
            foreach (var i in job.SetB!)
            {
                if (i < 0 || i >= items.Count || (member[i] & 2) != 0) continue;
                member[i] |= 2;
                countB++;
            }
        var pairs = Pairs(job, member, self, job.Clearance ? tolerance * 0.5 : 1e-4);
        var context = new Context(items);
        var found = new ConcurrentBag<ClashHit>();
        var hits = 0;
        var truncated = false;
        var threads = job.Threads > 0 ? job.Threads : Math.Max(1, Environment.ProcessorCount - 1);
        Parallel.For(0, pairs.Count, new ParallelOptions { MaxDegreeOfParallelism = threads }, () => new Scratch(), (index, state, s) =>
        {
            if (cancel.IsCancellationRequested || Volatile.Read(ref hits) >= job.MaxClashes) { state.Stop(); return s; }
            var (a, b) = pairs[index];
            try
            {
                if (TestPair(context, s, a, b, job.Clearance, tolerance, job.Contained, out var hit))
                {
                    found.Add(hit);
                    if (Interlocked.Increment(ref hits) >= job.MaxClashes) truncated = true;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException)
            {
                // A broken body does not stop the run.
            }
            return s;
        }, _ => { });
        var list = found.ToList();
        list.Sort((x, y) => x.A != y.A ? x.A.CompareTo(y.A) : x.B.CompareTo(y.B));
        if (list.Count > job.MaxClashes) { list.RemoveRange(job.MaxClashes, list.Count - job.MaxClashes); truncated = true; }
        return new ClashRun(list, truncated, pairs.Count, countA, countB, clock.Elapsed.TotalSeconds);
    }

    /// <summary>Sweep and prune: every pair whose boxes (grown) meet and that the test asks for, set A's body first (a test of A against itself: the lower index first).</summary>
    static List<(int, int)> Pairs(ClashJob job, byte[] member, bool self, double grow)
    {
        var items = job.Items;
        var pairs = new List<(int, int)>();
        var list = new List<int>();
        Vec3 lo = new(double.MaxValue, double.MaxValue, double.MaxValue), hi = -lo;
        for (var i = 0; i < items.Count; i++)
        {
            if (member[i] == 0) continue;
            list.Add(i);
            var c = (items[i].Min + items[i].Max) * 0.5;
            lo = Collisions.Min(lo, c);
            hi = Collisions.Max(hi, c);
        }
        if (list.Count < 2) return pairs;
        var spread = hi - lo;
        var axis = spread.X >= spread.Y && spread.X >= spread.Z ? 0 : spread.Y >= spread.Z ? 1 : 2;
        static double At(Vec3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;
        var order = list.ToArray();
        var keys = order.Select(i => At(items[i].Min, axis) - grow).ToArray();
        Array.Sort(keys, order);
        var active = new List<int>();
        foreach (var p in order)
        {
            var start = At(items[p].Min, axis) - grow;
            for (var j = active.Count - 1; j >= 0; j--)
                if (At(items[active[j]].Max, axis) + grow < start) { active[j] = active[^1]; active.RemoveAt(active.Count - 1); }
            foreach (var q in active)
            {
                if (!Collisions.Overlap(items[p].Min, items[p].Max, items[q].Min, items[q].Max, grow * 2)) continue;
                if (!Wanted(member, self, p, q, out var a, out var b)) continue;
                if (job.Skip?.Invoke(a, b) == true) continue;
                pairs.Add((a, b));
            }
            active.Add(p);
        }
        pairs.Sort((x, y) => x.Item1 != y.Item1 ? x.Item1.CompareTo(y.Item1) : x.Item2.CompareTo(y.Item2));
        return pairs;
    }

    static bool Wanted(byte[] member, bool self, int p, int q, out int a, out int b)
    {
        a = Math.Min(p, q);
        b = Math.Max(p, q);
        if (self) return true;
        bool pA = (member[p] & 1) != 0, pB = (member[p] & 2) != 0, qA = (member[q] & 1) != 0, qB = (member[q] & 2) != 0;
        if (pA && qB && qA && pB) return true;
        if (pA && qB) { a = p; b = q; return true; }
        if (qA && pB) { a = q; b = p; return true; }
        return false;
    }

    static (Vec3 Min, Vec3 Max) Grow(Vec3 min, Vec3 max, double by) => (min - new Vec3(by, by, by), max + new Vec3(by, by, by));
    static (Vec3 Min, Vec3 Max) Intersect((Vec3 Min, Vec3 Max) a, (Vec3 Min, Vec3 Max) b) => (Collisions.Max(a.Min, b.Min), Collisions.Min(a.Max, b.Max));
    static bool Valid((Vec3 Min, Vec3 Max) b) => b.Min.X <= b.Max.X && b.Min.Y <= b.Max.Y && b.Min.Z <= b.Max.Z;

    static bool TestPair(Context context, Scratch s, int a, int b, bool clearance, double tolerance, bool contained, out ClashHit hit)
    {
        hit = default;
        ClashBody ia = context.Items[a], ib = context.Items[b];
        var reach = (clearance ? tolerance : 0) + 1e-4;
        var overlapA = Intersect((ia.Min, ia.Max), Grow(ib.Min, ib.Max, reach));
        var overlapB = Intersect((ib.Min, ib.Max), Grow(ia.Min, ia.Max, reach));
        if (!Valid(overlapA) || !Valid(overlapB)) return false;
        var centre = ((overlapA.Min + overlapA.Max) * 0.5 + (overlapB.Min + overlapB.Max) * 0.5) * 0.5;
        // A hard test also takes the faces within the tolerance round the common box: the inside test needs the nearest surface.
        var qa = clearance ? overlapA : Grow(overlapA.Min, overlapA.Max, tolerance);
        context.Gather(a, qa, centre, s.A);
        var qb = clearance ? overlapB : Grow(overlapB.Min, overlapB.Max, tolerance);
        if (s.A.Count > 0) context.Gather(b, qb, centre, s.B);
        // No faces near each other: one may still lie wholly inside the other (the slab's faces far from the pipe in it).
        if (s.A.Count == 0 || s.B.Count == 0) return contained && Inside(context, a, b, tolerance, out hit);
        if (clearance ? TestClearance(s, a, b, tolerance, centre, out hit) : TestHard(context, s, a, b, tolerance, centre, out hit)) return true;
        return contained && Inside(context, a, b, tolerance, out hit);
    }

    /// <summary>One body wholly inside the other, no face crossing: its box within the other's and a corner of it inside the other.</summary>
    static bool Inside(Context context, int a, int b, double tolerance, out ClashHit hit)
    {
        hit = default;
        ClashBody ia = context.Items[a], ib = context.Items[b];
        static bool Within(ClashBody inner, ClashBody outer) =>
            inner.Min.X >= outer.Min.X && inner.Min.Y >= outer.Min.Y && inner.Min.Z >= outer.Min.Z && inner.Max.X <= outer.Max.X && inner.Max.Y <= outer.Max.Y && inner.Max.Z <= outer.Max.Z;
        var (inner, outer) = Within(ia, ib) ? (a, b) : Within(ib, ia) ? (b, a) : (-1, -1);
        if (inner < 0) return false;
        var body = context.Items[inner];
        var size = body.Max - body.Min;
        var depth = Math.Min(size.X, Math.Min(size.Y, size.Z));
        if (depth <= tolerance) return false;
        var tris = context.All(inner);
        if (tris.Count == 0 || !context.Inside(outer, tris[0].A)) return false;
        hit = new ClashHit(a, b, (body.Min + body.Max) * 0.5, depth, true);
        return true;
    }

    static bool TestHard(Context context, Scratch s, int a, int b, double tolerance, Vec3 centre, out ClashHit hit)
    {
        hit = default;
        s.Points.Clear();
        s.Directions.Clear();
        s.Directions.Add(new Vec3(1, 0, 0));
        s.Directions.Add(new Vec3(0, 1, 0));
        s.Directions.Add(new Vec3(0, 0, 1));
        Sweep(s, 0, (t, u) =>
        {
            if (!Collisions.Crosses(t, u, out var p, out var q)) return true;
            if (s.Points.Count < MaxCrossingPoints - 1) { s.Points.Add(p); s.Points.Add(q); }
            AddDirection(s.Directions, t.Normal);
            AddDirection(s.Directions, u.Normal);
            return true;
        });
        if (s.Points.Count == 0) return false;
        // The depth: the narrowest width of where they cross.
        var depth = double.MaxValue;
        foreach (var d in s.Directions)
        {
            double min = double.MaxValue, max = double.MinValue;
            foreach (var p in s.Points) { var t = Vec3.Dot(p, d); if (t < min) min = t; if (t > max) max = t; }
            depth = Math.Min(depth, max - min);
        }
        // A shallow crossing: corners buried deeper than the tolerance still count (a pipe lying sunk in a slab).
        if (depth <= tolerance)
            depth = Math.Max(depth, Math.Max(Buried(context, s.A, b, s.B, tolerance, centre), Buried(context, s.B, a, s.A, tolerance, centre)));
        if (depth <= tolerance) return false;
        var sum = Vec3.Zero;
        foreach (var p in s.Points) sum += p;
        hit = new ClashHit(a, b, sum * (1.0 / s.Points.Count) + centre, depth, true);
        return true;
    }

    static bool TestClearance(Scratch s, int a, int b, double distance, Vec3 centre, out ClashHit hit)
    {
        hit = default;
        var best = double.MaxValue;
        var crossing = false;
        Vec3 bestA = Vec3.Zero, bestB = Vec3.Zero;
        Sweep(s, distance, (t, u) =>
        {
            if (Collisions.Crosses(t, u, out var p, out var q, out var proper))
            {
                best = 0;
                bestA = bestB = (p + q) * 0.5;
                if (!proper) return true; // touching: nothing between them, but look on for a real crossing
                crossing = true;
                return false;
            }
            var d = Collisions.Distance(t, u, out var onA, out var onB);
            if (d < best) { best = d; bestA = onA; bestB = onB; }
            return true;
        });
        if (!crossing && best >= distance) return false;
        hit = new ClashHit(a, b, (bestA + bestB) * 0.5 + centre, best, crossing);
        return true;
    }

    /// <summary>Sweep and prune over the two triangle lists' boxes (grown), calling <paramref name="test"/> on each pair that meets until it says stop.</summary>
    static void Sweep(Scratch s, double grow, Func<Tri, Tri, bool> test)
    {
        var ta = s.A;
        var tb = s.B;
        ta.Sort((x, y) => x.Min.X.CompareTo(y.Min.X));
        tb.Sort((x, y) => x.Min.X.CompareTo(y.Min.X));
        var startB = 0;
        foreach (var t in ta)
        {
            // Behind every A triangle from here on (they come by their least x): never looked at again.
            while (startB < tb.Count && tb[startB].Max.X + grow < t.Min.X) startB++;
            for (var j = startB; j < tb.Count; j++)
            {
                var u = tb[j];
                if (u.Min.X - grow > t.Max.X) break;
                if (u.Max.X + grow < t.Min.X) continue;
                if (t.Min.Y > u.Max.Y + grow || t.Max.Y + grow < u.Min.Y || t.Min.Z > u.Max.Z + grow || t.Max.Z + grow < u.Min.Z) continue;
                if (!test(t, u)) return;
            }
        }
    }

    /// <summary>How deep the corners of <paramref name="corners"/> lie inside body <paramref name="other"/>: the furthest from its faces among those inside it and further than the tolerance from every face near them (0 when none).</summary>
    static double Buried(Context context, List<Tri> corners, int other, List<Tri> near, double tolerance, Vec3 centre)
    {
        var deepest = 0.0;
        var tried = 0;
        foreach (var t in corners)
        {
            foreach (var p in new[] { t.A, t.B, t.C })
            {
                if (tried++ >= MaxInsideCorners) return deepest;
                var nearest = double.MaxValue;
                foreach (var u in near)
                {
                    if (p.X < u.Min.X - nearest || p.X > u.Max.X + nearest || p.Y < u.Min.Y - nearest || p.Y > u.Max.Y + nearest || p.Z < u.Min.Z - nearest || p.Z > u.Max.Z + nearest) continue;
                    var d = (p - Collisions.ClosestOnTriangle(p, u)).Length;
                    if (d < nearest) nearest = d;
                    if (nearest <= tolerance) break;
                }
                if (nearest <= tolerance || nearest <= deepest) continue;
                if (!context.Inside(other, p + centre)) continue;
                // No face of the other within reach: its box bounds the depth.
                if (nearest == double.MaxValue) nearest = DepthInBox(context.Items[other], p + centre);
                deepest = Math.Max(deepest, nearest);
            }
        }
        return deepest;
    }

    static double DepthInBox(ClashBody b, Vec3 p)
    {
        Vec3 low = p - b.Min, high = b.Max - p;
        return Math.Max(0, Math.Min(Math.Min(Math.Min(low.X, low.Y), low.Z), Math.Min(Math.Min(high.X, high.Y), high.Z)));
    }

    static void AddDirection(List<Vec3> directions, Vec3 normal)
    {
        if (normal == Vec3.Zero || directions.Count >= 15) return;
        foreach (var d in directions) if (Math.Abs(Vec3.Dot(d, normal)) > 0.995) return;
        directions.Add(normal);
    }

    sealed class Scratch
    {
        public readonly List<Tri> A = new(256), B = new(256);
        public readonly List<Vec3> Points = new(256), Directions = new(16);
    }

    /// <summary>The run's shared state: each body's triangles (asked once), and a tree for a body of many.</summary>
    sealed class Context(IReadOnlyList<ClashBody> items)
    {
        public readonly IReadOnlyList<ClashBody> Items = items;
        readonly ConcurrentDictionary<int, Lazy<(IReadOnlyList<Tri> All, TriangleTree? Tree)>> _bodies = new();

        (IReadOnlyList<Tri> All, TriangleTree? Tree) Body(int i) => _bodies.GetOrAdd(i, k => new Lazy<(IReadOnlyList<Tri>, TriangleTree?)>(() =>
        {
            var all = Items[k].Triangles();
            return (all, all.Count >= TreeMin ? new TriangleTree(all) : null);
        }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        /// <summary>All a body's triangles, in the world.</summary>
        public IReadOnlyList<Tri> All(int i) => Body(i).All;

        /// <summary>A body's triangles whose boxes meet a box, less <paramref name="centre"/>.</summary>
        public void Gather(int i, (Vec3 Min, Vec3 Max) box, Vec3 centre, List<Tri> output)
        {
            output.Clear();
            var (all, tree) = Body(i);
            if (tree != null) { tree.Query(box.Min, box.Max, -centre, output); return; }
            foreach (var t in all) if (Collisions.Overlap(t.Min, t.Max, box.Min, box.Max)) output.Add(t.Shifted(-centre));
        }

        /// <summary>Whether a point (world) is inside a body.</summary>
        public bool Inside(int i, Vec3 p)
        {
            var b = Items[i];
            if (p.X < b.Min.X || p.X > b.Max.X || p.Y < b.Min.Y || p.Y > b.Max.Y || p.Z < b.Min.Z || p.Z > b.Max.Z) return false;
            var (all, tree) = Body(i);
            if (tree != null) return tree.Inside(p);
            return Collisions.Inside(p, all is Tri[] arr ? arr : all.ToArray());
        }
    }
}
