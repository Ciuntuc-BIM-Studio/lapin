using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Ifc;

/// <summary>How a file is read: "auto", what carries Lapin's intent is rebuilt natively and the rest comes as reference; "reference", everything as reference.</summary>
/// <summary>
/// How a file is imported: <see cref="Mode"/> auto (Lapin's own natively, the rest recognised or referenced), reference
/// (everything as it is) or link; and what of it, as the user chose in the survey (<see cref="IfcSurvey"/>): the
/// <see cref="Kinds"/> (a class, or a class with its category) and the <see cref="Storeys"/> (their GlobalIds, "" for
/// elements on no storey) to bring, null for all; and the 2D: <see cref="Plan2d"/>, the elements' own 2D for plans
/// (their symbols, footprints, fills and texts; without it a plan shows their cut), and <see cref="Only2d"/>, the
/// elements the file draws only in 2D (lines, texts, symbols with no body), both brought unless the user unticks them.
/// <para>
/// And the import's rules (docs/spec-ifc.md, Regulile importului), general, whatever program wrote the file:
/// <see cref="Dedupe"/> keeps a body the file repeats (the same triangles, wherever it stands) once, its
/// elements instances of it, nothing lost (on unless unticked); <see cref="Flat"/> leaves out the bodies that lie
/// in one plane (texts, lines and hatches another program turned into triangles); <see cref="FarAway"/> the
/// elements further than that many metres from the rest (a drawing's texts, a title block); <see cref="Duplicates"/>
/// an element with the same body in the same place as one already brought; <see cref="Pseudo"/> the ports,
/// openings and virtual elements another program wrote as objects; <see cref="Simplify"/> simplifies the meshes
/// within that many metres (<see cref="Simplifier"/>); <see cref="Numbers"/> reads the numbers written as text
/// (<see cref="IfcValueText"/>); <see cref="SkipSets"/> are the property and quantity sets left out by name.
/// </para>
/// </summary>
/// <summary><paramref name="Group"/>: what the file places comes in as one group named after it (docs/groups.md); false for a link, and for an import the journal recorded before groups were.</summary>
public sealed record IfcImportOptions(string Mode = "auto", IReadOnlySet<string>? Kinds = null, IReadOnlySet<string>? Storeys = null, bool Plan2d = true, bool Only2d = true,
    bool Dedupe = true, bool Flat = false, double? FarAway = null, bool Duplicates = false, bool Pseudo = false, double Simplify = 0, bool Numbers = false, IReadOnlySet<string>? SkipSets = null, bool Group = false)
{
    public bool ReferenceOnly => string.Equals(Mode, "reference", StringComparison.OrdinalIgnoreCase);
    public bool Filtered => Kinds != null || Storeys != null;
    /// <summary>Whether a set of this name comes in.</summary>
    public bool Keeps(string? set) => SkipSets == null || set == null || !SkipSets.Contains(set);
}

public sealed record IfcImportResult(int Elements, int Updated, int References, int Storeys, IReadOnlyList<string> Notes, int Recognized = 0, IReadOnlyDictionary<string, int>? RecognizedByClass = null, IReadOnlyDictionary<string, int>? ReferencesByClass = null, IReadOnlyDictionary<string, int>? Skipped = null, string? LinkId = null)
{
    public JsonObject ToJson() => new()
    {
        ["elements"] = Elements, ["updated"] = Updated, ["references"] = References, ["storeys"] = Storeys, ["recognized"] = Recognized, ["linkId"] = LinkId,
        ["recognizedByClass"] = Counts(RecognizedByClass), ["referencesByClass"] = Counts(ReferencesByClass), ["skipped"] = Counts(Skipped),
        ["notes"] = new JsonArray(Notes.Select(n => (JsonNode)n).ToArray()),
    };

    static JsonObject Counts(IReadOnlyDictionary<string, int>? d)
    {
        var o = new JsonObject();
        if (d != null) foreach (var (k, v) in d.OrderByDescending(kv => kv.Value)) o[k] = v;
        return o;
    }
}

/// <summary>
/// An IFC file read into the open project, as one undo step. What Lapin (or
/// BubbleBIM) wrote with <c>Lapin_Intent</c> (<c>Pset_Lapin_Intent</c> in files written before 25.10.2026) is rebuilt natively: the
/// storeys, materials, types with their layer sets, the elements with their
/// attributes, their openings, doors and windows, usages, joins and phases;
/// an element already in the project (the same GlobalId) is updated, what
/// the project has and the file has not stays. Anything else with a body is
/// kept as a reference: its class and its triangles, shown and measured,
/// never edited (docs/spec-ifc.md, Importul).
/// </summary>
public static partial class IfcImport
{
    public static IfcImportResult Run(Session session, string path, IfcImportOptions? options, out Diff diff) =>
        Run(session, File.ReadAllBytes(path), System.IO.Path.GetFileName(path), options, out diff);

    /// <summary>An IFC file's bytes into the open project (<paramref name="name"/>: the file's name, for the undo label).</summary>
    public static IfcImportResult Run(Session session, byte[] bytes, string name, IfcImportOptions? options, out Diff diff)
    {
        session.Progress?.Invoke("Indexing the file", 0, 0);
        return Run(session, StepFile.Read(bytes), name, options, out diff);
    }

    /// <summary>A read IFC file into the open project (the survey read it already).</summary>
    public static IfcImportResult Run(Session session, StepFile file, string name, IfcImportOptions? options, out Diff diff)
    {
        var o = options ?? new IfcImportOptions();
        var reader = new Reader(session, file, o);
        diff = session.Edit($"Import {name}", edit =>
        {
            if (o.Group) Groups.Around(edit, System.IO.Path.GetFileNameWithoutExtension(name), reader.Apply);
            else reader.Apply(edit);
        });
        return reader.Result();
    }

    /// <summary>
    /// An IFC file as a link (Manage → Links): imported like any other (its
    /// elements recognised natively where they can be), then everything it
    /// brought marked as the link's, read-only. With <paramref name="existing"/>
    /// the link is reloaded: what it held goes first. One undo step.
    /// </summary>
    public static IfcImportResult Link(Session session, byte[] bytes, string name, string? path, IfcImportOptions? options, out Diff diff, string? existing = null)
    {
        session.Progress?.Invoke("Indexing the file", 0, 0);
        return Link(session, StepFile.Read(bytes), name, path, options, out diff, existing);
    }

