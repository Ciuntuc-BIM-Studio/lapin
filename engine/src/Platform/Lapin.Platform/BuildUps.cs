using System.Globalization;
using System.Text;
using Lapin.Drawing;
using Lapin.Drawing.Documents;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// One layer of a build-up as a build-up tag reads it (docs/plan-buildup-tags.md): its place in the layer set
/// (<see cref="Index"/>, what its cut is keyed by: <c>layer:&lt;n&gt;</c>), its material (an IfcMaterial id), thickness
/// (metres), whether it is the core, its name, the note the type gives it, and the materials of its pieces up its height
/// (decision 264), each a line of its own after the layer's. <see cref="Keys"/>: the keys of the element's parts the layer
/// is drawn as, when they are not <c>layer:&lt;n&gt;</c> (a roof's <c>deck</c>, <c>covering</c>; a key ending in ":" takes every
/// part whose key starts so, <c>timber:</c> its rafters), so a task can build the layer apart and the 4D simulation show it.
/// </summary>
public sealed record BuildupLayer(int Index, string? Material, double Thickness, bool Core, string Name, string? Note = null, IReadOnlyList<string>? PartMaterials = null,
    IReadOnlyList<string>? Keys = null)
{
    /// <summary>Whether a part's key is one of <paramref name="keys"/> (one ending in ":" a prefix).</summary>
    public static bool KeyMatches(IReadOnlyList<string> keys, string key) =>
        keys.Any(k => k == key || k.EndsWith(':') && key.StartsWith(k, StringComparison.Ordinal));
}

/// <summary>
/// An element's build-up: its layers in their natural order, exterior to interior across a wall (its first layer's side
/// is its exterior, the right as drawn unless it is flipped), top to bottom through a floor, a roof or a ceiling
/// (<see cref="Stack"/>), as the element stands (a floor laid upwards is read from its top all the same).
/// </summary>
public sealed record BuildUp(IReadOnlyList<BuildupLayer> Layers, bool Stack);

/// <summary>A module's build-ups other than layer sets (a room's finish, its layers from the floor's top down).</summary>
public interface IElementBuildUps
{
    BuildUp? Of(IModelView m, Entity e);
}

public static class BuildUps
{
    static BuildupLayer Layer(MaterialLayer l, int i, bool core, IModelView m) =>
        new(i, l.Material, l.Thickness, core, l.Name ?? m.Get(l.Material)?.Name ?? $"Layer {i + 1}", l.Note,
            l.Parts is { Count: > 0 } ps ? ps.Select(p => p.Material).Where(x => x != l.Material).Distinct().ToList() : null);

    static bool StackClass(string typeClass) => typeClass is IfcClass.SlabType or "IfcRoofType" or "IfcCoveringType";

    /// <summary>
    /// The element's build-up as it stands: its own layer set usage (a wall's layers in their order, the first on its
    /// exterior; a floor's from its top, whichever way the usage lays them), else its type's layers as listed, else what a
    /// module says of it; null when it has none.
    /// </summary>
    public static BuildUp? Of(IModelView m, Entity e, IServices? services = null)
    {
        if (LayerSets.UsageOf(m, e.Id) is { } u && Layers.ReadUsage(u) is { } usage && m.Get(usage.ForLayerSet) is { } set && Layers.Read(set) is { Count: > 0 } layers)
        {
            var core = Layers.CoreIndex(layers);
            var list = layers.Select((l, i) => Layer(l, i, i == core, m)).ToList();
            var stack = u.Str("layerSetDirection") == "AXIS3";
            if (stack)
            {
                // Top first: the band highest up, whichever way the usage runs.
                var bands = Layers.Bands(layers, usage);
                list = list.OrderByDescending(x => bands[x.Index].To).ThenByDescending(x => bands[x.Index].From).ToList();
            }
            return new BuildUp(list, stack);
        }
        var type = e.Class.EndsWith("Type", StringComparison.Ordinal) ? e : m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        if (type != null && OfType(m, type) is { } own) return own;
        foreach (var s in services?.All<IElementBuildUps>() ?? [])
            if (s.Of(m, e) is { Layers.Count: > 0 } b) return b;
        return null;
    }

