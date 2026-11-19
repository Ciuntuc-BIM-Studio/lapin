using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// A lift-and-slide portal (forum topic 180, decision 725, tester LVK): the leaf on the inner track runs past the fixed pane on
/// the outer one, so no mullion stands in its way. The mullion between them stands on the outer track only (the fixed pane's
/// frame), the leaf reaches over it, so their meeting stiles overlap; in 3D, plan, section and the configurator's preview. A
/// sliding window's sashes on their two tracks the same.
/// </summary>
public class LiftSlideTrackTests
{
    static readonly JoinerySpec Hs = JoinerySpecs.LiftSlidePortal;
    static double Y0(JoinerySpec s) => s.Frame.Offset - s.Frame.Depth / 2;
    static double Y1(JoinerySpec s) => s.Frame.Offset + s.Frame.Depth / 2;
    static double Track(JoinerySpec s) => Math.Min(s.Sash.Depth, s.Frame.Depth / 2);

    /// <summary>The frame's boxes inside the opening (mullions; not the jambs, head or sill) crossing a y band at a height.</summary>
    static List<JoineryBox> BarsIn(List<JoineryBox> boxes, JoinerySpec s, double ya, double yb, double z)
    {
        var inner = Joinery.InnerRect(s);
        return boxes.Where(b => b.Role == "frame" && b.Min.X > inner.X0 + 1e-6 && b.Max.X < inner.X1 - 1e-6
            && b.Min.Z <= z && b.Max.Z >= z && b.Min.Y < yb - 1e-6 && b.Max.Y > ya + 1e-6).ToList();
    }

    [Fact]
    public void Nothing_of_the_frame_stands_in_the_leafs_way()
    {
        var boxes = Joinery.Boxes(Hs);
        var (y0, y1, track) = (Y0(Hs), Y1(Hs), Track(Hs));
        // Along the whole width, on the inner track, at any height of the leaf: no mullion.
        foreach (var z in new[] { 0.3, 1.1, 2.0 }) Assert.Empty(BarsIn(boxes, Hs, y0, y0 + track, z));
        // The mullion is still there, on the outer track: the fixed pane's frame.
        var mullion = Assert.Single(BarsIn(boxes, Hs, y0 + track, y1, 1.1));
        Assert.Equal(y0 + track, mullion.Min.Y, 9);
        Assert.Equal(y1, mullion.Max.Y, 9);
        Assert.Equal(Hs.Frame.Mullion, mullion.Max.X - mullion.Min.X, 9);
    }

    [Fact]
    public void The_leaf_reaches_over_the_mullion_so_the_meeting_stiles_overlap()
    {
        var panels = Joinery.Normalised(Hs);
        var leaf = panels.Single(p => Joinery.LiftSlides(p.Operation));
        var fixedPane = panels.Single(p => p.Operation == "fixed");
        Assert.Equal(0, Joinery.SlidesPast(Hs, panels, leaf));
        Assert.Null(Joinery.SlidesPast(Hs, panels, fixedPane));
        Assert.Equal(-1, Joinery.TrackOf(Hs, panels, leaf));
        Assert.Equal(1, Joinery.TrackOf(Hs, panels, fixedPane));
        var (cols, _) = Joinery.Grid(Hs);
        // The leaf (sliding left, over the fixed pane at the left) from the mullion's far side; the fixed pane as it was.
        var r = Joinery.PanelRect(Hs, leaf);
        Assert.Equal(cols[0].B, r.X0, 9);
        Assert.Equal(cols[0].A, Joinery.PanelRect(Hs, fixedPane).X0, 9);
        Assert.Equal(cols[0].B, Joinery.PanelRect(Hs, fixedPane).X1, 9);
        // In 3D: the leaf's stile on the inner track over the same x as the mullion on the outer track.
        var boxes = Joinery.Boxes(Hs);
        var stile = boxes.Where(b => b.Role == "leaf").MinBy(b => b.Min.X)!;
        var mullion = Assert.Single(BarsIn(boxes, Hs, Y0(Hs), Y1(Hs), 1.1));
        Assert.True(stile.Min.X <= mullion.Min.X + 1e-9 && stile.Max.X > mullion.Min.X);
        Assert.True(stile.Max.Y <= mullion.Min.Y + 1e-9);
        // The leaf and the fixed pane on separate tracks, overlapping along the wall.
        var glass = boxes.Where(b => b.Role == "glass").OrderBy(b => b.Min.X).ToList();
        Assert.True(glass[1].Max.Y < glass[0].Min.Y);
        Assert.True(stile.Min.X < glass[0].Max.X + Hs.Frame.Mullion);
        // Still buildable; the other hand mirrors it.
        Assert.Null(Joinery.Problem(Hs));
        var right = Hs with { Panels = [new(0, 0, "lift-slide-right"), new(1, 0, "fixed")] };
        var rp = Joinery.Normalised(right);
        Assert.Equal(cols[1].A, Joinery.PanelRect(right, rp[0]).X1, 9);
        Assert.Empty(BarsIn(Joinery.Boxes(right), right, Y0(right), Y0(right) + Track(right), 1.1));
    }

