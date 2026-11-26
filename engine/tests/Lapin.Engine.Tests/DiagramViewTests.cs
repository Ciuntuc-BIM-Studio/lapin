using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The diagram look in 2D views (decision 885, forum topic 283; docs/plan-diagram-2d.md): a plan, a section or an elevation
/// whose style says white or pastel fills what it cuts and what it sees with its categories' soft colours (the 3D Diagram
/// display's palette, the project's), with thin light outlines and no hatches; a colour Visibility / Graphics sets for the view
/// wins; a view that is no diagram draws exactly as before; the choice and the palette are kept in the project.
/// </summary>
public class DiagramViewTests
{
    const string WallsPastel = "#e8d9c4", FloorsPastel = "#cfd8dc";

    /// <summary>A brick-faced wall over a slab, the brick hatched BRICK in elevations; the plan of its storey, a section across, the south elevation.</summary>
    static (Fixture F, string Wall, string Plan, string Section, string Elevation) Model()
    {
        var f = new Fixture();
        var type = f.Type("Brick 40", ("Facing brick", 0.1, false), ("Concrete", 0.2, true), ("Facing brick", 0.1, false));
        var brick = f.Model.OfClass(IfcClass.Material).First(e => e.Name == "Facing brick").Id;
        f.Run("material.update", new JsonObject { ["id"] = brick, ["elevation"] = new JsonObject { ["hatch"] = "BRICK" } });
        var wall = f.Wall(0, 0, 8, 0, type);
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = AnalysisRunTests.Rect(-2, -3, 10, 3) });
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(4, -4), ["end"] = new JsonArray(4, 4) })!["id"]!;
        var elevation = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        return (f, wall, plan, section, elevation);
    }

    static Views.Rendered Render(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);

    static void Diagram(Fixture f, string view, string? mode) => f.Run("view.setStyle", new JsonObject { ["id"] = view, ["diagram"] = mode });

    /// <summary>The model's pens alone (fills, hatches, lines), as text: what a drawing is, byte for byte.</summary>
    static string Text(Views.Rendered r)
    {
        var sb = new StringBuilder();
        foreach (var p in r.Pens)
        {
            sb.Append(p.Layer).Append('|').Append(p.Stroke).Append('|').Append(p.Width.ToString("R")).Append('|').Append(p.Fill).Append('|').Append(p.Dash == null ? "" : string.Join(",", p.Dash)).Append('\n');
            foreach (var l in p.Loops) sb.Append(string.Join(" ", l.Select(v => $"{v.X:R},{v.Y:R}"))).Append('\n');
            foreach (var (a, b) in p.Segments) sb.Append($"{a.X:R},{a.Y:R} {b.X:R},{b.Y:R}\n");
        }
        return sb.ToString();
    }

    static IEnumerable<Pen> Fills(Views.Rendered r, string colour) => r.Pens.Where(p => p.Fill == colour && p.Loops.Count > 0);

    [Fact]
    public void A_plan_in_pastel_fills_its_cut_and_the_floor_it_sees_by_category_with_thin_light_outlines_and_no_hatch()
    {
        var (f, _, plan, _, _) = Model();
        Diagram(f, plan, "pastel");
        var r = Render(f, plan);
        // The wall's cut: one soft area of the walls' pastel, its layers merged, no hatch.
        Assert.Contains(r.Pens, p => p.Layer == PenLayer.Fill && p.Fill == WallsPastel && p.Loops.Count > 0);
        Assert.DoesNotContain(r.Pens, p => p.Layer == PenLayer.Hatch && p.Segments.Count > 0);
        // The slab seen below the cut: the floors' pastel, on the surfaces' fill layer, under the cut.
        var floor = r.Pens.FindIndex(p => p.Layer == SurfaceHatches.FillLayer && p.Fill == FloorsPastel);
        Assert.True(floor >= 0);
        Assert.True(floor < r.Pens.FindIndex(p => p.Layer == PenLayer.Fill && p.Fill == WallsPastel));
        // Thin light lines: the cut 0.25 mm mid grey, nothing in black ink of the technical drawing's 0.5 mm.
        var cut = r.Pens.Where(p => p.Layer == PenLayer.Cut && p.Segments.Count > 0).ToList();
        Assert.NotEmpty(cut);
        Assert.All(cut, p => { Assert.Equal(DiagramLook.CutColor, p.Stroke); Assert.Equal(DiagramLook.CutWidth, p.Width); });
        Assert.All(r.Pens.Where(p => p.Layer == PenLayer.Seen && p.Segments.Count > 0), p => Assert.Equal(DiagramLook.SeenWidth, p.Width));
    }

    [Fact]
    public void The_white_model_fills_every_category_alike()
    {
        var (f, _, plan, _, elevation) = Model();
        Diagram(f, plan, "white");
        Diagram(f, elevation, "white");
        var r = Render(f, plan);
        Assert.NotEmpty(Fills(r, DiagramLook.WhiteModel));
        Assert.Empty(Fills(r, WallsPastel));
        Assert.Empty(Fills(r, FloorsPastel));
        Assert.NotEmpty(Fills(Render(f, elevation), DiagramLook.WhiteModel));
    }

    [Fact]
    public void A_section_and_an_elevation_fill_their_cut_and_the_faces_they_see_without_surface_hatches()
    {
        var (f, _, _, section, elevation) = Model();
        // As a technical drawing the elevation hatches the brick.
        Assert.Contains(Render(f, elevation).Pens, p => p.Layer == SurfaceHatches.Layer && p.Segments.Count > 0);
        Diagram(f, elevation, "pastel");
        var e = Render(f, elevation);
        Assert.Contains(e.Pens, p => p.Layer == SurfaceHatches.FillLayer && p.Fill == WallsPastel && p.Loops.Count > 0);
        Assert.DoesNotContain(e.Pens, p => p.Layer == SurfaceHatches.Layer && p.Segments.Count > 0);
        // The view keeps its hatches when it turns them on itself, over the fill.
        f.Run("view.setStyle", new JsonObject { ["id"] = elevation, ["surfaceHatch"] = true });
        var kept = Render(f, elevation);
        Assert.Contains(kept.Pens, p => p.Layer == SurfaceHatches.Layer && p.Segments.Count > 0);
        Assert.Contains(kept.Pens, p => p.Layer == SurfaceHatches.FillLayer && p.Fill == WallsPastel);
        Diagram(f, section, "pastel");
        var s = Render(f, section);
        Assert.Contains(s.Pens, p => p.Layer == PenLayer.Fill && p.Fill == WallsPastel && p.Loops.Count > 0);
        Assert.Contains(s.Pens, p => p.Layer == PenLayer.Fill && p.Fill == FloorsPastel && p.Loops.Count > 0);
        Assert.DoesNotContain(s.Pens, p => p.Layer == PenLayer.Hatch && p.Segments.Count > 0);
    }

    [Fact]
    public void A_colour_set_in_visibility_graphics_wins_over_the_palette()
    {
        var (f, _, plan, _, elevation) = Model();
        Diagram(f, plan, "pastel");
        Diagram(f, elevation, "pastel");
        foreach (var v in new[] { plan, elevation })
            f.Run("view.graphics", new JsonObject { ["id"] = v, ["category"] = "walls", ["value"] = new JsonObject { ["fillColor"] = "#ff00ff", ["lineColor"] = "#0000ff" } });
        var r = Render(f, plan);
        Assert.NotEmpty(Fills(r, "#ff00ff"));
        Assert.Empty(Fills(r, WallsPastel));
        // The category's line colour too, over the diagram's grey.
        Assert.Contains(r.Pens, p => p.Layer == PenLayer.Cut && p.Stroke == "#0000ff");
        // The slab keeps its pastel: only the walls were set.
        Assert.NotEmpty(Fills(r, FloorsPastel));
        var e = Render(f, elevation);
        Assert.Contains(e.Pens, p => p.Layer == SurfaceHatches.FillLayer && p.Fill == "#ff00ff");
        Assert.DoesNotContain(e.Pens, p => p.Layer == SurfaceHatches.FillLayer && p.Fill == WallsPastel);
    }

    [Fact]
    public void A_view_that_is_no_diagram_draws_exactly_as_before()
    {
        var (f, _, plan, section, elevation) = Model();
        var views = new[] { plan, section, elevation };
        var before = views.Select(v => Text(Render(f, v))).ToList();
        Assert.All(before, t => Assert.DoesNotContain(DiagramLook.CutColor, t));
        foreach (var v in views) Diagram(f, v, "pastel");
        Assert.All(views.Select((v, i) => (v, i)), x => Assert.NotEqual(before[x.i], Text(Render(f, x.v))));
        // Back to the style's (none), and set off explicitly: byte for byte the drawing it was.
        foreach (var v in views) Diagram(f, v, null);
        Assert.Equal(before, views.Select(v => Text(Render(f, v))).ToList());
        foreach (var v in views) Diagram(f, v, "off");
        Assert.Equal(before, views.Select(v => Text(Render(f, v))).ToList());
        // Nothing is written for a style that is no diagram.
        var styles = Spatial.Project(f.Model)!.Attrs[ViewStyles.Key]?.ToJsonString() ?? "";
        Assert.DoesNotContain("diagram", styles);
    }

    [Fact]
    public void A_style_says_it_for_its_views_and_a_view_sets_its_own_over_it()
    {
        var (f, _, plan, _, elevation) = Model();
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["diagram"] = "pastel" });
        Assert.NotEmpty(Fills(Render(f, elevation), WallsPastel));
        Assert.Equal("pastel", (string?)f.Session.Query("view.style", new JsonObject { ["id"] = plan })!["resolved"]!["diagram"]);
        // The style's diagram turns its hatches off in the views as they resolve them.
        Assert.False((bool)f.Session.Query("view.style", new JsonObject { ["id"] = plan })!["resolved"]!["floorHatch"]!);
        // The plan stays a technical drawing.
        Diagram(f, plan, "off");
        var r = Render(f, plan);
        Assert.Empty(Fills(r, WallsPastel));
        Assert.Contains(r.Pens, p => p.Layer == PenLayer.Cut && p.Stroke == "#000000");
        Assert.Throws<CommandException>(() => Diagram(f, plan, "sepia"));
    }

    [Fact]
    public void The_palette_is_the_projects_one_undo_step_and_kept_with_the_choice_in_the_package()
    {
        var (f, _, plan, _, _) = Model();
        Diagram(f, plan, "pastel");
        f.Run("diagram.setPalette", new JsonObject { ["palette"] = new JsonObject { ["walls"] = "#123456" } });
        Assert.NotEmpty(Fills(Render(f, plan), "#123456"));
        var q = f.Session.Query("diagram.palette", [])!;
        Assert.Equal("#123456", (string?)q["palette"]!["walls"]);
        Assert.Equal(FloorsPastel, (string?)q["palette"]!["floors"]);
        Assert.Equal("#123456", (string?)q["own"]!["walls"]);
        f.Session.Undo();
        Assert.NotEmpty(Fills(Render(f, plan), WallsPastel));
        f.Session.Redo();
        Assert.Throws<CommandException>(() => f.Run("diagram.setPalette", new JsonObject { ["palette"] = new JsonObject { ["bricks"] = "#123456" } }));
        Assert.Throws<CommandException>(() => f.Run("diagram.setPalette", new JsonObject { ["palette"] = new JsonObject { ["walls"] = "red" } }));
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        ms.Position = 0;
        var content = LapinPackage.Read(ms);
        var g = new Fixture();
        g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Assert.Equal("pastel", (string?)g.Session.Query("view.style", new JsonObject { ["id"] = plan })!["resolved"]!["diagram"]);
        Assert.NotEmpty(Fills(Render(g, plan), "#123456"));
        // The default again: nothing kept on the project.
        g.Run("diagram.setPalette", new JsonObject { ["reset"] = true });
        Assert.Null(Spatial.Project(g.Model)!.Attrs[DiagramLook.Key]);
    }

    [Fact]
    public void The_categories_and_the_palette_are_the_3d_diagrams()
    {
        var file = UiDiagram();
        var text = File.ReadAllText(file);
        var palette = Regex.Match(text, @"DEFAULT_PALETTE[^=]*=\s*\{([^}]*)\}").Groups[1].Value;
        foreach (Match m in Regex.Matches(palette, @"(\w+):\s*'(#[0-9a-f]{6})'"))
            Assert.Equal(DiagramLook.Defaults[m.Groups[1].Value], m.Groups[2].Value);
        Assert.Equal(DiagramLook.Defaults.Count, Regex.Matches(palette, @"(\w+):\s*'#").Count);
        Assert.Contains($"WHITE_MODEL = '{DiagramLook.WhiteModel}'", text);
        var classes = Regex.Match(text, @"OF_CLASS[^=]*=\s*\{([^}]*)\}").Groups[1].Value;
        var count = 0;
        foreach (Match m in Regex.Matches(classes, @"(Ifc\w+):\s*'(\w+)'"))
        {
            Assert.Equal(m.Groups[2].Value, DiagramLook.CategoryOf(m.Groups[1].Value));
            count++;
        }
        Assert.True(count > 20);
        foreach (var skipped in new[] { "IfcSpace", "IfcOpeningElement", "IfcAnnotation", "IfcGrid", "IfcVirtualElement" })
        {
            Assert.Contains($"'{skipped}'", text);
            Assert.Null(DiagramLook.CategoryOf(skipped));
        }
        Assert.Equal("greenery", DiagramLook.CategoryOf("IfcGeographicElement", "TREE"));
        Assert.Equal("site", DiagramLook.CategoryOf("IfcGeographicElement", "TERRAIN"));
        Assert.Equal("other", DiagramLook.CategoryOf("IfcBuildingElementProxy"));
    }

    static string UiDiagram()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "ui", "src", "viewport", "diagram.ts");
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException("ui/src/viewport/diagram.ts was not found above the tests.");
    }
}
