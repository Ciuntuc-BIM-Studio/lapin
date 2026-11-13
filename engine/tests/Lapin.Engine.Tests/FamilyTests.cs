using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Families of the user's own (docs/plan-user-extensibility.md §5.6): bodies whose corners and depths are formulas of parameters,
/// an object's own values over the defaults, a body made only when its condition holds, turned and moved as any element, a family
/// made from a sketch with its width and height parameters, written to IFC as the class it names; what does not read refused.
/// </summary>
public sealed class FamilyTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(params JsonNode?[] v) => new(v);

    // An angle bracket standing on the xz plane: W wide, H high, T thick, L long (along y); a shelf when HasShelf.
    static JsonObject Bracket() => new()
    {
        ["ifcClass"] = "IfcFurnishingElement",
        ["params"] = J(
            new JsonObject { ["key"] = "W", ["default"] = 0.4 }, new JsonObject { ["key"] = "H", ["default"] = 0.3 },
            new JsonObject { ["key"] = "T", ["default"] = 0.02 }, new JsonObject { ["key"] = "L", ["default"] = 0.1 },
            new JsonObject { ["key"] = "HasShelf", ["kind"] = "bool", ["default"] = 0 }),
        ["bodies"] = J(
            new JsonObject { ["name"] = "Angle", ["plane"] = "xz", ["points"] = J(J(0, 0), J("W", 0), J("W", "T"), J("T", "T"), J("T", "H"), J(0, "H")), ["depth"] = "L" },
            new JsonObject { ["name"] = "Shelf", ["plane"] = "xy", ["when"] = "HasShelf", ["offset"] = J(0, 0, "H"), ["points"] = J(J(0, 0), J("W", 0), J("W", "L"), J(0, "L")), ["depth"] = "T" }),
    };

    static string Family(Fixture f, JsonObject def) => (string)f.Run("studio.createFamily", new JsonObject { ["name"] = "Bracket", ["def"] = def })!["id"]!;
    static string Place(Fixture f, string family, double x = 0, double y = 0, double rotation = 0, JsonObject? values = null) =>
        (string)f.Run("studio.placeFamily", new JsonObject { ["familyId"] = family, ["storeyId"] = f.Storey, ["at"] = J(x, y), ["rotation"] = rotation, ["values"] = values })!["id"]!;

    [Fact]
    public void An_object_s_body_follows_its_family_s_formulas_and_its_own_values()
    {
        var f = F();
        var fam = Family(f, Bracket());
        var a = Place(f, fam);
        var (min, max) = f.Bounds(a);
        Assert.Equal(0.4, max.X - min.X, 6);
        Assert.Equal(0.3, max.Z - min.Z, 6);
        Assert.Equal(0.1, Math.Abs(max.Y - min.Y), 6);
        var volume = f.Session.Quantities(a).SelectMany(q => q.Quantities).Single(q => q.Name == "NetVolume").Value;
        Assert.Equal((0.4 * 0.02 + (0.3 - 0.02) * 0.02) * 0.1, volume, 9);

        // Its own width; a shelf when it asks for one.
        f.Run("studio.setFamilyValues", new JsonObject { ["ids"] = J(a), ["values"] = new JsonObject { ["W"] = 0.6, ["HasShelf"] = true } });
        var g = f.Session.Geometry(a)!;
        Assert.Equal(2, g.Parts.Count);
        (min, max) = f.Bounds(a);
        Assert.Equal(0.6, max.X - min.X, 6);
        Assert.Equal(0.32, max.Z - min.Z, 6);
        // Back to the default with null.
        f.Run("studio.setFamilyValues", new JsonObject { ["ids"] = J(a), ["values"] = new JsonObject { ["W"] = null, ["HasShelf"] = null } });
        Assert.Single(f.Session.Geometry(a)!.Parts);
        Assert.Throws<CommandException>(() => f.Run("studio.setFamilyValues", new JsonObject { ["ids"] = J(a), ["values"] = new JsonObject { ["Nope"] = 1 } }));
    }

    [Fact]
    public void An_object_turns_and_moves_as_any_element_and_its_family_is_not_deleted_while_used()
    {
        var f = F();
        var fam = Family(f, Bracket());
        var a = Place(f, fam, 2, 1, 90);
        var (min, max) = f.Bounds(a);
        // Turned a quarter: its width along y, from y 1 to 1.4; its length along −x.
        Assert.Equal(0.4, max.Y - min.Y, 6);
        Assert.Equal(1, min.Y, 6);
        f.Run("element.transform", new JsonObject { ["ids"] = J(a), ["op"] = "move", ["by"] = J(1, 0) });
        Assert.Equal(3, Args.PointOf(f.Model.Get(a)!.Attrs["at"]).X, 6);
        Assert.Throws<CommandException>(() => f.Run("studio.deleteFamily", new JsonObject { ["id"] = fam }));
    }

    [Fact]
    public void A_sketch_becomes_a_family_whose_width_and_height_stretch_it()
    {
        var f = F();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        // An L outline 2 × 1 in plan.
        var ids = f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = plan,
            ["shapes"] = J(new JsonObject { ["type"] = "polyline", ["pts"] = J(J(5, 5), J(7, 5), J(7, 5.4), J(5.4, 5.4), J(5.4, 6), J(5, 6)), ["closed"] = true }),
        })!["ids"]!.AsArray();
        var made = f.Run("studio.familyFromSketch", new JsonObject { ["viewId"] = plan, ["ids"] = ids.DeepClone(), ["name"] = "Counter", ["depth"] = 0.9 })!;
        var def = made["def"]!.AsObject();
        Assert.Equal(["W", "H", "D"], def["params"]!.AsArray().Select(p => (string)p!["key"]!).ToArray());
        var obj = Place(f, (string)made["id"]!, 0, 0, 0, new JsonObject { ["W"] = 3 });
        var (min, max) = f.Bounds(obj);
        Assert.Equal(3, max.X - min.X, 6);
        Assert.Equal(1, max.Y - min.Y, 6);
        Assert.Equal(0.9, max.Z - min.Z, 6);
        // Its inner corners keep their place: the notch stays 0.4 deep from the front, 0.4 in from the left.
        var volume = f.Session.Quantities(obj).SelectMany(q => q.Quantities).Single(q => q.Name == "NetVolume").Value;
        Assert.Equal((3 * 0.4 + 0.4 * 0.6) * 0.9, volume, 9);
    }

    [Fact]
    public void A_family_that_does_not_read_is_refused_and_an_object_is_written_as_its_class()
    {
        var f = F();
        var bad = Bracket();
        ((JsonObject)bad["bodies"]![0]!)["depth"] = "Z * 2";
        Assert.Throws<CommandException>(() => Family(f, bad));
        var none = Bracket();
        ((JsonObject)none["bodies"]![0]!)["when"] = "0";
        Assert.Throws<CommandException>(() => Family(f, none));
        var fam = Family(f, Bracket());
        Place(f, fam);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "family.ifc", new IfcExportOptions());
        Assert.Contains("IFCFURNISHINGELEMENT(", Encoding.UTF8.GetString(ms.ToArray()));
        // A family file carries it to another project.
        var text = (string)f.Session.Query("studio.exportFamily", new JsonObject { ["id"] = fam })!["text"]!;
        var g = F();
        var copy = (string)g.Run("studio.importFamily", new JsonObject { ["text"] = text })!["id"]!;
        Assert.Equal(0.4, g.Bounds(Place(g, copy)).Max.X - g.Bounds(g.Model.OfClass(Families.InstanceClass).First().Id).Min.X, 6);
    }
}
