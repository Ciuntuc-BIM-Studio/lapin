using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The marks views draw, each a slot a symbol can take (docs/plan-user-extensibility.md §3.3): a section's head, an elevation's
/// mark, a level's marker, a grid axis' bubble, a site plan's north arrow, a callout's head, a view's title on a sheet. A slot
/// names its symbol on the mark itself (<c>symbolSlots</c> on the section, the elevation, the grid, the inset, the view), else on
/// the project; none: Lapin's own drawing, exactly as before. A slot's symbol is laid with its +y the way the mark points (a
/// section's look, an elevation's look, an axis' outward end), and reads the mark's tokens: <c>{mark.name}</c>, <c>{mark.number}</c>
/// (its number on the sheet it is placed on), <c>{mark.sheet}</c> (that sheet's number), <c>{mark.scale}</c>; a level's
/// <c>{level.name}</c> and <c>{level.elevation}</c>; an axis' <c>{grid.label}</c>. Lapin's own symbols with attributes
/// <c>number</c>, <c>sheet</c> and <c>name</c> read the mark's.
/// </summary>
public static class SymbolSlots
{
    public const string Key = "symbolSlots";

    public sealed record Slot(string Id, string Label, string Owner, string Category);

    public static readonly IReadOnlyList<Slot> All =
    [
        new("sectionHead", "Section head", "section", "section"),
        new("elevationHead", "Elevation mark", "elevation", "elevation"),
        new("levelHead", "Level marker", "storey", "level"),
        new("gridBubble", "Grid bubble", "grid", "grid"),
        new("northArrow", "North arrow (site plans)", "view", "north"),
        new("calloutHead", "Callout head", "inset", "callout"),
        new("viewTitle", "View title (on sheets)", "view", "annotation"),
    ];

    public static bool Known(string slot) => All.Any(s => s.Id == slot);

    /// <summary>The symbol a slot draws for <paramref name="owner"/>: its own, else the project's; null (or one gone): Lapin's own drawing.</summary>
    public static string? SymbolFor(IModelView m, string slot, Entity? owner = null)
    {
        foreach (var e in new[] { owner, Spatial.Project(m) })
            if ((string?)e?.Attrs[Key]?[slot] is { Length: > 0 } id && Symbols.DefOf(m, id) != null) return id;
        return null;
    }

