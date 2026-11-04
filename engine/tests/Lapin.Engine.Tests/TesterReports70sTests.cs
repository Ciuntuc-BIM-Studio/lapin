using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Testers' reports #73 (the program froze while the levels were edited) and #74 (Side flipped only the first of three
/// selected windows).
/// </summary>
public sealed class TesterReports70sTests
{
    static Fixture House(out string ground, out string first, out string stair)
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Structure.StructureModule());
        f.Session.New();
        var storeys = Spatial.Storeys(f.Model).ToList();
        ground = storeys.Single(s => s.Name == "Ground floor").Id;
        first = storeys.Single(s => s.Name == "First floor").Id;
        foreach (var (a, b, c, d) in new[] { (0.0, 0.0, 10.0, 0.0), (10, 0, 10, 6), (10, 6, 0, 6), (0, 6, 0, 0) })
            f.Run("architecture.addWall", new JsonObject { ["storeyId"] = ground, ["x1"] = a, ["y1"] = b, ["x2"] = c, ["y2"] = d });
        stair = (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = ground, ["x"] = 1, ["y"] = 1, ["x2"] = 2, ["y2"] = 1 })!["id"]!;
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = ground, ["x"] = 5, ["y"] = 3 });
        f.Session.Snapshot();
        return f;
    }

    [Fact]
    public void A_level_typed_kilometres_away_leaves_the_stair_under_it_bounded_and_saying_so()
    {
        var f = House(out _, out var first, out var stair);
        // 3000 m for 3000 mm: the stair under it asked some 17,600 risers and the program froze.
        var clock = Stopwatch.StartNew();
        f.Run("storey.update", new JsonObject { ["id"] = first, ["elevation"] = 3000 });
        f.Session.Snapshot();
        Assert.True(clock.Elapsed.TotalSeconds < 20, $"{clock.Elapsed.TotalSeconds:0.0} s");
        var resolved = StairBuilder.Resolve(f.Model, f.Model.Get(stair)!)!.Value.Resolved;
        Assert.True(resolved.Risers <= StairSolver.MaxRisers, $"{resolved.Risers} risers");
        Assert.Contains(f.Session.Geometry(stair)!.Diagnostics!, d => d.Contains("would climb 3000 m"));
        // Back where it was: as before, and nothing to say.
        f.Run("storey.update", new JsonObject { ["id"] = first, ["elevation"] = 3 });
        Assert.Equal(StairSolver.Resolve(StairBuilder.SpecOf(f.Model, f.Model.Get(stair)!), 3).Risers, StairBuilder.Resolve(f.Model, f.Model.Get(stair)!)!.Value.Resolved.Risers);
        Assert.DoesNotContain(f.Session.Geometry(stair)!.Diagnostics ?? [], d => d.Contains("would climb"));
        // A count typed by hand is held too.
        Assert.Equal(StairSolver.MaxRisers, StairSolver.Resolve(StairBuilder.SpecOf(f.Model, f.Model.Get(stair)!), 3, new StairInputs(Risers: 100_000)).Risers);
    }

    [Fact]
    public void A_storey_too_high_or_not_a_number_is_refused_and_left_as_it_was()
    {
        var f = House(out var ground, out _, out _);
        Assert.Throws<CommandException>(() => f.Run("storey.update", new JsonObject { ["id"] = ground, ["height"] = 3000 }));
        Assert.Throws<CommandException>(() => f.Run("storey.update", new JsonObject { ["id"] = ground, ["top"] = 500 }));
        Assert.Throws<CommandException>(() => f.Run("storey.update", new JsonObject { ["id"] = ground, ["elevation"] = "NaN" }));
        Assert.Throws<CommandException>(() => f.Run("storey.update", new JsonObject { ["id"] = ground, ["elevation"] = 1e9 }));
        Assert.Throws<CommandException>(() => f.Run("storey.update", new JsonObject { ["id"] = ground, ["height"] = 0 }));
        Assert.Throws<CommandException>(() => f.Run("storey.create", new JsonObject { ["height"] = 1e6 }));
        Assert.Equal(0, f.Model.Get(ground)!.Num("elevation"));
        Assert.Equal(3, f.Model.Get(ground)!.Num("height"));
        // A tall hall is fine; so is a level below the others.
        f.Run("storey.update", new JsonObject { ["id"] = ground, ["height"] = 24 });
        f.Run("storey.update", new JsonObject { ["id"] = ground, ["elevation"] = -40, ["keepTop"] = true });
        Assert.Equal(64, f.Model.Get(ground)!.Num("height"), 6);
    }

    [Fact]
    public void Flipping_several_doors_and_windows_flips_them_all_in_one_step()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 12, 0);
        var windows = new[] { 2.0, 5.0, 8.0 }.Select(x => (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = x })!["id"]!).ToList();
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 10.5 })!["id"]!;
        var all = new JsonArray([.. windows.Append(door).Select(id => (JsonNode)id)]);
        f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = all.DeepClone(), ["axis"] = "y" });
        Assert.All(windows.Append(door), id => Assert.True(f.Model.Get(id)!.Bool("flipSide")));
        f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = all.DeepClone(), ["axis"] = "x" });
        Assert.All(windows.Append(door), id => Assert.True(f.Model.Get(id)!.Bool("flipHand")));
        // One undo step takes back the hands of all four, the sides stay.
        f.Session.Undo();
        Assert.All(windows.Append(door), id => Assert.False(f.Model.Get(id)!.Bool("flipHand")));
        Assert.All(windows.Append(door), id => Assert.True(f.Model.Get(id)!.Bool("flipSide")));

        // The sill of every selected window at once.
        f.Run("architecture.moveOpening", new JsonObject { ["ids"] = new JsonArray([.. windows.Select(id => (JsonNode)id)]), ["sill"] = 1.1 });
        Assert.All(windows, id => Assert.Equal(1.1, Openings.Place(f.Model, Openings.OpeningOf(f.Model, id)!).Sill, 9));
    }

    [Fact]
    public void Flipping_several_walls_and_changing_their_type_goes_to_them_all()
    {
        var f = new Fixture(new ArchitectureModule());
        var walls = new[] { f.Wall(0, 0, 5, 0), f.Wall(0, 3, 5, 3), f.Wall(0, 6, 5, 6) };
        var ids = new JsonArray([.. walls.Select(id => (JsonNode)id)]);
        f.Run("architecture.flipWall", new JsonObject { ["ids"] = ids.DeepClone() });
        Assert.All(walls, id => Assert.True(Layers.ReadUsage(LayerSets.UsageOf(f.Model, id)!) is { Negative: true }));
        var type = f.Type("Thin", ("Brick", 0.115, true));
        f.Run("architecture.setWallType", new JsonObject { ["ids"] = ids.DeepClone(), ["typeId"] = type });
        Assert.All(walls, id => Assert.Equal(type, f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!.Id));
    }
}
