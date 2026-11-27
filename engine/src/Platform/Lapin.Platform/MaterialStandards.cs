using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Drawing;
using Lapin.Drawing.Export;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The project's material standard (docs/plan-material-graphics.md stage 3, decision 681): the project names the one its
/// materials draw by (<see cref="Key"/>, none: Lapin's, today's look) among the built-in ones (<see cref="MaterialStandard.BuiltIns"/>)
/// and its own (<see cref="ListKey"/>: duplicated, edited, made from its materials, imported from a <c>.lapinstandards</c> file or
/// from the store). Applying one (<c>standards.applyMaterials</c>) leaves the materials' own graphics as they are (Î9), unless
/// asked to overwrite what the standard sets; never chosen by the language or the country (Î8). Every command one undo step.
/// </summary>
public static class MaterialStandards
{
    /// <summary>The project's attribute naming its standard (none: Lapin's).</summary>
    public const string Key = "materialStandard";
    /// <summary>The project's own standards, as their JSON.</summary>
    public const string ListKey = "materialStandards";

    static readonly ConditionalWeakTable<JsonNode, List<MaterialStandard>> Parsed = new();

    /// <summary>The project's own standards (one it cannot read left out).</summary>
    public static IReadOnlyList<MaterialStandard> Own(Entity? project) =>
        project?.Attrs[ListKey] is JsonArray a ? Parsed.GetValue(a, n => [.. ((JsonArray)n).OfType<JsonObject>().Select(TryRead).OfType<MaterialStandard>()]) : [];

    static MaterialStandard? TryRead(JsonObject o)
    {
        try { return MaterialStandard.Read(o); }
        catch (FormatException) { return null; }
    }

    /// <summary>The id of the standard the project names (Lapin's when none).</summary>
    public static string CurrentId(Entity? project) => (string?)project?.Attrs[Key] is { Length: > 0 } id ? id : MaterialStandard.LapinId;

    /// <summary>
    /// The project's mark that it was made with Lapin's look of decision 682 (stage 4 of docs/plan-material-graphics.md): Lapin's
    /// standard at version 1.1 (<see cref="MaterialStandard.LapinCurrent"/>), the built-in material graphics sets of the Structural
    /// and MEP styles (<see cref="MaterialGraphicsSets"/>), rooms' floor finishes hatched by the standard. A new project has it
    /// (<see cref="Spatial.EnsureBuilding(IEdit, SpatialSeed?)"/>); one made before never gains it, so it draws as it did (Î10).
    /// </summary>
    public const string LookRevKey = "materialLookRev";
    public const int LookRev = 1;

    /// <summary>Whether a project was made with the look of decision 682 (<see cref="LookRevKey"/>).</summary>
    public static bool NewLook(Entity? project) => Json.Number(project?.Attrs[LookRevKey]) is { } r && r >= LookRev;

    public static bool NewLook(IModelView m) => NewLook(Spatial.Project(m));

    /// <summary>Lapin's standard as a project draws by it: version 1.1 for a project made since decision 682, else 1.0, exactly as before.</summary>
    public static MaterialStandard LapinOf(Entity? project) => NewLook(project) ? MaterialStandard.LapinCurrent : MaterialStandard.Lapin;

    /// <summary>The built-in standards as a project sees them: Lapin's in the version it draws by (<see cref="LapinOf"/>), then the others.</summary>
    public static IReadOnlyList<MaterialStandard> BuiltInsOf(Entity? project) =>
        NewLook(project) ? MaterialStandard.BuiltIns : [MaterialStandard.Lapin, .. MaterialStandard.BuiltIns.Skip(1)];

