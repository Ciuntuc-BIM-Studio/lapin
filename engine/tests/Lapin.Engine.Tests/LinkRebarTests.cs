using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A linked structural IFC's IfcReinforcingBar (swept disk solids along their directrix) seen in 3D and hidden by the
/// Rebar category (decisions 402, 403): "architects don't want to see rebars" — every 3D view leaves them out until its
/// Visibility / Graphics shows them; plans and sections draw them only at the detail level that shows them.
/// </summary>
public class LinkRebarTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    /// <summary>
    /// A structural engineer's file: a concrete beam 6 m long with four longitudinal bars and a stirrup every 150 mm, each
    /// bar an IfcReinforcingBar of an IfcSweptDiskSolid (the stirrups closed polylines with bent corners), as Tekla or
    /// Revit write them; <paramref name="beams"/> such beams side by side for the timing.
    /// </summary>
    public static string StructuralIfc(int beams = 1)
    {
        var s = new StringBuilder("""
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('ViewDefinition [ReferenceView_V1.2]'),'2;1');
            FILE_NAME('structure.ifc','2026-11-08T00:00:00',(''),(''),'Structural exporter','Structural','');
            FILE_SCHEMA(('IFC4'));
            ENDSEC;
            DATA;
            #1=IFCCARTESIANPOINT((0.,0.,0.));
            #2=IFCAXIS2PLACEMENT3D(#1,$,$);
            #3=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,#2,$);
            #4=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
            #5=IFCUNITASSIGNMENT((#4));
            #6=IFCPROJECT('0RebarProject000000001',$,'Structure',$,$,$,$,(#3),#5);
            #7=IFCLOCALPLACEMENT($,#2);
            #8=IFCSITE('0RebarSite000000000001',$,'Site',$,$,#7,$,$,.ELEMENT.,$,$,$,$,$);
            #9=IFCLOCALPLACEMENT(#7,#2);
            #10=IFCBUILDING('0RebarBuilding0000001',$,'Building',$,$,#9,$,$,.ELEMENT.,$,$,$);
            #11=IFCLOCALPLACEMENT(#9,#2);
            #12=IFCBUILDINGSTOREY('0RebarStorey000000001',$,'Storey',$,$,#11,$,$,.ELEMENT.,0.);
            #13=IFCRELAGGREGATES('0RebarAgg100000000001',$,$,$,#6,(#8));
            #14=IFCRELAGGREGATES('0RebarAgg200000000001',$,$,$,#8,(#10));
            #15=IFCRELAGGREGATES('0RebarAgg300000000001',$,$,$,#10,(#12));
            #16=IFCDIRECTION((0.,0.,1.));
            #17=IFCDIRECTION((1.,0.,0.));

            """);
        var id = 100;
        var contained = new List<int>();
        int Add(string e) { s.Append($"#{id}={e};\n"); return id++; }
        static string R(double v) => v.ToString("0.0#####", CultureInfo.InvariantCulture);
        var n = 0;
        string Gid() => Step.Guid("rebar-test", (n++).ToString(CultureInfo.InvariantCulture));
        int Point(double x, double y, double z) => Add($"IFCCARTESIANPOINT(({R(x)},{R(y)},{R(z)}))");
        int Bar(List<(double X, double Y, double Z)> path, double d, string name, string role)
        {
            var poly = Add($"IFCPOLYLINE(({string.Join(",", path.Select(p => "#" + Point(p.X, p.Y, p.Z)))}))");
            var disk = Add($"IFCSWEPTDISKSOLID(#{poly},{R(d / 2)},$,$,$)");
            var rep = Add($"IFCSHAPEREPRESENTATION(#3,'Body','AdvancedSweptSolid',(#{disk}))");
            var shape = Add($"IFCPRODUCTDEFINITIONSHAPE($,$,(#{rep}))");
            var place = Add("IFCLOCALPLACEMENT(#11,#2)");
            var bar = Add($"IFCREINFORCINGBAR('{Gid()}',$,'{name}',$,$,#{place},#{shape},$,'B500C',{R(d)},{R(Math.PI * d * d / 4)},$,.{role}.,.TEXTURED.)");
            contained.Add(bar);
            return bar;
        }
        for (var b = 0; b < beams; b++)
        {
            var y0 = b * 1.0;
            // The beam: 300 × 500, 6 m along X.
            // Its placement runs along X (local Z = X), its profile's X up: 500 high, 300 wide.
            var profile = Add("IFCRECTANGLEPROFILEDEF(.AREA.,$,$,0.5,0.3)");
            var solid = Add($"IFCEXTRUDEDAREASOLID(#{profile},#2,#16,6.)");
            var rep = Add($"IFCSHAPEREPRESENTATION(#3,'Body','SweptSolid',(#{solid}))");
            var shape = Add($"IFCPRODUCTDEFINITIONSHAPE($,$,(#{rep}))");
            var at = Add($"IFCAXIS2PLACEMENT3D(#{Point(0, y0, 2.75)},#17,#16)");
            var bplace = Add($"IFCLOCALPLACEMENT(#11,#{at})");
            contained.Add(Add($"IFCBEAM('{Gid()}',$,'Beam {b + 1}',$,$,#{bplace},#{shape},$,.BEAM.)"));
            // Four longitudinal bars Ø16 at the corners inside a 30 mm cover.
            foreach (var (dy, z) in new[] { (-0.112, 2.538), (0.112, 2.538), (-0.112, 2.962), (0.112, 2.962) })
                Bar([(0.03, y0 + dy, z), (5.97, y0 + dy, z)], 0.016, "Ø16", "MAIN");
            // Stirrups Ø8 every 150 mm: a closed rectangle with its corners bent (three points on each 90° arc).
            for (var x = 0.05; x < 5.96; x += 0.15)
            {
                var path = new List<(double X, double Y, double Z)>();
                double w = 0.12, h = 0.22, r = 0.016;
                (double, double)[] corners = [(w, h), (-w, h), (-w, -h), (w, -h)];
                for (var c = 0; c < 4; c++)
                {
                    var (cy, cz) = corners[c];
                    var a0 = Math.PI / 2 * c;
                    for (var k = 0; k <= 2; k++)
                    {
                        var a = a0 + Math.PI / 4 * k;
                        path.Add((x, y0 + cy - Math.Sign(cy) * r + r * Math.Cos(a), 2.75 + cz - Math.Sign(cz) * r + r * Math.Sin(a)));
                    }
                }
                path.Add(path[0]);
                Bar(path, 0.008, "Ø8 stirrup", "LIGATURE");
            }
        }
        Add($"IFCRELCONTAINEDINSPATIALSTRUCTURE('0RebarContained000001',$,$,$,({string.Join(",", contained.Select(c => "#" + c))}),#12)");
        s.Append("ENDSEC;\nEND-ISO-10303-21;\n");
        return s.ToString();
    }

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    [Fact]
    public void A_linked_structural_model_s_bars_show_in_3D_and_hide_by_the_rebar_category()
    {
        var f = F();
        var r = IfcImport.Link(f.Session, Encoding.ASCII.GetBytes(StructuralIfc()), "structure.ifc", null, null, out _);
        var bars = f.Model.OfClass("IfcReinforcingBar").Where(e => e.Str(Links.Key) == r.LinkId).ToList();
        Assert.Equal(4 + 40, bars.Count);
        Assert.All(bars, b => Assert.True(StoredBodies.Has(b)));
        Assert.All(bars, b => Assert.Equal("rebar", ReferenceLayers.CategoryOf(b)));
        // Their bodies: tubes along the directrix (a stirrup's four sides, its bends round).
        var g = f.Session.Geometry(bars[^1].Id)!;
        Assert.True(g.Parts.Sum(p => p.Solid.Faces.Length) > 100);
        // The stirrups are one body, each an instance where it stands.
        Assert.True(bars.Count(b => StoredBodies.InstanceOf(b) != null) >= 40);

        // "Architects don't want to see rebars": the architectural 3D leaves them out until shown.
        var looks = (JsonObject)f.Session.Query("view.looks3d", new JsonObject())!;
        Assert.All(bars, b => Assert.True((bool)looks[b.Id]!["hidden"]!));
        var vis = (JsonObject)f.Session.Query("view.visibility", new JsonObject { ["id"] = "3d" })!;
        Assert.Contains("rebar", vis["hiddenByDefault"]!.AsArray().Select(x => (string)x!));
        f.Run("view.graphics", new JsonObject { ["id"] = "3d", ["category"] = "rebar", ["value"] = new JsonObject { ["hidden"] = false } });
        looks = (JsonObject)f.Session.Query("view.looks3d", new JsonObject())!;
        Assert.DoesNotContain(bars, b => looks.ContainsKey(b.Id));
        // A structural 3D view hides them too until shown (decision 403).
        f.Run("view.graphics", new JsonObject { ["id"] = "3d", ["category"] = "rebar", ["value"] = null });
        f.Run("view.setStyle", new JsonObject { ["id"] = "3d", ["discipline"] = "Structure" });
        looks = (JsonObject)f.Session.Query("view.looks3d", new JsonObject())!;
        Assert.All(bars, b => Assert.True((bool)looks[b.Id]!["hidden"]!));
    }

    [Fact]
    public void A_section_draws_the_linked_bars_only_at_LOD_400()
    {
        var f = F();
        var r = IfcImport.Link(f.Session, Encoding.ASCII.GetBytes(StructuralIfc()), "structure.ifc", null, null, out _);
        var beam = f.Model.OfClass(IfcClass.Beam).Single(e => e.Str(Links.Key) == r.LinkId);
        _ = beam;
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(3, -1), ["end"] = new JsonArray(3, 1), ["depth"] = 0.5, ["scale"] = 20 })!["id"]!;
        int BarCuts()
        {
            var (d, _, problem) = Views.Generate(new Q(f), Views.Find(f.Model, view)!);
            Assert.True(d != null, problem);
            return d!.Cuts.Select(c => f.Model.Get(c.Element)).Count(e => e?.Class == "IfcReinforcingBar");
        }
        // The architectural style's LOD 350 leaves them out (decision 404); LOD 400 (a structural style's) draws them.
        Assert.Equal(0, BarCuts());
        f.Run("view.setStyle", new JsonObject { ["id"] = view, ["detail"] = 400 });
        Assert.True(BarCuts() >= 4);
        f.Run("view.setStyle", new JsonObject { ["id"] = view, ["style"] = "structural", ["detail"] = null });
        Assert.True(BarCuts() >= 4);
        // The rebar category hides them in this view alone.
        f.Run("view.graphics", new JsonObject { ["id"] = view, ["category"] = "rebar", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.Equal(0, BarCuts());
    }

    /// <summary>Many bars (opt-in: <c>LAPIN_REBAR_BENCH=&lt;beams&gt;</c>, each 44 bars): the link's time, the bodies shared, the triangles.</summary>
    [Fact]
    public void Many_linked_bars_when_asked()
    {
        if (!int.TryParse(Environment.GetEnvironmentVariable("LAPIN_REBAR_BENCH"), out var beams) || beams <= 0) return;
        var f = F();
        var text = StructuralIfc(beams);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = IfcImport.Link(f.Session, Encoding.ASCII.GetBytes(text), "structure.ifc", null, null, out var diff);
        var linked = sw.Elapsed.TotalSeconds;
        var bars = f.Model.OfClass("IfcReinforcingBar").Count();
        var shared = f.Model.OfClass(StoredBodies.SharedClass).Count();
        var triangles = diff.Meshes?.Sum(mm => mm.Parts.Sum(p => p.Indices.Length / 3)) ?? 0;
        var instances = diff.Render.Count(x => x.Instance != null);
        var own = diff.Render.Where(x => x.Instance == null).Sum(x => (x.Parts ?? []).Sum(p => p.Indices.Length / 3));
        sw.Restart();
        f.Session.Query("view.looks3d", new JsonObject());
        Console.WriteLine($"REBAR-BENCH {beams} beams, {bars} bars ({text.Length / 1e6:F1} MB): linked in {linked:F1} s; {shared} shared bodies ({triangles} triangles) drawn as {instances} instances, {own} triangles of their own; looks3d {sw.Elapsed.TotalMilliseconds:F0} ms");
    }
}
