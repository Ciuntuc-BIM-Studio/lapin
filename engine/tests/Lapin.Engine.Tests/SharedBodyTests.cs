using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Bodies shared between references (docs/plan-instancing.md): products of an
/// IFC file that use one shape, or one type's map, become instances of one
/// shared body, each keeping where it stands; the interface gets the mesh once.
/// </summary>
public class SharedBodyTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-shared-bodies").FullName;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    static Fixture Make() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());

    /// <summary>
    /// A file of cubes (1 m, centred on their placement in plan, standing on it) on one storey: <paramref name="shared"/> of them using the
    /// shape #100, two more each placing the type map #200 (turned a quarter round, the
    /// second), and with <paramref name="opening"/> one more cube of the shared shape,
    /// voided by an opening.
    /// </summary>
    internal static string Cubes(int shared, bool turned = false, bool mapped = false, bool opening = false)
    {
        var s = new StringBuilder("""
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
            FILE_NAME('cubes.ifc','2026-10-29T00:00:00',(''),(''),'','','');
            FILE_SCHEMA(('IFC4'));
            ENDSEC;
            DATA;
            #1=IFCORGANIZATION($,'Test',$,$,$);
            #2=IFCAPPLICATION(#1,'1.0','test','T');
            #3=IFCOWNERHISTORY(#5,#2,$,.NOTDEFINED.,$,$,$,0);
            #4=IFCPERSON($,'User',$,$,$,$,$,$);
            #5=IFCPERSONANDORGANIZATION(#4,#1,$);
            #6=IFCDIRECTION((1.,0.,0.));
            #7=IFCDIRECTION((0.,0.,1.));
            #8=IFCCARTESIANPOINT((0.,0.,0.));
            #9=IFCAXIS2PLACEMENT3D(#8,#7,#6);
            #10=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.0E-5,#9,$);
            #11=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
            #12=IFCUNITASSIGNMENT((#11));
            #13=IFCPROJECT('2CubeProject0000000000',#3,'Cubes',$,$,$,$,(#10),#12);
            #20=IFCSITE('2CubeSite0000000000000',#3,'Site',$,$,#21,$,$,.ELEMENT.,$,$,$,$,$);
            #21=IFCLOCALPLACEMENT($,#9);
            #30=IFCBUILDING('2CubeBuilding000000000',#3,'Building',$,$,#31,$,$,.ELEMENT.,$,$,$);
            #31=IFCLOCALPLACEMENT(#21,#9);
            #40=IFCBUILDINGSTOREY('2CubeStorey00000000000',#3,'Cubes',$,$,#41,$,$,.ELEMENT.,0.);
            #41=IFCLOCALPLACEMENT(#31,#9);
            #42=IFCRELAGGREGATES('2CubeRelAgg00000000000',#3,$,$,#13,(#20));
            #43=IFCRELAGGREGATES('2CubeRelAgg00000000001',#3,$,$,#20,(#30));
            #44=IFCRELAGGREGATES('2CubeRelAgg00000000002',#3,$,$,#30,(#40));
            #45=IFCDIRECTION((0.,1.,0.));
            #90=IFCAXIS2PLACEMENT2D(#8,$);
            #91=IFCRECTANGLEPROFILEDEF(.AREA.,$,#90,1.0,1.0);
            #92=IFCEXTRUDEDAREASOLID(#91,#9,#7,1.0);
            #99=IFCSHAPEREPRESENTATION(#10,'Body','SweptSolid',(#92));
            #100=IFCPRODUCTDEFINITIONSHAPE($,$,(#99));
            #200=IFCREPRESENTATIONMAP(#9,#99);

            """);
        var products = new List<int>();
        var id = 1000;
        void Cube(double x, double y, int shape, bool turn)
        {
            s.Append($"#{id}=IFCCARTESIANPOINT(({x:0.0###},{y:0.0###},0.));\n");
            s.Append(turn ? $"#{id + 1}=IFCAXIS2PLACEMENT3D(#{id},#7,#45);\n" : $"#{id + 1}=IFCAXIS2PLACEMENT3D(#{id},$,$);\n");
            s.Append($"#{id + 2}=IFCLOCALPLACEMENT(#41,#{id + 1});\n");
            s.Append($"#{id + 3}=IFCBUILDINGELEMENTPROXY('{products.Count:D22}',#3,'cube-{products.Count}',$,$,#{id + 2},#{shape},$,.NOTDEFINED.);\n");
            products.Add(id + 3);
            id += 4;
        }
        for (var i = 0; i < shared; i++) Cube(2 * i, 0, 100, turned && i == 1);
        if (mapped)
            for (var k = 0; k < 2; k++)
            {
                s.Append($"#{id}=IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,#8,$,$);\n");
                s.Append($"#{id + 1}=IFCMAPPEDITEM(#200,#{id});\n");
                s.Append($"#{id + 2}=IFCSHAPEREPRESENTATION(#10,'Body','MappedRepresentation',(#{id + 1}));\n");
                s.Append($"#{id + 3}=IFCPRODUCTDEFINITIONSHAPE($,$,(#{id + 2}));\n");
                var pds = id + 3;
                id += 4;
                Cube(2 * k, 4, pds, k == 1);
            }
        if (opening)
        {
            Cube(0, 8, 100, false);
            var host = products[^1];
            // A 0.4 m hole through the cube, from the side.
            s.Append($"#{id}=IFCCARTESIANPOINT((0.3,-0.5,0.3));\n#{id + 1}=IFCAXIS2PLACEMENT3D(#{id},$,$);\n#{id + 2}=IFCLOCALPLACEMENT(#{host - 1},#{id + 1});\n");
            s.Append($"#{id + 3}=IFCRECTANGLEPROFILEDEF(.AREA.,$,#90,0.4,0.4);\n#{id + 4}=IFCAXIS2PLACEMENT3D(#8,#45,#6);\n");
            s.Append($"#{id + 5}=IFCEXTRUDEDAREASOLID(#{id + 3},#{id + 4},#7,2.0);\n#{id + 6}=IFCSHAPEREPRESENTATION(#10,'Body','SweptSolid',(#{id + 5}));\n#{id + 7}=IFCPRODUCTDEFINITIONSHAPE($,$,(#{id + 6}));\n");
            s.Append($"#{id + 8}=IFCOPENINGELEMENT('2CubeOpening0000000000',#3,'hole',$,$,#{id + 2},#{id + 7},$,.OPENING.);\n");
            s.Append($"#{id + 9}=IFCRELVOIDSELEMENT('2CubeVoids000000000000',#3,$,$,#{host},#{id + 8});\n");
            id += 10;
        }
        s.Append($"#5000000=IFCRELCONTAINEDINSPATIALSTRUCTURE('2CubeRelCnt00000000000',#3,$,$,({string.Join(",", products.Select(p => "#" + p))}),#40);\n");
        s.Append("ENDSEC;\nEND-ISO-10303-21;\n");
        return s.ToString();
    }

    string Write(string text, string name = "cubes.ifc")
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        return path;
    }

    static List<Entity> Proxies(IModelView m) => m.OfClass("IfcBuildingElementProxy").ToList();
    static List<Entity> Shared(IModelView m) => m.OfClass(StoredBodies.SharedClass).ToList();

    static (Lapin.Geometry.Vec3 Min, Lapin.Geometry.Vec3 Max) Box(Fixture f, string id)
    {
        var g = f.Session.Geometry(id)!;
        var v = g.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        return (new(v.Min(p => p.X), v.Min(p => p.Y), v.Min(p => p.Z)), new(v.Max(p => p.X), v.Max(p => p.Y), v.Max(p => p.Z)));
    }

    [Fact]
    public void Cubes_sharing_a_shape_are_instances_of_one_body_each_where_it_stands()
    {
        var f = Make();
        var r = IfcImport.Run(f.Session, Write(Cubes(8)), null, out var diff);
        Assert.Equal(8, r.References);
        var body = Assert.Single(Shared(f.Model));
        var cubes = Proxies(f.Model);
        Assert.Equal(8, cubes.Count);
        foreach (var c in cubes)
        {
            Assert.Equal(body.Id, c.Str(StoredBodies.BodyOf));
            Assert.Null(c.Attrs[StoredBodies.Key]);
        }
        // Each where the file put it: the n-th about x = 2n.
        for (var i = 0; i < 8; i++)
        {
            var (lo, hi) = Box(f, cubes.Single(c => c.Name == $"cube-{i}").Id);
            Assert.Equal(2 * i - 0.5, lo.X, 6); Assert.Equal(2 * i + 0.5, hi.X, 6);
            Assert.Equal(0, lo.Z, 6); Assert.Equal(1, hi.Z, 6);
        }
        // The import's diff: one mesh, eight instances of it with no parts of their own, the body not among the props.
        var mesh = Assert.Single(diff.Meshes!);
        Assert.Equal(body.Id, mesh.Id);
        Assert.Equal([-0.5f, -0.5f, 0f, 0.5f, 0.5f, 1f], mesh.Box);
        var items = diff.Render.Where(i => i.Type == "IfcBuildingElementProxy").ToList();
        Assert.Equal(8, items.Count);
        Assert.All(items, i => { Assert.Equal(body.Id, i.Instance!.Mesh); Assert.Empty(i.Parts!); });
        Assert.Contains(items, i => i.Instance!.Matrix[9] == 14f);
        Assert.All(diff.Nodes, n => Assert.Null(n.Props[StoredBodies.Key]));
        // A snapshot sends the mesh again (the interface starts over); a later edit's diff does not.
        Assert.Single(f.Session.Snapshot().Meshes!);
        var moved = f.Session.Execute("element.rename", new JsonObject { ["id"] = cubes[0].Id, ["name"] = "first" }, out _);
        Assert.Empty(moved.Meshes ?? []);
    }

    [Fact]
    public void A_frame_carries_the_mesh_once_however_many_instances()
    {
        var few = Make();
        IfcImport.Run(few.Session, Write(Cubes(10), "few.ifc"), null, out var a);
        var many = Make();
        IfcImport.Run(many.Session, Write(Cubes(400), "many.ifc"), null, out var b);
        var (fa, fb) = (BinaryFrame.Encode(a).Length, BinaryFrame.Encode(b).Length);
        // Per instance: its node (id, storey, properties) and its item (matrix): about a kilobyte, no triangles.
        Assert.InRange((fb - fa) / 390.0, 100, 1500);
    }

    [Fact]
    public void A_turned_placement_turns_its_instance()
    {
        var f = Make();
        IfcImport.Run(f.Session, Write(Cubes(3, turned: true)), null, out _);
        Assert.Single(Shared(f.Model));
        // The second cube, at x = 2, turned a quarter round about its middle: in the same place.
        var (lo, hi) = Box(f, Proxies(f.Model).Single(c => c.Name == "cube-1").Id);
        Assert.Equal(1.5, lo.X, 6); Assert.Equal(2.5, hi.X, 6);
        Assert.Equal(-0.5, lo.Y, 6); Assert.Equal(0.5, hi.Y, 6);
        Assert.Equal(1f, f.Session.Snapshot().Render.Single(i => i.NodeId == Proxies(f.Model).Single(c => c.Name == "cube-1").Id).Instance!.Matrix[1]);
        // Its faces turned with it: the side that faced +X faces +Y.
        var g = f.Session.Geometry(Proxies(f.Model).Single(c => c.Name == "cube-1").Id)!;
        Assert.Contains(g.Parts[0].Solid.Faces, x => (x.Normal - new Lapin.Geometry.Vec3(0, 1, 0)).Length < 1e-9);
        Assert.DoesNotContain(g.Parts[0].Solid.Faces, x => Math.Abs(x.Normal.Length - 1) > 1e-9);
    }

    [Fact]
    public void Occurrences_of_a_types_map_share_it_and_a_voided_product_keeps_its_own_body()
    {
        var f = Make();
        var r = IfcImport.Run(f.Session, Write(Cubes(2, mapped: true, opening: true)), null, out _);
        Assert.Equal(5, r.References);
        // Two bodies shared: the shape #100 (two cubes; the voided one is cut, its own) and the map #200 (two occurrences).
        Assert.Equal(2, Shared(f.Model).Count);
        var voided = Proxies(f.Model).Single(c => c.Name == "cube-4");
        Assert.Null(voided.Attrs[StoredBodies.BodyOf]);
        Assert.NotNull(voided.Attrs[StoredBodies.Key]);
        var mapped = Proxies(f.Model).Where(c => c.Name is "cube-2" or "cube-3").ToList();
        Assert.Single(mapped.Select(c => c.Str(StoredBodies.BodyOf)).Distinct());
        var (lo, hi) = Box(f, mapped.Single(c => c.Name == "cube-3").Id);
        Assert.Equal(1.5, lo.X, 6); Assert.Equal(2.5, hi.X, 6);
        Assert.Equal(3.5, lo.Y, 6); Assert.Equal(4.5, hi.Y, 6);
        // The voided one is cut: more than a box's six faces.
        Assert.True(f.Session.Geometry(voided.Id)!.Parts[0].Solid.Faces.Length > 6);
    }

    [Fact]
    public void A_shape_used_once_is_kept_by_its_product()
    {
        var f = Make();
        IfcImport.Run(f.Session, Write(Cubes(1)), null, out _);
        Assert.Empty(Shared(f.Model));
        Assert.NotNull(Assert.Single(Proxies(f.Model)).Attrs[StoredBodies.Key]);
    }

    [Fact]
    public void A_second_import_doubles_nothing_and_undo_takes_the_body_away()
    {
        var f = Make();
        var path = Write(Cubes(6));
        IfcImport.Run(f.Session, path, null, out _);
        IfcImport.Run(f.Session, path, null, out _);
        Assert.Single(Shared(f.Model));
        Assert.Equal(6, Proxies(f.Model).Count);
        f.Session.Undo();
        f.Session.Undo();
        Assert.Empty(Shared(f.Model));
        Assert.Empty(Proxies(f.Model));
    }

    [Fact]
    public void Deleting_instances_keeps_the_body_until_the_last_one_goes()
    {
        var f = Make();
        IfcImport.Run(f.Session, Write(Cubes(3)), null, out _);
        var cubes = Proxies(f.Model).Select(c => c.Id).ToList();
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(cubes[0], cubes[1]) });
        Assert.Single(Shared(f.Model));
        Assert.NotNull(f.Session.Geometry(cubes[2]));
        var d = f.RunDiff("element.delete", new JsonObject { ["id"] = cubes[2] });
        Assert.Empty(Shared(f.Model));
        Assert.Single(d.MeshesRemoved!);
    }

    [Fact]
    public void The_plan_draws_every_instance()
    {
        var f = Make();
        IfcImport.Run(f.Session, Write(Cubes(5)), null, out _);
        var storey = Spatial.Storeys(f.Model).Single(s => s.Name == "Cubes").Id;
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = "plan:" + storey })!;
        var xs = d["pens"]!.AsArray().SelectMany(p => p!["segments"]?.AsArray() ?? []).Select((v, k) => (v, k)).Where(x => x.k % 2 == 0).Select(x => (double)x.v!).ToList();
        for (var i = 0; i < 5; i++) Assert.Contains(xs, x => Math.Abs(x - (2 * i + 0.5)) < 1e-6);
    }

    [Fact]
    public void Saved_and_opened_the_instances_still_share_their_body()
    {
        var f = Make();
        IfcImport.Run(f.Session, Write(Cubes(4)), null, out _);
        var path = Path.Combine(_dir, "cubes.lapin");
        PackageFiles.Save(f.Session, path);
        var g = Make();
        var opened = PackageFiles.Open(g.Session, path);
        Assert.Single(Shared(g.Model));
        Assert.Equal(4, Proxies(g.Model).Count(c => c.Str(StoredBodies.BodyOf) != null));
        Assert.Single(opened.Meshes!);
        var (lo, hi) = Box(g, Proxies(g.Model).Single(c => c.Name == "cube-3").Id);
        Assert.Equal(5.5, lo.X, 6); Assert.Equal(6.5, hi.X, 6);
    }

    [Fact]
    public void Exported_to_IFC_and_read_again_every_instance_comes_back()
    {
        var f = Make();
        IfcImport.Run(f.Session, Write(Cubes(4, mapped: true)), null, out _);
        var path = Path.Combine(_dir, "again.ifc");
        IfcExport.Write(f.Session, path, null);
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("bodyOf", text);
        // Each shared body written once, as a representation map; each instance maps it.
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(text, "=IFCREPRESENTATIONMAP\\(").Count);
        Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(text, "=IFCMAPPEDITEM\\(").Count);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(text, "=IFCFACETEDBREP\\(").Count);
        // Read again by Lapin, a reference it wrote is a reference again, with the body the file gives it.
        var g = Make();
        var r = IfcImport.Run(g.Session, path, null, out _);
        Assert.Equal(6, r.References);
        Assert.Equal(6, Proxies(g.Model).Count);
        // Read again, the instances share their bodies again.
        Assert.Equal(2, Shared(g.Model).Count);
        Assert.All(Proxies(g.Model), c => Assert.NotNull(c.Attrs[StoredBodies.BodyOf]));
        var (lo, hi) = Box(g, Proxies(g.Model).Single(c => c.Name == "cube-5").Id);
        Assert.Equal(1.5, lo.X, 6); Assert.Equal(2.5, hi.X, 6);
        Assert.Equal(3.5, lo.Y, 6); Assert.Equal(4.5, hi.Y, 6);
    }

    /// <summary>A diff header's lists folded as the interface folds them: removals, then additions; a reset starts over.</summary>
    sealed class Folded
    {
        public readonly HashSet<string> Nodes = [], Render = [], Relations = [], Meshes = [];
        public readonly List<(long Version, bool Reset)> Seen = [];
        public void Apply(JsonObject d)
        {
            Seen.Add(((long)d["version"]!, (bool)d["reset"]!));
            if ((bool)d["reset"]!) { Nodes.Clear(); Render.Clear(); Relations.Clear(); Meshes.Clear(); }
            void Fold(HashSet<string> set, string removed, string added, string key)
            {
                foreach (var id in d[removed]?.AsArray() ?? []) set.Remove((string)id!);
                foreach (var x in d[added]?.AsArray() ?? []) set.Add((string)x![key]!);
            }
            Fold(Nodes, "removedNodes", "nodes", "id");
            Fold(Render, "renderRemoved", "render", "nodeId");
            Fold(Relations, "removedRelations", "relations", "id");
            Fold(Meshes, "meshesRemoved", "meshes", "id");
        }
    }

    [Fact]
    public void A_big_snapshot_goes_in_parts_the_reply_carrying_the_last()
    {
        var f = Make();
        IfcImport.Run(f.Session, Write(Cubes(45)), null, out _);
        var whole = f.Session.Snapshot();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create()) { PartSize = 10 };
        var (reply, diff) = d.Handle("""{"id":1,"method":"doc.snapshot","params":{}}""");
        Assert.Null(diff);
        var before = d.TakeCallerFrames();
        Assert.Empty(d.TakeCallerFrames());
        Assert.True(before.Count >= 4);
        var folded = new Folded();
        foreach (var frame in before)
        {
            var e = JsonNode.Parse(BinaryFrame.HeaderText(frame))!;
            Assert.Equal("diff", (string?)e["event"]);
            folded.Apply((JsonObject)e["data"]!);
        }
        folded.Apply((JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!["result"]!);
        // Only the first resets; the versions go up, the reply's last.
        Assert.True(folded.Seen[0].Reset);
        Assert.All(folded.Seen.Skip(1), x => Assert.False(x.Reset));
        Assert.Equal(folded.Seen.Select(x => x.Version).Order(), folded.Seen.Select(x => x.Version));
        Assert.Equal(folded.Seen.Count, folded.Seen.Select(x => x.Version).Distinct().Count());
        // Folded, the parts are the snapshot.
        Assert.Equal(whole.Nodes.Select(n => n.Id).ToHashSet(), folded.Nodes);
        Assert.Equal(whole.Render.Select(r => r.NodeId).ToHashSet(), folded.Render);
        Assert.Equal(whole.Relations.Select(r => r.Id).ToHashSet(), folded.Relations);
        Assert.Single(folded.Meshes);
        // The next edit's diff comes after every part.
        var next = f.Session.Execute("element.rename", new JsonObject { ["id"] = Proxies(f.Model)[0].Id, ["name"] = "first" }, out _);
        Assert.True(next.Version > folded.Seen[^1].Version);
    }

    [Fact]
    public void A_big_edit_is_pushed_in_parts_in_order()
    {
        var f = Make();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create()) { PartSize = 10 };
        IfcImport.Run(f.Session, Write(Cubes(35)), null, out var diff);
        var parts = d.Parts(diff);
        Assert.Equal(4, parts.Count);
        Assert.Equal(diff.Version, parts[0].Version);
        Assert.Equal(35, parts.Sum(p => p.Render.Count(r => r.Instance != null)));
        Assert.Single(parts[0].Meshes!);
        Assert.All(parts.Skip(1), p => Assert.Empty(p.Meshes!));
        Assert.All(parts, p => Assert.True(p.Nodes.Count <= 10 && p.Render.Count <= 10 && p.Relations.Count <= 10));
        // Small diffs go whole.
        Assert.Single(d.Parts(f.Session.Execute("element.rename", new JsonObject { ["id"] = Proxies(f.Model)[0].Id, ["name"] = "first" }, out _)));
    }
}
