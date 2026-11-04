using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Schedules;

/// <summary>A placed door, window or lintel: its entity, level, number within its entry (1-based) and whether it is external.</summary>
public sealed record Occurrence(string Id, string Level, int Number, bool? External);

/// <summary>A lintel group's facts: precast or cast, its product, section and length (m), steel per piece (kg), whether a stock length fits, concrete per piece (m³), the marks of what it spans.</summary>
public sealed record LintelInfo(bool Precast, string? Product, double Width, double Height, double Length, double SteelKg, bool Fits, double Volume, List<string> Openings);

/// <summary>
/// One line of a schedule: a door or window type (or a lintel group), its
/// mark and name, size (m; a lintel's Width holds its length), how many are
/// on each level, the total, and each placed one.
/// </summary>
public sealed record ScheduleEntry(string Key, string Kind, JoinerySpec? Spec, string Mark, string Name, double Width, double Height,
    Dictionary<string, int> Counts, int Total, List<Occurrence> Occurrences, LintelInfo? Lintel = null, IReadOnlyDictionary<string, string>? Params = null)
{
    /// <summary>A column of the schedule's own (decision 251): no type behind it, every value typed.</summary>
    public const string Manual = "manual";

    /// <summary>The key's ending for a type's mirror image (report #83), as IFC derives its mirrored type's GlobalId.</summary>
    public const string MirroredKey = "#mirrored";

    /// <summary>The type the entry lists (its key without the mirror's ending).</summary>
    public string TypeKey => Key.EndsWith(MirroredKey, StringComparison.Ordinal) ? Key[..^MirroredKey.Length] : Key;

    /// <summary>Whether it lists a type's mirror image: the doors and windows of the type flipped to its other hand.</summary>
    public bool Mirrored => Key.EndsWith(MirroredKey, StringComparison.Ordinal);
}

/// <summary>What a schedule lists, its levels (lowest first), and for joinery the lintel mark over each door and window.</summary>
public sealed record ScheduleData(List<ScheduleEntry> Entries, List<string> Levels, Dictionary<string, string>? LintelMarks = null);

/// <summary>
/// What the schedules list (the old Lapin's schedule-data and
/// lintel-schedule-data): door and window types with their placed
/// occurrences, counted per level, unplaced types too; lintels grouped by
/// kind, product and size, marked B1, B2…
/// </summary>
public static class Schedules
{
    public const string NoLevel = "—";

    static int CompareMarks(string a, string b) => string.Compare(a, b, System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.CompareOptions.NumericOrdering);

    /// <summary>A placed one's own mark: W1.3, or Door-3 for a type without a mark.</summary>
    public static string OccurrenceMark(ScheduleEntry e, Occurrence o) => e.Mark is { Length: > 0 } and not NoLevel ? $"{e.Mark}.{o.Number}" : $"{e.Name}-{o.Number}";

    static (List<string> Levels, Func<string, string> LevelOf) LevelsOf(IModelView m)
    {
        var storeys = Spatial.Storeys(m).OrderBy(s => s.Num("elevation")).ToList();
        return (storeys.Select(s => s.Name).Distinct().ToList(), id => Spatial.StoreyOf(m, id)?.Name ?? NoLevel);
    }

    /// <summary>The mark's ending for a type's mirror image: D and D-M (report #83).</summary>
    public const string MirroredMark = "-M";

    /// <summary>
    /// Doors, windows or both: one entry per type, its occurrences numbered by level, then as placed. The ones flipped to
    /// the type's other hand (on one axis, not both: <see cref="Architecture.Joinery.IsMirrored"/>) are an entry of their own
    /// (report #83): the type's mirror image, its mark with <see cref="MirroredMark"/>, "(mirrored)" after its name (as the
    /// IFC export names the mirrored type), its opening and drawings the other hand's.
    /// </summary>
    public static ScheduleData Joinery(IModelView m, string kind)
    {
        var (levels, levelOf) = LevelsOf(m);
        var entries = new List<ScheduleEntry>();
        foreach (var (typeClass, k) in new[] { (IfcClass.WindowType, "window"), (IfcClass.DoorType, "door") })
        {
            if (kind != "all" && kind != k) continue;
            foreach (var type in m.OfClass(typeClass))
            {
                var spec = JoinerySpec.From(type.Attrs["spec"]);
                var all = m.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(e => e.Class is IfcClass.Door or IfcClass.Window).ToList();
                var mark = spec?.Mark ?? type.Str("mark") ?? "";
                var mirrored = all.Where(Architecture.Joinery.IsMirrored).ToList();
                var own = all.Except(mirrored).ToList();
                ScheduleEntry Entry(string key, JoinerySpec? s, string mk, string name, List<Entity> placed)
                {
                    var counts = new Dictionary<string, int>();
                    var occ = placed.Select(e => (e, Level: levelOf(e.Id))).ToList();
                    foreach (var (_, level) in occ) counts[level] = counts.GetValueOrDefault(level) + 1;
                    var numbered = occ.OrderBy(x => levels.IndexOf(x.Level) is var i && i < 0 ? 99 : i).ThenBy(x => m.Order(x.e.Id))
                        .Select((x, i) => new Occurrence(x.e.Id, x.Level, i + 1, s?.IsExternal)).ToList();
                    return new ScheduleEntry(key, k, s, mk, name, s?.Width ?? 0, s?.Height ?? 0, counts, placed.Count, numbered, Params: ParamValues(type, placed));
                }
                // A type only ever placed flipped is listed by its mirror image alone; one placed nowhere, as itself (unused).
                if (own.Count > 0 || mirrored.Count == 0) entries.Add(Entry(type.Id, spec, mark, type.Name, own));
                if (mirrored.Count > 0)
                    entries.Add(Entry(type.Id + ScheduleEntry.MirroredKey, spec == null ? null : Architecture.Joinery.Mirrored(spec), mark.Length > 0 ? mark + MirroredMark : mark, $"{type.Name} (mirrored)", mirrored));
            }
        }
        // Windows before doors, then by mark (numbers as numbers), then width.
        entries = entries.OrderBy(e => e.Kind == "window" ? 0 : 1).ThenBy(e => e.Mark, Comparer<string>.Create(CompareMarks)).ThenBy(e => e.Width).ToList();
        if (entries.Any(e => e.Counts.ContainsKey(NoLevel))) levels.Add(NoLevel);
        return new ScheduleData(entries, levels);
    }

