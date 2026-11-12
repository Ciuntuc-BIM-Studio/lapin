using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A view's scale changed from the view (forum #30, decision 492): the project's standard scales, offered by the view's
/// scale picker, and the scales new plans, sections and elevations take (Drafting standards → Project).
/// </summary>
public class ViewScaleTests
{
    static JsonObject Book(Fixture f) => (JsonObject)f.Session.Query("standards.get", new JsonObject())!;
    static double[] Choices(Fixture f) => Book(f)["scaleChoices"]!.AsArray().Select(x => (double)x!).ToArray();
    static string View(Fixture f, JsonObject a) => (string)f.Run("view.create", a)!["id"]!;
    static double ScaleOf(Fixture f, string id) => Views.Find(f.Model, id)!.Num("scale");

    [Fact]
    public void The_project_offers_the_usual_scales_until_it_has_its_own()
    {
        var f = new Fixture();
        Assert.Equal(Standards.DefaultViewScales, Choices(f));
        Assert.Null(Book(f)["scales"]);
        // Its own list: sorted, each once; empty goes back to the usual ones.
        f.Run("standards.options", new JsonObject { ["scales"] = new JsonArray(100, 50, 20, 50, 75) });
        Assert.Equal([20.0, 50, 75, 100], Choices(f));
        f.Session.Undo();
        Assert.Equal(Standards.DefaultViewScales, Choices(f));
        f.Run("standards.options", new JsonObject { ["scales"] = new JsonArray(10, 25) });
        f.Run("standards.options", new JsonObject { ["scales"] = null });
        Assert.Equal(Standards.DefaultViewScales, Choices(f));
        Assert.Throws<CommandException>(() => f.Run("standards.options", new JsonObject { ["scales"] = new JsonArray(0, 100) }));
        Assert.Throws<CommandException>(() => f.Run("standards.options", new JsonObject { ["scales"] = new JsonArray(20000) }));
    }

    [Fact]
    public void New_views_take_the_projects_scale_for_their_kind()
    {
        var f = new Fixture();
        var section = new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, -1), ["end"] = new JsonArray(5, -1) };
        Assert.Equal(100, ScaleOf(f, View(f, (JsonObject)section.DeepClone())));
        f.Run("standards.options", new JsonObject { ["planScale"] = 50, ["sectionScale"] = 20, ["elevationScale"] = 200 });
        Assert.Equal(50.0, (double)Book(f)["planScale"]!);
        // A level's default plan, held by no view, draws at the project's plan scale.
        Assert.Equal(50, ScaleOf(f, "plan:" + f.Storey));
        Assert.Equal(50, ScaleOf(f, View(f, new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })));
        Assert.Equal(20, ScaleOf(f, View(f, (JsonObject)section.DeepClone())));
        Assert.Equal(200, ScaleOf(f, View(f, new JsonObject { ["kind"] = "elevation", ["direction"] = "north" })));
        // A scale asked for wins.
        Assert.Equal(25, ScaleOf(f, View(f, new JsonObject { ["kind"] = "elevation", ["direction"] = "east", ["scale"] = 25 })));
        // Cleared: 1:100 again.
        f.Run("standards.options", new JsonObject { ["planScale"] = null });
        Assert.Equal(100, ScaleOf(f, "plan:" + f.Storey));
        Assert.Throws<CommandException>(() => f.Run("standards.options", new JsonObject { ["sectionScale"] = 0 }));
    }

    [Fact]
    public void A_views_scale_changes_in_one_step()
    {
        var f = new Fixture();
        f.Wall(0, 0, 5, 0);
        var section = View(f, new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(2, -2), ["end"] = new JsonArray(2, 2) });
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(section), ["attrs"] = new JsonObject { ["scale"] = 50 } });
        Assert.Equal(50, ScaleOf(f, section));
        Assert.Equal(50.0, (double)((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = section })!)["scale"]!);
        f.Session.Undo();
        Assert.Equal(100, ScaleOf(f, section));
        // The default plan's scale picked: the level's plan view is made, at that scale, in one step.
        var plan = View(f, new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 });
        Assert.Equal(plan, StoreyPlans.Of(f.Model, f.Storey)?.Id);
        f.Session.Undo();
        Assert.Null(StoreyPlans.Of(f.Model, f.Storey));
    }
}
