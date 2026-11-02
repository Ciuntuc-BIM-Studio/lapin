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
}
