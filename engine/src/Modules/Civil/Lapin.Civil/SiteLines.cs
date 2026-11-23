using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// The lines on the site a channel, a standalone kerb or an array of accessories runs along (docs/plan-site-grading.md, T6), read
/// live from what they follow each time they are built: a feature line as it is resolved, a hardscape's top edge (all of it, a ring,
/// or one edge), a kerb's line, a channel's axis at its top, a platform's outline at its level, a path's, wall's or ditch's axis at
/// its red line, or a path's carriageway edge (<c>side</c> left or right) at its top there, a site feature's line. Heights are model
/// heights; a point whose height the line does not give has none (the ground then gives it, where asked).
/// <para>
/// <see cref="Surface"/>: what lies on top of the site at a point, which a channel's top and a kerb laid on the ground follow: a
/// hardscape's top where one covers it, else the terrain's designed ground.
/// </para>
/// </summary>
public static class SiteLines
{
    /// <summary>A line: its points in plan, each one's height (null: none), whether it closes on itself (the first point not repeated), what it is.</summary>
    public sealed record Line(List<Vec2> Plan, List<double?> Z, bool Closed, string Name)
    {
        public bool HasHeights => Z.Count > 0 && Z.All(z => z != null);
    }

    /// <summary>Whether an element can be followed (<see cref="Of"/> reads it).</summary>
    public static bool Followable(Entity x) =>
        FeatureLines.Is(x) || Hardscapes.Is(x) || Kerbs.Is(x) || Channels.Is(x) || Corridors.Is(x) || (x.Class == IfcClass.EarthworksCut && !Ditches.Is(x))
        || (x.Class == SiteFeatures.Class && SiteFeatures.Kind(x) is "line" or "parcel" or "area");

    /// <summary>
    /// The line an element gives: <paramref name="edge"/> one edge of an outline (from its corner <paramref name="edge"/> to the next),
    /// <paramref name="side"/> a path's carriageway edge (left, right) instead of its axis. Null when it gives none.
    /// </summary>
    public static Line? Of(IModelView m, Entity x, int? edge = null, string? side = null)
    {
        if (FeatureLines.Is(x))
        {
            var (line, _, probs) = FeatureLines.Resolve(m, x);
            if (line.Count < 2) return null;
            var pts = line.ToList();
            var closed = FeatureLines.SpecOf(x).Closed;
            if (closed && pts.Count > 1 && (pts[0].Xy - pts[^1].Xy).Length < 1e-9) pts.RemoveAt(pts.Count - 1);
            return new Line([.. pts.Select(p => p.Xy)], [.. pts.Select(p => double.IsFinite(p.Z) ? p.Z : (double?)null)], closed, x.Name);
        }
        if (Hardscapes.Is(x)) return Hardscapes.Corners(x).Count >= 3 ? Ring(Hardscapes.Top(x), edge, x.Name) : null;
        if (Kerbs.Is(x))
        {
            if (x.Str(Kerbs.HostKey) is { } host)
                return m.Get(host) is { } h && Hardscapes.Is(h) && Hardscapes.Corners(h).Count >= 3 ? Ring(Hardscapes.Top(h), edge, x.Name) : null;
            var laid = KerbLines.Laid(m, x);
            return laid.Plan.Count >= 2 ? new Line(laid.Plan, [.. laid.Z.Select(z => (double?)z)], laid.Closed, x.Name) : null;
        }
        if (Channels.Is(x))
        {
            var l = Channels.LaidOf(m, x);
            return l.Stations.Count >= 2 ? new Line([.. l.Stations.Select(l.Chain.PointAt)], [.. l.Top.Select(z => (double?)z)], false, x.Name) : null;
        }
        if (x.Class == IfcClass.EarthworksCut && !Ditches.Is(x))
        {
            var o = ArcOutline.BuiltOf(x);
            if (o.Count < 3) return null;
            var z = x.Num("elevation");
            var ring = Polygon2.EnsureCcw(o);
            if (edge != null) return null;
            return new Line([.. ring], [.. ring.Select(_ => (double?)z)], true, x.Name);
        }
        if (Corridors.Is(x))
        {
            var c = Corridors.ChainOf(x);
            if (c.Segments == 0) return null;
            var red = Corridors.RedLine(m, x, c);
            if (side is "left" or "right" && x.Class == IfcClass.Pavement)
            {
                var t = Paths.TemplateOf(x, m);
                var off = side == "left" ? t.Half : -t.Half;
                var stations = c.PlanStations(Chords.Tolerance);
                return new Line([.. stations.Select(s => c.PointAt(s.Station) + c.Across(s.Station, s.Segment) * off)],
                    [.. stations.Select(s => (double?)Paths.TopAt(t, red(s.Station), off))], false, x.Name);
            }
            var st = c.PlanStations(Chords.Tolerance);
            return new Line([.. st.Select(s => c.PointAt(s.Station))], [.. st.Select(s => (double?)red(s.Station))], false, x.Name);
        }
        if (x.Class == SiteFeatures.Class && SiteFeatures.Kind(x) is "line" or "parcel" or "area")
        {
            var pts = SiteFeatures.Points(x);
            if (pts.Count < 2) return null;
            var closed = SiteFeatures.Kind(x) is "parcel" or "area";
            return new Line([.. pts.Select(p => p.Xy)], [.. pts.Select(p => double.IsFinite(p.Z) ? p.Z : (double?)null)], closed, x.Name);
        }
        return null;
    }

