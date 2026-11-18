using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Real-size hatches (decision 679, docs/plan-material-graphics.md stage 1): a pattern's units (paper or model) read from
/// and written to <c>.pat</c> (<c>;%TYPE=MODEL</c>, <c>;%UNITS=INCH</c>); a model pattern keeps its real size at any scale;
/// one denser on paper than the view style's <c>minHatchSpacing</c> is not drawn; a new project's facades take the real-size
/// patterns from their names while a project saved before keeps its paper ones; an element's <c>hatchOrigin</c> moves its
/// courses, which otherwise start at its base.
/// </summary>
public sealed class ModelHatchTests
{
    [Fact]
    public void Units_are_read_from_and_written_to_pat_text()
    {
        var text = """
            ;%UNITS=MM
            *COURSES, Courses
            ;%TYPE=MODEL
            0, 0,0, 0,73
            ;%TYPE=MODEL
            *TILE, Tiles before their header
            0, 0,0, 0,300
            *PAPER, Paper lines
            ;%TYPE=DRAFTING
            45, 0,0, 0,3
            *PLAIN, No mark
            0, 0,0, 0,2
            """;
        var (patterns, errors) = Pat.Parse(text);
        Assert.Empty(errors);
        Assert.Equal([HatchUnits.Model, HatchUnits.Model, HatchUnits.Paper, HatchUnits.Paper], patterns.Select(p => p.Units));
        // Round trip: the model ones keep their mark, the paper ones are written as before (no mark).
        var written = Pat.Serialize(patterns);
        Assert.Equal(2, written.Split('\n').Count(l => l == ";%TYPE=MODEL"));
        Assert.DoesNotContain("DRAFTING", written);
        Assert.Equal(patterns, Pat.Parse(written).Patterns);
        Assert.Equal("*PLAIN, No mark\n0, 0, 0, 0, 2\n", Pat.Serialize([patterns[3]]));
        // Inches turn into millimetres.
        var inch = Pat.Parse(";%UNITS=INCH\n*IN\n0, 1,0, 0,0.5, 1,-1\n").Patterns.Single();
        Assert.Equal((25.4, 12.7, 25.4), (inch.Families[0].Origin.X, inch.Families[0].DeltaY, inch.Families[0].Dashes[0]));
        // The built-in ones: the paper ones as they were, the real-size ones model.
        Assert.All(new[] { "LINES45", "BRICK", "TILES", "BOARDS", "STONE", "VERTICAL", "HORIZONTAL", "MASONRY" }, n => Assert.Equal(HatchUnits.Paper, HatchLibrary.Find(n)!.Units));
        Assert.All(new[] { "BRICK_240X63", "BRICK_240X71", "BRICK_215X65", "BLOCK_400X200", "BLOCK_600X250", "TILE_300X300", "TILE_600X600", "BOARDS_150", "STONE_600X300", "ROOF_TILES", "METAL_SEAM_500" },
            n => Assert.Equal(HatchUnits.Model, HatchLibrary.Find(n)!.Units));
        // The default brick: 63 mm and a 10 mm joint, 240 mm and a 10 mm joint.
        var brick = HatchLibrary.Find("brick_240x63")!;
        Assert.Equal(73, brick.Families[0].DeltaY);
        Assert.Equal(250, 2 * brick.Families[1].DeltaY);
    }

    [Fact]
    public void A_model_pattern_keeps_its_real_size_and_a_paper_one_its_paper_size()
    {
        var brick = HatchLibrary.Find("BRICK_240X63")!;
        var lines = HatchLibrary.Find("LINES45")!;
        // Drawing metres per pattern unit: the object's millimetres, or paper millimetres at 1:scale.
        Assert.Equal(0.001, HatchScale.Factor(brick, 20), 12);
        Assert.Equal(0.001, HatchScale.Factor(brick, 100), 12);
        Assert.Equal(0.1, HatchScale.Factor(lines, 100), 12);
        // A material's hatchUnits over the pattern's.
        Assert.Equal(0.1, HatchScale.Factor(brick, 100, 1, HatchUnits.Paper), 12);
        // On paper: 3.65 mm at 1:20, 0.73 at 1:100, 0.365 at 1:200 (under 0.7: not drawn).
        Assert.Equal(3.65, HatchScale.PaperSpacing(brick, 0.001, 20), 9);
        Assert.NotNull(HatchScale.Lay(brick, 100, 1, 0, null, 1000));
        Assert.Null(HatchScale.Lay(brick, 200, 1, 0, null, 1000));
        Assert.NotNull(HatchScale.Lay(brick, 200, 1, 0, null, 1000, minSpacing: 0.3));
        // Paper patterns are always drawn, however fine.
        Assert.NotNull(HatchScale.Lay(lines, 1000, 0.1, 0, null, 1000));
    }

