using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// A dimension string edited as one object, like a polyline: its points
/// moved, inserted and taken out, the string split into single dimensions,
/// and single dimensions that continue one another on one dimension line
/// joined into a string. An older project's strings (one dimension per
/// segment, grouped by <c>chain</c> and <c>chainIx</c>) open as one string each.
/// </summary>
public static class DimensionStrings
{
    /// <summary>Points this close (m) are one point.</summary>
    public const double Same = 1e-6;
    /// <summary>Directions this close (sine of the angle) are parallel; dimension lines this close (m) are one line.</summary>
    const double Parallel = 1e-4;

    /// <summary>A dimension's points (with what each holds on) and its segments' texts.</summary>
    public sealed record Piece(Entity Dim, List<(Vec2 P, DimEndRef? Ref)> Stations, List<string?> Texts)
    {
        public Piece Reversed() => this with { Stations = Enumerable.Reverse(Stations).ToList(), Texts = Enumerable.Reverse(Texts).ToList() };
    }

    public static Piece PieceOf(Entity dim)
    {
        var st = Dimensions.Stations(dim);
        return new Piece(dim, st, Dimensions.TextsOf(dim.Attrs, st.Count - 1));
    }

    /// <summary><paramref name="next"/> after <paramref name="string"/>, sharing its first point with the string's last (what that point holds on: either's).</summary>
    static void Append(List<(Vec2 P, DimEndRef? Ref)> stations, List<string?> texts, Piece next)
    {
        stations[^1] = (stations[^1].P, stations[^1].Ref ?? next.Stations[0].Ref);
        stations.AddRange(next.Stations.Skip(1));
        texts.AddRange(next.Texts);
    }

    static bool Near(Vec2 a, Vec2 b, double tol) => (a - b).Length <= tol;

    /// <summary>
    /// Pieces laid end to end in any order, each turned round as needed so that
    /// it goes on from an end of what is laid; null when one does not continue
    /// the others.
    /// </summary>
    public static (List<(Vec2 P, DimEndRef? Ref)> Stations, List<string?> Texts)? Chain(IReadOnlyList<Piece> pieces, double tol = Same)
    {
        if (pieces.Count == 0) return null;
        var stations = pieces[0].Stations.ToList();
        var texts = pieces[0].Texts.ToList();
        var left = pieces.Skip(1).ToList();
        while (left.Count > 0)
        {
            var found = false;
            for (var k = 0; k < left.Count && !found; k++)
            {
                var p = left[k];
                Piece? after = Near(p.Stations[0].P, stations[^1].P, tol) ? p : Near(p.Stations[^1].P, stations[^1].P, tol) ? p.Reversed() : null;
                if (after != null) { Append(stations, texts, after); left.RemoveAt(k); found = true; continue; }
                Piece? before = Near(p.Stations[^1].P, stations[0].P, tol) ? p : Near(p.Stations[0].P, stations[0].P, tol) ? p.Reversed() : null;
                if (before != null)
                {
                    var s2 = before.Stations.ToList();
                    var t2 = before.Texts.ToList();
                    Append(s2, t2, new Piece(pieces[0].Dim, stations, texts));
                    (stations, texts) = (s2, t2);
                    left.RemoveAt(k);
                    found = true;
                }
            }
            if (!found) return null;
        }
        return (stations, texts);
    }

    /// <summary>The direction a piece measures along (its <c>dir</c>, else end to end), as stored.</summary>
    static Vec2? Along(Piece p) => Dimensions.Along(p.Dim.Attrs, p.Stations[0].P, p.Stations[^1].P);

    /// <summary>
    /// The attributes of one string made of <paramref name="pieces"/> (the first's
    /// line, styles and view): null when they do not lie on one dimension line,
    /// measure along one direction and continue one another.
    /// </summary>
    public static JsonObject? Joined(IReadOnlyList<Piece> pieces, double tol = Same)
    {
        if (pieces.Count == 0 || Along(pieces[0]) is not { } u) return null;
        var first = pieces[0].Dim;
        var n = u.Perp;
        var at = P(first.Attrs["at"]);
        foreach (var p in pieces)
        {
            if (p.Dim.Str("viewId") != first.Str("viewId")) return null;
            if (Along(p) is not { } v || Math.Abs(Vec2.Cross(u, v)) > Parallel) return null;
            if ((p.Dim.Attrs["dir"] is JsonArray) != (first.Attrs["dir"] is JsonArray)) return null;
            if (Math.Abs(Vec2.Dot(P(p.Dim.Attrs["at"]) - at, n)) > Math.Max(Parallel, tol)) return null;
        }
        if (Chain(pieces, tol) is not { } chained) return null;
        var attrs = first.CloneAttrs();
        Dimensions.Write(attrs, chained.Stations, chained.Texts);
        // An aligned string whose points do not lie along its first segment measures along that segment's direction.
        if (attrs["dir"] is not JsonArray && Dimensions.Along(attrs, chained.Stations[0].P, chained.Stations[^1].P) is { } w && Math.Abs(Vec2.Cross(u, w)) > Parallel)
            attrs["dir"] = new JsonArray(u.X, u.Y);
        return attrs;
    }