    [Fact]
    public void The_plan_and_the_section_show_the_mullion_outside_and_the_leaf_across_it()
    {
        var (y0, y1, track) = (Y0(Hs), Y1(Hs), Track(Hs));
        var (cols, _) = Joinery.Grid(Hs);
        var (mx0, mx1) = (cols[0].B, cols[1].A);
        // Plan: the leaf cut on the inner track from over the mullion; the mullion cut on the outer track only.
        var plan = JoinerySymbols.Plan(Hs);
        Assert.Contains(plan, x => x.Weight == "cut" && x.Closed && Math.Abs(x.Pts.Min(p => p.Y) - y0) < 1e-9 && Math.Abs(x.Pts.Min(p => p.X) - mx0) < 1e-9);
        var atMullion = plan.Where(x => x.Weight == "cut" && x.Closed && Math.Abs(x.Pts.Min(p => p.X) - mx0) < 1e-9 && Math.Abs(x.Pts.Max(p => p.X) - mx1) < 1e-9).ToList();
        Assert.Contains(atMullion, x => Math.Abs(x.Pts.Min(p => p.Y) - (y0 + track)) < 1e-9 && Math.Abs(x.Pts.Max(p => p.Y) - y1) < 1e-9);
        Assert.DoesNotContain(atMullion, x => x.Pts.Min(p => p.Y) < y0 + track - 1e-9 && x.Pts.Max(p => p.Y) > y0 + track + 1e-9);
        // A section through the mullion: it is cut on the outer track, the leaf's stile on the inner one.
        var section = JoinerySymbols.Section(Hs, (mx0 + mx1) / 2);
        var cut = section.Where(x => x.Weight == "cut" && x.Closed).ToList();
        Assert.Contains(cut, x => Math.Abs(x.Pts.Min(p => p.X) - (y0 + track)) < 1e-9 && Math.Abs(x.Pts.Max(p => p.X) - y1) < 1e-9 && x.Pts.Max(p => p.Y) - x.Pts.Min(p => p.Y) > 1.5);
        Assert.Contains(cut, x => Math.Abs(x.Pts.Min(p => p.X) - y0) < 1e-9 && x.Pts.Max(p => p.X) <= y0 + track + 1e-9 && x.Pts.Max(p => p.Y) - x.Pts.Min(p => p.Y) > 1.5);
        Assert.DoesNotContain(cut, x => x.Pts.Min(p => p.X) < y0 + 1e-9 && x.Pts.Max(p => p.X) > y1 - 1e-9 && x.Pts.Min(p => p.Y) > 0.2 && x.Pts.Max(p => p.Y) < 2.0);
    }

    [Fact]
    public void The_configurators_preview_is_the_same_body()
    {
        var f = new Fixture();
        var r = f.Session.Query("architecture.joineryPreview", new JsonObject { ["spec"] = Hs.ToJson() })!;
        var (y0, track) = (Y0(Hs), Track(Hs));
        var inner = Joinery.InnerRect(Hs);
        var bars = r["boxes"]!.AsArray().Select(b => b!.AsObject())
            .Where(b => (string?)b["role"] == "frame" && (double)b["x0"]! > inner.X0 + 1e-6 && (double)b["x1"]! < inner.X1 - 1e-6 && (double)b["z0"]! < 1 && (double)b["z1"]! > 1).ToList();
        var bar = Assert.Single(bars);
        Assert.Equal(y0 + track, (double)bar["y0"]!, 9);
        var leaf = r["panels"]!.AsArray().Select(p => p!.AsObject()).Single(p => ((string)p["operation"]!).StartsWith("lift-slide"));
        Assert.Equal((double)bar["x0"]!, (double)leaf["x0"]!, 9);
    }

    [Fact]
    public void A_sliding_windows_sashes_pass_each_other_with_no_mullion_between()
    {
        var w = JoinerySpec.DefaultWindow("Slider") with
        {
            Width = 2.0, Height = 1.4, Columns = [1, 1], Rows = [1],
            Panels = [new(0, 0, "sliding-right"), new(1, 0, "sliding-left")],
        };
        var panels = Joinery.Normalised(w);
        Assert.Equal([-1, 1], panels.Select(p => Joinery.TrackOf(w, panels, p)));
        Assert.Equal(0, Joinery.SlidesPast(w, panels, panels[0]));
        Assert.Equal(0, Joinery.SlidesPast(w, panels, panels[1]));
        var (y0, y1, track) = (Y0(w), Y1(w), Track(w));
        var boxes = Joinery.Boxes(w);
        // Neither track is crossed by a mullion: what is left of it stands between them, or nothing when they fill the frame.
        Assert.Empty(BarsIn(boxes, w, y0, y0 + track, 0.7));
        Assert.Empty(BarsIn(boxes, w, y1 - track, y1, 0.7));
        // Both sashes reach over where the mullion was: they overlap at the meeting stiles.
        var (a, b) = (Joinery.PanelRect(w, panels[0]), Joinery.PanelRect(w, panels[1]));
        Assert.True(a.X1 > b.X0 + 1e-6);
        // A sash beside a fixed pane on the same track runs nowhere past it: the mullion stays whole.
        var one = w with { Panels = [new(0, 0, "fixed"), new(1, 0, "sliding-left")] };
        var op = Joinery.Normalised(one);
        Assert.Equal(-1, Joinery.TrackOf(one, op, op[1]));
        Assert.Equal(0, Joinery.SlidesPast(one, op, op[1]));
        Assert.Empty(BarsIn(Joinery.Boxes(one), one, y0, y0 + track, 0.7));
    }

    [Fact]
    public void A_swing_door_beside_a_fixed_pane_keeps_its_whole_mullion()
    {
        var d = JoinerySpecs.DoorWithSidelights;
        var boxes = Joinery.Boxes(d);
        Assert.All(Joinery.Normalised(d), p => Assert.Null(Joinery.SlidesPast(d, Joinery.Normalised(d), p)));
        Assert.Contains(boxes, b => b.Role == "frame" && Math.Abs(b.Min.Y - Y0(d)) < 1e-9 && Math.Abs(b.Max.Y - Y1(d)) < 1e-9 && b.Max.X - b.Min.X < 0.2 && b.Min.Z < 1 && b.Max.Z > 1);
    }
}