    /// <summary>A type's layers as it lists them (a wall type's exterior first, a floor type's top first); null without any.</summary>
    public static BuildUp? OfType(IModelView m, Entity type)
    {
        if (LayerSets.Of(m, type.Id) is not { } set || Layers.Read(set) is not { Count: > 0 } layers) return null;
        var core = Layers.CoreIndex(layers);
        return new BuildUp(layers.Select((l, i) => Layer(l, i, i == core, m)).ToList(), StackClass(type.Class));
    }
}

/// <summary>
/// A build-up tag's template (docs/plan-buildup-tags.md): TagText's {field}s, and optional parts in square brackets,
/// dropped whole when a field in them is empty ("[, λ = {λ} W/mK]" only where the material has a λ); brackets nest, an
/// inner part dropping alone. "|" starts a new line; a backslash writes the next character as it is (\[, \], \{, \}, \|, \\).
/// </summary>
public static class BuildupTemplate
{
    /// <summary>The template filled in by <paramref name="value"/> (null or empty: the field is empty); lines split by "\n".</summary>
    public static string Fill(string template, Func<string, string?> value)
    {
        var i = 0;
        return Segment(template, ref i, value, false, out _);
    }

    static string Segment(string t, ref int i, Func<string, string?> value, bool inner, out bool empty)
    {
        var b = new StringBuilder();
        empty = false;
        while (i < t.Length)
        {
            var ch = t[i];
            if (ch == '\\' && i + 1 < t.Length) { b.Append(t[i + 1]); i += 2; continue; }
            if (ch == '{')
            {
                var close = t.IndexOf('}', i + 1);
                if (close > i + 1 && t.IndexOf('{', i + 1, close - i - 1) < 0)
                {
                    var v = value(t[(i + 1)..close].Trim());
                    if (string.IsNullOrEmpty(v)) empty = true; else b.Append(v);
                    i = close + 1;
                    continue;
                }
            }
            else if (ch == '[')
            {
                i++;
                var part = Segment(t, ref i, value, true, out var dropped);
                if (!dropped) b.Append(part);
                continue;
            }
            else if (ch == ']' && inner) { i++; return b.ToString(); }
            else if (ch == '|') { b.Append('\n'); i++; continue; }
            b.Append(ch);
            i++;
        }
        return b.ToString();
    }

    /// <summary>The template's lines, each trimmed, empty ones dropped.</summary>
    public static IReadOnlyList<string> Lines(string template, Func<string, string?> value) =>
        Fill(template, value).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
}

/// <summary>One layer's text in a build-up tag: the layer it writes (its index in the set), its lines.</summary>
public sealed record BuildupEntry(int Layer, IReadOnlyList<string> Lines);

/// <summary>What a build-up tag writes: its header's lines, an entry per layer (and per material up a layer's height), its footer's lines.</summary>
public sealed record BuildupContent(IReadOnlyList<string> Header, IReadOnlyList<BuildupEntry> Entries, IReadOnlyList<string> Footer, bool Stack);

/// <summary>A text of a build-up tag laid out: paper mm from the block's top left (x right, y down to its baseline), its role (header, bullet, line, footer), its entry.</summary>
public sealed record LaidText(double X, double Y, string Text, string Role, int Entry = -1);

/// <summary>A build-up tag laid out on paper (mm): its texts, its size, and each entry's first line (the middle of its letters, from the top) where its leader leaves.</summary>
public sealed record BuildupLayout(IReadOnlyList<LaidText> Texts, double Width, double Height, IReadOnlyList<double> EntryMid);

public static class BuildupText
{
    /// <summary>The layer fields a build-up tag's line reads, beside the material's parameters and the element's fields.</summary>
    public static readonly string[] LayerFields = ["index", "material", "description", "thickness", "note", "function", "category", "layer"];