    /// <summary>A read IFC file as a link (the survey read it already).</summary>
    public static IfcImportResult Link(Session session, StepFile file, string name, string? path, IfcImportOptions? options, out Diff diff, string? existing = null)
    {
        var reader = new Reader(session, file, options ?? new IfcImportOptions());
        string? linkId = null;
        diff = session.Edit(existing != null ? $"Reload {name}" : $"Link {name}", edit =>
        {
            Entity link;
            if (existing != null) { link = Links.Require(edit.Model, existing); Links.RemoveElements(edit, link.Id); }
            else link = Links.Create(edit, "ifc", name, path);
            var before = edit.Model.Entities.Select(e => e.Id).ToHashSet();
            reader.Apply(edit);
            Links.Tag(edit, link.Id, edit.Model.Entities.Select(e => e.Id).Where(id => !before.Contains(id)).ToList());
            linkId = link.Id;
        });
        var r = reader.Result();
        return r with { Notes = [.. r.Notes, $"Linked as {linkId}."], LinkId = linkId };
    }

    /// <summary>What an intent set says: the attributes, and the Lapin class, predefined type and host it names.</summary>
    sealed record Intent(JsonObject Attrs, string? Class, string? Predefined, string? Host, string? LapinId);

    /// <summary>An element of the file: a rooted product with a placement or a shape, not the spatial structure, an opening, an annotation, a space (those are read apart) or a port (carried with its element).</summary>
    public static bool IsProductEntity(StepEntity e) =>
        e.Args.Count >= 7 && e[0] is string { Length: 22 } && !e.Type.StartsWith("IFCREL", StringComparison.Ordinal) && !e.Type.EndsWith("TYPE", StringComparison.Ordinal)
        && (e[5] is StepRef || e[6] is StepRef)
        && e.Type is not ("IFCPROJECT" or "IFCSITE" or "IFCBUILDING" or "IFCBUILDINGSTOREY" or "IFCOPENINGELEMENT" or "IFCANNOTATION" or "IFCSPACE" or "IFCDISTRIBUTIONPORT");

    sealed partial class Reader(Session s, StepFile f, IfcImportOptions o)
    {
        readonly IfcModel model = new(f);
        readonly List<string> notes = [];
        int elements, updated, references, storeys;
        readonly Dictionary<int, List<int>> definitions = [];     // product → its property sets
        readonly Dictionary<int, int> containedIn = [];            // product → its spatial structure
        readonly Dictionary<int, string> storeyOf = [];            // file storey → model storey
        readonly Dictionary<int, string> typeIds = [], layerSets = [], materialIds = [];
        readonly HashSet<string> seen = [];
        readonly List<Action<IEdit>> relations = [];               // written after every entity is there
        readonly Dictionary<string, string> contained = [];        // element → the storey it joins (WriteContainment)
        readonly HashSet<int> sketched = [];                       // Studio solids: Lapin's, built from a sketch the file has not (they come as references)

        static readonly Dictionary<string, string> ProperNames = BuildNames();

        public IfcImportResult Result() => new(elements, updated, references, storeys, notes, recognized, recognizedByClass, referencesByClass, recognition?.Skipped);

        public void Apply(IEdit edit)
        {
            foreach (var r in f.OfType("IFCRELDEFINESBYPROPERTIES"))
                foreach (var p in Refs(r[4])) (definitions.TryGetValue(p, out var l) ? l : definitions[p] = []).Add(Ref(r[5]));
            foreach (var r in f.OfType("IFCRELCONTAINEDINSPATIALSTRUCTURE"))
                foreach (var p in Refs(r[4])) containedIn[p] = Ref(r[5]);
            // A part aggregated under an element (a box's wall under its assembly) stands where its whole does; twice, for a part of a part.
            var aggregates = f.OfType("IFCRELAGGREGATES").Where(r => f.Get(r[4]) is { } whole && whole.Type is not ("IFCPROJECT" or "IFCSITE" or "IFCBUILDING" or "IFCBUILDINGSTOREY")).ToList();
            for (var pass = 0; pass < 2; pass++)
                foreach (var r in aggregates)
                    if (containedIn.TryGetValue(Ref(r[4]), out var sp))
                        foreach (var part in Refs(r[5])) containedIn.TryAdd(part, sp);
            // Rooms are aggregated into their storey, not contained.
            foreach (var r in f.OfType("IFCRELAGGREGATES"))
                if (f.Get(r[4]) is { Type: "IFCBUILDINGSTOREY" } storey)
                    foreach (var p in Refs(r[5])) if (f.Entities.GetValueOrDefault(p) is { Type: "IFCSPACE" }) containedIn[p] = storey.Id;
            Project(edit);
            Storeys(edit);
            if (!o.ReferenceOnly)
            {
                Materials(edit);
                Types(edit);
            }
            Elements(edit);
            foreach (var r in relations) r(edit);
            WriteContainment(edit);
            var left = edit.Model.Entities.Count(e => s.Catalog.Geometry(e.Class) != null && !seen.Contains(e.Id) && e.Class is not (IfcClass.OpeningElement or IfcClass.Annotation or IfcClass.Grid));
            if (left > 0 && elements + updated > 0) notes.Add($"{left} element(s) of the project are not in the file; they stay as they are.");
        }

        // ─── Reading ────────────────────────────────────────────────────────

        static string? Str(object? v) => IfcModel.Str(v);
        static double Num(object? v) => IfcModel.Num(v);
        static int Ref(object? v) => v is StepRef r ? r.Id : -1;
        static IEnumerable<int> Refs(object? v) => v is List<object?> l ? l.OfType<StepRef>().Select(r => r.Id) : [];
        static string? Enum(object? v) => (v as StepEnum)?.Value;
        List<StepEntity> Items(object? v) => v is List<object?> l ? [.. l.Select(f.Get).OfType<StepEntity>()] : [];

