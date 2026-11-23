using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Mep;

/// <summary>One line, fill or text of a symbol drawn alone, in paper mm (y up, its insertion point at 0, 0).</summary>
public sealed record MepGlyph(IReadOnlyList<Vec2> Points, bool Closed, bool Heavy, bool Fill, string? Text = null, double Height = 0, double Angle = 0);

/// <summary>
/// A role's symbol drawn alone as a plan prints it (the symbol sets' pictures, a legend's rows): the project's set's drawing (or
/// another set's) for a node of the role's size at the origin, unturned; sized on paper at its 1:50 size, at the model's size at
/// the plan's scale.
/// </summary>
public static class MepGlyphs
{
    public static List<MepGlyph> Of(IModelView m, string key, string? setId, double scale)
    {
        var role = key == MepSymbols.Drop ? Roles.Require("rise") : Roles.Require(key);
        var choice = setId != null ? MepSymbols.For(m, key, setId) : MepSymbols.For(m, key);
        var items = Nodes.SymbolItems(m, role, choice, role.Size, (x, y) => new Vec2(x, y), key != MepSymbols.Drop, 0);
        var k = 1000 / (choice.Paper ? Nodes.SymbolScale : Math.Max(1, scale));
        return [.. items.Where(i => i.Points.Count > 0).Select(i => new MepGlyph([.. i.Points.Select(p => p * k)], i.Closed, i.Kind == "heavy", i.Kind == "fill", i.Text, i.TextHeight * k, i.TextAngle))];
    }

    /// <summary>The box of what a glyph draws (mm); a point's when it draws nothing.</summary>
    public static (Vec2 Min, Vec2 Max) Bounds(IReadOnlyList<MepGlyph> g)
    {
        var pts = g.SelectMany(x => x.Text != null ? [x.Points[0], x.Points[0] + new Vec2(x.Height * 0.7 * x.Text.Length, x.Height)] : x.Points).ToList();
        if (pts.Count == 0) return (new Vec2(0, 0), new Vec2(0, 0));
        return (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y)));
    }

    public static JsonArray ToJson(IReadOnlyList<MepGlyph> g) => new(g.Select(x =>
    {
        var o = new JsonObject
        {
            ["pts"] = new JsonArray(x.Points.SelectMany(p => new[] { Math.Round(p.X, 3), Math.Round(p.Y, 3) }).Select(v => (JsonNode)v).ToArray()),
            ["closed"] = x.Closed,
        };
        if (x.Heavy) o["heavy"] = true;
        if (x.Fill) o["fill"] = true;
        if (x.Text != null) { o["text"] = x.Text; o["h"] = Math.Round(x.Height, 3); o["angle"] = x.Angle; }
        return (JsonNode)o;
    }).ToArray());
}
