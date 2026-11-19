using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// How a bar's mark is written (decision 729, forum topic 174): its number kept on the bar (<c>mark</c>, never renumbered by
/// itself, so adding a bar moves no other), a prefix by the bar's shape group from the project's settings (straight R, U-bar U,
/// stirrup S…; a bar may name its own, <c>markPrefix</c>) and a pattern of live text (<c>{prefix}{n}</c>, the coded default; any
/// <c>{…}</c> token or <c>{= …}</c> formula of <see cref="LiveText"/>: <c>{prefix}{= n | 00}</c>, <c>{partition}-{n}</c>). Marks
/// are numbered apart in each partition and each prefix: R1, R2… beside U1, U2…. With no prefix set (the default) every bar
/// shares the one numbering, as before.
/// </summary>
public static class RebarMarks
{
    public const string PrefixKey = "markPrefix";
    public const string DefaultPattern = "{prefix}{n}";

    /// <summary>The groups a prefix is set for, by the shapes of the bars in them.</summary>
    public static readonly string[] Groups = ["straight", "L", "U", "stirrup", "tie", "polyline", "other"];

    /// <summary>The tokens a mark's pattern reads.</summary>
    public static readonly string[] Tokens = ["prefix", "n", "partition", "d", "shape"];

    /// <summary>The group of a shape: a bar in section is a straight bar seen end on; a shape of the user's is „other”.</summary>
    public static string GroupOf(string? shape) => shape switch
    {
        "straight" or "dot" => "straight",
        "L" or "U" or "stirrup" or "tie" or "polyline" => shape,
        _ => "other",
    };

    /// <summary>The prefix the settings give a group ("" none).</summary>
    public static string PrefixOf(RebarSettings st, string group) => st.MarkPrefixes.TryGetValue(group, out var p) ? p : "";

    /// <summary>A bar's, a distribution's or a set's prefix: its own when it names one, else its shape group's.</summary>
    public static string PrefixOf(RebarSettings st, Entity e) =>
        e.Str(PrefixKey) is { } own ? own.Trim() : PrefixOf(st, GroupOf(ShapeOf(e)));

    static string? ShapeOf(Entity e) => e.Str("shape") ?? (e.Class == RebarSets.Class ? "straight" : null);

