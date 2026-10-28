using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// A run's route edited after it is placed (decision 164): its points are
/// where it is drawn (a connected end that follows its port is written back
/// when the port moves), a leg slides across itself with its neighbours
/// stretching (a jog where it meets an end or a leg in line with it), a span
/// of a leg goes round an obstacle (out, along, back), the whole path drawn
/// again with its connections kept. Every route is laid by the material's
/// rules (<see cref="MepHooks.Laying"/>) before it is followed.
/// </summary>
public static partial class Runs
{
    const double Eps = 1e-6;

    /// <summary>
    /// The run's points written as it is drawn (its ends on what they are on,
    /// <see cref="Follow"/>), its levels carried; then the runs branching off
    /// it, at their stations. <paramref name="skip"/>: runs carried by the
    /// same transformation, which move by themselves.
    /// </summary>
    public static void Refollow(ICommandContext ctx, string runId, IReadOnlySet<string>? skip = null, HashSet<string>? seen = null)
    {
        seen ??= [];
        if (!seen.Add(runId) || ctx.Model.Get(runId) is not { } run || !IsRun(run)) return;
        var old = Points(run);
        if (old.Count >= 2)
        {
            var s = SettingOf(ctx.Model, run);
            var now = Follow(ctx.Model, run, s, Nodes.StoreyZ(ctx.Model, run) + s.Elevation).Points;
            if (now.Count != old.Count || now.Where((p, i) => (p - old[i]).Length > Eps).Any())
            {
                var attrs = run.CloneAttrs();
                attrs["points"] = J(now.Select(Round));
                if (RemapLevels(old, LevelsOf(run), now, s.Elevation) is { } levels) attrs["levels"] = levels; else attrs.Remove("levels");
                ctx.Replace(run.With(attrs: attrs));
            }
        }
        foreach (var (branch, _) in BranchesOf(ctx.Model, runId).ToList())
            if (skip == null || !skip.Contains(branch.Id)) Refollow(ctx, branch.Id, skip, seen);
    }

    /// <summary>The runs on a node's ports written as they follow it (a node moved, turned, mirrored or changed).</summary>
    public static void RefollowOn(ICommandContext ctx, string nodeId, IReadOnlySet<string>? skip = null)
    {
        var seen = new HashSet<string>();
        foreach (var (run, _, _) in OnNode(ctx.Model, nodeId).ToList())
            if (skip == null || !skip.Contains(run.Id)) Refollow(ctx, run.Id, skip, seen);
    }

