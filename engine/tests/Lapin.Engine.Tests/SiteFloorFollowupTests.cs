using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The site and floor follow-ups of 23.11.2026: a channel called a drainage channel wherever it is shown; floors' slope marks at
/// 1:50 and larger (a standards setting changes it) and clear of rooms' stamps; an edge slope's limit acting near its own edge
/// only; T6's leftovers (IL and low point in the drawing's language, a standalone kerb cutting a hardscape, a hardscape's layer
/// quantities less a channel). The triangulation's goldens are <see cref="TriangulationGoldenTests"/>.
/// </summary>
public sealed class SiteFloorFollowupTests
{
    static Fixture F() => new(new ArchitectureModule(), new CivilModule(), new StructureModule());
    static JsonArray P(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);
    static JsonArray Pts(params double[][] pts) => new([.. pts.Select(p => (JsonNode)P(p))]);
    static JsonArray Rect(double x0, double y0, double x1, double y1) => Pts([x0, y0], [x1, y0], [x1, y1], [x0, y1]);

    static string Field(Fixture f, double z = 1.0)
    {
        var pts = new JsonArray();
        for (var i = 0; i <= 20; i++) for (var j = 0; j <= 20; j++) pts.Add(P(i * 2.0, j * 2.0, z));
        return (string)f.Run("civil.addTerrain", new JsonObject { ["storeyId"] = f.Storey, ["points"] = pts, ["datum"] = 0, ["interval"] = 0.5, ["depth"] = 2 })!["id"]!;
    }

    static string Type(Fixture f) => (string)f.Run("architecture.createSlabType", new JsonObject
    {
        ["name"] = "Tiled floor",
        ["layers"] = new JsonArray(
            new JsonObject { ["material"] = "Ceramic tiles", ["thickness"] = 0.01 },
            new JsonObject { ["material"] = "Screed", ["thickness"] = 0.08 },
            new JsonObject { ["material"] = "Concrete", ["thickness"] = 0.2, ["core"] = true }),
    })!["id"]!;

