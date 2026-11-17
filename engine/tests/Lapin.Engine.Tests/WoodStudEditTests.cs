using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>
/// A timber-frame wall's own exceptions to its type's frame (audit 2026-11-15 §14): studs moved, taken out and added on the
/// wall, kings and jacks counted on an opening, by commands and by the frame elevation's grips. One undo step each; a wall
/// without exceptions is framed as its type says; a split wall keeps them on its parts.
/// </summary>
public sealed class WoodStudEditTests
{
    static (Fixture F, string Wall, string Window) Wall()
    {
        var f = new Fixture(new ArchitectureModule(), new StudioModule(), new WoodModule());
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3 })!["id"]!;
        f.Run("wood.setWallFrame", new JsonObject { ["id"] = Walls.TypeOf(f.Model, wall)!.Id, ["spec"] = FrameSpec.Nordic.ToJson() });
        return (f, wall, window);
    }

    static WoodModule.WallFrame Frame(Fixture f, string wall) => WoodModule.Of(f.Model, f.Model.Get(wall)!, f.Session.Geometry(wall), null)!;
    static List<double> Studs(Fixture f, string wall) => [.. Frame(f, wall).Result.Members.Where(m => m.Role == "stud").Select(m => Math.Round((m.U0 + m.U1) / 2, 4)).Order()];

    [Fact]
    public void A_stud_moved_taken_out_and_added_on_one_wall_and_reset_and_undone()
    {
        var (f, wall, _) = Wall();
        var before = Studs(f, wall);
        Assert.Contains(0.6, before);
        f.Run("wood.editStuds", new JsonObject { ["id"] = wall, ["move"] = new JsonObject { ["from"] = 0.6, ["to"] = 0.75 } });
        var moved = Studs(f, wall);
        Assert.DoesNotContain(0.6, moved);
        Assert.Contains(0.75, moved);
        Assert.Equal(before.Count, moved.Count);
        f.Run("wood.editStuds", new JsonObject { ["id"] = wall, ["remove"] = new JsonArray(0.75) });
        Assert.Equal(before.Count - 1, Studs(f, wall).Count);
        // On another member: refused.
        Assert.Throws<CommandException>(() => f.Run("wood.editStuds", new JsonObject { ["id"] = wall, ["add"] = new JsonArray(1.2) }));
        f.Run("wood.editStuds", new JsonObject { ["id"] = wall, ["add"] = new JsonArray(0.9) });
        Assert.Contains(0.9, Studs(f, wall));
        f.Session.Undo();
        Assert.DoesNotContain(0.9, Studs(f, wall));
        f.Run("wood.editStuds", new JsonObject { ["id"] = wall, ["reset"] = true });
        Assert.Equal(before, Studs(f, wall));
        Assert.Null(f.Model.Get(wall)!.Attrs[Framing.StudEdits.Key]);
    }

    [Fact]
    public void The_frame_elevation_offers_grips_for_studs_kings_and_jacks_that_edit_the_wall_and_its_opening()
    {
        var (f, wall, window) = Wall();
        var view = (string)f.Run("wood.frameView", new JsonObject { ["id"] = wall })!["id"]!;
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
        var mark = d["marks"]!.AsArray().OfType<JsonObject>().First(m => (string?)m["id"] == wall && m["data"]?["frameGrips"] != null);
        var grips = mark["data"]!["frameGrips"]!.AsArray().OfType<JsonObject>().ToDictionary(g => (string)g["key"]!, g => (X: (double)g["at"]![0]!, Y: (double)g["at"]![1]!));
        var opening = Lapin.Architecture.Openings.PlacedOn(f.Model, wall).Single().Opening.Id;
        Assert.Contains("stud:0.6", grips.Keys);
        Assert.Contains($"king:{opening}:L", grips.Keys);
        Assert.Contains($"jack:{opening}:R", grips.Keys);
        // A stud dragged 150 mm along the wall (the elevation's x runs along it, one way or the other).
        var s = grips["stud:0.6"];
        var other = grips["stud:1.2"];
        var dx = Math.Sign(other.X - s.X) * 0.15;
        f.Run("wood.frameGrip", new JsonObject { ["id"] = wall, ["viewId"] = view, ["key"] = "stud:0.6", ["point"] = new JsonArray(s.X + dx, s.Y) });
        Assert.Contains(0.75, Studs(f, wall));
        // The left king dragged out by one stud: two kings on that opening's own rule.
        var kings = Frame(f, wall).Result.Openings[opening].Rule.Kings;
        var k = grips[$"king:{opening}:L"];
        var t = FrameSpec.Nordic.Stud.W;
        f.Run("wood.frameGrip", new JsonObject { ["id"] = wall, ["viewId"] = view, ["key"] = $"king:{opening}:L", ["point"] = new JsonArray(k.X - Math.Sign(other.X - s.X) * t, k.Y) });
        Assert.Equal(kings + 1, Frame(f, wall).Result.Openings[opening].Rule.Kings);
        Assert.NotNull(f.Model.Get(opening)!.Attrs[WoodModule.OpeningKey]);
        // Ctrl-click: a stud taken out.
        f.Run("wood.frameGrip", new JsonObject { ["id"] = wall, ["viewId"] = view, ["key"] = "stud:1.8", ["remove"] = true });
        Assert.DoesNotContain(1.8, Studs(f, wall));
    }

    [Fact]
    public void A_split_wall_keeps_its_own_studs_on_its_parts()
    {
        var (f, wall, _) = Wall();
        f.Run("wood.editStuds", new JsonObject { ["id"] = wall, ["move"] = new JsonObject { ["from"] = 4.8, ["to"] = 4.95 } });
        var second = ((JsonArray)f.Run("element.split", new JsonObject { ["id"] = wall, ["at"] = new JsonArray(4.5, 0) })!["ids"]!)[1]!.ToString();
        Assert.Null(f.Model.Get(wall)!.Attrs[Framing.StudEdits.Key]);
        var e = Framing.StudEdits.Of(f.Model.Get(second)!)!;
        Assert.Equal(0.45, e.Add.Single(), 4);
        Assert.Equal(0.3, e.Remove.Single(), 4);
    }
}
