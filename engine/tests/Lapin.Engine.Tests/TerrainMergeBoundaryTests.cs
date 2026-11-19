using System.Text;
using System.Text.Json.Nodes;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Found filming the terrain tutorial (19.11.2026): a terrain with a drawn boundary split and merged again came back with no
/// boundary (its convex hull); and its quantities' cut and fill, which the engine works out, were hidden in the panel.
/// </summary>
public class TerrainMergeBoundaryTests
{
    static Fixture F() => new(new Lapin.Architecture.ArchitectureModule(), new CivilModule());

    /// <summary>A 40 × 40 m slope: z = 100 + 0.05 x (surveyed), the datum at 100.</summary>
    static string Slope(Fixture f, int x0 = 0)
    {
        var lines = new List<string>();
        for (var i = 0; i <= 4; i++) for (var j = 0; j <= 4; j++) lines.Add($"{x0 + i * 10} {j * 10} {100 + 0.05 * i * 10}");
        return (string)f.Run("civil.addTerrain", new JsonObject { ["text"] = string.Join("\n", lines), ["datum"] = 100, ["interval"] = 0.5, ["majorEvery"] = 2, ["depth"] = 1 })!["id"]!;
    }

    static JsonArray Json(params (double X, double Y)[] pts) => new([.. pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]);

    static double PlanArea(Fixture f, string t) =>
        f.Session.Quantities(t).Single(q => q.Name == "Lapin_TerrainQuantities").Quantities.Single(x => x.Name == "PlanArea").Value;

    /// <summary>Whether two outlines have the same corners in the same cyclic order, whichever corner they start from.</summary>
    static bool SameLoop(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b)
    {
        if (a.Count != b.Count) return false;
        for (var s = 0; s < b.Count; s++)
            if (Enumerable.Range(0, a.Count).All(i => (a[i] - b[(i + s) % b.Count]).Length < 1e-6)) return true;
        return false;
    }

    // The film's outline: seven corners, not convex.
    static readonly (double, double)[] Seven = [(2, 2), (30, 4), (38, 15), (33, 36), (18, 30), (6, 38), (4, 20)];
    static List<Vec2> SevenCcw() { var l = Seven.Select(p => new Vec2(p.Item1, p.Item2)).ToList(); return Polygon2.SignedArea(l) > 0 ? l : [.. Enumerable.Reverse(l)]; }

    [Fact]
    public void A_split_terrain_merged_again_has_its_drawn_boundary_back_and_each_half_had_its_side()
    {
        var f = F();
        var t = Slope(f);
        f.Run("civil.setBoundary", new JsonObject { ["id"] = t, ["points"] = Json(Seven) });
        var whole = Math.Abs(Polygon2.SignedArea(SevenCcw()));
        Assert.Equal(whole, PlanArea(f, t), 6);
        Assert.Null(Terrains.Of(f.Model.Get(t)!).ElevationAt(18, 36));   // in the notch at the top

        // Split along x = 20: each half carries the boundary cut to its side, and together they cover the whole.
        var ids = ((JsonObject)f.Run("civil.splitTerrain", new JsonObject { ["id"] = t, ["x1"] = 20, ["y1"] = -5, ["x2"] = 20, ["y2"] = 45 })!)["ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal(t, ids[0]);
        var halves = ids.Select(id => ArcOutline.CornersOf(f.Model.Get(id)!, Terrains.BoundaryKey)).ToList();
        Assert.All(halves, h => Assert.True(h.Count >= 3));
        var leftOf = halves.Single(h => h.All(p => p.X <= 20 + 1e-9));
        var rightOf = halves.Single(h => h.All(p => p.X >= 20 - 1e-9));
        Assert.Equal(whole, Math.Abs(Polygon2.SignedArea(leftOf)) + Math.Abs(Polygon2.SignedArea(rightOf)), 6);
        Assert.Equal(whole, PlanArea(f, ids[0]) + PlanArea(f, ids[1]), 6);

        // Merged (the smaller half first: it keeps its GlobalId): the seven corners again, not the hull.
        var merged = (JsonObject)f.Run("civil.mergeTerrains", new JsonObject { ["ids"] = new JsonArray(ids[1], ids[0]) })!;
        Assert.Equal(ids[1], (string)merged["id"]!);
        Assert.Null(merged["problems"]);
        Assert.Null(f.Model.Get(ids[0]));
        var m = f.Model.Get(ids[1])!;
        Assert.True(SameLoop(SevenCcw(), ArcOutline.CornersOf(m, Terrains.BoundaryKey)), string.Join(" ", ArcOutline.CornersOf(m, Terrains.BoundaryKey)));
        Assert.Null(m.Attrs[Terrains.BoundaryArcs]);
        Assert.Equal(whole, PlanArea(f, ids[1]), 6);
        Assert.Null(Terrains.Of(m).ElevationAt(18, 36));
        Assert.NotNull(Terrains.Of(m).ElevationAt(20, 15));

        // One undo step: the two halves again, each with its own boundary.
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(ids[0]));
        Assert.True(SameLoop(halves[1], ArcOutline.CornersOf(f.Model.Get(ids[1])!, Terrains.BoundaryKey)));
    }

