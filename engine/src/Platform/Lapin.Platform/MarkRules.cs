using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>A kind of element its marks are built for (walls, door types…): the classes in it, whether they are types, whether its names end in the number Lapin gave it (D3, Stair 2).</summary>
public sealed record MarkCategory(string Id, string Label, string[] Classes, bool IsType = false, bool NumberedNames = false);

/// <summary>
/// A category's mark rule (decision 737): a <see cref="Pattern"/> of live text with its <see cref="Prefix"/> and <see cref="Suffix"/>,
/// its number counted in the whole project or on each storey (<see cref="Scope"/>).
/// </summary>
public sealed record MarkRule(string Prefix = "", string Suffix = "", string Pattern = MarkRules.DefaultPattern, string Scope = "project")
{
    /// <summary>The built-in rule: the mark Lapin gives by itself, nothing before or after it.</summary>
    public bool IsDefault => Prefix.Length == 0 && Suffix.Length == 0 && Pattern == MarkRules.DefaultPattern && Scope == "project";
    public bool PerStorey => Scope == "storey";
    public bool UsesNumber => MarkRules.UsesNumber(Pattern);

    public JsonObject ToJson() => new() { ["prefix"] = Prefix, ["suffix"] = Suffix, ["pattern"] = Pattern, ["scope"] = Scope };

    public static MarkRule From(JsonNode? n)
    {
        if (n is not JsonObject o) return new();
        string Text(string k) => o[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
        var pattern = Text("pattern").Trim();
        return new(Text("prefix"), Text("suffix"), pattern.Length > 0 ? pattern : MarkRules.DefaultPattern, Text("scope") == "storey" ? "storey" : "project");
    }
}

/// <summary>
/// Marks the user builds (decision 737, the project owner's decision of 19.11.2026: „marks built by the user, prefix and suffix,
/// like a formula”). Each category (walls, wall types, doors, windows, their types, floors, columns, beams, footings, stairs…)
/// has a rule kept on the project (<c>markRules</c>): a pattern of live text, <c>{prefix}{mark}{suffix}</c> by default, where
/// <c>{mark}</c> is the mark Lapin gives by itself (its type's mark, else its type's name, else its name), <c>{n}</c> the
/// element's number in its category (kept on it, <c>markNo</c>, never moved by itself), <c>{= n | 00}</c> a formula, and any
/// field a tag reads: <c>{storey.code}</c>, <c>{type.mark}</c>, <c>{width}</c>, a parameter. The built-in rule gives exactly the
/// marks Lapin gave before, so a project changes only where the user edits a rule. An element's own mark
/// (<c>element.setMark</c>) still wins, and cleared it goes back to the rule; the shop drawings' register (decision 583) goes before
/// both, as before. Numbers are given when a rule first reads <c>{n}</c> (seeded from the names Lapin numbered, D3 → 3, or from an
/// element's own mark), to each new element after any command (the next in its category, or on its storey), and again by
/// <c>markRules.renumber</c>, in one undo step. Elements an IFC file brought keep the file's marks: no rule reads them.
/// Rebar keeps its own scheme (decision 729), written the same way.
/// </summary>
public static partial class MarkRules
{
    public const string Key = "markRules";
    public const string NumberKey = "markNo";
    public const string DefaultPattern = "{prefix}{mark}{suffix}";
    public static readonly string[] Orders = ["rows", "columns", "current"];
    /// <summary>The tokens a rule reads besides any field of the element.</summary>
    public static readonly string[] Tokens = ["prefix", "suffix", "mark", "n"];

