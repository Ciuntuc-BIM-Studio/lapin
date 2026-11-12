using System.Globalization;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;

namespace Lapin.Analysis;

/// <summary>
/// A load derived from the model (docs/plan-fem.md §8.4), never stored: its kind (selfWeight, finishes, partitions, occupancy),
/// the case that takes it (null: none does), the element it comes from, where it lies (an area's outline with its holes, a
/// line's polyline, world metres, <see cref="Z"/> absolute; nothing for a self weight), its value in <see cref="Unit"/> and its
/// whole vertical force (kN). The rooms' carry their <see cref="Category"/>.
/// </summary>
public sealed record DerivedLoad(string Kind, string? CaseId, string SourceId, string SourceName, string? StoreyId, string Shape,
    IReadOnlyList<Vec2> Points, double Z, double Value, string Unit, double Total, string? Category = null)
{
    public IReadOnlyList<IReadOnlyList<Vec2>> Holes { get; init; } = [];
}

/// <summary>
/// The project's loads worked out on every ask (docs/plan-fem.md §8.4): the cases, the user's loads, and the derived loads —
/// the self weight (Σ volume × density × g over the analytical model's members: walls' and slabs' cores, bars), the slabs'
/// layers outside their core (Σ thickness × density × g over the slab's outline, at its top face), the walls outside the
/// analytical model (their body's parts' weight over their axis' length, at their base) and the rooms (their category's qk over
/// their outline, at their storey's level). Densities are the material's (<see cref="MaterialMeasure.DensityOf"/>), else
/// what <see cref="StructuralMaterials"/> reads from its name; what has none is counted and said.
/// </summary>
public sealed class LoadsReport
{
    public required List<LoadCase> Cases { get; init; }
    public required List<UserLoad> UserLoads { get; init; }
    public required List<DerivedLoad> Derived { get; init; }
    public required AnalyticalModel Model { get; init; }
    /// <summary>The rooms of each category no case takes.</summary>
    public required Dictionary<string, List<string>> Missing { get; init; }
    public required List<string> Notes { get; init; }

    /// <summary>The model's members' weight by element is in the self weight's case; the members counted, and those without a density.</summary>
    public int Weighed { get; private init; }

    /// <summary>The analytical model the loads stand on: every load-bearing wall, the feet fixed (as JG's export by default).</summary>
    public static readonly AnalyticalOptions Options = new();

    public LoadCase? SelfWeightCase => Cases.FirstOrDefault(c => c.SelfWeight);

    /// <summary>The vertical force (kN) in a case: its user loads, its derived loads, the self weight if it carries it.</summary>
    public double Total(LoadCase c) =>
        UserLoads.Where(l => l.CaseId == c.Id).Sum(l => l.Vertical) + Derived.Where(d => d.CaseId == c.Id).Sum(d => d.Total);

    /// <summary>Whether a case holds anything: the self weight of a member, a derived load or a user's.</summary>
    public bool Empty(LoadCase c) => !UserLoads.Any(l => l.CaseId == c.Id) && !Derived.Any(d => d.CaseId == c.Id);

    static string N(double v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);

