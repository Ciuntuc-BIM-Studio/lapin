using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Angular, radial, diameter, arc-length, slope, level and ordinate dimensions: what they measure, what they write, what they hold on and drive.</summary>
public class DimensionKindTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static string Dim(Fixture f, string view, string kind, JsonObject args)
    {
        args["viewId"] = view;
        args["dimKind"] = kind;
        return (string)f.Run("drafting.addDimension", args)!["id"]!;
    }
    static double Value(Fixture f, string id) => (double)((JsonObject)f.Session.Query("drafting.dimension", new JsonObject { ["id"] = id })!)["value"]!;
    static List<string> Texts(Fixture f, string view) =>
        Views.Render(new Q(f), Views.Find(f.Model, view)!).Pens.SelectMany(p => p.Texts).Select(t => t.Text).ToList();

    sealed class Q(Fixture f) : IQueryContext
    {
        public Lapin.Model.IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    [Fact]
    public void An_angle_measures_its_sector_the_small_or_the_reflex_one()
    {
        var f = F();
        var plan = Plan(f);
        var a = Dim(f, plan, "angular", new JsonObject { ["c"] = J(0, 0), ["a"] = J(2, 0), ["b"] = J(0, 2), ["at"] = J(1, 1) });
        Assert.Equal(90, Value(f, a), 9);
        var r = Dim(f, plan, "angular", new JsonObject { ["c"] = J(0, 0), ["a"] = J(2, 0), ["b"] = J(0, 2), ["at"] = J(-1, -1) });
        Assert.Equal(270, Value(f, r), 9);
        Assert.Contains("90°", Texts(f, plan));
        Assert.Contains("270°", Texts(f, plan));
        Assert.Throws<CommandException>(() => Dim(f, plan, "angular", new JsonObject { ["c"] = J(0, 0), ["a"] = J(1, 0) }));
    }

    [Fact]
    public void A_radius_on_a_drafted_circle_follows_it_and_drives_it_a_diameter_and_an_arc_length_too()
    {
        var f = F();
        var plan = Plan(f);
        var circle = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = new JsonObject { ["type"] = "circle", ["c"] = J(5, 5), ["r"] = 1.25 } })!["ids"]![0]!;
        var r = Dim(f, plan, "radial", new JsonObject { ["shape"] = circle, ["at"] = J(8, 6) });
        var d = Dim(f, plan, "diameter", new JsonObject { ["shape"] = circle, ["at"] = J(2, 4) });
        Assert.Equal(1.25, Value(f, r), 9);
        Assert.Equal(2.5, Value(f, d), 9);
        var texts = Texts(f, plan);
        // In the view's dimension style (here metres, two decimals).
        Assert.Contains("R1.25", texts);
        Assert.Contains("Ø2.50", texts);
        // Typed: the circle takes it.
        f.Run("drafting.setDimensionValue", new JsonObject { ["id"] = d, ["value"] = 4 });
        Assert.Equal(2, ((Circle)Drafts.ShapeOf(f.Model.Get(circle)!)!).R, 9);
        Assert.Equal(2, Value(f, r), 9);
        // An arc of radius 2 over 90°: π m.
        var arc = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = new JsonObject { ["type"] = "arc", ["c"] = J(0, 0), ["r"] = 2, ["start"] = 0, ["end"] = Math.PI / 2 } })!["ids"]![0]!;
        var len = Dim(f, plan, "arc", new JsonObject { ["shape"] = arc, ["at"] = J(2, 2) });
        Assert.Equal(Math.PI, Value(f, len), 9);
        Assert.Throws<CommandException>(() => Dim(f, plan, "arc", new JsonObject { ["shape"] = circle }));
        // The circle gone: the radius is an orphan, and cannot drive.
        f.Run("element.delete", new JsonObject { ["id"] = circle });
        Assert.True((bool)((JsonObject)f.Session.Query("drafting.dimension", new JsonObject { ["id"] = r })!)["orphan"]!);
        Assert.Throws<CommandException>(() => f.Run("drafting.setDimensionValue", new JsonObject { ["id"] = r, ["value"] = 1 }));
    }

    [Fact]
    public void A_slope_a_level_and_an_ordinate()
    {
        var f = F();
        var plan = Plan(f);
        // 0.5 down over 4 m: 12.5 %; as degrees, and as a ratio.
        var s = Dim(f, plan, "slope", new JsonObject { ["a"] = J(0, 0), ["b"] = J(4, -0.5), ["at"] = J(2, 0) });
        Assert.Equal(0.125, Value(f, s), 9);
        Assert.Contains("12.5 %", Texts(f, plan));
        Assert.Equal("7.1°", DimensionKinds.FormatSlope(0.125, "deg"));
        Assert.Equal("1:8", DimensionKinds.FormatSlope(0.125, "ratio"));
        Assert.Equal("30.0°", DimensionKinds.FormatSlope(Math.Tan(Math.PI / 6), "auto"));
        // A level in a plan: its storey's; given, its own.
        var upper = (string)f.Run("storey.create", new JsonObject { ["elevation"] = 3.06 })!["id"]!;
        var upperPlan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = upper })!["id"]!;
        Dim(f, upperPlan, "level", new JsonObject { ["a"] = J(1, 1), ["at"] = J(3, 1) });
        Assert.Contains("+3.06", Texts(f, upperPlan));
        var low = Dim(f, plan, "level", new JsonObject { ["a"] = J(1, 1), ["at"] = J(3, 1), ["z"] = -0.45 });
        Assert.Equal(-0.45, Value(f, low), 9);
        Assert.Contains("−0.45", Texts(f, plan));
        // An ordinate along y from the origin.
        var o = Dim(f, plan, "ordinate", new JsonObject { ["a"] = J(2.5, 1.2), ["at"] = J(3, 2), ["axis"] = "y" });
        Assert.Equal(1.2, Value(f, o), 9);
        Assert.Throws<CommandException>(() => Dim(f, plan, "spiral", new JsonObject { ["a"] = J(0, 0) }));
    }
}
