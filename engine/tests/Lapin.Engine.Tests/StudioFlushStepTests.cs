using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Platform;
using Lapin.Studio;
using static Lapin.Engine.Tests.StudioFlushCutTests;

namespace Lapin.Engine.Tests;

/// <summary>
/// Push/Pull through <see cref="Flush"/> beyond a primitive's split face (decision 519): a Studio extrusion's faces split
/// by lines and pushed, a push/pull step (the face whole, on a Studio solid) cut and added at its true size, the faces a
/// step made pushed again and then split, copies of an array set face to face, and two flush faces that cross without
/// either holding the other's middle. Each clean: the faces it must have, no membranes, the edges of a clean solid, the volume exact.
/// </summary>
public class StudioFlushStepTests
{
    static JsonArray J3(Vec3 p) => [p.X, p.Y, p.Z];
    static JsonArray J2(Vec2 p) => [p.X, p.Y];

    /// <summary>A 2 × 2 × 1 m solid centred on (ax, ay), turned by rot°, and its (u, v, z) → model.</summary>
    sealed record Body(Fixture F, string Id, double Rot, Func<double, double, double, Vec3> At)
    {
        public Vec3 Dir(double u, double v) => At(u, v, 0) - At(0, 0, 0);
    }

    static Func<double, double, double, Vec3> Frame(double ax, double ay, double rot)
    {
        var r = rot * Math.PI / 180;
        return (u, v, z) => new Vec3(ax + u * Math.Cos(r) - v * Math.Sin(r), ay + u * Math.Sin(r) + v * Math.Cos(r), z);
    }

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());

    /// <summary>A Studio extrusion: the square drawn on a plan, extruded 1 m up.</summary>
    static Body Extrusion(double ax, double ay, double rot)
    {
        var f = F();
        var at = Frame(ax, ay, rot);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        Vec2 P(double u, double v) { var q = at(u, v, 0); return new(q.X, q.Y); }
        var shape = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J2(P(-1, -1)), J2(P(1, -1)), J2(P(1, 1)), J2(P(-1, 1))), ["closed"] = true };
        var profile = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = shape })!["ids"]![0]!;
        var id = (string)f.Run("studio.extrude", new JsonObject { ["profileId"] = profile, ["depth"] = 1 })!["id"]!;
        Assert.Equal("extrude", f.Model.Get(id)!.Str("operation"));
        return new Body(f, id, rot, at);
    }

    /// <summary>A Studio box primitive (no sketch: a push on its face is a step).</summary>
    static Body Primitive(double ax, double ay, double rot)
    {
        var f = F();
        var id = (string)f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = new JsonArray(ax, ay, 0), ["width"] = 2, ["length"] = 2, ["height"] = 1, ["rotation"] = rot, ["storeyId"] = f.Storey })!["id"]!;
        return new Body(f, id, rot, Frame(ax, ay, rot));
    }

    /// <summary>A line (open) or closed shape drawn on the face through <paramref name="on"/> facing <paramref name="n"/>.</summary>
    static void Draw(Body b, Vec3 on, Vec3 n, bool open, params Vec3[] pts) =>
        b.F.Run("studio.onFace", new JsonObject { ["id"] = b.Id, ["point"] = J3(on), ["normal"] = J3(n), ["open"] = open, ["outline"] = new JsonArray(pts.Select(p => (JsonNode)J3(p)).ToArray()) });

    static JsonObject Push(Body b, Vec3 at, Vec3 n, double d) =>
        b.F.Run("studio.pushPull", new JsonObject { ["id"] = b.Id, ["point"] = J3(at), ["normal"] = J3(n), ["distance"] = d })!.AsObject();

    static Solid Solid(Body b) => b.F.Session.Geometry(b.Id)!.Parts.Single().Solid;

    static readonly Vec3 Up = new(0, 0, 1);
    static readonly (double, double) A = (0.2, 1), B = (1, -0.2);
    static readonly double Tri = Area(A, B, (1, 1));

    [Theory]
    [InlineData(0, 0, 0, -0.5)]
    [InlineData(12.345, -7.77, 17, -0.5)]
    [InlineData(3.7, 1.3, 30, -1)]
    [InlineData(12.345, -7.77, 17, -1)]
    [InlineData(0, 0, 0, 0.5)]
    [InlineData(12.345, -7.77, 17, 0.5)]
    public void An_extrusions_top_split_by_a_line_is_pushed_or_pulled_cleanly(double ax, double ay, double rot, double d)
    {
        var b = Extrusion(ax, ay, rot);
        Draw(b, b.At(0.05, -0.07, 1), Up, true, b.At(A.Item1, A.Item2, 1), b.At(B.Item1, B.Item2, 1));
        var r = Push(b, b.At(0.9, 0.9, 1), Up, d);
        if (d > 0)
        {
            Clean(Solid(b), 6, 4);
            Clean(b.F.Session.Geometry((string)r["id"]!)!.Parts.Single().Solid, 5, Tri * d);
        }
        else if (d <= -1) Clean(Solid(b), 7, 4 - Tri);
        else Clean(Solid(b), 8, 4 + Tri * d);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(12.345, -7.77, 17)]
    public void An_extrusions_side_split_by_an_upright_line_is_pushed_in_cleanly(double ax, double ay, double rot)
    {
        var b = Extrusion(ax, ay, rot);
        var south = b.Dir(0, -1).Normalized();
        Draw(b, b.At(-0.5, -1, 0.5), south, true, b.At(0.3, -1, 0), b.At(0.3, -1, 1));
        Assert.Equal("cut", (string?)Push(b, b.At(0.6, -1, 0.5), south, -0.2)["how"]);
        // The side's part east of the line taken 0.2 in, the whole height: the side in two, set back, and the step between.
        Clean(Solid(b), 8, 4 - 0.7 * 0.2 * 1);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(12.345, -7.77, 17)]
    [InlineData(3.7, 1.3, 30)]
    public void Steps_push_and_pull_whole_faces_cleanly_and_a_face_a_step_made_takes_a_split_and_a_push(double ax, double ay, double rot)
    {
        var b = Primitive(ax, ay, rot);
        var south = b.Dir(0, -1).Normalized();
        // The top pulled 0.3: a step, the prism on it the face's own size, sunk into the body a hair.
        Assert.Equal("step", (string?)Push(b, b.At(0.1, 0.2, 1), Up, 0.3)["how"]);
        Clean(Solid(b), 6, 4 * 1.3);
        // The top the step made pushed 0.5 down: another step, the cut its true size, moved proud a hair.
        Assert.Equal("step", (string?)Push(b, b.At(-0.3, 0.4, 1.3), Up, -0.5)["how"]);
        Clean(Solid(b), 6, 4 * 0.8);
        // A side pushed 0.2 in, whole: the cut's top and bottom and both ends lie on the body's faces.
        Assert.Equal("step", (string?)Push(b, b.At(0.2, -1, 0.4), south, -0.2)["how"]);
        Clean(Solid(b), 6, 2 * 1.8 * 0.8);
        // The top the steps left, split by a line and its corner pushed down 0.3.
        Draw(b, b.At(0.05, 0.07, 0.8), Up, true, b.At(A.Item1, A.Item2, 0.8), b.At(B.Item1, B.Item2, 0.8));
        Assert.Equal("cut", (string?)Push(b, b.At(0.9, 0.9, 0.8), Up, -0.3)["how"]);
        Clean(Solid(b), 8, 2 * 1.8 * 0.8 - Tri * 0.3);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(12.345, -7.77, 17)]
    public void A_pull_step_on_part_of_a_face_then_a_push_on_it_again(double ax, double ay, double rot)
    {
        var b = Primitive(ax, ay, rot);
        // A step pulled on the top (whole), then its new top split by a closed square and the square pushed through the pulled part.
        Push(b, b.At(0, 0, 1), Up, 0.5);
        Clean(Solid(b), 6, 6);
        Draw(b, b.At(0.9, 0.9, 1.5), Up, false, b.At(-0.5, -0.5, 1.5), b.At(0.5, -0.5, 1.5), b.At(0.5, 0.5, 1.5), b.At(-0.5, 0.5, 1.5));
        Push(b, b.At(0, 0, 1.5), Up, -0.5);
        Clean(Solid(b), 11, 6 - 0.5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void An_arrays_copies_set_face_to_face_join_into_one_clean_body(double rot)
    {
        var b = Primitive(12.345, -7.77, rot);
        var step = b.Dir(2, 0);
        b.F.Run("studio.addFeature", new JsonObject { ["id"] = b.Id, ["feature"] = new JsonObject { ["op"] = "array", ["count"] = 3, ["step"] = J3(step) } });
        Clean(Solid(b), 6, 12);
    }

    /// <summary>
    /// A cut priority (decision 519): a wall turned 17° cuts a Studio block drawn flush with its face and its base, ranked
    /// under it; the wall's envelope moved proud a hair where it lies on the block's faces, so the block is left a clean box.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void A_wall_cutting_a_block_flush_with_its_face_leaves_it_clean(double rot)
    {
        var f = F();
        var at = Frame(0, 0, rot);
        var end = at(6, 0, 0);
        var wall = f.Wall(0, 0, end.X, end.Y);
        var left = at(0, 1, 0).Normalized();
        var across = f.Session.Geometry(wall)!.Parts.SelectMany(p => p.Solid.Vertices).Select(v => Vec3.Dot(v, left)).ToList();
        var (lo, hi) = (across.Min(), across.Max());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        Vec2 P(double u, double v) { var q = at(u, v, 0); return new(q.X, q.Y); }
        var shape = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J2(P(2, lo)), J2(P(3, lo)), J2(P(3, 1)), J2(P(2, 1))), ["closed"] = true };
        var profile = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = shape })!["ids"]![0]!;
        var block = (string)f.Run("studio.extrude", new JsonObject { ["profileId"] = profile, ["depth"] = 0.5 })!["id"]!;
        Clean(b(block), 6, 1 * (1 - hi) * 0.5);
        Solid b(string id) => f.Session.Geometry(id)!.Parts.Single().Solid;
    }

    /// <summary>
    /// Two flush faces crossing like a plus, each a long thin rectangle: neither holds a centroid of the other's triangles,
    /// yet they share a square. The cutter's top, a hair off the bar's top as a plane's rounding leaves it, is moved proud.
    /// </summary>
    [Fact]
    public void Flush_faces_crossing_without_holding_each_others_middle_overlap()
    {
        var mb = new Lapin.Booleans.ManifoldBooleans();
        static Solid Prism3(IEnumerable<Vec3> ring, Vec3 by) => Prism.Extrude(ring.ToList(), null, by)!;
        var bar = Prism3([new(-1, -0.1, 0), new(1, -0.1, 0), new(1, 0.1, 0), new(-1, 0.1, 0)], new Vec3(0, 0, 1));
        // Across it, its top at 1 give or take the rounding a plane's sines leave (1e-12), down to 0.5.
        var top = 1 + 3e-12;
        var cross = Prism3([new(-0.1, -1, top), new(0.1, -1, top - 2e-12), new(0.1, 1, top), new(-0.1, 1, top + 1e-12)], new Vec3(0, 0, -0.5 - 3e-12));
        var proud = Flush.Proud(cross, bar);
        Assert.NotSame(cross, proud);
        Assert.Equal(1 + Flush.By, proud.Vertices.Max(v => v.Z), 9);
        // A slot across the bar, the whole width: the top in two, each long side a U, the slot's two walls and its floor.
        Clean(mb.Difference(bar, [proud])!, 10, 2 * 0.2 * 1 - 0.2 * 0.2 * 0.5);
        // A face on the bar's top plane that only meets it along an edge shares no area: left as it is.
        var beside = Prism3([new(1, -0.1, 1), new(1.5, -0.1, 1), new(1.5, 0.1, 1), new(1, 0.1, 1)], new Vec3(0, 0, -0.5));
        Assert.Same(beside, Flush.Proud(beside, bar));
        // Nor one that meets it at a corner only.
        var corner = Prism3([new(1, 0.1, 1), new(1.5, 0.1, 1), new(1.5, 0.5, 1), new(1, 0.5, 1)], new Vec3(0, 0, -0.5));
        Assert.Same(corner, Flush.Proud(corner, bar));
    }
}
