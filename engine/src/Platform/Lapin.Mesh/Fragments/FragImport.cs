using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mesh.Fragments;

/// <summary>What a Fragments import brought: elements (references), shared bodies, storeys made, what it left out, and its notes.</summary>
/// <summary><paramref name="Native"/>: items already in the project as Lapin elements (the file came from it), left as they are; <paramref name="LeftOut"/>: items the choice left out.</summary>
public sealed record FragImportResult(int Elements, int Shared, int Storeys, int Skipped, IReadOnlyList<string> Notes, IReadOnlyDictionary<string, int> ByClass, int Native = 0, int LeftOut = 0, EditableReport? Editable = null, MeshRecognitionReport? Recognition = null, StoreyReplacement? Replaced = null)
{
    public JsonObject ToJson()
    {
        var by = new JsonObject();
        foreach (var (k, v) in ByClass.OrderByDescending(kv => kv.Value)) by[k] = v;
        return new JsonObject
        {
            ["elements"] = Elements, ["shared"] = Shared, ["storeys"] = Storeys, ["skipped"] = Skipped, ["byClass"] = by, ["native"] = Native, ["leftOut"] = LeftOut, ["editable"] = Editable?.ToJson(), ["recognition"] = Recognition?.ToJson(), ["replaced"] = Replaced?.ToJson(),
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
        catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or ArgumentException or InvalidDataException or OverflowException)
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
        // Recognised from their triangles where a module takes them (a wall with its openings, a slab, a column: decision 214),
        // and what stays a reference made editable on request.
        EditableReport? editable = null;
        MeshRecognitionReport? recognition = null;
        void Apply(IEdit edit)
        {
            var before = edit.Model.Entities.Select(e => e.Id).ToHashSet();
            reader.Apply(edit);
            var brought = edit.Model.Entities.Where(e => !before.Contains(e.Id)).Select(e => e.Id).ToList();
            if (o.Recognize) recognition = MeshRecognition.Recognize(edit, session.Catalog, brought, o.Fidelity);
            if (o.Editable) editable = EditableReferences.Make(edit, session.Catalog, brought);
        }
        // The file's storeys replace the project's (ImportStoreys), in the same undo step.
        var replaced = StoreyReplacement.None;
        diff = session.Edit($"Import {name}", edit =>
        {
            if (o.Group) Groups.Around(edit, Path.GetFileNameWithoutExtension(name), Apply);
            else Apply(edit);
            replaced = ImportStoreys.Replace(edit, session.Catalog, reader.FileStoreys);
        });
        var r = reader.Result;
        return r with { Editable = editable, Recognition = recognition, Replaced = replaced, Notes = [.. r.Notes, .. replaced.Notes()] };
    }

    /// <summary>Fragments' frame (Y up) to the model's (Z up): (x, y, z) → (x, −z, y).</summary>
    static Vec3 ToModel((double X, double Y, double Z) p) => MeshMath.FromYUp(p.X, p.Y, p.Z);
    /// <summary>The model's frame to Fragments': (x, y, z) → (x, z, −y).</summary>
    static (double X, double Y, double Z) ToFrag(Vec3 p) => MeshMath.ToYUp(p.X, p.Y, p.Z);

    sealed class Reader(Session session, FragModel model, string name, FragImportOptions o)
    {
        readonly List<string> notes = [];
        readonly Dictionary<string, int> byClass = [];
        int elements, shared, storeysMade, skipped, native, leftOut;
        public FragImportResult Result => new(elements, shared, storeysMade, skipped, notes, byClass, native, leftOut);
        /// <summary>The model's storeys the file's became (made or found again).</summary>
        public IReadOnlyList<string> FileStoreys { get; private set; } = [];
        /// <summary>The file's coordinates transform (null: the identity), undone on every point.</summary>
        FragTransform? coordinates;

        /// <summary>A point of the file's (Y up, coordinated) placed by <paramref name="g"/>, in the model's frame (Z up, the project's coordinates).</summary>
        Vec3 Place(FragTransform g, double x, double y, double z, bool world = true)
        {
            var p = g.Apply(x, y, z);
            if (world && coordinates is { } c) p = c.Unapply(p.X, p.Y, p.Z);
            return ToModel(p);
        }

