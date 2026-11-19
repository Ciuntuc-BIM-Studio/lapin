using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 177 (decision 731): a type's mark (a wall's, a floor's, a column's… as a door's or window's has) and an element's
/// own, set with element.setMark in one undo step. A tag's {mark} reads the element's own, else its type's, else its type's
/// name; {TypeMark} and {type.mark} the type's alone. IFC writes the type's mark as the Pset_…Common Reference and the
/// element's own as its Tag.
/// </summary>
public sealed class ElementMarkTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-mark-").FullName;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    static (Fixture F, string Type, string Wall) Walled()
    {
        var f = new Fixture();
        var type = f.Type("Porotherm 240", ("Brick", 0.24, true));
        var wall = f.Wall(0, 0, 5, 0, type, height: 3);
        return (f, type, wall);
    }

    static string Field(Fixture f, string id, string field) => TagText.Value(f.Model, f.Model.Get(id)!, field);

    [Fact]
    public void A_wall_types_mark_is_set_in_one_step_and_its_walls_tags_read_it()
    {
        var (f, type, wall) = Walled();
        // No mark yet: the wall's {mark} is its type's name, as before; {TypeMark} is empty.
        Assert.Equal("Porotherm 240", Field(f, wall, "mark"));
        Assert.Equal("", Field(f, wall, "TypeMark"));
        f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(type), ["mark"] = "  W1 " });
        Assert.Equal("W1", f.Model.Get(type)!.Str("mark"));
        Assert.Equal("W1", Field(f, wall, "mark"));
        Assert.Equal("W1", Field(f, wall, "TypeMark"));
        Assert.Equal("W1", Field(f, wall, "type.mark"));
        Assert.Equal("W1", TagText.Fill("{TypeMark}-{name}", f.Model, f.Model.Get(wall)!).Split('-')[0]);
        // One undo step.
        f.Session.Undo();
        Assert.Null(f.Model.Get(type)!.Str("mark"));
        f.Session.Redo();
        Assert.Equal("W1", f.Model.Get(type)!.Str("mark"));
        // Saving the type in its editor keeps it.
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["name"] = "Porotherm 240 N" });
        Assert.Equal("W1", f.Model.Get(type)!.Str("mark"));
        // Cleared: the type's name again.
        f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(type), ["mark"] = "" });
        Assert.Null(f.Model.Get(type)!.Str("mark"));
        Assert.Equal("Porotherm 240 N", Field(f, wall, "mark"));
    }

    [Fact]
    public void A_walls_own_mark_goes_before_its_types()
    {
        var (f, type, wall) = Walled();
        var other = f.Wall(0, 4, 5, 4, type, height: 3);
        f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(type), ["mark"] = "W1" });
        f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(wall), ["mark"] = "W1.03" });
        Assert.Equal("W1.03", Field(f, wall, "mark"));
        Assert.Equal("W1", Field(f, wall, "TypeMark"));
        Assert.Equal("W1", Field(f, other, "mark"));
    }

    [Fact]
    public void A_door_types_mark_follows_into_its_spec()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0, height: 3);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 })!["id"]!;
        var type = f.Model.RelatingOf(door, IfcClass.RelDefinesByType)!.Id;
        f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(type), ["mark"] = "D7" });
        var t = f.Model.Get(type)!;
        Assert.Equal("D7", t.Str("mark"));
        Assert.Equal("D7", (string?)t.Attrs["spec"]!["mark"]);
        Assert.Equal("D7", Field(f, door, "mark"));
    }

    [Fact]
    public void Storeys_annotations_and_unknown_ids_have_no_mark()
    {
        var (f, _, _) = Walled();
        Assert.Throws<CommandException>(() => f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(f.Storey), ["mark"] = "S" }));
        Assert.Throws<CommandException>(() => f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray("nope"), ["mark"] = "S" }));
        Assert.Throws<CommandException>(() => f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(), ["mark"] = "S" }));
    }

    [Fact]
    public void Ifc_writes_the_types_mark_as_its_reference_and_the_walls_own_as_its_tag()
    {
        var (f, type, wall) = Walled();
        var other = f.Wall(0, 4, 5, 4, type, height: 3);
        f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(type), ["mark"] = "W1" });
        f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(wall), ["mark"] = "W1.03" });
        var path = Path.Combine(_dir, "marks.ifc");
        IfcExport.Write(f.Session, path);
        var file = StepFile.Read(File.ReadAllBytes(path));
        var text = File.ReadAllText(path);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('Reference',$,IFCIDENTIFIER('W1'),$)", text);
        Assert.DoesNotContain("IFCPROPERTYSINGLEVALUE('Reference',$,IFCIDENTIFIER('Porotherm 240'),$)", text);
        var walls = file.OfType("IFCWALL").ToList();
        Assert.Contains(walls, w => (string?)w[7] == "W1.03");
        // A wall without a mark of its own keeps its id as its tag, as before.
        Assert.Contains(walls, w => (string?)w[7] == other);
    }
}
