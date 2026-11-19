using System.Globalization;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A window's sills as things of their own (forum topic 178, decision 727, tester LVK): the interior and the exterior sill a
/// window's type sets (its detailed sills, else its plain boards), each with its side, material, length, depth across the wall,
/// projection past the wall's face, thickness, overhang past the jambs and slope, worked out on every ask from the type and where
/// the window stands, never stored. They stay parts of the window (its body draws them, IFC writes them inside the window, as
/// before); what is new is that schedules list them as rows (the <c>sills</c> part source and the shipped "Window sills"
/// schedule, for the sills' supplier), tags point at one (a tag's <c>part</c>), and the window's own fields read them.
/// <para>
/// IFC4 has no element made for a window's sill: IfcCoveringTypeEnum has none (a sill is no flooring, cladding or molding),
/// and IfcMember, IfcPlate or IfcBuildingElementPart would turn a part of the window into elements of their own that a file
/// read back would hold twice. So the export stays as it was: the sills inside the window's body, their lengths in its
/// quantities (SillInsideLength, SillOutsideLength), the material by its board constituent.
/// </para>
/// </summary>
public static class WindowSills
{
    /// <summary>The part source's id: a field schedule set to it lists every window's sills.</summary>
    public const string Source = "sills";

    /// <summary>
    /// One sill: <paramref name="Key"/> its part (sill:inside, sill:outside, as the window's body names it), <paramref name="Side"/>
    /// interior or exterior, its sizes in metres (<paramref name="Depth"/> across the wall, from the frame to its front edge;
    /// <paramref name="Projection"/> past the wall's face), its slope in degrees, whether it is a detailed sill or the type's
    /// plain board, its corners in the model.
    /// </summary>
    public sealed record Sill(string Key, string Side, string? Material, double Length, double Depth, double Projection, double Thickness,
        double Overhang, double Slope, bool Detailed, IReadOnlyList<Vec3> Points);

    /// <summary>The sills of a door or window, interior first; none for one without, a bay window (its sill is its own), a curtain wall's door.</summary>
    public static IReadOnlyList<Sill> Of(IModelView m, Entity fill)
    {
        if (fill.Class is not (IfcClass.Window or IfcClass.Door) || CurtainCells.SeatOf(m, fill) != null) return [];
        if (Openings.SpecOf(m, fill.Id) is not { } spec || spec.Frameless != null || BayWindows.Projecting(spec)) return [];
        if (Openings.OpeningOf(m, fill.Id) is not { } opening || Openings.WallOf(m, opening.Id) is not { } wall) return [];
        var p = Openings.Place(m, opening);
        var f = Openings.FrameOf(m, wall, p.Along);
        var (sx, sy) = (fill.Bool("flipHand") ? -1 : 1, fill.Bool("flipSide") ? -1 : 1);
        Vec3 Map(double x, double y, double z) => f.At(p.Along + sx * x, f.Centre - sy * y, p.FillSill + z);
        IReadOnlyList<Vec3> Box(double x0, double x1, double y0, double y1, double z0, double z1) =>
            [Map(x0, y0, z0), Map(x1, y0, z0), Map(x1, y1, z0), Map(x0, y1, z0), Map(x0, y0, z1), Map(x1, y0, z1), Map(x1, y1, z1), Map(x0, y1, z1)];
        var t = f.YMax - f.YMin;
        var (yInt, yExt) = (-t / 2, t / 2);
        var (yFI, yFE) = (spec.Frame.Offset - spec.Frame.Depth / 2, spec.Frame.Offset + spec.Frame.Depth / 2);
        var w = spec.Width;
        var d = spec.Details ?? new JoineryDetails();
        // The detailed sills stand where the wall is straight (Openings.Dressing); else, and when none is set, the plain boards.
        var detailed = PathCurve.Of(wall) is not { IsArc: true };
        var boardMaterial = spec.Materials?.Board;
        var o = new List<Sill>();
        if (detailed && d.SillInside is { On: true } si)
        {
            var proj = Math.Max(0, si.Projection);
            o.Add(new("sill:inside", "interior", si.Material ?? boardMaterial, w + 2 * si.Overhang, yFI - (yInt - proj), proj, si.Thickness, si.Overhang, 0, true,
                Box(-w / 2 - si.Overhang, w / 2 + si.Overhang, yInt - proj, yFI, -si.Thickness, 0)));
        }
        else if (!spec.IsDoor && spec.Board.Interior > 0)
        {
            var b = spec.Board.Interior;
            o.Add(new("sill:inside", "interior", boardMaterial, w, b, Math.Max(0, yInt - (yFI - b)), BoardThickness, 0, 0, false,
                Box(-w / 2, w / 2, yFI - b, yFI, 0, BoardThickness)));
        }
        if (detailed && d.SillOutside is { On: true } so)
        {
            var proj = Math.Max(0, so.Projection);
            var yOut = yExt + proj;
            var drop = (yOut - yFE) * Math.Tan(Math.Clamp(so.Slope, 0, 45) * Math.PI / 180);
            o.Add(new("sill:outside", "exterior", so.Material ?? boardMaterial, w + 2 * so.Overhang, yOut - yFE, proj, so.Thickness, so.Overhang, so.Slope, true,
                Box(-w / 2 - so.Overhang, w / 2 + so.Overhang, yFE, yOut, -so.Thickness - drop, 0)));
        }
        else if (!spec.IsDoor && spec.Board.Exterior > 0)
        {
            var b = spec.Board.Exterior;
            o.Add(new("sill:outside", "exterior", boardMaterial, w, b, Math.Max(0, yFE + b - yExt), BoardThickness, 0, 0, false,
                Box(-w / 2, w / 2, yFE, yFE + b, 0, BoardThickness)));
        }
        return o;
    }

