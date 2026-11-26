using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Archicad;

/// <summary>What a receive did to the project's storeys: matched to Archicad's stories, made, renamed, moved, removed, kept.</summary>
public sealed record ArchicadStoreyReport(int Matched, int Made, int Renamed, int Moved, int Removed, int Kept)
{
    public JsonObject ToJson() => new() { ["matched"] = Matched, ["made"] = Made, ["renamed"] = Renamed, ["moved"] = Moved, ["removed"] = Removed, ["kept"] = Kept };
}

/// <summary>
/// Archicad's stories as the project's storeys (docs/plan-archicad.md §3, as Speckle's levels are, <c>SpeckleLevels</c>): each found by
/// the GlobalId Archicad exports it with (its <c>ifcId</c>: a storey Lapin sent or an earlier receive made), else the storey an earlier
/// receive of the model made for it (by its index), else by its level (5 cm, the same name first), else by its name; a story none
/// takes is made a storey (its ifcId its GlobalId) when asked to, the default. Received again, a storey this model made is renamed and
/// moved with its story while nothing but this model's references stands on it; one whose story is gone is removed when nothing stands
/// on it. A storey the project had is never renamed, moved or removed.
/// </summary>
public sealed class ArchicadStoreys
{
    public const string Key = "archicadStory";

    readonly Dictionary<int, string> _of = [];
    readonly string _key;
    readonly ArchicadModel _model;
    readonly List<string> _notes;
    int matched, made, renamed, moved, removed, kept;
    string? _fallback;

    ArchicadStoreys(string key, ArchicadModel model, List<string> notes) { _key = key; _model = model; _notes = notes; }

    public ArchicadStoreyReport Report => new(matched, made, renamed, moved, removed, kept);

    public IReadOnlyDictionary<int, string> ByIndex => _of;

    static List<Entity> Made(IModelView m, string key) => [.. Spatial.Storeys(m).Where(e => (string?)(e.Attrs[Key] as JsonObject)?["key"] == key)];

    static bool Ours(Entity e, string key) => (string?)(e.Attrs[ArchicadReceive.Key] as JsonObject)?["key"] == key;

    static List<Entity> On(IModelView m, string storeyId) =>
        [.. m.Relations.Where(r => r.Class == IfcClass.RelContainedInSpatialStructure && r.Relating == storeyId).SelectMany(r => r.Related).Select(m.Get).OfType<Entity>()];

    /// <summary>The storey an element of story <paramref name="floor"/> goes on; when its story matched none, the one under its lowest point, else the lowest.</summary>
    public string StoreyOf(int floor, double? lowest, IEdit edit)
    {
        if (_of.TryGetValue(floor, out var id) && edit.Model.Get(id) != null) return id;
        var storeys = Spatial.Storeys(edit.Model).Select(x => (x.Id, Elevation: x.Num("elevation"))).OrderBy(x => x.Elevation).ToList();
        var z = lowest ?? _model.Story(floor)?.Level;
        if (z is { } at && storeys.LastOrDefault(x => x.Elevation <= at + ImportStoreys.Tolerance).Id is { } under) return under;
        if (storeys.Count > 0) return storeys[0].Id;
        return _fallback ??= Spatial.EnsureStorey(edit, null).Id;
    }

