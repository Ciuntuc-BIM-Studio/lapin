using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The bars in 3D on request (decision 221): <c>rebar.bodies</c> gives hexagonal tubes along every bar in space, grouped by
/// colour, as the protocol's binary buffers; over the cap the rest is left out and the reply says so.
/// </summary>
public class RebarBodiesTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static JsonObject Bodies(Fixture f, JsonObject? args = null) => (JsonObject)f.Session.Query("rebar.bodies", args ?? [])!;
    static float[] Positions(JsonObject bodies, int group) => bodies["groups"]![group]!["positions"]!.GetValue<float[]>();
    static int[] Indices(JsonObject bodies, int group) => bodies["groups"]![group]!["indices"]!.GetValue<int[]>();

    /// <summary>A hooked stirrup's chain: eleven pieces, its two 135° bends five segments each, its four 90° corners three each.</summary>
    const int StirrupPoints = 26;

    [Fact]
    public void A_straight_chain_is_a_hexagonal_tube_with_its_caps()
    {
        var positions = new List<float>();
        var indices = new List<int>();
        RebarBodies.Tube([new Vec3(0, 0, 0), new Vec3(2, 0, 0)], 0.01, positions, indices);
        Assert.Equal(12 * 3, positions.Count);
        Assert.Equal(20 * 3, indices.Count);
        Assert.Equal(20, RebarBodies.TrianglesOf(2));
        Assert.All(indices, i => Assert.InRange(i, 0, 11));
        // Every vertex a radius off the axis, on one of the two rings.
        for (var i = 0; i < 12; i++)
        {
            Assert.Equal(i < 6 ? 0.0 : 2.0, positions[i * 3], 6);
            Assert.Equal(0.01, Math.Sqrt(positions[i * 3 + 1] * positions[i * 3 + 1] + positions[i * 3 + 2] * positions[i * 3 + 2]), 6);
        }
        // Appended after what is there, the indices counted on.
        RebarBodies.Tube([new Vec3(0, 0, 1), new Vec3(0, 0, 3)], 0.005, positions, indices);
        Assert.Equal(24 * 3, positions.Count);
        Assert.All(indices.Skip(60), i => Assert.InRange(i, 12, 23));
    }

    [Fact]
    public void A_bend_is_three_segments_on_its_arc_and_the_tube_keeps_its_thickness_round_it()
    {
        var spec = new RebarSpec("L", new Dictionary<string, double> { ["a"] = 1, ["b"] = 0.5 }, null, 0.012, null, null);
        var chain = RebarBodies.Sample(RebarShapes.Segments(spec));
        // The leg, three segments round the corner, the other leg: 1 + (1 + 3 + 1) points.
        Assert.Equal(6, chain.Count);
        Assert.Equal(new Vec2(0, 0), chain[0]);
        Assert.Equal(new Vec2(1, 0.5), chain[^1]);
        var r = RebarShapes.BendRadius(0.012);
        Assert.All(chain.Skip(1).Take(4), p => Assert.Equal(r, (p - new Vec2(1 - r, r)).Length, 9));
        var positions = new List<float>();
        var indices = new List<int>();
        RebarBodies.Tube(chain.Select(p => new Vec3(p.X, p.Y, 0)).ToList(), 0.006, positions, indices);
        Assert.Equal(RebarBodies.TrianglesOf(6) * 3, indices.Count);
        // The rings round the corner stay within a little of the bar's radius of the axis (halved angles widen them slightly).
        for (var i = 1; i < 5; i++)
            for (var k = 0; k < 6; k++)
            {
                var v = new Vec3(positions[(i * 6 + k) * 3], positions[(i * 6 + k) * 3 + 1], positions[(i * 6 + k) * 3 + 2]);
                var c = chain[i];
                Assert.InRange((v - new Vec3(c.X, c.Y, 0)).Length, 0.0059, 0.0061);
            }
    }

    [Fact]
    public void A_beam_s_type_stirrups_are_tubes_at_every_station_in_the_diameter_s_colour()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id, ["enabled"] = true });
        var laid = TypeStirrups.Of(f.Model, f.Model.Get(beam)!)!;
        Assert.Equal(41, laid.Stations.Count);
        var b = Bodies(f);
        Assert.False((bool)b["truncated"]!);
        Assert.Single((JsonArray)b["groups"]!);
        Assert.Equal(RebarShapes.ColorOf(0.008), (string)b["groups"]![0]!["color"]!);
        Assert.Equal(41 * RebarBodies.TrianglesOf(StirrupPoints), (int)b["triangles"]!);
        var positions = Positions(b, 0);
        var indices = Indices(b, 0);
        Assert.Equal(41 * StirrupPoints * 6 * 3, positions.Length);
        Assert.Equal(41 * RebarBodies.TrianglesOf(StirrupPoints) * 3, indices.Length);
        Assert.All(indices, i => Assert.InRange(i, 0, positions.Length / 3 - 1));
        // Inside the beam: its 0.25 m width about y = 0, the stirrup's axis 29 mm in from each face (the cover and half a Ø8), the tube up to 4 mm more (a hexagon's flat or corner).
        var ys = Enumerable.Range(0, positions.Length / 3).Select(i => positions[i * 3 + 1]).ToList();
        Assert.InRange(ys.Max(), 0.096 + 0.004 * 0.85, 0.1 + 1e-6);
        Assert.InRange(ys.Min(), -0.1 - 1e-6, -0.096 - 0.004 * 0.85);
        var xs = Enumerable.Range(0, positions.Length / 3).Select(i => positions[i * 3]).ToList();
        Assert.InRange(xs.Min(), -0.004 - 1e-6, -0.004 * 0.85);
        Assert.InRange(xs.Max(), 6.004 * 0.85, 6.004 + 1e-6);
        var top = Beams.Top(f.Model, f.Model.Get(beam)!);
        var zs = Enumerable.Range(0, positions.Length / 3).Select(i => positions[i * 3 + 2]).ToList();
        Assert.InRange(zs.Max(), top - 0.029 + 0.004 * 0.85, top - 0.025 + 1e-6);
        Assert.InRange(zs.Min(), top - 0.4 + 0.025 - 1e-6, top - 0.4 + 0.029 - 0.004 * 0.85);
        // Asked for another element alone, nothing; for the beam, the same.
        Assert.Empty((JsonArray)Bodies(f, new JsonObject { ["ids"] = new JsonArray("nothing") })["groups"]!);
        Assert.Equal(41 * RebarBodies.TrianglesOf(StirrupPoints), (int)Bodies(f, new JsonObject { ["ids"] = new JsonArray(beam) })["triangles"]!);
    }

    [Fact]
    public void A_column_s_bars_are_straight_tubes_from_its_base_past_its_top_by_the_lap()
    {
        var f = F();
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 3 })!["id"]!;
        f.Run("rebar.setColumnBars", new JsonObject { ["id"] = f.Model.RelatingOf(column, IfcClass.RelDefinesByType)!.Id, ["enabled"] = true, ["perFace"] = 3 });
        var laid = ColumnBars.Of(f.Model, f.Model.Get(column)!)!;
        Assert.Equal(8, laid.Bars.Count);
        var b = Bodies(f);
        Assert.Single((JsonArray)b["groups"]!);
        Assert.Equal(RebarShapes.ColorOf(0.016), (string)b["groups"]![0]!["color"]!);
        Assert.Equal(8 * 20, (int)b["triangles"]!);
        var positions = Positions(b, 0);
        Assert.Equal(8 * 12 * 3, positions.Length);
        var zs = Enumerable.Range(0, positions.Length / 3).Select(i => positions[i * 3 + 2]).ToList();
        var (_, _, height) = Columns.Spec(f.Model, f.Model.Get(column)!);
        var baseZ = f.Model.Get(f.Storey)!.Num("elevation");
        Assert.Equal(baseZ, zs.Min(), 6);
        Assert.Equal(baseZ + height + 1.0, zs.Max(), 6);
        // The bars inside the 0.3 m section about (2, 3): 33 mm in from the faces (the cover and half a Ø16), the tube up to 8 mm more.
        var xs = Enumerable.Range(0, positions.Length / 3).Select(i => positions[i * 3]).ToList();
        Assert.InRange(xs.Min(), 2 - 0.125 - 1e-6, 2 - 0.117 - 0.008 * 0.85);
        Assert.InRange(xs.Max(), 2 + 0.117 + 0.008 * 0.85, 2 + 0.125 + 1e-6);
        // Its stirrups on too: a second group, the stirrup's colour, with the bars' tubes untouched.
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = f.Model.RelatingOf(column, IfcClass.RelDefinesByType)!.Id, ["enabled"] = true, ["pattern"] = "cross" });
        b = Bodies(f);
        Assert.Equal(2, ((JsonArray)b["groups"]!).Count);
        var stations = TypeStirrups.Of(f.Model, f.Model.Get(column)!)!.Stations.Count;
        Assert.Equal(8 * 20 + 3 * stations * RebarBodies.TrianglesOf(StirrupPoints), (int)b["triangles"]!);
    }

    [Fact]
    public void A_hand_drawn_bar_s_copies_are_tubes_where_the_bar_stands_in_space()
    {
        var f = F();
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 })!["id"]!;
        var top = Beams.Top(f.Model, f.Model.Get(beam)!);
        var along = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(6, 0), ["scale"] = 50 })!["id"]!;
        var bar = (string)f.Run("rebar.add", new JsonObject { ["viewId"] = along, ["shape"] = "straight", ["at"] = new JsonArray(1, top - 0.36), ["params"] = new JsonObject { ["length"] = 4 }, ["diameter"] = 0.016,
            ["across"] = new JsonObject { ["count"] = 3, ["mode"] = "cover" } })!["id"]!;
        var b = Bodies(f);
        Assert.Single((JsonArray)b["groups"]!);
        Assert.Equal(RebarShapes.ColorOf(0.016), (string)b["groups"]![0]!["color"]!);
        Assert.Equal(3 * 20, (int)b["triangles"]!);
        var positions = Positions(b, 0);
        var xs = Enumerable.Range(0, positions.Length / 3).Select(i => positions[i * 3]).ToList();
        Assert.Equal(1.0, xs.Min(), 6);
        Assert.Equal(5.0, xs.Max(), 6);
        // Three copies by the cover: the outer ones 33 mm in from the beam's faces (the cover and half a Ø16, no stirrups on this type), the tube 8 mm more.
        var ys = Enumerable.Range(0, positions.Length / 3).Select(i => positions[i * 3 + 1]).ToList();
        Assert.Equal(0.100, ys.Max(), 3);
        Assert.Equal(-0.100, ys.Min(), 3);
        // The same bar put in section elsewhere stands for this one: no tube of its own.
        f.Run("rebar.duplicateAs", new JsonObject { ["id"] = bar, ["viewId"] = along, ["at"] = new JsonArray(0.5, top - 0.2) });
        Assert.Equal(3 * 20, (int)Bodies(f)["triangles"]!);
    }

    [Fact]
    public void Over_the_cap_the_rest_is_left_out_and_the_reply_says_so()
    {
        Assert.Equal(2_000_000, RebarBodies.MaxTriangles);
        var chains = Enumerable.Range(0, 8).Select(i => new RebarBodies.Chain([new Vec3(0, i, 0), new Vec3(1, i, 0)], 0.012, "#000000")).ToList();
        var whole = RebarBodies.Build(chains);
        Assert.False(whole.Truncated);
        Assert.Equal(160, whole.Triangles);
        Assert.Single(whole.Groups);
        var capped = RebarBodies.Build(chains, cap: 100);
        Assert.True(capped.Truncated);
        Assert.Equal(100, capped.Triangles);
        Assert.Equal(100, capped.Groups[0].Triangles);
        Assert.Equal(5 * 12 * 3, capped.Groups[0].Positions.Length);
        // The chains in two colours, each its own group, in the order met.
        var mixed = RebarBodies.Build([chains[0], chains[1] with { Color = "#ff0000" }, chains[2]]);
        Assert.Equal(["#000000", "#ff0000"], mixed.Groups.Select(g => g.Color));
        Assert.Equal([40, 20], mixed.Groups.Select(g => g.Triangles));
    }

    [Fact]
    public void The_arrays_travel_as_the_frame_s_binary_buffers_by_the_generated_contracts_alone()
    {
        var result = RebarBodies.Build([new RebarBodies.Chain([new Vec3(0, 0, 0), new Vec3(1, 0, 0)], 0.012, "#0f766e")]);
        var frame = BinaryFrame.Encode(new Reply(JsonValue.Create(1), result.ToJson()), contractsOnly: true);
        var (header, buffers) = BinaryFrame.Decode(frame);
        var group = header.RootElement.GetProperty("result").GetProperty("groups")[0];
        Assert.Equal("#0f766e", group.GetProperty("color").GetString());
        Assert.Equal(JsonValueKind.Object, group.GetProperty("positions").ValueKind);
        Assert.Equal("f32", group.GetProperty("positions").GetProperty("t").GetString());
        Assert.Equal("i32", group.GetProperty("indices").GetProperty("t").GetString());
        Assert.Equal(result.Groups[0].Positions, BinaryFrame.Floats(group.GetProperty("positions"), buffers));
        Assert.Equal(result.Groups[0].Indices, BinaryFrame.Ints(group.GetProperty("indices"), buffers));
        Assert.Equal(20, header.RootElement.GetProperty("result").GetProperty("triangles").GetInt32());
        // Written as plain text (a log, the MCP bridge), the arrays are JSON numbers.
        var text = JsonNode.Parse(result.ToJson().ToJsonString())!;
        Assert.Equal(36, ((JsonArray)text["groups"]![0]!["positions"]!).Count);
    }
}
