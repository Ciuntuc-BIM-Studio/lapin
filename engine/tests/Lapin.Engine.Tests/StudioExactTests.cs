using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// What each Studio operation is exactly (docs/plan-studio-freeform.md §9, F2b): derived from its recipe, held against its body, refused
/// when a step or a cut changed the body; the smooth loft's body cut from the B-spline surface it exports.
/// </summary>
public class StudioExactTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static JsonArray J3(double x, double y, double z) => [x, y, z];
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
    static string Draw(Fixture f, string view, JsonObject shape) => (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = shape })!["ids"]![0]!;
    static JsonObject Rect(double x0, double y0, double x1, double y1) =>
        new() { ["type"] = "polyline", ["pts"] = new JsonArray(J(x0, y0), J(x1, y0), J(x1, y1), J(x0, y1)), ["closed"] = true };
    static JsonObject Circle(double x, double y, double r) => new() { ["type"] = "circle", ["c"] = J(x, y), ["r"] = r };
    static JsonObject Line(double x1, double y1, double x2, double y2) => new() { ["type"] = "line", ["a"] = J(x1, y1), ["b"] = J(x2, y2) };
    static JsonObject Arc(double x, double y, double r, double start, double end) => new() { ["type"] = "arc", ["c"] = J(x, y), ["r"] = r, ["start"] = start, ["end"] = end };
    static string Id(JsonNode? r) => (string)r!["id"]!;

    static (ExactBody? Body, string? Problem) Exact(Fixture f, string id)
    {
        var e = f.Model.Get(id)!;
        var part = Assert.Single(f.Session.Geometry(id)!.Parts);
        return StudioModule.Exact(e, f.Model, f.Session.Catalog, part.Solid);
    }

    static Dictionary<string, int> Kinds(Fixture f, string id)
    {
        var (b, why) = Exact(f, id);
        Assert.True(b != null, why);
        return b!.SurfaceKinds();
    }

    static string Primitive(Fixture f, string shape, double x, JsonObject sizes)
    {
        var a = new JsonObject { ["shape"] = shape, ["at"] = J(x, 0) };
        foreach (var (k, v) in sizes) a[k] = v?.DeepClone();
        return Id(f.Run("studio.primitive", a));
    }

    [Fact]
    public void Primitives_are_planes_cylinders_cones_spheres_and_tori()
    {
        var f = F();
        Assert.Equal(new Dictionary<string, int> { ["plane"] = 6 }, Kinds(f, Primitive(f, "box", 0, new() { ["width"] = 1, ["length"] = 2, ["height"] = 0.5, ["rotation"] = 30 })));
        Assert.Equal(new Dictionary<string, int> { ["cylinder"] = 2, ["plane"] = 2 }, Kinds(f, Primitive(f, "cylinder", 3, new() { ["radius"] = 0.4, ["height"] = 1.5 })));
        Assert.Equal(new Dictionary<string, int> { ["cone"] = 2, ["plane"] = 4 }, Kinds(f, Primitive(f, "cone", 6, new() { ["radius"] = 0.6, ["radius2"] = 0.2, ["height"] = 1 })));
        Assert.Equal(new Dictionary<string, int> { ["cone"] = 2, ["plane"] = 2 }, Kinds(f, Primitive(f, "cone", 9, new() { ["radius"] = 0.6, ["height"] = 1 })));
        Assert.Equal(new Dictionary<string, int> { ["sphere"] = 2 }, Kinds(f, Primitive(f, "sphere", 12, new() { ["radius"] = 0.7 })));
        Assert.Equal(new Dictionary<string, int> { ["torus"] = 4 }, Kinds(f, Primitive(f, "torus", 15, new() { ["radius"] = 1, ["radius2"] = 0.25 })));
        Assert.Equal(new Dictionary<string, int> { ["plane"] = 5 }, Kinds(f, Primitive(f, "wedge", 18, new() { ["width"] = 1, ["length"] = 2, ["height"] = 0.5 })));
        // The sphere's surface: its centre a radius above where it stands, its radius exact.
        var sphere = Exact(f, f.Model.Entities.Last(e => e.Str("shape") == "sphere").Id).Body!;
        var s = Assert.IsType<SphereSurface>(sphere.Faces[0].Surface);
        Assert.Equal(0.7, s.Radius, 12);
        Assert.Equal(0.7 + f.Model.Get(f.Storey)!.Num("elevation"), s.Frame.Origin.Z, 9);
    }

    [Fact]
    public void An_extruded_circle_with_a_circular_hole_is_planes_and_cylinders()
    {
        var f = F();
        var plan = Plan(f);
        var outer = Draw(f, plan, Circle(0, 0, 1));
        Draw(f, plan, Circle(0.2, 0, 0.3));
        var tube = Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = outer, ["depth"] = 2 }));
        var (b, why) = Exact(f, tube);
        Assert.True(b != null, why);
        Assert.Equal(new Dictionary<string, int> { ["cylinder"] = 4, ["plane"] = 2 }, b!.SurfaceKinds());
        // The caps hold the hole: an outer loop and an inner one each.
        Assert.All(b.Faces.Where(x => x.Surface is PlaneSurface), x => Assert.Equal(2, x.Loops.Count));
        // The hole's cylinder faces into the hole: its own normal points out of its axis, so against the body.
        var hole = b.Faces.Where(x => x.Surface is CylinderSurface { Radius: < 0.5 }).ToList();
        Assert.Equal(2, hole.Count);
        Assert.All(hole, x => Assert.False(x.SameSense));
        Assert.All(b.Faces.Where(x => x.Surface is CylinderSurface { Radius: > 0.5 }), x => Assert.True(x.SameSense));
    }

    [Fact]
    public void A_profile_of_a_line_and_an_arc_drawn_apart_extrudes_into_a_plane_and_a_cylinder()
    {
        var f = F();
        var plan = Plan(f);
        // A half disc: its diameter a line, its round side an arc (two shapes, chained).
        var d = Draw(f, plan, Line(-1, 0, 1, 0));
        Draw(f, plan, Arc(0, 0, 1, 0, Math.PI));
        var id = Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = d, ["depth"] = 0.5 }));
        Assert.Equal(new Dictionary<string, int> { ["cylinder"] = 1, ["plane"] = 3 }, Kinds(f, id));
    }

    [Fact]
    public void A_revolved_square_is_cylinders_and_planes_and_a_part_turn_has_its_caps()
    {
        var f = F();
        var plan = Plan(f);
        var sq = Draw(f, plan, Rect(1, 0, 2, 1));
        var axis = Draw(f, plan, Line(0, 0, 0, 1));
        var ring = Id(f.Run("studio.revolve", new JsonObject { ["profileId"] = sq, ["axisId"] = axis }));
        Assert.Equal(new Dictionary<string, int> { ["cylinder"] = 4, ["plane"] = 4 }, Kinds(f, ring));
        f.Run("element.update", new JsonObject { ["id"] = ring, ["attrs"] = new JsonObject { ["angle"] = 90 } });
        Assert.Equal(new Dictionary<string, int> { ["cylinder"] = 2, ["plane"] = 4 }, Kinds(f, ring));
        // A semicircle on its axis turned: a sphere; a circle off it: a torus.
        var semi = Draw(f, plan, Arc(10, 0, 1, -Math.PI / 2, Math.PI / 2));
        var diam = Draw(f, plan, Line(10, 1, 10, -1));
        var ball = Id(f.Run("studio.revolve", new JsonObject { ["profileId"] = semi, ["axisId"] = diam }));
        Assert.Equal(new Dictionary<string, int> { ["sphere"] = 2 }, Kinds(f, ball));
        var tube = Draw(f, plan, Circle(22, 0, 0.5));
        var tAxis = Draw(f, plan, Line(20, -1, 20, 1));
        var donut = Id(f.Run("studio.revolve", new JsonObject { ["profileId"] = tube, ["axisId"] = tAxis }));
        Assert.Equal(new Dictionary<string, int> { ["torus"] = 4 }, Kinds(f, donut));
    }

    [Fact]
    public void A_smooth_loft_is_a_skinned_b_spline_surface_its_body_is_cut_from()
    {
        var f = F();
        while (Spatial.Storeys(f.Model).Count() < 3) f.Run("storey.create", new JsonObject());
        var storeys = Spatial.Storeys(f.Model).OrderBy(s => s.Num("elevation")).ToList();
        string PlanOf(string storey) => Id(f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey }));
        var (p0, p1, p2) = (PlanOf(storeys[0].Id), PlanOf(storeys[1].Id), PlanOf(storeys[2].Id));
        // A square, a circle twice as wide, a square again.
        var a = Draw(f, p0, Rect(-0.5, -0.5, 0.5, 0.5));
        var b = Draw(f, p1, Circle(0, 0, 1));
        var c = Draw(f, p2, Rect(-0.5, -0.5, 0.5, 0.5));
        var loft = Id(f.Run("studio.loft", new JsonObject { ["profileId"] = a, ["profileIds"] = new JsonArray(a, b, c), ["smooth"] = true }));
        var (body, why) = Exact(f, loft);
        Assert.True(body != null, why);
        Assert.Equal(new Dictionary<string, int> { ["bspline"] = 2, ["plane"] = 2 }, body!.SurfaceKinds());
        // It passes through the circle exactly: on the middle level, the surface is the circle of radius 1.
        var z1 = storeys[1].Num("elevation");
        var side = body.Faces.Select(x => x.Surface).OfType<SplineSurface>().First().Surface;
        var (v0, v1) = side.DomainV;
        var hits = Enumerable.Range(0, 2001).Select(k => v0 + (v1 - v0) * k / 2000.0).Select(v => side.At((side.DomainU.Start + side.DomainU.End) / 2, v)).MinBy(p => Math.Abs(p.Z - z1));
        Assert.Equal(1, new Vec2(hits.X, hits.Y).Length, 2);
        // The body the model uses lies on that surface: every vertex of it.
        var solid = f.Session.Geometry(loft)!.Parts[0].Solid;
        Assert.All(solid.Vertices, p => Assert.True(body.Faces.Min(x => x.Surface.Distance(p)) < 1e-6, $"{p} is off the exact surface"));
        // And bulges past the straight one between the profiles, as before.
        var (a2, b2, c2) = (Draw(f, p0, Rect(9.5, -0.5, 10.5, 0.5)), Draw(f, p1, Circle(10, 0, 1)), Draw(f, p2, Rect(9.5, -0.5, 10.5, 0.5)));
        var straight = Id(f.Run("studio.loft", new JsonObject { ["profileId"] = a2, ["profileIds"] = new JsonArray(a2, b2, c2) }));
        Assert.True(Measure.Volume(solid) > Measure.Volume(f.Session.Geometry(straight)!.Parts[0].Solid) + 1e-3);
        Assert.NotNull(Exact(f, straight).Problem);
    }

    [Fact]
    public void A_closed_subd_is_patches_and_facets()
    {
        var f = F();
        var id = Id(f.Run("studio.subdCreate", new JsonObject { ["shape"] = "box", ["at"] = J3(0, 0, 0), ["width"] = 1, ["length"] = 1, ["height"] = 1 }));
        var (b, why) = Exact(f, id);
        Assert.True(b != null, why);
        Assert.Equal(72, b!.SurfaceKinds()["bspline"]);
        Assert.True(b.SurfaceKinds()["plane"] > 0);
        // An open one has none.
        var sheet = Id(f.Run("studio.subdCreate", new JsonObject { ["shape"] = "plane", ["at"] = J3(0, 10, 3), ["sizes"] = new JsonArray(4, 2), ["segments"] = new JsonArray(4, 2), ["thickness"] = 0.2 }));
        Assert.Null(Exact(f, sheet).Body);
    }

    [Fact]
    public void A_step_or_a_cut_by_another_element_leaves_the_solid_its_facets()
    {
        var f = F();
        var cyl = Primitive(f, "cylinder", 0, new() { ["radius"] = 0.5, ["height"] = 1 });
        Assert.NotNull(Exact(f, cyl).Body);
        // Its top pushed down: a step.
        f.Run("studio.pushPull", new JsonObject { ["id"] = cyl, ["point"] = J3(0, 0, 1), ["normal"] = J3(0, 0, 1), ["distance"] = -0.2 });
        Assert.Contains("steps", Exact(f, cyl).Problem);
        // A cylinder a wall runs through: cut by the wall's priority, it is not the cylinder any more.
        var post = Primitive(f, "cylinder", 5, new() { ["radius"] = 0.5, ["height"] = 1 });
        Assert.NotNull(Exact(f, post).Body);
        f.Wall(4, 0, 6, 0);
        var (b, why) = Exact(f, post);
        Assert.Null(b);
        Assert.NotNull(why);
    }
}
