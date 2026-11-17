using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// A run in the platform's split, trim, extend and offset (<see cref="PolylineEdits"/>, audit 2026-11-15 §15). Its points and
/// its own elevations (one per point) are cut with it; the branches that join it go to the part they join, their stations
/// counted from its start, and one on a part cut away refuses. Split, the second part takes what the run's end was on, and a
/// coupling (an inline fitting, IfcPipeFitting CONNECTOR) joins the two at the cut, their ends at its middle so their lengths
/// add up to the run's; a paired run (supply and return), or <c>coupling: false</c>, leaves the two ends open there. An end
/// trimmed or carried on lets go of the port or the run it was on.
/// </summary>
public sealed class RunPieces : IPolylinePieces
{
    public bool Handles(Entity e) => Runs.IsRun(e);

    public (List<Vec2> Points, double[] Rises) Line(Entity e) { var p = Runs.Points(e); return (p, new double[Math.Max(0, p.Count - 1)]); }

    public void Piece(IModelView m, Entity e, JsonObject attrs, PolySlice slice, bool newStart, bool newEnd)
    {
        attrs["points"] = Runs.J(slice.Points);
        if (Runs.LevelsOf(e) is { } levels && Runs.RemapLevels(Runs.Points(e), levels, slice.Points, Runs.SettingOf(m, e).Elevation) is { } kept) attrs["levels"] = kept;
        else attrs.Remove("levels");
    }

    public void Rehost(ICommandContext ctx, Entity e, IReadOnlyList<PathPiece> pieces, JsonObject args)
    {
        var m = ctx.Model;
        var length = Pipes.Length(Runs.Points(e));
        var setting = Runs.SettingOf(m, e);
        // Where the cut is, in 3D, before what the run is on changes.
        var split = pieces.Count == 2;
        Vec3? cut = split ? On(Runs.Centreline(m, e, setting, out _), new PolylineEdits.Line(Runs.Points(e), null).At(pieces[0].S1)) : null;

        // The branches: each on the part it joins.
        foreach (var r in m.RelationsOf(e.Id, Runs.Connects).Where(r => r.Related.Contains(e.Id) && r.Relating != e.Id).ToList())
        {
            var st = Json.Number(r.Attrs?["station"]) ?? 0;
            var to = pieces.FirstOrDefault(p => st >= p.S0 - 1e-6 && st <= p.S1 + 1e-6);
            if (to.Id == null)
                throw new CommandException($"„{m.Get(r.Relating)?.Name}” branches off „{e.Name}” on the part cut away: disconnect it first, or keep that part.");
            var na = r.Attrs?.DeepClone() as JsonObject ?? [];
            na["station"] = Math.Round(Math.Max(0, st - to.S0), 9);
            ctx.ReplaceRelation(r with { Related = [.. r.Related.Select(x => x == e.Id ? to.Id : x)], Attrs = na });
        }
        // Its own ends.
        if (split)
        {
            if (Runs.Connection(m, e.Id, "end") is { } endConn) ctx.ReplaceRelation(endConn with { Relating = pieces[1].Id });
        }
        else
        {
            if (Math.Abs(pieces[0].S0) > 1e-9) Runs.Disconnect(ctx, e, "start");
            if (Math.Abs(pieces[0].S1 - length) > 1e-9) Runs.Disconnect(ctx, e, "end");
        }
        if (!split || setting.Pair || args["coupling"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && !c) return;
        if (Runs.SystemOf(ctx.Model, e) is not { } system || Spatial.StoreyOf(ctx.Model, e.Id) is not { } storey) return;

        // The coupling at the cut, turned along the run, its ports at the run's height there.
        var first = ctx.Model.Get(pieces[0].Id)!;
        var pts = Runs.Points(first);
        var dir = pts[^1] - pts[^2];
        var role = Roles.Require("coupling");
        var at = cut!.Value;
        var zStorey = Spatial.Elevation(ctx.Model, e);
        var node = Nodes.Place(ctx, system, role, storey.Id, new JsonObject
        {
            ["x"] = Math.Round(at.X, 9), ["y"] = Math.Round(at.Y, 9), ["rotation"] = Math.Round(Math.Atan2(dir.Y, dir.X) * 180 / Math.PI, 6),
            ["elevation"] = Math.Round(at.Z - zStorey - role.Ports[0].At.Z, 9),
        });
        Runs.Connect(ctx, ctx.Model.Get(pieces[0].Id)!, "end", node, "in");
        Runs.Connect(ctx, ctx.Model.Get(pieces[1].Id)!, "start", ctx.Model.Get(node.Id)!, "out");
    }

    /// <summary>The point of a 3D line over a plan point (the nearest in plan, its height between its ends').</summary>
    static Vec3 On(IReadOnlyList<Vec3> line, Vec2 p)
    {
        var best = (D: double.MaxValue, P: new Vec3(p.X, p.Y, line.Count > 0 ? line[0].Z : 0));
        for (var i = 0; i + 1 < line.Count; i++)
        {
            var (a, b) = (line[i], line[i + 1]);
            var ab = b.Xy - a.Xy;
            var len2 = Vec2.Dot(ab, ab);
            var t = len2 < 1e-12 ? 0 : Math.Clamp(Vec2.Dot(p - a.Xy, ab) / len2, 0, 1);
            var q = a.Xy + ab * t;
            var d = (q - p).Length;
            if (d < best.D - 1e-9) best = (d, new Vec3(q.X, q.Y, a.Z + (b.Z - a.Z) * t));
        }
        return best.P;
    }
}