    public static readonly IReadOnlyList<MarkCategory> Categories =
    [
        new("wall", "Walls", [IfcClass.Wall]),
        new("wallType", "Wall types", [IfcClass.WallType], IsType: true),
        new("door", "Doors", [IfcClass.Door], NumberedNames: true),
        new("doorType", "Door types", [IfcClass.DoorType], IsType: true),
        new("window", "Windows", [IfcClass.Window], NumberedNames: true),
        new("windowType", "Window types", [IfcClass.WindowType], IsType: true),
        new("floor", "Floors", [IfcClass.Slab]),
        new("floorType", "Floor types", [IfcClass.SlabType], IsType: true),
        new("column", "Columns", [IfcClass.Column]),
        new("columnType", "Column types", [IfcClass.ColumnType], IsType: true),
        new("beam", "Beams", [IfcClass.Beam]),
        new("beamType", "Beam types", [IfcClass.BeamType], IsType: true),
        new("footing", "Footings", [IfcClass.Footing]),
        new("footingType", "Footing types", [IfcClass.FootingType], IsType: true),
        new("stair", "Stairs and ramps", [IfcClass.Stair, IfcClass.Ramp], NumberedNames: true),
    ];

    static readonly Dictionary<string, MarkCategory> byClass = Categories.SelectMany(c => c.Classes.Select(x => (x, c))).ToDictionary(p => p.x, p => p.c);

    public static MarkCategory? CategoryOf(string ifcClass) => byClass.GetValueOrDefault(ifcClass);

    public static MarkCategory Category(string? id) =>
        Categories.FirstOrDefault(c => c.Id == id) ?? throw new CommandException($"Marks are built for {string.Join(", ", Categories.Select(c => c.Id))}, not „{id}”.");

    /// <summary>Whether a pattern reads the number: {n}, or n in a formula.</summary>
    public static bool UsesNumber(string pattern) => pattern.Contains("{n}", StringComparison.Ordinal) || FormulaN().IsMatch(pattern);

    [GeneratedRegex(@"\{=[^{}]*(?<![\w.])n(?![\w.(])[^{}]*\}")]
    private static partial Regex FormulaN();

    /// <summary>The rules the project keeps, read once per project as it is (an edit makes a new one).</summary>
    static readonly ConditionalWeakTable<Entity, Dictionary<string, MarkRule>> cache = [];

    public static IReadOnlyDictionary<string, MarkRule> All(IModelView m)
    {
        if (Spatial.Project(m) is not { } project || project.Attrs[Key] is not JsonObject) return Empty;
        return cache.GetValue(project, p =>
        {
            var d = new Dictionary<string, MarkRule>();
            foreach (var (k, v) in (JsonObject)p.Attrs[Key]!)
                if (Categories.Any(c => c.Id == k) && MarkRule.From(v) is { IsDefault: false } rule) d[k] = rule;
            return d;
        });
    }

    static readonly Dictionary<string, MarkRule> Empty = [];

    /// <summary>A category's rule: the project's, else the built-in one.</summary>
    public static MarkRule RuleFor(IModelView m, string category) => All(m).GetValueOrDefault(category) ?? new();

    /// <summary>Whether its marks are the file's: an element an IFC import brought keeps them.</summary>
    static bool Foreign(Entity e) => e.Attrs[IfcSource.Key] != null;

    /// <summary>The rule the user set for the element's category; null when it has the built-in one (or came from a file).</summary>
    public static MarkRule? RuleOf(IModelView m, Entity e)
    {
        var all = All(m);
        if (all.Count == 0 || CategoryOf(e.Class) is not { } c || Foreign(e)) return null;
        return all.GetValueOrDefault(c.Id);
    }

    /// <summary>The element's number in its category; null when it has none.</summary>
    public static int? Number(Entity e) => e.NumOrNull(NumberKey) is { } d && d >= 1 && d < int.MaxValue ? (int)d : null;

    [ThreadStatic] static int depth;

    /// <summary>The mark the user's rule gives the element; null when its category keeps the built-in rule.</summary>
    public static string? Evaluate(IModelView m, Entity e) => RuleOf(m, e) is { } rule ? Apply(m, e, rule, Number(e)) : null;

    /// <summary>The rule's pattern filled for the element, its number <paramref name="n"/>: never empty (the built-in mark then).</summary>
    public static string Apply(IModelView m, Entity e, MarkRule rule, int? n)
    {
        var coded = TagText.Coded(m, e);
        // A rule reading another element's mark that reads this one's: the built-in mark rather than going round.
        if (depth > 6) return coded;
        depth++;
        try
        {
            var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["prefix"] = rule.Prefix, ["suffix"] = rule.Suffix, ["n"] = n?.ToString(CultureInfo.InvariantCulture) ?? "",
                ["mark"] = coded, ["tag"] = coded,
            };
            var text = TagText.Fill(rule.Pattern, m, e, extra).Trim();
            return text.Length > 0 ? text : coded;
        }
        finally { depth--; }
    }

