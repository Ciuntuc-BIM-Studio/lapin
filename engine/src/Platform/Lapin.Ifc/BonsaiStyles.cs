using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Ifc;

/// <summary>
/// The project's drawing styles for Bonsai (BlenderBIM), written beside an IFC export (docs/spec-ifc.md, Stilurile de
/// desen pentru Bonsai): a CSS stylesheet and an SVG file of hatch patterns, after the convention Bonsai's drawings
/// follow (read from its public source for the naming only). Bonsai draws a drawing as SVG and puts classes on what it
/// draws: the IFC class (<c>IfcWall</c>), <c>material-&lt;name&gt;</c> (the material's name, or a layer set's name, with
/// every run of characters other than ASCII letters and digits removed), for a wall cut layer by layer
/// <c>layer-material-&lt;name&gt;</c>, and what the line is (<c>cut</c>, <c>projection</c>, <c>surface</c> for a cut's
/// fill). Its stylesheet is inlined into the drawing, and its patterns file's children copied into the drawing's
/// definitions, so a rule fills with <c>url(#id)</c>. A project points new drawings at both through
/// <c>BBIM_Documentation</c> on IfcProject (<c>StylesheetPath</c>, <c>PatternsPath</c>, relative to the IFC file).
/// What Lapin gives: the cut, projection and hatch pens of its drawings (the project's object styles over Lapin's
/// defaults, at 1:100), each IFC class's cut and projection lines, each material's cut hatch (its own, or what its name
/// suggests) with its fill colour and scale, its elevation hatch on what is seen, the text heights of the drafting
/// standards. Paper millimetres throughout, as Lapin's patterns are.
/// </summary>
public static class BonsaiStyles
{
    public const string SetName = "BBIM_Documentation";
    public const string StylesheetKey = "StylesheetPath", PatternsKey = "PatternsPath";
    const double Scale = 100;

    /// <summary>The stylesheet and the patterns file, as text.</summary>
    public sealed record Sheet(string Css, string Patterns);

    /// <summary>A name as Bonsai makes a class of it: every run of characters other than ASCII letters and digits removed ("Concrete C25/30" → ConcreteC2530).</summary>
    public static string ClassName(string name) => Regex.Replace(name, "[^0-9a-zA-Z]+", "");

    /// <summary>The files' names beside an IFC file of this name: <c>house.css</c> and <c>house-patterns.svg</c>.</summary>
    public static (string Css, string Patterns) FileNames(string ifcName)
    {
        var stem = Path.GetFileNameWithoutExtension(ifcName);
        if (stem.Length == 0) stem = "project";
        return ($"{stem}.css", $"{stem}-patterns.svg");
    }

