using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Balconies;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// What hangs on a wall or a floor goes to the part it falls on when the element is split or trimmed (audit 2026-11-15 §15):
/// finishes painted on faces, sunshades, balconies, a ceiling's fixtures, layer extents on curved edges, formwork. The areas
/// of the parts add up to the element's; one undo step.
/// </summary>
public sealed class HostedSplitTests
{
    const double Left = 0.125 + 0.015, H = 3.0;
    static JsonArray P(double x, double y) => new(x, y);
    static JsonArray P(double x, double y, double z) => new(x, y, z);
    static string[] Ids(JsonNode? r) => [.. ((JsonArray)r!["ids"]!).Select(n => (string)n!)];

    static double Net(Fixture f, string id) =>
        f.Session.Quantities(id).Single(q => q.Name == "Qto_CoveringBaseQuantities").Quantities.Single(q => q.Name == "NetArea").Value;

    static List<Entity> Zones(Fixture f) => f.Model.OfClass(FinishZones.Class).ToList();

    static string Library(Fixture f, string name) => (string)f.Run("architecture.surfaceFinishTypeFromLibrary", new JsonObject { ["name"] = name })!["id"]!;

    [Fact]
    public void A_split_walls_finishes_go_to_their_parts_and_run_on_across_the_cut_unless_a_split_line_stops_them()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0, height: H);
        var etics = Library(f, "ETICS EPS 100");
        var tiles = Library(f, "Wall tiles");
        var plaster = Library(f, "Lime plaster 25");
        // The right side all in ETICS; the left side split upright at x = 2: tiles before it, plaster after it.
        f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["face"] = "Face.Right", ["whole"] = true, ["finishType"] = etics });
        f.Run("architecture.splitFinishFace", new JsonObject { ["hostId"] = wall, ["face"] = "Face.Left", ["points"] = new JsonArray(P(2, Left, 0), P(2, Left, H)) });
        f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["point"] = P(1, Left, 1), ["normal"] = P(0, 1, 0), ["finishType"] = tiles });
        f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["point"] = P(4, Left, 1), ["normal"] = P(0, 1, 0), ["finishType"] = plaster });
        double Sum(string type) => Zones(f).Where(z => z.Str("finishType") == type).Sum(z => Net(f, z.Id));
        var (e0, t0, p0) = (Sum(etics), Sum(tiles), Sum(plaster));
        Assert.Equal(5 * H, e0, 6);

        var second = Ids(f.Run("element.split", new JsonObject { ["id"] = wall, ["at"] = P(3, 0) }))[1];
        // The ETICS on both parts; the tiles stopped by the split line stay on the first; the plaster runs on to the cut.
        Assert.Equal(2, Zones(f).Count(z => z.Str("finishType") == etics));
        Assert.Contains(Zones(f), z => z.Str("finishType") == etics && z.Str("hostId") == second);
        Assert.All(Zones(f).Where(z => z.Str("finishType") == tiles), z => Assert.Equal(wall, z.Str("hostId")));
        Assert.Equal(2, Zones(f).Count(z => z.Str("finishType") == plaster));
        Assert.Equal(e0, Sum(etics), 6);
        Assert.Equal(t0, Sum(tiles), 6);
        Assert.Equal(p0, Sum(plaster), 6);

        f.Session.Undo();
        Assert.Equal(3, Zones(f).Count);
        Assert.All(Zones(f), z => Assert.Equal(wall, z.Str("hostId")));
    }

    [Fact]
    public void A_wall_trimmed_from_its_start_keeps_its_finishes_where_they_were()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 5, 0, height: H);
        var tiles = Library(f, "Wall tiles");
        f.Run("architecture.splitFinishFace", new JsonObject { ["hostId"] = wall, ["face"] = "Face.Left", ["points"] = new JsonArray(P(3, Left, 0), P(3, Left, H)) });
        var z = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["point"] = P(4, Left, 1), ["normal"] = P(0, 1, 0), ["finishType"] = tiles })!["id"]!;
        Assert.Equal(2 * H, Net(f, z), 6);
        f.Run("element.trimExtend", new JsonObject { ["id"] = wall, ["at"] = P(4, 0), ["line"] = new JsonArray(P(1, -1), P(1, 1)) });
        // Still from x = 3 to the end: 2 m.
        Assert.Equal(2 * H, Net(f, z), 6);
    }

    [Fact]
    public void Sunshades_and_balconies_on_a_split_wall_go_to_the_part_they_are_on_and_one_across_the_cut_refuses()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 10, 0);
        var shade = (string)f.Run("architecture.addSunshade", new JsonObject { ["kind"] = "louvres", ["wallId"] = wall, ["x1"] = 7.2, ["y1"] = 0, ["x2"] = 8.8, ["y2"] = 0 })!["id"]!;
        var balcony = (string)f.Run("architecture.addBalcony", new JsonObject { ["storeyId"] = f.Storey, ["style"] = "stacked", ["x"] = 2, ["y"] = -0.5 })!["id"]!;
        Assert.Equal(wall, f.Model.Get(balcony)!.Str("wallId"));
        var e = Assert.Throws<CommandException>(() => f.Run("element.split", new JsonObject { ["id"] = wall, ["at"] = P(8, 0) }));
        Assert.Contains("across the cut", e.Message);
        var second = Ids(f.Run("element.split", new JsonObject { ["id"] = wall, ["at"] = P(5, 0) }))[1];
        var s = f.Model.Get(shade)!;
        Assert.Equal(second, s.Str("wall"));
        Assert.Equal(2.2, Math.Min(s.Num("from"), s.Num("to")), 6);
        Assert.Equal(3.8, Math.Max(s.Num("from"), s.Num("to")), 6);
        Assert.Equal(wall, f.Model.Get(balcony)!.Str("wallId"));
        // Trimmed back past the balcony: it stands free where it is.
        var before = f.Bounds(balcony);
        f.Run("element.trimExtend", new JsonObject { ["id"] = wall, ["at"] = P(4.5, 0), ["line"] = new JsonArray(P(4, -1), P(4, 1)) });
        Assert.Null(f.Model.Get(balcony)!.Str("wallId"));
        Assert.Equal(before.Min.X, f.Bounds(balcony).Min.X, 6);
    }

    [Fact]
    public void A_ceiling_split_keeps_its_grid_where_it_was_and_each_fixture_in_its_part()
    {
        var f = new Fixture();
        var c = (string)f.Run("architecture.addCeiling", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(P(0, 0), P(3.6, 0), P(3.6, 4.8), P(0, 4.8)), ["height"] = 2.6 })!["id"]!;
        f.Run("architecture.ceilingFixture", new JsonObject { ["id"] = c, ["kind"] = "light", ["x"] = 1.0, ["y"] = 1.0 });
        f.Run("architecture.ceilingFixture", new JsonObject { ["id"] = c, ["kind"] = "light", ["x"] = 1.0, ["y"] = 3.8 });
        var origin = Ceilings.GridOf(f.Model, f.Model.Get(c)!).Origin;
        var ids = Ids(f.Run("architecture.splitOutline", new JsonObject { ["id"] = c, ["a"] = P(-1, 2.4), ["b"] = P(5, 2.4), ["keep"] = P(1, 1) }));
        foreach (var id in ids)
        {
            Near(origin, Ceilings.GridOf(f.Model, f.Model.Get(id)!).Origin);
            Assert.Single(Ceilings.Fixtures(f.Model.Get(id)!));
        }
    }

    static void Near(Vec2 a, Vec2 b) { Assert.Equal(a.X, b.X, 6); Assert.Equal(a.Y, b.Y, 6); }

    [Fact]
    public void A_floor_split_across_a_curved_edge_keeps_that_edges_layer_extent_on_both_parts_and_its_top_finish()
    {
        var f = new Fixture();
        var slab = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = new JsonArray(P(0, 0), P(8, 0), P(8, 4), P(0, 4)), ["arcs"] = new JsonArray(0, 0, 1, 0),
        })!["id"]!;
        f.Run("architecture.setLayerExtent", new JsonObject { ["id"] = slab, ["layer"] = 0, ["edge"] = "edge", ["index"] = 2, ["offset"] = 0.05 });
        var tiles = Library(f, "Wall tiles");
        var zone = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = slab, ["face"] = "Top", ["whole"] = true, ["finishType"] = tiles })!["id"]!;
        var area = Net(f, zone);
        var ids = Ids(f.Run("architecture.splitOutline", new JsonObject { ["id"] = slab, ["a"] = P(4, -1), ["b"] = P(4, 6), ["keep"] = P(1, 1) }));
        foreach (var id in ids)
            Assert.Contains(LayerExtents.Of(f.Model.Get(id)!), x => x.Edge == LayerExtents.Edge && Math.Abs(x.Offset - 0.05) < 1e-9);
        Assert.Equal(2, Zones(f).Count);
        Assert.Equal(area, Zones(f).Sum(z => Net(f, z.Id)), 6);
    }

    [Fact]
    public void A_floors_formwork_forms_both_parts_once_it_is_split()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Structure.StructureModule(), new Lapin.Phases.PhasesModule(), new Lapin.Civil.CivilModule(), new Lapin.SiteWorks.SiteWorksModule());
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(P(0, 0), P(6, 0), P(6, 4), P(0, 4)) })!["id"]!;
        var fw = Ids(f.Run("sitework.addFormwork", new JsonObject { ["hostIds"] = new JsonArray(slab) }))[0];
        double Soffit(string id) => f.Session.Quantities(id).SelectMany(s => s.Quantities).First(q => q.Name == "SoffitArea").Value;
        var all = Soffit(fw);
        var ids = Ids(f.Run("architecture.splitOutline", new JsonObject { ["id"] = slab, ["a"] = P(2, -1), ["b"] = P(2, 5), ["keep"] = P(1, 1) }));
        var forms = f.Model.OfClass(Lapin.SiteWorks.Formwork.Class).ToList();
        Assert.Equal(2, forms.Count);
        Assert.Contains(forms, x => x.Str("host") == ids[1]);
        Assert.Equal(all, forms.Sum(x => Soffit(x.Id)), 6);
    }

    [Fact]
    public void A_split_beams_bars_are_hosted_by_the_part_their_point_lies_in()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Structure.StructureModule(), new Lapin.Drafting.DraftingModule(), new Lapin.Rebar.RebarModule());
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var top = Lapin.Structure.Beams.Top(f.Model, f.Model.Get(beam)!);
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0.03), ["end"] = new JsonArray(6, 0.03), ["scale"] = 50 })!["id"]!;
        string Bar(double x) => (string)f.Run("rebar.add", new JsonObject { ["viewId"] = view, ["shape"] = "straight", ["at"] = new JsonArray(x, top - 0.2), ["params"] = new JsonObject { ["length"] = 1 }, ["diameter"] = 0.016 })!["id"]!;
        var (a, b) = (Bar(0.5), Bar(4.5));
        Assert.Equal(beam, f.Model.Get(b)!.Str("host"));
        var second = Ids(f.Run("element.split", new JsonObject { ["id"] = beam, ["at"] = P(3, 0) }))[1];
        Assert.Equal(beam, f.Model.Get(a)!.Str("host"));
        Assert.Equal(second, f.Model.Get(b)!.Str("host"));
    }

    // ─── Roofs, rooms, platforms, parcels ─────────────────────────────────────

    static double RoofArea(Fixture f, string id) =>
        f.Session.Quantities(id).Single(q => q.Name == "Qto_RoofBaseQuantities").Quantities.Single(q => q.Name == "GrossArea").Value;

    [Fact]
    public void A_gable_roof_split_across_its_ridge_keeps_its_planes_and_area_and_a_cut_that_would_change_them_refuses()
    {
        var f = new Fixture();
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(P(0, 0), P(10, 0), P(10, 6), P(0, 6)) })!["id"]!;
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["shape"] = "gable", ["ridgeDirection"] = "x" } });
        var area = RoofArea(f, roof);
        var height = Lapin.Architecture.Roofs.RoofTop.ShapeOf(f.Model.Get(roof)!, f.Model)!.Height;
        var e = Assert.Throws<CommandException>(() => f.Run("architecture.splitOutline", new JsonObject { ["id"] = roof, ["a"] = P(-1, 2), ["b"] = P(11, 2) }));
        Assert.Contains("slopes", e.Message);
        var ids = Ids(f.Run("element.splitOutline", new JsonObject { ["id"] = roof, ["a"] = P(5, -1), ["b"] = P(5, 7), ["keep"] = P(1, 1) }));
        Assert.Equal(area, RoofArea(f, ids[0]) + RoofArea(f, ids[1]), 6);
        foreach (var id in ids) Assert.Equal(height, Lapin.Architecture.Roofs.RoofTop.ShapeOf(f.Model.Get(id)!, f.Model)!.Height, 6);
        f.Session.Undo();
        Assert.Single(f.Model.OfClass(IfcClass.Roof));
    }

    [Fact]
    public void A_drawn_room_split_gives_a_second_room_with_the_next_number_and_areas_that_add_up()
    {
        var f = new Fixture();
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(P(0, 0), P(6, 0), P(6, 4), P(0, 4)), ["arcs"] = new JsonArray(0, 0, 1, 0) })!["id"]!;
        var area = ArcOutline.AreaOf(f.Model.Get(room)!);
        var ids = Ids(f.Run("element.splitOutline", new JsonObject { ["id"] = room, ["a"] = P(2, -1), ["b"] = P(2, 6), ["keep"] = P(1, 1) }));
        Assert.Equal(area, ArcOutline.AreaOf(f.Model.Get(ids[0])!) + ArcOutline.AreaOf(f.Model.Get(ids[1])!), 6);
        Assert.NotEqual(f.Model.Get(ids[0])!.Str("number"), f.Model.Get(ids[1])!.Str("number"));
        // A room bounded by walls is not cut by a line.
        foreach (var (x1, y1, x2, y2) in new[] { (10.0, 0.0, 14.0, 0.0), (14, 0, 14, 4), (14, 4, 10, 4), (10, 4, 10, 0) }) f.Wall(x1, y1, x2, y2);
        var auto = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 12, ["y"] = 2 })!["id"]!;
        Assert.Contains("separation", Assert.Throws<CommandException>(() => f.Run("element.splitOutline", new JsonObject { ["id"] = auto, ["a"] = P(12, -1), ["b"] = P(12, 5) })).Message);
    }

    [Fact]
    public void A_platform_split_keeps_its_hole_on_its_part_and_cuts_the_terrain_with_both_and_a_parcel_is_only_trimmed()
    {
        var f = new Fixture(new Lapin.Civil.CivilModule());
        var t = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = -20, ["y1"] = -20, ["x2"] = 60, ["y2"] = 60, ["elevation"] = 0 })!["id"]!;
        var pad = (string)f.Run("civil.addPlatform", new JsonObject
        {
            ["terrainId"] = t, ["points"] = new JsonArray(P(0, 0), P(10, 0), P(10, 6), P(0, 6)), ["elevation"] = 0.5,
            ["holes"] = new JsonArray(new JsonArray(P(7, 2), P(9, 2), P(9, 4), P(7, 4))),
        })!["id"]!;
        var area = ArcOutline.AreaOf(f.Model.Get(pad)!) - OutlineHoles.Area(f.Model.Get(pad)!);
        Assert.Contains("crosses", Assert.Throws<CommandException>(() => f.Run("element.splitOutline", new JsonObject { ["id"] = pad, ["a"] = P(8, -1), ["b"] = P(8, 7) })).Message);
        var ids = Ids(f.Run("element.splitOutline", new JsonObject { ["id"] = pad, ["a"] = P(4, -1), ["b"] = P(4, 7), ["keep"] = P(1, 1) }));
        var (a, b) = (f.Model.Get(ids[0])!, f.Model.Get(ids[1])!);
        Assert.Empty(OutlineHoles.Of(a));
        Assert.Single(OutlineHoles.Of(b));
        Assert.Equal(area, ArcOutline.AreaOf(a) - OutlineHoles.Area(a) + ArcOutline.AreaOf(b) - OutlineHoles.Area(b), 6);
        Assert.Equal(t, Lapin.Civil.Terrains.TerrainOf(f.Model, ids[1])!.Id);
        Assert.Equal(0.5, b.Num("elevation"), 9);

        f.Run("civil.setParcel", new JsonObject { ["terrainId"] = t, ["points"] = new JsonArray(P(0, 0), P(20, 0), P(20, 15), P(0, 15)) });
        Assert.Contains("one parcel", Assert.Throws<CommandException>(() => f.Run("element.splitOutline", new JsonObject { ["id"] = t, ["a"] = P(10, -1), ["b"] = P(10, 16) })).Message);
        f.Run("element.trimOutline", new JsonObject { ["id"] = t, ["a"] = P(10, -1), ["b"] = P(10, 16), ["keep"] = P(1, 1) });
        Assert.Equal(150, ArcOutline.AreaOf(f.Model.Get(t)!, "parcel", Lapin.Civil.Terrains.ParcelArcs), 6);
    }
}
