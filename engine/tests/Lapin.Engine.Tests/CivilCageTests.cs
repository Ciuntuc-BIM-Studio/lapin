using System.Text.Json.Nodes;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A terrain's control cage (issue #113): a few chosen survey points drive the others, the free points following by
/// the cage's interpolation of the controls' moves (Cage.cs); with no controls the point commands work as before.
/// </summary>
public class CivilCageTests
{
    static Fixture F() => new(new Lapin.Architecture.ArchitectureModule(), new CivilModule());

    /// <summary>A 40 × 40 m terrain with a point every 2 m (21 × 21), heights z(x, y), datum 0.</summary>
    static string Field(Fixture f, Func<double, double, double>? z = null)
    {
        z ??= (_, _) => 0;
        var pts = new JsonArray();
        for (var i = 0; i <= 20; i++) for (var j = 0; j <= 20; j++) pts.Add(new JsonArray(i * 2.0, j * 2.0, z(i * 2.0, j * 2.0)));
        return (string)f.Run("civil.addTerrain", new JsonObject { ["points"] = pts, ["datum"] = 0, ["interval"] = 0.5, ["majorEvery"] = 2, ["depth"] = 1 })!["id"]!;
    }

    static int Index(double x, double y) => (int)Math.Round(x / 2) * 21 + (int)Math.Round(y / 2);

    static List<Vec3> Pts(Fixture f, string t) => Terrains.Points(f.Model.Get(t)!);

    static void Controls(Fixture f, string t, params int[] indices) =>
        f.Run("civil.setTerrainControls", new JsonObject { ["id"] = t, ["indices"] = new JsonArray([.. indices.Select(i => (JsonNode)i)]) });

