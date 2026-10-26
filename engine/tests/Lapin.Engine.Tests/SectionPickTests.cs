using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>A section or an elevation picks what it draws: every element shown has a mark of its own lines (never drawn, only hit).</summary>
public class SectionPickTests
{
    [Fact]
    public void Walls_and_floors_in_a_section_and_an_elevation_carry_pick_marks()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var wall = f.Wall(0, 0, 6, 0);
        var other = f.Wall(0, 3, 6, 3);
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 3), new JsonArray(0, 3)) })!["id"]!;
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(3, -2), ["end"] = new JsonArray(3, 5), ["name"] = "A" })!["id"]!;
        var marks = Views.Drawing(f.Session.Reader, f.Model.Get(section)!)["marks"]!.AsArray().Select(m => (string)m!["id"]!).ToHashSet();
        Assert.Contains(wall, marks);
        Assert.Contains(other, marks);
        Assert.Contains(slab, marks);
        // The pick lines are not drawn: no pen on the PICK layer.
        var pens = Views.Drawing(f.Session.Reader, f.Model.Get(section)!)["pens"]!.AsArray().Select(p => (string?)p!["layer"]).ToList();
        Assert.DoesNotContain("PICK", pens);
        var elevation = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["start"] = new JsonArray(-2, -4), ["end"] = new JsonArray(8, -4), ["name"] = "South" })!["id"]!;
        var emarks = Views.Drawing(f.Session.Reader, f.Model.Get(elevation)!)["marks"]!.AsArray().Select(m => (string)m!["id"]!).ToHashSet();
        Assert.Contains(wall, emarks);
    }
}
