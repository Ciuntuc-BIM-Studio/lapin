using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A saved 3D view as a drawing (forum topic 91): what a sheet, its PDF and the drawing pane show of it, made by the engine
/// with the same hidden-line removal as an elevation, from the view's camera. Orthographic: the whole model seen along the
/// camera's direction (an axonometric). Perspective: the camera's field of view from its eye, the parts mapped by the
/// perspective (a projective map keeps faces planar and the order along each ray, so the exact orthographic hidden-line
/// removal of the mapped parts is the perspective's), at true scale in the plane through the camera's target. What the 3D
/// view shows: its Visibility / Graphics and hidden elements (as every drawing), the storeys it lists, its section box.
/// A view saved without a camera is seen from the south-west, orthographic, as the 3D starts.
/// </summary>
public static class View3dDrawings
{
    /// <summary>A 3D view's camera: angles in radians, the vertical field of view in degrees, the window's width over its height.</summary>
    public sealed record Camera3d(double Azimuth, double Pitch, double Distance, Vec3 Target, bool Ortho, double FovDeg = DefaultFov, double Aspect = DefaultAspect)
    {
        /// <summary>The direction it looks along (unit).</summary>
        public Vec3 Look => new(Math.Cos(Azimuth) * Math.Cos(Pitch), Math.Sin(Azimuth) * Math.Cos(Pitch), Math.Sin(Pitch));
        /// <summary>The drawing's right: level, square to the look (as the 3D's camera, which keeps up up).</summary>
        public Vec3 Right => new(Math.Sin(Azimuth), -Math.Cos(Azimuth), 0);
        public Vec3 Up => Vec3.Cross(Right, Look).Normalized();
        public Vec3 Eye => Target - Look * Distance;
    }

    /// <summary>The 3D's vertical field of view (degrees) and the window's proportion when the camera does not say.</summary>
    public const double DefaultFov = 45, DefaultAspect = 16.0 / 9;
    /// <summary>How near the eye a perspective sees (m): what is nearer is cut away.</summary>
    public const double Near = 0.1;

    /// <summary>The view's camera, or (none saved) the 3D's first look, south-west from above, orthographic, round <paramref name="bounds"/>.</summary>
    public static Camera3d CameraOf(Entity view, (Vec3 Min, Vec3 Max)? bounds)
    {
        if (view.Attrs["camera"] is JsonObject c && c["target"] is JsonArray t && t.Count >= 3)
            return new Camera3d(Json.Number(c["azimuth"]) ?? Math.PI / 4, Json.Number(c["pitch"]) ?? -0.5, Math.Max(0.01, Json.Number(c["distance"]) ?? 30),
                new Vec3(Json.Number(t[0]) ?? 0, Json.Number(t[1]) ?? 0, Json.Number(t[2]) ?? 0),
                c["ortho"] is JsonValue ov && ov.TryGetValue<bool>(out var o) && o,
                Json.Number(c["fov"]) is { } fov && fov is > 1 and < 179 ? fov : DefaultFov,
                Json.Number(c["aspect"]) is { } asp && asp is > 0.05 and < 20 ? asp : DefaultAspect);
        var (lo, hi) = bounds ?? (new Vec3(-1, -1, 0), new Vec3(1, 1, 1));
        var radius = Math.Max(1, (hi - lo).Length / 2);
        return new Camera3d(Math.PI / 4, -Math.PI / 6, radius / Math.Sin(DefaultFov * Math.PI / 360) * 1.05, (lo + hi) * 0.5, true);
    }

