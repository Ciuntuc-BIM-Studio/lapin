using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Surface hatches in elevations and sections (decision 325): the faces a drawing sees hatched with their material's
/// elevation graphics where nothing nearer hides them; a brick facing hatched round its window and not inside it, a face
/// behind another left bare, one material going on in one plane across two walls hatched as one, the view style's switch,
/// and a section hatching what it sees beyond its cut (its cut areas hiding what lies behind them). Decision 327: a
/// material's name suggests its elevation hatch (brick, stone, boards, cladding; never insulation's BATT, which stays a
/// section's), an explicit none or BATT honoured; developed sections hatched and shadowed piece by piece.
/// </summary>
public class SurfaceHatchTests
{
    /// <summary>A wall type with a brick facing on both sides of a concrete core; the brick hatched BRICK in elevations.</summary>
    static string BrickType(Fixture f, string? align = null)
    {
        var type = f.Type("Brick 40", ("Facing brick", 0.1, false), ("Concrete", 0.2, true), ("Facing brick", 0.1, false));
        var brick = f.Model.OfClass(IfcClass.Material).First(e => e.Name == "Facing brick").Id;
        var g = new JsonObject { ["hatch"] = "BRICK" };
        if (align != null) g["hatchAlign"] = align;
        f.Run("material.update", new JsonObject { ["id"] = brick, ["elevation"] = g });
        return type;
    }

