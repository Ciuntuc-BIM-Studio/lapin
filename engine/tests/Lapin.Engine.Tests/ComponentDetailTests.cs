using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
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

    // Details in sections and elevations (docs/components.md, "Detail sets"): kept on a side of the component (front, back,
    // left, right: the way a view looks at it in its own frame), drawn in every section and elevation that looks at that side
    // of an instance and sees it, turned and mirrored with it.

    static ViewFrame FrameOf(Fixture f, string view) => Views.Generate(f.Session.Reader, Views.Find(f.Model, view)!).Spec!.Frame;
    static JsonObject FrameArg(ViewFrame fr) => new()
    {
        ["origin"] = new JsonArray(fr.Origin.X, fr.Origin.Y, fr.Origin.Z),
        ["right"] = new JsonArray(fr.Right.X, fr.Right.Y, fr.Right.Z),
        ["up"] = new JsonArray(fr.Up.X, fr.Up.Y, fr.Up.Z),
    };
    static bool At(List<(double X, double Y)> pts, Vec2 p) => pts.Any(q => Math.Abs(q.X - p.X) < 1e-6 && Math.Abs(q.Y - p.Y) < 1e-6);
    static JsonArray P(Vec2 p) => new(p.X, p.Y);

    /// <summary>
    /// The 5 × 4 box (its instance standing at its middle, (2.5, 2)) with a line drawn on the south elevation along its front,
    /// 1 m up, from x = 1 to 2, put in the set "Elevations"; a second instance 20 m along, turned 180° about its middle.
    /// </summary>
    static (Fixture F, string Group, string Twin, string Line, string South, string North) Sides(bool turned = true)
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var walls = new[] { f.Wall(0, 0, 5, 0), f.Wall(5, 0, 5, 4), f.Wall(5, 4, 0, 4), f.Wall(0, 4, 0, 0) };
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = walls[0], ["along"] = 1 });
        var group = (string)f.Run("component.make", new JsonObject { ["ids"] = Ids(walls), ["name"] = "A1" })!["id"]!;
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var north = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "north" })!["id"]!;
        var fr = FrameOf(f, south);
        var line = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = south, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = P(fr.Map(new Vec3(1, 0, 1))), ["b"] = P(fr.Map(new Vec3(2, 0, 1))) } })!["ids"]![0]!;
        f.Run("component.addDetail", new JsonObject { ["id"] = group, ["ids"] = Ids(line), ["set"] = "Elevations" });
        var twin = TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "copy", ["by"] = P(20, 0) })!);
        if (turned) f.Run("element.transform", new JsonObject { ["ids"] = Ids(twin), ["op"] = "rotate", ["center"] = P(22.5, 2), ["angle"] = 180 });
        return (f, group, twin, line, south, north);
    }

    static string DraftOf(Fixture f, string group) => Details(f, group).Single(e => e.PredefinedType == "DRAFT").Id;

    [Fact]
    public void A_detail_drawn_on_an_elevation_shows_on_that_side_of_every_instance_turned_with_it()
    {
        var (f, group, twin, line, south, north) = Sides();
        Assert.Equal(group, Groups.Of(f.Model, line)!.Id);
        Assert.Equal(Components.DetailView, f.Model.Get(line)!.Str("viewId"));
        Assert.Equal("front", f.Model.Get(line)!.Str(Components.DetailSideKey));
        var copy = DraftOf(f, twin);
        Assert.NotEqual(line, copy);
        // The south elevation sees the first instance's front where it was drawn; the turned one shows it its back.
        var s = FrameOf(f, south);
        var drawn = Drawn(f, south, line);
        Assert.True(At(drawn, s.Map(new Vec3(1, 0, 1))) && At(drawn, s.Map(new Vec3(2, 0, 1))));
        Assert.False(Shows(f, south, copy));
        // The north elevation sees the turned instance's front: its line from x = 24 to 23, and not the first one's.
        var n = FrameOf(f, north);
        var there = Drawn(f, north, copy);
        Assert.True(At(there, n.Map(new Vec3(24, 4, 1))) && At(there, n.Map(new Vec3(23, 4, 1))));
        Assert.False(Shows(f, north, line));
        // Seen from the east or the west, neither: no detail was drawn on those sides.
        var east = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "east" })!["id"]!;
        Assert.False(Shows(f, east, line) || Shows(f, east, copy));
        // The plans draw only what was drawn on a plan.
        Assert.False(Shows(f, Plan(f), line) || Shows(f, Plan(f), copy));
        // Turned back, the copy is seen from the south again.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(twin), ["op"] = "rotate", ["center"] = P(22.5, 2), ["angle"] = 180 });
        s = FrameOf(f, south);
        var back = Drawn(f, south, DraftOf(f, twin));
        Assert.True(At(back, s.Map(new Vec3(21, 0, 1))) && At(back, s.Map(new Vec3(22, 0, 1))));
        Assert.False(Shows(f, north, DraftOf(f, twin)));
    }

    [Fact]
    public void A_mirrored_instance_shows_its_side_detail_mirrored()
    {
        var (f, group, _, _, south, _) = Sides(turned: false);
        var mirrored = TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "mirror", ["a"] = P(-1, 0), ["b"] = P(-1, 1), ["copy"] = true })!);
        // About x = -1: from x = 1 and 2 to -3 and -4, still on the front, the side the south elevation sees.
        var s = FrameOf(f, south);
        var drawn = Drawn(f, south, DraftOf(f, mirrored));
        Assert.True(At(drawn, s.Map(new Vec3(-3, 0, 1))) && At(drawn, s.Map(new Vec3(-4, 0, 1))));
    }

    [Fact]
    public void A_section_through_instances_shows_the_side_it_looks_at_of_those_it_sees()
    {
        var (f, group, twin, line, south, north) = Sides();
        var copy = DraftOf(f, twin);
        // Along y = 1, looking north (+y): the first instance's front; the turned one shows its back.
        var ahead = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = P(-2, 1), ["end"] = P(30, 1) })!["id"]!;
        var a = FrameOf(f, ahead);
        var drawn = Drawn(f, ahead, line);
        Assert.True(At(drawn, a.Map(new Vec3(1, 1, 1))) && At(drawn, a.Map(new Vec3(2, 1, 1))));
        Assert.False(Shows(f, ahead, copy));
        // Drawn the other way, looking south: the turned instance's front only.
        var backwards = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = P(30, 3), ["end"] = P(-2, 3) })!["id"]!;
        var b = FrameOf(f, backwards);
        var there = Drawn(f, backwards, copy);
        Assert.True(At(there, b.Map(new Vec3(24, 3, 1))) && At(there, b.Map(new Vec3(23, 3, 1))));
        Assert.False(Shows(f, backwards, line));
        // A section past both, looking on: it sees neither instance, so neither detail.
        var past = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = P(-2, 10), ["end"] = P(30, 10) })!["id"]!;
        Assert.False(Shows(f, past, line) || Shows(f, past, copy));
        // A text drawn in the section, put in the set: it shows on the south elevation, and on the turned instance's north one.
        var text = (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = ahead, ["at"] = P(a.Map(new Vec3(3, 1, 2))), ["text"] = "Plinth" })!["id"]!;
        f.Run("component.addDetail", new JsonObject { ["id"] = group, ["ids"] = Ids(text), ["set"] = "Elevations" });
        Assert.True(Shows(f, south, text));
        Assert.True(Shows(f, north, Details(f, twin).Single(e => e.PredefinedType == "TEXT").Id));
        Assert.False(Shows(f, north, text));
    }

    [Fact]
    public void Moved_in_an_elevation_the_side_detail_follows_in_every_instance()
    {
        var (f, _, twin, line, south, north) = Sides();
        var copy = DraftOf(f, twin);
        // One metre right on the north elevation, seen on the turned instance: one metre towards its own right, the first one's too.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(copy), ["op"] = "move", ["by"] = P(1, 0), ["frame"] = FrameArg(FrameOf(f, north)) });
        Assert.Equal(copy, DraftOf(f, twin));
        var n = FrameOf(f, north);
        var there = Drawn(f, north, copy);
        Assert.True(At(there, n.Map(new Vec3(23, 4, 1))) && At(there, n.Map(new Vec3(22, 4, 1))));
        var s = FrameOf(f, south);
        var drawn = Drawn(f, south, line);
        Assert.True(At(drawn, s.Map(new Vec3(2, 0, 1))) && At(drawn, s.Map(new Vec3(3, 0, 1))));
        // Mirrored on the south elevation about the vertical through x = 2: from 2–3 back to 1–2, in both instances.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(line), ["op"] = "mirror", ["a"] = P(s.Map(new Vec3(2, 0, 0))), ["b"] = P(s.Map(new Vec3(2, 0, 3))), ["frame"] = FrameArg(s) });
        drawn = Drawn(f, south, line);
        Assert.True(At(drawn, s.Map(new Vec3(1, 0, 1))) && At(drawn, s.Map(new Vec3(2, 0, 1))));
        there = Drawn(f, north, copy);
        Assert.True(At(there, n.Map(new Vec3(24, 4, 1))) && At(there, n.Map(new Vec3(23, 4, 1))));
    }

    [Fact]
    public void Adding_a_side_detail_is_one_undo_step()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var walls = new[] { f.Wall(0, 0, 5, 0), f.Wall(5, 0, 5, 4), f.Wall(5, 4, 0, 4), f.Wall(0, 4, 0, 0) };
        var group = (string)f.Run("component.make", new JsonObject { ["ids"] = Ids(walls) })!["id"]!;
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var line = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = south, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = P(0, 1), ["b"] = P(1, 1) } })!["ids"]![0]!;
        var before = f.Model.Get(line)!.Attrs.ToJsonString();
        f.Run("component.addDetail", new JsonObject { ["id"] = group, ["ids"] = Ids(line), ["set"] = "Elevations" });
        Assert.Equal("front", f.Model.Get(line)!.Str(Components.DetailSideKey));
        f.Session.Undo();
        Assert.Equal(before, f.Model.Get(line)!.Attrs.ToJsonString());
        Assert.Null(Groups.Of(f.Model, line));
        Assert.True(Shows(f, south, line));
    }

    [Fact]
    public void A_view_not_square_to_the_instance_takes_no_side_detail()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var walls = new[] { f.Wall(0, 0, 5, 0), f.Wall(5, 0, 5, 4), f.Wall(5, 4, 0, 4), f.Wall(0, 4, 0, 0) };
        var group = (string)f.Run("component.make", new JsonObject { ["ids"] = Ids(walls) })!["id"]!;
        var skew = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = P(-1, -1), ["end"] = P(6, 5) })!["id"]!;
        var line = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = skew, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = P(1, 1), ["b"] = P(2, 1) } })!["ids"]![0]!;
        var ex = Assert.Throws<CommandException>(() => f.Run("component.addDetail", new JsonObject { ["id"] = group, ["ids"] = Ids(line), ["set"] = "Elevations" }));
        Assert.Contains("square", ex.Message);
    }

    [Fact]
    public void Hidden_per_view_saved_and_opened_again_the_side_details_stay()
    {
        var (f, _, twin, line, south, north) = Sides();
        f.Run("view.graphics", new JsonObject { ["id"] = south, ["detailSet"] = "Elevations", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.False(Shows(f, south, line));
        Assert.True(Shows(f, north, DraftOf(f, twin)));
        var path = Path.Combine(_dir, "sides.lapin");
        PackageFiles.Save(f.Session, path);
        var g = new Fixture(new ArchitectureModule(), new DraftingModule());
        PackageFiles.Open(g.Session, path);
        Assert.False(Shows(g, south, line));
        var n = FrameOf(g, north);
        var there = Drawn(g, north, DraftOf(g, twin));
        Assert.True(At(there, n.Map(new Vec3(24, 4, 1))) && At(there, n.Map(new Vec3(23, 4, 1))));
        g.Run("view.graphics", new JsonObject { ["id"] = south, ["detailSet"] = "Elevations", ["value"] = null });
        Assert.True(Shows(g, south, line));
    }

    [Fact]
    public void A_plan_detail_stays_out_of_sections_and_elevations()
    {
        var (f, _, twin, line, _, _) = Two();
        var south = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var cut = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = P(-2, 1), ["end"] = P(30, 1) })!["id"]!;
        Assert.False(Shows(f, south, line) || Shows(f, cut, line));
        Assert.False(Shows(f, south, DraftOf(f, twin)));
        Assert.True(Shows(f, Plan(f), line));
    }

    [Fact]
    public void Taken_out_of_its_instance_a_side_detail_goes_to_a_view_that_sees_it_where_it_was()
    {
        var (f, _, twin, _, _, north) = Sides();
        var copy = DraftOf(f, twin);
        var n = FrameOf(f, north);
        f.Run("group.ungroup", new JsonObject { ["ids"] = Ids(twin) });
        var plain = f.Model.Get(copy)!;
        Assert.Equal(north, plain.Str("viewId"));
        Assert.Null(plain.Str(Components.DetailSideKey));
        var there = Drawn(f, north, copy);
        Assert.True(At(there, n.Map(new Vec3(24, 4, 1))) && At(there, n.Map(new Vec3(23, 4, 1))));
    }

    [Fact]
    public void A_side_details_points_edited_in_any_view_that_sees_it_land_right_in_every_instance()
    {
        var (f, group, twin, line, south, north) = Sides();
        var copy = DraftOf(f, twin);
        // A grip of the turned instance's copy, on the north elevation: its end raised to 2 m.
        var n = FrameOf(f, north);
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = copy, ["viewId"] = north, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = P(n.Map(new Vec3(24, 4, 1))), ["b"] = P(n.Map(new Vec3(23, 4, 2))) } });
        Assert.Equal(copy, DraftOf(f, twin));
        var there = Drawn(f, north, copy);
        Assert.True(At(there, n.Map(new Vec3(24, 4, 1))) && At(there, n.Map(new Vec3(23, 4, 2))));
        var s = FrameOf(f, south);
        var drawn = Drawn(f, south, line);
        Assert.True(At(drawn, s.Map(new Vec3(1, 0, 1))) && At(drawn, s.Map(new Vec3(2, 0, 2))));
        // The first instance moved 3 m along, then its line edited on the south elevation: both follow.
        f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "move", ["by"] = P(3, 0) });
        s = FrameOf(f, south);
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = line, ["viewId"] = south, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = P(s.Map(new Vec3(4, 0, 1))), ["b"] = P(s.Map(new Vec3(5, 0, 0.5))) } });
        drawn = Drawn(f, south, line);
        Assert.True(At(drawn, s.Map(new Vec3(4, 0, 1))) && At(drawn, s.Map(new Vec3(5, 0, 0.5))));
        n = FrameOf(f, north);
        there = Drawn(f, north, DraftOf(f, twin));
        Assert.True(At(there, n.Map(new Vec3(24, 4, 1))) && At(there, n.Map(new Vec3(23, 4, 0.5))));
        // A mirrored instance's copy edited on its own view: the first instance takes it mirrored back.
        var mirrored = TwinOf(f, f.Run("element.transform", new JsonObject { ["ids"] = Ids(group), ["op"] = "mirror", ["a"] = P(-1, 0), ["b"] = P(-1, 1), ["copy"] = true })!);
        s = FrameOf(f, south);
        // The first instance (middle 5.5) at 4–5 is, about x = -1, at -6 to -7 in the mirrored one: its end there put at 1.5 m.
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = DraftOf(f, mirrored), ["viewId"] = south, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = P(s.Map(new Vec3(-6, 0, 1))), ["b"] = P(s.Map(new Vec3(-7, 0, 1.5))) } });
        var flipped = Drawn(f, south, DraftOf(f, mirrored));
        Assert.True(At(flipped, s.Map(new Vec3(-6, 0, 1))) && At(flipped, s.Map(new Vec3(-7, 0, 1.5))));
        drawn = Drawn(f, south, line);
        Assert.True(At(drawn, s.Map(new Vec3(4, 0, 1))) && At(drawn, s.Map(new Vec3(5, 0, 1.5))));
    }
}
