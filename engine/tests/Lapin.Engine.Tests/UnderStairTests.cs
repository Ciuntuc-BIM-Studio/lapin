using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The space under a stair (a tester's report, #71): it fills the space under a flight between the floor and the soffit,
/// its parts laid out to it and cut by the slope, follows the stair when it changes, goes with it, and exports as an
/// assembly of furniture connected to its stair.
/// </summary>
public class UnderStairTests
{
    static Fixture TwoStoreys()
    {
        var f = new Fixture(new ArchitectureModule());
        f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 });
        return f;
    }

    static string Stair(Fixture f, StairSpec? spec = null, string turn = "left")
    {
        spec ??= StairSpec.Default();
        var type = (string)f.Run("architecture.createStairType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        return (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0, ["turn"] = turn })!["id"]!;
    }

    static string Nook(Fixture f, string stair, string style = "storage", JsonObject? more = null)
    {
        var a = new JsonObject { ["stairId"] = stair, ["style"] = style };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("architecture.addUnderStair", a)!["id"]!;
    }

    /// <summary>The soffit of the default stair's flight at x along it (a waist of 150 mm under the step corners, the finish 30 mm under the treads).</summary>
    static double Soffit(Fixture f, string stair, double x)
    {
        var (r, _, spec) = StairBuilder.Resolve(f.Model, f.Model.Get(stair)!)!.Value;
        var (h, b) = (r.RiserHeight, r.Tread);
        var wv = spec.Construction.Waist * Math.Sqrt(h * h + b * b) / b;
        return Math.Max(-spec.Finish.TreadThickness, x * h / b - wv - spec.Finish.TreadThickness);
    }

    [Fact]
    public void It_fills_the_space_under_the_flight_up_to_the_soffit_and_no_further()
    {
        var f = TwoStoreys();
        var stair = Stair(f);
        var id = Nook(f, stair);
        var e = f.Model.Get(id)!;
        Assert.Equal(UnderStairs.Class, e.Class);
        Assert.Equal(stair, e.Str("host"));
        Assert.Equal(Spatial.StoreyOf(f.Model, stair)!.Id, Spatial.StoreyOf(f.Model, id)!.Id);
        var g = f.Session.Geometry(id)!;
        Assert.Null(g.Diagnostics);
        // The storage style: doors on the open side, shelves behind them, a bookcase at the high end, boards.
        Assert.Contains(g.Parts, p => p.Key!.StartsWith("nook-door:", StringComparison.Ordinal));
        Assert.Contains(g.Parts, p => p.Key == "nook-shelves");
        Assert.Contains(g.Parts, p => p.Key == "nook-bookcase");
        Assert.Contains(g.Parts, p => p.Key == "nook-panel");
        var (r, _, _) = StairBuilder.Resolve(f.Model, f.Model.Get(stair)!)!.Value;
        var run = (r.Flights[0].Risers - 1) * r.Tread;
        foreach (var p in g.Parts)
            foreach (var v in p.Solid.Vertices)
            {
                // Under the flight (climbing +X from the origin, 1 m wide about y = 0) and under its soffit, over the floor.
                Assert.InRange(v.X, -1e-6, run + 1e-6);
                Assert.InRange(v.Y, -0.5 - 0.04, 0.5 + 1e-6);
                Assert.InRange(v.Z, -1e-6, 3.0);
                Assert.True(v.Z <= Soffit(f, stair, v.X) + 1e-6, $"{p.Name} reaches {v.Z:0.000} over the soffit {Soffit(f, stair, v.X):0.000} at x {v.X:0.000}");
            }
        // It starts where the soffit stands 300 mm over the floor and ends at the flight's last riser, its high end tall.
        var minX = g.Parts.Min(p => Measure.Bounds(p.Solid)!.Value.Min.X);
        Assert.InRange(Soffit(f, stair, minX), 0.27, 0.33);
        var top = g.Parts.Max(p => Measure.Bounds(p.Solid)!.Value.Max.Z);
        Assert.True(top > 2.2, $"the high end reaches {top:0.00}");
        // Its quantities: the space's length, depth and volume, each part's pieces.
        var q = f.Session.Quantities(id);
        var set = Assert.Single(q, s => s.Name == "Lapin_UnderStairQuantities");
        Assert.Equal(1.0, set.Quantities.First(x => x.Name == "Depth").Value, 6);
        Assert.True(set.Quantities.First(x => x.Name == "GrossVolume").Value > 1.0);
        Assert.Contains(q, s => s.Name == "Lapin_UnderStairPart" && s.Part == "nook-shelves" && s.Quantities[0].Value >= 3);
    }

    [Fact]
    public void Each_style_lays_its_parts_out_and_says_what_does_not_fit()
    {
        var f = TwoStoreys();
        var stair = Stair(f);
        string[] Keys(string id) => f.Session.Geometry(id)!.Parts.Select(p => p.Key!.Split(':')[0]).Distinct().ToArray();
        Assert.Contains("nook-desk", Keys(Nook(f, stair, "office")));
        Assert.Contains("nook-light", Keys(Nook(f, stair, "office")));
        var reading = Keys(Nook(f, stair, "reading"));
        Assert.Contains("nook-bench", reading);
        Assert.Contains("nook-cushion", reading);
        Assert.Contains("nook-bookcase", reading);
        Assert.DoesNotContain("nook-door", reading);
        var mudroom = Keys(Nook(f, stair, "mudroom"));
        Assert.Contains("nook-cubbies", mudroom);
        Assert.Contains("nook-hooks", mudroom);
        var play = Keys(Nook(f, stair, "playhouse"));
        Assert.Contains("nook-door", play);
        Assert.Contains("nook-panel", play);
        var dog = Keys(Nook(f, stair, "dogbed"));
        Assert.Contains("nook-bed", dog);
        Assert.Contains("nook-trim", dog);
        // Ended where the soffit is still low, the desk has no headroom: it is left out, and said.
        var low = Nook(f, stair, "office", new JsonObject { ["end"] = 1.4 });
        var g = f.Session.Geometry(low)!;
        Assert.DoesNotContain(g.Parts, p => p.Key == "nook-desk");
        Assert.Contains(g.Diagnostics!, d => d.Contains("desk", StringComparison.Ordinal));
        // The cupboard's doors over the whole of a front of doors: each about the door width.
        var doors = f.Session.Geometry(Nook(f, stair, "storage", new JsonObject { ["start"] = 1.0 }))!.Parts.Where(p => p.Key!.StartsWith("nook-door:", StringComparison.Ordinal)).ToList();
        Assert.True(doors.Count >= 5, $"{doors.Count} doors");
        Assert.All(doors, d => Assert.InRange(Measure.Bounds(d.Solid)!.Value.Max.X - Measure.Bounds(d.Solid)!.Value.Min.X, 0.35, 0.5));
    }

    [Fact]
    public void It_follows_the_stair_when_the_stair_changes_and_goes_with_it()
    {
        var f = TwoStoreys();
        var stair = Stair(f);
        var id = Nook(f, stair);
        var before = f.Bounds(id);
        f.Run("architecture.updateStair", new JsonObject { ["id"] = stair, ["width"] = 1.4, ["rotation"] = 90 });
        Assert.NotNull(f.Model.Get(id));
        var after = f.Bounds(id);
        // Turned to climb along +Y and 1.4 m wide: the space under it turns and widens, the same element.
        Assert.True(after.Max.Y - after.Min.Y > before.Max.X - before.Min.X - 0.5);
        Assert.InRange(after.Max.X - after.Min.X, 1.35, 1.45);
        // Placed on the other side, then under a deeper soffit: the same id, rebuilt.
        f.Run("architecture.updateUnderStair", new JsonObject { ["id"] = id, ["side"] = "left", ["depth"] = 0.8 });
        var left = f.Bounds(id);
        Assert.InRange(left.Max.X - left.Min.X, 0.78, 0.84);
        // The stair deleted, the space goes with it; undone, both come back.
        f.Run("element.delete", new JsonObject { ["id"] = stair });
        Assert.Null(f.Model.Get(id));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(id));
    }

    [Fact]
    public void Only_a_stair_with_straight_flights_and_room_under_it_takes_one()
    {
        var f = TwoStoreys();
        var solid = Stair(f, StairSpec.Default("Solid") with { Construction = new StairConstruction("solid", 0.15, 0.15, "Concrete") });
        Assert.Throws<CommandException>(() => Nook(f, solid));
        var spiral = Stair(f, StairSpec.Library["spiral"]());
        Assert.Throws<CommandException>(() => Nook(f, spiral));
        // A U stair: under its second flight, which starts at the landing, the space is tall from its start.
        var u = Stair(f, StairSpec.Default("U") with { Shape = "u" });
        var second = Nook(f, u, "office", new JsonObject { ["flight"] = 2 });
        Assert.Null(f.Session.Geometry(second)!.Diagnostics);
        Assert.Contains(f.Session.Geometry(second)!.Parts, p => p.Key == "nook-desk");
        // Open treads: the space goes up to the treads' underside.
        var open = Stair(f, StairSpec.Library["cantilever"]());
        var g = f.Session.Geometry(Nook(f, open))!;
        Assert.NotEmpty(g.Parts);
    }

    [Fact]
    public void The_projects_own_styles_are_saved_followed_and_deleted()
    {
        var f = TwoStoreys();
        var stair = Stair(f);
        var a = Nook(f, stair, "reading");
        var spec = UnderStairSpec.Preset("reading")! with { BenchHeight = 0.5, Carcass = "Birch plywood" };
        var sid = (string)f.Run("architecture.saveUnderStairStyle", new JsonObject { ["id"] = a, ["name"] = "Our nook", ["spec"] = spec.ToJson() })!["styleId"]!;
        Assert.StartsWith("own:", sid);
        Assert.Equal(sid, f.Model.Get(a)!.Str("style"));
        Assert.NotNull(Materials.Find(f.Model, "Birch plywood"));
        var b = Nook(f, stair, sid);
        Assert.Equal(0.5, UnderStairs.SpecOf(f.Model.Get(b)!)!.BenchHeight, 9);
        // Saved again: both spaces of the style follow.
        f.Run("architecture.saveUnderStairStyle", new JsonObject { ["styleId"] = sid, ["name"] = "Our nook", ["spec"] = (spec with { BenchHeight = 0.42 }).ToJson() });
        Assert.All(new[] { a, b }, id => Assert.Equal(0.42, UnderStairs.SpecOf(f.Model.Get(id)!)!.BenchHeight, 9));
        f.Run("architecture.renameUnderStairStyle", new JsonObject { ["styleId"] = sid, ["name"] = "Reading corner" });
        Assert.Equal("Reading corner", (string?)UnderStairs.Styles(f.Model)[0]["label"]);
        f.Run("architecture.deleteUnderStairStyle", new JsonObject { ["styleId"] = sid });
        Assert.Empty(UnderStairs.Styles(f.Model));
        Assert.Null(f.Model.Get(a)!.Str("style"));
        Assert.Equal(0.42, UnderStairs.SpecOf(f.Model.Get(a)!)!.BenchHeight, 9);
    }

    [Fact]
    public void The_configurator_previews_the_elevation_the_plan_and_the_parts()
    {
        var f = TwoStoreys();
        var stair = Stair(f);
        var o = (JsonObject)f.Session.Query("architecture.underStairPreview", new JsonObject { ["stairId"] = stair, ["style"] = "mudroom" })!;
        Assert.Null(o["problem"]);
        Assert.True(((JsonArray)o["elevation"]!).Count > 5);
        Assert.True(((JsonArray)o["plan"]!).Count > 5);
        Assert.Contains(((JsonArray)o["parts"]!).OfType<JsonObject>(), p => (string?)p["key"] == "nook-hooks" && (int)p["count"]! > 2);
        Assert.Equal("right", (string?)o["space"]!["side"]);
        // A schedule lists each space's parts with their material.
        Nook(f, stair, "mudroom");
        var grid = new UnderStairSchedule().Grid(f.Session.Reader);
        Assert.Contains(grid.Rows, r => r[3] == "Coat hooks");
        Assert.Contains(grid.Rows, r => r[0] == "Total" && r[5] == "Oak plywood");
    }

    [Fact]
    public void It_exports_as_an_assembly_of_furniture_connected_to_its_stair_and_comes_back()
    {
        var f = TwoStoreys();
        var stair = Stair(f);
        Nook(f, stair, "office");
        var path = Path.Combine(Path.GetTempPath(), $"lapin-nook-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            var asm = Assert.Single(step.OfType("IFCELEMENTASSEMBLY"), x => Equals(x[4], "Under-stair space"));
            Assert.Contains(step.OfType("IFCFURNITURE"), x => Equals(x[8], new StepEnum("DESK")));
            Assert.Contains(step.OfType("IFCFURNITURE"), x => Equals(x[8], new StepEnum("SHELF")));
            Assert.NotEmpty(step.OfType("IFCDOOR"));
            Assert.Single(step.OfType("IFCLIGHTFIXTURE"));
            Assert.Contains(step.OfType("IFCRELAGGREGATES"), r => Equals(r[4], new StepRef(asm.Id)));
            Assert.Contains(step.OfType("IFCRELCONNECTSELEMENTS"), r => Equals(r[2], "Under stair") && Equals(r[6], new StepRef(asm.Id)));
            var b = TwoStoreys();
            IfcImport.Run(b.Session, path, null, out _);
            Assert.Contains(b.Model.Entities, e => e.Class == UnderStairs.Class);
        }
        finally { File.Delete(path); }
    }
}