    static string N(double v) => Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>The stylesheet and patterns of the model's materials, layer sets and classes.</summary>
    public static Sheet Of(IModelView m)
    {
        var project = Spatial.Project(m);
        var table = LineStyles.ReadTable(project?.Attrs["penTable"]);
        var styles = LineStyles.For(project?.Attrs["objectStyles"] as JsonObject, null, null, table, Scale);
        var extra = DraftingResources.Patterns(m);
        var book = DraftingResources.Book(m);
        var (cut, seen, hidden, hatchPen, _) = Pens.Default;
        var patterns = new Patterns(extra, hatchPen);
        var css = new StringBuilder();
        css.Append("/* Drawing styles for Bonsai (BlenderBIM), written by Lapin with the IFC export: the project's materials, pens and text.\n")
           .Append("   Paper millimetres. Bonsai puts on what it draws the IFC class, material-<name> (or layer-material-<name>, layer by layer)\n")
           .Append("   and cut, projection or surface; the hatches are in the patterns file beside this one (url(#id)). */\n\n");

        // What every drawing has: cut lines, what is seen, the fills of a cut, the edges Bonsai tells apart, annotation and text.
        css.Append("* { stroke-linecap: round; stroke-linejoin: round; }\n");
        css.Append($".cut {{ fill: white; stroke: black; stroke-width: {N(cut)}; fill-rule: evenodd; }}\n");
        css.Append($".projection {{ fill: white; stroke: black; stroke-width: {N(seen)}; }}\n");
        css.Append($".surface {{ fill: white; stroke: none; }}\n");
        css.Append($"path.outline {{ stroke-width: {N(seen)}; }}\npath.boundary {{ stroke-width: {N(seen)}; }}\npath.crease {{ stroke-width: {N(hidden)}; }}\n");
        css.Append($"path.sharp {{ stroke-width: {N(hidden)}; }}\npath.flush {{ stroke-width: {N(hatchPen)}; }}\n");
        css.Append($".IfcAnnotation {{ fill: none; stroke: black; stroke-width: {N(seen)}; }}\n");
        var regular = book.TextStyles.FirstOrDefault()?.Height ?? 2.5;
        css.Append($"text {{ fill: black; stroke: none; font-family: sans-serif; font-size: {N(regular)}px; }}\n");
        foreach (var (cls, k) in new[] { ("title", 2.0), ("header", 1.4), ("large", 1.4), ("regular", 1.0), ("small", 0.7) })
            css.Append($"text.{cls} {{ font-size: {N(regular * k)}px; }}\n");
        css.Append('\n');

        // Each IFC class's cut and projection lines, as the project's object styles give them.
        var classes = m.Entities.Select(e => (string?)e.Attrs["ifcClass"] ?? e.Class).Where(c => c.StartsWith("Ifc", StringComparison.Ordinal) && LineStyles.CategoryOf(c) != null)
            .Distinct().Order(StringComparer.Ordinal).ToList();
        if (styles != null)
            foreach (var c in classes)
                foreach (var (role, cssClass) in new[] { ("cut", "cut"), ("projection", "projection") })
                    if (styles(role, c, null) is { } line && (line.WidthMm != null || line.Color != null || line.Dash is { Length: > 0 }))
                    {
                        var decl = new List<string>();
                        if (line.WidthMm is { } w) decl.Add($"stroke-width: {N(w)}");
                        if (line.Color is { Length: > 0 } col) decl.Add($"stroke: {col}");
                        if (line.Dash is { Length: > 0 } d) decl.Add($"stroke-dasharray: {string.Join(" ", d.Select(N))}");
                        css.Append($".{c}.{cssClass}, .{c} .{cssClass} {{ {string.Join("; ", decl)}; }}\n");
                    }
        css.Append('\n');

        // Each material: its cut hatch (and fill) where it is cut, its elevation hatch where it is seen.
        var taken = new Dictionary<string, string>(StringComparer.Ordinal);
        void Material(string name, Entity? material, bool layerToo)
        {
            var key = ClassName(name);
            if (key.Length == 0) return;
            if (taken.TryGetValue(key, out var first))
            {
                if (first != name) css.Append($"/* \"{name}\" makes the same class as \"{first}\": the first one's styles. */\n");
                return;
            }
            taken[key] = name;
            var section = MaterialGraphics.Resolve(material, "section", name);
            var fill = section.Membrane ? null : patterns.Fill(section.Hatch, section.HatchScale, section.Fill);
            var selectors = new List<string> { $".cut.material-{key}", $".surface.material-{key}", $".material-{key} .cut", $".material-{key} .surface" };
            if (layerToo) selectors.AddRange([$".cut.layer-material-{key}", $".surface.layer-material-{key}"]);
            css.Append($"{string.Join(", ", selectors)} {{ fill: {fill ?? "white"}; }}\n");
            var elevation = MaterialGraphics.Resolve(material, "elevation", name);
            if (patterns.Fill(elevation.Hatch, elevation.HatchScale, null, background: "white") is { } seenFill)
                css.Append($".projection.material-{key}, .material-{key} .projection {{ fill: {seenFill}; }}\n");
        }
        foreach (var mat in m.OfClass(IfcClass.Material).OrderBy(x => x.Name, StringComparer.Ordinal)) Material(mat.Name, mat, true);
        // A layered element's material class is its layer set's name: the core layer's material.
        foreach (var set in m.OfClass(IfcClass.MaterialLayerSet).OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            var layers = Layers.Read(set);
            if (layers.Count == 0) continue;
            var core = layers[Math.Clamp(Layers.CoreIndex(layers), 0, layers.Count - 1)];
            var material = m.Get(core.Material);
            if (taken.ContainsKey(ClassName(set.Name))) continue;
            css.Append($"/* Layer set \"{set.Name}\": its core, {material?.Name ?? core.Material}. */\n");
            Material(set.Name, material, false);
        }
        return new Sheet(css.ToString(), patterns.Svg());
    }

    /// <summary>
    /// The SVG patterns the stylesheet fills with, one per hatch, scale and background, made from Lapin's patterns (the
    /// public .pat line families): each family a pattern of its own (a line along the tile, as long as its dashes repeat,
    /// the tile as tall as the spacing, sheared by the shift between lines and turned by the angle), and the hatch a tile
    /// that lays its background and each family over it.
    /// </summary>
    sealed class Patterns(IReadOnlyList<HatchPattern> extra, double pen)
    {
        readonly Dictionary<string, string> made = new(StringComparer.Ordinal);
        readonly StringBuilder svg = new();

