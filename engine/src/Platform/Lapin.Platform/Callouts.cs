using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Detail callouts (forum topic 107, as Revit's and ArchiCAD's): a <c>LapinCallout</c> on a plan, a section or an elevation
/// (<c>viewId</c>) marks an area of it with a boundary, a rectangle (<c>min</c>, <c>max</c>, kept in the view's turned frame
/// as a crop is), a circle (<c>centre</c>, <c>radius</c>) or a polygon (<c>pts</c>), and a leader to a head (<c>head</c>: its
/// offset from the boundary's upper-right corner, m) that writes the number and the sheet of the view it calls
/// (<c>target</c>), live: "3 / A-501", "–" while that view is on no sheet. The head is the calloutHead symbol slot's when one
/// is chosen. Made by the callout (<c>made</c>), that view is a detail view: of the same kind as its parent, its settings
/// copied (<c>calloutOf</c> names the parent), cropped to the boundary's box, at a larger scale (1:20, or the project's next
/// standard scale under the parent's) and the finest level of development, with its own Visibility / Graphics, annotations and
/// drafting. Its model content stays live; when the parent's cut or line changes, the detail follows (<see cref="FollowUp"/>);
/// the boundary's grips crop it again (<c>view.updateCallout</c>). A reference callout (<c>made</c> false) points at a view
/// that is already there (a drafting view, another detail) and crops nothing. The engine draws the mark with the view, so
/// sheets, PDF and DXF print it as the screen shows it; Visibility / Graphics shows or hides it with the callouts.
/// </summary>
public static class Callouts
{
    public const string Class = "LapinCallout";
    public const string Layer = "CALLOUT";
    /// <summary>On a detail view: the view it was called out from.</summary>
    public const string Key = "calloutOf";
    public static readonly string[] Shapes = ["rect", "circle", "polygon"];
    /// <summary>The head's bubble radius (paper mm), as Lapin's Callout bubble symbol.</summary>
    public const double HeadRadius = 6;
    /// <summary>The smallest a boundary may be across (m), as a crop region.</summary>
    const double MinSize = 0.05;

    /// <summary>What a detail view takes again from its parent when the parent changes: where it looks, never how it draws.</summary>
    static readonly string[] Followed =
    [
        "storeyId", "cutHeight", "viewRange", "orientation", "foundation", "storeys", "look",
        "start", "end", "pts", "curve", "along", "stations", "jogged", "depth", "flip", "margin", "marginA", "marginB", "station", AcrossSections.Key,
        "direction", "originX", "planeDepth", "markerAlong", "markerDepth", "markerShift", "extentMin", "extentMax",
        "x", "y", "z", "bearing", "tilt", "spin",
    ];

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Callout", [new("shape", "Shape", "choice", Choices: Shapes, ReadOnly: true)], "#000000", Kind: "annotation"));
        r.Service<IDeletion>(new Deletion());
        r.Service<ICommandFollowUp>(new FollowUp());

