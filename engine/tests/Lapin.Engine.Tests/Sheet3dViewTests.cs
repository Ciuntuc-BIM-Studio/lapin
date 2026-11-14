using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// A 3D view placed on a sheet (forum topic 91: it stayed empty, the engine drew no 3D view): drawn from its camera by the
/// engine, orthographic or in perspective, a view saved without a camera too, after the project is saved and opened again,
/// and in the sheet's PDF.
/// </summary>
public class Sheet3dViewTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static JsonObject Camera(double azimuth, double pitch, double distance, double[] target, bool ortho) => new()
    {
        ["azimuth"] = azimuth, ["pitch"] = pitch, ["distance"] = distance, ["target"] = new JsonArray(target[0], target[1], target[2]), ["ortho"] = ortho,
    };

    static string View3d(Fixture f, JsonObject? camera = null)
    {
        var args = new JsonObject { ["kind"] = "3d" };
        if (camera != null) args["camera"] = camera;
        return (string)f.Run("view.create", args)!["id"]!;
    }

    static (string Sheet, string Viewport) Place(Fixture f, string view)
    {
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "3D", ["paper"] = "A3" })!["id"]!;
        var vp = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = view, ["x"] = 150, ["y"] = 140 })!["viewportId"]!;
        return (sheet, vp);
    }

    /// <summary>The segments the viewport's view draws on the paper (not its title).</summary>
    static List<(Vec2 A, Vec2 B)> Drawn(Fixture f, string sheet, string viewport) =>
        [.. Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!).Pens.Where(p => p.Viewport == viewport && p.Layer is not ("TITLE" or "TEXT")).SelectMany(p => p.Segments)];

    static void House(Fixture f)
    {
        f.Wall(0, 0, 8, 0);
        f.Wall(8, 0, 8, 6);
        f.Wall(8, 6, 0, 6);
        f.Wall(0, 6, 0, 0);
    }

    [Fact]
    public void A_saved_3D_view_on_a_sheet_draws_the_model_from_its_camera()
    {
        var f = F();
        House(f);
        var view = View3d(f, Camera(Math.PI / 4, -0.5, 30, [4, 3, 1.5], ortho: true));
        var (sheet, vp) = Place(f, view);
        var lines = Drawn(f, sheet, vp);
        Assert.True(lines.Count > 12, $"the 3D view drew {lines.Count} lines on the sheet");
        // Drawn at its scale (1:100): the house's 8 × 6 m plan across 3 m of height fits in some 120 mm, not a dot.
        var w = lines.Max(l => Math.Max(l.A.X, l.B.X)) - lines.Min(l => Math.Min(l.A.X, l.B.X));
        Assert.InRange(w, 60, 140);
        // Seen from above: the walls' tops are drawn, and what the near walls hide is not (no hidden lines in 3D).
        var r = Views.Render(f.Session.Reader, f.Model.Get(view)!, print: true);
        Assert.Equal("3d", r.Kind);
        Assert.Null(r.Problem);
        Assert.DoesNotContain(r.Pens, p => p.Dash is { Length: > 0 });
    }

    [Fact]
    public void A_new_3D_view_without_a_camera_is_drawn_from_the_south_west()
    {
        var f = F();
        House(f);
        var (sheet, vp) = Place(f, View3d(f));
        Assert.True(Drawn(f, sheet, vp).Count > 12);
    }

    [Fact]
    public void A_perspective_sees_the_near_end_larger_than_the_far_end()
    {
        var f = F();
        // One wall seen along its length from beyond its west end, a little from the side, level (so its vertical edges stay vertical).
        f.Wall(0, 0, 20, 0);
        var target = new[] { 10.0, 0, 1.5 };
        var persp = View3d(f, Camera(0.15, 0, 14, target, ortho: false));
        var ortho = View3d(f, Camera(0.15, 0, 14, target, ortho: true));
        var p = Views.Render(f.Session.Reader, f.Model.Get(persp)!, print: true);
        var o = Views.Render(f.Session.Reader, f.Model.Get(ortho)!, print: true);
        Assert.NotEmpty(p.Pens.SelectMany(x => x.Segments));
        // The wall's vertical edges: in orthographic all of one height, in perspective the near end's taller than the far end's.
        static List<double> Verticals(Views.Rendered r) => [.. r.Pens.SelectMany(x => x.Segments).Where(s => Math.Abs(s.Item1.X - s.Item2.X) < 1e-6 && Math.Abs(s.Item1.Y - s.Item2.Y) > 0.1).Select(s => Math.Abs(s.Item1.Y - s.Item2.Y))];
        var ov = Verticals(o);
        Assert.NotEmpty(ov);
        Assert.True(ov.Max() - ov.Min() < 1e-6, "orthographic: every vertical edge of the wall is as tall");
        var pv = Verticals(p);
        Assert.True(pv.Count >= 2, $"perspective drew {pv.Count} vertical edges");
        Assert.True(pv.Max() > pv.Min() * 1.5, $"perspective: the near end ({pv.Max():0.00}) is taller than the far end ({pv.Min():0.00})");
        // Within the camera's field of view (45° high, 16:9): what is past it is not drawn.
        var halfH = 14 * Math.Tan(Math.PI / 8);
        Assert.All(p.Pens.SelectMany(x => x.Segments), s => Assert.True(Math.Abs(s.Item1.Y) <= halfH + 1e-6 && Math.Abs(s.Item2.Y) <= halfH + 1e-6));
    }

    [Fact]
    public void A_perspective_from_inside_the_model_is_cut_at_the_eye()
    {
        var f = F();
        var south = f.Wall(0, 0, 8, 0);
        var east = f.Wall(8, 0, 8, 6);
        f.Wall(8, 6, 0, 6);
        var west = f.Wall(0, 6, 0, 0);
        // Standing in the room looking east: the far wall seen, the side walls running past the eye cut at it, the wall behind not drawn.
        var view = View3d(f, Camera(0, 0, 4, [6, 3, 1.5], ortho: false));
        var r = Views.Render(f.Session.Reader, f.Model.Get(view)!, print: true);
        var segments = r.Pens.SelectMany(x => x.Segments).ToList();
        Assert.NotEmpty(segments);
        Assert.All(segments, s => Assert.True(double.IsFinite(s.Item1.X) && double.IsFinite(s.Item1.Y) && double.IsFinite(s.Item2.X) && double.IsFinite(s.Item2.Y)));
        var picked = r.Marks.Where(mk => mk.Pick).Select(mk => mk.Owner).ToHashSet();
        Assert.Contains(east, picked);
        Assert.DoesNotContain(west, picked);
        if (Lapin.Booleans.ManifoldBooleans.Available) Assert.Contains(south, picked);
    }

    [Fact]
    public void The_view_s_section_box_and_hidden_elements_are_drawn_as_the_3D_shows_them()
    {
        var f = F();
        var kept = f.Wall(0, 0, 8, 0);
        var boxedOut = f.Wall(0, 20, 8, 20);
        var hidden = f.Wall(0, 10, 8, 10);
        var view = View3d(f, Camera(Math.PI / 4, -0.5, 30, [4, 10, 1.5], ortho: true));
        f.Run("view.setSectionBox", new JsonObject { ["id"] = view, ["box"] = new JsonObject { ["min"] = new JsonArray(-1, -1, -1), ["max"] = new JsonArray(9, 15, 5) } });
        f.Run("view.hideElements", new JsonObject { ["viewId"] = view, ["ids"] = new JsonArray(hidden) });
        var r = Views.Render(f.Session.Reader, f.Model.Get(view)!, print: true);
        var picked = r.Marks.Where(mk => mk.Pick).Select(mk => mk.Owner).ToHashSet();
        Assert.Contains(kept, picked);
        Assert.DoesNotContain(boxedOut, picked);
        Assert.DoesNotContain(hidden, picked);
    }

    [Fact]
    public void The_camera_keeps_its_field_of_view_and_the_sheet_draws_the_same_after_saving_and_opening()
    {
        var f = F();
        House(f);
        var camera = Camera(Math.PI / 4, -0.4, 25, [4, 3, 1.5], ortho: false);
        camera["fov"] = 50;
        camera["aspect"] = 1.5;
        var view = View3d(f);
        f.Run("view.setCamera", new JsonObject { ["id"] = view, ["camera"] = camera });
        Assert.Equal(50, Json.Number(f.Model.Get(view)!.Attrs["camera"]!["fov"]));
        var (sheet, vp) = Place(f, view);
        var before = Drawn(f, sheet, vp);
        Assert.NotEmpty(before);

        var files = new ProjectFiles((_, _) => throw new InvalidOperationException("no files"), (_, _, _, _, _) => throw new InvalidOperationException("no files"));
        var (bytes, _) = files.SaveBytes(f.Session, "house.lapin");
        var g = F();
        files.OpenBytes(g.Session, "house.lapin", bytes);
        var after = Drawn(g, sheet, vp);
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(before.Sum(s => s.A.X + s.B.Y), after.Sum(s => s.A.X + s.B.Y), 6);
    }

    [Fact]
    public void The_sheet_s_PDF_prints_the_3D_view()
    {
        var f = F();
        House(f);
        var view = View3d(f, Camera(Math.PI / 4, -0.5, 30, [4, 3, 1.5], ortho: true));
        var empty = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Empty", ["paper"] = "A3" })!["id"]!;
        var (sheet, _) = Place(f, view);
        var page = Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!);
        var bare = Sheets.Compose(f.Session.Reader, f.Model.Get(empty)!);
        // The sheet prints what it composes: the 3D view's lines besides its frame and title block.
        Assert.True(page.Pens.Sum(p => p.Segments.Count) > bare.Pens.Sum(p => p.Segments.Count) + 12);
        var pdf = Sheets.Pdf(f.Session.Reader, [f.Model.Get(sheet)!]);
        var none = Sheets.Pdf(f.Session.Reader, [f.Model.Get(empty)!]);
        Assert.True(pdf.Length > none.Length + 200, $"the PDF with the 3D view is {pdf.Length} bytes, the empty sheet's {none.Length}");
    }
}
