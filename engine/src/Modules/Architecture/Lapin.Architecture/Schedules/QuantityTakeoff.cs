using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Schedules;

/// <summary>
/// The quantity takeoff (decision 287, the tester's report #75): every element the model measures, its quantity sets
/// added up by what the schedule groups by — the element's kind, type, storey and material, in the order chosen — with
/// the count, length, gross and net area, gross and net volume and weight. By material, each layer, zone or finish
/// counts under its own material (a wall's plaster, brick and insulation apart); an element of one material counts whole.
/// A schedule like any (on sheets, CSV, XLSX, columns and rows of its own); its options are the schedule's
/// <c>groupBy</c> (a list of kind, type, storey, material; default kind and type) and <c>classes</c> (the IFC classes
/// it lists; none: all).
/// </summary>
public sealed class QuantityTakeoff : IScheduleSource
{
    public const string KindId = "quantities";
    public string Kind => KindId;
    public const string Title = "Quantity takeoff";
    public string Label => Title;

    public static readonly string[] Dimensions = ["kind", "type", "storey", "material"];
    static readonly string[] DefaultGroups = ["kind", "type"];
    static readonly Dictionary<string, string> Headings = new() { ["kind"] = "Kind", ["type"] = "Type", ["storey"] = "Storey", ["material"] = "Material" };

    /// <summary>The measures, their headings, and the quantity names that give them (the first an element has).</summary>
    static readonly (string Heading, string[] Names)[] Measures =
    [
        ("Length (m)", ["Length"]),
        ("Gross area (m²)", ["GrossSideArea", "GrossArea", "GrossFloorArea", "GrossSurfaceArea", "Area"]),
        ("Net area (m²)", ["NetSideArea", "NetArea", "NetFloorArea", "NetSurfaceArea"]),
        ("Gross volume (m³)", ["GrossVolume"]),
        ("Net volume (m³)", ["NetVolume", "Volume"]),
        ("Weight (kg)", ["NetWeight", "GrossWeight", "Weight"]),
    ];

    public static IReadOnlyList<string> GroupsOf(Entity? schedule)
    {
        var g = schedule?.Attrs["groupBy"] is JsonArray a ? a.Select(x => (string?)x).OfType<string>().Where(Dimensions.Contains).Distinct().ToList() : null;
        return g is { Count: > 0 } ? g : DefaultGroups;
    }

    public static IReadOnlyList<string>? ClassesOf(Entity? schedule) =>
        schedule?.Attrs["classes"] is JsonArray a && a.Select(x => (string?)x).OfType<string>().ToList() is { Count: > 0 } l ? l : null;

    public ScheduleGrid Grid(IQueryContext q) => Table(q, null);
    public ScheduleGrid Grid(IQueryContext q, Entity schedule) => Table(q, schedule);

    /// <summary>A kind's name as the interface says it: the class's label, else its IFC name without the prefix ("IfcWall": "Wall").</summary>
    public static string KindLabel(IQueryContext q, string cls) => q.Describe(cls)?.Label is { Length: > 0 } l ? l
        : cls.StartsWith("Ifc", StringComparison.Ordinal) ? cls[3..] : cls.StartsWith("Lapin", StringComparison.Ordinal) ? cls[5..] : cls;

    /// <summary>The elements the takeoff can list: each entity with quantities, its class and how many of them there are.</summary>
    public static List<(string Class, string Label, int Count)> Classes(IQueryContext q) =>
        [.. Measured(q, null).GroupBy(x => x.Element.Class).Select(g => (g.Key, KindLabel(q, g.Key), g.Count())).OrderBy(x => x.Item2, StringComparer.CurrentCultureIgnoreCase)];

    static IEnumerable<(Entity Element, IReadOnlyList<QuantitySet> Sets)> Measured(IQueryContext q, IReadOnlyList<string>? classes)
    {
        foreach (var e in q.Model.Entities)
        {
            // A room is the air between the elements, not something built: its own schedule lists it (its volume would swell the totals).
            if (e.Class == IfcClass.Space || classes != null && !classes.Contains(e.Class)) continue;
            var sets = q.Quantities(e.Id);
            if (sets.Count > 0) yield return (e, sets);
        }
    }

    static double? Pick(IReadOnlyList<Quantity> qs, string[] names)
    {
        foreach (var n in names) if (qs.FirstOrDefault(x => x.Name == n) is { } hit) return hit.Value;
        return null;
    }

    /// <summary>Whether a set has any of the measures the takeoff adds up (a door's reveal set may have none of them).</summary>
    static bool HasMeasure(IReadOnlyList<Quantity> qs) => Measures.Any(m => Pick(qs, m.Names) != null);

    sealed class Sum
    {
        public readonly HashSet<string> Elements = [];
        public readonly double?[] Values = new double?[Measures.Length];
        public void Add(string element, IReadOnlyList<Quantity> qs, double share = 1)
        {
            Elements.Add(element);
            for (var i = 0; i < Measures.Length; i++)
                if (Pick(qs, Measures[i].Names) is { } v) Values[i] = (Values[i] ?? 0) + v * share;
        }
    }

