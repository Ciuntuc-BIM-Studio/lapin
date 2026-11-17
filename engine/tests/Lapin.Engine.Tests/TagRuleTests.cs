using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Issue #72 (forum topic 114): a door's or window's marker drawn by a symbol of the user's, its texts the opening's fields; the
/// tag rules, per class and in their order, a condition formula giving a tag type over the one a tag was made with (first that
/// holds wins), a tag's own choice keeping its type, a tag switched off by itself; the rules kept in the standards, their file
/// and the project. Without a symbol or a rule that holds, the marker is drawn exactly as before.
/// </summary>
public sealed class TagRuleTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    /// <summary>A wall along X with a door at 1.5 m and a window at 4 m, its plan, both tagged by Annotate → Door &amp; window tags.</summary>
    static (Fixture F, string Door, string Window, string Plan) Project()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4 })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("architecture.tagOpenings", new JsonObject { ["viewId"] = plan });
        return (f, door, window, plan);
    }

    static Views.Rendered Render(Fixture f, string plan, bool print = true) => Views.Render(new Q(f), Views.Find(f.Model, plan)!, print: print);
    static List<string> Texts(Fixture f, string plan) => Render(f, plan).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();

    /// <summary>Everything the plan prints, written out: each pen's layer, colour, width, segments and texts.</summary>
    static string Snapshot(Fixture f, string plan) => string.Join("\n", Render(f, plan).Pens.Select(p =>
        $"{p.Layer}|{p.Stroke}|{p.Width}|{string.Join(";", p.Segments.Select(s => $"{s.A.X:0.######},{s.A.Y:0.######},{s.B.X:0.######},{s.B.Y:0.######}"))}|{string.Join(";", p.Texts.Select(t => $"{t.Text}@{t.At.X:0.######},{t.At.Y:0.######},{t.Angle:0.###}"))}"));

    static string TagOf(Fixture f, string element) => f.Model.OfClass(IfcClass.Annotation).Single(t => t.Str("element") == element).Id;

    static void Rule(Fixture f, string id, string cls, string when, string type) =>
        f.Run("standards.set", new JsonObject { ["kind"] = "tagRule", ["entry"] = new JsonObject { ["id"] = id, ["class"] = cls, ["when"] = when, ["tagType"] = type } });

    static string DoorSymbol(Fixture f) => (string)f.Run("symbols.create", new JsonObject
    {
        ["name"] = "Door hexagon",
        ["def"] = new JsonObject
        {
            ["scale"] = "annotation", ["category"] = "annotation",
            ["items"] = new JsonArray(
                new JsonObject { ["type"] = "rect", ["x"] = -6, ["y"] = -3, ["w"] = 12, ["h"] = 6 },
                new JsonObject { ["type"] = "text", ["at"] = new JsonArray(0, 0), ["text"] = "D·{mark}·{width}", ["h"] = 2, ["align"] = "middle", ["valign"] = "middle" }),
        },
    })!["id"]!;

    [Fact]
    public void Without_a_symbol_or_a_rule_that_holds_the_markers_are_drawn_as_before_and_the_standards_keep_their_shape()
    {
        var (f, _, _, plan) = Project();
        var before = Snapshot(f, plan);
        // The standards a project starts with write no rules (a project saved before them reads the same).
        Assert.Null(Standards.ToJson(DraftingResources.Book(f.Model))["tagRules"]);
        Assert.Null(DraftingResources.Book(f.Model).TagRules);
        // A rule that does not hold, and one of another class, change nothing.
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "door-wide", ["name"] = "Wide door", ["template"] = "W{mark}", ["classes"] = "", ["shape"] = "opening", ["inner"] = "{width}" } });
        Rule(f, "r1", "IfcDoor", "width > 5", "door-wide");
        Rule(f, "r2", "IfcSlab", "1", "door-wide");
        Assert.Equal(before, Snapshot(f, plan));
        Assert.Equal(2, DraftingResources.Book(f.Model).TagRules!.Count);
    }

    [Fact]
    public void A_door_marker_drawn_by_a_symbol_fills_its_texts_from_the_door_and_keeps_its_line_and_inner_text()
    {
        var (f, door, _, plan) = Project();
        var mark = TagText.Mark(f.Model, f.Model.Get(door)!);
        var p = Openings.Place(f.Model, Openings.OpeningOf(f.Model, door)!);
        var width = TagText.Length(p.Spec?.Width ?? p.Width, "cm");
        var lineCount = Render(f, plan).Pens.Where(x => x.Layer == "TAG").Sum(x => x.Segments.Count);
        var symbol = DoorSymbol(f);
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "door", ["symbol"] = symbol } });
        var texts = Texts(f, plan);
        // The symbol's text, its fields the door's (its width as the marker writes it, in cm); the outer template not drawn as well.
        Assert.Contains($"D·{mark}·{width}", texts);
        Assert.DoesNotContain(mark, texts);
        // The inner text stays, and the line across the wall with it; the symbol's four sides are drawn too.
        Assert.Contains(texts, t => t.StartsWith(width + "/", StringComparison.Ordinal));
        Assert.Equal(lineCount + 4, Render(f, plan).Pens.Where(x => x.Layer == "TAG").Sum(x => x.Segments.Count));
        // The symbol stands beyond the marker's exterior end, clear of the wall (the wall is 0.3 m thick or less, at 1:100).
        var at = Render(f, plan).Pens.SelectMany(x => x.Texts).Single(t => t.Text.StartsWith("D·", StringComparison.Ordinal)).At;
        Assert.True(Math.Abs(at.Y) > 0.6, $"{at}");
        // Its part o0 has grips as a text line has: moved, it follows.
        var drawing = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        var tagMark = drawing["marks"]!.AsArray().OfType<JsonObject>().Single(m => (string?)m["id"] == TagOf(f, door));
        Assert.Contains(tagMark["data"]!["parts"]!.AsArray().OfType<JsonObject>(), x => (string?)x["key"] == "o0");
        f.Run("drafting.updateTag", new JsonObject { ["id"] = TagOf(f, door), ["part"] = "o0", ["offset"] = new JsonArray(10, 0) });
        var moved = Render(f, plan).Pens.SelectMany(x => x.Texts).Single(t => t.Text.StartsWith("D·", StringComparison.Ordinal)).At;
        Assert.Equal(1.0, (moved - at).Length, 3);
        // The symbol deleted: the mark is the template's text again.
        f.Run("symbols.delete", new JsonObject { ["id"] = symbol });
        Assert.Contains(mark, Texts(f, plan));
    }

    [Fact]
    public void The_first_rule_of_the_class_that_holds_gives_the_type_and_a_tag_s_own_choice_keeps_it()
    {
        var (f, door, window, plan) = Project();
        var mark = TagText.Mark(f.Model, f.Model.Get(door)!);
        var windowMark = TagText.Mark(f.Model, f.Model.Get(window)!);
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "door-wide", ["name"] = "Wide door", ["template"] = "W{mark}", ["shape"] = "opening", ["classes"] = "" } });
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "door-fire", ["name"] = "Fire door", ["template"] = "F{mark}", ["shape"] = "opening", ["classes"] = "" } });
        // In metres: the door is wider than half a metre, not than five.
        Rule(f, "huge", "IfcDoor", "width > 5", "door-fire");
        Rule(f, "wide", "IfcDoor", "{= width > 0.5}", "door-wide");
        var texts = Texts(f, plan);
        Assert.Contains("W" + mark, texts);
        Assert.DoesNotContain(mark, texts);
        // The window has no rule: its own type.
        Assert.Contains(windowMark, texts);
        // A rule on a parameter, put first: it wins over the one after it.
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(door), ["name"] = "Fire rating", ["kind"] = "text", ["value"] = "EI30" });
        Rule(f, "fire", "IfcDoor", "FireRating == \"EI30\"", "door-fire");
        Assert.Contains("W" + mark, Texts(f, plan));
        f.Run("standards.move", new JsonObject { ["kind"] = "tagRule", ["id"] = "fire", ["index"] = 0 });
        Assert.Equal(["fire", "huge", "wide"], DraftingResources.Book(f.Model).TagRules!.Select(r => r.Id));
        Assert.Contains("F" + mark, Texts(f, plan));
        // Followed on every draw: the rating changed, the next rule that holds.
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(door), ["name"] = "Fire rating", ["value"] = "EI60" });
        Assert.Contains("W" + mark, Texts(f, plan));
        // A rule may give a box: the door tagged by its rating in a box, its marker gone.
        Rule(f, "box", "IfcDoor", "FireRating != \"\"", "fire");
        f.Run("standards.move", new JsonObject { ["kind"] = "tagRule", ["id"] = "box", ["index"] = 0 });
        texts = Texts(f, plan);
        Assert.Contains("EI60", texts);
        Assert.DoesNotContain("W" + mark, texts);

        // Chosen in the tag's properties: its own type, whatever the rules say; given back to them, theirs again.
        var tag = TagOf(f, door);
        f.Run("drafting.updateTag", new JsonObject { ["id"] = tag, ["tagType"] = "door" });
        Assert.True(f.Model.Get(tag)!.Bool(TagRules.FixedKey));
        Assert.Contains(mark, Texts(f, plan));
        f.Run("drafting.updateTag", new JsonObject { ["id"] = tag, ["byRules"] = true });
        Assert.Contains("EI60", Texts(f, plan));

        // A tag type removed takes its rules with it.
        f.Run("standards.remove", new JsonObject { ["kind"] = "tag", ["id"] = "door-fire" });
        Assert.Equal(["box", "wide"], DraftingResources.Book(f.Model).TagRules!.Select(r => r.Id));
    }

    [Fact]
    public void A_rule_is_checked_and_tags_made_after_it_take_its_type()
    {
        var f = F();
        Assert.Throws<CommandException>(() => Rule(f, "bad", "IfcDoor", "width >", "door"));
        Assert.Throws<CommandException>(() => Rule(f, "bad", "IfcDoor", "width > 1", "nope"));
        Assert.Throws<CommandException>(() => Rule(f, "bad", "", "width > 1", "door"));
        Assert.Null(DraftingResources.Book(f.Model).TagRules);
        // Bare or as {= …}, with or without the equals sign: the same formula.
        Assert.Equal("width > 1", TagRules.Formula("{= width > 1}"));
        Assert.Equal("width > 1", TagRules.Formula("= width > 1"));
        Assert.Equal("width > 1", TagRules.Formula(" width > 1 "));
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "door-wide", ["name"] = "Wide door", ["template"] = "W{mark}", ["shape"] = "opening", ["classes"] = "" } });
        Rule(f, "wide", "IfcDoor", "width > 0.5", "door-wide");
        var wall = f.Wall(0, 0, 6, 0);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("architecture.tagOpenings", new JsonObject { ["viewId"] = plan });
        Assert.Equal("door-wide", f.Model.Get(TagOf(f, door))!.Str("tagType"));
    }

    [Fact]
    public void A_tag_switched_off_prints_nothing_shows_faintly_on_screen_and_comes_back_in_one_undo_step()
    {
        var (f, door, _, plan) = Project();
        var mark = TagText.Mark(f.Model, f.Model.Get(door)!);
        var tag = TagOf(f, door);
        var before = Snapshot(f, plan);
        f.Run("drafting.updateTag", new JsonObject { ["id"] = tag, ["shown"] = false });
        Assert.DoesNotContain(mark, Texts(f, plan));
        // On screen: on the construction layer, faint, still a mark a click takes, told it is off.
        var screen = Render(f, plan, print: false);
        Assert.Contains(screen.Pens, p => p.Layer == Views.ConstructionLayer && p.Stroke == TagRules.GhostColor && p.Segments.Count > 0);
        var drawing = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        Assert.True((bool)drawing["marks"]!.AsArray().OfType<JsonObject>().Single(m => (string?)m["id"] == tag)["data"]!["off"]!);
        f.Session.Undo();
        Assert.Equal(before, Snapshot(f, plan));
        f.Session.Redo();
        f.Run("drafting.updateTag", new JsonObject { ["id"] = tag, ["shown"] = true });
        Assert.Equal(before, Snapshot(f, plan));
        // A box tag too.
        var box = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = plan, ["element"] = door, ["field"] = "name", ["at"] = new JsonArray(1, 3) })!["id"]!;
        var name = f.Model.Get(door)!.Name;
        Assert.Contains(name, Texts(f, plan));
        f.Run("drafting.updateTag", new JsonObject { ["id"] = box, ["shown"] = false });
        Assert.DoesNotContain(name, Texts(f, plan));
        Assert.Throws<CommandException>(() => f.Run("drafting.updateTag", new JsonObject { ["id"] = box, ["shown"] = "maybe" }));
    }

    [Fact]
    public void The_rules_go_with_the_standards_file_and_the_saved_project()
    {
        var (f, door, _, plan) = Project();
        var mark = TagText.Mark(f.Model, f.Model.Get(door)!);
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "door-wide", ["name"] = "Wide door", ["template"] = "W{mark}", ["shape"] = "opening", ["classes"] = "" } });
        Rule(f, "wide", "IfcDoor", "width > 0.5", "door-wide");
        Rule(f, "never", "IfcDoor", "0", "door");
        var rules = DraftingResources.Book(f.Model).TagRules!;

        // The .lapinstandards file: brought into another project, the same rules in the same order, its doors tagged by them.
        var text = (string)f.Session.Query("standards.export", new JsonObject { ["name"] = "Office" })!["text"]!;
        var (g, gDoor, _, gPlan) = Project();
        var read = f.Session.Query("standards.readPack", new JsonObject { ["text"] = text })!["entries"]!.AsArray().OfType<JsonObject>();
        Assert.Contains(read, e => (string?)e["kind"] == "tagRule" && (string?)e["id"] == "wide");
        g.Run("standards.importFrom", new JsonObject { ["text"] = text });
        Assert.Equal(rules, DraftingResources.Book(g.Model).TagRules!);
        Assert.Contains("W" + TagText.Mark(g.Model, g.Model.Get(gDoor)!), Texts(g, gPlan));

        // Saved and opened: the rules, and what they draw.
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        var h = F();
        var content = LapinPackage.Read(new MemoryStream(ms.ToArray()));
        h.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Assert.Equal(rules, DraftingResources.Book(h.Model).TagRules!);
        Assert.Contains("W" + mark, Texts(h, plan));
    }
}
