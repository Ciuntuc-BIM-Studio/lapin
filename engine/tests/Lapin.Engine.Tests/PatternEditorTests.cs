using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage 7 of docs/plan-material-graphics.md: hatch patterns made by drawing. Line-family patterns from their numbers
/// (hatch.create, hatch.update, hatch.delete, built-ins fixed, a rename followed everywhere, a pattern in use deleted only by
/// force), an inline pattern previewed, a drawn cell saved as a pattern (drafting.toPattern): line families when they say it
/// exactly, else a tile (Î12) drawn clipped, carried to DXF as lines, to IFC as the nearest hatching and to Bonsai as an SVG tile.
/// </summary>
public class PatternEditorTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f, double scale = 100) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = scale })!["id"]!;
    static string Mat(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;

    static JsonObject Family(double angle, double dy, params double[] dashes) => new()
    {
        ["angle"] = angle, ["origin"] = J(0, 0), ["deltaX"] = 0, ["deltaY"] = dy, ["dashes"] = new JsonArray(dashes.Select(d => (JsonNode)d).ToArray()),
    };

    static List<string> Draw(Fixture f, string view, params JsonObject[] shapes) =>
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(shapes) })!["ids"]!.AsArray().Select(x => (string)x!).ToList();

    static JsonObject Line(double x0, double y0, double x1, double y1) => new() { ["type"] = "line", ["a"] = J(x0, y0), ["b"] = J(x1, y1) };

    static double Length(IEnumerable<HatchSegment> s) => s.Sum(x => (x.B - x.A).Length);

    static readonly IReadOnlyList<IReadOnlyList<Vec2>> Box = [[new(0.013, 0.021), new(1.71, 0.021), new(1.71, 1.33), new(0.013, 1.33)]];

    [Fact]
    public void A_pattern_made_from_its_numbers_is_created_changed_and_undone()
    {
        var f = F();
        var made = f.Run("hatch.create", new JsonObject { ["name"] = "my_lines", ["description"] = "Mine", ["units"] = "model", ["families"] = new JsonArray(Family(30, 50, 100, -25)) })!;
        Assert.Equal("MY_LINES", (string?)made["name"]);
        var p = DraftingResources.Patterns(f.Model).Single();
        Assert.Equal((HatchUnits.Model, 30.0, 50.0), (p.Units, p.Families[0].AngleDeg, p.Families[0].DeltaY));
        Assert.Equal([100.0, -25.0], p.Families[0].Dashes);
        var get = f.Session.Query("hatch.get", new JsonObject { ["name"] = "my_lines" })!;
        Assert.Equal(("lines", true, "model", 0), ((string?)get["kind"], (bool)get["project"]!, (string?)get["units"], (int)get["uses"]!));
        Assert.Contains(f.Session.Query("hatch.list", new JsonObject())!.AsArray(), x => (string?)x!["name"] == "MY_LINES" && (string?)x["kind"] == "lines");
        // Built-ins are fixed: no new pattern takes their name, none is changed or deleted.
        Assert.Throws<CommandException>(() => f.Run("hatch.create", new JsonObject { ["name"] = "CROSS", ["families"] = new JsonArray(Family(0, 2)) }));
        Assert.Throws<CommandException>(() => f.Run("hatch.update", new JsonObject { ["name"] = "CROSS", ["description"] = "x" }));
        Assert.Throws<CommandException>(() => f.Run("hatch.delete", new JsonObject { ["name"] = "CROSS" }));
        Assert.Throws<CommandException>(() => f.Run("hatch.create", new JsonObject { ["name"] = "MY_LINES", ["families"] = new JsonArray(Family(0, 2)) }));
        Assert.Throws<CommandException>(() => f.Run("hatch.create", new JsonObject { ["name"] = "two words", ["families"] = new JsonArray(Family(0, 2)) }));
        Assert.Throws<CommandException>(() => f.Run("hatch.create", new JsonObject { ["name"] = "FLAT", ["families"] = new JsonArray(Family(0, 0)) }));
        Assert.Throws<CommandException>(() => f.Run("hatch.create", new JsonObject { ["name"] = "NONE_GIVEN" }));
        // Changed: its families, its units kept.
        f.Run("hatch.update", new JsonObject { ["name"] = "MY_LINES", ["families"] = new JsonArray(Family(0, 20), Family(90, 20)) });
        p = DraftingResources.Patterns(f.Model).Single();
        Assert.Equal((2, HatchUnits.Model, "Mine"), (p.Families.Count, p.Units, p.Description));
        // Each command one undo step.
        f.Session.Undo();
        Assert.Single(DraftingResources.Patterns(f.Model).Single().Families);
        f.Session.Undo();
        Assert.Empty(DraftingResources.Patterns(f.Model));
    }

    [Fact]
    public void A_rename_follows_the_pattern_everywhere_and_a_delete_in_use_needs_force()
    {
        var f = F();
        var plan = Plan(f);
        f.Wall(0, 0, 5, 0, f.Type("Concrete 20", ("Concrete", 0.2, true)));
        f.Run("hatch.create", new JsonObject { ["name"] = "MINE", ["families"] = new JsonArray(Family(45, 2)) });
        var concrete = Mat(f, "Concrete");
        f.Run("material.update", new JsonObject { ["id"] = concrete, ["section"] = new JsonObject { ["hatch"] = "MINE", ["backgroundHatch"] = new JsonObject { ["hatch"] = "MINE" } } });
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["category"] = "walls", ["sub"] = "core", ["value"] = new JsonObject { ["cutHatch"] = "MINE" } });
        Draw(f, plan, new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 2), J(2, 2), J(2, 4), J(0, 4)), ["closed"] = true });
        var hatch = (string)f.Run("drafting.hatch", new JsonObject { ["viewId"] = plan, ["at"] = J(1, 3), ["pattern"] = "MINE" })!["id"]!;
        Assert.Equal(4, (int)f.Session.Query("hatch.usage", new JsonObject { ["name"] = "mine" })!["count"]!);

        var r = f.Run("hatch.update", new JsonObject { ["name"] = "MINE", ["newName"] = "OURS" })!;
        Assert.Equal(("OURS", 4), ((string?)r["name"], (int)r["repointed"]!));
        var section = f.Model.Get(concrete)!.Attrs["section"]!;
        Assert.Equal(("OURS", "OURS"), ((string?)section["hatch"], (string?)section["backgroundHatch"]!["hatch"]));
        Assert.Equal("OURS", (string?)f.Session.Query("view.visibility", new JsonObject { ["id"] = plan })!["graphics"]!["categories"]!["walls"]!["sub"]!["core"]!["cutHatch"]);
        Assert.Equal("OURS", (string?)f.Model.Get(hatch)!.Attrs["hatch"]!["pattern"]);
        Assert.Throws<CommandException>(() => f.Run("hatch.update", new JsonObject { ["name"] = "OURS", ["newName"] = "CROSS" }));

        // In use: refused, unless forced; then the material has no hatch, the row none, the drafted hatch the 45° lines.
        Assert.Throws<CommandException>(() => f.Run("hatch.delete", new JsonObject { ["name"] = "OURS" }));
        Assert.Equal(4, (int)f.Run("hatch.delete", new JsonObject { ["name"] = "OURS", ["force"] = true })!["repointed"]!);
        Assert.Empty(DraftingResources.Patterns(f.Model));
        Assert.Null(MaterialGraphics.Resolve(f.Model.Get(concrete), "section").Hatch);
        Assert.Equal("none", (string?)f.Session.Query("view.visibility", new JsonObject { ["id"] = plan })!["graphics"]!["categories"]!["walls"]!["sub"]!["core"]!["cutHatch"]);
        Assert.Equal(HatchPatternCommands.DraftedFallback, (string?)f.Model.Get(hatch)!.Attrs["hatch"]!["pattern"]);
        // One undo step brings it all back.
        f.Session.Undo();
        Assert.Equal("OURS", (string?)f.Model.Get(concrete)!.Attrs["section"]!["hatch"]);
        Assert.Single(DraftingResources.Patterns(f.Model));
    }

    [Fact]
    public void A_pattern_is_read_renamed_and_deleted_beside_switches_of_the_same_name()
    {
        // Forum 275: a view style keeps surfaceHatch as a switch (true/false), not a pattern's name. Reading what names a pattern
        // must pass over it, or hatch.get fails for every pattern and the editor never opens (nor its Delete).
        var f = F();
        var plan = Plan(f);
        var style = (string)f.Run("viewStyle.create", new JsonObject { ["name"] = "Mine", ["surfaceHatch"] = true })!["id"]!;
        Assert.Contains("\"surfaceHatch\":true", Spatial.Project(f.Model)!.Attrs.ToJsonString());
        var cross = f.Session.Query("hatch.get", new JsonObject { ["name"] = "CROSS" })!;
        Assert.Equal(("lines", false, 0), ((string?)cross["kind"], (bool)cross["project"]!, (int)cross["uses"]!));

        f.Wall(0, 0, 5, 0, f.Type("Concrete 20", ("Concrete", 0.2, true)));
        f.Run("hatch.create", new JsonObject { ["name"] = "MINE", ["families"] = new JsonArray(Family(45, 2)) });
        var concrete = Mat(f, "Concrete");
        f.Run("material.update", new JsonObject { ["id"] = concrete, ["section"] = new JsonObject { ["hatch"] = "MINE" } });
        Draw(f, plan, new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 2), J(2, 2), J(2, 4), J(0, 4)), ["closed"] = true });
        var hatch = (string)f.Run("drafting.hatch", new JsonObject { ["viewId"] = plan, ["at"] = J(1, 3), ["pattern"] = "MINE" })!["id"]!;
        Assert.Equal(2, (int)f.Session.Query("hatch.get", new JsonObject { ["name"] = "MINE" })!["uses"]!);

        // Edited: its spacing and angle; renamed, followed everywhere; the switch left as it was.
        f.Run("hatch.update", new JsonObject { ["name"] = "MINE", ["newName"] = "MINE-2", ["families"] = new JsonArray(Family(30, 4)) });
        var p = DraftingResources.Patterns(f.Model).Single();
        Assert.Equal(("MINE-2", 30.0, 4.0), (p.Name, p.Families[0].AngleDeg, p.Families[0].DeltaY));
        Assert.Equal("MINE-2", (string?)f.Model.Get(hatch)!.Attrs["hatch"]!["pattern"]);
        Assert.True(ViewStyles.All(f.Model).Single(s => s.Id == style).SurfaceHatch);

        // Deleted in use (forced, as the dialog asks): what named it falls back, the same entities (their GlobalIds) kept; one undo step.
        var before = f.Model.Entities.Select(e => e.Id).ToHashSet();
        Assert.Equal(2, (int)f.Run("hatch.delete", new JsonObject { ["name"] = "MINE-2", ["force"] = true })!["repointed"]!);
        Assert.Empty(DraftingResources.Patterns(f.Model));
        Assert.True(before.SetEquals(f.Model.Entities.Select(e => e.Id)));
        Assert.Null(MaterialGraphics.Resolve(f.Model.Get(concrete), "section").Hatch);
        Assert.Equal(HatchPatternCommands.DraftedFallback, (string?)f.Model.Get(hatch)!.Attrs["hatch"]!["pattern"]);
        Assert.True(ViewStyles.All(f.Model).Single(s => s.Id == style).SurfaceHatch);
        f.Session.Undo();
        Assert.Equal("MINE-2", DraftingResources.Patterns(f.Model).Single().Name);
        Assert.Equal("MINE-2", (string?)f.Model.Get(hatch)!.Attrs["hatch"]!["pattern"]);
    }

    [Fact]
    public void The_editor_previews_a_pattern_not_saved()
    {
        var f = F();
        var lines = f.Session.Query("hatch.preview", new JsonObject { ["pattern"] = new JsonObject { ["families"] = new JsonArray(Family(0, 5)) }, ["size"] = 20 })!;
        // Horizontal lines 5 mm apart over a 20 mm square: y = 0, 5, 10, 15 (the top edge is outside, half-open).
        Assert.Equal(16, lines["segments"]!.AsArray().Count);
        var tile = f.Session.Query("hatch.preview", new JsonObject
        {
            ["pattern"] = new JsonObject { ["tile"] = new JsonObject { ["width"] = 10, ["height"] = 10, ["arcs"] = new JsonArray(new JsonObject { ["c"] = J(5, 5), ["r"] = 3, ["sweep"] = 360 }) } },
            ["size"] = 20,
        })!;
        var segs = tile["segments"]!.AsArray().Select(x => (double)x!).ToList();
        Assert.True(segs.Count > 4 * 4 * 24);
        Assert.Empty(DraftingResources.Patterns(f.Model));
        Assert.Throws<CommandException>(() => f.Session.Query("hatch.preview", new JsonObject { ["pattern"] = new JsonObject { ["families"] = new JsonArray() } }));
    }

    [Fact]
    public void Straight_pieces_on_the_cell_s_grid_become_line_families_drawing_what_the_tile_would()
    {
        // A running bond: the bed joint, and the head joints of two courses half a brick apart; a diagonal; a dot.
        (double W, double H) cell = (250, 140);
        List<TileLine> pieces =
        [
            new(new(0, 0), new(250, 0)), new(new(0, 0), new(0, 70)), new(new(125, 70), new(125, 140)), new(new(0, 70), new(250, 70)),
            // The cell's top edge is the next one's bottom: the same family.
            new(new(0, 140), new(250, 140)),
            new(new(0, 0), new(250, 140)), new(new(60, 30), new(60, 30)),
        ];
        var families = TileFit.Families(cell.W, cell.H, pieces);
        Assert.NotNull(families);
        Assert.Equal(6, families!.Count);
        var asLines = new HatchPattern("RB", "", families, HatchUnits.Model);
        var asTile = new HatchPattern("RB", "", [], HatchUnits.Model) { Tile = new HatchTile(cell.W, cell.H, pieces.Where((_, i) => i != 4).ToList(), []) };
        var o = new HatchOptions(0.001, 0, new Vec2(0.0, 0.0));
        var a = HatchFill.Segments(Box, asLines, o).Segments;
        var b = HatchFill.Segments(Box, asTile, o).Segments;
        Assert.Equal(Length(b), Length(a), 6);
        Assert.Equal(b.Count(s => s.A == s.B), a.Count(s => s.A == s.B));
        // Turned, the same.
        var turned = o with { AngleDeg = 17 };
        Assert.Equal(Length(HatchFill.Segments(Box, asTile, turned).Segments), Length(HatchFill.Segments(Box, asLines, turned).Segments), 6);
        // A piece at an angle no small grid step takes, and an arc, fit none.
        Assert.Null(TileFit.Families(cell.W, cell.H, [new(new(0, 0), new(250, 13.7))]));
        Assert.NotNull(TileFit.Make("X", "", HatchUnits.Paper, 10, 10, [new(new(0, 0), new(10, 0))], [new(new(5, 5), 2, 0, 360)]).Tile);
    }

    [Fact]
    public void Drawn_linework_saved_as_a_pattern_is_line_families_when_it_can_be()
    {
        var f = F();
        var plan = Plan(f, 100);
        // A 10 × 5 mm cell on paper at 1:100 is 1 × 0.5 m of the view: a bed joint and a head joint.
        var ids = Draw(f, plan, Line(2, 2, 3, 2), Line(2, 2, 2, 2.5));
        var r = f.Run("drafting.toPattern", new JsonObject
        {
            ["viewId"] = plan, ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["name"] = "Bond", ["units"] = "paper",
            ["cell"] = new JsonObject { ["origin"] = J(2, 2), ["width"] = 1, ["height"] = 0.5 },
        })!;
        Assert.Equal(("BOND", "lines", 2), ((string?)r["name"], (string?)r["kind"], (int)r["families"]!));
        var p = DraftingResources.Patterns(f.Model).Single();
        Assert.Equal(new HatchLineFamily(0, new Vec2(0, 0), 0, 5, []), p.Families[0]);
        // The head joint: vertical, continuous (as long as its period, the cell's height), its lines a cell's width apart.
        Assert.Equal(new HatchLineFamily(90, new Vec2(0, 0), 0, 10, []), p.Families[1]);
        // The drafting stays; the pattern is the project's, written to .pat as line families.
        Assert.All(ids, id => Assert.NotNull(f.Model.Get(id)));
        Assert.DoesNotContain("LAPIN-TILE", f.Model.Get(Spatial.Project(f.Model)!.Id)!.Str("hatchPatterns"));
        Assert.Throws<CommandException>(() => f.Run("drafting.toPattern", new JsonObject { ["viewId"] = plan, ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["name"] = "BOND" }));
    }

    static (Fixture F, string Plan) TileOnWall()
    {
        var f = F();
        var plan = Plan(f, 50);
        f.Wall(0, 0, 6, 0, f.Type("Block 30", ("Concrete block", 0.3, true)));
        // A cell 100 × 100 mm of the object: a circle and a line across it, a piece poking out of the cell clipped.
        var ids = Draw(f, plan,
            new JsonObject { ["type"] = "circle", ["c"] = J(10.05, 10.05), ["r"] = 0.03 },
            Line(10, 10, 10.15, 10.1));
        var r = f.Run("drafting.toPattern", new JsonObject
        {
            ["viewId"] = plan, ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["name"] = "RINGS", ["units"] = "model", ["description"] = "Rings",
            ["cell"] = new JsonObject { ["origin"] = J(10, 10), ["width"] = 0.1, ["height"] = 0.1 },
        })!;
        Assert.Equal("tile", (string?)r["kind"]);
        f.Run("material.update", new JsonObject { ["id"] = Mat(f, "Concrete block"), ["section"] = new JsonObject { ["hatch"] = "RINGS" } });
        return (f, plan);
    }

    [Fact]
    public void A_tile_with_arcs_is_kept_drawn_clipped_and_carried_in_the_pattern_text()
    {
        var (f, plan) = TileOnWall();
        var p = DraftingResources.Patterns(f.Model).Single();
        var tile = p.Tile!;
        Assert.Equal((100.0, 100.0, HatchUnits.Model), (tile.Width, tile.Height, p.Units));
        var arc = Assert.Single(tile.Arcs);
        Assert.Equal((50.0, 50.0, 30.0, 360.0), (Math.Round(arc.C.X, 6), Math.Round(arc.C.Y, 6), Math.Round(arc.R, 6), arc.Sweep));
        // The line poking out of the cell is cut at its edge.
        var line = Assert.Single(tile.Lines);
        Assert.Equal(100, line.B.X, 6);
        // Through the project's pattern text and back, the same.
        Assert.Equal(p, Pat.Parse(Pat.Serialize([p])).Patterns.Single());
        Assert.Equal("tile", (string?)f.Session.Query("hatch.get", new JsonObject { ["name"] = "RINGS" })!["kind"]);
        // A .pat for another program leaves it out and says so.
        var pat = f.Session.Query("hatch.exportPat", new JsonObject())!;
        Assert.Equal(["RINGS"], pat["skipped"]!.AsArray().Select(x => (string)x!));
        Assert.DoesNotContain("RINGS", (string?)pat["text"] ?? "");
        // Drawn in the plan, every stroke inside the wall's cut.
        var rendered = Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!);
        var strokes = rendered.Pens.Where(x => x.Layer == PenLayer.Hatch).SelectMany(x => x.Segments).ToList();
        Assert.True(strokes.Count > 60 * 3 * 10);
        Assert.All(strokes, s => Assert.True(s.A.X >= -1e-6 && s.A.X <= 6 + 1e-6 && Math.Abs(s.A.Y) <= 0.15 + 1e-6 && Math.Abs(s.B.Y) <= 0.15 + 1e-6));
        // The materials' standards file carries it.
        var file = StandardsLibrary.Export(f.Model, "Mine");
        Assert.Contains("LAPIN-ARC", file.ToJsonString());
    }

    [Fact]
    public void A_tile_goes_to_dxf_as_lines_to_ifc_as_the_nearest_hatching_and_to_bonsai_as_an_svg_tile()
    {
        var (f, plan) = TileOnWall();
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = plan, ["x"] = 150, ["y"] = 140 });
        var dxf = Encoding.Latin1.GetString(Sheets.Dxf(f.Session.Reader, f.Model.Get(s)!));
        Assert.True(dxf.Split("\nLINE\n").Length > 1000);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "tile.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains(ifc.Split('\n'), l => l.Contains("IFCCURVESTYLE('RINGS'"));
        Assert.Single(ifc.Split('\n'), l => l.Contains("IFCFILLAREASTYLEHATCHING("));
        var svg = BonsaiStyles.Of(f.Model).Patterns;
        Assert.Contains("<circle cx=\"50\" cy=\"50\" r=\"30\"/>", svg);
        Assert.Contains("width=\"100\" height=\"100\"", svg);
    }
}
