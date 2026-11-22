namespace Lapin.Analysis.Solver;

/// <summary>
/// The members' buckling lengths from αcr (docs/plan-fem.md §14.23), for the design checks that ask for them: each ultimate
/// static combination's buckling (6 modes, as the checks' αcr, §14.11), the lengths of every member from its modes,
/// derived on asking and kept with the run (never stored).
/// </summary>
public sealed partial class AnalysisRun
{
    public const int LengthModes = 6;
    readonly Dictionary<string, MemberLength[]> _lengths = [];

    /// <summary>The buckling of every ultimate static combination, side by side (§14.20), before the lengths of each are asked.</summary>
    public void PrepareLengths(CancellationToken cancel = default) => BucklingOfAll(UltimateStatic().Where(c => !Failed(c.Key)), LengthModes, cancel);

    /// <summary>
    /// Each member's lengths from αcr in a design set (a combination, or one of its imperfection variants, whose buckling is its
    /// +X variant's, §14.14); null for a set without a buckling of its own (the seismic bounds, a combination left out).
    /// </summary>
    public MemberLength[]? MemberLengths(string setKey)
    {
        var key = setKey.Split('|')[0];
        if (key.StartsWith("seismic/", StringComparison.Ordinal)) return null;
        lock (_gate)
        {
            if (_lengths.TryGetValue(key, out var had)) return had;
            var c = Combinations.FirstOrDefault(x => x.Key == key && UserCombinations.Ultimate(x.Kind));
            if (c == null || Failed(c.Key)) return null;
            var b = BucklingOf(c, LengthModes);
            var members = Problem.PieceMap.Select(ps => ps.Select(x => x.Element).ToArray()).ToList();
            return _lengths[key] = BucklingLengths.Of(Problem.Problem, members, b);
        }
    }
}
