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
            // Its type: chosen for it, else the first tag rule of its class that holds, else its own (issue #72).
            if (TagRules.TypeOf(m, c.Book, t, fill) is not { Shape: "opening" } type) continue;
            if (Openings.OpeningOf(m, fill.Id) is not { } opening || Openings.WallOf(m, opening.Id) is not { } wall) continue;
            if (Mark(c, t, fill, wall, Openings.Place(m, opening), type) is { } mark) yield return TagRules.Off(t) ? TagRules.Ghost(mark, c.Mm) : mark;
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
        // A bubble marker (forum topic 205): out of the frame's middle across the wall (moved by the type's offset, + outwards)
        // to its bubble, its interior end at the origin or past the interior face; its ends drawn out or pulled in as the other's.
        var origin = (exterior + interior) * 0.5 + outward * ((p.Spec?.Frame.Offset ?? 0) + (type.LineOffset ?? 0));
        if (type.Bubbled)
        {
            (a0, b0) = (type.LineInside is { } inside ? interior - outward * inside : origin, origin + outward * type.LineLengthOrDefault);
            (a, b) = (a0 - outward * ((LabelParts.Of(parts, "a").Ext ?? 0) * mm), b0 + outward * ((LabelParts.Of(parts, "b").Ext ?? 0) * mm));
        }

        var style = Standards.TextOf(c.Book, tag.Str("textStyle") ?? type.TextStyle);
        var color = style.Color ?? "#000000";
        // What it marks (of): a click on the door or window where the marker's line crosses it takes the element.
        var mark = new ViewMark(tag.Id);
        mark.Line(a, b, "TAG", Pens.Default.Hatch, color);

        // Its sizes in the tag type's unit (forum topic 204: the clear opening and the wall opening beside {width} and {height}).
        var values = Sizes(c.Model, p, wall).ToDictionary(kv => kv.Key, kv => TagText.Length(kv.Value, type.Unit, c.Book.Units), StringComparer.OrdinalIgnoreCase);
        foreach (var key in SizeKeys) values.TryAdd(key, "");
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
        IReadOnlyList<string> Lines(string? template) => string.IsNullOrEmpty(template) ? [] : TagText.Lines(template, c.Model, fill, values, c.Book.Units);
        if (type.Bubbled)
        {
            // The bubble with the mark at the line's outer end, or the type's symbol there; the texts over and under the line
            // along it, the one at the origin and the one at the interior end as the type lays its texts (across the line by default).
            if (type.Symbol is not { } bubbleSymbol || !Symbol(c, mark, data, bubbleSymbol, fill, values, b, outward, along, alongLine, parts, color))
                Bubble(mark, data, b, outward, along, alongLine, Lines(type.Written), type.BubbleSizeOrDefault / 2, style, color, parts, mm);
            var (tFace, tEnd) = (Vec2.Dot(exterior - origin, outward), Vec2.Dot(b - origin, outward));
            var tStart = tFace > 0 && tFace < tEnd - h ? tFace : 0;
            var middle = origin + outward * ((tStart + tEnd) / 2);
            Beside("ab", middle, outward, along, Lines(type.Above), 1, style, color, mark, data, parts, mm);
            Beside("be", middle, outward, along, Lines(type.Below), -1, style, color, mark, data, parts, mm);
            Write("c", origin - outward * (type.CentreOffset ?? 0), outward * -1, Lines(type.Centre));
            Write("i", a - outward * (type.InnerOffset ?? 0), outward * -1, Lines(type.Inner));
        }
        else
        {
            // Its outer text, or the type's symbol there (issue #72), its texts reading the door or window as the template does.
            if (type.Symbol is not { } symbolId || !Symbol(c, mark, data, symbolId, fill, values, b, outward, along, alongLine, parts, color))
                Write("o", b, outward, TagText.Lines(type.Written, c.Model, fill, values, c.Book.Units));
            Write("i", a, outward * -1, TagText.Lines(type.Inner, c.Model, fill, values, c.Book.Units));
        }
        mark.Data = new JsonObject { ["of"] = fill.Id, ["marker"] = true, ["parts"] = data };
        mark.Snaps.Add(a);
        mark.Snaps.Add(b);
        return mark;
    }

    /// <summary>
    /// A bubble marker's bubble (forum topic 205): a circle of <paramref name="r"/> (model metres) just past the line's outer end
    /// <paramref name="b"/>, its lines (the mark) in its middle, across the line (along the wall) or along it as the type lays its
    /// texts, upright; moved and turned on screen as one part, o0. No circle when <paramref name="r"/> is 0: the mark alone.
    /// </summary>
    static void Bubble(ViewMark mark, JsonArray data, Vec2 b, Vec2 outward, Vec2 along, bool alongLine, IReadOnlyList<string> lines, double r,
        TextStyle style, string color, JsonObject? parts, double mm)
    {
        var h = style.Height * mm;
        var part = LabelParts.Of(parts, "o0");
        var angle0 = LabelParts.Upright(LabelParts.AngleOf(alongLine ? outward : along));
        var angle = part.Rot is { } rot ? LabelParts.Upright(angle0 + rot) : angle0;
        var widest = lines.Count == 0 ? h : lines.Max(l => LabelParts.Width(l, h));
        var reach = r > 0 ? r : Math.Max(widest * Math.Abs(Vec2.Dot(LabelParts.Dir(angle0), outward)), h * 1.4 * Math.Max(1, lines.Count)) / 2 + 0.9 * mm;
        var centre0 = b + outward * reach;
        var centre = LabelParts.Moved(centre0, part, along, outward, mm);
        if (r > 0) mark.Polyline(ViewMark.Circle(centre, r), true, "TAG", Pens.Default.Hatch, color);
        var (u, up) = (LabelParts.Dir(angle), new Vec2(-LabelParts.Dir(angle).Y, LabelParts.Dir(angle).X));
        for (var i = 0; i < lines.Count; i++)
            mark.Text(new PenText(centre + up * (h * 1.4 * ((lines.Count - 1) / 2.0 - i)), lines[i], style.Height, angle, "middle", "middle", style.Bold, style.Font, style.Italic), color);
        var (hw, hh) = r > 0 ? (r, r) : (widest / 2, h * 0.7 * Math.Max(1, lines.Count));
        data.Add((JsonNode)LabelParts.TextData("o0", centre, centre0, along, outward, mm, angle, angle0, centre + u * (hw + 2.5 * mm), part, (centre, hw, hh, angle)));
    }

    /// <summary>
    /// A bubble marker's texts beside its line (forum topic 205): along it, upright, centred on <paramref name="middle"/>, each line
    /// further from it, over the line (<paramref name="side"/> 1) or under it (-1) as it reads, whichever way the door turns.
    /// </summary>
    static void Beside(string prefix, Vec2 middle, Vec2 outward, Vec2 along, IReadOnlyList<string> lines, int side, TextStyle style, string color,
        ViewMark mark, JsonArray data, JsonObject? parts, double mm)
    {
        var h = style.Height * mm;
        var angle0 = LabelParts.Upright(LabelParts.AngleOf(outward));
        var up = new Vec2(-LabelParts.Dir(angle0).Y, LabelParts.Dir(angle0).X);
        for (var i = 0; i < lines.Count; i++)
        {
            var key = prefix + i;
            var part = LabelParts.Of(parts, key);
            var at0 = middle + up * (side * h * (0.9 + 1.4 * i));
            var at = LabelParts.Moved(at0, part, along, outward, mm);
            var angle = part.Rot is { } r ? LabelParts.Upright(angle0 + r) : angle0;
            var t = new PenText(at, lines[i], style.Height, angle, "middle", "middle", style.Bold, style.Font, style.Italic);
            mark.Text(t, color);
            var box = LabelParts.BoxOf(t, mm);
            data.Add((JsonNode)LabelParts.TextData(key, at, at0, along, outward, mm, angle, angle0, box.C + LabelParts.Dir(angle) * (box.Hw + h), part, box));
        }
    }

    /// <summary>
    /// A marker's symbol (issue #72) beyond its line's exterior end <paramref name="b"/>, clear of it: turned as its texts would
    /// lie (along the wall, or along the line), upright; its part o0 moved and turned on screen as a text line is. False when the
    /// symbol is gone (the outer text is drawn instead).
    /// </summary>
    static bool Symbol(ViewContext c, ViewMark mark, JsonArray data, string symbolId, Entity fill, IReadOnlyDictionary<string, string> values,
        Vec2 b, Vec2 outward, Vec2 along, bool alongLine, JsonObject? parts, string color)
    {
        var mm = c.Mm;
        var part = LabelParts.Of(parts, "o0");
        var angle0 = LabelParts.Upright(LabelParts.AngleOf(alongLine ? outward : along));
        var angle = part.Rot is { } r ? LabelParts.Upright(angle0 + r) : angle0;
        var rad = angle * Math.PI / 180;
        if (TagSymbols.Lay(c, symbolId, b, rad, fill, values) is not { } laid) return false;
        // Its box about its own origin, turned: how far it reaches along the line's outward direction, so it stands clear of the end.
        var (mn, mx) = laid.Drawing.Bounds();
        var u = laid.Xf.Unit;
        var (cx, cy, hw, hh) = ((mn.X + mx.X) / 2 * u, (mn.Y + mx.Y) / 2 * u, Math.Max((mx.X - mn.X) / 2 * u, mm), Math.Max((mx.Y - mn.Y) / 2 * u, mm));
        var (cos, sin) = (Math.Cos(rad), Math.Sin(rad));
        var offset = new Vec2(cx * cos - cy * sin, cx * sin + cy * cos);
        var (ux, vy) = (LabelParts.Dir(angle), new Vec2(-Math.Sin(rad), Math.Cos(rad)));
        var reach = hw * Math.Abs(Vec2.Dot(ux, outward)) + hh * Math.Abs(Vec2.Dot(vy, outward));
        var centre0 = b + outward * (reach + 0.9 * mm);
        var centre = LabelParts.Moved(centre0, part, along, outward, mm);
        var at = centre - offset;
        var moved = (laid.Drawing, laid.Xf with { At = at });
        TagSymbols.Into(c, mark, moved, at, rad, "TAG", color);
        var rotAt = centre + ux * (hw + 2.5 * mm);
        data.Add((JsonNode)LabelParts.TextData("o0", centre, centre0, along, outward, mm, angle, angle0, rotAt, part, (centre, hw, hh, angle)));
        return true;
    }

    /// <summary>The sizes a door's or window's marker writes, its symbol's texts and its tag rules read (<see cref="Sizes"/>).</summary>
    public static readonly string[] SizeKeys = ["width", "height", "sill", "openingWidth", "openingHeight", "clearWidth", "clearHeight", "sillFinished", "frameWidth", "frameDepth"];

    /// <summary>
    /// A door's or window's sizes, in metres, by their tokens (forum topic 204):
    /// <list type="bullet">
    /// <item><c>width</c>, <c>height</c>: its own size (its type's, or its own), as before;</item>
    /// <item><c>openingWidth</c>, <c>openingHeight</c>: the wall opening, the hole in the wall it fills: its size, or the hole an
    /// imported file cuts round it where that differs;</item>
    /// <item><c>clearWidth</c>, <c>clearHeight</c>: the clear opening inside its frame (<see cref="Joinery.ClearOpening"/>, a door's
    /// threshold taken off unless the drafting standards say not, <see cref="Joinery.ClearHeightLessThreshold"/>); none for
    /// one without a configuration (nothing says where its frame is);</item>
    /// <item><c>sill</c>: its sill over what it is measured from (forum topic 192), as before; <c>sillFinished</c>: over the wall's
    /// finished floor, whatever it is measured from (the structural level when the wall has none);</item>
    /// <item><c>frameWidth</c>, <c>frameDepth</c> (forum topic 204, LVK): its frame's face width (what a jamb or the head takes off
    /// the opening) and its depth across the wall; frameless glass has no face (0) and is as deep as its glass. None for one
    /// without a configuration.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyDictionary<string, double> Sizes(IModelView m, Openings.Placed p, Entity? wall)
    {
        var o = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["width"] = p.Spec?.Width ?? p.Width,
            ["height"] = p.Spec?.Height ?? p.Height,
            ["sill"] = p.FillSill - p.SillBase,
            ["openingWidth"] = p.Width,
            ["openingHeight"] = p.Height,
            ["sillFinished"] = p.FillSill - (wall is { Class: IfcClass.Wall } ? OpeningSills.FinishedFloor(m, wall) : 0),
        };
        if (p.Spec is { } spec)
        {
            (o["clearWidth"], o["clearHeight"]) = Joinery.ClearOpening(spec, Joinery.ClearHeightLessThreshold(m));
            (o["frameWidth"], o["frameDepth"]) = spec.Frameless is { } fl ? (0, fl.Glass) : (spec.Frame.Width, spec.Frame.Depth);
        }
        return o;
    }

    /// <summary>A door's or window's sizes (<see cref="Sizes"/>); null when it stands in no opening.</summary>
    public static IReadOnlyDictionary<string, double>? SizesOf(IModelView m, Entity fill) =>
        Openings.OpeningOf(m, fill.Id) is { } opening ? Sizes(m, Openings.Place(m, opening), Openings.WallOf(m, opening.Id)) : null;

    /// <summary>
    /// What the tag rules' conditions read of a door or a window (issue #72), in metres: its sizes as its marker writes them
    /// (<see cref="Sizes"/>: <c>width</c>, <c>height</c>, <c>sill</c>, <c>clearWidth</c>, <c>openingWidth</c>…); null for any
    /// other name, or a door or window in no opening.
    /// </summary>
    public static double? Measure(IModelView m, Entity fill, string name)
    {
        var key = TagText.Squash(name);
        if (SizeKeys.FirstOrDefault(k => TagText.Squash(k) == key) is not { } known || SizesOf(m, fill) is not { } sizes) return null;
        return sizes.TryGetValue(known, out var v) ? v : null;
    }

    /// <summary>
    /// The sizes beyond a door's or window's own attributes as worked-out fields (forum topic 204), in millimetres as the sills'
    /// are: box tags, labels, schedules, the fields' pickers and formulas read them by the same tokens as the marker (which
    /// writes them in its tag type's unit). Its <c>width</c> and <c>height</c> stay what they were.
    /// </summary>
    public static void RegisterFields()
    {
        (string Key, string Label)[] fields =
        [
            ("clearWidth", "Clear opening width, inside the frame (mm)"), ("clearHeight", "Clear opening height, inside the frame (mm)"),
            ("openingWidth", "Wall opening width (mm)"), ("openingHeight", "Wall opening height (mm)"),
            ("sillFinished", "Sill over the finished floor (mm)"),
            ("frameWidth", "Frame face width (mm)"), ("frameDepth", "Frame depth, across the wall (mm)"),
        ];
        foreach (var cls in new[] { IfcClass.Door, IfcClass.Window })
            foreach (var (key, label) in fields)
            {
                var k = key;
                DerivedFields.Add(cls, new(k, label, (m, e) => SizesOf(m, e) is { } s && s.TryGetValue(k, out var v) ? DerivedFields.Number(Math.Round(v * 1000, 3)) : null));
            }
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
            // The type named, else the first tag rule of its class that holds (issue #72), else the standards' type for its class.
            var type = (named == null ? TagRules.RuleType(m, book, fill) : null) ?? Standards.TagTypeOf(book, named, fill.Class)
                ?? (named != null ? throw new CommandException("There is no such tag type.") : null);
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
