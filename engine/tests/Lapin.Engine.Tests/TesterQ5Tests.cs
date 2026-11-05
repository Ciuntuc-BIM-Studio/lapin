using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// A tester's six items (decision 312): drafted shapes in the 3D view on their view's plane, and bare wall openings
/// picked and edited as doors and windows are (their storey, their hit area and outline, moved and resized as one
/// undo step each).
/// </summary>
public sealed class TesterQ5Tests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());

    static string Draw(Fixture f, string viewId, JsonObject shape, string? role = null)
    {
        var a = new JsonObject { ["viewId"] = viewId, ["shapes"] = new JsonArray(shape) };
        if (role != null) a["role"] = role;
        return (string)f.Run("drafting.draw", a)!["ids"]!.AsArray()[0]!;
    }
    static JsonArray J(double x, double y) => new(x, y);

    [Fact]
    public void A_shape_drafted_on_a_plan_stands_in_3D_at_its_storey_s_level_a_circle_in_chords_construction_lines_left_out()
    {
        var f = F();
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
        var line = Draw(f, $"plan:{upper}", new JsonObject { ["type"] = "line", ["a"] = J(0, 0), ["b"] = J(4, 0) });
        var g = f.Session.Geometry(line)!;
        Assert.Empty(g.Parts);
        var run = Assert.Single(g.Lines!);
        Assert.Equal([new Vec3(0, 0, 3), new Vec3(4, 0, 3)], run);

        var circle = Draw(f, $"plan:{upper}", new JsonObject { ["type"] = "circle", ["c"] = J(1, 1), ["r"] = 2 });
        var ring = Assert.Single(f.Session.Geometry(circle)!.Lines!);
        Assert.True(ring.Count > 16);
        Assert.All(ring, p => { Assert.Equal(3, p.Z, 9); Assert.Equal(2, Math.Sqrt((p.X - 1) * (p.X - 1) + (p.Y - 1) * (p.Y - 1)), 6); });
        Assert.Equal(ring[0].X, ring[^1].X, 9);

        // Construction lines stay on their view; a dimension is no draft at all.
        var construction = Draw(f, $"plan:{upper}", new JsonObject { ["type"] = "line", ["a"] = J(0, 1), ["b"] = J(4, 1) }, "draft");
        Assert.Null(f.Session.Geometry(construction));

        // The storey raised: the line rises with it.
        f.Run("storey.update", new JsonObject { ["id"] = upper, ["elevation"] = 3.5 });
        Assert.Equal(3.5, f.Session.Geometry(line)!.Lines![0][0].Z, 9);
    }

    [Fact]
    public void A_shape_drafted_on_a_reference_plane_stands_on_that_plane_in_3D()
    {
        var f = F();
        // A plane through (2, 0, 1) facing +x: its x runs along +y, its y up.
        var plane = (string)f.Run("view.create", new JsonObject { ["kind"] = "plane", ["point"] = new JsonArray(2, 0, 1), ["normal"] = new JsonArray(1, 0, 0) })!["id"]!;
        var line = Draw(f, plane, new JsonObject { ["type"] = "line", ["a"] = J(0, 0), ["b"] = J(0, 2) });
        var run = Assert.Single(f.Session.Geometry(line)!.Lines!);
        Assert.All(run, p => Assert.Equal(2, p.X, 9));
        Assert.Equal(2, Math.Abs(run[1].Z - run[0].Z), 9);
    }

    [Fact]
    public void A_bare_wall_opening_is_on_its_wall_s_storey_with_a_hit_area_over_the_gap_and_its_outline_on_the_wall()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0);
        var id = (string)f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 2, ["width"] = 1.2, ["height"] = 2.0, ["sill"] = 0.5 })!["id"]!;
        var diff = f.RunDiff("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["height"] = 2.1 });
        Assert.Equal(f.Storey, diff.Nodes.Single(n => n.Id == id).StoreyId);
        var item = diff.Render.Single(r => r.NodeId == id);
        Assert.Equal(f.Storey, item.StoreyId);
        var hit = Assert.Single(item.Plan, p => p.Kind == "hit");
        Assert.True(hit.Closed);
        var xs = Enumerable.Range(0, hit.Points.Length / 2).Select(i => hit.Points[2 * i]).ToList();
        Assert.Equal(1.4, xs.Min(), 6);
        Assert.Equal(2.6, xs.Max(), 6);
        var g = f.Session.Geometry(id)!;
        Assert.Empty(g.Parts);
        var outline = Assert.Single(g.Lines!);
        Assert.Equal(0.5, outline.Min(p => p.Z), 6);
        Assert.Equal(2.6, outline.Max(p => p.Z), 6);

        // An opening a door fills has none of it (the door is what is picked).
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 4.5 })!["id"]!;
        Assert.Null(f.Session.Geometry(Openings.OpeningOf(f.Model, door)!.Id));
    }

    [Fact]
    public void A_bare_opening_s_jamb_dragged_resizes_and_moves_it_in_one_undo_step_refused_where_it_does_not_fit()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0);
        var id = (string)f.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = wall, ["along"] = 2, ["width"] = 1 })!["id"]!;
        var before = f.Session.Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid));
        // The far jamb from 2.5 to 3.0, the near one kept at 1.5: the grip's one command.
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["along"] = 2.25, ["width"] = 1.5 });
        Assert.Equal(1.5, f.Model.Get(id)!.Num("width"), 9);
        Assert.Equal(2.25, f.Model.Get(id)!.Num("along"), 9);
        Assert.True(f.Session.Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid)) < before - 1e-6);
        // Dragged along the wall by its middle.
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = id, ["along"] = 4 });
        Assert.Equal(4, f.Model.Get(id)!.Num("along"), 9);
        // Past the wall's end: refused, as a window's is.
        Assert.Throws<CommandException>(() => f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = id, ["along"] = 5.6, ["width"] = 1.5 }));
        f.Session.Undo();
        Assert.Equal(2.25, f.Model.Get(id)!.Num("along"), 9);
        f.Session.Undo();
        Assert.Equal(1.0, f.Model.Get(id)!.Num("width"), 9);
        Assert.Equal(2.0, f.Model.Get(id)!.Num("along"), 9);
    }
}
