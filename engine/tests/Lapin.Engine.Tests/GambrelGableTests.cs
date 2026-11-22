using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// The gable walls under a mansard with gable ends (a gambrel, tester's report
/// #24): both ends rise to the same profile, the upper tier's apex included,
/// and are cut along it, whichever way they were drawn and whether they came
/// before or after the roof. Before: the upper tier, whose edge is the wall's
/// axis, was missed at one end, and a wall built up to the ridge was cut by
/// the roof's framing into slivers the roof's own cut refused, so it stood
/// uncut above the roof.
/// </summary>
public class GambrelGableTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Both_gable_walls_of_a_gambrel_rise_to_its_profile_and_are_cut_along_it(bool wallsAfterRoof)
    {
        var f = new Fixture(new ArchitectureModule());
        (double, double, double, double)[] rect = [(0, 0, 10, 0), (10, 0, 10, 6), (10, 6, 0, 6), (0, 6, 0, 0)];
        var walls = new List<string>();
        if (!wallsAfterRoof) foreach (var (x1, y1, x2, y2) in rect) walls.Add(f.Wall(x1, y1, x2, y2));
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6)) })!["id"]!;
        f.Run("architecture.updateRoof", new JsonObject
        {
            ["id"] = roof, ["attrs"] = new JsonObject { ["shape"] = "mansard", ["pitch"] = 70, ["upperPitch"] = 25 },
            ["edges"] = new JsonArray(new JsonObject(), new JsonObject { ["kind"] = "gable" }, new JsonObject(), new JsonObject { ["kind"] = "gable" }),
        });
        if (wallsAfterRoof) foreach (var (x1, y1, x2, y2) in rect) walls.Add(f.Wall(x1, y1, x2, y2));
        // The gable walls are the short sides, x = 10 and x = 0.
        double Top(string id) => f.Session.Geometry(id)!.Parts.Max(p => Measure.Bounds(p.Solid)!.Value.Max.Z);
        double TopAt(string id, double y, double tol) => f.Session.Geometry(id)!.Parts.SelectMany(p => p.Solid.Vertices).Where(v => Math.Abs(v.Y - y) < tol).Max(v => v.Z);
        var (east, west) = (walls[1], walls[3]);
        foreach (var id in new[] { east, west })
        {
            Assert.Null(f.Session.Geometry(id)!.Diagnostics);
            // The apex of the upper tier over the middle, the break lower, and near the eaves the steep tier lower still.
            Assert.Equal(Top(west), Top(id), 6);
            Assert.True(TopAt(id, 3, 0.05) > TopAt(id, 1, 0.05) + 0.5, "the upper tier's apex stands over the break");
            Assert.True(TopAt(id, 1, 0.05) > 5, "the steep tier rises to the break");
        }
        Assert.True(Top(east) > 6.5);
    }

    [Fact]
    public void A_gable_wall_s_gross_values_follow_what_is_built_up_to_the_roof_as_the_net_values_do()
    {
        // Audit 22.11.2026 §3.6: the gross values were the axis length × the eave height while the net counted the gable.
        var f = new Fixture(new ArchitectureModule());
        (double, double, double, double)[] rect = [(0, 0, 10, 0), (10, 0, 10, 6), (10, 6, 0, 6), (0, 6, 0, 0)];
        var walls = rect.Select(w => f.Wall(w.Item1, w.Item2, w.Item3, w.Item4)).ToList();
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6)) })!["id"]!;
        f.Run("architecture.updateRoof", new JsonObject
        {
            ["id"] = roof, ["edges"] = new JsonArray(new JsonObject(), new JsonObject { ["kind"] = "gable" }, new JsonObject(), new JsonObject { ["kind"] = "gable" }),
        });
        Dictionary<string, double> Q(string id) => f.Session.Quantities(id).Single(s => s.Name == "Qto_WallBaseQuantities").Quantities.ToDictionary(x => x.Name, x => x.Value);
        var gable = Q(walls[1]);
        Assert.True(gable["GrossSideArea"] > gable["Length"] * gable["Height"] + 1, $"the gable counts: {gable["GrossSideArea"]}");
        // Built as measured: the gross is the gable's too, the net as near it as an eave wall's is (only the corners' layers
        // wrapping round the joins, a few litres, lie past the axis's length).
        var eave = Q(walls[0]);
        Assert.Equal(gable["GrossVolume"], gable["NetVolume"], 0.01);
        Assert.Equal(eave["GrossVolume"], eave["NetVolume"], 0.01);
        Assert.Equal(gable["GrossSideArea"], gable["NetSideArea"], 0.05);
    }
}
