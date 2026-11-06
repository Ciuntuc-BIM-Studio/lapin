using Lapin.Geometry;

namespace Lapin.Platform;

/// <summary>
/// A small element's body and plan symbol built in its own frame (an exhaust fan, a gate): x across it as seen from its
/// front, y from its front to its back, z up from where it stands; the origin <paramref name="at"/> at <paramref name="z"/>,
/// turned by <paramref name="rotationDeg"/>, its x mirrored by <paramref name="mirror"/>. Every solid is a prism of a planar
/// profile swept along a direction (blocks, cylinders on any of the three axes, a leaf's outline with its hole), closed and
/// low-poly; the profiles drawn in the front (x, z) are kept for a configurator's elevation preview.
/// </summary>
public sealed class LocalBuilder(Vec2 at, double z, double rotationDeg, bool mirror = false)
{
    readonly double c = Math.Cos(rotationDeg * Math.PI / 180), s = Math.Sin(rotationDeg * Math.PI / 180);

    /// <summary>The solids by material role, in the order they were made.</summary>
    public readonly Dictionary<string, List<Solid>> Solids = [];
    public readonly List<PlanItem> Plan = [];
    /// <summary>What a configurator's front view draws: each piece's outline in (x, z), by role, with its depth (y) to sort by.</summary>
    public readonly List<(string Role, IReadOnlyList<(double X, double Z)> Outline, double Y)> Front = [];
    /// <summary>The holes of the front's pieces (a leaf's frame round its infill), by the piece's index in <see cref="Front"/>.</summary>
    public readonly Dictionary<int, IReadOnlyList<IReadOnlyList<(double X, double Z)>>> FrontHoles = [];

    double Mx(double x) => mirror ? -x : x;
    public Vec3 W(double x, double y, double zz) { x = Mx(x); return new(at.X + c * x - s * y, at.Y + s * x + c * y, z + zz); }
    public Vec2 Q(double x, double y) { x = Mx(x); return new(at.X + c * x - s * y, at.Y + s * x + c * y); }
    /// <summary>A direction of the local frame in the world (not mirrored: a sweep's direction).</summary>
    Vec3 D(double dx, double dy, double dz) { dx = Mx(dx); return new(c * dx - s * dy, s * dx + c * dy, dz); }

    void Add(string role, Solid? solid) { if (solid == null) return; if (!Solids.TryGetValue(role, out var l)) Solids[role] = l = []; l.Add(solid); }

    /// <summary>A profile (local points) swept along a local direction, with holes.</summary>
    public void Prism(string role, IReadOnlyList<(double X, double Y, double Z)> profile, (double X, double Y, double Z) dir, IReadOnlyList<IReadOnlyList<(double X, double Y, double Z)>>? holes = null)
    {
        if (profile.Count < 3) return;
        var h = holes?.Select(x => (IReadOnlyList<Vec3>)x.Select(p => W(p.X, p.Y, p.Z)).ToList()).ToList();
        Add(role, Lapin.Geometry.Prism.Extrude(profile.Select(p => W(p.X, p.Y, p.Z)).ToList(), h, D(dir.X, dir.Y, dir.Z), "body"));
    }

    /// <summary>An outline in the front plane (x, z) at depth y0, swept to y1, with holes: a gate's leaf, a fan's plate.</summary>
    public void Slab(string role, IReadOnlyList<(double X, double Z)> outline, double y0, double y1, IReadOnlyList<IReadOnlyList<(double X, double Z)>>? holes = null)
    {
        if (outline.Count < 3 || Math.Abs(y1 - y0) < 1e-5) return;
        if (holes is { Count: > 0 }) FrontHoles[Front.Count] = holes;
        Front.Add((role, outline, Math.Min(y0, y1)));
        Prism(role, [.. outline.Select(p => (p.X, y0, p.Z))], (0, y1 - y0, 0), holes?.Select(hh => (IReadOnlyList<(double, double, double)>)[.. hh.Select(p => (p.X, y0, p.Z))]).ToList());
    }

    public void Box(string role, double x0, double x1, double y0, double y1, double z0, double z1)
    {
        if (x1 - x0 < 1e-5 || y1 - y0 < 1e-5 || z1 - z0 < 1e-5) return;
        Slab(role, [(x0, z0), (x1, z0), (x1, z1), (x0, z1)], y0, y1);
    }

    static IEnumerable<(double A, double B)> Ring(double ca, double cb, double r, int sides) =>
        Enumerable.Range(0, sides).Select(i => { var a = 2 * Math.PI * i / sides; return (ca + r * Math.Cos(a), cb + r * Math.Sin(a)); });

