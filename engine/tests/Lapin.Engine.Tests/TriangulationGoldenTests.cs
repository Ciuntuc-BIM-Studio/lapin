using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// One triangulation engine (the site and floor follow-ups of 23.11.2026, item 4): the TIN and its constrained Delaunay moved
/// from the civil module to <c>Lapin.Geometry</c>, where the floors' <see cref="PlanMesh"/> shares its predicates. Nothing may
/// change: a terrain with a feature line, a terrain on an outline with an arc, a hardscape on corners out of one plane, a grading
/// patch between two plazas and a floor with drains, a line, points, edge slopes and a region are built and their bodies hashed
/// (every vertex to the nanometre, every face), against the hashes taken before the move.
/// </summary>
public sealed class TriangulationGoldenTests
{
    static Fixture F() => new(new ArchitectureModule(), new CivilModule(), new StructureModule());
    static JsonArray P(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);
    static JsonArray Pts(params double[][] pts) => new([.. pts.Select(p => (JsonNode)P(p))]);
    static JsonArray Rect(double x0, double y0, double x1, double y1) => Pts([x0, y0], [x1, y0], [x1, y1], [x0, y1]);

    /// <summary>A body as text: each part's key, its vertices to the nanometre and its faces' loops, hashed.</summary>
    static string Hash(Fixture f, string id)
    {
        var g = f.Session.Geometry(id)!;
        var b = new StringBuilder();
        string N(double v) => Math.Round(v, 9).ToString("0.#########", CultureInfo.InvariantCulture);
        foreach (var p in g.Parts)
        {
            b.Append(p.Key).Append('|');
            foreach (var v in p.Solid.Vertices) b.Append(N(v.X)).Append(',').Append(N(v.Y)).Append(',').Append(N(v.Z)).Append(';');
            foreach (var face in p.Solid.Faces)
            {
                b.Append('[').Append(string.Join(',', face.Outer));
                foreach (var h in face.Holes) b.Append('/').Append(string.Join(',', h));
                b.Append(']');
            }
            b.Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(b.ToString())))[..16];
    }

    static string Field(Fixture f)
    {
        // An irregular survey: a grid shaken by a fixed rule, heights a gentle saddle.
        var pts = new JsonArray();
        for (var i = 0; i <= 14; i++)
            for (var j = 0; j <= 14; j++)
            {
                var (x, y) = (i * 3.0 + 0.37 * Math.Sin(i * 7.1 + j), j * 3.0 + 0.41 * Math.Cos(j * 5.3 + i));
                pts.Add(P(x, y, 1.0 + 0.02 * x - 0.015 * y + 0.0004 * (x - 20) * (y - 20)));
            }
        return (string)f.Run("civil.addTerrain", new JsonObject { ["storeyId"] = f.Storey, ["points"] = pts, ["datum"] = 0, ["interval"] = 0.5, ["depth"] = 2 })!["id"]!;
    }

    [Fact]
    public void Terrains_hardscapes_gradings_and_floors_are_built_exactly_as_before_the_move()
    {
        var f = F();
        var t = Field(f);
        f.Run("civil.addFeatureLine", new JsonObject { ["terrainId"] = t, ["points"] = Pts([6, 6, 1.4], [20, 9, 1.1], [33, 25, 1.6]) });
        var outlined = (string)f.Run("civil.addTerrainOnOutline", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = Pts([60, 0, 2.0], [80, 0, 2.5], [80, 15, 3.0], [60, 15, 2.2]), ["arcs"] = P(0, 3, 0, 0), ["spacing"] = 2.5,
        })!["id"]!;
        var cornered = (string)f.Run("civil.addHardscape", new JsonObject { ["points"] = Pts([4, 30], [14, 31], [15, 38], [5, 39]), ["type"] = "pavers", ["cornerZ"] = P(1.2, 1.35, 1.1, 1.5) })!["id"]!;
        var lower = (string)f.Run("civil.addHardscape", new JsonObject { ["points"] = Rect(18, 4, 26, 12), ["arcs"] = P(0, -2, 0, 0), ["elevation"] = 0.6, ["type"] = "pavers" })!["id"]!;
        var upper = (string)f.Run("civil.addHardscape", new JsonObject { ["points"] = Rect(31, 4, 39, 12), ["arcs"] = P(0, 0, 0, 2), ["elevation"] = 1.6, ["type"] = "pavers" })!["id"]!;
        var grading = (string)f.Run("civil.addGrading", new JsonObject { ["kind"] = "patch", ["edges"] = new JsonArray(new JsonObject { ["id"] = lower }, new JsonObject { ["id"] = upper }) })!["id"]!;
        var type = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Tiled floor",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Ceramic tiles", ["thickness"] = 0.01 },
                new JsonObject { ["material"] = "Screed", ["thickness"] = 0.07 },
                new JsonObject { ["material"] = "Concrete", ["thickness"] = 0.2, ["core"] = true }),
        })!["id"]!;
        var floor = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts([100, 0], [108, 0], [108, 6], [103, 6], [103, 9], [100, 9]), ["typeId"] = type })!["id"]!;
        void S(JsonObject a) { a["id"] = floor; f.Run("architecture.addFloorSlope", a); }
        S(new JsonObject { ["kind"] = "edge", ["edge"] = 0, ["grade"] = "1.5%", ["fall"] = "in" });
        S(new JsonObject { ["kind"] = "edge", ["edge"] = 1, ["grade"] = "1%", ["fall"] = "in" });
        S(new JsonObject { ["kind"] = "drain", ["at"] = P(105, 3), ["depth"] = 0.02, ["grate"] = 0.15 });
        S(new JsonObject { ["kind"] = "drain", ["at"] = P(101, 7), ["depth"] = 0.015 });
        S(new JsonObject { ["kind"] = "line", ["a"] = P(100.5, 5), ["b"] = P(102.5, 5.5), ["offset"] = new JsonArray(-0.005, -0.01) });
        S(new JsonObject { ["kind"] = "point", ["at"] = P(107, 5), ["offset"] = -0.012 });
        f.Run("architecture.addFloorSlopeRegion", new JsonObject { ["id"] = floor, ["points"] = Rect(106, 0.5, 107.5, 2), ["elevation"] = -0.02 });
        var ids = new[] { t, outlined, cornered, lower, upper, grading, floor };
        Assert.All(ids, id => Assert.NotEmpty(f.Session.Geometry(id)!.Parts));
        Assert.Equal(string.Join(" ", Golden), string.Join(" ", ids.Select(id => Hash(f, id))));
    }

    static readonly string[] Golden = ["42155ADD69F38D69", "88BC0FB3700E4363", "45EC1C6398C70CD9", "1DE14F7C52A63E04", "F6EA2D624EB253DD", "5437249887C1927B", "7AE8FB9AD106BA1D"];
}
