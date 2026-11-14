using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A layer reaching past an edge of its wall or floor, or stopping short of it (forum topic 89, docs/plan-layer-extents.md):
/// the element keeps its type's layer set; each extent is the element's own (layerExtents, in Lapin_Intent). A wall's layer
/// at its start, end, bottom or top; a floor's all round its outline or at one edge. What is past the edge is a piece of the
/// layer's own, one body with it, that overlaps what it meets or, set to cut, is taken out of the walls and floors it meets.
/// </summary>
public sealed class LayerExtentTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-layer-extents").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    const double Length = 5, Height = 3, Plaster = 0.015, Brick = 0.25;

    static JsonObject L(string material, double thickness, bool core = false) => new() { ["material"] = material, ["thickness"] = thickness, ["core"] = core };

    static string WallType(Fixture f) =>
        (string)f.Run("architecture.createWallType", new JsonObject { ["name"] = "Plastered brick", ["layers"] = new JsonArray(L("Plaster", Plaster), L("Brick", Brick, true), L("Plaster", Plaster)) })!["id"]!;

    static JsonArray Outline() => new(new JsonArray(-1, -2), new JsonArray(6, -2), new JsonArray(6, 2), new JsonArray(-1, 2));

    static List<GeometryPart> Parts(Fixture f, string id, string key) => f.Session.Geometry(id)!.Parts.Where(p => p.Key == key).ToList();
    static double Volume(Fixture f, string id, string key) => Parts(f, id, key).Sum(p => Measure.Volume(p.Solid));
    static double All(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static double Bottom(Fixture f, string id, string key) => Parts(f, id, key).SelectMany(p => p.Solid.Vertices).Min(v => v.Z);
    static double Top(Fixture f, string id, string key) => Parts(f, id, key).SelectMany(p => p.Solid.Vertices).Max(v => v.Z);
    static double Measured(Fixture f, string id, string key) => f.Session.Quantities(id).Single(q => q.Part == key).Quantities.Single(x => x.Name == "NetVolume").Value;

    static void Set(Fixture f, string id, int layer, string edge, double offset, bool? cuts = null, int? index = null)
    {
        var a = new JsonObject { ["id"] = id, ["layer"] = layer, ["edge"] = edge, ["offset"] = offset };
        if (cuts != null) a["cuts"] = cuts;
        if (index != null) a["index"] = index;
        f.Run("architecture.setLayerExtent", a);
    }

    /// <summary>A free wall along x from 0 to 5, 3 m high: plaster, brick (core), plaster.</summary>
    static (Fixture F, string Wall) FreeWall()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, Length, 0, WallType(f), height: Height);
        return (f, wall);
    }

    [Fact]
    public void A_wall_without_extents_builds_as_before_and_one_layer_goes_down_past_its_base()
    {
        var (f, wall) = FreeWall();
        Assert.Null(f.Model.Get(wall)!.Attrs[LayerExtents.Key]);
        Assert.Equal(0, Bottom(f, wall, "layer:0"), 6);
        Assert.Equal(Length * Height * Plaster, Volume(f, wall, "layer:0"), 6);
        Set(f, wall, 0, "bottom", 0.02);
        Assert.Equal(-0.02, Bottom(f, wall, "layer:0"), 6);
        // One body: the piece below is joined to the layer.
        Assert.Single(Parts(f, wall, "layer:0"));
        Assert.Equal(Length * (Height + 0.02) * Plaster, Volume(f, wall, "layer:0"), 6);
        Assert.Equal(Length * (Height + 0.02) * Plaster, Measured(f, wall, "layer:0"), 6);
        // The other layers do not move.
        Assert.Equal(0, Bottom(f, wall, "layer:1"), 6);
        Assert.Equal(0, Bottom(f, wall, "layer:2"), 6);
    }

    [Fact]
    public void A_layer_stops_short_of_the_top_and_reaches_past_an_end()
    {
        var (f, wall) = FreeWall();
        Set(f, wall, 2, "top", -0.10);
        Assert.Equal(Height - 0.10, Top(f, wall, "layer:2"), 6);
        Assert.Equal(Height, Top(f, wall, "layer:1"), 6);
        Set(f, wall, 2, "end", 0.05);
        var b = Parts(f, wall, "layer:2").SelectMany(p => p.Solid.Vertices).ToList();
        Assert.Equal(Length + 0.05, b.Max(v => v.X), 6);
        Assert.Equal(0, b.Min(v => v.X), 6);
        // The strip past the end goes the layer's own height (short of the top by 10 cm).
        Assert.Equal(Height - 0.10, b.Max(v => v.Z), 6);
        Assert.Equal((Length + 0.05) * (Height - 0.10) * Plaster, Volume(f, wall, "layer:2"), 6);
        // In plan the layer's cut runs on past the end too.
        var plan = f.Session.Geometry(wall)!.Plan!.Where(p => p.Kind == "cut").SelectMany(p => p.Points).ToList();
        Assert.Equal(Length + 0.05, plan.Max(p => p.X), 6);
        // Short of the start.
        Set(f, wall, 1, "start", -0.10);
        Assert.Equal(0.10, Parts(f, wall, "layer:1").SelectMany(p => p.Solid.Vertices).Min(v => v.X), 6);
    }

    [Fact]
    public void Each_setting_is_one_undo_step_and_zero_takes_it_away()
    {
        var (f, wall) = FreeWall();
        Set(f, wall, 0, "bottom", 0.02);
        Set(f, wall, 0, "top", 0.03);
        Assert.Equal(2, LayerExtents.Of(f.Model.Get(wall)!).Count);
        f.Session.Undo();
        Assert.Single(LayerExtents.Of(f.Model.Get(wall)!));
        Assert.Equal(Height, Top(f, wall, "layer:0"), 6);
        Set(f, wall, 0, "bottom", 0);
        Assert.Null(f.Model.Get(wall)!.Attrs[LayerExtents.Key]);
        Assert.Equal(0, Bottom(f, wall, "layer:0"), 6);
        Set(f, wall, 0, "bottom", 0.02);
        f.Run("architecture.clearLayerExtents", new JsonObject { ["id"] = wall });
        Assert.Null(f.Model.Get(wall)!.Attrs[LayerExtents.Key]);
    }

    [Fact]
    public void Refused_where_it_would_not_build()
    {
        var (f, wall) = FreeWall();
        Assert.Throws<CommandException>(() => Set(f, wall, 5, "bottom", 0.02));
        Assert.Throws<CommandException>(() => Set(f, wall, 0, "outline", 0.02));
        Assert.Throws<CommandException>(() => Set(f, wall, 0, "bottom", 2));
        // Short of both ends by more than the wall is long.
        var shortWall = f.Wall(0, 3, 1.5, 3, WallType(f), height: Height);
        Set(f, shortWall, 0, "start", -0.8);
        Assert.Throws<CommandException>(() => Set(f, shortWall, 0, "end", -0.8));
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Outline() })!["id"]!;
        Assert.Throws<CommandException>(() => Set(f, slab, 0, "top", 0.02));
        Assert.Throws<CommandException>(() => Set(f, slab, 0, "edge", 0.02, index: 4));
        // Short by more than half a narrow floor is wide: its outline would turn inside out.
        var narrow = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 5), new JsonArray(7, 5), new JsonArray(7, 6.5), new JsonArray(0, 6.5)) })!["id"]!;
        Assert.Throws<CommandException>(() => Set(f, narrow, 0, "outline", -0.8));
        Assert.Null(f.Model.Get(narrow)!.Attrs[LayerExtents.Key]);
    }

    [Fact]
    public void A_floors_finish_goes_past_its_outline_all_round_and_at_one_edge()
    {
        var f = new Fixture();
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Outline() })!["id"]!;
        Assert.Single(f.Session.Geometry(slab)!.Plan!);
        Set(f, slab, 0, "outline", 0.02);
        Assert.Equal(7.04 * 4.04 * 0.02, Volume(f, slab, "layer:0"), 6);
        Assert.Single(Parts(f, slab, "layer:0"));
        Assert.Equal(7 * 4 * 0.05, Volume(f, slab, "layer:1"), 6);
        Assert.Equal(7.04 * 4.04 * 0.02, Measured(f, slab, "layer:0"), 6);
        // In plan, the finish's outline beside the floor's.
        Assert.Equal(2, f.Session.Geometry(slab)!.Plan!.Count);
        // One edge's own over the outline's: the top edge (corner 2 to 3) out by 5 cm, the rest stays at 2.
        Set(f, slab, 0, "edge", 0.05, index: 2);
        Assert.Equal(7.04 * 4.07 * 0.02, Volume(f, slab, "layer:0"), 6);
        // Short all round: inside the outline.
        Set(f, slab, 1, "outline", -0.10);
        Assert.Equal(6.8 * 3.8 * 0.05, Volume(f, slab, "layer:1"), 6);
    }

    [Fact]
    public void A_floors_edge_extent_follows_its_edge_when_a_corner_is_added()
    {
        var f = new Fixture();
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Outline() })!["id"]!;
        Set(f, slab, 0, "edge", 0.05, index: 2);
        // A corner on the bottom edge: the top edge is the fourth now.
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = new JsonArray(new JsonArray(-1, -2), new JsonArray(2, -2), new JsonArray(6, -2), new JsonArray(6, 2), new JsonArray(-1, 2)) });
        Assert.Equal(3, LayerExtents.Of(f.Model.Get(slab)!).Single().Index);
        Assert.Equal(7 * 4.05 * 0.02, Volume(f, slab, "layer:0"), 6);
        // The edge taken away: its extent goes with it.
        f.Run("architecture.updateSlab", new JsonObject { ["id"] = slab, ["points"] = new JsonArray(new JsonArray(-1, -2), new JsonArray(6, -2), new JsonArray(-1, 2)) });
        Assert.Null(f.Model.Get(slab)!.Attrs[LayerExtents.Key]);
    }

    /// <summary>A floor at the storey's level; a wall beside it on its top edge (its right face on y = 2), standing on the floor's underside.</summary>
    static (Fixture F, string Slab, string Wall) Beside()
    {
        var f = new Fixture();
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Outline() })!["id"]!;
        var half = Plaster + Brick / 2;
        var wall = f.Wall(0, 2 + half, Length, 2 + half, WallType(f), height: Height);
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["baseOffset"] = -0.22 });
        return (f, slab, wall);
    }

    [Fact]
    public void Past_the_edge_it_overlaps_or_cuts_what_it_meets()
    {
        var (f, slab, wall) = Beside();
        var wallBefore = All(f, wall);
        // The finish 5 cm into the wall: overlapping it, the wall keeps its volume.
        Set(f, slab, 0, "edge", 0.05, index: 2);
        Assert.Equal(7 * 4.05 * 0.02, Volume(f, slab, "layer:0"), 6);
        Assert.Equal(wallBefore, All(f, wall), 6);
        // Cutting it, the wall loses where the finish goes, along its length (0 to 5).
        Set(f, slab, 0, "edge", 0.05, cuts: true, index: 2);
        Assert.Equal(wallBefore - Length * 0.05 * 0.02, All(f, wall), 6);
        Assert.Equal(7 * 4.05 * 0.02, Volume(f, slab, "layer:0"), 6);
        // The cut kept when only the offset changes.
        Set(f, slab, 0, "edge", 0.04, index: 2);
        Assert.Equal(wallBefore - Length * 0.04 * 0.02, All(f, wall), 6);
        // Undone: the wall whole again.
        f.Session.Undo();
        f.Session.Undo();
        Assert.Equal(wallBefore, All(f, wall), 6);
    }

    [Fact]
    public void A_walls_layer_going_down_cuts_the_floor_it_reaches()
    {
        var (f, slab, wall) = Beside();
        var finish = Volume(f, slab, "layer:0");
        // The wall's right plaster 0.5 m past its start, beside the floor: it reaches nothing of it.
        Set(f, wall, 0, "start", 0.5, cuts: true);
        Assert.Equal(finish, Volume(f, slab, "layer:0"), 6);
        // The wall on the floor's top, its right plaster 2 cm down past its base.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["baseOffset"] = 0.0 });
        Set(f, wall, 0, "bottom", 0.02, cuts: true);
        // The plaster's footprint (y 2.0 to 2.015) lies outside the floor: nothing taken. Move the wall 1 cm in.
        var half = Plaster + Brick / 2;
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["y1"] = 2 + half - 0.01, ["y2"] = 2 + half - 0.01 });
        // The plaster over the floor for 1 cm of its 1.5, along 5 m and the 0.5 m past the start, 2 cm deep: out of the finish.
        Assert.Equal(finish - (Length + 0.5) * 0.01 * 0.02, Volume(f, slab, "layer:0"), 6);
    }

    [Fact]
    public void A_section_draws_the_finish_past_the_edge_and_IFC_and_the_package_keep_it()
    {
        var (f, slab, wall) = Beside();
        Set(f, slab, 0, "edge", 0.05, cuts: true, index: 2);
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(2.5, -3), ["end"] = new JsonArray(2.5, 3) })!["id"]!;
        var (d, _, problem) = Views.Generate(new Q(f), Views.Find(f.Model, section)!);
        Assert.True(d != null, problem);
        var finish = d!.Cuts.Where(c => c.Element == slab && c.Key == "layer:0").SelectMany(c => c.Loops.SelectMany(l => l)).ToList();
        // Across the section (its x along the cut line from y = -3): the finish from the floor's edge at -2 to 2.05.
        Assert.Equal(4.05, finish.Max(p => p.X) - finish.Min(p => p.X), 6);
        var ifc = Path.Combine(_dir, "extents.ifc");
        IfcExport.Write(f.Session, ifc, null);
        var text = File.ReadAllText(ifc);
        Assert.Contains("'layerExtents'", text);
        Assert.Contains("IFCMATERIALLAYERSETUSAGE", text);
        var b = new Fixture();
        IfcImport.Run(b.Session, ifc, null, out _);
        var back = b.Model.OfClass(IfcClass.Slab).Single();
        Assert.Equal(LayerExtents.Of(f.Model.Get(slab)!), LayerExtents.Of(back));
        var lapin = Path.Combine(_dir, "extents.lapin");
        Lapin.Host.PackageFiles.Save(f.Session, lapin);
        var c = new Fixture();
        Lapin.Host.PackageFiles.Open(c.Session, lapin);
        Assert.Equal(LayerExtents.Of(f.Model.Get(slab)!), LayerExtents.Of(c.Model.Get(slab)!));
        Assert.Equal(All(f, wall), c.Session.Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid)), 6);
    }

    [Fact]
    public void Curved_walls_are_refused_for_now()
    {
        var (f, wall) = FreeWall();
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, [PathCurve.Key] = 0.5 });
        Assert.Throws<CommandException>(() => Set(f, wall, 0, "bottom", 0.02));
    }
}
