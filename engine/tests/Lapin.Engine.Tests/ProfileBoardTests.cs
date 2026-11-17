using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// The wall profile and the section profile on the shared sketch canvas (audit S11 stage 2, docs/plan-sketch-canvas.md §4): each
/// opened on a reference plane of its own, its payload copied in as drafts; finished unchanged, the same JSON; edits written through
/// the payload's own command in one undo step; shapes that make no payload refused, saying why, the board kept open.
/// </summary>
public sealed class ProfileBoardTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray P(params double[] uv) => new([.. Enumerable.Range(0, uv.Length / 2).Select(i => (JsonNode)new JsonArray(uv[2 * i], uv[2 * i + 1]))]);

    static (string View, List<string> Ids) EditWall(Fixture f, string id)
    {
        var r = f.Run("studio.editWallProfile", new JsonObject { ["id"] = id })!;
        return ((string)r["viewId"]!, r["ids"]!.AsArray().Select(x => (string)x!).ToList());
    }

    static string? Top(Fixture f, string id) => f.Model.Get(id)!.Attrs["topProfile"]?.ToJsonString();

    static void Draw(Fixture f, string view, params Shape[] shapes) =>
        f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray([.. shapes.Select(s => (JsonNode)s.ToJson())]) });

    [Fact]
    public void A_wall_profile_opens_on_a_plane_along_the_wall_and_finished_unchanged_is_the_same_json()
    {
        var f = F();
        var wall = f.Wall(2, 1, 8, 1);
        f.Run("architecture.setWallProfile", new JsonObject { ["id"] = wall, ["top"] = P(0, 3, 2.5, 4.123457, 2.5, 3.2, 6, 3.2) });
        var before = Top(f, wall);
        var (view, ids) = EditWall(f, wall);
        var v = f.Model.Get(view)!;
        Assert.Equal(RefPlanes.Kind, v.Str("kind"));
        Assert.Equal(wall, (string?)v.Attrs[ProfileBoards.WallKey]!["id"]);
        Assert.Contains(wall, v.Attrs[ViewHiding.Key]!.AsArray().Select(x => (string?)x));
        // The plane's x runs along the wall from its start, its y up from its base.
        var plane = RefPlanes.Of(v);
        Assert.Equal(2, plane.Origin.X, 9);
        Assert.Equal(1, plane.Origin.Y, 9);
        Assert.Equal(1, plane.Right.X, 9);
        Assert.Equal(1, plane.Up.Z, 9);
        // The top as one polyline, its points copied; the base and the ends as construction lines.
        var top = Assert.IsType<Polyline>(Drafts.ShapeOf(f.Model.Get(ids[0])!));
        Assert.Equal(4, top.Pts.Count);
        Assert.Equal(4.123457, top.Pts[1].Y, 12);
        Assert.Equal(3, ids.Skip(1).Count(id => f.Model.Get(id)!.Str("role") == "draft"));
        f.Run("studio.finishWallProfile", new JsonObject { ["viewId"] = view });
        Assert.Null(f.Model.Get(view));
        Assert.Empty(Drafts.On(f.Model, view));
        Assert.Equal(before, Top(f, wall));
        // A flat wall finished unchanged stays flat: no profile written.
        var flat = f.Wall(0, 5, 4, 5);
        var (fv, _) = EditWall(f, flat);
        f.Run("studio.finishWallProfile", new JsonObject { ["viewId"] = fv });
        Assert.Null(Top(f, flat));
    }

    [Fact]
    public void A_top_drawn_with_lines_and_an_arc_is_written_in_one_step_and_undo_brings_the_board_back()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0);
        var (view, ids) = EditWall(f, wall);
        // The flat top erased; a line up, an arc over the middle, a line down.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(ids[0]) });
        Draw(f, view, new Line(new Vec2(0, 3), new Vec2(2, 3)), new Arc(new Vec2(3, 3), 1, 0, Math.PI), new Line(new Vec2(4, 3), new Vec2(6, 3)));
        f.Run("studio.finishWallProfile", new JsonObject { ["viewId"] = view });
        Assert.Equal("Finish wall profile", f.Session.History.UndoLabel);
        var top = f.Model.Get(wall)!.Attrs["topProfile"]!.AsArray();
        Assert.True(top.Count > 6);
        // In order from start to end, the arc's top at 4 m, laid in chords.
        var pts = top.Select(p => (U: (double)p![0]!, V: (double)p[1]!)).ToList();
        Assert.True(pts.Zip(pts.Skip(1)).All(x => x.Second.U >= x.First.U));
        Assert.Equal(4, pts.Max(p => p.V), 3);
        Assert.Equal(4, f.Bounds(wall).Max.Z, 2);
        f.Session.Undo();
        Assert.Null(Top(f, wall));
        Assert.NotNull(f.Model.Get(view));
    }

    [Fact]
    public void A_top_that_turns_back_is_open_in_pieces_or_closed_is_refused_and_the_board_stays()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0);
        var (view, ids) = EditWall(f, wall);
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(ids[0]) });
        Draw(f, view, new Polyline([new Vec2(0, 3), new Vec2(4, 3), new Vec2(3, 4), new Vec2(6, 4)], false));
        var ex = Assert.Throws<CommandException>(() => f.Run("studio.finishWallProfile", new JsonObject { ["viewId"] = view }));
        Assert.Contains("turns back", ex.Message);
        Assert.NotNull(f.Model.Get(view));
        f.Run("studio.setBoardShapes", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(new Line(new Vec2(0, 3), new Vec2(2, 3)).ToJson(), new Line(new Vec2(3, 3), new Vec2(6, 3)).ToJson()) });
        Assert.Contains("2 pieces", Assert.Throws<CommandException>(() => f.Run("studio.finishWallProfile", new JsonObject { ["viewId"] = view })).Message);
        f.Run("studio.setBoardShapes", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(new Circle(new Vec2(3, 3), 1).ToJson()) });
        Assert.Contains("closed shape", Assert.Throws<CommandException>(() => f.Run("studio.finishWallProfile", new JsonObject { ["viewId"] = view })).Message);
        f.Run("studio.setBoardShapes", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(new Line(new Vec2(0, 3), new Vec2(7, 3)).ToJson()) });
        Assert.Contains("past the wall's end", Assert.Throws<CommandException>(() => f.Run("studio.finishWallProfile", new JsonObject { ["viewId"] = view })).Message);
        // The construction lines stayed through every replacement; a step straight up is one.
        Assert.Equal(3, Drafts.On(f.Model, view).Count(d => d.Str("role") == "draft"));
        f.Run("studio.setBoardShapes", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(new Polyline([new Vec2(0, 3), new Vec2(2, 3), new Vec2(2, 1.5), new Vec2(6, 1.5)], false).ToJson()) });
        var q = f.Session.Query("studio.profileBoard", new JsonObject { ["viewId"] = view })!;
        Assert.Null(q["problem"]);
        f.Run("studio.finishWallProfile", new JsonObject { ["viewId"] = view });
        Assert.Equal("[[0,3],[2,3],[2,1.5],[6,1.5]]", Top(f, wall));
        Assert.Null(f.Model.Get(view));
    }

    [Fact]
    public void A_curtain_walls_boundary_and_a_flat_top_go_through_their_own_commands_and_cancel_drops_the_board()
    {
        var f = F();
        var cw = (string)f.Run("architecture.addCurtainWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["height"] = 3 })!["id"]!;
        var (view, _) = EditWall(f, cw);
        f.Run("studio.setBoardShapes", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(new Polyline([new Vec2(0, 3), new Vec2(3, 4.8), new Vec2(6, 3)], false).ToJson()) });
        f.Run("studio.finishWallProfile", new JsonObject { ["viewId"] = view });
        Assert.Equal(4.8, f.Model.Get(cw)!.Num("height"), 6);
        // Opened again, cancelled: as it was.
        var before = Top(f, cw);
        var (again, _) = EditWall(f, cw);
        f.Run("studio.setBoardShapes", new JsonObject { ["viewId"] = again, ["shapes"] = new JsonArray(new Line(new Vec2(0, 2), new Vec2(6, 2)).ToJson()) });
        f.Run("studio.cancelWallProfile", new JsonObject { ["viewId"] = again });
        Assert.Null(f.Model.Get(again));
        Assert.Equal(before, Top(f, cw));
        // Flat top.
        var (last, _) = EditWall(f, cw);
        f.Run("studio.finishWallProfile", new JsonObject { ["viewId"] = last, ["flat"] = true });
        Assert.Null(Top(f, cw));
    }

    static JsonObject Board(Fixture f, JsonObject args)
    {
        var r = f.Run("studio.editProfile", args)!;
        return r.AsObject();
    }

    [Fact]
    public void Every_kind_of_profile_opens_on_a_board_and_finished_unchanged_is_the_same_json()
    {
        var f = F();
        var drawn = f.Session.Query("profiles.fromRings", new JsonObject
        {
            ["rings"] = new JsonArray(P(0, 0, 50, 0, 50, 100, 0, 100), P(3, 3, 3, 97, 47, 97, 47, 3)), ["unit"] = 0.001, ["name"] = "Box 50x100", ["material"] = "aluminium",
        })!.AsObject();
        var ipe = new JsonObject { ["name"] = "IPE 200", ["kind"] = "I", ["material"] = "steel", ["h"] = 0.2, ["b"] = 0.1, ["tw"] = 0.0056, ["tf"] = 0.0085, ["r1"] = 0.012 };
        var chs = new JsonObject { ["name"] = "CHS 60.3x4", ["kind"] = "CHS", ["material"] = "steel", ["b"] = 0.0603, ["tw"] = 0.004 };
        foreach (var profile in new[] { drawn, ipe, chs })
        {
            var r = Board(f, new JsonObject { ["profile"] = profile.DeepClone(), ["library"] = "My profiles" });
            var view = (string)r["viewId"]!;
            Assert.Equal(2, Drafts.On(f.Model, view).Count(d => d.Str("role") == "draft"));
            var done = f.Run("studio.finishProfile", new JsonObject { ["viewId"] = view })!;
            Assert.Equal(profile.ToJsonString(), done["profile"]!.ToJsonString());
            Assert.False((bool)done["changed"]!);
            Assert.Equal("My profiles", (string?)done["library"]);
            Assert.Null(f.Model.Get(view));
        }
        // Renamed only: the same section under its new name.
        var renamed = Board(f, new JsonObject { ["profile"] = ipe.DeepClone() });
        var named = f.Run("studio.finishProfile", new JsonObject { ["viewId"] = (string)renamed["viewId"]!, ["name"] = "IPE 200 cut" })!["profile"]!;
        Assert.Equal("IPE 200 cut", (string?)named["name"]);
        Assert.Equal("I", (string?)named["kind"]);
    }

    [Fact]
    public void A_section_drawn_with_a_rectangle_a_circle_and_lines_and_arcs_is_made_a_centred_profile()
    {
        var f = F();
        var r = Board(f, new JsonObject { ["rings"] = new JsonArray(P(0, 0, 0.05, 0, 0.05, 0.1, 0, 0.1)), ["name"] = "Drawn", ["material"] = "aluminium" });
        var view = (string)r["viewId"]!;
        // A round chamber; a slot of two lines and two arcs.
        Draw(f, view, new Circle(new Vec2(0.025, 0.075), 0.01),
            new Line(new Vec2(0.015, 0.02), new Vec2(0.035, 0.02)), new Arc(new Vec2(0.035, 0.03), 0.01, -Math.PI / 2, Math.PI / 2),
            new Line(new Vec2(0.035, 0.04), new Vec2(0.015, 0.04)), new Arc(new Vec2(0.015, 0.03), 0.01, Math.PI / 2, 3 * Math.PI / 2));
        var q = f.Session.Query("studio.profileBoard", new JsonObject { ["viewId"] = view })!;
        Assert.Equal(3, q["rings"]!.AsArray().Count);
        Assert.Equal(0.05 * 0.1 - Math.PI * 1e-4 - (0.02 * 0.02 + Math.PI * 1e-4), (double)q["area"]!, 4);
        var done = f.Run("studio.finishProfile", new JsonObject { ["viewId"] = view, ["name"] = "Slotted", ["library"] = "Mine" })!;
        Assert.True((bool)done["changed"]!);
        Assert.Equal("Mine", (string?)done["library"]);
        var p = done["profile"]!;
        Assert.Equal("outline", (string?)p["kind"]);
        Assert.Equal("Slotted", (string?)p["name"]);
        Assert.Equal(3, p["rings"]!.AsArray().Count);
        // Centred on its box.
        var xs = p["rings"]![0]!.AsArray().Select(x => (double)x![0]!).ToList();
        Assert.Equal(0, xs.Min() + xs.Max(), 6);
        Assert.Equal("Finish profile", f.Session.History.UndoLabel);
    }

    [Fact]
    public void A_section_left_open_or_crossing_itself_is_refused_and_the_board_stays()
    {
        var f = F();
        var view = (string)Board(f, new JsonObject { ["rings"] = new JsonArray(P(0, 0, 0.05, 0, 0.05, 0.1, 0, 0.1)) })["viewId"]!;
        Draw(f, view, new Line(new Vec2(0.01, 0.01), new Vec2(0.02, 0.01)));
        Assert.Contains("not closed", Assert.Throws<CommandException>(() => f.Run("studio.finishProfile", new JsonObject { ["viewId"] = view })).Message);
        f.Run("studio.setBoardShapes", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(new Polyline([new Vec2(0, 0), new Vec2(0.05, 0.05), new Vec2(0.05, 0), new Vec2(0, 0.03)], true).ToJson()) });
        Assert.Contains("crosses itself", Assert.Throws<CommandException>(() => f.Run("studio.finishProfile", new JsonObject { ["viewId"] = view })).Message);
        Assert.NotNull(f.Model.Get(view));
        f.Run("studio.cancelProfile", new JsonObject { ["viewId"] = view });
        Assert.Null(f.Model.Get(view));
        Assert.Empty(Drafts.On(f.Model, view));
    }
}
