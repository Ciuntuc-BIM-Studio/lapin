namespace Lapin.Mesh;

/// <summary>
/// The unit a mesh file's storey levels are written in (a BIM Open Schema or Fragments file keeps an IFC's own: metres or
/// millimetres, Revit's feet) while its vertices are in metres: one scale for the levels of one model. First by the
/// spacing of its levels (a building's storeys stand 1.5–15 m apart: −3370, −150, 3750 are millimetres, −3.37, −0.15,
/// 3.75 metres); where that leaves a choice, by how well the levels fit the typical height of their storeys' elements;
/// else a level above 500 is millimetres.
/// </summary>
public static class StoreyUnits
{
    static readonly double[] Scales = [1, 0.001, 0.3048];

    /// <param name="levels">Each storey's level as written, and the median of its elements' lowest points (metres), when it has elements.</param>
    public static double Scale(IReadOnlyList<(double Raw, double? Typical)> levels)
    {
        if (levels.Count == 0) return 1;
        var plausible = Scales.ToList();
        var distinct = levels.Select(l => l.Raw).Distinct().Order().ToList();
        if (distinct.Count >= 2)
        {
            var gaps = distinct.Zip(distinct.Skip(1), (a, b) => b - a).Order().ToList();
            var median = gaps[gaps.Count / 2];
            var bySpacing = Scales.Where(k => median * k is >= 1.5 and <= 15).ToList();
            if (bySpacing.Count == 1) return bySpacing[0];
            if (bySpacing.Count > 1) plausible = bySpacing;
        }
        var fit = levels.Where(l => l.Typical != null).ToList();
        if (fit.Count > 0) return plausible.OrderBy(k => fit.Sum(l => Math.Abs(l.Raw * k - l.Typical!.Value))).First();
        return levels.Any(l => Math.Abs(l.Raw) > 500) && plausible.Contains(0.001) ? 0.001 : plausible[0];
    }

    /// <summary>The median of some values (null when there are none).</summary>
    public static double? Median(List<double> values)
    {
        if (values.Count == 0) return null;
        values.Sort();
        return values[values.Count / 2];
    }
}