    /// <summary>A type's mark as tags and IFC read it: its own, else its category's rule; null when neither gives one.</summary>
    public static string? TypeMark(IModelView m, Entity? type) => type == null ? null : type.Str("mark") ?? Evaluate(m, type);

    // ------------------------------------------------------------ numbers

    /// <summary>The digits a text ends in (D12 → 12); 0 when none.</summary>
    public static int Trailing(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var i = text.Length;
        while (i > 0 && char.IsAsciiDigit(text[i - 1])) i--;
        return i < text.Length && text.Length - i <= 9 && int.TryParse(text.AsSpan(i), out var n) ? n : 0;
    }

    /// <summary>The number an unnumbered element is first given where free: its own mark's digits, else (a name Lapin numbered) its name's.</summary>
    static int Seed(MarkCategory c, Entity e) => Trailing(e.Str("mark")) is > 0 and var own ? own : c.NumberedNames ? Trailing(e.Name) : 0;

    /// <summary>
    /// Numbers for those of <paramref name="items"/> without one, in each scope (the project, or a storey): the seed where it is
    /// free, else the next past the highest held. Those numbered keep theirs. Element id → number.
    /// </summary>
    public static Dictionary<string, int> Numbers(MarkCategory c, IEnumerable<(Entity E, string Scope)> items)
    {
        var result = new Dictionary<string, int>();
        foreach (var g in items.GroupBy(x => x.Scope))
        {
            var taken = g.Select(x => Number(x.E)).OfType<int>().ToHashSet();
            var lacking = g.Where(x => Number(x.E) == null).Select(x => x.E).ToList();
            var later = new List<Entity>();
            foreach (var e in lacking)
                if (Seed(c, e) is > 0 and var s && taken.Add(s)) result[e.Id] = s; else later.Add(e);
            var next = taken.DefaultIfEmpty(0).Max();
            foreach (var e in later) { result[e.Id] = ++next; taken.Add(next); }
        }
        return result;
    }

    /// <summary>The category's elements in the model (not those a file brought).</summary>
    public static IEnumerable<Entity> Members(IModelView m, MarkCategory c) => c.Classes.SelectMany(m.OfClass).Where(e => !Foreign(e));

    static string ScopeOf(IModelView m, MarkCategory c, MarkRule rule, Entity e) => rule.PerStorey && !c.IsType ? Spatial.StoreyOf(m, e.Id)?.Id ?? "" : "";

    /// <summary>Numbers the category's elements that have none; how many it numbered.</summary>
    public static int NumberMissing(ICommandContext ctx, MarkCategory c, MarkRule rule)
    {
        var m = ctx.Model;
        var numbers = Numbers(c, Members(m, c).Select(e => (e, ScopeOf(m, c, rule, e))).ToList());
        foreach (var (id, n) in numbers) SetNumber(ctx, m.Get(id)!, n);
        return numbers.Count;
    }

    static void SetNumber(IEdit edit, Entity e, int n)
    {
        var attrs = e.CloneAttrs();
        attrs[NumberKey] = n;
        edit.Replace(e.With(attrs: attrs));
    }

