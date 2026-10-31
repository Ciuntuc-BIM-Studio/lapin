using System.Text.Json.Nodes;
using Lapin.Geometry;
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
        // The first flight's last riser lands on the first winder (no double riser where they meet), so the flights keep one riser more.
        Assert.Equal([8, 8], l.Flights.Select(x => x.Risers));
        Assert.Equal(-1, l.Flights[1].Dir.Y, 9);                       // a right turn climbs on towards -Y
        Assert.Equal(18, l.Flights.Sum(x => x.Risers) + l.Winders.Count - 1);
        Assert.Equal(l.Flights[0].Risers * l.RiserHeight, l.Winders[0].Z, 9);
        Assert.Equal(l.Flights[1].Z0, l.Winders[^1].Z, 9);
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

    // ─── Entrance steps (feedback report #33: a step at the porch) ─────────

    [Fact]
    public void Entrance_steps_take_a_rise_of_their_own_in_few_risers()
    {
        var spec = StairSpec.Entrance();
        Assert.Equal(0.45, spec.Rise);
        Assert.Equal("solid", spec.Construction.Kind);
        var r = StairSolver.Resolve(spec, spec.Rise!.Value);
        Assert.Equal(3, r.Risers);
        Assert.Equal(0.15, r.RiserHeight, 9);
        Assert.Equal(0.35, r.Tread, 9);
        Assert.Equal(2.0, r.Width, 9);
        Assert.True(r.Ok, string.Join("; ", r.Checks.Where(c => !c.Ok).Select(c => c.Label)));
        // One step of 150 mm: a single riser, within the entrance rules.
        var one = StairSolver.Resolve(spec, 0.15);
        Assert.Equal(1, one.Risers);
        Assert.True(one.Ok, string.Join("; ", one.Checks.Where(c => !c.Ok).Select(c => c.Label)));
        // Fixed to two risers over 300 mm.
        Assert.Equal(2, StairSolver.Resolve(spec, 0.30, new StairInputs(Risers: 2)).Risers);
        // A stair between storeys keeps at least two risers and three to a flight in its checks.
        var low = StairSolver.Resolve(StairSpec.Default(), 0.15);
        Assert.Equal(2, low.Risers);
        Assert.Contains(low.Checks, c => c.Key == "flight" && !c.Ok);
        // The spec survives its JSON, with the rule set's minimum.
        var back = StairSpec.From(spec.ToJson())!;
        Assert.Equal(0.45, back.Rise);
        Assert.Equal(1, back.Rules.MinRisers);
        Assert.Equal(3, StairSpec.From(StairSpec.Default().ToJson())!.Rules.MinRisers);
        Assert.Null(StairSpec.From(StairSpec.Default().ToJson())!.Rise);
    }

    [Fact]
    public void Entrance_steps_come_up_from_below_the_storey_to_its_level_as_solid_concrete()
    {
        var f = TwoStoreys(out var upper);
        // A floor above over the steps: they climb no storey, so they ask it for no opening.
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = upper, ["points"] = new JsonArray(new JsonArray(-3, -3), new JsonArray(3, -3), new JsonArray(3, 3), new JsonArray(-3, 3)) });
        var id = (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["library"] = "entrance", ["x"] = 0, ["y"] = -1, ["x2"] = 0, ["y2"] = 0 })!["id"]!;
        var stair = f.Model.Get(id)!;
        Assert.Equal(-0.45, stair.Num("baseOffset"), 9);
        Assert.Null(StairBuilder.OpeningOf(f.Model, id));
        var type = f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!;
        Assert.Equal("Entrance steps", type.Name);
        Assert.Equal("entrance", type.Str("library"));
        // Asked again, the library gives the project's copy, not a second one; a plain stair still takes a storey type.
        Assert.Equal(type.Id, (string)f.Run("architecture.stairTypeFromLibrary", new JsonObject { ["key"] = "entrance" })!["id"]!);
        var plain = (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 10, ["y"] = 0, ["x2"] = 11, ["y2"] = 0 })!["id"]!;
        Assert.NotEqual(type.Id, f.Model.RelatingOf(plain, IfcClass.RelDefinesByType)!.Id);
        Assert.Equal(18, (int)((JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = plain })!)["risers"]!);

        var check = (JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = id })!;
        Assert.Equal(3, (int)check["risers"]!);
        Assert.Equal(-0.45, (double)check["fromLevel"]!, 9);
        Assert.Equal(0.0, (double)check["toLevel"]!, 9);
        Assert.Null(check["topStorey"]);
        Assert.True((bool)check["ok"]!);
        // One solid of concrete from the ground (−0.45, a finish under) to a finish under ±0.00, 2 m wide, climbing +Y.
        var g = f.Session.Geometry(id)!;
        var flight = g.Parts.Single(p => p.Key == "flight:0");
        var (min, max) = Measure.Bounds(flight.Solid)!.Value;
        Assert.Equal(-0.45 - 0.03, min.Z, 6);
        Assert.Equal(-0.03, max.Z, 6);
        Assert.Equal(-1.0, min.X, 6);
        Assert.Equal(1.0, max.X, 6);
        Assert.Equal(-1.0, min.Y, 6);
        Assert.Equal(-1.0 + 2 * 0.35, max.Y, 6);
        Assert.Equal(2.0 * 3 * 0.35 * 0.15, Measure.Volume(flight.Solid), 6);
        Assert.DoesNotContain(g.Parts, p => p.Key!.StartsWith("railing", StringComparison.Ordinal));

        // A lower rise keeps the top at ±0.00 and moves the foot up.
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["rise"] = 0.30 });
        check = (JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = id })!;
        Assert.Equal(2, (int)check["risers"]!);
        Assert.Equal(-0.30, (double)check["fromLevel"]!, 9);
        Assert.Equal(0.0, (double)check["toLevel"]!, 9);
        // One riser: no tread to build, the step is the porch's edge, said so.
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["rise"] = 0.15 });
        g = f.Session.Geometry(id)!;
        Assert.DoesNotContain(g.Parts, p => p.Key!.StartsWith("flight:", StringComparison.Ordinal));
        Assert.NotNull(g.Diagnostics);
        Assert.Single(g.Plan!, i => i.Kind == "symbol");
    }

    [Fact]
    public void A_stair_of_any_type_can_climb_a_rise_of_its_own_from_a_base_offset()
    {
        var f = TwoStoreys(out _);
        var id = Stair(f, "straight");
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["rise"] = 0.6, ["baseOffset"] = -0.6 });
        var check = (JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = id })!;
        Assert.Equal(0.6, (double)check["height"]!, 9);
        Assert.Equal(-0.6, (double)check["fromLevel"]!, 9);
        Assert.Null(check["topStorey"]);
        // Freed, it climbs to the storey above again, from its base offset.
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["rise"] = null, ["baseOffset"] = 0 });
        check = (JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = id })!;
        Assert.Equal(3.0, (double)check["height"]!, 9);
        Assert.NotNull(check["topStorey"]);
        // Its type changed to entrance steps: its top comes to the storey's level.
        var entrance = (string)f.Run("architecture.stairTypeFromLibrary", new JsonObject { ["key"] = "entrance" })!["id"]!;
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["typeId"] = entrance });
        Assert.Equal(-0.45, f.Model.Get(id)!.Num("baseOffset"), 9);
        // The type's rise changed: its stairs keep their tops.
        var spec = StairSpec.From(f.Model.Get(entrance)!.Attrs["spec"])! with { Rise = 0.60 };
        f.Run("architecture.updateStairType", new JsonObject { ["id"] = entrance, ["spec"] = spec.ToJson() });
        Assert.Equal(-0.60, f.Model.Get(id)!.Num("baseOffset"), 9);
    }

    [Fact]
    public void A_porch_is_a_floor_outside_the_walls_with_its_top_at_the_storey_level()
    {
        var f = new Fixture(new ArchitectureModule());
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 });
        var porch = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(2, -0.2), new JsonArray(4, -0.2), new JsonArray(4, -1.7), new JsonArray(2, -1.7)) })!["id"]!;
        Assert.Equal(0.0, Slabs.Top(f.Model, f.Model.Get(porch)!), 9);
        var (min, max) = Measure.Bounds(f.Session.Geometry(porch)!.Parts[0].Solid)!.Value;
        Assert.True(max.Z <= 1e-6 && max.Z > -0.05, $"{max.Z}");
        // The steps in front of it, coming up to its edge.
        var steps = (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["library"] = "entrance", ["x"] = 3, ["y"] = -1.7 - 0.7, ["x2"] = 3, ["y2"] = 0 })!["id"]!;
        var top = (JsonArray)((JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = steps })!)["top"]!;
        Assert.Equal(-1.7, (double)top[1]!, 6);
    }

    [Fact]
    public void Entrance_steps_export_their_flight_numbers_to_IFC()
    {
        var f = new Fixture(new ArchitectureModule());
        f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["library"] = "entrance", ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0 });
        var path = Path.Combine(Path.GetTempPath(), $"lapin-steps-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            Assert.Equal(new StepEnum("STRAIGHT_RUN_STAIR"), Assert.Single(step.OfType("IFCSTAIR"))[8]);
            var flight = Assert.Single(step.OfType("IFCSTAIRFLIGHT"));
            Assert.Equal(3L, flight[8]);
            Assert.Equal(2L, flight[9]);
            Assert.Equal(0.15, Convert.ToDouble(flight[10]), 6);
            Assert.Equal(0.35, Convert.ToDouble(flight[11]), 6);
            var text = File.ReadAllText(path);
            Assert.Contains("'Pset_StairCommon'", text);
            Assert.Contains("IFCPROPERTYSINGLEVALUE('NumberOfRiser',$,IFCCOUNTMEASURE(3),$)", text);
        }
        finally { File.Delete(path); }
    }
}
