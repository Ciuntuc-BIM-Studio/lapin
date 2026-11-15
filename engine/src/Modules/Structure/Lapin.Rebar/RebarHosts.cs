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
