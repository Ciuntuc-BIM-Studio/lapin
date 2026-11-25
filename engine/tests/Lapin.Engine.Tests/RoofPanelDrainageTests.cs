using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;
using Lapin.RoofPanels;

namespace Lapin.Engine.Tests;

/// <summary>
/// The roof panels' rainwater system and flat sheet (docs/plan-roof-panels.md §2.4, stage S5). The numbers are the Panotaj Bilka
/// app's (v1.3.3, the owner's Python app): the reference roofs of <see cref="RoofPanelTakeoffTests"/> (and a hip on a house with a
/// corner cut at 45°) exported to IFC by Lapin on 25.11.2026 and run through its <c>analyze(…)</c> (a copy, outside its folder)
/// with <c>--drainage-system</c>, <c>--downpipes</c>, <c>--rain</c> and <c>--ridge</c> where named: its <c>estimate.drainage</c>
/// (pluvial.sizing: the system recommended and used, each slope's least downpipes, the count, below the least or not, a downpipe's
/// average length and clamps), its "Pluvial" rows and its <c>flat_stock_options</c> (flat_stock.nest: the sheets each combination
/// of ridge cap and system takes, each piece's place).
/// </summary>
public class RoofPanelDrainageTests
{
    static JsonObject Layout(Fixture f, string roof, JsonObject? extra = null)
    {
        var q = new JsonObject { ["id"] = roof };
        if (extra != null) foreach (var (k, v) in extra) q[k] = v?.DeepClone();
        return (JsonObject)f.Session.Query("roofpanels.layout", q)!;
    }

    static JsonObject Set(string profile, string? ridge = null, string? system = null, int? downpipes = null, double? rain = null)
    {
        var a = new JsonObject { ["profile"] = profile };
        if (ridge != null) a["ridge"] = ridge;
        if (system != null) a["drainage"] = system;
        if (downpipes != null) a["downpipes"] = downpipes;
        if (rain != null) a["rain"] = rain;
        return a;
    }

    static void Run(Fixture f, string id, JsonObject a)
    {
        var args = (JsonObject)a.DeepClone();
        args["id"] = id;
        f.Run("roofpanels.setRoofPanels", args);
    }

