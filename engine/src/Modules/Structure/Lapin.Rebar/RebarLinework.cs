using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Drawing.Views;

namespace Lapin.Rebar;

/// <summary>
/// Drafted lines and polylines become bars (docs/plan-user-extensibility.md §6.1): on any 2D view, each line or polyline a bar of
/// the diameter and steel chosen, its ends hooked as asked. A shape the catalogue knows is recognised with its parameters and grips
/// — two points a straight bar, three with a square corner an L, four with two square corners and equal legs a U, a closed rectangle
/// a stirrup —, anything else a polyline bar through the same points. One undo step for all.
/// </summary>
public static class RebarLinework
{
    const double Square = 1e-3;

    /// <summary>A bar's arguments for a run of points (open, or closed back to its start): the shape it is, where it stands, its parameters or vertices.</summary>
    public static JsonObject Recognise(IReadOnlyList<Vec2> pts, bool closed, bool recognise)
    {
        static double Angle(Vec2 d) => Math.Round(Math.Atan2(d.Y, d.X) * 180 / Math.PI, 9);
        static bool Right(Vec2 u, Vec2 v) => Math.Abs(Vec2.Dot(u.Normalized(), v.Normalized())) < Square;
        static double Cross(Vec2 u, Vec2 v) => u.X * v.Y - u.Y * v.X;
        static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));
        var o = new JsonObject();
        if (recognise)
        {
            if (!closed && pts.Count == 2)
            {
                o["shape"] = "straight"; o["at"] = J(pts[0]); o["rotation"] = Angle(pts[1] - pts[0]);
                o["params"] = new JsonObject { ["length"] = Math.Round((pts[1] - pts[0]).Length, 9) };
                return o;
            }
            if (!closed && pts.Count == 3 && Right(pts[1] - pts[0], pts[2] - pts[1]))
            {
                var (u, v) = (pts[1] - pts[0], pts[2] - pts[1]);
                o["shape"] = "L"; o["at"] = J(pts[0]); o["rotation"] = Angle(u);
                o["params"] = new JsonObject { ["a"] = Math.Round(u.Length, 9), ["b"] = Math.Round(v.Length, 9) };
                // Turning right: the L turned over across its own x.
                if (Cross(u, v) < 0) o["mirrorY"] = true;
                return o;
            }
            if (!closed && pts.Count == 4)
            {
                var (l1, w, l2) = (pts[0] - pts[1], pts[2] - pts[1], pts[3] - pts[2]);
                if (Right(l1, w) && Right(w, l2) && Vec2.Dot(l1, l2) > 0 && Math.Abs(l1.Length - l2.Length) < 1e-4)
                {
                    // The U's origin is its first corner, its x along the bottom, its legs up (+y) or, turned over, down.
                    o["shape"] = "U"; o["at"] = J(pts[1]); o["rotation"] = Angle(w);
                    o["params"] = new JsonObject { ["a"] = Math.Round(w.Length, 9), ["b"] = Math.Round(l1.Length, 9) };
                    if (Cross(w, l1) < 0) o["mirrorY"] = true;
                    return o;
                }
            }
            if (closed && pts.Count == 4 && Enumerable.Range(0, 4).All(i => Right(pts[(i + 1) % 4] - pts[i], pts[(i + 2) % 4] - pts[(i + 1) % 4])))
            {
                // A stirrup's origin is its bottom-left corner, the inside on its left: run the outline counter-clockwise.
                var ring = Polygon2.SignedArea(pts) < 0 ? pts.Reverse().ToList() : pts.ToList();
                // From the corner whose side runs nearest to +x: its width a along the drawing, its height b up it.
                var k = Enumerable.Range(0, 4).OrderBy(i => Math.Abs(Math.Atan2((ring[(i + 1) % 4] - ring[i]).Y, (ring[(i + 1) % 4] - ring[i]).X))).First();
                ring = [.. ring.Skip(k), .. ring.Take(k)];
                var (x, y) = (ring[1] - ring[0], ring[3] - ring[0]);
                o["shape"] = "stirrup"; o["at"] = J(ring[0]); o["rotation"] = Angle(x);
                o["params"] = new JsonObject { ["a"] = Math.Round(x.Length, 9), ["b"] = Math.Round(y.Length, 9) };
                return o;
            }
        }
        // Any other run: a polyline bar through its points, from its first.
        var run = closed ? [.. pts, pts[0]] : pts.ToList();
        o["shape"] = "polyline"; o["at"] = J(run[0]); o["rotation"] = 0;
        o["vertices"] = new JsonArray(run.Select(p => (JsonNode)J(p - run[0])).ToArray());
        return o;
    }

    /// <summary>
    /// An element's cut outline in a view (its outer loop, counter-clockwise), for bars laid along its edges: the cut that holds
    /// <paramref name="at"/>, else the host's own (<paramref name="hostId"/>); null when the view cuts neither.
    /// </summary>
    public static (string Host, List<Vec2> Loop)? Outline(ICommandContext ctx, Entity view, string? hostId, Vec2? at)
    {
        var (drawing, _, _) = Views.Generate(ctx.Query, view);
        if (drawing == null) return null;
        foreach (var cut in drawing.Cuts)
        {
            if (cut.Loops.Count == 0) continue;
            if (hostId != null ? cut.Element != hostId : at is not { } p || !Polygon2.Inside(cut.Loops.Select(l => l.ToArray()).ToArray(), p)) continue;
            var outer = cut.Loops.OrderByDescending(l => Math.Abs(Polygon2.SignedArea(l))).First().ToList();
            if (Polygon2.SignedArea(outer) < 0) outer.Reverse();
            return (cut.Element, outer);
        }
        return null;
    }

    /// <summary>
    /// Bars along an element's edges as a view cuts it (docs/plan-user-extensibility.md §6.2): the outline brought in by the cover and
    /// half the bar; all its edges a closed bar (a rectangle a stirrup), some of them (each the side from its corner to the next) an
    /// open bar along them, stopping at the cover from the faces it meets.
    /// </summary>
    public static JsonObject AlongOutline(List<Vec2> outer, IReadOnlyList<int>? edges, double inset)
    {
        // Outward positive (Polygon2.Offset): inward by the inset.
        var inner = Polygon2.Offset(outer, [-inset]);
        if (Math.Abs(Polygon2.SignedArea(inner)) < 1e-6 || Polygon2.SignedArea(inner) < 0) throw new CommandException("The element is too small for the cover and the bar.");
        if (edges == null || edges.Count >= outer.Count) return Recognise([.. inner], true, true);
        var n = outer.Count;
        var set = edges.Select(i => ((i % n) + n) % n).Distinct().ToHashSet();
        // A run of sides that follow one another: from the first side whose previous one is not taken (the run round the corner of the loop).
        var start = Enumerable.Range(0, n).FirstOrDefault(i => set.Contains(i) && !set.Contains((i - 1 + n) % n), -1);
        if (start < 0) return Recognise([.. inner], true, true);
        var pts = new List<Vec2> { inner[start] };
        for (var k = 0; k < n && set.Contains((start + k) % n); k++) pts.Add(inner[(start + k + 1) % n]);
        if (pts.Count - 1 != set.Count) throw new CommandException("The edges chosen do not follow one another round the outline.");
        return Recognise(pts, false, true);
    }

    public static readonly LineworkConverter Bars = new("rebar", "Reinforcement bars", "rebar", "any", ["plan", "section", "elevation", "drafting", "detail"],
    [
        new("diameter", "Diameter", "length", JsonValue.Create(0.012)),
        new("steel", "Steel", "choice", JsonValue.Create(RebarShapes.DefaultSteel), RebarShapes.Steels),
        new("hooks", "Hooks at both ends", "choice", JsonValue.Create("none"), ["none", "90", "135", "180"]),
        new("count", "Bars", "number", JsonValue.Create(1)),
        new("recognise", "Recognise straight, L, U and stirrup shapes", "bool", JsonValue.Create(true)),
    ],
    (q, view, s, o) =>
    {
        if (s.Kind == "circle") throw new CommandException("A circle is not a bar: draw it as a polyline.");
        var recognise = o["recognise"] is not JsonValue rv || !rv.TryGetValue<bool>(out var r) || r;
        var args = Recognise(s.Points, s.Closed, recognise && s.Kind != "arc");
        args["viewId"] = view.Id;
        var d = LineworkConversion.Num(o, "diameter") ?? 0.012;
        args["diameter"] = d;
        if ((string?)o["steel"] is { Length: > 0 } steel) args["steel"] = steel;
        if (LineworkConversion.Num(o, "count") is { } n && n >= 1) args["count"] = Math.Round(n);
        if ((string?)o["hooks"] is "90" or "135" or "180" && double.TryParse((string?)o["hooks"], System.Globalization.CultureInfo.InvariantCulture, out var angle))
        {
            var hook = new JsonObject { ["angle"] = angle, ["length"] = RebarShapes.HookLength(d, angle) };
            args["hooks"] = new JsonObject { ["start"] = hook.DeepClone(), ["end"] = hook.DeepClone() };
        }
        return [new("rebar.add", args, $"A {(string)args["shape"]!} bar Ø{Math.Round(d * 1000)} on {view.Name}")];
    }, "A bar along each line or polyline, a known shape recognised.");
}
