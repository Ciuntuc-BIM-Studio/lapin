using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// Tags (the old Lapin's TAG): an element's mark, name, class or type, in a
/// box at a point of a view, with a leader to the element. The text is read
/// from the model each time the view is drawn, so it follows the element;
/// an element gone leaves its tag red with a question mark.
/// </summary>
public static class Tags
{
    public static readonly string[] Fields = ["mark", "name", "class", "type"];

    /// <summary>What a tag shows of an element.</summary>
    public static string? Text(IModelView m, Entity e, string field)
    {
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        return field switch
        {
            "name" => e.Name,
            "class" => e.Class.StartsWith("Ifc") ? e.Class[3..] : e.Class,
            "type" => type?.Name ?? e.Name,
            _ => e.Str("mark") ?? type?.Str("mark") ?? type?.Name ?? e.Name,
        };
    }

    /// <summary>Where a leader points: the middle of what the element draws on the view (its cut, else its lines).</summary>
    static Vec2? Anchor(ViewDrawing d, string element)
    {
        var pts = d.Cuts.Where(p => p.Element == element).SelectMany(p => p.Loops.SelectMany(l => l))
            .Concat(d.Lines.Where(l => l.Element == element).SelectMany(l => new[] { l.A, l.B })).ToList();
        return pts.Count == 0 ? null : new Vec2((pts.Min(p => p.X) + pts.Max(p => p.X)) / 2, (pts.Min(p => p.Y) + pts.Max(p => p.Y)) / 2);
    }

    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        foreach (var t in c.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "TAG" && x.Str("viewId") == c.View.Id))
        {
            var style = Standards.ResolveText(c.Book, Dimensions.ParamsOf(t));
            var at = t.Attrs["at"] is JsonArray a ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
            var e = c.Model.Get(t.Str("element") ?? "");
            var text = e != null ? Text(c.Model, e, t.Str("field") ?? "mark") ?? "?" : "?";
            var color = e == null ? "#c0392b" : style.Color ?? "#000000";
            var h = style.Height * c.Mm;
            var w = Math.Max(1, text.Length) * h * 0.62 + h;
            var mk = new ViewMark(t.Id);
            Vec2[] box = [at + new Vec2(-w / 2, -h), at + new Vec2(w / 2, -h), at + new Vec2(w / 2, h), at + new Vec2(-w / 2, h)];
            mk.Polyline(box, true, "TAG", Pens.Default.Hatch, color);
            mk.Text(new PenText(at, text, style.Height, 0, "middle", "middle", style.Bold, style.Font), color);
            if (e != null && Anchor(c.Drawing, e.Id) is { } target && (target - at).Length > h * 1.5)
            {
                // The leader leaves the box's nearer side.
                var from = new Vec2(Math.Clamp(target.X, at.X - w / 2, at.X + w / 2), Math.Clamp(target.Y, at.Y - h, at.Y + h));
                if (Math.Abs(target.X - at.X) / (w / 2) > Math.Abs(target.Y - at.Y) / h) from = new Vec2(at.X + Math.Sign(target.X - at.X) * w / 2, at.Y);
                else from = new Vec2(at.X, at.Y + Math.Sign(target.Y - at.Y) * h);
                mk.Line(from, target, "TAG", Pens.Default.Hatch, color);
                mk.Polyline(ViewMark.Circle(target, h / 5, 8), true, "TAG", Pens.Default.Seen, color);
            }
            mk.Snaps.Add(at);
            yield return mk;
        }
    }

    public static void Register(IModuleRegistry r)
    {
        // A tag of an element on a view: its mark (or name, class, type), at a point.
        r.Command("addTag", "Tag", (ctx, a) =>
        {
            var viewId = (string?)a["viewId"] ?? throw new CommandException("A tag is put on a view.");
            if (Views.Find(ctx.Model, viewId) == null) throw new CommandException("There is no such view.");
            var e = ctx.Model.Get((string?)a["element"] ?? "") ?? throw new CommandException("Tag what? Pick an element.");
            if (e.Class == IfcClass.Annotation) throw new CommandException("Tags are for the model's elements.");
            var field = (string?)a["field"] ?? "mark";
            if (!Fields.Contains(field)) throw new CommandException("A tag shows the mark, the name, the class or the type.");
            var at = a["at"] is JsonArray p && p.Count >= 2 ? new JsonArray(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0) : throw new CommandException("Where does the tag go?");
            var tag = new Entity(ctx.NewId(), IfcClass.Annotation, $"Tag {Text(ctx.Model, e, field)}", new JsonObject { ["viewId"] = viewId, ["element"] = e.Id, ["field"] = field, ["at"] = at }, "TAG");
            ctx.Add(tag);
            return new JsonObject { ["id"] = tag.Id };
        });
    }
}
