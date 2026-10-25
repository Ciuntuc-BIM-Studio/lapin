using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Ifc;

/// <summary>
/// The recognition of foreign elements (docs/spec-ifc.md, Recunoașterea): an
/// element without Lapin's intent is read into a <see cref="ForeignElement"/>
/// (its body as prisms, its openings and fills, its type, its layers) and
/// offered to the modules' <see cref="IIfcRecognizer"/>s; the first that makes
/// it natively wins, the others leave it to come as a reference.
/// </summary>
public static partial class IfcImport
{
    sealed partial class Reader
    {
        RecognitionContext? recognition;
        IReadOnlyList<IIfcRecognizer> recognizers = [];
        int recognized, madeSeen;
        readonly Dictionary<string, int> recognizedByClass = [], referencesByClass = [];
        readonly HashSet<int> recognizedFills = [];
        Dictionary<int, StepEntity>? fillOf;            // opening → its door or window
        Dictionary<string, int>? fillIdOf;              // a door's or window's GlobalId → its file id
        Dictionary<int, List<StepEntity>>? openingsOf;  // element → its openings
        Dictionary<int, StepEntity>? typeOf;            // object → its type (IfcRelDefinesByType)
        Dictionary<int, StepEntity>? materialOf;        // object → what IfcRelAssociatesMaterial relates
        Dictionary<int, List<StepEntity>>? partsOf;     // element → the parts aggregated under it

        void PrepareRecognition()
        {
            recognizers = [.. s.Catalog.All<IIfcRecognizer>()];
            recognition = new RecognitionContext(s.Catalog);
            fillOf = [];
            fillIdOf = [];
            foreach (var r in f.OfType("IFCRELFILLSELEMENT")) if (f.Get(r[5]) is { } filler) { fillOf[Ref(r[4])] = filler; if (Str(filler[0]) is { } fg) fillIdOf[fg] = filler.Id; }
            openingsOf = [];
            foreach (var r in f.OfType("IFCRELVOIDSELEMENT"))
                if (f.Get(r[5]) is { Type: "IFCOPENINGELEMENT" } op) (openingsOf.TryGetValue(Ref(r[4]), out var l) ? l : openingsOf[Ref(r[4])] = []).Add(op);
            typeOf = [];
            foreach (var r in f.OfType("IFCRELDEFINESBYTYPE")) if (f.Get(r[5]) is { } t) foreach (var obj in Refs(r[4])) typeOf[obj] = t;
            materialOf = [];
            foreach (var r in f.OfType("IFCRELASSOCIATESMATERIAL")) if (f.Get(r[5]) is { } m) foreach (var obj in Refs(r[4])) materialOf[obj] = m;
            partsOf = [];
            foreach (var r in f.OfType("IFCRELAGGREGATES"))
                if (f.Get(r[4]) is { } whole && whole.Type is not ("IFCPROJECT" or "IFCSITE" or "IFCBUILDING" or "IFCBUILDINGSTOREY"))
                    foreach (var part in Refs(r[5])) if (f.Entities.GetValueOrDefault(part) is { } pe) (partsOf.TryGetValue(whole.Id, out var l) ? l : partsOf[whole.Id] = []).Add(pe);
        }

        /// <summary>The element made natively by a module, with the file's name, class and property sets kept on it for the inspector.</summary>
        bool Recognize(IEdit edit, StepEntity e)
        {
            if (recognition == null) PrepareRecognition();
            if (recognizers.Count == 0 || e.Type is "IFCOPENINGELEMENT") return false;
            var gid = Str(e[0]) ?? "";
            if (!GlobalId.IsValid(gid)) return false;
            // The same element imported before: made again from the file (its openings, fills and usage go with it); one the user made stays.
            var again = false;
            if (edit.Model.Get(gid) is { } old && !seen.Contains(gid))
            {
                if (old.Attrs["imported"] == null) return false;
                edit.Remove(gid);
                again = true;
            }
            var foreign = Foreign(edit, e);
            foreach (var r in recognizers)
            {
                string? id;
                try { id = r.Recognize(edit, foreign, recognition!); }
                catch (CommandException ex) { recognition!.Skip(ex.Message); continue; }
                if (id == null) continue;
                if (edit.Model.Get(id) is { } made)
                {
                    var attrs = made.CloneAttrs();
                    attrs["imported"] = new JsonObject
                    {
                        ["ifcClass"] = foreign.IfcClass, ["name"] = foreign.Name, ["objectType"] = foreign.ObjectType, ["typeName"] = foreign.TypeName,
                        ["properties"] = foreign.Properties.DeepClone(),
                    };
                    edit.Replace(made.With(attrs: attrs));
                    seen.Add(id);
                }
                recognized++;
                if (again) updated++;
                recognizedByClass[foreign.IfcClass] = recognizedByClass.GetValueOrDefault(foreign.IfcClass) + 1;
                for (; madeSeen < recognition!.Made.Count; madeSeen++)
                    if (edit.Model.Get(recognition.Made[madeSeen]) is { } m && m.Id != id)
                        recognizedByClass[m.Class] = recognizedByClass.GetValueOrDefault(m.Class) + 1;
                foreach (var op in foreign.Openings) if (op.Fill != null && fillIdOf != null && fillIdOf.TryGetValue(op.Fill.GlobalId, out var fid)) recognizedFills.Add(fid);
                return true;
            }
            return false;
        }

