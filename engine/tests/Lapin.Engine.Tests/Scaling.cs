namespace Lapin.Engine.Tests;

/// <summary>
/// How a test checks that work grows as it should without a wall-clock limit (issue #81): a run of N against one of
/// k·N, or a large case against a small one, timed in the same run and each kept at its best of a few tries, the tries
/// interleaved. A machine under load (the whole suite in parallel, CI) slows both alike, so their ratio holds where a
/// limit in seconds does not; the best of a few drops the tries a burst of load fell on.
/// </summary>
static class Scaling
{
    /// <summary>The collection these tests run in: by themselves, after the tests that run in parallel.</summary>
    public const string Serial = "Scaling (serial)";

    /// <summary>The best time (ms) of each action over <paramref name="tries"/> rounds, the actions taken in turn each round.</summary>
    public static double[] Best(int tries, params Action[] actions)
    {
        var best = Enumerable.Repeat(double.MaxValue, actions.Length).ToArray();
        for (var t = 0; t < tries; t++)
            for (var i = 0; i < actions.Length; i++)
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                actions[i]();
                best[i] = Math.Min(best[i], clock.Elapsed.TotalMilliseconds);
            }
        return best;
    }
}

/// <summary>The timing tests' collection: not run in parallel with any other (xUnit runs it alone, after the parallel ones).</summary>
[CollectionDefinition(Scaling.Serial, DisableParallelization = true)]
public sealed class ScalingCollection;
