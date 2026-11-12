using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Analysis.Solver;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A development check, on request only (`LAPIN_SAV_OUT=<folder>`; nothing runs without it): a set of models exported with
/// IFC's structural analysis view (`<name>.ifc`) next to Lapin's own analysis of them (`<name>.lapin.json`: analysis.run, the
/// solver's problem as it took it — nodes, bars, releases, arms, each case's loads — analysis.results for every case, and the
/// same problem solved again as Euler–Bernoulli bars, no shear deformation, to compare with programs that have none). The
/// files are read by scripts/fem-check (ifc_structural_mechanics run as a separate program on the .ifc, PyNite on the
/// problem); see its README. `LAPIN_SAV_MODELS=a,b` takes only those.
/// </summary>
public class SavDumpTests
{
    const string Concrete = AnalysisRunTests.Concrete;

    static void Density(Fixture f, string material, double rho) => f.Run("material.setData", new JsonObject
    {
        ["id"] = f.Model.OfClass(IfcClass.Material).First(m => m.Name == material).Id,
        ["params"] = new JsonArray(new JsonObject { ["pset"] = "Pset_MaterialCommon", ["name"] = "MassDensity", ["value"] = rho }),
    });

    static string Rect(Fixture f, string kind, string name, double w, double d, string material) =>
        (string)f.Run(kind == "column" ? "structure.createColumnType" : "structure.createBeamType", new JsonObject
        {
            ["name"] = name, ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = w, ["depth"] = d }, ["material"] = material,
        })!["id"]!;

    static string Column(Fixture f, double x, double y, string type, bool footing = true)
    {
        var id = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y, ["typeId"] = type })!["id"]!;
        if (footing) f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y });
        return id;
    }

    static string Beam(Fixture f, double x1, double y1, double x2, double y2, string type) =>
        (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["typeId"] = type })!["id"]!;

    static string Case(Fixture f, string code) => LoadCaseTests.IdOf(f, code);

    static void Point(Fixture f, double x, double y, double z, double value, string direction, string code) =>
        f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = f.Storey, ["at"] = new JsonArray(x, y), ["z"] = z, ["value"] = value, ["direction"] = direction, ["caseId"] = Case(f, code) });

    static void Line(Fixture f, double x1, double y1, double x2, double y2, double z, double value, string direction, string code) =>
        f.Run("analysis.addLineLoad", new JsonObject
        {
            ["storeyId"] = f.Storey, ["a"] = new JsonArray(x1, y1), ["b"] = new JsonArray(x2, y2), ["z"] = z, ["value"] = value, ["direction"] = direction, ["caseId"] = Case(f, code),
        });

    /// <summary>The height of the bars' top nodes (the beams' axis) as the analytical model puts them.</summary>
    static double Top(Fixture f) => AnalysisRun.Of(f.Session.Reader).Problem.Model.Nodes.Max(n => n.At.Z);

    /// <summary>A concrete column 20 × 40 cm on a pad footing: its own weight, 10 kN along +x and 5 kN along +y at its head.</summary>
    static Fixture Cantilever()
    {
        var f = LoadCaseTests.F();
        var col = Rect(f, "column", "C20x40", 0.2, 0.4, Concrete);
        Density(f, Concrete, 2500);
        Column(f, 0, 0, col);
        f.Run("analysis.defaultCases", []);
        var h = Top(f);
        Point(f, 0, 0, h, 10, "+x", "W");
        Point(f, 0, 0, h, 5, "+y", "S");
        return f;
    }

    /// <summary>A 6 m beam 30 × 50 pinned at both ends onto two columns 30 × 30: 10 kN/m over it, 20 kN in its middle, its own weight.</summary>
    static Fixture SimpleBeam()
    {
        var f = LoadCaseTests.F();
        var col = Rect(f, "column", "C30x30", 0.3, 0.3, Concrete);
        Density(f, Concrete, 2500);
        Column(f, 0, 0, col);
        Column(f, 6, 0, col);
        var beam = Beam(f, 0, 0, 6, 0, Rect(f, "beam", "B30x50", 0.3, 0.5, Concrete));
        f.Run("analysis.defaultCases", []);
        f.Run("analysis.setReleases", new JsonObject { ["ids"] = new JsonArray(beam), ["i"] = "pinned", ["j"] = "pinned" });
        var h = Top(f);
        Line(f, 0, 0, 6, 0, h, 10, "down", "Q-A");
        Point(f, 3, 0, h, 20, "down", "G2");
        return f;
    }

    /// <summary>A fixed-base portal 6 m wide: 10 kN sideways at a column's head, 10 kN/m on its beam, its own weight.</summary>
    static Fixture Portal()
    {
        var f = LoadCaseTests.F();
        var col = Rect(f, "column", "C30x30", 0.3, 0.3, Concrete);
        Density(f, Concrete, 2500);
        Column(f, 0, 0, col);
        Column(f, 6, 0, col);
        Beam(f, 0, 0, 6, 0, Rect(f, "beam", "B30x50", 0.3, 0.5, Concrete));
        f.Run("analysis.defaultCases", []);
        var h = Top(f);
        Point(f, 0, 0, h, 10, "+x", "W");
        Line(f, 0, 0, 6, 0, h, 10, "down", "Q-A");
        return f;
    }

    /// <summary>
    /// A space frame 5 × 4 m: three concrete columns 30 × 30 and a UPN 200 (an unsymmetric section: its centroid off its line,
    /// eccentric ends), four perimeter beams, a diagonal beam pinned at both ends, a beam pinned about its strong axis (local 3) at one end; loads
    /// along three directions in four cases. Without the UPN (<paramref name="channel"/> false), all four columns 30 × 30.
    /// </summary>
    static Fixture SpaceFrame(bool channel)
    {
        var f = LoadCaseTests.F();
        var col = Rect(f, "column", "C30x30", 0.3, 0.3, Concrete);
        Density(f, Concrete, 2500);
        var upn = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "UPN 200", ["section"] = "UPN 200", ["material"] = "S355" })!["id"]!;
        Column(f, 0, 0, col);
        Column(f, 5, 0, col);
        Column(f, 5, 4, channel ? upn : col);
        Column(f, 0, 4, col);
        var bt = Rect(f, "beam", "B30x50", 0.3, 0.5, Concrete);
        var beams = new[] { Beam(f, 0, 0, 5, 0, bt), Beam(f, 5, 0, 5, 4, bt), Beam(f, 5, 4, 0, 4, bt), Beam(f, 0, 4, 0, 0, bt) };
        var diagonal = Beam(f, 0, 0, 5, 4, Rect(f, "beam", "B20x30", 0.2, 0.3, Concrete));
        f.Run("analysis.defaultCases", []);
        f.Run("analysis.setReleases", new JsonObject { ["ids"] = new JsonArray(diagonal), ["i"] = "pinned", ["j"] = "pinned" });
        f.Run("analysis.setReleases", new JsonObject { ["ids"] = new JsonArray(beams[1]), ["j"] = new JsonArray(false, false, false, false, false, true) });
        var h = Top(f);
        Point(f, 5, 4, h, 8, "+x", "W");
        Point(f, 0, 4, h, 6, "-y", "W");
        Point(f, 2.5, 2, h, 15, "down", "G2");
        Line(f, 0, 0, 5, 0, h, 8, "down", "Q-A");
        Line(f, 5, 4, 0, 4, h, 4, "down", "Q-A");
        Line(f, 5, 0, 5, 4, h, 2, "+y", "S");
        return f;
    }

    /// <summary>The guide's floor (scripts/user-docs/scenes.mjs, scene analysis): 6 × 4 m on four beams and columns, 5 kN/m² on it.</summary>
    static Fixture GuideFrame()
    {
        var t = AnalysisRunTests.Frame6x4();
        t.F.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = AnalysisRunTests.Rect(0, 0, 6, 4), ["value"] = 5 });
        return t.F;
    }

    /// <summary>
    /// The default steel hall with its purlins, rails and eaves beams UPN 160 instead of cold-formed Z and lipped C sections
    /// (which ifc_structural_mechanics does not take: its README).
    /// </summary>
    static Fixture HallUpn()
    {
        var f = new Fixture(new Lapin.Structure.StructureModule(), new Lapin.Steel.SteelModule(), new Lapin.Drafting.DraftingModule(), new Lapin.Studio.StudioModule(), new Lapin.Analysis.AnalysisModule());
        f.Run("steel.addHall", new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = new JsonObject { ["purlin"] = "UPN 160", ["rail"] = "UPN 160", ["eavesBeamProfile"] = "UPN 160" } });
        f.Run("analysis.defaultCases", []);
        f.Run("material.setData", new JsonObject
        {
            ["id"] = f.Model.OfClass(IfcClass.Material).Single(m => m.Name == "Steel").Id,
            ["params"] = new JsonArray(
                new JsonObject { ["pset"] = "Pset_MaterialMechanical", ["name"] = "YoungModulus", ["value"] = 210e9 },
                new JsonObject { ["pset"] = "Pset_MaterialMechanical", ["name"] = "PoissonRatio", ["value"] = 0.3 },
                new JsonObject { ["pset"] = "Pset_MaterialCommon", ["name"] = "MassDensity", ["value"] = 7850 }),
        });
        return f;
    }

    static readonly (string Name, Func<Fixture> Build)[] Models =
    [
        ("cantilever", Cantilever), ("simple-beam", SimpleBeam), ("portal", Portal), ("space-frame", () => SpaceFrame(true)), ("space-frame-rect", () => SpaceFrame(false)),
        ("guide-frame", GuideFrame), ("steel-hall-upn", HallUpn), ("steel-hall", AnalysisRunTests.Hall),
    ];

    static JsonArray V(Vec3 v) => new(v.X, v.Y, v.Z);
    static JsonArray B(bool[] b) => new(b.Select(x => (JsonNode)x).ToArray());
    static JsonArray D(IEnumerable<double> xs) => new(xs.Select(x => (JsonNode)x).ToArray());

    static JsonObject ProblemJson(FrameProblem p, IReadOnlyList<string> codes) => new()
    {
        ["units"] = new JsonObject { ["length"] = "m", ["force"] = "kN", ["modulus"] = "kN/m2" },
        ["nodes"] = new JsonArray(p.Nodes.Select((n, i) => (JsonNode)new JsonObject { ["id"] = p.NodeIds[i], ["at"] = V(n), ["held"] = p.Held[i] is { } h ? B(h) : null }).ToArray()),
        ["elements"] = new JsonArray(p.Elements.Select(e => (JsonNode)new JsonObject
        {
            ["id"] = e.Id, ["ifcGuid"] = Step.Guid(e.Id, "structural-member"), ["sourceId"] = e.SourceId, ["name"] = e.Name, ["i"] = e.I, ["j"] = e.J, ["offI"] = V(e.OffI), ["offJ"] = V(e.OffJ), ["local2"] = V(e.Local2),
            ["E"] = e.E, ["G"] = e.G, ["A"] = e.A, ["I22"] = e.I22, ["I33"] = e.I33, ["J"] = e.Jt, ["As2"] = e.As2, ["As3"] = e.As3,
            ["releaseI"] = B(e.ReleaseI), ["releaseJ"] = B(e.ReleaseJ), ["L"] = e.L, ["ex"] = V(e.Ex), ["ey"] = V(e.Ey), ["ez"] = V(e.Ez),
        }).ToArray()),
        ["cases"] = new JsonArray(p.Cases.Select((c, k) => (JsonNode)new JsonObject
        {
            ["id"] = c.Id, ["code"] = codes[k],
            ["nodal"] = new JsonArray(c.Nodal.Select(x => (JsonNode)new JsonObject { ["node"] = x.Key, ["f"] = V(x.Value) }).ToArray()),
            ["moments"] = new JsonArray(c.Moments.Select(x => (JsonNode)new JsonObject { ["node"] = x.Key, ["m"] = V(x.Value) }).ToArray()),
            ["span"] = new JsonArray(c.Span.Select(x => (JsonNode)new JsonObject
            {
                ["element"] = x.Element, ["a"] = x.Load.A, ["b"] = x.Load.B, ["qa"] = V(x.Load.Qa), ["qb"] = V(x.Load.Qb), ["point"] = x.Load.Point,
            }).ToArray()),
        }).ToArray()),
    };

    /// <summary>The same problem as Euler–Bernoulli bars (no shear areas), solved again: each case's displacements and reactions.</summary>
    static JsonObject EulerBernoulli(FrameProblem p, IReadOnlyList<string> codes)
    {
        var q = new FrameProblem();
        for (var i = 0; i < p.Nodes.Count; i++) q.AddNode(p.Nodes[i], p.NodeIds[i], p.Held[i]);
        foreach (var e in p.Elements)
            q.Elements.Add(new FrameElement
            {
                Id = e.Id, Name = e.Name, SourceId = e.SourceId, I = e.I, J = e.J, OffI = e.OffI, OffJ = e.OffJ, Local2 = e.Local2,
                E = e.E, G = e.G, A = e.A, I22 = e.I22, I33 = e.I33, Jt = e.Jt, ReleaseI = e.ReleaseI, ReleaseJ = e.ReleaseJ,
            });
        q.Cases.AddRange(p.Cases);
        var s = FrameSolver.Solve(q);
        var o = new JsonObject { ["ok"] = s.Ok, ["springs"] = s.Springs };
        var cases = new JsonObject();
        for (var k = 0; k < s.Cases.Count; k++)
        {
            var c = s.Cases[k];
            cases[codes[k]] = new JsonObject
            {
                ["u"] = new JsonArray(Enumerable.Range(0, q.Nodes.Count).Select(n => (JsonNode)D(c.U.Skip(n * 6).Take(6))).ToArray()),
                ["reactions"] = new JsonObject(c.Reactions.OrderBy(r => r.Key).Select(r => KeyValuePair.Create(q.NodeIds[r.Key], (JsonNode?)D(r.Value)))),
                ["ends"] = new JsonArray(c.Elements.Select(r => (JsonNode)D(r.Forces)).ToArray()),
            };
        }
        o["cases"] = cases;
        return o;
    }

    [Fact]
    public void Dump_the_models_with_their_structural_analysis_view_and_Lapins_results_when_asked()
    {
        if (Environment.GetEnvironmentVariable("LAPIN_SAV_OUT") is not { Length: > 0 } dir) return;
        Directory.CreateDirectory(dir);
        var only = Environment.GetEnvironmentVariable("LAPIN_SAV_MODELS") is { Length: > 0 } list ? list.Split(',', StringSplitOptions.TrimEntries).ToHashSet() : null;
        foreach (var (name, build) in Models)
        {
            if (only != null && !only.Contains(name)) continue;
            var f = build();
            using (var fs = File.Create(Path.Combine(dir, name + ".ifc")))
                IfcExport.Write(f.Session, fs, name + ".ifc", new IfcExportOptions(Structural: true));
            var run = AnalysisRun.Of(f.Session.Reader);
            Assert.True(run.Ok, $"{name}: {run.RunJson().ToJsonString()}");
            var codeOf = run.Report.Cases.ToDictionary(c => c.Id, c => c.Code);
            var codes = run.Problem.Problem.Cases.Select(c => codeOf[c.Id]).ToList();
            var results = new JsonObject();
            foreach (var code in codes) results[code] = f.Session.Query("analysis.results", new JsonObject { ["case"] = code });
            // Each bar's axis displaced, in global axes, at 21 points along it, in each case.
            var along = new JsonObject();
            var sol = run.Solution!;
            for (var k = 0; k < codes.Count; k++)
                along[codes[k]] = new JsonArray(run.Problem.Problem.Elements.Select((e, i) => (JsonNode)new JsonArray(Enumerable.Range(0, 21).Select(t =>
                {
                    var x = e.L * t / 20;
                    var (u, v, w) = Stations.Deflection(e, sol.Cases[k].Elements[i], x);
                    var g = e.Ex * u + e.Ey * v + e.Ez * w;
                    return (JsonNode)new JsonArray(x, g.X, g.Y, g.Z);
                }).ToArray())).ToArray());
            var o = new JsonObject
            {
                ["model"] = name,
                ["along"] = along,
                ["run"] = run.RunJson(),
                ["problem"] = ProblemJson(run.Problem.Problem, codes),
                ["results"] = results,
                ["eulerBernoulli"] = EulerBernoulli(run.Problem.Problem, codes),
            };
            File.WriteAllText(Path.Combine(dir, name + ".lapin.json"), o.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        }
    }
}
