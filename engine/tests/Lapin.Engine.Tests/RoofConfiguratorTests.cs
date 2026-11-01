using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Engine.Tests;

/// <summary>
/// The roof configurator (decision 231): a draft (a style, fields, edge rules) previewed without touching the model — its plan,
/// a section through its framing, what it takes — and applied as one step.
/// </summary>
public class RoofConfiguratorTests
{
    static JsonArray House10x6 => new(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6));

    static string AddRoof(Fixture f, string style) =>
        (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = House10x6, ["style"] = style })!["id"]!;

    static JsonObject Preview(Fixture f, JsonObject args) => (JsonObject)f.Session.Query("architecture.roofPreview", args)!;

    static List<string> Roles(JsonObject p) => ((JsonArray)p["section"]!["items"]!).Select(i => (string)i!["role"]!).ToList();

    [Fact]
    public void A_style_previewed_shows_its_framing_and_leaves_the_roof_as_it_was()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "fr_provence");                            // gable 24°, rafters on purlins
        var p = Preview(f, new JsonObject { ["id"] = roof, ["style"] = "se_truss" });
        Assert.Null(p["problem"]);
        Assert.Equal("trusses", (string?)p["attrs"]!["structure"]);
        Assert.Equal(27, (double)p["attrs"]!["pitch"]!);
        // The plan: four edges, two eaves and two gables, one ridge.
        Assert.Equal(["eave", "gable", "eave", "gable"], ((JsonArray)p["kinds"]!).Select(k => (string)k!));
        Assert.Single((JsonArray)p["lines"]!, l => (string?)l!["kind"] == "ridge");
        // The section across the ridge: the covering on both slopes, the walls and plates, one truss with its four webs.
        var roles = Roles(p);
        Assert.Equal(2, roles.Count(r => r == "covering"));
        Assert.Equal(2, roles.Count(r => r == "wall"));
        Assert.Equal(2, roles.Count(r => r == "plate"));
        Assert.Equal(2, roles.Count(r => r == "topChord"));
        Assert.Equal(1, roles.Count(r => r == "bottomChord"));
        Assert.Equal(4, roles.Count(r => r == "web"));
        Assert.Equal(6, (double)p["section"]!["span"]!, 6);
        Assert.Contains((JsonArray)p["summary"]!, row => (string?)row![0] == "Trusses" && (string?)row[1] == "8");
        // Nothing changed.
        Assert.Equal("fr_provence", f.Model.Get(roof)!.Str("style"));
        Assert.Equal("rafters", f.Model.Get(roof)!.Str("structure"));
        // Rafters on purlins: the section cuts the purlins and the ridge beam.
        var own = Roles(Preview(f, new JsonObject { ["id"] = roof }));
        Assert.Contains("purlin", own);
        Assert.Contains("ridge", own);
        Assert.Equal(2, own.Count(r => r == "rafter"));
    }

    [Fact]
    public void A_collar_couple_shows_in_the_section_and_a_bad_field_comes_back_as_the_problem()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "nordic_collar");
        var roles = Roles(Preview(f, new JsonObject { ["id"] = roof }));
        Assert.Equal(2, roles.Count(r => r == "rafter"));
        Assert.Single(roles, r => r == "collar");
        var bad = Preview(f, new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["pitch"] = 120 } });
        Assert.NotNull(bad["problem"]);
        Assert.Equal(38, (double)bad["attrs"]!["pitch"]!);
    }

    [Fact]
    public void Applied_a_style_with_fields_and_edges_is_one_step()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "traditional_campie");                     // hip
        f.Run("architecture.updateRoof", new JsonObject
        {
            ["id"] = roof, ["style"] = "se_truss",
            ["attrs"] = new JsonObject { ["pitch"] = 30, ["timberGrade"] = "C30" },
            ["edges"] = new JsonArray(null, new JsonObject { ["overhang"] = 0.1 }, null, null),
        });
        var e = f.Model.Get(roof)!;
        Assert.Equal(("gable", "trusses", 30.0, "C30"), (e.Str("shape"), e.Str("structure"), e.Num("pitch"), e.Str("timberGrade")));
        Assert.Equal(0.1, (double)((JsonArray)e.Attrs["edges"]!)[1]!["overhang"]!, 9);
        // Changed by hand past its style: no longer exactly the style.
        Assert.Null(e.Str("style"));
        f.Session.Undo();
        e = f.Model.Get(roof)!;
        Assert.Equal(("hip", "rafters", "traditional_campie"), (e.Str("shape"), e.Str("structure"), e.Str("style")));
    }
}