    public static ArchicadStoreys Resolve(IEdit edit, ArchicadModel a, string key, double shiftZ, bool create, List<string> notes)
    {
        var m = edit.Model;
        var o = new ArchicadStoreys(key, a, notes);
        var before = Made(m, key).ToDictionary(e => (int)(Json.Number((e.Attrs[Key] as JsonObject)?["index"]) ?? int.MinValue), e => e);
        var renamedNames = new List<string>();
        var held = new List<string>();
        foreach (var s in a.Stories.OrderBy(s => s.Level))
        {
            var z = Math.Round(s.Level + shiftZ, 6);
            var storeys = Spatial.Storeys(m).Select(x => (x.Id, x.Name, Elevation: x.Num("elevation"))).ToList();
            if (s.IfcId is { } g && storeys.Any(x => x.Id == g) && before.Values.All(b => b.Id != g)) { o._of[s.Index] = g; o.matched++; continue; }
            if (before.TryGetValue(s.Index, out var mine) && m.Get(mine.Id) is { } current)
            {
                o._of[s.Index] = current.Id;
                o.matched++;
                var on = On(m, current.Id);
                var others = on.Any(e => !Ours(e, key));
                var attrs = current.CloneAttrs();
                var name = current.Name;
                var changed = false;
                if (!string.Equals(current.Name, s.Name, StringComparison.Ordinal) && !others) { renamedNames.Add($"„{current.Name}” → „{s.Name}”"); name = s.Name; o.renamed++; changed = true; }
                if (Math.Abs(current.Num("elevation") - z) > 1e-4)
                {
                    if (others || on.Any(e => Ours(e, key) && !StoredBodies.Has(e))) held.Add(current.Name);
                    else { attrs["elevation"] = z; o.moved++; changed = true; }
                }
                if (Math.Abs(current.Num("height", 3) - s.Height) > 1e-4 && !others) { attrs["height"] = s.Height; changed = true; }
                if (changed) edit.Replace(current.With(name: name, attrs: attrs));
                continue;
            }
            if (storeys.Where(x => Math.Abs(x.Elevation - z) <= ImportStoreys.Tolerance)
                    .OrderByDescending(x => string.Equals(x.Name, s.Name, StringComparison.OrdinalIgnoreCase)).ThenBy(x => Math.Abs(x.Elevation - z)).Select(x => x.Id).FirstOrDefault() is { } near)
            { o._of[s.Index] = near; o.matched++; continue; }
            if (storeys.FirstOrDefault(x => string.Equals(x.Name, s.Name, StringComparison.OrdinalIgnoreCase)).Id is { } byName) { o._of[s.Index] = byName; o.matched++; continue; }
            if (!create) continue;
            var id = s.IfcId is { } sg && GlobalId.IsValid(sg) && m.Get(sg) == null ? sg : ArchicadForeign.Derived(key, $"story\n{s.Index}");
            for (var n = 2; m.Get(id) != null; n++) id = ArchicadForeign.Derived(key, $"story\n{s.Index}\n{n}");
            var storey = Spatial.AddStorey(edit, Spatial.EnsureBuilding(edit), id, s.Name, z, s.Height);
            var sa = storey.CloneAttrs();
            sa[Key] = new JsonObject { ["key"] = key, ["index"] = s.Index };
            edit.Replace(storey.With(attrs: sa));
            o._of[s.Index] = id;
            o.made++;
        }
        if (renamedNames.Count > 0) notes.Add($"Storeys renamed as their stories now are: {string.Join(", ", renamedNames)}.");
        if (held.Count > 0) notes.Add($"Not moved to their stories' new levels, as other or native elements stand on them: {string.Join(", ", held.Select(x => $"„{x}”"))}.");
        var unmatched = a.Stories.Where(s => !o._of.ContainsKey(s.Index)).Select(s => $"„{s.Name}”").ToList();
        if (unmatched.Count > 0) notes.Add($"No storey of the project is at the level of {string.Join(", ", unmatched)}: its elements went on the storey under them.");
        return o;
    }

    /// <summary>After the elements came in: the storeys this model made whose stories are gone removed when nothing stands on them, kept otherwise.</summary>
    public void Finish(IEdit edit)
    {
        var m = edit.Model;
        var indices = _model.Stories.Select(s => s.Index).ToHashSet();
        foreach (var storey in Made(m, _key).Where(s => !indices.Contains((int)(Json.Number((s.Attrs[Key] as JsonObject)?["index"]) ?? int.MinValue))))
        {
            if (!m.Relations.Any(x => x.Relating == storey.Id) && !m.Entities.Any(e => e.Str("storeyId") == storey.Id)) { edit.Remove(storey.Id); removed++; }
            else { kept++; _notes.Add($"Storey „{storey.Name}” kept, as elements stand on it, though Archicad no longer has its story."); }
        }
    }
}
