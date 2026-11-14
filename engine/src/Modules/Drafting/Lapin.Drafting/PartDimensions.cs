using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// A part's dimensions on its element views (docs/plan-shop-drawings.md §5.2, decisions 581–582): derived on every draw from the
/// element as it is, never kept, so they follow the part. <c>of.dims</c> says which:
/// <list type="bullet">
/// <item><c>overall</c>: its length and height under and beside the drawing, in the frame's axes;</item>
/// <item><c>features</c>: those, and over the drawing the holes seen face on: a chain from end to end through each run's first and
/// last hole, a run written "4×80 = 320", the ordinates of every hole from the part's start, the rows' places across from its edge
/// on the left, each run's count and diameter; a notch's length and depth; an end cut off square, its angle.</item>
/// </list>
/// They draw in the drafting standards' dimension style, as the user's own dimensions do (<see cref="Dimensions.Draw"/>); their owner
/// is the view's (<c>view:partdim:key</c>), so the view's annotation category for dimensions hides them. A string moved by the user
/// (<c>drawing.moveDimension</c>) keeps its move by its key (<c>partDimMoves</c> on the view).
/// </summary>
public static class PartDimensions
{
    /// <summary>How far the first string stands off the drawing (paper mm), and each next one from the one before.</summary>
    public const double FirstOffset = 7, NextOffset = 7;
    public const string MovesKey = "partDimMoves";

    public static string Owner(Entity view, string key) => $"{view.Id}:partdim:{key}";

    sealed record Part(Entity Element, ElementFrame Frame, Vec2 Min, Vec2 Max, string Dims, IReadOnlyList<Solid> Body);

    /// <summary>The element view's part, its frame and its box as the view draws it; null when it is not an element view with dimensions.</summary>
    static Part? Of(ViewContext c)
    {
        if (!ElementViews.Is(c.View) || ElementViews.RoleOf(c.View) == ElementViews.Iso) return null;
        if ((string?)c.View.Attrs[ElementViews.Key]?["dims"] is not { } dims || dims == "none") return null;
        if (c.Model.Get(ElementViews.ElementOf(c.View) ?? "") is not { } e || c.Geometry?.Invoke(e.Id) is not { } g) return null;
        var body = g.Parts.Select(p => p.Solid).ToList();
        if (ElementFrames.Of(c.Model, c.Services, e, body.SelectMany(s => s.Vertices).ToList()) is not { } f) return null;
        var corners = new[] { f.Min.X, f.Max.X }.SelectMany(x => new[] { f.Min.Y, f.Max.Y }.SelectMany(y => new[] { f.Min.Z, f.Max.Z }.Select(z => c.Frame.Map(f.World(new Vec3(x, y, z)))))).ToList();
        return new Part(e, f, new Vec2(corners.Min(p => p.X), corners.Min(p => p.Y)), new Vec2(corners.Max(p => p.X), corners.Max(p => p.Y)), dims, body);
    }

    /// <summary>A string's move by the user (drawing metres), by its key.</summary>
    static Vec2 MoveOf(Entity view, string key) =>
        view.Attrs[MovesKey]?[key] is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : default;

    /// <summary>A string along <paramref name="u"/> through <paramref name="pts"/>, its line at <paramref name="at"/>; <paramref name="texts"/> written over values.</summary>
    public static ViewMark? String(ViewContext c, string owner, IReadOnlyList<Vec2> pts, Vec2 u, Vec2 at, IReadOnlyList<string?>? texts = null)
    {
        if (pts.Count < 2) return null;
        var n = u.Perp;
        var off = pts.Select(p => Vec2.Dot(at - p, n)).ToList();
        var lay = new Dimensions.Layout(u, n, pts.Select((p, i) => p + n * off[i]).ToList(), off);
        List<WallOpening>? none = [];
        var mk = Dimensions.Draw(c, owner, pts, lay, new JsonObject(), texts, null, false, ref none);
        if (mk != null) mk.Data = (mk.Data ?? []).With("partDim", owner[(owner.LastIndexOf(":partdim:", StringComparison.Ordinal) + 9)..]);
        return mk;
    }

