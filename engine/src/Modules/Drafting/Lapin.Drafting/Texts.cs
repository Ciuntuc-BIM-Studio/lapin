using Lapin.Geometry;
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
            var at = Args.PointOf(t.Attrs["at"]);
            var body = t.Str("text") ?? "";
            var angle = t.Num("angle");
            var h = style.Height * c.Mm;
            var charW = Math.Max(1e-6, h * 0.6);
            var maxW = t.NumOrNull("width") is { } mw && mw > 0 ? mw : (double?)null;
            var lines = WrapLines(body, charW, maxW);
            var (u, n) = (new Vec2(Math.Cos(angle * Math.PI / 180), Math.Sin(angle * Math.PI / 180)), new Vec2(-Math.Sin(angle * Math.PI / 180), Math.Cos(angle * Math.PI / 180)));
            var lineStep = h * 1.35;
            var mk = new ViewMark(t.Id);
            for (var k = 0; k < lines.Count; k++)
                mk.Text(new PenText(at - n * (k * lineStep), lines[k], style.Height, angle, "start", "bottom", style.Bold, style.Font, style.Italic), style.Color ?? "#000000");
            // What a click hits: the text's box across all lines.
            var w = maxW ?? Math.Max(1, lines.Max(l => l.Length)) * charW;
            var top = at + n * h;
            var bot = at - n * ((lines.Count - 1) * lineStep);
            Vec2[] box = [bot, bot + u * w, top + u * w, top];
            for (var i = 0; i < 4; i++) mk.Lines.Add((box[i], box[(i + 1) % 4], "PICK", 0, "none", null));
            mk.Snaps.Add(at);
            yield return mk;
        }
    }

    /// <summary>Split text on newlines and optionally word-wrap each paragraph to <paramref name="maxW"/> (metres).</summary>
    public static List<string> WrapLines(string body, double charW, double? maxW)
    {
        var paras = body.Replace("\r\n", "\n").Split('\n');
        if (maxW is not > 0) return [.. paras];
        var maxChars = Math.Max(1, (int)Math.Floor(maxW.Value / charW));
        var result = new List<string>();
        foreach (var para in paras)
        {
            if (para.Length <= maxChars) { result.Add(para); continue; }
            var cur = "";
            foreach (var word in para.Split(' '))
            {
                if (cur.Length == 0) cur = word;
                else if (cur.Length + 1 + word.Length <= maxChars) cur += " " + word;
                else { result.Add(cur); cur = word; }
            }
            result.Add(cur);
        }
        return result;
    }
}
