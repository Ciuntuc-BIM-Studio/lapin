using System.Globalization;
using System.Text.RegularExpressions;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Documents;

namespace Lapin.Architecture;

/// <summary>
/// The architecture's fields for documents (docs/plan-documents.md, Câmpurile):
/// <list type="bullet">
/// <item><c>areas.built</c>, the ground floor's footprint: the area inside the outer outline of its walls, curtain walls, columns
/// and slabs, cut where they stand (the storey at ±0.00, else the lowest above ground); <c>areas.gross</c>, the sum of every
/// storey's footprint; <c>areas.basement</c>, the basements' (empty without one); <c>areas.usable</c>, the rooms' net floor
/// area;</item>
/// <item><c>height.max</c>, the highest top of the building's elements above ±0.00; <c>levels.text</c>, the storeys as
/// S+P+nE+M (a storey counts when it has walls or rooms: below −1.00 m a basement S, the lowest other one the ground floor P,
/// one named attic, loft, pod or mansardă M, the others floors E);</item>
/// <item><c>counts.doors</c>, <c>counts.windows</c>, <c>counts.rooms</c>;</item>
/// </list>
/// the collections <c>rooms</c> ({{room.*}}), <c>wallTypes</c>, <c>slabTypes</c> (those used, their layers as text), <c>roofs</c>,
/// <c>materials</c> (those used) and <c>joineryTypes</c>; and each of the platform's <c>storeys</c> its <c>area</c>, <c>usable</c>
/// and <c>rooms</c>, merged by id.
/// </summary>
public sealed partial class DocumentFields : IDocumentFields
{
    /// <summary>A storey below this level (m) is a basement.</summary>
    public const double BasementBelow = -1.0;

    static readonly string[] Footprinted = [IfcClass.Wall, CurtainWalls.Class, IfcClass.Column, IfcClass.Slab];
    static readonly string[] Counted = [IfcClass.Wall, CurtainWalls.Class, Rooms.Class];
    static readonly string[] Tall = [IfcClass.Wall, CurtainWalls.Class, IfcClass.Slab, IfcClass.Roof, IfcClass.Column, IfcClass.Beam, IfcClass.Stair, "IfcRailing", "IfcChimney", "IfcMember", IfcClass.BuildingElementProxy];

