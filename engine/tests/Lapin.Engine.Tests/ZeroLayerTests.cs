using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Host;
using Lapin.Package;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Layers of thickness 0 (decision 291, the tester's report #77: "layers with 0 thickness for materials that have a neglectable
/// thickness, like waterproofing, paint, extremely important to add to the BOQ and to the technical detail"): kept in the type,
/// building no solid, drawn as a line, measured by the area they cover, carried by IFC and the .lapin file.
/// </summary>
public sealed class ZeroLayerTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-zero-").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static JsonObject L(string material, double t, bool core = false) => new() { ["material"] = material, ["thickness"] = t, ["core"] = core };

    /// <summary>Paint 0 | render 20 | waterproofing 0 | brick 250 (core) | plaster 15 | paint 0; and the same without the zero layers.</summary>
    static (string With, string Without) WallTypes(Fixture f)
    {
        var with = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Membrane wall",
            ["layers"] = new JsonArray(L("Facade paint", 0), L("Render", 0.02), L("Waterproofing", 0), L("Brick", 0.25, true), L("Plaster", 0.015), L("Paint", 0)),
        })!["id"]!;
        var without = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Plain wall",
            ["layers"] = new JsonArray(L("Render", 0.02), L("Brick", 0.25, true), L("Plaster", 0.015)),
        })!["id"]!;
        return (with, without);
    }

    /// <summary>A 6 × 5 m box of walls with an inner wall (L corners, T junctions), a door and a window in the first.</summary>
    static string[] Box(Fixture f, string type, double y0 = 0)
    {
        var walls = new[] { f.Wall(0, y0, 6, y0, type), f.Wall(6, y0, 6, y0 + 5, type), f.Wall(6, y0 + 5, 0, y0 + 5, type), f.Wall(0, y0 + 5, 0, y0, type), f.Wall(3, y0, 3, y0 + 5, type) };
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["x"] = 1.5, ["y"] = y0 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = walls[0], ["x"] = 4.5, ["y"] = y0 });
        return walls;
    }

    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    static QuantitySet LayerSet(Fixture f, string id, int index) => f.Session.Quantities(id).Single(q => q.Part == $"layer:{index}");
    static double Val(QuantitySet s, string name) => s.Quantities.Single(q => q.Name == name).Value;

    [Fact]
    public void A_layer_of_thickness_0_is_kept_but_the_core_and_the_build_up_need_a_thickness()
    {
        var f = new Fixture();
        var (with, _) = WallTypes(f);
        var layers = LayerSets.LayersOf(f.Model, with);
        Assert.Equal([0, 0.02, 0, 0.25, 0.015, 0], layers.Select(l => l.Thickness));
        Assert.Equal(3, Layers.CoreIndex(layers));
        CommandException Refused(params JsonObject[] ls) => Assert.Throws<CommandException>(() => LayerSets.Parse(new JsonArray(ls)));
        Assert.Equal("The core needs a thickness.", Refused(L("Brick", 0, true), L("Plaster", 0.015)).Message);
        Assert.Contains("at least one layer with a thickness", Refused(L("Paint", 0), L("Waterproofing", 0)).Message);
        Assert.Equal("Every layer needs a thickness.", Refused(L("Paint", -0.001), L("Brick", 0.25, true)).Message);
        Assert.Equal("Every layer needs a thickness.", Refused(new JsonObject { ["material"] = "Paint" }, L("Brick", 0.25, true)).Message);
        // Floor types and room finishes take it as well.
        f.Run("architecture.createSlabType", new JsonObject { ["name"] = "F", ["layers"] = new JsonArray(L("Waterproofing", 0), L("Concrete", 0.15, true)) });
        Assert.Equal(0, RoomFinishSpec.From(new RoomFinishSpec("Painted", Walls: [new FinishLayer("Plaster", 0.01), new FinishLayer("Paint", 0)]).ToJson()).Walls![1].Thickness);
    }

    [Fact]
    public void Walls_with_zero_layers_build_the_same_solids_through_corners_junctions_and_openings()
    {
        var f = new Fixture();
        var (with, without) = WallTypes(f);
        var a = Box(f, with);
        var b = Box(f, without, 20);
        for (var i = 0; i < a.Length; i++)
        {
            var g = f.Session.Geometry(a[i])!;
            Assert.Null(g.Diagnostics);
            // No body for a layer of no thickness, nothing degenerate.
            Assert.DoesNotContain(g.Parts, p => p.Key is "layer:0" or "layer:2" or "layer:5");
            Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 1e-9 && !double.IsNaN(Measure.Volume(p.Solid)), p.Key));
            string Dump(string id) => string.Join(", ", f.Session.Geometry(id)!.Parts.Select(p => $"{p.Key}={Measure.Volume(p.Solid):0.000000}"));
            Assert.True(Math.Abs(Volume(f, b[i]) - Volume(f, a[i])) < 1e-9, $"{i}: {Dump(a[i])} | {Dump(b[i])}");
            Assert.Equal(f.Bounds(b[i]).Max.X, f.Bounds(a[i]).Max.X, 9);
        }
    }

    [Fact]
    public void Curved_zoned_and_partial_height_walls_and_strip_footings_take_zero_layers_too()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Structure.StructureModule());
        var (with, without) = WallTypes(f);
        string Arc(string type, double y)
        {
            var w = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = y, ["x2"] = 8, ["y2"] = y, ["arc"] = 2, ["typeId"] = type })!["id"]!;
            f.Run("architecture.addWindow", new JsonObject { ["wallId"] = w, ["along"] = 4 });
            return w;
        }
        Assert.Equal(Volume(f, Arc(without, 20)), Volume(f, Arc(with, 0)), 9);

        // A zoned core (masonry, ring beam) and a paint only up to 1.5 m, a waterproofing in pieces of two materials.
        var zones = new JsonArray(WallZones.Defaults("Brick").Select(z => (JsonNode)z.DeepClone()).ToArray());
        var zoned = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Zoned membranes", ["zones"] = zones,
            ["layers"] = new JsonArray(L("Render", 0.02),
                new JsonObject { ["material"] = "Bitumen", ["thickness"] = 0, ["parts"] = new JsonArray(new JsonObject { ["material"] = "Bitumen", ["height"] = 0.5 }, new JsonObject { ["material"] = "Membrane", ["height"] = 1 }) },
                L("Brick", 0.25, true), new JsonObject { ["material"] = "Dado paint", ["thickness"] = 0, ["to"] = 1.5 }, L("Plaster", 0.015)),
        })!["id"]!;
        var plainZoned = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Zoned plain", ["zones"] = zones.DeepClone(), ["layers"] = new JsonArray(L("Render", 0.02), L("Brick", 0.25, true), L("Plaster", 0.015)),
        })!["id"]!;
        var z = f.Wall(0, 40, 5, 40, zoned);
        Assert.Equal(Volume(f, f.Wall(0, 50, 5, 50, plainZoned)), Volume(f, z), 9);
        var sets = f.Session.Quantities(z);
        // Pieces in height: the bitumen to 0.5 m, the membrane 1 m over it, then the layer's own material (bitumen) to the top.
        var bitumen = sets.Single(q => q.Part == $"layer:1:{Materials.Find(f.Model, "Bitumen")!.Id}");
        var membrane = sets.Single(q => q.Part == $"layer:1:{Materials.Find(f.Model, "Membrane")!.Id}");
        // On the zoned core (measured by its zones' volumes): the wall's whole side, 5 × 3 m, no opening in it.
        const double core = 5 * 3;
        Assert.Equal(core, Val(bitumen, "NetSideArea") + Val(membrane, "NetSideArea"), 6);
        Assert.Equal(core / 3, Val(membrane, "NetSideArea"), 6);
        // The dado paint on the plaster, to 1.5 m of its 3.
        Assert.Equal(Val(LayerSet(f, z, 4), "NetSideArea") / 2, Val(LayerSet(f, z, 3), "NetSideArea"), 6);

        var strip = (string)f.Run("structure.createStripType", new JsonObject { ["name"] = "Tanked", ["layers"] = new JsonArray(L("Waterproofing", 0), L("Concrete", 0.3, true)) })!["id"]!;
        var footing = (string)f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 60, ["x2"] = 6, ["y2"] = 60, ["typeId"] = strip })!["id"]!;
        Assert.All(f.Session.Geometry(footing)!.Parts, p => Assert.True(Measure.Volume(p.Solid) > 1e-9, p.Key));
    }

    [Fact]
    public void A_zero_layer_is_measured_by_the_area_of_the_layer_it_lies_on_and_counted_in_the_takeoff()
    {
        var f = new Fixture();
        var (with, _) = WallTypes(f);
        var walls = Box(f, with);
        var w = walls[0];
        // The waterproofing lies on the brick (towards the core), the facade paint on the render, the inner paint on the plaster.
        var brick = LayerSet(f, w, 3);
        var waterproofing = LayerSet(f, w, 2);
        Assert.Equal(Val(brick, "NetSideArea"), Val(waterproofing, "NetSideArea"), 9);
        Assert.Equal(0, Val(waterproofing, "NetVolume"));
        Assert.Equal(0, Val(waterproofing, "Width"));
        Assert.Equal(Materials.Find(f.Model, "Waterproofing")!.Id, waterproofing.Material);
        Assert.Equal(Val(LayerSet(f, w, 1), "NetSideArea"), Val(LayerSet(f, w, 0), "NetSideArea"), 9);
        Assert.Equal(Val(LayerSet(f, w, 4), "NetSideArea"), Val(LayerSet(f, w, 5), "NetSideArea"), 9);
        // Net of the openings: less than the wall's whole side.
        Assert.True(Val(waterproofing, "NetSideArea") < 6 * 3 - 1, $"{Val(waterproofing, "NetSideArea")}");
        // The wall's own quantities are those of the wall without them.
        Assert.Equal(0.285, Val(f.Session.Quantities(w).Single(s => s.Name == "Qto_WallBaseQuantities"), "Width"), 9);

        // The takeoff by material: the waterproofing with its area, no volume.
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "quantities" })!["id"]!;
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["groupBy"] = new JsonArray("material"), ["classes"] = new JsonArray(IfcClass.Wall) });
        var grid = new QuantityTakeoff().Grid(f.Session.Reader, f.Model.Get(id)!);
        var row = grid.Rows.Single(r => r[0] == "Waterproofing");
        string Cell(string column) => row[grid.Columns.ToList().IndexOf(column)];
        var total = walls.Sum(x => Val(LayerSet(f, x, 2), "NetSideArea"));
        Assert.Equal(total.ToString("0.00", CultureInfo.InvariantCulture), Cell("Net area (m²)"));
        Assert.Equal("5", Cell("Count"));
        Assert.Contains(grid.Rows, r => r[0] == "Paint");
        Assert.Contains(grid.Rows, r => r[0] == "Facade paint");
    }

    [Fact]
    public void A_floor_with_zero_layers_builds_the_same_slab_and_measures_them_by_its_area()
    {
        var f = new Fixture();
        var with = (string)f.Run("architecture.createSlabType", new JsonObject { ["name"] = "Membrane floor", ["layers"] = new JsonArray(L("Paint", 0), L("Screed", 0.05), L("Waterproofing", 0), L("Reinforced concrete", 0.15, true), L("Ceiling paint", 0)) })!["id"]!;
        var without = (string)f.Run("architecture.createSlabType", new JsonObject { ["name"] = "Plain floor", ["layers"] = new JsonArray(L("Screed", 0.05), L("Reinforced concrete", 0.15, true)) })!["id"]!;
        string Slab(string type, double x0) => (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x0, ["y1"] = 0, ["x2"] = x0 + 6, ["y2"] = 5, ["typeId"] = type })!["id"]!;
        var a = Slab(with, 0);
        var b = Slab(without, 20);
        Assert.Equal(2, f.Session.Geometry(a)!.Parts.Count);
        Assert.Equal(Volume(f, b), Volume(f, a), 9);
        Assert.Equal(f.Bounds(b).Min.Z, f.Bounds(a).Min.Z, 9);
        Assert.Equal(30, Val(LayerSet(f, a, 2), "NetArea"), 6);
        Assert.Equal(30, Val(LayerSet(f, a, 0), "NetArea"), 6);
        Assert.Equal(30, Val(LayerSet(f, a, 4), "NetArea"), 6);
        Assert.Equal(0, Val(LayerSet(f, a, 2), "NetVolume"));
        Assert.Equal(0.2, Val(f.Session.Quantities(a).Single(s => s.Name == "Qto_SlabBaseQuantities"), "Width"), 9);
    }

    [Fact]
    public void A_zero_layer_inside_the_wall_is_drawn_as_a_membrane_line_in_plan_and_in_the_build_up()
    {
        var f = new Fixture();
        var (with, _) = WallTypes(f);
        f.Wall(0, 0, 6, 0, with);
        var plan = Views.Render(new Q(f), Views.Find(f.Model, "plan:" + f.Storey)!, print: true);
        Assert.Null(plan.Problem);
        // The layers run from the right face (−y for a wall drawn along +x): render −0.145 … −0.125, the waterproofing at −0.125, the brick's face.
        var membrane = plan.Pens.Where(p => p.Layer == PenLayer.Membrane).SelectMany(p => p.Segments).ToList();
        Assert.Contains(membrane, s => Math.Abs(s.A.Y + 0.125) < 1e-6 && Math.Abs(s.B.Y + 0.125) < 1e-6 && Math.Abs(s.B.X - s.A.X) > 5);

        // The type assemblies' build-up: the zero layers named like the others, the waterproofing a dashed line where it lies.
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "types" })!["id"]!;
        var layout = (JsonObject)f.Session.Query("schedule.layout", new JsonObject { ["id"] = id })!;
        var texts = ((JsonArray)layout["pens"]!).SelectMany(p => p!["texts"] as JsonArray ?? []).Select(t => (string)t!["text"]!).ToList();
        Assert.Contains("3  Waterproofing  0", texts);
        Assert.Contains("1  Facade paint  0", texts);
        Assert.Contains("285", texts);
        var bands = TypeBuildUps.Bands(f.Model);
        Assert.Contains(bands.SelectMany(b => b.Pens), p => p.Dash != null && p.Segments.Count == 1);
        // The table: its thickness 0.
        var table = new TypeAssemblies().Grid(new Q(f));
        Assert.Contains(table.Rows, r => r[4] == "Waterproofing" && r[5] == "0");
    }

    [Fact]
    public void A_zero_layer_goes_through_IFC_and_the_lapin_file()
    {
        var f = new Fixture();
        var (with, _) = WallTypes(f);
        var wall = f.Wall(0, 0, 6, 0, with);
        // IFC4: IfcMaterialLayer.LayerThickness is an IfcNonNegativeLengthMeasure, 0 written as it is.
        var ifc = Path.Combine(_dir, "zero.ifc");
        IfcExport.Write(f.Session, ifc);
        var file = StepFile.Read(ifc);
        var written = file.OfType("IFCMATERIALLAYER").Where(l => file.Get(l[0]) is { } mat && (string?)mat[0] == "Waterproofing").ToList();
        Assert.NotEmpty(written);
        Assert.All(written, l => Assert.Equal(0.0, Convert.ToDouble(l[1], CultureInfo.InvariantCulture)));
        var g = new Fixture();
        IfcImport.Run(g.Session, ifc, null, out _);
        var back = LayerSets.LayersOf(g.Model, with);
        Assert.Equal([0, 0.02, 0, 0.25, 0.015, 0], back.Select(l => Math.Round(l.Thickness, 9)));
        Assert.Equal(Volume(f, wall), Volume(g, wall), 6);

        var path = Path.Combine(_dir, "zero.lapin");
        PackageFiles.Create().Save(f.Session, path);
        var h = new Fixture();
        PackageFiles.Open(h.Session, path);
        Assert.Equal([0, 0.02, 0, 0.25, 0.015, 0], LayerSets.LayersOf(h.Model, with).Select(l => l.Thickness));
        Assert.Equal(Val(LayerSet(f, wall, 2), "NetSideArea"), Val(LayerSet(h, wall, 2), "NetSideArea"), 9);
    }

    [Fact]
    public void A_room_finish_layer_of_thickness_0_builds_nothing_and_is_measured_by_its_area()
    {
        var f = new Fixture();
        var walls = new[] { ((0.0, 0.0), (5.0, 0.0)), ((5.0, 0.0), (5.0, 4.0)), ((5.0, 4.0), (0.0, 4.0)), ((0.0, 4.0), (0.0, 0.0)) }
            .Select(x => f.Wall(x.Item1.Item1, x.Item1.Item2, x.Item2.Item1, x.Item2.Item2)).ToArray();
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["x"] = 2, ["y"] = 0 });
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        var spec = new RoomFinishSpec("Painted", Floor: [new FinishLayer("Floor paint", 0), new FinishLayer("Screed", 0.05)],
            Walls: [new FinishLayer("Plaster", 0.01), new FinishLayer("Paint", 0)], Ceiling: [new FinishLayer("Ceiling paint", 0)]);
        var type = (string)f.Run("architecture.createRoomFinishType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomId"] = room, ["typeId"] = type });
        var finishes = f.Model.OfClass(RoomFinishes.Class).Where(e => e.Str("roomId") == room).ToDictionary(e => e.Str("part")!);
        RoomFinishes.FinishMeasure M(string part, string material) => RoomFinishes.Measures(f.Session.Geometry(finishes[part].Id)).Single(x => x.Material == material);
        foreach (var e in finishes.Values)
            Assert.All(f.Session.Geometry(e.Id)!.Parts, p => Assert.True(Measure.Volume(p.Solid) > 1e-9, p.Key));
        Assert.DoesNotContain(f.Session.Geometry(finishes["walls"].Id)!.Parts, p => p.Key == "walls:1");
        var paint = M("walls", "Paint");
        var plaster = M("walls", "Plaster");
        Assert.Equal(0, paint.Volume);
        Assert.Equal(plaster.Gross, paint.Gross, 6);
        Assert.Equal(plaster.Net, paint.Net, 6);
        Assert.True(paint.Net < paint.Gross, "the door is taken out");
        var floorPaint = M("floor", "Floor paint");
        Assert.Equal(M("floor", "Screed").Net, floorPaint.Net, 6);
        // A ceiling of paint alone: its own area.
        Assert.Equal(M("floor", "Screed").Gross, M("ceiling", "Ceiling paint").Net, 6);
        Assert.Empty(f.Session.Geometry(finishes["ceiling"].Id)!.Parts);
    }
}
