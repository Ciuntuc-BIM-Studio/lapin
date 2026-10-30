using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Cde;

/// <summary>One thing a rule found: the rule, how bad (block or warn), what it says, and what to click (an element, a sheet, a view).</summary>
public sealed record CdeFinding(string Rule, string Severity, string Message, string? EntityId = null, string? SheetId = null, string? ViewId = null)
{
    public const string Block = "block", Warn = "warn";
    public bool Blocking => Severity == Block;

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["rule"] = Rule, ["severity"] = Severity, ["message"] = Message };
        if (EntityId != null) o["entityId"] = EntityId;
        if (SheetId != null) o["sheetId"] = SheetId;
        if (ViewId != null) o["viewId"] = ViewId;
        return o;
    }
}

/// <summary>What a rule runs over: the model, the sheets in scope (those being issued, or all), and the issue's revision when there is one.</summary>
public sealed record CdeCheckContext(IQueryContext Query, IReadOnlyList<Entity> Sheets, string? Revision, CdeNaming Naming)
{
    public IModelView Model => Query.Model;
    readonly Dictionary<string, string> _hashes = [];
    /// <summary>A sheet's drawing hash, computed once per run.</summary>
    public string DrawingHash(Entity sheet) => _hashes.TryGetValue(sheet.Id, out var h) ? h : _hashes[sheet.Id] = Platform.Sheets.DrawingHash(Query, sheet);
    /// <summary>The title block field, trimmed; null when empty.</summary>
    public static string? Field(Entity sheet, string key) => (sheet.Attrs["titleBlock"] as JsonObject)?[key] is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim().Length > 0 ? s.Trim() : null;
}

/// <summary>
/// A pre-issue rule (docs/plan-cde.md). The platform has its own; a module
/// registers more as a service (<c>r.Service&lt;ICdeRule&gt;</c>), and a module
/// of another repository (a company's rule set) through <see cref="CdeHooks"/>.
/// </summary>
public interface ICdeRule
{
    string Id { get; }
    string Label { get; }
    /// <summary>Whether a finding stops an issue (else it warns and may be overridden).</summary>
    bool Blocking { get; }
    IEnumerable<CdeFinding> Run(CdeCheckContext c);
}

/// <summary>What another module adds over the open CDE: more rules, and a say in the names.</summary>
public static class CdeHooks
{
    /// <summary>Rules added from outside the product's modules (a premium module registers its company rule sets here).</summary>
    public static List<ICdeRule> Rules { get; } = [];
    /// <summary>A naming override: given the project's naming, the one to use (a company convention); null: the project's.</summary>
    public static Func<CdeNaming, CdeNaming>? Naming { get; set; }
}

/// <summary>A rule made of a function.</summary>
sealed class Rule(string id, string label, bool blocking, Func<CdeCheckContext, IEnumerable<CdeFinding>> run) : ICdeRule
{
    public string Id => id;
    public string Label => label;
    public bool Blocking => blocking;
    public IEnumerable<CdeFinding> Run(CdeCheckContext c) => run(c);
}

/// <summary>
/// The built-in rules and their runner. A rule says what it found on which
/// sheet, view or element; the interface lists the findings and a click goes
/// there. Blocking: what a drawing must not go out with; warnings: what a
/// drawing office would want looked at.
/// </summary>
public static class CdeChecks
{
    /// <summary>The building classes the model-basics rules look at (what stands on a storey, has a type, a material).</summary>
    static readonly string[] Building = [IfcClass.Wall, IfcClass.Slab, IfcClass.Roof, IfcClass.Column, IfcClass.Beam, IfcClass.Door, IfcClass.Window, "IfcStair", "IfcCurtainWall", "IfcFooting", "IfcMember", "IfcCovering", "IfcRailing"];
    static readonly string[] Typed = [IfcClass.Wall, IfcClass.Slab, IfcClass.Roof, IfcClass.Door, IfcClass.Window, "IfcCurtainWall", "IfcStair"];
    static readonly string[] WithMaterial = [IfcClass.Wall, IfcClass.Slab, IfcClass.Roof, IfcClass.Column, IfcClass.Beam, "IfcFooting", "IfcMember"];

    /// <summary>The Pset_…Common properties required by default: the property and the classes it is asked on.</summary>
    public static readonly (string Name, string[] Classes)[] DefaultRequired =
    [
        ("IsExternal", [IfcClass.Wall, IfcClass.Slab, IfcClass.Roof, IfcClass.Door, IfcClass.Window]),
        ("LoadBearing", [IfcClass.Wall, IfcClass.Slab, IfcClass.Column, IfcClass.Beam]),
        ("FireRating", [IfcClass.Door]),
    ];

