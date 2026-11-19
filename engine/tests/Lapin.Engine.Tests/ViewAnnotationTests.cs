using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A view's annotations (issue #111, decisions 722 and 723): any annotation hidden in one view by itself (Hide in view, a grid's
/// axis among them), only the view's own annotations shown (the grids, levels, rooms' stamps and other views' marks left out),
/// and a north arrow and a scale bar that are annotations of the view: moved, sized, their symbol chosen, hidden by their
/// category, printed where they stand; a site plan's made so when a project saved before opens.
/// </summary>
public class ViewAnnotationTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static JsonArray J(double x, double y) => new(x, y);
    static Views.Rendered Draw(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);
    static HashSet<string> Owners(Fixture f, string view) => [.. Draw(f, view).Marks.Select(m => m.Owner).OfType<string>()];
    static List<string> Texts(Fixture f, string view) => [.. Draw(f, view).Marks.SelectMany(m => m.Texts).Select(t => t.Text.Text)];

    [Fact]
    public void An_annotation_or_one_grid_axis_is_hidden_in_one_view_by_itself_and_revealed_in_magenta()
    {
        var f = F();
        f.Wall(0, 0, 8, 0);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Floor plan" })!["id"]!;
        var text = (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = plan, ["at"] = J(1, 1), ["text"] = "Only here" })!["id"]!;
        var grid = (string)f.Run("drafting.addAxis", new JsonObject { ["tag"] = "A", ["x1"] = 2, ["y1"] = -1, ["x2"] = 2, ["y2"] = 10 })!["gridId"]!;
        f.Run("drafting.addAxis", new JsonObject { ["tag"] = "B", ["x1"] = 6, ["y1"] = -1, ["x2"] = 6, ["y2"] = 10 });
        // A second plan of the storey shows the grid too.
        var other = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Furnishing" })!["id"]!;
        var id = (string)f.Run("view.hideElements", new JsonObject { ["viewId"] = plan, ["ids"] = new JsonArray(text, grid + "#A") })!["id"]!;
        Assert.Equal(plan, id);
        var owners = Owners(f, id);
        Assert.DoesNotContain(text, owners);
        Assert.DoesNotContain(grid + "#A", owners);
        Assert.Contains(grid + "#B", owners);
        // Printed as shown.
        Assert.DoesNotContain(Views.Render(f.Session.Reader, f.Model.Get(id)!, print: true).Marks, m => m.Owner == text);
        // The other plan keeps its axis A.
        Assert.Contains(grid + "#A", Owners(f, other));
        // Reveal hidden: drawn again, in the reveal colour, to be picked and shown again.
        var revealed = Views.Render(f.Session.Reader, ViewHiding.Temporary(f.Model, f.Model.Get(id)!, new JsonObject { ["reveal"] = true }));
        var mark = revealed.Marks.Single(m => m.Owner == text);
        Assert.All(mark.Texts, t => Assert.Equal(ViewHiding.RevealColor, t.Color));
        f.Run("view.unhideElements", new JsonObject { ["viewId"] = id, ["ids"] = new JsonArray(text) });
        Assert.Contains(text, Owners(f, id));
        // The whole grid hidden: both its axes.
        f.Run("view.hideElements", new JsonObject { ["viewId"] = id, ["ids"] = new JsonArray(grid) });
        Assert.DoesNotContain(Owners(f, id), o => o.StartsWith(grid, StringComparison.Ordinal));
        Assert.Throws<CommandException>(() => f.Run("view.hideElements", new JsonObject { ["viewId"] = id, ["ids"] = new JsonArray("nothing#A") }));
    }

    [Fact]
    public void A_grid_axis_is_hidden_with_its_category()
    {
        var f = F();
        f.Wall(0, 0, 8, 0);
        var id = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Floor plan" })!["id"]!;
        var grid = (string)f.Run("drafting.addAxis", new JsonObject { ["tag"] = "A", ["x1"] = 2, ["y1"] = -1, ["x2"] = 2, ["y2"] = 10 })!["gridId"]!;
        Assert.Contains(grid + "#A", Owners(f, id));
        f.Run("view.graphics", new JsonObject { ["id"] = id, ["annotation"] = "grids", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.DoesNotContain(grid + "#A", Owners(f, id));
    }

    [Fact]
    public void A_view_that_shows_only_its_own_annotations_leaves_out_the_grids_the_rooms_stamps_and_other_views_marks()
    {
        var f = F();
        f.Wall(0, 0, 8, 0);
        f.Wall(8, 0, 8, 6);
        f.Wall(8, 6, 0, 6);
        f.Wall(0, 6, 0, 0);
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 4, ["y"] = 3 })!["id"]!;
        var grid = (string)f.Run("drafting.addAxis", new JsonObject { ["tag"] = "A", ["x1"] = 2, ["y1"] = -1, ["x2"] = 2, ["y2"] = 10 })!["gridId"]!;
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(3, -2), ["end"] = J(3, 8), ["name"] = "A-A" })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Location" })!["id"]!;
        var text = (string)f.Run("drafting.addText", new JsonObject { ["viewId"] = plan, ["at"] = J(1, 1), ["text"] = "Its own" })!["id"]!;
        var arrow = (string)f.Run("sheet.addNorthArrow", new JsonObject { ["viewId"] = plan })!["id"]!;
        var before = Owners(f, plan);
        Assert.Contains(room, before);
        Assert.Contains(grid + "#A", before);
        Assert.Contains(section, before);
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["ownAnnotationsOnly"] = true });
        var after = Owners(f, plan);
        Assert.DoesNotContain(grid + "#A", after);
        Assert.DoesNotContain(section, after);
        Assert.Contains(text, after);
        Assert.Contains(arrow, after);
        // The room itself is drawn; its stamp is not.
        Assert.DoesNotContain(Draw(f, plan).Marks, m => m.Owner == room && !m.Under && !m.Pick);
        // Every annotation again, one step back.
        f.Session.Undo();
        Assert.Contains(grid + "#A", Owners(f, plan));
    }

    [Fact]
    public void A_north_arrow_stands_by_itself_until_moved_and_takes_its_size_and_symbol()
    {
        var f = F();
        f.Wall(0, 0, 8, 0);
        f.Wall(0, 0, 0, 6);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Plan", ["scale"] = 100 })!["id"]!;
        var arrow = (string)f.Run("sheet.addNorthArrow", new JsonObject { ["viewId"] = plan })!["id"]!;
        var r = Draw(f, plan);
        var mark = r.Marks.Single(m => m.Owner == arrow);
        Assert.Contains(mark.Texts, t => t.Text.Text == "N");
        Assert.True((bool)mark.Data!["auto"]!);
        // Right of the drawing's top: 8 mm round it, 4 mm off, its radius (7 mm at 1:100 is 0.7 m) in.
        var (generated, _, _) = Views.Generate(f.Session.Reader, f.Model.Get(plan)!);
        var drawingTop = generated!.Bounds!.Value.Max;
        var at = Args.PointOf(mark.Data["at"]);
        Assert.Equal(drawingTop.X + 0.8 + 0.4 + 0.7, at.X, 6);
        Assert.Equal(drawingTop.Y + 0.8 - 0.7 - 0.6, at.Y, 6);
        // Moved, sized: drawn there, larger.
        f.Run("sheet.updateKey", new JsonObject { ["id"] = arrow, ["at"] = J(-5, 2), ["size"] = 28 });
        mark = Draw(f, plan).Marks.Single(m => m.Owner == arrow);
        Assert.Equal(new Vec2(-5, 2), Args.PointOf(mark.Data!["at"]));
        Assert.False((bool)mark.Data["auto"]!);
        var reach = mark.Lines.Max(l => Math.Max((l.A - new Vec2(-5, 2)).Length, (l.B - new Vec2(-5, 2)).Length));
        Assert.Equal(1.4 * 1.25, reach, 6);
        // True north turned by the project's angle: the arrow's tip goes with it.
        f.Run("project.location", new JsonObject { ["base"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["northAngle"] = 90 } });
        var north = Args.PointOf(Draw(f, plan).Marks.Single(m => m.Owner == arrow).Data!["north"]);
        Assert.Equal(-1, north.X, 6);
        // Back where it stands by itself; a section takes none, nor a point-less one on paper.
        f.Run("sheet.updateKey", new JsonObject { ["id"] = arrow, ["at"] = null });
        Assert.True((bool)Draw(f, plan).Marks.Single(m => m.Owner == arrow).Data!["auto"]!);
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(3, -2), ["end"] = J(3, 8) })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("sheet.addNorthArrow", new JsonObject { ["viewId"] = section }));
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("sheet.addNorthArrow", new JsonObject { ["viewId"] = PaperViews.IdOf(s) }));
        Assert.Throws<CommandException>(() => f.Run("sheet.updateKey", new JsonObject { ["id"] = arrow, ["size"] = 1 }));
        // Its own symbol (Marker symbols): drawn instead of Lapin's arrow.
        var symbol = BuiltinSymbols.Prefix + "north-arrow-circle";
        f.Run("symbols.setSlot", new JsonObject { ["slot"] = "northArrow", ["symbolId"] = symbol, ["ownerIds"] = new JsonArray(arrow) });
        Assert.Equal(symbol, (string?)f.Model.Get(arrow)!.Attrs[SymbolSlots.Key]?["northArrow"]);
        Assert.NotEmpty(Draw(f, plan).Marks.Single(m => m.Owner == arrow).Lines);
        // Its category (north arrows and scale bars) hides it.
        f.Run("view.graphics", new JsonObject { ["id"] = plan, ["annotation"] = "keys", ["value"] = new JsonObject { ["hidden"] = true } });
        Assert.DoesNotContain(arrow, Owners(f, plan));
    }

    [Fact]
    public void A_north_arrow_on_a_sheet_points_as_its_viewport_lays_a_turned_plan_out()
    {
        var f = F();
        f.Wall(0, 0, 8, 0);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = "Plan" })!["id"]!;
        f.Run("view.setOrientation", new JsonObject { ["id"] = plan, ["angle"] = 90 });
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        var vp = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = plan, ["x"] = 150, ["y"] = 140 })!["viewportId"]!;
        var paper = PaperViews.IdOf(s);
        var arrow = (string)f.Run("sheet.addNorthArrow", new JsonObject { ["viewId"] = paper, ["at"] = J(0.38, 0.25), ["viewportId"] = vp })!["id"]!;
        var mark = Draw(f, paper).Marks.Single(m => m.Owner == arrow);
        // The model's +Y (north) shown to the right: so it points on the paper.
        var north = Args.PointOf(mark.Data!["north"]);
        Assert.Equal(1, north.X, 6);
        Assert.Equal(0, north.Y, 6);
    }

    static Fixture Project()
    {
        var f = new Fixture(new ArchitectureModule(), new CivilModule(), new DraftingModule());
        f.Session.New();
        return f;
    }

    [Fact]
    public void A_site_plans_north_arrow_and_scale_bar_are_its_own_annotations_drawn_where_it_drew_them()
    {
        var f = Project();
        var storey = Spatial.Storeys(f.Model).Single(s => s.Name == "Ground floor").Id;
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 120, ["y2"] = 0 });
        var site = f.Model.OfClass(Views.Class).Single(v => SitePlans.Of(v) == SitePlans.Situation);
        Assert.True((bool)site.Attrs[SitePlans.KeysKey]!);
        var arrow = Assert.Single(DrawingKeys.On(f.Model, site.Id, DrawingKeys.NorthArrow));
        var bar = Assert.Single(DrawingKeys.On(f.Model, site.Id, DrawingKeys.ScaleBar));
        Assert.Equal("site", bar.Str("look"));
        var texts = Texts(f, site.Id);
        // 60 mm at 1:500 is 30 m: a bar of 20 m in five parts, its half written, its scale under it.
        Assert.Contains("N", texts);
        Assert.Contains("20 m", texts);
        Assert.Contains("10", texts);
        Assert.Contains("1:500", texts);
        // Under the drawing's bottom left: 8 mm round it, 6 mm down (4 m and 3 m at 1:500).
        var (generated, _, _) = Views.Generate(f.Session.Reader, site);
        var drawn = generated!.Bounds!.Value;
        var barMark = Draw(f, site.Id).Marks.Single(m => m.Owner == bar.Id);
        Assert.Equal(new Vec2(drawn.Min.X - 4, drawn.Min.Y - 4 - 3), Args.PointOf(barMark.Data!["at"]));
        // Moved, it stays there; switched off in the site's settings, it goes; on again, a new one stands by itself.
        f.Run("sheet.updateKey", new JsonObject { ["id"] = bar.Id, ["at"] = J(10, -30) });
        Assert.Equal(new Vec2(10, -30), Args.PointOf(Draw(f, site.Id).Marks.Single(m => m.Owner == bar.Id).Data!["at"]));
        f.Run("view.setSite", new JsonObject { ["id"] = site.Id, ["northArrow"] = false });
        Assert.Empty(DrawingKeys.On(f.Model, site.Id, DrawingKeys.NorthArrow));
        Assert.False(SitePlans.NorthArrow(f.Model, site));
        Assert.DoesNotContain("N", Texts(f, site.Id));
        f.Run("view.setSite", new JsonObject { ["id"] = site.Id, ["northArrow"] = true });
        f.Run("view.setSite", new JsonObject { ["id"] = site.Id, ["northArrow"] = true });
        Assert.Single(DrawingKeys.On(f.Model, site.Id, DrawingKeys.NorthArrow));
        Assert.NotEqual(arrow.Id, DrawingKeys.On(f.Model, site.Id, DrawingKeys.NorthArrow).Single().Id);
        // A copy of the view, without its detailing, takes them along.
        var copy = (string)f.Run("view.duplicate", new JsonObject { ["id"] = site.Id })!["id"]!;
        Assert.Single(DrawingKeys.On(f.Model, copy, DrawingKeys.NorthArrow));
        Assert.Single(DrawingKeys.On(f.Model, copy, DrawingKeys.ScaleBar));
    }

    [Fact]
    public void A_site_plan_saved_before_has_its_keys_made_annotations_when_it_opens()
    {
        var site = new Entity("site", Views.Class, "Site plan", new JsonObject { ["kind"] = "plan", ["site"] = "situation", ["storeyId"] = "s", ["scale"] = 500 });
        var location = new Entity("loc", Views.Class, "Location plan", new JsonObject { ["kind"] = "plan", ["site"] = "location", ["storeyId"] = "s", ["scale"] = 2000, ["northArrow"] = false });
        var plan = new Entity("plan", Views.Class, "Plan", new JsonObject { ["kind"] = "plan", ["storeyId"] = "s" });
        var up = new SitePlans.KeysUpgrade();
        var (list, _) = up.Upgrade([site, location, plan], []);
        Assert.Equal(6, list.Count);
        Assert.Equal(["NORTHARROW", "SCALEBAR"], list.Where(e => e.Str("viewId") == "site").Select(e => e.PredefinedType!).Order());
        // The location plan had no arrow: its bar alone, its flag gone.
        Assert.Equal(["SCALEBAR"], list.Where(e => e.Str("viewId") == "loc").Select(e => e.PredefinedType!));
        Assert.Null(list.Single(e => e.Id == "loc").Attrs["northArrow"]);
        Assert.All(list.Where(e => e.Str("viewId") != null), e => Assert.Null(e.Attrs["at"]));
        // Once only.
        var (again, _) = up.Upgrade(list, []);
        Assert.Same(list, again);
    }
}
