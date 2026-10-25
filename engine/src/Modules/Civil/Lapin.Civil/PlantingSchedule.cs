using System.Globalization;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>The planting schedule: every species the project plants, with its kind, size and how many, and the total; a table the schedules show and export.</summary>
public sealed class PlantingSchedule : IScheduleSource
{
    public string Kind => "planting";
    public string Label => "Planting schedule";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        var rows = new List<(Species Species, int Count)>();
        foreach (var e in m.OfClass(Plantings.Class))
            foreach (var group in Plantings.Plants(m, e).GroupBy(p => p.Species.Name))
            {
                var i = rows.FindIndex(r => r.Species.Name == group.Key);
                if (i >= 0) rows[i] = (rows[i].Species, rows[i].Count + group.Count());
                else rows.Add((group.First().Species, group.Count()));
            }
        string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        var kindOrder = new Dictionary<string, int> { ["tree"] = 0, ["bush"] = 1, ["grass"] = 2 };
        var table = rows.OrderBy(r => kindOrder.GetValueOrDefault(r.Species.Type, 3)).ThenBy(r => r.Species.Name)
            .Select(r => (IReadOnlyList<string>)[r.Species.Name, r.Species.Type, N(r.Species.Height), N(r.Species.Crown), r.Count.ToString(CultureInfo.InvariantCulture)]).ToList();
        if (rows.Count > 0) table.Add(["Total", "", "", "", rows.Sum(r => r.Count).ToString(CultureInfo.InvariantCulture)]);
        return new ScheduleGrid(Label, ["Species", "Kind", "Height (m)", "Crown (m)", "Count"], table);
    }
}
