using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// Texts on a view (IfcAnnotation TEXT): at a point, in a text style, turned by an angle; with a leader (<c>leader</c>: its
/// arrow's point, then any bends, decision 620) a line from the arrow to the text's nearer side. On a sheet's paper its
/// <c>{…}</c> tokens are live, as a title block's (<c>{sheet.number}</c>, <c>{project.name}</c>); anywhere else it reads as typed.
/// <para>
/// Its lines are aligned (<c>align</c>, forum topic 148) left (as before), centred or right: without a width, on its point (the
/// point is their start, middle or end); with a width, in its box, which stays where it was drawn (from its point along the width).
/// </para>
/// </summary>
public static class Texts
{
    /// <summary>A text's alignments; none: left.</summary>
    public static readonly string[] Alignments = ["left", "center", "right"];

    /// <summary>Where the fields only a text has (its width, alignment, leader) apply: an annotation with a text.</summary>
    public static readonly IReadOnlyList<IReadOnlyDictionary<string, string[]>> OnTexts = [new Dictionary<string, string[]> { ["text"] = ["!"] }];

    /// <summary>A leader's field kind: taken off from the properties (null), never written there.</summary>
    public const string LeaderKind = "removeOnly";

    /// <summary>How far along its width a line's anchor is (0 left, ½ centre, 1 right), and the pen's anchor for it.</summary>
    public static (double Share, string Anchor) AlignOf(Entity t) => t.Str("align") switch
    {
        "center" => (0.5, "middle"),
        "right" => (1, "end"),
        _ => (0, "start"),
    };

    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        foreach (var t in c.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "TEXT" && x.Str("viewId") == c.View.Id))
        {
            var style = Standards.ResolveText(c.Book, Dimensions.ParamsOf(t));
            var at = Args.PointOf(t.Attrs["at"]);
            var body = PaperViews.Fill(c, t.Str("text") ?? "");
            var angle = t.Num("angle");
            var h = style.Height * c.Mm;
            // Wrapped and boxed by the lines' printed widths (TextMeasure, the PDF's own).
            double Width(string l) => TextMeasure.Width(l, h, style.Font, style.Bold, style.Italic);
            var maxW = t.NumOrNull("width") is { } mw && mw > 0 ? mw : (double?)null;
            var lines = TextWrap.Lines(body, Width, maxW);
            var (u, n) = (new Vec2(Math.Cos(angle * Math.PI / 180), Math.Sin(angle * Math.PI / 180)), new Vec2(-Math.Sin(angle * Math.PI / 180), Math.Cos(angle * Math.PI / 180)));
            var lineStep = h * TextMeasure.LinePitch;
            var mk = new ViewMark(t.Id);
            var w = maxW ?? Math.Max(Width("x"), lines.Max(Width));
            // Its box's left side: its point, or (no width of its own) moved back so that the point is the lines' middle or end.
            var (share, anchor) = AlignOf(t);
            var left = maxW != null ? at : at - u * (share * w);
            for (var k = 0; k < lines.Count; k++)
                mk.Text(new PenText(left + u * (share * w) - n * (k * lineStep), lines[k], style.Height, angle, anchor, "bottom", style.Bold, style.Font, style.Italic), style.Color ?? "#000000");
            // What a click hits: the text's box across all lines.
            var top = left + n * h;
            var bot = left - n * ((lines.Count - 1) * lineStep);
            Vec2[] box = [bot, bot + u * w, top + u * w, top];
            for (var i = 0; i < 4; i++) mk.Lines.Add((box[i], box[(i + 1) % 4], "PICK", 0, "none", null));
            mk.Snaps.Add(at);
            if (Leader(t) is { Count: > 0 } leader)
            {
                // To the side the leader comes from, at the first line's middle, a millimetre off the text.
                var last = leader[^1];
                var right = Vec2.Dot(last - left, u) > w / 2;
                var attach = left + n * (h / 2) + (right ? u * (w + c.Mm) : u * -c.Mm);
                var run = new List<Vec2>(leader) { attach };
                var color = style.Color ?? "#000000";
                for (var i = 0; i + 1 < run.Count; i++) mk.Line(run[i], run[i + 1], "LEADER", 0.18, color);
                var tip = run[0];
                var dir = run[1] - tip;
                if (dir.Length > 1e-12)
                {
                    var d = dir * (1 / dir.Length);
                    var side = new Vec2(-d.Y, d.X);
                    var (len, half) = (2.5 * c.Mm, 0.8 * c.Mm);
                    mk.Fills.Add(([new[] { tip, tip + d * len + side * half, tip + d * len - side * half }], color));
                }
                mk.Snaps.Add(tip);
            }
            yield return mk;
        }
    }

    /// <summary>A text's leader: its arrow's point first, then its bends (view coordinates); empty without one.</summary>
    public static List<Vec2> Leader(Entity t) =>
        t.Attrs["leader"] is JsonArray a ? [.. a.Select(Args.TryPoint).OfType<Vec2>()] : [];

    /// <summary>Split text on newlines and optionally word-wrap each paragraph to <paramref name="maxW"/> (metres): <see cref="TextWrap.Lines"/>.</summary>
    public static List<string> WrapLines(string body, double charW, double? maxW) => TextWrap.Lines(body, charW, maxW);
}