    static string Elevation(Fixture f, string direction = "south") =>
        (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = direction })!["id"]!;

    static Views.Rendered Render(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);

    static List<(Vec2 A, Vec2 B)> Hatch(Views.Rendered r) => r.Pens.Where(p => p.Layer == SurfaceHatches.Layer).SelectMany(p => p.Segments).ToList();

    static Vec2 Mid((Vec2 A, Vec2 B) s) => (s.A + s.B) * 0.5;

    /// <summary>An element's box as the view sees it.</summary>
    static (Vec2 Min, Vec2 Max) Seen(Fixture f, string id, ViewFrame frame)
    {
        var (lo, hi) = f.Bounds(id);
        var pts = Enumerable.Range(0, 8).Select(i => frame.Map(new Vec3((i & 1) == 0 ? lo.X : hi.X, (i & 2) == 0 ? lo.Y : hi.Y, (i & 4) == 0 ? lo.Z : hi.Z))).ToList();
        return (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y)));
    }

    static bool Inside(Vec2 p, (Vec2 Min, Vec2 Max) b, double inset = 0.01) =>
        p.X > b.Min.X + inset && p.X < b.Max.X - inset && p.Y > b.Min.Y + inset && p.Y < b.Max.Y - inset;

    [Fact]
    public void A_brick_facing_is_hatched_in_elevation_round_its_window_and_not_inside_it()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 8, 0, BrickType(f));
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4 })!["id"]!;
        // The south elevation looks north and sees the wall's south face.
        var view = Elevation(f, "south");
        var r = Render(f, view);
        var hatch = Hatch(r);
        Assert.NotEmpty(hatch);
        var face = Seen(f, wall, r.Frame!);
        var opening = Seen(f, window, r.Frame!);
        // Every stroke on the wall's face, none in the window.
        Assert.All(hatch, s => Assert.True(Inside(Mid(s), face, -1e-6), $"{Mid(s)} is off the wall"));
        // (The window's box takes in its sill and lining, a few centimetres past the opening.)
        Assert.DoesNotContain(hatch, s => Inside(Mid(s), opening, 0.1));
        // The courses run the wall's length, on both sides of the window.
        Assert.Contains(hatch, s => Mid(s).X < opening.Min.X - 0.2);
        Assert.Contains(hatch, s => Mid(s).X > opening.Max.X + 0.2);
        // Under the lines: the hatch's pen before the first seen line's.
        var pens = r.Pens.Select(p => p.Layer).ToList();
        Assert.True(pens.IndexOf(SurfaceHatches.Layer) < pens.IndexOf(PenLayer.Seen));
        // The courses of BRICK at 1:100 are 0.4 m apart: horizontal strokes at multiples of 0.4 from the view's origin.
        var levels = hatch.Where(s => Math.Abs(s.A.Y - s.B.Y) < 1e-9).Select(s => Math.Round(s.A.Y, 3)).Distinct().Order().ToList();
        Assert.True(levels.Count >= 5);
        Assert.All(levels, y => Assert.Equal(0, Math.Abs(Math.IEEERemainder(y, 0.4)), 3));
    }

    [Fact]
    public void A_face_behind_a_nearer_one_is_not_hatched()
    {
        var f = new Fixture();
        f.Wall(0, 0, 10, 0, BrickType(f));
        // A concrete wall in front of it (to the south), with no elevation hatch of its own.
        var plain = f.Type("Concrete 20", ("Concrete", 0.2, true));
        var front = f.Wall(3, -2, 6, -2, plain);
        var r = Render(f, Elevation(f, "south"));
        var hatch = Hatch(r);
        Assert.NotEmpty(hatch);
        var near = Seen(f, front, r.Frame!);
        Assert.DoesNotContain(hatch, s => Inside(Mid(s), near));
        Assert.Contains(hatch, s => Mid(s).X < near.Min.X - 0.2 && Mid(s).Y > near.Min.Y + 0.2 && Mid(s).Y < near.Max.Y - 0.2);
    }

    /// <summary>The distinct x of the vertical strokes and y of the horizontal ones (mm).</summary>
    static (HashSet<double> Xs, HashSet<double> Ys) Grid(List<(Vec2 A, Vec2 B)> hatch) => (
        hatch.Where(s => Math.Abs(s.A.X - s.B.X) < 1e-9).Select(s => Math.Round(s.A.X, 3)).ToHashSet(),
        hatch.Where(s => Math.Abs(s.A.Y - s.B.Y) < 1e-9).Select(s => Math.Round(s.A.Y, 3)).ToHashSet());

    [Fact]
    public void One_material_going_on_in_one_plane_across_two_walls_is_hatched_as_one()
    {
        // Aligned to the layer, each face's hatch starts at its surface's corner: two walls in one line share theirs.
        var one = new Fixture();
        one.Wall(0, 0, 9.7, 0, BrickType(one, "layer"));
        var whole = Grid(Hatch(Render(one, Elevation(one))));
        var two = new Fixture();
        var type = BrickType(two, "layer");
        two.Wall(0, 0, 4.3, 0, type);
        two.Wall(4.3, 0, 9.7, 0, type);
        var view = Elevation(two);
        var split = Grid(Hatch(Render(two, view)));
        Assert.NotEmpty(whole.Xs);
        Assert.Equal(whole.Ys.Order(), split.Ys.Order());
        Assert.Equal(whole.Xs.Order(), split.Xs.Order());
        // Without the style's joins each wall's hatch starts on its own: the second wall's joints move.
        two.Run("view.setStyle", new JsonObject { ["id"] = view, ["joins"] = false });
        var apart = Grid(Hatch(Render(two, view)));
        Assert.NotEqual(whole.Xs.Order(), apart.Xs.Order());
    }

    [Fact]
    public void The_view_style_s_switch_turns_them_off_and_structural_views_draw_none()
    {
        var f = new Fixture();
        f.Wall(0, 0, 8, 0, BrickType(f));
        var view = Elevation(f);
        Assert.NotEmpty(Hatch(Render(f, view)));
        Assert.True(ViewStyles.Of(f.Model, Views.Find(f.Model, view)!).SurfaceHatch);
        f.Run("view.setStyle", new JsonObject { ["id"] = view, ["surfaceHatch"] = false });
        Assert.Empty(Hatch(Render(f, view)));
        f.Run("view.setStyle", new JsonObject { ["id"] = view, ["surfaceHatch"] = null });
        Assert.NotEmpty(Hatch(Render(f, view)));
        // A structural elevation: off by its style.
        var structural = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south", ["discipline"] = "Structure" })!["id"]!;
        Assert.False(ViewStyles.Of(f.Model, Views.Find(f.Model, structural)!).SurfaceHatch);
        Assert.Empty(Hatch(Render(f, structural)));
        // A material without elevation graphics whose name suggests none (render, concrete) hatches nothing.
        var g = new Fixture();
        g.Wall(0, 0, 8, 0, g.Type("Rendered 30", ("Lime render", 0.3, true)));
        Assert.Empty(Hatch(Render(g, Elevation(g))));
        // A plan never.
        Assert.DoesNotContain(Views.Render(f.Session.Reader, Views.Find(f.Model, "plan:" + f.Storey)!).Pens, p => p.Layer == SurfaceHatches.Layer);
    }

    [Fact]
    public void A_section_hatches_what_it_sees_beyond_its_cut_and_not_behind_its_cut_areas()
    {
        var f = new Fixture();
        var brick = BrickType(f);
        var plain = f.Type("Concrete 30", ("Concrete", 0.3, true));
        // The brick wall beyond the cut; a concrete wall the section cuts across, running back to it; a brick wall before the cut.
        f.Wall(0, 5, 10, 5, brick);
        f.Wall(5, -1, 5, 4.8, plain);
        f.Wall(20, -2, 25, -2, brick);
        // Along y = 1 from west to east, looking north (to its left).
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, 1), ["end"] = new JsonArray(30, 1) })!["id"]!;
        var r = Render(f, view);
        var hatch = Hatch(r);
        Assert.NotEmpty(hatch);
        // The wall before the cut is not seen (it is behind the viewer).
        Assert.DoesNotContain(hatch, s => Mid(s).X > 18);
        // The concrete wall's cut area hides the brick behind it.
        var cut = r.Pens.Where(p => p.Layer == PenLayer.Cut).SelectMany(p => p.Segments).SelectMany(s => new[] { s.A, s.B }).ToList();
        var x0 = cut.Min(p => p.X); var x1 = cut.Max(p => p.X);
        Assert.InRange(x1 - x0, 0.29, 0.31);
        Assert.DoesNotContain(hatch, s => Mid(s).X > x0 + 0.01 && Mid(s).X < x1 - 0.01 && Mid(s).Y > 0.05 && Mid(s).Y < 2.5);
        Assert.Contains(hatch, s => Mid(s).X < x0 - 0.2);
        Assert.Contains(hatch, s => Mid(s).X > x1 + 0.2);
    }

    [Theory]
    [InlineData("Facing brick", "BRICK")]
    [InlineData("Zidărie BCA", "BRICK")]
    [InlineData("Cărămidă plină", "BRICK")]
    [InlineData("Ceramic tiles", "TILES")]
    [InlineData("Faianță", "TILES")]
    [InlineData("Larch cladding", "BOARDS")]
    [InlineData("Lemn de brad", "BOARDS")]
    [InlineData("Zinc standing seam", "VERTICAL")]
    [InlineData("Aluminium cladding", "VERTICAL")]
    [InlineData("Tablă cutată", "VERTICAL")]
    [InlineData("Natural stone", "STONE")]
    [InlineData("Piatră naturală", "STONE")]
    [InlineData("Gravel", "GRAVEL")]
    [InlineData("Earth", "EARTH")]
    [InlineData("Concrete C25/30", null)]
    [InlineData("Beton armat", null)]
    [InlineData("Mineral wool", null)]
    [InlineData("Stone wool", null)]
    [InlineData("Vată minerală", null)]
    [InlineData("EPS 100", null)]
    [InlineData("Lime plaster", null)]
    [InlineData("Tencuială decorativă", null)]
    [InlineData("Paint", null)]
    [InlineData("Vapour barrier", null)]
    [InlineData("Bituminous membrane", null)]
    [InlineData("Steel", null)]
    [InlineData("Glass", null)]
    [InlineData("Clay tile", "HORIZONTAL")]
    [InlineData("Concrete tile", "HORIZONTAL")]
    [InlineData("Țiglă ceramică", "HORIZONTAL")]
    [InlineData("Timber shingle", "HORIZONTAL")]
    [InlineData("Slate", "HORIZONTAL")]
    [InlineData("Metal sheet", "VERTICAL")]
    [InlineData("Tile adhesive", null)]
    [InlineData("Timber cladding", "BOARDS")]
    public void A_material_s_name_suggests_its_elevation_hatch(string name, string? hatch)
    {
        var material = new Entity("m", IfcClass.Material, name, []);
        var r = MaterialGraphics.Resolve(material, "elevation");
        Assert.Equal(hatch, r.Hatch);
        Assert.Equal("name", r.Source);
        Assert.Equal(hatch, MaterialGraphics.ElevationFromName(name));
    }

    [Fact]
    public void Insulation_is_batt_in_section_and_nothing_in_elevation_unless_asked()
    {
        var wool = new Entity("m", IfcClass.Material, "Mineral wool", []);
        Assert.Equal("BATT", MaterialGraphics.Resolve(wool, "section").Hatch);
        Assert.Equal("BATT", MaterialGraphics.Resolve(wool, "plan").Hatch);
        Assert.Null(MaterialGraphics.Resolve(wool, "elevation").Hatch);
        // Asked for in its Elevation row, BATT is honoured: its looped line drawn on the face.
        var asked = new Entity("m", IfcClass.Material, "Mineral wool", new JsonObject { ["elevation"] = new JsonObject { ["hatch"] = "BATT" } });
        Assert.Equal("BATT", MaterialGraphics.Resolve(asked, "elevation").Hatch);
        Assert.Equal("own", MaterialGraphics.Resolve(asked, "elevation").Source);
        var f = new Fixture();
        f.Wall(0, 0, 8, 0, f.Type("Insulated", ("Mineral wool", 0.1, false), ("Concrete", 0.2, true)));
        var view = Elevation(f);
        Assert.Empty(Hatch(Render(f, view)));
        var wall = f.Model.OfClass(IfcClass.Material).First(e => e.Name == "Mineral wool").Id;
        f.Run("material.update", new JsonObject { ["id"] = wall, ["elevation"] = new JsonObject { ["hatch"] = "BATT" } });
        Assert.NotEmpty(Hatch(Render(f, view)));
    }

    [Fact]
    public void A_guessed_hatch_is_drawn_and_an_explicit_none_is_respected()
    {
        var f = new Fixture();
        // "Facing brick" with nothing set: BRICK from its name.
        f.Wall(0, 0, 8, 0, f.Type("Brick 40", ("Facing brick", 0.1, false), ("Concrete", 0.2, true), ("Facing brick", 0.1, false)));
        var view = Elevation(f);
        var guessed = Hatch(Render(f, view));
        Assert.NotEmpty(guessed);
        // The same courses as BRICK set by hand.
        var levels = guessed.Where(s => Math.Abs(s.A.Y - s.B.Y) < 1e-9).Select(s => Math.Round(s.A.Y, 3)).Distinct().ToList();
        Assert.All(levels, y => Assert.Equal(0, Math.Abs(Math.IEEERemainder(y, 0.4)), 3));
        // Its Elevation hatch set to none: nothing.
        var brick = f.Model.OfClass(IfcClass.Material).First(e => e.Name == "Facing brick").Id;
        f.Run("material.update", new JsonObject { ["id"] = brick, ["elevation"] = new JsonObject { ["hatch"] = null } });
        Assert.Null(MaterialGraphics.Resolve(f.Model.Get(brick), "elevation").Hatch);
        Assert.Empty(Hatch(Render(f, view)));
        // A guess is not written into IFC: only what the user set.
        var g = new Fixture();
        g.Type("Brick 10", ("Facing brick", 0.1, true));
        var plain = g.Model.OfClass(IfcClass.Material).First(e => e.Name == "Facing brick");
        Assert.Null(MaterialGraphics.Appearance(plain)?["hatches"]?["elevation"]);
    }

    /// <summary>
    /// A jogged section: along +x at y = 1 (x 0…6, looking +y), then along +y at x = 6 (y 1…9, looking −x), laid at 6.
    /// The first piece sees brick wall A (y = 4, x 0…5); the second brick wall B (x = 3, y 6…9), laid at 6 + (y − 1).
    /// Concrete walls D (y = 3, x 1…2) and E (x = 4, y 7…8) cast shadows on them.
    /// </summary>
    static (Fixture F, string View) Jogged()
    {
        var f = new Fixture();
        var brick = BrickType(f);
        var plain = f.Type("Concrete 30", ("Concrete", 0.3, true));
        f.Wall(0, 4, 5, 4, brick);
        f.Wall(3, 6, 3, 9, brick);
        f.Wall(1, 3, 2, 3, plain);
        f.Wall(4, 7, 4, 8, plain);
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["pts"] = new JsonArray(new JsonArray(0, 1), new JsonArray(6, 1), new JsonArray(6, 9)) })!["id"]!;
        return (f, view);
    }

    [Fact]
    public void A_jogged_section_hatches_each_piece_beyond_its_cut_laid_at_its_station()
    {
        var (f, view) = Jogged();
        var r = Render(f, view);
        var hatch = Hatch(r);
        // Wall A on the first piece, x 0…5.
        Assert.Contains(hatch, s => Mid(s).X > 0.2 && Mid(s).X < 4.8);
        // Wall B on the second, laid at 6 + (y − 1): x 11…14.
        Assert.Contains(hatch, s => Mid(s).X > 11.2 && Mid(s).X < 13.8);
        // Nothing past the pieces' ends, nor beyond B; between them only wall A's end, seen by the second piece at x ≈ 9.
        Assert.All(hatch, s => Assert.InRange(Mid(s).X, -1e-6, 14.2));
        Assert.DoesNotContain(hatch, s => Mid(s).X > 5.2 && Mid(s).X < 8.7);
        Assert.DoesNotContain(hatch, s => Mid(s).X > 9.3 && Mid(s).X < 10.8);
        // The pieces' courses run on: BRICK at 1:100 every 0.4 m from the developed drawing's origin.
        var levels = hatch.Where(s => Math.Abs(s.A.Y - s.B.Y) < 1e-9).Select(s => Math.Round(s.A.Y, 3)).Distinct().ToList();
        Assert.All(levels, y => Assert.Equal(0, Math.Abs(Math.IEEERemainder(y, 0.4)), 3));
    }

    [Fact]
    public void A_jogged_section_draws_each_piece_s_sun_shadows()
    {
        var (f, view) = Jogged();
        f.Run("view.setShadows", new JsonObject { ["id"] = view, ["mode"] = "view" });
        var r = Render(f, view);
        var shadows = r.Pens.Where(p => p.Layer == ViewShadows.Layer).SelectMany(p => p.Loops).ToList();
        Assert.NotEmpty(shadows);
        var xs = shadows.Select(l => l.Average(p => p.X)).ToList();
        // D's shadow on A (first piece), E's on B (second), each within its piece.
        Assert.Contains(xs, x => x > 0 && x < 6);
        Assert.Contains(xs, x => x > 11 && x < 14);
        Assert.All(shadows.SelectMany(l => l), p => Assert.InRange(p.X, -1e-6, 14 + 1e-6));
    }

    [Fact]
    public void A_curved_section_hatches_what_each_of_its_pieces_sees()
    {
        var f = new Fixture();
        // A half circle of radius 5 round (5, 0), drawn west to east over its top and looking to its left (outwards):
        // a brick wall along y = 7 beyond its top.
        f.Wall(1, 7, 9, 7, BrickType(f));
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["pts"] = new JsonArray(new JsonArray(0, 0), new JsonArray(5, 5), new JsonArray(10, 0)), ["curve"] = "arc" })!["id"]!;
        var hatch = Hatch(Render(f, view));
        Assert.NotEmpty(hatch);
        var length = Math.PI * 5;
        Assert.All(hatch, s => Assert.InRange(Mid(s).X, -1e-6, length + 1e-6));
        // Seen from both sides of the arc's top, by several pieces.
        Assert.Contains(hatch, s => Mid(s).X < length / 2 - 1);
        Assert.Contains(hatch, s => Mid(s).X > length / 2 + 1);
    }
}
