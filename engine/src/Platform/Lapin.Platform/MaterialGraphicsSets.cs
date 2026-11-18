using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Drawing;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Material graphics sets (stage 4 of docs/plan-material-graphics.md, decision 682): the project's named sets (<see cref="Key"/>)
/// of what a discipline's drawings make of its materials, over what the materials and the standard give: „Structure” draws
/// concrete solid grey and masonry in 45° lines, finishes plain, insulation halftone. A view style names one
/// (<c>materialGraphicsSet</c>), a view may name another or none (<c>view.setStyle</c>); it is the third tier of
/// <see cref="ViewMaterialGraphics"/>, under the view's filters and its subcategories' graphics, over the small-scale cut and
/// the material's own.
/// <para>
/// A set is <c>{id, name, entries: [{match: {material?, materialName?, category?}, section?, plan?, elevation?}]}</c>: each kind
/// of view's graphics as a material's (every field of decisions 678–680, steps by scale too). An entry matches a material when
/// all it gives holds: <c>material</c> its id; <c>materialName</c> a word or a small expression found in its name (folded as a
/// standard folds it: lower case, one space, no diacritics); <c>category</c> its category (its own, else what the project's
/// standard says of its name); an entry matching nothing in particular (<c>match: {}</c>) takes every material. Of the entries
/// that match, the most particular wins (a material's id over a name over a category over all), the first of them in order
/// when two are as particular.
/// </para>
/// A project made since decision 682 has two built in (<see cref="BuiltIns"/>: „Structure” for the Structural style, „MEP” for
/// the MEP style) until it changes its list; one made before has none, and its styles name none (Î10). Every command one undo step.
/// </summary>
public static class MaterialGraphicsSets
{
    /// <summary>The project's sets (absent: the built-in ones in a project made since decision 682, none in one made before).</summary>
    public const string Key = "materialGraphicsSets";
    public const string StructureId = "structure", MepId = "mep";
    static readonly HashSet<string> SetFields = ["id", "name", "entries"];
    static readonly HashSet<string> EntryFields = ["match", "section", "plan", "elevation"];
    static readonly HashSet<string> MatchFields = ["material", "materialName", "category"];

    /// <summary>An entry of a set: what it matches and its kinds of view's graphics (<see cref="Json"/>, as given).</summary>
    public sealed class Entry
    {
        public string? Material { get; init; }
        public string? MaterialName { get; init; }
        public string? Category { get; init; }
        internal Regex? NameRe { get; init; }
        public required JsonObject Json { get; init; }

        /// <summary>How particular it is: a material's id 3, a name 2, a category 1, every material 0.</summary>
        public int Specificity => Material != null ? 3 : MaterialName != null ? 2 : Category != null ? 1 : 0;

        public bool Matches(string? materialId, string folded, string? category)
        {
            if (Material != null && !string.Equals(Material, materialId, StringComparison.Ordinal)) return false;
            if (Category != null && !string.Equals(Category, category, StringComparison.OrdinalIgnoreCase)) return false;
            if (NameRe != null)
            {
                try { if (!NameRe.IsMatch(folded)) return false; }
                catch (RegexMatchTimeoutException) { return false; }
            }
            return true;
        }

        /// <summary>Its graphics for a kind of view at 1:<paramref name="scale"/> (a plan: the section's, then its own; each with its step).</summary>
        public ViewGraphics? For(string kind, double? scale) => MaterialGraphics.OwnOf(Json, kind, scale).Own;
    }

    /// <summary>A set: its id, name and entries in order; <see cref="Json"/> as the project keeps it.</summary>
    public sealed record Set(string Id, string Name, IReadOnlyList<Entry> Entries, JsonObject Json)
    {
        /// <summary>The entry that rules a material (its id when the project keeps it, its name, its category): the most particular that matches, the first of those.</summary>
        public Entry? For(string? materialId, string name, string? category)
        {
            var folded = Fold(name);
            Entry? best = null;
            foreach (var e in Entries)
                if ((best == null || e.Specificity > best.Specificity) && e.Matches(materialId, folded, category)) best = e;
            return best;
        }
    }

