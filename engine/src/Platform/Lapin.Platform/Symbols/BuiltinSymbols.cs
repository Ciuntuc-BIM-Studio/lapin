using System.Text.Json.Nodes;

namespace Lapin.Platform;

/// <summary>
/// Lapin's own symbols (docs/plan-user-extensibility.md §3): read-only, duplicable, written as data the way a user's are, so a
/// copy can be changed in the symbol editor. Annotation symbols, in paper mm, their insertion point at the origin.
/// </summary>
public static class BuiltinSymbols
{
    public const string Prefix = "builtin:sym:";

    public sealed record Builtin(string Id, string Name, SymbolDef Def);

    public static bool Is(string? id) => id?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    static JsonArray P(params (double X, double Y)[] pts) => new(pts.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 3), Math.Round(p.Y, 3))).ToArray());
    static JsonObject Poly(bool closed, string? fill, double w, params (double X, double Y)[] pts)
    {
        var o = new JsonObject { ["type"] = "polyline", ["pts"] = P(pts), ["closed"] = closed, ["stroke"] = w };
        if (fill != null) o["fill"] = fill;
        return o;
    }
    static JsonObject Line(double x0, double y0, double x1, double y1, double w = 0.25) =>
        new() { ["type"] = "line", ["a"] = P((x0, y0))[0]!.DeepClone(), ["b"] = P((x1, y1))[0]!.DeepClone(), ["w"] = w };
    static JsonObject Circle(double x, double y, double r, double w = 0.25, string? fill = null)
    {
        var o = new JsonObject { ["type"] = "circle", ["c"] = new JsonArray(x, y), ["r"] = r, ["stroke"] = w };
        if (fill != null) o["fill"] = fill;
        return o;
    }
    static JsonObject Text(double x, double y, string text, double h, string align = "middle", string valign = "bottom", bool bold = false) =>
        new() { ["type"] = "text", ["at"] = new JsonArray(x, y), ["text"] = text, ["h"] = h, ["align"] = align, ["valign"] = valign, ["bold"] = bold };
    static SymbolAttribute A(string key, string label, string def) => new(key, label, def);

    static SymbolDef Def(string category, string description, JsonNode[] items, params SymbolAttribute[] attrs) =>
        new("annotation", category, new JsonArray(items), attrs, description);

    public static readonly IReadOnlyList<Builtin> All =
    [
        new(Prefix + "north-arrow", "North arrow", Def("north", "An arrow pointing north, its left half black, N over its tip. Turn it to the project's north.",
        [
            Poly(true, "byInstance", 0.25, (0, 10), (-4, -6), (0, -2)),
            Poly(true, null, 0.25, (0, 10), (4, -6), (0, -2), (-4, -6)),
            Text(0, 11.5, "N", 3.5, bold: true),
        ])),
        new(Prefix + "north-arrow-circle", "North arrow (circle)", Def("north", "A circle with a black arrow through it and N above.",
        [
            Circle(0, 0, 8),
            Poly(true, "byInstance", 0.25, (0, 8), (-3, -6), (0, -3.5), (3, -6)),
            Text(0, 9.5, "N", 3.5, bold: true),
        ])),
        new(Prefix + "callout", "Callout bubble", Def("callout", "A bubble with the detail's number over the sheet it is drawn on.",
        [
            Circle(0, 0, 6), Line(-6, 0, 6, 0),
            Text(0, 1.2, "{attr.number}", 3, bold: true), Text(0, -4.3, "{attr.sheet}", 2.2),
        ], A("number", "Number", "1"), A("sheet", "Sheet", "A-101"))),
        new(Prefix + "section-head", "Section head (bubble and arrow)", Def("section", "A bubble with the section's number and sheet, and a black arrow along the way it looks (+y).",
        [
            Poly(true, "byInstance", 0, (-4.24, 4.24), (0, 9.5), (4.24, 4.24)),
            Circle(0, 0, 6, fill: "#ffffff"), Line(-6, 0, 6, 0),
            Text(0, 1.2, "{attr.number}", 3, bold: true), Text(0, -4.3, "{attr.sheet}", 2.2),
        ], A("number", "Number", "1"), A("sheet", "Sheet", "A-101"))),
        new(Prefix + "elevation-mark", "Elevation mark", Def("elevation", "A circle and a black arrow looking +y, the elevation's number inside.",
        [
            Poly(true, "byInstance", 0, (-5.66, 5.66), (0, 11.3), (5.66, 5.66)),
            Circle(0, 0, 8, fill: "#ffffff"),
            Text(0, 0, "{attr.number}", 3, valign: "middle", bold: true),
        ], A("number", "Number", "1"))),
        new(Prefix + "revision", "Revision triangle", Def("revision", "A triangle with the revision's number.",
        [
            Poly(true, null, 0.25, (-4, -3), (4, -3), (0, 4)),
            Text(0, -1.9, "{attr.rev}", 2.5),
        ], A("rev", "Revision", "1"))),
        new(Prefix + "break-line", "Break line", Def("break", "A straight line with a zigzag in its middle, 30 mm long.",
        [
            Poly(false, null, 0.25, (-15, 0), (-2, 0), (-1, 3), (1, -3), (2, 0), (15, 0)),
        ])),
        new(Prefix + "spot-level", "Spot level", Def("level", "A small open triangle standing on a point, its level beside: read from the model in sections and elevations, the plan's level in plans.",
        [
            Poly(true, null, 0.25, (0, 0), (-1.7, 3), (1.7, 3)),
            Line(-3, 0, 3, 0),
            Text(2.4, 3.4, "{point.elevation}", 2.5, align: "start"),
        ])),
        new(Prefix + "level-open", "Level marker (open)", Def("level", "An open triangle on the level line, the level's name and elevation beside it.",
        [
            Poly(true, null, 0.25, (0, 0), (-2.9, 5), (2.9, 5)),
            Text(3.9, 1, "{level.name} {level.elevation}", 2.5, align: "start"),
        ])),
        new(Prefix + "level-half", "Level marker (half black)", Def("level", "A triangle one half black, as the Romanian standard has it.",
        [
            Poly(true, "byInstance", 0, (0, 0), (-2.9, 5), (0, 5)),
            Poly(true, null, 0.25, (0, 0), (-2.9, 5), (2.9, 5)),
            Line(0, 0, 0, 5),
            Text(3.9, 1, "{level.name} {level.elevation}", 2.5, align: "start"),
        ])),
        new(Prefix + "grid-circle", "Grid bubble (circle)", Def("grid", "A circle with the axis' label, its insertion point where the axis ends.",
        [
            Circle(0, 5, 5),
            Text(0, 5, "{grid.label}", 3.5, valign: "middle", bold: true),
        ])),
        new(Prefix + "grid-square", "Grid bubble (square)", Def("grid", "A square with the axis' label.",
        [
            new JsonObject { ["type"] = "rect", ["x"] = -5, ["y"] = 0, ["w"] = 10, ["h"] = 10, ["stroke"] = 0.25 },
            Text(0, 5, "{grid.label}", 3.5, valign: "middle", bold: true),
        ])),
    ];

    public static Builtin? Find(string? id) => All.FirstOrDefault(b => b.Id == id);
}
