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

/// <summary>Steel, stage 3 (decision 273): the connections of portal frame halls — found from the topology, their plates, bolts and anchors, setbacks, rebuilt with the hall, IFC.</summary>
public class SteelNodeTests
{
    static Fixture F() => new(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());
    static double Tan(double deg) => Math.Tan(deg * Math.PI / 180);
    static JsonArray J(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);

    /// <summary>A hall as stage 2 and 3 built it: its secondary steel off (decision 275; SteelSecondaryTests).</summary>
    static string Hall(Fixture f, JsonObject? attrs = null, string? style = null)
    {
        var o = attrs ?? [];
        if (!o.ContainsKey("secondary")) o["secondary"] = false;
        var a = new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = o };
        if (style != null) a["style"] = style;
        return (string)f.Run("steel.addHall", a)!["id"]!;
    }

    static Dictionary<string, Entity> Members(Fixture f, string hall) => Halls.Members(f.Model, hall);
    static Dictionary<string, Entity> Nodes(Fixture f, string hall) => Halls.Nodes(f.Model, hall);

    static IEnumerable<GeometryPart> Parts(Fixture f, string id, string prefix) => f.Session.Geometry(id)!.Parts.Where(p => p.Key!.StartsWith(prefix, StringComparison.Ordinal));
    static IEnumerable<Vec3> Body(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Where(p => p.Key == "body").SelectMany(p => p.Solid.Vertices);
    static (Vec3 Min, Vec3 Max) Box(GeometryPart p) => Measure.Bounds(p.Solid)!.Value;

    // ------------------------------------------------------------ topology

    [Fact]
    public void A_halls_connections_are_those_its_members_topology_asks_for()
    {
        var f = F();
        // The hall without its connections, then each found from how its members meet.
        var hall = Hall(f, new JsonObject { ["nodes"] = false });
        Assert.Empty(f.Model.OfClass(SteelNodes.Class));
        var found = f.Run("steel.findNodes", new JsonObject())!["ids"]!.AsArray().Select(x => f.Model.Get((string)x!)!).ToList();
        int Count(IEnumerable<Entity> ns, string kind) => ns.Count(n => SteelNodes.KindOf(n) == kind);
        // Six frames: eight columns and ten gable posts on their bases, eight eaves, six apexes, ten posts under their rafters.
        Assert.Equal(18, Count(found, "base"));
        Assert.Equal(8, Count(found, "eaves"));
        Assert.Equal(6, Count(found, "apex"));
        Assert.Equal(10, Count(found, "post"));
        // Nothing new the second time.
        Assert.Empty(f.Run("steel.findNodes", new JsonObject())!["ids"]!.AsArray());
        // The hall plans the same ones for its frames, each knowing its place.
        var g = F();
        var made = Nodes(g, Hall(g)).Values.ToList();
        Assert.Equal(42, made.Count);
        foreach (var kind in SteelNodes.Kinds) Assert.Equal(Count(found, kind), Count(made, kind));
        Assert.Contains("eaves@f1:raf:a@start", Nodes(g, g.Model.OfClass(Halls.Class).Single().Id).Keys);
    }

    [Fact]
    public void Two_members_in_line_end_to_end_are_spliced_with_two_end_plates()
    {
        var f = F();
        string M(double x1, double x2) => (string)f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = 0, ["x2"] = x2, ["y2"] = 0, ["z1"] = 3, ["z2"] = 3 })!["id"]!;
        var (a, b) = (M(0, 4), M(4, 9));
        var ids = f.Run("steel.findNodes", new JsonObject { ["ids"] = new JsonArray(a, b) })!["ids"]!.AsArray();
        var node = f.Model.Get((string)Assert.Single(ids)!)!;
        Assert.Equal("splice", SteelNodes.KindOf(node));
        // IPE 200: M16 bolts, plates 20 mm (the bolt's diameter, rounded up to 5 mm), each member set back by its plate; two plates between them, bolts through both.
        var spec = SteelNodes.Spec(f.Model, node);
        Assert.Equal(0.016, spec.Bolt, 9);
        Assert.Equal(0.02, spec.Plate, 9);
        Assert.Equal(4 - 0.02, Body(f, a).Max(p => p.X), 6);
        Assert.Equal(4 + 0.02, Body(f, b).Min(p => p.X), 6);
        var plates = Parts(f, node.Id, "steel-plate").Select(Box).OrderBy(x => x.Min.X).ToList();
        Assert.Equal(2, plates.Count);
        Assert.Equal(4 - 0.02, plates[0].Min.X, 6);
        Assert.Equal(4 + 0.02, plates[1].Max.X, 6);
        var bolts = Parts(f, node.Id, "steel-bolt").ToList();
        Assert.Equal(2 * SteelNodes.Build(f.Model, node).Rows, bolts.Count);
        Assert.All(bolts, p => Assert.True(Box(p).Min.X < 4 - 0.02 && Box(p).Max.X > 4 + 0.02));
    }

    // ------------------------------------------------------------ eaves, apex, posts

    [Fact]
    public void An_eaves_sets_the_rafter_back_by_its_end_plate_and_bolts_it_to_the_column_flange()
    {
        var f = F();
        var hall = Hall(f);
        var raf = Members(f, hall)["f1:raf:a"];
        var col = Members(f, hall)["f1:col:a"];
        var node = Nodes(f, hall)["eaves@f1:raf:a@start"];
        var spec = SteelNodes.Spec(f.Model, node);
        // An IPE 360 rafter: M20 bolts, a 20 mm plate standing 80 mm above it, the bolts 100 mm apart across.
        Assert.Equal(0.02, spec.Bolt, 9);
        Assert.Equal(0.02, spec.Plate, 9);
        Assert.Equal(0.08, spec.Extension, 9);
        Assert.Equal(0.1, spec.Gauge, 9);
        Assert.Equal(-0.02, SteelEnd.Of(raf).Start.Extension, 9);
        // The rafter starts 20 mm off the column's face; the plate fills the gap, from below its haunch to 80 mm over its top.
        Assert.Equal(0.22, Body(f, raf.Id).Min(p => p.X), 6);
        var plate = Box(Assert.Single(Parts(f, node.Id, "steel-plate:1")));
        Assert.Equal(0.2, plate.Min.X, 6);
        Assert.Equal(0.22, plate.Max.X, 6);
        Assert.Equal(6 + 0.42 * Tan(10) + 0.08, plate.Max.Z, 6);
        Assert.True(plate.Min.Z < 6 + 0.42 * Tan(10) - 0.36 / Math.Cos(10 * Math.PI / 180) - 0.36);
        // Two bolts a row: one row in the extension, the rest between the flanges; each through the plate and the column's flange.
        var built = SteelNodes.Build(f.Model, node);
        var bolts = Parts(f, node.Id, "steel-bolt").Select(Box).ToList();
        Assert.Equal(2 * (built.Rows + 1), bolts.Count);
        Assert.All(bolts, b => Assert.Contains(Math.Round((b.Min.Y + b.Max.Y) / 2, 6), new[] { 5.95, 6.05 }));
        var tfc = Section.Catalogue["IPE 400"].Flange;
        Assert.All(bolts, b => Assert.True(b.Min.X < 0.2 - tfc && b.Max.X > 0.22));
        Assert.Contains(bolts, b => (b.Min.Z + b.Max.Z) / 2 > 6 + 0.42 * Tan(10));
        // The column's stiffeners: two each side of its web, in line with the rafter's top flange and the haunch's.
        Assert.Equal(4, Parts(f, node.Id, "steel-plate").Count(p => p.Name == "Stiffener"));
        Assert.Equal("M20×60", SteelNodes.P(Parts(f, node.Id, "steel-bolt").First().Properties!, "Size"));
        Assert.Equal(col.Id, SteelNodes.MembersOf(node)[0].Id);
    }

    [Fact]
    public void An_apex_bolts_two_end_plates_on_the_mitre_and_a_gable_post_hangs_on_a_fin_plate()
    {
        var f = F();
        var hall = Hall(f);
        var m = Members(f, hall);
        var apex = Nodes(f, hall)["apex@f1"];
        // Each rafter set back by its plate from the mitre: the plates meet on it at mid-span.
        Assert.Equal(10 - 0.02, Body(f, m["f1:raf:a"].Id).Max(p => p.X), 6);
        Assert.Equal(10 + 0.02, Body(f, m["f1:raf:b"].Id).Min(p => p.X), 6);
        var plates = Parts(f, apex.Id, "steel-plate").Select(Box).OrderBy(b => b.Min.X).ToList();
        Assert.Equal(2, plates.Count);
        Assert.Equal(9.98, plates[0].Min.X, 6);
        Assert.Equal(10, plates[0].Max.X, 6);
        Assert.Equal(10.02, plates[1].Max.X, 6);
        Assert.Equal(2 * SteelNodes.Build(f.Model, apex).Rows, Parts(f, apex.Id, "steel-bolt").Count());
        // A gable post: its top 20 mm under the rafter, a fin plate down from the rafter's underside, two bolts through the post's web.
        var post = m["f0:post:1"];
        var node = Nodes(f, hall)["post@f0:post:1@end"];
        double Under(double x) => 6 + (x + 0.2) * Tan(10) - 0.24 / Math.Cos(10 * Math.PI / 180);
        var under = Under(5);
        // The post's top follows the rafter's slope across its flanges (IPE 240: 120 mm wide), highest on the apex's side, the gap square to the rafter.
        Assert.Equal(Under(5.06) - 0.02 / Math.Cos(10 * Math.PI / 180), Body(f, post.Id).Max(p => p.Z), 6);
        var fin = Box(Assert.Single(Parts(f, node.Id, "steel-plate")));
        Assert.Equal(under, fin.Max.Z, 6);
        Assert.Equal(2, Parts(f, node.Id, "steel-bolt").Count());
    }

    // ------------------------------------------------------------ bases

    [Fact]
    public void A_column_base_stands_the_column_on_its_plate_and_anchors_it_pinned_or_fixed()
    {
        var f = F();
        var hall = Hall(f);
        var col = Members(f, hall)["f1:col:a"];
        var node = Nodes(f, hall)["base@f1:col:a@start"];
        var spec = SteelNodes.Spec(f.Model, node);
        // An IPE 400 column, pinned: a 25 mm plate, four M24 anchors inside the flanges, 750 mm into the concrete under 30 mm of grout.
        Assert.Equal(("pinned", 0.025, 0.024, 4, 0.75), (spec.Base, spec.Plate, spec.Bolt, spec.Anchors, spec.AnchorLength));
        Assert.Equal(0.025, Body(f, col.Id).Min(p => p.Z), 6);
        var plate = Box(Assert.Single(Parts(f, node.Id, "steel-plate")));
        Assert.Equal((0.0, 0.025), (Math.Round(plate.Min.Z, 9), Math.Round(plate.Max.Z, 9)));
        var anchors = Parts(f, node.Id, "steel-anchor").Select(Box).ToList();
        Assert.Equal(4, anchors.Count);
        Assert.All(anchors, a => Assert.Equal(-0.03 - 0.75, a.Min.Z, 6));
        Assert.All(anchors, a => Assert.True(Math.Abs((a.Min.X + a.Max.X) / 2) < 0.2 - Section.Catalogue["IPE 400"].Flange));
        // Fixed, hooked: the 30 m hall's IPE 600 columns on 40 mm plates, eight M30 anchors outside the flanges, ribs.
        var g = F();
        var big = Hall(g, new JsonObject { ["anchor"] = "hooked" }, "hall-30");
        var fixedNode = Nodes(g, big)["base@f1:col:a@start"];
        var fs = SteelNodes.Spec(g.Model, fixedNode);
        Assert.Equal(("fixed", 0.04, 0.03, 8), (fs.Base, fs.Plate, fs.Bolt, fs.Anchors));
        var hooked = Parts(g, fixedNode.Id, "steel-anchor").Select(Box).ToList();
        Assert.Equal(8, hooked.Count);
        Assert.All(hooked, a => Assert.True(Math.Abs((a.Min.X + a.Max.X) / 2) > 0.3 || Math.Abs(a.Max.X - a.Min.X) > 0.1));
        // A hook runs 150 mm out at the foot (5 diameters).
        Assert.All(hooked, a => Assert.True(Math.Max(a.Max.X - a.Min.X, a.Max.Y - a.Min.Y) > 0.15));
        Assert.Equal(2, Parts(g, fixedNode.Id, "steel-plate").Count(p => p.Name == "Stiffener"));
    }

    [Fact]
    public void Anchors_go_into_the_footing_under_the_column_and_stop_short_of_its_bottom()
    {
        var f = F();
        var hall = Hall(f);
        var col = Members(f, hall)["f1:col:a"];
        var node = Nodes(f, hall)["base@f1:col:a@start"];
        var at = Args.TryPoint(col.Attrs["at"])!.Value;
        var type = (string)f.Run("structure.createPadType", new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["length"] = 1.2, ["width"] = 1.2, ["height"] = 0.9 }), ["bottom"] = -1.0 })!["id"]!;
        var pad = (string)f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = f.Storey, ["x"] = at.X, ["y"] = at.Y, ["typeId"] = type })!["id"]!;
        // Under the column it rises to its base (decision 507): the anchors 750 mm into it from there.
        Assert.All(Parts(f, node.Id, "steel-anchor"), a => Assert.Equal(-0.75, Box(a).Min.Z, 6));
        // Given its own top 100 mm under the column: the anchors 750 mm into it from there.
        f.Run("structure.setPadLevel", new JsonObject { ["id"] = pad, ["top"] = -0.1 });
        var built = SteelNodes.Build(f.Model, node);
        Assert.Equal(pad, built.Footing?.Id);
        Assert.All(Parts(f, node.Id, "steel-anchor"), a => Assert.Equal(-0.1 - 0.75, Box(a).Min.Z, 6));
        Assert.All(Parts(f, node.Id, "steel-anchor"), a => Assert.Contains(a.Properties!, p => p.Name == "Footing"));
        // Longer than the footing is deep: they stop 50 mm above its bottom, and say so.
        f.Run("steel.updateNode", new JsonObject { ["id"] = node.Id, ["params"] = new JsonObject { ["anchorLength"] = 1.5, ["anchor"] = "plate" } });
        Assert.All(Parts(f, node.Id, "steel-anchor"), a => Assert.Equal(-0.95, Box(a).Min.Z, 6));
        Assert.Equal(4, Parts(f, node.Id, "steel-anchorplate").Count());
        Assert.Contains(f.Session.Geometry(node.Id)!.Diagnostics!, d => d.Contains("footing"));
        // The column next to it has no footing: its anchors go 750 mm under the grout.
        var other = Nodes(f, hall)["base@f2:col:a@start"];
        Assert.Null(SteelNodes.Build(f.Model, other).Footing);
        Assert.All(Parts(f, other.Id, "steel-anchor"), a => Assert.Equal(-0.78, Box(a).Min.Z, 6));
    }

    // ------------------------------------------------------------ parameters, rebuild, deletion

    [Fact]
    public void A_connection_set_on_its_own_keeps_its_parameters_and_the_hall_rebuilt_keeps_its_ids()
    {
        var f = F();
        var hall = Hall(f);
        var before = Nodes(f, hall).ToDictionary(x => x.Key, x => x.Value.Id);
        var eaves = before["eaves@f1:raf:a@start"];
        f.Run("steel.updateNode", new JsonObject { ["id"] = eaves, ["params"] = new JsonObject { ["plate"] = 0.03, ["rows"] = 3, ["cols"] = 4, ["stiffeners"] = false } });
        var raf = Members(f, hall)["f1:raf:a"].Id;
        Assert.Equal(-0.03, SteelEnd.Of(f.Model.Get(raf)!).Start.Extension, 9);
        Assert.Equal(4 * 4, Parts(f, eaves, "steel-bolt").Count());
        Assert.DoesNotContain(Parts(f, eaves, "steel-plate"), p => p.Name == "Stiffener");
        Assert.Throws<CommandException>(() => f.Run("steel.updateNode", new JsonObject { ["id"] = eaves, ["params"] = new JsonObject { ["cols"] = 3 } }));
        // The hall changed: the same connections, the one set on its own as it was; the hall's eaves plates for the others.
        f.Run("steel.updateHall", new JsonObject { ["id"] = hall, ["attrs"] = new JsonObject { ["eaves"] = 7, ["nodeParams"] = new JsonObject { ["eaves"] = new JsonObject { ["plate"] = 0.025 } } } });
        var after = Nodes(f, hall);
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        Assert.All(after, x => Assert.Equal(before[x.Key], x.Value.Id));
        Assert.Equal(0.03, SteelNodes.Spec(f.Model, after["eaves@f1:raf:a@start"]).Plate, 9);
        Assert.Equal(-0.03, SteelEnd.Of(f.Model.Get(raf)!).Start.Extension, 9);
        Assert.Equal(0.025, SteelNodes.Spec(f.Model, after["eaves@f2:raf:a@start"]).Plate, 9);
        Assert.Equal(-0.025, SteelEnd.Of(Members(f, hall)["f2:raf:a"]).Start.Extension, 9);
        // Fewer bays: the last frames' connections go with their members.
        f.Run("steel.updateHall", new JsonObject { ["id"] = hall, ["attrs"] = new JsonObject { ["bays"] = J(6, 6) } });
        Assert.DoesNotContain(Nodes(f, hall).Keys, k => k.Contains("f4:") || k.EndsWith("@f4", StringComparison.Ordinal));
        Assert.All(f.Model.OfClass(SteelNodes.Class), n => Assert.All(SteelNodes.MembersOf(n), j => Assert.NotNull(f.Model.Get(j.Id))));
        // Connections off: all go, and the members' ends with them.
        f.Run("steel.updateHall", new JsonObject { ["id"] = hall, ["attrs"] = new JsonObject { ["nodes"] = false } });
        Assert.Empty(f.Model.OfClass(SteelNodes.Class));
        Assert.Equal(0, SteelEnd.Of(f.Model.Get(raf)!).Start.Extension, 9);
    }

    [Fact]
    public void Deleting_a_connection_gives_the_ends_back_and_a_hall_goes_with_its_members_and_connections()
    {
        var f = F();
        var hall = Hall(f);
        var raf = Members(f, hall)["f1:raf:a"].Id;
        var eaves = Nodes(f, hall)["eaves@f1:raf:a@start"].Id;
        f.Run("element.delete", new JsonObject { ["id"] = eaves });
        Assert.Null(f.Model.Get(eaves));
        Assert.Equal(0.2, Body(f, raf).Min(p => p.X), 6);
        // A member deleted takes its connections: the apex of its frame goes, the other rafter's end set back no more.
        var rafB = Members(f, hall)["f1:raf:b"].Id;
        f.Run("element.delete", new JsonObject { ["id"] = raf });
        Assert.DoesNotContain(Nodes(f, hall).Keys, k => k == "apex@f1");
        Assert.Equal(0, SteelEnd.Of(f.Model.Get(rafB)!).End.Extension, 9);
        // The hall deleted: its members, connections and grid with it, in one step.
        var count = f.Model.Entities.Count();
        f.Run("element.delete", new JsonObject { ["id"] = hall });
        Assert.Empty(f.Model.OfClass(Halls.Class));
        Assert.Empty(f.Model.OfClass(SteelModule.Member));
        Assert.Empty(f.Model.OfClass(SteelNodes.Class));
        Assert.Empty(f.Model.OfClass(IfcClass.Column));
        Assert.Empty(f.Model.OfClass(IfcClass.Grid));
        f.Session.Undo();
        Assert.Equal(count, f.Model.Entities.Count());
    }

    // ------------------------------------------------------------ schedule, preview, IFC

    [Fact]
    public void The_steel_parts_schedule_counts_plates_bolts_and_anchors_and_the_preview_weighs_them()
    {
        var f = F();
        var hall = Hall(f);
        var rows = SteelPartsSchedule.Rows(f.Model);
        var parts = Nodes(f, hall).Values.SelectMany(n => f.Session.Geometry(n.Id)!.Parts).ToList();
        int Count(string part) => parts.Count(p => SteelNodes.P(p.Properties!, "Part") == part);
        Assert.Equal(Count("Bolt"), rows.Where(r => r.Part == "Bolt").Sum(r => r.Count));
        Assert.Equal(Count("Anchor"), rows.Where(r => r.Part == "Anchor").Sum(r => r.Count));
        Assert.Equal(Count("Plate"), rows.Where(r => r.Part == "Plate").Sum(r => r.Count));
        Assert.Equal(parts.Sum(p => Measure.Volume(p.Solid)) * SteelModule.SteelDensity, rows.Sum(r => r.Weight), 6);
        Assert.Contains(rows, r => r.Part == "Weld" && r.Length > 0);
        var grid = f.Session.Catalog.All<IScheduleSource>().Single(s => s.Kind == "steelParts").Grid(new QueryContextFor(f));
        Assert.Equal("Total", grid.Rows[^1][0]);
        // The configurator's preview: the same connections, a typical frame's drawn over it.
        var p = (JsonObject)f.Session.Query("steel.hallPreview", new JsonObject { ["attrs"] = new JsonObject { ["secondary"] = false } })!;
        var sum = p["summary"]!;
        Assert.Equal(42, (int)sum["nodes"]!);
        Assert.Equal(Count("Bolt"), (int)sum["bolts"]!);
        Assert.Equal(Count("Anchor"), (int)sum["anchors"]!);
        Assert.True(p["frameNodes"]!.AsArray().Count > 20);
    }

    [Fact]
    public void Connections_export_as_assemblies_of_plates_and_fasteners_realizing_the_members_connections()
    {
        var f = F();
        var hall = Hall(f, new JsonObject { ["bays"] = J(6), ["gable"] = "frame", ["eavesHaunch"] = null, ["apexHaunch"] = null });
        var nodes = Nodes(f, hall);
        // One bay, two frames: four bases, four eaves, two apexes.
        Assert.Equal(10, nodes.Count);
        int Of(string part) => nodes.Values.SelectMany(n => f.Session.Geometry(n.Id)!.Parts).Count(p => SteelNodes.P(p.Properties!, "Part") == part);
        var path = Path.Combine(Path.GetTempPath(), $"nodes-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            Assert.Equal(10, step.OfType("IFCELEMENTASSEMBLY").Count());
            Assert.Equal(Of("Bolt") + Of("Anchor"), step.OfType("IFCMECHANICALFASTENER").Count());
            Assert.Equal(Of("Anchor"), step.OfType("IFCMECHANICALFASTENER").Count(x => x[10] is StepEnum { Value: "ANCHORBOLT" }));
            Assert.Equal(Of("Plate"), step.OfType("IFCPLATE").Count());
            // The eaves and the apexes realize their members' connections; the bases have no footing to connect to.
            Assert.Equal(6, step.OfType("IFCRELCONNECTSWITHREALIZINGELEMENTS").Count());
            // The hall's system holds its connections too.
            Assert.Contains(step.OfType("IFCPROPERTYSET"), x => x[2] is string s && s == "Lapin_SteelPart");

            // Read back: an eaves's parts, the volumes Lapin built.
            var model = new IfcModel(step);
            var booleans = f.Session.Catalog.Get<ISolidBooleans>()!;
            var eaves = nodes["eaves@f0:raf:a@start"];
            var mine = f.Session.Geometry(eaves.Id)!.Parts.Sum(p => Measure.Volume(p.Solid));
            var read = model.Products.Where(p => p.Tag?.StartsWith(eaves.Id + "/steel-", StringComparison.Ordinal) == true)
                .Sum(p => model.Shape(p.Id, booleans).Body.Sum(b => Math.Abs(MeshVolume(b.Mesh))));
            Assert.Equal(mine, read, 7);
        }
        finally { File.Delete(path); }
    }

    static double MeshVolume(MeshBuilder mesh) => mesh.T.Sum(t => Vec3.Dot(mesh.V[t.A], Vec3.Cross(mesh.V[t.B], mesh.V[t.C]))) / 6;

    sealed class QueryContextFor(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
