using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Archicad;

/// <summary>
/// How a receive goes (docs/plan-archicad.md §3): the missing storeys made (<c>storeys: "create"</c>, the default) or only the
/// project's used; recognition on (the default: Archicad gives parameters, not meshes); the coordinates from Archicad's project
/// origin (the default) or its survey point; as a copy with ids of its own.
/// </summary>
public sealed record ArchicadReceiveOptions(int? Port = null, bool CreateStoreys = true, bool Recognize = true, string Origin = "project", bool AsCopy = false, FidelityOptions? Fidelity = null)
{
    public static ArchicadReceiveOptions From(JsonObject a)
    {
        var origin = (string?)a["origin"] ?? "project";
        if (origin is not ("project" or "survey")) throw new CommandException($"„{origin}” is not project or survey.");
        var storeys = (string?)a["storeys"] ?? "create";
        if (storeys is not ("create" or "existing")) throw new CommandException($"„{storeys}” is not create or existing.");
        bool Flag(string k, bool fallback) => a[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
        return new(Json.Number(a["port"]) is { } p ? (int)p : null, storeys == "create", Flag("recognize", true), origin, Flag("asCopy", false));
    }
}

/// <summary>What recognition made of a receive: how many elements became native (by class), how many were offered, why the rest stayed references, and the gate's report.</summary>
public sealed record ArchicadRecognitionReport(int Recognized, int Offered, IReadOnlyDictionary<string, int> ByClass, IReadOnlyDictionary<string, int> Kept, FidelityReport? Fidelity)
{
    public JsonObject ToJson()
    {
        static JsonObject Counts(IReadOnlyDictionary<string, int> d)
        {
            var o = new JsonObject();
            foreach (var (k, v) in d.OrderByDescending(kv => kv.Value)) o[k] = v;
            return o;
        }
        return new JsonObject
        {
            ["recognized"] = Recognized, ["offered"] = Offered, ["byClass"] = Counts(ByClass), ["kept"] = Counts(Kept), ["reverted"] = Fidelity?.Checks.Count(c => c.Reverted) ?? 0,
            ["fidelity"] = Fidelity?.ToJson(),
        };
    }
}

public sealed record ArchicadReceiveResult(int Added, int Updated, int Unchanged, int Removed, int Native, int References, IReadOnlyDictionary<string, int> Storeys,
    IReadOnlyDictionary<string, int> Skipped, ArchicadRecognitionReport? Recognition, ArchicadStoreyReport? Levels, string? GroupId, IReadOnlyList<string> Notes)
{
    public JsonObject ToJson()
    {
        static JsonObject Counts(IReadOnlyDictionary<string, int> d)
        {
            var o = new JsonObject();
            foreach (var (k, v) in d.OrderByDescending(kv => kv.Value)) o[k] = v;
            return o;
        }
        return new JsonObject
        {
            ["elements"] = Added + Updated + Unchanged, ["added"] = Added, ["updated"] = Updated, ["unchanged"] = Unchanged, ["removed"] = Removed,
            ["native"] = Native, ["references"] = References, ["storeys"] = Counts(Storeys), ["skipped"] = Counts(Skipped),
            ["recognized"] = Recognition?.Recognized ?? 0, ["recognition"] = Recognition?.ToJson(), ["levels"] = Levels?.ToJson(), ["groupId"] = GroupId,
            ["notes"] = new JsonArray([.. Notes.Select(n => (JsonNode)n)]),
        };
    }
}

/// <summary>
/// An Archicad model (<see cref="ArchicadRead"/>) into the open project, in one undo step (docs/plan-archicad.md, A1). Every element
/// with a body comes in first as the reference Archicad's own 3D makes it (<see cref="StoredBodies"/>: its faces triangulated,
/// holes and concave ones too, a part per surface, each surface a Lapin material), with Archicad's IFC id as its GlobalId (so the
/// same as Archicad's IFC export gives it), its properties (Lapin's own set <c>Archicad</c>: guid, ID, type, layer, story; Archicad's
/// custom property groups as sets named as in Archicad; the IFC property sets Archicad maps, by their IFC names), its classifications
/// (<see cref="Classifications"/>, written by the IFC export as IfcClassificationReference) and in its <c>archicad</c> attribute the
/// model it came from, its guid, type, layer and a hash of everything read of it. Then, asked to (the default), its walls, slabs,
/// columns, beams, openings and zones are made native from their parameters (<see cref="ArchicadRecognition"/>), each measured against
/// Archicad's body by the fidelity gate (enforced). Nothing of Lapin's own is added: no lintels, reveals or joins beyond the file's.
/// <para>
/// Received again, an element whose hash did not change is left as it is (native or reference); one that changed comes in again with
/// its GlobalId and is offered again; one gone from Archicad is removed. A wall's hash takes its doors' and windows' with it.
/// Elements whose GlobalIds are this project's own (it was sent from it) are refused unless received as a copy.
/// </para>
/// </summary>
public static class ArchicadReceive
{
    public const string Key = "archicad";