        static Dictionary<string, string> BuildNames()
        {
            var d = new Dictionary<string, string>();
            foreach (var field in typeof(IfcClass).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.GetValue(null) is string name) d[name.ToUpperInvariant()] = name;
            foreach (var name in new[] { "IfcBuildingElementPart", "IfcMember", "IfcPlate", "IfcCovering", "IfcRailing", "IfcStair", "IfcRamp", "IfcChimney", "IfcFurnishingElement", "IfcPipeSegment", "IfcDiscreteAccessory", "IfcElementAssembly", "IfcCurtainWall", "IfcStairFlight", "IfcRampFlight", "IfcPile", "IfcWallStandardCase", "IfcSpace", "IfcMemberType", "IfcPlateType", "IfcCoveringType", "IfcRailingType", "IfcStairType", "IfcRampType", "IfcBuildingElementProxyType" })
                d[name.ToUpperInvariant()] = name;
            return d;
        }

        static string ClassName(string upper) => ProperNames.TryGetValue(upper, out var n) ? n : IfcModel.ClassName(upper);

        /// <summary>The file's intent set on an entity: through IfcRelDefinesByProperties, or a type's own HasPropertySets, or a material's properties.</summary>
        Intent? IntentOf(int id, object? hasPropertySets = null)
        {
            var sets = Items(hasPropertySets).Select(x => x.Id).Concat(definitions.GetValueOrDefault(id) ?? []);
            foreach (var sid in sets)
                if (f.Entities.GetValueOrDefault(sid) is { Type: "IFCPROPERTYSET" } set && IfcExport.IsIntentSet(Str(set[2]))) return Parse(Items(set[4]));
            return null;
        }

        Intent Parse(IEnumerable<StepEntity> properties)
        {
            var attrs = new JsonObject();
            string? cls = null, pre = null, host = null, lapinId = null;
            foreach (var p in properties)
            {
                if (p.Type != "IFCPROPERTYSINGLEVALUE" || Str(p[0]) is not { } name) continue;
                var value = Value(p[2]);
                switch (name)
                {
                    case "LapinClass": cls = value as string; break;
                    case "LapinPredefinedType": pre = value as string; break;
                    case "LapinHost": host = value as string; break;
                    case "LapinId": lapinId = value as string; break;
                    case "LapinVersion": break;
                    default: attrs[name] = value switch { JsonNode n => n, bool b => b, double d => d, string t => t, _ => null }; break;
                }
            }
            return new Intent(attrs, cls, pre, host, lapinId);
        }

        /// <summary>A property's value as Lapin held it: a bool, a number, text, or the JSON a list or object was written as.</summary>
        static object? Value(object? v) => v switch
        {
            StepTyped { Type: "IFCBOOLEAN", Value: StepEnum e } => e.Value == "T",
            StepTyped { Type: "IFCLOGICAL", Value: StepEnum e } => e.Value == "T",
            StepTyped { Type: "IFCTEXT", Value: string t } => Json(t),
            StepTyped t => Value(t.Value),
            double d => d,
            long l => (double)l,
            string str => str,
            StepEnum e => e.Value,
            _ => null,
        };

        static object? Json(string text)
        {
            try { return JsonNode.Parse(text); }
            catch (JsonException) { return text; }
        }

        static bool IsProduct(StepEntity e) => IsProductEntity(e);

        Func<int, int>? storeyOfProduct;

        /// <summary>What the user chose to bring (the import's options): this element's kind and the storey it stands on (up its space or its whole, <see cref="IfcSurvey.StoreyResolver"/>); everything without a choice.</summary>
        bool Takes(StepEntity e, IReadOnlyDictionary<int, int>? parentOf = null)
        {
            if (!o.Filtered) return true;
            if (o.Kinds != null && !o.Kinds.Contains(IfcSurvey.KindOf(e))) return false;
            if (o.Storeys == null) return true;
            var sp = (storeyOfProduct ??= IfcSurvey.StoreyResolver(f))(e.Id);
            var gid = sp != 0 && f.Entities.TryGetValue(sp, out var storey) ? Str(storey[0]) ?? "" : "";
            return o.Storeys.Contains(gid);
        }

        void Report(string stage, int done, int total) => s.Progress?.Invoke(stage, done, total);

        /// <summary>Progress told every hundredth of <paramref name="total"/>, and at the end.</summary>
        static bool Due(int done, int total) => total > 0 && (done == total || done % Math.Max(1, total / 100) == 0);

        // ─── Writing ────────────────────────────────────────────────────────

        /// <summary>An entity put as the file says: the one with the same id updated (its class changed if need be), else added.</summary>
        Entity Put(IEdit edit, string id, string cls, string name, JsonObject attrs, string? predefined, bool counts = true)
        {
            seen.Add(id);
            if (edit.Model.Get(id) is { } existing)
            {
                if (existing.Class == cls)
                {
                    var e = existing.With(name: name, attrs: attrs, predefinedType: predefined);
                    edit.Replace(e);
                    if (counts) updated++;
                    return e;
                }
                edit.Remove(id);
            }
            var made = new Entity(id, cls, name, attrs, predefined);
            edit.Add(made);
            if (counts) elements++;
            return made;
        }

        /// <summary>A relationship as the model keeps it: typing and material associations grouped by what relates (one relation per type), the others one each, with the file's id.</summary>
        void Relate(IEdit edit, string? id, string cls, string relating, string related, JsonObject? attrs = null)
        {
            if (edit.Model.Get(relating) == null || edit.Model.Get(related) == null) return;
            if (edit.Model.RelationsOf(relating, cls).Any(r => r.Relating == relating && r.Related.Contains(related))) return;
            if (cls is IfcClass.RelDefinesByType or IfcClass.RelAssociatesMaterial) { edit.Relate(cls, relating, related); return; }
            var rid = id != null && GlobalId.IsValid(id) && edit.Model.GetRelation(id) == null ? id : edit.NewId();
            edit.AddRelation(new Relation(rid, cls, relating, [related], attrs));
        }

        void Contain(IEdit edit, string elementId, string? storeyId)
        {
            storeyId ??= Spatial.Storeys(edit.Model).FirstOrDefault()?.Id;
            if (storeyId == null) return;
            var now = Spatial.StoreyOf(edit.Model, elementId);
            if (now?.Id == storeyId) return;
            if (now != null) edit.Unrelate(IfcClass.RelContainedInSpatialStructure, elementId);
            contained[elementId] = storeyId;
        }

