using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Speckle;

/// <summary>Where a received version came from: the server, project, model (its name) and version, and the application that sent it.</summary>
public sealed record SpeckleSource(string Server, string ProjectId, string ModelId, string? ModelName, string VersionId, string? SourceApplication = null, string? Message = null);

/// <summary>
/// How a version comes in: as a copy with ids of its own (<paramref name="AsCopy"/>: a version sent from this project, beside
/// its elements); its coordinates from the project's base point (<c>base</c>, as Send writes them) or its internal origin;
/// every object on one storey (<paramref name="StoreyId"/>) instead of by its level; <paramref name="ActiveStoreyId"/> the storey
/// the interface is on, where an object goes that neither its level nor its height places; <paramref name="Recognize"/>: its walls
/// and slabs made native where they pass the fidelity gate (<see cref="SpeckleRecognition"/>; the dialog asks for it by default).
/// </summary>
/// <paramref name="CreateStoreys"/>: a level no storey matches made a storey (the default; <c>levels: "existing"</c> keeps to the project's storeys, as S2 did).
public sealed record SpeckleReceiveOptions(bool AsCopy = false, string Origin = "base", string? StoreyId = null, string? ActiveStoreyId = null, bool Recognize = false,
    bool CreateStoreys = true)
{
    public static SpeckleReceiveOptions From(JsonObject a)
    {
        var origin = (string?)a["origin"] ?? "base";
        if (origin is not ("base" or "internal")) throw new CommandException($"„{origin}” is not base or internal.");
        return new SpeckleReceiveOptions(a["asCopy"] is JsonValue v && v.TryGetValue<bool>(out var b) && b, origin, (string?)a["storeyId"] is { Length: > 0 } s ? s : null,
            (string?)a["activeStoreyId"] is { Length: > 0 } act ? act : null, a["recognize"] is JsonValue rv && rv.TryGetValue<bool>(out var rb) && rb,
            ((string?)a["levels"] ?? "create") switch
            {
                "create" => true,
                "existing" => false,
                var other => throw new CommandException($"„{other}” is not create or existing."),
            });
    }
}

/// <summary>
/// What a receive did: the objects added, updated, left as they were and removed, the shared bodies, where they went, and what
/// was left out; what recognition made native (<see cref="Recognition"/>, null when not asked), and how many of the model's
/// objects are now native elements (<see cref="Native"/>) and how many references (<see cref="References"/>).
/// </summary>
public sealed record SpeckleReceiveResult(int Added, int Updated, int Unchanged, int Removed, int Shared, int Triangles, int Lines, IReadOnlyDictionary<string, int> Skipped,
    IReadOnlyDictionary<string, int> Storeys, IReadOnlyList<string> Notes, string? GroupId, SpeckleRecognitionReport? Recognition = null, int Native = 0, int References = 0,
    SpeckleLevelReport? Levels = null)
{
    public JsonObject ToJson()
    {
        var skipped = new JsonObject();
        foreach (var (k, v) in Skipped.OrderByDescending(kv => kv.Value)) skipped[k] = v;
        var storeys = new JsonObject();
        foreach (var (k, v) in Storeys) storeys[k] = v;
        return new JsonObject
        {
            ["elements"] = Added + Updated + Unchanged, ["added"] = Added, ["updated"] = Updated, ["unchanged"] = Unchanged, ["removed"] = Removed, ["shared"] = Shared,
            ["triangles"] = Triangles, ["lines"] = Lines, ["skipped"] = skipped, ["storeys"] = storeys, ["groupId"] = GroupId,
            ["recognized"] = Recognition?.Recognized ?? 0, ["native"] = Native, ["references"] = References, ["recognition"] = Recognition?.ToJson(), ["levels"] = Levels?.ToJson(),
            ["notes"] = new JsonArray(Notes.Select(n => (JsonNode)n).ToArray()),
        };
    }
}

