using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// How the export writes bodies (docs/spec-ifc.md, Geometria la export): as faceted B-reps (the default), or as face
/// sets (the same faces, a smaller file), and simplified within a tolerance on request.
/// </summary>
public class IfcExportGeometryTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ifc-geometry").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture House()
    {
        var f = new Fixture(new ArchitectureModule());
        var t = f.Type("Brick 30", ("Brick", 0.3, true));
        var w = f.Wall(0, 0, 6, 0, t);
        f.Wall(6, 0, 6, 4, t);
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = w, ["along"] = 3 });
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 4), new JsonArray(0, 4)) });
        return f;
    }

    string Export(Fixture f, string name, IfcExportOptions o)
    {
        var path = Path.Combine(_dir, name);
        IfcExport.Write(f.Session, path, o);
        return path;
    }

    /// <summary>Each element's volume and box, read back as it is (a reference).</summary>
    static Dictionary<string, (double Volume, double Lo, double Hi)> Read(string path)
    {
        var g = new Fixture(new ArchitectureModule());
        IfcImport.Run(g.Session, path, new IfcImportOptions("reference"), out _);
        return g.Model.Entities.Where(StoredBodies.Has).ToDictionary(e => e.Id, e =>
        {
            var parts = g.Session.Geometry(e.Id)!.Parts;
            var zs = parts.SelectMany(p => p.Solid.Vertices).Select(v => v.X + v.Y + v.Z).ToList();
            return (parts.Sum(p => Measure.Volume(p.Solid)), zs.Min(), zs.Max());
        });
    }

    [Fact]
    public void Face_sets_hold_the_same_bodies_in_fewer_entities()
    {
        var f = House();
        var brep = Export(f, "brep.ifc", new IfcExportOptions());
        var sets = Export(f, "sets.ifc", new IfcExportOptions(Geometry: "faceset"));
        var text = File.ReadAllText(sets);
        Assert.DoesNotContain("IFCFACETEDBREP", text);
        Assert.Contains("IFCPOLYGONALFACESET", text);
        Assert.Contains("'Tessellation'", text);
        Assert.Contains("IFCFACETEDBREP", File.ReadAllText(brep));
        var a = Read(brep);
        var b = Read(sets);
        Assert.Equal(a.Keys.Order(), b.Keys.Order());
        foreach (var (id, x) in a)
        {
            Assert.Equal(x.Volume, b[id].Volume, 6);
            Assert.Equal(x.Lo, b[id].Lo, 6);
            Assert.Equal(x.Hi, b[id].Hi, 6);
        }
        Assert.True(new FileInfo(sets).Length < new FileInfo(brep).Length);
    }

    /// <summary>A file with one duct: a tube meshed finely as a triangulated face set (radius 0.1 m, 2 m long, 128 facets round, 40 rings).</summary>
    static string Duct()
    {
        const int around = 128, along = 40;
        var pts = new List<string>();
        for (var j = 0; j <= along; j++)
            for (var i = 0; i < around; i++)
            {
                var a = 2 * Math.PI * i / around;
                pts.Add(string.Create(CultureInfo.InvariantCulture, $"({0.1 * Math.Cos(a):0.#########},{0.1 * Math.Sin(a):0.#########},{2.0 * j / along:0.#########})"));
            }
        var tris = new List<string>();
        for (var j = 0; j < along; j++)
            for (var i = 0; i < around; i++)
            {
                int p = j * around + i + 1, q = j * around + (i + 1) % around + 1, s = p + around, u = q + around;
                tris.Add($"({p},{q},{u})"); tris.Add($"({p},{u},{s})");
            }
        var b = new StringBuilder();
        b.Append("ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION(('ViewDefinition [ReferenceView]'),'2;1');\nFILE_NAME('duct.ifc','2026-10-30T10:00:00',(''),(''),'x','x','');\nFILE_SCHEMA(('IFC4'));\nENDSEC;\nDATA;\n");
        b.Append("#1=IFCPROJECT('0Proj00000000000000003',$,'Duct',$,$,$,$,(#4),#2);\n#2=IFCUNITASSIGNMENT((#3));\n#3=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);\n");
        b.Append("#4=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,#6,$);\n#5=IFCCARTESIANPOINT((0.,0.,0.));\n#6=IFCAXIS2PLACEMENT3D(#5,$,$);\n");
        b.Append("#10=IFCSITE('0Site00000000000000003',$,'Site',$,$,#11,$,$,.ELEMENT.,$,$,$,$,$);\n#11=IFCLOCALPLACEMENT($,#6);\n");
        b.Append("#12=IFCBUILDING('0Bldg00000000000000003',$,'Building',$,$,#13,$,$,.ELEMENT.,$,$,$);\n#13=IFCLOCALPLACEMENT(#11,#6);\n");
        b.Append("#14=IFCBUILDINGSTOREY('0Stry00000000000000003',$,'Level 0',$,$,#15,$,$,.ELEMENT.,0.);\n#15=IFCLOCALPLACEMENT(#13,#6);\n");
        b.Append("#16=IFCRELAGGREGATES('0Agg100000000000000003',$,$,$,#1,(#10));\n#17=IFCRELAGGREGATES('0Agg200000000000000003',$,$,$,#10,(#12));\n#18=IFCRELAGGREGATES('0Agg300000000000000003',$,$,$,#12,(#14));\n");
        b.Append($"#20=IFCCARTESIANPOINTLIST3D(({string.Join(",", pts)}),$);\n#21=IFCTRIANGULATEDFACESET(#20,$,.F.,({string.Join(",", tris)}),$);\n");
        b.Append("#22=IFCSHAPEREPRESENTATION(#4,'Body','Tessellation',(#21));\n#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#22));\n#24=IFCLOCALPLACEMENT(#15,#6);\n");
        b.Append("#25=IFCDUCTSEGMENT('2Duct00000000000000003',$,'Duct 1',$,$,#24,#23,$,.RIGIDSEGMENT.);\n");
        b.Append("#26=IFCRELCONTAINEDINSPATIALSTRUCTURE('0Cnt100000000000000003',$,$,$,(#25),#14);\nENDSEC;\nEND-ISO-10303-21;\n");
        return b.ToString();
    }

    static List<Vec3> Points(Fixture f, string id) => [.. f.Session.Geometry(id)!.Parts.SelectMany(p => p.Solid.Vertices)];
    static int Faces(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => p.Solid.Faces.Length);

    [Fact]
    public void A_meshed_body_is_simplified_within_the_tolerance_asked()
    {
        var src = Path.Combine(_dir, "duct.ifc");
        File.WriteAllText(src, Duct());
        var f = new Fixture(new ArchitectureModule());
        IfcImport.Run(f.Session, src, null, out _);
        const string id = "2Duct00000000000000003";
        // Read back, each straight strip along it is one face: 128 round.
        var before = Faces(f, id);
        Assert.Equal(128, before);

        var g = new Fixture(new ArchitectureModule());
        IfcImport.Run(g.Session, Export(f, "simple.ifc", new IfcExportOptions(Geometry: "faceset", Tolerance: 0.002)), null, out _);
        // Round it, as many facets as 2 mm allows on a radius of 0.1 m (a facet's sagitta r(1 − cos(π/n)) at most 2 mm: 16 or more),
        // each a strip or its two triangles: far fewer than 128 strips.
        Assert.InRange(Faces(g, id), 16, before / 2);
        // Within 2 mm of the tube, and as long as it was.
        var pts = Points(g, id);
        Assert.All(pts, p => Assert.InRange(Math.Sqrt(p.X * p.X + p.Y * p.Y), 0.1 - 0.002, 0.1 + 0.002));
        Assert.Equal(0, pts.Min(p => p.Z), 6);
        Assert.Equal(2, pts.Max(p => p.Z), 6);
        // What the file said of it is untouched.
        Assert.Equal("IfcDuctSegment", g.Model.Get(id)!.Class);

        // Without a tolerance, the curve is kept facet for facet.
        var h = new Fixture(new ArchitectureModule());
        IfcImport.Run(h.Session, Export(f, "exact.ifc", new IfcExportOptions(Geometry: "faceset")), null, out _);
        Assert.Equal(128, Points(h, id).Select(p => Math.Round(Math.Atan2(p.Y, p.X), 6)).Distinct().Count());
    }

    [Fact]
    public void A_body_Lapin_built_is_written_as_it_is()
    {
        var f = House();
        var exact = Read(Export(f, "exact.ifc", new IfcExportOptions()));
        var simplified = Read(Export(f, "simplified.ifc", new IfcExportOptions(Tolerance: 0.005)));
        foreach (var (id, x) in exact) Assert.Equal(x.Volume, simplified[id].Volume, 6);
    }
}
