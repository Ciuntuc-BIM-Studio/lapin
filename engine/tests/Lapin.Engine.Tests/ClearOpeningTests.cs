using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The clear opening and the wall opening of doors and windows (forum topic 204): Polish and Czech drawings write a door's size
/// inside its frame (800/1970), not the hole in the wall. <c>{clearWidth}</c>, <c>{clearHeight}</c> beside <c>{width}</c> and
/// <c>{height}</c>, which stay as they were, and <c>{openingWidth}</c>, <c>{openingHeight}</c> for the wall opening; in markers
/// (in the tag type's unit), tag symbols, tag rules (metres) and as worked-out fields (mm) for schedules and other tags.
/// </summary>
public sealed class ClearOpeningTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static List<PenText> Texts(Fixture f, string view) => Views.Render(new Q(f), Views.Find(f.Model, view)!, print: true).Pens.SelectMany(p => p.Texts).ToList();

    /// <summary>A door 900 × 2100 with a 60 mm frame and a 20 mm threshold, a window 1200 × 1500 with a 60 mm frame, in one wall.</summary>
    static (Fixture F, string Door, string Window, string Plan) Project()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Drafting.DraftingModule());
        var wall = f.Wall(0, 0, 6, 0);
        var doorSpec = JoinerySpec.DefaultDoor("Door 90") with { Frame = new JoineryFrame(0.1, 0.06, 0.06, 0.06, 0) };
        var doorType = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = doorSpec.ToJson() })!["id"]!;
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5, ["typeId"] = doorType })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4 })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        return (f, door, window, plan);
    }

    [Fact]
    public void The_clear_opening_is_the_size_inside_the_frame()
    {
        // A door: both jambs and the head off, its threshold under (it has no bottom member): 900 − 2·60, 2100 − 60 − 20.
        var door = JoinerySpec.DefaultDoor() with { Frame = new JoineryFrame(0.1, 0.06, 0.06, 0.06, 0) };
        var (w, h) = Joinery.ClearOpening(door);
        Assert.Equal(0.78, w, 9);
        Assert.Equal(2.02, h, 9);
        // Without a threshold, the head alone: 2100 − 60.
        Assert.Equal(2.04, Joinery.ClearOpening(door with { Threshold = 0 }).Height, 9);
        // A window: its bottom frame member too: the default window 1200 × 1500, frame 60 mm → 1080 × 1380.
        var window = JoinerySpec.DefaultWindow();
        Assert.Equal((1.08, 1.38), (Math.Round(Joinery.ClearOpening(window).Width, 9), Math.Round(Joinery.ClearOpening(window).Height, 9)));
        // A round window: the ring's inside both ways.
        var round = window with { Width = 0.9, Height = 0.9, Shape = new JoineryShape("circle") };
        Assert.Equal((0.78, 0.78), (Math.Round(Joinery.ClearOpening(round).Width, 9), Math.Round(Joinery.ClearOpening(round).Height, 9)));
        // A round-headed one: up to the head's inner apex at its middle (a half circle 1200 wide springs at 1500 − 600 = 900, apex 900 + 540).
        var arched = window with { Height = 1.5, Shape = new JoineryShape("round") };
        Assert.Equal(0.9 + 0.54 - 0.06, Joinery.ClearOpening(arched).Height, 9);
    }

    [Fact]
    public void A_marker_writes_the_clear_and_the_wall_opening_in_its_unit_and_width_height_stay()
    {
        var (f, door, window, plan) = Project();
        f.Run("architecture.tagOpenings", new JsonObject { ["viewId"] = plan });
        // The default markers say what they said: the door's width/height in cm.
        Assert.Contains(Texts(f, plan), t => t.Text == "90/210");
        // The Czech way: the clear size in mm, 780/2020; the wall opening beside it.
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "door", ["inner"] = "{clearWidth}/{clearHeight}|{openingWidth}x{openingHeight}", ["unit"] = "mm" } });
        var texts = Texts(f, plan);
        Assert.Contains(texts, t => t.Text == "780/2020");
        Assert.Contains(texts, t => t.Text == "900x2100");
        // A window in cm: 108/138, its sill over the finished floor (none here: the structural sill, 90).
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "window", ["inner"] = "{clearWidth}/{clearHeight}|{sillFinished}" } });
        texts = Texts(f, plan);
        Assert.Contains(texts, t => t.Text == "108/138");
        Assert.Contains(texts, t => t.Text == "90");
        // Over the finished floor of its wall, 50 mm above its base, whatever the sill is measured from.
        var wall = Openings.WallOf(f.Model, Openings.OpeningOf(f.Model, window)!.Id)!;
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall.Id, ["finishedFloor"] = 0.05 });
        Assert.Contains(Texts(f, plan), t => t.Text == "85");
        Assert.NotNull(door);
    }

    [Fact]
    public void Tag_rules_read_the_clear_opening_in_metres_and_fields_in_millimetres()
    {
        var (f, door, window, _) = Project();
        var d = f.Model.Get(door)!;
        Assert.Equal(0.78, OpeningMarkers.Measure(f.Model, d, "clearWidth")!.Value, 9);
        Assert.Equal(2.02, OpeningMarkers.Measure(f.Model, d, "clear height")!.Value, 9);
        Assert.Equal(0.9, OpeningMarkers.Measure(f.Model, d, "openingWidth")!.Value, 9);
        Assert.Equal(2.1, OpeningMarkers.Measure(f.Model, d, "OpeningHeight")!.Value, 9);
        Assert.Equal(0.9, OpeningMarkers.Measure(f.Model, d, "width")!.Value, 9);
        Assert.Null(OpeningMarkers.Measure(f.Model, d, "depth"));
        // A rule over it: doors narrower than 800 clear get another type.
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "narrow", ["name"] = "Narrow door", ["template"] = "!{mark}", ["shape"] = "opening", ["classes"] = "IfcDoor" } });
        f.Run("standards.set", new JsonObject { ["kind"] = "tagRule", ["entry"] = new JsonObject { ["id"] = "r1", ["class"] = "IfcDoor", ["when"] = "clearWidth < 0.8", ["tagType"] = "narrow" } });
        Assert.Equal("narrow", TagRules.RuleType(f.Model, DraftingResources.Book(f.Model), d)?.Id);
        // As fields, in mm: what box tags, labels, schedules and formulas read.
        Assert.Equal("780", TagText.Value(f.Model, d, "clearWidth"));
        Assert.Equal("2020", TagText.Value(f.Model, d, "clearHeight"));
        Assert.Equal("900", TagText.Value(f.Model, d, "openingWidth"));
        Assert.Equal("2100", TagText.Value(f.Model, d, "openingHeight"));
        Assert.Equal("1080", TagText.Value(f.Model, f.Model.Get(window)!, "clearWidth"));
        Assert.Contains(DerivedFields.Of(IfcClass.Door), x => x.Key == "clearWidth");
        Assert.Contains(DerivedFields.Of(IfcClass.Window), x => x.Key == "openingHeight");
    }

    [Fact]
    public void The_clear_height_leaves_the_threshold_in_where_the_standards_say_so()
    {
        // Lapin's default as before: 900 × 2100, frame 60, threshold 20 → 780/2020.
        var door = JoinerySpec.DefaultDoor() with { Frame = new JoineryFrame(0.1, 0.06, 0.06, 0.06, 0) };
        Assert.Equal(0.78, Joinery.ClearOpening(door).Width, 9);
        Assert.Equal(2.02, Joinery.ClearOpening(door).Height, 9);
        Assert.Equal(2.02, Joinery.ClearOpening(door, lessThreshold: true).Height, 9);
        // The Czech rule (forum topic 204): the void less the frame only, whatever the threshold: 2050, frame 50 → 2000.
        var cz = JoinerySpec.DefaultDoor() with { Height = 2.05, Frame = new JoineryFrame(0.1, 0.05, 0.06, 0.06, 0), Threshold = 0.03 };
        Assert.Equal(2.0, Joinery.ClearOpening(cz, lessThreshold: false).Height, 9);
        Assert.Equal(2.0, Joinery.ClearOpening(cz with { Threshold = 0 }, lessThreshold: false).Height, 9);
        Assert.Equal(2.04, Joinery.ClearOpening(door, lessThreshold: false).Height, 9);
        // A window's bottom member stays subtracted either way: it is the frame.
        var window = JoinerySpec.DefaultWindow();
        Assert.Equal(1.38, Joinery.ClearOpening(window, lessThreshold: false).Height, 9);
    }

    [Fact]
    public void The_drafting_standards_option_reaches_tags_rules_and_fields_and_is_one_undo_step()
    {
        var (f, door, window, plan) = Project();
        var d = f.Model.Get(door)!;
        Assert.True(DraftingResources.Book(f.Model).ClearHeightLessThreshold is null);
        Assert.Equal("2020", TagText.Value(f.Model, d, "clearHeight"));
        f.Run("architecture.tagOpenings", new JsonObject { ["viewId"] = plan });
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "door", ["inner"] = "{clearWidth}/{clearHeight}", ["unit"] = "mm" } });
        Assert.Contains(Texts(f, plan), t => t.Text == "780/2020");
        // The Czech way: the threshold is not taken off.
        f.Run("standards.options", new JsonObject { ["clearHeightLessThreshold"] = false });
        Assert.False(DraftingResources.Book(f.Model).ClearHeightLessThreshold);
        Assert.Equal("2040", TagText.Value(f.Model, f.Model.Get(door)!, "clearHeight"));
        Assert.Equal(2.04, OpeningMarkers.Measure(f.Model, f.Model.Get(door)!, "clearHeight")!.Value, 9);
        Assert.Contains(Texts(f, plan), t => t.Text == "780/2040");
        // The width and a window are as they were.
        Assert.Equal("780", TagText.Value(f.Model, f.Model.Get(door)!, "clearWidth"));
        Assert.Equal("1380", TagText.Value(f.Model, f.Model.Get(window)!, "clearHeight"));
        // One undo step back to Lapin's default; true (the default) stores nothing.
        f.Session.Undo();
        Assert.Equal("2020", TagText.Value(f.Model, f.Model.Get(door)!, "clearHeight"));
        f.Run("standards.options", new JsonObject { ["clearHeightLessThreshold"] = false });
        f.Run("standards.options", new JsonObject { ["clearHeightLessThreshold"] = true });
        Assert.Null(Spatial.Project(f.Model)!.Attrs["drafting"]?["clearHeightLessThreshold"]);
        Assert.Throws<CommandException>(() => f.Run("standards.options", new JsonObject { ["clearHeightLessThreshold"] = "no" }));
        // A standards file carries it to another project with the options.
        f.Run("standards.options", new JsonObject { ["clearHeightLessThreshold"] = false });
        var text = (string)f.Session.Query("standards.export", new JsonObject())!["text"]!;
        var (g, gDoor, _, _) = Project();
        g.Run("standards.importFrom", new JsonObject { ["text"] = text, ["options"] = true });
        Assert.Equal("2040", TagText.Value(g.Model, g.Model.Get(gDoor)!, "clearHeight"));
    }

    [Fact]
    public void A_door_type_draws_its_threshold_in_plan_when_it_says_so()
    {
        // Today's plan draws no threshold (it lies under the cut): a type that says nothing draws as before.
        var door = JoinerySpec.DefaultDoor();
        Assert.Empty(JoinerySymbols.ThresholdPlan(door, 0.3));
        // Shown: the frame's bottom member seen under the cut, between the jambs, as deep as the frame.
        var shown = door with { ThresholdInPlan = true };
        var frame = Assert.Single(JoinerySymbols.ThresholdPlan(shown, 0.3));
        Assert.Equal("outline", frame.Weight);
        var (mn, mx) = JoinerySymbols.Bounds([frame]);
        Assert.Equal(door.Width - 2 * door.Frame.Width, mx.X - mn.X, 6);
        Assert.Equal(door.Frame.Depth, mx.Y - mn.Y, 6);
        // A detailed threshold across the wall: as wide as the opening, as deep as the wall.
        var detailed = shown with { Details = new JoineryDetails(Threshold: new JoineryThreshold(On: true)) };
        (mn, mx) = JoinerySymbols.Bounds(JoinerySymbols.ThresholdPlan(detailed, 0.3));
        Assert.Equal((door.Width, 0.3), (Math.Round(mx.X - mn.X, 6), Math.Round(mx.Y - mn.Y, 6)));
        // None to draw: no threshold, or a window.
        Assert.Empty(JoinerySymbols.ThresholdPlan(shown with { Threshold = 0 }, 0.3));
        Assert.Empty(JoinerySymbols.ThresholdPlan(JoinerySpec.DefaultWindow() with { ThresholdInPlan = true }, 0.3));
        Assert.Empty(JoinerySymbols.ThresholdPlan(door with { ThresholdInPlan = false }, 0.3));

        // In a project: the door's plan gains the threshold's outline at LOD 200 and up, nothing else changes.
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 6, 0);
        string Door(JoinerySpec s, double along)
        {
            var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = s.ToJson() })!["id"]!;
            return (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = along, ["typeId"] = type })!["id"]!;
        }
        var plain = f.Session.Geometry(Door(door, 1.5))!.Plan!;
        var drawn = f.Session.Geometry(Door(shown, 4))!.Plan!;
        Assert.Equal(plain.Count + 1, drawn.Count);
        Assert.Contains(drawn, i => i.Closed && i.MinDetail == PlanDetail.Medium && i.MaxDetail == PlanDetail.Fine);
        // The spec keeps nothing new until asked: its JSON as before.
        Assert.Null(door.ToJson()["thresholdInPlan"]);
        Assert.True((bool)shown.ToJson()["thresholdInPlan"]!);
        Assert.True(JoinerySpec.From(shown.ToJson())!.ThresholdInPlan);
    }
}