/// <summary>
/// A version read (<see cref="SpeckleRead"/>) into the open project as references (docs/plan-speckle.md, S2), in one undo step:
/// each object an element drawn from its stored body (<see cref="StoredBodies"/>, as an imported mesh, Fragments or IFC
/// reference is), in the IFC class it names (else a proxy); instances standing as instances of one shared body per
/// definition; curves as plan lines. Each keeps its properties as sets (<c>Speckle</c>, <c>Speckle · …</c>), and in its
/// <c>speckle</c> attribute the server, project, model and version it came from, its applicationId, its Speckle id and type,
/// and its collections. They go on their level's storey (<see cref="SpeckleLevels"/>: by the GlobalId Lapin sent it with, the
/// storey an earlier receive made for it, its elevation, 5 cm, or its name; a level none matches made a storey unless asked
/// to keep to the project's), else the one their lowest point stands on, else the one asked for; the version's coordinates are read from the
/// project's base point (as Send writes them) unless asked otherwise. The first receive of a model makes one group named
/// after it; what a later one adds joins it.
/// <para>
/// Received again (a newer version of the same model), what came before is updated in place, matched by applicationId (else
/// by Speckle's id): it keeps its GlobalId, group and anything else that names it; an object whose Speckle id did not change
/// is left as it is. What the new version no longer has is removed, as a link's reload removes what its file no longer holds:
/// the references mirror one Speckle model. A new object takes its applicationId as its GlobalId when that is one and free
/// (a model Lapin or an IFC connector sent), else an id derived from the model and its applicationId, so the same object
/// gets the same id every time.
/// </para>
/// <para>
/// A version whose objects are this project's own elements (sent from it: their applicationIds are its GlobalIds) is refused
/// unless asked for as a copy, which takes ids of its own (derived) and never touches the native elements.
/// </para>
/// <para>
/// Asked to (<see cref="SpeckleReceiveOptions.Recognize"/>), the walls and slabs then become native where they really are
/// (<see cref="SpeckleRecognition"/>, S3), keeping their GlobalIds and their <c>speckle</c> attribute. Received again, a
/// recognised element whose object did not change is left as it is; one whose object changed comes in again as a reference
/// (same GlobalId) and is offered again, native once more if it passes, a reference if it no longer does; one whose object is
/// gone is removed. What is removed goes before the rest comes in, so a native wall's openings never take a newer object with them.
/// </para>
/// </summary>
public static class SpeckleReceive
{
    public const string Key = "speckle";

    /// <summary>The key a model's references are known by: its server, project and model (and whether they are a copy).</summary>
    public static string ModelKey(SpeckleSource s, bool asCopy) => $"{s.Server}|{s.ProjectId}|{s.ModelId}{(asCopy ? "|copy" : "")}";

    /// <summary>The references (and shared bodies) a model's earlier receives made.</summary>
    public static List<Entity> Existing(IModelView m, string key) =>
        m.Entities.Where(e => (string?)(e.Attrs[Key] as JsonObject)?["key"] == key).ToList();

    /// <summary>The project's own elements a version's objects name by their applicationIds (it was sent from this project).</summary>
    public static int Native(IModelView m, SpeckleReceived r, string key) =>
        r.Elements.Select(e => e.ApplicationId).OfType<string>().Distinct().Count(id => m.Get(id) is { } e && IsNative(e, key));

    /// <summary>An entity a receive must never write over: anything that is not a reference this model's receives made.</summary>
    static bool IsNative(Entity e, string key) => (string?)(e.Attrs[Key] as JsonObject)?["key"] != key && !StoredBodies.Has(e) && e.Class != StoredBodies.SharedClass;

    /// <summary>What a receive would do, said before it is done (the dialog's summary): no edit.</summary>
    public static JsonObject Survey(IModelView m, SpeckleReceived r, SpeckleSource s, bool asCopy)
    {
        var key = ModelKey(s, asCopy);
        var existing = Existing(m, key).Where(e => e.Class != StoredBodies.SharedClass).ToList();
        var apps = existing.Select(e => (string?)(e.Attrs[Key] as JsonObject)?["applicationId"]).OfType<string>().ToHashSet();
        var objs = existing.Select(e => (string?)(e.Attrs[Key] as JsonObject)?["objectId"]).OfType<string>().ToHashSet();
        var matched = r.Elements.Count(e => e.ApplicationId is { } a && apps.Contains(a) || e.ObjectId is { } o && objs.Contains(o));
        var native = Native(m, r, key);
        var skipped = new JsonObject();
        foreach (var (k, v) in r.Skipped.OrderByDescending(kv => kv.Value)) skipped[k] = v;
        var levels = r.Elements.Select(e => e.Level?.Name).OfType<string>().Distinct().ToList();
        return new JsonObject
        {
            ["versionId"] = s.VersionId, ["model"] = s.ModelName, ["message"] = s.Message, ["sourceApplication"] = s.SourceApplication,
            ["elements"] = r.Elements.Count, ["instances"] = r.Elements.Count(e => e.Instance != null), ["definitions"] = r.Definitions.Count,
            ["lines"] = r.Elements.Count(e => e.Lines != null), ["triangles"] = r.Triangles, ["skipped"] = skipped,
            ["levels"] = new JsonArray(levels.Select(l => (JsonNode)l).ToArray()),
            ["existing"] = existing.Count, ["updates"] = matched, ["adds"] = r.Elements.Count - matched, ["removes"] = Math.Max(0, existing.Count - matched),
            ["native"] = native,
            // Sent from this project: its root is this project, or its objects are this project's elements.
            ["fromThisProject"] = native > 0 || r.RootApplicationId != null && Spatial.Project(m)?.Id == r.RootApplicationId,
            ["notes"] = new JsonArray(r.Notes.Select(n => (JsonNode)n).ToArray()),
        };
    }

