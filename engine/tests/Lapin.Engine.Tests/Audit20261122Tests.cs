using System.Text.Json.Nodes;
using Lapin.Architecture.Stairs;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The audit of 22.11.2026 (docs/audit-2026-11-22.md): its fixes that have no other test file of their own.</summary>
public class Audit20261122Tests
{
    [Theory]
    [InlineData("height", 5000.0)]
    [InlineData("height", 0.0)]
    [InlineData("elevation", 20000.0)]
    public void Element_update_keeps_a_storeys_bounds_as_storey_update_does(string key, double value)
    {
        // G16: element.update (scripts, MCP, the assistant) took a 5 km storey, and what was built to it froze the engine (#73).
        var f = new Fixture();
        var before = f.Model.Get(f.Storey)!.Num(key);
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = f.Storey, ["attrs"] = new JsonObject { [key] = value } }));
        Assert.Equal(before, f.Model.Get(f.Storey)!.Num(key));
    }

    [Fact]
    public void Element_update_still_changes_a_storey_within_its_bounds()
    {
        var f = new Fixture();
        f.Run("element.update", new JsonObject { ["id"] = f.Storey, ["attrs"] = new JsonObject { ["height"] = 3.2, ["elevation"] = -2.5 } });
        Assert.Equal(3.2, f.Model.Get(f.Storey)!.Num("height"));
        Assert.Equal(-2.5, f.Model.Get(f.Storey)!.Num("elevation"));
    }

    [Fact]
    public void A_new_stair_types_tread_aims_at_its_target_and_an_older_type_keeps_the_step_rules_middle()
    {
        // G10 (the user's choice, 22.11.2026): treadTarget was never read. 3.15 m in 18 risers of 0.175: the step rule's
        // middle gives 0.62 − 0.35 = 0.27; the target 0.28 keeps 2h + b = 0.63 within 0.60–0.64, so a new type takes 0.28.
        var made = StairSolver.Resolve(StairSpec.Default(), 3.15, new StairInputs(Risers: 18));
        Assert.Equal(0.175, made.RiserHeight, 9);
        Assert.Equal(0.28, made.Tread, 9);

        // A type saved before (its rules carry no aimTread): the step rule's middle, as it was drawn.
        var json = StairSpec.Default().ToJson();
        json["rules"]!.AsObject().Remove("aimTread");
        var older = StairSpec.From(json)!;
        Assert.False(older.Rules.AimTread);
        Assert.Equal(0.27, StairSolver.Resolve(older, 3.15, new StairInputs(Risers: 18)).Tread, 9);

        // A target that would break the step rule is not taken: 0.32 + 0.35 = 0.67 > 0.64.
        var far = StairSpec.Default() with { Rules = StairRules.Residential with { TreadTarget = 0.32 } };
        Assert.Equal(0.27, StairSolver.Resolve(far, 3.15, new StairInputs(Risers: 18)).Tread, 9);
    }
}
