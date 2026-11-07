using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A tester's report #114 („Please add wall opening edit window … If I want to change that opening to another type I must be able
/// to edit it, like how we edit doors and windows”): a wall opening's reveal finish and lintel, several at once, and an opening
/// made a door or a window and back, each one undo step (decision 392).
/// </summary>
public sealed class BareOpeningTests
{
    static Fixture F() => new(new ArchitectureModule());

    static string Opening(Fixture f, string wall, double along, double width = 1.0, double height = 2.1, double sill = 0, string? shape = null) =>
        (string)f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = along, ["width"] = width, ["height"] = height, ["sill"] = sill, ["shape"] = shape })!["id"]!;

    static GeometryPart? Lining(Fixture f, string wall, string opening) => f.Session.Geometry(wall)!.Parts.SingleOrDefault(p => p.Key == "lining:" + opening);

    static string TypeOf(Fixture f, string kind)
    {
        string? id = null;
        f.Session.Edit("Type", e => id = Openings.EnsureType(e, kind).Id);
        return id!;
    }

    static double Thickness(Fixture f, string wall)
    {
        var fr = Openings.FrameOf(f.Model, f.Model.Get(wall)!);
        return fr.YMax - fr.YMin;
    }

    [Fact]
    public void A_reveal_finish_lines_the_jambs_and_head_and_under_a_raised_opening()
    {
        var f = F();
        var wall = f.Wall(0, 0, 8, 0);
        var d = Thickness(f, wall);
        var passage = Opening(f, wall, 2);
        var hatch = Opening(f, wall, 5, 1.2, 1.0, sill: 0.9);
        Assert.Null(Lining(f, wall, passage));

        // Both at once, one step: plastered, 15 mm; the material made in the project.
        f.Run("architecture.updateWallOpening", new JsonObject { ["ids"] = new JsonArray(passage, hatch), ["reveal"] = "plaster" });
        Assert.NotNull(Materials.Find(f.Model, "Plaster"));
        const double t = 0.015;
        var onFloor = Lining(f, wall, passage)!;
        Assert.Equal(Materials.Find(f.Model, "Plaster")!.Id, onFloor.Material);
        // The jambs and the head; no finish on the floor.
        Assert.Equal((2 * 2.1 * t + (1.0 - 2 * t) * t) * d, Measure.Volume(onFloor.Solid), 5);
        // Above the floor, its bottom finished too.
        Assert.Equal((1.2 * 1.0 - (1.2 - 2 * t) * (1.0 - 2 * t)) * d, Measure.Volume(Lining(f, wall, hatch)!.Solid), 5);
        f.Session.Undo(f.Session.Author);
        Assert.Null(Lining(f, wall, passage));
        Assert.Null(Lining(f, wall, hatch));
        f.Session.Redo(f.Session.Author);

        // A lining of its own thickness and material; an arched head lined round its arch.
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = passage, ["reveal"] = "lining", ["revealThickness"] = 0.03, ["revealMaterial"] = "Oak" });
        Assert.NotNull(Materials.Find(f.Model, "Oak"));
        Assert.Equal((2 * 2.1 * 0.03 + (1.0 - 0.06) * 0.03) * d, Measure.Volume(Lining(f, wall, passage)!.Solid), 5);
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = passage, ["shape"] = "round" });
        var arched = Measure.Volume(Lining(f, wall, passage)!.Solid);
        Assert.InRange(arched, 0.5 * (2 * 2.1 * 0.03) * d, (2 * 2.1 * 0.03 + 1.0 * 0.03) * d);

        // Too thick for the opening, or of an unknown kind, refused; none takes it away.
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = hatch, ["revealThickness"] = 0.6 }));
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = hatch, ["reveal"] = "marble" }));
        // IFC: still openings voiding the wall, nothing filling them.
        using (var ms = new MemoryStream())
        {
            Lapin.Ifc.IfcExport.Write(f.Session, ms, "openings.ifc", new Lapin.Ifc.IfcExportOptions("IFC4", "group", null));
            var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            Assert.Equal(2, text.Split("IFCOPENINGELEMENT(").Length - 1);
            Assert.DoesNotContain("IFCRELFILLSELEMENT(", text);
        }
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = hatch, ["reveal"] = "none" });
        Assert.Null(Lining(f, wall, hatch));
        Assert.Null(f.Model.Get(hatch)!.Str("reveal"));
    }

    [Fact]
    public void An_opening_takes_the_walls_lintel_or_none()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0);
        var id = Opening(f, wall, 3, 1.2, 1.2, sill: 0.9);
        int Lintels() => f.Session.Geometry(wall)!.Parts.Count(p => p.Name.StartsWith("Lintel", StringComparison.Ordinal));
        var withRule = Lintels();
        Assert.True(withRule > 0);
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["lintel"] = "none" });
        Assert.Equal(0, Lintels());
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["lintel"] = "wall" });
        Assert.Equal(withRule, Lintels());
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["lintel"] = "steel" }));
    }

    [Fact]
    public void An_opening_becomes_a_door_or_a_window_and_back_keeping_its_place_and_globalid()
    {
        var f = F();
        var wall = f.Wall(0, 0, 8, 0);
        var id = Opening(f, wall, 2, 1.0, 2.2);
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["reveal"] = "plaster" });
        var doorType = TypeOf(f, "door");

        // Fitted: a type of its size, made from the one chosen and named by the size.
        var r = (JsonObject)f.Run("architecture.openingToFill", new JsonObject { ["id"] = id, ["typeId"] = doorType, ["fit"] = true })!;
        var door = (string)r["id"]!;
        Assert.Equal(id, Openings.OpeningOf(f.Model, door)!.Id);
        var spec = Openings.SpecOf(f.Model, door)!;
        Assert.Equal((1.0, 2.2), (spec.Width, spec.Height));
        Assert.Contains("1000×2200", spec.Name);
        Assert.NotEqual(doorType, (string)r["typeId"]!);
        Assert.Equal(2, f.Model.Get(id)!.Num("along"), 9);
        Assert.Null(f.Model.Get(id)!.Str("reveal"));
        Assert.Null(Lining(f, wall, id));
        Assert.Equal(IfcClass.Door, f.Model.Get(door)!.Class);
        Assert.NotNull(Spatial.StoreyOf(f.Model, door));
        // One step back: the opening as it was.
        f.Session.Undo(f.Session.Author);
        Assert.Null(f.Model.Get(door));
        Assert.Equal("plaster", f.Model.Get(id)!.Str("reveal"));
        f.Session.Redo(f.Session.Author);

        // Back to an opening of the door's hole, the opening's GlobalId kept, the door gone.
        var back = (JsonObject)f.Run("architecture.fillToOpening", new JsonObject { ["id"] = door })!;
        Assert.Equal(id, (string)back["ids"]![0]!);
        Assert.Null(f.Model.Get(door));
        var opening = f.Model.Get(id)!;
        Assert.Equal((1.0, 2.2, 2.0), (opening.Num("width"), opening.Num("height"), opening.Num("along")));
        Assert.Null(Openings.FillOf(f.Model, id));
        Assert.Equal(wall, Openings.WallOf(f.Model, id)!.Id);

        // A window of the type as it is, at the opening's sill: it must fit there.
        var windowType = TypeOf(f, "window");
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["sill"] = 0.9, ["height"] = 1.5, ["width"] = 1.2 });
        var window = (string)f.Run("architecture.openingToFill", new JsonObject { ["id"] = id, ["typeId"] = windowType })!["id"]!;
        Assert.Equal(windowType, f.Model.RelatingOf(window, IfcClass.RelDefinesByType)!.Id);
        Assert.Equal(0.9, f.Model.Get(id)!.Num("sill"), 9);
        Assert.Throws<CommandException>(() => f.Run("architecture.openingToFill", new JsonObject { ["id"] = id, ["typeId"] = windowType }));
    }
}
