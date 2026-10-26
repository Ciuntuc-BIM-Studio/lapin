using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The testers' reports of 23.10.2026 (#10, #12, #16, #17, #19): separation pens, line caps, textures per face, imperial units, hatch origins.</summary>
public class ReportFixesTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static Entity Material(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name);

    [Fact]
    public void The_lines_between_a_walls_layers_take_the_separation_pen_its_contour_the_cut_pen()
    {
        var f = F();
        var type = f.Type("Layered", ("Plaster", 0.015, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
        f.Wall(0, 0, 5, 0, type);
        var plan = Plan(f);
        var r = Views.Render(f.Session.Reader, f.Model.Get(plan)!);
        var cut = r.Pens.Where(p => p.Layer == PenLayer.Cut).ToList();
        var contour = Assert.Single(cut, p => Math.Abs(p.Width - Pens.Default.Cut) < 1e-9);
        var separation = Assert.Single(cut, p => Math.Abs(p.Width - Pens.Separation) < 1e-9);
        // The two layer boundaries run the wall's length at y = ±0.125; the contour holds the faces at ±0.14 and the ends.
        Assert.Equal(2, separation.Segments.Count);
        Assert.All(separation.Segments, s => Assert.True(Math.Abs(Math.Abs(s.A.Y) - 0.125) < 1e-6 && Math.Abs(Math.Abs(s.B.Y) - 0.125) < 1e-6));
        Assert.Contains(contour.Segments, s => Math.Abs(s.A.Y - 0.14) < 1e-6 && Math.Abs(s.B.Y - 0.14) < 1e-6);
        Assert.Contains(contour.Segments, s => Math.Abs(s.A.X - s.B.X) < 1e-9); // an end
        // Object styles give the separation its own pen, apart from the cut's.
        var styles = LineStyles.For(new JsonObject { ["walls"] = new JsonObject { ["separation"] = new JsonObject { ["pen"] = 1 } } }, null, null, null, 100)!;
        Assert.NotNull(styles(LineRole.Separation, "IfcWall", null));
        Assert.Null(styles(LineRole.Cut, "IfcWall", null));
        var (drawing, _, _) = Views.Generate(f.Session.Reader, f.Model.Get(plan)!);
        var pens = Pens.Of(drawing!, null, new Pens.Options(LineStyle: styles));
        var thin = LineStyles.PenWidth(LineStyles.DefaultPenTable(), 1, 100);
        Assert.Contains(pens, p => p.Layer == PenLayer.Cut && Math.Abs(p.Width - thin) < 1e-9 && p.Segments.Count == 2);
        Assert.Contains(LineRole.All, x => x == LineRole.Separation);
    }

    [Fact]
    public void Dimensions_write_feet_and_inches_or_inches_in_fractions()
    {
        Assert.Equal("12'-0\"", Standards.FormatDimension(12 * 0.3048, "ft-in", 2));
        Assert.Equal("12'-3 1/2\"", Standards.FormatDimension(147.5 * 0.0254, "ft-in", 2));
        Assert.Equal("147 1/2\"", Standards.FormatDimension(147.5 * 0.0254, "in", 2));
        Assert.Equal("3 3/16\"", Standards.FormatDimension(3.1875 * 0.0254, "ft-in", 2));
        // The precision is the fraction: 0 → quarters, 2 → sixteenths, 4 → sixty-fourths.
        Assert.Equal("3 1/4\"", Standards.FormatDimension(3.1875 * 0.0254, "in", 0));
        Assert.Equal("1/32\"", Standards.FormatDimension(0.03125 * 0.0254, "in", 4));
        Assert.Equal("0\"", Standards.FormatDimension(0.03125 * 0.0254, "in", 2));
        Assert.Equal("2500.00", Standards.FormatDimension(2.5, "mm", 2));
    }

    [Fact]
    public void The_project_sets_its_units_and_how_lines_end_and_the_drawing_carries_them()
    {
        var f = F();
        var plan = Plan(f);
        var d0 = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        Assert.Equal("round", (string)d0["lineCap"]!);
        Assert.Equal("mm", (string)d0["units"]!);
        f.Run("standards.options", new JsonObject { ["units"] = "ft-in", ["lineCap"] = "square" });
        var book = DraftingResources.Book(f.Model);
        Assert.Equal("ft-in", book.Units);
        Assert.Equal("square", book.LineCap);
        var d1 = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        Assert.Equal("square", (string)d1["lineCap"]!);
        Assert.Equal("ft-in", (string)d1["units"]!);
        Assert.Equal("ft-in", (string)f.Session.Query("standards.get", new JsonObject())!["units"]!);
        Assert.Throws<CommandException>(() => f.Run("standards.options", new JsonObject { ["units"] = "furlongs" }));
        Assert.Throws<CommandException>(() => f.Run("standards.options", new JsonObject { ["lineCap"] = "bevel" }));
        // The style book round-trips through its JSON.
        var again = Standards.Read(Standards.ToJson(book));
        Assert.Equal("square", again.LineCap);
        Assert.Equal("ft-in", again.Units);
    }

    [Fact]
    public void A_materials_hatch_can_align_to_the_band_it_fills_with_its_own_angle()
    {
        // A band of 5 × 0.25 turned 30°: the hatch starts at the start of its longest edge and runs along it.
        var (c, s) = (Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6));
        Vec2 R(double x, double y) => new(x * c - y * s, x * s + y * c);
        Vec2[] band = [R(0, 0), R(5, 0), R(5, 0.25), R(0, 0.25)];
        var aligned = MaterialHatch.Aligned([band], "LINES45", 1, null, "IfcWall", 15, HatchAlign.Layer);
        Assert.Equal(45, aligned.Angle, 6);
        Assert.Equal(band[0], aligned.Origin);
        var plain = MaterialHatch.Aligned([band], "LINES45", 1, null, "IfcWall", 15, HatchAlign.View);
        Assert.Equal(15, plain.Angle, 6);
        Assert.Null(plain.Origin);
        // Through the material: its section graphics hold the angle and the alignment; the hatch of a wall's band moves with it.
        var f = F();
        var type = f.Type("Layered", ("Brick", 0.25, true));
        f.Wall(0, 0, 5, 0, type);
        var brick = Material(f, "Brick");
        f.Run("material.update", new JsonObject { ["id"] = brick.Id, ["section"] = new JsonObject { ["hatch"] = "LINES45", ["hatchAlign"] = "layer", ["hatchAngle"] = 10 } });
        var resolved = MaterialGraphics.Resolve(f.Model.Get(brick.Id));
        Assert.Equal("layer", resolved.HatchAlign);
        Assert.Equal(10, resolved.HatchAngle);
        var listed = f.Session.Query("material.list", new JsonObject())!.AsArray().OfType<JsonObject>().Single(m => (string)m["name"]! == "Brick");
        Assert.Equal("layer", (string)listed["section"]!["hatchAlign"]!);
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = brick.Id, ["section"] = new JsonObject { ["hatchAlign"] = "diagonal" } }));
        var plan = Plan(f);
        var r = Views.Render(f.Session.Reader, f.Model.Get(plan)!);
        var hatch = r.Pens.Where(p => p.Layer == PenLayer.Hatch).SelectMany(p => p.Segments).ToList();
        Assert.NotEmpty(hatch);
        // Lines at 55° (45 along the wall, plus 10): every segment's direction.
        Assert.All(hatch, sg => { var d = sg.B - sg.A; var ang = Math.Abs(Math.Atan2(d.Y, d.X) * 180 / Math.PI); Assert.True(Math.Abs(ang - 55) < 1e-6 || Math.Abs(ang - 125) < 1e-6, ang.ToString()); });
    }

    [Fact]
    public void A_material_names_a_texture_for_its_faces_its_ends_and_its_top()
    {
        var f = F();
        var type = f.Type("Layered", ("Brick", 0.25, true));
        f.Wall(0, 0, 5, 0, type);
        var brick = Material(f, "Brick");
        f.Run("material.update", new JsonObject { ["id"] = brick.Id, ["texture"] = "brick", ["textureEnd"] = "brick-end", ["textureTop"] = "concrete" });
        var m = f.Model.Get(brick.Id)!;
        Assert.Equal("brick-end", m.Str("textureEnd"));
        Assert.Equal("concrete", m.Str("textureTop"));
        f.Run("material.update", new JsonObject { ["id"] = brick.Id, ["textureTop"] = null });
        Assert.Null(f.Model.Get(brick.Id)!.Str("textureTop"));
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = brick.Id, ["textureEnd"] = "marble" }));
    }

    [Fact]
    public void A_drafted_hatch_takes_a_pattern_scale_angle_and_origin_of_its_own()
    {
        var f = F();
        var plan = Plan(f);
        f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(new JsonArray(0, 0), new JsonArray(4, 0), new JsonArray(4, 3), new JsonArray(0, 3)), ["closed"] = true }) });
        var id = (string)f.Run("drafting.hatch", new JsonObject { ["viewId"] = plan, ["at"] = new JsonArray(2, 1.5), ["pattern"] = "HORIZONTAL" })!["id"]!;
        // The hatch's pen: the hatch width on layer 0 (the outline's pen is the layer's 0.25).
        static List<double> Hatched(JsonObject d) => d["pens"]!.AsArray().OfType<JsonObject>().Where(p => (string?)p["layer"] == "0" && Math.Abs((double)p["width"]! - Pens.Default.Hatch) < 1e-9).SelectMany(p => p["segments"]!.AsArray()).Select(v => (double)v!).ToList();
        var before = Hatched((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!);
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = id, ["hatch"] = new JsonObject { ["angle"] = 90, ["scale"] = 2, ["origin"] = new JsonArray(0.3, 0.1) } });
        var h = (JsonObject)f.Model.Get(id)!.Attrs["hatch"]!;
        Assert.Equal(90, (double)h["angle"]!);
        Assert.Equal(2, (double)h["scale"]!);
        Assert.Equal(0.3, (double)h["origin"]![0]!);
        Assert.Equal("HORIZONTAL", (string)h["pattern"]!);
        var after = Hatched((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!);
        Assert.NotEmpty(after);
        Assert.NotEqual(before, after);
        // Turned 90°: the lines now run up.
        Assert.True(Math.Abs(after[0] - after[2]) < 1e-6 && Math.Abs(after[1] - after[3]) > 0.01);
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = id, ["hatch"] = new JsonObject { ["pattern"] = "lines45" } });
        Assert.Equal("LINES45", (string)f.Model.Get(id)!.Attrs["hatch"]!["pattern"]!);
        Assert.Throws<CommandException>(() => f.Run("drafting.updateDraft", new JsonObject { ["id"] = id, ["hatch"] = new JsonObject { ["pattern"] = "nothing-like-it" } }));
    }
}
