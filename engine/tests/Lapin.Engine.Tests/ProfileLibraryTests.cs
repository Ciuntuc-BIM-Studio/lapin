using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Platform.ProfileLibraries;
using Lapin.Steel;
using Lapin.Structure;
using Xunit;

namespace Lapin.Engine.Tests;

/// <summary>Profile libraries (docs/plan-profile-libraries.md): the computed properties against the published tables, the table import, the project's copies.</summary>
public class ProfileLibraryTests
{
    static Fixture F() => new(new StructureModule(), new SteelModule());

    static void Near(double expected, double actual, double rel, string what) =>
        Assert.True(Math.Abs(actual - expected) <= Math.Abs(expected) * rel, $"{what}: {actual:G5} is not within {rel:P0} of {expected:G5}");

    static IReadOnlyDictionary<string, double> Props(string name) => ProfileProps.Compute(BuiltInProfiles.Find(name) ?? throw new Exception($"no {name}")).Values;

    [Fact]
    public void The_built_in_I_sections_match_the_published_tables()
    {
        // EN 10365 / the producers' tables: A cm², Iy, Iz, It cm⁴, Wel, Wpl cm³, Iw ×10³ cm⁶, G kg/m, AL m²/m.
        var ipe = Props("IPE 300");
        Near(53.81, ipe["A"] * 1e4, 0.01, "IPE 300 A");
        Near(8356, ipe["Iy"] * 1e8, 0.01, "IPE 300 Iy");
        Near(557.1, ipe["Wely"] * 1e6, 0.01, "IPE 300 Wel.y");
        Near(628.4, ipe["Wply"] * 1e6, 0.01, "IPE 300 Wpl.y");
        Near(603.8, ipe["Iz"] * 1e8, 0.01, "IPE 300 Iz");
        Near(80.50, ipe["Welz"] * 1e6, 0.01, "IPE 300 Wel.z");
        Near(125.2, ipe["Wplz"] * 1e6, 0.01, "IPE 300 Wpl.z");
        Near(12.46, ipe["iy"] * 100, 0.01, "IPE 300 iy");
        Near(20.12, ipe["It"] * 1e8, 0.03, "IPE 300 It");
        Near(125.9, ipe["Iw"] * 1e9, 0.02, "IPE 300 Iw");
        Near(42.2, ipe["mass"], 0.01, "IPE 300 G");
        Near(1.160, ipe["paint"], 0.01, "IPE 300 AL");

        var hea = Props("HEA 200");
        Near(53.83, hea["A"] * 1e4, 0.01, "HEA 200 A");
        Near(3692, hea["Iy"] * 1e8, 0.01, "HEA 200 Iy");
        Near(1336, hea["Iz"] * 1e8, 0.01, "HEA 200 Iz");
        Near(429.5, hea["Wply"] * 1e6, 0.01, "HEA 200 Wpl.y");
        Near(20.98, hea["It"] * 1e8, 0.03, "HEA 200 It");
        Near(108.0, hea["Iw"] * 1e9, 0.02, "HEA 200 Iw");
        Near(1.136, hea["paint"], 0.01, "HEA 200 AL");

        var heb = Props("HEB 300");
        Near(149.1, heb["A"] * 1e4, 0.01, "HEB 300 A");
        Near(25170, heb["Iy"] * 1e8, 0.01, "HEB 300 Iy");
        Near(8563, heb["Iz"] * 1e8, 0.01, "HEB 300 Iz");
        Near(1869, heb["Wply"] * 1e6, 0.01, "HEB 300 Wpl.y");
        Near(185.0, heb["It"] * 1e8, 0.03, "HEB 300 It");
        Near(1688, heb["Iw"] * 1e9, 0.02, "HEB 300 Iw");
        Near(117.0, heb["mass"], 0.01, "HEB 300 G");

        var hem = Props("HEM 200");
        Near(131.3, hem["A"] * 1e4, 0.01, "HEM 200 A");
        Near(10640, hem["Iy"] * 1e8, 0.01, "HEM 200 Iy");
        Near(3651, hem["Iz"] * 1e8, 0.01, "HEM 200 Iz");
    }

