using Lapin.Geometry;

namespace Lapin.Platform;

/// <summary>
/// Gaussian splats drawn on the CPU (docs/plan-gaussian-splats.md, G4): a 3D or camera view's splats as a picture for its
/// sheet and PDF, made the way the 3D view draws them (<c>ui/src/viewport/splats3d.ts</c>). Each Gaussian's 3D covariance is
/// projected to the picture (the EWA splatting of Zwicker et al., as Kerbl et al. 2023 use it: Σ' = J W Σ Wᵀ Jᵀ, a 0.3 px²
/// low-pass added), weighed out to 3σ (exp(−½ dᵀ Σ'⁻¹ d) times its opacity, nothing under 1/255), and blended premultiplied,
/// back to front (the farthest first, by the centres' depths). The model's faces, given as triangles, hide what is behind
/// them as the 3D view's depth test does: a splat is drawn where its centre is in front of the nearest face. The picture is
/// worked in bands of rows, each band on its own thread with the splats and faces that reach it, so memory stays a band's.
/// </summary>
public static class SplatRaster
{
    /// <summary>A Gaussian in the project: its centre, its covariance (xx, xy, xz, yy, yz, zz, m²), its colour (0–1, as seen) and opacity.</summary>
    public readonly record struct Gaussian(Vec3 Centre, double Xx, double Xy, double Xz, double Yy, double Yz, double Zz, float R, float G, float B, float A);

    /// <summary>
    /// How the view sees: orthographic, a point's drawing coordinates are its distances from <c>Origin</c> along <c>Right</c>
    /// and <c>Up</c> and its depth along <c>Look</c>; in perspective <c>Origin</c> is the eye, a point at (x, y, d) in the
    /// eye's axes is drawn at (x·D/d, y·D/d) and its depth is D − D²/d (View3dDrawings' map, the plane through the target at
    /// its true size), D being <c>Distance</c>.
    /// </summary>
    public sealed record Projection(Vec3 Origin, Vec3 Right, Vec3 Up, Vec3 Look, bool Ortho, double Distance = 1, double Near = 0.1)
    {
        /// <summary>A point in the eye's (or the origin's) axes.</summary>
        public Vec3 Axes(Vec3 p) { var q = p - Origin; return new Vec3(Vec3.Dot(q, Right), Vec3.Dot(q, Up), Vec3.Dot(q, Look)); }

        /// <summary>A point in the drawing (u, v) and its depth, as the view's drawing has its parts; null behind the near plane.</summary>
        public (double U, double V, double Depth)? Map(Vec3 p)
        {
            var e = Axes(p);
            if (Ortho) return (e.X, e.Y, e.Z);
            if (e.Z < Near) return null;
            return (e.X * Distance / e.Z, e.Y * Distance / e.Z, Distance - Distance * Distance / e.Z);
        }
    }

    /// <summary>The picture's pixels over the drawing: its top left corner (drawing units, y up), a pixel's side and how many a row and a column.</summary>
    public sealed record Grid(double X0, double Y1, double Pixel, int W, int H)
    {
        public double X1 => X0 + W * Pixel;
        public double Y0 => Y1 - H * Pixel;
    }

    /// <summary>The picture (RGBA, rows top first, straight alpha, see-through where nothing is drawn) and how many splats reached it.</summary>
    public sealed record Picture(int W, int H, byte[] Rgba, int Drawn);

    /// <summary>The low-pass the 3D view adds to a projected covariance (px²): a Gaussian at least about a pixel wide.</summary>
    public const double LowPass = 0.3;
    /// <summary>How far in front of a face (in the depth's units, m) a splat's centre must be to show: a capture's wall on the model's wall still shows.</summary>
    public const double DepthTolerance = 0.02;
    const int Band = 32;