    /// <summary>
    /// What the view draws and how it sees it: the parts it shows (cut by its section box; in perspective cut at the eye and
    /// mapped), and the spec (kind "3d", nothing cut, no hidden lines; a perspective's window its field of view).
    /// </summary>
    public static (ViewSpec Spec, List<DrawPart> Parts) Prepare(IModelView m, Entity view, IReadOnlyList<DrawPart> gathered, IServices? services)
    {
        var booleans = services?.Get<ISolidBooleans>();
        // An entourage figure is drawn by its body: a silhouette is a flat elevation's.
        var parts = gathered.Select(p => p.BySilhouette ? p with { BySilhouette = false } : p).Where(p => p.Solid.Vertices.Length > 0).ToList();
        // The storeys it shows: those listed ("some"); the active storey's choices are the interface's, so every storey here.
        var storeys = Views.Storeys3d.Of(view);
        if (storeys.Mode == "some" || !storeys.Unplaced)
        {
            var storeyOf = new Dictionary<string, string?>();
            parts = parts.Where(p =>
            {
                if (!storeyOf.TryGetValue(p.Element, out var s)) storeyOf[p.Element] = s = Spatial.StoreyOf(m, p.Element)?.Id;
                return s == null ? storeys.Unplaced : storeys.Mode != "some" || storeys.Storeys.Contains(s);
            }).ToList();
        }
        if (SectionBoxOf(view) is { } box) parts = Keep(parts, box.ToBox, box.Min, box.Max, box.ToWorld, booleans);
        var camera = CameraOf(view, Bounds(parts));
        var (right, up, look) = (camera.Right, camera.Up, camera.Look);
        if (camera.Ortho)
            return (new ViewSpec("3d", new ViewFrame(camera.Target, right, up, look), false, double.PositiveInfinity, Hidden: false), parts);

        // Perspective: in the eye's axes (x right, y up, d along the look), what lies at or behind the eye's near plane is cut away.
        var eye = camera.Eye;
        Vec3 ToEye(Vec3 p) { var q = p - eye; return new Vec3(Vec3.Dot(q, right), Vec3.Dot(q, up), Vec3.Dot(q, look)); }
        Vec3 FromEye(Vec3 p) => eye + right * p.X + up * p.Y + look * p.Z;
        var reach = parts.Count == 0 ? 10 : parts.SelectMany(p => p.Solid.Vertices).Max(v => (v - eye).Length) * 2 + 10;
        var near = Math.Min(Near, camera.Distance / 2);
        // What is behind the eye cannot be mapped: a part across the near plane is cut there, or left out without the boolean library.
        parts = Keep(parts, ToEye, new Vec3(-reach, -reach, near), new Vec3(reach, reach, reach), FromEye, booleans, mustCut: true);
        // x·D/d, y·D/d: the plane through the target at its true size; depth D − D²/d grows with d, so what is nearer stays in front.
        var dist = camera.Distance;
        Vec3 Map(Vec3 p) { var e = ToEye(p); return new Vec3(e.X * dist / e.Z, e.Y * dist / e.Z, dist - dist * dist / e.Z); }
        var mapped = parts.Select(p => p with { Solid = SolidBuilder.Transform(p.Solid, Map) }).ToList();
        var halfH = dist * Math.Tan(camera.FovDeg * Math.PI / 360);
        var halfW = halfH * camera.Aspect;
        var spec = new ViewSpec("3d", new ViewFrame(new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0), Vec3.UnitZ), false, double.PositiveInfinity, Hidden: false)
        {
            Span = (-halfW, halfW), Rise = (-halfH, halfH),
        };
        return (spec, mapped);
    }

    /// <summary>A saved 3D view's section box: its corners in its own axes (turned by rot about the vertical through the world's origin).</summary>
    public sealed record Box(Vec3 Min, Vec3 Max, double Rot)
    {
        public Vec3 ToBox(Vec3 p) { var (c, s) = (Math.Cos(Rot), Math.Sin(Rot)); return new Vec3(c * p.X + s * p.Y, -s * p.X + c * p.Y, p.Z); }
        public Vec3 ToWorld(Vec3 p) { var (c, s) = (Math.Cos(Rot), Math.Sin(Rot)); return new Vec3(c * p.X - s * p.Y, s * p.X + c * p.Y, p.Z); }
    }

    public static Box? SectionBoxOf(Entity view)
    {
        if (view.Attrs["sectionBox"] is not JsonObject o || o["min"] is not JsonArray a || o["max"] is not JsonArray b || a.Count < 3 || b.Count < 3) return null;
        Vec3 V(JsonArray x) => new(Json.Number(x[0]) ?? 0, Json.Number(x[1]) ?? 0, Json.Number(x[2]) ?? 0);
        return new Box(V(a), V(b), Json.Number(o["rot"]) ?? 0);
    }

    /// <summary>
    /// The parts inside a box given in axes of its own (<paramref name="toAxes"/>, back by <paramref name="toWorld"/>): one wholly
    /// inside as it is, one wholly outside left out, one across cut by the box (without the boolean library kept whole, or left out with <paramref name="mustCut"/>).
    /// </summary>
    static List<DrawPart> Keep(List<DrawPart> parts, Func<Vec3, Vec3> toAxes, Vec3 min, Vec3 max, Func<Vec3, Vec3> toWorld, ISolidBooleans? booleans, bool mustCut = false)
    {
        const double eps = 1e-9;
        Solid? cutter = null;
        var o = new List<DrawPart>(parts.Count);
        foreach (var p in parts)
        {
            double x0 = double.PositiveInfinity, y0 = x0, z0 = x0, x1 = double.NegativeInfinity, y1 = x1, z1 = x1;
            foreach (var v in p.Solid.Vertices)
            {
                var q = toAxes(v);
                (x0, y0, z0) = (Math.Min(x0, q.X), Math.Min(y0, q.Y), Math.Min(z0, q.Z));
                (x1, y1, z1) = (Math.Max(x1, q.X), Math.Max(y1, q.Y), Math.Max(z1, q.Z));
            }
            if (x1 < min.X - eps || y1 < min.Y - eps || z1 < min.Z - eps || x0 > max.X + eps || y0 > max.Y + eps || z0 > max.Z + eps) continue;
            if (x0 >= min.X - eps && y0 >= min.Y - eps && z0 >= min.Z - eps && x1 <= max.X + eps && y1 <= max.Y + eps && z1 <= max.Z + eps) { o.Add(p); continue; }
            // Across the box: only its part inside.
            if (booleans == null) { if (!mustCut) o.Add(p); continue; }
            cutter ??= Prism.Box((min + max) * 0.5, max.X - min.X, max.Y - min.Y, max.Z - min.Z) is { } b ? SolidBuilder.Transform(b, toWorld) : null;
            if (cutter == null) continue;
            if (booleans.Intersection(p.Solid, cutter) is { Vertices.Length: > 0 } inside) o.Add(p with { Solid = inside });
        }
        return o;
    }

    static (Vec3 Min, Vec3 Max)? Bounds(IReadOnlyList<DrawPart> parts)
    {
        var bs = parts.Select(p => Measure.Bounds(p.Solid)).OfType<(Vec3 Min, Vec3 Max)>().ToList();
        if (bs.Count == 0) return null;
        return (new Vec3(bs.Min(b => b.Min.X), bs.Min(b => b.Min.Y), bs.Min(b => b.Min.Z)), new Vec3(bs.Max(b => b.Max.X), bs.Max(b => b.Max.Y), bs.Max(b => b.Max.Z)));
    }
}
