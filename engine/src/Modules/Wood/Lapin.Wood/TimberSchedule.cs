using System.Globalization;
using Lapin.Architecture.Roofs;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Wood;

/// <summary>
/// Every piece of timber in the project as a cutting list (docs/plan-wood.md): the timber-frame walls' members and the
/// roofs' (RoofTimberSchedule), grouped by element, member, section, grade and length (to 10 mm), with the pieces, the total
/// length and the volume; the totals per section and grade after.
/// </summary>
public sealed class TimberSchedule : IScheduleSource
{
    public string Kind => "timber";
    public string Label => "Timber";

    public static List<RoofTimberSchedule.Row> Rows(IQueryContext q)
    {
        var o = new List<RoofTimberSchedule.Row>();
        foreach (var wall in q.Model.OfClass(IfcClass.Wall).OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase).ThenBy(w => w.Id))
        {
            if (WoodModule.Of(q.Model, wall, q.Geometry(wall.Id), q.Services) is not { } f) continue;
            o.AddRange(f.Result.Members.GroupBy(m => (m.Role, Section: WoodModule.Section(m.Section), Length: Math.Round(m.Length, 2)))
                .OrderBy(g => Array.IndexOf(Framing.Roles, g.Key.Role)).ThenBy(g => g.Key.Section).ThenByDescending(g => g.Key.Length)
                .Select(g => new RoofTimberSchedule.Row(wall.Name, WoodModule.Label(g.Key.Role), g.Key.Section, f.Spec.Grade, g.Key.Length, g.Count(), g.Sum(m => m.Volume))));
        }
        o.AddRange(RoofTimberSchedule.Rows(q.Model));
        return o;
    }

    public ScheduleGrid Grid(IQueryContext q)
    {
        string N(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);
        var rows = Rows(q);
        var grid = rows.Select(x => (IReadOnlyList<string>)[x.Roof, x.Role, x.Section, x.Grade, N(x.Length), x.Count.ToString(CultureInfo.InvariantCulture), N(x.Length * x.Count), N(x.Volume, "0.000")]).ToList();
        foreach (var t in rows.GroupBy(x => (x.Section, x.Grade)).OrderBy(g => g.Key.Section))
            grid.Add(["Total", "", t.Key.Section, t.Key.Grade, "", t.Sum(x => x.Count).ToString(CultureInfo.InvariantCulture), N(t.Sum(x => x.Length * x.Count)), N(t.Sum(x => x.Volume), "0.000")]);
        return new ScheduleGrid(Label, ["Element", "Member", "Section", "Grade", "Length (m)", "Pieces", "Total length (m)", "Volume (m³)"], grid);
    }
}
