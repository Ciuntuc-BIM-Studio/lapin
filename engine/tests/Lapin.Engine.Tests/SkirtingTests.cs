using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Skirtings (a tester's report #61, decision 254): a profile swept round a room along the walls' faces, interrupted at the doors, exported as IfcCovering SKIRTINGBOARD.</summary>
public class SkirtingTests
{
    /// <summary>A 4 × 5 m box of 20 cm walls on their axes (the room inside 3.80 × 4.80), a 900 mm door in the south wall, the room.</summary>
    static (Fixture F, string[] Walls, string Room) Box()
    {
        var f = new Fixture();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        string[] walls = [f.Wall(0, 0, 4, 0, type), f.Wall(4, 0, 4, 5, type), f.Wall(4, 5, 0, 5, type), f.Wall(0, 5, 0, 0, type)];
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["along"] = 2 });
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 2.5 })!["id"]!;
        return (f, walls, room);
    }

    static Dictionary<string, double> Qs(Fixture f, string id) =>
        f.Session.Quantities(id).Single(s => s.Name == "Lapin_SkirtingQuantities").Quantities.ToDictionary(x => x.Name, x => x.Value);

    [Fact]
    public void A_skirting_round_a_room_runs_along_the_walls_faces_and_stops_at_the_door()
    {
        var (f, _, room) = Box();
        var id = (string)f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["roomId"] = room })!["id"]!;
        var q = Qs(f, id);
        // The room's inner outline is 2 × (3.8 + 4.8) = 17.2 m; the door takes 0.9 m of it.
        Assert.Equal(17.2 - 0.9, q["Length"], 6);
        Assert.Equal(1, q["Pieces"]);
        var spec = Skirtings.SpecOf(f.Model, f.Model.Get(id)!);
        Assert.Equal(0.08, spec.Height, 9);
        Assert.Equal(0.012, spec.Thickness, 9);
        var g = f.Session.Geometry(id)!;
        var part = Assert.Single(g.Parts);
        var box = Measure.Bounds(part.Solid)!.Value;
        // On the floor, its type's height, inside the walls' faces and no thicker than the board.
        Assert.Equal(0, box.Min.Z, 6);
        Assert.Equal(0.08, box.Max.Z, 6);
        Assert.Equal(0.1, box.Min.X, 6);
        Assert.Equal(3.9, box.Max.X, 6);
        Assert.Equal(0.1, box.Min.Y, 6);
        Assert.Equal(4.9, box.Max.Y, 6);
        // Its volume, the profile swept along its length (the mitres inside the corners take a little off).
        var volume = Measure.Volume(part.Solid);
        Assert.InRange(volume / (q["Length"] * Skirtings.Area(spec)), 0.99, 1.0);
        Assert.Equal(volume, q["Volume"], 9);
        // The gap lies across the door: no board between x = 1.55 and 2.45 along the south wall.
        Assert.DoesNotContain(part.Solid.Vertices, v => v.Y < 0.2 && v.X > 1.56 && v.X < 2.44);
    }

    [Fact]
    public void A_click_inside_the_room_lays_the_same_skirting_and_one_inside_walls_without_a_room_too()
    {
        var (f, walls, room) = Box();
        var id = (string)f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 2.5 })!["id"]!;
        Assert.Equal(room, f.Model.Get(id)!.Str("roomId"));
        Assert.Equal(16.3, Qs(f, id)["Length"], 6);
        f.Run("element.delete", new JsonObject { ["id"] = room });
        // Its room gone, the skirting keeps the outline it had.
        Assert.Equal(16.3, Qs(f, id)["Length"], 6);
        var free = (string)f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 2.5 })!["id"]!;
        Assert.Null(f.Model.Get(free)!.Str("roomId"));
        Assert.Equal(16.3, Qs(f, free)["Length"], 6);
        Assert.Throws<CommandException>(() => f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 9, ["y"] = 9 }));
        _ = walls;
    }

    [Fact]
    public void A_skirting_drawn_along_a_wall_lies_away_from_it_and_flips()
    {
        var (f, _, _) = Box();
        // Along the east wall's inner face (x = 3.9), drawn northwards: the wall is on the right, the board on the left.
        var id = (string)f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(3.9, 0.1), new JsonArray(3.9, 4.9)) })!["id"]!;
        Assert.Equal("left", f.Model.Get(id)!.Str("side"));
        var box = Measure.Bounds(Assert.Single(f.Session.Geometry(id)!.Parts).Solid)!.Value;
        Assert.Equal(3.9 - 0.012, box.Min.X, 6);
        Assert.Equal(3.9, box.Max.X, 6);
        Assert.Equal(4.8, Qs(f, id)["Length"], 6);
        // Drawn the other way round (southwards), it is found on the right.
        var back = (string)f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(3.9, 4.9), new JsonArray(3.9, 0.1)) })!["id"]!;
        Assert.Equal("right", f.Model.Get(back)!.Str("side"));
        // Flipped, its board goes into the wall's side; raised, it stands off the floor.
        f.Run("architecture.updateSkirting", new JsonObject { ["id"] = id, ["side"] = "flip", ["baseOffset"] = 0.02 });
        box = Measure.Bounds(Assert.Single(f.Session.Geometry(id)!.Parts).Solid)!.Value;
        Assert.Equal(3.9 + 0.012, box.Max.X, 6);
        Assert.Equal(0.02, box.Min.Z, 6);
        // Along the south wall, across the door: two pieces.
        var south = (string)f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0.1, 0.1), new JsonArray(3.9, 0.1)) })!["id"]!;
        Assert.Equal(2, Qs(f, south)["Pieces"]);
        Assert.Equal(3.8 - 0.9, Qs(f, south)["Length"], 6);
        f.Run("architecture.updateSkirting", new JsonObject { ["id"] = south, ["gaps"] = false });
        Assert.Equal(3.8, Qs(f, south)["Length"], 6);
    }

    [Fact]
    public void Each_profile_is_a_closed_section_of_the_types_size()
    {
        foreach (var spec in SkirtingSpec.Library)
        {
            var p = Skirtings.Profile(spec);
            Assert.True(Polygon2.SignedArea(p) > 0, $"{spec.Name}: anticlockwise");
            Assert.Equal(spec.Thickness, p.Max(x => x.X), 9);
            Assert.Equal(spec.Height, p.Max(x => x.Y), 9);
            Assert.Equal(0, p.Min(x => x.X), 9);
            Assert.True(Skirtings.Area(spec) < spec.Height * spec.Thickness + 1e-12, spec.Name);
        }
        Assert.Equal(SkirtingSpec.Profiles.Length, SkirtingSpec.Library.Select(s => s.Profile).Distinct().Count());
        // The flush board is a full rectangle; the cove's foot leaves the plate thin above it.
        Assert.Equal(0.08 * 0.008, Skirtings.Area(SkirtingSpec.Library.Single(s => s.Profile == "flush")), 12);
        var cove = SkirtingSpec.Library.Single(s => s.Profile == "cove");
        Assert.True(Skirtings.Area(cove) < cove.Height * cove.Thickness * 0.4);
    }

    [Fact]
    public void A_library_type_is_made_once_and_a_skirting_changes_its_type()
    {
        var (f, _, room) = Box();
        var oak = f.Run("architecture.skirtingTypeFromLibrary", new JsonObject { ["name"] = "Oak 70 moulded" })!;
        Assert.True((bool)oak["created"]!);
        var again = f.Run("architecture.skirtingTypeFromLibrary", new JsonObject { ["name"] = "Oak 70 moulded" })!;
        Assert.False((bool)again["created"]!);
        Assert.Equal((string)oak["id"]!, (string)again["id"]!);
        Assert.Single(f.Model.OfClass(Skirtings.TypeClass));
        Assert.NotNull(Materials.Find(f.Model, "Oak"));
        Assert.Equal("Finish", Materials.Find(f.Model, "Oak")!.Str("category"));
        var id = (string)f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["roomId"] = room, ["typeId"] = (string)oak["id"]! })!["id"]!;
        Assert.Equal(0.07, Measure.Bounds(f.Session.Geometry(id)!.Parts[0].Solid)!.Value.Max.Z, 6);
        var tile = (string)f.Run("architecture.skirtingTypeFromLibrary", new JsonObject { ["name"] = "Tile 80 flush" })!["id"]!;
        f.Run("architecture.updateSkirting", new JsonObject { ["id"] = id, ["typeId"] = tile });
        Assert.Equal("Tile 80 flush", Skirtings.SpecOf(f.Model, f.Model.Get(id)!).Name);
        Assert.Equal(0.08, Measure.Bounds(f.Session.Geometry(id)!.Parts[0].Solid)!.Value.Max.Z, 6);
        // A type changed: its skirtings follow.
        f.Run("architecture.updateSkirtingType", new JsonObject { ["id"] = tile, ["spec"] = new SkirtingSpec("Tile 100 flush", "flush", 0.1, 0.008, "Ceramic tile").ToJson() });
        Assert.Equal(0.1, Measure.Bounds(f.Session.Geometry(id)!.Parts[0].Solid)!.Value.Max.Z, 6);
        Assert.Throws<CommandException>(() => f.Run("architecture.skirtingTypeFromLibrary", new JsonObject { ["name"] = "Marble" }));
    }

    [Fact]
    public void The_schedule_lists_skirtings_by_type_with_their_length()
    {
        var (f, _, room) = Box();
        f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["roomId"] = room });
        f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(3.9, 0.1), new JsonArray(3.9, 4.9)) });
        var source = f.Session.Catalog.All<IScheduleSource>().Single(s => s.Kind == "skirtings");
        var sched = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "skirtings" })!["id"]!;
        Assert.Equal("Skirtings", f.Model.Get(sched)!.Name);
        var grid = source.Grid(new Q(f));
        var row = Assert.Single(grid.Rows, r => r[0] == "MDF 80 bevelled");
        Assert.Equal("2", row[4]);
        Assert.Equal("2", row[5]);
        Assert.Equal((16.3 + 4.8).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), row[6]);
    }

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    [Fact]
    public void A_skirting_goes_to_IFC_as_a_skirting_board_covering_of_its_type()
    {
        var (f, _, room) = Box();
        f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["roomId"] = room });
        var path = Path.Combine(Path.GetTempPath(), $"skirting-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            var covering = Assert.Single(step.OfType("IFCCOVERING"));
            Assert.Equal(new StepEnum("SKIRTINGBOARD"), covering[8]);
            var type = Assert.Single(step.OfType("IFCCOVERINGTYPE"));
            Assert.Equal(new StepEnum("SKIRTINGBOARD"), type[9]);
        }
        finally { File.Delete(path); }
    }
}