    public static string ModelKey(ArchicadSource s, bool asCopy) => $"archicad|{s.Project ?? "untitled"}{(asCopy ? "|copy" : "")}";

    public static List<Entity> Existing(IModelView m, string key) =>
        [.. m.Entities.Where(e => (string?)(e.Attrs[Key] as JsonObject)?["key"] == key && e.Class != IfcClass.BuildingStorey)];

    /// <summary>The elements a receive brings: those with a body, and the zones (a room needs none); the rest counted by why not.</summary>
    public static (List<ArchicadElement> Taken, Dictionary<string, int> Skipped) Taken(ArchicadModel m)
    {
        var skipped = new Dictionary<string, int>(m.Skipped);
        var taken = new List<ArchicadElement>();
        foreach (var e in m.Elements)
        {
            if (e.Bodies.Count == 0 && e.Type != "Zone") { var k = $"2D only, or no 3D body ({e.Type})"; skipped[k] = skipped.GetValueOrDefault(k) + 1; continue; }
            taken.Add(e);
        }
        return (taken, skipped);
    }

    /// <summary>The hash a receive compares: an element's own, and for a wall and its doors and windows all of theirs together.</summary>
    public static Dictionary<string, string> Hashes(IReadOnlyList<ArchicadElement> els)
    {
        var fillsOf = els.Where(IsFill).GroupBy(e => HostOf(e) ?? "").ToDictionary(g => g.Key, g => g.OrderBy(x => x.Guid, StringComparer.Ordinal).ToList());
        var byGuid = els.ToDictionary(e => e.Guid);
        var o = new Dictionary<string, string>();
        foreach (var e in els)
        {
            var host = IsFill(e) ? HostOf(e) is { } h && byGuid.TryGetValue(h, out var he) ? he : null : e;
            if (host == null) { o[e.Guid] = e.Hash; continue; }
            var group = new[] { host }.Concat(fillsOf.GetValueOrDefault(host.Guid) ?? []).Select(x => x.Hash);
            o[e.Guid] = Lapin.Model.Hashes.Sha256Hex(string.Join("|", group))[..32];
        }
        return o;
    }

    public static bool IsFill(ArchicadElement e) => e.Type is "Door" or "Window";

    public static string? HostOf(ArchicadElement e) => (string?)e.Details["ownerElementId"]?["guid"];

    /// <summary>The shift of Archicad's coordinates into the project's: none from its project origin; from its survey point, the transform's move (a turned one is refused).</summary>
    public static Vec3 Shift(ArchicadModel m, ArchicadReceiveOptions o)
    {
        if (o.Origin != "survey" || m.ProjectToSurvey is not { } t) return Vec3.Zero;
        if (Math.Abs(t[0] - 1) > 1e-9 || Math.Abs(t[5] - 1) > 1e-9 || Math.Abs(t[10] - 1) > 1e-9 || Math.Abs(t[1]) > 1e-9 || Math.Abs(t[4]) > 1e-9)
            throw new CommandException("Archicad's survey point is turned against its project origin: receive from the project origin (a turned survey point comes in a later stage).");
        return new Vec3(t[3], t[7], t[11]);
    }

    /// <summary>The GlobalId an element comes in with: Archicad's IFC id (or the one it came into Archicad with), else one derived from the model and its guid.</summary>
    static string IdOf(ArchicadElement e, string key, bool asCopy) =>
        !asCopy && e.GlobalId is { } g && GlobalId.IsValid(g) ? g : ArchicadForeign.Derived(key, e.Guid);