    /// <summary>
    /// The lintels over the openings of every wall (its derived lintel parts):
    /// grouped by kind, product, section, length and steel; precast first, then
    /// by length, height and width; marked B1, B2…; with what each spans.
    /// </summary>
    public static ScheduleData Lintels(IQueryContext q, ScheduleData? joinery = null)
    {
        var m = q.Model;
        var (levels, levelOf) = LevelsOf(m);
        joinery ??= Joinery(m, "all");
        var typeMarkOf = joinery.Entries.SelectMany(e => e.Occurrences.Select(o => (o.Id, e.Mark))).ToDictionary(x => x.Id, x => x.Mark);
        var pieces = new List<(string Group, LintelInfo Info, string Level, string Wall, List<string> Fills)>();
        foreach (var wall in m.OfClass(IfcClass.Wall))
        {
            if (q.Geometry(wall.Id) is not { } g) continue;
            var dir = (Walls.End(wall) - Walls.Start(wall)).Normalized();
            var across = dir.Perp;
            var wallRule = Walls.LintelRuleOf(m, wall);
            foreach (var part in g.Parts.Where(p => p.Key?.StartsWith("lintel:", StringComparison.Ordinal) == true))
            {
                var key = part.Key!["lintel:".Length..];
                key = key[..key.LastIndexOf(':')];
                // What it spans: each opening's filling (a door or window).
                var fills = key.Split('+').SelectMany(o => m.RelatedOf(o, IfcClass.RelFillsElement)).Select(f => f.Id).ToList();
                var own = fills.Select(f => Openings.SpecOf(m, f)?.Lintel).FirstOrDefault(r => r is { Mode: not "none" });
                var rule = own ?? wallRule;
                var vs = part.Solid.Vertices;
                double Extent(Func<Vec3, double> f) => vs.Max(f) - vs.Min(f);
                // In a curved wall the lintel is bent along the arc: measured along the axis and across it (radially), not on the chord.
                var curve = PathCurve.Of(wall);
                var length = curve.IsArc ? Extent(v => curve.Project(v.Xy)) : Extent(v => v.X * dir.X + v.Y * dir.Y);
                var width = curve.IsArc ? Extent(v => (v.Xy - curve.Centre).Length) : Extent(v => v.X * across.X + v.Y * across.Y);
                var height = Extent(v => v.Z);
                var precast = part.Name.Contains("precast", StringComparison.OrdinalIgnoreCase);
                var product = precast && rule?.Product is { } p ? $"{p.Manufacturer} {p.Code}".Trim() : null;
                var steel = !precast && rule?.Reinforcement is { } r ? Steel(r, width, height, length) : 0;
                var fits = !(precast && rule?.Product is { } pr && !pr.Lengths.Any(l => l >= length - 1e-6));
                var info = new LintelInfo(precast, product, width, height, length, Math.Round(steel, 2), fits, Measure.Volume(part.Solid), []);
                var group = new System.Text.Json.Nodes.JsonArray(precast, product, Mm(width), Mm(height), Mm(length), Math.Round(steel * 10)).ToJsonString();
                pieces.Add((group, info, levelOf(wall.Id), wall.Id, fills));
            }
        }
        var entries = pieces.GroupBy(p => p.Group).Select(g =>
        {
            var first = g.First().Info;
            var counts = new Dictionary<string, int>();
            foreach (var x in g) counts[x.Level] = counts.GetValueOrDefault(x.Level) + 1;
            var openings = g.SelectMany(x => x.Fills).Select(f => typeMarkOf.GetValueOrDefault(f, NoLevel)).Distinct().Order().ToList();
            var occ = g.Select((x, i) => new Occurrence(x.Wall, x.Level, i + 1, null)).ToList();
            return (Fills: g.SelectMany(x => x.Fills).ToList(), Entry: new ScheduleEntry("lintel:" + g.Key, "lintel", null, "", first.Product ?? "", first.Length, first.Height, counts, g.Count(), occ, first with { Openings = openings }));
        })
        .OrderBy(x => x.Entry.Lintel!.Precast ? 0 : 1).ThenBy(x => x.Entry.Lintel!.Length).ThenBy(x => x.Entry.Lintel!.Height).ThenBy(x => x.Entry.Lintel!.Width).ToList();
        var marks = new Dictionary<string, string>();
        var list = entries.Select((x, i) =>
        {
            var mark = $"B{i + 1}";
            foreach (var f in x.Fills) marks[f] = mark;
            return x.Entry with { Mark = mark };
        }).ToList();
        if (list.Any(e => e.Counts.ContainsKey(NoLevel))) levels.Add(NoLevel);
        return new ScheduleData(list, levels, marks);
    }