    static string Label(Entity e) => e.Name is { Length: > 0 } n ? n : e.Class.Replace("Ifc", "");
    static string SheetLabel(Entity s) => $"{s.Str("number")} {s.Name}".Trim();

    public static readonly IReadOnlyList<ICdeRule> Builtin =
    [
        new Rule("titleBlock", "Title block", true, c =>
        {
            var o = new List<CdeFinding>();
            // Blocking: the number, the title and who drew it; warnings: the rest (printed as defaults or set by the issue).
            CdeFinding Block(Entity s, string message) => new("titleBlock", CdeFinding.Block, message, SheetId: s.Id);
            CdeFinding Warn(Entity s, string message) => new("titleBlock", CdeFinding.Warn, message, SheetId: s.Id);
            foreach (var s in c.Sheets)
            {
                if (s.Str("number") is not { Length: > 0 }) o.Add(Block(s, "The sheet has no number."));
                if (s.Name is not { Length: > 0 } || s.Name == "Sheet") o.Add(Block(s, $"Sheet {s.Str("number")} has no title."));
                if (CdeCheckContext.Field(s, "drawnBy") == null) o.Add(Block(s, $"Sheet {SheetLabel(s)}: „Drawn by” is empty."));
                if (CdeCheckContext.Field(s, "checkedBy") == null) o.Add(Warn(s, $"Sheet {SheetLabel(s)}: „Checked by” is empty."));
                if (CdeCheckContext.Field(s, "date") == null) o.Add(Warn(s, $"Sheet {SheetLabel(s)}: the date is empty (today's is printed)."));
                if (CdeCheckContext.Field(s, "phase") == null) o.Add(Warn(s, $"Sheet {SheetLabel(s)}: the phase is empty."));
                if (c.Revision == null && CdeCheckContext.Field(s, "revision") == null) o.Add(Warn(s, $"Sheet {SheetLabel(s)}: the revision is empty (an issue sets it)."));
                if (!Sheets.Place(c.Query, s).Any(p => !p.Content)) o.Add(Warn(s, $"Sheet {SheetLabel(s)} has no view on it, so no scale."));
            }
            return o;
        }),
        new Rule("viewScale", "View scale", true, c => c.Sheets.SelectMany(s => Sheets.Viewports(s)
            .Select(vp => Views.Find(c.Model, vp.ViewId))
            .Where(v => v != null && !(v.NumOrNull("scale") is { } sc && sc > 0))
            .Select(v => new CdeFinding("viewScale", CdeFinding.Block, $"View „{v!.Name}” on sheet {SheetLabel(s)} has no scale.", SheetId: s.Id, ViewId: v.Id)))),
        new Rule("viewOutside", "View on the paper", true, c => c.Sheets.SelectMany(s => Sheets.Place(c.Query, s)
            .Select(p => (p, problems: Sheets.Problems(s, p)))
            .Where(x => x.problems.Count > 0)
            .Select(x => new CdeFinding("viewOutside", CdeFinding.Block,
                $"„{x.p.View.Name}” on sheet {SheetLabel(s)} {(x.problems.Contains("outside") ? "runs past the frame" : "")}{(x.problems.Count == 2 ? " and " : "")}{(x.problems.Contains("titleBlock") ? "lies over the title block" : "")}.",
                SheetId: s.Id, ViewId: x.p.View.Id)))),
        new Rule("legend", "Legend", false, c =>
        {
            var o = new List<CdeFinding>();
            foreach (var s in c.Sheets)
                foreach (var pl in Sheets.Place(c.Query, s).Where(p => !p.Content))
                {
                    var hasLegend = c.Model.OfClass(IfcClass.Annotation).Any(a => a.PredefinedType == "LEGEND" && a.Str("viewId") == pl.View.Id);
                    if (hasLegend) continue;
                    if (pl.View.Str("colorScheme") is { Length: > 0 })
                        o.Add(new CdeFinding("legend", CdeFinding.Warn, $"„{pl.View.Name}” on sheet {SheetLabel(s)} colours its rooms by a scheme and has no legend.", SheetId: s.Id, ViewId: pl.View.Id));
                    else if (pl.Drawing.Pens.Where(p => p.Layer is "FILL" or "HATCH").Select(p => p.Fill ?? $"{p.Stroke}/{string.Join(",", p.Dash ?? [])}").Distinct().Count() >= 4)
                        o.Add(new CdeFinding("legend", CdeFinding.Warn, $"„{pl.View.Name}” on sheet {SheetLabel(s)} draws several hatches and fills and has no legend.", SheetId: s.Id, ViewId: pl.View.Id));
                }
            return o;
        }),
        new Rule("viewUnplaced", "Views on no sheet", false, c =>
        {
            var placed = c.Model.OfClass(Sheets.Class).SelectMany(Sheets.Viewports).Select(v => v.ViewId).ToHashSet();
            return c.Model.OfClass(Views.Class)
                .Where(v => v.Str("kind") is "plan" or "section" or "elevation" && !placed.Contains(v.Id))
                .Select(v => new CdeFinding("viewUnplaced", CdeFinding.Warn, $"View „{v.Name}” is on no sheet.", ViewId: v.Id));
        }),
        new Rule("northArrow", "North arrow", false, c => c.Model.OfClass(Views.Class)
            .Where(v => SitePlans.Of(v) != null && !SitePlans.NorthArrow(v))
            .Select(v => new CdeFinding("northArrow", CdeFinding.Warn, $"Site plan „{v.Name}” has no north arrow.", ViewId: v.Id))),
        new Rule("revisionUnchanged", "Revision after a change", true, c =>
        {
            var o = new List<CdeFinding>();
            foreach (var s in c.Sheets)
            {
                if (CdeIssues.LastOf(c.Model, s.Id) is not { } last || last.File.DrawingHash == null) continue;
                var revision = c.Revision ?? CdeCheckContext.Field(s, "revision");
                if (revision != last.Issue.Revision) continue;
                if (c.DrawingHash(s) != last.File.DrawingHash)
                    o.Add(new CdeFinding("revisionUnchanged", CdeFinding.Block, $"Sheet {SheetLabel(s)} changed since it went out as {last.Issue.Revision} ({last.Issue.Name}) but is still {revision}.", SheetId: s.Id));
            }
            return o;
        }),
        new Rule("naming", "File name", true, c => c.Sheets.SelectMany(s =>
        {
            var name = c.Naming.For(c.Model, s);
            var problems = c.Naming.Validate(name);
            if (CdeNaming.NumberOf(s.Str("number")) == null) problems.Insert(0, $"the sheet number „{s.Str("number")}” ends in no digits");
            return problems.Select(p => new CdeFinding("naming", CdeFinding.Block, $"Sheet {SheetLabel(s)} → {name}: {p}.", SheetId: s.Id));
        })),
        new Rule("storey", "Elements on a storey", false, c => c.Model.Entities
            .Where(e => Building.Contains(e.Class) && e.Class is not (IfcClass.Door or IfcClass.Window) && Spatial.StoreyOf(c.Model, e.Id) == null)
            .Select(e => new CdeFinding("storey", CdeFinding.Warn, $"{Label(e)} is on no storey.", e.Id))),
        new Rule("type", "Elements with a type", false, c => c.Model.Entities
            .Where(e => Typed.Contains(e.Class) && c.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType) == null)
            .Select(e => new CdeFinding("type", CdeFinding.Warn, $"{Label(e)} has no type.", e.Id))),
        new Rule("material", "Elements with a material", false, c => c.Model.Entities
            .Where(e => WithMaterial.Contains(e.Class) && !HasMaterial(c.Model, e))
            .Select(e => new CdeFinding("material", CdeFinding.Warn, $"{Label(e)} has no material.", e.Id))),
        new Rule("openingTag", "Door and window tags", false, c =>
        {
            var tagged = c.Model.OfClass(IfcClass.Annotation).Where(a => a.PredefinedType == "TAG").Select(a => a.Str("element")).OfType<string>().ToHashSet();
            return c.Model.Entities.Where(e => e.Class is IfcClass.Door or IfcClass.Window && !tagged.Contains(e.Id))
                .Select(e => new CdeFinding("openingTag", CdeFinding.Warn, $"{Label(e)} has no tag.", e.Id));
        }),
        new Rule("roomNumber", "Room numbers", false, c => c.Model.OfClass(IfcClass.Space)
            .Where(r => r.Str("number") is not { Length: > 0 })
            .Select(r => new CdeFinding("roomNumber", CdeFinding.Warn, $"Room „{r.Name}” has no number.", r.Id))),
        new Rule("psetCommon", "Required properties", false, c =>
        {
            var o = new List<CdeFinding>();
            foreach (var (name, classes) in Required(c.Model))
                foreach (var e in c.Model.Entities.Where(e => classes.Contains(e.Class)))
                {
                    var set = StandardPsets.All.FirstOrDefault(s => StandardPsets.AppliesTo(s, e.Class) && s.Properties.ContainsKey(name));
                    if (set == null) continue;
                    if (!HasParam(c.Model, e, set.Name, name))
                        o.Add(new CdeFinding("psetCommon", CdeFinding.Warn, $"{Label(e)}: {set.Name}.{name} is empty.", e.Id));
                }
            return o;
        }),
    ];

    /// <summary>The required Pset_…Common properties: the project's list (<c>cde.requiredProps</c>, names) on the default classes, else the defaults.</summary>
    public static IEnumerable<(string Name, string[] Classes)> Required(IModelView m)
    {
        if (Cde.Settings(m)["requiredProps"] is JsonArray a)
        {
            foreach (var n in a.Select(x => (string?)x).OfType<string>())
                yield return (n, DefaultRequired.FirstOrDefault(d => d.Name == n).Classes ?? StandardPsets.All.Where(s => s.Properties.ContainsKey(n)).SelectMany(s => s.Classes).Distinct().ToArray());
            yield break;
        }
        foreach (var d in DefaultRequired) yield return d;
    }

    static bool HasMaterial(IModelView m, Entity e)
    {
        if (e.Str("material") is { Length: > 0 } || e.Attrs["material"] is JsonObject) return true;
        if (m.RelatingOf(e.Id, IfcClass.RelAssociatesMaterial) != null) return true;
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        return type != null && (m.RelatingOf(type.Id, IfcClass.RelAssociatesMaterial) != null || type.Attrs["layers"] is JsonArray { Count: > 0 } || type.Str("material") is { Length: > 0 });
    }

    /// <summary>Whether the element (or its type) holds a value for the property in the set, among its parameters.</summary>
    static bool HasParam(IModelView m, Entity e, string pset, string name)
    {
        static bool In(Entity x, string pset, string name) => x.Attrs["params"] is JsonArray ps && ps.OfType<JsonObject>().Any(p =>
            (string?)p["pset"] == pset && string.Equals((string?)p["name"], name, StringComparison.OrdinalIgnoreCase) && p["value"] is JsonValue v && !(v.TryGetValue<string>(out var s) && s.Length == 0));
        if (In(e, pset, name)) return true;
        // A door's or window's IsExternal is in its type's spec, as the IFC export writes it.
        if (name == "IsExternal" && e.Class is IfcClass.Door or IfcClass.Window && m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } t && t.Attrs["spec"] is JsonObject spec && spec["isExternal"] is JsonValue) return true;
        return m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } type && In(type, pset, name);
    }

    /// <summary>Every rule: the platform's, the modules' services, the hooks'.</summary>
    public static IEnumerable<ICdeRule> Rules(IQueryContext q) => Builtin.Concat(q.Services.All<ICdeRule>()).Concat(CdeHooks.Rules);

    /// <summary>Runs every rule over the sheets in scope (all when none given); the findings, blocking first.</summary>
    public static List<CdeFinding> Run(IQueryContext q, IReadOnlyList<Entity>? sheets = null, string? revision = null)
    {
        var naming = CdeHooks.Naming?.Invoke(CdeNaming.Of(q.Model)) ?? CdeNaming.Of(q.Model);
        var c = new CdeCheckContext(q, sheets ?? q.Model.OfClass(Sheets.Class).ToList(), revision, naming);
        return Rules(q).SelectMany(r => r.Run(c)).OrderBy(f => f.Blocking ? 0 : 1).ToList();
    }

    public static JsonObject ToJson(IQueryContext q, List<CdeFinding> findings) => new()
    {
        ["blocking"] = findings.Count(f => f.Blocking), ["warnings"] = findings.Count(f => !f.Blocking),
        ["findings"] = new JsonArray(findings.Select(f => (JsonNode)f.ToJson()).ToArray()),
        ["rules"] = new JsonArray(Rules(q).Select(r => (JsonNode)new JsonObject { ["id"] = r.Id, ["label"] = r.Label, ["blocking"] = r.Blocking }).ToArray()),
    };
}