    /// <summary>This project's own elements the model's elements name by their GlobalIds (it was sent from this project).</summary>
    public static int Native(IModelView m, IEnumerable<ArchicadElement> els, string key) =>
        els.Select(e => e.GlobalId).OfType<string>().Distinct().Count(id => m.Get(id) is { } x && (string?)(x.Attrs[Key] as JsonObject)?["key"] != key);

    /// <summary>What a receive would do, before it is done.</summary>
    public static JsonObject Survey(IModelView m, ArchicadModel a, ArchicadReceiveOptions o)
    {
        var key = ModelKey(a.Source, o.AsCopy);
        var (taken, skipped) = Taken(a);
        var existing = Existing(m, key);
        var guids = existing.Select(e => (string?)(e.Attrs[Key] as JsonObject)?["guid"]).OfType<string>().ToHashSet();
        var updates = taken.Count(e => guids.Contains(e.Guid));
        var byType = new JsonObject();
        foreach (var g in taken.GroupBy(e => e.Type).OrderByDescending(g => g.Count())) byType[g.Key] = g.Count();
        var native = new JsonObject
        {
            ["walls"] = taken.Count(e => e.Type == "Wall"), ["slabs"] = taken.Count(e => e.Type == "Slab"), ["columns"] = taken.Count(e => e.Type == "Column"),
            ["beams"] = taken.Count(e => e.Type == "Beam"), ["openings"] = taken.Count(IsFill), ["zones"] = taken.Count(e => e.Type == "Zone"),
        };
        var candidates = taken.Count(e => e.Type is "Wall" or "Slab" or "Column" or "Beam" or "Zone" || IsFill(e));
        var skip = new JsonObject();
        foreach (var (k, v) in skipped.OrderByDescending(kv => kv.Value)) skip[k] = v;
        var nativeHere = Native(m, taken, key);
        return new JsonObject
        {
            ["port"] = a.Source.Port, ["project"] = a.Source.Project, ["untitled"] = a.Source.Untitled, ["archicad"] = a.Source.Archicad, ["tapir"] = a.Source.Tapir,
            ["storeys"] = new JsonArray([.. a.Stories.Select(s => (JsonNode)new JsonObject { ["index"] = s.Index, ["name"] = s.Name, ["level"] = s.Level, ["height"] = s.Height })]),
            ["elements"] = taken.Count, ["byType"] = byType, ["native"] = native, ["references"] = taken.Count - candidates, ["skipped"] = skip,
            ["existing"] = existing.Count, ["updates"] = updates, ["adds"] = taken.Count - updates, ["removes"] = Math.Max(0, existing.Select(e => (string?)(e.Attrs[Key] as JsonObject)?["guid"]).OfType<string>().Distinct().Count(g => !taken.Any(t => t.Guid == g))),
            ["fromThisProject"] = nativeHere > 0, ["ms"] = Math.Round(a.Milliseconds), ["notes"] = new JsonArray([.. a.Notes.Select(n => (JsonNode)n)]),
        };
    }

    /// <summary>Whether Archicad's model changed since the last receive: what would be added, updated and removed (a full compare by hash; A2 asks Archicad's changes instead).</summary>
    public static JsonObject Status(IModelView m, ArchicadModel a, bool asCopy = false)
    {
        var key = ModelKey(a.Source, asCopy);
        var (taken, _) = Taken(a);
        var hashes = Hashes(taken);
        var before = new Dictionary<string, string?>();
        foreach (var e in Existing(m, key))
            if ((string?)(e.Attrs[Key] as JsonObject)?["guid"] is { } g) before.TryAdd(g, (string?)(e.Attrs[Key] as JsonObject)?["hash"]);
        var added = taken.Count(e => !before.ContainsKey(e.Guid));
        var updated = taken.Count(e => before.TryGetValue(e.Guid, out var h) && h != hashes[e.Guid]);
        var removed = before.Keys.Count(g => !taken.Any(t => t.Guid == g));
        return new JsonObject
        {
            ["received"] = before.Count > 0, ["added"] = added, ["updated"] = updated, ["removed"] = removed, ["same"] = taken.Count - added - updated,
            ["changed"] = added + updated + removed > 0,
        };
    }

