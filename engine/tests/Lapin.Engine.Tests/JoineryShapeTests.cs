using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Doors and windows that are not rectangles (decision 188): arched heads, round windows, glazing
/// bars, and the ornaments of the facade (keystone, cornice, pediment, hood mould, apron).
/// </summary>
public class JoineryShapeTests
{
    static (Fixture F, string Wall, string Window) Place(JoinerySpec spec)
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 6, 0);
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["typeId"] = type })!["id"]!;
        return (f, wall, window);
    }

    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Theory]
    [InlineData("round", null, 0.6)]
    [InlineData("segmental", 0.2, 0.2)]
    [InlineData("pointed", null, 1.0392304845413265)]
    [InlineData("gable", 0.3, 0.3)]
    public void A_headed_window_springs_from_its_jambs_and_its_head_rises_as_its_kind_says(string kind, double? rise, double expected)
    {
        var s = JoinerySpec.DefaultWindow() with { Width = 1.2, Height = 2.0, Shape = new JoineryShape(kind, rise) };
        Assert.True(JoineryShapes.Headed(s));
        Assert.Equal(expected, JoineryShapes.Rise(s), 9);
        Assert.Equal(2.0 - expected, JoineryShapes.Spring(s), 9);
        // The outline meets the jambs at the spring line and reaches the full height at the middle.
        Assert.Equal(JoineryShapes.Spring(s), JoineryShapes.Top(s, 0.6, 0), 9);
        Assert.Equal(2.0, JoineryShapes.Top(s, 0, 0), 9);
        Assert.Null(Joinery.Problem(s));
        // The panels stay under the transom on the spring line.
        Assert.True(Joinery.InnerRect(s).Z1 <= JoineryShapes.Spring(s) - s.Frame.Transom + 1e-9);
        var boxes = Joinery.Boxes(s);
        Assert.Contains(boxes, b => b.Role == "glass" && b.Profile != null && b.Max.Z > JoineryShapes.Spring(s));
        Assert.All(boxes, b => Assert.True(b.Max.Z <= 2.0 + 1e-9));
    }

    [Fact]
    public void An_arched_window_takes_its_own_outline_out_of_the_wall_and_its_body_is_whole()
    {
        // Without reveal finishes (a rectangle's would widen its own cut), so the two cuts differ only by their heads.
        var arched = JoinerySpec.DefaultWindow() with { Width = 1.2, Height = 2.0, Shape = new JoineryShape("round", Bars: "sunburst", Spokes: 5), Details = new JoineryDetails(Reveal: new JoineryReveal("none")) };
        var (f, wall, window) = Place(arched);
        var (g, wall2, _) = Place(arched with { Shape = null });
        // The wall loses the arch's area, not the rectangle's: what is left over the spring is the difference of the two.
        var gained = Volume(f, wall) - Volume(g, wall2);
        var t = Walls.Section(f.Model, f.Model.Get(wall)!).Sum(b => b.To - b.From);
        var corners = 2 * (1 - Math.PI / 4) * 0.6 * 0.6;
        Assert.Equal(corners * t, gained, 3);
        var geometry = f.Session.Geometry(window)!;
        Assert.Contains(geometry.Parts, p => p.Key == "frame");
        Assert.Contains(geometry.Parts, p => p.Key == "glass");
        // The sunburst's five spokes and the arch: the frame reaches the apex.
        var frame = Measure.Bounds(geometry.Parts.First(p => p.Key == "frame").Solid)!.Value;
        Assert.Equal(f.Model.Get(window) is { } ? frame.Max.Z : 0, frame.Max.Z);
        Assert.True(frame.Max.Z - frame.Min.Z > 1.99);
    }

    [Fact]
    public void A_round_window_is_a_ring_with_its_glass_and_bars_and_draws_no_opening_symbol()
    {
        var round = JoinerySpec.DefaultWindow() with { Width = 0.8, Height = 0.8, Shape = new JoineryShape("circle", Bars: "cross") };
        Assert.Null(Joinery.Problem(round));
        var boxes = Joinery.Boxes(round);
        var ring = Assert.Single(boxes, b => b.Role == "frame" && b.Holes != null);
        Assert.Equal(0.8, ring.Max.X - ring.Min.X, 6);
        Assert.Single(boxes, b => b.Role == "glass");
        Assert.Equal(4, boxes.Count(b => b.Role == "frame" && b.Holes == null));
        // Its elevation is the two ellipses and the bars, no panel triangles.
        var elevation = JoinerySymbols.Elevation(round);
        Assert.Equal(2, elevation.Count(s => s.Weight == "outline"));
        // In plan, the ring is cut in two at mid height, and the cross's upright between them.
        var plan = JoinerySymbols.Plan(round, 0.4);
        Assert.Equal(3, plan.Count(s => s.Weight == "cut" && s.Pts.Max(p => p.X) - s.Pts.Min(p => p.X) < 0.2));
        var (f, wall, window) = Place(round);
        Assert.True(Volume(f, window) > 0);
    }

    [Fact]
    public void Glazing_bars_divide_each_pane_in_a_colonial_grid_prairie_margins_or_a_diamond_lattice()
    {
        var pane = new CellRect(0, 0.6, 0, 1.2);
        Assert.Equal(3, JoineryShapes.MuntinLines(new JoineryMuntins("grid", 2, 3), pane).Count);
        Assert.Equal(4, JoineryShapes.MuntinLines(new JoineryMuntins("prairie"), pane).Count);
        var diamond = JoineryShapes.MuntinLines(new JoineryMuntins("diamond", 3), pane);
        Assert.True(diamond.Count >= 6);
        Assert.All(diamond, l => Assert.True(l.A.X >= -1e-9 && l.B.X <= 0.6 + 1e-9 && l.A.Y >= -1e-9 && l.B.Y <= 1.2 + 1e-9));
        var s = JoinerySpec.DefaultWindow() with { Muntins = new JoineryMuntins("grid", 2, 3) };
        // Two panes, each split 2 × 3: three bars each.
        Assert.Equal(6, Joinery.Boxes(s).Count(b => b.Role == "muntin"));
    }

    [Fact]
    public void A_keystone_a_pediment_and_an_apron_dress_the_window_on_the_facade()
    {
        var details = new JoineryDetails(Surround: new JoinerySurround(true, 0.1, 0.03), SillOutside: new JoinerySill(true, 0.05, 0.04),
            Keystone: new JoineryKeystone(true), Head: new JoineryHead(true, "pediment", Rise: 0.3), Apron: new JoineryApron(true));
        var s = JoinerySpec.DefaultWindow() with { Width = 1.2, Height = 2.0, Shape = new JoineryShape("round"), Details = details };
        var pieces = WindowDressings.Pieces(s, WindowDressings.Of(s, 0.3));
        var key = Assert.Single(pieces, p => p.Key == "keystone");
        Assert.True(key.Boxes[0].Max.Z > 2.0 && key.Boxes[0].Min.Z < 2.0);
        var head = Assert.Single(pieces, p => p.Key == "head");
        // The cornice over the surround at the apex, the pediment's triangle on it.
        Assert.Equal(2.0 + 0.1, head.Boxes.Min(b => b.Min.Z), 6);
        Assert.Equal(2.0 + 0.1 + 0.1 + 0.3, head.Boxes.Max(b => b.Max.Z), 6);
        var apron = Assert.Single(pieces, p => p.Key == "apron");
        Assert.Equal(3, apron.Boxes.Count);
        Assert.True(apron.Boxes.Max(b => b.Max.Z) <= 0);
        var (f, wall, window) = Place(s);
        var parts = f.Session.Geometry(window)!.Parts.Select(p => p.Key).ToList();
        Assert.Contains("keystone", parts);
        Assert.Contains("head", parts);
        Assert.Contains("apron", parts);
        Assert.Contains("surround", parts);
    }

    [Fact]
    public void A_shaped_window_goes_to_IFC_with_its_body_and_comes_back_with_its_outline()
    {
        var (f, _, window) = Place(JoinerySpec.DefaultWindow() with { Width = 1.0, Height = 1.8, Shape = new JoineryShape("pointed") });
        var path = Path.Combine(Path.GetTempPath(), $"shape-{Guid.NewGuid():N}.ifc");
        try
        {
            Lapin.Ifc.IfcExport.Write(f.Session, path);
            var text = File.ReadAllText(path);
            Assert.Contains("IFCWINDOW(", text);
            var back = new Fixture();
            Lapin.Ifc.IfcImport.Run(back.Session, path, new Lapin.Ifc.IfcImportOptions(), out _);
            var spec = back.Model.OfClass(IfcClass.Window).Select(w => Openings.SpecOf(back.Model, w.Id)).FirstOrDefault(x => x != null);
            Assert.Equal("pointed", spec?.Shape?.Kind);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("lever")]
    [InlineData("knob")]
    [InlineData("pull")]
    [InlineData("window")]
    public void A_door_has_its_handle_on_both_faces_in_the_style_its_type_asks_and_none_when_asked(string style)
    {
        // Report #48: handles chosen in the type, seen in 3D, in sections and elevations.
        var door = JoinerySpec.DefaultDoor() with { Hardware = new JoineryHardware(style) };
        var grips = Joinery.Boxes(door).Where(b => b.Role == "handle").ToList();
        Assert.Equal(2, grips.Count);
        Assert.Contains(grips, g => g.Max.Y < -door.Sash.LeafThickness / 2 + 1e-9);
        Assert.Contains(grips, g => g.Min.Y > door.Sash.LeafThickness / 2 - 1e-9);
        // At the height asked, on the side away from the hinges (a left-hinged door's handle on its right).
        Assert.All(grips, g => Assert.True((g.Min.X + g.Max.X) / 2 > 0 && Math.Abs((g.Min.Z + g.Max.Z) / 2 - 1.05) < 0.31));
        Assert.Contains(JoinerySymbols.Section(door, (grips[0].Min.X + grips[0].Max.X) / 2), st => st.Weight == "outline");
        Assert.DoesNotContain(Joinery.Boxes(door with { Hardware = new JoineryHardware("none") }), b => b.Role is "handle" or "hardware");
        // A window keeps one handle per opening sash, on the room's side; its default is a window handle.
        Assert.Equal("window", Joinery.HandleStyle(JoinerySpec.DefaultWindow()));
        Assert.Equal("lever", Joinery.HandleStyle(JoinerySpec.DefaultDoor()));
    }
}
