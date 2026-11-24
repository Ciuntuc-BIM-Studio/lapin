using System.Text;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Speckle;

/// <summary>What a receive did to the project's storeys: matched to the version's levels, made, renamed, moved, removed, and kept although their level is gone.</summary>
public sealed record SpeckleLevelReport(int Matched, int Made, int Renamed, int Moved, int Removed, int Kept, double Datum)
{
    public JsonObject ToJson() => new()
    {
        ["matched"] = Matched, ["made"] = Made, ["renamed"] = Renamed, ["moved"] = Moved, ["removed"] = Removed, ["kept"] = Kept, ["datum"] = Math.Round(Datum, 4),
    };
}

/// <summary>
/// The version's levels as the project's storeys (docs/plan-speckle.md, the storeys stage): each level found among the storeys
/// by the GlobalId Lapin sent it with, else as the storey an earlier receive of the model made for it (by the level's
/// applicationId, else its name), else by its elevation (5 cm, the same name first), else by its name; a level none takes is
/// made a storey, with its name and elevation, when asked to (the default), under an id derived from the model and the level.
/// Elevations are the version's own, read from the dialog's origin (the base point's elevation added, as the geometry is) and
/// from the geometry's datum: when the objects of most levels stand a same height off their levels' elevations (Revit's levels
/// given from the survey point, the geometry from the internal origin), the levels are moved by it, so a storey stands where its
/// objects do.
/// <para>
/// Received again, only a storey this model's receives made is touched, and only while nothing else stands on it: renamed as
/// its level now is; moved to its level's new elevation unless a native element of the model stands on it too (a recognised
/// wall's base is the storey's). One whose level is gone is removed when nothing stands on it, kept otherwise. A storey the
/// project had is never renamed, moved or removed.
/// </para>
/// </summary>
public sealed class SpeckleLevels
{
    /// <summary>The attribute a storey a receive made carries: the model's key and the level's.</summary>
    public const string Key = "speckleLevel";

    readonly Dictionary<string, string> storeyOf = [];
    readonly List<string> notes;
    int matched, made, renamed, moved, removed, kept;
    double datum;

    SpeckleLevels(List<string> notes) => this.notes = notes;

    /// <summary>The storey a level went on, or null (none matched, and none was made).</summary>
    public string? StoreyOf(ReceivedLevel level) => storeyOf.GetValueOrDefault(level.Key);

    public SpeckleLevelReport Report => new(matched, made, renamed, moved, removed, kept, datum);

    /// <summary>The storeys this model's receives made (under <paramref name="key"/>).</summary>
    public static List<Entity> Made(IModelView m, string key) =>
        Spatial.Storeys(m).Where(e => (string?)(e.Attrs[Key] as JsonObject)?["key"] == key).ToList();

    static string? LevelKeyOf(Entity storey) => (string?)(storey.Attrs[Key] as JsonObject)?["level"];

    /// <summary>What stands on a storey: the elements it contains.</summary>
    static List<Entity> On(IModelView m, string storeyId) =>
        m.Relations.Where(r => r.Class == IfcClass.RelContainedInSpatialStructure && r.Relating == storeyId).SelectMany(r => r.Related).Select(m.Get).OfType<Entity>().ToList();

    static bool Ours(Entity e, string key) => (string?)(e.Attrs[SpeckleReceive.Key] as JsonObject)?["key"] == key;

    /// <summary>
    /// The height most of the version's objects stand off their levels' elevations (metres), when it is one and more than 5 cm:
    /// the levels' datum and the geometry's differ by it. 0 otherwise.
    /// </summary>
    public static double Datum(SpeckleReceived r)
    {
        var offsets = r.Elements.Where(e => e.Level?.Elevation != null && e.Parts.Count > 0)
            .Select(e => Math.Round(e.Parts.SelectMany(p => p.Vertices).Min(v => v.Z) - e.Level!.Elevation!.Value, 2)).ToList();
        if (offsets.Count < 3) return 0;
        var mode = offsets.GroupBy(x => x).OrderByDescending(g => g.Count()).First();
        return mode.Count() * 2 > offsets.Count && Math.Abs(mode.Key) > ImportStoreys.Tolerance ? mode.Key : 0;
    }

