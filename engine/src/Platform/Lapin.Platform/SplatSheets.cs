using System.Diagnostics;
using Lapin.Drawing.Documents;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.PointClouds;

namespace Lapin.Platform;

/// <summary>
/// Gaussian splats in a 3D or camera view's drawing (docs/plan-gaussian-splats.md, G4): what a sheet, its PDF and the drawing
/// pane show of a splat link the view draws as splats (its Visibility / Graphics' Point clouds row, else the link's Display).
/// A picture made by the engine (<see cref="SplatRaster"/>, the 3D view's projection, sort and blending) at 300 dpi on the
/// paper at the view's scale, laid under the view's lines; the model's faces hide the splats behind them. Each Gaussian is
/// placed by its link (its transform, the crop box), kept inside the view's section box and in front of a perspective's
/// near plane, coloured Full (its spherical harmonics along the eye's ray, <see cref="Splat.Colour"/>) or Base by the view,
/// halftone by the view or the link. The largest and most opaque come first when a file holds more than the budget.
/// The picture is kept per link, view, camera and model faces, so a sheet redrawn after an unrelated change takes it again.
/// </summary>
public static class SplatSheets
{
    /// <summary>A pixel of the picture on paper (mm): 300 dpi.</summary>
    public const double PaperPixel = 25.4 / 300;
    /// <summary>At most this many pixels a picture (an A3 sheet's whole printable area at 300 dpi), and this many a side; coarser beyond.</summary>
    public const long MaxPixels = 24_000_000;
    public const int MaxSide = 8192;
    /// <summary>At most this many Gaussians a cloud (the 3D view's Precise budget).</summary>
    public const long Budget = 4_000_000;

    sealed record Drawn(string Id, byte[] Png, Vec2[] Corners, int Splats);
    static readonly Dictionary<string, Drawn> Cache = [];
    static readonly LinkedList<string> Order = new();
    static readonly Lock Gate = new();

    /// <summary>What the last picture made took: Gaussians drawn, its size and the time (ms) to gather, splat and encode it.</summary>
    public static (int Splats, int W, int H, double GatherMs, double SplatMs, double EncodeMs) Last { get; private set; }

    /// <summary>The pictures of the splat links a 3D view draws, as marks under its drawing; <paramref name="parts"/> are its parts as drawn (a perspective's mapped).</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c, ViewSpec spec, VisibilityGraphics vg, IReadOnlyList<DrawPart> parts) =>
        spec.Kind == "3d" && PointCloudLinks.All(c.Model).Any(PointCloudLinks.IsSplats) ? MarksOf(c, spec, vg, parts) : [];

    /// <summary>Apart from <see cref="Marks"/>: nothing of Lapin.PointClouds is touched in a project without splats (the browser downloads it when first needed).</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static List<ViewMark> MarksOf(ViewContext c, ViewSpec spec, VisibilityGraphics vg, IReadOnlyList<DrawPart> parts)
    {
        var o = new List<ViewMark>();
        var projection = ProjectionOf(c.View, spec);
        List<(Vec3, Vec3, Vec3)>? faces = null;
        string? facesKey = null;
        foreach (var link in PointCloudLinks.All(c.Model))
        {
            var look = PointCloudLinks.LookIn(vg.Graphics, link);
            if (!look.Shown || look.Display != "splats" || PointCloudLinks.SplatsOf(link) is not { } store) continue;
            facesKey ??= PartsKey(parts);
            Drawn? d;
            try { d = Draw(c, spec, link, store, look, projection, facesKey, () => faces ??= Faces(parts, spec.Frame)); }
            catch (Exception e) when (e is IOException or PointCloudException) { continue; }
            if (d == null) continue;
            var mk = new ViewMark(link.Id) { Under = true };
            mk.Images.Add(new PenImage(d.Id, d.Corners, 1, d.Png, "image/png"));
            o.Add(mk);
        }
        return o;
    }

    /// <summary>
    /// How a 3D view's drawing sees (View3dDrawings.Prepare): a perspective's drawing has a window (its field of view) and
    /// the eye's map; an orthographic one (or an element's iso) its frame.
    /// </summary>
    public static SplatRaster.Projection ProjectionOf(Entity view, ViewSpec spec)
    {
        if (spec.Span != null && spec.Rise != null)
        {
            var cam = View3dDrawings.CameraOf(view, null);
            return new SplatRaster.Projection(cam.Eye, cam.Right, cam.Up, cam.Look, false, cam.Distance, Math.Min(View3dDrawings.Near, cam.Distance / 2));
        }
        var f = spec.Frame;
        return new SplatRaster.Projection(f.Origin, f.Right, f.Up, f.Dir, true);
    }

