using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Mep;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Cde.Remarks;
using Lapin.Platform.Coordination;

namespace Lapin.Engine.Tests;

/// <summary>The openings-vs-MEP check run on demand as a report, fixed from it, and MEP runs that cut their hosts by the cut order (decision 293).</summary>
public class ClashReportTests
{
    /// <summary>A wall along y = 1 from x = −2 to 8 (x = 0 is 2 m along it).</summary>
    static string Wall(MepFixture f) =>
        (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = -2, ["y1"] = 1, ["x2"] = 8, ["y2"] = 1, ["join"] = false })!["id"]!;

    /// <summary>A straight single heating pipe across the wall at x (0 by default), its axis 1.0 m up, of the diameter asked, no insulation.</summary>
    static string Pipe(MepFixture f, double size, double x = 0)
    {
        var system = f.System(defaults: new JsonObject { ["insulation"] = 0 });
        var run = f.Draw(system, MepFixture.Pts((x, -1), (x, 3)), more: new JsonObject { ["pair"] = false });
        Set(f, run, ("size", size), ("elevation", 1.0));
        return run;
    }

    static void Set(MepFixture f, string id, params (string Key, JsonNode? Value)[] values) => f.Session.Edit("set", e =>
    {
        var x = e.Model.Get(id)!;
        var a = x.CloneAttrs();
        foreach (var (k, v) in values) a[k] = v;
        e.Replace(x.With(attrs: a));
    });

    static ClashReport Report(MepFixture f) => ClashReports.Read(f.Model)!;
    static JsonObject RunCheck(MepFixture f) => (JsonObject)f.Run("coordination.runCheck", new JsonObject())!;
    static List<JsonObject> Findings(MepFixture f) =>
        ((JsonArray)((JsonObject)f.Session.Query("cde.check", new JsonObject())!)["findings"]!).OfType<JsonObject>().Where(x => (string?)x["rule"] == "openingsMep").ToList();
    static List<ProvisionOpening> Provisions(MepFixture f) => Lapin.Platform.Coordination.Provisions.Of(f.Model, f.Session.Catalog);
    static double Volume(MepFixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static void AutoCut(MepFixture f, JsonObject? more = null)
    {
        var a = new JsonObject { ["cut"] = true };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        f.Run("cutOrder.setMep", a);
    }

    [Fact]
    public void The_check_runs_only_when_asked_and_keeps_its_report_until_run_again()
    {
        var f = new MepFixture();
        var wall = Wall(f);
        var pipe = Pipe(f, 0.16);
        // Nothing has run it: no report, and Manage → Check says so instead of running it.
        Assert.Null(ClashReports.Read(f.Model));
        Assert.Null(f.Session.Query("coordination.report", new JsonObject()));
        var none = Assert.Single(Findings(f));
        Assert.Contains("No clash check has been run", (string)none["message"]!);
        Assert.Null(ClashReports.Read(f.Model));

        // Run once: the report kept with its date, the model's hash and its counts.
        var r = RunCheck(f);
        Assert.False((bool)r["stale"]!);
        Assert.Equal(1, (int)r["counts"]!["missing"]!);
        var report = Report(f);
        var item = Assert.Single(report.Items);
        Assert.Equal((1, OpeningClashes.Missing, pipe, wall, ClashReports.Open), (item.Number, item.Kind, item.MepId, item.HostId, item.State));
        Assert.True(DateTime.TryParse(report.Date, out _));
        Assert.Equal(ClashReports.ModelHash(f.Model), report.Hash);
        var finding = Assert.Single(Findings(f));
        Assert.StartsWith("Clash 1:", (string)finding["message"]!);
        Assert.Equal(pipe, (string?)finding["entityId"]);

        // Editing the model runs nothing: the report stays as it was and says it is out of date.
        Set(f, pipe, ("size", 0.2));
        Assert.Equal(report.Hash, Report(f).Hash);
        Assert.True((bool)((JsonObject)f.Session.Query("coordination.report", new JsonObject())!)["stale"]!);
        Assert.Contains(Findings(f), x => ((string)x["message"]!).Contains("out of date"));
        // A view or a remark is not the model: the report stays current.
        RunCheck(f);
        f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey });
        Assert.False((bool)((JsonObject)f.Session.Query("coordination.report", new JsonObject())!)["stale"]!);
    }

    [Fact]
    public void Make_opening_fixes_a_clash_with_the_clearance_and_a_run_again_marks_it_fixed()
    {
        var f = new MepFixture();
        var wall = Wall(f);
        Pipe(f, 0.16);
        f.Run("coordination.setSettings", new JsonObject { ["clearance"] = 0.03 });
        RunCheck(f);
        var fix = (JsonObject)f.Run("coordination.fix", new JsonObject { ["numbers"] = new JsonArray(1) })!;
        Assert.Equal(1, (int)fix["fixed"]!);
        Assert.Empty((JsonArray)fix["failed"]!);
        // A round opening in the wall: 160 + 2 × 30 (the larger of the check's and the rule's clearance) = 220, its centre on the pipe's axis.
        var opening = Assert.Single(Openings.Of(f.Model, wall));
        var p = Openings.Place(f.Model, opening);
        Assert.Equal("circle", opening.Str("shape"));
        Assert.Equal(0.22, p.Width, 3);
        Assert.Equal(0.22, p.Height, 3);
        Assert.Equal(2, p.Along, 2);
        Assert.Equal(1.0, p.Sill + p.Height / 2, 2);
        Assert.Equal(ClashReports.Fixed, Assert.Single(Report(f).Items).State);
        // Its void is round, and the check finds it fits keeping 30 mm.
        Assert.Equal(OpeningVoids.Round, Assert.Single(OpeningVoids.Of(f.Session.Reader, VoidOptions.All)).Shape);
        Assert.Equal(OpeningClashes.Ok, Assert.Single(OpeningClashes.Run(f.Session.Reader, new OpeningClashes.Options(0.03))).Status);
        // Run again: still fixed, nothing open.
        var again = RunCheck(f);
        Assert.Equal(0, (int)again["counts"]!["open"]!);
        Assert.Equal(1, (int)again["counts"]!["fixed"]!);
        Assert.Equal(ClashReports.Fixed, Assert.Single(Report(f).Items).State);
    }

    [Fact]
    public void Enlarge_opening_grows_one_too_small_and_ignored_clashes_stay_ignored()
    {
        var f = new MepFixture();
        var wall = Wall(f);
        var pipe = Pipe(f, 0.25);
        f.Id("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 2, ["width"] = 0.2, ["height"] = 0.2, ["sill"] = 0.9 });
        var second = Pipe(f, 0.1, 5);
        RunCheck(f);
        var items = Report(f).Items.OrderByDescending(i => i.Kind).ToList();
        Assert.Equal([OpeningClashes.TooSmall, OpeningClashes.Missing], items.Select(i => i.Kind).ToArray());
        var small = items[0];
        Assert.Equal(pipe, small.MepId);

        // The second ignored with a comment, as one step.
        f.Run("coordination.setItems", new JsonObject { ["numbers"] = new JsonArray(items[1].Number), ["state"] = "ignored", ["comment"] = "Cut on site" });
        // The first enlarged: 250 + 2 × 20 = 290 round it, keeping what the opening had.
        var fix = (JsonObject)f.Run("coordination.fix", new JsonObject { ["numbers"] = new JsonArray(small.Number) })!;
        Assert.Equal(1, (int)fix["fixed"]!);
        var p = Openings.Place(f.Model, Assert.Single(Openings.Of(f.Model, wall)));
        Assert.InRange(p.Width, 0.29 - 1e-6, 0.3 + 1e-6);
        Assert.InRange(p.Height, 0.29 - 1e-6, 0.3 + 1e-6);
        Assert.Equal(OpeningClashes.Ok, OpeningClashes.Run(f.Session.Reader, new OpeningClashes.Options()).Single(c => c.MepId == pipe).Status);

        RunCheck(f);
        var now = Report(f).Items.ToDictionary(i => i.Number);
        Assert.Equal(ClashReports.Fixed, now[small.Number].State);
        Assert.Equal((ClashReports.Ignored, "Cut on site"), (now[items[1].Number].State, now[items[1].Number].Comment));
        Assert.Equal(second, now[items[1].Number].MepId);
        // Ignored clashes are no warnings.
        Assert.DoesNotContain(Findings(f), x => ((string)x["message"]!).StartsWith("Clash "));
        // An empty void has nothing to make an opening round.
        Assert.Throws<CommandException>(() => f.Run("coordination.fix", new JsonObject { ["numbers"] = new JsonArray(99) }));
    }

    [Fact]
    public void The_report_goes_out_as_bcf_csv_and_pdf_and_marks_its_clashes_in_a_plan_on_screen_only()
    {
        var f = new MepFixture();
        var wall = Wall(f);
        var pipe = Pipe(f, 0.1);
        RunCheck(f);
        var made = (JsonObject)f.Run("coordination.remarks", new JsonObject())!;
        Assert.Equal(1, (int)made["added"]!);
        var remark = Assert.Single(Remarks.All(f.Model));
        Assert.StartsWith("Clash 1:", remark.Description);
        Assert.Equal(remark.Guid.ToString(), Model.GlobalId.ToGuid(Assert.Single(((JsonArray)made["ids"]!).Select(x => (string)x!))).ToString());

        var csv = Encoding.UTF8.GetString(Convert.FromBase64String((string)((JsonObject)f.Session.Query("coordination.reportFile", new JsonObject { ["format"] = "csv" })!)["data"]!));
        Assert.Contains("No.,Kind,Status,MEP element,Host", csv);
        Assert.Contains("1,No opening,open", csv);
        var pdf = Convert.FromBase64String((string)((JsonObject)f.Session.Query("coordination.reportFile", new JsonObject { ["format"] = "pdf" })!)["data"]!);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        // A plan marks it only when asked, on the screen; printed only when asked too.
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        List<string> Clouds(bool print) => Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!, print).Pens.Where(p => p.Layer == ClashMarks.Layer).SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        Assert.Empty(Clouds(false));
        f.Run("coordination.markView", new JsonObject { ["id"] = plan, ["on"] = true });
        Assert.Contains("1", Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!).Pens.Where(p => p.Stroke == ClashMarks.Color || p.Layer == "TEXT").SelectMany(p => p.Texts).Select(t => t.Text));
        var mark = Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!).Marks.Single(m => m.Data?["clash"] != null);
        Assert.Equal(pipe, mark.Owner);
        Assert.DoesNotContain(Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!, print: true).Marks, m => m.Data?["clash"] != null);
        f.Run("coordination.markView", new JsonObject { ["id"] = plan, ["print"] = true });
        Assert.Single(Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!, print: true).Marks, m => m.Data?["clash"] != null);
        // Fixed or ignored: no longer marked.
        f.Run("coordination.setItems", new JsonObject { ["numbers"] = new JsonArray(1), ["state"] = "ignored" });
        Assert.DoesNotContain(Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!).Marks, m => m.Data?["clash"] != null);
        _ = wall;
    }

    [Fact]
    public void A_run_cuts_a_provision_opening_with_its_clearance_that_follows_it_and_goes_with_it()
    {
        var f = new MepFixture();
        var wall = Wall(f);
        var full = Volume(f, wall);
        var pipe = Pipe(f, 0.16);
        // An existing project (no rule): nothing cut.
        Assert.Empty(Provisions(f));
        Assert.Equal(full, Volume(f, wall), 6);

        AutoCut(f);
        var p = Assert.Single(Provisions(f));
        Assert.Equal((pipe, wall, OpeningVoids.Round, true), (p.SourceId, p.HostId, p.Shape, p.Cuts));
        // 160 + 2 × 20 = 200 round, through the wall's whole thickness.
        Assert.Equal(0.2, p.Width, 6);
        var t = Walls.Section(f.Model, f.Model.Get(wall)!).Sum(b => b.To - b.From);
        Assert.Equal(t, p.Depth, 6);
        var box = Measure.Bounds(p.Void)!.Value;
        Assert.Equal(0, (box.Min.X + box.Max.X) / 2, 3);
        Assert.Equal(1.0, (box.Min.Z + box.Max.Z) / 2, 3);
        // The wall is cut by it (a 200 mm circle through it), it is a void, and the check finds nothing to report.
        Assert.Equal(full - Math.PI * 0.01 * t, Volume(f, wall), 2);
        var v = Assert.Single(OpeningVoids.Of(f.Session.Reader, VoidOptions.All));
        Assert.Equal((pipe, pipe), (v.Source, v.SelectId));
        Assert.Equal(OpeningClashes.Ok, Assert.Single(OpeningClashes.Run(f.Session.Reader, new OpeningClashes.Options(0.02))).Status);
        Assert.Equal(0, (int)RunCheck(f)["counts"]!["open"]!);

        // The pipe moved 1.5 m along the wall: its opening follows, the wall rebuilt.
        Set(f, pipe, ("points", MepFixture.Pts((1.5, -1), (1.5, 3))));
        box = Measure.Bounds(Assert.Single(Provisions(f)).Void)!.Value;
        Assert.Equal(1.5, (box.Min.X + box.Max.X) / 2, 3);
        Assert.Equal(full - Math.PI * 0.01 * t, Volume(f, wall), 2);
        // Bigger: the opening grows; along the wall: no crossing, no opening.
        Set(f, pipe, ("size", 0.25));
        Assert.Equal(0.29, Assert.Single(Provisions(f)).Width, 6);
        Set(f, pipe, ("points", MepFixture.Pts((-1, 0), (6, 0))));
        Assert.Empty(Provisions(f));
        Assert.Equal(full, Volume(f, wall), 6);
        // Deleted: gone, the wall whole.
        Set(f, pipe, ("points", MepFixture.Pts((0, -1), (0, 3))));
        Assert.Single(Provisions(f));
        f.Session.Edit("delete", e => e.Remove(pipe));
        Assert.Empty(Provisions(f));
        Assert.Equal(full, Volume(f, wall), 6);
    }

    [Fact]
    public void Load_bearing_hosts_and_hosts_ranked_over_the_run_are_reported_not_cut()
    {
        var f = new MepFixture();
        var wall = Wall(f);
        var full = Volume(f, wall);
        var pipe = Pipe(f, 0.1);
        AutoCut(f);
        Set(f, wall, ("params", new JsonArray(new JsonObject { ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true })));
        var p = Assert.Single(Provisions(f));
        Assert.Equal(ProvisionOpening.Structural, p.Held);
        Assert.Equal(full, Volume(f, wall), 6);
        Assert.Empty(OpeningVoids.Of(f.Session.Reader, VoidOptions.All));
        var c = Assert.Single(OpeningClashes.Run(f.Session.Reader, new OpeningClashes.Options()));
        Assert.Equal((OpeningClashes.Missing, ProvisionOpening.Structural), (c.Status, c.Held));
        Assert.Contains("bears load", c.Message);
        Assert.Equal(1, (int)RunCheck(f)["counts"]!["held"]!);
        // The rule asked to cut load-bearing hosts too: cut.
        AutoCut(f, new JsonObject { ["structural"] = true });
        Assert.True(Assert.Single(Provisions(f)).Cuts);
        Assert.True(Volume(f, wall) < full - 1e-4);
        // Walls ranked over pipes in the cut order: the walls win, not cut.
        f.Run("cutOrder.set", new JsonObject { ["priorities"] = new JsonObject { ["IfcWall"] = 80 } });
        Assert.Equal(ProvisionOpening.Order, Assert.Single(Provisions(f)).Held);
        Assert.Equal(full, Volume(f, wall), 6);
        // The MEP rule survives the cut order put back to its defaults.
        f.Run("cutOrder.reset", new JsonObject());
        Assert.True(CutOrder.Mep(f.Model).Cut);
        Assert.True(Assert.Single(Provisions(f)).Cuts);
        // A system that says no: nothing cut.
        f.Run("mep.updateSystem", new JsonObject { ["id"] = f.Model.Get(pipe)!.Str("systemId"), ["cutHosts"] = false });
        Assert.Empty(Provisions(f));
        // A demolished run cuts nothing either.
        f.Run("mep.updateSystem", new JsonObject { ["id"] = f.Model.Get(pipe)!.Str("systemId"), ["cutHosts"] = null });
        Set(f, pipe, ("phaseDemolished", "p1"));
        Assert.Empty(Provisions(f));
    }

    [Fact]
    public void A_run_through_a_floor_cuts_it_and_the_ifc_export_writes_the_provision_as_an_opening()
    {
        var f = new MepFixture();
        var slab = f.Id("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = MepFixture.Pts((-2, -2), (2, -2), (2, 2), (-2, 2)) });
        var full = Volume(f, slab);
        var system = f.System(defaults: new JsonObject { ["insulation"] = 0 });
        // A pipe falling through the floor at a slant, from 1 m over it to 1 m under.
        var run = f.Draw(system, MepFixture.Pts((0, -1), (0, 1)), more: new JsonObject { ["pair"] = false });
        Set(f, run, ("size", 0.1), ("levels", new JsonArray(1.0, -1.0)));
        AutoCut(f);
        var p = Assert.Single(Provisions(f));
        Assert.Equal((slab, OpeningVoids.Slab, OpeningVoids.Rectangle), (p.HostId, p.Kind, p.Shape));
        Assert.True(Volume(f, slab) < full - 1e-4);
        Assert.Equal(OpeningClashes.Ok, Assert.Single(OpeningClashes.Run(f.Session.Reader, new OpeningClashes.Options())).Status);

        string Export(IfcExportOptions o)
        {
            using var ms = new MemoryStream();
            IfcExport.Write(f.Session, ms, "t.ifc", o);
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        var ifc = Export(new IfcExportOptions());
        Assert.Equal(1, ifc.Split("IFCOPENINGELEMENT(").Length - 1);
        Assert.Contains($"IFCOPENINGELEMENT('{p.GlobalId}'", ifc);
        Assert.Contains("'Lapin_ProvisionOpening'", ifc);
        Assert.Contains("'Provision opening'", ifc);
        Assert.DoesNotContain("PROVISIONFORVOID", ifc);
        // With the voids asked: a provision for void beside it.
        Assert.Equal(1, Export(new IfcExportOptions(Voids: true)).Split(".PROVISIONFORVOID.").Length - 1);
    }

    [Fact]
    public void A_new_project_cuts_by_default_and_an_opened_one_keeps_what_it_had()
    {
        var f = new MepFixture();
        Assert.False(CutOrder.Mep(f.Model).Cut);
        f.Session.New();
        var rule = CutOrder.Mep(f.Model);
        Assert.True(rule.Cut);
        Assert.False(rule.CutStructural);
        Assert.Equal(0.02, rule.Clearance, 6);
        // The cut order lists runs over walls and floors, under beams and columns.
        var rows = ((JsonArray)f.Session.Query("cutOrder.list", new JsonObject())!).OfType<JsonObject>().ToDictionary(x => (string)x["class"]!, x => (int)x["priority"]!);
        Assert.True(rows["IfcPipeSegment"] > rows["IfcWall"] && rows["IfcPipeSegment"] > rows["IfcSlab"]);
        // Sizes: grown by the clearance and rounded up to the step, never under the smallest.
        Assert.Equal(0.2, rule.Size(0.16, 0.02), 9);
        Assert.Equal(0.21, rule.Size(0.161, 0.02), 9);
        Assert.Equal(0.05, rule.Size(0.001, 0), 9);
    }
}