        /// <summary>One item's geometry: its samples (a shell in a material, placed by a local transform) and its global transform.</summary>
        sealed record Piece(int Shell, FragMaterial Material, FragTransform Local);

        public void Apply(IEdit edit)
        {
            var m = model.Meshes;
            var itemOfLocal = new Dictionary<uint, int>();
            for (var i = 0; i < model.LocalIds.Length; i++) itemOfLocal[model.LocalIds[i]] = i;
            itemOf = itemOfLocal;
            // The official importer stores the model moved near the origin and keeps the move as `coordinates` (stored = coordinates · project):
            // taken back, the elements stand where the IFC file had them, where Lapin's own import of that file puts them.
            coordinates = m.Coordinates is { IsIdentity: false } c ? c : null;

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
            var storeyLocals = storeyItems.Select(s => s.LocalId).ToHashSet();
            var meshOfLocal = new Dictionary<uint, uint>();
            for (uint k = 0; k < m.MeshesItems.Length; k++) if (m.MeshesItems[k] < model.LocalIds.Length) meshOfLocal[model.LocalIds[m.MeshesItems[k]]] = k;
            var storeyOfElement = new Dictionary<uint, uint>();
            var heights = new Dictionary<uint, List<double>>();
            foreach (var (local, mesh) in meshOfLocal)
            {
                if (!pieces.ContainsKey(mesh)) continue;
                foreach (var id in model.RelationsOf(local).GetValueOrDefault("ContainedInStructure") ?? [])
                    if (storeyLocals.Contains(id)) { storeyOfElement[local] = id; break; }
                if (storeyOfElement.TryGetValue(local, out var sid) && mesh < m.GlobalTransforms.Length)
                {
                    var z = Place(m.GlobalTransforms[mesh], 0, 0, 0).Z;
                    (heights.TryGetValue(sid, out var hl) ? hl : heights[sid] = []).Add(z);
                }
            }
            // The file's storeys, at the file's levels: the project's at the same id (an import again), else made under the file's
            // GlobalIds; then they replace the project's own (ImportStoreys, at the end of the import). A level is in the file's
            // unit (an IFC in millimetres) and the geometry in metres: one scale for the file (StoreyUnits, as a BIM Open Schema file's).
            // The typical height of each storey's items (the median of their placements).
            var typical = heights.ToDictionary(kv => kv.Key, kv => StoreyUnits.Median(kv.Value)!.Value);
            var scale = StoreyUnits.Scale([.. storeyItems.Where(x => x.Elevation != null).Select(x => (x.Elevation!.Value, typical.TryGetValue(x.LocalId, out var t) ? t : (double?)null))]);
            var fileStoreys = new List<string>();
            foreach (var (local, storeyName, elevation) in storeyItems.OrderBy(x => x.Elevation ?? 0))
            {
                if (o.Storeys != null && !o.Storeys.Contains(FragSurvey.KeyOf(model, local))) continue;
                var z = elevation is { } raw ? Math.Round(raw * scale, 4) : (typical.TryGetValue(local, out var lo) ? Math.Round(lo, 2) : 0);
                // The same storey named twice (a federated model's): one storey of the project, by its name and level.
                if (fileStoreys.Select(edit.Model.Get).FirstOrDefault(x => x != null && string.Equals(x.Name, storeyName, StringComparison.OrdinalIgnoreCase) && Math.Abs(x.Num("elevation") - z) <= ImportStoreys.Tolerance) is { } twin)
                {
                    storeyOfLocal[local] = twin.Id;
                    continue;
                }
                var sid = GuidOf(local) ?? DerivedId($"storey\n{local}");
                if (edit.Model.Get(sid) is { } again)
                {
                    if (again.Class != IfcClass.BuildingStorey) sid = edit.NewId();
                    else
                    {
                        var a = again.CloneAttrs();
                        a["elevation"] = z;
                        edit.Replace(again.With(name: storeyName, attrs: a));
                        storeyOfLocal[local] = again.Id;
                        fileStoreys.Add(again.Id);
                        continue;
                    }
                }
                var storey = new Entity(sid, IfcClass.BuildingStorey, storeyName, new JsonObject { ["elevation"] = z, ["height"] = 3.0 });
                edit.Add(storey);
                edit.Relate(IfcClass.RelAggregates, building.Id, storey.Id);
                storeyOfLocal[local] = storey.Id;
                fileStoreys.Add(storey.Id);
                storeysMade++;
            }
            FileStoreys = fileStoreys;
            if (!Spatial.Storeys(edit.Model).Any()) storeysMade++;
            // An item on no storey of the file's: on the file's storey nearest ±0.00, else the project's lowest.
            var fallback = fileStoreys.Select(edit.Model.Get).OfType<Entity>().OrderBy(x => Math.Abs(x.Num("elevation"))).FirstOrDefault()?.Id
                ?? Spatial.EnsureStorey(edit, session.Product).Id;

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
                        // In its own frame: the coordinates are undone by the instances' placement.
                        var parts = Parts(list, FragTransform.Identity, world: false);
                        bodyId = null;
                        if (parts.Count > 0)
                        {
                            bodyId = DerivedId($"shared-body\n{key}");
                            Put(edit, new Entity(bodyId, StoredBodies.SharedClass, $"{cls[3..]} (shared)", new JsonObject { [StoredBodies.Key] = Stored(edit, parts), ["source"] = key }));
                            shared++;
                        }
                        sharedIds[key] = bodyId;
                    }
                    if (bodyId == null) { skipped++; continue; }
                    json[StoredBodies.BodyOf] = bodyId;
                    json[StoredBodies.XfKey] = StoredBodies.WriteXf(Xf(global, coordinates));
                }
                else
                {
                    var parts = Parts(list, global);
                    if (parts.Count == 0) { skipped++; continue; }
                    json[StoredBodies.Key] = Stored(edit, parts);
                }
                var props = new JsonObject();
                foreach (var (k, v) in attrs) if (v is JsonValue) props[k] = v.DeepClone();
                var properties = new JsonObject();
                if (props.Count > 0) properties["Fragments"] = props;
                // Its property and quantity sets, its type and its materials, as the file relates them, shown under IFC properties as an IFC reference's.
                Definitions(local, props, properties);
                if (properties.Count > 0) json["properties"] = properties;
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
            if (withoutGeometry > 0) notes.Add($"{withoutGeometry} item(s) without geometry (types, property sets, materials, the spatial structure) were read into the elements' properties.");
        }

        /// <summary>An item's pieces as a stored body's parts, in the model's frame, placed by <paramref name="global"/> (the identity for a shared body's own frame).</summary>
        List<(string Name, string Color, IReadOnlyList<Vec3> Vertices, IReadOnlyList<(int A, int B, int C)> Triangles, FragMaterial Material)> Parts(List<Piece> list, FragTransform global, bool world = true)
        {
            var o = new List<(string, string, IReadOnlyList<Vec3>, IReadOnlyList<(int, int, int)>, FragMaterial)>();
            foreach (var p in list)
            {
                var shell = model.Meshes.Shells[p.Shell];
                var n = shell.Points.Length / 3;
                var vertices = new List<Vec3>(n);
                for (var i = 0; i < n; i++)
                {
                    var q = p.Local.Apply(shell.Points[i * 3], shell.Points[i * 3 + 1], shell.Points[i * 3 + 2]);
                    vertices.Add(Place(global, q.X, q.Y, q.Z, world));
                }
                var tris = Triangles(shell, vertices);
                if (tris.Count == 0) continue;
                o.Add(("Body", $"#{p.Material.R:x2}{p.Material.G:x2}{p.Material.B:x2}", vertices, tris, p.Material));
            }
            return o;
        }

        /// <summary>
        /// The parts as a stored body; a part whose colour lets light through (glass) takes a material of that colour and
        /// transparency, one per colour, so it draws, renders and exports see-through as in the file.
        /// </summary>
        JsonObject Stored(IEdit edit, List<(string Name, string Color, IReadOnlyList<Vec3> Vertices, IReadOnlyList<(int A, int B, int C)> Triangles, FragMaterial Material)> parts)
        {
            var body = StoredBodies.Write(parts.Select(p => (p.Name, p.Color, p.Vertices, p.Triangles)));
            if (body["parts"] is not JsonArray written) return body;
            for (var i = 0; i < parts.Count && i < written.Count; i++)
            {
                var mat = parts[i].Material;
                if (mat.A >= 255 || written[i] is not JsonObject part) continue;
                var colour = $"#{mat.R:x2}{mat.G:x2}{mat.B:x2}";
                if (!glassIds.TryGetValue(colour + mat.A, out var materialId))
                {
                    var name = $"Fragments {colour.TrimStart('#').ToUpperInvariant()} {Math.Round(100.0 * mat.A / 255)}%";
                    var material = Lapin.Platform.Materials.Ensure(edit, name, colour);
                    var a = material.CloneAttrs();
                    a["transparency"] = Math.Round(1 - mat.A / 255.0, 3);
                    edit.Replace(material.With(attrs: a));
                    glassIds[colour + mat.A] = materialId = material.Id;
                }
                part["material"] = materialId;
            }
            return body;
        }

        readonly Dictionary<string, string> glassIds = [];

        /// <summary>
        /// What the file defines an item by (IsDefinedBy: its property sets with their HasProperties, its quantity sets with their
        /// Quantities, its type) and associates it with (HasAssociations: its materials): each set under its name in
        /// <paramref name="properties"/>, the type's and materials' names with the item's own attributes.
        /// </summary>
        void Definitions(uint local, JsonObject attributes, JsonObject properties)
        {
            var rels = model.RelationsOf(local);
            foreach (var def in rels.GetValueOrDefault("IsDefinedBy") ?? [])
            {
                if (!itemOf.TryGetValue(def, out var d) || d >= model.Categories.Length) continue;
                var category = model.Categories[d];
                var defAttrs = model.AttributesOf(d);
                if (category.EndsWith("TYPE", StringComparison.Ordinal))
                {
                    if (Text(defAttrs, "Name") is { Length: > 0 } typeName) attributes["Type"] = typeName;
                    continue;
                }
                if (category is not ("IFCPROPERTYSET" or "IFCELEMENTQUANTITY") || Text(defAttrs, "Name") is not { Length: > 0 } setName) continue;
                // A part's set (a lintel's, a layer's) bears the element's set name; its description, the part, keeps it apart.
                if (properties.ContainsKey(setName) && Text(defAttrs, "Description") is { Length: > 0 } part) setName = $"{setName} ({part})";
                var set = properties[setName] as JsonObject ?? new JsonObject();
                var defRels = model.RelationsOf(def);
                foreach (var v in (defRels.GetValueOrDefault("HasProperties") ?? []).Concat(defRels.GetValueOrDefault("Quantities") ?? []))
                {
                    if (!itemOf.TryGetValue(v, out var vi)) continue;
                    var va = model.AttributesOf(vi);
                    if (Text(va, "Name") is not { Length: > 0 } name) continue;
                    var value = va.FirstOrDefault(kv => kv.Key is "NominalValue" or "LengthValue" or "AreaValue" or "VolumeValue" or "CountValue" or "WeightValue" or "TimeValue").Value;
                    if (value is JsonValue) set[name] = value.DeepClone();
                }
                if (set.Count > 0) properties[setName] = set;
            }
            var materials = new List<string>();
            foreach (var a in rels.GetValueOrDefault("HasAssociations") ?? []) MaterialNames(a, materials, 0);
            if (materials.Count > 0) attributes["Material"] = string.Join(", ", materials.Distinct());
        }

        /// <summary>The material names under an association: a material's own, a layer set's layers', a usage's set's.</summary>
        void MaterialNames(uint local, List<string> names, int depth)
        {
            if (depth > 4 || !itemOf.TryGetValue(local, out var i) || i >= model.Categories.Length) return;
            var attrs = model.AttributesOf(i);
            if (model.Categories[i] == "IFCMATERIAL") { if (Text(attrs, "Name") is { Length: > 0 } n) names.Add(n); return; }
            foreach (var (_, ids) in model.RelationsOf(local))
                foreach (var id in ids)
                    if (itemOf.TryGetValue(id, out var j) && j < model.Categories.Length && model.Categories[j].StartsWith("IFCMATERIAL", StringComparison.Ordinal)) MaterialNames(id, names, depth + 1);
        }

        /// <summary>Each local id's index in the file's lists.</summary>
        Dictionary<uint, int> itemOf = [];

        /// <summary>
        /// A shell's profiles as triangles: a triangle is itself; any other profile, a polygon that may be concave and
        /// have holes (the official importer writes a wall's side as one polygon, its window a hole), triangulated in its plane.
        /// </summary>
        static List<(int, int, int)> Triangles(FragShell shell, List<Vec3> vertices)
        {
            var n = vertices.Count;
            var holesOf = new Dictionary<int, List<int[]>>();
            foreach (var (indices, profile) in shell.Holes)
                if (indices.Length >= 3 && indices.All(k => k >= 0 && k < n)) (holesOf.TryGetValue(profile, out var l) ? l : holesOf[profile] = []).Add(indices);
            var tris = new List<(int, int, int)>();
            for (var p = 0; p < shell.Profiles.Length; p++)
            {
                var profile = shell.Profiles[p];
                if (profile.Length < 3 || profile.Any(k => k < 0 || k >= n)) continue;
                var holes = holesOf.GetValueOrDefault(p);
                if (profile.Length == 3 && holes == null) { tris.Add((profile[0], profile[1], profile[2])); continue; }
                // The polygon's normal by Newell's rule, as the library takes it.
                var normal = new Vec3(0, 0, 0);
                for (var k = 0; k < profile.Length; k++)
                {
                    Vec3 a = vertices[profile[k]], b = vertices[profile[(k + 1) % profile.Length]];
                    normal += new Vec3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
                }
                if (normal.Length < 1e-14) continue;
                tris.AddRange(Triangulator.Face(vertices, profile, holes?.ToArray(), normal.Normalized()));
            }
            return tris;
        }

        /// <summary>An instance's twelve numbers in the model's frame: the images of the axes and of the origin under the file's global transform.</summary>
        static double[] Xf(FragTransform g, FragTransform? coordinates)
        {
            (double X, double Y, double Z) At(double x, double y, double z)
            {
                var p = g.Apply(x, y, z);
                return coordinates is { } c ? c.Unapply(p.X, p.Y, p.Z) : p;
            }
            var o = new double[12];
            // The model's axes, taken to the file's frame, placed (as directions), brought back.
            Vec3[] axes = [new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)];
            var zero = At(0, 0, 0);
            for (var k = 0; k < 3; k++)
            {
                var f = ToFrag(axes[k]);
                var placed = At(f.X, f.Y, f.Z);
                var dir = ToModel((placed.X - zero.X, placed.Y - zero.Y, placed.Z - zero.Z));
                o[k * 3] = dir.X; o[k * 3 + 1] = dir.Y; o[k * 3 + 2] = dir.Z;
            }
            var origin = ToModel(zero);
            o[9] = origin.X; o[10] = origin.Y; o[11] = origin.Z;
            return o;
        }

        string? GuidOf(uint local) => model.GuidOf(local) is { Length: 22 } g ? g : null;

        /// <summary>A GlobalId the same for the same file and key, so an import done again finds what it made.</summary>
        string DerivedId(string key)
        {
            var bytes = Md5.HashData(Encoding.UTF8.GetBytes((model.Guid ?? name) + "\n" + key));
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

/// <summary>What of a Fragments file comes in: its kinds and storeys as <see cref="FragSurvey"/> names them (null: all; "" the items on none), and whether as one group.
/// <paramref name="Fidelity"/>: how what is recognised is measured against the file's triangles (docs/spec-ifc.md, Poarta de fidelitate); null, enforced.</summary>
public sealed record FragImportOptions(IReadOnlySet<string>? Kinds = null, IReadOnlySet<string>? Storeys = null, bool Group = true, bool Editable = false, bool Recognize = false,
    FidelityOptions? Fidelity = null);
