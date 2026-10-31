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
            var foreign = foreignReady.TryRemove(e.Id, out var ready) ? ready : Foreign(e, DefaultStorey(edit) ?? "");
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
                        ["shape"] = model.Describe(e.Id),
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

        readonly System.Collections.Concurrent.ConcurrentDictionary<int, ForeignElement> foreignReady = [];

        /// <summary>
        /// The elements the recognisers will be offered read from the file on every core (their prisms,
        /// openings, fills, materials, parts): the heavy part of recognising, which reads only the file.
        /// The recognisers then make them one by one, in order.
        /// </summary>
        void PrepareForeign(IEdit edit, IReadOnlyList<StepEntity> candidates)
        {
            if (recognition == null) PrepareRecognition();
            if (recognizers.Count == 0 || candidates.Count < 8) return;
            model.Warm();
            var storey = DefaultStorey(edit) ?? "";
            var done = 0;
            Parallel.ForEach(candidates, e =>
            {
                if (e.Type is "IFCOPENINGELEMENT" || !GlobalId.IsValid(Str(e[0]) ?? "")) return;
                foreignReady[e.Id] = Foreign(e, storey);
                var n = Interlocked.Increment(ref done);
                if (Due(n, candidates.Count)) Report("Reading the elements", n, candidates.Count);
            });
        }

        ForeignElement Foreign(StepEntity e, string defaultStorey)
        {
            var cls = ClassName(e.Type);
            var prisms = model.Prisms(e.Id, out var why);
            var openings = new List<ForeignOpening>();
            foreach (var op in openingsOf?.GetValueOrDefault(e.Id) ?? [])
            {
                var opPrisms = model.Prisms(op.Id, out _);
                ForeignFill? fill = null;
                if (fillOf != null && fillOf.TryGetValue(op.Id, out var filler) && filler.Type is "IFCDOOR" or "IFCWINDOW" or "IFCDOORSTANDARDCASE" or "IFCWINDOWSTANDARDCASE" && Takes(filler))
                {
                    var style = typeOf?.GetValueOrDefault(filler.Id);
                    var (operation, userDefined) = Operation(filler, style);
                    double? h = filler[8] is null ? null : Length(filler[8]), w = filler[9] is null ? null : Length(filler[9]);
                    // The way its panels open: its placement's +Y (IFC's rule, which its left and right are seen along).
                    var y = model.Placement(filler[5]).Y;
                    Vec2? opens = new Vec2(y.X, y.Y) is { Length: > 1e-6 } yy ? yy.Normalized() : null;
                    fill = new ForeignFill(Str(filler[0]) ?? "", ClassName(filler.Type), Str(filler[2]), w is > 0 ? w : null, h is > 0 ? h : null, operation, style == null ? null : Str(style[2]), style == null ? null : Str(style[0]), PropertiesJson(filler.Id),
                        UserDefined: userDefined, Panels: Panels(filler, style), OpensTowards: opens);
                }
                openings.Add(new ForeignOpening(Str(op[0]) ?? "", Str(op[2]), opPrisms, fill));
            }
            var type = typeOf?.GetValueOrDefault(e.Id);
            var (layers, material, usage) = MaterialOf(e.Id, type?.Id);
            var storey = StoreyFor(e.Id) ?? defaultStorey;
            // IfcSpace: LongName [7], CompositionType [8], PredefinedType [9].
            var space = e.Type == "IFCSPACE";
            var predefined = space ? (e.Args.Count > 9 ? Enum(e[9]) : null) : e.Args.Count > 8 && e.Type is not ("IFCDOOR" or "IFCWINDOW") ? Enum(e[8]) : null;
            // A space with no vertical prism (a B-rep whose floor did not read as one, only its FootPrint, no shape): its outline from its curve or its floor.
            var unswept = space && !prisms.Any(p => p.Vertical && p.Height > 1e-3);
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
                Axis: axis, Box: prisms.Count == 0 || unswept ? model.Bounds(e.Id) : null, Usage: usage, Parts: parts,
                Extents: origin is { } o2 && dir is { } d2 ? model.Extents(e.Id, o2, d2) : null,
                LongName: space && e.Args.Count > 7 ? Str(e[7]) : null,
                Footprint: unswept ? model.FootprintLoops(e.Id) ?? Footprint(e.Id) : null);
        }

        /// <summary>
        /// A body's outline in plan from its triangles, when it is no prism: the
        /// edges of its triangles lying at its lowest level (its floor; slivers
        /// of nearly collinear points too, so their edges pair up) that only one
        /// such triangle has, chained into loops (the outline and its holes).
        /// </summary>
        List<List<Vec2>>? Footprint(int product)
        {
            var (body, _) = model.Shape(product);
            var edges = new Dictionary<(long, long, long, long), (Vec2 A, Vec2 B, int N)>();
            static (long, long) K(Vec2 p) => ((long)Math.Round(p.X * 1e5), (long)Math.Round(p.Y * 1e5));
            var low = double.MaxValue;
            foreach (var (mesh, _) in body) foreach (var v in mesh.V) low = Math.Min(low, v.Z);
            foreach (var (mesh, _) in body)
                foreach (var (a, b, c) in mesh.T)
                {
                    var (pa, pb, pc) = (mesh.V[a], mesh.V[b], mesh.V[c]);
                    if (Math.Max(pa.Z, Math.Max(pb.Z, pc.Z)) > low + 0.01) continue;
                    foreach (var (p, q) in new[] { (pa, pb), (pb, pc), (pc, pa) })
                    {
                        var (u, w) = (new Vec2(p.X, p.Y), new Vec2(q.X, q.Y));
                        var (ku, kw) = (K(u), K(w));
                        var key = ku.CompareTo(kw) < 0 ? (ku.Item1, ku.Item2, kw.Item1, kw.Item2) : (kw.Item1, kw.Item2, ku.Item1, ku.Item2);
                        edges[key] = edges.TryGetValue(key, out var x) ? x with { N = x.N + 1 } : (u, w, 1);
                    }
                }
            var open = edges.Values.Where(x => x.N == 1).Select(x => (x.A, x.B)).ToList();
            if (open.Count < 3) return null;
            var loops = new List<List<Vec2>>();
            while (open.Count > 0)
            {
                var (a0, b0) = open[0];
                open.RemoveAt(0);
                var loop = new List<Vec2> { a0 };
                var cur = b0;
                for (var guard = 0; guard < 100_000 && K(cur) != K(a0); guard++)
                {
                    var i = open.FindIndex(x => K(x.A) == K(cur) || K(x.B) == K(cur));
                    if (i < 0) break;
                    loop.Add(cur);
                    cur = K(open[i].A) == K(cur) ? open[i].B : open[i].A;
                    open.RemoveAt(i);
                }
                if (K(cur) == K(a0) && loop.Count >= 3) loops.Add(Footprints.Clean(loop));
            }
            return loops.Count > 0 ? [.. loops.OrderByDescending(l => Math.Abs(Footprints.Area(l)))] : null;
        }

        static bool Defined(string? v) => v is { Length: > 0 } and not "NOTDEFINED";

        /// <summary>
        /// How a door or window operates: its own OperationType/PartitioningType (IFC4 [11], with
        /// the user-defined text [12]), else its type's ([10], [12]), else an IFC2X3 style's
        /// (a door style's [8], a window style's [9]).
        /// </summary>
        (string? Operation, string? UserDefined) Operation(StepEntity filler, StepEntity? type)
        {
            if (filler.Args.Count > 12 && Enum(filler[11]) is { } own && Defined(own)) return (own, Str(filler[12]));
            if (type is { Type: "IFCDOORTYPE" or "IFCWINDOWTYPE" } && type.Args.Count > 12 && Enum(type[10]) is { } typed && Defined(typed)) return (typed, Str(type[12]));
            if (type is { Type: "IFCDOORSTYLE" } && Enum(type[8]) is { } ds && Defined(ds)) return (ds, null);
            if (type is { Type: "IFCWINDOWSTYLE" } && Enum(type[9]) is { } ws && Defined(ws)) return (ws, null);
            return (null, null);
        }

        /// <summary>A door's or window's panels as its type (or itself) describes them: IfcDoorPanelProperties, IfcWindowPanelProperties.</summary>
        List<IfcJoineryPanel>? Panels(StepEntity filler, StepEntity? type)
        {
            var sets = (type != null && type.Args.Count > 5 ? Items(type[5]) : []).Concat((definitions.GetValueOrDefault(filler.Id) ?? []).Select(f.Entities.GetValueOrDefault).OfType<StepEntity>());
            List<IfcJoineryPanel>? o = null;
            foreach (var p in sets)
            {
                if (p.Type == "IFCDOORPANELPROPERTIES" && Enum(p[5]) is { } dop)
                    (o ??= []).Add(new IfcJoineryPanel(dop, Enum(p[7]) ?? "NOTDEFINED", p[6] is null ? null : Num(p[6])));
                else if (p.Type == "IFCWINDOWPANELPROPERTIES" && Enum(p[4]) is { } wop)
                    (o ??= []).Add(new IfcJoineryPanel(wop, Enum(p[5]) ?? "NOTDEFINED", null));
            }
            return o;
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
