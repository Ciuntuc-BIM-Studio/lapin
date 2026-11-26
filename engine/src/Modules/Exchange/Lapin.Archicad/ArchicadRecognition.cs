using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Archicad;

/// <summary>
/// A receive's references made native from Archicad's own parameters (docs/plan-archicad.md §3, "Fidelity"): each wall, slab,
/// column, beam, door or window and zone built as the foreign element its details describe (<see cref="ArchicadForeign"/>), offered
/// to the product's recognisers (<see cref="IIfcRecognizer"/>: architecture's walls, slabs, openings and rooms; structure's columns
/// and beams), and measured against Archicad's body for it by the fidelity gate (<see cref="RecognitionFidelity"/>, enforced): what
/// differs by more than 1 mm or 0.5 % comes back the reference it was. A door or window is native only where its native body (the
/// configurator's) is Archicad's; else its wall is made with the opening bare and the door or window stands in it as its reference.
/// A column or beam of a profile or several segments is read from its body instead (<see cref="MeshRecognition"/>, its own section).
/// Recognised elements keep their GlobalIds, properties, classifications and <c>archicad</c> attribute.
/// </summary>
public static class ArchicadRecognition
{
    /// <summary>A wall's cut priority (architecture: walls 50): a recognised slab's and column's, so neither cuts what Archicad did not.</summary>
    const int WallCutPriority = 50;

    public const string Differs = "differs from Archicad's body by more than 1 mm or 0.5 %";
    public const string FillDiffers = "a door or window Lapin's configurator does not make as Archicad's body is (its opening native, it a reference in it)";
    public const string NoModule = "no module of this product makes it";
    /// <summary>Structure's beam attribute (Beams.ThroughColumnsKey): the beam does not stop at the faces of the columns on its axis.</summary>
    const string ThroughColumnsKey = "throughColumns";

    public const string NotOffered = "a type Lapin does not make natively yet";

    sealed record Saved(Entity E, List<(string Class, string Relating)> By);

    static List<(string Class, string Relating)> Related(IModelView m, string id) =>
        [.. m.Relations.Where(x => x.Related.Contains(id)).Select(x => (x.Class, x.Relating))];

    public static ArchicadRecognitionReport Run(IEdit edit, IServices services, ArchicadModel a, IReadOnlyList<(string Id, ArchicadElement El)> offered,
        ArchicadStoreys storeys, Vec3 shift, FidelityOptions? fidelity = null)
    {
        var gate = fidelity ?? FidelityOptions.Default;
        var m = edit.Model;
        var recognizers = services.All<IIfcRecognizer>();
        var kept = new Dictionary<string, int>();
        void Keep(string why, int n = 1) => kept[why] = kept.GetValueOrDefault(why) + n;
        var ctx = new RecognitionContext(services);
        var elOf = offered.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().El);
        var meta = new Dictionary<string, (JsonNode? Meta, JsonNode? Props, JsonNode? Source, JsonNode? Classes)>();
        void Remember(Entity e) => meta[e.Id] = (e.Attrs[ArchicadReceive.Key]?.DeepClone(), e.Attrs["properties"]?.DeepClone(), e.Attrs["source"]?.DeepClone(), e.Attrs[Classifications.Key]?.DeepClone());
        void Stamp(string id, string how, string? why = null)
        {
            if (m.Get(id) is not { } e || e.Attrs[ArchicadReceive.Key] is not JsonObject mt) return;
            var attrs = e.CloneAttrs();
            var next = (JsonObject)mt.DeepClone();
            next["recognition"] = how;
            next["why"] = why;
            attrs[ArchicadReceive.Key] = next;
            edit.Replace(e.With(attrs: attrs));
        }
        double LevelOf(string storeyId) => m.Get(storeyId)?.Num("elevation") ?? 0;

        var gated = new List<(string Id, string Cls, List<MeshBuilder> Body, List<Saved> Saved)>();
        var fromBodies = new List<string>();
        var count = 0;

