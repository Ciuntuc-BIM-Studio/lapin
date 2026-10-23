using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Materials and drafting standards. Ported from the old Lapin's
/// materials-views.test.ts (the row now being the IfcMaterial's own
/// attributes), styles.test.ts and standards-file.test.ts (same cases).
/// </summary>
public class DraftingResourcesTests
{
    static Entity Mat(string name, JsonObject? attrs = null) => new("m", IfcClass.Material, name, attrs ?? []);

    // ------------------------------------------------------------ materials per kind of view

    [Fact]
    public void A_material_s_own_graphics_come_before_its_name_and_a_plan_falls_back_to_the_section()
    {
        var own = Mat("Beton C25", new JsonObject
        {
            ["color"] = "#808080",
            ["section"] = new JsonObject { ["hatch"] = "CONCRETE", ["hatchScale"] = 2, ["fill"] = "#eeeeee" },
            ["elevation"] = new JsonObject { ["hatch"] = "LINES45" },
        });
        var s = MaterialGraphics.Resolve(own, "section");
        Assert.Equal("own", s.Source);
        Assert.Equal(("CONCRETE", 2.0, "#eeeeee", "#808080"), (s.Hatch, s.HatchScale, s.Fill, s.Color));
        Assert.Equal("CONCRETE", MaterialGraphics.Resolve(own, "plan").Hatch);
        Assert.Equal("LINES45", MaterialGraphics.Resolve(own, "elevation").Hatch);
        // Nothing of its own: the name; and an elevation draws nothing unless told.
        Assert.Equal("CONCRETE", MaterialGraphics.Resolve(Mat("Beton C25"), "section").Hatch);
        Assert.Equal("name", MaterialGraphics.Resolve(Mat("Beton C25"), "section").Source);
        Assert.Null(MaterialGraphics.Resolve(Mat("Beton C25"), "elevation").Hatch);
    }

    [Fact]
    public void A_line_hatching_read_from_IFC_maps_onto_a_library_pattern()
    {
        Assert.Equal("LINES45", MaterialGraphics.FromIfcHatch(null, null, 45, null)!.Hatch);
        Assert.Equal("HORIZONTAL", MaterialGraphics.FromIfcHatch(null, null, 180, null)!.Hatch);
        Assert.Equal(("CONCRETE", 2.0, "#eeeeee"), MaterialGraphics.FromIfcHatch("concrete", 2, null, "#eeeeee") is { } g ? (g.Hatch, g.HatchScale!.Value, g.Fill) : default);
        Assert.Equal("BRICK", MaterialGraphics.FromIfcHatch("Masonry hatch", null, null, null)!.Hatch);
    }

    [Fact]
    public void Names_suggest_hatches_and_membranes()
    {
        Assert.Equal("BRICK", MaterialGraphics.Resolve(Mat("Cărămidă")).Hatch);
        Assert.Equal("INSULATION", MaterialGraphics.Resolve(Mat("EPS 100")).Hatch);
        Assert.Equal("WOOD", MaterialGraphics.Resolve(Mat("Timber")).Hatch);
        Assert.True(MaterialGraphics.Resolve(Mat("Vapour barrier")).Membrane);
        Assert.Null(MaterialGraphics.Resolve(Mat("Vapour barrier")).Hatch);
        Assert.Null(MaterialGraphics.Resolve(Mat("Plaster")).Hatch);
        Assert.Null(MaterialGraphics.Resolve(Mat("Unobtainium")).Hatch);
    }

