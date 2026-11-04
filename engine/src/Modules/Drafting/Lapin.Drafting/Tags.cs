using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// Tags (the old Lapin's TAG): what an element says of itself in a box at a
/// point of a view, with a leader to the element. It shows one field (its
/// mark, name, class, type, or any of its parameters: the user's own of the
/// element or its type, a standard set's {Pset_WallCommon.FireRating}, what
/// an imported element came with), or a template of its own, or its tag
/// type's template from the drafting standards. The text is read from the
/// model each time the view is drawn, so it follows the element; an element
/// gone leaves its tag red with a question mark. The leader (report #64) may
/// be switched off, bent at an elbow and pointed at another spot of the
/// element; dragged, the tag's head leaves the leader's end where it is. A
/// door's or window's marker (a type shaped "opening") is drawn in plans by
/// the architecture module, which knows the wall it sits in.
/// </summary>
public static class Tags
{
    /// <summary>The fields the command line offers by a word; any other parameter is typed or chosen by its name.</summary>
    public static readonly string[] Fields = TagText.Builtin;

    /// <summary>What a tag shows of an element.</summary>
    public static string? Text(IModelView m, Entity e, string field, string units = "mm") => TagText.Value(m, e, string.IsNullOrWhiteSpace(field) ? "mark" : field, null, units);

    /// <summary>A field as a tag keeps it: trimmed, its braces taken off ("{FireRating}" → "FireRating"); null when empty.</summary>
    public static string? CleanField(string? field) => field?.Trim().TrimStart('{').TrimEnd('}').Trim() is { Length: > 0 } f ? f : null;

    /// <summary>Whether a tag is a door's or window's marker, drawn by the architecture module in plans.</summary>
    public static bool IsOpeningMarker(TagType? type, Entity? e, string viewKind) =>
        viewKind == "plan" && type?.Shape == "opening" && e?.Class is IfcClass.Door or IfcClass.Window;

    /// <summary>Where a leader points: the middle of what the element draws on the view (its cut, else its lines).</summary>
    static Vec2? Anchor(ViewDrawing d, string element)
    {
        var pts = d.Cuts.Where(p => p.Element == element).SelectMany(p => p.Loops.SelectMany(l => l))
            .Concat(d.Lines.Where(l => l.Element == element).SelectMany(l => new[] { l.A, l.B })).ToList();
        return pts.Count == 0 ? null : new Vec2((pts.Min(p => p.X) + pts.Max(p => p.X)) / 2, (pts.Min(p => p.Y) + pts.Max(p => p.Y)) / 2);
    }

    static Vec2? Pt(JsonNode? n) => n is JsonArray a && a.Count >= 2 && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : null;
    static JsonArray J(Vec2 p) => new(p.X, p.Y);

    /// <summary>The lines a tag shows: its own template, else its type's, else its field; "?" when they come out empty or the element is gone.</summary>
    public static IReadOnlyList<string> LinesOf(IModelView m, Entity tag, Entity? e, TagType? type, string units)
    {
        if (e == null) return ["?"];
        var template = tag.Str("template") is { Length: > 0 } own ? own : type?.Template;
        var lines = template != null ? TagText.Lines(template, m, e, null, units) : [Text(m, e, tag.Str("field") ?? "mark", units) ?? ""];
        lines = lines.Where(l => l.Length > 0).ToList();
        return lines.Count > 0 ? lines : ["?"];
    }

