using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// What a roof keeps by edge index — the per-edge rules (<c>edges</c>: kind, pitch, overhang, gutter), a shed's
/// <c>lowEdge</c>, its downpipes' <c>edge</c> and <c>station</c> — carried to the edges they belong to when the outline's
/// edges are renumbered: a mirror (the outline reversed to stay counter-clockwise, edge i becoming n−2−i and running the
/// other way) or an outline redrawn (each new edge takes the rules of the old edge it lies on).
/// </summary>
public static class RoofEdges
{
    /// <summary>For each edge of a mirrored outline of <paramref name="n"/> corners, the edge it was.</summary>
    public static int[] Mirrored(int n) => [.. Enumerable.Range(0, n).Select(k => ((n - 2 - k) % n + n) % n)];

    /// <summary>
    /// For each edge of the new outline, the old edge it continues: the one it lies along (parallel, the same way, on its
    /// line and overlapping it: a corner added on an edge keeps both halves its rules), else with as many corners the one
    /// of the same index (a corner moved); −1 when none.
    /// </summary>
    public static int[] Match(IReadOnlyList<Vec2> before, IReadOnlyList<Vec2> after)
    {
        var (n, m) = (before.Count, after.Count);
        var o = new int[m];
        for (var k = 0; k < m; k++)
        {
            var (a, b) = (after[k], after[(k + 1) % m]);
            var d = (b - a).Normalized();
            o[k] = -1;
            for (var i = 0; i < n; i++)
            {
                var (p, q) = (before[i], before[(i + 1) % n]);
                var e = (q - p).Normalized();
                if (Vec2.Dot(d, e) < 0.9999 || Math.Abs(Vec2.Cross(e, a - p)) > 1e-4) continue;
                var (s0, s1) = (Vec2.Dot(a - p, e), Vec2.Dot(b - p, e));
                if (s1 > 1e-4 && s0 < (q - p).Length - 1e-4) { o[k] = i; break; }
            }
            if (o[k] < 0 && n == m) o[k] = k;
        }
        return o;
    }

    /// <summary>The roof's attributes with its per-edge rules and low edge renumbered: <paramref name="from"/> gives each new edge's old one.</summary>
    public static void Renumber(JsonObject attrs, int[] from)
    {
        if (attrs["edges"] is JsonArray old)
        {
            var next = new JsonArray([.. from.Select(i => i >= 0 && i < old.Count ? old[i]?.DeepClone() : null)]);
            if (next.Any(x => x != null)) attrs["edges"] = next; else attrs.Remove("edges");
        }
        if (Json.Number(attrs["lowEdge"]) is { } low && low >= 0)
        {
            var k = Array.IndexOf(from, (int)low);
            if (k >= 0) attrs["lowEdge"] = k; else attrs.Remove("lowEdge");
        }
    }

    /// <summary>
    /// The roof's downpipes put back on their gutters after the roof became <paramref name="after"/>: each outlet's point
    /// (carried by <paramref name="carry"/>) found again on the gutter of the edge its old one became (the nearest gutter
    /// when that edge has none), at the station there.
    /// </summary>
    public static void Downpipes(ICommandContext ctx, Entity before, Entity after, int[] from, Func<Vec2, Vec2> carry, IEnumerable<Entity>? pipes = null)
    {
        var m = ctx.Model;
        pipes ??= m.OfClass(Drainage.DownpipeClass).Where(p => p.Str("roofId") == before.Id).ToList();
        if (Drainage.DrainOf(before, m) is not { } was) return;
        var now = Roof.Resolve(after, m) is { } r && Roof.Solve(r).Shape is { } shape ? RoofParts.Gutters(r, shape) : [];
        if (now.Count == 0) return;
        foreach (var pipe in pipes)
        {
            var edge = (int)pipe.Num("edge");
            if (was.Gutters.FirstOrDefault(g => g.Edge == edge) is not { } g0) continue;
            var at = carry((g0.A + (g0.B - g0.A) * (Math.Clamp(pipe.Num("station"), 0, g0.Length) / g0.Length)).Xy);
            var own = now.Where(g => g.Edge >= 0 && g.Edge < from.Length && from[g.Edge] == edge).ToList();
            var (best, station) = (own.Count > 0 ? own : now).Select(g =>
            {
                var (a, b) = (g.A.Xy, g.B.Xy);
                var s = Math.Clamp(Vec2.Dot(at - a, (b - a).Normalized()), 0, g.Length);
                return (G: g, S: s, D: (a + (b - a).Normalized() * s - at).Length);
            }).MinBy(x => x.D) is var x ? (x.G, x.S) : default;
            var attrs = pipe.CloneAttrs();
            attrs["edge"] = best.Edge;
            attrs["station"] = Math.Round(station, 4);
            if (pipe.Str("roofId") != after.Id) attrs["roofId"] = after.Id;
            ctx.Replace(pipe.With(attrs: attrs));
        }
    }
}

/// <summary>
/// How a roof moves, turns, mirrors and is copied: the platform's own placement, its roof windows and dormers carried
/// with it, and, mirrored, its edge-indexed rules and its downpipes renumbered (<see cref="RoofEdges"/>). A copy takes its
/// downpipe elements along. A downpipe follows its roof and is not moved on its own.
/// </summary>
sealed class RoofPlacement : IElementPlacement
{
    public bool Handles(Entity e) => e.Class == Drainage.DownpipeClass || e.Class == IfcClass.Roof && Roof.Contour(e).Count >= 3;

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        if (e.Class == Drainage.DownpipeClass) return;
        var before = ctx.Model.Get(e.Id) ?? e;
        Placement.Own(ctx, before, t, moving);
        Settle(ctx, before, ctx.Model.Get(e.Id)!, t, null);
    }

    public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        if (e.Class == Drainage.DownpipeClass) return null;
        var pipes = ctx.Model.OfClass(Drainage.DownpipeClass).Where(p => p.Str("roofId") == e.Id).ToList();
        var id = Placement.DeepCopy(ctx, e, t);
        var copy = ctx.Model.Get(id)!;
        var clones = new List<Entity>();
        foreach (var p in pipes)
        {
            var attrs = p.CloneAttrs();
            attrs["roofId"] = id;
            var c = new Entity(ctx.NewId(), p.Class, p.Name, attrs, p.PredefinedType);
            ctx.Add(c);
            if (Spatial.StoreyOf(ctx.Model, id) is { } storey) Spatial.Contain(ctx, storey.Id, c.Id);
            clones.Add(c);
        }
        Settle(ctx, e, copy, t, clones);
        return id;
    }

    /// <summary>The roof as carried: its openings' plan points with it, and, mirrored, its edges renumbered.</summary>
    static void Settle(ICommandContext ctx, Entity before, Entity after, Placement2D t, List<Entity>? copiedPipes)
    {
        var attrs = after.CloneAttrs();
        if (attrs["openings"] is JsonArray ops)
            foreach (var o in ops.OfType<JsonObject>())
            {
                var p = t.Point(new Vec2(Json.Number(o["x"]) ?? 0, Json.Number(o["y"]) ?? 0));
                (o["x"], o["y"]) = (p.X, p.Y);
            }
        var from = t.Mirrored ? RoofEdges.Mirrored(Roof.Contour(after).Count) : null;
        if (from != null) RoofEdges.Renumber(attrs, from);
        var next = after.With(attrs: attrs);
        ctx.Replace(next);
        if (from != null) RoofEdges.Downpipes(ctx, before, next, from, t.Point, copiedPipes?.Select(p => ctx.Model.Get(p.Id)!));
    }
}
