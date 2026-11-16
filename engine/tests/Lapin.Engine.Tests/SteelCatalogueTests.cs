using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// One steel catalogue (audit 15.11.2026, D1): a profile typed „IPE 300” or „IPE300” is the built-in library's EN section,
/// its root radius and an UPN's sloping flanges included; a section an earlier build stored from its own catalogue (the four
/// nominal sizes only) is the library's when the project opens.
/// </summary>
public class SteelCatalogueTests
{
    /// <summary>The old catalogue's names and nominal sizes (mm: depth, width, web, flange, lip), as it held them.</summary>
    public static readonly TheoryData<string, string, double, double, double, double, double> Old = new()
    {
        { "IPE 200", "I", 200, 100, 5.6, 8.5, 0 }, { "IPE 240", "I", 240, 120, 6.2, 9.8, 0 }, { "IPE 300", "I", 300, 150, 7.1, 10.7, 0 },
        { "IPE 330", "I", 330, 160, 7.5, 11.5, 0 }, { "IPE 360", "I", 360, 170, 8, 12.7, 0 }, { "IPE 400", "I", 400, 180, 8.6, 13.5, 0 },
        { "IPE 450", "I", 450, 190, 9.4, 14.6, 0 }, { "IPE 500", "I", 500, 200, 10.2, 16, 0 }, { "IPE 550", "I", 550, 210, 11.1, 17.2, 0 },
        { "IPE 600", "I", 600, 220, 12, 19, 0 },
        { "HEA 200", "I", 190, 200, 6.5, 10, 0 }, { "HEA 240", "I", 230, 240, 7.5, 12, 0 }, { "HEA 300", "I", 290, 300, 8.5, 14, 0 }, { "HEA 400", "I", 390, 300, 11, 19, 0 },
        { "HEB 200", "I", 200, 200, 9, 15, 0 }, { "HEB 300", "I", 300, 300, 11, 19, 0 },
        { "UPN 200", "U", 200, 75, 8.5, 11.5, 0 }, { "UPN 300", "U", 300, 100, 10, 16, 0 },
        { "L 50×50×5", "L", 50, 50, 5, 5, 0 }, { "L 60×60×6", "L", 60, 60, 6, 6, 0 }, { "L 70×70×7", "L", 70, 70, 7, 7, 0 },
        { "L 80×80×8", "L", 80, 80, 8, 8, 0 }, { "L 100×100×10", "L", 100, 100, 10, 10, 0 },
        { "RHS 100×100×5", "RHS", 100, 100, 5, 5, 0 }, { "RHS 150×100×6", "RHS", 150, 100, 6, 6, 0 }, { "RHS 200×100×8", "RHS", 200, 100, 8, 8, 0 },
        { "CHS 60.3×3.2", "CHS", 60.3, 60.3, 3.2, 3.2, 0 }, { "CHS 88.9×4", "CHS", 88.9, 88.9, 4, 4, 0 }, { "CHS 114.3×5", "CHS", 114.3, 114.3, 5, 5, 0 }, { "CHS 168.3×6", "CHS", 168.3, 168.3, 6, 6, 0 },
        { "C 150×50×2", "C", 150, 50, 2, 2, 15 }, { "C 200×65×2", "C", 200, 65, 2, 2, 20 },
        { "Z 150×55×2", "Z", 150, 110, 2, 2, 15 }, { "Z 200×65×2", "Z", 200, 130, 2, 2, 20 },
        { "Σ 200×62×2", "SIGMA", 200, 62, 2, 2, 20 },
    };

    [Theory]
    [MemberData(nameof(Old))]
    public void Every_name_the_old_catalogue_had_keeps_its_nominal_sizes(string name, string kind, double depth, double width, double web, double flange, double lip)
    {
        var s = Section.Named(name)!;
        Assert.Equal(kind, s.Kind);
        Assert.Equal(name, s.Name);
        Assert.Equal(depth / 1000, s.Depth, 9);
        Assert.Equal(width / 1000, s.Width, 9);
        Assert.Equal(web / 1000, s.Web, 9);
        Assert.Equal(flange / 1000, s.Flange, 9);
        Assert.Equal(lip / 1000, s.Lip, 9);
        Assert.Contains(name, Section.Catalogue.Keys);
    }

    [Theory]
    [InlineData("IPE 300", "IPE300")]
    [InlineData("IPE 300", "ipe 300")]
    [InlineData("L 100×100×10", "L 100x100x10")]
    [InlineData("HEA 200", "HEA200")]
    public void A_profile_typed_with_or_without_spaces_is_the_same_EN_section(string a, string b)
    {
        var (x, y) = (Section.Named(a)!, Section.Named(b)!);
        Assert.True(JsonNode.DeepEquals(x.ToJson(), y.ToJson()), $"{x.ToJson()} ≠ {y.ToJson()}");
        Assert.Equal(x.Area, y.Area, 12);
    }

