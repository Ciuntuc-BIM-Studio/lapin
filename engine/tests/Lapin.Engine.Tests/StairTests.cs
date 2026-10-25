using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stairs (docs/spec-stairs.md, decision 120): the solver works the risers
/// and tread out of the height and the rules, the shapes lay the flights out,
/// the stair builds its parts and its plan symbol, asks the floor above for
/// its opening and follows the type; it exports as a group.
/// </summary>
public class StairTests
{
    static Fixture TwoStoreys(out string upper)
    {
        var f = new Fixture(new ArchitectureModule());
        upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        return f;
    }

    static string Stair(Fixture f, string shape, string turn = "landing", string side = "left", double x = 0, double y = 0)
    {
        var spec = StairSpec.Default() with { Shape = shape, Turn = turn, Name = $"{shape} {turn}" };
        var type = (string)f.Run("architecture.createStairType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        return (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = x, ["y"] = y, ["x2"] = x + 1, ["y2"] = y, ["turn"] = side })!["id"]!;
    }

    [Fact]
    public void The_solver_takes_the_height_and_the_rules_to_risers_and_tread()
    {
        var r = StairSolver.Resolve(StairSpec.Default(), 3.0);
        Assert.Equal(18, r.Risers);
        Assert.Equal(3.0 / 18, r.RiserHeight, 9);
        Assert.InRange(r.Tread, 0.26, 0.32);
        Assert.InRange(r.Step, 0.60, 0.64);
        Assert.True(r.Ok, string.Join("; ", r.Checks.Where(c => !c.Ok).Select(c => c.Label)));
        Assert.Single(r.Flights);
        // Fixed to 16 risers: the riser grows and the tread follows the step rule.
        var fixedN = StairSolver.Resolve(StairSpec.Default(), 3.0, new StairInputs(Risers: 16));
        Assert.Equal(16, fixedN.Risers);
        Assert.Equal(0.1875, fixedN.RiserHeight, 9);
        Assert.InRange(fixedN.Step, 0.60, 0.64);
        Assert.Contains(fixedN.Checks, c => c.Key == "riser" && !c.Ok);
        // A public stair: lower risers, more of them.
        var pub = StairSolver.Resolve(StairSpec.Default() with { Rules = StairRules.Public }, 3.0);
        Assert.True(pub.Risers >= 19);
    }

    [Fact]
    public void The_shapes_lay_the_flights_out_with_landings_or_winders()
    {
        var u = StairSolver.Resolve(StairSpec.Default() with { Shape = "u" }, 3.0);
        Assert.Equal([9, 9], u.Flights.Select(x => x.Risers));
        Assert.Single(u.Landings);
        Assert.Equal(-1, u.Flights[1].Dir.X, 9);                       // back the way it came
        Assert.Equal(1.0, u.Flights[1].Start.Y, 9);                    // one width to the left
        var l = StairSolver.Resolve(StairSpec.Default() with { Shape = "l", Turn = "winders", Winders = 3 }, 3.0, turnLeft: false);
        Assert.Equal(3, l.Winders.Count);
        Assert.Equal([8, 7], l.Flights.Select(x => x.Risers));
        Assert.Equal(-1, l.Flights[1].Dir.Y, 9);                       // a right turn climbs on towards -Y
        Assert.Equal(18, l.Flights.Sum(x => x.Risers) + l.Winders.Count);
        Assert.All(l.Winders, w => Assert.True(w.Outline.Length >= 3));
        var sl = StairSolver.Resolve(StairSpec.Default() with { Shape = "straight-landing" }, 3.0, new StairInputs(Landing: 1.5));
        Assert.Equal(1.5, sl.LandingLength, 9);
        Assert.Equal(8 * sl.Tread + 1.5, sl.Flights[1].Start.X, 9);
    }

    [Fact]
    public void A_stair_builds_its_parts_its_symbol_and_its_quantities()
    {
        var f = TwoStoreys(out _);
        var id = Stair(f, "u");
        var g = f.Session.Geometry(id)!;
        Assert.Contains(g.Parts, p => p.Key == "flight:0");
        Assert.Contains(g.Parts, p => p.Key == "flight:1");
        Assert.Contains(g.Parts, p => p.Key == "landing:0");
        Assert.Contains(g.Parts, p => p.Key == "finish");
        Assert.Contains(g.Parts, p => p.Key == "railing:left");
        Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 1e-6, p.Name));
        // A flight of 9 risers over 1.5 m: concrete about the waist slab plus the steps.
        var flight = g.Parts.First(p => p.Key == "flight:0");
        Assert.InRange(Measure.Volume(flight.Solid), 0.35, 0.9);
        var (min, max) = (Measure.Bounds(flight.Solid)!.Value.Min, Measure.Bounds(flight.Solid)!.Value.Max);
        Assert.Equal(-0.5, min.Y, 6);
        Assert.Equal(0.5, max.Y, 6);
        Assert.Equal(1.5 - 0.03, max.Z, 6);                             // the concrete stops a finish under the landing level
        // The plan: risers below the cut as lines, a break, an arrow.
        Assert.True(g.Plan!.Count(i => i.Kind == "symbol") >= 8);
        var q = f.Session.Quantities(id);
        Assert.Equal(2, q.Count(s => s.Name == "Qto_StairFlightBaseQuantities"));
        var own = q.Single(s => s.Name == "Lapin_StairQuantities");
        Assert.Equal(18, own.Quantities.Single(x => x.Name == "NumberOfRiser").Value, 9);
        Assert.True(own.Quantities.Single(x => x.Name == "ConcreteVolume").Value > 1);
    }

    [Fact]
    public void The_floor_above_gets_an_opening_that_follows_the_stair_and_goes_with_it()
    {
        var f = TwoStoreys(out var upper);
        var slab = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = upper, ["points"] = new JsonArray(new JsonArray(-2, -2), new JsonArray(8, -2), new JsonArray(8, 6), new JsonArray(-2, 6)) })!["id"]!;
        var before = f.Session.Geometry(slab)!.Parts.Sum(p => Measure.Volume(p.Solid));
        var id = Stair(f, "straight");
        var opening = StairBuilder.OpeningOf(f.Model, id);
        Assert.NotNull(opening);
        Assert.Equal(slab, f.Model.RelatingOf(opening.Id, IfcClass.RelVoidsElement)!.Id);
        var after = f.Session.Geometry(slab)!.Parts.Sum(p => Measure.Volume(p.Solid));
        Assert.True(after < before - 0.1, $"{before} → {after}");
        // The stair moved: the opening's outline is read from the stair, so the floor follows without a second edit.
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["x"] = 1, ["y"] = 1 });
        var contour = Slabs.HoleContour(f.Model, f.Model.Get(opening.Id)!);
        Assert.True(contour.Min(p => p.Y) > 0.4);
        // Switched off, the opening goes; switched on, it comes back; deleted with the stair.
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["noOpening"] = true });
        Assert.Null(StairBuilder.OpeningOf(f.Model, id));
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["noOpening"] = false });
        Assert.NotNull(StairBuilder.OpeningOf(f.Model, id));
        f.Run("element.delete", new JsonObject { ["id"] = id });
        Assert.Null(f.Model.Get(id));
        Assert.Empty(f.Model.OfClass(IfcClass.OpeningElement));
        Assert.Equal(before, f.Session.Geometry(slab)!.Parts.Sum(p => Measure.Volume(p.Solid)), 6);
    }

    [Fact]
    public void A_stair_follows_its_type_and_what_it_fixes()
    {
        var f = TwoStoreys(out _);
        var id = Stair(f, "straight");
        var type = f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!;
        Assert.Single(f.Session.Geometry(id)!.Parts, p => p.Key!.StartsWith("flight:", StringComparison.Ordinal));
        var spec = StairSpec.From(type.Attrs["spec"])! with { Shape = "l", Turn = "winders" };
        f.Run("architecture.updateStairType", new JsonObject { ["id"] = type.Id, ["spec"] = spec.ToJson() });
        var g = f.Session.Geometry(id)!;
        Assert.Equal(2, g.Parts.Count(p => p.Key!.StartsWith("flight:", StringComparison.Ordinal)));
        Assert.Contains(g.Parts, p => p.Key == "winders");
        Assert.Equal("QUARTER_WINDING_STAIR", f.Model.Get(id)!.PredefinedType);
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["risers"] = 20, ["width"] = 1.2 });
        var check = (JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = id })!;
        Assert.Equal(20, (int)check["risers"]!);
        Assert.Equal(1.2, (double)check["width"]!, 9);
        Assert.Contains(((JsonArray)check["checks"]!).OfType<JsonObject>(), c => (string?)c["key"] == "riser" && (bool)c["ok"]!);
        var preview = (JsonObject)f.Session.Query("architecture.stairPreview", new JsonObject { ["spec"] = StairSpec.Default().ToJson(), ["height"] = 2.8 })!;
        Assert.True(((JsonArray)preview["outlines"]!).Count >= 1);
        Assert.True(((JsonArray)preview["profile"]!).Count > 10);
    }

    [Fact]
    public void A_stair_exports_as_a_group_of_flights_landings_and_railings()
    {
        var f = TwoStoreys(out _);
        Stair(f, "u");
        var path = Path.Combine(Path.GetTempPath(), $"lapin-stair-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            var stair = Assert.Single(step.OfType("IFCSTAIR"));
            Assert.Equal(new StepEnum("HALF_TURN_STAIR"), stair[8]);
            Assert.Equal(2, step.OfType("IFCSTAIRFLIGHT").Count());
            Assert.Contains(step.OfType("IFCSLAB"), s => Equals(s[8], new StepEnum("LANDING")));
            Assert.Single(step.OfType("IFCRAILING"));
            Assert.Single(step.OfType("IFCSTAIRTYPE"));
            Assert.Contains(step.OfType("IFCRELAGGREGATES"), r => Equals(r[4], new StepRef(stair.Id)));
        }
        finally { File.Delete(path); }
    }
}