    /// <summary>The point of the box's outline a leader towards <paramref name="toward"/> leaves from: the middle of the nearer side.</summary>
    static Vec2 Leaves(Vec2 at, double halfW, double halfH, Vec2 toward) =>
        Math.Abs(toward.X - at.X) / halfW > Math.Abs(toward.Y - at.Y) / halfH
            ? new Vec2(at.X + Math.Sign(toward.X - at.X) * halfW, at.Y)
            : new Vec2(at.X, at.Y + Math.Sign(toward.Y - at.Y) * halfH);

    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        foreach (var t in c.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "TAG" && x.Str("viewId") == c.View.Id))
        {
            var e = c.Model.Get(t.Str("element") ?? "");
            var type = Standards.TagTypeOf(c.Book, t.Str("tagType"));
            if (IsOpeningMarker(type, e, c.Kind)) continue;
            var p = Dimensions.ParamsOf(t);
            if (type != null && p["textStyle"] == null) p["textStyle"] = type.TextStyle;
            var style = Standards.ResolveText(c.Book, p);
            var at = Pt(t.Attrs["at"]) ?? default;
            var lines = LinesOf(c.Model, t, e, type, c.Book.Units);
            var color = e == null ? "#c0392b" : style.Color ?? "#000000";
            var h = style.Height * c.Mm;
            // One line: a box twice the text's height; more: each 1.5 heights apart.
            var halfH = (lines.Count * 1.5 * h + 0.5 * h) / 2;
            var halfW = (Math.Max(1, lines.Max(l => l.Length)) * h * 0.62 + h) / 2;
            var mk = new ViewMark(t.Id);
            Vec2[] box = [at + new Vec2(-halfW, -halfH), at + new Vec2(halfW, -halfH), at + new Vec2(halfW, halfH), at + new Vec2(-halfW, halfH)];
            mk.Polyline(box, true, "TAG", Pens.Default.Hatch, color);
            for (var i = 0; i < lines.Count; i++)
                mk.Text(new PenText(new Vec2(at.X, at.Y + halfH - 0.25 * h - 1.5 * h * (i + 0.5)), lines[i], style.Height, 0, "middle", "middle", style.Bold, style.Font, style.Italic), color);
            var data = new JsonObject { ["at"] = J(at), ["leader"] = t.Attrs["leader"] is not JsonValue lv || !lv.TryGetValue<bool>(out var on) || on };
            // The leader: to the spot it was pointed at, else the middle of what the element draws here; through its elbow.
            var end = Pt(t.Attrs["end"]) ?? (e != null ? Anchor(c.Drawing, e.Id) : null);
            var elbow = Pt(t.Attrs["elbow"]);
            var shaped = t.Attrs["end"] is JsonArray || elbow != null;
            if ((bool)data["leader"]! && end is { } target && (shaped || (target - at).Length > halfH * 1.5))
            {
                var from = Leaves(at, halfW, halfH, elbow ?? target);
                Vec2[] path = elbow is { } k ? [from, k, target] : [from, target];
                mk.Polyline(path, false, "TAG", Pens.Default.Hatch, color);
                mk.Polyline(ViewMark.Circle(target, h / 5, 8), true, "TAG", Pens.Default.Seen, color);
                data["from"] = J(from);
                data["end"] = J(target);
                if (elbow is { } kk) data["elbow"] = J(kk);
                mk.Snaps.Add(target);
            }
            mk.Data = data;
            mk.Snaps.Add(at);
            yield return mk;
        }
    }

    public static void Register(IModuleRegistry r)
    {
        // A tag of an element on a view: its mark (or name, class, type, any parameter), at a point.
        r.Command("addTag", "Tag", (ctx, a) =>
        {
            var viewId = (string?)a["viewId"] ?? throw new CommandException("A tag is put on a view.");
            if (Views.Find(ctx.Model, viewId) == null) throw new CommandException("There is no such view.");
            var e = ctx.Model.Get((string?)a["element"] ?? "") ?? throw new CommandException("Tag what? Pick an element.");
            if (e.Class == IfcClass.Annotation) throw new CommandException("Tags are for the model's elements.");
            var field = CleanField((string?)a["field"]);
            if (a["field"] != null && field == null) throw new CommandException("Which field does the tag show? A mark, a name, a class, a type or a parameter's name.");
            var at = a["at"] is JsonArray p && p.Count >= 2 ? new JsonArray(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0) : throw new CommandException("Where does the tag go?");
            // Its type: the one named, else (no field asked for) the standards' type for the element's class (a door's marker), else none.
            var book = DraftingResources.Book(ctx.Model);
            var typeId = (string?)a["tagType"];
            if (typeId != null && Standards.TagTypeOf(book, typeId) == null) throw new CommandException("There is no such tag type.");
            var type = typeId != null ? Standards.TagTypeOf(book, typeId) : field == null ? Standards.TagTypeOf(book, null, e.Class) : null;
            var attrs = new JsonObject { ["viewId"] = viewId, ["element"] = e.Id, ["field"] = field ?? "mark", ["at"] = at };
            if (type != null) attrs["tagType"] = type.Id;
            if (a["leader"] is JsonValue lv && lv.TryGetValue<bool>(out var leader) && !leader) attrs["leader"] = false;
            if (Pt(a["elbow"]) is { } elbow) attrs["elbow"] = J(elbow);
            var tag = new Entity(ctx.NewId(), IfcClass.Annotation, $"Tag {TagText.Mark(ctx.Model, e)}", attrs, "TAG");
            ctx.Add(tag);
            return new JsonObject { ["id"] = tag.Id };
        });

        // A tag changed (report #64): its head moved (at), its leader bent (elbow; null: straight again), pointed elsewhere (end;
        // null: back on the element's middle), switched on or off (leader); what it shows: a field, a template of its own (""
        // or null: none), a tag type (null: none, the field shows). Several tags take the same.
        r.Command("updateTag", "Change tag", (ctx, a) =>
        {
            var ids = Args.Ids(a).ToList();
            if (ids.Count == 0) throw new CommandException("Which tag?");
            var book = DraftingResources.Book(ctx.Model);
            foreach (var id in ids)
            {
                var t = ctx.Model.Get(id);
                if (t is not { PredefinedType: "TAG" }) throw new CommandException("That is not a tag.");
                var attrs = t.CloneAttrs();
                foreach (var key in new[] { "at", "elbow", "end" })
                {
                    if (!a.ContainsKey(key)) continue;
                    if (a[key] is null) { if (key == "at") throw new CommandException("A tag stays somewhere."); attrs.Remove(key); continue; }
                    attrs[key] = J(Pt(a[key]) ?? throw new CommandException("A point is two numbers."));
                }
                if (a.ContainsKey("leader"))
                {
                    if (a["leader"] is JsonValue lv && lv.TryGetValue<bool>(out var on)) { if (on) attrs.Remove("leader"); else attrs["leader"] = false; }
                    else throw new CommandException("A leader is on or off.");
                }
                if (a.ContainsKey("field"))
                {
                    attrs["field"] = CleanField((string?)a["field"]) ?? throw new CommandException("Which field does the tag show? A mark, a name, a class, a type or a parameter's name.");
                    // A field chosen is what the tag shows: no template over it.
                    if (!a.ContainsKey("template")) attrs.Remove("template");
                    if (!a.ContainsKey("tagType")) attrs.Remove("tagType");
                }
                if (a.ContainsKey("template"))
                {
                    if (((string?)a["template"])?.Trim() is { Length: > 0 } tpl) attrs["template"] = tpl; else attrs.Remove("template");
                }
                if (a.ContainsKey("tagType"))
                {
                    if ((string?)a["tagType"] is { Length: > 0 } typeId)
                    {
                        if (Standards.TagTypeOf(book, typeId) == null) throw new CommandException("There is no such tag type.");
                        attrs["tagType"] = typeId;
                    }
                    else attrs.Remove("tagType");
                }
                ctx.Replace(t.With(attrs: attrs));
            }
            return null;
        });

        // What a tag of this element can show: its fields, its and its type's parameters, the standard sets' properties for its class, an import's sets.
        r.Query("drafting.tagFields", (q, a) =>
        {
            var e = q.Model.Get((string?)a["element"] ?? "") ?? throw new CommandException("Which element?");
            return TagText.Fields(q.Model, e, DraftingResources.Book(q.Model).Units);
        });
    }
}