        // One element offered: its reference (and its fills' references) taken away, the foreign element offered, the result kept to gate.
        string? Offer(ForeignElement fe, string refId, IReadOnlyList<string> fillIds, ArchicadElement el)
        {
            var saved = new[] { refId }.Concat(fillIds).Select(m.Get).OfType<Entity>().Select(x => new Saved(x, Related(m, x.Id))).ToList();
            foreach (var s in saved) { Remember(s.E); edit.Remove(s.E.Id); }
            string? made = null;
            var before = ctx.Skipped.ToDictionary(kv => kv.Key, kv => kv.Value);
            foreach (var r in recognizers.Where(r => r.Takes(fe.IfcClass)))
            {
                try { made = r.Recognize(edit, fe, ctx); }
                catch (CommandException ex) { ctx.Skip(ex.Message); made = null; }
                if (made != null) break;
            }
            if (made == null)
            {
                foreach (var s in saved) Restore(edit, s);
                var why = ctx.Skipped.Where(kv => kv.Value > before.GetValueOrDefault(kv.Key)).Select(kv => kv.Key).FirstOrDefault() ?? NoModule;
                Keep(why);
                Stamp(refId, "reference", why);
                return null;
            }
            // Made: what Archicad said of it kept, its group as it was.
            foreach (var s in saved)
                if (m.Get(s.E.Id) is { } now)
                    foreach (var (c, rel) in s.By) if (c != IfcClass.RelContainedInSpatialStructure && m.Get(rel) != null) edit.RelateAll(c, rel, [s.E.Id]);
            // A beam runs as Archicad's body does: through the columns it meets (Archicad's 3D shows where it ends, not Lapin's join).
            if (fe.IfcClass == IfcClass.Beam && m.Get(made) is { } beam)
            {
                var ba = beam.CloneAttrs();
                ba[ThroughColumnsKey] = true;
                edit.Replace(beam.With(attrs: ba));
            }
            if (gate.On) gated.Add((made, fe.IfcClass, ArchicadRead.Meshes(el.Bodies, shift), saved));
            return made;
        }

        string StoreyOf(string id) => Spatial.StoreyOf(m, id)?.Id ?? "";