    [Fact]
    public void Merging_terrains_whose_boundaries_overlap_takes_the_outline_round_both()
    {
        var f = F();
        var a = Slope(f);
        var b = Slope(f, 20);
        f.Run("civil.setBoundary", new JsonObject { ["terrainId"] = a, ["points"] = Json((0, 0), (30, 0), (30, 20), (0, 20)) });
        f.Run("civil.setBoundary", new JsonObject { ["terrainId"] = b, ["points"] = Json((20, 10), (50, 10), (50, 30), (20, 30)) });
        var r = (JsonObject)f.Run("civil.mergeTerrains", new JsonObject { ["ids"] = new JsonArray(a, b) })!;
        Assert.Null(r["problems"]);
        var outline = ArcOutline.CornersOf(f.Model.Get(a)!, Terrains.BoundaryKey);
        Assert.True(SameLoop([new(0, 0), new(30, 0), new(30, 10), new(50, 10), new(50, 30), new(20, 30), new(20, 20), new(0, 20)], outline), string.Join(" ", outline));
        Assert.Equal(600 + 600 - 100, PlanArea(f, a), 6);
    }

    [Fact]
    public void Merging_terrains_whose_boundaries_lie_apart_says_so_and_covers_all_the_points()
    {
        var f = F();
        var a = Slope(f);
        var b = Slope(f, 60);
        f.Run("civil.setBoundary", new JsonObject { ["terrainId"] = a, ["points"] = Json((5, 5), (30, 5), (30, 30), (5, 30)) });
        var r = (JsonObject)f.Run("civil.mergeTerrains", new JsonObject { ["ids"] = new JsonArray(a, b) })!;
        Assert.Equal(SiteCommands.MergedBoundaryProblem, (string)r["problems"]!.AsArray().Single()!);
        Assert.Null(f.Model.Get(a)!.Attrs[Terrains.BoundaryKey]);
        Assert.Equal(100 * 40, PlanArea(f, a), 6);
    }

    [Fact]
    public void Merging_terrains_none_of_them_bounded_stays_as_it_was()
    {
        var f = F();
        var a = Slope(f);
        var b = Slope(f, 60);
        var r = (JsonObject)f.Run("civil.mergeTerrains", new JsonObject { ["ids"] = new JsonArray(a, b) })!;
        Assert.Null(r["problems"]);
        Assert.Null(f.Model.Get(a)!.Attrs[Terrains.BoundaryKey]);
    }

    [Fact]
    public void The_union_of_outlines_rejoins_a_cut_keeps_an_untouched_arc_and_tells_pieces_apart_and_gaps()
    {
        // A square with an arc on its top, cut down the middle: the union is the square with its arc again.
        List<Vec2> sq = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];
        double[] rises = [0, 0, -2, 0];
        var (left, right) = OutlineCuts.Split(sq, rises, new Vec2(4, -1), new Vec2(4, 11));
        var u = OutlineUnion.Of([new(left.Corners, left.Rises), new(right.Corners, right.Rises)])!.Single!;
        Assert.Equal(ArcOutline.SignedArea(sq, rises), ArcOutline.SignedArea(u.Corners, u.Rises), 9);
        // The arc was cut in two by the line: its two pieces stay arcs of the same circle, so five corners (the cut's top kept).
        Assert.Equal(5, u.Corners.Count);

