using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using Lapin.Platform.Coordination;

namespace Lapin.Architecture;

/// <summary>
/// Walls and floors in the analytical model (docs/jg-structures.md): a wall on its core's centre line (its axis moved across to
/// the core's middle; an arc as chords) from its base to its height, load-bearing when Pset_WallCommon.LoadBearing (on it or
/// its type) says so; a floor's core at its mid-plane over its outline. The core's thickness and material are the section's.
/// </summary>
public sealed class ArchitectureAnalytical : IAnalyticalElements
{
    public void Contribute(IModelView m, AnalyticalContribution into)
    {
        foreach (var w in m.OfClass(IfcClass.Wall))
        {
            var (start, end) = (Walls.Start(w), Walls.End(w));
            if ((end - start).Length < 1e-3) { into.LeftOut.Add(new(w.Id, w.Name, w.Class, "no length")); continue; }
            var bands = Walls.Section(m, w);
            var core = bands.FirstOrDefault(b => b.Core) is { To: var t } c && t - c.From > 1e-6 ? c : bands.MaxBy(b => b.To - b.From);
            var mid = (core.From + core.To) / 2;
            var curve = PathCurve.Of(w);
            var line = curve.IsArc ? curve.Line(0, curve.Length, mid)
                : [start + (end - start).Normalized().Perp * mid, end + (end - start).Normalized().Perp * mid];
            var z0 = (Spatial.StoreyOf(m, w.Id)?.Num("elevation") ?? 0) + w.Num("baseOffset");
            var type = Walls.TypeOf(m, w.Id);
            var thickness = core.To - core.From;
            into.Shells.Add(new AnalyticalShell(w.Id, w.Name, w.Class, "wall", type?.Id ?? $"wall-{Math.Round(thickness * 1000)}", type?.Name ?? $"Wall {Math.Round(thickness * 1000)} mm",
                thickness, core.Material, Spatial.StoreyOf(m, w.Id)?.Id, Provisions.LoadBearing(m, w),
                Line: line, Z0: z0, Z1: z0 + w.Num("height", Walls.DefaultHeight),
                Note: curve.IsArc ? $"curved: {line.Count - 1} planar panels on its chords" : null));
        }
        foreach (var s in m.OfClass(IfcClass.Slab))
        {
            var contour = Slabs.Contour(s);
            if (contour.Count < 3) { into.LeftOut.Add(new(s.Id, s.Name, s.Class, "no outline")); continue; }
            var bands = Slabs.Section(m, s);
            var layers = LayerSets.BandsOf(m, s.Id);
            var coreBand = layers?.FirstOrDefault(b => b.Core);
            var (from, to, material) = coreBand is { } cb && cb.To - cb.From > 1e-6 ? (cb.From, cb.To, (string?)cb.Material)
                : bands.MaxBy(b => b.To - b.From) is var thick ? (thick.From, thick.To, thick.Material) : default;
            var type = m.RelatingOf(s.Id, IfcClass.RelDefinesByType);
            var thickness = to - from;
            into.Shells.Add(new AnalyticalShell(s.Id, s.Name, s.Class, "slab", type?.Id ?? $"slab-{Math.Round(thickness * 1000)}", type?.Name ?? $"Floor {Math.Round(thickness * 1000)} mm",
                thickness, material, Spatial.StoreyOf(m, s.Id)?.Id, true,
                Outline: contour, Z: Slabs.Top(m, s) + (from + to) / 2,
                Note: Slabs.Holes(m, s.Id).Any() ? "its openings are not in the exchange file (contract 1.0 has none)" : null));
        }
    }
}
