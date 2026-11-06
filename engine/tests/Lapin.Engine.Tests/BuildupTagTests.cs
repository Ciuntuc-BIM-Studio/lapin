using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage 2 of docs/plan-buildup-tags.md: the build-up tag. Its template's fields and optional parts; a layered wall's
/// layers written exterior to interior with their materials' descriptions and λ, the other way, a flipped wall, layers of
/// no thickness shown or not; per-layer leaders each ending inside its own layer; a floor top to bottom, in plan and in a
/// section; the tag following its wall; the tag type through JSON and back.
/// </summary>
public class BuildupTagTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonObject Draw(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonObject Mark(JsonObject d, string id) => d["marks"]!.AsArray().OfType<JsonObject>().First(m => (string)m["id"]! == id);
    /// <summary>The texts drawn, in order, without the bullets.</summary>
    static List<string> Texts(JsonObject d) => d["pens"]!.AsArray().OfType<JsonObject>().Where(p => p["texts"] != null)
        .SelectMany(p => p["texts"]!.AsArray()).Select(t => (string)t!["text"]!).Where(t => t != "•").ToList();
    /// <summary>The mark's snaps but its own point (the last): where its leaders end.</summary>
    static List<Vec2> Ends(JsonObject mark)
    {
        var s = mark["snaps"]!.AsArray().Select(x => (double)x!).ToList();
        return Enumerable.Range(0, s.Count / 2 - 1).Select(i => new Vec2(s[2 * i], s[2 * i + 1])).ToList();
    }

    static string Mat(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;

    static void Data(Fixture f, string material, string description, double? lambda)
    {
        var a = new JsonObject { ["id"] = Mat(f, material), ["description"] = description };
        if (lambda is { } l) a["params"] = new JsonArray(new JsonObject { ["pset"] = "Pset_MaterialThermal", ["name"] = "ThermalConductivity", ["value"] = l });
        f.Run("material.setData", a);
    }

    /// <summary>A wall 5 m along x, its five layers (a vapour barrier of no thickness among them), its materials described.</summary>
    static (Fixture F, string Wall, string View) Wall()
    {
        var f = F();
        var type = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Insulated brick",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Render", ["thickness"] = 0.02 },
                new JsonObject { ["material"] = "Mineral wool", ["thickness"] = 0.1 },
                new JsonObject { ["material"] = "Vapour barrier", ["thickness"] = 0 },
                new JsonObject { ["material"] = "Brick", ["thickness"] = 0.25, ["core"] = true },
                new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.015 }),
        })!["id"]!;
        Data(f, "Render", "Lime render", null);
        Data(f, "Mineral wool", "Mineral wool boards", 0.035);
        Data(f, "Brick", "Solid brick", 0.8);
        Data(f, "Plaster", "Gypsum plaster", null);
        var w = f.Wall(0, 0, 5, 0, type);
        return (f, w, "plan:" + f.Storey);
    }

    static string Tag(Fixture f, string view, string element, double x, double y) =>
        (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = element, ["tagType"] = "buildup", ["at"] = new JsonArray(x, y) })!["id"]!;

    static void Set(Fixture f, JsonObject entry) { entry["id"] = "buildup"; f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = entry }); }

    static readonly string[] Written =
    [
        "EXTERIOR",
        "Lime render: 20 mm",
        "Mineral wool boards, λ = 0.035 W/mK: 100 mm",
        "Vapour barrier: 0 mm",
        "Solid brick, λ = 0.8 W/mK: 250 mm",
        "Gypsum plaster: 15 mm",
        "INTERIOR",
    ];

    [Fact]
    public void The_template_fills_its_fields_drops_an_optional_part_with_an_empty_one_and_keeps_escaped_brackets()
    {
        var values = new Dictionary<string, string?> { ["a"] = "A", ["b"] = "", ["λ"] = "0.04" };
        string? V(string k) => values.GetValueOrDefault(k);
        Assert.Equal("A, λ = 0.04", BuildupTemplate.Fill("{a}[, λ = {λ}]", V));
        Assert.Equal("A", BuildupTemplate.Fill("{a}[, μ = {μ}]", V));
        // Nested: the inner part goes alone, the outer stays.
        Assert.Equal("A (x)", BuildupTemplate.Fill("{a}[ (x[, {b}])]", V));
        // A part without fields stays; an empty field outside brackets writes nothing.
        Assert.Equal("[A] is: ", BuildupTemplate.Fill("\\[{a}\\] is: {b}[{c}]", V));
        Assert.Equal(["A", "B|C"], BuildupTemplate.Lines("{a}|B\\|C|  |", V));
        // An unclosed brace is text; an unclosed bracket ends with the template.
        Assert.Equal("{a A", BuildupTemplate.Fill("{a [{a}", V));
    }

    [Fact]
    public void A_line_wraps_between_words_never_inside_a_value_with_its_symbol_and_unit()
    {
        Assert.Equal(["Mineral", "wool,", "λ = 0.035 W/mK:", "150 mm"], BuildupText.Words("Mineral wool, λ = 0.035 W/mK: 150 mm"));
        Assert.Equal(["Blocks", "25 cm,", "M10", "mortar"], BuildupText.Words("Blocks 25 cm, M10 mortar"));
        var lines = BuildupText.Wrap("Mineral wool boards, fixed with dowels, λ = 0.035 W/mK: 150 mm", 70, 2.5, false);
        Assert.True(lines.Count > 1);
        Assert.Contains(lines, l => l.Contains("λ = 0.035 W/mK:"));
        Assert.All(lines, l => Assert.True(BuildupText.Measure(l, 2.5, false) <= 70));
    }

    [Fact]
    public void The_default_build_up_tag_writes_a_wall_s_layers_exterior_to_interior_with_descriptions_and_lambda()
    {
        var (f, w, view) = Wall();
        // Its straight leader: the block's top left at the tag's point (an elbow's is in TagLeaderTests).
        Set(f, new JsonObject { ["leaderShape"] = "straight" });
        var t = Tag(f, view, w, 1, 3);
        var d = Draw(f, view);
        Assert.Equal(Written, Texts(d));
        // Left-aligned from the tag's point, header and layers and footer each lower than the one before.
        var texts = d["pens"]!.AsArray().OfType<JsonObject>().Where(p => p["texts"] != null).SelectMany(p => p["texts"]!.AsArray()).OfType<JsonObject>().ToList();
        Assert.All(texts.Where(x => (string)x["text"]! is "EXTERIOR" or "INTERIOR"), x => Assert.Equal(1, (double)x["x"]!, 6));
        var ys = texts.Where(x => (string)x["text"]! != "•").Select(x => (double)x["y"]!).ToList();
        Assert.True(ys.Zip(ys.Skip(1)).All(p => p.First > p.Second));
        Assert.All(texts, x => Assert.Equal("start", (string)x["anchor"]!));
        // One leader, to the wall's middle.
        var ends = Ends(Mark(d, t));
        Assert.Single(ends);
        Assert.Equal(2.5, ends[0].X, 6);
        Assert.Equal(0, ends[0].Y, 6);

        // The other way: interior to exterior.
        Set(f, new JsonObject { ["order"] = "in-out" });
        Assert.Equal([Written[0], .. Written[1..^1].Reverse(), Written[^1]], Texts(Draw(f, view)));
        // A floor's word for the same order is read as the wall's.
        Set(f, new JsonObject { ["order"] = "top-down" });
        Assert.Equal(Written, Texts(Draw(f, view)));

        // Layers of no thickness hidden: the vapour barrier goes.
        Set(f, new JsonObject { ["zeroLayers"] = false });
        Assert.DoesNotContain("Vapour barrier: 0 mm", Texts(Draw(f, view)));
        Assert.Equal(6, Texts(Draw(f, view)).Count);

        // A template of the user's: the note, the function, an optional μ the materials do not have; centimetres.
        Set(f, new JsonObject { ["zeroLayers"] = true, ["template"] = "{index}. {material}[ μ = {μ}] ({function}): {thickness} cm", ["unit"] = "cm" });
        var own = Texts(Draw(f, view));
        Assert.Contains("1. Render (finish): 2 cm", own);
        Assert.Contains("4. Brick (core): 25 cm", own);
    }

    [Fact]
    public void A_flipped_wall_keeps_its_exterior_first_and_its_per_layer_leaders_end_inside_their_layers()
    {
        var (f, w, view) = Wall();
        Set(f, new JsonObject { ["leaders"] = "per-layer" });
        var t = Tag(f, view, w, 1, 3);
        void Check()
        {
            var d = Draw(f, view);
            var ends = Ends(Mark(d, t));
            var bands = LayerSets.BandsOf(f.Model, w)!;
            // One leader per line, in the lines' order (the layers'), each inside its own band across the axis (+y left of it).
            Assert.Equal(5, ends.Count);
            for (var i = 0; i < 5; i++)
            {
                var (lo, hi) = (bands[i].From, bands[i].To);
                Assert.InRange(ends[i].Y, lo - 1e-9, hi + 1e-9);
                if (hi - lo > 1e-9) Assert.True(ends[i].Y > lo && ends[i].Y < hi);
                Assert.Equal(2.5, ends[i].X, 6);
            }
            // The leaders leave the block's side facing the wall (the tag stands above it, to the left of the leaders' ends: its right side).
            Assert.Equal(Written, Texts(d));
        }
        Check();
        var before = Ends(Mark(Draw(f, view), t));
        // Flipped across its axis: the first layer, the exterior, is on its left now; the text still starts there.
        f.Run("architecture.flipWall", new JsonObject { ["ids"] = new JsonArray(w) });
        Check();
        var after = Ends(Mark(Draw(f, view), t));
        Assert.True(before[0].Y < 0 && after[0].Y > 0);
        Assert.Equal(before[0].Y, -after[0].Y, 6);

        // Dragged along the wall: the leaders cross it there.
        f.Run("drafting.updateTag", new JsonObject { ["id"] = t, ["end"] = new JsonArray(4, 0.05) });
        Assert.All(Ends(Mark(Draw(f, view), t)), p => Assert.Equal(4, p.X, 6));
        // The zero layer hidden: one leader fewer.
        Set(f, new JsonObject { ["zeroLayers"] = false });
        Assert.Equal(4, Ends(Mark(Draw(f, view), t)).Count);
    }

    [Fact]
    public void The_tag_follows_its_wall()
    {
        var (f, w, view) = Wall();
        Set(f, new JsonObject { ["leaders"] = "per-layer" });
        var t = Tag(f, view, w, 1, 3);
        var was = Ends(Mark(Draw(f, view), t));
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(w), ["op"] = "move", ["by"] = new JsonArray(0, -3) });
        var d = Draw(f, view);
        var at = (JsonArray)Mark(d, t)["data"]!["at"]!;
        Assert.Equal(1, (double)at[0]!, 6);
        Assert.Equal(0, (double)at[1]!, 6);
        var now = Ends(Mark(d, t));
        Assert.Equal(was.Count, now.Count);
        for (var i = 0; i < was.Count; i++) Assert.Equal(was[i].Y - 3, now[i].Y, 6);
        Assert.Equal(Written, Texts(d));
    }

    [Fact]
    public void A_floor_is_written_top_to_bottom_and_its_per_layer_leaders_in_a_section_end_inside_their_layers()
    {
        var f = F();
        var type = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Floor",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Parquet", ["thickness"] = 0.015 },
                new JsonObject { ["material"] = "Screed", ["thickness"] = 0.05 },
                new JsonObject { ["material"] = "Concrete", ["thickness"] = 0.15, ["core"] = true }),
        })!["id"]!;
        var slab = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["typeId"] = type,
            ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(4, 0), new JsonArray(4, 4), new JsonArray(0, 4)),
        })!["id"]!;
        Set(f, new JsonObject { ["header"] = "", ["footer"] = "", ["leaders"] = "per-layer" });
        var plan = "plan:" + f.Storey;
        var t = Tag(f, plan, slab, 5, 5);
        var d = Draw(f, plan);
        Assert.Equal(["Parquet: 15 mm", "Screed: 50 mm", "Concrete: 150 mm"], Texts(d));
        // In plan a floor's layers lie on each other: one leader.
        Assert.Single(Ends(Mark(d, t)));
        Set(f, new JsonObject { ["order"] = "bottom-up" });
        Assert.Equal(["Concrete: 150 mm", "Screed: 50 mm", "Parquet: 15 mm"], Texts(Draw(f, plan)));
        Set(f, new JsonObject { ["order"] = "out-in" });

        // In a section across it: a leader into each layer, top first, at the middle of each.
        var s = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(2, -1), ["end"] = new JsonArray(2, 5) })!["id"]!;
        var ts = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = s, ["element"] = slab, ["tagType"] = "buildup", ["at"] = new JsonArray(-3, 2) })!["id"]!;
        var ends = Ends(Mark(Draw(f, s), ts));
        Assert.Equal(3, ends.Count);
        Assert.Equal((0.015 + 0.05) / 2, ends[0].Y - ends[1].Y, 6);
        Assert.Equal((0.05 + 0.15) / 2, ends[1].Y - ends[2].Y, 6);
        Assert.All(ends, p => Assert.Equal(ends[0].X, p.X, 6));
    }

    [Fact]
    public void A_build_up_tag_type_goes_through_json_and_back_and_is_never_taken_unasked()
    {
        var book = Standards.Read(null);
        var tag = book.TagTypes.Single(t => t.Shape == "buildup");
        Assert.Equal(Standards.DefaultBuildupTag, tag);
        var own = tag with { Id = "mine", Header = "OUTSIDE", Order = "in-out", ZeroLayers = false, Leaders = "per-layer", Bullet = "number", Width = 60 };
        var back = Standards.Read(Standards.ToJson(book with { TagTypes = [.. book.TagTypes, own] })).TagTypes.Single(t => t.Id == "mine");
        Assert.Equal(own, back);
        Assert.True(back.Reversed && !back.ShowsZeroLayers && back.LeaderPerLayer);
        Assert.Equal(60, back.WidthOrDefault);
        // The other shapes write none of it.
        var json = Standards.ToJson(book);
        var door = json["tagTypes"]!.AsArray().OfType<JsonObject>().Single(t => (string?)t["id"] == "door");
        Assert.Null(door["header"]);
        Assert.Null(door["reversed"]);
        // Tagged without a type, a wall shows its mark: a build-up tag is chosen, never assumed.
        var (f, w, view) = Wall();
        var t = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = view, ["element"] = w, ["at"] = new JsonArray(1, 3) })!["id"]!;
        Assert.Null(f.Model.Get(t)!.Str("tagType"));
        // The editor's preview: a wall type's build-up laid out on paper.
        var preview = (JsonObject)f.Session.Query("drafting.buildupPreview", new JsonObject { ["type"] = f.Model.RelatingOf(w, IfcClass.RelDefinesByType)!.Id, ["entry"] = Standards.ToJson(book)["tagTypes"]!.AsArray().OfType<JsonObject>().Single(x => (string?)x["shape"] == "buildup").DeepClone() })!;
        Assert.Equal(Written, preview["texts"]!.AsArray().Select(x => (string)x!["text"]!).Where(x => x != "•").ToArray());
    }

    [Fact]
    public void A_lambda_prints_on_paper_in_the_symbol_font()
    {
        var pen = new Pen("TEXT", "#000000", 0.18, null, null);
        pen.Texts.Add(new PenText(new Vec2(10, 10), "Mineral wool, λ = 0.035 W/mK", 2.5, Anchor: "start"));
        var pdf = PdfWriter.Write([new PaperPage(100, 100, [pen])]);
        var text = System.Text.Encoding.Latin1.GetString(pdf);
        Assert.Contains("/BaseFont /Symbol", text);
        var at = text.IndexOf("stream\n", StringComparison.Ordinal) + 7;
        var length = int.Parse(System.Text.RegularExpressions.Regex.Match(text, @"/Length (\d+) /Filter /FlateDecode").Groups[1].Value);
        using var z = new System.IO.Compression.ZLibStream(new MemoryStream(pdf, at, length), System.IO.Compression.CompressionMode.Decompress);
        var content = new StreamReader(z, System.Text.Encoding.Latin1).ReadToEnd();
        Assert.Contains("(Mineral wool, ) Tj /F5", content);
        Assert.Contains("(l) Tj /F1", content);
        Assert.Contains("( = 0.035 W/mK) Tj ET", content);
        // A page without Greek letters has no Symbol font.
        var plain = new Pen("TEXT", "#000000", 0.18, null, null);
        plain.Texts.Add(new PenText(new Vec2(10, 10), "EXTERIOR", 2.5));
        Assert.DoesNotContain("/Symbol", System.Text.Encoding.Latin1.GetString(PdfWriter.Write([new PaperPage(100, 100, [plain])])));
    }
}