    /// <summary>The sheet a view is placed on (its number) and its number there (its place among the sheet's views, 1, 2…); none: empty.</summary>
    public static (string Sheet, string Number) PlacedOn(IModelView m, string viewId)
    {
        foreach (var sheet in m.OfClass(Sheets.Class).OrderBy(s => s.Str("number") ?? "", StringComparer.Ordinal))
        {
            var vps = Sheets.Viewports(sheet);
            var i = vps.FindIndex(v => v.ViewId == viewId);
            if (i >= 0) return (sheet.Str("number") ?? "", m.Get(viewId)?.Str("detailNumber") is { Length: > 0 } own ? own : (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return ("", m.Get(viewId)?.Str("detailNumber") ?? "");
    }

    /// <summary>A mark's tokens: a view's (a section, an elevation, an inset's view) name, number, sheet and scale, and its attributes' names for Lapin's own symbols.</summary>
    public static Func<string, (bool Known, string? Value)> MarkTokens(IModelView m, Entity? markedView, Func<string, (bool Known, string? Value)>? more = null)
    {
        var (sheet, number) = markedView != null ? PlacedOn(m, markedView.Id) : ("", "");
        return key =>
        {
            if (more?.Invoke(key) is { Known: true } r) return r;
            return key switch
            {
                "mark.name" or "attr.name" => (true, markedView?.Name ?? ""),
                "mark.number" or "attr.number" => (true, number),
                "mark.sheet" or "attr.sheet" => (true, sheet),
                "mark.scale" => (true, markedView != null ? $"1:{markedView.NumOrNull("scale") ?? 100:0}" : ""),
                _ => (false, null),
            };
        };
    }

    /// <summary>
    /// A slot's symbol drawn into <paramref name="mk"/> at <paramref name="at"/>, its +y along <paramref name="up"/> (a unit vector in the
    /// view), turned over across its x when <paramref name="flipX"/>, its size × <paramref name="size"/> (the mark's size against
    /// the default one); an annotation symbol's mm on paper, a model one's the building's.
    /// </summary>
    public static void Draw(ViewContext c, ViewMark mk, string symbolId, Vec2 at, Vec2 up, bool flipX, double size, string layer, string color, Func<string, (bool Known, string? Value)> tokens)
    {
        if (Symbols.DefOf(c.Model, symbolId) is not { } def) return;
        var model = def.Scale == "model";
        var unit = (model ? 0.001 : c.Mm) * size;
        // +y along up: the symbol's +x is up turned a quarter clockwise.
        var angle = Math.Atan2(up.Y, up.X) - Math.PI / 2;
        var x = new SymbolXf(at, angle, unit, model ? size * 0.001 / c.Mm : size, flipX);
        var sd = SymbolRender.Render(c.Model, def, Symbols.Resolver(c.Model, def, null, c.View, at, tokens), unit);
        SymbolRender.Into(mk, sd, x, layer, color, c.Mm, DraftingResources.Patterns(c.Model));
    }

    /// <summary>A slot's symbol on a sheet's paper (mm, y down) at <paramref name="at"/>, upright.</summary>
    public static List<Pen> SheetPens(IQueryContext q, string symbolId, Vec2 at, Entity? view, Func<string, (bool Known, string? Value)> tokens)
    {
        var m = q.Model;
        if (Symbols.DefOf(m, symbolId) is not { } def) return [];
        var sd = SymbolRender.Render(m, def, Symbols.Resolver(m, def, null, view, at, tokens), 0.001);
        return SymbolRender.ToPens(sd, at, 0, 1, false, false, "#000000", DraftingResources.Patterns(m));
    }

    public static void Register(IModuleRegistry r)
    {
        // The slots, and what the project and a mark name for each.
        r.Query("symbols.slots", (q, a) =>
        {
            var m = q.Model;
            var owner = m.Get((string?)a["ownerId"] ?? "");
            return new JsonObject
            {
                ["slots"] = new JsonArray(All.Select(s => (JsonNode)new JsonObject
                {
                    ["id"] = s.Id, ["label"] = s.Label, ["owner"] = s.Owner, ["category"] = s.Category,
                    ["project"] = (string?)Spatial.Project(m)?.Attrs[Key]?[s.Id],
                    ["own"] = (string?)owner?.Attrs[Key]?[s.Id],
                }).ToArray()),
            };
        });

        // A slot's symbol set, one undo step: on the project ({slot, symbolId}), or on marks of their own ({slot, symbolId, ownerIds});
        // no symbol (null or empty): back to the project's, or to Lapin's own drawing.
        r.Command("symbols.setSlot", "Marker symbol", (ctx, a) =>
        {
            var m = ctx.Model;
            var slot = (string?)a["slot"] is { } s && Known(s) ? s : throw new CommandException($"A slot is one of {string.Join(", ", All.Select(x => x.Id))}.");
            var symbol = (string?)a["symbolId"] is { Length: > 0 } sid ? sid : null;
            if (symbol != null && Symbols.DefOf(m, symbol) == null) throw new CommandException("There is no such symbol.");
            var owners = a["ownerIds"] is JsonArray oa ? oa.Select(x => m.Get((string?)x ?? "") ?? throw new CommandException("A mark chosen is gone.")).ToList()
                : [Spatial.Project(m) ?? throw new CommandException("The model has no project.")];
            foreach (var e in owners)
            {
                var attrs = e.CloneAttrs();
                var slots = attrs[Key] as JsonObject ?? new JsonObject();
                slots = (JsonObject)slots.DeepClone();
                if (symbol == null) slots.Remove(slot); else slots[slot] = symbol;
                if (slots.Count > 0) attrs[Key] = slots; else attrs.Remove(Key);
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });
    }
}