    /// <summary>
    /// Renumbers a category from 1 (in each storey when its rule counts by storey), in one undo step: storey by storey, lowest
    /// first (<paramref name="byStorey"/>), then by where each stands (<c>rows</c>: top to bottom, each row left to right;
    /// <c>columns</c>: left to right, each column top to bottom) or as they are numbered now (<c>current</c>). Types by their number,
    /// then their name. Returns how many changed.
    /// </summary>
    public static int Renumber(ICommandContext ctx, MarkCategory c, string order, bool byStorey)
    {
        if (!Orders.Contains(order)) throw new CommandException($"Renumber by {string.Join(", ", Orders)}, not „{order}”.");
        var m = ctx.Model;
        var rule = RuleFor(m, c.Id);
        var members = Members(m, c).Select((e, i) => (E: e, I: i)).ToList();
        var changed = 0;
        foreach (var g in members.GroupBy(x => ScopeOf(m, c, rule, x.E)))
        {
            List<Entity> sorted;
            if (c.IsType || order == "current")
                sorted = g.OrderBy(x => byStorey && !c.IsType ? Spatial.Elevation(m, x.E) : 0).ThenBy(x => Number(x.E) ?? int.MaxValue).ThenBy(x => c.IsType ? x.E.Name : "", StringComparer.CurrentCulture).ThenBy(x => x.I).Select(x => x.E).ToList();
            else
            {
                var placed = g.Select(x => (x.E, x.I, At: Centre(ctx.Query, x.E.Id), Level: byStorey ? Spatial.Elevation(m, x.E) : 0)).ToList();
                sorted = [];
                foreach (var level in placed.GroupBy(x => x.Level).OrderBy(l => l.Key))
                    sorted.AddRange(Bands(level.ToList(), order == "rows"));
            }
            var k = 0;
            foreach (var e in sorted)
            {
                k++;
                if (Number(e) == k) continue;
                SetNumber(ctx, e, k);
                changed++;
            }
        }
        return changed;
    }

    /// <summary>How far apart (m) two elements may stand across and still be in one row or column.</summary>
    public const double BandTolerance = 0.5;

    /// <summary>In rows (top to bottom, each left to right) or columns (left to right, each top to bottom).</summary>
    static IEnumerable<Entity> Bands(List<(Entity E, int I, Vec2 At, double Level)> items, bool rows)
    {
        double Across((Entity E, int I, Vec2 At, double Level) x) => rows ? -x.At.Y : x.At.X;
        double Along((Entity E, int I, Vec2 At, double Level) x) => rows ? x.At.X : -x.At.Y;
        var list = items.OrderBy(Across).ThenBy(Along).ThenBy(x => x.I).ToList();
        var i = 0;
        while (i < list.Count)
        {
            var start = Across(list[i]);
            var j = i;
            while (j < list.Count && Across(list[j]) - start <= BandTolerance) j++;
            foreach (var x in list.Skip(i).Take(j - i).OrderBy(Along).ThenBy(x => x.I)) yield return x.E;
            i = j;
        }
    }

    /// <summary>Where an element stands in plan: its body's middle (the origin when it has none).</summary>
    static Vec2 Centre(IQueryContext q, string id)
    {
        if (q.Geometry(id) is not { } g || Measure.Bounds(g.Parts.Select(p => p.Solid)) is not { } b) return new(0, 0);
        return new((b.Min.X + b.Max.X) / 2, (b.Min.Y + b.Max.Y) / 2);
    }

    // ------------------------------------------------------------ commands

    /// <summary>A rule as a command gives it, checked: its texts' lengths, its braces closed, a known numbering.</summary>
    public static MarkRule Check(JsonNode? node)
    {
        var rule = MarkRule.From(node);
        if (rule.Prefix.Length > 16 || rule.Suffix.Length > 16) throw new CommandException("A mark's prefix or suffix is 16 characters at most.");
        if (rule.Pattern.Length > 120) throw new CommandException("A mark's pattern is 120 characters at most.");
        if (rule.Prefix.IndexOfAny(['{', '}']) >= 0 || rule.Suffix.IndexOfAny(['{', '}']) >= 0) throw new CommandException("A prefix or a suffix is plain text: the pattern holds the {fields}.");
        var open = 0;
        foreach (var ch in rule.Pattern)
        {
            open += ch == '{' ? 1 : ch == '}' ? -1 : 0;
            if (open is < 0 or > 1) throw new CommandException("A mark's pattern closes each {field} it opens.");
        }
        if (open != 0) throw new CommandException("A mark's pattern closes each {field} it opens.");
        if (node is JsonObject o && o["scope"] is JsonValue sv && sv.TryGetValue<string>(out var scope) && scope is not ("project" or "storey"))
            throw new CommandException("A mark is numbered in the project or on each storey.");
        return rule;
    }