        // Apart: two outlines; four bars round a court: one outline and a gap.
        List<Vec2> Box(double x0, double y0, double x1, double y1) => [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];
        var apart = OutlineUnion.Of([new(Box(0, 0, 1, 1), null), new(Box(5, 5, 6, 6), null)])!;
        Assert.Equal(2, apart.Outers.Count);
        Assert.Null(apart.Single);
        var court = OutlineUnion.Of([new(Box(0, 0, 10, 2), null), new(Box(8, 0, 10, 10), null), new(Box(0, 8, 10, 10), null), new(Box(0, 0, 2, 10), null)])!;
        Assert.Single(court.Outers);
        Assert.Single(court.Holes);
        Assert.Null(court.Single);
        Assert.Equal(100, ArcOutline.SignedArea(court.Outers[0].Corners, null), 9);
        Assert.Equal(-36, ArcOutline.SignedArea(court.Holes[0].Corners, null), 9);
    }

    [Fact]
    public void A_platforms_cut_and_fill_are_listed_each_beside_the_net_on_the_terrain_and_per_platform_and_go_to_IFC()
    {
        var f = F();
        var t = Slope(f);
        // 20 × 10 from x = 10 to 30, the ground from 0.5 to 1.5, levelled at 1.2: cut where the ground is above 1.2 (x 24–30),
        // ½ · 6 · 0.3 · 10 = 9 m³; fill below it (x 10–24), ½ · 14 · 0.7 · 10 = 49 m³; net −40 m³.
        var pad = (string)f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["x1"] = 10, ["y1"] = 10, ["x2"] = 30, ["y2"] = 20, ["elevation"] = 1.2, ["name"] = "Pad A" })!["id"]!;
        var sets = f.Session.Quantities(t).ToList();
        var terrain = sets.Single(q => q.Name == "Lapin_TerrainQuantities").Quantities;
        Assert.Equal(9, terrain.Single(x => x.Name == "CutVolume").Value, 4);
        Assert.Equal(49, terrain.Single(x => x.Name == "FillVolume").Value, 4);
        Assert.Equal(-40, terrain.Single(x => x.Name == "NetVolume").Value, 4);
        Assert.Equal("Cut", terrain.Single(x => x.Name == "CutVolume").Label);
        Assert.Equal("Fill", terrain.Single(x => x.Name == "FillVolume").Label);

        // A second platform: each has its own set, named after it.
        var pad2 = (string)f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["x1"] = 10, ["y1"] = 25, ["x2"] = 20, ["y2"] = 35, ["elevation"] = 2.0 })!["id"]!;
        var works = f.Session.Quantities(t).Where(q => q.Name == "Lapin_EarthworkQuantities").ToList();
        Assert.Equal(2, works.Count);
        var first = works.Single(w => w.Part == "pad:" + pad);
        Assert.Equal(f.Model.Get(pad)!.Name, first.PartName);
        Assert.Equal(9, first.Quantities.Single(x => x.Name == "CutVolume").Value, 4);
        Assert.Equal(49, first.Quantities.Single(x => x.Name == "FillVolume").Value, 4);
        // 10 × 10 over x 10–20 (ground 0.5–1.0) raised to 2.0: all fill, 10 · 10 · (2 − 0.75) = 125 m³.
        var second = works.Single(w => w.Part == "pad:" + pad2);
        Assert.Equal(0, second.Quantities.Single(x => x.Name == "CutVolume").Value, 4);
        Assert.Equal(125, second.Quantities.Single(x => x.Name == "FillVolume").Value, 4);
        Assert.Equal(9 - 49 - 125, TerrainQ(f, t, "NetVolume"), 4);

        // In the IFC file: the terrain's Qto with cut, fill and net, and one per platform.
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        var text = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("'Lapin_TerrainQuantities'", text);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(text, "IFCQUANTITYVOLUME\\('CutVolume'").Count);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(text, "IFCQUANTITYVOLUME\\('FillVolume'").Count);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(text, "'Lapin_EarthworkQuantities'").Count);
    }

    static double TerrainQ(Fixture f, string t, string name) =>
        f.Session.Quantities(t).Single(q => q.Name == "Lapin_TerrainQuantities").Quantities.Single(x => x.Name == name).Value;
}
