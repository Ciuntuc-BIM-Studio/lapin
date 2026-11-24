using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A floor seen from above hatched in plan by its top layer's surface pattern (forum topic 259): the top face of a floor below
/// the plan's cut, its openings out, under the walls cut over it, on its own layer, as the view style's <c>floorHatch</c> says
/// (on in a new project's Architectural style, off in the others and in a project saved before).
/// </summary>
public sealed class FloorPlanHatchTests
{
    /// <summary>A 6 × 4 floor of parquet (20, its surface diagonal lines) on concrete (180), a 1 × 1 opening in it, a brick wall across it at y 2.</summary>
    static (Fixture F, string Slab, string Wall) Floor(double offset = 0)
    {
        var f = new Fixture();
        var type = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Parquet on concrete",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Parquet", ["thickness"] = 0.02, ["core"] = false },
                new JsonObject { ["material"] = "Concrete", ["thickness"] = 0.18, ["core"] = true }),
        })!["id"]!;
        var slab = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = f.Storey, ["typeId"] = type, ["offset"] = offset,
            ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 4), new JsonArray(0, 4)),
        })!["id"]!;
        f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = slab, ["points"] = new JsonArray(new JsonArray(2, 0.5), new JsonArray(3, 0.5), new JsonArray(3, 1.5), new JsonArray(2, 1.5)) });
        Surface(f, "Parquet", new JsonObject { ["hatch"] = "LINES45" });
        // The concrete under it shows nothing of its own (its top is hidden by the parquet anyway).
        Surface(f, "Concrete", new JsonObject { ["hatch"] = null });
        var wall = f.Wall(-1, 2, 7, 2, f.Type("Brick", ("Brick", 0.25, true)), height: 3);
        return (f, slab, wall);
    }

    static void Surface(Fixture f, string material, JsonObject elevation) =>
        f.Run("material.update", new JsonObject { ["id"] = f.Model.OfClass(IfcClass.Material).First(m => m.Name == material).Id, ["elevation"] = elevation });

    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;

    static Views.Rendered Render(Fixture f, string plan) => Views.Render(f.Session.Reader, Views.Find(f.Model, plan)!);

    static List<(Vec2 A, Vec2 B)> Hatch(Views.Rendered r) => r.Pens.Where(p => p.Layer == SurfaceHatches.FloorLayer).SelectMany(p => p.Segments).ToList();

    static Vec2 Mid((Vec2 A, Vec2 B) s) => (s.A + s.B) * 0.5;

    [Fact]
    public void A_floor_seen_from_above_is_hatched_by_its_top_layers_surface_inside_its_outline_without_its_opening_under_the_walls()
    {
        var (f, _, _) = Floor();
        var r = Render(f, Plan(f));
        var hatch = Hatch(r);
        Assert.NotEmpty(hatch);
        const double e = 1e-6;
        // Inside the floor's outline.
        Assert.All(hatch, s => Assert.True(Mid(s).X >= -e && Mid(s).X <= 6 + e && Mid(s).Y >= -e && Mid(s).Y <= 4 + e, $"{Mid(s)} is outside the floor."));
        // Diagonal lines (LINES45), not a grid.
        Assert.All(hatch, s => Assert.True(Math.Abs(Math.Abs(s.B.X - s.A.X) - Math.Abs(s.B.Y - s.A.Y)) < 1e-6));
        // None in the opening.
        Assert.DoesNotContain(hatch, s => Mid(s) is var p && p.X > 2 + e && p.X < 3 - e && p.Y > 0.5 + e && p.Y < 1.5 - e);
        // None under the wall cut across it (y 1.875 to 2.125); some on either side.
        Assert.DoesNotContain(hatch, s => Mid(s).Y > 2 - 0.125 + e && Mid(s).Y < 2 + 0.125 - e);
        Assert.Contains(hatch, s => Mid(s).Y < 1.8);
        Assert.Contains(hatch, s => Mid(s).Y > 2.2);
        // Under the model's cut lines, in the hatch pen.
        var pen = r.Pens.First(p => p.Layer == SurfaceHatches.FloorLayer);
        Assert.True(r.Pens.IndexOf(pen) < r.Pens.FindIndex(p => p.Layer == PenLayer.Cut));
        Assert.Null(pen.Fill);
    }

    [Fact]
    public void The_view_style_turns_it_off_a_view_on_again_and_the_structural_style_draws_none()
    {
        var (f, _, _) = Floor();
        var plan = Plan(f);
        Assert.NotEmpty(Hatch(Render(f, plan)));
        Assert.True(ViewStyles.All(f.Model).Single(s => s.Id == "architectural").FloorHatch);
        Assert.All(ViewStyles.All(f.Model).Where(s => s.Id != "architectural"), s => Assert.False(s.FloorHatch));

        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["floorHatch"] = false });
        Assert.Empty(Hatch(Render(f, plan)));
        Assert.False((bool)Spatial.Project(f.Model)!.Attrs[ViewStyles.Key]!.AsArray().Single(s => (string?)s!["id"] == "architectural")!["floorHatch"]!);
        // The view's own choice over its style's.
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["floorHatch"] = true });
        Assert.NotEmpty(Hatch(Render(f, plan)));

        var structural = Plan(f);
        f.Run("view.setStyle", new JsonObject { ["id"] = structural, ["style"] = "structural" });
        Assert.Empty(Hatch(Render(f, structural)));
    }

    [Fact]
    public void A_project_saved_before_keeps_its_plans_without_floor_hatches()
    {
        var (f, _, _) = Floor();
        var plan = Plan(f);
        Assert.NotEmpty(Hatch(Render(f, plan)));
        // As a build before saved it: no mark, no styles of its own.
        var saved = f.Model.Entities.Select(e =>
        {
            if (e.Class != IfcClass.Project) return e;
            var a = e.CloneAttrs();
            a.Remove(ViewStyles.FloorHatchRevKey);
            a.Remove(ViewStyles.Key);
            return e.With(attrs: a);
        }).ToList();
        f.Session.Load(saved, f.Model.Relations.ToList(), null);
        Assert.Empty(Hatch(Render(f, plan)));
        var project = Spatial.Project(f.Model)!;
        Assert.Equal(ViewStyles.FloorHatchRev, (int)project.Attrs[ViewStyles.FloorHatchRevKey]!);
        Assert.All(ViewStyles.All(f.Model), s => Assert.False(s.FloorHatch));
        // Turned on by the user, it draws.
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["floorHatch"] = true });
        Assert.NotEmpty(Hatch(Render(f, plan)));

        // One with styles of its own: each says off.
        var own = f.Model.Entities.Select(e =>
        {
            if (e.Class != IfcClass.Project) return e;
            var a = e.CloneAttrs();
            a.Remove(ViewStyles.FloorHatchRevKey);
            foreach (var s in a[ViewStyles.Key]!.AsArray().OfType<JsonObject>()) s.Remove("floorHatch");
            return e.With(attrs: a);
        }).ToList();
        f.Session.Load(own, f.Model.Relations.ToList(), null);
        Assert.Empty(Hatch(Render(f, plan)));
        Assert.All(Spatial.Project(f.Model)!.Attrs[ViewStyles.Key]!.AsArray(), s => Assert.False((bool)s!["floorHatch"]!));
    }

    [Fact]
    public void A_rooms_floor_finish_lies_over_the_floor_so_its_hatch_draws_there_and_the_floors_does_not()
    {
        var (f, _, _) = Floor();
        // A room closed by walls on the floor's left part (0..2 by 2.125..4 is outside it; the room 0.125..1.875 by 2.125..3.875).
        foreach (var ((x1, y1), (x2, y2)) in new[] { ((0.0, 2.0), (0.0, 4.0)), ((0.0, 4.0), (2.0, 4.0)), ((2.0, 4.0), (2.0, 2.0)) })
            f.Wall(x1, y1, x2, y2, f.Model.OfClass(IfcClass.WallType).First(t => t.Name == "Brick").Id, height: 3);
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 1, ["y"] = 3 })!["id"]!;
        var plan = Plan(f);
        Assert.Contains(Hatch(Render(f, plan)), s => Mid(s).X is > 0.2 and < 1.8 && Mid(s).Y is > 2.2 and < 3.8);
        f.Run("architecture.applyRoomFinish", new JsonObject
        {
            ["roomId"] = room, ["typeId"] = (string)f.Run("architecture.roomFinishTypeFromLibrary", new JsonObject { ["name"] = "Bathroom" })!["id"]!,
        });
        var r = Render(f, plan);
        Assert.NotEmpty(r.Pens.Where(p => p.Layer == Lapin.Architecture.Rooms.FinishLayer).SelectMany(p => p.Segments));
        // The finish covers the room: the floor's own hatch only outside it.
        Assert.DoesNotContain(Hatch(r), s => Mid(s).X is > 0.2 and < 1.8 && Mid(s).Y is > 2.2 and < 3.8);
        Assert.Contains(Hatch(r), s => Mid(s).X > 2.5);
    }

    [Fact]
    public void A_floor_the_plan_cuts_is_not_hatched_from_above()
    {
        // Its top at 1.5, over the plan's cut (1.2): cut, never seen from above.
        var (f, _, _) = Floor(offset: 1.5);
        Assert.Empty(Hatch(Render(f, Plan(f))));
    }

    [Fact]
    public void A_hidden_floor_draws_no_hatch()
    {
        var (f, slab, _) = Floor();
        var plan = Plan(f);
        f.Run("view.hideElements", new JsonObject { ["viewId"] = plan, ["ids"] = new JsonArray(slab) });
        Assert.Empty(Hatch(Render(f, plan)));
    }

    [Fact]
    public void The_hatch_goes_to_DXF_on_its_own_layer()
    {
        var (f, _, _) = Floor();
        var dxf = System.Text.Encoding.Latin1.GetString(Views.Dxf(f.Session.Reader, Views.Find(f.Model, Plan(f))!));
        Assert.Contains(SurfaceHatches.FloorLayer, dxf);
    }
}
