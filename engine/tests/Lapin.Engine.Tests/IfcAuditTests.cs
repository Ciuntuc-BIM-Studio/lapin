using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The IFC findings of the audit of 15.11.2026 (docs/audit-2026-11-15.md): units of a material's properties, trimmed
/// curves, carried profiles, durations and dates of tasks, empty relationships, rare STEP forms, SI prefixes and the
/// one decoder of STEP strings.
/// </summary>
public class IfcAuditTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ifc-audit").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    /// <summary>A small IFC4 file: a project with <paramref name="units"/> (#3 onwards, #2 assigning <paramref name="assigned"/>), a storey and a proxy box, then <paramref name="data"/>.</summary>
    static string File4(string units, string assigned, string data) => $"""
ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [ReferenceView]'),'2;1');
FILE_NAME('audit.ifc','2026-11-15T10:00:00',('x'),('x'),'probe','probe','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('0Proj00000000000000001',$,'Audit',$,$,$,$,(#4),#2);
#2=IFCUNITASSIGNMENT(({assigned}));
{units}
#4=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,#6,$);
#5=IFCCARTESIANPOINT((0.,0.,0.));
#6=IFCAXIS2PLACEMENT3D(#5,$,$);
#10=IFCSITE('0Site00000000000000001',$,'Site',$,$,#11,$,$,.ELEMENT.,$,$,$,$,$);
#11=IFCLOCALPLACEMENT($,#6);
#12=IFCBUILDING('0Bldg00000000000000001',$,'Building',$,$,#13,$,$,.ELEMENT.,$,$,$);
#13=IFCLOCALPLACEMENT(#11,#6);
#14=IFCBUILDINGSTOREY('0Stry00000000000000001',$,'Level 0',$,$,#15,$,$,.ELEMENT.,0.);
#15=IFCLOCALPLACEMENT(#13,#6);
#16=IFCRELAGGREGATES('0Agg100000000000000001',$,$,$,#1,(#10));
#17=IFCRELAGGREGATES('0Agg200000000000000001',$,$,$,#10,(#12));
#18=IFCRELAGGREGATES('0Agg300000000000000001',$,$,$,#12,(#14));
#20=IFCRECTANGLEPROFILEDEF(.AREA.,$,$,1000.,1000.);
#21=IFCDIRECTION((0.,0.,1.));
#22=IFCEXTRUDEDAREASOLID(#20,#6,#21,1000.);
#23=IFCSHAPEREPRESENTATION(#4,'Body','SweptSolid',(#22));
#24=IFCPRODUCTDEFINITIONSHAPE($,$,(#23));
#25=IFCLOCALPLACEMENT(#15,#6);
#26=IFCBUILDINGELEMENTPROXY('2Prox00000000000000001',$,'Box',$,$,#25,#24,$,$);
#27=IFCRELCONTAINEDINSPATIALSTRUCTURE('0Cont00000000000000001',$,$,$,(#26),#14);
{data}
ENDSEC;
END-ISO-10303-21;
""";

    string Write(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        return path;
    }

    const string Mm = "#3=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);";

    // ─── P5: a material's measures in the project's units ────────────────────

    [Fact]
    public void A_materials_area_in_a_project_in_millimetres_is_read_in_its_area_unit_not_the_length_unit_squared()
    {
        var units = Mm + """

#30=IFCSIUNIT(*,.AREAUNIT.,$,.SQUARE_METRE.);
#31=IFCSIUNIT(*,.VOLUMEUNIT.,$,.CUBIC_METRE.);
#32=IFCSIUNIT(*,.AREAUNIT.,.CENTI.,.SQUARE_METRE.);
#33=IFCSIUNIT(*,.MASSUNIT.,.KILO.,.GRAM.);
#34=IFCDERIVEDUNITELEMENT(#33,1);
#35=IFCSIUNIT(*,.LENGTHUNIT.,.CENTI.,.METRE.);
#36=IFCDERIVEDUNITELEMENT(#35,-3);
#37=IFCDERIVEDUNIT((#34,#36),.MASSDENSITYUNIT.,$);
""";
        var data = """
#40=IFCMATERIAL('Foreign wool',$,$);
#41=IFCPROPERTYSINGLEVALUE('Face',$,IFCAREAMEASURE(2.5),$);
#42=IFCPROPERTYSINGLEVALUE('Batch',$,IFCVOLUMEMEASURE(3.),$);
#43=IFCPROPERTYSINGLEVALUE('Board',$,IFCLENGTHMEASURE(150.),$);
#44=IFCPROPERTYSINGLEVALUE('Sample',$,IFCAREAMEASURE(25000.),#32);
#45=IFCPROPERTYSINGLEVALUE('Density',$,IFCMASSDENSITYMEASURE(0.00014),$);
#46=IFCMATERIALPROPERTIES('Audit_Material',$,(#41,#42,#43,#44,#45),#40);
""";
        var f = new Fixture(new ArchitectureModule());
        IfcImport.Run(f.Session, Write("mat.ifc", File4(units, "#3,#30,#31,#37", data)), null, out _);
        var m = MaterialFields.Of(f.Model.OfClass(IfcClass.Material).Single(x => x.Name == "Foreign wool"));
        double V(string name) => (double)m.Param("Audit_Material." + name)!.Value!;
        Assert.Equal(2.5, V("Face"), 9);
        Assert.Equal(3, V("Batch"), 9);
        Assert.Equal(0.15, V("Board"), 9);
        // Its own unit (square centimetres) over the project's.
        Assert.Equal(2.5, V("Sample"), 9);
        // kg/cm³ → kg/m³.
        Assert.Equal(140, V("Density"), 6);

        // No area unit assigned: square metres (SI), as the quantities read it.
        var g = new Fixture(new ArchitectureModule());
        IfcImport.Run(g.Session, Write("mat2.ifc", File4(units, "#3", data)), null, out _);
        Assert.Equal(2.5, (double)MaterialFields.Of(g.Model.OfClass(IfcClass.Material).Single(x => x.Name == "Foreign wool")).Param("Audit_Material.Face")!.Value!, 9);
    }

    // ─── P16: every SI prefix ───────────────────────────────────────────────

    [Theory]
    [InlineData("EXA", 1e18)]
    [InlineData("PETA", 1e15)]
    [InlineData("TERA", 1e12)]
    [InlineData("GIGA", 1e9)]
    [InlineData("MEGA", 1e6)]
    [InlineData("KILO", 1e3)]
    [InlineData("MILLI", 1e-3)]
    [InlineData("NANO", 1e-9)]
    [InlineData("PICO", 1e-12)]
    [InlineData("FEMTO", 1e-15)]
    [InlineData("ATTO", 1e-18)]
    public void Every_SI_prefix_has_its_size(string prefix, double size)
    {
        var f = StepFile.Parse(File4($"#3=IFCSIUNIT(*,.LENGTHUNIT.,.{prefix}.,.METRE.);", "#3", ""));
        Assert.Equal(size, IfcModel.LengthUnit(f), size * 1e-12);
    }

    // ─── P7, P13: trimmed curves ─────────────────────────────────────────────

    static IfcModel Curves(string data, string angle = "") =>
        new(StepFile.Parse(File4("#3=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);" + angle, angle.Length > 0 ? "#3,#7" : "#3", data)));

    [Fact]
    public void A_line_trimmed_by_parameter_runs_between_its_parameters()
    {
        var m = Curves("""
#50=IFCCARTESIANPOINT((1.,2.));
#51=IFCDIRECTION((1.,0.));
#52=IFCVECTOR(#51,2.);
#53=IFCLINE(#50,#52);
#54=IFCTRIMMEDCURVE(#53,(IFCPARAMETERVALUE(0.5)),(IFCPARAMETERVALUE(2.)),.T.,.PARAMETER.);
#55=IFCTRIMMEDCURVE(#53,(IFCPARAMETERVALUE(2.)),(IFCPARAMETERVALUE(0.5)),.F.,.PARAMETER.);
#56=IFCCARTESIANPOINT((7.,2.));
#57=IFCTRIMMEDCURVE(#53,(#50,IFCPARAMETERVALUE(0.5)),(#56,IFCPARAMETERVALUE(2.)),.T.,.CARTESIAN.);
""");
        // P + t·V, V = 2 along x: from x = 2 to x = 5.
        Assert.Equal([new Vec3(2, 2, 0), new Vec3(5, 2, 0)], m.CurvePoints(54));
        // Against the line's direction, still from the first trim to the second.
        Assert.Equal([new Vec3(5, 2, 0), new Vec3(2, 2, 0)], m.CurvePoints(55));
        // Both forms given: the master's (the points).
        Assert.Equal([new Vec3(1, 2, 0), new Vec3(7, 2, 0)], m.CurvePoints(57));
    }

    [Fact]
    public void An_ellipse_trimmed_by_parameter_or_by_points_is_an_arc_by_the_chord_rule()
    {
        // Degrees as the project's angle unit.
        var deg = """

#7=IFCCONVERSIONBASEDUNIT(#8,.PLANEANGLEUNIT.,'DEGREE',#9);
#8=IFCDIMENSIONALEXPONENTS(0,0,0,0,0,0,0);
#9=IFCMEASUREWITHUNIT(IFCPLANEANGLEMEASURE(0.017453292519943295),#90);
#90=IFCSIUNIT(*,.PLANEANGLEUNIT.,$,.RADIAN.);
""";
        var m = Curves("""
#60=IFCCARTESIANPOINT((0.,0.));
#61=IFCAXIS2PLACEMENT2D(#60,$);
#62=IFCELLIPSE(#61,4.,2.);
#63=IFCTRIMMEDCURVE(#62,(IFCPARAMETERVALUE(0.)),(IFCPARAMETERVALUE(180.)),.T.,.PARAMETER.);
#64=IFCCARTESIANPOINT((4.,0.));
#65=IFCCARTESIANPOINT((-4.,0.));
#66=IFCTRIMMEDCURVE(#62,(#64),(#65),.T.,.CARTESIAN.);
#67=IFCTRIMMEDCURVE(#62,(#64),(#65),.F.,.CARTESIAN.);
""", deg);
        foreach (var (id, up) in new[] { (63, true), (66, true), (67, false) })
        {
            var pts = m.CurvePoints(id);
            Assert.Equal(Chords.Count(4, Math.PI, 2) + 1, pts.Count);
            Assert.Equal(4, pts[0].X, 9);
            Assert.Equal(-4, pts[^1].X, 9);
            // On the ellipse, through its top (counter-clockwise) or its bottom.
            Assert.All(pts, p => Assert.Equal(1, p.X * p.X / 16 + p.Y * p.Y / 4, 9));
            var mid = pts[pts.Count / 2];
            Assert.Equal(up ? 2 : -2, mid.Y, 1);
        }
    }

    [Fact]
    public void A_huge_or_broken_trimming_parameter_ends_quickly()
    {
        var m = Curves("""
#60=IFCCARTESIANPOINT((0.,0.));
#61=IFCAXIS2PLACEMENT2D(#60,$);
#62=IFCCIRCLE(#61,1.);
#63=IFCTRIMMEDCURVE(#62,(IFCPARAMETERVALUE(-1.E300)),(IFCPARAMETERVALUE(1.E300)),.T.,.PARAMETER.);
#64=IFCTRIMMEDCURVE(#62,(IFCPARAMETERVALUE(1.E300)),(IFCPARAMETERVALUE(-1.E300)),.F.,.PARAMETER.);
#65=IFCTRIMMEDCURVE(#62,(IFCPARAMETERVALUE(1.E308)),(IFCPARAMETERVALUE(-1.E308)),.T.,.PARAMETER.);
""");
        var t = System.Diagnostics.Stopwatch.StartNew();
        foreach (var id in new[] { 63, 64, 65 })
        {
            var pts = m.CurvePoints(id);
            Assert.True(pts.Count <= Chords.Circle(1) + 1);
            Assert.All(pts, p => Assert.True(double.IsFinite(p.X) && double.IsFinite(p.Y)));
        }
        Assert.True(t.Elapsed.TotalSeconds < 5);
        // The sweep along and against the sense, within one turn.
        Assert.Equal(Math.PI / 2, IfcModel.Sweep(0, Math.PI / 2, true), 12);
        Assert.Equal(-3 * Math.PI / 2, IfcModel.Sweep(0, Math.PI / 2, false), 12);
        Assert.Equal(2 * Math.PI, IfcModel.Sweep(1, 1, true), 12);
        Assert.Equal(0, IfcModel.Sweep(double.NaN, 1, true));
    }

    // ─── P8: carried profiles whole, or not at all ──────────────────────────

    /// <summary>A pipe of a material profile set whose profile's outline is <paramref name="outline"/> (#80), carried as a reference.</summary>
    static string ProfiledPipe(string outline, string profile = "#81=IFCARBITRARYCLOSEDPROFILEDEF(.AREA.,'Outline',#80);") => File4("#3=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);", "#3", $"""
#70=IFCCARTESIANPOINT((0.,0.));
#71=IFCCARTESIANPOINT((1.,0.));
#72=IFCCARTESIANPOINT((1.,1.));
#73=IFCCARTESIANPOINT((0.,1.));
#74=IFCPOLYLINE((#70,#71,#72));
#75=IFCPOLYLINE((#72,#73,#70));
{outline}
{profile}
#82=IFCMATERIAL('Steel',$,$);
#83=IFCMATERIALPROFILE('P',$,#82,#81,$,$);
#84=IFCMATERIALPROFILESET('Set',$,(#83),$);
#85=IFCPIPESEGMENT('2Pipe00000000000000001',$,'Pipe',$,$,#25,#24,$,.RIGIDSEGMENT.);
#86=IFCRELASSOCIATESMATERIAL('0Mat000000000000000001',$,$,$,(#85),#84);
#87=IFCRELCONTAINEDINSPATIALSTRUCTURE('0Cont00000000000000002',$,$,$,(#85),#14);
""");

    [Theory]
    [InlineData("#80=IFCCOMPOSITECURVE((#88,#89),.F.);\n#88=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#74);\n#89=IFCCOMPOSITECURVESEGMENT(.CONTINUOUS.,.T.,#75);")]
    [InlineData("#80=IFCINDEXEDPOLYCURVE(#88,$,.F.);\n#88=IFCCARTESIANPOINTLIST2D(((0.,0.),(1.,0.),(1.,1.),(0.,1.)),$);")]
    [InlineData("#80=IFCTRIMMEDCURVE(#88,(IFCPARAMETERVALUE(0.)),(IFCPARAMETERVALUE(360.)),.T.,.PARAMETER.);\n#88=IFCCIRCLE(#89,0.5);\n#89=IFCAXIS2PLACEMENT2D(#70,$);")]
    public void A_carried_profile_keeps_its_outline_whatever_the_curve(string outline)
    {
        var f = StepFile.Parse(ProfiledPipe(outline));
        var snap = IfcCarry.Snapshot(f, 84, root: true)!;
        Assert.NotNull(snap);
        var profile = snap["a"]![2]![0]!["a"]![3]!;
        Assert.Equal("IFCARBITRARYCLOSEDPROFILEDEF", (string?)profile["t"]);
        // The outline followed, and what it is drawn with (its first attribute: segments, points, the trimmed circle).
        var curve = Assert.IsType<JsonObject>(profile["a"]![2]);
        Assert.NotEqual("$", curve["a"]![0] is JsonValue v ? (string?)v : null);
    }

    [Fact]
    public void A_derived_profile_carries_its_operator()
    {
        var f = StepFile.Parse(ProfiledPipe("#80=IFCPOLYLINE((#70,#71,#72,#73,#70));",
            "#79=IFCARBITRARYCLOSEDPROFILEDEF(.AREA.,'Outline',#80);\n#78=IFCCARTESIANTRANSFORMATIONOPERATOR2D($,$,#70,2.);\n#81=IFCDERIVEDPROFILEDEF(.AREA.,'Twice',#79,#78,$);"));
        var profile = IfcCarry.Snapshot(f, 84, root: true)!["a"]![2]![0]!["a"]![3]!;
        Assert.Equal("IFCDERIVEDPROFILEDEF", (string?)profile["t"]);
        Assert.Equal("IFCCARTESIANTRANSFORMATIONOPERATOR2D", (string?)profile["a"]![3]!["t"]);
    }

    [Fact]
    public void A_profile_that_cannot_be_carried_whole_leaves_the_material_to_Lapin_and_no_required_attribute_is_empty()
    {
        // An outline Lapin does not follow (an offset curve): the carried set would have $ for the profile's OuterCurve.
        var text = ProfiledPipe("#80=IFCOFFSETCURVE2D(#74,0.1,.F.);");
        Assert.Null(IfcCarry.Snapshot(StepFile.Parse(text), 84, root: true));
        var g = new Fixture(new ArchitectureModule(), new Lapin.Structure.StructureModule(), new Lapin.Mep.MepModule());
        var r = IfcImport.Run(g.Session, Write("offset.ifc", text), new IfcImportOptions("reference"), out _);
        Assert.True(r.References >= 1);
        var path = Path.Combine(_dir, "offset-out.ifc");
        IfcExport.Write(g.Session, path, null);
        var written = File.ReadAllText(path);
        Assert.Contains("'2Pipe00000000000000001'", written);
        Assert.DoesNotContain("'Outline',$)", written);
        Assert.DoesNotContain("IFCMATERIALPROFILESET('Set',$,$", written);
        Assert.DoesNotContain("IFCMATERIALPROFILE('P',$,#82,$", written);
    }

    // ─── P12: no empty containment ──────────────────────────────────────────

    [Fact]
    public void A_storey_whose_only_element_went_into_an_assembly_elsewhere_gets_no_empty_containment()
    {
        var f = new Fixture(new ArchitectureModule());
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        var a = f.Wall(0, 0, 5, 0);
        f.Session.Edit("Assembly", edit =>
        {
            var asm = edit.NewId();
            edit.Add(new Entity(asm, "IfcElementAssembly", "Pair", []));
            Spatial.Contain(edit, first, asm);
            edit.Relate(IfcClass.RelAggregates, asm, a);
        });
        var path = Path.Combine(_dir, "asm.ifc");
        IfcExport.Write(f.Session, path, null);
        var text = File.ReadAllText(path);
        Assert.Contains("IFCELEMENTASSEMBLY(", text);
        Assert.DoesNotMatch(@"IFCRELCONTAINEDINSPATIALSTRUCTURE\([^;]*,\(\),#\d+\);", text);
        Assert.Contains("IFCRELCONTAINEDINSPATIALSTRUCTURE(", text);
    }

    // ─── P9: a part of a day's duration ─────────────────────────────────────

    [Fact]
    public void A_fractional_duration_comes_back_as_it_went()
    {
        Assert.Equal("'P2DT4H'", IfcExport.IfcDuration(2.5));
        Assert.Equal("'P2DT5H'", IfcExport.IfcDuration(2.5, 10));
        Assert.Equal("'-P1DT2H'", IfcExport.IfcDuration(-1.25));
        Assert.Equal("'P3D'", IfcExport.IfcDuration(2.99999999));
        foreach (var hours in new[] { 8.0, 10.0, 7.5 })
            foreach (var days in new[] { 0.0, 0.5, 2.5, 3.125, 10.75 })
                Assert.Equal(days, IfcImport.DurationDays(IfcExport.IfcDuration(days, hours).Trim('\''), hours)!.Value, 1e-4);
    }

    // ─── P11: a task's date as written ──────────────────────────────────────

    [Theory]
    [InlineData("2026-11-16T23:30:00-11:00", "2026-11-16T23:30:00")]
    [InlineData("2026-11-16T00:30:00+13:00", "2026-11-16T00:30:00")]
    [InlineData("2026-11-16T08:00:00Z", "2026-11-16T08:00:00")]
    [InlineData("2026-11-16T08:00:00", "2026-11-16T08:00:00")]
    [InlineData("2026-11-16", "2026-11-16T00:00:00")]
    public void A_task_date_is_read_as_written_whatever_its_offset(string text, string expected)
    {
        var d = IfcImport.IfcWhen(text)!.Value;
        Assert.Equal(expected, d.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(DateOnly.Parse(expected[..10], System.Globalization.CultureInfo.InvariantCulture), DateOnly.FromDateTime(d));
    }

    // ─── P15: rare STEP forms ────────────────────────────────────────────────

    [Fact]
    public void A_typed_value_with_nothing_in_it_reads_as_no_value()
    {
        var f = StepFile.Parse(File4(Mm, "#3", "#90=IFCPROPERTYSINGLEVALUE('Empty',$,IFCLABEL( ),$);"));
        var p = f.Entities[90];
        Assert.Equal(4, p.Args.Count);
        Assert.Null(p[2]);
    }

    [Fact]
    public void A_complex_instance_is_skipped_and_told_and_the_rest_imports()
    {
        var text = File4(Mm, "#3,#91", """
#91=(IFCNAMEDUNIT(*,.AREAUNIT.)IFCSIUNIT($,.SQUARE_METRE.) );
#92=( IFCREPRESENTATIONITEM()IFCGEOMETRICREPRESENTATIONITEM() IFCSTYLEDITEM(#22,(),'x'));
""");
        var file = StepFile.Parse(text);
        Assert.Equal(2, file.ComplexCount);
        Assert.Equal(StepFile.Complex, file.Entities[91].Type);
        Assert.Empty(file.Entities[91].Args);
        var f = new Fixture(new ArchitectureModule());
        var r = IfcImport.Run(f.Session, Write("complex.ifc", text), null, out _);
        Assert.Equal(1, r.Elements + r.References);
        Assert.Contains(r.Notes, n => n.StartsWith("2 complex STEP instance(s)", StringComparison.Ordinal));
    }

    // ─── D5: one decoder of STEP strings ────────────────────────────────────

    [Theory]
    [InlineData(@"Caf\X\E9", "Café")]
    [InlineData(@"\X2\0103\X0\ a", "ă a")]
    [InlineData(@"\X4\0001F600\X0\!", "😀!")]
    [InlineData(@"\S\i", "é")]
    [InlineData(@"\PA\x", "x")]
    [InlineData(@"a\\b", @"a\b")]
    [InlineData(@"open \X2\0103 rest", @"open \X2\0103 rest")]
    public void STEP_strings_are_decoded_one_way(string raw, string text)
    {
        Assert.Equal(text, StepText.Decode(raw));
        Assert.Equal(text, StepFile.Decode(raw));
    }
}
