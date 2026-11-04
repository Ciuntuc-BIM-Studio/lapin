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
/// height). Texts run along the wall (or along the line, as the tag type
/// says), read upright, each line further out. Drawn from the model each
/// time, so the marker follows the opening, its size and a flipped side.
/// Each part may be edited on screen, per marker and so per view (decision
/// 292, <see cref="LabelParts"/>): a text line moved or turned (o0, o1… the
/// outer lines, i0… the inner ones), the line's interior (a) or exterior (b)
/// end drawn out or pulled in; kept in the marker's directions, so they follow
/// the door when it moves, turns or flips.
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
        // The marker's own directions: along the wall (u) and out of it (v); its parts' offsets are kept in them (decision 292).
        var along = new Vec2(-outward.Y, outward.X);
        var parts = tag.Attrs["parts"] as JsonObject;
        var mm = c.Mm;
        var reach = type.Reach * mm;
        // Each end drawn out or pulled in on its own, never past the wall's middle.
        var half = (exterior - interior).Length / 2;
        double Reach(string key) => Math.Max(reach + (LabelParts.Of(parts, key).Ext ?? 0) * mm, -half + 0.5 * mm);
        var (a0, b0) = (interior - outward * reach, exterior + outward * reach);
        var (a, b) = (interior - outward * Reach("a"), exterior + outward * Reach("b"));

        var style = Standards.TextOf(c.Book, tag.Str("textStyle") ?? type.TextStyle);
        var color = style.Color ?? "#000000";
        // What it marks (of): a click on the door or window where the marker's line crosses it takes the element.
        var mark = new ViewMark(tag.Id);
        mark.Line(a, b, "TAG", Pens.Default.Hatch, color);

        var values = new Dictionary<string, string>
        {
            ["width"] = TagText.Length(p.Width, type.Unit, c.Book.Units),
            ["height"] = TagText.Length(p.Height, type.Unit, c.Book.Units),
            ["sill"] = TagText.Length(p.Sill, type.Unit, c.Book.Units),
        };
        var h = style.Height * mm;
        var data = new JsonArray();
        data.Add((JsonNode)LabelParts.EndData("a", a, a0, outward * -1, mm, LabelParts.Of(parts, "a")));
        data.Add((JsonNode)LabelParts.EndData("b", b, b0, outward, mm, LabelParts.Of(parts, "b")));
        // Across the line (along the wall) by default, or along it, as the tag type says; upright either way.
        var alongLine = type.TextOrientation == "along";
        void Write(string prefix, Vec2 end, Vec2 away, IReadOnlyList<string> lines)
        {
            var angle0 = LabelParts.Upright(LabelParts.AngleOf(alongLine ? away : along));
            // Along the line, a text starts at the line's end and runs away from it: its start or its end, as it reads.
            var anchor = !alongLine ? "middle" : Vec2.Dot(LabelParts.Dir(angle0), away) > 0 ? "start" : "end";
            var up = new Vec2(-LabelParts.Dir(angle0).Y, LabelParts.Dir(angle0).X);
            for (var i = 0; i < lines.Count; i++)
            {
                var key = prefix + i;
                var part = LabelParts.Of(parts, key);
                var at0 = alongLine
                    ? end + away * (h * 0.6) + up * (h * 1.4 * ((lines.Count - 1) / 2.0 - i))
                    : end + away * (h * (0.9 + 1.4 * i));
                var at = LabelParts.Moved(at0, part, along, outward, mm);
                var angle = part.Rot is { } r ? LabelParts.Upright(angle0 + r) : angle0;
                var t = new PenText(at, lines[i], style.Height, angle, anchor, "middle", style.Bold, style.Font, style.Italic);
                mark.Text(t, color);
                // The turning grip a little past the text's far end.
                var box = LabelParts.BoxOf(t, mm);
                var rotAt = box.C + LabelParts.Dir(angle) * (box.Hw + h);
                data.Add((JsonNode)LabelParts.TextData(key, at, at0, along, outward, mm, angle, angle0, rotAt, part, box));
            }
        }
        Write("o", b, outward, TagText.Lines(type.Template, c.Model, fill, values, c.Book.Units));
        Write("i", a, outward * -1, TagText.Lines(type.Inner, c.Model, fill, values, c.Book.Units));
        mark.Data = new JsonObject { ["of"] = fill.Id, ["marker"] = true, ["parts"] = data };
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
