using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Gutters and downpipes made parametric (decision 235): the gutter's size, offset, drop, fall and end caps, on or off per eave
/// edge; the automatic downpipes made elements tied to a gutter's station, with their route, diameter and shoe; IFC.
/// </summary>
public class DrainageTests
{
    static JsonArray House10x6 => new(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6));

    static string AddRoof(Fixture f, string style = "fr_provence") =>
        (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = House10x6, ["style"] = style })!["id"]!;

    static void Set(Fixture f, string roof, JsonObject attrs) => f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = attrs });

    static double Q(Fixture f, string id, string name) => f.Session.Quantities(id).SelectMany(s => s.Quantities).Where(q => q.Name == name).Sum(q => q.Value);

    static List<GeometryPart> Parts(Fixture f, string id, string prefix) => f.Session.Geometry(id)!.Parts.Where(p => p.Key?.StartsWith(prefix) == true).ToList();

    static (Vec3 Min, Vec3 Max) Box(IEnumerable<GeometryPart> parts)
    {
        var v = parts.SelectMany(p => p.Solid.Vertices).ToList();
        return (new Vec3(v.Min(p => p.X), v.Min(p => p.Y), v.Min(p => p.Z)), new Vec3(v.Max(p => p.X), v.Max(p => p.Y), v.Max(p => p.Z)));
    }

    [Fact]
    public void A_gutter_takes_its_size_offset_drop_and_end_caps_and_goes_off_per_edge()
    {
        var f = new Fixture();
        var roof = AddRoof(f);                                           // gable: eaves along y = 0 and y = 6, 0.3 m overhang
        // Each eave its gutter, each a part; old roofs as they were: 125 half-round against the eave, with end caps.
        Assert.Equal(2, Parts(f, roof, "gutter:").Count);
        var south = Parts(f, roof, "gutter:").MinBy(p => Box([p]).Min.Y)!;
        var (min0, max0) = Box([south]);
        Assert.Equal(0.125, max0.Y - min0.Y, 3);
        Assert.Equal(4, Q(f, roof, "GutterEndCount"), 9);
        // 150 wide, 50 mm out, 30 mm down.
        Set(f, roof, new JsonObject { ["gutterWidth"] = 0.15, ["gutterOffset"] = 0.05, ["gutterDrop"] = 0.03 });
        var (min1, max1) = Box([Parts(f, roof, "gutter:").MinBy(p => Box([p]).Min.Y)!]);
        Assert.Equal(0.15, max1.Y - min1.Y, 3);
        Assert.Equal(-0.3, max0.Y, 3);
        Assert.Equal(max0.Y - 0.05, max1.Y, 3);
        Assert.Equal(max0.Z - 0.03, max1.Z, 3);
        // The south eave's gutter off: one gutter left, its length counted alone.
        var edges = new JsonArray(new JsonObject { ["gutter"] = false }, null, null, null);
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["edges"] = edges });
        Assert.Single(Parts(f, roof, "gutter:"));
        Assert.Equal(10.6, Q(f, roof, "GutterLength"), 6);
        Assert.Equal(2, Q(f, roof, "DownpipeCount"), 9);
    }

    [Fact]
    public void A_shed_roof_s_gutter_is_on_its_low_edge_only()
    {
        // Testers' report: the shed's high edge got a gutter too.
        var f = new Fixture();
        var roof = AddRoof(f, "modern_monopitch");
        foreach (var dir in new[] { "auto", "x", "y" })
        {
            f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["ridgeDirection"] = dir } });
            var r = Roof.Resolve(f.Model.Get(roof)!, f.Model)!;
            var shape = Roof.Solve(r).Shape!;
            var g = Assert.Single(RoofParts.Gutters(r, shape));
            Assert.True(Math.Abs(g.A.Z - g.B.Z) < 1e-9 && g.A.Z < shape.Height - 0.5, $"{dir}: the gutter at {g.A.Z:0.00}, the roof's top at {shape.Height:0.00}");
        }
    }

    [Fact]
    public void A_gutter_falls_to_its_outlet()
    {
        var f = new Fixture();
        var roof = AddRoof(f);
        var level = Box(Parts(f, roof, "gutter:")).Min.Z;
        Set(f, roof, new JsonObject { ["gutterFall"] = 5 });
        // Two outlets on each 10.6 m gutter, 0.3 m in from its ends: the high point halfway, 5 m from each, 5 mm a metre lower at them.
        var low = Box(Parts(f, roof, "gutter:")).Min.Z;
        Assert.Equal(level - 0.005 * 5.0, low, 3);
        var depth = Drainage.FallOf(10, [5], 0.005);
        Assert.Equal((0.0, 0.025, 0.0), (Math.Round(depth(0), 6), Math.Round(depth(5), 6), Math.Round(depth(10), 6)));
    }

    [Fact]
    public void The_automatic_downpipes_become_elements_tied_to_their_gutter_and_are_edited_there()
    {
        var f = new Fixture();
        var roof = AddRoof(f);
        var autoLength = Q(f, roof, "DownpipeLength");
        Assert.Equal(4, Parts(f, roof, "downpipe:").Count);
        var ids = ((JsonArray)f.Run("architecture.makeDownpipes", new JsonObject { ["id"] = roof })!["ids"]!).Select(x => (string)x!).ToList();
        Assert.Equal(4, ids.Count);
        Assert.Equal("elements", f.Model.Get(roof)!.Str("downpipes"));
        Assert.Empty(Parts(f, roof, "downpipe:"));
        // Where they were, as long: the roof's drainage count gone, the elements count themselves.
        Assert.Equal(0, Q(f, roof, "DownpipeCount"));
        Assert.Equal(autoLength, ids.Sum(id => Q(f, id, "Length")), 3);
        var pipe = f.Model.Get(ids[0])!;
        Assert.Equal(("RIGIDSEGMENT", "IfcPipeSegment"), (pipe.PredefinedType, pipe.Str("exportAs")));

        // Slid along its gutter and given a route: 0.4 m straight in, then down; a shoe at the foot.
        f.Run("architecture.updateDownpipe", new JsonObject
        {
            ["id"] = ids[0], ["station"] = 2, ["diameter"] = 0.08, ["shoe"] = true,
            ["route"] = new JsonArray(new JsonObject { ["length"] = 0.4, ["angle"] = 90 }, new JsonObject { ["angle"] = 0 }),
        });
        var placed = Drainage.Place(f.Model, f.Model.Get(ids[0])!)!;
        Assert.Equal(2, placed.Station, 9);
        Assert.Equal(4, placed.Chain.Count);
        Assert.Equal(0.4, (placed.Chain[1] - placed.Chain[0]).Length, 6);
        Assert.Equal(placed.Chain[0].Z, placed.Chain[1].Z, 6);
        Assert.Equal(placed.Chain[1].Xy.X, placed.Chain[2].Xy.X, 6);
        Assert.Equal(Drainage.GroundOf(f.Model), placed.Chain[^1].Z, 3);
        Assert.Throws<CommandException>(() => f.Run("architecture.updateDownpipe", new JsonObject { ["id"] = ids[0], ["route"] = new JsonArray(new JsonObject { ["angle"] = 120 }) }));
        // Another one on the north eave.
        var north = Drainage.EdgeOf(Roof.Resolve(f.Model.Get(roof)!, f.Model)!, Roof.Solve(Roof.Resolve(f.Model.Get(roof)!, f.Model)!).Shape!.Lines.Where(l => l.Kind == LineKind.Eave).MaxBy(l => l.A.Y)!);
        var more = (string)f.Run("architecture.addDownpipe", new JsonObject { ["roofId"] = roof, ["edge"] = north, ["station"] = 5 })!["id"]!;
        Assert.NotNull(f.Session.Geometry(more));
        // Undo takes the elements back to the automatic ones.
        f.Session.Undo();
        f.Session.Undo();
        f.Session.Undo();
        Assert.Null(f.Model.Get(roof)!.Str("downpipes"));
        Assert.Equal(4, Parts(f, roof, "downpipe:").Count);
    }

    [Fact]
    public void Gutters_and_downpipes_go_to_IFC_as_pipe_segments()
    {
        var f = new Fixture();
        var roof = AddRoof(f);
        f.Run("architecture.makeDownpipes", new JsonObject { ["id"] = roof });
        var path = Path.Combine(Path.GetTempPath(), $"drainage-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var segs = StepFile.Read(path).OfType("IFCPIPESEGMENT").ToList();
            Assert.Equal(2, segs.Count(s => s[8] is StepEnum { Value: "GUTTER" }));
            Assert.Equal(4, segs.Count(s => s[8] is StepEnum { Value: "RIGIDSEGMENT" } && (string?)s[4] == "Downpipe"));
        }
        finally { File.Delete(path); }
    }
}
