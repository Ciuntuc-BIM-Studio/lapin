using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Report #92, decision 338: a dimension's segment across a door, a window or a bare wall opening writes the opening's
/// height under the line (the width over it), as the drawing style says (150, h=150, h=150 with p=90, 150/90), read
/// from the model each time; on in a new project, off in an older one until its dimension style says so; plans and
/// elevations; printed.
/// </summary>
public class OpeningHeightTests
{
    /// <summary>A 6 m wall of 20 cm on its axis (faces at y = ±0.1) with a bare opening 1.20 × 1.50 at 3 m, its sill at 0.90.</summary>
    static (Fixture F, string Wall, string Opening) Setup(bool fresh = true)
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        if (fresh) f.Session.New();
        var type = f.Type("Core 20", ("Brick", 0.2, true));
        var wall = f.Wall(0, 0, 6, 0, type);
        var opening = (string)f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["width"] = 1.2, ["height"] = 1.5, ["sill"] = 0.9 })!["id"]!;
        return (f, wall, opening);
    }

    static string Plan(Fixture f) => "plan:" + f.Storey;

    static string Dim(Fixture f, string view, params (double X, double Y)[] pts) =>
        (string)f.Run("drafting.addDimension", new JsonObject
        {
            ["viewId"] = view, ["pts"] = new JsonArray(pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray()),
            ["at"] = new JsonArray(pts[0].X, pts[0].Y - 1), ["dir"] = new JsonArray(1, 0),
        })!["id"]!;

    /// <summary>The texts a dimension draws on its view, in the order it writes them.</summary>
    static List<PenText> Texts(Fixture f, string view, string dim, bool print = false) =>
        Views.Render(f.Session.Reader, Views.Find(f.Model, view)!, print).Marks.Single(m => m.Owner == dim).Texts.Select(t => t.Text).ToList();

    static void Style(Fixture f, string format) =>
        f.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = new JsonObject { ["id"] = Standards.Standard, ["openingHeight"] = format } });

    [Fact]
    public void A_new_projects_dimension_across_an_opening_writes_its_height_under_the_line()
    {
        var (f, _, _) = Setup();
        Assert.Equal("number", Standards.DimOf(DraftingResources.Book(f.Model), Standards.Standard).OpeningHeight);
        // A string from the wall's start to both jambs: only the segment across the opening writes a height.
        var dim = Dim(f, Plan(f), (0, -0.1), (2.4, -0.1), (3.6, -0.1));
        var texts = Texts(f, Plan(f), dim);
        Assert.Equal(["2400", "1200", "1500"], texts.Select(t => t.Text));
        // The width over the dimension line (y = −1.1), the height under it, both at the segment's middle.
        Assert.True(texts[1].At.Y > -1.1);
        Assert.True(texts[2].At.Y < -1.1 - 2.5 * 0.1, "the height lies a text's height and its gap under the line");
        Assert.Equal(3, texts[2].At.X, 6);
        // Measured on the other face, or held on the axis, it is the same opening.
        Assert.Equal(["1200", "1500"], Texts(f, Plan(f), Dim(f, Plan(f), (2.4, 0.1), (3.6, 0.1))).Select(t => t.Text));
    }

    [Fact]
    public void The_height_follows_the_opening_and_goes_when_the_segment_no_longer_spans_it()
    {
        var (f, _, opening) = Setup();
        var dim = Dim(f, Plan(f), (2.4, -0.1), (3.6, -0.1));
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = opening, ["height"] = 1.8 });
        Assert.Equal(["1200", "1800"], Texts(f, Plan(f), dim).Select(t => t.Text));
        // Moved 1 m along: the dimension's points stay on the wall's face, measuring nothing it spans.
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = opening, ["along"] = 4 });
        Assert.DoesNotContain(Texts(f, Plan(f), dim), t => t.Text == "1800");
        // A segment's own text (typed over its measure) writes no height either.
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = opening, ["along"] = 3 });
        f.Run("drafting.setDimensionText", new JsonObject { ["id"] = dim, ["text"] = "120" });
        Assert.Equal(["120"], Texts(f, Plan(f), dim).Select(t => t.Text));
    }

    [Fact]
    public void The_style_writes_it_as_a_number_h_with_the_sill_or_over_the_sill()
    {
        var (f, wall, _) = Setup();
        var window = Dim(f, Plan(f), (2.4, -0.1), (3.6, -0.1));
        // A door-like opening, its sill at the floor: no p=.
        f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 1, ["width"] = 0.9, ["height"] = 2.1 });
        var door = Dim(f, Plan(f), (0.55, -0.1), (1.45, -0.1));
        Style(f, "h");
        Assert.Equal(["1200", "h=1500"], Texts(f, Plan(f), window).Select(t => t.Text));
        Style(f, "hp");
        var rows = Texts(f, Plan(f), window);
        Assert.Equal(["1200", "h=1500", "p=900"], rows.Select(t => t.Text));
        Assert.True(rows[2].At.Y < rows[1].At.Y, "the sill under the height");
        Assert.Equal(["900", "h=2100"], Texts(f, Plan(f), door).Select(t => t.Text));
        Style(f, "slash");
        Assert.Equal(["1200", "1500/900"], Texts(f, Plan(f), window).Select(t => t.Text));
        Assert.Equal(["900", "2100"], Texts(f, Plan(f), door).Select(t => t.Text));
        Style(f, "off");
        Assert.Equal(["1200"], Texts(f, Plan(f), window).Select(t => t.Text));
        // In the style's unit: centimetres with one decimal.
        f.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = new JsonObject { ["id"] = Standards.Standard, ["openingHeight"] = "number", ["unit"] = "cm", ["precision"] = 0 } });
        Assert.Equal(["120", "150"], Texts(f, Plan(f), window).Select(t => t.Text));
    }

    [Fact]
    public void An_older_project_keeps_its_dimensions_as_they_were_until_its_style_says_otherwise()
    {
        var (f, _, _) = Setup(fresh: false);
        Assert.Equal("off", Standards.DimOf(DraftingResources.Book(f.Model), Standards.Standard).OpeningHeight);
        var dim = Dim(f, Plan(f), (2.4, -0.1), (3.6, -0.1));
        Assert.Equal(["1200"], Texts(f, Plan(f), dim).Select(t => t.Text));
        Style(f, "number");
        Assert.Equal(["1200", "1500"], Texts(f, Plan(f), dim).Select(t => t.Text));
        // A new style in a new project writes heights as its Standard does; one dimension may turn them off for itself.
        var (g, _, _) = Setup();
        var made = (string)g.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = new JsonObject { ["name"] = "Mine" } })!["id"]!;
        Assert.Equal("number", Standards.DimOf(DraftingResources.Book(g.Model), made).OpeningHeight);
        Assert.Equal("off", Standards.ResolveDim(DraftingResources.Book(g.Model), new JsonObject { ["o.openingHeight"] = "off" }).OpeningHeight);
    }

    [Fact]
    public void An_elevation_writes_it_under_a_dimension_across_the_opening_not_under_one_up_its_height()
    {
        var (f, _, _) = Setup();
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var spec = Views.Generate(f.Session.Reader, Views.Find(f.Model, view)!).Spec!;
        var z = Spatial.Storeys(f.Model).First().Num("elevation");
        Vec2 P(double x, double h) => spec.Frame.Map(new Vec3(x, -0.1, z + h));
        var (a, b) = (P(2.4, 1.2), P(3.6, 1.2));
        var across = Dim(f, view, (a.X, a.Y), (b.X, b.Y));
        var texts = Texts(f, view, across);
        Assert.Equal(["1200", "1500"], texts.Select(t => t.Text));
        // Up its height (a vertical dimension from its sill to its head): that is the height itself, nothing more.
        var (s, h) = (P(2.4, 0.9), P(2.4, 2.4));
        var up = (string)f.Run("drafting.addDimension", new JsonObject
        {
            ["viewId"] = view, ["pts"] = new JsonArray(new JsonArray(s.X, s.Y), new JsonArray(h.X, h.Y)), ["at"] = new JsonArray(s.X - 1, s.Y), ["dir"] = new JsonArray(0, 1),
        })!["id"]!;
        Assert.Equal(["1500"], Texts(f, view, up).Select(t => t.Text));
    }

    [Fact]
    public void Sheets_the_PDF_and_the_DXF_print_it()
    {
        var (f, _, _) = Setup();
        var dim = Dim(f, Plan(f), (2.4, -0.1), (3.6, -0.1));
        Assert.Contains(Texts(f, Plan(f), dim, print: true), t => t.Text == "1500");
        var dxf = System.Text.Encoding.Latin1.GetString(Views.Dxf(f.Session.Reader, Views.Find(f.Model, Plan(f))!));
        Assert.Matches(@"\n\s*1\r?\n1500\r?\n", dxf);
    }
}