    static double Mm(double m) => Math.Round(m * 1000);

    /// <summary>A stored parameter's value as the schedule writes it: a length in mm, an area in m², yes or no.</summary>
    public static string ParamText(System.Text.Json.Nodes.JsonObject p)
    {
        var v = p["value"];
        if (v is null) return "";
        var kind = (string?)p["kind"] ?? "text";
        if (kind == "bool") return v is System.Text.Json.Nodes.JsonValue b && b.TryGetValue<bool>(out var x) ? (x ? "yes" : "no") : v.ToString();
        if (Json.Number(v) is { } n && kind is "number" or "length" or "area" or "angle")
            return (kind == "length" ? Math.Round(n * 1000, 1) : Math.Round(n, 4)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return v.ToString();
    }

    static IEnumerable<System.Text.Json.Nodes.JsonObject> StoredParams(Entity e) =>
        e.Attrs["params"] is System.Text.Json.Nodes.JsonArray a ? a.OfType<System.Text.Json.Nodes.JsonObject>().Where(p => ((string?)p["name"])?.Trim() is { Length: > 0 }) : [];

    /// <summary>
    /// A type's parameters as its schedule entry shows them (decision 251): the type's own, each placed one's own over it; where the
    /// placed ones differ, their values one after another. Keyed by the parameter's name (case aside, the first spelling kept).
    /// </summary>
    static Dictionary<string, string> ParamValues(Entity type, List<Entity> placed)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var typeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in StoredParams(type)) { var n = ((string)p["name"]!).Trim(); names.TryAdd(n, n); typeValues[n] = ParamText(p); }
        var own = placed.Select(e => StoredParams(e).GroupBy(p => ((string)p["name"]!).Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => ParamText(g.Last()), StringComparer.OrdinalIgnoreCase)).ToList();
        foreach (var o in own) foreach (var n in o.Keys) names.TryAdd(n, n);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in names.Values)
        {
            var list = placed.Count == 0 ? [typeValues.GetValueOrDefault(n, "")]
                : own.Select(o => o.TryGetValue(n, out var v) ? v : typeValues.GetValueOrDefault(n, "")).ToList();
            values[n] = string.Join(", ", list.Where(v => v.Length > 0).Distinct());
        }
        return values;
    }

    /// <summary>Whether a parameter's name says a fire rating (Fire rating, Fire Rated, FireRating, Fire resistance): the schedule's Fire rating row reads it.</summary>
    public static bool IsFireRating(string name)
    {
        var k = new string(name.Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray());
        return k is "firerating" or "firerated" or "fireresistance" or "fireresistancerating" or "rezistentalafoc" or "rezistenţalafoc";
    }

    /// <summary>Steel of a cast lintel (kg): its bars along it, its stirrups round it (7850 kg/m³).</summary>
    static double Steel(LintelReinforcement r, double width, double height, double length)
    {
        double Bars(LintelBars? b) => b == null ? 0 : b.Count * Math.PI * b.Diameter * b.Diameter / 4 * length;
        var cover = r.Cover ?? 0.025;
        var stirrups = r.Stirrups is { Spacing: > 0 } s
            ? (Math.Floor(length / s.Spacing) + 1) * Math.PI * s.Diameter * s.Diameter / 4 * Math.Max(0, 2 * (width + height) - 8 * cover)
            : 0;
        return (Bars(r.Top) + Bars(r.Bottom) + stirrups) * 7850;
    }

    /// <summary>The joinery a schedule lists, with the lintel marks over each door and window; or the lintels.</summary>
    public static ScheduleData Of(IQueryContext q, string kind)
    {
        var joinery = Joinery(q.Model, kind == "lintel" ? "all" : kind);
        var lintels = Lintels(q, joinery);
        return kind == "lintel" ? lintels : joinery with { LintelMarks = lintels.LintelMarks };
    }
}