    /// <summary>A name as the entries see it: trimmed, lower case, one space, every diacritic off.</summary>
    static string Fold(string name) => MaterialStandard.FoldText(MaterialGraphics.Normalise(name), MaterialStandard.FoldAll);

    // ------------------------------------------------------------ reading

    /// <summary>A set read from its JSON (an exception naming what is wrong): an id, a name, entries with known fields and a valid name expression.</summary>
    public static Set Read(JsonObject o)
    {
        foreach (var (k, _) in o) if (!SetFields.Contains(k)) throw new FormatException($"„{k}” is not a field of a material graphics set (id, name, entries).");
        var id = ((string?)o["id"])?.Trim() is { Length: > 0 and <= 80 } i ? i : throw new FormatException("A material graphics set needs an id.");
        var name = ((string?)o["name"])?.Trim() is { Length: > 0 and <= 120 } n ? n : throw new FormatException("A material graphics set needs a name (120 characters at most).");
        var entries = new List<Entry>();
        var canon = new JsonArray();
        var at = 0;
        foreach (var x in o["entries"] as JsonArray ?? [])
        {
            at++;
            var e = ReadEntry(x as JsonObject ?? throw new FormatException($"Entry {at} of „{name}” is not an object."), $"„{name}”, entry {at}");
            entries.Add(e);
            canon.Add((JsonNode)e.Json.DeepClone());
        }
        if (entries.Count > 200) throw new FormatException($"„{name}” has more than 200 entries.");
        return new Set(id, name, entries, new JsonObject { ["id"] = id, ["name"] = name, ["entries"] = canon });
    }

