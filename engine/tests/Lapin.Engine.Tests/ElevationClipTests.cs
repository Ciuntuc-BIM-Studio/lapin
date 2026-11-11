using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// An elevation's marker moved into the building clips the elevation there, as Revit's (decision 460, the user's answer
/// to its open item): its line is where the elevation starts seeing, what lies between it and the viewer is not drawn,
/// what its plane cuts is drawn cut, with the materials' cut hatches. Left outside (where a new one stands), it sees the
/// whole building as before.
/// </summary>
public sealed class ElevationClipTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonObject? Mark(JsonObject d, string id) => ((JsonArray)d["marks"]!).Select(m => (JsonObject)m!).FirstOrDefault(m => (string?)m["id"] == id);
    static List<JsonObject> Pens(JsonObject d, string layer) => ((JsonArray)d["pens"]!).Select(p => (JsonObject)p!).Where(p => (string?)p["layer"] == layer).ToList();
    static IEnumerable<(double X0, double Y0, double X1, double Y1)> Segs(JsonObject pen)
    {
        var s = ((JsonArray?)pen["segments"] ?? []).Select(x => (double)x!).ToArray();
        for (var k = 0; k + 3 < s.Length; k += 4) yield return (s[k], s[k + 1], s[k + 2], s[k + 3]);
    }

    static (string South, string North, string East, string Plan, string Elevation) House(Fixture f)
    {
        var south = f.Wall(0, 0, 10, 0); f.Wall(10, 0, 10, 8); var north = f.Wall(10, 8, 0, 8); var west = f.Wall(0, 8, 0, 0);
        var elevation = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        return (south, north, west, plan, elevation);
    }

    [Fact]
    public void A_marker_moved_into_the_building_clips_the_elevation_and_draws_the_cut()
    {
        var f = F();
        var (south, north, west, plan, elevation) = House(f);
        var whole = Drawing(f, elevation);
        Assert.NotNull(Mark(whole, south));
        Assert.Empty(Pens(whole, "HATCH"));
        // Its line put at y = 4, through the house: the south wall, before it, is gone; the west wall is cut; the north one seen.
        f.Run("view.moveMarker", new JsonObject { ["id"] = elevation, ["depth"] = 4 });
        var clipped = Drawing(f, elevation);
        Assert.Null(Mark(clipped, south));
        Assert.NotNull(Mark(clipped, north));
        Assert.NotNull(Mark(clipped, west)!["data"]?["loops"]);
        // The cut takes the cut hatch (brick's 45° lines), not the facade's courses.
        var hatch = Pens(clipped, "HATCH").SelectMany(Segs).ToList();
        Assert.NotEmpty(hatch);
        Assert.All(hatch, s => Assert.Equal(1, Math.Abs((s.Y1 - s.Y0) / (s.X1 - s.X0)), 3));
        // The plan's marker stands there.
        var data = (JsonObject)Mark(Drawing(f, plan), elevation)!["data"]!;
        Assert.All((JsonArray)data["ends"]!, p => Assert.Equal(4, (double)p![1]!, 6));
        Assert.Equal(4, (double)data["depth"]!, 6);
        // One undo step: the whole building again.
        f.Session.Undo();
        Assert.NotNull(Mark(Drawing(f, elevation), south));
    }

    [Fact]
    public void A_marker_left_outside_sees_the_whole_building_as_before()
    {
        var f = F();
        var (_, _, _, plan, elevation) = House(f);
        var before = Drawing(f, elevation).ToJsonString();
        f.Run("view.moveMarker", new JsonObject { ["id"] = elevation, ["depth"] = -6, ["along"] = 2 });
        Assert.Equal(before, Drawing(f, elevation).ToJsonString());
        var data = (JsonObject)Mark(Drawing(f, plan), elevation)!["data"]!;
        Assert.Equal(-6, (double)data["depth"]!, 6);
        Assert.Equal(7, (double)data["symbol"]![0]!, 6);
    }

    [Fact]
    public void The_Move_tool_takes_the_marker_in_from_where_it_stands_and_a_pinned_one_stays()
    {
        var f = F();
        var (south, _, _, plan, elevation) = House(f);
        var at = (double)Mark(Drawing(f, plan), elevation)!["data"]!["depth"]!;
        // Moved 6 m north from where the plan draws it: inside the house.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(elevation), ["op"] = "move", ["by"] = new JsonArray(0, 6) });
        Assert.Equal(at + 6, f.Model.Get(elevation)!.Num("markerDepth"), 6);
        Assert.Null(Mark(Drawing(f, elevation), south));
        f.Run("element.pin", new JsonObject { ["ids"] = new JsonArray(elevation) });
        Assert.Throws<CommandException>(() => f.Run("view.moveMarker", new JsonObject { ["id"] = elevation, ["depth"] = -6 }));
    }

    [Fact]
    public void Opening_symbols_show_only_where_the_elevation_sees()
    {
        var f = F();
        var (south, _, _, _, elevation) = House(f);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = south, ["x"] = 3, ["y"] = 0 })!["id"]!;
        Assert.NotNull(Mark(Drawing(f, elevation), window));
        // Past the elevation's reach, no symbol.
        f.Run("view.setExtent", new JsonObject { ["id"] = elevation, ["min"] = 5, ["max"] = 10 });
        Assert.Null(Mark(Drawing(f, elevation), window));
        f.Run("view.setExtent", new JsonObject { ["id"] = elevation, ["min"] = null, ["max"] = null });
        // Before the marker's line, inside the house, neither.
        f.Run("view.moveMarker", new JsonObject { ["id"] = elevation, ["depth"] = 4 });
        Assert.Null(Mark(Drawing(f, elevation), window));
    }
}