    public static SpeckleReceiveResult Apply(IEdit edit, SpeckleReceived r, SpeckleSource s, SpeckleReceiveOptions o, ProductManifest? product = null,
        Action<string, int, int>? progress = null, CancellationToken ct = default, IServices? services = null)
    {
        var m = edit.Model;
        var key = ModelKey(s, o.AsCopy);
        var native = Native(m, r, key);
        if (native > 0 && !o.AsCopy)
            throw new CommandException($"This version was sent from this project: {native} of its objects are this project's own elements. Receive it as a copy (with ids of its own) to compare, or receive it in another project.");
        var notes = new List<string>(r.Notes);
        var skipped = new Dictionary<string, int>(r.Skipped);

        // Where the version's coordinates start: the base point, as Send writes them, unless asked otherwise.
        var basePoint = Location.Base(m);
        var shift = o.Origin == "internal" ? Vec3.Zero : new Vec3(basePoint.At.X, basePoint.At.Y, basePoint.Elevation);
        // Where the version's origin stood in the project when it came in: an object is the same as before only if this is too.
        var At = $"{Math.Round(shift.X, 4)},{Math.Round(shift.Y, 4)},{Math.Round(shift.Z, 4)}";

        // What this model's earlier receives made, by applicationId and by Speckle id.
        var existing = Existing(m, key);
        var byApp = new Dictionary<string, Entity>();
        var byObject = new Dictionary<string, Entity>();
        var sharedBefore = new Dictionary<string, Entity>();
        foreach (var e in existing)
        {
            var sp = (JsonObject)e.Attrs[Key]!;
            if (e.Class == StoredBodies.SharedClass) { if ((string?)sp["definition"] is { } d) sharedBefore[d] = e; continue; }
            if ((string?)sp["applicationId"] is { } a) byApp.TryAdd(a, e);
            if ((string?)sp["objectId"] is { } ob) byObject.TryAdd(ob, e);
        }
        var group = existing.Select(e => Groups.Of(m, e.Id)).FirstOrDefault(g => g != null);

        string Derived(string what)
        {
            var bytes = Md5.HashData(Encoding.UTF8.GetBytes(key + "\n" + what));
            return GlobalId.FromGuid(new Guid(bytes));
        }

        JsonObject Meta(string? applicationId, string? objectId, string? type, string path) => new()
        {
            ["key"] = key, ["server"] = s.Server, ["projectId"] = s.ProjectId, ["modelId"] = s.ModelId, ["model"] = s.ModelName, ["versionId"] = s.VersionId,
            ["applicationId"] = applicationId, ["objectId"] = objectId, ["type"] = type, ["path"] = path, ["copy"] = o.AsCopy ? true : null,
            ["at"] = At,
        };

        // Translucent colours take a material of their own (glass), so they draw, render and export see-through; one per colour and opacity.
        var glass = new Dictionary<string, string>();
        JsonObject Body(IEnumerable<ReceivedPart> parts, Vec3 by)
        {
            var list = parts.ToList();
            var body = StoredBodies.Write(list.Select(p => (p.Material ?? "Body", p.Color, (IReadOnlyList<Vec3>)(by == Vec3.Zero ? p.Vertices : p.Vertices.Select(v => v + by).ToList()),
                (IReadOnlyList<(int, int, int)>)p.Triangles)));
            if (body["parts"] is not JsonArray written) return body;
            for (var i = 0; i < list.Count && i < written.Count; i++)
            {
                var p = list[i];
                if (p.Opacity >= 0.999 || written[i] is not JsonObject part) continue;
                var k = p.Color + Math.Round(p.Opacity, 2);
                if (!glass.TryGetValue(k, out var materialId))
                {
                    var name = $"Speckle {(p.Material is { Length: > 0 } mn ? mn : p.Color.TrimStart('#').ToUpperInvariant())} {Math.Round(100 * p.Opacity)}%";
                    var material = Materials.Ensure(edit, name, p.Color);
                    var a = material.CloneAttrs();
                    a["transparency"] = Math.Round(1 - p.Opacity, 3);
                    edit.Replace(material.With(attrs: a));
                    glass[k] = materialId = material.Id;
                }
                part["material"] = materialId;
            }
            return body;
        }

        void Put(Entity e) { if (m.Get(e.Id) != null) edit.Replace(e); else edit.Add(e); }

        // The definitions instances share: one shared body each, in its own frame, under an id derived from the model and the definition.
        var used = r.Elements.Where(e => e.Instance != null).Select(e => e.Instance!.Value.Definition).ToHashSet();
        var sharedIds = new Dictionary<string, string>();
        var shared = 0;
        foreach (var def in r.Definitions.Values.Where(d => used.Contains(d.Id)))
        {
            if (def.Parts.Count == 0) continue;
            var id = sharedBefore.TryGetValue(def.Id, out var before) ? before.Id : Derived("definition\n" + def.Id);
            var meta = Meta(null, null, "InstanceDefinitionProxy", "");
            meta["definition"] = def.Id;
            Put(new Entity(id, StoredBodies.SharedClass, $"{def.Name} (shared)", new JsonObject { [StoredBodies.Key] = Body(def.Parts, Vec3.Zero), [Key] = meta, ["source"] = SourceText(s) }));
            sharedIds[def.Id] = id;
            shared++;
        }

        // The storeys, and where each object goes.
        var storeys = Spatial.Storeys(m).Select(x => (x.Id, x.Name, Elevation: x.Num("elevation"))).OrderBy(x => x.Elevation).ToList();
        var asked = o.StoreyId is { } sid ? m.Get(sid) is { Class: IfcClass.BuildingStorey } given ? given.Id : throw new CommandException("That storey is not in the project.") : null;
        string? fallback = null;
        string Fallback() => fallback ??= o.ActiveStoreyId is { } act && m.Get(act) is { Class: IfcClass.BuildingStorey } active ? active.Id : Spatial.EnsureStorey(edit, product).Id;
        string? StoreyAt(double z) => storeys.LastOrDefault(x => x.Elevation <= z + ImportStoreys.Tolerance).Id ?? (storeys.Count > 0 ? storeys[0].Id : null);
        var unmatchedLevels = new HashSet<string>();
        SpeckleLevels? levels = null;
        string StoreyFor(ReceivedLevel? level, double? lowest)
        {
            if (asked != null) return asked;
            if (level != null)
            {
                if (levels?.StoreyOf(level) is { } byLevel) return byLevel;
                if (level.GlobalId is { } g && storeys.Any(x => x.Id == g)) return g;
                if (level.Elevation is { } el)
                {
                    var z = el + shift.Z + (levels?.Report.Datum ?? 0);
                    var near = storeys.Where(x => Math.Abs(x.Elevation - z) <= ImportStoreys.Tolerance).OrderByDescending(x => string.Equals(x.Name, level.Name, StringComparison.OrdinalIgnoreCase)).ThenBy(x => Math.Abs(x.Elevation - z)).Select(x => x.Id).FirstOrDefault();
                    if (near != null) return near;
                    unmatchedLevels.Add(level.Name);
                    if (StoreyAt(z) is { } at) return at;
                }
                else if (storeys.FirstOrDefault(x => string.Equals(x.Name, level.Name, StringComparison.OrdinalIgnoreCase)).Id is { } byName) return byName;
                else unmatchedLevels.Add(level.Name);
            }
            if (lowest is { } low && StoreyAt(low) is { } st) return st;
            return Fallback();
        }

        // What each object updates (this model's earlier one with its applicationId, else its Speckle id), found first: what is gone
        // from the version, and a recognised element whose object changed, go before anything comes in (a native wall takes its
        // openings with it, and what a removal takes is never what this receive just wrote).
        var total = r.Elements.Count;
        var befores = new Entity?[total];
        var matched = new HashSet<string>();
        for (var i = 0; i < total; i++)
        {
            var el = r.Elements[i];
            var b = el.ApplicationId is { } aid && byApp.TryGetValue(aid, out var x1) && !matched.Contains(x1.Id) ? x1
                : el.ObjectId is { } oid && byObject.TryGetValue(oid, out var x2) && !matched.Contains(x2.Id) ? x2 : null;
            if (b != null) matched.Add(b.Id);
            befores[i] = b;
        }
        bool Same(Entity before, ReceivedElement el) => before.Attrs[Key] is JsonObject was && (string?)was["objectId"] == el.ObjectId && el.ObjectId != null && (string?)was["at"] == At
            && before.Class == el.IfcClass;
        var removed = 0;
        foreach (var e in existing.Where(e => e.Class != StoredBodies.SharedClass && !matched.Contains(e.Id)))
            if (m.Get(e.Id) != null) { edit.Remove(e.Id); removed++; }
        // A recognised element whose object changed: it comes in again as a reference, in its group, and is offered again.
        var rejoin = new List<string>();
        for (var i = 0; i < total; i++)
            if (befores[i] is { } b && !StoredBodies.Has(b) && m.Get(b.Id) != null && !Same(b, r.Elements[i]))
            {
                if (Groups.Of(m, b.Id) != null) rejoin.Add(b.Id);
                edit.Remove(b.Id);
            }

        // The version's levels as storeys: matched, else made (the default); after what goes went, so a storey is moved or renamed
        // only while nothing else stands on it.
        if (asked == null)
        {
            levels = SpeckleLevels.Resolve(edit, r, key, shift.Z, o.CreateStoreys, notes);
            storeys = Spatial.Storeys(m).Select(x => (x.Id, x.Name, Elevation: x.Num("elevation"))).OrderBy(x => x.Elevation).ToList();
        }

        var seen = new HashSet<string>();
        var taken = new HashSet<string>();
        var offered = new List<(string Id, ReceivedElement El)>();
        var joins = new Dictionary<string, List<string>>();
        var made = new List<string>();
        var perStorey = new Dictionary<string, int>();
        int added = 0, updated = 0, unchanged = 0, lines = 0, triangles = 0;
        for (var i = 0; i < total; i++)
        {
            if (i % 200 == 0)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Invoke("Bringing the objects in", i, total);
            }
            var el = r.Elements[i];
            // The one it updates: this model's earlier object with its applicationId, else with its Speckle id.
            var before = befores[i];
            string id;
            if (before != null) id = before.Id;
            else if (!o.AsCopy && el.ApplicationId is { } own && GlobalId.IsValid(own) && m.Get(own) == null && !taken.Contains(own)) id = own;
            else
            {
                id = Derived("object\n" + (el.ApplicationId ?? "id:" + el.ObjectId));
                for (var n = 2; taken.Contains(id) || m.Get(id) is { } other && (other.Attrs[Key] as JsonObject)?["key"]?.GetValue<string>() != key; n++)
                    id = Derived($"object\n{el.ApplicationId ?? "id:" + el.ObjectId}\n{n}");
            }
            taken.Add(id);
            seen.Add(id);

            var attrs = new JsonObject();
            double? lowest = null;
            if (el.Instance is var (def, xf0))
            {
                if (!sharedIds.TryGetValue(def, out var bodyId)) { skipped["InstanceProxy (empty definition)"] = skipped.GetValueOrDefault("InstanceProxy (empty definition)") + 1; seen.Remove(id); continue; }
                var xf = (double[])xf0.Clone();
                xf[9] += shift.X; xf[10] += shift.Y; xf[11] += shift.Z;
                attrs[StoredBodies.BodyOf] = bodyId;
                attrs[StoredBodies.XfKey] = StoredBodies.WriteXf(xf);
                var d = r.Definitions[def];
                lowest = d.Parts.SelectMany(p => p.Vertices).Select(v => StoredBodies.Apply(xf, v).Z).DefaultIfEmpty(0).Min();
                triangles += d.Parts.Sum(p => p.Triangles.Count);
            }
            else if (el.Parts.Count > 0)
            {
                attrs[StoredBodies.Key] = Body(el.Parts, shift);
                lowest = el.Parts.SelectMany(p => p.Vertices).Min(v => v.Z) + shift.Z;
                triangles += el.Parts.Sum(p => p.Triangles.Count);
            }
            else if (el.Lines is { Count: > 0 } ls)
            {
                // Curves only: drawn in plan as the object's own lines (as an IFC element's 2D representation), no body.
                var moved = ls.Select(l => l with { Loops = l.Loops.Select(loop => (IReadOnlyList<Vec2>)loop.Select(p => p + shift.Xy).ToList()).ToList() }).ToList();
                attrs[StoredBodies.Key] = StoredBodies.Write([]);
                attrs[Plan2d.Key] = Plan2d.Write(moved);
                lines++;
            }
            else { seen.Remove(id); continue; }
            attrs["properties"] = el.Properties.DeepClone();
            attrs[Key] = Meta(el.ApplicationId, el.ObjectId, el.SpeckleType, el.Path);
            attrs["source"] = SourceText(s);

            var storey = StoreyFor(el.Level, lowest);
            var storeyName = storeys.FirstOrDefault(x => x.Id == storey).Name ?? m.Get(storey)?.Name ?? "";
            perStorey[storeyName] = perStorey.GetValueOrDefault(storeyName) + 1;

            if (before != null && m.Get(id) is { } still && Same(still, el))
            {
                // The same object as before (Speckle's id is a hash of it): left as it is, unless it moves storey; a reference
                // recognition never offered (received without it) offered now.
                unchanged++;
                if (StoredBodies.Has(still) && (still.Attrs[Key] as JsonObject)?["recognition"] == null) offered.Add((id, el));
            }
            else
            {
                Put(new Entity(id, el.IfcClass, el.Name, attrs));
                if (before != null) updated++; else { added++; made.Add(id); }
                offered.Add((id, el));
            }
            if (Spatial.StoreyOf(m, id)?.Id != storey) (joins.TryGetValue(storey, out var js) ? js : joins[storey] = []).Add(id);
        }
        foreach (var (storeyId, ids) in joins) edit.RelateAll(IfcClass.RelContainedInSpatialStructure, storeyId, ids);

