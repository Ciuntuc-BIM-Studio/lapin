using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Decision 739 (the user's answer to decision 738: build the angle lock): an angular dimension held on two walls with its padlock
/// closed keeps the angle between them whatever command turns one of them, the other turned about where they meet in the same
/// undo step; chains follow, a lock that cannot be kept refuses the command.
/// </summary>
public class AngleLockTests
{
    static JsonArray J(double x, double y) => [x, y];

    sealed class Setup
    {
        public required Fixture F;
        public required string Plan, A, B, Dim, Type;
        public double Dir(string id)
        {
            var e = F.Model.Get(id)!;
            var (s, t) = (Args.PointOf(e.Attrs["start"]), Args.PointOf(e.Attrs["end"]));
            return Math.Atan2(t.Y - s.Y, t.X - s.X) * 180 / Math.PI;
        }
        public double Value(string? dim = null) => (double)F.Session.Query("drafting.dimension", new JsonObject { ["id"] = dim ?? Dim })!["value"]!;
        public string Lock(string a, string b, double wa = 1, double wb = 1, bool locked = true) => (string)F.Run("drafting.addDimension", new JsonObject
        {
            ["viewId"] = Plan, ["dimKind"] = "angular", ["c"] = J(0, 0), ["a"] = J(1, 0), ["b"] = J(0, 1), ["at"] = J(0.7, 0.7), ["locked"] = locked,
            ["holds"] = new JsonArray(new JsonObject { ["element"] = a, ["way"] = wa }, new JsonObject { ["element"] = b, ["way"] = wb }),
        })!["id"]!;
        public void Rotate(double angle, double cx, double cy, params string[] ids) =>
            F.Run("element.transform", new JsonObject { ["ids"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]), ["op"] = "rotate", ["center"] = J(cx, cy), ["angle"] = angle });
    }

    /// <summary>A wall along x from the origin and one at 60° from the same corner, their angle locked.</summary>
    static Setup Corner(bool locked = true)
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var t = f.Type("Brick 300", ("Brick", 0.3, true));
        var a = f.Wall(0, 0, 6, 0, t);
        var b = f.Wall(0, 0, 4 * Math.Cos(Math.PI / 3), 4 * Math.Sin(Math.PI / 3), t);
        var s = new Setup { F = f, Plan = plan, A = a, B = b, Dim = "", Type = t };
        return new Setup { F = f, Plan = plan, A = a, B = b, Dim = s.Lock(a, b, locked: locked), Type = t };
    }

    [Fact]
    public void Turning_one_wall_turns_the_other_about_the_corner_in_the_same_undo_step()
    {
        var s = Corner();
        Assert.Equal(60, s.Value(), 6);
        Assert.True((bool)s.F.Session.Query("drafting.dimension", new JsonObject { ["id"] = s.Dim })!["locked"]!);
        Assert.Equal(60, s.F.Model.Get(s.Dim)!.Num(AngleLocks.AngleKey), 6);

        s.Rotate(10, 0, 0, s.A);
        Assert.Equal(10, s.Dir(s.A), 6);
        Assert.Equal(70, s.Dir(s.B), 6);
        Assert.Equal(60, s.Value(), 6);
        // The corner stays where it was: the second turned about it.
        Assert.Equal(new[] { 0.0, 0.0 }, s.F.Model.Get(s.B)!.Attrs["start"]!.AsArray().Select(x => Math.Round((double)x!, 9)));

        // One undo step puts both back.
        s.F.Session.Undo();
        Assert.Equal(0, s.Dir(s.A), 6);
        Assert.Equal(60, s.Dir(s.B), 6);

        // Unlocked (its properties' Locked untick): the other stays put, as before decision 739.
        s.F.Run("element.update", new JsonObject { ["id"] = s.Dim, ["attrs"] = new JsonObject { ["locked"] = false } });
        s.Rotate(10, 0, 0, s.A);
        Assert.Equal(60, s.Dir(s.B), 6);
        Assert.Equal(50, s.Value(), 6);
        // Locked again: kept at what it reads now.
        s.F.Run("element.update", new JsonObject { ["id"] = s.Dim, ["attrs"] = new JsonObject { ["locked"] = true } });
        Assert.Equal(50, s.F.Model.Get(s.Dim)!.Num(AngleLocks.AngleKey), 6);
        s.Rotate(-10, 0, 0, s.B);
        Assert.Equal(0, s.Dir(s.A), 6);
        Assert.Equal(50, s.Value(), 6);
    }

    [Fact]
    public void An_end_dragged_turns_the_partner_and_both_turned_together_keep_the_angle_with_no_extra_turn()
    {
        var s = Corner();
        // The second wall's far end dragged to 75°: the first turns 15° about the corner.
        s.F.Run("architecture.updateWall", new JsonObject { ["id"] = s.B, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4 * Math.Cos(75 * Math.PI / 180), ["y2"] = 4 * Math.Sin(75 * Math.PI / 180) });
        Assert.Equal(75, s.Dir(s.B), 6);
        Assert.Equal(15, s.Dir(s.A), 6);
        Assert.Equal(60, s.Value(), 6);

        // Both turned by one command: they keep their angle, nothing more turns.
        s.Rotate(30, 3, 3, s.A, s.B);
        Assert.Equal(45, s.Dir(s.A), 6);
        Assert.Equal(105, s.Dir(s.B), 6);
        // Both moved: nothing turns.
        s.F.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(s.A), ["op"] = "move", ["by"] = J(0, 0) });
        Assert.Equal(60, s.Value(), 6);
    }

    [Fact]
    public void A_chain_of_locks_follows_without_looping()
    {
        var s = Corner();
        // A third wall square to the first at its far end, its angle locked too: A–B and A–C.
        var c = s.F.Wall(6, 0, 6, 4, s.Type);
        var ac = s.Lock(s.A, c, -1, 1);
        Assert.Equal(90, s.Value(ac), 6);

        // B turned 10°: A follows about their corner, then C about A's end (where it went).
        s.Rotate(10, 0, 0, s.B);
        Assert.Equal(70, s.Dir(s.B), 6);
        Assert.Equal(10, s.Dir(s.A), 6);
        Assert.Equal(100, s.Dir(c), 6);
        Assert.Equal(60, s.Value(), 6);
        Assert.Equal(90, s.Value(ac), 6);
        s.F.Session.Undo();
        Assert.Equal(90, s.Dir(c), 6);
    }

    [Fact]
    public void A_lock_that_cannot_be_kept_refuses_the_command_and_nothing_changes()
    {
        var s = Corner();
        // Pinned: the partner would have to turn.
        s.F.Run("element.pin", new JsonObject { ["ids"] = new JsonArray(s.A) });
        var before = s.F.Model.Get(s.B)!.Attrs["end"]!.ToJsonString();
        var e = Assert.Throws<CommandException>(() => s.Rotate(10, 0, 0, s.B));
        Assert.Contains("pinned", e.Message);
        Assert.Equal(before, s.F.Model.Get(s.B)!.Attrs["end"]!.ToJsonString());
        s.F.Run("element.pin", new JsonObject { ["ids"] = new JsonArray(s.A), ["pinned"] = false });

        // A triangle with every angle locked, one of them typed to another value: the loop cannot agree.
        var f = new Fixture(new ArchitectureModule(), new DraftingModule());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var t = f.Type("Brick 300", ("Brick", 0.3, true));
        var h = 6 * Math.Sin(Math.PI / 3);
        var (a, b, c) = (f.Wall(0, 0, 6, 0, t), f.Wall(0, 0, 3, h, t), f.Wall(6, 0, 3, h, t));
        var tri = new Setup { F = f, Plan = plan, A = a, B = b, Dim = "", Type = t };
        var ab = tri.Lock(a, b);
        tri.Lock(b, c, -1, -1);
        tri.Lock(a, c, -1, 1);
        var ends = string.Join(";", new[] { a, b, c }.Select(id => f.Model.Get(id)!.Attrs["end"]!.ToJsonString()));
        e = Assert.Throws<CommandException>(() => f.Run("drafting.setDimensionValue", new JsonObject { ["id"] = ab, ["value"] = 70 }));
        Assert.Contains("locked", e.Message);
        Assert.Equal(ends, string.Join(";", new[] { a, b, c }.Select(id => f.Model.Get(id)!.Attrs["end"]!.ToJsonString())));
        Assert.Equal(60, tri.Value(ab), 6);
    }

    [Fact]
    public void A_value_typed_turns_the_second_and_the_lock_keeps_the_new_value()
    {
        var s = Corner();
        s.F.Run("drafting.setDimensionValue", new JsonObject { ["id"] = s.Dim, ["value"] = 75 });
        Assert.Equal(75, s.Value(), 6);
        Assert.Equal(0, s.Dir(s.A), 6);
        Assert.Equal(75, s.F.Model.Get(s.Dim)!.Num(AngleLocks.AngleKey), 6);
        s.Rotate(5, 0, 0, s.A);
        Assert.Equal(80, s.Dir(s.B), 6);
        Assert.Equal(75, s.Value(), 6);
        // One step: the typed value and its turn undone together, the lock back at 60.
        s.F.Session.Undo();
        s.F.Session.Undo();
        Assert.Equal(60, s.F.Model.Get(s.Dim)!.Num(AngleLocks.AngleKey), 6);
        Assert.Equal(60, s.Dir(s.B), 6);

        // An element gone leaves the dimension an orphan; the other turns freely.
        s.F.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(s.B) });
        s.Rotate(10, 0, 0, s.A);
        Assert.True((bool)s.F.Session.Query("drafting.dimension", new JsonObject { ["id"] = s.Dim })!["orphan"]!);
    }

    [Fact]
    public void The_lock_survives_the_project_saved_and_opened_again()
    {
        var s = Corner();
        using var ms = new MemoryStream();
        ProjectPackage.Write(s.F.Session, ms);
        ms.Position = 0;
        var content = LapinPackage.Read(ms);
        var g = new Fixture(new ArchitectureModule(), new DraftingModule());
        g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        var back = new Setup { F = g, Plan = s.Plan, A = s.A, B = s.B, Dim = s.Dim, Type = s.Type };
        Assert.True(AngleLocks.IsLocked(g.Model.Get(s.Dim)));
        back.Rotate(-20, 0, 0, s.A);
        Assert.Equal(40, back.Dir(s.B), 6);
        Assert.Equal(60, back.Value(), 6);

        // A dimension left unlocked (every project before decision 739) follows and keeps nothing.
        var u = Corner(locked: false);
        Assert.False(AngleLocks.IsLocked(u.F.Model.Get(u.Dim)));
        u.Rotate(10, 0, 0, u.A);
        Assert.Equal(60, u.Dir(u.B), 6);
    }
}