    static ScheduleGrid Table(IQueryContext q, Entity? schedule)
    {
        var m = q.Model;
        var groups = GroupsOf(schedule);
        var byMaterial = groups.Contains("material");
        var storeyOrder = Spatial.Storeys(m).Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var sums = new Dictionary<string, (string[] Cells, (int, string)[] Order, Sum Sum)>();
        string MaterialName(string? id) => id == null ? "—" : m.Get(id)?.Name is { Length: > 0 } n ? n : id;

        void Add(Entity e, string? material, IReadOnlyList<Quantity> qs, double share = 1)
        {
            var cells = new string[groups.Count];
            var order = new (int, string)[groups.Count];
            for (var i = 0; i < groups.Count; i++)
            {
                switch (groups[i])
                {
                    case "kind": cells[i] = KindLabel(q, e.Class); order[i] = (0, cells[i]); break;
                    case "type": cells[i] = m.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.Name is { Length: > 0 } t ? t : "—"; order[i] = (cells[i] == "—" ? 1 : 0, cells[i]); break;
                    case "storey":
                        var s = Spatial.StoreyOf(m, e.Id);
                        cells[i] = s?.Name ?? "—"; order[i] = (s == null ? int.MaxValue : storeyOrder.GetValueOrDefault(s.Id), ""); break;
                    default: cells[i] = MaterialName(material); order[i] = (material == null ? 1 : 0, cells[i]); break;
                }
            }
            var key = string.Join("\u001f", cells);
            if (!sums.TryGetValue(key, out var row)) sums[key] = row = (cells, order, new Sum());
            row.Sum.Add(e.Id, qs, share);
        }

        foreach (var (e, sets) in Measured(q, ClassesOf(schedule)))
        {
            var main = sets.FirstOrDefault(s => s.Part == null);
            if (!byMaterial) { if (main != null) Add(e, null, main.Quantities); else foreach (var s in sets) Add(e, s.Material, s.Quantities); continue; }
            // By material: each part with its material (a layer, a zone, a finish, a lintel); else the element's own parts' material.
            var parts = sets.Where(s => s.Part != null && s.Material != null && HasMeasure(s.Quantities)).ToList();
            if (parts.Count > 0) { foreach (var s in parts) Add(e, s.Material, s.Quantities); continue; }
            if (main == null) continue;
            var built = q.Geometry(e.Id)?.Parts ?? [];
            var materials = built.Select(p => p.Material).Distinct().ToList();
            if (materials.Count <= 1) { Add(e, materials.FirstOrDefault(), main.Quantities); continue; }
            // Several materials and no measured parts: the volume shared out by the parts' own volumes, the rest left to the element.
            var volumes = built.Where(p => p.Instances == null).GroupBy(p => p.Material).ToDictionary(g => g.Key ?? "", g => g.Sum(p => Measure.Volume(p.Solid)));
            var total = volumes.Values.Sum();
            if (total <= 0) { Add(e, null, main.Quantities); continue; }
            var only = main.Quantities.Where(x => x.Kind == "VOLUME" || x.Kind == "WEIGHT").ToList();
            // Nothing to share out (a door measured by its opening): the element whole, under no one material.
            if (!HasMeasure(only)) { Add(e, null, main.Quantities); continue; }
            foreach (var (mat, v) in volumes) Add(e, mat.Length == 0 ? null : mat, only, v / total);
        }

        var ordered = sums.Values.OrderBy(r => r.Order, Comparer<(int, string)[]>.Create((a, b) =>
        {
            for (var i = 0; i < a.Length; i++)
            {
                var c = a[i].Item1.CompareTo(b[i].Item1);
                if (c == 0) c = string.Compare(a[i].Item2, b[i].Item2, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);
                if (c != 0) return c;
            }
            return 0;
        })).ToList();
        // A measure no row has is left out: a table of walls has no weight column.
        var shown = Enumerable.Range(0, Measures.Length).Where(i => ordered.Any(r => r.Sum.Values[i] is { } v && Math.Abs(v) > 1e-9)).ToList();
        string N(double? v, int i) => v is { } x ? x.ToString(i == 5 ? "0.0" : i is 3 or 4 ? "0.000" : "0.00", CultureInfo.InvariantCulture) : "";
        var rows = new List<IReadOnlyList<string>>();
        var keys = new List<string?>();
        foreach (var r in ordered)
        {
            rows.Add([.. r.Cells, r.Sum.Elements.Count.ToString(CultureInfo.InvariantCulture), .. shown.Select(i => N(r.Sum.Values[i], i))]);
            keys.Add(string.Join("|", r.Cells));
        }
        if (rows.Count > 1)
        {
            var count = ordered.SelectMany(r => r.Sum.Elements).Distinct().Count();
            rows.Add(["Total", .. Enumerable.Repeat("", groups.Count - 1), count.ToString(CultureInfo.InvariantCulture),
                .. shown.Select(i => N(ordered.Sum(r => r.Sum.Values[i] ?? 0), i))]);
            keys.Add(null);
        }
        return new ScheduleGrid(Title, [.. groups.Select(g => Headings[g]), "Count", .. shown.Select(i => Measures[i].Heading)], rows, keys);
    }
}