        // What came in but was not brought (no geometry, an empty definition) goes too; then any shared body no instance stands on.
        foreach (var e in existing.Where(e => e.Class != StoredBodies.SharedClass && !seen.Contains(e.Id)))
            if (m.Get(e.Id) != null) { edit.Remove(e.Id); removed++; }
        var standing = m.Entities.Select(e => (string?)e.Attrs[StoredBodies.BodyOf]).OfType<string>().ToHashSet();
        foreach (var e in existing.Where(e => e.Class == StoredBodies.SharedClass && !sharedIds.ContainsValue(e.Id) && !standing.Contains(e.Id)))
            if (m.Get(e.Id) != null) edit.Remove(e.Id);

        // One group for the model: the first receive makes it, a later one adds to it.
        string? groupId = group != null && m.Get(group.Id) != null ? group.Id : null;
        if (made.Count > 0)
        {
            if (groupId != null) edit.RelateAll(Groups.Rel, groupId, made);
            else groupId = Groups.Make(edit, s.ModelName is { Length: > 0 } gn ? gn : "Speckle", made)?.Id;
        }
        if (groupId != null && rejoin.Where(id => m.Get(id) != null).ToList() is { Count: > 0 } back) edit.RelateAll(Groups.Rel, groupId, back);
        if (groupId != null) Groups.DropEmpty(edit, [groupId]);

