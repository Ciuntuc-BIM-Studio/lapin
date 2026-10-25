using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>Profiles that change: a sweep that ends with another profile, a sweep between two rails, a loft through profiles on different views; drafted shapes' names and roles.</summary>
public class StudioLoftTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static string Plan(Fixture f, string? storey = null) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey ?? f.Storey })!["id"]!;
    static string Draw(Fixture f, string view, JsonObject shape, string? role = null)
    {
        var a = new JsonObject { ["viewId"] = view, ["shape"] = shape };
        if (role != null) a["role"] = role;
        return (string)f.Run("drafting.draw", a)!["ids"]![0]!;
    }
    static JsonObject Rect(double x0, double y0, double x1, double y1) =>
        new() { ["type"] = "polyline", ["pts"] = new JsonArray(J(x0, y0), J(x1, y0), J(x1, y1), J(x0, y1)), ["closed"] = true };
    static JsonObject Line(double x1, double y1, double x2, double y2) => new() { ["type"] = "line", ["a"] = J(x1, y1), ["b"] = J(x2, y2) };
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void A_sweep_that_ends_with_a_bigger_profile_grows_along_the_path()
    {
        var f = F();
        var plan = Plan(f);
        // A 1 × 1 square becoming a 2 × 2 one over 3 m, bottoms on the path: ∫(1 + t/3)² dt = 7.
        var start = Draw(f, plan, Rect(20, 0, 21, 1));
        var end = Draw(f, plan, Rect(30, 0, 32, 2));
        var path = Draw(f, plan, Line(0, 0, 3, 0));
        var id = (string)f.Run("studio.sweep", new JsonObject { ["profileId"] = start, ["pathId"] = path, ["endProfileId"] = end })!["id"]!;
        Assert.Equal(7, Vol(f, id), 6);
        // The end profile edited, the solid follows: 3 × 3 at the end → ∫(1 + 2t/3)² dt = 13.
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = end, ["shape"] = Rect(30, 0, 33, 3) });
        Assert.Equal(13, Vol(f, id), 6);
        // A square into a circle: it builds, its volume between the two prisms.
        var circle = Draw(f, plan, new JsonObject { ["type"] = "circle", ["c"] = J(40, 0.5), ["r"] = 0.5 });
        var sq = Draw(f, plan, Rect(50, 0, 51, 1));
        var morph = (string)f.Run("studio.sweep", new JsonObject { ["profileId"] = sq, ["pathId"] = Draw(f, plan, Line(0, 5, 3, 5)), ["endProfileId"] = circle })!["id"]!;
        Assert.InRange(Vol(f, morph), 3 * Math.PI * 0.25, 3);
        Assert.Throws<CommandException>(() => f.Run("studio.sweep", new JsonObject { ["profileId"] = Draw(f, plan, Rect(60, 0, 61, 1)), ["pathId"] = path, ["endProfileId"] = end }));
    }

    [Fact]
    public void A_sweep_between_two_rails_stretches_across_them()
    {
        var f = F();
        var plan = Plan(f);
        // 0.5 m high, across rails 2 m apart at the start and 4 m at the end, over 4 m: ∫ 0.5 (2 + x/2) dx = 6.
        var profile = Draw(f, plan, Rect(20, 0, 21, 0.5));
        var r1 = Draw(f, plan, Line(0, 0, 4, 0));
        var r2 = Draw(f, plan, Line(0, 2, 4, 4));
        var id = (string)f.Run("studio.sweep", new JsonObject { ["profileId"] = profile, ["pathId"] = r1, ["railId"] = r2 })!["id"]!;
        Assert.Equal(6, Vol(f, id), 6);
        var b = f.Bounds(id);
        Assert.Equal(4, b.Max.Y, 6);
        Assert.Equal(0.5, b.Max.Z, 9);
    }

    [Fact]
    public void A_loft_through_profiles_on_two_levels()
    {
        var f = F();
        var upper = (string)f.Run("storey.create", [])!["id"]!;            // +3.00
        var (p0, p1) = (Plan(f), Plan(f, upper));
        // A 2 × 2 square below, a 1 × 1 square 3 m above it, centred: a frustum 3/3 (4 + 1 + 2) = 7.
        var a = Draw(f, p0, Rect(0, 0, 2, 2));
        var b = Draw(f, p1, Rect(0.5, 0.5, 1.5, 1.5));
        var id = (string)f.Run("studio.loft", new JsonObject { ["profileId"] = a, ["profileIds"] = new JsonArray(a, b) })!["id"]!;
        Assert.Equal(7, Vol(f, id), 6);
        Assert.Equal(3, f.Bounds(id).Max.Z, 9);
        // Two profiles on one level cannot loft.
        var c = Draw(f, p0, Rect(5, 5, 6, 6));
        var bad = (string)f.Run("studio.loft", new JsonObject { ["profileId"] = c, ["profileIds"] = new JsonArray(c, Draw(f, p0, Rect(5.2, 5.2, 5.8, 5.8))) })!["id"]!;
        Assert.Empty(f.Session.Geometry(bad)!.Parts);
        Assert.Contains(f.Session.Geometry(bad)!.Diagnostics!, d => d.Contains("same place"));
    }

    [Fact]
    public void Drafted_shapes_get_unique_names_by_kind_or_role_and_construction_lines_stay_off_sheets()
    {
        var f = F();
        var plan = Plan(f);
        var l1 = Draw(f, plan, Line(0, 0, 1, 0));
        var l2 = Draw(f, plan, Line(0, 1, 1, 1));
        var pr = Draw(f, plan, Rect(0, 2, 1, 3), "profile");
        Assert.Equal(["Line 1", "Line 2", "Profile 1"], new[] { l1, l2, pr }.Select(id => f.Model.Get(id)!.Name));
        // An edit keeps the name; a duplicate name on the view is refused.
        f.Run("drafting.updateDraft", new JsonObject { ["id"] = l1, ["shape"] = Line(0, 0, 2, 0), ["role"] = "path" });
        Assert.Equal("Line 1", f.Model.Get(l1)!.Name);
        Assert.Equal("path", f.Model.Get(l1)!.Str("role"));
        Assert.Throws<CommandException>(() => f.Run("element.rename", new JsonObject { ["id"] = l2, ["name"] = "line 1" }));
        Assert.Throws<CommandException>(() => f.Run("drafting.updateDraft", new JsonObject { ["id"] = l2, ["role"] = "decoration" }));
        // A construction line is on the screen's drawing, not on a sheet's.
        Draw(f, plan, Line(0, 5, 10, 5), "draft");
        var view = Views.Find(f.Model, plan)!;
        bool HasConstruction(Views.Rendered r) => r.Pens.Any(p => p.Layer == Views.ConstructionLayer);
        Assert.True(HasConstruction(Views.Render(new TestQuery(f), view)));
        Assert.False(HasConstruction(Views.Render(new TestQuery(f), view, print: true)));
    }

    sealed class TestQuery(Fixture f) : IQueryContext
    {
        public Lapin.Model.IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
