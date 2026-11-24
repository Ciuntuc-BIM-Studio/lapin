using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Detail sets of a component (forum #224, Revit's attached detail groups; docs/components.md): named sets of 2D drafting
/// owned by the component, drawn in every plan of each instance's storey, moved, turned and mirrored with it, shown or hidden
/// per view by its Visibility / Graphics; edited from inside one instance, followed by all, under stable ids.
/// </summary>
public class ComponentDetailTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-component-details").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static JsonArray P(double x, double y) => new(x, y);
    static JsonArray Ids(params string[] ids) => new([.. ids.Select(x => (JsonNode)x)]);
    static string TwinOf(Fixture f, JsonNode made) => made["ids"]!.AsArray().Select(x => (string)x!).First(x => f.Model.Get(x)!.Class == Groups.Class);
    static List<Entity> Members(Fixture f, string group) => Groups.MembersOf(f.Model, group).Select(f.Model.Get).OfType<Entity>().ToList();
    static List<Entity> Details(Fixture f, string group, string? set = null) =>
        [.. Members(f, group).Where(e => e.Class == IfcClass.Annotation && e.Str(Components.DetailSetKey) is { } s && (set == null || s == set))];

    /// <summary>The marks a view draws for an annotation: its lines' points.</summary>
    static List<(double X, double Y)> Drawn(Fixture f, string viewId, string owner) =>
        [.. Views.Render(f.Session.Reader, Views.Find(f.Model, viewId)!).Marks.Where(m => m.Owner == owner)
            .SelectMany(m => m.Lines).Where(l => l.Layer != "PICK").SelectMany(l => new[] { (l.A.X, l.A.Y), (l.B.X, l.B.Y) })];
    static bool Shows(Fixture f, string viewId, string owner) => Views.Render(f.Session.Reader, Views.Find(f.Model, viewId)!).Marks.Any(m => m.Owner == owner);

    static string Plan(Fixture f) => "plan:" + f.Storey;

    /// <summary>
    /// A 5 × 4 box of walls with a door, made a component; inside it a line drawn from (1, 1) to (2, 1) and a text, put in
    /// the set "Technical"; a second instance 20 m along.
    /// </summary>
    static (Fixture F, string Group, string Twin, string Line, string Text, string Component) Two()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var walls = new[] { f.Wall(0, 0, 5, 0), f.Wall(5, 0, 5, 4), f.Wall(5, 4, 0, 4), f.Wall(0, 4, 0, 0) };
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["along"] = 1 });
        var made = f.Run("component.make", new JsonObject { ["ids"] = Ids(walls), ["name"] = "A1" })!;
        var group = (string)made["id"]!;
        var line = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = Plan(f), ["shape"] = new JsonObject { ["type"] = "line", ["a"] = P(1, 1), ["b"] = P(2, 1) } })!["ids"]![0]!;
        var text = (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = Plan(f), ["at"] = P(2.5, 2), ["text"] = "Kitchen 12 m²" })!["id"]!;
        f.Run("component.addDetail", new JsonObject { ["id"] = group, ["ids"] = Ids(line, text), ["set"] = "Technical" });
        var twin = TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "copy", ["by"] = P(20, 0) })!);
        return (f, group, twin, line, text, (string)made["component"]!);
    }

    [Fact]
    public void Drafting_added_to_a_set_becomes_the_components_and_reaches_every_instance()
    {
        var (f, group, twin, line, text, component) = Two();
        Assert.Equal(group, Groups.Of(f.Model, line)!.Id);
        Assert.Equal("Technical", f.Model.Get(line)!.Str(Components.DetailSetKey));
        Assert.Equal(2, Details(f, twin, "Technical").Count);
        // The same members by key, each its own id.
        Assert.Equal(Details(f, group).Select(e => e.Str(Components.MemberKey)).Order(), Details(f, twin).Select(e => e.Str(Components.MemberKey)).Order());
        Assert.DoesNotContain(Details(f, twin), e => e.Id == line || e.Id == text);
        // The component lists its sets.
        var listed = f.Session.Query("component.list", new JsonObject())!.AsArray().Single(c => (string?)c!["id"] == component)!;
        Assert.Equal(["Technical"], listed["detailSets"]!.AsArray().Select(x => (string)x!));
        // One undo step takes the set back out.
        f.Session.Undo();
        f.Session.Undo();
        Assert.Null(f.Model.Get(line)!.Str(Components.DetailSetKey));
        Assert.Null(Groups.Of(f.Model, line));
    }

    [Fact]
    public void Each_instance_draws_its_set_where_it_stands_in_the_plan()
    {
        var (f, _, twin, line, _, _) = Two();
        Assert.Contains((1.0, 1.0), Drawn(f, Plan(f), line));
        var copy = Details(f, twin).Single(e => e.PredefinedType == "DRAFT").Id;
        var pts = Drawn(f, Plan(f), copy);
        Assert.Contains(pts, p => Math.Abs(p.X - 21) < 1e-6 && Math.Abs(p.Y - 1) < 1e-6);
        Assert.Contains(pts, p => Math.Abs(p.X - 22) < 1e-6 && Math.Abs(p.Y - 1) < 1e-6);
        Assert.True(Shows(f, Plan(f), Details(f, twin).Single(e => e.PredefinedType == "TEXT").Id));
    }

    [Fact]
    public void A_turned_and_a_mirrored_instance_carry_their_set_turned_and_mirrored()
    {
        var (f, group, twin, line, _, _) = Two();
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(twin), ["op"] = "rotate", ["center"] = P(20, 0), ["angle"] = 90 });
        var turned = Details(f, twin).Single(e => e.PredefinedType == "DRAFT").Id;
        // (21, 1)–(22, 1) turned 90° about (20, 0): (19, 1)–(19, 2).
        var pts = Drawn(f, Plan(f), turned);
        Assert.Contains(pts, p => Math.Abs(p.X - 19) < 1e-6 && Math.Abs(p.Y - 1) < 1e-6);
        Assert.Contains(pts, p => Math.Abs(p.X - 19) < 1e-6 && Math.Abs(p.Y - 2) < 1e-6);
        var mirrored = TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "mirror", ["a"] = P(-1, 0), ["b"] = P(-1, 1), ["copy"] = true })!);
        var flipped = Details(f, mirrored).Single(e => e.PredefinedType == "DRAFT").Id;
        // About x = -1: (1, 1)–(2, 1) to (-3, 1)–(-4, 1).
        var mp = Drawn(f, Plan(f), flipped);
        Assert.Contains(mp, p => Math.Abs(p.X + 3) < 1e-6 && Math.Abs(p.Y - 1) < 1e-6);
        Assert.Contains(mp, p => Math.Abs(p.X + 4) < 1e-6 && Math.Abs(p.Y - 1) < 1e-6);
        Assert.Contains((1.0, 1.0), Drawn(f, Plan(f), line));
        // The mirrored instance's text still reads left to right.
        var angle = Details(f, mirrored).Single(e => e.PredefinedType == "TEXT").Num("angle");
        Assert.InRange(angle, -90 + 1e-9, 90);
        // Its lines end where they began, the mirror of its box.
        Assert.Equal("right", Details(f, mirrored).Single(e => e.PredefinedType == "TEXT").Str("align"));
    }

    [Fact]
    public void Edited_inside_one_instance_the_set_follows_in_the_others_under_their_ids()
    {
        var (f, _, twin, line, _, _) = Two();
        var copy = Details(f, twin).Single(e => e.PredefinedType == "DRAFT").Id;
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(line), ["op"] = "move", ["by"] = P(0, 1) });
        Assert.Equal(copy, Details(f, twin).Single(e => e.PredefinedType == "DRAFT").Id);
        Assert.Contains(Drawn(f, Plan(f), copy), p => Math.Abs(p.X - 21) < 1e-6 && Math.Abs(p.Y - 2) < 1e-6);
        // Edited in the other, back in the first.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(copy), ["op"] = "move", ["by"] = P(1, 0) });
        Assert.Contains(Drawn(f, Plan(f), line), p => Math.Abs(p.X - 2) < 1e-6 && Math.Abs(p.Y - 2) < 1e-6);
        // Deleted in one, gone from the other.
        f.Run("element.delete", new JsonObject { ["ids"] = Ids(line) });
        Assert.DoesNotContain(Details(f, twin), e => e.PredefinedType == "DRAFT");
        f.Session.Undo();
        Assert.Single(Details(f, twin), e => e.PredefinedType == "DRAFT");
    }

    [Fact]
    public void A_view_shows_or_hides_each_set_by_its_visibility_graphics()
    {
        var (f, group, _, line, _, _) = Two();
        var sales = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = Plan(f), ["shape"] = new JsonObject { ["type"] = "line", ["a"] = P(1, 3), ["b"] = P(4, 3) } })!["ids"]![0]!;
        f.Run("component.addDetail", new JsonObject { ["id"] = group, ["ids"] = Ids(sales), ["set"] = "Sales" });
        var technical = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Technical plan" })!["id"]!;
        var selling = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Sales plan" })!["id"]!;
        // Every plan of the storey shows every set by default: it belongs to the instance, not to the view it was drawn on.
        Assert.True(Shows(f, technical, line));
        Assert.True(Shows(f, selling, sales));
        f.Run("view.graphics", new JsonObject { ["id"] = technical, ["detailSet"] = "Sales", ["value"] = new JsonObject { ["hidden"] = true } });
        f.Run("view.graphics", new JsonObject { ["id"] = selling, ["detailSet"] = "Technical", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.True(Shows(f, technical, line));
        Assert.False(Shows(f, technical, sales));
        Assert.True(Shows(f, selling, sales));
        Assert.False(Shows(f, selling, line));
        Assert.True(VisibilityGraphics.Of(f.Model, f.Model.Get(selling)!).HidesDetailSet("Technical"));
        // Shown again.
        f.Run("view.graphics", new JsonObject { ["id"] = selling, ["detailSet"] = "Technical", ["value"] = null });
        Assert.True(Shows(f, selling, line));
    }

    [Fact]
    public void An_instance_on_another_storey_draws_its_set_in_that_storeys_plans_only()
    {
        var (f, group, _, line, _, _) = Two();
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
        var copied = f.Run("element.copyToStoreys", new JsonObject { ["ids"] = Ids(group), ["storeyIds"] = new JsonArray(upper) })!;
        var up = Groups.All(f.Model).Single(g => Components.Of(g) != null && Members(f, g.Id).Any(e => Spatial.StoreyOf(f.Model, e.Id)?.Id == upper)).Id;
        var copy = Details(f, up).Single(e => e.PredefinedType == "DRAFT").Id;
        Assert.True(Shows(f, "plan:" + upper, copy));
        Assert.False(Shows(f, "plan:" + upper, line));
        Assert.False(Shows(f, Plan(f), copy));
        Assert.NotNull(copied);
    }

    [Fact]
    public void A_tag_in_a_set_tags_the_same_wall_of_each_instance()
    {
        var (f, group, twin, _, _, _) = Two();
        var south = Members(f, group).First(e => e.Class == IfcClass.Wall && Openings.Of(f.Model, e.Id).Any());
        var tag = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = Plan(f), ["element"] = south.Id, ["field"] = "type", ["at"] = P(2.5, -1) })!["id"]!;
        f.Run("component.addDetail", new JsonObject { ["id"] = group, ["ids"] = Ids(tag), ["set"] = "Technical" });
        var twinSouth = Members(f, twin).Single(e => e.Str(Components.MemberKey) == south.Str(Components.MemberKey));
        var twinTag = Details(f, twin).Single(e => e.PredefinedType == "TAG");
        Assert.Equal(twinSouth.Id, twinTag.Str("element"));
        Assert.True(Shows(f, Plan(f), twinTag.Id));
    }

    [Fact]
    public void Taken_out_of_its_instance_a_detail_goes_back_to_a_plan_of_its_storey()
    {
        var (f, group, twin, line, _, _) = Two();
        f.Run("group.ungroup", new JsonObject { ["ids"] = Ids(twin) });
        var left = f.Model.OfClass(IfcClass.Annotation).Where(e => e.Str("viewId") == Components.DetailView).ToList();
        Assert.All(left, e => Assert.NotNull(Groups.Of(f.Model, e.Id)));
        // The ungrouped instance's drafting is plain drafting on its storey's plan, where it was drawn.
        var plain = f.Model.OfClass(IfcClass.Annotation).Where(e => e.PredefinedType == "DRAFT" && Groups.Of(f.Model, e.Id) == null).ToList();
        var mine = Assert.Single(plain);
        Assert.Equal(Plan(f), mine.Str("viewId"));
        Assert.Null(mine.Str(Components.DetailSetKey));
        Assert.True(Shows(f, Plan(f), mine.Id));
        Assert.True(Shows(f, Plan(f), line));
        Assert.NotNull(group);
    }

    [Fact]
    public void A_door_alone_or_a_model_element_cannot_be_a_detail()
    {
        var (f, group, _, _, _, _) = Two();
        var wall = f.Wall(10, 10, 12, 10);
        var ex = Assert.Throws<CommandException>(() => f.Run("component.addDetail", new JsonObject { ["id"] = group, ["ids"] = Ids(wall), ["set"] = "Technical" }));
        Assert.Contains("2D", ex.Message);
        var notInstance = Assert.Throws<CommandException>(() => f.Run("component.addDetail", new JsonObject { ["id"] = wall, ["ids"] = Ids(wall), ["set"] = "X" }));
        Assert.Contains("instance", notInstance.Message);
    }

    [Fact]
    public void Saved_and_opened_again_the_sets_and_the_views_choice_stay()
    {
        var (f, group, twin, line, _, _) = Two();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Sales plan" })!["id"]!;
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["detailSet"] = "Technical", ["value"] = new JsonObject { ["hidden"] = true } });
        var path = Path.Combine(_dir, "details.lapin");
        PackageFiles.Save(f.Session, path);
        var g = new Fixture(new ArchitectureModule(), new DraftingModule());
        PackageFiles.Open(g.Session, path);
        Assert.Equal(2, Details(g, twin).Count);
        Assert.True(Shows(g, "plan:" + g.Storey, line));
        Assert.False(Shows(g, plan, line));
        g.Run("element.transform", new JsonObject { ["ids"] = Ids(line), ["op"] = "move", ["by"] = P(0, 1) });
        var copy = Details(g, twin).Single(e => e.PredefinedType == "DRAFT").Id;
        Assert.Contains(Drawn(g, "plan:" + g.Storey, copy), p => Math.Abs(p.X - 21) < 1e-6 && Math.Abs(p.Y - 2) < 1e-6);
        Assert.NotNull(group);
    }
}
