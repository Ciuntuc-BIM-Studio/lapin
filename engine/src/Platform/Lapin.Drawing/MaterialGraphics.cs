using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Drawing.Hatch;
using Lapin.Model;

namespace Lapin.Drawing;

/// <summary>A material as one kind of view draws it: a hatch (a pattern name, or none), its scale, a fill colour, a hatch pen.</summary>
public sealed record ViewGraphics(string? Hatch = null, double? HatchScale = null, string? Fill = null, int? HatchPen = null)
{
    public static ViewGraphics? From(JsonNode? n) => n is JsonObject o
        ? new ViewGraphics(o.ContainsKey("hatch") ? (string?)o["hatch"] ?? "" : null, Lapin.Model.Json.Number(o["hatchScale"]),
            o.ContainsKey("fill") ? (string?)o["fill"] ?? "" : null, Lapin.Model.Json.Number(o["hatchPen"]) is { } p ? (int)p : null)
        : null;
}

/// <summary>A material's graphics in one kind of view, and where they came from: its own attributes ("own") or its name ("name").</summary>
public sealed record ResolvedMaterial(string Name, string? Hatch, double HatchScale, string? Fill, int? HatchPen, bool Membrane, bool DrawJoints, string? Color, string Source)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["name"] = Name, ["hatch"] = Hatch, ["hatchScale"] = HatchScale, ["fill"] = Fill, ["membrane"] = Membrane, ["drawJoints"] = DrawJoints, ["source"] = Source };
        if (HatchPen is { } p) o["hatchPen"] = p;
        if (Color != null) o["color"] = Color;
        return o;
    }
}

/// <summary>
/// How an IfcMaterial draws where a view cuts or sees it (the old Lapin's
/// project materials, held now on the material itself). Its attributes:
/// <c>section</c> {hatch, hatchScale, fill, hatchPen} — what a cut shows (fill: a
/// colour, or "material" for the material's own colour; none unless asked);
/// <c>plan</c> and <c>elevation</c>, the same, for those kinds of view;
/// <c>membrane</c> (drawn as a heavy dashed line); <c>drawJoints</c>.
/// A field the material does not give comes from its name: concrete,
/// brick, insulation, timber, steel… A plan takes the section's graphics
/// unless it has its own; an elevation draws nothing unless told.
/// <c>hatch</c> null (JSON null) means "no hatch", absent means "from the name".
/// </summary>
public static class MaterialGraphics
{
    public static readonly string[] Kinds = ["section", "plan", "elevation"];

    /// <summary>Layers thinner than this are not cut into bands: they draw as membranes.</summary>
    public const double MembraneThickness = 0.002;

    public static string Normalise(string name) => Regex.Replace(name.Trim().ToLowerInvariant(), @"\s+", " ");

    sealed record Guess(bool Membrane = false, string? Hatch = null);

    static readonly (Regex Re, Guess G)[] Guesses =
    [
        (new(@"vapou?r|barrier|membrane|foil|\bdpc\b|damp|bitum|sheet|folie|barier"), new(Membrane: true)),
        (new(@"insul|wool|eps\b|xps\b|polystyren|pir\b|pur\b|vata|izola"), new(Hatch: "INSULATION")),
        (new(@"brick|masonry|block|caramid|zidari|bca|aac"), new(Hatch: "BRICK")),
        (new(@"concrete|beton|screed|sapa|c\d\d/"), new(Hatch: "CONCRETE")),
        (new(@"timber|wood|clt|osb|plywood|lemn"), new(Hatch: "WOOD")),
        (new(@"steel|metal|otel|alumin"), new(Hatch: "STEEL")),
        (new(@"gravel|ballast|pietris|aggregate"), new(Hatch: "GRAVEL")),
        (new(@"earth|soil|pamant|ground"), new(Hatch: "EARTH")),
        (new(@"tile|ceramic|gresie|faianta"), new(Hatch: "TILES")),
        (new(@"plaster|render|gypsum|tencuial|gips|finish|paint"), new()),
    ];

    static Guess? GuessOf(string name)
    {
        // Romanian diacritics fold to their letters, as the old names were typed.
        var n = Normalise(name).Replace('ă', 'a').Replace('â', 'a').Replace('î', 'i').Replace('ș', 's').Replace('ş', 's').Replace('ț', 't').Replace('ţ', 't');
        foreach (var (re, g) in Guesses) if (re.IsMatch(n)) return g;
        return null;
    }

    /// <summary>What a name suggests for a section: its hatch (or none) and whether it is a membrane.</summary>
    public static (string? Hatch, bool Membrane) FromName(string name) => GuessOf(name) is { } g ? (g.Hatch, g.Membrane) : (null, false);

    /// <summary>The material's own graphics for a kind of view (a plan: the section's fields, then the plan's own).</summary>
    static ViewGraphics? Own(Entity? material, string kind)
    {
        if (material == null) return null;
        var section = ViewGraphics.From(material.Attrs["section"]);
        return kind switch
        {
            "section" => section,
            "plan" => Merge(section, ViewGraphics.From(material.Attrs["plan"])),
            _ => ViewGraphics.From(material.Attrs["elevation"]),
        };
    }