    /// <param name="create">Whether a level no storey matches is made one (else its objects go on the storey below them, as S2 did).</param>
    /// <param name="shiftZ">The elevation the version's origin stands at in the project (the base point's, or 0 from the internal origin).</param>
    public static SpeckleLevels Resolve(IEdit edit, SpeckleReceived r, string key, double shiftZ, bool create, List<string> notes)
    {
        var m = edit.Model;
        var o = new SpeckleLevels(notes);
        o.datum = Datum(r);
        if (o.datum != 0)
            notes.Add($"The objects stand {Math.Round(o.datum * 1000)} mm off their levels' elevations (the levels given from another datum than the geometry): the storeys are placed by the objects.");
        var before = Made(m, key).ToDictionary(e => LevelKeyOf(e) ?? "", e => e);
        string Derived(string what) => GlobalId.FromGuid(new Guid(Md5.HashData(Encoding.UTF8.GetBytes(key + "\nlevel\n" + what))));
        List<(string Id, string Name, double Elevation)> Storeys() => Spatial.Storeys(m).Select(x => (x.Id, x.Name, Elevation: x.Num("elevation"))).ToList();
        var renamedNames = new List<string>();
        var keptNames = new List<string>();
        var heldPlace = new List<string>();

        foreach (var level in r.AllLevels.OrderBy(l => l.Elevation ?? double.MaxValue))
        {
            double? z = level.Elevation is { } el ? Math.Round(el + o.datum + shiftZ, 6) : null;
            var storeys = Storeys();
            // Lapin's own: the storey it was sent from.
            if (level.GlobalId is { } g && storeys.Any(x => x.Id == g)) { o.storeyOf[level.Key] = g; o.matched++; continue; }
            // Made by an earlier receive of this model for this level: renamed and moved with it while nothing else stands on it.
            if (before.TryGetValue(level.Key, out var mine) && m.Get(mine.Id) is { } current)
            {
                o.storeyOf[level.Key] = current.Id;
                o.matched++;
                var on = On(m, current.Id);
                var others = on.Any(e => !Ours(e, key));
                var attrs = current.CloneAttrs();
                var name = current.Name;
                var changed = false;
                if (!string.Equals(current.Name, level.Name, StringComparison.Ordinal))
                {
                    if (others) keptNames.Add(current.Name);
                    else { renamedNames.Add($"„{current.Name}” → „{level.Name}”"); name = level.Name; o.renamed++; changed = true; }
                }
                if (z is { } to && Math.Abs(current.Num("elevation") - to) > 1e-4)
                {
                    // A native element of the model (a recognised wall) stands on its storey's elevation: it would move with it.
                    if (others || on.Any(e => Ours(e, key) && !StoredBodies.Has(e))) heldPlace.Add(current.Name);
                    else { attrs["elevation"] = to; o.moved++; changed = true; }
                }
                if (changed) edit.Replace(current.With(name: name, attrs: attrs));
                continue;
            }
            if (z is { } at && storeys.Where(x => Math.Abs(x.Elevation - at) <= ImportStoreys.Tolerance)
                    .OrderByDescending(x => string.Equals(x.Name, level.Name, StringComparison.OrdinalIgnoreCase)).ThenBy(x => Math.Abs(x.Elevation - at)).Select(x => x.Id).FirstOrDefault() is { } near)
            {
                o.storeyOf[level.Key] = near; o.matched++; continue;
            }
            if (storeys.FirstOrDefault(x => string.Equals(x.Name, level.Name, StringComparison.OrdinalIgnoreCase)).Id is { } byName) { o.storeyOf[level.Key] = byName; o.matched++; continue; }
            if (!create || z == null) continue;
            var id = level.GlobalId is { } lg && GlobalId.IsValid(lg) && m.Get(lg) == null ? lg : Derived(level.Key);
            for (var n = 2; m.Get(id) != null; n++) id = Derived($"{level.Key}\n{n}");
            var building = Spatial.EnsureBuilding(edit);
            var storey = Spatial.AddStorey(edit, building, id, level.Name, z.Value, 3.0);
            var a = storey.CloneAttrs();
            a[Key] = new JsonObject { ["key"] = key, ["level"] = level.Key };
            edit.Replace(storey.With(attrs: a));
            o.storeyOf[level.Key] = id;
            o.made++;
        }
        // Their heights before anything stands on them: a recognised beam's top is read from its storey's top.
        Heights(edit, key, _ => true);
        if (renamedNames.Count > 0) notes.Add($"Storeys renamed as their levels now are: {string.Join(", ", renamedNames)}.");
        if (keptNames.Count > 0) notes.Add($"Not renamed, as other elements stand on them: {string.Join(", ", keptNames.Select(x => $"„{x}”"))}.");
        if (heldPlace.Count > 0) notes.Add($"Not moved to their levels' new elevations, as other or native elements stand on them: {string.Join(", ", heldPlace.Select(x => $"„{x}”"))}.");
        return o;
    }

    /// <summary>
    /// After the objects came in: the storeys this model made whose levels the version no longer has removed when nothing stands on
    /// them (kept otherwise), and those it made given their heights, up to the storey over them.
    /// </summary>
    public void Finish(IEdit edit, SpeckleReceived r, string key)
    {
        var m = edit.Model;
        var levels = r.AllLevels.Select(l => l.Key).ToHashSet();
        var keptNames = new List<string>();
        foreach (var storey in Made(m, key).Where(s => !levels.Contains(LevelKeyOf(s) ?? "")))
        {
            // Nothing on it: no element, nothing aggregated under it, no view kept for it.
            if (!m.Relations.Any(x => x.Relating == storey.Id) && !m.Entities.Any(e => e.Str("storeyId") == storey.Id)) { edit.Remove(storey.Id); removed++; }
            else { keptNames.Add(storey.Name); kept++; }
        }
        if (keptNames.Count > 0) notes.Add($"Kept, as elements stand on them, though the version no longer has their levels: {string.Join(", ", keptNames.Select(x => $"„{x}”"))}.");
        // A storey's height changed under a native element of the model would move it (a beam's top is its storey's top less its offset).
        Heights(edit, key, s => !On(m, s.Id).Any(e => !StoredBodies.Has(e)));
    }

    /// <summary>The storeys this model made given their heights, up to the storey over them (the top one 3 m), those <paramref name="may"/> lets.</summary>
    static void Heights(IEdit edit, string key, Func<Entity, bool> may)
    {
        var m = edit.Model;
        var all = Spatial.Storeys(m).Select(x => (x.Id, Elevation: x.Num("elevation"))).OrderBy(x => x.Elevation).ToList();
        foreach (var storey in Made(m, key).Where(may))
        {
            var z = storey.Num("elevation");
            var next = all.Where(x => x.Elevation > z + ImportStoreys.Tolerance).Select(x => (double?)x.Elevation).FirstOrDefault();
            var height = next is { } n ? Math.Round(n - z, 6) : 3.0;
            if (Math.Abs(storey.Num("height", 3) - height) < 1e-6) continue;
            var a = storey.CloneAttrs();
            a["height"] = height;
            edit.Replace(storey.With(attrs: a));
        }
    }
}
