using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Graphs;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The geometry nodes a twisted tower needs (Rotate, Scale, Centre, a circle's start angle) and the tower itself, baked:
/// storeys from a series of levels, an outline turned a little more on each, a floor on each storey from the list of
/// outlines (a list of polylines on a command's points maps once per outline), one undo step. The ready graph is the
/// bundled example ui/src/modules/graphs/examples/twisted-tower.graph.json (the one the Graphs panel starts from).
/// </summary>
public sealed class GraphTowerTests(ITestOutputHelper output)
{
    static Fixture Project()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new GraphsModule());
        f.Run("storey.create", []);
        return f;
    }

    static JsonObject N(string id, string kind, JsonObject? values = null)
    {
        var o = new JsonObject { ["id"] = id, ["kind"] = kind, ["x"] = 0, ["y"] = 0 };
        if (values != null) o["values"] = values;
        return o;
    }

    static JsonObject W(string from, string output, string to, string input) => new() { ["from"] = from, ["out"] = output, ["to"] = to, ["in"] = input };

    static JsonObject G(JsonObject[] nodes, params JsonObject[] wires) => new()
    {
        ["format"] = 2, ["nodes"] = new JsonArray(nodes.Cast<JsonNode>().ToArray()), ["wires"] = new JsonArray(wires.Cast<JsonNode>().ToArray()),
    };

    static Evaluator Run(Fixture f, JsonObject graph) =>
        new Evaluator(GraphDoc.Read(graph), f.Model, (ModuleCatalog)f.Session.Reader.Services, null, f.Session.Reader, GraphLimits.PreviewTime).RunAll();

    static object? Out(Evaluator e, string node, string port)
    {
        Assert.Null(e.Results[node].Error);
        return e.Results[node].Outputs[port];
    }

    static void Near(Vec3 want, Vec3 got)
    {
        Assert.Equal(want.X, got.X, 9);
        Assert.Equal(want.Y, got.Y, 9);
        Assert.Equal(want.Z, got.Z, 9);
    }

    [Fact]
    public void Rotate_and_scale_take_points_and_polylines_about_a_centre_and_map_over_a_list_of_angles()
    {
        var f = Project();
        var e = Run(f, G(
            [N("p", "point", new() { ["x"] = 2, ["y"] = 0, ["z"] = 1 }), N("c", "point", new() { ["x"] = 1 }),
             N("r", "rotate", new() { ["angle"] = 90 }), N("rc", "rotate", new() { ["angle"] = 180 }),
             N("box", "rectangle", new() { ["width"] = 4, ["depth"] = 2 }), N("mid", "center"),
             N("angles", "series", new() { ["start"] = 0, ["step"] = 30, ["count"] = 4 }), N("many", "rotate"),
             N("s", "scale", new() { ["factor"] = 2 }), N("sz", "scale", new() { ["factor"] = 2, ["zfactor"] = 1 }), N("half", "scale", new() { ["factor"] = 0.5 }),
             N("ring", "circle", new() { ["radius"] = 1, ["count"] = 4, ["start"] = 45 }), N("ring0", "circle", new() { ["radius"] = 1, ["count"] = 4 })],
            W("p", "point", "r", "geometry"), W("p", "point", "rc", "geometry"), W("c", "point", "rc", "center"),
            W("box", "line", "mid", "geometry"), W("box", "line", "many", "geometry"), W("mid", "point", "many", "center"), W("angles", "list", "many", "angle"),
            W("p", "point", "s", "geometry"), W("p", "point", "sz", "geometry"), W("box", "line", "half", "geometry"), W("mid", "point", "half", "center")));

        // About the origin by default, about the centre given; the height kept.
        Near(new Vec3(0, 2, 1), (Vec3)Out(e, "r", "geometry")!);
        Near(new Vec3(0, 0, 1), (Vec3)Out(e, "rc", "geometry")!);
        // A rectangle's centre is the middle of its box.
        Near(new Vec3(2, 1, 0), (Vec3)Out(e, "mid", "point")!);
        // A list of angles × one outline: one turned outline for each angle, closed as it was.
        var many = (List<object?>)Out(e, "many", "geometry")!;
        Assert.Equal(4, many.Count);
        var third = (Polyline)many[3]!;
        Assert.True(third.Closed);
        Near(new Vec3(2, 1, 0) + Turn(new Vec3(-2, -1, 0), 90), third.Points[0]);
        // Scale: by the factor every way; a Z factor of its own keeps the heights apart.
        Near(new Vec3(4, 0, 2), (Vec3)Out(e, "s", "geometry")!);
        Near(new Vec3(4, 0, 1), (Vec3)Out(e, "sz", "geometry")!);
        var half = (Polyline)Out(e, "half", "geometry")!;
        Near(new Vec3(1, 0.5, 0), half.Points[0]);
        Near(new Vec3(3, 1.5, 0), half.Points[2]);
        // A circle's start angle: 0 keeps the first point along X, as before.
        Near(new Vec3(1, 0, 0), (Vec3)((List<object?>)Out(e, "ring0", "points")!)[0]!);
        Near(new Vec3(Math.Sqrt(0.5), Math.Sqrt(0.5), 0), (Vec3)((List<object?>)Out(e, "ring", "points")!)[0]!);

        // Something that is not a point nor a polyline is refused, saying what it got.
        var bad = Run(f, G([N("t", "text", new() { ["value"] = "x" }), N("r", "rotate")], W("t", "value", "r", "geometry")));
        Assert.Contains("points or polylines", bad.Results["r"].Error);
    }

    static Vec3 Turn(Vec3 v, double degrees)
    {
        var a = degrees * Math.PI / 180;
        return new Vec3(v.X * Math.Cos(a) - v.Y * Math.Sin(a), v.X * Math.Sin(a) + v.Y * Math.Cos(a), v.Z);
    }

    const int Floors = 12;
    const double FloorHeight = 3.2, Twist = 90, Width = 20;

    /// <summary>
    /// The tower in its fewest nodes: storeys at a series of levels; a square centred on the origin turned by index ×
    /// (twist / floors), scaled by <paramref name="taper"/> from 1 at the bottom when given; a floor on each storey.
    /// </summary>
    static JsonObject Tower(double? taper = null)
    {
        var nodes = new List<JsonObject>
        {
            N("levels", "series", new() { ["start"] = 0, ["step"] = FloorHeight, ["count"] = Floors }),
            N("storeys", "command:storey.create", new() { ["height"] = FloorHeight }),
            N("square", "rectangle", new() { ["origin"] = new JsonObject { ["$pt"] = new JsonArray(-Width / 2, -Width / 2, 0) }, ["width"] = Width, ["depth"] = Width }),
            N("angles", "series", new() { ["start"] = 0, ["step"] = Twist / Floors, ["count"] = Floors }),
            N("turned", "rotate"),
            N("floor", "command:architecture.addSlab"),
        };
        var wires = new List<JsonObject>
        {
            W("levels", "list", "storeys", "elevation"), W("angles", "list", "turned", "angle"),
            W("turned", "geometry", "floor", "points"), W("storeys", "ids", "floor", "storeyId"),
        };
        if (taper is { } t)
        {
            nodes.Add(N("sizes", "series", new() { ["start"] = 1, ["step"] = (t - 1) / (Floors - 1), ["count"] = Floors }));
            nodes.Add(N("scaled", "scale"));
            wires.AddRange([W("square", "line", "scaled", "geometry"), W("sizes", "list", "scaled", "factor"), W("scaled", "geometry", "turned", "geometry")]);
        }
        else wires.Add(W("square", "line", "turned", "geometry"));
        return G([.. nodes], [.. wires]);
    }

    /// <summary>The floors baked, by their storeys' levels: (level, contour).</summary>
    static List<(double Level, IReadOnlyList<Vec2> Contour)> Floorplates(Fixture f) =>
        f.Model.OfClass(IfcClass.Slab).Select(s => (Spatial.StoreyOf(f.Model, s.Id)!.Num("elevation"), Slabs.Contour(s))).OrderBy(x => x.Item1).ToList();

    static void Outline(IReadOnlyList<Vec2> got, double half, double degrees)
    {
        Assert.Equal(4, got.Count);
        // The square's corners turned, in the order drawn (anticlockwise, from the corner at -half, -half).
        Vec3[] corners = [new(-half, -half, 0), new(half, -half, 0), new(half, half, 0), new(-half, half, 0)];
        for (var i = 0; i < 4; i++)
        {
            var want = Turn(corners[i], degrees);
            Assert.Equal(want.X, got[i].X, 6);
            Assert.Equal(want.Y, got[i].Y, 6);
        }
    }

    [Fact]
    public void A_twisted_tower_is_baked_a_floor_on_each_storey_each_turned_a_little_more_in_one_undo_step()
    {
        var f = Project();
        var storeysBefore = Spatial.Storeys(f.Model).Count();
        var undo = f.Session.History.UndoLabel;
        var r = f.Run("graphs.bake", new JsonObject { ["graph"] = Tower() })!;
        Assert.Equal("Bake graph", f.Session.History.UndoLabel);
        Assert.Equal(storeysBefore + Floors, Spatial.Storeys(f.Model).Count());

        var plates = Floorplates(f);
        Assert.Equal(Floors, plates.Count);
        for (var i = 0; i < Floors; i++)
        {
            Assert.Equal(i * FloorHeight, plates[i].Level, 6);
            Outline(plates[i].Contour, Width / 2, i * Twist / Floors);
        }
        // Each floor on a storey of its own.
        Assert.Equal(Floors, f.Model.OfClass(IfcClass.Slab).Select(s => Spatial.StoreyOf(f.Model, s.Id)!.Id).Distinct().Count());
        // What was made: the storeys (no body of their own) and the floors.
        Assert.Equal(2 * Floors, r["made"]!.AsArray().Count);

        // One step: Undo takes the floors and the storeys back.
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(IfcClass.Slab));
        Assert.Equal(storeysBefore, Spatial.Storeys(f.Model).Count());
        Assert.Equal(undo, f.Session.History.UndoLabel);
    }

    [Fact]
    public void A_tapering_tower_scales_each_floor_from_the_full_size_at_the_bottom_to_the_taper_at_the_top()
    {
        var f = Project();
        f.Run("graphs.bake", new JsonObject { ["graph"] = Tower(taper: 0.5) });
        var plates = Floorplates(f);
        Assert.Equal(Floors, plates.Count);
        for (var i = 0; i < Floors; i++)
        {
            var k = 1 + (0.5 - 1) * i / (Floors - 1);
            Outline(plates[i].Contour, k * Width / 2, i * Twist / Floors);
        }
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(IfcClass.Slab));
    }

    static JsonObject FixtureGraph() => GraphExamplesTests.Read("twisted-tower");

    [Fact]
    public void The_ready_tower_graph_reads_its_palette_and_bakes_floors_and_columns_in_one_step()
    {
        var f = Project();
        var g = FixtureGraph();
        var doc = GraphDoc.Read(g);
        Assert.True((bool)doc.Tool!["show"]!);
        // Its palette: the five fields, in their order, with their sliders' ends.
        var fields = doc.Nodes.Where(n => n.Palette?["order"] != null).OrderBy(n => Json.Number(n.Palette!["order"])).ToList();
        Assert.Equal(["Floors", "Floor height", "Total twist (degrees)", "Width", "Taper"], fields.Select(n => n.Label));
        Assert.All(fields, n => Assert.True(Json.Number(n.Palette!["min"]) < Json.Number(n.Palette!["max"])));

        var id = (string)f.Run("graphs.save", new JsonObject { ["name"] = "Twisted tower", ["graph"] = g })!["id"]!;
        var storeys = Spatial.Storeys(f.Model).Count();
        f.Run("graphs.bake", new JsonObject { ["id"] = id });
        Assert.Equal("Bake graph", f.Session.History.UndoLabel);
        var plates = Floorplates(f);
        Assert.Equal(12, plates.Count);
        // The project's ground storey (level 0) is the first floor's: 11 storeys are new, 12 in all.
        Assert.Equal(storeys + 11, Spatial.Storeys(f.Model).Count());
        Assert.Equal(0, plates[0].Level, 6);
        Assert.Equal(3.2 * 11, plates[^1].Level, 6);
        Assert.Equal(48, f.Model.OfClass(IfcClass.Column).Count());
        // The top floor: turned by the whole twist, 11 × 90 / 11 and scaled to the taper, about the square's middle.
        var top = plates[^1].Contour;
        var mid = new Vec2(12, 12);
        Assert.Equal(0.7 * 24 / Math.Sqrt(2) * 1, (top[0] - mid).Length, 6);
        var turn = Math.Atan2(top[1].Y - top[0].Y, top[1].X - top[0].X) * 180 / Math.PI;
        Assert.Equal(90.0, turn, 6);
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(IfcClass.Slab));
        Assert.Empty(f.Model.OfClass(IfcClass.Column));
        Assert.Equal(storeys, Spatial.Storeys(f.Model).Count());

        // From the palette: 20 floors, no taper; one step.
        f.Run("graphs.bake", new JsonObject { ["id"] = id, ["values"] = new JsonObject { ["floors"] = 20, ["taper"] = 1 } });
        Assert.Equal(20, f.Model.OfClass(IfcClass.Slab).Count());
        Assert.Equal(80, f.Model.OfClass(IfcClass.Column).Count());
    }

    [Theory]
    [InlineData(12)]
    [InlineData(30)]
    public void The_ready_tower_previews_in_good_time(int floors)
    {
        var f = Project();
        var id = (string)f.Run("graphs.save", new JsonObject { ["name"] = "Twisted tower", ["graph"] = FixtureGraph() })!["id"]!;
        var values = new JsonObject { ["floors"] = floors };
        // The first preview makes the model's copy; the next ones are what moving a slider costs.
        f.Session.Query("graphs.evaluate", new JsonObject { ["id"] = id, ["values"] = values.DeepClone() });
        var times = new List<double>();
        JsonObject r = null!;
        for (var i = 0; i < 3; i++)
        {
            var clock = Stopwatch.StartNew();
            r = f.Session.Query("graphs.evaluate", new JsonObject { ["id"] = id, ["values"] = values.DeepClone() })!.AsObject();
            times.Add(clock.Elapsed.TotalMilliseconds);
        }
        Assert.All(r["nodes"]!.AsObject(), n => Assert.Null(n.Value!["error"]));
        output.WriteLine($"Twisted tower, {floors} floors: preview {times.Min():0} ms (best of 3), {string.Join(", ", times.Select(t => t.ToString("0")))} ms; {r["ghost"]?["parts"]?.AsArray().Count} ghost parts");
        Assert.True(times.Min() < 10_000, $"The preview took {times.Min():0} ms.");
    }
}
