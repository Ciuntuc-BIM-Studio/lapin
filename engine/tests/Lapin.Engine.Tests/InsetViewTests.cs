using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Inset views anywhere (decision 576): any 2D view drawn inside any other, not only on rebar's sections, at the source's
/// scale or a scale of its own, cut to a crop of its own, live (it follows the model), with what is drafted on the source
/// seen and edited through it; printed on sheets and in DXF as the view shows it, the source's screen-only marks left out.
/// </summary>
public class InsetViewTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Id(JsonNode? n) => (string)n!["id"]!;
    static JsonObject Drawing(Fixture f, string view) => (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = view })!;
    static JsonNode Mark(JsonObject d, string id) => ((JsonArray)d["marks"]!).First(m => (string?)m!["id"] == id)!;
    static double[] Segs(JsonNode mark) => ((JsonArray)mark["segments"]!).Select(x => (double)x!).ToArray();

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    /// <summary>A 4 m wall along x on the storey, a section across it at x = 2 (looking +x), its storey's plan as the host.</summary>
    static (Fixture F, string Wall, string Section, string Plan) Setup()
    {
        var f = F();
        var wall = f.Wall(0, 0, 4, 0, height: 3);
        var section = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(2, -2), ["end"] = J(2, 2), ["scale"] = 20 }));
        return (f, wall, section, "plan:" + f.Storey);
    }

    [Fact]
    public void A_section_inset_in_a_plan_draws_round_its_point_at_its_own_scale_and_follows_the_model()
    {
        var (f, wall, section, plan) = Setup();
        var inset = Id(f.Run("view.inset", new JsonObject { ["viewId"] = plan, ["source"] = section, ["at"] = J(20, 0) }));
        var mark = Mark(Drawing(f, plan), inset);
        // The plan at 1:100, the section at 1:20: the section's own paper size, five times its model size in the plan.
        Assert.Equal(5.0, (double)mark["data"]!["factor"]!, 9);
        Assert.Equal(20.0, (double)mark["data"]!["scale"]!, 6);
        Assert.Equal(section, (string?)mark["data"]!["source"]);
        var before = Segs(mark);
        Assert.True(before.Length >= 8);
        // Its own scale, 1:50: twice the plan's size.
        f.Run("view.updateInset", new JsonObject { ["id"] = inset, ["scale"] = 50 });
        Assert.Equal(2.0, (double)Mark(Drawing(f, plan), inset)["data"]!["factor"]!, 9);
        Assert.Equal(50.0, f.Model.Get(inset)!.Num("scale"), 9);
        // A factor given stands for the scale; no scale given is the source's again.
        f.Run("view.updateInset", new JsonObject { ["id"] = inset, ["factor"] = 3 });
        Assert.Null(f.Model.Get(inset)!.Attrs["scale"]);
        f.Run("view.updateInset", new JsonObject { ["id"] = inset, ["factor"] = null, ["scale"] = null });
        Assert.Equal(5.0, (double)Mark(Drawing(f, plan), inset)["data"]!["factor"]!, 9);
        // Live: the wall made higher draws higher in the inset.
        var h0 = Height(Segs(Mark(Drawing(f, plan), inset)));
        f.Run("element.update", new JsonObject { ["id"] = wall, ["attrs"] = new JsonObject { ["height"] = 4 } });
        Assert.True(Height(Segs(Mark(Drawing(f, plan), inset))) > h0 + 2);
    }

    static double Height(double[] s)
    {
        var ys = s.Where((_, i) => i % 2 == 1).ToArray();
        return ys.Max() - ys.Min();
    }

    [Fact]
    public void Its_own_crop_cuts_the_inset_and_a_crop_round_what_it_shows_is_taken_when_asked()
    {
        var (f, _, section, plan) = Setup();
        var inset = Id(f.Run("view.inset", new JsonObject { ["viewId"] = plan, ["source"] = section, ["at"] = J(20, 0), ["frame"] = false }));
        // Round what it shows now.
        f.Run("view.updateInset", new JsonObject { ["id"] = inset, ["crop"] = true });
        Assert.NotNull(Insets.CropOf(f.Model.Get(inset)!));
        // The lower metre of the section only (its coordinates: across the wall, and up from the storey).
        f.Run("view.updateInset", new JsonObject { ["id"] = inset, ["crop"] = new JsonObject { ["min"] = J(-1, -0.5), ["max"] = J(1, 1) } });
        var mark = Mark(Drawing(f, plan), inset);
        var b = ((JsonArray)mark["data"]!["bounds"]!).Select(x => (double)x!).ToArray();
        // Five times its size: 10 m by 7.5 m round the point, nothing drawn past it.
        Assert.Equal(10.0, b[2] - b[0], 5);
        Assert.Equal(7.5, b[3] - b[1], 5);
        var s = Segs(mark);
        for (var i = 0; i < s.Length; i += 2) { Assert.InRange(s[i], b[0] - 1e-6, b[2] + 1e-6); Assert.InRange(s[i + 1], b[1] - 1e-6, b[3] + 1e-6); }
        Assert.NotNull(mark["data"]!["crop"]);
        // Off again.
        f.Run("view.updateInset", new JsonObject { ["id"] = inset, ["crop"] = false });
        Assert.Null(f.Model.Get(inset)!.Attrs["crop"]);
        Assert.Throws<CommandException>(() => f.Run("view.updateInset", new JsonObject { ["id"] = inset, ["crop"] = new JsonObject { ["min"] = J(0, 0), ["max"] = J(0.001, 1) } }));
    }

    [Fact]
    public void A_line_drafted_on_the_source_shows_through_the_inset_as_its_own_mark_with_the_mapping_it_came_by()
    {
        var (f, _, section, plan) = Setup();
        var line = f.Run("drafting.draw", new JsonObject { ["viewId"] = section, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = J(-1, 0.5), ["b"] = J(1, 0.5) }) })!["ids"]![0]!.GetValue<string>();
        var inset = Id(f.Run("view.inset", new JsonObject { ["viewId"] = plan, ["source"] = section, ["at"] = J(20, 0) }));
        var d = Drawing(f, plan);
        var own = ((JsonArray)d["marks"]!).Single(m => (string?)m!["id"] == line)!;
        var via = own["data"]!["via"]!;
        Assert.Equal(inset, (string?)via["inset"]);
        Assert.Equal(section, (string?)via["source"]);
        Assert.Equal(plan, (string?)via["host"]);
        // Mapped: the 2 m line is 10 m long in the plan, round the inset's point.
        var s = Segs(own);
        Assert.Equal(10.0, Math.Abs(s[2] - s[0]), 4);
        Assert.InRange((s[0] + s[2]) / 2, 15, 25);
        // An inset never draws its source's own insets (depth 1), and a view cannot show itself.
        f.Run("view.inset", new JsonObject { ["viewId"] = section, ["source"] = plan, ["at"] = J(10, 0) });
        Assert.Throws<CommandException>(() => f.Run("view.inset", new JsonObject { ["viewId"] = plan, ["source"] = plan, ["at"] = J(0, 0) }));
        Assert.Throws<CommandException>(() => f.Run("view.inset", new JsonObject { ["viewId"] = plan, ["source"] = "3d", ["at"] = J(0, 0) }));
        Assert.DoesNotContain((JsonArray)Drawing(f, plan)["marks"]!, m => (string?)m!["data"]?["via"]?["host"] == section);
    }

    [Fact]
    public void Making_an_inset_is_one_undo_step()
    {
        var (f, _, section, plan) = Setup();
        var inset = Id(f.Run("view.inset", new JsonObject { ["viewId"] = plan, ["source"] = section, ["at"] = J(20, 0), ["scale"] = 10, ["crop"] = true }));
        Assert.Equal(10.0, f.Model.Get(inset)!.Num("scale"), 9);
        Assert.NotNull(f.Model.Get(inset)!.Attrs["crop"]);
        f.Session.Undo();
        Assert.Null(f.Model.Get(inset));
    }

    [Fact]
    public void Printed_the_inset_leaves_out_what_its_source_shows_on_the_screen_only_and_comes_out_in_DXF()
    {
        var f = F();
        f.Wall(0, 0, 4, 0, height: 3);
        // A reference plane standing along the wall, 1 m in front of it: it cuts the plan, not a section parallel to it.
        var refPlane = Id(f.Run("view.create", new JsonObject { ["kind"] = RefPlanes.Kind, ["point"] = new JsonArray(0, -1, 0), ["normal"] = new JsonArray(0, -1, 0) }));
        var host = Id(f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(-1, -3), ["end"] = J(5, -3), ["scale"] = 50 }));
        var plan = "plan:" + f.Storey;
        f.Run("view.inset", new JsonObject { ["viewId"] = host, ["source"] = plan, ["at"] = J(12, 2) });
        var q = new Q(f);
        var screen = Views.Render(q, Views.Find(f.Model, host)!);
        Assert.Contains(screen.Marks, m => m.Owner == refPlane && m.Data?["via"] != null);
        var print = Views.Render(q, Views.Find(f.Model, host)!, print: true);
        Assert.DoesNotContain(print.Marks, m => m.Owner == refPlane);
        Assert.Contains(print.Pens, p => p.Layer == Insets.Layer);
        Assert.Contains("INSET", Encoding.UTF8.GetString(Views.Dxf(q, Views.Find(f.Model, host)!)));
    }
}
