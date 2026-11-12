using System.Text.Json.Nodes;
using Lapin.Geometry;
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
    [Fact]
    public void Released_ends_are_written_as_their_connections_condition_and_eccentric_ends_with_their_offsets()
    {
        // The steel hall: its secondary members pinned on cleats and gussets, their ends on their hosts' axes by arms (§9.1).
        var f = AnalysisRunTests.Hall();
        var am = AnalyticalModel.Build(f.Model, f.Session.Reader.Services, new AnalyticalOptions());
        var releases = FrameReleases.Of(am, f.Model);
        var step = Export(f);
        var plain = step.OfType("IFCRELCONNECTSSTRUCTURALMEMBER").ToList();
        var eccentric = step.OfType("IFCRELCONNECTSWITHECCENTRICITY").ToList();
        // Every bar end connected once, an eccentric one with its offset.
        Assert.Equal(2 * am.Frames.Count, plain.Count + eccentric.Count);
        // The joints' own offsets (arms), not an unsymmetric section's centroid off the line (the hall's C purlins'): that is the
        // profile's to say.
        Assert.Equal(am.Frames.Count(x => x.OffI.Length > 1e-9) + am.Frames.Count(x => x.OffJ.Length > 1e-9), eccentric.Count);
        Assert.Contains(am.Frames, x => AnalyticalModel.CentroidOffset(x).Length > 1e-9);
        // An eccentric bar's edge ends at its own vertex, the member's end, where the eccentricity's point on it is.
        var bars = step.OfType("IFCSTRUCTURALCURVEMEMBER").ToDictionary(b => b.Id);
        foreach (var r in eccentric)
        {
            var (edge, _) = Topology(step, bars[Ref(r[4])]);
            var c = step.Get(r[10])!;
            var end = new[] { edge[0], edge[1] }.Select(v => (List<object?>)step.Get(step.Get(v)![0])![0]!).Select(xs => new Vec3((double)xs[0]!, (double)xs[1]!, (double)xs[2]!));
            var on = (List<object?>)step.Get(c[0])![0]!;
            var at = new Vec3((double)on[0]!, (double)on[1]!, (double)on[2]!);
            Assert.Contains(end, p => (p - at).Length < 1e-6);
            var node = (List<object?>)step.Get(step.Get(c[1])![0])![0]!;
            Assert.True((at - new Vec3((double)node[0]!, (double)node[1]!, (double)node[2]!)).Length > 1e-3);
        }
        var ecc = step.Get(eccentric[0][10])!;
        Assert.Equal("IFCCONNECTIONPOINTECCENTRICITY", ecc.Type);
        Assert.Equal("IFCCARTESIANPOINT", step.Get(ecc[0])!.Type);
        Assert.Equal("IFCVERTEXPOINT", step.Get(ecc[1])!.Type);
        var offset = Math.Sqrt(Enumerable.Range(2, 3).Sum(i => (double)ecc[i]! * (double)ecc[i]!));
        Assert.InRange(offset, 0.01, 1.0);
        // A pinned end: held in translation and torsion, free to turn about local 2 and 3.
        var conditions = plain.Concat(eccentric).Where(r => r[6] is StepRef).Select(r => step.Get(r[6])!).ToList();
        Assert.Equal(releases.Values.Count(r => r.I.Any(x => x)) + releases.Values.Count(r => r.J.Any(x => x)), conditions.Count);
        var pinned = conditions.First(c => (string?)c[0] == "pinned");
        Assert.Equal("IFCBOUNDARYNODECONDITION", pinned.Type);
        StepTyped B(bool b) => new("IFCBOOLEAN", new StepEnum(b ? "T" : "F"));
        Assert.Equal([B(true), B(true), B(true), B(true), B(false), B(false)], Enumerable.Range(1, 6).Select(i => pinned[i]));
        // The far end of a bar pinned at both: its twist free too (held at its first end only).
        Assert.Contains(conditions, c => (string?)c[0] == "released" && Equals(c[4], B(false)) && Equals(c[5], B(false)) && Equals(c[6], B(false)) && Equals(c[3], B(true)));
        // A frame's concrete beams: rigidly joined, no condition.
        var concrete = Export(Build().F);
        Assert.All(concrete.OfType("IFCRELCONNECTSSTRUCTURALMEMBER"), r => Assert.False(r[6] is StepRef));
    }
    [Fact]
    public void An_unsymmetric_bar_is_written_on_its_line_its_centroid_left_to_its_profile()
    {
        // A UPN 200 column on a pad footing: the curve member on the box centre's line between its two nodes, plainly connected
        // at both ends; its profile a parametric U, whose origin IFC puts on the curve at the middle of its bounding box, so a
        // reader finds the centroid off the line from the profile, as the analysis takes it (no eccentricity besides).
        var f = LoadCaseTests.F();
        var type = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = "UPN 200", ["material"] = "S355" })!["id"]!;
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0, ["typeId"] = type });
        f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 });
        var am = AnalyticalModel.Build(f.Model, f.Session.Reader.Services, new AnalyticalOptions());
        var bar = Assert.Single(am.Frames);
        Assert.True(AnalyticalModel.CentroidOffset(bar).Length > 0.005);
        var step = Export(f);
        Assert.Empty(step.OfType("IFCRELCONNECTSWITHECCENTRICITY"));
        Assert.Equal(2, step.OfType("IFCRELCONNECTSSTRUCTURALMEMBER").Count());
        var member = Assert.Single(step.OfType("IFCSTRUCTURALCURVEMEMBER"));
        var set = step.Get(step.OfType("IFCRELASSOCIATESMATERIAL").Single(r => Refs(r[4]).Contains(member.Id))[5])!;
        var profile = step.Get(step.Get(new StepRef(Refs(set[2]).Single()))![3])!;
        Assert.Equal("IFCUSHAPEPROFILEDEF", profile.Type);
        // Its values as the analysis takes them, in Pset_ProfileMechanical (MomentOfInertiaY bending in the depth: I33).
        var props = SectionProperties.Of(bar.Source.Section.Shape)!;
        var pset = step.OfType("IFCPROFILEPROPERTIES").Single(p => Ref(p[3]) == profile.Id);
        Assert.Equal("Pset_ProfileMechanical", (string?)pset[0]);
        var values = Refs(pset[2]).Select(i => step.Entities[i]).ToDictionary(p => (string)p[0]!, p => (double)((StepTyped)p[2]!).Value!);
        Assert.Equal(props.Area, values["CrossSectionArea"], 12);
        Assert.Equal(1, values["MomentOfInertiaY"] / props.I33, 9);
        Assert.Equal(1, values["MomentOfInertiaZ"] / props.I22, 9);
        Assert.Equal(1, values["TorsionalConstantX"] / props.J, 9);
    }

    [Fact]
    public void A_line_load_lies_on_the_bars_it_runs_along_and_the_materials_carry_what_the_analysis_takes()
    {
        // Three columns in a row and two beams 0–3 m and 3–6 m: a line load along both is two actions, each on its beam, over
        // the stretch the beam covers (as the analysis maps it); one along a single beam stays one action, with its own id.
        var f = LoadCaseTests.F();
        var col = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30x30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 }, ["material"] = "Concrete C25/30" })!["id"]!;
        var bt = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "B30x50", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.5 }, ["material"] = "Concrete C25/30" })!["id"]!;
        foreach (var x in new[] { 0.0, 3, 6 })
        {
            f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = 0, ["typeId"] = col });
            f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = 0 });
        }
        var beams = new[] { (0.0, 3.0), (3.0, 6.0) }.Select(b => (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = b.Item1, ["y1"] = 0, ["x2"] = b.Item2, ["y2"] = 0, ["typeId"] = bt })!["id"]!).ToArray();
        f.Run("analysis.defaultCases", []);
        var am = AnalyticalModel.Build(f.Model, f.Session.Reader.Services, new AnalyticalOptions());
        var z = am.Nodes.Max(n => n.At.Z);
        var both = (string)f.Run("analysis.addLineLoad", new JsonObject { ["storeyId"] = f.Storey, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(6, 0), ["z"] = z, ["value"] = 10 })!["id"]!;
        var one = (string)f.Run("analysis.addLineLoad", new JsonObject { ["storeyId"] = f.Storey, ["a"] = new JsonArray(0.5, 0), ["b"] = new JsonArray(2.5, 0), ["z"] = z, ["value"] = 4 })!["id"]!;
        var step = Export(f);
        var curves = step.OfType("IFCSTRUCTURALCURVEACTION").ToList();
        Assert.Equal(3, curves.Count);
        Assert.Single(curves, c => (string?)c[0] == one);
        Assert.DoesNotContain(curves, c => (string?)c[0] == both);
        var on = step.OfType("IFCRELCONNECTSSTRUCTURALACTIVITY").ToDictionary(r => Ref(r[5]), r => step.Get(r[4])!);
        Assert.All(curves, c => Assert.Equal("IFCSTRUCTURALCURVEMEMBER", on[c.Id].Type));
        // Each stretch from its beam's start to its end, on its beam.
        foreach (var c in curves.Where(c => (string?)c[0] != one))
        {
            var (edge, _) = Topology(step, c);
            var xs = new[] { edge[0], edge[1] }.Select(v => (double)((List<object?>)step.Get(step.Get(v)![0])![0]!)[0]!).Order().ToArray();
            var beam = f.Model.Get(xs[0] < 1.5 ? beams[0] : beams[1])!;
            Assert.Equal(xs[0] < 1.5 ? [0.0, 3.0] : [3.0, 6.0], xs.Select(x => Math.Round(x, 6)));
            Assert.StartsWith(beam.Name, (string?)on[c.Id][2]);
            Assert.Equal(-10000.0, (double)step.Get(c[7])![3]!, 6);
        }
        // The same GlobalIds at a second export.
        Assert.Equal(curves.Select(c => (string)c[0]!).Order(), Export(f).OfType("IFCSTRUCTURALCURVEACTION").Select(c => (string)c[0]!).Order());

        // The concrete's E and ν by its class (EN 1992-1-1), each saying so; nothing of it without the view.
        var props = step.OfType("IFCPROPERTYSINGLEVALUE").ToList();
        var e = props.Single(p => (string?)p[0] == "YoungModulus");
        Assert.Equal(new StepTyped("IFCMODULUSOFELASTICITYMEASURE", 31e9), e[2]);
        Assert.StartsWith("Lapin: EN 1992-1-1", (string?)e[1]);
        Assert.Equal(new StepTyped("IFCPOSITIVERATIOMEASURE", 0.2), props.Single(p => (string?)p[0] == "PoissonRatio")[2]);
        var mechanical = step.OfType("IFCMATERIALPROPERTIES").Single(m => (string?)m[0] == "Pset_MaterialMechanical");
        Assert.Equal("Concrete C25/30", (string?)step.Get(mechanical[3])![0]);
        Assert.DoesNotContain(Export(f, structural: false).OfType("IFCPROPERTYSINGLEVALUE"), p => (string?)p[0] is "YoungModulus" or "PoissonRatio");
    }

    [Fact]
    public void A_materials_own_values_are_kept_and_its_density_added_only_where_it_has_none()
    {
        // The steel hall's steel states E, ν and its density: written once each, as the project has them (no source said).
        var f = AnalysisRunTests.Hall();
        var step = Export(f);
        var steel = step.OfType("IFCMATERIAL").Single(m => (string?)m[0] == "Steel");
        var mine = step.OfType("IFCMATERIALPROPERTIES").Where(m => Ref(m[3]) == steel.Id).SelectMany(m => Refs(m[2])).Select(i => step.Entities[i]).ToList();
        foreach (var name in new[] { "YoungModulus", "PoissonRatio", "MassDensity" })
        {
            var p = Assert.Single(mine, x => (string?)x[0] == name);
            Assert.Null(p[1]);
        }
    }

    [Fact]
    public void A_release_is_written_in_the_members_IFC_axes_its_Axis_as_z()
    {
        // A level beam pinned about its strong axis (Lapin's local 3, horizontal, square to it) at one end, and free to slide
        // along its depth (Lapin's local 2) at the other: IFC's member axes have z on the Axis (local 2, up) and y = z × x, so
        // the first is free about y, the second along z.
        var f = LoadCaseTests.F();
        var col = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30x30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 }, ["material"] = "Concrete C25/30" })!["id"]!;
        var bt = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "B30x50", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.5 }, ["material"] = "Concrete C25/30" })!["id"]!;
        foreach (var x in new[] { 0.0, 6 })
        {
            f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = 0, ["typeId"] = col });
            f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = 0 });
        }
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["typeId"] = bt })!["id"]!;
        f.Run("analysis.setReleases", new JsonObject { ["ids"] = new JsonArray(beam), ["i"] = new JsonArray(false, false, false, false, false, true), ["j"] = new JsonArray(false, true, false, false, false, false) });
        var step = Export(f);
        var member = step.OfType("IFCSTRUCTURALCURVEMEMBER").Single(b => ((string?)b[2])!.StartsWith(f.Model.Get(beam)!.Name));
        Assert.Equal(1.0, (double)((List<object?>)step.Get(member[8])![0]!)[2]!, 9);
        var conds = step.OfType("IFCRELCONNECTSSTRUCTURALMEMBER").Where(r => Ref(r[4]) == member.Id && r[6] is StepRef)
            .Select(r => (At: (double)((List<object?>)step.Get(Topology(step, step.Get(r[5])!).Item[0])![0]!)[0]!, C: step.Get(r[6])!)).OrderBy(x => x.At).ToList();
        Assert.Equal(2, conds.Count);
        StepTyped B(bool b) => new("IFCBOOLEAN", new StepEnum(b ? "T" : "F"));
        Assert.Equal([B(true), B(true), B(true), B(true), B(false), B(true)], Enumerable.Range(1, 6).Select(i => conds[0].C[i]));
        Assert.Equal("pinned about the strong axis", (string?)conds[0].C[0]);
        Assert.Equal([B(true), B(true), B(false), B(true), B(true), B(true)], Enumerable.Range(1, 6).Select(i => conds[1].C[i]));
    }
}