        /// <summary>
        /// The elements gathered by <see cref="Contain"/> joined to their storeys, each storey's
        /// relationship written once: one at a time, it was copied and indexed again for every
        /// element (quadratic: a million elements never finished).
        /// </summary>
        void WriteContainment(IEdit edit)
        {
            foreach (var group in contained.GroupBy(c => c.Value, c => c.Key))
            {
                var own = edit.Model.RelationsOf(group.Key, IfcClass.RelContainedInSpatialStructure).FirstOrDefault(r => r.Relating == group.Key);
                if (own == null) edit.AddRelation(new Relation(edit.NewId(), IfcClass.RelContainedInSpatialStructure, group.Key, group.ToList()));
                else edit.ReplaceRelation(own with { Related = [.. own.Related, .. group] });
            }
            contained.Clear();
        }

        string? StoreyFor(int product) => containedIn.TryGetValue(product, out var sp) && storeyOf.TryGetValue(sp, out var id) ? id : null;

        void Project(IEdit edit)
        {
            if (o.ReferenceOnly || f.OfType("IFCPROJECT").FirstOrDefault() is not { } p || IntentOf(p.Id) is not { } intent) return;
            var project = Spatial.Project(edit.Model) ?? Spatial.Project(edit.Model);
            if (project == null) { Spatial.EnsureBuilding(edit); project = Spatial.Project(edit.Model)!; }
            var attrs = project.CloneAttrs();
            foreach (var (k, v) in intent.Attrs) attrs[k] = v?.DeepClone();
            edit.Replace(project.With(name: Str(p[2]) ?? project.Name, attrs: attrs));
        }

        /// <summary>The file's storeys: the project's by id, else by name, else by elevation; new ones added under the building.</summary>
        void Storeys(IEdit edit)
        {
            var building = Spatial.EnsureBuilding(edit);
            var matched = new HashSet<string>();
            foreach (var st in f.OfType("IFCBUILDINGSTOREY"))
            {
                var gid = Str(st[0]) ?? "";
                var name = Str(st[2]) ?? Str(st[7]) ?? "Level";
                var z = Length(st[9]);
                var existing = Spatial.Storeys(edit.Model).Where(x => !matched.Contains(x.Id)).ToList();
                var match = edit.Model.Get(gid) is { Class: IfcClass.BuildingStorey } byId ? byId
                    : existing.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
                    ?? existing.FirstOrDefault(x => Math.Abs(x.Num("elevation") - z) < 1e-6);
                var attrs = (o.ReferenceOnly ? null : IntentOf(st.Id)?.Attrs) ?? match?.CloneAttrs() ?? new JsonObject();
                attrs["elevation"] = z;
                if (attrs["height"] == null) attrs["height"] = 3.0;
                if (match != null)
                {
                    edit.Replace(match.With(name: name, attrs: attrs));
                    storeyOf[st.Id] = match.Id;
                    matched.Add(match.Id);
                    continue;
                }
                var made = new Entity(GlobalId.IsValid(gid) && edit.Model.Get(gid) == null ? gid : edit.NewId(), IfcClass.BuildingStorey, name, attrs);
                edit.Add(made);
                edit.Relate(IfcClass.RelAggregates, building.Id, made.Id);
                storeyOf[st.Id] = made.Id;
                matched.Add(made.Id);
                storeys++;
            }
        }

        /// <summary>A length in metres, clean of the noise a unit conversion leaves (2.8000000000000003 from millimetres).</summary>
        double Length(object? v) => Math.Round(Num(v) * model.Scale, 9);

        /// <summary>The file's materials: those with a Lapin id keep it (what refers to them still holds); the others by name.</summary>
        void Materials(IEdit edit)
        {
            foreach (var mat in f.OfType("IFCMATERIAL"))
            {
                var name = Str(mat[0]) ?? "Material";
                var props = f.OfType("IFCMATERIALPROPERTIES").FirstOrDefault(p => Ref(p[3]) == mat.Id && IfcExport.IsIntentSet(Str(p[0])));
                var intent = props == null ? null : Parse(Items(props[2]));
                string id;
                if (intent?.LapinId is { } lapinId && GlobalId.IsValid(lapinId))
                {
                    var attrs = intent.Attrs;
                    if (attrs["color"] == null && Colour(mat.Id) is { } c) attrs["color"] = c;
                    id = Put(edit, lapinId, IfcClass.Material, name, attrs, null, counts: false).Id;
                }
                else
                {
                    var found = Lapin.Platform.Materials.Ensure(edit, name, Colour(mat.Id));
                    id = found.Id;
                }
                materialIds[mat.Id] = id;
            }
        }

        /// <summary>A material's colour from its representation (a styled item with a surface style).</summary>
        string? Colour(int material)
        {
            foreach (var rep in f.OfType("IFCMATERIALDEFINITIONREPRESENTATION").Where(r => Ref(r[3]) == material))
                foreach (var styled in Items(rep[2]).SelectMany(r => Items(r[3])).Where(i => i.Type == "IFCSTYLEDITEM"))
                    foreach (var style in Items(styled[1]).Where(x => x.Type == "IFCSURFACESTYLE"))
                        foreach (var shading in Items(style[2]))
                            if (f.Get(shading[0]) is { Type: "IFCCOLOURRGB" } rgb)
                                return $"#{(int)Math.Round(Num(rgb[1]) * 255):x2}{(int)Math.Round(Num(rgb[2]) * 255):x2}{(int)Math.Round(Num(rgb[3]) * 255):x2}";
            return null;
        }

        string MaterialId(IEdit edit, int fileMaterial)
        {
            if (materialIds.TryGetValue(fileMaterial, out var id)) return id;
            var name = Str(f.Entities.GetValueOrDefault(fileMaterial)?[0]) ?? "Material";
            return materialIds[fileMaterial] = Lapin.Platform.Materials.Ensure(edit, name, null).Id;
        }

