using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The diagram look (decisions 843 and 885, forum topic 283; docs/plan-diagram-2d.md): a white model, or a muted pastel by
/// category, in the 3D's Diagram display and in the plans, sections and elevations whose view style says <c>diagram</c>
/// white or pastel. The categories and the default palette are the 3D's (<c>ui/src/viewport/diagram.ts</c>:
/// <c>categoryOf</c>, <c>DEFAULT_PALETTE</c>, <c>WHITE_MODEL</c>; <c>DiagramViewTests</c> reads both and keeps them equal).
/// The palette is the project's (<see cref="Key"/>: only the categories it changes), so a sheet prints alike on every
/// computer and the 3D, which reads it too, matches the drawings.
/// </summary>
public static class DiagramLook
{
    /// <summary>The project's attribute holding its palette: {category: #rrggbb}, the categories that differ from the default.</summary>
    public const string Key = "diagramPalette";

    public const string Off = "off", White = "white", Pastel = "pastel";
    public static readonly string[] Modes = [Off, White, Pastel];

    public static readonly string[] Categories = ["walls", "floors", "roofs", "structure", "openings", "site", "greenery", "other"];

    /// <summary>The default pastel palette: muted, readable on a light ground, one hue family a category.</summary>
    public static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        ["walls"] = "#e8d9c4", ["floors"] = "#cfd8dc", ["roofs"] = "#dcaa9c", ["structure"] = "#c9c3dd", ["openings"] = "#a9cfe0",
        ["site"] = "#dcd6c0", ["greenery"] = "#b5d6ac", ["other"] = "#e3e3e1",
    };

    /// <summary>The white model's one colour: a light warm grey, a card model's.</summary>
    public const string WhiteModel = "#ecebe8";

    /// <summary>A diagram drawing's cut outline: thin, mid grey.</summary>
    public const string CutColor = "#7a8087";
    public const double CutWidth = 0.25;
    /// <summary>A diagram drawing's seen lines: thinner, light grey.</summary>
    public const string SeenColor = "#a4a9af";
    public const double SeenWidth = 0.13;

    static readonly HashSet<string> Skipped = ["IfcSpace", "IfcOpeningElement", "IfcAnnotation", "IfcGrid", "IfcVirtualElement"];
    static readonly Dictionary<string, string> OfClass = new()
    {
        ["IfcWall"] = "walls", ["IfcWallStandardCase"] = "walls", ["IfcCurtainWall"] = "walls", ["IfcPlate"] = "walls",
        ["IfcSlab"] = "floors", ["IfcCovering"] = "floors", ["IfcStair"] = "floors", ["IfcStairFlight"] = "floors", ["IfcRamp"] = "floors", ["IfcRampFlight"] = "floors",
        ["IfcRoof"] = "roofs",
        ["IfcColumn"] = "structure", ["IfcBeam"] = "structure", ["IfcMember"] = "structure", ["IfcPile"] = "structure", ["IfcFooting"] = "structure",
        ["IfcDoor"] = "openings", ["IfcWindow"] = "openings",
        ["IfcSite"] = "site", ["IfcCivilElement"] = "site", ["IfcGeographicElement"] = "site",
        ["IfcFurniture"] = "greenery", ["IfcFurnishingElement"] = "greenery",
    };
    static readonly string[] Green = ["VEGETATION", "TREE", "SHRUB", "PLANT", "HEDGE", "LAWN", "GRASS"];

    /// <summary>The category of an IFC class (and its predefined type: a geographic element is terrain, or vegetation); null: not coloured.</summary>
    public static string? CategoryOf(string ifcClass, string? predefinedType = null)
    {
        if (Skipped.Contains(ifcClass)) return null;
        if (ifcClass == "IfcGeographicElement" && predefinedType is { } p && Green.Any(g => p.StartsWith(g, StringComparison.OrdinalIgnoreCase))) return "greenery";
        return OfClass.GetValueOrDefault(ifcClass) ?? "other";
    }

    /// <summary>An element's category (<see cref="CategoryOf(string, string?)"/>), its class when it is not in the model.</summary>
    public static string? CategoryOf(IModelView m, string element, string ifcClass) =>
        CategoryOf(ifcClass, ifcClass == "IfcGeographicElement" ? m.Get(element)?.PredefinedType : null);

    /// <summary>The project's palette: its own colours over the defaults.</summary>
    public static Dictionary<string, string> Palette(IModelView m)
    {
        var o = new Dictionary<string, string>(Defaults);
        if (Spatial.Project(m)?.Attrs[Key] is JsonObject own)
            foreach (var c in Categories)
                if ((string?)own[c] is { } v && HexColor.IsColour(v)) o[c] = v.ToLowerInvariant();
        return o;
    }

    /// <summary>A category's colour in a mode (white: the white model's; pastel: the palette's); null when the mode is off or there is no category.</summary>
    public static string? ColourOf(string mode, string? category, IReadOnlyDictionary<string, string> palette) => category == null ? null : mode switch
    {
        White => WhiteModel,
        Pastel => palette.GetValueOrDefault(category) ?? Defaults["other"],
        _ => null,
    };

    /// <summary>A mode as a command gives it (null: off).</summary>
    public static string ModeOf(JsonNode? n) => n is null ? Off
        : (string?)n is { } v && Modes.Contains(v) ? v : throw new CommandException($"„{n}” is not off, white or pastel.");

    /// <summary>
    /// The colours a view draws its categories in: its mode and the project's palette, worked out once a drawing; null when
    /// the view is not a diagram.
    /// </summary>
    public sealed class View(IModelView m, string mode)
    {
        readonly Dictionary<string, string> _palette = Palette(m);
        readonly Dictionary<string, string?> _cache = [];
        public string Mode { get; } = mode;

        /// <summary>An element's colour in the view; null: none (a room, an opening's void, an annotation).</summary>
        public string? ColourOf(string element, string ifcClass)
        {
            var k = ifcClass == "IfcGeographicElement" ? element : ifcClass;
            if (_cache.TryGetValue(k, out var c)) return c;
            return _cache[k] = DiagramLook.ColourOf(Mode, CategoryOf(m, element, ifcClass), _palette);
        }

        public static View? Of(IModelView m, ViewStyles.Style s) => s.Diagram is White or Pastel ? new View(m, s.Diagram) : null;
    }

    public static void Register(IModuleRegistry r)
    {
        // The project's palette: the colours it draws by (its own over the defaults), what it changes, and the defaults.
        r.Query("diagram.palette", (q, _) =>
        {
            var own = Spatial.Project(q.Model)?.Attrs[Key] as JsonObject;
            var p = Palette(q.Model);
            return new JsonObject
            {
                ["palette"] = new JsonObject(p.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
                ["own"] = own?.DeepClone() ?? new JsonObject(),
                ["defaults"] = new JsonObject(Defaults.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
                ["white"] = WhiteModel,
            };
        });

        // The palette changed: {palette: {category: #rrggbb | null (back to the default)}}, or {reset: true}; one undo step.
        r.Command("diagram.setPalette", "Diagram colours", (ctx, a) =>
        {
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            JsonObject own = a["reset"] is JsonValue rv && rv.TryGetValue<bool>(out var reset) && reset ? new JsonObject() : project.Attrs[Key]?.DeepClone() as JsonObject ?? new JsonObject();
            if (a["palette"] is JsonObject patch)
                foreach (var (c, v) in patch)
                {
                    if (!Categories.Contains(c)) throw new CommandException($"„{c}” is not a diagram category ({string.Join(", ", Categories)}).");
                    if (v is null) { own.Remove(c); continue; }
                    var hex = (string?)v is { } s && HexColor.IsColour(s) ? s.ToLowerInvariant() : throw new CommandException($"A diagram colour is #rrggbb (or null for the default); „{v}” is not.");
                    if (hex == Defaults[c]) own.Remove(c); else own[c] = hex;
                }
            else if (a["reset"] is null) throw new CommandException("diagram.setPalette takes {palette: {category: colour | null}} or {reset: true}.");
            var attrs = project.CloneAttrs();
            if (own.Count > 0) attrs[Key] = own; else attrs.Remove(Key);
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });
    }
}
