using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>Studio: ported from the old Lapin's studio-feature.test.ts and element-boolean tests (same cases and numbers), on the engine.</summary>
public class StudioTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static string Draw(Fixture f, string view, JsonObject shape) => (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = shape })!["ids"]![0]!;
    static JsonObject Rect(double x0, double y0, double x1, double y1) =>
        new() { ["type"] = "polyline", ["pts"] = new JsonArray(J(x0, y0), J(x1, y0), J(x1, y1), J(x0, y1)), ["closed"] = true };
    static JsonObject Line(double x1, double y1, double x2, double y2) => new() { ["type"] = "line", ["a"] = J(x1, y1), ["b"] = J(x2, y2) };
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void An_extrusion_takes_its_first_level_holes_and_follows_its_sketch()
    {
        var f = F();
        var plan = Plan(f);
        // A 100 × 100 plate with a 40 × 40 hole at (10, 10), an island 10 × 10 inside the hole.
        var plate = Draw(f, plan, Rect(0, 0, 100, 100));
        var hole = Draw(f, plan, Rect(10, 10, 50, 50));
        Draw(f, plan, Rect(20, 20, 30, 30));
        var solid = (string)f.Run("studio.extrude", new JsonObject { ["profileId"] = plate, ["depth"] = 0.2, ["class"] = "SLAB" })!["id"]!;
        Assert.Equal((10000 - 1600) * 0.2, Vol(f, solid), 6);
        var feature = (JsonObject)f.Session.Query("studio.feature", new JsonObject { ["id"] = solid })!;
        Assert.Equal("extrude", (string)feature["operation"]!);
        Assert.Equal([plate, hole], feature["sketch"]!.AsArray().Select(x => (string)x!));
        Assert.Equal(1, (int)feature["holes"]!);
        // A wall or a slab is written as a proxy that exports as itself.
        var e = f.Model.Get(solid)!;
        Assert.Equal(IfcClass.BuildingElementProxy, e.Class);
        Assert.Equal("IfcSlab", e.Str("exportAs"));
        // The sketch edited, the solid follows; the depth changed the same.
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = hole, ["shape"] = Rect(10, 10, 40, 40) });
        Assert.Equal((10000 - 900) * 0.2, Vol(f, solid), 6);
        f.Run("element.update", new JsonObject { ["id"] = solid, ["attrs"] = new JsonObject { ["depth"] = 0.3 } });
        Assert.Equal((10000 - 900) * 0.3, Vol(f, solid), 6);
        // The outline taken away: the solid says why it is not there.
        f.Run("element.delete", new JsonObject { ["id"] = plate });
        Assert.Empty(f.Session.Geometry(solid)!.Parts);
        Assert.Throws<CommandException>(() => f.Run("studio.extrude", new JsonObject { ["profileId"] = Draw(f, plan, Line(0, 0, 1, 1)) }));
    }

    [Fact]
    public void A_revolution_and_a_sweep_from_a_plan_sketch()
    {
        var f = F();
        var plan = Plan(f);
        // A 1 × 1 square 1 m from an axis, turned all the way round: a ring π (2² − 1²) · 1 = 3π.
        var sq = Draw(f, plan, Rect(1, 0, 2, 1));
        var axis = Draw(f, plan, Line(0, 0, 0, 1));
        var ring = (string)f.Run("studio.revolve", new JsonObject { ["profileId"] = sq, ["axisId"] = axis })!["id"]!;
        Assert.InRange(Vol(f, ring), 3 * Math.PI * 0.99, 3 * Math.PI);
        f.Run("element.update", new JsonObject { ["id"] = ring, ["attrs"] = new JsonObject { ["angle"] = 90 } });
        Assert.InRange(Vol(f, ring), 3 * Math.PI / 4 * 0.99, 3 * Math.PI / 4);
        // A 0.2 × 0.2 section along an L 5 + 5 m, mitred: 0.04 · 10.
        var profile = Draw(f, plan, Rect(20, 20, 20.2, 20.2));
        var path = Draw(f, plan, new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 10), J(5, 10), J(5, 15)), ["closed"] = false });
        var sweep = (string)f.Run("studio.sweep", new JsonObject { ["profileId"] = profile, ["pathId"] = path })!["id"]!;
        Assert.Equal(0.4, Vol(f, sweep), 6);
        var b = f.Bounds(sweep);
        Assert.Equal(0, b.Min.Z, 9);
        Assert.Equal(0.2, b.Max.Z, 9);
    }

    // ------------------------------------------------------------ booleans: two crossing walls

    static (Fixture F, string A, string B) Walls(double bThickness = 0.4)
    {
        var f = F();
        var ta = f.Type("A", ("Concrete", 0.2, true));
        var tb = f.Type("B", ("Concrete", bThickness, true));
        string W(double x1, double y1, double x2, double y2, string type, double h) => (string)f.Run("architecture.addWall", new JsonObject
        {
            ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["typeId"] = type, ["height"] = h, ["join"] = false,
        })!["id"]!;
        return (f, W(0, 0, 4, 0, ta, 3), W(2, -1, 2, 1, tb, 5));
    }

    [Fact]
    public void Subtract_cuts_the_target_and_both_stay_editable()
    {
        var (f, a, b) = Walls();
        f.Run("studio.boolean", new JsonObject { ["target"] = a, ["tools"] = new JsonArray(b), ["op"] = "subtract" });
        Assert.Equal(4 * 0.2 * 3 - 0.4 * 0.2 * 3, Vol(f, a), 6);
        var box = f.Bounds(a);
        Assert.Equal(0, box.Min.X, 6);
        Assert.Equal(4, box.Max.X, 6);
        Assert.NotEmpty(f.Session.Geometry(b)!.Parts);
        // Again: nothing new.
        Assert.Equal(0, (int)f.Run("studio.boolean", new JsonObject { ["target"] = a, ["tools"] = new JsonArray(b), ["op"] = "subtract" })!["added"]!);
        Assert.Equal(1, (int)f.Run("studio.removeBoolean", new JsonObject { ["ids"] = new JsonArray(a) })!["removed"]!);
        Assert.Equal(4 * 0.2 * 3, Vol(f, a), 6);
    }

    [Fact]
    public void A_union_follows_its_tool_and_separates_again()
    {
        var (f, a, b) = Walls();
        f.Run("studio.boolean", new JsonObject { ["target"] = a, ["tools"] = new JsonArray(b), ["op"] = "union" });
        var box = f.Bounds(a);
        Assert.Equal(5, box.Max.Z, 6);
        Assert.Equal(0, box.Min.X, 6);
        Assert.Equal(4, box.Max.X, 6);
        Assert.Empty(f.Session.Geometry(b)!.Parts); // drawn inside its target
        Assert.Throws<CommandException>(() => f.Run("studio.boolean", new JsonObject { ["target"] = a, ["tools"] = new JsonArray(b), ["op"] = "intersect" }));
        f.Run("studio.operands", new JsonObject { ["id"] = a });
        Assert.NotEmpty(f.Session.Geometry(b)!.Parts);
        f.Run("studio.operands", new JsonObject { ["id"] = a });
        Assert.Empty(f.Session.Geometry(b)!.Parts);
        // The tool raised to 6 m: the union follows.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = b, ["height"] = 6 });
        Assert.Equal(6, f.Bounds(a).Max.Z, 6);
        f.Run("studio.removeBoolean", new JsonObject { ["ids"] = new JsonArray(b) });
        Assert.Equal(3, f.Bounds(a).Max.Z, 6);
        Assert.NotEmpty(f.Session.Geometry(b)!.Parts);
        f.Session.Undo();
        Assert.Equal(6, f.Bounds(a).Max.Z, 6);
    }

    [Fact]
    public void An_intersection_keeps_what_both_share()
    {
        var (f, a, b) = Walls();
        f.Run("studio.boolean", new JsonObject { ["target"] = a, ["tools"] = new JsonArray(b), ["op"] = "intersect" });
        var box = f.Bounds(a);
        Assert.Equal(1.8, box.Min.X, 6);
        Assert.Equal(2.2, box.Max.X, 6);
        Assert.Equal(3, box.Max.Z, 6);
        var (f2, a2, b2) = Walls(1);
        f2.Run("studio.boolean", new JsonObject { ["target"] = a2, ["tools"] = new JsonArray(b2), ["op"] = "intersect" });
        Assert.Equal(1.5, f2.Bounds(a2).Min.X, 6);
        Assert.Equal(2.5, f2.Bounds(a2).Max.X, 6);
    }
}
