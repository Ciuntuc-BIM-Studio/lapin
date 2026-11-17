using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Holes in an outline (audit S4): islands cut out of a floor, a ceiling or a platform as part of its own outline, not separate
/// elements (a floor opening stays an element of its own). Kept beside the outline as <c>holes</c> (each a loop of corners, as
/// <c>contour</c> keeps its) and, when an edge of one is curved, <c>holeArcs</c> (each hole's rises, <see cref="ArcOutline"/>,
/// null for a hole of straight edges) and, when one runs along an ellipse, <c>holeEllipses</c> (each hole's elliptical edges,
/// <see cref="EllipseEdge"/>, null for a hole with none). An element with no hole keeps none, so what was saved before reads unchanged.
/// Each hole is checked: three corners at least, enclosing something, counter-clockwise, inside the outline and apart from the
/// other holes.
/// </summary>
public static class OutlineHoles
{
    public const string Key = "holes";
    public const string ArcsKey = "holeArcs";
    public const string EllipsesKey = "holeEllipses";

    /// <summary>One hole: its corners (counter-clockwise), its edges' rises (null when every edge is straight) and its elliptical edges (null: none).</summary>
    public sealed record Hole(IReadOnlyList<Vec2> Corners, double[]? Rises, EllipseEdge?[]? Ellipses = null)
    {
        /// <summary>Its loop as built: each arc and elliptical edge laid in chords by the one rule.</summary>
        public IReadOnlyList<Vec2> Points => Rises != null || Ellipses != null ? ArcOutline.Points(Corners, Rises, null, Ellipses) : Corners;
        public double Area => Math.Abs(ArcOutline.SignedArea(Corners, Rises, Ellipses));
        public double Perimeter => ArcOutline.Perimeter(Corners, Rises, Ellipses);
    }

    /// <summary>The holes an element keeps (none: empty).</summary>
    public static IReadOnlyList<Hole> Of(Entity e) => Read(e.Attrs[Key], e.Attrs[ArcsKey], e.Attrs[EllipsesKey]);

    public static IReadOnlyList<Hole> Read(JsonNode? holes, JsonNode? arcs, JsonNode? ellipses = null)
    {
        if (holes is not JsonArray arr) return [];
        var o = new List<Hole>();
        for (var i = 0; i < arr.Count; i++)
        {
            var c = Args.StoredPoints(arr[i]);
            if (c.Count < 3) continue;
            o.Add(new Hole(c, arcs is JsonArray a && i < a.Count ? ArcOutline.Rises(a[i], c.Count) : null,
                ellipses is JsonArray el && i < el.Count ? EllipseEdge.Read(el[i], c.Count) : null));
        }
        return o;
    }

    /// <summary>The holes' loops as built (each arc in chords).</summary>
    public static List<IReadOnlyList<Vec2>> Built(Entity e) => [.. Of(e).Select(h => h.Points)];

    /// <summary>The area the holes take out of the outline, exactly.</summary>
    public static double Area(Entity e) => Of(e).Sum(h => h.Area);

    /// <summary>Writes the holes (none: both attributes taken away).</summary>
    public static void Write(JsonObject attrs, IReadOnlyList<Hole> holes)
    {
        if (holes.Count == 0) { attrs.Remove(Key); attrs.Remove(ArcsKey); attrs.Remove(EllipsesKey); return; }
        attrs[Key] = new JsonArray([.. holes.Select(h => (JsonNode)new JsonArray([.. h.Corners.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]))]);
        if (holes.Any(h => ArcOutline.HasArcs(h.Rises))) attrs[ArcsKey] = new JsonArray([.. holes.Select(h => ArcOutline.ToJson(h.Rises))]);
        else attrs.Remove(ArcsKey);
        if (holes.Any(h => h.Ellipses?.Any(x => x != null) == true))
            attrs[EllipsesKey] = new JsonArray([.. holes.Select(h => h.Ellipses?.Any(x => x != null) == true ? (JsonNode)new JsonArray([.. h.Ellipses.Select(x => (JsonNode?)x?.ToJson())]) : null)]);
        else attrs.Remove(EllipsesKey);
    }

