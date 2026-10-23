using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Openings, doors and windows in walls. The zone cases with a door are
/// ported from the old Lapin's wall-zones.test.ts (same numbers); the rest
/// follows joinery.e2e.test.ts (placing by type, refitting, flipping).
/// </summary>
public class OpeningTests
{
    static double Vol(Fixture f, string id, string? key = null) =>
        f.Session.Geometry(id)!.Parts.Where(p => key == null || p.Key == key).Sum(p => Measure.Volume(p.Solid));

    static string DoorType(Fixture f, double width, double height) => (string)f.Run("architecture.createJoineryType", new JsonObject
    {
        ["spec"] = (JoinerySpec.DefaultDoor($"Door {width * 1000:0}") with { Width = width, Height = height }).ToJson(),
    })!["id"]!;

    static string Door(Fixture f, string wall, double along, string? typeId = null)
    {
        var a = new JsonObject { ["wallId"] = wall, ["along"] = along };
        if (typeId != null) a["typeId"] = typeId;
        return (string)f.Run("architecture.addDoor", a)!["id"]!;
    }

    [Fact]
    public void A_door_takes_its_opening_out_of_every_layer_of_its_wall()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0);
        var before = Vol(f, wall);
        var door = Door(f, wall, 2.5);
        Assert.Equal(IfcClass.Door, f.Model.Get(door)!.Class);
        var opening = Openings.OpeningOf(f.Model, door)!;
        Assert.Equal(wall, Openings.WallOf(f.Model, opening.Id)!.Id);
        // Default door 900 × 2100 through the default 285 mm wall.
        Assert.Equal(before - 0.9 * 2.1 * 0.285, Vol(f, wall), 6);
        Assert.Equal(5 * 3 * 0.285 - 0.9 * 2.1 * 0.285, f.Session.Quantities(wall)[0].Quantities.Single(q => q.Name == "NetVolume").Value, 6);
    }

    [Fact]
    public void Zones_lose_what_a_door_takes_from_them_blocks_follow()
    {
        var f = new Fixture();
        var type = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Brick 30 zoned",
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.3 }),
            ["zones"] = new JsonArray(WallZones.Defaults("Brick").Select(z => (JsonNode)z.DeepClone()).ToArray()),
            ["lintels"] = new JsonObject { ["mode"] = "none", ["bearing"] = 0, ["height"] = 0.25 },   // the zones alone, as the old test
        })!["id"]!;
        var wall = f.Wall(0, 0, 5, 0, type);
        // The old test's door: from 1 m to 2 m along the wall, 2.1 m high.
        Door(f, wall, 1.5, DoorType(f, 1, 2.1));
        var sets = f.Session.Quantities(wall);
        double Value(string part, string name) => sets.Single(s => s.Part == part).Quantities.Single(q => q.Name == name).Value;
        Assert.Equal(0.3 * (5 * 2.5 - 1 * 1.85), Value("zone:masonry", "NetVolume"), 6);
        Assert.Equal(5 * 2.5 - 1 * 1.85, Value("zone:masonry", "NetSideArea"), 6);
        Assert.Equal(Math.Ceiling(10.65 / (0.385 * 0.248)), Value("zone:masonry", "BlockCount"));
        Assert.Equal(Math.Ceiling(4 * 0.25 / (0.603 * 0.253)), Value("zone:first-course", "BlockCount"));
        Assert.Equal(5 * 0.25 * 0.3, Value("zone:ring-beam", "NetVolume"), 6);
    }

    [Fact]
    public void A_window_is_cut_where_it_stands_and_the_plan_shows_its_gap()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0);
        var cutsBefore = f.Session.Geometry(wall)!.Plan!.Count;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["x"] = 3.6, ["y"] = 0.3 })!["id"]!;
        var opening = Openings.OpeningOf(f.Model, window)!;
        Assert.Equal(3.6, opening.Num("along"), 9);      // the click, projected on the axis
        Assert.Equal(0.9, opening.Num("sill"), 9);       // the type's sill
        Assert.Equal(5 * 3 * 0.285 - 1.2 * 1.5 * 0.285, Vol(f, wall), 6);
        // 1.2 m above the floor the plan cuts through the window: every band in two.
        Assert.Equal(2 * cutsBefore, f.Session.Geometry(wall)!.Plan!.Count);
        // The window's own body stays within its opening.
        var g = f.Session.Geometry(window)!;
        Assert.Contains(g.Parts, p => p.Name == "glass");
        var bounds = g.Parts.Where(p => p.Name is not ("board" or "handle")).Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        Assert.True(bounds.Min(b => b.Min.X) >= 3.0 - 1e-6 && bounds.Max(b => b.Max.X) <= 4.2 + 1e-6);
        Assert.True(bounds.Min(b => b.Min.Z) >= 0.9 - 1e-6 && bounds.Max(b => b.Max.Z) <= 2.4 + 1e-6);
    }

    [Fact]
    public void An_opening_follows_its_wall_and_goes_with_it()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0);
        var door = Door(f, wall, 2);
        var opening = Openings.OpeningOf(f.Model, door)!.Id;
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["y1"] = 2, ["y2"] = 2 });
        var bounds = f.Session.Geometry(door)!.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        Assert.True(bounds.Min(b => b.Min.Y) > 1.5);
        f.Run("element.delete", new JsonObject { ["id"] = wall });
        Assert.Null(f.Model.Get(opening));
        Assert.Null(f.Model.Get(door));
    }

    [Fact]
    public void Deleting_a_door_takes_its_opening_and_the_wall_closes()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0);
        var full = Vol(f, wall);
        var door = Door(f, wall, 2);
        var opening = Openings.OpeningOf(f.Model, door)!.Id;
        f.Run("element.delete", new JsonObject { ["id"] = door });
        Assert.Null(f.Model.Get(opening));
        Assert.Equal(full, Vol(f, wall), 6);
    }

    [Fact]
    public void What_does_not_fit_is_refused()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 2, 0);
        Assert.Throws<CommandException>(() => Door(f, wall, 0.3));                       // past the start
        Assert.Throws<CommandException>(() => f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 1, ["sill"] = 2 }));   // above the top
        var door = Door(f, wall, 1);
        Assert.Throws<CommandException>(() => f.Run("architecture.moveOpening", new JsonObject { ["id"] = door, ["along"] = 1.8 }));
        var wide = DoorType(f, 3, 2.1);
        Assert.Throws<CommandException>(() => f.Run("architecture.setJoineryType", new JsonObject { ["id"] = door, ["typeId"] = wide }));
    }

    [Fact]
    public void A_changed_type_refits_every_opening_it_fills()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 6, 0);
        var type = DoorType(f, 0.9, 2.1);
        Door(f, wall, 1.5, type);
        Door(f, wall, 4.5, type);
        var before = Vol(f, wall);
        f.Run("architecture.updateJoineryType", new JsonObject { ["id"] = type, ["spec"] = (JoinerySpec.DefaultDoor("Door 1000") with { Width = 1.0, Height = 2.2 }).ToJson() });
        Assert.Equal(before - 2 * (1.0 * 2.2 - 0.9 * 2.1) * 0.285, Vol(f, wall), 6);
        Assert.Equal("Door 1000", f.Model.Get(type)!.Name);
        Assert.Equal("SINGLE_SWING_LEFT", f.Model.Get(type)!.Str("operationType"));
    }

    [Fact]
    public void Flipping_a_door_mirrors_its_hand_or_its_side()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0);
        var door = Door(f, wall, 2.5);
        Vec3 Handle() => Measure.Bounds(f.Session.Geometry(door)!.Parts.Single(p => p.Name == "handle").Solid)!.Value.Min;
        var h = Handle();
        f.Run("architecture.flipJoinery", new JsonObject { ["id"] = door, ["axis"] = "x" });
        Assert.True(Math.Abs((Handle().X - 2.5) + (h.X - 2.5)) < 0.05);   // the other hand
        f.Run("architecture.flipJoinery", new JsonObject { ["id"] = door, ["axis"] = "y" });
        Assert.True(Math.Sign(Handle().Y) != Math.Sign(h.Y) || Math.Abs(Handle().Y - h.Y) > 0.03);   // the other side
    }

    [Fact]
    public void A_door_gets_the_default_precast_lintel_through_the_core_which_it_takes_from()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 6, 0);
        var full = Vol(f, wall);
        Door(f, wall, 3);
        var parts = f.Session.Geometry(wall)!.Parts;
        var lintel = Assert.Single(parts, p => p.Key!.StartsWith("lintel:"));
        var b = Measure.Bounds(lintel.Solid)!.Value;
        // 900 + 2 × 250 = 1400 → the 1500 catalogue length, centred over the door; 250 high over its 2100 head; the 250 mm brick core.
        Assert.Equal(1.5, b.Max.X - b.Min.X, 9);
        Assert.Equal(2.25, b.Min.X, 9);
        Assert.Equal(2.1, b.Min.Z, 9);
        Assert.Equal(2.35, b.Max.Z, 9);
        Assert.Equal(0.25, b.Max.Y - b.Min.Y, 9);
        // What the lintel is, the core is not: the wall's volume, door out, stays the same.
        Assert.Equal(full - 0.9 * 2.1 * 0.285, Vol(f, wall), 6);
        var set = f.Session.Quantities(wall).Single(s => s.Part?.StartsWith("lintel:") == true);
        Assert.Equal("Qto_BeamBaseQuantities", set.Name);
        Assert.Equal(1.5 * 0.25 * 0.25, set.Quantities.Single(q => q.Name == "NetVolume").Value, 6);
        Assert.Equal(1.5, set.Quantities.Single(q => q.Name == "Length").Value, 6);
    }

    [Fact]
    public void A_wall_type_without_lintels_and_a_window_type_with_its_own_rule()
    {
        var f = new Fixture();
        var type = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Partition", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Plasterboard", ["thickness"] = 0.1 }),
            ["lintels"] = new JsonObject { ["mode"] = "none", ["bearing"] = 0, ["height"] = 0.25 },
        })!["id"]!;
        var wall = f.Wall(0, 0, 6, 0, type);
        Door(f, wall, 2);
        Assert.DoesNotContain(f.Session.Geometry(wall)!.Parts, p => p.Key!.StartsWith("lintel:"));
        var cast = (string)f.Run("architecture.createJoineryType", new JsonObject
        {
            ["spec"] = (JoinerySpec.DefaultDoor("Door cast") with { Lintel = new LintelRule("cast", 0.2, 0.2) }).ToJson(),
        })!["id"]!;
        Door(f, wall, 4.5, cast);
        var lintel = Assert.Single(f.Session.Geometry(wall)!.Parts, p => p.Key!.StartsWith("lintel:"));
        Assert.Equal("Lintel (cast)", lintel.Name);
        Assert.Equal(0.9 + 0.4, Measure.Bounds(lintel.Solid)!.Value.Max.X - Measure.Bounds(lintel.Solid)!.Value.Min.X, 9);
    }
}
