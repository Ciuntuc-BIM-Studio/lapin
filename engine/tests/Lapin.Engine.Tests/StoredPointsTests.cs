using System.Text.Json.Nodes;
using Lapin.Architecture.Balconies;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// One reader of stored points (audit 15.11.2026, D8): the modules' copies read a coordinate that is not a number as 0,
/// so [5, "x"] stood at (5, 0); now a point that is not two finite numbers is no point (the origin where one is needed,
/// left out of a list), and a valid one reads as before.
/// </summary>
public class StoredPointsTests
{
    static Entity With(string key, JsonNode value) => new("e1", IfcClass.Slab, "E", new JsonObject { [key] = value });

    [Fact]
    public void A_valid_point_reads_as_before()
    {
        Assert.Equal(new Vec2(1.5, -2), SectionHeads.Offset(With("headA", new JsonArray(1.5, -2)), "a"));
        Assert.Equal([new Vec2(0, 0), new Vec2(4, 0), new Vec2(4, 3)],
            BalconyBuilder.Points(With("contour", new JsonArray(new JsonArray(0, 0), new JsonArray(4, 0), new JsonArray(4, 3)))));
    }

    [Fact]
    public void A_coordinate_that_is_no_number_is_never_read_as_zero()
    {
        Assert.Equal(default, SectionHeads.Offset(With("headA", new JsonArray(5, "x")), "a"));
        Assert.Equal([new Vec2(0, 0), new Vec2(4, 3)],
            BalconyBuilder.Points(With("contour", new JsonArray(new JsonArray(0, 0), new JsonArray(4, "x"), new JsonArray(4, 3)))));
        var rings = Section.RingsFrom(new JsonArray(new JsonArray(new JsonArray(0, 0), new JsonArray(1, 0), new JsonArray(1, double.NaN.ToString()), new JsonArray(1, 1), new JsonArray(0, 1))));
        Assert.Equal(4, Assert.Single(rings!).Count);
    }
}
