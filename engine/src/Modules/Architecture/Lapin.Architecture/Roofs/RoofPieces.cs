using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// A roof split or trimmed by a line (<see cref="OutlineSplits"/>, audit 2026-11-15 §15). Each part keeps the rules of its
/// edges written out (kind, pitch, overhang, gutter: what the roof's shape gave them, so a part's own shape cannot change
/// them); the cut's edge is a gable with no overhang. The cut is allowed only where it leaves the roof as it was: each part's
/// planes are solved again and must stand where the roof's stood (within a millimetre) over all of the part; elsewhere it is
/// refused, as the planes of a part would rise or fall to new ridges. A barrel roof is refused. Its roof windows and dormers
/// go to the part they stand on, its downpipes to the part their outlet is on, and walls attached to it by name to both parts.
/// </summary>
sealed class RoofPieces : IOutlinePieces
{
    public bool Handles(Entity e) => e.Class == IfcClass.Roof && e.Attrs["contour"] is JsonArray;

    public string What(Entity e) => "A roof";

    public string? Refuse(IModelView m, Entity e, bool split) =>
        e.Str("shape") == "barrel" ? $"„{e.Name}” is a barrel vault: it is not cut by a line." : null;

    static JsonObject RuleJson(EdgeRule r, JsonObject? old)
    {
        var o = new JsonObject { ["kind"] = r.Kind == EdgeKind.Gable ? "gable" : "eave", ["pitch"] = r.Pitch, ["overhang"] = r.Overhang };
        if (old?["gutter"] is JsonValue g) o["gutter"] = g.DeepClone();
        return o;
    }

    public void Piece(ICommandContext ctx, Entity e, JsonObject attrs, IReadOnlyList<Vec2> corners, double[]? rises, bool first, Func<Vec2, bool> on)
    {
        var m = ctx.Model;
        var was = Roof.Contour(e);
        var rules = Roof.Rules(e, was);
        // Each edge the old one it lies along; the cut's, none (not the same index, as a redrawn outline's would).
        var from = RoofEdges.Match(was, corners).Select((i, k) => i >= 0 && Along(was[i], was[(i + 1) % was.Count], corners[k], corners[(k + 1) % corners.Count]) ? i : -1).ToArray();
        var overrides = e.Attrs["edges"] as JsonArray;
        attrs["edges"] = new JsonArray([.. from.Select(i => (JsonNode)(i >= 0
            ? RuleJson(rules[i], overrides is { } o && i < o.Count ? o[i] as JsonObject : null)
            : new JsonObject { ["kind"] = "gable", ["overhang"] = 0.0, ["gutter"] = false }))]);
        attrs.Remove("lowEdge");
        attrs.Remove("shedFlip");
        if (attrs["openings"] is JsonArray ops)
            attrs["openings"] = new JsonArray([.. ops.Where(x => x is JsonObject w && on(new Vec2(Json.Number(w["x"]) ?? 0, Json.Number(w["y"]) ?? 0))).Select(x => x!.DeepClone())]);

        // The part's planes where the roof's were.
        if (e.Str("shape") == "flat" || Roof.Resolve(e, m) is not { } before || Roof.Solve(before).Shape is not { } old) return;
        var probe = new Entity(e.Id, e.Class, e.Name, attrs, e.PredefinedType);
        if (Roof.Resolve(probe, m) is not { } after) return;
        var (shape, problem) = Roof.Solve(after);
        if (shape == null) throw new CommandException($"„{e.Name}” cut there would not build: {problem}");
        var (x0, x1, y0, y1) = (corners.Min(p => p.X), corners.Max(p => p.X), corners.Min(p => p.Y), corners.Max(p => p.Y));
        var step = Math.Max(0.1, Math.Max(x1 - x0, y1 - y0) / 40);
        for (var x = x0 + step / 2; x < x1; x += step)
            for (var y = y0 + step / 2; y < y1; y += step)
            {
                var p = new Vec2(x, y);
                if (!Polygon2.Contains(corners, p) || Edges(corners).Min(s => Distance(s.A, s.B, p)) < 0.05) continue;
                if (old.HeightAt(p) is not { } z0 || shape.HeightAt(p) is not { } z1) continue;
                if (Math.Abs(z0 - z1) > 1e-3)
                    throw new CommandException($"Cut there, „{e.Name}” would change its slopes (each part keeps its edges' rules): cut it where its planes allow, as across a gable roof's ridge.");
            }
    }

