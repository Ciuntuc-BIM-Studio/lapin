using System.Text.Json.Nodes;
using Lapin.Drawing.Documents;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.PointClouds;

namespace Lapin.Platform;

/// <summary>
/// A point cloud in plans, sections and elevations (decision 396), as Revit shows one: the points the view's cut takes,
/// drawn as dots. A plan draws the slice round its cut plane (the view's row for the cloud says how thick, 10 cm by
/// default), a section the slice just behind its plane, an elevation every point it faces, the nearest in front. The
/// dots are a picture laid under the drawing (a raster at a quarter of a paper millimetre, so sheets and the PDF print it
/// as the screen shows it; each point a dot of two pixels, its colour darkened so a pale scan reads on paper), with the points as snaps to trace over (decimated to one a few pixels). The octree is read
/// from the root down, a node's children only while its sample is coarser than a pixel, so a plan of a whole scan reads
/// little of it.
/// </summary>
public static class PointCloudSlices
{
    /// <summary>A pixel of the picture on paper (mm).</summary>
    public const double PaperPixel = 0.2;
    const int MaxSide = 4096;
    const int MaxPoints = 6_000_000;
    const int MaxSnaps = 20_000;

    sealed record Drawn(string Id, byte[] Png, Vec2[] Corners, Vec2[] Snaps, int Points);
    static readonly Dictionary<string, Drawn> Cache = [];
    static readonly LinkedList<string> Order = new();
    static readonly Lock Gate = new();

    /// <summary>The marks of every cloud the view shows, under its drawing; none in a developed section's pieces.</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c, ViewSpec spec, VisibilityGraphics vg) =>
        PointCloudLinks.All(c.Model).Any() ? MarksOf(c, spec, vg) : [];

    /// <summary>Apart from <see cref="Marks"/>, which every drawing calls: nothing of Lapin.PointClouds is touched in a project without clouds (the browser downloads it when first needed).</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static List<ViewMark> MarksOf(ViewContext c, ViewSpec spec, VisibilityGraphics vg)
    {
        var o = new List<ViewMark>();
        foreach (var link in PointCloudLinks.All(c.Model))
        {
            var look = PointCloudLinks.LookIn(vg.Graphics, link);
            if (!look.Shown || PointCloudLinks.StoreOf(link) is not { } store) continue;
            Drawn? d;
            try { d = Draw(c, spec, link, store, look); }
            catch (Exception e) when (e is IOException or PointCloudException) { continue; }
            if (d == null) continue;
            // Owned by its link, so its snaps reach the interface (a mark with no owner sends none); it has no lines to be picked by.
            var mk = new ViewMark(link.Id) { Under = true };
            mk.Images.Add(new PenImage(d.Id, d.Corners, 1, d.Png, "image/png"));
            mk.Snaps.AddRange(d.Snaps);
            o.Add(mk);
        }
        return o;
    }

    /// <summary>The cloud's picture in the view, from the cache when nothing it is made of changed.</summary>
    static Drawn? Draw(ViewContext c, ViewSpec spec, Entity link, PointStore store, PointCloudLinks.Look look)
    {
        var t = PointCloudLinks.TransformOf(c.Model, link);
        var f = spec.Frame;
        var px = PaperPixel * c.Scale / 1000;
        var key = string.Join('|', link.Str("key"), string.Join(',', t.Select(v => v.ToString("R"))), spec.Kind, f.Origin, f.Right, f.Up, f.Dir, spec.Span, spec.Rise,
            px.ToString("R"), look.ColorMode, look.Halftone, look.Slice, string.Join(',', look.HiddenClasses.Order()));
        lock (Gate)
            if (Cache.TryGetValue(key, out var hit)) { Order.Remove(key); Order.AddFirst(key); return hit; }
        var d = Render(c, spec, store, t, look, px);
        if (d == null) return null;
        // Its own id: one sheet may hold two views of the same cloud, each its own picture.
        d = d with { Id = $"pointcloud:{link.Id}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)))[..12]}" };
        lock (Gate)
        {
            Cache[key] = d;
            Order.AddFirst(key);
            while (Order.Count > 24) { Cache.Remove(Order.Last!.Value); Order.RemoveLast(); }
        }
        return d;
    }

    /// <summary>Whether a point at depth <paramref name="depth"/> (along the view) is in what the view draws of a cloud.</summary>
    static (double Min, double Max) Range(ViewSpec spec, double slice) => spec.Kind switch
    {
        // A plan's cut plane is its frame's origin: the slice round it. A reflected ceiling plan looks up: the same slice.
        "plan" => (-slice / 2, slice / 2),
        // A section: just behind its plane.
        "section" => (0, slice),
        // An elevation: everything it faces.
        _ => (0, double.PositiveInfinity),
    };

