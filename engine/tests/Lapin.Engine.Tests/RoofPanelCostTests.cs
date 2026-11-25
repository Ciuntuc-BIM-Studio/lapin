using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Cost;
using Lapin.Model;
using Lapin.Platform;
using Lapin.RoofPanels;

namespace Lapin.Engine.Tests;

/// <summary>
/// The roof panels' cost link (docs/plan-roof-panels.md §2.7, stage S4). The numbers are the Panotaj Bilka app's (v1.3.3, the
/// owner's Python app): the reference roofs of <see cref="RoofPanelTakeoffTests"/> exported to IFC by Lapin on 25.11.2026 and run
/// through its <c>analyze(…)</c> (a copy, outside its folder) with its own catalogue (prices read on 24.11.2026); each line is a
/// row of its "Deviz RON" sheet (the amount: the quantity by the unit price, ROUND_HALF_UP to the ban), the subtotal its whole
/// <c>priced_subtotal_ron</c> since stage S5 (the flat sheets and the rainwater system with the rest; the roofs re-exported on
/// 25.11.2026 with the hip on a cut corner, run with <c>--drainage-system</c>, <c>--downpipes</c> and <c>--rain</c> where named).
/// </summary>
public class RoofPanelCostTests
{
    const string Template = PanelCost.BilkaTemplate.TemplateId;

    static (Fixture F, string Roof) Make(string roof) => RoofPanelTakeoffTests.Make(roof, new CostModule());

    static JsonObject Layout(Fixture f, string roof) => (JsonObject)f.Session.Query("roofpanels.layout", new JsonObject { ["id"] = roof })!;

    static void Panels(Fixture f, string roof, string profile, string? color, string? ridge = null, string? fixing = null, string? support = null,
        string? system = null, int? downpipes = null, double? rain = null)
    {
        var a = new JsonObject { ["id"] = roof, ["profile"] = profile };
        if (system != null) a["drainage"] = system;
        if (downpipes != null) a["downpipes"] = downpipes;
        if (rain != null) a["rain"] = rain;
        if (color != null) a["color"] = color;
        if (ridge != null) a["ridge"] = ridge;
        if (fixing != null) a["fixing"] = fixing;
        if (support != null) a["support"] = support;
        f.Run("roofpanels.setRoofPanels", a);
    }

