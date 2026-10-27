using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A door's or window's marker in plan (a tag whose type is shaped
/// "opening", from the drafting standards): a line across the wall through
/// the opening's middle, perpendicular to the wall's axis, reaching past both
/// faces; at its exterior end the type's template (the mark, by default), at
/// its interior end its inner template (width/height, and a window's sill
/// height). Texts run along the wall, read upright, each line further out.
/// Drawn from the model each time, so the marker follows the opening, its
/// size and a flipped side.
/// </summary>
public static class OpeningMarkers
{
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.Kind != "plan") yield break;
        var m = c.Model;
        foreach (var t in m.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "TAG" && x.Str("viewId") == c.View.Id))
        {
            if (m.Get(t.Str("element") ?? "") is not { Class: IfcClass.Door or IfcClass.Window } fill) continue;
            if (Standards.TagTypeOf(c.Book, t.Str("tagType")) is not { Shape: "opening" } type) continue;
            if (Openings.OpeningOf(m, fill.Id) is not { } opening || Openings.WallOf(m, opening.Id) is not { } wall) continue;
            if (Mark(c, t, fill, wall, Openings.Place(m, opening), type) is { } mark) yield return mark;
        }
    }

    static ViewMark? Mark(ViewContext c, Entity tag, Entity fill, Entity wall, Openings.Placed p, TagType type)
    {
        var f = Openings.FrameOf(c.Model, wall, p.Along);
        var sy = fill.Bool("flipSide") ? -1 : 1;
        // The exterior is the wall's right, its left when the side is flipped (as the body has it).
        var (yExt, yInt) = sy > 0 ? (f.YMin, f.YMax) : (f.YMax, f.YMin);
        Vec2 P(double y) => c.Frame.Map(f.At(p.Along, y, 0));
        var (exterior, interior) = (P(yExt), P(yInt));
        if ((exterior - interior).Length < 1e-9) return null;
        var outward = (exterior - interior).Normalized();
        var reach = type.Reach * c.Mm;
        var (a, b) = (interior - outward * reach, exterior + outward * reach);

        var style = Standards.TextOf(c.Book, tag.Str("textStyle") ?? type.TextStyle);
        var color = style.Color ?? "#000000";
        var mark = new ViewMark(tag.Id);
        mark.Line(a, b, "TAG", Pens.Default.Hatch, color);

        var values = new Dictionary<string, string>
        {
            ["width"] = TagText.Length(p.Width, type.Unit, c.Book.Units),
            ["height"] = TagText.Length(p.Height, type.Unit, c.Book.Units),
            ["sill"] = TagText.Length(p.Sill, type.Unit, c.Book.Units),
        };
        // Along the wall, upright: the angle of the wall folded to (−90°, 90°].
        var along = new Vec2(-outward.Y, outward.X);
        var angle = Math.Atan2(along.Y, along.X) * 180 / Math.PI;
        if (angle > 90 + 1e-9) angle -= 180;
        else if (angle <= -90 + 1e-9) angle += 180;
        var h = style.Height * c.Mm;
        void Write(Vec2 end, Vec2 away, IReadOnlyList<string> lines)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                var at = end + away * (h * (0.9 + 1.4 * i));
                mark.Text(new PenText(at, lines[i], style.Height, angle, "middle", "middle", style.Bold, style.Font), color);
            }
        }
        Write(b, outward, TagText.Lines(type.Template, c.Model, fill, values));
        Write(a, outward * -1, TagText.Lines(type.Inner, c.Model, fill, values));
        mark.Snaps.Add(a);
        mark.Snaps.Add(b);
        return mark;
    }

    /// <summary>
    /// Markers for every door and window of a plan's storey not yet tagged in
    /// it: each with the type named for its kind, else the standards' type for
    /// its class. Returns how many were made.
    /// </summary>
    public static int TagAll(ICommandContext ctx, Entity view, string? doorType, string? windowType)
    {
        var m = ctx.Model;
        var book = DraftingResources.Book(m);
        var storey = view.Str("storeyId");
        var tagged = m.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "TAG" && x.Str("viewId") == view.Id).Select(x => x.Str("element")).ToHashSet();
        var made = 0;
        foreach (var fill in m.OfClass(IfcClass.Door).Concat(m.OfClass(IfcClass.Window)).ToList())
        {
            if (tagged.Contains(fill.Id) || Openings.OpeningOf(m, fill.Id) is not { } opening || Openings.WallOf(m, opening.Id) is not { } wall) continue;
            if (Spatial.StoreyOf(m, wall.Id)?.Id != storey) continue;
            var named = fill.Class == IfcClass.Door ? doorType : windowType;
            var type = Standards.TagTypeOf(book, named, fill.Class) ?? (named != null ? throw new CommandException("There is no such tag type.") : null);
            if (type == null) continue;
            var p = Openings.Place(m, opening);
            var centre = Openings.FrameOf(m, wall, p.Along).At(p.Along, 0, 0);
            ctx.Add(new Entity(ctx.NewId(), IfcClass.Annotation, $"Tag {TagText.Mark(m, fill)}", new JsonObject
            {
                ["viewId"] = view.Id, ["element"] = fill.Id, ["field"] = "mark", ["tagType"] = type.Id, ["at"] = new JsonArray(centre.X, centre.Y),
            }, "TAG"));
            made++;
        }
        return made;
    }
}