    /// <summary>Σ = R · diag(s²) · Rᵀ of a unit quaternion (w, x, y, z) turned by a linear map M (column by column, 9 numbers: the images of the axes), i.e. (M R) diag(s²) (M R)ᵀ.</summary>
    public static (double Xx, double Xy, double Xz, double Yy, double Yz, double Zz) Covariance(double s0, double s1, double s2, double w, double x, double y, double z, double[]? m = null)
    {
        Span<double> r =
        [
            1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y),
            2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x),
            2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y),
        ];
        // A = M R, row by row (M's columns are given).
        Span<double> a = stackalloc double[9];
        if (m == null) r.CopyTo(a);
        else
            for (var i = 0; i < 3; i++)
                for (var j = 0; j < 3; j++)
                    a[3 * i + j] = m[i] * r[j] + m[3 + i] * r[3 + j] + m[6 + i] * r[6 + j];
        double s20 = s0 * s0, s21 = s1 * s1, s22 = s2 * s2;
        double E(Span<double> a, int i, int j) => a[3 * i] * s20 * a[3 * j] + a[3 * i + 1] * s21 * a[3 * j + 1] + a[3 * i + 2] * s22 * a[3 * j + 2];
        return (E(a, 0, 0), E(a, 0, 1), E(a, 0, 2), E(a, 1, 1), E(a, 1, 2), E(a, 2, 2));
    }

    /// <summary>
    /// A Gaussian's covariance in the picture (px², x right, y down), the low-pass added: Σ' = J W Σ Wᵀ Jᵀ, W the view's axes,
    /// J the projection's Jacobian at the centre (constant when orthographic) over the pixel's side.
    /// </summary>
    public static (double A, double B, double D) Projected(in Gaussian g, Projection p, double pixel)
    {
        var e = p.Axes(g.Centre);
        // J's rows (drawing u, v by the eye's x, y, d), then y turned down for the picture's rows.
        double j00, j02, j11, j12;
        if (p.Ortho) (j00, j02, j11, j12) = (1, 0, 1, 0);
        else (j00, j02, j11, j12) = (p.Distance / e.Z, -p.Distance * e.X / (e.Z * e.Z), p.Distance / e.Z, -p.Distance * e.Y / (e.Z * e.Z));
        var k = 1 / pixel;
        // T = J W: two rows of three, in the project's axes.
        var t0 = (p.Right * j00 + p.Look * j02) * k;
        var t1 = (p.Up * j11 + p.Look * j12) * -k;
        var s = g;
        double Q(Vec3 a, Vec3 b) =>
            a.X * (s.Xx * b.X + s.Xy * b.Y + s.Xz * b.Z) + a.Y * (s.Xy * b.X + s.Yy * b.Y + s.Yz * b.Z) + a.Z * (s.Xz * b.X + s.Yz * b.Y + s.Zz * b.Z);
        return (Q(t0, t0) + LowPass, Q(t0, t1), Q(t1, t1) + LowPass);
    }

    /// <summary>
    /// The splats drawn over <paramref name="grid"/>: <paramref name="faces"/> (triangles in the drawing, x and y as u and v,
    /// z as the depth) hide what is behind them; halftone mixes every colour 60 % towards a light grey, as the 3D view does.
    /// </summary>
    public static Picture Render(IReadOnlyList<Gaussian> splats, Projection p, Grid grid, IReadOnlyList<(Vec3 A, Vec3 B, Vec3 C)>? faces = null, bool halftone = false)
    {
        int W = grid.W, H = grid.H;
        var n = splats.Count;
        // Each splat in the picture: its centre (px), its conic (Σ'⁻¹), its rows and columns, its depths.
        var cx = new float[n]; var cy = new float[n];
        var ca = new float[n]; var cb = new float[n]; var cc = new float[n];
        var x0 = new int[n]; var x1 = new int[n]; var y0 = new int[n]; var y1 = new int[n];
        var depth = new float[n];
        var key = new float[n];
        var order = new int[n];
        var m = 0;
        for (var i = 0; i < n; i++)
        {
            var g = splats[i];
            if (!(g.A >= 1 / 255f) || p.Map(g.Centre) is not { } at) continue;
            var (a, b, d) = Projected(g, p, grid.Pixel);
            var det = a * d - b * b;
            if (!(det > 1e-12)) continue;
            var px = (at.U - grid.X0) / grid.Pixel;
            var py = (grid.Y1 - at.V) / grid.Pixel;
            // Its box to 3σ: the ellipse's reach along x is 3√a, along y 3√d.
            double rx = 3 * Math.Sqrt(a), ry = 3 * Math.Sqrt(d);
            var (l, r, t, bo) = ((int)Math.Floor(px - rx), (int)Math.Ceiling(px + rx), (int)Math.Floor(py - ry), (int)Math.Ceiling(py + ry));
            if (r < 0 || l >= W || bo < 0 || t >= H) continue;
            cx[m] = (float)px; cy[m] = (float)py;
            ca[m] = (float)(d / det); cb[m] = (float)(-b / det); cc[m] = (float)(a / det);
            x0[m] = Math.Max(0, l); x1[m] = Math.Min(W - 1, r); y0[m] = Math.Max(0, t); y1[m] = Math.Min(H - 1, bo);
            depth[m] = (float)at.Depth;
            // Back to front: the farthest first (the eye's distance in perspective, as the 3D view's sort).
            var e = p.Axes(g.Centre);
            key[m] = -(float)e.Z;
            order[m] = i;
            m++;
        }
        var idx = new int[m];
        for (var i = 0; i < m; i++) idx[i] = i;
        Array.Sort(key, idx, 0, m);
        // The splats each band of rows takes, in the order they are drawn.
        var bands = (H + Band - 1) / Band;
        var start = new int[bands + 1];
        foreach (var s in idx) for (var b = y0[s] / Band; b <= y1[s] / Band; b++) start[b + 1]++;
        for (var b = 0; b < bands; b++) start[b + 1] += start[b];
        var list = new int[start[bands]];
        var fill = (int[])start.Clone();
        foreach (var s in idx) for (var b = y0[s] / Band; b <= y1[s] / Band; b++) list[fill[b]++] = s;
        // The faces each band takes, in the picture's pixels.
        var tris = new List<(float Ax, float Ay, float Az, float Bx, float By, float Bz, float Cx, float Cy, float Cz)>();
        var triBands = new List<int>[bands];
        if (faces != null)
            foreach (var (fa, fb, fc) in faces)
            {
                float X(Vec3 v) => (float)((v.X - grid.X0) / grid.Pixel);
                float Y(Vec3 v) => (float)((grid.Y1 - v.Y) / grid.Pixel);
                var tri = (X(fa), Y(fa), (float)fa.Z, X(fb), Y(fb), (float)fb.Z, X(fc), Y(fc), (float)fc.Z);
                var lo = Math.Min(tri.Item2, Math.Min(tri.Item5, tri.Item8));
                var hi = Math.Max(tri.Item2, Math.Max(tri.Item5, tri.Item8));
                var lx = Math.Min(tri.Item1, Math.Min(tri.Item4, tri.Item7));
                var hx = Math.Max(tri.Item1, Math.Max(tri.Item4, tri.Item7));
                if (hi < 0 || lo >= H || hx < 0 || lx >= W) continue;
                var k = tris.Count;
                tris.Add(tri);
                for (var b = Math.Max(0, (int)lo / Band); b <= Math.Min(bands - 1, (int)hi / Band); b++) (triBands[b] ??= []).Add(k);
            }
        var rgba = new byte[W * H * 4];
        var hit = new bool[m];
        var color = new float[3 * n];
        var alpha = new float[n];
        var grey = 0.62f;
        for (var s = 0; s < m; s++)
        {
            var g = splats[order[s]];
            var (r, gg, bb) = (Math.Clamp(g.R, 0, 1), Math.Clamp(g.G, 0, 1), Math.Clamp(g.B, 0, 1));
            if (halftone) (r, gg, bb) = (r + (grey - r) * 0.6f, gg + (grey - gg) * 0.6f, bb + (grey - bb) * 0.6f);
            color[3 * s] = r; color[3 * s + 1] = gg; color[3 * s + 2] = bb;
            alpha[s] = Math.Min(1, g.A);
        }
        void One(int band)
        {
            var top = band * Band;
            var rows = Math.Min(Band, H - top);
            var zb = new float[W * rows];
            Array.Fill(zb, float.PositiveInfinity);
            if (triBands[band] is { } own) foreach (var k in own) Face(tris[k], zb, W, top, rows);
            var acc = new float[W * rows * 4];
            for (var q = start[band]; q < start[band + 1]; q++)
            {
                var s = list[q];
                int ya = Math.Max(y0[s], top), yb = Math.Min(y1[s], top + rows - 1);
                var (A, B, C) = (ca[s], cb[s], cc[s]);
                var (r, gg, bb, al, dep) = (color[3 * s], color[3 * s + 1], color[3 * s + 2], alpha[s], depth[s] - (float)DepthTolerance);
                var any = false;
                for (var y = ya; y <= yb; y++)
                {
                    var dy = y + 0.5f - cy[s];
                    var row = (y - top) * W;
                    for (var x = x0[s]; x <= x1[s]; x++)
                    {
                        var dx = x + 0.5f - cx[s];
                        var power = A * dx * dx + 2 * B * dx * dy + C * dy * dy;
                        if (power > 9) continue;
                        var a = al * MathF.Exp(-0.5f * power);
                        if (a < 1 / 255f) continue;
                        if (dep > zb[row + x]) continue;
                        var o = 4 * (row + x);
                        var keep = 1 - a;
                        acc[o] = r * a + acc[o] * keep;
                        acc[o + 1] = gg * a + acc[o + 1] * keep;
                        acc[o + 2] = bb * a + acc[o + 2] * keep;
                        acc[o + 3] = a + acc[o + 3] * keep;
                        any = true;
                    }
                }
                if (any) hit[s] = true;
            }
            for (var i = 0; i < W * rows; i++)
            {
                var a = acc[4 * i + 3];
                if (a < 0.5f / 255) continue;
                var o = 4 * (top * W + i);
                rgba[o] = (byte)Math.Clamp(MathF.Round(acc[4 * i] / a * 255), 0, 255);
                rgba[o + 1] = (byte)Math.Clamp(MathF.Round(acc[4 * i + 1] / a * 255), 0, 255);
                rgba[o + 2] = (byte)Math.Clamp(MathF.Round(acc[4 * i + 2] / a * 255), 0, 255);
                rgba[o + 3] = (byte)Math.Clamp(MathF.Round(a * 255), 0, 255);
            }
        }
        if (OperatingSystem.IsBrowser() || bands < 2) for (var b = 0; b < bands; b++) One(b);
        else Parallel.For(0, bands, One);
        return new Picture(W, H, rgba, hit.Count(h => h));
    }

    /// <summary>A triangle's nearest depths into a band's buffer (its pixels' centres inside it, the depth interpolated: exact for a planar face, in perspective too, since the map keeps faces planar).</summary>
    static void Face((float Ax, float Ay, float Az, float Bx, float By, float Bz, float Cx, float Cy, float Cz) t, float[] zb, int w, int top, int rows)
    {
        var area = (t.Bx - t.Ax) * (t.Cy - t.Ay) - (t.By - t.Ay) * (t.Cx - t.Ax);
        if (Math.Abs(area) < 1e-9f) return;
        var ylo = Math.Max(top, (int)Math.Floor(Math.Min(t.Ay, Math.Min(t.By, t.Cy))));
        var yhi = Math.Min(top + rows - 1, (int)Math.Ceiling(Math.Max(t.Ay, Math.Max(t.By, t.Cy))));
        var xlo = Math.Max(0, (int)Math.Floor(Math.Min(t.Ax, Math.Min(t.Bx, t.Cx))));
        var xhi = Math.Min(w - 1, (int)Math.Ceiling(Math.Max(t.Ax, Math.Max(t.Bx, t.Cx))));
        var inv = 1 / area;
        for (var y = ylo; y <= yhi; y++)
        {
            var py = y + 0.5f;
            for (var x = xlo; x <= xhi; x++)
            {
                var px = x + 0.5f;
                var w0 = ((t.Bx - px) * (t.Cy - py) - (t.By - py) * (t.Cx - px)) * inv;
                var w1 = ((t.Cx - px) * (t.Ay - py) - (t.Cy - py) * (t.Ax - px)) * inv;
                var w2 = 1 - w0 - w1;
                if (w0 < -1e-5f || w1 < -1e-5f || w2 < -1e-5f) continue;
                var z = w0 * t.Az + w1 * t.Bz + w2 * t.Cz;
                var o = (y - top) * w + x;
                if (z < zb[o]) zb[o] = z;
            }
        }
    }
}
