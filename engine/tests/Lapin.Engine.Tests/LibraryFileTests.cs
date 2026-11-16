using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Rebar;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// The one envelope and the one conflict handling of the library files (audit D13, <see cref="LibraryFile"/>, <see cref="LibraryMerge"/>):
/// every kind as the build before the envelope saved it (Fixtures/libraries/v0, written by that build) still reads and comes in;
/// what this build writes carries the header and comes back the same; a newer file is refused; and the choices for a conflict
/// (keep both as „Name (2)”, update, skip) are the same words with the same results for every kind.
/// </summary>
public sealed class LibraryFileTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StructureModule(), new RebarModule(), new StudioModule());

    static readonly string V0 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "libraries", "v0");
    static string Old(string file) => File.ReadAllText(Path.Combine(V0, file));

    static JsonObject Preview(Fixture f, string text, string? ext = null) =>
        (JsonObject)f.Session.Query("library.preview", new JsonObject { ["text"] = text, ["ext"] = ext })!;

    static JsonObject Import(Fixture f, string text, JsonObject? choices = null, string? all = null, string? ext = null) =>
        (JsonObject)f.Run("library.import", new JsonObject { ["text"] = text, ["ext"] = ext, ["choices"] = choices, ["all"] = all })!;

    static List<JsonObject> Rows(JsonObject preview) => [.. preview["rows"]!.AsArray().OfType<JsonObject>()];

    /// <summary>Every file of the build before the envelope, by the kind its JSON (or its extension) names.</summary>
    public static TheoryData<string, string> OldFiles => new()
    {
        { "office.lapinsymbols", "lapin-symbols" },
        { "walls.lapintypes", "lapin-type-pack" },
        { "types.json", "lapin-types" },
        { "office.lapinstandards", "lapin-standards" },
        { "lapin.lin", "autocad-lin" },
        { "Office (DE).lapinrules", "lapin-rules" },
        { "parameters.lapinparams", "lapin-parameters" },
        { "aliases.lapinaliases", "lapin-field-aliases" },
        { "Office block.lapintitleblock", "lapin.titleBlock" },
        { "shapes.lapinbarshapes", "lapin-bar-shapes" },
        { "Bracket.lapinpart", "lapinpart" },
        { "Shelf bracket.lapinfamily", "lapin-family" },
        { "Walls by type.lapinschedule", "lapin-schedule" },
    };

    [Theory]
    [MemberData(nameof(OldFiles))]
    public void A_file_of_the_build_before_the_envelope_reads_as_version_0_and_comes_in(string file, string format)
    {
        var f = F();
        var text = Old(file);
        var ext = Path.GetExtension(file);
        var p = Preview(f, text, ext);
        Assert.Equal(format, (string?)p["format"]);
        if (LibraryFile.Of(format).Json)
        {
            Assert.Equal(0, (int)p["header"]!["envelope"]!);
            Assert.Null((string?)p["header"]!["app"]);
        }
        Assert.NotEmpty(Rows(p));
        // A fresh project has none of it: every item new, none to choose.
        Assert.True((int)p["conflicts"]! == 0, string.Join("; ", Rows(p).Where(x => (string)x["status"]! is "name" or "id").Select(x => (string)x["key"]!)));
        var r = Import(f, text, ext: ext);
        Assert.Equal(format, (string?)r["format"]);
        Assert.True((int)r["added"]! > 0, $"{file}: nothing added");
        // Brought in again: all of it the same, nothing to choose.
        var again = Preview(f, text, ext);
        Assert.Equal(0, (int)again["conflicts"]!);
        Assert.Equal(0, (int)again["new"]!);
        // One undo step takes it all away: new again, as before.
        f.Session.Undo();
        Assert.Equal((int)p["new"]!, (int)Preview(f, text, ext)["new"]!);
    }

    [Fact]
    public void My_furniture_and_a_profile_library_of_the_build_before_read_through_the_same_envelope()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lapin-libfile-" + Guid.NewGuid().ToString("N"));
        try
        {
            var shelf = new FurnitureLibrary(folder);
            var bytes = Encoding.UTF8.GetBytes(Old("My furniture.lapinlib"));
            Assert.Equal(LibraryMerge.Match.New, Assert.Single(shelf.Preview(bytes)).Match);
            Assert.Single(shelf.Import(bytes).Added);
            Assert.Equal(LibraryMerge.Match.Same, Assert.Single(shelf.Preview(bytes)).Match);
            // Written again by this build: the envelope, and the same entry.
            var now = shelf.Export(null);
            var header = LibraryFile.HeaderOf((JsonObject)JsonNode.Parse(now)!);
            Assert.Equal((LibraryFile.Envelope, Session.AppVersion), (header.Envelope, header.App));
            Assert.Equal(LibraryMerge.Match.Same, Assert.Single(shelf.Preview(now)).Match);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        var lib = LibraryFile.Read(Old("Office steel.lapinprofiles"), LibraryFile.Of("lapin-profiles"));
        Assert.Equal(0, lib.Header.Envelope);
        var read = Lapin.Platform.ProfileLibraries.ProfileLibrary.From(lib.Root)!;
        Assert.Equal(("Office steel", 3), (read.Name, read.Profiles.Count));
        var written = LibraryFile.Write(LibraryFile.Of("lapin-profiles"), read.ToJson(), "Firm");
        Assert.Equal("Firm", LibraryFile.HeaderOf(written).Source);
        Assert.Equal(read.ToJson().ToJsonString(), Lapin.Platform.ProfileLibraries.ProfileLibrary.From(LibraryFile.Read(LibraryFile.Text(written), LibraryFile.Of("lapin-profiles")).Root)!.ToJson().ToJsonString());
    }

    /// <summary>Each kind's export, as the interface asks for it, after the old file came in.</summary>
    public static TheoryData<string, string, string> Exports => new()
    {
        { "office.lapinsymbols", "symbols.export", "ids" },
        { "walls.lapintypes", "types.export", "ids" },
        { "types.json", "architecture.exportTypes", "" },
        { "office.lapinstandards", "standards.export", "" },
        { "Office (DE).lapinrules", "rules.export", "id" },
        { "parameters.lapinparams", "params.exportDefs", "" },
        { "aliases.lapinaliases", "fields.exportAliases", "" },
        { "Office block.lapintitleblock", "titleBlock.export", "id" },
        { "shapes.lapinbarshapes", "rebar.exportShapes", "" },
        { "Bracket.lapinpart", "studio.exportPart", "id" },
        { "Shelf bracket.lapinfamily", "studio.exportFamily", "id" },
        { "Walls by type.lapinschedule", "schedule.exportTemplate", "id" },
    };

    [Theory]
    [MemberData(nameof(Exports))]
    public void What_this_build_writes_carries_the_header_and_comes_back_the_same(string file, string export, string by)
    {
        var f = F();
        LibraryFile.Clock = () => new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
        var r = Import(f, Old(file));
        var args = new JsonObject();
        if ((string?)JsonNode.Parse(Old(file))!["name"] is { } named && by != "id") args["name"] = named;
        if (by == "id") args["id"] = (string?)r["id"] ?? (string?)r["types"]?[0]?["id"];
        if (by == "ids") args["ids"] = r["ids"]?.DeepClone() ?? new JsonArray([.. r["types"]!.AsArray().Select(t => (JsonNode)(string)t!["id"]!)]);
        var o = f.Session.Query(export, args)!;
        var text = o["text"] is JsonValue ? (string)o["text"]! : o.ToJsonString();
        var root = (JsonObject)JsonNode.Parse(text)!;
        var header = LibraryFile.HeaderOf(root);
        Assert.Equal(LibraryFile.Envelope, header.Envelope);
        Assert.Equal(Session.AppVersion, header.App);
        Assert.Equal("2026-11-16T10:00:00Z", header.Created);
        Assert.Equal(LibraryFile.SourceOf(f.Model) ?? "Lapin", header.Source);
        // The header first after format and version, the payload's own keys after it.
        Assert.Equal(["format", "version", LibraryFile.HeaderKey], root.Select(kv => kv.Key).Take(3).ToArray());
        // The same project takes it back as the same: nothing new, nothing to choose.
        var p = Preview(f, text);
        Assert.Equal((0, 0), ((int)p["new"]!, (int)p["conflicts"]!));
        // The payload is the old file's, but for the header (and the version a format writes now).
        var old = (JsonObject)JsonNode.Parse(Old(file))!;
        Assert.Equal(old.Where(kv => kv.Key != "version").Select(kv => kv.Key).Order(), root.Where(kv => kv.Key is not ("version" or LibraryFile.HeaderKey)).Select(kv => kv.Key).Order());
    }

    [Theory]
    [InlineData("{\"format\":\"lapin-symbols\",\"version\":2,\"symbols\":[]}", "newer Lapin")]
    [InlineData("{\"format\":\"lapin-symbols\",\"version\":1,\"lapin\":{\"envelope\":7,\"app\":\"9.0.1\"},\"symbols\":[]}", "newer Lapin (9.0.1)")]
    [InlineData("{\"format\":\"lapin-rules\",\"version\":1}", "not a symbol library (.lapinsymbols)")]
    [InlineData("not json", "it is not JSON")]
    public void A_file_of_a_newer_lapin_or_another_kind_is_refused_with_a_sentence(string text, string why)
    {
        var e = Assert.Throws<CommandException>(() => LibraryFile.Read(text, LibraryFile.Of("lapin-symbols")));
        Assert.Contains(why, e.Message);
    }

    [Fact]
    public void An_older_version_is_brought_up_to_date_by_its_format_s_steps()
    {
        LibraryFile.Upgrade("lapin-field-aliases", 0, o => { o["aliases"] = o["list"]?.DeepClone(); o.Remove("list"); return o; });
        var doc = LibraryFile.Read("{\"format\":\"lapin-field-aliases\",\"version\":0,\"list\":[{\"name\":\"Stage\",\"sources\":[\"P.E\"]}]}", LibraryFile.Of("lapin-field-aliases"));
        Assert.True(doc.Upgraded);
        Assert.Equal(1, (int)doc.Root["version"]!);
        Assert.Equal("Stage", (string?)doc.Root["aliases"]![0]!["name"]);
        Assert.Null(doc.Root["list"]);
        // The same file in a project, through the aliases' own reader.
        var f = F();
        Assert.Equal(1, (int)Import(f, "{\"format\":\"lapin-field-aliases\",\"version\":0,\"list\":[{\"name\":\"Stage\",\"sources\":[\"P.E\"]}]}")["added"]!);
    }

    [Fact]
    public void Every_kind_is_in_the_inventory_and_the_edition_says_which_it_takes_in()
    {
        var formats = F().Session.Query("library.formats", [])!["formats"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal(LibraryFile.Formats.Count, formats.Count);
        var imports = formats.Where(x => (bool)x["imports"]!).Select(x => (string)x["format"]!).ToHashSet();
        foreach (var (_, format) in OldFiles.Select(x => ((string)x[0], (string)x[1]))) Assert.Contains(format, imports);
        Assert.Contains("autocad-pat", imports);
        // Kept by the interface or this computer, not taken into a project by the engine.
        Assert.DoesNotContain("lapin-price-list", imports);
        Assert.DoesNotContain("lapin-furniture-library", imports);
        // Every extension the firm folders keep is a kind of the inventory.
        foreach (var ext in EngineDispatcher.LibraryKinds) Assert.Contains(LibraryFile.Formats, x => x.Extension == ext);
    }

    // ------------------------------------------------------------ the one conflict handling

    [Fact]
    public void The_same_name_keeps_both_as_name_2_updates_in_place_or_skips_for_every_kind_named_by_its_name()
    {
        // Symbols, a rule pack, a title block, a part, a schedule template: the file's changed, the project's kept.
        foreach (var (file, change) in new (string, Action<JsonObject>)[]
        {
            ("office.lapinsymbols", o => o["symbols"]![0]!["def"]!["items"]![0]!["b"] = new JsonArray(5, 0)),
            ("Office (DE).lapinrules", o => o["description"] = "Changed"),
            ("Office block.lapintitleblock", o => o["template"]!["w"] = 120),
            ("Bracket.lapinpart", o => o["def"]!["params"]![0]!["default"] = 0.6),
            ("Walls by type.lapinschedule", o => o["fields"]!["columns"]![0]!["decimals"] = 3),
        })
        {
            var f = F();
            Import(f, Old(file));
            var changed = (JsonObject)JsonNode.Parse(Old(file))!;
            change(changed);
            var text = changed.ToJsonString();
            var row = Assert.Single(Rows(Preview(f, text)), x => (string)x["status"]! == "name");
            Assert.Equal("both", (string?)row["choice"]);
            Assert.Equal(["both", "update", "skip"], row["choices"]!.AsArray().Select(x => (string)x!).ToArray());
            var name = (string)row["name"]!;

            // Keep both: beside it as „Name (2)”.
            var both = Import(f, text);
            Assert.Equal([$"{name} (2)"], both["renamed"]!.AsArray().Select(x => (string)x!).ToArray());
            f.Session.Undo();
            // Skip: nothing changes.
            var skip = Import(f, text, new JsonObject { [(string)row["key"]!] = "skip" });
            Assert.Equal((1, 0, 0), ((int)skip["skipped"]!, (int)skip["added"]!, (int)skip["updated"]!));
            // Update („replace” reads the same): the project's takes the file's, the same thing now.
            var update = Import(f, text, all: "replace");
            Assert.Equal(1, (int)update["updated"]!);
            Assert.Equal("same", (string?)Rows(Preview(f, text)).Single(x => (string)x["key"]! == (string)row["key"]!)["status"]);
        }
    }

    [Fact]
    public void The_same_thing_by_its_id_is_updated_by_default_and_a_choice_it_does_not_offer_is_refused()
    {
        var f = F();
        // A shared parameter: its id.
        Import(f, Old("parameters.lapinparams"));
        var p = (JsonObject)JsonNode.Parse(Old("parameters.lapinparams"))!;
        p["parameters"]![0]!["description"] = "EI minutes";
        var row = Rows(Preview(f, p.ToJsonString())).Single();
        Assert.Equal(("id", "update"), ((string)row["status"]!, (string?)row["choice"]));
        Assert.Equal(1, (int)Import(f, p.ToJsonString())["updated"]!);
        f.Session.Undo();
        var both = Import(f, p.ToJsonString(), all: "both");
        Assert.Equal(["FireRating (2)"], both["renamed"]!.AsArray().Select(x => (string)x!).ToArray());

        // A bar shape: its code is its identity, no keeping both.
        Import(f, Old("shapes.lapinbarshapes"));
        var s = (JsonObject)JsonNode.Parse(Old("shapes.lapinbarshapes"))!;
        s["shapes"]![0]!["label"] = "U bar, long";
        var shape = Rows(Preview(f, s.ToJsonString())).Single();
        Assert.Equal(["update", "skip"], shape["choices"]!.AsArray().Select(x => (string)x!).ToArray());
        Assert.Contains("not a choice", Assert.Throws<CommandException>(() => Import(f, s.ToJsonString(), new JsonObject { ["21"] = "both" })).Message);
        // „All: keep both” passes over what does not offer it: its default.
        Assert.Equal(1, (int)Import(f, s.ToJsonString(), all: "both")["updated"]!);

        // A drafting standard's style, a .lin's line type: the same id, updated by default.
        var g = F();
        Import(g, Old("lapin.lin"), ext: ".lin");
        var lin = Old("lapin.lin").Replace("A,6,-2,1,-2", "A,8,-2,1,-2");
        var lt = Rows(Preview(g, lin, ".lin")).Single();
        Assert.Equal(("id", "update"), ((string)lt["status"]!, (string?)lt["choice"]));
        Assert.Equal(["Fence (2)"], Import(g, lin, all: "both", ext: ".lin")["renamed"]!.AsArray().Select(x => (string)x!).ToArray());
    }

    [Fact]
    public void A_type_pack_s_material_keeps_the_project_s_by_default_and_its_type_is_kept_both()
    {
        var f = F();
        Import(f, Old("walls.lapintypes"));
        var pack = (JsonObject)JsonNode.Parse(Old("walls.lapintypes"))!;
        foreach (var r in pack["resources"]!.AsArray().OfType<JsonObject>().Where(x => (string?)x["class"] == IfcClass.Material && (string?)x["name"] == "Brick"))
            r["attrs"]!["color"] = "#aa0000";
        foreach (var t in pack["types"]!.AsArray().OfType<JsonObject>()) t["attrs"]!["mark"] = "EX1";
        var rows = Rows(Preview(f, pack.ToJsonString()));
        Assert.Equal("skip", (string?)rows.Single(x => (string)x["key"]! == "material:Brick")["choice"]);
        Assert.Equal("both", (string?)rows.Single(x => ((string)x["key"]!).StartsWith("type:"))["choice"]);
        var r1 = Import(f, pack.ToJsonString());
        Assert.Equal(["WT1 Exterior (2)"], r1["renamed"]!.AsArray().Select(x => (string)x!).ToArray());
        Assert.Equal(1, (int)r1["materialsKept"]!);
        f.Session.Undo();
        // The same words for all of it: update the type and the material in place.
        var r2 = Import(f, pack.ToJsonString(), all: "update");
        Assert.Equal((1, 1), ((int)r2["updated"]!, (int)r2["materialsUpdated"]!));
        Assert.Equal("#aa0000", f.Model.OfClass(IfcClass.Material).Single(m => m.Name == "Brick").Str("color"));
    }

    [Fact]
    public void My_furniture_s_entry_is_the_same_by_its_id_and_another_of_its_name_is_kept_both()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lapin-libfile-" + Guid.NewGuid().ToString("N"));
        try
        {
            var shelf = new FurnitureLibrary(folder);
            var old = Old("My furniture.lapinlib");
            shelf.Import(Encoding.UTF8.GetBytes(old));
            var changed = (JsonObject)JsonNode.Parse(old)!;
            changed["items"]![0]!["price"] = 120;
            var bytes = Encoding.UTF8.GetBytes(changed.ToJsonString());
            var row = Assert.Single(shelf.Preview(bytes));
            Assert.Equal((LibraryMerge.Match.Id, "update"), (row.Match, row.Choice));
            Assert.Equal(1, (int)shelf.Import(bytes, null, null)["updated"]!);
            Assert.Equal(120, (double)shelf.Get("chair-1")["price"]!);
            // Another entry of the same name: kept both, „Chair (2)”.
            changed["items"]![0]!["id"] = "chair-2";
            var other = Encoding.UTF8.GetBytes(changed.ToJsonString());
            Assert.Equal(LibraryMerge.Match.Name, Assert.Single(shelf.Preview(other)).Match);
            Assert.Equal(["Chair (2)"], shelf.Import(other, null, null)["renamed"]!.AsArray().Select(x => (string)x!).ToArray());
            // The command without choices does as it always did: one of its id made otherwise comes in under a new id, its name kept.
            changed["items"]![0]!["id"] = "chair-1";
            changed["items"]![0]!["price"] = 99;
            var (added, _) = shelf.Import(Encoding.UTF8.GetBytes(changed.ToJsonString()));
            Assert.Equal("Chair", (string?)shelf.Get(Assert.Single(added))["name"]);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public void The_one_decision_reads_the_same_words_everywhere()
    {
        var byName = new LibraryMerge.Row("A", "symbol", "A", LibraryMerge.Match.Name);
        var byId = new LibraryMerge.Row("B", "lineType", "B", LibraryMerge.Match.Id);
        Assert.Equal(LibraryMerge.Both, LibraryMerge.Decide(byName, null, null));
        Assert.Equal(LibraryMerge.Update, LibraryMerge.Decide(byId, null, null));
        Assert.Equal(LibraryMerge.Update, LibraryMerge.Decide(byName, new JsonObject { ["A"] = "replace" }, null));
        Assert.Equal(LibraryMerge.Skip, LibraryMerge.Decide(byId, new JsonObject { ["B"] = "keep" }, null));
        Assert.Equal(LibraryMerge.Skip, LibraryMerge.Decide(byName, null, "skip"));
        // A command's own default from before stands when nothing is chosen.
        Assert.Equal(LibraryMerge.Skip, LibraryMerge.Decide(byId, null, null, LibraryMerge.Skip));
        Assert.Equal(LibraryMerge.Add, LibraryMerge.Decide(byName with { Match = LibraryMerge.Match.New }, new JsonObject { ["A"] = "skip" }, null));
        Assert.Equal(LibraryMerge.Same, LibraryMerge.Decide(byName with { Match = LibraryMerge.Match.Same }, null, "update"));
        Assert.Equal("Wall (3)", LibraryMerge.FreeName(n => n == "Wall (2)", "Wall"));
        // A material's choices keep their words: „skip” is „keep”.
        Assert.Equal(LibraryMerge.Keep, LibraryMerge.Choices(new JsonObject { ["Brick"] = "skip" }, LibraryMerge.MaterialChoices)["Brick"]);
    }
}
