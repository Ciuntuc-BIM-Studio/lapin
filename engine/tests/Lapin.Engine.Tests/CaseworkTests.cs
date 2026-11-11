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
/// Casework (docs/plan-furniture.md, Configuratorul flexibil, stage 1; decision 475): furniture made of boards, one or
/// more carcasses side by side, each with its zones stacked (fronts: open, doors, drawers by ratio, a flap; interior:
/// shelves, a rail, inner drawers), on a plinth, legs or hung on the wall, with or without a top, handles or none; the
/// presets and the modern styles; the cut list; the materials by role; ids kept on edits; IFC there and back; the
/// furniture made before unchanged.
/// </summary>
public sealed class CaseworkTests
{
    static Fixture F() => new(new ArchitectureModule(), new FurnitureModule());

    static string Type(Fixture f, JsonObject? spec = null, string? preset = null)
    {
        var a = new JsonObject { ["kind"] = "casework" };
        if (preset != null) a["preset"] = preset;
        if (spec != null) a["spec"] = spec.DeepClone();
        return (string)f.Run("furniture.createType", a)!["id"]!;
    }

    static string Place(Fixture f, string type, double x = 0) =>
        (string)f.Run("furniture.place", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = x, ["y"] = 0, ["against"] = false })!["id"]!;

    static Builder Built(Fixture f, string id)
    {
        var e = f.Model.Get(id)!;
        var s = new Spec(FurnitureModule.TypeOf(f.Model, e)!.Attrs["spec"] as JsonObject);
        var b = new Builder(new Vec2(0, 0), 0, 0, e.Bool("mirror"));
        FurnitureBodies.Build(b, s);
        return b;
    }

    static JsonObject Z(string front, params (string Key, JsonNode? Value)[] more)
    {
        var o = new JsonObject { ["front"] = front };
        foreach (var (k, v) in more) o[k] = v;
        return o;
    }

    static JsonObject C(params JsonObject[] zones) => new() { ["zones"] = new JsonArray([.. zones]) };

    /// <summary>A sideboard of two carcasses 600 wide on a plinth: a door with a shelf, three drawers; 18 mm boards, an 8 mm back set in.</summary>
    static JsonObject KnownSideboard() => new()
    {
        ["width"] = 1.2, ["depth"] = 0.45, ["height"] = 0.8,
        ["base"] = "plinth", ["plinthHeight"] = 0.1, ["plinthRecess"] = 0.05, ["top"] = "none", ["topOn"] = "between", ["back"] = "inset",
        ["carcassBoard"] = 0.018, ["backBoard"] = 0.008, ["shelfBoard"] = 0.018, ["frontBoard"] = 0.018, ["handles"] = "none", ["frontStyle"] = "slab",
        ["carcasses"] = new JsonArray(
            C(Z("doors", ("doors", 1), ("hand", "left"), ("shelves", 1), ("adjustable", true))),
            C(Z("drawers", ("drawers", 3)))),
    };

    static (Vec3 Min, Vec3 Max) Bounds(ElementGeometry g)
    {
        var all = g.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        return (new Vec3(all.Min(b => b.Min.X), all.Min(b => b.Min.Y), all.Min(b => b.Min.Z)), new Vec3(all.Max(b => b.Max.X), all.Max(b => b.Max.Y), all.Max(b => b.Max.Z)));
    }

    [Fact]
    public void Every_preset_builds_closed_boards_of_its_size_with_a_plan_symbol()
    {
        var f = F();
        var x = 0.0;
        foreach (var p in Casework.Presets)
        {
            var type = Type(f, preset: p.Key);
            var id = Place(f, type, x);
            var g = FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!;
            Assert.NotEmpty(g.Parts);
            Assert.All(g.Parts, part => Assert.True(Measure.Volume(part.Solid) > 0, $"{p.Key}: {part.Name} is closed"));
            Assert.All(g.Parts, part => Assert.NotNull(part.Material));
            Assert.Null(g.Diagnostics);
            Assert.NotEmpty(g.Plan!);
            Assert.True(g.Parts.Sum(part => part.Solid.Faces.Length) < 8000, $"{p.Key}: low-poly");
            var s = new Spec(f.Model.Get(type)!.Attrs["spec"] as JsonObject);
            var (min, max) = Bounds(g);
            // As wide as its type, within a top's overhang either side.
            Assert.InRange(max.X - min.X, s.W - 0.002, s.W + 0.05);
            // Its height over where it stands: the floor, or the wall it hangs on at its height.
            var bottom = s.Str("base") == "floating" ? s.Num("mount", 0) : 0;
            Assert.InRange(min.Z, bottom - 0.001, bottom + 0.001);
            Assert.InRange(max.Z, bottom + s.H - 0.01, bottom + s.H + 0.3);
            // The cut list has its boards.
            Assert.NotEmpty(Built(f, id).Boards);
            x += 4;
        }
        // The type's name says what it is.
        Assert.Contains(f.Model.OfClass(FurnitureModule.TypeClass), t => t.Name.StartsWith("Wardrobe", StringComparison.Ordinal));
    }

    [Fact]
    public void A_known_sideboard_s_cut_list_has_its_boards_and_their_sizes()
    {
        var f = F();
        var id = Place(f, Type(f, KnownSideboard()));
        var b = Built(f, id);
        int Count(string part, int carcass) => b.Boards.Count(x => x.Part == part && x.Carcass == carcass);
        (int L, int W, int T) Size(string part, int carcass = 1)
        {
            var x = b.Boards.First(r => r.Part == part && (carcass == 0 || r.Carcass == carcass));
            return ((int)Math.Round(x.Length * 1000), (int)Math.Round(x.Width * 1000), (int)Math.Round(x.Thickness * 1000));
        }
        foreach (var c in new[] { 1, 2 })
        {
            Assert.Equal(2, Count("Side", c));
            Assert.Equal(1, Count("Bottom", c));
            Assert.Equal(1, Count("Top", c));
            Assert.Equal(1, Count("Back", c));
        }
        // The body 700 high over the 100 plinth, its boards 450 − 18 (the fronts) deep; the top and bottom between the sides.
        Assert.Equal((700, 432, 18), Size("Side"));
        Assert.Equal((564, 432, 18), Size("Bottom"));
        Assert.Equal((564, 432, 18), Size("Top"));
        // The back set in between the sides, the top and the bottom.
        Assert.Equal((664, 564, 8), Size("Back"));
        // An adjustable shelf: 1 mm clear of each side, 20 mm back from the front, in front of the back.
        Assert.Equal((562, 394, 18), Size("Shelf (adjustable)"));
        // The plinth along the run, front and back.
        Assert.Equal(2, b.Boards.Count(x => x.Part == "Plinth"));
        Assert.Equal((1200, 100, 18), Size("Plinth", 0));
        // Fronts apart: the door (grain up) and three drawer fronts (grain across), each 3 mm short of its neighbours.
        Assert.All(b.Boards.Where(x => x.Part is "Door" or "Drawer front"), x => Assert.True(x.Front));
        Assert.Equal((697, 597, 18), Size("Door"));
        Assert.Equal(3, Count("Drawer front", 2));
        Assert.Equal((597, 230, 18), Size("Drawer front", 2));
        // Each drawer a box: two sides, two ends and a bottom; 400 deep (the runners' 50 mm steps).
        Assert.Equal(6, Count("Drawer side", 2));
        Assert.Equal(400, (int)Math.Round(b.Boards.First(x => x.Part == "Drawer side").Length * 1000));
        Assert.Equal(3, Count("Drawer bottom", 2));
        Assert.All(b.Boards.Where(x => !x.Front), x => Assert.NotEqual("fronts", x.Role));

        // The schedule: a row per part and size, the fronts after the boards, counts by the pieces of the type.
        Place(f, FurnitureModule.TypeOf(f.Model, f.Model.Get(id)!)!.Id, 3);
        var grid = new CutListSchedule().Grid(f.Session.Reader);
        Assert.Equal(["Type", "Group", "Part", "Carcass", "Length (mm)", "Width (mm)", "Thickness (mm)", "Material", "Count"], grid.Columns);
        var side = grid.Rows.First(r => r[2] == "Side");
        Assert.Equal(["Boards", "Side", "1", "700", "432", "18", "Oak veneer", "4"], side.Skip(1));
        var groups = grid.Rows.Select(r => r[1]).Where(g => g.Length > 0).ToList();
        Assert.True(groups.LastIndexOf("Boards") < groups.IndexOf("Fronts"));
        Assert.Contains(grid.Rows, r => r[2] == "Drawer front" && r[^1] == "6");
    }

    [Fact]
    public void A_carcass_stacks_its_zones_drawers_under_doors_with_a_divider_between()
    {
        var f = F();
        var spec = KnownSideboard();
        spec["width"] = 0.6;
        spec["height"] = 1.6;
        spec["carcasses"] = new JsonArray(C(Z("drawers", ("drawers", 2), ("height", 0.4), ("ratios", new JsonArray(1, 3))), Z("doors", ("doors", 2), ("shelves", 2))));
        var b = Built(f, Place(f, Type(f, spec)));
        var drawers = b.Boards.Where(x => x.Part == "Drawer front").ToList();
        Assert.Equal(2, drawers.Count);
        // By their ratios, 1 : 3 of the 400 (each 3 mm short): the top one the shallow one.
        Assert.Equal([0.097, 0.297], drawers.Select(x => Math.Round(x.Width, 3)).Order());
        var doors = b.Front.Where(p => p.Role == "fronts" && p.Z0 > 0.5).ToList();
        Assert.Equal(2, doors.Count);
        Assert.All(doors, d => Assert.Equal(0.1 + 0.4 + 0.0015, d.Z0, 4));
        Assert.All(doors, d => Assert.Equal(1.6 - 0.0015, d.Z1, 4));
        Assert.Single(b.Boards, x => x.Part == "Divider");
        Assert.Equal(2, b.Boards.Count(x => x.Part == "Shelf (adjustable)"));
    }

    [Fact]
    public void Legs_stand_it_up_and_a_floating_one_hangs_at_its_height()
    {
        var f = F();
        var legs = KnownSideboard();
        legs["base"] = "legs";
        legs["legs"] = "metal";
        legs["legHeight"] = 0.15;
        var gl = FurnitureModule.Geometry(f.Model.Get(Place(f, Type(f, legs)))!, f.Model)!;
        var lp = gl.Parts.Single(p => p.Name == "Legs");
        Assert.Equal(0, Measure.Bounds(lp.Solid)!.Value.Min.Z, 4);
        Assert.Equal(0.15, Measure.Bounds(lp.Solid)!.Value.Max.Z, 4);
        Assert.Equal(0.15, Measure.Bounds(gl.Parts.Single(p => p.Name == "Body").Solid)!.Value.Min.Z, 4);
        Assert.Equal(0.8, Bounds(gl).Max.Z, 4);
        Assert.DoesNotContain(gl.Parts, p => p.Name == "Plinth");

        var floating = KnownSideboard();
        floating["base"] = "floating";
        floating["mount"] = 0.3;
        floating["height"] = 0.45;
        var id = Place(f, Type(f, floating), 4);
        var gf = FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!;
        // Its underside at 300, its top 450 over it; nothing under it.
        Assert.Equal(0.3, Bounds(gf).Min.Z, 4);
        Assert.Equal(0.75, Bounds(gf).Max.Z, 4);
        Assert.DoesNotContain(gf.Parts, p => p.Name is "Legs" or "Plinth");
        Assert.Equal(0.45, Built(f, id).Boards.First(x => x.Part == "Side").Length, 4);
    }

    [Theory]
    [InlineData("none", 0)]
    [InlineData("jprofile", 0)]
    [InlineData("bar", 4)]
    [InlineData("knob", 4)]
    [InlineData("edge", 4)]
    public void Handleless_fronts_have_no_handles_and_the_others_one_each(string handles, int count)
    {
        var f = F();
        var spec = KnownSideboard();
        spec["handles"] = handles;
        var b = Built(f, Place(f, Type(f, spec)));
        Assert.Equal(count, b.Solids.GetValueOrDefault("handles")?.Count ?? 0);
        // A J-profile is milled into the fronts: each front two solids, its lip set back.
        if (handles == "jprofile") Assert.Equal(8, b.Solids["fronts"].Count);
    }

    [Fact]
    public void Fluted_and_framed_fronts_are_geometry_and_the_cut_list_says_what_they_are()
    {
        var f = F();
        var slab = Built(f, Place(f, Type(f, KnownSideboard())));
        var fluted = KnownSideboard();
        fluted["frontStyle"] = "fluted";
        var fb = Built(f, Place(f, Type(f, fluted), 3));
        Assert.True(fb.Solids["fronts"].Sum(s => s.Faces.Length) > 3 * slab.Solids["fronts"].Sum(s => s.Faces.Length), "the flutes are grooves in the fronts");
        Assert.Equal(slab.Boards.Count(x => x.Front), fb.Boards.Count(x => x.Front));
        var framed = KnownSideboard();
        framed["frontStyle"] = "framed";
        var rb = Built(f, Place(f, Type(f, framed), 6));
        // The door a frame of two stiles and two rails round a panel set back; the drawer fronts (230 high) too.
        Assert.Equal(2, rb.Boards.Count(x => x.Part == "Door stile"));
        Assert.Equal(2, rb.Boards.Count(x => x.Part == "Door rail"));
        Assert.Single(rb.Boards, x => x.Part == "Door panel");
    }

    [Fact]
    public void The_materials_are_the_project_s_by_role()
    {
        var f = F();
        var spec = KnownSideboard();
        spec["handles"] = "bar";
        spec["top"] = "overhang";
        spec["base"] = "legs";
        spec["carcasses"]![0]!["zones"]![0]!["glazed"] = true;
        spec["materials"] = new JsonObject
        {
            ["body"] = "Walnut veneer", ["fronts"] = "Lacquered MDF sage", ["top"] = "Oak solid wood", ["legs"] = "Black powder-coated steel",
            ["handles"] = "Brass", ["back"] = "HDF white", ["glass"] = "Clear glass",
        };
        var g = FurnitureModule.Geometry(f.Model.Get(Place(f, Type(f, spec)))!, f.Model)!;
        foreach (var (part, material) in new[] { ("Body", "Walnut veneer"), ("Fronts", "Lacquered MDF sage"), ("Top", "Oak solid wood"), ("Legs", "Black powder-coated steel"), ("Handles", "Brass"), ("Back", "HDF white"), ("Glass", "Clear glass") })
            Assert.Equal(Materials.Find(f.Model, material)!.Id, g.Parts.Single(p => p.Name == part).Material);
    }

    [Fact]
    public void Carcasses_share_what_the_given_widths_leave_and_those_past_the_width_are_left_out_with_a_warning()
    {
        var f = F();
        var spec = KnownSideboard();
        spec["width"] = 1.8;
        spec["carcasses"] = new JsonArray(new JsonObject { ["width"] = 0.4, ["zones"] = new JsonArray(Z("doors")) }, C(Z("open")), C(Z("drawers")));
        var b = Built(f, Place(f, Type(f, spec)));
        Assert.Equal([0.4, 0.7, 0.7], b.Carcasses.Select(c => Math.Round(c.X1 - c.X0, 4)));
        Assert.Empty(b.Warnings);
        spec["carcasses"] = new JsonArray(new JsonObject { ["width"] = 1.0, ["zones"] = new JsonArray(Z("doors")) }, new JsonObject { ["width"] = 1.0, ["zones"] = new JsonArray(Z("doors")) });
        var over = Built(f, Place(f, Type(f, spec), 3));
        Assert.Single(over.Carcasses);
        Assert.NotEmpty(over.Warnings);
        // A desk: a pedestal and a knee space, a gable at the far end under the top.
        var desk = Built(f, Place(f, Type(f, preset: "desk"), 6));
        Assert.Single(desk.Boards, x => x.Part == "Gable");
        Assert.Contains(desk.Boards, x => x.Part == "Top" && x.Carcass == 0 && Math.Abs(x.Length - 1.4) < 1e-6);
    }

    [Fact]
    public void The_values_are_checked()
    {
        var f = F();
        Assert.Throws<CommandException>(() => Type(f, new JsonObject { ["carcasses"] = new JsonArray(C(Z("shutters"))) }));
        Assert.Throws<CommandException>(() => Type(f, new JsonObject { ["carcasses"] = new JsonArray(C(Z("drawers", ("drawers", 9)))) }));
        Assert.Throws<CommandException>(() => Type(f, new JsonObject { ["carcasses"] = new JsonArray(new JsonObject { ["width"] = 0.02 }) }));
        Assert.Throws<CommandException>(() => Type(f, new JsonObject { ["carcasses"] = new JsonArray(C(Z("drawers", ("drawers", 2), ("ratios", new JsonArray(1, 2, 3))))) }));
        Assert.Throws<CommandException>(() => Type(f, new JsonObject { ["handles"] = "magnet" }));
        Assert.Throws<CommandException>(() => Type(f, preset: "spaceship"));
        Assert.Throws<CommandException>(() => f.Run("furniture.createType", new JsonObject { ["kind"] = "sofa", ["spec"] = new JsonObject { ["carcasses"] = new JsonArray() } }));
    }

    [Fact]
    public void Editing_keeps_the_piece_s_and_type_s_ids_in_one_undo_step_and_a_style_is_one_step_too()
    {
        var f = F();
        var type = Type(f, KnownSideboard());
        var id = Place(f, type);
        var before = Built(f, id).Boards.Count;
        f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["carcasses"] = new JsonArray(C(Z("doors")), C(Z("drawers")), C(Z("open", ("shelves", 3)))) } });
        Assert.NotNull(f.Model.Get(id));
        Assert.Equal(type, FurnitureModule.TypeOf(f.Model, f.Model.Get(id)!)!.Id);
        // The piece's id is its GlobalId: it is the same piece, rebuilt.
        Assert.Equal(3, Built(f, id).Carcasses.Count);
        f.Session.Undo();
        Assert.Equal(before, Built(f, id).Boards.Count);
        // A modern style is a patch on the type, in one step: fluted fronts.
        var style = Casework.Styles.Single(s => s.Key == "fluted").Patch;
        f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = style.DeepClone() });
        Assert.Equal("fluted", new Spec(f.Model.Get(type)!.Attrs["spec"] as JsonObject).Str("frontStyle"));
        Assert.Equal(id, f.Model.OfClass(FurnitureModule.Class).Single().Id);
        f.Session.Undo();
        Assert.Equal("slab", new Spec(f.Model.Get(type)!.Attrs["spec"] as JsonObject).Str("frontStyle"));
    }

    [Fact]
    public void A_preset_changed_on_the_type_changes_its_IFC_type_and_its_pieces()
    {
        var f = F();
        var type = Type(f, preset: "sideboard");
        var id = Place(f, type);
        Assert.Equal("USERDEFINED", f.Model.Get(id)!.PredefinedType);
        Assert.Equal("Sideboard", f.Model.Get(id)!.Str("objectType"));
        f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = Casework.Preset("bookcase") });
        Assert.Equal("SHELF", f.Model.Get(type)!.PredefinedType);
        Assert.Equal("SHELF", f.Model.Get(id)!.PredefinedType);
        Assert.Null(f.Model.Get(id)!.Attrs["objectType"]);
    }

    [Fact]
    public void Casework_goes_through_IFC_with_its_carcasses()
    {
        var f = F();
        Place(f, Type(f, preset: "vanity"));
        Place(f, Type(f, KnownSideboard()), 3);
        Place(f, Type(f, preset: "bookcase"), 6);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("'Bathroom vanity'", ifc);
        Assert.Contains(".SHELF.", ifc);
        var path = Path.Combine(Path.GetTempPath(), $"casework-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc);
        try
        {
            var g = F();
            IfcImport.Run(g.Session, path, null, out _);
            var pieces = g.Model.OfClass(FurnitureModule.Class).ToList();
            Assert.Equal(3, pieces.Count);
            var sideboard = pieces.Select(p => new Spec(FurnitureModule.TypeOf(g.Model, p)!.Attrs["spec"] as JsonObject)).Single(s => s.List("carcasses")?.Count == 2);
            Assert.Equal("drawers", (string?)sideboard.List("carcasses")![1]!["zones"]![0]!["front"]);
            Assert.All(pieces, p => Assert.NotEmpty(FurnitureModule.Geometry(p, g.Model)!.Parts));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_bed_has_its_frame_headboard_and_drawers_under_it()
    {
        var f = F();
        var type = Type(f, new JsonObject { ["bedDrawers"] = 2, ["drawersOn"] = "both" }, "bed");
        var b = Built(f, Place(f, type));
        Assert.Single(b.Boards, x => x.Part == "Headboard");
        Assert.Equal(4, b.Boards.Count(x => x.Part == "Drawer front"));
        Assert.Contains(b.Solids.Keys, k => k == "linen");
        var s = new Spec(f.Model.Get(type)!.Attrs["spec"] as JsonObject);
        Assert.Equal(s.H, b.Front.Max(p => p.Z1), 4);
        var ifc = FurnitureKinds.IfcOf(s);
        Assert.Equal("BED", ifc.Predefined);
    }

    [Fact]
    public void Sliding_doors_run_across_the_carcasses_on_two_tracks_and_the_wardrobes_made_before_are_as_they_were()
    {
        var f = F();
        var b = Built(f, Place(f, Type(f, preset: "wardrobeSliding")));
        var doors = b.Boards.Where(x => x.Part == "Sliding door").ToList();
        Assert.Equal(2, doors.Count);
        Assert.Contains(b.Plan, p => p.Kind == "symbol");
        // The wardrobe kind keeps its own body: three hinged doors, three handles; sliding, its doors on two tracks.
        var old = new Builder(new Vec2(0, 0), 0, 0, false);
        FurnitureBodies.Build(old, new Spec(FurnitureKinds.NewSpec(FurnitureKinds.Find("wardrobe")!, null)));
        Assert.Equal(3, old.Solids["fronts"].Count);
        Assert.Equal(3, old.Solids["metal"].Count);
        Assert.Empty(old.Boards);
        var sliding = new Builder(new Vec2(0, 0), 0, 0, false);
        FurnitureBodies.Build(sliding, new Spec(FurnitureKinds.NewSpec(FurnitureKinds.Find("wardrobe")!, new JsonObject { ["opening"] = "sliding" })));
        Assert.Equal(3, sliding.Solids["fronts"].Count);
        Assert.False(sliding.Solids.ContainsKey("metal"));
    }

    [Fact]
    public void The_preview_lists_the_boards_and_the_carcasses_as_laid()
    {
        var f = F();
        var r = f.Session.Query("furniture.preview", new JsonObject { ["kind"] = "casework", ["spec"] = KnownSideboard() })!;
        Assert.Equal(2, r["carcasses"]!.AsArray().Count);
        Assert.Contains(r["boards"]!.AsArray(), x => (string?)x!["part"] == "Door");
        Assert.Contains(r["plan"]!.AsArray(), x => (string?)x!["kind"] == "dashed");
    }
}
