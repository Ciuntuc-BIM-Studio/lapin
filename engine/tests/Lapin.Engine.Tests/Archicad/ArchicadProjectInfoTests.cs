using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Archicad;
using Lapin.Architecture;
using Lapin.Platform.Documents;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Archicad's Project Info into the project's information (docs/plan-archicad.md, A1b): the mapping table Lapin shares with Atrium Server
/// (the same bytes as the server's), the rows' rules (first id with a value, parts joined, phases routed, <c>when</c>), and the receive:
/// empty fields filled in its undo step, fields already set left as they are and reported as differing, the rest kept apart as
/// <c>info.source.archicad</c>. The synthetic fixture (fixtures/project-info) fills every field the table maps.
/// </summary>
public sealed class ArchicadProjectInfoTests
{
    static string Here([CallerFilePath] string here = "") => Path.GetDirectoryName(here)!;

    static JsonArray Full()
    {
        var j = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(Here(), "fixtures", "project-info", "GetProjectInfoFields.json")))!;
        return (JsonArray)j["response"]!["result"]!["addOnCommandResponse"]!["fields"]!.DeepClone();
    }

    static JsonObject FullAnswer() => new() { ["succeeded"] = true, ["result"] = new JsonObject { ["addOnCommandResponse"] = new JsonObject { ["fields"] = Full() } } };

    static Fixture Project(JsonObject? infoAnswer = null) => Project(infoAnswer, out _);

    static Fixture Project(JsonObject? infoAnswer, out ArchicadModule module)
    {
        var answers = ArchicadAnswers.Demo();
        answers.Answers["GetProjectInfoFields"] = [infoAnswer ?? FullAnswer()];
        module = new ArchicadModule(answers.Transport());
        return new Fixture(new ArchitectureModule(), new StructureModule(), module);
    }

    static JsonObject Receive(Fixture f) => (JsonObject)f.Run("archicad.receive", new JsonObject { ["port"] = ArchicadClient.FirstPort })!;

    static string[] Keys(JsonNode? list, string member = "key") => [.. (list as JsonArray ?? []).Select(x => (string)x![member]!)];

    [Fact]
    public void The_mapping_table_is_the_one_Atrium_Server_embeds_byte_for_byte()
    {
        var shipped = File.ReadAllBytes(Path.Combine(Here(), "..", "..", "..", "src", "Platform", "Lapin.Platform", "Documents", "project-info-mapping.json"));
        using (var s = typeof(ProjectInfoMapping).Assembly.GetManifestResourceStream(ProjectInfoMapping.ResourceName)!)
        using (var ms = new MemoryStream()) { s.CopyTo(ms); Assert.Equal(shipped, ms.ToArray()); }
        // The server's copy, when its repository is beside this one (ATRIUM_REPO, else ../Atrium-refs or ../Atrium): the same bytes.
        var github = Path.GetFullPath(Path.Combine(Here(), "..", "..", "..", "..", ".."));
        var candidates = new[] { Environment.GetEnvironmentVariable("ATRIUM_REPO"), Path.Combine(github, "Atrium-refs"), Path.Combine(github, "Atrium") }
            .OfType<string>().Select(r => Path.Combine(r, "docs", "project-info-mapping.json")).Where(File.Exists).ToList();
        foreach (var server in candidates) Assert.True(shipped.AsSpan().SequenceEqual(File.ReadAllBytes(server)), $"{server} differs from Lapin's copy: copy it again.");
        // Its keys are the project information's.
        Assert.Equal(new[] { "name" }.Concat(ProjectInfo.Fields.Select(f => f.Key)), ProjectInfoMapping.Default.Keys);
        Assert.Equal(ProjectInfo.Phases, ProjectInfoMapping.Default.PhaseCodes);
    }

    [Fact]
    public void Every_field_the_table_maps_from_Archicad_lands_under_Lapins_key()
    {
        var (fields, extras) = ArchicadProjectInfo.Map(Full());
        Assert.Equal("Locuință P+1 Huși", fields["name"]);
        Assert.Equal("104/2026", fields["number"]);                    // PROJECTNUMBER first: the code and the ID are kept apart
        Assert.Equal("DTAC", fields["phase"]);                         // a phase code in Lapin's spelling
        Assert.False(fields.ContainsKey("phaseName"));
        Assert.Equal("Ion Popescu", fields["client"]);                 // the company is empty: the client's full name
        Assert.Equal("Str. Mare 1, Iași", fields["clientAddress"]);    // the full address, its parts used with it
        Assert.Equal("Str. Florilor 12, Huși, Vaslui, 735100, România", fields["address"]);   // no full address: the parts joined
        Assert.Equal("Ciuntuc BIM Studio SRL", fields["designer"]);
        Assert.Equal("Str. Huși 1, Huși, România", fields["designerAddress"]);
        Assert.Equal("J2025031471009", fields["designerRegistration"]);
        Assert.Equal("0770 000 000", fields["designerPhone"]);
        Assert.Equal("office@example.com", fields["designerEmail"]);
        Assert.Equal("Ionuț Ciuntuc", fields["projectManager"]);
        Assert.Equal("Ana Popescu", fields["author"]);
        Assert.Equal(13, fields.Count);                                // phase or phaseName, never both
        Assert.Equal(["CLIENTEMAIL", "CONTACT_ROLE", "KEYWORDS", "PROJECT_CODE", "PROJECT_DESCRIPTION", "PROJECT_ID", "SITE_NAME", "autotext-PROJECT-1"], extras.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(("Permit authority", "Primăria Huși"), (extras["autotext-PROJECT-1"].Label, extras["autotext-PROJECT-1"].Value));
    }

    [Fact]
    public void A_status_that_is_not_a_phase_code_is_the_phase_name_and_the_number_falls_back_to_the_code()
    {
        var raw = new JsonObject { ["PROJECTSTATUS"] = "Concept design", ["PROJECT_CODE"] = " LH-7 ", ["PROJECT_ID"] = "P-104", ["NOTES"] = "" };
        var (fields, extras) = ArchicadProjectInfo.Map(raw);
        Assert.Equal("Concept design", fields["phaseName"]);
        Assert.False(fields.ContainsKey("phase"));
        Assert.Equal("LH-7", fields["number"]);
        Assert.Equal(["PROJECT_ID"], extras.Keys);                    // an empty value is never kept
        // IFC's table: Name is the number only when LongName says something else.
        var ifc = ProjectInfoMapping.Default.Map("ifc", ProjectInfoMapping.Raw(new JsonObject { ["IfcProject.Name"] = "P-104", ["IfcProject.LongName"] = "Locuință", ["IfcProject.Phase"] = "pth" }));
        Assert.Equal(("Locuință", "P-104", "PTh"), (ifc.Fields["name"], ifc.Fields["number"], ifc.Fields["phase"]));
        var same = ProjectInfoMapping.Default.Map("ifc", ProjectInfoMapping.Raw(new JsonObject { ["IfcProject.Name"] = "Casa" }));
        Assert.Equal("Casa", same.Fields["name"]);
        Assert.False(same.Fields.ContainsKey("number"));
    }

    [Fact]
    public void A_receive_fills_the_empty_fields_in_its_undo_step_and_keeps_the_rest_apart()
    {
        var f = Project();
        var s = (JsonObject)f.Session.Query("archicad.survey", new JsonObject { ["port"] = ArchicadClient.FirstPort })!;
        Assert.Equal(13, ((JsonArray)s["projectInfo"]!["filled"]!).Count);       // the survey says what the receive would fill
        Assert.Null(ProjectInfo.Get(f.Model, "number"));

        var r = Receive(f);
        var info = (JsonObject)r["projectInfo"]!;
        Assert.Equal(13, Keys(info["filled"]).Length);
        Assert.Empty((JsonArray)info["differs"]!);
        Assert.Equal(8, ((JsonArray)info["kept"]!).Count);
        Assert.Equal("Locuință P+1 Huși", ProjectInfo.Name(f.Model));
        Assert.Equal("DTAC", ProjectInfo.Get(f.Model, "phase"));
        var kept = (JsonObject)ProjectInfo.Sources(f.Model)["archicad"]!;
        Assert.Equal("Casă unifamilială P+1", (string?)kept["PROJECT_DESCRIPTION"]!["value"]);
        Assert.Equal("Project Description", (string?)kept["PROJECT_DESCRIPTION"]!["label"]);
        Assert.Equal("Primăria Huși", (string?)((JsonObject)f.Session.Query("project.info", new JsonObject())!)["source"]!["archicad"]!["autotext-PROJECT-1"]!["value"]);

        // One undo step takes the elements and the information back together.
        f.Session.Undo();
        Assert.Null(ProjectInfo.Name(f.Model));
        Assert.Null(ProjectInfo.Get(f.Model, "phase"));
        Assert.Empty(ProjectInfo.Sources(f.Model));
        Assert.Empty(ArchicadReceive.Existing(f.Model, "archicad|untitled"));
    }

    [Fact]
    public void A_field_set_here_is_never_overwritten_the_report_says_it_differs()
    {
        var f = Project(null, out var module);
        f.Run("project.setInfo", new JsonObject { ["number"] = "77/2026", ["client"] = "Ion Popescu" });
        var r = Receive(f);
        var info = (JsonObject)r["projectInfo"]!;
        var differs = Assert.Single((JsonArray)info["differs"]!)!;
        Assert.Equal(("number", "77/2026", "104/2026"), ((string?)differs["key"], (string?)differs["lapin"], (string?)differs["archicad"]));
        Assert.Contains("client", ((JsonArray)info["same"]!).Select(x => (string)x!));
        Assert.Equal("77/2026", ProjectInfo.Get(f.Model, "number"));
        Assert.DoesNotContain("number", Keys(info["filled"]));

        // Received again: nothing left to fill; the kept values replaced by what Archicad says now.
        var answers = ArchicadAnswers.Demo();
        var fewer = FullAnswer();
        var list = (JsonArray)fewer["result"]!["addOnCommandResponse"]!["fields"]!;
        foreach (var x in list.OfType<JsonObject>().Where(x => (string?)x["projectInfoId"] == "KEYWORDS").ToList()) x["projectInfoValue"] = "";
        answers.Answers["GetProjectInfoFields"] = [fewer];
        module.Transport = answers.Transport();
        var again = (JsonObject)Receive(f)["projectInfo"]!;
        Assert.Empty((JsonArray)again["filled"]!);
        Assert.Null(((JsonObject)ProjectInfo.Sources(f.Model)["archicad"]!)["KEYWORDS"]);
    }

    [Fact]
    public void Archicad_busy_with_a_dialog_still_receives_and_says_the_information_was_not_read()
    {
        var f = Project(new JsonObject { ["succeeded"] = false, ["error"] = new JsonObject { ["code"] = 4001, ["message"] = "Invalid program status (there is an open modal dialog: Project Info)" } });
        var r = Receive(f);
        Assert.Null(r["projectInfo"]);
        Assert.Contains(((JsonArray)r["notes"]!).Select(n => (string)n!), n => n.StartsWith("Archicad's Project Info was not read", StringComparison.Ordinal));
        Assert.True((int)r["elements"]! > 0);
    }

    [Fact]
    public void Send_to_Atrium_reads_Tapirs_answer_as_it_is_for_the_servers_proposal()
    {
        var f = Project();
        var o = (JsonObject)f.Session.Query("archicad.projectInfo", new JsonObject { ["port"] = ArchicadClient.FirstPort })!;
        Assert.Equal(Full().ToJsonString(), o["raw"]!.ToJsonString());
        Assert.Equal(13, ((JsonArray)o["filled"]!).Count);
        Assert.Null(o["file"]);                                        // the demo project is untitled
    }
}
