using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// A part sketch's closed outlines, what a body is pushed from (decision 520): a closed polyline, a circle, or a closed chain of the
/// sketch's lines, arcs and open polylines meeting end to end (within <see cref="Join"/>, in any order and either direction). Curved
/// edges are faceted by <see cref="Chords"/>, the one rule for every curve, so a body's volume is the faceted outline's.
/// </summary>
public static partial class PartSketches
{
    /// <summary>How near (m) two ends are to be one corner of a chain.</summary>
    public const double Join = 1e-6;

    /// <summary>A closed outline of a sketch: the shapes it is made of (in the order walked) and its corners, faceted.</summary>
    internal sealed record Loop(List<string> Ids, List<Vec2> Ring);

    static List<Vec2> CircleRing(Circle c)
    {
        var n = Chords.Circle(c.R);
        return [.. Enumerable.Range(0, n).Select(k => c.C + new Vec2(Math.Cos(2 * Math.PI * k / n), Math.Sin(2 * Math.PI * k / n)) * c.R)];
    }

    /// <summary>An arc's points from its start to its end, by <see cref="Chords"/>, its ends exact.</summary>
    static List<Vec2> ArcPoints(Arc a)
    {
        var sweep = a.Sweep;
        var n = Chords.Count(a.R, sweep);
        var o = new List<Vec2> { a.At(a.Start) };
        for (var k = 1; k < n; k++) o.Add(a.At(a.Start + sweep * k / n));
        o.Add(a.At(a.End));
        return o;
    }

    static bool Near(Vec2 p, Vec2 q) => Math.Abs(p.X - q.X) <= Join && Math.Abs(p.Y - q.Y) <= Join;

    /// <summary>
    /// A sketch's closed outlines, in the order of their first shape in the sketch: each closed polyline and circle on its own, and
    /// the chains its lines, arcs and open polylines close end to end. A chain is walked from the first shape not yet in one, each
    /// step taking the first shape (in the sketch's order) whose end meets the last; a shape is in one chain at most. A branching
    /// chain may so miss a loop a search would find: draw such outlines as separate shapes.
    /// </summary>
    internal static List<Loop> Loops(IReadOnlyList<(string Id, Shape Shape)> shapes)
    {
        var found = new List<(int Order, Loop Loop)>();
        var pieces = new List<(int Order, string Id, List<Vec2> Pts)>();
        for (var i = 0; i < shapes.Count; i++)
        {
            var (id, s) = shapes[i];
            switch (s)
            {
                case Polyline { Closed: true } p when p.Pts.Count >= 3: found.Add((i, new Loop([id], [.. p.Pts]))); break;
                case Circle c: found.Add((i, new Loop([id], CircleRing(c)))); break;
                case Line l when !Near(l.A, l.B): pieces.Add((i, id, [l.A, l.B])); break;
                case Arc a when a.Sweep > 1e-9: pieces.Add((i, id, ArcPoints(a))); break;
                case Polyline { Closed: false } p when p.Pts.Count >= 2: pieces.Add((i, id, [.. p.Pts])); break;
            }
        }
        var used = new bool[pieces.Count];
        for (var first = 0; first < pieces.Count; first++)
        {
            if (used[first]) continue;
            var chain = new List<int> { first };
            var ring = new List<Vec2>(pieces[first].Pts);
            var start = ring[0];
            var closed = false;
            while (true)
            {
                if (ring.Count >= 4 && Near(ring[^1], start)) { closed = true; break; }
                var end = ring[^1];
                var next = -1;
                var backward = false;
                for (var j = 0; j < pieces.Count && next < 0; j++)
                {
                    if (used[j] || chain.Contains(j)) continue;
                    if (Near(pieces[j].Pts[0], end)) next = j;
                    else if (Near(pieces[j].Pts[^1], end)) { next = j; backward = true; }
                }
                if (next < 0) break;
                chain.Add(next);
                var pts = backward ? Enumerable.Reverse(pieces[next].Pts).ToList() : pieces[next].Pts;
                ring.AddRange(pts.Skip(1));
            }
            if (!closed) continue;
            foreach (var k in chain) used[k] = true;
            ring.RemoveAt(ring.Count - 1);
            found.Add((chain.Min(k => pieces[k].Order), new Loop([.. chain.Select(k => pieces[k].Id)], ring)));
        }
        return [.. found.OrderBy(x => x.Order).Select(x => x.Loop)];
    }

    /// <summary>The closed outline <paramref name="id"/> is part of (or the first, for null); null when it is in none.</summary>
    internal static Loop? LoopOf(List<Loop> loops, string? id) => id == null ? loops.FirstOrDefault() : loops.FirstOrDefault(l => l.Ids.Contains(id));

    /// <summary>
    /// A local sketch of the part tree completed: <c>bodies</c>, the names of the bodies drawn from it; on each shape <c>inProfile</c>
    /// (in an outline or a hole one of those bodies is pushed from) and <c>inLoop</c> (in a closed outline a body could take).
    /// </summary>
    internal static void Annotate(JsonObject def, JsonObject sketch, JsonObject described)
    {
        var sid = (string?)sketch["id"];
        var bodies = new List<(string Name, JsonObject Body)>();
        var i = 0;
        foreach (var b in (def["bodies"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var name = Parts.BodyName(b, ++i);
            if ((string?)b["sketch"] == sid) bodies.Add((name, b));
        }
        described["bodies"] = new JsonArray(bodies.Select(b => (JsonNode)b.Name).ToArray());
        var shapes = ShapesOf(described);
        var loops = Loops(shapes);
        var inLoop = loops.SelectMany(l => l.Ids).ToHashSet();
        var inProfile = new HashSet<string>();
        foreach (var (_, b) in bodies)
        {
            if (LoopOf(loops, (string?)b["shape"]) is { } outer) inProfile.UnionWith(outer.Ids);
            foreach (var h in (b["holes"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>())
                if (LoopOf(loops, h) is { } hole) inProfile.UnionWith(hole.Ids);
        }
        foreach (var s in (described["shapes"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var id = (string?)s["id"] ?? "";
            s["inProfile"] = inProfile.Contains(id);
            s["inLoop"] = inLoop.Contains(id);
        }
    }

    /// <summary>A body's outline and holes on its sketch's shapes: the loops they name, checked distinct.</summary>
    static (Loop Outer, List<Loop> Holes) ProfileLoops(IReadOnlyList<(string Id, Shape Shape)> shapes, string? outline, IEnumerable<string> holes, string what)
    {
        var loops = Loops(shapes);
        var outer = LoopOf(loops, outline) ?? throw new CommandException(outline == null
            ? $"{what} has no closed outline: draw a closed polyline, a circle, or lines and arcs meeting end to end."
            : $"{what}: „{outline}” is not a closed polyline, a circle, or in a closed chain of lines and arcs.");
        var hs = new List<Loop>();
        foreach (var h in holes)
        {
            var hole = LoopOf(loops, h) ?? throw new CommandException($"{what}: the hole „{h}” is not a closed polyline, a circle, or in a closed chain of lines and arcs.");
            if (hole == outer || hs.Contains(hole)) throw new CommandException($"{what}: the hole „{h}” is the outline or another hole.");
            hs.Add(hole);
        }
        return (outer, hs);
    }
}
