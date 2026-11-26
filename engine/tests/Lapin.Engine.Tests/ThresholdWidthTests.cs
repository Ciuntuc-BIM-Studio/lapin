using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 204, LVK's post 8: a door's threshold (its sill) has a width across the wall, as a window's sill has its projection,
/// centred on the frame and moved to one side by an offset; empty, across the whole wall as before. Seen in plan it runs between
/// the frame's jambs, not through them. His post 6: the frame's face width and depth as tag and formula tokens.
/// </summary>
public sealed class ThresholdWidthTests
{
    static (Vec3 Min, Vec3 Max) Bounds(GeometryPart p) => Measure.Bounds(p.Solid)!.Value;

    /// <summary>A 900 door, frame 60 wide and 100 deep on the wall's middle, in plan.</summary>
    static JoinerySpec Door(JoineryThreshold? th) => JoinerySpec.DefaultDoor("D") with
    {
        Frame = new JoineryFrame(0.1, 0.06, 0.06, 0.06, 0),
        Details = th == null ? null : new JoineryDetails(Threshold: th),
        ThresholdInPlan = true,
    };

    [Fact]
    public void Without_a_width_the_threshold_is_drawn_and_built_as_before()
    {
        var th = new JoineryThreshold(On: true);
        Assert.Equal((-0.15, 0.15), th.Across(Door(th), 0.3));
        Assert.Equal((-0.15, 0.15), (th with { Width = 0 }).Across(Door(th), 0.3));
        var plan = Assert.Single(JoinerySymbols.ThresholdPlan(Door(th), 0.3));
        Assert.True(plan.Closed);
        var (mn, mx) = JoinerySymbols.Bounds([plan]);
        Assert.Equal((0.9, 0.3), (Math.Round(mx.X - mn.X, 6), Math.Round(mx.Y - mn.Y, 6)));
        // Its outline steps in to the jambs where they stand on it: between them across the frame's depth (LVK's green lines).
        Assert.Contains(plan.Pts, p => Math.Abs(Math.Abs(p.X) - 0.39) < 1e-9 && Math.Abs(Math.Abs(p.Y) - 0.05) < 1e-9);
        Assert.Equal(12, plan.Pts.Count);
        // The spec's JSON keeps nothing new.
        var json = Door(th).ToJson()["details"]!["threshold"]!.AsObject();
        Assert.False(json.ContainsKey("width"));
        Assert.False(json.ContainsKey("offset"));
    }

    [Fact]
    public void A_width_sets_its_extent_across_the_wall_centred_on_the_frame_and_an_offset_moves_it()
    {
        // As deep as the frame: between the jambs only, a rectangle 780 × 100.
        var th = new JoineryThreshold(On: true, Width: 0.1);
        var plan = Assert.Single(JoinerySymbols.ThresholdPlan(Door(th), 0.3));
        var (mn, mx) = JoinerySymbols.Bounds([plan]);
        Assert.Equal((-0.39, -0.05, 0.39, 0.05), (Math.Round(mn.X, 6), Math.Round(mn.Y, 6), Math.Round(mx.X, 6), Math.Round(mx.Y, 6)));
        Assert.Equal(4, plan.Pts.Count);
        // 200 wide, 50 towards the exterior: from −50 to +150, the wall's exterior face (LVK's blue arrow); the opening's width beside the frame.
        var out_ = th with { Width = 0.2, Offset = 0.05 };
        (mn, mx) = JoinerySymbols.Bounds(JoinerySymbols.ThresholdPlan(Door(out_), 0.3));
        Assert.Equal((-0.45, -0.05, 0.45, 0.15), (Math.Round(mn.X, 6), Math.Round(mn.Y, 6), Math.Round(mx.X, 6), Math.Round(mx.Y, 6)));
        // Kept within the wall's faces.
        Assert.Equal((0.0, 0.15), (th with { Width = 0.4, Offset = 0.2 }).Across(Door(th), 0.3));
        // The frame moved across the wall: the threshold goes with it.
        var moved = Door(th) with { Frame = Door(th).Frame with { Offset = -0.1 } };
        Assert.Equal((-0.15, -0.05), (Math.Round(th.Across(moved, 0.3).Y0, 6), Math.Round(th.Across(moved, 0.3).Y1, 6)));
        // Read back from the type's JSON.
        Assert.Equal(0.2, JoinerySpec.From(Door(out_).ToJson())!.Details!.Threshold!.Width);
        Assert.Equal(0.05, JoinerySpec.From(Door(out_).ToJson())!.Details!.Threshold!.Offset);
    }

    [Fact]
    public void The_threshold_in_3D_follows_its_width_in_a_wall()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 6, 0);
        var bands = Walls.Section(f.Model, f.Model.Get(wall)!);
        var t = bands.Max(b => b.To) - bands.Min(b => b.From);
        string Add(JoineryThreshold th, double along) => (string)f.Run("architecture.addDoor", new JsonObject
        {
            ["wallId"] = wall, ["along"] = along,
            ["typeId"] = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = Door(th).ToJson() })!["id"]!,
        })!["id"]!;
        GeometryPart Threshold(string id) => f.Session.Geometry(id)!.Parts.Single(p => p.Key == "threshold");
        // As before: across the whole wall.
        var whole = Bounds(Threshold(Add(new JoineryThreshold(On: true), 1.5)));
        Assert.Equal(t, whole.Max.Y - whole.Min.Y, 6);
        // 120 wide: 120 across the wall, as long as the opening.
        var narrow = Bounds(Threshold(Add(new JoineryThreshold(On: true, Width: 0.12), 4)));
        Assert.Equal(0.12, narrow.Max.Y - narrow.Min.Y, 6);
        Assert.Equal(0.9, narrow.Max.X - narrow.Min.X, 6);
        Assert.Equal((whole.Min.Y + whole.Max.Y) / 2, (narrow.Min.Y + narrow.Max.Y) / 2, 6);
        // Its length stays the opening's.
        var door = f.Model.OfClass(IfcClass.Door).Last();
        var q = f.Session.Quantities(door.Id).Single(s => s.Name == "Lapin_Details").Quantities.ToDictionary(x => x.Name, x => x.Value);
        Assert.Equal(0.9, q["ThresholdLength"], 6);
    }

    [Fact]
    public void Tags_and_formulas_read_the_frame_width_and_depth()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Drafting.DraftingModule());
        var wall = f.Wall(0, 0, 6, 0);
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = Door(null).ToJson() })!["id"]!;
        var door = f.Model.Get((string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5, ["typeId"] = type })!["id"]!)!;
        Assert.Equal(0.06, OpeningMarkers.Measure(f.Model, door, "frameWidth")!.Value, 9);
        Assert.Equal(0.1, OpeningMarkers.Measure(f.Model, door, "frame depth")!.Value, 9);
        Assert.Equal("60", TagText.Value(f.Model, door, "frameWidth"));
        Assert.Equal("100", TagText.Value(f.Model, door, "frameDepth"));
        // LVK's question: the wall opening's height less the frame, in a formula.
        Assert.Equal("2040", TagText.Value(f.Model, door, "= openingHeight - frameWidth"));
        Assert.Contains(DerivedFields.Of(IfcClass.Door), x => x.Key == "frameWidth");
        Assert.Contains(DerivedFields.Of(IfcClass.Window), x => x.Key == "frameDepth");
        // A window's too.
        var window = f.Model.Get((string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4 })!["id"]!)!;
        Assert.Equal(JoinerySpec.DefaultWindow().Frame.Width, OpeningMarkers.Measure(f.Model, window, "frameWidth")!.Value, 9);
    }
}
