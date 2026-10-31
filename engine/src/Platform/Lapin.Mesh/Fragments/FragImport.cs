using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mesh.Fragments;

/// <summary>What a Fragments import brought: elements (references), shared bodies, storeys made, what it left out, and its notes.</summary>
/// <summary><paramref name="Native"/>: items already in the project as Lapin elements (the file came from it), left as they are; <paramref name="LeftOut"/>: items the choice left out.</summary>
public sealed record FragImportResult(int Elements, int Shared, int Storeys, int Skipped, IReadOnlyList<string> Notes, IReadOnlyDictionary<string, int> ByClass, int Native = 0, int LeftOut = 0, EditableReport? Editable = null)
{
    public JsonObject ToJson()
    {
        var by = new JsonObject();
        foreach (var (k, v) in ByClass.OrderByDescending(kv => kv.Value)) by[k] = v;
        return new JsonObject
        {
            ["elements"] = Elements, ["shared"] = Shared, ["storeys"] = Storeys, ["skipped"] = Skipped, ["byClass"] = by, ["native"] = Native, ["leftOut"] = LeftOut, ["editable"] = Editable?.ToJson(),
            ["notes"] = new JsonArray(Notes.Select(n => (JsonNode)n).ToArray()),
        };
    }
}

/// <summary>
/// A Fragments file into the open project (docs/fragments.md): every item with
/// geometry becomes a reference drawn from its stored body (<see cref="StoredBodies"/>),
/// in the class the file names, with the file's attributes under IFC
/// properties; items whose geometry is the same (the same shells, materials
/// and placements) share one <see cref="StoredBodies.SharedClass"/> and stand
/// as instances of it. Elements go to the storey the file contains them in,
/// matched by name to the project's storeys and made when missing. The ids
/// are the file's GlobalIds, so importing the file again updates what it
/// brought before; a Lapin element with the file's id (the file came from this
/// project) is left as it is. Nothing is recognised into native elements: a <c>.frag</c>
/// holds triangles, not axes and layers.
/// </summary>
public static class FragImport
{
    /// <summary>A file's bytes read as a Fragments model, or why not.</summary>
    public static FragModel Read(byte[] bytes, string name)
    {
        try { return FragReader.Read(bytes); }
        catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or ArgumentOutOfRangeException or InvalidDataException)
        {
            throw new CommandException($"„{name}” is not a Fragments file Lapin can read: {ex.Message}");
        }
    }

    public static FragImportResult Run(Session session, byte[] bytes, string name, out Diff diff, FragImportOptions? options = null)
    {
        session.Progress?.Invoke("Reading the file", 0, 0);
        return Run(session, Read(bytes, name), name, out diff, options);
    }

    /// <summary>A read file into the open project: what <paramref name="options"/> chose (kinds and storeys as <see cref="FragSurvey"/> names them; null: all), as one group unless told otherwise.</summary>
    public static FragImportResult Run(Session session, FragModel model, string name, out Diff diff, FragImportOptions? options = null)
    {
        var o = options ?? new FragImportOptions();
        var reader = new Reader(session, model, name, o);
        // What the file places anew comes in as one group named after it (docs/groups.md).
        // Everything editable on request: the references made Studio solids where they can be.
        EditableReport? editable = null;
        void Apply(IEdit edit) { if (o.Editable) editable = EditableReferences.Around(edit, session.Catalog, reader.Apply); else reader.Apply(edit); }
        diff = session.Edit($"Import {name}", edit =>
        {
            if (o.Group) Groups.Around(edit, Path.GetFileNameWithoutExtension(name), Apply);
            else Apply(edit);
        });
        return reader.Result with { Editable = editable };
    }

    /// <summary>Fragments' frame (Y up) to the model's (Z up): (x, y, z) → (x, −z, y).</summary>
    static Vec3 ToModel((double X, double Y, double Z) p) => new(p.X, -p.Z, p.Y);
    /// <summary>The model's frame to Fragments': (x, y, z) → (x, z, −y).</summary>
    static (double X, double Y, double Z) ToFrag(Vec3 p) => (p.X, p.Z, -p.Y);

    sealed class Reader(Session session, FragModel model, string name, FragImportOptions o)
    {
        readonly List<string> notes = [];
        readonly Dictionary<string, int> byClass = [];
        int elements, shared, storeysMade, skipped, native, leftOut;
        public FragImportResult Result => new(elements, shared, storeysMade, skipped, notes, byClass, native, leftOut);

        /// <summary>One item's geometry: its samples (a shell in a material, placed by a local transform) and its global transform.</summary>
        sealed record Piece(int Shell, FragMaterial Material, FragTransform Local);

        public void Apply(IEdit edit)
        {
            var m = model.Meshes;
            var itemOfLocal = new Dictionary<uint, int>();
            for (var i = 0; i < model.LocalIds.Length; i++) itemOfLocal[model.LocalIds[i]] = i;
            if (m.Coordinates is { IsIdentity: false }) notes.Add("The file places its model by a coordinates transform; it was read as it is, in its own coordinates.");

            // The pieces of every mesh (an item with geometry), by mesh index.
            var pieces = new Dictionary<uint, List<Piece>>();
            foreach (var s in m.Samples)
            {
                if (s.Representation >= m.Representations.Length) { skipped++; continue; }
                var rep = m.Representations[s.Representation];
                if (rep.Class != FragSchema.RepresentationShell || rep.Id >= m.Shells.Length) { skipped++; continue; }
                var material = s.Material < m.Materials.Length ? m.Materials[s.Material] : new FragMaterial(200, 200, 200, 255, 0, 0);
                var local = s.LocalTransform < m.LocalTransforms.Length ? m.LocalTransforms[s.LocalTransform] : FragTransform.Identity;
                (pieces.TryGetValue(s.Item, out var list) ? list : pieces[s.Item] = []).Add(new Piece((int)rep.Id, material, local));
            }
            if (m.CircleExtrusions > 0) notes.Add($"{m.CircleExtrusions} circle extrusion(s) (pipes, bars) were left out: Lapin reads shells.");

            // The storeys the file names, matched to the project's by name, made when missing.
            var storeyOfLocal = new Dictionary<uint, string>();
            var building = Spatial.EnsureBuilding(edit);
            var storeyItems = new List<(uint LocalId, string Name, double? Elevation)>();
            for (var i = 0; i < model.LocalIds.Length; i++)
                if (i < model.Categories.Length && model.Categories[i] == "IFCBUILDINGSTOREY")
                {
                    var attrs = model.AttributesOf(i);
                    storeyItems.Add((model.LocalIds[i], Text(attrs, "Name") ?? $"Storey {model.LocalIds[i]}", Number(attrs, "Elevation")));
                }
            // An element's storey: the one the file contains it in; else the lowest the model has.
            var meshOfLocal = new Dictionary<uint, uint>();
            for (uint k = 0; k < m.MeshesItems.Length; k++) if (m.MeshesItems[k] < model.LocalIds.Length) meshOfLocal[model.LocalIds[m.MeshesItems[k]]] = k;
            var storeyOfElement = new Dictionary<uint, uint>();
            var lowest = new Dictionary<uint, double>();
            foreach (var (local, mesh) in meshOfLocal)
            {
                if (!pieces.ContainsKey(mesh)) continue;
                foreach (var id in model.RelationsOf(local).GetValueOrDefault("ContainedInStructure") ?? [])
                    if (storeyItems.Any(s => s.LocalId == id)) { storeyOfElement[local] = id; break; }
                if (storeyOfElement.TryGetValue(local, out var sid) && mesh < m.GlobalTransforms.Length)
                {
                    var z = ToModel((m.GlobalTransforms[mesh].X, m.GlobalTransforms[mesh].Y, m.GlobalTransforms[mesh].Z)).Z;
                    lowest[sid] = Math.Min(lowest.GetValueOrDefault(sid, double.MaxValue), z);
                }
            }
            foreach (var (local, storeyName, elevation) in storeyItems)
            {
                if (!storeyOfElement.ContainsValue(local)) continue;
                if (o.Storeys != null && !o.Storeys.Contains(FragSurvey.KeyOf(model, local))) continue;
                var existing = Spatial.Storeys(edit.Model).FirstOrDefault(s => string.Equals(s.Name, storeyName, StringComparison.OrdinalIgnoreCase));
                if (existing != null) { storeyOfLocal[local] = existing.Id; continue; }
                var z = elevation ?? (lowest.TryGetValue(local, out var lo) && lo < double.MaxValue ? Math.Round(lo, 2) : 0);
                var storey = new Entity(edit.NewId(), IfcClass.BuildingStorey, storeyName, new JsonObject { ["elevation"] = z, ["height"] = 3.0 });
                edit.Add(storey);
                edit.Relate(IfcClass.RelAggregates, building.Id, storey.Id);
                storeyOfLocal[local] = storey.Id;
                storeysMade++;
            }
            var fallback = Spatial.Storeys(edit.Model).OrderBy(s => s.Num("elevation")).FirstOrDefault()?.Id;
            if (fallback == null)
            {
                var ground = new Entity(edit.NewId(), IfcClass.BuildingStorey, "Ground floor", new JsonObject { ["elevation"] = 0.0, ["height"] = 3.0 });
                edit.Add(ground);
                edit.Relate(IfcClass.RelAggregates, building.Id, ground.Id);
                fallback = ground.Id;
                storeysMade++;
            }

            // Items whose pieces are the same share a body: the key names the shells, materials and local placements.
            var keyOfMesh = new Dictionary<uint, string>();
            var usesOfKey = new Dictionary<string, int>();
            foreach (var (mesh, list) in pieces)
            {
                var key = string.Join("|", list.Select(p => $"{p.Shell}:{p.Material.R},{p.Material.G},{p.Material.B},{p.Material.A}:{p.Local}"));
                keyOfMesh[mesh] = key;
                usesOfKey[key] = usesOfKey.GetValueOrDefault(key) + 1;
            }
            var sharedIds = new Dictionary<string, string?>();
            var modelGuid = model.Guid ?? name;

            // Each element's storey, written once per storey after the loop (one at a time it was quadratic).
            var joins = new Dictionary<string, List<string>>();
            var total = meshOfLocal.Count;
            var done = 0;
            foreach (var (local, mesh) in meshOfLocal.OrderBy(kv => kv.Value))
            {
                if (++done % 500 == 0) session.Progress?.Invoke("Bringing the elements in", done, total);
                if (!pieces.TryGetValue(mesh, out var list) || !itemOfLocal.TryGetValue(local, out var item)) { skipped++; continue; }
                // What the import's choice leaves out (the kinds and storeys of the survey).
                if (o.Kinds != null && !o.Kinds.Contains(FragSurvey.KindOf(model, item))) { leftOut++; continue; }
                if (o.Storeys != null && !o.Storeys.Contains(storeyOfElement.TryGetValue(local, out var chosen) ? FragSurvey.KeyOf(model, chosen) : "")) { leftOut++; continue; }
                var global = mesh < m.GlobalTransforms.Length ? m.GlobalTransforms[mesh] : FragTransform.Identity;
                var category = item < model.Categories.Length ? model.Categories[item] : "IFCBUILDINGELEMENTPROXY";
                var cls = category.StartsWith("IFC", StringComparison.OrdinalIgnoreCase) && category.Length > 3 ? IfcModel.ClassName(category) : IfcClass.BuildingElementProxy;
                var attrs = model.AttributesOf(item);
                var elementName = Text(attrs, "Name") is { Length: > 0 } n ? n : cls[3..];
                var guid = GuidOf(local);
                var id = guid ?? DerivedId($"item\n{local}");
                // A Lapin element with this id (the file came from this project): it stays what it is, never a reference over it.
                if (edit.Model.Get(id) is { } own && !StoredBodies.Has(own)) { native++; continue; }
                var json = new JsonObject();
                var key = keyOfMesh[mesh];
                if (usesOfKey[key] > 1)
                {
                    // A body many items share, once, in its own frame; the item an instance of it, placed by its transform.
                    if (!sharedIds.TryGetValue(key, out var bodyId))
                    {
                        var parts = Parts(list, FragTransform.Identity);
                        bodyId = null;
                        if (parts.Count > 0)
                        {
                            bodyId = DerivedId($"shared-body\n{key}");
                            Put(edit, new Entity(bodyId, StoredBodies.SharedClass, $"{cls[3..]} (shared)", new JsonObject { [StoredBodies.Key] = StoredBodies.Write(parts), ["source"] = key }));
                            shared++;
                        }
                        sharedIds[key] = bodyId;
                    }
                    if (bodyId == null) { skipped++; continue; }
                    json[StoredBodies.BodyOf] = bodyId;
                    json[StoredBodies.XfKey] = StoredBodies.WriteXf(Xf(global));
                }
                else
                {
                    var parts = Parts(list, global);
                    if (parts.Count == 0) { skipped++; continue; }
                    json[StoredBodies.Key] = StoredBodies.Write(parts);
                }
                var props = new JsonObject();
                foreach (var (k, v) in attrs) if (v is JsonValue) props[k] = v.DeepClone();
                if (props.Count > 0) json["properties"] = new JsonObject { ["Fragments"] = props };
                json["fragment"] = new JsonObject { ["localId"] = local, ["category"] = category, ["model"] = modelGuid };
                Put(edit, new Entity(id, cls, elementName, json, Text(attrs, "PredefinedType")));
                var storeyId = storeyOfElement.TryGetValue(local, out var sl) && storeyOfLocal.TryGetValue(sl, out var st) ? st : fallback;
                if (Spatial.StoreyOf(edit.Model, id)?.Id != storeyId) (joins.TryGetValue(storeyId, out var js) ? js : joins[storeyId] = []).Add(id);
                elements++;
                byClass[cls] = byClass.GetValueOrDefault(cls) + 1;
            }
            foreach (var (storeyId, ids) in joins) edit.RelateAll(IfcClass.RelContainedInSpatialStructure, storeyId, ids);
            if (native > 0) notes.Add($"{native} element(s) are already in the project as Lapin elements (the file came from it): they were left as they are.");
            var withoutGeometry = model.LocalIds.Length - meshOfLocal.Count;
            if (withoutGeometry > 0) notes.Add($"{withoutGeometry} item(s) without geometry (types, materials, the spatial structure) were read for their names only.");
        }

        /// <summary>An item's pieces as a stored body's parts, in the model's frame, placed by <paramref name="global"/> (the identity for a shared body's own frame).</summary>
        List<(string Name, string Color, IReadOnlyList<Vec3> Vertices, IReadOnlyList<(int A, int B, int C)> Triangles)> Parts(List<Piece> list, FragTransform global)
        {
            var o = new List<(string, string, IReadOnlyList<Vec3>, IReadOnlyList<(int, int, int)>)>();
            foreach (var p in list)
            {
                var shell = model.Meshes.Shells[p.Shell];
                var n = shell.Points.Length / 3;
                var vertices = new List<Vec3>(n);
                for (var i = 0; i < n; i++)
                {
                    var q = p.Local.Apply(shell.Points[i * 3], shell.Points[i * 3 + 1], shell.Points[i * 3 + 2]);
                    vertices.Add(ToModel(global.Apply(q.X, q.Y, q.Z)));
                }
                var tris = new List<(int, int, int)>();
                foreach (var profile in shell.Profiles)
                {
                    // A profile is one polygon: a fan from its first point (a triangle is itself).
                    if (profile.Length < 3) continue;
                    if (profile.Any(k => k < 0 || k >= n)) continue;
                    for (var k = 1; k + 1 < profile.Length; k++) tris.Add((profile[0], profile[k], profile[k + 1]));
                }
                if (shell.Holes.Length > 0) notes.Add("A shell with holes was read by its outer profiles: the holes are filled.");
                if (tris.Count == 0) continue;
                o.Add(("Body", $"#{p.Material.R:x2}{p.Material.G:x2}{p.Material.B:x2}", vertices, tris));
            }
            return o;
        }

        /// <summary>An instance's twelve numbers in the model's frame: the images of the axes and of the origin under the file's global transform.</summary>
        static double[] Xf(FragTransform g)
        {
            var o = new double[12];
            // The model's axes, taken to the file's frame, placed (as directions), brought back.
            Vec3[] axes = [new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)];
            for (var k = 0; k < 3; k++)
            {
                var f = ToFrag(axes[k]);
                var placed = g.Apply(f.X, f.Y, f.Z);
                var dir = ToModel((placed.X - g.X, placed.Y - g.Y, placed.Z - g.Z));
                o[k * 3] = dir.X; o[k * 3 + 1] = dir.Y; o[k * 3 + 2] = dir.Z;
            }
            var origin = ToModel((g.X, g.Y, g.Z));
            o[9] = origin.X; o[10] = origin.Y; o[11] = origin.Z;
            return o;
        }

        string? GuidOf(uint local)
        {
            var k = Array.IndexOf(model.GuidsItems, local);
            return k >= 0 && k < model.Guids.Length && model.Guids[k].Length == 22 ? model.Guids[k] : null;
        }

        /// <summary>A GlobalId the same for the same file and key, so an import done again finds what it made.</summary>
        string DerivedId(string key)
        {
            var bytes = MD5.HashData(Encoding.UTF8.GetBytes((model.Guid ?? name) + "\n" + key));
            return GlobalId.FromGuid(new Guid(bytes));
        }

        static void Put(IEdit edit, Entity e)
        {
            if (edit.Model.Get(e.Id) != null) edit.Replace(e); else edit.Add(e);
        }

        static string? Text(Dictionary<string, JsonNode?> attrs, string key) =>
            attrs.TryGetValue(key, out var v) && v is JsonValue jv ? (jv.TryGetValue<string>(out var s) ? s : jv.ToJsonString()) : null;

        static double? Number(Dictionary<string, JsonNode?> attrs, string key) =>
            attrs.TryGetValue(key, out var v) && v is JsonValue jv && jv.TryGetValue<double>(out var d) ? d : null;
    }
}

/// <summary>What of a Fragments file comes in: its kinds and storeys as <see cref="FragSurvey"/> names them (null: all; "" the items on none), and whether as one group.</summary>
public sealed record FragImportOptions(IReadOnlySet<string>? Kinds = null, IReadOnlySet<string>? Storeys = null, bool Group = true, bool Editable = false);
