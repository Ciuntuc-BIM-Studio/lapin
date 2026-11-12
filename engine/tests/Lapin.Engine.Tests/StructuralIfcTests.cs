using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;

namespace Lapin.Engine.Tests;

/// <summary>
/// IFC's structural analysis view (docs/plan-fem.md §8.7), read back with Lapin's STEP reader: a one-storey concrete frame (four
/// columns 30×30 on pad footings, four beams 30×50, a floor over it with 5 cm of screed) with the default cases, a room on the
/// floor and a point load on a beam: the analysis model serving the building, its nodes, bars and shells on their topology, the
/// supports, the members assigned to their elements and connected to their nodes, the cases, the actions in their cases and on
/// the member under them, the combinations by factor, the force units; the same GlobalIds at a second export; nothing of it
/// when not asked for; an import of the file ignores it.
/// </summary>
public class StructuralIfcTests
{
    sealed record Frame(Fixture F, string First, string Slab, string Room, string Point, string[] Columns, string[] Beams);

    static Frame Build()
    {
        var f = LoadCaseTests.F();
        var ground = f.Storey;
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        const string concrete = "Concrete C25/30";
        var colType = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30x30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 }, ["material"] = concrete })!["id"]!;
        var beamType = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "B30x50", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.5 }, ["material"] = concrete })!["id"]!;
        var slabType = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Floor 20",
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Screed", ["thickness"] = 0.05 }, new JsonObject { ["material"] = concrete, ["thickness"] = 0.2, ["core"] = true }),
        })!["id"]!;
        foreach (var (name, rho) in new[] { ("Screed", 2200.0), (concrete, 2500.0) })
            f.Run("material.setData", new JsonObject
            {
                ["id"] = f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id,
                ["params"] = new JsonArray(new JsonObject { ["pset"] = "Pset_MaterialCommon", ["name"] = "MassDensity", ["value"] = rho }),
            });
        (double X, double Y)[] corners = [(0, 0), (6, 0), (6, 5), (0, 5)];
        var columns = corners.Select(c => (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = ground, ["x"] = c.X, ["y"] = c.Y, ["typeId"] = colType })!["id"]!).ToArray();
        var beams = Enumerable.Range(0, 4).Select(i => (string)f.Run("structure.addBeam", new JsonObject
        {
            ["storeyId"] = ground, ["x1"] = corners[i].X, ["y1"] = corners[i].Y, ["x2"] = corners[(i + 1) % 4].X, ["y2"] = corners[(i + 1) % 4].Y, ["typeId"] = beamType,
        })!["id"]!).ToArray();
        var slab = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = first, ["points"] = new JsonArray(new JsonArray(-0.15, -0.15), new JsonArray(6.15, -0.15), new JsonArray(6.15, 5.15), new JsonArray(-0.15, 5.15)), ["typeId"] = slabType,
        })!["id"]!;
        foreach (var (x, y) in corners) f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = ground, ["x"] = x, ["y"] = y });
        f.Run("analysis.defaultCases", []);
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = first, ["x1"] = 1, ["y1"] = 0.5, ["x2"] = 5, ["y2"] = 4.5, ["function"] = "Living" })!["id"]!;
        var point = (string)f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = first, ["at"] = new JsonArray(3, 0), ["value"] = 10, ["caseId"] = LoadCaseTests.IdOf(f, "G2") })!["id"]!;
        return new Frame(f, first, slab, room, point, columns, beams);
    }

    static StepFile Export(Fixture f, bool structural = true, string schema = "IFC4")
    {
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "frame.ifc", new IfcExportOptions(schema, Structural: structural));
        return StepFile.Read(ms.ToArray());
    }

    static byte[] Bytes(Fixture f, bool structural)
    {
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "frame.ifc", new IfcExportOptions(Structural: structural));
        return ms.ToArray();
    }

    static List<int> Refs(object? list) => (list as List<object?>)?.OfType<StepRef>().Select(r => r.Id).ToList() ?? [];
    static int Ref(object? r) => r is StepRef x ? x.Id : -1;
    static string? Enum(object? e) => (e as StepEnum)?.Value;

    /// <summary>The topology item of a structural item's representation, and the representation's type (Vertex, Edge, Face).</summary>
    static (StepEntity Item, string Type) Topology(StepFile step, StepEntity item)
    {
        var shape = step.Get(item[6])!;
        var rep = step.Get(Refs(shape[2]).Select(i => new StepRef(i)).Single())!;
        Assert.Equal("IFCTOPOLOGYREPRESENTATION", rep.Type);
        Assert.Equal("Reference", (string?)rep[1]);
        Assert.Equal("Reference", (string?)step.Get(rep[0])![0]);
        return (step.Get(new StepRef(Refs(rep[3]).Single()))!, (string)rep[2]!);
    }

    [Fact]
    public void The_view_writes_the_analytical_model_its_loads_and_combinations()
    {
        var fr = Build();
        var f = fr.F;
        var am = AnalyticalModel.Build(f.Model, f.Session.Reader.Services, new AnalyticalOptions());
        var step = Export(f);

        // The analysis model, 3D, serving the building, loaded by the cases and the combinations.
        var model = Assert.Single(step.OfType("IFCSTRUCTURALANALYSISMODEL"));
        Assert.Equal("LOADING_3D", Enum(model[5]));
        var services = Assert.Single(step.OfType("IFCRELSERVICESBUILDINGS"));
        Assert.Equal(model.Id, Ref(services[4]));
        Assert.Equal("IFCBUILDING", step.Get(new StepRef(Refs(services[5]).Single()))!.Type);
        var cases = step.OfType("IFCSTRUCTURALLOADCASE").ToList();
        var combos = step.OfType("IFCSTRUCTURALLOADGROUP").ToList();
        Assert.Equal(5, cases.Count);
        Assert.Equal(cases.Select(c => c.Id).Concat(combos.Select(c => c.Id)).Order(), Refs(model[7]).Order());
        Assert.Equal("IFCLOCALPLACEMENT", step.Get(model[9])!.Type);

        // Nodes, bars, shells: as many as the analytical model has, all grouped in the analysis model.
        var nodes = step.OfType("IFCSTRUCTURALPOINTCONNECTION").ToList();
        var bars = step.OfType("IFCSTRUCTURALCURVEMEMBER").ToList();
        var shells = step.OfType("IFCSTRUCTURALSURFACEMEMBER").ToList();
        Assert.Equal(am.Nodes.Count, nodes.Count);
        Assert.Equal(am.Frames.Count, bars.Count);
        Assert.Equal(am.Shells.Count, shells.Count);
        Assert.True(bars.Count >= 8);
        Assert.NotEmpty(shells);
        Assert.All(nodes.Concat(bars).Concat(shells), x => Assert.Equal(Ref(model[9]), Ref(x[5])));
        var grouped = step.OfType("IFCRELASSIGNSTOGROUP").Single(r => Ref(r[6]) == model.Id);
        Assert.Equal(nodes.Select(n => n.Id).Concat(bars.Select(b => b.Id)).Concat(shells.Select(s => s.Id)).Order(), Refs(grouped[4]).Order());

        // A node on a vertex; the four feet held, fixed, by a boundary condition of booleans.
        var vertexOfNode = nodes.ToDictionary(n => Topology(step, n).Item.Id, n => n.Id);
        Assert.All(nodes, n => Assert.Equal("Vertex", Topology(step, n).Type));
        var supports = nodes.Where(n => n[7] is StepRef).ToList();
        Assert.Equal(am.Nodes.Count(n => n.Restraints != null), supports.Count);
        Assert.Equal(4, supports.Count);
        var cond = step.Get(supports[0][7])!;
        Assert.Equal("IFCBOUNDARYNODECONDITION", cond.Type);
        Assert.All(Enumerable.Range(1, 6), i => Assert.Equal(new StepTyped("IFCBOOLEAN", new StepEnum("T")), cond[i]));

        // A bar on an edge between two nodes' vertices, its axis local 2, rigidly joined, connected to both nodes.
        var connects = step.OfType("IFCRELCONNECTSSTRUCTURALMEMBER").ToList();
        foreach (var b in bars)
        {
            Assert.Equal("RIGID_JOINED_MEMBER", Enum(b[7]));
            Assert.Equal("IFCDIRECTION", step.Get(b[8])!.Type);
            var (edge, type) = Topology(step, b);
            Assert.Equal("Edge", type);
            Assert.Equal("IFCEDGE", edge.Type);
            var ends = new[] { vertexOfNode[Ref(edge[0])], vertexOfNode[Ref(edge[1])] };
            Assert.Equal(ends.Order(), connects.Where(c => Ref(c[4]) == b.Id).Select(c => Ref(c[5])).Order());
        }
        // A beam's axis (local 2) is vertical.
        var beam = bars.First(b => (string?)b[2] is { } n && n.StartsWith(f.Model.Get(fr.Beams[0])!.Name));
        Assert.Equal(1.0, Math.Abs((double)((List<object?>)step.Get(beam[8])![0]!)[2]!), 6);
        // Sections: a profile set of a rectangle 30 × 50 and the concrete, for the beams.
        var profileRel = step.OfType("IFCRELASSOCIATESMATERIAL").Single(r => Refs(r[4]).Contains(beam.Id));
        var set = step.Get(profileRel[5])!;
        Assert.Equal("IFCMATERIALPROFILESET", set.Type);
        var mp = step.Get(new StepRef(Refs(set[2]).Single()))!;
        Assert.Equal("Concrete C25/30", (string?)step.Get(mp[2])![0]);
        var profile = step.Get(mp[3])!;
        Assert.Equal("IFCRECTANGLEPROFILEDEF", profile.Type);
        Assert.Equal([0.3, 0.5], new[] { (double)profile[3]!, (double)profile[4]! });

        // A shell on a face of its plane, bounded by its nodes' edges, its core 20 cm thick, connected to each of its nodes.
        foreach (var s in shells)
        {
            Assert.Equal("SHELL", Enum(s[7]));
            Assert.Equal(0.2, (double)s[8]!, 9);
            var (face, type) = Topology(step, s);
            Assert.Equal("Face", type);
            Assert.Equal("IFCFACESURFACE", face.Type);
            Assert.Equal("IFCPLANE", step.Get(face[1])!.Type);
            var loop = step.Get(step.Get(new StepRef(Refs(face[0]).Single()))![0])!;
            var edges = Refs(loop[0]).Select(i => step.Get(step.Entities[i][2])!).ToList();
            Assert.All(edges, e => Assert.Equal("IFCEDGE", e.Type));
            Assert.Equal(edges.Count, connects.Count(c => Ref(c[4]) == s.Id));
        }

        // Each member assigned to its physical element: a column's bar to the IfcColumn, the floor's shells to the IfcSlab.
        var assigns = step.OfType("IFCRELASSIGNSTOPRODUCT").ToList();
        var column = step.OfType("IFCCOLUMN").Single(c => (string?)c[0] == fr.Columns[0]);
        Assert.NotEmpty(Refs(assigns.Single(a => Ref(a[6]) == column.Id)[4]));
        var slab = step.OfType("IFCSLAB").Single(c => (string?)c[0] == fr.Slab);
        Assert.Equal(shells.Select(s => s.Id).Order(), Refs(assigns.Single(a => Ref(a[6]) == slab.Id)[4]).Order());

        // The cases: G1 carries the self weight (0, 0, −1).
        var g1 = cases.Single(c => (string?)c[2] == "G1");
        Assert.Equal(LoadCaseTests.IdOf(f, "G1"), (string?)g1[0]);
        Assert.Equal("LOAD_CASE", Enum(g1[5]));
        Assert.Equal("PERMANENT_G", Enum(g1[6]));
        Assert.Equal("DEAD_LOAD_G", Enum(g1[7]));
        Assert.Equal([0.0, 0.0, -1.0], ((List<object?>)g1[10]!).Select(x => (double)x!));
        Assert.Null(cases.Single(c => (string?)c[2] == "G2")[10]);

        // The actions: the floor's screed (−1078.73 N/m²) in G2 on its own slab, the room's 2 kN/m² in Q-A on the floor,
        // the point load (−10 kN) in G2 on the beam under it.
        var planar = step.OfType("IFCSTRUCTURALPLANARACTION").ToList();
        Assert.Equal(2, planar.Count);
        StepEntity Load(StepEntity a) => step.Get(a[7])!;
        var screed = planar.Single(a => Load(a).Type == "IFCSTRUCTURALLOADPLANARFORCE" && Math.Abs((double)Load(a)[3]! + 0.05 * 2200 * 9.80665) < 1e-3);
        var occupancy = planar.Single(a => Math.Abs((double)Load(a)[3]! + 2000) < 1e-6);
        Assert.All(planar, a => { Assert.Equal("GLOBAL_COORDS", Enum(a[8])); Assert.Equal("CONST", Enum(a[11])); Assert.Equal("Face", Topology(step, a).Type); });
        var point = Assert.Single(step.OfType("IFCSTRUCTURALPOINTACTION"));
        Assert.Equal(fr.Point, (string?)point[0]);
        Assert.Equal("IFCSTRUCTURALLOADSINGLEFORCE", Load(point).Type);
        Assert.Equal(-10000.0, (double)Load(point)[3]!, 6);
        var inCase = step.OfType("IFCRELASSIGNSTOGROUP").Where(r => Ref(r[6]) != model.Id).ToDictionary(r => (string)step.Get(r[6])![2]!, r => Refs(r[4]));
        Assert.Equal(new[] { screed.Id, point.Id }.Order(), inCase["G2"].Order());
        Assert.Equal([occupancy.Id], inCase["Q-A"]);
        var on = step.OfType("IFCRELCONNECTSSTRUCTURALACTIVITY").ToDictionary(r => Ref(r[5]), r => step.Get(r[4])!);
        Assert.Equal("IFCSTRUCTURALSURFACEMEMBER", on[screed.Id].Type);
        Assert.Equal("IFCSTRUCTURALSURFACEMEMBER", on[occupancy.Id].Type);
        Assert.Equal("IFCSTRUCTURALCURVEMEMBER", on[point.Id].Type);
        Assert.StartsWith(f.Model.Get(fr.Beams[0])!.Name, (string?)on[point.Id][2]);

        // The combinations, by factor over their cases (only G2 and Q-A hold loads of their own besides G1's self weight).
        var expected = (JsonArray)f.Session.Query("analysis.combinations", [])!["combinations"]!;
        Assert.Equal(expected.Count, combos.Count);
        Assert.All(combos, c => Assert.Equal("LOAD_COMBINATION", Enum(c[5])));
        var uls = combos.Single(c => (string?)c[3] == $"uls/{LoadCaseTests.IdOf(f, "Q-A")}");
        Assert.Equal("1.35 G1 + 1.35 G2 + 1.5 Q-A", (string?)uls[2]);
        var byFactor = step.OfType("IFCRELASSIGNSTOGROUPBYFACTOR").Where(r => Ref(r[6]) == uls.Id)
            .ToDictionary(r => (string)step.Get(new StepRef(Refs(r[4]).Single()))![2]!, r => (double)r[7]!);
        Assert.Equal(new Dictionary<string, double> { ["G1"] = 1.35, ["G2"] = 1.35, ["Q-A"] = 1.5 }, byFactor);

        // The units: N, N/m, N/m², in the project's assignment.
        var units = Refs(Assert.Single(step.OfType("IFCUNITASSIGNMENT"))[0]).Select(i => step.Entities[i]).ToList();
        Assert.Contains(units, u => u.Type == "IFCSIUNIT" && Enum(u[1]) == "FORCEUNIT" && Enum(u[3]) == "NEWTON");
        Assert.Contains(units, u => u.Type == "IFCDERIVEDUNIT" && Enum(u[1]) == "LINEARFORCEUNIT");
        Assert.Contains(units, u => u.Type == "IFCDERIVEDUNIT" && Enum(u[1]) == "PLANARFORCEUNIT");
    }

    static readonly string[] ViewTypes =
    [
        "IFCSTRUCTURALANALYSISMODEL", "IFCSTRUCTURALPOINTCONNECTION", "IFCSTRUCTURALCURVEMEMBER", "IFCSTRUCTURALSURFACEMEMBER", "IFCSTRUCTURALLOADCASE",
        "IFCSTRUCTURALLOADGROUP", "IFCSTRUCTURALPLANARACTION", "IFCSTRUCTURALPOINTACTION", "IFCRELCONNECTSSTRUCTURALMEMBER", "IFCRELCONNECTSSTRUCTURALACTIVITY",
        "IFCRELASSIGNSTOPRODUCT", "IFCRELASSIGNSTOGROUPBYFACTOR", "IFCRELSERVICESBUILDINGS",
    ];

    [Fact]
    public void A_second_export_keeps_the_GlobalIds_and_an_export_not_asking_for_it_has_none()
    {
        var f = Build().F;
        Dictionary<string, List<string>> Ids(StepFile s) => ViewTypes.ToDictionary(t => t, t => s.OfType(t).Select(e => (string)e[0]!).Order(StringComparer.Ordinal).ToList());
        var (one, two) = (Ids(Export(f)), Ids(Export(f)));
        foreach (var t in ViewTypes)
        {
            Assert.NotEmpty(one[t]);
            Assert.Equal(one[t], two[t]);
            Assert.All(one[t], g => Assert.True(GlobalId.IsValid(g), $"{t}: {g}"));
        }
        Assert.Equal(one.Values.Sum(v => v.Count), one.Values.SelectMany(v => v).Distinct().Count());

        var plain = Export(f, structural: false);
        Assert.All(ViewTypes.Where(t => t.StartsWith("IFCSTRUCTURAL")), t => Assert.Empty(plain.OfType(t)));
        Assert.DoesNotContain(plain.OfType("IFCSIUNIT"), u => Enum(u[1]) == "FORCEUNIT");
        Assert.DoesNotContain(plain.OfType("IFCDERIVEDUNIT"), u => Enum(u[1]) is "LINEARFORCEUNIT" or "PLANARFORCEUNIT");
        // The loads and the cases are never elements of the file.
        Assert.Empty(plain.OfType("IFCSTRUCTURALLOADCASE"));
        Assert.Equal(Export(f).OfType("IFCBUILDINGELEMENTPROXY").Count(), plain.OfType("IFCBUILDINGELEMENTPROXY").Count());
    }

    [Fact]
    public void IFC_4_3_writes_the_view_too_its_derived_units_named()
    {
        var f = Build().F;
        var step = Export(f, schema: "IFC4X3");
        Assert.Single(step.OfType("IFCSTRUCTURALANALYSISMODEL"));
        Assert.All(step.OfType("IFCDERIVEDUNIT").Where(u => Enum(u[1]) is "LINEARFORCEUNIT" or "PLANARFORCEUNIT"), u => Assert.Equal(4, u.Args.Count));
    }

    [Fact]
    public void An_import_ignores_the_view()
    {
        var f = Build().F;
        int Imported(byte[] bytes)
        {
            var g = new Fixture(new Lapin.Architecture.ArchitectureModule(), new Lapin.Structure.StructureModule(), new Lapin.Analysis.AnalysisModule());
            var r = IfcImport.Run(g.Session, bytes, "frame.ifc", null, out _);
            Assert.Empty(g.Model.OfClass(Lapin.Analysis.LoadCases.Class));
            return r.Elements + r.References;
        }
        Assert.Equal(Imported(Bytes(f, false)), Imported(Bytes(f, true)));
    }
}
