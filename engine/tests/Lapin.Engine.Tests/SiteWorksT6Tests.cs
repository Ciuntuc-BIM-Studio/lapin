using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Site grading T6 (docs/plan-site-grading.md; forum 230 §5): a standalone channel at a grade (its invert exact, cutting the
/// terrain and the hardscape it runs through, its volumes) or at a depth under the ground; a standalone kerb along a feature line
/// with a drop; accessories along a line on the ground, as an array whose members are real elements and keep their ids; the
/// follow-ups (an old-style kerb of a typed path, a hosted hardscape copied, a platform's slopes made a grading); IFC; undo.
/// </summary>
public sealed class SiteWorksT6Tests
{
    static Fixture F() => new(new ArchitectureModule(), new CivilModule(), new StructureModule());
    static JsonArray P(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);
    static JsonArray Pts(params double[][] pts) => new([.. pts.Select(p => (JsonNode)P(p))]);
    static JsonArray Rect(double x0, double y0, double x1, double y1) => Pts([x0, y0], [x1, y0], [x1, y1], [x0, y1]);

    /// <summary>A 40 × 40 m terrain surveyed every 2 m, heights z(x, y), datum 0.</summary>
    static string Field(Fixture f, Func<double, double, double>? z = null)
    {
        z ??= (_, _) => 0;
        var pts = new JsonArray();
        for (var i = 0; i <= 20; i++) for (var j = 0; j <= 20; j++) pts.Add(P(i * 2.0, j * 2.0, z(i * 2.0, j * 2.0)));
        return (string)f.Run("civil.addTerrain", new JsonObject { ["storeyId"] = f.Storey, ["points"] = pts, ["datum"] = 0, ["interval"] = 0.5, ["depth"] = 2 })!["id"]!;
    }

    static double Vol(Fixture f, string id, string? key = null) => f.Session.Geometry(id)!.Parts.Where(p => key == null || p.Key == key).Sum(p => Measure.Volume(p.Solid));

    static double Q(Fixture f, string id, string set, string name) =>
        f.Session.Quantities(id).First(s => s.Name == set).Quantities.First(q => q.Name == name).Value;

    [Fact]
    public void A_grated_drain_at_a_grade_has_its_invert_exact_and_cuts_the_plaza_and_the_ground()
    {
        var f = F();
        var t = Field(f, (_, _) => 1.0);
        var plaza = (string)f.Run("civil.addHardscape", new JsonObject { ["points"] = Rect(10, 10, 30, 20), ["type"] = "pavers", ["elevation"] = 1.0 })!["id"]!;
        var plazaBefore = Vol(f, plaza);
        var groundBefore = Vol(f, t, "ground");
        // 16 m of grated drain at 0.5 % from an invert of 0.80, along the plaza's middle.
        var r = f.Run("civil.addChannel", new JsonObject { ["points"] = Pts([12, 15], [28, 15]), ["kind"] = "grated", ["grade"] = "0.5 %", ["startZ"] = 0.80 })!;
        var id = (string)r["id"]!;
        Assert.Equal(t, (string?)r["terrainId"]);
        var e = f.Model.Get(id)!;
        Assert.Equal(Channels.Class, e.Class);
        Assert.Equal("TRENCH", e.PredefinedType);
        Assert.Equal("IfcDistributionChamberElement", e.Str("exportAs"));
        var l = Channels.LaidOf(f.Model, e);
        Assert.Equal(16, l.Length, 12);
        // Its invert falls 0.5 % from 0.80, exactly, at every section; its top is the plaza's.
        for (var i = 0; i < l.Stations.Count; i++)
        {
            Assert.Equal(0.80 - 0.005 * l.Stations[i], l.Invert[i], 12);
            Assert.Equal(1.0, l.Top[i], 9);
        }
        Assert.Equal(0.72, l.InvertAt(16), 12);
        // Its outlet: its lowest point, the end; a low point there.
        Assert.Equal([16.0], l.Outlets);
        Assert.Contains(16.0, l.Lows);
        // Its body: the grating at the top, the U under the invert by its base.
        var g = f.Session.Geometry(id)!;
        Assert.Contains(g.Parts, p => p.Key == "grating");
        Assert.Contains(g.Parts, p => p.Key == "outlet");
        var body = Measure.Bounds(g.Parts.First(p => p.Key == "body").Solid)!.Value;
        Assert.Equal(1.0, body.Max.Z, 9);
        Assert.Equal(0.72 - 0.08, body.Min.Z, 6);
        Assert.Equal(16, Q(f, id, "Lapin_ChannelQuantities", "Length"), 9);
        Assert.Equal(16, Q(f, id, "Lapin_ChannelQuantities", "GratingLength"), 9);
        Assert.Equal(0.08, Q(f, id, "Lapin_ChannelQuantities", "Fall"), 12);
        Assert.Equal(0.20, Q(f, id, "Lapin_ChannelQuantities", "MinDepth"), 9);
        Assert.Equal(0.28, Q(f, id, "Lapin_ChannelQuantities", "MaxDepth"), 9);
        // The U body: its walls and base, the section's area along it (0.25 wide outside, 0.15 inside, 8 cm base).
        var area = (Func<double, double>)(s => { var d = 1.0 - (0.80 - 0.005 * s); return 0.25 * (d + 0.08) - 0.15 * d; });
        var expected = 0.0;
        for (var k = 0; k < 1600; k++) expected += area((k + 0.5) / 100) / 100;
        Assert.Equal(expected, Vol(f, id, "body"), 4);
        // Its excavation: its outer shape from its top down, 0.25 wide, plus the gully's pit.
        var trench = 0.25 * (0.28 * 16 + 0.005 * 16 * 16 / 2);
        Assert.InRange(Q(f, id, "Lapin_ChannelQuantities", "ExcavationVolume"), trench, trench + 0.4 * 0.4 * 0.9);
        // It cuts the plaza's layers and the ground under them.
        Assert.True(Vol(f, plaza) < plazaBefore - 0.25 * 16 * 0.29, $"{plazaBefore} → {Vol(f, plaza)}");
        Assert.True(Vol(f, t, "ground") < groundBefore - 0.1, $"{groundBefore} → {Vol(f, t, "ground")}");
        // One undo step: the channel gone, the plaza whole again.
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
        Assert.Equal(plazaBefore, Vol(f, plaza), 9);
        Assert.Equal(groundBefore, Vol(f, t, "ground"), 6);
    }

    [Fact]
    public void A_channel_at_a_depth_follows_the_ground_and_its_low_end_is_its_outlet()
    {
        var f = F();
        var t = Field(f, (x, _) => 0.02 * x);
        var id = (string)f.Run("civil.addChannel", new JsonObject { ["points"] = Pts([30, 10], [2, 10]), ["kind"] = "u", ["depth"] = 0.3 })!["id"]!;
        var l = Channels.LaidOf(f.Model, f.Model.Get(id)!);
        for (var i = 0; i < l.Stations.Count; i++)
        {
            var x = 30 - l.Stations[i];
            Assert.Equal(0.02 * x, l.Top[i], 9);
            Assert.Equal(0.02 * x - 0.3, l.Invert[i], 9);
        }
        Assert.Equal([28.0], l.Outlets.Select(s => Math.Round(s, 9)));
        Assert.Equal(0.3, Q(f, id, "Lapin_ChannelQuantities", "MinDepth"), 9);
        Assert.Equal(Channels.Class, f.Model.Get(id)!.Class);
        Assert.Equal(IfcClass.EarthworksCut, f.Model.Get(id)!.Str("exportAs"));
        // Set to a grade: the invert from its start level down; the start raised by a 3D move.
        f.Run("civil.updateChannel", new JsonObject { ["id"] = id, ["grade"] = 0.01, ["startZ"] = 0.2 });
        l = Channels.LaidOf(f.Model, f.Model.Get(id)!);
        Assert.Equal(0.2 - 0.01 * 28, l.Invert[^1], 12);
        // A notes where the invert would come out of the ground: none here (0.2 under 0.6 at the start).
        Assert.DoesNotContain(l.Notes, n => n.Contains("reaches its top"));
        // A swale: shallow, its body paved.
        f.Run("civil.updateChannel", new JsonObject { ["id"] = id, ["kind"] = "swale", ["invert"] = "depth" });
        Assert.Equal("Cobbles", f.Model.Get(f.Model.Get(id)!.Str("material")!)!.Name);
        Assert.True(Vol(f, id, "body") > 0);
        Assert.NotNull(Terrains.Of(f.Model.Get(t)!));
    }

    [Fact]
    public void A_standalone_kerb_follows_a_feature_line_with_a_drop_and_a_mitre()
    {
        var f = F();
        var t = Field(f, (_, _) => 1.0);
        var fl = (string)f.Run("civil.addFeatureLine", new JsonObject { ["terrainId"] = t, ["points"] = Pts([5, 5, 1.0], [15, 5, 1.2], [15, 15, 1.2]) })!["id"]!;
        var groundBefore = Vol(f, t, "ground");
        var id = (string)f.Run("civil.addKerb", new JsonObject { ["along"] = new JsonObject { ["id"] = fl }, ["drops"] = new JsonArray(new JsonObject { ["from"] = 4, ["to"] = 6, ["reveal"] = 0 }) })!["id"]!;
        var k = f.Model.Get(id)!;
        Assert.True(KerbLines.Is(k));
        Assert.Equal("line", k.Str("follow"));
        Assert.Equal(t, k.Str("terrain"));
        var l = KerbLines.Laid(f.Model, k);
        Assert.Equal(1.0, l.Z[0], 9);
        Assert.Equal(1.2, l.Z[^1], 9);
        // Three runs: before the drop, the drop (its top at the line: reveal 0), after it.
        var solids = KerbLines.Solids(f.Model, k);
        Assert.Equal(3, solids.Count);
        var drop = Measure.Bounds(solids[1].Solid)!.Value;
        Assert.Equal(1.0 + 0.02 * 6, drop.Max.Z, 9);
        Assert.Equal(1.0 + 0.02 * 4 - 0.25, drop.Min.Z, 9);
        var first = Measure.Bounds(solids[0].Solid)!.Value;
        Assert.Equal(1.0 + 0.02 * 4 + 0.12, first.Max.Z, 9);
        // On the right of the line, away from the paving: below y = 5 along the first piece.
        Assert.Equal(5 - 0.15, first.Min.Y, 9);
        // The corner mitred: the last run's outer corner at (15.15, 4.85).
        var last = solids[2].Solid.Vertices;
        Assert.Contains(last, v => Math.Abs(v.X - 15.15) < 1e-9 && Math.Abs(v.Y - 4.85) < 1e-9);
        Assert.Equal(20, Q(f, id, "Lapin_KerbQuantities", "Length"), 9);
        Assert.Equal(2, Q(f, id, "Lapin_KerbQuantities", "DroppedLength"), 9);
        Assert.True(Q(f, id, "Lapin_KerbQuantities", "Volume") > 0);
        // It cuts the ground it stands in.
        Assert.True(Vol(f, t, "ground") < groundBefore - 0.1);
        // The line raised: the kerb follows it, in the same step.
        f.Run("civil.setFeatureLinePoints", new JsonObject { ["id"] = fl, ["indices"] = P(0), ["z"] = 1.1 });
        Assert.Equal(1.1, KerbLines.Laid(f.Model, f.Model.Get(id)!).Z[0], 9);
        // A second drop asked, one taken away (reveal null).
        f.Run("civil.updateKerb", new JsonObject { ["id"] = id, ["drop"] = new JsonObject { ["from"] = 12, ["to"] = 13, ["reveal"] = 0.02 } });
        Assert.Equal(2, KerbLines.DropsOf(f.Model.Get(id)!).Count);
        f.Run("civil.updateKerb", new JsonObject { ["id"] = id, ["drop"] = new JsonObject { ["from"] = 3, ["to"] = 7, ["reveal"] = null } });
        Assert.Single(KerbLines.DropsOf(f.Model.Get(id)!));
        f.Session.Undo();
        Assert.Equal(2, KerbLines.DropsOf(f.Model.Get(id)!).Count);
        // A kerb drawn on the ground: its points each at the ground's height.
        var own = (string)f.Run("civil.addKerb", new JsonObject { ["points"] = Pts([20, 20], [30, 20]), ["side"] = "left", ["shape"] = "bevelled" })!["id"]!;
        Assert.Equal("ground", f.Model.Get(own)!.Str("follow"));
        Assert.All(KerbLines.Laid(f.Model, f.Model.Get(own)!).Z, z => Assert.Equal(1.0, z, 9));
        Assert.Equal(20 + 0.15, Measure.Bounds(f.Session.Geometry(own)!.Parts[0].Solid)!.Value.Max.Y, 9);
        // Moved, it goes with its points.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(own), ["op"] = "move", ["by"] = P(0, 2) });
        Assert.Equal(22, KerbLines.Laid(f.Model, f.Model.Get(own)!).Plan[0].Y, 9);
    }

    [Fact]
    public void Accessories_along_a_line_stand_on_the_ground_as_an_array_of_real_elements()
    {
        var f = F();
        var t = Field(f, (x, _) => 0.05 * x);
        var fl = (string)f.Run("civil.addFeatureLine", new JsonObject { ["terrainId"] = t, ["points"] = Pts([2, 20], [20, 20]), ["drape"] = true })!["id"]!;
        var post = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 18 })!["id"]!;
        // Every 6 m, a metre in from each end, on the ground.
        var r = f.Run("element.array", new JsonObject
        {
            ["ids"] = new JsonArray(post), ["kind"] = "path", ["along"] = fl, ["spacing"] = 6, ["start"] = 1, ["end"] = 1, ["level"] = "ground", ["base"] = P(2, 18),
        })!;
        var arr = (string)r["array"]!;
        var items = Arrays.Items(f.Model.Get(arr)!);
        Assert.Equal(3, items.Count);
        var ids = items.Select(i => i.Ids.Single()).ToList();
        Assert.Equal(post, ids[0]);
        Assert.All(ids, id => Assert.Equal(IfcClass.Column, f.Model.Get(id)!.Class));
        // Each at its station (3, 9, 15 m in x) and its foot on the ground there.
        var xs = new[] { 3.0, 9, 15 };
        for (var i = 0; i < 3; i++)
        {
            var b = f.Bounds(ids[i]);
            Assert.Equal(xs[i], (b.Min.X + b.Max.X) / 2, 6);
            Assert.Equal(20, (b.Min.Y + b.Max.Y) / 2, 6);
            Assert.Equal(0.05 * xs[i], b.Min.Z, 6);
        }
        // Every 4 m: the first ones keep their ids, the rest added.
        f.Run("array.update", new JsonObject { ["id"] = arr, ["spacing"] = 4 });
        var now = Arrays.Items(f.Model.Get(arr)!).Select(i => i.Ids.Single()).ToList();
        Assert.Equal(5, now.Count);
        Assert.Equal(ids, now.Take(3));
        Assert.Equal(0.05 * 19, f.Bounds(now[4]).Min.Z, 6);
        // The line moved over higher ground: the posts follow it, up as the ground is.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(fl), ["op"] = "move", ["by"] = P(4, 0) });
        Assert.Equal(0.05 * 7, f.Bounds(now[0]).Min.Z, 6);
        f.Session.Undo();
        Assert.Equal(0.05 * 3, f.Bounds(now[0]).Min.Z, 6);
        // On the line's own height instead, half a metre up.
        f.Run("array.update", new JsonObject { ["id"] = arr, ["level"] = "line", ["rise"] = 0.5 });
        Assert.Equal(0.05 * 3 + 0.5, f.Bounds(now[0]).Min.Z, 6);
        // As the original stood: back down where it was made.
        f.Run("array.update", new JsonObject { ["id"] = arr, ["level"] = null });
        Assert.Equal(0, f.Bounds(now[0]).Min.Z, 6);
    }

    [Fact]
    public void Along_a_hardscape_s_perimeter_lamps_stand_on_its_top()
    {
        var f = F();
        Field(f, (_, _) => 0.5);
        var plaza = (string)f.Run("civil.addHardscape", new JsonObject { ["points"] = Rect(10, 10, 20, 16), ["elevation"] = 0.5 })!["id"]!;
        var path = Arrays.Follow(f.Model, f.Session.Catalog, f.Model.Get(plaza)!)!;
        Assert.True(path.Closed);
        Assert.Equal(32, path.Length, 9);
        Assert.Equal(0.5, path.Height!(7)!.Value, 9);
        var post = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 })!["id"]!;
        var r = f.Run("element.array", new JsonObject { ["ids"] = new JsonArray(post), ["kind"] = "path", ["along"] = plaza, ["count"] = 8, ["level"] = "line", ["base"] = P(0, 0) })!;
        var ids = Arrays.Items(f.Model.Get((string)r["array"]!)!).Select(i => i.Ids.Single()).ToList();
        Assert.Equal(8, ids.Count);
        Assert.All(ids, id => Assert.Equal(0.5, f.Bounds(id).Min.Z, 6));
    }

    [Fact]
    public void An_old_style_kerb_of_a_typed_path_is_as_deep_as_the_type_s_top_layer()
    {
        var f = F();
        var id = (string)f.Run("civil.addPath", new JsonObject { ["points"] = Pts([0, 0], [20, 0]), ["width"] = 2.0, ["kerb"] = true, ["surfaceThickness"] = 0.03, ["storeyId"] = f.Storey })!["id"]!;
        double KerbBottom() => Measure.Bounds(f.Session.Geometry(id)!.Parts.First(p => p.Key == "kerb").Solid)!.Value.Min.Z;
        double Top() => Measure.Bounds(f.Session.Geometry(id)!.Parts.First(p => p.Key is "surface" or "layer:0").Solid)!.Value.Max.Z;
        // Its own courses: as deep as its own surface course (3 cm), as before.
        Assert.Equal(Top() - 0.03, KerbBottom(), 9);
        // Of the pavers' type: as deep as their 6 cm.
        f.Run("civil.updatePath", new JsonObject { ["id"] = id, ["hardscapeType"] = "pavers" });
        Assert.Equal(Top() - 0.06, KerbBottom(), 9);
        f.Run("civil.updatePath", new JsonObject { ["id"] = id, ["useHardscapeType"] = false });
        Assert.Equal(Top() - 0.03, KerbBottom(), 9);
    }

    [Fact]
    public void A_hardscape_cut_into_a_terrain_copied_cuts_the_same_terrain_with_its_kerb()
    {
        var f = F();
        var t = Field(f);
        var id = (string)f.Run("civil.addHardscape", new JsonObject { ["points"] = Rect(5, 5, 10, 9), ["type"] = "pavers", ["kerb"] = new JsonObject { ["all"] = true } })!["id"]!;
        var made = f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "copy", ["by"] = P(12, 0) })!;
        var copies = Hardscapes.Of(f.Model, t).Where(h => h.Id != id).ToList();
        var copy = Assert.Single(copies);
        Assert.Contains(made["ids"]!.AsArray(), x => (string?)x == copy.Id);
        Assert.Equal(new Vec2(17, 5), Hardscapes.Corners(copy)[0]);
        Assert.Equal(Hardscapes.TypeOf(f.Model, f.Model.Get(id)!)!.Id, Hardscapes.TypeOf(f.Model, copy)!.Id);
        Assert.Equal(Vol(f, id), Vol(f, copy.Id), 9);
        Assert.NotNull(Kerbs.Of(f.Model, copy.Id));
        Assert.NotEqual(Kerbs.Of(f.Model, id)!.Id, Kerbs.Of(f.Model, copy.Id)!.Id);
        // The ground meets both: its derived lines hold both outlines.
        var featureLines = (f.Model.Get(t)!.Attrs[FeatureLines.Derived] as JsonArray)?.Count ?? 0;
        Assert.True(featureLines >= 2);
        // One undo step.
        f.Session.Undo();
        Assert.Single(Hardscapes.Of(f.Model, t));
        Assert.Single(f.Model.OfClass(Kerbs.Class));
    }

    [Fact]
    public void A_platform_s_slopes_become_a_grading_in_one_step_and_others_stay_as_they_are()
    {
        var f = F();
        var t = Field(f, (x, _) => 0.05 * x);
        string Pad(double x0) => (string)f.Run("civil.addPlatform", new JsonObject { ["terrainId"] = t, ["points"] = Rect(x0, 15, x0 + 6, 21), ["elevation"] = 0.05 * (x0 + 3) })!["id"]!;
        var pad = Pad(8);
        var other = Pad(24);
        foreach (var p in new[] { pad, other }) f.Run("civil.updatePlatform", new JsonObject { ["id"] = p, ["cutSlope"] = 1.5, ["fillSlope"] = 2.5 });
        var otherBefore = f.Model.Get(other)!.Attrs.ToJsonString();
        var r = f.Run("civil.platformSlopesToGrading", new JsonObject { ["id"] = pad })!;
        var g = f.Model.Get((string)r["id"]!)!;
        Assert.Equal(Gradings.Class, g.Class);
        Assert.Equal("slope", g.Str("kind"));
        Assert.Equal(1.5, g.Num("cutSlope"), 12);
        Assert.Equal(2.5, g.Num("fillSlope"), 12);
        Assert.Equal(pad, (string?)((JsonArray)g.Attrs["edges"]!)[0]!["id"]);
        Assert.Null(f.Model.Get(pad)!.NumOrNull("cutSlope"));
        Assert.Null(f.Model.Get(pad)!.NumOrNull("fillSlope"));
        Assert.Equal(otherBefore, f.Model.Get(other)!.Attrs.ToJsonString());
        // The slope from its outline meets the ground: cut on the high side, fill on the low.
        var shape = Gradings.ShapeOf(f.Model, g);
        Assert.NotNull(shape.Surface);
        Assert.Contains(shape.Strips.SelectMany(s => s.Cut), c => c);
        Assert.Contains(shape.Strips.SelectMany(s => s.Cut), c => !c);
        // No slopes, nothing to convert.
        Assert.Throws<CommandException>(() => f.Session.Execute("civil.platformSlopesToGrading", new JsonObject { ["id"] = pad }, out _));
        f.Session.Undo();
        Assert.Equal(1.5, f.Model.Get(pad)!.Num("cutSlope"), 12);
        Assert.Empty(f.Model.OfClass(Gradings.Class));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC4X3")]
    public void Channels_and_kerbs_go_to_IFC_and_come_back(string schema)
    {
        var f = F();
        var t = Field(f, (_, _) => 1.0);
        var drain = (string)f.Run("civil.addChannel", new JsonObject { ["points"] = Pts([5, 5], [15, 5]), ["kind"] = "grated", ["grade"] = 0.005, ["startZ"] = 0.8 })!["id"]!;
        var open = (string)f.Run("civil.addChannel", new JsonObject { ["points"] = Pts([5, 10], [15, 10]), ["kind"] = "u" })!["id"]!;
        var kerb = (string)f.Run("civil.addKerb", new JsonObject { ["points"] = Pts([5, 20], [15, 20], [15, 25]) })!["id"]!;
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "site.ifc", new IfcExportOptions(Schema: schema));
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("IFCDISTRIBUTIONCHAMBERELEMENT(", text);
        if (schema == "IFC4X3") { Assert.Contains("IFCEARTHWORKSCUT(", text); Assert.Contains("IFCKERB(", text); }
        else { Assert.Contains("'Trench'", text); Assert.Contains("'Kerb'", text); Assert.DoesNotContain("IFCKERB(", text); }
        var g = F();
        IfcImport.Run(g.Session, ms.ToArray(), "site.ifc", null, out _);
        foreach (var id in new[] { drain, open })
        {
            var back = g.Model.Get(id);
            Assert.NotNull(back);
            Assert.Equal(Channels.Class, back!.Class);
            Assert.Equal(f.Model.Get(id)!.Str("kind"), back.Str("kind"));
            Assert.Equal(t, back.Str("terrain"));
            Assert.Equal(Vol(f, id), Vol(g, id), 6);
        }
        Assert.Equal(0.005, g.Model.Get(drain)!.Num("grade"), 12);
        var k = g.Model.Get(kerb);
        Assert.NotNull(k);
        Assert.True(KerbLines.Is(k!));
        Assert.Equal(Vol(f, kerb), Vol(g, kerb), 6);
    }

    [Fact]
    public void A_project_without_channels_or_standalone_kerbs_is_as_before()
    {
        var f = F();
        var t = Field(f, (x, _) => 0.02 * x);
        var h = (string)f.Run("civil.addHardscape", new JsonObject { ["points"] = Rect(5, 5, 10, 9), ["type"] = "pavers", ["kerb"] = new JsonObject { ["all"] = true } })!["id"]!;
        // A hardscape's kerb is still its hardscape's, not a standalone one.
        var k = Kerbs.Of(f.Model, h)!;
        Assert.False(KerbLines.Is(k));
        Assert.Equal(18, Q(f, k.Id, "Lapin_KerbQuantities", "Length"), 9);
        Assert.Throws<CommandException>(() => f.Session.Execute("civil.updateKerb", new JsonObject { ["id"] = k.Id, ["height"] = 0.2 }, out _));
        Assert.Empty(f.Model.OfClass(Channels.Class));
        Assert.True(Vol(f, t, "ground") > 0);
    }
}
