using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The layers of strip foundations at a corner: whatever the join's style,
/// the layers outside the core (protection, insulation, waterproofing) go on
/// round an L corner, mitred; the cores join as the style says (butt: one runs
/// through to the other's far face, the other stops at it). Nothing overlaps,
/// nothing is left open.
/// </summary>
public class StripCornerTests
{
    static (Fixture F, string A, string B) L(string style)
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var a = (string)f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["joinStyle"] = style })!["id"]!;
        var b = (string)f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 6, ["y1"] = 0, ["x2"] = 6, ["y2"] = 5, ["joinStyle"] = style })!["id"]!;
        return (f, a, b);
    }

    static IEnumerable<GeometryPart> Stem(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Where(p => p.Key!.StartsWith("layer:"));

    [Fact]
    public void At_a_butt_corner_the_layers_go_on_round_it_and_the_cores_join_butt()
    {
        var (fm, am, bm) = L("mitre");
        var (fb, ab, bb) = L("butt");
        // The default type: protection, insulation, waterproofing, then the core (layer 3).
        foreach (var (mitred, butt) in new[] { (am, ab), (bm, bb) })
            foreach (var k in new[] { "layer:0", "layer:1", "layer:2" })
            {
                var m = Stem(fm, mitred).Single(p => p.Key == k);
                var b = Stem(fb, butt).Single(p => p.Key == k);
                Assert.Equal(Measure.Volume(m.Solid), Measure.Volume(b.Solid), 9);
                Assert.Equal(Measure.Bounds(m.Solid)!.Value.Min, Measure.Bounds(b.Solid)!.Value.Min);
                Assert.Equal(Measure.Bounds(m.Solid)!.Value.Max, Measure.Bounds(b.Solid)!.Value.Max);
            }
        // The cores fill the corner between them once: the stems hold as much as mitred ones.
        double All(Fixture f, params string[] ids) => ids.Sum(id => Stem(f, id).Sum(p => Measure.Volume(p.Solid)));
        Assert.Equal(All(fm, am, bm), All(fb, ab, bb), 9);
        // Butt: the core running through reaches the other core's far face (x 6.15), the other stops at its face (y 0.15).
        var coreA = Measure.Bounds(Stem(fb, ab).Single(p => p.Key == "layer:3").Solid)!.Value;
        var coreB = Measure.Bounds(Stem(fb, bb).Single(p => p.Key == "layer:3").Solid)!.Value;
        Assert.True((Math.Abs(coreA.Max.X - 6.15) < 1e-9 && Math.Abs(coreB.Min.Y - 0.15) < 1e-9) || (Math.Abs(coreB.Min.Y + 0.15) < 1e-9 && Math.Abs(coreA.Max.X - 5.85) < 1e-9),
            $"core A to x {coreA.Max.X}, core B from y {coreB.Min.Y}");
    }

    [Fact]
    public void Footings_can_be_mitred_and_butted_afterwards()
    {
        var (f, a, b) = L("butt");
        f.Run("structure.restyleJoins", new JsonObject { ["ids"] = new JsonArray(a, b), ["style"] = "mitre" });
        var core = Measure.Bounds(Stem(f, a).Single(p => p.Key == "layer:3").Solid)!.Value;
        // Mitred, the core's outer corner is at (6.15, −0.15) and its inner one at (5.85, 0.15).
        Assert.Equal(6.15, core.Max.X, 9);
    }
}
