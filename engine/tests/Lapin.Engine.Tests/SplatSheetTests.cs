using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Platform;
using Lapin.PointClouds;
using static Lapin.Engine.Tests.SplatTests;

namespace Lapin.Engine.Tests;

/// <summary>
/// Gaussian splats on sheets (docs/plan-gaussian-splats.md, G4): the CPU splatter against the analytic ellipse of one
/// Gaussian, two overlapping splats blended back to front whatever their order in the file, a face hiding what is behind it;
/// then a 3D view of a linked capture on a sheet: the crop box and the view's section box keep what they keep, Full and Base
/// colour, the model's wall hiding the capture behind it, the picture under the view's lines, and the PDF's image.
/// </summary>
public class SplatSheetTests
{
    static SplatSheetTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lapin-pointcloud-tests");
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("LAPIN_POINTCLOUD_CACHE", dir);
    }

    /// <summary>Looking north (+y) from y = −10, x to the right, z up.</summary>
    static readonly SplatRaster.Projection North = new(new Vec3(0, -10, 0), new Vec3(1, 0, 0), new Vec3(0, 0, 1), new Vec3(0, 1, 0), true);

    static SplatRaster.Gaussian Gauss(Vec3 at, double sx, double sy, double sz, (double W, double X, double Y, double Z) q, float r, float g, float b, float a)
    {
        var c = SplatRaster.Covariance(sx, sy, sz, q.W, q.X, q.Y, q.Z);
        return new SplatRaster.Gaussian(at, c.Xx, c.Xy, c.Xz, c.Yy, c.Yz, c.Zz, r, g, b, a);
    }

    [Fact]
    public void A_single_gaussian_s_footprint_is_its_analytic_ellipse_to_three_sigma()
    {
        // 0.30 m along its x, 0.12 m along its z, turned 30° about the look: at 1 cm a pixel, σ 30 and 12 px.
        var th = 30 * Math.PI / 180;
        var q = (Math.Cos(th / 2), 0.0, Math.Sin(th / 2), 0.0);
        var g = Gauss(new Vec3(0, 0, 0), 0.30, 0.05, 0.12, q, 1, 1, 1, 1);
        var grid = new SplatRaster.Grid(-1.5, 1.5, 0.01, 300, 300);
        var pic = SplatRaster.Render([g], North, grid);
        Assert.Equal(1, pic.Drawn);
        // The variances in pixels with the 3D view's low-pass.
        double v1 = 30 * 30 + SplatRaster.LowPass, v2 = 12 * 12 + SplatRaster.LowPass;
        long count = 0;
        double sw = 0, sxx = 0, sxy = 0, syy = 0;
        for (var y = 0; y < 300; y++)
            for (var x = 0; x < 300; x++)
            {
                var a = pic.Rgba[4 * (y * 300 + x) + 3];
                if (a == 0) continue;
                count++;
                double dx = x + 0.5 - 150, dy = y + 0.5 - 150, w = a / 255.0;
                sw += w; sxx += w * dx * dx; sxy += w * dx * dy; syy += w * dy * dy;
            }
        // Its pixels: the ellipse to 3σ, π · 3σ₁ · 3σ₂.
        var area = Math.PI * 9 * Math.Sqrt(v1 * v2);
        Assert.InRange(count, area * 0.98, area * 1.02);
        Assert.Equal(255, pic.Rgba[4 * (150 * 300 + 150) + 3]);
        // Its weighted second moments: the Gaussian's, truncated at 3σ (a factor 1 − 9e^−4.5 / (2(1 − e^−4.5))), its long axis 30° down to the right
        // (the file's x turned about +y tilts down towards +x; the picture's rows run down).
        var trunc = 1 - 9 * Math.Exp(-4.5) / (2 * (1 - Math.Exp(-4.5)));
        double mxx = sxx / sw, mxy = sxy / sw, myy = syy / sw;
        var mid = (mxx + myy) / 2;
        var rad = Math.Sqrt((mxx - myy) * (mxx - myy) / 4 + mxy * mxy);
        Assert.InRange((mid + rad) / (trunc * v1), 0.98, 1.02);
        Assert.InRange((mid - rad) / (trunc * v2), 0.98, 1.02);
        var angle = 0.5 * Math.Atan2(2 * mxy, mxx - myy) * 180 / Math.PI;
        Assert.InRange(angle, 29.5, 30.5);
    }

    [Fact]
    public void Two_overlapping_splats_blend_back_to_front_premultiplied_whatever_their_order_and_a_face_hides_the_one_behind()
    {
        var one = (1.0, 0.0, 0.0, 0.0);
        var red = Gauss(new Vec3(0, -1, 0), 0.2, 0.2, 0.2, one, 1, 0, 0, 0.5f);
        var blue = Gauss(new Vec3(0, 1, 0), 0.2, 0.2, 0.2, one, 0, 0, 1, 0.5f);
        var grid = new SplatRaster.Grid(-1, 1, 0.01, 200, 200);
        var mid = 4 * (100 * 200 + 100);
        foreach (var list in new[] { new[] { red, blue }, new[] { blue, red } })
        {
            var pic = SplatRaster.Render(list, North, grid);
            // Blue (far) first: 0.5 blue; then red over it: 0.5 red + 0.25 blue, alpha 0.75; straight: 2/3 red, 1/3 blue.
            Assert.InRange(pic.Rgba[mid], 168, 172);
            Assert.Equal(0, pic.Rgba[mid + 1]);
            Assert.InRange(pic.Rgba[mid + 2], 83, 87);
            Assert.InRange(pic.Rgba[mid + 3], 189, 193);
        }
        // A face between them (at y = 0, depth 10 from the eye's plane) hides the blue one: red alone, half opaque.
        var face = (new Vec3(-5, -5, 10), new Vec3(5, -5, 10), new Vec3(0, 5, 10));
        var hidden = SplatRaster.Render([blue, red], North, grid, [face]);
        Assert.Equal(255, hidden.Rgba[mid]);
        Assert.Equal(0, hidden.Rgba[mid + 2]);
        Assert.InRange(hidden.Rgba[mid + 3], 126, 130);
        // Behind the face, the near one too is hidden.
        var behind = SplatRaster.Render([red], North, grid, [(new Vec3(-5, -5, 8), new Vec3(5, -5, 8), new Vec3(0, 5, 8))]);
        Assert.Equal(0, behind.Drawn);
    }

    [Fact]
    public void A_perspective_projects_a_splat_smaller_the_farther_it_is()
    {
        // The eye at the origin looking north, the target plane 10 m away: a splat at 20 m is half as wide as one at 10 m.
        var p = new SplatRaster.Projection(new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 0, 1), new Vec3(0, 1, 0), false, 10);
        var one = (1.0, 0.0, 0.0, 0.0);
        var near = SplatRaster.Projected(Gauss(new Vec3(0, 10, 0), 0.5, 0.01, 0.5, one, 1, 1, 1, 1), p, 0.01);
        var far = SplatRaster.Projected(Gauss(new Vec3(0, 20, 0), 0.5, 0.01, 0.5, one, 1, 1, 1, 1), p, 0.01);
        Assert.InRange(Math.Sqrt(near.A - SplatRaster.LowPass) / Math.Sqrt(far.A - SplatRaster.LowPass), 1.99, 2.01);
        Assert.Equal(50, Math.Sqrt(near.A - SplatRaster.LowPass), 3);
        // Behind the near plane: not drawn.
        Assert.Null(p.Map(new Vec3(0, 0.05, 0)));
    }

    // ─── a capture linked, a 3D view of it on a sheet ───

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    /// <summary>A wall of Gaussians in the plane y = 5, x 0–4, z 0–2, a 10 cm grid, grey (0.4) with a red harmonic of degree 1 that shows seen from the south.</summary>
    static byte[] Capture()
    {
        var gs = new List<G>();
        var rest = new double[9];
        // Red's first coefficient: its basis is −C1·y; −1 here gives +C1 · y, red seen looking north (+y).
        rest[0] = -1;
        for (var i = 0; i <= 40; i++)
            for (var k = 0; k <= 20; k++)
                gs.Add(new G(i * 0.1, 5, k * 0.1, [(0.4 - 0.5) / Splat.C0, (0.4 - 0.5) / Splat.C0, (0.4 - 0.5) / Splat.C0], Math.Log(0.9 / 0.1),
                    [Math.Log(0.08), Math.Log(0.02), Math.Log(0.08)], [1, 0, 0, 0], rest));
        return Ply(gs, 9);
    }

    static string Link(Fixture f)
    {
        var placement = new JsonObject { ["mode"] = "origin", ["up"] = "z" };
        var r = (JsonObject)f.Run("pointcloud.link", new JsonObject { ["name"] = "wall.ply", ["data"] = Convert.ToBase64String(Capture()), ["placement"] = placement })!;
        var id = (string)r["id"]!;
        PointCloudSamples.WaitIndexed(f, id);
        f.Session.TakeDeliveries();
        return id;
    }

    /// <summary>A 3D view looking north, level, orthographic, at the capture's middle.</summary>
    static string View(Fixture f)
    {
        var camera = new JsonObject { ["azimuth"] = Math.PI / 2, ["pitch"] = 0.0, ["distance"] = 20, ["target"] = new JsonArray(2, 5, 1), ["ortho"] = true };
        var id = (string)f.Run("view.create", new JsonObject { ["kind"] = "3d", ["camera"] = camera })!["id"]!;
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(id), ["attrs"] = new JsonObject { ["scale"] = 50 } });
        return id;
    }

    static PenImage? Picture(Fixture f, string view) =>
        Views.Render(f.Session.Reader, f.Model.Get(view)!, print: true).Pens.SelectMany(p => p.Images).FirstOrDefault(i => i.ImageId.Contains(":splats:"));

    [Fact]
    public void The_crop_box_and_the_view_s_section_box_keep_what_they_keep_and_full_colour_follows_the_eye()
    {
        var f = F();
        var id = Link(f);
        var link = Links.Require(f.Model, id);
        var store = PointCloudLinks.SplatsOf(link)!;
        var t = PointCloudLinks.TransformOf(f.Model, link);
        Assert.Equal(41 * 21, SplatSheets.Gather(store, t, North, 0, null, null).Count);
        // The crop box: x 0–2 kept (21 columns).
        var crop = new CropBox([1, 5, 1], [2.05, 1, 4], 0);
        Assert.Equal(21 * 21, SplatSheets.Gather(store, t, North, 0, crop, null).Count);
        // The section box: z 0–1 kept (11 rows).
        var box = new View3dDrawings.Box(new Vec3(-1, 0, -0.5), new Vec3(5, 10, 1.05), 0);
        Assert.Equal(41 * 11, SplatSheets.Gather(store, t, North, 0, null, box).Count);
        // Base colour: its grey; full: red added seen looking north, as the reference evaluation gives it.
        var b = SplatSheets.Gather(store, t, North, 0, null, null)[0];
        var full = SplatSheets.Gather(store, t, North, 1, null, null)[0];
        Assert.InRange(b.R, 0.39f, 0.41f);
        Assert.Equal(b.G, b.R, 3);
        var want = Splat.Colour((0.4, 0.4, 0.4), [0, 128, 128, 128, 128, 128, 128, 128, 128], 1, (0, 1, 0));
        Assert.InRange(full.R, want.R - 0.01, want.R + 0.01);
        Assert.True(full.R > b.R + 0.4, $"full colour's red {full.R} is the base's {b.R} and the harmonic's");
        Assert.Equal(b.G, full.G, 2);
        // Seen from the north (looking south) the harmonic takes red away.
        var south = new SplatRaster.Projection(new Vec3(0, 20, 0), new Vec3(-1, 0, 0), new Vec3(0, 0, 1), new Vec3(0, -1, 0), true);
        Assert.Equal(0f, SplatSheets.Gather(store, t, south, 1, null, null)[0].R, 2);
    }

    [Fact]
    public void A_3D_view_of_a_capture_on_a_sheet_prints_its_splats_under_its_lines_hidden_behind_the_model_and_the_PDF_carries_the_image()
    {
        var f = F();
        var id = Link(f);
        // A wall in front of the capture's middle (x 1.5–2.5, at y = 2), its height above the capture's 2 m.
        f.Wall(1.5, 2, 2.5, 2, height: 3);
        var view = View(f);
        var img = Picture(f, view);
        Assert.NotNull(img);
        var raster = Raster.Read(img!.Data!)!;
        // At 1:50 and 300 dpi a pixel is 4.2 mm of the model: the capture's 4 m and its edge Gaussians' 3σ (4.48 m) 1058 pixels wide.
        var pixel = SplatSheets.PaperPixel * 50 / 1000;
        var (tl, br) = (img.Corners[0], img.Corners[2]);
        Assert.Equal((int)Math.Round((br.X - tl.X) / pixel), raster.W);
        Assert.InRange(raster.W, 1050, 1065);
        // The frame's origin is the target (x = 2): x = 1 and x = 3 are left and right of the wall, x = 2 behind it.
        int Column(double x) => (int)((x - 2 - tl.X) / pixel);
        var row = (int)((tl.Y - 0) / pixel);
        byte Alpha(double x) => raster.Alpha![row * raster.W + Column(x)];
        Assert.True(Alpha(1) > 200, $"left of the wall the capture shows ({Alpha(1)})");
        Assert.True(Alpha(3) > 200, $"right of the wall the capture shows ({Alpha(3)})");
        Assert.Equal(0, Alpha(2));
        Assert.True(raster.Data[3 * (row * raster.W + Column(1))] > raster.Data[3 * (row * raster.W + Column(1)) + 1] + 60, "full colour: red seen from the south");

        // On a sheet: the picture under the view's lines, and the same picture taken again (the cache) when the sheet is drawn again.
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Capture", ["paper"] = "A3" })!["id"]!;
        var vp = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = view, ["x"] = 200, ["y"] = 140 })!["viewportId"]!;
        var page = Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!);
        var pens = page.Pens.Where(p => p.Viewport == vp).ToList();
        var under = pens.FindIndex(p => p.Images.Any(i => i.ImageId.Contains(":splats:")));
        var lines = pens.FindIndex(p => p.Segments.Count > 0);
        Assert.True(under >= 0 && lines > under, $"the picture ({under}) under the lines ({lines})");
        var again = Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!).Pens.SelectMany(p => p.Images).First(i => i.ImageId.Contains(":splats:"));
        Assert.Same(img.Data, again.Data);

        // The PDF: an image XObject of the picture's size, its alpha as a soft mask, Flate.
        var pdf = Encoding.Latin1.GetString(Sheets.Pdf(f.Session.Reader, [f.Model.Get(sheet)!]));
        Assert.Contains($"/Subtype /Image /Width {raster.W} /Height {raster.H} /ColorSpace /DeviceRGB /BitsPerComponent 8 /SMask", pdf);

        // Base colour in this view: grey, another picture.
        f.Run("view.graphics", new JsonObject { ["id"] = view, ["pointCloud"] = id, ["value"] = new JsonObject { ["splatColor"] = "base" } });
        var grey = Picture(f, view)!;
        Assert.NotEqual(img.ImageId, grey.ImageId);
        var gr = Raster.Read(grey.Data!)!;
        var q = 3 * (row * gr.W + Column(1));
        Assert.InRange(gr.Data[q] - gr.Data[q + 1], -3, 3);
        // Drawn as points in this view: no splats on the sheet.
        f.Run("view.graphics", new JsonObject { ["id"] = view, ["pointCloud"] = id, ["value"] = new JsonObject { ["display"] = "points" } });
        Assert.Null(Picture(f, view));
    }

    [Fact]
    public void A_camera_view_draws_the_capture_inside_its_field_of_view()
    {
        var f = F();
        Link(f);
        var camera = new JsonObject { ["azimuth"] = Math.PI / 2, ["pitch"] = 0.0, ["distance"] = 6, ["target"] = new JsonArray(2, 5, 1), ["ortho"] = false, ["fov"] = 40, ["aspect"] = 1.5 };
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "3d", ["camera"] = camera })!["id"]!;
        var img = Picture(f, view);
        Assert.NotNull(img);
        // The window at the target's plane: ±6 tan 20° high, 1.5 times as wide; the capture (4 × 2 m there) inside it.
        var halfH = 6 * Math.Tan(20 * Math.PI / 180);
        Assert.True(img!.Corners.All(c => Math.Abs(c.X) <= halfH * 1.5 + 1e-9 && Math.Abs(c.Y) <= halfH + 1e-9));
        Assert.InRange(img.Corners[1].X - img.Corners[0].X, 4, 4.6);
    }
}

