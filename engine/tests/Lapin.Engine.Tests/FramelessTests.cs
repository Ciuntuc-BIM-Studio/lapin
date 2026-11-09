using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Frameless glass doors and windows (tester's question 7, decision 444): toughened glass with no frame or sash, a leaf on
/// pivots with patch fittings or on glass hinges, a sliding leaf hanging from a track in front of the panes, fixed panes in
/// channels; drawn, scheduled and written to IFC like any door.
/// </summary>
public class FramelessTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-frameless").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static JoinerySpec Door(string fixing = "pivot", double height = 2.1, double[]? columns = null, JoineryPanel[]? panels = null) =>
        JoinerySpec.DefaultDoor("Glass door") with
        {
            Height = height,
            Columns = columns ?? [1],
            Panels = panels ?? [new(0, 0, "swing-left")],
            Frameless = new JoineryFrameless(fixing),
        };

    [Fact]
    public void A_frameless_leaf_is_its_glass_alone_with_patch_fittings_and_a_pull_bar()
    {
        var s = Door();
        Assert.Null(Joinery.Problem(s));
        var boxes = Joinery.Boxes(s);
        Assert.DoesNotContain(boxes, b => b.Role is "frame" or "sash" or "leaf");
        var glass = Assert.Single(boxes, b => b.Role == "glass");
        Assert.Equal(0.012, glass.Max.Y - glass.Min.Y, 9);
        // 4 mm clear of the jambs and head, 8 mm over the floor.
        Assert.Equal(-0.45 + 0.004, glass.Min.X, 9);
        Assert.Equal(0.45 - 0.004, glass.Max.X, 9);
        Assert.Equal(0.008, glass.Min.Z, 9);
        Assert.Equal(2.1 - 0.004, glass.Max.Z, 9);
        Assert.True(glass.Leaf);
        // Two patch fittings at the hinge side (the left, x0), a lock patch at the bottom opposite; each clamping both faces.
        var fittings = boxes.Where(b => b.Role == "hardware" && b.Max.X - b.Min.X > 0.15).ToList();
        Assert.Equal(3, fittings.Count);
        Assert.Equal(2, fittings.Count(b => Math.Abs(b.Min.X - glass.Min.X) < 1e-9));
        Assert.All(fittings, b => Assert.True(b.Min.Y < glass.Min.Y && b.Max.Y > glass.Max.Y));
        // A pull bar on each face by default.
        Assert.Equal("pull", Joinery.HandleStyle(s));
        Assert.Equal(2, boxes.Count(b => b.Role == "handle"));
        Assert.Contains(boxes, b => b.Role == "handle" && b.Min.Y > glass.Max.Y);
        Assert.Contains(boxes, b => b.Role == "handle" && b.Max.Y < glass.Min.Y);
    }

    [Fact]
    public void Glass_hinges_hold_the_leaf_at_its_hinge_side_two_or_three_by_its_height()
    {
        static List<JoineryBox> Hinges(JoinerySpec s) => Joinery.Boxes(s).Where(b => b.Role == "hardware" && Math.Abs(b.Max.Z - b.Min.Z - 0.09) < 1e-9).ToList();
        var low = Hinges(Door("hinges"));
        Assert.Equal(2, low.Count);
        // On the right (swing-right's hinges), reaching across the gap to the jamb.
        var right = Hinges(Door("hinges", panels: [new(0, 0, "swing-right")]));
        Assert.All(right, b => Assert.Equal(0.45, b.Max.X, 9));
        Assert.Equal(3, Hinges(Door("hinges", 2.5)).Count);
        // No patch fittings then.
        Assert.DoesNotContain(Joinery.Boxes(Door("hinges")), b => b.Role == "hardware" && b.Max.X - b.Min.X > 0.15);
    }

    [Fact]
    public void A_sliding_leaf_hangs_from_a_track_in_front_of_the_fixed_pane_and_runs_past_it()
    {
        var s = Door(columns: [1, 1], panels: [new(0, 0, "fixed"), new(1, 0, "door-sliding-left")]) with { Width = 1.8 };
        Assert.Null(Joinery.Problem(s));
        var boxes = Joinery.Boxes(s);
        var glass = boxes.Where(b => b.Role == "glass").OrderBy(b => b.Min.X).ToList();
        Assert.Equal(2, glass.Count);
        var (pane, leaf) = (glass[0], glass[1]);
        // The leaf towards the room by the glass and 20 mm, past the pane by 50 mm, under the track.
        Assert.Equal(pane.Min.Y - 0.012 - 0.02, leaf.Min.Y, 9);
        Assert.Equal(pane.Max.X + 0.004 - 0.05, leaf.Min.X, 9);
        Assert.Equal(2.1 - Frameless.Rail - 0.004, leaf.Max.Z, 9);
        // The pane full height in its channels, at the floor and under the head.
        Assert.Equal(2.1 - 0.004, pane.Max.Z, 9);
        Assert.Equal(2, boxes.Count(b => b.Role == "hardware" && Math.Abs(b.Max.Z - b.Min.Z - 0.03) < 1e-9));
        // The track across the whole opening at the leaf's plane.
        var track = Assert.Single(boxes, b => b.Role == "hardware" && b.Max.X - b.Min.X > 1.7);
        Assert.Equal(2.1, track.Max.Z, 9);
        Assert.True(track.Min.Y < leaf.Min.Y && track.Max.Y > leaf.Max.Y);
        Assert.Equal("SLIDING_TO_LEFT", Joinery.DoorOperation(s).Type);
    }

    [Fact]
    public void What_frameless_glass_cannot_be_is_refused()
    {
        Assert.Contains("rectangle", Joinery.Problem(Door() with { Shape = new JoineryShape("round") }));
        Assert.Contains("fold", Joinery.Problem(Door(panels: [new(0, 0, "folding-left")])));
        Assert.Contains("6 to 25 mm", Joinery.Problem(Door() with { Frameless = new JoineryFrameless(Glass: 0.03) }));
        Assert.Contains("not a fixing", Joinery.Problem(Door() with { Frameless = new JoineryFrameless("clamps") }));
        var window = JoinerySpec.DefaultWindow() with { Frameless = new JoineryFrameless() };
        Assert.Contains("fixed glazing", Joinery.Problem(window));
        Assert.Null(Joinery.Problem(window with { Panels = [new(0, 0, "fixed"), new(1, 0, "fixed")] }));
    }

    [Fact]
    public void The_plan_draws_the_leaf_open_as_its_glass_and_never_closed()
    {
        var s = Door();
        var plan = JoinerySymbols.Plan(s);
        (double W, double D) Size(JStroke k) => (k.Pts.Max(p => p.X) - k.Pts.Min(p => p.X), k.Pts.Max(p => p.Y) - k.Pts.Min(p => p.Y));
        // The open leaf: the glass's thickness by the leaf's width; no frame, nothing across the opening.
        Assert.Contains(plan, k => k.Weight == "cut" && Math.Abs(Size(k).W - 0.012) < 1e-9 && Math.Abs(Size(k).D - 0.892) < 1e-6);
        Assert.DoesNotContain(plan, k => k.Closed && Size(k).W > 0.5);
        Assert.Contains(plan, k => !k.Closed && k.Pts.Count > 4);
        // The elevation shows the patch fittings and no sash inside the glass.
        var elevation = JoinerySymbols.Elevation(s);
        Assert.Equal(3, elevation.Count(k => k.Closed && Math.Abs(Size(k).W - 0.18) < 1e-9));
    }

    [Fact]
    public void A_frameless_type_is_kept_placed_and_written_to_IFC_without_a_lining()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 6, 0);
        var spec = Door();
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 2, ["typeId"] = type })!["id"]!;
        var back = Openings.SpecOf(f.Model, door);
        Assert.Equal(new JoineryFrameless("pivot"), back?.Frameless);
        Assert.NotNull(f.Model.Get(door));

        var ifc = JoineryIfc.Of(spec);
        Assert.Equal(0.012, ifc.Lining.Depth, 9);
        Assert.Equal(0, ifc.Lining.Thickness);
        Assert.Equal("SINGLE_SWING_LEFT", ifc.Operation);

        var path = Path.Combine(_dir, "glass.ifc");
        IfcExport.Write(f.Session, path);
        var text = File.ReadAllText(path);
        Assert.Contains("IFCDOOR(", text);
        Assert.Contains("IFCDOORLININGPROPERTIES(", text);
    }
}
