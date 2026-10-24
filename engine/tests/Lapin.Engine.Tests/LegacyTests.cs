using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Host;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Old Lapin projects (format 2) open: the user's test model (two storeys,
/// three walls of a layered type, a floor, a column) rebuilt natively.
/// </summary>
public class LegacyTests
{
    static string Old => Path.Combine(AppContext.BaseDirectory, "Fixtures", "old-format2.lapin");

    [Fact]
    public void An_old_package_is_told_from_format_3_and_read_as_rows()
    {
        Assert.True(LegacyPackage.Is(Old));
        var old = LegacyPackage.Read(Old);
        Assert.Equal(["Ground Floor", "Level 1"], old.Storeys.Select(s => (string?)s["name"]));
        Assert.Equal(3, old.Of("wall").Count());
        Assert.Single(old.Of("slab"));
        Assert.Single(old.Of("column"));
        Assert.Equal("wall", (string?)old.Types.Single()["kind"]);
    }

    [Fact]
    public void An_old_project_opens_as_a_native_one_unsaved_and_without_undo()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new DraftingModule());
        var r = LegacyImport.Run(f.Session, LegacyPackage.Read(Old));
        var m = f.Model;
        Assert.Equal([("Ground Floor", 0.0, 3.0), ("Level 1", 3.0, 3.0)], Spatial.Storeys(m).Select(s => (s.Name, s.Num("elevation"), s.Num("height"))));
        var walls = m.OfClass(IfcClass.Wall).ToList();
        Assert.Equal(3, walls.Count);
        // The layered type: render 20, brick 250 (the core), plaster 15.
        var type = Walls.TypeOf(m, walls[0].Id)!;
        Assert.Equal("Wall WT1", type.Name);
        Assert.Equal(0.285, Walls.Section(m, walls[0]).Sum(b => b.To - b.From), 9);
        Assert.Equal("Brick", m.Get(Walls.Section(m, walls[0]).Single(b => b.Core).Material!)!.Name);
        // The walls meet: joined at their corners, as drawn now.
        Assert.Contains(m.Relations, x => x.Class == Joins.Class);
        Assert.Single(m.OfClass(IfcClass.Slab));
        Assert.Single(m.OfClass(IfcClass.Column));
        Assert.Equal(0.4, Lapin.Structure.Section.From(m.OfClass(IfcClass.ColumnType).Single().Attrs["section"])!.Width, 9);
        Assert.False(f.Session.History.CanUndo);
        Assert.Null(f.Session.Path);
        Assert.All(r.Notes, n => Assert.DoesNotContain("could not", n));
    }
}
