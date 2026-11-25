using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.RoofPanels;

namespace Lapin.Engine.Tests;

/// <summary>
/// The roof panel layout's takeoff and openings (docs/plan-roof-panels.md, stage S3). The numbers are the Panotaj Bilka app's
/// (v1.3.3, the owner's Python app): each roof below was exported to IFC by Lapin on 25.11.2026 and run through its
/// <c>analyze(…)</c> (a copy, outside its folder), with <c>--wood-fastener</c>, <c>--ridge</c> and <c>--retro-support</c> where
/// named; the lines are its estimate's quantities (prices aside), the stats its report's <c>stats</c>, the abutments its edges.
/// The app reads walls from the IFC's faceted breps; Lapin from its walls themselves.
/// </summary>
public class RoofPanelTakeoffTests
{
    static JsonArray Pts(params double[] xy)
    {
        var a = new JsonArray();
        for (var i = 0; i < xy.Length; i += 2) a.Add(new JsonArray(xy[i], xy[i + 1]));
        return a;
    }

    static readonly double[] House = [0, 0, 10, 0, 10, 6, 0, 6];
    static readonly double[] L = [0, 0, 12, 0, 12, 6, 6, 6, 6, 11, 0, 11];

    static string AddRoof(Fixture f, double[] pts, JsonObject attrs, string? storey = null)
    {
        var id = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = storey ?? f.Storey, ["points"] = Pts(pts) })!["id"]!;
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = id, ["attrs"] = attrs });
        return id;
    }

    /// <summary>
    /// The roofs the reference numbers were taken on: the S1 gable and L hip; a gable whose west verge is flush with the face of a
    /// 9 m wall (the default 280 mm brick wall's east face on x = 0); a lean-to (shed, 15°) whose top edge is under a 9 m wall's face.
    /// </summary>
    internal static (Fixture F, string Roof) Make(string name, params ILapinModule[] more)
    {
        var f = new Fixture([new ArchitectureModule(), new RoofPanelsModule(), .. more]);
        switch (name)
        {
            case "gable": return (f, AddRoof(f, House, new JsonObject { ["shape"] = "gable", ["pitch"] = 30, ["overhang"] = 0.5, ["gableOverhang"] = 0.3 }));
            case "lhip": return (f, AddRoof(f, L, new JsonObject { ["shape"] = "hip", ["pitch"] = 35, ["overhang"] = 0.5 }));
            case "wallgable":
                f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = -0.14, ["y1"] = 7, ["x2"] = -0.14, ["y2"] = -1, ["height"] = 9 });
                return (f, AddRoof(f, House, new JsonObject { ["shape"] = "gable", ["pitch"] = 30, ["overhang"] = 0.5, ["gableOverhang"] = 0.0, ["ridgeDirection"] = "x" }));
            case "leanto":
                f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 11, ["y1"] = 6.14, ["x2"] = -1, ["y2"] = 6.14, ["height"] = 9 });
                return (f, AddRoof(f, House, new JsonObject { ["shape"] = "shed", ["pitch"] = 15, ["overhang"] = 0.4, ["gableOverhang"] = 0.3 }));
            default: throw new ArgumentException(name);
        }
    }

    static JsonObject Layout(Fixture f, string roof, JsonObject? extra = null)
    {
        var q = new JsonObject { ["id"] = roof };
        if (extra != null) foreach (var (k, v) in extra) q[k] = v?.DeepClone();
        return (JsonObject)f.Session.Query("roofpanels.layout", q)!;
    }

    static string Items(JsonObject l) => string.Join(" ", l["takeoff"]!["items"]!.AsArray().Select(i => $"{i!["code"]}:{(double)i["quantity"]!:0.##}"));

    static double Stat(JsonObject l, string key) => (double)l["takeoff"]!["stats"]![key]!;

    static string Edges(JsonObject l) => string.Join(" ", l["edges"]!.AsArray().GroupBy(e => (string)e!["kind"]!)
        .Select(g => $"{g.Key}:{g.Count()}:{Math.Round(g.Sum(e => (double)e!["length"]!), 4)}"));

    [Theory]
    [InlineData("gable", "bilka-balcanic", null, "membrane:2 membrane-tape:2 batten:90 counter-batten:40 wood-nails:2 screw-short:2 screw-long:6 ridge-large:6 ridge-roll-large:3 gable-cap:2 eave:12 verge:12 snow-guard:12")]
    [InlineData("gable", "bilka-clasic-35", null, "membrane:2 membrane-tape:2 batten:90 counter-batten:40 wood-nails:2 screw-short:2 screw-long:7 ridge-large:6 ridge-roll-large:3 gable-cap:2 eave:12 verge:12 snow-guard:12")]
    // --ridge small --wood-fastener screws: the small ridge cap and its roll, 558 screws instead of two boxes of nails.
    [InlineData("gable", "bilka-clasic-35", "small screws", "membrane:2 membrane-tape:2 batten:90 counter-batten:40 wood-screws:558 screw-short:2 screw-long:7 ridge-small:6 ridge-roll-small:3 gable-cap:2 eave:12 verge:12 snow-guard:12")]
    // Retro Panel on its boards (150 mm, 50 mm apart), and on the older guide's battens (--retro-support attached).
    [InlineData("gable", "bilka-retro-panel", null, "membrane:2 membrane-tape:2 board:117 counter-batten:40 wood-nails:2 screw-flat:1161 screw-sealed:2 ridge-retro:6 gable-cap:2 eave-retro:12 verge-retro:12 ridge-support:12 butyl-tape:3 snow-guard-retro:12")]
    [InlineData("gable", "bilka-retro-panel", "attached", "membrane:2 membrane-tape:2 batten:90 counter-batten:40 wood-nails:2 screw-flat:745 screw-sealed:2 ridge-retro:6 gable-cap:2 eave-retro:12 verge-retro:12 ridge-support:12 butyl-tape:3 snow-guard-retro:12")]
    // The L hip with its valley: hip caps and their end caps, ridge nodes, the valley and its seal, Retro's lap elements and valley clips.
    [InlineData("lhip", "bilka-balcanic", null, "membrane:3 membrane-tape:4 batten:165 counter-batten:68 wood-nails:3 screw-short:2 screw-long:11 ridge-large:7 hip-large:15 hip-end-large:5 ridge-roll-large:9 ridge-nodes:4 valley:3 valley-seal:13 eave:29 snow-guard:24")]
    [InlineData("lhip", "bilka-clasic-35", null, "membrane:3 membrane-tape:4 batten:165 counter-batten:68 wood-nails:3 screw-short:2 screw-long:12 ridge-large:7 hip-large:15 hip-end-large:5 ridge-roll-large:9 ridge-nodes:4 valley:3 valley-seal:13 eave:29 snow-guard:24")]
    [InlineData("lhip", "bilka-retro-panel", null, "membrane:3 membrane-tape:4 board:210 counter-batten:68 wood-nails:4 screw-flat:2390 screw-sealed:3 ridge-retro:7 hip-retro:15 hip-end-retro:5 ridge-nodes:4 valley-retro:3 eave-retro:29 ridge-support:44 lap-small:6 butyl-tape:7 valley-clips:44 snow-guard-retro:24")]
    // A gable against a higher wall: two side abutments for two verges, one ridge end cap.
    [InlineData("wallgable", "bilka-balcanic", null, "membrane:2 membrane-tape:2 batten:84 counter-batten:37 wood-nails:2 screw-short:2 screw-long:6 ridge-large:6 ridge-roll-large:3 gable-cap:1 eave:12 verge:6 wall-side:6 snow-guard:11")]
    [InlineData("wallgable", "bilka-clasic-35", null, "membrane:2 membrane-tape:2 batten:84 counter-batten:37 wood-nails:2 screw-short:2 screw-long:6 ridge-large:6 ridge-roll-large:3 gable-cap:1 eave:12 verge:6 wall-side:6 snow-guard:11")]
    [InlineData("wallgable", "bilka-retro-panel", null, "membrane:2 membrane-tape:2 board:111 counter-batten:37 wood-nails:2 screw-flat:1103 screw-sealed:2 ridge-retro:6 gable-cap:1 eave-retro:12 verge-retro:6 wall-side-retro:6 ridge-support:12 butyl-tape:3 snow-guard-retro:11")]
    // A lean-to under a wall: its top edge a top abutment.
    [InlineData("leanto", "bilka-balcanic", null, "membrane:2 membrane-tape:2 batten:75 counter-batten:34 wood-nails:2 screw-short:1 screw-long:6 eave:6 verge:8 wall-top:6 snow-guard:12")]
    [InlineData("leanto", "bilka-clasic-35", null, "membrane:2 membrane-tape:2 batten:75 counter-batten:34 wood-nails:2 screw-short:1 screw-long:6 eave:6 verge:8 wall-top:6 snow-guard:12")]
    [InlineData("leanto", "bilka-retro-panel", null, "membrane:2 membrane-tape:2 board:98 counter-batten:34 wood-nails:2 screw-flat:951 screw-sealed:2 eave-retro:6 verge-retro:8 wall-top-retro:6 ridge-support:6 butyl-tape:2 snow-guard-retro:12")]
    public void The_takeoff_is_the_Panotaj_Bilka_apps_estimate(string roof, string profile, string? options, string items)
    {
        var (f, id) = Make(roof);
        var extra = new JsonObject { ["profile"] = profile };
        if (options?.Contains("small") == true) extra["ridge"] = "small";
        if (options?.Contains("screws") == true) extra["fixing"] = "screws";
        if (options == "attached") extra["support"] = "attached";
        var l = Layout(f, id, extra);
        Assert.Null(l["problem"]);
        Assert.Equal(items, Items(l));
    }

    [Theory]
    // roof, profile, support: batten (m), counterBatten (m), foilOverlap (m²), edgeScrews, shortScrews, retroFasteners, valleyBatten, hipCaps, threeWay, gableCaps, transverseJoints, snow (m)
    [InlineData("gable", "bilka-balcanic", null, 254.4, 113.1607, 4.24, 88, 216, 0, 0, 0, 0, 2, 20, 20.6)]
    [InlineData("gable", "bilka-retro-panel", null, 445.2, 113.1607, 4.24, 88, 0, 882, 0, 0, 0, 2, 0, 20.6)]
    [InlineData("gable", "bilka-retro-panel", "attached", 254.4, 113.1607, 4.24, 88, 0, 504, 0, 0, 0, 2, 0, 20.6)]
    [InlineData("lhip", "bilka-balcanic", null, 468.738, 193.7711, 8.7991, 206, 434, 0, 13, 5, 3, 0, 32, 43.4)]
    [InlineData("lhip", "bilka-clasic-35", null, 468.738, 193.7711, 8.7991, 206, 421, 0, 13, 5, 3, 0, 0, 43.4)]
    [InlineData("lhip", "bilka-retro-panel", null, 797.2414, 193.7711, 8.7991, 206, 0, 1612, 22, 5, 3, 0, 0, 43.4)]
    [InlineData("wallgable", "bilka-clasic-35", null, 240, 105.0777, 4, 82, 192, 0, 0, 0, 0, 1, 0, 19.4)]
    [InlineData("leanto", "bilka-retro-panel", null, 371, 97.1089, 4.24, 44, 0, 735, 0, 0, 0, 0, 0, 20.6)]
    public void The_battens_counter_battens_membrane_and_fixings_count_as_the_app_does(string roof, string profile, string? support,
        double batten, double counter, double foil, int edgeScrews, int stitches, int field, int valleyBatten, int hipCaps, int threeWay, int gableCaps, int joints, double snow)
    {
        var (f, id) = Make(roof);
        var extra = new JsonObject { ["profile"] = profile };
        if (support != null) extra["support"] = support;
        var l = Layout(f, id, extra);
        Assert.Equal(batten, Stat(l, "batten"), 3);
        // The counter-battens: the native rafters' own length (Lapin_RoofStructureQuantities' RafterLength), at their spacing.
        Assert.Equal(counter, Stat(l, "counterBatten"), 3);
        Assert.Equal(0.8, Stat(l, "rafterSpacing"), 9);
        Assert.False((bool)l["takeoff"]!["raftersEstimated"]!);
        Assert.Equal(foil, Stat(l, "foilOverlap"), 3);
        Assert.Equal(edgeScrews, Stat(l, "edgeScrews"));
        Assert.Equal(stitches, Stat(l, "shortScrews"));
        Assert.Equal(field, Stat(l, "retroFasteners"));
        Assert.Equal(valleyBatten, Stat(l, "valleyBatten"));
        Assert.Equal(hipCaps, Stat(l, "hipCaps"));
        Assert.Equal(threeWay, Stat(l, "threeWay"));
        Assert.Equal(gableCaps, Stat(l, "gableCaps"));
        Assert.Equal(joints, Stat(l, "transverseJoints"));
        Assert.Equal(snow, Stat(l, "snow"), 3);
        // Each slope's share adds up to the roof's.
        var faces = l["takeoff"]!["faces"]!.AsArray();
        Assert.Equal(batten, faces.Sum(x => (double)x!["battens"]!), 2);
        Assert.Equal(foil, faces.Sum(x => (double)x!["membraneLaps"]!), 3);
    }

    [Theory]
    // The app's edges on the same roofs: the verges against the wall are side abutments, the lean-to's top a top abutment.
    [InlineData("wallgable", "eave:2:20 verge:2:8.0829 ridge:1:10 wallSide:2:8.0829")]
    [InlineData("leanto", "eave:1:10.6 verge:2:13.8727 wallTop:1:10.6")]
    public void Edges_against_a_wall_rising_over_them_are_wall_abutments(string roof, string edges)
    {
        var (f, id) = Make(roof);
        var l = Layout(f, id, new JsonObject { ["profile"] = "bilka-clasic-35" });
        Assert.Equal(edges, Edges(l));
        // A wall no higher than the edge is no abutment: the same wall 2 m high leaves verges.
        var wall = f.Model.OfClass(IfcClass.Wall).Single();
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall.Id, ["height"] = 2 });
        Assert.DoesNotContain("wall", Edges(Layout(f, id, new JsonObject { ["profile"] = "bilka-clasic-35" })));
    }

    [Fact]
    public void A_lean_to_ending_under_an_upper_roof_abuts_it()
    {
        // RoofJunctionTests' block: a hip roof on the upper storey, a 40° lean-to on the ground floor rising into its eave.
        var f = new Fixture(new ArchitectureModule(), new RoofPanelsModule());
        var ground = f.Storey;
        var level1 = (string)f.Run("storey.create", new JsonObject { ["name"] = "Level 1" })!["id"]!;
        AddRoof(f, [0, 0, 10, 0, 10, 6, 0, 6], new JsonObject { ["shape"] = "hip", ["pitch"] = 30, ["overhang"] = 0.6, ["snowGuards"] = "False", ["gutterProfile"] = "none", ["downpipes"] = "none" }, level1);
        var lean = AddRoof(f, [0, -4, 10, -4, 10, 0, 0, 0], new JsonObject
        {
            ["shape"] = "shed", ["lowEdge"] = 0, ["pitch"] = 40, ["overhang"] = 0.5, ["gableOverhang"] = 0,
            ["snowGuards"] = "False", ["gutterProfile"] = "none", ["downpipes"] = "none",
        }, ground);
        var l = Layout(f, lean, new JsonObject { ["profile"] = "bilka-clasic-35" });
        Assert.Null(l["problem"]);
        // One slope, the upper roof's three regions over it taken away as one (one of them with a corner twice: no duplicate plane).
        Assert.Single(l["faces"]!.AsArray());
        // Its top, where the upper roof's fascia takes it away, is a top abutment (its flashing), not a top edge.
        var top = l["edges"]!.AsArray().Where(e => (string)e!["kind"]! == "wallTop").ToList();
        Assert.Single(top);
        Assert.Equal(10, (double)top[0]!["length"]!, 2);
        Assert.DoesNotContain(l["edges"]!.AsArray(), e => (string)e!["kind"]! == "topEdge");
        Assert.Contains(l["takeoff"]!["items"]!.AsArray(), i => (string)i!["code"]! == "wall-top");
    }

    static double Overlap(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> hole)
    {
        // The part of a over the hole (a convex region): a clipped by each of its sides.
        var r = a.ToList();
        for (var i = 0; i < hole.Count && r.Count >= 3; i++)
        {
            var (p, q) = (hole[i], hole[(i + 1) % hole.Count]);
            var inward = (q - p).Perp;
            r = Clip.Trim(r, inward, Vec2.Dot(p, inward));
        }
        return Clip.Area(r);
    }

    static List<Vec2> Uv(JsonNode? pts) => [.. pts!.AsArray().Select(v => new Vec2((double)v![0]!, (double)v[1]!))];

    [Theory]
    [InlineData("bilka-balcanic")]
    [InlineData("bilka-clasic-35")]
    [InlineData("bilka-retro-panel")]
    public void A_roof_window_is_cut_out_of_the_panels_with_its_flashing_kit(string profile)
    {
        var (f, id) = Make("gable");
        var before = Layout(f, id, new JsonObject { ["profile"] = profile });
        // A 780 × 1180 roof window on the south slope.
        f.Run("architecture.addRoofOpening", new JsonObject { ["x"] = 4.3, ["y"] = 1.2 });
        f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = profile });
        var l = Layout(f, id);
        Assert.Null(l["problem"]);
        Assert.Empty(l["warnings"]!.AsArray());
        var face = l["faces"]!.AsArray().Single(x => x!["holes"]!.AsArray().Count > 0)!;
        var hole = Uv(face["holes"]![0]!["outline"]);
        var area = Clip.Area(hole);
        Assert.Equal(0.78 * 1.18, area, 3);
        // What the panels cover: the roof less the window, and no panel over it.
        Assert.Equal((double)before["totals"]!["coverage"]! - area, (double)l["totals"]!["coverage"]!, 4);
        var mine = l["panels"]!.AsArray().Where(p => (string)p!["face"]! == (string)face["id"]!).ToList();
        Assert.All(mine, p => Assert.True(Overlap(Uv(p!["net"]), hole) < 1e-7, $"{p!["id"]} lies over the window"));
        // The column it crosses is laid below it, beside it and above it: more panels there than without it.
        Assert.True(l["panels"]!.AsArray().Count > before["panels"]!.AsArray().Count);
        // Its edges: the kit's bottom, top and two sides, one kit.
        var round = l["edges"]!.AsArray().Where(e => e!["opening"] != null).Select(e => (string)e!["kind"]!).Order();
        Assert.Equal(["windowBottom", "windowSide", "windowSide", "windowTop"], round);
        Assert.Contains(l["takeoff"]!["items"]!.AsArray(), i => (string)i!["code"]! == "window-kit" && (double)i["quantity"]! == 1);
        // The battens and the membrane less the window.
        Assert.True(Stat(l, "batten") < Stat(before, "batten"));
        Assert.Equal(Stat(before, "roofArea") - area, Stat(l, "roofArea"), 3);
        // The 3D panels follow: a closed sheet each, none over the window.
        var parts = f.Session.Geometry(id)!.Parts.Where(p => p.Key?.StartsWith("panel:", StringComparison.Ordinal) == true).ToList();
        Assert.Equal(l["panels"]!.AsArray().Count, parts.Count);
        foreach (var part in parts)
        {
            var (_, tris) = part.Mesh!.Value;
            var sides = new Dictionary<(int, int), int>();
            for (var t = 0; t < tris.Length; t += 3)
                for (var k = 0; k < 3; k++) { var e = (tris[t + k], tris[t + (k + 1) % 3]); sides[e] = sides.GetValueOrDefault(e) + 1; }
            Assert.All(sides, kv => Assert.True(kv.Value == 1 && sides.GetValueOrDefault((kv.Key.Item2, kv.Key.Item1)) == 1, $"{part.Key}: side {kv.Key} is open"));
            Assert.True(Measure.Volume(part.Solid) > 0, $"{part.Key} has no plate");
        }
    }

    [Fact]
    public void A_dormer_is_cut_out_with_its_abutments_and_valleys()
    {
        var (f, id) = Make("gable");
        f.Run("architecture.addRoofOpening", new JsonObject { ["x"] = 5, ["y"] = 0.2, ["kind"] = "dormer", ["width"] = 1.6, ["wallHeight"] = 0.6 });
        var l = Layout(f, id, new JsonObject { ["profile"] = "bilka-clasic-35" });
        Assert.Null(l["problem"]);
        // The front wall's apron, the cheeks, the dormer roof's two valleys with the slope.
        var round = l["edges"]!.AsArray().Where(e => e!["opening"] != null).Select(e => (string)e!["kind"]!).Order();
        Assert.Equal(["valley", "valley", "wallSide", "wallSide", "wallTop"], round);
        var codes = l["takeoff"]!["items"]!.AsArray().Select(i => (string)i!["code"]!).ToList();
        Assert.Contains("wall-top", codes);
        Assert.Contains("wall-side", codes);
        Assert.DoesNotContain("window-kit", codes);
        var face = l["faces"]!.AsArray().Single(x => x!["holes"]!.AsArray().Count > 0)!;
        var hole = Uv(face["holes"]![0]!["outline"]);
        Assert.All(l["panels"]!.AsArray().Where(p => (string)p!["face"]! == (string)face["id"]!), p => Assert.True(Overlap(Uv(p!["net"]), hole) < 1e-7, $"{p!["id"]} lies over the dormer"));
    }

    [Fact]
    public void The_options_are_kept_on_the_roof_and_the_accessories_schedule_is_shipped()
    {
        var (f, id) = Make("lhip");
        f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = "bilka-clasic-35", ["ridge"] = "small", ["fixing"] = "screws" });
        var stored = f.Model.Get(id)!.Attrs[RoofPanels.RoofPanels.Key]!.AsObject();
        Assert.Equal("small", (string)stored["ridge"]!);
        Assert.Equal("screws", (string)stored["fixing"]!);
        var l = Layout(f, id);
        Assert.Equal("small", (string)l["takeoff"]!["ridge"]!);
        Assert.Contains(l["takeoff"]!["items"]!.AsArray(), i => (string)i!["code"]! == "hip-small");
        Assert.Contains(l["takeoff"]!["items"]!.AsArray(), i => (string)i!["code"]! == "wood-screws");
        // The defaults are not stored; an option the profile has not is refused.
        f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = "bilka-clasic-35", ["ridge"] = "large", ["fixing"] = "nails" });
        Assert.Single(f.Model.Get(id)!.Attrs[RoofPanels.RoofPanels.Key]!.AsObject());
        Assert.Throws<CommandException>(() => f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = "bilka-retro-panel", ["ridge"] = "small" }));
        Assert.Throws<CommandException>(() => f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = "bilka-clasic-35", ["support"] = "boards" }));
        Assert.Throws<CommandException>(() => f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = "bilka-clasic-35", ["fixing"] = "glue" }));
        f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = "bilka-retro-panel", ["support"] = "attached" });
        Assert.Equal("attached", (string)Layout(f, id)["takeoff"]!["support"]!);

        // The Roof panel accessories schedule: a row a line of the takeoff, in its unit.
        f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = "bilka-balcanic" });
        var made = f.Run("architecture.importSchedule", new JsonObject { ["builtin"] = "roofPanelItems" })!;
        var schedule = f.Model.Get((string)made["id"]!)!;
        Assert.Equal("Roof panel accessories", schedule.Name);
        var grid = new FieldSchedule().Grid(f.Session.Reader, schedule);
        Assert.Equal(["Roof", "Group", "Item", "Quantity", "Unit", "Needed", "Needed in"], grid.Columns);
        var rows = grid.Rows.Where(r => r[2].Length > 0).ToDictionary(r => r[2], r => r[3]);
        Assert.Equal("15", rows["Hip cap, large (290 mm)"]);
        Assert.Equal("165", rows["Batten 50 × 30 mm"]);
        Assert.Equal("3", rows["Breathable roof membrane 150 g/m²"]);
    }
}
