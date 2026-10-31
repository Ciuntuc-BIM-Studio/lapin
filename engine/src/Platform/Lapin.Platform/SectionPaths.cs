using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>A path a section can follow: the element's line in plan and the station its start is at (a road's chainage; 0 for the rest).</summary>
public sealed record FollowedPath(IReadOnlyList<Vec2> Points, double Station0 = 0);

/// <summary>A module's elements a section can follow (a road's axis, a ditch, a drafted polyline): the element's path, or null when it has none.</summary>
public interface ISectionPaths
{
    FollowedPath? PathOf(IModelView m, Entity e);
}

/// <summary>
/// A section's line in plan (docs/sections.md, decision 176): two points as
/// before, or more (a jogged section: each straight piece a planar cut seen
/// only between its ends, the pieces laid side by side at their lengths, a
/// developed section), an arc through three points (a curved section, sampled
/// to pieces), or the path of an element it follows (a road's axis, a wall, a
/// drafted polyline: an alignment), read live, with the stations along it.
/// The view keeps <c>pts</c>, <c>curve</c> ("arc") and <c>along</c> (the
/// element); <c>start</c> and <c>end</c> stay its first and last point, so
/// whatever reads a section as one line still does.
/// </summary>
public static class SectionPaths
{
    public const string ArcCurve = "arc";

    /// <summary>One straight piece of the line: its ends, its length and the station (the developed x) where it starts.</summary>
    public sealed record Piece(Vec2 A, Vec2 B, double Length, double Station);

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;

