using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform.Cde;

namespace Lapin.Platform.Coordination;

/// <summary>
/// One problem of a clash report: its number (kept from run to run), its kind (as <see cref="OpeningClashes"/> names it),
/// the pair, how far out, where, what it says, and what is being done about it: <see cref="State"/> open, fixed or
/// ignored (with a comment).
/// </summary>
public sealed record ClashItem(int Number, string Key, string Kind, string? MepId, string HostId, string? Mark, string? OpeningId, string? VoidKey,
    double By, Vec3 Centre, string Message, string State, string? Comment = null, string? Held = null, string? FixedBy = null, string? ResolvedAt = null)
{
    public JsonObject ToJson(IModelView? m = null)
    {
        var o = new JsonObject
        {
            ["n"] = Number, ["key"] = Key, ["kind"] = Kind, ["mepId"] = MepId, ["hostId"] = HostId, ["mark"] = Mark, ["openingId"] = OpeningId, ["voidKey"] = VoidKey,
            ["by"] = Math.Round(By, 4), ["centre"] = new JsonArray(Math.Round(Centre.X, 4), Math.Round(Centre.Y, 4), Math.Round(Centre.Z, 4)),
            ["message"] = Message, ["state"] = State,
        };
        if (Comment != null) o["comment"] = Comment;
        if (Held != null) o["held"] = Held;
        if (FixedBy != null) o["fixedBy"] = FixedBy;
        if (ResolvedAt != null) o["resolvedAt"] = ResolvedAt;
        if (m != null)
        {
            o["mep"] = MepId is { } id ? m.Get(id)?.Name : null;
            o["mepClass"] = MepId is { } c ? m.Get(c)?.Class : null;
            o["host"] = m.Get(HostId)?.Name;
            o["hostClass"] = m.Get(HostId)?.Class;
        }
        return o;
    }

    public static ClashItem? From(JsonNode? n)
    {
        if (n is not JsonObject o || (string?)o["key"] is not { } key || (string?)o["hostId"] is not { } host) return null;
        var c = o["centre"] as JsonArray;
        Vec3 centre = c is { Count: 3 } ? new(Json.Number(c[0]) ?? 0, Json.Number(c[1]) ?? 0, Json.Number(c[2]) ?? 0) : new(0, 0, 0);
        return new((int)(Json.Number(o["n"]) ?? 0), key, (string?)o["kind"] ?? OpeningClashes.Missing, (string?)o["mepId"], host, (string?)o["mark"], (string?)o["openingId"], (string?)o["voidKey"],
            Json.Number(o["by"]) ?? 0, centre, (string?)o["message"] ?? "", (string?)o["state"] ?? ClashReports.Open, (string?)o["comment"], (string?)o["held"], (string?)o["fixedBy"], (string?)o["resolvedAt"]);
    }
}

/// <summary>A clash report as it was kept: when it ran, the model's state then, the clearance, its items.</summary>
public sealed record ClashReport(string Id, string Date, string Hash, double Clearance, int Ok, IReadOnlyList<ClashItem> Items);

/// <summary>
/// The openings-vs-MEP check run on demand (decision 293, the user's direction: "the clash check only when asked: one
/// run, a report of the problems, then the correction; the cut order should solve it from the start"): Run clash check
/// runs <see cref="OpeningClashes"/> once and keeps its result as the project's clash report (one <see cref="Class"/>
/// entity: the date, a hash of the model's state, the counts, the items) until it is run again. Nothing runs it by
/// itself: Manage → Check reads the report (<see cref="Findings"/>), and the report says when the model has changed
/// since. A run again follows each item by its pair: its number and an ignore are kept, what is no longer found is
/// marked fixed. Items are fixed from the report (an opening made or enlarged round the MEP element through the
/// module that owns the host, <see cref="IOpeningMaker"/>) or ignored with a comment, each as one undo step.
/// </summary>
public static class ClashReports
{
    public const string Class = "LapinClashReport";
    public const string Open = "open", Fixed = "fixed", Ignored = "ignored";
    public static readonly string[] States = [Open, Fixed, Ignored];

