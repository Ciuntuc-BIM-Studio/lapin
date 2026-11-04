using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The visual joins of one material from element to element (on by default),
/// and the view styles (docs/view-styles.md): which layers a view draws (all,
/// all but the finishes, the cores), whether it joins, whether it draws
/// overhead, per style and per view.
/// </summary>
public sealed class ViewStyleTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    /// <summary>Two strip footings meeting in an L at (6, 0), butt: the second one's core runs through the corner, the first stops at it.</summary>
    static (Fixture F, string A, string B, string Plan) Corner(string? discipline = null)
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var a = (string)f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["joinStyle"] = "butt" })!["id"]!;
        var b = (string)f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 6, ["y1"] = 0, ["x2"] = 6, ["y2"] = 5, ["joinStyle"] = "butt" })!["id"]!;
        var args = new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["foundation"] = true };
        if (discipline != null) args["discipline"] = discipline;
        var plan = (string)f.Run("view.create", args)!["id"]!;
        return (f, a, b, plan);
    }

    static Views.Rendered Render(Fixture f, string view) => Views.Render(new Q(f), Views.Find(f.Model, view)!, print: true);

    /// <summary>The drawn cut and seen segments that cross the stretch from <paramref name="p"/> to <paramref name="q"/> (collinear, overlapping it).</summary>
    static List<(Pen Pen, Vec2 A, Vec2 B)> On(Views.Rendered r, Vec2 p, Vec2 q)
    {
        var d = (q - p).Normalized();
        var found = new List<(Pen, Vec2, Vec2)>();
        foreach (var pen in r.Pens.Where(x => x.Layer is PenLayer.Cut or PenLayer.Seen))
            foreach (var (a, b) in pen.Segments)
            {
                double Off(Vec2 x) => Math.Abs((x - p).X * d.Y - (x - p).Y * d.X);
                if (Off(a) > 1e-6 || Off(b) > 1e-6) continue;
                double T(Vec2 x) => (x - p).X * d.X + (x - p).Y * d.Y;
                var (t0, t1) = (Math.Min(T(a), T(b)), Math.Max(T(a), T(b)));
                if (t1 > 1e-6 && t0 < (q - p).Length - 1e-6) found.Add((pen, a, b));
            }
        return found;
    }

    static List<string> Keys(Fixture f, string view, string element)
    {
        var (d, _, problem) = Views.Generate(new Q(f), Views.Find(f.Model, view)!);
        Assert.True(d != null, problem);
        return d!.Cuts.Where(c => c.Element == element).Select(c => c.Key ?? "").Distinct().Order().ToList();
    }

    [Fact]
    public void One_material_goes_on_round_a_corner_without_a_line_between_the_cores()
    {
        var (f, _, _, plan) = Corner();
        var r = Render(f, plan);
        // Where the first core stops against the one running through: x = 5.85, y −0.15 … 0.15.
        Assert.Empty(On(r, new Vec2(5.85, -0.15), new Vec2(5.85, 0.15)));
        // The running core's side goes on beyond the corner, drawn.
        Assert.NotEmpty(On(r, new Vec2(5.85, 0.2), new Vec2(5.85, 4.9)));
        // The cores' outer face, under the waterproofing, is one line between layers all along: the separation pen, not the contour's.
        var face = On(r, new Vec2(0.1, -0.15), new Vec2(6.15, -0.15));
        Assert.NotEmpty(face);
        Assert.Single(face.Select(x => x.Pen.Width).Distinct());
        Assert.True(face[0].Pen.Width < On(r, new Vec2(0.1, -0.275), new Vec2(6, -0.275))[0].Pen.Width);
    }

    [Fact]
    public void Joins_can_be_turned_off_in_a_view_and_by_a_material()
    {
        var (f, _, _, plan) = Corner();
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["joins"] = false });
        Assert.NotEmpty(On(Render(f, plan), new Vec2(5.85, -0.15), new Vec2(5.85, 0.15)));
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["joins"] = null });
        Assert.Empty(On(Render(f, plan), new Vec2(5.85, -0.15), new Vec2(5.85, 0.15)));
        // A material whose joints are drawn joins nothing, in any view.
        var concrete = Materials.Find(f.Model, "Reinforced concrete")!.Id;
        f.Run("material.update", new JsonObject { ["id"] = concrete, ["drawJoints"] = true });
        Assert.NotEmpty(On(Render(f, plan), new Vec2(5.85, -0.15), new Vec2(5.85, 0.15)));
    }

    [Fact]
    public void A_structural_view_draws_the_cores_alone_an_architectural_one_every_layer()
    {
        var (f, a, _, plan) = Corner();
        Assert.Equal(["layer:0", "layer:1", "layer:2", "layer:3"], Keys(f, plan, a));
        var structural = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["foundation"] = true, ["discipline"] = "Structure" })!["id"]!;
        Assert.Equal(["layer:3"], Keys(f, structural, a));
        // The view sets its own layers over its style's, and takes them back.
        f.Run("view.setStyle", new JsonObject { ["id"] = structural, ["layers"] = "all" });
        Assert.Equal(4, Keys(f, structural, a).Count);
        f.Run("view.setStyle", new JsonObject { ["id"] = structural, ["layers"] = null });
        Assert.Single(Keys(f, structural, a));
        // An architectural plan takes the structural style by name.
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["style"] = "structural" });
        Assert.Equal(["layer:3"], Keys(f, plan, a));
    }

    [Fact]
    public void Without_finishes_a_view_leaves_out_the_finish_layers_by_their_materials_category()
    {
        var f = new Fixture();
        var type = f.Type("Plastered brick", ("Lime plaster", 0.02, false), ("Brick", 0.25, true), ("Mineral wool", 0.1, false), ("Silicate render", 0.01, false));
        var wall = f.Wall(0, 0, 5, 0, type);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["layers"] = "noFinishes" });
        // Plaster and render are finishes by their names; the wool stays.
        Assert.Equal(["layer:1", "layer:2"], Keys(f, plan, wall));
        // A category says otherwise: the render becomes structure, the wool a finish.
        f.Run("material.update", new JsonObject { ["id"] = Materials.Find(f.Model, "Silicate render")!.Id, ["category"] = "Structure" });
        f.Run("material.update", new JsonObject { ["id"] = Materials.Find(f.Model, "Mineral wool")!.Id, ["category"] = "Finish" });
        Assert.Equal(["layer:1", "layer:3"], Keys(f, plan, wall));
        // A section follows its style too.
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(2.5, -1), ["end"] = new JsonArray(2.5, 1) })!["id"]!;
        f.Run("view.setStyle", new JsonObject { ["id"] = section, ["layers"] = "core" });
        Assert.Equal(["layer:1"], Keys(f, section, wall));
    }

    [Fact]
    public void A_view_style_can_leave_out_the_overhead()
    {
        var (f, _, _, plan) = Corner();
        var view = Views.Find(f.Model, plan)!;
        Assert.True(Views.Spec(f.Model, view, []).Spec!.Above > 0);
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["overhead"] = false });
        Assert.Equal(0, Views.Spec(f.Model, Views.Find(f.Model, plan)!, []).Spec!.Above);
    }

    [Fact]
    public void Styles_are_the_projects_made_changed_and_deleted()
    {
        var (f, a, _, plan) = Corner();
        // One per discipline, and Coordination (the openings' voids drawn, decision 290), taken by name.
        Assert.Equal(["architectural", "structural", "mep", "coordination"], ViewStyles.All(f.Model).Select(s => s.Id));
        var made = (string)f.Run("viewStyle.create", new JsonObject { ["from"] = "structural", ["name"] = "Formwork", ["layers"] = "noFinishes" })!["id"]!;
        Assert.Equal(5, ViewStyles.All(f.Model).Count);
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["style"] = made });
        // The footing's protection board is a finish by its name? No: "Protection board" is not; its XPS neither: all its layers draw.
        Assert.Equal(4, Keys(f, plan, a).Count);
        f.Run("viewStyle.update", new JsonObject { ["id"] = made, ["layers"] = "core" });
        Assert.Single(Keys(f, plan, a));
        // The style goes: the view takes its discipline's again (architectural: every layer).
        f.Run("viewStyle.delete", new JsonObject { ["id"] = made });
        Assert.Null(f.Model.Get(plan)!.Str("viewStyle"));
        Assert.Equal(4, Keys(f, plan, a).Count);
        // A style edited is the project's from then on; undo brings the defaults back.
        f.Run("viewStyle.update", new JsonObject { ["id"] = "structural", ["layers"] = "all" });
        Assert.Equal("all", ViewStyles.All(f.Model).Single(s => s.Id == "structural").Layers);
        Assert.Throws<CommandException>(() => f.Run("viewStyle.update", new JsonObject { ["id"] = "structural", ["layers"] = "some" }));
        Assert.Throws<CommandException>(() => f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["style"] = "nope" }));
    }

    /// <summary>
    /// Layered walls meeting in a T and crossing in an X read as one build-up (decision 255): the ending wall's core
    /// runs through the crossed wall's plaster to its core, with no line between the two cores; its plaster meets the
    /// crossed wall's plaster with no line between them; the crossed wall's plaster stops at the core's edges, where a
    /// line stays. The default type: render 20 mm on the right, a 250 mm core, plaster 15 mm on the left.
    /// </summary>
    [Fact]
    public void Walls_meeting_in_a_T_or_crossing_in_an_X_read_as_one_build_up()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        f.Wall(0, 3, 8, 3);
        // Ending on the crossed wall's plaster side (its left, y > 3): its own plaster lies at x 5.125–5.14, its core at 4.875–5.125.
        f.Wall(5, 7, 5, 3);
        // Crossing a second wall at (14, 3): the new one's core at x 13.875–14.125, its plaster at 13.86–13.875.
        f.Wall(10, 3, 18, 3);
        f.Wall(14, 0, 14, 6);
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var r = Render(f, plan);
        // The T: no line where the ending core meets the crossed core (y = 3.125), nor where its plaster meets the crossed plaster (y = 3.14).
        Assert.Empty(On(r, new Vec2(4.875, 3.125), new Vec2(5.125, 3.125)));
        Assert.Empty(On(r, new Vec2(5.125, 3.14), new Vec2(5.14, 3.14)));
        // The crossed wall's plaster stops at the core's edges: a line across it there.
        Assert.NotEmpty(On(r, new Vec2(4.875, 3.125), new Vec2(4.875, 3.14)));
        Assert.NotEmpty(On(r, new Vec2(5.125, 3.125), new Vec2(5.125, 3.14)));
        // And the crossed wall's outer line is whole beyond the ending wall.
        Assert.NotEmpty(On(r, new Vec2(5.2, 3.14), new Vec2(7.9, 3.14)));
        // The X: no line on either core face of the through wall within the crossing core, nor where the plasters meet on each side.
        Assert.Empty(On(r, new Vec2(13.875, 3.125), new Vec2(14.125, 3.125)));
        Assert.Empty(On(r, new Vec2(13.875, 2.875), new Vec2(14.125, 2.875)));
        Assert.Empty(On(r, new Vec2(13.86, 3.14), new Vec2(13.875, 3.14)));
        // The crossing wall's core stops at the through wall's core: no line of its own across the through core.
        Assert.Empty(On(r, new Vec2(13.875, 2.875), new Vec2(13.875, 3.125)));
        Assert.NotEmpty(On(r, new Vec2(13.875, 3.125), new Vec2(13.875, 3.14)));
    }
}
