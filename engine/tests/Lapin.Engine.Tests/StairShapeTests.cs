using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The stairs the testers asked for (reports #45, #46; decision 190): spiral, curved, split, dog-leg,
/// winder and cantilever stairs, stringers, railings in five designs, a sweep set by hand, and the
/// plan's numbered steps, label and "UP".
/// </summary>
public class StairShapeTests
{
    static Fixture TwoStoreys()
    {
        var f = new Fixture(new ArchitectureModule());
        f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 });
        return f;
    }

    static string Place(Fixture f, StairSpec spec, JsonObject? extra = null)
    {
        var type = (string)f.Run("architecture.createStairType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        var args = new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0 };
        foreach (var (k, v) in extra ?? []) args[k] = v?.DeepClone();
        return (string)f.Run("architecture.addStair", args)!["id"]!;
    }

    [Fact]
    public void A_spiral_turns_each_step_its_tread_over_the_walking_radius_round_a_pole_and_is_checked_for_headroom()
    {
        var spec = StairSpec.Library["spiral"]();
        var r = StairSolver.Resolve(spec, 3.0);
        var arc = Assert.IsType<ArcRun>(r.Arc);
        Assert.Empty(r.Flights);
        Assert.Equal(r.Risers - 1, r.Winders.Count);
        Assert.Equal(spec.InnerRadius + spec.Width / 2, arc.Walk, 9);
        Assert.Equal(r.Tread / arc.Walk, Math.Abs(arc.Step), 9);
        // The foot's riser lies across the walking line at the origin; the top is round the centre.
        Assert.Equal(0, (r.Foot.At - new Vec2(0, 0)).Length, 9);
        Assert.Equal(arc.Walk, (r.Top.At - arc.Centre).Length, 9);
        Assert.Contains(r.Checks, c => c.Key == "headroom");
        var f = TwoStoreys();
        var id = Place(f, spec);
        var g = f.Session.Geometry(id)!;
        Assert.Contains(g.Parts, p => p.Key == "pole");
        Assert.Contains(g.Parts, p => p.Key == "winders");
        Assert.Contains(g.Parts, p => p.Key == "railing:right");
    }

    [Fact]
    public void A_curved_stair_turned_through_a_sweep_set_by_hand_takes_its_tread_from_it()
    {
        var spec = StairSpec.Library["curved"]();
        var free = StairSolver.Resolve(spec, 3.0);
        var set = StairSolver.Resolve(spec, 3.0, new StairInputs(Sweep: 120));
        Assert.Equal(120, Math.Abs(set.Arc!.Step * set.Arc.Steps) * 180 / Math.PI, 6);
        Assert.Equal(set.Arc.Walk * (120 * Math.PI / 180) / (set.Risers - 1), set.Tread, 9);
        Assert.NotEqual(free.Tread, set.Tread);
        // Turning right puts the centre on the right.
        Assert.True(StairSolver.Resolve(spec, 3.0, turnLeft: false).Arc!.Centre.Y < 0);
        var f = TwoStoreys();
        var id = Place(f, spec, new JsonObject { ["sweep"] = 120 });
        var d = (JsonObject)f.Session.Query("architecture.stairCheck", new JsonObject { ["id"] = id })!;
        Assert.Equal(120, (double)d["sweep"]!, 3);
    }

    [Fact]
    public void A_split_stair_climbs_up_the_middle_to_a_landing_and_returns_on_both_sides()
    {
        var spec = StairSpec.Library["split"]();
        var r = StairSolver.Resolve(spec, 3.0);
        Assert.Equal(3, r.Flights.Count);
        Assert.Single(r.Landings);
        Assert.Equal(r.Risers, r.Flights[0].Risers + r.Flights[1].Risers);
        Assert.Equal(r.Flights[1].Risers, r.Flights[2].Risers);
        // The returns go back beside the main flight, one each side, as wide as the side width.
        Assert.True(r.Flights[1].Dir.X < 0 && r.Flights[2].Dir.X < 0);
        Assert.True(r.Flights[1].Start.Y > spec.Width / 2 && r.Flights[2].Start.Y < -spec.Width / 2);
        Assert.Equal(r.WidthOf(r.Flights[1]), r.WidthOf(r.Flights[2]), 9);
        Assert.Equal("DOUBLE_RETURN_STAIR", spec.PredefinedType);
        var f = TwoStoreys();
        var g = f.Session.Geometry(Place(f, spec))!;
        Assert.Equal(3, g.Parts.Count(p => p.Key!.StartsWith("flight:", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("concrete")]
    [InlineData("entrance")]
    [InlineData("dogleg")]
    [InlineData("winder")]
    [InlineData("spiral")]
    [InlineData("curved")]
    [InlineData("cantilever")]
    [InlineData("split")]
    public void Every_stair_in_the_library_builds_without_a_problem_and_exports(string key)
    {
        var f = TwoStoreys();
        var id = (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["library"] = key, ["x"] = 2, ["y"] = 2, ["x2"] = 3, ["y2"] = 2 })!["id"]!;
        var g = f.Session.Geometry(id)!;
        Assert.NotEmpty(g.Parts);
        Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 0, $"{key}: {p.Name} has no volume"));
        Assert.DoesNotContain(g.Diagnostics ?? [], d => d.Contains("could not", StringComparison.Ordinal));
        var path = Path.Combine(Path.GetTempPath(), $"stair-{key}-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            Assert.Single(StepFile.Read(path).OfType("IFCSTAIR"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_cantilever_stair_has_open_treads_and_stringers_carry_them_on_both_sides()
    {
        var cantilever = StairSpec.Library["cantilever"]();
        var f = TwoStoreys();
        var flight = f.Session.Geometry(Place(f, cantilever))!.Parts.Single(p => p.Key == "flight:0");
        var r = StairSolver.Resolve(cantilever, 3.0);
        // One plate per tread, each its thickness: far less than a waist slab under the steps.
        Assert.Equal((r.Risers - 1) * cantilever.Width * (r.Tread + cantilever.Finish.Nosing) * cantilever.Construction.Waist, Measure.Volume(flight.Solid), 3);
        var stringers = cantilever with { Construction = cantilever.Construction with { Kind = "stringers" } };
        var withStringers = f.Session.Geometry(Place(f, stringers))!.Parts.Single(p => p.Key == "flight:0");
        Assert.True(Measure.Volume(withStringers.Solid) > Measure.Volume(flight.Solid) + 0.05);
    }

    [Theory]
    [InlineData("bar")]
    [InlineData("balusters")]
    [InlineData("glass")]
    [InlineData("cable")]
    [InlineData("panel")]
    public void A_railing_is_built_in_its_design_and_a_stair_may_take_other_sides_than_its_type(string style)
    {
        var spec = StairSpec.Default() with { Railing = new StairRailing("left", 0.9, 0.04, 0.03, 1.2, style, style == "glass" ? "round" : "square", Extension: 0.3) };
        var f = TwoStoreys();
        var id = Place(f, spec);
        var g = f.Session.Geometry(id)!;
        Assert.Contains(g.Parts, p => p.Key == "railing:left");
        if (style == "glass") Assert.Contains(g.Parts, p => p.Key == "railing-glass:left");
        if (style == "balusters")
        {
            // Balusters at most 110 mm apart: many more pieces than posts at 1.2 m.
            var bar = f.Session.Geometry(Place(f, spec with { Railing = spec.Railing with { Style = "bar" } }))!.Parts.Single(p => p.Key == "railing:left");
            Assert.True(g.Parts.Single(p => p.Key == "railing:left").Solid.Faces.Count() > 4 * bar.Solid.Faces.Count());
        }
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["railingSides"] = "both" });
        Assert.Contains(f.Session.Geometry(id)!.Parts, p => p.Key == "railing:right");
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["railingSides"] = null });
        Assert.DoesNotContain(f.Session.Geometry(id)!.Parts, p => p.Key == "railing:right");
    }

    [Fact]
    public void A_plan_numbers_every_step_from_the_first_riser_up_and_labels_the_stair()
    {
        // Report #46: numbered steps in circles, as the tester's drawing shows.
        var spec = StairSpec.Default() with { Shape = "u", Symbols = new StairPlanSymbols(Numbers: true, Label: true) };
        var r = StairSolver.Resolve(spec, 3.0);
        var centres = StairMarks.StepCentres(r);
        Assert.Equal(Enumerable.Range(1, r.Risers), centres.Select(c => c.Number).Order());
        var winders = StairMarks.StepCentres(StairSolver.Resolve(spec with { Turn = "winders" }, 3.0));
        Assert.Equal(Enumerable.Range(1, r.Risers), winders.Select(c => c.Number).Order());
        var round = StairMarks.StepCentres(StairSolver.Resolve(StairSpec.Library["spiral"](), 3.0));
        Assert.Equal(round.Count, round.Select(c => c.Number).Distinct().Count());

        var f = TwoStoreys();
        Place(f, spec);
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["storeyId"] = f.Storey })!;
        var texts = d.ToJsonString();
        Assert.Contains("\"UP\"", texts);
        Assert.Contains($"\"{r.Risers}\"", texts);
        Assert.Contains("Risers", texts);
    }
}
