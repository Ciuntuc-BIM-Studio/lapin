using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.RoofPanels;

namespace Lapin.Engine.Tests;

/// <summary>
/// The roof panel layout (docs/plan-roof-panels.md, stage 1). The numbers are the Panotaj Bilka app's (v1.3.3, the owner's
/// Python app, the behaviour this module rewrites): each roof below was exported to IFC by Lapin and laid out by
/// <c>python -m bilka_roof &lt;ifc&gt; --model …</c> on 24.11.2026; the panels to buy, those laid, the offcuts reused and
/// the edges are its report's (<c>purchase_by_length</c>, <c>installed_by_length</c>, <c>recoveries</c>, <c>edges</c>).
/// </summary>
public class RoofPanelTests
{
    static JsonArray Pts(params double[] xy)
    {
        var a = new JsonArray();
        for (var i = 0; i < xy.Length; i += 2) a.Add(new JsonArray(xy[i], xy[i + 1]));
        return a;
    }

    static readonly double[] House = [0, 0, 10, 0, 10, 6, 0, 6];
    static readonly double[] L = [0, 0, 12, 0, 12, 6, 6, 6, 6, 11, 0, 11];

    /// <summary>The roofs the reference numbers were taken on.</summary>
    static (double[] Points, JsonObject Attrs) RoofOf(string name) => name switch
    {
        "gable" => (House, new JsonObject { ["shape"] = "gable", ["pitch"] = 30, ["overhang"] = 0.5, ["gableOverhang"] = 0.3 }),
        "hip" => (House, new JsonObject { ["shape"] = "hip", ["pitch"] = 30, ["overhang"] = 0.5 }),
        "lgable" => (L, new JsonObject { ["shape"] = "gable", ["pitch"] = 40, ["overhang"] = 0.4, ["gableOverhang"] = 0.3 }),
        "lhip" => (L, new JsonObject { ["shape"] = "hip", ["pitch"] = 35, ["overhang"] = 0.5 }),
        "mansard" => ([0, 0, 10, 0, 10, 8, 0, 8], new JsonObject { ["shape"] = "mansard", ["pitch"] = 60, ["upperPitch"] = 25, ["overhang"] = 0.4 }),
        _ => throw new ArgumentException(name),
    };

