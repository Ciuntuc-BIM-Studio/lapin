using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage 4 of docs/plan-material-graphics.md (decision 674): material graphics sets. An entry rules a material by its id over a
/// name over a category over all; a view style names a set and a view names another or none; a set removed leaves its styles
/// none; a project made since has „Structure” and „MEP” for its Structural and MEP styles, one made before none; sets travel in
/// a .lapinstandards file with the merge choices; every command one undo step.
/// </summary>
public class MaterialGraphicsSetTests
{
    static string Mat(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;

    /// <summary>A concrete wall with brick, plaster and EPS layers, and a plan at 1:50 in the Architectural style.</summary>
    static (Fixture F, string Plan) Project()
    {
        var f = new Fixture();
        f.EveryLayerInPlans();
        var type = f.Type("Mixed", ("Gypsum plaster", 0.015, false), ("Concrete", 0.2, true), ("Brick masonry", 0.115, false), ("EPS insulation", 0.1, false));
        f.Wall(0, 0, 5, 0, type);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50 })!["id"]!;
        return (f, plan);
    }

    static Entity View(Fixture f, string id) => Views.Find(f.Model, id)!;

    static ResolvedMaterial? Look(Fixture f, string plan, string material) =>
        ViewMaterialGraphics.For(f.Model, View(f, plan), "plan", 50).Material(Mat(f, material));

    static string Create(Fixture f, string name, JsonArray entries) =>
        (string)f.Run("materialGraphicsSet.create", new JsonObject { ["name"] = name, ["entries"] = entries })!["id"]!;

    static JsonObject Entry(JsonObject match, JsonObject section) => new() { ["match"] = match, ["section"] = section };

    /// <summary>A project made before decision 674: its mark taken off.</summary>
    static void MakeOld(Fixture f) => f.Session.Edit("old", e =>
    {
        var p = Spatial.Project(e.Model)!;
        var a = p.CloneAttrs();
        a.Remove(MaterialStandards.LookRevKey);
        e.Replace(p.With(attrs: a));
    });

    [Fact]
    public void The_most_particular_entry_wins_a_material_id_over_a_name_over_a_category_over_all()
    {
        var (f, plan) = Project();
        var set = Create(f, "Mine", new JsonArray(
            Entry(new JsonObject(), new JsonObject { ["hatch"] = "HORIZONTAL" }),
            Entry(new JsonObject { ["category"] = "Structure" }, new JsonObject { ["hatch"] = "LINES45" }),
            Entry(new JsonObject { ["materialName"] = "concrete|brick" }, new JsonObject { ["hatch"] = "CROSS" }),
            Entry(new JsonObject { ["materialName"] = "brick" }, new JsonObject { ["hatch"] = "TILES" }),
            Entry(new JsonObject { ["material"] = Mat(f, "Concrete") }, new JsonObject { ["hatch"] = "STEEL", ["fill"] = "#999999" })));
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["materialGraphicsSet"] = set });
        // The concrete by its id; the brick by the first of the two names that match; the EPS (insulation) by all.
        Assert.Equal(("STEEL", "#999999"), (Look(f, plan, "Concrete")!.Hatch, Look(f, plan, "Concrete")!.Fill));
        Assert.Equal("CROSS", Look(f, plan, "Brick masonry")!.Hatch);
        Assert.Equal("HORIZONTAL", Look(f, plan, "EPS insulation")!.Hatch);
        Assert.Equal(set, Look(f, plan, "Concrete")!.GraphicsSet);
        // A category the standard gives the name (brick is Structure) when the material has none of its own: with the name entries gone, the category's.
        f.Run("materialGraphicsSet.update", new JsonObject
        {
            ["id"] = set,
            ["entries"] = new JsonArray(Entry(new JsonObject { ["category"] = "Structure" }, new JsonObject { ["hatch"] = "LINES45" })),
        });
        Assert.Equal("LINES45", Look(f, plan, "Brick masonry")!.Hatch);
        // A material no entry matches keeps its own look.
        Assert.Equal("GYPSUM", Look(f, plan, "Gypsum plaster")!.Hatch);
        Assert.Null(Look(f, plan, "Gypsum plaster")!.GraphicsSet);
    }

    [Fact]
    public void A_style_names_a_set_and_a_view_names_another_or_none()
    {
        var (f, plan) = Project();
        var grey = Create(f, "Grey", new JsonArray(Entry(new JsonObject { ["materialName"] = "concrete" }, new JsonObject { ["hatch"] = null, ["fill"] = "#999999" })));
        var red = Create(f, "Red", new JsonArray(Entry(new JsonObject { ["materialName"] = "concrete" }, new JsonObject { ["hatch"] = null, ["fill"] = "#ff0000" })));
        static IEnumerable<string?> Fills(Fixture f, string plan) => Views.Render(f.Session.Reader, View(f, plan)).Pens.Where(p => p.Layer == PenLayer.Fill).Select(p => p.Fill);
        Assert.DoesNotContain("#999999", Fills(f, plan));
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["materialGraphicsSet"] = grey });
        Assert.Contains("#999999", Fills(f, plan));
        Assert.Equal(grey, (string?)f.Session.Query("view.style", new JsonObject { ["id"] = plan })!["resolved"]!["materialGraphicsSet"]);
        // The view's own over its style's; none over it; null its style's again.
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["materialGraphicsSet"] = red });
        Assert.Contains("#ff0000", Fills(f, plan));
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["materialGraphicsSet"] = "none" });
        Assert.DoesNotContain("#ff0000", Fills(f, plan));
        Assert.DoesNotContain("#999999", Fills(f, plan));
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["materialGraphicsSet"] = null });
        Assert.Contains("#999999", Fills(f, plan));
        // An unknown set refused.
        Assert.Throws<CommandException>(() => f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["materialGraphicsSet"] = "nope" }));
        Assert.Throws<CommandException>(() => f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["materialGraphicsSet"] = "nope" }));
    }

    [Fact]
    public void The_set_lies_over_the_small_scale_cut_and_its_steps_by_scale_hold()
    {
        var (f, plan) = Project();
        var set = Create(f, "Coarse", new JsonArray(Entry(new JsonObject { ["materialName"] = "concrete" }, new JsonObject
        {
            ["fill"] = "#00ff00", ["byScale"] = new JsonArray(new JsonObject { ["from"] = 100, ["fill"] = "#0000ff" }),
        })));
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["materialGraphicsSet"] = set, ["smallScaleCut"] = new JsonObject { ["mode"] = "solid", ["from"] = 20 } });
        var view = View(f, plan);
        var cut = Views.Render(f.Session.Reader, view).Generated!.Cuts.First(c => c.Material == Mat(f, "Concrete"));
        Assert.Equal("#00ff00", ViewMaterialGraphics.For(f.Model, view, "plan", 50).Band(cut)!.Fill);
        Assert.Equal("#0000ff", ViewMaterialGraphics.For(f.Model, view, "plan", 100).Band(cut)!.Fill);
        // The small-scale cut still takes what the set does not set (the brick solid black).
        var brick = Views.Render(f.Session.Reader, view).Generated!.Cuts.First(c => c.Material == Mat(f, "Brick masonry"));
        Assert.Equal("#000000", ViewMaterialGraphics.For(f.Model, view, "plan", 50).Band(brick)!.Fill);
    }

    [Fact]
    public void A_set_removed_leaves_its_styles_none_and_its_views_their_style_s_and_each_command_is_one_undo_step()
    {
        var (f, plan) = Project();
        var set = Create(f, "Grey", new JsonArray(Entry(new JsonObject { ["materialName"] = "concrete" }, new JsonObject { ["fill"] = "#999999" })));
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["materialGraphicsSet"] = set });
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["materialGraphicsSet"] = set });
        f.Run("materialGraphicsSet.delete", new JsonObject { ["id"] = set });
        Assert.DoesNotContain(MaterialGraphicsSets.All(f.Model), s => s.Id == set);
        Assert.Null(ViewStyles.All(f.Model).First(s => s.Id == "architectural").MaterialGraphicsSet);
        Assert.Null(View(f, plan).Attrs["styleOverrides"]?["materialGraphicsSet"]);
        Assert.Null(Look(f, plan, "Concrete")!.GraphicsSet);
        // One step: undone, the set, the style's name and the view's are back.
        f.Session.Undo();
        Assert.Contains(MaterialGraphicsSets.All(f.Model), s => s.Id == set);
        Assert.Equal(set, ViewStyles.All(f.Model).First(s => s.Id == "architectural").MaterialGraphicsSet);
        Assert.Equal(set, (string?)View(f, plan).Attrs["styleOverrides"]?["materialGraphicsSet"]);
        // Update and create one step each.
        f.Run("materialGraphicsSet.update", new JsonObject { ["id"] = set, ["name"] = "Renamed" });
        f.Session.Undo();
        Assert.Equal("Grey", MaterialGraphicsSets.Find(f.Model, set)!.Name);
        f.Session.Undo();
        f.Session.Undo();
        f.Session.Undo();
        Assert.DoesNotContain(MaterialGraphicsSets.All(f.Model), s => s.Id == set);
    }

    [Fact]
    public void Entries_are_checked_as_material_graphics()
    {
        var (f, _) = Project();
        Assert.Throws<CommandException>(() => Create(f, "Bad", new JsonArray(Entry(new JsonObject(), new JsonObject { ["hatch"] = "NO_SUCH" }))));
        Assert.Throws<CommandException>(() => Create(f, "Bad", new JsonArray(Entry(new JsonObject { ["category"] = "Nope" }, new JsonObject()))));
        Assert.Throws<CommandException>(() => Create(f, "Bad", new JsonArray(Entry(new JsonObject { ["materialName"] = "(" }, new JsonObject()))));
        Assert.Throws<CommandException>(() => Create(f, "Bad", new JsonArray(Entry(new JsonObject { ["material"] = "0000000000000000000000" }, new JsonObject()))));
        Assert.Throws<CommandException>(() => Create(f, "Bad", new JsonArray(Entry(new JsonObject(), new JsonObject { ["fill"] = "red" }))));
        Assert.Throws<CommandException>(() => Create(f, "Bad", new JsonArray(Entry(new JsonObject(), new JsonObject { ["nope"] = 1 }))));
        // A name taken refused; a copy of another made from it.
        var a = Create(f, "Mine", new JsonArray(Entry(new JsonObject(), new JsonObject { ["hatch"] = "lines45" })));
        Assert.Equal("LINES45", (string?)MaterialGraphicsSets.Find(f.Model, a)!.Json["entries"]![0]!["section"]!["hatch"]);
        Assert.Throws<CommandException>(() => Create(f, "Mine", new JsonArray()));
        var b = (string)f.Run("materialGraphicsSet.create", new JsonObject { ["name"] = "Copy", ["from"] = a })!["id"]!;
        Assert.True(JsonNode.DeepEquals(MaterialGraphicsSets.Find(f.Model, a)!.Json["entries"], MaterialGraphicsSets.Find(f.Model, b)!.Json["entries"]));
    }

    [Fact]
    public void A_new_project_s_structural_and_mep_styles_have_their_sets_and_an_old_project_s_never_gain_them()
    {
        var (f, _) = Project();
        var list = (JsonArray)f.Session.Query("materialGraphicsSet.list", new JsonObject())!["sets"]!;
        Assert.Equal(["structure", "mep"], list.Select(s => (string)s!["id"]!));
        Assert.True((bool)list[0]!["builtIn"]!);
        Assert.Equal("structural", (string?)list[0]!["styles"]![0]);
        Assert.Equal("structure", ViewStyles.All(f.Model).First(s => s.Id == "structural").MaterialGraphicsSet);
        Assert.Equal("mep", ViewStyles.All(f.Model).First(s => s.Id == "mep").MaterialGraphicsSet);
        Assert.Null(ViewStyles.All(f.Model).First(s => s.Id == "architectural").MaterialGraphicsSet);
        // A structural plan: the concrete solid grey and unhatched, the brick 45° lines, the plaster with no hatch, the EPS halftone.
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50, ["discipline"] = "Structure" })!["id"]!;
        Assert.Equal((null, "#999999"), (Look(f, plan, "Concrete")!.Hatch, Look(f, plan, "Concrete")!.Fill));
        Assert.Equal("MASONRY", Look(f, plan, "Brick masonry")!.Hatch);
        Assert.Null(Look(f, plan, "Gypsum plaster")!.Hatch);
        Assert.True(Look(f, plan, "EPS insulation")!.Halftone);
        // The MEP style's: no hatch, grey lines.
        var mep = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 50, ["discipline"] = "MEP" })!["id"]!;
        Assert.Null(Look(f, mep, "Brick masonry")!.Hatch);
        Assert.Equal("#999999", Look(f, mep, "Brick masonry")!.CutLine!.Color);
        // A project made before (its styles never saved with a set): no set, and its styles name none.
        var (o, _) = Project();
        o.Session.Edit("styles of before", e =>
        {
            var p = Spatial.Project(e.Model)!;
            var a = p.CloneAttrs();
            a.Remove(ViewStyles.Key);
            a.Remove(MaterialStandards.LookRevKey);
            e.Replace(p.With(attrs: a));
        });
        Assert.Empty(MaterialGraphicsSets.All(o.Model));
        Assert.All(ViewStyles.All(o.Model), s => Assert.Null(s.MaterialGraphicsSet));
        var oldPlan = (string)o.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = o.Storey, ["scale"] = 50, ["discipline"] = "Structure" })!["id"]!;
        Assert.Equal("CONCRETE", Look(o, oldPlan, "Concrete")!.Hatch);
        // Removing the built-in ones from a new project keeps them removed.
        var g = new Fixture();
        g.Run("materialGraphicsSet.delete", new JsonObject { ["id"] = "structure" });
        g.Run("materialGraphicsSet.delete", new JsonObject { ["id"] = "mep" });
        Assert.Empty(MaterialGraphicsSets.All(g.Model));
        Assert.Null(ViewStyles.All(g.Model).First(s => s.Id == "structural").MaterialGraphicsSet);
    }

    [Fact]
    public void Sets_travel_in_a_standards_file_with_the_merge_choices()
    {
        var (a, _) = Project();
        var mine = Create(a, "Firm", new JsonArray(
            Entry(new JsonObject { ["material"] = Mat(a, "Concrete") }, new JsonObject { ["fill"] = "#999999", ["hatch"] = null }),
            Entry(new JsonObject { ["category"] = "Insulation" }, new JsonObject { ["halftone"] = true })));
        var text = (string)a.Session.Query("standards.export", new JsonObject { ["name"] = "Firm" })!["text"]!;
        var file = JsonNode.Parse(text)!;
        var sets = (JsonArray)file[MaterialGraphicsSets.Key]!;
        // The built-in ones as shipped stay out; the material named by its name, not its id.
        Assert.Equal([mine], sets.Select(s => (string)s!["id"]!));
        Assert.Equal("^concrete$", (string?)sets[0]!["entries"]![0]!["match"]!["materialName"]);
        Assert.Null(sets[0]!["entries"]![0]!["match"]!["material"]);

        var (b, bplan) = Project();
        var read = b.Session.Query("standards.readPack", new JsonObject { ["text"] = text })!;
        Assert.False((bool)read["materialGraphicsSets"]![0]!["exists"]!);
        Assert.Contains(((JsonArray)read["entries"]!), e => (string?)e!["kind"] == "materialGraphicsSet" && (string?)e["id"] == mine);
        b.Run("standards.importFrom", new JsonObject { ["text"] = text });
        Assert.NotNull(MaterialGraphicsSets.Find(b.Model, mine));
        b.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["materialGraphicsSet"] = mine });
        Assert.Equal("#999999", Look(b, bplan, "Concrete")!.Fill);
        Assert.True((bool)b.Session.Query("standards.readPack", new JsonObject { ["text"] = text })!["materialGraphicsSets"]![0]!["same"]!);

        // Changed in b, then the file again: both keeps a copy beside it, skip leaves it, update takes the file's.
        b.Run("materialGraphicsSet.update", new JsonObject { ["id"] = mine, ["entries"] = new JsonArray() });
        var key = "materialGraphicsSet:" + mine;
        b.Run("standards.importFrom", new JsonObject { ["text"] = text, ["choices"] = new JsonObject { [key] = "skip" } });
        Assert.Empty(MaterialGraphicsSets.Find(b.Model, mine)!.Entries);
        var both = b.Run("standards.importFrom", new JsonObject { ["text"] = text, ["choices"] = new JsonObject { [key] = "both" } })!;
        Assert.Equal("Firm (2)", (string?)both["renamed"]![0]);
        Assert.Contains(MaterialGraphicsSets.All(b.Model), s => s.Name == "Firm (2)" && s.Entries.Count == 2);
        b.Run("standards.importFrom", new JsonObject { ["text"] = text, ["choices"] = new JsonObject { [key] = "update" } });
        Assert.Equal(2, MaterialGraphicsSets.Find(b.Model, mine)!.Entries.Count);
        // Into a project made before: the set comes in (it has none built in).
        var (c, _) = Project();
        MakeOld(c);
        c.Run("standards.importFrom", new JsonObject { ["text"] = text });
        Assert.Equal([mine], MaterialGraphicsSets.All(c.Model).Select(s => s.Id));
    }

    [Fact]
    public void A_line_type_renamed_follows_into_the_sets()
    {
        var (f, _) = Project();
        f.Run("standards.setLineType", new JsonObject { ["name"] = "Mine", ["dashes"] = new JsonArray(2, 1) });
        var set = Create(f, "Lines", new JsonArray(Entry(new JsonObject(), new JsonObject { ["hatchLineType"] = "Mine", ["cutLine"] = new JsonObject { ["lineType"] = "Mine" } })));
        f.Run("standards.setLineType", new JsonObject { ["name"] = "Mine", ["newName"] = "Ours" });
        var section = MaterialGraphicsSets.Find(f.Model, set)!.Json["entries"]![0]!["section"]!;
        Assert.Equal("Ours", (string?)section["hatchLineType"]);
        Assert.Equal("Ours", (string?)section["cutLine"]!["lineType"]);
    }
}
