using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Platform;
using Lapin.Studio;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// Exact bodies written out (docs/plan-studio-freeform.md §9, F2c): IFC4 IfcAdvancedBrep on request and STEP AP214 MANIFOLD_SOLID_BREP,
/// read back by Lapin's own readers (IfcAdvanced, StepFile) and measured against the Studio bodies: a cylinder on a cylindrical surface, a
/// sphere on a spherical one, a smooth loft on a B-spline surface, a SubD on bicubic patches; what is not exact as its facets.
/// </summary>
public class ExactExportTests(ITestOutputHelper output)
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static JsonArray J3(double x, double y, double z) => [x, y, z];
    static string Draw(Fixture f, string view, JsonObject shape) => (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = shape })!["ids"]![0]!;
    static JsonObject Rect(double x0, double y0, double x1, double y1) =>
        new() { ["type"] = "polyline", ["pts"] = new JsonArray(J(x0, y0), J(x1, y0), J(x1, y1), J(x0, y1)), ["closed"] = true };
    static JsonObject Circle(double x, double y, double r) => new() { ["type"] = "circle", ["c"] = J(x, y), ["r"] = r };
    static JsonObject Line(double x1, double y1, double x2, double y2) => new() { ["type"] = "line", ["a"] = J(x1, y1), ["b"] = J(x2, y2) };
    static string Id(JsonNode? r) => (string)r!["id"]!;

    sealed record Scene(Fixture F, Dictionary<string, string> Ids);

    /// <summary>One of each: a box, a cylinder, a cone, a sphere, a torus, a revolved ring, a smooth loft, a SubD box, and a sweep (not exact).</summary>
    static Scene Build()
    {
        var f = F();
        while (Spatial.Storeys(f.Model).Count() < 3) f.Run("storey.create", new JsonObject());
        var storeys = Spatial.Storeys(f.Model).OrderBy(s => s.Num("elevation")).ToList();
        string PlanOf(string storey) => Id(f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey }));
        var (p0, p1, p2) = (PlanOf(storeys[0].Id), PlanOf(storeys[1].Id), PlanOf(storeys[2].Id));
        string Prim(string shape, double x, JsonObject sizes)
        {
            var a = new JsonObject { ["shape"] = shape, ["at"] = J(x, 0), ["name"] = shape };
            foreach (var (k, v) in sizes) a[k] = v?.DeepClone();
            return Id(f.Run("studio.primitive", a));
        }
        var ids = new Dictionary<string, string>
        {
            ["box"] = Prim("box", 0, new() { ["width"] = 1, ["length"] = 2, ["height"] = 0.5, ["rotation"] = 20 }),
            ["cylinder"] = Prim("cylinder", 3, new() { ["radius"] = 0.4, ["height"] = 1.5 }),
            ["cone"] = Prim("cone", 6, new() { ["radius"] = 0.6, ["radius2"] = 0.2, ["height"] = 1 }),
            ["sphere"] = Prim("sphere", 9, new() { ["radius"] = 0.7 }),
            ["torus"] = Prim("torus", 13, new() { ["radius"] = 1, ["radius2"] = 0.3 }),
        };
        var sq = Draw(f, p0, Rect(18, 0, 19, 1));
        var axis = Draw(f, p0, Line(17, 0, 17, 1));
        ids["ring"] = Id(f.Run("studio.revolve", new JsonObject { ["profileId"] = sq, ["axisId"] = axis, ["name"] = "ring" }));
        var a = Draw(f, p0, Rect(24.5, -0.5, 25.5, 0.5));
        var b = Draw(f, p1, Circle(25, 0, 1));
        var c = Draw(f, p2, Rect(24.5, -0.5, 25.5, 0.5));
        ids["loft"] = Id(f.Run("studio.loft", new JsonObject { ["profileId"] = a, ["profileIds"] = new JsonArray(a, b, c), ["smooth"] = true, ["name"] = "loft" }));
        ids["subd"] = Id(f.Run("studio.subdCreate", new JsonObject { ["shape"] = "box", ["at"] = J3(30, 0, 0), ["width"] = 1, ["length"] = 1, ["height"] = 1, ["name"] = "subd" }));
        var profile = Draw(f, p0, Rect(40, 0, 40.2, 0.2));
        var path = Draw(f, p0, new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(40, 5), J(43, 5), J(43, 8)), ["closed"] = false });
        ids["sweep"] = Id(f.Run("studio.sweep", new JsonObject { ["profileId"] = profile, ["pathId"] = path, ["name"] = "sweep" }));
        return new Scene(f, ids);
    }

    static double Volume(MeshBuilder m)
    {
        var w = m.Welded();
        var o = w.V.Count > 0 ? w.V[0] : Vec3.Zero;
        return Math.Abs(w.T.Sum(t => Vec3.Dot(w.V[t.A] - o, Vec3.Cross(w.V[t.B] - o, w.V[t.C] - o))) / 6);
    }

    static double Area(MeshBuilder m) => m.T.Sum(t => Vec3.Cross(m.V[t.B] - m.V[t.A], m.V[t.C] - m.V[t.A]).Length / 2);

    static double SourceVolume(Scene sc, string key) => Measure.Volume(sc.F.Session.Geometry(sc.Ids[key])!.Parts[0].Solid);
    static double SourceArea(Scene sc, string key) => Measure.SurfaceArea(sc.F.Session.Geometry(sc.Ids[key])!.Parts[0].Solid);

    [Fact]
    public void IFC4_advanced_breps_on_exact_surfaces_read_back_with_the_bodies_volumes_and_areas()
    {
        var sc = Build();
        var (plain, _) = IfcExport.Bytes(sc.F.Session, "plain.ifc");
        Assert.DoesNotContain("IFCADVANCEDBREP", Encoding.UTF8.GetString(plain));
        var (bytes, result) = IfcExport.Bytes(sc.F.Session, "exact.ifc", new IfcExportOptions(AdvancedBrep: true));
        var text = Encoding.UTF8.GetString(bytes);
        // Every one but the sweep, exactly.
        Assert.Equal(8, text.Split("=IFCADVANCEDBREP(").Length - 1);
        Assert.Contains(result.Notes, n => n.StartsWith("8 bodies written exactly", StringComparison.Ordinal));
        foreach (var entity in new[] { "=IFCCYLINDRICALSURFACE(", "=IFCCONICALSURFACE(", "=IFCSPHERICALSURFACE(", "=IFCTOROIDALSURFACE(", "=IFCBSPLINESURFACEWITHKNOTS(",
            "=IFCRATIONALBSPLINESURFACEWITHKNOTS(", "=IFCCIRCLE(", "=IFCLINE(", "=IFCBSPLINECURVEWITHKNOTS(", "=IFCADVANCEDFACE(", "'AdvancedBrep'" })
            Assert.Contains(entity, text);
        // Read back by Lapin's IFC reader: each body's volume and area as the model has it.
        var model = new IfcModel(StepFile.Read(bytes));
        foreach (var (key, id) in sc.Ids)
        {
            var product = model.Products.Single(p => p.GlobalId.Length == 22 && p.Name == key);
            var meshes = model.Shape(product.Id).Body.Select(x => x.Mesh).ToList();
            var (v, a) = (meshes.Sum(Volume), meshes.Sum(Area));
            var (sv, sa) = (SourceVolume(sc, key), SourceArea(sc, key));
            output.WriteLine($"{key}: volume {v:0.######} / {sv:0.######} ({(v / sv - 1) * 100:0.###} %), area {a:0.####} / {sa:0.####} ({(a / sa - 1) * 100:0.###} %)");
            Assert.InRange(v / sv, 0.985, 1.015);
            Assert.InRange(a / sa, 0.985, 1.015);
        }
    }

    [Fact]
    public void STEP_AP214_solids_on_exact_surfaces_read_back_with_the_bodies_volumes()
    {
        var sc = Build();
        var (bytes, result) = StepExport.Bytes(sc.F.Session, "studio.step");
        Assert.Equal(9, result.Solids);
        Assert.Equal(8, result.Exact);
        Assert.Equal(1, result.Faceted);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("FILE_SCHEMA(('AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }'));", text);
        foreach (var entity in new[] { "=CYLINDRICAL_SURFACE(", "=CONICAL_SURFACE(", "=SPHERICAL_SURFACE(", "=TOROIDAL_SURFACE(", "=B_SPLINE_SURFACE_WITH_KNOTS(",
            "RATIONAL_B_SPLINE_SURFACE(", "=MANIFOLD_SOLID_BREP(", "=ADVANCED_BREP_SHAPE_REPRESENTATION(", "SI_UNIT(.MILLI.,.METRE.)", "=STYLED_ITEM(" })
            Assert.Contains(entity, text);
        // The file reads as STEP: every entity, the complex ones' partial records too.
        var file = StepFile.Read(bytes);
        Assert.Equal("AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }", file.Schema);
        Assert.True(file.ComplexCount > 4);
        Assert.Equal(0.001, StepBreps.LengthUnit(file));
        var solids = StepBreps.Read(bytes);
        Assert.Equal(9, solids.Count);
        foreach (var (key, _) in sc.Ids)
        {
            var mesh = solids.Single(s => s.Name == key).Mesh;
            var (v, sv) = (Volume(mesh), SourceVolume(sc, key));
            output.WriteLine($"{key}: volume {v:0.######} / {sv:0.######} ({(v / sv - 1) * 100:0.###} %)");
            Assert.InRange(v / sv, 0.985, 1.015);
        }
    }

    [Fact]
    public void A_cylinder_is_a_cylindrical_surface_a_sphere_a_spherical_one_in_both_formats()
    {
        var f = F();
        var cyl = Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "cylinder", ["at"] = J(0, 0), ["radius"] = 0.25, ["height"] = 2 }));
        var ifc = Encoding.UTF8.GetString(IfcExport.Bytes(f.Session, "c.ifc", new IfcExportOptions(AdvancedBrep: true)).Bytes);
        Assert.Equal(2, ifc.Split("=IFCCYLINDRICALSURFACE(").Length - 1);
        Assert.Contains("=IFCCYLINDRICALSURFACE(#", ifc);
        Assert.Contains(",0.25);", ifc);
        var step = Encoding.UTF8.GetString(StepExport.Bytes(f.Session, "c.step").Bytes);
        // In millimetres.
        Assert.Contains(",250.);", step);
        Assert.Equal(2, step.Split("=CYLINDRICAL_SURFACE(").Length - 1);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(cyl) });
        f.Run("studio.primitive", new JsonObject { ["shape"] = "sphere", ["at"] = J(0, 0), ["radius"] = 0.5 });
        ifc = Encoding.UTF8.GetString(IfcExport.Bytes(f.Session, "s.ifc", new IfcExportOptions(AdvancedBrep: true)).Bytes);
        Assert.Equal(2, ifc.Split("=IFCSPHERICALSURFACE(").Length - 1);
        Assert.DoesNotContain("IFCCYLINDRICALSURFACE", ifc);
    }

    [Fact]
    public void An_exact_IFC_comes_back_into_Lapin_as_references_of_the_same_volume()
    {
        var sc = Build();
        var dir = Directory.CreateTempSubdirectory("lapin-exact-ifc").FullName;
        try
        {
            var path = Path.Combine(dir, "exact.ifc");
            IfcExport.Write(sc.F.Session, path, new IfcExportOptions(AdvancedBrep: true));
            var g = F();
            var r = IfcImport.Run(g.Session, path, null, out _);
            Assert.DoesNotContain(r.Notes, n => n.Contains("left out"));
            foreach (var (key, id) in sc.Ids)
            {
                var v = g.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
                output.WriteLine($"{key}: {v:0.######} / {SourceVolume(sc, key):0.######}");
                Assert.InRange(v / SourceVolume(sc, key), 0.985, 1.015);
            }
        }
        finally { Directory.Delete(dir, true); }
    }
}
