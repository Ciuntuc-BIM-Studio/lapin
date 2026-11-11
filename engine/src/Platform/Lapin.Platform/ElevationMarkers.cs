using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// An elevation's marker in plans (decision 456, a tester's request): a fine chain line standing across the elevation's
/// look in front of the building, as far as the elevation reaches, and at its middle the symbol (a circle, its filled
/// arrow pointing the way the elevation looks) with the elevation's name behind it, sized as the section markers are
/// (<see cref="SectionMarkers.Sizes"/>). The line's ends set how far the elevation reaches: <c>extentMin</c> and
/// <c>extentMax</c>, along its right from the model's origin (metres), the drawing seeing only between them
/// (<see cref="Lapin.Drawing.Views.ViewSpec.Span"/>); without them, the whole building. Hidden with the elevation marks'
/// annotation category. Its mark tells the interface its line (<c>data</c>: ends, right, min, max, own) for its grips.
/// </summary>
public static class ElevationMarkers
{
    public const string MinKey = "extentMin", MaxKey = "extentMax";

    /// <summary>How far an elevation reaches along its right (from the model's origin), when it is set.</summary>
    public static (double Min, double Max)? Extent(Entity view) =>
        view.Str("kind") == "elevation" && view.NumOrNull(MinKey) is { } lo && view.NumOrNull(MaxKey) is { } hi && hi > lo ? (lo, hi) : null;

    public const string ShiftKey = "markerShift", DepthKey = "markerDepth", AlongKey = "markerAlong";

    /// <summary>
    /// Where an elevation's marker was put (decision 460): its symbol along its line (m, along its right, from the
    /// line's middle) and, once moved, its line's place along the look (<c>markerDepth</c>, m from the model's origin);
    /// decision 460's <c>markerShift</c> [along, out] is read as it was kept.
    /// </summary>
    public static (double Along, double? Depth, double Out) Placed(Entity view)
    {
        var (along, outward) = view.Attrs[ShiftKey] is JsonArray { Count: >= 2 } s ? (Json.Number(s[0]) ?? 0, Json.Number(s[1]) ?? 0) : (0, 0);
        return (view.NumOrNull(AlongKey) ?? along, view.NumOrNull(DepthKey), outward);
    }

    /// <summary>The marker line's place along the look where it stands unmoved: a symbol and a half and 25 mm on paper in front of <paramref name="front"/>.</summary>
    public static double DefaultDepth(double front, double size, double mmPerPaper) => front - (1.5 * size + 25) * mmPerPaper;

    /// <summary>
    /// The plane an elevation starts seeing at (decision 460, as Revit's): its marker's line, when it was moved into the
    /// building (past <paramref name="front"/>, the nearest the model comes along the look); null when it stands outside,
    /// where the elevation sees the whole building.
    /// </summary>
    public static double? Clip(Entity view, double front) => Placed(view).Depth is { } depth && depth > front + 1e-6 ? depth : null;

    /// <summary>
    /// An elevation's marker put at <paramref name="depth"/> along its look and <paramref name="along"/> along its line
    /// (null: where it is). Moved into the building, the elevation starts seeing at its line (<see cref="Clip"/>).
    /// </summary>
    public static Entity Placed(Entity view, double? depth, double? along)
    {
        var attrs = view.CloneAttrs();
        var (a0, _, _) = Placed(view);
        attrs.Remove(ShiftKey);
        var a1 = Math.Round(along ?? a0, 6);
        if (Math.Abs(a1) < 1e-9) attrs.Remove(AlongKey); else attrs[AlongKey] = a1;
        if (depth is { } d) attrs[DepthKey] = Math.Round(d, 6);
        return view.With(attrs: attrs);
    }

    /// <summary>
    /// An elevation's marker moved by <paramref name="by"/> in plan (the Move tool): along its line and along its look,
    /// from where it stands (unmoved: in front of the model, as plans draw it at the elevation's scale).
    /// </summary>
    public static Entity Moved(IQueryContext q, Entity view, Vec2 by)
    {
        var (d, right) = Axes(view);
        var (along, depth, outward) = Placed(view);
        var now = depth ?? DefaultDepth(Views.ModelFront(q, view) ?? 0, SectionMarkers.Sizes(DraftingResources.Book(q.Model), view, Standards.ResolveText(DraftingResources.Book(q.Model), [])).Size, (view.NumOrNull("scale") ?? 100) / 1000) - outward;
        return Placed(view, now + Vec2.Dot(by, d), along + Vec2.Dot(by, right));
    }

    /// <summary>The plan direction an elevation looks along, and its right (looking along it).</summary>
    static (Vec2 Look, Vec2 Right) Axes(Entity view)
    {
        var d = Views.Look(view) ?? new Vec2(0, -1);
        return (d, new Vec2(d.Y, -d.X));
    }

    /// <summary>
    /// Whether an elevation sees a point at all: within its reach (<see cref="Extent"/>) and beyond its marker's line when
    /// that clips it (decision 460); what a module draws over an elevation (an opening's symbol) asks it.
    /// </summary>
    public static bool Sees(ViewContext c, Vec3 p)
    {
        if (c.Kind != "elevation") return true;
        if (c.Frame.Depth(p) < -1e-6) return false;
        if (Extent(c.View) is not var (lo, hi)) return true;
        var o = Vec3.Dot(c.Frame.Origin, c.Frame.Right);
        var x = c.Map(p).X;
        return x >= lo - o - 1e-6 && x <= hi - o + 1e-6;
    }

