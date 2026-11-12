using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Walls through the floors' layers by priority (the forum's topic #30, Poland; as Revit's layer functions): a wall layer may
/// extend down, from the wall's base through the layers of the floor it stands on ranked under it, to the top of the first
/// ranked as high or higher (the brick core on the concrete, through the finish and the screed), and up, through the floor
/// above's lower layers, to the underside of the first as high. The floor loses what the wall takes. Priorities are the
/// layers' own (0–100), else their material's in the cut order, else 50 for a core and 0 for the rest, as where walls join.
/// Off for every layer unless its type says so, so a project saved before draws as it did; IFC keeps the layer sets and
/// carries the choice on each IfcMaterialLayer (Lapin_LayerJoins), the bodies built as they are.
/// </summary>
public sealed class WallFloorLayerJoinTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-wall-floor").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    const double Area = 7 * 4, Length = 5, Brick = 0.25;

    static JsonObject L(string material, double thickness, bool core = false, bool down = false, bool up = false, int? priority = null)
    {
        var o = new JsonObject { ["material"] = material, ["thickness"] = thickness, ["core"] = core };
        if (down) o["extendDown"] = true;
        if (up) o["extendUp"] = true;
        if (priority != null) o["priority"] = priority;
        return o;
    }

    static string WallType(Fixture f, params JsonObject[] layers) =>
        (string)f.Run("architecture.createWallType", new JsonObject { ["name"] = "Plastered brick", ["layers"] = new JsonArray(layers.Select(l => (JsonNode)l).ToArray()) })!["id"]!;

    static void Relayer(Fixture f, string type, params JsonObject[] layers) =>
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = type, ["layers"] = new JsonArray(layers.Select(l => (JsonNode)l).ToArray()) });

    static JsonArray Outline() => new(new JsonArray(-1, -2), new JsonArray(6, -2), new JsonArray(6, 2), new JsonArray(-1, 2));

    /// <summary>A floor of the default type (finish 20, screed 50, concrete 150 core) at the storey's level; a free wall on it along x from 0 to 5.</summary>
    static (Fixture F, string Type, string Wall, string Slab) OnFloor(bool coreDown)
    {
        var f = new Fixture();
        var type = WallType(f, L("Plaster", 0.015), L("Brick", Brick, true, down: coreDown), L("Plaster", 0.015));
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Outline() })!["id"]!;
        var wall = f.Wall(0, 0, Length, 0, type);
        return (f, type, wall, slab);
    }

    static List<GeometryPart> Parts(Fixture f, string id, string key) => f.Session.Geometry(id)!.Parts.Where(p => p.Key == key).ToList();
    static double Volume(Fixture f, string id, string key) => Parts(f, id, key).Sum(p => Measure.Volume(p.Solid));
    static double Bottom(Fixture f, string id, string key) => Parts(f, id, key).SelectMany(p => p.Solid.Vertices).Min(v => v.Z);
    static double Top(Fixture f, string id, string key) => Parts(f, id, key).SelectMany(p => p.Solid.Vertices).Max(v => v.Z);
    static double Measured(Fixture f, string id, string key) => f.Session.Quantities(id).Single(q => q.Part == key).Quantities.Single(x => x.Name == "NetVolume").Value;

    [Fact]
    public void Off_the_wall_stands_on_the_floors_top()
    {
        var (f, _, wall, slab) = OnFloor(coreDown: false);
        Assert.Equal(0, Bottom(f, wall, "layer:1"), 6);
        Assert.Equal(Area * 0.02, Volume(f, slab, "layer:0"), 6);
        Assert.Equal(Area * 0.05, Volume(f, slab, "layer:1"), 6);
    }

    [Fact]
    public void The_core_goes_down_through_the_finish_and_the_screed_to_the_concrete()
    {
        var (f, _, wall, slab) = OnFloor(coreDown: true);
        // The brick from the concrete's top (−0.07) up; the plasters still on the floor's top.
        Assert.Equal(-0.07, Bottom(f, wall, "layer:1"), 6);
        Assert.Equal(0, Bottom(f, wall, "layer:0"), 6);
        Assert.Equal(0, Bottom(f, wall, "layer:2"), 6);
        Assert.Equal(Length * Brick * (3 + 0.07), Volume(f, wall, "layer:1"), 6);
        // The floor's finish and screed lose the brick's footprint; the concrete keeps all of itself.
        Assert.Equal(Area * 0.02 - Length * Brick * 0.02, Volume(f, slab, "layer:0"), 6);
        Assert.Equal(Area * 0.05 - Length * Brick * 0.05, Volume(f, slab, "layer:1"), 6);
        Assert.Equal(Area * 0.15, Volume(f, slab, "layer:2"), 6);
        // The quantities follow the bodies.
        Assert.Equal(Volume(f, wall, "layer:1"), Measured(f, wall, "layer:1"), 6);
        Assert.Equal(Volume(f, slab, "layer:1"), Measured(f, slab, "layer:1"), 6);
    }

    [Fact]
    public void A_layer_stops_at_the_first_floor_layer_ranked_as_high()
    {
        var (f, type, wall, slab) = OnFloor(coreDown: true);
        // The screed ranked as the core: the brick stops on it, through the finish alone.
        f.Run("architecture.updateSlabType", new JsonObject
        {
            ["id"] = f.Model.RelatingOf(slab, IfcClass.RelDefinesByType)!.Id,
            ["layers"] = new JsonArray(L("Floor finish", 0.02), L("Screed", 0.05, priority: 50), L("Reinforced concrete", 0.15, true)),
        });
        Assert.Equal(-0.02, Bottom(f, wall, "layer:1"), 6);
        Assert.Equal(Area * 0.05, Volume(f, slab, "layer:1"), 6);
        // A plaster ranked 10 going down: through the finish (0), onto the screed (50).
        Relayer(f, type, L("Plaster", 0.015, down: true, priority: 10), L("Brick", Brick, true, down: true), L("Plaster", 0.015));
        Assert.Equal(-0.02, Bottom(f, wall, "layer:0"), 6);
        Assert.Equal(0, Bottom(f, wall, "layer:2"), 6);
        // A plaster of rank 0 going down stops at once: the finish is as high.
        Relayer(f, type, L("Plaster", 0.015, down: true), L("Brick", Brick, true, down: true), L("Plaster", 0.015));
        Assert.Equal(0, Bottom(f, wall, "layer:0"), 6);
    }

    [Fact]
    public void Up_the_core_goes_through_the_ceilings_plaster_to_the_concrete_above()
    {
        var f = new Fixture();
        var type = WallType(f, L("Plaster", 0.015), L("Brick", Brick, true, up: true), L("Plaster", 0.015));
        // The floor above: concrete 150 over a ceiling plaster of 10, its top at 3.00, its underside at 2.84.
        var above = (string)f.Run("architecture.createSlabType", new JsonObject { ["name"] = "Plastered slab", ["layers"] = new JsonArray(L("Reinforced concrete", 0.15, true), L("Ceiling plaster", 0.01)) })!["id"]!;
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Outline(), ["offset"] = 3.0, ["typeId"] = above })!["id"]!;
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = Length, ["y2"] = 0, ["typeId"] = type, ["height"] = 2.84 })!["id"]!;
        Assert.Equal(2.85, Top(f, wall, "layer:1"), 6);
        Assert.Equal(2.84, Top(f, wall, "layer:0"), 6);
        Assert.Equal(Area * 0.01 - Length * Brick * 0.01, Volume(f, slab, "layer:1"), 6);
        Assert.Equal(Area * 0.15, Volume(f, slab, "layer:0"), 6);
        // A wall built up into the floor (its top at 3.00): cut at the floor's underside as before, the core back to the concrete.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["height"] = 3.0 });
        Assert.Equal(2.85, Top(f, wall, "layer:1"), 6);
        Assert.Equal(2.84, Top(f, wall, "layer:0"), 6);
    }

    [Fact]
    public void A_section_shows_the_brick_on_the_concrete()
    {
        var (f, _, wall, slab) = OnFloor(coreDown: true);
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(2.5, -1), ["end"] = new JsonArray(2.5, 1) })!["id"]!;
        var (d, _, problem) = Views.Generate(new Q(f), Views.Find(f.Model, section)!);
        Assert.True(d != null, problem);
        var core = d!.Cuts.Where(c => c.Element == wall && c.Key == "layer:1").SelectMany(c => c.Loops.SelectMany(l => l)).ToList();
        Assert.Equal(-0.07, core.Min(p => p.Y), 6);
        // One brick from the concrete up: no line across it at the floor's top.
        var (cx0, cx1) = (core.Min(p => p.X), core.Max(p => p.X));
        var r = Views.Render(new Q(f), Views.Find(f.Model, section)!, print: true);
        Assert.DoesNotContain(r.Pens.Where(p => p.Layer is PenLayer.Cut or PenLayer.Seen).SelectMany(p => p.Segments),
            s => Math.Abs(s.A.Y) < 1e-6 && Math.Abs(s.B.Y) < 1e-6 && Math.Min(s.A.X, s.B.X) > cx0 - 1e-6 && Math.Max(s.A.X, s.B.X) < cx1 + 1e-6 && Math.Abs(s.A.X - s.B.X) > 0.01);
        // The screed's cut stops at the brick's faces.
        // The screed's cut stops at the brick's faces: two pieces, one each side.
        Assert.Equal(2, d.Cuts.Where(c => c.Element == slab && c.Key == "layer:1").Sum(c => c.Loops.Count));
    }

    [Fact]
    public void The_choice_is_kept_in_the_type_and_goes_to_IFC_and_the_package_and_back()
    {
        var (f, type, _, _) = OnFloor(coreDown: true);
        Relayer(f, type, L("Plaster", 0.015), L("Brick", Brick, true, down: true, up: true), L("Plaster", 0.015));
        Assert.Equal(new bool?[] { null, true, null }, LayerSets.LayersOf(f.Model, type).Select(l => l.ExtendDown));
        Assert.Equal(new bool?[] { null, true, null }, LayerSets.LayersOf(f.Model, type).Select(l => l.ExtendUp));
        var ifc = Path.Combine(_dir, "walls.ifc");
        IfcExport.Write(f.Session, ifc, null);
        var text = File.ReadAllText(ifc);
        Assert.Contains("'Lapin_LayerJoins'", text);
        Assert.Contains("'ExtendToFloorBelow'", text);
        // The layer set stays a layer set, its usage the wall's.
        Assert.Contains("IFCMATERIALLAYERSETUSAGE", text);
        var b = new Fixture();
        IfcImport.Run(b.Session, ifc, null, out _);
        var back = b.Model.OfClass(IfcClass.WallType).Single(t => t.Name == "Plastered brick");
        Assert.Equal(new bool?[] { null, true, null }, LayerSets.LayersOf(b.Model, back.Id).Select(l => l.ExtendDown));
        Assert.Equal(new bool?[] { null, true, null }, LayerSets.LayersOf(b.Model, back.Id).Select(l => l.ExtendUp));
        var lapin = Path.Combine(_dir, "walls.lapin");
        Lapin.Host.PackageFiles.Save(f.Session, lapin);
        var c = new Fixture();
        Lapin.Host.PackageFiles.Open(c.Session, lapin);
        Assert.Equal(new bool?[] { null, true, null }, LayerSets.LayersOf(c.Model, type).Select(l => l.ExtendDown));
    }
}
