using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Cost;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Protocol;
using Lapin.Platform;
using Lapin.RoofPanels;

namespace Lapin.Engine.Tests;

/// <summary>
/// The roof panels' stage S7 (docs/plan-roof-panels.md §2.1, §3): the project's own profiles beside the built-in ones (a
/// <c>LapinPanelProfile</c> each, duplicated from a built-in one and edited, a firm's <c>.lapinpanels</c> file in and out), the
/// built-in ones never changed; and the exceptions on a roof's slopes — a slope left without panels, a slope whose columns' phase
/// is fixed — stored on the roof's <c>panels</c> and followed by the layout, the takeoff, the cost, the geometry and IFC.
/// </summary>
public class RoofPanelProfileTests
{
    static JsonObject Layout(Fixture f, string roof, JsonObject? extra = null)
    {
        var q = new JsonObject { ["id"] = roof };
        if (extra != null) foreach (var (k, v) in extra) q[k] = v?.DeepClone();
        return (JsonObject)f.Session.Query("roofpanels.layout", q)!;
    }

    static string Counts(JsonNode? n) => string.Join(" ", n!.AsObject().Select(kv => $"{kv.Key}×{kv.Value}"));

    static void Use(Fixture f, string roof, string profile, string? color = null)
    {
        var a = new JsonObject { ["id"] = roof, ["profile"] = profile };
        if (color != null) a["color"] = color;
        f.Run("roofpanels.setRoofPanels", a);
    }

    static (string Entity, string Profile) Duplicate(Fixture f, string from, string? name = null)
    {
        var r = f.Run("roofpanels.duplicateProfile", new JsonObject { ["from"] = from, ["name"] = name })!;
        return ((string)r["id"]!, (string)r["profile"]!);
    }

    static JsonObject Def(Fixture f, string entity) => (JsonObject)ProjectProfiles.DefOf(f.Model.Get(entity)!).DeepClone();

