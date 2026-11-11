using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Documents;
using Lapin.Structure;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// A tester's report (#76, Finland; decision 471): in the Nordic countries room areas are written rounded to x,0 or x,5 m²
/// (SFS 5139), or to whole square metres. The project's Area rounding (Manage → Drafting standards → Project) rounds every
/// space area shown, half up: the rooms' stamps, the legend, the room schedule (each row rounded, the total the sum of the
/// rows) and the documents' figures; the quantities and the IFC file keep the exact area.
/// </summary>
public sealed class AreaRoundingTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-area-rounding").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    /// <summary>Two drawn rooms side by side: 3 × 4.09 m (12.27 m²) and 3.5 × 3.5 m (12.25 m²), and a plan of their storey.</summary>
    static (Fixture F, string A, string B, string Plan) Two()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new DraftingModule());
        string Drawn(double x, double width, double depth, string function) => (string)f.Run("architecture.addRoom", new JsonObject
        {
            ["storeyId"] = f.Storey, ["x1"] = x, ["y1"] = 0, ["x2"] = x + width, ["y2"] = depth, ["name"] = function, ["function"] = function,
        })!["id"]!;
        var a = Drawn(0, 3, 4.09, "Kitchen");
        var b = Drawn(10, 3.5, 3.5, "Bedroom");
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        return (f, a, b, plan);
    }

    static void Step(Fixture f, double? step) => f.Run("standards.options", new JsonObject { ["areaRounding"] = step });

    static List<string> Texts(Fixture f, string plan) =>
        Views.Render(new Q(f), Views.Find(f.Model, plan)!).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();

    [Fact]
    public void An_area_rounds_to_its_step_half_up()
    {
        Assert.Equal(12.5, AreaRounding.ToStep(12.27, 0.5), 9);
        Assert.Equal(12, AreaRounding.ToStep(12.27, 1), 9);
        Assert.Equal(12.3, AreaRounding.ToStep(12.27, 0.1), 9);
        Assert.Equal(12.5, AreaRounding.ToStep(12.25, 0.5), 9);
        Assert.Equal(12.5, AreaRounding.ToStep(12.74, 0.5), 9);
        Assert.Equal(13, AreaRounding.ToStep(12.75, 0.5), 9);
        Assert.Equal(13, AreaRounding.ToStep(12.5, 1), 9);
        Assert.Equal(12.3, AreaRounding.ToStep(12.25, 0.1), 9);
        Assert.Equal(12.27, AreaRounding.Shown(12.27, null), 9);
        Assert.Equal("12.27 m²", AreaRounding.Text(12.27, null, "mm"));
        Assert.Equal("12.5 m²", AreaRounding.Text(12.27, 0.5, "mm"));
        Assert.Equal("12.0 m²", AreaRounding.Text(12.1, 0.5, "mm"));
        Assert.Equal("12 m²", AreaRounding.Text(12.27, 1, "m"));
        // An imperial project rounds its square feet: 12.27 m² is 132.07 ft².
        Assert.Equal("132.1 ft²", AreaRounding.Text(12.27, null, "ft-in"));
        Assert.Equal("132 ft²", AreaRounding.Text(12.27, 1, "ft-in"));
        Assert.Equal("132.0 ft²", AreaRounding.Text(12.27, 0.5, "in"));
    }

    [Fact]
    public void The_project_keeps_its_step_and_refuses_another()
    {
        var (f, _, _, _) = Two();
        Assert.Null(DraftingResources.Book(f.Model).AreaRounding);
        Step(f, 0.5);
        Assert.Equal(0.5, DraftingResources.Book(f.Model).AreaRounding);
        Assert.Equal(0.5, (double)((JsonObject)f.Session.Query("standards.get", [])!)["areaRounding"]!);
        Assert.ThrowsAny<Exception>(() => Step(f, 0.25));
        Assert.Equal(0.5, DraftingResources.Book(f.Model).AreaRounding);
        // One undo step, and null puts it back to the measured area.
        f.Session.Undo();
        Assert.Null(DraftingResources.Book(f.Model).AreaRounding);
        Step(f, 1);
        Step(f, null);
        Assert.Null(DraftingResources.Book(f.Model).AreaRounding);
        // A standards file read with a step that is not one is read without it.
        Assert.Null(Standards.Read(new JsonObject { ["areaRounding"] = 0.3 }).AreaRounding);
    }

    [Fact]
    public void A_rooms_stamp_shows_its_area_to_the_projects_step()
    {
        var (f, _, _, plan) = Two();
        Assert.Contains("12.27 m²", Texts(f, plan));
        Step(f, 0.5);
        var half = Texts(f, plan);
        Assert.Contains("12.5 m²", half);
        Assert.DoesNotContain("12.27 m²", half);
        Step(f, 1);
        Assert.Contains("12 m²", Texts(f, plan));
        Step(f, 0.1);
        Assert.Contains("12.3 m²", Texts(f, plan));
    }

    [Fact]
    public void A_legend_adds_up_its_rooms_as_shown()
    {
        var (f, _, _, plan) = Two();
        f.Run("architecture.viewColorScheme", new JsonObject { ["viewId"] = plan, ["schemeId"] = "by-function" });
        f.Run("architecture.addColorLegend", new JsonObject { ["viewId"] = plan, ["x"] = 20, ["y"] = 0, ["showArea"] = true });
        Assert.Contains("Kitchen   12.27 m²", Texts(f, plan));
        Step(f, 0.5);
        Assert.Contains("Kitchen   12.5 m²", Texts(f, plan));
        // The configurator's values sum their rooms as the stamps show them.
        var values = ((JsonObject)f.Session.Query("architecture.colorSchemes", [])!)["values"]!["function"]!.AsArray();
        Assert.Equal(12.5, (double)values.Single(v => (string)v!["value"]! == "Kitchen")!["area"]!, 9);
    }

    [Fact]
    public void The_room_schedule_rounds_each_room_and_totals_what_its_rows_show()
    {
        var (f, _, _, _) = Two();
        string[] Areas() => [.. new RoomSchedule().Grid(new Q(f)).Rows.Select(r => r[5])];
        Assert.Equal(["12.27", "12.25", "24.52"], Areas());
        // 12.27 and 12.25 both show 12.5: the total is 25.0, not 24.52 rounded (24.5).
        Step(f, 0.5);
        Assert.Equal(["12.5", "12.5", "25.0"], Areas());
        // To whole metres both show 12 and the total 24 (24.52 rounded would be 25).
        Step(f, 1);
        Assert.Equal(["12", "12", "24"], Areas());
        // The volume stays the exact area times the height.
        var grid = new RoomSchedule().Grid(new Q(f));
        var h = Rooms.HeightOf(f.Model.OfClass(Rooms.Class).First(), f.Model);
        Assert.Equal((12.27 * h).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), grid.Rows[0][8]);
    }

    [Fact]
    public void A_documents_areas_follow_the_step_and_its_usable_area_adds_up_the_rooms_as_shown()
    {
        var (f, a, _, _) = Two();
        var ro = new DocFormat("ro", "metric", 0.5);
        Assert.Equal("12,5 m²", DocNumbers.Area(12.27, ro));
        Assert.Equal("1.234,50 m²", DocNumbers.Area(1234.5, new DocFormat("ro", "metric")));
        Assert.Equal("12 m²", DocNumbers.Area(12.27, new DocFormat("en", "metric", 1)));
        Step(f, 0.5);
        var format = DocumentFill.FormatOf(f.Model, f.Model.Get(a)!);
        Assert.Equal(0.5, format.AreaStep);
        var fields = DocumentFill.AllFields(f.Session.Reader, format).ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal("25,0 m²", fields["areas.usable"]);
        Assert.Equal("12,5 m²", DocumentFill.Items(f.Session.Reader, "rooms", format).Single(x => x["id"] == a)["area"]);
    }

    [Fact]
    public void Quantities_and_the_ifc_file_keep_the_exact_area()
    {
        var (f, a, _, _) = Two();
        Step(f, 1);
        Assert.Equal(12.27, f.Session.Quantities(a).Single().Quantities.Single(q => q.Name == "NetFloorArea").Value, 6);
        var path = Path.Combine(_dir, "rooms.ifc");
        IfcExport.Write(f.Session, path);
        var file = StepFile.Read(File.ReadAllBytes(path));
        var areas = file.OfType("IFCQUANTITYAREA").Where(x => (string?)x[0] == "NetFloorArea").Select(x => IfcModel.Num(x[3])).ToList();
        Assert.Contains(areas, v => Math.Abs(v - 12.27) < 1e-6);
        Assert.DoesNotContain(areas, v => Math.Abs(v - 12) < 1e-6);
    }
}