        /// <summary>The fill of a hatch (a url, a colour) or null when it has none.</summary>
        public string? Fill(string? hatch, double scale, string? fill, string? background = null)
        {
            var bg = fill ?? background;
            if (string.IsNullOrEmpty(hatch)) return fill;
            var p = HatchLibrary.Find(hatch.Equals(BattHatch.Name, StringComparison.OrdinalIgnoreCase) ? "INSULATION" : hatch, extra);
            if (p == null) return fill;
            if (p.Solid) return "black";
            if (scale <= 0 || !double.IsFinite(scale)) scale = 1;
            var id = $"lapin-{ClassName(p.Name).ToLowerInvariant()}" + (Math.Abs(scale - 1) > 1e-9 ? $"-x{N(scale).Replace('.', '_')}" : "")
                + (bg is { Length: > 0 } ? $"-{ClassName(bg).ToLowerInvariant()}" : "");
            if (!made.ContainsKey(id)) made[id] = Make(id, p, scale, bg);
            return $"url(#{id})";
        }

        string Make(string id, HatchPattern p, double scale, string? bg)
        {
            var families = new List<string>();
            var i = 0;
            foreach (var f in p.Families)
            {
                if (Math.Abs(f.DeltaY) < 1e-9) continue;
                var fid = $"{id}-{i++}";
                var h = Math.Abs(f.DeltaY);
                var (dash, period) = Dashes(f.Dashes);
                var w = period > 0 ? period : 10;
                var skew = Math.Atan(f.DeltaX / f.DeltaY) * 180 / Math.PI;
                // y runs down in SVG: the pattern mirrored first, so 45° lines rise to the right as Lapin draws them.
                var t = $"scale({N(scale)} {N(-scale)}) translate({N(f.Origin.X)} {N(f.Origin.Y)}) rotate({N(f.AngleDeg)}) skewX({N(skew)}) translate(0 {N(-h / 2)})";
                svg.Append($"  <pattern id=\"{fid}\" patternUnits=\"userSpaceOnUse\" width=\"{N(w)}\" height=\"{N(h)}\" patternTransform=\"{t}\">")
                   .Append($"<line x1=\"0\" y1=\"{N(h / 2)}\" x2=\"{N(w)}\" y2=\"{N(h / 2)}\" stroke=\"black\" stroke-width=\"{N(pen / scale)}\"")
                   .Append(dash != null ? $" stroke-dasharray=\"{dash}\"" : "").Append("/></pattern>\n");
                families.Add(fid);
            }
            // The hatch: a large tile (a metre of paper, past any sheet) of its background and its families laid over it.
            const double Tile = 1000;
            svg.Append($"  <pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" width=\"{N(Tile)}\" height=\"{N(Tile)}\">");
            if (bg is { Length: > 0 }) svg.Append($"<rect width=\"{N(Tile)}\" height=\"{N(Tile)}\" fill=\"{bg}\"/>");
            foreach (var fid in families) svg.Append($"<rect width=\"{N(Tile)}\" height=\"{N(Tile)}\" fill=\"url(#{fid})\"/>");
            svg.Append("</pattern>\n");
            return id;
        }

        /// <summary>A family's dashes (positive drawn, negative a gap, 0 a dot) as an SVG dash array, and their period; null: a continuous line.</summary>
        static (string? Array, double Period) Dashes(IReadOnlyList<double> d)
        {
            if (d.Count == 0) return (null, 0);
            // Runs alternating drawn, gap, drawn…: a list that starts with a gap starts with a drawn run of nothing.
            var runs = new List<double>();
            bool? last = null;
            foreach (var x in d)
            {
                var drawn = x >= 0;
                var length = x == 0 ? 0.01 : Math.Abs(x);
                if (last == null && !drawn) { runs.Add(0); last = true; }
                if (last == drawn) runs[^1] += length;
                else { runs.Add(length); last = drawn; }
            }
            if (runs.Count % 2 == 1) runs.Add(0);
            var period = runs.Sum();
            return period <= 0 ? (null, 0) : (string.Join(" ", runs.Select(N)), period);
        }

        public string Svg() => "<svg xmlns=\"http://www.w3.org/2000/svg\">\n  <!-- Hatch patterns for Bonsai's drawings, written by Lapin with the IFC export (paper millimetres). -->\n" + svg + "</svg>\n";
    }
}