        /// <summary>A layer set as the model's entity, its id derived from its owner's (a type) so a second import finds it.</summary>
        string LayerSet(IEdit edit, int fileSet, string ownerId)
        {
            if (layerSets.TryGetValue(fileSet, out var id)) return id;
            var set = f.Entities[fileSet];
            var layers = Items(set[0]).Select(l => new MaterialLayer(MaterialId(edit, Ref(l[0])), Length(l[1]), Str(l[5]) == "Core", Str(l[3]))).ToList();
            id = Step.Guid(ownerId, "layerset");
            Put(edit, id, IfcClass.MaterialLayerSet, Str(set[1]) ?? "Layers", Layers.Write(layers), null, counts: false);
            return layerSets[fileSet] = id;
        }

        void Types(IEdit edit)
        {
            foreach (var t in f.Rooted.Where(e => e.Type.EndsWith("TYPE", StringComparison.Ordinal) && e.Args.Count >= 9 && e[0] is string { Length: 22 }))
            {
                if (IntentOf(t.Id, t[5]) is not { } intent) continue;
                // A door's or window's mirrored type (written for elements flipped on one axis) is the type itself.
                var gid = intent.Attrs["LapinMirrorOf"] is JsonValue mirror && mirror.TryGetValue<string>(out var of) && of.Length > 0 ? of : Str(t[0])!;
                intent.Attrs.Remove("LapinMirrorOf");
                var cls = intent.Class ?? ClassName(t.Type);
                var typeName = Str(t[2]) ?? cls[3..];
                if (gid != Str(t[0]) && typeName.EndsWith(" (mirrored)", StringComparison.Ordinal)) typeName = typeName[..^" (mirrored)".Length];
                Put(edit, gid, cls, typeName, intent.Attrs, intent.Predefined ?? Enum(t[9]), counts: false);
                typeIds[t.Id] = gid;
                foreach (var rel in f.OfType("IFCRELASSOCIATESMATERIAL").Where(r => Refs(r[4]).Contains(t.Id)))
                    if (f.Get(rel[5]) is { Type: "IFCMATERIALLAYERSET" } set)
                    {
                        var setId = LayerSet(edit, set.Id, gid);
                        var relId = Str(rel[0]);
                        relations.Add(e => Relate(e, relId, IfcClass.RelAssociatesMaterial, setId, gid));
                    }
            }
        }

        void Elements(IEdit edit)
        {
            // Parts aggregated under an element that is rebuilt natively are its own again; under a foreign one they are what carries the body.
            var parentOf = new Dictionary<int, int>();
            foreach (var r in f.OfType("IFCRELAGGREGATES"))
                if (f.Get(r[4]) is { } whole && whole.Type is not ("IFCPROJECT" or "IFCSITE" or "IFCBUILDING" or "IFCBUILDINGSTOREY"))
                    foreach (var part in Refs(r[5])) parentOf[part] = whole.Id;
            var booleans = s.Catalog.Get<ISolidBooleans>();
            var fills = new HashSet<int>(f.OfType("IFCRELFILLSELEMENT").Select(r => Ref(r[5])));
            // What a module rebuilds from the whole file (alignments: outside the storeys), taken before the elements; an alignment's
            // segments are never references of their own (without a module that takes them, the alignment comes as one by its axis).
            var taken = new HashSet<int>();
            if (!o.ReferenceOnly && s.Catalog.All<IIfcFileRecognizer>() is { Count: > 0 } fileRecognizers && DefaultStorey(edit) is { } storey)
            {
                if (recognition == null) PrepareRecognition();
                foreach (var r in fileRecognizers) taken.UnionWith(r.Recognize(edit, f, storey, recognition!));
            }
            var products = f.Rooted.Where(IsProduct).Where(e => !taken.Contains(e.Id) && e.Type != "IFCALIGNMENTSEGMENT" && Takes(e, parentOf)).OrderBy(e => e.Id).ToList();
            // First what is rebuilt natively (Lapin's intent, or recognised); then, as references, the rest: not the
            // parts of a native element (its body is its own), nor a door or window made with its wall.
            var native = new HashSet<int>();
            var rest = new List<StepEntity>();
            // What the recognisers will be offered, read from the file on every core first.
            if (!o.ReferenceOnly)
                PrepareForeign(edit, [.. products.Where(e => !fills.Contains(e.Id) && !parentOf.ContainsKey(e.Id) && IntentOf(e.Id) == null)]);
            var seenProducts = 0;
            foreach (var e in products)
            {
                if (Due(++seenProducts, products.Count)) Report("Recognising the elements", seenProducts, products.Count);
                var intent = o.ReferenceOnly ? null : IntentOf(e.Id);
                // A reference Lapin wrote is one again, from the body the file gives it (its intent carries no body).
                if (intent != null && (bool?)intent.Attrs["reference"] == true) { rest.Add(e); continue; }
                // A Studio solid (docs/plan-studio-thick.md) is built from a sketch the file does not carry: it comes back as a
                // reference with the body the file gives it, under its class, with its type and its assembly; the project it came
                // from keeps its own, live solid.
                if (intent != null && Sketched(intent))
                {
                    if (edit.Model.Get(Str(e[0]) ?? "") is { } own && own.Str("operation") != null) { seen.Add(own.Id); continue; }
                    sketched.Add(e.Id);
                    rest.Add(e);
                    continue;
                }
                if (intent != null) { Native(edit, e, intent); native.Add(e.Id); continue; }
                if (fills.Contains(e.Id)) { rest.Add(e); continue; } // decided by its wall, below
                if (!o.ReferenceOnly && !parentOf.ContainsKey(e.Id) && Recognize(edit, e)) { native.Add(e.Id); continue; }
                rest.Add(e);
            }
            // The references to come, read from the file on every core before they are put in the model in order.
            PrepareAll([.. rest.Where(e => !(parentOf.TryGetValue(e.Id, out var parent) && (native.Contains(parent) || (!o.ReferenceOnly && IntentOf(parent) != null)))
                && !recognizedFills.Contains(e.Id) && (o.ReferenceOnly || !parentOf.ContainsKey(e.Id) || fills.Contains(e.Id)))], booleans);
            var seenRest = 0;
            foreach (var e in rest)
            {
                if (Due(++seenRest, rest.Count)) Report("Bringing the references", seenRest, rest.Count);
                if (parentOf.TryGetValue(e.Id, out var parent) && (native.Contains(parent) || (!o.ReferenceOnly && IntentOf(parent) != null))) continue;
                if (recognizedFills.Contains(e.Id)) continue;
                if (!o.ReferenceOnly && parentOf.ContainsKey(e.Id) && !fills.Contains(e.Id) && !sketched.Contains(e.Id) && Recognize(edit, e)) continue;
                Reference(edit, e, booleans);
            }
            Spaces(edit);
            FinishRecognition(edit);
            RuleNotes();
            if (left2d > 0) notes.Add($"{left2d} element(s) drawn only in 2D left out, as chosen.");
            if (without2d > 0) notes.Add($"{without2d} element(s) brought without their own 2D, as chosen: plans draw their cut.");
            foreach (var (cls, n) in bodiless.OrderByDescending(kv => kv.Value))
                notes.Add($"{n} × {cls} left out: no body Lapin can read (nor 2D for plans).");
            if (with2d > 0) notes.Add($"{with2d} reference(s) brought their own 2D for plans; plans draw it instead of their cut (a view's style can turn it off).");
        }

