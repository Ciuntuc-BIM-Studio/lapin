using System.Text.Json.Nodes;
using Lapin.Analysis;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The load cases (docs/plan-fem.md §8.2): the defaults made once and in one undo step, a code unique in the project, one case
/// with the self weight, a derived kind in one case (the rooms' in one a category), a case not removed while a load is in it.
/// </summary>
public class LoadCaseTests
{
    internal static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new AnalysisModule());

    internal static JsonObject Cases(Fixture f) => f.Session.Query("analysis.loads", [])!.AsObject();

    internal static string IdOf(Fixture f, string code) =>
        (string)Cases(f)["cases"]!.AsArray().Single(c => (string?)c!["code"] == code)!["id"]!;

    static JsonArray Square(double x, double y, double s) => new(new JsonArray(x, y), new JsonArray(x + s, y), new JsonArray(x + s, y + s), new JsonArray(x, y + s));

    [Fact]
    public void The_defaults_are_made_once_in_one_undo_step()
    {
        var f = F();
        var made = f.Run("analysis.defaultCases", [])!["ids"]!.AsArray();
        Assert.Equal(5, made.Count);
        var cases = Cases(f)["cases"]!.AsArray().Select(c => c!.AsObject()).ToList();
        Assert.Equal(["G1", "G2", "Q-A", "S", "W"], cases.Select(c => (string)c["code"]!));
        Assert.Equal(["Self weight", "Finishes and partitions", "Imposed, category A", "Snow", "Wind"], cases.Select(c => (string)c["name"]!));
        Assert.Equal(["PERMANENT_G", "PERMANENT_G", "VARIABLE_Q", "VARIABLE_Q", "VARIABLE_Q"], cases.Select(c => (string)c["actionType"]!));
        Assert.Equal(["DEAD_LOAD_G", "COMPLETION_G1", "LIVE_LOAD_Q", "SNOW_S", "WIND_W"], cases.Select(c => (string)c["actionSource"]!));
        Assert.True((bool)cases[0]["selfWeight"]!);
        Assert.Equal(["finishes", "partitions"], cases[1]["derives"]!.AsArray().Select(x => (string)x!));
        Assert.Equal("A", (string?)cases[2]["category"]);
        Assert.Equal(["occupancy"], cases[2]["derives"]!.AsArray().Select(x => (string)x!));
        Assert.Equal("snow", (string?)cases[3]["category"]);
        Assert.Equal("wind", (string?)cases[4]["category"]);
        // ψ used: A's 0.7 / 0.5 / 0.3, snow's 0.5 / 0.2 / 0; none for a permanent case.
        Assert.Equal(0.3, (double)cases[2]["psi"]!["2"]!);
        Assert.Equal(0.2, (double)cases[3]["psi"]!["1"]!);
        Assert.Null(cases[0]["psi"]);
        // Every case is empty: nothing is modelled yet.
        Assert.All(cases, c => Assert.True((bool)c["empty"]!));

        // One undo step takes all five.
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(LoadCases.Class));
        f.Session.Redo();
        // A project with cases gets no more.
        Assert.Empty(f.Run("analysis.defaultCases", [])!["ids"]!.AsArray());
        Assert.Equal(5, f.Model.OfClass(LoadCases.Class).Count());
    }

    [Fact]
    public void A_load_without_a_case_goes_in_Q_A_made_with_the_defaults_in_the_same_step()
    {
        var f = F();
        var id = (string)f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Square(0, 0, 2), ["value"] = 1.5 })!["id"]!;
        Assert.Equal(5, f.Model.OfClass(LoadCases.Class).Count());
        Assert.Equal(IdOf(f, "Q-A"), f.Model.Get(id)!.Str("caseId"));
        Assert.Equal("down", f.Model.Get(id)!.Str("direction"));
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, id)!.Id);
        var qa = Cases(f)["cases"]!.AsArray().Single(c => (string?)c!["code"] == "Q-A")!;
        Assert.Equal(1, (int)qa["userLoads"]!);
        Assert.Equal(6.0, (double)qa["total"]!, 9);             // 1.5 kN/m² × 4 m²
        Assert.False((bool)qa["empty"]!);
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(LoadCases.Class));
        Assert.Empty(f.Model.OfClass(UserLoads.AreaClass));

        // A project whose cases have no Q-A gets only Q-A.
        var g = F();
        var own = (string)g.Run("analysis.addCase", new JsonObject { ["name"] = "Dead", ["actionType"] = "PERMANENT_G", ["selfWeight"] = true })!["id"]!;
        Assert.Equal("G1", g.Model.Get(own)!.Str("code"));
        g.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = g.Storey, ["at"] = new JsonArray(1, 1), ["value"] = 10 });
        Assert.Equal(["G1", "Q-A"], Cases(g)["cases"]!.AsArray().Select(c => (string)c!["code"]!));
    }

    [Fact]
    public void A_code_is_unique_and_a_new_case_gets_one_of_its_own()
    {
        var f = F();
        f.Run("analysis.defaultCases", []);
        Assert.Throws<CommandException>(() => f.Run("analysis.addCase", new JsonObject { ["name"] = "Other", ["code"] = "g1", ["actionType"] = "PERMANENT_G" }));
        var g3 = (string)f.Run("analysis.addCase", new JsonObject { ["name"] = "Facade", ["actionType"] = "PERMANENT_G" })!["id"]!;
        Assert.Equal("G3", f.Model.Get(g3)!.Str("code"));
        var b = (string)f.Run("analysis.addCase", new JsonObject { ["name"] = "Offices", ["actionType"] = "VARIABLE_Q", ["category"] = "B", ["derives"] = new JsonArray("occupancy") })!["id"]!;
        Assert.Equal("Q-B", f.Model.Get(b)!.Str("code"));
        Assert.Equal("LIVE_LOAD_Q", f.Model.Get(b)!.Str("actionSource"));
        // Renaming onto another's code is refused, by the command and by the inspector.
        Assert.Throws<CommandException>(() => f.Run("analysis.updateCase", new JsonObject { ["id"] = g3, ["code"] = "Q-A" }));
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = g3, ["attrs"] = new JsonObject { ["code"] = "S" } }));
        f.Run("analysis.updateCase", new JsonObject { ["id"] = g3, ["code"] = "G-F", ["name"] = "Facade panels" });
        Assert.Equal("G-F", f.Model.Get(g3)!.Str("code"));
        Assert.Equal("Facade panels", f.Model.Get(g3)!.Name);
        // A category is a variable case's, and one of the table's.
        Assert.Throws<CommandException>(() => f.Run("analysis.addCase", new JsonObject { ["name"] = "X", ["actionType"] = "PERMANENT_G", ["category"] = "A" }));
        Assert.Throws<CommandException>(() => f.Run("analysis.addCase", new JsonObject { ["name"] = "X", ["actionType"] = "VARIABLE_Q", ["category"] = "Z9" }));
        Assert.Throws<CommandException>(() => f.Run("analysis.addCase", new JsonObject { ["name"] = "X", ["actionType"] = "LIVE" }));
        Assert.Throws<CommandException>(() => f.Run("analysis.updateCase", new JsonObject { ["id"] = b, ["psi"] = new JsonObject { ["0"] = 1.2, ["1"] = 0.5, ["2"] = 0.3 } }));
        f.Run("analysis.updateCase", new JsonObject { ["id"] = b, ["psi"] = new JsonObject { ["0"] = 0.7, ["1"] = 0.6, ["2"] = 0.4 } });
        Assert.Equal(0.6, (double)Cases(f)["cases"]!.AsArray().Single(c => (string?)c!["code"] == "Q-B")!["psi"]!["1"]!);
    }

    [Fact]
    public void One_case_carries_the_self_weight_and_a_derived_kind_goes_in_one_case()
    {
        var f = F();
        f.Run("analysis.defaultCases", []);
        Assert.Throws<CommandException>(() => f.Run("analysis.addCase", new JsonObject { ["name"] = "More", ["actionType"] = "PERMANENT_G", ["selfWeight"] = true }));
        Assert.Throws<CommandException>(() => f.Run("analysis.updateCase", new JsonObject { ["id"] = IdOf(f, "G2"), ["selfWeight"] = true }));
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = IdOf(f, "G2"), ["attrs"] = new JsonObject { ["selfWeight"] = true } }));
        // The self weight is permanent.
        Assert.Throws<CommandException>(() => f.Run("analysis.updateCase", new JsonObject { ["id"] = IdOf(f, "S"), ["selfWeight"] = true }));
        // The finishes are G2's; the rooms of category A Q-A's; another category's rooms may have their own case.
        Assert.Throws<CommandException>(() => f.Run("analysis.addCase", new JsonObject { ["name"] = "Floors", ["actionType"] = "PERMANENT_G", ["derives"] = new JsonArray("finishes") }));
        Assert.Throws<CommandException>(() => f.Run("analysis.addCase", new JsonObject { ["name"] = "Flats", ["actionType"] = "VARIABLE_Q", ["category"] = "A", ["derives"] = new JsonArray("occupancy") }));
        Assert.Throws<CommandException>(() => f.Run("analysis.addCase", new JsonObject { ["name"] = "Rooms", ["actionType"] = "VARIABLE_Q", ["derives"] = new JsonArray("occupancy") }));
        f.Run("analysis.addCase", new JsonObject { ["name"] = "Offices", ["actionType"] = "VARIABLE_Q", ["category"] = "B", ["derives"] = new JsonArray("occupancy") });
        // Moved to another case, the self weight goes where it is taken off first.
        f.Run("analysis.updateCase", new JsonObject { ["id"] = IdOf(f, "G1"), ["selfWeight"] = false });
        f.Run("analysis.updateCase", new JsonObject { ["id"] = IdOf(f, "G2"), ["selfWeight"] = true });
        Assert.Equal(["G2"], Cases(f)["cases"]!.AsArray().Where(c => (bool)c!["selfWeight"]!).Select(c => (string)c!["code"]!));
    }

    [Fact]
    public void A_case_is_not_removed_while_a_load_is_in_it()
    {
        var f = F();
        f.Run("analysis.defaultCases", []);
        var s = IdOf(f, "S");
        var load = (string)f.Run("analysis.addLineLoad", new JsonObject { ["storeyId"] = f.Storey, ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(4, 0), ["value"] = 2, ["caseId"] = s })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("analysis.removeCase", new JsonObject { ["id"] = s }));
        Assert.Throws<CommandException>(() => f.Run("element.delete", new JsonObject { ["id"] = s }));
        // With its load, or after it, it goes.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(s, load) });
        Assert.Null(f.Model.Get(s));
        f.Session.Undo();
        f.Run("element.delete", new JsonObject { ["id"] = load });
        f.Run("analysis.removeCase", new JsonObject { ["id"] = s });
        Assert.Equal(["G1", "G2", "Q-A", "W"], Cases(f)["cases"]!.AsArray().Select(c => (string)c!["code"]!));
        Assert.Throws<CommandException>(() => f.Run("analysis.removeCase", new JsonObject { ["id"] = s }));
    }

    [Fact]
    public void A_load_is_checked_when_made_and_when_changed()
    {
        var f = F();
        Assert.Throws<CommandException>(() => f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Square(0, 0, 2), ["value"] = 0 }));
        Assert.Throws<CommandException>(() => f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(1, 0)), ["value"] = 1 }));
        Assert.Throws<CommandException>(() => f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = f.Storey, ["at"] = new JsonArray(0, 0), ["value"] = 5, ["direction"] = "sideways" }));
        Assert.Throws<CommandException>(() => f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = f.Storey, ["at"] = new JsonArray(0, 0), ["value"] = 5, ["caseId"] = "nope" }));
        var p = (string)f.Run("analysis.addPointLoad", new JsonObject { ["storeyId"] = f.Storey, ["at"] = new JsonArray(0, 0), ["value"] = 5, ["direction"] = "up", ["z"] = 1.2 })!["id"]!;
        Assert.Equal(1.2, f.Model.Get(p)!.Num("z"));
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = p, ["attrs"] = new JsonObject { ["value"] = 0 } }));
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = p, ["attrs"] = new JsonObject { ["caseId"] = f.Storey } }));
        f.Run("element.update", new JsonObject { ["id"] = p, ["attrs"] = new JsonObject { ["value"] = 7.5, ["caseId"] = IdOf(f, "G2") } });
        Assert.Equal(7.5, f.Model.Get(p)!.Num("value"));
        // An upward load counts against the case's vertical total.
        Assert.Equal(-7.5, (double)Cases(f)["cases"]!.AsArray().Single(c => (string?)c!["code"] == "G2")!["total"]!, 9);
    }

    [Fact]
    public void A_load_draws_in_its_storeys_plan_with_its_value_and_in_3D_as_lines()
    {
        var f = F();
        var area = (string)f.Run("analysis.addAreaLoad", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Square(0, 0, 3), ["value"] = 2.5, ["z"] = 0.5 })!["id"]!;
        var line = (string)f.Run("analysis.addLineLoad", new JsonObject { ["storeyId"] = f.Storey, ["a"] = new JsonArray(5, 0), ["b"] = new JsonArray(9, 0), ["value"] = 4 })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = plan })!;
        var ids = d["marks"]!.AsArray().OfType<JsonObject>().Select(m => (string?)m["id"]).ToList();
        Assert.Contains(area, ids);
        Assert.Contains(line, ids);
        Assert.Contains("2.5 kN/m² (Q-A)", d.ToJsonString(new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        // 3D: no body; the outline at its level (0.5 m up) and arrows.
        var g = f.Session.Geometry(area)!;
        Assert.Empty(g.Parts);
        Assert.Equal(0.5, g.Lines![0][0].Z, 9);
        Assert.True(g.Lines.Count > 2);
        // Moved with the others, its points follow.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(line), ["op"] = "move", ["by"] = new JsonArray(1, 2) });
        Assert.Equal([6.0, 2.0], f.Model.Get(line)!.Attrs["a"]!.AsArray().Select(x => (double)x!));
    }

    [Fact]
    public void The_missing_cases_are_made_one_a_category_of_the_rooms()
    {
        var f = F();
        string Room(double x, string function) => (string)f.Run("architecture.addRoom", new JsonObject
        {
            ["storeyId"] = f.Storey, ["x1"] = x, ["y1"] = 0, ["x2"] = x + 3, ["y2"] = 3, ["function"] = function,
        })!["id"]!;
        Room(0, "Bedroom");
        Room(10, "Office");
        Room(20, "Garage");
        var r = Cases(f);
        Assert.Empty(r["cases"]!.AsArray());
        Assert.Equal(["A", "B", "F"], r["missing"]!.AsArray().Select(x => (string)x!["category"]!));
        f.Run("analysis.addMissingCases", []);
        Assert.Equal(["G1", "G2", "Q-A", "S", "W", "Q-B", "Q-F"], Cases(f)["cases"]!.AsArray().Select(c => (string)c!["code"]!));
        Assert.Empty(Cases(f)["missing"]!.AsArray());
        var qf = Cases(f)["cases"]!.AsArray().Single(c => (string?)c!["code"] == "Q-F")!;
        Assert.Equal("F", (string?)qf["category"]);
        Assert.Equal(["occupancy"], qf["derives"]!.AsArray().Select(x => (string)x!));
        Assert.Empty(f.Run("analysis.addMissingCases", [])!["ids"]!.AsArray());
    }
}
