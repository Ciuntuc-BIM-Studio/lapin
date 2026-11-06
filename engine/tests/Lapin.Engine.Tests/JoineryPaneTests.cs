using Lapin.Architecture;

namespace Lapin.Engine.Tests;

/// <summary>
/// The configurator's panes and profiles (decision 370, a tester's questions): any pane glass or a solid panel (a door's lower
/// panel, a window's opaque infill, a solid or glazed leaf), mullions and transoms of their own width, and a profile system's
/// finer shapes (a rebate, the sash lapping over the frame, glazing beads, a drip), none of it on a type without one.
/// </summary>
public class JoineryPaneTests
{
    static int Count(List<JoineryBox> b, string role) => b.Count(x => x.Role == role);

    [Fact]
    public void A_pane_is_glass_or_a_solid_panel_whatever_opens_it()
    {
        var w = JoinerySpec.DefaultWindow() with { Columns = [1], Rows = [1, 2], Panels = [new(0, 0, "fixed", Infill: "solid"), new(0, 1, "side-left", Infill: "solid")] };
        Assert.Null(Joinery.Problem(w));
        var boxes = Joinery.Boxes(w);
        Assert.Equal(0, Count(boxes, "glass"));
        Assert.Equal(2, Count(boxes, "leaf"));
        Assert.True(Count(boxes, "sash") >= 4);
        // A door leaf glazed by its infill, as by the old flag; a solid one is one slab.
        var door = JoinerySpec.DefaultDoor() with { Panels = [new(0, 0, "swing-left", Infill: "glass")] };
        Assert.Equal(1, Count(Joinery.Boxes(door), "glass"));
        Assert.Equal("glass", Joinery.InfillOf(new JoineryPanel(0, 0, "swing-left", Glazed: true)));
        Assert.Equal("solid", Joinery.InfillOf(new JoineryPanel(0, 0, "swing-left")));
        Assert.Equal("glass", Joinery.InfillOf(new JoineryPanel(0, 0, "fixed")));
    }

    [Fact]
    public void A_shop_door_is_two_leaves_with_side_lights_and_a_transom_over_them_in_one_type()
    {
        // Four columns, a transom row spanning all: leaves in the middle (one solid, one glazed), a solid side panel, a glazed one.
        var s = JoinerySpec.DefaultDoor() with
        {
            Width = 2.4, Height = 2.7, Columns = [424, 636, 636, 424], Rows = [2120, 450],
            Panels = [new(0, 0, "fixed", Infill: "solid"), new(1, 0, "swing-left"), new(2, 0, "swing-right", Infill: "glass"), new(3, 0, "fixed"), new(0, 1, "fixed", ColSpan: 4)],
        };
        Assert.Null(Joinery.Problem(s));
        var panels = Joinery.Normalised(s);
        Assert.Equal(5, panels.Count);
        Assert.Equal(4, panels.Single(p => p.Row == 1).ColSpan);
        var boxes = Joinery.Boxes(s);
        Assert.Equal(3, Count(boxes, "glass"));
        Assert.Equal("DOUBLE_DOOR_SINGLE_SWING", Joinery.DoorOperation(s).Type);
        // No mullion crosses the transom light: the bars above the transom are none.
        var top = Joinery.PanelRect(s, panels.Single(p => p.Row == 1));
        Assert.DoesNotContain(boxes, b => b.Role == "frame" && b.Min.Z >= top.Z0 - 1e-9 && b.Max.Z <= top.Z1 + 1e-9 && b.Max.X - b.Min.X < 0.2);
    }

    [Fact]
    public void A_mullion_takes_its_own_width()
    {
        var s = JoinerySpec.DefaultWindow() with { Frame = JoinerySpec.DefaultWindow().Frame with { Width = 0.05, Mullion = 0.05 } };
        var (cols, _) = Joinery.Grid(s);
        Assert.Equal(0.05, cols[1].A - cols[0].B, 9);
    }

    [Fact]
    public void A_profile_system_adds_the_rebate_the_lapped_sash_the_beads_and_the_drip_and_a_type_without_keeps_its_boxes()
    {
        var plain = JoinerySpec.DefaultWindow();
        var (frame, sash, profile) = Joinery.ProfileSystems["pvc"];
        var pvc = plain with { Frame = frame, Sash = sash, Profile = profile };
        Assert.Null(Joinery.Problem(pvc));
        var a = Joinery.Boxes(plain with { Frame = frame, Sash = sash });
        var b = Joinery.Boxes(pvc);
        Assert.True(b.Count > a.Count + 10, $"{a.Count} boxes plain, {b.Count} with the profile");
        // The sash's lip stands on the interior face, in front of the frame (−Y).
        Assert.Contains(b, x => x.Role == "sash" && x.Max.Y <= pvc.Frame.Offset - pvc.Frame.Depth / 2 + 1e-9);
        // A stored spec reads back with its panes' infills and its profile.
        var back = JoinerySpec.From((pvc with { Panels = [new(0, 0, "fixed", Infill: "solid")] }).ToJson())!;
        Assert.Equal("pvc", back.Profile!.System);
        Assert.Equal("solid", back.Panels[0].Infill);
        Assert.Null(JoinerySpec.From(plain.ToJson())!.Profile);
    }
}