/// <summary>
/// How long a sheet's splats take (docs/plan-gaussian-splats.md, G4's measurement): a capture of a million Gaussians with
/// harmonics of degree 3 on the walls and floor of a 12 × 8 × 4 m room (scales 3 mm–6 cm, as a trained capture's), drawn
/// on an A3 sheet's whole drawing area (400 × 277 mm) at 300 dpi, an axonometric from the south-west. Run only when asked
/// (LAPIN_SPLAT_BENCH=1, best in Release); otherwise it returns at once.
/// </summary>
public class SplatSheetBenchTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public void A_million_gaussians_on_an_A3_sheet_at_300_dpi()
    {
        if (Environment.GetEnvironmentVariable("LAPIN_SPLAT_BENCH") != "1") return;
        var n = int.TryParse(Environment.GetEnvironmentVariable("LAPIN_SPLAT_BENCH_N"), out var nn) ? nn : 1_000_000;
        var r = new Random(3);
        double U(double a, double b) => a + (b - a) * r.NextDouble();
        var gs = new List<G>(n);
        for (var i = 0; i < n; i++)
        {
            // On a wall or the floor, flat along it.
            var side = i % 5;
            var (x, y, z) = side switch { 0 => (U(0, 12), U(0, 8), 0.0), 1 => (U(0, 12), 0.0, U(0, 4)), 2 => (U(0, 12), 8.0, U(0, 4)), 3 => (0.0, U(0, 8), U(0, 4)), _ => (12.0, U(0, 8), U(0, 4)) };
            var s = Math.Log(U(0.003, 0.06));
            var t = Math.Log(U(0.003, 0.06));
            double[] scale = side == 0 ? [s, t, -7] : side is 1 or 2 ? [s, -7, t] : [-7, s, t];
            var rest = Enumerable.Range(0, 45).Select(_ => U(-0.3, 0.3)).ToArray();
            gs.Add(new G(x, y, z, [U(-1.5, 1.5), U(-1.5, 1.5), U(-1.5, 1.5)], U(-2, 5), scale, [1, U(-0.1, 0.1), U(-0.1, 0.1), U(-0.1, 0.1)], rest));
        }
        var dir = Directory.CreateTempSubdirectory("lapin-splat-sheet-bench-");
        try
        {
            var file = Path.Combine(dir.FullName, "room.ply");
            File.WriteAllBytes(file, Ply(gs, 45));
            gs.Clear();
            var cache = Path.Combine(dir.FullName, "index");
            Indexer.Build(PointReaders.Open(new FileSource(file)), cache, "bench", new FileInfo(file).Length);
            var store = SplatStore.Open(cache)!;
            double[] t0 = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0];
            var look = new Vec3(1, 1, -0.8).Normalized();
            var right = new Vec3(Math.Sqrt(0.5), -Math.Sqrt(0.5), 0);
            var p = new SplatRaster.Projection(new Vec3(6, 4, 2), right, Vec3.Cross(right, look).Normalized(), look, true);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var splats = SplatSheets.Gather(store, t0, p, 3, null, null);
            var gather = clock.Elapsed.TotalSeconds;
            // 400 × 277 mm at 300 dpi round the room's middle, 20 m across: 1:50.
            var (w, h) = ((int)Math.Round(400 / SplatSheets.PaperPixel), (int)Math.Round(277 / SplatSheets.PaperPixel));
            var pixel = 20.0 / w;
            var grid = new SplatRaster.Grid(-10, h * pixel / 2, pixel, w, h);
            clock.Restart();
            var pic = SplatRaster.Render(splats, p, grid);
            var splat = clock.Elapsed.TotalSeconds;
            clock.Restart();
            var png = Lapin.Drawing.Documents.DocPng.EncodeRgba(pic.W, pic.H, pic.Rgba);
            var encode = clock.Elapsed.TotalSeconds;
            var line = $"G4: {splats.Count:N0} Gaussians (degree 3) gathered and coloured in {gather:F2} s, {pic.Drawn:N0} splatted on {w} × {h} px (A3 at 300 dpi) in {splat:F2} s, PNG {png.Length / 1048576.0:F1} MB in {encode:F2} s";
            output.WriteLine(line);
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "lapin-splat-bench.txt"), line + "\n");
            Assert.True(pic.Drawn > n / 2);
        }
        finally { dir.Delete(true); }
    }
}
