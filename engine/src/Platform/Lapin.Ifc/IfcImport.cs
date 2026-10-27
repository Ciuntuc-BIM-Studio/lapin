using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Ifc;

/// <summary>How a file is read: "auto", what carries Lapin's intent is rebuilt natively and the rest comes as reference; "reference", everything as reference.</summary>
public sealed record IfcImportOptions(string Mode = "auto")
{
    public bool ReferenceOnly => string.Equals(Mode, "reference", StringComparison.OrdinalIgnoreCase);
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
        var file = StepFile.Read(bytes);
        var reader = new Reader(session, file, options ?? new IfcImportOptions());
        diff = session.Edit($"Import {name}", reader.Apply);
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
        var file = StepFile.Read(bytes);
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

        static readonly Dictionary<string, string> ProperNames = BuildNames();

        public IfcImportResult Result() => new(elements, updated, references, storeys, notes, recognized, recognizedByClass, referencesByClass, recognition?.Skipped);

        public void Apply(IEdit edit)
        {
            foreach (var r in f.OfType("IFCRELDEFINESBYPROPERTIES"))
                foreach (var p in Refs(r[4])) (definitions.TryGetValue(p, out var l) ? l : definitions[p] = []).Add(Ref(r[5]));
            foreach (var r in f.OfType("IFCRELCONTAINEDINSPATIALSTRUCTURE"))
                foreach (var p in Refs(r[4])) containedIn[p] = Ref(r[5]);
            Project(edit);
            Storeys(edit);
            if (!o.ReferenceOnly)
            {
                Materials(edit);
                Types(edit);
            }
            Elements(edit);
            foreach (var r in relations) r(edit);
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

        bool IsProduct(StepEntity e) =>
            e.Args.Count >= 7 && e[0] is string { Length: 22 } && !e.Type.StartsWith("IFCREL", StringComparison.Ordinal) && !e.Type.EndsWith("TYPE", StringComparison.Ordinal)
            && (e[5] is StepRef || e[6] is StepRef)
            && e.Type is not ("IFCPROJECT" or "IFCSITE" or "IFCBUILDING" or "IFCBUILDINGSTOREY" or "IFCOPENINGELEMENT" or "IFCANNOTATION" or "IFCSPACE");

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
            edit.Relate(IfcClass.RelContainedInSpatialStructure, storeyId, elementId);
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
            foreach (var t in f.Entities.Values.Where(e => e.Type.EndsWith("TYPE", StringComparison.Ordinal) && e.Args.Count >= 9 && e[0] is string { Length: 22 }))
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
            var products = f.Entities.Values.Where(IsProduct).OrderBy(e => e.Id).ToList();
            // First what is rebuilt natively (Lapin's intent, or recognised); then, as references, the rest: not the
            // parts of a native element (its body is its own), nor a door or window made with its wall.
            var native = new HashSet<int>();
            var rest = new List<StepEntity>();
            foreach (var e in products)
            {
                var intent = o.ReferenceOnly ? null : IntentOf(e.Id);
                if (intent != null) { Native(edit, e, intent); native.Add(e.Id); continue; }
                if (fills.Contains(e.Id)) { rest.Add(e); continue; } // decided by its wall, below
                if (!o.ReferenceOnly && !parentOf.ContainsKey(e.Id) && Recognize(edit, e)) { native.Add(e.Id); continue; }
                rest.Add(e);
            }
            foreach (var e in rest)
            {
                if (parentOf.TryGetValue(e.Id, out var parent) && (native.Contains(parent) || (!o.ReferenceOnly && IntentOf(parent) != null))) continue;
                if (recognizedFills.Contains(e.Id)) continue;
                if (!o.ReferenceOnly && parentOf.ContainsKey(e.Id) && !fills.Contains(e.Id) && Recognize(edit, e)) continue;
                Reference(edit, e, booleans);
            }
            FinishRecognition(edit);
        }

        void Native(IEdit edit, StepEntity e, Intent intent)
        {
            var gid = Str(e[0])!;
            var cls = intent.Class ?? ClassName(e.Type);
            var predefined = intent.Predefined ?? (e.Args.Count > 8 && e.Type is not ("IFCDOOR" or "IFCWINDOW") ? Enum(e[8]) : null);
            if (predefined is "NOTDEFINED" or "USERDEFINED") predefined = intent.Predefined;
            Put(edit, gid, cls, Str(e[2]) ?? cls[3..], intent.Attrs, predefined);
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

        /// <summary>An element the file describes only by its body: kept as it is, its openings cut where the booleans are.</summary>
        void Reference(IEdit edit, StepEntity e, ISolidBooleans? booleans)
        {
            var gid = Str(e[0])!;
            var (body, openings) = model.Shape(e.Id, booleans);
            if (body.Count == 0) return;
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
            if (parts.Count == 0) return;
            var cls = ClassName(e.Type);
            var attrs = new JsonObject
            {
                [StoredBodies.Key] = StoredBodies.Write(parts),
                ["reference"] = true,
                ["ifcClass"] = cls,
                ["objectType"] = Str(e[4]),
                ["properties"] = PropertiesJson(e.Id),
                ["shape"] = model.Describe(e.Id),
            };
            Put(edit, gid, cls, Str(e[2]) ?? cls[3..], attrs, null, counts: false);
            references++;
            referencesByClass[cls] = referencesByClass.GetValueOrDefault(cls) + 1;
            var storey = StoreyFor(e.Id);
            relations.Add(x => Contain(x, gid, storey));
        }

        JsonObject PropertiesJson(int product)
        {
            var o = new JsonObject();
            foreach (var (set, props) in model.Properties(product))
            {
                var p = new JsonObject();
                foreach (var (k, v) in props) p[k] = v;
                o[set] = p;
            }
            return o;
        }
    }
}
