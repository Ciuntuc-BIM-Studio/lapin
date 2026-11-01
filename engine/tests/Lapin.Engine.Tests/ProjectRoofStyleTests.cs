using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The project's own roof styles (decision 233): a roof configured by hand saved under a name, offered beside the catalogue's,
/// put on other roofs and new ones, saved again (its roofs follow), renamed and deleted.
/// </summary>
public class ProjectRoofStyleTests
{
    static JsonArray Rect(double w, double d) => new(new JsonArray(0, 0), new JsonArray(w, 0), new JsonArray(w, d), new JsonArray(0, d));

    static string AddRoof(Fixture f, string style, double w = 10, double d = 6) =>
        (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Rect(w, d), ["style"] = style })!["id"]!;

    [Fact]
    public void A_roof_configured_by_hand_is_saved_as_a_style_and_put_on_others()
    {
        var f = new Fixture();
        var a = AddRoof(f, "fr_provence");
        // From the configurator's draft: a style, two fields changed, then saved.
        var sid = (string)f.Run("architecture.saveRoofStyle", new JsonObject
        {
            ["id"] = a, ["name"] = "Our farmhouse", ["style"] = "se_truss", ["attrs"] = new JsonObject { ["pitch"] = 32, ["covering"] = "Metal sheet" },
        })!["styleId"]!;
        Assert.StartsWith(ProjectRoofStyles.Prefix, sid);
        var style = Assert.Single(ProjectRoofStyles.All(f.Model));
        Assert.Equal(("Our farmhouse", "gable", 32.0, "trusses"), ((string)style["label"]!, (string)style["shape"]!, (double)style["pitch"]!, (string)style["structure"]!));
        // The roof it was saved from takes it.
        var e = f.Model.Get(a)!;
        Assert.Equal((sid, 32.0, "Metal sheet", "trusses"), (e.Str("style"), e.Num("pitch"), e.Str("covering"), e.Str("structure")));

        // Another roof in it, and a new one drawn in it.
        var b = AddRoof(f, "traditional_campie", 12, 8);
        f.Run("architecture.roofStyle", new JsonObject { ["id"] = b, ["style"] = sid });
        var c = AddRoof(f, sid, 8, 8);
        foreach (var id in new[] { b, c })
        {
            var x = f.Model.Get(id)!;
            Assert.Equal((sid, "gable", 32.0, "Metal sheet", "trusses", "45x170 mm"), (x.Str("style"), x.Str("shape"), x.Num("pitch"), x.Str("covering"), x.Str("structure"), x.Str("rafterSection")));
        }
        // Not where the roof is: its outline stays its own.
        Assert.Equal(12, Roof.Contour(f.Model.Get(b)!).Max(p => p.X), 9);
    }

    [Fact]
    public void Saved_again_under_its_name_every_roof_of_the_style_follows()
    {
        var f = new Fixture();
        var a = AddRoof(f, "fr_provence");
        var b = AddRoof(f, "fr_provence", 8, 5);
        var sid = (string)f.Run("architecture.saveRoofStyle", new JsonObject { ["id"] = a, ["name"] = "House", ["attrs"] = new JsonObject { ["pitch"] = 35 } })!["styleId"]!;
        f.Run("architecture.roofStyle", new JsonObject { ["id"] = b, ["style"] = sid });
        // Changed on the one roof and saved again by name: the same style, both roofs at 40°.
        var again = (string)f.Run("architecture.saveRoofStyle", new JsonObject { ["id"] = a, ["name"] = "house", ["attrs"] = new JsonObject { ["pitch"] = 40 } })!["styleId"]!;
        Assert.Equal(sid, again);
        Assert.Single(ProjectRoofStyles.All(f.Model));
        Assert.Equal(40, f.Model.Get(b)!.Num("pitch"));
        // One step: undone, both back at 35°.
        f.Session.Undo();
        Assert.Equal(35, f.Model.Get(a)!.Num("pitch"));
        Assert.Equal(35, f.Model.Get(b)!.Num("pitch"));
        // Renamed; deleted, its roofs keep how they look and are shaped by hand.
        f.Run("architecture.renameRoofStyle", new JsonObject { ["styleId"] = sid, ["name"] = "Old house" });
        Assert.Equal("Old house", (string)ProjectRoofStyles.All(f.Model)[0]["label"]!);
        f.Run("architecture.deleteRoofStyle", new JsonObject { ["styleId"] = sid });
        Assert.Empty(ProjectRoofStyles.All(f.Model));
        Assert.Null(f.Model.Get(b)!.Str("style"));
        Assert.Equal(35, f.Model.Get(b)!.Num("pitch"));
        Assert.Throws<CommandException>(() => f.Run("architecture.saveRoofStyle", new JsonObject { ["id"] = a, ["name"] = "  " }));
    }

    [Fact]
    public void A_vault_s_rise_goes_with_the_style_as_a_share_of_the_span()
    {
        var f = new Fixture();
        var a = AddRoof(f, "modern_barrel");                             // 10 × 6: a 6 m span
        var sid = (string)f.Run("architecture.saveRoofStyle", new JsonObject { ["id"] = a, ["name"] = "Vault", ["attrs"] = new JsonObject { ["rise"] = 1.5 } })!["styleId"]!;
        var b = AddRoof(f, sid, 12, 8);                                  // an 8 m span
        Assert.Equal(2.0, f.Model.Get(b)!.Num("rise"), 3);
    }
}