    /// <summary>A hardscape's top edge: the ring (closed), or one edge of it from its corner to the next.</summary>
    static Line? Ring(TopSurface.Solved top, int? edge, string name)
    {
        if (edge is not { } k) return new Line([.. top.Points], [.. top.Z.Select(z => (double?)z)], true, name);
        if (k < 0 || k >= top.CornerIndex.Count) return null;
        var ix = Hardscapes.EdgePoints(top, k);
        return new Line([.. ix.Select(i => top.Points[i])], [.. ix.Select(i => (double?)top.Z[i])], false, name);
    }

    /// <summary>
    /// What a channel, a kerb or an accessory says it runs along: <c>{id, edge?, side? (left | right, a path's edge), offset? (m, +
    /// left)}</c>. The line read live, shifted across by its offset (its corners mitred); null with a reason when it gives none.
    /// </summary>
    public static Line? Along(IModelView m, JsonObject along, List<string> problems)
    {
        var id = (string?)along["id"];
        if (string.IsNullOrEmpty(id) || m.Get(id) is not { } x) { problems.Add("What it runs along is gone: it keeps no line."); return null; }
        var edge = Json.Number(along["edge"]) is { } ed && ed >= 0 && ed == Math.Floor(ed) ? (int)ed : (int?)null;
        var l = Of(m, x, edge, (string?)along["side"]);
        if (l == null) { problems.Add($"„{x.Name}” gives no line to run along."); return null; }
        var off = Json.Number(along["offset"]) ?? 0;
        if (Math.Abs(off) < 1e-12) return l;
        var plan = Polyline2.Offset(l.Plan, off, l.Closed, maxMitre: 4);
        return l with { Plan = plan };
    }

    /// <summary>An <c>along</c> argument checked: what it names can be followed; the edge and side kept as given.</summary>
    public static JsonObject AlongArg(IModelView m, JsonNode? node)
    {
        var a = node as JsonObject ?? (node is JsonValue v && v.TryGetValue<string>(out var s) ? new JsonObject { ["id"] = s } : null)
            ?? throw new CommandException("Run along what? {id, edge?, side?, offset?}.");
        var id = (string?)a["id"];
        if (string.IsNullOrEmpty(id) || m.Get(id) is not { } x) throw new CommandException("It runs along an element that is not in the project.");
        if (!Followable(x)) throw new CommandException($"„{x.Name}” gives no line to run along: pick a feature line, a hardscape's edge, a kerb, a channel, a platform, a path's edge or axis, a wall or a ditch.");
        var o = new JsonObject { ["id"] = id };
        if (Json.Number(a["edge"]) is { } e && e >= 0 && e == Math.Floor(e)) o["edge"] = (int)e;
        if ((string?)a["side"] is "left" or "right") o["side"] = (string?)a["side"];
        if (Json.Number(a["offset"]) is { } off && Math.Abs(off) > 1e-12) o["offset"] = off;
        if (Of(m, x, (int?)Json.Number(o["edge"]), (string?)o["side"]) == null) throw new CommandException($"„{x.Name}” gives no line there to run along.");
        return o;
    }

