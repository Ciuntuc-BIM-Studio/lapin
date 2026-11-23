using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Site grading T1 (docs/plan-site-grading.md; forum topic 230): a terrain made straight from an outline, every vertex of the
/// outline as built a point of its own (with a grip and a height that can be typed); a clipped terrain's outline made its
/// points on demand with the ground unchanged; the outline kept as points when the boundary is drawn again, split or merged;
/// a terrain without the flag working as before.
/// </summary>
public sealed class TerrainOutlineTests
{
    static Fixture F() => new(new ArchitectureModule(), new CivilModule(), new DraftingModule());
    static JsonArray P(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);
    static JsonArray Pts(params double[][] pts) => new([.. pts.Select(p => (JsonNode)P(p))]);

    static string OnOutline(Fixture f, JsonObject a)
    {
        a["storeyId"] ??= f.Storey;
        return (string)f.Run("civil.addTerrainOnOutline", a)!["id"]!;
    }

    static bool IsPoint(Fixture f, string t, Vec2 p) => Terrains.Points(f.Model.Get(t)!).Any(q => (q.Xy - p).Length <= 1e-6);

    /// <summary>An L, 20 × 20 m with a 10 × 10 m notch at its north-east.</summary>
    static readonly double[][] L = [[0, 0], [20, 0], [20, 10], [10, 10], [10, 20], [0, 20]];

