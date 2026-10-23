using BubbleBim.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>Texts on a view (IfcAnnotation TEXT): at a point, in a text style, turned by an angle.</summary>
public static class Texts
{
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        foreach (var t in c.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "TEXT" && x.Str("viewId") == c.View.Id))
        {
            var style = Standards.ResolveText(c.Book, Dimensions.ParamsOf(t));
            var at = t.Attrs["at"] is System.Text.Json.Nodes.JsonArray a ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
            var body = t.Str("text") ?? "";
            var angle = t.Num("angle");
            var mk = new ViewMark(t.Id).Text(new PenText(at, body, style.Height, angle, "start", "bottom", style.Bold, style.Font), style.Color ?? "#000000");
            // What a click hits: the text's box.
            var h = style.Height * c.Mm;
            var w = Math.Max(1, body.Length) * h * 0.6;
            var (u, n) = (new Vec2(Math.Cos(angle * Math.PI / 180), Math.Sin(angle * Math.PI / 180)), new Vec2(-Math.Sin(angle * Math.PI / 180), Math.Cos(angle * Math.PI / 180)));
            Vec2[] box = [at, at + u * w, at + u * w + n * h, at + n * h];
            for (var i = 0; i < 4; i++) mk.Lines.Add((box[i], box[(i + 1) % 4], "PICK", 0, "none", null));
            mk.Snaps.Add(at);
            yield return mk;
        }
    }
}
