using System.Text.Json.Nodes;
using Lapin.Analysis.Solver;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;

namespace Lapin.Engine.Tests;

/// <summary>
/// The linear static analysis of the model (docs/plan-fem.md §9.3, §9.4): analysis.run on a concrete floor on four beams and
/// columns (the cases' sums and reactions balanced, kept for the model's revision only), analysis.results for a case, a
/// combination (by superposition) and an envelope, analysis.diagram, analysis.setReleases (one undo step, the beam then a
/// simple span), a bar without E refused, a mechanism said, an IPE 300 column as a cantilever with its real section, and the
/// guide's house balanced in every case.
/// </summary>
public class AnalysisRunTests
{
    internal sealed record Frame(Fixture F, string Ground, string First, string Slab, string[] Columns, string[] Beams, string BeamType);

    internal const string Concrete = "Concrete C25/30";
    const double G = 9.80665;

    static void Density(Fixture f, string material, double rho) => f.Run("material.setData", new JsonObject
    {
        ["id"] = f.Model.OfClass(IfcClass.Material).First(m => m.Name == material).Id,
        ["params"] = new JsonArray(new JsonObject { ["pset"] = "Pset_MaterialCommon", ["name"] = "MassDensity", ["value"] = rho }),
    });

    /// <summary>A floor 6 × 4 m on four beams 30×50 over four columns 30×30, 3 m high, on pad footings; the concrete at 2500 kg/m³; the default cases.</summary>
    internal static Frame Frame6x4(bool footings = true, string beamMaterial = Concrete)
    {
        var f = LoadCaseTests.F();
        var ground = f.Storey;
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        var colType = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30x30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 }, ["material"] = Concrete })!["id"]!;
        var beamType = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "B30x50", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.5 }, ["material"] = beamMaterial })!["id"]!;
        var slabType = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Floor 20", ["layers"] = new JsonArray(new JsonObject { ["material"] = Concrete, ["thickness"] = 0.2, ["core"] = true }),
        })!["id"]!;
        Density(f, Concrete, 2500);
        (double X, double Y)[] corners = [(0, 0), (6, 0), (6, 4), (0, 4)];
        var columns = corners.Select(c => (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = ground, ["x"] = c.X, ["y"] = c.Y, ["typeId"] = colType })!["id"]!).ToArray();
        var beams = Enumerable.Range(0, 4).Select(i => (string)f.Run("structure.addBeam", new JsonObject
        {
            ["storeyId"] = ground, ["x1"] = corners[i].X, ["y1"] = corners[i].Y, ["x2"] = corners[(i + 1) % 4].X, ["y2"] = corners[(i + 1) % 4].Y, ["typeId"] = beamType,
        })!["id"]!).ToArray();
        var slab = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = first, ["points"] = new JsonArray(new JsonArray(-0.15, -0.15), new JsonArray(6.15, -0.15), new JsonArray(6.15, 4.15), new JsonArray(-0.15, 4.15)), ["typeId"] = slabType,
        })!["id"]!;
        if (footings) foreach (var (x, y) in corners) f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = ground, ["x"] = x, ["y"] = y });
        f.Run("analysis.defaultCases", []);
        return new Frame(f, ground, first, slab, columns, beams, beamType);
    }

    /// <summary>The analysis as in S1: no shells, the slabs by the 45° rule, the walls line supports.</summary>
    internal static void NoShells(Fixture f) => f.Run("analysis.setOptions", new JsonObject { ["shells"] = "none" });

    internal static JsonArray Rect(double x0, double y0, double x1, double y1) =>
        new(new JsonArray(x0, y0), new JsonArray(x1, y0), new JsonArray(x1, y1), new JsonArray(x0, y1));

    internal static JsonObject Run(Fixture f) => f.Session.Query("analysis.run", [])!.AsObject();
    static JsonObject Results(Fixture f, JsonObject a) => f.Session.Query("analysis.results", a)!.AsObject();
    static JsonObject Bar(JsonObject results, string sourceId) => results["frames"]!.AsArray().Select(x => x!.AsObject()).Single(x => (string?)x["sourceId"] == sourceId);
    static JsonObject Station(JsonObject frame, int i) => frame["stations"]!.AsArray()[i]!.AsObject();

    [Fact]
    public void A_floor_on_four_beams_runs_balanced_in_every_case_and_is_kept_for_the_models_revision()
    {
        var t = Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = Rect(0, 0, 6, 4), ["value"] = 5 });
        var run = Run(f);
        Assert.True((bool)run["ok"]!);
        // Four columns and four beams on eight nodes; the floor a shell of 12 × 8 elements at 0.5 m sharing the beams' nodes,
        // the four feet fixed: (13 × 9 + 4 − 4) × 6 degrees of freedom.
        Assert.Equal(8, (int)run["elements"]!);
        Assert.Equal(96, (int)run["shells"]!["elements"]!);
        Assert.Equal(96, (int)run["shells"]!["quads"]!);
        Assert.Equal(0.5, (double)run["shells"]!["meshSize"]!);
        Assert.Equal(13 * 9 * 6, (int)run["dofs"]!);
        Assert.True((double)run["solveMs"]! >= 0);
        var cases = run["cases"]!.AsArray().Select(c => c!.AsObject()).ToDictionary(c => (string)c["code"]!);
        // G1: the columns' and beams' own weight along them, the slab's core over its outline (6 × 4 × 0.2 m), 25 kN/m³ less a hair (2500 kg/m³ × g).
        var g1 = 2500 * G / 1000 * (4 * 0.09 * 2.9 + 0.15 * (2 * 6 + 2 * 4) + 6 * 4 * 0.2);
        Assert.Equal(-g1, (double)cases["G1"]["sumLoads"]![2]!, 5);
        Assert.Equal(g1, (double)cases["G1"]["sumReactions"]![2]!, 5);
        Assert.Equal(-120, (double)cases["Q-A"]["sumLoads"]![2]!, 9);
        Assert.Equal(120, (double)cases["Q-A"]["sumReactions"]![2]!, 9);
        foreach (var c in cases.Values) Assert.True((bool)c["balanced"]!);
        Assert.Equal(0, (double)cases["Q-A"]["unmapped"]!);
        Assert.Contains(run["combinations"]!.AsArray(), c => (string?)c!["key"] == "slsq");
        Assert.Null(run["mechanism"]);
        // Kept until the model changes: the same analysis twice, a new one after an edit.
        var a1 = AnalysisRun.Of(f.Session.Reader);
        Assert.Same(a1, AnalysisRun.Of(f.Session.Reader));
        f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = t.First, ["at"] = new JsonArray(3, 2), ["value"] = 10 });
        var a2 = AnalysisRun.Of(f.Session.Reader);
        Assert.NotSame(a1, a2);
        Assert.Equal(-130, a2.Solution!.Cases.Single(c => c.Id == LoadCaseTests.IdOf(f, "Q-A")).SumLoads.Z, 9);
        // Nothing of it in the project.
        Assert.DoesNotContain(f.Model.Entities, e => e.Attrs.ToJsonString().Contains("sumLoads"));
    }

    [Fact]
    public void Results_give_a_case_a_combination_by_superposition_and_an_envelope()
    {
        var t = Frame6x4();
        var f = t.F;
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = Rect(0, 0, 6, 4), ["value"] = 5 });
        var q = Results(f, new JsonObject { ["case"] = "Q-A" });
        Assert.Equal("case", (string?)q["kind"]);
        Assert.Equal(8, q["frames"]!.AsArray().Count);
        Assert.Equal(8, q["nodes"]!.AsArray().Count);
        Assert.Equal(4, q["reactions"]!.AsArray().Count);
        Assert.Equal(120, q["reactions"]!.AsArray().Sum(r => (double)r!["f"]![2]!), 9);
        var beam = Bar(q, t.Beams[0]);
        Assert.Equal(Stations.Count, beam["stations"]!.AsArray().Count);
        Assert.Equal(6, (double)beam["length"]!, 9);
        // The long beam's moment largest in its middle, its deflection there too.
        Assert.Equal(3, (double)beam["extremes"]!["Mz"]!["xMax"]!, 6);
        Assert.True((double)beam["extremes"]!["Mz"]!["max"]! > 0);
        Assert.Equal(3, (double)beam["deflection"]!["x"]!, 6);
        // One beam asked for: its stations, its two nodes.
        var one = Results(f, new JsonObject { ["case"] = "Q-A", ["elementId"] = t.Beams[0] });
        Assert.Single(one["frames"]!.AsArray());
        Assert.Equal(2, one["nodes"]!.AsArray().Count);

        // A combination is its cases' results by their factors.
        var key = $"uls/{LoadCaseTests.IdOf(f, "Q-A")}";
        var c = Results(f, new JsonObject { ["combination"] = key });
        var g = Results(f, new JsonObject { ["case"] = "G1" });
        foreach (var name in new[] { "N", "Vy", "Mz" })
            for (var i = 0; i < Stations.Count; i++)
                Assert.Equal(1.35 * (double)Station(Bar(g, t.Beams[0]), i)[name]! + 1.5 * (double)Station(beam, i)[name]!, (double)Station(Bar(c, t.Beams[0]), i)[name]!, 4);
        // The ULS envelope: each station's least and largest over the ULS combinations.
        var env = Results(f, new JsonObject { ["envelope"] = "uls" });
        Assert.Equal("envelope", (string?)env["kind"]);
        Assert.Equal(2, env["combinations"]!.AsArray().Count);
        var mid = Station(Bar(env, t.Beams[0]), 5)["Mz"]!.AsArray();
        Assert.Equal((double)Station(Bar(c, t.Beams[0]), 5)["Mz"]!, (double)mid[1]!, 4);
        Assert.Equal(1.35 * (double)Station(Bar(g, t.Beams[0]), 5)["Mz"]!, (double)mid[0]!, 4);
        Assert.NotNull(env["maxima"]!["Mz"]);
        Assert.Throws<CommandException>(() => Results(f, new JsonObject { ["envelope"] = "quasi" }));
        Assert.Throws<CommandException>(() => Results(f, new JsonObject { ["case"] = "nope" }));
    }

    [Fact]
    public void Diagrams_draw_a_quantity_along_the_bars_and_the_deformed_shape_in_world_coordinates()
    {
        var t = Frame6x4();
        var f = t.F;
        var d = f.Session.Query("analysis.diagram", new JsonObject { ["case"] = "G1", ["quantity"] = "Mz", ["scale"] = 0.01 })!.AsObject();
        Assert.Equal("kN·m", (string?)d["unit"]);
        Assert.Equal(0.01, (double)d["scale"]!);
        var lines = d["polylines"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.Equal(8, lines.Count);
        var beam = lines.Single(l => (string?)l["sourceId"] == t.Beams[0]);
        var pts = beam["points"]!.AsArray();
        var vals = beam["values"]!.AsArray();
        Assert.Equal(pts.Count, vals.Count);
        // The beam on its own axis (2.75 m: under the shell slab by its arms), its sagging moment drawn below it (the tension
        // side), 1 cm a kN·m.
        Assert.Equal(2.75, (double)beam["base"]![0]![2]!, 9);
        var i = Enumerable.Range(0, vals.Count).MaxBy(k => (double)vals[k]!);
        Assert.Equal(2.75 - 0.01 * (double)vals[i]!, (double)pts[i]![2]!, 6);
        var deformed = f.Session.Query("analysis.diagram", new JsonObject { ["envelope"] = "sls", ["quantity"] = "deformed" })!.AsObject();
        Assert.Equal("m", (string?)deformed["unit"]);
        Assert.True((double)deformed["scale"]! > 0);
        Assert.All(deformed["polylines"]!.AsArray(), l => Assert.Equal(21, l!["points"]!.AsArray().Count));
        Assert.Throws<CommandException>(() => f.Session.Query("analysis.diagram", new JsonObject { ["case"] = "G1", ["quantity"] = "M" }));
    }

    [Fact]
    public void A_beam_released_at_both_ends_carries_its_load_as_a_simple_span_in_one_undo_step()
    {
        var t = Frame6x4();
        var f = t.F;
        NoShells(f);
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = t.First, ["points"] = Rect(0, 0, 6, 4), ["value"] = 5 });
        var before = Bar(Results(f, new JsonObject { ["case"] = "Q-A" }), t.Beams[0]);
        Assert.True(Math.Abs((double)Station(before, 0)["Mz"]!) > 1);
        var diff = f.RunDiff("analysis.setReleases", new JsonObject { ["ids"] = new JsonArray(t.Beams[0]), ["i"] = "pinned", ["j"] = new JsonArray(false, false, false, true, true, true) });
        Assert.Contains(diff.Nodes, n => n.Id == t.Beams[0]);
        var own = f.Model.Get(t.Beams[0])!.Attrs[FrameReleases.Key]!;
        Assert.Equal("[false,false,false,false,true,true]", own["i"]!.ToJsonString());
        var after = Bar(Results(f, new JsonObject { ["case"] = "Q-A" }), t.Beams[0]);
        Assert.True(Math.Abs((double)Station(after, 0)["Mz"]!) < 1e-6);
        Assert.True(Math.Abs((double)Station(after, 10)["Mz"]!) < 1e-6);
        // The trapezoid on it (peak 2 × 5 kN/m from 2 m to 4 m): wL²/8-like, (q·2)·(L²/8 − a²/6) with a = 2.
        Assert.Equal(10 * (36.0 / 8 - 4.0 / 6), (double)after["extremes"]!["Mz"]!["max"]!, 6);
        f.Session.Undo();
        Assert.Null(f.Model.Get(t.Beams[0])!.Attrs[FrameReleases.Key]);
        // Null takes the element's own off; bad releases are refused.
        f.Run("analysis.setReleases", new JsonObject { ["ids"] = new JsonArray(t.Beams[0]), ["j"] = "pinned" });
        f.Run("analysis.setReleases", new JsonObject { ["ids"] = new JsonArray(t.Beams[0]), ["j"] = null });
        Assert.Null(f.Model.Get(t.Beams[0])!.Attrs[FrameReleases.Key]);
        Assert.Throws<CommandException>(() => f.Run("analysis.setReleases", new JsonObject { ["ids"] = new JsonArray(t.Beams[0]), ["i"] = new JsonArray(true) }));
        Assert.Throws<CommandException>(() => f.Run("analysis.setReleases", new JsonObject { ["ids"] = new JsonArray(t.Beams[0]) }));
    }

    [Fact]
    public void Releases_that_leave_a_bar_free_to_slide_or_swing_are_refused_naming_it()
    {
        // ux at both ends: nothing holds the bar along its axis (its axial stiffness condenses to zero, a singular element).
        Assert.Equal("its releases leave it free to move (ux released at both ends): hold that degree of freedom at one end at least",
            FrameReleases.Unstable([true, false, false, false, false, false], [true, false, false, false, false, false]));
        // uy at one end with rz at both: it swings about the other end. uy at i with rz at j only: a sliding end on a pin, held.
        Assert.Contains("uy released with rz at both ends", FrameReleases.Unstable([false, true, false, false, false, true], [false, false, false, false, false, true]));
        Assert.Null(FrameReleases.Unstable([false, true, false, false, false, false], [false, false, false, false, false, true]));
        Assert.Null(FrameReleases.Unstable(FrameReleases.Pinned, FrameReleases.Pinned));
        var t = Frame6x4();
        var f = t.F;
        var name = f.Model.Get(t.Beams[0])!.Name;
        var ex = Assert.Throws<CommandException>(() => f.Run("analysis.setReleases", new JsonObject
            { ["ids"] = new JsonArray(t.Beams[0]), ["i"] = new JsonArray(true, false, false, false, false, false), ["j"] = new JsonArray(true, false, false, false, false, false) }));
        Assert.Contains($"„{name}”", ex.Message);
        Assert.Contains("ux released at both ends", ex.Message);
        Assert.Null(f.Model.Get(t.Beams[0])!.Attrs[FrameReleases.Key]);
    }

    [Fact]
    public void A_bar_whose_material_has_no_E_is_refused_and_the_analysis_does_not_start()
    {
        var t = Frame6x4(beamMaterial: "Mystery");
        var run = Run(t.F);
        Assert.False((bool)run["ok"]!);
        var refused = run["refused"]!.AsArray();
        Assert.Equal(4, refused.Count);
        Assert.Contains("has no E", (string)refused[0]!["reason"]!);
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).Contains("refused"));
        Assert.Throws<CommandException>(() => Results(t.F, new JsonObject { ["case"] = "G1" }));
    }

    /// <summary>
    /// Report #189: a bar refused for no E, its user giving E in Manage → Materials → Data (material.setData, an own parameter as
    /// the tab adds it: a text by default, or the Young's modulus E preset in MPa) and the analysis then running. The refusal says
    /// where to give E; a value in no unit of stress is left out, said.
    /// </summary>
    [Theory]
    [InlineData("E", "text", "30000", 30e9)]
    [InlineData("Young's modulus", "number", 30000.0, 30e9)]
    [InlineData("E", "text", "31 GPa", 31e9)]
    [InlineData("E (GPa)", "number", 31.0, 31e9)]
    [InlineData("Modulus of elasticity", "number", 3.1e10, 31e9)]
    [InlineData("E", "text", "4350 ksi", 4350 * 6894757.293168)]
    public void A_bar_without_E_runs_once_its_user_gives_E_in_the_materials_data(string param, string kind, object value, double pa)
    {
        var t = Frame6x4(beamMaterial: "Mystery");
        var first = Run(t.F);
        Assert.False((bool)first["ok"]!);
        var reason = (string)first["refused"]!.AsArray()[0]!["reason"]!;
        Assert.Contains("„Mystery” has no E", reason);
        Assert.Contains("Manage → Materials → Data", reason);

        var mystery = t.F.Model.OfClass(IfcClass.Material).Single(m => m.Name == "Mystery");
        t.F.Run("material.setData", new JsonObject
        {
            ["id"] = mystery.Id,
            ["params"] = new JsonArray(new JsonObject { ["name"] = param, ["kind"] = kind, ["value"] = value is string text ? JsonValue.Create(text) : JsonValue.Create((double)value) }),
        });
        var e = StructuralMaterials.Of(t.F.Model, t.F.Model.Get(mystery.Id)!).E;
        Assert.NotNull(e);
        Assert.Equal(pa, e.Value, pa * 1e-9);
        Assert.Contains("project: material „Mystery”", e.Source);
        Assert.True((bool)Run(t.F)["ok"]!);
    }

    [Fact]
    public void A_modulus_in_no_unit_of_stress_is_left_out_and_the_refusal_says_so()
    {
        var t = Frame6x4(beamMaterial: "Mystery");
        var mystery = t.F.Model.OfClass(IfcClass.Material).Single(m => m.Name == "Mystery");
        t.F.Run("material.setData", new JsonObject
        {
            ["id"] = mystery.Id,
            ["params"] = new JsonArray(new JsonObject { ["name"] = "E", ["kind"] = "text", ["value"] = "30000 kg" }),
        });
        var run = Run(t.F);
        Assert.False((bool)run["ok"]!);
        Assert.Contains("„kg” is not a unit of stress", (string)run["refused"]!.AsArray()[0]!["reason"]!);
    }

    static void Give(Fixture f, string material, params JsonObject[] ps) => f.Run("material.setData", new JsonObject
    {
        ["id"] = f.Model.OfClass(IfcClass.Material).Single(m => m.Name == material).Id,
        ["params"] = new JsonArray(ps),
    });

    static JsonObject P(string name, object value, string? pset = null)
    {
        var o = new JsonObject { ["name"] = name, ["kind"] = value is string ? "text" : "number", ["value"] = value is string text ? JsonValue.Create(text) : JsonValue.Create(Convert.ToDouble(value)) };
        if (pset != null) { o["pset"] = pset; o.Remove("kind"); }
        return o;
    }

    static JsonObject BeamCheck(Frame t) => t.F.Session.Query("analysis.check", new JsonObject { ["id"] = t.Beams[0] })!["members"]!.AsArray()[0]!.AsObject();

    /// <summary>
    /// Report #189's other half: the Data tab's own Compressive strength (MPa) gives a concrete material that names no class its
    /// fck, and with it Ecm (EN 1992-1-1 Table 3.1: 22 (fcm/10)^0.3, fcm = fck + 8); its bars are analysed and checked.
    /// </summary>
    [Theory]
    [InlineData("Compressive strength", 30.0, 30e6)]
    [InlineData("fck", "30 MPa", 30e6)]
    [InlineData("Rezistență la compresiune", 30.0, 30e6)]
    [InlineData("Compressive strength (N/mm²)", 30.0, 30e6)]
    [InlineData("fc", 3e7, 30e6)]
    public void Concrete_naming_no_class_takes_fck_from_its_data_and_is_checked(string param, object value, double pa)
    {
        var t = Frame6x4(beamMaterial: "Concrete");
        Give(t.F, "Concrete", P(param, value));
        var mat = StructuralMaterials.Of(t.F.Model, t.F.Model.OfClass(IfcClass.Material).Single(m => m.Name == "Concrete"));
        Assert.Equal("concrete", mat.Kind);
        Assert.Equal(pa, mat.Fc!.Value, 1e-3);
        Assert.Contains("project: material „Concrete”", mat.Fc.Source);
        Assert.Equal(22e9 * Math.Pow((pa / 1e6 + 8) / 10, 0.3), mat.E!.Value, 1e3);
        Assert.Contains("EN 1992-1-1", mat.E.Source);
        Assert.True((bool)Run(t.F)["ok"]!);
        var beam = BeamCheck(t);
        Assert.NotEmpty(beam["checks"]!.AsArray());
        Assert.DoesNotContain(beam["notes"]!.AsArray(), n => ((string)n!).StartsWith("No fck"));
    }

    [Theory]
    [InlineData("Yield strength", 355.0, 355e6)]
    [InlineData("fy", "355 MPa", 355e6)]
    [InlineData("Limită de curgere", 355.0, 355e6)]
    [InlineData("Yield stress (ksi)", 51.5, 51.5 * 6894757.293168)]
    [InlineData("fyk", 3.55e8, 355e6)]
    public void Steel_naming_no_grade_takes_fy_from_its_data_and_is_checked(string param, object value, double pa)
    {
        var t = Frame6x4(beamMaterial: "Steel");
        Assert.StartsWith("No fy", (string)BeamCheck(t)["notes"]!.AsArray().First(n => ((string)n!).StartsWith("No fy"))!);
        Give(t.F, "Steel", P(param, value));
        var mat = StructuralMaterials.Of(t.F.Model, t.F.Model.OfClass(IfcClass.Material).Single(m => m.Name == "Steel"));
        Assert.Equal(pa, mat.Fy!.Value, 1e-3);
        Assert.Contains("project: material „Steel”", mat.Fy.Source);
        Assert.NotEmpty(BeamCheck(t)["checks"]!.AsArray());
    }

    /// <summary>
    /// Issue #121, finding 2: a Spanish designation (EHE-08, Código Estructural: HA reinforced, HM plain, HP prestressed, then
    /// fck in MPa) is read as a C class is: concrete, fck from the name, Ecm by EN 1992-1-1 Table 3.1 (its row when the table has
    /// that fck, else its formula); its bars are analysed and checked.
    /// </summary>
    [Theory]
    [InlineData("HA-30/P/20/IIa", 30, 33e9)]
    [InlineData("HM-20/B/40/I", 20, 30e9)]
    [InlineData("HP-45/F/12/IIIa", 45, 36e9)]
    [InlineData("HA-25", 25, 31e9)]
    public void A_spanish_concrete_designation_gives_fck_and_Ecm_and_is_checked(string name, double fck, double ecm)
    {
        var t = Frame6x4(beamMaterial: name);
        var mat = StructuralMaterials.Of(t.F.Model, t.F.Model.OfClass(IfcClass.Material).Single(m => m.Name == name));
        Assert.Equal("concrete", mat.Kind);
        Assert.Equal(fck * 1e6, mat.Fc!.Value, 1e-3);
        Assert.Contains(name.Split('/')[0], mat.Fc.Source);
        Assert.Equal(ecm, mat.E!.Value, 1e-3);
        Assert.Contains("EN 1992-1-1:2004 Table 3.1", mat.E.Source);
        Assert.True((bool)Run(t.F)["ok"]!);
        var beam = BeamCheck(t);
        Assert.NotEmpty(beam["checks"]!.AsArray());
        Assert.DoesNotContain(beam["notes"]!.AsArray(), n => ((string)n!).StartsWith("No fck"));
    }

    /// <summary>Issue #121, finding 2: a material whose name says nothing but which carries Pset_MaterialConcrete (an IFC file's) is concrete.</summary>
    [Fact]
    public void A_material_with_pset_material_concrete_is_concrete_whatever_its_name()
    {
        var t = Frame6x4(beamMaterial: "M-07");
        Give(t.F, "M-07", P("CompressiveStrength", 25e6, "Pset_MaterialConcrete"));
        var mat = StructuralMaterials.Of(t.F.Model, t.F.Model.OfClass(IfcClass.Material).Single(m => m.Name == "M-07"));
        Assert.Equal("concrete", mat.Kind);
        Assert.Equal(22e9 * Math.Pow((25 + 8) / 10.0, 0.3), mat.E!.Value, 1e3);
        Assert.True((bool)Run(t.F)["ok"]!);
    }

    /// <summary>Spanish words for concrete (hormigón) make a concrete as concrete and beton do; names the rules knew keep what they gave.</summary>
    [Theory]
    [InlineData("Hormigón armado", "concrete", null)]
    [InlineData("hormigon en masa", "concrete", null)]
    [InlineData("Concrete C25/30", "concrete", "C25/30")]
    [InlineData("Steel S355", "steel", "S355")]
    [InlineData("Timber C24", "timber", "C24")]
    [InlineData("Mystery", "other", null)]
    [InlineData("HAB-25", "other", null)]
    public void A_materials_kind_by_its_name(string name, string kind, string? grade)
    {
        var f = LoadCaseTests.F();
        var mat = StructuralMaterials.Of(f.Model, StructuralMaterials.EntityOf(f.Model, name));
        Assert.Equal(kind, mat.Kind);
        Assert.Equal(grade, mat.Grade);
    }

    [Fact]
    public void A_standard_set_comes_before_the_materials_own_strength()
    {
        var t = Frame6x4(beamMaterial: "Steel");
        Give(t.F, "Steel", P("YieldStress", 235e6, "Pset_MaterialSteel"), P("fy", 355.0));
        var mat = StructuralMaterials.Of(t.F.Model, t.F.Model.OfClass(IfcClass.Material).Single(m => m.Name == "Steel"));
        Assert.Equal(235e6, mat.Fy!.Value);
    }

    [Fact]
    public void Without_fy_or_fck_the_check_says_where_to_give_them_and_a_strength_out_of_range_is_left_out()
    {
        var steel = Frame6x4(beamMaterial: "Steel");
        var noFy = BeamCheck(steel)["notes"]!.AsArray().Select(n => (string)n!).Single(n => n.StartsWith("No fy"));
        Assert.Contains("Yield strength fy (MPa)", noFy);
        Assert.Contains("Manage → Materials → Data", noFy);

        var concrete = Frame6x4(beamMaterial: "Concrete");
        Give(concrete.F, "Concrete", P("E", "31 GPa"), P("Compressive strength", 3000.0));
        var mat = StructuralMaterials.Of(concrete.F.Model, concrete.F.Model.OfClass(IfcClass.Material).Single(m => m.Name == "Concrete"));
        Assert.Null(mat.Fc);
        Assert.Contains(mat.Sources, x => x.StartsWith("Compressive strength = 3000") && x.EndsWith("left out"));
        Assert.True((bool)Run(concrete.F)["ok"]!);
        var noFck = BeamCheck(concrete)["notes"]!.AsArray().Select(n => (string)n!).Single(n => n.StartsWith("No fck"));
        Assert.Contains("Compressive strength (MPa)", noFck);
        Assert.Contains("Manage → Materials → Data", noFck);
    }

    [Fact]
    public void Steel_that_names_no_grade_takes_E_by_EN_1993_and_the_bars_run()
    {
        var t = Frame6x4(beamMaterial: "Steel");
        var steel = StructuralMaterials.Of(t.F.Model, t.F.Model.OfClass(IfcClass.Material).Single(m => m.Name == "Steel"));
        Assert.Equal(210e9, steel.E!.Value);
        Assert.Equal(0.3, steel.Poisson!.Value);
        Assert.Null(steel.Fy);
        Assert.Contains("EN 1993-1-1", steel.E.Source);
        Assert.True((bool)Run(t.F)["ok"]!);
    }

    [Fact]
    public void A_frame_standing_on_nothing_is_a_mechanism_said_with_its_node()
    {
        var t = Frame6x4(footings: false);
        NoShells(t.F);
        var run = Run(t.F);
        Assert.False((bool)run["ok"]!);
        var m = run["mechanism"]!.AsObject();
        Assert.StartsWith("N", (string)m["node"]!);
        Assert.Contains((string)m["dof"]!, FrameReleases.Dofs);
        Assert.NotEmpty(m["elements"]!.AsArray());
        Assert.NotEmpty(m["moving"]!.AsArray());
        Assert.Contains("mechanism", (string)m["message"]!);
    }

    [Fact]
    public void An_IPE_300_column_bends_as_a_cantilever_with_its_real_section()
    {
        var f = LoadCaseTests.F();
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = "IPE 300", ["material"] = "S355" })!["id"]!;
        var col = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0, ["typeId"] = type })!["id"]!;
        f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 });
        f.Run("analysis.defaultCases", []);
        var run = AnalysisRun.Of(f.Session.Reader);
        var frame = Assert.Single(run.Problem.Model.Frames);
        var h = frame.J.At.Z - frame.I.At.Z;
        const double P = 5;
        foreach (var d in new[] { "+x", "+y" })
            f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = f.Storey, ["at"] = new JsonArray(0, 0), ["z"] = h, ["value"] = P, ["direction"] = d, ["caseId"] = LoadCaseTests.IdOf(f, d == "+x" ? "W" : "S") });
        run = AnalysisRun.Of(f.Session.Reader);
        Assert.True(run.Ok, run.RunJson().ToJsonString());
        frame = Assert.Single(run.Problem.Model.Frames);
        var props = SectionProperties.Of(frame.Source.Section.Shape)!;
        // EN 10365's IPE 300: I = 8356 cm⁴ and 603.8 cm⁴; Lapin's from the section's outline within half a per cent.
        Assert.True(Math.Abs(props.I33 * 1e8 - 8356) / 8356 < 0.005);
        Assert.True(Math.Abs(props.I22 * 1e8 - 603.8) / 603.8 < 0.005);
        var top = run.Problem.NodeIndex[frame.J];
        var ux = run.Solution!.Cases.Single(c => c.Id == LoadCaseTests.IdOf(f, "W")).U[top * 6];
        var uy = run.Solution!.Cases.Single(c => c.Id == LoadCaseTests.IdOf(f, "S")).U[top * 6 + 1];
        // Timoshenko: bending PL³/3EI and shear PL/(G·Av), Av by EN 1993-1-1 §6.2.6(3); G = E/2(1 + ν).
        const double E = 210e6, Gs = 210e6 / 2.6;
        double[] got = [ux, uy];
        double[] want = [P * h * h * h / (3 * E * props.I33) + P * h / (Gs * props.Av2!.Value), P * h * h * h / (3 * E * props.I22) + P * h / (Gs * props.Av3!.Value)];
        Assert.Equal(want.Order(), got.Order(), (a, b) => Math.Abs(a - b) / b < 1e-9);
        // The moment at its foot P·h about the axis the force bends it.
        var res = Results(f, new JsonObject { ["case"] = "W" });
        var foot = Station(Bar(res, col), 0);
        Assert.Equal(P * h, Math.Max(Math.Abs((double)foot["My"]!), Math.Abs((double)foot["Mz"]!)), 6);
    }

    [Fact]
    public void The_guides_house_balances_in_every_case()
    {
        // As scripts/user-docs/scenes.mjs builds it: 12 × 8 m, an inner wall, a door and windows, a floor and a gable roof (ro_saxon), its walls load-bearing.
        var f = LoadCaseTests.F();
        var s = f.Storey;
        (double, double)[] o = [(0, 0), (12, 0), (12, 8), (0, 8)];
        var walls = Enumerable.Range(0, 4).Select(i => f.Wall(o[i].Item1, o[i].Item2, o[(i + 1) % 4].Item1, o[(i + 1) % 4].Item2)).ToList();
        walls.Add(f.Wall(7, 0, 7, 8));
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["x"] = 3, ["y"] = 0 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = walls[0], ["x"] = 9.5, ["y"] = 0 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = walls[2], ["x"] = 3.5, ["y"] = 8 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = walls[2], ["x"] = 9.5, ["y"] = 8 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = walls[1], ["x"] = 12, ["y"] = 4 });
        var pts = new JsonArray(o.Select(p => (JsonNode)new JsonArray(p.Item1, p.Item2)).ToArray());
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = s, ["points"] = pts.DeepClone() });
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = s, ["points"] = pts.DeepClone() })!["id"]!;
        f.Run("architecture.roofStyle", new JsonObject { ["id"] = roof, ["style"] = "ro_saxon" });
        foreach (var w in walls) f.Run("element.setParam", new JsonObject { ["id"] = w, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        f.Run("analysis.defaultCases", []);
        // Snow over the house's plan: a framed roof has no deck, so the rafters take it by their tributary widths, per
        // horizontal projection. The finishes' case gets an area load per true area over the same plan. A point load on the
        // ridge's highest node goes onto it.
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = s, ["points"] = Rect(0, 0, 12, 8), ["z"] = 4, ["value"] = 1.2, ["caseId"] = LoadCaseTests.IdOf(f, "S") });
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = s, ["points"] = Rect(0, 0, 12, 8), ["z"] = 4, ["value"] = 0.5, ["caseId"] = LoadCaseTests.IdOf(f, "G2") });
        var ridge = AnalysisRun.Of(f.Session.Reader).Problem.Model.Nodes.MaxBy(n => n.At.Z)!.At;
        f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = s, ["at"] = new JsonArray(ridge.X, ridge.Y), ["z"] = ridge.Z, ["value"] = 2, ["caseId"] = LoadCaseTests.IdOf(f, "Q-A") });
        var run = Run(f);
        Assert.True((bool)run["ok"]!, run.ToJsonString());
        Assert.True((int)run["elements"]! > 100);
        foreach (var c in run["cases"]!.AsArray())
        {
            Assert.True((bool)c!["balanced"]!, c.ToJsonString());
            Assert.Equal(-(double)c["sumLoads"]![2]!, (double)c["sumReactions"]![2]!, 6);
        }
        var byCode = run["cases"]!.AsArray().ToDictionary(c => (string)c!["code"]!, c => c!.AsObject());
        Assert.Equal(0, (double)byCode["S"]["unmapped"]!, 9);
        Assert.Equal(-1.2 * 96, (double)byCode["S"]["sumLoads"]![2]!, 6);
        Assert.Equal(0, (double)byCode["G2"]["unmapped"]!, 9);
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).StartsWith("Area loads on a framed roof"));
        // Per true area: the plan's load over cos α of the rafters (one pitch on a gable roof).
        var a = AnalysisRun.Of(f.Session.Reader);
        var rafter = a.Problem.Frames.First(x => x.Source.Role == "rafter");
        var (ri, rj) = (rafter.I.At + rafter.OffI, rafter.J.At + rafter.OffJ);
        var cos = (rj.Xy - ri.Xy).Length / (rj - ri).Length;
        Assert.InRange(cos, 0.3, 0.99);
        Assert.Equal(-0.5 * 96 / cos, (double)byCode["G2"]["sumLoads"]![2]!, 3);
        // Each case's bars' loads are what it says: snow on the rafters only.
        var snow = a.Problem.Cases.Single(c => c.Loads.Id == LoadCaseTests.IdOf(f, "S"));
        Assert.All(snow.Loads.Span, l => Assert.Equal("rafter", a.Problem.Frames[l.Element].Source.Role));
        Assert.Equal(-2, (double)byCode["Q-A"]["sumLoads"]![2]!, 9);
        var g1 = run["cases"]!.AsArray().Single(c => (string?)c!["code"] == "G1")!;
        Assert.True((double)g1["sumLoads"]![2]! < -10);
        // The roof's timber pinned about its strong axis by default; ULS results for every bar.
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).Contains("strong axis"));
        var env = Results(f, new JsonObject { ["envelope"] = "uls" });
        Assert.Equal((int)run["elements"]!, env["frames"]!.AsArray().Count);
    }
    /// <summary>The default steel hall with its secondary steel (purlins and rails on cleats, bracing on gussets, fly braces), its steel given E, ν and its density.</summary>
    internal static Fixture Hall()
    {
        var f = new Fixture(new Lapin.Structure.StructureModule(), new Lapin.Steel.SteelModule(), new Lapin.Drafting.DraftingModule(), new Lapin.Studio.StudioModule(), new Lapin.Analysis.AnalysisModule());
        f.Run("steel.addHall", new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = new JsonObject { ["secondary"] = true } });
        f.Run("analysis.defaultCases", []);
        var steel = f.Model.OfClass(IfcClass.Material).Single(m => m.Name == "Steel");
        f.Run("material.setData", new JsonObject
        {
            ["id"] = steel.Id,
            ["params"] = new JsonArray(
                new JsonObject { ["pset"] = "Pset_MaterialMechanical", ["name"] = "YoungModulus", ["value"] = 210e9 },
                new JsonObject { ["pset"] = "Pset_MaterialMechanical", ["name"] = "PoissonRatio", ["value"] = 0.3 },
                new JsonObject { ["pset"] = "Pset_MaterialCommon", ["name"] = "MassDensity", ["value"] = 7850 }),
        });
        return f;
    }

    [Fact]
    public void A_steel_hall_with_its_secondary_steel_on_pinned_connections_runs_balanced()
    {
        var f = Hall();
        // Its steel named only „Steel”: no E until the project gives it one (refused before).
        var run = Run(f);
        Assert.True((bool)run["ok"]!, run["mechanism"]?.ToJsonString());
        Assert.True((int)run["elements"]! > 500);
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).Contains("pinned by default (the members a connection joins)"));
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).Contains("rigid arms"));
        var g1 = run["cases"]!.AsArray().Single(c => (string?)c!["code"] == "G1")!;
        Assert.True((bool)g1["balanced"]!);
        Assert.True((double)g1["sumLoads"]![2]! < -50);
        // Every member a cleat, gusset or fly brace joins is pinned at its ends.
        var a = AnalysisRun.Of(f.Session.Reader);
        var linked = a.Problem.Model.Links.Select(l => l.MemberId).ToHashSet();
        var bars = a.Problem.Model.Frames.Where(x => linked.Contains(x.Source.SourceId)).GroupBy(x => x.Source, ReferenceEqualityComparer.Instance).ToList();
        Assert.NotEmpty(bars);
        // Pinned at both ends, unless one end meets nothing (the purlins' overhangs past the gables): a cantilever keeps both
        // ends fixed (its free tip's release would hinge on nothing).
        var uses = a.Problem.Model.Frames.SelectMany(x => new[] { x.I, x.J }).GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());
        bool Free(ANode n) => uses[n] <= 1 && n.Restraints == null && !a.Problem.Model.Shells.Any(sh => sh.Nodes.Contains(n));
        var (both, cantilevers) = (0, 0);
        foreach (var g in bars)
        {
            var (first, last) = (g.First(), g.Last());
            var (ri, rj) = (a.Problem.Releases.GetValueOrDefault(first)?.I ?? FrameReleases.Fixed, a.Problem.Releases.GetValueOrDefault(last)?.J ?? FrameReleases.Fixed);
            if (Free(last.J) || Free(first.I)) { cantilevers++; Assert.False(ri[4] || ri[5] || rj[4] || rj[5]); continue; }
            both++;
            Assert.True(ri[4] && ri[5]);
            Assert.True(rj[4] && rj[5]);
        }
        Assert.True(both > cantilevers && cantilevers > 0);
    }
    [Fact]
    public void A_UPN_column_bends_about_its_centroid_and_a_force_across_its_web_twists_it()
    {
        // A channel's centroid is off its box centre across its web: the bar's axis goes through it on rigid arms, so a force
        // at the node square to that offset twists the column by P·e, and it bends as a Timoshenko cantilever.
        var f = LoadCaseTests.F();
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = "UPN 200", ["material"] = "S355" })!["id"]!;
        var col = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0, ["typeId"] = type })!["id"]!;
        f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 });
        f.Run("analysis.defaultCases", []);
        var frame = Assert.Single(AnalysisRun.Of(f.Session.Reader).Problem.Model.Frames);
        var props = SectionProperties.Of(frame.Source.Section.Shape)!;
        Assert.True(props.Centroid.Length > 0.005);
        var h = frame.J.At.Z - frame.I.At.Z;
        const double P = 4;
        foreach (var d in new[] { "+x", "+y" })
            f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = f.Storey, ["at"] = new JsonArray(0, 0), ["z"] = h, ["value"] = P, ["direction"] = d, ["caseId"] = LoadCaseTests.IdOf(f, d == "+x" ? "W" : "S") });
        var run = AnalysisRun.Of(f.Session.Reader);
        Assert.True(run.Ok, run.RunJson().ToJsonString());
        var el = Assert.Single(run.Problem.Problem.Elements);
        // The arms: the same at both ends, the centroid's offset, square to the column.
        Assert.Equal(props.Centroid.Length, el.OffI.Length, 9);
        Assert.Equal(el.OffI, el.OffJ);
        Assert.Equal(0, el.OffI.Z, 12);
        Assert.Contains(run.Notes, n => n.Contains("unsymmetric section"));
        var top = run.Problem.NodeIndex[run.Problem.Model.Frames[0].J];
        var foot = run.Problem.NodeIndex[run.Problem.Model.Frames[0].I];
        var e = props.Centroid.Length;
        var arm = new Lapin.Geometry.Vec3(el.OffI.X, el.OffI.Y, 0) * (1 / e);
        const double E = 210e6, Gs = 210e6 / 2.6;
        foreach (var (code, dir) in new[] { ("W", new Lapin.Geometry.Vec3(1, 0, 0)), ("S", new Lapin.Geometry.Vec3(0, 1, 0)) })
        {
            var c = run.Solution!.Cases.Single(x => x.Id == LoadCaseTests.IdOf(f, code));
            // The twist: the arm across the force, P·e in the bar (the foot's arm takes it back: the support, under the load's
            // line, feels none).
            var torque = P * e * Lapin.Geometry.Vec3.Cross(arm, dir).Z;
            Assert.Equal(Math.Abs(torque), Math.Abs(Stations.At(el, c.Elements[0], h / 2).T), 9);
            Assert.Equal(0, c.Reactions[foot][5], 9);
            // Bending about the axis the force bends it, the arm's twist moving the head by θ·e more along the force.
            var across = Math.Abs(Lapin.Geometry.Vec3.Dot(dir, el.Ey)) > 0.5;
            var (i, av) = across ? (props.I33, props.Av2!.Value) : (props.I22, props.Av3!.Value);
            var twist = torque * h / (Gs * props.J);
            var expected = P * h * h * h / (3 * E * i) + P * h / (Gs * av) + Math.Abs(twist) * e * Math.Abs(Lapin.Geometry.Vec3.Cross(arm, dir).Z);
            var moved = c.U[top * 6] * dir.X + c.U[top * 6 + 1] * dir.Y;
            Assert.True(Math.Abs(moved - expected) / expected < 1e-6, $"{code}: {moved} vs {expected}");
        }
    }

    [Fact]
    public void A_model_without_bars_runs_its_slabs_loads_straight_into_its_walls()
    {
        // Four load-bearing walls 6 × 4 m under a floor, no bars: the floor's load is the walls' reactions.
        var f = LoadCaseTests.F();
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        (double, double)[] o = [(0, 0), (6, 0), (6, 4), (0, 4)];
        var walls = Enumerable.Range(0, 4).Select(i => f.Wall(o[i].Item1, o[i].Item2, o[(i + 1) % 4].Item1, o[(i + 1) % 4].Item2)).ToList();
        foreach (var w in walls) f.Run("element.setParam", new JsonObject { ["id"] = w, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = first, ["points"] = Rect(0, 0, 6, 4) });
        f.Run("analysis.defaultCases", []);
        NoShells(f);
        f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = first, ["points"] = Rect(0, 0, 6, 4), ["value"] = 2 });
        var run = Run(f);
        Assert.True((bool)run["ok"]!, run.ToJsonString());
        Assert.Equal(0, (int)run["dofs"]!);
        Assert.Equal(0, (int)run["elements"]!);
        Assert.Contains(run["notes"]!.AsArray(), n => ((string)n!).StartsWith("No bars and no shells: the slabs' loads go straight into the load-bearing walls"));
        var q = run["cases"]!.AsArray().Single(c => (string?)c!["code"] == "Q-A")!;
        Assert.Equal(-48, (double)q["sumLoads"]![2]!, 9);
        Assert.Equal(48, (double)q["sumReactions"]![2]!, 9);
        Assert.True((bool)q["balanced"]!);
        var res = Results(f, new JsonObject { ["case"] = "Q-A" });
        Assert.Empty(res["frames"]!.AsArray());
        Assert.Equal(48, res["reactions"]!.AsArray().Sum(r => (double)r!["f"]![2]!), 9);
    }

    [Fact]
    public void A_hall_with_channel_purlins_runs_their_overhangs_free_tips_not_released()
    {
        // UPN purlins, rails and eaves beams: unsymmetric sections, so every bar sits on arms to its centroid. A purlin's
        // overhang past a gable is a cantilever; a release at its free tip would leave the tip's node turning on its arm with
        // nothing to hold it (a mechanism). Neither end of a cantilever is released.
        var f = new Fixture(new Lapin.Structure.StructureModule(), new Lapin.Steel.SteelModule(), new Lapin.Drafting.DraftingModule(), new Lapin.Studio.StudioModule(), new Lapin.Analysis.AnalysisModule());
        f.Run("steel.addHall", new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = new JsonObject { ["purlin"] = "UPN 160", ["rail"] = "UPN 160", ["eavesBeamProfile"] = "UPN 160" } });
        f.Run("analysis.defaultCases", []);
        f.Run("material.setData", new JsonObject
        {
            ["id"] = f.Model.OfClass(IfcClass.Material).Single(m => m.Name == "Steel").Id,
            ["params"] = new JsonArray(new JsonObject { ["pset"] = "Pset_MaterialMechanical", ["name"] = "YoungModulus", ["value"] = 210e9 }),
        });
        var run = Run(f);
        Assert.True((bool)run["ok"]!, run["mechanism"]?.ToJsonString());
        Assert.True((bool)run["cases"]!.AsArray().Single(c => (string?)c!["code"] == "G1")!["balanced"]!);
        var a = AnalysisRun.Of(f.Session.Reader);
        var uses = a.Problem.Model.Frames.SelectMany(x => new[] { x.I, x.J }).GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());
        var tips = a.Problem.Model.Frames.Where(x => x.Source.Role?.StartsWith("pur") == true && (uses[x.I] == 1 || uses[x.J] == 1)).ToList();
        Assert.NotEmpty(tips);
        Assert.All(tips, x => Assert.False(a.Problem.Releases.TryGetValue(x, out var r) && (r.I.Any(b => b) || r.J.Any(b => b))));
    }
}
