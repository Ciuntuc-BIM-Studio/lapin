using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Precision against display speed (docs/plan-display-precision.md): curves made into pieces by how far a
/// chord strays (<see cref="Chords"/>), and the screen's coarser copies of heavy parts (<see cref="MeshSimplify"/>),
/// each held to the error it says it has.
/// </summary>
public class DisplayPrecisionTests
{
    static double Sagitta(double r, double step) => r * (1 - Math.Cos(step / 2));

    [Fact]
    public void A_chord_strays_at_most_a_millimetre_and_a_step_turns_at_most_15_degrees()
    {
        foreach (var r in new[] { 0.01, 0.025, 0.15, 0.5, 2, 10, 50, 100 })
        {
            var n = Chords.Circle(r);
            Assert.True(Sagitta(r, 2 * Math.PI / n) <= Chords.Tolerance + 1e-12, $"r {r}: {n} sides");
            Assert.True(n >= 24);
        }
        // A pipe of 50 mm: 24 sides (it had 12, folding 30° and drawing a line between each); a 10 m wall: as true as the pipe.
        Assert.Equal(24, Chords.Circle(0.025));
        Assert.Equal(223, Chords.Circle(10));
        // Past about 105 m the step stops at half a degree.
        Assert.Equal(720, Chords.Circle(1000));
        // A thin bar may be lighter when asked: never under its minimum.
        Assert.Equal(8, Chords.Count(0.006, 2 * Math.PI, 8, Math.PI / 4));
    }