    [Fact]
    public void A_material_takes_its_graphics_a_plan_of_its_own_and_clears_back()
    {
        var f = new Fixture();
        var brick = (string)f.Run("material.create", new JsonObject { ["name"] = "Brick", ["color"] = "#aa5533" })!["id"]!;
        Assert.Equal("BRICK", MaterialGraphics.Resolve(f.Model.Get(brick), "section").Hatch);
        f.Run("material.update", new JsonObject { ["id"] = brick, ["plan"] = new JsonObject { ["hatch"] = "CROSS" } });
        Assert.Equal("CROSS", MaterialGraphics.Resolve(f.Model.Get(brick), "plan").Hatch);
        Assert.Equal("BRICK", MaterialGraphics.Resolve(f.Model.Get(brick), "section").Hatch);
        // A fill only when asked: "material" follows its colour.
        Assert.Null(MaterialGraphics.Resolve(f.Model.Get(brick), "section").Fill);
        f.Run("material.update", new JsonObject { ["id"] = brick, ["section"] = new JsonObject { ["fill"] = "material" } });
        Assert.Equal("#aa5533", MaterialGraphics.Resolve(f.Model.Get(brick), "section").Fill);
        f.Run("material.update", new JsonObject { ["id"] = brick, ["plan"] = null });
        Assert.Equal("BRICK", MaterialGraphics.Resolve(f.Model.Get(brick), "plan").Hatch);
        // An explicit none, then back to the name.
        f.Run("material.update", new JsonObject { ["id"] = brick, ["section"] = new JsonObject { ["hatch"] = null } });
        Assert.Null(MaterialGraphics.Resolve(f.Model.Get(brick), "section").Hatch);
        f.Run("material.reset", new JsonObject { ["id"] = brick });
        Assert.Equal("BRICK", MaterialGraphics.Resolve(f.Model.Get(brick), "section").Hatch);
        Assert.Equal("#aa5533", f.Model.Get(brick)!.Str("color"));
        // Unknown patterns and bad values are refused.
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = brick, ["section"] = new JsonObject { ["hatch"] = "NOPE" } }));
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = brick, ["section"] = new JsonObject { ["hatchScale"] = 0 } }));
        Assert.Throws<CommandException>(() => f.Run("material.update", new JsonObject { ["id"] = brick, ["section"] = new JsonObject { ["fill"] = "red" } }));
    }

    [Fact]
    public void The_material_s_appearance_is_what_IFC_gets()
    {
        var f = new Fixture();
        var brick = (string)f.Run("material.create", new JsonObject { ["name"] = "Brick", ["color"] = "#aa5533" })!["id"]!;
        f.Run("material.update", new JsonObject { ["id"] = brick, ["section"] = new JsonObject { ["hatchScale"] = 1.5 }, ["elevation"] = new JsonObject { ["hatch"] = "BRICK" } });
        var a = MaterialGraphics.Appearance(f.Model.Get(brick)!)!;
        Assert.True(JsonNode.DeepEquals(new JsonObject
        {
            ["color"] = "#aa5533",
            ["hatches"] = new JsonObject { ["section"] = new JsonObject { ["pattern"] = "BRICK", ["scale"] = 1.5 }, ["elevation"] = new JsonObject { ["pattern"] = "BRICK" } },
        }, a), a.ToJsonString());
    }

    [Fact]
    public void A_material_in_use_cannot_be_deleted_an_unused_one_can()
    {
        var f = new Fixture();
        f.Wall(0, 0, 5, 0);
        var used = f.Model.OfClass(IfcClass.Material).First();
        Assert.Throws<CommandException>(() => f.Run("material.delete", new JsonObject { ["id"] = used.Id }));
        var spare = (string)f.Run("material.create", new JsonObject { ["name"] = "Spare" })!["id"]!;
        f.Run("material.delete", new JsonObject { ["id"] = spare });
        Assert.Null(f.Model.Get(spare));
        var list = f.Session.Query("material.list", []) as JsonArray;
        Assert.Contains(list!, m => (bool)m!["inUse"]! && (string)m["section"]!["hatch"]! is not null);
    }

    // ------------------------------------------------------------ hatch patterns of the project

    [Fact]
    public void Imported_patterns_join_the_library_override_built_ins_and_undo()
    {
        var f = new Fixture();
        var r = f.Run("hatch.import", new JsonObject { ["text"] = "*MINE, Mine\n0, 0,0, 0,2\n*BRICK, my brick\n0, 0,0, 0,5\n*BAD\nx" })!;
        Assert.Equal(3, (int)r["count"]!);   // MINE, BRICK and BAD (no families: solid)
        Assert.Single(r["errors"]!.AsArray());
        var names = ((JsonArray)f.Session.Query("hatch.list", [])!).Select(x => ((string)x!["name"]!, (bool)x["project"]!)).ToList();
        Assert.Contains(("MINE", true), names);
        Assert.Contains(("BRICK", true), names);
        Assert.DoesNotContain(("BRICK", false), names);
        var brick = (string)f.Run("material.create", new JsonObject { ["name"] = "Clay" })!["id"]!;
        f.Run("material.update", new JsonObject { ["id"] = brick, ["section"] = new JsonObject { ["hatch"] = "mine" } });
        Assert.Equal("MINE", MaterialGraphics.Resolve(f.Model.Get(brick), "section").Hatch);
        var preview = f.Session.Query("hatch.preview", new JsonObject { ["pattern"] = "MINE", ["size"] = 10 })!;
        Assert.Equal(5 * 4, preview["segments"]!.AsArray().Count);      // lines at y = 0, 2, 4, 6, 8
        Assert.Throws<CommandException>(() => f.Run("hatch.import", new JsonObject { ["text"] = "nothing" }));
        f.Session.Undo(); f.Session.Undo(); f.Session.Undo();
        Assert.DoesNotContain(((JsonArray)f.Session.Query("hatch.list", [])!), x => (string)x!["name"]! == "MINE");
    }

    // ------------------------------------------------------------ styles and overrides (styles.test.ts)

    static readonly StyleBook Book = Standards.Read(new JsonObject
    {
        ["textStyles"] = new JsonArray(new JsonObject { ["id"] = "big", ["name"] = "Big", ["height"] = 5, ["bold"] = true }),
        ["dimStyles"] = new JsonArray(new JsonObject { ["id"] = "arch", ["name"] = "Arch", ["textStyle"] = "big", ["unit"] = "mm", ["precision"] = 0 }),
        ["axisStyles"] = new JsonArray(new JsonObject { ["id"] = "grid", ["name"] = "Grid", ["textStyle"] = "big", ["bubble"] = "square", ["color"] = "#cc0000" }),
    });

    [Fact]
    public void Text_reads_left_to_right_and_bottom_to_top_when_vertical()
    {
        Assert.Equal(0, Standards.ReadableDeg(0));
        Assert.Equal(0, Standards.ReadableDeg(180));
        Assert.Equal(-90, Standards.ReadableDeg(90));
        Assert.Equal(-90, Standards.ReadableDeg(-90));
        Assert.Equal(-45, Standards.ReadableDeg(135));
        Assert.Equal(45, Standards.ReadableDeg(-135));
    }

    [Fact]
    public void An_annotation_takes_its_style_then_its_own_overrides()
    {
        Assert.Equal(2.5, Standards.ResolveText(Book, []).Height);
        Assert.True(Standards.ResolveText(Book, new JsonObject { ["textStyle"] = "big" }).Bold);
        Assert.False(Standards.ResolveText(Book, new JsonObject { ["textStyle"] = "big", ["o.bold"] = false }).Bold);
        // A dimension's text follows its dimension style's text style.
        Assert.Equal(5, Standards.ResolveText(Book, new JsonObject { ["dimStyle"] = "arch" }).Height);
        Assert.Equal("below", Standards.ResolveDim(Book, new JsonObject { ["dimStyle"] = "arch", ["o.placement"] = "below" }).Placement);
        Assert.Equal("standard", Standards.ResolveDim(Book, new JsonObject { ["dimStyle"] = "gone" }).Id);
        // An axis: its label's text style, then the axis's overrides.
        Assert.Equal(5, Standards.ResolveText(Book, new JsonObject { ["axisStyle"] = "grid" }).Height);
        Assert.Equal("square", Standards.ResolveAxis(Book, new JsonObject { ["axisStyle"] = "grid" }).Bubble);
        Assert.Equal("circle", Standards.ResolveAxis(Book, new JsonObject { ["axisStyle"] = "grid", ["o.bubble"] = "circle", ["o.size"] = 14 }).Bubble);
        Assert.Equal(14, Standards.ResolveAxis(Book, new JsonObject { ["axisStyle"] = "grid", ["o.size"] = 14 }).Size);
        Assert.Equal("#00ff00", Standards.ResolveAxis(Book, new JsonObject { ["axisStyle"] = "grid", ["o.lineColor"] = "#00ff00" }).Color);
        Assert.Equal("standard", Standards.ResolveAxis(Book, new JsonObject { ["axisStyle"] = "gone" }).Id);
        Assert.Equal("standard", Standards.ResolveAxis(StyleBook.Default, []).Id);
    }

    [Fact]
    public void Dimensions_format_in_their_style_s_unit_and_precision()
    {
        Assert.Equal("3.25", Standards.FormatDimension(3.2456, "m", 2));
        Assert.Equal("3246", Standards.FormatDimension(3.2456, "mm", 0));
        Assert.Equal("324.6", Standards.FormatDimension(3.2456, "cm", 1));
    }

    [Fact]
    public void A_layer_shows_when_it_its_group_its_view_and_its_viewport_all_show_it()
    {
        var layer = new DraftLayer("a", "A", "#000000", true, false, "g");
        Assert.True(Standards.LayerShows(layer, [new LayerGroup("g", "G", true, false)]));
        Assert.False(Standards.LayerShows(layer, [new LayerGroup("g", "G", false, false)]));
        Assert.False(Standards.LayerShows(layer with { Visible = false }, []));
        Assert.False(Standards.LayerShows(layer, [], ["a"]));
        Assert.False(Standards.LayerShows(layer, [], null, ["a"]));
        Assert.True(Standards.LayerShows(layer, [], ["b"], []));
    }

    // ------------------------------------------------------------ standards in a project file (standards-file.test.ts)

    [Fact]
    public void Axis_types_are_read_tolerantly_and_Standard_is_always_there()
    {
        var axes = Standards.Read(new JsonObject
        {
            ["axisStyles"] = new JsonArray(
                new JsonObject { ["id"] = "grid", ["name"] = "Grid", ["textStyle"] = "big", ["bubble"] = "square", ["ends"] = "start", ["size"] = 12, ["lineType"] = "dashed", ["color"] = "#ff0000", ["firstLabel"] = "A" },
                new JsonObject { ["id"] = "odd", ["bubble"] = "hexagon", ["ends"] = "middle", ["size"] = -3, ["lineType"] = "zigzag", ["color"] = "red", ["firstLabel"] = "  " },
                "not an object"),
        }).AxisStyles;
        Assert.Equal(["standard", "grid", "odd"], axes.Select(a => a.Id));
        Assert.Equal(new AxisStyle("grid", "Grid", "big", "square", "start", 12, "dashed", "A", "#ff0000"), axes[1]);
        var odd = axes[2];
        Assert.Equal(("circle", "both", Standards.DefaultAxis.Size, "dashdot", "1", (string?)null), (odd.Bubble, odd.Ends, odd.Size, odd.LineType, odd.FirstLabel, odd.Color));
        Assert.Equal([Standards.DefaultAxis], Standards.Read(new JsonObject()).AxisStyles);
    }

    [Fact]
    public void The_default_axis_type_falls_back_to_Standard_and_tag_types_are_read_tolerantly()
    {
        var axisStyles = new JsonArray(new JsonObject { ["id"] = "grid", ["name"] = "Grid" });
        Assert.Equal("grid", Standards.Read(new JsonObject { ["axisStyles"] = axisStyles.DeepClone(), ["defaultAxisStyle"] = "grid" }).DefaultAxisStyle);
        Assert.Equal("standard", Standards.Read(new JsonObject { ["axisStyles"] = axisStyles.DeepClone(), ["defaultAxisStyle"] = "gone" }).DefaultAxisStyle);
        Assert.Equal("standard", Standards.Read(new JsonObject()).DefaultAxisStyle);
        var tags = Standards.Read(new JsonObject
        {
            ["tagTypes"] = new JsonArray(
                new JsonObject { ["id"] = "t1", ["name"] = "Wall", ["template"] = "{Name} {Pset_WallCommon.FireRating}", ["classes"] = new JsonArray("IfcWall", 3, " ") },
                new JsonObject { ["id"] = "t2", ["template"] = "{Tag}" },
                new JsonObject { ["name"] = "no id", ["template"] = "{Tag}" },
                new JsonObject { ["id"] = "t3", ["name"] = "no template" }),
        }).TagTypes;
        Assert.Equal([new TagType("t1", "Wall", "{Name} {Pset_WallCommon.FireRating}", ["IfcWall"]), new TagType("t2", "Tag", "{Tag}", [])], tags);
    }

    // ------------------------------------------------------------ standards as commands

    [Fact]
    public void Standards_are_set_and_removed_in_the_project_and_what_used_a_style_falls_back()
    {
        var f = new Fixture();
        var big = (string)f.Run("standards.set", new JsonObject { ["kind"] = "text", ["entry"] = new JsonObject { ["name"] = "Big", ["height"] = 5 } })!["id"]!;
        var arch = (string)f.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = new JsonObject { ["id"] = "arch", ["name"] = "Arch", ["textStyle"] = big, ["unit"] = "mm" } })!["id"]!;
        f.Run("standards.set", new JsonObject { ["kind"] = "axis", ["entry"] = new JsonObject { ["id"] = "grid", ["bubble"] = "square" }, ["default"] = true });
        var book = DraftingResources.Book(f.Model);
        Assert.Equal(["standard", big], book.TextStyles.Select(t => t.Id));
        Assert.Equal(5, Standards.ResolveText(book, new JsonObject { ["dimStyle"] = arch }).Height);
        Assert.Equal("grid", book.DefaultAxisStyle);
        // Changing one field keeps the others.
        f.Run("standards.set", new JsonObject { ["kind"] = "dim", ["entry"] = new JsonObject { ["id"] = "arch", ["precision"] = 0 } });
        Assert.Equal(("mm", 0), (Standards.DimOf(DraftingResources.Book(f.Model), "arch").Unit, Standards.DimOf(DraftingResources.Book(f.Model), "arch").Precision));
        // The text style goes: the dimension style falls back to Standard; the axis type goes: the default too.
        f.Run("standards.remove", new JsonObject { ["kind"] = "text", ["id"] = big });
        f.Run("standards.remove", new JsonObject { ["kind"] = "axis", ["id"] = "grid" });
        book = DraftingResources.Book(f.Model);
        Assert.Equal("standard", Standards.DimOf(book, "arch").TextStyle);
        Assert.Equal("standard", book.DefaultAxisStyle);
        Assert.Throws<CommandException>(() => f.Run("standards.remove", new JsonObject { ["kind"] = "text", ["id"] = "standard" }));
        Assert.Throws<CommandException>(() => f.Run("standards.remove", new JsonObject { ["kind"] = "layer", ["id"] = "0" }));
        // A layer group goes: its layers stay, ungrouped.
        f.Run("standards.set", new JsonObject { ["kind"] = "layerGroup", ["entry"] = new JsonObject { ["id"] = "g", ["name"] = "Walls" } });
        f.Run("standards.set", new JsonObject { ["kind"] = "layer", ["entry"] = new JsonObject { ["id"] = "A-WALL", ["group"] = "g", ["lineWeight"] = 0.5 } });
        f.Run("standards.remove", new JsonObject { ["kind"] = "layerGroup", ["id"] = "g" });
        Assert.Null(DraftingResources.Book(f.Model).Layers.Single(l => l.Id == "A-WALL").Group);
        // One undo step each.
        f.Session.Undo();
        Assert.Equal("g", DraftingResources.Book(f.Model).Layers.Single(l => l.Id == "A-WALL").Group);
    }
}
