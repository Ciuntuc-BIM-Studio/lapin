using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Furniture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Kitchen layouts (a tester's report #124): galley, L-shaped, U-shaped, island and peninsula besides the straight run.</summary>
public sealed class KitchenLayoutTests
{
    static Fixture F() => new(new ArchitectureModule(), new FurnitureModule());

    static string Kitchen(Fixture f, string layout, JsonObject? spec = null, double x = 0, double y = 0)
    {
        var type = (string)f.Run("furniture.createType", new JsonObject { ["kind"] = "kitchen", ["layout"] = layout, ["spec"] = spec ?? [] })!["id"]!;
        return (string)f.Run("furniture.place", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = x, ["y"] = y, ["against"] = false })!["id"]!;
    }

    static (Vec3 Min, Vec3 Max) Bounds(Fixture f, string id)
    {
        var g = FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!;
        var all = g.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        return (new Vec3(all.Min(b => b.Min.X), all.Min(b => b.Min.Y), all.Min(b => b.Min.Z)), new Vec3(all.Max(b => b.Max.X), all.Max(b => b.Max.Y), all.Max(b => b.Max.Z)));
    }

    static Spec SpecOf(Fixture f, string id) => new(FurnitureModule.TypeOf(f.Model, f.Model.Get(id)!)!.Attrs["spec"] as JsonObject);

    [Theory]
    [InlineData("one-wall", 3.0, 0.6)]
    [InlineData("galley", 3.0, 2.4)]
    [InlineData("l-shaped", 3.0, 2.4)]
    [InlineData("u-shaped", 3.0, 2.4)]
    [InlineData("island", 3.6, 2.6)]
    [InlineData("peninsula", 3.0, 2.4)]
    public void Each_layout_is_a_closed_body_in_its_footprint_with_a_sink_a_hob_and_a_tall_unit(string layout, double width, double depth)
    {
        var f = F();
        var id = Kitchen(f, layout);
        var spec = SpecOf(f, id);
        var (w, d) = FurnitureBodies.Footprint(spec, null);
        Assert.Equal(width, w, 6);
        Assert.Equal(depth, d, 6);
        var g = FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!;
        Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 0, $"{layout}: {p.Name} is closed"));
        Assert.Contains(g.Parts, p => p.Name == "Glass");
        Assert.Contains(g.Parts, p => p.Name == "Metal");
        // Inside the footprint (the tall unit's handle and a tap stand a little out), the tall unit as high as it is.
        var (min, max) = Bounds(f, id);
        Assert.InRange(min.X, -w / 2 - 0.03, -w / 2 + 0.01);
        Assert.InRange(max.X, w / 2 - 0.01, w / 2 + 0.03);
        Assert.InRange(min.Y, -d / 2 - 0.03, -d / 2 + 0.01);
        Assert.InRange(max.Y, d / 2 - 0.01, d / 2 + 0.01);
        Assert.InRange(max.Z, 2.1, 2.3);
        Assert.NotEmpty(g.Plan!);
        // The worktop's area grows with the legs and runs the layout has.
        var top = Measure.Volume(g.Parts.Single(p => p.Name == "Top").Solid);
        Assert.True(top > 0.04 * 0.6 * 0.5);
    }

    [Fact]
    public void The_layouts_have_the_runs_they_name()
    {
        var f = F();
        var plain = new JsonObject { ["tall"] = "none" };
        double Worktop(string layout, JsonObject? extra = null)
        {
            var spec = (JsonObject)plain.DeepClone();
            foreach (var (k, v) in extra ?? []) spec[k] = v?.DeepClone();
            return Measure.Volume(FurnitureModule.Geometry(f.Model.Get(Kitchen(f, layout, spec))!, f.Model)!.Parts.Single(p => p.Name == "Top").Solid);
        }
        var straight = Worktop("one-wall");
        // Two runs of the same length; the L has a leg more (2.4 m from the wall, less the run's depth); the U two; an island and a peninsula their own.
        Assert.InRange(Worktop("galley") / straight, 1.99, 2.01);
        Assert.InRange(Worktop("l-shaped") / straight, 1.55, 1.65);
        Assert.InRange(Worktop("u-shaped") / straight, 2.1, 2.2);
        Assert.InRange(Worktop("island", new JsonObject { ["width"] = 3.0 }) / straight, 1.85, 1.95);
        Assert.True(Worktop("peninsula") > Worktop("l-shaped"));
    }

    [Fact]
    public void An_L_with_its_corner_on_the_right_is_the_left_one_mirrored_and_the_hob_stays_where_it_was_asked()
    {
        var f = F();
        var spec = new JsonObject { ["hobOn"] = "run", ["hobAt"] = 0.95, ["tall"] = "none" };
        var left = Kitchen(f, "l-shaped", spec, 0, 0);
        var right = Kitchen(f, "l-shaped", new JsonObject { ["corner"] = "right", ["hobOn"] = "run", ["hobAt"] = 0.95, ["tall"] = "none" }, 20, 0);
        var (lmin, lmax) = Bounds(f, left);
        var (rmin, rmax) = Bounds(f, right);
        Assert.Equal(lmax.X - lmin.X, rmax.X - rmin.X, 4);
        // The leg is on the other side: the left L's front cabinets are at its low X, the right's at its high X.
        double Front(string id) => FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!.Parts.Single(p => p.Name == "Body").Solid.Vertices.Where(v => v.Y < Args.PointOf(f.Model.Get(id)!.Attrs["at"]).Y - 0.5 && v.Z < 0.5).Average(v => v.X - Args.PointOf(f.Model.Get(id)!.Attrs["at"]).X);
        Assert.True(Front(left) < -0.5 && Front(right) > 0.5);
        // 0.95 along from the left as seen from the front: near the right end for both, the corner side of the right-cornered L.
        double Hob(string id) => FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!.Parts.Single(p => p.Name == "Glass").Solid.Vertices.Average(v => v.X - Args.PointOf(f.Model.Get(id)!.Attrs["at"]).X);
        Assert.True(Hob(left) > 0.5 && Hob(right) > 0.5, $"{Hob(left)} {Hob(right)}");
    }

    [Fact]
    public void The_sink_and_the_hob_go_on_the_run_or_on_the_other_element()
    {
        var f = F();
        int Glass(string id) => FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!.Parts.Count(p => p.Name == "Glass");
        // Galley: the hob on the opposite run, the sink on the first: the glass is there, and nowhere in the back run.
        var galley = Kitchen(f, "galley");
        var g = FurnitureModule.Geometry(f.Model.Get(galley)!, f.Model)!;
        var glass = g.Parts.Single(p => p.Name == "Glass").Solid;
        var at = Args.PointOf(f.Model.Get(galley)!.Attrs["at"]);
        Assert.All(glass.Vertices, v => Assert.True(v.Y - at.Y < 0, "the hob is on the front run (the south one)"));
        var back = Kitchen(f, "galley", new JsonObject { ["hobOn"] = "run" }, 0, 10);
        var atBack = Args.PointOf(f.Model.Get(back)!.Attrs["at"]);
        Assert.All(FurnitureModule.Geometry(f.Model.Get(back)!, f.Model)!.Parts.Single(p => p.Name == "Glass").Solid.Vertices, v => Assert.True(v.Y - atBack.Y > 0));
        Assert.Equal(1, Glass(back));
    }

    [Fact]
    public void A_kitchen_drawn_along_a_wall_backs_onto_it_and_an_L_tucks_its_leg_into_the_corner()
    {
        var f = F();
        f.Wall(0, 6, 8, 6);
        f.Wall(0, 0, 0, 6);
        var face = Walls.Section(f.Model, f.Model.Get(f.Model.OfClass(IfcClass.Wall).First().Id)!).Min(b => b.From);
        var type = (string)f.Run("furniture.createType", new JsonObject { ["kind"] = "kitchen", ["layout"] = "l-shaped" })!["id"]!;
        // Drawn a little off the corner, 3 m: the leg's back lands on the side wall's face, the run's back on the north wall's.
        var k = (string)f.Run("furniture.kitchen", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = 0.4, ["y1"] = 5.5, ["x2"] = 3.4, ["y2"] = 5.5 })!["id"]!;
        var (min, max) = Bounds(f, k);
        Assert.InRange(6 + face - max.Y, 0, 0.005);
        var sideFace = Walls.Section(f.Model, f.Model.Get(f.Model.OfClass(IfcClass.Wall).Last().Id)!).Max(b => b.To);
        Assert.InRange(min.X - sideFace, -0.01, 0.01);
        Assert.InRange(max.Y - min.Y, 2.39, 2.43);
        Assert.Equal(3.0, f.Model.Get(k)!.Num("length"), 3);
    }

    [Fact]
    public void The_schedule_names_the_layout_and_the_footprints_depth_and_ifc_keeps_it_a_kitchen()
    {
        var f = F();
        Kitchen(f, "u-shaped");
        var grid = new FurnitureSchedule().Grid(f.Session.Reader);
        var row = grid.Rows.First(r => r[1].StartsWith("Kitchen", StringComparison.Ordinal));
        Assert.Equal("Kitchen · U-shaped", row[1]);
        Assert.Equal("3000 × 2400 × 900", row[2]);
        var path = Path.Combine(Path.GetTempPath(), $"kitchen-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var text = File.ReadAllText(path);
            Assert.Contains("IFCFURNITURE(", text);
            Assert.Contains("'Kitchen'", text);
            Assert.Contains("u-shaped", text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_layouts_values_are_checked_and_a_type_can_change_its_layout()
    {
        var f = F();
        var id = Kitchen(f, "one-wall");
        var type = FurnitureModule.TypeOf(f.Model, f.Model.Get(id)!)!.Id;
        Assert.ThrowsAny<Exception>(() => f.Run("furniture.createType", new JsonObject { ["kind"] = "kitchen", ["layout"] = "circular" }));
        Assert.ThrowsAny<Exception>(() => f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["layout"] = "circular" } }));
        Assert.ThrowsAny<Exception>(() => f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["leg"] = 20 } }));
        f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = KitchenLayouts.Preset("island") });
        var (w, d) = FurnitureBodies.Footprint(SpecOf(f, id), null);
        Assert.Equal((3.6, 2.6), (w, d));
        Assert.Equal(6, KitchenLayouts.Catalog().Count);
    }
}