        // Slabs first (so a wall is measured with the slabs about it native), then walls with their doors and windows, columns, beams, zones.
        var order = new[] { "Slab", "Wall", "Column", "Beam", "Zone" };
        var offeredCount = 0;
        foreach (var type in order)
            foreach (var (id, el) in offered.Where(x => x.El.Type == type).ToList())
            {
                if (m.Get(id) is not { } refEntity || !StoredBodies.Has(refEntity) && el.Type != "Zone") continue;
                offeredCount++;
                var storeyId = StoreyOf(id);
                var level = LevelOf(storeyId);
                (ForeignElement? fe, string? why) built;
                var fillIds = new List<string>();
                switch (type)
                {
                    case "Wall":
                        var fills = new List<(ArchicadElement, string)>();
                        foreach (var f in offered.Where(x => ArchicadReceive.IsFill(x.El) && ArchicadReceive.HostOf(x.El) == el.Guid && m.Get(x.Id) != null))
                        {
                            fills.Add((f.El, f.Id));
                            fillIds.Add(f.Id);
                        }
                        // The wall's story, not where its fills were put: Archicad places a wall on its home story.
                        built = ArchicadForeign.Wall(a, el, id, storeyId, (a.Story(el.Floor)?.Level ?? level - shift.Z), fills, shift);
                        break;
                    case "Slab": built = ArchicadForeign.Slab(a, el, id, storeyId, a.Story(el.Floor)?.Level ?? level - shift.Z, shift); break;
                    case "Column":
                        built = ArchicadForeign.Column(a, el, id, storeyId, a.Story(el.Floor)?.Level ?? level - shift.Z, shift);
                        if (built.fe == null && !ArchicadForeign.Basic(el.Details)) { fromBodies.Add(id); continue; }
                        break;
                    case "Beam":
                        built = ArchicadForeign.Beam(a, el, id, storeyId, shift);
                        if (built.fe == null && !ArchicadForeign.Basic(el.Details)) { fromBodies.Add(id); continue; }
                        break;
                    default: built = ArchicadForeign.Zone(el, id, storeyId, a.Story(el.Floor)?.Level ?? level - shift.Z, shift); break;
                }
                if (built.fe is not { } fe)
                {
                    Keep(built.why!);
                    Stamp(id, "reference", built.why);
                    foreach (var f in fillIds) { Keep("in a wall that stays a reference"); Stamp(f, "reference", "in a wall that stays a reference"); }
                    continue;
                }
                var made = Offer(fe, id, fillIds, el);
                if (made == null)
                {
                    foreach (var f in fillIds) { Keep("in a wall that stays a reference"); Stamp(f, "reference", "in a wall that stays a reference"); }
                    continue;
                }
                if (type != "Wall" || fillIds.Count == 0) continue;
                // Its doors and windows, each measured against Archicad's body for it: one that differs makes the wall again with its opening bare.
                var failed = new HashSet<string>();
                foreach (var fid in fillIds)
                {
                    if (m.Get(fid) is not { } native || StoredBodies.Has(native) || !elOf.TryGetValue(fid, out var fx)) { failed.Add(fid); continue; }
                    var check = RecognitionFidelity.Check(fid, native.Class, native.Class, native.Name, ArchicadRead.Meshes(fx.Bodies, shift),
                        RecognitionFidelity.Meshes(RecognitionFidelity.NativeGeometry(native, m, services)), gate, source: "archicad");
                    if (!(check.Measured && check.Pass)) failed.Add(fid);
                }
                if (failed.Count == 0) { count += fillIds.Count; continue; }
                // The wall taken back, made again with those openings bare, their doors and windows the references they were.
                var last = gated[^1];
                gated.RemoveAt(gated.Count - 1);
                edit.Remove(made);
                foreach (var fid in fillIds) if (m.Get(fid) != null) edit.Remove(fid);
                foreach (var s in last.Saved) Restore(edit, s);
                var bare = fe with { Openings = [.. fe.Openings.Select(op => op.Fill != null && failed.Contains(op.Fill.GlobalId) ? op with { Fill = null } : op)] };
                var keepFills = fillIds.Where(f => !failed.Contains(f)).ToList();
                var again = Offer(bare, id, keepFills, el);
                foreach (var f in failed)
                {
                    Keep(FillDiffers);
                    Stamp(f, "reference", FillDiffers);
                    // The door or window stands in its native opening: the opening is the wall's, the reference its fill.
                    if (again != null && m.Get(f) is { } fillRef && m.RelatedOf(again, IfcClass.RelVoidsElement).FirstOrDefault(o => o.Id == ArchicadForeign.Derived(f, "opening")) is { } opening)
                        Tag(edit, opening.Id, (JsonObject?)meta.GetValueOrDefault(f).Meta);
                }
                if (again == null) foreach (var f in keepFills) { Keep("in a wall that stays a reference"); Stamp(f, "reference", "in a wall that stays a reference"); }
                else count += keepFills.Count;
            }
        // What is left: each offered element Lapin makes no native element of, said by its type.
        foreach (var (id, el) in offered)
            if (!order.Contains(el.Type) && !ArchicadReceive.IsFill(el)) { Keep($"{NotOffered}: {Label(el.Type)}"); Stamp(id, "reference", NotOffered); }
            else if (ArchicadReceive.IsFill(el) && m.Get(id) is { } fr && StoredBodies.Has(fr) && (fr.Attrs[ArchicadReceive.Key] as JsonObject)?["recognition"] == null)
            { Keep("a door or window without its host wall"); Stamp(id, "reference", "a door or window without its host wall"); }

        foreach (var r in recognizers) r.Finish(edit, ctx);

