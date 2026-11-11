using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Cut priorities take out of each part only the cutters that reach that part: an element whose parts spread far (a roof's
/// downpipes down to the ground) is not cut, covering and all, by everything under its whole box.
/// </summary>
public class CutPartsTests
{
    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);

    static bool Overlap((Vec3 Min, Vec3 Max) a, (Vec3 Min, Vec3 Max) b) =>
        a.Min.X < b.Max.X && b.Min.X < a.Max.X && a.Min.Y < b.Max.Y && b.Min.Y < a.Max.Y && a.Min.Z < b.Max.Z && b.Min.Z < a.Max.Z;

    [Theory]
    [InlineData("fr_provence")]
    [InlineData("traditional_campie")]
    public void A_floor_under_a_roof_cuts_only_the_parts_it_meets(string style)
    {
        var f = new Fixture();
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (10, 0), (10, 6), (0, 6)), ["style"] = style })!["id"]!;
        var alone = f.Session.Geometry(roof)!.Parts;
        Assert.All(alone, p => Assert.True(Topology.IsManifold(p.Solid), $"{p.Key} open before any floor"));
        var before = alone.ToDictionary(p => p.Key ?? p.Name, p => Measure.Volume(p.Solid));
        Assert.Contains(before.Keys, k => k.StartsWith("downpipe"));
        var box = f.Bounds(roof);

        // A floor a metre up, wider than the house: its top inside the roof's box, through the downpipes, nowhere near the covering.
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((-2, -2), (12, -2), (12, 8), (-2, 8)), ["offset"] = 1.0 })!["id"]!;
        var floor = f.Bounds(slab);
        Assert.True(floor.Max.Z > box.Min.Z && floor.Max.Z < box.Max.Z);

        var g = f.Session.Geometry(roof)!;
        Assert.Empty(g.Diagnostics ?? []);
        foreach (var p in g.Parts) Assert.True(Topology.IsManifold(p.Solid), $"{p.Key}: {string.Join("; ", Topology.Validate(p.Solid).Select(d => d.Message))}");
        var cut = 0;
        foreach (var p in g.Parts)
        {
            var key = p.Key ?? p.Name;
            var was = before[key];
            var now = Measure.Volume(p.Solid);
            if (Overlap(Measure.Bounds(p.Solid)!.Value, floor) && key.StartsWith("downpipe")) { Assert.True(now < was - 1e-9, $"{key} not cut"); cut++; }
            else if (!Overlap(Measure.Bounds(p.Solid)!.Value, floor)) Assert.Equal(was, now, 9);
        }
        Assert.True(cut > 0);
    }
}
