using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A view placed on a sheet (forum topic 207): it cannot be deleted until it comes off the sheet, the whole deletion refused
/// with the sheets named (one step: nothing goes); the sheet itself, or the view taken off it, still can. And Place on sheet…
/// → New sheet: the sheet made with the view on it, one undo step.
/// </summary>
public class SheetPlacedViewTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Id(JsonNode? r) => (string)r!["id"]!;
    static JsonArray J(double x, double y) => new(x, y);
    static string Sheet(Fixture f, string name) => Id(f.Run("sheet.create", new JsonObject { ["name"] = name }));
    static string Section(Fixture f, double x) => Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(x, -2), ["end"] = J(x, 2) }));
    static string Place(Fixture f, string sheet, string view) =>
        (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = view })!["viewportId"]!;
    static JsonObject Ids(params string[] ids) => new() { ["ids"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]) };

    [Fact]
    public void A_view_on_a_sheet_is_not_deleted_and_the_message_names_the_sheet()
    {
        var f = F();
        var s = Sheet(f, "Sections");
        var v = Section(f, 2);
        Place(f, s, v);
        var e = Assert.Throws<CommandException>(() => f.Run("element.delete", new JsonObject { ["id"] = v }));
        Assert.Equal($"„{f.Model.Get(v)!.Name}” is on sheet A-101 · Sections: remove it from the sheet first, then delete it.", e.Message);
        Assert.NotNull(f.Model.Get(v));
        Assert.Single(Sheets.Viewports(f.Model.Get(s)!));
    }

    [Fact]
    public void Taken_off_the_sheet_the_view_deletes_and_a_view_on_no_sheet_always_did()
    {
        var f = F();
        var s = Sheet(f, "Sections");
        var v = Section(f, 2);
        var free = Section(f, 4);
        var vp = Place(f, s, v);
        f.Run("element.delete", new JsonObject { ["id"] = free });
        Assert.Null(f.Model.Get(free));
        f.Run("sheet.removeViewport", new JsonObject { ["id"] = s, ["viewportId"] = vp });
        f.Run("element.delete", new JsonObject { ["id"] = v });
        Assert.Null(f.Model.Get(v));
    }

    [Fact]
    public void Several_at_once_with_one_on_a_sheet_delete_nothing_one_undo_step_rule()
    {
        var f = F();
        var s = Sheet(f, "Sections");
        var on = Section(f, 2);
        var free = Section(f, 4);
        var wall = f.Wall(0, 0, 5, 0);
        Place(f, s, on);
        var e = Assert.Throws<CommandException>(() => f.Run("element.delete", Ids(free, on, wall)));
        Assert.Contains("is on sheet A-101 · Sections", e.Message);
        Assert.NotNull(f.Model.Get(free));
        Assert.NotNull(f.Model.Get(on));
        Assert.NotNull(f.Model.Get(wall));
    }

    [Fact]
    public void Two_views_on_two_sheets_are_counted_with_the_sheet_numbers()
    {
        var f = F();
        var a = Sheet(f, "One");
        var b = Sheet(f, "Two");
        var v1 = Section(f, 2);
        var v2 = Section(f, 4);
        Place(f, a, v1);
        Place(f, b, v2);
        var e = Assert.Throws<CommandException>(() => f.Run("element.delete", Ids(v1, v2)));
        Assert.Equal("2 views are on sheets (A-101, A-102): remove them from the sheets first, then delete them. Nothing was deleted.", e.Message);
    }

    [Fact]
    public void A_view_on_two_sheets_by_its_boundaries_names_both()
    {
        var f = F();
        var a = Sheet(f, "One");
        var b = Sheet(f, "Two");
        var v = Section(f, 2);
        Place(f, a, v);
        // The same view on a second page of the second sheet (a view goes on once per page and boundary).
        f.Run("sheet.addViewport", new JsonObject { ["id"] = b, ["viewId"] = v, ["page"] = 2 });
        var e = Assert.Throws<CommandException>(() => f.Run("element.delete", new JsonObject { ["id"] = v }));
        Assert.Contains("is on sheets A-101 · One, A-102 · Two: remove it from the sheets first", e.Message);
    }

    [Fact]
    public void The_sheet_itself_still_deletes_and_with_its_views_together_too()
    {
        var f = F();
        var s = Sheet(f, "Sections");
        var v = Section(f, 2);
        Place(f, s, v);
        f.Run("element.delete", Ids(s, v));
        Assert.Null(f.Model.Get(s));
        Assert.Null(f.Model.Get(v));

        var s2 = Sheet(f, "Again");
        var v2 = Section(f, 3);
        Place(f, s2, v2);
        f.Run("element.delete", new JsonObject { ["id"] = s2 });
        Assert.Null(f.Model.Get(s2));
        f.Run("element.delete", new JsonObject { ["id"] = v2 });
        Assert.Null(f.Model.Get(v2));
    }

    [Fact]
    public void A_level_whose_default_plan_is_on_a_sheet_stays()
    {
        var f = F();
        var level = Id(f.Run("storey.create", new JsonObject()));
        var s = Sheet(f, "Plans");
        Place(f, s, "plan:" + level);
        var e = Assert.Throws<CommandException>(() => f.Run("element.delete", new JsonObject { ["id"] = level }));
        Assert.Contains("is on sheet A-101 · Plans", e.Message);
        Assert.NotNull(f.Model.Get(level));
    }

    [Fact]
    public void A_new_sheet_made_with_the_view_on_it_is_one_undo_step()
    {
        var f = F();
        var v = Section(f, 2);
        var r = f.Run("sheet.create", new JsonObject { ["name"] = "Sections", ["paper"] = "A3", ["viewId"] = v })!;
        var s = (string)r["id"]!;
        var vp = Assert.Single(Sheets.Viewports(f.Model.Get(s)!));
        Assert.Equal(v, vp.ViewId);
        Assert.Equal((string)r["viewportId"]!, vp.Id);
        // Placed where a view placed with no point goes: the middle of the paper left of the title block.
        Assert.Equal(297 / 2.0, vp.Y);
        f.Session.Undo();
        Assert.Null(f.Model.Get(s));
        // Already on a sheet: the new sheet is not made either.
        var s2 = Sheet(f, "Held");
        Place(f, s2, v);
        var count = f.Model.OfClass(Sheets.Class).Count();
        Assert.Throws<CommandException>(() => f.Run("sheet.create", new JsonObject { ["name"] = "Again", ["viewId"] = v }));
        Assert.Equal(count, f.Model.OfClass(Sheets.Class).Count());
    }
}
