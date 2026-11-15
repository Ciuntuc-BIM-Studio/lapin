using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Planning;

namespace Lapin.Engine.Tests;

/// <summary>
/// Audit 15.11.2026, P2: a link between a summary task and a task under it (refused by <c>planning.link</c>) left behind by an
/// indent or an outdent — a task and a subtask of its new parent — used to throw out of the critical path and take every
/// planning query and the IFC export with it. The move takes such links away and says so; the critical path leaves any
/// such link out (a file may hold one); any other failure comes back as the schedule's problem, never as an engine error.
/// </summary>
public class PlanningNestedLinkTests
{
    static Fixture F() => new(new PhasesModule(), new PlanningModule());

    static string Task(Fixture f, string name, int duration, string? parent = null)
    {
        var a = new JsonObject { ["name"] = name, ["duration"] = duration };
        if (parent != null) a["parent"] = parent;
        return (string)f.Run("planning.addTask", a)!["id"]!;
    }

    static string Link(Fixture f, string from, string to) =>
        (string)f.Run("planning.link", new JsonObject { ["from"] = from, ["to"] = to })!["id"]!;

    static JsonObject Schedule(Fixture f) => (JsonObject)f.Session.Query("planning.schedule", new JsonObject())!;

    [Fact]
    public void Indenting_a_task_under_one_linked_to_its_subtask_takes_the_link_away_and_reports_it_in_one_undo_step()
    {
        var f = F();
        var a = Task(f, "A", 2);
        var b = Task(f, "B", 2);
        var b1 = Task(f, "B1", 3, b);
        var l = Link(f, a, b1);
        // B goes under A: A would be a summary linked to its own grandchild B1.
        var r = f.Run("planning.indent", new JsonObject { ["ids"] = new JsonArray(b) });
        Assert.Equal([l], r!["removedLinks"]!.AsArray().Select(x => (string)x!));
        var s = Schedule(f);
        Assert.Null((string?)s["problem"]);
        Assert.Empty(s["links"]!.AsArray());
        Assert.Equal(3, s["tasks"]!.AsArray().Count);
        // One undo step: the task back at its level and the link with it.
        f.Session.Undo();
        s = Schedule(f);
        Assert.Single(s["links"]!.AsArray());
        Assert.Null((string?)s["tasks"]!.AsArray().First(t => (string)t!["id"]! == b)!["parent"]);
    }

    [Fact]
    public void Outdenting_a_task_linked_to_a_subtask_of_a_later_sibling_takes_the_link_away()
    {
        var f = F();
        var p = Task(f, "P", 1);
        var x = Task(f, "X", 2, p);
        var y = Task(f, "Y", 1, p);
        var y1 = Task(f, "Y1", 2, y);
        var keep = Task(f, "Z", 1);
        var l = Link(f, x, y1);
        var other = Link(f, x, keep);
        // X goes up a level and takes Y (after it under P) along: X would be a summary linked to its own grandchild Y1.
        var r = f.Run("planning.outdent", new JsonObject { ["ids"] = new JsonArray(x) });
        Assert.Equal([l], r!["removedLinks"]!.AsArray().Select(v => (string)v!));
        var s = Schedule(f);
        Assert.Null((string?)s["problem"]);
        Assert.Equal([other], s["links"]!.AsArray().Select(v => (string)v!["id"]!));
    }

    [Fact]
    public void A_move_with_no_link_to_drop_reports_nothing()
    {
        var f = F();
        Task(f, "A", 1);
        var b = Task(f, "B", 1);
        Assert.Null(f.Run("planning.indent", new JsonObject { ["ids"] = new JsonArray(b) }));
    }

    [Fact]
    public void The_critical_path_leaves_out_a_link_between_a_summary_and_a_task_under_it_and_the_IFC_export_goes_on()
    {
        var f = F();
        var a = Task(f, "A", 2);
        var a1 = Task(f, "A1", 3, a);
        var a2 = Task(f, "A2", 4, a);
        // Such a link cannot be made by a command: a file or an older project may hold one.
        var p = Plan.Read(f.Model);
        var nested = p.WithLinks([.. p.Links, new PlanLink("nested", a, a2, "FS", 0)]);
        var r = Cpm.Solve(nested);
        Assert.Equal(r.Times[a1].EarlyStart, r.Times[a2].EarlyStart);
        Assert.Null(PlanningModule.Solve(nested).Problem);
        Assert.False(Cpm.HasLoop(nested));
        // Held by the model: the schedule and the IFC export go through.
        f.Session.Edit("Old file", edit => edit.AddRelation(new Relation("nested", Plan.SequenceClass, a, [a2], new JsonObject { ["type"] = "FS", ["lag"] = 0 })));
        Assert.Null((string?)Schedule(f)["problem"]);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "plan.ifc");
        Assert.True(ms.Length > 0);
    }
}