    static void Write(ICommandContext ctx, Dictionary<string, MarkRule> rules)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        if (rules.Count == 0) attrs.Remove(Key);
        else attrs[Key] = new JsonObject(rules.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value.ToJson())));
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>What the rules dialog shows of a category: its rule, how many it has, a few marks as written now.</summary>
    static JsonObject Describe(IModelView m, MarkCategory c, MarkRule? rule = null)
    {
        rule ??= RuleFor(m, c.Id);
        var members = Members(m, c).ToList();
        var samples = new JsonArray();
        // A rule not yet set numbers the first ones as it would.
        var numbers = rule.UsesNumber ? Numbers(c, members.Select(e => (e, ScopeOf(m, c, rule, e)))) : [];
        foreach (var e in members.Take(4))
        {
            var mark = e.Str("mark") ?? (rule.IsDefault ? TagText.Coded(m, e) : Apply(m, e, rule, Number(e) ?? numbers.GetValueOrDefault(e.Id)));
            samples.Add((JsonNode)new JsonObject { ["id"] = e.Id, ["name"] = e.Name, ["mark"] = mark, ["own"] = e.Str("mark") != null });
        }
        var o = rule.ToJson();
        o["id"] = c.Id; o["label"] = c.Label; o["isType"] = c.IsType; o["count"] = members.Count; o["isDefault"] = rule.IsDefault; o["samples"] = samples;
        return o;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Service<ICommandFollowUp>(new FollowUp());
        r.Service<IProjectUpgrade>(new NumbersUpgrade());

        // Each category's rule, how many elements it has and a few of their marks.
        r.Query("markRules.list", (q, _) => new JsonObject
        {
            ["defaultPattern"] = DefaultPattern,
            ["categories"] = new JsonArray([.. Categories.Select(c => (JsonNode)Describe(q.Model, c))]),
        });

        // The marks a rule not yet applied would give ({category, rule}), for the dialog's preview.
        r.Query("markRules.preview", (q, a) => Describe(q.Model, Category((string?)a["category"]), Check(a["rule"])));

        // Rules set, one undo step: {rules: {category: {prefix, suffix, pattern, scope} | null (back to the built-in one)}}. A rule
        // that reads {n} numbers its elements that have no number.
        r.Command("markRules.set", "Mark rules", (ctx, a) =>
        {
            if (a["rules"] is not JsonObject given) throw new CommandException("Which rules?");
            var rules = new Dictionary<string, MarkRule>(All(ctx.Model));
            foreach (var (k, v) in given)
            {
                var c = Category(k);
                var rule = v == null ? new MarkRule() : Check(v);
                if (rule.IsDefault) rules.Remove(c.Id); else rules[c.Id] = rule;
            }
            Write(ctx, rules);
            var numbered = 0;
            foreach (var (k, rule) in rules)
                if (given.ContainsKey(k) && rule.UsesNumber) numbered += NumberMissing(ctx, Category(k), rule);
            return new JsonObject { ["numbered"] = numbered };
        });

        // A category numbered again from 1 ({category, order: rows | columns | current, byStorey}), one undo step.
        r.Command("markRules.renumber", "Renumber marks", (ctx, a) =>
        {
            var c = Category((string?)a["category"]);
            var byStorey = a["byStorey"] is not JsonValue bv || !bv.TryGetValue<bool>(out var b) || b;
            return new JsonObject { ["changed"] = Renumber(ctx, c, (string?)a["order"] ?? "rows", byStorey) };
        });
    }

    /// <summary>
    /// After every command, in its step: an element of a category whose rule reads {n} gets the next number when it has none (a
    /// copy whose number another holds too gets a new one; one rebuilt keeps the one it had).
    /// </summary>
    sealed class FollowUp : ICommandFollowUp
    {
        public JsonNode? After(ICommandContext ctx, IEdit edit, string command, JsonObject args, JsonNode? result)
        {
            var m = ctx.Model;
            var rules = All(m);
            if (rules.Count == 0 || !rules.Values.Any(x => x.UsesNumber)) return result;
            var changes = edit.EntityChanges;
            if (changes.Count == 0) return result;
            // Each entity's state before the command: its first change's.
            var before = new Dictionary<string, Entity?>();
            foreach (var ch in changes)
                if (!before.ContainsKey(ch.Id)) before[ch.Id] = ch.Before;
            Dictionary<(string, string), Dictionary<int, int>>? held = null;
            Dictionary<int, int> Held(MarkCategory c, MarkRule rule, string scope)
            {
                held ??= [];
                if (!held.TryGetValue((c.Id, scope), out var d))
                {
                    d = [];
                    foreach (var x in Members(m, c))
                        if (Number(x) is { } n && ScopeOf(m, c, rule, x) == scope) d[n] = d.GetValueOrDefault(n) + 1;
                    held[(c.Id, scope)] = d;
                }
                return d;
            }
            foreach (var (id, was) in before)
            {
                if (m.Get(id) is not { } e || CategoryOf(e.Class) is not { } c || Foreign(e) || !rules.TryGetValue(c.Id, out var rule) || !rule.UsesNumber) continue;
                var scope = ScopeOf(m, c, rule, e);
                var d = Held(c, rule, scope);
                var n = Number(e);
                if (n == null && was != null && Number(was) is { } kept && d.GetValueOrDefault(kept) == 0) { d[kept] = 1; SetNumber(ctx, e, kept); continue; }
                if (n is { } has && (was != null || d.GetValueOrDefault(has) <= 1)) continue;
                if (n is { } dup) d[dup]--;
                var next = d.Where(kv => kv.Value > 0).Select(kv => kv.Key).DefaultIfEmpty(0).Max() + 1;
                d[next] = 1;
                SetNumber(ctx, e, next);
            }
            return result;
        }
    }

    /// <summary>
    /// A project an older build saved (or a file brought back with Lapin's intent): where a rule reads {n}, its elements without a
    /// number get one as <c>markRules.set</c> gives them (D3 keeps 3). A project with no rules of its own (every project before
    /// decision 737) is given back as it came: its marks print as before, its own marks stay its own.
    /// </summary>
    public sealed class NumbersUpgrade : IProjectUpgrade
    {
        public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
        {
            var project = entities.FirstOrDefault(e => e.Class == IfcClass.Project);
            if (project?.Attrs[Key] is not JsonObject raw) return (entities, relations);
            var rules = raw.Where(kv => Categories.Any(c => c.Id == kv.Key)).Select(kv => (C: Category(kv.Key), R: MarkRule.From(kv.Value))).Where(x => !x.R.IsDefault && x.R.UsesNumber).ToList();
            if (rules.Count == 0) return (entities, relations);
            Dictionary<string, string>? storeyOf = null;
            string StoreyOf(string id)
            {
                storeyOf ??= relations.Where(r => r.Class == IfcClass.RelContainedInSpatialStructure).SelectMany(r => r.Related.Select(x => (x, r.Relating))).GroupBy(p => p.x).ToDictionary(g => g.Key, g => g.First().Relating);
                return storeyOf.GetValueOrDefault(id) ?? "";
            }
            var numbers = new Dictionary<string, int>();
            foreach (var (c, rule) in rules)
            {
                var members = entities.Where(e => c.Classes.Contains(e.Class) && !Foreign(e)).ToList();
                if (members.All(e => Number(e) != null)) continue;
                foreach (var (id, n) in Numbers(c, members.Select(e => (e, rule.PerStorey && !c.IsType ? StoreyOf(e.Id) : "")))) numbers[id] = n;
            }
            if (numbers.Count == 0) return (entities, relations);
            var list = entities.Select(e =>
            {
                if (!numbers.TryGetValue(e.Id, out var n)) return e;
                var attrs = e.CloneAttrs();
                attrs[NumberKey] = n;
                return e.With(attrs: attrs);
            }).ToList();
            return (list, relations);
        }
    }
}
