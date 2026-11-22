using System.Runtime.ExceptionServices;

namespace Lapin.Analysis.Solver;

/// <summary>
/// Independent solves run side by side (docs/plan-fem.md §14.20): the nonlinear combinations and their imperfection
/// variants, the buckling of each ultimate combination, a factor's load cases. Each job builds its own system (its own
/// assembly, factor and results); the bars and shells the problems share are prepared once (<see cref="FrameElement.Prepare"/>),
/// so a job reads them only. The results go back in the jobs' order and each is what it would be alone, bit for bit.
/// <para>
/// How many at once: the cores but one (<c>LAPIN_FEM_THREADS</c> sets it, 1 for one at a time), no more than the jobs, and no
/// more than a quarter of the memory the process may use takes at the jobs' estimated size (a large shell model's factor),
/// at least one. The browser's engine (WebAssembly, one thread) runs them in turn.
/// </para>
/// </summary>
public static class FemParallel
{
    static readonly AsyncLocal<int?> Override = new();
    [ThreadStatic] static bool _inWorker;

    /// <summary>Whether threads can be used here: not in the browser's engine.</summary>
    public static bool Available => !OperatingSystem.IsBrowser() && !OperatingSystem.IsWasi();

    /// <summary>Inside a job already: what it does runs on its own thread (no second level of jobs).</summary>
    public static bool InWorker => _inWorker;

    /// <summary>The most jobs at once asked for: the override, else <c>LAPIN_FEM_THREADS</c>, else the cores but one.</summary>
    public static int MaxDegree
    {
        get
        {
            if (Override.Value is { } o) return Math.Max(1, o);
            if (int.TryParse(Environment.GetEnvironmentVariable("LAPIN_FEM_THREADS"), out var env) && env > 0) return env;
            return Math.Max(1, Environment.ProcessorCount - 1);
        }
    }

    /// <summary>The most jobs at once within this flow of calls (a test's): disposed, as before.</summary>
    public static IDisposable Limit(int degree)
    {
        var had = Override.Value;
        Override.Value = degree;
        return new Restore(had);
    }

    sealed class Restore(int? had) : IDisposable
    {
        public void Dispose() => Override.Value = had;
    }

    /// <summary>The memory jobs may take together (bytes): a quarter of what the process may use.</summary>
    static long Budget()
    {
        var total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return total > 0 ? total / 4 : 2L << 30;
    }

    /// <summary>How many of <paramref name="jobs"/> jobs run at once, each taking about <paramref name="bytesPerJob"/>.</summary>
    public static int Degree(int jobs, long bytesPerJob)
    {
        if (jobs <= 1 || !Available || _inWorker) return 1;
        var d = Math.Min(MaxDegree, jobs);
        if (bytesPerJob > 0) d = (int)Math.Min(d, Math.Max(1, Budget() / bytesPerJob));
        return Math.Max(1, d);
    }

    /// <summary>
    /// What a solve of a problem with <paramref name="stored"/> entries in its factor takes, about (bytes): the factor, its
    /// assembly's structure and the mechanism's second factor when one is met, eight bytes an entry; the nodes' vectors.
    /// </summary>
    public static long BytesFor(long stored, int dofs) => 3 * 8 * Math.Max(stored, 1) + 64L * Math.Max(dofs, 1);

    /// <summary>
    /// <paramref name="f"/> on each item, at most <see cref="Degree"/> at once, the results in the items' order. A stop asked
    /// for (<paramref name="cancel"/>) ends it before the next job; when jobs throw, the lowest one's is thrown, as one at a
    /// time would.
    /// </summary>
    public static TOut[] Map<TIn, TOut>(IReadOnlyList<TIn> items, Func<TIn, TOut> f, long bytesPerJob = 0, CancellationToken cancel = default) =>
        Map(items, static () => 0, (item, _) => f(item), bytesPerJob, cancel);

    /// <summary>
    /// <see cref="Map{TIn, TOut}(IReadOnlyList{TIn}, Func{TIn, TOut}, long, CancellationToken)"/> with something of each
    /// worker's own (<paramref name="local"/>, made when a worker starts), handed to the jobs it runs one after another and
    /// to no other: what a nonlinear solve's systems give up for the next one (§14.22, <see cref="FactorStore"/>). Dropped
    /// when the worker ends, so no more are alive than jobs run at once.
    /// </summary>
    public static TOut[] Map<TIn, TLocal, TOut>(IReadOnlyList<TIn> items, Func<TLocal> local, Func<TIn, TLocal, TOut> f, long bytesPerJob = 0, CancellationToken cancel = default)
    {
        var o = new TOut[items.Count];
        var degree = Degree(items.Count, bytesPerJob);
        if (degree <= 1)
        {
            var mine = local();
            for (var i = 0; i < items.Count; i++)
            {
                cancel.ThrowIfCancellationRequested();
                o[i] = f(items[i], mine);
            }
            return o;
        }
        var errors = new ExceptionDispatchInfo?[items.Count];
        Parallel.For(0, items.Count, new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = cancel }, local, (i, _, mine) =>
        {
            var was = _inWorker;
            _inWorker = true;
            try { o[i] = f(items[i], mine); }
            catch (Exception ex) { errors[i] = ExceptionDispatchInfo.Capture(ex); }
            finally { _inWorker = was; }
            return mine;
        }, static _ => { });
        foreach (var e in errors) e?.Throw();
        return o;
    }

    /// <summary>
    /// <paramref name="count"/> solves on one factor (a subspace's vectors), side by side when the factor is large enough to
    /// be worth it (<paramref name="equations"/>) and no job runs this already; each solve writes its own vector only.
    /// </summary>
    public static void For(int count, int equations, Action<int> solve)
    {
        if (equations < SolvesApart || Degree(count, 0) <= 1)
        {
            for (var j = 0; j < count; j++) solve(j);
            return;
        }
        Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = Degree(count, 0) }, j =>
        {
            var was = _inWorker;
            _inWorker = true;
            try { solve(j); }
            finally { _inWorker = was; }
        });
    }

    /// <summary>Equations past which a factor's solves are shared between cores (smaller ones are quicker in turn).</summary>
    public const int SolvesApart = 20_000;
}