    /// <summary>What a layout says, but for the profile's own name and id: the panels, quantities, takeoff, rainwater, flat sheet and cost.</summary>
    static string Essence(JsonObject l)
    {
        var o = (JsonObject)l.DeepClone();
        var name = (string)o["profile"]!["name"]!;
        o.Remove("profile");
        o.Remove("warnings");
        o.Remove("roofId");
        // The lines name the profile (its panels' material, „Bilka Clasic 35 (copy), RAL 7016”): the same but for its name.
        return o.ToJsonString().Replace(name, "<profile>", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("lhip", "bilka-balcanic", "8017")]
    [InlineData("lhip", "bilka-clasic-35", "7016")]
    [InlineData("chamfer", "bilka-retro-panel", "8019")]
    [InlineData("gable", "bilka-clasic-35", null)]
    public void A_copy_of_a_built_in_profile_lays_counts_and_prices_as_the_original(string roof, string profile, string? color)
    {
        var (f, id) = RoofPanelTakeoffTests.Make(roof, new CostModule());
        f.Run("cost.addPriceList", new JsonObject { ["template"] = PanelCost.BilkaTemplate.TemplateId });
        Use(f, id, profile, color);
        var shipped = Layout(f, id);
        var (_, copy) = Duplicate(f, profile);
        Assert.Equal($"{profile}-copy", copy);
        Use(f, id, copy, color);
        var mine = Layout(f, id);
        Assert.Equal(copy, (string)mine["profile"]!["id"]!);
        // The same panels, takeoff, rainwater, flat sheet and priced lines: the copy's article codes are the original's.
        Assert.Equal(Essence(shipped), Essence(mine));
        Assert.True((double)mine["cost"]!["subtotal"]!["RON"]! > 0);
        Assert.Equal((double)shipped["cost"]!["subtotal"]!["RON"]!, (double)mine["cost"]!["subtotal"]!["RON"]!, 6);
    }

    [Fact]
    public void A_profile_of_the_project_used_by_a_roof_lays_from_its_own_values()
    {
        var (f, id) = RoofPanelTakeoffTests.Make("gable");
        // The S1 profile of one's own (1100 mm useful, a 300 mm module, 1300 and 3100 in stock), now kept in the project.
        var def = new JsonObject
        {
            ["id"] = "my-tile", ["name"] = "My tile", ["grossWidth"] = 1150, ["usefulWidth"] = 1100, ["slopeLap"] = 100, ["module"] = 300,
            ["lengths"] = new JsonArray(1300, 3100), ["reuseOffcuts"] = true, ["colors"] = new JsonArray(new JsonObject { ["code"] = "9005", ["name"] = "RAL 9005", ["rgb"] = "#0e0e10" }),
        };
        var made = f.Run("roofpanels.createProfile", new JsonObject { ["def"] = def })!;
        Assert.Equal("my-tile", (string)made["profile"]!);
        Use(f, id, "my-tile");
        var l = Layout(f, id);
        Assert.Equal("1300×20 3100×20", Counts(l["purchase"]));
        // Its panels in its own colour, a material of their own.
        Assert.NotNull(Materials.Find(f.Model, "My tile, RAL 9005"));
        // Edited: 4600 in stock instead of 3100 (4500 = 15 modules): one panel a column; the roof follows.
        var e = (string)made["id"]!;
        var changed = Def(f, e);
        changed["lengths"] = new JsonArray(1300, 4600);
        f.Run("roofpanels.updateProfile", new JsonObject { ["id"] = e, ["def"] = changed.DeepClone() });
        Assert.Equal("4600×20", Counts(Layout(f, id)["purchase"]));
        // The project's profiles are listed beside Lapin's, with the roofs that use them.
        var list = f.Session.Query("roofpanels.projectProfiles", [])!;
        Assert.Equal(3, list["builtin"]!.AsArray().Count);
        var row = list["project"]!.AsArray().Single()!;
        Assert.Equal((1, "my-tile"), ((int)row["uses"]!, (string)row["profile"]!));
        // A profile that does not lay, or takes a built-in id, is refused with why.
        changed["usefulWidth"] = 0;
        Assert.Contains("useful width", Assert.Throws<CommandException>(() => f.Run("roofpanels.updateProfile", new JsonObject { ["id"] = e, ["def"] = changed.DeepClone() })).Message);
        changed["usefulWidth"] = 1100;
        changed["id"] = "bilka-balcanic";
        Assert.Contains("Lapin's own", Assert.Throws<CommandException>(() => f.Run("roofpanels.updateProfile", new JsonObject { ["id"] = e, ["def"] = changed.DeepClone() })).Message);
    }

    [Fact]
    public void Editing_the_copy_changes_neither_the_built_in_profile_nor_another_project()
    {
        var builtIn = PanelProfiles.Raw("bilka-clasic-35")!.ToJsonString();
        var (a, roofA) = RoofPanelTakeoffTests.Make("gable");
        var other = RoofPanelTakeoffTests.Make("gable");
        var plain = RoofPanelTakeoffTests.Make("gable");
        Use(a, roofA, "bilka-clasic-35");
        Use(other.F, other.Roof, "bilka-clasic-35");
        var before = Counts(Layout(other.F, other.Roof)["purchase"]);
        Assert.Equal("4350×20", before);

        // In project A: a second roof keeps the built-in profile, the first takes a copy whose lap is 200 mm.
        var second = (string)a.Run("architecture.addRoof", new JsonObject { ["storeyId"] = a.Storey, ["points"] = new JsonArray(new JsonArray(20, 0), new JsonArray(30, 0), new JsonArray(30, 6), new JsonArray(20, 6)) })!["id"]!;
        a.Run("architecture.updateRoof", new JsonObject { ["id"] = second, ["attrs"] = new JsonObject { ["shape"] = "gable", ["pitch"] = 30, ["overhang"] = 0.5, ["gableOverhang"] = 0.3 } });
        Use(a, second, "bilka-clasic-35");
        var (e, copy) = Duplicate(a, "bilka-clasic-35", "Clasic 35, 200 mm lap");
        var def = Def(a, e);
        Assert.Equal("Clasic 35, 200 mm lap", (string)def["name"]!);
        Assert.Equal("bilka-clasic-35", (string)def["basedOn"]!);
        def["slopeLap"] = 200;
        def["order"]!["start"] = 550;
        a.Run("roofpanels.updateProfile", new JsonObject { ["id"] = e, ["def"] = def });
        Use(a, roofA, copy);
        Assert.NotEqual(before, Counts(Layout(a, roofA)["purchase"]));
        Assert.Equal(200, (int)Layout(a, roofA)["profile"]!["slopeLap"]!);
        // The built-in profile, the roof that keeps it, the other project and a project without the copy: all as they were.
        Assert.Equal(builtIn, PanelProfiles.Raw("bilka-clasic-35")!.ToJsonString());
        Assert.Equal(150, PanelProfiles.Find("bilka-clasic-35")!.SlopeLap);
        Assert.Equal(before, Counts(Layout(a, second)["purchase"]));
        Assert.Equal(before, Counts(Layout(other.F, other.Roof)["purchase"]));
        Assert.Contains("no roof panel profile", (string)Layout(plain.F, plain.Roof, new JsonObject { ["profile"] = copy })["problem"]!);
    }

    [Fact]
    public void A_company_library_file_goes_out_and_comes_back_the_same()
    {
        var (a, roofA) = RoofPanelTakeoffTests.Make("gable");
        var (e, copy) = Duplicate(a, "bilka-balcanic", "Office Balcanic");
        var def = Def(a, e);
        def["valleyGap"] = 80;
        a.Run("roofpanels.updateProfile", new JsonObject { ["id"] = e, ["def"] = def });
        Use(a, roofA, copy);
        var file = a.Session.Query("roofpanels.exportProfiles", [])!;
        Assert.EndsWith(".lapinpanels", (string)file["file"]!);
        var text = (string)file["text"]!;
        var header = LibraryFile.HeaderOf((JsonObject)JsonNode.Parse(text)!);
        Assert.Equal(("lapin-panel-profiles", LibraryFile.Envelope), (header.Format, header.Envelope));

        // Another project: the one import dialog's preview, then the import, one undo step.
        var (b, roofB) = RoofPanelTakeoffTests.Make("gable");
        var preview = (JsonObject)b.Session.Query("library.preview", new JsonObject { ["text"] = text })!;
        Assert.Equal(("lapin-panel-profiles", 1, 0), ((string)preview["format"]!, (int)preview["new"]!, (int)preview["conflicts"]!));
        var r = (JsonObject)b.Run("library.import", new JsonObject { ["text"] = text })!;
        Assert.Equal(1, (int)r["added"]!);
        Use(b, roofB, copy);
        var la = Layout(a, roofA);
        var lb = Layout(b, roofB);
        Assert.True(la["problem"] == null && lb["problem"] == null, $"{la["problem"]} | {lb["problem"]}");
        Assert.Equal(Essence(la), Essence(lb));
        Assert.Equal(0, (int)((JsonObject)b.Session.Query("library.preview", new JsonObject { ["text"] = text })!)["new"]!);

        // Changed in the file: the same id, updated by default (its roof follows), kept beside as „-2”, or skipped.
        var changed = (JsonObject)JsonNode.Parse(text)!;
        changed["profiles"]![0]!["valleyGap"] = 70;
        var row = ((JsonObject)b.Session.Query("library.preview", new JsonObject { ["text"] = changed.ToJsonString() })!)["rows"]![0]!;
        Assert.Equal(("id", "update"), ((string)row["status"]!, (string)row["choice"]!));
        var both = (JsonObject)b.Run("roofpanels.importProfiles", new JsonObject { ["text"] = changed.ToJsonString(), ["all"] = "both" })!;
        Assert.Equal(["Office Balcanic (2)"], both["renamed"]!.AsArray().Select(x => (string)x!).ToArray());
        Assert.NotNull(ProjectProfiles.Find(b.Model, $"{copy}-2"));
        b.Session.Undo();
        Assert.Null(ProjectProfiles.Find(b.Model, $"{copy}-2"));
        Assert.Equal(1, (int)b.Run("library.import", new JsonObject { ["text"] = changed.ToJsonString() })!["updated"]!);
        Assert.Equal(70, (int)Layout(b, roofB)["profile"]!["valleyGap"]!);
        b.Session.Undo();
        var back = Layout(b, roofB);
        Assert.True(back["problem"] == null, (string?)back["problem"]);
        Assert.Equal(80, (int)back["profile"]!["valleyGap"]!);
    }

    [Fact]
    public void A_profile_in_use_is_not_deleted_and_each_command_is_one_undo_step()
    {
        var (f, id) = RoofPanelTakeoffTests.Make("gable");
        var (e, copy) = Duplicate(f, "bilka-clasic-35");
        Use(f, id, copy);
        var why = Assert.Throws<CommandException>(() => f.Run("roofpanels.deleteProfile", new JsonObject { ["id"] = e })).Message;
        Assert.Contains("choose another profile", why);
        Assert.NotNull(f.Model.Get(e));
        // Its id changed: the roof follows in the same step; undone, both are back.
        var def = Def(f, e);
        def["id"] = "office-clasic";
        f.Run("roofpanels.updateProfile", new JsonObject { ["id"] = copy, ["def"] = def });
        Assert.Equal("office-clasic", RoofPanels.RoofPanels.ChoiceOf(f.Model.Get(id)!)!.Profile);
        f.Session.Undo();
        Assert.Equal(copy, RoofPanels.RoofPanels.ChoiceOf(f.Model.Get(id)!)!.Profile);
        Assert.Equal(copy, (string)Def(f, e)["id"]!);
        // Another profile on the roof: now it goes, and comes back with one undo.
        Use(f, id, "bilka-clasic-35");
        f.Run("roofpanels.deleteProfile", new JsonObject { ["id"] = e });
        Assert.Null(f.Model.Get(e));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(e));
        // The two profile choices and the duplicate undone: the project has no profile of its own.
        f.Session.Undo();
        f.Session.Undo();
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(ProjectProfiles.Class));
    }

    [Fact]
    public void A_slope_without_panels_drops_out_of_the_layout_the_takeoff_the_cost_and_IFC()
    {
        var (f, id) = RoofPanelTakeoffTests.Make("gable", new CostModule());
        f.Run("cost.addPriceList", new JsonObject { ["template"] = PanelCost.BilkaTemplate.TemplateId });
        Use(f, id, "bilka-clasic-35", "7016");
        var whole = Layout(f, id);
        Assert.Equal("4350×20", Counts(whole["purchase"]));
        f.Run("roofpanels.setFacePanels", new JsonObject { ["id"] = id, ["face"] = "A02", ["skip"] = true });
        Assert.True((bool)f.Model.Get(id)!.Attrs["panels"]!["faces"]!["A02"]!["skip"]!);
        var half = Layout(f, id);
        Assert.Equal("4350×10", Counts(half["purchase"]));
        Assert.DoesNotContain(half["panels"]!.AsArray(), p => (string)p!["face"]! == "A02");
        Assert.All(half["panels"]!.AsArray(), p => Assert.StartsWith("A01-", (string)p!["id"]!));
        // The slope is still listed, said to be without panels; the areas and the takeoff are the other slope's.
        Assert.True((bool)half["faces"]!.AsArray().Single(x => (string)x!["id"]! == "A02")!["skip"]!);
        Assert.Equal((double)whole["totals"]!["roofArea"]! / 2, (double)half["totals"]!["roofArea"]!, 3);
        double Item(JsonObject l, string code) => (double)l["takeoff"]!["items"]!.AsArray().Single(i => (string)i!["code"]! == code)!["need"]!;
        Assert.Equal(Item(whole, "membrane") / 2, Item(half, "membrane"), 2);
        Assert.Single(half["takeoff"]!["faces"]!.AsArray());
        // The eave and verges of the slope without panels need no flashing; the ridge still does (its other side has panels).
        Assert.Equal(1, half["edges"]!.AsArray().Count(x => (string)x!["kind"]! == "eave"));
        Assert.Equal(1, half["edges"]!.AsArray().Count(x => (string)x!["kind"]! == "ridge"));
        // The rainwater system still drains both slopes.
        Assert.Equal((double)whole["drainage"]!["roofArea"]!, (double)half["drainage"]!["roofArea"]!, 6);
        Assert.True((double)half["cost"]!["subtotal"]!["RON"]! < (double)whole["cost"]!["subtotal"]!["RON"]!);
        // In 3D the slope keeps the roof's covering; in IFC its panels are gone, the covering of the slope a roof slab.
        var g = f.Session.Geometry(id)!;
        Assert.Contains(g.Parts, p => p.Key == "covering:A02");
        Assert.Equal(half["panels"]!.AsArray().Count, g.Parts.Count(p => p.Key?.StartsWith("panel:", StringComparison.Ordinal) == true));
        var s = StepFile.Read(IfcExport.Bytes(f.Session, "panels.ifc").Bytes);
        Assert.Equal(half["panels"]!.AsArray().Count, s.OfType("IFCCOVERING").Count(c => c[8] is StepEnum { Value: "ROOFING" }));
        Assert.Contains(s.OfType("IFCSLAB"), x => x[8] is StepEnum { Value: "ROOF" });
        // Another colour keeps the exception; both slopes without panels is refused, said; undone, the slope has its panels again.
        Use(f, id, "bilka-clasic-35", "8017");
        Assert.Equal("4350×10", Counts(Layout(f, id)["purchase"]));
        f.Run("roofpanels.setFacePanels", new JsonObject { ["id"] = id, ["face"] = "A01", ["skip"] = true });
        Assert.Equal("Every roof plane is left without panels.", (string)Layout(f, id)["problem"]!);
        f.Session.Undo();
        f.Session.Undo();
        f.Session.Undo();
        Assert.Equal("4350×20", Counts(Layout(f, id)["purchase"]));
        Assert.Null(f.Model.Get(id)!.Attrs["panels"]!["faces"]);
    }

    [Fact]
    public void A_fixed_phase_lays_the_slopes_columns_there()
    {
        var (f, id) = RoofPanelTakeoffTests.Make("lhip");
        Use(f, id, "bilka-balcanic");
        var free = Layout(f, id);
        var face = free["faces"]!.AsArray().First()!;
        var faceId = (string)face["id"]!;
        var chosen = (double)face["phase"]!;
        Assert.Null(face["fixedPhase"]);
        // A phase 300 mm on from the one the layout chose.
        var width = (int)free["profile"]!["usefulWidth"]!;
        var fixedAt = (int)Math.Round((chosen + 300) % width);
        f.Run("roofpanels.setFacePanels", new JsonObject { ["id"] = id, ["face"] = faceId, ["phase"] = fixedAt });
        var l = Layout(f, id);
        var mine = l["faces"]!.AsArray().Single(x => (string)x!["id"]! == faceId)!;
        Assert.Equal(fixedAt, (int)mine["fixedPhase"]!);
        Assert.Equal(fixedAt, (double)mine["phase"]!, 1);
        // Every column of the slope starts on it (x in mm along the eave), the other slopes as they were.
        foreach (var p in l["panels"]!.AsArray().Where(p => (string)p!["face"]! == faceId))
            Assert.Equal(0, ((((int)(double)p!["x"]! - fixedAt) % width) + width) % width);
        // The other slopes keep a phase of their own (the offcuts reused may pick another one for them, the roof's cheapest).
        Assert.All(l["faces"]!.AsArray().Where(x => (string)x!["id"]! != faceId), x => Assert.Null(x!["fixedPhase"]));
        // A phase from 0 to 10 000 mm only; null takes the layout's own again, in one undo step.
        Assert.Throws<CommandException>(() => f.Run("roofpanels.setFacePanels", new JsonObject { ["id"] = id, ["face"] = faceId, ["phase"] = -5 }));
        Assert.Throws<CommandException>(() => f.Run("roofpanels.setFacePanels", new JsonObject { ["id"] = id, ["face"] = "roof", ["skip"] = true }));
        f.Run("roofpanels.setFacePanels", new JsonObject { ["id"] = id, ["face"] = faceId, ["phase"] = null });
        Assert.Null(f.Model.Get(id)!.Attrs["panels"]!["faces"]);
        Assert.Equal(Counts(free["purchase"]), Counts(Layout(f, id)["purchase"]));
        f.Session.Undo();
        Assert.Equal(fixedAt, (int)f.Model.Get(id)!.Attrs["panels"]!["faces"]![faceId]!["phase"]!);
    }

    [Fact]
    public void A_project_saved_before_S7_opens_and_lays_as_it_did()
    {
        // A roof as S6 stored it: its profile and colour, nothing else; saved and opened again by this build.
        var (f, id) = RoofPanelTakeoffTests.Make("lhip");
        Use(f, id, "bilka-clasic-35", "7016");
        var stored = f.Model.Get(id)!.Attrs["panels"]!.ToJsonString();
        Assert.Equal("{\"profile\":\"bilka-clasic-35\",\"color\":\"7016\"}", stored);
        var before = Layout(f, id);
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        ms.Position = 0;
        var content = LapinPackage.Read(ms);
        var g = new Fixture(new ArchitectureModule(), new RoofPanelsModule());
        g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Assert.Equal(stored, g.Model.Get(id)!.Attrs["panels"]!.ToJsonString());
        Assert.Equal(before.ToJsonString(), Layout(g, id).ToJsonString());
        Assert.Empty(g.Model.OfClass(ProjectProfiles.Class));

        // And a project with a profile of its own and an exception keeps them through a save.
        var (e, copy) = Duplicate(g, "bilka-clasic-35");
        Use(g, id, copy);
        g.Run("roofpanels.setFacePanels", new JsonObject { ["id"] = id, ["face"] = "A02", ["skip"] = true });
        var mine = Layout(g, id);
        using var ms2 = new MemoryStream();
        ProjectPackage.Write(g.Session, ms2);
        ms2.Position = 0;
        var c2 = LapinPackage.Read(ms2);
        var h = new Fixture(new ArchitectureModule(), new RoofPanelsModule());
        h.Session.Load(c2.Entities, c2.Relations, null, c2.Classes);
        Assert.NotNull(h.Model.Get(e));
        Assert.Equal(mine.ToJsonString(), Layout(h, id).ToJsonString());
    }
}
