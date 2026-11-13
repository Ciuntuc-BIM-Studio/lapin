using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// A tester's topic #37 (decision 551): a room tag or any tag drawn by a symbol of the user's — its lines as drawn, its text slots
/// the element's fields and formulas ({storey.code}.{number}, {name}, {area}, {Floor finish}, {= …}) — placed tags following the
/// element and what it reads; a symbol gone leaves the type's template.
/// </summary>
public sealed class TagSymbolTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule());

    static void Set(Fixture f, string id, string name, JsonObject more)
    {
        var a = new JsonObject { ["ids"] = new JsonArray(id), ["name"] = name };
        foreach (var (k, v) in more) a[k] = v?.DeepClone();
        f.Run("element.setParam", a);
    }

    static JsonObject Text(double x, double y, string text, double h = 2.5) => new() { ["type"] = "text", ["at"] = new JsonArray(x, y), ["text"] = text, ["h"] = h };

    /// <summary>Rychlas' layout: index | name on the first row, area | finish under it, in a box split by lines.</summary>
    static string RoomSymbol(Fixture f) => (string)f.Run("symbols.create", new JsonObject
    {
        ["name"] = "Room tag K",
        ["def"] = new JsonObject
        {
            ["scale"] = "annotation", ["category"] = "annotation",
            ["items"] = new JsonArray(
                new JsonObject { ["type"] = "rect", ["x"] = -20, ["y"] = -6, ["w"] = 40, ["h"] = 12 },
                new JsonObject { ["type"] = "line", ["a"] = new JsonArray(-20, 0), ["b"] = new JsonArray(20, 0) },
                new JsonObject { ["type"] = "line", ["a"] = new JsonArray(0, -6), ["b"] = new JsonArray(0, 6) },
                Text(-18, 1.5, "{storey.code}.{number}"), Text(2, 1.5, "{name}"),
                Text(-18, -4.5, "{area}"), Text(2, -4.5, "{Floor finish}"), Text(-18, -9, "{= upper(left(FloorFinish; 1))}", 1.8)),
        },
    })!["id"]!;

    static List<string> Texts(Fixture f, string plan) =>
        Views.Render(new Q(f), Views.Find(f.Model, plan)!).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();

    [Fact]
    public void A_room_stamp_drawn_by_a_symbol_shows_the_room_s_fields_in_its_slots_and_follows_them()
    {
        var f = F();
        Set(f, f.Storey, "code", new JsonObject { ["kind"] = "text", ["value"] = "K1" });
        var room = (string)f.Run("architecture.addRoom", new JsonObject
        {
            ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 4.9, ["name"] = "Living", ["number"] = "1",
        })!["id"]!;
        Set(f, room, "Floor finish", new JsonObject { ["kind"] = "text", ["value"] = "Oak" });
        var symbol = RoomSymbol(f);
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "room", ["symbol"] = symbol } });
        Assert.Equal(symbol, Standards.TagTypeOf(DraftingResources.Book(f.Model), "room")!.Symbol);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var texts = Texts(f, plan);
        Assert.Contains("K1.1", texts);
        Assert.Contains("Living", texts);
        Assert.Contains(texts, t => t.StartsWith("24.5", StringComparison.Ordinal) && t.Contains("m²"));
        Assert.Contains("Oak", texts);
        Assert.Contains("O", texts);
        // The template's lines are not drawn as well.
        Assert.DoesNotContain("1", texts);
        // The storey's code and the room's finish change: the stamp follows.
        Set(f, f.Storey, "code", new JsonObject { ["value"] = "K2" });
        Set(f, room, "Floor finish", new JsonObject { ["value"] = "Tiles" });
        texts = Texts(f, plan);
        Assert.Contains("K2.1", texts);
        Assert.Contains("Tiles", texts);
        Assert.DoesNotContain("K1.1", texts);
        // The stamp is picked by its box: a mark of the room's with its point.
        var drawing = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        Assert.Contains(drawing["marks"]!.AsArray().OfType<JsonObject>(), m => (string?)m["id"] == room);
        // The symbol deleted: the stamp is the template's text again.
        f.Run("symbols.delete", new JsonObject { ["id"] = symbol });
        texts = Texts(f, plan);
        Assert.Contains("Living", texts);
        Assert.DoesNotContain("K2.1", texts);
    }

    [Fact]
    public void A_tag_type_with_a_symbol_draws_it_for_any_element_with_its_leader()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0);
        Set(f, wall, "Fire rating", new JsonObject { ["kind"] = "text", ["value"] = "EI 60" });
        var symbol = (string)f.Run("symbols.create", new JsonObject
        {
            ["name"] = "Fire circle",
            ["def"] = new JsonObject
            {
                ["scale"] = "annotation", ["category"] = "annotation",
                ["items"] = new JsonArray(new JsonObject { ["type"] = "circle", ["c"] = new JsonArray(0, 0), ["r"] = 5 },
                    new JsonObject { ["type"] = "text", ["at"] = new JsonArray(0, 0), ["text"] = "{Fire rating} / {storey.name}", ["h"] = 2, ["align"] = "middle", ["valign"] = "middle" }),
            },
        })!["id"]!;
        var type = (string)f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["name"] = "Fire (drawn)", ["template"] = "{Fire rating}", ["symbol"] = symbol } })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var tag = (string)f.Run("drafting.addTag", new JsonObject { ["viewId"] = plan, ["element"] = wall, ["tagType"] = type, ["at"] = new JsonArray(3, 3) })!["id"]!;
        var storeyName = f.Model.Get(f.Storey)!.Name;
        Assert.Contains($"EI 60 / {storeyName}", Texts(f, plan));
        var drawing = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        var mark = drawing["marks"]!.AsArray().OfType<JsonObject>().Single(m => (string?)m["id"] == tag);
        Assert.True((bool)mark["data"]!["symbol"]!);
        Assert.Equal(wall, (string?)mark["data"]!["of"]);
        // Its leader runs from the symbol's box to the wall.
        Assert.NotNull(mark["data"]!["end"]);
        Set(f, wall, "Fire rating", new JsonObject { ["value"] = "EI 90" });
        Assert.Contains($"EI 90 / {storeyName}", Texts(f, plan));
    }
}
