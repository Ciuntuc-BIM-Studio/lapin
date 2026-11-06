using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Architecture.Schedules;
using Lapin.Cost;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A roof's covering made of a project material (decision 328, the open item of 327): the covering choice's own by default
/// (found or made by name, in its colour, no texture), chosen on the roof, following the covering when it changes; elevations
/// hatching the slopes they see by its graphics or its name (tiles HORIZONTAL, metal VERTICAL, an explicit none respected),
/// a section's cut drawing the covering band with its section hatch, IFC carrying it both ways, the takeoff and an article
/// ruled by the material measuring the covering's area once, a planning rule by material finding the roof, old roofs upgraded.
/// </summary>
public class RoofCoveringMaterialTests
{
    static JsonArray House10x6 => new(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6));

    /// <summary>A gable roof over a 10 × 6 m house (Provence: clay tile, 24°, the ridge along x).</summary>
    static string AddRoof(Fixture f, string style = "fr_provence") =>
        (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = House10x6, ["style"] = style })!["id"]!;

    static void Set(Fixture f, string roof, JsonObject attrs) => f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = attrs });

    static Entity? Material(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).FirstOrDefault(e => e.Name == name);

    static GeometryPart Covering(Fixture f, string roof) => f.Session.Geometry(roof)!.Parts.Single(p => p.Key == "covering");

    static string Elevation(Fixture f, string direction) =>
        (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = direction })!["id"]!;

    static Views.Rendered Render(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);

    static List<(Vec2 A, Vec2 B)> Hatch(Views.Rendered r) => r.Pens.Where(p => p.Layer == SurfaceHatches.Layer).SelectMany(p => p.Segments).ToList();

    static double Length(IEnumerable<(Vec2 A, Vec2 B)> segs) => segs.Sum(s => (s.B - s.A).Length);

    [Fact]
    public void A_new_roof_s_covering_is_made_of_its_covering_s_material_in_its_colour()
    {
        var f = new Fixture();
        var roof = AddRoof(f);
        Assert.Equal("Clay tile", f.Model.Get(roof)!.Str(RoofCoverings.Key));
        var tile = Material(f, "Clay tile")!;
        // Made as the covering looked: its colour, no texture (3D as before), not a membrane.
        Assert.Equal(("#b0563d", "none"), (tile.Str("color"), tile.Str("texture")));
        var part = Covering(f, roof);
        Assert.Equal((tile.Id, "#b0563d"), (part.Material, part.Color));
        // A second roof finds it: one material of that name.
        f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = House10x6, ["style"] = "fr_provence" });
        Assert.Single(f.Model.OfClass(IfcClass.Material), e => e.Name == "Clay tile");
        // The material's colour is the covering's in 3D.
        f.Run("material.update", new JsonObject { ["id"] = tile.Id, ["color"] = "#aa2222" });
        Assert.Equal("#aa2222", Covering(f, roof).Color);
        // A metal sheet's name reads as a membrane in a cut: its material is made not to be one, so its band stays.
        Set(f, roof, new JsonObject { ["covering"] = "Metal sheet" });
        var metal = Material(f, "Metal sheet")!;
        Assert.False(MaterialGraphics.Resolve(metal, "section").Membrane);
    }

    [Fact]
    public void The_covering_s_material_follows_the_covering_unless_the_user_chose_one()
    {
        var f = new Fixture();
        var roof = AddRoof(f);
        // Changed in the configurator: the new covering's material, made.
        Set(f, roof, new JsonObject { ["covering"] = "Slate" });
        Assert.Equal("Slate", f.Model.Get(roof)!.Str(RoofCoverings.Key));
        Assert.Equal(Material(f, "Slate")!.Id, Covering(f, roof).Material);
        // Changed in the properties (element.update): the same.
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(roof), ["attrs"] = new JsonObject { ["covering"] = "Clay tile" } });
        Assert.Equal("Clay tile", f.Model.Get(roof)!.Str(RoofCoverings.Key));
        // One the user chose stays when the covering changes.
        f.Run("material.create", new JsonObject { ["name"] = "Tondach Figaro red" });
        Set(f, roof, new JsonObject { [RoofCoverings.Key] = "Tondach Figaro red" });
        Set(f, roof, new JsonObject { ["covering"] = "Concrete tile" });
        Assert.Equal("Tondach Figaro red", f.Model.Get(roof)!.Str(RoofCoverings.Key));
        Assert.Equal(Material(f, "Tondach Figaro red")!.Id, Covering(f, roof).Material);
        // A style put on it keeps the user's choice too.
        f.Run("architecture.roofStyle", new JsonObject { ["id"] = roof, ["style"] = "modern_barn" });
        Assert.Equal("Tondach Figaro red", f.Model.Get(roof)!.Str(RoofCoverings.Key));
        // Renamed in Manage → Materials: the roof follows the name.
        f.Run("material.update", new JsonObject { ["id"] = Material(f, "Tondach Figaro red")!.Id, ["name"] = "Figaro" });
        Assert.Equal("Figaro", f.Model.Get(roof)!.Str(RoofCoverings.Key));
        // None: the part has no material.
        Set(f, roof, new JsonObject { [RoofCoverings.Key] = "" });
        Assert.Null(Covering(f, roof).Material);
    }

    [Fact]
    public void A_tiled_roof_s_elevation_hatches_its_visible_slopes_with_horizontal_courses()
    {
        var f = new Fixture();
        var roof = AddRoof(f);
        // From the south the south slope faces the viewer: courses, every one level.
        var south = Hatch(Render(f, Elevation(f, "south")));
        Assert.NotEmpty(south);
        Assert.All(south, s => Assert.True(Math.Abs(s.A.Y - s.B.Y) < 1e-6));
        // Within the roof, between the eaves and the ridge (the plates at 3 m, the ridge 3 · tan 24° above them).
        var (lo, hi) = f.Bounds(roof);
        Assert.All(south, s => Assert.InRange((s.A.Y + s.B.Y) / 2, lo.Z - 1e-6, hi.Z + 1e-6));
        Assert.True(Length(south) > 50);
        // From the east the slopes are edge-on: only the covering's thin verge, a fraction of a slope's courses.
        var east = Hatch(Render(f, Elevation(f, "east")));
        Assert.True(Length(east) < 0.05 * Length(south), $"east {Length(east):0.00} m against south {Length(south):0.00} m");
        // From the north the south slope is turned away: the north slope's courses, no more than the south's.
        var north = Hatch(Render(f, Elevation(f, "north")));
        Assert.InRange(Length(north), 0.9 * Length(south), 1.1 * Length(south));
    }

    [Fact]
    public void A_metal_roof_is_hatched_vertical_and_an_explicit_none_is_respected()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "modern_barn");
        Assert.Equal("Metal sheet", RoofCoverings.NameOf(f.Model.Get(roof)!));
        var view = Elevation(f, "south");
        var seams = Hatch(Render(f, view));
        Assert.NotEmpty(seams);
        Assert.All(seams, s => Assert.True(Math.Abs(s.A.X - s.B.X) < 1e-6));
        // The material's own Elevation hatch set to none: nothing, the name's guess put aside.
        f.Run("material.update", new JsonObject { ["id"] = Material(f, "Metal sheet")!.Id, ["elevation"] = new JsonObject { ["hatch"] = null } });
        Assert.Empty(Hatch(Render(f, view)));
        // Without a material the covering draws no surface at all.
        f.Run("material.update", new JsonObject { ["id"] = Material(f, "Metal sheet")!.Id, ["elevation"] = null });
        Assert.NotEmpty(Hatch(Render(f, view)));
        Set(f, roof, new JsonObject { [RoofCoverings.Key] = "" });
        Assert.Empty(Hatch(Render(f, view)));
    }

    [Fact]
    public void A_section_through_the_roof_draws_the_covering_band_with_its_material_s_hatch()
    {
        var f = new Fixture();
        var roof = AddRoof(f);
        // Across the ridge at x = 5, looking west: both slopes cut.
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(5, -2), ["end"] = new JsonArray(5, 8) })!["id"]!;
        var r = Render(f, view);
        Assert.Equal("TILES", MaterialGraphics.Resolve(Material(f, "Clay tile"), "section").Hatch);
        var cut = r.Pens.Where(p => p.Layer == PenLayer.Hatch).SelectMany(p => p.Segments).ToList();
        Assert.NotEmpty(cut);
        // In the covering's band: above the plates and under the ridge's top, on both sides of the ridge.
        var (lo, hi) = f.Bounds(roof);
        Assert.All(cut, s => Assert.InRange((s.A.Y + s.B.Y) / 2, lo.Z - 1e-6, hi.Z + 1e-6));
        var xs = cut.Select(s => (s.A.X + s.B.X) / 2).ToList();
        Assert.True(xs.Max() - xs.Min() > 5);
        // No membrane line: a covering's cut is a band (a metal sheet's too).
        Set(f, roof, new JsonObject { ["covering"] = "Metal sheet" });
        Assert.DoesNotContain(Render(f, view).Pens, p => p.Layer == PenLayer.Membrane && p.Segments.Count > 0);
    }

    [Fact]
    public void IFC_carries_the_covering_s_material_both_ways()
    {
        var f = new Fixture();
        var roof = AddRoof(f);
        f.Run("material.update", new JsonObject { ["id"] = Material(f, "Clay tile")!.Id, ["color"] = "#993322" });
        var path = Path.Combine(Path.GetTempPath(), $"roof-material-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            // The covering slab (IfcSlab ROOF) is associated with the material Clay tile.
            var slab = step.OfType("IFCSLAB").Single(s => s[8] is StepEnum { Value: "ROOF" } && (string?)s[2] == "Covering");
            var tile = step.OfType("IFCMATERIAL").Single(m => (string?)m[0] == "Clay tile");
            Assert.Contains(step.OfType("IFCRELASSOCIATESMATERIAL"), rel => rel[5] is StepRef to && to.Id == tile.Id
                && rel[4] is List<object?> list && list.OfType<StepRef>().Any(x => x.Id == slab.Id));
            // Read back: the roof names it, the project has it in its colour, the covering is made of it.
            var g = new Fixture();
            IfcImport.Run(g.Session, path, null, out _);
            var back = g.Model.OfClass(IfcClass.Roof).Single();
            Assert.Equal("Clay tile", back.Str(RoofCoverings.Key));
            var mat = Material(g, "Clay tile")!;
            Assert.Equal("#993322", mat.Str("color"));
            Assert.Equal(mat.Id, Covering(g, back.Id).Material);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void The_takeoff_and_an_article_ruled_by_the_material_measure_the_covering_once()
    {
        var f = new Fixture(new ArchitectureModule(), new CostModule());
        var roof = AddRoof(f);
        var sets = f.Session.Quantities(roof);
        var whole = sets.Single(s => s.Name == "Qto_RoofBaseQuantities").Quantities;
        var covering = sets.Single(s => s.Part == "covering");
        Assert.Equal(Material(f, "Clay tile")!.Id, covering.Material);
        double Q(IReadOnlyList<Quantity> qs, string n) => qs.Single(q => q.Name == n).Value;
        Assert.Equal(Q(whole, "GrossArea"), Q(covering.Quantities, "GrossArea"), 9);
        Assert.Equal(Q(whole, "NetArea"), Q(covering.Quantities, "NetArea"), 9);
        Assert.Equal(0.05, Q(covering.Quantities, "Width"), 9);

        // The takeoff by material: the roof under Clay tile, its covering's area, nothing under no material.
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "quantities" })!["id"]!;
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["groupBy"] = new JsonArray("material"), ["classes"] = new JsonArray(IfcClass.Roof) });
        var grid = new QuantityTakeoff().Grid(f.Session.Reader, f.Model.Get(id)!);
        var row = grid.Rows.Single(r => r[0] == "Clay tile");
        Assert.Equal(Q(whole, "GrossArea").ToString("0.00", CultureInfo.InvariantCulture), row[grid.Columns.ToList().IndexOf("Gross area (m²)")]);
        Assert.DoesNotContain(grid.Rows, r => r[0] == "—");
        // By kind: the roof once, its own areas.
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["groupBy"] = new JsonArray("kind") });
        var byKind = new QuantityTakeoff().Grid(f.Session.Reader, f.Model.Get(id)!);
        Assert.Equal(Q(whole, "GrossArea").ToString("0.00", CultureInfo.InvariantCulture), byKind.Rows.Single(r => r[0] == "Roof")[byKind.Columns.ToList().IndexOf("Gross area (m²)")]);

        // An article ruled by the covering's material: priced by the covering's area, in the Roof chapter, once.
        var csv = "Cod;Denumire;U.M.;Material;Manoperă\nINV01;Învelitoare din țiglă ceramică;mp;60;25\n";
        f.Run("cost.importPrices", new JsonObject { ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(csv)), ["name"] = "Prețuri.csv" });
        f.Run("cost.setRule", new JsonObject { ["material"] = Material(f, "Clay tile")!.Id, ["code"] = "INV01" });
        f.Run("cost.createEstimate", new JsonObject());
        var est = (JsonObject)f.Session.Query("cost.estimate", new JsonObject())!;
        var lines = est["chapters"]!.AsArray().SelectMany(c => c!["lines"]!.AsArray().Select(l => ((string)c["name"]!, (JsonObject)l!))).Where(x => (string)x.Item2["code"]! == "INV01").ToList();
        var (chapter, line) = Assert.Single(lines);
        Assert.Equal("Roof", chapter);
        Assert.Equal(Q(whole, "NetArea"), (double)line["qty"]!, 2);
    }

    [Fact]
    public void A_planning_rule_by_the_covering_s_material_takes_the_roof()
    {
        var f = new Fixture(new ArchitectureModule(), new PhasesModule(), new PlanningModule());
        var roof = AddRoof(f);
        var wall = f.Wall(0, 0, 10, 0);
        var task = (string)f.Run("planning.addTask", new JsonObject { ["name"] = "Roof covering", ["duration"] = 5 })!["id"]!;
        f.Run("planning.updateTask", new JsonObject { ["id"] = task, ["rule"] = new JsonObject { ["materials"] = new JsonArray(Material(f, "Clay tile")!.Id) } });
        var s = (JsonObject)f.Session.Query("planning.schedule", new JsonObject())!;
        var row = (JsonObject)s["tasks"]!.AsArray().First(t => (string)t!["id"]! == task)!;
        var elements = (row["elements"] as JsonArray)?.Select(x => (string)x!).ToList() ?? [];
        Assert.Contains(roof, elements);
        Assert.DoesNotContain(wall, elements);
    }

    [Fact]
    public void An_old_roof_is_given_its_covering_s_material_and_looks_as_it_did()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "dk_parcelhus");
        var before = Covering(f, roof).Color;
        // As a project from before decision 328 kept it: no covering material on the roof, none in the project.
        var old = f.Model.Entities.Where(e => e.Class != IfcClass.Material || e.Name != "Slate")
            .Select(e => e.Id == roof ? e.With(attrs: Without(e.CloneAttrs(), RoofCoverings.Key)) : e).ToList();
        var (up, _) = f.Session.Upgraded(old, f.Model.Relations.ToList());
        var upRoof = up.Single(e => e.Id == roof);
        Assert.Equal("Slate", upRoof.Str(RoofCoverings.Key));
        var slate = Assert.Single(up, e => e.Class == IfcClass.Material && e.Name == "Slate");
        Assert.Equal((before, "none"), (slate.Str("color"), slate.Str("texture")));
        // The same material id each time the file opens (a recovery replays edits on it); nothing more the second time.
        var (again, _) = f.Session.Upgraded(old, f.Model.Relations.ToList());
        Assert.Equal(slate.Id, again.Single(e => e.Class == IfcClass.Material && e.Name == "Slate").Id);
        var (twice, _) = new RoofCoveringUpgrade().Upgrade(up, []);
        Assert.Equal(up.Count, twice.Count);
        // A project that already has a material of that name keeps it (one material, the project's).
        var (kept, _) = new RoofCoveringUpgrade().Upgrade([.. old, new Entity("2ab3Cd4Ef5Gh6Ij7Kl8Mn9", IfcClass.Material, "slate", new JsonObject { ["color"] = "#111111" })], []);
        Assert.Single(kept, e => e.Class == IfcClass.Material && string.Equals(e.Name, "slate", StringComparison.OrdinalIgnoreCase));
    }

    static JsonObject Without(JsonObject o, string key) { o.Remove(key); return o; }
}
