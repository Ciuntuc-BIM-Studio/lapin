using System.Text.Json.Nodes;
using Lapin.Drawing.Documents;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Documents;

namespace Lapin.Engine.Tests;

/// <summary>
/// The project's document templates and the style (docs/plan-documents.md, stage 4): a document saved as a template and listed with
/// its origin, a document made from it, renamed, replaced, deleted, imported, a built-in one given whole to duplicate, each change
/// one undo step, kept in the project's file; the style set on a document and on a template, checked, and filled into the page.
/// </summary>
public sealed class DocumentTemplateStoreTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-doctpl-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static JsonArray Templates(Fixture f) => (JsonArray)f.Session.Query("document.templates", [])!;
    static JsonObject? Listed(Fixture f, string id) => Templates(f).OfType<JsonObject>().FirstOrDefault(t => (string?)t["id"] == id);

    /// <summary>A document from the first built-in template, its blocks changed and an input added, so its template is its own.</summary>
    static string OwnDocument(Fixture f)
    {
        var builtIn = DocumentTemplates.BuiltIn[0].Id;
        var id = (string)f.Run("document.create", new JsonObject { ["template"] = builtIn })!["id"]!;
        f.Run("document.update", new JsonObject
        {
            ["id"] = id, ["lang"] = "en",
            ["page"] = new JsonObject { ["size"] = "A4", ["header"] = "Ciuntuc BIM Studio · {{project.name}}", ["footer"] = "{{page}}/{{pages}}" },
            ["blocks"] = new JsonArray(
                new JsonObject { ["kind"] = "heading", ["level"] = 1, ["text"] = "Company report for {{project.name}}" },
                new JsonObject { ["kind"] = "input", ["key"] = "summary" }),
        });
        return id;
    }

    static JsonObject Style() => new()
    {
        ["font"] = "Georgia", ["size"] = 11, ["headingColor"] = "#1F4E8C", ["pdfFont"] = "times",
        ["logo"] = new JsonObject { ["data"] = Convert.ToBase64String(DocumentStyleWriterTests.Logo()), ["w"] = 30, ["position"] = "right" },
    };

    [Fact]
    public void A_document_saved_as_a_template_is_listed_from_the_project_and_starts_documents()
    {
        var f = new Fixture();
        var doc = OwnDocument(f);
        Assert.All(Templates(f).OfType<JsonObject>(), t => Assert.Equal("builtin", (string?)t["origin"]));

        var tid = (string)f.Run("document.saveTemplate", new JsonObject { ["id"] = doc, ["title"] = "Raport de firmă", ["description"] = "Ours" })!["templateId"]!;
        Assert.Equal("raport-de-firma", tid);
        var listed = Listed(f, tid)!;
        Assert.Equal("project", (string?)listed["origin"]);
        Assert.Equal("Raport de firmă", (string?)listed["title"]);
        Assert.Equal("Ours", (string?)listed["description"]);
        Assert.Equal("en", (string?)listed["lang"]);

        // A document from it, by its id: the template's blocks and page, its title.
        var made = (string)f.Run("document.create", new JsonObject { ["template"] = tid })!["id"]!;
        var d = f.Model.Get(made)!;
        Assert.Equal(tid, d.Str("template"));
        Assert.Equal("Raport de firmă", d.Name);
        Assert.Equal("Company report for {{project.name}}", (string?)d.Attrs["blocks"]![0]!["text"]);
        Assert.Equal("Ciuntuc BIM Studio · {{project.name}}", (string?)d.Attrs["page"]!["header"]);
        // "Update from template" finds it in the project too.
        f.Run("document.refresh", new JsonObject { ["id"] = made });

        // The same title again is a new template; templateId replaces the one named.
        Assert.Equal("raport-de-firma-2", (string?)f.Run("document.saveTemplate", new JsonObject { ["id"] = doc, ["title"] = "Raport de firmă" })!["templateId"]);
        f.Run("document.update", new JsonObject { ["id"] = doc, ["blocks"] = new JsonArray(new JsonObject { ["kind"] = "paragraph", ["text"] = "Changed" }) });
        f.Run("document.saveTemplate", new JsonObject { ["id"] = doc, ["title"] = "Raport de firmă", ["templateId"] = tid });
        var json = (JsonObject)f.Session.Query("document.templateJson", new JsonObject { ["templateId"] = tid })!;
        Assert.Equal("Changed", (string?)json["blocks"]![0]!["text"]);
        Assert.Equal("Ours", (string?)json["description"]);   // kept when not given
        Assert.Throws<CommandException>(() => f.Run("document.saveTemplate", new JsonObject { ["id"] = doc, ["title"] = "X", ["templateId"] = DocumentTemplates.BuiltIn[0].Id }));
        Assert.Throws<CommandException>(() => f.Run("document.saveTemplate", new JsonObject { ["id"] = doc, ["title"] = " " }));
    }

    [Fact]
    public void Templates_are_renamed_deleted_and_imported_each_one_undo_step()
    {
        var f = new Fixture();
        var doc = OwnDocument(f);
        var tid = (string)f.Run("document.saveTemplate", new JsonObject { ["id"] = doc, ["title"] = "Firm" })!["templateId"]!;

        f.Run("document.renameTemplate", new JsonObject { ["templateId"] = tid, ["title"] = "Firm report", ["description"] = "Renamed" });
        Assert.Equal("Firm report", (string?)Listed(f, tid)!["title"]);
        f.Session.Undo();
        Assert.Equal("Firm", (string?)Listed(f, tid)!["title"]);

        // A built-in one is not renamed or deleted: it is duplicated, given whole.
        var builtIn = DocumentTemplates.BuiltIn[0].Id;
        Assert.Throws<CommandException>(() => f.Run("document.renameTemplate", new JsonObject { ["templateId"] = builtIn, ["title"] = "Mine" }));
        Assert.Throws<CommandException>(() => f.Run("document.deleteTemplate", new JsonObject { ["templateId"] = builtIn }));
        var whole = (JsonObject)f.Session.Query("document.templateJson", new JsonObject { ["templateId"] = builtIn })!;
        Assert.Equal(DocumentTemplates.BuiltIn[0].Blocks.Count, whole["blocks"]!.AsArray().Count);
        var copy = (JsonObject)f.Run("document.importTemplate", new JsonObject { ["template"] = whole, ["title"] = "My copy" })!;
        Assert.NotEqual(builtIn, (string?)copy["templateId"]);
        Assert.False((bool)copy["replaced"]!);
        Assert.Equal("project", (string?)Listed(f, (string)copy["templateId"]!)!["origin"]);

        // Imported again: a new id unless asked to overwrite.
        var mine = (JsonObject)f.Session.Query("document.templateJson", new JsonObject { ["templateId"] = tid })!;
        mine["title"] = "Firm v2";
        Assert.NotEqual(tid, (string?)f.Run("document.importTemplate", new JsonObject { ["template"] = mine.DeepClone() })!["templateId"]);
        var over = (JsonObject)f.Run("document.importTemplate", new JsonObject { ["template"] = mine, ["overwrite"] = true })!;
        Assert.Equal(tid, (string?)over["templateId"]);
        Assert.True((bool)over["replaced"]!);
        Assert.Equal("Firm v2", (string?)Listed(f, tid)!["title"]);
        // A template that is not one is refused.
        Assert.Throws<CommandException>(() => f.Run("document.importTemplate", new JsonObject { ["template"] = new JsonObject { ["id"] = "x", ["blocks"] = new JsonArray(new JsonObject { ["kind"] = "poem" }) } }));

        var before = Templates(f).Count;
        f.Run("document.deleteTemplate", new JsonObject { ["templateId"] = tid });
        Assert.Null(Listed(f, tid));
        Assert.Equal(before - 1, Templates(f).Count);
        f.Session.Undo();
        Assert.NotNull(Listed(f, tid));
        Assert.Throws<CommandException>(() => f.Run("document.create", new JsonObject { ["template"] = "nothing-here" }));
    }

    [Fact]
    public void A_template_from_the_library_given_whole_makes_a_document()
    {
        var f = new Fixture();
        var whole = (JsonObject)f.Session.Query("document.templateJson", new JsonObject { ["id"] = OwnDocument(f), ["title"] = "From the library" })!;
        Assert.Equal("from-the-library", (string?)whole["id"]);
        var made = (string)f.Run("document.create", new JsonObject { ["template"] = whole })!["id"]!;
        Assert.Equal("from-the-library", f.Model.Get(made)!.Str("template"));
        Assert.Empty(DocumentTemplateStore.Project(f.Model));   // given, not kept
    }

    [Fact]
    public void The_style_is_set_on_a_document_and_a_template_checked_and_filled_into_the_page()
    {
        var f = new Fixture();
        var doc = OwnDocument(f);
        f.Run("document.setStyle", new JsonObject { ["id"] = doc, ["style"] = Style() });
        var page = DocumentFill.Fill(f.Session.Reader, f.Model.Get(doc)!).Document.Page;
        Assert.Equal("Georgia", page.Style!.Font);
        Assert.Equal(11, page.Style.Size);
        Assert.Equal("#1f4e8c", page.Style.HeadingColor);
        Assert.Equal("times", page.Style.PdfFont);
        Assert.Equal((300, 100), page.Style.Logo!.Pixels);
        Assert.True(page.Style.Logo.Right);
        Assert.Equal("Ciuntuc BIM Studio · {{project.name}}", (string?)f.Model.Get(doc)!.Attrs["page"]!["header"]);   // the rest of the page kept
        Assert.Equal("Georgia", (string?)((JsonObject)f.Session.Query("document.style", new JsonObject { ["id"] = doc })!)["font"]);
        Assert.Equal("Georgia", (string?)((JsonObject)f.Session.Query("document.preview", new JsonObject { ["id"] = doc })!)["page"]!["style"]!["font"]);

        // Saved with the template, set on it, taken away.
        var tid = (string)f.Run("document.saveTemplate", new JsonObject { ["id"] = doc, ["title"] = "Styled" })!["templateId"]!;
        Assert.Equal("Georgia", (string?)((JsonObject)f.Session.Query("document.style", new JsonObject { ["templateId"] = tid })!)["font"]);
        f.Run("document.setStyle", new JsonObject { ["templateId"] = tid, ["style"] = new JsonObject { ["font"] = "Verdana" } });
        Assert.Equal("Verdana", (string?)((JsonObject)f.Session.Query("document.style", new JsonObject { ["templateId"] = tid })!)["font"]);
        f.Run("document.setStyle", new JsonObject { ["id"] = doc, ["style"] = null });
        Assert.Null(DocumentFill.Fill(f.Session.Reader, f.Model.Get(doc)!).Document.Page.Style);
        f.Session.Undo();
        Assert.NotNull(DocumentFill.Fill(f.Session.Reader, f.Model.Get(doc)!).Document.Page.Style);

        // What a writer could not use is refused when set.
        foreach (var bad in new JsonObject[]
        {
            new() { ["size"] = 40 }, new() { ["headingColor"] = "blue" }, new() { ["pdfFont"] = "courier" },
            new() { ["logo"] = new JsonObject { ["data"] = Convert.ToBase64String("not a picture"u8.ToArray()), ["w"] = 30 } },
            new() { ["logo"] = new JsonObject { ["data"] = Convert.ToBase64String(DocumentStyleWriterTests.Logo()), ["w"] = 500 } },
        })
            Assert.Throws<CommandException>(() => f.Run("document.setStyle", new JsonObject { ["id"] = doc, ["style"] = bad }));
        // Read leniently: a bad value set some other way is left out, the rest kept.
        Assert.Equal("Arial", DocumentStyles.Parse(new JsonObject { ["font"] = "Arial", ["size"] = 99, ["headingColor"] = "x" })!.Font);
        Assert.Null(DocumentStyles.Parse(new JsonObject { ["size"] = 99 }));
    }

    [Fact]
    public void The_projects_templates_are_saved_in_its_file_and_opened_again()
    {
        var f = new Fixture();
        var doc = OwnDocument(f);
        f.Run("document.setStyle", new JsonObject { ["id"] = doc, ["style"] = Style() });
        var tid = (string)f.Run("document.saveTemplate", new JsonObject { ["id"] = doc, ["title"] = "Kept" })!["templateId"]!;
        var path = Path.Combine(_dir, "templates.lapin");
        PackageFiles.Save(f.Session, path);

        var g = new Fixture();
        PackageFiles.Open(g.Session, path);
        var t = Assert.Single(DocumentTemplateStore.Project(g.Model));
        Assert.Equal(tid, t.Id);
        Assert.Equal("Kept", t.Title);
        Assert.Equal("Georgia", (string?)t.Page["style"]!["font"]);
        Assert.Equal("project", (string?)Listed(g, tid)!["origin"]);
    }
}
