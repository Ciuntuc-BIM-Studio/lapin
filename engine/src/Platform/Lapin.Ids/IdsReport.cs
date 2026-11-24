using System.Globalization;
using Lapin.Drawing.Export;

namespace Lapin.Ids;

/// <summary>An IDS run written down as a workbook (Lapin's own OOXML writer, <see cref="XlsxWriter"/>).</summary>
public static class IdsReport
{
    public sealed class Checked(string Title, string File, IdsRun Run)
{
    public string Title { get; } = Title;
    public string File { get; } = File;
    public IdsRun Run { get; } = Run;
}

    public static readonly string[] SummaryColumns = ["IDS", "Specification", "Identifier", "Usage", "Result", "Applicable", "Passed", "Failed", "Note"];
    public static readonly string[] FailureColumns = ["IDS", "Specification", "GlobalId", "Class", "Name", "Requirement", "Actual", "Expected", "Instructions"];

    static string N(int i) => i.ToString(CultureInfo.InvariantCulture);

    public static byte[] Xlsx(string project, string made, IReadOnlyList<Checked> runs)
    {
        var report = new List<IReadOnlyList<string>>
        {
            new[] { "IDS report", project },
            new[] { "Made", made },
            new[] { "IDS files", N(runs.Count) },
            new[] { "Specifications", N(runs.Sum(r => r.Run.Specifications.Count)) },
            new[] { "Failing", N(runs.Sum(r => r.Run.Failing)) },
            new[] { "Passing", N(runs.Sum(r => r.Run.Passing)) },
            new[] { "Not for this IFC version", N(runs.Sum(r => r.Run.Skipped)) },
        };
        var summary = new List<IReadOnlyList<string>> { SummaryColumns };
        var failures = new List<IReadOnlyList<string>> { FailureColumns };
        foreach (var r in runs)
            foreach (var s in r.Run.Specifications)
            {
                summary.Add(new[] { r.Title, s.Name, s.Identifier ?? "", s.Usage, s.Status, N(s.Applicable), N(s.Passed), N(s.Failed), s.Message ?? "" });
                foreach (var f in s.Failures)
                    failures.Add(new[] { r.Title, s.Name, f.GlobalId, f.Class, f.Name ?? "", f.Requirement, f.Actual, f.Expected, f.Instructions ?? "" });
                if (s.Dropped > 0) failures.Add(new[] { r.Title, s.Name, "", "", "", $"{s.Dropped} more not listed", "", "", "" });
            }
        return XlsxWriter.Write([("Report", report), ("Specifications", summary), ("Failures", failures)]);
    }
}