        // The gate: each native element measured against Archicad's body, joins settled, what differs taken back under "enforce".
        FidelityReport? report = null;
        if (gated.Count > 0)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var checks = new List<FidelityCheck>();
            foreach (var (id, cls, body, _) in gated)
            {
                if (m.Get(id) is not { } native) { checks.Add(new(id, cls, "", null, false, true, null, null, null, null, "absorbed into another element", null, "archicad")); continue; }
                checks.Add(RecognitionFidelity.Check(id, cls, native.Class, native.Name, body, RecognitionFidelity.Meshes(RecognitionFidelity.NativeGeometry(native, m, services)), gate,
                    source: "archicad", joinedTo: RecognitionFidelity.JoinedTo(m, id)));
            }
            checks = RecognitionFidelity.Settle(checks, gate);
            for (var i = 0; i < checks.Count && gate.Enforcing; i++)
            {
                if (!checks[i].Measured || checks[i].Pass) continue;
                var (id, _, _, saved) = gated[i];
                edit.Remove(id);
                foreach (var s in saved)
                {
                    if (m.Get(s.E.Id) != null) edit.Remove(s.E.Id);
                    Restore(edit, s);
                }
                var what = string.Join(", ", (checks[i].Reason ?? "").Split(';').Select(x => System.Text.RegularExpressions.Regex.Replace(x, @"[-+−]?[0-9][0-9.,]*\s*(%|mm)?", "").Trim())
                    .Select(x => System.Text.RegularExpressions.Regex.Replace(x, @"\s+", " ")).Where(x => x.Length > 0).Distinct());
                var why = what.Length > 0 ? $"{Differs}: {what}" : Differs;
                Keep(why);
                foreach (var s in saved) Stamp(s.E.Id, "reference", why);
                checks[i] = checks[i] with { Reverted = true };
            }
            RecognitionFidelity.DropUnusedTypes(edit, ctx);
            report = new FidelityReport(gate, checks, checks.Count(c => c.Reverted), sw.Elapsed.TotalMilliseconds);
        }
        TypeMappings.AfterImport(edit, services, ctx.CreatedTypes, null);

        // Columns and beams of a profile or several segments: their own sections, read from Archicad's body.
        if (fromBodies.Count > 0)
        {
            foreach (var id in fromBodies) if (m.Get(id) is { } e) Remember(e);
            var r = MeshRecognition.Recognize(edit, services, fromBodies, gate, outlineSections: true);
            foreach (var (why, n) in r.Skipped) Keep($"{why} (read from its body)", n);
            if (r.Fidelity?.Checks.Count(c => c.Reverted) is > 0 and var rv) Keep($"{Differs} (read from its body)", rv);
            report = report == null ? r.Fidelity : report.Then(r.Fidelity);
            foreach (var id in fromBodies) if (m.Get(id) is { } e && StoredBodies.Has(e)) Stamp(id, "reference", "its section read from its body is no section Lapin has");
        }

        // Native: provenance kept (the Archicad attribute, properties, classifications, source), cut priorities as Archicad's bodies show.
        var recognized = 0;
        var byClass = new Dictionary<string, int>();
        foreach (var (id, (mt, props, source, classes)) in meta)
        {
            if (m.Get(id) is not { } now || StoredBodies.Has(now) || mt is not JsonObject mo) continue;
            var attrs = now.CloneAttrs();
            var next = (JsonObject)mo.DeepClone();
            next["recognition"] = "native";
            next.Remove("why");
            attrs[ArchicadReceive.Key] = next;
            if (props != null) attrs["properties"] = props.DeepClone();
            if (source != null) attrs["source"] = source.DeepClone();
            if (classes != null) attrs[Classifications.Key] = classes.DeepClone();
            if (now.Class is IfcClass.Slab or IfcClass.Column) attrs["cutPriority"] = WallCutPriority;
            edit.Replace(now.With(attrs: attrs));
            recognized++;
            byClass[now.Class] = byClass.GetValueOrDefault(now.Class) + 1;
            // Its openings: tagged with their wall's model, so a later receive that removes the wall finds them too.
            foreach (var op in m.RelatedOf(id, IfcClass.RelVoidsElement).ToList()) Tag(edit, op.Id, mo);
        }
        return new ArchicadRecognitionReport(recognized, offeredCount, byClass, kept, report);
    }

    static void Restore(IEdit edit, Saved s)
    {
        if (edit.Model.Get(s.E.Id) != null) edit.Remove(s.E.Id);
        edit.Add(s.E);
        foreach (var (c, r) in s.By) if (edit.Model.Get(r) != null) edit.RelateAll(c, r, [s.E.Id]);
    }

    /// <summary>An opening a native wall has marked with the model it came from (as its door's or wall's), so a receive finds it.</summary>
    static void Tag(IEdit edit, string id, JsonObject? from)
    {
        if (from == null || edit.Model.Get(id) is not { } e) return;
        var attrs = e.CloneAttrs();
        var t = (JsonObject)from.DeepClone();
        t["role"] = "opening";
        t["recognition"] = "native";
        t.Remove("why");
        attrs[ArchicadReceive.Key] = t;
        edit.Replace(e.With(attrs: attrs));
    }

    static string Label(string type) => System.Text.RegularExpressions.Regex.Replace(type, "(?<=[a-z])(?=[A-Z])", " ").ToLowerInvariant();
}
