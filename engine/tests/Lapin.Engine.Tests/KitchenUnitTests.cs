using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Furniture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A kitchen's own units (a tester's report #76, decision 472): base units, tall units, appliances and fillers along the
/// main run, each its own width (cabinets 100–800 mm, appliances their sizes) and front; the wall line with units of
/// their own height; materials for the fronts, carcass, worktop, plinth and handles; the units schedule; kitchens made
/// before built as they were.
/// </summary>
public sealed class KitchenUnitTests
{
    static Fixture F() => new(new ArchitectureModule(), new FurnitureModule());

    static JsonObject U(string type, double width, params (string Key, JsonNode? Value)[] more)
    {
        var o = new JsonObject { ["type"] = type, ["width"] = width };
        foreach (var (k, v) in more) o[k] = v;
        return o;
    }

    static string Type(Fixture f, JsonObject spec, string layout = "one-wall") =>
        (string)f.Run("furniture.createType", new JsonObject { ["kind"] = "kitchen", ["layout"] = layout, ["spec"] = spec })!["id"]!;

    static string Place(Fixture f, string type, double length, double x = 0) =>
        (string)f.Run("furniture.place", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = x, ["y"] = 0, ["against"] = false, ["length"] = length })!["id"]!;

    static Builder Built(Fixture f, string id)
    {
        var e = f.Model.Get(id)!;
        var s = new Spec(FurnitureModule.TypeOf(f.Model, e)!.Attrs["spec"] as JsonObject);
        var b = new Builder(new Vec2(0, 0), 0, 0, e.Bool("mirror") ^ FurnitureBodies.Handed(s));
        FurnitureBodies.Build(b, s, e.NumOrNull("length"));
        return b;
    }

    /// <summary>The fronts (doors, drawers, panels) standing at the base line's front plane, by their spans across and up.</summary>
    static List<(double X0, double X1, double Z0, double Z1)> Fronts(Builder b, string role = "fronts") =>
        [.. b.Front.Where(p => p.Role == role).Select(p => (p.X0, p.X1, p.Z0, p.Z1))];

    static JsonArray Mixed() =>
    [
        U("base", 0.4, ("front", "doors"), ("doors", 1), ("hand", "right")),
        U("sink", 0.8, ("sink", "one-half"), ("tap", "pull-out")),
        U("dishwasher", 0.45),
        U("hob", 0.6, ("fuel", "induction"), ("oven", true)),
        U("base", 0.6, ("front", "drawers"), ("drawers", 3)),
        U("fridge", 0.6, ("fridge", "tall")),
    ];

    [Fact]
    public void A_run_of_mixed_units_and_appliances_builds_each_where_it_is_listed()
    {
        var f = F();
        var type = Type(f, new JsonObject { ["tall"] = "none", ["units"] = Mixed() });
        var id = Place(f, type, 3.45);
        var g = FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!;
        Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 0, $"{p.Name} is closed"));
        Assert.Null(g.Diagnostics);
        var b = Built(f, id);
        var rows = b.Units.Where(u => u.Line == "Base").ToList();
        Assert.Equal(["base", "sink", "dishwasher", "hob", "base", "fridge"], rows.Select(r => r.Type));
        Assert.Equal([0.4, 0.8, 0.45, 0.6, 0.6, 0.6], rows.Select(r => r.Width));
        // Laid from the left, each after the one before: the units' middles step by their widths.
        Assert.Equal(-3.45 / 2 + 0.2, rows[0].At, 4);
        Assert.Equal(-3.45 / 2 + 0.4 + 0.8 + 0.225, rows[2].At, 4);
        Assert.Equal("1 door, hinged right", rows[0].Detail);
        Assert.Equal("1½ bowl sink, pull-out tap", rows[1].Detail);
        Assert.Equal("3 drawers", rows[4].Detail);
        Assert.Equal(2.15, rows[5].Height, 4);
        // The oven's door and the hob are glass; the fridge is a tall unit with its own carcass to 2.15 m.
        Assert.Contains(g.Parts, p => p.Name == "Glass");
        Assert.Contains(b.Front, p => p.Role == "body" && Math.Abs(p.Z1 - 2.15) < 1e-6 && p.X0 > 1.1);
        // Three drawers on their unit, the oven's glass front 600 high under the hob.
        var drawers = Fronts(b).Where(p => p.X0 > -3.45 / 2 + 2.25 - 1e-3 && p.X1 < -3.45 / 2 + 2.85 + 1e-3 && p.Z1 < 1).ToList();
        Assert.Equal(3, drawers.Count);
        Assert.Contains(b.Front, p => p.Role == "glass" && Math.Abs(p.Z1 - p.Z0 - 0.6) < 0.01);
        // In plan, the dishwasher and the fridge are marked, and the base doors' swings drawn at the finest detail only.
        Assert.Contains(g.Plan!, p => p.Kind == "dashed" && p.MinDetail == PlanDetail.Fine);
        // A wall unit over each base unit but the tall one, a hood over the hob.
        var wall = b.Units.Where(u => u.Line == "Wall").ToList();
        Assert.Equal(["wall", "wall", "wall", "hood", "wall"], wall.Select(r => r.Type));
    }

    [Theory]
    [InlineData("base", 0.1, true)]
    [InlineData("base", 0.375, true)]
    [InlineData("base", 0.8, true)]
    [InlineData("base", 0.05, false)]
    [InlineData("base", 0.85, false)]
    [InlineData("wall", 0.25, true)]
    [InlineData("tall", 0.9, false)]
    [InlineData("dishwasher", 0.45, true)]
    [InlineData("dishwasher", 0.6, true)]
    [InlineData("dishwasher", 0.5, false)]
    [InlineData("hob", 0.9, true)]
    [InlineData("hob", 0.7, false)]
    [InlineData("cooker", 0.5, true)]
    public void Cabinets_take_any_width_from_100_to_800_mm_and_appliances_their_sizes(string type, double width, bool ok)
    {
        var f = F();
        var upper = type == "wall";
        var spec = new JsonObject { [upper ? "upper" : "units"] = new JsonArray(U(type, width)) };
        if (ok) Assert.NotNull(Type(f, spec));
        else Assert.Throws<CommandException>(() => Type(f, spec));
    }

    [Fact]
    public void The_fields_of_a_unit_are_checked()
    {
        var f = F();
        Assert.Throws<CommandException>(() => Type(f, new JsonObject { ["units"] = new JsonArray(U("base", 0.6, ("drawers", 6))) }));
        Assert.Throws<CommandException>(() => Type(f, new JsonObject { ["units"] = new JsonArray(U("sink", 0.6, ("sink", "double"))) }));
        Assert.Throws<CommandException>(() => Type(f, new JsonObject { ["units"] = new JsonArray(U("hob", 0.6, ("tap", "standard"))) }));
        Assert.Throws<CommandException>(() => Type(f, new JsonObject { ["units"] = new JsonArray(U("wall", 0.6)) }));
        Assert.Throws<CommandException>(() => f.Run("furniture.createType", new JsonObject { ["kind"] = "sofa", ["spec"] = new JsonObject { ["units"] = new JsonArray(U("base", 0.6)) } }));
    }

    [Fact]
    public void A_dishwasher_45_and_a_dishwasher_60_have_their_widths_and_a_full_height_front()
    {
        var f = F();
        foreach (var w in new[] { 0.45, 0.6 })
        {
            var id = Place(f, Type(f, new JsonObject { ["tall"] = "none", ["wallUnits"] = false, ["units"] = new JsonArray(U("dishwasher", w)) }), 0.6);
            var b = Built(f, id);
            // The dishwasher first; a kitchen is at least 600 long, the rest of it a unit of its own.
            var front = Fronts(b).OrderBy(p => p.X0).First();
            Assert.Equal(w - 0.003, front.X1 - front.X0, 4);
            // From the plinth to under the worktop: 100 + 3 to 900 − 40 − 3.
            Assert.Equal(0.103, front.Z0, 4);
            Assert.Equal(0.857, front.Z1, 4);
            Assert.Equal($"Integrated {(int)Math.Round(w * 1000)}", b.Units[0].Detail);
            Assert.Equal(w == 0.6 ? 1 : 2, b.Units.Count);
            Assert.Contains(FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!.Parts, p => p.Name == "Metal");
        }
    }

    [Fact]
    public void A_wall_unit_has_its_own_height_and_mounting_or_the_run_s()
    {
        var f = F();
        var spec = new JsonObject
        {
            ["tall"] = "none", ["wallHeight"] = 0.72, ["wallFrom"] = 1.45,
            ["units"] = new JsonArray(U("base", 0.6), U("base", 0.6), U("base", 0.6)),
            ["upper"] = new JsonArray(U("wall", 0.6, ("height", 0.9)), U("wall", 0.6), U("wall", 0.6, ("from", 1.6), ("height", 0.36), ("front", "glass"), ("doors", 1))),
        };
        var b = Built(f, Place(f, Type(f, spec), 1.8));
        var wall = Fronts(b).Where(p => p.Z0 > 1.0).OrderBy(p => p.X0).ToList();
        Assert.Equal(2, wall.Count);
        Assert.Equal(1.45 + 0.002, wall[0].Z0, 4);
        Assert.Equal(1.45 + 0.9 - 0.002, wall[0].Z1, 4);
        Assert.Equal(1.45 + 0.72 - 0.002, wall[1].Z1, 4);
        var glazed = Assert.Single(Fronts(b, "glass"));
        Assert.Equal(1.6 + 0.002, glazed.Z0, 4);
        Assert.Equal(1.96 - 0.002, glazed.Z1, 4);
        Assert.Equal([0.9, 0.72, 0.36], b.Units.Where(u => u.Line == "Wall").Select(u => u.Height));
    }

    [Fact]
    public void Wall_units_go_round_a_tall_unit_and_what_is_left_of_the_run_is_split_as_before()
    {
        var f = F();
        var spec = new JsonObject
        {
            ["tall"] = "none", ["unit"] = 0.6,
            ["units"] = new JsonArray(U("tall", 0.6, ("height", 2.3)), U("base", 0.6)),
            ["upper"] = new JsonArray(U("wall", 0.6), U("wall", 0.6)),
        };
        var b = Built(f, Place(f, Type(f, spec), 3.0));
        var rows = b.Units.Where(u => u.Line == "Base").ToList();
        // The tall unit, the base unit, then the rest (1.8 m) as three units of 600.
        Assert.Equal(5, rows.Count);
        Assert.All(rows.Skip(2), r => Assert.Equal(0.6, r.Width, 4));
        Assert.Equal(2.3, rows[0].Height, 4);
        var wall = b.Units.Where(u => u.Line == "Wall").ToList();
        // The first wall unit is slid past the tall unit, over the base unit.
        Assert.Equal(rows[1].At, wall[0].At, 4);
        Assert.Equal(rows[2].At, wall[1].At, 4);
    }

    [Fact]
    public void Units_past_the_run_s_end_are_left_out_with_a_warning_and_Fit_to_units_lengthens_it()
    {
        var f = F();
        var type = Type(f, new JsonObject { ["tall"] = "right", ["units"] = Mixed() });
        var id = Place(f, type, 3.0);
        var g = FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!;
        Assert.NotNull(g.Diagnostics);
        Assert.Contains("left out", g.Diagnostics![0]);
        f.Run("furniture.fitKitchen", new JsonObject { ["ids"] = new JsonArray(id) });
        // The units' 3.45 m and the tall unit at its end.
        Assert.Equal(4.05, f.Model.Get(id)!.Num("length"), 4);
        Assert.Null(FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!.Diagnostics);
    }

    [Fact]
    public void The_materials_of_the_fronts_carcass_worktop_plinth_and_handles_are_the_project_s()
    {
        var f = F();
        var type = Type(f, new JsonObject { ["units"] = Mixed() });
        f.Run("furniture.updateType", new JsonObject
        {
            ["id"] = type,
            ["spec"] = new JsonObject { ["materials"] = new JsonObject { ["fronts"] = "Oak fronts", ["body"] = "Carcass white", ["top"] = "Granite", ["plinth"] = "Steel plinth", ["handles"] = "Black handles" } },
        });
        var id = Place(f, type, 4.05);
        var g = FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!;
        foreach (var (part, material) in new[] { ("Fronts", "Oak fronts"), ("Body", "Carcass white"), ("Top", "Granite"), ("Plinth", "Steel plinth"), ("Handles", "Black handles") })
        {
            var p = g.Parts.Single(x => x.Name == part);
            Assert.Equal(Materials.Find(f.Model, material)!.Id, p.Material);
        }
    }

    [Fact]
    public void Editing_the_units_keeps_the_kitchen_s_and_its_type_s_ids_in_one_undo_step()
    {
        var f = F();
        var type = Type(f, new JsonObject { ["units"] = new JsonArray(U("base", 0.6)) });
        var id = Place(f, type, 3.0);
        var before = Built(f, id).Units.Count;
        f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["units"] = new JsonArray(U("base", 0.6), U("dishwasher", 0.45), U("sink", 0.8)) } });
        Assert.NotNull(f.Model.Get(id));
        Assert.Equal(type, FurnitureModule.TypeOf(f.Model, f.Model.Get(id)!)!.Id);
        Assert.Contains(Built(f, id).Units, u => u.Type == "dishwasher");
        f.Session.Undo();
        Assert.Equal(before, Built(f, id).Units.Count);
        Assert.DoesNotContain(Built(f, id).Units, u => u.Type == "dishwasher");
        // Null takes the units out: the run split evenly again.
        f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["units"] = null } });
        Assert.Null((f.Model.Get(type)!.Attrs["spec"] as JsonObject)!["units"]);
    }

    [Fact]
    public void A_kitchen_made_before_its_units_is_built_as_it_was()
    {
        // A spec as kitchens were saved before decision 472: no units, no plinth or handles material.
        var old = JsonNode.Parse("""
            {"kind":"kitchen","width":3.0,"depth":0.6,"height":0.9,"layout":"one-wall","worktop":0.04,"plinth":0.1,"unit":0.6,"sinkOn":"run","sinkAt":0.3,"hobOn":"run","hobAt":0.72,
             "wallUnits":true,"wallFrom":1.45,"wallHeight":0.72,"wallDepth":0.35,"tall":"right",
             "materials":{"body":"Oak veneer","fronts":"Lacquered MDF white","top":"Oak solid wood","metal":"Brushed steel","glass":"Black glass"}}
            """)!.AsObject();
        var s = new Spec(old);
        var b = new Builder(new Vec2(0, 0), 0, 0, false);
        FurnitureBodies.Build(b, s, 3.0);
        // Its parts' roles as they were: the plinth in the carcass, the handles in the metal.
        Assert.Equal(["body", "fronts", "glass", "metal", "top"], b.Solids.Keys.Order());
        // 2.4 m of base units at 600: four, each a drawer over a door; four wall units; the tall unit's two doors.
        Assert.Equal(4 * 2 + 4 + 2, b.Solids["fronts"].Count);
        // Metal: the four drawers' handles, the sink and its tap, the tall unit's handle.
        Assert.Equal(4 + 2 + 1, b.Solids["metal"].Count);
        Assert.Single(b.Solids["top"]);
        // Its schedule rows say what it has.
        Assert.Equal(5, b.Units.Count(u => u.Line == "Base"));
        Assert.All(b.Units.Where(u => u.Line == "Base").Take(4), u => Assert.Equal("Drawer over a door", u.Detail));
    }

    [Fact]
    public void An_L_cornered_on_the_right_lays_its_units_from_the_left_as_listed_and_a_corner_sink_reaches_into_the_corner()
    {
        var f = F();
        var units = new JsonArray(U("dishwasher", 0.6), U("base", 0.4, ("front", "doors")));
        var left = Built(f, Place(f, Type(f, new JsonObject { ["tall"] = "none", ["units"] = units.DeepClone() }, "l-shaped"), 3.0));
        var right = Built(f, Place(f, Type(f, new JsonObject { ["tall"] = "none", ["corner"] = "right", ["units"] = units.DeepClone() }, "l-shaped"), 3.0, 10));
        string[] Order(Builder b) => [.. b.Units.Where(u => u.Line == "Base").Select(u => u.Type)];
        // The left L: the corner, then the units, then the rest; the right L: the rest, the units, then the corner.
        Assert.Equal(["dishwasher", "base"], Order(left).Take(2));
        Assert.Equal(["dishwasher", "base"], Order(right).Skip(Order(right).Length - 2));
        var lr = left.Units.Where(u => u.Line == "Base").ToList();
        Assert.True(lr[0].At < lr[1].At);
        // A single door hinged on its left as listed stays so when the L is built mirrored.
        Assert.Equal("1 door, hinged left", right.Units.Single(u => u.Type == "base" && u.Width == 0.4).Detail);
        // The corner sink's plate starts in the blind corner square (600 deep) before it.
        var sink = Built(f, Place(f, Type(f, new JsonObject { ["tall"] = "none", ["units"] = new JsonArray(U("sink", 0.6, ("sink", "corner"))) }, "l-shaped"), 3.0, 20));
        var plate = sink.Front.Where(p => p.Role == "metal" && p.Z0 >= 0.9 - 1e-6 && p.Z1 < 0.91).OrderBy(p => p.X0).First();
        Assert.True(plate.X1 - plate.X0 > 0.9, $"{plate.X1 - plate.X0}");
    }

    [Fact]
    public void The_kitchen_units_schedule_lists_each_kitchen_s_units()
    {
        var f = F();
        var type = Type(f, new JsonObject { ["tall"] = "none", ["units"] = Mixed() });
        Place(f, type, 3.45);
        var grid = new KitchenUnitsSchedule().Grid(f.Session.Reader);
        Assert.Equal(["Kitchen", "Line", "No.", "Unit", "Width (mm)", "Height (mm)", "Front / appliance"], grid.Columns);
        Assert.Equal(11, grid.Rows.Count);
        Assert.Equal(["", "Base", "3", "Dishwasher", "450", "900", "Integrated 450"], grid.Rows[2]);
        Assert.Equal("Fridge", grid.Rows[5][3]);
        Assert.Equal("Hood", grid.Rows[9][3]);
    }

    [Fact]
    public void The_preview_is_as_long_as_the_units_need_and_lists_them()
    {
        var f = F();
        var r = f.Session.Query("furniture.preview", new JsonObject { ["kind"] = "kitchen", ["spec"] = new JsonObject { ["tall"] = "right", ["units"] = Mixed() } })!;
        Assert.Equal(4.05, (double)r["width"]!, 4);
        Assert.Equal(4.05, (double)r["unitsLength"]!, 4);
        Assert.Equal(6 + 1 + 5, r["units"]!.AsArray().Count);
        Assert.Empty(r["warnings"]!.AsArray());
    }

    [Fact]
    public void Units_go_through_IFC_with_their_type()
    {
        var f = F();
        Place(f, Type(f, new JsonObject { ["units"] = Mixed() }), 4.05);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        var path = Path.Combine(Path.GetTempPath(), $"kitchen-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, Encoding.UTF8.GetString(ms.ToArray()));
        try
        {
            var g = F();
            IfcImport.Run(g.Session, path, null, out _);
            var piece = g.Model.OfClass(FurnitureModule.Class).Single();
            var spec = new Spec(FurnitureModule.TypeOf(g.Model, piece)!.Attrs["spec"] as JsonObject);
            Assert.Equal(6, spec.List("units")!.Count);
            Assert.Equal(0.45, Json.Number(spec.List("units")![2]!["width"]));
        }
        finally { File.Delete(path); }
    }
}