    static Drawn? Render(ViewContext c, ViewSpec spec, PointStore store, double[] t, PointCloudLinks.Look look, double px)
    {
        var f = spec.Frame;
        var (dmin, dmax) = Range(spec, look.Slice);
        var scale = Math.Sqrt(t[0] * t[0] + t[1] * t[1] + t[2] * t[2]);
        // The nodes the slice reaches, from the root, a node's children while its sample is coarser than a pixel.
        // The picture's pixel can be no finer than the cloud's breadth in the view over MaxSide (an airborne scan 450 m
        // wide at 1:100 is 11 cm a pixel, not 2 cm): the octree is read down to that, not to the paper's pixel (decision 399).
        var sb = store.Bounds;
        double bl = double.MaxValue, bh = double.MinValue, bu = double.MaxValue, buh = double.MinValue;
        for (var k = 0; k < 8; k++)
        {
            var (wx, wy, wz) = PointCloudLinks.Apply(t, (k & 1) != 0 ? sb[3] : sb[0], (k & 2) != 0 ? sb[4] : sb[1], (k & 4) != 0 ? sb[5] : sb[2]);
            var at = f.Map(new Vec3(wx, wy, wz));
            (bl, bh) = (Math.Min(bl, at.X), Math.Max(bh, at.X));
            (bu, buh) = (Math.Min(bu, at.Y), Math.Max(buh, at.Y));
        }
        if (spec.Span is { } sw) (bl, bh) = (Math.Max(bl, sw.Min), Math.Min(bh, sw.Max));
        if (spec.Rise is { } rw) (bu, buh) = (Math.Max(bu, rw.Min), Math.Min(buh, rw.Max));
        var reach = Math.Max(px, Math.Max(bh - bl, buh - bu) / (MaxSide - 2));
        var children = new Dictionary<string, List<PointStore.Node>>();
        foreach (var n in store.Nodes) if (n.Name.Length > 1) { var p = n.Name[..^1]; if (!children.TryGetValue(p, out var l)) children[p] = l = []; l.Add(n); }
        var take = new List<PointStore.Node>();
        var stack = new Stack<PointStore.Node>();
        if (store.Get("r") is { } root) stack.Push(root);
        long total = 0;
        bool Inside((double Min, double Max) along, (double Min, double Max)? window) => window is not { } w || along.Max >= w.Min && along.Min <= w.Max;
        while (stack.Count > 0 && total < MaxPoints)
        {
            var n = stack.Pop();
            var (x, y, z, e) = store.CubeOf(n.Name);
            double dl = double.MaxValue, dh = double.MinValue, rl = double.MaxValue, rh = double.MinValue, ul = double.MaxValue, uh = double.MinValue;
            for (var k = 0; k < 8; k++)
            {
                var (wx, wy, wz) = PointCloudLinks.Apply(t, x + ((k & 1) != 0 ? e : 0), y + ((k & 2) != 0 ? e : 0), z + ((k & 4) != 0 ? e : 0));
                var w = new Vec3(wx, wy, wz);
                var dep = f.Depth(w);
                var at = f.Map(w);
                (dl, dh) = (Math.Min(dl, dep), Math.Max(dh, dep));
                (rl, rh) = (Math.Min(rl, at.X), Math.Max(rh, at.X));
                (ul, uh) = (Math.Min(ul, at.Y), Math.Max(uh, at.Y));
            }
            if (dh < dmin || dl > dmax || !Inside((rl, rh), spec.Span) || !Inside((ul, uh), spec.Rise)) continue;
            take.Add(n);
            total += n.Count;
            if (e / PointStore.Resolution * scale > reach && children.TryGetValue(n.Name, out var kids)) foreach (var k in kids) stack.Push(k);
        }
        // The points in the slice, mapped into the view.
        var pts = new List<(Vec2 At, double Depth, byte R, byte G, byte B)>();
        double zLo = store.Bounds[2], zHi = store.Bounds[5];
        foreach (var n in take)
        {
            var p = store.Read(n.Name);
            for (var i = 0; i < p.Count; i++)
            {
                if (look.HiddenClasses.Contains(p.Class(i))) continue;
                var (wx, wy, wz) = PointCloudLinks.Apply(t, p.X(i), p.Y(i), p.Z(i));
                var w = new Vec3(wx, wy, wz);
                var dep = f.Depth(w);
                if (dep < dmin || dep > dmax) continue;
                var at = f.Map(w);
                if (spec.Span is { } sp && (at.X < sp.Min || at.X > sp.Max)) continue;
                if (spec.Rise is { } ri && (at.Y < ri.Min || at.Y > ri.Max)) continue;
                var (r, g, b) = ColorOf(look.ColorMode, store, p, i, (p.Z(i) - zLo) / Math.Max(zHi - zLo, 1e-6));
                if (look.Halftone) (r, g, b) = ((byte)(r + (255 - r) * 0.6), (byte)(g + (255 - g) * 0.6), (byte)(b + (255 - b) * 0.6));
                pts.Add((at, dep, r, g, b));
            }
        }
        if (pts.Count == 0) return null;
        double x0 = pts.Min(q => q.At.X), x1 = pts.Max(q => q.At.X), y0 = pts.Min(q => q.At.Y), y1 = pts.Max(q => q.At.Y);
        // The picture's pixel: a quarter of a paper millimetre, coarser when the cloud would need more than MaxSide pixels a side.
        var pix = Math.Max(px, Math.Max(x1 - x0, y1 - y0) / (MaxSide - 2));
        var w0 = (int)Math.Ceiling((x1 - x0) / pix) + 1;
        var h0 = (int)Math.Ceiling((y1 - y0) / pix) + 1;
        var rgba = new byte[w0 * h0 * 4];
        var depth = new float[w0 * h0];
        Array.Fill(depth, float.MaxValue);
        // Snaps: one point a cell of 4 × 4 pixels, the nearest to the cell's middle kept.
        const int Cell = 4;
        var snaps = new Dictionary<long, (Vec2 At, double D)>();
        foreach (var (at, dep, r, g, b) in pts)
        {
            var ix = (int)((at.X - x0) / pix);
            var iy = (int)((y1 - at.Y) / pix);
            // A dot of 2 × 2 pixels, the nearest point in front where dots meet.
            for (var dy = 0; dy < 2; dy++)
                for (var dx = 0; dx < 2; dx++)
                {
                    if (ix + dx >= w0 || iy + dy >= h0) continue;
                    var q = (iy + dy) * w0 + ix + dx;
                    if (dep >= depth[q]) continue;
                    depth[q] = (float)dep;
                    rgba[4 * q] = r; rgba[4 * q + 1] = g; rgba[4 * q + 2] = b; rgba[4 * q + 3] = 255;
                }
            var cell = (long)(iy / Cell) * (w0 / Cell + 1) + ix / Cell;
            var mid = new Vec2(x0 + (ix / Cell * Cell + Cell / 2.0) * pix, y1 - (iy / Cell * Cell + Cell / 2.0) * pix);
            var dd = (at - mid).Length;
            if (!snaps.TryGetValue(cell, out var s) || dd < s.D) snaps[cell] = (at, dd);
        }
        var png = DocPng.EncodeRgba(w0, h0, rgba);
        var (L, R, B, T) = (x0, x0 + w0 * pix, y1 - h0 * pix, y1);
        var snapList = snaps.Values.Select(s => s.At).ToList();
        if (snapList.Count > MaxSnaps) snapList = snapList.Where((_, i) => i % (snapList.Count / MaxSnaps + 1) == 0).ToList();
        return new Drawn("", png, [new Vec2(L, T), new Vec2(R, T), new Vec2(R, B), new Vec2(L, B)], [.. snapList], pts.Count);
    }