    [Fact]
    public void With_no_controls_moving_a_point_moves_that_point_only_as_before()
    {
        var f = F();
        var t = Field(f);
        var before = Pts(f, t);
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = t, ["index"] = Index(20, 20), ["dz"] = 1.5 });
        var after = Pts(f, t);
        for (var i = 0; i < before.Count; i++)
            Assert.Equal(i == Index(20, 20) ? before[i].Z + 1.5 : before[i].Z, after[i].Z, 12);
        f.Run("civil.setTerrainPoints", new JsonObject { ["id"] = t, ["indices"] = new JsonArray(0, 1), ["dz"] = 1.0 });
        Assert.Equal(1.0, Pts(f, t)[0].Z, 12);
        Assert.Equal(0.0, Pts(f, t)[2].Z, 12);
        Assert.Null(f.Model.Get(t)!.Attrs[Cage.Key]);
    }

    [Fact]
    public void A_control_keeps_its_exact_height_and_the_free_points_between_two_controls_rise_smoothly_and_monotonically()
    {
        var f = F();
        var t = Field(f);
        int a = Index(0, 20), b = Index(40, 20);
        Controls(f, t, a, b);
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = t, ["index"] = b, ["dz"] = 2.0 });
        var p = Pts(f, t);
        Assert.Equal(2.0, p[b].Z, 12);
        Assert.Equal(0.0, p[a].Z, 12);
        var prev = 0.0;
        for (var x = 2; x <= 38; x += 2)
        {
            var z = p[Index(x, 20)].Z;
            Assert.True(z > prev, $"x = {x}: {z} after {prev}");
            Assert.True(z < 2.0);
            prev = z;
        }
        // The middle rises by nearly half the move (the cage's reach lets it sag a little towards where it was).
        Assert.InRange(p[Index(20, 20)].Z, 0.9, 1.0);
        // A height typed for a control: exactly that.
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = t, ["index"] = a, ["z"] = -0.75 });
        p = Pts(f, t);
        Assert.Equal(-0.75, p[a].Z, 12);
        Assert.Equal(2.0, p[b].Z, 12);
    }

    [Fact]
    public void The_free_points_keep_their_shape_moving_by_the_cage_and_one_move_is_one_undo_step()
    {
        var f = F();
        var t = Field(f, (x, y) => 0.1 * x + Math.Sin(y / 5));
        Controls(f, t, Index(0, 0), Index(40, 0), Index(0, 40), Index(40, 40), Index(20, 20));
        var before = Pts(f, t);
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = t, ["index"] = Index(20, 20), ["dz"] = 3.0 });
        var after = Pts(f, t);
        // Each free point moves by a part of the control's move, between none and all of it, the nearer the more.
        for (var i = 0; i < before.Count; i++)
        {
            var d = after[i].Z - before[i].Z;
            Assert.InRange(d, -1e-12, 3.0 + 1e-12);
        }
        Assert.True(after[Index(22, 20)].Z - before[Index(22, 20)].Z > after[Index(30, 20)].Z - before[Index(30, 20)].Z);
        // Back down by as much: every point where it was (the free points follow the controls' offsets, not their heights).
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = t, ["index"] = Index(20, 20), ["dz"] = -3.0 });
        var back = Pts(f, t);
        for (var i = 0; i < before.Count; i++) Assert.Equal(before[i].Z, back[i].Z, 9);
        // One undo step takes the whole move back: the control and every free point.
        f.Session.Undo();
        var undone = Pts(f, t);
        for (var i = 0; i < before.Count; i++) Assert.Equal(after[i].Z, undone[i].Z, 12);
        f.Session.Undo();
        var first = Pts(f, t);
        for (var i = 0; i < before.Count; i++) Assert.Equal(before[i].Z, first[i].Z, 12);
    }

    [Fact]
    public void Controls_are_chosen_by_index_toggled_by_a_grid_spacing_or_along_the_boundary_and_cleared()
    {
        var f = F();
        var t = Field(f);
        Controls(f, t, 0, 5);
        Assert.Equal([0, 5], Cage.Indices(f.Model.Get(t)!));
        f.Run("civil.setTerrainControls", new JsonObject { ["id"] = t, ["indices"] = new JsonArray(5, 7), ["mode"] = "toggle" });
        Assert.Equal([0, 7], Cage.Indices(f.Model.Get(t)!));
        f.Run("civil.setTerrainControls", new JsonObject { ["id"] = t, ["indices"] = new JsonArray(0), ["mode"] = "remove" });
        Assert.Equal([7], Cage.Indices(f.Model.Get(t)!));
        // One every 10 m: the points at the nodes of a 10 m grid over the 40 m field, its corners among them.
        f.Run("civil.setTerrainControls", new JsonObject { ["id"] = t, ["spacing"] = 10.0 });
        var grid = Cage.Indices(f.Model.Get(t)!);
        Assert.Equal(25, grid.Count);
        Assert.Contains(Index(0, 0), grid);
        Assert.Contains(Index(20, 30), grid);
        Assert.Contains(Index(40, 40), grid);
        // Along the boundary: the points on the terrain's outline.
        f.Run("civil.setTerrainControls", new JsonObject { ["id"] = t, ["boundary"] = true });
        var rim = Cage.Indices(f.Model.Get(t)!);
        Assert.True(rim.Count >= 4);
        Assert.All(rim, i => { var p = Pts(f, t)[i]; Assert.True(p.X < 1e-9 || p.Y < 1e-9 || p.X > 40 - 1e-9 || p.Y > 40 - 1e-9); });
        f.Run("civil.setTerrainControls", new JsonObject { ["id"] = t, ["clear"] = true });
        Assert.Null(f.Model.Get(t)!.Attrs[Cage.Key]);
        // Choosing controls moves no point, and is one undo step.
        Assert.All(Pts(f, t), p => Assert.Equal(0, p.Z, 12));
        f.Session.Undo();
        Assert.Equal(rim, Cage.Indices(f.Model.Get(t)!));
    }

    [Fact]
    public void A_control_removed_or_moved_in_plan_split_off_or_merged_keeps_the_cage_consistent()
    {
        var f = F();
        var t = Field(f);
        Controls(f, t, Index(4, 20), Index(36, 20), Index(20, 4));
        // Moved in plan: it stays a control where it went.
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = t, ["index"] = Index(20, 4), ["x"] = 21.0, ["dz"] = 0.0 });
        Assert.Contains(Index(20, 4), Cage.Indices(f.Model.Get(t)!));
        Assert.Equal(3, Cage.Of(f.Model.Get(t)!).Count);
        // Removed: the control goes too.
        f.Run("civil.removeTerrainPoint", new JsonObject { ["id"] = t, ["index"] = Index(20, 4) });
        Assert.Equal(2, Cage.Of(f.Model.Get(t)!).Count);
        Assert.Equal(2, Cage.Indices(f.Model.Get(t)!).Count);
        // Split down the middle: each half keeps the controls inside it.
        var ids = ((JsonObject)f.Run("civil.splitTerrain", new JsonObject { ["id"] = t, ["x1"] = 19.0, ["y1"] = -5, ["x2"] = 19.0, ["y2"] = 45 })!)["ids"]!.AsArray().Select(x => (string)x!).ToList();
        var halves = ids.Select(id => Cage.Of(f.Model.Get(id)!)).ToList();
        Assert.All(halves, h => Assert.Single(h));
        Assert.NotEqual(halves[0][0], halves[1][0]);
        // A control moved on a half still drives that half.
        var half = ids[0];
        var ci = Cage.Indices(f.Model.Get(half)!)[0];
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = half, ["index"] = ci, ["dz"] = 1.0 });
        Assert.Equal(1.0, Pts(f, half)[ci].Z, 12);
        // Merged: both controls again.
        var merged = (string)((JsonObject)f.Run("civil.mergeTerrains", new JsonObject { ["ids"] = new JsonArray(ids[0], ids[1]) })!)["id"]!;
        Assert.Equal(2, Cage.Indices(f.Model.Get(merged)!).Count);
        // Replacing the points drops the controls that are gone, without failing.
        f.Run("civil.updateTerrain", new JsonObject { ["id"] = merged, ["text"] = "0 0 0\n10 0 0\n0 10 0\n10 10 1" });
        Assert.Empty(Cage.Indices(f.Model.Get(merged)!));
        f.Run("civil.moveTerrainPoint", new JsonObject { ["id"] = merged, ["index"] = 3, ["dz"] = 1.0 });
        Assert.Equal(2.0, Pts(f, merged)[3].Z, 12);
    }

    [Fact]
    public void The_interpolation_returns_the_controls_moves_at_the_controls_and_fades_away_from_them()
    {
        var controls = new List<Vec2> { new(0, 0), new(10, 0) };
        var w = Cage.Shift(controls, [0.0, 1.0], new Vec2(10, 0), 5);
        Assert.Equal(1.0, w, 12);
        Assert.Equal(0.0, Cage.Shift(controls, [0.0, 1.0], new Vec2(0, 0), 5), 12);
        // One control alone: a smooth bump, half its move at the reach, little far away.
        var one = new List<Vec2> { new(0, 0) };
        Assert.Equal(0.5, Cage.Shift(one, [1.0], new Vec2(5, 0), 5), 12);
        Assert.True(Cage.Shift(one, [1.0], new Vec2(50, 0), 5) < 0.01);
    }
}
