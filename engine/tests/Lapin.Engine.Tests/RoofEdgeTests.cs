using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// What a roof keeps by edge index (its per-edge rules, a shed's low edge, its downpipes' edge and station) stays on the
/// same edges when the edges are renumbered: the roof mirrored (its outline reversed), copied mirrored, or its outline redrawn.
/// </summary>
public class RoofEdgeTests
{
    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);
    static JsonArray House => Pts((0, 0), (10, 0), (10, 6), (0, 6));

    static string AddRoof(Fixture f, string style = "fr_provence") =>
        (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = House, ["style"] = style })!["id"]!;

    static Roof.Resolved Resolved(Fixture f, string id) => Roof.Resolve(f.Model.Get(id)!, f.Model)!;

    /// <summary>Each edge's rule by where its middle is, mapped back through <paramref name="back"/>.</summary>
    static List<(Vec2 Mid, EdgeRule Rule)> Rules(Fixture f, string id, Func<Vec2, Vec2>? back = null)
    {
        var r = Resolved(f, id);
        var n = r.Outline.Count;
        return [.. Enumerable.Range(0, n).Select(i => ((back ?? (p => p))((r.Outline[i] + r.Outline[(i + 1) % n]) * 0.5), r.Rules[i]))];
    }

    static void SameRules(List<(Vec2 Mid, EdgeRule Rule)> before, List<(Vec2 Mid, EdgeRule Rule)> after)
    {
        Assert.Equal(before.Count, after.Count);
        foreach (var (mid, rule) in after)
        {
            var was = before.Single(x => (x.Mid - mid).Length < 1e-6);
            Assert.Equal(was.Rule, rule);
        }
    }

    static JsonObject Mirror(string id, bool copy = false) => new()
    {
        ["ids"] = new JsonArray(id), ["op"] = "mirror", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(0, 1), ["copy"] = copy,
    };

    static Vec2 Back(Vec2 p) => new(-p.X, p.Y);

    [Fact]
    public void A_mirrored_roof_keeps_its_edge_rules_on_their_edges()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "traditional_campie");                    // hip: every edge an eave
        f.Run("architecture.updateRoof", new JsonObject
        {
            ["id"] = roof,
            ["edges"] = new JsonArray(new JsonObject { ["overhang"] = 0.8 }, new JsonObject { ["kind"] = "gable" }, null, new JsonObject { ["pitch"] = 45 }),
        });
        var before = Rules(f, roof);
        f.Run("element.transform", Mirror(roof));
        SameRules(before, Rules(f, roof, Back));
    }

    [Fact]
    public void A_mirrored_shed_keeps_its_low_edge()
    {
        var f = new Fixture();
        var roof = AddRoof(f);
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["shape"] = "shed", ["lowEdge"] = 0 } });
        var before = Rules(f, roof);
        Assert.Equal(EdgeKind.Eave, before[0].Rule.Kind);
        f.Run("element.transform", Mirror(roof));
        SameRules(before, Rules(f, roof, Back));
    }

    [Fact]
    public void A_mirrored_roof_and_its_mirrored_copy_keep_their_downpipes_where_they_were()
    {
        var f = new Fixture();
        var roof = AddRoof(f);
        var ids = ((JsonArray)f.Run("architecture.makeDownpipes", new JsonObject { ["id"] = roof })!["ids"]!).Select(x => (string)x!).ToList();
        f.Run("architecture.updateDownpipe", new JsonObject { ["id"] = ids[0], ["station"] = 2 });
        var outlets = ids.Select(id => Drainage.Place(f.Model, f.Model.Get(id)!)!.Outlet.Xy).ToList();

        // A copy, mirrored: the same downpipes, on the copy.
        var copy = (string)f.Run("element.transform", Mirror(roof, copy: true))!["ids"]![0]!;
        var copied = f.Model.OfClass(Drainage.DownpipeClass).Where(p => p.Str("roofId") == copy).ToList();
        Assert.Equal(ids.Count, copied.Count);
        var mirrored = copied.Select(p => Back(Drainage.Place(f.Model, p)!.Outlet.Xy)).ToList();
        Assert.All(outlets, o => Assert.Contains(mirrored, q => (q - o).Length < 1e-6));

        // The roof itself mirrored: each downpipe where its outlet was, mirrored.
        f.Run("element.transform", Mirror(roof));
        for (var i = 0; i < ids.Count; i++)
        {
            var p = Drainage.Place(f.Model, f.Model.Get(ids[i])!);
            Assert.NotNull(p);
            Assert.Equal(0, (Back(p!.Outlet.Xy) - outlets[i]).Length, 6);
        }
    }

    [Fact]
    public void A_roof_redrawn_keeps_its_edge_rules_and_downpipes_on_the_edges_they_were_on()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "traditional_campie");
        f.Run("architecture.updateRoof", new JsonObject
        {
            ["id"] = roof,
            ["edges"] = new JsonArray(new JsonObject { ["overhang"] = 0.8 }, new JsonObject { ["kind"] = "gable" }, null, null),
        });
        var ids = ((JsonArray)f.Run("architecture.makeDownpipes", new JsonObject { ["id"] = roof })!["ids"]!).Select(x => (string)x!).ToList();
        var outlets = ids.Select(id => Drainage.Place(f.Model, f.Model.Get(id)!)!.Outlet.Xy).ToList();
        var old = Resolved(f, roof);
        // The same outline drawn from another corner, the south edge split in two.
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["points"] = Pts((10, 6), (0, 6), (0, 0), (4, 0), (10, 0)) });
        var after = Rules(f, roof);
        Assert.Equal(5, after.Count);
        // Each new edge lies on an old one: it takes its rule.
        foreach (var (mid, rule) in after)
        {
            var i = Enumerable.Range(0, 4).Single(k => Math.Abs(Vec2.Cross((old.Outline[(k + 1) % 4] - old.Outline[k]).Normalized(), mid - old.Outline[k])) < 1e-6);
            Assert.Equal(old.Rules[i], rule);
        }
        for (var i = 0; i < ids.Count; i++)
            Assert.Equal(0, (Drainage.Place(f.Model, f.Model.Get(ids[i])!)!.Outlet.Xy - outlets[i]).Length, 6);
    }

    [Fact]
    public void A_moved_roof_takes_its_roof_windows_with_it()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "modern_barn");
        f.Run("architecture.addRoofOpening", new JsonObject { ["x"] = 3, ["y"] = 1.5 });
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(roof), ["op"] = "move", ["by"] = new JsonArray(20, 0) });
        var o = (JsonObject)((JsonArray)f.Model.Get(roof)!.Attrs["openings"]!)[0]!;
        Assert.Equal((23.0, 1.5), (Json.Number(o["x"])!.Value, Json.Number(o["y"])!.Value));
    }
}
