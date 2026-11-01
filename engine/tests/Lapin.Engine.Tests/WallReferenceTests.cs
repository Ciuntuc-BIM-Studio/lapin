using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Walls drawn on a face (testers' request, decision 237): the line clicked is the core's centre (as before), its exterior or
/// interior face, or the exterior or interior finish face; the axis stays on the core, set off from the line; a corner drawn on
/// the exterior faces meets there.
/// </summary>
public class WallReferenceTests
{
    static Vec2 P(JsonNode? n) => new((double)n![0]!, (double)n[1]!);

    static (Entity Wall, IReadOnlyList<Walls.Band> Bands) Draw(Fixture f, string reference, double x1, double y1, double x2, double y2)
    {
        var id = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["reference"] = reference })!["id"]!;
        var w = f.Model.Get(id)!;
        return (w, Walls.Section(f.Model, w));
    }

    [Theory]
    [InlineData("core")]
    [InlineData("coreExterior")]
    [InlineData("coreInterior")]
    [InlineData("exterior")]
    [InlineData("interior")]
    public void The_line_drawn_is_the_face_asked_for_and_the_axis_stays_on_the_core(string reference)
    {
        var f = new Fixture();
        var (w, bands) = Draw(f, reference, 0, 0, 10, 0);
        // Drawn along +x, so left of the axis is +y and the exterior (the first layer) is the right, −y.
        var axisY = P(w.Attrs["start"]).Y;
        var core = bands.First(b => b.Core);
        var face = reference switch
        {
            "core" => (core.From + core.To) / 2, "coreExterior" => core.From, "coreInterior" => core.To,
            "exterior" => bands.Min(b => b.From), _ => bands.Max(b => b.To),
        };
        Assert.Equal(0, axisY + face, 9);
        Assert.Equal(axisY, P(w.Attrs["end"]).Y, 9);
    }

    [Fact]
    public void A_corner_drawn_on_the_exterior_faces_meets_there()
    {
        var f = new Fixture();
        // Round a house anticlockwise: the south wall along +x with the building on its left (+y), then up the east side.
        var (south, bands) = Draw(f, "exterior", 0, 0, 10, 0);
        var (east, _) = Draw(f, "exterior", 10, 0, 10, 8);
        south = f.Model.Get(south.Id)!;
        var lo = bands.Min(b => b.From);
        // Both axes end where the exterior faces' corner (10, 0) is set in by the build-up to the core's middle.
        Assert.Equal(P(south.Attrs["end"]).X, P(east.Attrs["start"]).X, 9);
        Assert.Equal(P(south.Attrs["end"]).Y, P(east.Attrs["start"]).Y, 9);
        Assert.Equal(-lo, P(south.Attrs["start"]).Y, 9);
        Assert.Equal(10 + lo, P(east.Attrs["start"]).X, 9);
        Assert.Throws<CommandException>(() => f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 1, ["y2"] = 0, ["reference"] = "middle" }));
    }
}