    /// <summary>A point's colour by the mode: its RGB (grey when it has none), its intensity, its height, its class.</summary>
    static (byte R, byte G, byte B) ColorOf(string mode, PointStore s, PointStore.NodePoints p, int i, double height)
    {
        switch (mode)
        {
            // Darkened to 60 %: a scan's pale walls would not read on white paper.
            case "rgb" when s.HasColor: return ((byte)(p.R(i) * 0.6), (byte)(p.G(i) * 0.6), (byte)(p.B(i) * 0.6));
            case "intensity" when s.HasIntensity:
            {
                var v = (p.Intensity(i) - s.IntensityMin) / Math.Max(1.0, s.IntensityMax - s.IntensityMin);
                var g = (byte)Math.Clamp(Math.Pow(Math.Clamp(v, 0, 1), 0.6) * 230, 0, 255);
                return (g, g, g);
            }
            case "classification": return ClassColor(p.Class(i));
            case "elevation" or "rgb" or "intensity": return Ramp(height);
        }
        return (90, 90, 90);
    }

    /// <summary>The ASPRS classes' colours, as the 3D view draws them.</summary>
    public static (byte R, byte G, byte B) ClassColor(int c)
    {
        string hex = c switch
        {
            0 => "9e9e9e", 1 => "bdbdbd", 2 => "a0522d", 3 => "9ccc65", 4 => "43a047", 5 => "1b5e20", 6 => "e53935", 7 => "ff00ff", 8 => "ffeb3b",
            9 => "1e88e5", 10 => "8d6e63", 11 => "616161", 12 => "ffa726", 13 => "00acc1", 14 => "fdd835", 15 => "7e57c2", 16 => "26a69a", 17 => "ab47bc", 18 => "ec407a", _ => "9e9e9e",
        };
        return (Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..], 16));
    }

    /// <summary>Heights as the 3D view's ramp: blue, cyan, green, yellow, red.</summary>
    static (byte R, byte G, byte B) Ramp(double t)
    {
        t = Math.Clamp(t, 0, 1);
        (double R, double G, double B)[] stops = [(0.17, 0.32, 0.75), (0.1, 0.7, 0.85), (0.3, 0.75, 0.3), (0.95, 0.85, 0.2), (0.85, 0.2, 0.15)];
        var k = Math.Min(3, (int)(t * 4));
        var u = t * 4 - k;
        var (a, b) = (stops[k], stops[k + 1]);
        return ((byte)((a.R + (b.R - a.R) * u) * 255), (byte)((a.G + (b.G - a.G) * u) * 255), (byte)((a.B + (b.B - a.B) * u) * 255));
    }
}