        void FinishRecognition(IEdit edit)
        {
            if (recognition == null) return;
            foreach (var r in recognizers) r.Finish(edit, recognition);
            foreach (var (reason, n) in recognition.Skipped.OrderByDescending(kv => kv.Value).Take(6))
                notes.Add($"{n} left as reference: {reason}.");
            foreach (var g in recognition.Notes.GroupBy(n => n).OrderByDescending(g => g.Count()).Take(6))
                notes.Add(g.Count() > 1 ? $"{g.Count()} × {g.Key}" : g.Key);
        }

        ForeignElement Foreign(IEdit edit, StepEntity e)
        {
            var cls = ClassName(e.Type);
            var prisms = model.Prisms(e.Id, out var why);
            var openings = new List<ForeignOpening>();
            foreach (var op in openingsOf?.GetValueOrDefault(e.Id) ?? [])
            {
                var opPrisms = model.Prisms(op.Id, out _);
                ForeignFill? fill = null;
                if (fillOf != null && fillOf.TryGetValue(op.Id, out var filler) && filler.Type is "IFCDOOR" or "IFCWINDOW" or "IFCDOORSTANDARDCASE" or "IFCWINDOWSTANDARDCASE")
                {
                    var style = typeOf?.GetValueOrDefault(filler.Id);
                    // IFC4: OverallHeight [8], OverallWidth [9], OperationType/PartitioningType [11]; IFC2X3 doors: the style's OperationType [8].
                    var operation = filler.Args.Count > 11 ? Enum(filler[11]) : null;
                    operation ??= style is { Type: "IFCDOORSTYLE" } ? Enum(style[8]) : style is { Type: "IFCWINDOWSTYLE" } ? Enum(style[9]) : null;
                    double? h = filler[8] is null ? null : Length(filler[8]), w = filler[9] is null ? null : Length(filler[9]);
                    fill = new ForeignFill(Str(filler[0]) ?? "", ClassName(filler.Type), Str(filler[2]), w is > 0 ? w : null, h is > 0 ? h : null, operation, style == null ? null : Str(style[2]), style == null ? null : Str(style[0]), PropertiesJson(filler.Id));
                }
                openings.Add(new ForeignOpening(Str(op[0]) ?? "", Str(op[2]), opPrisms, fill));
            }
            var type = typeOf?.GetValueOrDefault(e.Id);
            var (layers, material, usage) = MaterialOf(e.Id, type?.Id);
            var storey = StoreyFor(e.Id) ?? Spatial.Storeys(edit.Model).FirstOrDefault()?.Id ?? "";
            var predefined = e.Args.Count > 8 && e.Type is not ("IFCDOOR" or "IFCWINDOW") ? Enum(e[8]) : null;
            var axis = model.AxisCurve(e.Id);
            Vec2? origin = null, dir = null;
            if (axis is { Count: >= 2 } && new Vec2(axis[^1].X - axis[0].X, axis[^1].Y - axis[0].Y) is { Length: > 1e-3 } d)
            {
                origin = new Vec2(axis[0].X, axis[0].Y); dir = d.Normalized();
            }
            List<ForeignPart>? parts = null;
            foreach (var part in partsOf?.GetValueOrDefault(e.Id) ?? [])
            {
                var (pl, pm, _) = MaterialOf(part.Id, typeOf?.GetValueOrDefault(part.Id)?.Id);
                (parts ??= []).Add(new ForeignPart(Str(part[0]) ?? "", Str(part[2]), model.Prisms(part.Id, out _), pm ?? pl?.FirstOrDefault().Material,
                    origin is { } o1 && dir is { } d1 ? model.Extents(part.Id, o1, d1) : null));
            }
            return new ForeignElement(Str(e[0]) ?? "", cls, predefined, Str(e[2]), Str(e[4]), storey, prisms, why, openings,
                type == null ? null : Str(type[2]), type == null ? null : Str(type[0]), layers, material, PropertiesJson(e.Id),
                Axis: axis, Box: prisms.Count == 0 ? model.Bounds(e.Id) : null, Usage: usage, Parts: parts,
                Extents: origin is { } o2 && dir is { } d2 ? model.Extents(e.Id, o2, d2) : null);
        }

        /// <summary>What the file associates as material to an object (or its type): a layer set (with its usage, when the object has one), or one material.</summary>
        (List<(string Material, double Thickness, bool Core)>? Layers, string? Material, (bool Negative, double Offset)? Usage) MaterialOf(int id, int? typeId)
        {
            var mat = materialOf?.GetValueOrDefault(id) ?? (typeId == null ? null : materialOf?.GetValueOrDefault(typeId.Value));
            if (mat == null) return (null, null, null);
            var set = mat.Type == "IFCMATERIALLAYERSETUSAGE" ? f.Get(mat[0]) : mat.Type == "IFCMATERIALLAYERSET" ? mat : null;
            if (set != null)
            {
                var layers = Items(set[0]).Select(l => (Str(f.Get(l[0])?[0]) ?? "Material", Length(l[1]), Str(l[5]) == "Core")).ToList();
                var usage = mat.Type == "IFCMATERIALLAYERSETUSAGE" ? (Enum(mat[2]) == "NEGATIVE", Length(mat[3])) : ((bool, double)?)null;
                return (layers, null, usage);
            }
            if (mat.Type == "IFCMATERIAL") return (null, Str(mat[0]), null);
            if (mat.Type == "IFCMATERIALLIST" && Items(mat[0]).FirstOrDefault() is { } m0) return (null, Str(m0[0]), null);
            if (mat.Type == "IFCMATERIALCONSTITUENTSET" && Items(mat[2]).FirstOrDefault() is { } c0 && f.Get(c0[2]) is { } cm) return (null, Str(cm[0]), null);
            return (null, null, null);
        }
    }
}
