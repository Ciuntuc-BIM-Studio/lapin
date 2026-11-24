using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Material keys (forum topic 255, item 5): each material's key, a number stored once the first time it is keyed and written
/// by the project's format (F{n}) unless the material has a code of its own; a material key tag clicked on a facade in an
/// elevation reads the material seen there (the render, not the core) and writes its key in a circle, live; a material
/// legend on a sheet lists what its views show (their seen faces too) or what their tags read, each row its key, its name
/// and its description, in the keys' order.
/// </summary>
public class MaterialKeyTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static string Mat(Fixture f, string name, string? description = null)
    {
        var id = (string)f.Run("material.create", new JsonObject { ["name"] = name })!["id"]!;
        if (description != null) f.Run("material.setData", new JsonObject { ["id"] = id, ["description"] = description });
        return id;
    }

    static string Key(Fixture f, string id) => MaterialKeys.KeyOf(f.Model, id)!;
    static int? Stored(Fixture f, string id) => MaterialKeys.Stored(f.Model.Get(id)!);

    [Fact]
    public void Keys_are_numbered_once_when_first_keyed_and_stay_when_materials_are_added_or_deleted()
    {
        var f = F();
        // The project's own materials (a template's) are numbered by nobody yet: numbers in effect for the time being, by name.
        var render = Mat(f, "Zz render");
        var brick = Mat(f, "Zz brick");
        var stone = Mat(f, "Zz stone");
        Assert.All(new[] { render, brick, stone }, id => Assert.Null(Stored(f, id)));
        // Keyed first, the stone is 1, then the render 2: the order they were first keyed, the next number each.
        f.Run("material.numberKeys", new JsonObject { ["ids"] = new JsonArray(stone) });
        f.Run("material.numberKeys", new JsonObject { ["ids"] = new JsonArray(render) });
        Assert.Equal(1, Stored(f, stone));
        Assert.Equal(2, Stored(f, render));
        Assert.Equal("1", Key(f, stone));
        Assert.Equal("2", Key(f, render));
        // Numbered again, nothing changes; a material added takes nothing from them.
        f.Run("material.numberKeys", new JsonObject { ["ids"] = new JsonArray(stone, render) });
        var added = Mat(f, "Aa added");
        Assert.Equal("1", Key(f, stone));
        Assert.Equal("2", Key(f, render));
        // One not keyed yet is numbered after the highest, by name, for the time being: the added one before the brick.
        Assert.True(MaterialKeys.Numbers(f.Model)[added] < MaterialKeys.Numbers(f.Model)[brick]);
        Assert.True(MaterialKeys.Numbers(f.Model)[added] > 2);
        // The stone deleted leaves its gap: the render stays 2, the brick keyed now is 3.
        f.Run("material.delete", new JsonObject { ["id"] = stone });
        f.Run("material.numberKeys", new JsonObject { ["ids"] = new JsonArray(brick) });
        Assert.Equal("2", Key(f, render));
        Assert.Equal("3", Key(f, brick));
        // Renumbered by hand, the gaps close in one undo step.
        f.Run("material.renumberKeys", new JsonObject());
        Assert.Equal("1", Key(f, render));
        Assert.Equal("2", Key(f, brick));
        f.Session.Undo();
        Assert.Equal("2", Key(f, render));
        Assert.Equal("3", Key(f, brick));
    }

    [Fact]
    public void A_format_writes_the_number_and_a_material_s_own_code_goes_before_it()
    {
        var f = F();
        var render = Mat(f, "Zz render");
        var brick = Mat(f, "Zz brick");
        // Several at once take their numbers in their keys' order now (by name, for materials not keyed yet): the brick first.
        f.Run("material.numberKeys", new JsonObject { ["ids"] = new JsonArray(render, brick) });
        Assert.Equal("1", Key(f, brick));
        Assert.Equal("2", Key(f, render));
        f.Run("material.setKey", new JsonObject { ["id"] = brick, ["number"] = 3 });
        f.Run("material.setKey", new JsonObject { ["id"] = render, ["number"] = 1 });
        f.Run("material.setKey", new JsonObject { ["id"] = brick, ["number"] = 2 });
        Assert.Equal("1", Key(f, render));
        f.Run("standards.options", new JsonObject { ["materialKeyFormat"] = "F{n}" });
        Assert.Equal("F1", Key(f, render));
        Assert.Equal("F2", Key(f, brick));
        f.Run("standards.options", new JsonObject { ["materialKeyFormat"] = "M-{n:00}" });
        Assert.Equal("M-02", Key(f, brick));
        Assert.Equal("M-07", MaterialKeys.Write("M-{n:00}", 7));
        // A format holds its number once.
        Assert.Throws<CommandException>(() => f.Run("standards.options", new JsonObject { ["materialKeyFormat"] = "F" }));
        Assert.Throws<CommandException>(() => f.Run("standards.options", new JsonObject { ["materialKeyFormat"] = "{n}{n}" }));
        f.Run("standards.options", new JsonObject { ["materialKeyFormat"] = null });
        Assert.Equal("1", Key(f, render));
        // Its own code over the format's; another material's key is refused, as a number another has.
        f.Run("material.setKey", new JsonObject { ["id"] = brick, ["key"] = "RF-02" });
        Assert.Equal("RF-02", Key(f, brick));
        Assert.Throws<CommandException>(() => f.Run("material.setKey", new JsonObject { ["id"] = brick, ["key"] = "1" }));
        Assert.Throws<CommandException>(() => f.Run("material.setKey", new JsonObject { ["id"] = brick, ["number"] = 1 }));
        f.Run("material.setKey", new JsonObject { ["id"] = brick, ["key"] = null });
        Assert.Equal("2", Key(f, brick));
        // Keys sort as they are read: 2 before 10, F2 before F10.
        Assert.True(MaterialKeys.Compare("2", "10") < 0);
        Assert.True(MaterialKeys.Compare("F2", "F10") < 0);
        Assert.True(MaterialKeys.Compare("F10", "G1") < 0);
    }

    /// <summary>A wall rendered on both faces of a concrete core, in a south elevation; the point of its south face at its middle.</summary>
    static (Fixture F, string Wall, string Render, string Concrete, string View, Vec2 Face) Facade()
    {
        var f = F();
        var type = f.Type("Rendered 24", ("Lime render", 0.02, false), ("Concrete", 0.2, true), ("Lime render", 0.02, false));
        var wall = f.Wall(0, 0, 8, 0, type);
        var render = Materials.Find(f.Model, "Lime render")!.Id;
        var concrete = Materials.Find(f.Model, "Concrete")!.Id;
        f.Run("material.setData", new JsonObject { ["id"] = render, ["description"] = "Lime render, white, 2 cm\nsecond line" });
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var frame = Views.Render(f.Session.Reader, Views.Find(f.Model, view)!).Frame!;
        var (lo, hi) = f.Bounds(wall);
        return (f, wall, render, concrete, view, frame.Map(new Vec3(4, lo.Y, (lo.Z + hi.Z) / 2)));
    }

    [Fact]
    public void An_elevation_sees_a_wall_s_render_not_its_core()
    {
        var (f, wall, render, concrete, view, face) = Facade();
        var drawn = Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);
        Assert.Equal(render, drawn.Seen!.At(face)!.Value.Material);
        Assert.Equal(wall, drawn.Seen.At(face, wall)!.Value.Element);
        Assert.Contains(render, drawn.Seen.Seen());
        Assert.DoesNotContain(concrete, drawn.Seen.Seen());
        // Off the wall, nothing.
        Assert.Null(drawn.Seen.At(face + new Vec2(20, 0)));
    }

    [Fact]
    public void A_material_key_tag_writes_the_render_s_key_in_a_circle_and_follows_the_material()
    {
        var (f, wall, render, concrete, view, face) = Facade();
        f.Run("standards.options", new JsonObject { ["materialKeyFormat"] = "F{n}" });
        var tag = (string)f.Run("drafting.addTag", new JsonObject
        {
            ["viewId"] = view, ["element"] = wall, ["field"] = "materialKey", ["at"] = new JsonArray(face.X + 1.5, face.Y + 1.5), ["pick"] = new JsonArray(face.X, face.Y),
        })!["id"]!;
        var t = f.Model.Get(tag)!;
        Assert.Equal("circle", t.Str("frame"));
        Assert.Equal("straight", t.Str("leaderShape"));
        // Placing it numbered the render, in the same step.
        Assert.NotNull(Stored(f, render));
        Assert.Null(Stored(f, concrete));
        var key = Key(f, render);
        Assert.StartsWith("F", key);
        ViewMark Mark() => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!).Marks.Single(m => m.Owner == tag);
        var mk = Mark();
        Assert.Contains(mk.Texts, x => x.Text.Text == key);
        Assert.Equal(render, (string?)mk.Data!["material"]);
        // A circle, not a box: more than four corners round the text.
        Assert.Contains(mk.Lines.GroupBy(l => l.Layer), g => g.Key == "TAG" && g.Count() > 20);
        // Its own code: the tag follows.
        f.Run("material.setKey", new JsonObject { ["id"] = render, ["key"] = "R1" });
        Assert.Contains(Mark().Texts, x => x.Text.Text == "R1");
        // Undone together: the tag and the number it gave.
        f.Session.Undo();
        f.Session.Undo();
        Assert.Null(f.Model.Get(tag));
        Assert.Null(Stored(f, render));
    }

    [Fact]
    public void A_keyed_legend_lists_what_the_sheet_s_elevation_shows_in_the_keys_order()
    {
        var (f, wall, render, concrete, view, face) = Facade();
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Facades", ["paper"] = "A3" })!["id"]!;
        var paper = PaperViews.IdOf(s);
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = view, ["x"] = 150, ["y"] = 140 });
        var paperView = Views.Find(f.Model, paper)!;
        // As before, a material legend lists cuts: an elevation has none.
        var legend = (string)f.Run("sheet.addLegend", new JsonObject { ["viewId"] = paper, ["at"] = new JsonArray(0.3, 0.25) })!["id"]!;
        Assert.DoesNotContain(DrawingKeys.Entries(f.Session.Reader, paperView, "materials"), r => r.Material == render);
        Assert.Null(Stored(f, render));
        // What the views show: the render seen, keyed, its row "key — name — description", and numbered by being listed.
        f.Run("sheet.updateKey", new JsonObject { ["id"] = legend, ["of"] = "seen" });
        var how = DrawingKeys.MaterialRows.Of_(f.Model.Get(legend)!);
        Assert.True(how.Keys);
        var rows = DrawingKeys.Entries(f.Session.Reader, paperView, "materials", null, how);
        var row = Assert.Single(rows, r => r.Material == render);
        Assert.DoesNotContain(rows, r => r.Material == concrete);
        Assert.NotNull(Stored(f, render));
        Assert.Equal(Key(f, render), row.Key);
        Assert.Equal("Lime render — Lime render, white, 2 cm", row.Name);
        var texts = Sheets.Compose(f.Session.Reader, f.Model.Get(s)!).Pens.SelectMany(p => p.Texts).Select(x => x.Text).ToList();
        Assert.Contains(row.Key!, texts);
        Assert.Contains(row.Name, texts);
        // Its own row text, a field left empty taking its dash with it.
        f.Run("sheet.updateKey", new JsonObject { ["id"] = legend, ["row"] = "{name} — {Finish}" });
        rows = DrawingKeys.Entries(f.Session.Reader, paperView, "materials", null, DrawingKeys.MaterialRows.Of_(f.Model.Get(legend)!));
        Assert.Equal("Lime render", rows.Single(r => r.Material == render).Name);
        // Only what the tags read: none yet, then the render once a tag reads it.
        f.Run("sheet.updateKey", new JsonObject { ["id"] = legend, ["of"] = "tagged", ["row"] = null });
        Assert.Empty(DrawingKeys.Entries(f.Session.Reader, paperView, "materials", null, DrawingKeys.MaterialRows.Of_(f.Model.Get(legend)!)));
        f.Run("drafting.addTag", new JsonObject
        {
            ["viewId"] = view, ["element"] = wall, ["field"] = "materialKey", ["at"] = new JsonArray(face.X + 1, face.Y + 1), ["pick"] = new JsonArray(face.X, face.Y),
        });
        rows = DrawingKeys.Entries(f.Session.Reader, paperView, "materials", null, DrawingKeys.MaterialRows.Of_(f.Model.Get(legend)!));
        Assert.Equal(render, Assert.Single(rows).Material);
        Assert.Throws<CommandException>(() => f.Run("sheet.updateKey", new JsonObject { ["id"] = legend, ["of"] = "everything" }));
    }
}