    static string Mm(double m) => Math.Round(m * 1000).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Ordinates from <paramref name="zero"/> along the drawing's x: a base line at <paramref name="y"/>, a tick and an extension line
    /// at each point, each point's distance from the zero written along its extension line.
    /// </summary>
    static ViewMark Ordinate(ViewContext c, string owner, Vec2 zero, IReadOnlyList<Vec2> pts, double y)
    {
        var style = Standards.ResolveDim(c.Book, []);
        var text = Standards.ResolveText(c.Book, []);
        var mm = c.Mm;
        var color = style.Color ?? "#000000";
        var mk = new ViewMark(owner);
        var xs = pts.Select(p => p.X).Prepend(zero.X).Distinct().OrderBy(x => x).ToList();
        mk.Line(new Vec2(xs[0], y), new Vec2(xs[^1], y), "DIMENSION", Pens.Default.Hatch, color);
        var s = style.ArrowSize * mm;
        foreach (var p in pts.Prepend(zero).GroupBy(p => Math.Round(p.X, 6)).Select(g => g.First()))
        {
            var side = y >= p.Y ? 1 : -1;
            if (Math.Abs(y - p.Y) > style.ExtGap * mm) mk.Line(new Vec2(p.X, p.Y + side * style.ExtGap * mm), new Vec2(p.X, y + side * style.ExtOver * mm), "DIMENSION", Pens.Default.Hatch, color);
            var t = new Vec2(1, 1).Normalized() * (s / 2);
            mk.Line(new Vec2(p.X, y) - t, new Vec2(p.X, y) + t, "DIMENSION", Pens.Default.Seen, color);
            // The value along the extension line, reading upwards, just past the base line.
            mk.Text(new PenText(new Vec2(p.X - style.TextGap * mm, y + side * (style.ExtOver + 1) * mm), Mm(p.X - zero.X), text.Height, 90, side > 0 ? "start" : "end", "bottom", text.Bold, text.Font, text.Italic), color);
        }
        mk.Snaps.AddRange(xs.Select(x => new Vec2(x, y)));
        mk.Data = new JsonObject { ["partDim"] = owner[(owner.LastIndexOf(":partdim:", StringComparison.Ordinal) + 9)..] };
        return mk;
    }

    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (Of(c) is not { } part) yield break;
        var (min, max) = (part.Min, part.Max);
        var mm = c.Mm;
        var v = c.View;
        string Key(string k) => Owner(v, k);
        Vec2 Moved(string k, Vec2 at) => at + MoveOf(v, k);
        // The overall length under the drawing, the height on its right.
        if (max.X - min.X > 1e-6 && String(c, Key("overall:x"), [min, new Vec2(max.X, min.Y)], new Vec2(1, 0), Moved("overall:x", new Vec2(0, min.Y - FirstOffset * mm))) is { } h) yield return h;
        if (max.Y - min.Y > 1e-6 && String(c, Key("overall:y"), [new Vec2(max.X, min.Y), max], new Vec2(0, 1), Moved("overall:y", new Vec2(max.X + FirstOffset * mm, 0))) is { } vy) yield return vy;
        if (part.Dims != "features") yield break;

        var f = part.Frame;
        var features = ElementFeatures.Of(c.Model, c.Services, part.Element, f, part.Body);
        if (features.Count == 0) yield break;
        // The view's axes in the frame: its right, its up, the way it looks.
        Vec3 Local(Vec3 world) => new(Vec3.Dot(world, f.X), Vec3.Dot(world, f.Y), Vec3.Dot(world, f.Z));
        var (r, u, d) = (Local(c.Frame.Right), Local(c.Frame.Up), Local(c.Frame.Dir));
        Vec2 P(Vec3 local) => c.Frame.Map(f.World(local));
        var text = Standards.ResolveText(c.Book, []);
        var color = Standards.ResolveDim(c.Book, []).Color ?? "#000000";
        var above = max.Y + FirstOffset * mm;
        var left = min.X - FirstOffset * mm;

        // Notches seen from the side (their length along the view's right, their depth up it): the length over the drawing, the depth at their end.
        foreach (var n in features.Where(x => x.Kind == "notch"))
        {
            var a = n.At;
            var (pa, pb) = (P(a), P(a + new Vec3(n.Size.X, n.Size.Y, n.Size.Z)));
            if (Math.Abs(Vec3.Dot(new Vec3(1, 0, 0), r)) < 0.99 || Math.Abs(Vec3.Dot(Vec3.UnitZ, u)) < 0.99) continue;
            var (x0, x1) = (Math.Min(pa.X, pb.X), Math.Max(pa.X, pb.X));
            var (y0, y1) = (Math.Min(pa.Y, pb.Y), Math.Max(pa.Y, pb.Y));
            var top = y1 >= max.Y - 1e-6;
            var lk = $"notch:{n.Key}:length";
            if (String(c, Key(lk), [new Vec2(x0, top ? y1 : y0), new Vec2(x1, top ? y1 : y0)], new Vec2(1, 0), Moved(lk, new Vec2(0, top ? above : min.Y - (FirstOffset + NextOffset) * mm))) is { } nl) yield return nl;
            var atStart = x0 <= min.X + 1e-6;
            var dk = $"notch:{n.Key}:depth";
            var dx = atStart ? min.X - (FirstOffset + NextOffset) * mm : max.X + (FirstOffset + NextOffset) * mm;
            if (String(c, Key(dk), [new Vec2(atStart ? x0 : x1, y0), new Vec2(atStart ? x0 : x1, y1)], new Vec2(0, 1), Moved(dk, new Vec2(dx, 0))) is { } nd) yield return nd;
        }
        if (features.Any(x => x.Kind == "notch" && P(x.At + x.Size).Y >= max.Y - 1e-6)) above += NextOffset * mm;