    /// <summary>What a layer is for: the core, else its material's category (insulation, membrane…), else a finish by its name; null when nothing says.</summary>
    public static string? Function(IModelView m, BuildupLayer l, string? material)
    {
        if (l.Core && material == l.Material) return "core";
        if (material != null && m.Get(material) is { } mat)
        {
            if (mat.Str("category") is { Length: > 0 } c) return c.Trim().ToLowerInvariant();
            if (Materials.IsFinish(mat)) return "finish";
        }
        return null;
    }

    /// <summary>
    /// A layer's line filled in: its fields ({index}, {material}, {description}: the material's, else its name,
    /// {thickness} in the tag's unit, {note}, {function}), the element's ({mark}, {name}, {type}, {class}), any
    /// parameter of the material by its name or symbol ({λ}, {ThermalConductivity}, {Pset_MaterialThermal.ThermalConductivity}),
    /// else the element's own parameters as any tag reads them.
    /// </summary>
    public static Func<string, string?> Fields(IModelView m, Entity e, BuildupLayer l, string? material, int number, string unit, string projectUnits)
    {
        var mat = material != null ? m.Get(material) : null;
        var mf = mat != null ? MaterialFields.Of(mat) : null;
        return field =>
        {
            switch (field.Trim().ToLowerInvariant())
            {
                case "index": return number.ToString(CultureInfo.InvariantCulture);
                case "material": return mat?.Name ?? l.Name;
                case "description": return mf?.Description ?? mat?.Name ?? l.Name;
                case "thickness": return TagText.Length(l.Thickness, unit, projectUnits);
                case "note": return l.Note;
                case "function": return Function(m, l, material);
                case "category": return mf?.Category;
                case "layer": return l.Name;
                case "mark": case "name": case "type": case "class": return TagText.Value(m, e, field, null, projectUnits);
            }
            if (mf?.Param(field) is { } p && TagText.ParamText(p.Stored, projectUnits) is { Length: > 0 } pv) return pv;
            return TagText.Value(m, e, field, null, projectUnits) is { Length: > 0 } v ? v : null;
        };
    }

    /// <summary>
    /// A build-up tag's text for <paramref name="e"/> (or a type, for a preview): the header and footer read from the
    /// element, a line per layer in the tag type's order, a layer with pieces up its height a line per material, layers of
    /// no thickness left out when the type says so. Null when the element has no build-up.
    /// </summary>
    public static BuildupContent? Content(IModelView m, Entity e, TagType t, string projectUnits, IServices? services = null, BuildUp? buildUp = null)
    {
        if ((buildUp ?? BuildUps.Of(m, e, services)) is not { } b) return null;
        var unit = t.Unit;
        string? Element(string f) => TagText.Value(m, e, f, null, projectUnits) is { Length: > 0 } v ? v : null;
        var layers = t.Reversed ? b.Layers.Reverse().ToList() : b.Layers.ToList();
        var entries = new List<BuildupEntry>();
        foreach (var l in layers)
        {
            if (!t.ShowsZeroLayers && l.Thickness <= 1e-9) continue;
            foreach (var material in new[] { l.Material }.Concat(l.PartMaterials ?? []))
            {
                var lines = BuildupTemplate.Lines(t.Template, Fields(m, e, l, material, entries.Count + 1, unit, projectUnits));
                if (lines.Count > 0) entries.Add(new BuildupEntry(l.Index, lines));
            }
        }
        return new BuildupContent(BuildupTemplate.Lines(t.Header ?? "", Element), entries, BuildupTemplate.Lines(t.Footer ?? "", Element), b.Stack);
    }

    /// <summary>A text's width on paper (mm) at a cap height of <paramref name="h"/> mm: Helvetica's, as the PDF prints it.</summary>
    public static double Measure(string s, double h, bool bold) => HelveticaMetrics.Advance(s, bold) * h / 0.718;

