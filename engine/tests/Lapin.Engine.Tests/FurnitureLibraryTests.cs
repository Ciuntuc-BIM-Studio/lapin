using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Furniture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// My furniture library (docs/plan-furniture.md, Configuratorul flexibil, stage 3; decision 476): a furniture type saved from
/// one project goes into another as it was — its spec, its IFC type, its materials (matched by name or made, an image too) —
/// under a GlobalId of its own, in one undo step, an identical type reused; the library goes out as a .lapinlib file and comes
/// back from it, on the desktop (files in the user's folder) and in the browser (bytes the engine checks, packs and unpacks);
/// a template made from a project keeps the library's types it has, and may be given the library's others.
/// </summary>
public sealed class FurnitureLibraryTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "lapin-furniture-" + Guid.NewGuid().ToString("N"));

    public void Dispose() { try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

    static Fixture F() => new(new ArchitectureModule(), new FurnitureModule());

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

    /// <summary>A desk of the casework presets in walnut with sage fronts, the walnut given its own colour, category and an image.</summary>
    static string WalnutDesk(Fixture f)
    {
        var id = (string)f.Run("furniture.createType", new JsonObject
        {
            ["kind"] = "casework", ["preset"] = "desk", ["name"] = "Walnut desk",
            ["spec"] = new JsonObject { ["width"] = 1.4, ["materials"] = new JsonObject { ["body"] = "Walnut veneer", ["fronts"] = "Sage lacquer" } },
        })!["id"]!;
        var walnut = Materials.Find(f.Model, "Walnut veneer")!;
        var png = Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4]);
        var image = (string)f.Run("image.add", new JsonObject { ["name"] = "walnut.png", ["mime"] = "image/png", ["data"] = png })!["id"]!;
        f.Run("material.update", new JsonObject { ["id"] = walnut.Id, ["color"] = "#5b3a29", ["category"] = "Finish", ["textureImage"] = image });
        return id;
    }

    static JsonObject Item(Fixture f, string typeId) => (JsonObject)f.Session.Query("furniture.libraryItem", new JsonObject { ["typeId"] = typeId })!;

    static JsonObject Entry(JsonObject item, string name = "Walnut desk", string category = "office", params string[] tags) => new()
    {
        ["name"] = name, ["category"] = category, ["tags"] = new JsonArray([.. tags.Select(t => (JsonNode)t)]),
        ["article"] = "WD-140", ["price"] = 2450, ["thumbnail"] = "data:image/svg+xml;base64,PHN2Zy8+", ["type"] = item.DeepClone(),
    };

    [Fact]
    public void A_casework_type_saved_to_the_library_goes_into_a_fresh_project_as_it_was_in_one_undo_step()
    {
        var a = F();
        var desk = WalnutDesk(a);
        var original = a.Model.Get(desk)!;
        var item = Item(a, desk);
        Assert.Equal("IfcFurnitureType", (string?)item["class"]);
        Assert.Equal("Walnut desk", (string?)item["name"]);
        Assert.Equal(original.PredefinedType, (string?)item["predefinedType"]);
        // The materials it names travel by value: the walnut with its colour, category and image (no id of the first project).
        var walnut = Assert.Single(item["materials"]!.AsArray(), m => (string?)m!["name"] == "Walnut veneer")!;
        Assert.Equal("#5b3a29", (string?)walnut["attrs"]!["color"]);
        Assert.Null(walnut["attrs"]!["textureImage"]);
        Assert.Equal("image/png", (string?)walnut["image"]!["mime"]);
        Assert.Contains(item["materials"]!.AsArray(), m => (string?)m!["name"] == "Sage lacquer");

        var library = new FurnitureLibrary(_dir);
        var id = library.Add(Entry(item, tags: ["walnut", "home office"]));
        Assert.True(File.Exists(Path.Combine(_dir, id + ".json")));
        var kept = library.Get(id);
        Assert.Equal(["walnut", "home office"], kept["tags"]!.AsArray().Select(t => (string?)t));

        // A fresh project that already has a sage lacquer of its own (another colour): that one is used, not made again.
        var b = F();
        b.Run("material.create", new JsonObject { ["name"] = "sage LACQUER", ["color"] = "#00ff00" });
        var materialsBefore = b.Model.OfClass(IfcClass.Material).Count();
        var before = b.Model.Entities.Count();
        var r = b.Run("furniture.insertFromLibrary", new JsonObject { ["item"] = kept["type"]!.DeepClone() })!;
        Assert.False((bool)r["reused"]!);
        var inserted = b.Model.Get((string)r["id"]!)!;
        Assert.Equal("IfcFurnitureType", inserted.Class);
        Assert.NotEqual(original.Id, inserted.Id);
        Assert.Equal("Walnut desk", inserted.Name);
        Assert.Equal(original.PredefinedType, inserted.PredefinedType);
        Assert.Equal(LibraryItems.Canonical(original.Attrs["spec"]), LibraryItems.Canonical(inserted.Attrs["spec"]));
        // Walnut made as it was, with its image; sage matched to the project's own.
        var w = Materials.Find(b.Model, "Walnut veneer")!;
        Assert.Equal("#5b3a29", w.Str("color"));
        Assert.Equal("Finish", w.Str("category"));
        Assert.Equal("image/png", Images.Of(b.Model, w)!.Str("mime"));
        Assert.Equal("#00ff00", Materials.Find(b.Model, "Sage lacquer")!.Str("color"));
        Assert.Single(b.Model.OfClass(IfcClass.Material), m => string.Equals(m.Name, "Sage lacquer", StringComparison.OrdinalIgnoreCase));
        Assert.True(b.Model.OfClass(IfcClass.Material).Count() > materialsBefore);
        // A piece of it builds as the first project's did.
        var piece = (string)b.Run("furniture.place", new JsonObject { ["storeyId"] = b.Storey, ["typeId"] = inserted.Id, ["x"] = 0, ["y"] = 0, ["against"] = false })!["id"]!;
        Assert.NotNull(b.Session.Geometry(piece));
        b.Session.Undo();

        // One undo step takes it all away: the type, the walnut and its image.
        b.Session.Undo();
        Assert.Null(b.Model.Get(inserted.Id));
        Assert.Null(Materials.Find(b.Model, "Walnut veneer"));
        Assert.Empty(b.Model.OfClass(Images.Class));
        Assert.Equal(before, b.Model.Entities.Count());
        b.Session.Redo();

        // Inserted again: the identical type is reused (renamed in the project meanwhile), nothing made.
        b.Run("furniture.updateType", new JsonObject { ["id"] = inserted.Id, ["name"] = "My desk" });
        var count = b.Model.Entities.Count();
        var again = b.Run("furniture.insertFromLibrary", new JsonObject { ["item"] = kept["type"]!.DeepClone() })!;
        Assert.True((bool)again["reused"]!);
        Assert.Equal(inserted.Id, (string?)again["id"]);
        Assert.Equal(count, b.Model.Entities.Count());

        // Changed in the project, it is no longer identical: the library's comes in as a type of its own, under a free name.
        b.Run("furniture.updateType", new JsonObject { ["id"] = inserted.Id, ["name"] = "Walnut desk", ["spec"] = new JsonObject { ["width"] = 1.6 } });
        var third = b.Run("furniture.insertFromLibrary", new JsonObject { ["item"] = kept["type"]!.DeepClone() })!;
        Assert.False((bool)third["reused"]!);
        Assert.Equal("Walnut desk 2", b.Model.Get((string)third["id"]!)!.Name);
    }

    [Fact]
    public void Any_kind_of_type_goes_into_the_library_and_a_kind_this_edition_lacks_is_refused_plainly()
    {
        var a = F();
        var sofa = (string)a.Run("furniture.createType", new JsonObject { ["kind"] = "sofa", ["spec"] = new JsonObject { ["seats"] = 3 } })!["id"]!;
        var kitchen = (string)a.Run("furniture.createType", new JsonObject { ["kind"] = "kitchen", ["layout"] = "l-shaped" })!["id"]!;
        var b = F();
        foreach (var t in new[] { sofa, kitchen })
        {
            var r = b.Run("furniture.insertFromLibrary", new JsonObject { ["item"] = Item(a, t) })!;
            Assert.Equal(LibraryItems.Canonical(a.Model.Get(t)!.Attrs["spec"]), LibraryItems.Canonical(b.Model.Get((string)r["id"]!)!.Attrs["spec"]));
        }
        // A mesh-based piece of stage 2 is a spec naming its model: the library keeps it as it is; an edition without the kind says so.
        var mesh = new JsonObject
        {
            ["class"] = "IfcFurnitureType", ["name"] = "Three-seat sofa",
            ["spec"] = new JsonObject { ["kind"] = "meshSofa", ["model"] = "kenney/loungeSofa", ["width"] = 2.2, ["materials"] = new JsonObject { ["upholstery"] = "Linen grey" } },
        };
        var kept = FurnitureLibrary.Check(Entry(mesh, "Sofa", "living"));
        Assert.Equal("kenney/loungeSofa", (string?)kept["type"]!["spec"]!["model"]);
        var e = Assert.Throws<CommandException>(() => b.Run("furniture.insertFromLibrary", new JsonObject { ["item"] = mesh }));
        Assert.Contains("meshSofa", e.Message);
    }

    [Fact]
    public void The_library_goes_out_as_a_file_and_comes_back_from_it()
    {
        var a = F();
        var desk = WalnutDesk(a);
        var shelf = (string)a.Run("furniture.createType", new JsonObject { ["kind"] = "casework", ["preset"] = "bookcase" })!["id"]!;
        var library = new FurnitureLibrary(Path.Combine(_dir, "one"));
        var d1 = library.Add(Entry(Item(a, desk), tags: ["walnut"]));
        var d2 = library.Add(Entry(Item(a, shelf), "Tall bookcase", "living", "oak"));

        var file = library.Export(null);
        var text = Encoding.UTF8.GetString(file);
        Assert.Contains("\"format\": \"lapin-furniture-library\"", text);
        var other = new FurnitureLibrary(Path.Combine(_dir, "two"));
        var (added, skipped) = other.Import(file);
        Assert.Equal(2, added.Count);
        Assert.Equal(0, skipped);
        foreach (var id in new[] { d1, d2 }) Assert.True(FurnitureLibrary.Same(library.Get(id), other.Get(id)));
        Assert.Equal(["Tall bookcase", "Walnut desk"], other.List().Select(e => (string?)e["name"]));

        // Taken in again: nothing new. One entry changed here since: it comes in beside it, under a new id.
        Assert.Equal((0, 2), (other.Import(file).Added.Count, other.Import(file).Skipped));
        other.Update(d1, new JsonObject { ["name"] = "Walnut desk, long", ["price"] = null, ["tags"] = new JsonArray("walnut", "Walnut", " ") });
        Assert.Null(other.Get(d1)["price"]);
        Assert.Equal(["walnut"], other.Get(d1)["tags"]!.AsArray().Select(t => (string?)t));
        var (again, same) = other.Import(file);
        Assert.Single(again);
        Assert.NotEqual(d1, again[0]);
        Assert.Equal(1, same);
        Assert.Equal(3, other.List().Count);

        // One entry alone; a copy; a delete.
        var one = FurnitureLibrary.Unpack(library.Export([d2]));
        Assert.Equal("Tall bookcase", (string?)Assert.Single(one)["name"]);
        var copy = other.Duplicate(d2);
        Assert.Equal("Tall bookcase copy", (string?)other.Get(copy)["name"]);
        other.Delete(copy);
        Assert.Throws<CommandException>(() => other.Get(copy));

        // What is not a library is refused, and so is a newer one.
        Assert.Throws<CommandException>(() => FurnitureLibrary.Unpack(Encoding.UTF8.GetBytes("{\"format\":\"something\"}")));
        Assert.Throws<CommandException>(() => FurnitureLibrary.Unpack(Encoding.UTF8.GetBytes("not json")));
        Assert.Contains("newer", Assert.Throws<CommandException>(() => FurnitureLibrary.Unpack(Encoding.UTF8.GetBytes("{\"format\":\"lapin-furniture-library\",\"version\":9,\"items\":[]}"))).Message);
        // An entry is checked: a name, a known category (else other), a price not below 0.
        Assert.Throws<CommandException>(() => FurnitureLibrary.Check(Entry(Item(a, desk), " ")));
        Assert.Equal("other", (string?)FurnitureLibrary.Check(Entry(Item(a, desk), category: "garage"))["category"]);
        var bad = Entry(Item(a, desk));
        bad["price"] = -1;
        Assert.Throws<CommandException>(() => FurnitureLibrary.Check(bad));
    }

    [Fact]
    public void The_desktop_keeps_the_library_through_the_engine_and_the_browser_through_bytes_it_checks_packs_and_unpacks()
    {
        var a = F();
        var desk = WalnutDesk(a);
        var item = (JsonObject)Call(new EngineDispatcher(a.Session, PackageFiles.Create()), "doc.query", new JsonObject { ["name"] = "furniture.libraryItem", ["args"] = new JsonObject { ["typeId"] = desk } })!;

        // The desktop: the engine keeps entries in the user's folder.
        var desktop = new EngineDispatcher(a.Session, PackageFiles.Create()) { Furniture = new FurnitureLibrary(_dir) };
        var id = (string)Call(desktop, "furnitureLibrary.add", new JsonObject { ["entry"] = Entry(item) })!["id"]!;
        Assert.Single(Call(desktop, "furnitureLibrary.list")!.AsArray());
        Call(desktop, "furnitureLibrary.update", new JsonObject { ["id"] = id, ["patch"] = new JsonObject { ["category"] = "living" } });
        Assert.Equal("living", (string?)Call(desktop, "furnitureLibrary.get", new JsonObject { ["id"] = id })!["category"]);
        var exported = (string)Call(desktop, "furnitureLibrary.export", new JsonObject { ["ids"] = new JsonArray(id) })!["data"]!;

        // The browser: no folder; the page keeps the entries, the engine checks, packs and unpacks them.
        var browser = new EngineDispatcher(a.Session, PackageFiles.Create());
        Assert.Contains("desktop", Error(browser, "furnitureLibrary.add", new JsonObject { ["entry"] = Entry(item) }));
        Assert.Empty(Call(browser, "furnitureLibrary.list")!.AsArray());
        var entries = Call(browser, "furnitureLibrary.unpack", new JsonObject { ["data"] = exported })!.AsArray();
        var entry = Assert.IsType<JsonObject>(Assert.Single(entries));
        Assert.Equal(id, (string?)entry["id"]);
        var checkedEntry = (JsonObject)Call(browser, "furnitureLibrary.check", new JsonObject { ["entry"] = Entry(item) })!;
        Assert.False(string.IsNullOrEmpty((string?)checkedEntry["id"]));
        var packed = (string)Call(browser, "furnitureLibrary.pack", new JsonObject { ["entries"] = new JsonArray(entry.DeepClone()) })!["data"]!;
        // What the browser packs, the desktop takes in: the same entry.
        var other = new EngineDispatcher(a.Session, PackageFiles.Create()) { Furniture = new FurnitureLibrary(Path.Combine(_dir, "other")) };
        var imported = Call(other, "furnitureLibrary.import", new JsonObject { ["data"] = packed })!;
        Assert.Equal(id, (string?)imported["added"]![0]);
        Assert.True(FurnitureLibrary.Same(new FurnitureLibrary(_dir).Get(id), new FurnitureLibrary(Path.Combine(_dir, "other")).Get(id)));

        // And either way the entry's type goes into a project through doc.execute, one command.
        var b = F();
        var into = new EngineDispatcher(b.Session, PackageFiles.Create());
        Call(into, "doc.execute", new JsonObject { ["command"] = "furniture.insertFromLibrary", ["args"] = new JsonObject { ["item"] = entry["type"]!.DeepClone() } });
        Assert.Single(b.Model.OfClass(FurnitureModule.TypeClass), t => t.Name == "Walnut desk");
    }

    [Fact]
    public void A_template_keeps_the_library_furniture_its_project_has_and_may_be_given_the_rest()
    {
        var a = F();
        var desk = WalnutDesk(a);
        var deskItem = Item(a, desk);
        var b = F();
        var bookcase = (string)b.Run("furniture.createType", new JsonObject { ["kind"] = "casework", ["preset"] = "bookcase", ["spec"] = new JsonObject { ["materials"] = new JsonObject { ["body"] = "Smoked oak" } } })!["id"]!;
        var bookcaseItem = Item(b, bookcase);

        // A project with the desk from the library, and a piece of it.
        var p = F();
        var inserted = (string)p.Run("furniture.insertFromLibrary", new JsonObject { ["item"] = deskItem.DeepClone() })!["id"]!;
        p.Run("furniture.place", new JsonObject { ["storeyId"] = p.Storey, ["typeId"] = inserted, ["x"] = 1, ["y"] = 1, ["against"] = false });
        var d = new EngineDispatcher(p.Session, PackageFiles.Create()) { Templates = new TemplateLibrary(Path.Combine(_dir, "templates")) };
        Call(d, "templates.save", new JsonObject { ["name"] = "Office" });
        // Saved with the library's bookcase put in too: the open project left alone.
        Call(d, "templates.save", new JsonObject { ["name"] = "Office plus", ["furniture"] = new JsonArray(new JsonObject { ["name"] = "Bookcase", ["type"] = bookcaseItem.DeepClone() }) });
        Assert.DoesNotContain(p.Model.OfClass(FurnitureModule.TypeClass), t => t.Name == bookcaseItem["name"]!.ToString());

        Call(d, "project.newFromTemplate", new JsonObject { ["id"] = "Office" });
        var m = p.Model;
        Assert.Empty(m.OfClass(FurnitureModule.Class));
        var kept = Assert.Single(m.OfClass(FurnitureModule.TypeClass));
        Assert.NotEqual(inserted, kept.Id);
        Assert.Equal(LibraryItems.Canonical(deskItem["spec"]), LibraryItems.Canonical(kept.Attrs["spec"]));
        Assert.Equal("#5b3a29", Materials.Find(m, "Walnut veneer")!.Str("color"));
        Assert.NotNull(Images.Of(m, Materials.Find(m, "Walnut veneer")!));
        Assert.NotNull(Materials.Find(m, "Sage lacquer"));
        // In the project made from it, the library's desk is the template's: reused, not made twice.
        Assert.True((bool)p.Run("furniture.insertFromLibrary", new JsonObject { ["item"] = deskItem.DeepClone() })!["reused"]!);

        Call(d, "project.newFromTemplate", new JsonObject { ["id"] = "Office plus" });
        Assert.Equal(2, p.Model.OfClass(FurnitureModule.TypeClass).Count());
        Assert.NotNull(Materials.Find(p.Model, "Smoked oak"));

        // Add to a template…: the bookcase put into the first template, kept under its name.
        Call(d, "templates.addFurniture", new JsonObject { ["id"] = "Office", ["furniture"] = new JsonArray(bookcaseItem.DeepClone()) });
        Call(d, "project.newFromTemplate", new JsonObject { ["id"] = "Office" });
        Assert.Equal(2, p.Model.OfClass(FurnitureModule.TypeClass).Count());

        // The browser's way: template.make with the furniture, from the open project or from a template's bytes.
        var made = (string)Call(d, "template.make", new JsonObject { ["furniture"] = new JsonArray(bookcaseItem.DeepClone()) })!["data"]!;
        Call(d, "project.newFromTemplate", new JsonObject { ["data"] = made });
        Assert.Equal(2, p.Model.OfClass(FurnitureModule.TypeClass).Count());
    }
}