    static Vec2 Round(Vec2 p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6));

    /// <summary>
    /// A new route for the run: laid by its material, its levels carried from
    /// the old points (or given), its ends followed; the branches on it stay
    /// where they joined it (their stations measured again on the new route).
    /// </summary>
    public static void SetRoute(ICommandContext ctx, Entity run, IReadOnlyList<Vec2> points, JsonArray? levels = null)
    {
        var before = Followed(ctx.Model, run);
        var setting = SettingOf(ctx.Model, run);
        var next = Lay(setting.Service, setting.Material, points);
        var attrs = run.CloneAttrs();
        attrs["points"] = J(next);
        var lv = levels != null && levels.Count == next.Count ? (JsonArray)levels.DeepClone() : RemapLevels(Points(run), LevelsOf(run), next, setting.Elevation);
        if (lv != null) attrs["levels"] = lv; else attrs.Remove("levels");
        ctx.Replace(run.With(attrs: attrs));
        // Followed alone first, so the branches are measured on the route as it now stands.
        Refollow(ctx, run.Id, BranchesOf(ctx.Model, run.Id).Select(b => b.Branch.Id).ToHashSet());
        var after = Points(ctx.Model.Get(run.Id)!);
        foreach (var rel in ctx.Model.RelationsOf(run.Id, Connects).Where(r => r.Related.Contains(run.Id) && r.Relating != run.Id).ToList())
        {
            var st = Json.Number(rel.Attrs?["station"]) ?? 0;
            var ns = Math.Round(Pipes.Nearest(after, Pipes.AtStation(before, st).At).Station, 6);
            if (Math.Abs(ns - st) < Eps) continue;
            var na = rel.Attrs?.DeepClone() as JsonObject ?? [];
            na["station"] = ns;
            ctx.ReplaceRelation(rel with { Attrs = na });
        }
        foreach (var (branch, _) in BranchesOf(ctx.Model, run.Id).ToList()) Refollow(ctx, branch.Id);
    }

    static bool Parallel(Vec2 a, Vec2 b) => a.Length < Eps || b.Length < Eps || Math.Abs(Vec2.Cross(a.Normalized(), b.Normalized())) < 1e-6;

    /// <summary>
    /// A polyline without the points it no longer needs: one on the point
    /// before it, and (among the points an edit made, <c>Made</c>) one in line
    /// with its neighbours. The first and last stay.
    /// </summary>
    static List<Vec2> Clean(List<(Vec2 P, bool Made)> pts)
    {
        var o = new List<(Vec2 P, bool Made)>(pts);
        for (var changed = true; changed;)
        {
            changed = false;
            for (var i = 1; i < o.Count && o.Count > 2; i++)
                if ((o[i].P - o[i - 1].P).Length < 1e-5)
                {
                    // The one kept takes what the other was (an edit's corner on an old point is still the edit's).
                    var made = o[i].Made || o[i - 1].Made;
                    if (i == o.Count - 1) { o.RemoveAt(i - 1); o[^1] = (o[^1].P, made); }
                    else { o.RemoveAt(i); o[i - 1] = (o[i - 1].P, made); }
                    changed = true; break;
                }
            if (changed) continue;
            for (var i = 1; i + 1 < o.Count; i++)
                if (o[i].Made && Parallel(o[i].P - o[i - 1].P, o[i + 1].P - o[i].P))
                {
                    o.RemoveAt(i);
                    changed = true; break;
                }
        }
        return [.. o.Select(x => x.P)];
    }

    /// <summary>
    /// A leg slid across itself by the part of <paramref name="by"/> square to
    /// it. Where it meets a leg at an angle, that leg stretches (its direction
    /// kept); where it meets the run's end, or a leg in line with it, a jog is
    /// made there instead (two new corners), so an end on a port stays on it.
    /// </summary>
    public static List<Vec2> SlideLeg(IReadOnlyList<Vec2> pts, int leg, Vec2 by)
    {
        if (leg < 0 || leg >= pts.Count - 1) throw new CommandException($"The run has legs 0 to {pts.Count - 2}.");
        Vec2 a = pts[leg], b = pts[leg + 1], d = b - a;
        if (d.Length < Eps) throw new CommandException("That leg has no length.");
        var u = d.Normalized();
        var n = new Vec2(-u.Y, u.X);
        var off = n * Vec2.Dot(by, n);
        if (off.Length < Eps) return [.. pts];
        var o = new List<(Vec2 P, bool Made)>();
        for (var i = 0; i < leg; i++) o.Add((pts[i], false));
        if (leg == 0 || Parallel(a - pts[leg - 1], u)) { o.Add((a, true)); o.Add((a + off, true)); }
        else o.Add((Pipes.Intersect(pts[leg - 1], a - pts[leg - 1], a + off, u) ?? a + off, true));
        if (leg + 1 == pts.Count - 1 || Parallel(pts[leg + 2] - b, u)) { o.Add((b + off, true)); o.Add((b, true)); }
        else o.Add((Pipes.Intersect(pts[leg + 2], b - pts[leg + 2], b + off, u) ?? b + off, true));
        for (var i = leg + 2; i < pts.Count; i++) o.Add((pts[i], false));
        return Clean(o);
    }

    /// <summary>
    /// A span of one leg taken round an obstacle: from the station nearest
    /// <paramref name="from"/> to the one nearest <paramref name="to"/> (both
    /// on the leg <paramref name="from"/> falls on), the run goes out square to
    /// the leg by <paramref name="offset"/> (signed: + to the leg's left), along,
    /// and back: four new corners.
    /// </summary>
    public static List<Vec2> Detour(IReadOnlyList<Vec2> pts, Vec2 from, Vec2 to, double offset)
    {
        if (pts.Count < 2) throw new CommandException("A run needs two points.");
        var (station, _) = Pipes.Nearest(pts, from);
        var (_, leg, _) = Pipes.AtStation(pts, station);
        Vec2 a = pts[leg], d = pts[leg + 1] - a;
        var len = d.Length;
        if (len < Eps) throw new CommandException("That leg has no length.");
        var u = d * (1 / len);
        var n = new Vec2(-u.Y, u.X);
        double t0 = Math.Clamp(Vec2.Dot(from - a, u), 0, len), t1 = Math.Clamp(Vec2.Dot(to - a, u), 0, len);
        if (t0 > t1) (t0, t1) = (t1, t0);
        if (t1 - t0 < 0.01) throw new CommandException("A detour goes between two points apart on one leg.");
        if (Math.Abs(offset) < 1e-4) throw new CommandException("How far does the detour go out?");
        Vec2 p0 = a + u * t0, p1 = a + u * t1, o = n * offset;
        var list = new List<(Vec2 P, bool Made)>();
        for (var i = 0; i <= leg; i++) list.Add((pts[i], false));
        list.Add((p0, true)); list.Add((p0 + o, true)); list.Add((p1 + o, true)); list.Add((p1, true));
        for (var i = leg + 1; i < pts.Count; i++) list.Add((pts[i], false));
        return Clean(list);
    }

    /// <summary>The signed distance of a point from the leg nearest <paramref name="on"/> (+: the leg's left), for a detour's side.</summary>
    public static double SideOf(IReadOnlyList<Vec2> pts, Vec2 on, Vec2 side)
    {
        var (station, _) = Pipes.Nearest(pts, on);
        var (_, leg, dir) = Pipes.AtStation(pts, station);
        return Vec2.Dot(side - pts[leg], new Vec2(-dir.Y, dir.X));
    }

    static Entity RequireRun(ICommandContext ctx, JsonObject a)
    {
        var run = ctx.Model.Get((string?)a["id"] ?? (string?)a["runId"] ?? "") ?? throw new CommandException("Which run?");
        return IsRun(run) ? run : throw new CommandException("Not a run.");
    }

    static Vec2 PointArg(JsonNode? n, string what) => n is JsonArray p && p.Count >= 2 ? new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0) : throw new CommandException($"Where is the {what}? [x, y]");

    static void RegisterRouteEdits(IModuleRegistry r)
    {
        // A leg slid across itself (the part of "by" square to it); a jog where it meets an end or a leg in line.
        r.Command("slideLeg", "Slide leg", (ctx, a) =>
        {
            var run = RequireRun(ctx, a);
            var leg = (int?)Json.Number(a["leg"]) ?? throw new CommandException("Which leg?");
            SetRoute(ctx, run, SlideLeg(Followed(ctx.Model, run), leg, PointArg(a["by"], "displacement")));
            return null;
        });

        // A span of one leg taken round an obstacle: from, to (on the leg), out by offset (signed) or to the side of a point.
        r.Command("detour", "Detour", (ctx, a) =>
        {
            var run = RequireRun(ctx, a);
            var pts = Followed(ctx.Model, run);
            var from = PointArg(a["from"], "detour's start");
            var to = PointArg(a["to"], "detour's end");
            var offset = Json.Number(a["offset"]) is { } off
                ? (a["side"] is JsonArray ? Math.Abs(off) * Math.Sign(SideOf(pts, from, PointArg(a["side"], "side"))) : off)
                : a["side"] is JsonArray ? SideOf(pts, from, PointArg(a["side"], "side")) : throw new CommandException("How far, and to which side? (offset or side)");
            SetRoute(ctx, run, Detour(pts, from, to, offset));
            return null;
        });

        // The run's path drawn again: its system, size and connections kept, its connected ends on their ports.
        r.Command("redrawRun", "Redraw run", (ctx, a) =>
        {
            var run = RequireRun(ctx, a);
            SetRoute(ctx, run, PointsArg(a["points"]));
            return null;
        });
    }
}
