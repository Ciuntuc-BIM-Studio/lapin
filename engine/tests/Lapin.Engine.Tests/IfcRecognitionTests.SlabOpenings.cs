using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// The openings of a recognised floor (docs/spec-ifc.md, Golurile plăcilor): an IfcOpeningElement that goes vertically
/// through the floor's whole depth comes in as an opening of the native floor, under the file's GlobalId and name. Any
/// other opening is not carried: while the gate enforces, a floor that also has one it can carry stays a reference, and
/// a floor with none it can carry is the gate's to judge.
/// </summary>
public partial class IfcRecognitionTests
{
    /// <summary>The floor's area and thickness, and the stair opening's area.</summary>
    const double FloorArea = 6 * 4, FloorDepth = 0.2, StairArea = 2.6 * 1.2;

    /// <summary>
    /// A floor 6 × 4 m, 200 mm, its top at the storey, x 0..6 and y 0..4, with the openings <paramref name="bodies"/> gives
    /// (each a key, its GlobalId from it, and its body; millimetres) voiding it. The opening <paramref name="filled"/> names
    /// has a window in it; <paramref name="cut"/> makes the floor's body from its box; <paramref name="retyped"/> writes the
    /// void it names as another class than IfcOpeningElement.
    /// </summary>
    string FloorWith(Func<Foreign, (string Key, string Body)[]> bodies, string? filled = null, Func<Foreign, string, string>? cut = null, (string Key, string Type)? retyped = null)
    {
        var x = new Foreign();
        var box = x.Box(3000, 2000, -200, 6000, 4000, 200);
        var slab = x.Element("IFCSLAB", "so-slab", "Floor", x.Place(0, 0, 0), x.Shape(cut?.Invoke(x, box) ?? box), E("FLOOR"));
        x.Material(slab, x.Brick);
        var n = 0;
        foreach (var (key, body) in bodies(x))
        {
            var op = x.Element(key == retyped?.Key ? retyped.Value.Type : "IFCOPENINGELEMENT", key, "Stair opening", x.Place(0, 0, 0), x.Shape(body), E("OPENING"));
            x.F.Add("IFCRELVOIDSELEMENT", x.G($"so-v{n++}"), Null, Null, Null, slab, op);
            if (key != filled) continue;
            var hatch = x.Element("IFCWINDOW", "so-hatch", "Hatch", x.Place(0, 0, 0), x.Shape(x.Box(600, 3500, -50, 500, 400, 50)), "400.", "500.", E("SKYLIGHT"), Null, Null);
            x.F.Add("IFCRELFILLSELEMENT", x.G("so-f"), Null, Null, Null, op, hatch);
        }
        return x.Write(Path.Combine(_dir, "slab-opening.ifc"));
    }

    /// <summary>A stair opening 2.6 × 1.2 m centred on (3, <paramref name="cy"/>), extruded up from <paramref name="z0"/>.</summary>
    static string Stair(Foreign x, double z0 = -200, double height = 200, double cy = 2000) => x.Box(3000, cy, z0, 2600, 1200, height);

    /// <summary>A profile extruded along <paramref name="direction"/> from the height <paramref name="z"/>.</summary>
    static string Extruded(Foreign x, string profile, double z, string direction, double depth) =>
        x.F.Add("IFCEXTRUDEDAREASOLID", profile, x.F.Add("IFCAXIS2PLACEMENT3D", x.F.Add("IFCCARTESIANPOINT", $"(0.,0.,{R(z)})"), Null, Null), x.F.Add("IFCDIRECTION", direction), R(depth));

