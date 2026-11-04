using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform.Cde;
using Lapin.Platform.Cde.Remarks;

namespace Lapin.Platform.Coordination;

/// <summary>
/// One MEP element where it crosses a wall, floor or roof: <see cref="OpeningClashes.Ok"/> (wholly inside a void, keeping
/// the clearance), <see cref="OpeningClashes.Missing"/> (through the host with no opening), <see cref="OpeningClashes.TooSmall"/>
/// (through an opening it does not fit, <see cref="By"/> metres out of it), or <see cref="OpeningClashes.Empty"/> (a void nothing goes through; no MEP element then).
/// </summary>
public sealed record OpeningClash(string Status, string? MepId, string HostId, OpeningVoid? Void, double By, Vec3 Centre, string Message)
{
    public bool IsClash => Status is OpeningClashes.Missing or OpeningClashes.TooSmall;
    /// <summary>A stable key for the pair: the same clash found again names the same remark.</summary>
    public string Key => $"{MepId ?? "-"}|{HostId}|{Void?.Key ?? "-"}|{Status}";
    /// <summary>The pair whatever its status (an empty void: the void), what a clash report follows an item by from one run to the next.</summary>
    public string PairKey => MepId != null ? $"{MepId}|{HostId}" : $"-|{HostId}|{Void?.Key}";
    /// <summary>Why the cut order's MEP rule did not cut this opening (decision 293): "structural" or "order"; null otherwise.</summary>
    public string? Held { get; init; }

    public JsonObject ToJson(IModelView m)
    {
        var o = new JsonObject
        {
            ["key"] = Key, ["status"] = Status, ["mepId"] = MepId, ["mep"] = MepId is { } id ? m.Get(id)?.Name : null, ["mepClass"] = MepId is { } c ? m.Get(c)?.Class : null,
            ["hostId"] = HostId, ["host"] = m.Get(HostId)?.Name, ["hostClass"] = m.Get(HostId)?.Class,
            ["by"] = Math.Round(By, 4), ["centre"] = new JsonArray(Math.Round(Centre.X, 4), Math.Round(Centre.Y, 4), Math.Round(Centre.Z, 4)), ["message"] = Message,
        };
        if (Void != null) { o["mark"] = Void.Mark; o["voidKind"] = Void.Kind; o["openingId"] = Void.OpeningId; o["selectId"] = Void.SelectId; }
        if (Held != null) o["held"] = Held;
        return o;
    }
}

/// <summary>
/// Openings against MEP (a tester's request, decision 289): every pipe, duct,
/// cable carrier or MEP provision for void — Lapin's runs and imported IFC
/// segments alike — that crosses a wall, floor or roof must pass through the
/// host inside a void, keeping the clearance asked on every side ("x completely
/// inside y"). Measured on the solids: bounding boxes first, then the element
/// intersected with the host as built (cut by its openings) and, with a
/// clearance, with the ring between the opening and its void shrunk by it; what
/// is left is the clash, its distance out of the void how much too small the opening is.
/// </summary>
public static class OpeningClashes
{
    public const string Ok = "ok", Missing = "missing", TooSmall = "tooSmall", Empty = "empty";

    /// <summary>The MEP classes checked: segments (Lapin's runs are pipe, duct and cable segments) and the MEP side's own openings, provisions for voids.</summary>
    public static readonly string[] MepClasses = ["IfcPipeSegment", "IfcDuctSegment", "IfcCableSegment", "IfcCableCarrierSegment", "IfcFlowSegment"];
    /// <summary>The hosts an MEP element must go through by a void.</summary>
    public static readonly string[] HostClasses = [IfcClass.Wall, IfcClass.Slab, IfcClass.Roof];

    /// <summary>What is less than this is touching, not a clash (m³ of overlap; m out of the void).</summary>
    const double VolumeEps = 1e-8, DistanceEps = 0.0005;

    public static bool IsMep(Entity e) =>
        MepClasses.Contains(e.Class) || e.Class == IfcClass.BuildingElementProxy && e.PredefinedType == "PROVISIONFORVOID";