        int with2d, left2d, without2d;

        /// <summary>Whether the intent is a Studio solid's: an operation on a sketch (its profile, or the box a bottom or top belongs to).</summary>
        static bool Sketched(Intent intent) =>
            intent.Attrs["operation"] is JsonValue && (intent.Attrs["profile"] is JsonValue || intent.Attrs["box"] is JsonValue);

        /// <summary>
        /// An assembly without a body of its own (a Studio box with its bottom and top): an IfcElementAssembly entity in its
        /// storey, aggregating the parts the file gives it (references by now), so the box reads as one thing again. The project
        /// it came from keeps its own assembly.
        /// </summary>
        bool Assembly(IEdit edit, StepEntity e, string gid)
        {
            var parts = f.OfType("IFCRELAGGREGATES").Where(r => Ref(r[4]) == e.Id).SelectMany(r => Refs(r[5]))
                .Select(id => f.Entities.GetValueOrDefault(id)).OfType<StepEntity>().Select(p => Str(p[0])).OfType<string>().ToList();
            if (parts.Count == 0) return false;
            if (edit.Model.Get(gid) is { } own && own.Attrs["reference"] == null) { seen.Add(gid); return true; }
            var attrs = new JsonObject { ["reference"] = true, ["ifcClass"] = "IfcElementAssembly", ["objectType"] = Str(e[4]) };
            var predefined = e.Args.Count > 8 ? Enum(e[8]) : null;
            Put(edit, gid, "IfcElementAssembly", Str(e[2]) ?? "Assembly", attrs, predefined, counts: false);
            references++;
            referencesByClass["IfcElementAssembly"] = referencesByClass.GetValueOrDefault("IfcElementAssembly") + 1;
            var storey = StoreyFor(e.Id);
            relations.Add(x => Contain(x, gid, storey));
            foreach (var part in parts) relations.Add(x => Relate(x, null, IfcClass.RelAggregates, gid, part));
            return true;
        }

        /// <summary>
        /// The file's rooms (IfcSpace): Lapin's own rebuilt from their intent,
        /// the others recognised by the modules (a room of the outline of their
        /// body); without a module that makes rooms, or reading for reference
        /// only, they are left out (a space is air, not something to show).
        /// </summary>
        void Spaces(IEdit edit)
        {
            if (o.ReferenceOnly) return;
            var rooms = 0;
            foreach (var e in f.OfType("IFCSPACE").OrderBy(x => x.Id))
            {
                if (e[0] is not string { Length: 22 } || !Takes(e)) continue;
                if (IntentOf(e.Id) is { } intent) { Native(edit, e, intent); rooms++; continue; }
                if (Recognize(edit, e)) rooms++;
            }
            // Where to find them: a room is air, drawn in plans only.
            if (rooms > 0)
                notes.Add($"{rooms} room(s) from the file's spaces, each on its storey: its plan shows their stamps (View → Room colours fills them; a Rooms schedule, + under Schedules in the browser, lists them); the 3D view leaves rooms out until its Visibility/Graphics shows them.");
        }

        /// <summary>Where what has no storey of its own goes: the storey at ±0.00 or the lowest above it, else the lowest.</summary>
        static string? DefaultStorey(IEdit edit)
        {
            var all = Spatial.Storeys(edit.Model).ToList();
            return (all.Where(x => x.Num("elevation") >= -1e-6).OrderBy(x => x.Num("elevation")).FirstOrDefault() ?? all.FirstOrDefault())?.Id;
        }

