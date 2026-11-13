using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Doors and windows in one object (forum topic 34, testers Robby and LVK): door leaves and window sashes in one frame, a door
/// when it has a leaf (one mark, one schedule line); a bottom rail under a sash in a door, boards only over a parapet panel;
/// every panel in IFC with its own operation (sashes USERDEFINED, their window terms in Lapin_Joinery), read back; window sashes'
/// swings in plan (auto: dashed for those reaching the floor); lift-and-slide (HS) and tilt-and-slide leaves; a glazing build-up
/// setting the glass's thickness, in Lapin_Joinery and Pset_DoorWindowGlazingType.
/// </summary>
public class DoorWindowCombinationTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-combo").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static readonly JoinerySpec[] Presets = [JoinerySpecs.BalconyDoorWindow, JoinerySpecs.DoorWithSidelights, JoinerySpecs.LiftSlidePortal, JoinerySpecs.FrenchWindow];

    static (Fixture F, string Wall, string Fill, string Type) Placed(JoinerySpec spec, double sill = 0)
    {
        var f = new Fixture();
        var type = f.Type("Block 300", ("Plaster", 0.015, false), ("Block", 0.3, true), ("Render", 0.02, false));
        var wall = f.Wall(0, 0, 8, 0, type);
        var typeId = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        var kind = f.Session.Model.Get(typeId)!.Class == IfcClass.DoorType ? "addDoor" : "addWindow";
        var fill = (string)f.Run($"architecture.{kind}", new JsonObject { ["wallId"] = wall, ["along"] = 4, ["typeId"] = typeId, ["sill"] = sill })!["id"]!;
        return (f, wall, fill, typeId);
    }

    /// <summary>The balcony door's shape built from a window: a sash beside a door leaf.</summary>
    static JoinerySpec WindowWithLeaf => JoinerySpec.DefaultWindow("Combo") with
    {
        SillHeight = 0, Width = 1.6, Height = 2.2, Columns = [1, 1], Rows = [1],
        Panels = [new(0, 0, "swing-left"), new(1, 0, "side-right")],
    };

    [Fact]
    public void A_leaf_makes_it_a_door_sashes_without_one_a_window()
    {
        Assert.Equal("door", Joinery.KindOf(WindowWithLeaf));
        Assert.Equal("window", Joinery.KindOf(JoinerySpec.DefaultDoor() with { Panels = [new(0, 0, "side-left")] }));
        // A fixed light keeps what it was made as.
        Assert.Equal("door", Joinery.KindOf(JoinerySpec.DefaultDoor() with { Panels = [new(0, 0, "fixed")] }));
        Assert.Equal("window", Joinery.KindOf(JoinerySpec.DefaultWindow() with { Panels = [new(0, 0, "fixed"), new(1, 0, "fixed")] }));

        // Made from the configurator: a window with a leaf is a door type, its element an IfcDoor with one mark.
        var (f, _, fill, typeId) = Placed(WindowWithLeaf);
        Assert.Equal(IfcClass.DoorType, f.Session.Model.Get(typeId)!.Class);
        Assert.Equal(IfcClass.Door, f.Session.Model.Get(fill)!.Class);
        Assert.Equal("W1", f.Session.Model.Get(fill)!.Name);
        // A window type cannot take a leaf in place: its class stays.
        var windowType = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = JoinerySpec.DefaultWindow().ToJson() })!["id"]!;
        var ex = Assert.ThrowsAny<Exception>(() => f.Run("architecture.updateJoineryType", new JsonObject { ["id"] = windowType, ["spec"] = WindowWithLeaf.ToJson() }));
        Assert.Contains("save it as a new type", ex.Message);
    }

    [Fact]
    public void Every_preset_builds_closed_bodies_in_a_closed_wall()
    {
        foreach (var spec in Presets)
        {
            Assert.Null(Joinery.Problem(spec));
            var (f, wall, fill, _) = Placed(spec);
            Assert.Equal(spec.Kind == "door" ? IfcClass.Door : IfcClass.Window, f.Session.Model.Get(fill)!.Class);
            var parts = f.Session.Geometry(fill)!.Parts;
            Assert.Contains(parts, p => p.Key == "glass");
            foreach (var p in parts.Concat(f.Session.Geometry(wall)!.Parts))
                Assert.True(Topology.IsManifold(p.Solid), $"{spec.Name} {p.Key ?? p.Name}: {string.Join("; ", Topology.Validate(p.Solid).Select(d => d.Message))}");
            // Mirrored, too.
            Assert.Null(Joinery.Problem(Joinery.Mirrored(spec)));
        }
    }

    [Fact]
    public void A_sash_in_a_door_stands_on_a_rail_and_boards_run_only_over_a_parapet()
    {
        // A leaf beside a sash to the floor: the sash's bottom is the rail's top, the frame's width over the threshold.
        var door = WindowWithLeaf with { Kind = "door", Threshold = 0.02, Board = new JoineryBoard(0.2, 0.05) };
        var sash = Joinery.Normalised(door).Single(p => p.Operation == "side-right");
        var leaf = Joinery.Normalised(door).Single(p => p.Operation == "swing-left");
        Assert.Equal(0.02 + door.Frame.Width, Joinery.PanelRect(door, sash).Z0, 9);
        Assert.Equal(0.02, Joinery.PanelRect(door, leaf).Z0, 9);
        var boxes = Joinery.Boxes(door);
        var r = Joinery.PanelRect(door, sash);
        Assert.Contains(boxes, b => b.Role == "frame" && Math.Abs(b.Min.X - r.X0) < 1e-9 && Math.Abs(b.Max.X - r.X1) < 1e-9 && Math.Abs(b.Max.Z - r.Z0) < 1e-9 && Math.Abs(b.Min.Z - 0.02) < 1e-9);
        // Glazing to the floor: no window boards.
        Assert.DoesNotContain(boxes, b => b.Role == "board");

        // The balcony door's window over its parapet panel: both boards on the transom over it, across that column only.
        var balcony = JoinerySpecs.BalconyDoorWindow;
        var parapet = Joinery.Normalised(balcony).Single(p => p.Operation == "fixed");
        var top = Joinery.ParapetTop(balcony, parapet)!.Value;
        var boards = Joinery.Boxes(balcony).Where(b => b.Role == "board").ToList();
        Assert.Equal(2, boards.Count);
        Assert.All(boards, b => Assert.Equal(top, b.Max.Z, 9));
        Assert.All(boards, b => Assert.Equal(balcony.Width / 2, b.Max.X, 9));
        Assert.All(boards, b => Assert.True(b.Min.X > -balcony.Width / 2 + 0.5));
        // The sash in a door gets a window's handle, the leaf the door's lever.
        Assert.Equal("window", Joinery.HandleStyle(balcony, "tilt-turn-right"));
        Assert.Equal("lever", Joinery.HandleStyle(balcony, "swing-left"));
    }

    [Fact]
    public void Ifc_has_every_panel_with_its_own_operation_and_reads_it_back()
    {
        var t = JoineryIfc.Of(JoinerySpecs.BalconyDoorWindow);
        Assert.True(t.Door);
        // One leaf, its window part beside it in two panes: a single swing door in IFC's terms, the panels say the rest.
        Assert.Equal("SINGLE_SWING_LEFT", t.Operation);
        Assert.Equal(["SWINGING", "FIXEDPANEL", "USERDEFINED"], t.Panels.Select(p => p.Operation));
        Assert.Equal("SWINGING; FIXEDPANEL; TILTANDTURNRIGHTHAND", t.Properties!.Single(p => p.Name == "PanelOperations").Value);
        Assert.Equal("4-16-4", t.Properties!.Single(p => p.Name == "Glazing").Value);
        // Mirrored: the sash's hand too.
        Assert.Equal("SWINGING; FIXEDPANEL; TILTANDTURNLEFTHAND", JoineryIfc.Of(JoinerySpecs.BalconyDoorWindow, mirrored: true).Properties!.Single(p => p.Name == "PanelOperations").Value);
        // A door's transom lights are panels now (they were left out).
        Assert.Equal(4, JoineryIfc.Of(JoinerySpecs.DoorWithSidelights).Panels.Count);
        Assert.Equal(["LEFT", "MIDDLE", "RIGHT", "MIDDLE"], JoineryIfc.Of(JoinerySpecs.DoorWithSidelights).Panels.Select(p => p.Position));

        // Read back from the terms (a file's panels with Lapin's PanelOperations put in): the leaf and the sash beside it.
        var basis = JoinerySpec.DefaultDoor() with { Width = 1.6, Height = 2.2 };
        var back = JoineryIfc.FromIfc(basis, "SWING_FIXED_LEFT", null, [new IfcJoineryPanel("SWINGING", "LEFT", 0.5), new IfcJoineryPanel("SIDEHUNGRIGHTHAND", "RIGHT", 0.5)])!;
        Assert.Equal(["swing-left", "side-right"], back.Panels.Select(p => p.Operation));
        Assert.Null(Joinery.Problem(back));
        // Lapin's user-defined text with a sash in it.
        Assert.Equal(["swing-left", "tilt-turn-right"], JoineryIfc.FromIfc(basis, "USERDEFINED", "swing-left + tilt-turn-right", null)!.Panels.Select(p => p.Operation));
        // A lift-and-slide portal: SLIDING_TO_LEFT, its leaf's panel SLIDING, LIFTANDSLIDE in Lapin_Joinery, and back.
        var hs = JoineryIfc.Of(JoinerySpecs.LiftSlidePortal);
        Assert.Equal("SLIDING_TO_LEFT", hs.Operation);
        Assert.Equal(["FIXEDPANEL", "SLIDING"], hs.Panels.Select(p => p.Operation));
        Assert.Equal("FIXEDPANEL; LIFTANDSLIDE", hs.Properties!.Single(p => p.Name == "PanelOperations").Value);
        Assert.Equal("lift-and-slide", hs.Properties!.Single(p => p.Name == "Opening").Value);
        var hsBack = JoineryIfc.FromIfc(basis with { Width = 3 }, "SLIDING_TO_LEFT", null, [new IfcJoineryPanel("FIXEDPANEL", "LEFT", 0.5), new IfcJoineryPanel("LIFTANDSLIDE", "RIGHT", 0.5)])!;
        Assert.Equal(["fixed", "lift-slide-left"], hsBack.Panels.Select(p => p.Operation));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC4X3")]
    public void An_ifc_file_says_it_and_lapin_reads_its_own_back(string schema)
    {
        var (f, _, _, _) = Placed(JoinerySpecs.BalconyDoorWindow);
        var path = Path.Combine(_dir, $"combo-{schema}.ifc");
        IfcExport.Write(f.Session, path, new IfcExportOptions(schema));
        var text = File.ReadAllText(path);
        Assert.Contains(".SINGLE_SWING_LEFT.", text);
        Assert.Contains("'PanelOperations'", text);
        Assert.Contains("'SWINGING; FIXEDPANEL; TILTANDTURNRIGHTHAND'", text);
        Assert.Contains("IFCDOORPANELPROPERTIES", text);
        Assert.Contains(".USERDEFINED.", text);
        Assert.Contains("'Pset_DoorWindowGlazingType'", text);
        Assert.Contains("'GlassLayers'", text);
        Assert.Single(text.Split('\n'), l => l.Contains("IFCDOOR(") || l.Contains("IFCDOOR ("));

        var g = new Fixture();
        IfcImport.Run(g.Session, path, null, out _);
        var type = Assert.Single(g.Session.Model.OfClass(IfcClass.DoorType));
        var spec = JoinerySpec.From(type.Attrs["spec"])!;
        Assert.Equal(["swing-left", "fixed", "tilt-turn-right"], Joinery.Normalised(spec).Select(p => p.Operation));
        Assert.Equal("4-16-4", spec.Sash.Glazing);
        Assert.Single(g.Session.Model.OfClass(IfcClass.Door));
        Assert.Empty(g.Session.Model.OfClass(IfcClass.Window));
    }

    [Fact]
    public void Plans_draw_window_swings_as_the_project_says()
    {
        static int Swings(IEnumerable<JStroke> s, bool dashed) => s.Count(x => x.Weight == "thin" && x.Dashed == dashed && x.Pts.Count > 8);
        var french = JoinerySpecs.FrenchWindow;
        // Auto: the sashes reaching the floor dashed; a window at its sill none.
        Assert.Equal(2, Swings(JoinerySymbols.Plan(french, windowSwing: "auto"), true));
        Assert.Equal(0, Swings(JoinerySymbols.Plan(french, windowSwing: "auto", sill: 0.9), true));
        Assert.Equal(2, Swings(JoinerySymbols.Plan(french, windowSwing: "solid", sill: 0.9), false));
        Assert.Equal(0, Swings(JoinerySymbols.Plan(french, windowSwing: "none"), true));
        // The balcony door: its leaf's arc solid as always, its sash over the parapet not reaching the floor; dashed when asked.
        var balcony = JoinerySpecs.BalconyDoorWindow;
        Assert.Equal(0, Swings(JoinerySymbols.Plan(balcony, cutZ: 1.5, windowSwing: "auto"), true));
        Assert.Equal(1, Swings(JoinerySymbols.Plan(balcony, cutZ: 1.5, windowSwing: "dashed"), true));
        Assert.Equal(1, Swings(JoinerySymbols.Plan(balcony, cutZ: 1.5, windowSwing: "dashed"), false));

        // In the project: auto by default, the setting changes every plan.
        var (f, _, fill, _) = Placed(french);
        Assert.Equal(2, f.Session.Geometry(fill)!.Plan!.Count(p => p.Kind == "dashed" && p.Points.Count > 8 && p.MinDetail == PlanDetail.Fine));
        f.Run("architecture.joinerySymbols", new JsonObject { ["windowSwing"] = "none" });
        Assert.Equal("none", Joinery.WindowSwing(f.Session.Model));
        Assert.DoesNotContain(f.Session.Geometry(fill)!.Plan!, p => p.Kind == "dashed" && p.Points.Count > 8);
        Assert.ThrowsAny<Exception>(() => f.Run("architecture.joinerySymbols", new JsonObject { ["windowSwing"] = "sideways" }));
    }

    [Fact]
    public void A_lift_and_slide_leaf_runs_on_the_inner_track_over_the_fixed_pane()
    {
        var s = JoinerySpecs.LiftSlidePortal;
        var boxes = Joinery.Boxes(s);
        var y0 = s.Frame.Offset - s.Frame.Depth / 2;
        var y1 = s.Frame.Offset + s.Frame.Depth / 2;
        var glass = boxes.Where(b => b.Role == "glass").OrderBy(b => b.Min.X).ToList();
        Assert.Equal(2, glass.Count);
        // The fixed pane on the outer track, the leaf's on the inner one: they do not meet across the wall.
        Assert.True(glass[0].Min.Y > glass[1].Max.Y);
        Assert.True(glass[0].Max.Y <= y1 + 1e-9 && glass[1].Min.Y >= y0 - 1e-9);
        // A flush threshold: the leaf on the floor; its handle inside.
        Assert.Equal(0, boxes.Where(b => b.Role == "leaf").Min(b => b.Min.Z), 9);
        Assert.Contains(boxes, b => b.Role == "handle" && b.Max.Y <= y0 + 1e-9);
        // Plan: the leaf cut on its track and the arrow the way it runs; elevation: the lift and the run.
        var plan = JoinerySymbols.Plan(s);
        Assert.Contains(plan, x => x.Weight == "cut" && x.Closed && Math.Abs(x.Pts.Min(p => p.Y) - y0) < 1e-9);
        Assert.True(JoinerySymbols.OpeningMarks("lift-slide-left", new CellRect(0, 1, 0, 2), false).Count >= 4);
        Assert.Contains(JoinerySymbols.OpeningMarks("tilt-slide-right", new CellRect(0, 1, 0, 2), false), x => x.Pts.Count == 3);
        // Tilt-and-slide too; a leaf up in a transom row is refused.
        Assert.Null(Joinery.Problem(s with { Panels = [new(0, 0, "fixed"), new(1, 0, "tilt-slide-left")] }));
        Assert.NotNull(Joinery.Problem(s with { Rows = [2, 1], Panels = [new(0, 0, "fixed"), new(1, 1, "lift-slide-left")] }));
        Assert.Equal("lift & slide left", ScheduleLayout.OperationText(s).Split(" + ").First(x => x.Contains("slide")));
    }

    [Fact]
    public void A_glazing_build_up_is_the_glass_thickness()
    {
        Assert.Equal(0.024, GlazingBuildUp.Parse("4-16-4")!.Thickness, 9);
        Assert.Equal(0.036, GlazingBuildUp.Parse("4-12-4-12-4")!.Thickness, 9);
        var lam = GlazingBuildUp.Parse("33.1-16-4")!;
        Assert.True(lam.Laminated);
        Assert.Equal(0.00638 + 0.016 + 0.004, lam.Thickness, 9);
        Assert.Equal(0.00876, GlazingBuildUp.Parse("44.2")!.Thickness, 9);
        Assert.Equal(3, GlazingBuildUp.Parse("4T-14Ar-4-14Ar-4")!.Panes.Count);
        Assert.Null(GlazingBuildUp.Parse("4-16"));
        Assert.Null(GlazingBuildUp.Parse("four"));

        var spec = JoinerySpec.DefaultWindow() with { Sash = JoinerySpec.DefaultWindow().Sash with { Glazing = "4-12-4-12-4" } };
        var read = JoinerySpec.From(spec.ToJson())!;
        Assert.Equal(0.036, read.Sash.GlassThickness, 9);
        var glass = Joinery.Boxes(read).First(b => b.Role == "glass");
        Assert.Equal(0.036, glass.Max.Y - glass.Min.Y, 9);
        Assert.NotNull(Joinery.Problem(spec with { Sash = spec.Sash with { Glazing = "4-16" } }));
        Assert.NotNull(Joinery.Problem(spec with { Sash = spec.Sash with { Glazing = "8-27-8-27-8" } }));
    }

    [Fact]
    public void The_schedule_lists_it_once_as_a_door_with_all_its_operations()
    {
        var (f, _, _, _) = Placed(JoinerySpecs.BalconyDoorWindow);
        var data = Schedules.Joinery(f.Model, "door");
        var entry = Assert.Single(data.Entries);
        var rows = ScheduleLayout.Rows(data, new ScheduleTable(null, [], new Dictionary<string, Dictionary<string, string>>(), null));
        var op = rows.Single(x => x.Id == "operation").Value(entry);
        Assert.Contains("swing right", op);
        Assert.Contains("tilt & turn left", op);
        Assert.Empty(Schedules.Joinery(f.Model, "window").Entries);
    }
}
