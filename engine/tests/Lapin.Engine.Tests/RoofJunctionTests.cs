using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Where two roofs meet (a tester's report: "the lower roof continues up to the wall, it does not stop at the projection
/// of the upper roof", decision 847). A roof under another one ends where it would meet the upper roof's soffit or fascia,
/// and runs on to the wall only when it stays below them; two roofs on plates at one height meet where their coverings
/// cross (the valley), each kept where it is the higher one.
/// </summary>
public class RoofJunctionTests
{
    static JsonArray Corners(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());

    static string AddRoof(Fixture f, string storey, JsonArray points, JsonObject attrs)
    {
        var id = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = storey, ["points"] = points, ["style"] = "traditional_campie" })!["id"]!;
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = id, ["attrs"] = attrs });
        return id;
    }

    /// <summary>A two-storey block 10 × 6 under a hip roof (30°, eaves 0.6 m out) on its upper storey, and the storey below.</summary>
    static (Fixture F, string Ground, string Upper) TwoStoreyBlock()
    {
        var f = new Fixture();
        var ground = f.Storey;
        var level1 = (string)f.Run("storey.create", new JsonObject { ["name"] = "Level 1" })!["id"]!;
        var upper = AddRoof(f, level1, Corners((0, 0), (10, 0), (10, 6), (0, 6)),
            new JsonObject { ["shape"] = "hip", ["pitch"] = 30, ["overhang"] = 0.6, ["snowGuards"] = "False", ["gutterProfile"] = "none", ["downpipes"] = "none" });
        return (f, ground, upper);
    }

    /// <summary>A ground-floor lean-to 10 × 4 against the block's south wall (y = 0), falling south, its top edge a gable flush with the wall.</summary>
    static string LeanTo(Fixture f, string ground, double pitch) =>
        AddRoof(f, ground, Corners((0, -4), (10, -4), (10, 0), (0, 0)), new JsonObject
        {
            ["shape"] = "shed", ["lowEdge"] = 0, ["pitch"] = pitch, ["overhang"] = 0.5, ["gableOverhang"] = 0,
            ["snowGuards"] = "False", ["gutterProfile"] = "none", ["downpipes"] = "none",
        });

    static Solid Covering(Fixture f, string roof) => f.Session.Geometry(roof)!.Parts.Single(p => p.Key == "covering").Solid;

    static IEnumerable<Vec3> BodyVertices(Fixture f, string roof) =>
        f.Session.Geometry(roof)!.Parts.Where(p => p.Key?.StartsWith("downpipe:") != true).SelectMany(p => p.Solid.Vertices);

    static double Q(Fixture f, string id, string name) =>
        f.Session.Quantities(id).Where(s => s.Part == null).SelectMany(s => s.Quantities).Where(q => q.Name == name).Sum(q => q.Value);

    /// <summary>Whether a vertical line through <paramref name="p"/> passes through the solid.</summary>
    static bool Over(Solid s, Vec2 p)
    {
        foreach (var (a, b, c) in Measure.Triangles(s))
        {
            var (pa, pb, pc) = (s.Vertices[a].Xy, s.Vertices[b].Xy, s.Vertices[c].Xy);
            var d = Vec2.Cross(pb - pa, pc - pa);
            if (Math.Abs(d) < 1e-12) continue;
            var u = Vec2.Cross(p - pa, pc - pa) / d;
            var v = Vec2.Cross(pb - pa, p - pa) / d;
            if (u >= 0 && v >= 0 && u + v <= 1) return true;
        }
        return false;
    }

    /// <summary>The upper roof's underside (the planes under its rafters, its soffit over the overhang) over a plan point.</summary>
    static double Underside(Fixture f, string roof, Vec2 p) => Roof.Solve(Roof.Resolve(f.Model.Get(roof)!, f.Model)!).Shape!.HeightAt(p)!.Value;

    [Theory]
    [InlineData("rafters")]
    [InlineData("collar")]
    [InlineData("trusses")]
    [InlineData("none")]
    public void A_lean_to_rising_into_the_upper_eave_ends_at_its_fascia_not_at_the_wall(string structure)
    {
        var (f, ground, upper) = TwoStoreyBlock();
        // 40° over 4 m: at the block's fascia (y = −0.6) its covering is already over the upper roof's soffit.
        var lean = LeanTo(f, ground, 40);
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = lean, ["attrs"] = new JsonObject { ["structure"] = structure } });

        // Nothing of it is left inside the upper roof's plan over that roof's underside: it does not run on through the eave.
        foreach (var v in BodyVertices(f, lean).Where(v => v.Y > -0.6 + 0.01 && v.X > 0.01 && v.X < 9.99))
            Assert.True(v.Z <= Underside(f, upper, v.Xy) + 0.002, $"({v.X:0.###}, {v.Y:0.###}, {v.Z:0.###}) is in the upper roof");
        // It stops at the fascia's line, not at the wall behind (y = 0), and not short of the upper roof either.
        var cover = Measure.Bounds(Covering(f, lean))!.Value;
        Assert.InRange(cover.Max.Y, -0.61, -0.59);
        Assert.False(Over(Covering(f, lean), new Vec2(5, -0.3)));
        Assert.True(Over(Covering(f, lean), new Vec2(5, -0.7)));
        // The upper roof is whole.
        Assert.True(Over(Covering(f, upper), new Vec2(5, -0.3)));

        // The covering's area follows: its slope from the eave (y = −4.5) to the fascia (−0.6), not to the wall.
        var slope = 10 / Math.Cos(40 * Math.PI / 180);
        Assert.Equal(slope * 3.9, Q(f, lean, "GrossArea"), 2);
        Assert.Equal(10 * 3.9, Q(f, lean, "ProjectedArea"), 2);
    }

    [Fact]
    public void A_lean_to_staying_under_the_upper_eave_runs_on_to_the_wall()
    {
        var (f, ground, upper) = TwoStoreyBlock();
        // 20°: its top at the wall (≈ 4.8 m) stays well under the upper soffit (≈ 5.8–6.1 m): as built, flashed to the wall under the eave.
        var lean = LeanTo(f, ground, 20);
        var cover = Measure.Bounds(Covering(f, lean))!.Value;
        Assert.Equal(0, cover.Max.Y, 3);
        Assert.True(Over(Covering(f, lean), new Vec2(5, -0.3)));
        Assert.Equal(10 * 4.5 / Math.Cos(20 * Math.PI / 180), Q(f, lean, "GrossArea"), 2);
        Assert.True(Over(Covering(f, upper), new Vec2(5, -0.3)));
    }

    [Fact]
    public void A_flat_roof_under_the_upper_eave_runs_on_to_the_wall()
    {
        var (f, ground, upper) = TwoStoreyBlock();
        var terrace = AddRoof(f, ground, Corners((0, -4), (10, -4), (10, 0), (0, 0)), new JsonObject { ["shape"] = "flat", ["overhang"] = 0 });
        Assert.Equal(0, Measure.Bounds(Covering(f, terrace))!.Value.Max.Y, 3);
        Assert.Equal(40, Q(f, terrace, "GrossArea"), 6);
    }

    /// <summary>An L on one storey: a main block 10 × 6, gable roof with its ridge along x (y = 3), and a wing 4 wide (x 3–7) to the south, gable roof with its ridge along y; both 30°, eaves 0.5 m out.</summary>
    static (Fixture F, string Main, string Wing) LShape(double wingEnd)
    {
        var f = new Fixture();
        var attrs = () => new JsonObject { ["shape"] = "gable", ["pitch"] = 30, ["overhang"] = 0.5, ["gableOverhang"] = 0.3, ["snowGuards"] = "False", ["gutterProfile"] = "none", ["downpipes"] = "none" };
        var main = AddRoof(f, f.Storey, Corners((0, 0), (10, 0), (10, 6), (0, 6)), attrs().Also("ridgeDirection", "x"));
        var wing = AddRoof(f, f.Storey, Corners((3, -5), (7, -5), (7, wingEnd), (3, wingEnd)), attrs().Also("ridgeDirection", "y"));
        return (f, main, wing);
    }

    [Fact]
    public void A_lower_ridge_drawn_into_the_higher_roof_ends_on_the_valleys_where_the_coverings_cross()
    {
        // The wing drawn on to the main ridge's line (y = 3): its ridge (1.15 m up) meets the main roof's slope at y = 2,
        // the valleys run from there down at 45° to where the eaves meet.
        var (f, main, wing) = LShape(3);
        var (mc, wc) = (Covering(f, main), Covering(f, wing));
        // Over the valley's wing side the wing is on top and the main roof gives way; past the valleys, the other way round.
        Assert.True(Over(wc, new Vec2(5, 1.0)));
        Assert.False(Over(mc, new Vec2(5, 1.0)));
        Assert.False(Over(wc, new Vec2(5, 2.5)));
        Assert.True(Over(mc, new Vec2(5, 2.5)));
        Assert.True(Over(wc, new Vec2(3.4, 0.2)));
        Assert.False(Over(mc, new Vec2(3.4, 0.2)));
        Assert.False(Over(wc, new Vec2(3.2, 0.6)));
        Assert.True(Over(mc, new Vec2(3.2, 0.6)));
        // Over the whole overlap one covering, never two and never none.
        OneCoveringOver(mc, wc, 2.5, 7.5, -0.5, 3.3);
        // The wing's ridge line ends on the valleys' meeting (from its south rake, y = −5.3, to y = 2), in plan and in the takeoff.
        var ridge = f.Session.Geometry(wing)!.Plan!.Where(p => p.Kind == "ridge").SelectMany(p => p.Points).ToList();
        Assert.Equal(2, ridge.Max(p => p.Y), 2);
        Assert.Equal(7.3, Q(f, wing, "RidgeLength"), 2);
    }

    [Fact]
    public void A_wing_drawn_to_the_main_wall_takes_the_main_eave_away_where_it_runs_over_it()
    {
        // The wing drawn to the main block's wall (y = 0): the main roof's eave, 0.5 m out over the wing, meets the wing's
        // covering on the valleys from the eaves' corners; where the wing is higher the main eave stops at it.
        var (f, main, wing) = LShape(0);
        var (mc, wc) = (Covering(f, main), Covering(f, wing));
        Assert.True(Over(wc, new Vec2(5, -0.25)));
        Assert.False(Over(mc, new Vec2(5, -0.25)));
        Assert.True(Over(mc, new Vec2(2.7, -0.1)));
        Assert.False(Over(wc, new Vec2(2.7, -0.1)));
        OneCoveringOver(mc, wc, 2.5, 7.5, -0.5, -0.01);
    }

    static void OneCoveringOver(Solid a, Solid b, double x0, double x1, double y0, double y1)
    {
        // Off the lines the cuts run along (the valleys at 45°, the ridges), by a step that never lands on them.
        for (var x = x0 + 0.0137; x < x1; x += 0.191)
            for (var y = y0 + 0.0071; y < y1; y += 0.173)
            {
                var p = new Vec2(x, y);
                var n = (Over(a, p) ? 1 : 0) + (Over(b, p) ? 1 : 0);
                Assert.True(n == 1, $"{n} coverings over ({x:0.###}, {y:0.###})");
            }
    }
}

static class JsonObjectAlso
{
    public static JsonObject Also(this JsonObject o, string key, JsonNode? value) { o[key] = value; return o; }
}
