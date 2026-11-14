using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The forum's topic 64 (a structural engineer's): „let the opening — a simple void, or one made by a door/window — be defined only
/// partially in an element's profile … a door which starts in a reinforced concrete wall and goes through the upper masonry part”.
/// An opening cuts every wall along its host its box passes through, worked out on every build; one moved out of a wall no longer
/// cuts it; IFC has an opening element for each wall voided; walls recognised from a file are left as the file has them.
/// </summary>
public sealed class ThroughOpeningTests
{
    static Fixture F() => new(new ArchitectureModule());

    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    /// <summary>A straight wall of its own height from a base offset, not joined to the others.</summary>
    static string Wall(Fixture f, string type, double x1, double y1, double x2, double y2, double height, double baseOffset = 0) =>
        (string)f.Run("architecture.addWall", new JsonObject
        {
            ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["height"] = height, ["baseOffset"] = baseOffset, ["join"] = false,
        })!["id"]!;

    static (Fixture F, string Concrete, string Masonry) Stacked(double masonryTo = 6)
    {
        var f = F();
        var rc = f.Type("RC 250", ("Reinforced concrete", 0.25, true));
        var brick = f.Type("Brick 250", ("Brick", 0.25, true));
        // A concrete wall 1 m high, a masonry wall on it from 1 m to 3 m.
        return (f, Wall(f, rc, 0, 0, 6, 0, 1.0), Wall(f, brick, 0, 0, masonryTo, 0, 2.0, 1.0));
    }

