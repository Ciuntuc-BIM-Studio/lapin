using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Publish sets (forum topic 225): named choices of sheets and views kept on the project, each change one undo step;
/// a deleted sheet or view drops out as missing; a view prints on a page of its own; an export writes each PDF and DXF,
/// the one PDF for all, into a folder or as one zip, through the protocol.
/// </summary>
public sealed class PublishSetTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-publish-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StructureModule());

    static (string Sheet, string Plan) SheetWithPlan(Fixture f, string number, string name)
    {
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = name, ["number"] = number, ["paper"] = "A3" })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = name, ["scale"] = 100 })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = plan, ["x"] = 150, ["y"] = 120 });
        return (s, plan);
    }

    static JsonArray Sets(Fixture f) => (JsonArray)f.Session.Query("publish.sets", [])!;

    [Fact]
    public void A_set_is_kept_on_the_project_changed_in_place_and_each_change_is_one_undo_step()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var (a, _) = SheetWithPlan(f, "A-101", "Ground floor");
        var (b, _) = SheetWithPlan(f, "A-102", "Roof");
        var id = (string)f.Run("publish.setSet", new JsonObject
        {
            ["name"] = "Building permit", ["sheets"] = new JsonArray(b, a), ["views"] = new JsonArray("plan:" + f.Storey),
            ["formats"] = new JsonObject { ["pdf"] = true, ["combined"] = true, ["dxf"] = false },
        })!["id"]!;
        var set = (JsonObject)Sets(f).Single()!;
        Assert.Equal("Building permit", (string)set["name"]!);
        // The order chosen is kept.
        Assert.Equal([b, a], ((JsonArray)set["sheets"]!).Select(x => (string)x!));
        Assert.Equal(["plan:" + f.Storey], ((JsonArray)set["views"]!).Select(x => (string)x!));
        Assert.True((bool)set["formats"]!["combined"]!);

        // Renamed: the sheets and formats not given stay.
        f.Run("publish.setSet", new JsonObject { ["id"] = id, ["name"] = "DTAC" });
        set = (JsonObject)Sets(f).Single()!;
        Assert.Equal("DTAC", (string)set["name"]!);
        Assert.Equal(2, ((JsonArray)set["sheets"]!).Count);
        Assert.True((bool)set["formats"]!["combined"]!);
        f.Session.Undo();
        Assert.Equal("Building permit", (string)Sets(f).Single()!["name"]!);

        // A second set may not take the same name; an unknown sheet is refused; removing is one step too.
        Assert.Throws<CommandException>(() => f.Run("publish.setSet", new JsonObject { ["name"] = "building PERMIT" }));
        Assert.Throws<CommandException>(() => f.Run("publish.setSet", new JsonObject { ["name"] = "Other", ["sheets"] = new JsonArray("nope") }));
        Assert.Throws<CommandException>(() => f.Run("publish.setSet", new JsonObject { ["name"] = " " }));
        f.Run("publish.removeSet", new JsonObject { ["id"] = id });
        Assert.Empty(Sets(f));
        f.Session.Undo();
        Assert.Single(Sets(f));
    }

    [Fact]
    public void A_sheet_deleted_since_drops_out_of_the_set_as_missing()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var (a, _) = SheetWithPlan(f, "A-101", "Ground floor");
        var (b, _) = SheetWithPlan(f, "A-102", "Roof");
        var id = (string)f.Run("publish.setSet", new JsonObject { ["name"] = "Set", ["sheets"] = new JsonArray(a, b) })!["id"]!;
        f.Run("element.delete", new JsonObject { ["id"] = b });
        var set = (JsonObject)Sets(f).Single()!;
        Assert.Equal([a], ((JsonArray)set["sheets"]!).Select(x => (string)x!));
        Assert.Equal(1, (int)set["missing"]!);
        var (sheets, views, _, name, missing) = PublishSets.Request(f.Model, new JsonObject { ["set"] = id });
        Assert.Single(sheets);
        Assert.Empty(views);
        Assert.Equal("Set", name);
        Assert.Equal(1, missing);
    }

    [Fact]
    public void A_view_prints_on_a_page_of_its_own_as_large_as_its_drawing_at_its_scale()
    {
        var f = F();
        f.Wall(0, 0, 10, 0);
        f.Wall(10, 0, 10, 6);
        var view = Views.Find(f.Model, "plan:" + f.Storey)!;
        var page = ViewPages.Page(f.Session.Reader, view);
        // 10 m at 1:100 is 100 mm, with a 10 mm margin each side; the walls' thickness takes a little more.
        Assert.InRange(page.W, 120, 125);
        Assert.InRange(page.H, 60 + 2 * ViewPages.Margin + ViewPages.TitleBand - 1, 60 + 2 * ViewPages.Margin + ViewPages.TitleBand + 5);
        // Everything drawn lies on the paper, within the margin.
        foreach (var (p, q) in page.Pens.SelectMany(p => p.Segments))
            foreach (var v in new[] { p, q })
            {
                Assert.InRange(v.X, ViewPages.Margin - 1e-3, page.W - ViewPages.Margin + 1e-3);
                Assert.InRange(v.Y, ViewPages.Margin - 1e-3, page.H - ViewPages.Margin - ViewPages.TitleBand + 1e-3);
            }
        // Its name and scale under it.
        Assert.Contains(page.Pens.SelectMany(p => p.Texts), t => t.Text.Contains("1:100"));
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(ViewPages.Pdf(f.Session.Reader, [view]), 0, 4));
        // A view with nothing drawn is refused with its name.
        var empty = F();
        var e = Assert.Throws<CommandException>(() => ViewPages.Page(empty.Session.Reader, Views.Find(empty.Model, "plan:" + empty.Storey)!));
        Assert.Contains("draws nothing", e.Message);
    }

    [Fact]
    public void File_names_carry_the_sheet_number_and_name_and_never_clash()
    {
        Assert.Equal("A_B_C", PublishSets.FileStem("A/B:C"));
        Assert.Equal("Untitled", PublishSets.FileStem(" .. "));
        var f = F();
        f.Wall(0, 0, 5, 0);
        var (a, plan) = SheetWithPlan(f, "A-101", "Ground floor");
        var (b, _) = SheetWithPlan(f, "A-102", "Ground floor");
        var outs = PublishSets.Files(f.Session.Reader, PublishSets.SheetsOf(f.Model, [a, b]), PublishSets.ViewsOf(f.Model, [plan]), new PublishSets.Formats(true, true, true), "Permit");
        Assert.Equal(["A-101 Ground floor.pdf", "A-101 Ground floor.dxf", "A-102 Ground floor.pdf", "A-102 Ground floor.dxf", "Ground floor.pdf", "Ground floor.dxf", "Permit.pdf"], outs.Select(o => o.Name));
        // The one PDF for all has a page for each sheet and view.
        var combined = Encoding.Latin1.GetString(outs[^1].Bytes);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(combined, @"/Type\s*/Page[^s]").Count);
        Assert.Throws<CommandException>(() => PublishSets.Files(f.Session.Reader, [], [], PublishSets.Formats.Default, "x"));
        Assert.Throws<CommandException>(() => PublishSets.Files(f.Session.Reader, PublishSets.SheetsOf(f.Model, [a]), [], new PublishSets.Formats(false, false, false), "x"));
    }

    [Fact]
    public void An_export_through_the_protocol_writes_a_folder_or_hands_back_one_zip()
    {
        var f = F();
        f.Wall(0, 0, 5, 0);
        var (a, plan) = SheetWithPlan(f, "A-101", "Ground floor");
        var set = (string)f.Run("publish.setSet", new JsonObject
        {
            ["name"] = "Erection plans", ["sheets"] = new JsonArray(a), ["views"] = new JsonArray(plan),
            ["formats"] = new JsonObject { ["pdf"] = true, ["combined"] = false, ["dxf"] = true },
        })!["id"]!;
        var dispatcher = new EngineDispatcher(f.Session, PackageFiles.Create());
        JsonObject Call(JsonObject p)
        {
            var (reply, _) = dispatcher.Handle(new JsonObject { ["id"] = 1, ["method"] = "export.publish", ["params"] = p }.ToJsonString());
            var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
            if (o["error"] is { } err) throw new InvalidOperationException((string)err!);
            return (JsonObject)o["result"]!;
        }

        var folder = Path.Combine(_dir, "out");
        var r = Call(new JsonObject { ["set"] = set, ["folder"] = folder });
        Assert.Equal(["A-101 Ground floor.dxf", "A-101 Ground floor.pdf", "Ground floor.dxf", "Ground floor.pdf"], Directory.GetFiles(folder).Select(Path.GetFileName).Order());
        Assert.Equal(4, ((JsonArray)r["written"]!).Count);

        // Without a folder: one zip, named after the set, with the formats given for this export only.
        var z = Call(new JsonObject { ["set"] = set, ["formats"] = new JsonObject { ["dxf"] = false } });
        Assert.Equal("Erection plans.zip", (string)z["name"]!);
        using (var zip = new ZipArchive(new MemoryStream(Convert.FromBase64String((string)z["data"]!))))
            Assert.Equal(["A-101 Ground floor.pdf", "Ground floor.pdf"], zip.Entries.Select(e => e.FullName));
        Assert.True((bool)((JsonObject)Sets(f).Single()!)["formats"]!["dxf"]!);

        // One file alone comes as itself, unless a zip is asked.
        var one = Call(new JsonObject { ["sheets"] = new JsonArray(a), ["formats"] = new JsonObject { ["pdf"] = true } });
        Assert.Equal("A-101 Ground floor.pdf", (string)one["name"]!);
        var zipped = Call(new JsonObject { ["sheets"] = new JsonArray(a), ["zip"] = true, ["name"] = "Mine" });
        Assert.Equal("Mine.zip", (string)zipped["name"]!);

        // A guest may export (as export.pdf), never to a path on the host's disk.
        Assert.Null(EngineDispatcher.AllowedRemote("export.publish", new JsonObject { ["set"] = set }, "viewer"));
        Assert.NotNull(EngineDispatcher.AllowedRemote("export.publish", new JsonObject { ["set"] = set, ["folder"] = folder }, "editor"));
    }
}