    /// <summary>
    /// The holes a command gives: <c>holes</c> ([[[x, y]…]…], each a loop) and, optional, <c>holeArcs</c> (each hole's rises or
    /// null) and <c>holeEllipses</c> (each hole's elliptical edges or null), each checked (<see cref="ArcOutline.Check"/>) and made
    /// counter-clockwise; null when the command gives none.
    /// </summary>
    public static List<Hole>? Arg(JsonObject a)
    {
        if (a[Key] is not JsonArray arr) return null;
        var o = new List<Hole>();
        for (var i = 0; i < arr.Count; i++)
        {
            var pts = Args.StoredPoints(arr[i]);
            var rises = a[ArcsKey] is JsonArray ra && i < ra.Count ? ArcOutline.RisesArg(ra[i], pts.Count) : null;
            var given = a[EllipsesKey] is JsonArray ea && i < ea.Count && ea[i] is JsonArray one ? one.Select(EllipseEdge.From).ToArray() : null;
            o.Add(Checked(pts, rises, given));
        }
        return o;
    }

    /// <summary>A hole checked (<see cref="ArcOutline.Check"/>: counter-clockwise), its elliptical edges fitted to its corners as they then run.</summary>
    public static Hole Checked(IReadOnlyList<Vec2> pts, double[]? rises, IReadOnlyList<EllipseEdge?>? ellipses)
    {
        var (c, r) = ArcOutline.Check(pts, rises, "A hole");
        // Given clockwise, the check runs it back: its edges taken in that order, each run back (an edge's corners alone cannot
        // tell, when every edge is a piece of one ellipse).
        if (ellipses != null && ellipses.Count == pts.Count && ellipses.Any(x => x != null) && ArcOutline.SignedArea(pts, rises, ellipses) < 0)
        {
            var n = ellipses.Count;
            ellipses = [.. Enumerable.Range(0, n).Select(j => ellipses[((n - 2 - j) % n + n) % n]?.Reversed)];
        }
        return new Hole(c, r, EllipseEdge.Fit(c, ellipses));
    }

    /// <summary>
    /// Refused unless every hole lies inside the outline (<paramref name="outer"/>, as built) and apart from the others: no edge
    /// of one crosses the outline or another hole, and none lies inside another.
    /// </summary>
    public static void Fits(IReadOnlyList<Vec2> outer, IReadOnlyList<Hole> holes)
    {
        var loops = holes.Select(h => h.Points).ToList();
        for (var i = 0; i < loops.Count; i++)
        {
            if (!loops[i].All(p => Polygon2.Contains(outer, p)) || Crosses(loops[i], outer))
                throw new CommandException($"Hole {i + 1} must lie inside the outline.");
            for (var j = 0; j < i; j++)
                if (Crosses(loops[i], loops[j]) || Polygon2.Contains(loops[j], loops[i][0]) || Polygon2.Contains(loops[i], loops[j][0]))
                    throw new CommandException($"Holes {j + 1} and {i + 1} overlap: keep them apart.");
        }
    }

