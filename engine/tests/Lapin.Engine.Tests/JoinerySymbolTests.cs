using Lapin.Geometry;
using Lapin.Architecture;

namespace Lapin.Engine.Tests;

/// <summary>Joinery drawings from the spec: ported from the old Lapin's symbols.test.ts (same cases and numbers).</summary>
public class JoinerySymbolTests
{
    static readonly CellRect R = new(0, 1, 0, 2);
    static Vec2 Apex(JStroke s) => s.Pts[1];
    static double ApexX(List<JStroke> strokes) => strokes.First(s => s.Pts.Count == 3 && !s.Closed).Pts[1].X;

    [Fact]
    public void Points_a_side_hung_sash_at_its_handle_the_base_on_its_hinges()
    {
        var left = JoinerySymbols.OpeningMarks("side-left", R, false)[0];
        Assert.Equal(new Vec2(1, 1), Apex(left));
        Assert.Equal((0.0, 0.0), (left.Pts[0].X, left.Pts[2].X));
        Assert.Equal(new Vec2(0, 1), Apex(JoinerySymbols.OpeningMarks("side-right", R, false)[0]));
    }

    [Fact]
    public void Tilt_points_up_tilt_and_turn_has_both_top_hung_points_down()
    {
        Assert.Equal(new Vec2(0.5, 2), Apex(JoinerySymbols.OpeningMarks("bottom-hung", R, false)[0]));
        Assert.Equal([new Vec2(0, 1), new Vec2(0.5, 2)], JoinerySymbols.OpeningMarks("tilt-turn-right", R, false).Select(Apex));
        Assert.Equal(new Vec2(0.5, 0), Apex(JoinerySymbols.OpeningMarks("top-hung", R, false)[0]));
    }

    [Fact]
    public void Crosses_a_fixed_pane_and_the_elevation_follows_the_convention()
    {
        Assert.Equal(2, JoinerySymbols.OpeningMarks("fixed", R, false).Count);
        var w = JoinerySpec.DefaultWindow();
        Assert.NotEqual(ApexX(JoinerySymbols.Elevation(w)), ApexX(JoinerySymbols.Elevation(w, convention: "hinge")));
    }

    [Fact]
    public void DIN_1356_the_lines_meet_at_the_hinges()
    {
        Assert.Equal(new Vec2(0, 1), Apex(JoinerySymbols.OpeningMarks("side-left", R, false, "hinge")[0]));
        Assert.Equal(new Vec2(1, 1), Apex(JoinerySymbols.OpeningMarks("side-right", R, false, "hinge")[0]));
        var marks = JoinerySymbols.OpeningMarks("tilt-turn-left", R, false, "hinge");
        Assert.Equal(2, marks.Count);
        Assert.Equal(new Vec2(0.5, 0), Apex(marks[1]));
        Assert.Equal(new Vec2(0.5, 2), Apex(JoinerySymbols.OpeningMarks("top-hung", R, false, "hinge")[0]));
        Assert.Empty(JoinerySymbols.OpeningMarks("fixed", R, false, "hinge"));
        var shaft = JoinerySymbols.OpeningMarks("sliding-left", R, false)[0];
        Assert.True(shaft.Pts[1].X < shaft.Pts[0].X);
    }

    [Fact]
    public void The_exterior_is_dashed_and_mirrored()
    {
        var w = JoinerySpec.DefaultWindow();
        var outside = JoinerySymbols.Elevation(w, exterior: true);
        Assert.Contains(outside, s => s.Dashed);
        Assert.DoesNotContain(JoinerySymbols.Elevation(w), s => s.Dashed);
        Assert.True(Math.Abs(ApexX(JoinerySymbols.Elevation(w)) + ApexX(outside)) < 1e-9);
    }

    [Fact]
    public void A_door_leaf_opens_90_to_the_interior_with_its_swing_arc_the_size_of_the_leaf()
    {
        var spec = JoinerySpec.DefaultDoor();
        var strokes = JoinerySymbols.Plan(spec);
        var b = JoinerySymbols.Bounds(strokes);
        var leafWidth = spec.Width - 2 * spec.Frame.Width;
        Assert.True(Math.Abs(b.Min.Y - (spec.Frame.Offset - spec.Frame.Depth / 2 - leafWidth)) < 1e-6, $"{b.Min.Y}");
        Assert.True(strokes.Count(s => s.Weight == "cut") >= 2);
    }

    [Fact]
    public void A_window_is_cut_through_frame_mullion_and_sashes_and_shows_its_boards()
    {
        var strokes = JoinerySymbols.Plan(JoinerySpec.DefaultWindow());
        Assert.True(strokes.Count(s => s.Weight == "cut") >= 7);
        Assert.Equal(2, strokes.Count(s => s.Weight == "thin"));
        Assert.Equal(2, strokes.Count(s => s.Weight == "outline"));
    }

    [Fact]
    public void A_plan_cuts_only_the_row_it_passes_through()
    {
        var spec = JoinerySpec.DefaultWindow() with { Rows = [3, 1], Panels = [new JoineryPanel(0, 1, "fixed", ColSpan: 2)] };
        Assert.NotEqual(JoinerySymbols.Plan(spec, cutZ: 0.3).Count, JoinerySymbols.Plan(spec, cutZ: 1.4).Count);
    }

    [Fact]
    public void A_section_cuts_head_sill_sash_rails_and_glass()
    {
        var spec = JoinerySpec.DefaultWindow();
        var strokes = JoinerySymbols.Section(spec);
        var b = JoinerySymbols.Bounds(strokes);
        Assert.True(Math.Abs(b.Max.Y - spec.Height) < 1e-9);
        Assert.True(b.Min.X < spec.Frame.Offset - spec.Frame.Depth / 2 - 0.1);
        Assert.Equal(1, strokes.Count(s => s.Weight == "thin"));
    }
}
