using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Engine.Tests;

/// <summary>
/// An arched casement (a tester's report #112: "an arched window with no bars in between"): a headed type without the
/// transom at the spring line, its one sash following the arch all round, opening side-hung.
/// </summary>
public class ArchedSashTests
{
    static JoinerySpec Casement(string kind = "round", double? rise = null, bool? transom = false) => JoinerySpec.DefaultWindow() with
    {
        Width = 1.2, Height = 1.8,
        Shape = new JoineryShape(kind, rise, Transom: transom),
        Columns = [1], Rows = [1],
        Panels = [new JoineryPanel(0, 0, "side-left")],
    };

    static double Area(IReadOnlyList<Vec2> loop) => Math.Abs(Polygon2.SignedArea(loop));

    [Fact]
    public void Without_its_transom_the_sash_follows_the_arch_and_nothing_crosses_under_it()
    {
        var s = Casement();
        Assert.True(JoineryShapes.ArchedPanes(s));
        Assert.Null(Joinery.Problem(s));
        var (fw, sw) = (s.Frame.Width, s.Sash.Width);
        var zs = JoineryShapes.Spring(s);
        // The grid reaches the head's inner apex instead of stopping under a transom.
        Assert.Equal(s.Height - fw, Joinery.InnerRect(s).Z1, 9);
        var boxes = Joinery.Boxes(s);
        // No member lies across the opening at the spring line.
        Assert.DoesNotContain(boxes, b => b.Role == "frame" && b.Profile == null && b.Min.Z > 0.1 && b.Max.X - b.Min.X > s.Width / 2);
        // The sash is one ring following the outline, up into the arch; the glass inside it.
        var sash = boxes.Single(b => b.Role == "sash" && b.Holes is { Count: 1 } && b.Max.Y - b.Min.Y >= s.Sash.Depth - 1e-9);
        Assert.Equal(s.Height - fw, sash.Max.Z, 6);
        var glass = boxes.Single(b => b.Role == "glass");
        Assert.NotNull(glass.Profile);
        Assert.Equal(s.Height - fw - sw, glass.Max.Z, 6);
        // The ring's area: the pane (a rectangle to the spring and a half circle over it) less the glass's, both by the chord rule within 0.5 %.
        double Pane(double inset)
        {
            var (half, z0) = (s.Width / 2 - fw - inset, fw + inset);
            return 2 * half * (zs - z0) + Math.PI * half * half / 2;
        }
        Assert.Equal(Pane(0), Area(sash.Profile!), Pane(0) * 0.005);
        Assert.Equal(Pane(sw), Area(sash.Holes![0]), Pane(sw) * 0.005);
        Assert.Equal(Pane(sw), Area(glass.Profile!), Pane(sw) * 0.005);
        // Every point of the sash's outline lies on or under the frame's inner curve.
        Assert.All(sash.Profile!, p => Assert.True(p.Y <= JoineryShapes.Top(s, p.X, fw) + 1e-6));
    }

    [Fact]
    public void A_headed_type_keeps_its_transom_and_fanlight_unless_told_otherwise()
    {
        foreach (bool? transom in new bool?[] { null, true })
        {
            var s = Casement(transom: transom);
            Assert.False(JoineryShapes.ArchedPanes(s));
            Assert.Equal(JoineryShapes.Spring(s) - s.Frame.Transom, Joinery.InnerRect(s).Z1, 9);
            Assert.Contains(Joinery.Boxes(s), b => b.Role == "frame" && b.Profile == null && Math.Abs(b.Max.Z - JoineryShapes.Spring(s)) < 1e-9);
        }
    }

    [Theory]
    [InlineData("segmental", 0.25)]
    [InlineData("elliptical", null)]
    [InlineData("pointed", null)]
    [InlineData("radiused", null)]
    [InlineData("gable", 0.3)]
    public void Every_head_kind_takes_a_sash_that_follows_it(string kind, double? rise)
    {
        var s = Casement(kind, rise);
        Assert.Null(Joinery.Problem(s));
        var sash = Joinery.Boxes(s).Single(b => b.Role == "sash" && b.Holes is { Count: 1 } && b.Max.Y - b.Min.Y >= s.Sash.Depth - 1e-9);
        Assert.Equal(JoineryShapes.Top(s, 0, s.Frame.Width), sash.Max.Z, 3);
        Assert.All(sash.Profile!, p => Assert.True(p.Y <= JoineryShapes.Top(s, p.X, s.Frame.Width) + 1e-6));
    }