    /// <summary>What is not the model a clash report checks: its own report, remarks, views, sheets, annotations, issues.</summary>
    static readonly HashSet<string> NotModel = [Class, Cde.Remarks.Remarks.Class, "LapinSheetRemark", Views.Class, Sheets.Class, "LapinIssue", IfcClass.Annotation, "LapinViewBoundary", "LapinInset", "LapinImage", "LapinSchedule", "LapinVersion", "LapinClashTest", "LapinSearchSet"];

    /// <summary>
    /// The model's state as a clash check sees it: a SHA-256 over every element, type, material and relation (sorted by
    /// id), leaving out what only documents it (views, sheets, annotations, remarks, issues, the report itself, the clash module's tests) and of the
    /// project only its coordination settings. The same model gives the same hash, saved and opened again.
    /// </summary>
    public static string ModelHash(IModelView m)
    {
        var skip = new HashSet<string>();
        var rows = new List<string>();
        foreach (var e in m.Entities)
        {
            if (NotModel.Contains(e.Class)) { skip.Add(e.Id); continue; }
            var attrs = e.Class == IfcClass.Project ? (e.Attrs[OpeningVoids.Key]?.ToJsonString() ?? "") : e.Attrs.ToJsonString();
            rows.Add($"{e.Id}|{e.Class}|{e.Name}|{e.PredefinedType}|{attrs}");
        }
        foreach (var r in m.Relations)
        {
            if (skip.Contains(r.Relating) || r.Related.Any(skip.Contains)) continue;
            rows.Add($"{r.Id}|{r.Class}|{r.Relating}|{string.Join(",", r.Related)}|{r.Attrs?.ToJsonString()}");
        }
        rows.Sort(StringComparer.Ordinal);
        var sb = new StringBuilder();
        foreach (var row in rows) sb.Append(row).Append('\n');
        return CdeTransmittal.Sha256(sb.ToString());
    }

    public static ClashReport? Read(IModelView m)
    {
        if (m.OfClass(Class).FirstOrDefault() is not { } e) return null;
        var items = (e.Attrs["items"] as JsonArray ?? []).Select(ClashItem.From).OfType<ClashItem>().ToList();
        return new ClashReport(e.Id, e.Str("date") ?? "", e.Str("hash") ?? "", e.Num("clearance"), (int)e.Num("ok"), items);
    }

    static Dictionary<string, int> Counts(IEnumerable<ClashItem> items, int ok)
    {
        var list = items.ToList();
        var open = list.Where(i => i.State == Open).ToList();
        return new()
        {
            [OpeningClashes.Missing] = open.Count(i => i.Kind == OpeningClashes.Missing && i.Held == null),
            ["held"] = open.Count(i => i.Kind == OpeningClashes.Missing && i.Held != null),
            [OpeningClashes.TooSmall] = open.Count(i => i.Kind == OpeningClashes.TooSmall),
            [OpeningClashes.Empty] = open.Count(i => i.Kind == OpeningClashes.Empty),
            [OpeningClashes.Ok] = ok,
            [Open] = open.Count, [Fixed] = list.Count(i => i.State == Fixed), [Ignored] = list.Count(i => i.State == Ignored),
        };
    }

    /// <summary>The report as the interface reads it, with whether the model changed since (<c>stale</c>); null when none was run.</summary>
    public static JsonObject? ToJson(IModelView m, ClashReport? r)
    {
        if (r == null) return null;
        var counts = new JsonObject();
        foreach (var (k, v) in Counts(r.Items, r.Ok)) counts[k] = v;
        return new JsonObject
        {
            ["id"] = r.Id, ["date"] = r.Date, ["hash"] = r.Hash, ["stale"] = ModelHash(m) != r.Hash, ["clearance"] = r.Clearance, ["counts"] = counts,
            ["items"] = new JsonArray(r.Items.OrderBy(i => i.Number).Select(i => (JsonNode)i.ToJson(m)).ToArray()),
        };
    }

