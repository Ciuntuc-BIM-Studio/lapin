using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Curtain walls (the old Lapin's configurator): a grid of mullions, transoms and panels along an axis, from their type.</summary>
public class CurtainWallTests
{
    static string Add(Fixture f, double x1, double y1, double x2, double y2, JsonObject? more = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["height"] = 3 };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("architecture.addCurtainWall", a)!["id"]!;
    }

    [Fact]
    public void A_grid_no_wider_than_its_spacing_lays_mullions_transoms_and_panels()
    {
        // 6 × 3 m in bays of at most 1.5 m: 4 bays along, 2 up.
        var layout = CurtainWalls.Layout(CurtainSpec.Default, 6, 3);
        Assert.Equal([0, 1.5, 3, 4.5, 6], layout.U);
        Assert.Equal([0, 1.5, 3], layout.V);
        Assert.Equal(5, layout.Pieces.Count(p => p.Role == "mullion"));
        Assert.Equal(12, layout.Pieces.Count(p => p.Role == "transom"));
        Assert.Equal(8, layout.Pieces.Count(p => p.Role == "panel"));
        // A fixed spacing leaves the rest split at both ends; offsets are taken as they are.
        Assert.Equal([0.5, 1.7, 2.9, 4.1, 5.3], new CurtainGrid("spacing", Spacing: 1.2).Lines(5.8).Select(x => Math.Round(x, 9)));
        Assert.Equal([1, 2.5], new CurtainGrid("offsets", Offsets: [2.5, 1, 9]).Lines(4));
        Assert.Equal([2, 4], new CurtainGrid("count", Count: 3).Lines(6));
    }

    [Fact]
    public void A_curtain_wall_is_its_parts_measured_and_cuts_the_wall_it_is_drawn_in()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 8, 0);
        var full = f.Session.Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid));
        var cw = Add(f, 2, 0, 6, 0);
        var g = f.Session.Geometry(cw)!;
        Assert.Null(g.Diagnostics);
        // 4 m: 3 bays (1.33 m), 2 up: 4 mullions, 9 transoms, 6 panels.
        Assert.Equal(4, g.Parts.Count(p => p.Key!.StartsWith("mullion:")));
        Assert.Equal(9, g.Parts.Count(p => p.Key!.StartsWith("transom:")));
        Assert.Equal(6, g.Parts.Count(p => p.Key!.StartsWith("panel:")));
        var q = f.Session.Quantities(cw);
        Assert.Equal(12, q.First(x => x.Name == "Qto_CurtainWallQuantities").Quantities.Single(x => x.Name == "GrossSideArea").Value, 9);
        var glass = q.First(x => x.Name == "Lapin_CurtainWallParts").Quantities.Single(x => x.Name == "GlassArea").Value;
        Assert.InRange(glass, 10.5, 11.5);
        // The wall is cut along the curtain wall's 4 m.
        var t = Walls.Section(f.Model, f.Model.Get(wall)!).Sum(b => b.To - b.From);
        Assert.Equal(full - 4 * t * 3, f.Session.Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid)), 6);
    }

    [Fact]
    public void Its_type_changes_every_curtain_wall_of_it_and_a_grid_too_fine_is_refused()
    {
        var f = new Fixture();
        var cw = Add(f, 0, 0, 6, 0);
        var type = f.Model.RelatingOf(cw, IfcClass.RelDefinesByType)!.Id;
        var spec = CurtainSpec.Default.ToJson();
        spec["u"] = new JsonObject { ["mode"] = "count", ["count"] = 2 };
        spec["edge"] = false;
        spec["panel"] = new JsonObject { ["thickness"] = 0.03, ["kind"] = "opaque" };
        f.Run("architecture.updateCurtainWallType", new JsonObject { ["id"] = type, ["spec"] = spec.DeepClone() });
        var g = f.Session.Geometry(cw)!;
        Assert.Single(g.Parts, p => p.Key!.StartsWith("mullion:"));
        Assert.All(g.Parts.Where(p => p.Key!.StartsWith("panel:")), p => Assert.Equal("Panel", p.Name));
        spec["u"] = new JsonObject { ["mode"] = "count", ["count"] = 200 };
        Assert.Throws<CommandException>(() => f.Run("architecture.updateCurtainWallType", new JsonObject { ["id"] = type, ["spec"] = spec.DeepClone() }));
    }

    [Fact]
    public void A_curved_curtain_wall_is_straight_between_its_mullions()
    {
        var f = new Fixture();
        var cw = Add(f, 0, 0, 8, 0, new JsonObject { ["arc"] = 2 });
        var g = f.Session.Geometry(cw)!;
        Assert.Null(g.Diagnostics);
        var c = PathCurve.Of(f.Model.Get(cw)!);
        // Each mullion stands on the arc.
        foreach (var part in g.Parts.Where(p => p.Key!.StartsWith("mullion:")))
        {
            var b = Measure.Bounds(part.Solid)!.Value;
            Assert.True(c.Distance(new Vec2((b.Min.X + b.Max.X) / 2, (b.Min.Y + b.Max.Y) / 2)) < 0.03);
        }
        Assert.Equal(c.Length, f.Session.Quantities(cw)[0].Quantities.Single(x => x.Name == "Length").Value, 9);
    }
}
