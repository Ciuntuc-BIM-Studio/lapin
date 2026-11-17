using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Detail callouts' boundaries and inset views' frames (issue #73, forum 113): their pen weight, colour and line type, the
/// project's default in the drafting standards and each one's own; nothing set draws as before (a callout 0.35 black, grey
/// dashed while it refers to no view; an inset's frame 0.18 grey); views, sheets and PDF draw them through the same marks; a
/// standards file carries the defaults.
/// </summary>
public class FrameLineTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Id(JsonNode? n) => (string)n!["id"]!;

    /// <summary>The lines a mark draws on a layer, as the view renders it for a sheet.</summary>
    static List<(double Width, string Color, double[]? Dash)> Lines(Fixture f, string view, string owner, string layer) =>
        [.. Views.Render(f.Session.Reader, Views.Find(f.Model, view)!, print: true).Marks.Where(m => m.Owner == owner)
            .SelectMany(m => m.Lines).Where(l => l.Layer == layer).Select(l => (l.Width, l.Color, l.Dash))];

    static (Fixture F, string Plan, string Callout) Callout()
    {
        var f = F();
        f.Wall(0, 0, 6, 0, height: 3);
        var plan = Id(f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey }));
        var callout = Id(f.Run("view.callout", new JsonObject { ["viewId"] = plan, ["min"] = J(-0.5, -0.5), ["max"] = J(1.5, 1) }));
        return (f, plan, callout);
    }

    static (double Width, string Color, double[]? Dash) Boundary(Fixture f, string plan, string callout) =>
        Lines(f, plan, callout, Callouts.Layer).First();

    [Fact]
    public void A_callout_draws_as_before_when_nothing_is_set()
    {
        var (f, plan, callout) = Callout();
        var b = Boundary(f, plan, callout);
        Assert.Equal(0.35, b.Width, 9);
        Assert.Equal("#000000", b.Color);
        Assert.Null(b.Dash);
        // Refers to no view (its view deleted would delete it: a reference to a view that is gone): grey and dashed.
        var e = f.Model.Get(callout)!;
        Assert.Equal(new FramePen(0.35, "#8f98a3", [2, 1.5]).Dash, Callouts.BoundaryPen(StyleBook.Default, e, refers: false).Dash);
        Assert.Equal("#8f98a3", Callouts.BoundaryPen(StyleBook.Default, e, refers: false).Color);
        Assert.Equal(0.35, Callouts.BoundaryPen(StyleBook.Default, e, refers: false).Width);
        // The standards untouched write no frame lines.
        var book = (JsonObject)f.Session.Query("standards.get", new JsonObject())!;
        Assert.Null(book[FrameLines.CalloutKey]);
        Assert.Null(book[FrameLines.InsetKey]);
    }

    [Fact]
    public void The_projects_default_and_a_callouts_own_draw_its_boundary()
    {
        var (f, plan, callout) = Callout();
        f.Run("standards.frameLine", new JsonObject { ["kind"] = "callout", ["weight"] = 0.5, ["color"] = "#ff0000", ["lineType"] = "dashdot" });
        var b = Boundary(f, plan, callout);
        Assert.Equal(0.5, b.Width, 9);
        Assert.Equal("#ff0000", b.Color);
        Assert.Equal(Dashes.Of("dashdot"), b.Dash);
        // The head and its leader keep their ink.
        Assert.Contains(Lines(f, plan, callout, Callouts.Layer), l => l.Color == "#000000");
        // Its own: the weight and the line type, the colour still the project's.
        f.Run("view.updateCallout", new JsonObject { ["id"] = callout, ["lineWeight"] = 0.7, ["lineType"] = "continuous" });
        b = Boundary(f, plan, callout);
        Assert.Equal(0.7, b.Width, 9);
        Assert.Equal("#ff0000", b.Color);
        Assert.Null(b.Dash);
        // A line type of the project's own, by name.
        f.Run("standards.setLineType", new JsonObject { ["name"] = "Fence", ["dashes"] = new JsonArray(5, 1, 1, 1) });
        f.Run("view.updateCallout", new JsonObject { ["id"] = callout, ["lineType"] = "fence" });
        Assert.Equal("Fence", f.Model.Get(callout)!.Str("lineType"));
        Assert.Equal([5.0, 1, 1, 1], Boundary(f, plan, callout).Dash!);
        // Cleared: the project's again; the project's cleared: Lapin's own.
        f.Run("view.updateCallout", new JsonObject { ["id"] = callout, ["lineWeight"] = null, ["lineType"] = null });
        Assert.Equal(0.5, Boundary(f, plan, callout).Width, 9);
        f.Run("standards.frameLine", new JsonObject { ["kind"] = "callout", ["weight"] = null, ["color"] = null, ["lineType"] = null });
        Assert.Equal((0.35, "#000000", (double[]?)null), Boundary(f, plan, callout));
        Assert.Null(((JsonObject)f.Session.Query("standards.get", new JsonObject())!)[FrameLines.CalloutKey]);
        // What does not read is refused.
        Assert.ThrowsAny<Exception>(() => f.Run("view.updateCallout", new JsonObject { ["id"] = callout, ["lineType"] = "zigzag" }));
        Assert.ThrowsAny<Exception>(() => f.Run("view.updateCallout", new JsonObject { ["id"] = callout, ["lineWeight"] = 9 }));
        Assert.ThrowsAny<Exception>(() => f.Run("standards.frameLine", new JsonObject { ["kind"] = "wall", ["weight"] = 0.5 }));
    }

    [Fact]
    public void A_sheets_pens_carry_the_callouts_line()
    {
        var (f, plan, callout) = Callout();
        f.Run("view.updateCallout", new JsonObject { ["id"] = callout, ["lineWeight"] = 0.5, ["lineColor"] = "#0055ff", ["lineType"] = "dashed" });
        var printed = Views.Render(f.Session.Reader, f.Model.Get(plan)!, print: true);
        Assert.Contains(printed.Pens, p => p.Layer == Callouts.Layer && p.Stroke == "#0055ff" && Math.Abs(p.Width - 0.5) < 1e-9 && p.Dash is { } d && d.SequenceEqual(Dashes.Of("dashed")));
        // And the screen's drawing.
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        Assert.Contains("#0055ff", d.ToJsonString());
    }

    [Fact]
    public void An_insets_frame_takes_the_projects_default_and_its_own()
    {
        var f = F();
        f.Wall(0, 0, 4, 0, height: 3);
        var section = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(2, -2), ["end"] = J(2, 2), ["scale"] = 20 }));
        var plan = "plan:" + f.Storey;
        var inset = Id(f.Run("view.inset", new JsonObject { ["viewId"] = plan, ["source"] = section, ["at"] = J(20, 0) }));
        var frame = Lines(f, plan, inset, Insets.Layer);
        Assert.Equal(4, frame.Count);
        Assert.All(frame, l => Assert.Equal((0.18, "#555555", (double[]?)null), l));
        f.Run("standards.frameLine", new JsonObject { ["kind"] = "inset", ["lineType"] = "dashdot", ["weight"] = 0.25 });
        Assert.All(Lines(f, plan, inset, Insets.Layer), l => Assert.Equal((0.25, "#555555"), (l.Width, l.Color)));
        Assert.All(Lines(f, plan, inset, Insets.Layer), l => Assert.Equal(Dashes.Of("dashdot"), l.Dash));
        f.Run("view.updateInset", new JsonObject { ["id"] = inset, ["lineColor"] = "#00aa00" });
        Assert.All(Lines(f, plan, inset, Insets.Layer), l => Assert.Equal("#00aa00", l.Color));
        // The callouts' default is not the insets'.
        Assert.Null(DraftingResources.Book(f.Model).CalloutLine);
        // Frameless: no frame to draw.
        f.Run("view.updateInset", new JsonObject { ["id"] = inset, ["frame"] = false });
        Assert.Empty(Lines(f, plan, inset, Insets.Layer));
    }

    [Fact]
    public void A_standards_file_carries_the_defaults()
    {
        var (f, _, _) = Callout();
        f.Run("standards.setLineType", new JsonObject { ["name"] = "Fence", ["dashes"] = new JsonArray(5, 1, 1, 1) });
        f.Run("standards.frameLine", new JsonObject { ["kind"] = "callout", ["weight"] = 0.5, ["color"] = "#ff0000", ["lineType"] = "Fence" });
        f.Run("standards.frameLine", new JsonObject { ["kind"] = "inset", ["lineType"] = "dashed" });
        var text = (string)f.Session.Query("standards.export", new JsonObject { ["name"] = "Office" })!["text"]!;
        var g = F();
        g.Run("standards.importFrom", new JsonObject { ["text"] = text, ["options"] = true });
        var book = DraftingResources.Book(g.Model);
        Assert.Equal(new FrameLine(0.5, "#ff0000", "Fence"), book.CalloutLine);
        Assert.Equal(new FrameLine(null, null, "dashed"), book.InsetFrame);
        // Read and written again, the same.
        Assert.Equal(book.CalloutLine, Standards.Read(Standards.ToJson(book)).CalloutLine);
        // An unknown line type or a weight out of range in a file is dropped, the rest kept.
        var odd = Standards.Read(JsonNode.Parse("""{"calloutLine":{"weight":40,"color":"#123456","lineType":"nope"},"insetFrame":{"weight":"x"}}"""));
        Assert.Equal(new FrameLine(null, "#123456", null), odd.CalloutLine);
        Assert.Null(odd.InsetFrame);
    }
}