    /// <summary>The cloud's picture in the view, from the cache when nothing it is made of changed.</summary>
    static Drawn? Draw(ViewContext c, ViewSpec spec, Entity link, SplatStore store, PointCloudLinks.Look look, SplatRaster.Projection p, string facesKey, Func<List<(Vec3, Vec3, Vec3)>> faces)
    {
        var t = PointCloudLinks.TransformOf(c.Model, link);
        var box = View3dDrawings.SectionBoxOf(c.View);
        var degree = look.SplatColor == "base" ? 0 : store.ShDegree;
        var key = string.Join('|', link.Str("key"), string.Join(',', t.Select(v => v.ToString("R"))), p, spec.Span, spec.Rise, c.Scale.ToString("R"), look.Halftone, degree,
            look.Crop?.ToJson().ToJsonString() ?? "", box, facesKey);
        lock (Gate)
            if (Cache.TryGetValue(key, out var hit)) { Order.Remove(key); Order.AddFirst(key); return hit; }
        var d = Render(spec, store, t, look, p, degree, box, c.Scale, faces);
        if (d == null) return null;
        // Its own id: one sheet may hold two views of the same capture, each its own picture.
        d = d with { Id = $"pointcloud:{link.Id}:splats:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)))[..12]}" };
        lock (Gate)
        {
            Cache[key] = d;
            Order.AddFirst(key);
            // A picture can be several megabytes: a few kept.
            while (Order.Count > 8) { Cache.Remove(Order.Last!.Value); Order.RemoveLast(); }
        }
        return d;
    }

    /// <summary>The Gaussians the view takes of a cloud, in the project, coloured as seen from its eye.</summary>
    public static List<SplatRaster.Gaussian> Gather(SplatStore store, double[] t, SplatRaster.Projection p, int degree, CropBox? cloudCrop, View3dDrawings.Box? box, long budget = Budget)
    {
        var o = new List<SplatRaster.Gaussian>((int)Math.Min(store.Count, budget));
        double[] m = t[..9];
        // The project's directions into the file's, for the harmonics (only the direction counts).
        var inv = degree > 0 ? PointCloudLinks.Invert(t) : null;
        var per = Splat.ShCoeffs(store.ShDegree) * 3;
        var take = Splat.ShCoeffs(Math.Min(degree, store.ShDegree)) * 3;
        for (long start = 0; start < store.Count; start += PointCloudLinks.SplatPageRecords)
        {
            var (idx, bytes) = store.Read(start, PointCloudLinks.SplatPageRecords, budget);
            var sh = take > 0 ? store.ReadSh(start, (int)Math.Min(PointCloudLinks.SplatPageRecords, store.Count - start)) : [];
            foreach (var i in idx)
            {
                var g = SplatStore.Unpack(bytes.AsSpan((int)(i - start) * SplatStore.BytesPerSplat, SplatStore.BytesPerSplat));
                var (x, y, z) = PointCloudLinks.Apply(t, g.X, g.Y, g.Z);
                if (cloudCrop != null && !cloudCrop.Inside(x, y, z)) continue;
                var at = new Vec3(x, y, z);
                if (box != null)
                {
                    var b = box.ToBox(at);
                    if (b.X < box.Min.X || b.X > box.Max.X || b.Y < box.Min.Y || b.Y > box.Max.Y || b.Z < box.Min.Z || b.Z > box.Max.Z) continue;
                }
                var cov = SplatRaster.Covariance(g.S0, g.S1, g.S2, g.Qw, g.Qx, g.Qy, g.Qz, m);
                (double R, double G, double B) colour = (g.R / 255.0, g.G / 255.0, g.B / 255.0);
                if (take > 0 && sh.Length > 0 && inv != null)
                {
                    // Along the eye's ray (orthographic: the look), in the file's frame.
                    var dir = p.Ortho ? p.Look : at - p.Origin;
                    var fd = (inv[0] * dir.X + inv[3] * dir.Y + inv[6] * dir.Z, inv[1] * dir.X + inv[4] * dir.Y + inv[7] * dir.Z, inv[2] * dir.X + inv[5] * dir.Y + inv[8] * dir.Z);
                    colour = Splat.Colour(colour, sh.AsSpan((int)(i - start) * per, take), degree, fd);
                }
                o.Add(new SplatRaster.Gaussian(at, cov.Xx, cov.Xy, cov.Xz, cov.Yy, cov.Yz, cov.Zz, (float)Math.Min(1, colour.R), (float)Math.Min(1, colour.G), (float)Math.Min(1, colour.B), g.A / 255f));
            }
        }
        return o;
    }

