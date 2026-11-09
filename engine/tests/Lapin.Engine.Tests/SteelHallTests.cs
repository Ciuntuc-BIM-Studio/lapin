using System.Text.Json.Nodes;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>Steel, stage 2 (decision 270): the portal frame hall configurator — frames with haunches, gables, the grid, regeneration, the preview, IFC.</summary>
public class SteelHallTests
{
    static Fixture F() => new(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());
    static double Tan(double deg) => Math.Tan(deg * Math.PI / 180);

    /// <summary>A hall as stage 2 and 3 built it: its secondary steel off (decision 275; SteelSecondaryTests).</summary>
    static string Hall(Fixture f, JsonObject? attrs = null, string? style = null)
    {
        var o = attrs ?? [];
        if (!o.ContainsKey("secondary")) o["secondary"] = false;
        var a = new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = o };
        if (style != null) a["style"] = style;
        return (string)f.Run("steel.addHall", a)!["id"]!;
    }

    /// <summary>A hall's fields without its connections: the members' ends as stage 2 built them, before the connections set them back (SteelNodeTests).</summary>
    static JsonObject Bare(JsonObject? attrs = null) { var o = attrs ?? []; o["nodes"] = false; return o; }

    static void Update(Fixture f, string hall, JsonObject attrs) => f.Run("steel.updateHall", new JsonObject { ["id"] = hall, ["attrs"] = attrs });

    static Dictionary<string, Entity> Members(Fixture f, string hall) => Halls.Members(f.Model, hall);

    static IEnumerable<Vec3> Points(Fixture f, string id, string key = "body") => f.Session.Geometry(id)!.Parts.Where(p => p.Key == key).SelectMany(p => p.Solid.Vertices);

    static double[] A(Vec3 v) => [v.X, v.Y, v.Z];
    static double[] A2(Vec2 v) => [v.X, v.Y];
    static JsonArray J(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);

    // ------------------------------------------------------------ the frame

    [Fact]
    public void A_frame_has_its_eaves_and_apex_from_the_span_the_height_and_the_pitch()
    {
        var s = HallSpec.Default;
        // IPE 400 columns: the rafters' top 6 m over the column's outer face, 0.2 m out from its axis.
        Assert.Equal(6 + 0.2 * Tan(10), s.Top(0), 9);
        Assert.Equal(6 + 10.2 * Tan(10), s.ApexHeight, 9);
        Assert.Equal(s.Top(0), s.Top(20), 9);
        var mono = s with { Roof = "mono", Pitch = 5 };
        Assert.Equal(6 + 20.2 * Tan(5), mono.Top(20), 9);
        Assert.Equal(6 + 20.4 * Tan(5), mono.ApexHeight, 9);

        var f = F();
        var hall = Hall(f, Bare());
        var m = Members(f, hall);
        // A typical frame: the rafters' axes on their top, from the columns' axes to the apex, the columns as tall as the rafters' top over them.
        var raf = m["f1:raf:a"];
        Assert.Equal([0, 6, 6 + 0.2 * Tan(10)], A(MemberAxes.Point(raf, "start")), new Tol(1e-9));
        Assert.Equal([10, 6, 6 + 10.2 * Tan(10)], A(MemberAxes.Point(raf, "end")), new Tol(1e-9));
        Assert.Equal(6 + 0.2 * Tan(10), m["f1:col:a"].Num("height"), 9);
        // The column's top follows the slope: 6 m at its outer face, the rafters' top at its inner face.
        var col = Points(f, m["f1:col:a"].Id).ToList();
        Assert.Equal(6, col.Where(p => p.X < -0.199).Max(p => p.Z), 6);
        Assert.Equal(6 + 0.4 * Tan(10), col.Where(p => p.X > 0.199).Max(p => p.Z), 6);
        // The rafter is fitted to the column's inner face and mitred plumb at the apex.
        var r = Points(f, raf.Id).ToList();
        Assert.Equal(0.2, r.Min(p => p.X), 6);
        Assert.Equal(10, r.Max(p => p.X), 6);
        Assert.Equal(6 + 10.2 * Tan(10), r.Max(p => p.Z), 6);
        var b = Points(f, m["f1:raf:b"].Id).ToList();
        Assert.Equal(10, b.Min(p => p.X), 6);
        Assert.Equal(19.8, b.Max(p => p.X), 6);
    }

    [Fact]
    public void A_mono_pitch_frame_runs_one_rafter_from_the_low_column_to_the_high_one()
    {
        var f = F();
        var hall = Hall(f, Bare(), "lean-to");
        var m = Members(f, hall);
        Assert.False(m.ContainsKey("f1:raf:a"));
        var raf = m["f1:raf"];
        var hc = Section.Catalogue["HEA 240"].Depth;
        Assert.Equal(4 + hc / 2 * Tan(5), MemberAxes.Point(raf, "start").Z, 9);
        Assert.Equal(4 + (10 + hc / 2) * Tan(5), MemberAxes.Point(raf, "end").Z, 9);
        Assert.Equal(4 + (10 + hc / 2) * Tan(5), m["f1:col:b"].Num("height"), 9);
        var r = Points(f, raf.Id).ToList();
        Assert.Equal(hc / 2, r.Min(p => p.X), 6);
        Assert.Equal(10 - hc / 2, r.Max(p => p.X), 6);
        // The high column's top rises outwards with the roof: the rafters' top over its outer face.
        var col = Points(f, m["f1:col:b"].Id).ToList();
        Assert.Equal(4 + (10 + hc) * Tan(5), col.Max(p => p.Z), 6);
        // Haunches under both ends: the eaves' at either column.
        var (hs, he) = Haunch.Of(raf);
        Assert.NotNull(hs);
        Assert.NotNull(he);
    }

    // ------------------------------------------------------------ haunches

    /// <summary>A tapered T of legs L and D: its flange inside the sloping edge, its web the triangle left (alike, scaled by k).</summary>
    static (double Web, double Flange) Expected(double l, double d, double tw, double tf, double bf)
    {
        var h = Math.Sqrt(l * l + d * d);
        var k = 1 - tf * h / (l * d);
        return (tw * k * k * l * d / 2, bf * (1 - k * k) * l * d / 2);
    }

    [Fact]
    public void A_haunch_cut_from_the_section_takes_its_web_and_flange_and_one_of_plates_its_own()
    {
        var f = F();
        var type = (string)f.Run("steel.createType", new JsonObject { ["section"] = "IPE 360" })!["id"]!;
        var id = (string)f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = 0, ["y1"] = 0, ["z1"] = 5, ["x2"] = 8, ["y2"] = 0, ["z2"] = 5, ["insertion"] = "top" })!["id"]!;
        f.Run("steel.setHaunch", new JsonObject { ["id"] = id, ["end"] = "start", ["haunch"] = new JsonObject { ["length"] = 2, ["depth"] = 0.36, ["kind"] = "cut" } });
        f.Run("steel.setHaunch", new JsonObject { ["id"] = id, ["end"] = "end", ["haunch"] = new JsonObject { ["length"] = 1.5, ["depth"] = 0.3, ["kind"] = "plates", ["web"] = 0.01, ["flange"] = 0.015, ["width"] = 0.2 } });
        var g = f.Session.Geometry(id)!;
        double V(string key) => Measure.Volume(g.Parts.Single(p => p.Key == key).Solid);
        var ipe = Section.Catalogue["IPE 360"];
        var (cw, cf) = Expected(2, 0.36, ipe.Web, ipe.Flange, ipe.Width);
        Assert.Equal(cw, V("haunch:start:web"), 9);
        Assert.Equal(cf, V("haunch:start:flange"), 9);
        var (pw, pf) = Expected(1.5, 0.3, 0.01, 0.015, 0.2);
        Assert.Equal(pw, V("haunch:end:web"), 9);
        Assert.Equal(pf, V("haunch:end:flange"), 9);
        // Under the member: from its underside (the axis on its top) down the depth, at the end.
        var start = g.Parts.Where(p => p.Key!.StartsWith("haunch:start", StringComparison.Ordinal)).SelectMany(p => p.Solid.Vertices).ToList();
        Assert.Equal(5 - 0.36 - 0.36, start.Min(p => p.Z), 9);
        Assert.Equal(5 - 0.36, start.Max(p => p.Z), 9);
        Assert.Equal(2, start.Max(p => p.X), 9);
        // The member's own quantities leave its haunches out.
        var qto = f.Session.Quantities(id).Single(q => q.Name == "Qto_MemberBaseQuantities");
        Assert.Equal(ipe.Area * 8, qto.Quantities.Single(q => q.Name == "NetVolume").Value, 6);
        // Set on their own: kept; null takes one off.
        Assert.True(Haunch.Of(f.Model.Get(id)!).Start!.Own);
        f.Run("steel.setHaunch", new JsonObject { ["id"] = id, ["end"] = "start", ["haunch"] = null });
        Assert.Null(Haunch.Of(f.Model.Get(id)!).Start);
        Assert.Throws<CommandException>(() => f.Run("steel.setHaunch", new JsonObject { ["id"] = id, ["end"] = "start", ["haunch"] = new JsonObject { ["length"] = 9, ["depth"] = 0.3 } }));
    }

    [Fact]
    public void A_hall_puts_its_haunches_kind_under_its_rafters_and_keeps_one_set_on_its_own()
    {
        var f = F();
        var hall = Hall(f, Bare(new JsonObject { ["eavesHaunch"] = new JsonObject { ["length"] = 2, ["depth"] = 0.36, ["kind"] = "plates", ["web"] = 0.012, ["flange"] = 0.015 } }));
        var raf = Members(f, hall)["f2:raf:a"];
        var (eaves, apex) = Haunch.Of(raf);
        Assert.Equal("plates", eaves!.Kind);
        Assert.Equal("cut", apex!.Kind);
        Assert.Equal(1, apex.Length);
        // The haunch at the eaves stands on the column's face, under the rafter.
        var pts = Points(f, raf.Id, "haunch:start:flange").ToList();
        Assert.Equal(0.2, pts.Min(p => p.X), 6);
        f.Run("steel.setHaunch", new JsonObject { ["id"] = raf.Id, ["end"] = "end", ["haunch"] = new JsonObject { ["length"] = 1.2, ["depth"] = 0.2, ["kind"] = "plates" } });
        Update(f, hall, new JsonObject { ["apexHaunch"] = new JsonObject { ["length"] = 0.8, ["depth"] = 0.15 } });
        var after = Members(f, hall);
        Assert.Equal(1.2, Haunch.Of(after["f2:raf:a"]).End!.Length);
        Assert.Equal(0.8, Haunch.Of(after["f2:raf:b"]).End!.Length);
        // The gable rafters of a gable of posts have none.
        Assert.Equal((null, null), Haunch.Of(after["f0:raf:a"]));
    }

    // ------------------------------------------------------------ gables, bays, the grid

    [Fact]
    public void A_gable_is_posts_under_a_simple_rafter_or_a_regular_frame()
    {
        var f = F();
        var hall = Hall(f, Bare());
        var m = Members(f, hall);
        // Five bays: six frames, four regular (2 columns, 2 rafters), two gables of posts (2 corner posts, 3 posts at 5 m, 2 rafters).
        Assert.Equal(4 * 4 + 2 * 7, m.Count);
        Assert.Equal([5.0, 10, 15], HallSpec.Default.PostStations);
        var post = m["f0:post:2"];
        Assert.Equal(IfcClass.Column, post.Class);
        Assert.Equal("IPE 240", Section.From(f.Model.RelatingOf(post.Id, IfcClass.RelDefinesByType)!.Attrs["section"])!.Name);
        // A post fitted to the underside of the rafter over it (at the apex, under rafter a).
        var under = 6 + 10.2 * Tan(10) - 0.24 / Math.Cos(10 * Math.PI / 180);
        Assert.Equal(under, Points(f, post.Id).Where(p => Math.Abs(p.X - 10) < 0.06).Max(p => p.Z), 3);
        // The gable rafter runs to the walls' outer line, cut plumb.
        var gr = Points(f, m["f0:raf:a"].Id).ToList();
        Assert.Equal(-0.2, gr.Min(p => p.X), 6);
        Assert.Equal(-0.2, gr.Where(p => p.Z < 5.9).Min(p => p.X), 6);

        Update(f, hall, new JsonObject { ["gable"] = "frame" });
        m = Members(f, hall);
        Assert.Equal(6 * 4, m.Count);
        Assert.False(m.ContainsKey("f0:post:1"));
        Assert.NotNull(Haunch.Of(m["f0:raf:a"]).Start);
    }

    [Fact]
    public void Bays_listed_place_the_frames_and_the_grid_axes()
    {
        var f = F();
        var hall = Hall(f, new JsonObject { ["bays"] = J(6, 7.5, 6), ["x"] = 10, ["y"] = 5, ["angle"] = 90 });
        var m = Members(f, hall);
        // Turned 90°: the span along y, the hall along -x.
        Assert.Equal([10.0, 5], A2(Args.TryPoint(m["f0:col:a"].Attrs["at"])!.Value), new Tol(1e-9));
        Assert.Equal([10.0, 25], A2(Args.TryPoint(m["f0:col:b"].Attrs["at"])!.Value), new Tol(1e-9));
        Assert.Equal([10 - 13.5, 5], A2(Args.TryPoint(m["f2:col:a"].Attrs["at"])!.Value), new Tol(1e-9));
        var grid = f.Model.Get((string)f.Model.Get(hall)!.Attrs["gridId"]!)!;
        var axes = Grids.Axes(grid);
        Assert.Equal(["1", "2", "3", "4", "A", "B"], axes.Select(a => a.Tag));
        Assert.Equal(10 - 13.5, axes.Single(a => a.Tag == "3").A.X, 9);
        Assert.Equal(5 - 1.5, axes.Single(a => a.Tag == "3").A.Y, 9);
        Assert.Equal(25, axes.Single(a => a.Tag == "B").A.Y, 9);
        Assert.Equal(10 - 19.5 - 1.5, axes.Single(a => a.Tag == "B").B.X, 9);
        // The column's web in the frame's plane: its depth across the span (along y here).
        var col = Points(f, m["f1:col:a"].Id).ToList();
        Assert.Equal(0.4, col.Max(p => p.Y) - col.Min(p => p.Y), 6);
    }

    [Fact]
    public void A_hall_refuses_what_does_not_fit()
    {
        var f = F();
        Assert.Throws<CommandException>(() => Hall(f, new JsonObject { ["span"] = 1 }));
        Assert.Throws<CommandException>(() => Hall(f, new JsonObject { ["eavesHaunch"] = new JsonObject { ["length"] = 9, ["depth"] = 0.3 } }));
        Assert.Throws<CommandException>(() => Hall(f, new JsonObject { ["column"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } }));
        Assert.Throws<CommandException>(() => Hall(f, new JsonObject { ["bracing"] = J(9) }));
        Assert.Empty(f.Model.OfClass(Halls.Class));
        var p = (JsonObject)f.Session.Query("steel.hallPreview", new JsonObject { ["attrs"] = new JsonObject { ["span"] = 1 } })!;
        Assert.Contains("span", (string)p["problem"]!);
    }

    // ------------------------------------------------------------ regeneration

    [Fact]
    public void A_hall_changed_keeps_its_members_ids_and_drops_or_adds_those_its_frames_need()
    {
        var f = F();
        var hall = Hall(f);
        var before = Members(f, hall).ToDictionary(x => x.Key, x => x.Value.Id);
        var grid = (string)f.Model.Get(hall)!.Attrs["gridId"]!;
        Update(f, hall, new JsonObject { ["span"] = 24, ["postSpacing"] = 6, ["eaves"] = 7, ["rafter"] = "IPE 400" });
        var after = Members(f, hall);
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        Assert.All(after, x => Assert.Equal(before[x.Key], x.Value.Id));
        Assert.Equal(24, Args.TryPoint(after["f3:col:b"].Attrs["at"])!.Value.X, 9);
        Assert.Equal("IPE 400", Section.From(f.Model.RelatingOf(after["f3:raf:a"].Id, IfcClass.RelDefinesByType)!.Attrs["section"])!.Name);
        Assert.Equal(grid, (string)f.Model.Get(hall)!.Attrs["gridId"]!);
        // Two bays fewer: the last frames' members go, the gable moves to the new last frame (its own posts), the rest stay.
        Update(f, hall, new JsonObject { ["bays"] = J(6, 6, 6) });
        after = Members(f, hall);
        Assert.Equal(2 * 4 + 2 * 7, after.Count);
        Assert.Null(f.Model.Get(before["f5:col:a"]));
        Assert.Equal(before["f1:raf:a"], after["f1:raf:a"].Id);
        Assert.Equal(before["f3:col:a"], after["f3:col:a"].Id);
        Assert.Equal(4, Grids.Axes(f.Model.Get(grid)!).Count(a => char.IsDigit(a.Tag[0])));
        // Mono-pitch: one rafter a frame, the others gone.
        Update(f, hall, new JsonObject { ["roof"] = "mono", ["apexHaunch"] = null });
        after = Members(f, hall);
        Assert.Contains("f1:raf", after.Keys);
        Assert.DoesNotContain("f1:raf:b", after.Keys);
        // One undo step each: back to duo-pitch.
        f.Session.Undo();
        Assert.Contains("f1:raf:b", Members(f, hall).Keys);
    }

    [Fact]
    public void A_hall_is_added_in_one_step_and_undone_in_one()
    {
        var f = F();
        var count = f.Model.Entities.Count();
        Hall(f);
        Assert.True(f.Model.Entities.Count() > count + 30);
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(Halls.Class));
        Assert.Empty(f.Model.OfClass(SteelModule.Member));
        Assert.Empty(f.Model.OfClass(IfcClass.Grid));
    }

    // ------------------------------------------------------------ preview, styles

    [Fact]
    public void The_preview_weighs_what_the_hall_builds()
    {
        var f = F();
        var p = (JsonObject)f.Session.Query("steel.hallPreview", new JsonObject { ["attrs"] = new JsonObject { ["secondary"] = false } })!;
        Assert.Null(p["problem"]);
        var sum = p["summary"]!;
        Assert.Equal(6, (int)sum["frames"]!);
        Assert.Equal(6 + 10.2 * Tan(10), (double)sum["apex"]!, 3);
        Assert.Equal(8, (int)sum["columns"]!);
        Assert.Equal(10, (int)sum["posts"]!);
        Assert.Equal(12, (int)sum["rafters"]!);
        // The same weights as the hall built in the model.
        var hall = Hall(f);
        double kg(Func<string, bool> role, Func<string, bool> part) => Members(f, hall).Where(x => role(x.Key))
            .SelectMany(x => f.Session.Geometry(x.Value.Id)!.Parts.Where(g => part(g.Key ?? "")).Select(g => Measure.Volume(g.Solid) * (g.Key == "body" ? MemberAxes.Of(f.Model, x.Value)!.Section.RealRatio : 1))).Sum() * SteelModule.SteelDensity;
        var w = sum["weights"]!;
        Assert.Equal(kg(r => r.Contains(":raf"), k => k == "body"), (double)w["rafter"]!, 0.1);
        Assert.Equal(kg(r => r.Contains(":raf"), k => k.StartsWith("haunch", StringComparison.Ordinal)), (double)w["haunch"]!, 0.1);
        Assert.Equal(kg(r => r.Contains(":col") && r is not ("f0:col:a" or "f0:col:b" or "f5:col:a" or "f5:col:b"), _ => true), (double)w["column"]!, 0.1);
        // The connections' plates, bolts and anchors weighed too, and in the total.
        var nodes = Halls.Nodes(f.Model, hall).Values.SelectMany(n => f.Session.Geometry(n.Id)!.Parts).Sum(g => Measure.Volume(g.Solid)) * SteelModule.SteelDensity;
        Assert.True(nodes > 0);
        Assert.Equal(nodes, (double)w["node"]!, 0.1);
        Assert.Equal(kg(_ => true, _ => true) + nodes, (double)w["total"]!, 0.1);
        Assert.Equal((double)w["total"]! / 600, (double)sum["perArea"]!, 2);
        // A typical frame's elevation: two columns, two rafters, four haunches of two pieces each; a gable's of posts.
        Assert.Equal(12, p["frame"]!.AsArray().Count);
        Assert.Equal(7, p["gable"]!.AsArray().Count);
        Assert.Equal(8, p["axes"]!.AsArray().Count);
    }

    [Fact]
    public void Project_hall_styles_are_saved_renamed_and_deleted_and_their_halls_follow()
    {
        var f = F();
        var styles = f.Session.Catalog.Catalogs["steel.hallStyles"]!.AsArray();
        Assert.Equal(["hall-20", "hall-30", "lean-to"], styles.Select(s => (string)s!["id"]!));
        var hall = Hall(f, null, "hall-30");
        Assert.Equal(30, Halls.SpecOf(f.Model.Get(hall)!).Span);
        Assert.Equal("hall-30", f.Model.Get(hall)!.Str("style"));
        var sid = (string)f.Run("steel.saveHallStyle", new JsonObject { ["id"] = hall, ["name"] = "Workshop", ["attrs"] = new JsonObject { ["eaves"] = 9 } })!["styleId"]!;
        Assert.StartsWith("own:", sid);
        Assert.Equal(sid, f.Model.Get(hall)!.Str("style"));
        // A second hall of the style, elsewhere; the style saved again with 10 m eaves: both follow, each where it stands.
        var second = Hall(f, new JsonObject { ["x"] = 50 }, sid);
        Assert.Equal(9, Halls.SpecOf(f.Model.Get(second)!).Eaves);
        f.Run("steel.saveHallStyle", new JsonObject { ["id"] = second, ["styleId"] = sid, ["name"] = "Workshop", ["attrs"] = new JsonObject { ["eaves"] = 10 } });
        Assert.Equal(10, Halls.SpecOf(f.Model.Get(hall)!).Eaves);
        Assert.Equal(50, Halls.SpecOf(f.Model.Get(second)!).X);
        Assert.Equal(0, Halls.SpecOf(f.Model.Get(hall)!).X);
        f.Run("steel.renameHallStyle", new JsonObject { ["styleId"] = sid, ["name"] = "Shed" });
        Assert.Equal("Shed", (string)HallStyles.Own(f.Model).Single()["label"]!);
        f.Run("steel.deleteHallStyle", new JsonObject { ["styleId"] = sid });
        Assert.Empty(HallStyles.Own(f.Model));
        Assert.Null(f.Model.Get(hall)!.Str("style"));
        Assert.Equal(10, Halls.SpecOf(f.Model.Get(hall)!).Eaves);
    }

    // ------------------------------------------------------------ IFC

    [Fact]
    public void A_hall_exports_as_a_building_system_its_rafters_assemblies_with_their_haunches()
    {
        var f = F();
        var hall = Hall(f, new JsonObject
        {
            ["bays"] = J(6, 6), ["gable"] = "frame", ["nodes"] = false,
            ["apexHaunch"] = new JsonObject { ["length"] = 1, ["depth"] = 0.2, ["kind"] = "plates", ["web"] = 0.01, ["flange"] = 0.015 },
        });
        var path = Path.Combine(Path.GetTempPath(), $"hall-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            var system = Assert.Single(step.OfType("IFCBUILDINGSYSTEM"));
            Assert.Equal(new StepEnum("LOADBEARING"), system[5]);
            // Three frames: six columns and six rafters, each rafter an assembly of itself and its haunches.
            Assert.Equal(6, step.OfType("IFCCOLUMN").Count());
            Assert.Equal(6, step.OfType("IFCELEMENTASSEMBLY").Count());
            // The eaves haunch cut from the rafter (one member), the apex one of two plates.
            Assert.Equal(6 + 6, step.OfType("IFCMEMBER").Count());
            Assert.Equal(12, step.OfType("IFCPLATE").Count());
            Assert.Contains(step.OfType("IFCSHAPEREPRESENTATION"), r => r[2] is string s && s == "Clipping");
            Assert.Single(step.OfType("IFCGRID"));
            Assert.Contains(step.OfType("IFCPROPERTYSET"), p => p[2] is string s && s == "Lapin_SteelHall");

            // Read back: a haunch's plates and piece, the volumes Lapin built.
            var model = new IfcModel(step);
            var booleans = f.Session.Catalog.Get<ISolidBooleans>()!;
            var raf = Members(f, hall)["f1:raf:a"];
            var g = f.Session.Geometry(raf.Id)!;
            var cut = g.Parts.Where(p => p.Key!.StartsWith("haunch:start", StringComparison.Ordinal)).Sum(p => Measure.Volume(p.Solid));
            var plates = g.Parts.Where(p => p.Key!.StartsWith("haunch:end", StringComparison.Ordinal)).Sum(p => Measure.Volume(p.Solid));
            var products = model.Products.Where(p => p.Tag?.StartsWith(raf.Id + "/haunch", StringComparison.Ordinal) == true).ToList();
            double Read(Func<string, bool> tag) => products.Where(p => tag(p.Tag!)).Sum(p => model.Shape(p.Id, booleans).Body.Sum(b => Math.Abs(MeshVolume(b.Mesh))));
            Assert.Equal(cut, Read(t => t.EndsWith("haunch:start", StringComparison.Ordinal)), 7);
            Assert.Equal(plates, Read(t => t.Contains("haunch:end:", StringComparison.Ordinal)), 7);
        }
        finally { File.Delete(path); }
    }

    static double MeshVolume(MeshBuilder mesh) => mesh.T.Sum(t => Vec3.Dot(mesh.V[t.A], Vec3.Cross(mesh.V[t.B], mesh.V[t.C]))) / 6;
}

/// <summary>Doubles equal within a tolerance.</summary>
sealed class Tol(double eps) : IEqualityComparer<double>
{
    public bool Equals(double a, double b) => Math.Abs(a - b) <= eps;
    public int GetHashCode(double v) => 0;
}
