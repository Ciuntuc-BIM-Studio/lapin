using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Shop drawings, stage S1 (docs/plan-shop-drawings.md, decision 581): a single part on a sheet by the generic rule, laid out
/// in ISO E or third angle on the first paper that fits, made again with every id kept, undone in one step, previewed without
/// being kept, with the part's overall dimensions on its views.
/// </summary>
public class ShopDrawingTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());

    static string Id(JsonNode? n) => (string)n!["id"]!;

    static string Box(Fixture f, double length, double width, double height, double x = 0, double y = 0) =>
        Id(f.Run("studio.primitive", new JsonObject { ["shape"] = "box", ["at"] = new JsonArray(x, y), ["width"] = width, ["length"] = length, ["height"] = height }));

    /// <summary>Where each view's drawing middle falls on the paper (mm), by its role.</summary>
    static Dictionary<string, Vec2> Middles(Fixture f, JsonObject made)
    {
        var sheet = f.Model.Get((string)made["sheet"]!)!;
        var o = new Dictionary<string, Vec2>();
        foreach (var (key, idNode) in (JsonObject)made["views"]!)
        {
            var view = f.Model.Get((string)idNode!)!;
            var vp = Sheets.Viewports(sheet).Single(v => v.ViewId == view.Id);
            var k = 1000 / view.Num("scale");
            var c = vp.Center ?? default;
            o[key] = new Vec2(vp.X - c.X * k, vp.Y + c.Y * k);
        }
        return o;
    }

    [Fact]
    public void The_generic_rule_puts_front_top_right_and_iso_in_first_angle_places_on_an_a4()
    {
        var f = F();
        var box = Box(f, 0.6, 0.4, 0.3);
        var made = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = box })!;
        Assert.Equal("A4", (string?)made["paper"]);
        Assert.True((bool)made["fits"]!);
        var views = (JsonObject)made["views"]!;
        Assert.Equal(["front", "iso", "right", "top"], views.Select(kv => kv.Key).Order(StringComparer.Ordinal).ToArray());
        var at = Middles(f, made);
        // ISO E: the top view under the front, in line; the view from the right on the front's left, in line; the iso to their right.
        Assert.Equal(at["front"].X, at["top"].X, 6);
        Assert.True(at["top"].Y > at["front"].Y);
        Assert.Equal(at["front"].Y, at["right"].Y, 6);
        Assert.True(at["right"].X < at["front"].X);
        Assert.True(at["iso"].X > at["front"].X);
        // Every view is an element view of the box, on the sheet of the drawing, in the Shop drawings set.
        foreach (var (_, id) in views)
        {
            var v = f.Model.Get((string)id!)!;
            Assert.Equal(box, ElementViews.ElementOf(v));
            Assert.Equal(ShopDrawings.Discipline, v.Str("discipline"));
        }
        var sheet = f.Model.Get((string)made["sheet"]!)!;
        Assert.Equal("A4", sheet.Str("paper"));
        // Nothing past the frame or over the title block.
        var placed = Sheets.Place(f.Session.Reader, sheet);
        Assert.All(placed, p => Assert.Empty(Sheets.Problems(f.Model, sheet, p)));
        Assert.NotEmpty(Sheets.Pdf(f.Session.Reader, [sheet]));
    }

    [Fact]
    public void Third_angle_puts_the_top_view_over_the_front_and_the_right_on_its_right()
    {
        var f = F();
        var box = Box(f, 0.6, 0.4, 0.3);
        var made = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = box, ["projection"] = "third-angle" })!;
        var at = Middles(f, made);
        Assert.Equal(at["front"].X, at["top"].X, 6);
        Assert.True(at["top"].Y < at["front"].Y);
        Assert.True(at["right"].X > at["front"].X);
        Assert.Equal(at["front"].Y, at["right"].Y, 6);
    }

    [Fact]
    public void Made_again_it_keeps_every_id_and_the_users_edits()
    {
        var f = F();
        var box = Box(f, 0.6, 0.4, 0.3);
        var first = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = box })!;
        var sheet = f.Model.Get((string)first["sheet"]!)!;
        var vps = Sheets.Viewports(sheet).Select(v => v.Id).Order(StringComparer.Ordinal).ToList();
        var front = (string)first["views"]!["front"]!;
        // The user's own: the front renamed, a line drafted on it.
        f.Run("element.rename", new JsonObject { ["id"] = front, ["name"] = "My front" });
        f.Run("drafting.draw", new JsonObject { ["viewId"] = front, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(0.1, 0.1) }) });
        var again = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = box })!;
        Assert.Equal((string)first["id"]!, (string)again["id"]!);
        Assert.Equal((string)first["sheet"]!, (string)again["sheet"]!);
        foreach (var (key, id) in (JsonObject)first["views"]!) Assert.Equal((string)id!, (string)again["views"]![key]!);
        Assert.Equal(vps, Sheets.Viewports(f.Model.Get((string)again["sheet"]!)!).Select(v => v.Id).Order(StringComparer.Ordinal).ToList());
        Assert.Equal("My front", f.Model.Get(front)!.Name);
        Assert.Single(f.Model.OfClass(IfcClass.Annotation), x => x.Str("viewId") == front);
        Assert.Single(f.Model.OfClass(ShopDrawings.Class));
        Assert.Single(f.Model.OfClass(Sheets.Class));
        // Without the top view: its view goes, the others stay with their ids.
        var fewer = (JsonObject)f.Run("drawing.update", new JsonObject { ["id"] = (string)first["id"]!, ["views"] = new JsonArray("front", "right") })!;
        Assert.Null(f.Model.Get((string)first["views"]!["top"]!));
        Assert.Equal(front, (string)fewer["views"]!["front"]!);
        // Made again later, the choices it was made with are kept.
        var kept = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = box })!;
        Assert.Null(kept["views"]!["top"]);
    }

    [Fact]
    public void One_undo_takes_the_whole_drawing_away()
    {
        var f = F();
        var box = Box(f, 0.6, 0.4, 0.3);
        var views = f.Model.OfClass(Views.Class).Count();
        f.Run("drawing.create", new JsonObject { ["element"] = box });
        Assert.Single(f.Model.OfClass(Sheets.Class));
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(Sheets.Class));
        Assert.Empty(f.Model.OfClass(ShopDrawings.Class));
        Assert.Equal(views, f.Model.OfClass(Views.Class).Count());
        // drawing.remove: the sheet and the views go with it, in one step too.
        var made = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = box })!;
        f.Run("drawing.remove", new JsonObject { ["id"] = (string)made["id"]! });
        Assert.Empty(f.Model.OfClass(Sheets.Class));
        Assert.Equal(views, f.Model.OfClass(Views.Class).Count());
        f.Session.Undo();
        Assert.Single(f.Model.OfClass(Sheets.Class));
    }

    [Fact]
    public void A_part_too_long_for_an_a4_goes_on_an_a3_or_down_the_scales_when_asked()
    {
        var f = F();
        var box = Box(f, 2.0, 0.4, 0.3);
        var byPaper = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = box, ["scale"] = 10 })!;
        Assert.Equal("A3", (string?)byPaper["paper"]);
        Assert.Equal(10.0, (double)byPaper["scale"]!);
        var byScale = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = box, ["scale"] = 10, ["paper"] = "A4", ["prefer"] = "scale" })!;
        Assert.Equal("A4", (string?)byScale["paper"]);
        Assert.True((double)byScale["scale"]! > 10);
        Assert.Contains((int)(double)byScale["scale"]!, ElementViews.ShopScales);
        Assert.True((bool)byScale["fits"]!);
    }

    /// <summary>The texts a drawing draws.</summary>
    static List<string> Texts(JsonObject d) =>
        ((JsonArray)d["pens"]!).OfType<JsonObject>().Where(p => p["texts"] is JsonArray).SelectMany(p => ((JsonArray)p["texts"]!).OfType<JsonObject>()).Select(t => (string)t["text"]!).ToList();

    [Fact]
    public void The_views_show_the_parts_overall_dimensions_which_follow_it()
    {
        var f = F();
        var box = Box(f, 0.6, 0.4, 0.3);
        var made = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = box })!;
        var front = (string)made["views"]!["front"]!;
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = front })!;
        Assert.Contains("600", Texts(d));
        Assert.Contains("300", Texts(d));
        var top = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = (string)made["views"]!["top"]! })!;
        Assert.Contains("400", Texts(top));
        // The part made longer: its dimension says so.
        f.Run("element.update", new JsonObject { ["id"] = box, ["attrs"] = new JsonObject { ["length"] = 0.8 } });
        var after = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = front })!;
        Assert.Contains("800", Texts(after));
        // No dimensions: none drawn.
        f.Run("drawing.create", new JsonObject { ["element"] = box, ["dims"] = "none" });
        Assert.DoesNotContain("300", Texts((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = front })!));
    }

    [Fact]
    public void The_preview_draws_the_sheet_without_keeping_anything()
    {
        var f = F();
        var box = Box(f, 0.6, 0.4, 0.3);
        var count = f.Model.Entities.Count();
        var page = (JsonObject)f.Session.Query("drawing.preview", new JsonObject { ["element"] = box, ["views"] = new JsonArray("front", "top") })!;
        Assert.Equal(count, f.Model.Entities.Count());
        Assert.Equal("A4", (string?)page["paper"]);
        Assert.NotEmpty((JsonArray)page["pens"]!);
        // Its views and the part's data table.
        Assert.Equal(4, ((JsonArray)page["viewports"]!).Count);
        var rules = (JsonObject)f.Session.Query("drawing.rules", new JsonObject { ["id"] = box })!;
        Assert.Equal("generic", (string?)rules["rule"]);
        Assert.Equal("iso-e", (string?)rules["projection"]);
    }

    [Fact]
    public void A_steel_member_gets_a_drawing_with_its_data()
    {
        var f = F();
        var member = Id(f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["z1"] = 1, ["x2"] = 4, ["y2"] = 3, ["z2"] = 1 }));
        var made = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = member })!;
        var rows = ShopDrawings.DataRows(f.Session.Reader, f.Model.Get(member)!);
        Assert.Contains(rows, r => r.Label == "SIZE" && r.Value.StartsWith("5000 ×", StringComparison.Ordinal));
        Assert.Contains(rows, r => r.Label == "WEIGHT");
        var sheet = f.Model.Get((string)made["sheet"]!)!;
        var page = (JsonObject)f.Session.Query("sheet.drawing", new JsonObject { ["id"] = sheet.Id })!;
        Assert.Contains(Texts(page), t => t.StartsWith("5000 ×", StringComparison.Ordinal));
    }
}