    public sealed record Options(double Clearance = 0, bool ReportEmpty = false)
    {
        public static Options From(IModelView m, JsonObject? a)
        {
            var s = OpeningVoids.Settings(m);
            var clearance = Json.Number(a?["clearance"]) ?? Json.Number(s["clearance"]) ?? 0;
            var empty = (a?["empty"] ?? s["empty"]) is JsonValue ev && ev.TryGetValue<bool>(out var e) && e;
            return new(Math.Clamp(clearance, 0, 0.5), empty);
        }
    }

    sealed record Body(Entity Entity, List<Solid> Solids, (Vec3 Min, Vec3 Max) Box);

    static Body? BodyOf(IQueryContext q, Entity e)
    {
        ElementGeometry? g;
        try { g = q.Geometry(e.Id); }
        catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException) { return null; }
        var solids = g?.Parts.Select(p => p.Solid).Where(s => s.Faces.Length > 0).ToList() ?? [];
        if (solids.Count == 0) return null;
        var boxes = solids.Select(Measure.Bounds).OfType<(Vec3 Min, Vec3 Max)>().ToList();
        if (boxes.Count == 0) return null;
        return new Body(e, solids, (new Vec3(boxes.Min(b => b.Min.X), boxes.Min(b => b.Min.Y), boxes.Min(b => b.Min.Z)), new Vec3(boxes.Max(b => b.Max.X), boxes.Max(b => b.Max.Y), boxes.Max(b => b.Max.Z))));
    }

    static bool Overlap((Vec3 Min, Vec3 Max) a, (Vec3 Min, Vec3 Max) b, double pad = 1e-6) =>
        a.Min.X <= b.Max.X + pad && b.Min.X <= a.Max.X + pad && a.Min.Y <= b.Max.Y + pad && b.Min.Y <= a.Max.Y + pad && a.Min.Z <= b.Max.Z + pad && b.Min.Z <= a.Max.Z + pad;

    static string Label(Entity e) => e.Name is { Length: > 0 } n ? n : e.Class.Replace("Ifc", "");

    /// <summary>Runs the check over the model: one result per MEP element and host it crosses (and per empty void when asked); clashes first.</summary>
    public static List<OpeningClash> Run(IQueryContext q, Options o)
    {
        var m = q.Model;
        var results = new List<OpeningClash>();
        var booleans = q.Services.Get<ISolidBooleans>();
        if (booleans == null) return results;
        // The voids as the openings are (to know which one an element goes through) and shrunk by the clearance (what it must stay inside).
        var exact = OpeningVoids.Of(q, VoidOptions.All);
        var shrunk = o.Clearance > 0 ? OpeningVoids.Of(q, VoidOptions.All with { Offset = -o.Clearance }).ToDictionary(v => v.Key) : exact.ToDictionary(v => v.Key);
        var voidsOf = exact.GroupBy(v => v.HostId).ToDictionary(g => g.Key, g => g.Select(v => (Void: v, Box: Measure.Bounds(v.Solid)!.Value)).ToList());
        var hosts = HostClasses.SelectMany(m.OfClass).Select(e => BodyOf(q, e)).OfType<Body>().ToList();
        var used = new HashSet<string>();
        // The provisions the cut order's MEP rule left uncut (a load-bearing host, a host ranked over the run): reported, with why.
        var held = Provisions.Of(m, q.Services).Where(p => !p.Cuts).GroupBy(p => (p.SourceId, p.HostId)).ToDictionary(g => g.Key, g => g.First().Held!);
        foreach (var e in m.Entities.Where(IsMep).ToList())
        {
            if (BodyOf(q, e) is not { } mep) continue;
            foreach (var host in hosts)
            {
                if (!Overlap(mep.Box, host.Box)) continue;
                // What of the element runs through the host's material: the host as built, its openings cut out of it.
                var pieces = new List<Solid>();
                foreach (var a in mep.Solids)
                    foreach (var b in host.Solids)
                        if (Measure.Bounds(a) is { } ba && Measure.Bounds(b) is { } bb && Overlap(ba, bb) && booleans.Intersection(a, b) is { } x && Measure.Volume(x) > VolumeEps) pieces.Add(x);
                // The voids it goes through, and with a clearance what of it lies in the ring the clearance keeps clear.
                var through = new List<OpeningVoid>();
                foreach (var (v, box) in voidsOf.GetValueOrDefault(host.Entity.Id) ?? [])
                {
                    if (!Overlap(mep.Box, box)) continue;
                    var inside = new List<Solid>();
                    foreach (var a in mep.Solids)
                        if (booleans.Intersection(a, v.Solid) is { } x && Measure.Volume(x) > VolumeEps) inside.Add(x);
                    if (inside.Count == 0) continue;
                    through.Add(v);
                    used.Add(v.Key);
                    if (o.Clearance > 0 && shrunk.TryGetValue(v.Key, out var inner))
                        foreach (var x in inside)
                            if (booleans.Difference(x, [inner.Solid]) is { } ring && Measure.Volume(ring) > VolumeEps) pieces.Add(ring);
                }
                if (pieces.Count == 0 && through.Count == 0) continue;
                var centre = Centre(pieces.Count > 0 ? pieces : through.Select(v => v.Solid).ToList());
                if (pieces.Count == 0)
                {
                    var ok = through[0];
                    results.Add(new OpeningClash(Ok, e.Id, host.Entity.Id, ok, 0, centre, $"{Label(e)} goes through {ok.Mark} in {Label(host.Entity)}."));
                    continue;
                }
                if (through.Count == 0)
                {
                    var why = held.GetValueOrDefault((e.Id, host.Entity.Id));
                    var note = why == ProvisionOpening.Structural ? " It bears load: not cut automatically." : why == ProvisionOpening.Order ? " It ranks over the run in the cut order: not cut automatically." : "";
                    results.Add(new OpeningClash(Missing, e.Id, host.Entity.Id, null, 0, centre, $"No opening: {Label(e)} goes through {Label(host.Entity)} where it has none.{note}") { Held = why });
                    continue;
                }
                // How far out of the void (shrunk by the clearance) the element reaches: what the opening lacks on its worst side.
                var target = through.Select(v => shrunk.GetValueOrDefault(v.Key) ?? v).ToList();
                var by = pieces.SelectMany(p => p.Vertices).Max(p => target.Min(v => DistanceTo(v.Solid, p)));
                if (by < DistanceEps)
                {
                    results.Add(new OpeningClash(Ok, e.Id, host.Entity.Id, through[0], 0, centre, $"{Label(e)} goes through {through[0].Mark} in {Label(host.Entity)}."));
                    continue;
                }
                var mm = Math.Ceiling(by * 1000 - 0.05);
                results.Add(new OpeningClash(TooSmall, e.Id, host.Entity.Id, through[0], by, centre,
                    $"Opening too small: {Label(e)} stands {mm:0} mm out of {through[0].Mark} in {Label(host.Entity)}{(o.Clearance > 0 ? $" (keeping {o.Clearance * 1000:0} mm clear)" : "")}."));
            }
        }
        if (o.ReportEmpty)
            foreach (var v in exact.Where(v => !used.Contains(v.Key) && v.Kind is OpeningVoids.Wall or OpeningVoids.Slab && !IsStairwell(m, v)))
                results.Add(new OpeningClash(Empty, null, v.HostId, v, 0, Centre([v.Solid]), $"{v.Mark} in {(m.Get(v.HostId) is { } h ? Label(h) : "?")}: nothing goes through it."));
        return results.OrderBy(r => r.Status switch { Missing => 0, TooSmall => 1, Empty => 2, _ => 3 }).ToList();
    }

    /// <summary>A floor's opening a stair asked for: a stairwell, empty by design.</summary>
    static bool IsStairwell(IModelView m, OpeningVoid v) => v.OpeningId is { } id && m.Get(id)?.Str("stairId") != null;

    static Vec3 Centre(IReadOnlyList<Solid> solids)
    {
        var boxes = solids.Select(Measure.Bounds).OfType<(Vec3 Min, Vec3 Max)>().ToList();
        if (boxes.Count == 0) return new Vec3(0, 0, 0);
        var min = new Vec3(boxes.Min(b => b.Min.X), boxes.Min(b => b.Min.Y), boxes.Min(b => b.Min.Z));
        var max = new Vec3(boxes.Max(b => b.Max.X), boxes.Max(b => b.Max.Y), boxes.Max(b => b.Max.Z));
        return (min + max) * 0.5;
    }

    /// <summary>A point's distance to a solid's surface (0 on it): the nearest of its triangles.</summary>
    public static double DistanceTo(Solid s, Vec3 p)
    {
        var t = Tessellator.Tessellate(s, 90);
        var best = double.MaxValue;
        for (var i = 0; i + 2 < t.Indices.Length; i += 3)
        {
            Vec3 V(int k) => new(t.Positions[k * 3], t.Positions[k * 3 + 1], t.Positions[k * 3 + 2]);
            best = Math.Min(best, PointTriangle(p, V(t.Indices[i]), V(t.Indices[i + 1]), V(t.Indices[i + 2])));
        }
        return best;
    }

    /// <summary>The distance from a point to a triangle (Ericson, Real-Time Collision Detection, 5.1.5).</summary>
    public static double PointTriangle(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
    {
        Vec3 ab = b - a, ac = c - a, ap = p - a;
        double d1 = Vec3.Dot(ab, ap), d2 = Vec3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return (p - a).Length;
        var bp = p - b;
        double d3 = Vec3.Dot(ab, bp), d4 = Vec3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return (p - b).Length;
        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return (p - (a + ab * (d1 / (d1 - d3)))).Length;
        var cp = p - c;
        double d5 = Vec3.Dot(ab, cp), d6 = Vec3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return (p - c).Length;
        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return (p - (a + ac * (d2 / (d2 - d6)))).Length;
        var va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return (p - (b + (c - b) * ((d4 - d3) / (d4 - d3 + (d5 - d6))))).Length;
        var denom = 1 / (va + vb + vc);
        return (p - (a + ab * (vb * denom) + ac * (vc * denom))).Length;
    }

    public static JsonObject ToJson(IModelView m, List<OpeningClash> results, bool booleans) => new()
    {
        ["results"] = new JsonArray(results.Select(r => (JsonNode)r.ToJson(m)).ToArray()),
        ["counts"] = new JsonObject
        {
            [Ok] = results.Count(r => r.Status == Ok), [Missing] = results.Count(r => r.Status == Missing),
            [TooSmall] = results.Count(r => r.Status == TooSmall), [Empty] = results.Count(r => r.Status == Empty),
        },
        ["available"] = booleans,
    };

    /// <summary>What a remark is made of: the pair, its status, how far out, where, what it says, the void's mark and opening, the report's number.</summary>
    public sealed record RemarkSource(string MepId, string HostId, string Status, double By, Vec3 Centre, string Message, string? Mark, string? OpeningId, int? Number = null)
    {
        public static RemarkSource Of(OpeningClash c) => new(c.MepId!, c.HostId, c.Status, c.By, c.Centre, c.Message, c.Void?.Mark, c.Void?.OpeningId);
    }

    /// <summary>
    /// The clashes as remarks (BCF topics of type Clash), one per element and host: its GUID derived from the pair, so a check
    /// run again updates the remark it made (title, description; its comments and status kept) instead of adding another.
    /// The viewpoint selects both and looks at the clash from 3 m away. Returns the remarks' ids too.
    /// </summary>
    public static (int Added, int Updated) ToRemarks(ICommandContext ctx, IEnumerable<OpeningClash> clashes, string author) =>
        ToRemarks(ctx, clashes.Where(c => c.IsClash).Select(RemarkSource.Of), author, out _);

    public static (int Added, int Updated) ToRemarks(ICommandContext ctx, IEnumerable<RemarkSource> clashes, string author, out List<string> ids)
    {
        int added = 0, updated = 0;
        ids = [];
        foreach (var c in clashes)
        {
            var guid = Model.GlobalId.ToGuid(OpeningVoids.DerivedId($"{c.MepId}|{c.HostId}", "clash"));
            var id = Remarks.IdOf(guid);
            var title = c.Status == Missing ? $"No opening: {ctx.Model.Get(c.MepId)?.Name} / {ctx.Model.Get(c.HostId)?.Name}" : $"Opening too small: {c.Mark} ({Math.Ceiling(c.By * 1000 - 0.05):0} mm)";
            var description = c.Number is { } n ? $"Clash {n}: {c.Message}" : c.Message;
            var eye = c.Centre + new Vec3(-2, -2, 1.5);
            var dir = (c.Centre - eye).Normalized();
            var right = Vec3.Cross(dir, new Vec3(0, 0, 1)).Normalized();
            var up = Vec3.Cross(right, dir).Normalized();
            var selection = new List<string> { c.MepId, c.HostId };
            if (c.OpeningId is { } op) selection.Add(op);
            var now = Bcf.Now();
            if (ctx.Model.Get(id) is { Class: Remarks.Class } existing)
            {
                var t = Remarks.Read(existing);
                ctx.Replace(Remarks.ToEntity(t with { Title = title, Description = description, ModifiedDate = now, ModifiedAuthor = author }));
                updated++;
                ids.Add(id);
                continue;
            }
            if (ctx.Model.Get(id) != null) continue;
            var vp = new BcfViewpoint(Guid.NewGuid(), new BcfCamera(true, P(eye), P(dir), P(up), 60, 1), selection, true, [], [], [], null);
            ctx.Add(Remarks.ToEntity(new BcfTopic(guid, title, description, "Clash", "Open", "Normal", null, now, author, null, null, null, null, null, ["Openings vs MEP"], [], [vp])));
            added++;
            ids.Add(id);
        }
        return (added, updated);
    }

    static BcfPoint P(Vec3 v) => new(v.X, v.Y, v.Z);

    public static void Register(IModuleRegistry r)
    {
        // The check: {clearance?, empty?} over the project's settings; the results, clashes first, and their counts.
        r.Query("coordination.clashes", (q, a) => ToJson(q.Model, Run(q, Options.From(q.Model, a)), q.Services.Get<ISolidBooleans>() != null));
        // The clashes as remarks of type Clash (Manage → Remarks, BCF): {clearance?, author?}; how many were added and updated.
        // With a clash report (decision 293): its open clashes, numbered, without running the check again; else the check is run.
        r.Command("coordination.remarks", "Clashes as remarks", (ctx, a) =>
        {
            var author = ((string?)a["author"])?.Trim() is { Length: > 0 } s ? s : "Lapin";
            var sources = ClashReports.Read(ctx.Model) is { } report
                ? report.Items.Where(i => i.State == ClashReports.Open && i.MepId != null && i.Kind is Missing or TooSmall)
                    .Select(i => new RemarkSource(i.MepId!, i.HostId, i.Kind, i.By, i.Centre, i.Message, i.Mark, i.OpeningId, i.Number)).ToList()
                : Run(ctx.Query, Options.From(ctx.Model, a)).Where(c => c.IsClash).Select(RemarkSource.Of).ToList();
            var (added, updated) = ToRemarks(ctx, sources, author, out var ids);
            return new JsonObject { ["added"] = added, ["updated"] = updated, ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()) };
        });
        // In Manage → Check: the clash report's open clashes, never the check itself (decision 293: it runs only when asked).
        r.Service<ICdeRule>(new MepRule());
    }

    sealed class MepRule : ICdeRule
    {
        public string Id => "openingsMep";
        public string Label => "Openings vs MEP";
        public bool Blocking => false;
        public IEnumerable<CdeFinding> Run(CdeCheckContext c) => ClashReports.Findings(c.Model, Id);
    }
}