    static string StairProfile(Foreign x) =>
        x.F.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), Null, x.F.Add("IFCAXIS2PLACEMENT2D", x.F.Add("IFCCARTESIANPOINT", "(3000.,2000.)"), Null), "2600.", "1200.");

    /// <summary>A body with what stands over z = -100 cut away by a boolean.</summary>
    static string CutAtMidDepth(Foreign x, string body) =>
        x.F.Add("IFCBOOLEANCLIPPINGRESULT", E("DIFFERENCE"), body,
            x.F.Add("IFCHALFSPACESOLID", x.F.Add("IFCPLANE", x.F.Add("IFCAXIS2PLACEMENT3D", x.F.Add("IFCCARTESIANPOINT", "(0.,0.,-100.)"), x.F.Add("IFCDIRECTION", "(0.,0.,1.)"), Null)), False));

    /// <summary>The shallow recess's box as a triangulated face set, which no prism is read from.</summary>
    static string FaceSetRecess(Foreign x) => TriangulatedPrism(x, Plan([(350, 3300), (850, 3300), (850, 3700), (350, 3700)], -100), (0, 0, 100));

    static string SoId(string key) => Lapin.Ifc.Step.Guid("foreign-test", key);

    /// <summary>The floor came in native, passing the gate, with exactly these openings voiding it.</summary>
    static void AssertNativeFloor(Fixture f, IfcImportResult r, double openArea, params string[] openings)
    {
        var id = SoId("so-slab");
        var check = CheckOf(r, id);
        Assert.True(check.Pass, check.Reason);
        Assert.False(StoredBodies.Has(f.Model.Get(id)!));
        Assert.Equal(openings.Order(), f.Model.OfClass(IfcClass.OpeningElement).Select(o => o.Id).Order());
        Assert.Equal(openings.Order(), f.Model.RelatedOf(id, IfcClass.RelVoidsElement).Select(o => o.Id).Order());
        Assert.Equal((FloorArea - openArea) * FloorDepth, VolumeOf(f, id), 6);
    }

    [Fact]
    public void A_floors_stair_opening_comes_in_as_an_opening_of_the_native_floor()
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, FloorWith(x => [("so-op", Stair(x))]), null, out _);
        AssertNativeFloor(f, r, StairArea, SoId("so-op"));
        var opening = f.Model.Get(SoId("so-op"))!;
        Assert.Equal("Stair opening", opening.Name);
        Assert.Equal("OPENING", opening.PredefinedType);
    }

    [Theory]
    [InlineData("edge")]
    [InlineData("proud")]
    [InlineData("downward")]
    [InlineData("brep")]
    public void A_through_void_of_any_prism_form_is_carried(string form)
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, FloorWith(x => [("so-op", form switch
        {
            // Flush with the floor's edge at y = 0.
            "edge" => Stair(x, cy: 600),
            // Past both faces.
            "proud" => Stair(x, z0: -300, height: 400),
            "downward" => Extruded(x, StairProfile(x), 0, "(0.,0.,-1.)", 200),
            _ => BrepBox(x, 1700, 1400, -200, 4300, 2600, 0),
        })]), null, out _);
        AssertNativeFloor(f, r, StairArea, SoId("so-op"));
    }

    [Theory]
    [InlineData("recess")]
    [InlineData("short at the bottom")]
    [InlineData("short at the top")]
    [InlineData("sloped")]
    [InlineData("holed")]
    [InlineData("clipped")]
    [InlineData("partly read")]
    public void A_void_that_is_no_through_opening_keeps_the_floor_a_reference(string kind)
    {
        var path = FloorWith(x =>
        {
            var f = x.F;
            string Ring(params string[] points) => f.Add("IFCPOLYLINE", L(points.Append(points[0]).Select(p => f.Add("IFCCARTESIANPOINT", p))));
            return [("so-op", kind switch
            {
                "recess" => Stair(x, z0: -100, height: 100),
                "short at the bottom" => Stair(x, z0: -198.5, height: 198.5),
                "short at the top" => Stair(x, height: 198.5),
                "sloped" => Extruded(x, StairProfile(x), -200, "(0.6,0.,0.8)", 250),
                "holed" => Extruded(x, f.Add("IFCARBITRARYPROFILEDEFWITHVOIDS", E("AREA"), Null,
                    Ring("(1700.,1400.)", "(4300.,1400.)", "(4300.,2600.)", "(1700.,2600.)"),
                    L([Ring("(2500.,1700.)", "(3500.,1700.)", "(3500.,2300.)", "(2500.,2300.)")])), -200, "(0.,0.,1.)", 200),
                "partly read" => $"{Stair(x)},{FaceSetRecess(x)}",
                _ => CutAtMidDepth(x, Stair(x)),
            })];
        });
        var enforced = Full();
        var r = IfcImport.Run(enforced.Session, path, null, out _);
        var id = SoId("so-slab");
        Assert.True(StoredBodies.Has(enforced.Model.Get(id)!));
        Assert.True(CheckOf(r, id).Reverted);
        Assert.Empty(enforced.Model.OfClass(IfcClass.OpeningElement));
        // With the gate off the floor stays native, and still takes no opening from such a void.
        var off = Full();
        IfcImport.Run(off.Session, path, new IfcImportOptions(Fidelity: new FidelityOptions(FidelityOptions.Off)), out _);
        Assert.False(StoredBodies.Has(off.Model.Get(id)!));
        Assert.Empty(off.Model.OfClass(IfcClass.OpeningElement));
    }

    /// <summary>The 1 mm of the gate: a void that misses a face by less goes through.</summary>
    [Fact]
    public void A_void_short_of_a_face_by_less_than_a_millimetre_is_carried()
    {
        var f = Full();
        IfcImport.Run(f.Session, FloorWith(x => [("so-op", Stair(x, z0: -199.5, height: 199))]),
            new IfcImportOptions(Fidelity: new FidelityOptions(FidelityOptions.Off)), out _);
        Assert.Equal(SoId("so-op"), Assert.Single(f.Model.OfClass(IfcClass.OpeningElement)).Id);
    }

    [Fact]
    public void A_floor_the_gate_reverts_takes_its_native_openings_with_it()
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, FloorWith(x => [("so-op", Stair(x)), ("so-shaft", x.Box(600, 3500, -200, 800, 600, 200))], cut: CutAtMidDepth), null, out _);
        Assert.True(CheckOf(r, SoId("so-slab")).Reverted);
        AssertReferenceFloor(f);
        Assert.DoesNotContain(f.Model.Relations, x => x.Class == IfcClass.RelVoidsElement);
    }

    /// <summary>The floor came in as the file's reference, holding both its openings, with no native opening.</summary>
    static void AssertReferenceFloor(Fixture f)
    {
        var floor = f.Model.Get(SoId("so-slab"))!;
        Assert.True(StoredBodies.Has(floor));
        Assert.Equal(2, ((System.Text.Json.Nodes.JsonArray)floor.Attrs[StoredBodies.OpeningsKey]!).Count);
        Assert.Empty(f.Model.OfClass(IfcClass.OpeningElement));
    }

    /// <summary>A 0.5 × 0.4 × 0.1 m recess in the top is 0.48 % of the floor's volume: within the gate's 0.5 %.</summary>
    static string ShallowRecess(Foreign x) => x.Box(600, 3500, -100, 500, 400, 100);

    [Theory]
    [InlineData("IFCOPENINGELEMENT")]
    [InlineData("IFCOPENINGSTANDARDCASE")]
    [InlineData("IFCVOIDINGFEATURE")]
    public void A_floor_with_a_stair_opening_and_a_shallow_recess_stays_a_reference_with_both(string type)
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, FloorWith(x => [("so-op", Stair(x)), ("so-recess", ShallowRecess(x))], retyped: ("so-recess", type)), null, out _);
        AssertReferenceFloor(f);
        Assert.Equal(1, Assert.Single(r.Skipped!).Value);
    }

    /// <summary>One opening's body: a 0.2 m square shaft through the floor, and beside it the shallow recess as a face set.</summary>
    [Fact]
    public void A_floor_with_a_stair_opening_and_a_partly_read_one_stays_a_reference_with_both()
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, FloorWith(x => [("so-op", Stair(x)), ("so-partly", $"{x.Box(5200, 600, -200, 200, 200, 200)},{FaceSetRecess(x)}")]), null, out _);
        AssertReferenceFloor(f);
        Assert.Equal(1, Assert.Single(r.Skipped!).Value);
    }

    [Theory]
    [InlineData(FidelityOptions.Off)]
    [InlineData(FidelityOptions.Report)]
    public void Without_the_gate_enforced_that_floor_stays_native_with_its_stair_opening(string mode)
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, FloorWith(x => [("so-op", Stair(x)), ("so-recess", ShallowRecess(x))]),
            new IfcImportOptions(Fidelity: new FidelityOptions(mode)), out _);
        Assert.False(StoredBodies.Has(f.Model.Get(SoId("so-slab"))!));
        Assert.Equal(SoId("so-op"), Assert.Single(f.Model.OfClass(IfcClass.OpeningElement)).Id);
        Assert.Empty(r.Skipped!);
    }

    /// <summary>
    /// A filled recess is within the gate's tolerances; a filled through opening leaves a body that is a prism with two
    /// holes, which recognition from triangles could make a floor of.
    /// </summary>
    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    public void A_floor_with_a_stair_opening_and_a_filled_one_stays_a_reference_with_its_fill(double depth)
    {
        var f = Full();
        IfcImport.Run(f.Session, FloorWith(x => [("so-op", Stair(x)), ("so-filled", x.Box(600, 3500, -depth, 500, 400, depth))], filled: "so-filled"), null, out _);
        AssertReferenceFloor(f);
        Assert.True(StoredBodies.Has(f.Model.Get(SoId("so-hatch"))!));
    }

    [Fact]
    public void A_second_import_keeps_one_opening_with_the_files_id()
    {
        var path = FloorWith(x => [("so-op", Stair(x))]);
        var f = Full();
        IfcImport.Run(f.Session, path, null, out _);
        var r = IfcImport.Run(f.Session, path, null, out _);
        AssertNativeFloor(f, r, StairArea, SoId("so-op"));
        Assert.Single(f.Model.Relations, x => x.Class == IfcClass.RelVoidsElement);
    }

    [Fact]
    public void An_opening_whose_id_is_taken_gets_one_of_its_own()
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, FloorWith(x => [("so-op", Stair(x)), ("so-op", x.Box(600, 3500, -200, 800, 600, 200))]), null, out _);
        var openings = f.Model.OfClass(IfcClass.OpeningElement).Select(o => o.Id).ToArray();
        Assert.Equal(2, openings.Length);
        Assert.Contains(SoId("so-op"), openings);
        AssertNativeFloor(f, r, StairArea + 0.8 * 0.6, openings);
    }

    [Fact]
    public void An_IFC_export_writes_the_floors_opening_under_the_files_id()
    {
        var f = Full();
        IfcImport.Run(f.Session, FloorWith(x => [("so-op", Stair(x))]), null, out _);
        var exported = Path.Combine(_dir, "slab-opening-out.ifc");
        IfcExport.Write(f.Session, exported);
        var step = StepFile.Read(File.ReadAllBytes(exported));
        var slab = step.OfType("IFCSLAB").Single(e => (string?)e[0] == SoId("so-slab"));
        var opening = step.OfType("IFCOPENINGELEMENT").Single();
        Assert.Equal(SoId("so-op"), (string?)opening[0]);
        Assert.Equal("Stair opening", (string?)opening[2]);
        var voids = step.OfType("IFCRELVOIDSELEMENT").Single();
        Assert.Equal(slab.Id, ((StepRef)voids[4]!).Id);
        Assert.Equal(opening.Id, ((StepRef)voids[5]!).Id);
    }

    /// <summary>A floor makes no door or window: the gate keeps a floor with a filled hole through it a reference, so its fill is not lost.</summary>
    [Fact]
    public void A_floor_with_a_filled_opening_stays_a_reference_with_its_fill()
    {
        var x = new Foreign();
        var slab = x.Element("IFCSLAB", "so-slab", "Floor", x.Place(0, 0, 0), x.Shape(x.Box(3000, 2000, -200, 6000, 4000, 200)), E("FLOOR"));
        var op = x.Element("IFCOPENINGELEMENT", "so-op", "Hatch opening", x.Place(0, 0, 0), x.Shape(Stair(x)), E("OPENING"));
        var hatch = x.Element("IFCWINDOW", "so-hatch", "Hatch", x.Place(0, 0, 0), x.Shape(Stair(x, z0: -50, height: 50)), "1200.", "2600.", E("SKYLIGHT"), Null, Null);
        x.F.Add("IFCRELVOIDSELEMENT", x.G("so-v"), Null, Null, Null, slab, op);
        x.F.Add("IFCRELFILLSELEMENT", x.G("so-f"), Null, Null, Null, op, hatch);
        var f = Full();
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "slab-hatch.ifc")), null, out _);
        Assert.True(CheckOf(r, SoId("so-slab")).Reverted);
        Assert.True(StoredBodies.Has(f.Model.Get(SoId("so-slab"))!));
        Assert.True(StoredBodies.Has(f.Model.Get(SoId("so-hatch"))!));
        Assert.Empty(f.Model.OfClass(IfcClass.OpeningElement));
    }
}
