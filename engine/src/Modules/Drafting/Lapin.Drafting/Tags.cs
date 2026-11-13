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
/// element; dragged, the tag's head leaves the leader's end where it is. By
/// default it is ArchiCAD's (decision 331, <see cref="TagLeaders"/>): an
/// oblique segment to a knee, then a shoulder under the text, no box. A
/// door's or window's marker (a type shaped "opening") is drawn in plans by
/// the architecture module, which knows the wall it sits in. A tag follows its
/// element when it moves, turns or is reshaped (report #93, <see cref="TagHold"/>).
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
    internal static Vec2? Anchor(ViewDrawing d, string element)
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

    /// <summary>What an elbow tag's mark tells the interface: its knee (its point), its text's middle, the side the text lies on, its leader's end.</summary>
    internal static JsonObject ElbowData(Vec2 knee, Vec2 head, int side, bool leader, Entity? e, Vec2? end)
    {
        var data = new JsonObject
        {
            ["at"] = J(knee), ["knee"] = J(knee), ["head"] = J(head), ["side"] = side, ["leader"] = leader, ["of"] = e?.Id, ["shape"] = TagLeaders.Elbow,
        };
        if (leader && end is { } p) data["end"] = J(p);
        return data;
    }

    /// <summary>
    /// A straight tag: its lines centred in a box at <paramref name="at"/>, its leader from the box's nearer side to
    /// <paramref name="end"/> (none: no leader) through a free <paramref name="elbow"/>; a leader not pointed or bent by
    /// hand is left out while the box stands on its end.
    /// </summary>
    internal static ViewMark Box(string? id, IReadOnlyList<string> lines, TextStyle style, double mm, Vec2 at, Vec2? end, Vec2? elbow, bool shaped, string color, double angle = 0)
    {
        var turn = new Turn(at, angle);
        var h = style.Height * mm;
        // One line: a box twice the text's height; more: each 1.5 heights apart.
        var halfH = (lines.Count * 1.5 * h + 0.5 * h) / 2;
        var halfW = (Math.Max(1, lines.Max(l => l.Length)) * h * 0.62 + h) / 2;
        var mk = new ViewMark(id);
        Vec2[] box = [at + new Vec2(-halfW, -halfH), at + new Vec2(halfW, -halfH), at + new Vec2(halfW, halfH), at + new Vec2(-halfW, halfH)];
        mk.Polyline(box.Select(turn.P).ToArray(), true, "TAG", Pens.Default.Hatch, color);
        for (var i = 0; i < lines.Count; i++)
            mk.Text(turn.Text(new Vec2(at.X, at.Y + halfH - 0.25 * h - 1.5 * h * (i + 0.5)), lines[i], style, "middle", "middle"), color);
        var data = new JsonObject { ["at"] = J(at), ["leader"] = end != null, ["shape"] = TagLeaders.Straight };
        if (end is { } target && (shaped || (target - at).Length > halfH * 1.5))
        {
            var from = turn.P(Leaves(at, halfW, halfH, turn.Inv(elbow ?? target)));
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
        return mk;
    }

    /// <summary>
    /// The direction an element draws along in a view (degrees, counter-clockwise, in the view's coordinates): a path
    /// element's axis in a plan, else its longest edge (a cut's outline, or a line it draws); null when it draws nothing.
    /// </summary>
    internal static double? DirectionOf(ViewContext c, Entity? e)
    {
        if (e == null) return null;
        if (c.Kind == "plan" && e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray && Dimensions.Frame(e) is { } f)
            return Math.Atan2(f.U.Y, f.U.X) * 180 / Math.PI;
        var edges = c.Drawing.Cuts.Where(p => p.Element == e.Id).SelectMany(p => p.Loops.SelectMany(l => l.Select((q, i) => (A: q, B: l[(i + 1) % l.Length]))))
            .Concat(c.Drawing.Lines.Where(l => l.Element == e.Id).Select(l => (l.A, l.B))).ToList();
        if (edges.Count == 0) return null;
        var (a, b) = edges.MaxBy(x => (x.B - x.A).Length);
        return (b - a).Length < 1e-9 ? null : Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI;
    }

    /// <summary>A tag's angle as drawn (degrees, upright, never upside down): its own <c>rotation</c>, added to the element's direction when it is aligned to it (report #129).</summary>
    internal static double AngleOf(ViewContext c, Entity tag, Entity? e)
    {
        var own = tag.NumOrNull("rotation") ?? 0;
        var align = tag.Attrs["align"] is JsonValue v && v.TryGetValue<bool>(out var on) && on;
        return Turn.Readable(own + (align ? DirectionOf(c, e) ?? 0 : 0));
    }

    /// <summary>What the interface needs of a turned tag: the angle drawn, whether it follows its element, and that direction (the rotate grip's zero).</summary>
    internal static void AngleData(JsonObject data, Entity tag, Entity? e, ViewContext c, Vec2 centre, double reach)
    {
        var angle = AngleOf(c, tag, e);
        data["angle"] = Math.Round(angle, 4);
        // The turning grip: over the text's middle, square to its baseline, clear of the block.
        data["centre"] = J(centre);
        data["turn"] = J(centre + new Turn(default, angle * Math.PI / 180).V(new Vec2(0, reach)));
        var align = tag.Attrs["align"] is JsonValue v && v.TryGetValue<bool>(out var on) && on;
        if (align) { data["align"] = true; data["base"] = Math.Round(DirectionOf(c, e) ?? 0, 4); }
    }

    /// <summary>
    /// A tag drawn by its type's symbol (decision 552): the symbol at its point, turned with it, a straight leader from the
    /// symbol's box to the element (bent at its elbow), left out while the box stands on its end unless pointed or bent by hand.
    /// </summary>
    static ViewMark SymbolTag(ViewContext c, Entity t, Entity e, (SymbolDrawing Drawing, SymbolXf Xf) laid, Vec2 at, double rad, string color, bool leaderOn, Vec2? end, Vec2? elbow)
    {
        var mk = new ViewMark(t.Id);
        var (centre, halfW, halfH) = TagSymbols.Into(c, mk, laid, at, rad, "TAG", color);
        var turn = new Turn(at, rad);
        var data = new JsonObject { ["at"] = J(at), ["leader"] = leaderOn, ["shape"] = TagLeaders.Straight, ["of"] = e.Id, ["symbol"] = true };
        var shaped = t.Attrs["end"] is JsonArray || elbow != null;
        if (leaderOn && end is { } target && (shaped || (turn.Inv(target) - centre).Length > Math.Max(halfW, halfH) * 1.2))
        {
            var from = turn.P(Leaves(centre, halfW, halfH, turn.Inv(elbow ?? target)));
            Vec2[] path = elbow is { } k ? [from, k, target] : [from, target];
            mk.Polyline(path, false, "TAG", Pens.Default.Hatch, color);
            mk.Polyline(ViewMark.Circle(target, c.Mm * 0.5, 8), true, "TAG", Pens.Default.Seen, color);
            data["from"] = J(from);
            data["end"] = J(target);
            if (elbow is { } kk) data["elbow"] = J(kk);
            mk.Snaps.Add(target);
        }
        mk.Data = data;
        AngleData(data, t, e, c, turn.P(centre), halfH + 1.5 * 2.5 * c.Mm);
        mk.Snaps.Add(at);
        return mk;
    }

    static string? LeaderShape(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && s is TagLeaders.Straight or TagLeaders.Elbow ? s : null;

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
            // Its points where its element has taken them since they were set (report #93).
            var follow = TagHold.Follow(c.Model, t, e, c.Frame.Right, c.Frame.Up, c.Kind == "plan", c.Geometry);
            Vec2? Followed(Vec2? p) => p is { } q && follow != null ? follow(q) : p;
            // A build-up tag (docs/plan-buildup-tags.md): the element's layers as a note, its leaders one or one per layer.
            if (type is { Shape: "buildup" }) { yield return BuildupTags.Mark(c, t, e, type, style, follow); continue; }
            var at = Followed(Pt(t.Attrs["at"])) ?? default;
            var angle = AngleOf(c, t, e);
            var rad = angle * Math.PI / 180;
            var lines = LinesOf(c.Model, t, e, type, c.Book.Units);
            var color = e == null ? "#c0392b" : style.Color ?? "#000000";
            var leaderOn = t.Attrs["leader"] is not JsonValue lv || !lv.TryGetValue<bool>(out var on) || on;
            // A tag type drawn by a symbol of the user's (decision 552): the symbol at the tag's point, its texts the element's fields.
            if (type?.Symbol is { } symbolId && e != null && TagSymbols.Lay(c, symbolId, at, rad, e) is { } laid)
            {
                yield return SymbolTag(c, t, e, laid, at, rad, color, leaderOn, Followed(Pt(t.Attrs["end"])) ?? Anchor(c.Drawing, e.Id), Followed(Pt(t.Attrs["elbow"])));
                continue;
            }
            // An elbow (decision 331): the tag's point is its knee, the text beside it on its shoulder, no box.
            if (TagLeaders.ShapeOf(t, type) == TagLeaders.Elbow)
            {
                var to = Followed(Pt(t.Attrs["end"])) ?? (e != null ? Anchor(c.Drawing, e.Id) : null);
                var (em, head, side) = TagLeaders.Plain(t.Id, lines, style, type, c.Mm, at, to, leaderOn, color, rad);
                em.Data = ElbowData(at, head, side, leaderOn, e, to);
                AngleData(em.Data, t, e, c, head, (lines.Count * TagLeaders.Pitch * style.Height * c.Mm + 2 * style.Height * c.Mm) / 2 + 1.5 * style.Height * c.Mm);
                if (leaderOn && to is { } p0) em.Snaps.Add(p0);
                em.Snaps.Add(at);
                yield return em;
                continue;
            }
            // The leader: to the spot it was pointed at, else the middle of what the element draws here; through its elbow.
            var end = Followed(Pt(t.Attrs["end"])) ?? (e != null ? Anchor(c.Drawing, e.Id) : null);
            var elbow = Followed(Pt(t.Attrs["elbow"]));
            var mk = Box(t.Id, lines, style, c.Mm, at, leaderOn ? end : null, elbow, t.Attrs["end"] is JsonArray || elbow != null, color, rad);
            mk.Data!["of"] = e?.Id;
            AngleData(mk.Data, t, e, c, at, (lines.Count * 1.5 * style.Height * c.Mm + 0.5 * style.Height * c.Mm) / 2 + 1.5 * style.Height * c.Mm);
            mk.Data["leader"] = leaderOn;
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
            if (a["leaderShape"] is not null) attrs["leaderShape"] = LeaderShape(a["leaderShape"]) ?? throw new CommandException("A leader is straight or an elbow.");
            if (Json.Number(a["rotation"]) is { } rot && Math.Abs(rot) > 1e-9) attrs["rotation"] = Math.Round(rot, 4);
            if (a["align"] is JsonValue al && al.TryGetValue<bool>(out var aligned) && aligned) attrs["align"] = true;
            attrs[TagLeaders.RevKey] = TagLeaders.Rev;
            // Where the element stands now: the tag follows it from there (report #93).
            TagHold.Hold(ctx.Model, attrs, e, Views.Find(ctx.Model, viewId) is { } v && TagHold.Axes(ctx.Model, v) is { Plan: true }, ctx.Query.Geometry);
            var tag = new Entity(ctx.NewId(), IfcClass.Annotation, $"Tag {TagText.Mark(ctx.Model, e)}", attrs, "TAG");
            ctx.Add(tag);
            return new JsonObject { ["id"] = tag.Id };
        });

        // A tag changed (report #64): its head moved (at: an elbow leader's knee), its leader bent (elbow; null: straight again),
        // pointed elsewhere (end; null: back on the element's middle), switched on or off (leader), its shape (leaderShape
        // straight or elbow, null: its type's; decision 331); what it shows: a field, a template of its own (""
        // or null: none), a tag type (null: none, the field shows). A door's or window's marker edited part by part (decision
        // 292): part (o0, o1… its outer lines, i0… its inner ones, a and b its line's ends) with offset ([along, across] paper
        // mm), rot (degrees) or ext (paper mm); reset: true (that part, or every part). Several tags take the same.
        r.Command("updateTag", "Change tag", (ctx, a) =>
        {
            var ids = Args.Ids(a).ToList();
            if (ids.Count == 0) throw new CommandException("Which tag?");
            var book = DraftingResources.Book(ctx.Model);
            foreach (var id in ids)
            {
                var t = ctx.Model.Get(id);
                if (t is not { PredefinedType: "TAG" }) throw new CommandException("That is not a tag.");
                // Its points first where its element has taken them (report #93): what the view shows, which the change is made on.
                var attrs = TagHold.Baked(ctx.Model, t, ctx.Query.Geometry);
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
                // Its own leader shape (decision 331; null: its type's). An elbow takes no free bend.
                if (a.ContainsKey("leaderShape"))
                {
                    if (a["leaderShape"] is null || a["leaderShape"] is JsonValue sv && sv.TryGetValue<string>(out var none) && none == "") attrs.Remove("leaderShape");
                    else attrs["leaderShape"] = LeaderShape(a["leaderShape"]) ?? throw new CommandException("A leader is straight or an elbow.");
                    if ((string?)attrs["leaderShape"] == TagLeaders.Elbow && !a.ContainsKey("elbow")) attrs.Remove("elbow");
                }
                // Its angle (report #129): turned by hand (degrees; 0 or null: upright), and aligned to its element's direction.
                if (a.ContainsKey("rotation"))
                {
                    if (a["rotation"] is null) attrs.Remove("rotation");
                    else if (Json.Number(a["rotation"]) is { } deg && double.IsFinite(deg)) { if (Math.Abs(deg) < 1e-9) attrs.Remove("rotation"); else attrs["rotation"] = Math.Round(deg, 4); }
                    else throw new CommandException("A rotation is an angle in degrees.");
                }
                if (a.ContainsKey("align"))
                {
                    if (a["align"] is null || a["align"] is JsonValue av && av.TryGetValue<bool>(out var al) && !al) attrs.Remove("align");
                    else if (a["align"] is JsonValue av2 && av2.TryGetValue<bool>(out _)) attrs["align"] = true;
                    else throw new CommandException("Align is on or off.");
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
                // A marker's part edited on screen (decision 292): part with offset, rot or ext; reset (a part's, or all).
                if (a.ContainsKey("part") || a.ContainsKey("reset"))
                {
                    if (LabelParts.Apply(attrs["parts"] as JsonObject, a) is { } parts) attrs["parts"] = parts; else attrs.Remove("parts");
                }
                // The element's frame again: a leader's end moved along a wall holds on there.
                TagHold.Hold(ctx.Model, attrs, ctx.Model.Get(t.Str("element") ?? ""), TagHold.InPlan(ctx.Model, t), ctx.Query.Geometry);
                ctx.Replace(t.With(attrs: attrs));
            }
            return null;
        });

        // A build-up tag type as it would write a build-up (docs/plan-buildup-tags.md), for its editor's preview: the type
        // given (a draft, read as the standards read it; else the one named by id), on an element, a type (a wall type's
        // layers) or the project's first layered wall type (else floor type); its texts laid out on paper (mm).
        r.Query("drafting.buildupPreview", (q, a) =>
        {
            var book = DraftingResources.Book(q.Model);
            var type = a["entry"] is JsonObject draft
                ? Standards.Read(new JsonObject { ["tagTypes"] = new JsonArray(draft.DeepClone()) }).TagTypes.FirstOrDefault()
                : Standards.TagTypeOf(book, (string?)a["tagType"] ?? Standards.DefaultBuildupTag.Id);
            type ??= Standards.DefaultBuildupTag;
            var sample = q.Model.Get((string?)a["element"] ?? (string?)a["type"] ?? "")
                ?? q.Model.OfClass(IfcClass.WallType).FirstOrDefault(x => BuildUps.OfType(q.Model, x) != null)
                ?? q.Model.OfClass(IfcClass.SlabType).FirstOrDefault(x => BuildUps.OfType(q.Model, x) != null);
            if (sample == null) return new JsonObject { ["texts"] = new JsonArray(), ["width"] = 0, ["height"] = 0 };
            var b = sample.Class.EndsWith("Type", StringComparison.Ordinal) ? BuildUps.OfType(q.Model, sample) : BuildUps.Of(q.Model, sample, q.Services);
            var content = b != null ? BuildupText.Content(q.Model, sample, type, book.Units, q.Services, b) : null;
            var style = Standards.TextOf(book, type.TextStyle);
            var lay = BuildupText.Lay(content ?? new BuildupContent(["?"], [], [], false), style.Height, style.Bold, type.BulletOrDefault, type.WidthOrDefault);
            return new JsonObject
            {
                ["sample"] = new JsonObject { ["id"] = sample.Id, ["name"] = sample.Name, ["class"] = sample.Class },
                ["stack"] = b?.Stack ?? false,
                ["height"] = Math.Round(lay.Height, 3),
                ["width"] = Math.Round(lay.Width, 3),
                ["textHeight"] = style.Height,
                ["bold"] = style.Bold,
                ["texts"] = new JsonArray(lay.Texts.Select(x => (JsonNode)new JsonObject
                {
                    ["x"] = Math.Round(x.X, 3), ["y"] = Math.Round(x.Y, 3), ["text"] = x.Text, ["role"] = x.Role, ["entry"] = x.Entry,
                }).ToArray()),
                ["mids"] = new JsonArray(lay.EntryMid.Select(y => (JsonNode)Math.Round(y, 3)).ToArray()),
                // The sample's layers in their natural order (thickness in metres, colour), and the layer each entry writes.
                ["layers"] = new JsonArray((b?.Layers ?? []).Select(l => (JsonNode)new JsonObject
                {
                    ["index"] = l.Index, ["thickness"] = l.Thickness, ["color"] = Materials.ColorOf(q.Model, l.Material), ["name"] = l.Name,
                }).ToArray()),
                ["entries"] = new JsonArray((content?.Entries ?? []).Select(x => (JsonNode)x.Layer).ToArray()),
            };
        });

        // A tag type's look for its editor's preview (decision 331): the type given (a draft, read as the standards read it;
        // else the one named), laid out on paper (mm, y down) with its leader from a point below it: a plain type's lines
        // as an element it tags writes them (else its template's field names); a build-up type's on a sample build-up
        // (the type given, else as drafting.buildupPreview finds one) whose layers stand as a strip beside it, its leaders ending in them.
        r.Query("drafting.tagPreview", (q, a) =>
        {
            var book = DraftingResources.Book(q.Model);
            var type = (a["entry"] is JsonObject draft
                ? Standards.Read(new JsonObject { ["tagTypes"] = new JsonArray(draft.DeepClone()) }).TagTypes.FirstOrDefault()
                : Standards.TagTypeOf(book, (string?)a["tagType"])) ?? throw new CommandException("There is no such tag type.");
            var style = Standards.TextOf(book, type.TextStyle);
            const string ink = "#111111";
            var elbow = type.LeaderShapeOrDefault == TagLeaders.Elbow;
            var strip = new JsonArray();
            ViewMark mk;
            if (type.Shape == "buildup")
            {
                var sample = (q.Model.Get((string?)a["type"] ?? "") is { } given && BuildUps.OfType(q.Model, given) != null ? given : null)
                    ?? q.Model.OfClass(IfcClass.WallType).FirstOrDefault(x => BuildUps.OfType(q.Model, x) != null)
                    ?? q.Model.OfClass(IfcClass.SlabType).FirstOrDefault(x => BuildUps.OfType(q.Model, x) != null);
                var b = sample != null ? BuildUps.OfType(q.Model, sample) : null;
                var content = sample != null && b != null ? BuildupText.Content(q.Model, sample, type, book.Units, q.Services, b) : null;
                content ??= new BuildupContent(["?"], [], [], false);
                var lay = BuildupText.Lay(content, style.Height, style.Bold, type.BulletOrDefault, type.WidthOrDefault);
                var hp = style.Height;
                var knee = new Vec2(0, 0);
                // The block's top: where the knee puts it (an elbow), else at the origin, the strip a little to its right.
                var top = elbow ? TagLeaders.Buildup(null, lay, type, style, 1, knee, new Vec2(10, 0), true, null, ink).Head.Y + lay.Height / 2 : 0;
                var bottom = top - lay.Height;
                var stripX = elbow ? 6 + type.ExtensionOrDefault : lay.Width + 14;
                const double stripW = 9;
                var layers = b?.Layers ?? [];
                var total = layers.Sum(l => l.Thickness) is > 0 and var sum ? sum : 1;
                var bands = new Dictionary<int, (double Y0, double Y1)>();
                var y = top;
                foreach (var l in layers)
                {
                    var y1 = y - l.Thickness / total * lay.Height;
                    bands[l.Index] = (y, y1);
                    strip.Add((JsonNode)new JsonObject { ["x"] = stripX, ["width"] = stripW, ["y0"] = Math.Round(-y, 3), ["y1"] = Math.Round(-y1, 3), ["color"] = Materials.ColorOf(q.Model, l.Material) });
                    y = y1;
                }
                var mid = stripX + stripW / 2;
                var targets = type.LeaderPerLayer
                    ? content.Entries.Select((x, i) => (i, bands.TryGetValue(x.Layer, out var band) ? new Vec2(mid, (band.Y0 + band.Y1) / 2) : (Vec2?)null))
                        .Where(x => x.Item2 != null).Select(x => (x.i, x.Item2!.Value)).ToList()
                    : null;
                var one = new Vec2(mid, (top + bottom) / 2);
                if (elbow) mk = TagLeaders.Buildup(null, lay, type, style, 1, knee, one, true, targets, ink).Mark;
                else
                {
                    // Straight, as views draw it: out of the block's right side, a stub per layer, then into its layer.
                    mk = new ViewMark(null);
                    foreach (var x in lay.Texts) mk.Text(new PenText(new Vec2(x.X, -x.Y), x.Text, hp, 0, "start", "bottom", style.Bold, style.Font, style.Italic), ink);
                    var side = lay.Width + 0.8 * hp;
                    foreach (var (from, to) in targets is { Count: > 0 }
                        ? targets.Select(x => (new Vec2(side, -lay.EntryMid[x.Item1]), x.Item2)).ToList()
                        : [(new Vec2(side, (top + bottom) / 2), one)])
                    {
                        mk.Polyline(targets is { Count: > 0 } ? [from, from + new Vec2(2.5, 0), to] : [from, to], false, "TAG", Pens.Default.Hatch, ink);
                        mk.Polyline(ViewMark.Circle(to, hp / 5, 8), true, "TAG", Pens.Default.Seen, ink);
                    }
                }
            }
            else
            {
                var sample = type.Classes.SelectMany(q.Model.OfClass).FirstOrDefault();
                var lines = sample != null
                    ? LinesOf(q.Model, new Entity("", IfcClass.Annotation, "", new JsonObject()), sample, type, book.Units)
                    : type.Template.Split('|').Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"\{([^{}]*)\}", "$1").Trim()).Where(l => l.Length > 0).DefaultIfEmpty("?").ToList();
                mk = elbow
                    ? TagLeaders.Plain(null, lines, style, type, 1, new Vec2(8, 8), new Vec2(0, 0), true, ink).Mark
                    : Box(null, lines, style, 1, new Vec2(14 + lines.Max(l => l.Length) * style.Height * 0.31, 10), new Vec2(0, 0), null, true, ink);
            }
            var segments = new JsonArray(mk.Lines.Where(l => l.Layer != "PICK").Select(l => (JsonNode)new JsonArray(
                Math.Round(l.A.X, 3), Math.Round(-l.A.Y, 3), Math.Round(l.B.X, 3), Math.Round(-l.B.Y, 3))).ToArray());
            var texts = new JsonArray(mk.Texts.Select(x => (JsonNode)new JsonObject
            {
                ["x"] = Math.Round(x.Text.At.X, 3), ["y"] = Math.Round(-x.Text.At.Y, 3), ["text"] = x.Text.Text, ["anchor"] = x.Text.Anchor, ["baseline"] = x.Text.Baseline,
            }).ToArray());
            // What it all covers, for the picture's frame: the lines, the strip, each text's extent.
            var xs = new List<double>(); var ys = new List<double>();
            foreach (var l in mk.Lines) { xs.AddRange([l.A.X, l.B.X]); ys.AddRange([-l.A.Y, -l.B.Y]); }
            foreach (var x in mk.Texts)
            {
                var w = BuildupText.Measure(x.Text.Text, style.Height, style.Bold);
                var x0 = x.Text.Anchor switch { "end" => x.Text.At.X - w, "middle" => x.Text.At.X - w / 2, _ => x.Text.At.X };
                xs.AddRange([x0, x0 + w]); ys.AddRange([-x.Text.At.Y - style.Height * 1.2, -x.Text.At.Y + style.Height * 0.4]);
            }
            foreach (var band in strip.OfType<JsonObject>()) { xs.AddRange([(double)band["x"]!, (double)band["x"]! + 9]); ys.AddRange([(double)band["y0"]!, (double)band["y1"]!]); }
            return new JsonObject
            {
                ["shape"] = type.LeaderShapeOrDefault, ["textHeight"] = style.Height, ["bold"] = style.Bold,
                ["segments"] = segments, ["texts"] = texts, ["strip"] = strip,
                ["bounds"] = xs.Count == 0 ? new JsonArray(0, 0, 10, 10) : new JsonArray(Math.Round(xs.Min(), 3), Math.Round(ys.Min(), 3), Math.Round(xs.Max(), 3), Math.Round(ys.Max(), 3)),
            };
        });

        // What a tag of this element can show: its fields, its and its type's parameters, the standard sets' properties for its class, an import's sets.
        r.Query("drafting.tagFields", (q, a) =>
        {
            var e = q.Model.Get((string?)a["element"] ?? "") ?? throw new CommandException("Which element?");
            return TagText.Fields(q.Model, e, DraftingResources.Book(q.Model).Units);
        });
    }
}