    [Fact]
    public void Channels_angles_and_hollow_sections_match_the_published_tables()
    {
        // UPN 200, its flanges sloping 8 %: A 32.2 cm², Iy 1910, Iz 148 cm⁴, Wel.y 191 cm³, It 11.9 cm⁴, G 25.3 kg/m; Iw 9.07×10³ cm⁶
        // (the thin-walled formula ignores the slope and the fillets: within 20 %).
        var upn = Props("UPN 200");
        Near(32.2, upn["A"] * 1e4, 0.015, "UPN 200 A");
        Near(1910, upn["Iy"] * 1e8, 0.015, "UPN 200 Iy");
        Near(148, upn["Iz"] * 1e8, 0.03, "UPN 200 Iz");
        Near(191, upn["Wely"] * 1e6, 0.015, "UPN 200 Wel.y");
        Near(11.9, upn["It"] * 1e8, 0.10, "UPN 200 It");
        Near(9.07, upn["Iw"] * 1e9, 0.20, "UPN 200 Iw");
        // L 100×100×10: A 19.2 cm², Iy = Iz 177, Iu 280, Iv 73.0 cm⁴, G 15.0 kg/m.
        var l = Props("L 100×100×10");
        Near(19.2, l["A"] * 1e4, 0.01, "L A");
        Near(177, l["Iy"] * 1e8, 0.015, "L Iy");
        Near(177, l["Iz"] * 1e8, 0.015, "L Iz");
        Near(280, l["Iu"] * 1e8, 0.015, "L Iu");
        Near(73.0, l["Iv"] * 1e8, 0.02, "L Iv");
        Near(15.0, l["mass"], 0.015, "L G");
        Assert.Equal(45, Math.Abs(l["alpha"]), 6);
        Assert.Equal(0, l["Iw"]);
        // SHS 100×100×5 hot finished (EN 10210-2): A 18.7 cm², I 279 cm⁴, Wpl 66.4 cm³, G 14.7 kg/m.
        var shs = Props("SHS 100×100×4");
        Assert.True(shs["A"] > 0);
        var rhs = Props("RHS 100×100×5");
        Near(18.7, rhs["A"] * 1e4, 0.015, "SHS 100×100×5 A");
        Near(279, rhs["Iy"] * 1e8, 0.02, "SHS 100×100×5 I");
        Near(66.4, rhs["Wply"] * 1e6, 0.02, "SHS 100×100×5 Wpl");
        Near(14.7, rhs["mass"], 0.015, "SHS 100×100×5 G");
        // CHS 168.3×6.3: A 32.1 cm², I 1053 cm⁴, Wpl 165 cm³, It 2107 cm⁴, G 25.2 kg/m.
        var chs = Props("CHS 168.3×6.3");
        Near(32.1, chs["A"] * 1e4, 0.01, "CHS A");
        Near(1053, chs["Iy"] * 1e8, 0.01, "CHS I");
        Near(165, chs["Wply"] * 1e6, 0.01, "CHS Wpl");
        Near(2107, chs["It"] * 1e8, 0.01, "CHS It");
        Near(25.2, chs["mass"], 0.01, "CHS G");
    }

    [Fact]
    public void The_sectorial_warping_constant_of_a_thin_channel_is_its_closed_form()
    {
        // A channel of one thickness along its centreline, b × h: Iw = t·b³·h²/12·(3b + 2h)/(6b + h).
        double b = 0.08, h = 0.2, t = 0.004;
        var line = new List<Vec2> { new(b, -h / 2), new(0, -h / 2), new(0, h / 2), new(b, h / 2) };
        var expected = t * b * b * b * h * h / 12 * (3 * b + 2 * h) / (6 * b + h);
        Near(expected, Lapin.Platform.Analysis.SectionProperties.SectorialWarping(line, t), 1e-6, "channel Iw");
        // Its own section's: a cold-formed C has one, a Z too; the sharp-cornered Σ more than a C its size.
        var c = Props("C 200×65×2");
        var z = Props("Z 200×65×2");
        Assert.True(c["Iw"] > 0 && z["Iw"] > 0);
    }