    public static ArchicadReceiveResult Apply(IEdit edit, ArchicadModel a, ArchicadReceiveOptions o, IServices? services = null, Action<string, int, int>? progress = null, CancellationToken ct = default)
    {
        var m = edit.Model;
        var key = ModelKey(a.Source, o.AsCopy);
        var shift = Shift(a, o);
        var (taken, skipped) = Taken(a);
        var nativeHere = Native(m, taken, key);
        if (nativeHere > 0 && !o.AsCopy)
            throw new CommandException($"This Archicad model was made from this project: {nativeHere} of its elements are this project's own elements (the same GlobalIds). Receive it as a copy (with ids of its own) to compare, or receive it in another project.");
        var notes = new List<string>(a.Notes);
        var at = $"{Math.Round(shift.X, 4)},{Math.Round(shift.Y, 4)},{Math.Round(shift.Z, 4)}";
        var hashes = Hashes(taken);

        // What earlier receives of this model made, by Archicad guid (a native wall's openings and fills are its own entities).
        var existing = Existing(m, key);
        var byGuid = new Dictionary<string, List<Entity>>();
        foreach (var e in existing)
            if ((string?)(e.Attrs[Key] as JsonObject)?["guid"] is { } g) (byGuid.TryGetValue(g, out var l) ? l : byGuid[g] = []).Add(e);
        var group = existing.Select(e => Groups.Of(m, e.Id)).FirstOrDefault(g => g != null);

        bool Same(Entity e, ArchicadElement el) => e.Attrs[Key] is JsonObject meta && (string?)meta["hash"] == hashes[el.Guid] && (string?)meta["at"] == at;
        var takenGuids = taken.Select(t => t.Guid).ToHashSet();
        // Gone from Archicad, or changed: removed first (a native wall takes its openings with it).
        var removed = 0;
        foreach (var (g, list) in byGuid)
        {
            var el = taken.FirstOrDefault(t => t.Guid == g);
            if (el != null && list.All(e => Same(e, el))) continue;
            foreach (var e in list)
                if (m.Get(e.Id) != null)
                {
                    edit.Remove(e.Id);
                    if (el == null) removed++;
                }
        }

        progress?.Invoke("Matching Archicad's stories", 0, 0);
        var storeys = ArchicadStoreys.Resolve(edit, a, key, shift.Z, o.CreateStoreys, notes);

        // The surfaces as Lapin materials: one each, by name, with its colour; a see-through one with its transparency.
        var materials = new Dictionary<string, string>();
        string? MaterialOf(string? surface)
        {
            if (surface == null || !a.Surfaces.TryGetValue(surface, out var s)) return null;
            if (materials.TryGetValue(surface, out var id)) return id;
            var mat = Materials.Ensure(edit, s.Name, s.Color);
            if (s.Transparency > 0.001 && mat.NumOrNull("transparency") == null)
            {
                var attrs = mat.CloneAttrs();
                attrs["transparency"] = Math.Round(s.Transparency, 3);
                edit.Replace(mat = mat.With(attrs: attrs));
            }
            return materials[surface] = mat.Id;
        }

        var classification = new ArchicadClassifications(edit, a);
        var perStorey = new Dictionary<string, int>();
        var made = new List<string>();
        var offered = new List<(string Id, ArchicadElement El)>();
        var joins = new Dictionary<string, List<string>>();
        int added = 0, updated = 0, unchanged = 0;
        var taken2 = new HashSet<string>();
        for (var i = 0; i < taken.Count; i++)
        {
            if (i % 100 == 0) { ct.ThrowIfCancellationRequested(); progress?.Invoke("Bringing the elements in", i, taken.Count); }
            var el = taken[i];
            var storey = storeys.StoreyOf(el.Floor, el.LowestZ is { } z ? z + shift.Z : null, edit);
            var storeyName = m.Get(storey)?.Name ?? "";
            perStorey[storeyName] = perStorey.GetValueOrDefault(storeyName) + 1;
            var before = byGuid.GetValueOrDefault(el.Guid);
            if (before != null && before.All(e => Same(e, el)) && before.Count > 0 && m.Get(before[0].Id) != null)
            {
                unchanged++;
                // Received before without recognition: offered now.
                foreach (var e in before) if (StoredBodies.Has(e) && (e.Attrs[Key] as JsonObject)?["recognition"] == null) offered.Add((e.Id, el));
                continue;
            }
            var id = before?.FirstOrDefault(e => (string?)(e.Attrs[Key] as JsonObject)?["role"] is null or "element")?.Id ?? IdOf(el, key, o.AsCopy);
            for (var n = 2; taken2.Contains(id) || m.Get(id) != null; n++) id = ArchicadForeign.Derived(key, $"{el.Guid}\n{n}");
            taken2.Add(id);
            var parts = ArchicadRead.Triangulate(el.Bodies, shift);
            var body = StoredBodies.Write(parts.Select(p =>
            {
                var surface = a.Surfaces.GetValueOrDefault(p.Surface ?? "");
                return (surface?.Name ?? "Body", surface?.Color ?? "#c9ced6", (IReadOnlyList<Vec3>)p.Vertices, (IReadOnlyList<(int, int, int)>)p.Triangles);
            }));
            if (body["parts"] is JsonArray written)
                for (var k = 0; k < parts.Count && k < written.Count; k++)
                    if (MaterialOf(parts[k].Surface) is { } mid && written[k] is JsonObject part) part["material"] = mid;
            var attrs = new JsonObject
            {
                [StoredBodies.Key] = body,
                ["properties"] = Properties(a, el),
                [Key] = Meta(key, el, hashes[el.Guid], at, a.Source),
                ["source"] = $"Archicad: {a.Source.Label}",
            };
            if (ArchicadForeign.CompositeName(a, el.Details) is { } typeName) attrs[MeshRecognition.TypeNameKey] = typeName;
            classification.Write(attrs, el);
            var cls = ClassOf(el);
            var name = el.IdText is { Length: > 0 } idt ? idt : el.Type;
            edit.Add(new Entity(id, cls, name, attrs));
            if (before != null) updated++; else { added++; made.Add(id); }
            offered.Add((id, el));
            (joins.TryGetValue(storey, out var js) ? js : joins[storey] = []).Add(id);
        }
        foreach (var (storeyId, ids) in joins) edit.RelateAll(IfcClass.RelContainedInSpatialStructure, storeyId, ids);

        // One group for the model: the first receive makes it, a later one adds to it.
        string? groupId = group != null && m.Get(group.Id) != null ? group.Id : null;
        if (made.Count > 0)
        {
            if (groupId != null) edit.RelateAll(Groups.Rel, groupId, made);
            else groupId = Groups.Make(edit, a.Source.Project ?? "Archicad", made)?.Id;
        }
        var back = offered.Select(x => x.Id).Where(id => !made.Contains(id) && m.Get(id) != null && Groups.Of(m, id) == null).ToList();
        if (groupId != null && back.Count > 0) edit.RelateAll(Groups.Rel, groupId, back);

        ArchicadRecognitionReport? recognition = null;
        if (o.Recognize && services != null)
        {
            progress?.Invoke("Recognising the elements", 0, 0);
            recognition = ArchicadRecognition.Run(edit, services, a, offered, storeys, shift, o.Fidelity);
        }
        if (groupId != null) Groups.DropEmpty(edit, [groupId]);
        storeys.Finish(edit);
        var now = Existing(m, key).Where(e => (string?)(e.Attrs[Key] as JsonObject)?["role"] is null or "element").ToList();
        var nativeNow = now.Count(e => !StoredBodies.Has(e));
        return new ArchicadReceiveResult(added, updated, unchanged, removed, nativeNow, now.Count - nativeNow, perStorey, skipped, recognition, storeys.Report, groupId, notes);
    }