    /// <summary>A value's unit after it ("0.035 W/mK:", "150 mm"): letters, a slash, a degree or a power, short, maybe a stop after it.</summary>
    static bool IsUnit(string w) => w.Length <= 8 && System.Text.RegularExpressions.Regex.IsMatch(w, @"^[A-Za-z°/·%²³µμ]+[.,:;)]*$");
    static bool IsNumber(string w) => w.Length > 0 && (char.IsDigit(w[0]) || w[0] is '-' or '−' or '+') && w.Any(char.IsDigit);

    /// <summary>
    /// The words of a text kept together where a note keeps them: a symbol, its "=" and its value ("λ = 0.035"), a value
    /// and its unit ("0.035 W/mK:", "150 mm").
    /// </summary>
    public static List<string> Words(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var o = new List<string>();
        for (var i = 0; i < words.Length; i++)
        {
            var w = words[i];
            if (o.Count > 0 && (w == "=" || o[^1].EndsWith(" =", StringComparison.Ordinal) || o[^1] == "="
                || IsUnit(w) && IsNumber(o[^1].Split(' ')[^1]))) o[^1] += " " + w;
            else o.Add(w);
        }
        return o;
    }

    /// <summary>The words of <paramref name="text"/> in lines no wider than <paramref name="width"/> (a word longer than that alone on its line).</summary>
    public static List<string> Wrap(string text, double width, double h, bool bold)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in Words(text))
        {
            var next = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && Measure(next, h, bold) > width) { lines.Add(line); line = word; }
            else line = next;
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
    }

    /// <summary>What an entry's line starts with.</summary>
    public static string BulletOf(string bullet, int number) => bullet switch { "dash" => "–", "number" => $"{number}.", "none" => "", _ => "•" };

    /// <summary>
    /// The tag laid out on paper: left-aligned, lines 1.7 text heights apart, the header and footer a little apart from
    /// the layers; each entry after its bullet with a hanging indent, wrapped at <paramref name="widthMm"/>.
    /// </summary>
    public static BuildupLayout Lay(BuildupContent c, double h, bool bold, string bullet, double widthMm)
    {
        var step = 1.7 * h;
        var gap = 0.5 * h;
        var texts = new List<LaidText>();
        var mids = new List<double>();
        var y = h;
        var first = true;
        var widest = 0.0;
        void Line(double x, string s, string role, int entry)
        {
            texts.Add(new LaidText(x, y, s, role, entry));
            widest = Math.Max(widest, x + Measure(s, h, bold));
        }
        void Next() { y += step; }
        foreach (var hl in c.Header)
            foreach (var w in Wrap(hl, widthMm, h, bold)) { if (!first) Next(); Line(0, w, "header", -1); first = false; }
        var indent = bullet == "none" || c.Entries.Count == 0 ? 0
            : Enumerable.Range(1, c.Entries.Count).Max(n => Measure(BulletOf(bullet, n), h, bold)) + 0.8 * h;
        var available = Math.Max(widthMm - indent, 8 * h);
        for (var i = 0; i < c.Entries.Count; i++)
        {
            var lineNo = 0;
            foreach (var text in c.Entries[i].Lines)
                foreach (var w in Wrap(text, available, h, bold))
                {
                    if (!first) { Next(); if (lineNo == 0 && i == 0 && c.Header.Count > 0) y += gap; }
                    first = false;
                    if (lineNo == 0)
                    {
                        mids.Add(y - h / 2);
                        if (indent > 0) Line(0, BulletOf(bullet, i + 1), "bullet", i);
                    }
                    Line(indent, w, "line", i);
                    lineNo++;
                }
        }
        var footerFirst = true;
        foreach (var fl in c.Footer)
            foreach (var w in Wrap(fl, widthMm, h, bold))
            {
                if (!first) { Next(); if (footerFirst && c.Entries.Count > 0) y += gap; }
                first = false;
                footerFirst = false;
                Line(0, w, "footer", -1);
            }
        return new BuildupLayout(texts, widest, texts.Count == 0 ? 0 : y + 0.35 * h, mids);
    }
}