    public static LoadsReport Compute(IQueryContext q)
    {
        var m = q.Model;
        var cases = LoadCases.All(m);
        var notes = new List<string>();
        var derived = new List<DerivedLoad>();
        var model = AnalyticalModel.Build(m, q.Services, Options);
        var densities = new Densities(m);
        string? StoreyOf(string id) => Spatial.StoreyOf(m, id)?.Id;
        string NameOf(string id, string fallback) => m.Get(id)?.Name is { Length: > 0 } n ? n : fallback;

        // 1. The self weight: each member of the analytical model, its volume by its material's density.
        var selfCase = cases.FirstOrDefault(c => c.SelfWeight);
        var weights = new Dictionary<string, (string Name, string? Storey, double Kn)>();
        var unweighed = new List<string>();
        void Weigh(string sourceId, string name, string? storey, string? material, double volume)
        {
            if (densities.Of(material) is not { } rho) { if (!unweighed.Contains(name)) unweighed.Add(name); return; }
            var kn = volume * rho * Units.G / 1000;
            weights[sourceId] = weights.TryGetValue(sourceId, out var w) ? (w.Name, w.Storey, w.Kn + kn) : (name, storey, kn);
        }
        foreach (var f in model.Frames)
        {
            var length = (f.J.At + f.OffJ - (f.I.At + f.OffI)).Length;
            if (SectionProperties.Of(f.Source.Section.Shape)?.Area is not { } area || area <= 0) { if (!unweighed.Contains(f.Source.Name)) unweighed.Add(f.Source.Name); continue; }
            Weigh(f.Source.SourceId, NameOf(f.Source.SourceId, f.Source.Name), f.Source.StoreyId, f.Source.MaterialId, area * length);
        }
        foreach (var s in model.Shells)
        {
            var area = VecMath.NewellNormal(s.Nodes.Select(n => n.At).ToList()).Length;
            Weigh(s.Source.SourceId, NameOf(s.Source.SourceId, s.Source.Name), s.Source.StoreyId, s.Source.MaterialId, area * s.Source.Thickness);
        }
        foreach (var (id, w) in weights)
            derived.Add(new DerivedLoad("selfWeight", selfCase?.Id, id, w.Name, w.Storey, "none", [], 0, w.Kn, "kN", w.Kn));
        if (unweighed.Count > 0)
            notes.Add($"{unweighed.Count} member{(unweighed.Count == 1 ? " has" : "s have")} no density or section, their self weight left out: {string.Join(", ", unweighed.Take(10))}{(unweighed.Count > 10 ? "…" : "")}.");

        // 2. The slabs' layers outside their core, over the slab's outline at its top face.
        var finishesCase = LoadCases.Taking(cases, LoadCases.Finishes);
        // The materials without a density, by name, and the elements whose layers or parts they leave out: said once each, not per layer.
        var weightless = new Dictionary<string, HashSet<string>>();
        void Weightless(string? material, string fallback, string element)
        {
            var key = material != null && m.Get(material)?.Name is { Length: > 0 } mn ? mn : material ?? fallback;
            (weightless.TryGetValue(key, out var set) ? set : weightless[key] = []).Add(element);
        }
        foreach (var slab in model.Shells.Select(s => s.Source).Where(s => s.Kind == "slab" && s.Outline is { Count: >= 3 }).Distinct())
        {
            if (LayerSets.BandsOf(m, slab.SourceId) is not { Count: > 0 } bands) continue;
            var core = bands.Any(b => b.Core && b.To - b.From > 1e-6) ? bands.First(b => b.Core && b.To - b.From > 1e-6) : bands.MaxBy(b => b.To - b.From);
            var top = slab.Z - (core.From + core.To) / 2 + bands.Max(b => b.To);
            double w = 0;
            foreach (var b in bands.Where(b => b.Index != core.Index && b.To - b.From > 1e-9))
            {
                if (densities.Of(b.Material) is not { } rho) { Weightless(b.Material, b.Name, slab.SourceId); continue; }
                w += (b.To - b.From) * rho * Units.G / 1000;
            }
            if (w <= 0) continue;
            var area = Math.Abs(Polygon2.SignedArea(slab.Outline!)) - slab.Holes.Sum(h => Math.Abs(Polygon2.SignedArea(h)));
            derived.Add(new DerivedLoad(LoadCases.Finishes, finishesCase?.Id, slab.SourceId, NameOf(slab.SourceId, slab.Name), slab.StoreyId, "area",
                slab.Outline!, top, w, "kN/m²", w * area) { Holes = slab.Holes });
        }

        // 3. The walls outside the analytical model (not load-bearing): their body's weight along their axis, at their base.
        var partitionsCase = LoadCases.Taking(cases, LoadCases.Partitions);
        var inModel = model.Shells.Select(s => s.Source.SourceId).ToHashSet();
        var contributed = new AnalyticalContribution { Services = q.Services };
        foreach (var src in q.Services.All<IAnalyticalElements>()) src.Contribute(m, contributed);
        foreach (var wall in contributed.Shells.Where(s => s.Kind == "wall" && s.Line is { Count: >= 2 } && !inModel.Contains(s.SourceId)).GroupBy(s => s.SourceId).Select(g => g.First()))
        {
            var line = wall.Line!;
            var length = 0.0;
            for (var i = 1; i < line.Count; i++) length += (line[i] - line[i - 1]).Length;
            if (length < 1e-6 || q.Geometry(wall.SourceId) is not { } g) continue;
            double weight = 0;
            foreach (var p in g.Parts)
            {
                if (densities.Of(p.Material) is not { } rho) { Weightless(p.Material, p.Name, wall.SourceId); continue; }
                weight += Measure.Volume(p.Solid) * rho * Units.G / 1000;
            }
            if (weight <= 0) continue;
            derived.Add(new DerivedLoad(LoadCases.Partitions, partitionsCase?.Id, wall.SourceId, NameOf(wall.SourceId, wall.Name), wall.StoreyId ?? StoreyOf(wall.SourceId), "line",
                line, wall.Z0, weight / length, "kN/m", weight));
        }

        if (weightless.Count > 0)
            notes.Add($"{weightless.Count} material{(weightless.Count == 1 ? " has" : "s have")} no density, left out of the floors' finishes and the partitions: "
                + string.Join(", ", weightless.OrderByDescending(x => x.Value.Count).ThenBy(x => x.Key, StringComparer.Ordinal).Take(10).Select(x => $"{x.Key} ({x.Value.Count} element{(x.Value.Count == 1 ? "" : "s")})"))
                + (weightless.Count > 10 ? "…" : "") + ".");

        // 4. The rooms: their category's imposed load over their outline (without its islands), at their storey's level.
        var missing = new Dictionary<string, List<string>>();
        foreach (var room in m.OfClass(IfcClass.Space))
        {
            var category = Categories.OfRoom(room);
            if (category == Categories.None || Categories.Qk(m, category) is not { } qk) continue;
            if (q.Geometry(room.Id)?.Plan?.FirstOrDefault(p => p.Kind == "room") is not { Points.Count: >= 3 } outline) continue;
            var area = Math.Abs(Polygon2.SignedArea(outline.Points));
            if (area < 1e-9) continue;
            var storey = Spatial.StoreyOf(m, room.Id);
            var c = LoadCases.Taking(cases, LoadCases.Occupancy, category);
            if (c == null) (missing.TryGetValue(category, out var l) ? l : missing[category] = []).Add(room.Id);
            derived.Add(new DerivedLoad(LoadCases.Occupancy, c?.Id, room.Id, room.Name, storey?.Id, "area", outline.Points, storey?.Num("elevation") ?? 0, qk, "kN/m²", qk * area, category));
        }

        if (selfCase == null && weights.Count > 0) notes.Add("No case carries the self weight: give one permanent case selfWeight.");
        if (finishesCase == null && derived.Any(d => d.Kind == LoadCases.Finishes)) notes.Add("The floors' finishes are in no case: give one case derives finishes.");
        if (partitionsCase == null && derived.Any(d => d.Kind == LoadCases.Partitions)) notes.Add("The partitions are in no case: give one case derives partitions.");
        foreach (var (cat, rooms) in missing)
            notes.Add($"{rooms.Count} room{(rooms.Count == 1 ? "" : "s")} of category {cat} {(rooms.Count == 1 ? "is" : "are")} in no case: analysis.addMissingCases makes Q-{cat}.");
        if (derived.Any(d => d.Kind == LoadCases.Occupancy)) notes.Add($"Imposed loads {Categories.Indicative}.");

        return new LoadsReport
        {
            Cases = cases, UserLoads = global::Lapin.Analysis.UserLoads.All(m).Where(l => l.Points.Count > 0).ToList(), Derived = derived, Model = model,
            Missing = missing, Notes = notes, Weighed = weights.Count,
        };
    }

    /// <summary>A material's density (kg/m³) by its id or name: its own, else what its name says (a timber grade, steel, reinforced concrete); null without one.</summary>
    internal sealed class Densities(IModelView m)
    {
        readonly Dictionary<string, double?> cache = [];

        public double? Of(string? id)
        {
            if (id == null) return null;
            if (cache.TryGetValue(id, out var d)) return d;
            var e = m.Get(id) is { Class: IfcClass.Material } own ? own : Materials.Find(m, id);
            double? rho = e != null ? MaterialMeasure.DensityOf(e) : null;
            rho ??= StructuralMaterials.Of(m, e ?? new Entity($"lapin-material-{id}", IfcClass.Material, id, [])).Density?.Value;
            return cache[id] = rho is > 0 ? rho : null;
        }
    }
}
