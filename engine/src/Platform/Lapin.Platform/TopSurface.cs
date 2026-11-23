using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The top of a flat-built element over an outline (docs/plan-site-grading.md, T4; the slope engine T7 will give floors, forum
/// 157 / issue #96): <b>level</b> at an elevation; a <b>plane</b> falling at a grade (a fraction: 0.02 is 2 %) towards a
/// direction in plan (degrees from +x, anticlockwise; the way water runs), the elevation holding at its origin; or by the
/// <b>heights of the outline's corners</b>, a plane when they lie in one (within a millimetre), else the outline triangulated
/// between them, said in a note. A corner without a height of its own takes the elevation. Every point of the outline as laid
/// (its arcs in chords by the one rule, <see cref="ArcOutline.Points"/>) gets its height, so a body, a terrain's breaklines and a
/// drawing read the same top. Stored on the element's attributes: <c>topMode</c>, <c>elevation</c>, <c>grade</c>,
/// <c>slopeDirection</c>, <c>slopeOrigin</c>, <c>cornerZ</c>.
/// </summary>
public static partial class TopSurface
{
    public const string Level = "level", Plane = "slope", Corners = "corners";
    public static readonly string[] Modes = [Level, Plane, Corners];
    /// <summary>How far a corner may lie off the plane through the others before the top is triangulated instead (m).</summary>
    public const double PlanarTolerance = 0.001;

    /// <summary>What a top is: its mode, its elevation (model height), its grade and direction (a plane), its origin (where the elevation holds), the corners' own heights.</summary>
    public sealed record Spec(string Mode, double Elevation, double Grade, double Direction, Vec2? Origin, IReadOnlyList<double?> CornerZ)
    {
        /// <summary>The unit vector the plane falls towards.</summary>
        public Vec2 Down => new(Math.Cos(Direction * Math.PI / 180), Math.Sin(Direction * Math.PI / 180));
    }

    /// <summary>A top worked out over an outline: each laid point's height, the plan's triangles, whether it is one plane, what it says.</summary>
    public sealed record Solved(IReadOnlyList<Vec2> Points, IReadOnlyList<double> Z, IReadOnlyList<(int A, int B, int C)> Triangles, bool Planar,
        (double A, double B, double C)? PlaneOf, IReadOnlyList<int> CornerIndex, string? Note)
    {
        /// <summary>The laid outline in space.</summary>
        public List<Vec3> Ring => [.. Points.Select((p, i) => p.At(Z[i]))];

        /// <summary>The top's height at a point in plan (its plane, else the triangle it lies in, else the nearest point's); the plane extends past the outline.</summary>
        public double At(Vec2 p)
        {
            if (PlaneOf is var (a, b, c)) return a + b * p.X + c * p.Y;
            foreach (var (i, j, k) in Triangles)
            {
                var (pa, pb, pc) = (Points[i], Points[j], Points[k]);
                var den = (pb.Y - pc.Y) * (pa.X - pc.X) + (pc.X - pb.X) * (pa.Y - pc.Y);
                if (Math.Abs(den) < 1e-15) continue;
                var u = ((pb.Y - pc.Y) * (p.X - pc.X) + (pc.X - pb.X) * (p.Y - pc.Y)) / den;
                var v = ((pc.Y - pa.Y) * (p.X - pc.X) + (pa.X - pc.X) * (p.Y - pc.Y)) / den;
                if (u >= -1e-9 && v >= -1e-9 && u + v <= 1 + 1e-9) return u * Z[i] + v * Z[j] + (1 - u - v) * Z[k];
            }
            var best = 0;
            for (var i = 1; i < Points.Count; i++) if ((Points[i] - p).Length < (Points[best] - p).Length) best = i;
            return Z[best];
        }

        public double Min => Z.Min();
        public double Max => Z.Max();
    }

    /// <summary>The top an element's attributes say; <paramref name="corners"/>: how many corners its outline has (for <c>cornerZ</c>).</summary>
    public static Spec Read(JsonObject a, int corners)
    {
        var mode = (string?)a["topMode"] is { } m && Modes.Contains(m) ? m : Level;
        var origin = a["slopeOrigin"] is JsonArray { Count: 2 } o && Json.Number(o[0]) is { } ox && Json.Number(o[1]) is { } oy ? new Vec2(ox, oy) : (Vec2?)null;
        var cz = new List<double?>();
        var arr = a["cornerZ"] as JsonArray;
        for (var i = 0; i < corners; i++) cz.Add(arr != null && i < arr.Count ? Json.Number(arr[i]) : null);
        return new Spec(mode, Json.Number(a["elevation"]) ?? 0, Json.Number(a["grade"]) ?? 0, Json.Number(a["slopeDirection"]) ?? 0, origin, cz);
    }

    /// <summary>A top written onto attributes (only what its mode needs is kept besides the elevation; the corners' heights stay for a return to them).</summary>
    public static void Write(JsonObject a, Spec s)
    {
        a["topMode"] = s.Mode;
        a["elevation"] = R(s.Elevation);
        if (Math.Abs(s.Grade) > 0 || s.Mode == Plane) a["grade"] = Math.Round(s.Grade, 9); else a.Remove("grade");
        if (s.Mode == Plane || Math.Abs(s.Direction) > 0) a["slopeDirection"] = Math.Round(s.Direction, 9); else a.Remove("slopeDirection");
        if (s.Origin is { } o) a["slopeOrigin"] = new JsonArray(R(o.X), R(o.Y)); else a.Remove("slopeOrigin");
        if (s.CornerZ.Any(z => z != null)) a["cornerZ"] = new JsonArray([.. s.CornerZ.Select(z => z is { } v ? (JsonNode)R(v) : null)]);
        else a.Remove("cornerZ");
    }

    static double R(double v) => Math.Round(v, 9);

    /// <summary>
    /// A grade as typed: a number (a fraction, 0.02), "2 %" or "2%", or "1:50" (one in fifty); null when it does not read.
    /// </summary>
    public static double? Grade(JsonNode? node)
    {
        if (Json.Number(node) is { } d) return double.IsFinite(d) ? d : null;
        if (node is not JsonValue v || !v.TryGetValue<string>(out var s)) return null;
        s = s.Trim().Replace(',', '.');
        static double? Num(string t) => double.TryParse(t.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.IsFinite(x) ? x : null;
        if (s.EndsWith('%')) return Num(s[..^1]) is { } p ? p / 100 : null;
        if (s.IndexOf(':') is var k and > 0) return Num(s[..k]) is { } a && Num(s[(k + 1)..]) is { } b && b != 0 ? a / b : null;
        return Num(s);
    }

    /// <summary>
    /// The top over an outline (corners counter-clockwise, edges' rises): every laid point's height, the outline's triangles in
    /// plan, and whether it is one plane. A plane's origin defaults to the outline's centroid.
    /// </summary>
    public static Solved Solve(Spec s, IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises)
    {
        var edgeOf = new List<int>();
        var pts = ArcOutline.Points(corners, rises, edgeOf);
        // Where each corner is among the laid points (a corner is the first point of its edge).
        var cornerAt = new int[corners.Count];
        for (var i = pts.Count - 1; i >= 0; i--) cornerAt[edgeOf[i]] = i;
        var flat = pts.Select(p => p.At(0)).ToList();
        var tris = Triangulator.Face(flat, [.. Enumerable.Range(0, pts.Count)], null, Vec3.UnitZ);
        (double, double, double)? plane = null;
        string? note = null;
        double[] z;
        switch (s.Mode)
        {
            case Plane:
            {
                var o = s.Origin ?? Polygon2.Centroid(pts);
                var d = s.Down;
                // z = e − g·((p − o)·d)
                plane = (s.Elevation + s.Grade * (o.X * d.X + o.Y * d.Y), -s.Grade * d.X, -s.Grade * d.Y);
                break;
            }
            case Corners:
            {
                var cz = corners.Select((_, i) => i < s.CornerZ.Count && s.CornerZ[i] is { } v ? v : s.Elevation).ToArray();
                if (Fit(corners, cz) is var (a, b, c, off) && off <= PlanarTolerance) { plane = (a, b, c); break; }
                // Not one plane: the corners hold their heights, the points of an arc between two corners go by length along it.
                z = new double[pts.Count];
                for (var i = 0; i < pts.Count; i++)
                {
                    var k = edgeOf[i];
                    var (start, end) = (cornerAt[k], cornerAt[(k + 1) % corners.Count]);
                    if (i == start) { z[i] = cz[k]; continue; }
                    var along = 0.0;
                    var total = 0.0;
                    var stop = end == 0 ? pts.Count : end;
                    for (var q = start; q < stop; q++) { var len = (pts[(q + 1) % pts.Count] - pts[q]).Length; total += len; if (q < i) along += len; }
                    z[i] = cz[k] + (cz[(k + 1) % corners.Count] - cz[k]) * (total > 0 ? along / total : 0);
                }
                var offBy = Fit(corners, cz)?.Off ?? 0;
                note = $"The corners' heights are not in one plane (one is {offBy * 1000:0} mm off the best plane): the top is triangulated between them.";
                return new Solved(pts, z, tris, false, null, cornerAt, note);
            }
            default:
                plane = (s.Elevation, 0, 0);
                break;
        }
        var (pa, pb, pc) = plane!.Value;
        z = [.. pts.Select(p => pa + pb * p.X + pc * p.Y)];
        return new Solved(pts, z, tris, true, plane, cornerAt, note);
    }

    /// <summary>The plane z = a + b·x + c·y nearest the points by least squares, and how far the farthest is off it; null for fewer than three or a degenerate spread.</summary>
    public static (double A, double B, double C, double Off)? Fit(IReadOnlyList<Vec2> p, IReadOnlyList<double> z)
    {
        var n = p.Count;
        if (n < 3) return null;
        // Centred, for conditioning.
        double mx = p.Average(q => q.X), my = p.Average(q => q.Y), mz = z.Average();
        double sxx = 0, sxy = 0, syy = 0, sxz = 0, syz = 0;
        for (var i = 0; i < n; i++)
        {
            double x = p[i].X - mx, y = p[i].Y - my, w = z[i] - mz;
            sxx += x * x; sxy += x * y; syy += y * y; sxz += x * w; syz += y * w;
        }
        var det = sxx * syy - sxy * sxy;
        if (Math.Abs(det) < 1e-12) return null;
        var b = (sxz * syy - syz * sxy) / det;
        var c = (syz * sxx - sxz * sxy) / det;
        var a = mz - b * mx - c * my;
        var off = 0.0;
        for (var i = 0; i < n; i++) off = Math.Max(off, Math.Abs(a + b * p[i].X + c * p[i].Y - z[i]));
        return (a, b, c, off);
    }

    /// <summary>
    /// A plane from a slope arrow drawn from its high end to its low end: the direction it points, the grade given (else the
    /// fall between the two heights given over its length), the elevation at its start.
    /// </summary>
    public static Spec Arrow(Spec was, Vec2 from, Vec2 to, double? grade, double? fromZ, double? toZ)
    {
        var d = to - from;
        if (d.Length < 1e-6) throw new CommandException("A slope arrow needs two different points.");
        var dir = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
        var g = grade ?? (fromZ is { } a && toZ is { } b ? (a - b) / d.Length : was.Grade);
        return was with { Mode = Plane, Direction = dir, Grade = g, Origin = from, Elevation = fromZ ?? was.Elevation };
    }
}
