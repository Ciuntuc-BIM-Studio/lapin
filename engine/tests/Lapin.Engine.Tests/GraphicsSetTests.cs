using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Visibility / Graphics: a view's own settings, named sets shared by views, IFC classes, the 3D view.</summary>
public class GraphicsSetTests
{
    static int Cut(Fixture f, string view) =>
        ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!)["pens"]!.AsArray().OfType<JsonObject>()
            .Where(p => (string)p["layer"]! == "CUT").Sum(p => p["segments"]!.AsArray().Count / 4);

    [Fact]
    public void A_set_reaches_every_view_that_takes_it_and_its_views_keep_it_when_it_goes()
    {
        var f = new Fixture();
        f.Wall(0, 0, 5, 0);
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 4, ["offset"] = 0.5 });
        string Plan() => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var (v1, v2) = (Plan(), Plan());
        var all = Cut(f, v1);
        Assert.True(all > 0);
        f.Run("view.graphics", new JsonObject { ["id"] = v1, ["category"] = "walls", ["value"] = new JsonObject { ["hidden"] = true } });
        var noWalls = Cut(f, v1);
        Assert.True(noWalls < all);
        Assert.Equal(all, Cut(f, v2));
        var set = (string)f.Run("view.saveGraphicsSet", new JsonObject { ["id"] = v1, ["name"] = "No walls" })!["id"]!;
        f.Run("view.useGraphicsSet", new JsonObject { ["id"] = v2, ["setId"] = set });
        Assert.Equal(noWalls, Cut(f, v2));
        // A class hidden through one view hides it in both.
        f.Run("view.graphics", new JsonObject { ["id"] = v2, ["class"] = IfcClass.Slab, ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Equal(0, Cut(f, v1));
        f.Run("view.removeGraphicsSet", new JsonObject { ["setId"] = set });
        Assert.Equal(0, Cut(f, v2));
        Assert.Null(f.Model.Get(v1)!.Str("graphicsSet"));
        Assert.Throws<CommandException>(() => f.Run("view.saveGraphicsSet", new JsonObject { ["id"] = v1, ["name"] = "" }));
    }

    [Fact]
    public void The_3D_view_hides_by_category_and_class()
    {
        var f = new Fixture();
        var w = f.Wall(0, 0, 5, 0);
        Assert.Empty(f.Session.Query("view.hidden3d", new JsonObject())!.AsArray());
        f.Run("view.graphics", new JsonObject { ["id"] = "3d", ["category"] = "walls", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Equal([w], f.Session.Query("view.hidden3d", new JsonObject())!.AsArray().Select(x => (string)x!));
        var v = (JsonObject)f.Session.Query("view.visibility", new JsonObject { ["id"] = "3d" })!;
        Assert.True((bool)v["graphics"]!["categories"]!["walls"]!["hidden"]!);
        f.Session.Undo();
        Assert.Empty(f.Session.Query("view.hidden3d", new JsonObject())!.AsArray());
    }

    [Fact]
    public void A_plan_s_range_reads_back_with_its_problems()
    {
        var f = new Fixture();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var r = (JsonObject)f.Session.Query("view.range", new JsonObject { ["id"] = plan })!;
        Assert.False((bool)r["custom"]!);
        Assert.Empty(r["problems"]!.AsArray());
        f.Run("view.setRange", new JsonObject { ["id"] = plan, ["top"] = new JsonObject { ["level"] = "self", ["offset"] = 0.5 } });
        r = (JsonObject)f.Session.Query("view.range", new JsonObject { ["id"] = plan })!;
        Assert.True((bool)r["custom"]!);
        Assert.Equal(["topBelowCut"], r["problems"]!.AsArray().Select(x => (string)x!));
    }

    [Fact]
    public void The_project_s_pen_table_widens_its_pens_on_the_drawing()
    {
        var f = new Fixture();
        f.Wall(0, 0, 5, 0);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("view.objectStyle", new JsonObject { ["category"] = "walls", ["role"] = "cut", ["spec"] = new JsonObject { ["pen"] = 7 } });
        double CutWidth() => ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!)["pens"]!.AsArray().OfType<JsonObject>()
            .Where(p => (string)p["layer"]! == "CUT").Max(p => (double)p["width"]!);
        Assert.Equal(0.7, CutWidth(), 9); // pen 7 at 1:100: ISO 0.7
        f.Run("view.penTable", new JsonObject { ["pen"] = 7, ["scale"] = 100, ["width"] = 0.9 });
        Assert.Equal(0.9, CutWidth(), 9);
        Assert.True((bool)f.Session.Query("view.objectStyles", new JsonObject())!["customTable"]!);
        Assert.Throws<CommandException>(() => f.Run("view.penTable", new JsonObject { ["pen"] = 7, ["scale"] = 75, ["width"] = 0.9 }));
        f.Run("view.penTable", new JsonObject { ["reset"] = true });
        Assert.Equal(0.7, CutWidth(), 9);
    }

    [Fact]
    public void The_base_point_and_the_survey_point_show_in_plans_when_asked()
    {
        var f = new Fixture();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        List<string> Texts() => ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!)["pens"]!.AsArray().OfType<JsonObject>()
            .Where(p => p["texts"] != null).SelectMany(p => p["texts"]!.AsArray().Select(t => (string)t!["text"]!)).ToList();
        Assert.DoesNotContain(Texts(), t => t.StartsWith("Base point"));
        f.Run("project.location", new JsonObject { ["show"] = true, ["base"] = new JsonObject { ["elevation"] = 120.5 }, ["survey"] = new JsonObject { ["x"] = 10, ["y"] = 5, ["easting"] = 500000, ["northing"] = 4500000, ["elevation"] = 118 } });
        Assert.Contains("Base point +120.50", Texts());
        Assert.Contains("Survey point E 500000.000 N 4500000.000 +118.00", Texts());
    }
}
