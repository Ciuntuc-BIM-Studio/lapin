using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage 2 of docs/plan-material-graphics.md (decision 680), representation by scale: a material's steps by scale (byScale)
/// lay their fields over its own where the view's scale lies in their range; a view style's small-scale cut (smallScaleCut)
/// draws cuts solid, as outlines or the core alone from a threshold on; a view overrides it; what sets none draws as before.
/// </summary>
public class HatchByScaleTests
{
    static string Material(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;

    static void Set(Fixture f, string material, string kind, JsonObject g) => f.Run("material.update", new JsonObject { ["id"] = material, [kind] = g });

    static Views.Rendered Render(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);

    static string Plan(Fixture f, double scale) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = scale })!["id"]!;

    /// <summary>A plastered concrete wall with an EPS layer (the concrete its core), every layer drawn in plans.</summary>
    static (Fixture F, string Wall) Wall()
    {
        var f = new Fixture();
        f.EveryLayerInPlans();
        var type = f.Type("Insulated", ("Gypsum plaster", 0.015, false), ("Concrete", 0.2, true), ("EPS insulation", 0.1, false));
        return (f, f.Wall(0, 0, 5, 0, type));
    }

    static List<Pen> Hatches(IEnumerable<Pen> pens) => pens.Where(p => p.Layer == PenLayer.Hatch && (p.Segments.Count > 0 || p.Loops.Count > 0)).ToList();
    static List<Pen> Fills(IEnumerable<Pen> pens) => pens.Where(p => p.Layer == PenLayer.Fill && p.Loops.Count > 0).ToList();
    static int CutSegments(IEnumerable<Pen> pens) => pens.Where(p => p.Layer == PenLayer.Cut).Sum(p => p.Segments.Count);

    static JsonArray ConcreteSteps() =>
    [
        new JsonObject { ["to"] = 50, ["hatchColor"] = "#ff0000" },
        new JsonObject { ["from"] = 100, ["to"] = 199, ["hatch"] = null, ["fill"] = "#b3b3b3" },
        new JsonObject { ["from"] = 200, ["hatch"] = "SOLID" },
    ];

    [Fact]
    public void The_first_step_whose_range_holds_the_view_s_scale_is_taken()
    {
        var (f, _) = Wall();
        var concrete = Material(f, "Concrete");
        Set(f, concrete, "section", new JsonObject { ["byScale"] = ConcreteSteps() });
        var mat = f.Model.Get(concrete);
        // 1:20 and 1:50 (to 50 holds both ends): the concrete's speckle in red.
        Assert.Equal("#ff0000", MaterialGraphics.Resolve(mat, "section", scale: 20).HatchColor);
        Assert.Equal("#ff0000", MaterialGraphics.Resolve(mat, "section", scale: 50).HatchColor);
        Assert.Equal("CONCRETE", MaterialGraphics.Resolve(mat, "section", scale: 50).Hatch);
        // 1:75 lies in no step: the material's own.
        var between = MaterialGraphics.Resolve(mat, "section", scale: 75);
        Assert.Null(between.HatchColor);
        Assert.Null(between.Step);
        // 1:100 to 1:199: no hatch, a grey fill.
        var grey = MaterialGraphics.Resolve(mat, "section", scale: 100);
        Assert.Null(grey.Hatch);
        Assert.Equal("#b3b3b3", grey.Fill);
        Assert.Equal(("section", 1), grey.Step);
        // From 1:200: solid.
        Assert.Equal("SOLID", MaterialGraphics.Resolve(mat, "section", scale: 500).Hatch);
        // No scale (an export): no step.
        Assert.Null(MaterialGraphics.Resolve(mat, "section").HatchColor);

        // The drawings follow, each at its own scale.
        Assert.Contains(Hatches(Render(f, Plan(f, 50)).Pens), p => p.Stroke == "#ff0000");
        var at100 = Render(f, Plan(f, 100)).Pens;
        Assert.Contains(Fills(at100), p => p.Fill == "#b3b3b3");
        Assert.DoesNotContain(Hatches(at100), p => p.Stroke == "#ff0000");
        // Steps in order: the first that holds wins, though a later one holds too.
        Set(f, concrete, "section", new JsonObject { ["byScale"] = new JsonArray(new JsonObject { ["from"] = 100, ["hatchColor"] = "#00ff00" }, new JsonObject { ["from"] = 200, ["hatchColor"] = "#0000ff" }) });
        Assert.Equal("#00ff00", MaterialGraphics.Resolve(f.Model.Get(concrete), "section", scale: 200).HatchColor);
    }

    [Fact]
    public void A_step_lays_its_fields_over_the_material_s_one_by_one_and_a_plan_takes_the_section_s_step_then_its_own()
    {
        var (f, _) = Wall();
        var concrete = Material(f, "Concrete");
        Set(f, concrete, "section", new JsonObject
        {
            ["hatchScale"] = 2, ["hatchAngle"] = 30, ["cutLine"] = new JsonObject { ["color"] = "#112233", ["weight"] = 0.7 },
            ["byScale"] = new JsonArray(new JsonObject { ["from"] = 100, ["hatchColor"] = "#ff0000", ["cutLine"] = new JsonObject { ["weight"] = 0.35 } }),
        });
        var g = MaterialGraphics.Resolve(f.Model.Get(concrete), "section", scale: 100);
        Assert.Equal("CONCRETE", g.Hatch);
        Assert.Equal(2, g.HatchScale);
        Assert.Equal(30, g.HatchAngle);
        Assert.Equal("#ff0000", g.HatchColor);
        // A line field by field too: the step's weight, the material's colour.
        Assert.Equal(new MaterialLine("#112233", 0.35), g.CutLine);
        // A plan: the section's step, then its own fields, then its own step.
        Set(f, concrete, "plan", new JsonObject { ["hatchAngle"] = 0, ["byScale"] = new JsonArray(new JsonObject { ["from"] = 200, ["hatchColor"] = "#0000ff" }) });
        var plan100 = MaterialGraphics.Resolve(f.Model.Get(concrete), "plan", scale: 100);
        Assert.Equal(("#ff0000", 0.0, 2.0), (plan100.HatchColor, plan100.HatchAngle, plan100.HatchScale));
        var plan200 = MaterialGraphics.Resolve(f.Model.Get(concrete), "plan", scale: 200);
        Assert.Equal("#0000ff", plan200.HatchColor);
        Assert.Equal(("plan", 0), plan200.Step);
    }

    [Fact]
    public void An_elevation_s_surface_hatch_takes_its_step_by_scale()
    {
        var f = new Fixture();
        f.Wall(0, 0, 8, 0, f.Type("Brick 40", ("Facing brick", 0.1, false), ("Concrete", 0.2, true), ("Facing brick", 0.1, false)), height: 3);
        var brick = Material(f, "Facing brick");
        Set(f, brick, "elevation", new JsonObject { ["byScale"] = new JsonArray(new JsonObject { ["from"] = 100, ["hatch"] = null, ["fill"] = "#cc6644" }) });
        string Elevation(double scale) => (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south", ["scale"] = scale })!["id"]!;
        var near = Render(f, Elevation(50)).Pens;
        Assert.Contains(near, p => p.Layer == SurfaceHatches.Layer && p.Segments.Count > 0);
        var far = Render(f, Elevation(100)).Pens;
        Assert.DoesNotContain(far, p => p.Layer == SurfaceHatches.Layer && p.Segments.Count > 0);
        Assert.Contains(far, p => p.Fill == "#cc6644");
    }

    [Fact]
    public void Material_update_checks_the_steps_list_returns_them_preview_takes_the_scale_and_undo_takes_them_back()
    {
        var (f, _) = Wall();
        var concrete = Material(f, "Concrete");
        var before = f.Model.Get(concrete)!.Attrs.ToJsonString();
        Set(f, concrete, "section", new JsonObject { ["byScale"] = ConcreteSteps() });
        var list = f.Session.Query("material.list", new JsonObject())!.AsArray().First(m => (string?)m!["id"] == concrete)!;
        Assert.Equal(3, list["section"]!["byScale"]!.AsArray().Count);
        Assert.Equal(3, list["own"]!["section"]!["byScale"]!.AsArray().Count);
        // The preview at 1:100 shows the grey step; at 1:20 the red one.
        var preview = f.Session.Query("material.preview", new JsonObject { ["id"] = concrete, ["kind"] = "section", ["scale"] = 100 })!;
        Assert.Equal("#b3b3b3", (string?)preview["resolved"]!["fill"]);
        Assert.Equal(1, (int?)preview["resolved"]!["step"]!["index"]);
        var near = f.Session.Query("material.preview", new JsonObject { ["id"] = concrete, ["kind"] = "section", ["scale"] = 20 })!;
        Assert.Equal("#ff0000", (string?)near["resolved"]!["hatchColor"]);
        // An unsaved draft's steps preview too.
        var draft = f.Session.Query("material.preview", new JsonObject { ["id"] = concrete, ["kind"] = "section", ["scale"] = 500,
            ["graphics"] = new JsonObject { ["byScale"] = new JsonArray(new JsonObject { ["from"] = 500, ["hatchColor"] = "#00aa00" }) } })!;
        Assert.Equal("#00aa00", (string?)draft["resolved"]!["hatchColor"]);

        void Bad(JsonNode steps) => Assert.Throws<CommandException>(() => Set(f, concrete, "section", new JsonObject { ["byScale"] = steps }));
        Bad(new JsonArray(new JsonObject { ["from"] = 200, ["to"] = 100, ["hatchColor"] = "#ff0000" }));
        Bad(new JsonArray(new JsonObject { ["from"] = 0, ["hatchColor"] = "#ff0000" }));
        Bad(new JsonArray(new JsonObject { ["hatchColor"] = "#ff0000" }));
        Bad(new JsonArray(new JsonObject { ["from"] = 100 }));
        Bad(new JsonArray(new JsonObject { ["from"] = 100, ["hatch"] = "NO_SUCH_PATTERN" }));
        Bad(new JsonArray(new JsonObject { ["from"] = 100, ["colour"] = "#ff0000" }));
        Bad(new JsonArray(new JsonObject { ["from"] = 100, ["byScale"] = new JsonArray() }));
        Bad(new JsonObject { ["from"] = 100 });
        // A step's null hatch is none, kept; another null is left out.
        Set(f, concrete, "section", new JsonObject { ["byScale"] = new JsonArray(new JsonObject { ["from"] = 100, ["hatch"] = null, ["hatchColor"] = null }) });
        var kept = f.Model.Get(concrete)!.Attrs["section"]!["byScale"]![0]!.AsObject();
        Assert.True(kept.ContainsKey("hatch"));
        Assert.False(kept.ContainsKey("hatchColor"));
        // Null takes them off, as reset does; an empty list is none.
        Set(f, concrete, "section", new JsonObject { ["byScale"] = null });
        Assert.Equal(before, f.Model.Get(concrete)!.Attrs.ToJsonString());
        Set(f, concrete, "section", new JsonObject { ["byScale"] = ConcreteSteps() });
        Set(f, concrete, "section", new JsonObject { ["reset"] = new JsonArray("byScale") });
        Assert.Equal(before, f.Model.Get(concrete)!.Attrs.ToJsonString());
        Set(f, concrete, "section", new JsonObject { ["byScale"] = new JsonArray() });
        Assert.Equal(before, f.Model.Get(concrete)!.Attrs.ToJsonString());
        // One undo step.
        Set(f, concrete, "section", new JsonObject { ["byScale"] = ConcreteSteps() });
        Assert.NotNull(f.Model.Get(concrete)!.Attrs["section"]!["byScale"]);
        f.Session.Undo();
        Assert.Equal(before, f.Model.Get(concrete)!.Attrs.ToJsonString());
    }

    [Fact]
    public void A_material_without_steps_and_a_style_without_a_small_scale_cut_draw_exactly_as_before()
    {
        var (f, _) = Wall();
        foreach (var mat in f.Model.OfClass(IfcClass.Material))
            foreach (var kind in MaterialGraphics.Kinds)
                Assert.Equal(MaterialGraphics.Resolve(mat, kind).ToJson().ToJsonString(), MaterialGraphics.Resolve(mat, kind, scale: 500).ToJson().ToJsonString());
        // The styles say nothing new, and asMaterial set draws the same.
        Assert.DoesNotContain("smallScaleCut", f.Session.Query("viewStyle.list", new JsonObject())!.ToJsonString());
        var plan = Plan(f, 200);
        var before = MaterialGraphicsReferenceTests.Snapshot(Render(f, plan).Pens);
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["smallScaleCut"] = new JsonObject { ["mode"] = "asMaterial", ["from"] = 100 } });
        Assert.Equal(before, MaterialGraphicsReferenceTests.Snapshot(Render(f, plan).Pens));
    }

    [Fact]
    public void Solid_fills_each_element_one_area_from_its_threshold_on()
    {
        var (f, _) = Wall();
        var at100 = Plan(f, 100);
        var at200 = Plan(f, 200);
        var before100 = MaterialGraphicsReferenceTests.Snapshot(Render(f, at100).Pens);
        var before200 = Render(f, at200).Pens;
        Assert.NotEmpty(Hatches(before200));
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["smallScaleCut"] = new JsonObject { ["mode"] = "solid", ["from"] = 200 } });
        // Above its threshold (1:100): as before.
        Assert.Equal(before100, MaterialGraphicsReferenceTests.Snapshot(Render(f, at100).Pens));
        // At it: no hatch, the bands filled in ink, no lines between the layers (the outline alone: 4 sides).
        var pens = Render(f, at200).Pens;
        Assert.Empty(Hatches(pens));
        Assert.Equal("#000000", Assert.Single(Fills(pens)).Fill);
        Assert.Equal(4, CutSegments(pens));
        Assert.True(CutSegments(before200) > 4);
        // Its own colour; a smaller scale holds too.
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["smallScaleCut"] = new JsonObject { ["color"] = "#444444" } });
        Assert.Equal("#444444", Assert.Single(Fills(Render(f, Plan(f, 500)).Pens)).Fill);
        // The category's fill colour lays itself over it, as Visibility / Graphics over any material.
        Assert.Equal(new SmallScaleCut("solid", 200, "#444444"), ViewStyles.Of(f.Model, Views.Find(f.Model, at200)!).SmallScaleCut);
    }

    [Fact]
    public void Outline_draws_no_hatch_and_no_fill_and_core_only_fills_the_core()
    {
        var (f, _) = Wall();
        var plan = Plan(f, 200);
        var asMaterial = Render(f, plan).Pens;
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["smallScaleCut"] = new JsonObject { ["mode"] = "outline", ["from"] = 200 } });
        var outline = Render(f, plan).Pens;
        Assert.Empty(Hatches(outline));
        Assert.Empty(Fills(outline));
        Assert.Equal(4, CutSegments(outline));

        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["smallScaleCut"] = new JsonObject { ["mode"] = "coreOnly" } });
        var core = Render(f, plan).Pens;
        Assert.Empty(Hatches(core));
        // The concrete alone filled (one band), the plaster and the EPS empty, every line kept.
        var fill = Assert.Single(Fills(core));
        Assert.Equal("#000000", fill.Fill);
        Assert.Single(fill.Loops);
        Assert.Equal(CutSegments(asMaterial), CutSegments(core));
        // The concrete's band: 5 m by 0.2 m.
        var xs = fill.Loops[0].Select(p => p.Y).ToList();
        Assert.Equal(0.2, xs.Max() - xs.Min(), 6);
    }

    [Fact]
    public void A_view_overrides_its_style_s_small_scale_cut_field_by_field_and_null_takes_the_style_s_back()
    {
        var (f, _) = Wall();
        var plan = Plan(f, 200);
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["smallScaleCut"] = new JsonObject { ["mode"] = "solid", ["from"] = 200 } });
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["smallScaleCut"] = new JsonObject { ["mode"] = "outline" } });
        var view = Views.Find(f.Model, plan)!;
        Assert.Equal(new SmallScaleCut("outline", 200), ViewStyles.Of(f.Model, view).SmallScaleCut);
        Assert.Empty(Fills(Render(f, plan).Pens));
        Assert.Contains("smallScaleCut", f.Session.Query("view.style", new JsonObject { ["id"] = plan })!["overrides"]!.ToJsonString());
        // A threshold the view's scale does not reach: as the materials say.
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["smallScaleCut"] = new JsonObject { ["from"] = 500 } });
        Assert.NotEmpty(Hatches(Render(f, plan).Pens));
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["smallScaleCut"] = null });
        Assert.Equal("#000000", Assert.Single(Fills(Render(f, plan).Pens)).Fill);
        Assert.Throws<CommandException>(() => f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["smallScaleCut"] = new JsonObject { ["mode"] = "black" } }));
        Assert.Throws<CommandException>(() => f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["smallScaleCut"] = new JsonObject { ["from"] = 0 } }));
        Assert.Throws<CommandException>(() => f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["smallScaleCut"] = new JsonObject { ["color"] = "red" } }));
        // The style's taken off (null): none.
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["smallScaleCut"] = null });
        Assert.Null(ViewStyles.Of(f.Model, Views.Find(f.Model, plan)!).SmallScaleCut);
    }

    // ---- A wall type's layer graphics (decision 758, the forum's "a set of pens for the core and for the cladding, for each
    // scale, as in ArchiCAD"): the type's layer names its own lines (and any graphics), with steps by scale, over its material's.

    /// <summary>The long cut lines of a plan of a wall along x, by their distance from the axis (mm, rounded): their width and colour.</summary>
    static Dictionary<int, (double Width, string? Stroke)> FaceLines(IEnumerable<Pen> pens, double axis = 0)
    {
        var o = new Dictionary<int, (double, string?)>();
        foreach (var p in pens.Where(p => p.Layer == PenLayer.Cut))
            foreach (var (a, b) in p.Segments)
                if (Math.Abs(a.Y - b.Y) < 1e-6 && Math.Abs(a.X - b.X) > 1 && Math.Abs(a.Y - axis) < 1) o[(int)Math.Round(Math.Abs(a.Y - axis) * 1000)] = (p.Width, p.Stroke);
        return o;
    }

    /// <summary>The wall's type with its layers again, the core's and the insulation's graphics given.</summary>
    static void SetLayerGraphics(Fixture f, string type, JsonObject? core, JsonObject? insulation)
    {
        JsonObject L(string m, double t, bool c, JsonObject? g)
        {
            var o = new JsonObject { ["material"] = m, ["thickness"] = t, ["core"] = c };
            if (g != null) o["graphics"] = g;
            return o;
        }
        f.Run("architecture.updateWallType", new JsonObject
        {
            ["id"] = type,
            ["layers"] = new JsonArray(L("Gypsum plaster", 0.015, false, null), L("Concrete", 0.2, true, core), L("EPS insulation", 0.1, false, insulation)),
        });
    }

    /// <summary>The core's lines: 0.7 mm between the layers to 1:99, 0.35 mm red from 1:100; the insulation's outline 0.13 mm, 0.09 from 1:100.</summary>
    static (JsonObject Core, JsonObject Insulation) CladdingPens() => (
        new JsonObject
        {
            ["separatorLine"] = new JsonObject { ["weight"] = 0.7 },
            ["byScale"] = new JsonArray(new JsonObject { ["from"] = 100, ["separatorLine"] = new JsonObject { ["weight"] = 0.35, ["color"] = "#ff0000" } }),
        },
        new JsonObject
        {
            ["cutLine"] = new JsonObject { ["weight"] = 0.13 },
            ["byScale"] = new JsonArray(new JsonObject { ["from"] = 100, ["cutLine"] = new JsonObject { ["weight"] = 0.09 } }),
        });

    static string TypeOf(Fixture f, string wall) => f.Model.RelatingOf(wall, IfcClass.RelDefinesByType)!.Id;

    [Fact]
    public void A_wall_type_s_layer_gives_its_own_pens_for_each_scale()
    {
        var (f, wall) = Wall();
        var type = TypeOf(f, wall);
        var p50 = Plan(f, 50);
        var p100 = Plan(f, 100);
        var before50 = FaceLines(Render(f, p50).Pens);
        // As before: the outline 0.5, the lines between the layers the separation pen.
        Assert.Equal((0.25, "#000000"), before50[100]);
        Assert.Equal(0.5, before50[200].Width);
        var (core, insulation) = CladdingPens();
        SetLayerGraphics(f, type, core, insulation);

        // 1:50: the core's lines between the layers 0.7 (both its faces), the insulation's outer face 0.13, the plaster's face as before.
        var at50 = FaceLines(Render(f, p50).Pens);
        Assert.Equal((0.7, "#000000"), at50[100]);
        Assert.Equal(0.13, at50[200].Width);
        Assert.Equal(before50[115], at50[115]);
        // 1:100: the core's step (0.35, red), the insulation's (0.09).
        var at100 = FaceLines(Render(f, p100).Pens);
        Assert.Equal((0.35, "#ff0000"), at100[100]);
        Assert.Equal(0.09, at100[200].Width);
        // A section (a cut of the same wall) takes them too; the resolver gives the band its layer's tier.
        var band = new ViewMaterialGraphics(f.Model, "section", 100, ViewStyles.Of(f.Model, Views.Find(f.Model, p100)!))
            .Band(new DPolygon([], wall, "IFCWALL", "layer:1", Material(f, "Concrete")));
        Assert.Equal(new MaterialLine("#ff0000", 0.35), band!.SeparatorLine);
    }

    [Fact]
    public void A_layer_s_graphics_lie_over_its_material_s_and_under_the_view_s_and_another_type_of_the_material_keeps_the_material_s()
    {
        var (f, wall) = Wall();
        var concrete = Material(f, "Concrete");
        // The material's own step: separators 1.0 from 1:100, its hatch red.
        Set(f, concrete, "section", new JsonObject { ["byScale"] = new JsonArray(new JsonObject { ["from"] = 100, ["hatchColor"] = "#ff0000", ["separatorLine"] = new JsonObject { ["weight"] = 1.0 } }) });
        var (core, _) = CladdingPens();
        SetLayerGraphics(f, TypeOf(f, wall), core, null);
        var plan = Plan(f, 100);
        var pens = Render(f, plan).Pens;
        // The layer's step over the material's step: 0.35, the material's hatch colour kept (the layer sets none).
        Assert.Equal(0.35, FaceLines(pens)[100].Width);
        Assert.Contains(Hatches(pens), p => p.Stroke == "#ff0000");
        // Another wall type of the same concrete: the material's 1.0 (its own wall, far off).
        var other = f.Type("Bare", ("Gypsum plaster", 0.015, false), ("Concrete", 0.2, true), ("EPS insulation", 0.1, false));
        f.Wall(0, 10, 5, 10, other);
        pens = Render(f, plan).Pens;
        Assert.Equal(1.0, FaceLines(pens, 10)[100].Width);
        Assert.Equal(0.35, FaceLines(pens)[100].Width);
        // The element's own override in the view wins over its layer's.
        f.Run("view.elementGraphics", new JsonObject { ["id"] = plan, ["ids"] = new JsonArray(wall), ["value"] = new JsonObject { ["cutLine"] = new JsonObject { ["color"] = "#00ff00" } } });
        var band = ViewMaterialGraphics.For(f.Model, Views.Find(f.Model, plan)!, "plan", 100).Band(new DPolygon([], wall, "IFCWALL", "layer:1", concrete));
        Assert.Equal("#00ff00", band!.CutLine!.Color);
        Assert.Equal(0.35, band.SeparatorLine!.Weight);
    }

    [Fact]
    public void A_layer_s_graphics_are_checked_kept_with_the_type_one_undo_step_and_a_type_without_them_writes_as_before()
    {
        var (f, wall) = Wall();
        var type = TypeOf(f, wall);
        var set = LayerSets.Of(f.Model, type)!;
        var before = set.Attrs.ToJsonString();
        Assert.DoesNotContain("graphics", before);
        // The same layers again, none given: written exactly as before.
        SetLayerGraphics(f, type, null, null);
        Assert.Equal(before, LayerSets.Of(f.Model, type)!.Attrs.ToJsonString());

        var (core, insulation) = CladdingPens();
        SetLayerGraphics(f, type, core, insulation);
        var layers = LayerSets.LayersOf(f.Model, type);
        Assert.Equal(0.7, (double?)layers[1].Graphics!["separatorLine"]!["weight"]);
        Assert.Null(layers[0].Graphics);
        // One undo step.
        f.Session.Undo();
        Assert.Equal(before, LayerSets.Of(f.Model, type)!.Attrs.ToJsonString());

        void Bad(JsonObject g) => Assert.Throws<CommandException>(() => SetLayerGraphics(f, type, g, null));
        Bad(new JsonObject { ["pen"] = 3 });
        Bad(new JsonObject { ["cutLine"] = new JsonObject { ["weight"] = 9 } });
        Bad(new JsonObject { ["hatch"] = "NO_SUCH_PATTERN" });
        Bad(new JsonObject { ["byScale"] = new JsonArray(new JsonObject { ["cutLine"] = new JsonObject { ["weight"] = 0.35 } }) });
        Bad(new JsonObject { ["reset"] = new JsonArray("hatch") });
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWallType", new JsonObject
        {
            ["id"] = type, ["layers"] = new JsonArray(new JsonObject { ["material"] = "Concrete", ["thickness"] = 0.2, ["core"] = true, ["graphics"] = "thick" }),
        }));
        // Empty graphics: none kept.
        SetLayerGraphics(f, type, new JsonObject(), null);
        Assert.Equal(before, LayerSets.Of(f.Model, type)!.Attrs.ToJsonString());
    }

    [Fact]
    public void A_layer_s_graphics_travel_with_the_type_s_file()
    {
        var (f, wall) = Wall();
        var type = TypeOf(f, wall);
        var (core, insulation) = CladdingPens();
        SetLayerGraphics(f, type, core, insulation);
        var text = (string)f.Session.Query("architecture.exportTypes", new JsonObject { ["kinds"] = new JsonArray("wall") })!["text"]!;
        Assert.Contains("separatorLine", text);
        var g = new Fixture();
        g.Run("architecture.importTypes", new JsonObject { ["text"] = text });
        var t = g.Model.OfClass(IfcClass.WallType).First(x => x.Name == "Insulated");
        var layers = LayerSets.LayersOf(g.Model, t.Id);
        Assert.Equal(0.35, (double?)layers[1].Graphics!["byScale"]![0]!["separatorLine"]!["weight"]);
        Assert.Equal(0.13, (double?)layers[2].Graphics!["cutLine"]!["weight"]);
    }

    [Fact]
    public void A_legend_draws_a_material_at_its_view_s_scale()
    {
        var f = new Fixture(new Lapin.Architecture.ArchitectureModule(), new Lapin.Drafting.DraftingModule());
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plans", ["paper"] = "A3" })!["id"]!;
        f.Wall(0, 0, 8, 0, f.Type("Concrete 20", ("Concrete", 0.2, true)));
        Set(f, Material(f, "Concrete"), "section", new JsonObject { ["byScale"] = new JsonArray(new JsonObject { ["from"] = 100, ["hatchColor"] = "#ff0000" }) });
        var plan = Plan(f, 100);
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = plan, ["x"] = 150, ["y"] = 140 });
        f.Run("sheet.addLegend", new JsonObject { ["viewId"] = PaperViews.IdOf(s), ["at"] = new JsonArray(0.3, 0.25) });
        var pens = Sheets.Compose(f.Session.Reader, f.Model.Get(s)!).Pens;
        Assert.Contains(pens, p => p.Layer == "LEGEND" && p.Stroke == "#ff0000" && p.Segments.Count > 0);
        // The viewport itself at 1:100: the step too.
        Assert.Contains(pens, p => p.Layer != "LEGEND" && p.Stroke == "#ff0000" && p.Segments.Count > 0);
    }
}