    static string Amount(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>The estimate's Roof lines as "code#variant:amount", by key.</summary>
    static (string Lines, double Sum) RoofLines(JsonObject estimate)
    {
        var roof = estimate["chapters"]!.AsArray().Single(c => (string)c!["name"]! == "Roof")!;
        var lines = roof["lines"]!.AsArray().Select(l =>
        {
            var parts = ((string)l!["key"]!).Split('\u001f');
            var key = parts.Length > 2 ? $"{(string)l["code"]!}#{parts[2]}" : (string)l["code"]!;
            return (Key: key, Value: (double)l["value"]!);
        }).OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
        return (string.Join(" ", lines.Select(x => $"{x.Key}:{Amount(x.Value)}")), lines.Sum(x => x.Value));
    }

    [Theory]
    // roof, profile, colour, ridge, fixing, support, rainwater system, downpipes, rain (null: Auto, Auto, 0.05), the app's priced_subtotal_ron, its lines
    [InlineData("gable", "bilka-balcanic", "8017", null, null, null, null, null, null, "12931.17", "batten:1577.70 bilka-balcanic/8017/2230:4759.68 counter-batten:701.20 downpipe-90:351.40 downpipe-extension-90:97.28 eave:492.96 elbow-90:155.92 gable-cap:83.10 gutter-125:442.74 gutter-cap-125:33.12 gutter-hook-125:467.78 gutter-join-125:45.72 membrane:758.00 membrane-tape:232.00 outlet-125:77.96 pipe-anchors:66.66 pipe-clamp-90:89.40 pipe-exit-90:92.16 ridge-large:478.38 ridge-roll-large:287.07 screw-long:309.72 screw-short:103.24 snow-guard:492.96 verge:662.52 wood-nails:72.50")]
    [InlineData("gable", "bilka-clasic-35", "7016", null, null, null, null, null, null, "13523.50", "batten:1577.70 bilka-clasic-35/7016#4350 mm:5300.39 counter-batten:701.20 downpipe-90:351.40 downpipe-extension-90:97.28 eave:492.96 elbow-90:155.92 gable-cap:83.10 gutter-125:442.74 gutter-cap-125:33.12 gutter-hook-125:467.78 gutter-join-125:45.72 membrane:758.00 membrane-tape:232.00 outlet-125:77.96 pipe-anchors:66.66 pipe-clamp-90:89.40 pipe-exit-90:92.16 ridge-large:478.38 ridge-roll-large:287.07 screw-long:361.34 screw-short:103.24 snow-guard:492.96 verge:662.52 wood-nails:72.50")]
    [InlineData("gable", "bilka-clasic-35", "8017", null, null, null, null, null, null, "13523.50", "batten:1577.70 bilka-clasic-35/8017#4350 mm:5300.39 counter-batten:701.20 downpipe-90:351.40 downpipe-extension-90:97.28 eave:492.96 elbow-90:155.92 gable-cap:83.10 gutter-125:442.74 gutter-cap-125:33.12 gutter-hook-125:467.78 gutter-join-125:45.72 membrane:758.00 membrane-tape:232.00 outlet-125:77.96 pipe-anchors:66.66 pipe-clamp-90:89.40 pipe-exit-90:92.16 ridge-large:478.38 ridge-roll-large:287.07 screw-long:361.34 screw-short:103.24 snow-guard:492.96 verge:662.52 wood-nails:72.50")]
    [InlineData("gable", "bilka-clasic-35", "7016", "small", "screws", null, null, null, null, "13667.99", "batten:1577.70 bilka-clasic-35/7016#4350 mm:5300.39 counter-batten:701.20 downpipe-90:351.40 downpipe-extension-90:97.28 eave:492.96 elbow-90:155.92 gable-cap:83.10 gutter-125:442.74 gutter-cap-125:33.12 gutter-hook-125:467.78 gutter-join-125:45.72 membrane:758.00 membrane-tape:232.00 outlet-125:77.96 pipe-anchors:66.66 pipe-clamp-90:89.40 pipe-exit-90:92.16 ridge-roll-small:258.96 ridge-small:411.00 screw-long:361.34 screw-short:103.24 snow-guard:492.96 verge:662.52 wood-screws:312.48")]
    [InlineData("gable", "bilka-retro-panel", "8019", null, null, null, null, null, null, "17523.17", "bilka-retro-panel/8019#4100 mm:6350.60 board:4153.50 butyl-tape:432.00 counter-batten:701.20 downpipe-90:351.40 downpipe-extension-90:97.28 eave-retro:662.52 elbow-90:155.92 flat-sheet-8019:147.00 gable-cap:83.10 gutter-125:442.74 gutter-cap-125:33.12 gutter-hook-125:467.78 gutter-join-125:45.72 membrane:758.00 membrane-tape:232.00 outlet-125:77.96 pipe-anchors:66.66 pipe-clamp-90:89.40 pipe-exit-90:92.16 ridge-retro:380.76 ridge-support:264.96 screw-flat:197.37 screw-sealed:103.24 snow-guard-retro:492.96 verge-retro:571.32 wood-nails:72.50")]
    [InlineData("gable", "bilka-retro-panel", "8019", null, null, "attached", null, null, null, "14876.65", "batten:1577.70 bilka-retro-panel/8019#4100 mm:6350.60 butyl-tape:432.00 counter-batten:701.20 downpipe-90:351.40 downpipe-extension-90:97.28 eave-retro:662.52 elbow-90:155.92 flat-sheet-8019:147.00 gable-cap:83.10 gutter-125:442.74 gutter-cap-125:33.12 gutter-hook-125:467.78 gutter-join-125:45.72 membrane:758.00 membrane-tape:232.00 outlet-125:77.96 pipe-anchors:66.66 pipe-clamp-90:89.40 pipe-exit-90:92.16 ridge-retro:380.76 ridge-support:264.96 screw-flat:126.65 screw-sealed:103.24 snow-guard-retro:492.96 verge-retro:571.32 wood-nails:72.50")]
    [InlineData("lhip", "bilka-balcanic", "8017", null, null, null, null, null, null, "27747.95", "batten:2892.45 bilka-balcanic/8017/1180:1197.65 bilka-balcanic/8017/2230:6306.57 bilka-balcanic/8017/2930:1871.97 counter-batten:1192.04 downpipe-100:765.36 downpipe-extension-100:244.24 eave:1191.32 elbow-100:458.24 flat-sheet-8017:294.00 gutter-150:1605.00 gutter-hook-150:1152.00 gutter-inner-150:74.16 gutter-join-150:258.51 gutter-outer-150:357.55 hip-end-large:738.65 hip-large:1195.95 membrane:1137.00 membrane-tape:464.00 outlet-150:229.12 pipe-anchors:133.32 pipe-clamp-100:217.44 pipe-exit-100:247.92 ridge-large:558.11 ridge-roll-large:861.21 screw-long:567.82 screw-short:103.24 sealant:49.06 snow-guard:985.92 valley:244.14 valley-seal:45.24 wood-nails:108.75")]
    [InlineData("lhip", "bilka-clasic-35", "7016", null, null, null, null, null, null, "28603.78", "batten:2892.45 bilka-clasic-35/7016#1200 mm:584.87 bilka-clasic-35/7016#2600 mm:1900.83 bilka-clasic-35/7016#4000 mm:2924.35 bilka-clasic-35/7016#4350 mm:4770.35 counter-batten:1192.04 downpipe-100:765.36 downpipe-extension-100:244.24 eave:1191.32 elbow-100:458.24 flat-sheet-8017:294.00 gutter-150:1605.00 gutter-hook-150:1152.00 gutter-inner-150:74.16 gutter-join-150:258.51 gutter-outer-150:357.55 hip-end-large:738.65 hip-large:1195.95 membrane:1137.00 membrane-tape:464.00 outlet-150:229.12 pipe-anchors:133.32 pipe-clamp-100:217.44 pipe-exit-100:247.92 ridge-large:558.11 ridge-roll-large:861.21 screw-long:619.44 screw-short:103.24 sealant:49.06 snow-guard:985.92 valley:244.14 valley-seal:45.24 wood-nails:108.75")]
    [InlineData("lhip", "bilka-retro-panel", "8019", null, null, null, null, null, null, "35340.24", "bilka-retro-panel/8019#1300 mm:549.17 bilka-retro-panel/8019#1900 mm:802.63 bilka-retro-panel/8019#2500 mm:1056.09 bilka-retro-panel/8019#3100 mm:1309.55 bilka-retro-panel/8019#3700 mm:1563.01 bilka-retro-panel/8019#4300 mm:6357.64 bilka-retro-panel/8019#700 mm:295.70 board:7455.00 butyl-tape:1008.00 counter-batten:1192.04 downpipe-100:765.36 downpipe-extension-100:244.24 eave-retro:1601.09 elbow-100:458.24 flat-sheet-8019:441.00 gutter-150:1605.00 gutter-hook-150:1152.00 gutter-inner-150:74.16 gutter-join-150:258.51 gutter-outer-150:357.55 hip-retro:951.90 lap-small:66.30 membrane:1137.00 membrane-tape:464.00 outlet-150:229.12 pipe-anchors:133.32 pipe-clamp-100:217.44 pipe-exit-100:247.92 ridge-retro:444.22 ridge-support:971.52 screw-flat:406.30 screw-sealed:154.86 sealant:49.06 snow-guard-retro:985.92 valley-retro:190.38 wood-nails:145.00")]
    [InlineData("lhip", "bilka-clasic-35", "7016", "small", "screws", null, null, null, null, "28659.99", "batten:2892.45 bilka-clasic-35/7016#1200 mm:584.87 bilka-clasic-35/7016#2600 mm:1900.83 bilka-clasic-35/7016#4000 mm:2924.35 bilka-clasic-35/7016#4350 mm:4770.35 counter-batten:1192.04 downpipe-100:765.36 downpipe-extension-100:244.24 eave:1191.32 elbow-100:458.24 flat-sheet-8017:294.00 gutter-150:1605.00 gutter-hook-150:1152.00 gutter-inner-150:74.16 gutter-join-150:258.51 gutter-outer-150:357.55 hip-end-small:675.00 hip-small:1027.50 membrane:1137.00 membrane-tape:464.00 outlet-150:229.12 pipe-anchors:133.32 pipe-clamp-100:217.44 pipe-exit-100:247.92 ridge-roll-small:776.88 ridge-small:479.50 screw-long:619.44 screw-short:103.24 sealant:49.06 snow-guard:985.92 valley:244.14 valley-seal:45.24 wood-screws:560.00")]
    [InlineData("wallgable", "bilka-balcanic", "8017", null, null, null, null, null, null, "12496.89", "batten:1472.52 bilka-balcanic/8017/2230:4759.68 counter-batten:648.61 downpipe-90:351.40 downpipe-extension-90:97.28 eave:492.96 elbow-90:155.92 gable-cap:41.55 gutter-125:442.74 gutter-cap-125:33.12 gutter-hook-125:443.16 gutter-join-125:45.72 membrane:758.00 membrane-tape:232.00 outlet-125:77.96 pipe-anchors:66.66 pipe-clamp-90:89.40 pipe-exit-90:92.16 ridge-large:478.38 ridge-roll-large:287.07 screw-long:309.72 screw-short:103.24 snow-guard:451.88 verge:331.26 wall-side:162.00 wood-nails:72.50")]
    [InlineData("wallgable", "bilka-clasic-35", "7016", null, null, null, null, null, null, "12507.56", "batten:1472.52 bilka-clasic-35/7016#4350 mm:4770.35 counter-batten:648.61 downpipe-90:351.40 downpipe-extension-90:97.28 eave:492.96 elbow-90:155.92 gable-cap:41.55 gutter-125:442.74 gutter-cap-125:33.12 gutter-hook-125:443.16 gutter-join-125:45.72 membrane:758.00 membrane-tape:232.00 outlet-125:77.96 pipe-anchors:66.66 pipe-clamp-90:89.40 pipe-exit-90:92.16 ridge-large:478.38 ridge-roll-large:287.07 screw-long:309.72 screw-short:103.24 snow-guard:451.88 verge:331.26 wall-side:162.00 wood-nails:72.50")]
    [InlineData("wallgable", "bilka-retro-panel", "8019", null, null, null, null, null, null, "16542.57", "bilka-retro-panel/8019#4100 mm:5773.28 board:3940.50 butyl-tape:432.00 counter-batten:648.61 downpipe-90:351.40 downpipe-extension-90:97.28 eave-retro:662.52 elbow-90:155.92 flat-sheet-8019:147.00 gable-cap:41.55 gutter-125:442.74 gutter-cap-125:33.12 gutter-hook-125:443.16 gutter-join-125:45.72 membrane:758.00 membrane-tape:232.00 outlet-125:77.96 pipe-anchors:66.66 pipe-clamp-90:89.40 pipe-exit-90:92.16 ridge-retro:380.76 ridge-support:264.96 screw-flat:187.51 screw-sealed:103.24 snow-guard-retro:451.88 verge-retro:285.66 wall-side-retro:265.08 wood-nails:72.50")]
    [InlineData("leanto", "bilka-balcanic", "8017", null, null, null, null, null, null, "9844.55", "batten:1314.75 bilka-balcanic/8017/2230:2379.84 bilka-balcanic/8017/2930:1559.98 counter-batten:596.02 downpipe-90:281.12 downpipe-extension-90:72.96 eave:246.48 elbow-90:116.94 gutter-125:221.37 gutter-cap-125:16.56 gutter-hook-125:233.89 gutter-join-125:22.86 membrane:758.00 membrane-tape:232.00 outlet-125:58.47 pipe-anchors:66.66 pipe-clamp-90:67.05 pipe-exit-90:69.12 screw-long:309.72 screw-short:51.62 snow-guard:492.96 verge:441.68 wall-top:162.00 wood-nails:72.50")]
    [InlineData("leanto", "bilka-clasic-35", "7016", null, null, null, null, null, null, "10169.41", "batten:1314.75 bilka-clasic-35/7016#7000 mm:4264.68 counter-batten:596.02 downpipe-90:281.12 downpipe-extension-90:72.96 eave:246.48 elbow-90:116.94 gutter-125:221.37 gutter-cap-125:16.56 gutter-hook-125:233.89 gutter-join-125:22.86 membrane:758.00 membrane-tape:232.00 outlet-125:58.47 pipe-anchors:66.66 pipe-clamp-90:67.05 pipe-exit-90:69.12 screw-long:309.72 screw-short:51.62 snow-guard:492.96 verge:441.68 wall-top:162.00 wood-nails:72.50")]
    [InlineData("leanto", "bilka-retro-panel", "8019", null, null, null, null, null, null, "14007.52", "bilka-retro-panel/8019#7000 mm:5421.25 board:3479.00 butyl-tape:288.00 counter-batten:596.02 downpipe-90:281.12 downpipe-extension-90:72.96 eave-retro:331.26 elbow-90:116.94 gutter-125:221.37 gutter-cap-125:16.56 gutter-hook-125:233.89 gutter-join-125:22.86 membrane:758.00 membrane-tape:232.00 outlet-125:58.47 pipe-anchors:66.66 pipe-clamp-90:67.05 pipe-exit-90:69.12 ridge-support:132.48 screw-flat:161.67 screw-sealed:103.24 snow-guard-retro:492.96 verge-retro:380.88 wall-top-retro:331.26 wood-nails:72.50")]
    [InlineData("gable", "bilka-balcanic", "8017", null, null, null, "150/100", null, null, "13417.89", "batten:1577.70 bilka-balcanic/8017/2230:4759.68 counter-batten:701.20 downpipe-100:425.20 downpipe-extension-100:122.12 eave:492.96 elbow-100:229.12 gable-cap:83.10 gutter-150:642.00 gutter-cap-150:38.88 gutter-hook-150:486.40 gutter-join-150:49.24 membrane:758.00 membrane-tape:232.00 outlet-150:114.56 pipe-anchors:66.66 pipe-clamp-100:108.72 pipe-exit-100:123.96 ridge-large:478.38 ridge-roll-large:287.07 screw-long:309.72 screw-short:103.24 snow-guard:492.96 verge:662.52 wood-nails:72.50")]
    [InlineData("gable", "bilka-balcanic", "8017", null, null, null, null, 2, null, "12512.03", "batten:1577.70 bilka-balcanic/8017/2230:4759.68 counter-batten:701.20 downpipe-90:210.84 downpipe-extension-90:48.64 eave:492.96 elbow-90:77.96 gable-cap:83.10 gutter-125:442.74 gutter-cap-125:33.12 gutter-hook-125:467.78 gutter-join-125:45.72 membrane:758.00 membrane-tape:232.00 outlet-125:38.98 pipe-anchors:44.44 pipe-clamp-90:44.70 pipe-exit-90:46.08 ridge-large:478.38 ridge-roll-large:287.07 screw-long:309.72 screw-short:103.24 snow-guard:492.96 verge:662.52 wood-nails:72.50")]
    [InlineData("gable", "bilka-balcanic", "8017", null, null, null, "150/100", 6, null, "13981.65", "batten:1577.70 bilka-balcanic/8017/2230:4759.68 counter-batten:701.20 downpipe-100:595.28 downpipe-extension-100:183.18 eave:492.96 elbow-100:343.68 gable-cap:83.10 gutter-150:642.00 gutter-cap-150:38.88 gutter-hook-150:486.40 gutter-join-150:49.24 membrane:758.00 membrane-tape:232.00 outlet-150:171.84 pipe-anchors:111.10 pipe-clamp-100:163.08 pipe-exit-100:185.94 ridge-large:478.38 ridge-roll-large:287.07 screw-long:309.72 screw-short:103.24 snow-guard:492.96 verge:662.52 wood-nails:72.50")]
    [InlineData("gable", "bilka-balcanic", "8017", null, null, null, null, null, 0.08, "13417.89", "batten:1577.70 bilka-balcanic/8017/2230:4759.68 counter-batten:701.20 downpipe-100:425.20 downpipe-extension-100:122.12 eave:492.96 elbow-100:229.12 gable-cap:83.10 gutter-150:642.00 gutter-cap-150:38.88 gutter-hook-150:486.40 gutter-join-150:49.24 membrane:758.00 membrane-tape:232.00 outlet-150:114.56 pipe-anchors:66.66 pipe-clamp-100:108.72 pipe-exit-100:123.96 ridge-large:478.38 ridge-roll-large:287.07 screw-long:309.72 screw-short:103.24 snow-guard:492.96 verge:662.52 wood-nails:72.50")]
    [InlineData("lhip", "bilka-clasic-35", "7016", null, null, null, "125/90", null, null, "27494.04", "batten:2892.45 bilka-clasic-35/7016#1200 mm:584.87 bilka-clasic-35/7016#2600 mm:1900.83 bilka-clasic-35/7016#4000 mm:2924.35 bilka-clasic-35/7016#4350 mm:4770.35 counter-batten:1192.04 downpipe-90:632.52 downpipe-extension-90:194.56 eave:1191.32 elbow-90:311.84 flat-sheet-8017:294.00 gutter-125:1106.85 gutter-hook-125:1107.90 gutter-inner-125:64.51 gutter-join-125:240.03 gutter-outer-125:322.55 hip-end-large:738.65 hip-large:1195.95 membrane:1137.00 membrane-tape:464.00 outlet-125:155.92 pipe-anchors:133.32 pipe-clamp-90:178.80 pipe-exit-90:184.32 ridge-large:558.11 ridge-roll-large:861.21 screw-long:619.44 screw-short:103.24 sealant:49.06 snow-guard:985.92 valley:244.14 valley-seal:45.24 wood-nails:108.75")]
    [InlineData("lhip", "bilka-clasic-35", "7016", null, null, null, null, 10, null, "29167.54", "batten:2892.45 bilka-clasic-35/7016#1200 mm:584.87 bilka-clasic-35/7016#2600 mm:1900.83 bilka-clasic-35/7016#4000 mm:2924.35 bilka-clasic-35/7016#4350 mm:4770.35 counter-batten:1192.04 downpipe-100:935.44 downpipe-extension-100:305.30 eave:1191.32 elbow-100:572.80 flat-sheet-8017:294.00 gutter-150:1605.00 gutter-hook-150:1152.00 gutter-inner-150:74.16 gutter-join-150:258.51 gutter-outer-150:357.55 hip-end-large:738.65 hip-large:1195.95 membrane:1137.00 membrane-tape:464.00 outlet-150:286.40 pipe-anchors:177.76 pipe-clamp-100:271.80 pipe-exit-100:309.90 ridge-large:558.11 ridge-roll-large:861.21 screw-long:619.44 screw-short:103.24 sealant:49.06 snow-guard:985.92 valley:244.14 valley-seal:45.24 wood-nails:108.75")]
    [InlineData("lhip", "bilka-retro-panel", "8019", null, null, null, "125/90", 3, null, "33171.54", "bilka-retro-panel/8019#1300 mm:549.17 bilka-retro-panel/8019#1900 mm:802.63 bilka-retro-panel/8019#2500 mm:1056.09 bilka-retro-panel/8019#3100 mm:1309.55 bilka-retro-panel/8019#3700 mm:1563.01 bilka-retro-panel/8019#4300 mm:6357.64 bilka-retro-panel/8019#700 mm:295.70 board:7455.00 butyl-tape:1008.00 counter-batten:1192.04 downpipe-90:281.12 downpipe-extension-90:72.96 eave-retro:1601.09 elbow-90:116.94 flat-sheet-8019:441.00 gutter-125:1106.85 gutter-hook-125:1107.90 gutter-inner-125:64.51 gutter-join-125:240.03 gutter-outer-125:322.55 hip-retro:951.90 lap-small:66.30 membrane:1137.00 membrane-tape:464.00 outlet-125:58.47 pipe-anchors:66.66 pipe-clamp-90:67.05 pipe-exit-90:69.12 ridge-retro:444.22 ridge-support:971.52 screw-flat:406.30 screw-sealed:154.86 sealant:49.06 snow-guard-retro:985.92 valley-retro:190.38 wood-nails:145.00")]
    [InlineData("lhip", "bilka-balcanic", "8017", "small", null, null, null, null, null, "27352.91", "batten:2892.45 bilka-balcanic/8017/1180:1197.65 bilka-balcanic/8017/2230:6306.57 bilka-balcanic/8017/2930:1871.97 counter-batten:1192.04 downpipe-100:765.36 downpipe-extension-100:244.24 eave:1191.32 elbow-100:458.24 flat-sheet-8017:294.00 gutter-150:1605.00 gutter-hook-150:1152.00 gutter-inner-150:74.16 gutter-join-150:258.51 gutter-outer-150:357.55 hip-end-small:675.00 hip-small:1027.50 membrane:1137.00 membrane-tape:464.00 outlet-150:229.12 pipe-anchors:133.32 pipe-clamp-100:217.44 pipe-exit-100:247.92 ridge-roll-small:776.88 ridge-small:479.50 screw-long:567.82 screw-short:103.24 sealant:49.06 snow-guard:985.92 valley:244.14 valley-seal:45.24 wood-nails:108.75")]
    [InlineData("leanto", "bilka-retro-panel", "8019", null, null, null, "150/100", null, 0.03, "14037.58", "bilka-retro-panel/8019#7000 mm:5421.25 board:3479.00 butyl-tape:288.00 counter-batten:596.02 downpipe-100:255.12 downpipe-extension-100:61.06 eave-retro:331.26 elbow-100:114.56 gutter-150:321.00 gutter-cap-150:19.44 gutter-hook-150:243.20 gutter-join-150:24.62 membrane:758.00 membrane-tape:232.00 outlet-150:57.28 pipe-anchors:44.44 pipe-clamp-100:54.36 pipe-exit-100:61.98 ridge-support:132.48 screw-flat:161.67 screw-sealed:103.24 snow-guard-retro:492.96 verge-retro:380.88 wall-top-retro:331.26 wood-nails:72.50")]
    [InlineData("chamfer", "bilka-balcanic", "8017", null, null, null, null, null, null, "19096.68", "batten:1788.06 bilka-balcanic/8017/1180:945.51 bilka-balcanic/8017/2230:3450.77 bilka-balcanic/8017/2930:2183.97 counter-batten:753.79 downpipe-90:491.96 downpipe-extension-90:145.92 eave:862.68 elbow-90:233.88 flat-sheet-8017:294.00 gutter-125:811.69 gutter-hook-125:812.46 gutter-join-125:182.88 gutter-outer-125:193.53 hip-end-large:738.65 hip-large:1435.14 membrane:758.00 membrane-tape:348.00 outlet-125:116.94 pipe-anchors:111.10 pipe-clamp-90:134.10 pipe-exit-90:138.24 ridge-large:159.46 ridge-roll-large:669.83 screw-long:412.96 screw-short:103.24 sealant:49.06 snow-guard:698.36 wood-nails:72.50")]
    [InlineData("chamfer", "bilka-clasic-35", "7016", null, null, null, null, null, null, "19553.15", "batten:1788.06 bilka-clasic-35/7016#1200 mm:584.87 bilka-clasic-35/7016#2600 mm:1108.82 bilka-clasic-35/7016#4000 mm:2193.26 bilka-clasic-35/7016#4700 mm:3149.77 counter-batten:753.79 downpipe-90:491.96 downpipe-extension-90:145.92 eave:862.68 elbow-90:233.88 flat-sheet-8017:294.00 gutter-125:811.69 gutter-hook-125:812.46 gutter-join-125:182.88 gutter-outer-125:193.53 hip-end-large:738.65 hip-large:1435.14 membrane:758.00 membrane-tape:348.00 outlet-125:116.94 pipe-anchors:111.10 pipe-clamp-90:134.10 pipe-exit-90:138.24 ridge-large:159.46 ridge-roll-large:669.83 screw-long:412.96 screw-short:103.24 sealant:49.06 snow-guard:698.36 wood-nails:72.50")]
    [InlineData("chamfer", "bilka-retro-panel", "8019", null, null, null, null, null, null, "23523.60", "bilka-retro-panel/8019#1500 mm:369.63 bilka-retro-panel/8019#1900 mm:401.31 bilka-retro-panel/8019#2300 mm:404.83 bilka-retro-panel/8019#2700 mm:475.24 bilka-retro-panel/8019#3100 mm:654.77 bilka-retro-panel/8019#3500 mm:1108.89 bilka-retro-panel/8019#3900 mm:137.29 bilka-retro-panel/8019#4300 mm:1362.35 bilka-retro-panel/8019#4700 mm:2812.71 bilka-retro-panel/8019#700 mm:221.78 board:4650.50 butyl-tape:720.00 counter-batten:753.79 downpipe-90:491.96 downpipe-extension-90:145.92 eave-retro:1159.41 elbow-90:233.88 flat-sheet-8019:441.00 gutter-125:811.69 gutter-hook-125:812.46 gutter-join-125:182.88 gutter-outer-125:193.53 hip-retro:1142.28 membrane:758.00 membrane-tape:348.00 outlet-125:116.94 pipe-anchors:111.10 pipe-clamp-90:134.10 pipe-exit-90:138.24 ridge-retro:126.92 ridge-support:883.20 screw-flat:259.59 screw-sealed:103.24 sealant:49.06 snow-guard-retro:698.36 wood-nails:108.75")]
    [InlineData("chamfer", "bilka-clasic-35", "7016", "small", null, null, "150/100", null, null, "20014.00", "batten:1788.06 bilka-clasic-35/7016#1200 mm:584.87 bilka-clasic-35/7016#2600 mm:1108.82 bilka-clasic-35/7016#4000 mm:2193.26 bilka-clasic-35/7016#4700 mm:3149.77 counter-batten:753.79 downpipe-100:595.28 downpipe-extension-100:183.18 eave:862.68 elbow-100:343.68 flat-sheet-8017:294.00 gutter-150:1177.00 gutter-hook-150:844.80 gutter-join-150:196.96 gutter-outer-150:214.53 hip-end-small:675.00 hip-small:1233.00 membrane:758.00 membrane-tape:348.00 outlet-150:171.84 pipe-anchors:111.10 pipe-clamp-100:163.08 pipe-exit-100:185.94 ridge-roll-small:604.24 ridge-small:137.00 screw-long:412.96 screw-short:103.24 sealant:49.06 snow-guard:698.36 wood-nails:72.50")]
    public void The_estimate_is_the_Panotaj_Bilka_apps_Deviz_RON(string roof, string profile, string color, string? ridge, string? fixing, string? support,
        string? system, int? downpipes, double? rain, string subtotal, string lines)
    {
        var (f, id) = Make(roof);
        f.Run("cost.addPriceList", new JsonObject { ["template"] = Template });
        Panels(f, id, profile, color, ridge, fixing, support, system, downpipes, rain);
        // The Panel layout's Cost tab: each line priced, the subtotal of the priced ones.
        var cost = Layout(f, id)["cost"]!.AsObject();
        Assert.True((bool)cost["available"]!);
        Assert.True((bool)cost["indicative"]!);
        var tab = cost["lines"]!.AsArray().Where(l => l!["amount"] != null)
            .Select(l => (Key: (string?)l!["variant"] is { } v ? $"{(string)l["code"]!}#{v}" : (string)l["code"]!, Value: (double)l["amount"]!))
            .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}:{Amount(x.Value)}");
        Assert.Equal(lines, string.Join(" ", tab));
        Assert.Equal(subtotal, Amount((double)cost["subtotal"]!["RON"]!));
        // The estimate of the cost module, from the roof's Lapin_RoofPanelQuantities: the same lines under Roof, the same sum.
        f.Run("cost.createEstimate", new JsonObject());
        var estimate = (JsonObject)f.Session.Query("cost.estimate", new JsonObject())!;
        var (estimated, sum) = RoofLines(estimate);
        Assert.Equal(lines, estimated);
        Assert.Equal(subtotal, Amount(sum));
        Assert.Equal(subtotal, Amount((double)estimate["chapters"]!.AsArray().Single(c => (string)c!["name"]! == "Roof")!["m"]!));
        // The roof is priced: not listed among what has no price.
        Assert.DoesNotContain(estimate["unpriced"]!.AsArray(), u => (string)u!["class"]! == IfcClass.Roof);
    }

