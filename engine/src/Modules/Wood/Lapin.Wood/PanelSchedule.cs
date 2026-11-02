using System.Globalization;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Wood;

/// <summary>
/// The prefabricated wall panels (decision 245): each panel by its mark, its wall, its length and height, the openings in
/// it, its members, its timber's volume and its mass (the timber at 450 kg/m³, the boards and insulation left to the
/// supplier's sheet), so the panels can be ordered, made and lifted in turn.
/// </summary>
public sealed class PanelSchedule : IScheduleSource
{
    public string Kind => "panels";
    public string Label => "Wall panels";

    /// <summary>A panel: its mark, its wall, its length and height (metres), its openings, members and timber (m³), and the timber's mass (kg).</summary>
    public sealed record Row(string Mark, string Wall, double Length, double Height, int Openings, int Members, double Volume, double Mass);

    public const double Density = 450;

    public static List<Row> Rows(IQueryContext q)
    {
        var o = new List<Row>();
        foreach (var wall in q.Model.OfClass(IfcClass.Wall).OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase).ThenBy(w => w.Id))
        {
            if (WoodModule.Of(q.Model, wall, q.Geometry(wall.Id), q.Services) is not { Result.Panels: { } panels } f) continue;
            for (var i = 0; i < panels.Count; i++)
            {
                var mine = f.Result.Members.Where(m => m.Panel == i + 1).ToList();
                if (mine.Count == 0) continue;
                var volume = mine.Sum(m => m.Volume);
                o.Add(new Row(WoodModule.PanelMark(wall, i + 1), wall.Name, panels[i].U1 - panels[i].U0, mine.Max(m => m.Z1),
                    mine.Where(m => m.Opening != null).Select(m => m.Opening).Distinct().Count(), mine.Count, volume, volume * Density));
            }
        }
        return o;
    }

    public ScheduleGrid Grid(IQueryContext q)
    {
        string N(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);
        var rows = Rows(q);
        var grid = rows.Select(r => (IReadOnlyList<string>)[r.Mark, r.Wall, N(r.Length), N(r.Height), r.Openings.ToString(CultureInfo.InvariantCulture), r.Members.ToString(CultureInfo.InvariantCulture), N(r.Volume, "0.000"), N(r.Mass, "0")]).ToList();
        if (rows.Count > 0)
            grid.Add(["Total", $"{rows.Count} panels", N(rows.Sum(r => r.Length)), "", rows.Sum(r => r.Openings).ToString(CultureInfo.InvariantCulture), rows.Sum(r => r.Members).ToString(CultureInfo.InvariantCulture), N(rows.Sum(r => r.Volume), "0.000"), N(rows.Sum(r => r.Mass), "0")]);
        return new ScheduleGrid(Label, ["Panel", "Wall", "Length (m)", "Height (m)", "Openings", "Members", "Timber (m³)", "Timber mass (kg)"], grid);
    }
}