        void Native(IEdit edit, StepEntity e, Intent intent)
        {
            var gid = Str(e[0])!;
            var cls = intent.Class ?? ClassName(e.Type);
            var predefined = intent.Predefined ?? (e.Args.Count > 8 && e.Type is not ("IFCDOOR" or "IFCWINDOW" or "IFCSPACE") ? Enum(e[8]) : null);
            if (predefined is "NOTDEFINED" or "USERDEFINED") predefined = intent.Predefined;
            // A room's name is its LongName (its Name is its number).
            var name = e.Type == "IFCSPACE" && e.Args.Count > 7 && Str(e[7]) is { Length: > 0 } longName ? longName : Str(e[2]);
            Put(edit, gid, cls, name ?? cls[3..], intent.Attrs, predefined);
            var storey = StoreyFor(e.Id);
            relations.Add(x => Contain(x, gid, storey));
            if (intent.Host is { } host) relations.Add(x => Relate(x, null, IfcClass.RelVoidsElement, host, gid));
            foreach (var rel in f.OfType("IFCRELDEFINESBYTYPE").Where(r => Refs(r[4]).Contains(e.Id)))
                if (typeIds.TryGetValue(Ref(rel[5]), out var typeId)) relations.Add(x => Relate(x, null, IfcClass.RelDefinesByType, typeId, gid));
            foreach (var rel in f.OfType("IFCRELASSOCIATESMATERIAL").Where(r => Refs(r[4]).Contains(e.Id)))
                if (f.Get(rel[5]) is { Type: "IFCMATERIALLAYERSETUSAGE" } usage && f.Get(usage[0]) is { } set)
                {
                    var owner = f.OfType("IFCRELDEFINESBYTYPE").FirstOrDefault(r => Refs(r[4]).Contains(e.Id)) is { } byType && typeIds.TryGetValue(Ref(byType[5]), out var tid) ? tid : gid;
                    var setId = LayerSet(edit, set.Id, owner);
                    var attrs = Layers.WriteUsage(new LayerUsage(setId, Enum(usage[2]) == "NEGATIVE", Length(usage[3])));
                    attrs["layerSetDirection"] = Enum(usage[1]) ?? "AXIS2";
                    var usageId = Step.Guid(gid, "usage");
                    Put(edit, usageId, IfcClass.MaterialLayerSetUsage, "", attrs, null, counts: false);
                    var relId = Str(rel[0]);
                    relations.Add(x => Relate(x, relId, IfcClass.RelAssociatesMaterial, usageId, gid));
                }
            foreach (var rel in f.OfType("IFCRELVOIDSELEMENT").Where(r => Ref(r[4]) == e.Id))
            {
                if (f.Get(rel[5]) is not { } op) continue;
                var opGid = Str(op[0]) ?? "";
                var relId = Str(rel[0]);
                if (op.Type == "IFCOPENINGELEMENT")
                {
                    if (IntentOf(op.Id) is not { } opIntent) continue;
                    Put(edit, opGid, IfcClass.OpeningElement, Str(op[2]) ?? "Opening", opIntent.Attrs, opIntent.Predefined ?? "OPENING", counts: false);
                    foreach (var fill in f.OfType("IFCRELFILLSELEMENT").Where(r => Ref(r[4]) == op.Id))
                        if (f.Get(fill[5]) is { } filler && Str(filler[0]) is { } fillGid)
                        {
                            var fillRel = Str(fill[0]);
                            relations.Add(x => Relate(x, fillRel, IfcClass.RelFillsElement, opGid, fillGid));
                        }
                }
                relations.Add(x => Relate(x, relId, IfcClass.RelVoidsElement, gid, opGid));
            }
            foreach (var rel in f.OfType("IFCRELCONNECTSPATHELEMENTS").Where(r => Ref(r[5]) == e.Id))
                if (f.Get(rel[6]) is { } other && Str(other[0]) is { } otherGid)
                {
                    var attrs = new JsonObject
                    {
                        ["relatingConnection"] = Enum(rel[10]) ?? "NOTDEFINED", ["relatedConnection"] = Enum(rel[9]) ?? "NOTDEFINED",
                        ["kind"] = Str(rel[2]) ?? "L", ["style"] = Str(rel[3]) ?? "butt",
                    };
                    var relId = Str(rel[0]);
                    relations.Add(x => Relate(x, relId, IfcClass.RelConnectsPathElements, gid, otherGid, attrs));
                }
        }

        /// <summary>
        /// An element the file describes only by its body: kept as it is, its openings cut where the booleans are;
        /// with the 2D the file drew for its plans (<see cref="Plan2d"/>), and kept for that alone when it has no body.
        /// </summary>
        /// <summary>What a reference needs from the file, read before it is put in the model: its 2D, its shared key or else its own body, its description and properties.</summary>
        sealed record Prepared((string Key, Xf At)? Shared, BodyRead Body, Vec3 Centre, bool Pseudo,
            List<Plan2dItem> Plan2d, int Sections2d, int Elevations2d, JsonNode? Shape, JsonObject Properties, (JsonObject Own, List<IfcCarry.Shared> Shared) Carried);

        readonly System.Collections.Concurrent.ConcurrentDictionary<int, Prepared> prepared = [];

        /// <summary>
        /// The references to come read from the file on every core (the model warmed first, then only read):
        /// each one's body evaluated, welded, cut and read by the import's rules, its 2D, description and properties.
        /// Putting them in the model stays in order, one by one. A few are read as they come, unless a rule needs
        /// them all first (the middle of the model, the bodies the file repeats).
        /// </summary>
        void PrepareAll(IReadOnlyList<StepEntity> references, ISolidBooleans? booleans)
        {
            if (references.Count < 8 && o.FarAway == null && !o.Dedupe) return;
            model.Warm();
            var done = 0;
            Parallel.ForEach(references, e =>
            {
                prepared[e.Id] = Prepare(e, booleans);
                var n = Interlocked.Increment(ref done);
                if (Due(n, references.Count)) Report("Reading the references", n, references.Count);
            });
            FindMiddle();
        }

        Prepared Prepare(StepEntity e, ISolidBooleans? booleans)
        {
            var (plan2d, sections2d, elevations2d) = model.Drawing2d(e.Id);
            var shared = model.SharedKey(e.Id);
            var body = shared is var (key, _) ? SharedRead(key, booleans) : Read(OwnBody(e, booleans));
            var centre = shared is var (_, at) ? at.Apply(body.Centre) : body.Centre;
            if (body.Hash != null && body.Parts.Count > 0) hashUses.AddOrUpdate(body.Hash, 1, (_, n) => n + 1);
            return new Prepared(shared, body, centre, o.Pseudo && IsPseudo(e), plan2d, sections2d, elevations2d, model.Describe(e.Id), PropertiesJson(e.Id),
                IfcCarry.Capture(f, carryIndex.Value, e, Seed, o.Keeps, o.Numbers));
        }

