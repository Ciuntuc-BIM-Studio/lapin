using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Curved edges on the other outlines (audit S4, after decision 589's floors): a drawn room's, a drawn balcony's, a platform's
/// and a parcel's edges may be arcs, kept as each edge's rise in <c>arcs</c> beside the corners (a parcel's in
/// <c>parcelArcs</c> beside <c>parcel</c>), laid in chords by the one rule, measured exactly, rounded and cut in one undo step.
/// What was saved before (no arcs) reads unchanged.
/// </summary>
public sealed class OutlineArcsTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-outline-arcs").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);
    static JsonArray Nums(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);
    static double Q(Fixture f, string id, string set, string name) => f.Session.Quantities(id).First(q => q.Name == set).Quantities.Single(x => x.Name == name).Value;
    static Fixture F() => new(new ArchitectureModule(), new CivilModule());

    // The top edge of a 6 × 4 rectangle (corner 2 to 3, running −x) bulging up by 1 m: r = 5, sweep 2·asin(0.6).
    static readonly double R = 5, T = 2 * Math.Asin(0.6);
    static readonly double Segment = R * R / 2 * (T - Math.Sin(T));

    [Fact]
    public void A_drawn_room_with_a_curved_edge_is_measured_exactly_and_follows_its_arc()
    {
        var f = F();
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (6, 0), (6, 4), (0, 4)), ["arcs"] = Nums(0, 0, -1, 0) })!["id"]!;
        Assert.Equal(24 + Segment, Q(f, room, "Qto_SpaceBaseQuantities", "GrossFloorArea"), 9);
        Assert.Equal(24 + Segment, Q(f, room, "Qto_SpaceBaseQuantities", "NetFloorArea"), 9);
        Assert.Equal(4 + 4 + 6 + R * T, Q(f, room, "Qto_SpaceBaseQuantities", "GrossPerimeter"), 9);
        // Its outline in plan goes up to the arc; the schedule's outline is the exact one.
        var outer = f.Session.Geometry(room)!.Plan!.First(p => p.Kind == "room").Points;
        Assert.InRange(outer.Max(p => p.Y), 5 - Chords.Tolerance, 5 + 1e-9);
        Assert.Equal(24 + Segment, Rooms.Of(f.Model.Get(room)!, f.Model, f.Session.Geometry)!.GrossArea, 9);
        // A corner moved keeps the arcs; a corner rounded is one undo step.
        f.Run("architecture.updateRoom", new JsonObject { ["id"] = room, ["points"] = Pts((0, 0), (7, 0), (6, 4), (0, 4)) });
        Assert.Equal(-1, ArcOutline.RisesOf(f.Model.Get(room)!)![2], 9);
        f.Run("architecture.roundCorner", new JsonObject { ["id"] = room, ["index"] = 0, ["radius"] = 0.5 });
        Assert.Equal(5, ArcOutline.CornersOf(f.Model.Get(room)!).Count);
        f.Session.Undo();
        Assert.Equal(4, ArcOutline.CornersOf(f.Model.Get(room)!).Count);
        // Straight again: no arcs kept, as a room drawn before.
        f.Run("architecture.updateRoom", new JsonObject { ["id"] = room, ["points"] = Pts((0, 0), (6, 0), (6, 4), (0, 4)), ["arcs"] = Nums(0, 0, 0, 0) });
        Assert.Null(f.Model.Get(room)!.Attrs[ArcOutline.Key]);
        Assert.Equal(24, Q(f, room, "Qto_SpaceBaseQuantities", "GrossFloorArea"), 9);
    }

    [Fact]
    public void A_room_found_from_its_walls_takes_no_arcs()
    {
        var f = F();
        f.Wall(0, 0, 6, 0); f.Wall(6, 0, 6, 4); f.Wall(6, 4, 0, 4); f.Wall(0, 4, 0, 0);
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 3, ["y"] = 2 })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("architecture.setEdgeArc", new JsonObject { ["id"] = room, ["index"] = 0, ["rise"] = 0.5 }));
        Assert.Null(f.Model.Get(room)!.Attrs[ArcOutline.Key]);
    }

    [Fact]
    public void A_drawn_balcony_with_a_curved_front_is_built_along_it_and_keeps_its_wall_edge()
    {
        var f = F();
        // Its first edge (0,0)→(4,0) against the building; the front (4,1.5)→(0,1.5) bulging out by 0.5 (right of its way: −).
        var b = (string)f.Run("architecture.addBalcony", new JsonObject { ["storeyId"] = f.Storey, ["contour"] = Pts((0, 0), (4, 0), (4, 1.5), (0, 1.5)), ["arcs"] = Nums(0, 0, -0.5, 0) })!["id"]!;
        var plan = f.Session.Geometry(b)!.Plan!.First(p => p.Kind == "outline").Points;
        Assert.InRange(plan.Max(p => p.Y), 2 - Chords.Tolerance, 2 + 1e-6);
        // The segment of a chord of 4 rising 0.5: r = 4.25.
        var (r, t) = (4.25, 2 * Math.Asin(2 / 4.25));
        var area = 6 + r * r / 2 * (t - Math.Sin(t));
        Assert.Equal(area, Q(f, b, "Qto_SlabBaseQuantities", "GrossArea"), 9);
        // A corner of the front filleted: the outline keeps its order, its first edge still the one against the building.
        f.Run("architecture.chamferCorner", new JsonObject { ["id"] = b, ["index"] = 1, ["distance"] = 0.3 });
        var corners = ArcOutline.CornersOf(f.Model.Get(b)!);
        Assert.Equal(5, corners.Count);
        Assert.Equal(new Vec2(0, 0), corners[0]);
        Assert.Equal(-0.5, ArcOutline.RisesOf(f.Model.Get(b)!)![3], 9);
    }

    [Fact]
    public void A_platform_and_a_parcel_take_arcs_measured_exactly()
    {
        var f = F();
        var t = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = -10, ["y1"] = -10, ["x2"] = 30, ["y2"] = 30 })!["id"]!;
        var pad = (string)f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["points"] = Pts((0, 0), (6, 0), (6, 4), (0, 4)), ["arcs"] = Nums(0, 0, -1, 0), ["elevation"] = -1 })!["id"]!;
        // The platform's outline in the terrain's plan goes up to the arc, and the ground under it is levelled to it.
        var g = f.Session.Geometry(t)!;
        Assert.Contains(g.Plan!, p => p.Kind == "heavy" && p.Closed && Math.Abs(p.Points.Max(x => x.Y) - 5) < Chords.Tolerance + 1e-9);
        var cut = f.Session.Quantities(t).Single(q => q.Name == "Lapin_EarthworkQuantities").Quantities.Single(x => x.Name == "CutVolume").Value;
        Assert.True(Math.Abs(cut - (24 + Segment)) / (24 + Segment) < 0.002, $"{cut}");
        // Its corner rounded by the civil command, one undo step.
        f.Run("civil.roundCorner", new JsonObject { ["id"] = pad, ["index"] = 0, ["radius"] = 1 });
        Assert.Equal(5, ArcOutline.CornersOf(f.Model.Get(pad)!).Count);
        f.Session.Undo();
        Assert.Equal(4, ArcOutline.CornersOf(f.Model.Get(pad)!).Count);
        // The parcel: its area exact, in its own attribute beside the parcel's corners.
        var r = f.Run("civil.setParcel", new JsonObject { ["terrainId"] = t, ["points"] = Pts((0, 0), (6, 0), (6, 4), (0, 4)), ["arcs"] = Nums(0, 0, -1, 0) })!;
        Assert.Equal(Math.Round(24 + Segment, 2), (double)r["area"]!, 9);
        Assert.Equal(24 + Segment, Q(f, t, "Lapin_TerrainQuantities", "ParcelArea"), 9);
        Assert.Equal(4 + 4 + 6 + R * T, Q(f, t, "Lapin_TerrainQuantities", "ParcelPerimeter"), 9);
        Assert.NotNull(f.Model.Get(t)!.Attrs[Terrains.ParcelArcs]);
        f.Run("civil.setEdgeArc", new JsonObject { ["id"] = t, ["index"] = 2, ["rise"] = 0 });
        Assert.Null(f.Model.Get(t)!.Attrs[Terrains.ParcelArcs]);
        Assert.Equal(24, Q(f, t, "Lapin_TerrainQuantities", "ParcelArea"), 9);
    }

    [Fact]
    public void Outlines_drawn_straight_keep_no_arcs()
    {
        var f = F();
        var t = (string)f.Run("civil.addFlatTerrain", new JsonObject { ["x1"] = -10, ["y1"] = -10, ["x2"] = 30, ["y2"] = 30 })!["id"]!;
        var pad = (string)f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["points"] = Pts((0, 0), (6, 0), (6, 4), (0, 4)) })!["id"]!;
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (6, 0), (6, 4), (0, 4)) })!["id"]!;
        var b = (string)f.Run("architecture.addBalcony", new JsonObject { ["storeyId"] = f.Storey, ["contour"] = Pts((0, 0), (4, 0), (4, 1.5), (0, 1.5)) })!["id"]!;
        f.Run("civil.setParcel", new JsonObject { ["terrainId"] = t, ["points"] = Pts((0, 0), (6, 0), (6, 4), (0, 4)) });
        foreach (var id in new[] { pad, room, b }) Assert.Null(f.Model.Get(id)!.Attrs[ArcOutline.Key]);
        Assert.Null(f.Model.Get(t)!.Attrs[Terrains.ParcelArcs]);
    }

    [Fact]
    public void Ifc_carries_a_drawn_rooms_true_curve_and_reads_it_back()
    {
        var f = F();
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (6, 0), (6, 4), (0, 4)), ["arcs"] = Nums(0, 0, -1, 0) })!["id"]!;
        var path = Path.Combine(_dir, "room.ifc");
        IfcExport.Write(f.Session, path);
        Assert.Contains("IFCARCINDEX((3,5,4))", File.ReadAllText(path));
        var g = F();
        IfcImport.Run(g.Session, path, null, out _);
        var back = g.Model.Get(room)!;
        Assert.Equal(-1, ArcOutline.RisesOf(back)![2], 6);
        Assert.Equal(24 + Segment, Q(g, room, "Qto_SpaceBaseQuantities", "GrossFloorArea"), 6);
    }
}
