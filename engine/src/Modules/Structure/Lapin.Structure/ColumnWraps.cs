using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>
/// A column's finish layers as shapes (decision 719, <see cref="ColumnLayers"/>): laid outward round the convex hull of its
/// section (a concrete column's own outline; a steel I or H boxed, as fire boards are), each side of the hull offset by the
/// layers that cover its face, the corners mitred. A layer all round is a ring; one on some faces only a strip per run of
/// faces it covers, ending flush with the plane of the next face's layers. In the section's own axes (x along its width);
/// <see cref="Placed"/> lays a ring at the column.
/// </summary>
public static class ColumnWraps
{
    /// <summary>The section's hull, counter-clockwise, in its own axes.</summary>
    public static IReadOnlyList<Vec2> Hull(Section s) => Polygon2.ConvexHull(s.Outline());

    /// <summary>The face a side of the hull looks to: the axis its outward normal leans to most (x on a tie).</summary>
    public static string FaceOf(Vec2 a, Vec2 b)
    {
        var (nx, ny) = (b.Y - a.Y, a.X - b.X);
        return Math.Abs(nx) >= Math.Abs(ny) - 1e-12 ? (nx >= 0 ? "+x" : "-x") : (ny >= 0 ? "+y" : "-y");
    }

    /// <summary>The hull with each side moved out by its own distance, the corners where the moved sides meet.</summary>
    public static Vec2[] Offset(IReadOnlyList<Vec2> hull, IReadOnlyList<double> d)
    {
        var n = hull.Count;
        var o = new Vec2[n];
        for (var j = 0; j < n; j++)
        {
            var (pi, pj) = ((j - 1 + n) % n, j);
            Line(pi, out var p1, out var d1);
            Line(pj, out var p2, out var d2);
            var den = Vec2.Cross(d1, d2);
            o[j] = Math.Abs(den) < 1e-12 ? p2 : p1 + d1 * (Vec2.Cross(p2 - p1, d2) / den);
        }
        return o;

        void Line(int e, out Vec2 p, out Vec2 dir)
        {
            var (a, b) = (hull[e], hull[(e + 1) % n]);
            dir = (b - a).Normalized();
            p = a + new Vec2(dir.Y, -dir.X) * d[e];
        }
    }

    /// <summary>
    /// Each layer's regions in the section's axes, the core's hull inside them: per layer, its rings (an outer ring with the
    /// inside one as its hole when it goes all round; else one strip per run of the faces it covers); and the outermost outline.
    /// </summary>
    public static (List<List<IReadOnlyList<IReadOnlyList<Vec2>>>> Layers, IReadOnlyList<Vec2> Outline) Shapes(Section s, IReadOnlyList<WrapLayer> layers)
    {
        var hull = Hull(s);
        var n = hull.Count;
        var faces = Enumerable.Range(0, n).Select(e => FaceOf(hull[e], hull[(e + 1) % n])).ToArray();
        var inner = new double[n];
        var o = new List<List<IReadOnlyList<IReadOnlyList<Vec2>>>>();
        if (n < 3) return (o, hull);
        foreach (var l in layers)
        {
            var covers = faces.Select(l.Covers).ToArray();
            var outer = inner.Select((d, e) => covers[e] ? d + l.Thickness : d).ToArray();
            var (oi, ii) = (Offset(hull, outer), Offset(hull, inner));
            var regions = new List<IReadOnlyList<IReadOnlyList<Vec2>>>();
            if (covers.All(c => c)) regions.Add([oi, ii.Reverse().ToArray()]);
            else if (covers.Any(c => c))
            {
                // Runs of covered sides, cyclic: start after a side it does not cover.
                var start = Array.FindIndex(covers, c => !c);
                for (var k = 1; k <= n; k++)
                {
                    var e = (start + k) % n;
                    if (!covers[e] || covers[(e - 1 + n) % n]) continue;
                    var run = new List<int>();
                    for (var x = e; covers[x] && run.Count < n; x = (x + 1) % n) run.Add(x);
                    var strip = new List<Vec2>();
                    for (var r = 0; r <= run.Count; r++) strip.Add(oi[(run[0] + r) % n]);
                    for (var r = run.Count; r >= 0; r--) strip.Add(ii[(run[0] + r) % n]);
                    regions.Add([strip]);
                }
            }
            o.Add(regions);
            inner = outer;
        }
        return (o, Offset(hull, inner));
    }

    /// <summary>A ring of the section's axes laid at the column: at its point, turned by its rotation.</summary>
    public static IReadOnlyList<Vec2> Placed(Entity column, IReadOnlyList<Vec2> ring) => Place.Placed(ring, Place.Point(column), column.Num("rotation")).ToList();
}