    [GeneratedRegex(@"\b(mansard\w*|attic|loft|pod)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AtticName();

    /// <summary>Whether a storey counts in the building's levels and areas: it has walls or rooms.</summary>
    static bool HasContent(IModelView m, Entity storey) =>
        m.RelatedOf(storey.Id, IfcClass.RelContainedInSpatialStructure).Any(e => Counted.Contains(e.Class));

    /// <summary>The storeys that count, bottom up; every storey when none has anything yet.</summary>
    static List<Entity> Counting(IModelView m) => Spatial.Storeys(m).OrderBy(s => s.Num("elevation")).Where(s => HasContent(m, s)).ToList();

    static Entity? Ground(IReadOnlyList<Entity> storeys) => storeys.FirstOrDefault(s => s.Num("elevation") >= BasementBelow);

    static bool IsAttic(Entity s) => AtticName().IsMatch(s.Name);

    /// <summary>The storeys as S+P+nE+M (see the class).</summary>
    public static string LevelsText(IReadOnlyList<Entity> storeys)
    {
        if (storeys.Count == 0) return "";
        var basements = storeys.Count(s => s.Num("elevation") < BasementBelow);
        var ground = Ground(storeys);
        var above = ground == null ? [] : storeys.Where(s => s.Num("elevation") > ground.Num("elevation") + 1e-6).ToList();
        var attics = above.Count(IsAttic);
        var floors = above.Count - attics;
        var parts = new List<string>();
        if (basements > 0) parts.Add(basements == 1 ? "S" : $"{basements}S");
        if (ground != null) parts.Add(IsAttic(ground) ? "M" : "P");
        if (floors > 0) parts.Add($"{floors}E");
        if (attics > 0) parts.Add(attics == 1 ? "M" : $"{attics}M");
        return string.Join("+", parts);
    }

    /// <summary>
    /// A storey's footprint (m²): its walls, curtain walls, columns and slabs cut where they stand (a wall with its joins, as rooms
    /// find it), the outer outlines of what they make together, with all they close inside.
    /// </summary>
    public static double Footprint(IQueryContext q, Entity storey)
    {
        var m = q.Model;
        var paths = q.Services.Get<IPathElements>();
        var cutters = q.Services.Get<ICutters>();
        var areas = new List<IReadOnlyList<IReadOnlyList<Vec2>>>();
        foreach (var e in m.RelatedOf(storey.Id, IfcClass.RelContainedInSpatialStructure))
        {
            if (!Footprinted.Contains(e.Class)) continue;
            if (e.Class != IfcClass.Slab && paths != null && RoomBounds.JoinedFootprint(e, m, paths) is { } joined) { areas.Add([joined]); continue; }
            Solid? body;
            try { body = cutters?.EnvelopeOf(e, m); }
            catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException) { continue; }
            if (body == null || Measure.Bounds(body) is not { } b) continue;
            var z = (b.Min.Z + b.Max.Z) / 2;
            var loops = HiddenLines.Section(body, new ViewFrame(new Vec3(0, 0, z), new Vec3(1, 0, 0), new Vec3(0, 1, 0), -Vec3.UnitZ));
            if (loops.Count > 0) areas.Add(loops.Select(l => (IReadOnlyList<Vec2>)l).ToList());
        }
        if (areas.Count == 0) return 0;
        var all = FreeSpace.Of(areas).Loops.Select(l => (Loop: l, Area: Polygon2.SignedArea(l))).ToList();
        // Clockwise loops go round material; the outer ones are those inside no loop of free space (a room's island column is).
        double total = 0;
        foreach (var (loop, area) in all.Where(x => x.Area < 0))
        {
            var probe = (loop[0] + loop[1]) * 0.5;
            if (all.Any(c => c.Area > 0 && c.Area > -area && Polygon2.Contains(c.Loop, probe))) continue;
            total -= area;
        }
        return total;
    }

    static double? Positive(double v) => v > 1e-9 ? v : null;

    public IEnumerable<DocField> Fields(IQueryContext q, DocFormat f)
    {
        var m = q.Model;
        var storeys = Counting(m);
        var foot = storeys.ToDictionary(s => s.Id, s => Footprint(q, s));
        var ground = Ground(storeys);
        string? Area(double? v) => v is { } x ? DocNumbers.Area(x, f) : null;
        yield return new("areas.built", "Built area", Area(ground == null ? null : Positive(foot[ground.Id])));
        yield return new("areas.gross", "Gross floor area", Area(Positive(foot.Values.Sum())));
        yield return new("areas.basement", "Basement area", Area(Positive(storeys.Where(s => s.Num("elevation") < BasementBelow).Sum(s => foot[s.Id]))));
        // The rooms as each is shown (decision 471), so the usable area adds up the room table.
        yield return new("areas.usable", "Usable area", Area(Positive(AreaRounding.Sum(m.OfClass(Rooms.Class).Select(r => Rooms.NetAreaOf(r, m, q.Geometry)), f.AreaStep, f.Units))));
        yield return new("height.max", "Maximum height", MaxHeight(q) is { } h ? DocNumbers.Length(h, f) : null);
        yield return new("levels.text", "Height regime", LevelsText(storeys) is { Length: > 0 } t ? t : null);
        yield return new("counts.doors", "Doors", m.OfClass(IfcClass.Door).Count().ToString(CultureInfo.InvariantCulture));
        yield return new("counts.windows", "Windows", m.OfClass(IfcClass.Window).Count().ToString(CultureInfo.InvariantCulture));
        yield return new("counts.rooms", "Rooms", m.OfClass(Rooms.Class).Count().ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The highest top of the building's elements above ±0.00 (m); null when nothing stands above it.</summary>
    static double? MaxHeight(IQueryContext q)
    {
        double? top = null;
        foreach (var cls in Tall)
            foreach (var e in q.Model.OfClass(cls))
                foreach (var p in q.Geometry(e.Id)?.Parts ?? [])
                    if (p.Bounds() is { } b && b.Max.Z > (top ?? 0)) top = b.Max.Z;
        return top;
    }

    public IEnumerable<DocCollection> Collections =>
    [
        new("rooms", "Rooms", "room"),
        new("wallTypes", "Wall types", "wallType"),
        new("slabTypes", "Slab types", "slabType"),
        new("roofs", "Roofs", "roof"),
        new("materials", "Materials", "material"),
        new("joineryTypes", "Door and window types", "joinery"),
    ];

    /// <summary>A build-up as text: each layer's material and thickness, "Brick 25 cm + EPS 10 cm"; a layer of no thickness by its name alone.</summary>
    public static string LayersText(IEnumerable<(string Material, double Thickness)> layers, DocFormat f) =>
        string.Join(" + ", layers.Select(l => l.Thickness > 1e-9 ? $"{l.Material} {DocNumbers.Thickness(l.Thickness, f)}" : l.Material));

    static string MaterialName(IModelView m, string id) => m.Get(id)?.Name ?? id;

    /// <summary>The types the model's elements of <paramref name="cls"/> use, in the order they were made, with how many use each.</summary>
    static List<(Entity Type, int Count)> UsedTypes(IModelView m, string cls) =>
        m.OfClass(cls).Select(e => m.RelatingOf(e.Id, IfcClass.RelDefinesByType)).OfType<Entity>().GroupBy(t => t.Id)
            .Select(g => (g.First(), g.Count())).OrderBy(x => m.Order(x.Item1.Id)).ToList();

    static string? FinishText(IModelView m, Entity room, string part, DocFormat f)
    {
        if (room.Str("finishType") is { } tid && m.Get(tid) is { } type && RoomFinishes.SpecOf(m, type) is { } spec)
        {
            var layers = part switch { "floor" => spec.Floor, "walls" => spec.Walls, _ => spec.Ceiling };
            if (layers is { Count: > 0 }) return LayersText(layers.Select(l => (l.Material, l.Thickness)), f);
        }
        return part == "floor" ? room.Str("finishFloor") : null;
    }

    public IEnumerable<IReadOnlyDictionary<string, string>> Items(IQueryContext q, string collection, DocFormat f)
    {
        var m = q.Model;
        switch (collection)
        {
            case "storeys":
                return Spatial.Storeys(m).Select(s =>
                {
                    var rooms = m.RelatedOf(s.Id, IfcClass.RelContainedInSpatialStructure).Where(e => e.Class == Rooms.Class).ToList();
                    var usable = AreaRounding.Sum(rooms.Select(r => Rooms.NetAreaOf(r, m, q.Geometry)), f.AreaStep, f.Units);
                    return (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
                    {
                        ["id"] = s.Id, ["area"] = HasContent(m, s) ? DocNumbers.Area(Footprint(q, s), f) : "",
                        ["usable"] = usable > 0 ? DocNumbers.Area(usable, f) : "", ["rooms"] = rooms.Count.ToString(CultureInfo.InvariantCulture),
                    };
                }).ToList();
            case "rooms":
            {
                var order = Spatial.Storeys(m).Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
                return m.OfClass(Rooms.Class).Select(r => (Room: r, Storey: Spatial.StoreyOf(m, r.Id)))
                    .OrderBy(x => x.Storey == null ? int.MaxValue : order.GetValueOrDefault(x.Storey.Id))
                    .ThenBy(x => x.Room.Str("number") ?? "", StringComparer.OrdinalIgnoreCase)
                    .Select(x =>
                    {
                        var o = Rooms.Of(x.Room, m, q.Geometry) is { } outline ? Rooms.MeasuresOf(x.Room, outline, m) : null;
                        var h = Rooms.HeightOf(x.Room, m);
                        return (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
                        {
                            ["id"] = x.Room.Id, ["storeyId"] = x.Storey?.Id ?? "", ["storey"] = x.Storey?.Name ?? "",
                            ["number"] = x.Room.Str("number") ?? "", ["name"] = x.Room.Name, ["function"] = x.Room.Str("function") ?? "", ["department"] = x.Room.Str("department") ?? "",
                            ["area"] = o == null ? "" : DocNumbers.Area(o.NetArea, f), ["perimeter"] = o == null ? "" : DocNumbers.Length(o.Perimeter, f),
                            ["height"] = DocNumbers.Length(h, f), ["volume"] = o == null ? "" : DocNumbers.Number(o.NetVolume, f) + " m³",
                            ["floor"] = FinishText(m, x.Room, "floor", f) ?? "", ["walls"] = FinishText(m, x.Room, "walls", f) ?? "", ["ceiling"] = FinishText(m, x.Room, "ceiling", f) ?? "",
                        };
                    }).ToList();
            }
            case "wallTypes":
            case "slabTypes":
                return UsedTypes(m, collection == "wallTypes" ? IfcClass.Wall : IfcClass.Slab).Select(x =>
                {
                    var layers = LayerSets.LayersOf(m, x.Type.Id);
                    return (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
                    {
                        ["id"] = x.Type.Id, ["name"] = x.Type.Name, ["mark"] = x.Type.Str("mark") ?? "",
                        ["thickness"] = layers.Count > 0 ? DocNumbers.Thickness(layers.Sum(l => l.Thickness), f) : "",
                        ["layers"] = LayersText(layers.Select(l => (MaterialName(m, l.Material), l.Thickness)), f),
                        ["core"] = LayersText(layers.Where(l => l.Core).Select(l => (MaterialName(m, l.Material), l.Thickness)), f),
                        ["used"] = x.Count.ToString(CultureInfo.InvariantCulture),
                    };
                }).ToList();
            case "roofs":
                return m.OfClass(IfcClass.Roof).Select(r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
                {
                    ["id"] = r.Id, ["name"] = r.Name, ["storey"] = Spatial.StoreyOf(m, r.Id)?.Name ?? "", ["shape"] = r.Str("shape") ?? "hip",
                    ["pitch"] = r.Str("shape") == "flat" ? "" : DocNumbers.Number(r.NumOrNull("pitch") ?? 30, f, 0) + "°",
                    ["covering"] = Roofs.RoofCatalog.CoveringOf(r.Str("covering")).Id,
                }).ToList();
            case "materials":
                return UsedMaterials(m).Select(mat => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
                {
                    ["id"] = mat.Id, ["name"] = mat.Name, ["category"] = mat.Str("category") is { Length: > 0 } c ? c : Materials.IsFinish(mat) ? "Finish" : "Other",
                }).ToList();
            case "joineryTypes":
                return Schedules.Schedules.Of(q, "all").Entries.Where(e => e.Total > 0 && e.Kind is "door" or "window").Select(e => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
                {
                    ["id"] = e.Key, ["mark"] = e.Mark, ["name"] = e.Name, ["kind"] = e.Kind,
                    ["width"] = Math.Round(e.Width * 1000).ToString(CultureInfo.InvariantCulture), ["height"] = Math.Round(e.Height * 1000).ToString(CultureInfo.InvariantCulture),
                    ["size"] = $"{Math.Round(e.Width * 1000).ToString(CultureInfo.InvariantCulture)} × {Math.Round(e.Height * 1000).ToString(CultureInfo.InvariantCulture)}",
                    ["count"] = e.Total.ToString(CultureInfo.InvariantCulture),
                }).ToList();
            default:
                return [];
        }
    }

    /// <summary>The materials the project uses: in the layers of the wall and slab types its elements have, in its rooms' finishes, and named by an element (its <c>material</c>); in the order they were made.</summary>
    static List<Entity> UsedMaterials(IModelView m)
    {
        var ids = new HashSet<string>();
        foreach (var cls in new[] { IfcClass.Wall, IfcClass.Slab })
            foreach (var (type, _) in UsedTypes(m, cls))
                foreach (var l in LayerSets.LayersOf(m, type.Id))
                {
                    ids.Add(l.Material);
                    foreach (var p in l.Parts ?? []) ids.Add(p.Material);
                }
        foreach (var tid in m.OfClass(Rooms.Class).Select(r => r.Str("finishType")).OfType<string>().Distinct())
            if (m.Get(tid) is { } type && RoomFinishes.SpecOf(m, type) is { } spec)
                foreach (var name in spec.Materials())
                    if (Materials.Find(m, name) is { } mat) ids.Add(mat.Id);
        foreach (var e in m.Entities)
            if (e.Str("material") is { } mid && m.Get(mid) is { Class: IfcClass.Material }) ids.Add(mid);
        return m.OfClass(IfcClass.Material).Where(x => ids.Contains(x.Id)).OrderBy(x => m.Order(x.Id)).ToList();
    }
}
