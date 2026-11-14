using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// A part's dimensions on its element views (docs/plan-shop-drawings.md §5.2, decisions 581–582): derived on every draw from the
/// element as it is, never kept, so they follow the part. <c>of.dims</c> says which: <c>overall</c> (its length and height under
/// and beside the drawing, in the frame's axes), nothing for none. They draw in the drafting standards' dimension style, as the
/// user's own dimensions do (<see cref="Dimensions.Draw"/>); their owner is the view's (<c>view:partdim:key</c>), so the view's
/// annotation category for dimensions hides them.
/// </summary>
public static class PartDimensions
{
    /// <summary>How far the first string stands off the drawing (paper mm), and the next from it.</summary>
    public const double FirstOffset = 7, NextOffset = 6;

    public static string Owner(Entity view, string key) => $"{view.Id}:partdim:{key}";

    /// <summary>The element view's part, its frame and the box's corners as the view draws them; null when it is not an element view with dimensions.</summary>
    static (Entity Element, ElementFrame Frame, Vec2 Min, Vec2 Max)? Of(ViewContext c)
    {
        if (!ElementViews.Is(c.View) || ElementViews.RoleOf(c.View) == ElementViews.Iso) return null;
        if ((string?)c.View.Attrs[ElementViews.Key]?["dims"] is not { } dims || dims == "none") return null;
        if (c.Model.Get(ElementViews.ElementOf(c.View) ?? "") is not { } e || c.Geometry?.Invoke(e.Id) is not { } g) return null;
        var body = g.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        if (ElementFrames.Of(c.Model, c.Services, e, body) is not { } f) return null;
        var corners = new[] { f.Min.X, f.Max.X }.SelectMany(x => new[] { f.Min.Y, f.Max.Y }.SelectMany(y => new[] { f.Min.Z, f.Max.Z }.Select(z => c.Frame.Map(f.World(new Vec3(x, y, z)))))).ToList();
        return (e, f, new Vec2(corners.Min(p => p.X), corners.Min(p => p.Y)), new Vec2(corners.Max(p => p.X), corners.Max(p => p.Y)));
    }

    /// <summary>A string along <paramref name="u"/> through <paramref name="pts"/>, its line at <paramref name="at"/>.</summary>
    public static ViewMark? String(ViewContext c, string owner, IReadOnlyList<Vec2> pts, Vec2 u, Vec2 at)
    {
        var n = u.Perp;
        var off = pts.Select(p => Vec2.Dot(at - p, n)).ToList();
        var lay = new Dimensions.Layout(u, n, pts.Select((p, i) => p + n * off[i]).ToList(), off);
        List<WallOpening>? none = [];
        return Dimensions.Draw(c, owner, pts, lay, new JsonObject(), null, null, false, ref none);
    }

    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (Of(c) is not var (_, _, min, max)) yield break;
        var mm = c.Mm;
        // The overall length under the drawing, the height on its right.
        if (max.X - min.X > 1e-6 && String(c, Owner(c.View, "overall:x"), [min, new Vec2(max.X, min.Y)], new Vec2(1, 0), new Vec2(0, min.Y - FirstOffset * mm)) is { } h) yield return h;
        if (max.Y - min.Y > 1e-6 && String(c, Owner(c.View, "overall:y"), [new Vec2(max.X, min.Y), max], new Vec2(0, 1), new Vec2(max.X + FirstOffset * mm, 0)) is { } v) yield return v;
    }
}