    static bool Crosses(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b)
    {
        for (var i = 0; i < a.Count; i++)
            for (var j = 0; j < b.Count; j++)
                if (Polyline2.Crosses(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count])) return true;
        return false;
    }

    /// <summary>
    /// The holes written into an element's attributes after its outline (<paramref name="key"/>, <paramref name="arcsKey"/> in
    /// <paramref name="attrs"/>) was set or changed: the command's own (<c>holes</c>, <c>holeArcs</c>; an empty list takes them
    /// away), else those it keeps; each checked against the outline as it now is. One step with the outline's change.
    /// </summary>
    public static void Apply(JsonObject attrs, JsonObject a, string key = "contour", string arcsKey = ArcOutline.Key)
    {
        var was = Read(attrs[Key], attrs[ArcsKey], attrs[EllipsesKey]);
        var holes = Arg(a) is { } given ? a[EllipsesKey] == null ? Kept(given, was) : given : was.ToList();
        if (holes.Count > 0)
        {
            var corners = Args.StoredPoints(attrs[key]);
            var outer = ArcOutline.Rises(attrs[arcsKey], corners.Count) is { } r ? ArcOutline.Points(corners, r) : corners;
            Fits(outer, holes);
        }
        Write(attrs, holes);
    }

    /// <summary>
    /// Holes given again without their elliptical edges (a grip's edit, which sends corners and rises): each keeps the edges hole k
    /// had, moved with it when its corners all moved by one offset, else those whose ends its corners still lie on.
    /// </summary>
    static List<Hole> Kept(List<Hole> given, IReadOnlyList<Hole> was) =>
    [
        .. given.Select((h, k) =>
        {
            if (k >= was.Count || was[k].Ellipses is not { } old || old.Length != h.Corners.Count) return h;
            var c = h.Corners;
            var d = c[0] - was[k].Corners[0];
            if (c.Select((p, i) => p - was[k].Corners[i]).All(x => (x - d).Length < 1e-9))
                return h with { Ellipses = [.. old.Select(x => x == null ? null : x with { C = x.C + d })] };
            var kept = old.Select((x, i) => x != null && x.Off(c[i]) < 1e-6 && x.Off(c[(i + 1) % c.Count]) < 1e-6 ? x : null).ToArray();
            return kept.Any(x => x != null) ? h with { Ellipses = kept } : h;
        }),
    ];

    /// <summary>The hole an outline edit names (<c>hole</c>: its index), or null when it names the outline itself; refused when the element has no such hole.</summary>
    public static int? Index(JsonObject a, Entity e)
    {
        if (a["hole"] == null) return null;
        var n = Of(e).Count;
        return Json.Number(a["hole"]) is { } k && k >= 0 && k < n && k == Math.Floor(k) ? (int)k : throw new CommandException(n == 0 ? "The element has no hole." : $"Which hole? It has {n}, from 0.");
    }

    /// <summary>The element's attributes with hole <paramref name="k"/> replaced (checked, counter-clockwise, still inside the outline and apart).</summary>
    public static JsonObject Replaced(Entity e, int k, IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises, string key = "contour", string arcsKey = ArcOutline.Key)
    {
        var (c, r) = ArcOutline.Check(corners, rises, "A hole");
        var holes = Of(e).ToList();
        // Its elliptical edges kept while their corners still lie on them.
        var was = holes[k].Ellipses;
        var kept = was != null && was.Length == c.Count ? was.Select((x, i) => x != null && x.Off(c[i]) < 1e-6 && x.Off(c[(i + 1) % c.Count]) < 1e-6 ? x : null).ToArray() : null;
        holes[k] = new Hole(c, r, kept?.Any(x => x != null) == true ? kept : null);
        var attrs = e.CloneAttrs();
        Write(attrs, holes);
        Apply(attrs, new JsonObject(), key, arcsKey);
        return attrs;
    }

    /// <summary>The element's attributes with one more hole (<c>points</c>, <c>arcs?</c> of a command), checked.</summary>
    public static JsonObject Added(Entity e, JsonObject a)
    {
        var pts = Args.Points(a);
        var given = a[EllipseEdge.Key] is JsonArray ea ? ea.Select(EllipseEdge.From).ToArray() : null;
        var holes = Of(e).ToList();
        holes.Add(Checked(pts, ArcOutline.RisesArg(a[ArcOutline.Key], pts.Count), given));
        var attrs = e.CloneAttrs();
        Write(attrs, holes);
        Apply(attrs, new JsonObject());
        return attrs;
    }

    /// <summary>The element's attributes without hole <paramref name="k"/>.</summary>
    public static JsonObject Removed(Entity e, int k)
    {
        var holes = Of(e).ToList();
        holes.RemoveAt(k);
        var attrs = e.CloneAttrs();
        Write(attrs, holes);
        return attrs;
    }

    /// <summary>Whether a drawn loop lies inside an element's outline (as built) and outside its holes: where a new hole may go.</summary>
    public static bool Holds(Entity e, IReadOnlyList<Vec2> pts) =>
        ArcOutline.BuiltOf(e) is { Count: >= 3 } outer && pts.All(p => Polygon2.Contains(outer, p)) && !Built(e).Any(h => pts.Any(p => Polygon2.Contains(h, p)));

    /// <summary>The element's holes, refused when the new outline (as built) no longer holds them.</summary>
    public static void StillFit(Entity e, IReadOnlyList<Vec2> outer)
    {
        if (Of(e) is { Count: > 0 } holes) Fits(outer, holes);
    }
}
