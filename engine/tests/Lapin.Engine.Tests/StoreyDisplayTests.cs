using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A new project's storeys, and what a plan shows of the other storeys: its
/// own storey's elements by default; those of the storeys it lists, or of
/// every storey, when its view says so, halftone if asked; drawn by the
/// engine, so a sheet prints what the screen shows.
/// </summary>
public sealed class StoreyDisplayTests
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    [Fact]
    public void A_new_project_has_foundations_a_ground_floor_and_a_first_floor()
    {
        var f = new Fixture();
        f.Session.New();
        var storeys = Spatial.Storeys(f.Model).Select(s => (s.Name, s.Num("elevation"), s.Num("height"))).ToList();
        Assert.Equal([("Foundations", -1.5, 1.5), ("Ground floor", 0.0, 3.0), ("First floor", 3.0, 3.0)], storeys);
        Assert.False(f.Session.History.CanUndo);
        Assert.False(f.Session.History.Dirty);
        // A project another file fills starts with a ground floor alone.
        f.Session.New(plain: true);
        Assert.Equal(["Ground floor"], Spatial.Storeys(f.Model).Select(s => s.Name));
    }

    /// <summary>A ground floor with a wall, a foundation storey under it whose wall rises through the ground floor's cut plane, and a plan of the ground floor.</summary>
    static (Fixture F, string Ground, string Below, string GroundWall, string BelowWall, string Plan) Project()
    {
        var f = new Fixture();
        var ground = f.Storey;
        var below = (string)f.Run("storey.create", new JsonObject { ["name"] = "Foundations", ["elevation"] = -1.5, ["height"] = 1.5 })!["id"]!;
        // (Fixture.Storey is the lowest storey: the foundations now.)
        var groundWall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = ground, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0 })!["id"]!;
        // From −1.50 up to +1.50: the ground floor's cut plane (+1.20) goes through it.
        var belowWall = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = below, ["x1"] = 0, ["y1"] = 3, ["x2"] = 5, ["y2"] = 3, ["height"] = 3.0 })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = ground })!["id"]!;
        return (f, ground, below, groundWall, belowWall, plan);
    }

    static HashSet<string> Drawn(Fixture f, string view)
    {
        var (d, _, problem) = Views.Generate(new Q(f), Views.Find(f.Model, view)!);
        Assert.True(d != null, problem);
        return d!.Cuts.Select(c => c.Element).Concat(d.Lines.Select(l => l.Element)).ToHashSet();
    }

    [Fact]
    public void A_plan_shows_its_own_storeys_elements_by_default()
    {
        var (f, ground, _, groundWall, belowWall, plan) = Project();
        var drawn = Drawn(f, plan);
        Assert.Contains(groundWall, drawn);
        Assert.DoesNotContain(belowWall, drawn);
        // The storey's implicit plan too.
        Assert.DoesNotContain(belowWall, Drawn(f, "plan:" + ground));
        // A section shows every storey.
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(2.5, -1), ["end"] = new JsonArray(2.5, 4) })!["id"]!;
        var cut = Drawn(f, section);
        Assert.Contains(groundWall, cut);
        Assert.Contains(belowWall, cut);
    }

    [Fact]
    public void A_plan_shows_the_storeys_its_view_lists_or_all()
    {
        var (f, ground, below, groundWall, belowWall, plan) = Project();
        f.Run("view.setStoreys", new JsonObject { ["id"] = plan, ["mode"] = "some", ["ids"] = new JsonArray(below) });
        Assert.Contains(belowWall, Drawn(f, plan));
        // The plan's own storey is never listed: it is always shown.
        f.Run("view.setStoreys", new JsonObject { ["id"] = plan, ["ids"] = new JsonArray(below, ground) });
        Assert.Equal([below], ((JsonArray)f.Model.Get(plan)!.Attrs["storeys"]!["ids"]!).Select(x => (string)x!));
        f.Run("view.setStoreys", new JsonObject { ["id"] = plan, ["mode"] = "all", ["ids"] = new JsonArray() });
        Assert.Contains(belowWall, Drawn(f, plan));
        Assert.Contains(groundWall, Drawn(f, plan));
        // Back to its own storey: the setting goes from the view.
        f.Run("view.setStoreys", new JsonObject { ["id"] = plan, ["mode"] = "home", ["halftone"] = false });
        Assert.Null(f.Model.Get(plan)!.Attrs["storeys"]);
        Assert.DoesNotContain(belowWall, Drawn(f, plan));
        // Undo brings the setting back.
        f.Session.Undo();
        Assert.Contains(belowWall, Drawn(f, plan));
    }

    [Fact]
    public void Another_storeys_elements_print_halftone_when_asked()
    {
        var (f, _, below, groundWall, belowWall, plan) = Project();
        f.Run("view.setStoreys", new JsonObject { ["id"] = plan, ["mode"] = "some", ["ids"] = new JsonArray(below), ["halftone"] = true });
        var rendered = Views.Render(new Q(f), Views.Find(f.Model, plan)!, print: true);
        // (Lines beyond the view's bottom draw in the same grey: only the cut and seen pens count here.)
        bool Drawn(Pen p) => p.Layer is PenLayer.Cut or PenLayer.Seen;
        var grey = rendered.Pens.Where(p => Drawn(p) && p.Stroke == Links.Halftone).SelectMany(p => p.Segments).ToList();
        Assert.NotEmpty(grey);
        // The grey lines are the foundation wall's (y = 3 ± its thickness), the ground floor's wall keeps its own pens.
        Assert.All(grey, s => Assert.True(s.A.Y > 2 && s.B.Y > 2, $"{s.A} {s.B}"));
        var black = rendered.Pens.Where(p => Drawn(p) && p.Stroke != Links.Halftone).SelectMany(p => p.Segments).ToList();
        Assert.Contains(black, s => s.A.Y < 1 && s.B.Y < 1);
        Assert.NotNull(groundWall);
        Assert.NotNull(belowWall);
    }

    [Fact]
    public void Storeys_shown_are_refused_for_what_is_not_a_plan_or_a_storey()
    {
        var (f, _, _, groundWall, _, plan) = Project();
        Assert.Throws<CommandException>(() => f.Run("view.setStoreys", new JsonObject { ["id"] = plan, ["mode"] = "some", ["ids"] = new JsonArray(groundWall) }));
        Assert.Throws<CommandException>(() => f.Run("view.setStoreys", new JsonObject { ["id"] = plan, ["mode"] = "below" }));
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, -1), ["end"] = new JsonArray(0, 4) })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("view.setStoreys", new JsonObject { ["id"] = section, ["mode"] = "all" }));
    }

    [Fact]
    public void A_plan_keeps_its_reference_level()
    {
        var (f, ground, below, _, _, plan) = Project();
        f.Run("view.setUnderlay", new JsonObject { ["id"] = plan, ["storeyId"] = below });
        Assert.Equal(below, f.Model.Get(plan)!.Str("underlay"));
        Assert.Throws<CommandException>(() => f.Run("view.setUnderlay", new JsonObject { ["id"] = plan, ["storeyId"] = ground }));
        f.Run("view.setUnderlay", new JsonObject { ["id"] = plan, ["storeyId"] = null });
        Assert.Null(f.Model.Get(plan)!.Attrs["underlay"]);
    }
}