    static string N(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

    [Theory]
    // roof, profile, ridge, system, downpipes, rain; the app's recommended and used system, least downpipes (125/90 / 150/100), the
    // count, below the least, each slope (eave m, area m² as the app rounds them, least 125/90 / 150/100), a downpipe's average length (m), clamps a
    // downpipe, the "Pluvial" rows bought
    [InlineData("gable", "bilka-balcanic", null, null, null, null, "125/90", "125/90", "4/4", 4, false, "A01:10.6:42.83939:2/2 A02:10.6:42.83939:2/2", 3.1304, 3, "gutter-125:6 gutter-hook-125:38 gutter-join-125:4 gutter-cap-125:4 outlet-125:4 downpipe-90:5 elbow-90:8 downpipe-extension-90:4 pipe-clamp-90:12 pipe-exit-90:4 pipe-anchors:3")]
    [InlineData("gable", "bilka-clasic-35", null, null, null, null, "125/90", "125/90", "4/4", 4, false, "A01:10.6:42.83939:2/2 A02:10.6:42.83939:2/2", 3.1304, 3, "gutter-125:6 gutter-hook-125:38 gutter-join-125:4 gutter-cap-125:4 outlet-125:4 downpipe-90:5 elbow-90:8 downpipe-extension-90:4 pipe-clamp-90:12 pipe-exit-90:4 pipe-anchors:3")]
    [InlineData("gable", "bilka-clasic-35", "small", null, null, null, "125/90", "125/90", "4/4", 4, false, "A01:10.6:42.83939:2/2 A02:10.6:42.83939:2/2", 3.1304, 3, "gutter-125:6 gutter-hook-125:38 gutter-join-125:4 gutter-cap-125:4 outlet-125:4 downpipe-90:5 elbow-90:8 downpipe-extension-90:4 pipe-clamp-90:12 pipe-exit-90:4 pipe-anchors:3")]
    [InlineData("gable", "bilka-retro-panel", null, null, null, null, "125/90", "125/90", "4/4", 4, false, "A01:10.6:42.83939:2/2 A02:10.6:42.83939:2/2", 3.1304, 3, "gutter-125:6 gutter-hook-125:38 gutter-join-125:4 gutter-cap-125:4 outlet-125:4 downpipe-90:5 elbow-90:8 downpipe-extension-90:4 pipe-clamp-90:12 pipe-exit-90:4 pipe-anchors:3")]
    [InlineData("lhip", "bilka-balcanic", null, null, null, null, "150/100", "150/100", "8/8", 8, false, "A01:13:40.59075:2/2 A02:7:14.95449:1/1 A03:6:25.63627:1/1 A04:5:21.36355:1/1 A05:7:14.95449:1/1 A06:12:36.31804:2/2", 3.069, 3, "gutter-150:15 gutter-hook-150:90 gutter-outer-150:5 gutter-inner-150:1 gutter-join-150:21 outlet-150:8 downpipe-100:9 elbow-100:16 downpipe-extension-100:8 pipe-clamp-100:24 pipe-exit-100:8 pipe-anchors:6 sealant:2")]
    [InlineData("lhip", "bilka-clasic-35", null, null, null, null, "150/100", "150/100", "8/8", 8, false, "A01:13:40.59075:2/2 A02:7:14.95449:1/1 A03:6:25.63627:1/1 A04:5:21.36355:1/1 A05:7:14.95449:1/1 A06:12:36.31804:2/2", 3.069, 3, "gutter-150:15 gutter-hook-150:90 gutter-outer-150:5 gutter-inner-150:1 gutter-join-150:21 outlet-150:8 downpipe-100:9 elbow-100:16 downpipe-extension-100:8 pipe-clamp-100:24 pipe-exit-100:8 pipe-anchors:6 sealant:2")]
    [InlineData("lhip", "bilka-retro-panel", null, null, null, null, "150/100", "150/100", "8/8", 8, false, "A01:13:40.59075:2/2 A02:7:14.95449:1/1 A03:6:25.63627:1/1 A04:5:21.36355:1/1 A05:7:14.95449:1/1 A06:12:36.31804:2/2", 3.069, 3, "gutter-150:15 gutter-hook-150:90 gutter-outer-150:5 gutter-inner-150:1 gutter-join-150:21 outlet-150:8 downpipe-100:9 elbow-100:16 downpipe-extension-100:8 pipe-clamp-100:24 pipe-exit-100:8 pipe-anchors:6 sealant:2")]
    [InlineData("lhip", "bilka-clasic-35", "small", null, null, null, "150/100", "150/100", "8/8", 8, false, "A01:13:40.59075:2/2 A02:7:14.95449:1/1 A03:6:25.63627:1/1 A04:5:21.36355:1/1 A05:7:14.95449:1/1 A06:12:36.31804:2/2", 3.069, 3, "gutter-150:15 gutter-hook-150:90 gutter-outer-150:5 gutter-inner-150:1 gutter-join-150:21 outlet-150:8 downpipe-100:9 elbow-100:16 downpipe-extension-100:8 pipe-clamp-100:24 pipe-exit-100:8 pipe-anchors:6 sealant:2")]
    [InlineData("wallgable", "bilka-balcanic", null, null, null, null, "125/90", "125/90", "4/2", 4, false, "A01:10:40.41452:2/1 A02:10:40.41452:2/1", 3.1304, 3, "gutter-125:6 gutter-hook-125:36 gutter-join-125:4 gutter-cap-125:4 outlet-125:4 downpipe-90:5 elbow-90:8 downpipe-extension-90:4 pipe-clamp-90:12 pipe-exit-90:4 pipe-anchors:3")]
    [InlineData("wallgable", "bilka-clasic-35", null, null, null, null, "125/90", "125/90", "4/2", 4, false, "A01:10:40.41452:2/1 A02:10:40.41452:2/1", 3.1304, 3, "gutter-125:6 gutter-hook-125:36 gutter-join-125:4 gutter-cap-125:4 outlet-125:4 downpipe-90:5 elbow-90:8 downpipe-extension-90:4 pipe-clamp-90:12 pipe-exit-90:4 pipe-anchors:3")]
    [InlineData("wallgable", "bilka-retro-panel", null, null, null, null, "125/90", "125/90", "4/2", 4, false, "A01:10:40.41452:2/1 A02:10:40.41452:2/1", 3.1304, 3, "gutter-125:6 gutter-hook-125:36 gutter-join-125:4 gutter-cap-125:4 outlet-125:4 downpipe-90:5 elbow-90:8 downpipe-extension-90:4 pipe-clamp-90:12 pipe-exit-90:4 pipe-anchors:3")]
    [InlineData("leanto", "bilka-balcanic", null, null, null, null, "125/90", "125/90", "3/2", 3, false, "A01:10.6:73.52532:3/2", 3.2705, 3, "gutter-125:3 gutter-hook-125:19 gutter-join-125:2 gutter-cap-125:2 outlet-125:3 downpipe-90:4 elbow-90:6 downpipe-extension-90:3 pipe-clamp-90:9 pipe-exit-90:3 pipe-anchors:3")]
    [InlineData("leanto", "bilka-clasic-35", null, null, null, null, "125/90", "125/90", "3/2", 3, false, "A01:10.6:73.52532:3/2", 3.2705, 3, "gutter-125:3 gutter-hook-125:19 gutter-join-125:2 gutter-cap-125:2 outlet-125:3 downpipe-90:4 elbow-90:6 downpipe-extension-90:3 pipe-clamp-90:9 pipe-exit-90:3 pipe-anchors:3")]
    [InlineData("leanto", "bilka-retro-panel", null, null, null, null, "125/90", "125/90", "3/2", 3, false, "A01:10.6:73.52532:3/2", 3.2705, 3, "gutter-125:3 gutter-hook-125:19 gutter-join-125:2 gutter-cap-125:2 outlet-125:3 downpipe-90:4 elbow-90:6 downpipe-extension-90:3 pipe-clamp-90:9 pipe-exit-90:3 pipe-anchors:3")]
    [InlineData("gable", "bilka-balcanic", null, "150/100", null, null, "125/90", "150/100", "4/4", 4, false, "A01:10.6:42.83939:2/2 A02:10.6:42.83939:2/2", 3.1304, 3, "gutter-150:6 gutter-hook-150:38 gutter-join-150:4 gutter-cap-150:4 outlet-150:4 downpipe-100:5 elbow-100:8 downpipe-extension-100:4 pipe-clamp-100:12 pipe-exit-100:4 pipe-anchors:3")]
    [InlineData("gable", "bilka-balcanic", null, null, 2, null, "125/90", "125/90", "4/4", 2, true, "A01:10.6:42.83939:2/2 A02:10.6:42.83939:2/2", 3.1304, 3, "gutter-125:6 gutter-hook-125:38 gutter-join-125:4 gutter-cap-125:4 outlet-125:2 downpipe-90:3 elbow-90:4 downpipe-extension-90:2 pipe-clamp-90:6 pipe-exit-90:2 pipe-anchors:2")]
    [InlineData("gable", "bilka-balcanic", null, "150/100", 6, null, "125/90", "150/100", "4/4", 6, false, "A01:10.6:42.83939:2/2 A02:10.6:42.83939:2/2", 3.1304, 3, "gutter-150:6 gutter-hook-150:38 gutter-join-150:4 gutter-cap-150:4 outlet-150:6 downpipe-100:7 elbow-100:12 downpipe-extension-100:6 pipe-clamp-100:18 pipe-exit-100:6 pipe-anchors:5")]
    [InlineData("gable", "bilka-balcanic", null, null, null, 0.08, "150/100", "150/100", "6/4", 4, false, "A01:10.6:42.83939:3/2 A02:10.6:42.83939:3/2", 3.1304, 3, "gutter-150:6 gutter-hook-150:38 gutter-join-150:4 gutter-cap-150:4 outlet-150:4 downpipe-100:5 elbow-100:8 downpipe-extension-100:4 pipe-clamp-100:12 pipe-exit-100:4 pipe-anchors:3")]
    [InlineData("lhip", "bilka-clasic-35", null, "125/90", null, null, "150/100", "125/90", "8/8", 8, false, "A01:13:40.59075:2/2 A02:7:14.95449:1/1 A03:6:25.63627:1/1 A04:5:21.36355:1/1 A05:7:14.95449:1/1 A06:12:36.31804:2/2", 3.069, 3, "gutter-125:15 gutter-hook-125:90 gutter-outer-125:5 gutter-inner-125:1 gutter-join-125:21 outlet-125:8 downpipe-90:9 elbow-90:16 downpipe-extension-90:8 pipe-clamp-90:24 pipe-exit-90:8 pipe-anchors:6 sealant:2")]
    [InlineData("lhip", "bilka-clasic-35", null, null, 10, null, "150/100", "150/100", "8/8", 10, false, "A01:13:40.59075:2/2 A02:7:14.95449:1/1 A03:6:25.63627:1/1 A04:5:21.36355:1/1 A05:7:14.95449:1/1 A06:12:36.31804:2/2", 3.069, 3, "gutter-150:15 gutter-hook-150:90 gutter-outer-150:5 gutter-inner-150:1 gutter-join-150:21 outlet-150:10 downpipe-100:11 elbow-100:20 downpipe-extension-100:10 pipe-clamp-100:30 pipe-exit-100:10 pipe-anchors:8 sealant:2")]
    [InlineData("lhip", "bilka-retro-panel", null, "125/90", 3, null, "150/100", "125/90", "8/8", 3, true, "A01:13:40.59075:2/2 A02:7:14.95449:1/1 A03:6:25.63627:1/1 A04:5:21.36355:1/1 A05:7:14.95449:1/1 A06:12:36.31804:2/2", 3.069, 3, "gutter-125:15 gutter-hook-125:90 gutter-outer-125:5 gutter-inner-125:1 gutter-join-125:21 outlet-125:3 downpipe-90:4 elbow-90:6 downpipe-extension-90:3 pipe-clamp-90:9 pipe-exit-90:3 pipe-anchors:3 sealant:2")]
    [InlineData("lhip", "bilka-balcanic", "small", null, null, null, "150/100", "150/100", "8/8", 8, false, "A01:13:40.59075:2/2 A02:7:14.95449:1/1 A03:6:25.63627:1/1 A04:5:21.36355:1/1 A05:7:14.95449:1/1 A06:12:36.31804:2/2", 3.069, 3, "gutter-150:15 gutter-hook-150:90 gutter-outer-150:5 gutter-inner-150:1 gutter-join-150:21 outlet-150:8 downpipe-100:9 elbow-100:16 downpipe-extension-100:8 pipe-clamp-100:24 pipe-exit-100:8 pipe-anchors:6 sealant:2")]
    [InlineData("leanto", "bilka-retro-panel", null, "150/100", null, 0.03, "125/90", "150/100", "2/2", 2, false, "A01:10.6:73.52532:2/2", 3.2705, 3, "gutter-150:3 gutter-hook-150:19 gutter-join-150:2 gutter-cap-150:2 outlet-150:2 downpipe-100:3 elbow-100:4 downpipe-extension-100:2 pipe-clamp-100:6 pipe-exit-100:2 pipe-anchors:2")]
    [InlineData("chamfer", "bilka-balcanic", null, null, null, null, "125/90", "125/90", "6/6", 6, false, "A01:11:31.96337:2/2 A02:4.70711:9.0455:1/1 A03:4.65685:13.33562:1/1 A04:7.70711:22.53366:1/1 A05:8:18.47521:1/1", 3.1304, 3, "gutter-125:11 gutter-hook-125:66 gutter-outer-125:3 gutter-join-125:16 outlet-125:6 downpipe-90:7 elbow-90:12 downpipe-extension-90:6 pipe-clamp-90:18 pipe-exit-90:6 pipe-anchors:5 sealant:2")]
    [InlineData("chamfer", "bilka-clasic-35", null, null, null, null, "125/90", "125/90", "6/6", 6, false, "A01:11:31.96337:2/2 A02:4.70711:9.0455:1/1 A03:4.65685:13.33562:1/1 A04:7.70711:22.53366:1/1 A05:8:18.47521:1/1", 3.1304, 3, "gutter-125:11 gutter-hook-125:66 gutter-outer-125:3 gutter-join-125:16 outlet-125:6 downpipe-90:7 elbow-90:12 downpipe-extension-90:6 pipe-clamp-90:18 pipe-exit-90:6 pipe-anchors:5 sealant:2")]
    [InlineData("chamfer", "bilka-retro-panel", null, null, null, null, "125/90", "125/90", "6/6", 6, false, "A01:11:31.96337:2/2 A02:4.70711:9.0455:1/1 A03:4.65685:13.33562:1/1 A04:7.70711:22.53366:1/1 A05:8:18.47521:1/1", 3.1304, 3, "gutter-125:11 gutter-hook-125:66 gutter-outer-125:3 gutter-join-125:16 outlet-125:6 downpipe-90:7 elbow-90:12 downpipe-extension-90:6 pipe-clamp-90:18 pipe-exit-90:6 pipe-anchors:5 sealant:2")]
    [InlineData("chamfer", "bilka-clasic-35", "small", "150/100", null, null, "125/90", "150/100", "6/6", 6, false, "A01:11:31.96337:2/2 A02:4.70711:9.0455:1/1 A03:4.65685:13.33562:1/1 A04:7.70711:22.53366:1/1 A05:8:18.47521:1/1", 3.1304, 3, "gutter-150:11 gutter-hook-150:66 gutter-outer-150:3 gutter-join-150:16 outlet-150:6 downpipe-100:7 elbow-100:12 downpipe-extension-100:6 pipe-clamp-100:18 pipe-exit-100:6 pipe-anchors:5 sealant:2")]
    public void The_drainage_is_the_Panotaj_Bilka_apps_Pluvial_sheet(string roof, string profile, string? ridge, string? system, int? downpipes, double? rain,
        string recommended, string selected, string minimum, int count, bool below, string runs, double average, int clamps, string rows)
    {
        var (f, id) = RoofPanelTakeoffTests.Make(roof);
        Run(f, id, Set(profile, ridge, system, downpipes, rain));
        var d = Layout(f, id)["drainage"]!.AsObject();
        Assert.Equal(recommended, (string)d["recommended"]!);
        Assert.Equal(selected, (string)d["selected"]!);
        Assert.Equal(system ?? "auto", (string)d["selection"]!);
        Assert.Equal(minimum, $"{(int)d["minimum"]!["125/90"]!}/{(int)d["minimum"]!["150/100"]!}");
        Assert.Equal(count, (int)d["downpipes"]!);
        Assert.Equal(below, (bool)d["belowMinimum"]!);
        // Each slope, its eave and its least downpipes as the app's; its area within 0.1 cm² (the app read the IFC's coordinates, to 0.1 mm).
        var mine = d["runs"]!.AsArray().Select(r => (Text: $"{(string)r!["face"]!}:{N((double)r["eave"]!)}:{(int)r["minimum"]!["125/90"]!}/{(int)r["minimum"]!["150/100"]!}", Area: (double)r["area"]!)).ToList();
        var app = runs.Split(' ').Select(x => x.Split(':')).Select(x => (Text: $"{x[0]}:{x[1]}:{x[3]}", Area: double.Parse(x[2], CultureInfo.InvariantCulture))).ToList();
        Assert.Equal(app.Select(x => x.Text), mine.Select(x => x.Text));
        Assert.All(app.Zip(mine), x => Assert.Equal(x.First.Area, x.Second.Area, 4));
        Assert.Equal(0, (double)d["unrouted"]!);
        // The model's downpipes, as long as the app read them from the IFC; a system asked takes the roof's gutters to its size,
        // which moves their outlets by millimetres (a 150 mm gutter's outlet stands out and down 12.5 mm more).
        Assert.InRange((double)d["averagePipe"]!, average - 0.01, average + 0.0001);
        Assert.Equal(clamps, (int)d["clamps"]!);
        Assert.Equal(rows, string.Join(" ", d["items"]!.AsArray().Where(i => i!["madeFrom"] == null).Select(i => $"{(string)i!["code"]!}:{(double)i["quantity"]!}")));
    }

    [Theory]
    // roof, profile; the sheets of each combination of ridge cap and system (a standing seam has one ridge cap), the pieces of the roof's own
    [InlineData("gable", "bilka-balcanic", "large/125/90:0 large/150/100:0 small/125/90:0 small/150/100:0", "")]
    [InlineData("gable", "bilka-clasic-35", "large/125/90:0 large/150/100:0 small/125/90:0 small/150/100:0", "")]
    [InlineData("gable", "bilka-retro-panel", "125/90:1 150/100:1", "CF-01@1:10,10,180x457 CF-02@1:10,472,180x457")]
    [InlineData("lhip", "bilka-balcanic", "large/125/90:2 large/150/100:2 small/125/90:2 small/150/100:2", "NC-01@1:10,10,850x900 NC-02@1:865,10,900x850 CD-01@2:865,10,750x650 NC-03@2:10,10,850x900")]
    [InlineData("lhip", "bilka-clasic-35", "large/125/90:2 large/150/100:2 small/125/90:2 small/150/100:2", "NC-01@1:10,10,850x900 NC-02@1:865,10,900x850 CD-01@2:865,10,750x650 NC-03@2:10,10,850x900")]
    [InlineData("lhip", "bilka-retro-panel", "125/90:3 150/100:3", "NC-01@1:10,10,977x1027 NC-02@1:992,10,977x1027 AD-01@2:1907,10,70x1100 AD-02@2:10,1127,1100x70 AD-03@2:10,1042,220x70 CD-01@2:992,10,877x650 IC-01@2:992,665,300x457 IC-02@2:1297,665,300x457 IC-03@2:1602,665,300x457 NC-03@2:10,10,977x1027 IC-04@3:10,10,300x457 IC-05@3:10,472,300x457")]
    [InlineData("wallgable", "bilka-balcanic", "large/125/90:0 large/150/100:0 small/125/90:0 small/150/100:0", "")]
    [InlineData("wallgable", "bilka-clasic-35", "large/125/90:0 large/150/100:0 small/125/90:0 small/150/100:0", "")]
    [InlineData("wallgable", "bilka-retro-panel", "125/90:1 150/100:1", "CF-01@1:10,10,180x457")]
    [InlineData("leanto", "bilka-balcanic", "large/125/90:0 large/150/100:0 small/125/90:0 small/150/100:0", "")]
    [InlineData("leanto", "bilka-clasic-35", "large/125/90:0 large/150/100:0 small/125/90:0 small/150/100:0", "")]
    [InlineData("leanto", "bilka-retro-panel", "125/90:0 150/100:0", "")]
    [InlineData("chamfer", "bilka-balcanic", "large/125/90:2 large/150/100:2 small/125/90:2 small/150/100:2", "NC-01@1:10,10,850x900 NC-02@1:865,10,900x850 CJ-01@2:865,10,680x495 CJ-02@2:865,510,495x680 NC-03@2:10,10,850x900")]
    [InlineData("chamfer", "bilka-clasic-35", "large/125/90:2 large/150/100:2 small/125/90:2 small/150/100:2", "NC-01@1:10,10,850x900 NC-02@1:865,10,900x850 CJ-01@2:865,10,680x495 CJ-02@2:865,510,495x680 NC-03@2:10,10,850x900")]
    [InlineData("chamfer", "bilka-retro-panel", "125/90:3 150/100:3", "NC-01@1:10,10,977x1027 NC-02@1:992,10,977x1027 CJ-01@2:992,10,680x495 CJ-02@2:992,510,495x680 IC-01@2:1677,10,300x457 IC-02@2:1677,472,300x457 IC-03@2:1492,934,457x300 NC-03@2:10,10,977x1027 IC-04@3:10,10,300x457 IC-05@3:10,472,300x457")]
    public void The_flat_sheets_are_the_apps_Debitare_tabla(string roof, string profile, string combinations, string pieces)
    {
        var (f, id) = RoofPanelTakeoffTests.Make(roof);
        Run(f, id, Set(profile));
        var s = Layout(f, id)["flatSheet"]!.AsObject();
        Assert.Equal((2000, 1250, 10, 5), ((int)s["width"]!, (int)s["height"]!, (int)s["margin"]!, (int)s["gap"]!));
        Assert.Equal(combinations, string.Join(" ", s["combinations"]!.AsArray().Select(c => (string?)c!["ridge"] is { } r ? $"{r}/{(string)c["system"]!}:{(int)c["sheets"]!}" : $"{(string)c["system"]!}:{(int)c["sheets"]!}")));
        Assert.Equal(pieces, string.Join(" ", s["pieces"]!.AsArray().Select(p => $"{(string)p!["id"]!}@{(int)p["sheet"]!}:{(int)p["x"]!},{(int)p["y"]!},{(int)p["width"]!}x{(int)p["height"]!}")));
        // Every piece whole on its sheet, inside the margin, none over another.
        var laid = s["pieces"]!.AsArray().Select(p => (S: (int)p!["sheet"]!, X: (int)p["x"]!, Y: (int)p["y"]!, W: (int)p["width"]!, H: (int)p["height"]!)).ToList();
        Assert.All(laid, p => Assert.True(p.X >= 10 && p.Y >= 10 && p.X + p.W <= 1990 && p.Y + p.H <= 1240));
        foreach (var a in laid)
            foreach (var b in laid)
                if (a != b && a.S == b.S) Assert.True(a.X + a.W + 5 <= b.X || b.X + b.W + 5 <= a.X || a.Y + a.H + 5 <= b.Y || b.Y + b.H + 5 <= a.Y);
        Assert.Empty(s["tooBig"]!.AsArray());
    }

    [Fact]
    public void Asking_for_a_system_sizes_the_roofs_own_gutters_and_downpipes_in_one_undo_step()
    {
        var (f, id) = RoofPanelTakeoffTests.Make("lhip");
        Run(f, id, Set("bilka-clasic-35"));
        // Only the profile: the gutters stay as drawn (125 mm), the sizing (Auto: 150/100 above 100 m²) says they differ.
        var roof = f.Model.Get(id)!;
        Assert.Null(roof.NumOrNull("gutterWidth"));
        var d = Layout(f, id)["drainage"]!.AsObject();
        Assert.Equal(("150/100", 125.0, 100.0), ((string)d["selected"]!, (double)d["model"]!["gutterWidth"]!, (double)d["model"]!["pipeDiameter"]!));
        Assert.Contains(d["warnings"]!.AsArray(), w => ((string)w!).StartsWith("The roof's gutters are 125 mm", StringComparison.Ordinal));
        // Auto asked: the gutters take 150 mm and the downpipes 100 mm, one step.
        Run(f, id, Set("bilka-clasic-35", system: "auto"));
        roof = f.Model.Get(id)!;
        Assert.Equal((0.15, 0.1), (roof.Num("gutterWidth"), roof.Num("downpipeDiameter")));
        Assert.DoesNotContain("drainage", ((JsonObject)roof.Attrs["panels"]!).Select(kv => kv.Key));
        d = Layout(f, id)["drainage"]!.AsObject();
        Assert.Equal(150.0, (double)d["model"]!["gutterWidth"]!);
        Assert.DoesNotContain(d["warnings"]!.AsArray(), w => ((string)w!).StartsWith("The roof's gutters", StringComparison.Ordinal));
        // 125/90 asked, 3 downpipes: stored, the gutters 125 mm and the downpipes 90 mm; under the least, said.
        Run(f, id, Set("bilka-clasic-35", system: "125/90", downpipes: 3));
        roof = f.Model.Get(id)!;
        Assert.Equal((0.125, 0.09), (roof.Num("gutterWidth"), roof.Num("downpipeDiameter")));
        Assert.Equal(("125/90", 3), ((string)roof.Attrs["panels"]!["drainage"]!, (int)roof.Attrs["panels"]!["downpipes"]!));
        d = Layout(f, id)["drainage"]!.AsObject();
        Assert.True((bool)d["belowMinimum"]!);
        Assert.Contains(d["warnings"]!.AsArray(), w => (string)w! == "3 downpipe(s) are fewer than the 8 the 125/90 system needs.");
        // The 3 shared out over the six slopes by their least (2, 1, 1, 1, 1, 2): the largest first.
        Assert.Equal(3, d["runs"]!.AsArray().Sum(r => (int)r!["planned"]!));
        // The roof's quantities name the downpipes' size.
        Assert.Contains(f.Session.Quantities(id).Single(q => q.Name == "Lapin_RoofDrainageQuantities").Quantities, q => q.Label == "Downpipe Ø90");
        // One undo step each.
        f.Session.Undo();
        Assert.Equal(0.15, f.Model.Get(id)!.Num("gutterWidth"));
        f.Session.Undo();
        Assert.Null(f.Model.Get(id)!.NumOrNull("gutterWidth"));
        // What a command refuses.
        Assert.Throws<CommandException>(() => Run(f, id, Set("bilka-clasic-35", system: "200/120")));
        Assert.Throws<CommandException>(() => Run(f, id, Set("bilka-clasic-35", downpipes: 0)));
        Assert.Throws<CommandException>(() => Run(f, id, Set("bilka-clasic-35", rain: -0.01)));
        // A preview takes a system without changing the roof.
        var preview = Layout(f, id, new JsonObject { ["profile"] = "bilka-clasic-35", ["drainage"] = "125/90" });
        Assert.Equal("125/90", (string)preview["drainage"]!["selected"]!);
        Assert.Contains(preview["cost"]!["lines"]!.AsArray(), l => (string)l!["code"]! == "gutter-125");
        Assert.Null(f.Model.Get(id)!.NumOrNull("gutterWidth"));
    }

    [Fact]
    public void A_slope_whose_eave_has_no_gutter_has_no_outlet_and_a_roof_without_gutters_counts_none()
    {
        var (f, id) = RoofPanelTakeoffTests.Make("gable");
        Run(f, id, Set("bilka-balcanic"));
        // The south eave's gutter off: its slope drains nowhere; one slope sized, the other said.
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = id, ["edges"] = new JsonArray(new JsonObject { ["gutter"] = false }, null, null, null) });
        var d = Layout(f, id)["drainage"]!.AsObject();
        Assert.Single(d["runs"]!.AsArray());
        Assert.Equal(42.8394, (double)d["unrouted"]!, 3);
        Assert.Single(d["noOutlet"]!.AsArray());
        Assert.Contains(d["warnings"]!.AsArray(), w => ((string)w!).Contains("drain to no gutter", StringComparison.Ordinal));
        Assert.Equal(3, d["items"]!.AsArray().Single(i => (string)i!["code"]! == "gutter-125")!["quantity"]!.GetValue<double>());
        // No gutters at all: no rainwater lines, said once.
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["gutterProfile"] = "none" } });
        d = Layout(f, id)["drainage"]!.AsObject();
        Assert.Empty(d["runs"]!.AsArray());
        Assert.Empty(d["items"]!.AsArray());
        Assert.False((bool)d["model"]!["gutters"]!);
        Assert.Contains(d["warnings"]!.AsArray(), w => (string)w! == "The roof has no gutters: no outlet drains it and no gutter is counted.");
    }

    [Fact]
    public void Downpipe_elements_are_the_models_downpipes_and_their_length_counts()
    {
        var (f, id) = RoofPanelTakeoffTests.Make("gable");
        Run(f, id, Set("bilka-balcanic"));
        var auto = Layout(f, id)["drainage"]!.AsObject();
        Assert.Equal((4, "auto"), ((int)auto["model"]!["downpipes"]!, (string)auto["model"]!["mode"]!));
        // Made elements where they stand: the same four, as long, each slope's two.
        f.Run("architecture.makeDownpipes", new JsonObject { ["id"] = id });
        var d = Layout(f, id)["drainage"]!.AsObject();
        Assert.Equal((4, "elements"), ((int)d["model"]!["downpipes"]!, (string)d["model"]!["mode"]!));
        Assert.All(d["runs"]!.AsArray(), r => Assert.Equal(2, (int)r!["model"]!));
        Assert.Equal((double)auto["averagePipe"]!, (double)d["averagePipe"]!, 2);
        // One taken away: the model has fewer than the estimate counts, said.
        var pipe = f.Model.OfClass("LapinDownpipe").First().Id;
        f.Session.Edit("delete", e => e.Remove(pipe));
        d = Layout(f, id)["drainage"]!.AsObject();
        Assert.Equal(3, (int)d["model"]!["downpipes"]!);
        Assert.Contains(d["warnings"]!.AsArray(), w => (string)w! == "The model has 3 downpipe(s); the estimate counts 4.");
    }

    [Fact]
    public void The_flat_sheet_is_bought_by_the_sheet_and_follows_the_ridge_cap_and_the_system()
    {
        var (f, id) = RoofPanelTakeoffTests.Make("chamfer");
        Run(f, id, Set("bilka-clasic-35"));
        var l = Layout(f, id);
        var sheet = l["flatSheet"]!.AsObject();
        Assert.Equal(("flat-sheet-8017", "large", "125/90", 2), ((string)sheet["code"]!, (string)sheet["ridge"]!, (string)sheet["system"]!, (int)sheet["sheets"]!));
        // Two 135° gutter corners: cut from flat sheet, each a blank 4 × 125 + 180 by 3 × 125 + 120 mm.
        Assert.Equal(2, sheet["pieces"]!.AsArray().Count(p => ((string)p!["id"]!).StartsWith("CJ-", StringComparison.Ordinal)));
        Assert.Contains(sheet["pieces"]!.AsArray(), p => (string)p!["name"]! == "Gutter corner Ø125 at 135°" && (int)p["blankWidth"]! == 680 && (int)p["blankHeight"]! == 495);
        var special = l["drainage"]!["items"]!.AsArray().Single(i => (string)i!["code"]! == "gutter-special-125")!;
        Assert.Equal(("flatSheet", 2.0), ((string)special["madeFrom"]!, (double)special["quantity"]!));
        // The sheet line bought; the odd corners not.
        var lines = l["cost"]!["lines"]!.AsArray();
        Assert.Equal(2.0, (double)lines.Single(x => (string)x!["code"]! == "flat-sheet-8017")!["quantity"]!);
        Assert.Contains(lines, x => (string)x!["code"]! == "gutter-special-125" && (string?)x["madeFrom"] == "flatSheet");
        var set = f.Session.Quantities(id).Single(s => s.Name == PanelCost.SetName).Quantities.ToDictionary(q => q.Name);
        Assert.Equal(2, set["flat-sheet-8017"].Value);
        Assert.DoesNotContain("gutter-special-125", set.Keys);
        Assert.Equal(11, set["gutter-125"].Value);
        // The small ridge cap and the larger system: the roof's own combination follows, the gutter corners grow.
        Run(f, id, Set("bilka-clasic-35", ridge: "small", system: "150/100"));
        sheet = Layout(f, id)["flatSheet"]!.AsObject();
        Assert.Equal(("small", "150/100"), ((string)sheet["ridge"]!, (string)sheet["system"]!));
        Assert.Contains(sheet["pieces"]!.AsArray(), p => (string)p!["id"]! == "CJ-01" && (int)p["blankWidth"]! == 780 && (int)p["blankHeight"]! == 570);
        Assert.Contains(sheet["pieces"]!.AsArray(), p => (string)p!["id"]! == "NC-01" && (int)p["blankWidth"]! == 840 && (int)p["blankHeight"]! == 790);
    }

    [Fact]
    public void Nesting_lays_each_piece_whole_and_says_what_no_sheet_holds()
    {
        var d = PanelProfiles.Sheet;
        // Two 1200 × 1000 pieces cannot share a 2000 × 1250 sheet (margins and the gap): two sheets.
        var n = FlatStock.Nest([new("A-01", "A", 1200, 1000), new("A-02", "A", 1200, 1000)], d);
        Assert.Equal(2, n.Sheets);
        // Turned to fit: 1100 × 1900 lies 1900 along the sheet.
        n = FlatStock.Nest([new("B-01", "B", 1100, 1900)], d);
        Assert.Equal((1, 1900, 1100), (n.Sheets, n.Pieces[0].W, n.Pieces[0].H));
        // Larger than a sheet: left out, said.
        n = FlatStock.Nest([new("C-01", "C", 2500, 300), new("D-01", "D", 300, 300)], d);
        Assert.Equal((1, "C-01"), (n.Sheets, n.TooBig.Single().Id));
        // The downpipes shared out: the least each when that is the number, else by the least, one each while there are enough.
        Assert.Equal([2, 1, 1], Rainwater.Share(4, [2, 1, 1], true));
        Assert.Equal([3, 2, 1], Rainwater.Share(6, [2, 1, 1], false));
        Assert.Equal([1, 1, 1], Rainwater.Share(3, [4, 1, 1], false));
        Assert.Equal([1, 0, 0], Rainwater.Share(1, [2, 1, 1], false));
    }
}
