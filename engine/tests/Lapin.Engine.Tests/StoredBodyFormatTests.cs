using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>A stored body's triangles as base64 strings, and the arrays bodies were written with before still read.</summary>
public class StoredBodyFormatTests
{
    static readonly (string, string, IReadOnlyList<Vec3>, IReadOnlyList<(int, int, int)>) Tetra =
        ("Body", "#c9ced6", [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1.23456)], [(0, 2, 1), (0, 1, 3), (1, 2, 3), (0, 3, 2)]);

    [Fact]
    public void A_body_is_written_as_base64_and_read_back_to_the_tenth_of_a_millimetre()
    {
        var body = StoredBodies.Write([Tetra]);
        var part = (JsonObject)body["parts"]![0]!;
        Assert.NotNull((string?)part["v64"]);
        Assert.Null(part["v"]);
        var (v, t) = StoredBodies.ReadPart(part);
        Assert.Equal([0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1.2346], v);
        Assert.Equal([0, 2, 1, 0, 1, 3, 1, 2, 3, 0, 3, 2], t);
    }

    [Fact]
    public void A_body_written_before_as_arrays_is_read_the_same()
    {
        var legacy = new JsonObject
        {
            ["parts"] = new JsonArray(new JsonObject
            {
                ["name"] = "Body", ["color"] = "#c9ced6",
                ["v"] = new JsonArray(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1.2346),
                ["t"] = new JsonArray(0, 2, 1, 0, 1, 3, 1, 2, 3, 0, 3, 2),
            }),
        };
        var (v, t) = StoredBodies.ReadPart((JsonObject)legacy["parts"]![0]!);
        var (v2, t2) = StoredBodies.ReadPart((JsonObject)StoredBodies.Write([Tetra])["parts"]![0]!);
        Assert.Equal(v2, v);
        Assert.Equal(t2, t);
        // And built into the same solid.
        var e = new Lapin.Model.Entity("x", "IfcBuildingElementProxy", "x", new JsonObject { [StoredBodies.Key] = legacy });
        var f = new Lapin.Model.Entity("y", "IfcBuildingElementProxy", "y", new JsonObject { [StoredBodies.Key] = StoredBodies.Write([Tetra]) });
        var a = StoredBodies.Geometry(e, new Lapin.Model.ModelStore(), null!)!;
        var b = StoredBodies.Geometry(f, new Lapin.Model.ModelStore(), null!)!;
        Assert.Equal(a.Parts[0].Solid.Faces.Length, b.Parts[0].Solid.Faces.Length);
        Assert.Equal(4, a.Parts[0].Solid.Faces.Length);
    }
}