        // A callout drawn on a view: {viewId, shape?: rect | circle | polygon, min + max (rect, the view's turned frame) | centre + radius |
        // pts, scale? (1:N), detail? (LOD), name?, reference? (a view that is there: nothing made), head? [dx, dy]}. One step: the callout and its detail view.
        r.Command("view.callout", "Callout", (ctx, a) =>
        {
            var host = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("A callout is drawn on a view.");
            var kind = host.Str("kind") ?? "plan";
            if (kind == "3d") throw new CommandException("A callout is drawn on a plan, a section or an elevation, not in 3D.");
            var attrs = new JsonObject { ["viewId"] = host.Id };
            var shape = Shape(attrs, a, (string?)a["shape"] ?? "rect");
            if (a["head"] is JsonArray) attrs["head"] = J(Args.Point(a["head"], "the head's offset"));
            string target;
            if ((string?)a["reference"] is { Length: > 0 } refId)
            {
                var referenced = Views.Find(ctx.Model, refId) ?? throw new CommandException("There is no such view to refer to.");
                if (referenced.Id == host.Id) throw new CommandException("A callout refers to another view.");
                if (referenced.Str("kind") == "3d") throw new CommandException("A callout refers to a plan, a section, an elevation or a drafting view.");
                target = referenced.Id;
                attrs["made"] = false;
            }
            else
            {
                // An elevation's drawing moves with the model's bounds until it has a plane: fixed now, so the detail stays on what was called.
                if (kind == "elevation" && host.NumOrNull("originX") == null && ctx.Model.Get(host.Id) != null) host = Views.FixElevationPlane(ctx, host);
                var box = Box(attrs, Views.Turn(ctx.Model, host));
                target = MakeDetail(ctx, host, box, Json.Number(a["scale"]), (int?)Json.Number(a["detail"]), (string?)a["name"]).Id;
                attrs["made"] = true;
            }
            attrs["target"] = target;
            var e = new Entity(ctx.NewId(), Class, ctx.Model.Get(target)?.Name ?? "Callout", attrs);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id, ["view"] = target, ["shape"] = shape };
        });

        // A callout's boundary moved or reshaped ({id, shape?, min, max | centre, radius | pts}), its head moved ({head: [dx, dy] | null}),
        // or what it calls ({reference: view}: it then refers to that view): one step, its detail view cropped again.
        r.Command("view.updateCallout", "Callout", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("Not a callout.");
            Pins.Refuse([e]);
            var attrs = e.CloneAttrs();
            if (a.ContainsKey("shape") || a.ContainsKey("min") || a.ContainsKey("max") || a.ContainsKey("centre") || a.ContainsKey("radius") || a.ContainsKey("pts"))
            {
                var merged = new JsonObject();
                foreach (var k in new[] { "min", "max", "centre", "radius", "pts" }) if (attrs[k] != null) merged[k] = attrs[k]!.DeepClone();
                foreach (var k in new[] { "min", "max", "centre", "radius", "pts" }) if (a.ContainsKey(k)) merged[k] = a[k]?.DeepClone();
                foreach (var k in new[] { "min", "max", "centre", "radius", "pts" }) attrs.Remove(k);
                Shape(attrs, merged, (string?)a["shape"] ?? e.Str("shape") ?? "rect");
            }
            if (a.ContainsKey("head"))
            {
                if (a["head"] is JsonArray) attrs["head"] = J(Args.Point(a["head"], "the head's offset")); else attrs.Remove("head");
            }
            if ((string?)a["reference"] is { Length: > 0 } refId)
            {
                var referenced = Views.Find(ctx.Model, refId) ?? throw new CommandException("There is no such view to refer to.");
                if (referenced.Id == e.Str("viewId")) throw new CommandException("A callout refers to another view.");
                if (referenced.Id != e.Str("target")) { attrs["target"] = referenced.Id; attrs["made"] = false; }
            }
            var after = e.With(attrs: attrs);
            ctx.Replace(after);
            Recrop(ctx, after);
            return null;
        });
    }

    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6));

    /// <summary>The boundary as asked, written into <paramref name="attrs"/>: its shape, checked; returns the shape.</summary>
    static string Shape(JsonObject attrs, JsonObject a, string shape)
    {
        switch (shape)
        {
            case "rect":
            {
                var (p, q) = (Args.Point(a["min"], "the callout's corner"), Args.Point(a["max"], "the callout's corner"));
                var (lo, hi) = (new Vec2(Math.Min(p.X, q.X), Math.Min(p.Y, q.Y)), new Vec2(Math.Max(p.X, q.X), Math.Max(p.Y, q.Y)));
                if (hi.X - lo.X < MinSize || hi.Y - lo.Y < MinSize) throw new CommandException("A callout is 5 cm across at least.");
                attrs["min"] = J(lo); attrs["max"] = J(hi);
                break;
            }
            case "circle":
            {
                var c = Args.Point(a["centre"], "the callout's centre");
                var rad = Json.Number(a["radius"]) is { } rr && double.IsFinite(rr) ? rr : throw new CommandException("A round callout has a radius.");
                if (rad * 2 < MinSize) throw new CommandException("A callout is 5 cm across at least.");
                attrs["centre"] = J(c); attrs["radius"] = Math.Round(rad, 6);
                break;
            }
            case "polygon":
            {
                var pts = Args.StoredPoints(a["pts"]);
                if (pts.Count < 3) throw new CommandException("A sketched callout has three corners at least.");
                var (lo, hi) = Bounds(pts);
                if (hi.X - lo.X < MinSize || hi.Y - lo.Y < MinSize) throw new CommandException("A callout is 5 cm across at least.");
                attrs["pts"] = new JsonArray(pts.Select(p => (JsonNode)J(p)).ToArray());
                break;
            }
            default: throw new CommandException("A callout is a rectangle (rect), a circle or a polygon.");
        }
        attrs["shape"] = shape;
        return shape;
    }

    static (Vec2 Min, Vec2 Max) Bounds(IEnumerable<Vec2> pts)
    {
        var l = pts.ToList();
        return (new Vec2(l.Min(p => p.X), l.Min(p => p.Y)), new Vec2(l.Max(p => p.X), l.Max(p => p.Y)));
    }

    /// <summary>A callout's boundary in its view's drawing coordinates (a rectangle's corners turned back from the view's frame).</summary>
    public static List<Vec2> Outline(IModelView m, Entity e, double? turn = null)
    {
        var t = turn ?? (Views.Find(m, e.Str("viewId") ?? "") is { } v ? Views.Turn(m, v) : 0);
        Vec2 World(Vec2 p) => t == 0 ? p : Turning.Turn(p, -t);
        switch (e.Str("shape"))
        {
            case "circle":
            {
                var c = Args.PointOf(e.Attrs["centre"]);
                var r = e.NumOrNull("radius") ?? 0;
                var n = Math.Max(16, Chords.Circle(r));
                return [.. Enumerable.Range(0, n).Select(k => c + new Vec2(Math.Cos(2 * Math.PI * k / n), Math.Sin(2 * Math.PI * k / n)) * r)];
            }
            case "polygon": return Args.StoredPoints(e.Attrs["pts"]);
            default:
            {
                var (a, b) = (Args.PointOf(e.Attrs["min"]), Args.PointOf(e.Attrs["max"]));
                return [World(a), World(new Vec2(b.X, a.Y)), World(b), World(new Vec2(a.X, b.Y))];
            }
        }
    }

    /// <summary>The box a callout crops its detail view to, in the view's turned frame (the rectangle itself; a circle's or a polygon's bounds).</summary>
    static (Vec2 Min, Vec2 Max) Box(JsonObject attrs, double turn)
    {
        Vec2 Local(Vec2 p) => turn == 0 ? p : Turning.Turn(p, turn);
        switch ((string?)attrs["shape"])
        {
            case "circle":
            {
                var c = Local(Args.PointOf(attrs["centre"]));
                var r = Json.Number(attrs["radius"]) ?? 0;
                return (c - new Vec2(r, r), c + new Vec2(r, r));
            }
            case "polygon": return Bounds(Args.StoredPoints(attrs["pts"]).Select(Local));
            default: return (Args.PointOf(attrs["min"]), Args.PointOf(attrs["max"]));
        }
    }

    /// <summary>The scale a detail takes when none is asked: 1:20, or the project's largest standard scale under half the parent's.</summary>
    public static double DetailScale(StyleBook book, double parentScale)
    {
        var scales = Standards.ScalesOf(book).Order().ToList();
        var cap = Math.Min(20, parentScale / 2);
        if (scales.LastOrDefault(s => s <= cap + 1e-9) is var s0 and > 0) return s0;
        if (scales.LastOrDefault(s => s < parentScale - 1e-9) is var s1 and > 0) return s1;
        return Math.Max(1, Math.Round(parentScale / 2));
    }

    /// <summary>The detail view a callout makes: its parent's settings, cropped to the box, larger and finer, named "Detail N".</summary>
    static Entity MakeDetail(ICommandContext ctx, Entity host, (Vec2 Min, Vec2 Max) box, double? scale, int? detail, string? name)
    {
        var attrs = host.CloneAttrs();
        foreach (var k in new[] { ViewCrops.Key, "detailNumber", "headA", "headB", SymbolSlots.Key }) attrs.Remove(k);
        attrs[Key] = host.Id;
        var parentScale = host.NumOrNull("scale") ?? 100;
        attrs["scale"] = Standards.ViewScale(scale is { } s ? JsonValue.Create(s) : null) ?? DetailScale(DraftingResources.Book(ctx.Model), parentScale);
        var own = attrs["styleOverrides"] as JsonObject ?? [];
        own["detail"] = detail is { } lod && PlanDetail.Lods.Contains(lod) ? lod : PlanDetail.Lod400;
        attrs["styleOverrides"] = own;
        // Cropped to the boundary, its frame hidden on paper: the callout's grips in the parent crop it again.
        attrs[ViewCrops.Key] = new ViewCrops.Crop(true, false, box.Min, box.Max, false, [ViewCrops.DefaultOffset, ViewCrops.DefaultOffset, ViewCrops.DefaultOffset, ViewCrops.DefaultOffset]).ToJson();
        var v = new Entity(ctx.NewId(), Views.Class, name is { Length: > 0 } ? name : NextName(ctx.Model), attrs);
        ctx.Add(v);
        return v;
    }

    static string NextName(IModelView m)
    {
        var taken = m.OfClass(Views.Class).Select(v => v.Name).ToHashSet();
        var n = m.OfClass(Views.Class).Count(v => v.Str(Key) != null) + 1;
        while (taken.Contains($"Detail {n}")) n++;
        return $"Detail {n}";
    }

    /// <summary>A made callout's detail view cropped to its boundary again (its crop's other settings kept).</summary>
    static void Recrop(ICommandContext ctx, Entity callout)
    {
        if (!callout.Bool("made") || ctx.Model.Get(callout.Str("target") ?? "") is not { Class: Views.Class } detail) return;
        var host = Views.Find(ctx.Model, callout.Str("viewId") ?? "");
        var (lo, hi) = Box(callout.Attrs, host != null ? Views.Turn(ctx.Model, host) : 0);
        var old = ViewCrops.Of(detail);
        if (old != null && (old.Min - lo).Length < 1e-7 && (old.Max - hi).Length < 1e-7) return;
        var crop = new ViewCrops.Crop(old?.On ?? true, old?.Visible ?? false, lo, hi, old?.Annotation ?? false, old?.Offset ?? [ViewCrops.DefaultOffset, ViewCrops.DefaultOffset, ViewCrops.DefaultOffset, ViewCrops.DefaultOffset]);
        var attrs = detail.CloneAttrs();
        attrs[ViewCrops.Key] = crop.ToJson();
        ctx.Replace(detail.With(attrs: attrs));
    }

    /// <summary>Whether a view is a callout's detail (it draws no section or elevation marker of its own in other views).</summary>
    public static bool IsDetail(Entity view) => view.Str(Key) != null;

    /// <summary>The number and sheet a callout's head writes: the view's on its sheet, "–" while it is on none.</summary>
    public static (string Number, string Sheet) Head(IModelView m, Entity? target)
    {
        if (target == null) return ("?", "–");
        var (sheet, number) = SymbolSlots.PlacedOn(m, target.Id);
        return (sheet.Length > 0 ? number : "–", sheet.Length > 0 ? sheet : "–");
    }

    /// <summary>The view's callouts: each its boundary, a leader and its head with the called view's number and sheet.</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        var m = c.Model;
        var callouts = m.OfClass(Class).Where(x => x.Str("viewId") == c.View.Id).ToList();
        if (callouts.Count == 0) yield break;
        var turn = Views.Turn(m, c.View);
        var text = Standards.ResolveText(c.Book, []);
        foreach (var e in callouts)
        {
            var outline = Outline(m, e, turn);
            if (outline.Count < 3) continue;
            var target = Views.Find(m, e.Str("target") ?? "");
            var (number, sheet) = Head(m, target);
            var ink = target == null ? "#8f98a3" : "#000000";
            var mk = new ViewMark(e.Id);
            mk.Polyline(outline, true, Layer, 0.35, ink, target == null ? [2, 1.5] : null);
            // The head off the boundary's upper-right corner (its own offset, else 8 mm up and right on paper), a leader to it from the nearest point of the boundary.
            var (lo, hi) = Bounds(outline);
            var offset = Args.TryPoint(e.Attrs["head"]) ?? new Vec2(8 * c.Mm, 8 * c.Mm);
            var headAt = hi + offset;
            var rad = HeadRadius * c.Mm;
            var from = Nearest(outline, headAt);
            if ((headAt - from).Length > rad + 1e-9)
            {
                var to = headAt + (from - headAt) * (rad / (headAt - from).Length);
                mk.Line(from, to, Layer, Pens.Default.Seen, ink);
            }
            var tokens = SymbolSlots.MarkTokens(m, target, key => key switch
            {
                "mark.number" or "attr.number" => (true, number),
                "mark.sheet" or "attr.sheet" => (true, sheet),
                _ => (false, null),
            });
            if (SymbolSlots.SymbolFor(m, "calloutHead", e) is { } headSymbol)
                SymbolSlots.Draw(c, mk, headSymbol, headAt, new Vec2(0, 1), false, 1, Layer, ink, tokens);
            else
            {
                mk.Polyline(ViewMark.Circle(headAt, rad, Math.Max(24, Chords.Circle(rad))), true, Layer, Pens.Default.Seen, ink);
                mk.Line(headAt - new Vec2(rad, 0), headAt + new Vec2(rad, 0), Layer, Pens.Default.Seen, ink);
                mk.Text(new PenText(headAt + new Vec2(0, 2.6 * c.Mm), number, 3, 0, "middle", "middle", Bold: true, Font: text.Font), ink);
                mk.Text(new PenText(headAt - new Vec2(0, 2.9 * c.Mm), sheet, 2.2, 0, "middle", "middle", Font: text.Font), ink);
            }
            mk.Snaps.AddRange(e.Str("shape") == "circle" ? [Args.PointOf(e.Attrs["centre"])] : outline);
            mk.Snaps.Add(headAt);
            var label = sheet == "–" ? "–" : $"{number} / {sheet}";
            mk.Data = new JsonObject
            {
                ["callout"] = true, ["shape"] = e.Str("shape") ?? "rect", ["target"] = target?.Id, ["made"] = e.Bool("made"),
                ["number"] = number, ["sheet"] = sheet, ["label"] = label, ["head"] = J(headAt), ["anchor"] = J(hi), ["turn"] = Math.Round(turn, 12),
                ["bounds"] = new JsonArray(Math.Round(lo.X, 5), Math.Round(lo.Y, 5), Math.Round(hi.X, 5), Math.Round(hi.Y, 5)),
                ["scale"] = target?.NumOrNull("scale") is { } ts ? ts : null,
            };
            yield return mk;
        }
    }

    static Vec2 Nearest(IReadOnlyList<Vec2> loop, Vec2 p)
    {
        var best = loop[0];
        var bestD = double.MaxValue;
        for (var i = 0; i < loop.Count; i++)
        {
            var (a, b) = (loop[i], loop[(i + 1) % loop.Count]);
            var ab = b - a;
            var len2 = ab.X * ab.X + ab.Y * ab.Y;
            var t = len2 < 1e-18 ? 0 : Math.Clamp(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2, 0, 1);
            var q = a + ab * t;
            var d = (q - p).Length;
            if (d < bestD) { bestD = d; best = q; }
        }
        return best;
    }

    /// <summary>A view deleted takes its callouts with it, and the callouts that call it.</summary>
    sealed class Deletion : IDeletion
    {
        public bool Delete(ICommandContext ctx, string id)
        {
            if (ctx.Model.Get(id) is not { Class: Views.Class }) return false;
            foreach (var c in ctx.Model.OfClass(Class).Where(x => x.Str("viewId") == id || x.Str("target") == id).Select(x => x.Id).ToList())
                if (ctx.Model.Get(c) != null) ctx.Remove(c);
            return false;
        }
    }

    /// <summary>
    /// After every command, in its step: a parent view whose cut or line changed gives it to the detail views its callouts made;
    /// a detail view cropped by its own settings moves its rectangle callout's boundary with it.
    /// </summary>
    sealed class FollowUp : ICommandFollowUp
    {
        public JsonNode? After(ICommandContext ctx, IEdit edit, string command, JsonObject args, JsonNode? result)
        {
            var views = edit.EntityChanges.Where(ch => ch.After is { Class: Views.Class } && ch.Before != null).Select(ch => ch.Id).Distinct().ToList();
            if (views.Count == 0) return result;
            var callouts = ctx.Model.OfClass(Class).Where(x => x.Bool("made")).ToList();
            if (callouts.Count == 0) return result;
            foreach (var id in views)
            {
                if (ctx.Model.Get(id) is not { } view) continue;
                // A parent: what it looks at, given to its details.
                foreach (var c in callouts.Where(x => x.Str("viewId") == id))
                {
                    if (ctx.Model.Get(c.Str("target") ?? "") is not { Class: Views.Class } detail) continue;
                    var attrs = detail.CloneAttrs();
                    var changed = false;
                    foreach (var k in Followed)
                    {
                        var (mine, theirs) = (attrs[k], view.Attrs[k]);
                        if (JsonNode.DeepEquals(mine, theirs)) continue;
                        if (theirs == null) attrs.Remove(k); else attrs[k] = theirs.DeepClone();
                        changed = true;
                    }
                    if (changed) ctx.Replace(detail.With(attrs: attrs));
                }
                // A detail: its crop changed in its own settings moves its rectangle.
                if (view.Str(Key) != null && ViewCrops.Of(view) is { } crop)
                    foreach (var c in callouts.Where(x => x.Str("target") == id && (x.Str("shape") ?? "rect") == "rect"))
                    {
                        if (ctx.Model.Get(c.Id) is not { } now) continue;
                        var (lo, hi) = (Args.PointOf(now.Attrs["min"]), Args.PointOf(now.Attrs["max"]));
                        if ((lo - crop.Min).Length < 1e-7 && (hi - crop.Max).Length < 1e-7) continue;
                        var ca = now.CloneAttrs();
                        ca["min"] = J(crop.Min); ca["max"] = J(crop.Max);
                        ctx.Replace(now.With(attrs: ca));
                    }
            }
            return result;
        }
    }
}