    [Theory]
    [InlineData("Facing brick", "BRICK_240X63")]
    [InlineData("Cărămidă plină", "BRICK_240X63")]
    [InlineData("Zidărie BCA", "BLOCK_600X250")]
    [InlineData("Concrete block", "BLOCK_400X200")]
    [InlineData("Natural stone", "STONE_600X300")]
    [InlineData("Larch cladding", "BOARDS_150")]
    [InlineData("Zinc standing seam", "METAL_SEAM_500")]
    [InlineData("Tablă cutată", "METAL_SEAM_500")]
    [InlineData("Țiglă ceramică", "ROOF_TILES")]
    [InlineData("Slate", "ROOF_TILES")]
    [InlineData("Ceramic tiles", "TILE_300X300")]
    [InlineData("Gravel", "GRAVEL")]
    [InlineData("Lime plaster", null)]
    [InlineData("Concrete C25/30", null)]
    public void A_new_project_s_facades_take_real_size_patterns_from_their_names(string name, string? hatch)
    {
        Assert.Equal(hatch, MaterialGraphics.Resolve(new Entity("m", IfcClass.Material, name, []), "elevation").Hatch);
        if (hatch != null) Assert.NotNull(HatchLibrary.Find(hatch));
    }

    static string FacingBrickWall(Fixture f) =>
        f.Wall(0, 0, 8, 0, f.Type("Brick 40", ("Facing brick", 0.1, false), ("Concrete", 0.2, true), ("Facing brick", 0.1, false)), height: 3);

