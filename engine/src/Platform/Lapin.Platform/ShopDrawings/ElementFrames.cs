using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// An element's own frame (docs/plan-shop-drawings.md §3, decision 579): <see cref="X"/> along its main extent (its
/// length), <see cref="Z"/> up as it is drawn in the shop, <see cref="Y"/> completing a right-handed frame
/// (<c>X × Y = Z</c>), from <see cref="Origin"/>; <see cref="Min"/> and <see cref="Max"/> its body's box in the frame. Worked
/// out from the element's attributes and body on every ask, never kept, so it follows a move, a turn or a new profile.
/// </summary>
public sealed record ElementFrame(Vec3 Origin, Vec3 X, Vec3 Y, Vec3 Z, Vec3 Min, Vec3 Max, string Source)
{
    /// <summary>A world point in the frame (x along, y across, z up).</summary>
    public Vec3 Local(Vec3 p) { var d = p - Origin; return new Vec3(Vec3.Dot(d, X), Vec3.Dot(d, Y), Vec3.Dot(d, Z)); }
    /// <summary>A point of the frame in the world.</summary>
    public Vec3 World(Vec3 l) => Origin + X * l.X + Y * l.Y + Z * l.Z;
    /// <summary>A direction of the frame in the world.</summary>
    public Vec3 Dir(Vec3 l) => X * l.X + Y * l.Y + Z * l.Z;
    public double Length => Max.X - Min.X;
    public double Width => Max.Y - Min.Y;
    public double Height => Max.Z - Min.Z;
    /// <summary>The middle of the box, in the frame.</summary>
    public Vec3 Centre => (Min + Max) * 0.5;

    public JsonObject ToJson()
    {
        static JsonArray V(Vec3 v) => new(Math.Round(v.X, 9), Math.Round(v.Y, 9), Math.Round(v.Z, 9));
        return new JsonObject
        {
            ["origin"] = V(Origin), ["x"] = V(X), ["y"] = V(Y), ["z"] = V(Z), ["min"] = V(Min), ["max"] = V(Max),
            ["length"] = Math.Round(Length, 6), ["width"] = Math.Round(Width, 6), ["height"] = Math.Round(Height, 6), ["source"] = Source,
        };
    }
}

/// <summary>
/// How a module's elements' axes run (as <see cref="IElementFaces"/> says their faces): the frame's origin, its X (along the
/// element) and its Z (up in the shop), for the classes it builds; null for an element it does not know. The box is measured
/// by the platform from the body.
/// </summary>
public interface IElementFrames
{
    (Vec3 Origin, Vec3 X, Vec3 Z)? AxesOf(IModelView m, Entity e);
}

/// <summary>
/// The element frame (docs/plan-shop-drawings.md §3): a module's axes first, then the core's (path elements along their axis,
/// standing elements by their rotation, a plate by its thickness), else one worked out from the body: its principal axes, the
/// box of least area in plan when it stands upright within 1°, X along its longest side, Z the axis nearest up.
/// </summary>
public static class ElementFrames
{
    /// <summary>How near vertical (or level) an axis is snapped to it.</summary>
    public const double SnapDegrees = 1.0;
    static readonly double SnapCos = Math.Cos(SnapDegrees * Math.PI / 180);

    /// <summary>The element's frame, its box from its body as the geometry builds it; null when it has no body.</summary>
    public static ElementFrame? Of(IQueryContext q, Entity e) =>
        Of(q.Model, q.Services, e, q.Geometry(e.Id)?.Parts.SelectMany(p => p.Solid.Vertices).ToList() ?? []);

    /// <summary>The element's frame, its box round <paramref name="body"/> (the element's body's points); null without points.</summary>
    public static ElementFrame? Of(IModelView m, IServices? services, Entity e, IReadOnlyList<Vec3> body)
    {
        if (body.Count == 0) return null;
        (Vec3 Origin, Vec3 X, Vec3 Z)? axes = null;
        var source = "body";
        if (services != null)
            foreach (var s in services.All<IElementFrames>())
                if (s.AxesOf(m, e) is { } a) { axes = a; source = "module"; break; }
        if (axes == null && CoreAxes(m, services, e) is { } core) (axes, source) = (core.Axes, core.Source);
        if (axes is { } ax && Orthonormal(ax.X, ax.Z) is { } f)
            return Boxed(ax.Origin, f.X, f.Y, f.Z, body, source, originAtMin: false);
        var plate = e.Class == "IfcPlate";
        var (x, y, z) = FromBody(body, plate);
        return Boxed(Vec3.Zero, x, y, z, body, plate ? "plate" : "body", originAtMin: true);
    }

    /// <summary>A frame from X and an approximate Z: Z made square to X, Y completing it; null when they are parallel.</summary>
    static (Vec3 X, Vec3 Y, Vec3 Z)? Orthonormal(Vec3 x, Vec3 z)
    {
        if (x.Length < 1e-12) return null;
        x = Clean(x.Normalized());
        z = z - x * Vec3.Dot(z, x);
        if (z.Length < 1e-9) return null;
        z = Clean(z.Normalized());
        var y = Clean(Vec3.Cross(z, x).Normalized());
        return (x, y, z);
    }