    static ViewGraphics? Merge(ViewGraphics? a, ViewGraphics? b) => (a, b) switch
    {
        (null, null) => null,
        (null, _) => b,
        (_, null) => a,
        _ => new ViewGraphics(b!.Hatch ?? a!.Hatch, b.HatchScale ?? a!.HatchScale, b.Fill ?? a!.Fill, b.HatchPen ?? a!.HatchPen),
    };

    /// <summary>The material's graphics in one kind of view: its own fields, else what its name suggests (not for elevations).</summary>
    public static ResolvedMaterial Resolve(Entity? material, string kind = "section", string? name = null)
    {
        name ??= material?.Name ?? "";
        var own = Own(material, kind);
        var guess = kind == "elevation" ? null : GuessOf(name);
        var color = material?.Str("color");
        // "" in an own field is an explicit none; null is "not given".
        string? hatch = own?.Hatch is { } h ? (h.Length == 0 ? null : h.ToUpperInvariant()) : guess?.Hatch;
        // A fill only when asked: a colour, or "material" for the material's own (every material has a colour for 3D).
        string? fill = own?.Fill is { Length: > 0 } f ? (f == "material" ? color : f) : null;
        var hasOwn = own != null || material?.Attrs.ContainsKey("membrane") == true;
        var membrane = material?.Attrs["membrane"] is JsonValue mv && mv.TryGetValue<bool>(out var mb) ? mb : guess?.Membrane ?? false;
        if (membrane) hatch = null;
        var joints = material?.Attrs["drawJoints"] is JsonValue jv && jv.TryGetValue<bool>(out var j) && j;
        return new ResolvedMaterial(name, hatch, own?.HatchScale ?? 1, fill, own?.HatchPen, membrane, joints, color, hasOwn ? "own" : "name");
    }

    /// <summary>
    /// A hatch as an IFC file describes it, as a pattern of the library: a
    /// named pattern (known, or guessed from its name), or a line hatching
    /// (horizontal, else the 45° lines). For reading IFC materials into Lapin's.
    /// </summary>
    public static ViewGraphics? FromIfcHatch(string? pattern, double? scale, double? linesAngleDeg, string? background, IEnumerable<HatchPattern>? extra = null)
    {
        ViewGraphics? g = null;
        if (pattern != null)
        {
            var known = HatchLibrary.Find(pattern, extra) != null ? pattern.ToUpperInvariant() : FromName(pattern).Hatch;
            if (known != null) g = new ViewGraphics(known, scale ?? 1);
        }
        else if (linesAngleDeg is { } a)
        {
            var d = ((a % 180) + 180) % 180;
            g = new ViewGraphics(Math.Abs(d) < 1 || Math.Abs(d - 180) < 1 ? "HORIZONTAL" : "LINES45", 1);
        }
        if (background != null) g = (g ?? new ViewGraphics("")) with { Fill = background };
        return g;
    }

    /// <summary>
    /// What IFC gets from the material (null when it gives nothing of its own):
    /// its colour and the hatches of the section, plan and elevation — written
    /// on export as IfcSurfaceStyle and IfcFillAreaStyle, named by view.
    /// </summary>
    public static JsonObject? Appearance(Entity material)
    {
        var a = material.Attrs;
        if (!a.ContainsKey("section") && !a.ContainsKey("plan") && !a.ContainsKey("elevation") && !a.ContainsKey("color")) return null;
        var o = new JsonObject();
        if (material.Str("color") is { } c) o["color"] = c;
        if (material.Attrs["transparency"] is { } t) o["transparency"] = t.DeepClone();
        var hatches = new JsonObject();
        JsonObject? HatchOf(ViewGraphics? g)
        {
            if (g == null || (string.IsNullOrEmpty(g.Hatch) && string.IsNullOrEmpty(g.Fill))) return null;
            var h = new JsonObject();
            if (!string.IsNullOrEmpty(g.Hatch)) h["pattern"] = g.Hatch.ToUpperInvariant();
            if (!string.IsNullOrEmpty(g.Hatch) && g.HatchScale is { } s && s != 1) h["scale"] = s;
            if (!string.IsNullOrEmpty(g.Fill)) h["background"] = g.Fill;
            return h;
        }
        var section = Resolve(material, "section");
        if (!section.Membrane && HatchOf(new ViewGraphics(section.Hatch, section.HatchScale, section.Fill)) is { } sh) hatches["section"] = sh;
        if (a.ContainsKey("plan") && Resolve(material, "plan") is var plan && HatchOf(new ViewGraphics(plan.Hatch, plan.HatchScale, plan.Fill)) is { } ph) hatches["plan"] = ph;
        if (a.ContainsKey("elevation") && Resolve(material, "elevation") is var el && HatchOf(new ViewGraphics(el.Hatch, el.HatchScale, el.Fill)) is { } eh) hatches["elevation"] = eh;
        if (hatches.Count > 0) o["hatches"] = hatches;
        return o;
    }
}