    static void Write(ICommandContext ctx, ClashReport? old, string hash, double clearance, int ok, IReadOnlyList<ClashItem> items, string? date = null)
    {
        var attrs = new JsonObject
        {
            ["date"] = date ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture), ["hash"] = hash, ["clearance"] = clearance, ["ok"] = ok,
            ["items"] = new JsonArray(items.OrderBy(i => i.Number).Select(i => (JsonNode)i.ToJson()).ToArray()),
        };
        var counts = new JsonObject();
        foreach (var (k, v) in Counts(items, ok)) counts[k] = v;
        attrs["counts"] = counts;
        if (old != null && ctx.Model.Get(old.Id) is { } e) ctx.Replace(e.With(attrs: attrs));
        else ctx.Add(new Entity(ctx.NewId(), Class, "Clash report", attrs));
    }

    /// <summary>
    /// The check run once and kept: each clash (and each empty void when asked) an item, followed from the last run by
    /// its pair (its number, an ignore and its comment kept; a fixed one that clashes again open again), what the last
    /// run had and this one no longer finds marked fixed.
    /// </summary>
    public static ClashReport Run(ICommandContext ctx, OpeningClashes.Options o)
    {
        var m = ctx.Model;
        var date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var hash = ModelHash(m);
        var found = OpeningClashes.Run(ctx.Query, o);
        var old = Read(m);
        var before = old?.Items.ToDictionary(i => i.Key) ?? [];
        var next = before.Count == 0 ? 1 : before.Values.Max(i => i.Number) + 1;
        var items = new List<ClashItem>();
        var seen = new HashSet<string>();
        foreach (var c in found.Where(c => c.IsClash || c.Status == OpeningClashes.Empty))
        {
            if (!seen.Add(c.PairKey)) continue;
            var prev = before.GetValueOrDefault(c.PairKey);
            var state = prev?.State == Ignored ? Ignored : Open;
            items.Add(new ClashItem(prev?.Number ?? next++, c.PairKey, c.Status, c.MepId, c.HostId, c.Void?.Mark, c.Void?.OpeningId, c.Void?.Key, c.By, c.Centre, c.Message, state, prev?.Comment, c.Held));
        }
        // What the last run found and this one does not: resolved (an ignored one stays ignored), its message and sizes as they are now (decision 296):
        // the opening it goes through and the run's size in it, or that the pair no longer meets; the run it was first found resolved at kept.
        var fits = found.Where(c => c.Status == OpeningClashes.Ok).GroupBy(c => c.PairKey).ToDictionary(g => g.Key, g => g.First());
        foreach (var prev in before.Values.Where(p => !seen.Contains(p.Key)))
        {
            if (prev.State == Ignored) { items.Add(prev); continue; }
            var at = prev.State == Fixed && prev.ResolvedAt != null ? prev.ResolvedAt : date;
            var fit = fits.GetValueOrDefault(prev.Key);
            items.Add(prev with
            {
                State = Fixed, ResolvedAt = at, By = 0, Message = ResolvedMessage(m, prev, fit, at),
                Mark = fit?.Void?.Mark ?? prev.Mark, OpeningId = fit?.Void?.OpeningId ?? prev.OpeningId, VoidKey = fit?.Void?.Key ?? prev.VoidKey, Centre = fit?.Centre ?? prev.Centre,
            });
        }
        var ok = found.Count(c => c.Status == OpeningClashes.Ok);
        Write(ctx, old, hash, o.Clearance, ok, items, date);
        return Read(ctx.Model)!;
    }

    /// <summary>
    /// A resolved item's message as the run finds it (decision 296): "Resolved at the run of 04.11.2026 14:00: opening 160 mm, run 110 mm"
    /// (a rectangle's width × height; the run's size in the opening, as it passes), or that the pair no longer meets.
    /// </summary>
    public static string ResolvedMessage(IModelView m, ClashItem item, OpeningClash? fit, string at)
    {
        var head = $"Resolved at the run of {Day(at)}";
        if (fit?.Void is { } v)
        {
            var opening = $"opening {(v.Mark is { Length: > 0 } mark ? mark + " " : "")}{Mm(v.Width, v.Shape == OpeningVoids.Round ? null : v.Height)}";
            return fit.Fit is { } size ? $"{head}: {opening}, run {Mm(size.W, size.H)}." : $"{head}: {opening}.";
        }
        var mep = item.MepId is { } id ? m.Get(id) : null;
        var host = m.Get(item.HostId);
        if (mep == null) return $"{head}: the MEP element is gone.";
        if (host == null) return $"{head}: its host is gone.";
        return $"{head}: {Name(mep)} no longer goes through {Name(host)}.";
    }

    static string Name(Entity e) => e.Name is { Length: > 0 } n ? n : e.Class.Replace("Ifc", "");

    /// <summary>A size in whole mm: one number when both ways match (a circle, a square), else width × height.</summary>
    static string Mm(double w, double? h)
    {
        static string R(double x) => Math.Round(x * 1000).ToString("0", CultureInfo.InvariantCulture);
        return h is not { } hh || Math.Abs(w - hh) < 0.0015 ? $"{R(w)} mm" : $"{R(w)} × {R(hh)} mm";
    }

    /// <summary>The report's items with these numbers, refused when there is no report or a number is not in it.</summary>
    static (ClashReport Report, List<ClashItem> Items) Pick(IModelView m, JsonNode? numbers)
    {
        var report = Read(m) ?? throw new CommandException("Run the clash check first.");
        var want = (numbers as JsonArray ?? []).Select(x => Json.Number(x)).OfType<double>().Select(x => (int)x).ToHashSet();
        if (want.Count == 0) throw new CommandException("Give the numbers of the clashes.");
        var items = report.Items.Where(i => want.Contains(i.Number)).ToList();
        if (items.Count != want.Count) throw new CommandException($"The report has no clash {string.Join(", ", want.Except(items.Select(i => i.Number)))}.");
        return (report, items);
    }

    static void Update(ICommandContext ctx, ClashReport report, IReadOnlyDictionary<int, ClashItem> changed) =>
        Write(ctx, report, report.Hash, report.Clearance, report.Ok, report.Items.Select(i => changed.GetValueOrDefault(i.Number) ?? i).ToList(), report.Date);

    /// <summary>
    /// What of an MEP element lies in a host: its solids within the host's envelope (the host whole, its openings not cut),
    /// else within the host as built. What an opening is made round.
    /// </summary>
    public static List<Solid> Through(IQueryContext q, Entity mep, Entity host)
    {
        var booleans = q.Services.Get<ISolidBooleans>() ?? throw new CommandException("Making openings needs the boolean library (manifold), missing here.");
        var mine = q.Geometry(mep.Id)?.Parts.Select(p => p.Solid).Where(s => s.Faces.Length > 0).ToList() ?? [];
        var envelope = q.Services.Get<ICutters>()?.EnvelopeOf(host, q.Model) is { } env ? [env] : q.Geometry(host.Id)?.Parts.Select(p => p.Solid).ToList() ?? [];
        var o = new List<Solid>();
        foreach (var a in mine)
            foreach (var b in envelope)
                if (booleans.Intersection(a, b) is { } x && Measure.Volume(x) > 1e-9) o.Add(x);
        return o;
    }

    /// <summary>Whether an MEP element is round (a pipe, a cable, a round duct): its opening is round; a duct with a height, a tray: rectangular.</summary>
    public static bool RoundOf(Entity mep) => mep.Class switch
    {
        "IfcDuctSegment" => mep.NumOrNull("height") == null && mep.Str("shape") is null or "round" or "circular",
        "IfcCableCarrierSegment" => false,
        _ => true,
    };

    /// <summary>
    /// The items fixed: an opening made where there is none, the opening grown where it is too small (<paramref name="action"/>
    /// "auto" picks by kind; "make" and "enlarge" ask for one), sized round the element with the larger of the check's
    /// clearance and the cut order's MEP rule, rounded up to its step. What cannot be made is said, the rest done; one undo step.
    /// </summary>
    public static JsonObject Fix(ICommandContext ctx, JsonObject a)
    {
        var (report, items) = Pick(ctx.Model, a["numbers"]);
        var action = (string?)a["action"] ?? "auto";
        if (action is not ("auto" or "make" or "enlarge")) throw new CommandException("Fix by auto, make or enlarge.");
        var rule = CutOrder.Mep(ctx.Model);
        var clearance = Math.Max(Json.Number(OpeningVoids.Settings(ctx.Model)["clearance"]) ?? 0, rule.Clearance);
        var makers = ctx.Services.All<IOpeningMaker>();
        var changed = new Dictionary<int, ClashItem>();
        var failed = new JsonArray();
        foreach (var item in items)
        {
            string? Fail(string why) { failed.Add((JsonNode)new JsonObject { ["n"] = item.Number, ["message"] = why }); return null; }
            if (item.MepId is not { } mepId || ctx.Model.Get(mepId) is not { } mep) { Fail("An empty void has nothing to make an opening round: ignore it or delete its opening."); continue; }
            if (ctx.Model.Get(item.HostId) is not { } host) { Fail("Its host is gone."); continue; }
            var enlarge = action == "enlarge" || action == "auto" && item.Kind == OpeningClashes.TooSmall;
            try
            {
                var through = Through(ctx.Query, mep, host);
                if (through.Count == 0) { Fail("The element no longer goes through its host."); continue; }
                var request = new OpeningRequest(clearance, RoundOf(mep), rule);
                string? made = null;
                if (enlarge)
                {
                    if (item.OpeningId is not { } oid || ctx.Model.Get(oid) is not { } opening) { Fail("Its opening is gone, or it is no opening of the model (a roof window's): make one instead."); continue; }
                    if (!makers.Any(x => x.Enlarge(ctx, host, opening, through, request))) { Fail($"{host.Name}'s openings cannot be enlarged here."); continue; }
                    made = opening.Id;
                }
                else made = makers.Select(x => x.Make(ctx, host, through, request)).FirstOrDefault(x => x != null) ?? Fail($"No opening can be made in {host.Name} here (walls and floors only).");
                if (made != null) changed[item.Number] = item with { State = Fixed, FixedBy = made };
            }
            catch (CommandException ex) { Fail(ex.Message); }
        }
        if (changed.Count > 0) Update(ctx, report, changed);
        return new JsonObject { ["fixed"] = changed.Count, ["failed"] = failed };
    }

    /// <summary>Manage → Check's rule (decision 293): the report's open clashes as warnings, and a report out of date; the check itself never runs here.</summary>
    public static IEnumerable<CdeFinding> Findings(IModelView m, string rule)
    {
        var report = Read(m);
        if (report == null)
        {
            var hasMep = m.Entities.Any(OpeningClashes.IsMep) && OpeningClashes.HostClasses.Any(c => m.OfClass(c).Any());
            if (hasMep) yield return new CdeFinding(rule, CdeFinding.Warn, "No clash check has been run: Manage → Clash check… runs it.");
            yield break;
        }
        if (ModelHash(m) != report.Hash) yield return new CdeFinding(rule, CdeFinding.Warn, $"The clash report of {Day(report.Date)} is out of date: the model has changed since; run the clash check again.");
        foreach (var i in report.Items.Where(i => i.State == Open && i.Kind != OpeningClashes.Empty))
            yield return new CdeFinding(rule, CdeFinding.Warn, $"Clash {i.Number}: {i.Message}", i.MepId ?? i.HostId);
    }

    static string Day(string iso) => DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var d) ? d.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) : iso;

    static readonly Dictionary<string, string> KindLabels = new()
    {
        [OpeningClashes.Missing] = "No opening", [OpeningClashes.TooSmall] = "Opening too small", [OpeningClashes.Empty] = "Empty void", ["held"] = "Not cut: load-bearing or cut order",
    };

    static string KindOf(ClashItem i) => i.Kind == OpeningClashes.Missing && i.Held != null ? "held" : i.Kind;
    static string ShortBy(ClashItem i) => i.Kind == OpeningClashes.TooSmall && i.By > 0 ? Math.Max(1, Math.Ceiling(i.By * 1000 - 0.05)).ToString(CultureInfo.InvariantCulture) : "";

    /// <summary>The report as a CSV list (comma separated, quoted, UTF-8): one row per item.</summary>
    public static string Csv(IModelView m, ClashReport r)
    {
        static string Q(string? s) => s == null ? "" : s.IndexOfAny([',', '"', '\n']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        var sb = new StringBuilder();
        sb.Append("No.,Kind,Status,MEP element,Host,Void,Short by (mm),X,Y,Z,Comment,Message\n");
        foreach (var i in r.Items.OrderBy(i => i.Number))
        {
            string N(double v) => Math.Round(v, 3).ToString(CultureInfo.InvariantCulture);
            sb.Append(string.Join(",", i.Number.ToString(CultureInfo.InvariantCulture), Q(KindLabels.GetValueOrDefault(KindOf(i), i.Kind)), i.State, Q(i.MepId is { } id ? m.Get(id)?.Name : ""),
                Q(m.Get(i.HostId)?.Name), Q(i.Mark), ShortBy(i), N(i.Centre.X), N(i.Centre.Y), N(i.Centre.Z), Q(i.Comment), Q(i.Message))).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>The report as a PDF list: A4 landscape, the project, the date and whether it is out of date, the counts, then one line per item.</summary>
    public static byte[] Pdf(IQueryContext q, ClashReport r)
    {
        const double W = 297, H = 210, M = 15, Line = 5.5;
        var m = q.Model;
        var rows = r.Items.OrderBy(i => i.Number).ToList();
        var perPage = (int)((H - 2 * M - 45) / Line);
        var chunks = rows.Count == 0 ? [new List<ClashItem>()] : rows.Chunk(Math.Max(1, perPage)).Select(c => c.ToList()).ToList();
        var stale = ModelHash(m) != r.Hash;
        var counts = Counts(r.Items, r.Ok);
        var pages = new List<PaperPage>();
        for (var pi = 0; pi < chunks.Count; pi++)
        {
            var text = new Pen("TEXT", "#000000", 0, null, null);
            var red = new Pen("TEXT", "#c92a2a", 0, null, null);
            var rules = new Pen("TITLE", "#000000", 0.25, null, null);
            double y = M;
            void T(double x, string s, double size = 2.6, bool bold = false, string anchor = "start", Pen? pen = null) => (pen ?? text).Texts.Add(new PenText(new Vec2(x, y), s, size, Anchor: anchor, Bold: bold));
            y += 6; T(M, "CLASH REPORT · OPENINGS VS MEP", 5.5, true); T(W - M, Sheets.ProjectName(q), 4, true, "end");
            y += 7; T(M, $"Run {Day(r.Date)} · clearance {r.Clearance * 1000:0} mm", 3);
            if (stale) T(W - M, "Out of date: the model has changed since this run", 3, true, "end", red);
            y += 5; T(M, $"{counts[Open]} open ({counts[OpeningClashes.Missing]} no opening, {counts["held"]} not cut, {counts[OpeningClashes.TooSmall]} too small, {counts[OpeningClashes.Empty]} empty) · {counts[Fixed]} fixed · {counts[Ignored]} ignored · {r.Ok} fit", 3);
            if (chunks.Count > 1) T(W - M, $"page {pi + 1} / {chunks.Count}", 2.6, anchor: "end");
            y += 4; rules.Segments.Add((new Vec2(M, y), new Vec2(W - M, y)));
            double[] cols = [M, M + 10, M + 52, M + 68, M + 118, M + 168, M + 182, M + 200];
            y += 5; T(cols[0], "No.", 2.6, true); T(cols[1], "Kind", 2.6, true); T(cols[2], "Status", 2.6, true); T(cols[3], "MEP element", 2.6, true); T(cols[4], "Host", 2.6, true);
            T(cols[5], "Void", 2.6, true); T(cols[6], "Short by", 2.6, true); T(cols[7], "Comment", 2.6, true);
            y += 1.5; rules.Segments.Add((new Vec2(M, y), new Vec2(W - M, y)));
            foreach (var i in chunks[pi])
            {
                y += Line;
                var pen = i.State == Open ? red : text;
                T(cols[0], i.Number.ToString(CultureInfo.InvariantCulture), 2.6, true, pen: pen); T(cols[1], Fit(KindLabels.GetValueOrDefault(KindOf(i), i.Kind), 26)); T(cols[2], i.State);
                T(cols[3], Fit(i.MepId is { } id ? m.Get(id)?.Name ?? "" : "", 30)); T(cols[4], Fit(m.Get(i.HostId)?.Name ?? "", 30)); T(cols[5], i.Mark ?? "");
                T(cols[6], ShortBy(i) is { Length: > 0 } sb ? sb + " mm" : ""); T(cols[7], Fit(i.Comment ?? "", 50));
            }
            y += 2; rules.Segments.Add((new Vec2(M, y), new Vec2(W - M, y)));
            pages.Add(new PaperPage(W, H, [rules, text, red], $"Clash report {pi + 1}"));
        }
        return PdfWriter.Write(pages);
    }

    static string Fit(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Clash report", [], Kind: "resource", Template: false));
        // Run clash check (Manage → Clash check…): {clearance?, empty?} over the project's settings; the report kept, one undo step.
        r.Command("coordination.runCheck", "Run clash check", (ctx, a) => ToJson(ctx.Model, Run(ctx, OpeningClashes.Options.From(ctx.Model, a))));
        // The report as kept, with whether the model changed since; null before the first run.
        r.Query("coordination.report", (q, _) => ToJson(q.Model, Read(q.Model)));
        // Items open again or ignored, with a comment: {numbers, state?: open | ignored, comment?}.
        r.Command("coordination.setItems", "Clash status", (ctx, a) =>
        {
            var (report, items) = Pick(ctx.Model, a["numbers"]);
            var state = (string?)a["state"];
            if (state != null && state is not (Open or Ignored)) throw new CommandException("A clash is set open or ignored (it is fixed by its opening).");
            var comment = a.ContainsKey("comment") ? ((string?)a["comment"])?.Trim() is { Length: > 0 } c ? c : null : null;
            var changed = items.ToDictionary(i => i.Number, i => i with { State = state ?? i.State, Comment = a.ContainsKey("comment") ? comment : i.Comment, FixedBy = state == Open ? null : i.FixedBy });
            Update(ctx, report, changed);
            return null;
        });
        // Make opening, Enlarge opening: {numbers, action?: auto | make | enlarge}; how many were fixed and why the others were not.
        r.Command("coordination.fix", "Fix clashes", Fix);
        // The report as a file: {format: csv | pdf}; {name, data (base64)}.
        r.Query("coordination.reportFile", (q, a) =>
        {
            var report = Read(q.Model) ?? throw new CommandException("Run the clash check first.");
            var format = (string?)a["format"] ?? "csv";
            var bytes = format == "pdf" ? Pdf(q, report) : format == "csv" ? new UTF8Encoding(true).GetBytes(Csv(q.Model, report)) : throw new CommandException("A report is written as CSV or PDF.");
            return new JsonObject { ["name"] = $"Clash report.{format}", ["data"] = Convert.ToBase64String(bytes) };
        });
        // The report's marks in a plan, section or elevation (decision 293): {id, on, print?}; on the screen only unless print is asked.
        r.Command("coordination.markView", "Clash marks", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["id"] ?? "") ?? throw new CommandException("There is no such view.");
            var attrs = view.CloneAttrs();
            static bool? B(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;
            if (B(a["on"]) is { } on) { if (on) attrs[ClashMarks.Attr] = true; else attrs.Remove(ClashMarks.Attr); }
            if (B(a["print"]) is { } print) { if (print) attrs[ClashMarks.PrintAttr] = true; else attrs.Remove(ClashMarks.PrintAttr); }
            ctx.Replace(view.With(attrs: attrs));
            return new JsonObject { ["on"] = attrs[ClashMarks.Attr] != null, ["print"] = attrs[ClashMarks.PrintAttr] != null };
        });
    }
}