    /// <summary>A direction without the trigonometry's rounding (a cosine of 90° is 6e-17): an axis along the world's exact.</summary>
    static Vec3 Clean(Vec3 v)
    {
        static double C(double c) => Math.Abs(c) < 1e-12 ? 0 : c;
        return new Vec3(C(v.X), C(v.Y), C(v.Z)).Normalized();
    }

    static ElementFrame Boxed(Vec3 origin, Vec3 x, Vec3 y, Vec3 z, IReadOnlyList<Vec3> body, string source, bool originAtMin)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        foreach (var p in body)
        {
            var d = p - origin;
            var (a, b, c) = (Vec3.Dot(d, x), Vec3.Dot(d, y), Vec3.Dot(d, z));
            x0 = Math.Min(x0, a); x1 = Math.Max(x1, a); y0 = Math.Min(y0, b); y1 = Math.Max(y1, b); z0 = Math.Min(z0, c); z1 = Math.Max(z1, c);
        }
        if (!originAtMin) return new ElementFrame(origin, x, y, z, new Vec3(x0, y0, z0), new Vec3(x1, y1, z1), source);
        // A frame from the body alone has its origin at the box's first corner: every point of it at positive x, y and z.
        var o = origin + x * x0 + y * y0 + z * z0;
        static double R(double v) => Math.Round(v, 9);
        return new ElementFrame(new Vec3(R(o.X), R(o.Y), R(o.Z)), x, y, z, Vec3.Zero, new Vec3(x1 - x0, y1 - y0, z1 - z0), source);
    }

    /// <summary>The core's own axes: a path element along its axis from its start, a standing one by its rotation from its base's centre.</summary>
    static ((Vec3 Origin, Vec3 X, Vec3 Z) Axes, string Source)? CoreAxes(IModelView m, IServices? services, Entity e)
    {
        var level = (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("baseOffset");
        if (services?.Get<IPathElements>()?.IsPath(e.Class) == true && e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray)
        {
            var c = PathCurve.Of(e);
            if (c.Length < 1e-9) return null;
            var t = c.TangentAt(0);
            // A curved one along its chord, so its box holds it all.
            if (c.IsArc) t = (c.End - c.Start).Length > 1e-9 ? (c.End - c.Start).Normalized() : t;
            return ((new Vec3(c.Start.X, c.Start.Y, level), new Vec3(t.X, t.Y, 0), Vec3.UnitZ), "path");
        }
        if (e.Class is IfcClass.Column or IfcClass.Footing && Args.TryPoint(e.Attrs["at"]) is { } at)
        {
            var r = e.Num("rotation") * Math.PI / 180;
            return ((new Vec3(at.X, at.Y, level), new Vec3(Math.Cos(r), Math.Sin(r), 0), Vec3.UnitZ), "standing");
        }
        return null;
    }

    /// <summary>
    /// The axes of a body alone: upright (an axis of it within 1° of vertical), Z up and X along the long side of its box of least
    /// area in plan; otherwise its principal axes, X the longest, Z the one nearest up. A plate: Z through its thickness (its
    /// least principal axis), X along the long side of its box of least area on its face.
    /// </summary>
    public static (Vec3 X, Vec3 Y, Vec3 Z) FromBody(IReadOnlyList<Vec3> body, bool plate = false)
    {
        var pts = Distinct(body);
        var axes = Principal(pts);
        Vec3 z;
        if (plate) z = axes[2];
        else
        {
            var up = axes.OrderByDescending(a => Math.Abs(a.Z)).First();
            z = Math.Abs(up.Z) >= SnapCos ? Vec3.UnitZ : up;
        }
        if (Math.Abs(z.Z) >= SnapCos) z = z.Z > 0 ? Vec3.UnitZ : -Vec3.UnitZ;
        if (z.Z < -1e-12 || (Math.Abs(z.Z) <= 1e-12 && Vec3.Dot(z, new Vec3(1, 1e-3, 0)) < 0)) z = -z;
        // On the plane square to Z: the long side of the box of least area round the points.
        var (u, v) = Math.Abs(z.Z) > 1 - 1e-12 ? (new Vec3(1, 0, 0), new Vec3(0, 1, 0)) : LevelBasis(z);
        var x = LongSide(pts.Select(p => new Vec2(Vec3.Dot(p, u), Vec3.Dot(p, v))).ToList()) is { } d ? u * d.X + v * d.Y : u;
        // One way along, whatever the order of the points: towards +X of the world (then +Y, then up).
        if (Vec3.Dot(x, new Vec3(1, 1e-3, 1e-6)) < 0) x = -x;
        var f = Orthonormal(x, z)!.Value;
        return (f.X, f.Y, f.Z);
    }

    /// <summary>A basis of the plane square to <paramref name="z"/>: its first axis level (along the plane), the second completing it.</summary>
    static (Vec3 U, Vec3 V) LevelBasis(Vec3 z)
    {
        var u = Vec3.Cross(Vec3.UnitZ, z);
        u = u.Length < 1e-9 ? new Vec3(1, 0, 0) : u.Normalized();
        return (u, Vec3.Cross(z, u).Normalized());
    }

    static List<Vec3> Distinct(IReadOnlyList<Vec3> body)
    {
        var seen = new HashSet<(long, long, long)>();
        var o = new List<Vec3>();
        foreach (var p in body)
            if (seen.Add(((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6), (long)Math.Round(p.Z * 1e6)))) o.Add(p);
        return o;
    }

    /// <summary>The direction of the long side of the box of least area round the points (by the edges of their hull); null for fewer than two.</summary>
    static Vec2? LongSide(IReadOnlyList<Vec2> pts)
    {
        if (pts.Count < 2) return null;
        var hull = Polygon2.ConvexHull(pts);
        if (hull.Length < 2) return null;
        (double Area, Vec2 Long)? best = null;
        for (var i = 0; i < hull.Length; i++)
        {
            var edge = hull[(i + 1) % hull.Length] - hull[i];
            if (edge.Length < 1e-9) continue;
            var a = edge.Normalized();
            var b = a.Perp;
            double a0 = double.MaxValue, a1 = double.MinValue, b0 = double.MaxValue, b1 = double.MinValue;
            foreach (var p in hull)
            {
                var (s, t) = (Vec2.Dot(p, a), Vec2.Dot(p, b));
                a0 = Math.Min(a0, s); a1 = Math.Max(a1, s); b0 = Math.Min(b0, t); b1 = Math.Max(b1, t);
            }
            var area = (a1 - a0) * (b1 - b0);
            var along = a1 - a0 >= b1 - b0 - 1e-9 ? a : b;
            // Ties (a square, a circle's chords) go to the side nearest the plane's first axis, so the answer does not depend on the hull's start.
            if (best == null || area < best.Value.Area - 1e-9 || (Math.Abs(area - best.Value.Area) <= 1e-9 && Math.Abs(along.X) > Math.Abs(best.Value.Long.X) + 1e-9))
                best = (area, along);
        }
        if (best == null) return null;
        var d = best.Value.Long;
        return Math.Abs(d.X) < 1e-12 ? new Vec2(0, Math.Sign(d.Y)) : Math.Abs(d.Y) < 1e-12 ? new Vec2(Math.Sign(d.X), 0) : d;
    }

    /// <summary>The principal axes of the points (unit, the largest spread first), by the eigenvectors of their covariance (Jacobi).</summary>
    public static Vec3[] Principal(IReadOnlyList<Vec3> pts)
    {
        var n = pts.Count;
        if (n == 0) return [new Vec3(1, 0, 0), new Vec3(0, 1, 0), Vec3.UnitZ];
        var c = pts.Aggregate(Vec3.Zero, (s, p) => s + p) * (1.0 / n);
        var a = new double[3, 3];
        foreach (var p in pts)
        {
            var d = p - c;
            double[] v = [d.X, d.Y, d.Z];
            for (var i = 0; i < 3; i++) for (var j = 0; j < 3; j++) a[i, j] += v[i] * v[j] / n;
        }
        var e = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        var scale = Math.Max(1e-30, Math.Abs(a[0, 0]) + Math.Abs(a[1, 1]) + Math.Abs(a[2, 2]));
        for (var sweep = 0; sweep < 50; sweep++)
        {
            var off = Math.Abs(a[0, 1]) + Math.Abs(a[0, 2]) + Math.Abs(a[1, 2]);
            if (off <= 1e-15 * scale) break;
            for (var p = 0; p < 2; p++)
                for (var q = p + 1; q < 3; q++)
                {
                    if (Math.Abs(a[p, q]) <= 1e-18 * scale) continue;
                    var theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    var t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                    if (theta == 0) t = 1;
                    var cs = 1 / Math.Sqrt(t * t + 1);
                    var sn = t * cs;
                    for (var k = 0; k < 3; k++)
                    {
                        var (akp, akq) = (a[k, p], a[k, q]);
                        a[k, p] = cs * akp - sn * akq;
                        a[k, q] = sn * akp + cs * akq;
                    }
                    for (var k = 0; k < 3; k++)
                    {
                        var (apk, aqk) = (a[p, k], a[q, k]);
                        a[p, k] = cs * apk - sn * aqk;
                        a[q, k] = sn * apk + cs * aqk;
                    }
                    for (var k = 0; k < 3; k++)
                    {
                        var (ekp, ekq) = (e[k, p], e[k, q]);
                        e[k, p] = cs * ekp - sn * ekq;
                        e[k, q] = sn * ekp + cs * ekq;
                    }
                }
        }
        return Enumerable.Range(0, 3).OrderByDescending(i => a[i, i]).ThenBy(i => i)
            .Select(i => new Vec3(e[0, i], e[1, i], e[2, i]).Normalized()).ToArray();
    }
}