    /// <summary>The height a line gives at a point near it: the nearest point of its laid line in plan, its height there (linear between its points).</summary>
    public static double? HeightNear(Line l, Vec2 p)
    {
        if (!l.HasHeights) return null;
        var n = l.Plan.Count;
        var segs = l.Closed ? n : n - 1;
        var best = (D: double.MaxValue, Z: (double?)null);
        for (var i = 0; i < segs; i++)
        {
            var (a, b) = (l.Plan[i], l.Plan[(i + 1) % n]);
            var d = b - a;
            var len2 = Vec2.Dot(d, d);
            var t = len2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p - a, d) / len2, 0, 1);
            var dist = (a + d * t - p).Length;
            if (dist < best.D) best = (dist, l.Z[i]!.Value + (l.Z[(i + 1) % n]!.Value - l.Z[i]!.Value) * t);
        }
        return best.Z;
    }

    /// <summary>
    /// The site's top at a point (model height): the top of the hardscape whose outline holds it (the highest when several do),
    /// else the terrain's designed ground there; null where neither is.
    /// </summary>
    public static double? Surface(IModelView m, Vec2 p)
    {
        double? best = null;
        foreach (var h in m.OfClass(Hardscapes.Class))
        {
            if (Hardscapes.Corners(h).Count < 3) continue;
            var top = Hardscapes.Top(h);
            if (!Polygon2.Contains(top.Points, p)) continue;
            var z = top.At(p);
            if (best == null || z > best) best = z;
        }
        if (best != null) return best;
        return Terrains.Under(m, p) is { } t ? Terrains.Of(t).ElevationAt(p.X, p.Y) : null;
    }

    /// <summary>A polyline with a point every <paramref name="step"/> at most along each stretch (its heights linear).</summary>
    public static (List<Vec2> Plan, List<double?> Z) Densify(IReadOnlyList<Vec2> plan, IReadOnlyList<double?> z, bool closed, double step)
    {
        var (po, zo) = (new List<Vec2>(), new List<double?>());
        var n = plan.Count;
        var segs = closed ? n : n - 1;
        for (var i = 0; i < segs; i++)
        {
            var j = (i + 1) % n;
            var (a, b) = (plan[i], plan[j]);
            po.Add(a); zo.Add(z[i]);
            var k = (int)Math.Ceiling((b - a).Length / step - 1e-9);
            for (var q = 1; q < k; q++)
            {
                var f = (double)q / k;
                po.Add(a + (b - a) * f);
                zo.Add(z[i] is { } z0 && z[j] is { } z1 ? z0 + (z1 - z0) * f : null);
            }
        }
        if (!closed && n > 0) { po.Add(plan[^1]); zo.Add(z[^1]); }
        return (po, zo);
    }

    /// <summary>The stations (plan distance from the first point) of a polyline's points, closed: one more, the whole way round.</summary>
    public static double[] Stations(IReadOnlyList<Vec2> plan, bool closed)
    {
        var n = plan.Count;
        var st = new double[closed ? n + 1 : n];
        for (var i = 1; i < st.Length; i++) st[i] = st[i - 1] + (plan[i % n] - plan[i - 1]).Length;
        return st;
    }

    /// <summary>The terrain under a line (the first that reaches one of its points), or none.</summary>
    public static Entity? TerrainUnder(IModelView m, IEnumerable<Vec2> pts) => pts.Select(p => Terrains.Under(m, p)).FirstOrDefault(t => t != null);
}
