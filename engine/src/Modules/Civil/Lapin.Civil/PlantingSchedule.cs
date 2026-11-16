using System.Globalization;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>The planting schedule: every species the project plants, with its Latin and Romanian names, its kind, size and how many, and the total; a table the schedules show and export.</summary>
public sealed class PlantingSchedule : IScheduleSource
{
    public string Kind => "planting";
    public string Label => "Planting schedule";

    /// <summary>
    /// The plants of the plantings as parts a field schedule lists (audit S1), and the field schedule that gives the same table: a
    /// row per species, trees, then bushes, then grasses, by name, how many, the total under them.
    /// </summary>
    public static void RegisterTemplate(IModuleRegistry r)
    {
        r.Service<IPartSource>(new PlantParts());
        r.Service(ShippedScheduleTemplate.Of("planting", "Planting schedule", "planting", """
            {
              "source": "plants",
              "filter": { "classes": ["LapinPlanting"] },
              "groupBy": ["species"], "groupColumns": false, "count": false, "totals": "always",
              "sort": ["plantKindOrder", "species"],
              "columns": [
                { "field": "species", "heading": "Species", "aggregate": "first" },
                { "field": "latinName", "heading": "Latin name", "aggregate": "first" },
                { "field": "romanianName", "heading": "Romanian name", "aggregate": "first" },
                { "field": "plantKind", "heading": "Kind", "aggregate": "first" },
                { "field": "plantHeight", "heading": "Height (m)", "aggregate": "first" },
                { "field": "plantCrown", "heading": "Crown (m)", "aggregate": "first" },
                { "field": "species", "heading": "Count", "aggregate": "rows" }
              ]
            }
            """));
    }

    /// <summary>Each plant of a planting: its species, their Latin and Romanian names, its kind (and the kinds' order), height and crown.</summary>
    sealed class PlantParts : IPartSource
    {
        public string Source => "plants";
        public string Label => "Plants";
        static readonly Dictionary<string, int> KindOrder = new() { ["tree"] = 0, ["bush"] = 1, ["grass"] = 2 };
        public IReadOnlyList<(string Key, string Label)> Fields { get; } =
        [
            ("species", "Species"), ("latinName", "Latin name"), ("romanianName", "Romanian name"), ("family", "Family"), ("plantKind", "Kind (tree, bush, grass)"),
            ("plantKindOrder", "Kind's place: trees, bushes, grasses"), ("plantHeight", "Height (m)"), ("plantCrown", "Crown (m)"),
        ];

        public IEnumerable<ElementPart> PartsOf(IQueryContext q, Entity element)
        {
            if (element.Class != Plantings.Class) yield break;
            static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            var i = 0;
            foreach (var p in Plantings.Plants(q.Model, element))
            {
                var s = p.Species;
                yield return ElementPart.Of((i++).ToString(CultureInfo.InvariantCulture),
                    ("species", s.Name), ("latinName", s.Latin), ("romanianName", s.Ro), ("family", s.Family), ("plantKind", s.Type),
                    ("plantKindOrder", KindOrder.GetValueOrDefault(s.Type, 3)), ("plantHeight", N(s.Height)), ("plantCrown", N(s.Crown)));
            }
        }
    }

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
            .Select(r => (IReadOnlyList<string>)[r.Species.Name, r.Species.Latin ?? "", r.Species.Ro ?? "", r.Species.Type, N(r.Species.Height), N(r.Species.Crown), r.Count.ToString(CultureInfo.InvariantCulture)]).ToList();
        if (rows.Count > 0) table.Add(["Total", "", "", "", "", "", rows.Sum(r => r.Count).ToString(CultureInfo.InvariantCulture)]);
        return new ScheduleGrid(Label, ["Species", "Latin name", "Romanian name", "Kind", "Height (m)", "Crown (m)", "Count"], table);
    }
}
