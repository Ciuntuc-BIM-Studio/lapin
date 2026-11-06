using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// A tag's leader as ArchiCAD's labels draw it (decision 331): an elbow, an oblique segment from the element to a knee,
/// then a horizontal shoulder along the text that underlines its first row (or each, the last, none: then it stops at the
/// text's side, at the first row's middle). The knee is the tag's point (<c>at</c>), so it follows its element as the
/// tag does (<see cref="TagHold"/>); the text lies beside it on the side away from the leader's end, aligned towards
/// the knee, the shoulder as long as the text plus the type's <see cref="TagType.Extension"/>. No box is drawn. The
/// other shape, straight, is the box with its leader from the nearer side, bent at a free elbow (report #64).
/// </summary>
public static class TagLeaders
{
    /// <summary>Stamped on tags made since the elbow leader (decision 331): one without it was made straight, and is kept so (<see cref="TagLeaderUpgrade"/>).</summary>
    public const string RevKey = "leaderRev";
    public const int Rev = 2;

    public const string Straight = "straight", Elbow = "elbow";
    /// <summary>How far under a row's baseline its underline runs, in text heights.</summary>
    public const double Under = 0.3;
    /// <summary>The gap between the text and the shoulder's ends, in text heights.</summary>
    public const double Pad = 0.4;
    /// <summary>A plain tag's rows apart, in text heights (a build-up's are its layout's).</summary>
    public const double Pitch = 1.7;

    /// <summary>
    /// A tag's leader shape: its own (<c>leaderShape</c>), else straight for one bent by hand at a free elbow (an older
    /// tag keeps its look), else its type's, else an elbow.
    /// </summary>
    public static string ShapeOf(Entity tag, TagType? type) =>
        tag.Str("leaderShape") is Straight or Elbow ? tag.Str("leaderShape")!
        : tag.Attrs["elbow"] is JsonArray ? Straight
        : type?.LeaderShapeOrDefault ?? Elbow;

    /// <summary>Which side of its knee the text lies on: away from the leader's end (+1 right, −1 left); right without an end.</summary>
    public static int Side(Vec2 knee, Vec2? end) => end is { } e && knee.X < e.X - 1e-9 ? -1 : 1;

    /// <summary>The oblique segment from the end (its dot) to the knee and on along the shoulder to <paramref name="stopX"/>.</summary>
    static void Leader(ViewMark mk, Vec2? end, Vec2 knee, double stopX, double h, string color)
    {
        var path = new List<Vec2>();
        if (end is { } e && (e - knee).Length > 0.5 * h)
        {
            path.Add(e);
            mk.Polyline(ViewMark.Circle(e, h / 5, 8), true, "TAG", Pens.Default.Seen, color);
        }
        path.Add(knee);
        if (Math.Abs(stopX - knee.X) > 1e-9) path.Add(new Vec2(stopX, knee.Y));
        if (path.Count > 1) mk.Polyline(path, false, "TAG", Pens.Default.Hatch, color);
    }

    /// <summary>
    /// A plain tag's lines laid out by its elbow leader: the rows beside the knee, the anchor row (the underlined one,
    /// the first for none) just over it, its leader from <paramref name="end"/> when <paramref name="leader"/> is on (the
    /// end still says the side when it is off). Returns the mark, the text's middle (the head's grip) and the side.
    /// </summary>
    public static (ViewMark Mark, Vec2 Head, int Side) Plain(string? id, IReadOnlyList<string> lines, TextStyle style, TagType? type, double mm, Vec2 knee, Vec2? end, bool leader, string color)
    {
        var h = style.Height * mm;
        var (pad, ext) = (Pad * h, (type?.ExtensionOrDefault ?? 0) * mm);
        var under = type?.UnderlineOrDefault ?? "first";
        var n = lines.Count;
        var w = lines.Max(l => BuildupText.Measure(l, style.Height, style.Bold)) * mm;
        var side = Side(knee, end);
        var r = under == "last" ? n - 1 : 0;
        // The knee under the anchor row's baseline (none: at its middle), the other rows above or below it.
        var off = under == "none" ? -h / 2 : Under * h;
        double Base(int i) => knee.Y + off - Pitch * h * (i - r);
        var textX = knee.X + side * (ext + pad);
        var far = knee.X + side * (ext + pad + w + pad);
        var mk = new ViewMark(id);
        for (var i = 0; i < n; i++)
            mk.Text(new PenText(new Vec2(textX, Base(i)), lines[i], style.Height, 0, side > 0 ? "start" : "end", "bottom", style.Bold, style.Font, style.Italic), color);
        var (x0, x1) = side > 0 ? (knee.X, far) : (far, knee.X);
        var (top, bottom) = (Base(0) + h + pad, Base(n - 1) - Under * h - pad);
        // What a click on the tag hits: its text and shoulder, never drawn.
        mk.Polyline([new(x0, bottom), new(x1, bottom), new(x1, top), new(x0, top)], true, "PICK", 0, color);
        if (leader)
        {
            Leader(mk, end, knee, under == "none" ? knee.X + side * ext : far, h, color);
            if (under == "each")
                for (var i = 0; i < n; i++)
                    if (i != r) mk.Line(new Vec2(knee.X, Base(i) - Under * h), new Vec2(far, Base(i) - Under * h), "TAG", Pens.Default.Hatch, color);
        }
        return (mk, new Vec2((x0 + x1) / 2, (top + bottom) / 2), side);
    }

