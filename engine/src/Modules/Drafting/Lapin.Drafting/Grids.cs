using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>A grid axis: its tag (1, A, A1…) and its line in plan (m).</summary>
public sealed record Axis(string Tag, Vec2 A, Vec2 B)
{
    public Vec2 Dir => (B - A).Normalized();
    public JsonObject ToJson() => new() { ["tag"] = Tag, ["a"] = new JsonArray(A.X, A.Y), ["b"] = new JsonArray(B.X, B.Y) };
}

/// <summary>
/// The project's grids: an IfcGrid holds its axes, in plan, for every storey.
/// They are drawn in the project's default axis type (Drafting standards):
/// its bubble, ends, size, line type, colour and label text style, sized from
/// the view's scale. Plans show the axes; sections the axes they cross;
/// elevations those that run towards the eye.
/// </summary>
public static class Grids
{
    public static List<Axis> Axes(Entity grid) => grid.Attrs["axes"] is JsonArray arr
        ? arr.OfType<JsonObject>().Select(o => new Axis((string?)o["tag"] ?? "?", P(o["a"]), P(o["b"]))).ToList()
        : [];

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;

    public static Entity Ensure(ICommandContext ctx)
    {
        if (ctx.Model.OfClass(IfcClass.Grid).FirstOrDefault() is { } g) return g;
        var grid = new Entity(ctx.NewId(), IfcClass.Grid, "Grid", new JsonObject { ["axes"] = new JsonArray() });
        ctx.Add(grid);
        return grid;
    }

    /// <summary>The label after <paramref name="tag"/>: 1 → 2, 9 → 10, A → B, Z → AA, A1 → A2.</summary>
    public static string Next(string tag)
    {
        var m = System.Text.RegularExpressions.Regex.Match(tag, @"^(.*?)(\d+)$");
        if (m.Success) return m.Groups[1].Value + (long.Parse(m.Groups[2].Value) + 1);
        if (tag.Length == 0) return "1";
        var chars = tag.ToCharArray();
        for (var i = chars.Length - 1; i >= 0; i--)
        {
            if (chars[i] is >= 'A' and < 'Z' or >= 'a' and < 'z') { chars[i]++; return new string(chars); }
            if (chars[i] is 'Z' or 'z') { chars[i] = chars[i] == 'Z' ? 'A' : 'a'; continue; }
            return tag + "1";
        }
        return (char.IsUpper(tag[0]) ? "A" : "a") + new string(chars);
    }

    /// <summary>
    /// A new axis's tag: after the last axis that runs the same way; the first
    /// axis of a direction takes the axis type's first label, or letters when
    /// another direction already counts in numbers (and the other way round).
    /// </summary>
    public static string NextTag(IReadOnlyList<Axis> axes, Vec2 dir, string first)
    {
        var same = axes.Where(x => Math.Abs(Vec2.Cross(x.Dir, dir)) < 0.02).ToList();
        if (same.Count > 0) return Next(same[^1].Tag);
        var taken = axes.Select(x => x.Tag).ToHashSet();
        if (axes.Count == 0 || !taken.Contains(first)) return first;
        return char.IsDigit(first[0]) ? "A" : "1";
    }

    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        var axisStyle = Standards.ResolveAxis(c.Book, new JsonObject { ["axisStyle"] = c.Book.DefaultAxisStyle });
        var text = Standards.ResolveText(c.Book, new JsonObject { ["axisStyle"] = c.Book.DefaultAxisStyle });
        var color = axisStyle.Color ?? "#000000";
        var dash = Standards.DashOf(axisStyle.LineType);
        var r = axisStyle.Size * c.Mm / 2;
        // Sections and elevations run the axes over what they draw, else over the storeys.
        var storeys = Spatial.Storeys(c.Model).ToList();
        (double Y0, double Y1) span = c.Drawing.Bounds is { } drawn ? (drawn.Min.Y, drawn.Max.Y)
            : storeys.Count > 0 ? (storeys.Min(x => x.Num("elevation")), storeys.Max(x => x.Num("elevation") + x.Num("height", 3))) : (0, 3);
        foreach (var grid in c.Model.OfClass(IfcClass.Grid))
            foreach (var axis in Axes(grid))
            {
                Vec2 a, b;
                if (c.Kind == "plan") (a, b) = (axis.A, axis.B);
                else
                {
                    var k = c.Scale / 100;
                    double x;
                    if (c.Kind == "section")
                    {
                        // Where the axis crosses the section's plane.
                        var (da, db) = (c.Frame.Depth(new Vec3(axis.A.X, axis.A.Y, 0)), c.Frame.Depth(new Vec3(axis.B.X, axis.B.Y, 0)));
                        if (Math.Abs(da - db) < 1e-9 || da * db > 0) continue;
                        var t = da / (da - db);
                        var p = axis.A + (axis.B - axis.A) * t;
                        x = c.Map(p).X;
                    }
                    else
                    {
                        // An elevation sees the axes that run towards it.
                        var look = new Vec2(c.Frame.Dir.X, c.Frame.Dir.Y);
                        if (Math.Abs(Vec2.Dot(axis.Dir, look)) < 0.7) continue;
                        x = c.Map(axis.A).X;
                    }
                    (a, b) = (new Vec2(x, span.Y0 - 0.5 * k), new Vec2(x, span.Y1 + 1.0 * k));
                }
                var mk = new ViewMark(grid.Id);
                mk.Line(a, b, "GRID", Pens.Default.Hidden, color, dash);
                var u = (b - a).Normalized();
                var ends = c.Kind == "plan" ? axisStyle.Ends : "end";
                foreach (var (end, outward, on) in new[] { (a, -u, ends is "both" or "start"), (b, u, ends is "both" or "end") })
                {
                    if (!on) continue;
                    var centre = end + outward * r;
                    var outline = axisStyle.Bubble == "square"
                        ? new[] { centre + new Vec2(-r, -r), centre + new Vec2(r, -r), centre + new Vec2(r, r), centre + new Vec2(-r, r) }
                        : ViewMark.Circle(centre, r);
                    mk.Polyline(outline, true, "GRID", Pens.Default.Seen, color);
                    mk.Text(new PenText(centre, axis.Tag, Math.Min(text.Height, axisStyle.Size * 0.55), Baseline: "middle", Bold: text.Bold, Font: text.Font), color);
                }
                mk.Snaps.AddRange([a, b]);
                yield return mk;
            }
    }
}