    /// <summary>The class a reference stands for: Archicad's IFC type for it (IfcWallStandardCase read as IfcWall), else a proxy.</summary>
    public static string ClassOf(ArchicadElement e) => e.IfcType switch
    {
        "IfcWallStandardCase" or "IfcWallElementedCase" => IfcClass.Wall,
        "IfcSlabStandardCase" or "IfcSlabElementedCase" => IfcClass.Slab,
        "IfcColumnStandardCase" => IfcClass.Column,
        "IfcBeamStandardCase" => IfcClass.Beam,
        "IfcDoorStandardCase" => IfcClass.Door,
        "IfcWindowStandardCase" => IfcClass.Window,
        { Length: > 3 } t when t.StartsWith("Ifc", StringComparison.Ordinal) => t,
        _ => e.Type switch
        {
            "Wall" => IfcClass.Wall, "Slab" => IfcClass.Slab, "Column" => IfcClass.Column, "Beam" => IfcClass.Beam, "Door" => IfcClass.Door, "Window" => IfcClass.Window,
            "Zone" => IfcClass.Space, "Roof" => "IfcRoof", "Stair" => "IfcStair", "Railing" => "IfcRailing", "CurtainWall" => "IfcCurtainWall",
            _ => "IfcBuildingElementProxy",
        },
    };

