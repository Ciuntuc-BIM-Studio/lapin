using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;
using Cad = Lapin.Drafting.Cad;

namespace Lapin.Engine.Tests;

/// <summary>
/// Decision 520, §4.2 and §4.4 of docs/plan-studio-parametric.md: shapes drawn with the constraints recorded while drawing are one
/// undo step (<c>studio.drawConstrained</c>), the constraints the solver refuses told in its result; a dimension's placement
/// (<c>place</c>) is stored, changed in one undo step and carried by a move, turn or mirror of its shapes.
/// </summary>
public sealed class SketchDrawPlaceTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());

    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;

    static JsonObject LineShape(double x1, double y1, double x2, double y2) => new() { ["type"] = "line", ["a"] = J(x1, y1), ["b"] = J(x2, y2) };

    static string Draw(Fixture f, string view, JsonObject shape) =>
        (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = shape })!["ids"]![0]!;

    static JsonObject New(int i, string part = "whole") => new() { ["new"] = i, ["part"] = part };
    static JsonObject R(string shape, string part = "whole") => new() { ["shape"] = shape, ["part"] = part };

    static JsonObject C(string kind, params JsonObject[] refs) => new() { ["kind"] = kind, ["refs"] = new JsonArray(refs.Select(r => (JsonNode)r).ToArray()) };
    static JsonObject C(string kind, double value, params JsonObject[] refs) { var c = C(kind, refs); c["value"] = value; return c; }

    static JsonObject DrawConstrained(Fixture f, string view, JsonObject[] shapes, params JsonObject[] constraints) =>
        (JsonObject)f.Run("studio.drawConstrained", new JsonObject
        {
            ["viewId"] = view,
            ["shapes"] = new JsonArray(shapes.Select(s => (JsonNode)s).ToArray()),
            ["constraints"] = new JsonArray(constraints.Select(c => (JsonNode)c).ToArray()),
        })!;

    static List<JsonObject> All(Fixture f, string view) =>
        f.Session.Query("studio.constraints", new JsonObject { ["viewId"] = view })!["constraints"]!.AsArray().Select(c => (JsonObject)c!).ToList();

    static JsonObject One(Fixture f, string view, string id) => All(f, view).Single(c => (string)c["id"]! == id);

    static string Add(Fixture f, JsonObject c) => (string)f.Run("studio.addConstraint", c)!["id"]!;

    static Cad.Line L(Fixture f, string id) => (Cad.Line)Drafts.ShapeOf(f.Model.Get(id)!)!;

    [Fact]
    public void Shapes_drawn_with_their_constraints_are_one_undo_step()
    {
        var f = F();
        var plan = Plan(f);
        var d1 = Draw(f, plan, LineShape(0, 0, 4, 0));
        // A line begun on d1's end, drawn a little off the horizontal: its constraints join and level it, solved in the same command.
        var r = DrawConstrained(f, plan, [LineShape(4, 0, 9, 0.2)],
            C("coincident", New(0, "start"), R(d1, "end")), C("horizontal", New(0)), C("distance", 5, New(0)));
        var id = (string)r["ids"]![0]!;
        Assert.Equal(3, r["constraints"]!.AsArray().Count);
        Assert.Null(r["refused"]);
        var l = L(f, id);
        Assert.True((l.A - L(f, d1).B).Length < 1e-7);
        Assert.True(Math.Abs(l.B.Y - l.A.Y) < 1e-7);
        Assert.Equal(5, (l.B - l.A).Length, 6);
        Assert.Equal(3, All(f, plan).Count);
        // One undo takes the line and its constraints; d1 stays.
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
        Assert.NotNull(f.Model.Get(d1));
        Assert.Empty(All(f, plan));
        Assert.Empty(f.Model.OfClass(SketchConstraints.Class));
        f.Session.Redo();
        Assert.NotNull(f.Model.Get(id));
        Assert.Equal(3, All(f, plan).Count);
    }

    [Fact]
    public void The_constraints_the_solver_refuses_are_reported_and_the_rest_kept_in_the_same_step()
    {
        var f = F();
        var plan = Plan(f);
        var r = DrawConstrained(f, plan, [LineShape(0, 0, 3, 0.1)],
            C("horizontal", New(0)), C("distance", 3, New(0)), C("angle", 45, New(0)), C("vertical", New(3)));
        var id = (string)r["ids"]![0]!;
        Assert.Equal(2, r["constraints"]!.AsArray().Count);
        var refused = r["refused"]!.AsArray().Select(x => (JsonObject)x!).ToList();
        Assert.Equal([2, 3], refused.Select(x => (int)x["index"]!).ToArray());
        Assert.Equal(["angle", "vertical"], refused.Select(x => (string)x["kind"]!).ToArray());
        Assert.All(refused, x => Assert.False(string.IsNullOrEmpty((string?)x["reason"])));
        Assert.Equal(["horizontal", "distance"], All(f, plan).Select(c => (string)c["kind"]!).ToArray());
        Assert.True(Math.Abs(L(f, id).B.Y - L(f, id).A.Y) < 1e-7);
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
        Assert.Empty(All(f, plan));
    }

    [Fact]
    public void Drawing_with_no_constraints_or_a_bad_shape_behaves_as_draw()
    {
        var f = F();
        var plan = Plan(f);
        var r = DrawConstrained(f, plan, [LineShape(0, 0, 1, 0)]);
        Assert.Single(r["ids"]!.AsArray());
        Assert.Empty(f.Model.OfClass(SketchConstraints.Class));
        Assert.Throws<CommandException>(() => f.Run("studio.drawConstrained", new JsonObject { ["viewId"] = plan, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "nothing" }) }));
    }

    [Fact]
    public void A_dimension_s_place_is_stored_changed_in_one_step_and_cleared()
    {
        var f = F();
        var plan = Plan(f);
        var l = Draw(f, plan, LineShape(0, 0, 4, 0));
        var d = Add(f, C("distance", 4, R(l)));
        Assert.Null(One(f, plan, d)["place"]);
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = d, ["place"] = new JsonObject { ["offset"] = 0.5 } });
        Assert.Equal(0.5, (double)One(f, plan, d)["place"]!["offset"]!, 9);
        Assert.Equal(4, (double)One(f, plan, d)["value"]!, 9);
        f.Session.Undo();
        Assert.Null(One(f, plan, d)["place"]);
        f.Session.Redo();
        Assert.Equal(0.5, (double)One(f, plan, d)["place"]!["offset"]!, 9);
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = d, ["place"] = null });
        Assert.Null(One(f, plan, d)["place"]);
        // Only a dimension takes a place; an angle's is the radius of its arc, more than nothing.
        var h = Add(f, C("horizontal", R(l)));
        Assert.Throws<CommandException>(() => f.Run("studio.updateConstraint", new JsonObject { ["id"] = h, ["place"] = new JsonObject { ["offset"] = 1 } }));
        f.Run("studio.removeConstraint", new JsonObject { ["id"] = h });
        var a = Add(f, C("angle", 0, R(l)));
        Assert.Throws<CommandException>(() => f.Run("studio.updateConstraint", new JsonObject { ["id"] = a, ["place"] = new JsonObject { ["offset"] = -1 } }));
        Assert.Throws<CommandException>(() => f.Run("studio.updateConstraint", new JsonObject { ["id"] = d, ["place"] = new JsonObject { ["offset"] = "far" } }));
    }

    [Fact]
    public void A_place_is_carried_by_a_mirror_and_a_turn_of_its_shapes()
    {
        var f = F();
        var plan = Plan(f);
        // Off the axes, so the mirror and the turn change every shape.
        var l = Draw(f, plan, LineShape(0, 1, 4, 1));
        var c = Draw(f, plan, new JsonObject { ["type"] = "circle", ["c"] = J(10, 1), ["r"] = 1 });
        var d = Add(f, C("distance", 4, R(l)));
        var dx = Add(f, C("distanceX", 4, R(l)));
        var rad = Add(f, C("radius", 1, R(c)));
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = d, ["place"] = new JsonObject { ["offset"] = 0.5 } });
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = dx, ["place"] = new JsonObject { ["offset"] = 0.8 } });
        f.Run("studio.updateConstraint", new JsonObject { ["id"] = rad, ["place"] = new JsonObject { ["offset"] = 0.3, ["angle"] = 30 } });
        // Mirrored about the x axis: the dimension lines go to the other side, the radius' leader turns to -30°.
        f.Run("drafting.transform", new JsonObject { ["op"] = "mirror", ["ids"] = Ids(l, c), ["a"] = J(0, 0), ["b"] = J(1, 0) });
        Assert.Equal(-0.5, (double)One(f, plan, d)["place"]!["offset"]!, 9);
        Assert.Equal(-0.8, (double)One(f, plan, dx)["place"]!["offset"]!, 9);
        Assert.Equal(0.3, (double)One(f, plan, rad)["place"]!["offset"]!, 9);
        Assert.Equal(-30, (double)One(f, plan, rad)["place"]!["angle"]!, 9);
        f.Session.Undo();
        Assert.Equal(0.5, (double)One(f, plan, d)["place"]!["offset"]!, 9);
        // Turned a quarter about the origin: distanceX becomes distanceY, its line still 0.8 off the measured edge, on the turned side.
        f.Run("drafting.transform", new JsonObject { ["op"] = "rotate", ["ids"] = Ids(l, c), ["center"] = J(0, 0), ["angle"] = 90 });
        var turned = One(f, plan, dx);
        Assert.Equal("distanceY", (string)turned["kind"]!);
        Assert.Equal(0.8, (double)turned["place"]!["offset"]!, 9);
        Assert.Equal(0.5, (double)One(f, plan, d)["place"]!["offset"]!, 9);
        Assert.Equal(120, (double)One(f, plan, rad)["place"]!["angle"]!, 9);
        // Scaled twice: offsets double.
        f.Run("drafting.transform", new JsonObject { ["op"] = "scale", ["ids"] = Ids(l, c), ["center"] = J(0, 0), ["factor"] = 2 });
        Assert.Equal(1.0, (double)One(f, plan, d)["place"]!["offset"]!, 9);
        Assert.Equal(0.6, (double)One(f, plan, rad)["place"]!["offset"]!, 9);
    }
}