    /// <summary>
    /// A build-up tag's block laid out by its elbow leader (docs/plan-buildup-tags.md): the block beside the knee, left
    /// aligned as a note is, the anchor row over it (the first row, the last, or with leaders per layer the first layer's).
    /// One leader: the oblique to the knee, the shoulder under the rows the type underlines. One per layer
    /// (<paramref name="targets"/>: an entry and the point inside its layer): each from its layer to its own knee, the
    /// knees one column under the knee, each shoulder under its row (none: at its middle, stopping at the block's side).
    /// </summary>
    public static (ViewMark Mark, Vec2 Head, int Side) Buildup(string? id, BuildupLayout lay, TagType type, TextStyle style, double mm, Vec2 knee, Vec2? end, bool leader,
        IReadOnlyList<(int Entry, Vec2 To)>? targets, string color)
    {
        var hp = style.Height;
        var h = hp * mm;
        var (pad, ext) = (Pad * h, type.ExtensionOrDefault * mm);
        var under = type.UnderlineOrDefault;
        var perLayer = leader && targets is { Count: > 0 } && lay.EntryMid.Count > 0;
        var side = perLayer ? (knee.X >= targets!.Average(x => x.To.X) ? 1 : -1) : Side(knee, end);
        var rows = lay.Texts.Select(x => Math.Round(x.Y, 6)).Distinct().Order().ToList();
        if (rows.Count == 0) rows.Add(hp);
        var off = under == "none" ? -hp / 2 : Under * hp;
        var anchor = perLayer || type.LeaderPerLayer && lay.EntryMid.Count > 0 ? lay.EntryMid[0] + hp / 2 : under == "last" ? rows[^1] : rows[0];
        var top = knee.Y + (anchor + off) * mm;
        var width = lay.Width * mm;
        var left = side > 0 ? knee.X + ext + pad : knee.X - ext - pad - width;
        var far = knee.X + side * (ext + pad + width + pad);
        var stop = under == "none" ? knee.X + side * ext : far;
        var mk = new ViewMark(id);
        foreach (var x in lay.Texts)
            mk.Text(new PenText(new Vec2(left + x.X * mm, top - x.Y * mm), x.Text, hp, 0, "start", "bottom", style.Bold, style.Font, style.Italic), color);
        var bottom = top - lay.Height * mm;
        mk.Polyline([new(left, bottom), new(left + width, bottom), new(left + width, top), new(left, top)], true, "PICK", 0, color);
        if (perLayer)
        {
            foreach (var (entry, to) in targets!)
            {
                var k = new Vec2(knee.X, top - (lay.EntryMid[entry] + hp / 2 + off) * mm);
                Leader(mk, to, k, stop, h, color);
                mk.Snaps.Add(to);
            }
        }
        else if (leader)
        {
            Leader(mk, end, knee, stop, h, color);
            if (end is { } e && (e - knee).Length > 0.5 * h) mk.Snaps.Add(e);
            // A type of leaders per layer where this view cannot tell the layers apart: its shoulder alone, under the first layer.
            List<double> underlined = type.LeaderPerLayer ? [] : under switch { "each" => rows, "last" => [rows[^1]], "first" => [rows[0]], _ => [] };
            foreach (var y in underlined)
            {
                var at = top - (y + Under * hp) * mm;
                if (Math.Abs(at - knee.Y) > 1e-9) mk.Line(new Vec2(knee.X, at), new Vec2(far, at), "TAG", Pens.Default.Hatch, color);
            }
        }
        return (mk, new Vec2(left + width / 2, (top + bottom) / 2), side);
    }
}

/// <summary>
/// Tags an older Lapin wrote keep the straight leader in a box they were drawn with (decision 331):
/// those without <see cref="TagLeaders.RevKey"/> and a shape of their own are given <c>straight</c>, so
/// opening a project moves no text; new tags take their type's shape (elbow by default).
/// </summary>
public sealed class TagLeaderUpgrade : IProjectUpgrade
{
    public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
    {
        if (!entities.Any(Old)) return (entities, relations);
        return (entities.Select(e =>
        {
            if (!Old(e)) return e;
            var attrs = e.CloneAttrs();
            if (attrs["leaderShape"] == null) attrs["leaderShape"] = TagLeaders.Straight;
            attrs[TagLeaders.RevKey] = TagLeaders.Rev;
            return e.With(attrs: attrs);
        }).ToList(), relations);
    }

    static bool Old(Entity e) => TagHold.IsTag(e) && e.Attrs[TagLeaders.RevKey] == null;
}