    public static JsonArray PointsJson(IEnumerable<Vec2> pts) => new(pts.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 6), Math.Round(p.Y, 6))).ToArray());

    /// <summary>Consecutive points that coincide made one.</summary>
    public static List<Vec2> Clean(IEnumerable<Vec2> pts)
    {
        var o = new List<Vec2>();
        foreach (var p in pts) if (o.Count == 0 || (p - o[^1]).Length > 1e-6) o.Add(p);
        return o;
    }

    /// <summary>The points the view keeps: <c>pts</c>, else its start and end; empty when it has neither.</summary>
    public static List<Vec2> Points(Entity view)
    {
        if (view.Attrs["pts"] is JsonArray pts && pts.Count >= 2) return Clean(pts.Select(P));
        if (view.Attrs["start"] is JsonArray s && view.Attrs["end"] is JsonArray e) return Clean([P(s), P(e)]);
        return [];
    }

    public static bool IsArc(Entity view) => view.Str("curve") == ArcCurve;
    public static string? Along(Entity view) => view.Str("along");

    /// <summary>Whether the section is more than one straight line: jogged, curved, or along an element.</summary>
    public static bool Developed(Entity view) => IsArc(view) || Along(view) != null || Points(view).Count > 2;

    /// <summary>
    /// The line the section cuts along, as it is now: the element's path when it follows one (and the element is there),
    /// the arc through its three points sampled, else its points. Null with the reason when there is none.
    /// </summary>
    public static (List<Vec2>? Path, double Station0, string? Problem) Resolve(IModelView m, IServices? services, Entity view)
    {
        // Across an element: its line stands across the axis at the station, as the element is now (none for a horizontal cut).
        if (AcrossSections.Is(view)) return AcrossSections.Line(m, services, view) is { } across ? (across, 0, null) : (null, 0, "A horizontal cut has no line in plan.");
        if (Along(view) is { } id)
        {
            if (m.Get(id) is { } e && PathOfElement(m, services, e) is { } followed)
                return followed.Points.Count >= 2 ? (Clean(followed.Points), followed.Station0, null) : (null, 0, "The element the section follows has no line.");
            // The element gone: the line as it was when the section last followed it.
        }
        var pts = Points(view);
        if (IsArc(view) && pts.Count >= 3) pts = SampleArc(pts[0], pts[1], pts[2]);
        if (pts.Count < 2) return (null, 0, "A section needs its line.");
        return (pts, 0, null);
    }

    /// <summary>The element's path as a section follows it: a module's (a road, a drafted shape), else a path element's axis (a wall, a beam, an arc sampled).</summary>
    public static FollowedPath? PathOfElement(IModelView m, IServices? services, Entity e)
    {
        if (services != null)
            foreach (var s in services.All<ISectionPaths>())
                if (s.PathOf(m, e) is { } p) return p;
        if (services?.Get<IPathElements>()?.IsPath(e.Class) == true && e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray)
        {
            var c = PathCurve.Of(e);
            return new FollowedPath(c.IsArc ? c.Line(0, c.Length, 0) : [c.Start, c.End]);
        }
        return null;
    }

    /// <summary>The straight pieces of a line, each at the station (developed x) where it starts.</summary>
    public static List<Piece> Pieces(IReadOnlyList<Vec2> path)
    {
        var o = new List<Piece>();
        var at = 0.0;
        for (var i = 0; i + 1 < path.Count; i++)
        {
            var len = (path[i + 1] - path[i]).Length;
            if (len < 1e-9) continue;
            o.Add(new Piece(path[i], path[i + 1], len, at));
            at += len;
        }
        return o;
    }

    public static double Length(IReadOnlyList<Vec2> path) => Pieces(path).Sum(p => p.Length);

    /// <summary>
    /// The arc from <paramref name="a"/> through <paramref name="b"/> to <paramref name="c"/> as straight pieces, a step of at most 7.5° (at least 4,
    /// at most 48 pieces); the three points themselves when they lie in a line.
    /// </summary>
    public static List<Vec2> SampleArc(Vec2 a, Vec2 b, Vec2 c)
    {
        var rise = PathCurve.RiseThrough(a, c, b);
        var curve = new PathCurve(a, c, rise);
        if (!curve.IsArc) return Clean([a, b, c]);
        var n = Math.Clamp((int)Math.Ceiling(curve.Sweep / (7.5 * Math.PI / 180)), 4, 48);
        return Clean(Enumerable.Range(0, n + 1).Select(k => curve.PointAt(curve.Length * k / n)));
    }

    /// <summary>The section's line from a command's arguments: pts (two or more points), or start and end; with the curve and the element followed.</summary>
    public static void Set(IModelView m, IServices services, JsonObject attrs, JsonObject a, bool create)
    {
        List<Vec2>? pts = null;
        if (a["pts"] is JsonArray arr)
        {
            pts = Clean(arr.Select(n => n is JsonArray p && p.Count >= 2 && Json.Number(p[0]) is { } x && Json.Number(p[1]) is { } y ? new Vec2(x, y)
                : throw new CommandException("A section's points are [[x, y], …].")));
        }
        else if (a["start"] != null || a["end"] != null || create)
        {
            var s = a["start"] ?? attrs["start"];
            var e = a["end"] ?? attrs["end"];
            if (s is JsonArray && e is JsonArray) pts = Clean([P(s), P(e)]);
            else if (create && a["along"] == null) throw new CommandException("A section needs its line: start and end, or pts.");
        }
        if (a.ContainsKey("curve")) { if ((string?)a["curve"] is ArcCurve) attrs["curve"] = ArcCurve; else attrs.Remove("curve"); }
        if (a.ContainsKey("along"))
        {
            if ((string?)a["along"] is { Length: > 0 } id)
            {
                var e = m.Get(id) ?? throw new CommandException("There is no such element to follow.");
                var followed = PathOfElement(m, services, e) ?? throw new CommandException($"A section cannot follow {e.Name}: it has no line in plan (a road, a wall, a beam, a drafted line or polyline can be followed).");
                attrs["along"] = id;
                attrs.Remove("curve");
                pts = Clean(followed.Points);
            }
            else attrs.Remove("along");
        }
        if (a.ContainsKey("stations"))
        {
            if (Json.Number(a["stations"]) is { } st && st > 0) attrs["stations"] = st; else attrs.Remove("stations");
        }
        if (pts != null)
        {
            if (pts.Count < 2) throw new CommandException("A section needs two different points.");
            if (attrs["curve"] is not null && pts.Count != 3) throw new CommandException("A curved section goes through three points: its start, a point on the arc, its end.");
            attrs["pts"] = PointsJson(pts);
            attrs["start"] = new JsonArray(Math.Round(pts[0].X, 6), Math.Round(pts[0].Y, 6));
            attrs["end"] = new JsonArray(Math.Round(pts[^1].X, 6), Math.Round(pts[^1].Y, 6));
        }
        if (attrs["start"] is null || attrs["end"] is null) throw new CommandException("A section needs its line.");
    }

    /// <summary>The step of the stations written along a developed section (m): as set, else a tenth of the scale along an element (10 m at 1:100); null: none.</summary>
    public static double? Stations(Entity view, double scale)
    {
        if (view.NumOrNull("stations") is { } s) return s > 0 ? s : null;
        return Along(view) != null ? Math.Max(1, scale / 10) : null;
    }

    /// <summary>A station as a road writes it: kilometres + metres, "0+020", "1+250.5".</summary>
    public static string Station(double s)
    {
        var sign = s < 0 ? "−" : "";
        s = Math.Abs(s);
        var km = (int)Math.Floor(s / 1000 + 1e-9);
        var metres = s - km * 1000;
        var m = Math.Abs(metres - Math.Round(metres)) < 1e-6 ? ((int)Math.Round(metres)).ToString("000", CultureInfo.InvariantCulture) : metres.ToString("000.##", CultureInfo.InvariantCulture);
        return $"{sign}{km}+{m}";
    }
}
