using System.Globalization;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// The roofs' timber as a cutting list (decision 229): every member of every framed roof, grouped by roof, role, section,
/// grade and length (to 10 mm), with the pieces, the total length and the volume; the totals per section and grade after.
/// </summary>
public sealed class RoofTimberSchedule : IScheduleSource
{
    public string Kind => "roofTimber";
    public string Label => "Roof timber";

    public sealed record Row(string Roof, string Role, string Section, string Grade, double Length, int Count, double Volume);

    public static List<Row> Rows(IModelView m)
    {
        var o = new List<Row>();
        foreach (var roof in m.OfClass(IfcClass.Roof).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (Roof.Resolve(roof, m) is not { Framed: true } r || Roof.Solve(r).Shape is not { } shape) continue;
            var members = RoofParts.Framing(r, shape, RoofOpenings.Place(roof, shape, m, []));
            o.AddRange(members.GroupBy(x => (x.Role, Section: Roof.TimberSection(x), Length: Math.Round(x.Length, 2)))
                .OrderBy(g => Order(g.Key.Role)).ThenBy(g => g.Key.Section).ThenByDescending(g => g.Key.Length)
                .Select(g => new Row(roof.Name, RoofParts.RoleLabel(g.Key.Role), g.Key.Section, r.Grade, g.Key.Length, g.Count(), g.Sum(x => x.Volume))));
        }
        return o;
    }

    static int Order(string role) => Array.IndexOf(["plate", "rafter", "topChord", "bottomChord", "web", "collar", "purlin", "ridge", "hip", "valley", "trimmer"], role) is var i and >= 0 ? i : 99;

    public ScheduleGrid Grid(IQueryContext q)
    {
        string N(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);
        var rows = Rows(q.Model);
        var grid = rows.Select(x => (IReadOnlyList<string>)[x.Roof, x.Role, x.Section, x.Grade, N(x.Length), x.Count.ToString(CultureInfo.InvariantCulture), N(x.Length * x.Count), N(x.Volume, "0.000")]).ToList();
        foreach (var t in rows.GroupBy(x => (x.Section, x.Grade)).OrderBy(g => g.Key.Section))
            grid.Add(["Total", "", t.Key.Section, t.Key.Grade, "", t.Sum(x => x.Count).ToString(CultureInfo.InvariantCulture), N(t.Sum(x => x.Length * x.Count)), N(t.Sum(x => x.Volume), "0.000")]);
        return new ScheduleGrid(Label, ["Roof", "Member", "Section", "Grade", "Length (m)", "Pieces", "Total length (m)", "Volume (m³)"], grid);
    }
}
