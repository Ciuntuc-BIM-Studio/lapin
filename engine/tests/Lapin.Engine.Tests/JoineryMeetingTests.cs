using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Per-mullion widths and the meeting of two leaves (decision 450): a double door with no mullion between its leaves keeps
/// the mullions by its side panels; the leaves meet butt, on a rebated meeting stile or under an astragal on the active
/// leaf; the handle only on the active leaf, flush bolts on the passive one.
/// </summary>
public class JoineryMeetingTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-joinery-meeting").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    /// <summary>A pair of leaves 1.6 m wide, no mullion between them.</summary>
    static JoinerySpec Pair(JoineryMeeting? meeting) => JoinerySpec.DefaultDoor("Pair") with
    {
        Width = 1.6, Columns = [1, 1], Panels = [new(0, 0, "swing-left"), new(1, 0, "swing-right")], Mullions = [0], Meeting = meeting,
    };

    /// <summary>A pair with a fixed side panel each side: mullions by the side panels, none between the leaves.</summary>
    static JoinerySpec WithSides() => JoinerySpec.DefaultDoor("Sides") with
    {
        Width = 2.6, Columns = [0.6, 1, 1, 0.6],
        Panels = [new(0, 0, "fixed"), new(1, 0, "swing-left"), new(2, 0, "swing-right"), new(3, 0, "fixed")],
        Mullions = [null, 0, null], Meeting = new JoineryMeeting(),
    };

    static List<JoineryBox> Role(JoinerySpec s, string role) => Joinery.Boxes(s).Where(b => b.Role == role).ToList();

    // ------------------------------------------------------------ per-mullion widths

    [Fact]
    public void Each_boundary_takes_its_own_mullion_width_and_the_cells_share_what_is_left()
    {
        var s = WithSides();
        var (cols, _) = Joinery.Grid(s);
        var m = s.Frame.Mullion;
        Assert.Equal(-1.3 + s.Frame.Width, cols[0].A, 9);
        Assert.Equal(1.3 - s.Frame.Width, cols[3].B, 9);
        Assert.Equal(m, cols[1].A - cols[0].B, 9);
        Assert.Equal(0, cols[2].A - cols[1].B, 9);
        Assert.Equal(m, cols[3].A - cols[2].B, 9);
        var usable = 2.6 - 2 * s.Frame.Width - 2 * m;
        Assert.Equal(usable * 0.6 / 3.2, cols[0].B - cols[0].A, 9);
        Assert.Equal(usable * 1 / 3.2, cols[1].B - cols[1].A, 9);
        Assert.Equal([m, 0, m], Joinery.MullionWidths(s));
        // The frame between the cells: the side panels' mullions, nothing between the leaves.
        var bars = Role(s, "frame").Where(b => b.Min.X > cols[0].A && b.Max.X < cols[3].B && b.Max.Z - b.Min.Z > 1).ToList();
        Assert.Equal(2, bars.Count);
        Assert.Equal(cols[0].B, bars.Min(b => b.Min.X), 9);
        Assert.Equal(cols[3].A, bars.Max(b => b.Max.X), 9);
        Assert.Null(Joinery.Problem(s));
    }

    [Fact]
    public void Transoms_take_their_own_widths_too()
    {
        var s = JoinerySpec.DefaultWindow() with { Rows = [1, 1, 1], Columns = [1], Panels = [new(0, 0, "fixed"), new(0, 1, "fixed"), new(0, 2, "fixed")], Transoms = [0.12, null] };
        var (_, rows) = Joinery.Grid(s);
        Assert.Equal(0.12, rows[1].A - rows[0].B, 9);
        Assert.Equal(s.Frame.Transom, rows[2].A - rows[1].B, 9);
        Assert.Equal([0.12, s.Frame.Transom], Joinery.TransomWidths(s));
    }

    [Fact]
    public void A_spec_without_the_lists_builds_exactly_as_before_and_reads_back_unchanged()
    {
        static string Key(JoineryBox b) => $"{b.Role} {b.Min} {b.Max} {b.Profile?.Count}";
        var window2x2 = JoinerySpec.DefaultWindow("W-2") with { Width = 1.6, Height = 2, Columns = [1, 1], Rows = [3, 1], Panels = [new(0, 0, "tilt-turn-left"), new(1, 0, "side-right"), new(0, 1, "bottom-hung", ColSpan: 2)] };
        foreach (var s in new[] { JoinerySpec.DefaultDoor(), JoinerySpec.DefaultWindow(), window2x2 })
        {
            // The lists given with the frame's own widths build the same body.
            var same = s with { Mullions = [.. Enumerable.Repeat<double?>(s.Frame.Mullion, s.Columns.Length - 1)], Transoms = [.. Enumerable.Repeat<double?>(null, s.Rows.Length - 1)] };
            Assert.Equal(Joinery.Boxes(s).Select(Key), Joinery.Boxes(same).Select(Key));
            // An old type's JSON has none of the new fields, and reads back to the same body.
            var json = s.ToJson();
            Assert.False(json.ContainsKey("mullions") || json.ContainsKey("transoms") || json.ContainsKey("meeting"));
            var back = JoinerySpec.From(JsonNode.Parse(json.ToJsonString()))!;
            Assert.Equal(Joinery.Boxes(s).Select(Key), Joinery.Boxes(back).Select(Key));
        }
        // A pair with a mullion and no meeting: two handles, as before.
        var old = JoinerySpec.DefaultDoor() with { Width = 1.6, Columns = [1, 1], Panels = [new(0, 0, "swing-left"), new(1, 0, "swing-right")] };
        Assert.Contains(Role(old, "handle"), b => b.Max.X < 0);
        Assert.Contains(Role(old, "handle"), b => b.Min.X > 0);
        Assert.Empty(Joinery.Meetings(old));
    }

    [Fact]
    public void The_lists_and_the_meeting_round_trip_through_json()
    {
        var s = WithSides() with { Meeting = new JoineryMeeting("astragal", "left", 0.05, 0.01, false) };
        var back = JoinerySpec.From(JsonNode.Parse(s.ToJson().ToJsonString()))!;
        Assert.Equal(s.Mullions, back.Mullions);
        Assert.Equal(s.Meeting, back.Meeting);
        Assert.Equal("astragal", (string?)s.ToJson()["meeting"]!["kind"]);
    }

    [Fact]
    public void Problem_catches_a_mullion_too_wide_for_its_cells_and_bad_widths()
    {
        var w = JoinerySpec.DefaultWindow();
        Assert.Matches("no room", Joinery.Problem(w with { Mullions = [1.0] }));
        Assert.Matches("zero or positive", Joinery.Problem(w with { Mullions = [-0.01] }));
        Assert.Matches("zero or positive", Joinery.Problem(w with { Transoms = [double.NaN] }));
        Assert.Matches("meeting", Joinery.Problem(Pair(new JoineryMeeting("glued"))));
        Assert.Matches("active", Joinery.Problem(Pair(new JoineryMeeting(Active: "middle"))));
        Assert.Null(Joinery.Problem(Pair(new JoineryMeeting())));
    }

    [Fact]
    public void A_mirror_reverses_the_mullions_and_the_active_leaf()
    {
        var s = WithSides() with { Mullions = [0.04, 0, null], Meeting = new JoineryMeeting(Active: "right") };
        var m = Joinery.Mirrored(s);
        Assert.Equal([null, 0, 0.04], m.Mullions!);
        Assert.Equal("left", m.Meeting!.Active);
    }

    // ------------------------------------------------------------ meetings

    [Fact]
    public void Two_leaves_meet_where_their_free_edges_touch_with_no_mullion()
    {
        var pair = Assert.Single(Joinery.Meetings(WithSides()));
        Assert.Equal((1, 1, 2, 2, 1, "rebated"), (pair.Boundary, pair.Left.Col, pair.Right.Col, pair.Active.Col, pair.Passive.Col, pair.Kind));
        // Hinges at the meeting are no pair; a mullion between them neither.
        Assert.Empty(Joinery.Meetings(Pair(new()) with { Panels = [new(0, 0, "swing-left"), new(1, 0, "swing-left")] }));
        Assert.Empty(Joinery.Meetings(Pair(new()) with { Mullions = null }));
        // Double-acting leaves have no rebate: they meet butt.
        Assert.Equal("butt", Assert.Single(Joinery.Meetings(Pair(new()) with { Panels = [new(0, 0, "double-acting-left"), new(1, 0, "double-acting-right")] })).Kind);
        // Without a meeting setting the pair meets butt (an old type with no mullions).
        Assert.Equal("butt", Assert.Single(Joinery.Meetings(Pair(null))).Kind);
    }

    [Fact]
    public void A_rebated_meeting_steps_both_leaves_half_their_thickness_the_active_lapping_on_the_room_side()
    {
        var s = Pair(new JoineryMeeting("rebated", "right"));
        var t = s.Sash.LeafThickness;
        var o = JoineryMeeting.DefaultRebate;
        var leaves = Role(s, "leaf");
        Assert.Equal(4, leaves.Count);
        // The bodies stop half the overlap short of the meeting line, each lip spans it on half the thickness.
        Assert.Contains(leaves, b => Math.Abs(b.Max.X + o / 2) < 1e-9 && b.Min.X < -0.7);
        Assert.Contains(leaves, b => Math.Abs(b.Min.X - o / 2) < 1e-9 && b.Max.X > 0.7);
        var lips = leaves.Where(b => b.Max.X - b.Min.X < o + 1e-9).ToList();
        Assert.Equal(2, lips.Count);
        Assert.All(lips, b => { Assert.Equal(-o / 2, b.Min.X, 9); Assert.Equal(o / 2, b.Max.X, 9); Assert.Equal(t / 2, b.Max.Y - b.Min.Y, 9); });
        // The active leaf's lip on the room side (−Y, where the leaves open), the passive's outside.
        Assert.Single(lips, b => Math.Abs(b.Min.Y - (s.Frame.Offset - t / 2)) < 1e-9);
        Assert.Single(lips, b => Math.Abs(b.Max.Y - (s.Frame.Offset + t / 2)) < 1e-9);
        // As much leaf as when they meet butt.
        static double Volume(IEnumerable<JoineryBox> bs) => bs.Sum(b => (b.Max.X - b.Min.X) * (b.Max.Y - b.Min.Y) * (b.Max.Z - b.Min.Z));
        Assert.Equal(Volume(Role(Pair(new JoineryMeeting("butt")), "leaf")), Volume(leaves), 9);
    }

    [Fact]
    public void An_astragal_covers_the_meeting_on_the_active_leafs_room_face_and_a_butt_meeting_adds_nothing()
    {
        var s = Pair(new JoineryMeeting("astragal", "right"));
        var t = s.Sash.LeafThickness;
        var leaves = Role(s, "leaf");
        Assert.Equal(3, leaves.Count);
        var strip = Assert.Single(leaves, b => b.Max.X - b.Min.X < 0.1);
        Assert.Equal(-JoineryMeeting.DefaultAstragalWidth / 2, strip.Min.X, 9);
        Assert.Equal(JoineryMeeting.DefaultAstragalWidth / 2, strip.Max.X, 9);
        Assert.Equal(s.Frame.Offset - t / 2, strip.Max.Y, 9);
        Assert.Equal(JoineryMeeting.DefaultAstragalThickness, strip.Max.Y - strip.Min.Y, 9);
        var butt = Role(Pair(new JoineryMeeting("butt")), "leaf");
        Assert.Equal(2, butt.Count);
        Assert.Equal(0, butt.Min(b => Math.Abs(b.Max.X)), 9);
    }

    [Fact]
    public void Only_the_active_leaf_has_a_handle_and_the_passive_one_has_flush_bolts()
    {
        var right = Pair(new JoineryMeeting(Active: "right"));
        Assert.NotEmpty(Role(right, "handle"));
        Assert.All(Role(right, "handle"), b => Assert.True(b.Min.X > 0));
        // The passive leaf's bolts: one at its top, one at its bottom, by its meeting edge on the room face.
        var bolts = Role(right, "hardware").Where(b => b.Max.X < 0).ToList();
        Assert.Equal(2, bolts.Count);
        Assert.All(bolts, b => Assert.True(b.Max.X > -0.1 && b.Max.Y <= right.Frame.Offset - right.Sash.LeafThickness / 2 + 1e-9));
        Assert.True(bolts.Max(b => b.Max.Z) > 1.8 && bolts.Min(b => b.Min.Z) < 0.3);
        var left = Pair(new JoineryMeeting(Active: "left", Bolts: false));
        Assert.All(Role(left, "handle"), b => Assert.True(b.Max.X < 0));
        Assert.DoesNotContain(Role(left, "hardware"), b => b.Max.Y <= left.Frame.Offset - left.Sash.LeafThickness / 2 + 1e-9 && b.Max.Z - b.Min.Z > 0.15);
    }

    [Fact]
    public void The_drawings_show_both_swings_and_the_handle_on_the_active_leaf_only()
    {
        var s = Pair(new JoineryMeeting(Active: "right"));
        // The plan: each leaf open with its arc.
        var plan = JoinerySymbols.Plan(s);
        Assert.Equal(2, plan.Count(st => st.Pts.Count > 8 && !st.Closed));
        // The elevation with hardware: a lever's rose (a circle) on the active leaf only.
        var elevation = JoinerySymbols.Elevation(s, false, "handle", true);
        var roses = elevation.Where(st => st.Pts.Count > 30 && st.Weight == "outline").ToList();
        Assert.NotEmpty(roses);
        Assert.All(roses, st => Assert.True(st.Pts.Min(q => q.X) > 0));
    }

    // ------------------------------------------------------------ IFC

    [Fact]
    public void Ifc_says_a_double_door_with_a_panel_per_leaf_and_no_mullion_in_its_lining()
    {
        var t = JoineryIfc.Of(Pair(new JoineryMeeting()));
        Assert.Equal("DOUBLE_DOOR_SINGLE_SWING", t.Operation);
        Assert.Equal([("SWINGING", "LEFT", 0.5), ("SWINGING", "RIGHT", 0.5)], t.Panels.Select(p => (p.Operation, p.Position, p.Width!.Value)));
        Assert.Empty(t.Lining.MullionOffsets);
        // With side panels: the two mullions there, the widest one's thickness.
        var sides = WithSides() with { Mullions = [0.04, 0, 0.07] };
        var ts = JoineryIfc.Of(sides);
        Assert.Equal(2, ts.Lining.MullionOffsets.Count);
        Assert.Equal(0.07, ts.Lining.Mullion, 9);
        var inner = Joinery.InnerRect(sides);
        var (cols, _) = Joinery.Grid(sides);
        Assert.Equal(Math.Round((cols[1].B - cols[1].A) / (inner.X1 - inner.X0), 4), ts.Panels[1].Width!.Value, 9);
    }

    [Fact]
    public void An_exported_pair_writes_two_door_panel_properties()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new DraftingModule());
        var wall = f.Wall(0, 0, 6, 0);
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = Pair(new JoineryMeeting()).ToJson() })!["id"]!;
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 2, ["typeId"] = type });
        var path = Path.Combine(_dir, "pair.ifc");
        IfcExport.Write(f.Session, path);
        var step = StepFile.Read(path);
        var doorType = Assert.Single(step.OfType("IFCDOORTYPE"), e => (e[10] as StepEnum)?.Value == "DOUBLE_DOOR_SINGLE_SWING");
        var sets = (doorType[5] as List<object?> ?? []).Select(step.Get).OfType<StepEntity>().ToList();
        var panels = sets.Where(x => x.Type == "IFCDOORPANELPROPERTIES").ToList();
        Assert.Equal(2, panels.Count);
        Assert.All(panels, p => Assert.Equal(0.5, (double)p[6]!, 4));
    }

    // ------------------------------------------------------------ the configurator

    [Fact]
    public void The_preview_grid_carries_each_boundarys_width_and_where_leaves_meet()
    {
        var f = new Fixture(new ArchitectureModule());
        var p = (JsonObject)f.Session.Query("architecture.joineryPreview", new JsonObject { ["spec"] = WithSides().ToJson() })!;
        Assert.Null((string?)p["problem"]);
        var grid = (JsonObject)p["grid"]!;
        var m = WithSides().Frame.Mullion;
        Assert.Equal([m, 0, m], grid["mullions"]!.AsArray().Select(x => (double)x!));
        Assert.Equal([false, true, false], grid["meetings"]!.AsArray().Select(x => (bool)x!));
        Assert.Empty(grid["transoms"]!.AsArray());
        var pair = (JsonObject)Assert.Single(grid["pairs"]!.AsArray())!;
        Assert.Equal((1, 2, 1, "rebated"), ((int)pair["boundary"]!, (int)pair["active"]!, (int)pair["passive"]!, (string)pair["kind"]!));
    }

    [Fact]
    public void The_catalog_offers_a_double_door_with_a_rebated_meeting_active_right()
    {
        var f = new Fixture(new ArchitectureModule());
        var catalog = (JsonObject)f.Session.Catalog.Catalogs["architecture.joinery"]!;
        var dd = JoinerySpec.From(catalog["doorPresets"]!["double"])!;
        Assert.Null(Joinery.Problem(dd));
        Assert.Equal(("rebated", "right"), (dd.Meeting!.Kind, dd.Meeting.Active));
        Assert.Equal(0, Joinery.MullionWidths(dd)[0]);
        Assert.Equal(["butt", "rebated", "astragal"], catalog["meetingKinds"]!.AsArray().Select(x => (string)x!));
        Assert.Equal("rebated", (string?)catalog["defaultMeeting"]!["kind"]);
    }
}
