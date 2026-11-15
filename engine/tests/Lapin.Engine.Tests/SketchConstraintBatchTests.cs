using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Audit 15.11.2026, M5: in an edit.batch (and Python's <c>lapin.batch()</c>) every step shares the batch's command context,
/// so a step's result told again what an earlier step had dropped, and what the follow-up after the batch carried or dropped
/// was lost (the batch's result is a list). Each step tells only its own now, and what follows the batch is told on its last
/// step's result.
/// </summary>
public sealed class SketchConstraintBatchTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];

    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;

    static string Line(Fixture f, string view, double x1, double y1, double x2, double y2) =>
        (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(x1, y1), ["b"] = J(x2, y2) } })!["ids"]![0]!;

    static JsonObject R(string shape, string part = "whole") => new() { ["shape"] = shape, ["part"] = part };

    static JsonObject Constraint(string kind, params JsonObject[] refs) => new() { ["kind"] = kind, ["refs"] = new JsonArray(refs.Select(r => (JsonNode)r).ToArray()) };

    static string Add(Fixture f, string kind, params JsonObject[] refs) => (string)f.Run("studio.addConstraint", Constraint(kind, refs))!["id"]!;

    static JsonObject Step(string command, JsonObject args) => new() { ["command"] = command, ["args"] = args };

    static string[] DroppedIds(JsonNode? result) => result?["dropped"]?.AsArray().Select(d => (string)d!["id"]!).ToArray() ?? [];

    [Fact]
    public void Each_step_of_a_batch_tells_only_what_it_dropped()
    {
        var f = F();
        var plan = Plan(f);
        var a = Line(f, plan, 0, 0, 1, 0);
        var b = Line(f, plan, 1, 0, 1, 1);
        var c = Line(f, plan, 3, 0, 3, 2);
        var d = Line(f, plan, 5, 0, 6, 0);
        var meets = Add(f, "perpendicular", R(a), R(b));
        var results = f.Run("edit.batch", new JsonObject
        {
            ["steps"] = new JsonArray(
                Step("element.delete", new JsonObject { ["ids"] = new JsonArray(b) }),
                Step("studio.addConstraint", Constraint("vertical", R(c))),
                Step("studio.addConstraint", Constraint("horizontal", R(d)))),
        })!.AsArray();
        Assert.Equal(3, results.Count);
        // The constraint on the deleted line is told once, by the step that dropped it, not again by the next one.
        var told = results.SelectMany(DroppedIds).ToList();
        Assert.Equal([meets], told);
        Assert.Empty(DroppedIds(results[2]));
    }

    [Fact]
    public void What_the_follow_up_after_a_batch_drops_is_told_on_its_last_step()
    {
        var f = F();
        var plan = Plan(f);
        var a = Line(f, plan, 0, 0, 1, 0);
        var b = Line(f, plan, 1, 0, 1, 1);
        var meets = Add(f, "perpendicular", R(a), R(b));
        // The delete's constraints are dropped by the follow-up after the batch: told on the batch's last step, a list kept.
        var results = f.Run("edit.batch", new JsonObject
        {
            ["steps"] = new JsonArray(Step("element.delete", new JsonObject { ["ids"] = new JsonArray(b) })),
        })!.AsArray();
        Assert.Single(results);
        Assert.Equal([meets], DroppedIds(results[^1]));
    }
}