        /// <summary>The relationships the carried IFC is read through, indexed once (on the first core that asks).</summary>
        readonly Lazy<IfcCarry.Index> carryIndex = new(() => new IfcCarry.Index(f), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>What the records' ids derive from: the file's project, so importing again finds them.</summary>
        string Seed => seed ??= Str(f.OfType("IFCPROJECT").FirstOrDefault()?[0]) ?? "ifc";
        string? seed;

        readonly HashSet<string> recordsPut = [];
        readonly Dictionary<string, int> bodiless = [];

        void Reference(IEdit edit, StepEntity e, ISolidBooleans? booleans)
        {
            var gid = Str(e[0])!;
            var p = prepared.TryRemove(e.Id, out var ready) ? ready : Prepare(e, booleans);
            var bodied = p.Body.Parts.Count > 0;
            if (!bodied && e.Type == "IFCELEMENTASSEMBLY" && Assembly(edit, e, gid)) return;
            // The 2D the user left out: an element's own (it is drawn by its cut), or an element that is nothing but 2D.
            if (p.Plan2d.Count > 0 || p.Sections2d + p.Elevations2d > 0)
            {
                if (!bodied && !o.Only2d) { left2d++; return; }
                if (bodied && !o.Plan2d) { p = p with { Plan2d = [], Sections2d = 0, Elevations2d = 0 }; without2d++; }
            }
            // The import's rules: what they leave out.
            if (LeftOutBy(p, p.Plan2d.Count > 0) is { } reason) { Drop(reason); return; }
            var plan2d = p.Plan2d;
            // A body many products share: kept once, each product an instance of it (docs/plan-instancing.md); a body
            // the file repeats under other names, kept once by what it is, each instance placed from its low corner.
            if (p.Shared is var (key, at) && bodied)
            {
                if (o.Dedupe && ContentBody(edit, e, p.Body, fromCorner: false) is var (byContent, kept))
                {
                    // This map's body is the one kept moved by the difference of their corners.
                    Referenced(edit, e, gid, Instance(byContent, at with { O = at.Apply(p.Body.Lo - kept) }), p);
                    instancesByContent++;
                    return;
                }
                if (SharedBody(edit, key, e, p.Body.Parts) is { } bodyId) { Referenced(edit, e, gid, Instance(bodyId, at), p); return; }
            }
            if (!bodied && plan2d.Count == 0)
            {
                var cls = ClassName(e.Type);
                bodiless[cls] = bodiless.GetValueOrDefault(cls) + 1;
                return;
            }
            if (bodied && o.Dedupe && p.Body.Hash is { } hash && hashUses.GetValueOrDefault(hash) > 1 && ContentBody(edit, e, p.Body, fromCorner: true) is var (shared, keptLo))
            {
                Referenced(edit, e, gid, Instance(shared, Xf.Identity with { O = p.Body.Lo - keptLo }), p);
                instancesByContent++;
                return;
            }
            Referenced(edit, e, gid, new JsonObject { [StoredBodies.Key] = StoredBodies.Write(p.Body.Parts) }, p);
        }

        /// <summary>A product's own body: its meshes welded, cut by its openings where the booleans are.</summary>
        List<(string Name, string Color, IReadOnlyList<Vec3> Vertices, IReadOnlyList<(int A, int B, int C)> Triangles)> OwnBody(StepEntity e, ISolidBooleans? booleans)
        {
            var (body, openings) = model.Shape(e.Id, booleans);
            List<Solid>? cutters = null;
            if (openings.Count > 0 && booleans != null)
                cutters = openings.Select(m => { var w = m.Welded(); return SolidFromMesh.Build(w.V, w.T); }).OfType<Solid>().ToList();
            var parts = new List<(string Name, string Color, IReadOnlyList<Vec3> Vertices, IReadOnlyList<(int A, int B, int C)> Triangles)>();
            foreach (var (mesh, colour) in body)
            {
                var m = mesh.Welded();
                if (cutters is { Count: > 0 })
                {
                    try
                    {
                        if (SolidFromMesh.Build(m.V, m.T) is { } solid && booleans!.Difference(solid, cutters) is { } cut) m = MeshBuilder.FromSolid(cut);
                    }
                    catch (InvalidOperationException) { /* uncut, as it came */ }
                }
                if (m.T.Count > 0) parts.Add(("Body", colour ?? "#c9ced6", m.V, m.T));
            }
            return parts;
        }

        /// <summary>A reference put in the model: its body (its own, or an instance's) in <paramref name="attrs"/>, what the file says of it added.</summary>
        void Referenced(IEdit edit, StepEntity e, string gid, JsonObject attrs, Prepared p)
        {
            var cls = ClassName(e.Type);
            attrs["reference"] = true;
            attrs["ifcClass"] = cls;
            attrs["objectType"] = Str(e[4]);
            attrs["properties"] = p.Properties;
            attrs["shape"] = p.Shape;
            if (p.Plan2d.Count > 0) { attrs[Plan2d.Key] = Plan2d.Write(p.Plan2d, p.Sections2d, p.Elevations2d); with2d++; }
            // What the file says of it that Lapin does not model, for the export to write back as it came.
            attrs[IfcSource.Key] = p.Carried.Own;
            foreach (var r in p.Carried.Shared)
                if (recordsPut.Add(r.Id)) Put(edit, r.Id, IfcSource.RecordClass, "IFC record", r.Attrs, null, counts: false);
            Put(edit, gid, cls, Str(e[2]) ?? cls[3..], attrs, null, counts: false);
            references++;
            referencesByClass[cls] = referencesByClass.GetValueOrDefault(cls) + 1;
            var storey = StoreyFor(e.Id);
            relations.Add(x => Contain(x, gid, storey));
            // A Studio solid keeps its type (Lapin's, read from its intent), as the native elements do.
            if (sketched.Contains(e.Id))
                foreach (var rel in f.OfType("IFCRELDEFINESBYTYPE").Where(r => Refs(r[4]).Contains(e.Id)))
                    if (typeIds.TryGetValue(Ref(rel[5]), out var typeId)) relations.Add(x => Relate(x, null, IfcClass.RelDefinesByType, typeId, gid));
        }

        readonly Dictionary<string, string?> sharedBodies = [];

        /// <summary>The shared body a key names, made the first time (its id from the file and the key, so importing again finds it); null when it has no triangles.</summary>
        string? SharedBody(IEdit edit, string key, StepEntity first, List<(string Name, string Color, IReadOnlyList<Vec3> Vertices, IReadOnlyList<(int A, int B, int C)> Triangles)> parts)
        {
            if (sharedBodies.TryGetValue(key, out var known)) return known;
            string? id = null;
            if (parts.Count > 0)
            {
                id = Step.Guid(Seed, "shared-body\n" + key);
                Put(edit, id, StoredBodies.SharedClass, $"{Str(first[2]) ?? ClassName(first.Type)[3..]} (shared)", new JsonObject { [StoredBodies.Key] = StoredBodies.Write(parts), ["source"] = key }, null, counts: false);
            }
            return sharedBodies[key] = id;
        }

        JsonObject PropertiesJson(int product)
        {
            var json = new JsonObject();
            foreach (var (set, props) in model.Properties(product, o.SkipSets == null ? null : name => o.Keeps(name), o.Numbers))
            {
                var p = new JsonObject();
                foreach (var (k, v) in props) p[k] = v;
                json[set] = p;
            }
            return json;
        }
    }
}
