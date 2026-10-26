using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Linked files (Manage → Links): read-only, shown or hidden, halftone, unlinked, converted to native, reloaded.</summary>
public class LinkTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Plan(Fixture f, string? storey = null) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey ?? f.Storey })!["id"]!;

    /// <summary>A small IFC: a project with one wall, written by Lapin itself.</summary>
    static byte[] SmallIfc()
    {
        var f = F();
        f.Wall(0, 0, 4, 0);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "small.ifc", new IfcExportOptions("IFC4", "group", null));
        return ms.ToArray();
    }

    static JsonArray Pens(Fixture f, string view) => ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!)["pens"]!.AsArray();

    [Fact]
    public void A_linked_ifc_is_drawn_but_not_edited_until_converted_or_unlinked()
    {
        var f = F();
        var r = IfcImport.Link(f.Session, SmallIfc(), "small.ifc", null, null, out _);
        Assert.NotNull(r.LinkId);
        var link = Links.Require(f.Model, r.LinkId);
        Assert.Equal("ifc", link.Str("kind"));
        Assert.Equal("small", link.Name);
        var walls = f.Model.OfClass(IfcClass.Wall).ToList();
        var wall = Assert.Single(walls);
        Assert.Equal(link.Id, wall.Str(Links.Key));
        // Listed with its count; drawn in the plan of the storey it brought.
        var listed = f.Session.Query("link.list", new JsonObject())!.AsArray().OfType<JsonObject>().Single();
        Assert.Equal(1, (int)listed["elements"]!);
        Assert.True((bool)listed["visible"]!);
        var storey = Spatial.StoreyOf(f.Model, wall.Id)!;
        var plan = Plan(f, storey.Id);
        Assert.Contains(Pens(f, plan).OfType<JsonObject>(), p => (string?)p["layer"] == "CUT");
        // Read-only: the wall and its storey refuse edits; so does deleting.
        var ex = Assert.Throws<CommandException>(() => f.Run("architecture.updateWall", new JsonObject { ["id"] = wall.Id, ["height"] = 2 }));
        Assert.Contains("link", ex.Message);
        Assert.Throws<CommandException>(() => f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(wall.Id) }));
        // A storey the link made is its own too (one the file matched to the project's stays the project's).
        if (storey.Str(Links.Key) != null) Assert.Throws<CommandException>(() => f.Run("storey.update", new JsonObject { ["id"] = storey.Id, ["height"] = 4 }));
        // Hidden: out of the drawing. Halftone: grey lines.
        f.Run("link.update", new JsonObject { ["id"] = link.Id, ["visible"] = false });
        Assert.DoesNotContain(Pens(f, plan).OfType<JsonObject>(), p => (string?)p["layer"] == "CUT");
        f.Run("link.update", new JsonObject { ["id"] = link.Id, ["visible"] = true, ["halftone"] = true });
        var cut = Pens(f, plan).OfType<JsonObject>().Where(p => (string?)p["layer"] == "CUT").ToList();
        Assert.NotEmpty(cut);
        Assert.All(cut, p => Assert.Equal(Links.Halftone, (string?)p["stroke"]));
        // Converted: the wall is the project's own, editable; the link is gone.
        f.Run("link.convert", new JsonObject { ["id"] = link.Id });
        Assert.Null(f.Model.Get(link.Id));
        Assert.Null(f.Model.Get(wall.Id)!.Str(Links.Key));
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall.Id, ["height"] = 2 });
        Assert.Equal(2, f.Model.Get(wall.Id)!.Num("height"));
        // Linked again, then unlinked: the wall goes, its storey too when nothing else stands on it.
        var r2 = IfcImport.Link(f.Session, SmallIfc(), "small.ifc", null, null, out _);
        var link2 = Links.Require(f.Model, r2.LinkId);
        var storeys = f.Model.OfClass(IfcClass.BuildingStorey).Count();
        f.Run("link.unlink", new JsonObject { ["id"] = link2.Id });
        Assert.Null(f.Model.Get(link2.Id));
        Assert.Empty(Links.ElementsOf(f.Model, link2.Id));
        Assert.True(f.Model.OfClass(IfcClass.BuildingStorey).Count() <= storeys);
        // One undo step brings the link back.
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(link2.Id));
    }

    [Fact]
    public void A_reloaded_link_takes_what_its_file_holds_now()
    {
        var f = F();
        var dir = Directory.CreateTempSubdirectory("lapin-link");
        var path = Path.Combine(dir.FullName, "small.ifc");
        File.WriteAllBytes(path, SmallIfc());
        var r = IfcImport.Link(f.Session, File.ReadAllBytes(path), "small.ifc", path, null, out _);
        var link = Links.Require(f.Model, r.LinkId);
        Assert.Equal(path, link.Str("path"));
        f.Run("link.update", new JsonObject { ["id"] = link.Id, ["halftone"] = true });
        var wallBefore = f.Model.OfClass(IfcClass.Wall).Single().Id;
        // The file changes: two walls now.
        var g = F(); g.Wall(0, 0, 4, 0); g.Wall(0, 3, 4, 3);
        using (var ms = new MemoryStream()) { IfcExport.Write(g.Session, ms, "small.ifc", new IfcExportOptions("IFC4", "group", null)); File.WriteAllBytes(path, ms.ToArray()); }
        IfcImport.Link(f.Session, File.ReadAllBytes(path), "small.ifc", path, null, out _, link.Id);
        var walls = f.Model.OfClass(IfcClass.Wall).ToList();
        Assert.Equal(2, walls.Count);
        Assert.All(walls, w => Assert.Equal(link.Id, w.Str(Links.Key)));
        Assert.DoesNotContain(walls, w => w.Id == wallBefore);
        Assert.True(Links.IsHalftone(f.Model.Get(link.Id)!)); // the link's settings stay
        dir.Delete(true);
    }

    [Fact]
    public void A_linked_dxf_is_drafting_that_cannot_be_edited_and_follows_its_link()
    {
        var f = F();
        var plan = Plan(f);
        var dxf = string.Join("\n", ["0", "SECTION", "2", "ENTITIES", "0", "LINE", "8", "A", "10", "0", "20", "0", "11", "1000", "21", "0", "0", "ENDSEC", "0", "EOF"]);
        var r = (JsonObject)f.Run("drafting.importDxf", new JsonObject { ["viewId"] = plan, ["name"] = "detail.dxf", ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(dxf)), ["link"] = true, ["units"] = "mm" })!;
        var link = Links.Require(f.Model, (string?)r["linkId"]);
        Assert.Equal("dxf", link.Str("kind"));
        Assert.Equal(plan, link.Str("viewId"));
        var line = Assert.Single(Drafts.On(f.Model, plan));
        Assert.Equal(link.Id, line.Str(Links.Key));
        Assert.Throws<CommandException>(() => f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(line.Id), ["op"] = "move", ["by"] = new JsonArray(1, 0) }));
        Assert.Throws<CommandException>(() => f.Run("drafting.updateDraft", new JsonObject { ["id"] = line.Id, ["layer"] = "0" }));
        // Hidden: no mark. Halftone: grey.
        JsonArray Marks() => ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!)["pens"]!.AsArray();
        Assert.Contains(Marks().OfType<JsonObject>(), p => (string?)p["layer"] == "A");
        f.Run("link.update", new JsonObject { ["id"] = link.Id, ["visible"] = false });
        Assert.DoesNotContain(Marks().OfType<JsonObject>(), p => (string?)p["layer"] == "A");
        f.Run("link.update", new JsonObject { ["id"] = link.Id, ["visible"] = true, ["halftone"] = true });
        Assert.Equal(Links.Halftone, (string?)Marks().OfType<JsonObject>().Single(p => (string?)p["layer"] == "A")["stroke"]);
        // Converted: an ordinary drafted line.
        f.Run("link.convert", new JsonObject { ["id"] = link.Id });
        f.Run("drafting.transform", new JsonObject { ["ids"] = new JsonArray(line.Id), ["op"] = "move", ["by"] = new JsonArray(1, 0) });
        Assert.Equal(1, ((Line)Drafts.ShapeOf(f.Model.Get(line.Id)!)!).A.X, 9);
    }
}

