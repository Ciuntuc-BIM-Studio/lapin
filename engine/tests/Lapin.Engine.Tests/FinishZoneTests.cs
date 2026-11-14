using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The finish painter (forum topic 83, docs/plan-finish-painter.md): finishes painted on the named faces of walls and floors,
/// whole or on the regions lines drawn on the face divide it into; real bodies from the face outward, drawn, measured and
/// exported as IfcCovering; following their host.
/// </summary>
public class FinishZoneTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-finish-zones-").FullName;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    // The default wall type: render 20 mm on the right, a 250 mm core, plaster 15 mm on the left.
    const double Left = 0.125 + 0.015, Right = 0.125 + 0.02, H = 3.0;
    const double Etics = 0.005 + 0.10 + 0.01;

    static string Library(Fixture f, string name) => (string)f.Run("architecture.surfaceFinishTypeFromLibrary", new JsonObject { ["name"] = name })!["id"]!;

    static List<Entity> Zones(Fixture f, string host) => f.Model.OfClass(FinishZones.Class).Where(e => e.Str("hostId") == host).ToList();

    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    static double Material(Fixture f, string id, string material) =>
        f.Session.Geometry(id)!.Parts.Where(p => f.Model.Get(p.Material ?? "")?.Name == material).Sum(p => Measure.Volume(p.Solid));

    static double Net(Fixture f, string id) =>
        f.Session.Quantities(id).Single(q => q.Name == "Qto_CoveringBaseQuantities").Quantities.Single(q => q.Name == "NetArea").Value;

    static JsonArray P(double x, double y, double z) => new(x, y, z);

    [Fact]
    public void An_exterior_face_painted_with_etics_builds_its_layers_net_of_the_openings_draws_them_and_goes_to_ifc_as_a_cladding()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0, height: H);
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 2 });
        var window = Openings.PlacedOn(f.Model, wall).Single();
        var etics = Library(f, "ETICS EPS 100");
        var zone = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["face"] = "Face.Right", ["whole"] = true, ["finishType"] = etics })!["id"]!;

        var net = 5 * H - window.Width * window.Height;
        Assert.Equal(net, Net(f, zone), 6);
        Assert.Equal(net * Etics, Volume(f, zone), 6);
        Assert.Equal(net * 0.10, Material(f, zone, "EPS insulation"), 6);
        // Outside the render's face, from the face outward: the adhesive against it, the render outermost.
        var b = f.Bounds(zone);
        Assert.Equal(-Right - Etics, b.Min.Y, 6);
        Assert.Equal(-Right, b.Max.Y, 6);
        var e = f.Model.Get(zone)!;
        Assert.Equal("CLADDING", e.PredefinedType);
        Assert.Equal("IfcCovering", e.Str("exportAs"));
        // Its layers measured each, by material.
        var layers = f.Session.Quantities(zone).Where(q => q.Name == "Lapin_FinishLayerQuantities").ToList();
        Assert.Equal(["Adhesive mortar", "EPS insulation", "Render"], layers.Select(q => q.PartName!));
        Assert.All(layers, q => Assert.Equal(net, q.Quantities.Single(x => x.Name == "NetArea").Value, 6));

        // A section across the wall cuts the three layers, by material.
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0.5, -1), ["end"] = new JsonArray(0.5, 1) })!["id"]!;
        var (d, _, problem) = Views.Generate(f.Session.Reader, Views.Find(f.Model, section)!);
        Assert.True(d != null, problem);
        var cut = d!.Cuts.Where(c => c.Element == zone).Select(c => f.Model.Get(c.Material ?? "")?.Name).Distinct().Order().ToList();
        Assert.Equal(["Adhesive mortar", "EPS insulation", "Render"], cut);
        // And the plan, drawing every layer (the Architectural style leaves the finishes out of plans by default, as the walls' own).
        f.EveryLayerInPlans();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var (pd, _, _) = Views.Generate(f.Session.Reader, Views.Find(f.Model, plan)!);
        Assert.Contains(pd!.Cuts, c => c.Element == zone);

        // The schedule lists it by material.
        var grid = new FinishZoneSchedule().Grid(f.Session.Reader);
        Assert.Contains(grid.Rows, r => r[3] == "EPS insulation");

        // A stroke on the zone itself goes to the face under it: the eyedropper reads it there, the brush paints it again.
        JsonArray Outside() => P(1, -Right - Etics, 1);
        var picked = (JsonObject)f.Session.Query("architecture.finishFaceAt", new JsonObject { ["id"] = zone, ["point"] = Outside(), ["normal"] = P(0, -1, 0) })!;
        Assert.Equal(zone, (string?)picked["zoneId"]);
        Assert.Equal(-Right - Etics, (double)((JsonArray)((JsonArray)picked["outline"]!)[0]!)[1]!, 6);
        var again = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = zone, ["point"] = Outside(), ["normal"] = P(0, -1, 0), ["whole"] = true, ["finishType"] = etics })!["id"]!;
        Assert.Null(f.Model.Get(zone));
        Assert.Equal("Face.Right", f.Model.Get(again)!.Str("face"));
        zone = again;

        // IFC: a covering, CLADDING, covering its wall.
        var path = Path.Combine(_dir, "etics.ifc");
        IfcExport.Write(f.Session, path);
        var step = StepFile.Read(path);
        var covering = Assert.Single(step.OfType("IFCCOVERING"));
        Assert.Equal(new StepEnum("CLADDING"), covering[8]);
        var rel = Assert.Single(step.OfType("IFCRELCOVERSBLDGELEMENTS"));
        Assert.Equal(step.OfType("IFCWALL").Single().Id, ((StepRef)rel[4]!).Id);
    }

    [Fact]
    public void A_face_split_at_a_height_takes_tiles_below_and_plaster_above_and_follows_its_wall_and_undo()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0, height: H);
        f.Run("architecture.splitFinishFace", new JsonObject { ["hostId"] = wall, ["face"] = "Face.Left", ["height"] = 1.2 });
        var tiles = Library(f, "Wall tiles");
        var plaster = Library(f, "Lime plaster 25");
        var low = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["point"] = P(2.5, Left, 0.5), ["normal"] = P(0, 1, 0), ["finishType"] = tiles })!["id"]!;
        Assert.Equal("Face.Left", f.Model.Get(low)!.Str("face"));
        Assert.Equal(5 * 1.2, Net(f, low), 6);
        Assert.Equal(5 * 1.2 * 0.015, Volume(f, low), 6);
        Assert.Equal(1.2, f.Bounds(low).Max.Z, 6);
        Assert.Equal(Left, f.Bounds(low).Min.Y, 6);
        // The rest of the face: the plaster above the tiles only.
        var rest = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["face"] = "Face.Left", ["rest"] = true, ["finishType"] = plaster })!["id"]!;
        Assert.Equal(5 * (H - 1.2), Net(f, rest), 6);
        Assert.Equal(1.2, f.Bounds(rest).Min.Z, 6);
        Assert.Equal(5 * 1.2, Net(f, low), 6);

        // The eyedropper reads what is painted where it is pointed.
        var at = (JsonObject)f.Session.Query("architecture.finishFaceAt", new JsonObject { ["id"] = wall, ["point"] = P(1, Left, 0.3), ["normal"] = P(0, 1, 0) })!;
        Assert.Equal(low, (string?)at["zoneId"]);
        Assert.Equal(tiles, (string?)at["finishType"]);
        Assert.Equal(4, ((JsonArray)at["outline"]!).Count);

        // The wall moved a metre north and made a metre longer: the zones go with it, the tiles still to 1.2 m.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["y1"] = 1, ["y2"] = 1, ["x2"] = 6 });
        Assert.Equal(1 + Left, f.Bounds(low).Min.Y, 6);
        Assert.Equal(6 * 1.2, Net(f, low), 6);
        Assert.Equal(1.2, f.Bounds(low).Max.Z, 6);
        f.Session.Undo();
        Assert.Equal(Left, f.Bounds(low).Min.Y, 6);
        Assert.Equal(5 * 1.2, Net(f, low), 6);

        // Cleared below: the rest of the face takes it, plaster all the way up.
        f.Run("architecture.clearFinish", new JsonObject { ["hostId"] = wall, ["point"] = P(2.5, Left, 0.5), ["normal"] = P(0, 1, 0) });
        Assert.Null(f.Model.Get(low));
        Assert.Equal(5 * H, Net(f, rest), 6);
        // Cleared again there: a bare region in the rest, nothing built below the split.
        f.Run("architecture.clearFinish", new JsonObject { ["hostId"] = wall, ["point"] = P(2.5, Left, 0.5), ["normal"] = P(0, 1, 0) });
        Assert.Equal(5 * (H - 1.2), Net(f, rest), 6);
        Assert.Throws<CommandException>(() => f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["face"] = "Face.Left", ["whole"] = true }));

        // The wall deleted takes its zones and splits.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(wall) });
        Assert.Empty(f.Model.OfClass(FinishZones.Class));
        Assert.Empty(f.Model.OfClass(FinishZones.SplitClass));
    }

    [Fact]
    public void A_rectangle_drawn_on_a_face_is_a_region_of_its_own_taken_out_of_the_one_round_it()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0, height: H);
        // A 1 × 1 m panel on the right face (seen from outside, x runs from 5 to 0).
        f.Run("architecture.splitFinishFace", new JsonObject
        {
            ["hostId"] = wall, ["point"] = P(2, -Right, 1), ["normal"] = P(0, -1, 0), ["closed"] = true,
            ["points"] = new JsonArray(P(2, -Right, 1), P(3, -Right, 1), P(3, -Right, 2), P(2, -Right, 2)),
        });
        var stone = Library(f, "Stone cladding 30");
        var render = f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["face"] = "Face.Right", ["whole"] = true, ["layers"] = new JsonArray(new JsonObject { ["material"] = "Silicate render", ["thickness"] = 0.01 }) })!["id"]!.ToString();
        var panel = f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["point"] = P(2.5, -Right, 1.5), ["normal"] = P(0, -1, 0), ["finishType"] = stone })!["id"]!.ToString();
        Assert.Equal(1, Net(f, panel), 6);
        Assert.Equal(5 * H - 1, Net(f, render), 6);
        Assert.Equal((5 * H - 1) * 0.01, Volume(f, render), 6);
    }

    [Fact]
    public void A_floor_top_and_its_soffit_take_finishes_net_of_its_openings()
    {
        var f = new Fixture();
        var slab = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(4, 0), new JsonArray(4, 3), new JsonArray(0, 3)),
        })!["id"]!;
        f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["points"] = new JsonArray(new JsonArray(1, 1), new JsonArray(2, 1), new JsonArray(2, 2), new JsonArray(1, 2)) });
        var floor = Library(f, "Floor tiles");
        var top = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = slab, ["point"] = P(3, 2, 0), ["finishType"] = floor })!["id"]!;
        Assert.Equal("Top", f.Model.Get(top)!.Str("face"));
        Assert.Equal("FLOORING", f.Model.Get(top)!.PredefinedType);
        Assert.Equal(12 - 1, Net(f, top), 6);
        var slabTop = f.Bounds(slab).Max.Z;
        Assert.Equal(slabTop, f.Bounds(top).Min.Z, 6);
        Assert.Equal(slabTop + 0.015, f.Bounds(top).Max.Z, 6);
        // A room finish's ceiling under it: its layers from the slab down.
        var bathroom = (string)f.Run("architecture.roomFinishTypeFromLibrary", new JsonObject { ["name"] = "Bathroom" })!["id"]!;
        var ceiling = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = slab, ["face"] = "Soffit", ["whole"] = true, ["finishType"] = bathroom, ["part"] = "ceiling" })!["id"]!;
        Assert.Equal("CEILING", f.Model.Get(ceiling)!.PredefinedType);
        var soffit = f.Bounds(slab).Min.Z;
        Assert.Equal(soffit, f.Bounds(ceiling).Max.Z, 6);
        Assert.Equal(soffit - 0.011, f.Bounds(ceiling).Min.Z, 6);
        Assert.Equal(11 * 0.011, Volume(f, ceiling), 6);
        // The palette offers the room finish's parts and the library's surface finishes.
        var palette = (JsonArray)f.Session.Query("architecture.finishPalette", new JsonObject())!;
        Assert.Contains(palette.OfType<JsonObject>(), x => (string?)x["typeId"] == bathroom && (string?)x["part"] == "walls");
        Assert.Contains(palette.OfType<JsonObject>(), x => (string?)x["name"] == "ETICS EPS 100" && x["library"]?.GetValue<bool>() == true);
    }

    [Fact]
    public void Four_facades_painted_close_their_outer_corners_with_mitres()
    {
        var f = new Fixture();
        foreach (var ((x1, y1), (x2, y2)) in new[] { ((0.0, 0.0), (5.0, 0.0)), ((5.0, 0.0), (5.0, 4.0)), ((5.0, 4.0), (0.0, 4.0)), ((0.0, 4.0), (0.0, 0.0)) })
            f.Wall(x1, y1, x2, y2, height: H);
        var etics = Library(f, "ETICS EPS 100");
        var walls = f.Model.OfClass(IfcClass.Wall).ToList();
        foreach (var w in walls) f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = w.Id, ["face"] = "Face.Right", ["whole"] = true, ["finishType"] = etics });
        // The ring round the building, outside the render: nothing missing at the corners, nothing twice.
        double W = 5 + 2 * Right, D = 4 + 2 * Right;
        var ring = (W + 2 * Etics) * (D + 2 * Etics) - W * D;
        Assert.Equal(ring * H, walls.Sum(w => Zones(f, w.Id).Sum(z => Volume(f, z.Id))), 6);
        // Measured on the facade itself (the mitres are no area of it).
        Assert.Equal(2 * (W + D) * H, walls.Sum(w => Zones(f, w.Id).Sum(z => Net(f, z.Id))), 6);
    }
}
