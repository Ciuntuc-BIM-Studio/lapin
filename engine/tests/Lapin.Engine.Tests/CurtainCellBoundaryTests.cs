using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Report #133, the user's answers: a curtain wall door drawn in elevations with the opening symbols every door and window has
/// there; a door in a cell trimmed by the wall's drawn boundary (#127) wherever its rectangle fits under it, the transom and
/// the glass over it following the boundary, refused with why only where it does not fit; a framed panel in a trimmed cell
/// framed along its outline.
/// </summary>
public class CurtainCellBoundaryTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    /// <summary>
    /// A 6 m curtain wall of bays at most 1.5 m and no transom, under a gable 3.6 m at its ends and 4.2 m in the middle: every
    /// cell is trimmed by the boundary (panel:i:top), 50 mm under it (the edge member).
    /// </summary>
    static string Gable(Fixture f)
    {
        var type = (string)f.Run("architecture.createCurtainWallType", new JsonObject
        {
            ["name"] = "Gable front",
            ["spec"] = new JsonObject { ["u"] = JsonNode.Parse("""{"mode":"max","spacing":1.5}"""), ["v"] = JsonNode.Parse("""{"mode":"count","count":1}""") },
        })!["id"]!;
        var id = (string)f.Run("architecture.addCurtainWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["height"] = 3, ["typeId"] = type })!["id"]!;
        f.Run("architecture.setCurtainWallProfile", new JsonObject { ["ids"] = new JsonArray(id), ["top"] = JsonNode.Parse("[[0,3.6],[3,4.2],[6,3.6]]") });
        return id;
    }

    /// <summary>The cell's top under the gable at u (along the wall), on the left half: 3.6 m rising 0.2 m a metre, less the edge member.</summary>
    static double Top(double u) => 3.6 + 0.2 * u - 0.05;

    static (Vec3 A, Vec3 B) Part(Fixture f, string id, string key) => Measure.Bounds(f.Session.Geometry(id)!.Parts.Single(p => p.Key == key).Solid)!.Value;
    static HashSet<string> Keys(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Select(p => p.Key!).ToHashSet();

    static string Door(Fixture f, string id, string key, JsonObject? more = null)
    {
        var a = new JsonObject { ["id"] = id, ["keys"] = new JsonArray(key) };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("architecture.addCurtainDoor", a)!["ids"]![0]!;
    }

    [Fact]
    public void A_door_filling_a_cell_under_a_slope_stops_a_transom_under_its_lower_side_and_the_glass_over_it_follows_the_slope()
    {
        var f = F();
        var id = Gable(f);
        Assert.Contains("panel:1:top", Keys(f, id));
        var door = Door(f, id, "panel:1:top");
        var keys = Keys(f, id);
        Assert.DoesNotContain("panel:1:top", keys);
        Assert.DoesNotContain("transom:1:0", keys);
        // Its width the cell's (1.525 to 2.975), its head a transom under the boundary's lowest point over it.
        var (a, b) = f.Bounds(door);
        Assert.Equal(1.525, a.X, 6);
        Assert.Equal(2.975, b.X, 6);
        Assert.Equal(0, a.Z, 6);
        Assert.Equal(Top(1.525) - 0.05, b.Z, 6);
        var (ta, tb) = Part(f, id, "transom:1:top:head");
        Assert.Equal(b.Z, ta.Z, 6);
        Assert.Equal(Top(1.525), tb.Z, 6);
        // The glass over it rises with the boundary: from the transom's top to the boundary at the cell's higher side.
        var (ga, gb) = Part(f, id, "panel:1:top:head");
        Assert.Equal(Top(1.525), ga.Z, 6);
        Assert.Equal(Top(2.975), gb.Z, 6);
        Assert.Null(f.Session.Geometry(door)!.Diagnostics);
    }

    [Fact]
    public void A_smaller_door_under_a_slope_has_mullions_up_to_the_boundary_and_glass_beside_and_over_it()
    {
        var f = F();
        var id = Gable(f);
        var type = (string)f.Run("architecture.createCurtainDoorType", new JsonObject { ["width"] = 1.0, ["height"] = 2.2 })!["id"]!;
        var door = Door(f, id, "panel:1:top", new JsonObject { ["typeId"] = type, ["fit"] = false });
        var (a, b) = f.Bounds(door);
        Assert.Equal(1.75, a.X, 6);
        Assert.Equal(2.75, b.X, 6);
        Assert.Equal(2.2, b.Z, 6);
        // Each mullion from the floor to the boundary over it.
        var (la, lb) = Part(f, id, "mullion:1:top:left");
        Assert.Equal(1.70, la.X, 6);
        Assert.Equal(0, la.Z, 6);
        Assert.Equal(Top(1.75), lb.Z, 6);
        var (_, rb) = Part(f, id, "mullion:1:top:right");
        Assert.Equal(Top(2.80), rb.Z, 6);
        // The panes beside it reach the boundary; the transom spans the door; the glass over it follows the slope.
        Assert.Equal(Top(1.70), Part(f, id, "panel:1:top:left").B.Z, 6);
        Assert.Equal(Top(2.975), Part(f, id, "panel:1:top:right").B.Z, 6);
        var (ta, tb) = Part(f, id, "transom:1:top:head");
        Assert.Equal((1.75, 2.75, 2.2, 2.25), (Math.Round(ta.X, 6), Math.Round(tb.X, 6), Math.Round(ta.Z, 6), Math.Round(tb.Z, 6)));
        Assert.Equal(Top(2.75), Part(f, id, "panel:1:top:head").B.Z, 6);
    }

    [Fact]
    public void A_door_that_does_not_fit_under_the_boundary_is_refused_saying_so()
    {
        var f = F();
        var id = Gable(f);
        var tall = (string)f.Run("architecture.createCurtainDoorType", new JsonObject { ["width"] = 1.0, ["height"] = 3.6 })!["id"]!;
        var e = Assert.Throws<CommandException>(() => Door(f, id, "panel:0:top", new JsonObject { ["typeId"] = tall, ["fit"] = false }));
        Assert.Contains("boundary", e.Message);
        // Under the higher middle it fits (the boundary over it at 3.9 m and more, room for the transom too).
        var ok = Door(f, id, "panel:1:top", new JsonObject { ["typeId"] = tall, ["fit"] = false, ["align"] = "right" });
        Assert.Equal(3.6, f.Bounds(ok).Max.Z, 6);
        // Lowered under the door later: the door says why and keeps its settings.
        f.Run("architecture.setCurtainWallProfile", new JsonObject { ["ids"] = new JsonArray(id), ["top"] = JsonNode.Parse("[[0,3.2],[6,3.2]]") });
        Assert.Contains(f.Session.Geometry(ok)!.Diagnostics!, d => d.Contains("boundary"));
    }

    [Fact]
    public void A_framed_panel_in_a_trimmed_cell_is_framed_along_its_outline()
    {
        var f = F();
        var id = Gable(f);
        var framed = (string)f.Run("architecture.createCurtainPanelType", new JsonObject
        {
            ["name"] = "Framed", ["panel"] = new JsonObject { ["kind"] = "framed", ["thickness"] = 0.03, ["frameWidth"] = 0.06, ["frameDepth"] = 0.08, ["inset"] = 0.01, ["infill"] = "glass" },
        })!["id"]!;
        f.Run("architecture.setCurtainPanels", new JsonObject { ["id"] = id, ["keys"] = new JsonArray("panel:1:top"), ["typeId"] = framed });
        var (fa, fb) = Part(f, id, "frame:1:top");
        Assert.Equal(1.525, fa.X, 6);
        Assert.Equal(Top(2.975), fb.Z, 6);
        // The infill inside the frame: a frame's width in from every side, the sloped one too.
        var (ia, ib) = Part(f, id, "panel:1:top");
        Assert.Equal(1.585, ia.X, 6);
        Assert.Equal(0.11, ia.Z, 6);
        Assert.True(ib.Z < fb.Z - 0.06 && ib.Z > fb.Z - 0.08, $"the infill's top {ib.Z} a frame's width under {fb.Z}");
        // Less than the whole outline: the frame is a ring.
        var whole = (2.975 - 1.525) * ((Top(1.525) + Top(2.975)) / 2 - 0.05) * 0.08;
        Assert.True(Measure.Volume(f.Session.Geometry(id)!.Parts.Single(p => p.Key == "frame:1:top").Solid) < whole * 0.3);
    }

    static List<(double X0, double Y0, double X1, double Y1)> Joinery(JsonObject d) =>
        d["pens"]!.AsArray().OfType<JsonObject>().Where(p => (string)p["layer"]! == "JOINERY" && p["segments"] is JsonArray)
            .SelectMany(p => { var a = (JsonArray)p["segments"]!; return Enumerable.Range(0, a.Count / 4).Select(i => ((double)a[4 * i]!, (double)a[4 * i + 1]!, (double)a[4 * i + 2]!, (double)a[4 * i + 3]!)); }).ToList();

    [Fact]
    public void A_curtain_wall_door_is_marked_in_the_elevation_its_exterior_faces_as_any_door()
    {
        var f = F();
        var id = Gable(f);
        var type = (string)f.Run("architecture.createCurtainDoorType", new JsonObject { ["width"] = 1.0, ["height"] = 2.2 })!["id"]!;
        Door(f, id, "panel:1:top", new JsonObject { ["typeId"] = type, ["fit"] = false });
        // The exterior is the wall's right: south. The south elevation sees its swing, the north one does not.
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var north = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "north" })!["id"]!;
        var marks = Joinery((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = south })!);
        Assert.NotEmpty(marks);
        Assert.All(marks, s => Assert.True(Math.Min(s.Y0, s.Y1) >= -1e-6 && Math.Max(s.Y0, s.Y1) <= 2.2 + 1e-6, $"a mark at {s.Y0}..{s.Y1}"));
        Assert.Empty(Joinery((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = north })!));
        // Flipped to open the other way: the north elevation has it.
        var door = f.Model.RelatedOf(id, IfcClass.RelAggregates).Single(e => e.Class == IfcClass.Door).Id;
        f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = new JsonArray(door), ["axis"] = "y" });
        Assert.NotEmpty(Joinery((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = north })!));
    }
}
