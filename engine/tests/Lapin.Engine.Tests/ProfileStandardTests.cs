using System.Text.Json.Nodes;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;
using Lapin.Steel;
using Lapin.Structure;
using Xunit;

namespace Lapin.Engine.Tests;

/// <summary>The standards the profile pickers group by and a project offers (decision 457, docs/plan-profile-libraries.md).</summary>
public class ProfileStandardTests
{
    static Fixture F() => new(new StructureModule(), new SteelModule());

    [Theory]
    [InlineData("EN 10365:2017", "EN")]
    [InlineData("EN 10210-2:2019", "EN")]
    [InlineData("DIN EN 10025-2", "EN")]
    [InlineData("SR EN 10056-1", "EN")]
    [InlineData("PN-EN 10219", "EN")]
    [InlineData("ASTM A6/A6M", "AISC / ASTM")]
    [InlineData("AISC Shapes Database v16.0", "AISC / ASTM")]
    [InlineData("BS 4-1:2005", "BS")]
    [InlineData("DIN 1025-5", "DIN")]
    [InlineData("JIS G 3192", "JIS")]
    [InlineData("AS/NZS 3679.1", "AS/NZS")]
    [InlineData("GB/T 11263", "GB")]
    [InlineData("ISO 657", null)]
    [InlineData("Firm catalogue 2024", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void A_standard_s_name_is_the_group_it_is_shown_under(string? standard, string? group) =>
        Assert.Equal(group, ProfileStandards.Group(standard));

    [Fact]
    public void A_library_says_its_standard_else_its_profiles_do_else_it_is_its_own()
    {
        // Lapin's library is EN, its generic cold-formed sections (no standard of their own) too.
        var builtin = ProfileLibrary.BuiltIn();
        Assert.Equal("EN", builtin.Standard);
        Assert.Equal(["EN"], ProfileStandards.Of(builtin));
        Assert.Contains(builtin.Profiles, p => p.Standard == null);
        // A library that says none: its profiles' standards, the ones with none under the library's name.
        var w = new SectionProfile { Name = "W14X90", Kind = "I", Family = "W", Standard = "ASTM A6", H = 0.356, B = 0.369, Tw = 0.0112, Tf = 0.018 };
        var own = new SectionProfile { Name = "Alu 50x100", Kind = "rectangle", H = 0.1, B = 0.05 };
        var mixed = new ProfileLibrary("Firm X", null, "steel", [w, own]);
        Assert.Equal(["AISC / ASTM", "Firm X"], ProfileStandards.Of(mixed));
        // One that says its own puts all its profiles there; it reads back as written.
        var firm = mixed with { Standard = "Firm X standard" };
        Assert.Equal(["Firm X standard"], ProfileStandards.Of(firm));
        var back = ProfileLibrary.From(JsonNode.Parse(firm.ToJson().ToJsonString()))!;
        Assert.Equal("Firm X standard", back.Standard);
        Assert.Null(ProfileLibrary.From(JsonNode.Parse(mixed.ToJson().ToJsonString()))!.Standard);
        Assert.Equal("EN", (string?)new Fixture().Session.Query("profiles.builtin", new JsonObject())!["standard"]);
    }

    [Fact]
    public void A_new_project_offers_the_standards_chosen_and_holds_no_profile_until_one_is_used()
    {
        var f = F();
        // By default a new project offers every standard.
        f.Session.New();
        Assert.Null(ProfileStandards.OfProject(f.Model));
        Assert.Null(f.Session.Query("profiles.project", new JsonObject())!["standards"]);
        // Made with the user's choice: the names on the project, no undo step, no profile copied.
        var made = f.Session.New(profileStandards: ["EN", "Firm X"]);
        Assert.Equal(["EN", "Firm X"], ProfileStandards.OfProject(f.Model));
        Assert.False(made.State!.CanUndo);
        Assert.Equal(["EN", "Firm X"], f.Session.Query("profiles.project", new JsonObject())!["standards"]!.AsArray().Select(x => (string?)x));
        Assert.Empty(f.Model.OfClass(ProjectProfiles.Class));
        // A type of a library profile: that one profile copied, with the standard it is shown under.
        var p = BuiltInProfiles.Find("IPE 300")!;
        f.Run("steel.createType", new JsonObject { ["profile"] = p.ToJson(), ["library"] = BuiltInProfiles.Name });
        var copy = Assert.Single(f.Model.OfClass(ProjectProfiles.Class));
        Assert.Equal("EN", copy.Str("standard"));
        Assert.Equal("EN", (string?)f.Session.Query("profiles.project", new JsonObject())!["copies"]![0]!["standard"]);
        // Sent with the group it was picked under, it keeps that one.
        var w = new SectionProfile { Name = "W14X90", Kind = "I", Family = "W", Standard = "ASTM A6", H = 0.356, B = 0.369, Tw = 0.0112, Tf = 0.018 };
        f.Run("profiles.use", new JsonObject { ["profile"] = w.ToJson(), ["library"] = "AISC v16", ["standard"] = "Firm X" });
        Assert.Equal("Firm X", f.Model.OfClass(ProjectProfiles.Class).Single(e => e.Name == "W14X90").Str("standard"));
        // A DXF opened as a project (plain) is not given any.
        f.Session.New(plain: true, profileStandards: ["EN"]);
        Assert.Null(ProfileStandards.OfProject(f.Model));
    }

    [Fact]
    public void The_project_s_standards_change_in_one_undo_step()
    {
        var f = F();
        f.Run("profiles.setStandards", new JsonObject { ["standards"] = new JsonArray("AISC / ASTM", " EN ", "en") });
        Assert.Equal(["AISC / ASTM", "EN"], ProfileStandards.OfProject(f.Model));
        f.Run("profiles.setStandards", new JsonObject { ["standards"] = null });
        Assert.Null(ProfileStandards.OfProject(f.Model));
        f.Session.Undo();
        Assert.Equal(["AISC / ASTM", "EN"], ProfileStandards.OfProject(f.Model));
        f.Session.Undo();
        Assert.Null(ProfileStandards.OfProject(f.Model));
    }

    [Fact]
    public void The_protocol_s_new_project_takes_the_standards()
    {
        var f = F();
        var d = new Lapin.Protocol.EngineDispatcher(f.Session, Lapin.Host.PackageFiles.Create());
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = "project.new", ["params"] = new JsonObject { ["profileStandards"] = new JsonArray("EN") } }.ToJsonString());
        Assert.Null(JsonNode.Parse(Lapin.Protocol.BinaryFrame.HeaderText(reply))!["error"]);
        Assert.Equal(["EN"], ProfileStandards.OfProject(f.Model));
    }
}