    static (Fixture F, string Roof) Make(string name)
    {
        var f = new Fixture(new ArchitectureModule(), new RoofPanelsModule());
        var (pts, attrs) = RoofOf(name);
        var id = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts(pts) })!["id"]!;
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = id, ["attrs"] = attrs });
        return (f, id);
    }

    static JsonObject Layout(Fixture f, string roof, string? profile = null, int? maxLength = null)
    {
        var q = new JsonObject { ["id"] = roof };
        if (profile != null) q["profile"] = profile;
        if (maxLength != null) q["maxLength"] = maxLength;
        return (JsonObject)f.Session.Query("roofpanels.layout", q)!;
    }

    static string Counts(JsonNode? n) => string.Join(" ", n!.AsObject().Select(kv => $"{kv.Key}×{kv.Value}"));

    [Theory]
    // Two slopes of 10.6 × 4.04 m: one panel a column, 10 columns a slope.
    [InlineData("gable", "bilka-balcanic", null, "2230×40", "2230×40", 0)]
    [InlineData("gable", "bilka-clasic-35", null, "4350×20", "4350×20", 0)]
    [InlineData("gable", "bilka-retro-panel", null, "4100×44", "4100×44", 0)]
    [InlineData("hip", "bilka-balcanic", null, "1180×16 2230×36 2930×4", "1180×16 2230×36 2930×4", 0)]
    [InlineData("hip", "bilka-clasic-35", null, "1200×8 2600×8 4000×8 4350×10", "1200×8 2600×8 4000×8 4350×10", 0)]
    [InlineData("hip", "bilka-retro-panel", null, "700×8 1200×8 1800×8 2400×8 3000×8 3500×8 4100×24", "700×8 1200×8 1800×8 2400×8 3000×8 3500×8 4100×24", 0)]
    // An L with a valley: hip offcuts cut the valley pieces (Balcanic and Clasic), never Retro Panel's.
    [InlineData("lgable", "bilka-balcanic", null, "1180×8 2230×32 2930×34", "1180×9 2230×35 2930×34", 4)]
    [InlineData("lgable", "bilka-clasic-35", null, "1200×6 2600×5 4000×5 4700×27", "1200×7 2600×7 4000×6 4700×27", 4)]
    [InlineData("lgable", "bilka-retro-panel", null, "600×5 1000×5 1400×6 1800×5 2200×3 2600×7 3000×3 3400×6 3800×5 4200×3 4500×52", "600×5 1000×5 1400×6 1800×5 2200×3 2600×7 3000×3 3400×6 3800×5 4200×3 4500×52", 0)]
    [InlineData("lhip", "bilka-balcanic", null, "1180×19 2230×53 2930×12", "1180×20 2230×57 2930×12", 5)]
    [InlineData("lhip", "bilka-clasic-35", null, "1200×8 2600×12 4000×12 4350×18", "1200×10 2600×14 4000×12 4350×18", 4)]
    [InlineData("lhip", "bilka-retro-panel", null, "700×12 1300×12 1900×12 2500×12 3100×12 3700×12 4300×42", "700×12 1300×12 1900×12 2500×12 3100×12 3700×12 4300×42", 0)]
    // Retro Panel at 4000 mm at most (--retro-limit 4000): joints on the 4.27 m slopes, every other column's 800 mm lower.
    [InlineData("lhip", "bilka-retro-panel", 4000, "700×34 1500×32 1900×12 2700×12 3100×12 3200×20 3900×12 4000×22", "700×34 1500×32 1900×12 2700×12 3100×12 3200×20 3900×12 4000×22", 0)]
    // A mansard's two tiers (60° and 25°).
    [InlineData("mansard", "bilka-balcanic", null, "1180×22 2230×20 2930×36", "1180×22 2230×20 2930×36", 0)]
    [InlineData("mansard", "bilka-clasic-35", null, "850×6 1550×6 2250×10 2950×32 3650×10", "850×6 1550×6 2250×10 2950×32 3650×10", 0)]
    [InlineData("mansard", "bilka-retro-panel", null, "900×12 1300×12 1700×12 2100×4 2500×8 2900×72 3400×16", "900×12 1300×12 1700×12 2100×4 2500×8 2900×72 3400×16", 0)]
    public void The_panels_to_buy_are_the_Panotaj_Bilka_apps(string roof, string profile, int? maxLength, string buy, string installed, int reused)
    {
        var (f, id) = Make(roof);
        var r = Layout(f, id, profile, maxLength);
        Assert.Null(r["problem"]);
        Assert.Equal(buy, Counts(r["purchase"]));
        Assert.Equal(installed, Counts(r["installed"]));
        Assert.Equal(reused, (int)r["totals"]!["reused"]!);
        // Every panel bought is laid or gives its offcut; what the panels cover is the roof less the valley gaps.
        var panels = r["panels"]!.AsArray();
        Assert.Equal(panels.Count, (int)r["totals"]!["panels"]!);
        Assert.Equal(panels.Count - reused, panels.Count(p => (int)p!["buy"]! == 1));
    }

    [Theory]
    [InlineData("gable", 85.6788, 85.6788, "eave:2:21.2 verge:4:16.1658 ridge:1:10.6")]
    [InlineData("hip", 88.9119, 88.9119, "eave:4:36 hip:4:21.3854 ridge:1:4")]
    [InlineData("lgable", 157.1188, 156.392, "eave:5:42.2 verge:2:8.8768 ridge:2:14.3 hip:3:16.773 valley:1:5.591")]
    [InlineData("lhip", 153.8176, 153.0996, "eave:6:50 hip:5:27.6161 ridge:2:11 valley:1:5.5232")]
    // The app reads a mansard's tiers from the IFC covering, where they do not touch (each is lifted by its own pitch): it
    // finds four more eaves and four top edges there. On the planes themselves the tiers share the line: four convex breaks.
    [InlineData("mansard", 147.0421, 147.0421, "eave:4:39.2 hip:8:30.3913 convexBreak:4:28 ridge:1:2")]
    public void The_edges_are_classified_as_the_app_does(string roof, double area, double coverage, string edges)
    {
        var (f, id) = Make(roof);
        var r = Layout(f, id, "bilka-balcanic");
        Assert.Equal(area, (double)r["totals"]!["roofArea"]!, 3);
        Assert.Equal(coverage, (double)r["totals"]!["coverage"]!, 3);
        var got = r["edges"]!.AsArray().GroupBy(e => (string)e!["kind"]!)
            .Select(g => $"{g.Key}:{g.Count()}:{Math.Round(g.Sum(e => (double)e!["length"]!), 4)}");
        Assert.Equal(edges, string.Join(" ", got));
    }

    [Fact]
    public void Hip_offcuts_go_to_the_valley_pieces_the_app_cuts_from_them()
    {
        var (f, id) = Make("lhip");
        var r = Layout(f, id, "bilka-balcanic");
        var reuse = r["panels"]!.AsArray().Where(p => p!["donor"] != null).Select(p => $"{p!["id"]}<{p["donor"]}@{p["shift"]}").Order();
        Assert.Equal("A03-07-01<A03-02-01@700 A03-09-01<A01-02-01@350 A04-01-01<A01-01-01@700 A04-02-01<A02-06-01@1050 A04-04-01<A05-07-01@0", string.Join(" ", reuse));
        // A donor says what it gives; the offcut is not bought.
        var panels = r["panels"]!.AsArray().ToDictionary(p => (string)p!["id"]!);
        Assert.Equal("A04-01-01", (string?)panels["A01-01-01"]!["donatesTo"]);
        Assert.Equal(0, (int)panels["A04-01-01"]!["buy"]!);
        Assert.Contains("valley", panels["A04-01-01"]!["edges"]!.AsArray().Select(x => (string)x!));
        Assert.Contains("hip", panels["A01-01-01"]!["edges"]!.AsArray().Select(x => (string)x!));
        // The sheet bought: each panel bought at its nominal length by the whole width.
        var sheet = r["purchase"]!.AsObject().Sum(kv => int.Parse(kv.Key) * (int)kv.Value! * 1180 / 1e6);
        Assert.Equal(sheet, (double)r["totals"]!["sheetArea"]!, 3);
    }

    [Fact]
    public void The_profiles_are_data_and_a_made_to_order_profile_lists_every_length()
    {
        Assert.Equal(["bilka-balcanic", "bilka-clasic-35", "bilka-retro-panel"], PanelProfiles.BuiltIn.Select(p => p.Id));
        var balcanic = PanelProfiles.Find("bilka-balcanic")!;
        Assert.Equal([1180, 2230, 2930], balcanic.Lengths);
        Assert.False(balcanic.OnOrder);
        var clasic = PanelProfiles.Find("bilka-clasic-35")!;
        // 500, 850 … 6800 on the 350 mm module, and 7000 to end a column.
        Assert.Equal(20, clasic.Lengths.Count);
        Assert.Equal([500, 850, 1200], clasic.Lengths.Take(3));
        Assert.Equal([6800, 7000], clasic.Lengths.TakeLast(2));
        Assert.Equal(["7016", "8017"], clasic.Colors.Select(c => c.Code));
        var retro = PanelProfiles.Find("bilka-retro-panel")!;
        Assert.Equal(69, retro.Lengths.Count);
        Assert.Equal([470, 500, 600], retro.Lengths.Take(3));
        Assert.Equal([7100, 7130], retro.Lengths.TakeLast(2));
        Assert.Equal(0.505, retro.EffectiveLength(530), 9);
        var capped = retro.WithMaxLength(4000);
        Assert.Equal([3800, 3900, 4000], capped.Lengths.TakeLast(3));
        Assert.Throws<ArgumentException>(() => retro.WithMaxLength(5000));
        Assert.Throws<ArgumentException>(() => balcanic.WithMaxLength(4000));
        // A valley flashing laps 20 cm under standing seams, 15 cm under tiles.
        Assert.Equal(0.2, retro.AccessoryLap(EdgeKinds.Valley), 9);
        Assert.Equal(0.15, balcanic.AccessoryLap(EdgeKinds.Valley), 9);
    }

    [Fact]
    public void A_profile_of_ones_own_lays_out_as_the_built_in_ones()
    {
        var (f, id) = Make("gable");
        // A tile of the user's: 1100 mm useful, a 300 mm module, stock 3100 and 1300 (3100 − 100 = 10 modules).
        var own = new JsonObject
        {
            ["id"] = "my-tile", ["name"] = "My tile", ["grossWidth"] = 1150, ["usefulWidth"] = 1100, ["slopeLap"] = 100, ["module"] = 300,
            ["lengths"] = new JsonArray(1300, 3100), ["reuseOffcuts"] = true,
        };
        var r = (JsonObject)f.Session.Query("roofpanels.layout", new JsonObject { ["id"] = id, ["profile"] = own.DeepClone() })!;
        Assert.Null(r["problem"]);
        // 4.04 m of slope: 3100 + 1300 lapped 100 (4.3 m) on the module, ten columns of 1.1 m a 10.6 m slope.
        Assert.Equal("1300×20 3100×20", Counts(r["purchase"]));
        // A profile that is not one is refused, named.
        own["usefulWidth"] = 0;
        var bad = (JsonObject)f.Session.Query("roofpanels.layout", new JsonObject { ["id"] = id, ["profile"] = own.DeepClone() })!;
        Assert.Contains("useful width", (string)bad["problem"]!);
    }

    [Fact]
    public void A_roof_names_its_profile_in_one_undo_step_and_its_layout_follows_the_roof()
    {
        var (f, id) = Make("gable");
        Assert.Equal("The roof names no panel profile.", (string)Layout(f, id)["problem"]!);
        f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = "bilka-clasic-35", ["color"] = "7016" });
        var stored = f.Model.Get(id)!.Attrs[RoofPanels.RoofPanels.Key]!.AsObject();
        Assert.Equal("bilka-clasic-35", (string)stored["profile"]!);
        Assert.Equal("7016", (string)stored["color"]!);
        // Nothing of the layout is stored: only what the roof is covered with.
        Assert.Equal(2, stored.Count);
        var r = Layout(f, id);
        Assert.Equal("7016", (string)r["color"]!);
        Assert.Equal("4350×20", Counts(r["purchase"]));
        // The roof made steeper: the panels follow (5 m slopes of 45°, one 5300 mm panel a column).
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["pitch"] = 45 } });
        var steeper = Layout(f, id);
        Assert.NotEqual("4350×20", Counts(steeper["purchase"]));
        Assert.Single(steeper["purchase"]!.AsObject());
        // A colour the profile is not made in, an unknown profile: refused.
        Assert.Throws<CommandException>(() => f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = "bilka-clasic-35", ["color"] = "9005" }));
        Assert.Throws<CommandException>(() => f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = "nope" }));
        Assert.Throws<CommandException>(() => f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = "bilka-retro-panel", ["maxLength"] = 5000 }));
        // Undone, the pitch is back; undone again, the roof names no profile.
        f.Session.Undo();
        Assert.Equal("4350×20", Counts(Layout(f, id)["purchase"]));
        f.Session.Undo();
        Assert.Null(f.Model.Get(id)!.Attrs[RoofPanels.RoofPanels.Key]);
        // Cleared with a null profile.
        f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = "bilka-retro-panel", ["maxLength"] = 4000 });
        Assert.Equal(4000, (int)f.Model.Get(id)!.Attrs[RoofPanels.RoofPanels.Key]!["maxLength"]!);
        f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = null });
        Assert.Null(f.Model.Get(id)!.Attrs[RoofPanels.RoofPanels.Key]);
    }

    [Fact]
    public void A_slope_under_the_profiles_least_pitch_is_warned_and_a_flat_roof_refused()
    {
        var f = new Fixture(new ArchitectureModule(), new RoofPanelsModule());
        var id = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts(House) })!["id"]!;
        // 12° hips: under the 14° tiles ask where a hip or valley cuts them, over Retro Panel's 8°.
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["shape"] = "hip", ["pitch"] = 12, ["overhang"] = 0.5 } });
        var tiles = Layout(f, id, "bilka-balcanic");
        Assert.Equal(4, tiles["warnings"]!.AsArray().Count(w => ((string)w!).Contains("least pitch of 14°")));
        Assert.Empty(Layout(f, id, "bilka-retro-panel")["warnings"]!.AsArray());
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["shape"] = "flat" } });
        Assert.Equal("A flat roof takes no panels.", (string)Layout(f, id, "bilka-balcanic")["problem"]!);
    }

    [Fact]
    public void Accessories_are_counted_per_edge_and_the_panels_are_parts_schedules_list()
    {
        // accessories.piece_count: 2 m flashings lapped 10 cm along a 10.6 m ridge: 6, five joints.
        Assert.Equal(6, RoofPanels.RoofPanels.PieceCount(10.6, 2, 0.1));
        Assert.Equal(1, RoofPanels.RoofPanels.PieceCount(1.5, 2, 0.1));
        Assert.Equal(0, RoofPanels.RoofPanels.PieceCount(0, 2, 0.1));
        var (f, id) = Make("gable");
        var ridge = Layout(f, id, "bilka-balcanic")["edges"]!.AsArray().Single(e => (string)e!["kind"]! == "ridge")!;
        Assert.Equal(6, (int)ridge["pieces"]!);
        Assert.Equal(5, (int)ridge["joints"]!);
        var sources = f.Session.Query("schedule.partSources", new JsonObject())!.AsArray();
        var panels = sources.Single(s => (string)s!["source"]! == "roofPanels")!;
        Assert.Contains("panelLength", panels["fields"]!.AsArray().Select(x => (string)x!["key"]!));
        Assert.Equal(3, f.Session.Catalog.Catalogs["roofpanels.profiles"]!.AsArray().Count);
    }
}