    /// <summary>The text of a mark: the pattern with its tokens filled ("" for no mark).</summary>
    public static string Text(RebarSettings st, string prefix, int n, string partition = "", double diameter = 0, string shape = "")
    {
        if (n <= 0) return "";
        var pattern = string.IsNullOrWhiteSpace(st.MarkPattern) ? DefaultPattern : st.MarkPattern;
        // The coded default, quickly: the prefix and the number.
        if (pattern == DefaultPattern) return prefix + n.ToString(CultureInfo.InvariantCulture);
        (bool, string?) Resolve(string key) => key switch
        {
            "prefix" => (true, prefix),
            "n" => (true, n.ToString(CultureInfo.InvariantCulture)),
            "partition" => (true, partition),
            "d" => (true, Math.Round(diameter * 1000).ToString(CultureInfo.InvariantCulture)),
            "shape" => (true, shape),
            _ => (false, null),
        };
        // Its formulas first ({= n | 00}), then its plain tokens ({prefix}, {n}…; live text's are {source.key}, these have no source).
        var text = pattern.Contains("{=", StringComparison.Ordinal)
            ? LiveText.Formulas(pattern, name => Resolve(name) is (true, { } v) ? XValue.Read(v) : null, null) : pattern;
        foreach (var t in Tokens) text = text.Replace("{" + t + "}", Resolve(t).Item2, StringComparison.Ordinal);
        text = text.Trim();
        return text.Length > 0 ? text : n.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>A drawn bar's, a distribution's or a set's mark as written.</summary>
    public static string TextOf(RebarSettings st, Entity e) =>
        Text(st, PrefixOf(st, e), Rebars.Mark(e), Rebars.Partition(e), e.NumOrNull("diameter") ?? 0, GroupOf(ShapeOf(e)));

    /// <summary>The mark the stirrups of a type are written with (their group: stirrup).</summary>
    public static string StirrupText(RebarSettings st, int n, string partition, double d) => Text(st, PrefixOf(st, "stirrup"), n, partition, d, "stirrup");

    /// <summary>The mark a column type's bars are written with (their group: straight).</summary>
    public static string ColumnBarsText(RebarSettings st, int n, string partition, double d) => Text(st, PrefixOf(st, "straight"), n, partition, d, "straight");

    /// <summary>The settings' mark scheme read from a command: {pattern?, prefixes?: {group: text | null}}; an unknown group or a pattern without {n} is refused.</summary>
    public static (string Pattern, Dictionary<string, string> Prefixes) Patch(RebarSettings now, JsonNode? node)
    {
        var pattern = now.MarkPattern;
        var prefixes = new Dictionary<string, string>(now.MarkPrefixes);
        if (node is not JsonObject o) return (pattern, prefixes);
        if (o.ContainsKey("pattern"))
        {
            var p = ((string?)o["pattern"])?.Trim();
            if (string.IsNullOrEmpty(p)) p = DefaultPattern;
            if (!p.Contains("{n}", StringComparison.Ordinal) && !p.Contains("{=", StringComparison.Ordinal))
                throw new CommandException("A mark's pattern holds its number: {n}.");
            if (p.Length > 40) throw new CommandException("A mark's pattern is 40 characters at most.");
            pattern = p;
        }
        if (o["prefixes"] is JsonObject po)
            foreach (var (k, v) in po)
            {
                if (!Groups.Contains(k)) throw new CommandException($"Prefixes are set for {string.Join(", ", Groups)}, not „{k}”.");
                var text = ((string?)v)?.Trim() ?? "";
                if (text.Length > 8) throw new CommandException("A mark's prefix is 8 characters at most.");
                if (text.Length == 0) prefixes.Remove(k); else prefixes[k] = text;
            }
        return (pattern, prefixes);
    }

    /// <summary>
    /// Every mark held in a partition with a prefix: the bars, distributions and sets drawn (by their own prefix), the types'
    /// stirrups (prefix of stirrups) and column bars (prefix of straight bars).
    /// </summary>
    public static IEnumerable<int> Held(IModelView m, RebarSettings st, string partition, string prefix)
    {
        foreach (var e in m.OfClass(Rebars.Class).Concat(m.OfClass(Rebars.RunClass)).Concat(m.OfClass(RebarSets.Class)))
            if (Rebars.Partition(e) == partition && PrefixOf(st, e) == prefix) yield return Rebars.Mark(e);
        if (PrefixOf(st, "stirrup") == prefix) yield return TypeStirrups.HighestMark(m, partition);
        if (PrefixOf(st, "straight") == prefix) yield return ColumnBars.HighestMark(m, partition);
    }

    /// <summary>
    /// Renumbers the marks of each partition and prefix from 1 (decision 729), in the order they stand: bars sharing a mark keep
    /// sharing one; the types' stirrups and column bars keep theirs (they are numbered by their own dialog) and the numbers
    /// they hold are skipped. Returns how many bars, distributions and sets changed. Only <paramref name="partition"/> when given.
    /// </summary>
    public static int Renumber(ICommandContext ctx, string? partition)
    {
        var m = ctx.Model;
        var st = RebarSettings.Of(m);
        var changed = 0;
        // A bar's copy in another view shares its original's mark, and keeps sharing it.
        var all = m.OfClass(Rebars.Class).Concat(m.OfClass(Rebars.RunClass)).Concat(m.OfClass(RebarSets.Class)).ToList();
        foreach (var g in all.GroupBy(e => (Part: Rebars.Partition(e), Prefix: PrefixOf(st, e))))
        {
            if (partition != null && g.Key.Part != partition.Trim()) continue;
            // Numbers the types hold in this partition and prefix: kept, so a drawn bar never takes one.
            var taken = new HashSet<int>();
            if (PrefixOf(st, "stirrup") == g.Key.Prefix)
                foreach (var t in m.OfClass(IfcClass.BeamType).Concat(m.OfClass(IfcClass.ColumnType)))
                    if ((t.Str(Rebars.StirrupsPartitionKey)?.Trim() ?? "") == g.Key.Part && StirrupSpec.Of(t) is { Enabled: true } s)
                        foreach (var k in (s.InnerMarks ?? []).Prepend(s.Mark)) taken.Add(k);
            if (PrefixOf(st, "straight") == g.Key.Prefix)
                foreach (var t in m.OfClass(IfcClass.ColumnType))
                    if ((t.Str(Rebars.ColumnBarsPartitionKey)?.Trim() ?? "") == g.Key.Part && ColumnBarsSpec.Of(t) is { Enabled: true } c) taken.Add(c.Mark);
            var map = new Dictionary<int, int>();
            var next = 1;
            foreach (var old in g.Select(Rebars.Mark).Distinct().Order())
            {
                while (taken.Contains(next)) next++;
                map[old] = next++;
            }
            foreach (var e in g)
            {
                var to = map[Rebars.Mark(e)];
                if (to == Rebars.Mark(e)) continue;
                var attrs = e.CloneAttrs();
                attrs["mark"] = to;
                ctx.Replace(e.With(attrs: attrs));
                changed++;
            }
        }
        return changed;
    }
}
