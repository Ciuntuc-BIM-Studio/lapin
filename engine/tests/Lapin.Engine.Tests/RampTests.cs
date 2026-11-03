using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Ramps in the stair configurator (decision 252, report #53): a ramp is a stair type of kind
/// <c>ramp</c>, an IfcRampType whose ramps (IfcRamp) climb by a slope instead of risers, in
/// flights no longer than the type's longest run with landings between; straight, with landings,
/// an L or a switchback; up to the storey above or a rise of their own, from below the storey's
/// level (an entrance ramp) or up from it (to a terrace). Stairs take the same rise from the level.
/// </summary>
public class RampTests
{
    static Fixture TwoStoreys(out string upper)
    {
        var f = new Fixture(new ArchitectureModule());
        upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        return f;
    }

    static StairSpec Ramp(string shape = "straight-landing") => StairSpec.Ramp() with { Shape = shape, Name = $"Ramp {shape}" };

    [Fact]
    public void The_solver_splits_the_run_into_flights_no_longer_than_the_longest_with_landings_between()
    {
        // 0.6 m at 6 %: 10 m of run, longer than the 9 m a flight may run, so two flights of 5 m with a landing.
        var r = RampSolver.Resolve(Ramp() with { Slope = 0.06, MaxRun = 9 }, 0.6);
        Assert.Equal(2, r.Flights.Count);
        Assert.All(r.Flights, fl => Assert.Equal(5.0, fl.Length, 9));
        Assert.Equal(0.3, r.Flights[0].Z1, 9);
        Assert.Equal(0.3, r.Flights[1].Z0, 9);
        Assert.Equal(0.6, r.Flights[1].Z1, 9);
        var landing = Assert.Single(r.Landings);
        Assert.Equal(0.3, landing.Z, 9);
        // The second flight starts past the landing, straight on.
        Assert.Equal(5.0 + r.LandingLength, r.Flights[1].Start.X, 9);
        Assert.Equal(10.0 + r.LandingLength, r.Top.At.X, 9);
        Assert.True(r.Ok, string.Join("; ", r.Checks.Where(c => !c.Ok).Select(c => c.Label)));

        // Straight, it is one flight of 10 m, and the check says it runs too long.
        var straight = RampSolver.Resolve(Ramp("straight") with { Slope = 0.06, MaxRun = 9 }, 0.6);
        Assert.Single(straight.Flights);
        Assert.Contains(straight.Checks, c => c.Key == "run" && !c.Ok);
        // Steeper than the rules allow: said so.
        var steep = RampSolver.Resolve(Ramp() with { Slope = 0.12 }, 0.6);
        Assert.Contains(steep.Checks, c => c.Key == "slope" && !c.Ok);
        // The instance's own slope wins over the type's.
        Assert.Equal(0.05, RampSolver.Resolve(Ramp(), 0.6, new RampInputs(Slope: 0.05)).Slope, 9);
    }

    [Fact]
    public void A_switchback_ramp_goes_back_and_forth_in_lanes_side_by_side()
    {
        // 1.5 m at 6 %: 25 m of run in flights of at most 9 m, so three of 8.33 m, each lane back the way the last came.
        var r = RampSolver.Resolve(Ramp("u") with { Slope = 0.06, MaxRun = 9, Width = 1.2, Gap = 0.1 }, 1.5);
        Assert.Equal(3, r.Flights.Count);
        Assert.Equal(2, r.Landings.Count);
        Assert.Equal(1, r.Flights[0].Dir.X, 9);
        Assert.Equal(-1, r.Flights[1].Dir.X, 9);
        Assert.Equal(1, r.Flights[2].Dir.X, 9);
        Assert.Equal(1.3, r.Flights[1].Start.Y - r.Flights[0].Start.Y, 9);
        Assert.Equal(2.6, r.Flights[2].Start.Y, 9);
        Assert.Equal(1.5, r.Flights[2].Z1, 9);
        // Turned right, the lanes go the other way.
        var right = RampSolver.Resolve(Ramp("u") with { Slope = 0.06, MaxRun = 9, Width = 1.2, Gap = 0.1 }, 1.5, turnLeft: false);
        Assert.Equal(-2.6, right.Flights[2].Start.Y, 9);
        // An L: two flights at right angles.
        var l = RampSolver.Resolve(Ramp("l"), 0.6);
        Assert.Equal(2, l.Flights.Count);
        Assert.Equal(1, l.Flights[1].Dir.Y, 9);
    }

    [Fact]
    public void A_ramp_between_storeys_is_an_IfcRamp_that_reaches_the_floor_above_and_opens_it()
    {
        var f = TwoStoreys(out var upper);
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = upper, ["points"] = new JsonArray(new JsonArray(-5, -10), new JsonArray(60, -10), new JsonArray(60, 10), new JsonArray(-5, 10)) });
        var type = (string)f.Run("architecture.createStairType", new JsonObject { ["spec"] = (Ramp("u") with { Slope = 0.08, MaxRun = 9 }).ToJson() })!["id"]!;
        Assert.Equal(IfcClass.RampType, f.Model.Get(type)!.Class);
        Assert.Equal("HALF_TURN_RAMP", f.Model.Get(type)!.PredefinedType);
        var id = (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0 })!["id"]!;
        var ramp = f.Model.Get(id)!;
        Assert.Equal(IfcClass.Ramp, ramp.Class);
        Assert.StartsWith("Ramp", ramp.Name);
        var check = (JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = id })!;
        Assert.Equal("ramp", (string)check["kind"]!);
        Assert.Equal(3.0, (double)check["toLevel"]!, 9);
        Assert.Equal(0.08, (double)check["slope"]!, 9);
        Assert.Equal(37.5, (double)check["run"]!, 6);                   // 3 m at 8 %
        Assert.NotNull(check["topStorey"]);
        Assert.NotNull(StairBuilder.OpeningOf(f.Model, id));
        var g = f.Session.Geometry(id)!;
        var flights = g.Parts.Where(p => p.Key!.StartsWith("flight:", StringComparison.Ordinal)).ToList();
        Assert.Equal(5, flights.Count);                                 // 37.5 m in flights of at most 9 m
        Assert.Equal(4, g.Parts.Count(p => p.Key!.StartsWith("landing:", StringComparison.Ordinal)));
        // The last flight's finished top comes to the floor above's level.
        var top = g.Parts.Where(p => p.Key == "finish").Max(p => Measure.Bounds(p.Solid)!.Value.Max.Z);
        Assert.Equal(3.0, top, 6);
        Assert.Contains(g.Parts, p => p.Key!.StartsWith("railing", StringComparison.Ordinal));
        Assert.Contains(f.Session.Quantities(id), q => q.Name == "Lapin_RampQuantities");
        // A ramp's type cannot be given to a stair, nor a stair's to a ramp.
        var stairType = (string)f.Run("architecture.createStairType", new JsonObject { ["spec"] = StairSpec.Default().ToJson() })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["typeId"] = stairType }));
        // Nor does a type turn from one to the other.
        Assert.Throws<CommandException>(() => f.Run("architecture.updateStairType", new JsonObject { ["id"] = type, ["spec"] = StairSpec.Default().ToJson() }));
        // Its slope set on the instance: a longer run.
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["slope"] = 0.06 });
        Assert.Equal(50.0, (double)((JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = id })!)["run"]!, 6);
    }

    [Fact]
    public void An_entrance_ramp_comes_up_from_below_to_the_storey_level_as_a_solid_wedge()
    {
        var f = new Fixture(new ArchitectureModule());
        var id = (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["library"] = "entrance-ramp", ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0 })!["id"]!;
        Assert.Equal(IfcClass.Ramp, f.Model.Get(id)!.Class);
        Assert.Equal(-0.45, f.Model.Get(id)!.Num("baseOffset"), 9);
        var check = (JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = id })!;
        Assert.Equal(-0.45, (double)check["fromLevel"]!, 9);
        Assert.Equal(0.0, (double)check["toLevel"]!, 9);
        Assert.Null(check["topStorey"]);
        // 0.45 m at 6 %: 7.5 m, one flight; solid concrete from the ground, a finish under the surface.
        var g = f.Session.Geometry(id)!;
        var flight = Assert.Single(g.Parts, p => p.Key == "flight:0");
        var spec = StairSpec.Library["entrance-ramp"]();
        Assert.Equal(0.5 * 7.5 * 0.45 * spec.Width, Measure.Volume(flight.Solid), 6);
        var (min, max) = Measure.Bounds(flight.Solid)!.Value;
        Assert.Equal(7.5, max.X - min.X, 6);
        Assert.Equal(-0.45 - spec.Finish.TreadThickness, min.Z, 6);
    }

    [Fact]
    public void A_stair_with_a_rise_of_its_own_may_start_at_its_storey_level_and_climb_from_it()
    {
        var f = new Fixture(new ArchitectureModule());
        var spec = StairSpec.Default("Terrace stair") with { Rise = 1.2, RiseFrom = "level" };
        var type = (string)f.Run("architecture.createStairType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        var id = (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0 })!["id"]!;
        Assert.Equal(0.0, f.Model.Get(id)!.Num("baseOffset"), 9);
        var check = (JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = id })!;
        Assert.Equal(0.0, (double)check["fromLevel"]!, 9);
        Assert.Equal(1.2, (double)check["toLevel"]!, 9);
        // A higher rise on the type: its stairs keep their foot on the level and climb higher.
        f.Run("architecture.updateStairType", new JsonObject { ["id"] = type, ["spec"] = (spec with { Rise = 1.5 }).ToJson() });
        Assert.Equal(0.0, f.Model.Get(id)!.Num("baseOffset"), 9);
        check = (JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = id })!;
        Assert.Equal(1.5, (double)check["toLevel"]!, 9);
        // Its own rise changed on the instance: the foot stays too.
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["rise"] = 0.9 });
        check = (JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = id })!;
        Assert.Equal(0.0, (double)check["fromLevel"]!, 9);
        Assert.Equal(0.9, (double)check["toLevel"]!, 9);
        // Read back, an unknown anchor is the entrance steps' (from below).
        Assert.Equal("below", StairSpec.From((spec with { RiseFrom = "sideways" }).ToJson())!.RiseFrom);
    }

    [Fact]
    public void A_ramp_exports_as_an_IfcRamp_of_ramp_flights_and_landings()
    {
        var f = new Fixture(new ArchitectureModule());
        var type = (string)f.Run("architecture.createStairType", new JsonObject { ["spec"] = (Ramp() with { Rise = 0.6, Slope = 0.06, MaxRun = 6 }).ToJson() })!["id"]!;
        f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0 });
        var path = Path.Combine(Path.GetTempPath(), $"lapin-ramp-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            var ramp = Assert.Single(step.OfType("IFCRAMP"));
            Assert.Equal(new StepEnum("TWO_STRAIGHT_RUN_RAMP"), ramp[8]);
            Assert.Equal(2, step.OfType("IFCRAMPFLIGHT").Count());
            Assert.Contains(step.OfType("IFCSLAB"), s => Equals(s[8], new StepEnum("LANDING")));
            Assert.Single(step.OfType("IFCRAMPTYPE"));
            Assert.Empty(step.OfType("IFCSTAIR"));
            Assert.Contains(step.OfType("IFCRELAGGREGATES"), r => Equals(r[4], new StepRef(ramp.Id)));
        }
        finally { File.Delete(path); }
    }
}
