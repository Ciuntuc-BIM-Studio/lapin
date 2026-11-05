using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Documents;

namespace Lapin.Engine.Tests;

/// <summary>
/// The document editor's engine side (docs/plan-documents.md, Etapa 2): a document's blocks as kept (<c>document.source</c>), one
/// block filled in the document's context (<c>document.block</c>): a paragraph with its fields, a table from the project, a repeat
/// whole, and the atoms of a repeat filled inside its first item or another; the input definitions saved with the blocks and undone.
/// </summary>
public sealed class DocumentEditingTests
{
    const string Template = """
    {
      "id": "test-edit", "title": "Test edit", "lang": "ro",
      "inputs": [{ "key": "structura", "label": "Structure", "group": "Construction", "default": "Zidărie portantă." }],
      "blocks": [
        { "kind": "heading", "level": 1, "text": "Memoriu {{project.name}}" },
        { "kind": "repeat", "over": "storeys", "blocks": [
          { "kind": "heading", "level": 2, "text": "{{storey.name}}" },
          { "kind": "repeat", "over": "rooms", "where": { "storeyId": "{{storey.id}}" }, "blocks": [
            { "kind": "paragraph", "text": "{{room.index}}. {{room.name}}" } ] } ] },
        { "kind": "input", "key": "structura" }
      ]
    }
    """;

