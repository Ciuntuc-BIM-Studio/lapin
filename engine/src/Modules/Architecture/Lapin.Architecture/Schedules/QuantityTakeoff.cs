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
        // Pieces of a material counted by its board (docs/plan-buildup-tags.md): its area over the board's, rounded up per row.
        ("Pieces", [Pieces]),
    ];

    /// <summary>The quantity a material counted by its board gives (pieces), made by the takeoff.</summary>
    public const string Pieces = "Pieces";

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

    /// <summary>
    /// A part's quantities as its material is measured (docs/plan-buildup-tags.md, <see cref="MaterialMeasure"/>): by area, its
    /// areas under the layer's thickness (a row per material × thickness: mineral wool 150 mm apart from 200 mm); by volume, its
    /// volumes; by mass, its weight from the volume and the material's density; by count, the pieces of its board, per thickness
    /// too. Each share: the thickness (mm, null for an element measured whole), the quantities, a note (not a catalogue thickness,
    /// no density, no board size). Null: the material has no rule, the part is measured as before.
    /// </summary>
    public static List<(double? Thickness, IReadOnlyList<Quantity> Quantities, string? Note)>? ByMeasure(Entity? material, IReadOnlyList<Quantity> qs, bool layered)
    {
        if (MaterialMeasure.Of(material) is not { } rule) return null;
        var areas = qs.Where(x => x.Kind == "AREA" && Measures.Take(3).Any(m => m.Names.Contains(x.Name))).ToList();
        var volumes = qs.Where(x => x.Kind == "VOLUME").ToList();
        var o = new List<(double?, IReadOnlyList<Quantity>, string?)>();
        // The layer's thickness (mm): its Width; none for an element measured whole.
        double? layer = layered && Pick(qs, ["Width"]) is { } w && w > 1e-9 ? w * 1000 : null;
        switch (rule.By)
        {
            case "volume":
                if (volumes.Count == 0) return null;
                o.Add((null, volumes, null));
                return o;
            case "mass":
                if (Pick(qs, ["NetVolume", "Volume", "GrossVolume"]) is not { } v) return null;
                if (MaterialMeasure.DensityOf(material) is not { } density) { o.Add((null, volumes, "no density: by volume")); return o; }
                o.Add((null, [new Quantity("Weight", v * density, "WEIGHT", "Weight")], null));
                return o;
        }
        if (areas.Count == 0) return null;
        IReadOnlyList<Quantity> counted = areas;
        string? note = null;
        if (rule.By == "count")
        {
            if (material != null && rule.BoardOf(material) is { } board && (Pick(areas, Measures[2].Names) ?? Pick(areas, Measures[1].Names)) is { } area)
                counted = [new Quantity(Pieces, area / (board.Length * board.Width), "COUNT", "Pieces"), .. areas];
            else note = "no board size: by area";
        }
        // A thickness the material is not made in (its catalogue, the thicknesses it is sold in) is flagged.
        if (layer is { } t && !MaterialMeasure.InCatalogue(rule, t)) note = note is null ? NotCatalogue : $"{NotCatalogue}; {note}";
        o.Add((layer is { } lt ? Math.Round(lt, 1) : null, counted, note));
        return o;
    }

    /// <summary>The note on a row whose layer is not one of its material's catalogue thicknesses.</summary>
    public const string NotCatalogue = "not a catalogue thickness";

    sealed class Sum
    {
        public readonly HashSet<string> Elements = [];
        public readonly double?[] Values = new double?[Measures.Length];
        public readonly SortedSet<string> Notes = new(StringComparer.Ordinal);
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
        var sums = new Dictionary<string, (string[] Cells, string Thickness, (int, string)[] Order, Sum Sum)>();
        string MaterialName(string? id) => id == null ? "—" : m.Get(id)?.Name is { Length: > 0 } n ? n : id;

        void Add(Entity e, string? material, IReadOnlyList<Quantity> qs, double share = 1, double? thickness = null, string? note = null)
        {
            var cells = new string[groups.Count];
            var order = new (int, string)[groups.Count + 1];
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
            var thick = thickness is { } th ? MaterialFields.Mm(th) : "";
            order[groups.Count] = (0, thick);
            var key = string.Join("\u001f", cells) + "\u001f" + thick;
            if (!sums.TryGetValue(key, out var row)) sums[key] = row = (cells, thick, order, new Sum());
            row.Sum.Add(e.Id, qs, share);
            if (note != null) row.Sum.Notes.Add(note);
        }

        /// <summary>A part (or an element of one material) under its material, as the material is measured.</summary>
        void AddMeasured(Entity e, string? material, IReadOnlyList<Quantity> qs, bool layered)
        {
            if (ByMeasure(material == null ? null : m.Get(material), qs, layered) is not { } shares) { Add(e, material, qs); return; }
            foreach (var (thickness, quantities, note) in shares) Add(e, material, quantities, 1, thickness, note);
        }

        foreach (var (e, sets) in Measured(q, ClassesOf(schedule)))
        {
            var main = sets.FirstOrDefault(s => s.Part == null);
            if (!byMaterial) { if (main != null) Add(e, null, main.Quantities); else foreach (var s in sets) Add(e, s.Material, s.Quantities); continue; }
            // By material: each part with its material (a layer, a zone, a finish, a lintel); else the element's own parts' material.
            var parts = sets.Where(s => s.Part != null && s.Material != null && HasMeasure(s.Quantities)).ToList();
            if (parts.Count > 0) { foreach (var s in parts) AddMeasured(e, s.Material, s.Quantities, true); continue; }
            if (main == null) continue;
            var built = q.Geometry(e.Id)?.Parts ?? [];
            var materials = built.Select(p => p.Material).Distinct().ToList();
            if (materials.Count <= 1) { AddMeasured(e, materials.FirstOrDefault(), main.Quantities, false); continue; }
            // Several materials and no measured parts: the volume shared out by the parts' own volumes, the rest left to the element.
            var volumes = built.Where(p => p.Instances == null).GroupBy(p => p.Material).ToDictionary(g => g.Key ?? "", g => g.Sum(p => Measure.Volume(p.Solid)));
            var total = volumes.Values.Sum();
            if (total <= 0) { Add(e, null, main.Quantities); continue; }
            var only = main.Quantities.Where(x => x.Kind == "VOLUME" || x.Kind == "WEIGHT").ToList();
            // Nothing to share out (a door measured by its opening): the element whole, under no one material.
            if (!HasMeasure(only)) { Add(e, null, main.Quantities); continue; }
            foreach (var (mat, v) in volumes)
            {
                var shared = only.Select(x => x with { Value = x.Value * v / total }).ToList();
                if (mat.Length > 0 && MaterialMeasure.Of(m.Get(mat)) is { By: "mass" or "volume" }) AddMeasured(e, mat, shared, false);
                else Add(e, mat.Length == 0 ? null : mat, shared);
            }
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
        // The thickness a row is in, and its notes, only when a material measured by thickness or with a note is in it.
        var thicknessShown = ordered.Any(r => r.Thickness.Length > 0);
        var notesShown = ordered.Any(r => r.Sum.Notes.Count > 0);
        string N(double? v, int i) => v is { } x ? i == 6 ? Math.Ceiling(x - 1e-9).ToString("0", CultureInfo.InvariantCulture)
            : x.ToString(i == 5 ? "0.0" : i is 3 or 4 ? "0.000" : "0.00", CultureInfo.InvariantCulture) : "";
        var rows = new List<IReadOnlyList<string>>();
        var keys = new List<string?>();
        foreach (var r in ordered)
        {
            rows.Add([.. r.Cells, .. thicknessShown ? [r.Thickness] : Array.Empty<string>(), r.Sum.Elements.Count.ToString(CultureInfo.InvariantCulture), .. shown.Select(i => N(r.Sum.Values[i], i)),
                .. notesShown ? [string.Join("; ", r.Sum.Notes)] : Array.Empty<string>()]);
            keys.Add(string.Join("|", r.Cells) + (r.Thickness.Length > 0 ? "|" + r.Thickness : ""));
        }
        if (rows.Count > 1)
        {
            var count = ordered.SelectMany(r => r.Sum.Elements).Distinct().Count();
            rows.Add(["Total", .. Enumerable.Repeat("", groups.Count - 1), .. thicknessShown ? [""] : Array.Empty<string>(), count.ToString(CultureInfo.InvariantCulture),
                .. shown.Select(i => N(i == 6 ? ordered.Sum(r => r.Sum.Values[i] is { } p ? Math.Ceiling(p - 1e-9) : 0) : ordered.Sum(r => r.Sum.Values[i] ?? 0), i)),
                .. notesShown ? [""] : Array.Empty<string>()]);
            keys.Add(null);
        }
        return new ScheduleGrid(Title, [.. groups.Select(g => Headings[g]), .. thicknessShown ? ["Thickness (mm)"] : Array.Empty<string>(), "Count", .. shown.Select(i => Measures[i].Heading),
            .. notesShown ? ["Note"] : Array.Empty<string>()], rows, keys);
    }
}
