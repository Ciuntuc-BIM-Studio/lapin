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
/// before, or more (a jogged section: projected by default, report #146, its
/// pieces parallel to the first cutting in one frame and its jogs cutting
/// nothing; or developed, <c>jogged: "developed"</c>, each straight piece a
/// planar cut seen only between its ends, the pieces laid side by side at
/// their lengths), an arc through three points (a curved section, sampled
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
        if (view.Attrs["pts"] is JsonArray pts && pts.Count >= 2) return Clean(pts.Select(Args.PointOf));
        if (view.Attrs["start"] is JsonArray s && view.Attrs["end"] is JsonArray e) return Clean([Args.PointOf(s), Args.PointOf(e)]);
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

    /// <summary>The view's field that says how a jogged section is drawn (report #146): <see cref="ProjectedMode"/> (absent: the default) or <see cref="DevelopedMode"/>.</summary>
    public const string JoggedKey = "jogged";
    public const string ProjectedMode = "projected";
    public const string DevelopedMode = "developed";

    /// <summary>A piece within this angle of the first piece's direction is parallel to it and cuts; any other is a jog.</summary>
    public const double ParallelDegrees = 1.0;

    /// <summary>Whether the section's line is jogged (three or more points, not an arc, following nothing, not across an element): the only line <c>jogged</c> applies to.</summary>
    public static bool IsJogged(Entity view) => !IsArc(view) && Along(view) == null && !AcrossSections.Is(view) && Points(view).Count > 2;

    /// <summary>
    /// Whether a jogged section is drawn projected (Revit's: the pieces parallel to the first cut, the jogs cut nothing, one
    /// frame along the first piece): the default for a jogged line unless <c>jogged: "developed"</c>. A curved section or one
    /// along an element is always developed.
    /// </summary>
    public static bool Projects(Entity view) => IsJogged(view) && view.Str(JoggedKey) != DevelopedMode;

    /// <summary>
    /// The planar cuts a section is drawn by, and whether each sees only its stretch (a line of more than one piece). Developed:
    /// every straight piece at its developed station (<see cref="Pieces(IReadOnlyList{Vec2})"/>). Projected (<see cref="Projects"/>):
    /// <see cref="ProjectedPieces"/>, falling back to developed when the line turns back over itself.
    /// </summary>
    public static (List<Piece> Pieces, bool Spanned) Cuts(Entity view, IReadOnlyList<Vec2> path)
    {
        var raw = Pieces(path);
        var spanned = raw.Count > 1;
        if (spanned && Projects(view) && ProjectedPieces(raw) is { } projected) return (projected, true);
        return (raw, spanned);
    }

    /// <summary>
    /// A jogged line's cuts drawn projected (report #146, as Revit's): x is the distance along the first piece's direction from
    /// its start, the pieces parallel to it (within <see cref="ParallelDegrees"/>) each cut on a plane of the first piece's
    /// direction (through the piece's middle) and see only their stretch of x; the others (the jogs) cut nothing and add no
    /// width: where the cut jumps from one parallel piece to the next is the middle of the jogs between them (a perpendicular
    /// jog's own x), so the stretches meet with no gap. Each cut is a piece from the start of its stretch to its end, laid at
    /// its station (its x0), so a drawing point's x is the plan point's projection on the first direction for every piece.
    /// Null when the line turns back on itself (a stretch running backwards), which only a developed section can draw.
    /// </summary>
    public static List<Piece>? ProjectedPieces(IReadOnlyList<Piece> raw)
    {
        if (raw.Count == 0) return null;
        var origin = raw[0].A;
        var u = (raw[0].B - raw[0].A) * (1 / raw[0].Length);
        var n = u.Perp;
        var sinTol = Math.Sin(ParallelDegrees * Math.PI / 180);
        double T(Vec2 p) => Vec2.Dot(p - origin, u);
        bool Parallel(Piece p) { var d = (p.B - p.A) * (1 / p.Length); return Vec2.Dot(d, u) > 0 && Math.Abs(d.X * u.Y - d.Y * u.X) <= sinTol; }
        var cutting = raw.Where(Parallel).ToList();
        var o = new List<Piece>(cutting.Count);
        var lo = 0.0;
        for (var i = 0; i < cutting.Count; i++)
        {
            var p = cutting[i];
            // To the middle of the jogs before the next parallel piece; the last to its own end.
            var hi = i + 1 < cutting.Count ? (T(p.B) + T(cutting[i + 1].A)) / 2 : T(p.B);
            if (i + 1 < cutting.Count && T(cutting[i + 1].A) < T(p.B) - 1e-6) return null;
            if (hi < lo - 1e-6) return null;
            if (hi - lo > 1e-9)
            {
                var off = Vec2.Dot((p.A + p.B) * 0.5 - origin, n);
                o.Add(new Piece(origin + u * lo + n * off, origin + u * hi + n * off, hi - lo, lo));
            }
            lo = Math.Max(lo, hi);
        }
        return o.Count == 0 ? null : o;
    }

    public static double Length(IReadOnlyList<Vec2> path) => Polyline2.Length(path);

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
            if (s is JsonArray && e is JsonArray) pts = Clean([Args.PointOf(s), Args.PointOf(e)]);
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
        if (a.ContainsKey(JoggedKey))
        {
            // How a jogged line is drawn: "developed" kept; "projected" (the default) or null clears the field.
            var mode = (string?)a[JoggedKey];
            if (mode is not (null or ProjectedMode or DevelopedMode)) throw new CommandException("A jogged section is drawn \"projected\" or \"developed\".");
            if (mode == DevelopedMode) attrs[JoggedKey] = DevelopedMode; else attrs.Remove(JoggedKey);
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
