using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Detail callouts (forum topic 107): a boundary drawn on a plan, a section or an elevation makes a detail view of the same
/// kind cropped to it, at a larger scale and the finest level; the parent draws the boundary, a leader and a head with the
/// detail's number and sheet, live; the boundary's grips crop the detail again; one undo step each; a reference callout
/// points at a view that is there.
/// </summary>
public class CalloutTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Id(JsonNode? n) => (string)n!["id"]!;
    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonNode? Mark(JsonObject d, string id) => ((JsonArray)d["marks"]!).FirstOrDefault(m => (string?)m!["id"] == id);

    [Fact]
    public void A_callout_on_a_plan_makes_a_cropped_detail_view_at_a_larger_scale_and_finer_level()
    {
        var f = F();
        f.Wall(0, 0, 6, 0, height: 3);
        var plan = "plan:" + f.Storey;
        var r = f.Run("view.callout", new JsonObject { ["viewId"] = plan, ["min"] = J(-0.5, -0.5), ["max"] = J(1.5, 1) })!;
        var detail = f.Model.Get((string)r["view"]!)!;
        Assert.Equal("plan", detail.Str("kind"));
        Assert.Equal(f.Storey, detail.Str("storeyId"));
        Assert.Equal(plan, detail.Str(Callouts.Key));
        Assert.Equal("Detail 1", detail.Name);
        // The plan at 1:100: the detail at 1:20, at LOD 400.
        Assert.Equal(20, detail.Num("scale"));
        Assert.Equal(PlanDetail.Lod400, ViewDetail.Of(f.Model, detail));
        var crop = ViewCrops.Of(detail)!;
        Assert.True(crop.On);
        Assert.Equal(-0.5, crop.Min.X, 9); Assert.Equal(-0.5, crop.Min.Y, 9);
        Assert.Equal(1.5, crop.Max.X, 9); Assert.Equal(1.0, crop.Max.Y, 9);
        // The detail is no level's floor plan: the level keeps its default plan.
        Assert.Null(StoreyPlans.Of(f.Model, f.Storey));
        // It draws the wall, cut to the box.
        var d = Drawing(f, detail.Id);
        var b = ((JsonArray)d["bounds"]!).Select(x => (double)x!).ToArray();
        Assert.InRange(b[2], 1.5 - 1e-6, 1.5 + 1e-6);
        // A second one is Detail 2; a scale asked is kept.
        var r2 = f.Run("view.callout", new JsonObject { ["viewId"] = plan, ["min"] = J(4, -0.5), ["max"] = J(6.5, 1), ["scale"] = 10 })!;
        var d2 = f.Model.Get((string)r2["view"]!)!;
        Assert.Equal("Detail 2", d2.Name);
        Assert.Equal(10, d2.Num("scale"));
    }

    [Fact]
    public void The_parent_draws_the_mark_with_the_details_number_and_sheet_live()
    {
        var f = F();
        f.Wall(0, 0, 6, 0, height: 3);
        // The level's own plan view.
        var plan = Id(f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey }));
        var r = f.Run("view.callout", new JsonObject { ["viewId"] = plan, ["min"] = J(-0.5, -0.5), ["max"] = J(1.5, 1) })!;
        var callout = Id(r);
        var detail = (string)r["view"]!;
        var mark = Mark(Drawing(f, plan), callout)!;
        Assert.Equal("–", (string?)mark["data"]!["label"]);
        Assert.Equal("–", (string?)mark["data"]!["sheet"]);
        Assert.Equal(detail, (string?)mark["data"]!["target"]);
        // The boundary's four sides and the head drawn; picked by them.
        Assert.True(((JsonArray)mark["segments"]!).Count >= 4 * 4);
        // Placed third on sheet A-501: "3 / A-501".
        var sheet = Id(f.Run("sheet.create", new JsonObject { ["name"] = "Details", ["number"] = "A-501", ["paper"] = "A3" }));
        foreach (var other in new[] { plan, Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(2, -2), ["end"] = J(2, 2) })) })
            f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = other, ["x"] = 100, ["y"] = 100 });
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = detail, ["x"] = 300, ["y"] = 100 });
        mark = Mark(Drawing(f, plan), callout)!;
        Assert.Equal("3", (string?)mark["data"]!["number"]);
        Assert.Equal("A-501", (string?)mark["data"]!["sheet"]);
        Assert.Equal("3 / A-501", (string?)mark["data"]!["label"]);
        // Printed (a sheet, PDF, DXF): the head's texts in the plan's pens.
        var printed = Views.Render(f.Session.Reader, f.Model.Get(plan)!, print: true);
        Assert.Contains(printed.Marks, m => m.Owner == callout);
        Assert.Contains(printed.Pens.SelectMany(p => p.Texts), t => t.Text == "A-501");
        Assert.Contains(printed.Pens.SelectMany(p => p.Texts), t => t.Text == "3");
        // Visibility / Graphics hides it with the callouts.
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["annotation"] = "callouts", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Null(Mark(Drawing(f, plan), callout));
    }

    [Fact]
    public void Its_grips_crop_the_detail_again_and_each_change_is_one_undo_step()
    {
        var f = F();
        f.Wall(0, 0, 6, 0, height: 3);
        var plan = "plan:" + f.Storey;
        var r = f.Run("view.callout", new JsonObject { ["viewId"] = plan, ["min"] = J(-0.5, -0.5), ["max"] = J(1.5, 1) })!;
        var (callout, detail) = (Id(r), (string)r["view"]!);
        f.Run("view.updateCallout", new JsonObject { ["id"] = callout, ["max"] = J(3, 2) });
        var crop = ViewCrops.Of(f.Model.Get(detail)!)!;
        Assert.Equal(3, crop.Max.X, 9); Assert.Equal(2, crop.Max.Y, 9);
        Assert.Equal(-0.5, crop.Min.X, 9);
        // The head moved: kept as an offset.
        f.Run("view.updateCallout", new JsonObject { ["id"] = callout, ["head"] = J(1, 0.5) });
        var head = ((JsonArray)Mark(Drawing(f, plan), callout)!["data"]!["head"]!).Select(x => (double)x!).ToArray();
        Assert.Equal(4, head[0], 6); Assert.Equal(2.5, head[1], 6);
        // A circle: the detail cropped to its box.
        f.Run("view.updateCallout", new JsonObject { ["id"] = callout, ["shape"] = "circle", ["centre"] = J(2, 0), ["radius"] = 1 });
        crop = ViewCrops.Of(f.Model.Get(detail)!)!;
        Assert.Equal(1, crop.Min.X, 9); Assert.Equal(3, crop.Max.X, 9); Assert.Equal(-1, crop.Min.Y, 9);
        // Undo: the head, then the rectangle again, each one step; and the callout's creation took one too.
        f.Session.Undo();
        crop = ViewCrops.Of(f.Model.Get(detail)!)!;
        Assert.Equal(3, crop.Max.X, 9); Assert.Equal(2, crop.Max.Y, 9);
        Assert.Equal("rect", f.Model.Get(callout)!.Str("shape"));
        f.Session.Undo();
        f.Session.Undo();
        Assert.Equal(1.5, ViewCrops.Of(f.Model.Get(detail)!)!.Max.X, 9);
        f.Session.Undo();
        Assert.Null(f.Model.Get(callout));
        Assert.Null(f.Model.Get(detail));
        // The detail's own crop changed in its settings moves the rectangle.
        r = f.Run("view.callout", new JsonObject { ["viewId"] = plan, ["min"] = J(-0.5, -0.5), ["max"] = J(1.5, 1) })!;
        (callout, detail) = (Id(r), (string)r["view"]!);
        f.Run("view.setCrop", new JsonObject { ["id"] = detail, ["max"] = J(2.5, 1.5) });
        Assert.Equal(2.5, Args.PointOf(f.Model.Get(callout)!.Attrs["max"]).X, 9);
    }

    [Fact]
    public void Deleting_the_callout_leaves_the_detail_unless_asked_and_deleting_the_detail_takes_its_callout()
    {
        var f = F();
        f.Wall(0, 0, 6, 0, height: 3);
        var plan = "plan:" + f.Storey;
        var r = f.Run("view.callout", new JsonObject { ["viewId"] = plan, ["min"] = J(-0.5, -0.5), ["max"] = J(1.5, 1) })!;
        var (callout, detail) = (Id(r), (string)r["view"]!);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(callout) });
        Assert.Null(f.Model.Get(callout));
        Assert.NotNull(f.Model.Get(detail));
        f.Session.Undo();
        // Both at once (the interface asks): one step.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(callout, detail) });
        Assert.Null(f.Model.Get(callout));
        Assert.Null(f.Model.Get(detail));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(callout));
        // The detail deleted from the browser: its callout goes with it.
        f.Run("element.delete", new JsonObject { ["id"] = detail });
        Assert.Null(f.Model.Get(callout));
    }

    [Fact]
    public void A_section_callout_makes_a_section_detail_that_follows_its_line_and_marks_no_line_of_its_own()
    {
        var f = F();
        f.Wall(0, 0, 4, 0, height: 3);
        var section = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(2, -2), ["end"] = J(2, 2), ["scale"] = 50 }));
        // The section's coordinates: along its line and up from the storey.
        var r = f.Run("view.callout", new JsonObject { ["viewId"] = section, ["min"] = J(1.5, -0.3), ["max"] = J(2.5, 0.6) })!;
        var (callout, detail) = (Id(r), (string)r["view"]!);
        var d = f.Model.Get(detail)!;
        Assert.Equal("section", d.Str("kind"));
        Assert.Equal(20, d.Num("scale"));
        Assert.Equal(section, d.Str(Callouts.Key));
        Assert.NotNull(Mark(Drawing(f, section), callout));
        var drawn = Drawing(f, detail);
        Assert.Null(drawn["problem"]);
        var crop = ViewCrops.Of(d)!;
        Assert.True(crop.On);
        Assert.Equal(1.5, crop.Min.X, 9); Assert.Equal(0.6, crop.Max.Y, 9);
        // The plan marks the section, not its detail.
        var planMarks = ((JsonArray)Drawing(f, "plan:" + f.Storey)["marks"]!).Select(m => (string?)m!["id"]).ToHashSet();
        Assert.Contains(section, planMarks);
        Assert.DoesNotContain(detail, planMarks);
        // The section's line moved: the detail moves with it (one step), its own scale and crop kept.
        f.Run("view.setLine", new JsonObject { ["id"] = section, ["start"] = J(3, -2), ["end"] = J(3, 2) });
        d = f.Model.Get(detail)!;
        Assert.Equal(3, Args.PointOf(d.Attrs["start"]).X, 9);
        Assert.Equal(20, d.Num("scale"));
        Assert.NotNull(ViewCrops.Of(d));
        f.Session.Undo();
        Assert.Equal(2, Args.PointOf(f.Model.Get(detail)!.Attrs["start"]).X, 9);
    }

    [Fact]
    public void A_reference_callout_points_at_a_view_that_is_there_and_makes_none()
    {
        var f = F();
        f.Wall(0, 0, 4, 0, height: 3);
        var plan = "plan:" + f.Storey;
        var section = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(2, -2), ["end"] = J(2, 2) }));
        var views = f.Model.OfClass(Views.Class).Count();
        var r = f.Run("view.callout", new JsonObject { ["viewId"] = plan, ["shape"] = "polygon", ["pts"] = new JsonArray(J(0, -1), J(2, -1), J(1, 1)), ["reference"] = section })!;
        Assert.Equal(section, (string?)r["view"]);
        Assert.Equal(views, f.Model.OfClass(Views.Class).Count());
        var c = f.Model.Get(Id(r))!;
        Assert.False(c.Bool("made"));
        // Its grips reshape it and crop nothing.
        f.Run("view.updateCallout", new JsonObject { ["id"] = c.Id, ["pts"] = new JsonArray(J(0, -1), J(3, -1), J(1, 2)) });
        Assert.Null(ViewCrops.Of(f.Model.Get(section)!));
        Assert.Equal("–", (string?)Mark(Drawing(f, plan), c.Id)!["data"]!["label"]);
        // Not in 3D, not to itself.
        Assert.Throws<CommandException>(() => f.Run("view.callout", new JsonObject { ["viewId"] = plan, ["min"] = J(0, 0), ["max"] = J(1, 1), ["reference"] = plan }));
        Assert.Throws<CommandException>(() => f.Run("view.callout", new JsonObject { ["viewId"] = plan, ["min"] = J(0, 0), ["max"] = J(0.01, 1) }));
    }

    [Fact]
    public void The_detail_scale_comes_from_the_standard_scales()
    {
        var book = StyleBookOf([1, 5, 10, 20, 50, 100]);
        Assert.Equal(20, Callouts.DetailScale(book, 100));
        Assert.Equal(20, Callouts.DetailScale(book, 50));
        Assert.Equal(10, Callouts.DetailScale(book, 20));
        Assert.Equal(5, Callouts.DetailScale(book, 10));
        Assert.Equal(1, Callouts.DetailScale(book, 5));
    }

    static Lapin.Drawing.StyleBook StyleBookOf(double[] scales) => Lapin.Drawing.StyleBook.Default with { Scales = scales };
}