    /// <summary>
    /// The picture's pixels: round what the Gaussians reach (3σ), inside a perspective's window (and <paramref name="clip"/>), a pixel
    /// a 300th of an inch on the paper, coarser when that would pass <see cref="MaxPixels"/> or <see cref="MaxSide"/>.
    /// </summary>
    public static SplatRaster.Grid? GridOf(IReadOnlyList<SplatRaster.Gaussian> splats, SplatRaster.Projection p, ViewSpec spec, (Vec2 Min, Vec2 Max)? clip, double scale)
    {
        double l = double.MaxValue, r = double.MinValue, b = double.MaxValue, tp = double.MinValue;
        foreach (var g in splats)
        {
            if (p.Map(g.Centre) is not { } at) continue;
            var (a, _, d) = SplatRaster.Projected(g, p, 1);
            var (rx, ry) = (3 * Math.Sqrt(Math.Max(0, a - SplatRaster.LowPass)), 3 * Math.Sqrt(Math.Max(0, d - SplatRaster.LowPass)));
            (l, r) = (Math.Min(l, at.U - rx), Math.Max(r, at.U + rx));
            (b, tp) = (Math.Min(b, at.V - ry), Math.Max(tp, at.V + ry));
        }
        if (spec.Span is { } sp) (l, r) = (Math.Max(l, sp.Min), Math.Min(r, sp.Max));
        if (spec.Rise is { } rs) (b, tp) = (Math.Max(b, rs.Min), Math.Min(tp, rs.Max));
        if (clip is { } c) (l, r, b, tp) = (Math.Max(l, c.Min.X), Math.Min(r, c.Max.X), Math.Max(b, c.Min.Y), Math.Min(tp, c.Max.Y));
        if (!(r > l) || !(tp > b)) return null;
        var pixel = PaperPixel * scale / 1000;
        pixel = Math.Max(pixel, Math.Max(r - l, tp - b) / MaxSide);
        pixel = Math.Max(pixel, Math.Sqrt((r - l) * (tp - b) / MaxPixels));
        var w = Math.Max(1, (int)Math.Ceiling((r - l) / pixel));
        var h = Math.Max(1, (int)Math.Ceiling((tp - b) / pixel));
        return new SplatRaster.Grid(l, tp, pixel, w, h);
    }

    static Drawn? Render(ViewSpec spec, SplatStore store, double[] t, PointCloudLinks.Look look, SplatRaster.Projection p, int degree, View3dDrawings.Box? box, double scale, Func<List<(Vec3, Vec3, Vec3)>> faces)
    {
        var clock = Stopwatch.StartNew();
        var splats = Gather(store, t, p, degree, look.Crop, box);
        if (splats.Count == 0) return null;
        if (GridOf(splats, p, spec, null, scale) is not { } grid) return null;
        var gathered = clock.Elapsed.TotalMilliseconds;
        var pic = SplatRaster.Render(splats, p, grid, faces(), look.Halftone);
        var splatted = clock.Elapsed.TotalMilliseconds;
        if (pic.Drawn == 0) return null;
        var png = DocPng.EncodeRgba(pic.W, pic.H, pic.Rgba);
        Last = (pic.Drawn, pic.W, pic.H, gathered, splatted - gathered, clock.Elapsed.TotalMilliseconds - splatted);
        return new Drawn("", png, [new Vec2(grid.X0, grid.Y1), new Vec2(grid.X1, grid.Y1), new Vec2(grid.X1, grid.Y0), new Vec2(grid.X0, grid.Y0)], pic.Drawn);
    }

    /// <summary>The parts' faces as triangles in the drawing (x, y as drawn, z the depth), the occluders of the splats.</summary>
    public static List<(Vec3, Vec3, Vec3)> Faces(IReadOnlyList<DrawPart> parts, ViewFrame f)
    {
        var o = new List<(Vec3, Vec3, Vec3)>();
        foreach (var part in parts)
        {
            var vs = part.Solid.Vertices;
            var mapped = new Vec3[vs.Length];
            for (var i = 0; i < vs.Length; i++) { var at = f.Map(vs[i]); mapped[i] = new Vec3(at.X, at.Y, f.Depth(vs[i])); }
            foreach (var face in part.Solid.Faces)
            {
                if (face.Outer.Length < 3) continue;
                // Its plane's normal by Newell's rule (a perspective's mapped faces keep their plane, not their stored normal).
                var n = Vec3.Zero;
                for (var i = 0; i < face.Outer.Length; i++)
                {
                    var (a, b) = (vs[face.Outer[i]], vs[face.Outer[(i + 1) % face.Outer.Length]]);
                    n += new Vec3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
                }
                if (n.Length < 1e-12) continue;
                if (face.Outer.Length == 3 && face.Holes.Length == 0) { o.Add((mapped[face.Outer[0]], mapped[face.Outer[1]], mapped[face.Outer[2]])); continue; }
                foreach (var (a, b, c) in Triangulator.Face(vs, face.Outer, face.Holes, n.Normalized())) o.Add((mapped[a], mapped[b], mapped[c]));
            }
        }
        return o;
    }

    /// <summary>What the parts are, for the cache: their vertices hashed.</summary>
    static string PartsKey(IReadOnlyList<DrawPart> parts)
    {
        var h = new HashCode();
        long vertices = 0;
        h.Add(parts.Count);
        foreach (var p in parts)
        {
            h.Add(p.Solid.Faces.Length);
            vertices += p.Solid.Vertices.Length;
            foreach (var v in p.Solid.Vertices) h.Add(v);
        }
        return $"{vertices}:{h.ToHashCode():x8}";
    }
}