    [Fact]
    public void The_Bilka_list_is_a_template_added_only_when_asked_indicative_with_its_sources()
    {
        var (f, id) = Make("gable");
        Panels(f, id, "bilka-clasic-35", "7016");
        // Nothing is loaded by itself: the roof has panels, the project no price list.
        Assert.Empty(f.Model.OfClass(PriceLists.Class));
        var templates = f.Session.Query("cost.templates", new JsonObject())!.AsArray();
        var bilka = templates.Single(t => (string)t!["id"]! == Template)!;
        Assert.Equal("Bilka – Dedeman (indicative, 24.11.2026)", (string)bilka["name"]!);
        Assert.Equal(79, (int)bilka["rows"]!);
        Assert.Equal("2026-11-24", (string)bilka["checked"]!);
        Assert.True((bool)bilka["indicative"]!);
        Assert.Contains(templates, t => (string)t!["id"]! == "lapin");
        // Asked: one undo step, an indicative list, each row with its source, page and date; the merchant only in the source.
        f.Run("cost.addPriceList", new JsonObject { ["template"] = Template });
        var list = PriceLists.All(f.Model).Single();
        Assert.True(list.Indicative);
        Assert.Equal("RON", list.Currency);
        Assert.Equal("Bilka – Dedeman (indicative, 24.11.2026)", list.Name);
        Assert.All(list.Rows, r => Assert.True(r.Source is { Length: > 0 } && r.Url!.StartsWith("https://", StringComparison.Ordinal) && r.Checked == "2026-11-24", r.Code));
        Assert.All(list.Rows, r => Assert.EndsWith("(indicative)", r.Name));
        Assert.All(list.Rows, r => Assert.DoesNotContain("Dedeman", r.Name));
        var tile = list.Articles["bilka-balcanic/8017/2930"];
        Assert.Equal((45.12, "m²", "Dedeman 6037244"), (tile.Material, tile.Unit, tile.Source));
        // The tile per m² by colour: Clasic 7016 and 8017 alike, Retro 8019.
        Assert.Equal(50.77, list.Articles["bilka-clasic-35/7016"].Material);
        Assert.Equal(50.77, list.Articles["bilka-clasic-35/8017"].Material);
        Assert.Equal(64.95, list.Articles["bilka-retro-panel/8019"].Material);
        Assert.Equal("eAcoperiș", list.Articles["hip-end-large"].Source);
        // Each priced line of the Cost tab names where its price is from.
        var line = Layout(f, id)["cost"]!["lines"]!.AsArray().First(l => (string)l!["code"]! == "bilka-clasic-35/7016")!;
        Assert.Equal(("Dedeman 6061465", "2026-11-24", "m²"), ((string)line["source"]!, (string)line["checked"]!, (string)line["priceUnit"]!));
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(PriceLists.Class));
        Assert.Throws<CommandException>(() => f.Run("cost.addPriceList", new JsonObject { ["template"] = "nowhere" }));
    }

    [Fact]
    public void Without_a_price_list_the_roof_has_its_quantities_and_no_prices()
    {
        var (f, id) = Make("lhip");
        Panels(f, id, "bilka-clasic-35", "7016");
        // The roof's Lapin_RoofPanelQuantities: m² of sheet a length, the accessories in their units, by their codes.
        var set = f.Session.Quantities(id).Single(s => s.Name == PanelCost.SetName);
        Assert.Equal(ArticleQuantities.Part, set.Part);
        var q = set.Quantities.ToDictionary(x => x.Name);
        Assert.Equal(93.96, q["bilka-clasic-35/7016#4350 mm"].Value, 9);
        Assert.Equal("AREA", q["bilka-clasic-35/7016#4350 mm"].Kind);
        Assert.Equal(15, q["hip-large"].Value);
        Assert.Equal(12, q["screw-long"].Value);
        // What is cut from flat sheet is not bought: not in the quantities.
        Assert.DoesNotContain("ridge-nodes", q.Keys);
        var cost = Layout(f, id)["cost"]!.AsObject();
        Assert.True((bool)cost["available"]!);
        Assert.Equal(0, (int)cost["priced"]!);
        Assert.Empty(cost["subtotal"]!.AsObject());
        Assert.Equal(set.Quantities.Count, (int)cost["toQuote"]!);
        Assert.Equal(1, (int)cost["fromSheet"]!);
        // The joints between the ridge caps, apart (the app's 15 on this roof).
        Assert.Equal(15, (int)cost["joints"]!);
        // The estimate has the lines, to quote: no value, their codes missing from the lists.
        f.Run("cost.createEstimate", new JsonObject());
        var estimate = (JsonObject)f.Session.Query("cost.estimate", new JsonObject())!;
        var roof = estimate["chapters"]!.AsArray().Single(c => (string)c!["name"]! == "Roof")!["lines"]!.AsArray();
        Assert.Equal(set.Quantities.Count, roof.Count);
        Assert.All(roof, l => Assert.False((bool)l!["priced"]!));
        Assert.Contains(estimate["missing"]!.AsArray(), x => (string)x! == "hip-large");
        Assert.Equal(93.96, (double)roof.Single(l => ((string)l!["key"]!).EndsWith("4350 mm", StringComparison.Ordinal))!["qty"]!, 9);
        // Another colour is another article, live: the preview and the roof alike.
        var preview = (JsonObject)f.Session.Query("roofpanels.layout", new JsonObject { ["id"] = id, ["profile"] = "bilka-clasic-35", ["color"] = "8017" })!;
        Assert.Contains(preview["cost"]!["lines"]!.AsArray(), l => (string)l!["code"]! == "bilka-clasic-35/8017");
        Panels(f, id, "bilka-clasic-35", "8017", fixing: "screws");
        var names = f.Session.Quantities(id).Single(s => s.Name == PanelCost.SetName).Quantities.Select(x => x.Name).ToList();
        Assert.Contains("bilka-clasic-35/8017#4350 mm", names);
        Assert.Contains("wood-screws", names);
        Assert.DoesNotContain("wood-nails", names);
        // No profile, no panel quantities.
        f.Run("roofpanels.setRoofPanels", new JsonObject { ["id"] = id, ["profile"] = null });
        Assert.DoesNotContain(f.Session.Quantities(id), s => s.Name == PanelCost.SetName);
    }

    [Fact]
    public void Retro_Panels_flat_sheet_pieces_are_not_bought_and_your_own_prices_come_first()
    {
        var (f, id) = Make("lhip");
        f.Run("cost.addPriceList", new JsonObject { ["template"] = Template });
        Panels(f, id, "bilka-retro-panel", null);
        var cost = Layout(f, id)["cost"]!.AsObject();
        var sheet = cost["lines"]!.AsArray().Where(l => (string?)l!["madeFrom"] == "flatSheet").Select(l => (string)l!["code"]!).Order().ToList();
        Assert.Equal(["hip-end-retro", "ridge-nodes", "valley-clips"], sheet);
        Assert.Equal(0, (int)cost["toQuote"]!);
        Assert.Equal("35340.24", Amount((double)cost["subtotal"]!["RON"]!));
        // A list of your own, named first by the estimate, prices what it has; the template the rest.
        var mine = (string)f.Run("cost.addPriceList", new JsonObject
        {
            ["name"] = "My roofer", ["currency"] = "RON",
            ["rows"] = new JsonArray(new JsonObject { ["code"] = "board", ["name"] = "Board 24 × 150 × 4000", ["unit"] = "board 4 m", ["m"] = 30, ["source"] = "Local timber yard" }),
        })!["id"]!;
        var template = PriceLists.All(f.Model).Single(l => l.Name.StartsWith("Bilka", StringComparison.Ordinal)).Id;
        f.Run("cost.createEstimate", new JsonObject());
        f.Run("cost.updateEstimate", new JsonObject { ["priceLists"] = new JsonArray(mine, template) });
        var board = Layout(f, id)["cost"]!["lines"]!.AsArray().Single(l => (string)l!["code"]! == "board")!;
        Assert.Equal((30.0, 6300.0, "Local timber yard", false), ((double)board["price"]!, (double)board["amount"]!, (string)board["source"]!, (bool)board["indicative"]!));
        Assert.Equal(Amount(35340.24 - 7455 + 6300), Amount((double)Layout(f, id)["cost"]!["subtotal"]!["RON"]!));
    }

    [Fact]
    public void Without_the_cost_module_the_layout_says_so()
    {
        var (f, id) = RoofPanelTakeoffTests.Make("gable");
        Panels(f, id, "bilka-balcanic", null);
        var cost = Layout(f, id)["cost"]!.AsObject();
        Assert.False((bool)cost["available"]!);
        Assert.Equal(0, (int)cost["priced"]!);
        // The quantities are the roof's all the same.
        Assert.Contains(f.Session.Quantities(id), s => s.Name == PanelCost.SetName);
    }

    sealed class Plain(bool indicative) : IPriceListTemplate
    {
        public string Id => "plain";
        public string Name => "Plain";
        public string Note => "";
        public JsonObject Read() => new() { ["format"] = "lapin-price-list", ["version"] = 1, ["indicative"] = indicative, ["rows"] = new JsonArray(new JsonObject { ["c"] = "x", ["u"] = "piece", ["m"] = 1 }) };
    }

    [Fact]
    public void A_template_that_is_not_marked_indicative_is_refused_and_a_tables_sources_are_read()
    {
        Assert.Throws<CommandException>(() => PriceLists.FromTemplate(new Plain(false)));
        Assert.Single(PriceLists.FromTemplate(new Plain(true)).Rows);
        // A table of your own with its sources: the columns guessed from Romanian headings.
        var table = PriceLists.Csv("Cod;Denumire;UM;Preț;Sursa;Link;Data\nridge-large;Coamă mare;piece 2 m;75,5;Magazin X;https://x.ro/coama;2026-11-20\n");
        var (header, map) = PriceLists.Guess(table);
        var row = PriceLists.Import(table, header, map).Rows.Single();
        Assert.Equal(("ridge-large", 75.5, "Magazin X", "https://x.ro/coama", "2026-11-20"), (row.Code, row.Material, row.Source, row.Url, row.Checked));
    }
}
