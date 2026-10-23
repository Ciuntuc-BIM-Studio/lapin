using Lapin.Architecture;

namespace Lapin.Engine.Tests;

/// <summary>
/// The door and window configurator's spec. Ported from the old Lapin's
/// joinery.test.ts (spec, IFC terms, geometry), same cases and numbers.
/// </summary>
public class JoineryTests
{
    static JoinerySpec Window2x2() => JoinerySpec.DefaultWindow("W-2") with
    {
        Width = 1.6, Height = 2,
        Columns = [1, 1], Rows = [3, 1],
        Panels =
        [
            new(0, 0, "tilt-turn-left"),
            new(1, 0, "side-right"),
            new(0, 1, "bottom-hung", ColSpan: 2),
        ],
    };

    // ------------------------------------------------------------ spec

    [Fact]
    public void Splits_the_inside_of_the_frame_between_columns_and_rows_bars_excluded()
    {
        var spec = Window2x2();
        var (cols, rows) = Joinery.Grid(spec);
        var f = spec.Frame;
        Assert.Equal(-0.8 + f.Width, cols[0].A, 6);
        Assert.Equal(0.8 - f.Width, cols[1].B, 6);
        Assert.Equal(f.Mullion, cols[1].A - cols[0].B, 6);
        var inside = 2 - 2 * f.Width - f.Transom;
        Assert.Equal(inside * 0.75, rows[0].B - rows[0].A, 6);
        // The top panel spans both columns and the mullion between them.
        var top = Joinery.PanelRect(spec, spec.Panels[2]);
        Assert.Equal(cols[0].A, top.X0, 6);
        Assert.Equal(cols[1].B, top.X1, 6);
    }

    [Fact]
    public void Fills_cells_no_panel_covers_with_fixed_glazing_and_drops_overlapping_panels()
    {
        var spec = JoinerySpec.DefaultWindow() with { Columns = [1, 1, 1], Panels = [new(0, 0, "side-left", ColSpan: 2), new(1, 0, "side-right")] };
        Assert.Equal([(0, "side-left"), (2, "fixed")], Joinery.Normalised(spec).Select(p => (p.Col, p.Operation)));
    }

    [Fact]
    public void Refuses_specs_that_cannot_be_built()
    {
        Assert.Null(Joinery.Problem(JoinerySpec.DefaultWindow()));
        Assert.Matches("positive", Joinery.Problem(JoinerySpec.DefaultWindow() with { Width = 0 }));
        Assert.NotNull(Joinery.Problem(JoinerySpec.DefaultWindow() with { Width = 0.3 }));
    }

    // ------------------------------------------------------------ IFC terms

    [Fact]
    public void Names_the_window_partitioning_from_the_grid()
    {
        Assert.Equal("SINGLE_PANEL", Joinery.WindowPartitioning(JoinerySpec.DefaultWindow() with { Columns = [1], Panels = [] }).Type);
        Assert.Equal("DOUBLE_PANEL_VERTICAL", Joinery.WindowPartitioning(JoinerySpec.DefaultWindow()).Type);
        Assert.Equal("TRIPLE_PANEL_TOP", Joinery.WindowPartitioning(Window2x2()).Type);
        Assert.Equal(("USERDEFINED", "4x1 grid, 4 panels"), Joinery.WindowPartitioning(JoinerySpec.DefaultWindow() with { Columns = [1, 1, 1, 1] }));
    }

    [Fact]
    public void Names_the_door_operation_from_its_leaves()
    {
        Assert.Equal("SINGLE_SWING_LEFT", Joinery.DoorOperation(JoinerySpec.DefaultDoor()).Type);
        var twin = JoinerySpec.DefaultDoor() with { Width = 1.6, Columns = [1, 1], Panels = [new(0, 0, "swing-left"), new(1, 0, "swing-right")] };
        Assert.Equal("DOUBLE_DOOR_SINGLE_SWING", Joinery.DoorOperation(twin).Type);
        var sidelight = JoinerySpec.DefaultDoor() with { Width = 1.3, Columns = [2, 1], Panels = [new(0, 0, "swing-right")] };
        Assert.Equal("SWING_FIXED_RIGHT", Joinery.DoorOperation(sidelight).Type);
    }

    [Fact]
    public void Positions_panels_and_expresses_mullions_as_ratios()
    {
        var spec = Window2x2();
        Assert.Equal(["LEFT", "RIGHT", "MIDDLE"], Joinery.Normalised(spec).Select(p => Joinery.PanelPosition(spec, p, "window")));
        var (mullions, transoms) = Joinery.LiningOffsets(JoinerySpec.DefaultWindow());
        Assert.Single(mullions);
        Assert.Equal(0.5, mullions[0], 6);
        Assert.Empty(transoms);
    }

    // ------------------------------------------------------------ geometry

    [Fact]
    public void Stays_inside_the_overall_size_and_puts_a_handle_opposite_the_hinges()
    {
        var spec = JoinerySpec.DefaultWindow();
        var boxes = Joinery.Boxes(spec);
        foreach (var b in boxes.Where(b => b.Role is not ("board" or "handle")))
        {
            Assert.True(b.Min.X >= -spec.Width / 2 - 1e-9);
            Assert.True(b.Max.X <= spec.Width / 2 + 1e-9);
            Assert.True(b.Min.Z >= -1e-9);
            Assert.True(b.Max.Z <= spec.Height + 1e-9);
        }
        var handles = boxes.Where(b => b.Role == "handle").ToList();
        Assert.Equal(2, handles.Count);
        // Left-hinged left sash: handle on its right; right-hinged right sash: on its left — both near the mullion.
        foreach (var h in handles) Assert.True(Math.Abs((h.Min.X + h.Max.X) / 2) < 0.2);
        // Handles on the interior (−Y) side of the sash.
        foreach (var h in handles) Assert.True(h.Max.Y <= spec.Frame.Offset - spec.Sash.Depth / 2 + 1e-9);
        Assert.Equal(2, boxes.Count(b => b.Role == "glass"));
    }

    [Fact]
    public void Does_not_run_a_mullion_through_a_panel_that_spans_across_it()
    {
        var spec = Window2x2();
        var (cols, rows) = Joinery.Grid(spec);
        var mullionX = (cols[0].B + cols[1].A) / 2;
        var frames = Joinery.Boxes(spec).Where(b => b.Role == "frame" && b.Min.X < mullionX && b.Max.X > mullionX && b.Max.X - b.Min.X < 0.2).ToList();
        // One mullion in the bottom row and the crossing — none in the top row.
        Assert.All(frames, b => Assert.True(b.Max.Z <= rows[0].B + spec.Frame.Transom + 1e-9));
    }

    [Fact]
    public void Gives_a_door_a_threshold_and_a_solid_leaf()
    {
        var spec = JoinerySpec.DefaultDoor();
        var boxes = Joinery.Boxes(spec);
        Assert.Single(boxes, b => b.Role == "leaf");
        var bottom = boxes.First(b => b.Role == "frame" && b.Min.Z == 0 && b.Max.Z < 0.05 && b.Max.X - b.Min.X > 0.5);
        Assert.Equal(spec.Threshold, bottom.Max.Z, 6);
    }
}
