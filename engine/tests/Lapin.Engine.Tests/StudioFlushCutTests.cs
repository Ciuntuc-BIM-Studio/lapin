using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Push/Pull on a face split by lines drawn on it (decision 518, a tester's report): a 2 × 2 × 1 m box, a slanted line
/// on its top from one edge to the next, the corner pushed down. The cut is clean: the faces it must have and no more,
/// no two faces back to back on one plane (a membrane), the edges a clean solid has, the volume exact, and no outline
/// of the region pushed left hanging over the part taken away.
/// </summary>
public class StudioFlushCutTests
{
    static JsonArray J3(Vec3 p) => [p.X, p.Y, p.Z];
    static readonly Vec3 Up = new(0, 0, 1);

    /// <summary>The box (centred on its base at <paramref name="ax"/>, <paramref name="ay"/>, turned by <paramref name="rot"/>°) and its top's (u, v) → model.</summary>
    sealed record Box(Fixture F, string Id, Func<double, double, Vec3> L);

    static Box Make(double ax = 0, double ay = 0, double rot = 0)
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule(), new StudioModule());
        var id = (string)f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = new JsonArray(ax, ay, 0), ["width"] = 2, ["length"] = 2, ["height"] = 1, ["rotation"] = rot, ["storeyId"] = f.Storey })!["id"]!;
        var r = rot * Math.PI / 180;
        return new Box(f, id, (u, v) => new Vec3(ax + u * Math.Cos(r) - v * Math.Sin(r), ay + u * Math.Sin(r) + v * Math.Cos(r), 1));
    }

    /// <summary>A line (open) or a closed shape drawn on the top through the given (u, v) points, not pushed.</summary>
    static string Draw(Box b, bool open, params (double U, double V)[] pts) =>
        (string)b.F.Run("studio.onFace", new JsonObject
        {
            ["id"] = b.Id, ["point"] = J3(b.L(0.05, -0.07)), ["normal"] = J3(Up), ["open"] = open,
            ["outline"] = new JsonArray(pts.Select(p => (JsonNode)J3(b.L(p.U, p.V))).ToArray()),
        })!["sketchId"]!;

    static JsonObject Push(Box b, double u, double v, double d) =>
        b.F.Run("studio.pushPull", new JsonObject { ["id"] = b.Id, ["point"] = J3(b.L(u, v)), ["normal"] = J3(Up), ["distance"] = d })!.AsObject();

    static Solid Body(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Single().Solid;

    /// <summary>Faces back to back on one plane (opposite normals, a shared plane): the membranes a sliver leaves.</summary>
    static int Membranes(Solid s)
    {
        var n = 0;
        for (var i = 0; i < s.Faces.Length; i++)
            for (var j = i + 1; j < s.Faces.Length; j++)
            {
                var (a, b) = (s.Faces[i], s.Faces[j]);
                var (na, nb) = (a.Normal.Normalized(), b.Normal.Normalized());
                if (Vec3.Dot(na, nb) < -0.999999 && Math.Abs(Vec3.Dot(na, s.Vertices[a.Outer[0]] - s.Vertices[b.Outer[0]])) < 1e-6) n++;
            }
        return n;
    }

    static int Edges(Solid s) => Tessellator.Tessellate(s).Edges.Length / 6;

    static void Clean(Solid s, int faces, double volume)
    {
        Assert.Equal(0, Membranes(s));
        Assert.Equal(faces, s.Faces.Length);
        // A clean solid of genus 0: its drawn edges are V + F − R − 2 (Euler–Poincaré, R the holes of faces), no loose ones.
        var used = s.Faces.SelectMany(f => f.Holes.Prepend(f.Outer)).SelectMany(l => l).Distinct().Count();
        var holes = s.Faces.Sum(f => f.Holes.Length);
        Assert.Equal(used + faces - holes - 2, Edges(s));
        Assert.Equal(volume, Measure.Volume(s), 6);
    }

    static double Area(params (double U, double V)[] pts) => Math.Abs(Polygon2.SignedArea(pts.Select(p => new Vec2(p.U, p.V)).ToList()));

    static readonly (double, double) A = (0.2, 1), B = (1, -0.2);
    static readonly double Tri = Area(A, B, (1, 1));

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(12.345, -7.77, 0)]
    [InlineData(12.345, -7.77, 17)]
    [InlineData(3.7, 1.3, 30)]
    public void A_corner_cut_off_by_a_slanted_line_is_pushed_down_cleanly(double ax, double ay, double rot)
    {
        var b = Make(ax, ay, rot);
        Draw(b, true, A, B);
        Assert.Equal("cut", (string?)Push(b, 0.9, 0.9, -0.5)["how"]);
        Clean(Body(b.F, b.Id), 8, 4 - Tri * 0.5);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(12.345, -7.77, 17)]
    public void A_corner_pushed_through_is_gone(double ax, double ay, double rot)
    {
        var b = Make(ax, ay, rot);
        Draw(b, true, A, B);
        Push(b, 0.9, 0.9, -1);
        Clean(Body(b.F, b.Id), 7, 4 - Tri);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(12.345, -7.77, 17)]
    public void The_rest_of_the_top_pushed_down_leaves_the_corner_standing(double ax, double ay, double rot)
    {
        var b = Make(ax, ay, rot);
        Draw(b, true, A, B);
        Push(b, -0.5, -0.5, -0.5);
        Clean(Body(b.F, b.Id), 8, 4 - (4 - Tri) * 0.5);
    }

    [Fact]
    public void A_corner_pulled_up_is_a_solid_on_the_face()
    {
        var b = Make();
        Draw(b, true, A, B);
        var made = (string)Push(b, 0.9, 0.9, 0.5)["id"]!;
        Clean(Body(b.F, b.Id), 6, 4);
        Clean(Body(b.F, made), 5, Tri * 0.5);
    }

    [Fact]
    public void A_line_from_edge_to_opposite_edge_makes_a_step()
    {
        var b = Make();
        Draw(b, true, (-0.3, -1), (0.4, 1));
        Push(b, 0.8, 0, -0.5);
        Clean(Body(b.F, b.Id), 8, 4 - Area((-0.3, -1), (1, -1), (1, 1), (0.4, 1)) * 0.5);
    }

    [Fact]
    public void A_closed_loop_inside_the_face_is_a_pocket()
    {
        var b = Make(12.345, -7.77, 17);
        Draw(b, false, (-0.5, -0.5), (0.3, -0.5), (0.3, 0.4), (-0.5, 0.4));
        Push(b, 0, 0, -0.5);
        Clean(Body(b.F, b.Id), 11, 4 - 0.8 * 0.9 * 0.5);
    }

    [Fact]
    public void A_rectangle_touching_one_side_is_a_notch()
    {
        var b = Make(12.345, -7.77, 17);
        Draw(b, false, (-0.5, -1), (0.3, -1), (0.3, 0.4), (-0.5, 0.4));
        Push(b, 0, 0, -0.5);
        Clean(Body(b.F, b.Id), 10, 4 - 0.8 * 1.4 * 0.5);
    }

    [Fact]
    public void Two_lines_cut_off_two_corners()
    {
        var b = Make(12.345, -7.77, 17);
        Draw(b, true, A, B);
        Draw(b, true, (-1, 0.2), (-0.2, -1));
        Push(b, 0.9, 0.9, -0.5);
        Push(b, -0.9, -0.9, -0.5);
        Clean(Body(b.F, b.Id), 10, 4 - Tri * 0.5 - Area((-1, 0.2), (-0.2, -1), (-1, -1)) * 0.5);
    }

    [Fact]
    public void No_outline_of_the_region_pushed_hangs_over_the_part_taken_away()
    {
        var b = Make(12.345, -7.77, 17);
        var line = Draw(b, true, A, B);
        var cut = (string)Push(b, 0.9, 0.9, -0.5)["id"]!;
        var region = b.F.Model.Get(cut)!.Str("profile")!;
        var render = b.F.Session.Snapshot().Render;
        // The region's outline is the cut's sketch: kept, editable on its plane, but drawn in 3D no more.
        Assert.NotNull(b.F.Model.Get(region));
        Assert.True(StudioModule.ConsumedOnFace(b.F.Model.Get(region)!, b.F.Model));
        Assert.DoesNotContain(render, r => r.NodeId == region && r.Lines is { Length: > 0 });
        // The line drawn stays: it lies on the cut's top edge.
        Assert.Contains(render, r => r.NodeId == line && r.Lines is { Length: > 0 });
        // Nothing drawn in space passes over the corner taken away, at the top's height.
        var r = 17 * Math.PI / 180;
        Vec2 Uv(double x, double y) { var (dx, dy) = (x - 12.345, y + 7.77); return new(dx * Math.Cos(r) + dy * Math.Sin(r), -dx * Math.Sin(r) + dy * Math.Cos(r)); }
        var tri = new List<Vec2> { new(0.2, 1), new(1, -0.2), new(1, 1) };
        foreach (var item in render.Where(x => x.Lines is { Length: > 0 }))
            for (var k = 0; k < item.Lines!.Length; k += 6)
                for (var t = 0.1; t < 1; t += 0.2)
                {
                    var (x, y, z) = (item.Lines[k] + (item.Lines[k + 3] - item.Lines[k]) * t, item.Lines[k + 1] + (item.Lines[k + 4] - item.Lines[k + 1]) * t, item.Lines[k + 2] + (item.Lines[k + 5] - item.Lines[k + 2]) * t);
                    var p = Uv(x, y);
                    var inside = Polygon2.Contains(tri, p) && tri.Select((a, i) => Geo.NearestOnSegment(p, a, tri[(i + 1) % 3]).Dist).Min() > 1e-3;
                    Assert.False(Math.Abs(z - 1) < 1e-3 && inside, $"a line of {item.NodeId} hangs over the corner taken away at ({x}, {y}, {z})");
                }
    }

    [Fact]
    public void A_wall_face_pushed_in_is_a_cut_of_its_true_size()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule(), new StudioModule());
        var wall = f.Wall(0, 0, 4, 0, f.Type("Block", ("Concrete", 0.3, true)));
        var before = f.Session.Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid));
        var box = f.Bounds(wall);
        var h = box.Max.Z - box.Min.Z;
        var cut = (string)f.Run("studio.pushPull", new JsonObject { ["id"] = wall, ["point"] = J3(new Vec3(2, box.Min.Y, 1)), ["normal"] = J3(new Vec3(0, -1, 0)), ["distance"] = -0.1 })!["id"]!;
        // The cut is the face pushed 0.1 in, no hair more: its body's volume and the wall's loss are the same, exactly.
        var cutBody = StudioModule.Geometry(f.Model.Get(cut)!, f.Model, null)!.Parts.Single().Solid;
        Assert.Equal(4 * 0.1 * h, Measure.Volume(cutBody), 9);
        var wallBody = f.Session.Geometry(wall)!.Parts.Single().Solid;
        Clean(wallBody, 6, before - 4 * 0.1 * h);
    }

    [Fact]
    public void A_face_plane_lying_level_or_upright_has_exact_axes()
    {
        foreach (var n in new[] { new Vec3(0, 0, 1), new Vec3(0, 0, -1), new Vec3(1, 0, 0), new Vec3(0, -1, 0) })
        {
            var p = RefPlanes.Of(new Entity("v", Views.Class, "v", RefPlanes.OnFace(new Vec3(1, 2, 3), n)));
            foreach (var c in new[] { p.Normal, p.Right, p.Up })
                foreach (var x in new[] { c.X, c.Y, c.Z })
                    Assert.True(x == 0 || Math.Abs(x) == 1, $"{n}: axis component {x:R}");
            Assert.Equal(n, p.Normal);
        }
    }

    /// <summary>A cutter built through a level plane's rounding (its faces a hair off the box's and tilted): clean once proud.</summary>
    [Fact]
    public void A_cutter_flush_with_its_target_is_moved_proud_and_cuts_cleanly()
    {
        var mb = new Lapin.Booleans.ManifoldBooleans();
        static Solid Prism3(IEnumerable<Vec3> ring, Vec3 by) => Prism.Extrude(ring.ToList(), null, by)!;
        var box = Prism3([new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)], new Vec3(0, 0, 1));
        // A level plane's axes as the trigonometry gives them (cos 90° = 6e-17), points taken through it and back.
        var n = new Vec3(Math.Cos(Math.PI / 2) * Math.Cos(-Math.PI / 2), Math.Cos(Math.PI / 2) * Math.Sin(-Math.PI / 2), 1).Normalized();
        var x0 = new Vec3(-Math.Sin(-Math.PI / 2), Math.Cos(-Math.PI / 2), 0);
        var pl = new WorkPlane(new Vec3(0.1, 0.1, 1), x0, Vec3.Cross(n, x0).Normalized(), n);
        List<Vec3> Noisy(params (double X, double Y)[] r) => r.Select(q => pl.World(pl.Local(new Vec3(q.X, q.Y, 1)).At)).ToList();
        var tri = Prism.Extrude(Noisy((0.2, 1), (1, -0.2), (1, 1)), null, pl.Normal * -0.5)!;
        var proud = Flush.Proud(tri, box);
        Assert.NotSame(tri, proud);
        var s = mb.Difference(box, [proud])!;
        Clean(s, 8, 4 - Tri * 0.5);
        var through = mb.Difference(box, [Flush.Proud(Prism.Extrude(Noisy((0.2, 1), (1, -0.2), (1, 1)), null, pl.Normal * -1)!, box)])!;
        Clean(through, 7, 4 - Tri);
        // A cutter inside the box, touching none of its faces, is left as it is.
        var inner = Prism3([new(-0.5, -0.5, 0.2), new(0.5, -0.5, 0.2), new(0.5, 0.5, 0.2), new(-0.5, 0.5, 0.2)], new Vec3(0, 0, 0.5));
        Assert.Same(inner, Flush.Proud(inner, box));
    }

    [Fact]
    public void A_union_tool_resting_on_its_target_is_sunk_in_and_joins_cleanly()
    {
        var mb = new Lapin.Booleans.ManifoldBooleans();
        static Solid Prism3(IEnumerable<Vec3> ring, Vec3 by) => Prism.Extrude(ring.ToList(), null, by)!;
        var box = Prism3([new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)], new Vec3(0, 0, 1));
        var top = Prism3([new(-0.5, -0.5, 1), new(0.5, -0.5, 1), new(0.5, 0.5, 1), new(-0.5, 0.5, 1)], new Vec3(0, 0, 0.5));
        var sunk = Flush.Sunk(top, box);
        Assert.NotSame(top, sunk);
        Clean(mb.Union([box, sunk], null)!, 11, 4 + 0.5);
        // Overhanging the box, it is not sunk: it would stick out under it.
        var over = Prism3([new(0.5, -0.5, 1), new(1.5, -0.5, 1), new(1.5, 0.5, 1), new(0.5, 0.5, 1)], new Vec3(0, 0, 0.5));
        Assert.Same(over, Flush.Sunk(over, box));
    }
}
