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
            // The file's connections between walls (and other path elements): the joins of what is recognised try them first.
            static Connection? At(string? c) => c switch { "ATSTART" => Connection.AtStart, "ATEND" => Connection.AtEnd, "ATPATH" => Connection.AtPath, _ => null };
            foreach (var r in f.OfType("IFCRELCONNECTSPATHELEMENTS"))
                if (f.Get(r[5]) is { } relating && f.Get(r[6]) is { } related && Str(relating[0]) is { } ga && Str(related[0]) is { } gb
                    && At(r.Args.Count > 10 ? Enum(r[10]) : null) is { } atA && At(r.Args.Count > 9 ? Enum(r[9]) : null) is { } atB)
                    recognition.Connections.Add(new ForeignConnection(ga, atA, gb, atB));
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
            var foreign = foreignReady.TryRemove(e.Id, out var ready) ? ready : ForeignOrNull(e, DefaultStorey(edit) ?? "");
            if (foreign == null) { recognition!.Skip("a body the file describes in a way that could not be read"); return false; }
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
                var extra = new List<(string Id, string Class)>();
                for (; madeSeen < recognition!.Made.Count; madeSeen++)
                    if (edit.Model.Get(recognition.Made[madeSeen]) is { } m && m.Id != id)
                    {
                        recognizedByClass[m.Class] = recognizedByClass.GetValueOrDefault(m.Class) + 1;
                        extra.Add((m.Id, m.Class));
                    }
                var fills = new List<int>();
                foreach (var op in foreign.Openings) if (op.Fill != null && fillIdOf != null && fillIdOf.TryGetValue(op.Fill.GlobalId, out var fid)) { recognizedFills.Add(fid); fills.Add(fid); }
                // A room is air: the gate measures bodies.
                if (o.Gate.On && foreign.IfcClass != IfcClass.Space) gated.Add(new Gated(e, id, foreign.IfcClass, foreign.Prisms.Any(p => p.Clipped), extra, fills));
                return true;
            }
            return false;
        }

        void FinishRecognition(IEdit edit)
        {
            if (recognition == null) return;
            foreach (var r in recognizers) r.Finish(edit, recognition);
            Gate(edit);
            foreach (var (reason, n) in recognition.Skipped.OrderByDescending(kv => kv.Value).Take(6))
                notes.Add($"{n} left as reference: {reason}.");
            foreach (var g in recognition.Notes.GroupBy(n => n).OrderByDescending(g => g.Count()).Take(6))
                notes.Add(g.Count() > 1 ? $"{g.Count()} × {g.Key}" : g.Key);
        }

        /// <summary>
        /// The types this import made mapped onto the project's as the user chose before (Manage → Map imported types), once the
        /// gate has measured what recognition made against the file: a mapping is the user's choice, not recognition's, and the
        /// elements it changes (another thickness) are the ones that passed.
        /// </summary>
        void MapTypes(IEdit edit)
        {
            if (recognition == null) return;
            if (TypeMappings.AfterImport(edit, s.Catalog, recognition.CreatedTypes, FileName) is > 0 and var mapped)
                notes.Add($"{mapped} imported type{(mapped == 1 ? "" : "s")} became the project's, as mapped before (Manage → Map imported types).");
            if (TypeMappings.Mappable(edit.Model, recognition.CreatedTypes) is > 0 and var mappable)
                notes.Add($"{mappable} type{(mappable == 1 ? "" : "s")} came with the file where the project has its own of the kind: Manage → Map imported types makes them the project's.");
        }

        // ─── The fidelity gate (docs/spec-ifc.md, Poarta de fidelitate) ─────────

        /// <summary>A recognised element waiting to be measured: the file's entity, the native id, what was made with it (its openings, doors and windows), the file's fills it took.</summary>
        sealed record Gated(StepEntity E, string Id, string IfcClass, bool Clipped, List<(string Id, string Class)> Extra, List<int> Fills)
        {
            /// <summary>Rebuilt from Lapin's intent (a placed part), not by a recogniser: counted with the elements.</summary>
            public bool Native { get; init; }
        }

        readonly List<Gated> gated = [];
        FidelityReport? fidelity;
        /// <summary>The elements the gate made references again (enforce), with the fills and finishes that went with them.</summary>
        public HashSet<string> Reverted { get; } = [];

        /// <summary>The file's bodies of the gated elements (by file id), read in <see cref="Gate"/>; kept for <see cref="Measure"/> when the gate only reports.</summary>
        System.Collections.Concurrent.ConcurrentDictionary<int, List<MeshBuilder>>? gateBodies;
        System.Diagnostics.Stopwatch? gateTime;

        /// <summary>
        /// The gate, once the recognisers have finished (walls joined, finishes absorbed): every element recognised this import
        /// is to be measured against the body the file gives it, its openings cut, as the reference would have been. The file's
        /// bodies are read now. Under "enforce" the native ones are measured at the end of the reading (<see cref="Enforce"/>), and
        /// one that differs comes in as that reference, in the same step; reporting only, they are measured after the edit
        /// (<see cref="Measure"/>), from the geometry the session builds for the views anyway, so the gate costs little more than
        /// reading the file's bodies.
        /// </summary>
        void Gate(IEdit edit)
        {
            if (!o.Gate.On || gated.Count == 0) return;
            gateTime = System.Diagnostics.Stopwatch.StartNew();
            var booleans = s.Catalog.Get<ISolidBooleans>();
            // The file's bodies, read on every core (the file only).
            model.Warm();
            gateBodies = [];
            Parallel.ForEach(gated, g => gateBodies[g.E.Id] = Guarded(g.E, () => FileBody(g.E, booleans)) ?? []);
            gateTime.Stop();
        }

        /// <summary>
        /// The gate enforced, at the end of the reading (every relationship and containment written, so the native elements are
        /// built as the views will build them): each recognised element measured against the file's body, and one that differs
        /// brought as the reference it would have been, in the same step.
        /// </summary>
        void Enforce(IEdit edit)
        {
            if (!o.Gate.Enforcing || gateBodies == null || gateTime == null) return;
            gateTime.Start();
            var booleans = s.Catalog.Get<ISolidBooleans>();
            // On every core: the native bodies built from the model as it stands, which nothing edits meanwhile.
            var checks = Checks(edit.Model, native => RecognitionFidelity.NativeGeometry(native, edit.Model, s.Catalog), parallel: true);
            var byGid = ByGid();
            var reverted = 0;
            var written = relations.Count;
            for (var i = 0; i < checks.Count; i++)
            {
                if (!checks[i].Measured || checks[i].Pass || !byGid.TryGetValue(checks[i].Id, out var g)) continue;
                Revert(edit, g, byGid, booleans);
                checks[i] = checks[i] with { Reverted = true };
                reverted++;
            }
            // The references' relationships (their storeys), and the types made for the elements alone, which go with them.
            foreach (var r in relations.Skip(written).ToList()) r(edit);
            WriteContainment(edit);
            if (reverted > 0) RecognitionFidelity.DropUnusedTypes(edit, recognition!);
            gateTime.Stop();
            fidelity = new FidelityReport(o.Gate, checks, reverted, gateTime.Elapsed.TotalMilliseconds);
            notes.Add(fidelity.Note());
        }

        /// <summary>
        /// The gate's report, after the edit (reporting only): each recognised element's geometry as the session built it for the
        /// views, against the file's body read in <see cref="Gate"/>.
        /// </summary>
        public void Measure(Session session)
        {
            if (gateBodies == null || gateTime == null || fidelity != null) return;
            gateTime.Start();
            // The session's own builds, one after the other (built already by the edit's diff: read from its cache).
            var built = new Dictionary<string, ElementGeometry?>();
            foreach (var g in gated) if (session.Model.Get(g.Id) != null) built[g.Id] = session.Geometry(g.Id);
            var checks = Checks(session.Model, native => built.GetValueOrDefault(native.Id), parallel: true);
            fidelity = new FidelityReport(o.Gate, checks, 0, gateTime.Elapsed.TotalMilliseconds);
            notes.Add(fidelity.Note());
        }

        Dictionary<string, Gated> ByGid()
        {
            var byGid = new Dictionary<string, Gated>();
            foreach (var g in gated) byGid.TryAdd(g.Id, g);
            return byGid;
        }

        /// <summary>
        /// Every gated element measured: the native geometry <paramref name="geometry"/> gives against the file's body (a wall's
        /// with the finishes it took as layers), the joins settled. One that differs is measured again with the file's body
        /// losing what the project's cut order takes out of the native one (a slab over a wall's head: <see cref="ICutters"/>),
        /// so both are compared after the same cuts; the booleans only where they can change the verdict.
        /// </summary>
        List<FidelityCheck> Checks(IModelView m, Func<Entity, ElementGeometry?> geometry, bool parallel)
        {
            var byGid = ByGid();
            var bodies = gateBodies!;
            var cutters = s.Catalog.Get<ICutters>();
            var booleans = s.Catalog.Get<ISolidBooleans>();
            var measured = new FidelityCheck[gated.Count];
            void One(int i)
            {
                var g = gated[i];
                var name = Str(g.E[2]);
                if (m.Get(g.Id) is not { } native)
                {
                    measured[i] = new(g.Id, g.IfcClass, "", name, false, true, null, null, null, null, "absorbed into another element (a finish made a layer of its wall)", null);
                    return;
                }
                var file = bodies.GetValueOrDefault(g.E.Id) ?? [];
                // A wall that took finishes as layers: their bodies are its own now.
                foreach (var a in Absorbed(native))
                    if (byGid.TryGetValue(a, out var fg)) file = [.. file, .. bodies.GetValueOrDefault(fg.E.Id) ?? []];
                var mine = RecognitionFidelity.Meshes(geometry(native));
                var joinedTo = RecognitionFidelity.JoinedTo(m, g.Id);
                var check = RecognitionFidelity.Check(g.Id, g.IfcClass, native.Class, name, file, mine, o.Gate, g.Clipped, joinedTo: joinedTo);
                // The cut order only for what differs as it is (the booleans are the gate's dearest part): measured again against the file's body cut so.
                if (!check.Pass && check.Measured && cutters != null && booleans != null
                    && RecognitionFidelity.CutAsNative(native, m, file, cutters, booleans) is var cut && !ReferenceEquals(cut, file))
                    check = RecognitionFidelity.Check(g.Id, g.IfcClass, native.Class, name, cut, mine, o.Gate, g.Clipped, joinedTo: joinedTo);
                measured[i] = check;
            }
            if (parallel) Parallel.For(0, gated.Count, One);
            else for (var i = 0; i < gated.Count; i++) One(i);
            return RecognitionFidelity.Settle(measured, o.Gate);
        }

        /// <summary>The file's GlobalIds of the finishes a recognised wall took as layers.</summary>
        static IEnumerable<string> Absorbed(Entity native) =>
            (native.Attrs["imported"] as JsonObject)?["absorbed"] is JsonArray list ? list.Select(a => (string?)a?["id"]).OfType<string>() : [];

        /// <summary>The body the file gives an element, as its reference would have it (welded, its openings cut); its parts' when it has none of its own.</summary>
        List<MeshBuilder> FileBody(StepEntity e, ISolidBooleans? booleans)
        {
            static MeshBuilder Mesh(IReadOnlyList<Vec3> v, IReadOnlyList<(int A, int B, int C)> t)
            {
                var m = new MeshBuilder();
                m.V.AddRange(v); m.T.AddRange(t);
                return m;
            }
            var own = OwnBody(e, booleans).Select(p => Mesh(p.Vertices, p.Triangles)).ToList();
            if (own.Count > 0) return own;
            return [.. (partsOf?.GetValueOrDefault(e.Id) ?? []).SelectMany(part => OwnBody(part, booleans)).Select(p => Mesh(p.Vertices, p.Triangles))];
        }

        /// <summary>
        /// A recognised element that failed the gate made the reference it would have been (enforce): the native element goes with
        /// what was made with it (its openings, doors and windows, its usage, its joins), then it, its fills and the finishes it
        /// took come in as references from the file, as if no recogniser had taken them.
        /// </summary>
        void Revert(IEdit edit, Gated g, Dictionary<string, Gated> byGid, ISolidBooleans? booleans)
        {
            var absorbed = edit.Model.Get(g.Id) is { } native ? Absorbed(native).ToList() : [];
            void Forget(Gated x)
            {
                Reverted.Add(x.Id);
                edit.Remove(x.Id);
                foreach (var (id, cls) in x.Extra)
                {
                    if (edit.Model.Get(id) != null) edit.Remove(id);
                    recognizedByClass[cls] = recognizedByClass.GetValueOrDefault(cls) - 1;
                }
                seen.Remove(x.Id);
                if (x.Native) elements--;
                else
                {
                    recognized--;
                    recognizedByClass[x.IfcClass] = recognizedByClass.GetValueOrDefault(x.IfcClass) - 1;
                }
                Reference(edit, x.E, booleans);
                // The parts it was made of (a drywall's boards, ArchiCAD's layers): left out while it was native, its body as a
                // reference, as a reference import brings them; not offered again to recognition from their triangles.
                foreach (var part in partsOf?.GetValueOrDefault(x.E.Id) ?? [])
                {
                    if (Str(part[0]) is not { } pg || edit.Model.Get(pg) != null) continue;
                    Reference(edit, part, booleans);
                    Reverted.Add(pg);
                }
                foreach (var fid in x.Fills)
                {
                    recognizedFills.Remove(fid);
                    if (f.Entities.GetValueOrDefault(fid) is { } filler) { Reference(edit, filler, booleans); if (Str(filler[0]) is { } fg) Reverted.Add(fg); }
                }
            }
            Forget(g);
            foreach (var a in absorbed) if (byGid.TryGetValue(a, out var finish)) Forget(finish);
            foreach (var k in recognizedByClass.Where(kv => kv.Value <= 0).Select(kv => kv.Key).ToList()) recognizedByClass.Remove(k);
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
                if (ForeignOrNull(e, storey) is { } read) foreignReady[e.Id] = read;
                var n = Interlocked.Increment(ref done);
                if (Due(n, candidates.Count)) Report("Reading the elements", n, candidates.Count);
            });
        }

        readonly System.Collections.Concurrent.ConcurrentDictionary<int, bool> foreignFailed = [];

        /// <summary>The element read for the recognisers, or null when the file is malformed there (it is then tried as a reference, once).</summary>
        ForeignElement? ForeignOrNull(StepEntity e, string defaultStorey)
        {
            if (foreignFailed.ContainsKey(e.Id)) return null;
            try { return Foreign(e, defaultStorey); }
            catch (Exception ex) when (Unreadable(ex)) { foreignFailed[e.Id] = true; return null; }
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
                    origin is { } o1 && dir is { } d1 ? model.Extents(part.Id, o1, d1) : null,
                    origin is { } o3 && dir is { } d3 ? model.PartProfile(part.Id, o3, d3, s.Catalog.Get<ISolidBooleans>()) : null));
            }
            return new ForeignElement(Str(e[0]) ?? "", cls, predefined, Str(e[2]), Str(e[4]), storey, prisms, why, openings,
                type == null ? null : Str(type[2]), type == null ? null : Str(type[0]), layers, material, PropertiesJson(e.Id),
                Axis: axis, Box: prisms.Count == 0 || unswept ? model.Bounds(e.Id) : null, Usage: usage, Parts: parts,
                Extents: origin is { } o2 && dir is { } d2 ? model.Extents(e.Id, o2, d2) : null,
                LongName: space && e.Args.Count > 7 ? Str(e[7]) : null,
                Footprint: unswept ? model.FootprintLoops(e.Id) ?? Footprint(e.Id)
                    // A wall whose body is not one plain prism (a boolean cuts it, its ends trimmed or its top under a roof; several bodies, no prism): its footprint as cut, for its joins.
                    : cls is IfcClass.Wall or "IfcWallStandardCase" && !(prisms.Count == 1 && prisms[0].Vertical && !prisms[0].Clipped) ? Footprint(e.Id, s.Catalog.Get<ISolidBooleans>()) : null);
        }

        /// <summary>
        /// A body's outline in plan from its triangles, when it is no prism: the
        /// edges of its triangles lying at its lowest level (its floor; slivers
        /// of nearly collinear points too, so their edges pair up) that only one
        /// such triangle has, chained into loops (the outline and its holes);
        /// with <paramref name="booleans"/>, of the body as its booleans cut it.
        /// </summary>
        List<List<Vec2>>? Footprint(int product, ISolidBooleans? booleans = null)
        {
            var (body, _) = model.Shape(product, booleans);
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
            return loops.Count > 0 ? [.. loops.OrderByDescending(l => Math.Abs(Polygon2.SignedArea(l)))] : null;
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
            string[]? own = null;
            foreach (var p in sets)
            {
                if (p.Type == "IFCDOORPANELPROPERTIES" && Enum(p[5]) is { } dop)
                    (o ??= []).Add(new IfcJoineryPanel(dop, Enum(p[7]) ?? "NOTDEFINED", p[6] is null ? null : Num(p[6])));
                else if (p.Type == "IFCWINDOWPANELPROPERTIES" && Enum(p[4]) is { } wop)
                    (o ??= []).Add(new IfcJoineryPanel(wop, Enum(p[5]) ?? "NOTDEFINED", null));
                // Lapin's own terms for a door's panels (forum topic 34): a sash's window operation, a lift-and-slide leaf's.
                else if (p.Type == "IFCPROPERTYSET" && Str(p[2]) == IfcJoineryType.PropertySet)
                    foreach (var prop in Items(p[4]))
                        if (prop.Type == "IFCPROPERTYSINGLEVALUE" && Str(prop[0]) == "PanelOperations" && prop[2] is StepTyped { Value: string text })
                            own = text.Split(';', StringSplitOptions.TrimEntries);
            }
            if (o != null && own != null && own.Length == o.Count && own.All(x => x.Length > 0))
                o = [.. o.Select((x, i) => x with { Operation = own[i] })];
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