    internal static JsonObject Meta(string key, ArchicadElement el, string hash, string at, ArchicadSource s, string role = "element") => new()
    {
        ["key"] = key, ["guid"] = el.Guid, ["type"] = el.Type, ["hash"] = hash, ["at"] = at, ["layer"] = el.Layer, ["project"] = s.Project,
        ["role"] = role == "element" ? null : role,
    };

    /// <summary>
    /// An element's properties: Lapin's own set <c>Archicad</c> (its guid, element ID, type, layer, story), Archicad's custom property
    /// groups as sets named as in Archicad, and the IFC property sets Archicad maps to it under their IFC names.
    /// </summary>
    public static JsonObject Properties(ArchicadModel a, ArchicadElement el)
    {
        var o = new JsonObject
        {
            ["Archicad"] = new JsonObject
            {
                ["Guid"] = el.Guid, ["ID"] = el.IdText, ["Type"] = el.Type, ["Layer"] = a.Layers.TryGetValue(el.Layer, out var ln) ? ln : el.Layer.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Story"] = a.Story(el.Floor)?.Name, ["IFC type"] = el.IfcType,
            },
        };
        foreach (var (k, v) in el.Properties) if (k != "Archicad") o[k] = v?.DeepClone();
        foreach (var (k, v) in el.IfcProperties) if (k != "Archicad" && o[k] == null) o[k] = v?.DeepClone();
        return o;
    }
}

/// <summary>Archicad's classification systems as the project's (<see cref="Classifications"/>): each by name, its items added as elements use them, with their parents.</summary>
sealed class ArchicadClassifications(IEdit edit, ArchicadModel a)
{
    readonly Dictionary<string, string> _systems = [];

    public void Write(JsonObject attrs, ArchicadElement el)
    {
        var refs = new JsonArray();
        foreach (var (sys, item) in el.Classifications)
        {
            if (!a.ClassificationSystems.TryGetValue(sys, out var s) || !s.Items.TryGetValue(item, out var it) || it.Code.Length == 0) continue;
            var systemId = System(s);
            Add(systemId, s, item);
            refs.Add((JsonNode)new JsonObject { ["system"] = systemId, ["code"] = it.Code });
        }
        if (refs.Count > 0) attrs[Classifications.Key] = refs;
    }

    string System(ArchicadClassificationSystem s)
    {
        if (_systems.TryGetValue(s.Guid, out var id)) return id;
        var existing = Classifications.System(edit.Model, s.Name);
        if (existing == null)
        {
            var attrs = new JsonObject { ["items"] = new JsonArray() };
            if (s.Source != null) attrs["source"] = s.Source;
            if (s.Version != null) attrs["edition"] = s.Version;
            var made = ArchicadForeign.Derived("archicad classification", s.Name);
            existing = new Entity(edit.Model.Get(made) == null ? made : edit.NewId(), Classifications.Class, s.Name, attrs);
            edit.Add(existing);
        }
        return _systems[s.Guid] = existing.Id;
    }

    void Add(string systemId, ArchicadClassificationSystem s, string item)
    {
        var system = edit.Model.Get(systemId)!;
        var have = Classifications.Items(system).Select(x => x.Code).ToHashSet();
        var add = new List<JsonObject>();
        for (var at = item; at != null && s.Items.TryGetValue(at, out var it) && !have.Contains(it.Code); at = it.Parent)
        {
            var o = new JsonObject { ["code"] = it.Code, ["title"] = it.Title };
            if (it.Parent != null && s.Items.TryGetValue(it.Parent, out var p) && p.Code.Length > 0) o["parent"] = p.Code;
            add.Add(o);
            have.Add(it.Code);
        }
        if (add.Count == 0) return;
        var attrs = system.CloneAttrs();
        var items = (attrs["items"] as JsonArray)?.DeepClone() as JsonArray ?? [];
        foreach (var x in Enumerable.Reverse(add)) items.Add((JsonNode)x);
        attrs["items"] = items;
        edit.Replace(system.With(attrs: attrs));
    }
}