    [Fact]
    public void The_built_in_library_keeps_the_catalogue_s_names_and_sizes()
    {
        foreach (var (name, s) in Section.Catalogue)
        {
            var p = BuiltInProfiles.Find(name);
            Assert.True(p != null, $"{name} is in the built-in library");
            Assert.True(ProjectProfiles.Same(p!, s.ToJson()), $"{name} has the catalogue's size");
        }
        // Any built-in profile by its name, as the catalogue's are.
        var heb = Section.From(JsonValue.Create("HEB 240"))!;
        Assert.Equal("I", heb.Kind);
        Assert.Equal(0.24, heb.Depth, 9);
        Assert.Equal(0.021, heb.Root, 9);
        Assert.Equal("EN 10365:2017", heb.Standard);
        Assert.Null(Section.From(JsonValue.Create("W14X90")));
    }

    static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void An_EN_table_is_read_with_its_units_and_its_values_checked()
    {
        // A producer's table: German headings, the radius of gyration small, a mass and an Iw ×10³ cm⁶.
        var csv = "Bezeichnung;h [mm];b [mm];s [mm];t [mm];r [mm];A [cm²];G [kg/m];Iy [cm4];iy [cm];Iz [cm4];It [cm4];Iw [x10³ cm⁶]\n" +
                  "IPE 300;300;150;7,1;10,7;15;53,81;42,2;8356;12,46;603,8;20,12;125,9\n" +
                  "HEA 200;190;200;6,5;10;18;53,83;42,3;3692;8,28;1336;20,98;108\n" +
                  "IPE 999;300;150;7,1;10,7;15;99;42,2;8356;12,46;603,8;20,12;125,9\n";
        var table = Lapin.Platform.Tables.Read(Utf8(csv));
        var g = ProfileTables.Guess(table);
        Assert.Equal(0, g.Header);
        Assert.Equal("en", g.Convention);
        Assert.Equal(3, g.Map["tw"]!.Index);
        Assert.Equal(4, g.Map["t"]!.Index);
        Assert.Equal(8, g.Map["Iy"]!.Index);
        Assert.Equal(9, g.Map["iy"]!.Index);
        Assert.Equal(1e-9, g.Map["Iw"]!.Factor, 15);
        var res = ProfileTables.Import(table, g.Header, g.Map, null, null, "EN 10365", "steel", "test.csv");
        Assert.Equal(3, res.Profiles.Count);
        var ipe = res.Profiles[0];
        Assert.Equal("I", ipe.Kind);
        Assert.Equal("IPE", ipe.Family);
        Assert.Equal(0.0107, ipe.Tf, 9);
        Assert.Equal(53.81e-4, ipe.Given["A"], 12);
        Assert.Equal(125.9e-9, ipe.Given["Iw"], 15);
        // The table's values agree with Lapin's, but the made-up row's area is flagged.
        Assert.DoesNotContain(res.Checks, c => c.Profile == "IPE 300");
        Assert.Contains(res.Checks, c => c.Profile == "IPE 999" && c.Key == "A");
    }