        // The walls and slabs made native where they pass the gate: in their storeys and group already, which they keep.
        SpeckleRecognitionReport? recognition = null;
        if (o.Recognize && services != null)
        {
            progress?.Invoke("Recognising walls and slabs", 0, 0);
            recognition = SpeckleRecognition.Run(edit, services, offered, shift);
        }
        // The storeys of levels the version no longer has, removed when nothing stands on them; those made given their heights.
        levels?.Finish(edit, r, key);
        var now = Existing(m, key).Where(e => e.Class != StoredBodies.SharedClass).ToList();
        var nativeNow = now.Count(e => !StoredBodies.Has(e));

        if (unmatchedLevels.Count > 0)
            notes.Add($"No storey of the project is at the level of {string.Join(", ", unmatchedLevels.Order().Select(l => $"„{l}”"))}: its objects went on the storey below them.");
        if (r.Definitions.Count > 0 && shared > 0) notes.Add($"{shared} definition(s) shared by their instances.");
        return new SpeckleReceiveResult(added, updated, unchanged, removed, shared, triangles, lines, skipped, perStorey, notes, groupId, recognition, nativeNow, now.Count - nativeNow, levels?.Report);
    }

    static string SourceText(SpeckleSource s) => $"Speckle: {s.ModelName ?? s.ModelId} ({s.Server})";
}
