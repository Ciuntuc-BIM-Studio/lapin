using Lapin.Alignments;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;
using Lapin.Structure;
using Lapin.Studio;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// The IFC reader against what other programs write and what a broken file holds: bodies swept downwards, names
/// beyond the Basic Multilingual Plane, a boolean that fails, references that loop, indices past their lists.
/// One bad element never fails the import.
/// </summary>
public class IfcRobustnessTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ifc-robust").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture Full() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new CivilModule(), new PhasesModule(), new StudioModule());

    /// <summary>A small foreign file: a project in millimetres with one storey, its elements contained in it.</summary>
    sealed class Tiny
    {
        public readonly Step F = new();
        public readonly string Ctx, StoreyPlacement, Storey;
        readonly List<string> _contained = [];

        public Tiny()
        {
            var origin = F.Add("IFCCARTESIANPOINT", "(0.,0.,0.)");
            var world = F.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null);
            Ctx = F.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", Null, S("Model"), "3", "1.E-05", world, Null);
            var mm = F.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), E("MILLI"), E("METRE"));
            var project = F.Add("IFCPROJECT", G("project"), Null, S("Tiny"), Null, Null, Null, Null, L([Ctx]), F.Add("IFCUNITASSIGNMENT", L([mm])));
            var pl = F.Add("IFCLOCALPLACEMENT", Null, world);
            var building = F.Add("IFCBUILDING", G("building"), Null, S("Building"), Null, Null, pl, Null, Null, E("ELEMENT"), Null, Null, Null);
            StoreyPlacement = F.Add("IFCLOCALPLACEMENT", pl, world);
            Storey = F.Add("IFCBUILDINGSTOREY", G("storey"), Null, S("Ground"), Null, Null, StoreyPlacement, Null, Null, E("ELEMENT"), "0.");
            F.Add("IFCRELAGGREGATES", G("agg1"), Null, Null, Null, project, L([building]));
            F.Add("IFCRELAGGREGATES", G("agg2"), Null, Null, Null, building, L([Storey]));
        }

        public static string Gid(string key) => Guid("robust-test", key);
        public string G(string key) => S(Gid(key));

        public string Place(double x, double y, double z) =>
            F.Add("IFCLOCALPLACEMENT", StoreyPlacement, F.Add("IFCAXIS2PLACEMENT3D", F.Add("IFCCARTESIANPOINT", $"({R(x)},{R(y)},{R(z)})"), Null, Null));

        /// <summary>A rectangle dx × dy about (cx, cy), at height z0, extruded along (dirX, dirY, dirZ) by <paramref name="depth"/>.</summary>
        public string Extrusion(double cx, double cy, double z0, double dx, double dy, double depth, string dir = "(0.,0.,1.)")
        {
            var prof = F.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), Null, F.Add("IFCAXIS2PLACEMENT2D", F.Add("IFCCARTESIANPOINT", $"({R(cx)},{R(cy)})"), Null), R(dx), R(dy));
            var at = F.Add("IFCAXIS2PLACEMENT3D", F.Add("IFCCARTESIANPOINT", $"(0.,0.,{R(z0)})"), Null, Null);
            return F.Add("IFCEXTRUDEDAREASOLID", prof, at, F.Add("IFCDIRECTION", dir), R(depth));
        }

        public string Shape(string item, string kind = "SweptSolid") =>
            F.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L([F.Add("IFCSHAPEREPRESENTATION", Ctx, S("Body"), S(kind), L([item]))]));

        public string Element(string type, string key, string name, string placement, string shape, params string[] tail)
        {
            var id = F.Add(type, [G(key), Null, S(name), Null, Null, placement, shape, Null, .. tail]);
            _contained.Add(id);
            return id;
        }

        public string Write(string path)
        {
            F.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", G("contained"), Null, Null, Null, L(_contained), Storey);
            F.Write(path, Path.GetFileName(path), "ViewDefinition [ReferenceView_V1.2]");
            return path;
        }
    }

    /// <summary>The lowest and highest z of a reference's stored body.</summary>
    static (double Lo, double Hi) Heights(Entity e)
    {
        double lo = double.MaxValue, hi = double.MinValue;
        foreach (var part in e.Attrs[StoredBodies.Key]!["parts"]!.AsArray())
        {
            var (v, _) = StoredBodies.ReadPart(part!.AsObject());
            for (var i = 2; i < v.Length; i += 3) { lo = Math.Min(lo, v[i]); hi = Math.Max(hi, v[i]); }
        }
        return (lo, hi);
    }

    static int Id(string stepRef) => int.Parse(stepRef[1..]);

    /// <summary>
    /// A slab and a footing some exporters write swept downwards, from their top along (0, 0, −1): the body lies below
    /// its profile, as a reference and recognised alike (the reference stood one thickness too high).
    /// </summary>
    [Fact]
    public void A_body_swept_downwards_lies_below_its_profile()
    {
        var x = new Tiny();
        x.Element("IFCSLAB", "slab", "Floor", x.Place(0, 0, 0), x.Shape(x.Extrusion(3000, 2000, 0, 6000, 4000, 200, "(0.,0.,-1.)")), E("FLOOR"));
        // A footing swept down and sideways: its bottom 500 below its top, its sides leaning.
        var leaning = x.Element("IFCFOOTING", "footing", "F1", x.Place(10000, 0, 0), x.Shape(x.Extrusion(0, 0, 0, 1000, 1000, 500, "(0.6,0.,-0.8)")), E("PAD_FOOTING"));
        var path = x.Write(Path.Combine(_dir, "down.ifc"));

        var model = new IfcModel(StepFile.Read(path));
        var mesh = model.Shape(Id(leaning)).Body.Single().Mesh;
        Assert.Equal(-0.4, mesh.V.Min(p => p.Z), 6);
        Assert.Equal(0, mesh.V.Max(p => p.Z), 6);
        Assert.Equal(9.5, mesh.V.Min(p => p.X), 6);
        Assert.Equal(10.8, mesh.V.Max(p => p.X), 6);
        Assert.True(mesh.Closed());

        var reference = Full();
        IfcImport.Run(reference.Session, path, new IfcImportOptions("reference"), out _);
        var (lo, hi) = Heights(reference.Session.Model.Get(Tiny.Gid("slab"))!);
        Assert.Equal((-0.2, 0.0), (Math.Round(lo, 6), Math.Round(hi, 6)));

        var native = Full();
        IfcImport.Run(native.Session, path, null, out _);
        var slab = native.Session.Model.Get(Tiny.Gid("slab"))!;
        Assert.Equal(IfcClass.Slab, slab.Class);
        Assert.Equal(0, slab.Num("offset"), 6);
        Assert.Equal(0.2, LayerSets.BandsOf(native.Session.Model, slab.Id)!.Sum(b => b.To - b.From), 6);
    }

    static StepFile OneEntity(string args) => StepFile.Parse($"ISO-10303-21;\nHEADER;\nFILE_SCHEMA(('IFC4'));\nENDSEC;\nDATA;\n#1=IFCLABEL({args});\nENDSEC;\nEND-ISO-10303-21;\n");

    /// <summary>
    /// A name beyond the Basic Multilingual Plane (an emoji, a mathematical letter) written as \X4\ and read back the same;
    /// a foreign file's surrogate pair in \X2\ read as the character it makes (it threw, failing the import).
    /// </summary>
    [Fact]
    public void Names_beyond_the_BMP_survive_a_round_trip()
    {
        const string name = "Bucătărie 🍳 𝑥 = 'a\\b'";
        var written = S(name);
        Assert.Contains("\\X4\\0001F373\\X0\\", written);
        Assert.Contains("\\X4\\0001D465\\X0\\", written);
        Assert.Contains("\\X2\\0103\\X0\\", written);
        Assert.Equal(name, OneEntity(written).Entities[1][0]);
        // As another program writes them: UTF-16 units, the pair in one \X2\ run.
        Assert.Equal("🍳 x", OneEntity("'\\X2\\D83CDF73\\X0\\ x'").Entities[1][0]);
        // Malformed escapes kept as written, never thrown.
        Assert.Equal("a\\X\\4", StepFile.Decode("a\\X\\4"));
        Assert.Equal("\\X2\\ZZZZ\\X0\\", StepFile.Decode("\\X2\\ZZZZ\\X0\\"));
        Assert.Equal("\\X2\\00E\\X0\\", StepFile.Decode("\\X2\\00E\\X0\\"));
        Assert.Equal("�", StepFile.Decode("\\X4\\7FFFFFFF\\X0\\"));
        Assert.Equal("é", StepFile.Decode("\\X\\E9"));
    }

    /// <summary>
    /// A Lapin file of many elements read back natively: each element's relationships (its type, material, openings,
    /// joins) found through indexes made once, not by scanning every relationship of the file for each element
    /// (10,000 elements took some 10⁹ steps).
    /// </summary>
    [Fact]
    public void Many_native_elements_import_in_linear_time()
    {
        const int count = 6000;
        var x = new Tiny();
        var material = x.F.Add("IFCMATERIAL", S("Concrete"), Null, Null);
        var type = x.F.Add("IFCBUILDINGELEMENTPROXYTYPE", x.G("type"), Null, S("Block"), Null, Null, Null, Null, Null, Null, E("NOTDEFINED"));
        var typed = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var e = x.Element("IFCBUILDINGELEMENTPROXY", $"e{i}", $"E{i}", x.Place(i, 0, 0), Null, E("ELEMENT"));
            var intent = x.F.Add("IFCPROPERTYSET", x.G($"intent{i}"), Null, S(IfcExport.IntentSetName), Null,
                L([x.F.Add("IFCPROPERTYSINGLEVALUE", S("LapinClass"), Null, Typed("IFCLABEL", S("IfcBuildingElementProxy")), Null),
                   x.F.Add("IFCPROPERTYSINGLEVALUE", S("index"), Null, Typed("IFCREAL", R(i)), Null)]));
            x.F.Add("IFCRELDEFINESBYPROPERTIES", x.G($"def{i}"), Null, Null, Null, L([e]), intent);
            x.F.Add("IFCRELASSOCIATESMATERIAL", x.G($"mat{i}"), Null, Null, Null, L([e]), material);
            x.F.Add("IFCRELDEFINESBYTYPE", x.G($"rt{i}"), Null, Null, Null, L([e]), type);
            if (i > 0) x.F.Add("IFCRELCONNECTSPATHELEMENTS", x.G($"join{i}"), Null, S("L"), S("butt"), Null, e, typed[^1], Null, Null, E("ATEND"), E("ATSTART"));
            typed.Add(e);
        }
        var path = x.Write(Path.Combine(_dir, "many.ifc"));
        var fx = Full();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        IfcImport.Run(fx.Session, path, null, out _);
        clock.Stop();
        // About 0.6 s here, 12 s with the scans: a margin for a loaded machine.
        Assert.True(clock.ElapsedMilliseconds < 6000, $"{count} native elements took {clock.ElapsedMilliseconds} ms");
        Assert.Equal(count, fx.Session.Model.OfClass("IfcBuildingElementProxy").Count());
        Assert.Equal(count - 1, Enumerable.Range(1, count - 1).Count(i => fx.Session.Model.RelationsOf(Tiny.Gid($"e{i}"), IfcClass.RelConnectsPathElements).Any()));
    }

    /// <summary>
    /// Alignments read in the project's own units: a project in metres whose file also holds a millimetre unit (a
    /// property's) reads its alignments in metres (they came 1000× too small), one in feet in feet, its angles in radians.
    /// </summary>
    [Theory]
    [InlineData("#5=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);", "#6=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);", 50.0)]
    [InlineData("#5=IFCCONVERSIONBASEDUNIT(#4,.LENGTHUNIT.,'FOOT',#7);#7=IFCMEASUREWITHUNIT(IFCLENGTHMEASURE(304.8),#6);", "#6=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);", 15.24)]
    public void Alignments_are_read_in_the_projects_units(string length, string other, double metres)
    {
        var ifc = $"""
            ISO-10303-21;
            DATA;
            #1=IFCPROJECT('0YvctVUKr0kugbFTf53O9L',$,'P',$,$,$,$,$,#2);
            #2=IFCUNITASSIGNMENT((#5,#8));
            #3=IFCSIUNIT(*,.PLANEANGLEUNIT.,$,.RADIAN.);
            #4=IFCDIMENSIONALEXPONENTS(1,0,0,0,0,0,0);
            {length}
            {other}
            #8=IFCSIUNIT(*,.PLANEANGLEUNIT.,$,.RADIAN.);
            #9=IFCCONVERSIONBASEDUNIT(#4,.PLANEANGLEUNIT.,'DEGREE',#10);
            #10=IFCMEASUREWITHUNIT(IFCPLANEANGLEMEASURE(0.0174532925199433),#3);
            #20=IFCALIGNMENT('g',$,'Main',$,$,$,$,$);
            #21=IFCALIGNMENTHORIZONTAL('h',$,$,$,$,$,$);
            #23=IFCRELNESTS('n',$,$,$,#20,(#21));
            #28=IFCCARTESIANPOINT((0.,0.));
            #29=IFCALIGNMENTHORIZONTALSEGMENT($,$,#28,1.,0.,0.,50.,$,.LINE.);
            #30=IFCALIGNMENTSEGMENT('a',$,$,$,$,$,$,#29);
            #34=IFCRELNESTS('m',$,$,$,#21,(#30));
            ENDSEC;
            END-ISO-10303-21;
            """;
        var h = Assert.Single(Assert.Single(IfcAlignmentReader.Read(ifc)).Alignment.Horizontal);
        Assert.Equal(metres, h.Length, 9);
        Assert.Equal(1.0, h.StartDirection, 12);
    }

    /// <summary>Booleans that fail as manifold does on a body that is not closed.</summary>
    sealed class FailingBooleans : ISolidBooleans
    {
        public Solid? Difference(Solid a, IReadOnlyList<Solid> cutters, string? tag = null) => throw new InvalidOperationException("manifold: error 1");
        public Solid? Union(IReadOnlyList<Solid> solids, string? tag = null) => throw new InvalidOperationException("manifold: error 1");
        public Solid? Intersection(Solid a, Solid b, string? tag = null) => throw new InvalidOperationException("manifold: error 1");
    }

    /// <summary>A box clipped by a half-space whose booleans fail: the body comes uncut, as it would without booleans (the import failed).</summary>
    [Fact]
    public void A_boolean_that_fails_leaves_the_body_uncut()
    {
        var x = new Tiny();
        var box = x.Extrusion(0, 0, 0, 1000, 1000, 3000);
        var plane = x.F.Add("IFCPLANE", x.F.Add("IFCAXIS2PLACEMENT3D", x.F.Add("IFCCARTESIANPOINT", "(0.,0.,2000.)"), Null, Null));
        var clip = x.F.Add("IFCBOOLEANCLIPPINGRESULT", E("DIFFERENCE"), box, x.F.Add("IFCHALFSPACESOLID", plane, False));
        var col = x.Element("IFCCOLUMN", "column", "C1", x.Place(0, 0, 0), x.Shape(clip, "Clipping"), E("COLUMN"));
        var model = new IfcModel(StepFile.Read(x.Write(Path.Combine(_dir, "clip.ifc"))));
        var (body, _) = model.Shape(Id(col), new FailingBooleans());
        Assert.Equal(3.0, body.Single().Mesh.V.Max(p => p.Z), 6);
    }

    /// <summary>
    /// Elements whose references loop (a mapped item inside its own map, a composite curve its own segment, a style
    /// assignment holding itself) and values nested past any sense: each a FormatException, never a stack overflow
    /// (which ends the engine); the import leaves that element out, says so, and brings the rest.
    /// </summary>
    [Fact]
    public void References_that_loop_leave_their_element_out_and_the_import_goes_on()
    {
        var x = new Tiny();
        for (var i = 0; i < 9; i++)
            x.Element("IFCCOLUMN", $"c{i}", $"C{i}", x.Place(i * 2000, 0, 0), x.Shape(x.Extrusion(0, 0, 0, 300, 300, 3000)), E("COLUMN"));
        // A mapped item whose map's representation holds the mapped item itself: the ids it will take, written ahead.
        var op = x.F.Add("IFCCARTESIANTRANSFORMATIONOPERATOR3D", Null, Null, x.F.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), "1.", Null);
        var axis = x.F.Add("IFCAXIS2PLACEMENT3D", x.F.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null);
        var n = Id(axis);
        var rep = x.F.Add("IFCSHAPEREPRESENTATION", x.Ctx, S("Body"), S("MappedRepresentation"), $"(#{n + 3})");
        var map = x.F.Add("IFCREPRESENTATIONMAP", axis, rep);
        var item = x.F.Add("IFCMAPPEDITEM", map, op);
        Assert.Equal(n + 3, Id(item));
        var looped = x.F.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L([x.F.Add("IFCSHAPEREPRESENTATION", x.Ctx, S("Body"), S("MappedRepresentation"), L([item]))]));
        x.Element("IFCBUILDINGELEMENTPROXY", "looped", "Loop", x.Place(0, 5000, 0), looped, E("ELEMENT"));
        // A composite curve whose one segment is the curve itself, as a profile.
        var m = Id(x.F.Add("IFCCARTESIANPOINT", "(0.,0.)"));
        var segment = x.F.Add("IFCCOMPOSITECURVESEGMENT", E("CONTINUOUS"), True, $"#{m + 2}");
        var curve = x.F.Add("IFCCOMPOSITECURVE", L([segment]), False);
        Assert.Equal(m + 2, Id(curve));
        var prof = x.F.Add("IFCARBITRARYCLOSEDPROFILEDEF", E("AREA"), Null, curve);
        var swept = x.F.Add("IFCEXTRUDEDAREASOLID", prof, x.F.Add("IFCAXIS2PLACEMENT3D", x.F.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null), x.F.Add("IFCDIRECTION", "(0.,0.,1.)"), "100.");
        x.Element("IFCSLAB", "curve", "Curve", x.Place(0, 8000, 0), x.Shape(swept), E("FLOOR"));
        var path = x.Write(Path.Combine(_dir, "loops.ifc"));

        var model = new IfcModel(StepFile.Read(path));
        var proxy = model.Products.Single(p => p.GlobalId == Tiny.Gid("looped"));
        Assert.Throws<FormatException>(() => model.Shape(proxy.Id));

        foreach (var mode in new[] { "auto", "reference" })
        {
            var fx = Full();
            var r = IfcImport.Run(fx.Session, path, new IfcImportOptions(mode), out _);
            for (var i = 0; i < 9; i++) Assert.NotNull(fx.Session.Model.Get(Tiny.Gid($"c{i}")));
            Assert.Null(fx.Session.Model.Get(Tiny.Gid("looped")));
            Assert.Null(fx.Session.Model.Get(Tiny.Gid("curve")));
            Assert.Contains(r.Notes, note => note.StartsWith("2 element(s) left out") && note.Contains("Loop"));
        }

        // Values nested past the limit, and malformed tokens: a FormatException naming the entity.
        var deep = new string('(', 200) + new string(')', 200);
        Assert.Contains("#1", Assert.Throws<FormatException>(() => OneEntity(deep).Entities[1]).Message);
        Assert.Throws<FormatException>(() => OneEntity(".ELEMENT").Entities[1]);
        Assert.Throws<FormatException>(() => OneEntity("\"0FF").Entities[1]);
        Assert.Throws<FormatException>(() => OneEntity("#99999999999").Entities[1]);
        Assert.Throws<FormatException>(() => StepFile.Parse("ISO-10303-21;\nDATA;\n#99999999999=IFCLABEL('x');\nENDSEC;\n"));
        Assert.Throws<FormatException>(() => StepFile.Parse("ISO-10303-21;\nDATA;\n#1=IFCLABEL(('x',").Entities[1]);
        Assert.Equal(".ELEMENT.", "." + ((StepEnum)OneEntity(".ELEMENT.").Entities[1][0]!).Value + ".");
    }
}
