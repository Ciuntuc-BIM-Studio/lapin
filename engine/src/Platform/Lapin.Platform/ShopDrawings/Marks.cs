using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Marks of identical parts (docs/plan-shop-drawings.md §8): stage S3 fills this in; until then a part has no mark of its own
/// (its drawing shows its name) and a quantity of one.
/// </summary>
public static class Marks
{
    /// <summary>The part's mark, null when it has none.</summary>
    public static string? MarkOf(IQueryContext q, Entity e) => null;

    /// <summary>How many parts share the element's mark (one without a mark).</summary>
    public static int CountOf(IQueryContext q, Entity e) => 1;
}
