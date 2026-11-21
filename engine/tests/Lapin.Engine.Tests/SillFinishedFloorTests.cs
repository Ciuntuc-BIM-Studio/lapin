using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 192: a window's sill was measured from the wall's base, the structural level, while architects give it over the
/// finished floor. Each opening says what its sill is measured from (<c>sillFrom</c>: structural, as before and for every IFC
/// import, or finished), the project gives new ones theirs, and the finished floor's height over the wall's base is worked
/// out on every ask: the wall's own <c>finishedFloor</c>, else its type's, else 0.
/// The engine turns it into the height over the base in one place (<see cref="Openings.Place"/>), which everything reads.
/// </summary>
public sealed class SillFinishedFloorTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-sill-finished").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static string OpeningOf(Fixture f, string fill) => Openings.OpeningOf(f.Model, fill)!.Id;
    static Openings.Placed Placed(Fixture f, string fill) => Openings.Place(f.Model, f.Model.Get(OpeningOf(f, fill))!);

    static string Window(Fixture f, string wall, double along, double sill, string? sillFrom = null)
    {
        var a = new JsonObject { ["wallId"] = wall, ["along"] = along, ["sill"] = sill };
        if (sillFrom != null) a["sillFrom"] = sillFrom;
        return (string)f.Run("architecture.addWindow", a)!["id"]!;
    }

    static (Fixture F, string Type, string Wall) WallOfType(double? finishedFloor)
    {
        var f = new Fixture();
        var type = f.Type("Brick 30", ("Brick", 0.3, true));
        if (finishedFloor is { } ff) f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["finishedFloor"] = ff });
        return (f, type, f.Wall(0, 0, 8, 0, type));
    }

    [Fact]
    public void A_window_measured_from_the_finished_floor_stands_that_floor_higher_over_the_walls_base()
    {
        var (f, _, wall) = WallOfType(0.10);
        var w = Window(f, wall, 2, 0.9, "finished");
        var p = Placed(f, w);
        Assert.Equal(1.00, p.Sill, 9);
        Assert.Equal(1.00, p.FillSill, 9);
        Assert.Equal(0.10, p.SillBase, 9);
        Assert.Equal(0.9, p.OwnSill, 9);
        // Stored as given: the sill over the finished floor and what it is measured from; never the floor's height.
        var o = f.Model.Get(OpeningOf(f, w))!;
        Assert.Equal(0.9, o.Num("sill"), 9);
        Assert.Equal("finished", o.Str("sillFrom"));
        Assert.Null(o.Attrs["finishedFloor"]);

        // Its body is where a structural window at 1.00 m is: the same frame, the same hole.
        var s = Window(f, wall, 5.5, 1.00, "structural");
        var (a, b) = (f.Bounds(w), f.Bounds(s));
        Assert.Equal(b.Min.Z, a.Min.Z, 9);
        Assert.Equal(b.Max.Z, a.Max.Z, 9);
        // The query the interface reads: the sill as typed, its reference, the floor.
        var r = f.Session.Query("architecture.openingReferences", new JsonObject { ["id"] = w })!["opening"]!;
        Assert.Equal(0.9, (double)r["sill"]!, 9);
        Assert.Equal("finished", (string?)r["sillFrom"]);
        Assert.Equal(0.10, (double)r["finishedFloor"]!, 9);
        Assert.Equal(1.00, (double)r["bottom"]!, 9);
    }

    [Fact]
    public void A_structural_sill_stays_over_the_walls_base_whatever_the_finished_floor()
    {
        var (f, _, wall) = WallOfType(0.10);
        var w = Window(f, wall, 2, 0.9);
        var p = Placed(f, w);
        Assert.Equal(0.9, p.Sill);
        Assert.Equal(0, p.SillBase);
        Assert.Null(f.Model.Get(OpeningOf(f, w))!.Attrs["sillFrom"]);
        // An explicit "structural" writes nothing either: the opening is as it was before.
        var s = Window(f, wall, 5, 0.9, "structural");
        Assert.Null(f.Model.Get(OpeningOf(f, s))!.Attrs["sillFrom"]);
    }

    [Fact]
    public void The_walls_own_finished_floor_beats_its_types_and_moving_either_moves_the_window()
    {
        var (f, type, wall) = WallOfType(0.10);
        var w = Window(f, wall, 2, 0.9, "finished");
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["finishedFloor"] = 0.05 });
        Assert.Equal(0.95, Placed(f, w).Sill, 9);
        // In the inspector too (element.update), and cleared: the type's again.
        f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["finishedFloor"] = 0.07 } });
        Assert.Equal(0.97, Placed(f, w).Sill, 9);
        f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["finishedFloor"] = null } });
        Assert.Equal(1.00, Placed(f, w).Sill, 9);
        // The type's moved: the window with it, its own sill over the floor unchanged.
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["finishedFloor"] = 0.15 });
        Assert.Equal(1.05, Placed(f, w).Sill, 9);
        Assert.Equal(0.9, Placed(f, w).OwnSill, 9);
        f.Run("element.update", new JsonObject { ["id"] = type, ["attrs"] = new JsonObject { ["finishedFloor"] = 0.12 } });
        Assert.Equal(1.02, Placed(f, w).Sill, 9);
    }

    [Fact]
    public void A_finished_floor_that_would_push_a_window_out_of_its_wall_is_refused()
    {
        var (f, type, wall) = WallOfType(null);
        var w = Window(f, wall, 2, 1.2, "finished");
        var p = Placed(f, w);
        var room = Walls.HeightOf(f.Model, f.Model.Get(wall)!) - (p.Sill + p.Height);
        var tooHigh = Math.Round(room + 0.05, 3);
        Assert.InRange(tooHigh, 0.01, 1);
        var before = f.Model.Get(type)!.Attrs.ToJsonString();
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["finishedFloor"] = tooHigh }));
        Assert.Equal(before, f.Model.Get(type)!.Attrs.ToJsonString());
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["finishedFloor"] = tooHigh }));
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["finishedFloor"] = tooHigh } }));
        Assert.Null(f.Model.Get(wall)!.Attrs["finishedFloor"]);
        Assert.Equal(1.2, Placed(f, w).Sill, 9);
        // A structural window at the same height does not move, so the floor may rise under it.
        var g = WallOfType(null);
        Window(g.F, g.Wall, 2, 1.2);
        g.F.Run("architecture.updateWallType", new JsonObject { ["id"] = g.Type, ["finishedFloor"] = tooHigh });
        // A new window measured from the finished floor that would not fit is refused too.
        Assert.Throws<CommandException>(() => Window(g.F, g.Wall, 5, 1.2, "finished"));
    }

    [Fact]
    public void A_rooms_floor_finish_is_not_the_finished_floor_only_the_wall_or_its_type_gives_one()
    {
        const double inside = 0.125 + 0.015;
        var f = new Fixture();
        var (x0, y0, x1, y1) = (-inside, -inside, 5 + inside, 4 + inside);
        var walls = new[] { ((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1)), ((x0, y1), (x0, y0)) }
            .Select(x => f.Wall(x.Item1.Item1, x.Item1.Item2, x.Item2.Item1, x.Item2.Item2)).ToArray();
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        var w = Window(f, walls[0], 2.5, 0.9, "finished");
        // No finish yet: the floor is the wall's base.
        Assert.Equal(0.9, Placed(f, w).Sill, 9);
        var finish = (string)f.Run("architecture.createRoomFinishType", new JsonObject
        {
            ["spec"] = new RoomFinishSpec("Tiled", Floor: [new("Ceramic tiles", 0.01), new("Tile adhesive", 0.005)]).ToJson(),
        })!["id"]!;
        // The finish's layers go down from the room's level (the user's decision, 20.11.2026): the window does not move.
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = finish });
        Assert.Equal(0.9, Placed(f, w).Sill, 9);
        // The wall's type, then the wall.
        var type = f.Model.RelatingOf(walls[0], IfcClass.RelDefinesByType)!.Id;
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["finishedFloor"] = 0.08 });
        Assert.Equal(0.98, Placed(f, w).Sill, 9);
        f.Run("architecture.updateWall", new JsonObject { ["id"] = walls[0], ["finishedFloor"] = 0.03 });
        Assert.Equal(0.93, Placed(f, w).Sill, 9);
    }

    [Fact]
    public void Each_change_is_one_undo_step()
    {
        var (f, type, wall) = WallOfType(0.10);
        var w = Window(f, wall, 2, 0.9);
        var opening = OpeningOf(f, w);
        var was = f.Model.Get(opening)!.Attrs.ToJsonString();

        // Another reference and no sill: the window stays where it stands, its sill now read over the floor.
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = w, ["sillFrom"] = "finished" });
        Assert.Equal(0.9, Placed(f, w).Sill, 9);
        Assert.Equal(0.8, Placed(f, w).OwnSill, 9);
        f.Session.Undo(f.Session.Author);
        Assert.Equal(was, f.Model.Get(opening)!.Attrs.ToJsonString());

        // A sill and the reference together: the sill is over it.
        f.Run("architecture.moveOpening", new JsonObject { ["ids"] = new JsonArray(w), ["sill"] = 0.9, ["sillFrom"] = "finished" });
        Assert.Equal(1.0, Placed(f, w).Sill, 9);
        // Moved along or up, it keeps its reference.
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = w, ["along"] = 3, ["sill"] = 0.85 });
        Assert.Equal(0.95, Placed(f, w).Sill, 9);
        Assert.Equal("finished", f.Model.Get(opening)!.Str("sillFrom"));
        f.Session.Undo(f.Session.Author);
        Assert.Equal(1.0, Placed(f, w).Sill, 9);
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["finishedFloor"] = 0.2 });
        Assert.Equal(1.1, Placed(f, w).Sill, 9);
        f.Session.Undo(f.Session.Author);
        Assert.Equal(1.0, Placed(f, w).Sill, 9);
        f.Session.Undo(f.Session.Author);
        Assert.Equal(was, f.Model.Get(opening)!.Attrs.ToJsonString());
        // Back to structural: it stays where it stands again, the key gone.
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = w, ["sillFrom"] = "finished" });
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = w, ["sillFrom"] = "structural" });
        Assert.Equal(was, f.Model.Get(opening)!.Attrs.ToJsonString());
        Assert.Throws<CommandException>(() => f.Run("architecture.moveOpening", new JsonObject { ["id"] = w, ["sillFrom"] = "slab" }));
    }

    [Fact]
    public void The_projects_default_is_the_one_new_openings_take_and_structural_until_switched()
    {
        var (f, _, wall) = WallOfType(0.10);
        Assert.Equal("structural", (string?)f.Session.Query("architecture.openingDefaults", new JsonObject())!["sillFrom"]);
        var before = Window(f, wall, 1, 0.9);
        f.Run("architecture.setOpeningDefaults", new JsonObject { ["sillFrom"] = "finished" });
        Assert.Equal("finished", (string?)f.Session.Query("architecture.openingDefaults", new JsonObject())!["sillFrom"]);
        var after = Window(f, wall, 3, 0.9);
        var asked = Window(f, wall, 5, 0.9, "structural");
        var hole = (string)f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 7, ["sill"] = 0.5, ["width"] = 0.6, ["height"] = 0.6 })!["id"]!;
        Assert.Equal(0.9, Placed(f, before).Sill, 9);
        Assert.Equal(1.0, Placed(f, after).Sill, 9);
        Assert.Equal(0.9, Placed(f, asked).Sill, 9);
        Assert.Equal(0.6, Openings.Place(f.Model, f.Model.Get(hole)!).Sill, 9);
        // One step, and back.
        f.Session.Undo(f.Session.Author);
        f.Session.Undo(f.Session.Author);
        f.Session.Undo(f.Session.Author);
        f.Session.Undo(f.Session.Author);
        Assert.Equal("structural", (string?)f.Session.Query("architecture.openingDefaults", new JsonObject())!["sillFrom"]);
        f.Session.Redo(f.Session.Author);
        f.Run("architecture.setOpeningDefaults", new JsonObject { ["sillFrom"] = "structural" });
        Assert.Null(Spatial.Project(f.Model)!.Attrs["sillFrom"]);
    }

    [Fact]
    public void Nothing_set_is_the_old_behaviour_bit_for_bit()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 6, 0);
        var w = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 2 })!["id"]!;
        var d = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 4.5 })!["id"]!;
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = w, ["sill"] = 0.8123456789 });
        foreach (var fill in new[] { w, d })
        {
            var o = f.Model.Get(OpeningOf(f, fill))!;
            // The opening's attributes are what they always were: no reference, no floor.
            Assert.Equal(["along", "sill"], o.Attrs.Select(kv => kv.Key).Order().ToArray());
            var p = Placed(f, fill);
            Assert.Equal(o.Num("sill"), p.Sill);
            Assert.Equal(0, p.SillBase);
            Assert.Equal(p.FillSill, p.OwnSill);
        }
        Assert.Equal(0.8123456789, Placed(f, w).Sill);
        Assert.Null(f.Model.Get(wall)!.Attrs["finishedFloor"]);
        Assert.Null(Spatial.Project(f.Model)!.Attrs["sillFrom"]);
    }

    [Fact]
    public void IFC_keeps_the_structural_height_and_Lapins_own_file_brings_the_reference_back()
    {
        var (f, _, wall) = WallOfType(0.10);
        var finished = Window(f, wall, 2, 0.9, "finished");
        var structural = Window(f, wall, 5.5, 1.0);
        var path = Path.Combine(_dir, "sills.ifc");
        IfcExport.Write(f.Session, path);
        var text = File.ReadAllText(path);
        Assert.Contains("'sillFrom'", text);
        Assert.Contains("'finishedFloor'", text);

        // Read as references, the bodies as the file has them: both windows at the same structural height.
        var r = new Fixture();
        IfcImport.Run(r.Session, path, new IfcImportOptions("reference"), out _);
        double Bottom(string id) => r.Session.Geometry(id)!.Parts.SelectMany(p => p.Solid.Vertices).Min(v => v.Z);
        Assert.Equal(Bottom(structural), Bottom(finished), 6);
        Assert.Equal(f.Bounds(finished).Min.Z, Bottom(finished), 6);

        // Read back native: the same opening, its sill over the finished floor of its wall's type.
        var b = new Fixture();
        var result = IfcImport.Run(b.Session, path, null, out _);
        Assert.True(result.References == 0, string.Join("; ", result.Notes));
        Assert.Equal(f.Model.Get(OpeningOf(f, finished))!.Attrs.ToJsonString(), b.Model.Get(OpeningOf(b, finished))!.Attrs.ToJsonString());
        Assert.Equal(1.0, Placed(b, finished).Sill, 9);
        Assert.Equal(1.0, Placed(b, structural).Sill, 9);
    }
}
