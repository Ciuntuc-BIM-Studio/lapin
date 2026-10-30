using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Cde;
using Lapin.Protocol;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The project folder as a CDE (docs/plan-cde.md, decision 180): the
/// ISO 19650-2 names, revisions and status codes, the pre-issue checks (each
/// rule both ways), the transmittal and the manifest with their hashes, and
/// an issue end to end into a temporary folder through the protocol.
/// </summary>
public sealed class CdeTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-cde-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StructureModule());

    /// <summary>A sheet with the ground floor's plan on it, its title block filled so the blocking rules pass.</summary>
    static string Sheet(Fixture f, string number = "A-101", string name = "Ground floor plan", bool filled = true, string? discipline = null)
    {
        var a = new JsonObject { ["name"] = name, ["number"] = number, ["paper"] = "A3" };
        if (discipline != null) a["discipline"] = discipline;
        var s = (string)f.Run("sheet.create", a)!["id"]!;
        // A plan view of its own (a view sits on one sheet).
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = name, ["scale"] = 100 })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = plan, ["x"] = 150, ["y"] = 120 });
        if (filled) f.Run("sheet.titleBlock", new JsonObject { ["id"] = s, ["fields"] = new JsonObject { ["drawnBy"] = "IC", ["checkedBy"] = "AB", ["date"] = "30.10.2026", ["phase"] = "DTAC" } });
        return s;
    }

    static List<CdeFinding> Check(Fixture f, string? revision = null, params string[] sheets) =>
        CdeChecks.Run(f.Session.Reader, sheets.Length == 0 ? null : sheets.Select(id => f.Model.Get(id)!).ToList(), revision);

    static IEnumerable<CdeFinding> Of(IEnumerable<CdeFinding> findings, string rule) => findings.Where(x => x.Rule == rule);

    // ------------------------------------------------------------ naming

    [Fact]
    public void Names_follow_ISO_19650_project_originator_volume_level_type_role_number()
    {
        var f = F();
        var s = Sheet(f);
        var naming = CdeNaming.Of(f.Model);
        Assert.Equal("PRJ-ORG-ZZ-00-DR-A-0101", naming.For(f.Model, f.Model.Get(s)!));
        Assert.Equal("PRJ-ORG-ZZ-ZZ-M3-A-0001", naming.ForModel());
        Assert.Equal("PRJ-ORG-ZZ-ZZ-IE-A-0003", naming.ForTransmittal(3));
        Assert.Equal("PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf", CdeNaming.WithRevision("PRJ-ORG-ZZ-00-DR-A-0101", "S2", "P01", "pdf"));
        Assert.Empty(naming.Validate("PRJ-ORG-ZZ-00-DR-A-0101"));
        var parsed = naming.Parse("PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf");
        Assert.Equal(("PRJ", "ORG", "ZZ", "00", "DR", "A", "0101", "S2", "P01"), parsed!.Value);
        // A structural sheet files under S; a sheet of sections alone has no level; a number without digits has no number field.
        var structural = Sheet(f, "S-201", "Foundations", discipline: "Structure");
        Assert.Equal("PRJ-ORG-ZZ-00-DR-S-0201", naming.For(f.Model, f.Model.Get(structural)!));
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["name"] = "A-A", ["start"] = new JsonArray(0, -1), ["end"] = new JsonArray(0, 5), ["scale"] = 50 })!["id"]!;
        var sections = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Sections", ["number"] = "A-301" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sections, ["viewId"] = section, ["x"] = 200, ["y"] = 150 });
        Assert.Equal("PRJ-ORG-ZZ-ZZ-DR-A-0301", naming.For(f.Model, f.Model.Get(sections)!));
        Assert.Null(CdeNaming.NumberOf("Plans"));
        Assert.Equal("0007", CdeNaming.NumberOf("7"));
    }

    [Fact]
    public void Level_codes_count_from_the_storey_at_zero_with_B_below()
    {
        var f = F();
        // Foundations below, the ground floor at ±0.00, two floors above.
        f.Run("storey.create", new JsonObject { ["name"] = "Foundations", ["elevation"] = -3 });
        f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 });
        f.Run("storey.create", new JsonObject { ["name"] = "Second floor", ["elevation"] = 6 });
        var storeys = Spatial.Storeys(f.Model).ToList();
        Assert.Equal(["B1", "00", "01", "02"], storeys.Select(s => CdeNaming.LevelOf(f.Model, s.Id)));
        Assert.Equal("ZZ", CdeNaming.LevelOf(f.Model, "nope"));
    }

    [Fact]
    public void A_name_is_validated_field_by_field_and_the_project_s_codes_are_cleaned_or_refused()
    {
        var naming = new CdeNaming();
        Assert.Contains(naming.Validate("PRJ-ORG-ZZ-00-DR-A"), p => p.Contains("6 fields"));
        Assert.Contains(naming.Validate("prj-ORG-ZZ-00-DR-A-0101"), p => p.Contains("project"));
        Assert.Contains(naming.Validate("PRJ-ORG-ZZ-00-QQ-A-0101"), p => p.Contains("type"));
        Assert.Contains(naming.Validate("PRJ-ORG-ZZ-00-DR-A-01A1"), p => p.Contains("digits"));
        var f = F();
        var set = (JsonObject)f.Run("cde.setNaming", new JsonObject { ["project"] = "hs-24", ["originator"] = "cbs", ["role"] = "s" })!;
        Assert.Equal("HS24", (string)set["project"]!);
        Assert.Equal("CBS", (string)set["originator"]!);
        Assert.Equal("S", (string)set["role"]!);
        Assert.Equal("HS24-CBS-ZZ-ZZ-M3-S-0001", CdeNaming.Of(f.Model).ForModel());
        Assert.Throws<CommandException>(() => f.Run("cde.setNaming", new JsonObject { ["project"] = "x" }));
        // The required properties are the project's to choose; null brings the defaults back.
        f.Run("cde.setNaming", new JsonObject { ["requiredProps"] = new JsonArray("FireRating") });
        Assert.Equal(["FireRating"], CdeChecks.Required(f.Model).Select(r => r.Name));
        f.Run("cde.setNaming", new JsonObject { ["requiredProps"] = null });
        Assert.Equal(CdeChecks.DefaultRequired.Select(r => r.Name), CdeChecks.Required(f.Model).Select(r => r.Name));
    }

    // ------------------------------------------------------------ revisions

    [Fact]
    public void Revisions_go_P01_P02_then_C01_and_statuses_suggest_their_container()
    {
        Assert.Equal("P01", CdeRevisions.Next(null));
        Assert.Equal("C01", CdeRevisions.Next(null, contractual: true));
        Assert.Equal("P02", CdeRevisions.Next("P01"));
        Assert.Equal("P10", CdeRevisions.Next("P09"));
        Assert.Equal("C01", CdeRevisions.Next("P04", contractual: true));
        Assert.Equal("C02", CdeRevisions.Next("C01"));
        Assert.Equal("C02", CdeRevisions.Next("C02", contractual: false));
        Assert.True(CdeRevisions.Compare("P03", "C01") < 0);
        Assert.True(CdeRevisions.IsRevision("P01") && CdeRevisions.IsRevision("C12") && !CdeRevisions.IsRevision("R1") && !CdeRevisions.IsRevision("P1"));
        Assert.True(CdeRevisions.IsStatus("S0") && CdeRevisions.IsStatus("A3") && CdeRevisions.IsStatus("CR") && !CdeRevisions.IsStatus("S9") && !CdeRevisions.IsStatus("X1"));
        Assert.Equal("WIP", CdeRevisions.ContainerFor("S0"));
        Assert.Equal("Shared", CdeRevisions.ContainerFor("S2"));
        Assert.Equal("Published", CdeRevisions.ContainerFor("A1"));
        Assert.Equal("Published", CdeRevisions.ContainerFor("CR"));
    }

    // ------------------------------------------------------------ the checks

    [Fact]
    public void Title_block_blocks_on_number_title_and_drawn_by_and_warns_on_the_rest()
    {
        var f = F();
        var s = Sheet(f, filled: false);
        var findings = Of(Check(f), "titleBlock").ToList();
        Assert.Contains(findings, x => x.Blocking && x.Message.Contains("Drawn by") && x.SheetId == s);
        Assert.Contains(findings, x => !x.Blocking && x.Message.Contains("Checked by"));
        Assert.Contains(findings, x => !x.Blocking && x.Message.Contains("date"));
        Assert.Contains(findings, x => !x.Blocking && x.Message.Contains("phase"));
        Assert.Contains(findings, x => !x.Blocking && x.Message.Contains("revision"));
        f.Run("sheet.titleBlock", new JsonObject { ["id"] = s, ["fields"] = new JsonObject { ["drawnBy"] = "IC", ["checkedBy"] = "AB", ["date"] = "30.10.2026", ["phase"] = "DTAC", ["revision"] = "P01" } });
        Assert.Empty(Of(Check(f), "titleBlock"));
        // An issue brings its own revision: the empty revision is not a finding then.
        f.Run("sheet.titleBlock", new JsonObject { ["id"] = s, ["fields"] = new JsonObject { ["revision"] = "" } });
        Assert.Contains(Of(Check(f), "titleBlock"), x => x.Message.Contains("revision"));
        Assert.Empty(Of(Check(f, "P01"), "titleBlock"));
        // A sheet named "Sheet" with no number: blocking, both.
        var bare = (string)f.Run("sheet.create", new JsonObject { ["number"] = " " })!["id"]!;
        f.Session.Edit("no number", e => { var sh = e.Model.Get(bare)!; var a = sh.CloneAttrs(); a.Remove("number"); e.Replace(sh.With(attrs: a)); });
        Assert.Equal(2, Of(Check(f, null, bare), "titleBlock").Count(x => x.Blocking && (x.Message.Contains("no number") || x.Message.Contains("no title"))));
    }

    [Fact]
    public void A_view_past_the_frame_or_over_the_title_block_blocks_and_one_without_a_scale_too()
    {
        var f = F();
        var s = Sheet(f);
        var vp = Sheets.Viewports(f.Model.Get(s)!).Single().Id;
        Assert.Empty(Of(Check(f), "viewOutside"));
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = s, ["viewportId"] = vp, ["x"] = 20, ["y"] = 20, ["width"] = 100, ["height"] = 80 });
        Assert.Contains(Of(Check(f), "viewOutside"), x => x.Blocking && x.Message.Contains("past the frame") && x.SheetId == s);
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = s, ["viewportId"] = vp, ["x"] = 350, ["y"] = 260, ["width"] = 100, ["height"] = 60 });
        Assert.Contains(Of(Check(f), "viewOutside"), x => x.Message.Contains("over the title block"));
        f.Run("sheet.updateViewport", new JsonObject { ["id"] = s, ["viewportId"] = vp, ["x"] = 150, ["y"] = 120, ["width"] = 100, ["height"] = 80 });
        Assert.Empty(Of(Check(f), "viewOutside"));
        // A view whose scale is gone.
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Plan", ["scale"] = 100 })!["id"]!;
        var other = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["number"] = "A-102" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = other, ["viewId"] = view, ["x"] = 150, ["y"] = 120 });
        Assert.Empty(Of(Check(f), "viewScale"));
        f.Session.Edit("no scale", e => { var v = e.Model.Get(view)!; var a = v.CloneAttrs(); a.Remove("scale"); e.Replace(v.With(attrs: a)); });
        Assert.Contains(Of(Check(f), "viewScale"), x => x.Blocking && x.ViewId == view && x.SheetId == other);
    }

    [Fact]
    public void A_plan_coloured_by_a_scheme_wants_a_legend_and_views_on_no_sheet_are_told()
    {
        var f = F();
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Rooms", ["scale"] = 100 })!["id"]!;
        Assert.Contains(Of(Check(f), "viewUnplaced"), x => !x.Blocking && x.ViewId == view);
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Rooms", ["number"] = "A-110" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = view, ["x"] = 150, ["y"] = 120 });
        Assert.Empty(Of(Check(f), "viewUnplaced"));
        Assert.Empty(Of(Check(f), "legend"));
        f.Session.Edit("scheme", e => { var v = e.Model.Get(view)!; var a = v.CloneAttrs(); a["colorScheme"] = "function"; e.Replace(v.With(attrs: a)); });
        Assert.Contains(Of(Check(f), "legend"), x => !x.Blocking && x.ViewId == view && x.Message.Contains("scheme"));
        f.Session.Edit("legend", e => e.Add(new Entity(e.NewId(), IfcClass.Annotation, "Colour legend", new JsonObject { ["viewId"] = view, ["at"] = new JsonArray(0, 0) }, "LEGEND")));
        Assert.Empty(Of(Check(f), "legend"));
    }

    [Fact]
    public void A_site_plan_without_its_north_arrow_is_told()
    {
        var f = F();
        var site = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["site"] = "situation", ["storeyId"] = f.Storey })!["id"]!;
        Assert.Empty(Of(Check(f), "northArrow"));
        f.Session.Edit("no arrow", e => { var v = e.Model.Get(site)!; var a = v.CloneAttrs(); a["northArrow"] = false; e.Replace(v.With(attrs: a)); });
        Assert.Contains(Of(Check(f), "northArrow"), x => !x.Blocking && x.ViewId == site);
    }

    [Fact]
    public void A_sheet_changed_since_its_last_issue_cannot_go_out_at_the_same_revision()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var s = Sheet(f);
        var hash = Sheets.DrawingHash(f.Session.Reader, f.Model.Get(s)!);
        // The hash reads the drawing, not the date or the title block.
        f.Run("sheet.titleBlock", new JsonObject { ["id"] = s, ["fields"] = new JsonObject { ["date"] = "01.01.2030", ["client"] = "Someone" } });
        Assert.Equal(hash, Sheets.DrawingHash(f.Session.Reader, f.Model.Get(s)!));
        f.Run("cde.recordIssue", new JsonObject
        {
            ["name"] = "Stage 2", ["revision"] = "P01", ["status"] = "S2", ["container"] = "Shared",
            ["files"] = new JsonArray(new JsonObject { ["name"] = "x.pdf", ["kind"] = "pdf", ["sheetId"] = s, ["sha256"] = "00", ["size"] = 1, ["drawingHash"] = hash }),
        });
        Assert.Equal("P01", CdeCheckContext.Field(f.Model.Get(s)!, "revision"));
        Assert.Equal("S2", CdeCheckContext.Field(f.Model.Get(s)!, "status"));
        Assert.Empty(Of(Check(f, "P01"), "revisionUnchanged"));
        f.Wall(0, 3, 5, 3);
        Assert.NotEqual(hash, Sheets.DrawingHash(f.Session.Reader, f.Model.Get(s)!));
        Assert.Contains(Of(Check(f, "P01"), "revisionUnchanged"), x => x.Blocking && x.SheetId == s && x.Message.Contains("Stage 2"));
        // Manage → Check reads the sheet's own revision; a new revision clears it.
        Assert.NotEmpty(Of(Check(f), "revisionUnchanged"));
        Assert.Empty(Of(Check(f, "P02"), "revisionUnchanged"));
        // The sheet's revision table lists the issue.
        var pens = Sheets.Compose(f.Session.Reader, f.Model.Get(s)!).Pens;
        Assert.Contains(pens.SelectMany(p => p.Texts), t => t.Text == "DESCRIPTION");
        Assert.Contains(pens.SelectMany(p => p.Texts), t => t.Text == "Stage 2");
        Assert.Contains(pens.SelectMany(p => p.Texts), t => t.Text == "REV");
    }

    [Fact]
    public void A_sheet_number_that_breaks_the_convention_blocks()
    {
        var f = F();
        Assert.Empty(Of(Check(f, null, Sheet(f)), "naming"));
        var odd = Sheet(f, number: "Plans");
        Assert.Contains(Of(Check(f, null, odd), "naming"), x => x.Blocking && x.Message.Contains("ends in no digits"));
    }

    [Fact]
    public void The_model_s_basics_storey_type_material_tags_room_numbers_and_required_properties()
    {
        var f = F();
        var wall = f.Wall(0, 0, 5, 0);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["x"] = 2, ["y"] = 0 });
        var door = f.Model.OfClass(IfcClass.Door).Single().Id;
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 2, ["x2"] = 5, ["y2"] = 2 })!["id"]!;
        var all = Check(f);
        Assert.Empty(Of(all, "storey"));
        Assert.Empty(Of(all, "type"));
        Assert.Empty(Of(all, "material"));
        Assert.Empty(Of(all, "roomNumber"));
        Assert.Contains(Of(all, "openingTag"), x => !x.Blocking && x.EntityId == door);
        Assert.Contains(Of(all, "psetCommon"), x => x.EntityId == wall && x.Message.Contains("Pset_WallCommon.IsExternal"));
        Assert.Contains(Of(all, "psetCommon"), x => x.EntityId == wall && x.Message.Contains("LoadBearing"));
        Assert.Contains(Of(all, "psetCommon"), x => x.EntityId == door && x.Message.Contains("FireRating"));

        // Fixed one by one.
        f.Run("drafting.addTag", new JsonObject { ["viewId"] = "plan:" + f.Storey, ["element"] = door, ["at"] = new JsonArray(2, 1) });
        f.Run("element.setParam", new JsonObject { ["id"] = wall, ["pset"] = "Pset_WallCommon", ["name"] = "IsExternal", ["value"] = true });
        f.Run("element.setParam", new JsonObject { ["id"] = wall, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        f.Run("element.setParam", new JsonObject { ["id"] = door, ["pset"] = "Pset_DoorCommon", ["name"] = "FireRating", ["value"] = "EI30" });
        var after = Check(f);
        Assert.Empty(Of(after, "openingTag"));
        Assert.DoesNotContain(Of(after, "psetCommon"), x => x.EntityId == wall || x.EntityId == door);

        // Broken on purpose: a wall on no storey and without a type, a beam without a material, a room without a number.
        string loose = "", room = "";
        f.Session.Edit("loose", e =>
        {
            loose = e.NewId();
            e.Add(new Entity(loose, IfcClass.Wall, "Loose wall", new JsonObject { ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(1, 0) }));
            room = e.NewId();
            e.Add(new Entity(room, IfcClass.Space, "Kitchen", new JsonObject { ["storeyId"] = f.Storey }));
            var b = e.Model.Get(beam)!; var a = b.CloneAttrs(); a.Remove("material"); e.Replace(b.With(attrs: a));
        });
        var broken = Check(f);
        Assert.Contains(Of(broken, "storey"), x => x.EntityId == loose);
        Assert.Contains(Of(broken, "type"), x => x.EntityId == loose);
        Assert.Contains(Of(broken, "material"), x => x.EntityId == loose);
        // A beam keeps its material through its type's relationship even with the attribute gone.
        Assert.DoesNotContain(Of(broken, "material"), x => x.EntityId == beam && f.Model.RelatingOf(beam, IfcClass.RelAssociatesMaterial) != null);
        Assert.Contains(Of(broken, "roomNumber"), x => x.EntityId == room && x.Message.Contains("Kitchen"));
        Assert.True(broken.All(x => x.Rule is "storey" or "type" or "material" or "roomNumber" or "openingTag" or "psetCommon" ? !x.Blocking : true));
    }

    [Fact]
    public void A_module_s_rule_joins_the_built_in_ones_through_the_hook()
    {
        var f = F();
        var rule = new HookRule();
        CdeHooks.Rules.Add(rule);
        try
        {
            var r = (JsonObject)f.Session.Query("cde.check", [])!;
            Assert.Contains((r["findings"] as JsonArray)!.OfType<JsonObject>(), x => (string?)x["rule"] == "company" && (string?)x["severity"] == "block");
            Assert.Contains((r["rules"] as JsonArray)!.OfType<JsonObject>(), x => (string?)x["id"] == "company");
            Assert.Equal(1, (int)r["blocking"]!);
        }
        finally { CdeHooks.Rules.Remove(rule); }
    }

    sealed class HookRule : ICdeRule
    {
        public string Id => "company";
        public string Label => "Company rule";
        public bool Blocking => true;
        public IEnumerable<CdeFinding> Run(CdeCheckContext c) => [new CdeFinding(Id, CdeFinding.Block, "The company says no.")];
    }

    // ------------------------------------------------------------ transmittal, manifest, hashes

    [Fact]
    public void The_transmittal_csv_and_the_manifest_carry_every_file_with_its_hash()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", CdeTransmittal.Sha256("abc"));
        var h = new CdeTransmittal.Header("House", "Stage 2", 1, "30.10.2026", "P01", "S2", "Shared", "For the client, \"draft\"", "ORG");
        var rows = new List<CdeTransmittal.Row>
        {
            new(new IssuedFile("PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf", "pdf", "sheet-1", "aa", 1234, "h1"), "A-101", "Ground floor, plan"),
            new(new IssuedFile("PRJ-ORG-ZZ-ZZ-M3-A-0001_S2_P01.ifc", "ifc", null, "bb", 5678), null, null),
        };
        var csv = CdeTransmittal.Csv(h, rows);
        var lines = csv.TrimEnd('\n').Split('\n');
        Assert.Equal("Project,House", lines[0]);
        Assert.Equal("Note,\"For the client, \"\"draft\"\"\"", lines[5]);
        Assert.Equal("File,Kind,Sheet,Title,Revision,Status,SHA-256,Size", lines[7]);
        Assert.Equal("PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf,PDF,A-101,\"Ground floor, plan\",P01,S2,aa,1234", lines[8]);
        Assert.Equal("PRJ-ORG-ZZ-ZZ-M3-A-0001_S2_P01.ifc,IFC,,,P01,S2,bb,5678", lines[9]);
        var manifest = JsonNode.Parse(CdeTransmittal.Manifest(h, rows, "PRJ-ORG-ZZ-ZZ-IE-A-0001_S2_P01.pdf")) as JsonObject;
        Assert.Equal("lapin-issue/1", (string)manifest!["format"]!);
        Assert.Equal("P01", (string)manifest["revision"]!);
        var files = (manifest["files"] as JsonArray)!.OfType<JsonObject>().ToList();
        Assert.Equal(2, files.Count);
        Assert.Equal("aa", (string)files[0]["sha256"]!);
        Assert.Equal("A-101", (string)files[0]["sheetNumber"]!);
        Assert.Equal("h1", (string)files[0]["drawingHash"]!);
        Assert.Null(files[1]["sheetId"]);
        // The PDF is a PDF with its texts.
        var pdf = CdeTransmittal.Pdf(h, rows);
        Assert.StartsWith("%PDF", System.Text.Encoding.Latin1.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 500);
    }

    // ------------------------------------------------------------ the issue, end to end

    [Fact]
    public void An_issue_writes_the_container_hashes_its_files_and_records_itself_then_the_next_revision_follows()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var s = Sheet(f);
        var files = PackageFiles.Create();
        var dispatcher = new EngineDispatcher(f.Session, files);
        string Call(string method, JsonObject p)
        {
            var (reply, _) = dispatcher.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
            return BinaryFrame.HeaderText(reply);
        }
        static JsonObject Result(string frame)
        {
            var o = (JsonObject)JsonNode.Parse(frame)!;
            if (o["error"] is { } err) throw new InvalidOperationException((string)err!);
            return (JsonObject)o["result"]!;
        }
        var folder = Path.Combine(_dir, "cde");

        // Without a folder, or an unsaved project: refused before anything is written.
        Assert.Contains("project folder", (string)((JsonObject)JsonNode.Parse(Call("cde.issue", new JsonObject { ["sheets"] = new JsonArray(s) }))!)["error"]!);
        f.Run("cde.setFolder", new JsonObject { ["path"] = folder });
        Assert.Contains("Save the project", (string)((JsonObject)JsonNode.Parse(Call("cde.issue", new JsonObject { ["sheets"] = new JsonArray(s) }))!)["error"]!);
        files.Save(f.Session, Path.Combine(_dir, "house.lapin"));
        Assert.False(Directory.Exists(folder));

        var settings = (JsonObject)f.Session.Query("cde.settings", [])!;
        Assert.Equal(folder, (string)settings["folder"]!);
        Assert.False((bool)settings["exists"]!);
        Assert.Equal("P01", (string)settings["nextRevision"]!);

        var r = Result(Call("cde.issue", new JsonObject { ["sheets"] = new JsonArray(s), ["model"] = true, ["name"] = "Stage 2", ["note"] = "For the client" }));
        Assert.Equal("Stage 2", (string)r["name"]!);
        Assert.Equal("P01", (string)r["revision"]!);
        Assert.Equal("S2", (string)r["status"]!);
        Assert.Equal(Path.Combine(folder, "Shared"), (string)r["path"]!);
        foreach (var c in CdeRevisions.Containers) Assert.True(Directory.Exists(Path.Combine(folder, c)), c);
        var written = Directory.GetFiles(Path.Combine(folder, "Shared")).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(
        [
            "PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf", "PRJ-ORG-ZZ-ZZ-IE-A-0001_S2_P01.csv", "PRJ-ORG-ZZ-ZZ-IE-A-0001_S2_P01.json", "PRJ-ORG-ZZ-ZZ-IE-A-0001_S2_P01.pdf",
            "PRJ-ORG-ZZ-ZZ-M3-A-0001_S2_P01.ifc",
        ], written);
        // The manifest's hashes are the files'.
        var manifest = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "Shared", "PRJ-ORG-ZZ-ZZ-IE-A-0001_S2_P01.json")))!;
        var listed = (manifest["files"] as JsonArray)!.OfType<JsonObject>().ToList();
        Assert.Equal(2, listed.Count);
        foreach (var entry in listed)
        {
            var bytes = File.ReadAllBytes(Path.Combine(folder, "Shared", (string)entry["name"]!));
            Assert.Equal(CdeTransmittal.Sha256(bytes), (string)entry["sha256"]!);
            Assert.Equal(bytes.LongLength, (long)entry["size"]!);
        }
        Assert.Equal("For the client", (string)manifest["note"]!);
        Assert.StartsWith("%PDF", File.ReadAllText(Path.Combine(folder, "Shared", "PRJ-ORG-ZZ-ZZ-IE-A-0001_S2_P01.pdf"))[..4]);
        Assert.Contains("IFCPROJECT", File.ReadAllText(Path.Combine(folder, "Shared", "PRJ-ORG-ZZ-ZZ-M3-A-0001_S2_P01.ifc")));
        // Recorded: the issue with its files, the sheet at P01 S2, and the project unsaved again (the version is the interface's next call).
        var issues = CdeIssues.All(f.Model);
        Assert.Single(issues);
        Assert.Equal(("Stage 2", "P01", "S2", "Shared", 1), (issues[0].Name, issues[0].Revision, issues[0].Status, issues[0].Container, issues[0].Number));
        Assert.Equal(5, issues[0].Files.Count);
        Assert.Equal(s, issues[0].Files[0].SheetId);
        Assert.NotNull(issues[0].Files[0].DrawingHash);
        Assert.Equal("P01", CdeCheckContext.Field(f.Model.Get(s)!, "revision"));
        Assert.True(f.Session.StateFor(History.Me).Dirty);
        Assert.Equal("P02", (string)((JsonObject)f.Session.Query("cde.settings", [])!)["nextRevision"]!);

        // The same again at P01: nothing changed, so it goes (a re-issue); after a change it is refused until P02.
        Result(Call("cde.issue", new JsonObject { ["sheets"] = new JsonArray(s), ["revision"] = "P01", ["status"] = "S3" }));
        Assert.Equal(2, CdeIssues.All(f.Model).Count);
        f.Wall(0, 3, 5, 3);
        var refused = (JsonObject)JsonNode.Parse(Call("cde.issue", new JsonObject { ["sheets"] = new JsonArray(s), ["revision"] = "P01" }))!;
        Assert.Contains("still P01", (string)refused["error"]!);
        var third = Result(Call("cde.issue", new JsonObject { ["sheets"] = new JsonArray(s), ["status"] = "A1", ["dxf"] = true }));
        Assert.Equal("P02", (string)third["revision"]!);
        Assert.Equal(Path.Combine(folder, "Published"), (string)third["path"]!);
        Assert.True(File.Exists(Path.Combine(folder, "Published", "PRJ-ORG-ZZ-00-DR-A-0101_A1_P02.dxf")));
        // Undo takes the record back; the files stay (they were sent).
        f.Session.Undo();
        Assert.Equal(2, CdeIssues.All(f.Model).Count);
        Assert.True(File.Exists(Path.Combine(folder, "Published", "PRJ-ORG-ZZ-00-DR-A-0101_A1_P02.pdf")));
        // A guest of a shared session cannot issue (it writes on the host's disk).
        Assert.Equal("Only the host can do that.", EngineDispatcher.AllowedRemote("cde.issue", [], "editor"));
    }

    [Fact]
    public void A_blocking_finding_refuses_the_issue_before_any_file_is_written()
    {
        var f = F();
        var s = Sheet(f, filled: false);
        var files = PackageFiles.Create();
        files.Save(f.Session, Path.Combine(_dir, "house.lapin"));
        var folder = Path.Combine(_dir, "cde");
        f.Run("cde.setFolder", new JsonObject { ["path"] = folder });
        var dispatcher = new EngineDispatcher(f.Session, files);
        var (reply, diff) = dispatcher.Handle(new JsonObject { ["id"] = 1, ["method"] = "cde.issue", ["params"] = new JsonObject { ["sheets"] = new JsonArray(s) } }.ToJsonString());
        var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
        Assert.Contains("Drawn by", (string)o["error"]!);
        Assert.Null(diff);
        Assert.False(Directory.Exists(folder));
        Assert.Empty(CdeIssues.All(f.Model));
    }
}
