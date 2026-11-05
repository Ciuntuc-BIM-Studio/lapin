using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing.Documents;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Cde;
using Lapin.Platform.Documents;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The written documents (docs/plan-documents.md, stage 1, engine): a document made from a template and filled from a small house
/// (areas, the height regime, rooms repeated per storey, wall types' layers, tables, inputs and their defaults, ifs, pictures),
/// numbers in the document's language, the project's information read by documents and title blocks, the commands and their undo,
/// the document saved in the project's file and issued with its revision and status. The writers' bytes are their own tests'.
/// </summary>
public sealed class DocumentTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-docs-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>The test's own template, given whole as JSON (built-in templates are resources; a company's come the same way).</summary>
    const string Template = """
    {
      "id": "test-house", "title": "Test house", "description": "A template of the tests", "lang": "ro", "isoType": "RP",
      // Comments and trailing commas are allowed, as a template written by hand has them.
      "page": { "size": "A4", "orientation": "portrait", "margins": [20, 15, 20, 25], "header": "{{project.name}}", "footer": "Pagina {{page}} din {{pages}}", },
      "inputs": [
        { "key": "structura", "label": "Structure", "multiline": true, "group": "Construction",
          "default": "Structură din zidărie portantă.\n- fundații continue\n- planșee din beton armat" },
        { "key": "situatie", "label": "Existing situation", "group": "General" }
      ],
      "blocks": [
        { "kind": "heading", "level": 1, "text": "Memoriu {{project.name}}" },
        { "kind": "paragraph", "text": "Beneficiar: **{{project.client}}**, *{{project.address}}*." },
        { "kind": "fields", "rows": [["Aria construită", "{{areas.built}}"], ["Aria desfășurată", "{{areas.gross}}"], ["Aria utilă", "{{areas.usable}}"],
                                      ["Regim de înălțime", "{{levels.text}}"], ["Înălțimea maximă", "{{height.max}}"], ["Uși", "{{counts.doors}}"]] },
        { "kind": "if", "field": "areas.basement", "blocks": [{ "kind": "paragraph", "text": "Subsol: {{areas.basement}}" }], "else": [{ "kind": "paragraph", "text": "Fără subsol" }] },
        { "kind": "if", "field": "counts.rooms", "not": true, "blocks": [{ "kind": "paragraph", "text": "Nicio încăpere" }] },
        { "kind": "repeat", "over": "storeys", "blocks": [
          { "kind": "heading", "level": 2, "text": "{{storey.name}} ({{storey.elevation}})" },
          { "kind": "repeat", "over": "rooms", "where": { "storeyId": "{{storey.id}}" }, "blocks": [
            { "kind": "paragraph", "text": "{{room.index}}. {{room.number}} {{room.name}}: {{room.area}}" } ] } ] },
        { "kind": "repeat", "over": "wallTypes", "blocks": [{ "kind": "paragraph", "text": "{{wallType.name}}: {{wallType.layers}}" }] },
        { "kind": "table", "source": "kind:rooms", "title": "Încăperi", "columns": ["Number", "Name", "Area (m²)"] },
        { "kind": "table", "source": "kind:all", "total": false },
        { "kind": "input", "key": "structura" },
        { "kind": "input", "key": "situatie" },
        { "kind": "toc" },
        { "kind": "pageBreak" }
      ]
    }
    """;

    static readonly DocFormat Ro = new("ro", "metric"), En = new("en", "metric");

    /// <summary>
    /// A small house: a basement, a ground floor and a first floor of 10 × 8 m walls (brick 25 cm + EPS 10 cm), an attic of the
    /// same walls; two rooms on the ground floor (an inner wall at x = 5), one on the first floor; a door and a window.
    /// </summary>
    static (Fixture F, Dictionary<string, string> Storeys) House()
    {
        var f = new Fixture();
        var storeys = new Dictionary<string, string> { ["P"] = f.Storey };
        f.Run("storey.update", new JsonObject { ["id"] = f.Storey, ["name"] = "Parter" });
        storeys["S"] = (string)f.Run("storey.create", new JsonObject { ["name"] = "Subsol", ["elevation"] = -3.0, ["height"] = 3.0 })!["id"]!;
        storeys["E1"] = (string)f.Run("storey.create", new JsonObject { ["name"] = "Etaj 1", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        storeys["M"] = (string)f.Run("storey.create", new JsonObject { ["name"] = "Mansardă", ["elevation"] = 6.0, ["height"] = 2.5 })!["id"]!;
        var type = f.Type("Exterior 35", ("Brick", 0.25, true), ("EPS", 0.10, false));
        var inner = f.Type("Interior 15", ("Brick", 0.15, true));
        foreach (var s in storeys.Values)
        {
            (double, double, double, double)[] ring = [(0, 0, 10, 0), (10, 0, 10, 8), (10, 8, 0, 8), (0, 8, 0, 0)];
            foreach (var (x1, y1, x2, y2) in ring)
                f.Run("architecture.addWall", new JsonObject { ["storeyId"] = s, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["typeId"] = type });
        }
        var split = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = storeys["P"], ["x1"] = 5, ["y1"] = 0, ["x2"] = 5, ["y2"] = 8, ["typeId"] = inner })!["id"]!;
        var south = f.Model.OfClass(IfcClass.Wall).First(w => Spatial.StoreyOf(f.Model, w.Id)?.Id == storeys["P"]);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = south.Id, ["along"] = 2 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = split, ["along"] = 4 });
        f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = storeys["P"], ["x"] = 2.5, ["y"] = 4, ["name"] = "Living" });
        f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = storeys["P"], ["x"] = 7.5, ["y"] = 4, ["name"] = "Bucătărie" });
        f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = storeys["E1"], ["x"] = 5, ["y"] = 4, ["name"] = "Dormitor" });
        return (f, storeys);
    }

    static string Create(Fixture f, string? name = null)
    {
        var a = new JsonObject { ["template"] = JsonNode.Parse(Template, documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true }) };
        if (name != null) a["name"] = name;
        return (string)f.Run("document.create", a)!["id"]!;
    }

    static (FilledDocument Doc, IReadOnlyList<string> Missing) Fill(Fixture f, string id) => DocumentFill.Fill(f.Session.Reader, f.Model.Get(id)!);

    static string Text(IEnumerable<DocRun> runs) => DocumentJson.Text(runs);

    /// <summary>The fields table's value for a label.</summary>
    static string Field(FilledDocument d, string label) =>
        Text(d.Blocks.OfType<DocTable>().First(t => t.Plain).Rows.First(r => Text(r[0]) == label)[1]);

    /// <summary>The footprint a storey's 10 × 8 m ring of walls should give: the box of its walls' bodies.</summary>
    static double Box(Fixture f, string storey)
    {
        var walls = f.Model.OfClass(IfcClass.Wall).Where(w => Spatial.StoreyOf(f.Model, w.Id)?.Id == storey).Select(w => f.Bounds(w.Id)).ToList();
        return (walls.Max(b => b.Max.X) - walls.Min(b => b.Min.X)) * (walls.Max(b => b.Max.Y) - walls.Min(b => b.Min.Y));
    }

    // ------------------------------------------------------------ numbers

    [Fact]
    public void Numbers_areas_levels_and_dates_are_written_in_the_documents_language()
    {
        Assert.Equal("1.234,50 m²", DocNumbers.Area(1234.5, Ro));
        Assert.Equal("1,234.50 m²", DocNumbers.Area(1234.5, En));
        Assert.Equal("8,45 m", DocNumbers.Length(8.449, Ro));
        Assert.Equal("±0,00", DocNumbers.Level(0, Ro));
        Assert.Equal("+3,00", DocNumbers.Level(3, Ro));
        Assert.Equal("-1.50", DocNumbers.Level(-1.5, En));
        Assert.Equal("25 cm", DocNumbers.Thickness(0.25, Ro));
        Assert.Equal("2,5 cm", DocNumbers.Thickness(0.025, Ro));
        Assert.Equal("8 mm", DocNumbers.Thickness(0.008, Ro));
        Assert.Equal("05.11.2026", DocNumbers.Date(new DateTime(2026, 11, 5), Ro));
        Assert.Equal("2026-11-05", DocNumbers.Date(new DateTime(2026, 11, 5), En));
        // A schedule's cells read as numbers with their units, whichever separator; a date or a mark is not a number.
        Assert.Equal((12.5, "", 2), DocNumbers.Parse("12.50"));
        Assert.Equal((1234.56, "m²", 2), DocNumbers.Parse("1.234,56 m²"));
        Assert.Equal((1234.0, "", 0), DocNumbers.Parse("1,234"));
        Assert.Equal((0.25, "m", 3), DocNumbers.Parse("0.250 m"));
        Assert.Null(DocNumbers.Parse("30.10.2026"));
        Assert.Null(DocNumbers.Parse("2x3"));
        Assert.Null(DocNumbers.Parse("Living"));
        Assert.Equal("12,50", DocNumbers.Reformat("12.50", Ro));
        Assert.True(DocNumbers.IsEmpty("0,00 m²"));
        Assert.True(DocNumbers.IsEmpty(" "));
        Assert.False(DocNumbers.IsEmpty("S+P"));
    }

    [Fact]
    public void The_height_regime_reads_basements_ground_floor_floors_and_attic_from_the_storeys()
    {
        static Entity S(string name, double z) => new(name, IfcClass.BuildingStorey, name, new JsonObject { ["elevation"] = z, ["height"] = 3.0 });
        Assert.Equal("S+P+1E+M", DocumentFields.LevelsText([S("Subsol", -3), S("Parter", 0), S("Etaj 1", 3), S("Mansardă", 6)]));
        Assert.Equal("P+2E", DocumentFields.LevelsText([S("Ground floor", 0), S("First floor", 3), S("Second floor", 6)]));
        Assert.Equal("2S+P+M", DocumentFields.LevelsText([S("B2", -6), S("B1", -3), S("Ground", 0), S("Attic", 3)]));
        // A raised or half-sunk ground floor is still the ground floor (above −1.00 m).
        Assert.Equal("P+1E", DocumentFields.LevelsText([S("Parter", -0.45), S("Etaj", 2.6)]));
        Assert.Equal("", DocumentFields.LevelsText([]));
    }

    // ------------------------------------------------------------ filling

    [Fact]
    public void A_document_is_filled_from_the_house_its_areas_regime_rooms_per_storey_wall_types_and_tables()
    {
        var (f, storeys) = House();
        var id = Create(f);
        var (d, missing) = Fill(f, id);

        Assert.Equal("Test house", d.Title);
        Assert.Equal((210.0, 297.0, 20.0, 15.0, 20.0, 25.0), (d.Page.W, d.Page.H, d.Page.Top, d.Page.Right, d.Page.Bottom, d.Page.Left));
        Assert.Contains(d.Page.Footer, r => r.Field == "page");
        Assert.Contains(d.Page.Footer, r => r.Field == "pages");
        Assert.Equal("Memoriu Project", Text(((DocHeading)d.Blocks[0]).Runs));

        // The areas: the ground floor's footprint is the box of its ring of walls; the gross area the four storeys'; the usable the rooms'.
        var built = Box(f, storeys["P"]);
        Assert.Equal(DocNumbers.Area(built, Ro), Field(d, "Aria construită"));
        Assert.Contains(',', Field(d, "Aria construită"));
        var gross = storeys.Values.Sum(s => Box(f, s));
        Assert.Equal(DocNumbers.Area(gross, Ro), Field(d, "Aria desfășurată"));
        var usable = f.Model.OfClass(Rooms.Class).Sum(r => Rooms.Of(r, f.Model, f.Session.Geometry)!.NetArea);
        Assert.Equal(DocNumbers.Area(usable, Ro), Field(d, "Aria utilă"));
        Assert.Equal("S+P+1E+M", Field(d, "Regim de înălțime"));
        // The walls of the attic rise 2.5 m over +6.00 (their own height may be the storey's).
        Assert.Matches(@"^\d+,\d\d m$", Field(d, "Înălțimea maximă"));
        Assert.Equal("1", Field(d, "Uși"));

        // The if: there is a basement; there are rooms (a negated if shows nothing).
        var paragraphs = d.Blocks.OfType<DocParagraph>().Select(p => Text(p.Runs)).ToList();
        Assert.Contains(paragraphs, p => p.StartsWith("Subsol: ") && p.EndsWith(" m²"));
        Assert.DoesNotContain("Fără subsol", paragraphs);
        Assert.DoesNotContain("Nicio încăpere", paragraphs);

        // The storeys repeated bottom up, each with its rooms (where: the storey's id), numbered in the repeat.
        var headings = d.Blocks.OfType<DocHeading>().Where(h => h.Level == 2).Select(h => Text(h.Runs)).ToList();
        Assert.Equal(["Subsol (-3,00)", "Parter (±0,00)", "Etaj 1 (+3,00)", "Mansardă (+6,00)"], headings);
        var afterGround = d.Blocks.SkipWhile(b => b is not DocHeading { Level: 2 } h || !Text(h.Runs).StartsWith("Parter")).Skip(1).TakeWhile(b => b is DocParagraph).Select(b => Text(((DocParagraph)b).Runs)).ToList();
        Assert.Equal(2, afterGround.Count);
        Assert.StartsWith("1. ", afterGround[0]);
        Assert.Contains("Living", afterGround[0]);
        Assert.StartsWith("2. ", afterGround[1]);
        Assert.Contains("Bucătărie", afterGround[1]);
        Assert.Contains(paragraphs, p => p.Contains("Dormitor"));

        // The wall types used, with their layers as text.
        Assert.Contains("Exterior 35: Brick 25 cm + EPS 10 cm", paragraphs);
        Assert.Contains("Interior 15: Brick 15 cm", paragraphs);

        // The room schedule's chosen columns, its totals line kept and bold, the area a number in Romanian.
        var tables = d.Blocks.OfType<DocTable>().Where(t => !t.Plain).ToList();
        var rooms = tables[0];
        Assert.Equal("Încăperi", rooms.Title);
        Assert.Equal(["Number", "Name", "Area (m²)"], rooms.Header);
        Assert.Equal(4, rooms.Rows.Count);
        Assert.True(rooms.TotalRow);
        Assert.Contains(2, rooms.Numeric);
        Assert.Contains(',', Text(rooms.Rows[0][2]));
        Assert.Equal(DocNumbers.Number(usable, Ro), Text(rooms.Rows[^1][2]));
        // The joinery schedule: the door and the window types in use, without a totals line.
        var joinery = tables[1];
        Assert.Equal("Joinery schedule", joinery.Title);
        Assert.Equal(2, joinery.Rows.Count);
        Assert.False(joinery.TotalRow);
        Assert.Contains("Mark", joinery.Header);

        // The input's default: a paragraph, then its lines starting with "- " as a list; an input with no text is to complete.
        Assert.Contains("Structură din zidărie portantă.", paragraphs);
        var list = d.Blocks.OfType<DocList>().Single();
        Assert.Equal(["fundații continue", "planșee din beton armat"], list.Items.Select(Text));
        Assert.Contains(d.Blocks.OfType<DocToc>(), t => t.Title == "Cuprins");
        Assert.IsType<DocPageBreak>(d.Blocks[^1]);

        // To complete: what the project does not say yet, once each, in order; never the field an if tests.
        Assert.Equal(["project.client", "project.address", "input.situatie"], missing);
        var client = ((DocParagraph)d.Blocks[1]).Runs.Single(r => r.Missing && r.Text == "project.client");
        Assert.True(client.Bold);
        Assert.True(((DocParagraph)d.Blocks[1]).Runs.Single(r => r.Text == "project.address").Italic);
    }

    [Fact]
    public void Project_information_fills_documents_and_title_blocks_as_one_undo_step()
    {
        var (f, _) = House();
        var id = Create(f);
        f.Run("project.setInfo", new JsonObject { ["name"] = "Casa Popescu", ["client"] = "Ion Popescu", ["address"] = "Huși, str. Mare 1", ["author"] = "IC", ["checkedBy"] = "AB", ["phase"] = "DTAC", ["number"] = "" });
        var info = (JsonObject)f.Session.Query("project.info", [])!;
        Assert.Equal("Casa Popescu", (string)info["name"]!);
        Assert.Equal("Ion Popescu", (string)info["client"]!);
        Assert.Null(info["number"]);

        var (d, missing) = Fill(f, id);
        Assert.Equal("Memoriu Casa Popescu", Text(((DocHeading)d.Blocks[0]).Runs));
        var beneficiary = ((DocParagraph)d.Blocks[1]).Runs;
        Assert.Equal("Beneficiar: Ion Popescu, Huși, str. Mare 1.", Text(beneficiary));
        Assert.True(beneficiary.Single(r => r.Text == "Ion Popescu").Bold);
        Assert.Equal(["input.situatie"], missing);
        Assert.Equal("Casa Popescu", Text(d.Page.Header));
        Assert.Equal("IC", d.Meta.Author);

        // A sheet that says nothing of who drew it takes the project's: printed, and the issue's checks pass.
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plan parter", ["number"] = "A-101", ["paper"] = "A3" })!["id"]!;
        var page = Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!);
        var texts = page.Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();
        Assert.Contains("IC", texts);
        Assert.Contains("AB", texts);
        Assert.Contains("Casa Popescu", texts);
        var findings = CdeChecks.Run(f.Session.Reader, [f.Model.Get(sheet)!], null);
        Assert.DoesNotContain(findings, x => x.Message.Contains("Drawn by") || x.Message.Contains("Checked by") || x.Message.Contains("phase"));

        // The sheet undone, then the information in one undo step.
        f.Session.Undo();
        Assert.Null(f.Model.Get(sheet));
        f.Session.Undo();
        Assert.Equal(["project.client", "project.address", "input.situatie"], Fill(f, id).Missing);
        Assert.Equal("Project", Spatial.Project(f.Model)!.Name);
    }

    [Fact]
    public void Inputs_take_the_users_text_over_the_default_and_a_null_gives_the_default_back()
    {
        var (f, _) = House();
        var id = Create(f);
        f.Run("document.update", new JsonObject { ["id"] = id, ["inputs"] = new JsonObject { ["structura"] = "Cadre din **beton armat**.\n\nPlanșee din beton.", ["situatie"] = "Teren liber." } });
        var (d, missing) = Fill(f, id);
        var paragraphs = d.Blocks.OfType<DocParagraph>().ToList();
        var frames = paragraphs.Single(p => Text(p.Runs) == "Cadre din beton armat.");
        Assert.True(frames.Runs.Single(r => r.Text == "beton armat").Bold);
        Assert.Contains(paragraphs, p => Text(p.Runs) == "Planșee din beton.");
        Assert.Contains(paragraphs, p => Text(p.Runs) == "Teren liber.");
        Assert.Empty(d.Blocks.OfType<DocList>());
        Assert.DoesNotContain("input.situatie", missing);

        // Merged: one input changed leaves the other; null removes it, and the default is shown again.
        f.Run("document.update", new JsonObject { ["id"] = id, ["inputs"] = new JsonObject { ["structura"] = null } });
        var doc = f.Model.Get(id)!;
        Assert.Equal("Teren liber.", (string?)doc.Attrs["inputs"]!["situatie"]);
        Assert.Contains(Fill(f, id).Doc.Blocks.OfType<DocParagraph>(), p => Text(p.Runs) == "Structură din zidărie portantă.");

        // The preview lists the declared inputs with their values.
        var preview = (JsonObject)f.Session.Query("document.preview", new JsonObject { ["id"] = id })!;
        var inputs = preview["inputs"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal(["structura", "situatie"], inputs.Select(i => (string)i["key"]!));
        Assert.StartsWith("Structură", (string)inputs[0]["value"]!);
        Assert.Equal("Construction", (string)inputs[0]["group"]!);
    }

    [Fact]
    public void English_writes_a_decimal_point_and_its_own_contents_title()
    {
        var (f, storeys) = House();
        var id = Create(f);
        f.Run("document.update", new JsonObject { ["id"] = id, ["lang"] = "en" });
        var (d, _) = Fill(f, id);
        Assert.Equal(DocNumbers.Area(Box(f, storeys["P"]), En), Field(d, "Aria construită"));
        Assert.DoesNotContain(',', Field(d, "Aria construită"));
        Assert.Contains('.', Field(d, "Aria construită"));
        Assert.Equal("Contents", d.Blocks.OfType<DocToc>().Single().Title);
        Assert.Equal("en", d.Meta.Lang);
    }

    [Fact]
    public void Without_a_basement_the_else_shows_and_the_basement_area_is_empty()
    {
        var f = new Fixture();
        foreach (var (x1, y1, x2, y2) in new[] { (0.0, 0.0, 6.0, 0.0), (6.0, 0.0, 6.0, 5.0), (6.0, 5.0, 0.0, 5.0), (0.0, 5.0, 0.0, 0.0) }) f.Wall(x1, y1, x2, y2);
        var id = Create(f);
        var (d, missing) = Fill(f, id);
        Assert.Contains(d.Blocks.OfType<DocParagraph>(), p => Text(p.Runs) == "Fără subsol");
        Assert.Contains(d.Blocks.OfType<DocParagraph>(), p => Text(p.Runs) == "Nicio încăpere");
        Assert.Equal("P", Field(d, "Regim de înălțime"));
        // No rooms: the usable area is to complete; the basement is not listed (an if only tests it).
        Assert.Contains("areas.usable", missing);
        Assert.DoesNotContain("areas.basement", missing);
    }

    [Fact]
    public void A_picture_is_a_view_drawn_at_its_scale_never_wider_than_the_text()
    {
        var (f, storeys) = House();
        var id = Create(f);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storeys["P"], ["name"] = "Plan parter", ["scale"] = 50 })!["id"]!;
        var blocks = (JsonArray)f.Model.Get(id)!.Attrs["blocks"]!.DeepClone();
        blocks.Add((JsonNode)new JsonObject { ["kind"] = "image", ["view"] = plan, ["scale"] = 100, ["caption"] = "Plan parter, {{project.name}}" });
        blocks.Add((JsonNode)new JsonObject { ["kind"] = "image", ["view"] = plan, ["caption"] = "At its own scale" });
        blocks.Add((JsonNode)new JsonObject { ["kind"] = "image", ["view"] = plan, ["width"] = 60 });
        blocks.Add((JsonNode)new JsonObject { ["kind"] = "image", ["view"] = "nothing" });
        f.Run("document.update", new JsonObject { ["id"] = id, ["blocks"] = blocks });
        var (d, missing) = Fill(f, id);
        var images = d.Blocks.OfType<DocImage>().ToList();
        Assert.Equal(3, images.Count);
        var text = d.Page.TextWidth;
        // 1:100: the house's ten metres and a little are about 10 cm wide, its pens on the paper from 0,0.
        Assert.InRange(images[0].W, 100, text);
        Assert.Equal("Plan parter, Project", images[0].Caption);
        Assert.NotEmpty(images[0].Pens);
        var pts = images[0].Pens.SelectMany(p => p.Segments.SelectMany(s => new[] { s.A, s.B })).ToList();
        Assert.True(pts.Min(p => p.X) >= -1e-6 && pts.Min(p => p.Y) >= -1e-6);
        Assert.True(pts.Max(p => p.X) <= images[0].W + 1e-6 && pts.Max(p => p.Y) <= images[0].H + 1e-6);
        // 1:50 would be over 20 cm: stepped down to a standard scale that fits the text.
        Assert.InRange(images[1].W, 50, text);
        // A width asked is fitted exactly.
        Assert.Equal(60, images[2].W, 3);
        Assert.StartsWith("img-", images[0].Key);
        Assert.NotEqual(images[0].Key, images[1].Key);
        Assert.Contains("view:nothing", missing);

        // document.images gives the same pictures, by the same keys, with their pens as JSON.
        var listed = (JsonArray)f.Session.Query("document.images", new JsonObject { ["id"] = id })!;
        Assert.Equal(images.Select(i => i.Key), listed.Select(x => (string)x!["key"]!));
        Assert.NotEmpty(listed[0]!["pens"]!.AsArray());
    }

    [Fact]
    public void Every_built_in_template_reads_and_fills_the_house()
    {
        var resources = typeof(DocumentTemplates).Assembly.GetManifestResourceNames().Count(n => n.StartsWith("Lapin.Platform.Documents.Templates.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal));
        Assert.Equal(resources, DocumentTemplates.BuiltIn.Count);
        Assert.Equal(DocumentTemplates.BuiltIn.Count, ((JsonArray)new Fixture().Session.Query("document.templates", [])!).Count);
        var (f, _) = House();
        foreach (var t in DocumentTemplates.BuiltIn)
        {
            var id = (string)f.Run("document.create", new JsonObject { ["template"] = t.Id })!["id"]!;
            var (d, _) = Fill(f, id);
            Assert.NotEmpty(d.Blocks);
            // Every collection a template repeats over is one the product knows.
            Assert.DoesNotContain(Fill(f, id).Missing, k => k.StartsWith("collection:", StringComparison.Ordinal));
            // Written for real in every format; with LAPIN_DOCS_SAMPLES set, kept there (scripts/documents-check.mjs opens them with LibreOffice).
            var folder = Environment.GetEnvironmentVariable("LAPIN_DOCS_SAMPLES");
            if (folder is { Length: > 0 }) f.Run("project.setInfo", new JsonObject { ["number"] = "12/2026", ["phase"] = "DTAC", ["client"] = "Ionescu Maria", ["address"] = "Str. Florilor 12, Huși", ["designer"] = "Ciuntuc BIM Studio SRL", ["chiefArchitect"] = "arh. Ion Popescu" });
            foreach (var format in new[] { "docx", "odt", "pdf" })
            {
                var r = (JsonObject)f.Session.Query("document.export", new JsonObject { ["id"] = id, ["format"] = format })!;
                var bytes = Convert.FromBase64String((string)r["data"]!);
                Assert.True(bytes.Length > 1000);
                if (folder is { Length: > 0 }) { Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, $"{t.Id}.{format}"), bytes); }
            }
        }
        // A where on the storey by its id finds the rooms' storeyId too.
        var doc = Create(f);
        f.Run("document.update", new JsonObject { ["id"] = doc, ["blocks"] = new JsonArray(new JsonObject
        {
            ["kind"] = "repeat", ["over"] = "storeys", ["blocks"] = new JsonArray(new JsonObject
            {
                ["kind"] = "repeat", ["over"] = "rooms", ["where"] = new JsonObject { ["storey"] = "{{storey.id}}" },
                ["blocks"] = new JsonArray(new JsonObject { ["kind"] = "paragraph", ["text"] = "{{storey.name}}: {{room.name}}" }),
            }),
        }) });
        Assert.Equal(["Parter: Living", "Parter: Bucătărie", "Etaj 1: Dormitor"], Fill(f, doc).Doc.Blocks.OfType<DocParagraph>().Select(p => Text(p.Runs)));
    }

    // ------------------------------------------------------------ commands and queries

    [Fact]
    public void Documents_are_made_renamed_duplicated_refreshed_and_deleted_each_one_undo_step()
    {
        var (f, _) = House();
        var id = Create(f);
        Assert.Equal("Test house", f.Model.Get(id)!.Name);
        // A second from the same template gets a name of its own.
        var second = Create(f);
        Assert.Equal("Test house 2", f.Model.Get(second)!.Name);
        f.Session.Undo();
        Assert.Null(f.Model.Get(second));
        Assert.Single(Documents.All(f.Model));

        f.Run("document.update", new JsonObject { ["id"] = id, ["name"] = "Memoriu arhitectură", ["inputs"] = new JsonObject { ["situatie"] = "Teren liber." } });
        var copy = (string)f.Run("document.duplicate", new JsonObject { ["id"] = id })!["id"]!;
        Assert.Equal("Memoriu arhitectură (copy)", f.Model.Get(copy)!.Name);
        Assert.Equal("Teren liber.", (string?)f.Model.Get(copy)!.Attrs["inputs"]!["situatie"]);

        // The blocks changed by hand come back from the template, the inputs kept.
        f.Run("document.update", new JsonObject { ["id"] = id, ["blocks"] = new JsonArray(new JsonObject { ["kind"] = "paragraph", ["text"] = "Only this" }) });
        Assert.Single(Fill(f, id).Doc.Blocks);
        var refused = Assert.Throws<CommandException>(() => f.Run("document.update", new JsonObject { ["id"] = id, ["blocks"] = new JsonArray(new JsonObject { ["kind"] = "poem" }) }));
        Assert.Contains("not a kind of block", refused.Message);
        // A template not built in is given again to refresh from.
        Assert.Throws<CommandException>(() => f.Run("document.refresh", new JsonObject { ["id"] = id }));
        f.Run("document.refresh", new JsonObject { ["id"] = id, ["template"] = JsonNode.Parse(Template, documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true }) });
        Assert.True(Fill(f, id).Doc.Blocks.Count > 10);
        Assert.Equal("Teren liber.", (string?)f.Model.Get(id)!.Attrs["inputs"]!["situatie"]);

        var list = (JsonArray)f.Session.Query("document.list", [])!;
        Assert.Equal(["Memoriu arhitectură", "Memoriu arhitectură (copy)"], list.Select(x => (string)x!["name"]!));
        Assert.Equal("test-house", (string)list[0]!["template"]!);
        f.Run("document.delete", new JsonObject { ["id"] = copy });
        Assert.Null(f.Model.Get(copy));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(copy));
        Assert.Throws<CommandException>(() => f.Run("document.create", new JsonObject { ["template"] = "no-such-template" }));
    }

    [Fact]
    public void Fields_list_every_key_with_its_value_the_collections_and_the_tables()
    {
        var (f, storeys) = House();
        var id = Create(f);
        var r = (JsonObject)f.Session.Query("document.fields", new JsonObject { ["id"] = id })!;
        var fields = r["fields"]!.AsArray().OfType<JsonObject>().ToDictionary(x => (string)x["key"]!, x => (string?)x["value"]);
        Assert.Equal("Test house", fields["document.name"]);
        Assert.Equal("S+P+1E+M", fields["levels.text"]);
        Assert.Null(fields["project.client"]);
        Assert.Equal("4", fields["counts.storeys"]);
        Assert.Equal("3", fields["counts.rooms"]);
        Assert.Equal("PRJ", fields["cde.project"]);
        Assert.True(fields.ContainsKey("date.today"));
        var collections = r["collections"]!.AsArray().OfType<JsonObject>().ToDictionary(x => (string)x["key"]!);
        Assert.Equal("room", (string)collections["rooms"]["itemPrefix"]!);
        Assert.Equal(3, (int)collections["rooms"]["count"]!);
        // The storeys' items carry the architecture's areas, merged by id.
        var storeyFields = collections["storeys"]["fields"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Contains("area", storeyFields);
        Assert.Contains("usable", storeyFields);
        var tables = r["tables"]!.AsArray().Select(x => (string)x!["source"]!).ToList();
        Assert.Contains("kind:rooms", tables);
        Assert.Contains("kind:door", tables);

        // A room's finishes, from its room finish's layers.
        var living = f.Model.OfClass(Rooms.Class).Single(x => x.Name == "Living");
        var finish = (string)f.Run("architecture.roomFinishTypeFromLibrary", new JsonObject { ["name"] = "Living room" })!["id"]!;
        f.Run("architecture.applyRoomFinish", new JsonObject { ["roomIds"] = new JsonArray(living.Id), ["typeId"] = finish });
        var room = DocumentFill.Items(f.Session.Reader, "rooms", Ro).Single(x => x["id"] == living.Id);
        Assert.Equal("Laminate 8 mm + Underlay 2 mm + Screed 5 cm", room["floor"]);
        Assert.Equal("Plaster 1,5 cm + Paint 1 mm", room["walls"]);
        Assert.Equal(storeys["P"], room["storeyId"]);
        Assert.Contains(DocumentFill.Items(f.Session.Reader, "materials", Ro), x => x["name"] == "Laminate");
        Assert.Contains(DocumentFill.Items(f.Session.Reader, "joineryTypes", Ro), x => x["kind"] == "door" && x["count"] == "1");

        // A schedule of the project, by its id, as it is laid out.
        var schedule = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "door", ["name"] = "Uși" })!["id"]!;
        Assert.Contains($"schedule:{schedule}", ((JsonObject)f.Session.Query("document.fields", [])!)["tables"]!.AsArray().Select(x => (string)x!["source"]!));
        var grid = f.Session.Reader.Services.Get<IDocumentTables>()!.Table(f.Session.Reader, $"schedule:{schedule}")!;
        Assert.Equal("Uși", grid.Title);
        Assert.Single(grid.Rows);
    }

    [Fact]
    public void The_preview_gives_blocks_runs_and_missing_fields_as_json_and_an_export_names_its_file()
    {
        var (f, _) = House();
        var id = Create(f);
        var p = (JsonObject)f.Session.Query("document.preview", new JsonObject { ["id"] = id })!;
        Assert.Equal(210, (double)p["page"]!["w"]!);
        var blocks = p["blocks"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal("heading", (string)blocks[0]["kind"]!);
        Assert.Equal(1, (int)blocks[0]["level"]!);
        var runs = blocks[1]["runs"]!.AsArray().OfType<JsonObject>().ToList();
        var client = runs.Single(r => (string)r["text"]! == "project.client");
        Assert.True((bool)client["missing"]!);
        Assert.True((bool)client["bold"]!);
        Assert.Null(runs[0]["missing"]);
        Assert.Contains(blocks, b => (string)b["kind"]! == "table" && (bool)b["plain"]!);
        Assert.Equal(["project.client", "project.address", "input.situatie"], p["missing"]!.AsArray().Select(x => (string)x!));
        Assert.Equal("field", p["page"]!["footer"]!.AsArray().OfType<JsonObject>().Where(r => r["field"] != null).Select(_ => "field").First());

        // The export: the document filled and written by the format's writer, its file named after the document.
        f.Run("document.update", new JsonObject { ["id"] = id, ["name"] = "Memoriu: arhitectură / DTAC" });
        FilledDocument? written = null;
        IReadOnlyDictionary<string, DocPicture>? given = null;
        var r = DocumentExport.Export(f.Session.Reader, f.Model.Get(id)!, "DOCX", new JsonObject { ["img-1"] = new JsonObject { ["svg"] = "<svg/>", ["png"] = Convert.ToBase64String([1, 2, 3]) } },
            (doc, format, pictures) => { written = doc; given = pictures; return Encoding.UTF8.GetBytes(format); });
        Assert.Equal("Memoriu_ arhitectură _ DTAC.docx", (string)r["name"]!);
        Assert.Equal("docx", Encoding.UTF8.GetString(Convert.FromBase64String((string)r["data"]!)));
        Assert.Equal("Memoriu: arhitectură / DTAC", written!.Title);
        Assert.Equal([1, 2, 3], given!["img-1"].Png);
        Assert.Equal("<svg/>", given["img-1"].Svg);
        Assert.Equal(3, r["missing"]!.AsArray().Count);
        Assert.Throws<CommandException>(() => DocumentExport.Export(f.Session.Reader, f.Model.Get(id)!, "rtf", null));
        Assert.Equal("Document.pdf", DocumentExport.FileName("  ", "pdf"));
    }

    // ------------------------------------------------------------ the project's file and the issue

    [Fact]
    public void A_document_is_saved_in_the_project_file_and_opened_again_with_its_inputs()
    {
        var (f, _) = House();
        var id = Create(f);
        f.Run("document.update", new JsonObject { ["id"] = id, ["inputs"] = new JsonObject { ["situatie"] = "Teren liber." } });
        f.Run("project.setInfo", new JsonObject { ["client"] = "Ion Popescu" });
        var path = Path.Combine(_dir, "house.lapin");
        PackageFiles.Save(f.Session, path);

        var g = new Fixture();
        PackageFiles.Open(g.Session, path);
        var doc = Assert.Single(Documents.All(g.Model));
        Assert.Equal(id, doc.Id);
        Assert.Equal("Teren liber.", (string?)doc.Attrs["inputs"]!["situatie"]);
        Assert.Equal("test-house", doc.Str("template"));
        Assert.Equal("Ion Popescu", ProjectInfo.Get(g.Model, "client"));
        var (d, missing) = DocumentFill.Fill(g.Session.Reader, doc);
        Assert.Equal(["project.address"], missing);
        Assert.Equal("S+P+1E+M", Field(d, "Regim de înălțime"));
    }

    [Fact]
    public void An_issue_writes_each_document_named_by_its_type_lists_it_and_gives_it_the_revision_and_status()
    {
        var (f, _) = House();
        var id = Create(f);
        var files = PackageFiles.Create();
        var written = new List<(string Format, string Revision, string Status)>();
        var (r, _) = CdeIssue.Run(f.Session, files.Issues(f.Session),
            new JsonObject { ["sheets"] = new JsonArray(), ["documents"] = new JsonArray(id), ["documentsDocx"] = true, ["revision"] = "P01", ["status"] = "S2", ["toFolder"] = false },
            null, (doc, format, _) => { written.Add((format, doc.Meta.Revision, doc.Meta.Status)); return Encoding.UTF8.GetBytes($"{format} {doc.Title}"); });

        // Each written as the issue sends it: its revision and status in its properties.
        Assert.Equal([("pdf", "P01", "S2"), ("docx", "P01", "S2")], written);
        var issue = Assert.Single(CdeIssues.All(f.Model));
        var docs = issue.Files.Where(x => x.Kind == "document").ToList();
        Assert.Equal(["PRJ-ORG-ZZ-ZZ-RP-A-0001_S2_P01.pdf", "PRJ-ORG-ZZ-ZZ-RP-A-0001_S2_P01.docx"], docs.Select(x => x.Name));
        Assert.All(docs, x => Assert.Equal(id, x.DocumentId));
        Assert.All(docs, x => Assert.True(x.Stored));
        Assert.Equal(CdeTransmittal.Sha256(Encoding.UTF8.GetBytes("pdf Test house")), docs[0].Sha256);
        // In the transmittal's manifest, with their hashes; the document now at the issue's revision and status.
        var manifest = issue.Files.Single(x => x.Kind == "json");
        Assert.NotNull(manifest);
        Assert.Equal(("P01", "S2"), (f.Model.Get(id)!.Str("revision"), f.Model.Get(id)!.Str("status")));
        Assert.Equal("P01", Fill(f, id).Doc.Meta.Revision);
        Assert.Equal("document", (string)((JsonArray)r["files"]!).OfType<JsonObject>().First()["kind"]!);
        // The names the issue dialog shows.
        var names = (JsonObject)f.Session.Query("cde.naming", new JsonObject { ["documents"] = new JsonArray(id) })!;
        Assert.Equal("PRJ-ORG-ZZ-ZZ-RP-A-0001", (string)names["documents"]![0]!["name"]!);
        Assert.Empty(names["documents"]![0]!["problems"]!.AsArray());
        // Nothing asked: refused.
        Assert.Throws<CommandException>(() => CdeIssue.Run(f.Session, files.Issues(f.Session), new JsonObject { ["sheets"] = new JsonArray() }));
    }

    [Fact]
    public void A_picture_cropped_to_a_boundary_is_the_boundary_at_its_scale_as_a_sheet_shows_it()
    {
        var (f, storeys) = House();
        var id = Create(f);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storeys["P"], ["name"] = "Plan parter", ["scale"] = 50 })!["id"]!;
        var other = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storeys["E1"], ["name"] = "Plan etaj", ["scale"] = 50 })!["id"]!;
        // A corner of the house, 4 × 3 m of its 10 × 8.
        var corner = (string)f.Run("view.addBoundary", new JsonObject { ["viewId"] = plan, ["a"] = new JsonArray(-0.5, -0.5), ["b"] = new JsonArray(3.5, 2.5), ["name"] = "Colț" })!["id"]!;
        var elsewhere = (string)f.Run("view.addBoundary", new JsonObject { ["viewId"] = other, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(2, 2) })!["id"]!;
        var blocks = new JsonArray(
            new JsonObject { ["kind"] = "image", ["view"] = plan, ["boundary"] = corner },
            new JsonObject { ["kind"] = "image", ["view"] = plan, ["boundary"] = corner, ["scale"] = 100 },
            new JsonObject { ["kind"] = "image", ["view"] = plan, ["boundary"] = corner, ["width"] = 60 },
            new JsonObject { ["kind"] = "image", ["view"] = plan });
        f.Run("document.update", new JsonObject { ["id"] = id, ["blocks"] = blocks });
        var images = Fill(f, id).Doc.Blocks.OfType<DocImage>().ToList();
        Assert.Equal(4, images.Count);
        // At the view's 1:50: 4 m → 80 mm, 3 m → 60 mm, exactly the boundary (no margin, as a bound viewport's box).
        Assert.Equal(80, images[0].W, 3);
        Assert.Equal(60, images[0].H, 3);
        Assert.Equal(40, images[1].W, 3);
        Assert.Equal(30, images[1].H, 3);
        Assert.Equal(60, images[2].W, 3);
        Assert.Equal(45, images[2].H, 3);
        // Cropped: every pen inside the box, and less drawn than the whole plan.
        var pts = images[1].Pens.SelectMany(p => p.Segments.SelectMany(x => new[] { x.A, x.B }).Concat(p.Loops.SelectMany(l => l))).ToList();
        Assert.NotEmpty(pts);
        Assert.True(pts.All(p => p.X >= -1e-6 && p.Y >= -1e-6 && p.X <= images[1].W + 1e-6 && p.Y <= images[1].H + 1e-6));
        Assert.True(images[3].W > images[0].W);
        Assert.NotEqual(images[0].Key, images[3].Key);

        // A boundary of another view is refused.
        var wrong = new JsonArray(new JsonObject { ["kind"] = "image", ["view"] = plan, ["boundary"] = elsewhere });
        Assert.Throws<CommandException>(() => f.Run("document.update", new JsonObject { ["id"] = id, ["blocks"] = wrong }));
    }

    [Fact]
    public void An_issued_docx_carries_the_pictures_the_interface_made()
    {
        var (f, storeys) = House();
        var id = Create(f);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storeys["P"], ["name"] = "Plan parter", ["scale"] = 100 })!["id"]!;
        f.Run("document.update", new JsonObject { ["id"] = id, ["blocks"] = new JsonArray(new JsonObject { ["kind"] = "image", ["view"] = plan, ["caption"] = "Plan" }) });
        var key = Assert.Single(Fill(f, id).Doc.Blocks.OfType<DocImage>()).Key;
        var png = DocPng.Encode(2, 2, [.. Enumerable.Repeat((byte)128, 12)]);
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10mm\" height=\"10mm\"/>";
        byte[]? docx = null;
        CdeIssue.Run(f.Session, PackageFiles.Create().Issues(f.Session),
            new JsonObject
            {
                ["sheets"] = new JsonArray(), ["documents"] = new JsonArray(id), ["documentsDocx"] = true, ["revision"] = "P01", ["status"] = "S2", ["toFolder"] = false,
                ["documentImages"] = new JsonObject { [id] = new JsonObject { [key] = new JsonObject { ["svg"] = svg, ["png"] = Convert.ToBase64String(png) } } },
            },
            null, (doc, format, pictures) => { var b = DocumentExport.Write(doc, format, pictures); if (format == "docx") docx = b; return b; });
        Assert.NotNull(docx);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(docx!));
        var media = zip.Entries.Where(e => e.FullName.StartsWith("word/media/", StringComparison.Ordinal)).ToDictionary(e => e.FullName);
        Assert.Contains("word/media/image1.png", media.Keys);
        Assert.Contains("word/media/image1.svg", media.Keys);
        using var ms = new MemoryStream();
        media["word/media/image1.png"].Open().CopyTo(ms);
        Assert.Equal(png, ms.ToArray());
    }
}