        // The holes seen face on: in runs along the view's right.
        var groups = ElementFeatures.Groups(features, d, r);
        if (groups.Count > 0)
        {
            var pts = new List<Vec2>();
            var texts = new List<string?>();
            var stations = new SortedDictionary<double, Vec2>();
            foreach (var g in groups)
                foreach (var hole in g.Holes) stations.TryAdd(Math.Round(P(hole.At).X, 6), P(hole.At));
            // The chain from the part's end through each run's first and last hole to its other end, a run as "n×p = total".
            var marks = new SortedDictionary<double, string?>();
            var runs = groups.Where(g => g.Spaces >= 2).Select(g => (A: Math.Round(P(g.First.At).X, 6), B: Math.Round(P(g.Last.At).X, 6), g)).ToList();
            foreach (var g in groups) { marks[Math.Round(P(g.First.At).X, 6)] = null; marks[Math.Round(P(g.Last.At).X, 6)] = null; }
            marks[Math.Round(min.X, 6)] = null;
            marks[Math.Round(max.X, 6)] = null;
            var xs = marks.Keys.ToList();
            for (var i = 0; i < xs.Count; i++) pts.Add(new Vec2(xs[i], max.Y));
            for (var i = 0; i + 1 < xs.Count; i++)
            {
                var run = runs.FirstOrDefault(x => Math.Abs(Math.Min(x.A, x.B) - xs[i]) < 1e-6 && Math.Abs(Math.Max(x.A, x.B) - xs[i + 1]) < 1e-6);
                texts.Add(run.g is { } rg ? $"{rg.Spaces}×{Mm(rg.Pitch)} = {Mm(rg.Pitch * rg.Spaces)}" : null);
            }
            if (String(c, Key("holes:chain"), pts, new Vec2(1, 0), Moved("holes:chain", new Vec2(0, above)), texts) is { } chain) yield return chain;
            above += NextOffset * mm;
            // The ordinates of every hole from the part's start.
            var om = MoveOf(v, "holes:ordinate");
            yield return Ordinate(c, Key("holes:ordinate"), new Vec2(min.X, max.Y) + om, [.. stations.Values.Select(p => p + new Vec2(om.X, 0))], above + om.Y);
            // The rows across, from the part's lower edge, on its left.
            var rows = groups.Select(g => Math.Round(P(g.First.At).Y, 6)).Distinct().OrderBy(y => y).ToList();
            var across = new List<Vec2> { new(min.X, min.Y) };
            across.AddRange(rows.Select(y => new Vec2(min.X, y)));
            across.Add(new Vec2(min.X, max.Y));
            if (String(c, Key("holes:across"), across, new Vec2(0, 1), Moved("holes:across", new Vec2(left - (features.Any(x => x.Kind == "notch" && x.End == "start") ? NextOffset * mm : 0), 0))) is { } ac) yield return ac;
            // Each run's count and diameter by its first hole.
            var tags = new ViewMark(Key("holes:tags"));
            foreach (var g in groups)
            {
                var p0 = P(g.First.At);
                var label = (g.Holes.Count > 1 ? $"{g.Holes.Count}× " : "") + $"Ø{Mm(g.First.Diameter)}";
                tags.Text(new PenText(p0 + new Vec2(g.First.Diameter / 2 + 1.5 * mm, g.First.Diameter / 2 + 1 * mm), label, text.Height * 0.85, 0, "start", "bottom", false, text.Font), color);
            }
            if (tags.Texts.Count > 0) yield return tags;
        }

        // An end cut off square: its angle beside it, in the view that sees it (from the front its angle about y, from above about z).
        foreach (var cut in features.Where(x => x.Kind == "cut"))
        {
            var angle = Math.Abs(Vec3.Dot(d, new Vec3(0, 1, 0))) > 0.99 ? cut.ElevAngle : Math.Abs(Vec3.Dot(d, Vec3.UnitZ)) > 0.99 ? cut.PlanAngle : 0;
            if (Math.Abs(angle) < 1e-9) continue;
            var atStart = cut.End == "start";
            var mk = new ViewMark(Key($"cut:{cut.End}"));
            var t = $"{Math.Abs(angle).ToString("0.#", CultureInfo.InvariantCulture)}°";
            // Under the end's corner, past the overall string's tick.
            mk.Text(new PenText(new Vec2(atStart ? min.X - 2 * mm : max.X + 2 * mm, min.Y - 2 * mm) + MoveOf(v, $"cut:{cut.End}"), t, text.Height, 0, atStart ? "end" : "start", "top", false, text.Font), color);
            yield return mk;
        }
    }
}

static class JsonExtensions
{
    public static JsonObject With(this JsonObject o, string key, JsonNode? value) { o[key] = value; return o; }
}