    static Entry ReadEntry(JsonObject o, string where)
    {
        foreach (var (k, _) in o) if (!EntryFields.Contains(k)) throw new FormatException($"{where}: „{k}” is not a field of an entry (match, section, plan, elevation).");
        var match = o["match"] as JsonObject ?? (o["match"] is null ? new JsonObject() : throw new FormatException($"{where}: match is {{material?, materialName?, category?}}."));
        foreach (var (k, _) in match) if (!MatchFields.Contains(k)) throw new FormatException($"{where}: „{k}” is not a field of match (material, materialName, category).");
        var material = ((string?)match["material"])?.Trim() is { Length: > 0 } mid ? mid : null;
        var materialName = ((string?)match["materialName"])?.Trim() is { Length: > 0 } mn ? mn : null;
        if (materialName is { Length: > 200 }) throw new FormatException($"{where}: a material name is 200 characters at most.");
        var category = ((string?)match["category"])?.Trim() is { Length: > 0 } c
            ? MaterialStandard.Categories.FirstOrDefault(x => x.Equals(c, StringComparison.OrdinalIgnoreCase)) ?? throw new FormatException($"{where}: „{c}” is not a category ({string.Join(", ", MaterialStandard.Categories)}).")
            : null;
        Regex? re = null;
        if (materialName != null)
        {
            try { re = new Regex(MaterialStandard.FoldText(materialName, MaterialStandard.FoldAll), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)); }
            catch (ArgumentException e) { throw new FormatException($"{where}: „{materialName}” is not a valid name expression ({e.Message})."); }
        }
        var mo = new JsonObject();
        if (material != null) mo["material"] = material;
        if (materialName != null) mo["materialName"] = materialName;
        if (category != null) mo["category"] = category;
        var json = new JsonObject { ["match"] = mo };
        foreach (var kind in MaterialGraphics.Kinds)
            if (o[kind] is JsonObject g) json[kind] = g.DeepClone();
            else if (o[kind] != null) throw new FormatException($"{where}: {kind} is a kind of view's graphics, as a material's.");
        return new Entry { Material = material, MaterialName = materialName, Category = category, NameRe = re, Json = json };
    }

    // ------------------------------------------------------------ the built-in ones

    const string BuiltInJson = """
    [
      { "id": "structure", "name": "Structure", "entries": [
        { "match": { "materialName": "block|\\bcmu\\b|aac|ytong|aerated|\\bbca\\b|boltari|blocuri" }, "section": { "hatch": "MASONRY" } },
        { "match": { "materialName": "concrete|beton|screed|sapa|reinforced|armat|c\\d\\d/" }, "section": { "hatch": null, "fill": "#999999" } },
        { "match": { "materialName": "brick|masonry|clinker|stone|caramid|zidari|piatra" }, "section": { "hatch": "MASONRY" } },
        { "match": { "category": "Finish" }, "section": { "hatch": null, "fill": null } },
        { "match": { "category": "Insulation" }, "section": { "halftone": true } }
      ] },
      { "id": "mep", "name": "MEP", "entries": [
        { "match": {}, "section": { "hatch": null, "fill": null, "halftone": true, "cutLine": { "color": "#999999" }, "separatorLine": { "color": "#999999" } },
          "elevation": { "hatch": null, "fill": null } }
      ] }
    ]
    """;

    static readonly Lazy<IReadOnlyList<Set>> Shipped = new(() => [.. JsonNode.Parse(BuiltInJson)!.AsArray().OfType<JsonObject>().Select(Read)]);

    /// <summary>
    /// The sets a project made since decision 682 starts with: „Structure” (the Structural style's: concrete solid grey 40 %, blocks,
    /// brick and stone in 45° lines, finishes with no hatch and no fill, insulation halftone) and „MEP” (the MEP style's: no hatch,
    /// no fill, the cut lines grey, the hatch halftone).
    /// </summary>
    public static IReadOnlyList<Set> BuiltIns => Shipped.Value;

    // ------------------------------------------------------------ the project's

    static readonly ConditionalWeakTable<JsonNode, List<Set>> Parsed = new();

    /// <summary>The project's sets: its list (one it cannot read left out), else the built-in ones in a project made since decision 682.</summary>
    public static IReadOnlyList<Set> All(Entity? project) =>
        project?.Attrs[Key] is JsonArray a ? Parsed.GetValue(a, n => [.. ((JsonArray)n).OfType<JsonObject>().Select(TryRead).OfType<Set>()])
        : MaterialStandards.NewLook(project) ? BuiltIns : [];

    public static IReadOnlyList<Set> All(IModelView m) => All(Spatial.Project(m));

    static Set? TryRead(JsonObject o)
    {
        try { return Read(o); }
        catch (FormatException) { return null; }
    }

    public static Set? Find(IModelView m, string? id) => id == null ? null : All(m).FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The category a material's set entries see: its own, else what the project's standard says of its name.</summary>
    public static string? CategoryOf(Entity? material, string name, MaterialStandard standard) =>
        material?.Str("category") ?? standard.CutRule(name)?.Category ?? standard.ElevationRule(name)?.Category;

    /// <summary>
    /// A set given by a command or a file, checked against the project: its fields (<see cref="Read"/>), a material id one of its
    /// materials, each kind's graphics as <c>material.update</c> checks a material's (patterns and line types the project knows).
    /// </summary>
    public static Set Checked(IModelView m, JsonObject given)
    {
        Set read;
        try { read = Read(given); }
        catch (FormatException e) { throw new CommandException(e.Message); }
        var entries = new JsonArray();
        for (var i = 0; i < read.Entries.Count; i++)
        {
            var e = (JsonObject)read.Entries[i].Json.DeepClone();
            var where = $"„{read.Name}”, entry {i + 1}";
            if (read.Entries[i].Material is { } mid && m.Get(mid) is not { Class: IfcClass.Material })
                throw new CommandException($"{where}: there is no material {mid}.");
            foreach (var kind in MaterialGraphics.Kinds)
                if (e[kind] is JsonObject g) e[kind] = DraftingResources.CheckStandardGraphics(m, g, $"{where}, {kind}", []);
            entries.Add((JsonNode)e);
        }
        try { return Read(new JsonObject { ["id"] = read.Id, ["name"] = read.Name, ["entries"] = entries }); }
        catch (FormatException e) { throw new CommandException(e.Message); }
    }

    static Entity Project(IModelView m) => Spatial.Project(m) ?? throw new CommandException("The model has no project.");

    static void Write(ICommandContext ctx, IEnumerable<Set> sets)
    {
        var project = Project(ctx.Model);
        var attrs = project.CloneAttrs();
        // Kept even empty: a project made since decision 682 that removed its sets does not get the built-in ones back.
        attrs[Key] = new JsonArray(sets.Select(s => (JsonNode)s.Json.DeepClone()).ToArray());
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>An id no set of the project has, made from a name.</summary>
    static string FreeId(IReadOnlyList<Set> sets, string name)
    {
        var slug = Regex.Replace(MaterialStandard.FoldText(name.Trim().ToLowerInvariant(), MaterialStandard.FoldAll), "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0) slug = "set";
        if (slug.Length > 60) slug = slug[..60].TrimEnd('-');
        bool Taken(string id) => sets.Any(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
        if (!Taken(slug)) return slug;
        var n = 2;
        while (Taken($"{slug}-{n}")) n++;
        return $"{slug}-{n}";
    }

    static JsonObject Summary(IModelView m, Set s) => new()
    {
        ["id"] = s.Id, ["name"] = s.Name, ["entries"] = s.Json["entries"]!.DeepClone(),
        ["builtIn"] = BuiltIns.Any(b => b.Id == s.Id), ["edited"] = BuiltIns.FirstOrDefault(b => b.Id == s.Id) is { } b && !JsonNode.DeepEquals(b.Json, s.Json),
        // The styles that name it.
        ["styles"] = new JsonArray(ViewStyles.All(m).Where(st => string.Equals(st.MaterialGraphicsSet, s.Id, StringComparison.OrdinalIgnoreCase)).Select(st => (JsonNode)st.Id).ToArray()),
    };

    // ------------------------------------------------------------ the standards' file

    /// <summary>
    /// A set as a file carries it: an entry naming a material by its id names it by its name instead (<c>^name$</c>), so the file
    /// carries no ids of this project.
    /// </summary>
    public static JsonObject ForFile(IModelView m, Set s)
    {
        var o = (JsonObject)s.Json.DeepClone();
        foreach (var e in (o["entries"] as JsonArray ?? []).OfType<JsonObject>())
            if (e["match"] is JsonObject mo && (string?)mo["material"] is { } mid)
            {
                mo.Remove("material");
                if (m.Get(mid) is { } mat) mo["materialName"] = "^" + Regex.Escape(Fold(mat.Name)) + "$";
            }
        return o;
    }

    /// <summary>
    /// The project's sets as a file's section (null: none): <paramref name="ids"/> those alone, else every set but a built-in one
    /// left as shipped (a project made since has it already).
    /// </summary>
    public static JsonArray? FileSection(IModelView m, IReadOnlyList<string>? ids = null)
    {
        var all = All(m);
        var list = ids == null ? all.Where(s => !BuiltIns.Any(b => JsonNode.DeepEquals(b.Json, s.Json))).ToList() : ids.Select(id => all.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)) ?? throw new CommandException($"There is no material graphics set „{id}”.")).ToList();
        return list.Count == 0 ? null : new JsonArray(list.Select(s => (JsonNode)ForFile(m, s)).ToArray());
    }

    /// <summary>A file's sets, each read (one that cannot be is refused, naming it).</summary>
    public static List<Set> InFile(JsonObject file)
    {
        var o = new List<Set>();
        foreach (var x in file[Key] as JsonArray ?? [])
        {
            if (x is not JsonObject so) continue;
            try { o.Add(Read(so)); }
            catch (FormatException e) { throw new CommandException($"A material graphics set of the file cannot be read: {e.Message}"); }
        }
        return o;
    }

    /// <summary>Whether the project has a file's set (by id) and whether it is the same (as a file carries both).</summary>
    public static (bool Exists, bool Same) Compare(IModelView m, Set s)
    {
        var have = Find(m, s.Id);
        return (have != null, have != null && JsonNode.DeepEquals(ForFile(m, have), s.Json));
    }

    /// <summary>
    /// A file's sets brought into the project (in <see cref="StandardsLibrary"/>'s import, one undo step): one it lacks added, one
    /// the same kept, one that differs as its choice says (update; both: „Name (2)” beside; skip). Each checked against the project.
    /// </summary>
    public static void Import(ICommandContext ctx, JsonObject file, Func<string, string, string> choice, ref int added, ref int updated, ref int kept, ref int skipped, List<string> renamed)
    {
        var given = InFile(file);
        if (given.Count == 0) return;
        var list = All(ctx.Model).ToList();
        var changed = false;
        foreach (var s in given)
        {
            var (exists, same) = Compare(ctx.Model, s);
            if (!exists) { list.Add(Checked(ctx.Model, s.Json)); added++; changed = true; continue; }
            if (same) { kept++; continue; }
            var c = choice("materialGraphicsSet", s.Id);
            if (c == LibraryMerge.Skip) { skipped++; continue; }
            if (c == LibraryMerge.Both)
            {
                var copy = (JsonObject)s.Json.DeepClone();
                copy["id"] = FreeId(list, s.Id);
                var name = list.Any(x => x.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase)) ? LibraryMerge.FreeName(n => list.Any(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase)), s.Name) : s.Name;
                copy["name"] = name;
                list.Add(Checked(ctx.Model, copy));
                added++;
                renamed.Add(name);
                changed = true;
                continue;
            }
            var at = list.FindIndex(x => string.Equals(x.Id, s.Id, StringComparison.OrdinalIgnoreCase));
            list[at] = Checked(ctx.Model, s.Json);
            updated++;
            changed = true;
        }
        if (changed) Write(ctx, list);
    }

    // ------------------------------------------------------------ commands

    public static void Register(IModuleRegistry r)
    {
        // The project's sets, each whole ({id, name, entries, builtIn, edited, styles}); the categories an entry can match.
        r.Query("materialGraphicsSet.list", (q, _) => new JsonObject
        {
            ["sets"] = new JsonArray(All(q.Model).Select(s => (JsonNode)Summary(q.Model, s)).ToArray()),
            ["categories"] = new JsonArray(MaterialStandard.Categories.Select(c => (JsonNode)c).ToArray()),
        });

        // A new set ({name, from?: a set's id to copy, entries?}): its id.
        r.Command("materialGraphicsSet.create", "New material graphics set", (ctx, a) =>
        {
            var list = All(ctx.Model).ToList();
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A material graphics set needs a name.");
            if (list.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new CommandException($"There is a material graphics set „{name}” already.");
            var entries = a["entries"] is JsonArray given ? given.DeepClone()
                : (string?)a["from"] is { } from ? (list.FirstOrDefault(s => string.Equals(s.Id, from, StringComparison.OrdinalIgnoreCase)) ?? throw new CommandException("There is no such material graphics set.")).Json["entries"]!.DeepClone()
                : new JsonArray();
            var made = Checked(ctx.Model, new JsonObject { ["id"] = FreeId(list, name), ["name"] = name, ["entries"] = entries });
            list.Add(made);
            Write(ctx, list);
            return new JsonObject { ["id"] = made.Id };
        });

        // A set changed ({id, name?, entries?}): its entries replaced as given.
        r.Command("materialGraphicsSet.update", "Change material graphics set", (ctx, a) =>
        {
            var list = All(ctx.Model).ToList();
            var at = list.FindIndex(s => string.Equals(s.Id, (string?)a["id"], StringComparison.OrdinalIgnoreCase));
            if (at < 0) throw new CommandException("There is no such material graphics set.");
            var json = (JsonObject)list[at].Json.DeepClone();
            if (a.ContainsKey("name"))
            {
                var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A material graphics set needs a name.");
                if (list.Where((_, i) => i != at).Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new CommandException($"There is a material graphics set „{name}” already.");
                json["name"] = name;
            }
            if (a.ContainsKey("entries")) json["entries"] = a["entries"] is JsonArray e ? e.DeepClone() : throw new CommandException("entries is a list: [{match, section?, plan?, elevation?}].");
            list[at] = Checked(ctx.Model, json);
            Write(ctx, list);
            return null;
        });

        // A set removed ({id}): the styles naming it name none; views naming it take their style's again.
        r.Command("materialGraphicsSet.delete", "Delete material graphics set", (ctx, a) =>
        {
            var list = All(ctx.Model).ToList();
            var at = list.FindIndex(s => string.Equals(s.Id, (string?)a["id"], StringComparison.OrdinalIgnoreCase));
            if (at < 0) throw new CommandException("There is no such material graphics set.");
            var id = list[at].Id;
            list.RemoveAt(at);
            Write(ctx, list);
            ViewStyles.ForgetMaterialSet(ctx, id);
            return null;
        });
    }
}