    /// <summary>A cylinder about the local y axis through (cx, cz), from y0 to y1: a fan's sleeve through its wall.</summary>
    public void CylinderY(string role, double cx, double cz, double r, double y0, double y1, int sides = 16)
    {
        if (r < 1e-4 || Math.Abs(y1 - y0) < 1e-5) return;
        Slab(role, [.. Ring(cx, cz, r, sides)], y0, y1);
    }

    /// <summary>A cylinder about the local x axis through (cy, cz), from x0 to x1: an inline fan's body, a gate's rail.</summary>
    public void CylinderX(string role, double cy, double cz, double r, double x0, double x1, int sides = 16)
    {
        if (r < 1e-4 || Math.Abs(x1 - x0) < 1e-5) return;
        Front.Add((role, [(x0, cz - r), (x1, cz - r), (x1, cz + r), (x0, cz + r)], cy - r));
        Prism(role, [.. Ring(cy, cz, r, sides).Select(p => (x0, p.A, p.B))], (x1 - x0, 0, 0));
    }

    /// <summary>An upright cylinder about (cx, cy), from z0 to z1.</summary>
    public void CylinderZ(string role, double cx, double cy, double r, double z0, double z1, int sides = 16)
    {
        if (r < 1e-4 || z1 - z0 < 1e-5) return;
        Front.Add((role, [(cx - r, z0), (cx + r, z0), (cx + r, z1), (cx - r, z1)], cy - r));
        Prism(role, [.. Ring(cx, cy, r, sides).Select(p => (p.A, p.B, z0))], (0, 0, z1 - z0));
    }

    /// <summary>A pyramid's frustum over a rectangle (a pier's cap): from the rectangle at z0 to a smaller one at z1, closed.</summary>
    public void Frustum(string role, double x0, double x1, double y0, double y1, double z0, double z1, double shrink)
    {
        if (x1 - x0 < 1e-5 || y1 - y0 < 1e-5 || z1 - z0 < 1e-5) return;
        var k = Math.Min(shrink, Math.Min(x1 - x0, y1 - y0) / 2 - 1e-3);
        Vec3[] v =
        [
            W(x0, y0, z0), W(x1, y0, z0), W(x1, y1, z0), W(x0, y1, z0),
            W(x0 + k, y0 + k, z1), W(x1 - k, y0 + k, z1), W(x1 - k, y1 - k, z1), W(x0 + k, y1 - k, z1),
        ];
        int[][] faces = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]];
        // Mirrored, the vertices' order turns: the faces too, so they still face out.
        if (mirror) faces = [.. faces.Select(f => f.Reverse().ToArray())];
        Front.Add((role, [(x0, z0), (x1, z0), (x1 - k, z1), (x0 + k, z1)], y0));
        Add(role, SolidBuilder.Make([.. v], [.. faces.Select(f => new FaceInput(f, null, null, "body"))], "body"));
    }

    // ---- Plan ----

    public void Line(string kind, params (double X, double Y)[] pts) => Plan.Add(new PlanItem(kind, false, [.. pts.Select(p => Q(p.X, p.Y))]));
    public void Loop(string kind, IEnumerable<(double X, double Y)> pts) => Plan.Add(new PlanItem(kind, true, [.. pts.Select(p => Q(p.X, p.Y))]));
    public void Rect(string kind, double x0, double x1, double y0, double y1) => Loop(kind, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]);
    public void Circle(string kind, double cx, double cy, double r, int sides = 24) => Loop(kind, Ring(cx, cy, r, sides));
    /// <summary>An arc about (cx, cy) from a0 to a1 (degrees, local, anticlockwise from +x).</summary>
    public void Arc(string kind, double cx, double cy, double r, double a0, double a1, int steps = 16) =>
        Line(kind, [.. Enumerable.Range(0, steps + 1).Select(i => { var a = (a0 + (a1 - a0) * i / steps) * Math.PI / 180; return (cx + r * Math.Cos(a), cy + r * Math.Sin(a)); })]);

    /// <summary>The solids of a role as one solid (they are never merged: pieces side by side).</summary>
    public static Solid Combine(IReadOnlyList<Solid> solids, string tag)
    {
        if (solids.Count == 1) return solids[0];
        var vertices = new List<Vec3>();
        var faces = new List<Face>();
        foreach (var x in solids)
        {
            var o = vertices.Count;
            vertices.AddRange(x.Vertices);
            faces.AddRange(x.Faces.Select(f => f with { Outer = f.Outer.Select(i => i + o).ToArray(), Holes = f.Holes.Select(h => h.Select(i => i + o).ToArray()).ToArray() }));
        }
        return new Solid([.. vertices], [.. faces], tag);
    }
}
