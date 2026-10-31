using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The import's rules (docs/spec-ifc.md, Regulile importului), general whatever program wrote the file: a body the
/// file repeats kept once; flat bodies, elements far from the rest, duplicates and ports or openings written as objects
/// left out on request; meshes simplified within a tolerance; numbers written as text read as numbers; property sets
/// left out by name.
/// </summary>
public class IfcImportRulesTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ifc-rules").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture Make() => new(new ArchitectureModule());

    /// <summary>A foreign file in metres, one storey; each element its own box (its own shape, as Navisworks writes them), with sets.</summary>
    internal sealed class Plant
    {
        readonly StringBuilder _s = new();
        readonly List<int> _contained = [];
        int _id = 100;
        static string R(double v) => v.ToString("0.0#########", CultureInfo.InvariantCulture);

        public Plant()
        {
            _s.Append("""
                ISO-10303-21;
                HEADER;
                FILE_DESCRIPTION(('ViewDefinition [ReferenceView_V1.2]'),'2;1');
                FILE_NAME('plant.ifc','2026-10-30T00:00:00',(''),(''),'BIMCamel IFC Exporter','BIMCamel','');
                FILE_SCHEMA(('IFC4'));
                ENDSEC;
                DATA;
                #1=IFCCARTESIANPOINT((0.,0.,0.));
                #2=IFCAXIS2PLACEMENT3D(#1,$,$);
                #3=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,#2,$);
                #4=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
                #5=IFCUNITASSIGNMENT((#4));
                #6=IFCPROJECT('0RulesProject000000001',$,'Plant',$,$,$,$,(#3),#5);
                #7=IFCLOCALPLACEMENT($,#2);
                #8=IFCSITE('0RulesSite000000000001',$,'Site',$,$,#7,$,$,.ELEMENT.,$,$,$,$,$);
                #9=IFCLOCALPLACEMENT(#7,#2);
                #10=IFCBUILDING('0RulesBuilding0000001',$,'Building',$,$,#9,$,$,.ELEMENT.,$,$,$);
                #11=IFCLOCALPLACEMENT(#9,#2);
                #12=IFCBUILDINGSTOREY('0RulesStorey000000001',$,'Storey',$,$,#11,$,$,.ELEMENT.,0.);
                #13=IFCRELAGGREGATES('0RulesAgg100000000001',$,$,$,#6,(#8));
                #14=IFCRELAGGREGATES('0RulesAgg200000000001',$,$,$,#8,(#10));
                #15=IFCRELAGGREGATES('0RulesAgg300000000001',$,$,$,#10,(#12));

                """);
        }

        int Add(string entity) { var id = _id++; _s.Append($"#{id}={entity};\n"); return id; }

        static string Gid(string key) => Step.Guid("rules-test", key);

        /// <summary>A box of the given size at x, y, z (its low corner), as a triangulated face set in world coordinates; a 2D square when <paramref name="flat"/>.</summary>
        public string Box(string name, double x, double y, double z, double size = 1, bool flat = false, params (string Set, string Prop, string Value)[] props) =>
            Element("IFCBUILDINGELEMENTPROXY", "$", name, x, y, z, size, flat, props);

        /// <summary>A box as an element of another class (its entity name and predefined type, "$" for none).</summary>
        public string Element(string cls, string predefined, string name, double x, double y, double z, double size = 1, bool flat = false, params (string Set, string Prop, string Value)[] props)
        {
            var dz = flat ? 0 : size;
            var pts = new[] { (0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1) }
                .Select(p => $"({R(x + p.Item1 * size)},{R(y + p.Item2 * size)},{R(z + p.Item3 * dz)})");
            var list = Add($"IFCCARTESIANPOINTLIST3D(({string.Join(",", pts)}),$)");
            var tris = flat ? "((1,3,2),(1,4,3))" : "((1,3,2),(1,4,3),(5,6,7),(5,7,8),(1,2,6),(1,6,5),(2,3,7),(2,7,6),(3,4,8),(3,8,7),(4,1,5),(4,5,8))";
            var set = Add($"IFCTRIANGULATEDFACESET(#{list},$,.T.,{tris},$)");
            var rep = Add($"IFCSHAPEREPRESENTATION(#3,'Body','Tessellation',(#{set}))");
            var pds = Add($"IFCPRODUCTDEFINITIONSHAPE($,$,(#{rep}))");
            var place = Add("IFCLOCALPLACEMENT(#11,#2)");
            var gid = Gid(name);
            var e = Add($"{cls}('{gid}',$,'{name}',$,$,#{place},#{pds},$,{predefined})");
            _contained.Add(e);
            foreach (var g in props.GroupBy(p => p.Set))
            {
                var items = g.Select(p => Add($"IFCPROPERTYSINGLEVALUE('{p.Prop}',$,IFCTEXT('{p.Value}'),$)")).ToList();
                var ps = Add($"IFCPROPERTYSET('{Gid(name + "/" + g.Key)}',$,'{g.Key}',$,({string.Join(",", items.Select(i => "#" + i))}))");
                Add($"IFCRELDEFINESBYPROPERTIES('{Gid(name + "/rel/" + g.Key)}',$,$,$,(#{e}),#{ps})");
            }
            return gid;
        }

        public string Bytes(string dir)
        {
            Add($"IFCRELCONTAINEDINSPATIALSTRUCTURE('0RulesContained000001',$,$,$,({string.Join(",", _contained.Select(c => "#" + c))}),#12)");
            _s.Append("ENDSEC;\nEND-ISO-10303-21;\n");
            var path = Path.Combine(dir, $"plant-{Guid.NewGuid():N}.ifc");
            File.WriteAllText(path, _s.ToString());
            return path;
        }
    }

    static (Lapin.Geometry.Vec3 Lo, Lapin.Geometry.Vec3 Hi) Box(Fixture f, string id)
    {
        var v = f.Session.Geometry(id)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        return (new(v.Min(p => p.X), v.Min(p => p.Y), v.Min(p => p.Z)), new(v.Max(p => p.X), v.Max(p => p.Y), v.Max(p => p.Z)));
    }

    [Fact]
    public void A_body_the_file_repeats_is_kept_once_and_each_element_stands_where_it_did()
    {
        var x = new Plant();
        var a = x.Box("bolt-a", 900, 1000, 100, 0.1);
        var b = x.Box("bolt-b", 905.5, 1003.25, 101, 0.1);
        var c = x.Box("plate", 0, 0, 0, 2);
        var path = x.Bytes(_dir);

        var f = Make();
        var r = IfcImport.Run(f.Session, path, null, out _);
        Assert.Equal(3, r.References);
        var shared = Assert.Single(f.Model.OfClass(StoredBodies.SharedClass));
        Assert.Equal(shared.Id, f.Model.Get(a)!.Str(StoredBodies.BodyOf));
        Assert.Equal(shared.Id, f.Model.Get(b)!.Str(StoredBodies.BodyOf));
        // The plate, used once, keeps its own body.
        Assert.Null(f.Model.Get(c)!.Str(StoredBodies.BodyOf));
        var (lo, hi) = Box(f, b);
        Assert.Equal(905.5, lo.X, 6); Assert.Equal(1003.25, lo.Y, 6); Assert.Equal(101, lo.Z, 6);
        Assert.Equal(905.6, hi.X, 6); Assert.Equal(101.1, hi.Z, 6);
        Assert.Contains(r.Notes, n => n.Contains("2 element(s) share 1 bod(ies) the file repeats"));

        // Unticked, each keeps its own.
        var g = Make();
        IfcImport.Run(g.Session, path, new IfcImportOptions(Dedupe: false), out _);
        Assert.Empty(g.Model.OfClass(StoredBodies.SharedClass));
    }

    [Fact]
    public void Flat_far_duplicate_and_pseudo_elements_are_left_out_only_as_chosen()
    {
        var x = new Plant();
        for (var i = 0; i < 8; i++) x.Box($"pipe-{i}", 900 + 3 * i, 1000, 100, 1 + 0.1 * i);
        var text = x.Box("label", 905, 1002, 100, 3, flat: true);
        var title = x.Box("title block", 200, 250, 100, 0.5);
        var first = x.Box("weld", 950, 1010, 100, 0.05);
        var twin = x.Box("weld copy", 950, 1010, 100, 0.05);
        var port = x.Box("port", 960, 1010, 100, 0.02, props: [("Elemento", "Tipo", "IfcDistributionPort")]);
        var opening = x.Box("IfcOpeningElement: Opening", 970, 1010, 100, 0.5);
        var path = x.Bytes(_dir);

        var all = Make();
        IfcImport.Run(all.Session, path, null, out _);
        Assert.All(new[] { text, title, twin, port, opening }, id => Assert.NotNull(all.Model.Get(id)));

        var clean = Make();
        var r = IfcImport.Run(clean.Session, path, new IfcImportOptions(Flat: true, FarAway: 1000, Duplicates: true, Pseudo: true), out _);
        Assert.Null(clean.Model.Get(text));
        Assert.Null(clean.Model.Get(title));
        Assert.Null(clean.Model.Get(twin));
        Assert.NotNull(clean.Model.Get(first));
        Assert.Null(clean.Model.Get(port));
        Assert.Null(clean.Model.Get(opening));
        Assert.Equal(9, r.References);
        Assert.Contains(r.Notes, n => n.Contains("1 element(s) left out, as chosen: flat bodies"));
        Assert.Contains(r.Notes, n => n.Contains("further than 1000 m"));
        Assert.Contains(r.Notes, n => n.Contains("the same body in the same place"));
        Assert.Contains(r.Notes, n => n.Contains("2 element(s) left out, as chosen: ports, openings"));
    }

    [Fact]
    public void Numbers_written_as_text_are_read_as_numbers_and_technical_sets_left_out()
    {
        var x = new Plant();
        var id = x.Box("beam", 0, 0, 0, 1, props: [
            ("800293_ECOPLANTA", "WIDTH", "DoubleLength:0,01"), ("800293_ECOPLANTA", "WEIGHT_NET", "1.15395 kg"), ("800293_ECOPLANTA", "ELEVATION", "+106.059"),
            ("800293_ECOPLANTA", "PROJECT NUMBER", "800293"), ("800293_ECOPLANTA", "PROFILE", "PL60*10"),
            ("Transformar", "Traslación.X", "DoubleLength:967,56")]);
        var path = x.Bytes(_dir);
        var survey = IfcSurvey.Of(StepFile.Read(path), "plant.ifc", 0);
        var sets = survey["sets"]!.AsArray().ToDictionary(s => (string)s!["name"]!, s => s!);
        Assert.False((bool)sets["800293_ECOPLANTA"]["technical"]!);
        Assert.True((bool)sets["Transformar"]["technical"]!);

        var f = Make();
        IfcImport.Run(f.Session, path, new IfcImportOptions(Numbers: true, SkipSets: new HashSet<string> { "Transformar" }), out _);
        var props = (JsonObject)f.Model.Get(id)!.Attrs["properties"]!;
        Assert.Null(props["Transformar"]);
        var p = (JsonObject)props["800293_ECOPLANTA"]!;
        Assert.Equal("0.01", (string?)p["WIDTH"]);
        Assert.Equal("106.059", (string?)p["ELEVATION"]);
        // A code stays as it was written; a profile is text.
        Assert.Equal("800293", (string?)p["PROJECT NUMBER"]);
        Assert.Equal("PL60*10", (string?)p["PROFILE"]);
        var exported = Path.Combine(_dir, "out.ifc");
        IfcExport.Write(f.Session, exported);
        var text = File.ReadAllText(exported);
        Assert.Contains("'WIDTH',$,IFCLENGTHMEASURE(0.01),$", text);
        Assert.Contains("'WEIGHT_NET',$,IFCMASSMEASURE(1.15395),$", text);
        Assert.Contains("'PROJECT NUMBER',$,IFCTEXT('800293'),$", text);
        Assert.DoesNotContain("Transformar", text);
    }

    [Fact]
    public void Meshes_are_simplified_within_the_tolerance_on_request()
    {
        // A tube of 128 facets round as a file meshes it (IfcExportGeometryTests' duct).
        var src = Path.Combine(_dir, "duct.ifc");
        File.WriteAllText(src, (string)typeof(IfcExportGeometryTests).GetMethod("Duct", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, null)!);
        var exact = Make();
        IfcImport.Run(exact.Session, src, null, out _);
        var simple = Make();
        var r = IfcImport.Run(simple.Session, src, new IfcImportOptions(Simplify: 0.002), out _);
        const string id = "2Duct00000000000000003";
        int Faces(Fixture f) => f.Session.Geometry(id)!.Parts.Sum(p => p.Solid.Faces.Length);
        Assert.True(Faces(simple) < Faces(exact) / 2, $"{Faces(exact)} → {Faces(simple)}");
        Assert.All(simple.Session.Geometry(id)!.Parts.SelectMany(p => p.Solid.Vertices), v => Assert.InRange(Math.Sqrt(v.X * v.X + v.Y * v.Y), 0.098, 0.102));
        Assert.Contains(r.Notes, n => n.StartsWith("Meshes simplified within 2 mm: 10,240 → ", StringComparison.Ordinal));
    }

    [Fact]
    public void The_rules_come_from_the_dialog_and_are_kept_for_recovery()
    {
        var o = EngineDispatcher.Rules(new IfcImportOptions(), new JsonObject
        {
            ["dedupe"] = false, ["flat"] = true, ["far"] = 1500, ["duplicates"] = true, ["pseudo"] = true, ["simplify"] = 0.005, ["numbers"] = true,
            ["skipSets"] = new JsonArray("Transformar", "Internal"),
        });
        Assert.False(o.Dedupe);
        Assert.True(o.Flat && o.Duplicates && o.Pseudo && o.Numbers);
        Assert.Equal(1500, o.FarAway);
        Assert.Equal(0.005, o.Simplify);
        Assert.False(o.Keeps("Internal"));
        Assert.True(o.Keeps("Repsol"));
        // Nothing sent: the defaults (a repeated body kept once; nothing left out).
        var d = EngineDispatcher.Rules(new IfcImportOptions(), null);
        Assert.True(d.Dedupe);
        Assert.False(d.Flat || d.Duplicates || d.Pseudo || d.Numbers);
        Assert.Null(d.FarAway);
    }

    [Fact]
    public void Text_numbers_are_read_by_their_prefix_their_unit_or_their_point()
    {
        Assert.Equal(("IFCLENGTHMEASURE(0.01)", "0.01"), IfcValueText.Parse("DoubleLength:0,01"));
        Assert.Equal(("IFCAREAMEASURE(3.549)", "3.549"), IfcValueText.Parse("DoubleArea:3,549"));
        Assert.Equal(("IFCVOLUMEMEASURE(3.E-05)", "3E-05"), IfcValueText.Parse("DoubleVolume:3E-05")); // STEP asks a real for its point
        Assert.Equal("IFCPLANEANGLEMEASURE(3.14159265358979)", IfcValueText.Parse("DoubleAngle:3,14159265358979")!.Value.Step);
        Assert.Equal("IFCINTEGER(4)", IfcValueText.Parse("Int32:4")!.Value.Step);
        Assert.Equal("IFCLENGTHMEASURE(0.25)", IfcValueText.Parse("250 mm")!.Value.Step);
        Assert.Equal("IFCREAL(-12.)", IfcValueText.Parse("-12")!.Value.Step);
        Assert.Null(IfcValueText.Parse("800293"));
        Assert.Null(IfcValueText.Parse("001"));
        Assert.Null(IfcValueText.Parse("1,234"));
        Assert.Null(IfcValueText.Parse("S-80"));
    }
}