    static string Elevation(Fixture f, double scale) =>
        (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south", ["scale"] = scale })!["id"]!;

    static Views.Rendered Render(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);

    static List<(Vec2 A, Vec2 B)> Hatch(Views.Rendered r) => r.Pens.Where(p => p.Layer == SurfaceHatches.Layer).SelectMany(p => p.Segments).ToList();

    /// <summary>The heights of the horizontal strokes (the courses' joints), from the wall's base.</summary>
    static List<double> Courses(Views.Rendered r, double baseY) =>
        Hatch(r).Where(s => Math.Abs(s.A.Y - s.B.Y) < 1e-9).Select(s => Math.Round(s.A.Y - baseY, 4)).Distinct().Order().ToList();

    static double BaseOf(Fixture f, string wall, ViewFrame frame)
    {
        var (lo, hi) = f.Bounds(wall);
        return Math.Min(frame.Map(lo).Y, frame.Map(hi).Y);
    }

    static void EveryMultipleOf(List<double> levels, double step, double offset = 0) =>
        Assert.All(levels, y => Assert.Equal(0, Math.Abs(Math.IEEERemainder(y - offset, step)), 4));

    [Fact]
    public void A_new_project_s_facade_draws_brick_at_its_real_size_at_1_20_and_1_100_from_the_wall_s_base()
    {
        var f = new Fixture();
        var wall = FacingBrickWall(f);
        var brick = f.Model.OfClass(IfcClass.Material).First(e => e.Name == "Facing brick");
        Assert.Equal("BRICK_240X63", MaterialGraphics.Resolve(brick, "elevation").Hatch);
        foreach (var scale in new[] { 20.0, 100.0 })
        {
            var r = Render(f, Elevation(f, scale));
            var levels = Courses(r, BaseOf(f, wall, r.Frame!));
            // The courses 73 mm apart in the model (63 + a 10 mm joint) at either scale, the first on the wall's base.
            Assert.True(levels.Count >= 30, $"1:{scale}: {levels.Count} courses");
            EveryMultipleOf(levels, 0.073);
            Assert.Equal(0.073, levels[1] - levels[0], 4);
        }
    }

    [Fact]
    public void A_real_size_hatch_too_dense_on_paper_is_not_drawn_unless_the_view_s_threshold_lets_it()
    {
        var f = new Fixture();
        FacingBrickWall(f);
        var view = Elevation(f, 200);
        // At 1:200 the courses lie 0.365 mm apart on paper: under the default 0.7, none drawn.
        Assert.Empty(Hatch(Render(f, view)));
        f.Run("view.setStyle", new JsonObject { ["id"] = view, ["minHatchSpacing"] = 0.3 });
        Assert.NotEmpty(Hatch(Render(f, view)));
        // The style's own threshold, the view's taken back.
        f.Run("view.setStyle", new JsonObject { ["id"] = view, ["minHatchSpacing"] = null });
        Assert.Empty(Hatch(Render(f, view)));
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["minHatchSpacing"] = 0 });
        Assert.NotEmpty(Hatch(Render(f, view)));
        Assert.Throws<CommandException>(() => f.Run("view.setStyle", new JsonObject { ["id"] = view, ["minHatchSpacing"] = -1 }));
    }

    [Fact]
    public void A_project_saved_before_keeps_its_paper_facades_and_a_new_one_has_the_new_mark()
    {
        var f = new Fixture();
        var wall = FacingBrickWall(f);
        var view = Elevation(f, 100);
        Assert.Equal(MaterialHatchUpgrade.Rev, (int)Spatial.Project(f.Model)!.Attrs[MaterialHatchUpgrade.RevKey]!);
        Assert.False(MaterialHatchUpgrade.PaperSurfaces(f.Model));
        // The same project as a build of hatchRev 2 saved it, opened again.
        var saved = f.Model.Entities.Select(e =>
        {
            if (e.Class != IfcClass.Project) return e;
            var a = e.CloneAttrs();
            a[MaterialHatchUpgrade.RevKey] = 2;
            return e.With(attrs: a);
        }).ToList();
        f.Session.Load(saved, f.Model.Relations.ToList(), null);
        Assert.True(MaterialHatchUpgrade.PaperSurfaces(f.Model));
        Assert.Equal(MaterialHatchUpgrade.Rev, (int)Spatial.Project(f.Model)!.Attrs[MaterialHatchUpgrade.RevKey]!);
        // Its materials untouched; its facade the paper BRICK, courses 0.4 m apart at 1:100 from the view's origin, as before.
        var brick = f.Model.OfClass(IfcClass.Material).First(e => e.Name == "Facing brick");
        Assert.Null(brick.Attrs["elevation"]);
        Assert.Equal("BRICK", MaterialGraphics.Resolve(brick, "elevation", paperSurfaces: true).Hatch);
        var r = Render(f, view);
        var levels = Courses(r, 0);
        Assert.True(levels.Count >= 5);
        EveryMultipleOf(levels, 0.4);
        Assert.Contains((string?)f.Session.Query("material.list", [])!.AsArray().Single(m => (string?)m!["name"] == "Facing brick")!["elevation"]!["hatch"], new[] { "BRICK" });
        _ = wall;
    }

    [Fact]
    public void An_element_s_hatch_origin_moves_its_courses_and_is_one_undo_step()
    {
        var f = new Fixture();
        var wall = FacingBrickWall(f);
        var view = Elevation(f, 50);
        var r = Render(f, view);
        var baseY = BaseOf(f, wall, r.Frame!);
        EveryMultipleOf(Courses(r, baseY), 0.073);
        f.Run("element.setHatchOrigin", new JsonObject { ["id"] = wall, ["origin"] = new JsonArray(0, 0.03) });
        Assert.Equal([0, 0.03], f.Model.Get(wall)!.Attrs[HatchOrigins.Key]!.AsArray().Select(n => (double)n!));
        var moved = Courses(Render(f, view), baseY);
        Assert.NotEmpty(moved);
        EveryMultipleOf(moved, 0.073, 0.03);
        // Undone in one step: from the base again.
        f.Session.Undo();
        Assert.Null(f.Model.Get(wall)!.Attrs[HatchOrigins.Key]);
        EveryMultipleOf(Courses(Render(f, view), baseY), 0.073);
        // Cleared with null.
        f.Run("element.setHatchOrigin", new JsonObject { ["id"] = wall, ["origin"] = new JsonArray(0.1, 0.02) });
        f.Run("element.setHatchOrigin", new JsonObject { ["id"] = wall, ["origin"] = null });
        Assert.Null(f.Model.Get(wall)!.Attrs[HatchOrigins.Key]);
        Assert.Throws<CommandException>(() => f.Run("element.setHatchOrigin", new JsonObject { ["id"] = wall, ["origin"] = "here" }));
    }

    [Fact]
    public void A_material_may_lay_a_pattern_in_other_units_and_the_protocol_names_them()
    {
        var f = new Fixture();
        FacingBrickWall(f);
        var brick = f.Model.OfClass(IfcClass.Material).First(e => e.Name == "Facing brick").Id;
        f.Run("material.update", new JsonObject { ["id"] = brick, ["elevation"] = new JsonObject { ["hatch"] = "BRICK_240X63", ["hatchUnits"] = "paper", ["hatchScale"] = 0.01 } });
        var g = MaterialGraphics.Resolve(f.Model.Get(brick), "elevation");
        Assert.Equal(HatchUnits.Paper, g.HatchUnits);
        Assert.Equal("paper", (string?)g.ToJson()["hatchUnits"]);
        // As paper at 1:100 with a scale of 0.01: 0.73 mm × 100 × 0.01 = 73 mm on the wall again, now from the view's origin.
        var r = Render(f, Elevation(f, 100));
        EveryMultipleOf(Courses(r, 0), 0.073);
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = brick, ["elevation"] = new JsonObject { ["hatchUnits"] = "metres" } }));
        var list = f.Session.Query("hatch.list", [])!.AsArray();
        Assert.Equal("model", (string?)list.Single(p => (string?)p!["name"] == "TILE_300X300")!["units"]);
        Assert.Equal("paper", (string?)list.Single(p => (string?)p!["name"] == "LINES45")!["units"]);
        var preview = f.Session.Query("hatch.preview", new JsonObject { ["pattern"] = "TILE_300X300", ["size"] = 30 })!;
        Assert.Equal(("model", 20.0), ((string?)preview["units"], (double)preview["viewScale"]!));
        // 300 mm at 1:20: lines every 15 paper mm over a 30 mm square.
        Assert.Contains(preview["segments"]!.AsArray().Select(n => (double)n!), v => Math.Abs(v - 15) < 1e-6);
    }
}
