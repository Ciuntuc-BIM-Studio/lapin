using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The type configurators' preview (decision 529): <c>types.preview</c> stands the type being edited up in a sample in a
/// scratch project and draws it as the drawings draw, a plan and a section through the view pipeline and the sample's
/// mesh, every part by its key; the open project is only read.
/// </summary>
public sealed class TypePreviewTests(ITestOutputHelper output)
{
    /// <summary>Three layers: mineral wool on the R face, the brick core, plaster on the L face.</summary>
    static JsonObject ThreeLayers() => new()
    {
        ["name"] = "Preview test",
        ["layers"] = new JsonArray(
            new JsonObject { ["material"] = "Mineral wool", ["thickness"] = 0.1 },
            new JsonObject { ["material"] = "Brick", ["thickness"] = 0.25, ["core"] = true },
            new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.03 }),
    };

    static JsonObject Ask(JsonObject type, double scale = 50, string detail = "fine") => new()
    {
        ["class"] = IfcClass.WallType,
        ["type"] = type,
        ["sample"] = new JsonObject(),
        ["views"] = new JsonArray("plan", "section", "3d"),
        ["scale"] = scale,
        ["detail"] = detail,
    };

    static JsonObject Preview(Fixture f, JsonObject ask) => (JsonObject)f.Session.Query("types.preview", ask)!;

    /// <summary>A project whose materials of the three layers are hatched (wool, brick in courses, plaster), as a project's own are.</summary>
    static Fixture Hatched()
    {
        var f = new Fixture();
        f.Type("Existing", ("Mineral wool", 0.1, false), ("Brick", 0.25, true), ("Plaster", 0.03, false));
        f.LegacyHatches();
        return f;
    }

    static List<Vec2[]> Loops(JsonNode? n) => (n as JsonArray ?? []).Select(l => ((JsonArray)l!).Select(v => (double)v!).Chunk(2).Select(c => new Vec2(c[0], c[1])).ToArray()).ToList();

    static double Area(Vec2[] loop)
    {
        var a = 0.0;
        for (var i = 0; i < loop.Length; i++) { var (p, q) = (loop[i], loop[(i + 1) % loop.Length]); a += p.X * q.Y - q.X * p.Y; }
        return Math.Abs(a) / 2;
    }

    [Fact]
    public void A_three_layer_type_is_drawn_in_plan_and_section_layer_by_layer_with_a_closed_mesh_for_each()
    {
        var f = Hatched();
        var o = Preview(f, Ask(ThreeLayers()));
        Assert.Null(o["problem"]);
        output.WriteLine($"{o["ms"]} ms");
        var keys = ((JsonArray)o["partKeys"]!).Select(k => (string)k!).ToList();
        Assert.Equal(["layer:0", "layer:1", "layer:2"], keys);

        // The plan at 1:50: each layer's cut areas, hatched as the drawings hatch them, the sample's R face (layer 1) on top.
        var plan = (JsonObject)o["plan"]!;
        Assert.Equal(50, (double)plan["scale"]!);
        var hatches = ((JsonArray)plan["pens"]!).Where(p => (string?)p!["layer"] == "HATCH").SelectMany(p => (p!["segments"] as JsonArray ?? []).Select(v => (double)v!)).Chunk(4)
            .Select(s => new Vec2((s[0] + s[2]) / 2, (s[1] + s[3]) / 2)).ToList();
        Assert.NotEmpty(hatches);
        // The long wall's pieces only (the T reaches 1.2 m down).
        double MeanY(string key) => Loops(plan["parts"]![key]).Where(l => l.All(p => p.Y > -0.5)).SelectMany(l => l).Average(p => p.Y);
        Assert.True(MeanY("layer:0") > MeanY("layer:1") && MeanY("layer:1") > MeanY("layer:2"));
        foreach (var key in keys)
        {
            var loops = Loops(plan["parts"]![key]);
            Assert.True(loops.Sum(Area) > 0.01, key);
            // Hatched as its material says: the wool's battens and the brick's courses lie in their layers, the plaster (no hatch) has none.
            if (key == "layer:2") Assert.DoesNotContain(hatches, h => Polygon2.Inside(loops, h));
            else Assert.Contains(hatches, h => Polygon2.Inside(loops, h));
        }

        // The section cuts through the three layers, as thick as they are, side by side (R on the right).
        var section = (JsonObject)o["section"]!;
        Assert.Equal("section", (string?)section["kind"]);
        var widths = keys.Select(k => Loops(section["parts"]![k]).SelectMany(l => l).ToList()).Select(ps => (Min: ps.Min(p => p.X), Max: ps.Max(p => p.X))).ToList();
        Assert.Equal(0.1, widths[0].Max - widths[0].Min, 3);
        Assert.Equal(0.25, widths[1].Max - widths[1].Min, 3);
        Assert.Equal(0.03, widths[2].Max - widths[2].Min, 3);
        Assert.True(widths[0].Min > widths[1].Min && widths[1].Min > widths[2].Min);

        // The mesh: the main wall's every layer closed bodies (each edge shared by two triangles); the plaster on the L face is
        // two, the T's core going through it.
        var mesh = ((JsonArray)o["mesh"]!).Select(p => (JsonObject)p!).ToList();
        foreach (var key in keys)
        {
            var parts = mesh.Where(p => (string?)p["element"] == "main" && (string?)p["key"] == key).ToList();
            Assert.Equal(key == "layer:2" ? 2 : 1, parts.Count);
            Assert.All(parts, p => Assert.True(Closed(p), key));
        }
        Assert.Contains(mesh, p => (string?)p["element"] == "other");
    }

    /// <summary>Every edge of the mesh (its vertices welded by position) shared by exactly two triangles.</summary>
    static bool Closed(JsonObject part)
    {
        var pos = ((JsonArray)part["positions"]!).Select(v => (double)v!).ToArray();
        var ix = ((JsonArray)part["indices"]!).Select(v => (int)v!).ToArray();
        var weld = new Dictionary<(long, long, long), int>();
        int Id(int i)
        {
            var k = ((long)Math.Round(pos[3 * i] * 1e4), (long)Math.Round(pos[3 * i + 1] * 1e4), (long)Math.Round(pos[3 * i + 2] * 1e4));
            if (!weld.TryGetValue(k, out var id)) weld[k] = id = weld.Count;
            return id;
        }
        var edges = new Dictionary<(int, int), int>();
        for (var t = 0; t + 2 < ix.Length; t += 3)
        {
            var v = new[] { Id(ix[t]), Id(ix[t + 1]), Id(ix[t + 2]) };
            for (var e = 0; e < 3; e++)
            {
                var (a, b) = (v[e], v[(e + 1) % 3]);
                var key = a < b ? (a, b) : (b, a);
                edges[key] = edges.GetValueOrDefault(key) + 1;
            }
        }
        return edges.Count > 0 && edges.Values.All(n => n == 2);
    }

    [Fact]
    public void The_preview_leaves_the_project_as_it_was()
    {
        var f = new Fixture();
        var entities = f.Model.Entities.Count();
        var relations = f.Model.Relations.Count();
        var revision = f.Model.Revision;
        var canUndo = f.Session.History.CanUndo;
        var o = Preview(f, Ask(ThreeLayers()));
        Assert.Null(o["problem"]);
        // Mineral wool is new to the project: made in the preview's own, never here.
        Assert.DoesNotContain(f.Model.OfClass(IfcClass.Material), m => m.Name == "Mineral wool");
        Assert.DoesNotContain(f.Model.OfClass(IfcClass.WallType), t => t.Name == "Preview test");
        Assert.Equal(entities, f.Model.Entities.Count());
        Assert.Equal(relations, f.Model.Relations.Count());
        Assert.Equal(revision, f.Model.Revision);
        Assert.Equal(canUndo, f.Session.History.CanUndo);
    }

    [Fact]
    public void The_same_question_is_answered_from_the_cache_until_the_project_changes()
    {
        var f = new Fixture();
        var first = Preview(f, Ask(ThreeLayers()));
        Assert.Null(first["cached"]);
        var watch = Stopwatch.StartNew();
        var again = Preview(f, Ask(ThreeLayers()));
        output.WriteLine($"first {first["ms"]} ms, again {watch.Elapsed.TotalMilliseconds:0.0} ms");
        Assert.True((bool?)again["cached"]);
        Assert.Equal(((JsonArray)first["partKeys"]!).ToJsonString(), ((JsonArray)again["partKeys"]!).ToJsonString());
        // Another scale is another question.
        Assert.Null(Preview(f, Ask(ThreeLayers(), scale: 20))["cached"]);
        // An edit of the project: asked afresh.
        f.Wall(0, 0, 4, 0);
        Assert.Null(Preview(f, Ask(ThreeLayers()))["cached"]);
    }

    [Fact]
    public void A_type_that_cannot_be_made_says_why()
    {
        var f = new Fixture();
        var o = Preview(f, Ask(new JsonObject { ["name"] = "Bad", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0 }) }));
        Assert.False(string.IsNullOrEmpty((string?)o["problem"]));
    }

    [Fact]
    public void A_coarse_preview_draws_the_layers_as_one_area()
    {
        var f = Hatched();
        var fine = Preview(f, Ask(ThreeLayers(), detail: "fine"));
        var coarse = Preview(f, Ask(ThreeLayers(), detail: "coarse"));
        // The cut outlines: fine draws the lines between the layers, coarse the outline alone.
        static int CutSegments(JsonObject o) => ((JsonArray)o["plan"]!["pens"]!).Where(p => (string?)p!["layer"] == "CUT").Sum(p => (p!["segments"] as JsonArray)?.Count ?? 0) / 4;
        output.WriteLine($"fine {CutSegments(fine)} cut segments, coarse {CutSegments(coarse)}; {coarse["ms"]} ms");
        Assert.True(CutSegments(coarse) < CutSegments(fine));
    }
}