    static string Door(Fixture f, string wall, double along) => (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = along, ["sill"] = 0 })!["id"]!;

    [Fact]
    public void A_door_from_a_concrete_wall_cuts_the_masonry_on_it_too()
    {
        var (f, rc, brick) = Stacked();
        Assert.Equal(6 * 1.0 * 0.25, Volume(f, rc), 6);
        Assert.Equal(6 * 2.0 * 0.25, Volume(f, brick), 6);

        // 2.1 m high, from the floor: it runs out of the concrete (1 m) into the masonry, so it fits the two together.
        var door = Door(f, rc, 3);
        var spec = Openings.SpecOf(f.Model, door)!;
        Assert.Equal(2.1, spec.Height, 9);
        var w = spec.Width;
        Assert.Equal(6 * 1.0 * 0.25 - w * 1.0 * 0.25, Volume(f, rc), 6);
        // The masonry gives up the door's 1.1 m over its base (its lintel a part of it: the body as a whole loses the hole alone).
        Assert.Equal(6 * 2.0 * 0.25 - w * 1.1 * 0.25, Volume(f, brick), 6);
        Assert.Contains(f.Session.Geometry(brick)!.Parts, p => p.Name.StartsWith("Lintel", StringComparison.Ordinal));
        // In plan (cut at 1.2 m, in the masonry), the masonry shows the door's gap: a piece either side of it.
        var cuts = f.Session.Geometry(brick)!.Plan!.Where(p => p.Kind == "cut").ToList();
        Assert.Equal(2, cuts.Count);
        Assert.Equal(3 - w / 2, cuts.Min(c => c.Points.Max(q => q.X)), 6);
        Assert.Equal(3 + w / 2, cuts.Max(c => c.Points.Min(q => q.X)), 6);
        // Still one door, one opening, hosted by the concrete: which walls it cuts is worked out, not stored.
        Assert.Single(f.Model.OfClass(IfcClass.OpeningElement));
        Assert.Equal(rc, Openings.WallOf(f.Model, Openings.OpeningOf(f.Model, door)!.Id)!.Id);

        // Moved along: the masonry is cut where it is now.
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = door, ["along"] = 4.5 });
        cuts = f.Session.Geometry(brick)!.Plan!.Where(p => p.Kind == "cut").ToList();
        Assert.Equal(4.5 - w / 2, cuts.Min(c => c.Points.Max(q => q.X)), 6);
        Assert.Equal(6 * 2.0 * 0.25 - w * 1.1 * 0.25, Volume(f, brick), 6);

        // Cutting its own wall alone, it no longer fits it: refused, as it was.
        Assert.Throws<CommandException>(() => f.Run("architecture.setCutThrough", new JsonObject { ["id"] = door, ["on"] = false }));
        Assert.Equal(6 * 2.0 * 0.25 - w * 1.1 * 0.25, Volume(f, brick), 6);
    }

    [Fact]
    public void The_masonry_moved_off_the_door_is_no_longer_cut()
    {
        var (f, rc, brick) = Stacked(masonryTo: 3);
        var door = Door(f, rc, 1.5);
        var w = Openings.SpecOf(f.Model, door)!.Width;
        Assert.Equal(3 * 2.0 * 0.25 - w * 1.1 * 0.25, Volume(f, brick), 6);
        // Past the masonry's end the door would stand above the concrete in nothing: refused.
        Assert.Throws<CommandException>(() => f.Run("architecture.moveOpening", new JsonObject { ["id"] = door, ["along"] = 4.5 }));
        // The masonry moved along, off the door: whole again (worked out on its build, nothing stored to undo).
        f.Run("architecture.updateWall", new JsonObject { ["id"] = brick, ["x1"] = 3.0, ["x2"] = 6.0 });
        Assert.Equal(3 * 2.0 * 0.25, Volume(f, brick), 6);
        f.Session.Undo(f.Session.Author);
        Assert.Equal(3 * 2.0 * 0.25 - w * 1.1 * 0.25, Volume(f, brick), 6);
    }

    [Fact]
    public void An_opening_in_a_wall_cuts_the_lining_against_it_and_not_once_moved_off_it()
    {
        var f = F();
        var core = f.Type("Block 250", ("Concrete block", 0.25, true));
        var lining = f.Type("Lining 100", ("Brick", 0.1, true));
        var wall = Wall(f, core, 0, 0, 6, 0, 3.0);
        // A lining of 10 cm against its left face (its axis 5 cm off that face), along the first 3 m.
        var y = Walls.Section(f.Model, f.Model.Get(wall)!).Max(b => b.To) + 0.05;
        var skin = Wall(f, lining, 0, y, 3, y, 3.0);
        Assert.Equal(3 * 3.0 * 0.1, Volume(f, skin), 6);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 })!["id"]!;
        var spec = Openings.SpecOf(f.Model, window)!;
        Assert.Equal(3 * 3.0 * 0.1 - spec.Width * spec.Height * 0.1, Volume(f, skin), 6);
        Assert.Equal(6 * 3.0 * 0.25 - spec.Width * spec.Height * 0.25, Volume(f, wall), 6);

        // Moved along the wall past the lining's end: the lining whole again.
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = window, ["along"] = 4.5 });
        Assert.Equal(3 * 3.0 * 0.1, Volume(f, skin), 6);
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = window, ["along"] = 1.5 });
        Assert.Equal(3 * 3.0 * 0.1 - spec.Width * spec.Height * 0.1, Volume(f, skin), 6);

        // Its own wall alone: the lining whole; one undo step back.
        f.Run("architecture.setCutThrough", new JsonObject { ["ids"] = new JsonArray(window), ["on"] = false });
        Assert.Equal(3 * 3.0 * 0.1, Volume(f, skin), 6);
        Assert.False(f.Model.Get(Openings.OpeningOf(f.Model, window)!.Id)!.Bool(ThroughOpenings.Key));
        f.Session.Undo(f.Session.Author);
        Assert.Equal(3 * 3.0 * 0.1 - spec.Width * spec.Height * 0.1, Volume(f, skin), 6);

        // A lining standing off the wall (1 cm) is not cut.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = skin, ["y1"] = y + 0.01, ["y2"] = y + 0.01 });
        Assert.Equal(3 * 3.0 * 0.1, Volume(f, skin), 6);
    }

    [Fact]
    public void A_wall_moved_against_another_is_cut_by_its_openings_the_other_built_again()
    {
        var f = F();
        var core = f.Type("Block 250", ("Concrete block", 0.25, true));
        var lining = f.Type("Lining 100", ("Brick", 0.1, true));
        var skin = Wall(f, lining, 0, 0.175, 3, 0.175, 3.0);
        // The wall with the window starts 1 m off the lining, then is moved against it: the lining (unchanged itself) is built again, cut.
        var wall = Wall(f, core, 0, -1, 6, -1, 3.0);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 })!["id"]!;
        var spec = Openings.SpecOf(f.Model, window)!;
        Assert.Equal(3 * 3.0 * 0.1, Volume(f, skin), 6);
        var diff = f.RunDiff("architecture.updateWall", new JsonObject { ["id"] = wall, ["y1"] = 0.0, ["y2"] = 0.0 });
        Assert.Contains(diff.Render, r => r.NodeId == skin);
        Assert.Equal(3 * 3.0 * 0.1 - spec.Width * spec.Height * 0.1, Volume(f, skin), 6);
    }

    [Fact]
    public void Walls_that_take_no_opening_through_them_keep_their_bodies()
    {
        var f = F();
        var core = f.Type("Block 250", ("Concrete block", 0.25, true));
        // A wall on the storey above the door's head, two walls meeting at a corner by the door, a wall in line with the host.
        var wall = Wall(f, core, 0, 0, 6, 0, 3.0);
        var above = Wall(f, core, 0, 0, 6, 0, 1.0, 3.0);
        var across = Wall(f, core, 0.125, 0.125, 0.125, 4, 3.0);
        var inLine = Wall(f, core, 6, 0, 9, 0, 3.0);
        var before = new[] { above, across, inLine }.ToDictionary(id => id, id => Volume(f, id));
        Door(f, wall, 1.0);
        Door(f, wall, 5.5);
        foreach (var (id, v) in before) Assert.Equal(v, Volume(f, id), 9);
    }

    [Fact]
    public void Ifc_writes_an_opening_element_for_each_wall_it_voids_with_stable_globalids()
    {
        var (f, rc, brick) = Stacked();
        var door = Door(f, rc, 3);
        string Export()
        {
            using var ms = new MemoryStream();
            IfcExport.Write(f.Session, ms, "through.ifc", new IfcExportOptions("IFC4", "group", null));
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        var ifc = Export();
        Assert.Equal(2, ifc.Split("IFCOPENINGELEMENT(").Length - 1);
        Assert.Equal(2, ifc.Split("IFCRELVOIDSELEMENT(").Length - 1);
        Assert.Equal(1, ifc.Split("IFCRELFILLSELEMENT(").Length - 1);
        // The masonry's: voiding it (its GlobalId on the relationship), the same at every export.
        var lines = ifc.Split('\n');
        var openingLines = lines.Where(l => l.Contains("IFCOPENINGELEMENT(", StringComparison.Ordinal)).ToList();
        Assert.Contains(openingLines, l => l.Contains("'Through ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("IFCRELVOIDSELEMENT(", StringComparison.Ordinal) && RefOf(lines, brick) is { } b && l.Contains(b + ",", StringComparison.Ordinal));
        Assert.Equal(openingLines.Select(GlobalIdOf), Export().Split('\n').Where(l => l.Contains("IFCOPENINGELEMENT(", StringComparison.Ordinal)).Select(GlobalIdOf));
    }

    static string GlobalIdOf(string line) => line.Split('\'')[1];

    /// <summary>The STEP reference (#n) of the entity whose GlobalId is <paramref name="id"/>.</summary>
    static string? RefOf(string[] lines, string id) => lines.FirstOrDefault(l => l.Contains($"('{id}'", StringComparison.Ordinal))?.Split('=')[0].Trim();

    [Fact]
    public void A_lapin_file_comes_back_with_the_same_bodies_and_a_foreign_wall_keeps_the_files_voids()
    {
        var (f, rc, brick) = Stacked();
        var door = Door(f, rc, 3);
        var w = Openings.SpecOf(f.Model, door)!.Width;
        var dir = Directory.CreateTempSubdirectory("lapin-through").FullName;
        try
        {
            var path = Path.Combine(dir, "through.ifc");
            IfcExport.Write(f.Session, path);
            var g = F();
            IfcImport.Run(g.Session, path, null, out _);
            // The walls come back with the same bodies: the masonry's hole is the file's (its own opening), not cut twice.
            Assert.Equal(6 * 1.0 * 0.25 - w * 1.0 * 0.25, Volume(g, rc), 4);
            Assert.Equal(6 * 2.0 * 0.25 - w * 1.1 * 0.25, Volume(g, brick), 4);
            // Lapin's own file comes back as Lapin's model: the door's one opening in the concrete, the masonry's cut worked out again
            // (the second opening element is the export's, derived, not read back as an opening of its own).
            Assert.Single(g.Model.OfClass(IfcClass.OpeningElement));
            Assert.Single(ThroughOpenings.Into(g.Model, g.Model.Get(brick)!));
            // Written again: the same two opening elements, under the same GlobalIds.
            var again = Path.Combine(dir, "again.ifc");
            IfcExport.Write(g.Session, again);
            static List<string> Openings(string file) => File.ReadAllLines(file).Where(l => l.Contains("IFCOPENINGELEMENT(", StringComparison.Ordinal)).Select(GlobalIdOf).Order().ToList();
            Assert.Equal(2, Openings(again).Count);
            Assert.Equal(Openings(path), Openings(again));

            // Walls recognised from a foreign file (their import record) keep the file's voids: no opening of another's cuts them.
            f.Session.Edit("Recognised", e =>
            {
                foreach (var id in new[] { rc, brick })
                {
                    var wall = e.Model.Get(id)!;
                    var attrs = wall.CloneAttrs();
                    attrs["imported"] = new JsonObject { ["ifcClass"] = "IfcWall" };
                    e.Replace(wall.With(attrs: attrs));
                }
            });
            Assert.Empty(ThroughOpenings.Into(f.Model, f.Model.Get(brick)!));
            Assert.Equal(6 * 2.0 * 0.25, Volume(f, brick), 6);
        }
        finally { Directory.Delete(dir, true); }
    }
}
