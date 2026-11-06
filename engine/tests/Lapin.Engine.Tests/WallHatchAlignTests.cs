using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>A wall's own hatch alignment (decision 369): along its axis, every band from the axis's start, over its materials' alignment.</summary>
public class WallHatchAlignTests
{
    /// <summary>The directions (degrees, 0–180) of the cut hatch's lines in a plan of the storey.</summary>
    static List<double> Directions(Fixture f)
    {
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var r = Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!);
        return r.Pens.Where(p => p.Layer == PenLayer.Hatch).SelectMany(p => p.Segments)
            .Where(s => (s.B - s.A).Length > 1e-4)
            .Select(s => { var d = Math.Atan2(s.B.Y - s.A.Y, s.B.X - s.A.X) * 180 / Math.PI; return ((d % 180) + 180) % 180; }).ToList();
    }

    static bool Along(double d, double axis) => new[] { axis, axis + 90 }.Any(a => Math.Abs(((d - a) % 180 + 270) % 180 - 90) < 0.5);

    [Fact]
    public void A_wall_hatched_along_its_axis_turns_its_courses_with_it()
    {
        var f = new Fixture();
        var type = f.Type("Brick 25", ("Solid brick", 0.25, true));
        var wall = f.Wall(0, 0, 6 * Math.Cos(Math.PI / 6), 6 * Math.Sin(Math.PI / 6), type);
        // By the material (from the view): the courses square to the plan, not to the wall at 30°.
        var before = Directions(f);
        Assert.NotEmpty(before);
        Assert.DoesNotContain(before, d => Along(d, 30) && !Along(d, 0));
        f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["hatchAlign"] = "axis" } });
        var after = Directions(f);
        Assert.NotEmpty(after);
        Assert.All(after, d => Assert.True(Along(d, 30), $"a hatch line at {d:0.#}° is not along the wall"));
        // Back to the material's.
        f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["hatchAlign"] = "material" } });
        Assert.Equal(before.Count, Directions(f).Count);
    }
}