    [Fact]
    public void An_American_table_reads_x_as_the_strong_axis_in_inches()
    {
        // As the AISC Shapes Database writes it: Type, label, d, bf, tw, tf, Ht, B, tdes, OD, A, Ix, Zx, Sx, Iy, J, Cw, W.
        var csv = "Type,AISC_Manual_Label,W,A,d,Ht,OD,bf,B,tw,tf,tdes,Ix,Zx,Sx,Iy,J,Cw\n" +
                  "W,W14X90,90,26.5,14,–,–,14.5,–,0.44,0.71,–,999,157,143,362,4.06,16000\n" +
                  "HSS,HSS6X6X1/2,35.1,9.74,–,6,–,–,6,–,–,0.465,–,–,–,–,–,–\n" +
                  "PIPE,Pipe6STD,19,5.2,–,–,6.63,–,–,–,–,0.261,28.1,–,–,28.1,56.3,–\n";
        var table = Lapin.Platform.Tables.Read(Utf8(csv));
        var g = ProfileTables.Guess(table);
        Assert.Equal("american", g.Convention);
        Assert.Equal("imperial", g.System);
        Assert.Equal(12, g.Map["Iy"]!.Index);
        Assert.Equal(15, g.Map["Iz"]!.Index);
        Assert.Equal(13, g.Map["Wply"]!.Index);
        var res = ProfileTables.Import(table, g.Header, g.Map, null, null, "ASTM A6", "steel", "aisc.csv");
        Assert.Equal(3, res.Profiles.Count);
        var w = res.Profiles[0];
        Assert.Equal("I", w.Kind);
        Assert.Equal(14 * 0.0254, w.H, 9);
        Assert.Equal(999 * Math.Pow(0.0254, 4), w.Given["Iy"], 12);
        Assert.Equal(90 * 0.45359237 / 0.3048, w.Given["mass"], 9);
        var hss = res.Profiles[1];
        Assert.Equal("RHS", hss.Kind);
        Assert.Equal(6 * 0.0254, hss.H, 9);
        Assert.Equal(0.465 * 0.0254, hss.Tw, 9);
        var pipe = res.Profiles[2];
        Assert.Equal("CHS", pipe.Kind);
        Assert.Equal(6.63 * 0.0254, pipe.B, 9);
        // The W's Ix was typed wrong (999 for 999 in⁴, the table's 999 against Lapin's ~1000): within its tolerance, no check; Zx too.
        Assert.DoesNotContain(res.Checks, c => c.Profile == "W14X90" && c.Key == "Iy");
    }