    static string Floor(Fixture f, JsonArray pts) =>
        (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = pts, ["typeId"] = Type(f) })!["id"]!;

    static double TopAt(Fixture f, string id, double x, double y)
    {
        var b = FloorSlopes.Solve(f.Model, f.Model.Get(id)!, f.Session.Catalog)!;
        foreach (var p in b.Pieces)
            if (p.Piece.At(new Vec2(x, y)) is { } h) return h;
        throw new InvalidOperationException("Not on the floor.");
    }

    static double Vol(Fixture f, string id, string? key = null) => f.Session.Geometry(id)!.Parts.Where(p => key == null || p.Key == key).Sum(p => Measure.Volume(p.Solid));

    static double Q(Fixture f, string id, string set, string name, string? part = null) =>
        f.Session.Quantities(id).First(s => s.Name == set && s.Part == part).Quantities.First(q => q.Name == name).Value;

    sealed class Ctx(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static List<PenText> Texts(Fixture f, string view) => [.. Views.Render(new Ctx(f), Views.Find(f.Model, view)!).Pens.SelectMany(p => p.Texts)];

    // ─── 1. The drainage channel ─────────────────────────────────────────

    [Fact]
    public void A_channel_is_called_a_drainage_channel_in_its_class_schedule_name_and_IFC()
    {
        var f = F();
        Field(f);
        var id = (string)f.Run("civil.addChannel", new JsonObject { ["points"] = Pts([5, 5], [15, 5]), ["kind"] = "u" })!["id"]!;
        var e = f.Model.Get(id)!;
        // The class id stays; what is shown is "Drainage channel", never "Channel" (the steel U profile in the dictionaries).
        Assert.Equal("LapinChannel", e.Class);
        Assert.Equal("Drainage channel", f.Session.Catalog.Class(Channels.Class)!.Label);
        Assert.Equal("Drainage channel 1", e.Name);
        Assert.Equal("Drainage channel", f.Session.Geometry(id)!.Parts.Single(p => p.Key == "body").Name);
        var schedule = f.Session.Catalog.All<IScheduleSource>().Single(s => s.Kind == "channel");
        Assert.Equal("Drainage channel schedule", schedule.Label);
        Assert.Equal("Drainage channel schedule", schedule.Grid(new ModelQuery(f.Model)).Title);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "site.ifc", new IfcExportOptions(Schema: "IFC4X3"));
        Assert.Contains("'Drainage channel 1'", System.Text.Encoding.UTF8.GetString(ms.ToArray()));
        // One named by its user keeps its name.
        var named = (string)f.Run("civil.addChannel", new JsonObject { ["points"] = Pts([5, 8], [15, 8]), ["name"] = "Rigola R1" })!["id"]!;
        Assert.Equal("Rigola R1", f.Model.Get(named)!.Name);
    }

    // ─── 2. Floors' slope marks by scale, clear of room stamps ──────────

    [Fact]
    public void Slope_marks_show_at_1_50_and_larger_and_the_standards_change_the_threshold()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 2, 3));
        f.Run("architecture.addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["at"] = P(1, 1.5), ["depth"] = 0.02 });
        var level = Standards.FormatLevel(Slabs.Top(f.Model, f.Model.Get(id)!) - 0.02);
        string View(double scale) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = scale })!["id"]!;
        var (at20, at50, at100) = (View(20), View(50), View(100));
        bool Marked(string v) => Texts(f, v).Any(t => t.Text.EndsWith(" %")) && Texts(f, v).Any(t => t.Text == level);
        Assert.True(Marked(at20));
        Assert.True(Marked(at50));
        // At 1:100 neither the arrows nor the levels; the floor and its drain are still drawn.
        Assert.DoesNotContain(Texts(f, at100), t => t.Text.EndsWith(" %") || t.Text == level);
        Assert.NotEmpty(Views.Render(new Ctx(f), Views.Find(f.Model, at100)!).Pens.Where(p => p.Layer == FloorSlopes.Layer).SelectMany(p => p.Segments));
        // The standards set to 1:100: shown there too; back to the default with null; at 1:20 only: not at 1:50.
        f.Run("standards.options", new JsonObject { ["floorSlopeMarks"] = 100 });
        Assert.Equal(100, DraftingResources.Book(f.Model).FloorSlopeMarks);
        Assert.True(Marked(at100));
        f.Run("standards.options", new JsonObject { ["floorSlopeMarks"] = 20 });
        Assert.False(Marked(at50));
        Assert.True(Marked(at20));
        f.Run("standards.options", new JsonObject { ["floorSlopeMarks"] = null });
        Assert.Null(DraftingResources.Book(f.Model).FloorSlopeMarks);
        Assert.True(Marked(at50));
        Assert.False(Marked(at100));
        Assert.Throws<CommandException>(() => f.Run("standards.options", new JsonObject { ["floorSlopeMarks"] = 0 }));
    }

    [Fact]
    public void A_fall_arrow_keeps_clear_of_the_room_stamp()
    {
        var f = F();
        // A bathroom falling to a drain at its middle, its stamp at the middle too: the arrows would run over it.
        var id = Floor(f, Rect(0, 0, 2.4, 1.8));
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 2.4, ["y2"] = 1.8, ["name"] = "Bath", ["function"] = "Bath" })!["id"]!;
        f.Run("architecture.addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 0, ["grade"] = "2%", ["fall"] = "out" });
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        var ctx = new ViewContext(f.Model, Views.Find(f.Model, view)!, "plan", default!, 50, DraftingResources.Book(f.Model), default!, f.Session.Geometry) { Services = f.Session.Catalog };
        var stamps = Rooms.StampBoxes(ctx);
        Assert.Single(stamps);
        var (min, max) = stamps[0];
        // Its arrow: the floor's mark's lines on LEVELS; none of them crosses the stamp's box.
        var marks = FloorSlopes.Marks(ctx).Where(m => m.Lines.Any(l => l.Layer == FloorSlopes.Layer)).ToList();
        var lines = marks.SelectMany(m => m.Lines).Where(l => l.Layer == FloorSlopes.Layer).ToList();
        Assert.NotEmpty(lines);
        static bool In(Vec2 p, Vec2 a, Vec2 b) => p.X > a.X && p.X < b.X && p.Y > a.Y && p.Y < b.Y;
        Assert.DoesNotContain(lines, l => Enumerable.Range(0, 11).Any(k => In(l.A + (l.B - l.A) * (k / 10.0), min, max)));
        // The grade still written, beside the moved arrow.
        Assert.Contains(marks.SelectMany(m => m.Texts), t => t.Text.Text == "2.0 %");
        Assert.NotNull(room);
    }

    // ─── 3. An edge slope's limit near its own edge ─────────────────────

    [Fact]
    public void A_terrace_with_two_gutters_keeps_the_limit_of_one_near_its_own_edge()
    {
        var f = F();
        // 6 × 4 m: a gutter along the south edge, rising 2 % from it until +20 mm; a gutter along the east edge, rising 1.5 %, no limit.
        var id = Floor(f, Rect(0, 0, 6, 4));
        f.Run("architecture.setFloorSlope", new JsonObject { ["id"] = id, ["how"] = "bend" });
        f.Run("architecture.addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 0, ["grade"] = "2%", ["fall"] = "out", ["until"] = new JsonObject { ["offset"] = 0.02 } });
        f.Run("architecture.addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 1, ["grade"] = "1.5%", ["fall"] = "out" });
        // Near the south gutter its slope, then its level beyond the limit.
        Assert.Equal(0.01, TopAt(f, id, 2, 0.5), 9);
        Assert.Equal(0.02, TopAt(f, id, 1, 1.5), 9);
        // The east gutter's slope is not capped by the south's limit: where it holds (nearer its gutter than the ridge) it rises on, past +20 mm.
        Assert.Equal(0.03, TopAt(f, id, 4, 3), 9);
        Assert.Equal(0.015, TopAt(f, id, 5, 3.5), 9);
        // The ridge where the two planes meet stays where it was: 2 % · y = 1.5 % · (6 − x).
        Assert.Equal(0.045, TopAt(f, id, 3, 2.25), 9);
        // Between the level and the ridge the south part rises again to meet it (its plane mirrored in the east's).
        Assert.Equal(0.04, TopAt(f, id, 2, 2.5), 9);
        // The edges keep their heights: both gutters at 0.
        Assert.Equal(0, TopAt(f, id, 3, 0), 9);
        Assert.Equal(0, TopAt(f, id, 6, 2), 9);
        // The body is continuous: its top's triangles meet edge to edge (a closed solid).
        Assert.True(Vol(f, id) > 0);
        // The limit taken away: the two planes meet in their ridge alone.
        f.Run("architecture.updateFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["index"] = 0, ["until"] = null });
        Assert.Equal(0.03, TopAt(f, id, 1, 1.5), 9);
    }

    [Fact]
    public void A_floor_falling_from_two_edges_keeps_the_limit_of_one_off_the_other()
    {
        var f = F();
        // 3 × 2 m: the south edge falls in at 2 %; the north at 2 % until −10 mm. Their valley at y = 1 (−20 mm) stays.
        var id = Floor(f, Rect(0, 0, 3, 2));
        f.Run("architecture.setFloorSlope", new JsonObject { ["id"] = id, ["how"] = "bend" });
        f.Run("architecture.addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 0, ["grade"] = "2%", ["fall"] = "in" });
        f.Run("architecture.addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["edge"] = 2, ["grade"] = "2%", ["fall"] = "in", ["until"] = new JsonObject { ["offset"] = -0.01 } });
        // The south slope falls on, below −10 mm, to the valley (it used to stop at −10 mm everywhere).
        Assert.Equal(-0.015, TopAt(f, id, 1.5, 0.75), 9);
        Assert.Equal(-0.02, TopAt(f, id, 1.5, 1), 9);
        // The north: its slope, its level, then down into the valley.
        Assert.Equal(-0.005, TopAt(f, id, 1.5, 1.75), 9);
        Assert.Equal(-0.01, TopAt(f, id, 1.5, 1.25), 9);
        Assert.Equal(0, TopAt(f, id, 1.5, 2), 9);
        // An edge alone with a limit: level beyond it everywhere, as before.
        f.Run("architecture.removeFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "edge", ["index"] = 0 });
        Assert.Equal(-0.01, TopAt(f, id, 1.5, 0.25), 9);
    }

    // ─── 4. The TIN in Lapin.Geometry ────────────────────────────────────

    [Fact]
    public void A_floor_s_mesh_read_as_a_TIN_gives_the_same_heights()
    {
        var f = F();
        var id = Floor(f, Rect(0, 0, 4, 3));
        f.Run("architecture.addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "drain", ["at"] = P(1.3, 1.1), ["depth"] = 0.02 });
        f.Run("architecture.addFloorSlope", new JsonObject { ["id"] = id, ["kind"] = "point", ["at"] = P(3.1, 2.2), ["offset"] = -0.006 });
        var piece = FloorSlopes.Solve(f.Model, f.Model.Get(id)!, f.Session.Catalog)!.Pieces[0].Piece;
        var ground = new Terrain(piece.Mesh.ToTin());
        foreach (var (x, y) in new[] { (0.5, 0.5), (1.3, 1.1), (2.2, 1.7), (3.9, 2.9), (3.1, 2.2) })
            Assert.Equal(piece.At(new Vec2(x, y))!.Value, ground.ElevationAt(x, y)!.Value, 12);
    }

    // ─── 5. T6's leftovers ───────────────────────────────────────────────

    [Fact]
    public void A_channel_s_IL_and_low_point_are_written_in_the_drawing_s_language()
    {
        var f = F();
        var t = Field(f);
        f.Run("civil.addChannel", new JsonObject { ["points"] = Pts([5, 5], [15, 5]), ["kind"] = "u", ["grade"] = 0.01, ["startZ"] = 0.6 });
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 200 })!["id"]!;
        var texts = Texts(f, view).Select(x => x.Text).ToList();
        Assert.Contains(texts, x => x.StartsWith("IL "));
        f.Run("standards.options", new JsonObject { ["drawingLang"] = "ro" });
        Assert.Equal("ro", DraftingResources.Book(f.Model).DrawingLang);
        texts = Texts(f, view).Select(x => x.Text).ToList();
        Assert.Contains(texts, x => x.StartsWith("CR "));
        Assert.DoesNotContain(texts, x => x.StartsWith("IL "));
        Assert.Equal("Punct jos", DrawingWords.Of("Low point", "ro"));
        Assert.Equal("Tiefpunkt", DrawingWords.Of("Low point", "de"));
        // Every language has both words.
        foreach (var lang in DrawingWords.Languages.Where(l => l != "en"))
        {
            Assert.NotEqual("IL", DrawingWords.Of("IL", lang));
            Assert.NotEqual("Low point", DrawingWords.Of("Low point", lang));
        }
        f.Run("standards.options", new JsonObject { ["drawingLang"] = "en" });
        Assert.Null(DraftingResources.Book(f.Model).DrawingLang);
        Assert.Throws<CommandException>(() => f.Run("standards.options", new JsonObject { ["drawingLang"] = "xx" }));
        Assert.NotNull(t);
    }

    [Fact]
    public void A_standalone_kerb_through_a_hardscape_cuts_its_layers_and_its_quantities()
    {
        var f = F();
        Field(f);
        var plaza = (string)f.Run("civil.addHardscape", new JsonObject { ["points"] = Rect(10, 10, 30, 20), ["type"] = "pavers", ["elevation"] = 1.0 })!["id"]!;
        var before = Vol(f, plaza);
        var layersBefore = Q(f, plaza, "Lapin_HardscapeQuantities", "Volume");
        Assert.Equal(before, layersBefore, 6);
        // A kerb across the plaza: its body comes out of the plaza's layers, and out of their volumes.
        var kerb = (string)f.Run("civil.addKerb", new JsonObject { ["points"] = Pts([12, 15], [28, 15]), ["follow"] = "ground" })!["id"]!;
        var cut = Vol(f, plaza);
        // 16 m of it, 0.15 m wide, 0.25 m down into the 0.30 m build-up from its top.
        Assert.Equal(16 * 0.15 * 0.25, before - cut, 6);
        Assert.Equal(cut, Q(f, plaza, "Lapin_HardscapeQuantities", "Volume"), 6);
        var sum = Enumerable.Range(0, 4).Select(i => f.Session.Quantities(plaza).FirstOrDefault(s => s.Name == "Lapin_HardscapeLayerQuantities" && s.Part == "layer:" + i))
            .OfType<QuantitySet>().Sum(s => s.Quantities.First(q => q.Name == "Volume").Value);
        Assert.Equal(cut, sum, 6);
        // Moved off the plaza: whole again; a kerb along its edge on the outside cuts nothing.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(kerb), ["op"] = "move", ["by"] = P(0, 10) });
        Assert.Equal(before, Vol(f, plaza), 9);
        f.Run("civil.addKerb", new JsonObject { ["along"] = new JsonObject { ["id"] = plaza } });
        Assert.Equal(before, Vol(f, plaza), 9);
        Assert.Equal(layersBefore, Q(f, plaza, "Lapin_HardscapeQuantities", "Volume"), 9);
    }

    [Fact]
    public void A_hardscape_s_layer_quantities_take_away_a_channel_running_through_it()
    {
        var f = F();
        Field(f);
        var plaza = (string)f.Run("civil.addHardscape", new JsonObject { ["points"] = Rect(10, 10, 30, 20), ["type"] = "pavers", ["elevation"] = 1.0 })!["id"]!;
        var area = Q(f, plaza, "Lapin_HardscapeQuantities", "Area");
        var layer0 = Q(f, plaza, "Lapin_HardscapeLayerQuantities", "Volume", "layer:0");
        var thickness = Q(f, plaza, "Lapin_HardscapeLayerQuantities", "Thickness", "layer:0");
        Assert.Equal(area * thickness, layer0, 9);
        f.Run("civil.addChannel", new JsonObject { ["points"] = Pts([12, 15], [28, 15]), ["kind"] = "grated", ["grade"] = "0.5 %", ["startZ"] = 0.80 });
        // Each layer's volume is its body's as drawn: the channel's 0.25 m wide trench out of it.
        for (var i = 0; i < 4; i++)
        {
            if (f.Session.Quantities(plaza).FirstOrDefault(s => s.Name == "Lapin_HardscapeLayerQuantities" && s.Part == "layer:" + i) is not { } set) continue;
            Assert.Equal(Vol(f, plaza, "layer:" + i), set.Quantities.First(q => q.Name == "Volume").Value, 9);
        }
        var cut0 = Q(f, plaza, "Lapin_HardscapeLayerQuantities", "Volume", "layer:0");
        Assert.InRange(layer0 - cut0, 16 * 0.25 * thickness * 0.98, 16 * 0.25 * thickness * 1.02 + 0.4 * 0.4 * thickness);
        Assert.Equal(Vol(f, plaza), Q(f, plaza, "Lapin_HardscapeQuantities", "Volume"), 6);
    }
}
