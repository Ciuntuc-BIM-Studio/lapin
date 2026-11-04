using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Coordination;

namespace Lapin.Engine.Tests;

/// <summary>
/// The clash work's follow-ups (decision 296): a run's edit builds again only the hosts it crossed or crosses, Lapin's own
/// IFC read back cuts its provision openings once, curved walls and roofs get provision openings, and resolved items of the
/// clash report say how they are now.
/// </summary>
public class ClashFollowUpTests
{
    static string Wall(MepFixture f, double x1, double x2, double y = 1, double? arc = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y, ["x2"] = x2, ["y2"] = y, ["join"] = false };
        if (arc != null) a["arc"] = arc;
        return (string)f.Run("architecture.addWall", a)!["id"]!;
    }

    /// <summary>A straight single heating pipe from (x, −1) to (x, 3) (across a wall at y = 1), its axis 1.0 m up, no insulation.</summary>
    static string Pipe(MepFixture f, double size, double x = 0, string? system = null)
    {
        system ??= f.System(defaults: new JsonObject { ["insulation"] = 0 });
        var run = f.Draw(system, MepFixture.Pts((x, -1), (x, 3)), more: new JsonObject { ["pair"] = false });
        Set(f, run, ("size", size), ("elevation", 1.0));
        return run;
    }

    static Diff Set(MepFixture f, string id, params (string Key, JsonNode? Value)[] values) => f.Session.Edit("set", e =>
    {
        var x = e.Model.Get(id)!;
        var a = x.CloneAttrs();
        foreach (var (k, v) in values) a[k] = v;
        e.Replace(x.With(attrs: a));
    });

    static void AutoCut(MepFixture f, JsonObject? more = null)
    {
        var a = new JsonObject { ["cut"] = true };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        f.Run("cutOrder.setMep", a);
    }

    static List<ProvisionOpening> Provisions(MepFixture f) => Lapin.Platform.Coordination.Provisions.Of(f.Model, f.Session.Catalog);
    static double Volume(MepFixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static HashSet<string> Rendered(Diff d) => d.Render.Select(r => r.NodeId).ToHashSet();
    static JsonObject RunCheck(MepFixture f) => (JsonObject)f.Run("coordination.runCheck", new JsonObject())!;

    // ─── 1. Only the crossed hosts are built again ─────────────────────────

    [Fact]
    public void A_run_edit_builds_again_only_the_hosts_it_crossed_or_crosses()
    {
        var f = new MepFixture();
        AutoCut(f);
        // Six walls along y = 1, ten metres apart: wall i from 10i − 2 to 10i + 3.
        var walls = Enumerable.Range(0, 6).Select(i => Wall(f, 10 * i - 2, 10 * i + 3)).ToList();
        var full = Volume(f, walls[0]);
        var system = f.System(defaults: new JsonObject { ["insulation"] = 0 });
        var pipe = Pipe(f, 0.16, 0, system);
        Assert.True(Volume(f, walls[0]) < full - 1e-4);

        // Bigger: its wall is built again, no other.
        var d = Set(f, pipe, ("size", 0.2));
        var built = Rendered(d);
        Assert.Contains(pipe, built);
        Assert.Contains(walls[0], built);
        Assert.DoesNotContain(walls[1], built);
        Assert.DoesNotContain(walls[5], built);
        Assert.Equal(0.24, Assert.Single(Provisions(f)).Width, 6);

        // Moved to wall 3: the wall it left and the wall it crosses now, no other; the first whole again, the other cut.
        d = Set(f, pipe, ("points", MepFixture.Pts((30, -1), (30, 3))));
        built = Rendered(d);
        Assert.Contains(walls[0], built);
        Assert.Contains(walls[3], built);
        Assert.DoesNotContain(walls.Where((_, i) => i is not (0 or 3)), built.Contains);
        Assert.Equal(full, Volume(f, walls[0]), 6);
        Assert.True(Volume(f, walls[3]) < full - 1e-4);
        Assert.Equal(walls[3], Assert.Single(Provisions(f)).HostId);

        // A new run by wall 5 builds wall 5 alone; deleting the first builds wall 3 alone.
        d = f.Session.Execute("mep.drawRun", new JsonObject { ["systemId"] = system, ["points"] = MepFixture.Pts((50, -1), (50, 3)), ["pair"] = false }, out var made);
        var second = (string)made!["id"]!;
        built = Rendered(d);
        Assert.Contains(walls[5], built);
        Assert.DoesNotContain(walls.Take(5), built.Contains);
        Assert.True(Volume(f, walls[5]) < full - 1e-4);
        d = f.Session.Edit("delete", e => e.Remove(pipe));
        built = Rendered(d);
        Assert.Contains(walls[3], built);
        Assert.DoesNotContain(walls.Where((_, i) => i != 3), built.Contains);
        Assert.Equal(full, Volume(f, walls[3]), 6);
        Assert.Equal(second, Assert.Single(Provisions(f)).SourceId);

        // Undone: the wall is cut again.
        f.Session.Undo();
        Assert.True(Volume(f, walls[3]) < full - 1e-4);
        // A wall's build no longer reads every run: a far wall does not depend on the pipe.
        Assert.DoesNotContain(walls[1], f.Session.Reader.DependentsOf(pipe));
    }

    [Fact]
    public void Many_walls_and_runs_an_edit_of_one_run_stays_where_it_is()
    {
        var f = new MepFixture();
        AutoCut(f);
        var system = f.System(defaults: new JsonObject { ["insulation"] = 0 });
        // 10 rows of 12 walls (120), each row crossed by 4 pipes (40 runs): every pipe crosses one wall.
        var walls = new List<string>();
        for (var row = 0; row < 10; row++)
            for (var k = 0; k < 12; k++)
                walls.Add(Wall(f, 6 * k, 6 * k + 5, 1 + 8 * row));
        var pipes = new List<string>();
        for (var row = 0; row < 10; row++)
            for (var k = 0; k < 4; k++)
            {
                var x = 18 * k + 2.5;
                pipes.Add(f.Draw(system, MepFixture.Pts((x, 8 * row - 1), (x, 8 * row + 3)), more: new JsonObject { ["pair"] = false, ["size"] = 0.1, ["elevation"] = 1.0 }));
            }
        Assert.Equal(40, Provisions(f).Count);

        // One run made bigger: the run and its one wall built again, not 120 walls.
        var watch = Stopwatch.StartNew();
        var d = Set(f, pipes[17], ("size", 0.125));
        var one = watch.Elapsed;
        Assert.Equal(1, walls.Count(Rendered(d).Contains));
        // Every wall built again (the rule changed) takes longer than one run's edit.
        watch.Restart();
        d = f.Session.Execute("cutOrder.setMep", new JsonObject { ["clearance"] = 0.03 }, out _);
        var all = watch.Elapsed;
        Assert.Equal(120, walls.Count(Rendered(d).Contains));
        Assert.True(one < all, $"one run's edit {one.TotalMilliseconds:0} ms, every wall {all.TotalMilliseconds:0} ms");
        Assert.Equal(40, Provisions(f).Count);
    }

    // ─── 2. Lapin's own IFC read back ─────────────────────────────────────

    static string Export(MepFixture f)
    {
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "t.ifc", new IfcExportOptions());
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    static int Count(string ifc, string what) => ifc.Split(what).Length - 1;

    [Fact]
    public void Lapins_own_ifc_read_back_cuts_its_provision_openings_once_and_keeps_them_without_their_run()
    {
        var f = new MepFixture();
        AutoCut(f);
        var wall = Wall(f, -2, 8);
        var slab = f.Id("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = MepFixture.Pts((4, -2), (8, -2), (8, 2), (4, 2)) });
        Pipe(f, 0.16);
        var system = f.System(defaults: new JsonObject { ["insulation"] = 0 });
        var drop = f.Draw(system, MepFixture.Pts((6, -1), (6, 1)), more: new JsonObject { ["pair"] = false });
        Set(f, drop, ("size", 0.1), ("levels", new JsonArray(1.0, -1.0)));
        var provisions = Provisions(f).Select(p => p.Key).Order().ToList();
        Assert.Equal(2, provisions.Count);
        var (wallVolume, slabVolume) = (Volume(f, wall), Volume(f, slab));
        var ifc = Export(f);
        Assert.Equal(2, Count(ifc, "IFCOPENINGELEMENT("));
        var path = Path.Combine(Path.GetTempPath(), $"provisions-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc);
        try
        {
            // With its runs, into a project that cuts: the runs cut them again, none taken as an opening of its own.
            var g = new MepFixture();
            AutoCut(g);
            IfcImport.Run(g.Session, path, null, out _);
            Assert.Empty(g.Model.OfClass(IfcClass.OpeningElement));
            Assert.Equal(provisions, Provisions(g).Select(p => p.Key).Order().ToList());
            Assert.Equal(wallVolume, Volume(g, wall), 6);
            Assert.Equal(slabVolume, Volume(g, slab), 6);
            Assert.Equal(2, Count(Export(g), "IFCOPENINGELEMENT("));

            // Without its runs (walls and floors only): each a real opening of its host, the same size, cut once.
            var h = new MepFixture();
            AutoCut(h);
            var r = IfcImport.Run(h.Session, path, new IfcImportOptions(Kinds: new HashSet<string> { "IfcWall", "IfcSlab" }), out _);
            Assert.Contains(r.Notes, n => n.Contains("kept as openings"));
            Assert.Empty(Provisions(h));
            Assert.Equal(2, h.Model.OfClass(IfcClass.OpeningElement).Count());
            var bare = Assert.Single(Openings.Of(h.Model, wall));
            var placed = Openings.Place(h.Model, bare);
            Assert.Equal("circle", bare.Str("shape"));
            Assert.Equal(0.2, placed.Width, 3);
            Assert.Equal(2, placed.Along, 2);
            Assert.Equal(1.0, placed.Sill + placed.Height / 2, 2);
            Assert.Equal(wallVolume, Volume(h, wall), 3);
            Assert.Equal(slabVolume, Volume(h, slab), 3);
            Assert.Equal(2, Count(Export(h), "IFCOPENINGELEMENT("));

            // Into a project that does not cut: kept as openings too, nothing lost.
            var k = new MepFixture();
            IfcImport.Run(k.Session, path, null, out _);
            Assert.Equal(2, k.Model.OfClass(IfcClass.OpeningElement).Count());
            Assert.Empty(Provisions(k));
            Assert.Equal(wallVolume, Volume(k, wall), 3);
        }
        finally { File.Delete(path); }
    }

    // ─── 3. Curved walls and roofs ────────────────────────────────────────

    [Fact]
    public void A_run_crossing_a_curved_wall_cuts_a_provision_on_the_arc_reported_like_a_straight_one()
    {
        var f = new MepFixture();
        AutoCut(f);
        // An arc from (−3, 1) to (3, 1) bulging 1 m.
        var wall = Wall(f, -3, 3, 1, arc: 1);
        var full = Volume(f, wall);
        var curve = PathCurve.Of(f.Model.Get(wall)!);
        var apex = curve.PointAt(curve.Length / 2);
        var system = f.System(defaults: new JsonObject { ["insulation"] = 0 });
        // Square through the arc at its middle: round, its centre on the arc, 1.0 m up.
        var straight = f.Draw(system, MepFixture.Pts((0, apex.Y - 2), (0, apex.Y + 2)), more: new JsonObject { ["pair"] = false, ["size"] = 0.11, ["elevation"] = 1.0 });
        var p = Assert.Single(Provisions(f));
        Assert.Equal((straight, wall, OpeningVoids.Wall, OpeningVoids.Round), (p.SourceId, p.HostId, p.Kind, p.Shape));
        Assert.Equal(0.15, p.Width, 6);
        var box = Measure.Bounds(p.Void)!.Value;
        Assert.Equal(apex.X, (box.Min.X + box.Max.X) / 2, 2);
        Assert.Equal(apex.Y, (box.Min.Y + box.Max.Y) / 2, 2);
        Assert.Equal(1.0, (box.Min.Z + box.Max.Z) / 2, 3);
        Assert.True(Volume(f, wall) < full - 1e-4);

        // At a slant, 2 m along the arc: a rectangle there (its width along the arc wider than the pipe), between radial sides.
        var at = curve.PointAt(curve.Length / 2 + 2);
        var normal = curve.TangentAt(curve.Length / 2 + 2).Perp;
        var slant = new Vec2(normal.X * 0.8 + normal.Y * 0.6, -normal.X * 0.6 + normal.Y * 0.8);
        var a = at - slant * 1.5;
        var b = at + slant * 1.5;
        var oblique = f.Draw(system, MepFixture.Pts((a.X, a.Y), (b.X, b.Y)), more: new JsonObject { ["pair"] = false, ["size"] = 0.11, ["elevation"] = 1.0 });
        var q = Assert.Single(Provisions(f), x => x.SourceId == oblique);
        Assert.Equal(OpeningVoids.Rectangle, q.Shape);
        Assert.True(q.Width > 0.11 / 0.8, $"{q.Width}");
        var qb = Measure.Bounds(q.Void)!.Value;
        Assert.True((new Vec2((qb.Min.X + qb.Max.X) / 2, (qb.Min.Y + qb.Max.Y) / 2) - at).Length < 0.05);
        // Both fit: the clash check has nothing open, two fits.
        var report = RunCheck(f);
        Assert.Equal(0, (int)report["counts"]!["open"]!);
        Assert.Equal(2, (int)report["counts"]!["ok"]!);
        // A load-bearing curved wall is held and reported, as a straight one.
        Set(f, wall, ("params", new JsonArray(new JsonObject { ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true })));
        Assert.All(Provisions(f), x => Assert.Equal(ProvisionOpening.Structural, x.Held));
        Assert.Equal(full, Volume(f, wall), 6);
        report = RunCheck(f);
        Assert.Equal(2, (int)report["counts"]!["held"]!);
    }

    [Fact]
    public void A_run_through_a_roof_cuts_its_slope_upright_or_along_its_travel_and_goes_out_to_ifc()
    {
        var f = new MepFixture();
        AutoCut(f);
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6)) })!["id"]!;
        var full = Volume(f, roof);
        var system = f.System(defaults: new JsonObject { ["insulation"] = 0 });
        // A vent pipe standing up through the slope at (2, 0.8), clear of the purlins under it: a round hole, square to the plan.
        var vent = f.Draw(system, MepFixture.Pts((2, 0.8), (2.00002, 0.8)), more: new JsonObject { ["pair"] = false, ["size"] = 0.11 });
        Set(f, vent, ("levels", new JsonArray(0.5, 12.0)));
        var p = Assert.Single(Provisions(f));
        Assert.Equal((roof, OpeningVoids.Roof, OpeningVoids.Round), (p.HostId, p.Kind, p.Shape));
        Assert.Equal(0.15, p.Width, 6);
        var box = Measure.Bounds(p.Void)!.Value;
        Assert.Equal(2, (box.Min.X + box.Max.X) / 2, 2);
        Assert.Equal(0.8, (box.Min.Y + box.Max.Y) / 2, 2);
        // On the slope: the void stands higher up the slope than down it (its top and bottom follow the pitch).
        Assert.True(box.Max.Z - box.Min.Z > p.Depth + 0.05);
        Assert.True(Volume(f, roof) < full - 1e-5);

        // At a slant through the slope: a rectangle drawn out along its travel.
        var duct = f.Draw(system, MepFixture.Pts((7, 0.3), (7, 1.3)), more: new JsonObject { ["pair"] = false, ["size"] = 0.11 });
        Set(f, duct, ("levels", new JsonArray(0.5, 12.0)));
        var q = Assert.Single(Provisions(f), x => x.SourceId == duct);
        Assert.Equal((OpeningVoids.Roof, OpeningVoids.Rectangle), (q.Kind, q.Shape));
        Assert.True(q.Width > q.Height);

        // Reported like the others: both fit; without the rule, two with no opening.
        var report = RunCheck(f);
        Assert.Equal(0, (int)report["counts"]!["open"]!);
        Assert.Equal(2, (int)report["counts"]!["ok"]!);
        // The IFC export writes them as openings of the roof.
        var ifc = Export(f);
        Assert.Contains($"IFCOPENINGELEMENT('{p.GlobalId}'", ifc);
        Assert.Contains($"IFCOPENINGELEMENT('{q.GlobalId}'", ifc);
        f.Run("cutOrder.setMep", new JsonObject { ["cut"] = false });
        Assert.Equal(full, Volume(f, roof), 6);
        report = RunCheck(f);
        Assert.Equal(2, (int)report["counts"]!["missing"]!);
    }

    // ─── 4. Resolved items say how they are now ───────────────────────────

    [Fact]
    public void A_resolved_item_says_at_which_run_and_how_the_opening_and_the_run_are_now()
    {
        var f = new MepFixture();
        Wall(f, -2, 8);
        var pipe = Pipe(f, 0.16);
        RunCheck(f);
        var first = Assert.Single(ClashReports.Read(f.Model)!.Items);
        Assert.StartsWith("No opening", first.Message);
        Assert.Null(first.ResolvedAt);

        // Made from the report, then run again: resolved at this run, the opening and the run's sizes in the message.
        f.Run("coordination.fix", new JsonObject { ["numbers"] = new JsonArray(1) });
        RunCheck(f);
        var report = ClashReports.Read(f.Model)!;
        var item = Assert.Single(report.Items);
        Assert.Equal(ClashReports.Fixed, item.State);
        Assert.Equal(report.Date, item.ResolvedAt);
        Assert.Matches(@"^Resolved at the run of \d\d\.\d\d\.\d{4} \d\d:\d\d: opening V1 200 mm, run 160 mm\.$", item.Message);
        Assert.Equal(0, item.By);
        Assert.NotNull(item.OpeningId);
        var resolvedAt = item.ResolvedAt;

        // The pipe bigger: the next run says its new size, still resolved at the run it was first found so.
        Set(f, pipe, ("size", 0.18));
        RunCheck(f);
        item = Assert.Single(ClashReports.Read(f.Model)!.Items);
        Assert.Equal((ClashReports.Fixed, resolvedAt), (item.State, item.ResolvedAt));
        Assert.EndsWith("opening V1 200 mm, run 180 mm.", item.Message);
        var json = (JsonObject)f.Session.Query("coordination.report", new JsonObject())!;
        Assert.Equal(resolvedAt, (string?)json["items"]![0]!["resolvedAt"]);

        // Too big for it: open again, its own message; then gone: resolved, the message says so.
        Set(f, pipe, ("size", 0.25));
        RunCheck(f);
        item = Assert.Single(ClashReports.Read(f.Model)!.Items);
        Assert.Equal(ClashReports.Open, item.State);
        Assert.Null(item.ResolvedAt);
        Assert.StartsWith("Opening too small", item.Message);
        f.Session.Edit("delete", e => e.Remove(pipe));
        RunCheck(f);
        item = Assert.Single(ClashReports.Read(f.Model)!.Items);
        Assert.Equal(ClashReports.Fixed, item.State);
        Assert.EndsWith(": the MEP element is gone.", item.Message);
    }

    [Fact]
    public void An_item_resolved_by_the_cut_order_says_the_provision_it_goes_through()
    {
        var f = new MepFixture();
        Wall(f, -2, 8);
        var pipe = Pipe(f, 0.1);
        RunCheck(f);
        AutoCut(f);
        RunCheck(f);
        var item = Assert.Single(ClashReports.Read(f.Model)!.Items);
        Assert.Equal(ClashReports.Fixed, item.State);
        Assert.Matches(@": opening V1 140 mm, run 100 mm\.$", item.Message);
        // Moved off the wall: it no longer goes through it.
        Set(f, pipe, ("points", MepFixture.Pts((-1, 0), (6, 0))));
        RunCheck(f);
        item = Assert.Single(ClashReports.Read(f.Model)!.Items);
        Assert.Contains(" no longer goes through ", item.Message);
    }
}
