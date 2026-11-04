using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Cost;

/// <summary>
/// Map articles (docs/plan-4d5d.md, stage 6, step 4.2): the project's types that elements use and the materials of their
/// layers and parts, each with what prices it now (its articles, a rule) and the article suggested for it — a code
/// written in its name, else the price list's row sharing the most words with it („zidărie”, „BCA”, „tencuială”,
/// „C25/30”) — and the measure proposed (by the row's unit; a layered type's core).
/// </summary>
public static partial class ArticleMapping
{
    static readonly HashSet<string> Stop = ["de", "din", "la", "cu", "si", "in", "pe", "pt", "pentru", "a", "al", "ale", "the", "of", "and", "for", "with", "to", "mm", "cm", "type", "tip"];

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:[/.,][\p{N}]+)*")]
    private static partial Regex WordRx();

    /// <summary>The words of a text as they are compared: folded, without the little ones; numbers kept (25/30, 250).</summary>
    public static HashSet<string> Words(string? s) =>
        WordRx().Matches(PriceLists.Fold(s ?? "")).Select(m => m.Value).Where(w => !Stop.Contains(w) && (w.Length >= 2 || char.IsDigit(w[0]))).ToHashSet();

    /// <summary>The best article for a text among the lists' articles: one whose code the text holds, else the most words shared (none: null).</summary>
    public static PriceRow? Suggest(IReadOnlyList<PriceLists.List> lists, string text)
    {
        var folded = " " + PriceLists.Fold(text) + " ";
        var words = Words(text);
        PriceRow? best = null;
        double score = 0;
        foreach (var l in lists)
            foreach (var row in l.Articles.Values)
            {
                if (row.Code.Length >= 3 && folded.Contains(" " + PriceLists.Fold(row.Code) + " ", StringComparison.Ordinal)) return row;
                var rw = Words(row.Name);
                double sc = 0;
                foreach (var w in words) if (rw.Contains(w)) sc += char.IsDigit(w[0]) ? 1.5 : w.Length >= 4 ? 2 : 1;
                // A word matched by its start (bca / bcaul, zidarie / zidaria): half.
                foreach (var w in words.Where(w => w.Length >= 5 && !rw.Contains(w))) if (rw.Any(r => r.Length >= 5 && (r.StartsWith(w[..5], StringComparison.Ordinal)))) sc += 1;
                if (sc > score) { score = sc; best = row; }
            }
        return score > 0 ? best : null;
    }

    static JsonObject Article(PriceRow r, string? part) =>
        new() { ["code"] = r.Code, ["name"] = r.Name, ["unit"] = r.Unit, ["part"] = part, ["basis"] = Units.BasisOf(r.Unit)?.Basis ?? "count" };

    public static JsonObject Of(IQueryContext q)
    {
        var m = q.Model;
        var lists = PriceLists.All(m);
        var rules = ArticleRule.All(m);
        var types = new Dictionary<string, (Entity Type, int Count, bool Layered)>();
        var classes = new Dictionary<string, int>();
        var materials = new Dictionary<string, int>();
        foreach (var (e, sets) in Costing.Measured(q))
        {
            var type = Costing.TypeOf(m, e);
            var layered = sets.Any(x => Parts.LayerIndex(x.Part) != null);
            if (type != null) types[type.Id] = (type, types.GetValueOrDefault(type.Id).Count + 1, types.GetValueOrDefault(type.Id).Layered || layered);
            else classes[e.Class] = classes.GetValueOrDefault(e.Class) + 1;
            var mats = sets.Where(x => x.Part != null && x.Material != null).Select(x => x.Material!).ToHashSet();
            if (mats.Count == 0 && Costing.OwnMaterial(m, e.Id) is { } own) mats.Add(own);
            foreach (var mat in mats) materials[mat] = materials.GetValueOrDefault(mat) + 1;
        }
        var typeRows = new JsonArray();
        foreach (var (type, count, layered) in types.Values.OrderBy(t => t.Type.Class, StringComparer.Ordinal).ThenBy(t => t.Type.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var arts = ArticleRef.Of(type);
            var core = layered && LayerSets.LayersOf(m, type.Id) is { Count: > 0 } ls ? ls.FirstOrDefault(l => l.Core) ?? ls[0] : null;
            var text = type.Name + (core != null ? " " + m.Get(core.Material)?.Name : "");
            var o = new JsonObject
            {
                ["id"] = type.Id, ["name"] = type.Name, ["class"] = type.Class, ["label"] = q.Describe(type.Class)?.Label ?? type.Class,
                ["count"] = count, ["layered"] = layered,
                ["articles"] = new JsonArray(arts.Select(a => (JsonNode)a.ToJson()).ToArray()),
            };
            if (Suggest(lists, text) is { } s) o["suggested"] = Article(s, layered ? Parts.Core : null);
            typeRows.Add((JsonNode)o);
        }
        var matRows = new JsonArray();
        foreach (var (id, count) in materials.OrderBy(x => m.Get(x.Key)?.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            if (m.Get(id) is not { Class: IfcClass.Material } mat) continue;
            var rule = Costing.RuleFor(m, rules, id);
            var o = new JsonObject
            {
                ["id"] = id, ["name"] = mat.Name, ["category"] = mat.Str("category"), ["count"] = count,
                ["rule"] = rule == null ? null : new JsonObject { ["id"] = rule.Id, ["code"] = rule.Article.Code, ["basis"] = rule.Article.Basis, ["own"] = rule.Material == id, ["category"] = rule.Category },
            };
            if (Suggest(lists, mat.Name) is { } s) o["suggested"] = Article(s, null);
            matRows.Add((JsonNode)o);
        }
        return new JsonObject
        {
            ["types"] = typeRows, ["materials"] = matRows,
            ["untyped"] = new JsonArray(classes.Select(c => (JsonNode)new JsonObject { ["class"] = c.Key, ["label"] = q.Describe(c.Key)?.Label ?? c.Key, ["count"] = c.Value }).ToArray()),
            ["lists"] = lists.Count,
        };
    }
}