    [Fact]
    public void A_curved_walls_axis_strays_at_most_a_millimetre_from_its_arc()
    {
        // A chord of 8 m rising 1 m: a radius of 8.5 m (by 5° steps it strayed 8 mm).
        var c = new PathCurve(new Vec2(0, 0), new Vec2(8, 0), 1);
        Assert.Equal(8.5, c.Radius, 9);
        var s = c.Stations(0, c.Length);
        for (var i = 0; i + 1 < s.Count; i++)
        {
            var (a, b) = (c.PointAt(s[i]), c.PointAt(s[i + 1]));
            var mid = (a + b) * 0.5;
            Assert.True(c.Radius - (mid - c.Centre).Length <= Chords.Tolerance + 1e-9);
        }
        // And the wall built on it: its body's footprint within the millimetre too.
        var f = new Fixture(new ArchitectureModule());
        var wall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 8, ["y2"] = 0, ["arc"] = 1, ["height"] = 3 })!["id"]!;
        Assert.NotNull(f.Session.Geometry(wall));
    }

    [Fact]
    public void A_cylinder_is_within_the_tolerance_of_a_true_one_and_its_shell_keeps_even_walls()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule(), new StudioModule());
        var id = (string)f.Run("studio.primitive", new JsonObject { ["shape"] = "cylinder", ["at"] = new JsonArray(0, 0), ["radius"] = 0.5, ["height"] = 2 })!["id"]!;
        var v = f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
        var exact = Math.PI * 0.25 * 2;
        // Under the true volume by the polygon's shortfall: under 0.3 % (32 sides lost 0.6 %).
        Assert.InRange((exact - v) / exact, 0, 0.003);
    }

    static TriMesh SphereMesh(double r, out Vec3 centre)
    {
        var res = Solids.Primitive("sphere", Vec3.Zero, 0, 0, 0, 0, r, 0);
        centre = new Vec3(0, 0, r);
        return Tessellator.Tessellate(res.Solid!);
    }

    [Fact]
    public void A_heavy_part_gets_coarser_copies_each_within_its_error()
    {
        var m = SphereMesh(1, out var c);
        var tris = m.Indices.Length / 3;
        Assert.True(tris > 4000, $"{tris} triangles");
        var levels = MeshSimplify.Levels(m);
        Assert.NotNull(levels);
        var last = tris;
        foreach (var level in levels!)
        {
            // A copy is three corners a triangle, no normals nor indices.
            Assert.Empty(level.Mesh.Indices);
            Assert.Empty(level.Mesh.Normals);
            var l = level with { Mesh = Indexed(level.Mesh) };
            var n = l.Mesh.Indices.Length / 3;
            Assert.True(n <= last * 0.6, $"{n} after {last}");
            last = n;
            // Every corner on the surface within the level's error (the sphere is convex: corners can only fall inside it).
            for (var i = 0; i < l.Mesh.Positions.Length; i += 3)
            {
                var p = new Vec3(l.Mesh.Positions[i], l.Mesh.Positions[i + 1], l.Mesh.Positions[i + 2]);
                Assert.True(Math.Abs((p - c).Length - 1) <= l.Error + 1e-4, $"{(p - c).Length} at error {l.Error}");
            }
            // Still closed: every edge between two triangles.
            Assert.Empty(l.Mesh.Edges.Length == 0 ? [] : OpenEdges(l.Mesh));
        }
        Assert.True(levels.Count >= 2);
        // The coarsest is a small share of the fine one.
        Assert.True(last < tris / 10, $"{last} of {tris}");
    }

    /// <summary>A copy's corners numbered in order, as the interface draws them.</summary>
    static TriMesh Indexed(TriMesh m) => m.Indices.Length > 0 ? m : m with { Indices = [.. Enumerable.Range(0, m.Positions.Length / 3)] };

    /// <summary>The edges only one triangle uses.</summary>
    static List<(int, int)> OpenEdges(TriMesh m)
    {
        var at = new Dictionary<(float, float, float), int>();
        int V(int i) { var k = (m.Positions[3 * i], m.Positions[3 * i + 1], m.Positions[3 * i + 2]); if (!at.TryGetValue(k, out var v)) at[k] = v = at.Count; return v; }
        var uses = new Dictionary<(int, int), int>();
        for (var t = 0; t < m.Indices.Length; t += 3)
            for (var e = 0; e < 3; e++)
            {
                int a = V(m.Indices[t + e]), b = V(m.Indices[t + (e + 1) % 3]);
                var key = a < b ? (a, b) : (b, a);
                uses[key] = uses.GetValueOrDefault(key) + 1;
            }
        return [.. uses.Where(kv => kv.Value == 1).Select(kv => kv.Key)];
    }

    [Fact]
    public void A_flat_sheet_keeps_its_outline_and_a_light_part_has_no_copies()
    {
        // A 4 × 4 m sheet of 40 × 40 squares: flat, so it folds down to a few triangles, its corners where they were.
        var pos = new List<float>(); var idx = new List<int>();
        const int n = 40;
        for (var j = 0; j <= n; j++) for (var i = 0; i <= n; i++) { pos.Add(i * 0.1f); pos.Add(j * 0.1f); pos.Add(0); }
        for (var j = 0; j < n; j++)
            for (var i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                idx.AddRange([a, b, d, a, d, c]);
            }
        var sheet = new TriMesh([.. pos], new float[pos.Count], [.. idx], []);
        var levels = MeshSimplify.Levels(sheet);
        Assert.NotNull(levels);
        var coarse = Indexed(levels![^1].Mesh);
        Assert.True(coarse.Indices.Length / 3 < 200, $"{coarse.Indices.Length / 3} triangles");
        float Min(int k) => Enumerable.Range(0, coarse.Positions.Length / 3).Min(i => coarse.Positions[3 * i + k]);
        float Max(int k) => Enumerable.Range(0, coarse.Positions.Length / 3).Max(i => coarse.Positions[3 * i + k]);
        Assert.Equal(0, Min(0), 4); Assert.Equal(4, Max(0), 4); Assert.Equal(0, Min(1), 4); Assert.Equal(4, Max(1), 4);
        // Its area is the sheet's.
        double area = 0;
        for (var t = 0; t < coarse.Indices.Length; t += 3)
        {
            Vec3 P(int i) => new(coarse.Positions[3 * i], coarse.Positions[3 * i + 1], coarse.Positions[3 * i + 2]);
            area += Vec3.Cross(P(coarse.Indices[t + 1]) - P(coarse.Indices[t]), P(coarse.Indices[t + 2]) - P(coarse.Indices[t])).Length / 2;
        }
        Assert.Equal(16, area, 3);
        // A box of 12 triangles is drawn as it is.
        Assert.Null(MeshSimplify.Levels(Tessellator.Tessellate(Prism.ExtrudeFootprint([new(0, 0), new(1, 0), new(1, 1), new(0, 1)], 0, 1)!)));
    }

    [Fact]
    public void The_session_sends_the_copies_with_a_heavy_part_only()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule(), new StudioModule());
        var sphere = (string)f.Run("studio.primitive", new JsonObject { ["shape"] = "sphere", ["at"] = new JsonArray(0, 0), ["radius"] = 1 })!["id"]!;
        var wall = f.Wall(5, 0, 9, 0);
        var render = f.Session.Snapshot().Render;
        Assert.NotNull(render.Single(r => r.NodeId == sphere).Parts![0].Lods);
        Assert.All(render.Single(r => r.NodeId == wall).Parts!, p => Assert.Null(p.Lods));
    }

    [Fact]
    public void The_display_quality_is_the_projects_and_one_undo_step()
    {
        var f = new Fixture(new ArchitectureModule());
        Assert.Equal("balanced", DisplayQuality.Of(f.Session.Model));
        f.Run("project.setDisplayQuality", new JsonObject { ["quality"] = "fast" });
        Assert.Equal("fast", DisplayQuality.Of(f.Session.Model));
        Assert.Equal("fast", (string?)f.Session.Snapshot().Nodes.Single(n => n.Type == "IfcProject").Props["displayQuality"]);
        Assert.Throws<CommandException>(() => f.Run("project.setDisplayQuality", new JsonObject { ["quality"] = "ultra" }));
        f.Session.Undo();
        Assert.Equal("balanced", DisplayQuality.Of(f.Session.Model));
    }
}
