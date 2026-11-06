using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Platform;
using Lapin.Platform.Cde.PdfRemarks;
using Drafts = Lapin.Drafting.Cad.Drafts;
using ViewImages = Lapin.Drafting.Cad.ViewImages;

namespace Lapin.Engine.Tests;

/// <summary>
/// Pictures and PDFs to trace over (a tester's wish, #89, decision 371): a PDF page's lines read back from Lapin's own PDF and
/// laid into a plan at the drawing's scale as one group, its picture as an image under them; an image placed and scaled true.
/// </summary>
public class PdfTraceTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;

    /// <summary>A 2 × 2 picture as a PNG.</summary>
    static byte[] Png() => PdfImport.Png([255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255], 2, 2, 3);

    /// <summary>An A4 landscape page with a 100 × 50 mm rectangle 20 mm in from its bottom-left corner and a 60 × 30 mm picture.</summary>
    static byte[] Pdf()
    {
        var pen = new Pen("CUT", "#000000", 0.35, null, null);
        Vec2 P(double x, double y) => new(x, 210 - y);
        Vec2[] r = [P(20, 20), P(120, 20), P(120, 70), P(20, 70)];
        for (var i = 0; i < 4; i++) pen.Segments.Add((r[i], r[(i + 1) % 4]));
        var pic = new Pen("IMAGE", null, 0, null, null);
        pic.Images.Add(new PenImage("img", [P(150, 100), P(210, 100), P(210, 130), P(150, 130)]));
        return PdfWriter.Write([new PaperPage(297, 210, [pic, pen])], id => id == "img" ? Png() : null);
    }

    [Fact]
    public void A_pdf_page_is_read_back_as_its_lines_and_its_picture()
    {
        var page = PdfImport.Read(Pdf(), 0);
        Assert.Equal(297, page.Width * 25.4 / 72, 0);
        var pts = page.Paths.SelectMany(p => p.Points).Select(p => p * (25.4 / 72)).ToList();
        Assert.Contains(pts, p => Math.Abs(p.X - 20) < 0.5 && Math.Abs(p.Y - 20) < 0.5);
        Assert.Contains(pts, p => Math.Abs(p.X - 120) < 0.5 && Math.Abs(p.Y - 70) < 0.5);
        var pic = Assert.Single(page.Pictures);
        Assert.Equal(60, (pic.Corners[1] - pic.Corners[0]).Length * 25.4 / 72, 0);
        Assert.Equal(2, pic.PixelWidth);
    }

    [Fact]
    public void A_pdf_comes_into_a_plan_at_its_drawing_scale_as_one_group()
    {
        var f = F();
        var plan = Plan(f);
        var r = (JsonObject)f.Run("drafting.importPdf", new JsonObject
        {
            ["viewId"] = plan, ["name"] = "survey.pdf", ["data"] = Convert.ToBase64String(Pdf()), ["scale"] = 100, ["at"] = new JsonArray(5, 5),
        })!;
        Assert.True((int)r["lines"]! >= 1);
        Assert.Equal(1, (int)r["pictures"]!);
        // 100 mm of paper at 1:100 is 10 m; the page's corner at (5, 5), the rectangle 2 m in from it.
        var pts = Drafts.On(f.Model, plan).SelectMany(e => Drafts.ShapeOf(e) switch { Drafting.Cad.Polyline p => p.Pts, Drafting.Cad.Line l => [l.A, l.B], _ => [] }).ToList();
        Assert.Contains(pts, p => Math.Abs(p.X - 7) < 0.05 && Math.Abs(p.Y - 7) < 0.05);
        Assert.Contains(pts, p => Math.Abs(p.X - 17) < 0.05 && Math.Abs(p.Y - 12) < 0.05);
        var image = Assert.Single(ViewImages.On(f.Model, plan));
        Assert.Equal(6, image.Num("width"), 2);
        Assert.Equal(3, image.Num("height"), 2);
        // One group, named after the file: moved as one.
        var group = f.Model.Get((string)r["groupId"]!)!;
        Assert.Equal("survey", group.Name);
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(group.Id), ["op"] = "move", ["by"] = new JsonArray(10, 0) });
        Assert.Equal(15 + 15, ViewImages.On(f.Model, plan).Single().Num("x"), 1);
    }

    [Fact]
    public void An_image_is_placed_by_its_pixels_aspect_and_scaled_true_by_two_points()
    {
        var f = F();
        var plan = Plan(f);
        var id = (string)f.Run("drafting.placeImage", new JsonObject { ["viewId"] = plan, ["name"] = "photo.png", ["data"] = Convert.ToBase64String(Png()), ["width"] = 4, ["at"] = new JsonArray(1, 1) })!["id"]!;
        var e = f.Model.Get(id)!;
        Assert.Equal(4, e.Num("height"), 6);
        // Two points 2 m apart on it are really 3 m apart: it grows by 1.5 about the first.
        f.Run("drafting.calibrateImage", new JsonObject { ["id"] = id, ["a"] = new JsonArray(1, 1), ["b"] = new JsonArray(3, 1), ["length"] = 3 });
        e = f.Model.Get(id)!;
        Assert.Equal(6, e.Num("width"), 6);
        Assert.Equal(1, e.Num("x"), 6);
        // The view draws it, under all, by its corners.
        var r = Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!);
        Assert.Contains(r.Marks, m => m.Owner == id && m.Images.Count == 1);
        Assert.ThrowsAny<Exception>(() => f.Run("drafting.placeImage", new JsonObject { ["viewId"] = plan, ["name"] = "x.txt", ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("hello")) }));
    }
}
