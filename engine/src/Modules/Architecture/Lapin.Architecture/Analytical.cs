using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using Lapin.Platform.Coordination;

namespace Lapin.Architecture;

/// <summary>
/// Walls, floors and roofs in the analytical model (docs/jg-structures.md): a wall on its core's centre line (its axis moved
/// across to the core's middle; an arc as chords) from its base to its height or the top of what is over it (a roof, an
/// attached slab: <see cref="WallTops"/>), with its doors, windows and openings, load-bearing when Pset_WallCommon.LoadBearing
/// (on it or its type) says so; a floor's core at its mid-plane over its outline, with its openings. A roof without framing is
/// its deck at the deck's mid-plane, a sloping shell a plane; a framed roof its timber members (rafters, purlins, ridge,
/// plates, collars, trusses' chords and webs) as frames of its timber grade. The core's thickness and material are the section's.
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
            var height = w.Num("height", Walls.DefaultHeight);
            // Its doors, windows and openings, each over its span; a wall under a roof or an attached slab up to its top.
            var openings = new List<AnalyticalOpening>();
            foreach (var o in Openings.PlacedOn(m, w.Id))
            {
                var (oc, od) = curve.IsArc
                    ? (curve.PointAt(Math.Clamp(o.Along, 0, curve.Length)), (curve.PointAt(Math.Min(o.Along + 0.01, curve.Length)) - curve.PointAt(Math.Max(o.Along - 0.01, 0))).Normalized())
                    : (start + (end - start).Normalized() * o.Along, (end - start).Normalized());
                openings.Add(new AnalyticalOpening(o.Opening.Id, oc, od, o.Width / 2, z0 + o.Sill, z0 + o.Sill + o.Height));
            }
            List<Vec3>? top = null;
            if (!curve.IsArc && into.Services != null)
            {
                var dir = (end - start).Normalized();
                var total = bands.Max(b => b.To) - bands.Min(b => b.From);
                if (WallTops.Profile(w, new RoofTop.WallAxis(start, dir, (end - start).Length, z0, height, total), m, into.Services) is { Count: >= 2 } profile)
                    top = profile.Select(p => (line[0] + dir * p.U).At(z0 + p.V)).ToList();
            }
            into.Shells.Add(new AnalyticalShell(w.Id, w.Name, w.Class, "wall", type?.Id ?? $"wall-{Math.Round(thickness * 1000)}", type?.Name ?? $"Wall {Math.Round(thickness * 1000)} mm",
                thickness, core.Material, Spatial.StoreyOf(m, w.Id)?.Id, Provisions.LoadBearing(m, w),
                Line: line, Z0: z0, Z1: z0 + height,
                Note: curve.IsArc ? $"curved: {line.Count - 1} planar panels on its chords" : top != null ? "its top follows the roof or slab over it" : null)
            { Openings = openings, Top = top });
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
                Outline: contour, Z: Slabs.Top(m, s) + (from + to) / 2)
            { Holes = Slabs.Holes(m, s.Id).Select(h => h.Contour).ToList() });
        }
        foreach (var roof in m.OfClass(IfcClass.Roof)) RoofOf(m, roof, into);
    }

    /// <summary>A roof as the analytical model reads it: its deck (flat, or a sloping plane each), or its timber framing's members.</summary>
    static void RoofOf(IModelView m, Entity roof, AnalyticalContribution into)
    {
        if (StoredBodies.Has(roof)) { into.LeftOut.Add(new(roof.Id, roof.Name, roof.Class, "an imported roof: Lapin knows its body, not its structure")); return; }
        if (Roof.Resolve(roof, m) is not { } r) { into.LeftOut.Add(new(roof.Id, roof.Name, roof.Class, "no outline")); return; }
        if (r.Barrel) { into.LeftOut.Add(new(roof.Id, roof.Name, roof.Class, "a barrel vault is not in the analytical model yet")); return; }
        var storey = Spatial.StoreyOf(m, roof.Id)?.Id;
        var deckMaterial = RoofCoverings.DeckMaterialOf(roof, m);
        var deckKey = $"roof-deck-{Math.Round(r.DeckThickness * 1000)}";
        var deckName = $"Roof deck {Math.Round(r.DeckThickness * 1000)} mm";
        if (r.Flat)
        {
            into.Shells.Add(new AnalyticalShell(roof.Id, roof.Name, "IfcRoof", "slab", deckKey, deckName, r.DeckThickness, deckMaterial, storey, true,
                Outline: Roof.EaveOutline(r), Z: r.BaseZ + r.StructureDepth / 2, Note: "the roof's deck"));
            return;
        }
        var (shape, _) = Roof.Solve(r);
        if (shape == null) { into.LeftOut.Add(new(roof.Id, roof.Name, roof.Class, "the roof cannot be solved")); return; }
        var openings = RoofOpenings.Place(roof, shape, m, []);
        if (!r.Framed)
        {
            for (var i = 0; i < shape.Planes.Count; i++)
            {
                var pts = shape.Planes[i].Points;
                var n = VecMath.NewellNormal(pts).Normalized();
                if (n.Z < 0) n = -n;
                if (n.Z < 1e-6 || pts.Count < 3) continue;
                // The deck's mid-plane: the plane lifted half the deck, vertically (as its body is).
                var lift = r.StructureDepth / 2 / n.Z;
                var gradient = new Vec2(-n.X / n.Z, -n.Y / n.Z);
                var z = pts[0].Z + lift - Vec2.Dot(gradient, pts[0].Xy);
                into.Shells.Add(new AnalyticalShell(roof.Id, roof.Name, "IfcRoof", "slab", deckKey, deckName, r.DeckThickness, deckMaterial, storey, true,
                    Outline: pts.Select(p => p.Xy).ToList(), Z: z, Note: $"the roof's deck, plane {i + 1}")
                { Gradient = gradient, Holes = openings.Where(o => o.Host.PlaneIndex == i).Select(o => (IReadOnlyList<Vec2>)o.Hole.Select(p => p.Xy).ToList()).ToList() });
            }
            return;
        }
        into.Roofs.Add(new AnalyticalRoof(roof.Id, shape.Planes.Select(p => (IReadOnlyList<Vec3>)p.Points).ToList()));
        var k = 0;
        foreach (var mb in RoofParts.Framing(r, shape, openings))
        {
            k++;
            if (mb.Length < 1e-3) continue;
            var label = Roof.TimberSection(mb);
            var section = new FrameSection($"timber-{Math.Round(mb.Width * 1000)}x{Math.Round(mb.Depth * 1000)}", $"Timber {label}",
                new SectionShape("rectangle", mb.Width, mb.Depth), $"Roof „{roof.Name}”, {RoofParts.RoleLabel(mb.Role).ToLowerInvariant()} {label}");
            var vertical = Math.Abs((mb.B - mb.A).Normalized().Z) > 1 - 1e-6;
            into.Frames.Add(new AnalyticalFrame(roof.Id, $"{roof.Name}: {RoofParts.RoleLabel(mb.Role)} {k}", "IfcMember", vertical ? "column" : "beam", AnalyticalKinds.Roofs,
                [mb.A, mb.B], vertical ? new Vec3(0, 1, 0) : Vec3.UnitZ, section, r.Grade, storey, mb.Depth, mb.Width, mb.Truss >= 0 ? $"truss:{mb.Truss + 1}:{mb.Role}" : mb.Role));
        }
    }
}
