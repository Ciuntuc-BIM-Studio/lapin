using System.Text.RegularExpressions;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A tag drawn by a symbol of the user's (decision 551, a tester's topic #37): a tag type (or the room stamp) naming a symbol is
/// drawn as that symbol at the tag's point, its texts' fields read from the tagged element as a template's are — a text slot
/// <c>{storey.code}.{Hall}.{Flat}.{number}</c>, <c>{name}</c>, <c>{area}</c>, <c>{Floor finish}</c>, formulas <c>{= …}</c> too — while the
/// symbol's own tokens (<c>{attr.…}</c>, <c>{view.scale}</c>, <c>{level.name}</c>, <c>{system.printDate}</c>) keep theirs. Read on every draw,
/// so a placed tag follows its element and everything the element reads.
/// </summary>
public static partial class TagSymbols
{
    /// <summary>The token sources a symbol answers itself; any other field is the tagged element's.</summary>
    static readonly string[] Own = ["attr", "symbol", "point", "view", "level", "system", "sheet"];

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex Field();

    static bool OwnToken(string key)
    {
        var dot = key.IndexOf('.');
        return dot > 0 && Own.Contains(key[..dot].Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>A symbol text's element fields filled ({number}, {storey.code}); its formulas and its own tokens left to live text.</summary>
    public static Func<string, string> Fields(IModelView m, Entity e, IReadOnlyDictionary<string, string>? extra, string units) =>
        raw => Field().Replace(raw, x =>
        {
            var key = x.Groups[1].Value.Trim();
            return key.StartsWith('=') || OwnToken(key) ? x.Value : TagText.Value(m, e, key, extra, units);
        });

    /// <summary>The tokens a symbol's formulas read: the element's fields, but for the symbol's own sources.</summary>
    public static Func<string, (bool Known, string? Value)> Tokens(IModelView m, Entity e, IReadOnlyDictionary<string, string>? extra, string units) =>
        key => OwnToken(key) ? (false, null) : (true, TagText.Value(m, e, key, extra, units));

    /// <summary>A symbol laid for an element at a point of a view: what it draws (its own mm) and how it lies; null when the symbol is gone.</summary>
    public static (SymbolDrawing Drawing, SymbolXf Xf)? Lay(ViewContext c, string symbolId, Vec2 at, double angle, Entity e, IReadOnlyDictionary<string, string>? extra = null)
    {
        if (Symbols.DefOf(c.Model, symbolId) is not { } def) return null;
        var model = def.Scale == "model";
        var unit = model ? 0.001 : c.Mm;
        var units = c.Book.Units;
        var sd = SymbolRender.Render(c.Model, def, Symbols.Resolver(c.Model, def, null, c.View, at, Tokens(c.Model, e, extra, units)), unit, null, Fields(c.Model, e, extra, units));
        return (sd, new SymbolXf(at, angle, unit, model ? 0.001 / c.Mm : 1));
    }

    /// <summary>
    /// A laid symbol drawn into a mark, with a box round it on the pick layer (what a click takes); its box in the view, unturned
    /// about <paramref name="at"/>: centre and half sizes.
    /// </summary>
    public static (Vec2 Centre, double HalfW, double HalfH) Into(ViewContext c, ViewMark mk, (SymbolDrawing Drawing, SymbolXf Xf) laid, Vec2 at, double angle, string layer, string color)
    {
        SymbolRender.Into(mk, laid.Drawing, laid.Xf, layer, color, c.Mm, DraftingResources.Patterns(c.Model));
        var (mn, mx) = laid.Drawing.Bounds();
        var u = laid.Xf.Unit;
        var centre = at + new Vec2((mn.X + mx.X) / 2, (mn.Y + mx.Y) / 2) * u;
        var (hw, hh) = (Math.Max((mx.X - mn.X) / 2 * u, c.Mm), Math.Max((mx.Y - mn.Y) / 2 * u, c.Mm));
        Vec2 Turned(Vec2 p) => angle == 0 ? p : at + new Vec2((p - at).X * Math.Cos(angle) - (p - at).Y * Math.Sin(angle), (p - at).X * Math.Sin(angle) + (p - at).Y * Math.Cos(angle));
        Vec2[] box = [new(centre.X - hw, centre.Y - hh), new(centre.X + hw, centre.Y - hh), new(centre.X + hw, centre.Y + hh), new(centre.X - hw, centre.Y + hh)];
        mk.Polyline(box.Select(Turned).ToArray(), true, "PICK", 0);
        return (centre, hw, hh);
    }
}
