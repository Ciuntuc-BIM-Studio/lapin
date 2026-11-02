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
}