    [Fact]
    public void The_elevation_draws_the_sash_round_the_arch_and_its_opening_lines_meet_the_arch()
    {
        var s = Casement();
        var fw = s.Frame.Width;
        var zs = JoineryShapes.Spring(s);
        var strokes = JoinerySymbols.Elevation(s);
        // The sash's outline and its inner edge are closed loops rising above the spring line.
        Assert.True(strokes.Count(st => st.Closed && st.Pts.Max(p => p.Y) > zs + 0.2 && st.Pts.Max(p => p.Y) < s.Height - fw + 1e-6) >= 2);
        // No line across at the spring (no transom drawn).
        Assert.DoesNotContain(strokes, st => !st.Closed && st.Pts.Count == 2 && Math.Abs(st.Pts[0].Y - st.Pts[1].Y) < 1e-9 && st.Pts[0].Y > zs - s.Frame.Transom - 0.01 && st.Pts[0].Y < zs + 0.01);
        // Hinged on the left: the symbol's lines leave the hinge side's ends, its top end where the arch meets the jamb.
        var marks = JoinerySymbols.OpeningMarks("side-left", Joinery.PanelRect(s, s.Panels[0]), false, "handle", JoineryShapes.PaneTop(s, Joinery.PanelRect(s, s.Panels[0])));
        var tri = Assert.Single(marks);
        Assert.All(tri.Pts, p => Assert.True(p.Y <= JoineryShapes.Top(s, p.X, fw) + 1e-6));
        Assert.Equal(JoineryShapes.Top(s, s.Width / 2 - fw, fw), tri.Pts[0].Y, 6);
    }

    [Fact]
    public void Mullions_and_glazing_bars_stop_at_the_arch()
    {
        var s = Casement() with
        {
            Columns = [1, 1],
            Panels = [new JoineryPanel(0, 0, "side-left"), new JoineryPanel(1, 0, "side-right")],
            Muntins = new JoineryMuntins("grid", 2, 3),
        };
        Assert.Null(Joinery.Problem(s));
        var fw = s.Frame.Width;
        var boxes = Joinery.Boxes(s);
        var mullion = boxes.Single(b => b.Role == "frame" && b.Profile is { Count: 4 } && Math.Abs((b.Min.X + b.Max.X) / 2) < 1e-6);
        Assert.Equal(JoineryShapes.Top(s, mullion.Max.X, fw), mullion.Max.Z, 6);
        var muntins = boxes.Where(b => b.Role == "muntin").ToList();
        Assert.NotEmpty(muntins);
        Assert.All(muntins.SelectMany(b => b.Profile!), p => Assert.True(p.Y <= JoineryShapes.Top(s, p.X, fw) + 0.02));
    }

    [Fact]
    public void A_top_row_too_far_up_the_head_is_refused()
    {
        // Two rows, the upper starting above the spring: the arch meets its bottom at the jambs.
        var s = Casement() with { Rows = [3, 1], Panels = [new JoineryPanel(0, 0, "side-left"), new JoineryPanel(0, 1, "fixed")] };
        Assert.Contains("top row", Joinery.Problem(s));
    }

    [Fact]
    public void A_placed_arched_casement_builds_and_says_in_IFC_it_is_one_side_hung_panel()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 6, 0);
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = Casement().ToJson() })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["typeId"] = type })!["id"]!;
        var g = f.Session.Geometry(window)!;
        Assert.True(g.Parts.Sum(p => Measure.Volume(p.Solid)) > 0);
        var ifc = JoineryIfc.Of(Casement());
        Assert.Equal("SINGLE_PANEL", ifc.Operation);
        Assert.Equal("SIDEHUNGLEFTHAND", Assert.Single(ifc.Panels).Operation);
        // The stored spec reads back with its choice.
        var stored = JoinerySpec.From(f.Session.Model.Get(type)!.Attrs["spec"]);
        Assert.False(stored!.Shape!.Transom);
    }
}
