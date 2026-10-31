using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The level marker in sections and elevations (decision 217): its style and size are the project's.</summary>
public class LevelMarkTests
{
    static JsonArray LevelPens(Fixture f, string view) =>
        new(((JsonArray)((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!)["pens"]!).Where(p => (string?)p!["layer"] == "LEVEL").Select(p => p!.DeepClone()).ToArray());

    [Fact]
    public void The_marker_is_an_open_triangle_of_5_mm_unless_the_project_says_half_black_or_a_tick()
    {
        var f = new Fixture();
        f.Wall(0, 0, 6, 0);
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, -2), ["end"] = new JsonArray(7, -2), ["scale"] = 50 })!["id"]!;
        Assert.Equal(("open", 5.0), Views.LevelMark(Spatial.Project(f.Model)));
        var open = LevelPens(f, view);
        Assert.DoesNotContain(open, p => p!["fill"] != null);
        // Half black: a fill for each level shown, and the triangle 8 mm tall on paper: 0.4 m at 1:50.
        f.Run("view.objectStyle", new JsonObject { ["levelMark"] = new JsonObject { ["style"] = "half", ["size"] = 8 } });
        Assert.Equal(("half", 8.0), Views.LevelMark(Spatial.Project(f.Model)));
        var levels = Spatial.Storeys(f.Model).Count();
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
        var fills = ((JsonArray)d["pens"]!).Where(p => p!["fill"] != null && (string?)p["layer"] == "DRAFT-FILL" && (string?)p["fill"] == "#222222").Sum(p => ((JsonArray)p!["loops"]!).Count);
        Assert.Equal(levels, fills);
        var half = ((JsonArray)d["pens"]!).First(p => (string?)p!["layer"] == "LEVEL" && (string?)p["stroke"] == "#222222" && (p["segments"] as JsonArray)?.Count > 0);
        var segs = ((JsonArray)half!["segments"]!).Select(x => (double)x!).ToArray();
        var tall = Enumerable.Range(0, segs.Length / 4).Max(i => Math.Abs(segs[4 * i + 3] - segs[4 * i + 1]));
        Assert.Equal(0.4, tall, 6);
        // A tick: one short line per level, no triangle.
        f.Run("view.objectStyle", new JsonObject { ["levelMark"] = new JsonObject { ["style"] = "tick" } });
        var tick = LevelPens(f, view).First(p => (string?)p!["stroke"] == "#222222");
        Assert.Equal(levels, ((JsonArray)tick!["segments"]!).Count / 4);
        Assert.Throws<CommandException>(() => f.Run("view.objectStyle", new JsonObject { ["levelMark"] = new JsonObject { ["style"] = "star" } }));
    }
}