/// <summary>A link survives the project's file: saved and opened again, it is still a link, its elements still read-only.</summary>
public class LinkPackageTests
{
    [Fact]
    public void A_link_is_kept_by_the_project_file()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var g = new Fixture(new ArchitectureModule(), new DraftingModule());
        g.Wall(0, 0, 4, 0);
        using var ms = new MemoryStream();
        IfcExport.Write(g.Session, ms, "small.ifc", new IfcExportOptions("IFC4", "group", null));
        var r = IfcImport.Link(f.Session, ms.ToArray(), "small.ifc", null, null, out _);
        f.Run("link.update", new JsonObject { ["id"] = r.LinkId!, ["halftone"] = true });
        var dir = Directory.CreateTempSubdirectory("lapin-linkpkg");
        var path = Path.Combine(dir.FullName, "linked.lapin");
        PackageFiles.Create().Save(f.Session, path);
        var h = new Fixture(new ArchitectureModule(), new DraftingModule());
        PackageFiles.Open(h.Session, path);
        var link = Assert.Single(Links.All(h.Model));
        Assert.True(Links.IsHalftone(link));
        var wall = Assert.Single(h.Model.OfClass(IfcClass.Wall));
        Assert.Equal(link.Id, wall.Str(Links.Key));
        Assert.Throws<CommandException>(() => h.Run("architecture.updateWall", new JsonObject { ["id"] = wall.Id, ["height"] = 2 }));
        dir.Delete(true);
    }
}
