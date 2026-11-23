using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// The MEP legend (a legend of kind <c>mep</c>, DrawingKeys): the line types and symbols the views it lists actually draw — each
/// service's line in its system's colour and line type (a pair's supply and return apart), then each role's symbol as the
/// project's set draws it (a rise and a drop apart), in its system's colour — each named in the legend's language. Worked out
/// on every draw, so it follows the model.
/// </summary>
public sealed class MepLegend : ILegendKind
{
    public const string KindId = "mep";
    public string Kind => KindId;
    public string Name => "MEP legend";
    public string Title(string? lang) => Words.Of("MEP LEGEND", lang);

    public List<DrawingKeys.Entry> Entries(IQueryContext q, IReadOnlyList<Entity> views, string? lang)
    {
        var m = q.Model;
        var book = DraftingResources.Book(m);
        var drawn = new HashSet<string>(StringComparer.Ordinal);
        var scale = 100.0;
        foreach (var view in views)
        {
            scale = view.NumOrNull("scale") ?? scale;
            if (Views.Generate(q, view).Drawing is { } d) foreach (var l in d.Lines) drawn.Add(l.Element);
        }
        // Lines: by service, its supply and (where a run is paired) its return; symbols: by key and colour.
        var lines = new SortedDictionary<(int Service, int Return, string Color), DrawingKeys.Entry>();
        var symbols = new SortedDictionary<(int Key, string Color), DrawingKeys.Entry>();
        var keys = MepSymbols.Keys.ToList();
        foreach (var id in drawn)
        {
            if (m.Get(id) is not { } e) continue;
            var system = m.Get(e.Str("systemId") ?? "") is { Class: Systems.Class } s ? s : null;
            if (Runs.IsRun(e) && system != null)
            {
                var service = Systems.Service(system);
                var style = Systems.StyleOf(system);
                var label = Words.Of(Services.Label(service), lang);
                var pair = Runs.SettingOf(m, e).Pair;
                foreach (var (look, ret) in pair ? new[] { (style.Line, 0), (style.Return, 1) } : [(style.Line, 0)])
                {
                    var key = (Array.IndexOf(Services.All, service), ret, look.Color);
                    if (lines.ContainsKey(key)) continue;
                    var name = pair ? $"{label} — {Words.Of(ret == 0 ? "Supply line" : "Return line", lang)}" : label;
                    lines[key] = new DrawingKeys.Entry(name, Stroke: look.Color, Width: (look.Pen ?? 3) >= 5 ? 0.5 : 0.35, Dash: look.Pattern is null or "solid" ? null : Standards.DashOf(book, look.Pattern));
                }
            }
            else if (Nodes.IsNode(e) && Nodes.RoleOf(e) is { } role)
            {
                var down = role.IsRise && Nodes.RiseTop(m, e) <= Nodes.StoreyZ(m, e) + Nodes.Elevation(e, role);
                var key = MepSymbols.KeyOf(role, down);
                var color = (system != null ? Systems.StyleOf(system) : SystemStyle.For(role.Ports.FirstOrDefault()?.Service ?? Services.Heating)).Line.Color;
                var k = (keys.IndexOf(key), color);
                if (symbols.ContainsKey(k)) continue;
                var glyph = MepGlyphs.Of(m, key, null, scale).Select(g => new LegendGlyph(g.Points, g.Closed, g.Heavy ? 0.5 : 0.25, g.Fill, g.Text, g.Height)).ToList();
                symbols[k] = new DrawingKeys.Entry(Words.Of(MepSymbols.Label(key), lang), Stroke: color, Glyph: glyph);
            }
        }
        return [.. lines.Values, .. symbols.Values];
    }

    /// <summary>The legend's words in its language, from the interface's translations (MepWords.json, scripts/mep-words.mjs); a word without one stays English.</summary>
    public static class Words
    {
        static readonly Dictionary<string, Dictionary<string, string>> Table = Load();

        static Dictionary<string, Dictionary<string, string>> Load()
        {
            using var stream = typeof(MepLegend).Assembly.GetManifestResourceStream("Lapin.Mep.MepWords.json");
            var o = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            if (stream == null || JsonNode.Parse(stream)?["words"] is not JsonObject words) return o;
            foreach (var (w, by) in words)
                if (by is JsonObject langs) o[w] = langs.Where(x => x.Value is JsonValue).ToDictionary(x => x.Key, x => (string)x.Value!);
            return o;
        }

        public static string Of(string word, string? lang) =>
            lang != null && lang != "en" && Table.TryGetValue(word, out var by) && by.TryGetValue(lang, out var w) ? w : word;

        public static bool Has(string word, string lang) => Table.TryGetValue(word, out var by) && by.ContainsKey(lang);
    }
}
