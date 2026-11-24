using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Architecture.Schedules;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.RoofPanels;

namespace Lapin.Engine.Tests;

/// <summary>
/// What the roof panel layout shows (docs/plan-roof-panels.md, stage S2): on a roof that names a profile, the covering is
/// its panels, a part each (a closed profiled sheet in 3D, a plate of the covering's thickness over what it covers for the
/// drawings), the reused offcuts orange, the panels' outlines in plan; the IFC export keeps the covering until stage S6;
/// a roof another roof cuts (decision 847) is panelled as it is built; the Roof panels schedule is shipped.
/// </summary>
public class RoofPanelGeometryTests
{
    static JsonArray Pts(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());

    static string AddRoof(Fixture f, JsonArray points, JsonObject attrs)
    {
        var id = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = points })!["id"]!;
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = id, ["attrs"] = attrs });
        return id;
    }

    static Fixture New() => new(new ArchitectureModule(), new RoofPanelsModule());

    static JsonObject Layout(Fixture f, string roof) => (JsonObject)f.Session.Query("roofpanels.layout", new JsonObject { ["id"] = roof })!;

    static void Panels(Fixture f, string roof, string profile, string? color = null) =>
        f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = roof, ["profile"] = profile, ["color"] = color });

    /// <summary>Every side of the mesh's triangles shared by exactly two of them, once each way: a closed sheet.</summary>
    static void Closed((double[] Xyz, int[] Tris) m, string what)
    {
        var sides = new Dictionary<(int, int), int>();
        for (var t = 0; t < m.Tris.Length; t += 3)
            for (var k = 0; k < 3; k++)
            {
                var e = (m.Tris[t + k], m.Tris[t + (k + 1) % 3]);
                sides[e] = sides.GetValueOrDefault(e) + 1;
            }
        Assert.All(sides, kv => Assert.True(kv.Value == 1 && sides.GetValueOrDefault((kv.Key.Item2, kv.Key.Item1)) == 1, $"{what}: side {kv.Key} is open or doubled"));
    }

    [Theory]
    [InlineData("bilka-clasic-35", "7016", "#383e42")]
    [InlineData("bilka-balcanic", null, "#45322e")]
    [InlineData("bilka-retro-panel", null, "#403a3a")]
    public void The_covering_is_its_panels_a_part_each_profiled_in_3D_and_a_plate_in_drawings(string profile, string? color, string rgb)
    {
        var f = New();
        var roof = AddRoof(f, Pts((0, 0), (10, 0), (10, 6), (0, 6)), new JsonObject { ["shape"] = "hip", ["pitch"] = 30, ["overhang"] = 0.5 });
        var before = f.Session.Geometry(roof)!;
        var covering = before.Parts.Single(p => p.Key == "covering");
        Panels(f, roof, profile, color);
        var g = f.Session.Geometry(roof)!;
        var layout = Layout(f, roof);
        var panels = layout["panels"]!.AsArray();
        // The covering gone, a part per panel in its place, keyed by its code; everything else of the roof as it was.
        Assert.DoesNotContain(g.Parts, p => p.Key == "covering");
        var parts = g.Parts.Where(p => p.Key?.StartsWith("panel:", StringComparison.Ordinal) == true).ToList();
        Assert.Equal(panels.Select(p => $"panel:{p!["id"]}").Order(), parts.Select(p => p.Key!).Order());
        Assert.Equal(before.Parts.Count - 1 + panels.Count, g.Parts.Count);
        // In the covering's material and the profile's colour (its neighbours a shade lighter).
        Assert.All(parts, p => Assert.Equal(covering.Material, p.Material));
        Assert.Contains(parts, p => p.Color == rgb);
        // Each a closed profiled sheet in 3D; for drawings a plate of the covering's thickness over what the panel covers.
        Assert.All(parts, p => Closed(p.Mesh!.Value, p.Key!));
        var cover = (double)layout["totals"]!["coverage"]!;
        var thickness = Roof.Resolve(f.Model.Get(roof)!, f.Model)!.Covering.Thickness;
        Assert.Equal(cover * thickness, parts.Sum(p => Measure.Volume(p.Solid)), 4);
        // The profile: its sheet rises to the waves' (or ribs') height over the plate's underside, never through the roof.
        var plane = f.Session.Geometry(roof)!.Parts.First(p => p.Key!.StartsWith("panel:", StringComparison.Ordinal));
        Assert.True(plane.Mesh!.Value.Tris.Length / 3 > 12);
        // Each panel's outline in plan (overhead, as the roof's lines), from 1:200.
        var outlines = g.Plan!.Where(i => i.Kind == "overhead" && i.MinDetail == PlanDetail.Fine).ToList();
        Assert.Equal(panels.Count, outlines.Count);
        var planArea = panels.Sum(p => Math.Abs(Polygon2.SignedArea(p!["plan"]!.AsArray().Select(v => new Vec2((double)v![0]!, (double)v[1]!)).ToList())));
        Assert.Equal(planArea, outlines.Sum(o => Math.Abs(Polygon2.SignedArea(o.Points))), 6);
    }

    [Fact]
    public void Reused_offcuts_are_orange_and_the_IFC_export_keeps_the_covering()
    {
        var f = New();
        var roof = AddRoof(f, Pts((0, 0), (12, 0), (12, 6), (6, 6), (6, 11), (0, 11)), new JsonObject { ["shape"] = "hip", ["pitch"] = 35, ["overhang"] = 0.5 });
        Panels(f, roof, "bilka-balcanic");
        var reused = Layout(f, roof)["panels"]!.AsArray().Where(p => p!["donor"] != null).Select(p => $"panel:{p!["id"]}").ToHashSet();
        Assert.Equal(5, reused.Count);
        var parts = f.Session.Geometry(roof)!.Parts.Where(p => p.Key?.StartsWith("panel:", StringComparison.Ordinal) == true).ToList();
        Assert.All(parts, p => Assert.Equal(reused.Contains(p.Key!), p.Color == PanelBodies.Reused));
        Assert.Contains(parts.Single(p => reused.Contains(p.Key!) && p.Key == "panel:A04-01-01").Properties!, x => x == ("CutFrom", "A01-01-01"));
        // The IFC export's geometry: the covering as before (the panels as IfcCovering are stage S6), so its GlobalIds stay.
        var ifc = f.Session.Geometry(roof, new GeometryOptions(CutOpenings: false))!;
        Assert.Contains(ifc.Parts, p => p.Key == "covering");
        Assert.DoesNotContain(ifc.Parts, p => p.Key?.StartsWith("panel:", StringComparison.Ordinal) == true);
        // No profile: the covering is back, in one undo step.
        f.Session.Undo();
        Assert.Contains(f.Session.Geometry(roof)!.Parts, p => p.Key == "covering");
    }

    [Fact]
    public void A_layered_roof_has_its_top_layer_replaced_by_the_panels()
    {
        var f = New();
        var roof = AddRoof(f, Pts((0, 0), (10, 0), (10, 6), (0, 6)), new JsonObject
        {
            ["shape"] = "gable", ["pitch"] = 30, ["overhang"] = 0.5, ["gableOverhang"] = 0.3,
            ["layers"] = new JsonArray(new JsonObject { ["name"] = "Insulation", ["thickness"] = 0.12 }, new JsonObject { ["name"] = "Tiles", ["thickness"] = 0.03 }),
        });
        var layers = f.Session.Geometry(roof)!.Parts.Count(p => p.Key?.StartsWith("layer:", StringComparison.Ordinal) == true);
        Assert.Equal(2, layers);
        Panels(f, roof, "bilka-clasic-35", "8017");
        var g = f.Session.Geometry(roof)!;
        Assert.Contains(g.Parts, p => p.Key == "layer:0");
        Assert.DoesNotContain(g.Parts, p => p.Key == "layer:1");
        Assert.Equal(20, g.Parts.Count(p => p.Key?.StartsWith("panel:", StringComparison.Ordinal) == true));
    }

    /// <summary>An L on one storey (decision 847's test): a main block 10 × 6 under a gable along x, a wing 4 wide (x 3–7) to the south under a gable along y drawn on to the main ridge's line; both 30°.</summary>
    static (Fixture F, string Main, string Wing) LShape()
    {
        var f = New();
        JsonObject Attrs(string ridge) => new() { ["shape"] = "gable", ["pitch"] = 30, ["overhang"] = 0.5, ["gableOverhang"] = 0.3, ["snowGuards"] = "False", ["gutterProfile"] = "none", ["downpipes"] = "none", ["ridgeDirection"] = ridge };
        var main = AddRoof(f, Pts((0, 0), (10, 0), (10, 6), (0, 6)), Attrs("x"));
        var wing = AddRoof(f, Pts((3, -5), (7, -5), (7, 3), (3, 3)), Attrs("y"));
        return (f, main, wing);
    }

    static double Q(Fixture f, string id, string name) =>
        f.Session.Quantities(id).Where(s => s.Part == null).SelectMany(s => s.Quantities).Where(q => q.Name == name).Sum(q => q.Value);

    [Fact]
    public void A_roof_another_roof_cuts_is_panelled_as_its_covering_is_built()
    {
        var (f, main, wing) = LShape();
        Panels(f, wing, "bilka-clasic-35");
        Panels(f, main, "bilka-clasic-35");
        var w = Layout(f, wing);
        Assert.Null(w["problem"]);
        // The wing's planes end on the valleys where the coverings cross (y = x − 3 west of its ridge, y = 7 − x east), not
        // on the main ridge's line: its sloped area is the covering's as built, the panels kept the valley gap short of them.
        Assert.Equal(Q(f, wing, "GrossArea"), (double)w["totals"]!["roofArea"]!, 3);
        var whole = 2 * 8.8 * 2.5 / Math.Cos(30 * Math.PI / 180);
        Assert.True((double)w["totals"]!["roofArea"]! < whole - 1);
        foreach (var p in w["panels"]!.AsArray())
            foreach (var v in p!["plan"]!.AsArray())
            {
                var (x, y) = ((double)v![0]!, (double)v[1]!);
                Assert.True(y <= (x < 5 ? x - 3 : 7 - x) - 0.03 + 1e-6, $"{p["id"]} reaches ({x:0.###}, {y:0.###}), past the valley");
            }
        // Those lines are valleys with the other roof, at most 2 ⋅ √2 ⋅ 2.5 m long each in plan, one a side.
        var valleys = w["edges"]!.AsArray().Where(e => (string)e!["kind"]! == "valley").ToList();
        Assert.Equal(2, valleys.Count);
        // The main roof: its south slope less the wing's notch, panelled round it, its panels clear of the wing's.
        var m = Layout(f, main);
        Assert.Null(m["problem"]);
        Assert.Equal(Q(f, main, "GrossArea"), (double)m["totals"]!["roofArea"]!, 3);
        Assert.Contains(m["edges"]!.AsArray(), e => (string)e!["kind"]! == "valley");
        foreach (var p in m["panels"]!.AsArray())
        {
            var pts = p!["plan"]!.AsArray().Select(v => new Vec2((double)v![0]!, (double)v[1]!)).ToList();
            var c = new Vec2(pts.Average(q => q.X), pts.Average(q => q.Y));
            Assert.False(c.X > 3.2 && c.X < 6.8 && c.Y < Math.Min(c.X - 3, 7 - c.X) - 0.05, $"{p["id"]} lies over the wing at ({c.X:0.###}, {c.Y:0.###})");
        }
        // The wing's 3D panels follow (its geometry from the same layout).
        Assert.Equal(w["panels"]!.AsArray().Count, f.Session.Geometry(wing)!.Parts.Count(p => p.Key?.StartsWith("panel:", StringComparison.Ordinal) == true));
    }

    [Fact]
    public void The_roof_panels_schedule_is_shipped_and_counts_what_to_buy()
    {
        var f = New();
        var roof = AddRoof(f, Pts((0, 0), (10, 0), (10, 6), (0, 6)), new JsonObject { ["shape"] = "hip", ["pitch"] = 30, ["overhang"] = 0.5 });
        Panels(f, roof, "bilka-balcanic");
        var made = f.Run("architecture.importSchedule", new JsonObject { ["builtin"] = "roofPanels" })!;
        var schedule = f.Model.Get((string)made["id"]!)!;
        Assert.Equal("Roof panels", schedule.Name);
        var grid = new FieldSchedule().Grid(f.Session.Reader, schedule);
        Assert.Equal(["Roof", "Profile", "Colour", "Length (mm)", "Bought", "Pieces", "Total length (m)", "Sheet bought (m²)"], grid.Columns);
        // The app's hip roof in Balcanic: 1180 × 16, 2230 × 36, 2930 × 4, all bought, longest first; and the totals line.
        var rows = grid.Rows.Where(r => r[0].Length > 0 && r[3].Length > 0).Select(r => $"{r[3]}×{r[5]}").ToList();
        Assert.Equal(["2930×4", "2230×36", "1180×16"], rows);
        var sheet = (4 * 2930 + 36 * 2230 + 16 * 1180) * 1180 / 1e6;
        Assert.Contains(grid.Rows, r => r[^1] == sheet.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
    }
}