    /// <summary>Every elevation's marker in a plan.</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c, TextStyle text)
    {
        if (c.Kind != "plan") yield break;
        var bounds = c.Drawing.Bounds;
        foreach (var v in c.Model.OfClass(Views.Class).Where(v => v.Str("kind") == "elevation"))
        {
            var (d, right) = Axes(v);
            var (size, textHeight) = SectionMarkers.Sizes(c.Book, v, text);
            var u = size * c.Mm;
            Vec2[] corners = bounds is { } b ? [b.Min, new(b.Max.X, b.Min.Y), b.Max, new(b.Min.X, b.Max.Y)] : [];
            var own = Extent(v);
            if (own == null && corners.Length == 0) continue;
            // In front of the building as the elevation sees it, clear of the sections' heads there (a symbol and a half and
            // 25 mm on paper away, decision 460), on its sketch plane's front when it has nothing to see; moved, as far as it was.
            var (along, placed, outward) = Placed(v);
            var front = corners.Length > 0 ? corners.Min(p => Vec2.Dot(p, d)) : v.Num("planeDepth");
            var depth = placed ?? DefaultDepth(front, size, c.Mm) - outward;
            var (lo, hi) = own ?? (corners.Min(p => Vec2.Dot(p, right)) - 0.5 * u, corners.Max(p => Vec2.Dot(p, right)) + 0.5 * u);
            Vec2 At(double t) => right * t + d * depth;
            var (a, bEnd) = (At(lo), At(hi));
            var mk = new ViewMark(v.Id);
            mk.Line(a, bEnd, "ELEVATION", Pens.Default.Hatch, dash: LineStyles.DashOf("center"));
            // The symbol behind the line's middle: a circle of the symbol's size, its arrow filled, pointing at the building.
            var r = u / 2;
            var centre = At((lo + hi) / 2 + along) - d * r;
            mk.Polyline(ViewMark.Circle(centre, r, 32), true, "ELEVATION", Pens.Default.Seen);
            Vec2[] arrow = [centre + d * (1.6 * r), centre + right * (0.8 * r), centre - right * (0.8 * r)];
            mk.Fills.Add(([arrow], "#000000"));
            mk.Polyline(arrow, true, "ELEVATION", Pens.Default.Seen);
            // Its name behind the symbol, away from the building.
            var away = d * -1;
            var gap = r + 0.3 * r + (Math.Abs(away.Y) > 0.5 ? 0.6 * textHeight * c.Mm : 0);
            var anchor = away.X > 0.5 ? "start" : away.X < -0.5 ? "end" : "middle";
            mk.Text(new PenText(centre + away * gap, v.Name, textHeight, Anchor: anchor, Baseline: "middle", Font: text.Font));
            mk.Snaps.AddRange([a, bEnd]);
            mk.Data = new JsonObject
            {
                ["ends"] = new JsonArray(new JsonArray(Math.Round(a.X, 6), Math.Round(a.Y, 6)), new JsonArray(Math.Round(bEnd.X, 6), Math.Round(bEnd.Y, 6))),
                ["right"] = new JsonArray(Math.Round(right.X, 9), Math.Round(right.Y, 9)),
                ["min"] = Math.Round(lo, 6), ["max"] = Math.Round(hi, 6), ["own"] = own != null,
                ["symbol"] = new JsonArray(Math.Round(centre.X, 6), Math.Round(centre.Y, 6)),
                ["depth"] = Math.Round(depth, 6), ["along"] = Math.Round(along, 6),
            };
            yield return mk;
        }
    }

    public static void Register(IModuleRegistry r)
    {
        // An elevation's marker put: {id, depth?: its line along the look (m), along?: its symbol along the line (m)}. Into the
        // building, the elevation starts seeing at its line (decision 460). One undo step; pinned, refused.
        r.Command("view.moveMarker", "Move elevation marker", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Views.Class } x && x.Str("kind") == "elevation" ? x : throw new CommandException("Only an elevation's marker is moved this way.");
            Pins.Refuse([v]);
            double? Num(string key) => a[key] is null ? null : Json.Number(a[key]) is { } n && double.IsFinite(n) ? n : throw new CommandException("A marker's place is in metres.");
            ctx.Replace(Placed(v, Num("depth"), Num("along")));
            return null;
        });

        // How far an elevation reaches: {id, min?, max?} along its right from the model's origin (m); null clears an end, both cleared the whole building.
        r.Command("view.setExtent", "Elevation extent", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Views.Class } x && x.Str("kind") == "elevation" ? x : throw new CommandException("Only an elevation's line is stretched or shortened.");
            var attrs = v.CloneAttrs();
            foreach (var (arg, key) in new[] { ("min", MinKey), ("max", MaxKey) })
            {
                if (!a.ContainsKey(arg)) continue;
                if (a[arg] == null) { attrs.Remove(key); continue; }
                attrs[key] = Json.Number(a[arg]) is { } n && double.IsFinite(n) ? Math.Round(n, 6) : throw new CommandException("An elevation's end is a length.");
            }
            var (lo, hi) = (Json.Number(attrs[MinKey]), Json.Number(attrs[MaxKey]));
            if ((lo == null) != (hi == null)) throw new CommandException("An elevation reaches between two ends: give both.");
            if (lo is { } l && hi is { } h && h - l < 0.01) throw new CommandException("An elevation's ends must lie apart, the first before the second.");
            ctx.Replace(v.With(attrs: attrs));
            return null;
        });
    }
}
