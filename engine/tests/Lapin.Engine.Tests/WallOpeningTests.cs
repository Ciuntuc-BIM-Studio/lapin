using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>An opening alone in a wall, nothing filling it (a tester's report): it voids the wall, is checked as a door is, resized and moved, and written to IFC as an opening without a door or window.</summary>
public sealed class WallOpeningTests
{
    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void An_opening_alone_cuts_its_wall_fits_as_a_door_does_and_is_written_as_an_opening()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 6, 0);
        var full = Volume(f, wall);
        var thickness = full / (6 * f.Model.Get(wall)!.Num("height", Walls.DefaultHeight));
        var id = (string)f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["x"] = 2, ["y"] = 0.05, ["width"] = 1.2, ["height"] = 2.2 })!["id"]!;
        var opening = f.Model.Get(id)!;
        Assert.Equal(IfcClass.OpeningElement, opening.Class);
        Assert.Null(Openings.FillOf(f.Model, id));
        Assert.Equal(2, opening.Num("along"), 9);
        Assert.Equal(full - 1.2 * 2.2 * thickness, Volume(f, wall), 6);

        // Checked as a door is: past the wall's end, or over another opening, refused.
        Assert.Throws<CommandException>(() => f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 5.8, ["width"] = 1 }));
        Assert.Throws<CommandException>(() => f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 2.5, ["width"] = 1 }));
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 4.5 })!["id"]!;

        // Resized and raised to a window's place; moved onto the door, or made too wide for the wall, refused, the opening as it was.
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["width"] = 0.9, ["height"] = 1.0, ["sill"] = 0.8 });
        Assert.Equal(0.9, f.Model.Get(id)!.Num("width"), 9);
        Assert.Equal(0.8, f.Model.Get(id)!.Num("sill"), 9);
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["along"] = 4.3 }));
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["width"] = 4.5 }));
        Assert.Equal(2, f.Model.Get(id)!.Num("along"), 9);
        Assert.Equal(0.9, f.Model.Get(id)!.Num("width"), 9);
        // A door's opening takes its size from the door's type.
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = Openings.OpeningOf(f.Model, door)!.Id, ["width"] = 1.5 }));

        // In IFC: two openings voiding the wall, one door filling one of them.
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "opening.ifc", new IfcExportOptions("IFC4", "group", null));
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(2, ifc.Split("IFCOPENINGELEMENT(").Length - 1);
        Assert.Equal(2, ifc.Split("IFCRELVOIDSELEMENT(").Length - 1);
        Assert.Equal(1, ifc.Split("IFCRELFILLSELEMENT(").Length - 1);
    }

    /// <summary>The area a bare opening takes out of the wall's face: the volume it removes over the wall's thickness.</summary>
    static double CutArea(Fixture f, string wall, double full, double thickness) => (full - Volume(f, wall)) / thickness;

    [Theory]
    // A semicircle (the "round" kind) on 1.0 × 2.1: the rectangle up to the spring line and half a circle of 0.5.
    [InlineData("round", null, 1.0, 2.1, 1.0 * 1.6 + Math.PI * 0.25 / 2)]
    // A segment rising 0.2 over 1.2: its circle's radius 1.0, the segment acos(0.8) − 0.8·0.6.
    [InlineData("segmental", 0.2, 1.2, 2.1, 1.2 * 1.9 + 0.6435011087932844 - 0.48)]
    // A half ellipse, its rise a quarter of the width unless asked.
    [InlineData("elliptical", null, 1.2, 2.1, 1.2 * 1.8 + Math.PI * 0.6 * 0.3 / 2)]
    [InlineData("elliptical", 0.5, 1.6, 2.2, 1.6 * 1.7 + Math.PI * 0.8 * 0.5 / 2)]
    // Rounded top corners of 150 mm unless asked: the rectangle less each corner's square outside its quarter circle.
    [InlineData("radiused", null, 1.0, 2.1, 1.0 * 2.1 - 2 * (1 - Math.PI / 4) * 0.15 * 0.15)]
    [InlineData("radiused", 0.3, 1.2, 2.1, 1.2 * 2.1 - 2 * (1 - Math.PI / 4) * 0.3 * 0.3)]
    // A corner radius more than half the width is held at half: a semicircle.
    [InlineData("radiused", 2.0, 1.0, 2.1, 1.0 * 1.6 + Math.PI * 0.25 / 2)]
    [InlineData("rect", null, 1.0, 2.1, 1.0 * 2.1)]
    public void An_opening_with_a_shaped_head_takes_its_outline_out_of_the_wall(string kind, double? rise, double width, double height, double area)
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 6, 0);
        var full = Volume(f, wall);
        var thickness = full / (6 * f.Model.Get(wall)!.Num("height", Walls.DefaultHeight));
        var args = new JsonObject { ["wallId"] = wall, ["along"] = 3, ["width"] = width, ["height"] = height, ["shape"] = kind };
        if (rise != null) args["rise"] = rise;
        var id = (string)f.Run("architecture.addWallOpening", args)!["id"]!;
        Assert.InRange(CutArea(f, wall, full, thickness), area - 0.002, area + 0.002);
        Assert.Equal(kind == "rect" ? null : kind, f.Model.Get(id)!.Str("shape"));
        // The IFC opening's body (as the export takes it) is the same outline, through the wall and 50 mm past each face.
        var body = Openings.CutterIn(f.Model, f.Model.Get(wall)!, Openings.Place(f.Model, f.Model.Get(id)!))!;
        Assert.InRange(Measure.Volume(body), (area - 0.002) * (thickness + 0.1), (area + 0.002) * (thickness + 0.1));
    }

    [Fact]
    public void An_openings_head_is_changed_refused_when_unknown_and_its_rise_goes_back_to_the_kinds_own()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 6, 0);
        var full = Volume(f, wall);
        var thickness = full / (6 * f.Model.Get(wall)!.Num("height", Walls.DefaultHeight));
        var id = (string)f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["width"] = 1.0, ["height"] = 2.1 })!["id"]!;
        Assert.Null(f.Model.Get(id)!.Str("shape"));
        Assert.Equal(2.1, CutArea(f, wall, full, thickness), 6);

        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["shape"] = "round" });
        Assert.InRange(CutArea(f, wall, full, thickness), 1.6 + Math.PI / 8 - 0.002, 1.6 + Math.PI / 8 + 0.002);
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["shape"] = "radiused", ["rise"] = 0.2 });
        Assert.Equal(0.2, f.Model.Get(id)!.Num("rise"), 9);
        var radiused = 2.1 - 2 * (1 - Math.PI / 4) * 0.04;
        Assert.InRange(CutArea(f, wall, full, thickness), radiused - 0.002, radiused + 0.002);
        // The rise back to the kind's own (150 mm).
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["rise"] = null });
        Assert.Null(f.Model.Get(id)!.NumOrNull("rise"));
        var standard = 2.1 - 2 * (1 - Math.PI / 4) * 0.0225;
        Assert.InRange(CutArea(f, wall, full, thickness), standard - 0.002, standard + 0.002);

        // An unknown kind, or a rise of nothing, refused; the opening as it was.
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["shape"] = "star" }));
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["rise"] = 0 }));
        Assert.Throws<CommandException>(() => f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 1, ["width"] = 0.8, ["shape"] = "pointed" }));
        Assert.Equal("radiused", f.Model.Get(id)!.Str("shape"));

        // Back to a rectangle: neither the kind nor the rise kept.
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["shape"] = "rect" });
        Assert.Null(f.Model.Get(id)!.Str("shape"));
        Assert.Equal(2.1, CutArea(f, wall, full, thickness), 6);

        // A door's opening takes its shape from the door's type.
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1 })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = Openings.OpeningOf(f.Model, door)!.Id, ["shape"] = "round" }));
    }
}
