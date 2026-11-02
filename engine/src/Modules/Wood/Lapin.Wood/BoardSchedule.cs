using System.Globalization;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Wood;

/// <summary>
/// The boards of the timber-frame walls (decision 240): by wall and layer, the sheet's size, the sheets it takes (each one
/// begun counts whole), their net area and what is cut off.
/// </summary>
public sealed class BoardSchedule : IScheduleSource
{
    public string Kind => "boards";
    public string Label => "Boards";

    public sealed record Row(string Wall, string Layer, string Sheet, int Sheets, double NetArea, double Waste);

    public static List<Row> Rows(IQueryContext q)
    {
        var o = new List<Row>();
        foreach (var wall in q.Model.OfClass(IfcClass.Wall).OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase).ThenBy(w => w.Id))
        {
            if (q.Geometry(wall.Id) is not { } g) continue;
            foreach (var layer in g.Parts.Where(p => p.Key?.StartsWith("wood-board:", StringComparison.Ordinal) == true).GroupBy(p => p.Key!.Split(':')[1]))
            {
                var first = layer.First();
                var props = first.Properties ?? [];
                string Prop(string n) => props.FirstOrDefault(p => p.Name == n).Value ?? "";
                var area = layer.Sum(p => double.TryParse((p.Properties ?? []).FirstOrDefault(x => x.Name == "Area").Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? a : 0);
                var size = Prop("Sheet").Replace(" mm", "").Split('x');
                var sheetArea = size.Length == 2 && double.TryParse(size[0], CultureInfo.InvariantCulture, out var w) && double.TryParse(size[1], CultureInfo.InvariantCulture, out var h) ? w * h / 1e6 : 0;
                var count = layer.Count();
                o.Add(new Row(wall.Name, Prop("Layer"), Prop("Sheet"), count, area, Math.Max(0, count * sheetArea - area)));
            }
        }
        return o;
    }

    public ScheduleGrid Grid(IQueryContext q)
    {
        string N(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        var rows = Rows(q);
        var grid = rows.Select(r => (IReadOnlyList<string>)[r.Wall, r.Layer, r.Sheet, r.Sheets.ToString(CultureInfo.InvariantCulture), N(r.NetArea), N(r.Waste)]).ToList();
        foreach (var t in rows.GroupBy(r => (r.Layer, r.Sheet)))
            grid.Add(["Total", t.Key.Layer, t.Key.Sheet, t.Sum(r => r.Sheets).ToString(CultureInfo.InvariantCulture), N(t.Sum(r => r.NetArea)), N(t.Sum(r => r.Waste))]);
        return new ScheduleGrid(Label, ["Wall", "Layer", "Sheet", "Sheets", "Net area (m²)", "Cut off (m²)"], grid);
    }
}