    /// <summary>A string's segments as single dimensions' attributes: each keeps the string's line, styles, direction and its own text.</summary>
    public static List<JsonObject> Split(Entity dim)
    {
        var piece = PieceOf(dim);
        var u = Along(piece);
        var o = new List<JsonObject>();
        for (var i = 0; i + 1 < piece.Stations.Count; i++)
        {
            var attrs = dim.CloneAttrs();
            attrs.Remove("texts"); attrs.Remove("text");
            Dimensions.Write(attrs, [piece.Stations[i], piece.Stations[i + 1]], [piece.Texts[i]]);
            // An aligned segment not along the string keeps measuring what it measured in it.
            if (attrs["dir"] is not JsonArray && u is { } su && Dimensions.Along(attrs, piece.Stations[i].P, piece.Stations[i + 1].P) is { } w && Math.Abs(Vec2.Cross(su, w)) > Parallel)
                attrs["dir"] = new JsonArray(su.X, su.Y);
            o.Add(attrs);
        }
        return o;
    }

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;

    /// <summary>
    /// An older project's dimensions as this Lapin keeps them: every string of
    /// single dimensions (<c>chain</c>, ordered by <c>chainIx</c>) one string,
    /// with the first one's id, line and styles (a string broken where a
    /// dimension does not start where the one before it ends goes on as
    /// another); every dimension's points in <c>pts</c> and <c>refs</c>.
    /// </summary>
    public static (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
    {
        static bool Old(Entity e) => Dimensions.IsLinear(e) && (e.Attrs["pts"] is not JsonArray || e.Attrs["chain"] != null);
        if (!entities.Any(Old)) return (entities, relations);
        var chains = entities.Where(e => Old(e) && e.Str("chain") != null).GroupBy(e => e.Str("chain")!)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Num("chainIx")).ToList());
        var gone = new HashSet<string>();
        var made = new Dictionary<string, Entity>();
        foreach (var (_, members) in chains)
        {
            // Runs of dimensions each starting where the last ended.
            var runs = new List<List<Piece>>();
            foreach (var m in members)
            {
                var p = PieceOf(m);
                if (runs.Count > 0 && Near(runs[^1][^1].Stations[^1].P, p.Stations[0].P, 1e-6)) runs[^1].Add(p);
                else runs.Add([p]);
            }
            foreach (var run in runs)
            {
                var attrs = Joined(run, 1e-6) ?? Fallback(run);
                made[run[0].Dim.Id] = run[0].Dim.With(attrs: attrs);
                foreach (var p in run.Skip(1)) gone.Add(p.Dim.Id);
            }
        }
        var outEntities = new List<Entity>(entities.Count);
        foreach (var e in entities)
        {
            if (gone.Contains(e.Id)) continue;
            if (made.TryGetValue(e.Id, out var m)) { outEntities.Add(m); continue; }
            if (Old(e))
            {
                var p = PieceOf(e);
                var attrs = e.CloneAttrs();
                Dimensions.Write(attrs, p.Stations, p.Texts);
                outEntities.Add(e.With(attrs: attrs));
                continue;
            }
            outEntities.Add(e);
        }
        if (gone.Count == 0) return (outEntities, relations);
        var outRelations = new List<Relation>();
        foreach (var r in relations)
        {
            if (gone.Contains(r.Relating)) continue;
            var related = r.Related.Where(x => !gone.Contains(x)).ToList();
            if (related.Count == 0) continue;
            outRelations.Add(related.Count == r.Related.Count ? r : r with { Related = related });
        }
        return (outEntities, outRelations);
    }

    /// <summary>A run that does not line up (dimensions of a string with lines of their own): one string all the same, along the first one's direction.</summary>
    static JsonObject Fallback(List<Piece> run)
    {
        var stations = run[0].Stations.ToList();
        var texts = run[0].Texts.ToList();
        foreach (var p in run.Skip(1)) Append(stations, texts, p);
        var attrs = run[0].Dim.CloneAttrs();
        Dimensions.Write(attrs, stations, texts);
        if (attrs["dir"] is not JsonArray && Along(run[0]) is { } u) attrs["dir"] = new JsonArray(u.X, u.Y);
        return attrs;
    }
}

/// <summary>Older projects' dimension strings, one object each as they open (<see cref="DimensionStrings.Upgrade"/>).</summary>
public sealed class DimensionUpgrade : IProjectUpgrade
{
    public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations) =>
        DimensionStrings.Upgrade(entities, relations);
}
