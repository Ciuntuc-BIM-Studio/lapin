using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// A bar's host deleted (audit 15.11.2026): the bars drawn in it let it go (<c>host</c>), and stay where they are. One laid
/// across its drawing by the cover in its host is laid at the places the cover gave it, as a step about the plane centred
/// where they were (<see cref="Rebar3D.Across"/>'s offset), since there is no host left to measure the cover in; undone,
/// the host and the bar's cover come back together.
/// </summary>
sealed class RebarHosts : IDeletionFollowers
{
    public void Deleting(ICommandContext ctx, IReadOnlySet<string> deleting)
    {
        var m = ctx.Model;
        var st = RebarSettings.Of(m);
        foreach (var bar in m.OfClass(Rebars.Class).Concat(m.OfClass(Rebars.RunClass)).Where(b => b.Str("host") is { } h && deleting.Contains(h) && !deleting.Contains(b.Id)).ToList())
        {
            var attrs = bar.CloneAttrs();
            attrs.Remove("host");
            if (Rebar3D.Across.Of(bar) is { Mode: "cover" } across
                && Rebar3D.FrameOf(m, ctx.Services, Views.Find(m, bar.Str("viewId") ?? "")) is { } frame
                && Rebar3D.Depths(m, ctx.Query.Geometry, bar, frame, st) is { Count: > 0 } depths)
            {
                var (lo, hi) = (depths.Min(), depths.Max());
                var step = depths.Count > 1 ? (hi - lo) / (depths.Count - 1) : across.Step;
                attrs["across"] = new Rebar3D.Across(across.Count, "step", step, (lo + hi) / 2).ToJson();
            }
            ctx.Replace(bar.With(attrs: attrs));
        }
    }
}

/// <summary>
/// A host split (audit 2026-11-15 §15): each bar on it hosted by the part its point lies on (its point in the view put in
/// space: along an axis, the part whose stretch holds it; an outline, the part whose outline holds it); one neither holds,
/// or one drawn where the view cannot be put in space, stays on the first. A trim leaves its bars on it.
/// </summary>
sealed class RebarParts : ISplitFollower
{
    public void Parts(ICommandContext ctx, Entity was, IReadOnlyList<PathPiece> pieces)
    {
        if (pieces.Count < 2) return;
        var m = ctx.Model;
        var axis = was.Attrs["start"] is System.Text.Json.Nodes.JsonArray && was.Attrs["end"] is System.Text.Json.Nodes.JsonArray ? PathCurve.Of(was) : (PathCurve?)null;
        foreach (var bar in m.OfClass(Rebars.Class).Where(b => b.Str("host") == was.Id).ToList())
        {
            if (Rebar3D.FrameOf(m, ctx.Services, Views.Find(m, bar.Str("viewId") ?? "")) is not { } frame) continue;
            var at = Rebars.At(bar);
            var w = frame.Origin + frame.Right * at.X + frame.Up * at.Y;
            string? host = null;
            if (axis is { } c)
            {
                var s = PathEdits.Along(c, w.Xy);
                host = pieces.Where(p => s >= p.S0 - 1e-6 && s <= p.S1 + 1e-6).Select(p => p.Id).FirstOrDefault();
            }
            else host = pieces.Select(p => p.Id).FirstOrDefault(id => m.Get(id) is { } part && ArcOutline.BuiltOf(part) is { Count: >= 3 } ring && Polygon2.Contains(ring, w.Xy));
            if (host == null || host == was.Id) continue;
            var attrs = bar.CloneAttrs();
            attrs["host"] = host;
            ctx.Replace(bar.With(attrs: attrs));
        }
    }
}
