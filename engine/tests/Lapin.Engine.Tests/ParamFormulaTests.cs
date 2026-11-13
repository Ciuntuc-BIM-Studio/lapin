using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Parameters worked out by formulas (docs/plan-user-extensibility.md §5.3): from the element's numbers, its other parameters (a
/// formula of a formula too) and, asked by a query, its quantities; never stored, so they follow a change; tags read them; a
/// formula that does not read is refused when it is set, one naming what is not there gives no value and says why.
/// </summary>
public sealed class ParamFormulaTests
{
    static Fixture F() => new(new ArchitectureModule());

    static void Set(Fixture f, string id, string name, JsonObject more)
    {
        var a = new JsonObject { ["ids"] = new JsonArray(id), ["name"] = name };
        foreach (var (k, v) in more) a[k] = v?.DeepClone();
        f.Run("element.setParam", a);
    }

    [Fact]
    public void A_formula_reads_the_element_s_numbers_and_other_parameters_and_follows_their_changes()
    {
        var f = F();
        var wall = f.Wall(0, 0, 5, 0);
        Set(f, wall, "Courses", new JsonObject { ["kind"] = "number", ["value"] = 12 });
        Set(f, wall, "Course height", new JsonObject { ["formula"] = "round(height / Courses; 3)" });
        Set(f, wall, "Check", new JsonObject { ["formula"] = "if(CourseHeight > 0.2; 1; 0)", ["kind"] = "bool" });
        var e = f.Model.Get(wall)!;
        var h = e.Num("height");
        Assert.Equal(Math.Round(h / 12, 3).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), TagText.Value(f.Model, e, "Course height"));
        // Nothing stored but the formula.
        var stored = (e.Attrs["params"] as JsonArray)!.OfType<JsonObject>().Single(p => (string)p["name"]! == "Course height");
        Assert.Null(stored["value"]);
        Assert.Equal("round(height / Courses; 3)", (string)stored["formula"]!);
        // A change of what it reads: the value follows.
        Set(f, wall, "Courses", new JsonObject { ["value"] = 10 });
        Assert.Equal(Math.Round(h / 10, 3).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), TagText.Value(f.Model, f.Model.Get(wall)!, "Course height"));
        Assert.Equal(h / 10 > 0.2 ? "yes" : "no", TagText.Value(f.Model, f.Model.Get(wall)!, "Check"));
    }

    [Fact]
    public void A_query_works_formulas_out_with_quantities_and_says_why_one_has_no_value()
    {
        var f = F();
        var wall = f.Wall(0, 0, 5, 0);
        Set(f, wall, "Bricks", new JsonObject { ["formula"] = "ceil(Length * height * 50)" });
        Set(f, wall, "Broken", new JsonObject { ["formula"] = "Nothing * 2" });
        var list = f.Session.Query("params.evaluate", new JsonObject { ["id"] = wall })!["params"]!.AsArray().OfType<JsonObject>().ToList();
        var bricks = list.Single(p => (string)p["name"]! == "Bricks");
        Assert.Equal(Math.Ceiling(5 * f.Model.Get(wall)!.Num("height") * 50), (double)bricks["value"]!, 6);
        var broken = list.Single(p => (string)p["name"]! == "Broken");
        Assert.Null(broken["value"]);
        Assert.Contains("Nothing", (string)broken["error"]!);
        Assert.Equal(10, (double)f.Session.Query("params.tryFormula", new JsonObject { ["id"] = wall, ["formula"] = "Length * 2" })!["value"]!, 6);
        // A formula that does not read is refused; a text's formula gives a text (decision 550), a template only a text.
        Assert.Throws<CommandException>(() => Set(f, wall, "Bad", new JsonObject { ["formula"] = "1 +" }));
        Set(f, wall, "Words", new JsonObject { ["formula"] = "2", ["kind"] = "text" });
        Assert.Equal("2", TagText.Value(f.Model, f.Model.Get(wall)!, "Words"));
        Assert.Throws<CommandException>(() => Set(f, wall, "Template", new JsonObject { ["formula"] = "{name}", ["kind"] = "number" }));
        Assert.Throws<CommandException>(() => Set(f, wall, "Open", new JsonObject { ["formula"] = "{name" }));
    }

    [Fact]
    public void A_formula_on_a_circle_of_names_stops_with_no_value()
    {
        var f = F();
        var wall = f.Wall(0, 0, 5, 0);
        Set(f, wall, "A", new JsonObject { ["formula"] = "B + 1" });
        Set(f, wall, "B", new JsonObject { ["formula"] = "A + 1" });
        var a = f.Session.Query("params.evaluate", new JsonObject { ["id"] = wall })!["params"]!.AsArray().OfType<JsonObject>().Single(p => (string)p["name"]! == "A");
        Assert.Null(a["value"]);
    }
}
