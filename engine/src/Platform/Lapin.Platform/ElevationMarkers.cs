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

    public const string ShiftKey = "markerShift";

    /// <summary>
    /// How far an elevation's marker was moved (decision 460): its symbol along its line (m, along its right) and the whole
    /// marker out from the building (m, against its look). The elevation sees the same wherever its marker stands.
    /// </summary>
    public static (double Along, double Out) Shift(Entity view) =>
        view.Attrs[ShiftKey] is JsonArray { Count: >= 2 } s ? (Json.Number(s[0]) ?? 0, Json.Number(s[1]) ?? 0) : (0, 0);

    /// <summary>
    /// An elevation's marker moved by <paramref name="by"/> in plan (the Move tool, its symbol dragged): along its line and
    /// out from the building. Unlike Revit's, whose marker line is the view's near clip, a Lapin elevation looks at the
    /// building whole from outside, so moving the marker never changes what it sees: its reach has its own grips
    /// (<c>view.setExtent</c>), and a cut inside the building is a section's.
    /// </summary>
    public static Entity Moved(Entity view, Vec2 by)
    {
        var (d, right) = Axes(view);
        var (along, outward) = Shift(view);
        (along, outward) = (Math.Round(along + Vec2.Dot(by, right), 6), Math.Round(outward - Vec2.Dot(by, d), 6));
        var attrs = view.CloneAttrs();
        if (Math.Abs(along) < 1e-9 && Math.Abs(outward) < 1e-9) attrs.Remove(ShiftKey);
        else attrs[ShiftKey] = new JsonArray(along, outward);
        return view.With(attrs: attrs);
    }

    /// <summary>The plan direction an elevation looks along, and its right (looking along it).</summary>
    static (Vec2 Look, Vec2 Right) Axes(Entity view)
    {
        var d = Views.Look(view) ?? new Vec2(0, -1);
        return (d, new Vec2(d.Y, -d.X));
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
            var (along, outward) = Shift(v);
            var front = corners.Length > 0 ? corners.Min(p => Vec2.Dot(p, d)) : v.Num("planeDepth");
            var depth = front - (1.5 * u + 25 * c.Mm) - outward;
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
            };
            yield return mk;
        }
    }

    public static void Register(IModuleRegistry r)
    {
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