    /// <summary>A standard by its id: a built-in one (Lapin's in the project's version), else the project's.</summary>
    public static MaterialStandard? Find(Entity? project, string? id) =>
        id == null ? null : MaterialStandard.FindBuiltIn(id) is { } b ? (b.IsLapin ? LapinOf(project) : b) : Own(project).FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The standard the project's materials draw by (Lapin's when it names none, or one it no longer has).</summary>
    public static MaterialStandard Of(IModelView m) => Of(Spatial.Project(m));

    public static MaterialStandard Of(Entity? project) => Find(project, CurrentId(project)) ?? LapinOf(project);

    static Entity Project(IModelView m) => Spatial.Project(m) ?? throw new CommandException("The model has no project.");

    // ------------------------------------------------------------ checking

    /// <summary>
    /// A standard given by a command or a file, checked against the project: its fields (<see cref="MaterialStandard.Read"/>), each
    /// rule's graphics as a material's (patterns: the built-in ones, the project's and its own; line types: the project's),
    /// its pens. Its JSON comes back canonical.
    /// </summary>
    public static MaterialStandard Checked(IModelView m, JsonObject given)
    {
        MaterialStandard read;
        try { read = MaterialStandard.Read(given); }
        catch (FormatException e) { throw new CommandException(e.Message); }
        var json = read.ToJson();
        var where = $"„{read.Name}”";
        if (given["pens"] is JsonObject pens)
        {
            var po = new JsonObject();
            foreach (var k in new[] { "hatch", "cut" })
                if (DraftingResources.CheckStandardLine(m, pens[k], where, k == "hatch" ? "the hatch pen" : "the cut pen") is { } line) po[k] = line;
            if (po.Count > 0) json["pens"] = po; else json.Remove("pens");
        }
        var rules = json["rules"] as JsonArray ?? [];
        for (var i = 0; i < rules.Count; i++)
        {
            var r = (JsonObject)rules[i]!;
            foreach (var kind in MaterialGraphics.Kinds)
                if (r[kind] is JsonObject g) r[kind] = DraftingResources.CheckStandardGraphics(m, g, $"{where}, rule {i + 1}, {kind}", read.Patterns);
        }
        try { return MaterialStandard.Read(json); }
        catch (FormatException e) { throw new CommandException(e.Message); }
    }

    /// <summary>An id no standard has (built-in or the project's), made from <paramref name="name"/>.</summary>
    public static string FreeId(Entity? project, string name)
    {
        var slug = Regex.Replace(MaterialStandard.FoldText(name.Trim().ToLowerInvariant(), MaterialStandard.FoldAll), "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0) slug = "standard";
        if (slug.Length > 60) slug = slug[..60].TrimEnd('-');
        bool Taken(string id) => Find(project, id) != null;
        if (!Taken(slug)) return slug;
        var n = 2;
        while (Taken($"{slug}-{n}")) n++;
        return $"{slug}-{n}";
    }

    // ------------------------------------------------------------ the project's list

    /// <summary>The project with one of its own standards added or replaced (by id), or removed (<paramref name="add"/> null).</summary>
    static void WriteOwn(ICommandContext ctx, string id, JsonObject? add)
    {
        var project = Project(ctx.Model);
        var attrs = project.CloneAttrs();
        var list = attrs[ListKey] as JsonArray ?? [];
        var at = list.OfType<JsonObject>().ToList().FindIndex(x => string.Equals((string?)x["id"], id, StringComparison.OrdinalIgnoreCase));
        if (add == null) { if (at >= 0) list.RemoveAt(at); }
        else if (at >= 0) list[at] = add;
        else list.Add((JsonNode)add);
        if (list.Count > 0) attrs[ListKey] = list; else attrs.Remove(ListKey);
        // A standard the project draws by, changed: its patterns with it.
        if (add != null && string.Equals(CurrentId(project), id, StringComparison.OrdinalIgnoreCase)) TakePatterns(attrs, MaterialStandard.Read(add), out _, out _);
        if (add == null && string.Equals(CurrentId(project), id, StringComparison.OrdinalIgnoreCase)) attrs.Remove(Key);
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>
    /// The standard's own patterns into the project's (its attributes): each the project lacks added; one it has by that name and
    /// drawn otherwise updated (the standard is what draws it).
    /// </summary>
    static void TakePatterns(JsonObject attrs, MaterialStandard s, out int added, out int updated)
    {
        added = updated = 0;
        if (s.Patterns.Count == 0) return;
        var mine = ((string?)attrs["hatchPatterns"] is { Length: > 0 } t ? Pat.Parse(t).Patterns : []).ToList();
        foreach (var p in s.Patterns)
        {
            var i = mine.FindIndex(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));
            if (i < 0) { mine.Add(p); added++; }
            else if (Pat.Serialize([mine[i]]) != Pat.Serialize([p])) { mine[i] = p; updated++; }
        }
        if (added + updated > 0) attrs["hatchPatterns"] = Pat.Serialize(mine);
    }

    // ------------------------------------------------------------ applying

    /// <summary>
    /// A material as applying <paramref name="s"/> with <paramref name="overwrite"/> leaves it: its own graphics fields the standard
    /// sets for it taken off (the rule's kinds' fields; with pens, the hatch's colour, weight and line type and the cut line), its
    /// own membrane flag when the rule says membrane; a category when it has none (overwrite: the rule's over its own). The fields taken off, by kind.
    /// </summary>
    static (Entity Material, JsonObject Cleared) Applied(Entity mat, MaterialStandard s, bool overwrite)
    {
        var attrs = mat.CloneAttrs();
        var cleared = new JsonObject();
        var category = mat.Str("category");
        var cut = s.CutRule(DrawsAs.NameFor(mat, mat.Name), category);
        var el = s.ElevationRule(DrawsAs.NameFor(mat, mat.Name), category);
        if (overwrite)
        {
            IEnumerable<string> Pens() => (s.HatchPen != null ? new[] { "hatchColor", "hatchWeight", "hatchLineType" } : []).Concat(s.CutPen != null ? ["cutLine"] : []);
            foreach (var kind in MaterialGraphics.Kinds)
            {
                if (attrs[kind] is not JsonObject own) continue;
                var fields = (kind == "elevation" ? el?.FieldsOf("elevation") ?? [] : (cut?.FieldsOf(kind) ?? []).Concat(Pens())).Distinct().Where(own.ContainsKey).ToList();
                if (fields.Count == 0) continue;
                foreach (var f in fields) own.Remove(f);
                if (own.Count == 0) attrs.Remove(kind);
                cleared[kind] = new JsonArray(fields.Select(f => (JsonNode)f).ToArray());
            }
            if (cut?.Membrane != null && attrs.ContainsKey("membrane")) { attrs.Remove("membrane"); cleared["membrane"] = true; }
        }
        var suggested = cut?.Category ?? el?.Category;
        if (suggested != null && (category == null || overwrite && category != suggested)) attrs["category"] = suggested;
        return (JsonNode.DeepEquals(attrs, mat.Attrs) ? mat : mat.With(attrs: attrs), cleared);
    }

    static JsonObject Looks(Entity mat, MaterialStandard s, bool paper, double? scale) =>
        new(MaterialGraphics.Kinds.Select(k => KeyValuePair.Create(k, (JsonNode?)MaterialGraphics.Resolve(mat, k, paperSurfaces: paper, scale: scale, standard: s).ToJson())));

    /// <summary>
    /// The project's standard made <paramref name="s"/> (Lapin's: the project names none): its patterns taken; with
    /// <paramref name="overwrite"/>, the materials' own fields the standard sets taken off. With <paramref name="preview"/>, nothing
    /// changes: each material's look before and after (at <paramref name="scale"/>, its steps; none: without), what would be taken off.
    /// </summary>
    public static JsonObject Apply(ICommandContext? ctx, IModelView m, MaterialStandard s, bool overwrite, bool preview, double? scale)
    {
        var project = Project(m);
        var before = Of(project);
        var paper = MaterialHatchUpgrade.PaperSurfaces(project);
        var rows = new JsonArray();
        var changes = new List<Entity>();
        var changed = 0;
        foreach (var mat in m.OfClass(IfcClass.Material).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var (after, cleared) = Applied(mat, s, overwrite);
            if (!ReferenceEquals(after, mat)) changes.Add(after);
            if (!preview) continue;
            var (b, a) = (Looks(mat, before, paper, scale), Looks(after, s, paper, scale));
            var differs = !JsonNode.DeepEquals(b, a);
            if (differs) changed++;
            var row = new JsonObject { ["id"] = mat.Id, ["name"] = mat.Name, ["changed"] = differs, ["before"] = b, ["after"] = a };
            if (cleared.Count > 0) row["cleared"] = cleared;
            if (after.Str("category") != mat.Str("category")) row["category"] = after.Str("category");
            rows.Add((JsonNode)row);
        }
        if (preview)
            return new JsonObject { ["standard"] = s.Id, ["from"] = before.Id, ["overwrite"] = overwrite, ["changed"] = changed, ["materials"] = rows };
        if (ctx == null) throw new InvalidOperationException("Applying a standard needs a command.");
        var attrs = project.CloneAttrs();
        if (s.IsLapin) attrs.Remove(Key); else attrs[Key] = s.Id;
        TakePatterns(attrs, s, out var added, out var updated);
        ctx.Replace(project.With(attrs: attrs));
        foreach (var e in changes) ctx.Replace(e);
        return new JsonObject { ["standard"] = s.Id, ["from"] = before.Id, ["materials"] = changes.Count, ["patternsAdded"] = added, ["patternsUpdated"] = updated };
    }

    // ------------------------------------------------------------ from the project

    /// <summary>
    /// A standard made from the project's materials' own graphics: one rule per material that sets any (its name, exactly; its
    /// section, plan and elevation fields, its membrane flag, its category), in the order of their names; then, unless
    /// <c>withRules</c> is false, the rules of the standard the project draws by, for the materials it does not name; the
    /// patterns of the project's its rules name, and the base standard's.
    /// </summary>
    public static JsonObject FromProject(IModelView m, JsonObject a)
    {
        var project = Project(m);
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A standard needs a name.");
        var language = ((string?)a["language"])?.Trim().ToLowerInvariant() is { Length: 2 or 3 } l ? l : "en";
        var withRules = !(a["withRules"] is JsonValue wv && wv.TryGetValue<bool>(out var w) && !w);
        var basis = Of(project);
        var fold = withRules ? basis.Fold : MaterialStandard.FoldAll;
        var rules = new JsonArray();
        foreach (var mat in m.OfClass(IfcClass.Material).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var kinds = MaterialGraphics.Kinds.Where(k => mat.Attrs[k] is JsonObject { Count: > 0 }).ToList();
            var membrane = mat.Attrs["membrane"] is JsonValue mv && mv.TryGetValue<bool>(out var mb) ? (bool?)mb : null;
            if (kinds.Count == 0 && membrane == null) continue;
            var folded = MaterialStandard.FoldText(MaterialGraphics.Normalise(mat.Name), fold);
            var rule = new JsonObject { ["match"] = new JsonObject { ["names"] = new JsonObject { [language] = new JsonArray((JsonNode)("^" + Regex.Escape(folded) + "$")) } } };
            foreach (var k in kinds) rule[k] = mat.Attrs[k]!.DeepClone();
            if (membrane != null) rule["membrane"] = membrane;
            // A membrane shows no surface on a facade (its rule's membrane is a cut's): none there either, unless it says.
            if (membrane == true && rule["elevation"] == null) rule["elevation"] = new JsonObject { ["hatch"] = null };
            if (mat.Str("category") is { } cat && MaterialStandard.Categories.Contains(cat)) rule["category"] = cat;
            rules.Add((JsonNode)rule);
        }
        if (withRules) foreach (var r in basis.Rules) rules.Add((JsonNode)r.Json.DeepClone());
        if (rules.Count == 0) throw new CommandException("The project's materials set no graphics of their own to make a standard of.");
        // The patterns its rules name that are the project's (a built-in one goes by its name), and the base's own.
        var own = DraftingResources.Patterns(m);
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Names(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o:
                    foreach (var (k, v) in o)
                        if (k == "hatch" && (string?)(v as JsonValue) is { Length: > 0 } h) named.Add(h); else Names(v);
                    break;
                case JsonArray arr: foreach (var x in arr) Names(x); break;
            }
        }
        Names(rules);
        var patterns = own.Where(p => named.Contains(p.Name)).ToList();
        if (withRules) patterns.AddRange(basis.Patterns.Where(p => !patterns.Any(x => x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase))));
        var o = new JsonObject { ["id"] = FreeId(project, name), ["name"] = name };
        foreach (var k in new[] { "description", "author", "country", "norm", "version", "license" })
            if (((string?)a[k])?.Trim() is { Length: > 0 } v) o[k] = v;
        o["version"] ??= "1.0.0";
        o["languages"] = a["languages"] is JsonArray ls ? ls.DeepClone() : new JsonArray((JsonNode)language);
        o["indicative"] = !(a["indicative"] is JsonValue iv && iv.TryGetValue<bool>(out var ib) && !ib);
        if (withRules) o["basedOn"] = basis.Id;
        if (fold == MaterialStandard.FoldRo) o["fold"] = fold;
        if (withRules && basis.Json["pens"] is JsonObject pens) o["pens"] = pens.DeepClone();
        if (patterns.Count > 0) o["patterns"] = Pat.Serialize(patterns);
        o["rules"] = rules;
        return o;
    }

    // ------------------------------------------------------------ the standards' file

    /// <summary>
    /// The <c>materialGraphics</c> section of a <c>.lapinstandards</c> file: the standards named (<paramref name="ids"/>, a built-in
    /// one written as data), else the project's own; the one the project draws by (for information: importing never applies it).
    /// Null when there is nothing to write.
    /// </summary>
    public static JsonObject? FileSection(IModelView m, IReadOnlyList<string>? ids)
    {
        var project = Project(m);
        var list = ids == null ? Own(project).ToList()
            : ids.Select(id => Find(project, id) ?? throw new CommandException($"There is no material standard „{id}”.")).ToList();
        if (list.Count == 0 && CurrentId(project) == MaterialStandard.LapinId) return null;
        var o = new JsonObject { ["standards"] = new JsonArray(list.Select(s => (JsonNode)s.ToJson()).ToArray()) };
        o["current"] = CurrentId(project);
        return o;
    }

    /// <summary>A file's standards, each read (one that cannot be is refused, naming it).</summary>
    public static List<MaterialStandard> InFile(JsonObject file)
    {
        var o = new List<MaterialStandard>();
        foreach (var x in (file["materialGraphics"] as JsonObject)?["standards"] as JsonArray ?? [])
        {
            if (x is not JsonObject so) continue;
            try { o.Add(MaterialStandard.Read(so)); }
            catch (FormatException e) { throw new CommandException($"A material standard of the file cannot be read: {e.Message}"); }
        }
        return o;
    }

    /// <summary>Whether a file's standard is one the project has (by id: built-in or its own) and whether it is the same.</summary>
    public static (bool Exists, bool Same) Compare(IModelView m, MaterialStandard s)
    {
        var have = Find(Spatial.Project(m), s.Id);
        return (have != null, have != null && JsonNode.DeepEquals(have.Json, s.Json));
    }

    /// <summary>
    /// A file's standards brought into the project (in <see cref="StandardsLibrary"/>'s import, one undo step): one it lacks added,
    /// one the same kept, one that differs as its choice says (update, both: „Name (2)” beside, skip); a built-in one that differs
    /// is never changed: kept beside as a copy, or skipped. Each checked against the project (its patterns and line types).
    /// </summary>
    public static void Import(ICommandContext ctx, JsonObject file, Func<string, string, string> choice, ref int added, ref int updated, ref int kept, ref int skipped, List<string> renamed)
    {
        foreach (var s in InFile(file))
        {
            var (exists, same) = Compare(ctx.Model, s);
            if (!exists) { WriteOwn(ctx, s.Id, Checked(ctx.Model, s.ToJson()).ToJson()); added++; continue; }
            if (same) { kept++; continue; }
            var c = choice("materialStandard", s.Id);
            if (c == LibraryMerge.Skip) { skipped++; continue; }
            if (c == LibraryMerge.Both || MaterialStandard.FindBuiltIn(s.Id) != null)
            {
                var project = Project(ctx.Model);
                var copy = s.ToJson();
                copy["id"] = FreeId(project, s.Id);
                bool Taken(string n) => Own(project).Concat(MaterialStandard.BuiltIns).Any(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase));
                copy["name"] = Taken(s.Name) ? LibraryMerge.FreeName(Taken, s.Name) : s.Name;
                var made = Checked(ctx.Model, copy);
                WriteOwn(ctx, made.Id, made.ToJson());
                added++;
                renamed.Add(made.Name);
                continue;
            }
            WriteOwn(ctx, s.Id, Checked(ctx.Model, s.ToJson()).ToJson());
            updated++;
        }
    }

    // ------------------------------------------------------------ the sample sheet

    /// <summary>The layers of a view's model drawing (what a sample sheet shows; the annotations' are left out).</summary>
    static readonly HashSet<string> ModelLayers = [PenLayer.Fill, PenLayer.Hatch, PenLayer.Cut, PenLayer.Seen, PenLayer.Hidden, PenLayer.Beyond, PenLayer.Overhead, PenLayer.Membrane, SurfaceHatches.Layer, SurfaceHatches.FillLayer];

    /// <summary>
    /// The reference details (<see cref="MaterialReferenceDetails"/>) drawn with a standard at one scale, each in a scratch project
    /// of the product's modules with the open project's patterns and line types: each view's drawing as the drawing pane shows it,
    /// and, asked for, one sheet of them all (its model lines, a title naming the standard) as SVG text and as PDF (base64).
    /// </summary>
    public static JsonObject PreviewSheet(IQueryContext q, JsonObject a)
    {
        var catalog = q.Services as ModuleCatalog ?? throw new CommandException("No modules to draw with.");
        var scale = Json.Number(a["scale"]) ?? 50;
        if (!(scale >= 1 && scale <= 1000)) throw new CommandException("A scale is 1 to 1000.");
        var source = Spatial.Project(q.Model);
        var s = a["standard"] is JsonObject given ? Checked(q.Model, given)
            : Find(source, (string?)a["id"] ?? CurrentId(source)) ?? throw new CommandException($"There is no material standard „{(string?)a["id"]}”.");
        var only = a["details"] is JsonArray da ? da.Select(x => (string?)x).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
        var drawings = !(a["drawings"] is JsonValue dv && dv.TryGetValue<bool>(out var db) && !db);
        var details = new JsonArray();
        var placed = new List<(string Title, Views.Rendered R)>();
        foreach (var (name, build) in MaterialReferenceDetails.All)
        {
            if (only != null && !only.Contains(name)) continue;
            var session = new Session(catalog, new ProductManifest("standard-preview", "Standard preview", ["core", .. catalog.Modules.Select(x => x.Id).Where(x => x != "core")]));
            session.Edit("Standard", edit =>
            {
                if (Spatial.Project(edit.Model) is not { } p) return;
                var attrs = p.CloneAttrs();
                foreach (var k in new[] { "hatchPatterns", "drafting" }) if (source?.Attrs[k] is { } v) attrs[k] = v.DeepClone();
                if (!s.BuiltIn) attrs[ListKey] = new JsonArray((JsonNode)s.ToJson());
                if (!s.IsLapin) attrs[Key] = s.Id;
                TakePatterns(attrs, s, out _, out _);
                edit.Replace(p.With(attrs: attrs));
            });
            MaterialReferenceDetails.Detail d;
            try
            {
                MaterialReferenceDetails.EveryLayerInPlans(session);
                d = build(session);
            }
            catch (CommandException) { continue; }   // a detail whose commands the product lacks
            var views = new JsonArray();
            foreach (var (vname, args) in d.Views)
            {
                var va = (JsonObject)args.DeepClone();
                va["scale"] = scale;
                va["name"] = $"{d.Title} — {vname}";
                session.Execute("view.create", va, out var made);
                var view = Views.Find(session.Model, (string?)made?["id"] ?? "") ?? throw new CommandException("The sample's view could not be made.");
                var reader = session.Reader;
                var r = Views.Render(reader, view);
                placed.Add(($"{d.Title} — {vname}", r));
                var vo = new JsonObject { ["name"] = vname, ["kind"] = (string?)args["kind"] };
                if (drawings) vo["drawing"] = Views.DrawingOf(reader, view, r);
                views.Add((JsonNode)vo);
            }
            details.Add((JsonNode)new JsonObject { ["name"] = d.Name, ["title"] = d.Title, ["views"] = views });
        }
        var o = new JsonObject { ["standard"] = s.Summary(), ["scale"] = scale, ["details"] = details };
        var svg = a["svg"] is JsonValue sv && sv.TryGetValue<bool>(out var sb) && sb;
        var pdf = a["pdf"] is JsonValue pv && pv.TryGetValue<bool>(out var pb) && pb;
        if (svg || pdf)
        {
            var page = Sheet(s, scale, placed);
            if (svg) o["svg"] = SvgWriter.Write(page);
            if (pdf) o["pdf"] = Convert.ToBase64String(PdfWriter.Write([page], info: null));
        }
        return o;
    }

    /// <summary>One page of the details' model lines at their scale, in rows, under a title naming the standard.</summary>
    static PaperPage Sheet(MaterialStandard s, double scale, List<(string Title, Views.Rendered R)> placed)
    {
        const double margin = 15, gap = 18, captionGap = 7;
        var width = 594.0;
        var pens = new List<Pen>();
        var text = new Pen("TEXT", "#000000", 0, null, null);
        pens.Add(text);
        var title = s.Name + (s.Norm is { Length: > 0 } nm && !s.Name.Contains(nm, StringComparison.OrdinalIgnoreCase) ? $" ({nm})" : "");
        text.Texts.Add(new PenText(new Vec2(margin, margin + 6), title, 6, Anchor: "start"));
        text.Texts.Add(new PenText(new Vec2(margin, margin + 13), $"Lapin material standard „{s.Id}” v{s.Version ?? "1"} · 1:{scale:0.##}" + (s.Indicative ? " · indicative" : ""), 3, Anchor: "start"));
        double x = margin, y = margin + 26, rowH = 0;
        var k = 1000 / scale;
        foreach (var (caption, r) in placed)
        {
            if (r.Bounds is not { } b) continue;
            var (w, h) = ((b.Max.X - b.Min.X) * k, (b.Max.Y - b.Min.Y) * k);
            if (x > margin && x + Math.Max(w, caption.Length * 1.4) > width - margin) { x = margin; y += rowH + gap + captionGap; rowH = 0; }
            var (ox, oy) = (x, y);
            Vec2 P(Vec2 p) => new(ox + (p.X - b.Min.X) * k, oy + (b.Max.Y - p.Y) * k);
            foreach (var pen in r.Pens.Where(p => ModelLayers.Contains(p.Layer)))
            {
                var c = new Pen(pen.Layer, pen.Stroke, pen.Width, pen.Fill, pen.Dash);
                foreach (var (sa, sb) in pen.Segments) c.Segments.Add((P(sa), P(sb)));
                foreach (var l in pen.Loops) c.Loops.Add(l.Select(P).ToArray());
                if (c.Segments.Count > 0 || c.Loops.Count > 0) pens.Add(c);
            }
            text.Texts.Add(new PenText(new Vec2(x, y + h + captionGap), caption, 2.5, Anchor: "start"));
            // As wide as the view or its caption (2.5 mm text: about 1.4 mm a letter), whichever is wider.
            x += Math.Max(Math.Max(w, 40), caption.Length * 1.4) + gap;
            rowH = Math.Max(rowH, h);
        }
        return new PaperPage(width, Math.Max(200, y + rowH + captionGap + margin), pens, s.Name);
    }

    // ------------------------------------------------------------ commands

    public static void Register(IModuleRegistry r)
    {
        // Every material standard: the built-in ones, then the project's; which one the project draws by.
        r.Query("standards.materialList", (q, _) =>
        {
            var project = Spatial.Project(q.Model);
            return new JsonObject
            {
                ["current"] = Of(project).Id,
                ["standards"] = new JsonArray(BuiltInsOf(project).Concat(Own(project)).Select(s => (JsonNode)s.Summary()).ToArray()),
            };
        });

        // One standard whole ({id}): its fields, rules, patterns and pens, whether it is built in.
        r.Query("standards.materialGet", (q, a) =>
        {
            var s = Find(Spatial.Project(q.Model), (string?)a["id"]) ?? throw new CommandException($"There is no material standard „{(string?)a["id"]}”.");
            return new JsonObject { ["standard"] = s.ToJson(), ["builtin"] = s.BuiltIn, ["current"] = Of(Spatial.Project(q.Model)).Id == s.Id };
        });

        // A standard of the project's made or changed ({standard: {id?, name, …, rules, patterns?, pens?}}): checked; a built-in id refused.
        r.Command("standards.materialSet", "Material standard", (ctx, a) =>
        {
            var given = a["standard"] as JsonObject ?? throw new CommandException("Give the standard: {name, rules, …}.");
            var project = Project(ctx.Model);
            var json = (JsonObject)given.DeepClone();
            if (((string?)json["id"])?.Trim() is not { Length: > 0 } id) json["id"] = id = FreeId(project, (string?)json["name"] ?? "standard");
            if (MaterialStandard.FindBuiltIn(id) != null) throw new CommandException($"„{id}” is built in: duplicate it to change it.");
            var s = Checked(ctx.Model, json);
            WriteOwn(ctx, s.Id, s.ToJson());
            return new JsonObject { ["id"] = s.Id };
        });

        // A copy of a standard (built-in or the project's) kept as the project's ({id, name?}), to edit; the built-in one stays as it was.
        r.Command("standards.materialDuplicate", "Duplicate material standard", (ctx, a) =>
        {
            var project = Project(ctx.Model);
            var from = Find(project, (string?)a["id"]) ?? throw new CommandException($"There is no material standard „{(string?)a["id"]}”.");
            var json = from.ToJson();
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : LibraryMerge.FreeName(x => Own(project).Concat(MaterialStandard.BuiltIns).Any(s => s.Name.Equals(x, StringComparison.OrdinalIgnoreCase)), from.Name);
            json["id"] = FreeId(project, name);
            json["name"] = name;
            json["basedOn"] = from.Id;
            var s = Checked(ctx.Model, json);
            WriteOwn(ctx, s.Id, s.ToJson());
            return new JsonObject { ["id"] = s.Id };
        });

        // A standard of the project's removed ({id}); a built-in one cannot be. The project drawing by it goes back to Lapin's.
        r.Command("standards.materialRemove", "Remove material standard", (ctx, a) =>
        {
            var id = (string?)a["id"] ?? throw new CommandException("Which standard?");
            if (MaterialStandard.FindBuiltIn(id) != null) throw new CommandException($"„{id}” is built in: it cannot be removed.");
            var project = Project(ctx.Model);
            if (!Own(project).Any(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) && !(project.Attrs[ListKey] as JsonArray ?? []).OfType<JsonObject>().Any(x => string.Equals((string?)x["id"], id, StringComparison.OrdinalIgnoreCase)))
                throw new CommandException($"The project has no material standard „{id}”.");
            WriteOwn(ctx, id, null);
            return new JsonObject { ["current"] = Of(Project(ctx.Model)).Id };
        });

        // The project's standard set ({id, overwrite?, scale?}); preview: true (a query) the materials' looks before and after, nothing changed.
        r.Command("standards.applyMaterials", "Apply material standard", (ctx, a) =>
        {
            var s = Find(Spatial.Project(ctx.Model), (string?)a["id"]) ?? throw new CommandException($"There is no material standard „{(string?)a["id"]}”.");
            var overwrite = a["overwrite"] is JsonValue ov && ov.TryGetValue<bool>(out var o) && o;
            var preview = a["preview"] is JsonValue pv && pv.TryGetValue<bool>(out var p) && p;
            return Apply(preview ? null : ctx, ctx.Model, s, overwrite, preview, Json.Number(a["scale"]));
        });

        // The same preview as a query ({id, overwrite?, scale?}): what applying would change, the model untouched.
        r.Query("standards.applyPreview", (q, a) =>
        {
            var s = Find(Spatial.Project(q.Model), (string?)a["id"]) ?? throw new CommandException($"There is no material standard „{(string?)a["id"]}”.");
            var overwrite = a["overwrite"] is JsonValue ov && ov.TryGetValue<bool>(out var o) && o;
            return Apply(null, q.Model, s, overwrite, true, Json.Number(a["scale"]));
        });

        // A standard made from the project's materials ({name, description?, author?, country?, norm?, version?, license?, languages?,
        // language?, indicative?, withRules?}) and kept as the project's.
        r.Command("standards.fromProject", "Standard from project", (ctx, a) =>
        {
            var s = Checked(ctx.Model, FromProject(ctx.Model, a));
            WriteOwn(ctx, s.Id, s.ToJson());
            return new JsonObject { ["id"] = s.Id, ["rules"] = s.Rules.Count };
        });

        // The reference details drawn with a standard ({id | standard, scale?, details?, drawings?, svg?, pdf?}).
        r.Query("standards.previewSheet", (q, a) => PreviewSheet(q, a));
    }
}
