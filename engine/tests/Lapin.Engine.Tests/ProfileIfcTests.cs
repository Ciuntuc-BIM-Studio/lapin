using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Platform.ProfileLibraries;
using Xunit;

namespace Lapin.Engine.Tests;

/// <summary>Profile libraries as IFC (docs/plan-profile-libraries.md): every kind written and read back, IFC2x3's properties, other exporters' units and axes, unnamed profiles named by their element.</summary>
public class ProfileIfcTests
{
    static IfcProfilesRead Read(byte[] bytes, string source = "lib.ifc") => new IfcModel(StepFile.Read(bytes)).ReadProfiles("steel", source);
    static IfcProfilesRead Read(string step) => Read(System.Text.Encoding.UTF8.GetBytes(step));

    static string File(string schema, string units, string data) => $"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION((''),'2;1');
        FILE_NAME('t.ifc','2026-01-01T00:00:00',(''),(''),'','','');
        FILE_SCHEMA(('{schema}'));
        ENDSEC;
        DATA;
        #1=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
        #2=IFCUNITASSIGNMENT(({units}));
        #3=IFCPROJECT('0abcdefghijklmnopqrstu',$,'P',$,$,$,$,$,#2);
        {data}
        ENDSEC;
        END-ISO-10303-21;
        """;

    static readonly SectionProfile[] EveryKind =
    [
        BuiltInProfiles.Find("IPE 300")!,
        BuiltInProfiles.Find("UPN 200")!,
        BuiltInProfiles.Find("L 120×80×10")!,
        BuiltInProfiles.Find("RHS 150×100×6")!,
        BuiltInProfiles.Find("CHS 114.3×5")!,
        BuiltInProfiles.Find("C 200×65×2")!,
        BuiltInProfiles.Find("Z 200×65×2")!,
        BuiltInProfiles.Find("Σ 200×62×2")!,
        new SectionProfile { Name = "T 100", Kind = "T", H = 0.1, B = 0.1, Tw = 0.011, Tf = 0.011, R1 = 0.011 },
        new SectionProfile { Name = "Welded", Kind = "asymI", H = 0.5, B = 0.2, Tf = 0.02, B2 = 0.3, Tf2 = 0.03, Tw = 0.012 },
        new SectionProfile { Name = "GL24h 120×360", Kind = "rectangle", H = 0.36, B = 0.12, Material = "timber", Family = "Glulam", Category = "glulam" },
        new SectionProfile { Name = "Ø40", Kind = "circle", H = 0.04, B = 0.04 },
        new SectionProfile { Name = "Ellipse 200×100", Kind = "ellipse", H = 0.1, B = 0.2 },
        ProjectProfiles.FromOutlines([[new(0, 0), new(0.1, 0), new(0.1, 0.06), new(0, 0.06)], [new(0.005, 0.005), new(0.005, 0.055), new(0.095, 0.055), new(0.095, 0.005)]], "Alu tube", "aluminium", null),
    ];

    [Fact]
    public void Every_kind_comes_back_as_it_was_written()
    {
        var back = Read(ProfileIfc.Write(new ProfileLibrary("Firm", null, "steel", EveryKind)));
        Assert.Empty(back.Checks);
        Assert.Equal(EveryKind.Length, back.Profiles.Count);
        foreach (var p in EveryKind)
        {
            var b = back.Profiles.Single(x => x.Name == p.Name);
            Assert.Equal(p.Kind, b.Kind);
            Assert.Equal(p.H, b.H, 6);
            Assert.Equal(p.B, b.B, 6);
            if (p.Kind != "outline")
            {
                Assert.Equal(p.Tw, b.Tw, 6);
                Assert.Equal(p.Tf, b.Tf, 6);
                Assert.Equal(p.R1, b.R1, 6);
                Assert.Equal(p.Lip, b.Lip, 6);
                Assert.Equal(p.Slope, b.Slope, 6);
                Assert.Equal(p.B2, b.B2, 6);
                Assert.Equal(p.Tf2, b.Tf2, 6);
            }
            Assert.Equal(p.Material, b.Material);
            Assert.Equal(p.Family ?? b.Family, b.Family);
            Assert.Equal(p.Standard, b.Standard);
            // What was written as its values reads back as the same values, in SI.
            var c = ProfileProps.Compute(p).Values;
            Assert.Equal(c["A"], b.Given["A"], 7);
            Assert.Equal(c["Iy"], b.Given["Iy"], 11);
            Assert.Equal(c["Iz"], b.Given["Iz"], 11);
            if (c.TryGetValue("Wply", out var wp)) Assert.Equal(wp, b.Given["Wply"], 9);
        }
        var alu = back.Profiles.Single(x => x.Name == "Alu tube");
        Assert.Equal(2, alu.Rings!.Count);
        Assert.Single(alu.Rings, r => Polygon2.SignedArea(r) < 0);
        Assert.Equal(0.1 * 0.06 - 0.09 * 0.05, ProfileProps.Compute(alu).Values["A"], 9);
    }

    [Fact]
    public void A_compound_outline_writes_a_composite_and_reads_both_parts()
    {
        var two = ProjectProfiles.FromOutlines([[new(0, 0), new(0.05, 0), new(0.05, 0.1), new(0, 0.1)], [new(0.06, 0), new(0.11, 0), new(0.11, 0.1), new(0.06, 0.1)]], "2 flats", "steel", null);
        var bytes = ProfileIfc.Write(new ProfileLibrary("F", null, "steel", [two]));
        Assert.Contains("IFCCOMPOSITEPROFILEDEF", System.Text.Encoding.UTF8.GetString(bytes));
        var b = Assert.Single(Read(bytes).Profiles);
        Assert.Equal(2, b.Rings!.Count);
        Assert.Equal(2 * 0.05 * 0.1, ProfileProps.Compute(b).Values["A"], 9);
    }

    [Fact]
    public void IFC2x3_steel_properties_are_read_in_the_project_s_units_and_axes_paired_by_the_geometry()
    {
        // An IPE 300 as an exporter in millimetres writes it: Y the weak axis here, the values in mm² and mm⁴ with no unit assigned.
        var read = Read(File("IFC2X3", "#1", """
            #10=IFCCARTESIANPOINT((0.,0.));
            #11=IFCAXIS2PLACEMENT2D(#10,$);
            #12=IFCISHAPEPROFILEDEF(.AREA.,'IPE300',#11,150.,300.,7.1,10.7,15.);
            #13=IFCSTRUCTURALSTEELPROFILEPROPERTIES('IPE300',#12,42.2,$,$,$,5381.,201200.,$,6038000.,83560000.,1.259E+11,$,$,$,$,$,80500.,$,557100.,$,$,$,$,$,1.555,1.128);
            """));
        var p = Assert.Single(read.Profiles);
        Assert.Equal("I", p.Kind);
        Assert.Equal("IPE", p.Family);
        Assert.Equal(53.81e-4, p.Given["A"], 9);
        Assert.Equal(8356e-8, p.Given["Iy"], 10);
        Assert.Equal(603.8e-8, p.Given["Iz"], 10);
        Assert.Equal(20.12e-8, p.Given["It"], 10);
        Assert.Equal(125.9e-9, p.Given["Iw"], 12);
        Assert.Equal(42.2, p.Given["mass"], 9);
        Assert.Equal(557.1e-6, p.Given["Wely"], 9);
        Assert.Equal(80.5e-6, p.Given["Welz"], 9);
        Assert.Equal(557.1e-6 * 1.128, p.Given["Wply"], 7);
        Assert.Empty(read.Checks);
    }

    [Fact]
    public void Values_in_SI_in_a_file_in_millimetres_are_found_so_and_a_wrong_one_is_flagged()
    {
        // Lengths in mm, the values in m² and m⁴ (no unit of their own): the geometry says so. The second profile's area is wrong.
        var read = Read(File("IFC4", "#1", """
            #12=IFCISHAPEPROFILEDEF(.AREA.,'IPE300',$,150.,300.,7.1,10.7,15.,$,$);
            #13=IFCPROPERTYSINGLEVALUE('CrossSectionArea',$,IFCAREAMEASURE(0.005381),$);
            #14=IFCPROPERTYSINGLEVALUE('MomentOfInertiaY',$,IFCMOMENTOFINERTIAMEASURE(8.356E-5),$);
            #15=IFCPROFILEPROPERTIES('Pset_ProfileMechanical',$,(#13,#14),#12);
            #22=IFCISHAPEPROFILEDEF(.AREA.,'IPE300B',$,150.,300.,7.1,10.7,15.,$,$);
            #23=IFCPROPERTYSINGLEVALUE('CrossSectionArea',$,IFCAREAMEASURE(0.01),$);
            #25=IFCPROFILEPROPERTIES('Pset_ProfileMechanical',$,(#23),#22);
            """));
        Assert.Equal(53.81e-4, read.Profiles[0].Given["A"], 9);
        Assert.Equal(8356e-8, read.Profiles[0].Given["Iy"], 10);
        var flagged = Assert.Single(read.Checks);
        Assert.Equal(("IPE300B", "A"), (flagged.Profile, flagged.Key));
    }

    [Fact]
    public void A_property_s_own_unit_is_its_unit()
    {
        var read = Read(File("IFC4", "#1", """
            #5=IFCSIUNIT(*,.LENGTHUNIT.,.CENTI.,.METRE.);
            #6=IFCDERIVEDUNITELEMENT(#5,4);
            #7=IFCDERIVEDUNIT((#6),.MOMENTOFINERTIAUNIT.,$);
            #12=IFCISHAPEPROFILEDEF(.AREA.,'IPE300',$,150.,300.,7.1,10.7,15.,$,$);
            #14=IFCPROPERTYSINGLEVALUE('MomentOfInertiaZ',$,IFCMOMENTOFINERTIAMEASURE(603.8),#7);
            #15=IFCPROFILEPROPERTIES('Pset_ProfileMechanical',$,(#14),#12);
            """));
        Assert.Equal(603.8e-8, read.Profiles[0].Given["Iz"], 10);
    }

    [Fact]
    public void Profiles_used_by_elements_are_named_by_them_once_and_placed_ones_become_outlines()
    {
        var read = Read(File("IFC4", "#1", """
            #10=IFCCARTESIANPOINT((0.,0.));
            #20=IFCRECTANGLEPROFILEDEF(.AREA.,$,$,100.,200.);
            #21=IFCEXTRUDEDAREASOLID(#20,$,$,1000.);
            #22=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#21));
            #23=IFCPRODUCTDEFINITIONSHAPE($,$,(#22));
            #24=IFCBEAM('1abcdefghijklmnopqrstu',$,'Beam 1',$,'Purlin 100x200',$,#23,$,$);
            #25=IFCRECTANGLEPROFILEDEF(.AREA.,$,$,100.,200.);
            #26=IFCEXTRUDEDAREASOLID(#25,$,$,2000.);
            #27=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#26));
            #28=IFCPRODUCTDEFINITIONSHAPE($,$,(#27));
            #29=IFCBEAM('2abcdefghijklmnopqrstu',$,'Beam 2',$,'Purlin 100x200',$,#28,$,$);
            #30=IFCCARTESIANPOINT((50.,0.));
            #31=IFCAXIS2PLACEMENT2D(#30,$);
            #32=IFCLSHAPEPROFILEDEF(.AREA.,'Offset L',#31,100.,100.,10.,$,$,$);
            #33=IFCMATERIAL('GL24h',$,$);
            #34=IFCRECTANGLEPROFILEDEF(.AREA.,$,$,120.,360.);
            #35=IFCMATERIALPROFILE('GL 120x360',$,#33,#34,$,$);
            #36=IFCCENTERLINEPROFILEDEF(.AREA.,'Line',#10,10.);
            """));
        Assert.Equal(["Purlin 100x200", "Offset L", "GL 120x360"], read.Profiles.Select(p => p.Name).OrderBy(n => n == "Purlin 100x200" ? 0 : n == "Offset L" ? 1 : 2).ToArray());
        var purlin = read.Profiles.Single(p => p.Name == "Purlin 100x200");
        Assert.Equal("rectangle", purlin.Kind);
        Assert.Equal(0.2, purlin.H, 9);
        Assert.Equal("outline", read.Profiles.Single(p => p.Name == "Offset L").Kind);
        Assert.Equal("timber", read.Profiles.Single(p => p.Name == "GL 120x360").Material);
        Assert.Contains(read.Notes, n => n.Contains("centre-line"));
    }

    [Fact]
    public void Asymmetric_C_and_Z_read_from_another_program_have_their_shapes()
    {
        var read = Read(File("IFC4", "#1", """
            #20=IFCASYMMETRICISHAPEPROFILEDEF(.AREA.,'A',$,300.,500.,12.,30.,$,200.,20.,$,$,$,$,$);
            #21=IFCCSHAPEPROFILEDEF(.AREA.,'C',$,200.,65.,2.,20.,$);
            #22=IFCZSHAPEPROFILEDEF(.AREA.,'Z',$,200.,66.,2.,2.,$,$);
            #23=IFCZSHAPEPROFILEDEF(.AREA.,'Zhot',$,200.,80.,10.,12.,$,$);
            """));
        var a = read.Profiles.Single(p => p.Name == "A");
        Assert.Equal(("asymI", 0.2, 0.3, 0.02, 0.03), (a.Kind, a.B, a.B2, a.Tf, a.Tf2));
        Assert.Equal(0.3 * 0.03 + 0.2 * 0.02 + 0.45 * 0.012, ProfileProps.Compute(a).Values["A"], 9);
        Assert.Equal(("C", 0.02), (read.Profiles.Single(p => p.Name == "C").Kind, read.Profiles.Single(p => p.Name == "C").Lip));
        var z = read.Profiles.Single(p => p.Name == "Z");
        Assert.Equal(("Z", 0.13), (z.Kind, Math.Round(z.B, 6)));
        var hot = read.Profiles.Single(p => p.Name == "Zhot");
        Assert.Equal("outline", hot.Kind);
        Assert.Equal(2 * 0.08 * 0.012 + (0.2 - 2 * 0.012) * 0.01, ProfileProps.Compute(hot).Values["A"], 9);
    }
}

/// <summary>A real library, on request (`LAPIN_PROFILE_IFC=<file[;file]>`; none is in the repository): how many profiles of each kind, and the built-in library's sizes against those of the same name.</summary>
public class ProfileIfcBenchmarkTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public void A_real_library_reads()
    {
        if (Environment.GetEnvironmentVariable("LAPIN_PROFILE_IFC") is not { Length: > 0 } files) return;
        foreach (var file in files.Split(';'))
        {
            var t = System.Diagnostics.Stopwatch.StartNew();
            var read = new IfcModel(StepFile.Read(file)).ReadProfiles("steel", Path.GetFileName(file));
            output.WriteLine($"{Path.GetFileName(file)}: {read.Profiles.Count} profiles in {t.ElapsedMilliseconds} ms, {read.Skipped} skipped, {read.Checks.Count} checks; {string.Join(", ", read.Profiles.GroupBy(p => p.Kind).Select(g => $"{g.Key} {g.Count()}"))}");
            foreach (var n in read.Notes) output.WriteLine("  " + n);
            foreach (var (name, g) in read.Profiles.GroupBy(p => p.Family ?? "").OrderBy(g => g.Key).Select(g => (g.Key, g.Count()))) output.WriteLine($"  family {name}: {g}");
            var compared = 0;
            foreach (var p in read.Profiles)
            {
                if (BuiltInProfiles.Find(p.Name) is not { } b || b.Kind != p.Kind) continue;
                compared++;
                static bool Off(double a, double c) => Math.Abs(a - c) > 1e-5;
                if (Off(p.H, b.H) || Off(p.B, b.B) || Off(p.Tw, b.Tw) || Off(p.Tf, b.Tf) || Off(p.R1, b.R1))
                    output.WriteLine($"  DIFF {p.Name}: file h {p.H * 1000} b {p.B * 1000} tw {p.Tw * 1000} tf {p.Tf * 1000} r {p.R1 * 1000} / built-in h {b.H * 1000} b {b.B * 1000} tw {b.Tw * 1000} tf {b.Tf * 1000} r {b.R1 * 1000}");
            }
            output.WriteLine($"  compared with the built-in library: {compared}");
        }
    }
}
