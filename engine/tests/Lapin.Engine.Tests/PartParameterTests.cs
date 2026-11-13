using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// A part's parameter table (docs/plan-studio-parametric.md §3.2, stage P1, decision 520): parameters added, changed, renamed
/// everywhere, removed while nothing names them and moved, one undo step each; derived parameters (a formula of the others, worked
/// out on every ask), cycles refused with their path; type parameters (one value for every placed part); min and max; the values a
/// placed part is built with and where each comes from; the preview in the world for a placed part, and why some values do not build.
/// </summary>
public sealed class PartParameterTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(params JsonNode?[] v) => new(v);

    static double Volume(Fixture f, string id) => f.Session.Quantities(id).SelectMany(q => q.Quantities).Single(q => q.Name == "NetVolume").Value;
    static string Part(Fixture f, JsonObject def) => (string)f.Run("studio.createPart", new JsonObject { ["name"] = "Box", ["def"] = def })!["id"]!;
    static string Place(Fixture f, string part, JsonObject? values = null, double x = 0, double y = 0) =>
        (string)f.Run("studio.placePart", new JsonObject { ["typeId"] = part, ["storeyId"] = f.Storey, ["at"] = J(x, y), ["values"] = values })!["id"]!;
    static JsonObject Def(Fixture f, string part) => f.Model.Get(part)!.Attrs["def"]!.AsObject();
    static List<string> Keys(Fixture f, string part) => [.. Def(f, part)["params"]!.AsArray().Select(p => (string)p!["key"]!)];
    static JsonObject Param(Fixture f, string part, string key) => Def(f, part)["params"]!.AsArray().Single(p => (string)p!["key"]! == key)!.AsObject();
    static JsonObject? Own(Fixture f, string id) => f.Model.Get(id)!.Attrs["values"] as JsonObject;
    static JsonNode? Update(Fixture f, string part, string key, JsonObject changes) =>
        f.Run("studio.updatePartParam", new JsonObject { ["partId"] = part, ["key"] = key, ["changes"] = changes });

    // W × D, D derived (W / 2), pushed by the type parameter T: W·W/2·T.
    static JsonObject Box() => new()
    {
        ["params"] = J(
            new JsonObject { ["key"] = "W", ["label"] = "Width", ["default"] = 1 },
            new JsonObject { ["key"] = "D", ["formula"] = "W / 2" },
            new JsonObject { ["key"] = "T", ["default"] = 0.2, ["instance"] = false }),
        ["bodies"] = J(new JsonObject { ["name"] = "Top", ["plane"] = "xy", ["points"] = J(J(0, 0), J("W", 0), J("W", "D"), J(0, "D")), ["depth"] = "T" }),
    };

    [Fact]
    public void Parameters_are_added_changed_removed_and_moved_one_undo_step_each()
    {
        var f = F();
        var part = Part(f, Box());
        var a = Place(f, part);

        // Added: its key a name formulas can use, unique whatever its case.
        var key = (string)f.Run("studio.addPartParam", new JsonObject { ["partId"] = a, ["param"] = new JsonObject { ["key"] = "Gap", ["kind"] = "length", ["default"] = 0.05, ["group"] = "Sizes" } })!["key"]!;
        Assert.Equal("Gap", key);
        Assert.Equal(["W", "D", "T", "Gap"], Keys(f, part));
        Assert.Equal("Sizes", (string)Param(f, part, "Gap")["group"]!);
        Assert.Throws<CommandException>(() => f.Run("studio.addPartParam", new JsonObject { ["partId"] = part, ["param"] = new JsonObject { ["key"] = "gap", ["default"] = 1 } }));
        Assert.Throws<CommandException>(() => f.Run("studio.addPartParam", new JsonObject { ["partId"] = part, ["param"] = new JsonObject { ["key"] = "2x", ["default"] = 1 } }));
        Assert.Throws<CommandException>(() => f.Run("studio.addPartParam", new JsonObject { ["partId"] = part, ["param"] = new JsonObject { ["key"] = "pi", ["default"] = 1 } }));
        Assert.Throws<CommandException>(() => f.Run("studio.addPartParam", new JsonObject { ["partId"] = part, ["param"] = new JsonObject { ["key"] = "Q", ["kind"] = "colour", ["default"] = 1 } }));
        // A text and a whole number, with no default: an empty text and nothing.
        f.Run("studio.addPartParam", new JsonObject { ["partId"] = part, ["param"] = new JsonObject { ["key"] = "Mark", ["kind"] = "text" } });
        f.Run("studio.addPartParam", new JsonObject { ["partId"] = part, ["param"] = new JsonObject { ["key"] = "N", ["kind"] = "integer", ["default"] = 3 } });
        Assert.Equal("", (string)Param(f, part, "Mark")["default"]!);
        Assert.Throws<CommandException>(() => f.Run("studio.addPartParam", new JsonObject { ["partId"] = part, ["param"] = new JsonObject { ["key"] = "M", ["kind"] = "integer", ["default"] = 2.5 } }));

        // Changed: label, unit, description, published; group cleared by null.
        Update(f, part, "Gap", new JsonObject { ["label"] = "Joint", ["unit"] = "mm", ["description"] = "Between panels", ["published"] = false, ["group"] = null });
        var gap = Param(f, part, "Gap");
        Assert.Equal("Joint", (string)gap["label"]!);
        Assert.Equal("mm", (string)gap["unit"]!);
        Assert.False((bool)gap["published"]!);
        Assert.Null(gap["group"]);
        f.Session.Undo();
        Assert.Equal("Sizes", (string)Param(f, part, "Gap")["group"]!);

        // Moved in the table.
        f.Run("studio.movePartParam", new JsonObject { ["partId"] = part, ["key"] = "Gap", ["to"] = 0 });
        Assert.Equal(["Gap", "W", "D", "T", "Mark", "N"], Keys(f, part));
        f.Session.Undo();
        Assert.Equal(["W", "D", "T", "Gap", "Mark", "N"], Keys(f, part));

        // Removed, the placed part's own value for it dropped.
        f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(a), ["values"] = new JsonObject { ["Gap"] = 0.1, ["Mark"] = "A1" } });
        Assert.Equal("A1", (string)Own(f, a)!["Mark"]!);
        var r = f.Run("studio.removePartParam", new JsonObject { ["partId"] = part, ["key"] = "Gap" })!.AsObject();
        Assert.Equal(1, (int)r["droppedValues"]!);
        Assert.DoesNotContain("Gap", Keys(f, part));
        Assert.False(Own(f, a)!.ContainsKey("Gap"));
        f.Session.Undo();
        Assert.Contains("Gap", Keys(f, part));
        Assert.Equal(0.1, (double)Own(f, a)!["Gap"]!, 9);
        Assert.Equal(0.1, Volume(f, a), 9);
    }

    [Fact]
    public void A_renamed_parameter_is_renamed_in_every_formula_and_every_placed_part_s_values()
    {
        var f = F();
        var def = Box();
        def["params"]!.AsArray().Add((JsonNode)new JsonObject { ["key"] = "E", ["formula"] = "w * 2 + W2" });
        def["params"]!.AsArray().Add((JsonNode)new JsonObject { ["key"] = "W2", ["default"] = 0.1 });
        var top = def["bodies"]![0]!.AsObject();
        top["when"] = "W > 0.5 and W2 > 0";
        top["offset"] = J(0, 0, "W - 1");
        def["sketches"] = J(new JsonObject
        {
            ["id"] = "base", ["name"] = "Base", ["plane"] = "xy", ["offset"] = J(0, 0, "W * 3"),
            ["shapes"] = J(new JsonObject { ["id"] = "ln", ["type"] = "line", ["a"] = J(0, 0), ["b"] = J(1, 0) }),
            ["constraints"] = J(
                new JsonObject { ["kind"] = "distanceX", ["refs"] = J(new JsonObject { ["shape"] = "ln", ["part"] = "start" }), ["value"] = 0 },
                new JsonObject { ["kind"] = "distanceY", ["refs"] = J(new JsonObject { ["shape"] = "ln", ["part"] = "start" }), ["value"] = 0 },
                new JsonObject { ["kind"] = "distance", ["refs"] = J(new JsonObject { ["shape"] = "ln", ["part"] = "whole" }), ["formula"] = "W + W2" }),
        });
        var part = Part(f, def);
        var a = Place(f, part, new JsonObject { ["W"] = 2 });

        var r = Update(f, part, "W", new JsonObject { ["newKey"] = "Width" })!.AsObject();
        Assert.Equal("Width", (string)r["key"]!);
        var d = Def(f, part);
        Assert.Equal(["Width", "D", "T", "E", "W2"], Keys(f, part));
        Assert.Equal("Width / 2", (string)Param(f, part, "D")["formula"]!);
        Assert.Equal("Width * 2 + W2", (string)Param(f, part, "E")["formula"]!);
        var body = d["bodies"]![0]!.AsObject();
        Assert.Equal("Width > 0.5 and W2 > 0", (string)body["when"]!);
        Assert.Equal("Width - 1", (string)body["offset"]![2]!);
        Assert.Equal("Width", (string)body["points"]![1]![0]!);
        var sk = d["sketches"]![0]!.AsObject();
        Assert.Equal("Width * 3", (string)sk["offset"]![2]!);
        Assert.Equal("Width + W2", (string)sk["constraints"]!.AsArray().Single(c => (string)c!["kind"]! == "distance")!["formula"]!);
        Assert.Equal(2, (double)Own(f, a)!["Width"]!, 9);
        Assert.False(Own(f, a)!.ContainsKey("W"));
        Assert.Equal(2 * 1 * 0.2, Volume(f, a), 9);
        // Onto another parameter's name, whatever its case: refused.
        Assert.Throws<CommandException>(() => Update(f, part, "Width", new JsonObject { ["newKey"] = "w2" }));

        f.Session.Undo();
        Assert.Equal("W", Keys(f, part)[0]);
        Assert.Equal("W / 2", (string)Param(f, part, "D")["formula"]!);
        Assert.Equal(2, (double)Own(f, a)!["W"]!, 9);
    }

    [Fact]
    public void A_derived_parameter_drives_a_body_and_takes_no_value_of_its_own()
    {
        var f = F();
        var part = Part(f, Box());
        var a = Place(f, part);
        var b = Place(f, part, new JsonObject { ["W"] = 2 }, x: 5);
        Assert.Equal(1 * 0.5 * 0.2, Volume(f, a), 9);
        Assert.Equal(2 * 1 * 0.2, Volume(f, b), 9);
        Assert.False(Param(f, part, "D").ContainsKey("default"));

        var ex = Assert.Throws<CommandException>(() => f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(b), ["values"] = new JsonObject { ["D"] = 3 } }));
        Assert.Contains("formula", ex.Message);
        Assert.Throws<CommandException>(() => Place(f, part, new JsonObject { ["D"] = 3 }));

        // The tree shows it derived, its value worked out.
        var ps = f.Session.Query("studio.partTree", new JsonObject { ["id"] = b })!["params"]!.AsArray();
        var dp = ps.Single(p => (string)p!["key"]! == "D")!;
        Assert.True((bool)dp["derived"]!);
        Assert.Equal("W / 2", (string)dp["formula"]!);
        Assert.Equal(1, (double)dp["value"]!, 9);
        Assert.False((bool)ps.Single(p => (string)p!["key"]! == "W")!["derived"]!);

        // A formula given, then cleared: the parameter takes its worked-out default as a value again.
        Update(f, part, "D", new JsonObject { ["formula"] = "W / 4" });
        Assert.Equal(2 * 0.5 * 0.2, Volume(f, b), 9);
        Update(f, part, "D", new JsonObject { ["formula"] = null });
        Assert.Equal(0.25, (double)Param(f, part, "D")["default"]!, 9);
        Assert.Equal(2 * 0.25 * 0.2, Volume(f, b), 9);
        // A formula naming something unknown is refused.
        Assert.Throws<CommandException>(() => Update(f, part, "D", new JsonObject { ["formula"] = "Q * 2" }));
        Assert.Throws<CommandException>(() => Update(f, part, "D", new JsonObject { ["formula"] = "W *" }));
    }

    [Fact]
    public void A_cycle_of_formulas_is_refused_with_its_path()
    {
        var f = F();
        var part = Part(f, Box());
        f.Run("studio.addPartParam", new JsonObject { ["partId"] = part, ["param"] = new JsonObject { ["key"] = "E", ["formula"] = "D + 1" } });
        var ex = Assert.Throws<CommandException>(() => Update(f, part, "D", new JsonObject { ["formula"] = "E * 2" }));
        Assert.Contains("D → E → D", ex.Message);
        var self = Assert.Throws<CommandException>(() => Update(f, part, "D", new JsonObject { ["formula"] = "D + 1" }));
        Assert.Contains("D → D", self.Message);
        Assert.Equal("W / 2", (string)Param(f, part, "D")["formula"]!);
        // In a whole definition too.
        var def = Box();
        def["params"]![0]!["formula"] = "D * 2";
        Assert.Contains("W → D → W", Assert.Throws<CommandException>(() => Part(f, def)).Message);
    }

    [Fact]
    public void A_type_parameter_is_one_value_for_every_placed_part()
    {
        var f = F();
        var part = Part(f, Box());
        var a = Place(f, part);
        var b = Place(f, part, new JsonObject { ["W"] = 2 }, x: 5);
        var ex = Assert.Throws<CommandException>(() => f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(a), ["values"] = new JsonObject { ["T"] = 0.5 } }));
        Assert.Contains("type parameter", ex.Message);

        // Changed on the part: every placed part follows.
        Update(f, part, "T", new JsonObject { ["default"] = 0.4 });
        Assert.Equal(1 * 0.5 * 0.4, Volume(f, a), 9);
        Assert.Equal(2 * 1 * 0.4, Volume(f, b), 9);
        f.Session.Undo();
        Assert.Equal(2 * 1 * 0.2, Volume(f, b), 9);

        // Made a type parameter: the placed parts' own values for it dropped, and told how many.
        var c = Place(f, part, new JsonObject { ["W"] = 3 }, x: 10);
        var r = Update(f, part, "W", new JsonObject { ["instance"] = false })!.AsObject();
        Assert.Equal(2, (int)r["droppedValues"]!);
        Assert.Null(Own(f, b));
        Assert.Equal(1 * 0.5 * 0.2, Volume(f, b), 9);
        Assert.Equal(1 * 0.5 * 0.2, Volume(f, c), 9);
        f.Session.Undo();
        Assert.Equal(3, (double)Own(f, c)!["W"]!, 9);
        Assert.Equal(3 * 1.5 * 0.2, Volume(f, c), 9);
    }

    [Fact]
    public void Min_and_max_hold_defaults_and_values()
    {
        var f = F();
        var part = Part(f, Box());
        var a = Place(f, part, new JsonObject { ["W"] = 2 });
        Update(f, part, "W", new JsonObject { ["min"] = 0.5, ["max"] = 3 });
        Assert.Equal(0.5, (double)Param(f, part, "W")["min"]!, 9);
        Assert.Throws<CommandException>(() => f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(a), ["values"] = new JsonObject { ["W"] = 5 } }));
        Assert.Throws<CommandException>(() => Place(f, part, new JsonObject { ["W"] = 0.1 }));
        Assert.Throws<CommandException>(() => Update(f, part, "W", new JsonObject { ["default"] = 4 }));
        Assert.Throws<CommandException>(() => Update(f, part, "W", new JsonObject { ["min"] = 2, ["max"] = 1 }));
        // A maximum a placed part's value would break: refused, naming it.
        var ex = Assert.Throws<CommandException>(() => Update(f, part, "W", new JsonObject { ["max"] = 1.5 }));
        Assert.Contains("„Box”", ex.Message);
        Assert.Equal(3, (double)Param(f, part, "W")["max"]!, 9);
        // Cleared by null.
        Update(f, part, "W", new JsonObject { ["max"] = null });
        Assert.False(Param(f, part, "W").ContainsKey("max"));
        f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(a), ["values"] = new JsonObject { ["W"] = 5 } });
    }

    [Fact]
    public void A_parameter_is_not_removed_while_something_names_it()
    {
        var f = F();
        var part = Part(f, Box());
        var ex = Assert.Throws<CommandException>(() => f.Run("studio.removePartParam", new JsonObject { ["partId"] = part, ["key"] = "W" }));
        Assert.Contains("the formula of D", ex.Message);
        Assert.Contains("the corners of Top", ex.Message);
        var t = Assert.Throws<CommandException>(() => f.Run("studio.removePartParam", new JsonObject { ["partId"] = part, ["key"] = "T" }));
        Assert.Contains("the depth of Top", t.Message);
        Assert.Throws<CommandException>(() => f.Run("studio.removePartParam", new JsonObject { ["partId"] = part, ["key"] = "Nope" }));
        Assert.Equal(["W", "D", "T"], Keys(f, part));
    }

    [Fact]
    public void A_placed_part_s_values_tell_where_each_comes_from()
    {
        var f = F();
        var part = Part(f, Box());
        f.Run("studio.addPartParam", new JsonObject { ["partId"] = part, ["param"] = new JsonObject { ["key"] = "Mark", ["kind"] = "text", ["default"] = "P" } });
        var a = Place(f, part, new JsonObject { ["W"] = 2 });
        var vs = f.Session.Query("studio.partValues", new JsonObject { ["id"] = a })!["values"]!.AsArray().ToDictionary(v => (string)v!["key"]!, v => v!.AsObject());
        Assert.Equal("instance", (string)vs["W"]["source"]!);
        Assert.Equal(2, (double)vs["W"]["value"]!, 9);
        Assert.Equal("formula", (string)vs["D"]["source"]!);
        Assert.Equal(1, (double)vs["D"]["value"]!, 9);
        Assert.Equal("type", (string)vs["T"]["source"]!);
        Assert.Equal("default", (string)vs["Mark"]["source"]!);
        Assert.Equal("P", (string)vs["Mark"]["value"]!);
        Assert.Throws<CommandException>(() => f.Session.Query("studio.partValues", new JsonObject { ["id"] = part }));
    }

    [Fact]
    public void A_preview_of_a_placed_part_is_in_the_world_and_tells_values_that_do_not_build()
    {
        var f = F();
        var part = Part(f, Box());
        Update(f, part, "W", new JsonObject { ["min"] = 0.5 });
        var a = Place(f, part, new JsonObject { ["W"] = 2 }, x: 10, y: 5);

        var p = f.Session.Query("studio.partPreview", new JsonObject { ["id"] = a, ["values"] = new JsonObject { ["T"] = 0.5 } })!.AsObject();
        Assert.True((bool)p["ok"]!);
        var body = p["bodies"]![0]!.AsObject();
        Assert.Equal(2 * 1 * 0.5, (double)body["volume"]!, 9);
        var edges = body["edges"]!.AsArray().Select(e => e!.AsArray().Select(x => (double)x!).ToArray()).ToList();
        Assert.Equal(10, edges.Min(e => Math.Min(e[0], e[3])), 6);
        Assert.Equal(12, edges.Max(e => Math.Max(e[0], e[3])), 6);
        Assert.Equal(6, edges.Max(e => Math.Max(e[1], e[4])), 6);
        // The part itself: its own frame, its defaults under the values.
        var q = f.Session.Query("studio.partPreview", new JsonObject { ["id"] = part, ["values"] = new JsonObject { ["W"] = 3 } })!.AsObject();
        Assert.Equal(3, q["bodies"]![0]!["edges"]!.AsArray().Max(e => (double)e![3]!), 6);

        // Values that do not build: not thrown, told.
        var bad = f.Session.Query("studio.partPreview", new JsonObject { ["id"] = a, ["values"] = new JsonObject { ["W"] = 0.1 } })!.AsObject();
        Assert.False((bool)bad["ok"]!);
        Assert.Contains("W", (string)bad["reason"]!);
        // The families' preview keeps throwing.
        Assert.Throws<CommandException>(() => f.Session.Query("studio.familyPreview", new JsonObject { ["id"] = part, ["values"] = new JsonObject { ["W"] = 0.1 } }));
    }

    [Fact]
    public void A_preview_solves_a_part_s_own_sketch_with_the_values_over_the_placed_part_s()
    {
        var f = F();
        static JsonObject Dim(string kind, string part, JsonNode v)
        {
            var o = new JsonObject { ["kind"] = kind, ["refs"] = J(new JsonObject { ["shape"] = "sq", ["part"] = part }) };
            if (v is JsonValue jv && jv.TryGetValue<string>(out var s)) o["formula"] = s; else o["value"] = v.DeepClone();
            return o;
        }
        var def = new JsonObject
        {
            ["params"] = J(new JsonObject { ["key"] = "W", ["default"] = 1 }, new JsonObject { ["key"] = "H", ["default"] = 1 }, new JsonObject { ["key"] = "D", ["default"] = 0.2 }),
            ["sketches"] = J(new JsonObject
            {
                ["id"] = "base", ["name"] = "Base", ["plane"] = "xy",
                ["shapes"] = J(new JsonObject { ["id"] = "sq", ["type"] = "polyline", ["pts"] = J(J(0, 0), J(1, 0), J(1, 1), J(0, 1)), ["closed"] = true }),
                ["constraints"] = J(
                    Dim("distanceX", "vertex:0", 0), Dim("distanceY", "vertex:0", 0), Dim("distanceX", "vertex:1", "W"), Dim("distanceY", "vertex:1", 0),
                    Dim("distanceX", "vertex:2", "W"), Dim("distanceY", "vertex:2", "H"), Dim("distanceX", "vertex:3", 0), Dim("distanceY", "vertex:3", "H")),
            }),
            ["bodies"] = J(new JsonObject { ["name"] = "Slab", ["sketch"] = "base", ["depth"] = "D" }),
        };
        var part = Part(f, def);
        var a = Place(f, part, new JsonObject { ["W"] = 2 }, x: 1);
        var p = f.Session.Query("studio.partPreview", new JsonObject { ["id"] = a, ["values"] = new JsonObject { ["H"] = 3 } })!.AsObject();
        Assert.True((bool)p["ok"]!);
        Assert.Equal(2 * 3 * 0.2, (double)p["bodies"]![0]!["volume"]!, 9);
        Assert.Equal(3, p["bodies"]![0]!["edges"]!.AsArray().Max(e => (double)e![3]!), 6);
    }
}
