using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Furniture;
using Lapin.Furniture.Models;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Furniture models of the user's own and symbols for a type's plan (docs/plan-user-extensibility.md §3.5, §5.5): a GLB brought in
/// with its licence, its extent and materials read; a type of it stretched like Lapin's own; its settings changed and its types
/// following its new id; the furniture library carrying it whole to another project; a symbol drawing a type's plan.
/// </summary>
public sealed class UserModelTests
{
    static Fixture F() => new(new ArchitectureModule(), new FurnitureModule());

    // A GLB to bring in: one of Lapin's CC0 models, as a user would have downloaded it.
    static string Glb() => Convert.ToBase64String(FurnitureModelFiles.Read(FurnitureModels.All[0].Id)!);

    static JsonObject Import(Fixture f, JsonObject? more = null)
    {
        var a = new JsonObject { ["data"] = Glb(), ["name"] = "My chair", ["licence"] = "CC0-1.0", ["source"] = "kenney.nl" };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return f.Run("furniture.importModel", a)!.AsObject();
    }

    static string Type(Fixture f, string model, double width) =>
        (string)f.Run("furniture.createType", new JsonObject { ["kind"] = "model", ["spec"] = new JsonObject { ["model"] = model, ["width"] = width } })!["id"]!;

    [Fact]
    public void A_GLB_becomes_a_model_a_type_stretches_by_its_zone()
    {
        var f = F();
        var r = Import(f, new JsonObject { ["zones"] = new JsonObject { ["x"] = new JsonArray(0.3, 0.7) } });
        var model = (string)r["model"]!;
        Assert.StartsWith(UserModels.Prefix, model);
        var natural = r["natural"]!.AsArray().Select(x => (double)x!).ToArray();
        Assert.True(r["materials"]!.AsArray().Count > 0);
        var type = Type(f, model, natural[0] * 2);
        var piece = (string)f.Run("furniture.place", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["against"] = false })!["id"]!;
        var (min, max) = f.Bounds(piece);
        Assert.Equal(natural[0] * 2, max.X - min.X, 3);
        Assert.Equal(natural[2], max.Z - min.Z, 3);
        // A licence is said; a file that is not a GLB is refused.
        Assert.Throws<CommandException>(() => f.Run("furniture.importModel", new JsonObject { ["data"] = Glb(), ["name"] = "x" }));
        Assert.Throws<CommandException>(() => f.Run("furniture.importModel", new JsonObject { ["data"] = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13]), ["licence"] = "CC0-1.0" }));
    }

    [Fact]
    public void Its_settings_changed_its_types_follow_and_it_is_not_deleted_while_used()
    {
        var f = F();
        var r = Import(f);
        var asset = (string)r["id"]!;
        var type = Type(f, (string)r["model"]!, 1);
        var changed = f.Run("furniture.updateModel", new JsonObject { ["id"] = asset, ["zones"] = new JsonObject { ["x"] = new JsonArray(0.4, 0.6) } })!;
        Assert.NotEqual((string)r["model"]!, (string)changed["model"]!);
        Assert.Equal((string)changed["model"]!, (string?)f.Model.Get(type)!.Attrs["spec"]!["model"]);
        Assert.Throws<CommandException>(() => f.Run("furniture.deleteModel", new JsonObject { ["id"] = asset }));
    }

    [Fact]
    public void The_furniture_library_carries_the_model_to_another_project()
    {
        var a = F();
        var r = Import(a);
        var type = Type(a, (string)r["model"]!, 0.8);
        var item = LibraryItems.Of(a.Model, a.Model.Get(type)!);
        Assert.NotNull(item["model"]?["glb"]);

        var b = F();
        var made = (string)b.Run("furniture.insertFromLibrary", new JsonObject { ["item"] = item })!["id"]!;
        Assert.Single(b.Model.OfClass(UserModels.Class));
        var piece = (string)b.Run("furniture.place", new JsonObject { ["storeyId"] = b.Storey, ["typeId"] = made, ["x"] = 0, ["y"] = 0, ["against"] = false })!["id"]!;
        var (min, max) = b.Bounds(piece);
        Assert.Equal(0.8, max.X - min.X, 3);
    }

    [Fact]
    public void A_symbol_draws_a_type_s_plan_in_place_of_the_body_seen_from_above()
    {
        var f = F();
        var symbol = (string)f.Run("symbols.create", new JsonObject
        {
            ["name"] = "Bathtub", ["def"] = new JsonObject { ["scale"] = "model", ["items"] = new JsonArray(new JsonObject { ["type"] = "rect", ["x"] = -850, ["y"] = -375, ["w"] = 1700, ["h"] = 750 }) },
        })!["id"]!;
        var type = (string)f.Run("furniture.createType", new JsonObject { ["kind"] = "model", ["spec"] = new JsonObject { ["model"] = FurnitureModels.All[0].Id, ["planSymbol"] = symbol } })!["id"]!;
        var piece = (string)f.Run("furniture.place", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 2, ["y"] = 1, ["against"] = false })!["id"]!;
        var plan = f.Session.Geometry(piece)!.Plan!;
        var items = plan.Where(p => p.Kind == "symbol").ToList();
        Assert.Single(items);
        Assert.Equal(1.7, items[0].Points.Max(p => p.X) - items[0].Points.Min(p => p.X), 6);
        Assert.Equal(2, items[0].Points.Average(p => p.X), 6);
    }
}