    /// <summary>A ground floor of two rooms and a first floor of one, 10 × 8 m.</summary>
    static (Fixture F, string Doc, string Ground) House()
    {
        var f = new Fixture();
        var ground = f.Storey;
        f.Run("storey.update", new JsonObject { ["id"] = ground, ["name"] = "Parter" });
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "Etaj 1", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        var type = f.Type("Exterior 30", ("Brick", 0.30, true));
        foreach (var s in new[] { ground, first })
            foreach (var (x1, y1, x2, y2) in new (double, double, double, double)[] { (0, 0, 10, 0), (10, 0, 10, 8), (10, 8, 0, 8), (0, 8, 0, 0) })
                f.Run("architecture.addWall", new JsonObject { ["storeyId"] = s, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["typeId"] = type });
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = ground, ["x1"] = 5, ["y1"] = 0, ["x2"] = 5, ["y2"] = 8, ["typeId"] = type });
        f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = ground, ["x"] = 2.5, ["y"] = 4, ["name"] = "Living" });
        f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = ground, ["x"] = 7.5, ["y"] = 4, ["name"] = "Bucătărie" });
        f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = first, ["x"] = 5, ["y"] = 4, ["name"] = "Dormitor" });
        f.Run("project.setInfo", new JsonObject { ["name"] = "Casa Popescu", ["client"] = "Ionescu Maria" });
        var doc = (string)f.Run("document.create", new JsonObject { ["template"] = JsonNode.Parse(Template) })!["id"]!;
        return (f, doc, ground);
    }

    static JsonObject Block(Fixture f, string doc, JsonNode? block, JsonNode? scope = null)
    {
        var a = new JsonObject { ["id"] = doc };
        if (block != null) a["block"] = block;
        if (scope != null) a["scope"] = scope;
        return (JsonObject)f.Session.Query("document.block", a)!;
    }

    /// <summary>The plain text of a preview block's runs.</summary>
    static string Text(JsonNode? runs) => string.Concat(runs!.AsArray().Select(r => (bool?)r!["missing"] == true ? $"⟨{(string)r["text"]!}⟩" : (string)r!["text"]!));

    [Fact]
    public void The_source_is_the_documents_own_blocks_and_input_definitions()
    {
        var (f, doc, _) = House();
        var src = (JsonObject)f.Session.Query("document.source", new JsonObject { ["id"] = doc })!;
        Assert.True(JsonNode.DeepEquals(f.Model.Get(doc)!.Attrs["blocks"], src["blocks"]));
        Assert.Equal("structura", (string)src["inputDefs"]![0]!["key"]!);
        Assert.Equal("ro", (string)src["lang"]!);
    }

    [Fact]
    public void A_paragraph_is_filled_with_its_fields_and_says_what_it_misses()
    {
        var (f, doc, _) = House();
        var r = Block(f, doc, new JsonObject { ["kind"] = "paragraph", ["text"] = "Beneficiar: **{{project.client}}**, {{project.cadastral}}." });
        var p = r["blocks"]![0]!;
        Assert.Equal("paragraph", (string)p["kind"]!);
        Assert.Equal("Beneficiar: Ionescu Maria, ⟨project.cadastral⟩.", Text(p["runs"]));
        Assert.Contains(p["runs"]!.AsArray(), x => (string)x!["text"]! == "Ionescu Maria" && (bool?)x["bold"] == true);
        Assert.Equal(["project.cadastral"], r["missing"]!.AsArray().Select(x => (string)x!));
        // A block a document cannot fill is refused as document.update refuses it.
        Assert.Throws<CommandException>(() => Block(f, doc, new JsonObject { ["kind"] = "poem" }));
    }

    [Fact]
    public void A_table_from_the_project_is_filled_with_its_columns()
    {
        var (f, doc, _) = House();
        var t = Block(f, doc, new JsonObject { ["kind"] = "table", ["source"] = "kind:rooms", ["title"] = "Încăperi", ["columns"] = new JsonArray("Number", "Name") })["blocks"]![0]!;
        Assert.Equal("table", (string)t["kind"]!);
        Assert.Equal("Încăperi", (string)t["title"]!);
        Assert.Equal(["Number", "Name"], t["header"]!.AsArray().Select(x => (string)x!));
        var names = t["rows"]!.AsArray().Select(row => Text(row![1])).ToList();
        Assert.Contains("Living", names);
        Assert.Contains("Dormitor", names);
        // A source the project does not have is marked where the table stands.
        var none = Block(f, doc, new JsonObject { ["kind"] = "table", ["source"] = "kind:nothing" });
        Assert.Equal(["table:kind:nothing"], none["missing"]!.AsArray().Select(x => (string)x!));
    }

    [Fact]
    public void A_repeat_is_filled_whole_and_its_atoms_inside_an_item()
    {
        var (f, doc, ground) = House();
        // The whole repeat, as the preview has it: a heading per storey and its rooms under it.
        var repeat = f.Model.Get(doc)!.Attrs["blocks"]![1]!.DeepClone();
        var whole = Block(f, doc, repeat)["blocks"]!.AsArray().Select(b => Text(b!["runs"])).ToList();
        Assert.Contains("Parter", whole);
        Assert.Contains("1. Living", whole);
        Assert.Contains("1. Dormitor", whole);

        // An inner paragraph in the rooms of the ground floor: the first room, then the second; the storey's and room's values.
        var where = new JsonObject { ["storeyId"] = "{{storey.id}}" };
        JsonArray Chain(int room) => [new JsonObject { ["collection"] = "storeys", ["where"] = new JsonObject { ["id"] = ground } }, new JsonObject { ["collection"] = "rooms", ["where"] = where.DeepClone(), ["index"] = room }];
        var para = new JsonObject { ["kind"] = "paragraph", ["text"] = "{{storey.name}}: {{room.index}}. {{room.name}}" };
        var first = Block(f, doc, para, Chain(0));
        Assert.Equal("Parter: 1. Living", Text(first["blocks"]![0]!["runs"]));
        Assert.Equal([1, 2], first["items"]!.AsArray().Select(x => (int)x!));
        Assert.Equal("Living", (string)first["scope"]!["room.name"]!);
        Assert.Equal("Parter", (string)first["scope"]!["storey.name"]!);
        Assert.Equal("Parter: 2. Bucătărie", Text(Block(f, doc, para.DeepClone(), Chain(1))["blocks"]![0]!["runs"]));
        // Past the last item: the last; a scope alone gives its values only.
        Assert.Equal("Parter: 2. Bucătărie", Text(Block(f, doc, para.DeepClone(), Chain(9))["blocks"]![0]!["runs"]));
        var only = Block(f, doc, null, new JsonObject { ["collection"] = "rooms" });
        Assert.Empty(only["blocks"]!.AsArray());
        Assert.Equal([3], only["items"]!.AsArray().Select(x => (int)x!));
        Assert.Equal("1", (string)only["scope"]!["room.index"]!);

        // An item field outside its repeat is missing; a collection no module gives is said (-1), its fields missing.
        Assert.Equal(["room.name"], Block(f, doc, new JsonObject { ["kind"] = "paragraph", ["text"] = "{{room.name}}" })["missing"]!.AsArray().Select(x => (string)x!));
        var unknown = Block(f, doc, new JsonObject { ["kind"] = "paragraph", ["text"] = "{{x.name}}" }, new JsonObject { ["collection"] = "nothing", ["as"] = "x" });
        Assert.Equal([-1], unknown["items"]!.AsArray().Select(x => (int)x!));
        Assert.Equal(["x.name"], unknown["missing"]!.AsArray().Select(x => (string)x!));
    }

    [Fact]
    public void The_input_definitions_are_saved_with_the_blocks_as_one_step_and_undone()
    {
        var (f, doc, _) = House();
        var blocks = (JsonArray)f.Model.Get(doc)!.Attrs["blocks"]!.DeepClone();
        blocks.Add((JsonNode)new JsonObject { ["kind"] = "input", ["key"] = "acoperis" });
        var defs = new JsonArray(
            new JsonObject { ["key"] = "structura", ["label"] = "Structure", ["group"] = "Construction", ["default"] = "Zidărie portantă." },
            new JsonObject { ["key"] = "acoperis", ["label"] = " Roof ", ["group"] = "", ["default"] = "Șarpantă din lemn.", ["multiline"] = false });
        f.Run("document.update", new JsonObject { ["id"] = doc, ["blocks"] = blocks, ["inputDefs"] = defs });
        var kept = f.Model.Get(doc)!.Attrs["inputDefs"]!.AsArray();
        Assert.Equal(2, kept.Count);
        Assert.Equal("Roof", (string)kept[1]!["label"]!);
        Assert.Null(kept[1]!["group"]);
        Assert.False((bool)kept[1]!["multiline"]!);
        var (filled, _) = DocumentFill.Fill(f.Session.Reader, f.Model.Get(doc)!);
        Assert.Contains(filled.Blocks.OfType<Lapin.Drawing.Documents.DocParagraph>(), p => DocumentJson.Text(p.Runs) == "Șarpantă din lemn.");

        // One undo takes the blocks and the definitions back together.
        f.Session.Undo();
        Assert.Single(f.Model.Get(doc)!.Attrs["inputDefs"]!.AsArray());
        Assert.Equal(3, f.Model.Get(doc)!.Attrs["blocks"]!.AsArray().Count);

        // Definitions a document cannot keep are refused: a key twice, a key that is not one, a label not text.
        foreach (var bad in new JsonNode[]
        {
            new JsonArray(new JsonObject { ["key"] = "a" }, new JsonObject { ["key"] = "a" }),
            new JsonArray(new JsonObject { ["key"] = "a b" }),
            new JsonArray(new JsonObject { ["key"] = "a", ["label"] = 3 }),
            new JsonArray(JsonValue.Create("a")),
        })
            Assert.Throws<CommandException>(() => f.Run("document.update", new JsonObject { ["id"] = doc, ["inputDefs"] = bad }));
    }
}