    [Fact]
    public void A_terrain_on_an_L_outline_has_every_corner_as_a_point_and_ends_exactly_there()
    {
        var f = F();
        var r = f.Run("civil.addTerrainOnOutline", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts(L), ["elevation"] = 5 })!;
        var id = (string)r["id"]!;
        Assert.Equal(6, (int)r["outline"]!);
        var t = f.Model.Get(id)!;
        Assert.True(TerrainOutlines.Holds(t));
        foreach (var c in L) Assert.True(IsPoint(f, id, new Vec2(c[0], c[1])), $"corner {c[0]}, {c[1]} is a point");
        var g = Terrains.Of(t);
        Assert.Equal(5, g.ElevationAt(20, 10)!.Value, 9);
        Assert.Equal(5, g.ElevationAt(5, 5)!.Value, 9);
        // The notch is no ground; the L's area is 300 m².
        Assert.Null(g.ElevationAt(15, 15));
        Assert.Equal(300, Terrains.PlanArea(g.Tin), 6);
        // Interior points on the default grid (2.5 m), none closer than a third of it to the outline.
        var inner = Terrains.Points(t).Skip(6).ToList();
        Assert.NotEmpty(inner);
        Assert.All(inner, p => Assert.True(Polygon2.Contains(L.Select(c => new Vec2(c[0], c[1])).ToList(), p.Xy)));
        // Its body and its plan: the ground as a solid, its edge drawn.
        var geo = f.Session.Geometry(id);
        Assert.NotNull(geo);
        Assert.NotEmpty(geo!.Parts);
        Assert.Contains(geo.Plan!, p => p.Kind == "terrainEdge");
        // One undo step takes it all away.
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
    }

    [Fact]
    public void A_circle_s_outline_is_laid_in_chords_by_the_one_rule_each_chord_vertex_a_point()
    {
        var f = F();
        var (corners, rises) = ArcOutline.Circle(new Vec2(10, 10), 8);
        var id = OnOutline(f, new JsonObject
        {
            ["points"] = new JsonArray([.. corners.Select(c => (JsonNode)P(c.X, c.Y))]),
            ["arcs"] = new JsonArray([.. rises.Select(x => (JsonNode)x)]),
            ["elevation"] = 2,
        });
        var t = f.Model.Get(id)!;
        var built = Terrains.BoundaryOutline(t);
        Assert.True(built.Count > 16);
        Assert.All(built, p => { Assert.Equal(8, (p - new Vec2(10, 10)).Length, 6); Assert.True(IsPoint(f, id, p)); });
        // The ground's edge is those points: its boundary has no other vertex.
        var g = Terrains.Of(t);
        Assert.Equal(built.Count, g.Boundary().Count);
        Assert.Equal(2, g.ElevationAt(10, 10)!.Value, 9);
        // Level exactly: no contour wanders through it at its own height.
        Assert.All(g.Tin.Points, p => Assert.Equal(2, p.Z));
        Assert.Empty(g.Contour(2));
    }

    [Fact]
    public void Corners_given_heights_keep_them_and_the_vertices_along_an_arc_run_evenly_between_its_ends()
    {
        var f = F();
        // A square whose north edge bulges out by 2 m, the corners at 1, 2, 3 and 4 m.
        var id = OnOutline(f, new JsonObject
        {
            ["points"] = Pts([0, 0, 1], [10, 0, 2], [10, 10, 3], [0, 10, 4]),
            ["arcs"] = P(0, 0, -2, 0),
        });
        var t = f.Model.Get(id)!;
        var g = Terrains.Of(t);
        Assert.Equal(1, g.ElevationAt(0, 0)!.Value, 9);
        Assert.Equal(3, g.ElevationAt(10, 10)!.Value, 9);
        Assert.Equal(4, g.ElevationAt(0, 10)!.Value, 9);
        // The arc's middle (5, 12): halfway along it, so halfway from 3 to 4.
        var top = Terrains.BoundaryOutline(t).Where(p => p.Y > 10 + 1e-9).ToList();
        Assert.NotEmpty(top);
        var mid = top.MinBy(p => Math.Abs(p.X - 5));
        Assert.Equal(3.5, g.ElevationAt(mid.X, mid.Y)!.Value, 1);
        foreach (var p in top) { var z = g.ElevationAt(p.X, p.Y)!.Value; Assert.InRange(z, 3 - 1e-9, 4 + 1e-9); }
        // The interior between the corners' heights.
        Assert.InRange(g.ElevationAt(5, 5)!.Value, 1, 4);
    }

    [Fact]
    public void On_the_ground_each_vertex_takes_the_height_of_the_terrain_under_it()
    {
        var f = F();
        var pts = new JsonArray();
        for (var x = 0; x <= 40; x += 5) for (var y = 0; y <= 40; y += 5) pts.Add(P(x, y, 100 + 0.1 * x));
        f.Run("civil.addTerrain", new JsonObject { ["points"] = pts, ["datum"] = 100 });
        var id = OnOutline(f, new JsonObject { ["points"] = Pts([10, 10], [30, 10], [30, 30], [10, 30]), ["ground"] = true, ["spacing"] = 5 });
        var g = Terrains.Of(f.Model.Get(id)!);
        Assert.Equal(1, g.ElevationAt(10, 10)!.Value, 9);
        Assert.Equal(3, g.ElevationAt(30, 30)!.Value, 9);
        Assert.Equal(2, g.ElevationAt(20, 20)!.Value, 9);
    }

    [Fact]
    public void A_room_or_a_platform_gives_its_outline_the_platform_its_level()
    {
        var f = F();
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts([0, 0], [6, 0], [6, 4], [0, 4]), ["name"] = "Yard" })!["id"]!;
        var r = f.Run("civil.addTerrainOnOutline", new JsonObject { ["fromId"] = room, ["elevation"] = -0.5 })!;
        Assert.Equal("room", (string)r["from"]!);
        var g = Terrains.Of(f.Model.Get((string)r["id"]!)!);
        Assert.Equal(24, Terrains.PlanArea(g.Tin), 6);
        Assert.Equal(-0.5, g.ElevationAt(3, 2)!.Value, 9);
        var pad = (string)f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = (string)r["id"]!, ["points"] = Pts([1, 1], [3, 1], [3, 3], [1, 3]), ["elevation"] = 1.25 })!["id"]!;
        var p = f.Run("civil.addTerrainOnOutline", new JsonObject { ["fromId"] = pad })!;
        Assert.Equal(1.25, Terrains.Of(f.Model.Get((string)p["id"]!)!).ElevationAt(2, 2)!.Value, 9);
        Assert.Throws<CommandException>(() => f.Run("civil.addTerrainOnOutline", new JsonObject { ["fromId"] = f.Storey }));
    }

    /// <summary>A survey 40 × 40 m every 4 m on a warped surface, clipped by an L with one arc edge.</summary>
    static string Clipped(Fixture f)
    {
        var pts = new JsonArray();
        for (var x = 0; x <= 40; x += 4) for (var y = 0; y <= 40; y += 4) pts.Add(P(x, y, 50 + 0.05 * x + 0.02 * y + 0.001 * x * y));
        var id = (string)f.Run("civil.addTerrain", new JsonObject { ["points"] = pts, ["datum"] = 50 })!["id"]!;
        f.Run("civil.setBoundary", new JsonObject { ["id"] = id, ["points"] = Pts([1, 1], [39, 1], [39, 21], [21, 21], [21, 39], [1, 39]), ["arcs"] = P(0, 0, 0, 0, 0, 3) });
        return id;
    }

    [Fact]
    public void A_clipped_terrain_s_outline_becomes_its_points_on_demand_and_the_ground_does_not_move()
    {
        var f = F();
        var id = Clipped(f);
        var before = Terrains.Of(f.Model.Get(id)!);
        var count = Terrains.Points(f.Model.Get(id)!).Count;
        Assert.False(TerrainOutlines.Holds(f.Model.Get(id)!));
        var r = f.Run("civil.outlineToPoints", new JsonObject { ["id"] = id })!;
        var t = f.Model.Get(id)!;
        Assert.True(TerrainOutlines.Holds(t));
        var built = Terrains.BoundaryOutline(t);
        Assert.Equal(built.Count, (int)r["added"]!);
        Assert.Equal(count + built.Count, Terrains.Points(t).Count);
        Assert.Null(r["moved"]);
        Assert.All(built, p => Assert.True(IsPoint(f, id, p)));
        var after = Terrains.Of(t);
        for (var x = 1.5; x < 39; x += 2.7)
            for (var y = 1.5; y < 39; y += 2.9)
            {
                var (z0, z1) = (before.ElevationAt(x, y), after.ElevationAt(x, y));
                Assert.Equal(z0.HasValue, z1.HasValue);
                if (z0 is { } a && z1 is { } b) Assert.Equal(a, b, 9);
            }
        // Again: nothing more to add.
        Assert.Equal(0, (int)f.Run("civil.outlineToPoints", new JsonObject { ["id"] = id })!["added"]!);
        var plain = (string)f.Run("civil.addTerrain", new JsonObject { ["points"] = Pts([0, 0, 0], [5, 0, 0], [0, 5, 0]) })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("civil.outlineToPoints", new JsonObject { ["id"] = plain }));
    }

    [Fact]
    public void A_terrain_without_its_outline_as_points_draws_a_boundary_again_with_its_points_untouched()
    {
        var f = F();
        var id = Clipped(f);
        var pts = Terrains.Points(f.Model.Get(id)!);
        f.Run("civil.setBoundary", new JsonObject { ["id"] = id, ["points"] = Pts([2, 2], [30, 2], [30, 30], [2, 30]) });
        Assert.Equal(pts, Terrains.Points(f.Model.Get(id)!));
    }

    [Fact]
    public void A_corner_dragged_keeps_its_height_a_corner_added_takes_the_ground_s_and_the_old_corner_s_point_goes()
    {
        var f = F();
        var id = OnOutline(f, new JsonObject { ["points"] = Pts([0, 0, 1], [10, 0, 2], [10, 10, 3], [0, 10, 4]), ["spacing"] = 0 });
        Assert.Equal(4, Terrains.Points(f.Model.Get(id)!).Count);
        // The north-east corner dragged out to (14, 12): still at 3 m, and (10, 10) no point any more.
        f.Run("civil.setBoundary", new JsonObject { ["id"] = id, ["points"] = Pts([0, 0], [10, 0], [14, 12], [0, 10]) });
        var t = f.Model.Get(id)!;
        Assert.Equal(4, Terrains.Points(t).Count);
        Assert.False(IsPoint(f, id, new Vec2(10, 10)));
        Assert.Equal(3, Terrains.Of(t).ElevationAt(14, 12)!.Value, 9);
        // A corner put on the south edge's middle: a point at the ground's height there, halfway from 1 to 2.
        f.Run("civil.setBoundary", new JsonObject { ["id"] = id, ["points"] = Pts([0, 0], [5, 0], [10, 0], [14, 12], [0, 10]) });
        t = f.Model.Get(id)!;
        Assert.Equal(5, Terrains.Points(t).Count);
        Assert.Equal(1.5, Terrains.Of(t).ElevationAt(5, 0)!.Value, 9);
        // Its height typed, absolute, for two outline points at once: one undo step.
        var pts = Terrains.Points(t);
        var two = new[] { pts.FindIndex(p => (p.Xy - new Vec2(5, 0)).Length < 1e-6), pts.FindIndex(p => (p.Xy - new Vec2(14, 12)).Length < 1e-6) };
        f.Run("civil.setTerrainPoints", new JsonObject { ["id"] = id, ["indices"] = new JsonArray([.. two.Select(i => (JsonNode)i)]), ["z"] = 7.25 });
        var g = Terrains.Of(f.Model.Get(id)!);
        Assert.Equal(7.25, g.ElevationAt(5, 0)!.Value, 9);
        Assert.Equal(7.25, g.ElevationAt(14, 12)!.Value, 9);
        // The boundary taken away: the points stay, the flag goes.
        f.Run("civil.setBoundary", new JsonObject { ["id"] = id });
        Assert.False(TerrainOutlines.Holds(f.Model.Get(id)!));
        Assert.Equal(5, Terrains.Points(f.Model.Get(id)!).Count);
    }

    [Fact]
    public void Each_half_of_a_split_and_a_merge_keep_their_outline_as_points()
    {
        var f = F();
        var id = OnOutline(f, new JsonObject { ["points"] = Pts([0, 0, 1], [20, 0, 1], [20, 10, 3], [0, 10, 3]), ["spacing"] = 2 });
        var ids = f.Run("civil.splitTerrain", new JsonObject { ["id"] = id, ["x1"] = 8, ["y1"] = -1, ["x2"] = 8, ["y2"] = 11 })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        foreach (var half in ids)
        {
            var t = f.Model.Get(half)!;
            Assert.True(TerrainOutlines.Holds(t));
            Assert.All(Terrains.BoundaryOutline(t), p => Assert.True(IsPoint(f, half, p)));
        }
        // The cut's ends at the ground's height: from 1 m at the south to 3 m at the north.
        Assert.Equal(1, Terrains.Of(f.Model.Get(ids[1])!).ElevationAt(8, 0)!.Value, 9);
        Assert.Equal(3, Terrains.Of(f.Model.Get(ids[0])!).ElevationAt(8, 10)!.Value, 9);
        var m = (string)f.Run("civil.mergeTerrains", new JsonObject { ["ids"] = new JsonArray(ids[0], ids[1]) })!["id"]!;
        var merged = f.Model.Get(m)!;
        Assert.True(TerrainOutlines.Holds(merged));
        Assert.All(Terrains.BoundaryOutline(merged), p => Assert.True(IsPoint(f, m, p)));
        Assert.Equal(200, Terrains.PlanArea(Terrains.Of(merged).Tin), 6);
    }

    [Fact]
    public void A_closed_drawn_shape_converts_to_a_terrain_inside_it()
    {
        var f = F();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 100 })!["id"]!;
        var ids = f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = plan, ["layer"] = "0",
            ["shapes"] = new JsonArray(new JsonObject { ["type"] = "circle", ["c"] = P(5, 5), ["r"] = 4 }),
        })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        f.Run("drafting.convert", new JsonObject { ["viewId"] = plan, ["ids"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]), ["converter"] = "terrainOutline", ["options"] = new JsonObject { ["elevation"] = 0.3 } });
        var t = Terrains.All(f.Model).Single();
        Assert.True(TerrainOutlines.Holds(t));
        Assert.Equal(0.3, Terrains.Of(t).ElevationAt(5, 5)!.Value, 9);
        Assert.All(Terrains.BoundaryOutline(t), p => Assert.Equal(4, (p - new Vec2(5, 5)).Length, 6));
    }

    /// <summary>The square of 10 m with its north edge bulging 2 m out, corners at 1, 2, 3, 4 m; the arc's vertices, west to east.</summary>
    static (string Id, List<int> Arc, int Ne, int Nw) Bulged(Fixture f)
    {
        var id = OnOutline(f, new JsonObject { ["points"] = Pts([0, 0, 1], [10, 0, 2], [10, 10, 3], [0, 10, 4]), ["arcs"] = P(0, 0, -2, 0), ["spacing"] = 0 });
        var pts = Terrains.Points(f.Model.Get(id)!);
        var arc = Enumerable.Range(0, pts.Count).Where(i => pts[i].Y > 10 + 1e-9).OrderBy(i => pts[i].X).ToList();
        int Find(double x, double y) => pts.FindIndex(p => (p.Xy - new Vec2(x, y)).Length < 1e-6);
        return (id, arc, Find(10, 10), Find(0, 10));
    }

    [Fact]
    public void A_corner_s_height_changed_lays_the_arc_s_vertices_again_between_its_corners_in_the_same_undo_step()
    {
        var f = F();
        var (id, arc, ne, _) = Bulged(f);
        Assert.True(arc.Count > 4);
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = id, ["index"] = ne, ["z"] = 6 });
        var pts = Terrains.Points(f.Model.Get(id)!);
        // From 4 at the west corner to 6 at the east one, evenly along the arc; each vertex between them.
        var mid = arc.MinBy(i => Math.Abs(pts[i].X - 5));
        Assert.Equal(5, pts[mid].Z, 1);
        for (var k = 1; k < arc.Count; k++) Assert.True(pts[arc[k]].Z >= pts[arc[k - 1]].Z - 1e-9, "rising west to east");
        Assert.All(arc, i => Assert.InRange(pts[i].Z, 4 - 1e-9, 6 + 1e-9));
        // The straight edges have no vertices of their own: nothing else moved.
        Assert.Equal(1, Terrains.Of(f.Model.Get(id)!).ElevationAt(0, 0)!.Value, 9);
        f.Session.Undo();
        pts = Terrains.Points(f.Model.Get(id)!);
        Assert.Equal(3.5, pts[mid].Z, 1);
        Assert.Equal(3, pts[ne].Z, 9);
        // Typed for many at once (the corners picked) the same.
        f.Run("civil.setTerrainPoints", new JsonObject { ["id"] = id, ["indices"] = new JsonArray(ne), ["z"] = 6 });
        Assert.Equal(5, Terrains.Points(f.Model.Get(id)!)[mid].Z, 1);
    }

    [Fact]
    public void A_vertex_of_the_arc_given_its_own_height_keeps_it_and_splits_the_arc_in_two()
    {
        var f = F();
        var (id, arc, ne, nw) = Bulged(f);
        var pts = Terrains.Points(f.Model.Get(id)!);
        var mid = arc.MinBy(i => Math.Abs(pts[i].X - 5));
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = id, ["index"] = mid, ["z"] = 10 });
        Assert.Single(PointLists.Xy(f.Model.Get(id)!.Attrs[TerrainOutlines.OwnKey]));
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = id, ["index"] = ne, ["z"] = 0 });
        pts = Terrains.Points(f.Model.Get(id)!);
        Assert.Equal(10, pts[mid].Z, 9);
        // East of it, from 10 down to the corner's 0; west of it, from the corner's 4 up to 10.
        var east = arc.Where(i => pts[i].X > pts[mid].X).ToList();
        var west = arc.Where(i => pts[i].X < pts[mid].X).ToList();
        Assert.All(east, i => Assert.InRange(pts[i].Z, 0 - 1e-9, 10 + 1e-9));
        Assert.All(west, i => Assert.InRange(pts[i].Z, 4 - 1e-9, 10 + 1e-9));
        for (var k = 1; k < east.Count; k++) Assert.True(pts[east[k]].Z <= pts[east[k - 1]].Z + 1e-9, "falling east of it");
        Assert.Equal(4, pts[nw].Z, 9);
    }
}
