using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Project templates (issues #9 and #20, decision 470): File → Save as template… keeps the project's set-up (types, materials,
/// units and standards, storeys, views and sheets) and no model; a new project made from it has that set-up, no element and
/// GlobalIds of its own; the templates live as .lapin files in the user's folder.
/// </summary>
public sealed class ProjectTemplateTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "lapin-templates-" + Guid.NewGuid().ToString("N"));

    public void Dispose() { try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

    static JsonNode? Result(byte[] frame)
    {
        var o = JsonNode.Parse(BinaryFrame.HeaderText(frame))!;
        if (o["error"] is { } error) throw new InvalidOperationException(error.ToJsonString());
        return o["result"];
    }

    static JsonNode? Call(EngineDispatcher d, string method, JsonObject? p = null) =>
        Result(d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p ?? [] }.ToJsonString()).Reply);

    static string? Error(EngineDispatcher d, string method, JsonObject? p = null) =>
        JsonNode.Parse(BinaryFrame.HeaderText(d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p ?? [] }.ToJsonString()).Reply))!["error"] switch
        {
            JsonObject o => (string?)o["message"],
            var e => (string?)e,
        };

    EngineDispatcher Dispatcher(Fixture f) => new(f.Session, PackageFiles.Create()) { Templates = new TemplateLibrary(_dir) };

    static string? Units(IModelView m) => (string?)Spatial.Project(m)!.Attrs["drafting"]?["units"];

    /// <summary>A project set up as an office would: its own wall type, centimetres, an extra storey, a sheet; then a wall drawn.</summary>
    static (Fixture F, string Type) SetUp()
    {
        var f = new Fixture(new ArchitectureModule());
        var type = f.Type("Office brick 30", ("Brick", 0.25, true), ("Plaster", 0.02, false));
        f.Run("standards.options", new JsonObject { ["units"] = "cm" });
        f.Run("storey.create", new JsonObject { ["name"] = "Attic" });
        f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" });
        f.Run("project.setInfo", new JsonObject { ["name"] = "Pop house", ["client"] = "Ion Pop", ["designer"] = "Ciuntuc BIM Studio" });
        f.Wall(0, 0, 5, 0, type);
        f.Wall(5, 0, 5, 4, type);
        return (f, type);
    }

    [Fact]
    public void A_template_saved_from_a_project_starts_a_new_project_with_its_types_and_units_and_no_elements()
    {
        var (f, type) = SetUp();
        var before = f.Model.Entities.Select(e => e.Id).Concat(f.Model.Relations.Select(r => r.Id)).ToHashSet();
        var storeys = Spatial.Storeys(f.Model).Select(s => s.Name).ToList();
        var d = Dispatcher(f);

        Assert.Equal("Office", (string?)Call(d, "templates.save", new JsonObject { ["name"] = "Office" })!["id"]);
        Assert.True(File.Exists(Path.Combine(_dir, "Office.lapin")));
        // The project saved as a template is still the project it was: nothing changed, nothing to save because of it.
        Assert.Equal(2, f.Model.OfClass(IfcClass.Wall).Count());

        // File → New without the template: the defaults, no wall type of the office's.
        Call(d, "project.new");
        Assert.DoesNotContain(f.Model.OfClass(IfcClass.WallType), t => t.Name == "Office brick 30");
        Assert.NotEqual("cm", Units(f.Model));

        Call(d, "project.newFromTemplate", new JsonObject { ["id"] = "Office" });
        var m = f.Model;
        // The office's set-up: its wall type with its layers, its units, its storeys, its sheet.
        var wallType = Assert.Single(m.OfClass(IfcClass.WallType), t => t.Name == "Office brick 30");
        var layers = Layers.Read(m.RelatingOf(wallType.Id, IfcClass.RelAssociatesMaterial)!);
        Assert.Equal([0.25, 0.02], layers.Select(l => l.Thickness));
        Assert.All(layers, l => Assert.NotNull(m.Get(l.Material)));
        Assert.Equal("cm", Units(m));
        Assert.Equal(storeys, Spatial.Storeys(m).Select(s => s.Name).ToList());
        Assert.Single(m.OfClass(Sheets.Class), s => s.Name == "Plans");
        // No model: no wall, nothing on a storey, no layer usage left behind.
        Assert.Empty(m.OfClass(IfcClass.Wall));
        Assert.Empty(m.OfClass(IfcClass.MaterialLayerSetUsage));
        Assert.DoesNotContain(m.Relations, r => r.Class is IfcClass.RelContainedInSpatialStructure or IfcClass.RelDefinesByType);
        // GlobalIds of its own: none the template's project had, and every relationship between what is there.
        Assert.DoesNotContain(m.Entities, e => before.Contains(e.Id));
        Assert.DoesNotContain(m.Relations, r => before.Contains(r.Id));
        Assert.NotEqual(type, wallType.Id);
        Assert.All(m.Relations, r => { Assert.NotNull(m.Get(r.Relating)); Assert.All(r.Related, id => Assert.NotNull(m.Get(id))); });
        // A storey's plan names its storey by the new id.
        Assert.All(m.OfClass(Views.Class).Where(v => v.Str("storeyId") != null), v => Assert.NotNull(m.Get(v.Str("storeyId")!)));
        // The office's information stays, the job's goes, and the project has its default name.
        var project = Spatial.Project(m)!;
        Assert.Equal("Project", project.Name);
        Assert.Equal("Ciuntuc BIM Studio", (string?)project.Attrs["info"]?["designer"]);
        Assert.Null(project.Attrs["info"]?["client"]);
        // A new project: no file, nothing to undo, nothing to save.
        var state = f.Session.Snapshot().State;
        Assert.Null(state.Path);
        Assert.False(state.Dirty);
        Assert.False(state.CanUndo);
        // The type draws walls as any.
        var wall = f.Wall(0, 0, 3, 0, wallType.Id);
        Assert.Equal(wallType.Id, m.RelatingOf(wall, IfcClass.RelDefinesByType)!.Id);
        Assert.NotNull(f.Session.Geometry(wall));
    }

    [Fact]
    public void Two_projects_from_one_template_share_no_GlobalId()
    {
        var (f, _) = SetUp();
        var d = Dispatcher(f);
        Call(d, "templates.save", new JsonObject { ["name"] = "Office" });
        Call(d, "project.newFromTemplate", new JsonObject { ["id"] = "Office" });
        var first = f.Model.Entities.Select(e => e.Id).ToHashSet();
        Call(d, "project.newFromTemplate", new JsonObject { ["id"] = "Office" });
        Assert.DoesNotContain(f.Model.Entities, e => first.Contains(e.Id));
        Assert.Equal(first.Count, f.Model.Entities.Count());
    }

    [Fact]
    public void Any_project_file_serves_as_a_template_and_the_browser_keeps_templates_as_bytes()
    {
        var (f, _) = SetUp();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        // The browser: no library here, the bytes go to the page.
        Assert.Contains("desktop", Error(d, "templates.save", new JsonObject { ["name"] = "Office" }));
        var made = (string)Call(d, "template.make")!["data"]!;
        var project = (string)Call(d, "project.saveBytes", new JsonObject { ["name"] = "house" })!["data"]!;
        foreach (var data in new[] { made, project })
        {
            Call(d, "project.newFromTemplate", new JsonObject { ["data"] = data });
            Assert.Single(f.Model.OfClass(IfcClass.WallType), t => t.Name == "Office brick 30");
            Assert.Empty(f.Model.OfClass(IfcClass.Wall));
            Assert.Equal("cm", Units(f.Model));
        }
        Assert.Contains("not a Lapin project", Error(d, "project.newFromTemplate", new JsonObject { ["data"] = Convert.ToBase64String("nonsense"u8.ToArray()) }));
        // A file made into a template in the browser's library leaves the open project as it is.
        f.Wall(0, 0, 2, 0);
        var fromFile = (string)Call(d, "template.make", new JsonObject { ["data"] = project })!["data"]!;
        Assert.Single(f.Model.OfClass(IfcClass.Wall));
        Assert.DoesNotContain(TemplateLibrary.Read(Convert.FromBase64String(fromFile)).Entities, e => e.Class == IfcClass.Wall);
    }

    [Fact]
    public void The_library_lists_keeps_imports_exports_renames_and_deletes_templates()
    {
        var (f, _) = SetUp();
        var d = Dispatcher(f);
        Assert.Empty(Call(d, "templates.list")!.AsArray());
        Call(d, "templates.save", new JsonObject { ["name"] = "Office: houses/2026" });
        var list = Call(d, "templates.list")!.AsArray();
        var id = (string)Assert.Single(list)!["id"]!;
        Assert.Equal("Office_ houses_2026", id);
        // One of that name stays unless replacing it is asked.
        Assert.True((bool)Call(d, "templates.save", new JsonObject { ["name"] = "Office: houses/2026" })!["exists"]!);
        Assert.Equal(id, (string?)Call(d, "templates.save", new JsonObject { ["name"] = "Office: houses/2026", ["overwrite"] = true })!["id"]);

        // Exported to a file, imported back under another name: the whole project saved as a file works too, its model left out.
        var exported = Path.Combine(_dir, "..", Path.GetFileName(_dir) + "-export.lapin");
        try
        {
            Call(d, "templates.export", new JsonObject { ["id"] = id, ["path"] = exported });
            Assert.Equal("Shared", (string?)Call(d, "templates.import", new JsonObject { ["path"] = exported, ["name"] = "Shared" })!["id"]);
            var saved = (string)Call(d, "project.saveBytes", new JsonObject { ["name"] = "house" })!["data"]!;
            Assert.Equal("From a project", (string?)Call(d, "templates.import", new JsonObject { ["data"] = saved, ["name"] = "From a project" })!["id"]);
            var content = TemplateLibrary.Read(File.ReadAllBytes(Path.Combine(_dir, "From a project.lapin")));
            Assert.DoesNotContain(content.Entities, e => e.Class == IfcClass.Wall);
            Assert.Contains(content.Entities, e => e.Name == "Office brick 30");
        }
        finally { File.Delete(exported); }

        Assert.Equal("Houses", (string?)Call(d, "templates.rename", new JsonObject { ["id"] = "Shared", ["name"] = "Houses" })!["id"]);
        Call(d, "templates.delete", new JsonObject { ["id"] = id });
        Assert.Equal(["From a project", "Houses"], Call(d, "templates.list")!.AsArray().Select(x => (string)x!["name"]!));
        Assert.Contains("no template", Error(d, "project.newFromTemplate", new JsonObject { ["id"] = id }));
        Assert.Contains("name", Error(d, "templates.save", new JsonObject { ["name"] = "  " }));
    }

    [Fact]
    public void A_template_keeps_what_its_types_use_and_leaves_out_the_projects_own_content()
    {
        var (f, _) = SetUp();
        f.Run("remarks.create", new JsonObject { ["title"] = "Check the lintel" });
        var (es, rs) = f.Session.TemplateContent();
        var kept = es.Select(e => e.Id).ToHashSet();
        Assert.DoesNotContain(es, e => e.Class == Lapin.Platform.Cde.Remarks.Remarks.Class);
        // The brick and the plaster the type's layers are made of.
        Assert.Contains(es, e => e.Class == IfcClass.Material && e.Name == "Brick");
        Assert.Contains(es, e => e.Class == IfcClass.Material && e.Name == "Plaster");
        Assert.All(rs, r => { Assert.Contains(r.Relating, kept); Assert.All(r.Related, id => Assert.Contains(id, kept)); });
    }

    [Fact]
    public void Renewing_ids_follows_them_in_values_keys_and_texts_that_hold_them()
    {
        var a = GlobalId.New();
        var b = GlobalId.New();
        var es = new[]
        {
            new Entity(a, IfcClass.BuildingStorey, "Ground floor", []),
            new Entity(b, "LapinView", "Plan", new JsonObject { ["storeyId"] = a, ["of"] = "plan:" + a, ["by"] = new JsonObject { [a] = 1 }, ["kept"] = "standard" }),
        };
        var (outE, _) = ProjectTemplates.Renew(es, []);
        var storey = outE[0].Id;
        Assert.NotEqual(a, storey);
        Assert.True(GlobalId.IsValid(storey));
        var view = outE[1].Attrs;
        Assert.Equal(storey, (string?)view["storeyId"]);
        Assert.Equal("plan:" + storey, (string?)view["of"]);
        Assert.Equal(1, (int?)view["by"]?[storey]);
        Assert.Equal("standard", (string?)view["kept"]);
    }
}
