using System.Text;
using System.Text.Json.Nodes;
using Lapin.Analysis.Solver;
using Lapin.Ifc;
using Lapin.Platform.Analysis;

namespace Lapin.Engine.Tests;

/// <summary>
/// Beams of different depths meeting a column (docs/plan-fem.md §9.9, from §9.8's check): each beam on its own axis' level, the
/// column one node at its head and the other beams joined to it by a rigid vertical arm — no column piece 25 mm long between the
/// beams' axes, no beam tilting 5 cm over 5 m; the frame balanced; the JG export and the view for IFC writing the arms.
/// </summary>
public class BeamLevelTests
{
    /// <summary>The cross-check's space frame: four columns 30 × 30 (3 m), beams 30 × 50 round 5 × 4 m and a diagonal 20 × 30, their tops flush.</summary>
    static Fixture SpaceFrame()
    {
        var f = LoadCaseTests.F();
        string Type(string kind, string name, double w, double d) => (string)f.Run(kind == "column" ? "structure.createColumnType" : "structure.createBeamType", new JsonObject
        {
            ["name"] = name, ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = w, ["depth"] = d }, ["material"] = AnalysisRunTests.Concrete,
        })!["id"]!;
        var col = Type("column", "C30x30", 0.3, 0.3);
        foreach (var (x, y) in new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 4.0), (0.0, 4.0) })
        {
            f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y, ["typeId"] = col });
            f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y });
        }
        var bt = Type("beam", "B30x50", 0.3, 0.5);
        string Beam(double x1, double y1, double x2, double y2, string type) =>
            (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["typeId"] = type })!["id"]!;
        Beam(0, 0, 5, 0, bt); Beam(5, 0, 5, 4, bt); Beam(5, 4, 0, 4, bt); Beam(0, 4, 0, 0, bt);
        var diagonal = Beam(0, 0, 5, 4, Type("beam", "B20x30", 0.2, 0.3));
        f.Run("analysis.defaultCases", []);
        f.Run("analysis.setReleases", new JsonObject { ["ids"] = new JsonArray(diagonal), ["i"] = "pinned", ["j"] = "pinned" });
        f.Run("analysis.addLineLoad", new JsonObject { ["storeyId"] = f.Storey, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(5, 0), ["z"] = 2.75, ["value"] = 8, ["caseId"] = LoadCaseTests.IdOf(f, "Q-A") });
        return f;
    }

    [Fact]
    public void Beams_of_different_depths_keep_their_levels_and_meet_the_column_by_an_arm()
    {
        var f = SpaceFrame();
        var am = AnalyticalModel.Build(f.Model, f.Session.Reader.Services, new AnalyticalOptions());
        // No sliver of column, every beam level.
        Assert.All(am.Frames, x => Assert.True((x.J.At + x.OffJ - x.I.At - x.OffI).Length > 0.5, $"{x.Source.Name}: {(x.J.At - x.I.At).Length} m"));
        foreach (var b in am.Frames.Where(x => x.Source.Kind == "beam"))
            Assert.Equal((b.I.At + b.OffI).Z, (b.J.At + b.OffJ).Z, 9);
        // The columns' heads at the shallow diagonal's axis (3 − 0.15) where it frames in, the deep beams' at theirs (3 − 0.25),
        // joined to it by an arm 10 cm down; elsewhere the deep beams' own level.
        var diag = am.Frames.Single(x => x.Source.Name.Contains("B20x30") || x.Source.Section.Name == "B20x30");
        Assert.Equal(2.85, diag.I.At.Z, 6);
        Assert.Equal(2.85, diag.J.At.Z, 6);
        var arms = am.Frames.Where(x => x.Source.Section.Name == "B30x50").SelectMany(x => new[] { x.OffI, x.OffJ }).Where(o => o.Length > 1e-9).ToList();
        Assert.Equal(4, arms.Count);
        Assert.All(arms, o => { Assert.Equal(-0.1, o.Z, 6); Assert.Equal(0, o.X, 9); Assert.Equal(0, o.Y, 9); });
        Assert.Equal(4, am.LevelJoins);
        var run = AnalysisRunTests.Run(f);
        Assert.True((bool)run["ok"]!, run["mechanism"]?.ToJsonString());
        Assert.All(run["cases"]!.AsArray(), c => Assert.True((bool)(c!["balanced"] ?? true)));
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).StartsWith("4 beam ends of another depth meet a column at its joint"));
    }

    [Fact]
    public void The_jg_export_and_the_view_for_ifc_write_the_arms()
    {
        var f = SpaceFrame();
        var r = f.Session.Query("jg.export", new JsonObject { ["units"] = "contract" })!;
        var doc = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String((string)r["data"]!)))!.AsObject();
        Assert.Empty(JgValidator.Validate(doc));
        Assert.Contains(doc["frames"]!.AsArray(), x => x!["end_offsets"] is JsonObject or JsonArray);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "frame.ifc", new IfcExportOptions(Structural: true));
        var step = StepFile.Read(ms.ToArray());
        Assert.Equal(4, step.OfType("IFCRELCONNECTSWITHECCENTRICITY").Count());
        // In the member's axes (x along it, z its Axis = local 2, upward for a level beam): 10 cm down.
        Assert.All(step.OfType("IFCCONNECTIONPOINTECCENTRICITY"), e => Assert.Equal(-0.1, Convert.ToDouble(e[4]), 6));
    }
}