    [Fact]
    public void A_library_profile_is_copied_into_the_project_once_and_its_types_follow_an_update()
    {
        var f = F();
        var p = BuiltInProfiles.Find("HEB 240")!;
        var type = (string)f.Run("steel.createType", new JsonObject { ["profile"] = p.ToJson(), ["library"] = BuiltInProfiles.Name })!["id"]!;
        var beam = (string)f.Run("structure.createBeamType", new JsonObject { ["profile"] = p.ToJson(), ["library"] = BuiltInProfiles.Name })!["id"]!;
        var copies = f.Model.OfClass(ProjectProfiles.Class).ToList();
        Assert.Single(copies);
        Assert.Equal(copies[0].Id, (string?)f.Model.Get(type)!.Attrs["section"]!["profileId"]);
        Assert.Equal(copies[0].Id, (string?)f.Model.Get(beam)!.Attrs["section"]!["profileId"]);
        Assert.Equal("Steel", f.Model.Get((string)f.Model.Get(type)!.Attrs["material"]!)!.Name);
        // The copy says where it came from; the type's section is the library's.
        Assert.Equal(BuiltInProfiles.Name, copies[0].Str("library"));
        var s = Section.From(f.Model.Get(type)!.Attrs["section"])!;
        Assert.Equal(0.24, s.Width, 9);
        // A member of it, then the library's newer profile (a thicker web): the copy and both types take it, one undo step.
        var m = (string)f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0, ["typeId"] = type })!["id"]!;
        var before = f.Bounds(m);
        var newer = p with { Tw = 0.012, H = 0.25 };
        var r = f.Run("profiles.update", new JsonObject { ["id"] = copies[0].Id, ["profile"] = newer.ToJson() })!;
        Assert.Equal(2, (int)r["types"]!);
        Assert.Equal(0.25, Section.From(f.Model.Get(beam)!.Attrs["section"])!.Depth, 9);
        Assert.True(f.Bounds(m).Max.Z - f.Bounds(m).Min.Z > before.Max.Z - before.Min.Z + 0.005);
        f.Session.Undo();
        Assert.Equal(0.24, Section.From(f.Model.Get(beam)!.Attrs["section"])!.Depth, 9);
        // Used again from the same library, the copy is the same (and says it differs from what was sent).
        var again = f.Run("profiles.use", new JsonObject { ["profile"] = newer.ToJson(), ["library"] = BuiltInProfiles.Name })!;
        Assert.Equal(copies[0].Id, (string?)again["id"]);
        Assert.True((bool)again["differs"]!);
        // A copy in use cannot be removed.
        Assert.ThrowsAny<Exception>(() => f.Run("profiles.remove", new JsonObject { ["id"] = copies[0].Id }));
    }

    [Fact]
    public void An_outline_profile_makes_a_hollow_beam_and_its_properties()
    {
        // A 100 × 60 mm aluminium tube drawn as two outlines: the outer and a hole.
        var outer = new List<Vec2> { new(0, 0), new(0.1, 0), new(0.1, 0.06), new(0, 0.06) };
        var hole = new List<Vec2> { new(0.005, 0.005), new(0.005, 0.055), new(0.095, 0.055), new(0.095, 0.005) };
        var p = ProjectProfiles.FromOutlines([outer, hole], "Alu 100×60×5", "aluminium", "test");
        Assert.Equal("outline", p.Kind);
        Assert.Equal(2, p.Rings!.Count);
        var v = ProfileProps.Compute(p).Values;
        Near(0.1 * 0.06 - 0.09 * 0.05, v["A"], 1e-9, "area");
        Near((0.06 * Math.Pow(0.1, 3) - 0.05 * Math.Pow(0.09, 3)) / 12, v["Iz"], 1e-6, "Iz");
        Near(v["A"] * 2700, v["mass"], 1e-9, "mass");
        Assert.False(v.ContainsKey("It"));
        var f = F();
        var type = (string)f.Run("structure.createBeamType", new JsonObject { ["profile"] = p.ToJson(), ["library"] = "Firm" })!["id"]!;
        Assert.Equal("Aluminium", f.Model.Get((string)f.Model.Get(type)!.Attrs["material"]!)!.Name);
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 2, ["y2"] = 0, ["typeId"] = type })!["id"]!;
        var b = f.Bounds(beam);
        Assert.Equal(0.06, b.Max.Z - b.Min.Z, 6);
        Assert.Equal(0.1, b.Max.Y - b.Min.Y, 6);
    }

    [Fact]
    public void A_library_file_reads_back_as_it_was_written()
    {
        var lib = new ProfileLibrary("Firm", "test", "steel", [BuiltInProfiles.Find("IPE 300")!, BuiltInProfiles.Find("UPN 200")!, BuiltInProfiles.Find("L 120×80×10")!]);
        var back = ProfileLibrary.From(JsonNode.Parse(lib.ToJson().ToJsonString()))!;
        Assert.Equal(3, back.Profiles.Count);
        Assert.True(JsonNode.DeepEquals(lib.ToJson(), back.ToJson()));
        // As CSV and back: the same kinds and sizes.
        var table = Lapin.Platform.Tables.Read(Utf8(ProfileTables.Csv(lib.Profiles)));
        var g = ProfileTables.Guess(table);
        var res = ProfileTables.Import(table, g.Header, g.Map, null, null, null, "steel", null);
        foreach (var (a, b) in lib.Profiles.Zip(res.Profiles))
        {
            Assert.Equal(a.Name, b.Name);
            Assert.Equal(a.Kind, b.Kind);
            Assert.True(ProjectProfiles.Same(a, b.SectionJson(null)), a.Name);
            Assert.Equal(a.Slope, b.Slope, 9);
        }
        Assert.Empty(res.Checks);
    }
}
