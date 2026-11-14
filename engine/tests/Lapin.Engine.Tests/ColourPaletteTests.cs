using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The project's colour palette (forum topic 97): kept on the project, one undo step per change.</summary>
public class ColourPaletteTests
{
    [Fact]
    public void The_palette_is_the_projects_cleaned_and_one_undo_step()
    {
        var f = new Fixture(new ArchitectureModule());
        Assert.Empty(ColourPalette.Of(f.Session.Model));
        f.Run("project.setColourPalette", new JsonObject { ["colours"] = new JsonArray("#B5532D", "#b5532d", "#112233") });
        Assert.Equal(["#b5532d", "#112233"], ColourPalette.Of(f.Session.Model));
        Assert.Equal(2, ((JsonArray)f.Session.Snapshot().Nodes.Single(n => n.Type == "IfcProject").Props["colourPalette"]!).Count);
        Assert.Throws<CommandException>(() => f.Run("project.setColourPalette", new JsonObject { ["colours"] = new JsonArray("red") }));
        f.Run("project.setColourPalette", new JsonObject { ["colours"] = new JsonArray() });
        Assert.Empty(ColourPalette.Of(f.Session.Model));
        f.Session.Undo();
        Assert.Equal(2, ColourPalette.Of(f.Session.Model).Count);
        f.Session.Undo();
        Assert.Empty(ColourPalette.Of(f.Session.Model));
    }
}
