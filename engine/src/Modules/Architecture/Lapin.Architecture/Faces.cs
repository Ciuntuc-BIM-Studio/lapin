using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// The named faces of floors and walls (<see cref="IElementFaces"/>): a floor's
/// soffit, top and edges (one per side of its outline, in its order); a wall's
/// left and right faces (seen along its axis, its openings as holes; a curved
/// wall's as curves along the arc at its base), its top, its ends, and the
/// faces of its cast zones (ring beams, concrete bands: Zone[key].Left/Right).
/// </summary>
public sealed class ArchitectureFaces : IElementFaces
{
    public bool Covers(string ifcClass) => ifcClass is IfcClass.Slab or IfcClass.Wall;

    public IReadOnlyList<ElementFace> FacesOf(Entity e, IModelView m) => e.Class switch
    {
        IfcClass.Slab => Slab(e, m),
        IfcClass.Wall => Wall(e, m),
        _ => [],
    };

    static List<ElementFace> Slab(Entity slab, IModelView m)
    {
        var contour = Slabs.Contour(slab);
        if (contour.Count < 3) return [];
        var ccw = Polygon2.EnsureCcw(contour);
        var top = Slabs.Top(m, slab);
        var section = Slabs.Section(m, slab);
        var bottom = top + section.Min(b => b.From);
        top += section.Max(b => b.To);
        var holes = Slabs.Holes(m, slab.Id).Select(h => h.Contour).ToList();
        var faces = new List<ElementFace>
        {
            ElementFaces.Level("Soffit", "Soffit", ccw, bottom, true, holes),
            ElementFaces.Level("Top", "Top", ccw, top, false, holes),
        };
        for (var i = 0; i < ccw.Count; i++)
        {
            var (a, b) = (ccw[i], ccw[(i + 1) % ccw.Count]);
            if ((b - a).Length < 1e-6) continue;
            faces.Add(ElementFaces.Upright($"Edge[{i}]", $"Edge {i + 1}", a, b, bottom, top, -(b - a).Normalized().Perp));
        }
        return faces;
    }

    static List<ElementFace> Wall(Entity wall, IModelView m)
    {
        if (Walls.Frame(wall, m) is not { } f) return [];
        var z0 = (Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");
        var z1 = z0 + wall.Num("height", Walls.DefaultHeight);
        if (PathCurve.Of(wall) is { IsArc: true } c)
        {
            // A curved wall: its faces as curves along the arc at its base, its top a band.
            var mid = c.TangentAt(c.Length / 2).Perp;
            return
            [
                ElementFaces.Along("Face.Left", "Left face", c.Line(0, c.Length, f.YMax), z0, new Vec3(mid.X, mid.Y, 0)),
                ElementFaces.Along("Face.Right", "Right face", c.Line(0, c.Length, f.YMin), z0, new Vec3(-mid.X, -mid.Y, 0)),
                ElementFaces.Level("Top", "Top", c.Band(_ => 0, _ => c.Length, f.YMin, f.YMax), z1, false),
            ];
        }
        var dir = (f.End - f.Start).Normalized();
        var left = dir.Perp;
        Vec2 P(Vec2 at, double y) => at + left * y;
        // The openings through it, as holes in both faces.
        var holes = Openings.PlacedOn(m, wall.Id).Select(o => (A: f.Start + dir * (o.Along - o.Width / 2), B: f.Start + dir * (o.Along + o.Width / 2), Z0: z0 + o.Sill, Z1: z0 + o.Sill + o.Height)).ToList();
        ElementFace Face(string role, string label, double y, Vec2 outward)
        {
            var face = ElementFaces.Upright(role, label, P(f.Start, y), P(f.End, y), z0, z1, outward);
            return holes.Count == 0 ? face : face with { Holes = holes.Select(h => (IReadOnlyList<Vec3>)[P(h.A, y).At(h.Z0), P(h.B, y).At(h.Z0), P(h.B, y).At(h.Z1), P(h.A, y).At(h.Z1)]).ToList() };
        }
        var faces = new List<ElementFace>
        {
            Face("Face.Left", "Left face", f.YMax, left),
            Face("Face.Right", "Right face", f.YMin, -left),
            ElementFaces.Level("Top", "Top", [P(f.Start, f.YMin), P(f.End, f.YMin), P(f.End, f.YMax), P(f.Start, f.YMax)], z1, false),
            ElementFaces.Upright("End.Start", "Start", P(f.Start, f.YMin), P(f.Start, f.YMax), z0, z1, -dir),
            ElementFaces.Upright("End.End", "End", P(f.End, f.YMin), P(f.End, f.YMax), z0, z1, dir),
        };
        // The cast zones of a layered wall (ring beams, concrete bands): their faces, each side.
        if (Walls.TypeOf(m, wall.Id) is { } type)
            foreach (var band in WallZones.Bands(WallZones.Of(type), z1 - z0).Where(b => b.Zone.Role is "ringBeam" or "concrete"))
            {
                faces.Add(ElementFaces.Upright($"Zone[{band.Zone.Key}].Left", $"{band.Zone.Name}, left", P(f.Start, f.YMax), P(f.End, f.YMax), z0 + band.Z0, z0 + band.Z1, left));
                faces.Add(ElementFaces.Upright($"Zone[{band.Zone.Key}].Right", $"{band.Zone.Name}, right", P(f.Start, f.YMin), P(f.End, f.YMin), z0 + band.Z0, z0 + band.Z1, -left));
            }
        return faces;
    }
}