    [Fact]
    public void The_named_sections_carry_the_libraries_radii_and_slopes()
    {
        var ipe = Section.Named("IPE 300")!;
        Assert.Equal(0.015, ipe.Root, 9);
        Assert.Equal("EN 10365:2017", ipe.Standard);
        Assert.Equal(0.08, Section.Named("UPN 200")!.Slope, 9);
        Assert.Equal(0.05, Section.Named("UPN 400")!.Slope, 9);
        Assert.Equal(0.004, Section.Named("L 60×60×6")!.Toe, 9);
        Assert.True(ipe.Area > ipe.SharpArea);
    }

    [Fact]
    public void A_type_made_from_a_catalogue_name_stores_the_EN_section()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var a = f.Model.Get((string)f.Run("structure.createBeamType", new JsonObject { ["section"] = "IPE 300" })!["id"]!)!;
        var b = f.Model.Get((string)f.Run("structure.createBeamType", new JsonObject { ["section"] = "IPE300" })!["id"]!)!;
        Assert.Equal(0.015, Json.Number(a.Attrs["section"]!["r1"]));
        Assert.True(JsonNode.DeepEquals(a.Attrs["section"], b.Attrs["section"]));
    }

    static JsonObject OldIpe300() => new() { ["kind"] = "I", ["width"] = 0.15, ["depth"] = 0.3, ["web"] = 0.0071, ["flange"] = 0.0107, ["name"] = "IPE 300" };

    [Fact]
    public void An_old_catalogue_section_is_the_librarys_when_the_project_opens()
    {
        var type = new Entity("t1", IfcClass.BeamType, "B1", new JsonObject { ["section"] = OldIpe300(), ["material"] = "m1" });
        var upn = new Entity("t2", IfcClass.ColumnType, "C1", new JsonObject { ["section"] = new JsonObject { ["kind"] = "U", ["width"] = 0.075, ["depth"] = 0.2, ["web"] = 0.0085, ["flange"] = 0.0115, ["name"] = "UPN 200" } });
        var (es, _) = new CatalogueSectionUpgrade().Upgrade([type, upn], []);
        var s = (JsonObject)es[0].Attrs["section"]!;
        Assert.Equal(0.015, Json.Number(s["r1"]));
        Assert.Equal("EN 10365:2017", (string?)s["standard"]);
        Assert.Equal("m1", (string?)es[0].Attrs["material"]);
        Assert.Equal(0.08, Json.Number(es[1].Attrs["section"]!["slope"]));
        Assert.True(JsonNode.DeepEquals(Section.Named("IPE 300")!.ToJson(), Section.From(s)!.ToJson()));
        // Done once: opening it again changes nothing.
        var (again, _) = new CatalogueSectionUpgrade().Upgrade(es, []);
        Assert.Same(es[0], again[0]);
    }

    [Fact]
    public void A_section_of_its_own_stays_as_it_is()
    {
        var edited = OldIpe300(); edited["web"] = 0.008;                       // its sizes changed
        var library = OldIpe300(); library["profileId"] = "p1";                 // a library's profile in the project
        var unknown = new JsonObject { ["kind"] = "I", ["width"] = 0.1, ["depth"] = 0.2, ["web"] = 0.005, ["flange"] = 0.008, ["name"] = "My I" };
        var rect = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.5 };
        var material = new Entity("m", IfcClass.Material, "Steel", new JsonObject { ["section"] = new JsonObject { ["hatch"] = "steel" } });
        var list = new List<Entity> { material };
        foreach (var (s, i) in new[] { edited, library, unknown, rect }.Select((s, i) => (s, i)))
            list.Add(new Entity($"t{i}", IfcClass.BeamType, $"T{i}", new JsonObject { ["section"] = s }));
        var (es, _) = new CatalogueSectionUpgrade().Upgrade(list, []);
        for (var i = 0; i < list.Count; i++) Assert.Same(list[i], es[i]);
    }
    /// <summary>
    /// A project saved by the build before (fab28778): beam types „IPE 300” and „C 150×50×2”, a column type „UPN 200” from
    /// its own catalogue and one typed „IPE300” from the library, a beam and a column of them.
    /// </summary>
    [Fact]
    public void A_project_saved_with_the_old_catalogue_opens_with_the_librarys_sections()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        PackageFiles.Open(f.Session, Path.Combine(AppContext.BaseDirectory, "Fixtures", "old-steel-catalogue.lapin"));
        JsonObject S(string type) => (JsonObject)f.Model.Entities.Single(e => e.Name == type && e.Class.EndsWith("Type")).Attrs["section"]!;
        Assert.True(JsonNode.DeepEquals(Section.Named("IPE 300")!.ToJson(), S("B IPE")), S("B IPE").ToJsonString());
        Assert.True(JsonNode.DeepEquals(S("B EN"), S("B IPE")));
        Assert.Equal(0.08, Json.Number(S("C UPN")["slope"]));
        Assert.True(JsonNode.DeepEquals(Section.Named("C 150×50×2")!.ToJson(), S("B C")), S("B C").ToJsonString());
        // The elements stay where they were, of the same types, and build.
        Assert.Equal(2, f.Model.OfClass(IfcClass.Beam).Count());
        Assert.Single(f.Model.OfClass(IfcClass.Column));
        foreach (var e in f.Model.OfClass(IfcClass.Beam).Concat(f.Model.OfClass(IfcClass.Column)))
            Assert.NotEmpty(f.Session.Geometry(e.Id)!.Parts);
    }
}
