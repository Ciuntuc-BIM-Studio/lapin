namespace Lapin.Analysis.Solver;

/// <summary>
/// The numeric values of factors given up by the systems one nonlinear solve has finished with (docs/plan-fem.md §14.22),
/// kept for the next system on the same structure: its iterations', and on one worker its next combinations' (their
/// problems share layouts, §14.21). A factor gives its values here (<see cref="ISymmetricFactor.Release"/>) only once nothing
/// reads it any more, and it can never be used again (it throws); <see cref="ISymmetricFactor.Fresh(FactorStore?)"/> takes
/// them back zeroed, so the new factor is the one a new array would make, bit for bit. Owned by one solve (or one worker's
/// solves, one after another): it is not shared between threads. Holds at most <see cref="Kept"/> sets, the most recent first
/// (a layout's factor and its rotations', and one more for a support that lifts), and nothing once dropped.
/// </summary>
public sealed class FactorStore
{
    /// <summary>Sets of values kept at most.</summary>
    public const int Kept = 3;

    readonly List<(object Shape, object Values)> _kept = [];

    /// <summary>The sets held now.</summary>
    public int Count => _kept.Count;

    /// <summary>A set of values given up on this structure (<paramref name="shape"/>, compared by reference), taken out; or null.</summary>
    internal object? Take(object shape)
    {
        for (var i = 0; i < _kept.Count; i++)
        {
            if (!ReferenceEquals(_kept[i].Shape, shape)) continue;
            var values = _kept[i].Values;
            _kept.RemoveAt(i);
            return values;
        }
        return null;
    }

    /// <summary>A factor's values, given up by it, kept for the next one on its structure; the oldest dropped past <see cref="Kept"/>.</summary>
    internal void Put(object shape, object values)
    {
        _kept.Insert(0, (shape, values));
        if (_kept.Count > Kept) _kept.RemoveAt(_kept.Count - 1);
    }
}
