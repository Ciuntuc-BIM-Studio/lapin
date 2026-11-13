using System.Text.Json.Nodes;
using Lapin.Analysis.Solver;
using Lapin.Ifc;
using Lapin.Platform.Analysis;

namespace Lapin.Engine.Tests;

/// <summary>
/// Rods and ties work axially (docs/plan-fem.md §9.9): a steel hall's Ø20 bracing rods are pinned at their own ends and carry
/// their weight to them, so they no longer sag as beams under it (645 mm before, §9.8); its own analysisAxial false makes a
/// rod a beam again; the view for IFC writes them PIN_JOINED_MEMBER.
/// </summary>
public class AxialMemberTests
{
    static JsonObject Frames(Fixture f, string code) => f.Session.Query("analysis.results", new JsonObject { ["case"] = code })!.AsObject();

    [Fact]
    public void A_halls_bracing_rods_are_pinned_carry_their_weight_to_their_ends_and_do_not_bend()
    {
        var f = AnalysisRunTests.Hall();
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run["mechanism"]?.ToJsonString());
        var a = AnalysisRun.Of(f.Session.Reader);
        var rods = a.Problem.Axial;
        Assert.Equal(12, rods.Count);
        Assert.All(a.Problem.Model.Frames.Where(x => rods.Contains(x.Source.SourceId)), x => Assert.Equal("circle", x.Source.Section.Shape.Kind));
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).StartsWith("12 rods or ties work axially"));
        // Their weight at their ends: no span load on them in the self weight's case.
        var g1 = a.Problem.Cases.Single(c => c.Loads.Id == LoadCaseTests.IdOf(f, "G1"));
        Assert.DoesNotContain(g1.Loads.Span, l => rods.Contains(a.Problem.Frames[l.Element].Source.SourceId));
        Assert.True((bool)run["cases"]!.AsArray().Single(c => (string?)c!["code"] == "G1")!["balanced"]!);
        // No bending: their moments and their sag off the line between their ends are nothing.
        var results = Frames(f, "G1")["frames"]!.AsArray().Select(x => x!.AsObject()).Where(x => rods.Contains((string)x["sourceId"]!)).ToList();
        Assert.NotEmpty(results);
        foreach (var r in results)
        {
            foreach (var s in r["stations"]!.AsArray())
            {
                Assert.True(Math.Abs((double)s!["My"]!) < 1e-6, $"{r["name"]}: My {s["My"]}");
                Assert.True(Math.Abs((double)s["Mz"]!) < 1e-6, $"{r["name"]}: Mz {s["Mz"]}");
            }
            Assert.True((double)r["deflection"]!["max"]! < 1e-4, $"{r["name"]} sags {r["deflection"]}");
        }
        // A rod made a beam again by its own word sags under its weight.
        var one = (string)results[0]["sourceId"]!;
        f.Run("analysis.setAxial", new JsonObject { ["ids"] = new JsonArray(one), ["axial"] = false });
        var again = AnalysisRun.Of(f.Session.Reader);
        Assert.DoesNotContain(one, again.Problem.Axial);
        var beam = Frames(f, "G1")["frames"]!.AsArray().Select(x => x!.AsObject()).Where(x => (string?)x["sourceId"] == one).ToList();
        Assert.Contains(beam, x => (double)x["deflection"]!["max"]! > 0.01);
    }

    [Fact]
    public void The_view_for_ifc_writes_rods_as_pin_joined_members()
    {
        var f = AnalysisRunTests.Hall();
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "hall.ifc", new IfcExportOptions(Structural: true));
        var step = StepFile.Read(ms.ToArray());
        var bars = step.OfType("IFCSTRUCTURALCURVEMEMBER").ToList();
        var axial = AnalysisRun.Of(f.Session.Reader).Problem.Model.Frames.Count(x => AnalysisRun.Of(f.Session.Reader).Problem.Axial.Contains(x.Source.SourceId));
        Assert.Equal(axial, bars.Count(b => (b[7] as StepEnum)?.Value == "PIN_JOINED_MEMBER"));
        Assert.Equal(bars.Count - axial, bars.Count(b => (b[7] as StepEnum)?.Value == "RIGID_JOINED_MEMBER"));
    }
}
