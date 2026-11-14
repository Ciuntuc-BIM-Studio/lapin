using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A view's crop region and annotation crop (forum topic 68): the model cut at the region's edge, annotations wholly
/// outside the annotation crop left out, crop off drawing everything, the region's frame drawn only when visible, the
/// sheet's viewport the region; a level's default plan saved as its plan view with it, in one step.
/// </summary>
public class ViewCropTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    /// <summary>A 10 × 8 m box of walls and its plan at 1:50.</summary>
    static (Fixture F, string Plan) House()
    {
        var f = F();
        f.Wall(0, 0, 10, 0); f.Wall(10, 0, 10, 8); f.Wall(10, 8, 0, 8); f.Wall(0, 8, 0, 0);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        return (f, plan);
    }

    static Views.Rendered Draw(Fixture f, string view, bool print = false) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!, print: print);

    static IEnumerable<Vec2> ModelPoints(Views.Rendered r) =>
        r.Pens.Where(p => p.Layer is not (ViewCrops.Layer or "DIMENSION" or "TEXT")).SelectMany(p => p.Segments.SelectMany(s => new[] { s.A, s.B }).Concat(p.Loops.SelectMany(l => l)));

    static void Crop(Fixture f, string view, JsonObject args) { args["id"] = view; f.Run("view.setCrop", args); }

    [Fact]
    public void A_plan_cropped_to_a_rectangle_draws_only_what_is_inside_its_lines_cut_at_the_edge()
    {
        var (f, plan) = House();
        Assert.True(ModelPoints(Draw(f, plan)).Max(p => p.X) > 10);
        Crop(f, plan, new JsonObject { ["on"] = true, ["min"] = new JsonArray(-1, -1), ["max"] = new JsonArray(5, 9) });
        var pts = ModelPoints(Draw(f, plan)).ToList();
        Assert.NotEmpty(pts);
        Assert.All(pts, p => Assert.True(p.X <= 5 + 1e-9 && p.X >= -1 - 1e-9 && p.Y >= -1 - 1e-9 && p.Y <= 9 + 1e-9, $"({p.X}, {p.Y}) outside"));
        // The walls along x are cut at x = 5.
        Assert.Contains(pts, p => Math.Abs(p.X - 5) < 1e-9);
        // The east wall, wholly outside, picks nothing any more.
        var east = Views.Render(f.Session.Reader, Views.Find(f.Model, "plan:" + f.Storey)!).Marks.Where(m => m.Pick && m.Lines.All(l => l.A.X > 9 && l.B.X > 9)).Select(m => m.Owner).First();
        Assert.DoesNotContain(Draw(f, plan).Marks, m => m.Owner == east);
    }

    [Fact]
    public void The_annotation_crop_leaves_out_a_dimension_outside_it_and_keeps_one_inside()
    {
        var (f, plan) = House();
        f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["a"] = new JsonArray(20, 0), ["b"] = new JsonArray(25, 0), ["at"] = new JsonArray(20, 1) });
        f.Run("drafting.addDimension", new JsonObject { ["viewId"] = plan, ["a"] = new JsonArray(0, 4), ["b"] = new JsonArray(3, 4), ["at"] = new JsonArray(0, 5) });
        string[] Texts() => [.. Draw(f, plan).Pens.SelectMany(p => p.Texts.Select(t => t.Text))];
        // Cropped without an annotation crop: the annotations stay, wherever they are.
        Crop(f, plan, new JsonObject { ["on"] = true, ["min"] = new JsonArray(-1, -1), ["max"] = new JsonArray(11, 9) });
        Assert.Contains(Texts(), t => t.Contains("5000"));
        // The annotation crop, 10 mm round the region at 1:50 (0.5 m): the far dimension goes, the near one stays.
        Crop(f, plan, new JsonObject { ["annotation"] = true, ["offset"] = 10 });
        Assert.DoesNotContain(Texts(), t => t.Contains("5000"));
        Assert.Contains(Texts(), t => t.Contains("3000"));
        // Wider (paper 400 mm to the right: 20 m), it is back.
        Crop(f, plan, new JsonObject { ["offset"] = new JsonArray(10, 10, 400, 10) });
        Assert.Contains(Texts(), t => t.Contains("5000"));
    }

    [Fact]
    public void Crop_off_draws_everything_and_keeps_the_region()
    {
        var (f, plan) = House();
        Crop(f, plan, new JsonObject { ["on"] = true, ["min"] = new JsonArray(-1, -1), ["max"] = new JsonArray(5, 9) });
        Crop(f, plan, new JsonObject { ["on"] = false });
        Assert.True(ModelPoints(Draw(f, plan)).Max(p => p.X) > 10);
        Assert.DoesNotContain(Draw(f, plan).Pens, p => p.Layer == ViewCrops.Layer);
        var kept = ViewCrops.Of(f.Model.Get(plan)!)!;
        Assert.Equal((5.0, 9.0, false), (kept.Max.X, kept.Max.Y, kept.On));
    }

    [Fact]
    public void A_visible_region_draws_its_frame_on_screen_and_paper_and_a_hidden_one_neither()
    {
        var (f, plan) = House();
        Crop(f, plan, new JsonObject { ["on"] = true, ["min"] = new JsonArray(-1, -1), ["max"] = new JsonArray(5, 9) });
        foreach (var print in new[] { false, true })
        {
            var frame = Draw(f, plan, print).Pens.Where(p => p.Layer == ViewCrops.Layer).SelectMany(p => p.Segments).ToList();
            Assert.Equal(4, frame.Count);
            Assert.Contains(frame, s => s.A == new Vec2(-1, -1) || s.B == new Vec2(-1, -1));
        }
        Crop(f, plan, new JsonObject { ["visible"] = false });
        Assert.DoesNotContain(Draw(f, plan).Pens, p => p.Layer == ViewCrops.Layer);
        Assert.DoesNotContain(Draw(f, plan, true).Pens, p => p.Layer == ViewCrops.Layer);
        // Still cropped.
        Assert.True(ModelPoints(Draw(f, plan)).Max(p => p.X) <= 5 + 1e-9);
    }

    [Fact]
    public void On_a_sheet_the_viewport_is_the_crop_region()
    {
        var (f, plan) = House();
        Crop(f, plan, new JsonObject { ["on"] = true, ["min"] = new JsonArray(-1, -1), ["max"] = new JsonArray(5, 9) });
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = plan, ["x"] = 200, ["y"] = 200 });
        var vp = (JsonObject)f.Session.Query("sheet.drawing", new JsonObject { ["id"] = sheet })!["viewports"]![0]!;
        // 6 × 10 m at 1:50.
        Assert.Equal((120.0, 200.0), (Math.Round((double)vp["x1"]! - (double)vp["x0"]!, 6), Math.Round((double)vp["y1"]! - (double)vp["y0"]!, 6)));
    }

    [Fact]
    public void A_turned_plan_is_cropped_square_to_the_screen()
    {
        var (f, plan) = House();
        f.Run("view.setOrientation", new JsonObject { ["id"] = plan, ["angle"] = 90 });
        // Turned by −90°: a model point (x, y) shows at (y, −x); the region on screen x 0..4 is the model's y 0..4.
        Crop(f, plan, new JsonObject { ["on"] = true, ["min"] = new JsonArray(-1, -11), ["max"] = new JsonArray(4, 1) });
        var pts = ModelPoints(Draw(f, plan)).ToList();
        Assert.NotEmpty(pts);
        Assert.All(pts, p => Assert.True(p.Y <= 4 + 1e-9, $"({p.X}, {p.Y})"));
    }

    [Fact]
    public void Cropping_a_levels_default_plan_saves_it_as_its_plan_view_in_one_step()
    {
        var f = F();
        f.Wall(0, 0, 10, 0);
        var r = f.Run("view.setCrop", new JsonObject { ["id"] = "plan:" + f.Storey, ["on"] = true })!;
        var id = (string)r["id"]!;
        Assert.NotEqual("plan:" + f.Storey, id);
        Assert.Equal(id, StoreyPlans.Of(f.Model, f.Storey)!.Id);
        // Round what it drew, with the margin.
        var crop = ViewCrops.Of(f.Model.Get(id)!)!;
        Assert.True(crop.On && crop.Visible && crop.Min.X < 0 && crop.Max.X > 10 && crop.Max.X < 11);
        f.Session.Undo();
        Assert.Null(StoreyPlans.Of(f.Model, f.Storey));
        Assert.Throws<CommandException>(() => f.Run("view.setCrop", new JsonObject { ["id"] = "plan:" + f.Storey, ["min"] = new JsonArray(0, 0), ["max"] = new JsonArray(0.01, 4) }));
    }
}