    /// <summary>A plain board's thickness (Joinery's boards).</summary>
    const double BoardThickness = 0.025;

    static string Mm(double metres) => Math.Round(metres * 1000).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>What a sill's tag says by default: its side, its size (length × depth, mm) and its material.</summary>
    public static string Label(Sill s) =>
        $"{(s.Side == "interior" ? "Interior" : "Exterior")} sill {Mm(s.Length)} × {Mm(s.Depth)}{(string.IsNullOrWhiteSpace(s.Material) ? "" : " · " + s.Material.Trim())}";

    /// <summary>A sill as a part of its window: its fields for schedules and tags, its corners for a tag's leader.</summary>
    public static ElementPart Part(Sill s) => ElementPart.Of(s.Key,
        ("sillSide", s.Side == "interior" ? "Interior" : "Exterior"), ("sillMaterial", s.Material ?? ""),
        ("sillLength", s.Length), ("sillDepth", s.Depth), ("sillProjection", s.Projection), ("sillThickness", s.Thickness),
        ("sillOverhang", s.Overhang), ("sillSlope", s.Slope), ("sillArea", s.Length * s.Depth), ("sillCount", 1),
        ("sillKind", s.Detailed ? "Sill" : "Board"), ("partLabel", Label(s))) with { Points = s.Points };

    /// <summary>The sills as parts (the <c>sills</c> source), the window's own sill fields, and the shipped "Window sills" schedule.</summary>
    public static void Register(IModuleRegistry r)
    {
        r.Service<IPartSource>(new Parts());
        foreach (var cls in new[] { IfcClass.Window, IfcClass.Door })
            foreach (var (side, label) in new[] { ("interior", "Interior"), ("exterior", "Exterior") })
            {
                var cap = label;
                Sill? Of(IModelView m, Entity e) => WindowSills.Of(m, e).FirstOrDefault(x => x.Side == side);
                DerivedFields.Add(cls, new($"sill{cap}Material", $"{cap} sill: material", (m, e) => Of(m, e)?.Material));
                DerivedFields.Add(cls, new($"sill{cap}Length", $"{cap} sill: length (mm)", (m, e) => Of(m, e) is { } s ? Mm(s.Length) : null));
                DerivedFields.Add(cls, new($"sill{cap}Depth", $"{cap} sill: depth across the wall (mm)", (m, e) => Of(m, e) is { } s ? Mm(s.Depth) : null));
                DerivedFields.Add(cls, new($"sill{cap}Thickness", $"{cap} sill: thickness (mm)", (m, e) => Of(m, e) is { } s ? Mm(s.Thickness) : null));
            }
        r.Service(ShippedScheduleTemplate.Of("windowSills", "Window sills", "windowSills", TemplateFields));
    }

    /// <summary>
    /// The sills' schedule for their supplier: a row per kind of sill (side, material and sizes alike), how many, the windows'
    /// marks, the total length and area; sorted by side, material and length.
    /// </summary>
    public const string TemplateFields = """
        {
          "source": "sills", "totals": "always",
          "groupBy": ["sillSide", "sillMaterial", "sillLength", "sillDepth", "sillThickness", "sillSlope"], "groupColumns": false, "count": false,
          "sort": ["sillSide", "sillMaterial", "sillLength"],
          "columns": [
            { "field": "sillSide", "heading": "Side", "aggregate": "first" },
            { "field": "sillMaterial", "heading": "Material", "aggregate": "first" },
            { "formula": "sillLength * 1000", "heading": "Length (mm)", "aggregate": "first", "decimals": 0 },
            { "formula": "sillDepth * 1000", "heading": "Depth (mm)", "aggregate": "first", "decimals": 0 },
            { "formula": "sillThickness * 1000", "heading": "Thickness (mm)", "aggregate": "first", "decimals": 0 },
            { "field": "sillSlope", "heading": "Slope (°)", "aggregate": "first", "decimals": 0 },
            { "field": "mark", "heading": "Windows", "aggregate": "list" },
            { "field": "sillCount", "heading": "Count", "aggregate": "sum", "decimals": 0 },
            { "field": "sillLength", "heading": "Total length (m)", "aggregate": "sum", "decimals": 2 },
            { "field": "sillArea", "heading": "Area (m²)", "aggregate": "sum", "decimals": 2 }
          ]
        }
        """;

    sealed class Parts : IPartSource
    {
        public string Source => WindowSills.Source;
        public string Label => "Window sills";
        public IReadOnlyList<(string Key, string Label)> Fields =>
        [
            ("sillSide", "Side (interior, exterior)"), ("sillMaterial", "Material"), ("sillLength", "Length (m)"), ("sillDepth", "Depth across the wall (m)"),
            ("sillProjection", "Projection past the wall's face (m)"), ("sillThickness", "Thickness (m)"), ("sillOverhang", "Overhang past each jamb (m)"),
            ("sillSlope", "Slope (°)"), ("sillArea", "Area, length × depth (m²)"), ("sillCount", "Count"), ("sillKind", "Sill or plain board"), ("partLabel", "Sill in a few words"),
        ];

        public IEnumerable<ElementPart> PartsOf(IQueryContext query, Entity element) => WindowSills.Of(query.Model, element).Select(Part);
    }
}