    static bool Along(Vec2 p, Vec2 q, Vec2 a, Vec2 b)
    {
        var e = (q - p).Normalized();
        return Vec2.Dot((b - a).Normalized(), e) > 0.9999 && Math.Abs(Vec2.Cross(e, a - p)) < 1e-4 && Math.Abs(Vec2.Cross(e, b - p)) < 1e-4;
    }

    static IEnumerable<(Vec2 A, Vec2 B)> Edges(IReadOnlyList<Vec2> c) => c.Select((p, i) => (p, c[(i + 1) % c.Count]));

    static double Distance(Vec2 a, Vec2 b, Vec2 p)
    {
        var ab = b - a;
        var l2 = Vec2.Dot(ab, ab);
        var t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p - a, ab) / l2, 0, 1);
        return (a + ab * t - p).Length;
    }

    public void Rehost(ICommandContext ctx, Entity e, Entity? second, Func<Vec2, bool> onFirst)
    {
        var m = ctx.Model;
        var pipes = m.OfClass(Drainage.DownpipeClass).Where(p => p.Str("roofId") == e.Id).ToList();
        if (pipes.Count > 0 && Drainage.DrainOf(e, m) is { } drain)
        {
            Vec2 Outlet(Entity pipe)
            {
                var g = drain.Gutters.FirstOrDefault(x => x.Edge == (int)pipe.Num("edge"));
                if (g == null) return default;
                var len = (g.B - g.A).Length;
                return (g.A + (g.B - g.A) * (len < 1e-9 ? 0 : Math.Clamp(pipe.Num("station"), 0, len) / len)).Xy;
            }
            var mine = pipes.Where(p => onFirst(Outlet(p))).ToList();
            var theirs = pipes.Except(mine).ToList();
            if (mine.Count > 0) RoofEdges.Downpipes(ctx, e, m.Get(e.Id)!, RoofEdges.Match(Roof.Contour(e), Roof.Contour(m.Get(e.Id)!)), p => p, mine);
            if (second != null && theirs.Count > 0) RoofEdges.Downpipes(ctx, e, m.Get(second.Id)!, RoofEdges.Match(Roof.Contour(e), Roof.Contour(m.Get(second.Id)!)), p => p, theirs);
            else foreach (var p in theirs) ctx.Remove(p.Id);
        }
        // Walls attached to it by name: to both parts.
        if (second == null) return;
        foreach (var w in m.OfClass(IfcClass.Wall).Where(w => w.Attrs["roofIds"] is JsonArray ids && ids.Any(x => (string?)x == e.Id)).ToList())
        {
            var a = w.CloneAttrs();
            ((JsonArray)a["roofIds"]!).Add((JsonNode)second.Id);
            ctx.Replace(w.With(attrs: a));
        }
    }
}

/// <summary>A drawn room (not one bounded by its walls) split or trimmed by a line: the new part takes the storey's next number.</summary>
sealed class RoomPieces : IOutlinePieces
{
    public bool Handles(Entity e) => e.Class == Rooms.Class && e.Attrs["contour"] is JsonArray;

    public string What(Entity e) => "A room";

    public string? Refuse(IModelView m, Entity e, bool split) =>
        e.Bool("auto") ? $"„{e.Name}” is bounded by its walls: place a room separation line across it instead." : null;

    public void Piece(ICommandContext ctx, Entity e, JsonObject attrs, IReadOnlyList<Vec2> corners, double[]? rises, bool first, Func<Vec2, bool> on)
    {
        if (!first && Spatial.StoreyOf(ctx.Model, e.Id) is { } storey) attrs["number"] = Rooms.NextNumber(ctx.Model, storey.Id);
    }
}
